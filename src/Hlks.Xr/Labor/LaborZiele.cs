// XR-Labor L1 (entwurf/labor-app.md, «Testwelt der Labor-App», «Zuschnitt L1»): die Ziele der Prüfaufgaben im Testraum des Labors. Der Labor-PC schreibt sie je Etage
// in scene.extras.kicad.labor der GLB (src/Hlks.Labor/TestraumModell.cs, Katalog kataloge/labor_testraum.json); die App nimmt sie, wenn es sie gibt, statt der Ziele,
// die sie sonst im Modell sucht (PruefAufgaben.MarkierungWaehlen, WandPaare.Waehlen, PruefAufgaben.BauteilWaehlen, Raum am Stand). Ohne «labor» (KICAD) bleibt alles
// wie bisher: AusSzene gibt null.
//
// Form (Fassung 1, Koordinaten KICAD-mm absolut: x Ost, y Nord, z oben; Normalen in KICAD-Achsen):
//   { art: "hlks-labor-testraum", fassung: 1, geschoss,
//     raeume:       [{ id, kennzeichen, name, bodenMm, raumArOeffnen }],
//     markierungen: [{ id, mitteMm: [x, y, z], radiusMm }],
//     wandpaare:    [{ id, raum, sollMm, a: { wand, vonMm, bisMm, normale }, b: { … } }]   Innenfläche am Boden, Normale in den Raum
//     bauteile:     [{ id, aufgabe: "tablet-wert-ablesen", hoeheMm }] }
// Gelesen wird tolerant: ein unbrauchbarer Eintrag fällt mit Hinweis weg, der Rest gilt. Eine andere Art oder Fassung zählt nicht (Hinweis).
//
// Wahl (Regeln L1):
//   teleport-markierung   unter den Markierungen der Etage am Stand zuerst die 2 … 4.5 m vom Kopf entfernten (wie die Markierung ohne Testraum,
//                         PruefAufgaben.MarkierungAbstaende), darunter die mit dem kleinsten Winkel zum Blick (auf 0.01° gerundet), bei Gleichstand die erste
//                         im Katalog.
//   messen-wand-wand      das Wandpaar der Etage am Stand, dessen Raum man gerade betritt (RaumStandort.RaumBei), sonst das erste der Etage; Sollmass aus dem
//                         Katalog (der Labor-PC hat es gegen die Geometrie geprüft), Markierung WandPaare.MarkeHoeheM hoch.
//   raum-ar-oeffnen       der Raum mit raumArOeffnen – zuerst in der Etage am Stand, sonst irgendeiner.
//   tablet-wert-ablesen   das Bauteil der Etage am Stand (als Element der Szene).

using System.Globalization;
using System.Text.Json.Nodes;
using Hlks.Xr.Modell;
using Hlks.Xr.Netz;

namespace Hlks.Xr.Labor;

/// <summary>Boden-Markierung (teleport-markierung): Mitte (Szene x, z), Boden (Szene y), Radius der Scheibe (m).</summary>
public sealed record ZielMarkierung(string Id, string GeschossId, Xz Mitte, double Boden, double RadiusM);

/// <summary>Raum des Testraums: Id, Kennzeichen, Name, Boden (Szene y) und ob er der Raum der Aufgabe «raum-ar-oeffnen» ist.</summary>
public sealed record ZielRaum(string Id, string GeschossId, string? Kennzeichen, string? Name, double Boden, bool RaumArOeffnen);

/// <summary>Wandpaar (messen-wand-wand) mit bekanntem Sollmass.</summary>
public sealed record ZielWandPaar(string Id, string GeschossId, WandPaar Paar);

/// <summary>Bauteil einer Aufgabe (tablet-wert-ablesen) mit seiner Höhe (m).</summary>
public sealed record ZielBauteil(string Id, string GeschossId, string Aufgabe, double HoeheM);

public sealed class LaborZiele
{
    public const string Art = "hlks-labor-testraum";
    public const int Fassung = 1;
    /// <summary>Sollmass im Katalog und Abstand der Flächen dürfen so weit (mm) auseinander liegen (Rundung der Koordinaten; technisch).</summary>
    public const double SollToleranzMm = 1;

    public IReadOnlyList<ZielMarkierung> Markierungen { get; }
    public IReadOnlyList<ZielRaum> Raeume { get; }
    public IReadOnlyList<ZielWandPaar> WandPaare { get; }
    public IReadOnlyList<ZielBauteil> Bauteile { get; }
    /// <summary>Etagen, deren GLB Ziele trägt.</summary>
    public IReadOnlyList<string> Geschosse { get; }
    public IReadOnlyList<string> Hinweise { get; }

    LaborZiele(List<ZielMarkierung> m, List<ZielRaum> r, List<ZielWandPaar> w, List<ZielBauteil> b, List<string> g, List<string> h)
    {
        Markierungen = m; Raeume = r; WandPaare = w; Bauteile = b; Geschosse = g; Hinweise = h;
    }

    /// <summary>Kurz für das Protokoll der App.</summary>
    public string Kurz => $"{Geschosse.Count} Etagen, {Markierungen.Count} Markierungen, {WandPaare.Count} Wandpaare, " +
        $"Raum {Raeume.FirstOrDefault(r => r.RaumArOeffnen)?.Id ?? "–"}, {Bauteile.Count} Bauteile";

    /// <summary>Die Ziele aus den GLB der Szene (Teil «modell»); null, wenn keine GLB «labor» trägt (KICAD).</summary>
    public static LaborZiele? AusSzene(ModellSzene szene) =>
        Lesen(szene.Extras.Where(e => e.Teil == GlbZwischenspeicher.TeilModell && e.Kicad["labor"] is JsonObject).Select(e => (e.GeschossId, (JsonObject)e.Kicad["labor"]!)),
            szene.Bezugspunkt);

    /// <summary>Liest die Ziele je Etage (Geschoss-Id der Liste, scene.extras.kicad.labor); null ohne einen gültigen Teil.</summary>
    public static LaborZiele? Lesen(IEnumerable<(string GeschossId, JsonObject Labor)> teile, double[]? bezug)
    {
        var m = new List<ZielMarkierung>(); var r = new List<ZielRaum>(); var w = new List<ZielWandPaar>(); var b = new List<ZielBauteil>();
        var geschosse = new List<string>(); var h = new List<string>();
        var bz = bezug is { Length: >= 3 } ? bezug[2] : 0;
        foreach (var (g, o) in teile)
        {
            if (Text(o["art"]) != Art || Ganz(o["fassung"]) != Fassung) { h.Add($"Ziele der Etage {g}: Art bzw. Fassung unbekannt – übergangen."); continue; }
            if (Text(o["geschoss"]) is { } og && og != g) h.Add($"Ziele der Etage {g} nennen das Geschoss «{og}» – sie gelten für {g}.");
            geschosse.Add(g);
            foreach (var x in Liste(o["raeume"]))
            {
                if (Text(x["id"]) is not { } id || Zahl(x["bodenMm"]) is not { } boden) { h.Add($"Etage {g}: Raum ohne Id bzw. Boden – übergangen."); continue; }
                r.Add(new ZielRaum(id, g, Text(x["kennzeichen"]), Text(x["name"]), (boden - bz) / 1000, x["raumArOeffnen"] is JsonValue v && v.TryGetValue<bool>(out var ja) && ja));
            }
            foreach (var x in Liste(o["markierungen"]))
            {
                if (Text(x["id"]) is not { } id || Punkt(x["mitteMm"], bezug) is not { } p || Zahl(x["radiusMm"]) is not { } rad || rad <= 0)
                { h.Add($"Etage {g}: Markierung ohne Id, Mitte bzw. Radius – übergangen."); continue; }
                m.Add(new ZielMarkierung(id, g, new Xz(p.X, p.Z), p.Y, rad / 1000));
            }
            foreach (var x in Liste(o["wandpaare"]))
            {
                var id = Text(x["id"]);
                if (id is null || Text(x["raum"]) is not { } raum || Zahl(x["sollMm"]) is not { } soll || Marke(x["a"], bezug) is not { } a || Marke(x["b"], bezug) is not { } bm)
                { h.Add($"Etage {g}: Wandpaar {id ?? "ohne Id"} unvollständig – übergangen."); continue; }
                var ist = Math.Abs((bm.Punkt - a.Punkt).Skalar(a.Normale)) * 1000;
                if (a.Normale.Skalar(bm.Normale) > -0.99 || Math.Abs(ist - soll) > SollToleranzMm)
                { h.Add(string.Create(CultureInfo.InvariantCulture, $"Etage {g}: Wandpaar {id} passt nicht zu seinem Sollmass ({ist:0.#} statt {soll:0.#} mm) bzw. die Wände liegen sich nicht gegenüber – übergangen.")); continue; }
                w.Add(new ZielWandPaar(id, g, new WandPaar(raum, a, bm, soll)));
            }
            foreach (var x in Liste(o["bauteile"]))
            {
                if (Text(x["id"]) is not { } id || Text(x["aufgabe"]) is not { } aufgabe || Zahl(x["hoeheMm"]) is not { } hoehe)
                { h.Add($"Etage {g}: Bauteil ohne Id, Aufgabe bzw. Höhe – übergangen."); continue; }
                b.Add(new ZielBauteil(id, g, aufgabe, hoehe / 1000));
            }
        }
        return geschosse.Count == 0 && h.Count == 0 ? null : new LaborZiele(m, r, w, b, geschosse, h);
    }

    static IEnumerable<JsonObject> Liste(JsonNode? n) => n is JsonArray a ? a.OfType<JsonObject>() : [];
    static string? Text(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s) ? s : null;
    static int? Ganz(JsonNode? n) => n is JsonValue v && v.TryGetValue<int>(out var i) ? i : null;
    /// <summary>Jede endliche Zahl (geparstes JSON wie gebaute Werte: double, int, long, decimal).</summary>
    static double? Zahl(JsonNode? n)
    {
        if (n is not JsonValue v) return null;
        if (v.TryGetValue<double>(out var d)) return double.IsFinite(d) ? d : null;
        if (v.TryGetValue<int>(out var i)) return i;
        if (v.TryGetValue<long>(out var l)) return l;
        return v.TryGetValue<decimal>(out var m) ? (double)m : null;
    }

    static double[]? Drei(JsonNode? n) =>
        n is JsonArray a && a.Count == 3 && Zahl(a[0]) is { } x && Zahl(a[1]) is { } y && Zahl(a[2]) is { } z ? [x, y, z] : null;

    /// <summary>KICAD-mm [x, y, z] → Szene.</summary>
    static Vec3? Punkt(JsonNode? n, double[]? bezug) => Drei(n) is { } p ? KicadUmrechnung.PunktAusKicad(p[0], p[1], p[2], bezug) : null;

    /// <summary>Fläche { wand, vonMm, bisMm, normale } → WandMarke in der Szene (Normale waagrecht, Markierung WandPaare.MarkeHoeheM hoch).</summary>
    static WandMarke? Marke(JsonNode? n, double[]? bezug)
    {
        if (n is not JsonObject f || Punkt(f["vonMm"], bezug) is not { } von || Punkt(f["bisMm"], bezug) is not { } bis || Drei(f["normale"]) is not { } nk) return null;
        var normale = new Vec3(nk[0], 0, -nk[1]);   // KICAD (x, y, z) → Szene (x, z, −y); waagrecht
        if (normale.Laenge < 1e-9 || (bis - von).Laenge < 1e-6) return null;
        normale = normale.Einheit();
        return new WandMarke(von, normale, von, bis, von.Y, Labor.WandPaare.MarkeHoeheM);
    }

    static double Wd(double a)
    {
        while (a > Math.PI) a -= 2 * Math.PI;
        while (a <= -Math.PI) a += 2 * Math.PI;
        return a;
    }

    /// <summary>teleport-markierung: die Markierung der Etage (null = alle) für Kopf und Blick (siehe Dateikopf); null, wenn die Etage keine hat.</summary>
    public ZielMarkierung? MarkierungWaehlen(string? geschossId, Vec3 kopf, double blickGieren)
    {
        double min = PruefAufgaben.MarkierungAbstaende.Min(), max = PruefAufgaben.MarkierungAbstaende.Max();
        var kand = Markierungen.Select((m, i) => (m, i)).Where(x => geschossId is null || x.m.GeschossId == geschossId)
            .Select(x =>
            {
                double dx = x.m.Mitte.X - kopf.X, dz = x.m.Mitte.Z - kopf.Z;
                return (x.m, x.i, D: Num.Hypot(dx, dz), W: Math.Abs(Wd(Math.Atan2(-dz, dx) - blickGieren)));
            }).ToList();
        if (kand.Count == 0) return null;
        var imBereich = kand.Where(x => x.D >= min - 1e-9 && x.D <= max + 1e-9).ToList();
        // Winkel auf 0.01° gerundet: ein Gleichstand (z. B. zwei Markierungen symmetrisch zum Blick) hängt nicht an Rundungsfehlern, es gilt die Reihenfolge des Katalogs
        return (imBereich.Count > 0 ? imBereich : kand).OrderBy(x => Math.Round(x.W * 180 / Math.PI, 2)).ThenBy(x => x.i).First().m;
    }

    /// <summary>messen-wand-wand: das Wandpaar der Etage (siehe Dateikopf); null, wenn die Etage keines hat.</summary>
    public ZielWandPaar? WandPaarWaehlen(ModellSzene? szene, string? geschossId, Vec3 kopf, double boden)
    {
        var kand = WandPaare.Where(p => geschossId is null || p.GeschossId == geschossId).ToList();
        if (kand.Count == 0) return null;
        if (szene is not null && RaumStandort.RaumBei(szene, geschossId, new Xz(kopf.X, kopf.Z), boden) is { } raum && kand.FirstOrDefault(p => p.Paar.Raum == raum) is { } drin)
            return drin;
        return kand[0];
    }

    /// <summary>raum-ar-oeffnen: Id des markierten Raums – zuerst in der Etage, sonst irgendeiner; null ohne.</summary>
    public string? RaumArOeffnen(string? geschossId) =>
        (Raeume.FirstOrDefault(r => r.RaumArOeffnen && r.GeschossId == geschossId) ?? Raeume.FirstOrDefault(r => r.RaumArOeffnen))?.Id;

    /// <summary>tablet-wert-ablesen: das Bauteil der Etage als Element der Szene (Teil «modell»); null, wenn die Etage keines hat bzw. es nicht in der Szene ist.</summary>
    public SzenenElement? BauteilWaehlen(ModellSzene szene, string? geschossId) =>
        Bauteile.Where(b => b.Aufgabe == PruefAufgaben.TabletWertAblesen && (geschossId is null || b.GeschossId == geschossId))
            .Select(b => szene.Finde(b.Id)).FirstOrDefault(e => e is not null && !e.Box.IstLeer);
}
