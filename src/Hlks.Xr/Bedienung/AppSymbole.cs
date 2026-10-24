// Quest App V1a, Vorgaben Christian 24.10.2026: frei schwebende App-Symbole – «angewählt werden sie durch berührung. finger zeigen den pointer mit tooltip»;
// «symbole nicht in einer leiste sondern verteilt»; nach den Bildern 02: «die appsymbole sollen frontal des nutzers in einem abstand von ca. 60cm und über eine höhe
// von ca. 0.6m verteilt sein. vorzugsweise maximal 3 reihig.» Rein, ohne StereoKit; die Gesten in HebeGeste.cs, die Berührung mit der ganzen Hand in
// HandBeruehrung.cs, gezeichnet in src/Hlks.Quest/SymbolApps.cs.
//   - Lage (AppSymbole.Plaetze): ein Feld frontal in Körperrichtung (Gierwinkel des Kopfs beim Auslösen, ohne Nicken), «symbole.abstand_m» vor den Augen (Bezugspunkt:
//     Mitte zwischen den Augen, waagrecht gemessen), senkrechte Mitte «symbole.hoehe_m» unter den Augen, von der obersten zur untersten Reihe «symbole.feldhoehe_m»,
//     höchstens «symbole.reihen» Reihen. Die Reihen sind locker gegeneinander versetzt, jedes Symbol leicht in Höhe und Tiefe verschoben (keine Leiste, keine starre
//     Matrix). Je App immer derselbe Platz im Feld (Muskelgedächtnis). Die Breite folgt aus der Zahl der Apps und dem Spaltenabstand. Ist eine Untergrenze gesetzt
//     (Tischplatte am Sitzungstisch, AppSymbole.Untergrenze mit «symbole.tisch_abstand_m»), rückt die unterste Reihe darüber und das Feld wird niedriger. Im Raum
//     fest, zum Kopf gewandt.
//   - Ist eine App offen (Variante «feld_bei_app»): das Feld weicht seitlich neben Fenster und Ornamente aus (Vorgabe; Christian 24.10.2026: «Das Symbolfeld weicht bei
//     offener App seitlich neben das Fenster aus.»; Seitlich) bzw. zum Vergleich dahinter zurück (Zurueck/Gedaempft).
//     Variante «symbole» = «verteilt» (Vorgabe) | «bogen» (eine Reihe im Bogen, zum Vergleich im Labor).
//   - Zeiger mit Tooltip (Zeiger): ein gestreckter Zeigefinger nahe an einem Symbol bzw. Knopf zeigt einen Punkt darauf und eine feine Linie von der Fingerspitze;
//     nach einer Verweilzeit der Name als Tooltip. Apple HIG «Buttons» (visionOS): «the system displays a tooltip after people hover over a button for a moment»
//     (abgerufen 24.10.2026). Der Tooltip steht ÜBER dem Ziel (Befund Prüfbilder 02: darunter verdeckte ihn die zeigende Hand; HIG «Eyes» nennt «below» für den Blick,
//     mit der Hand zeigt man von unten).
// V1d (Brillentest V1, Christian 24.10.2026: «die apps solten beim hochwischen etwas weiter oben sein», «die appanordnung muss bei erkanntem sitzen eher etwas weiter
// oben sein», am Tisch «apps sind nun ca. 20cm zu hoch», bestätigt «beim Begehen etwas höher, am Sitzungstisch rund 20 cm tiefer»): die Mitte liegt beim Begehen
// «symbole.hoehe_m» (0.22 m, bis V1d 0.32 m) unter den Augen, sitzend «symbole.hoehe_sitzend_m» (0.08 m) höher, am Sitzungstisch «symbole.hoehe_tisch_m» (0.52 m)
// darunter (MitteUnterAuge); die Tischregel hebt nur noch die unterste Reihe über die Platte (das Feld wird dafür gestaucht, höchstens bis FeldMinM). Die oberste
// Reihe darf beim Begehen leicht über Augenhöhe liegen. Bewegt (Neuanordnung nach der Wahl erkennen): HandBeruehrung sperrt die Wahl, solange sich das Feld bewegt.
// Alle Zahlen sind Annahmen V1a bzw. V1d bzw. Christians Werte (Stellschrauben symbole.*, zeiger.tooltip_s), keine Normwerte.

namespace Hlks.Xr.Bedienung;

/// <summary>Ein App-Symbol: Id der App, Lage (Kreis, Normale zum Auge).</summary>
public sealed record SymbolPlatz(string Id, TabletPose Lage);

public static class AppSymbole
{
    /// <summary>Durchmesser eines Symbols (pt), Annahme V1a: grosse runde Ziele für die Hand (visionOS-Startseite; im Feld 0.6 m vor den Augen etwa 8.6 cm, gut 8°).</summary>
    public const double DurchmesserPt = 96;
    /// <summary>So weit unter ihrem Platz beginnen die Symbole beim Hineinheben (m).</summary>
    public const double HubM = 0.28;
    /// <summary>Tooltip: Höhe und Lücke über dem Symbol (pt).</summary>
    public const double TooltipHoehePt = 30, TooltipLueckePt = 8;
    /// <summary>Ausblenden (s).</summary>
    public const double AusblendenS = 0.25;

    /// <summary>
    /// Feste Reihenfolge der Plätze im Feld (Zeile für Zeile von oben links): gleiche App immer am gleichen Platz. «ort» (Begehen) und «tisch» (Sitzungsraum) teilen
    /// sich einen Platz. Apps, die hier fehlen, bekommen Reserveplätze in einer Spalte rechts daneben.
    /// </summary>
    public static IReadOnlyList<string> Reihenfolge => reihenfolge;
    static readonly string[] reihenfolge = ["etagen", "ort", "messen", "marker", "status", "ebenen"];

    /// <summary>Waagrechter Abstand der Spalten (m, × Streuung), Annahme V1a: Symbole (6.5 cm) und Tooltips (darüber) überlappen nie, und eine flache Hand (8 cm)
    /// berührt nicht zwei Nachbarn zugleich tief.</summary>
    public const double SpalteM = 0.2;
    /// <summary>Lockerer Versatz der Reihen (Anteil des Spaltenabstands, je Reihe von oben) – keine starre Matrix.</summary>
    static readonly double[] ReiheVersatz = [-0.18, 0.32, -0.05];
    /// <summary>Leichte Verschiebung je Platz in der Höhe (m, nie über die Reihe hinaus nach oben in der obersten Reihe) und Tiefe (m, zum Auge hin).</summary>
    static readonly (double Hoch, double Vor)[] Wackeln = [(-0.012, 0.02), (-0.03, -0.015), (0.022, 0.0), (-0.018, 0.025), (0.015, -0.02), (-0.025, 0.012)];
    /// <summary>Kleinste Feldhöhe (m), wenn eine Untergrenze (Tischplatte) das Feld zusammenschiebt. V1a 0.2 m; V1d 0.24 m – am Tisch liegt das Feld 0.20 m tiefer
    /// und wird öfter gestaucht, die Reihen bleiben so mindestens 12 cm auseinander (Mitten mindestens 10 cm, Symbole 8.6 cm).</summary>
    public const double FeldMinM = 0.24;

    /// <summary>Durchmesser in m.</summary>
    public static double Durchmesser(GlasMasse m) => m.M(DurchmesserPt);

    /// <summary>
    /// Untergrenze der Symbolmitten am Sitzungstisch: Tischplatte (Oberkante, m) + <see cref="SymbolEinstellungen.TischAbstandM"/> + halber Durchmesser – die
    /// Unterkante jedes Symbols liegt mindestens um den Abstand über der Platte (auch mit der leichten Verschiebung je Platz).
    /// </summary>
    public static double Untergrenze(double tischOben, SymbolEinstellungen e, GlasMasse m) => tischOben + Math.Max(0, e.TischAbstandM) + Durchmesser(m) / 2;

    /// <summary>Platz im Feld je Id: (Reihe von oben, Spalte, Spalten insgesamt) bei r Reihen; Reserve rechts daneben in der Reihenfolge der Ids.</summary>
    public static Dictionary<string, (int Reihe, int Spalte)> Raster(IReadOnlyList<string> ids, int reihen, out int spalten)
    {
        var r = Math.Clamp(reihen, 1, 3);
        var feste = (reihenfolge.Length + r - 1) / r;
        var aus = new Dictionary<string, (int, int)>(StringComparer.Ordinal);
        var reserve = 0;
        foreach (var id in ids)
        {
            var k = Array.IndexOf(reihenfolge, id == "tisch" ? "ort" : id);
            if (k >= 0) aus[id] = (k / feste, k % feste);
            else { aus[id] = (reserve % r, feste + reserve / r); reserve++; }
        }
        spalten = feste + (reserve + r - 1) / r;
        return aus;
    }

    /// <summary>
    /// Plätze der Symbole (siehe Dateikopf): Feld frontal in Blickrichtung blickGieren (nur Gieren), <see cref="SymbolEinstellungen.AbstandM"/> vor dem Auge, senkrechte
    /// Mitte <see cref="SymbolEinstellungen.HoeheM"/> darunter, Höhe <see cref="SymbolEinstellungen.FeldHoeheM"/>, <see cref="SymbolEinstellungen.Reihen"/> Reihen;
    /// untergrenze = tiefste Höhe einer Symbolmitte (z. B. über der Tischplatte) oder null. «bogen»: eine Reihe im Bogen auf der Höhe der Mitte. Jedes Symbol ist zum
    /// Auge gedreht.
    /// </summary>
    public static List<SymbolPlatz> Plaetze(IReadOnlyList<string> ids, Vec3 auge, double blickGieren, SymbolEinstellungen e, GlasMasse m, double? untergrenze = null,
        bool sitzend = false)
    {
        var vor = new Vec3(Math.Cos(blickGieren), 0, -Math.Sin(blickGieren));
        var rechts = HebeGeste.Rechts(blickGieren);   // rechts vom Blick (Blick −z → rechts +x)
        var d = Durchmesser(m);
        var spalte = SpalteM * e.Streuung;
        var aus = new List<SymbolPlatz>();
        var mitteUnten = MitteUnterAuge(e, untergrenze is not null, sitzend);
        if (e.Verteilung == Varianten.SymboleBogen)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                var w = (i - (ids.Count - 1) / 2.0) * spalte / Math.Max(0.1, e.AbstandM);
                var r = new Vec3(Math.Cos(blickGieren - w), 0, -Math.Sin(blickGieren - w));
                var p = auge + r * e.AbstandM + new Vec3(0, -mitteUnten, 0);
                if (untergrenze is { } ub && p.Y < ub) p = p with { Y = ub };
                aus.Add(new SymbolPlatz(ids[i], XrTabletLogik.TabletLageAus(p, auge - p) with { B = d, H = d }));
            }
            return aus;
        }
        var reihen = Math.Clamp(e.Reihen, 1, 3);
        var raster = Raster(ids, reihen, out var spalten);
        // Höhe der obersten und untersten Reihe; eine Untergrenze schiebt das Feld zusammen (nie unter FeldMinM)
        var oben = auge.Y - mitteUnten + (reihen > 1 ? e.FeldHoeheM / 2 : 0);
        var unten = auge.Y - mitteUnten - (reihen > 1 ? e.FeldHoeheM / 2 : 0);
        if (untergrenze is { } u && unten < u)
        {
            unten = u;
            oben = Math.Max(oben, unten + (reihen > 1 ? FeldMinM : 0));
        }
        var mitte = auge + vor * e.AbstandM;
        foreach (var id in ids)
        {
            var (reihe, sp) = raster[id];
            var platz = reihe * 3 + sp;   // Index für die feste Verschiebung (unabhängig von Reserven: gleiche App, gleicher Platz)
            var (hoch, nachVorn) = Wackeln[platz % Wackeln.Length];
            if (reihe == 0) hoch = Math.Min(0, hoch);   // die oberste Reihe nie höher
            var y = reihen > 1 ? oben - (oben - unten) * reihe / (reihen - 1) : oben;
            if (untergrenze is { } ug) hoch = Math.Max(hoch, ug - y);   // die Verschiebung je Platz nie unter die Untergrenze (Tischplatte)
            var x = (sp - (spalten - 1) / 2.0 + ReiheVersatz[reihe % ReiheVersatz.Length]) * spalte;
            var p = new Vec3(mitte.X, y + hoch, mitte.Z) + rechts * x + vor * -nachVorn;
            aus.Add(new SymbolPlatz(id, XrTabletLogik.TabletLageAus(p, auge - p) with { B = d, H = d }));
        }
        return aus;
    }

    /// <summary>
    /// V1d: so weit (m) liegt die senkrechte Mitte des Felds (vor der Tischregel) unter den Augen: am Sitzungstisch <see cref="SymbolEinstellungen.HoeheTischM"/>, sonst
    /// <see cref="SymbolEinstellungen.HoeheM"/>, sitzend um <see cref="SymbolEinstellungen.HoeheSitzendM"/> höher (Brillentest V1, Christian 24.10.2026).
    /// </summary>
    public static double MitteUnterAuge(SymbolEinstellungen e, bool amTisch, bool sitzend) =>
        amTisch ? e.HoeheTischM : e.HoeheM - (sitzend ? e.HoeheSitzendM : 0);

    /// <summary>Mitte der Plätze (dort öffnet sich die gewählte App).</summary>
    public static Vec3 Mitte(IReadOnlyList<SymbolPlatz> p) =>
        p.Count == 0 ? Vec3.Null : p.Aggregate(Vec3.Null, (s, x) => s + x.Lage.Mitte) * (1.0 / p.Count);

    /// <summary>V1d: so schnell (m/s) darf sich ein Symbol höchstens bewegen, damit das Feld als «in Ruhe» gilt (Ende der Neuanordnung), Annahme V1d.</summary>
    public const double RuheTempoMs = 0.03;

    /// <summary>
    /// V1d: bewegt sich das Feld (Neuanordnung: Ausweichen neben die App, Zurückkehren, Hineinheben, Mitlaufen beim Wegwischen)? Wahr, wenn sich ein Symbol zwischen
    /// zwei Bildern schneller als <see cref="RuheTempoMs"/> bewegt hat oder sich die Zahl der Symbole geändert hat.
    /// </summary>
    public static bool Bewegt(IReadOnlyList<Vec3> vorher, IReadOnlyList<Vec3> jetzt, double dt)
    {
        if (vorher.Count != jetzt.Count) return true;
        var weg = RuheTempoMs * Math.Max(1e-3, dt);
        for (int i = 0; i < jetzt.Count; i++) if (vorher[i].Abstand(jetzt[i]) > weg) return true;
        return false;
    }

    // ---------------------------------------------------------------------------------------------------- Nach der Wahl (Variante «symbolwahl» = «stehen»)
    // Christian 24.10.2026: «werden die aps hineingehoben bleiben diese stehen.» Auslegung Hauptlauf (Christian kann berichtigen): die gewählte App öffnet frontal etwas
    // näher; das Symbolfeld weicht seitlich aus (Seitlich, Vorgabe) bzw. – Variante «dahinter» – leicht in die Tiefe zurück und wird gedämpft (visionOS: der Hintergrund weicht zurück); schliesst die App, kommt es wieder nach
    // vorn. Annahmen V1a.

    /// <summary>Die App steht so viel (m) näher als das Feld; das Feld weicht so weit (m) zurück und wird so gedämpft (Deckkraft).</summary>
    public const double AppVorM = 0.15, ZurueckM = 0.12, GedaempftDeckkraft = 0.45;

    /// <summary>
    /// Platz der gewählten App vor dem Feld: frontal in Richtung des Felds (Gieren beim Holen), <see cref="AppVorM"/> näher als das Feld, Mitte auf der Höhe der
    /// senkrechten Mitte des Felds.
    /// </summary>
    public static Vec3 FensterVorFeld(Vec3 auge, double blickGieren, SymbolEinstellungen e) =>
        auge + new Vec3(Math.Cos(blickGieren), 0, -Math.Sin(blickGieren)) * Math.Max(0.3, e.AbstandM - AppVorM) + new Vec3(0, -e.HoeheM, 0);

    /// <summary>Ein Symbol weicht um k (0 … 1) × <see cref="ZurueckM"/> entlang der Sichtlinie vom Auge zurück (im Blick an derselben Stelle, nur kleiner).</summary>
    public static Vec3 Zurueck(Vec3 p, Vec3 auge, double k)
    {
        var d = p - auge;
        var l = d.Laenge;
        return l < 1e-6 ? p : auge + d * ((l + ZurueckM * Math.Clamp(k, 0, 1)) / l);
    }

    /// <summary>Deckkraft des Felds, wenn es um k (0 … 1) zurückgewichen ist.</summary>
    public static double Gedaempft(double k) => 1 - (1 - GedaempftDeckkraft) * Math.Clamp(k, 0, 1);

    /// <summary>Variante «seitlich»: so viel Luft (m) bleibt zwischen dem Rand des Fensters (im Blick) und dem Rand eines Symbols, Annahme V1a.</summary>
    public const double SeitlichRandM = 0.03;

    /// <summary>
    /// Variante «feld_bei_app» = «seitlich» (Vorgabe; Christian 24.10.2026: «Das Symbolfeld weicht bei offener App seitlich neben das Fenster aus.»): ist eine App
    /// offen, weicht das Feld links und rechts neben das Fenster und seine Ornamente aus (flaechen: Fenster, Werkzeugleiste, Griff, Schliessen, Karte), statt dahinter
    /// zurückzuweichen. Die Symbole links der Mitte dieser Flächen (im Blick vom Auge) drehen gemeinsam um die senkrechte Achse durch das Auge nach links, die übrigen
    /// nach rechts, je so weit, dass das innerste im Blick <see cref="SeitlichRandM"/> neben der äussersten Kante steht. Weil sie um das Auge drehen, bleibt ihr Abstand
    /// zum Auge – das Feld bleibt in Griffweite wie vorher –, ebenso die Höhe und die Anordnung je Seite. Kein Symbol überdeckt im Blick eine der Flächen.
    /// mitten = Symbolmitten, radius = halber Durchmesser, k = 0 … 1 (weich hin und zurück; Gieren beim Holen = Richtung des Felds).
    /// </summary>
    public static List<Vec3> Seitlich(IReadOnlyList<Vec3> mitten, Vec3 auge, double blickGieren, IReadOnlyList<TabletPose> flaechen, double radius, double k)
    {
        if (flaechen.Count == 0 || mitten.Count == 0) return [.. mitten];
        var vor = new Vec3(Math.Cos(blickGieren), 0, -Math.Sin(blickGieren));
        var rechts = HebeGeste.Rechts(blickGieren);
        double Winkel(Vec3 p) { var v = p - auge; return Math.Atan2(v.Skalar(rechts), v.Skalar(vor)); }   // waagrecht im Blick, rechts positiv
        double links0 = double.PositiveInfinity, rechts0 = double.NegativeInfinity;
        foreach (var e in flaechen.SelectMany(Glas.Ecken)) { var w = Winkel(e); links0 = Math.Min(links0, w); rechts0 = Math.Max(rechts0, w); }
        var mitte = (links0 + rechts0) / 2;
        double nachLinks = 0, nachRechts = 0;
        var seite = new int[mitten.Count];
        for (int i = 0; i < mitten.Count; i++)
        {
            var v = mitten[i] - auge;
            var waag = Math.Sqrt(v.X * v.X + v.Z * v.Z);
            var rand = Math.Asin(Math.Clamp((radius + SeitlichRandM) / Math.Max(0.1, waag), 0, 1));   // Rand als Winkel in diesem Abstand
            var w = Winkel(mitten[i]);
            if (w < mitte) { seite[i] = -1; nachLinks = Math.Max(nachLinks, w - (links0 - rand)); }
            else { seite[i] = 1; nachRechts = Math.Max(nachRechts, rechts0 + rand - w); }
        }
        var kk = Math.Clamp(k, 0, 1);
        return [.. mitten.Select((p, i) => DrehenUmAuge(p, auge, (seite[i] < 0 ? -nachLinks : nachRechts) * kk))];
    }

    /// <summary>Punkt p um die senkrechte Achse durch das Auge drehen (Winkel im Bogenmass, positiv = im Blick nach rechts, d. h. im Uhrzeigersinn von oben).</summary>
    static Vec3 DrehenUmAuge(Vec3 p, Vec3 auge, double winkel)
    {
        if (Math.Abs(winkel) < 1e-12) return p;
        var v = p - auge;
        double c = Math.Cos(winkel), s = Math.Sin(winkel);
        // Blick −z → rechts +x: eine Drehung nach rechts (im Uhrzeigersinn von oben) bringt −z nach +x
        return auge + new Vec3(v.X * c - v.Z * s, v.Y, v.X * s + v.Z * c);
    }

    /// <summary>
    /// Hineinheben: Fortschritt f (0 … 1) der Geste bzw. der Bewegung danach → Versatz nach unten (m, positiv = tiefer) und Deckkraft je Symbol; jedes steigt etwas
    /// nach dem vorigen (Reihenfolge index), weich.
    /// </summary>
    public static (double Unten, double Deckkraft) Hineinheben(double f, int index, int anzahl)
    {
        var versatz = 0.06 * index;
        var k = GlasStil.Weich((Math.Clamp(f, 0, 1) * (1 + 0.06 * Math.Max(0, anzahl - 1)) - versatz));
        return ((1 - k) * HubM, k);
    }

    /// <summary>Beim Wegwischen laufen die Symbole mit so viel des Wegs der Hand nach links (mehr als die Hand: sie «fliegen» aus dem Bild), Annahme V1a.</summary>
    public const double WischFaktor = 1.4;
    /// <summary>Nach dem Wegwischen gleiten sie noch so weit (m) nach links; ab so viel (m) seitlich sind sie ganz verblasst.</summary>
    public const double NachWischM = 0.6, VerblasstM = 0.7;

    /// <summary>
    /// Versatz der Symbole nach links beim Wegwischen (m, positiv = nach links; Christian 24.10.2026: «wischbewegung von rechts nach links»): mit dem Weg der Hand nach
    /// links (× <see cref="WischFaktor"/>), nach dem Ereignis (danach 0 … 1) zusätzlich <see cref="NachWischM"/> weiter.
    /// </summary>
    public static double NachLinks(double wegHand, double danach) => Math.Max(0, wegHand) * WischFaktor + NachWischM * Math.Clamp(danach, 0, 1);

    /// <summary>Deckkraft nach dem Versatz zur Seite (1 = am Platz, 0 = ganz aus dem Bild gewischt).</summary>
    public static double SeitlichDeckkraft(double seitlich) => 1 - Math.Clamp(Math.Abs(seitlich) / VerblasstM, 0, 1);

    /// <summary>Breite eines Tooltips (m) für den Text (Schrift «klein», Rand je Seite 12 pt).</summary>
    public static double TooltipBreite(string text, GlasMasse m) => text.Length * m.Schrift(GlasStil.KleinPt) * 0.6 + m.M(24);

    /// <summary>Lage des Tooltips über dem Ziel, 4 mm davor (Befund Prüfbilder 02: unter dem Ziel verdeckte ihn die zeigende Hand).</summary>
    public static TabletPose TooltipLage(TabletPose ziel, string text, GlasMasse m)
    {
        double h = m.M(TooltipHoehePt), b = TooltipBreite(text, m);
        return ziel with { Mitte = ziel.Mitte + ziel.Oben * (ziel.H / 2 + m.M(TooltipLueckePt) + h / 2) + ziel.Normale * 0.004, B = b, H = h };
    }
}

/// <summary>Zustand des Zeigers einer Hand.</summary>
public sealed class ZeigerZustand
{
    internal int? Ziel;
    internal double Seit;
}

/// <summary>Bild des Zeigers: Ziel (Index), Punkt auf dem Ziel, Anfang der Linie (Fingerspitze), Nähe 0 … 1, Deckkraft des Tooltips 0 … 1.</summary>
public sealed record ZeigerBild(int Index, Vec3 Punkt, Vec3 Von, double Naehe, double Tooltip);

public static class Zeiger
{
    /// <summary>Ab so nah (m, vor der Fläche) zeigt der Zeiger, Annahme V1a.</summary>
    public const double NaeheM = 0.12;
    /// <summary>Seitlich darf die Spitze so weit neben dem Ziel sein (m).</summary>
    public const double RandM = 0.015;
    /// <summary>Einblenden des Tooltips (s).</summary>
    public const double EinblendenS = 0.15;
    /// <summary>Gestreckt: die Glieder des Zeigefingers weichen höchstens so weit voneinander ab (Grad), Annahme V1a.</summary>
    public const double GestrecktGrad = 35;

    /// <summary>Ist der Zeigefinger gestreckt (Grundglied und Spitze in einer Richtung)?</summary>
    public static bool ZeigefingerGestreckt(IReadOnlyDictionary<string, Vec3>? g)
    {
        if (g is null || !g.TryGetValue("index-finger-phalanx-proximal", out var a) || !g.TryGetValue("index-finger-phalanx-intermediate", out var b)
            || !g.TryGetValue("index-finger-phalanx-distal", out var c) || !g.TryGetValue("index-finger-tip", out var d)) return false;
        var u = b - a; var w = d - c;
        if (u.Laenge < 1e-6 || w.Laenge < 1e-6) return false;
        return u.Einheit().Skalar(w.Einheit()) >= Math.Cos(GestrecktGrad * Math.PI / 180);
    }

    /// <summary>
    /// Ein Schritt je Bild und Hand: spitze = Zeigefingerspitze (null = keine Hand), gestreckt = Zeigefinger gestreckt, ziele = Symbole bzw. Knöpfe; strahl = Zielstrahl
    /// der Hand (wenn der Finger nicht nah ist). Das nächste Ziel, vor dessen Fläche die Spitze höchstens <see cref="NaeheM"/> liegt (seitlich höchstens RandM daneben),
    /// sonst das erste, das der Strahl trifft. Der Tooltip erscheint nach tooltipS am selben Ziel.
    /// </summary>
    public static ZeigerBild? Schritt(ZeigerZustand z, double t, Vec3? spitze, bool gestreckt, IReadOnlyList<TabletPose> ziele, double tooltipS, (Vec3 U, Vec3 R)? strahl = null)
    {
        int? best = null;
        Vec3 punkt = default, von = default;
        double naehe = 0, bestD = double.PositiveInfinity;
        if (spitze is { } s && gestreckt)
            for (int i = 0; i < ziele.Count; i++)
            {
                var p = ziele[i];
                var d = (s - p.Mitte).Skalar(p.Normale);
                if (d < -0.02 || d > NaeheM) continue;
                var x = (s - p.Mitte).Skalar(p.Rechts);
                var y = (s - p.Mitte).Skalar(p.Oben);
                if (Math.Abs(x) > p.B / 2 + RandM || Math.Abs(y) > p.H / 2 + RandM) continue;
                if (d >= bestD) continue;
                bestD = d;
                best = i;
                punkt = p.Mitte + p.Rechts * Math.Clamp(x, -p.B / 2, p.B / 2) + p.Oben * Math.Clamp(y, -p.H / 2, p.H / 2) + p.Normale * 0.003;
                von = s;
                naehe = 1 - Math.Clamp(Math.Max(0, d) / NaeheM, 0, 1);
            }
        if (best is null && strahl is { } st && Glas.Strahl(ziele, st.U, st.R, 0.005) is { } tr)
        {
            best = tr.Index;
            punkt = st.U + st.R * tr.T;   // T in Einheiten von R (Glas.Strahl)
            von = st.U;
            naehe = 1;
        }
        if (best != z.Ziel) { z.Ziel = best; z.Seit = t; }
        if (best is not int b) return null;
        return new ZeigerBild(b, punkt, von, naehe, GlasStil.Weich((t - z.Seit - tooltipS) / EinblendenS));
    }
}
