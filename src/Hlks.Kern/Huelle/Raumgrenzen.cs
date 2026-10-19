// Raumgrenzen der Gebäudehülle (Modul 1c, Auftrag B1; verbindlich: entwurf/modul-1c.md, Abschnitte «Datenmodell» und
// «Raumgrenzen»). Der Kern rechnet je Raum, woran seine Flächen grenzen – aus Raumpolygonen, Wänden, Öffnungen, Decken
// und Dächern, nicht aus IFC-Space-Boundaries. Keine Normfaktoren (keine b-Faktoren, keine Temperaturkorrekturen), keine
// Kennwerte: U wird nur gelesen.
//
// Regeln (Spezifikation; was darüber hinausgeht, ist als «Annahme» bezeichnet):
// - Seiten: jede Kante des Raumpolygons wird den Wänden zugeordnet, deren Band (Achse ± Dicke/2 samt Verlängerung an den
//   Ecken, Toleranz 20 mm) sie berührt. Annahme: nur etwa parallele Wände (|sin| < 0.05 wie Wandgeometrie) und nur Wände,
//   deren Höhenbereich den des Raums um mehr als die Toleranz überdeckt (Wände unter dem Boden oder ganz darüber zählen
//   nicht; Split-Level-Wände, die nur einen Teil überdecken, schon). Liegen mehrere Wände auf demselben Stück, gilt die mit
//   der grössten Höhenüberdeckung, dann die, deren Fläche der Kante am nächsten liegt (Annahme). Stücke ohne Wand sind
//   «offen» (Hinweis). Stücke unter 40 mm (2 × Toleranz) gehen im längeren Nachbarstück auf (Annahme, Rundungssplitter).
// - Nachbar: abgetastet wird eine Linie parallel zum Stück, 50 mm hinter der Gegenseite der Wand – liegt die Kante auf der
//   Wandfläche, ist das der Versatz «Dicke + 50 mm» der Spezifikation. Annahme (Erweiterung): die ganze Linie statt eines
//   Punkts, damit ein Stück mit zwei Räumen dahinter in zwei Teile zerfällt; wo die Linie eine Querwand kreuzt, zählt der
//   Teil zum Nachbarn daneben. Raum gefunden → beheizt/unbeheizt nach Raum.beheizt (ohne Angabe ja); kein Raum → aussen;
//   aussen unter Gebaeude.terrain (Vorgabe ±0) → erdreich, Fläche nach Höhe getrennt. Offene Stücke: Linie 20 + 50 mm vor
//   der Kante (Annahme). Nachbar seitlich = Raum, der den Raum über mindestens die halbe kleinere Höhe überdeckt; Räume,
//   die sich im Grundriss und in der Höhe überlappen, gelten nicht als Nachbarn (Hinweis).
// - Öffnungen: Fenster und Türen der Wand im Stück werden abgezogen und als eigene Raumgrenze geführt (Breite auf das
//   Stück, Höhe auf den Raum bzw. den Teil über/unter Terrain abgeschnitten).
// - Boden und Decke: Raumfläche gegen die Räume darunter bzw. darüber (Polygon-Schnitt). Annahme zur Höhe: «darunter» liegt
//   ein Raum, der tiefer beginnt, den Raum um weniger als die halbe kleinere Höhe überdeckt und über der Kote des Geschosses
//   darunter endet (Räume zwei Geschosse tiefer zählen nicht); «darüber» spiegelbildlich bis zur Oberkante des Geschosses
//   darüber. Wandband-Regel (Entscheid Hauptlauf 18.10.2026): Der Rest unten wird gegen die Wandbänder des Geschosses
//   darunter geprüft, der Rest oben gegen die des Geschosses darüber (Band = Achse ± Dicke/2 samt Verlängerung, Toleranz
//   20 mm wie bei den Seiten). Ein Reststück im Band wird an der Wandachse geteilt, jede Hälfte geht an den Raum auf ihrer
//   Seite (Nachbar und Angrenzung dieses Raums, Art bleibt boden bzw. decke) und wird mit dessen Teilfläche vereinigt. Den
//   Raum findet dieselbe Suchlinie wie bei den Seiten (Dicke/2 + 50 mm neben der Achse; Querwände zählen je zur Hälfte zum
//   Raum daneben, so geht bei Kreuzungen und Ecken jede Teilfläche an den nächsten Raum). Annahmen zur Suchlinie: sie reicht
//   an beiden Enden halbe grösste Wanddicke + 90 mm über das Stück hinaus (Querwand am Ende hat so beidseitig Nachbarn), und
//   Abschnitte unter 40 mm gehen schon vor der Teilung der Querwände im längeren Nachbarn auf (Rundungssplitter zwischen
//   Raumkante und Bandrand teilten sonst eine Querwand zur Hälfte gegen «kein Raum»). Annahmen: «Wand des Geschosses
//   darunter» nach demselben Höhenfenster wie die Räume darunter (beginnt tiefer, endet höchstens die halbe kleinere Höhe
//   über dem Boden und über der Kote des Geschosses darunter), darüber spiegelbildlich; Wände mit der grössten
//   Überdeckung des Rests zuerst (dann die dickere), eine spätere Wand sieht nur noch, was die früheren nicht genommen haben.
//   Liegt auf einer Seite kein Raum (Aussenwand, Auskragung) und ausserhalb jedes Bands (Luftraum, Auskragung, Raum ohne
//   Gegenstück) gilt die bisherige Regel: Rest unten: unterstes Geschoss oder Boden auf oder unter Terrain (Toleranz 20 mm;
//   Entscheid Hauptlauf 18.10.2026) → erdreich, sonst aussen (Auskragung); Rest oben: aussen (Dach).
//   Bauteil = Decke/Dach, deren Umriss die Fläche überdeckt und deren Höhe zwischen den halben Höhen der beiden Räume liegt
//   (Annahme; Räume aus Wänden reichen von Kote zu Kote, die Platte liegt dann im oberen Teil des Raums darunter; ohne Raum
//   eine Geschosshöhe weit). Mehrere Platten teilen die Fläche, die mit der grössten Höhenüberdeckung (dickste) zuerst,
//   bei Gleichstand die nächste an der Fuge zwischen den Räumen (Annahme).
//   Teilflächen unter 0.01 m² gehen in der grössten Teilfläche desselben Bodens bzw. derselben Decke auf (Annahme).
// - Masse: Innenmasse – Raumhöhe wie in der Raumliste (lichteHoehe › Geometriehöhe › Geschosshöhe) × Kantenlänge, Boden und
//   Decke = Raumfläche. Orientierung: Azimut der Aussennormalen ab geografisch Nord im Uhrzeigersinn (Projekt-Nord = +y,
//   Projektinformation.nordwinkel = Winkel von Projekt-Nord zu geografisch Nord im Uhrzeigersinn, also Azimut = Winkel ab
//   +y − nordwinkel); Neigung 90° Wand, 0° Decke/Dach (Normale nach oben), 180° Boden.
// - Geometrie je Raumgrenze (für die Darstellung «Hülle»): Seiten Von/Bis auf der Raumkante mit Unten/Oben; Boden, Decke
//   und Dach je zusammenhängender Teil eine Raumgrenze mit Ring (Löcher über Stege der Breite 0) auf der Höhe Unten.
//   JeGeschoss liefert sie je Geschoss und nutzt den Zwischenspeicher von Wandgeometrie (Vorbereitung einmal je Lauf).
// - U aus Aufbau.uWert (bezuege.aufbau) bzw. Fenster.uw bzw. Tuer.ud (sonst Aufbau der Tür), nur gelesen; ua = U·A.
// - Bauteil.angrenzung überschreibt als ausdrückliche Vorgabe die gerechnete Angrenzung (Hinweis, wenn sie abweicht);
//   Öffnungen ohne eigene Vorgabe übernehmen die ihrer Wand.

using System.Text.Json.Nodes;
using Clipper2Lib;

namespace Hlks.Kern;

/// <summary>Eine Raumgrenze: Fläche eines Raums gegen einen Nachbarn (Raum, aussen, Erdreich), Innenmass.</summary>
public sealed class Raumgrenze
{
    public required Element Raum { get; init; }
    /// <summary>Id des Nachbarraums oder «aussen» bzw. «erdreich».</summary>
    public required string Nachbar { get; init; }
    public Element? NachbarRaum { get; init; }
    /// <summary>aussen | erdreich | beheizt | unbeheizt – bei einer Vorgabe am Bauteil diese.</summary>
    public required string Angrenzung { get; init; }
    /// <summary>Gerechnete Angrenzung, wenn Bauteil.angrenzung sie überschreibt; sonst null.</summary>
    public string? Gerechnet { get; init; }
    /// <summary>wand | fenster | tuer | boden | decke | dach</summary>
    public required string Art { get; init; }
    public Element? Bauteil { get; init; }
    public Element? Aufbau { get; init; }
    /// <summary>Fläche in m² (Innenmass).</summary>
    public double Flaeche { get; set; }
    /// <summary>Azimut der Aussennormalen in Grad ab geografisch Nord im Uhrzeigersinn; null bei waagrechten Flächen.</summary>
    public double? Azimut { get; init; }
    /// <summary>Neigung in Grad: 90 Wand, 0 Decke/Dach, 180 Boden.</summary>
    public double Neigung { get; init; }
    /// <summary>U-Wert W/(m²·K), nur wenn am Bauteil bzw. Aufbau vorhanden.</summary>
    public double? U { get; init; }
    /// <summary>Fundstelle von U, z. B. «AUF-1.uWert».</summary>
    public string? UQuelle { get; init; }
    /// <summary>U·A in W/K, wenn U bekannt.</summary>
    public double? UA => U is double u ? u * Flaeche : null;
    /// <summary>Kantenstück ohne Wand («offen»).</summary>
    public bool Offen { get; init; }
    /// <summary>Wand und Öffnung: Strecke auf der Raumkante [x, y] in mm.</summary>
    public double[]? Von { get; init; }
    public double[]? Bis { get; init; }
    /// <summary>Höhenbereich absolut in mm (Boden/Decke: Unten = Oben = Höhe der Fläche).</summary>
    public double Unten { get; init; }
    public double Oben { get; init; }
    /// <summary>Aussennormale im Projektsystem [x, y, z] (vom Raum weg).</summary>
    public required double[] Normale { get; init; }
    /// <summary>Boden/Decke: Fläche in xy (Clipper-Ringe, mm) auf der Höhe Unten – ein zusammenhängender Teil, Aussenring und Löcher.</summary>
    public PathsD? Flaechen { get; init; }
    /// <summary>Boden/Decke/Dach: dieselbe Fläche als ein Ring [x, y] in mm (Gegenuhrzeigersinn, Löcher über Stege der Breite 0,
    /// wie RaeumeAusWaenden.MitInseln); Höhe = Unten (Raumunterkante bzw. -oberkante). Für die Darstellung «Hülle».</summary>
    public List<double[]>? Ring { get; init; }

    /// <summary>Länge der Strecke in mm (Wand, Öffnung), sonst 0.</summary>
    public double Laenge => Von is null || Bis is null ? 0 : Math.Sqrt((Bis[0] - Von[0]) * (Bis[0] - Von[0]) + (Bis[1] - Von[1]) * (Bis[1] - Von[1]));
}

/// <summary>Masse eines gerechneten Raums: Fläche (m²), Höhe und Unterkante (mm).</summary>
public sealed record RaumMasse(Element Raum, double FlaecheM2, double HoeheMm, double UntenMm);

public sealed class RaumgrenzenErgebnis
{
    public List<Raumgrenze> Grenzen { get; } = [];
    public List<RaumMasse> Raeume { get; } = [];
    public List<string> Hinweise { get; } = [];
    public double Nordwinkel { get; init; }
    /// <summary>Rechenzeit je Schritt in Sekunden (vorbereiten, umgebung, seiten, bodenDecke) – für die Leistungsprüfung.</summary>
    public Dictionary<string, double> Zeiten { get; } = [];
}

public static class Raumgrenzen
{
    /// <summary>Toleranz Raumkante ↔ Wandband in mm (Spezifikation).</summary>
    public const double Toleranz = 20;
    /// <summary>Versatz der Nachbarsuche hinter der Gegenseite der Wand in mm (Spezifikation «Dicke + 50 mm»).</summary>
    public const double Versatz = 50;
    /// <summary>Kürzere Kantenstücke gehen im Nachbarstück auf (Annahme: 2 × Toleranz), mm.</summary>
    public const double MinStueck = 2 * Toleranz;
    /// <summary>Kleinere waagrechte Teilflächen gehen in der grössten auf (Annahme: 0.01 m²), mm².</summary>
    public const double KleinsteFlaeche = 1e4;

    public static readonly string[] Angrenzungen = ["aussen", "erdreich", "unbeheizt", "beheizt"];
    public static readonly string[] Arten = ["wand", "fenster", "tuer", "boden", "decke", "dach"];

    /// <summary>Raumgrenzen der angegebenen Räume (null = alle); Nachbarn sind immer alle Räume des Modells.</summary>
    public static RaumgrenzenErgebnis Rechnen(Modell m, Katalog k, IEnumerable<Element>? raeume = null) =>
        new RaumgrenzenLauf(m, k).Rechnen(raeume);

    /// <summary>Raumgrenzen der Räume eines Geschosses samt Geometrie (Seiten: Von/Bis auf der Raumkante, Unten/Oben; Boden,
    /// Decke, Dach: Ring auf der Höhe Unten) – für die Darstellung «Hülle». Innerhalb von Wandgeometrie.Zwischenspeichern
    /// wird die Vorbereitung (Räume, Wände, Platten des ganzen Modells) einmal gerechnet und das Ergebnis je Geschoss behalten;
    /// ohne Zwischenspeicher jedes Mal neu. Nachbarn sind immer alle Räume des Modells.</summary>
    public static RaumgrenzenErgebnis JeGeschoss(Modell m, Katalog k, string? geschossId)
    {
        var lauf = Wandgeometrie.Zwischengespeichert(m, "raumgrenzen.lauf", () => new RaumgrenzenLauf(m, k));
        return Wandgeometrie.Zwischengespeichert(m, "raumgrenzen.geschoss:" + (geschossId ?? ""),
            () => lauf.Rechnen(m.DerKlasse("Raum").Where(r => r.Geschoss == geschossId).ToList()));
    }

    /// <summary>Azimut in Grad (0 … 360) der Normale (nx, ny) im Projektsystem: Winkel ab +y im Uhrzeigersinn minus nordwinkel.</summary>
    public static double Azimut(double nx, double ny, double nordwinkel)
    {
        double a = Math.Atan2(nx, ny) * 180 / Math.PI - nordwinkel;
        a %= 360;
        if (a < 0) a += 360;
        a = Math.Round(a, 9);
        return a >= 360 ? 0 : a;
    }

    public static double Nordwinkel(Modell m) => m.DerKlasse("Projektinformation").FirstOrDefault()?.Zahl("nordwinkel") ?? 0;

    /// <summary>Raum.beheizt, ohne Angabe ja (Spezifikation).</summary>
    public static bool Beheizt(Element raum) =>
        raum.Eigenschaft("beheizt")?.Inhalt is not JsonValue v || !v.TryGetValue<bool>(out var b) || b;

    /// <summary>Raumhöhe wie in der Raumliste: lichteHoehe › Geometriehöhe › Geschosshöhe (mm, 0 = unbekannt).</summary>
    public static double Raumhoehe(Modell m, Element raum) =>
        raum.Zahl("lichteHoehe") ?? raum.Geometrie?.Hoehe ?? m.Finde(raum.Geschoss)?.Zahl("hoehe") ?? 0;

    /// <summary>Aufbau des Bauteils (bezuege.aufbau, Klasse Aufbau) oder null.</summary>
    public static Element? AufbauVon(Modell m, Katalog k, Element bauteil) =>
        bauteil.Bezuege.TryGetValue("aufbau", out var id) && m.Finde(id) is { } a && k.IstArt(a, "Aufbau") ? a : null;

    /// <summary>U-Wert des Bauteils, nur gelesen: Fenster.uw; Tuer.ud, sonst Aufbau der Tür; übrige Aufbau.uWert.</summary>
    public static (double? U, string? Quelle) UWert(Modell m, Katalog k, Element bauteil)
    {
        if (k.IstArt(bauteil, "Fenster"))
            return bauteil.Zahl("uw") is double uw ? (uw, $"{bauteil.Kennzeichen}.uw") : (null, null);
        if (k.IstArt(bauteil, "Tuer") && bauteil.Zahl("ud") is double ud) return (ud, $"{bauteil.Kennzeichen}.ud");
        if (AufbauVon(m, k, bauteil) is { } a && a.Zahl("uWert") is double u) return (u, $"{a.Kennzeichen}.uWert");
        return (null, null);
    }

    /// <summary>Ausdrückliche Vorgabe Bauteil.angrenzung oder null.</summary>
    public static string? Vorgabe(Element? bauteil) =>
        bauteil?.Text("angrenzung") is { } a && Angrenzungen.Contains(a) ? a : null;
}

/// <summary>Hinweise nach Grund gesammelt, mit Anzahl und einigen Beispielen.</summary>
sealed class RaumgrenzenHinweise
{
    readonly Dictionary<string, (int Anzahl, List<string> Beispiele)> gruende = [];
    readonly List<string> reihenfolge = [];

    public void Dazu(string grund, string? beispiel = null)
    {
        if (!gruende.TryGetValue(grund, out var g)) { g = (0, []); reihenfolge.Add(grund); }
        if (beispiel is not null && g.Beispiele.Count < 5 && !g.Beispiele.Contains(beispiel)) g.Beispiele.Add(beispiel);
        gruende[grund] = (g.Anzahl + 1, g.Beispiele);
    }

    public IEnumerable<string> Texte() => reihenfolge.Select(grund =>
    {
        var (n, b) = gruende[grund];
        return $"{grund}: {n}" + (b.Count > 0 ? $" (z. B. {string.Join("; ", b)})" : "");
    });
}

/// <summary>Abschnitt [A, B] eines Parameterbereichs 0 … 1 mit einem Wert.</summary>
sealed record GrenzAbschnitt<T>(double A, double B, T Wert);

sealed class RaumgrenzenLauf
{
    const double Eps = 1e-6;
    /// <summary>Wände gelten als parallel zur Raumkante bei |sin| unter diesem Wert (Annahme wie Wandgeometrie, rund 3°).</summary>
    const double Parallel = 0.05;

    sealed class RaumInfo
    {
        public required Element E;
        /// <summary>Bereinigte Ringe (Clipper-Vereinigung): Aussenringe im Gegenuhrzeigersinn, Löcher im Uhrzeigersinn – der Raum
        /// liegt immer links der Laufrichtung. Meist ein Ring; ein Polygon, das sich selbst berührt, zerfällt in mehrere.</summary>
        public required List<List<double[]>> Ringe;
        public required PathsD Pfad;
        /// <summary>Punkt im Raum (gerade/ungerade über alle Ringe).</summary>
        public bool Enthaelt(double x, double y)
        {
            bool drin = false;
            foreach (var r in Ringe) if (Polygone.ImRing(r, x, y)) drin = !drin;
            return drin;
        }
        public double MinX, MinY, MaxX, MaxY;
        public double Unten, Hoehe;
        public double Oben => Unten + Hoehe;
        public double FlaecheMm2;
        public bool Beheizt;
        public double Terrain;
        public bool Unterstes;
        public double UntereGrenze, ObereGrenze;   // Höhenfenster für Räume darunter/darüber
        public double HoeheUnter, HoeheUeber;      // Suchbereich der Platten ohne Raum darunter/darüber
        // je Raum vorbereitet (nur für gerechnete Räume)
        public List<WandInfo>? Waende;
        public List<RaumInfo>? Seitlich;
        public List<RaumInfo>? Darunter;
        public List<RaumInfo>? Darueber;
        // erst bei einem Rest von Boden bzw. Decke: Wände und Räume des Geschosses darunter bzw. darüber in der Nähe (Wandband-Regel)
        public List<WandInfo>? WaendeUnten, WaendeOben;
        public List<RaumInfo>? RaeumeUnten, RaeumeOben;
    }

    sealed class WandInfo
    {
        public required Wandlage L;
        public required List<Oeffnungslage> Oeffnungen;
        public double MinX, MinY, MaxX, MaxY;
        public double UK, OK;
        public required double[][] Ecken;
    }

    sealed class PlatteInfo
    {
        public required Element E;
        public required PathsD Pfad;
        public double MinX, MinY, MaxX, MaxY;
        public double OK, UK;
        public bool Dach;
    }

    readonly record struct Belegung(RaumInfo? Raum, bool Wand);

    readonly Modell m;
    readonly Katalog k;
    readonly double nordwinkel;
    // vorbereitet einmal je Lauf (Zwischenspeicher der Grafik: je Geschoss nur noch die Räume rechnen)
    bool vorbereitet;
    readonly List<(string Grund, string? Beispiel)> vorbereitungsHinweise = [];
    readonly List<RaumInfo> raeume = [];
    readonly List<WandInfo> waende = [];
    readonly List<PlatteInfo> platten = [];
    readonly Dictionary<string, (double? U, string? Quelle)> uWerte = new(StringComparer.Ordinal);
    readonly Dictionary<(string, string), double> ueberlappen = [];
    double dickeMax;
    // je Aufruf von Rechnen neu
    RaumgrenzenHinweise hinweise = new();
    readonly HashSet<string> vorgabeGemeldet = new(StringComparer.Ordinal);
    readonly HashSet<(string, string)> ueberlappungGemeldet = [];
    int splitter;
    int bandStuecke;      // Reststücke von Boden/Decke in Wandbändern, einem Raum zugeschlagen
    double bandFlaeche;   // deren Fläche in mm²

    public RaumgrenzenLauf(Modell m, Katalog k)
    {
        this.m = m; this.k = k;
        nordwinkel = Raumgrenzen.Nordwinkel(m);
    }

    // ------------------------------------------------------------------ Vorbereiten

    void Vorbereiten()
    {
        // Geschosse je Gebäude nach Kote: Geschoss darunter und darüber
        var unter = new Dictionary<string, Element?>(StringComparer.Ordinal);
        var ueber = new Dictionary<string, Element?>(StringComparer.Ordinal);
        foreach (var gruppe in m.DerKlasse("Geschoss").Where(g => g.Zahl("kote") is not null).GroupBy(g => g.TeilVon ?? ""))
        {
            var liste = gruppe.OrderBy(g => g.Zahl("kote")).ThenBy(g => g.Kennzeichen, StringComparer.OrdinalIgnoreCase).ToList();
            for (int i = 0; i < liste.Count; i++)
            {
                unter[liste[i].Id] = i > 0 ? liste[i - 1] : null;
                ueber[liste[i].Id] = i + 1 < liste.Count ? liste[i + 1] : null;
            }
        }
        var gebaeude = m.DerKlasse("Gebaeude").ToList();

        foreach (var r in m.DerKlasse("Raum").OrderBy(r => r.Kennzeichen, StringComparer.OrdinalIgnoreCase))
        {
            var g = r.Geometrie;
            if (g is null || g.Art != "polygon" || g.Punkte.Count < 3) { vorbereitungsHinweise.Add(("Raum ohne Polygon – keine Raumgrenzen", r.Kennzeichen)); continue; }
            double h = Raumgrenzen.Raumhoehe(m, r);
            if (h <= 0) { vorbereitungsHinweise.Add(("Raum ohne Höhe (lichteHoehe, Geometriehöhe, Geschosshöhe) – keine Raumgrenzen", r.Kennzeichen)); continue; }
            var ver = Polygone.Vereinigen([Polygone.Ccw(Polygone.Pfad(g.Punkte))]);
            if (ver.Count == 0) { vorbereitungsHinweise.Add(("Raumpolygon ohne Fläche – keine Raumgrenzen", r.Kennzeichen)); continue; }
            if (ver.Count > 1) vorbereitungsHinweise.Add(("Raumpolygon mit Loch (Insel über einen Steg), Selbstberührung oder -überschneidung – in Teilflächen gerechnet", r.Kennzeichen));
            var ringe = ver.Select(x => Polygone.Ring(x, Clipper.Area(x) > 0)).ToList();
            var alle = ringe.SelectMany(x => x).ToList();
            var geschoss = m.Finde(r.Geschoss);
            var geb = geschoss?.TeilVon is { } gid && m.Finde(gid) is { } gb && k.IstArt(gb, "Gebaeude") ? gb : gebaeude.Count == 1 ? gebaeude[0] : null;
            double unten = g.Punkte.Min(p => p.Length > 2 ? p[2] : 0);
            var gu = geschoss is not null ? unter.GetValueOrDefault(geschoss.Id) : null;
            var go = geschoss is not null ? ueber.GetValueOrDefault(geschoss.Id) : null;
            double? kote = geschoss?.Zahl("kote"), gh = geschoss?.Zahl("hoehe");
            var info = new RaumInfo
            {
                E = r, Ringe = ringe, Pfad = new PathsD(ringe.Select(Polygone.Pfad)),
                MinX = alle.Min(p => p[0]), MinY = alle.Min(p => p[1]), MaxX = alle.Max(p => p[0]), MaxY = alle.Max(p => p[1]),
                Unten = unten, Hoehe = h, FlaecheMm2 = Math.Abs(ringe.Sum(Polygone.Flaecheninhalt)),
                Beheizt = Raumgrenzen.Beheizt(r),
                Terrain = geb?.Zahl("terrain") ?? 0,
                Unterstes = geschoss is null || gu is null,
            };
            info.UntereGrenze = gu?.Zahl("kote") ?? kote ?? unten - h;
            info.ObereGrenze = go is not null ? (go.Zahl("kote") ?? 0) + (go.Zahl("hoehe") ?? h) : (kote ?? unten) + (gh ?? h);
            info.HoeheUnter = gu?.Zahl("hoehe") ?? gh ?? h;
            info.HoeheUeber = go?.Zahl("hoehe") ?? gh ?? h;
            raeume.Add(info);
        }

        var jeWand = Wandgeometrie.OeffnungenJeWand(m, k);
        foreach (var gid in m.Elemente.Where(e => k.IstArt(e, "Wand")).Select(e => e.Geschoss).Distinct().ToList())
            foreach (var l in Wandgeometrie.DesGeschosses(m, k, gid))
            {
                var band = l.Band();
                var ecken = band.Select(p => new[] { p.x, p.y }).ToArray();
                var oeff = Wandgeometrie.Oeffnungen(k, l, jeWand.GetValueOrDefault(l.Id) ?? []);
                foreach (var o in (jeWand.GetValueOrDefault(l.Id) ?? []).Where(o => oeff.All(x => x.Element.Id != o.Id)))
                    vorbereitungsHinweise.Add(("Öffnung ohne Breite oder Lage – nicht abgezogen", o.Kennzeichen));
                waende.Add(new WandInfo
                {
                    L = l, Oeffnungen = oeff, Ecken = ecken,
                    MinX = ecken.Min(p => p[0]), MinY = ecken.Min(p => p[1]), MaxX = ecken.Max(p => p[0]), MaxY = ecken.Max(p => p[1]),
                    UK = l.Unterkante, OK = l.Hoehe is double h ? l.Unterkante + h : double.PositiveInfinity,
                });
                dickeMax = Math.Max(dickeMax, l.Dicke);
            }

        foreach (var d in m.Elemente.Where(e => k.IstArt(e, "Decke") || k.IstArt(e, "Dach")).OrderBy(e => e.Kennzeichen, StringComparer.OrdinalIgnoreCase))
        {
            var g = d.Geometrie;
            if (g is null || g.Art != "polygon" || g.Punkte.Count < 3) continue;
            // ohne Vereinigung (spart Zeit bei vielen Platten): die Schnitte rechnen mit NonZero, Selbstüberschneidungen schaden nicht
            var ring = Polygone.Ccw(Polygone.Pfad(g.Punkte));
            if (Math.Abs(Clipper.Area(ring)) < Polygone.Splitter) continue;
            PathsD pfad = [ring];
            double ok = g.Punkte.Max(p => p.Length > 2 ? p[2] : 0);
            platten.Add(new PlatteInfo
            {
                E = d, Pfad = pfad, OK = ok, UK = ok - (g.Dicke is double t && t > 0 ? t : 0), Dach = k.IstArt(d, "Dach"),
                MinX = g.Punkte.Min(p => p[0]), MinY = g.Punkte.Min(p => p[1]), MaxX = g.Punkte.Max(p => p[0]), MaxY = g.Punkte.Max(p => p[1]),
            });
        }
    }

    /// <summary>Höhenüberdeckung zweier Bereiche in mm (negativ = Abstand).</summary>
    static double Ueberdeckung(double a0, double a1, double b0, double b1) => Math.Min(a1, b1) - Math.Max(a0, b0);

    /// <summary>«Gleichauf»: Überdeckung über der Toleranz und mindestens die halbe kleinere Höhe (Annahme, seitliche Nachbarn).</summary>
    static bool Gleichauf(double a0, double a1, double b0, double b1)
    {
        double u = Ueberdeckung(a0, a1, b0, b1);
        return u > Raumgrenzen.Toleranz && u >= Math.Min(a1 - a0, b1 - b0) / 2;
    }

    static bool Boxen(double ax0, double ay0, double ax1, double ay1, double bx0, double by0, double bx1, double by1, double rand = 0) =>
        ax0 - rand <= bx1 && bx0 <= ax1 + rand && ay0 - rand <= by1 && by0 <= ay1 + rand;

    /// <summary>Überlappungsfläche zweier Räume in mm², wenn sie über der Schwelle liegt (0.01 m² bzw. 1 % des kleineren), sonst 0.</summary>
    double Ueberlappung(RaumInfo a, RaumInfo b)
    {
        var schluessel = string.CompareOrdinal(a.E.Id, b.E.Id) < 0 ? (a.E.Id, b.E.Id) : (b.E.Id, a.E.Id);
        if (ueberlappen.TryGetValue(schluessel, out var x)) return x;
        double f = 0;
        if (Boxen(a.MinX, a.MinY, a.MaxX, a.MaxY, b.MinX, b.MinY, b.MaxX, b.MaxY))
        {
            f = Inhalt(Schneiden(a.Pfad, b.Pfad));
            if (f <= Math.Max(Raumgrenzen.KleinsteFlaeche, 0.01 * Math.Min(a.FlaecheMm2, b.FlaecheMm2))) f = 0;
        }
        return ueberlappen[schluessel] = f;
    }

    /// <summary>Nahe Wände, seitliche Nachbarn, Räume darunter und darüber eines gerechneten Raums.</summary>
    void Umgebung(RaumInfo r)
    {
        double rand = dickeMax + Raumgrenzen.Versatz + Raumgrenzen.Toleranz + 1;
        r.Waende = waende.Where(w => Boxen(r.MinX, r.MinY, r.MaxX, r.MaxY, w.MinX, w.MinY, w.MaxX, w.MaxY, rand)
                                     && Ueberdeckung(r.Unten, r.Oben, w.UK, w.OK) > Raumgrenzen.Toleranz).ToList();
        r.Seitlich = [];
        foreach (var s in raeume)
        {
            if (!Boxen(r.MinX, r.MinY, r.MaxX, r.MaxY, s.MinX, s.MinY, s.MaxX, s.MaxY, rand) || !Gleichauf(r.Unten, r.Oben, s.Unten, s.Oben)) continue;
            if (s != r && Ueberlappung(r, s) is var f && f > 0)
            {
                var (a, b) = string.CompareOrdinal(r.E.Id, s.E.Id) < 0 ? (r, s) : (s, r);
                if (ueberlappungGemeldet.Add((a.E.Id, b.E.Id)))
                    hinweise.Dazu("Räume überlappen im Grundriss und in der Höhe – gelten nicht als Nachbarn", $"{a.E.Kennzeichen} / {b.E.Kennzeichen} {M2(f)} m²");
                continue;
            }
            r.Seitlich.Add(s);
        }
        var ueberdeckt = raeume.Where(s => s != r && Boxen(r.MinX, r.MinY, r.MaxX, r.MaxY, s.MinX, s.MinY, s.MaxX, s.MaxY)).ToList();
        r.Darunter = ueberdeckt.Where(s => Unter(r, s.Unten, s.Oben, s.Hoehe))
            .OrderByDescending(s => s.Oben).ThenBy(s => s.E.Kennzeichen, StringComparer.OrdinalIgnoreCase).ToList();
        r.Darueber = ueberdeckt.Where(s => Ueber(r, s.Unten, s.Oben, s.Hoehe))
            .OrderBy(s => s.Unten).ThenBy(s => s.E.Kennzeichen, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Höhenbereich u … o (Höhe h) liegt «darunter» (Raum oder Wand des Geschosses darunter): beginnt tiefer, endet
    /// höchstens die halbe kleinere Höhe über dem Boden von r und über der Kote des Geschosses darunter (Annahme).</summary>
    static bool Unter(RaumInfo r, double u, double o, double h) =>
        u < r.Unten - Eps && o <= r.Unten + Math.Min(r.Hoehe, h) / 2 && o > r.UntereGrenze;

    /// <summary>Spiegelbildlich zu Unter: beginnt höchstens die halbe kleinere Höhe unter der Oberkante von r, endet höher und
    /// beginnt unter der Oberkante des Geschosses darüber.</summary>
    static bool Ueber(RaumInfo r, double u, double o, double h) =>
        o > r.Oben + Eps && u >= r.Oben - Math.Min(r.Hoehe, h) / 2 && u < r.ObereGrenze;

    // ------------------------------------------------------------------ Rechnen

    public RaumgrenzenErgebnis Rechnen(IEnumerable<Element>? nur)
    {
        var uhr = System.Diagnostics.Stopwatch.StartNew();
        if (!vorbereitet) { Vorbereiten(); vorbereitet = true; }
        hinweise = new();
        vorgabeGemeldet.Clear();
        ueberlappungGemeldet.Clear();
        splitter = 0;
        bandStuecke = 0;
        bandFlaeche = 0;
        foreach (var (grund, beispiel) in vorbereitungsHinweise) hinweise.Dazu(grund, beispiel);
        var erg = new RaumgrenzenErgebnis { Nordwinkel = nordwinkel };
        erg.Zeiten["vorbereiten"] = uhr.Elapsed.TotalSeconds;
        HashSet<string>? ids = nur?.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        var gerechnet = raeume.Where(r => ids is null || ids.Contains(r.E.Id))
            .OrderBy(r => m.Finde(r.E.Geschoss)?.Zahl("kote") ?? double.MinValue)
            .ThenBy(r => r.E.Text("nummer") ?? r.E.Kennzeichen, StringComparer.OrdinalIgnoreCase).ToList();
        double tUmgebung = 0, tSeiten = 0, tWaagrecht = 0;
        foreach (var r in gerechnet)
        {
            double t0 = uhr.Elapsed.TotalSeconds;
            Umgebung(r);
            double t1 = uhr.Elapsed.TotalSeconds;
            var liste = new List<Raumgrenze>();
            Seiten(r, liste);
            double t2 = uhr.Elapsed.TotalSeconds;
            Horizontal(r, false, liste);
            Horizontal(r, true, liste);
            tUmgebung += t1 - t0; tSeiten += t2 - t1; tWaagrecht += uhr.Elapsed.TotalSeconds - t2;
            erg.Grenzen.AddRange(liste);
            erg.Raeume.Add(new RaumMasse(r.E, r.FlaecheMm2 / 1e6, r.Hoehe, r.Unten));
        }
        erg.Zeiten["umgebung"] = tUmgebung;
        erg.Zeiten["seiten"] = tSeiten;
        erg.Zeiten["bodenDecke"] = tWaagrecht;
        foreach (var gr in erg.Grenzen.Where(g => g.Offen).DistinctBy(g => (g.Raum.Id, g.Von![0], g.Von[1], g.Bis![0], g.Bis[1])))
            hinweise.Dazu("Kantenstück ohne Wand («offen», Bauteil fehlt)", $"{gr.Raum.Kennzeichen} {Grafik.Format(gr.Laenge / 1000, 2)} m");
        var ohneU = erg.Grenzen.Where(g => g.U is null && g.Bauteil is not null).ToList();
        if (ohneU.Count > 0)
            erg.Hinweise.Add($"Ohne U-Wert (Aufbau ohne uWert, kein Aufbau, Fenster ohne uw): {ohneU.Count} Raumgrenzen, {Grafik.Format(ohneU.Sum(g => g.Flaeche), 2)} m² – Kennwerte nur mit Quelle setzen.");
        erg.Hinweise.AddRange(hinweise.Texte());
        if (bandStuecke > 0)
            erg.Hinweise.Add($"Boden bzw. Decke über bzw. unter Wänden des Nachbargeschosses an der Wandachse geteilt und dem Raum daneben zugeschlagen: {bandStuecke} {(bandStuecke == 1 ? "Stück" : "Stücke")}, {M2(bandFlaeche)} m².");
        if (splitter > 0)
            erg.Hinweise.Add($"{splitter} Teilflächen unter {Grafik.Format(Raumgrenzen.KleinsteFlaeche / 1e6, 2)} m² der grössten Teilfläche desselben Bodens bzw. derselben Decke zugeschlagen.");
        erg.Hinweise.Add("Flächen in Innenmassen; U·A ohne Normfaktoren (keine b-Faktoren, keine Temperaturkorrekturen).");
        return erg;
    }

    // ------------------------------------------------------------------ Seiten

    void Seiten(RaumInfo r, List<Raumgrenze> aus)
    {
        foreach (var ring in r.Ringe)
            for (int i = 0; i < ring.Count; i++)
                Kante(r, ring[i], ring[(i + 1) % ring.Count], aus);
    }

    /// <summary>Eine Kante p → q des Raums (Raum links, aussen rechts): Wände zuordnen, je Stück die Flächen.</summary>
    void Kante(RaumInfo r, double[] p, double[] q, List<Raumgrenze> aus)
    {
        double ex = q[0] - p[0], ey = q[1] - p[1], len = Math.Sqrt(ex * ex + ey * ey);
        if (len < 0.5) return;
        ex /= len; ey /= len;
        double nx = ey, ny = -ex;   // Raum links der Laufrichtung: rechts = aussen

        var kandidaten = new List<(double T0, double T1, WandInfo W, double Hoehe, double Abstand)>();
        foreach (var w in r.Waende!)
        {
            var l = w.L;
            if (Math.Abs(ex * l.Uy - ey * l.Ux) >= Parallel) continue;
            double sP = l.Projektion(p[0], p[1]), sQ = l.Projektion(q[0], q[1]);
            double qP = l.Quer(p[0], p[1]), qQ = l.Quer(q[0], q[1]);
            var (a0, a1) = Bereich(sP, sQ, l.Von - Raumgrenzen.Toleranz, l.Bis + Raumgrenzen.Toleranz);
            double halb = l.Dicke / 2 + Raumgrenzen.Toleranz;
            var (b0, b1) = Bereich(qP, qQ, -halb, halb);
            double t0 = Math.Max(0, Math.Max(a0, b0)), t1 = Math.Min(1, Math.Min(a1, b1));
            if ((t1 - t0) * len < 0.5) continue;
            double qm = qP + (t0 + t1) / 2 * (qQ - qP);
            kandidaten.Add((t0, t1, w, Ueberdeckung(r.Unten, r.Oben, w.UK, w.OK), Math.Abs(Math.Abs(qm) - l.Dicke / 2)));
        }
        var marken = Marken(kandidaten.SelectMany(c => new[] { c.T0, c.T1 }), 0.01 / len);
        var stuecke = new List<GrenzAbschnitt<WandInfo?>>();
        for (int j = 0; j + 1 < marken.Count; j++)
        {
            double a = marken[j], b = marken[j + 1], mitte = (a + b) / 2;
            var best = kandidaten.Where(c => c.T0 <= mitte && mitte <= c.T1)
                .OrderByDescending(c => c.Hoehe).ThenBy(c => c.Abstand).ThenByDescending(c => c.W.L.Dicke)
                .ThenBy(c => c.W.L.Wand.Kennzeichen, StringComparer.OrdinalIgnoreCase).ThenBy(c => c.W.L.Id, StringComparer.Ordinal)
                .Select(c => c.W).FirstOrDefault();
            stuecke.Add(new(a, b, best));
        }
        foreach (var st in Glaetten(stuecke, len, (x, y) => ReferenceEquals(x, y)))
            Stueck(r, p, q, len, nx, ny, st.A, st.B, st.Wert, aus);
    }

    /// <summary>Bereich von t (Parameter 0 … 1), in dem f0 + t·(f1 − f0) zwischen lo und hi liegt; leer = (1, 0).</summary>
    static (double A, double B) Bereich(double f0, double f1, double lo, double hi)
    {
        double df = f1 - f0;
        if (Math.Abs(df) < 1e-12) return f0 >= lo && f0 <= hi ? (double.NegativeInfinity, double.PositiveInfinity) : (1, 0);
        double a = (lo - f0) / df, b = (hi - f0) / df;
        return a < b ? (a, b) : (b, a);
    }

    /// <summary>0, 1 und die Marken dazwischen, sortiert, ohne Doppelte (Abstand unter eps).</summary>
    static List<double> Marken(IEnumerable<double> roh, double eps)
    {
        var l = roh.Where(t => t > 0 && t < 1).Append(0).Append(1).Order().ToList();
        var aus = new List<double>();
        foreach (var t in l) if (aus.Count == 0 || t - aus[^1] > eps) aus.Add(t);
        if (aus[^1] < 1) aus[^1] = 1;
        return aus;
    }

    static List<GrenzAbschnitt<T>> Zusammenfassen<T>(List<GrenzAbschnitt<T>> l, Func<T, T, bool> gleich)
    {
        var aus = new List<GrenzAbschnitt<T>>();
        foreach (var x in l)
            if (aus.Count > 0 && gleich(aus[^1].Wert, x.Wert)) aus[^1] = aus[^1] with { B = x.B };
            else aus.Add(x);
        return aus;
    }

    /// <summary>Gleiche Nachbarn zusammenfassen; Abschnitte unter MinStueck gehen im längeren Nachbarabschnitt auf.</summary>
    static List<GrenzAbschnitt<T>> Glaetten<T>(List<GrenzAbschnitt<T>> l, double laenge, Func<T, T, bool> gleich)
    {
        var aus = Zusammenfassen(l, gleich);
        while (aus.Count > 1)
        {
            int i = -1; double kuerzeste = double.MaxValue;
            for (int j = 0; j < aus.Count; j++)
            {
                double d = (aus[j].B - aus[j].A) * laenge;
                if (d < Raumgrenzen.MinStueck && d < kuerzeste) { kuerzeste = d; i = j; }
            }
            if (i < 0) break;
            int nb = i == 0 ? 1 : i == aus.Count - 1 ? i - 1
                : aus[i - 1].B - aus[i - 1].A >= aus[i + 1].B - aus[i + 1].A ? i - 1 : i + 1;
            aus[i] = aus[i] with { Wert = aus[nb].Wert };
            aus = Zusammenfassen(aus, gleich);
        }
        return aus;
    }

    /// <summary>Ein Kantenstück t = ta … tb mit Wand (oder offen): Nachbarn entlang der Suchlinie, je Teil die Flächen.</summary>
    void Stueck(RaumInfo r, double[] p, double[] q, double len, double nx, double ny, double ta, double tb, WandInfo? w, List<Raumgrenze> aus)
    {
        double[] Pt(double t) => [p[0] + (q[0] - p[0]) * t, p[1] + (q[1] - p[1]) * t];
        var pa = Pt(ta); var pb = Pt(tb);
        double[] la, lb;
        if (w is not null)
        {
            var l = w.L;
            double sgn = nx * l.Nx + ny * l.Ny >= 0 ? 1 : -1;
            double ql = sgn * (l.Dicke / 2 + Raumgrenzen.Versatz);
            la = l.Punkt(l.Projektion(pa[0], pa[1]), ql);
            lb = l.Punkt(l.Projektion(pb[0], pb[1]), ql);
        }
        else
        {
            double off = Raumgrenzen.Toleranz + Raumgrenzen.Versatz;
            la = [pa[0] + nx * off, pa[1] + ny * off];
            lb = [pb[0] + nx * off, pb[1] + ny * off];
        }
        double laengeStueck = (tb - ta) * len;
        foreach (var t in NachbarnEntlang(r, la, lb, w))
        {
            var a = Pt(ta + (tb - ta) * t.A); var b = Pt(ta + (tb - ta) * t.B);
            double l = (t.B - t.A) * laengeStueck;
            if (t.Wert is { } s) Seitenflaeche(r, a, b, l, nx, ny, w, s, null, r.Unten, r.Oben, aus);
            else
            {
                if (r.Terrain > r.Unten + Eps) Seitenflaeche(r, a, b, l, nx, ny, w, null, "erdreich", r.Unten, Math.Min(r.Terrain, r.Oben), aus);
                if (r.Oben > r.Terrain + Eps) Seitenflaeche(r, a, b, l, nx, ny, w, null, "aussen", Math.Max(r.Terrain, r.Unten), r.Oben, aus);
            }
        }
    }

    /// <summary>Abschnitte der Suchlinie a → b nach Nachbarraum (null = kein Raum). Querwände zählen zum Nachbarn daneben.</summary>
    static List<GrenzAbschnitt<RaumInfo?>> NachbarnEntlang(RaumInfo r, double[] a, double[] b, WandInfo? eigen) =>
        NachbarnEntlang(r.Seitlich!, r.Waende!, r.Unten, r.Oben, a, b, eigen);

    /// <summary>Wie oben mit gegebenen Kandidaten: Räume, Querwände; liegt ein Punkt in mehreren Räumen, gilt der mit der
    /// grössten Höhenüberdeckung mit unten … oben (bei Räumen darunter bzw. darüber: der nächste). splitterVorher: Abschnitte
    /// unter MinStueck schon vor der Teilung der Querwände dem Nachbarn zuschlagen – sonst teilt eine Querwand neben einem
    /// Rundungssplitter (Raumkante und Bandrand um Bruchteile eines mm versetzt) zur Hälfte gegen «kein Raum» (Wandband-Regel).</summary>
    static List<GrenzAbschnitt<RaumInfo?>> NachbarnEntlang(List<RaumInfo> raeumeKand, List<WandInfo> waendeKand, double unten, double oben,
        double[] a, double[] b, WandInfo? eigen, bool splitterVorher = false)
    {
        double dx = b[0] - a[0], dy = b[1] - a[1], len = Math.Sqrt(dx * dx + dy * dy);
        double x0 = Math.Min(a[0], b[0]), y0 = Math.Min(a[1], b[1]), x1 = Math.Max(a[0], b[0]), y1 = Math.Max(a[1], b[1]);
        var kandRaeume = raeumeKand.Where(s => Boxen(x0, y0, x1, y1, s.MinX, s.MinY, s.MaxX, s.MaxY, 1)).ToList();
        var kandWaende = waendeKand.Where(w => w != eigen && w.L.Dicke > 0 && Boxen(x0, y0, x1, y1, w.MinX, w.MinY, w.MaxX, w.MaxY, 1)).ToList();
        var roh = new List<double>();
        if (len > Eps)
        {
            foreach (var s in kandRaeume)
                foreach (var ring in s.Ringe)
                    for (int i = 0; i < ring.Count; i++)
                        if (Schnitt(a, b, ring[i], ring[(i + 1) % ring.Count]) is double u) roh.Add(u);
            foreach (var w in kandWaende)
                for (int i = 0; i < 4; i++)
                    if (Schnitt(a, b, w.Ecken[i], w.Ecken[(i + 1) % 4]) is double u) roh.Add(u);
        }
        var marken = Marken(roh, len > Eps ? 0.01 / len : 0.5);
        var teile = new List<GrenzAbschnitt<Belegung>>();
        for (int j = 0; j + 1 < marken.Count; j++)
        {
            double mitte = (marken[j] + marken[j + 1]) / 2, x = a[0] + dx * mitte, y = a[1] + dy * mitte;
            RaumInfo? best = null; double bestU = double.MinValue;
            foreach (var s in kandRaeume)
            {
                if (!s.Enthaelt(x, y)) continue;
                double u = Ueberdeckung(unten, oben, s.Unten, s.Oben);
                if (u > bestU + Eps || (Math.Abs(u - bestU) <= Eps && best is not null && string.Compare(s.E.Kennzeichen, best.E.Kennzeichen, StringComparison.OrdinalIgnoreCase) < 0))
                { best = s; bestU = u; }
            }
            var bel = best is not null ? new Belegung(best, false) : new Belegung(null, kandWaende.Any(w => ImBand(w, x, y)));
            teile.Add(new(marken[j], marken[j + 1], bel));
        }
        Func<Belegung, Belegung, bool> gleich = (p, q) => p.Wand == q.Wand && ReferenceEquals(p.Raum, q.Raum);
        var z = splitterVorher ? Glaetten(teile, Math.Max(len, Eps), gleich) : Zusammenfassen(teile, gleich);
        var aus = new List<GrenzAbschnitt<RaumInfo?>>();
        for (int i = 0; i < z.Count; i++)
        {
            var x = z[i];
            if (!x.Wert.Wand) { aus.Add(new(x.A, x.B, x.Wert.Raum)); continue; }
            var links = i > 0 ? z[i - 1] : null; var rechts = i + 1 < z.Count ? z[i + 1] : null;
            if (links is not null && rechts is not null)
            {
                double mitte = (x.A + x.B) / 2;
                aus.Add(new(x.A, mitte, links.Wert.Raum));
                aus.Add(new(mitte, x.B, rechts.Wert.Raum));
            }
            else aus.Add(new(x.A, x.B, (links ?? rechts)?.Wert.Raum));
        }
        return Glaetten(aus, Math.Max(len, Eps), (p, q) => ReferenceEquals(p, q));
    }

    static bool ImBand(WandInfo w, double x, double y)
    {
        var l = w.L;
        double s = l.Projektion(x, y), q = l.Quer(x, y);
        return s >= l.Von && s <= l.Bis && Math.Abs(q) <= l.Dicke / 2;
    }

    /// <summary>Parameter u auf a → b, wo die Strecke c → d sie schneidet; null bei parallel oder ohne Schnitt.</summary>
    static double? Schnitt(double[] a, double[] b, double[] c, double[] d)
    {
        double rx = b[0] - a[0], ry = b[1] - a[1], sx = d[0] - c[0], sy = d[1] - c[1];
        double den = rx * sy - ry * sx;
        if (Math.Abs(den) < 1e-12) return null;
        double px = c[0] - a[0], py = c[1] - a[1];
        double u = (px * sy - py * sx) / den, v = (px * ry - py * rx) / den;
        if (u < 0 || u > 1 || v < -1e-9 || v > 1 + 1e-9) return null;
        return u;
    }

    /// <summary>Wandfläche eines Teilstücks (a → b, Höhe zu … zo) samt Öffnungen der Wand darin.</summary>
    void Seitenflaeche(RaumInfo r, double[] a, double[] b, double laenge, double nx, double ny, WandInfo? w, RaumInfo? nachbar, string? draussen,
        double zu, double zo, List<Raumgrenze> aus)
    {
        double hoehe = zo - zu;
        if (hoehe <= Eps || laenge <= Eps) return;
        string gerechnet = nachbar is not null ? (nachbar.Beheizt ? "beheizt" : "unbeheizt") : draussen!;
        double flaeche = laenge * hoehe;
        double azimut = Raumgrenzen.Azimut(nx, ny, nordwinkel);
        double[] normale = [nx, ny, 0];
        var wand = w?.L.Wand;
        var vorgabeWand = Raumgrenzen.Vorgabe(wand);
        var oeffnungen = new List<Raumgrenze>();
        if (w is not null)
        {
            var l = w.L;
            double sa = l.Projektion(a[0], a[1]), sb = l.Projektion(b[0], b[1]);
            double s0 = Math.Min(sa, sb), s1 = Math.Max(sa, sb);
            double[] AufKante(double s) => Math.Abs(sb - sa) < Eps ? a : [a[0] + (b[0] - a[0]) * (s - sa) / (sb - sa), a[1] + (b[1] - a[1]) * (s - sa) / (sb - sa)];
            foreach (var o in w.Oeffnungen)
            {
                double von = Math.Max(s0, o.Von), bis = Math.Min(s1, o.Bis);
                if (bis - von <= Eps) continue;
                double ou = l.Unterkante + o.Unten, oo = l.Unterkante + (o.Oben ?? l.Hoehe ?? (zo - l.Unterkante));
                double z0 = Math.Max(zu, ou), z1 = Math.Min(zo, oo);
                if (z1 - z0 <= Eps) continue;
                double fo = (bis - von) * (z1 - z0);
                flaeche -= fo;
                oeffnungen.Add(Neu(r, nachbar, draussen, gerechnet, o.IstTuer ? "tuer" : "fenster", o.Element, fo / 1e6, azimut, 90, normale,
                    AufKante(von), AufKante(bis), z0, z1, false, null, Raumgrenzen.Vorgabe(o.Element) ?? vorgabeWand));
            }
        }
        if (flaeche < -1)
            hinweise.Dazu("Öffnungen grösser als die Wandfläche des Raums – Wandfläche 0", wand?.Kennzeichen ?? r.E.Kennzeichen);
        aus.Add(Neu(r, nachbar, draussen, gerechnet, "wand", wand, Math.Max(0, flaeche) / 1e6, azimut, 90, normale, a, b, zu, zo, w is null, null, vorgabeWand));
        aus.AddRange(oeffnungen);
    }

    Raumgrenze Neu(RaumInfo r, RaumInfo? nachbar, string? draussen, string gerechnet, string art, Element? bauteil, double flaecheM2,
        double? azimut, double neigung, double[] normale, double[]? von, double[]? bis, double unten, double oben, bool offen, PathsD? flaechen,
        string? vorgabe, List<double[]>? ring = null)
    {
        string angrenzung = gerechnet, nachbarId = nachbar?.E.Id ?? draussen!;
        Element? nachbarRaum = nachbar?.E;
        if (vorgabe is not null && vorgabe != gerechnet)
        {
            angrenzung = vorgabe;
            if (vorgabe is "aussen" or "erdreich") { nachbarId = vorgabe; nachbarRaum = null; }
            if (bauteil is not null && vorgabeGemeldet.Add($"{bauteil.Id}|{r.E.Id}|{gerechnet}"))
                hinweise.Dazu("Bauteil.angrenzung als Vorgabe überschreibt die gerechnete Angrenzung",
                    $"{bauteil.Kennzeichen} bei {r.E.Kennzeichen}: {vorgabe} statt {gerechnet}{(nachbar is null ? "" : $" ({nachbar.E.Kennzeichen})")}");
        }
        (double? U, string? Quelle) u = (null, null);
        if (bauteil is not null && !uWerte.TryGetValue(bauteil.Id, out u)) uWerte[bauteil.Id] = u = Raumgrenzen.UWert(m, k, bauteil);
        return new Raumgrenze
        {
            Raum = r.E, Nachbar = nachbarId, NachbarRaum = nachbarRaum, Angrenzung = angrenzung, Gerechnet = angrenzung != gerechnet ? gerechnet : null,
            Art = art, Bauteil = bauteil, Aufbau = bauteil is null ? null : Raumgrenzen.AufbauVon(m, k, bauteil),
            Flaeche = flaecheM2, Azimut = azimut, Neigung = neigung, U = u.U, UQuelle = u.Quelle, Offen = offen,
            Von = von, Bis = bis, Unten = unten, Oben = oben, Normale = normale, Flaechen = flaechen, Ring = ring,
        };
    }

    // ------------------------------------------------------------------ Boden und Decke

    void Horizontal(RaumInfo r, bool oben, List<Raumgrenze> aus)
    {
        var rest = new PathsD(r.Pfad);
        var teile = new List<(RaumInfo? S, PathsD F)>();
        foreach (var s in oben ? r.Darueber! : r.Darunter!)
        {
            var f = Schneiden(rest, s.Pfad);
            double inhalt = Inhalt(f);
            if (inhalt < Polygone.Splitter) continue;
            teile.Add((s, f));
            if (inhalt >= Inhalt(rest) - Polygone.Splitter) { rest = []; break; }   // ganz gedeckt
            rest = Polygone.Abziehen(rest, s.Pfad);
            if (rest.Count == 0) break;
        }
        if (Inhalt(rest) >= Polygone.Splitter)
        {
            // Wandband-Regel: Stücke über bzw. unter einer Wand des Nachbargeschosses gehen hälftig an die Räume daneben
            teile.AddRange(Wandbaender(r, rest, oben));
            teile = Zusammenlegen(teile);
        }

        double z = oben ? r.Oben : r.Unten;
        var neu = new List<Raumgrenze>();
        foreach (var (s, f) in teile)
        {
            string gerechnet; string? draussen = null;
            if (s is not null) gerechnet = s.Beheizt ? "beheizt" : "unbeheizt";
            else if (oben) gerechnet = draussen = "aussen";
            else gerechnet = draussen = r.Unterstes || r.Unten <= r.Terrain + Raumgrenzen.Toleranz ? "erdreich" : "aussen";
            // Höhenbereich, in dem die Platte liegen muss: zwischen den halben Höhen der beiden Räume (Räume aus Wänden reichen
            // von Kote zu Kote, die Platte liegt dann im Raum darunter), ohne Raum eine Geschosshöhe weit. Fuge = Bereich
            // zwischen Oberkante des unteren und Unterkante des oberen Raums.
            double mitte = r.Unten + r.Hoehe / 2, zu, zo, fuge0, fuge1;
            if (oben) { zu = mitte; zo = s is not null ? s.Unten + s.Hoehe / 2 : r.Oben + r.HoeheUeber; fuge0 = r.Oben; fuge1 = s?.Unten ?? r.Oben; }
            else { zo = mitte; zu = s is not null ? s.Unten + s.Hoehe / 2 : r.Unten - r.HoeheUnter; fuge0 = s?.Oben ?? r.Unten; fuge1 = r.Unten; }
            if (fuge0 > fuge1) (fuge0, fuge1) = (fuge1, fuge0);
            var (fx0, fy0, fx1, fy1) = Box(f);
            var rahmen = new RectD(fx0 - 1, fy0 - 1, fx1 + 1, fy1 + 1);
            var kandidaten = platten.Where(d => d.UK <= zo && d.OK >= zu && Boxen(fx0, fy0, fx1, fy1, d.MinX, d.MinY, d.MaxX, d.MaxY))
                .OrderByDescending(d => Ueberdeckung(d.UK, d.OK, zu, zo))
                .ThenBy(d => Math.Max(0, Math.Max(d.UK - fuge1, fuge0 - d.OK)))
                .ThenBy(d => d.E.Kennzeichen, StringComparer.OrdinalIgnoreCase).ToList();
            var restF = f;
            foreach (var d in kandidaten)
            {
                // Platte zuerst auf das Rechteck der Teilfläche zuschneiden (grosse Platten mit vielen Punkten)
                var dz = Clipper.RectClip(rahmen, d.Pfad, Polygone.Praezision);
                if (dz.Count == 0) continue;
                var g = Schneiden(restF, dz);
                double inhalt = Inhalt(g);
                if (inhalt < Polygone.Splitter) continue;
                neu.AddRange(Waagrecht(r, s, draussen, gerechnet, d.E, d.Dach, g, oben, z));
                if (inhalt >= Inhalt(restF) - Polygone.Splitter) { restF = []; break; }   // ganz gedeckt
                restF = Polygone.Abziehen(restF, dz);
                if (restF.Count == 0) break;
            }
            double restInhalt = Inhalt(restF);
            if (restInhalt >= Polygone.Splitter) neu.AddRange(Waagrecht(r, s, draussen, gerechnet, null, false, restF, oben, z));
        }
        // Splitter der grössten Teilfläche zuschlagen
        if (neu.Count > 1)
        {
            var groesste = neu.MaxBy(x => x.Flaeche)!;
            foreach (var x in neu.Where(x => x != groesste && x.Flaeche * 1e6 < Raumgrenzen.KleinsteFlaeche).ToList())
            {
                groesste.Flaeche += x.Flaeche;
                neu.Remove(x);
                splitter++;
            }
        }
        aus.AddRange(neu);
    }

    /// <summary>Wand mit ihrem Band (samt Toleranz) und dem Teil des Rests darin.</summary>
    sealed class BandKandidat
    {
        public required WandInfo W;
        public required PathD Band;
        public required PathsD ImBand;
        public double F;
    }

    /// <summary>Wandband-Regel (Entscheid Hauptlauf 18.10.2026): Rest von Boden bzw. Decke gegen die Wandbänder des Geschosses
    /// darunter bzw. darüber. Ein Stück im Band (Achse ± Dicke/2 samt Verlängerung, Toleranz 20 mm) wird an der Achse geteilt,
    /// jede Hälfte geht an den Raum auf ihrer Seite (NachSeite); ohne Raum auf der Seite und ausserhalb jedes Bands bleibt es
    /// ohne Raum (null, bisherige Regel). Wände mit der grössten Überdeckung des Rests zuerst.</summary>
    List<(RaumInfo? S, PathsD F)> Wandbaender(RaumInfo r, PathsD rest, bool oben)
    {
        var aus = new List<(RaumInfo? S, PathsD F)>();
        double tol = Raumgrenzen.Toleranz;
        var waendeNah = oben ? (r.WaendeOben ??= NaheWaende(r, true)) : (r.WaendeUnten ??= NaheWaende(r, false));
        // Kandidaten: Band samt Toleranz überdeckt den Rest. Geschnitten wird nur mit den Reststücken, deren Box das Band berührt
        // (ein Loch, dessen Box das Band nicht berührt, ändert den Schnitt nicht).
        var boxen = new (double X0, double Y0, double X1, double Y1)[rest.Count];
        for (int i = 0; i < rest.Count; i++) boxen[i] = Box([rest[i]]);
        var kand = new List<BandKandidat>();
        foreach (var w in waendeNah)
        {
            var nah = new PathsD();
            for (int i = 0; i < rest.Count; i++)
                if (Boxen(boxen[i].X0, boxen[i].Y0, boxen[i].X1, boxen[i].Y1, w.MinX, w.MinY, w.MaxX, w.MaxY, tol + 1)) nah.Add(rest[i]);
            if (nah.Count == 0) continue;
            var l = w.L;
            double h = l.Dicke / 2 + tol;
            var band = l.Rechteck(l.Von - tol, l.Bis + tol, -h, h);
            var imBand = Schneiden(nah, [band]);
            double f = Inhalt(imBand);
            if (f >= Polygone.Splitter) kand.Add(new BandKandidat { W = w, Band = band, ImBand = imBand, F = f });
        }
        if (kand.Count == 0) { aus.Add((null, rest)); return aus; }
        // grösste Überdeckung zuerst, dann die dickere Wand, dann Kennzeichen und Id (eindeutig, also feste Reihenfolge)
        kand.Sort((a, b) =>
        {
            int c = b.F.CompareTo(a.F);
            if (c == 0) c = b.W.L.Dicke.CompareTo(a.W.L.Dicke);
            if (c == 0) c = StringComparer.OrdinalIgnoreCase.Compare(a.W.L.Wand.Kennzeichen, b.W.L.Wand.Kennzeichen);
            if (c == 0) c = string.CompareOrdinal(a.W.L.Id, b.W.L.Id);
            return c;
        });
        var raeumeNah = oben ? (r.RaeumeOben ??= NaheRaeume(r, true)) : (r.RaeumeUnten ??= NaheRaeume(r, false));
        // Was eine frühere Wand genommen hat, sieht die spätere nicht mehr: ihr Bandstück minus die Bänder der früheren Wände
        // (= Rest nach den früheren Wänden ∩ Band); der Rest ausserhalb aller Bänder am Schluss in einem Abzug.
        var genommen = new List<BandKandidat>();
        foreach (var k in kand)
        {
            var w = k.W;
            var vorher = new PathsD();
            foreach (var g in genommen)
                if (Boxen(w.MinX, w.MinY, w.MaxX, w.MaxY, g.W.MinX, g.W.MinY, g.W.MaxX, g.W.MaxY, 2 * tol + 1)) vorher.Add(g.Band);
            var imBand = vorher.Count == 0 ? k.ImBand : Polygone.Abziehen(k.ImBand, vorher);
            if (Inhalt(imBand) < Polygone.Splitter) continue;   // von früheren Wänden schon genommen
            var l = w.L;
            double h = l.Dicke / 2 + tol, qMin = double.MaxValue, qMax = double.MinValue;
            foreach (var p in imBand) foreach (var q in p) { double x = l.Quer(q.x, q.y); qMin = Math.Min(qMin, x); qMax = Math.Max(qMax, x); }
            for (int seite = 1; seite >= -1; seite -= 2)
            {
                if (seite > 0 ? qMax <= Eps : qMin >= -Eps) continue;   // nichts auf dieser Seite
                var stueck = (seite > 0 ? qMin >= -Eps : qMax <= Eps) ? imBand   // ganz auf dieser Seite
                    : Schneiden(imBand, [l.Rechteck(l.Von - tol, l.Bis + tol, seite > 0 ? 0 : -h, seite > 0 ? h : 0)]);
                if (Inhalt(stueck) < Polygone.Splitter) continue;
                NachSeite(r, w, seite, stueck, raeumeNah, waendeNah, aus);
            }
            genommen.Add(k);
        }
        if (genommen.Count > 0)
        {
            var baender = new PathsD(genommen.Count);
            foreach (var g in genommen) baender.Add(g.Band);
            rest = Polygone.Abziehen(rest, baender);
        }
        if (Inhalt(rest) >= Polygone.Splitter) aus.Add((null, rest));
        return aus;
    }

    /// <summary>Halbes Wandband (seite +1 = links der Wandrichtung, −1 = rechts): Raum auf dieser Seite entlang der Wand über
    /// die Suchlinie Dicke/2 + 50 mm neben der Achse (wie bei den Seiten); je Abschnitt der Linie ein Teil des Stücks. Die Linie
    /// reicht an beiden Enden um die halbe grösste Wanddicke + 50 + 2 × 20 mm über das Stück hinaus, damit eine Querwand am Ende
    /// (Anschluss, Ecke) beidseitig Nachbarn hat und an ihrer Achse geteilt wird (Annahme).</summary>
    void NachSeite(RaumInfo r, WandInfo w, int seite, PathsD stueck, List<RaumInfo> raeumeNah, List<WandInfo> waendeNah, List<(RaumInfo? S, PathsD F)> aus)
    {
        var l = w.L;
        double s0 = double.MaxValue, s1 = double.MinValue;
        foreach (var p in stueck) foreach (var q in p) { double s = l.Projektion(q.x, q.y); s0 = Math.Min(s0, s); s1 = Math.Max(s1, s); }
        double ueber = dickeMax / 2 + Raumgrenzen.Versatz + 2 * Raumgrenzen.Toleranz, e0 = s0 - ueber, e1 = s1 + ueber;
        double qs = seite * (l.Dicke / 2 + Raumgrenzen.Versatz);
        var abschnitte = NachbarnEntlang(raeumeNah, waendeNah, r.Unten, r.Oben, l.Punkt(e0, qs), l.Punkt(e1, qs), w, splitterVorher: true);
        // Abschnitte als Bereiche von s (der erste und der letzte offen), nur die, die das Stück berühren
        var imStueck = new List<GrenzAbschnitt<RaumInfo?>>();
        for (int i = 0; i < abschnitte.Count; i++)
        {
            var x = abschnitte[i];
            double a = i == 0 ? double.NegativeInfinity : e0 + (e1 - e0) * x.A, b = i == abschnitte.Count - 1 ? double.PositiveInfinity : e0 + (e1 - e0) * x.B;
            if (b > s0 && a < s1) imStueck.Add(new(a, b, x.Wert));
        }
        int vorher = aus.Count;
        if (imStueck.Count <= 1) aus.Add((imStueck.Count == 1 ? imStueck[0].Wert : null, stueck));
        else
        {
            double h = l.Dicke / 2 + Raumgrenzen.Toleranz + 1, q0 = seite > 0 ? -1 : -h, q1 = seite > 0 ? h : 1;
            foreach (var x in imStueck)
            {
                var teil = Schneiden(stueck, [l.Rechteck(Math.Max(x.A, s0 - 1), Math.Min(x.B, s1 + 1), q0, q1)]);
                if (Inhalt(teil) >= Polygone.Splitter) aus.Add((x.Wert, teil));
            }
        }
        for (int i = vorher; i < aus.Count; i++)
            if (aus[i].S is not null) { bandStuecke++; bandFlaeche += Inhalt(aus[i].F); }
    }

    /// <summary>Teilflächen desselben Nachbarn vereinigen (Reihenfolge des ersten Auftretens, ohne Raum zuletzt).</summary>
    static List<(RaumInfo? S, PathsD F)> Zusammenlegen(List<(RaumInfo? S, PathsD F)> teile)
    {
        var nachbarn = new List<RaumInfo?>();
        var flaechen = new List<PathsD>();
        var anzahl = new List<int>();
        foreach (bool ohneRaum in new[] { false, true })
            foreach (var (s, f) in teile)
            {
                if ((s is null) != ohneRaum) continue;
                int i = nachbarn.IndexOf(s);   // Referenzgleichheit
                if (i < 0) { nachbarn.Add(s); flaechen.Add(new PathsD(f)); anzahl.Add(1); }
                else { flaechen[i].AddRange(f); anzahl[i]++; }
            }
        var aus = new List<(RaumInfo? S, PathsD F)>(nachbarn.Count);
        for (int i = 0; i < nachbarn.Count; i++) aus.Add((nachbarn[i], anzahl[i] == 1 ? flaechen[i] : Polygone.Vereinigen(flaechen[i])));
        return aus;
    }

    /// <summary>Wände des Geschosses darunter bzw. darüber nahe beim Raum (Höhenfenster wie die Räume; nur mit Dicke).</summary>
    List<WandInfo> NaheWaende(RaumInfo r, bool oben)
    {
        double rand = dickeMax + Raumgrenzen.Versatz + Raumgrenzen.Toleranz + 1;
        return waende.Where(w => w.L.Dicke > 0 && Boxen(r.MinX, r.MinY, r.MaxX, r.MaxY, w.MinX, w.MinY, w.MaxX, w.MaxY, rand)
                                 && (oben ? Ueber(r, w.UK, w.OK, w.OK - w.UK) : Unter(r, w.UK, w.OK, w.OK - w.UK))).ToList();
    }

    /// <summary>Räume darunter bzw. darüber nahe beim Raum (für die Suchlinie neben den Wandbändern).</summary>
    List<RaumInfo> NaheRaeume(RaumInfo r, bool oben)
    {
        double rand = dickeMax + Raumgrenzen.Versatz + Raumgrenzen.Toleranz + 1;
        return raeume.Where(s => s != r && Boxen(r.MinX, r.MinY, r.MaxX, r.MaxY, s.MinX, s.MinY, s.MaxX, s.MaxY, rand)
                                 && (oben ? Ueber(r, s.Unten, s.Oben, s.Hoehe) : Unter(r, s.Unten, s.Oben, s.Hoehe))).ToList();
    }

    /// <summary>Waagrechte Raumgrenzen einer Fläche f: je zusammenhängender Teil eine, mit einem Ring für die Darstellung.</summary>
    IEnumerable<Raumgrenze> Waagrecht(RaumInfo r, RaumInfo? s, string? draussen, string gerechnet, Element? bauteil, bool dach, PathsD f, bool oben, double z)
    {
        var vorgabe = Raumgrenzen.Vorgabe(bauteil);
        string endgueltig = vorgabe ?? gerechnet;
        string art = !oben ? "boden" : dach || endgueltig == "aussen" ? "dach" : "decke";
        foreach (var teil in Polygone.Flaechen(f))
        {
            var pfade = new PathsD { Polygone.Pfad(teil.Aussen) };
            foreach (var loch in teil.Loecher) pfade.Add(Polygone.Pfad(loch));
            var ring = teil.Loecher.Count == 0 ? teil.Aussen : RaeumeAusWaenden.MitInseln(teil.Aussen, teil.Loecher, out _);
            yield return Neu(r, s, draussen, gerechnet, art, bauteil, teil.Inhalt / 1e6, null, oben ? 0 : 180, oben ? [0, 0, 1] : [0, 0, -1],
                null, null, z, z, false, pfade, vorgabe, ring);
        }
    }

    static (double, double, double, double) Box(PathsD f)
    {
        double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
        foreach (var p in f) foreach (var q in p) { x0 = Math.Min(x0, q.x); y0 = Math.Min(y0, q.y); x1 = Math.Max(x1, q.x); y1 = Math.Max(y1, q.y); }
        return (x0, y0, x1, y1);
    }

    static PathsD Schneiden(PathsD a, PathsD b)
    {
        if (a.Count == 0 || b.Count == 0) return [];
        var c = new ClipperD(Polygone.Praezision) { PreserveCollinear = false };
        c.AddSubject(a);
        c.AddClip(b);
        var aus = new PathsD();
        c.Execute(ClipType.Intersection, FillRule.NonZero, aus);
        aus.RemoveAll(p => Math.Abs(Clipper.Area(p)) < Polygone.Splitter);
        return aus;
    }

    /// <summary>Netto-Fläche (Aussenringe minus Löcher) in mm².</summary>
    static double Inhalt(PathsD p) => Math.Abs(Clipper.Area(p));

    static string M2(double mm2) => Grafik.Format(mm2 / 1e6, 2);
}
