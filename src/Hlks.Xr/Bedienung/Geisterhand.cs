// Quest App P5a: Geisterhände (Ergänzung «Ins Interface-Labor», Christian 23.10.2026: «Zögert man, zeigen Geisterhände die passende Geste»).
// Zögern: die Fingerspitze liegt ZeitS lang ruhig (Umkreis RuheM) über demselben Ziel (Kachel, Eintrag, offene App, Griff am Tisch), ohne dass etwas ausgelöst
// wird. Dann führt eine halbdurchsichtige Hand die passende Geste vor – zweimal hintereinander, aus der Form der eigenen Hand: Antippen (Zeigefinger stösst
// in die Kachel), Blättern (Pinch + Wisch nach links), Greifen (Daumen und Zeigefinger schliessen sich am Griff); V1a: Heben (beide Handflächen oben) und
// Wegwischen (rechte Hand offen, Handfläche nach links, von rechts nach links; Christian 24.10.2026 – Pinch + Wisch nach vorn entfällt).
// Dieselbe Geste über demselben Ziel frühestens nach WiederholenS wieder. Rein, ohne StereoKit; die App zeichnet Gelenke und Knochen.
// Alle Zeiten und Wege sind Annahmen P5a (Labor).
// V1f: Pistole (die Hand zielt mit gestrecktem Zeigefinger, der Daumen oben klappt an die Hand und wieder hoch – Pistolengeste, Variante «ausloeser» = «pistole»), Faust
// (am Punkt des Tisches mit der ganzen Hand zugreifen) und FaustKreisen (die Faust führt den Punkt «Drehen» auf dem Bogen).

namespace Hlks.Xr.Bedienung;

/// <summary>Geste, die eine Geisterhand vorführt.</summary>
public enum GeisterGeste { Antippen, Blaettern, Wegwischen, Greifen, Heben, Zoomen, Drehknopf, Kreisen, Pistole, Faust, FaustKreisen }

/// <summary>Zustand der Zögern-Erkennung einer Hand.</summary>
public sealed class ZoegernZustand
{
    internal string? Ziel;
    internal double Seit;
    internal Vec3 Ort;
    internal string? Gemeldet;
    internal double GemeldetT = double.NegativeInfinity;
}

/// <summary>Bild einer Geisterhand: Gelenke (Namen wie WebXR) und Deckkraft (0 … 1).</summary>
public sealed record GeisterBild(IReadOnlyDictionary<string, Vec3> Gelenke, double Deckkraft);

public static class Geisterhand
{
    /// <summary>So lange ruhig über demselben Ziel gilt als Zögern (s), Annahme P5a.</summary>
    public const double ZoegernS = 1.5;
    /// <summary>Umkreis der Ruhe (m), Annahme P5a.</summary>
    public const double RuheM = 0.03;
    /// <summary>Dieselbe Vorführung über demselben Ziel frühestens wieder nach (s), Annahme P5a.</summary>
    public const double WiederholenS = 6;
    /// <summary>Dauer einer Vorführung (zwei Durchgänge), s.</summary>
    public const double DauerS = 1.8;
    /// <summary>Höchste Deckkraft der Geisterhand.</summary>
    public const double DeckkraftMax = 0.45;
    /// <summary>Antippen: die Spitze startet so weit vor der Kachel (m).</summary>
    public const double AnlaufM = 0.08;
    /// <summary>Blättern und Wegwischen: Weg der Hand (m), über dem Mindestweg eines Wischs.</summary>
    public const double WegM = 0.22;
    /// <summary>Blättern läuft so weit vor der Fläche (m) – bis V1d mit Pinch.</summary>
    public const double VorFlaecheM = 0.07;
    /// <summary>V1d: Blättern auf der Tafel (ohne Pinch) läuft so weit vor der Fläche (m), innerhalb von «tafel.blaettern_abstand_m».</summary>
    public const double BlaetternVorM = 0.02;
    /// <summary>V1e: Hebegeste und Wegwischen zeigen nur den kurzen Anstoss (Christian 24.10.2026: «nur einen anstoss»): erst stellen (AnstossVor des Durchgangs), dann so weit (m) in AnstossAnteil,
    /// dann steht die Hand (über «gesten.anstoss_weg_m» 6 cm).</summary>
    public const double AnstossM = 0.09, AnstossVor = 0.15, AnstossAnteil = 0.15;

    /// <summary>Vektor v um die Achse a (Einheit) um den Winkel w (rad, rechte Hand) drehen (Rodrigues).</summary>
    static Vec3 Drehen(Vec3 v, Vec3 a, double w)
    {
        double c = Math.Cos(w), s = Math.Sin(w);
        return v * c + a.Kreuz(v) * s + a * (a.Skalar(v) * (1 - c));
    }

    /// <summary>
    /// Ein Schritt je Bild und Hand: ort = Fingerspitze, ziel = Schlüssel des Ziels unter der Hand (null = keines), aktion = in diesem Bild ausgelöst oder Pinch.
    /// Gibt das Ziel zurück, wenn jetzt gezögert wird (einmal je Ruhe; dasselbe Ziel frühestens nach <see cref="WiederholenS"/> wieder), sonst null.
    /// </summary>
    public static string? Zoegern(ZoegernZustand z, double t, Vec3? ort, string? ziel, bool aktion)
    {
        if (ort is not { } o || ziel is null || aktion)
        {
            z.Ziel = null;
            if (aktion) z.Gemeldet = null;   // wer handelt, braucht keine Hilfe mehr
            return null;
        }
        if (ziel != z.Ziel || z.Ort.Abstand(o) > RuheM) { z.Ziel = ziel; z.Seit = t; z.Ort = o; return null; }
        if (t - z.Seit < ZoegernS - 1e-9) return null;
        if (z.Gemeldet == ziel && t - z.GemeldetT < WiederholenS) return null;
        z.Gemeldet = ziel;
        z.GemeldetT = t;
        z.Seit = t;
        return ziel;
    }

    /// <summary>Knochen der Hand als Paare von Gelenknamen (WebXR) zum Zeichnen: Handgelenk zu den Mittelhandknochen, dann jeder Finger bis zur Spitze.</summary>
    public static readonly IReadOnlyList<(string A, string B)> Knochen = BaueKnochen();

    static List<(string, string)> BaueKnochen()
    {
        var k = new List<(string, string)>();
        string[] daumen = ["thumb-metacarpal", "thumb-phalanx-proximal", "thumb-phalanx-distal", "thumb-tip"];
        k.Add(("wrist", daumen[0]));
        for (int i = 0; i + 1 < daumen.Length; i++) k.Add((daumen[i], daumen[i + 1]));
        foreach (var f in new[] { "index", "middle", "ring", "pinky" })
        {
            string[] g = [$"{f}-finger-metacarpal", $"{f}-finger-phalanx-proximal", $"{f}-finger-phalanx-intermediate", $"{f}-finger-phalanx-distal", $"{f}-finger-tip"];
            k.Add(("wrist", g[0]));
            for (int i = 0; i + 1 < g.Length; i++) k.Add((g[i], g[i + 1]));
        }
        return k;
    }

    static double Glatt(double x) { x = Math.Clamp(x, 0, 1); return x * x * (3 - 2 * x); }

    /// <summary>
    /// V1a, Befund Prüfbild 24.10.2026 («Geisterhand liegt über der Registerleiste»): Ziel für die Vorführung neben einem Bereich mit Bedienelementen statt auf ihm –
    /// dieselbe Höhe und Tiefe wie das Ziel, aber luft (m) links vom linken Rand des Bereichs. Mit einer linken Hand (<see cref="Vorlage"/>: Finger nach links, der
    /// Daumen 4.5 cm rechts der Spitze) liegt dann die ganze Hand links daneben (Vorgabe 6.5 cm, Annahme V1a).
    /// </summary>
    public static TabletPose Neben(TabletPose ziel, TabletPose bereich, double luft = 0.065)
    {
        var x = (bereich.Mitte - ziel.Mitte).Skalar(ziel.Rechts) - bereich.B / 2 - luft;
        return ziel with { Mitte = ziel.Mitte + ziel.Rechts * x };
    }

    /// <summary>
    /// Eine einfache Hand (25 Gelenke, Namen wie WebXR) für Vorführungen ohne eigene Hand (Prüflauf am PC): der Zeigefinger gestreckt, die Spitze an spitze, zeigt in
    /// das Ziel (gegen dessen Normale), die übrigen Finger eingerollt, der Daumen seitlich. links = linke Hand (die übrigen Finger liegen links vom Zeigefinger).
    /// Masse einer mittleren Hand, Annahme V1a.
    /// </summary>
    public static Dictionary<string, Vec3> Vorlage(Vec3 spitze, TabletPose ziel, bool links = true)
    {
        var n = ziel.Normale;
        var o = ziel.Oben;
        var r = ziel.Rechts * (links ? -1 : 1);   // Richtung zu den übrigen Fingern
        var h = new Dictionary<string, Vec3>(StringComparer.Ordinal);
        Vec3 P(double vor, double seite, double hoch) => spitze + n * vor + r * seite + o * hoch;
        h["wrist"] = P(0.165, 0.03, -0.025);
        h["thumb-metacarpal"] = P(0.15, -0.005, -0.03);
        h["thumb-phalanx-proximal"] = P(0.12, -0.025, -0.03);
        h["thumb-phalanx-distal"] = P(0.095, -0.035, -0.028);
        h["thumb-tip"] = P(0.07, -0.04, -0.025);
        h["index-finger-metacarpal"] = P(0.14, 0.008, -0.01);
        h["index-finger-phalanx-proximal"] = P(0.075, 0, 0);
        h["index-finger-phalanx-intermediate"] = P(0.045, 0, 0);
        h["index-finger-phalanx-distal"] = P(0.022, 0, 0);
        h["index-finger-tip"] = spitze;
        foreach (var (f, seite) in new[] { ("middle-finger", 0.02), ("ring-finger", 0.039), ("pinky-finger", 0.056) })
        {
            h[$"{f}-metacarpal"] = P(0.145, seite * 0.7 + 0.01, -0.012);
            h[$"{f}-phalanx-proximal"] = P(0.08, seite, -0.003);
            h[$"{f}-phalanx-intermediate"] = P(0.06, seite, -0.03);
            h[$"{f}-phalanx-distal"] = P(0.072, seite, -0.05);
            h[$"{f}-tip"] = P(0.088, seite, -0.055);
        }
        return h;
    }

    /// <summary>
    /// V1a: eine offene Hand mit der Handfläche nach oben (25 Gelenke, Namen wie WebXR), Mitte der Handfläche an mitte, Finger in Richtung vor (waagrecht), der Daumen
    /// aussen (rechte Hand: rechts). Für die Hebegeste (Geisterhand, Tests). Masse einer mittleren Hand, Annahme V1a.
    /// </summary>
    public static Dictionary<string, Vec3> VorlageOffen(Vec3 mitte, Vec3 vor, bool links)
    {
        var v = new Vec3(vor.X, 0, vor.Z).Einheit();
        var aussen = new Vec3(-v.Z, 0, v.X) * (links ? -1 : 1);   // Richtung des Daumens (rechte Hand: rechts vom Blick)
        var h = new Dictionary<string, Vec3>(StringComparer.Ordinal);
        Vec3 P(double nachVorn, double seite) => mitte + v * nachVorn + aussen * seite;
        h["wrist"] = P(-0.05, 0);
        h["thumb-metacarpal"] = P(-0.03, 0.03);
        h["thumb-phalanx-proximal"] = P(0, 0.05);
        h["thumb-phalanx-distal"] = P(0.025, 0.065);
        h["thumb-tip"] = P(0.045, 0.075);
        foreach (var (f, seite) in new[] { ("index-finger", 0.024), ("middle-finger", 0.006), ("ring-finger", -0.012), ("pinky-finger", -0.028) })
        {
            h[$"{f}-metacarpal"] = P(-0.02, seite * 0.7);
            h[$"{f}-phalanx-proximal"] = P(0.045, seite);
            h[$"{f}-phalanx-intermediate"] = P(0.085, seite);
            h[$"{f}-phalanx-distal"] = P(0.11, seite);
            h[$"{f}-tip"] = P(0.132, seite);
        }
        return h;
    }

    /// <summary>
    /// V1a: eine flache, offene Hand (25 Gelenke, Namen wie WebXR), Mitte der Handfläche an mitte, die Handfläche zeigt in Richtung nach, die Finger in Richtung oben
    /// (senkrecht zu nach); der Daumen seitlich (rechte Hand: links vom Blick in Richtung nach). Für die Berührung eines Symbols mit der Handfläche (Prüflauf, Tests).
    /// Masse einer mittleren Hand, Annahme V1a.
    /// </summary>
    public static Dictionary<string, Vec3> VorlageFlach(Vec3 mitte, Vec3 nach, Vec3 oben, bool links)
    {
        var n = nach.Einheit();
        var o = (oben - n * oben.Skalar(n)).Einheit();
        var rechts = n.Kreuz(o);   // rechts, von hinten in Richtung nach gesehen
        var daumen = rechts * (links ? 1 : -1);
        var h = new Dictionary<string, Vec3>(StringComparer.Ordinal);
        Vec3 P(double hoch, double seite) => mitte + o * hoch + daumen * seite - n * 0.01;   // die Gelenke liegen 1 cm hinter der Handfläche
        h["wrist"] = P(-0.07, 0);
        h["thumb-metacarpal"] = P(-0.05, 0.025);
        h["thumb-phalanx-proximal"] = P(-0.025, 0.05);
        h["thumb-phalanx-distal"] = P(0.0, 0.065);
        h["thumb-tip"] = P(0.02, 0.075);
        foreach (var (f, seite) in new[] { ("index-finger", 0.028), ("middle-finger", 0.009), ("ring-finger", -0.01), ("pinky-finger", -0.028) })
        {
            h[$"{f}-metacarpal"] = P(-0.045, seite * 0.6);
            h[$"{f}-phalanx-proximal"] = P(0.025, seite);
            h[$"{f}-phalanx-intermediate"] = P(0.065, seite * 1.05);
            h[$"{f}-phalanx-distal"] = P(0.09, seite * 1.08);
            h[$"{f}-tip"] = P(0.11, seite * 1.1);
        }
        return h;
    }

    /// <summary>
    /// V1f: die Hand in der Pistolengeste (25 Gelenke, Namen wie WebXR): Zeigefinger gestreckt mit der Spitze an spitze in Richtung vor, Mittel-, Ring- und kleiner Finger
    /// eingerollt unter dem Zeigefinger, der Daumen oben (Richtung oben). links = linke Hand (die Handfläche zeigt dann nach rechts). Masse einer mittleren Hand, Annahme V1f.
    /// </summary>
    public static Dictionary<string, Vec3> VorlagePistole(Vec3 spitze, Vec3 vor, Vec3 oben, bool links)
    {
        var f = vor.Einheit();
        var o = (oben - f * oben.Skalar(f)).Einheit();
        var r = f.Kreuz(o);                       // rechts vom Zeigefinger
        var pn = r * (links ? 1 : -1);            // Normale der Handfläche (rechte Hand: nach links)
        var h = new Dictionary<string, Vec3>(StringComparer.Ordinal);
        var k = spitze - f * 0.075;               // Grundgelenk des Zeigefingers
        h["index-finger-tip"] = spitze;
        h["index-finger-phalanx-distal"] = spitze - f * 0.02;
        h["index-finger-phalanx-intermediate"] = spitze - f * 0.044;
        h["index-finger-phalanx-proximal"] = k;
        h["index-finger-metacarpal"] = k - f * 0.065 - o * 0.005;
        h["wrist"] = k - f * 0.09 - o * 0.03;
        foreach (var (name, unten) in new[] { ("middle-finger", 0.02), ("ring-finger", 0.039), ("pinky-finger", 0.056) })
        {
            var g = k - o * unten - f * (unten * 0.15);
            h[$"{name}-metacarpal"] = g - f * 0.065 + o * (unten * 0.3);
            h[$"{name}-phalanx-proximal"] = g;
            var i = g + f * 0.012 + pn * 0.036;
            var d = i - f * 0.02 + pn * 0.008;
            h[$"{name}-phalanx-intermediate"] = i;
            h[$"{name}-phalanx-distal"] = d;
            h[$"{name}-tip"] = d - f * 0.016 - pn * 0.006;
        }
        // Daumen oben: von der Wurzel an der Seite der Hand schräg nach oben und vorn
        var w = h["wrist"];
        h["thumb-metacarpal"] = w + f * 0.03 + pn * 0.012 + o * 0.02;
        h["thumb-phalanx-proximal"] = k - f * 0.035 + pn * 0.012 + o * 0.03;
        h["thumb-phalanx-distal"] = k - f * 0.02 + pn * 0.01 + o * 0.055;
        h["thumb-tip"] = k - f * 0.005 + pn * 0.008 + o * 0.075;
        return h;
    }

    /// <summary>V1f: wo die Daumenspitze beim Abdrücken liegt – an der Seite des Mittelfingers zum Zeigefinger hin (aus den Gelenken der Hand).</summary>
    public static Vec3? DaumenAngelegt(IReadOnlyDictionary<string, Vec3> hand) =>
        hand.TryGetValue("middle-finger-phalanx-proximal", out var mp) && hand.TryGetValue("middle-finger-phalanx-intermediate", out var mi)
        && hand.TryGetValue("index-finger-phalanx-proximal", out var ip)
            ? Vec3.Mitte(mp, mi) + (ip - mp) * 0.5 : null;

    /// <summary>
    /// V1f: die Hand zur Faust schliessen (zu 0 … 1): die vier Finger beugen in den Gelenken um 80°, 100°, 60° zur Handfläche, der Daumen legt sich darüber. Die Seite der
    /// Handfläche wählt zu: sie zeigt zu richtung (bei einem Punkt auf dem Tisch nach unten).
    /// </summary>
    public static Dictionary<string, Vec3> ZurFaust(IReadOnlyDictionary<string, Vec3> hand, double zu, Vec3 richtung)
    {
        var neu = hand.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        if (!hand.TryGetValue("wrist", out var w) || !hand.TryGetValue("index-finger-metacarpal", out var im) || !hand.TryGetValue("pinky-finger-metacarpal", out var pm)) return neu;
        var pn = (im - w).Kreuz(pm - w).Einheit();
        if (pn.Skalar(richtung) < 0) pn = pn * -1;
        zu = Math.Clamp(zu, 0, 1);
        double[] beuge = [80 * Num.Grad, 100 * Num.Grad, 60 * Num.Grad];
        foreach (var name in new[] { "index-finger", "middle-finger", "ring-finger", "pinky-finger" })
        {
            string[] g = [$"{name}-phalanx-proximal", $"{name}-phalanx-intermediate", $"{name}-phalanx-distal", $"{name}-tip"];
            if (!g.All(hand.ContainsKey)) continue;
            var p = hand[g[0]];
            var f0 = hand[g[1]] - p;
            var fe = (f0 - pn * f0.Skalar(pn)).Einheit();
            if (fe.Laenge < 0.5) continue;
            var phi = 0.0;
            for (int j = 1; j < 4; j++)
            {
                var l = hand[g[j]].Abstand(hand[g[j - 1]]);
                phi += zu * beuge[j - 1];
                p += (fe * Math.Cos(phi) + pn * Math.Sin(phi)) * l;
                neu[g[j]] = p;
            }
        }
        if (hand.TryGetValue("thumb-tip", out var dt) && neu.TryGetValue("middle-finger-phalanx-intermediate", out var mi) && neu.TryGetValue("index-finger-phalanx-intermediate", out var ii))
        {
            var ziel = Vec3.Mitte(mi, ii) + pn * 0.012;
            neu["thumb-tip"] = dt + (ziel - dt) * (0.8 * zu);
            if (hand.TryGetValue("thumb-phalanx-distal", out var dd)) neu["thumb-phalanx-distal"] = dd + (Vec3.Mitte(dd, ziel) - dd) * (0.8 * zu);
        }
        return neu;
    }

    /// <summary>
    /// Die Geisterhand des Wegwischens ist so weit (Grad) aus «Handfläche nach links» zum Blick hin gedreht (die Handfläche zeigt nach links und etwas vom Auge weg):
    /// genau nach links stünde sie auf der Kante und wäre nur ein Strich (Befund Prüfbilder 03). Kleiner als «wegwischen.handflaeche_grad» (50°), damit die Vorführung
    /// eine gültige Geste zeigt; Annahme V1a.
    /// </summary>
    public const double WischSchraegGrad = 35;

    /// <summary>
    /// V1a: die offene rechte Hand für die Vorführung des Wegwischens: wie <see cref="VorlageFlach"/>, Finger nach oben, die Handfläche nach links (links = Richtung
    /// des Wischs) und um <see cref="WischSchraegGrad"/> nach vorn (vor = Blickrichtung) gedreht, so dass man schräg auf den Handrücken sieht.
    /// </summary>
    public static Dictionary<string, Vec3> VorlageWisch(Vec3 mitte, Vec3 links, Vec3 vor)
    {
        var l = new Vec3(links.X, 0, links.Z).Einheit();
        var v = new Vec3(vor.X, 0, vor.Z).Einheit();
        var w = WischSchraegGrad * Math.PI / 180;
        return VorlageFlach(mitte, l * Math.Cos(w) + v * Math.Sin(w), new Vec3(0, 1, 0), links: false);
    }

    /// <summary>
    /// Die Geisterhand zur Zeit t (s seit Beginn der Vorführung) aus der Form der eigenen Hand (Gelenke im Raum der Brille) für das Ziel (Kachel bzw. offene App;
    /// beim Greifen die Lage des Griffs als Mitte). null ausserhalb der Vorführung oder wenn der Hand die Zeigefinger- bzw. Daumenspitze fehlt.
    /// </summary>
    public static GeisterBild? Bild(GeisterGeste geste, double t, IReadOnlyDictionary<string, Vec3> hand, TabletPose ziel)
    {
        if (t < 0 || t > DauerS) return null;
        if (!hand.TryGetValue("index-finger-tip", out var spitze) || !hand.TryGetValue("thumb-tip", out var daumen)) return null;
        var deck = DeckkraftMax * Math.Min(1, Math.Min(t / 0.15, (DauerS - t) / 0.2));
        var zyklus = DauerS / 2;
        var u = (t % zyklus) / zyklus;   // 0 … 1 je Durchgang
        var pinchPunkt = Vec3.Mitte(spitze, daumen);
        Dictionary<string, Vec3> Verschoben(Vec3 bezug, Vec3 soll, bool zu)
        {
            var d = soll - bezug;
            var neu = hand.ToDictionary(x => x.Key, x => x.Value + d, StringComparer.Ordinal);
            if (zu)
            {
                // Pinch: Daumenspitze an die Zeigefingerspitze, das letzte Daumenglied halb dorthin
                neu["thumb-tip"] = neu["index-finger-tip"] + (neu["thumb-tip"] - neu["index-finger-tip"]) * 0.15;
                if (neu.TryGetValue("thumb-phalanx-distal", out var dd)) neu["thumb-phalanx-distal"] = Vec3.Mitte(dd, neu["thumb-tip"]);
            }
            return neu;
        }
        switch (geste)
        {
            case GeisterGeste.Antippen:
            {
                // hin (erste Hälfte) und zurück (zweite Hälfte): von AnlaufM vor der Kachel bis knapp hinein
                var s = u < 0.5 ? Glatt(u / 0.5) : 1 - Glatt((u - 0.5) / 0.5);
                var soll = ziel.Mitte + ziel.Normale * (AnlaufM * (1 - s) - 0.004 * s);
                return new GeisterBild(Verschoben(spitze, soll, false), deck);
            }
            case GeisterGeste.Blaettern:
            {
                // V1d (Christian 24.10.2026: «eine swap bewegung … soll in einem aktiven fenster blättern»): die Hand ohne Pinch knapp vor der Fläche (BlaetternVorM),
                // schnell von rechts nach links (nächste Seite), dann kurz stehen – wie auf einem Tablet (bis V1d Pinch + Wisch 7 cm davor)
                var s = Glatt(u / 0.55);
                var soll = ziel.Mitte + ziel.Normale * BlaetternVorM + ziel.Rechts * (WegM / 2 - WegM * s);
                return new GeisterBild(Verschoben(spitze, soll, false), deck);
            }
            case GeisterGeste.Zoomen:
            {
                // V1d, ein Griff, Variante «zweite_hand»: die andere Hand pincht neben der Griffhand (ziel.Mitte) und zieht waagrecht auseinander (ziel.Rechts), dann stehen
                var s = Glatt(u / 0.6);
                var soll = ziel.Mitte + ziel.Rechts * (0.08 + 0.14 * s);
                return new GeisterBild(Verschoben(pinchPunkt, soll, true), deck);
            }
            case GeisterGeste.Drehknopf:
            {
                // V1d, ein Griff, Variante «drehknopf»: die Hand am Griff rollt um ihre Achse (Handgelenk → Mittelfinger) bis 60° im Uhrzeigersinn und zurück
                var s = u < 0.5 ? Glatt(u / 0.5) : 1 - Glatt((u - 0.5) / 0.5);
                var bild = Verschoben(pinchPunkt, ziel.Mitte, true);
                if (!bild.TryGetValue("wrist", out var w) || !bild.TryGetValue("middle-finger-phalanx-proximal", out var m)) return new GeisterBild(bild, deck);
                var achse = (m - w).Einheit();
                var winkel = 60 * Num.Grad * s;   // um die Achse vom Menschen weg rechtsdrehend = im Uhrzeigersinn aus seiner Sicht (EinGriff.Rollen positiv: grösser)
                var gedreht = bild.ToDictionary(x => x.Key, x => Drehen(x.Value - w, achse, winkel) + w, StringComparer.Ordinal);
                return new GeisterBild(gedreht, deck);
            }
            case GeisterGeste.Wegwischen:
            {
                // V1a, Wegwischen (Christian 24.10.2026: «wischbewegung von rechts nach links»): die offene rechte Hand, Handfläche nach links, wischt vom Ziel aus in
                // Richtung «Rechts» des Ziels (die App setzt es nach links). V1e: nur ein kurzer Anstoss (AnstossM schnell), dann steht die Hand
                var s = Glatt((u - AnstossVor) / AnstossAnteil);
                var mitte = XrTabletLogik.HandflaechenMitte(hand) ?? pinchPunkt;
                var soll = ziel.Mitte + ziel.Rechts * (AnstossM * s);
                return new GeisterBild(Verschoben(mitte, soll, false), deck * (1 - 0.3 * s));
            }
            case GeisterGeste.Heben:
            {
                // V1a, Hebegeste: die offene Hand, Handfläche nach oben, hebt zum Ziel. V1e: nur ein kurzer Anstoss nach oben (AnstossM schnell), dann steht sie
                var s = Glatt((u - AnstossVor) / AnstossAnteil);
                var mitte = XrTabletLogik.HandflaechenMitte(hand) ?? pinchPunkt;
                var soll = ziel.Mitte - ziel.Oben * (AnstossM * (1 - s));
                return new GeisterBild(Verschoben(mitte, soll, false), deck);
            }
            case GeisterGeste.Kreisen:
            {
                // V1e, Punkt «Drehen und Vergrössern»: die Hand pincht am Punkt (ziel.Mitte) und führt ihn auf einem Bogen um das Zentrum (ziel.Rechts = Richtung des
                // Bogens) etwas nach aussen (ziel.Oben = vom Zentrum weg): Drehen und Vergrössern zugleich
                var s = Glatt(u / 0.6);
                var soll = ziel.Mitte + ziel.Rechts * (0.10 * s) + ziel.Oben * (0.04 * s);
                return new GeisterBild(Verschoben(pinchPunkt, soll, true), deck);
            }
            case GeisterGeste.Pistole:
            {
                // V1f, Pistolengeste: die Hand zielt (die Spitze des Zeigefingers an ziel.Mitte), der Daumen steht oben, klappt an die Hand (abdrücken), bleibt kurz und geht
                // wieder hoch
                var bild = Verschoben(spitze, ziel.Mitte, false);
                if (DaumenAngelegt(bild) is not { } an || !bild.TryGetValue("thumb-tip", out var oben)) return new GeisterBild(bild, deck);
                var s = u < 0.45 ? 0 : u < 0.55 ? Glatt((u - 0.45) / 0.1) : u < 0.8 ? 1 : 1 - Glatt((u - 0.8) / 0.15);
                bild["thumb-tip"] = oben + (an - oben) * s;
                if (bild.TryGetValue("thumb-phalanx-distal", out var dd)) bild["thumb-phalanx-distal"] = dd + (Vec3.Mitte(dd, an) - dd) * (0.6 * s);
                return new GeisterBild(bild, deck);
            }
            case GeisterGeste.Faust:
            case GeisterGeste.FaustKreisen:
            {
                // V1f, Faustgriff am Punkt: die Hand kommt offen an den Punkt (ziel.Mitte innen in der Hand) und schliesst sich zur Faust; Faust: öffnet sich wieder; FaustKreisen:
                // geschlossen auf dem Bogen um das Zentrum (ziel.Rechts) und etwas nach aussen (ziel.Oben), wie «Kreisen»
                var mitte = XrTabletLogik.HandflaechenMitte(hand) ?? pinchPunkt;
                var richtung = ziel.Normale * -1;   // die Handfläche zeigt zum Punkt (auf dem Tisch: nach unten)
                var zu = geste == GeisterGeste.Faust ? (u < 0.5 ? Glatt(u / 0.4) : 1 - Glatt((u - 0.6) / 0.4)) : Glatt(u / 0.15);
                var weg = geste == GeisterGeste.FaustKreisen ? ziel.Rechts * (0.10 * Glatt((u - 0.15) / 0.5)) + ziel.Oben * (0.04 * Glatt((u - 0.15) / 0.5)) : Vec3.Null;
                var offen = Verschoben(mitte, ziel.Mitte + weg - richtung * 0.03, false);
                return new GeisterBild(ZurFaust(offen, zu, richtung), deck);
            }
            default:
            {
                // Greifen: die Hand an den Griff, Daumen und Zeigefinger schliessen sich (erste Hälfte), öffnen sich wieder (zweite Hälfte)
                var zu = u < 0.5 ? Glatt(u / 0.5) : 1 - Glatt((u - 0.5) / 0.5);
                var bild = Verschoben(pinchPunkt, ziel.Mitte, false);
                var zs = bild["index-finger-tip"];
                bild["thumb-tip"] = bild["thumb-tip"] + (zs - bild["thumb-tip"]) * (0.85 * zu);
                return new GeisterBild(bild, deck);
            }
        }
    }
}
