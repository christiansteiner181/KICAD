// Quest App V1f (Brillentest, Christian 24.10.2026): «pich ist zu sensibel.. hier braucht es eine andere geste. bsp. pistolengeste.» Rein, ohne StereoKit.
// Die Pistolengeste löst die Zeige-Aktionen aus (Springen, Messen, Marker setzen, Knopf- und Symbolwahl auf Distanz; Variante «ausloeser» = «pistole», Vorgabe | «pinch»):
//   - HALTUNG: Zeigefinger gestreckt (Abstand Grundgelenk → Spitze mindestens StreckAnteil der Knochenlängen), Mittel-, Ring- und kleiner Finger eingerollt (höchstens
//     RollAnteil), die Daumenspitze nicht an der Zeigefingerspitze (das wäre ein Pinch).
//   - ZIELEN: in der Haltung mit dem Daumen oben (weg von der Hand, über «pistole.abzug_aus_m»). Der Strahl geht vom Grundgelenk des Zeigefingers durch seine Spitze.
//   - ABDRÜCKEN: der Daumen klappt an die Seite der Hand – die Daumenspitze näher als «pistole.abzug_an_m» an der Seite des Zeigefingers (Mittelhand und Grundglied) bzw.
//     am Grund- und Mittelglied des Mittelfingers; gelöst erst wieder über «pistole.abzug_aus_m» (Hysterese).
//   - Robust gegen Zittern und blosses Zeigen: gezählt wird ein Abzug nur, wenn der Daumen vorher «pistole.ruhe_s» lang oben stand und der Strahl dabei ruhig war (höchstens
//     ZielRuheGrad); nach einem Abzug ruht die Geste SperreS. Es gilt der Strahl von StrahlVorS vor dem Abzug (das Zucken des Daumens verreisst das Ziel nicht). Eine Hand,
//     die in der Haltung nur zeigt oder sich bewegt, löst nie aus (Tests V1fTests); der Daumen, der beim Zeigen locker an der Hand liegt, auch nicht (er stand nie oben).
// Alle Zahlen sind Annahmen V1f (Stellschrauben «pistole.*» bzw. benannte Konstanten), keine Normwerte.

namespace Hlks.Xr.Bedienung;

/// <summary>Schwellen der Pistolengeste (Stellschrauben «pistole.*», Annahmen V1f).</summary>
public sealed record PistoleEinstellungen
{
    /// <summary>«pistole.abzug_an_m»: Daumenspitze näher als so viel (m) an der Seite der Hand = abgedrückt (Annahme V1f 0.025 m; anliegend rund 1–2 cm, die Gelenke liegen
    /// in der Mitte der Finger).</summary>
    public double AbzugAnM { get; init; } = 0.025;
    /// <summary>«pistole.abzug_aus_m»: erst über so viel (m) ist der Daumen wieder oben bzw. der Abzug gelöst (Hysterese; Annahme V1f 0.04 m).</summary>
    public double AbzugAusM { get; init; } = 0.04;
    /// <summary>«pistole.ruhe_s»: so lange (s) muss der Daumen vor dem Abzug oben stehen, ruhig gezielt (Annahme V1f 0.15 s).</summary>
    public double RuheS { get; init; } = 0.15;

    public static readonly PistoleEinstellungen Vorgabe = new();
}

/// <summary>
/// Stand eines Bilds: Haltung (die Hand hält die Pistole), zielt (Daumen oben, bereit nach der Ruhezeit), gedrückt (Abzug gehalten), abgedrückt bzw. losgelassen (nur in
/// diesem Bild), der Strahl jetzt und beim (letzten) Abzug.
/// </summary>
public readonly record struct PistoleStand(bool Haltung, bool Zielt, bool Gedrueckt, bool Abgedrueckt, bool Losgelassen, (Vec3 U, Vec3 R)? Strahl, (Vec3 U, Vec3 R)? StrahlBeimAbzug)
{
    public static readonly PistoleStand Nichts = new(false, false, false, false, false, null, null);
}

/// <summary>Zustand der Pistolengeste einer Hand.</summary>
public sealed class PistoleZustand
{
    internal double? ObenSeit;
    internal bool Gedrueckt;
    internal double SperreBis = double.NegativeInfinity;
    internal readonly List<(double T, Vec3 U, Vec3 R)> Strahlen = [];
    internal (Vec3 U, Vec3 R)? BeimAbzug;
    /// <summary>So oft abgedrückt (Protokoll).</summary>
    public int Abzuege { get; internal set; }
    /// <summary>Stand des letzten Schritts.</summary>
    public PistoleStand Stand { get; internal set; } = PistoleStand.Nichts;

    /// <summary>Alles vergessen (Hand weg, Wechsel des Auslösers).</summary>
    public void Zuruecksetzen()
    {
        ObenSeit = null;
        Gedrueckt = false;
        Strahlen.Clear();
        Stand = PistoleStand.Nichts;
    }
}

public static class Pistole
{
    /// <summary>Zeigefinger gestreckt: Abstand Grundgelenk → Spitze mindestens so viel der Knochenlängen (Annahme V1f; ganz gestreckt 1, halb gekrümmt rund 0.84).</summary>
    public const double StreckAnteil = 0.85;
    /// <summary>Übrige Finger eingerollt: Abstand Grundgelenk → Spitze höchstens so viel der Knochenlängen (Annahme V1f; eine lockere Hand liegt darüber).</summary>
    public const double RollAnteil = 0.7;
    /// <summary>Die Daumenspitze ist mindestens so weit (m) von der Zeigefingerspitze weg – sonst ist es ein Pinch (wie «pinch.schwelle_aus_m»).</summary>
    public const double PinchFreiM = 0.04;
    /// <summary>Nach einem Abzug ruht die Geste so lange (s), Annahme V1f.</summary>
    public const double SperreS = 0.3;
    /// <summary>Beim Abzug gilt der Strahl von so viel (s) davor, Annahme V1f.</summary>
    public const double StrahlVorS = 0.08;
    /// <summary>Während der Ruhezeit dreht der Strahl höchstens so viel (Grad), Annahme V1f.</summary>
    public const double ZielRuheGrad = 20;

    static bool Hat(IReadOnlyDictionary<string, Vec3> g, string n, out Vec3 p) => g.TryGetValue(n, out p) && p.IstEndlich;

    /// <summary>Verhältnis Abstand Grundgelenk → Spitze zur Summe der Knochenlängen eines Fingers (1 = ganz gestreckt); null, wenn Gelenke fehlen.</summary>
    public static double? Streckung(IReadOnlyDictionary<string, Vec3>? g, string finger)
    {
        if (g is null || !Hat(g, finger + "-phalanx-proximal", out var a) || !Hat(g, finger + "-phalanx-intermediate", out var b)
            || !Hat(g, finger + "-phalanx-distal", out var c) || !Hat(g, finger + "-tip", out var d)) return null;
        var laenge = a.Abstand(b) + b.Abstand(c) + c.Abstand(d);
        return laenge > 1e-6 ? a.Abstand(d) / laenge : null;
    }

    /// <summary>V1f: wie ungleich die vier Finger gekrümmt sind – höchste minus tiefste Streckung von Zeige-, Mittel-, Ring- und kleinem Finger (0 = gleich; ohne Gelenke 0).</summary>
    public static double Spreizung(IReadOnlyDictionary<string, Vec3>? g)
    {
        double min = double.PositiveInfinity, max = double.NegativeInfinity;
        foreach (var f in new[] { "index-finger", "middle-finger", "ring-finger", "pinky-finger" })
            if (Streckung(g, f) is { } s) { min = Math.Min(min, s); max = Math.Max(max, s); }
        return max >= min ? max - min : 0;
    }

    /// <summary>Abstand eines Punkts zur Strecke a–b.</summary>
    public static double AbstandZurStrecke(Vec3 p, Vec3 a, Vec3 b)
    {
        var ab = b - a;
        var l2 = ab.Skalar(ab);
        var s = l2 > 1e-12 ? Math.Clamp((p - a).Skalar(ab) / l2, 0, 1) : 0;
        return p.Abstand(a + ab * s);
    }

    static readonly (string A, string B)[] Seite =
    [
        ("index-finger-metacarpal", "index-finger-phalanx-proximal"), ("index-finger-phalanx-proximal", "index-finger-phalanx-intermediate"),
        ("middle-finger-phalanx-proximal", "middle-finger-phalanx-intermediate"), ("middle-finger-phalanx-intermediate", "middle-finger-phalanx-distal"),
    ];

    /// <summary>
    /// Abstand der Daumenspitze zur Seite der Hand (m): die nächste der Strecken Mittelhand und Grundglied des Zeigefingers, Grund- und Mittelglied des Mittelfingers; null,
    /// wenn Gelenke fehlen.
    /// </summary>
    public static double? Abzug(IReadOnlyDictionary<string, Vec3>? g)
    {
        if (g is null || !Hat(g, "thumb-tip", out var d)) return null;
        var min = double.PositiveInfinity;
        foreach (var (a, b) in Seite)
            if (Hat(g, a, out var pa) && Hat(g, b, out var pb)) min = Math.Min(min, AbstandZurStrecke(d, pa, pb));
        return double.IsFinite(min) ? min : null;
    }

    /// <summary>Die Haltung der Pistole (siehe Dateikopf) – gleich, ob der Daumen oben oder angelegt ist.</summary>
    public static bool Haltung(IReadOnlyDictionary<string, Vec3>? g) =>
        Streckung(g, "index-finger") is >= StreckAnteil
        && Streckung(g, "middle-finger") is { } m && m <= RollAnteil
        && Streckung(g, "ring-finger") is { } r && r <= RollAnteil
        && Streckung(g, "pinky-finger") is { } k && k <= RollAnteil
        && Hat(g!, "thumb-tip", out var d) && Hat(g!, "index-finger-tip", out var z) && d.Abstand(z) >= PinchFreiM;

    /// <summary>Strahl der Pistole: vom Grundgelenk des Zeigefingers durch seine Spitze (Ursprung an der Spitze).</summary>
    public static (Vec3 U, Vec3 R)? Strahl(IReadOnlyDictionary<string, Vec3>? g)
    {
        if (g is null || !Hat(g, "index-finger-phalanx-proximal", out var a) || !Hat(g, "index-finger-tip", out var d)) return null;
        var r = d - a;
        return r.Laenge > 1e-6 ? (d, r.Einheit()) : null;
    }

    /// <summary>Ein Schritt je Bild und Hand (siehe Dateikopf): g = Gelenke (null = nicht erkannt). Der Stand steht auch in z.Stand.</summary>
    public static PistoleStand Schritt(PistoleZustand z, double t, IReadOnlyDictionary<string, Vec3>? g, PistoleEinstellungen? e = null)
    {
        e ??= PistoleEinstellungen.Vorgabe;
        var haltung = Haltung(g);
        var strahl = haltung ? Strahl(g) : null;
        var abzug = haltung ? Abzug(g) : null;
        if (!haltung || strahl is not { } s || abzug is not { } a)
        {
            var los = z.Gedrueckt;
            z.ObenSeit = null;
            z.Gedrueckt = false;
            z.Strahlen.Clear();
            return z.Stand = new PistoleStand(false, false, false, false, los, null, z.BeimAbzug);
        }
        z.Strahlen.Add((t, s.U, s.R));
        while (z.Strahlen.Count > 0 && z.Strahlen[0].T < t - 0.6) z.Strahlen.RemoveAt(0);
        if (z.Gedrueckt)
        {
            // gedrückt bleibt, bis der Daumen über die Hysterese zurück ist; danach beginnt die Ruhezeit neu
            if (a > e.AbzugAusM)
            {
                z.Gedrueckt = false;
                z.ObenSeit = t;
                return z.Stand = new PistoleStand(true, true, false, false, true, s, z.BeimAbzug);
            }
            return z.Stand = new PistoleStand(true, false, true, false, false, s, z.BeimAbzug);
        }
        if (a > e.AbzugAusM) z.ObenSeit ??= t;   // der Daumen ist oben
        else if (a >= e.AbzugAnM) { }             // dazwischen (Hysterese): wie bisher
        else
        {
            // der Daumen liegt an: ein Abzug, wenn er vorher lange genug oben stand, ruhig gezielt und nicht in der Sperre – sonst nur Zeigen (neu ansetzen)
            var bereit = z.ObenSeit is { } seit && t - seit >= e.RuheS - 1e-9 && t >= z.SperreBis && Ruhig(z, t, e);
            z.ObenSeit = null;
            if (!bereit) return z.Stand = new PistoleStand(true, false, false, false, false, s, z.BeimAbzug);
            z.Gedrueckt = true;
            z.SperreBis = t + SperreS;
            z.Abzuege++;
            z.BeimAbzug = StrahlZu(z, t - StrahlVorS) ?? s;
            return z.Stand = new PistoleStand(true, false, true, true, false, s, z.BeimAbzug);
        }
        var zielt = z.ObenSeit is { } o && t - o >= e.RuheS - 1e-9;
        return z.Stand = new PistoleStand(true, zielt, false, false, false, s, z.BeimAbzug);
    }

    /// <summary>Der Strahl zur Zeit t0 (der letzte bis dahin), null ohne.</summary>
    static (Vec3 U, Vec3 R)? StrahlZu(PistoleZustand z, double t0)
    {
        for (int i = z.Strahlen.Count - 1; i >= 0; i--)
            if (z.Strahlen[i].T <= t0 + 1e-9) return (z.Strahlen[i].U, z.Strahlen[i].R);
        return null;
    }

    /// <summary>War der Strahl in der Ruhezeit vor dem Abzug ruhig (alle Richtungen höchstens ZielRuheGrad von der bei t − StrahlVorS)?</summary>
    static bool Ruhig(PistoleZustand z, double t, PistoleEinstellungen e)
    {
        if (StrahlZu(z, t - StrahlVorS) is not { } bezug) return false;
        var cos = Math.Cos(ZielRuheGrad * Math.PI / 180);
        foreach (var (tk, _, r) in z.Strahlen)
            if (tk >= t - StrahlVorS - e.RuheS - 1e-9 && tk <= t - StrahlVorS + 1e-9 && r.Skalar(bezug.R) < cos - 1e-9) return false;
        return true;
    }
}

/// <summary>V1f: ein Wert mit kurzem Verlauf (z. B. das Ziel einer Hand), um beim Abzug den Wert von kurz davor zu nehmen (Pistole.StrahlVorS).</summary>
public sealed class Verlauf<T>
{
    readonly List<(double T, T Wert)> liste = [];
    public double BehaltenS { get; init; } = 0.5;

    public void Neu(double t, T wert)
    {
        liste.Add((t, wert));
        while (liste.Count > 1 && liste[0].T < t - BehaltenS) liste.RemoveAt(0);
    }

    /// <summary>Der Wert zur Zeit t0 (der letzte bis dahin; vor dem ersten der erste), default ohne Werte.</summary>
    public T? Zu(double t0)
    {
        for (int i = liste.Count - 1; i >= 0; i--) if (liste[i].T <= t0 + 1e-9) return liste[i].Wert;
        return liste.Count > 0 ? liste[0].Wert : default;
    }

    public void Leeren() => liste.Clear();
}
