// Schemasymbole Heizung und Kälte (Modul 5, Teil A-S 2): Übertragung aus der HKAP-Symbolbibliothek
// (AufgabenProtokolle.Web/Services/SchemaSymbole.cs, Abschnitte «Wärmeerzeugung» bis «Messen / Regeln»; die Symbole sind dort nach der
// H+K-Legende des Riehen-Plans gezeichnet). Die Geometrie steht hier in HKAP-Millimetern mit dem Ursprung in der Symbolmitte und der
// y-Achse nach unten, Zeile für Zeile wie in der Quelle; erst beim Zeichnen wird sie ins Zeichenmodell des Kerns umgerechnet
// (HeizungSymbole.Primitive). Der Auszug aus der Quelle entsteht mit werkzeuge/hkap_symbole_referenz (Referenz der Tests).
//
// H+K-Konvention: Armaturen stehen SENKRECHT (Durchfluss oben → unten, Anschlüsse N/S), der Stellantrieb sitzt links. Für waagrechten
// Einbau wird das Symbol um 90° gedreht (HzLage). Liegende Symbole (Klappen, Zähler) sind waagrecht gezeichnet (W/E).
//
// Umrechnung HKAP → Kern (alle Werte in Papier-mm):
//   - Massstab: HeizungSymbole.Skala (ANNAHME 0.625: die Umwälzpumpe Ø 8 der HKAP wird Ø 5 wie die bestehende Klassendarstellung der
//     Pumpe; die Proportionen bleiben die der HKAP).
//   - y gespiegelt (HKAP y nach unten, Kern y nach oben). Bögen: HKAP-Winkel im Uhrzeigersinn ab 3 Uhr, Kern im Gegenuhrzeigersinn
//     (von = −(start + sweep), bis = von + sweep). Rechtecke werden zu Polygonen (Ecken in der Reihenfolge der Quelle).
//   - Linienstärke: HKAP-Strichstärke 0 (Standard) → duenn, Umrisse mit eigener Stärke (0.4/0.5: Apparate, Zylinder, Verteiler) → mittel
//     (ANNAHME; die Bibliothek kennt nur drei Stifte).
//   - Linienfarbe: Planfarbe (die H+K-Symbole sind schwarz), ausser Symbole mit Standardfarbe (Ausdehnungsgefäss Heizung rot, Kälte
//     magenta, wie die HKAP sie beim Platzieren vorgibt).
//   - Füllung: Fill ohne Farbe → Linienfarbe («schnitt»), Fill mit Farbe → diese Farbe, «#FFFFFF» → «papier». Flächen ohne Füllung
//     bleiben durchsichtig (der grüne Apparat scheint durch); nur Armaturen und Zähler, die in einer Leitung sitzen (Lage ≠ Fest), werden
//     «papier» gefüllt, damit sie die Leitung unter dem Symbol verdecken (wie die bestehenden Klassendarstellungen und Sinnbilder).
//   - Hinterlegung (HKAP FlaecheApparat/-Gefaess/-ApparatKaelte): Apparat #CCFFE0, Gefäss #E4D6EA, Apparat der Kälte (Medium KAE-…)
//     #CCE0FF; die Kälte-Variante ersetzt nur #CCFFE0.
//   - Texte: Mitte des Textes an der HKAP-Stelle, Schrifthöhe mal Skala. Platzhalter «$1»/«$2» (Bezeichnung/Zusatz im Feld) bleiben leer,
//     solange der Aufrufer keinen Text liefert; «$FS» und «$MSR» zeigen ihren Vorgabewert (FS, MSR).
//
// Nicht übertragen (gehören nicht zu Heizung und Kälte): zapfstelle (Sanitär), knoten, blattverweis, kompensator, kamin, gaszaehler (Diverses).

namespace Hlks.Kern;

/// <summary>Einbaulage eines Symbols: Fest = steht aufrecht (Apparate, Instrumente, Antriebe); Senkrecht = Durchgangsarmatur mit Durchfluss
/// oben → unten (an einer waagrechten Leitung um 90° gedreht); Waagrecht = liegende Armatur mit Durchfluss links → rechts (an einer
/// senkrechten Leitung um 90° gedreht).</summary>
public enum HzLage { Fest, Senkrecht, Waagrecht }

/// <summary>Ein Zeichenteil eines HKAP-Symbols (HKAP-Typ SchemaGeo): line [x1,y1,x2,y2] · rect [x,y,w,h] · circle [cx,cy,r] ·
/// arc [cx,cy,r,start,sweep] · poly [x1,y1,…] · text [x,y]. HKAP-Millimeter, y nach unten, Ursprung = Symbolmitte.</summary>
public sealed class HzGeo
{
    public string T { get; set; } = "line";
    public double[] P { get; set; } = [];
    public bool Fill { get; set; }
    /// <summary>Füllfarbe; leer + Fill = mit der Linienfarbe gefüllt.</summary>
    public string Fc { get; set; } = "";
    public bool Close { get; set; }
    /// <summary>Strichstärke der HKAP in mm; 0 = Standard.</summary>
    public double W { get; set; }
    public bool Dash { get; set; }
    public string S { get; set; } = "";
    public double Size { get; set; } = 2.8;
    public bool Bold { get; set; }
}

/// <summary>Anschlusspunkt eines HKAP-Symbols (HKAP-Typ SchemaAnschluss): Name, Lage in HKAP-Millimetern, Richtung N | S | E | W.</summary>
public sealed class HzAnschluss
{
    public string Id { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public string Dir { get; set; } = "E";
}

/// <summary>Ein Heizungs- oder Kältesymbol der HKAP-Bibliothek.</summary>
public sealed class HzSymbol
{
    public string Key { get; set; } = "";
    public string Gruppe { get; set; } = "";
    public string Label { get; set; } = "";
    /// <summary>Nennbreite/-höhe in HKAP-Millimetern.</summary>
    public double W { get; set; }
    public double H { get; set; }
    public string LabelPos { get; set; } = "unten";
    public string Suchbegriffe { get; set; } = "";
    /// <summary>Vorgabe-Linienfarbe der HKAP (leer = Planfarbe).</summary>
    public string StandardFarbe { get; set; } = "";
    public List<HzGeo> Geo { get; set; } = [];
    public List<HzAnschluss> Anschluesse { get; set; } = [];
    public HzLage Lage { get; set; }
}

/// <summary>Anschlusspunkt im Kern in Papier-mm um den Symbolursprung (y nach oben), Richtung N | S | E | W auf dem Bildschirm.</summary>
public readonly record struct HzPunkt(string Id, double X, double Y, string Dir);

/// <summary>Zuordnung Klasse (und Art) → Symbol; Art null = jede Art der Klasse.</summary>
public readonly record struct HzZuordnung(string Klasse, string? Art, string Key);

public static class HeizungSymbole
{
    /// <summary>Massstab HKAP-Millimeter → Papier-Millimeter (ANNAHME Prüfliste: Pumpe Ø 8 → Ø 5 wie die bestehende Klassendarstellung).</summary>
    public const double Skala = 0.625;

    /// <summary>Hinterlegung von Apparaten der Heizung (HKAP FlaecheApparat).</summary>
    public const string FlaecheApparat = "#CCFFE0";
    /// <summary>Hinterlegung von Gefässen: Speicher-Ausdehnung, Druckhaltung (HKAP FlaecheGefaess).</summary>
    public const string FlaecheGefaess = "#E4D6EA";
    /// <summary>Hinterlegung von Apparaten der Kälte (HKAP FlaecheApparatKaelte).</summary>
    public const string FlaecheApparatKaelte = "#CCE0FF";

    // ---------------------------------------------------------------- Primitiv-Helfer (wie HKAP SchemaSymbole.cs)

    static HzGeo L(double x1, double y1, double x2, double y2, double w = 0)
        => new() { T = "line", P = [x1, y1, x2, y2], W = w };

    static HzGeo Rc(double x, double y, double w, double h, bool fill = false, double lw = 0)
        => new() { T = "rect", P = [x, y, w, h], Fill = fill, W = lw };

    /// <summary>Farbig hinterlegte Fläche mit schwarzem Umriss (Apparate der H+K-Pläne).</summary>
    static HzGeo RcF(double x, double y, double w, double h, string farbe, double lw = 0)
        => new() { T = "rect", P = [x, y, w, h], Fill = true, Fc = farbe, W = lw };

    static HzGeo Ci(double cx, double cy, double r, bool fill = false, double lw = 0)
        => new() { T = "circle", P = [cx, cy, r], Fill = fill, W = lw };

    static HzGeo Ar(double cx, double cy, double r, double start, double sweep, double lw = 0)
        => new() { T = "arc", P = [cx, cy, r, start, sweep], W = lw };

    /// <summary>Geschlossenes Polygon (Umriss oder gefüllt).</summary>
    static HzGeo Po(bool fill, params double[] pts)
        => new() { T = "poly", P = pts, Fill = fill, Close = true };

    /// <summary>Offener Polylinienzug (Mäander, Wendel, Feder …).</summary>
    static HzGeo Pl(params double[] pts)
        => new() { T = "poly", P = pts, Fill = false, Close = false };

    static HzGeo Tx(double x, double y, string s, double size = 2.8, bool bold = false)
        => new() { T = "text", P = [x, y], S = s, Size = size, Bold = bold };

    static HzAnschluss An(string id, double x, double y, string dir)
        => new() { Id = id, X = x, Y = y, Dir = dir };

    /// <summary>Ventil-Grundform der H+K-Pläne: SENKRECHTE Sanduhr, oberes Dreieck mit Basis oben und Spitze in der Mitte, unteres
    /// gespiegelt (Durchfluss von oben nach unten).</summary>
    static HzGeo Sanduhr(double b = 3.4, double h = 3.4)
        => Po(false, -b, -h, b, -h, -b, h, b, h);

    /// <summary>Nur das obere bzw. untere Dreieck der Sanduhr (für teilgefüllte Armaturen).</summary>
    static HzGeo DreieckOben(bool fill, double b = 3.4, double h = 3.4)
        => Po(fill, -b, -h, b, -h, 0, 0);
    static HzGeo DreieckUnten(bool fill, double b = 3.4, double h = 3.4)
        => Po(fill, -b, h, b, h, 0, 0);
    /// <summary>Dritter Abgang eines Mehrwegventils: Flügel nach rechts, bewusst schmaler und weiter aussen als die Sanduhr.</summary>
    static HzGeo DreieckRechts(bool fill, double b = 2.7, double h = 4.6)
        => Po(fill, h, -b, h, b, 0, 0);

    /// <summary>Stellantrieb links neben der Armatur: Quadrat mit grossem Kreis und «M» (Legende «Motor»), mit Steg zur Ventilachse.</summary>
    static IEnumerable<HzGeo> MotorLinks(double x = -10.4)
        => new[] { Rc(x, -2.6, 5.2, 5.2), Ci(x + 2.6, 0, 2.3), Tx(x + 2.6, 0, "M", 2.4),
                   L(x + 5.2, 0, -1.4, 0) };

    /// <summary>Zeigerinstrument (Thermometer/Manometer): Kreis, gefüllter Zeiger nach oben rechts, dünner Gegenzeiger, Kürzel rechts unten.</summary>
    static IEnumerable<HzGeo> Instrument(string kuerzel)
        => new[] { Ci(0, 0, 3.6),
                   L(0, 0, -2.2, 2.2),
                   Po(true, 2.6, -2.6, 0.4, -2.9, 2.9, -0.4),
                   Tx(4.4, 3.6, kuerzel, 3.0) };

    /// <summary>Die zwei Anschlüsse einer Durchgangsarmatur, bei H+K senkrecht (oben/unten).</summary>
    static List<HzAnschluss> Durchgang(double h = 3.4)
        => [An("1", 0, -h, "N"), An("2", 0, h, "S")];

    /// <summary>Brennerflamme im unteren Teil eines Kessels.</summary>
    static HzGeo Flamme()
        => Po(true, 0, 3.5, 2.0, 7.2, 1.5, 9.4, 2.6, 11.4, 0, 12.6, -2.6, 11.4, -1.5, 9.4, -2.0, 7.2);

    // ---------------------------------------------------------------- Bibliothek (HKAP SchemaSymbole.Build, Heizung und Kälte)

    static readonly List<HzSymbol> _alle = Bauen();

    /// <summary>Alle übertragenen Symbole in der Reihenfolge der HKAP (Wärmeerzeugung, Speicher, Verteilung, Wärmeübertrager, Pumpen,
    /// Armaturen, Regelarmaturen, Verbraucher, Messen / Regeln). Gruppe und Label sind die Benennung der Legende.</summary>
    public static IReadOnlyList<HzSymbol> Alle => _alle;

    /// <summary>Die Symbole nach Schlüssel (HKAP-Key).</summary>
    public static readonly IReadOnlyDictionary<string, HzSymbol> Bibliothek = _alle.ToDictionary(s => s.Key);

    static List<HzSymbol> Bauen()
    {
        var l = new List<HzSymbol>();

        void S(string key, string gruppe, string label, double w, double h,
               List<HzGeo> geo, List<HzAnschluss> an, string labelPos = "unten",
               string? such = null, string? farbe = null)
            => l.Add(new HzSymbol
            {
                Key = key, Gruppe = gruppe, Label = label, W = w, H = h,
                Geo = geo, Anschluesse = an, LabelPos = labelPos, Suchbegriffe = such ?? "",
                StandardFarbe = farbe ?? "", Lage = LageVon(key),
            });

        // ===================== Wärmeerzeugung =====================

        S("kessel", "Wärmeerzeugung", "Heizkessel", 26, 32, new()
        {
            RcF(-13, -16, 26, 32, FlaecheApparat, 0.4),
            Flamme(),
            L(-6, -6, 6, -6), L(-6, -2, 6, -2),
        }, new()
        {
            An("VL", 6, -16, "N"), An("RL", 6, 16, "S"),
            An("L1", -13, -8, "W"), An("L2", -13, 8, "W"),
            An("R1", 13, -8, "E"), An("R2", 13, 8, "E"),
            An("BR", 0, 16, "S"), An("ABG", -6, -16, "N"),
        }, such: "heizkessel gas öl brenner");

        S("kessel_holz", "Wärmeerzeugung", "Holz-/Pelletkessel", 26, 32, new()
        {
            RcF(-13, -16, 26, 32, FlaecheApparat, 0.4),
            Flamme(),
            Rc(-9, -12, 18, 7),
            L(-9, -8.5, 9, -8.5), L(-3, -12, -3, -5), L(3, -12, 3, -5),
        }, new()
        {
            An("VL", 6, -16, "N"), An("RL", 6, 16, "S"),
            An("L1", -13, -8, "W"), An("L2", -13, 8, "W"),
            An("R1", 13, -8, "E"), An("R2", 13, 8, "E"),
            An("ABG", -6, -16, "N"),
        }, such: "holz pellet schnitzel stückholz");

        S("waermepumpe", "Wärmeerzeugung", "Wärmepumpe", 28, 28, new()
        {
            RcF(-14, -14, 28, 28, FlaecheApparat, 0.4),
            Ci(0, 0, 6),
            Po(true, -2.4, -3.4, -2.4, 3.4, 3.6, 0),      // Verdichter
            Rc(-12.5, -4.5, 3, 9),                        // Verdampfer (Quellenseite)
            Rc(9.5, -4.5, 3, 9),                          // Kondensator (Heizungsseite)
            L(-9.5, 0, -6, 0), L(6, 0, 9.5, 0),
        }, new()
        {
            An("VL", 8, -14, "N"), An("RL", -8, -14, "N"),
            An("QE", -14, 8, "W"), An("QA", -14, -8, "W"),
            An("HV", 14, -8, "E"), An("HR", 14, 8, "E"),
        }, such: "wp sole wasser erdsonde kompressor");

        S("waermepumpe_luft", "Wärmeerzeugung", "Luft/Wasser-WP (Aussengerät)", 28, 22, new()
        {
            RcF(-14, -11, 28, 22, FlaecheApparat, 0.4),
            Ci(-4, 0, 6.5),
            L(-4, 0, -4, -6.5), L(-4, 0, 1.6, 3.2), L(-4, 0, -9.6, 3.2),
            Ci(-4, 0, 1.2, true),
            L(8, -7, 8, 7), L(6, -7, 6, 7), L(10, -7, 10, 7),
        }, new()
        {
            An("VL", 8, -11, "N"), An("RL", -8, -11, "N"),
            An("HV", 14, -5, "E"), An("HR", 14, 5, "E"),
        }, such: "luftwärmepumpe aussengerät monoblock ventilator");

        S("fernwaerme", "Wärmeerzeugung", "Fernwärme-Übergabe", 26, 26, new()
        {
            RcF(-13, -13, 26, 26, FlaecheApparat, 0.4),
            Pl(-6, -8, 6, -4, -6, 0, 6, 4, -6, 8),
            Tx(0, -10.5, "FW", 3, true),
        }, new()
        {
            An("PV", -13, -6, "W"), An("PR", -13, 6, "W"),
            An("SV", 13, -6, "E"), An("SR", 13, 6, "E"),
        }, such: "fernwärme übergabestation nahwärme");

        S("solarkollektor", "Wärmeerzeugung", "Sonnenkollektor", 32, 14, new()
        {
            RcF(-16, -7, 32, 14, FlaecheApparat, 0.4),
            Rc(-14, -5, 28, 10),
            L(-10, 5, -5, -5), L(-4, 5, 1, -5), L(2, 5, 7, -5), L(8, 5, 13, -5),
        }, new()
        {
            An("VL", 16, -7, "E"), An("RL", -16, 7, "W"),
        }, such: "solar kollektor thermisch röhrenkollektor");

        S("elektroheizung", "Wärmeerzeugung", "Elektro-Heizeinsatz", 16, 9, new()
        {
            Rc(-8, -4.5, 16, 9),
            Pl(-5.5, 0, -4, -2.5, -2, 2.5, 0, -2.5, 2, 2.5, 4, -2.5, 5.5, 0),
        }, new() { An("1", -8, 0, "W"), An("2", 8, 0, "E"), An("A", 0, 4.5, "S") },
           such: "elektro heizstab einschraubheizkörper e-patrone");

        S("bhkw", "Wärmeerzeugung", "BHKW / WKK", 28, 22, new()
        {
            RcF(-14, -11, 28, 22, FlaecheApparat, 0.4),
            Ci(-5, 0, 5), Tx(-5, 0, "M", 3.4, true),
            Ci(6, 0, 5), Tx(6, 0, "G", 3.4, true),
            L(0, 0, 1, 0),
        }, new()
        {
            An("VL", 8, -11, "N"), An("RL", -8, -11, "N"),
            An("GAS", -14, 6, "W"), An("ABG", 0, -11, "N"),
        }, such: "blockheizkraftwerk wkk kraftwärmekopplung");

        // ===================== Speicher & Ausdehnung =====================

        // In den H+K-Plänen ist der Speicher ein hinterlegtes Rechteck mit Beschriftung im Feld.
        S("speicher", "Speicher", "Energiespeicher", 26, 44, new()
        {
            RcF(-13, -22, 26, 44, FlaecheApparat, 0.4),
            Tx(0, -6, "$1", 2.8), Tx(0, -2.4, "$2", 2.8),
        }, new()
        {
            An("O", 0, -22, "N"), An("U", 0, 22, "S"),
            An("L1", -13, -16, "W"), An("L2", -13, -6, "W"), An("L3", -13, 6, "W"), An("L4", -13, 16, "W"),
            An("R1", 13, -16, "E"), An("R2", 13, -6, "E"), An("R3", 13, 6, "E"), An("R4", 13, 16, "E"),
        }, "keine", "puffer speicher energiespeicher");

        S("speicher_zylinder", "Speicher", "Speicher (Zylinderform)", 20, 40, new()
        {
            L(-10, -10, -10, 10, 0.5), L(10, -10, 10, 10, 0.5),
            Ar(0, -10, 10, 180, 180, 0.5), Ar(0, 10, 10, 0, 180, 0.5),
        }, new()
        {
            An("O", 0, -20, "N"), An("U", 0, 20, "S"),
            An("L1", -10, -12, "W"), An("L2", -10, -4, "W"), An("L3", -10, 4, "W"), An("L4", -10, 12, "W"),
            An("R1", 10, -12, "E"), An("R2", 10, -4, "E"), An("R3", 10, 4, "E"), An("R4", 10, 12, "E"),
        }, such: "puffer speicher zylinder stehend");

        S("speicher_register", "Speicher", "Speicher mit Register", 20, 40, new()
        {
            L(-10, -10, -10, 10, 0.5), L(10, -10, 10, 10, 0.5),
            Ar(0, -10, 10, 180, 180, 0.5), Ar(0, 10, 10, 0, 180, 0.5),
            // Register nur im geraden Mantelbereich (|y| ≤ 10) — sonst ragt es aus der Rundung.
            Pl(-10, 9, 5, 9, 5, 6, -5, 6, -5, 3, 5, 3, 5, 0, -10, 0),
        }, new()
        {
            An("O", 0, -20, "N"), An("U", 0, 20, "S"),
            An("L1", -10, -8, "W"), An("L2", -10, -4, "W"),
            An("R1", 10, -8, "E"), An("R2", 10, 8, "E"),
            An("RegE", -10, 0, "W"), An("RegA", -10, 9, "W"),
        }, such: "speicher register wendel wärmetauscher solarspeicher");

        S("speicher_2register", "Speicher", "Speicher, 2 Register", 20, 44, new()
        {
            L(-10, -12, -10, 12, 0.5), L(10, -12, 10, 12, 0.5),
            Ar(0, -12, 10, 180, 180, 0.5), Ar(0, 12, 10, 0, 180, 0.5),
            Pl(-10, 11, 5, 11, 5, 8, -5, 8, -5, 5, 5, 5, 5, 2, -10, 2),      // unteres Register
            Pl(-10, -2, 5, -2, 5, -5, -5, -5, -5, -8, 5, -8, 5, -11, -10, -11), // oberes Register
        }, new()
        {
            An("O", 0, -22, "N"), An("U", 0, 22, "S"),
            An("R1", 10, -8, "E"), An("R2", 10, 8, "E"),
            An("Reg1E", -10, 2, "W"), An("Reg1A", -10, 11, "W"),
            An("Reg2E", -10, -11, "W"), An("Reg2A", -10, -2, "W"),
        }, such: "kombispeicher zwei register solar");

        S("boiler", "Speicher", "Trinkwarmwasserspeicher", 20, 40, new()
        {
            L(-10, -10, -10, 10, 0.5), L(10, -10, 10, 10, 0.5),
            Ar(0, -10, 10, 180, 180, 0.5), Ar(0, 10, 10, 0, 180, 0.5),
            Pl(-10, 9, 5, 9, 5, 6, -5, 6, -5, 3, 5, 3, 5, 0, -10, 0),
            Tx(0, -12.5, "WW", 2.8, true),
        }, new()
        {
            An("WW", 0, -20, "N"), An("KW", 0, 20, "S"),
            An("ZIRK", 10, -6, "E"),
            An("RegE", -10, 0, "W"), An("RegA", -10, 9, "W"),
            An("L1", -10, -8, "W"),
        }, such: "boiler warmwasser trinkwasser ww-speicher");

        S("hydr_weiche", "Speicher", "Hydraulische Weiche", 12, 34, new()
        {
            RcF(-6, -17, 12, 34, FlaecheApparat, 0.4),
            L(-6, -6, 6, -6), L(-6, 6, 6, 6),
        }, new()
        {
            An("PV", -6, -12, "W"), An("PR", -6, 12, "W"),
            An("SV", 6, -12, "E"), An("SR", 6, 12, "E"),
            An("O", 0, -17, "N"), An("U", 0, 17, "S"),
        }, such: "weiche hydraulische entkopplung");

        // Wie in den H+K-Plänen: stehendes Gefäss mit gewölbtem Deckel, violett hinterlegt.
        S("ausdehnungsgefaess", "Speicher", "Ausdehnungsgefäss (MAG)", 14, 26, new()
        {
            new HzGeo { T = "rect", P = new[] { -6.5, -6.0, 13.0, 15.0 },
                            Fill = true, Fc = FlaecheGefaess, W = 0.4 },
            new HzGeo { T = "arc", P = new[] { 0.0, -6.0, 6.5, 180.0, 180.0 }, W = 0.4 },
            Ar(0, 3.0, 6.5, 200, 140),
            L(0, 9, 0, 13),
        }, new() { An("A", 0, 13, "S") },
           such: "mag expansionsgefäss ausdehnung membran compresso",
           farbe: "#FF0000");   // an der Heizung rot; im Kältekreis am Element umstellen

        S("druckhaltung_kaelte", "Speicher", "Ausdehnungsgefäss Kälte", 14, 26, new()
        {
            new HzGeo { T = "rect", P = new[] { -6.5, -6.0, 13.0, 15.0 },
                            Fill = true, Fc = FlaecheGefaess, W = 0.4 },
            new HzGeo { T = "arc", P = new[] { 0.0, -6.0, 6.5, 180.0, 180.0 }, W = 0.4 },
            Ar(0, 3.0, 6.5, 200, 140),
            L(0, 9, 0, 13),
        }, new() { An("A", 0, 13, "S") },
           such: "mag kälte expansionsgefäss kaltwasser",
           farbe: "#FF00CC");

        S("druckhaltung", "Speicher", "Druckhaltestation", 20, 24, new()
        {
            RcF(-10, -12, 20, 24, FlaecheGefaess, 0.4),
            Ci(-4, 3, 3.5), Po(true, -5.4, 1.2, -5.4, 4.8, -1.6, 3),
            Ci(4, -4, 3.5), Tx(4, -4, "P", 2.4, true),
        }, new() { An("A", 0, 12, "S"), An("N", -10, -6, "W") },
           such: "druckhaltung nachspeisung entgasung");

        S("verteiler", "Verteilung", "Verteiler / Sammler", 70, 8, new()
        {
            Rc(-35, -4, 70, 8, lw: 0.5),
        }, new()
        {
            An("O1", -26, -4, "N"), An("O2", -13, -4, "N"), An("O3", 0, -4, "N"), An("O4", 13, -4, "N"), An("O5", 26, -4, "N"),
            An("U1", -26, 4, "S"), An("U2", -13, 4, "S"), An("U3", 0, 4, "S"), An("U4", 13, 4, "S"), An("U5", 26, 4, "S"),
            An("E", -35, 0, "W"), An("A", 35, 0, "E"),
        }, "keine", "verteiler sammler balken heizgruppenverteiler");

        S("verteiler_klein", "Verteilung", "Verteiler kurz", 40, 8, new()
        {
            Rc(-20, -4, 40, 8, lw: 0.5),
        }, new()
        {
            An("O1", -13, -4, "N"), An("O2", 0, -4, "N"), An("O3", 13, -4, "N"),
            An("U1", -13, 4, "S"), An("U2", 0, 4, "S"), An("U3", 13, 4, "S"),
            An("E", -20, 0, "W"), An("A", 20, 0, "E"),
        }, "keine", "verteiler kurz sammler");

        // ===================== Wärmeübertrager =====================

        // In den H+K-Plänen ist der Wärmetauscher eine hinterlegte Raute mit den Temperaturen
        // beider Seiten im Feld.
        S("wt_platte", "Wärmeübertrager", "Wärmetauscher", 26, 26, new()
        {
            new HzGeo { T = "poly", P = new[] { 0.0, -13, 13, 0, 0, 13, -13, 0 },
                            Fill = true, Fc = FlaecheApparat, Close = true, W = 0.4 },
            L(0, -9, 0, 9),
            // Temperaturen der Primär-/Sekundärseite links und rechts der Trennlinie
            Tx(-5.6, 0, "$1", 2.0), Tx(5.6, 0, "$2", 2.0),
        }, new()
        {
            An("P1", -6.5, -6.5, "W"), An("P2", -6.5, 6.5, "W"),
            An("S1", 6.5, -6.5, "E"), An("S2", 6.5, 6.5, "E"),
            An("O", 0, -13, "N"), An("U", 0, 13, "S"),
        }, "unten", "wärmetauscher plattentauscher pwt übertrager raute");

        S("wt_rohr", "Wärmeübertrager", "Rohrbündel-Wärmetauscher", 26, 14, new()
        {
            RcF(-13, -7, 26, 14, FlaecheApparat, 0.4),
            Pl(-13, -3.5, 8, -3.5, 8, 0, -8, 0, -8, 3.5, 13, 3.5),
        }, new()
        {
            An("P1", -13, -5, "W"), An("P2", 13, 5, "E"),
            An("S1", -13, 5, "W"), An("S2", 13, -5, "E"),
        }, such: "rohrbündel wärmetauscher gegenstrom");

        // ===================== Pumpen =====================

        // H+K-Legende: Kreis mit waagrechtem Durchmesser; darüber ein ausgefülltes Dreieck,
        // dessen Basis der Durchmesser ist und dessen Spitze den Kreis oben berührt.
        S("pumpe", "Pumpen", "Umwälzpumpe", 8, 8, new()
        {
            Ci(0, 0, 4),
            Po(true, -4, 0, 4, 0, 0, -4),
            L(-4, 0, 4, 0),
        }, Durchgang(4), such: "pumpe umwälzpumpe förderpumpe");

        S("pumpe_geregelt", "Pumpen", "Pumpe, drehzahlgeregelt", 12, 12, new()
        {
            Ci(0, 0, 4),
            Po(true, -4, 0, 4, 0, 0, -4),
            L(-4, 0, 4, 0),
            L(-5.5, 5.5, 5.5, -5.5),
            Po(true, 5.5, -5.5, 3.2, -4.4, 4.4, -3.2),
        }, Durchgang(4), such: "pumpe geregelt frequenz hocheffizienz");

        S("doppelpumpe", "Pumpen", "Doppelpumpe", 16, 8, new()
        {
            Ci(-4.2, 0, 4), Po(true, -8.2, 0, -0.2, 0, -4.2, -4), L(-8.2, 0, -0.2, 0),
            Ci(4.2, 0, 4), Po(true, 0.2, 0, 8.2, 0, 4.2, -4), L(0.2, 0, 8.2, 0),
        }, new() { An("1", 0, -4, "N"), An("2", 0, 4, "S") },
           such: "doppelpumpe zwillingspumpe redundanz");

        // ===================== Absperr- und Sicherheitsarmaturen =====================

        S("absperrventil", "Armaturen", "Absperrventil", 10, 7, new()
        {
            Sanduhr(), L(0, 0, -5.4, 0), L(-5.4, -2.4, -5.4, 2.4),
        }, Durchgang(), such: "ventil absperrung handventil");

        // H+K-Legende: senkrechte Sanduhr mit weissem Kreis in der Achse (die Kugel).
        S("kugelhahn", "Armaturen", "Kugelhahn", 7, 7, new()
        {
            Sanduhr(),
            // Kugel weiss ausgespart, damit die Sanduhrkanten dahinter nicht durchscheinen.
            new HzGeo { T = "circle", P = new[] { 0.0, 0.0, 1.15 }, Fill = true, Fc = "#FFFFFF" },
        }, Durchgang(), such: "kugelhahn hahn absperrhahn absperrung");

        S("schieber", "Armaturen", "Absperrschieber", 11, 7, new()
        {
            Sanduhr(),
            L(0, 0, -6, 0), L(-6, -2.4, -6, 2.4), L(-3.8, -1.6, -3.8, 1.6),
        }, Durchgang(), such: "schieber flachschieber");

        // H+K-Legende: Rechteck mit Diagonale und ausgefülltem Achspunkt — liegend wie die
        // Rückschlagklappe, damit beide Klappen in der Palette gleich ausgerichtet sind.
        S("klappe", "Armaturen", "Absperrklappe", 11, 6, new()
        {
            Rc(-5.5, -3, 11, 6),
            L(-5.5, -3, 5.5, 3),
            Ci(0, 0, 1.5, true),
        }, new() { An("1", -5.5, 0, "W"), An("2", 5.5, 0, "E") },
           such: "klappe absperrklappe drosselklappe");

        // H+K-Legende „Rückschlagsklappe": liegendes Rechteck mit Diagonale und Klappenanschlag.
        S("rueckschlagventil", "Armaturen", "Rückschlagklappe", 11, 6, new()
        {
            Rc(-5.5, -3, 11, 6),
            L(-5.5, 3, 5.5, -3),
            Po(true, -5.5, -3, -3.4, -3, -5.5, -1.2),
        }, new() { An("1", -5.5, 0, "W"), An("2", 5.5, 0, "E") },
           such: "rückschlag rückschlagsklappe rückflussverhinderer");

        // H+K-Legende: nahezu quadratischer Kasten mit gestrichelter Mittellinie (Siebeinsatz).
        S("schmutzfaenger", "Armaturen", "Schmutzfänger", 8, 8, new()
        {
            Rc(-4, -4, 8, 8),
            new HzGeo { T = "line", P = new[] { 0.0, -4.0, 0.0, 4.0 }, Dash = true },
        }, new() { An("1", 0, -4, "N"), An("2", 0, 4, "S") },
           such: "filter schmutzfänger sieb y-filter");

        // H+K-Legende: Eckventil — waagrechter Austritt (Dreieck mit Spitze zur Achse), senkrechter
        // Eintritt von unten, ausgefüllter Achspunkt, darüber die Feder mit Endpunkt.
        S("sicherheitsventil", "Armaturen", "Sicherheitsventil", 13, 15, new()
        {
            Po(false, -3.4, -2.6, -3.4, 2.6, 0, 0),      // Austritt waagrecht
            Po(false, -2.6, 4.6, 2.6, 4.6, 0, 0),        // Eintritt von unten
            Ci(0, 0, 1.3, true),
            Pl(0, 0, 0, -2.4, 2.2, -3.4, -1.4, -4.8, 2.2, -6.2, 0, -7.2),
            Ci(0, -8.0, 0.7, true),
            L(-3.4, 0, -6.4, 0), L(-6.4, 0, -6.4, 4.6),  // Abblasleitung
        }, new() { An("E", 0, 4.6, "S"), An("A", -6.4, 4.6, "S") },
           such: "sicherheitsventil sv überdruck expansion");

        S("entlueftung", "Armaturen", "Entlüfter (automatisch)", 7, 11, new()
        {
            Ci(0, -3.5, 3),
            L(0, -0.5, 0, 4),
            Po(true, 0, -9.5, -1.6, -7, 1.6, -7),
        }, new() { An("A", 0, 4, "S") }, such: "entlüfter entlüftung luftabscheider");

        // H+K-Legende „Entleerhahnen": waagrechte Sanduhr mit Anschlagbügel am Auslauf.
        S("entleerung", "Armaturen", "Entleerhahn", 10, 6, new()
        {
            Po(false, -2.8, -2.6, -2.8, 2.6, 2.8, -2.6, 2.8, 2.6),
            L(-5.2, -2.6, -5.2, 2.6), L(-5.2, 0, -2.8, 0),
            L(-5.2, -2.6, -4.0, -2.6), L(-5.2, 2.6, -4.0, 2.6),
        }, new() { An("E", 2.8, 0, "E") }, such: "entleerung entleerhahn ablass kfe");

        // H+K-Legende: senkrechte Sanduhr, oberer Kegel ausgefüllt (voreinstellbar).
        S("strangregulierventil", "Armaturen", "Strangregulierventil", 7, 7, new()
        {
            DreieckOben(true), DreieckUnten(false),
        }, Durchgang(), such: "strangregulierventil abgleich einregulierung stad");

        S("ueberstroemventil", "Armaturen", "Überströmventil", 12, 7, new()
        {
            Sanduhr(), Ci(0, 0, 1.3, true),
            Pl(-3.4, 0, -4.6, 0, -5.8, -1.6, -7.4, 1.6, -8.6, 0, -9.8, 0),
        }, Durchgang(), such: "überströmventil bypass differenzdruck");

        // H+K-Legende: Sanduhr mit Membrandose und Impulsleitung seitlich.
        S("dd_regler", "Armaturen", "Differenzdruckregler", 14, 7, new()
        {
            Sanduhr(), Ci(0, 0, 1.3, true),
            Rc(-7.2, -2.2, 2.6, 4.4),
            L(-3.4, 0, -7.2, 0), L(-9.6, 0, -7.2, 0), L(-9.6, -2.6, -9.6, 2.6),
        }, Durchgang(), such: "differenzdruckregler dpr druckdifferenz");

        S("druckreduzierventil", "Armaturen", "Druckreduzierventil", 13, 7, new()
        {
            Sanduhr(), Ci(0, 0, 1.3, true),
            Rc(-9.4, -2.4, 4.8, 4.8), L(-4.6, 0, -1.2, 0),
            Po(true, -8.4, -0.6, -5.6, -0.6, -7, 1.4),
        }, Durchgang(), such: "druckreduzierventil dm druckminderer");

        // ===================== Regelarmaturen =====================

        // H+K-Legende „2-Weg-Ventil": senkrechte Sanduhr, grosser ausgefüllter Achspunkt,
        // Motorkästchen links am Steg.
        S("regelventil_2weg", "Regelarmaturen", "2-Weg-Ventil (Regelventil)", 17, 7, new(
            new[] { Sanduhr(), Ci(0, 0, 1.7, true) }.Concat(MotorLinks()))
        , Durchgang(), such: "regelventil motorventil 2-weg stellventil belimo");

        // H+K-Legende „3-Weg-Ventil": oberer Weg und dritter Abgang (rechts) ausgefüllt.
        S("mischer_3weg", "Regelarmaturen", "3-Weg-Ventil (Mischer)", 17, 7, new(
            new[] { DreieckOben(true), DreieckUnten(false), DreieckRechts(true), Ci(0, 0, 1.7, true) }
            .Concat(MotorLinks()))
        , new() { An("A", 0, -3.4, "N"), An("B", 0, 3.4, "S"), An("AB", 4.6, 0, "E") },
           such: "mischer 3-weg mischventil dreiwegmischer");

        S("mischer_4weg", "Regelarmaturen", "4-Weg-Mischventil", 17, 7, new(
            new[] { DreieckOben(true), DreieckUnten(false), DreieckRechts(true),
                    Po(false, -4.6, -2.7, -4.6, 2.7, 0, 0), Ci(0, 0, 1.7, true) }
            .Concat(MotorLinks(-11.8)))
        , new() { An("A", 0, -3.4, "N"), An("B", 0, 3.4, "S"), An("AB", 4.6, 0, "E"), An("C", -4.6, 0, "W") },
           such: "mischer 4-weg vierwegmischer");

        S("umschaltventil", "Regelarmaturen", "3-Weg-Umschaltventil", 17, 7, new(
            new[] { DreieckOben(false), DreieckUnten(false), DreieckRechts(true), Ci(0, 0, 1.7, true) }
            .Concat(MotorLinks()))
        , new() { An("A", 0, -3.4, "N"), An("B", 0, 3.4, "S"), An("AB", 4.6, 0, "E") },
           such: "umschaltventil zonenventil dreiwegeumschaltventil");

        // H+K-Legende „Motor": Quadrat mit grossem Kreis und „M".
        S("motor", "Regelarmaturen", "Stellantrieb / Motor", 5.2, 5.2, new()
        {
            Rc(-2.6, -2.6, 5.2, 5.2), Ci(0, 0, 2.3), Tx(0, 0, "M", 2.4),
        }, new() { An("1", -2.6, 0, "W"), An("2", 2.6, 0, "E") },
           "keine", "motor stellantrieb antrieb");

        S("zonenventil", "Regelarmaturen", "Zonenventil", 13, 6, new()
        {
            Sanduhr(2.8, 2.8), Ci(0, 0, 1.3, true),
            Rc(-8.4, -2.2, 4.4, 4.4), L(-4, 0, -1.2, 0),
        }, Durchgang(2.8), such: "zonenventil stellantrieb thermoantrieb");

        S("thermostatventil", "Regelarmaturen", "Thermostatventil", 13, 7, new()
        {
            Sanduhr(), Ci(0, 0, 1.3, true),
            Rc(-9.4, -2.4, 4.8, 4.8), L(-4.6, 0, -1.2, 0),
            L(-9.4, -0.8, -4.6, -0.8), L(-9.4, 0.8, -4.6, 0.8),
        }, Durchgang(), such: "thermostatventil hkv heizkörperventil trv");

        S("rl_verschraubung", "Regelarmaturen", "Rücklaufverschraubung", 8, 7, new()
        {
            Sanduhr(), L(0, 0, 3.6, -2.8),
        }, Durchgang(), such: "rücklaufverschraubung rlv voreinstellung");

        // ===================== Verbraucher =====================

        S("heizkoerper", "Verbraucher", "Heizkörper", 20, 11, new()
        {
            RcF(-10, -5.5, 20, 11, FlaecheApparat, 0.4),
            L(-5, -5.5, -5, 5.5), L(0, -5.5, 0, 5.5), L(5, -5.5, 5, 5.5),
        }, new()
        {
            An("VL", -6, 5.5, "S"), An("RL", 6, 5.5, "S"),
            An("L", -10, 0, "W"), An("R", 10, 0, "E"),
            An("O", 0, -5.5, "N"),
        }, such: "heizkörper radiator konvektor");

        S("fussbodenheizung", "Verbraucher", "Fussbodenheizung", 28, 18, new()
        {
            RcF(-14, -9, 28, 18, FlaecheApparat, 0.4),
            Pl(-11, 6, -11, -6, -6.5, -6, -6.5, 6, -2, 6, -2, -6, 2.5, -6, 2.5, 6, 7, 6, 7, -6, 11, -6, 11, 6),
        }, new()
        {
            An("VL", -14, -4, "W"), An("RL", -14, 4, "W"),
            An("O", 0, -9, "N"), An("U", 0, 9, "S"),
        }, such: "fussbodenheizung fbh flächenheizung bodenheizung");

        S("fbh_verteiler", "Verbraucher", "FBH-Verteiler", 30, 22, new()
        {
            Rc(-15, -7, 30, 5), Rc(-15, 2, 30, 5),
            L(-9, 7, -9, 11), L(-3, 7, -3, 11), L(3, 7, 3, 11), L(9, 7, 9, 11),
            L(-9, -11, -9, -7), L(-3, -11, -3, -7), L(3, -11, 3, -7), L(9, -11, 9, -7),
        }, new()
        {
            An("VL", -15, -4.5, "W"), An("RL", -15, 4.5, "W"),
            An("K1", -9, 11, "S"), An("K2", -3, 11, "S"), An("K3", 3, 11, "S"), An("K4", 9, 11, "S"),
        }, such: "fbh verteiler heizkreisverteiler kreise");

        S("lufterhitzer", "Verbraucher", "Lufterhitzer / Register", 26, 16, new()
        {
            RcF(-10, -8, 20, 16, FlaecheApparat, 0.4),
            Pl(-10, -4, 6, -4, 6, 0, -6, 0, -6, 4, 10, 4),
            Po(true, -13, -8, -13, -5.2, -10.4, -6.6),
            Po(true, 13, 6.6, 10.4, 5.2, 10.4, 8),
        }, new()
        {
            An("VL", 0, -8, "N"), An("RL", 0, 8, "S"),
            An("L", -10, -6, "W"), An("R", 10, 6, "E"),
        }, such: "lufterhitzer register erhitzer luftheizer monoblock");

        // Heizgruppe wie in den H+K-Plänen: hinterlegtes Feld, oben Bezeichnung/Temperaturen,
        // unten Leistung/Massenstrom (Bezeichnung → oberes Feld, Zusatz → unteres).
        S("heizgruppe", "Verbraucher", "Heizgruppe (Feld)", 34, 18, new()
        {
            RcF(-17, -9, 34, 18, FlaecheApparat, 0.4),
            L(-17, 0, 17, 0),
            Tx(0, -4.5, "$1", 2.8), Tx(0, 4.5, "$2", 2.8),
        }, new()
        {
            An("VL", 0, -9, "N"), An("RL", 0, 9, "S"),
            An("L", -17, 0, "W"), An("R", 17, 0, "E"),
        }, "keine", "heizgruppe gruppe verbraucher feld abnehmer");

        S("verbraucher", "Verbraucher", "Verbraucher allgemein", 18, 14, new()
        {
            Rc(-9, -7, 18, 14, lw: 0.5),
            L(-9, -7, 9, 7), L(9, -7, -9, 7),
        }, new()
        {
            An("VL", -9, -3.5, "W"), An("RL", -9, 3.5, "W"),
            An("O", 0, -7, "N"), An("U", 0, 7, "S"),
            An("R1", 9, -3.5, "E"), An("R2", 9, 3.5, "E"),
        }, such: "verbraucher abnehmer gruppe allgemein");
        // ===================== Messen / Regeln =====================

        // H+K-Legende „Temperaturfühler": kleiner Kreis am Fühlerstrich, Bezeichnung daneben.
        S("fuehler", "Messen / Regeln", "Temperaturfühler", 8, 3, new()
        {
            Ci(-2.6, 0, 1.2), L(-1.4, 0, 4, 0),
        }, new() { An("M", 4, 0, "E") }, "links", "fühler temperatur tf sensor vorlauffühler");

        S("aussenfuehler", "Messen / Regeln", "Aussenfühler", 8, 3, new()
        {
            Ci(-2.6, 0, 1.2), L(-1.4, 0, 4, 0),
        }, new() { An("M", 4, 0, "E") }, "links", "aussenfühler af witterung");

        // H+K-Legende: Zeigerinstrument mit Kürzel aussen.
        S("thermometer", "Messen / Regeln", "Thermometer", 11, 10, new(Instrument("T")),
           new() { An("M", 0, 3.6, "S"), An("L", -3.6, 0, "W") }, "keine", "thermometer temperaturanzeige");

        S("manometer", "Messen / Regeln", "Manometer", 11, 10, new(Instrument("P")),
           new() { An("M", 0, 3.6, "S"), An("L", -3.6, 0, "W") }, "keine", "manometer druck druckanzeige");

        // H+K-Legende: Durchflussgeber (Quadrat mit Kreuz) + Rechenwerk (stehendes Rechteck,
        // durch die Diagonale zur Hälfte ausgefüllt).
        S("waermezaehler", "Messen / Regeln", "Wärmezähler", 13, 9, new()
        {
            Rc(-6.4, -2.6, 5.2, 5.2), L(-6.4, -2.6, -1.2, 2.6), L(-6.4, 2.6, -1.2, -2.6),
            L(-1.2, 0, 1.4, 0),
            Rc(1.4, -4.4, 5, 8.8),
            Po(true, 1.4, -4.4, 6.4, -4.4, 6.4, 4.4),
        }, new() { An("1", -6.4, 0, "W"), An("2", 6.4, 0, "E") },
           "unten", "wärmezähler wmz zähler energiezähler");

        S("wasserzaehler", "Messen / Regeln", "Wasserzähler", 10, 8, new()
        {
            Rc(-5, -3.6, 10, 7.2), Ci(0, 0, 2.4), L(-2.4, 0, 2.4, 0), L(0, -2.4, 0, 2.4),
        }, new() { An("1", -5, 0, "W"), An("2", 5, 0, "E") },
           "rechts", "wasserzähler zähler wz");

        S("stroemungswaechter", "Messen / Regeln", "Strömungswächter", 10, 12, new()
        {
            Ci(0, 0, 5), Tx(0, 0, "$FS", 3),
            L(0, 5, 0, 8),
        }, new() { An("M", 0, 8, "S") }, "rechts", "strömungswächter durchfluss fs");

        S("regler", "Messen / Regeln", "Regler / DDC", 18, 11, new()
        {
            Rc(-9, -5.5, 18, 11),
            Tx(0, 0, "$MSR", 3.2, true),
        }, new()
        {
            An("1", -9, 0, "W"), An("2", 9, 0, "E"), An("3", 0, 5.5, "S"), An("4", 0, -5.5, "N"),
        }, "keine", "regler ddc steuerung msr regelung leitsystem");

        return l;
    }

    /// <summary>Einbaulage nach der H+K-Legende: Durchgangsarmaturen und Pumpen stehen senkrecht, Klappen und Zähler liegen, alles
    /// andere (Apparate, Eckventile, Entlüfter, Instrumente, Antriebe) steht fest aufrecht.</summary>
    static HzLage LageVon(string key) => key switch
    {
        "pumpe" or "pumpe_geregelt" or "doppelpumpe" or "absperrventil" or "kugelhahn" or "schieber" or "schmutzfaenger"
            or "strangregulierventil" or "ueberstroemventil" or "dd_regler" or "druckreduzierventil" or "regelventil_2weg"
            or "mischer_3weg" or "mischer_4weg" or "umschaltventil" or "zonenventil" or "thermostatventil" or "rl_verschraubung" => HzLage.Senkrecht,
        "klappe" or "rueckschlagventil" or "waermezaehler" or "wasserzaehler" => HzLage.Waagrecht,
        _ => HzLage.Fest,
    };

    // ---------------------------------------------------------------- Zuordnung Klasse / Art → Symbol

    /// <summary>Zuordnung Klasse (und <c>art</c>) → HKAP-Key, an dieser einen Stelle; die erste passende Zeile gilt. Klassen, die im Katalog
    /// (noch) fehlen, greifen nicht – die Zeilen bleiben stehen. Die Zeilen der Klassen aus Modul 5 Teil A-D (Mischventil, Ausdehnungsgefaess,
    /// Waermetauscher, Fuehler, Kaelteerzeuger und die neuen Ventil-Arten) sind vorbereitet. ANNAHMEN Prüfliste: Fuehler «druck» zeigt das
    /// Manometer (die HKAP kennt keinen Druckfühler); Kaelteerzeuger zeigt das Wärmepumpensymbol (die HKAP hat kein eigenes Symbol, die
    /// Kältemaschine ist ein Wärmepumpenkreis) in der Kälte-Hinterlegung.</summary>
    public static readonly IReadOnlyList<HzZuordnung> Zuordnung =
    [
        // Wärmeerzeuger je art
        new("Waermeerzeuger", "waermepumpe", "waermepumpe"),
        new("Waermeerzeuger", "gas", "kessel"),
        new("Waermeerzeuger", "oel", "kessel"),
        new("Waermeerzeuger", "holz", "kessel_holz"),
        new("Waermeerzeuger", "fernwaerme", "fernwaerme"),
        new("Kaelteerzeuger", null, "waermepumpe"),
        // Speicher, Verteilung, Verbraucher
        new("Speicher", null, "speicher"),
        new("Verteiler", null, "verteiler"),
        new("Heizkoerper", null, "heizkoerper"),
        new("Fussbodenheizkreis", null, "fussbodenheizung"),
        // Pumpe, Zähler
        new("Pumpe", null, "pumpe"),
        new("Waermezaehler", null, "waermezaehler"),
        // Ventil je art
        new("Ventil", "absperrventil", "absperrventil"),
        new("Ventil", "regulierventil", "regelventil_2weg"),
        new("Ventil", "rueckflussverhinderer", "rueckschlagventil"),
        new("Ventil", "druckreduzierventil", "druckreduzierventil"),
        new("Ventil", "mischventil", "mischer_3weg"),
        // neue Ventil-Arten (Modul 5 A-D)
        new("Ventil", "strangregulierventil", "strangregulierventil"),
        new("Ventil", "sicherheitsventil", "sicherheitsventil"),
        new("Ventil", "ueberstroemventil", "ueberstroemventil"),
        new("Ventil", "thermostatventil", "thermostatventil"),
        new("Ventil", "zonenventil", "zonenventil"),
        new("Ventil", "entlueftung", "entlueftung"),
        new("Ventil", "entleerung", "entleerung"),
        new("Ventil", "schmutzfaenger", "schmutzfaenger"),
        // neue Klassen (Modul 5 A-D)
        new("Mischventil", "3weg", "mischer_3weg"),
        new("Mischventil", "4weg", "mischer_4weg"),
        new("Mischventil", "umschalt", "umschaltventil"),
        new("Ausdehnungsgefaess", null, "ausdehnungsgefaess"),
        new("Waermetauscher", "platte", "wt_platte"),
        new("Waermetauscher", "rohr", "wt_rohr"),
        new("Fuehler", "temperatur", "fuehler"),
        new("Fuehler", "druck", "manometer"),
        new("Fuehler", "thermometer", "thermometer"),
        new("Fuehler", "manometer", "manometer"),
        new("Fuehler", "stroemung", "stroemungswaechter"),
    ];

    /// <summary>Symbole, die in der Kälte (Medium KAE-…) durch eine eigene Variante ersetzt werden (HKAP: «im Kältekreis am Element umstellen»).</summary>
    public static readonly IReadOnlyDictionary<string, string> KaelteVariante = new Dictionary<string, string>
    {
        ["ausdehnungsgefaess"] = "druckhaltung_kaelte",
    };

    /// <summary>Symbol der Klasse und Art eines Elements (ohne Kälte-Variante) oder null. Eine Klasse, die der Katalog nicht kennt, greift nicht.</summary>
    public static HzSymbol? SymbolVon(Katalog k, Element e)
    {
        var art = e.Text("art");
        foreach (var z in Zuordnung)
        {
            if (!k.IstArt(e, z.Klasse)) continue;
            if (z.Art is not null && !string.Equals(z.Art, art, StringComparison.OrdinalIgnoreCase)) continue;
            return Bibliothek.TryGetValue(z.Key, out var s) ? s : null;
        }
        return null;
    }

    /// <summary>Symbol des Elements samt Kälte-Kennzeichen; null ohne Heizungs- oder Kältesymbol.</summary>
    public static (HzSymbol Symbol, bool Kaelte)? Wahl(Projekt p, Element e)
    {
        if (SymbolVon(p.Katalog, e) is not { } s) return null;
        bool kaelte = IstKaelte(p, e);
        if (kaelte && KaelteVariante.TryGetValue(s.Key, out var v) && Bibliothek.TryGetValue(v, out var sv)) s = sv;
        return (s, kaelte);
    }

    /// <summary>Gehört das Element zur Kälte? Medium des Systems KAE-…; ohne System, wenn alle Anschlüsse mit festem Medium KAE-… führen;
    /// ein Kälteerzeuger immer.</summary>
    public static bool IstKaelte(Projekt p, Element e)
    {
        if (p.Katalog.IstArt(e, "Kaelteerzeuger")) return true;
        var medium = e.System is null ? null : Netz.SystemMedium(p.Modell, e.System);
        if (!string.IsNullOrEmpty(medium)) return IstKaeltemedium(medium);
        var fest = e.Anschluesse.Select(a => Netz.Medium(p.Modell, e, a)).Where(m => m != "*").ToList();
        return fest.Count > 0 && fest.All(IstKaeltemedium);
    }

    static bool IstKaeltemedium(string medium) => KaelteAlias.Medium(medium).StartsWith("KAE-", StringComparison.OrdinalIgnoreCase);

    // ---------------------------------------------------------------- Zeichnen

    /// <summary>Lage eines Symbols (Primitive und Anschlüsse): gedreht, wenn eine Senkrecht-Armatur an einer waagrechten oder eine
    /// Waagrecht-Armatur an einer senkrechten Leitung sitzt.</summary>
    public static bool Gedreht(HzSymbol s, bool leitungSenkrecht) => s.Lage switch
    {
        HzLage.Senkrecht => !leitungSenkrecht,
        HzLage.Waagrecht => leitungSenkrecht,
        _ => false,
    };

    static (double X, double Y) Dreh((double X, double Y) q, bool gedreht) => gedreht ? (-q.Y, q.X) : q;   // 90° gegen den Uhrzeigersinn

    static double Rund(double v) => Math.Round(v, 4) + 0.0;

    /// <summary>Primitive des Symbols um den Ursprung (Papier-mm, y nach oben), umgerechnet wie in der Kopfzeile beschrieben.
    /// <paramref name="kaelte"/> ersetzt die Hinterlegung #CCFFE0 durch #CCE0FF; <paramref name="gedreht"/> dreht um 90° gegen den
    /// Uhrzeigersinn (Texte bleiben waagrecht); <paramref name="texte"/> liefert die Texte der Platzhalter «$1», «$2», «$FS», «$MSR»
    /// (Schlüssel ohne «$»).</summary>
    public static List<Primitiv> Primitive(HzSymbol s, bool kaelte = false, bool gedreht = false, IReadOnlyDictionary<string, string>? texte = null)
    {
        string? linie = s.StandardFarbe.Length > 0 ? s.StandardFarbe : null;
        bool inLeitung = s.Lage != HzLage.Fest;
        double[] Pkt(double x, double y) { var q = Dreh((x * Skala, -y * Skala), gedreht); return [Rund(q.X), Rund(q.Y)]; }
        string Stift(HzGeo g) => g.W > 0 ? Stifte.Mittel : Stifte.Duenn;
        string? Fuellung(HzGeo g)
        {
            if (g.Fill)
            {
                if (g.Fc.Length == 0) return linie ?? "schnitt";
                if (g.Fc.Equals("#FFFFFF", StringComparison.OrdinalIgnoreCase)) return "papier";
                return kaelte && g.Fc.Equals(FlaecheApparat, StringComparison.OrdinalIgnoreCase) ? FlaecheApparatKaelte : g.Fc;
            }
            return inLeitung ? "papier" : null;
        }
        Stil Flaeche(HzGeo g) => new() { Stift = Stift(g), Farbe = linie, Fuellung = Fuellung(g) };

        var aus = new List<Primitiv>();
        foreach (var g in s.Geo)
        {
            var p = g.P;
            switch (g.T)
            {
                case "line":
                    aus.Add(new Primitiv
                    {
                        Art = "linie", Punkte = [Pkt(p[0], p[1]), Pkt(p[2], p[3])],
                        Stil = new Stil { Stift = Stift(g), Linie = g.Dash ? "strich" : "voll", Farbe = linie },
                    });
                    break;
                case "rect":
                    aus.Add(new Primitiv
                    {
                        Art = "polygon", Stil = Flaeche(g),
                        Punkte = [Pkt(p[0], p[1]), Pkt(p[0] + p[2], p[1]), Pkt(p[0] + p[2], p[1] + p[3]), Pkt(p[0], p[1] + p[3])],
                    });
                    break;
                case "circle":
                    aus.Add(new Primitiv { Art = "kreis", Punkte = [Pkt(p[0], p[1])], Radius = Rund(p[2] * Skala), Stil = Flaeche(g) });
                    break;
                case "arc":
                {
                    double von = ((-(p[3] + p[4]) + (gedreht ? 90 : 0)) % 360 + 360) % 360;
                    aus.Add(new Primitiv
                    {
                        Art = "bogen", Punkte = [Pkt(p[0], p[1])], Radius = Rund(p[2] * Skala), Von = von, Bis = von + p[4],
                        Stil = new Stil { Stift = Stift(g), Farbe = linie },
                    });
                    break;
                }
                case "poly":
                {
                    var pts = new List<double[]>();
                    for (int i = 0; i + 1 < p.Length; i += 2) pts.Add(Pkt(p[i], p[i + 1]));
                    aus.Add(g.Close
                        ? new Primitiv { Art = "polygon", Punkte = pts, Stil = Flaeche(g) }
                        : new Primitiv { Art = "polylinie", Punkte = pts, Stil = new Stil { Stift = Stift(g), Farbe = linie } });
                    break;
                }
                case "text":
                {
                    string text = g.S;
                    if (text.StartsWith('$'))
                    {
                        var name = text[1..];
                        text = texte is not null && texte.TryGetValue(name, out var t) ? t : name is "1" or "2" ? "" : name;
                    }
                    if (text.Length == 0) break;
                    double groesse = Rund(g.Size * Skala);
                    var m = Dreh((p[0] * Skala, -p[1] * Skala), gedreht);   // Mitte des Textes; die Grundlinie liegt eine halbe Kapitalhöhe tiefer
                    aus.Add(new Primitiv
                    {
                        Art = "text", Punkte = [[Rund(m.X), Rund(m.Y - groesse * 0.35)]], Text = text, Groesse = groesse, Ausrichtung = "mitte",
                        Fett = g.Bold ? true : null, Stil = new Stil { Stift = null, Farbe = linie },
                    });
                    break;
                }
            }
        }
        return aus;
    }

    /// <summary>Anschlusspunkte des Symbols (Namen wie in der HKAP) in Papier-mm um den Ursprung, y nach oben; die Richtung ist die der
    /// Leitung auf dem Bildschirm (N = nach oben) und dreht mit.</summary>
    public static List<HzPunkt> Anschluesse(HzSymbol s, bool gedreht = false) =>
        s.Anschluesse.Select(a =>
        {
            var q = Dreh((a.X * Skala, -a.Y * Skala), gedreht);
            var dir = gedreht ? a.Dir switch { "N" => "W", "W" => "S", "S" => "E", "E" => "N", var d => d } : a.Dir;
            return new HzPunkt(a.Id, Rund(q.X), Rund(q.Y), dir);
        }).ToList();
}
