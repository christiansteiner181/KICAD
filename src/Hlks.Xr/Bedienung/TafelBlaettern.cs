// Quest App V1d (Christian 24.10.2026): «eine swap bewegung mit der rechten oder linken hand soll in einem aktiven fenster blättern.» Rein, ohne StereoKit.
// Ein Wisch ohne Pinch mit der rechten oder linken Hand blättert in der aktiven Glas-App wie auf einem Tablet – der ORT entscheidet, damit er nicht mit dem
// Hineinziehen (links → rechts) und dem Wegwischen (rechts → links) im freien Raum zusammenstösst:
//   - Auf der Tafel: beginnt der Wisch, während ein Teil der Hand (Spitzen von Zeige- und Mittelfinger, Handfläche) höchstens «tafel.blaettern_abstand_m» vor der
//     Fläche des Fensters (innerhalb seines Umrisses, 1 cm Rand) liegt, ist es Blättern – und bleibt es bis zum Ende, auch wenn die Hand die Fläche verlässt (Fang).
//     links/rechts = Seite vor bzw. zurück (nach links gewischt = nächste Seite, wie Pinch + Wisch P5a), hoch/runter = die Liste rollen (nach oben = weiter); in den
//     Apps blättern Listen seitenweise, hoch/runter wechselt darum ebenfalls die Seite.
//   - Abgrenzung zum Antippen: erst ab «tafel.blaettern_weg_m» in der Ebene der Tafel (in höchstens ZeitS) ist es ein Wisch; ab AnsatzM seitlich «läuft» er (die Hand
//     tippt dann keine Knöpfe mehr an, die Seite gleitet mit). Ein Stoss in die Fläche bewegt sich kaum seitlich.
//   - Während ein Blättern läuft bzw. eine Hand auf der Tafel liegt, zählt diese Hand weder für das Wegwischen noch für das Hineinziehen (die App gibt sie dort nicht
//     weiter). Ein Wisch, der im freien Raum beginnt, behält seine Bedeutung, auch wenn er über die Tafel streicht.
//   - Ende eines Strichs: die Hand ist fort, pincht, bleibt RuheS stehen oder zieht sich mehr als WegVonTafelM von der Fläche zurück. Je Strich ein Ereignis; die
//     Gegenrichtung zählt danach SperreS lang nicht (das Zurückholen der Hand blättert nicht zurück).
//   - Der PC-Bildschirm (XR-I) ist keine Tafel: dort blättert nichts (er wird mit Maus und Tastatur des PCs bedient).
// Alle Zahlen sind Annahmen V1d (Stellschrauben «tafel.blaettern_*», sonst benannte Konstanten).

namespace Hlks.Xr.Bedienung;

/// <summary>Zustand des Blätterns einer Hand.</summary>
public sealed class TafelBlaetternZustand
{
    internal bool An, Lauf, Fertig;
    internal string? Punkt;                 // welches Gelenk zählt (das zur Tafel nächste beim Beginn)
    internal readonly List<(double T, double X, double Y, double Abstand)> Spur = [];
    internal double RuhigSeit;
    internal readonly List<(double T, double X, double Y)> Verlauf = [];
    internal bool Fremd;
    internal WischRichtung? Letzte;
    internal double LetzteT = double.NegativeInfinity;

    /// <summary>Läuft gerade ein Blättern (die Hand gehört der Tafel)?</summary>
    public bool Laeuft => Lauf;
    /// <summary>Liegt die Hand auf der Tafel bzw. läuft ein Strich, der dort begann?</summary>
    public bool AnTafel => An;
}

/// <summary>Stand eines Bilds: Ereignis (Richtung, einmal je Strich), Versatz der Seite in der Ebene der Tafel (m, x rechts, y oben; folgt der Hand), läuft.</summary>
public readonly record struct BlaetternStand(WischRichtung? Ereignis, double VersatzX, double VersatzY, bool Laeuft, bool AnTafel)
{
    public static readonly BlaetternStand Nichts = new(null, 0, 0, false, false);
}

public static class TafelBlaettern
{
    /// <summary>Ab so viel Weg (m) in der Ebene «läuft» das Blättern (Annahme V1d).</summary>
    public const double AnsatzM = 0.02;
    /// <summary>Der Mindestweg muss in so viel Zeit (s) zurückgelegt sein (Annahme V1d).</summary>
    public const double ZeitS = 0.8;
    /// <summary>Ein Strich endet nach so viel Ruhe (s; weniger als 1 cm Bewegung), Annahme V1d.</summary>
    public const double RuheS = 0.35;
    /// <summary>Ein Strich endet, wenn sich die Hand so weit (m) von der Fläche zurückzieht, Annahme V1d.</summary>
    public const double WegVonTafelM = 0.15;
    /// <summary>Die Gegenrichtung zählt nach einem Ereignis so lange (s) nicht (wie «begehen.drehen_sperre_s» beim Pinch + Wisch), Annahme V1d.</summary>
    public const double SperreS = 0.8;
    /// <summary>Hinter der Fläche zählt eine Hand noch bis so weit (m; man greift leicht hinein).</summary>
    public const double HinterM = 0.03;
    /// <summary>Waagrecht bzw. senkrecht: die Hauptrichtung muss mindestens so viel des Wegs ausmachen (cos 35°), Annahme V1d.</summary>
    public const double RichtungAnteil = 0.82;

    /// <summary>Vorlauf (s): so lange vor dem Ankommen auf der Tafel wird geprüft, ob die Hand schon seitlich wischt.</summary>
    public const double VorlaufS = 0.2;
    /// <summary>Mehr als so viel (m) seitlich im Vorlauf: der Wisch begann im freien Raum (Wegwischen bzw. Hineinziehen behalten ihre Bedeutung), Annahme V1d.</summary>
    public const double FremdM = 0.03;

    static readonly string[] Punkte = ["index-finger-tip", "middle-finger-tip", "palm"];

    /// <summary>Grösste seitliche Strecke im Verlauf (vom letzten Punkt aus).</summary>
    static double Seitlich(List<(double T, double X, double Y)> v)
    {
        if (v.Count < 2) return 0;
        var l = v[^1];
        var m = 0.0;
        foreach (var p in v) m = Math.Max(m, Math.Sqrt((p.X - l.X) * (p.X - l.X) + (p.Y - l.Y) * (p.Y - l.Y)));
        return m;
    }

    /// <summary>Lage eines Gelenks bzw. der Handfläche (Mitte) in der Hand.</summary>
    static Vec3? Gelenk(IReadOnlyDictionary<string, Vec3> hand, string name) =>
        name == "palm" ? XrTabletLogik.HandflaechenMitte(hand) : hand.TryGetValue(name, out var p) ? p : null;

    /// <summary>Lage eines Punkts gegen die Tafel: x, y in ihrer Ebene (m, von der Mitte), Abstand vor der Fläche (m; positiv = zum Auge hin), innerhalb des Umrisses.</summary>
    public static (double X, double Y, double Abstand, bool Innen) InEbene(Vec3 p, TabletPose tafel, double rand = 0.01)
    {
        var w = p - tafel.Mitte;
        double x = w.Skalar(tafel.Rechts), y = w.Skalar(tafel.Oben), d = w.Skalar(tafel.Normale);
        return (x, y, d, Math.Abs(x) <= tafel.B / 2 + rand && Math.Abs(y) <= tafel.H / 2 + rand);
    }

    /// <summary>Liegt ein Teil der Hand auf der Tafel (höchstens abstand davor, HinterM dahinter, im Umriss)? Gibt das nächste Gelenk zurück, sonst null.</summary>
    public static string? AufTafel(IReadOnlyDictionary<string, Vec3>? hand, TabletPose? tafel, double abstand)
    {
        if (hand is null || tafel is not { } f) return null;
        string? best = null;
        var bestD = double.PositiveInfinity;
        foreach (var n in Punkte)
        {
            if (Gelenk(hand, n) is not { } p) continue;
            var (_, _, d, innen) = InEbene(p, f);
            if (!innen || d > abstand || d < -HinterM) continue;
            if (Math.Abs(d) < bestD) { bestD = Math.Abs(d); best = n; }
        }
        return best;
    }

    /// <summary>
    /// Ein Schritt je Bild und Hand (siehe Dateikopf): hand = Gelenke (null = nicht erkannt), pinch, tafel = Fläche der aktiven Glas-App (null = keine offen), e =
    /// Stellschrauben. Ereignis: Richtung des Wischs (Links = nächste Seite, Rechts = vorige, Hoch = weiter, Runter = zurück).
    /// </summary>
    public static BlaetternStand Schritt(TafelBlaetternZustand z, double t, IReadOnlyDictionary<string, Vec3>? hand, bool pinch, TabletPose? tafel, TafelEinstellungen? e = null)
    {
        e ??= TafelEinstellungen.Vorgabe;
        if (hand is null || pinch || tafel is not { } f) { Ende(z); z.Verlauf.Clear(); z.Fremd = false; return BlaetternStand.Nichts; }
        // Verlauf der Zeigefingerspitze in der Ebene der Tafel (auch ausserhalb): kommt die Hand schon seitlich in Bewegung auf die Tafel, begann der Wisch im freien Raum
        if (Gelenk(hand, "index-finger-tip") is { } sp)
        {
            var (vx0, vy0, _, _) = InEbene(sp, f);
            z.Verlauf.Add((t, vx0, vy0));
            while (z.Verlauf.Count > 0 && z.Verlauf[0].T < t - VorlaufS - 1e-9) z.Verlauf.RemoveAt(0);
        }
        if (!z.An)
        {
            // beginnt nur auf der Tafel – und nur, wenn die Hand dort ankommt, ohne schon zu wischen
            if (AufTafel(hand, f, e.BlaetternAbstandM) is not { } punkt) { z.Fremd = false; return BlaetternStand.Nichts; }
            var seitlich = Seitlich(z.Verlauf);
            if (z.Fremd && seitlich < 0.01) z.Fremd = false;   // zur Ruhe gekommen: ab jetzt darf ein Strich beginnen
            else if (z.Fremd || seitlich > FremdM) { z.Fremd = true; return BlaetternStand.Nichts; }
            z.An = true;
            z.Lauf = false;
            z.Fertig = false;
            z.Punkt = punkt;
            z.Spur.Clear();
            z.RuhigSeit = t;
        }
        if (Gelenk(hand, z.Punkt!) is not { } q) { Ende(z); return BlaetternStand.Nichts; }
        var (x, y, d, _) = InEbene(q, f, 1);
        if (d > WegVonTafelM) { Ende(z); return BlaetternStand.Nichts; }
        if (z.Spur.Count > 0)
        {
            var l = z.Spur[^1];
            if (Math.Sqrt((x - l.X) * (x - l.X) + (y - l.Y) * (y - l.Y)) > 0.01) z.RuhigSeit = t;
        }
        z.Spur.Add((t, x, y, d));
        if (t - z.RuhigSeit > RuheS && z.Spur.Count > 1) { Ende(z); return BlaetternStand.Nichts; }
        var s0 = z.Spur[0];
        double dx = x - s0.X, dy = y - s0.Y, weg = Math.Sqrt(dx * dx + dy * dy);
        if (weg >= AnsatzM) z.Lauf = true;
        WischRichtung? ereignis = null;
        if (!z.Fertig)
        {
            // Mindestweg im Zeitfenster: vom ältesten Punkt, der höchstens ZeitS alt ist
            var a = z.Spur.First(p => p.T >= t - ZeitS - 1e-9);
            double fx = x - a.X, fy = y - a.Y, fw = Math.Sqrt(fx * fx + fy * fy);
            if (fw >= e.BlaetternWegM)
            {
                WischRichtung? r = Math.Abs(fx) >= RichtungAnteil * fw ? (fx < 0 ? WischRichtung.Links : WischRichtung.Rechts)
                    : Math.Abs(fy) >= RichtungAnteil * fw ? (fy > 0 ? WischRichtung.Hoch : WischRichtung.Runter) : null;
                if (r is { } rr && !(z.Letzte is { } lr && Gegen(lr, rr) && t - z.LetzteT < SperreS))
                {
                    ereignis = rr;
                    z.Fertig = true;
                    z.Letzte = rr;
                    z.LetzteT = t;
                }
            }
        }
        // die Seite gleitet mit der Hand (nur entlang der Hauptrichtung, höchstens die halbe Tafel)
        var waag = Math.Abs(dx) >= Math.Abs(dy);
        var vx = z.Lauf && waag ? Math.Clamp(dx, -f.B / 2, f.B / 2) : 0;
        var vy = z.Lauf && !waag ? Math.Clamp(dy, -f.H / 2, f.H / 2) : 0;
        return new BlaetternStand(ereignis, vx, vy, z.Lauf, true);
    }

    static bool Gegen(WischRichtung a, WischRichtung b) =>
        (a, b) is (WischRichtung.Links, WischRichtung.Rechts) or (WischRichtung.Rechts, WischRichtung.Links) or (WischRichtung.Hoch, WischRichtung.Runter) or (WischRichtung.Runter, WischRichtung.Hoch);

    static void Ende(TafelBlaetternZustand z)
    {
        z.An = false;
        z.Lauf = false;
        z.Fertig = false;
        z.Punkt = null;
        z.Spur.Clear();
    }

    /// <summary>Seite nach dem Ereignis: Links bzw. Hoch = nächste (+1), Rechts bzw. Runter = vorige (−1).</summary>
    public static int SeitenSchritt(WischRichtung r) => r is WischRichtung.Links or WischRichtung.Hoch ? 1 : -1;
}
