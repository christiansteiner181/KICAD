// XR-Labor L1 (entwurf/labor-app.md, «Testwelt der Labor-App», «Zuschnitt L1»; Entscheid Christian 26.10.2026: «Nur neutraler Testraum»): der Katalog des
// Testraums (kataloge/labor_testraum.json, Art «hlks-labor-testraum», Fassung 1) lesen und prüfen. Aus ihm baut TestraumModell.cs die GLB je Etage.
//
// Aufbau (Koordinaten KICAD-mm absolut: x Ost, y Nord, z oben):
//   geschosse  [{ id, kennzeichen, name, koteMm, hoeheMm }]                                  aufsteigend nach Kote, Ids eindeutig und als Dateiname tauglich
//   elemente   [{ id, art, geschoss, name?, … }]                                              Ids eindeutig (Muster wie ein Kennzeichen, VrStartLogik)
//     art wand        vonMm [x, y], bisMm [x, y], dickeMm, untenMm, obenMm                    Achse wie die Wandgeometrie «linie» von KICAD (Dicke beidseits halb)
//     art boden|decke umrissMm [[x, y] …], untenMm, obenMm                                    Platte; in der GLB Ebene «decke» (Klasse Decke des Kerns)
//     art raum        umrissMm, untenMm, obenMm, kennzeichen?                                  Raumkörper; in der GLB Gruppe «raum» (wie KICAD, Deckkraft 0.15)
//     art markierung  mitteMm [x, y], radiusMm                                                 flacher Ring auf OK Boden des Geschosses (Ebene «sonst»)
//   ziele      { <Id der Prüfaufgabe>: { … } }                                                nur Ids aus kataloge/xr_pruefaufgaben.json (Prüfung im Test)
//     teleport-markierung { markierungen: [Id] }
//     messen-wand-wand    { wandpaare: [{ id, raum, a, b, sollMm }] }                          a, b: parallele Wände gegenüber; sollMm = Abstand der Innenflächen
//     raum-ar-oeffnen     { raum }
//     tablet-wert-ablesen { bauteile: [{ bauteil, hoeheMm }] }                                hoeheMm = Höhe des Bauteils (wie die App sie aus der Hüllbox rechnet)
// Prüfung: Kopf (art, fassung), Werte endlich, Verweise (Geschoss, Raum, Wände, Bauteile), Lage (Markierung im Raum ihrer Etage), Sollmass und Höhe gegen die
// Geometrie (höchstens SollToleranzMm). Fehler: HlksFehler mit Ort im Katalog und Vorschlag – der Labor-PC liefert dann keinen Testraum (GET /api/xr/geschosse 409).
// Annahmen und Herkunft der Masse stehen im Katalog selbst («annahmen», «quelle»); hier stehen keine Werte fest.

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Hlks.Kern;

namespace Hlks.Labor;

/// <summary>Etage des Testraums (KICAD-mm).</summary>
public sealed record TestraumGeschoss(string Id, string Kennzeichen, string Name, double KoteMm, double HoeheMm);

/// <summary>Ein Element des Testraums (Felder je Art, siehe Dateikopf; Punkte [x, y] in KICAD-mm).</summary>
public sealed record TestraumElement(string Id, string Art, string Geschoss, string? Name, string? Kennzeichen,
    double[]? Von, double[]? Bis, double DickeMm, IReadOnlyList<double[]>? Umriss, double UntenMm, double ObenMm, double[]? Mitte, double RadiusMm)
{
    public bool IstWand => Art == TestraumKatalog.ArtWand;
    public bool IstPlatte => Art is TestraumKatalog.ArtBoden or TestraumKatalog.ArtDecke;
    public bool IstRaum => Art == TestraumKatalog.ArtRaum;
    public bool IstMarkierung => Art == TestraumKatalog.ArtMarkierung;
}

/// <summary>Wandpaar der Aufgabe «messen-wand-wand».</summary>
public sealed record TestraumWandPaar(string Id, string Raum, string A, string B, double SollMm);

/// <summary>Bauteil der Aufgabe «tablet-wert-ablesen» mit seiner Höhe (mm).</summary>
public sealed record TestraumBauteil(string Bauteil, double HoeheMm);

/// <summary>Innenfläche einer Wand am Boden: Anfang und Ende (KICAD-mm, z = Boden), Normale in den Raum (waagrecht, KICAD-Achsen).</summary>
public sealed record TestraumWandFlaeche(string Wand, double[] Von, double[] Bis, double[] Normale);

/// <summary>Der geprüfte Katalog des Testraums.</summary>
public sealed class TestraumKatalog
{
    public const string Datei = "labor_testraum.json";
    public const string ArtKatalog = "hlks-labor-testraum";
    public const int FassungKatalog = 1;
    public const string ArtWand = "wand", ArtBoden = "boden", ArtDecke = "decke", ArtRaum = "raum", ArtMarkierung = "markierung";
    public static readonly IReadOnlyList<string> Arten = [ArtWand, ArtBoden, ArtDecke, ArtRaum, ArtMarkierung];
    /// <summary>Ids der Prüfaufgaben mit Zielen im Katalog (kataloge/xr_pruefaufgaben.json; der Test vergleicht die Liste mit dem Katalog der Aufgaben).</summary>
    public const string TeleportMarkierung = "teleport-markierung", MessenWandWand = "messen-wand-wand", RaumArOeffnen = "raum-ar-oeffnen", TabletWertAblesen = "tablet-wert-ablesen";
    /// <summary>Sollmass und Höhe dürfen so weit (mm) von der Geometrie abweichen (Rundung; technisch, kein Normwert).</summary>
    public const double SollToleranzMm = 0.5;

    static readonly Regex IdMuster = new(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,39}\z", RegexOptions.CultureInvariant);   // wie VrStartLogik.KennzeichenMuster
    static readonly Regex GeschossMuster = new(@"^[A-Za-z0-9_-][A-Za-z0-9._-]{0,39}\z", RegexOptions.CultureInvariant);   // auch als Dateiname der App (GlbZwischenspeicher.Sicher)
    static readonly Regex FarbMuster = new("^#[0-9A-Fa-f]{6}$", RegexOptions.CultureInvariant);

    public required string Projekt { get; init; }
    public required IReadOnlyList<TestraumGeschoss> Geschosse { get; init; }
    public required IReadOnlyList<TestraumElement> Elemente { get; init; }
    /// <summary>Farben (#rrggbb) je Rolle: wand, platte, markierung.</summary>
    public required IReadOnlyDictionary<string, string> Farben { get; init; }
    public required IReadOnlyList<string> Markierungen { get; init; }
    public required IReadOnlyList<TestraumWandPaar> WandPaare { get; init; }
    public required string? RaumArOeffnenRaum { get; init; }
    public required IReadOnlyList<TestraumBauteil> Bauteile { get; init; }
    /// <summary>Ids der Prüfaufgaben, die im Katalog unter «ziele» stehen (auch ohne Ziel, nur mit Erklärung).</summary>
    public required IReadOnlyList<string> AufgabenMitEintrag { get; init; }
    /// <summary>SHA-256 des Dateitexts (24 Zeichen), für Hinweise und Tests.</summary>
    public required string Schluessel { get; init; }

    public TestraumElement? Element(string id) => Elemente.FirstOrDefault(e => e.Id == id);
    public TestraumGeschoss? Geschoss(string id) => Geschosse.FirstOrDefault(g => g.Id == id);

    /// <summary>Liest den Katalog aus dem Ordner der Kataloge (wie KICAD: --katalog, HLKS_KATALOG, …). Wirft HlksFehler, wenn er fehlt oder ungültig ist.</summary>
    public static TestraumKatalog Laden(string ordner)
    {
        var pfad = Path.Combine(ordner, Datei);
        if (!File.Exists(pfad)) throw new HlksFehler($"Der Katalog des Testraums fehlt ({pfad}).", $"kataloge/{Datei} in den Ordner der Kataloge legen (HLKS_KATALOG bzw. --katalog).");
        return Lesen(File.ReadAllText(pfad, Encoding.UTF8));
    }

    static HlksFehler F(string ort, string meldung) => new($"Katalog des Testraums, {ort}: {meldung}", $"kataloge/{Datei} korrigieren (Aufbau im Kopf von src/Hlks.Labor/TestraumKatalog.cs).");

    static string Text(JsonNode? n, string ort, bool pflicht = true)
    {
        if (n is JsonValue v && v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s)) return s.Trim();
        if (!pflicht && n is null) return "";
        throw F(ort, "Text fehlt.");
    }

    static double Zahl(JsonNode? n, string ort)
    {
        if (n is JsonValue v && v.TryGetValue<double>(out var d) && double.IsFinite(d)) return d;
        throw F(ort, "Zahl fehlt oder ist ungültig.");
    }

    static double[] Punkt(JsonNode? n, string ort)
    {
        if (n is JsonArray a && a.Count == 2) return [Zahl(a[0], ort + "[0]"), Zahl(a[1], ort + "[1]")];
        throw F(ort, "Punkt [x, y] in mm erwartet.");
    }

    static List<double[]> Umriss(JsonNode? n, string ort)
    {
        if (n is not JsonArray a || a.Count < 3) throw F(ort, "Umriss mit mindestens drei Punkten [x, y] erwartet.");
        var r = a.Select((p, i) => Punkt(p, $"{ort}[{i}]")).ToList();
        if (Math.Abs(Flaeche(r)) < 1) throw F(ort, "Der Umriss hat keine Fläche.");
        return r;
    }

    /// <summary>Fläche eines Rings (mm², Gegenuhrzeigersinn positiv).</summary>
    public static double Flaeche(IReadOnlyList<double[]> r)
    {
        double f = 0;
        for (int i = 0; i < r.Count; i++) { var a = r[i]; var b = r[(i + 1) % r.Count]; f += a[0] * b[1] - b[0] * a[1]; }
        return f / 2;
    }

    /// <summary>Liegt der Punkt im Ring (Strahlverfahren; auf dem Rand zählt nicht sicher)?</summary>
    public static bool Innen(IReadOnlyList<double[]> r, double x, double y)
    {
        bool innen = false;
        for (int i = 0, j = r.Count - 1; i < r.Count; j = i++)
            if ((r[i][1] > y) != (r[j][1] > y) && x < (r[j][0] - r[i][0]) * (y - r[i][1]) / (r[j][1] - r[i][1]) + r[i][0]) innen = !innen;
        return innen;
    }

    /// <summary>Liest und prüft den Text des Katalogs (siehe Dateikopf). Wirft HlksFehler.</summary>
    public static TestraumKatalog Lesen(string text)
    {
        JsonObject o;
        try { o = JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, MaxDepth = 32 }) as JsonObject ?? throw F("Datei", "kein JSON-Objekt."); }
        catch (JsonException ex) { throw F("Datei", "kein gültiges JSON (" + ex.Message + ")."); }
        if (Text(o["art"], "art") != ArtKatalog) throw F("art", $"«{ArtKatalog}» erwartet.");
        if (o["fassung"] is not JsonValue fv || !fv.TryGetValue<int>(out var fassung) || fassung != FassungKatalog) throw F("fassung", $"Fassung {FassungKatalog} erwartet.");
        var projekt = Text(o["projekt"], "projekt");
        if (!GeschossMuster.IsMatch(projekt)) throw F("projekt", "nur Buchstaben, Ziffern, Punkt, Strich und Unterstrich (höchstens 40 Zeichen).");

        // Farben
        var farben = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var rolle in new[] { "wand", "platte", "markierung" })
        {
            var f = Text(o["farben"]?[rolle]?["farbe"], $"farben.{rolle}.farbe");
            if (!FarbMuster.IsMatch(f)) throw F($"farben.{rolle}.farbe", "Farbe #rrggbb erwartet.");
            farben[rolle] = f.ToLowerInvariant();
        }

        // Geschosse
        var geschosse = new List<TestraumGeschoss>();
        foreach (var (g, i) in (o["geschosse"] as JsonArray ?? throw F("geschosse", "Liste fehlt.")).Select((g, i) => (g, i)))
        {
            var ort = $"geschosse[{i}]";
            var id = Text(g?["id"], ort + ".id");
            if (!GeschossMuster.IsMatch(id)) throw F(ort + ".id", "nur Buchstaben, Ziffern, Punkt, Strich und Unterstrich (höchstens 40 Zeichen).");
            if (geschosse.Any(x => x.Id == id)) throw F(ort + ".id", $"«{id}» doppelt.");
            var hoehe = Zahl(g?["hoeheMm"], ort + ".hoeheMm");
            if (hoehe <= 0) throw F(ort + ".hoeheMm", "muss grösser als 0 sein.");
            geschosse.Add(new TestraumGeschoss(id, Text(g?["kennzeichen"], ort + ".kennzeichen"), Text(g?["name"], ort + ".name"), Zahl(g?["koteMm"], ort + ".koteMm"), hoehe));
        }
        if (geschosse.Count == 0) throw F("geschosse", "mindestens ein Geschoss.");
        geschosse = [.. geschosse.OrderBy(g => g.KoteMm)];
        for (int i = 1; i < geschosse.Count; i++)
            if (geschosse[i].KoteMm < geschosse[i - 1].KoteMm + geschosse[i - 1].HoeheMm - 1e-6)
                throw F("geschosse", $"{geschosse[i].Kennzeichen} beginnt, bevor {geschosse[i - 1].Kennzeichen} endet.");

        // Elemente
        var elemente = new List<TestraumElement>();
        foreach (var (e, i) in (o["elemente"] as JsonArray ?? throw F("elemente", "Liste fehlt.")).Select((e, i) => (e, i)))
        {
            var ort = $"elemente[{i}]";
            var id = Text(e?["id"], ort + ".id");
            ort = $"Element «{id}»";
            if (!IdMuster.IsMatch(id)) throw F(ort, "Id nur aus Buchstaben, Ziffern, Punkt, Strich und Unterstrich (höchstens 40 Zeichen).");
            if (elemente.Any(x => x.Id == id)) throw F(ort, "Id doppelt.");
            var art = Text(e?["art"], ort + ".art");
            if (!Arten.Contains(art)) throw F(ort + ".art", $"unbekannt (erlaubt: {string.Join(", ", Arten)}).");
            var gid = Text(e?["geschoss"], ort + ".geschoss");
            var g = geschosse.FirstOrDefault(x => x.Id == gid) ?? throw F(ort + ".geschoss", $"«{gid}» gibt es nicht.");
            var name = e?["name"] is JsonValue nv && nv.TryGetValue<string>(out var nm) ? nm : null;
            var kz = e?["kennzeichen"] is JsonValue kv && kv.TryGetValue<string>(out var k) ? k : null;
            double unten = 0, oben = 0, dicke = 0, radius = 0;
            double[]? von = null, bis = null, mitte = null;
            List<double[]>? umriss = null;
            if (art == ArtMarkierung)
            {
                mitte = Punkt(e?["mitteMm"], ort + ".mitteMm");
                radius = Zahl(e?["radiusMm"], ort + ".radiusMm");
                if (radius <= 0) throw F(ort + ".radiusMm", "muss grösser als 0 sein.");
                unten = oben = g.KoteMm;
            }
            else
            {
                unten = Zahl(e?["untenMm"], ort + ".untenMm");
                oben = Zahl(e?["obenMm"], ort + ".obenMm");
                if (oben - unten < 1) throw F(ort, "obenMm muss über untenMm liegen.");
                if (art == ArtWand)
                {
                    von = Punkt(e?["vonMm"], ort + ".vonMm");
                    bis = Punkt(e?["bisMm"], ort + ".bisMm");
                    dicke = Zahl(e?["dickeMm"], ort + ".dickeMm");
                    if (dicke <= 0) throw F(ort + ".dickeMm", "muss grösser als 0 sein.");
                    if (Math.Sqrt(Math.Pow(bis[0] - von[0], 2) + Math.Pow(bis[1] - von[1], 2)) < 1) throw F(ort, "Anfang und Ende der Achse fallen zusammen.");
                }
                else umriss = Umriss(e?["umrissMm"], ort + ".umrissMm");
            }
            elemente.Add(new TestraumElement(id, art, gid, name, kz, von, bis, dicke, umriss, unten, oben, mitte, radius));
        }

        TestraumElement El(string id, string ort, string art)
        {
            var el = elemente.FirstOrDefault(x => x.Id == id) ?? throw F(ort, $"Element «{id}» gibt es nicht.");
            if (el.Art != art) throw F(ort, $"«{id}» ist kein Element der Art «{art}».");
            return el;
        }

        // Markierungen liegen in einem Raum ihrer Etage (sonst springt man ins Leere)
        foreach (var m in elemente.Where(e => e.IstMarkierung))
            if (!elemente.Any(r => r.IstRaum && r.Geschoss == m.Geschoss && Innen(r.Umriss!, m.Mitte![0], m.Mitte[1])))
                throw F($"Element «{m.Id}»", "die Markierung liegt in keinem Raum ihrer Etage.");

        // Ziele
        var ziele = o["ziele"] as JsonObject ?? throw F("ziele", "fehlt.");
        var markierungen = new List<string>();
        if (ziele[TeleportMarkierung]?["markierungen"] is JsonArray ma)
            foreach (var (m, i) in ma.Select((m, i) => (m, i)))
            {
                var id = Text(m, $"ziele.{TeleportMarkierung}.markierungen[{i}]");
                El(id, $"ziele.{TeleportMarkierung}", ArtMarkierung);
                markierungen.Add(id);
            }
        var paare = new List<TestraumWandPaar>();
        if (ziele[MessenWandWand]?["wandpaare"] is JsonArray pa)
            foreach (var (p, i) in pa.Select((p, i) => (p, i)))
            {
                var ort = $"ziele.{MessenWandWand}.wandpaare[{i}]";
                var paar = new TestraumWandPaar(Text(p?["id"], ort + ".id"), Text(p?["raum"], ort + ".raum"), Text(p?["a"], ort + ".a"), Text(p?["b"], ort + ".b"), Zahl(p?["sollMm"], ort + ".sollMm"));
                var raum = El(paar.Raum, ort + ".raum", ArtRaum);
                var a = El(paar.A, ort + ".a", ArtWand);
                var b = El(paar.B, ort + ".b", ArtWand);
                if (a.Geschoss != raum.Geschoss || b.Geschoss != raum.Geschoss) throw F(ort, "Raum und Wände müssen in derselben Etage liegen.");
                var (fa, fb) = Flaechen(a, b, raum) ?? throw F(ort, "die Wände sind nicht parallel bzw. liegen sich nicht gegenüber.");
                var ist = Abstand(fa, fb);
                if (Math.Abs(ist - paar.SollMm) > SollToleranzMm)
                    throw F(ort + ".sollMm", string.Create(CultureInfo.InvariantCulture, $"{paar.SollMm:0.#} mm, die Geometrie ergibt {ist:0.#} mm."));
                paare.Add(paar);
            }
        string? raumAr = null;
        if (ziele[RaumArOeffnen] is JsonObject ro && ro["raum"] is not null)
        {
            raumAr = Text(ro["raum"], $"ziele.{RaumArOeffnen}.raum");
            El(raumAr, $"ziele.{RaumArOeffnen}.raum", ArtRaum);
        }
        var bauteile = new List<TestraumBauteil>();
        if (ziele[TabletWertAblesen]?["bauteile"] is JsonArray ba)
            foreach (var (b, i) in ba.Select((b, i) => (b, i)))
            {
                var ort = $"ziele.{TabletWertAblesen}.bauteile[{i}]";
                var id = Text(b?["bauteil"], ort + ".bauteil");
                var el = elemente.FirstOrDefault(x => x.Id == id) ?? throw F(ort, $"Element «{id}» gibt es nicht.");
                if (!(el.IstWand || el.IstPlatte)) throw F(ort, "nur Wände und Platten (die App wählt unter den Bauteilen des Durchgangs).");
                var hoehe = Zahl(b?["hoeheMm"], ort + ".hoeheMm");
                if (Math.Abs(hoehe - (el.ObenMm - el.UntenMm)) > SollToleranzMm)
                    throw F(ort + ".hoeheMm", string.Create(CultureInfo.InvariantCulture, $"{hoehe:0.#} mm, die Geometrie ergibt {el.ObenMm - el.UntenMm:0.#} mm."));
                bauteile.Add(new TestraumBauteil(id, hoehe));
            }

        return new TestraumKatalog
        {
            Projekt = projekt, Geschosse = geschosse, Elemente = elemente, Farben = farben, Markierungen = markierungen, WandPaare = paare,
            RaumArOeffnenRaum = raumAr, Bauteile = bauteile, AufgabenMitEintrag = [.. ziele.Select(z => z.Key)],
            Schluessel = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..24],
        };
    }

    /// <summary>
    /// Die beiden Innenflächen eines Wandpaars am Boden des Raums: Achsen parallel (±0.1°), die Flächen auf der Seite des Raums (Dicke halb von der Achse zur Mitte
    /// des Raums), Anfang und Ende auf der Überlappung entlang der Wände und im Raum. null, wenn sie nicht parallel sind oder sich nicht gegenüberliegen.
    /// </summary>
    public static (TestraumWandFlaeche A, TestraumWandFlaeche B)? Flaechen(TestraumElement a, TestraumElement b, TestraumElement raum)
    {
        var (dx, dy) = Richtung(a);
        var (ex, ey) = Richtung(b);
        if (Math.Abs(dx * ex + dy * ey) < Math.Cos(0.1 * Math.PI / 180)) return null;
        // Mitte des Raums (Mittel der Umrisspunkte genügt für die Seite)
        double mx = raum.Umriss!.Average(p => p[0]), my = raum.Umriss!.Average(p => p[1]);
        TestraumWandFlaeche? Flaeche(TestraumElement w)
        {
            var (wx, wy) = Richtung(w);
            double nx = -wy, ny = wx;   // links der Achse
            var seite = (mx - w.Von![0]) * nx + (my - w.Von[1]) * ny;
            if (Math.Abs(seite) < w.DickeMm / 2) return null;   // die Mitte des Raums liegt in der Wand
            if (seite < 0) { nx = -nx; ny = -ny; }
            var h = w.DickeMm / 2;
            return new TestraumWandFlaeche(w.Id, [w.Von[0] + nx * h, w.Von[1] + ny * h, raum.UntenMm], [w.Bis![0] + nx * h, w.Bis[1] + ny * h, raum.UntenMm], [nx, ny, 0]);
        }
        if (Flaeche(a) is not { } fa || Flaeche(b) is not { } fb) return null;
        if (fa.Normale[0] * fb.Normale[0] + fa.Normale[1] * fb.Normale[1] > -0.999) return null;   // nicht gegeneinander
        // Überlappung entlang von a, begrenzt auf den Raum (Hüllrechteck des Umrisses in der Richtung von a)
        double S(double x, double y) => (x - fa.Von[0]) * dx + (y - fa.Von[1]) * dy;
        var raumS = raum.Umriss!.Select(p => S(p[0], p[1])).ToList();
        double lo = new[] { Math.Min(S(fa.Von[0], fa.Von[1]), S(fa.Bis[0], fa.Bis[1])), Math.Min(S(fb.Von[0], fb.Von[1]), S(fb.Bis[0], fb.Bis[1])), raumS.Min() }.Max();
        double hi = new[] { Math.Max(S(fa.Von[0], fa.Von[1]), S(fa.Bis[0], fa.Bis[1])), Math.Max(S(fb.Von[0], fb.Von[1]), S(fb.Bis[0], fb.Bis[1])), raumS.Max() }.Min();
        if (hi - lo < 1) return null;
        double[] Auf(TestraumWandFlaeche f, double s)
        {
            var t = (f.Von[0] - fa.Von[0]) * f.Normale[0] + (f.Von[1] - fa.Von[1]) * f.Normale[1];   // Lage der Fläche quer (für fb: Abstand zu fa)
            var bx = fa.Von[0] + dx * s; var by = fa.Von[1] + dy * s;
            return [bx + f.Normale[0] * t, by + f.Normale[1] * t, f.Von[2]];
        }
        return (fa with { Von = Auf(fa, lo), Bis = Auf(fa, hi) }, fb with { Von = Auf(fb, lo), Bis = Auf(fb, hi) });
    }

    /// <summary>Abstand der beiden Flächen (mm, entlang der Normale von a).</summary>
    public static double Abstand(TestraumWandFlaeche a, TestraumWandFlaeche b) =>
        Math.Abs((b.Von[0] - a.Von[0]) * a.Normale[0] + (b.Von[1] - a.Von[1]) * a.Normale[1]);

    static (double X, double Y) Richtung(TestraumElement w)
    {
        double dx = w.Bis![0] - w.Von![0], dy = w.Bis[1] - w.Von[1], l = Math.Sqrt(dx * dx + dy * dy);
        return (dx / l, dy / l);
    }
}
