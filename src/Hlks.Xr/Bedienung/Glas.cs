// Quest App V1a: Erscheinungsbild der Apps im Stil von visionOS (Christian 24.10.2026: «richte dich bei der Darstellung der apps und funktionen nach der vision» –
// gemeint ist die Apple Vision Pro). Quelle: Apple Human Interface Guidelines, visionOS-Abschnitte der Seiten «Materials», «Windows», «Buttons», «Tab bars»,
// «Ornaments», «Eyes», «Spatial layout» (abgerufen 24.10.2026). Rein, ohne StereoKit: Farben, Masse, Lage der Fensterteile, Verankerung im Raum, Lage neben dem
// Tisch, Hinweiszeile, Inhalt (Knöpfe, Zeilen) und die kurzen Bewegungen. Gezeichnet wird in src/Hlks.Quest/GlasApps.cs mit dem Shader Assets/glas.hlsl.
//
// Was aus den HIG übernommen ist (und wie):
//   - Glas: «windows generally use … glass», «Prefer translucency to opaque colors» → durchscheinende Fläche (seit den Bildern 02 im Pastellton der App, AppFarben), stark gerundete
//     Ecken, feiner Glanz an der Oberkante, weicher Schatten. Echte Weichzeichnung des Hintergrunds gibt es nicht (StereoKit 0.3.11 hat keinen Zugriff auf das Bild
//     hinter der Fläche; ein eigener Durchgang kostete auf der Quest mindestens die doppelte Füllrate) – angenähert durch die Tönung (Stellschraube anzeige.glas_deckkraft).
//     «Glass is an adaptive material» → Fassung und Deckkraft richten sich nach der Umgebung (AppFarben.Satz, Stellschraube anzeige.glas_helligkeit); die Fläche verdeckt
//     Weltobjekte dahinter (die App schreibt die Tiefe des Glases; Befund Prüfbild 24.10.2026: der Stift eines Markers schien durch das Fenster).
//   - Schrift: «label, secondaryLabel, tertiaryLabel» → drei Stufen (GlasSatz: Text, TextSekundaer, TextTertiaer; dunkel auf hellem Pastell, hell auf der dunklen Fassung).
//   - Knöpfe: «icon-only button uses a circle shape … text … capsule … roundedRectangle … in a vertical stack»; «thin material as the button’s background» → Grund
//     Duenn; «white background fill and black text … reserves … toggled state» → Ein; «hit region … in visionOS, 60x60 pt»; «centers … at least 60 pts apart»; jeder
//     Knopf mit Ton (die App: Rueckmeldung.Kachel).
//   - Hover: Apple nutzt den Blick; die Quest 3 hat keine Augenverfolgung → das Ziel hebt sich bei Annäherung des Fingers bzw. des Strahls leicht an und hellt auf.
//   - Fenster: «window bar … close button» → unter dem Fenster ein Kapsel-Griff zum Verschieben, links daneben ein runder Schliessen-Knopf; «tab bar is always
//     vertical … leading side … symbol always visible … reveals tab labels» → Register links, Beschriftung klappt bei Annäherung auf; «ornament … parallel … slightly in
//     front» → Werkzeugleiste an der Unterkante, leicht davor.
//   - Tiefe sparsam: «when a sheet appears over a window, the window recedes» → GlasStil.ZurueckM für ein Blatt über dem Fenster. Die Karte einer Prüfaufgabe ist
//     seit dem Befund 24.10.2026 ein kleines Ornament über dem Fenster (deckt die App nicht) – das Fenster weicht dann nicht zurück.
//   - Lage: «Avoid anchoring content to the wearer’s head», «Center important content within the field of view» → im Raum verankert (folgt dem Kopf nicht), erscheint
//     beim Holen seitlich nach apps.seitlich_grad (Annahme V1a 30°; P5a: 50°), kommt nur nach, wenn man weiter als NachholenM weggeht.
//   - Massstab: «1 pt» ist in visionOS ein Winkel; ein Fenster mit 1280 × 720 pt erscheint in rund 2 m Abstand etwa 3 m breit → 1 pt ≈ 1.2 mm je Meter Abstand
//     (eigene Rechnung, Annahme). Für das Fenster im Griffbereich (0.5 m) und Fingerziele × 1.5 (Annahme V1a): PtM = 0.9 mm.
// Nicht verwendet: Apple-Logos, SF-Schriften, SF Symbols (Lizenz nur für Apple-Plattformen); die Symbole sind die von KICAD (GlasSymbole, Texturen).

namespace Hlks.Xr.Bedienung;

/// <summary>Farbe (sRGB 0 … 1, Alpha 0 … 1).</summary>
public readonly record struct GlasFarbe(double R, double G, double B, double A)
{
    public GlasFarbe MitAlpha(double a) => this with { A = a };
    /// <summary>Aufhellen um anteil (0 … 1) Richtung Weiss.</summary>
    public GlasFarbe Heller(double anteil) => new(R + (1 - R) * anteil, G + (1 - G) * anteil, B + (1 - B) * anteil, A);
}

/// <summary>Farben, Masse und Zeiten an einer Stelle (Annahmen V1a nach den HIG, siehe Dateikopf).</summary>
public static class GlasStil
{
    // ---------------------------------------------------------------------------------------------------- Massstab
    /// <summary>1 pt in mm je Meter Abstand (aus «1280 × 720 pt … 2 m … etwa 3 m breit», eigene Rechnung).</summary>
    public const double MmJePtJeMeter = 1.2;
    /// <summary>Bezugsabstand des Fensters (m, Griffbereich) und Vergrösserung für Fingerziele (Annahme V1a).</summary>
    public const double BezugAbstandM = 0.5, FingerFaktor = 1.5;
    /// <summary>Ein pt in m beim Bezugsabstand mit Fingerfaktor (0.9 mm).</summary>
    public const double PtM = MmJePtJeMeter / 1000 * BezugAbstandM * FingerFaktor;
    /// <summary>Ein pt der Schrift in m (ohne Fingerfaktor, 0.6 mm).</summary>
    public const double SchriftPtM = MmJePtJeMeter / 1000 * BezugAbstandM;

    // ---------------------------------------------------------------------------------------------------- Masse in pt (Annahmen V1a nach den HIG)
    public const double FensterBreitePt = 360, FensterHoehePt = 344, EckenPt = 34, RandPt = 20, KopfPt = 30, FussPt = 30;
    /// <summary>Zeilen und Knöpfe: Höhe 52 pt («Large»), Mitten 60 pt auseinander (HIG «at least 60 pts apart»).</summary>
    public const double ZeilePt = 52, ZeilenMittePt = 60;
    /// <summary>Register: Kreise 56 pt, Mitten 64 pt, links vom Fenster mit 12 pt Lücke; aufgeklappt 150 pt breit.</summary>
    public const double RegisterKreisPt = 56, RegisterMittePt = 64, RegisterLueckePt = 12, RegisterInnenPt = 8, RegisterAufPt = 150;
    /// <summary>Fensterleiste: Kapsel-Griff 120 × 20 pt, 14 pt unter dem Fenster; Schliessen-Kreis 36 pt links daneben.</summary>
    public const double GriffBreitePt = 120, GriffHoehePt = 20, GriffAbstandPt = 14, SchliessenPt = 36;
    /// <summary>
    /// Werkzeugleiste (Ornament) an der Unterkante: Kreise 52 pt, 12 mm vor dem Fenster, ragt 10 pt über die Unterkante (deckt die Fusszeile nicht; Befund Prüfbild
    /// 24.10.2026: mittig auf der Kante verdeckte sie den Hinweis). Griff und Schliessen stehen darunter.
    /// </summary>
    public const double OrnamentKreisPt = 52, OrnamentInnenPt = 8, OrnamentUeberlappPt = 10;
    public const double OrnamentVorM = 0.012;
    /// <summary>Schrift (pt, ohne Fingerfaktor): Titel 22, Text 17, klein 15 (HIG «Typography», visionOS-Stufen als Annahme).</summary>
    public const double TitelPt = 22, TextPt = 17, KleinPt = 15;

    // ---------------------------------------------------------------------------------------------------- Farben (neutral, nicht farbig)
    /// <summary>Glas ohne Katalog der Farben: neutral hellgrau (Rückfall; sonst der Pastellton der App, AppFarben; Deckkraft aus anzeige.glas_deckkraft).</summary>
    public static readonly GlasFarbe Glas = new(0.56, 0.58, 0.61, 0.75);
    /// <summary>Feiner Glanz an der Oberkante (Deckkraft).</summary>
    public const double Glanz = 0.35;
    /// <summary>Weicher Schatten unter dem Fenster.</summary>
    public static readonly GlasFarbe Schatten = new(0, 0, 0, 0.28);
    /// <summary>«thin material» für Knöpfe auf dem Glas, beim Annähern heller.</summary>
    public static readonly GlasFarbe Duenn = new(1, 1, 1, 0.14), DuennNah = new(1, 1, 1, 0.26), DuennGedrueckt = new(1, 1, 1, 0.38);
    /// <summary>Eingeschaltet: weisser Grund, dunkle Schrift (HIG: nur für «toggled»).</summary>
    public static readonly GlasFarbe Ein = new(0.97, 0.97, 0.98, 0.96), TextAufEin = new(0.10, 0.10, 0.12, 1);
    /// <summary>Schrift in drei Stufen (label, secondaryLabel, tertiaryLabel).</summary>
    public static readonly GlasFarbe TextPrimaer = new(1, 1, 1, 1), TextSekundaer = new(1, 1, 1, 0.62), TextTertiaer = new(1, 1, 1, 0.36);
    /// <summary>Ampel (nur als kleiner Punkt): verbunden, ohne PC, Fehler.</summary>
    public static readonly GlasFarbe Gruen = new(0.30, 0.85, 0.45, 1), Orange = new(1.0, 0.70, 0.25, 1), Rot = new(1.0, 0.36, 0.33, 1);

    // ---------------------------------------------------------------------------------------------------- Umgebung (für die Tönung, AppFarben)
    // Apple HIG «Materials», visionOS (abgerufen 24.10.2026): «Glass is an adaptive material that limits the range of background color information so a window can
    // continue to provide contrast for app content while becoming brighter or darker depending on people’s physical surroundings». Seit den Bildern 02 trägt das Glas
    // den Pastellton der App (kataloge/xr_farben.json); Fassung (hell/dunkel) und Deckkraft richten sich nach der Umgebung (AppFarben.Satz). Die Helligkeit des
    // Hintergrunds kennt die App nicht aus dem Bild (kein Zugriff auf das Kamerabild bzw. den Bildpuffer) – sie gilt je Umgebung (Annahmen V1a).

    /// <summary>Hintergrund (sRGB, grau) je Umgebung, Annahmen V1a: heller Sitzungsraum (Farbe des Horizonts), dunkler («tron»), Modell beim Begehen (Wände
    /// mittelgrau), echter Raum (Passthrough, unbekannt – eher hell).</summary>
    public const double HintergrundHell = 0.88, HintergrundDunkel = 0.03, HintergrundModell = 0.55, HintergrundEcht = 0.6;

    /// <summary>Hintergrund der Umgebung (Begehen bzw. Sitzungsraum hell/dunkel bzw. Passthrough).</summary>
    public static double HintergrundFuer(bool sitzung, bool passthrough, bool hell) =>
        passthrough ? HintergrundEcht : !sitzung ? HintergrundModell : hell ? HintergrundHell : HintergrundDunkel;

    // ---------------------------------------------------------------------------------------------------- Bewegungen (ruhig und kurz)
    /// <summary>Aufbauen beim Holen (s), Zurückweichen bei einer Karte (s, m), Anheben beim Annähern (m), Register aufklappen (s).</summary>
    public const double AufbauS = 0.22, ZurueckS = 0.25, ZurueckM = 0.03, AnhebenM = 0.004, AufklappenS = 0.12;
    /// <summary>Verankert: kommt erst nach, wenn der Kopf weiter als so viel (m) vom Fenster weg ist.</summary>
    public const double NachholenM = 1.4;
    /// <summary>Fenster unter den Augen (m): Mitte auf Brusthöhe wie die Kacheln P5a.</summary>
    public const double UnterAugeM = 0.28;

    /// <summary>Weiches Ein- und Ausblenden (0 … 1, «smoothstep»).</summary>
    public static double Weich(double x) { x = Math.Clamp(x, 0, 1); return x * x * (3 - 2 * x); }

    /// <summary>Aufbauen: Deckkraft und Grösse (0.94 → 1) nach t Sekunden seit dem Holen.</summary>
    public static (double Deckkraft, double Groesse) Aufbau(double t)
    {
        var k = Weich(t / AufbauS);
        return (k, 0.94 + 0.06 * k);
    }

    /// <summary>Ein Wert folgt dem Ziel weich (Zeitkonstante s), z. B. Zurückweichen und Anheben.</summary>
    public static double Folgen(double wert, double ziel, double dt, double s) => wert + (ziel - wert) * (1 - Math.Exp(-Math.Max(0, dt) / Math.Max(1e-6, s)));
}

/// <summary>Masse eines Fensters für eine Skala (anzeige.skala / 1.35) und Grösse (apps.groesse_m / 0.18), alles in m.</summary>
public sealed record GlasMasse(double Pt, double SchriftPt)
{
    public static GlasMasse Fuer(AppEinstellungen e)
    {
        var groesse = e.GroesseM / 0.18 * (e.Anordnung == Anordnungen.AnHand ? AppLayout.AnHandAnteil : 1);
        return new GlasMasse(GlasStil.PtM * groesse, GlasStil.SchriftPtM * groesse * e.Skala / 1.35);
    }
    public double M(double pt) => pt * Pt;
    public double Schrift(double pt) => pt * SchriftPt;
    public double FensterB => M(GlasStil.FensterBreitePt);
    /// <summary>V1e: so viele Zeilen (je 60 pt) ist das Fenster höher als gewöhnlich (die Etagenliste auf einer Seite, EtagenListe).</summary>
    public int MehrZeilen { get; init; }
    public double FensterH => M(GlasStil.FensterHoehePt + MehrZeilen * GlasStil.ZeilenMittePt);
}

/// <summary>Die Teile eines Fensters im Raum der Brille (alle als TabletPose, Normale zum Auge).</summary>
public sealed record GlasRahmen(TabletPose Fenster, TabletPose Inhalt, TabletPose Fuss, TabletPose RegisterLeiste, IReadOnlyList<TabletPose> Register,
    TabletPose Griff, TabletPose Schliessen, TabletPose? Ornament, IReadOnlyList<TabletPose> OrnamentKnoepfe)
{
    /// <summary>Alle Ziele zum Antippen: Register, Ornament-Knöpfe, Schliessen, Griff (in dieser Reihenfolge).</summary>
    public IEnumerable<TabletPose> Rand() => Register.Concat(OrnamentKnoepfe).Append(Schliessen).Append(Griff);
}

/// <summary>Form eines Ziels (HIG «Buttons»): Kreis für Symbole, Kapsel für Text, abgerundetes Rechteck nur in senkrechten Stapeln.</summary>
public enum GlasForm { Kreis, Kapsel, Rechteck }

/// <summary>Art eines Inhaltselements.</summary>
public enum GlasArt { Text, Knopf, Schalter, Zeile, Segment }

/// <summary>
/// Ein Element im Inhalt eines Fensters: Id (Ziel; Text ohne), Art, Form, Mitte (X rechts, Y oben, m, relativ zur Mitte des Inhalts), Grösse, Titel, zweite Zeile,
/// Symbol, Ein (Schalter bzw. gewähltes Segment), bedienbar, aktuell (z. B. die Etage am Stand), Schriftstufe (0 Titel, 1 primär, 2 sekundär, 3 tertiär), Ampel.
/// </summary>
public sealed record GlasElement(string? Id, GlasArt Art, GlasForm Form, double X, double Y, double B, double H, string? Titel = null, string? Zeile = null,
    string? Symbol = null, bool Ein = false, bool Aktiv = true, bool Aktuell = false, int Stufe = 1, int Ampel = 0, bool Primaer = false)
{
    public bool Ziel => Id is not null && Art != GlasArt.Text;
}

/// <summary>
/// Inhalt eines Fensters von oben nach unten (Annahmen V1a): Titel, Texte, Knöpfe (Kapsel), Schalter (Zeile mit Schalter rechts), Reihen von Kapseln (Segmente),
/// Zeilen einer Liste (abgerundetes Rechteck im senkrechten Stapel). Die Mitten der Ziele liegen mindestens 60 pt auseinander (GlasStil.ZeilenMittePt).
/// </summary>
public sealed class GlasInhalt(GlasMasse m, double breite, double hoehe)
{
    readonly List<GlasElement> elemente = [];
    double y = hoehe / 2;   // Oberkante des freien Platzes
    public IReadOnlyList<GlasElement> Elemente => elemente;
    public double Breite => breite;
    public double Hoehe => hoehe;
    /// <summary>Platz für so viele weitere Zeilen (Ziele).</summary>
    public int FreieZeilen => Math.Max(0, (int)Math.Floor((y + hoehe / 2 + 1e-9 + (m.M(GlasStil.ZeilenMittePt) - m.M(GlasStil.ZeilePt))) / m.M(GlasStil.ZeilenMittePt)));

    /// <summary>Breite einer Kapsel für einen Text (Schätzung 0.55 × Schrifthöhe je Zeichen plus Rand), höchstens die Breite.</summary>
    public double KapselBreite(string text, bool symbol = false) =>
        Math.Min(breite, Math.Max(m.M(GlasStil.ZeilePt) * 1.6, text.Length * 0.55 * m.Schrift(GlasStil.TextPt) + m.M(40) + (symbol ? m.M(28) : 0)));

    public void Titel(string text)
    {
        var h = m.M(GlasStil.KopfPt);
        elemente.Add(new GlasElement(null, GlasArt.Text, GlasForm.Rechteck, 0, y - h / 2, breite, h, text, Stufe: 0));
        y -= h;
    }

    /// <summary>Ein Text (Stufe 1 primär, 2 sekundär, 3 tertiär) über zeilen Zeilen.</summary>
    public void Text(string text, int stufe = 2, int zeilen = 1)
    {
        var h = m.Schrift(stufe <= 1 ? GlasStil.TextPt : GlasStil.KleinPt) * 1.35 * zeilen + m.M(4);
        elemente.Add(new GlasElement(null, GlasArt.Text, GlasForm.Rechteck, 0, y - h / 2, breite, h, text, Stufe: stufe));
        y -= h;
    }

    double Platz()
    {
        var h = m.M(GlasStil.ZeilenMittePt);
        var mitte = y - h / 2;
        y -= h;
        return mitte;
    }

    /// <summary>Ein Knopf (Kapsel, mittig; primär = der wichtigste der Ansicht).</summary>
    public void Knopf(string id, string titel, string? symbol = null, bool primaer = false, bool aktiv = true, bool ein = false) =>
        elemente.Add(new GlasElement(id, GlasArt.Knopf, GlasForm.Kapsel, 0, Platz(), KapselBreite(titel, symbol is not null), m.M(GlasStil.ZeilePt), titel, Symbol: symbol,
            Aktiv: aktiv, Primaer: primaer, Ein: ein));

    /// <summary>Ein Schalter: Zeile mit Titel links und Schalter rechts (ein = weisser Knopf), ganze Zeile antippbar.</summary>
    public void Schalter(string id, string titel, bool ein, string? zeile = null, bool aktiv = true) =>
        elemente.Add(new GlasElement(id, GlasArt.Schalter, GlasForm.Rechteck, 0, Platz(), breite, m.M(GlasStil.ZeilePt), titel, zeile, Ein: ein, Aktiv: aktiv));

    /// <summary>Eine Reihe von Kapseln gleicher Breite (Segmente bzw. Kurzwahl); ein = gewählt.</summary>
    public void Reihe(IReadOnlyList<(string Id, string Titel, bool Ein)> knoepfe)
    {
        var mitte = Platz();
        if (knoepfe.Count == 0) return;
        var luecke = m.M(8);
        var b = (breite - luecke * (knoepfe.Count - 1)) / knoepfe.Count;
        for (int i = 0; i < knoepfe.Count; i++)
            elemente.Add(new GlasElement(knoepfe[i].Id, GlasArt.Segment, GlasForm.Kapsel, -breite / 2 + b / 2 + i * (b + luecke), mitte, b, m.M(GlasStil.ZeilePt),
                knoepfe[i].Titel, Ein: knoepfe[i].Ein));
    }

    /// <summary>Eine Zeile einer Liste (abgerundetes Rechteck im senkrechten Stapel): Titel, zweite Zeile, Symbol, aktuell.</summary>
    public void Zeile(string id, string titel, string? zeile = null, string? symbol = null, bool aktuell = false, int ampel = 0) =>
        elemente.Add(new GlasElement(id, GlasArt.Zeile, GlasForm.Rechteck, 0, Platz(), breite, m.M(GlasStil.ZeilePt), titel, zeile, symbol, Aktuell: aktuell, Ampel: ampel));
}

/// <summary>Zustand der Verankerung eines Fensters im Raum der Brille.</summary>
public sealed class GlasAnker
{
    /// <summary>Mitte des Fensters (Raum der Brille), null = noch nicht gesetzt.</summary>
    public Vec3? Mitte { get; internal set; }
    /// <summary>Zeitpunkt des letzten Holens (für das Aufbauen).</summary>
    public double Seit { get; internal set; } = double.NegativeInfinity;
    /// <summary>Griff: Abstand von der Hand zur Mitte beim Fassen; null = nicht gefasst.</summary>
    public Vec3? GriffVersatz { get; internal set; }
    public bool Gezogen => GriffVersatz is not null;
}

public static class Glas
{
    /// <summary>Waagrechte Richtung zum Gieren (Blick nach −z bei 90°).</summary>
    static Vec3 Richtung(double gieren) => new(Math.Cos(gieren), 0, -Math.Sin(gieren));

    /// <summary>Platz beim Holen: Abstand vor der Brust, seitlich links neben dem Blick (Grad), auf Brusthöhe (GlasStil.UnterAugeM unter den Augen).</summary>
    public static Vec3 Platz(Vec3 auge, double blickGieren, AppEinstellungen e)
    {
        var d = Richtung(blickGieren + e.SeitlichGrad * Math.PI / 180);
        return new Vec3(auge.X + d.X * e.AbstandM, auge.Y - GlasStil.UnterAugeM, auge.Z + d.Z * e.AbstandM);
    }

    /// <summary>Fenster holen (linke Handfläche, Start): an den Platz vor dem Blick, Aufbauen beginnt.</summary>
    public static void Holen(GlasAnker a, Vec3 mitte, double t)
    {
        a.Mitte = mitte;
        a.Seit = t;
        a.GriffVersatz = null;
    }

    /// <summary>
    /// Verankert (HIG «Avoid anchoring content to the wearer’s head»): das Fenster bleibt, wo es ist; nur wenn der Kopf weiter als <see cref="GlasStil.NachholenM"/>
    /// weg ist (man ist weggegangen) bzw. noch keins steht, kommt es an den Platz. true, wenn es neu gesetzt wurde.
    /// </summary>
    public static bool Pruefen(GlasAnker a, Vec3 auge, Vec3 platz, double t)
    {
        if (a.Gezogen) return false;
        if (a.Mitte is { } m && Num.Hypot(m.X - auge.X, m.Z - auge.Z) <= GlasStil.NachholenM) return false;
        Holen(a, platz, t);
        return true;
    }

    /// <summary>
    /// V1d: träges Nachführen der Tafel (<see cref="Tafel.Nachfuehren"/>) auf den Anker anwenden: dreht man sich weiter als «tafel.nachfuehren_grad» weg, rückt das Fenster
    /// ruhig in die Blickmitte (platz = <see cref="Tafel.Platz"/> für den Blick jetzt); am Griff gehalten bleibt es, wo man es hinstellt.
    /// </summary>
    public static void Nachfuehren(GlasAnker a, TafelFolgen z, Vec3 auge, double blickGieren, double dt, Vec3 platz, TafelEinstellungen e, bool handAn)
    {
        if (a.Mitte is not { } m) return;
        a.Mitte = Tafel.Nachfuehren(z, m, auge, blickGieren, dt, platz, e, handAn, a.Gezogen);
    }

    /// <summary>V1d: so weit (m) reichen Fenster, Werkzeugleiste, Griff und Schliessen unter die Mitte des Fensters (für die Höhe der Tafel am Tisch).</summary>
    public static double UnterhalbM(GlasMasse m, int ornament)
    {
        var f = new TabletPose(Vec3.Null, new Vec3(0, 0, 1), new Vec3(1, 0, 0), new Vec3(0, 1, 0), m.FensterB, m.FensterH);
        var r = Rahmen(f, m, 0, 0, ornament);
        var teile = new List<TabletPose> { r.Fenster, r.Griff, r.Schliessen };
        if (r.Ornament is { } o) teile.Add(o);
        return -teile.Min(t => t.Mitte.Y - t.H / 2);
    }

    /// <summary>Griff fassen (Pinch an der Fensterleiste): die Hand führt das Fenster.</summary>
    public static void Fassen(GlasAnker a, Vec3 hand)
    {
        if (a.Mitte is { } m) a.GriffVersatz = m - hand;
    }

    /// <summary>Die gefasste Hand bewegt das Fenster (es bleibt zum Auge gedreht); loslassen = null.</summary>
    public static void Ziehen(GlasAnker a, Vec3? hand)
    {
        if (a.GriffVersatz is not { } v) return;
        if (hand is not { } h) { a.GriffVersatz = null; return; }
        a.Mitte = h + v;
    }

    /// <summary>Lage des Fensters: Mitte, zum Auge gedreht (aufrecht, um die Neigung zum Auge gekippt), Grösse; zurueck = so weit vom Auge weg (m).</summary>
    public static TabletPose Fenster(Vec3 mitte, Vec3 auge, GlasMasse m, double zurueck = 0)
    {
        var n = (auge - mitte).Einheit();
        var p = XrTabletLogik.TabletLageAus(mitte - n * zurueck, n);
        return p with { B = m.FensterB, H = m.FensterH };
    }

    static TabletPose Teil(TabletPose f, double x, double y, double b, double h, double vor = 0) =>
        f with { Mitte = f.Mitte + f.Rechts * x + f.Oben * y + f.Normale * vor, B = b, H = h };

    /// <summary>
    /// Die Teile des Fensters (siehe Dateikopf): Inhalt (innerhalb des Rands, unter dem Kopf, über der Fusszeile), Fusszeile (Hinweise), Register links (je Register
    /// ein Kreis; aufgeklappt = Anteil 0 … 1 breiter), Griff und Schliessen unter dem Fenster, Werkzeugleiste (Ornament) mit ornament Knöpfen an der Unterkante.
    /// </summary>
    public static GlasRahmen Rahmen(TabletPose f, GlasMasse m, int register, double aufgeklappt = 0, int ornament = 0)
    {
        double rand = m.M(GlasStil.RandPt), fuss = m.M(GlasStil.FussPt);
        var innenB = f.B - 2 * rand;
        var innenH = f.H - 2 * rand - fuss;
        var inhalt = Teil(f, 0, (fuss) / 2, innenB, innenH, 0.001);
        var fussLage = Teil(f, 0, -f.H / 2 + rand / 2 + fuss / 2, innenB, fuss, 0.001);
        // Register: senkrechte Leiste links (Kapsel), Kreise in 64 pt Abstand, mittig zur Höhe
        double kreis = m.M(GlasStil.RegisterKreisPt), abst = m.M(GlasStil.RegisterMittePt), innen = m.M(GlasStil.RegisterInnenPt);
        var leisteB = kreis + 2 * innen + (m.M(GlasStil.RegisterAufPt) - kreis) * Math.Clamp(aufgeklappt, 0, 1);
        var leisteH = Math.Max(kreis + 2 * innen, (register - 1) * abst + kreis + 2 * innen);
        var leisteX = -f.B / 2 - m.M(GlasStil.RegisterLueckePt) - leisteB / 2;
        var leiste = Teil(f, leisteX, 0, leisteB, leisteH, 0.004);
        var reg = new List<TabletPose>();
        for (int i = 0; i < register; i++)
        {
            var y = ((register - 1) / 2.0 - i) * abst;
            var b = leisteB - 2 * innen;
            reg.Add(Teil(f, leisteX, y, b, kreis, 0.006));
        }
        // Werkzeugleiste (Ornament) an der Unterkante, leicht davor; ragt nur OrnamentUeberlappPt über die Kante (die Fusszeile bleibt frei)
        TabletPose? orn = null;
        var ok = new List<TabletPose>();
        var unten = -f.H / 2;   // Unterkante von Fenster bzw. Werkzeugleiste
        if (ornament > 0)
        {
            double ok0 = m.M(GlasStil.OrnamentKreisPt), oi = m.M(GlasStil.OrnamentInnenPt), oabst = m.M(GlasStil.ZeilenMittePt);
            var ob = (ornament - 1) * oabst + ok0 + 2 * oi;
            var oh = ok0 + 2 * oi;
            var oy = -f.H / 2 + m.M(GlasStil.OrnamentUeberlappPt) - oh / 2;
            orn = Teil(f, 0, oy, ob, oh, GlasStil.OrnamentVorM);
            for (int i = 0; i < ornament; i++) ok.Add(Teil(f, (i - (ornament - 1) / 2.0) * oabst, oy, ok0, ok0, GlasStil.OrnamentVorM + 0.002));
            unten = oy - oh / 2;
        }
        // Fensterleiste darunter: Griff (Kapsel) und links daneben Schliessen (Kreis)
        double gb = m.M(GlasStil.GriffBreitePt), gh = m.M(GlasStil.GriffHoehePt), ga = m.M(GlasStil.GriffAbstandPt), s = m.M(GlasStil.SchliessenPt);
        var gy = unten - ga - Math.Max(gh, s) / 2;
        var griff = Teil(f, 0, gy, gb, gh, 0);
        var schliessen = Teil(f, -gb / 2 - m.M(14) - s / 2, gy, s, s, 0);
        return new GlasRahmen(f, inhalt, fussLage, leiste, reg, griff, schliessen, orn, ok);
    }

    /// <summary>Lage eines Inhaltselements im Raum (Mitte relativ zur Mitte des Inhalts), 2 mm vor dem Glas; anheben = zusätzlich zum Auge hin (m).</summary>
    public static TabletPose Lage(TabletPose inhalt, GlasElement e, double anheben = 0) =>
        inhalt with { Mitte = inhalt.Mitte + inhalt.Rechts * e.X + inhalt.Oben * e.Y + inhalt.Normale * (0.002 + anheben), B = e.B, H = e.H };

    // ---------------------------------------------------------------------------------------------------- Sitzungsraum: neben dem Tisch

    /// <summary>
    /// Befund Prüfbild 24.10.2026 («am Sitzungstisch überdecken die Kacheln den Tisch»): Platz des Fensters am Tisch so, dass es im Grundriss nicht über der Platte
    /// steht. tisch = die vier Ecken der Platte im Raum der Brille. Gesucht wird ab apps.seitlich_grad links in 5°-Schritten bis 120°, dann rechts; das Fenster steht
    /// im Abstand apps.abstand_m (bei Bedarf bis 0.2 m näher) auf Brusthöhe. Zählt: die Unterkante des Fensters samt rand (m) liegt im Grundriss ausserhalb der Platte.
    /// Ohne passenden Platz der Platz nach der Stellschraube.
    /// </summary>
    public static Vec3 NebenTisch(Vec3 auge, double blickGieren, AppEinstellungen e, GlasMasse m, IReadOnlyList<Vec3> tisch, double rand = 0.05)
    {
        var poly = tisch.Select(p => new Xz(p.X, p.Z)).ToList();
        IEnumerable<double> Winkel()
        {
            for (var w = e.SeitlichGrad; w <= 120 + 1e-9; w += 5) yield return w;
            for (var w = -e.SeitlichGrad; w >= -120 - 1e-9; w -= 5) yield return w;
        }
        foreach (var abzug in new[] { 0.0, 0.1, 0.2 })
            foreach (var w in Winkel())
            {
                var d = Richtung(blickGieren + w * Math.PI / 180);
                var abstand = Math.Max(0.25, e.AbstandM - abzug);
                var mitte = new Vec3(auge.X + d.X * abstand, auge.Y - GlasStil.UnterAugeM, auge.Z + d.Z * abstand);
                if (!UeberTisch(Fenster(mitte, auge, m), poly, rand)) return mitte;
            }
        return Platz(auge, blickGieren, e);
    }

    /// <summary>Steht das Fenster (samt Rand) im Grundriss über dem Vieleck? Geprüft wird die Unterkante als Strecke (das Fenster steht aufrecht).</summary>
    public static bool UeberTisch(TabletPose f, IReadOnlyList<Xz> poly, double rand)
    {
        var a = f.Mitte - f.Rechts * (f.B / 2 + rand) - f.Oben * (f.H / 2);
        var b = f.Mitte + f.Rechts * (f.B / 2 + rand) - f.Oben * (f.H / 2);
        var pa = new Xz(a.X, a.Z);
        var pb = new Xz(b.X, b.Z);
        for (int i = 0; i <= 10; i++)
        {
            var p = new Xz(pa.X + (pb.X - pa.X) * i / 10, pa.Z + (pb.Z - pa.Z) * i / 10);
            if (Innen(poly, p) || AbstandVieleck(poly, p) < rand) return true;
        }
        return false;
    }

    static bool Innen(IReadOnlyList<Xz> poly, Xz p)
    {
        var innen = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            if ((poly[i].Z > p.Z) != (poly[j].Z > p.Z) && p.X < (poly[j].X - poly[i].X) * (p.Z - poly[i].Z) / (poly[j].Z - poly[i].Z) + poly[i].X) innen = !innen;
        return innen;
    }

    static double AbstandVieleck(IReadOnlyList<Xz> poly, Xz p)
    {
        var best = double.PositiveInfinity;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            double dx = poly[i].X - poly[j].X, dz = poly[i].Z - poly[j].Z, l2 = dx * dx + dz * dz;
            var t = l2 < 1e-12 ? 0 : Math.Clamp(((p.X - poly[j].X) * dx + (p.Z - poly[j].Z) * dz) / l2, 0, 1);
            best = Math.Min(best, Num.Hypot(p.X - poly[j].X - dx * t, p.Z - poly[j].Z - dz * t));
        }
        return best;
    }

    // ---------------------------------------------------------------------------------------------------- Hinweise (Befund Prüfbild 24.10.2026)

    /// <summary>
    /// Befund Prüfbild 24.10.2026 («Hinweistexte … liegen frei im Raum über den Kacheln und sind schlecht lesbar»): ein Hinweis steht nie frei im Raum. Ist das Fenster
    /// sichtbar, in seiner Fusszeile (auf dem Glas); sonst als eigene kleine Glas-Kapsel unten vor dem Blick (0.55 m vor dem Auge, 0.22 m tiefer), die nichts überdeckt:
    /// liegt sie über einem anderen Glas (z. B. der Karte), rückt sie darunter. Rückgabe: Lage und ob es die Fusszeile ist.
    /// </summary>
    public static (TabletPose Lage, bool Fusszeile) HinweisLage(GlasRahmen? fenster, Vec3 auge, double blickGieren, GlasMasse m, IReadOnlyList<TabletPose> andere, double breite)
    {
        if (fenster is not null) return (fenster.Fuss, true);
        var d = Richtung(blickGieren);
        var mitte = new Vec3(auge.X + d.X * 0.55, auge.Y - 0.22, auge.Z + d.Z * 0.55);
        var h = m.M(40);
        for (int i = 0; i < 6; i++)
        {
            var p = XrTabletLogik.TabletLageAus(mitte, auge - mitte) with { B = breite, H = h };
            if (!andere.Any(a => Ueberdeckt(p, a))) return (p, false);
            mitte -= new Vec3(0, h + m.M(12), 0);
        }
        return (XrTabletLogik.TabletLageAus(mitte, auge - mitte) with { B = breite, H = h }, false);
    }

    /// <summary>Überdecken sich zwei aufrechte Flächen (Rechtecke, grob in der Ebene der ersten gemessen; Abstand in der Tiefe höchstens 0.3 m)?</summary>
    public static bool Ueberdeckt(TabletPose a, TabletPose b)
    {
        var d = b.Mitte - a.Mitte;
        if (Math.Abs(d.Skalar(a.Normale)) > 0.3) return false;
        var x = Math.Abs(d.Skalar(a.Rechts));
        var y = Math.Abs(d.Skalar(a.Oben));
        return x < (a.B + b.B) / 2 && y < (a.H + b.H) / 2;
    }

    // ---------------------------------------------------------------------------------------------------- Überdecken im Blick (V1a, Befund Prüfbilder 02)
    // Hinweise in verschiedener Tiefe überlagerten sich, weil «Ueberdeckt» nur Flächen in ähnlicher Tiefe vergleicht. Im Blick zählt der Winkel: die Ecken der einen
    // Fläche werden vom Auge aus auf die Ebene der anderen projiziert (Zentralprojektion) und dort verglichen – auch für Modell und Hände (Hülle im Blick).

    /// <summary>Fläche b × h an p, zum Auge gedreht (Hinweis, Tooltip).</summary>
    public static TabletPose ImBlick(Vec3 p, Vec3 auge, double b, double h) => XrTabletLogik.TabletLageAus(p, auge - p) with { B = b, H = h };

    /// <summary>Punkt p vom Auge aus auf die Ebene der Fläche projiziert; null, wenn er nicht vor dem Auge liegt (in Richtung der Ebene).</summary>
    public static Vec3? AufEbene(Vec3 p, Vec3 auge, TabletPose ebene)
    {
        var weg = ebene.Normale * -1;   // die Normale zeigt zum Auge
        var abstand = (ebene.Mitte - auge).Skalar(weg);
        var d = p - auge;
        var nenner = d.Skalar(weg);
        if (abstand <= 1e-6 || nenner <= 1e-4) return null;
        return auge + d * (abstand / nenner);
    }

    static (double X0, double X1, double Y0, double Y1)? Spanne(IEnumerable<Vec3> punkte, Vec3 auge, TabletPose ebene)
    {
        double x0 = double.PositiveInfinity, x1 = double.NegativeInfinity, y0 = double.PositiveInfinity, y1 = double.NegativeInfinity;
        foreach (var p in punkte)
        {
            if (AufEbene(p, auge, ebene) is not { } q) continue;
            var w = q - ebene.Mitte;
            double x = w.Skalar(ebene.Rechts), y = w.Skalar(ebene.Oben);
            x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y);
        }
        return double.IsPositiveInfinity(x0) ? null : (x0, x1, y0, y1);
    }

    /// <summary>Die vier Ecken einer Fläche.</summary>
    public static Vec3[] Ecken(TabletPose f) =>
    [
        f.Mitte - f.Rechts * (f.B / 2) - f.Oben * (f.H / 2), f.Mitte + f.Rechts * (f.B / 2) - f.Oben * (f.H / 2),
        f.Mitte + f.Rechts * (f.B / 2) + f.Oben * (f.H / 2), f.Mitte - f.Rechts * (f.B / 2) + f.Oben * (f.H / 2),
    ];

    /// <summary>Überdecken sich a und b im Blick vom Auge (Winkel statt Tiefe; luft = Abstand in der Ebene von a, m)? Was hinter dem Auge liegt, zählt nicht.</summary>
    public static bool UeberdecktImBlick(TabletPose a, TabletPose b, Vec3 auge, double luft = 0)
    {
        if (Spanne(Ecken(b), auge, a) is not { } s) return false;
        return s.X0 < a.B / 2 + luft && s.X1 > -a.B / 2 - luft && s.Y0 < a.H / 2 + luft && s.Y1 > -a.H / 2 - luft;
    }

    /// <summary>
    /// Hülle von Punkten im Blick (Modell auf dem Tisch, Hand): Rechteck senkrecht zum Blick durch die Mitte der Punkte, das alle Punkte vom Auge aus gesehen umfasst,
    /// plus rand (m); null, wenn kein Punkt vor dem Auge liegt.
    /// </summary>
    public static TabletPose? HuelleImBlick(IReadOnlyCollection<Vec3> punkte, Vec3 auge, double rand = 0)
    {
        var gueltig = punkte.Where(p => p.IstEndlich).ToList();
        if (gueltig.Count == 0) return null;
        var mitte = gueltig.Aggregate(Vec3.Null, (s, p) => s + p) * (1.0 / gueltig.Count);
        if ((mitte - auge).Laenge < 1e-6) return null;
        var ebene = XrTabletLogik.TabletLageAus(mitte, auge - mitte);
        if (Spanne(gueltig, auge, ebene) is not { } s) return null;
        return ebene with { Mitte = mitte + ebene.Rechts * ((s.X0 + s.X1) / 2) + ebene.Oben * ((s.Y0 + s.Y1) / 2), B = s.X1 - s.X0 + 2 * rand, H = s.Y1 - s.Y0 + 2 * rand };
    }

    /// <summary>
    /// Liegt p vom Auge aus gesehen HINTER der Fläche f (innerhalb ihres Umrisses plus rand, weiter weg als ihre Ebene)? Für Dinge im Modell hinter einem Fenster bzw.
    /// der Karte: sie treten zurück (Befund Prüfbilder 03: der Kopf eines Problem-Markers hinter dem Fenster leuchtete in der Lücke zwischen Karte und Fenster).
    /// </summary>
    public static bool HinterFlaeche(Vec3 p, Vec3 auge, TabletPose f, double rand = 0)
    {
        if (AufEbene(p, auge, f) is not { } q) return false;
        if ((p - auge).Laenge <= (q - auge).Laenge) return false;   // vor der Fläche (oder auf ihr)
        var w = q - f.Mitte;
        return Math.Abs(w.Skalar(f.Rechts)) <= f.B / 2 + rand && Math.Abs(w.Skalar(f.Oben)) <= f.H / 2 + rand;
    }

    /// <summary>
    /// Liegt p im Blick hinter der Bedienung (Fenster mit Leiste, Griff, Schliessen; Karte)? Hinter einer der Flächen (mit rand, m, in ihrer Ebene) oder im Blick
    /// innerhalb der Hülle aller Flächen und weiter weg als jede ihrer Ecken – so zählt auch die Lücke zwischen Karte und Fenster (Befund Prüfbilder 03). Die Hülle
    /// allein genügt nicht: sie steht senkrecht zum Blick auf ihre Mitte und liegt am Rand weiter weg als das Fenster.
    /// </summary>
    public static bool HinterBedienung(Vec3 p, Vec3 auge, IReadOnlyList<TabletPose> flaechen, double rand = 0.02)
    {
        if (flaechen.Count == 0) return false;
        foreach (var f in flaechen) if (HinterFlaeche(p, auge, f, rand)) return true;
        var ecken = flaechen.SelectMany(Ecken).ToList();
        if ((p - auge).Laenge <= ecken.Max(e => (e - auge).Laenge)) return false;
        if (HuelleImBlick(ecken, auge) is not { } h || AufEbene(p, auge, h) is not { } q) return false;
        var w = q - h.Mitte;
        return Math.Abs(w.Skalar(h.Rechts)) <= h.B / 2 && Math.Abs(w.Skalar(h.Oben)) <= h.H / 2;
    }

    /// <summary>Höchstens so viele Zeilen je Seite einer Liste, wenn vorher schon belegt Zeilen stehen (mindestens 1).</summary>
    public static int JeSeite(int frei) => Math.Max(1, frei);

    // ---------------------------------------------------------------------------------------------------- Strahl auf Ziele

    /// <summary>Ziel unter dem Strahl (u, r; Einheit): Index und Weg, nur von vorn und innerhalb der Fläche (rand m Zugabe), sonst null.</summary>
    public static (int Index, double T)? Strahl(IReadOnlyList<TabletPose> ziele, Vec3 u, Vec3 r, double rand = 0.005)
    {
        (int, double)? best = null;
        for (int i = 0; i < ziele.Count; i++)
        {
            var z = ziele[i];
            var nr = z.Normale.Skalar(r);
            if (nr > -1e-6) continue;
            var t = (z.Mitte - u).Skalar(z.Normale) / nr;
            if (t <= 0) continue;
            var p = u + r * t - z.Mitte;
            if (Math.Abs(p.Skalar(z.Rechts)) <= z.B / 2 + rand && Math.Abs(p.Skalar(z.Oben)) <= z.H / 2 + rand && (best is null || t < best.Value.Item2)) best = (i, t);
        }
        return best;
    }
}

/// <summary>Zustand der Bedienung mit dem Strahl je Hand: Ziel unter dem Strahl, Pinch, das beim Drücken getroffene Ziel (wählt beim Loslassen auf demselben Ziel).</summary>
public sealed class GlasStrahlHand
{
    public int? Hover { get; internal set; }
    internal bool Pinch;
    internal int? Gedrueckt;
}

public static class GlasStrahl
{
    /// <summary>
    /// V1f, Pistolengeste: hover = Ziel unter dem Strahl; das Abdrücken wählt sofort das Ziel unter dem Strahl beim Abzug (von kurz davor, Pistole.StrahlVorS), sonst nichts.
    /// </summary>
    public static int? Abzug(GlasStrahlHand z, IReadOnlyList<TabletPose> ziele, (Vec3 U, Vec3 R)? strahl, bool abgedrueckt, (Vec3 U, Vec3 R)? strahlBeimAbzug)
    {
        z.Hover = strahl is { } s ? Glas.Strahl(ziele, s.U, s.R)?.Index : null;
        z.Pinch = false;
        z.Gedrueckt = null;
        if (!abgedrueckt || (strahlBeimAbzug ?? strahl) is not { } a) return null;
        return Glas.Strahl(ziele, a.U, a.R)?.Index;
    }

    /// <summary>
    /// Ein Schritt: hover = Ziel unter dem Strahl; ein Pinch, der auf einem Ziel beginnt und auf demselben endet, wählt es (Rückgabe). Ohne Strahl (null) nichts.
    /// </summary>
    public static int? Schritt(GlasStrahlHand z, IReadOnlyList<TabletPose> ziele, (Vec3 U, Vec3 R)? strahl, bool pinch)
    {
        z.Hover = strahl is { } s ? Glas.Strahl(ziele, s.U, s.R)?.Index : null;
        int? aus = null;
        if (pinch && !z.Pinch) z.Gedrueckt = z.Hover;
        else if (!pinch && z.Pinch)
        {
            if (z.Gedrueckt is int g && z.Hover == g) aus = g;
            z.Gedrueckt = null;
        }
        z.Pinch = pinch;
        return aus;
    }
}
