// XR-Labor L1 (entwurf/labor-app.md, «Testwelt der Labor-App», «Zuschnitt L1»): der Testraum als GLB je Etage – im selben Format wie KICAD (src/Hlks.Kern/Xr/
// XrModell.cs, Quest App P2), damit die Brillen-App ihn unverändert lädt. Geschrieben mit dem GlbSchreiber des Kerns; die Körper sind Körperteile des Kerns
// (quader, prisma, zylinder) und gehen über XrNetzbau und XrPuffer in die Netze, wie in XrModell.Modell.
//
// Gleich wie KICAD:
//   - Koordinaten (m, y oben, x Ost, z −Nord) relativ zum Bezugspunkt (XrKoordinaten.Bezugspunkt über alle Körper; der Testraum liegt am Nullpunkt → 0, 0, 0).
//   - Je Etage ein Netz je (Gruppe, Farbe, Deckkraft in VR, Deckkraft 3D), undurchsichtige zuerst; Farben, Deckkraft und Gruppen nach XrFarben aus Material und
//     Ebene der Körperteile (wand → Ebene «wand», boden/decke → «decke», raum → «raum» mit Deckkraft 0.15 wie die Räume des Kerns, markierung → «sonst», als flacher
//     Ring: Prisma mit Loch).
//   - mesh.extras.kicad { gruppe, farbe, deckkraft, deckkraft3d, dreiecke, elemente { ids, ab, anzahl, ebenen } }.
//   - scene.extras.kicad { fassung, teil, schluessel, projekt, geschoss { id, kennzeichen, name, koteMm, hoeheMm }, bezugspunktMm, achsen, netze, dreiecke,
//     elemente, hinweise }, asset.extras.kicad { format «kicad-xr-glb», fassung }.
//   - Versionsschlüssel je Etage und Teil (SHA-256, 24 Zeichen) aus dem Inhalt der Etage; nur der Teil «modell» (keine IFC-Unterlage, unterlage = null).
// Zusätzlich: scene.extras.kicad.labor – die Ziele der Prüfaufgaben in dieser Etage (Form siehe <see cref="TestraumModell.Ziele"/> und
// src/Hlks.Labor.Quest/LIESMICH.md; die App liest sie mit Hlks.Xr.Labor.LaborZiele).

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Hlks.Kern;

namespace Hlks.Labor;

/// <summary>Eine Etage des Testraums mit Versionsschlüssel, Zahl der Elemente und der fertigen GLB.</summary>
public sealed record TestraumEtage(TestraumGeschoss Geschoss, string Schluessel, int Elemente, byte[] Glb, int Netze, long Dreiecke);

/// <summary>Der gebaute Testraum: Projekt, Bezugspunkt, Etagen (aufsteigend nach Kote) und der Katalog, aus dem er stammt.</summary>
public sealed record TestraumStand(TestraumKatalog Katalog, double[] Bezugspunkt, IReadOnlyList<TestraumEtage> Etagen)
{
    public string Projekt => Katalog.Projekt;

    /// <summary>Etage nach Id oder Kennzeichen (Gross-/Kleinschreibung beim Kennzeichen egal), wie XrStand.Geschoss.</summary>
    public TestraumEtage? Etage(string bezug) =>
        Etagen.FirstOrDefault(e => e.Geschoss.Id == bezug) ?? Etagen.FirstOrDefault(e => string.Equals(e.Geschoss.Kennzeichen, bezug, StringComparison.OrdinalIgnoreCase));
}

public static class TestraumModell
{
    /// <summary>Fassung des GLB-Formats wie KICAD (XrModell.Fassung; der Test hält die Gleichheit fest).</summary>
    public const int Fassung = 1;
    /// <summary>Fassung der Form von scene.extras.kicad.labor (die App nimmt nur diese).</summary>
    public const int LaborFassung = 1;
    public const string TeilModell = "modell";
    /// <summary>Höhe der Markierung: flacher Ring von 1 bis 6 mm über OK Boden (Annahme L1, siehe Katalog; ohne gemeinsame Fläche mit dem Boden).</summary>
    public const double MarkierungUntenMm = 1, MarkierungObenMm = 6;
    /// <summary>Innenradius des Rings als Anteil des Radius (Annahme L1 nach den ersten Prüfbildern: eine volle Scheibe in der Akzentfarbe wirkte wie ein Loch im Boden).</summary>
    public const double MarkierungInnen = 0.75;
    /// <summary>Ecken je Kreis des Rings (wie XrNetzbau.Rund für Zylinder: Sehnenfehler unter 1 % des Radius).</summary>
    public const int MarkierungEcken = 32;
    /// <summary>Deckkraft der Räume wie im Kern (ArchitekturGrafik: Raum «Deckkraft 0.15»).</summary>
    public const double RaumDeckkraft = 0.15;

    /// <summary>Ebene der GLB (wie Koerper.Ebene im Kern) zu einer Art des Katalogs.</summary>
    public static string Ebene(string art) => art switch
    {
        TestraumKatalog.ArtWand => "wand",
        TestraumKatalog.ArtBoden or TestraumKatalog.ArtDecke => "decke",
        TestraumKatalog.ArtRaum => "raum",
        _ => "sonst",
    };

    /// <summary>Körperteil des Kerns zu einem Element (Farben aus dem Katalog; Räume mit der Materialfarbe des Kerns).</summary>
    public static Koerperteil Teil(TestraumKatalog k, TestraumElement e)
    {
        switch (e.Art)
        {
            case TestraumKatalog.ArtWand:
                double dx = e.Bis![0] - e.Von![0], dy = e.Bis[1] - e.Von[1];
                return new Koerperteil
                {
                    Art = "quader", Material = "wand", Farbe = k.Farben["wand"],
                    Mitte = [(e.Von[0] + e.Bis[0]) / 2, (e.Von[1] + e.Bis[1]) / 2, (e.UntenMm + e.ObenMm) / 2],
                    Masse = [Math.Sqrt(dx * dx + dy * dy), e.DickeMm, e.ObenMm - e.UntenMm],
                    Drehung = Math.Atan2(dy, dx) * 180 / Math.PI,
                };
            case TestraumKatalog.ArtBoden or TestraumKatalog.ArtDecke:
                return new Koerperteil { Art = "prisma", Material = "decke", Farbe = k.Farben["platte"], Grundriss = [.. e.Umriss!], Unten = e.UntenMm, Oben = e.ObenMm };
            case TestraumKatalog.ArtRaum:
                return new Koerperteil { Art = "prisma", Material = "raum", Deckkraft = RaumDeckkraft, Grundriss = [.. e.Umriss!], Unten = e.UntenMm, Oben = e.ObenMm, NichtSchneiden = true };
            default:   // markierung: flacher Ring (Prisma mit Loch)
                List<double[]> Kreis(double r) =>
                    [.. Enumerable.Range(0, MarkierungEcken).Select(i => new[] { e.Mitte![0] + r * Math.Cos(2 * Math.PI * i / MarkierungEcken), e.Mitte[1] + r * Math.Sin(2 * Math.PI * i / MarkierungEcken) })];
                return new Koerperteil
                {
                    Art = "prisma", Material = "sonst", Farbe = k.Farben["markierung"], Grundriss = Kreis(e.RadiusMm), Loecher = [Kreis(e.RadiusMm * MarkierungInnen)],
                    Unten = e.UntenMm + MarkierungUntenMm, Oben = e.UntenMm + MarkierungObenMm,
                };
        }
    }

    /// <summary>Hüllbox [minx, miny, minz, maxx, maxy, maxz] eines Elements (mm), für den Bezugspunkt.</summary>
    static double[] Huelle(TestraumElement e)
    {
        if (e.IstMarkierung) return [e.Mitte![0] - e.RadiusMm, e.Mitte[1] - e.RadiusMm, e.UntenMm, e.Mitte[0] + e.RadiusMm, e.Mitte[1] + e.RadiusMm, e.UntenMm + MarkierungObenMm];
        if (e.IstWand)
        {
            var h = e.DickeMm / 2;   // grob, aber sicher grösser (Ecken der gedrehten Wand)
            return [Math.Min(e.Von![0], e.Bis![0]) - h, Math.Min(e.Von[1], e.Bis[1]) - h, e.UntenMm, Math.Max(e.Von[0], e.Bis[0]) + h, Math.Max(e.Von[1], e.Bis[1]) + h, e.ObenMm];
        }
        return [e.Umriss!.Min(p => p[0]), e.Umriss!.Min(p => p[1]), e.UntenMm, e.Umriss!.Max(p => p[0]), e.Umriss!.Max(p => p[1]), e.ObenMm];
    }

    static string Z(double v) => v.ToString("R", CultureInfo.InvariantCulture);
    /// <summary>Zahlen auf 0.001 mm gerundet, ohne «−0» (Normalen quer zur Achse).</summary>
    static JsonArray P(params double[] werte) => new([.. werte.Select(v => (JsonNode)JsonValue.Create(Math.Round(v, 3) is var r && r == 0 ? 0.0 : r))]);

    /// <summary>
    /// Ziele der Prüfaufgaben in einer Etage (scene.extras.kicad.labor), Koordinaten in KICAD-mm absolut (x Ost, y Nord, z oben), Normalen in KICAD-Achsen:
    /// <code>
    /// { art: "hlks-labor-testraum", fassung: 1, geschoss: Id,
    ///   raeume:       [{ id, kennzeichen, name, bodenMm, raumArOeffnen }],
    ///   markierungen: [{ id, mitteMm: [x, y, z], radiusMm }],
    ///   wandpaare:    [{ id, raum, sollMm, a: Fläche, b: Fläche }]   Fläche = { wand, vonMm: [x, y, z], bisMm: [x, y, z], normale: [x, y, z] } (Innenfläche am Boden, Normale in den Raum)
    ///   bauteile:     [{ id, aufgabe: "tablet-wert-ablesen", hoeheMm }] }
    /// </code>
    /// Nur Ziele, die zu dieser Etage gehören (Markierung, Raum, Wandpaar über seinen Raum, Bauteil); die Reihenfolge ist die des Katalogs.
    /// </summary>
    public static JsonObject Ziele(TestraumKatalog k, TestraumGeschoss g)
    {
        var raeume = new JsonArray();
        foreach (var r in k.Elemente.Where(e => e.IstRaum && e.Geschoss == g.Id))
            raeume.Add(new JsonObject
            {
                ["id"] = r.Id, ["kennzeichen"] = r.Kennzeichen, ["name"] = r.Name, ["bodenMm"] = r.UntenMm, ["raumArOeffnen"] = k.RaumArOeffnenRaum == r.Id,
            });
        var markierungen = new JsonArray();
        foreach (var id in k.Markierungen)
            if (k.Element(id) is { } m && m.Geschoss == g.Id)
                markierungen.Add(new JsonObject { ["id"] = m.Id, ["mitteMm"] = P(m.Mitte![0], m.Mitte[1], m.UntenMm), ["radiusMm"] = m.RadiusMm });
        var paare = new JsonArray();
        foreach (var p in k.WandPaare)
        {
            var raum = k.Element(p.Raum)!;
            if (raum.Geschoss != g.Id) continue;
            var (fa, fb) = TestraumKatalog.Flaechen(k.Element(p.A)!, k.Element(p.B)!, raum)!.Value;
            static JsonObject F(TestraumWandFlaeche f) => new() { ["wand"] = f.Wand, ["vonMm"] = P(f.Von), ["bisMm"] = P(f.Bis), ["normale"] = P(f.Normale) };
            paare.Add(new JsonObject { ["id"] = p.Id, ["raum"] = p.Raum, ["sollMm"] = p.SollMm, ["a"] = F(fa), ["b"] = F(fb) });
        }
        var bauteile = new JsonArray();
        foreach (var b in k.Bauteile)
            if (k.Element(b.Bauteil) is { } e && e.Geschoss == g.Id)
                bauteile.Add(new JsonObject { ["id"] = b.Bauteil, ["aufgabe"] = TestraumKatalog.TabletWertAblesen, ["hoeheMm"] = b.HoeheMm });
        return new JsonObject
        {
            ["art"] = TestraumKatalog.ArtKatalog, ["fassung"] = LaborFassung, ["geschoss"] = g.Id,
            ["raeume"] = raeume, ["markierungen"] = markierungen, ["wandpaare"] = paare, ["bauteile"] = bauteile,
        };
    }

    /// <summary>Baut alle Etagen (GLB im Speicher; der Testraum ist klein). Wirft HlksFehler, wenn ein Körper nicht gezeichnet werden kann.</summary>
    public static TestraumStand Bauen(TestraumKatalog k)
    {
        var bezug = XrKoordinaten.Bezugspunkt(k.Elemente.Select(Huelle));
        var etagen = new List<TestraumEtage>();
        foreach (var g in k.Geschosse) etagen.Add(Etage(k, g, bezug));
        return new TestraumStand(k, bezug, etagen);
    }

    static TestraumEtage Etage(TestraumKatalog k, TestraumGeschoss g, double[] bezug)
    {
        var puffer = new Dictionary<(string Gruppe, string Farbe, double Vr, double D3), XrPuffer>();
        int elemente = 0;
        var inhalt = new StringBuilder();
        foreach (var e in k.Elemente.Where(e => e.Geschoss == g.Id).OrderBy(e => e.Id, StringComparer.Ordinal))   // nach Id wie XrModell
        {
            var t = Teil(k, e);
            var ebene = Ebene(e.Art);
            var farbe = XrFarben.Farbe(t);
            var d3 = XrFarben.Deckkraft3d(t);
            var vr = XrFarben.DeckkraftVr(ebene, d3);
            var key = (XrFarben.Gruppe(ebene), farbe, vr, d3);
            if (!puffer.TryGetValue(key, out var p)) puffer[key] = p = new XrPuffer(bezug);
            int ab = p.Dreiecke;
            if (!XrNetzbau.Teil(t, p)) throw new HlksFehler($"Testraum: Element «{e.Id}» lässt sich nicht zeichnen.", $"kataloge/{TestraumKatalog.Datei} prüfen.");
            p.Zuordnen(e.Id, ebene, ab);
            elemente++;
            inhalt.Append($"e|{e.Id}|{ebene}|{Json.Text(t)}|{farbe}|{Z(d3)}|{Z(vr)}\n");
        }
        var ziele = Ziele(k, g);
        inhalt.Append("labor|").Append(ziele.ToJsonString()).Append('\n');
        var schluessel = Schluessel(k.Projekt, bezug, g, inhalt.ToString());

        var netze = puffer.Where(x => x.Value.Dreiecke > 0)
            .OrderBy(x => x.Key.Vr < 1 ? 1 : 0).ThenBy(x => XrFarben.Rang(x.Key.Gruppe)).ThenBy(x => x.Key.Farbe, StringComparer.Ordinal).ThenByDescending(x => x.Key.Vr).ThenByDescending(x => x.Key.D3)
            .Select(x =>
            {
                var (gruppe, farbe, vr, d3) = x.Key;
                var name = $"{gruppe} {farbe} {D(vr)}" + (d3 != vr ? $" (3D {D(d3)})" : "");
                var extras = new JsonObject
                {
                    ["kicad"] = new JsonObject
                    {
                        ["gruppe"] = gruppe, ["farbe"] = farbe, ["deckkraft"] = vr, ["deckkraft3d"] = d3, ["dreiecke"] = x.Value.Dreiecke,
                        ["elemente"] = Spalten(x.Value.Bereiche),
                    },
                };
                return x.Value.Netz(name, new GlbMaterial(name, farbe, vr), extras);
            }).ToList();
        var szene = new JsonObject
        {
            ["kicad"] = new JsonObject
            {
                ["fassung"] = Fassung, ["teil"] = TeilModell, ["schluessel"] = schluessel, ["projekt"] = k.Projekt,
                ["geschoss"] = GeschossJson(g),
                ["bezugspunktMm"] = new JsonArray(bezug[0], bezug[1], bezug[2]),
                ["achsen"] = XrKoordinaten.Achsen,
                ["netze"] = netze.Count, ["dreiecke"] = netze.Sum(n => (long)n.Dreiecke), ["elemente"] = elemente,
                ["hinweise"] = new JsonArray(),
                ["labor"] = ziele,
            },
        };
        var asset = new JsonObject { ["kicad"] = new JsonObject { ["format"] = "kicad-xr-glb", ["fassung"] = Fassung } };
        var glb = GlbSchreiber.Schreiben(netze, $"{g.Kennzeichen} {TeilModell}", szene, asset);
        return new TestraumEtage(g, schluessel, elemente, glb, netze.Count, netze.Sum(n => (long)n.Dreiecke));
    }

    static string D(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    static JsonObject GeschossJson(TestraumGeschoss g) => new()
    {
        ["id"] = g.Id, ["kennzeichen"] = g.Kennzeichen, ["name"] = g.Name, ["koteMm"] = g.KoteMm, ["hoeheMm"] = g.HoeheMm,
    };

    static JsonObject Spalten(IReadOnlyList<XrBereich> b) => new()
    {
        ["ids"] = new JsonArray([.. b.Select(x => (JsonNode)JsonValue.Create(x.Id))]),
        ["ab"] = new JsonArray([.. b.Select(x => (JsonNode)JsonValue.Create(x.Ab))]),
        ["anzahl"] = new JsonArray([.. b.Select(x => (JsonNode)JsonValue.Create(x.Anzahl))]),
        ["ebenen"] = new JsonArray([.. b.Select(x => (JsonNode?)JsonValue.Create(x.Zusatz))]),
    };

    /// <summary>Versionsschlüssel einer Etage (24 Zeichen hex), wie XrModell: Kopfzeile mit Fassung, Teil, Projekt, Bezugspunkt und Geschoss, dann der Inhalt.</summary>
    static string Schluessel(string projekt, double[] bezug, TestraumGeschoss g, string inhalt)
    {
        var kopf = $"labor-testraum|fassung={Fassung}|labor={LaborFassung}|teil={TeilModell}|projekt={projekt}|bezug={Z(bezug[0])},{Z(bezug[1])},{Z(bezug[2])}|" +
                   $"geschoss={g.Id}|{g.Kennzeichen}|{g.Name}|{Z(g.KoteMm)}|{Z(g.HoeheMm)}\n";
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(kopf + inhalt)))[..24];
    }

    /// <summary>Adresse der GLB wie KICAD (XrModellEndpunkte.Url).</summary>
    public static string Url(string geschossId) => $"/api/xr/geschoss/{Uri.EscapeDataString(geschossId)}.glb?teil={TeilModell}";

    /// <summary>Liste der Etagen für GET /api/xr/geschosse – derselbe Aufbau wie XrModell.Liste (unterlage = null).</summary>
    public static JsonObject Liste(TestraumStand s) => new()
    {
        ["fassung"] = Fassung,
        ["projekt"] = s.Projekt,
        ["bezugspunktMm"] = new JsonArray(s.Bezugspunkt[0], s.Bezugspunkt[1], s.Bezugspunkt[2]),
        ["achsen"] = XrKoordinaten.Achsen,
        ["geschosse"] = new JsonArray([.. s.Etagen.Select(e => (JsonNode)new JsonObject
        {
            ["id"] = e.Geschoss.Id, ["kennzeichen"] = e.Geschoss.Kennzeichen, ["name"] = e.Geschoss.Name,
            ["koteMm"] = e.Geschoss.KoteMm, ["hoeheMm"] = e.Geschoss.HoeheMm,
            ["modell"] = new JsonObject { ["schluessel"] = e.Schluessel, ["elemente"] = e.Elemente, ["url"] = Url(e.Geschoss.Id) },
            ["unterlage"] = null,
        })]),
        ["hinweise"] = new JsonArray(),
    };
}
