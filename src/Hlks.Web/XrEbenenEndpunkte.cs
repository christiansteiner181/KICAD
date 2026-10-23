// Quest App P4b (Modul XR, entwurf/modul-xr.md «Ergänzungen»: Schnittstelle für Fachmodule – «Fachmodule legen eigene Ebenen in die Szene, z. B. animierte
// Fliessrichtung, Luftströmung als Partikel (10a/10b) oder Temperaturfelder. Der Inhalt kommt vom Server; die App zeigt Linien, Pfeile, Partikel und Farbflächen.
// Im Prototyp genügt eine Probe-Ebene.»). In Program.cs hängt nur «app.MapXrEbenen(dienst);» die Endpunkte ein.
//
//   GET /api/xr/ebenen                  { fassung: 1, projekt, ebenen: [{ name, titel, beschreibung, url, schluessel, elemente }] }
//   GET /api/xr/ebene/{name}            { art: "hlks-xr-ebene", fassung: 1, name, titel, quelle, projekt, schluessel, einheit: "mm", elemente: [...], hinweise: [...] }
//                                       ETag = "<schluessel>", Cache-Control no-cache; If-None-Match mit diesem ETag → 304. Fehler { fehler, vorschlag }: 404 (Ebene unbekannt),
//                                       409 (kein Projekt).
//   Element: { typ: linie | pfeil | punkte | partikel | flaeche, geschoss (Id wie GET /api/xr/geschosse) | null, element (Element-Id) | null, farbe "#rrggbb",
//              deckkraft?, breite? (mm), groesse? (mm), tempo? (mm/s), abstand? (mm), punkte: [[x, y, z], …] (KICAD-mm absolut: x Ost, y Nord, z oben) }.
//   Die App liest das Format mit Hlks.Xr.Ebenen.XrEbene (Beschreibung der Typen dort).
//
// Neue Ebenen eines Fachmoduls: einen XrEbenenAnbieter in XrEbenen.Anbieter eintragen (Name, Titel, Beschreibung, Funktion Projekt → Inhalt). Die Funktion läuft unter der
// Sperre des Projekts und liest nur (keine Befehle, keine Pakete).
//
// Probe-Ebene «fliessrichtung» (P4b): Pfeile und laufende Partikel entlang der Achsen aller Leitungen (Rohr, Kanal) des Modells. Herkunft jedes Werts:
//   - Richtung: Reihenfolge der Leitungspunkte, «Fliessrichtung von anfang nach ende» (kataloge/klassen.json, Klasse Leitung; leitung_ziehen: «Verlauf in
//     Fliessrichtung»). ANNAHME: für übernommene Leitungen (IFC) gilt dieselbe Reihenfolge, auch wenn sie nicht geprüft ist – die Richtung aus dem Netzdurchlauf
//     (netz_folgen) ist ein späterer Schritt.
//   - Farbe: Farbe des Mediums des Systems (kataloge/medien.json «farbeHex», HlksEinordnung.Medium/Farbe wie Plan und Schema), sonst Rohrfarbe der 3D-Ansicht.
//   - Lage: ANNAHME P4b – über der Leitung (halbe Grösse + Dämmung + 50 mm), damit die Pfeile nicht im Körper der Leitung verschwinden; senkrechte Leitungen seitlich (+x).
//     Grösse: grösster Wert aus da, durchmesser, dn, hoehe, breite (mm); ohne Angabe 100 mm.
//   - Pfeil je gerader Strecke ab 200 mm, in der Mitte, 60 % der Strecke lang, höchstens 500 mm; Breite 15 mm. Partikel: 30 mm, 500 mm/s, alle 1000 mm. Aussehen, keine Normwerte.
//   - Hat das Modell keine Leitungen (z. B. nur eine IFC-Unterlage wie Test_praxis), liefert die Ebene eine feste Probe, damit die Darstellung in der Brille prüfbar ist:
//     je Geschoss mit Räumen ein Umlauf aus vier Pfeilen und Partikeln um die Grundrisse der Räume, 2.4 m über der Kote, ohne fachliche Bedeutung (steht in quelle und hinweise).
//
// Einordnung: reine Rechner-Funktion wie xr_modell – Datenlieferung an ein Gerät des Menschen, nur lesend, kein Kern-Befehl, kein Paket; für die KI verboten
// (KiEinordnung.RechnerFunktionen «xr_ebenen»), kein MCP-Werkzeug. Herkunftsschutz wie alle /api; mit der Marke .XrImNetz() auch im Büronetz (mit Schlüssel).

using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Hlks.Kern;
using Microsoft.Net.Http.Headers;
using static Hlks.Web.EndpunktHilfe;

namespace Hlks.Web;

/// <summary>Inhalt einer Ebene: Herkunft (Text), Elemente (Format siehe Dateikopf), Hinweise.</summary>
public sealed record XrEbeneInhalt(string Quelle, JsonArray Elemente, IReadOnlyList<string> Hinweise);

/// <summary>Ein Anbieter einer Ebene (Fachmodul): Name (Muster [a-z0-9][a-z0-9_-]{0,39}), Titel, Beschreibung und die Funktion, die den Inhalt aus dem Projekt baut.</summary>
public sealed record XrEbenenAnbieter(string Name, string Titel, string Beschreibung, Func<Projekt, XrEbeneInhalt> Bauen);

public static class XrEbenen
{
    public const string Art = "hlks-xr-ebene";
    public const int Fassung = 1;
    /// <summary>So viele Elemente gibt eine Ebene höchstens aus (die App nimmt bis 50 000); mehr steht als Hinweis drin.</summary>
    public const int ElementeMax = 20_000;

    public const string Fliessrichtung = "fliessrichtung";

    /// <summary>Die Ebenen, die der Server anbietet (Fachmodule tragen sich hier ein).</summary>
    public static readonly IReadOnlyList<XrEbenenAnbieter> Anbieter =
    [
        new(Fliessrichtung, "Fliessrichtung", "Probe-Ebene: Pfeile und laufende Partikel entlang der Leitungen, in der Farbe des Mediums.", FliessrichtungBauen),
    ];

    public static XrEbenenAnbieter? Finde(string name) => Anbieter.FirstOrDefault(a => a.Name == name);

    // ------------------------------------------------------------------ Probe «fliessrichtung»

    public const double PfeilMinMm = 200, PfeilAnteil = 0.6, PfeilMaxMm = 500, PfeilBreiteMm = 15, AbstandUeberMm = 50, GroesseOhneAngabeMm = 100;
    public const double PartikelMm = 30, PartikelTempoMmS = 500, PartikelAbstandMm = 1000;

    static double R(double x) => Math.Round(x, 1);
    static JsonArray Punkt(double x, double y, double z) => [R(x), R(y), R(z)];

    /// <summary>Halbe Grösse einer Leitung samt Dämmung (mm) für den Abstand der Pfeile (siehe Dateikopf).</summary>
    public static double Radius(Element e)
    {
        var g = new[] { "da", "durchmesser", "dn", "hoehe", "breite" }.Select(n => e.Zahl(n) ?? 0).Where(v => double.IsFinite(v) && v > 0).DefaultIfEmpty(GroesseOhneAngabeMm).Max();
        var d = e.Zahl("daemmdicke") is double dd && double.IsFinite(dd) && dd > 0 ? dd : 0;
        return g / 2 + d;
    }

    static XrEbeneInhalt FliessrichtungBauen(Projekt p)
    {
        var m = p.Modell;
        var k = p.Katalog;
        var geschosse = m.DerKlasse("Geschoss").Select(g => g.Id).ToHashSet(StringComparer.Ordinal);
        var elemente = new JsonArray();
        var hinweise = new List<string>();
        int leitungen = 0, ohneMedium = 0, gekuerzt = 0;
        foreach (var e in m.Elemente.Where(x => HlksEinordnung.Art(k, x) == HlksArt.Leitung).OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            var pk = HlksEinordnung.Punkte(e);
            if (pk.Count < 2) continue;
            leitungen++;
            var geschoss = e.Geschoss is { } gs && geschosse.Contains(gs) ? gs : Routing.GeschossAusHoehe(m, pk[0][2]);
            var md = HlksEinordnung.Medium(p, e);
            var hex = HlksEinordnung.Farbe(md);
            if (hex is { Length: 9 or 5 }) hex = hex[..(hex.Length == 9 ? 7 : 4)];   // #rrggbbaa bzw. #rgba: ohne Alpha
            var farbe = XrFarben.Normiert(hex);
            if (farbe is null) { ohneMedium++; farbe = XrFarben.Hell["rohr"]; }
            var abstand = Radius(e) + AbstandUeberMm;
            // senkrechte Leitungen (alle Strecken überwiegend senkrecht): seitlich versetzt, sonst nach oben
            bool senkrecht = true;
            for (int i = 1; i < pk.Count; i++)
            {
                double dx = pk[i][0] - pk[i - 1][0], dy = pk[i][1] - pk[i - 1][1], dz = pk[i][2] - pk[i - 1][2];
                if (Math.Abs(dz) < 0.7 * Math.Sqrt(dx * dx + dy * dy + dz * dz)) { senkrecht = false; break; }
            }
            double ox = senkrecht ? abstand : 0, oz = senkrecht ? 0 : abstand;
            JsonObject Neu(string typ) => new() { ["typ"] = typ, ["geschoss"] = geschoss, ["element"] = e.Id, ["farbe"] = farbe };
            for (int i = 1; i < pk.Count; i++)
            {
                double[] a = pk[i - 1], b = pk[i];
                double dx = b[0] - a[0], dy = b[1] - a[1], dz = b[2] - a[2], l = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                if (l < PfeilMinMm) continue;
                double h = Math.Min(PfeilAnteil * l, PfeilMaxMm) / 2 / l;
                double mx = (a[0] + b[0]) / 2 + ox, my = (a[1] + b[1]) / 2, mz = (a[2] + b[2]) / 2 + oz;
                var pfeil = Neu("pfeil");
                pfeil["breite"] = PfeilBreiteMm;
                pfeil["punkte"] = new JsonArray(Punkt(mx - dx * h, my - dy * h, mz - dz * h), Punkt(mx + dx * h, my + dy * h, mz + dz * h));
                if (elemente.Count >= ElementeMax) { gekuerzt++; continue; }
                elemente.Add(pfeil);
            }
            var partikel = Neu("partikel");
            partikel["groesse"] = PartikelMm;
            partikel["tempo"] = PartikelTempoMmS;
            partikel["abstand"] = PartikelAbstandMm;
            partikel["punkte"] = new JsonArray([.. pk.Select(q => (JsonNode)Punkt(q[0] + ox, q[1], q[2] + oz))]);
            if (elemente.Count >= ElementeMax) { gekuerzt++; continue; }
            elemente.Add(partikel);
        }
        if (ohneMedium > 0) hinweise.Add($"{ohneMedium} Leitungen ohne Medium (System fehlt oder ohne Farbe): Rohrfarbe der 3D-Ansicht.");
        if (gekuerzt > 0) hinweise.Add($"{gekuerzt} Elemente weggelassen (höchstens {ElementeMax} je Ebene).");
        var quelle = "Leitungen des Modells: Richtung = Reihenfolge der Punkte (klassen.json, Leitung: «Fliessrichtung von anfang nach ende»); Farbe = Medium des Systems " +
            "(medien.json farbeHex); Pfeile 50 mm über der Leitung (Annahme P4b).";
        if (leitungen == 0)
        {
            hinweise.Add($"Keine Leitungen im Modell: Probe-Pfeile im Umlauf um die Räume jedes Geschosses ({ProbeHoeheMm / 1000:0.0} m über der Kote) – nur zum Prüfen der Darstellung.");
            quelle = "Keine Leitungen im Modell – feste Probe (P4b): je Geschoss ein Umlauf aus Pfeilen und Partikeln um die Grundrisse seiner Räume, " +
                $"{ProbeHoeheMm} mm über der Kote; ohne fachliche Bedeutung.";
            ProbeUmRaeume(p, elemente);
        }
        return new XrEbeneInhalt(quelle, elemente, hinweise);
    }

    /// <summary>Höhe der festen Probe über der Kote (mm), Annahme P4b (etwa unter der Decke).</summary>
    public const double ProbeHoeheMm = 2400;
    public const string ProbeFarbe = "#33ccff";

    /// <summary>
    /// Feste Probe für Modelle ohne Leitungen (z. B. nur IFC-Unterlage wie Test_praxis): je Geschoss mit Räumen ein Rechteck um ihre Grundrisse, gegen den Uhrzeigersinn
    /// (von oben), als vier Pfeile und ein Partikel-Umlauf.
    /// </summary>
    static void ProbeUmRaeume(Projekt p, JsonArray elemente)
    {
        var m = p.Modell;
        foreach (var g in m.DerKlasse("Geschoss").OrderBy(x => x.Zahl("kote") ?? 0))
        {
            var punkte = m.Elemente.Where(e => e.Geschoss == g.Id && p.Katalog.IstArt(e, "Raum") && e.Geometrie is { Art: "polygon", Punkte.Count: >= 3 })
                .SelectMany(e => e.Geometrie!.Punkte).Where(q => q.Length >= 2 && double.IsFinite(q[0]) && double.IsFinite(q[1])).ToList();
            if (punkte.Count == 0) continue;
            double x0 = punkte.Min(q => q[0]), x1 = punkte.Max(q => q[0]), y0 = punkte.Min(q => q[1]), y1 = punkte.Max(q => q[1]);
            var z = (g.Zahl("kote") ?? 0) + ProbeHoeheMm;
            double[][] ecken = [[x0, y0, z], [x1, y0, z], [x1, y1, z], [x0, y1, z], [x0, y0, z]];
            JsonObject Neu(string typ) => new() { ["typ"] = typ, ["geschoss"] = g.Id, ["element"] = null, ["farbe"] = ProbeFarbe };
            for (int i = 1; i < ecken.Length; i++)
            {
                double[] a = ecken[i - 1], b = ecken[i];
                double dx = b[0] - a[0], dy = b[1] - a[1], l = Math.Sqrt(dx * dx + dy * dy);
                if (l < PfeilMinMm) continue;
                double h = Math.Min(PfeilAnteil * l, PfeilMaxMm * 2) / 2 / l;
                double mx = (a[0] + b[0]) / 2, my = (a[1] + b[1]) / 2;
                var pfeil = Neu("pfeil");
                pfeil["breite"] = PfeilBreiteMm * 2;
                pfeil["punkte"] = new JsonArray(Punkt(mx - dx * h, my - dy * h, z), Punkt(mx + dx * h, my + dy * h, z));
                elemente.Add(pfeil);
            }
            var umlauf = Neu("partikel");
            umlauf["groesse"] = PartikelMm * 2;
            umlauf["tempo"] = PartikelTempoMmS * 2;
            umlauf["abstand"] = PartikelAbstandMm;
            umlauf["punkte"] = new JsonArray([.. ecken.Select(q => (JsonNode)Punkt(q[0], q[1], q[2]))]);
            elemente.Add(umlauf);
        }
    }

    // ------------------------------------------------------------------ Ausgabe

    /// <summary>Versionsschlüssel: SHA-256 über Fassung, Projekt, Name und Elemente (24 Zeichen hex).</summary>
    public static string Schluessel(string projekt, string name, JsonArray elemente) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{Fassung}|{projekt}|{name}|{elemente.ToJsonString()}")))[..24];

    /// <summary>Die Ebene im Format für die App (siehe Dateikopf).</summary>
    public static JsonObject Ebene(Projekt p, XrEbenenAnbieter a)
    {
        var inhalt = a.Bauen(p);
        return new JsonObject
        {
            ["art"] = Art, ["fassung"] = Fassung, ["name"] = a.Name, ["titel"] = a.Titel, ["quelle"] = inhalt.Quelle, ["projekt"] = p.Kopf.Id,
            ["schluessel"] = Schluessel(p.Kopf.Id, a.Name, inhalt.Elemente), ["einheit"] = "mm", ["elemente"] = inhalt.Elemente,
            ["hinweise"] = new JsonArray([.. inhalt.Hinweise.Select(h => (JsonNode)h)]),
        };
    }

    public static string Url(string name) => $"/api/xr/ebene/{name}";
}

/// <summary>Fertige Ebenen je Projekt, Stand des Modells (Aenderungszaehler) und Name; ein neuer Stand ersetzt den alten.</summary>
public sealed class XrEbenenSpeicher
{
    readonly Dictionary<string, (string Projekt, long Zaehler, JsonObject Ebene, string Text)> je = new(StringComparer.Ordinal);
    readonly object sperre = new();
    public int Gebaut { get; private set; }

    public (JsonObject Ebene, string Text) Holen(Projekt p, XrEbenenAnbieter a)
    {
        lock (sperre)
            if (je.TryGetValue(a.Name, out var da) && da.Projekt == p.Kopf.Id && da.Zaehler == p.Modell.Aenderungszaehler) return (da.Ebene, da.Text);
        var e = XrEbenen.Ebene(p, a);
        var t = e.ToJsonString();
        lock (sperre)
        {
            je[a.Name] = (p.Kopf.Id, p.Modell.Aenderungszaehler, e, t);
            Gebaut++;
        }
        return (e, t);
    }
}

public static class XrEbenenEndpunkte
{
    public static WebApplication MapXrEbenen(this WebApplication app, ProjektDienst dienst)
    {
        var speicher = new XrEbenenSpeicher();
        var g = app.MapGroup("/api/xr");

        g.MapGet("/ebenen", () => Gesichert(async () =>
        {
            var liste = await dienst.Mit(p => new JsonObject
            {
                ["fassung"] = XrEbenen.Fassung,
                ["projekt"] = p.Kopf.Id,
                ["ebenen"] = new JsonArray([.. XrEbenen.Anbieter.Select(a =>
                {
                    var (e, _) = speicher.Holen(p, a);
                    return (JsonNode)new JsonObject
                    {
                        ["name"] = a.Name, ["titel"] = a.Titel, ["beschreibung"] = a.Beschreibung, ["url"] = XrEbenen.Url(a.Name),
                        ["schluessel"] = (string?)e["schluessel"], ["elemente"] = e["elemente"]!.AsArray().Count,
                    };
                })]),
            });
            return Results.Text(liste.ToJsonString(), "application/json; charset=utf-8");
        })).XrImNetz();   // auch im Büronetz (mit Schlüssel)

        g.MapGet("/ebene/{name}", (string name, HttpContext ctx) => Gesichert(async () =>
        {
            var a = XrEbenen.Finde(name);
            if (a is null)
                return Antwort(new { fehler = $"Ebene «{name}» gibt es nicht.", vorschlag = "Namen aus GET /api/xr/ebenen verwenden." }, 404);
            var (e, text) = await dienst.Mit(p => speicher.Holen(p, a));
            var etag = new EntityTagHeaderValue($"\"{(string)e["schluessel"]!}\"");
            ctx.Response.Headers.ETag = etag.ToString();
            ctx.Response.Headers.CacheControl = "no-cache";
            if (Passt(ctx.Request, etag)) return Results.StatusCode(StatusCodes.Status304NotModified);
            return Results.Text(text, "application/json; charset=utf-8");
        })).XrImNetz();   // auch im Büronetz (mit Schlüssel)
        return app;
    }

    /// <summary>If-None-Match enthält diesen ETag (schwacher Vergleich) oder «*» – wie bei den GLB (XrModellEndpunkte).</summary>
    static bool Passt(HttpRequest r, EntityTagHeaderValue etag)
    {
        var kopf = r.Headers.IfNoneMatch;
        if (kopf.Count == 0) return false;
        if (!EntityTagHeaderValue.TryParseList(kopf, out var liste)) return false;
        return liste.Any(x => x.Equals(EntityTagHeaderValue.Any) || x.Compare(etag, useStrongComparison: false));
    }
}
