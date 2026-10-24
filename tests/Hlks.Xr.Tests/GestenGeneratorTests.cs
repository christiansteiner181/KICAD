// Tests der Quest App V1b-1 (Modul XR, Gestenlabor): synthetischer Generator plausibler Hände und Gesten, mit Tests, dass die erzeugten Hände plausibel sind
// (die Regeln von XrTabletLogik erkennen offen, Faust, Pinch und die Normale). Alle Daten sind erfunden; Zufall mit fester Saat (die Tests sind reproduzierbar).
//
// Hand: Handgelenk, Mittelhand (4 «metacarpal»), Finger mit Grund-, Mittel-, Endgelenk und Spitze (Knochenlängen eines Erwachsenen, gerundet, Annahme), Daumen mit
// vier Gelenken. Krümmung 0 … 1 beugt Grund-, Mittel- und Endgelenk um 75°, 95°, 65° zur Handfläche; Pinch 0 … 1 führt die Daumenspitze an die Zeigefingerspitze.
// Haltung im Körperbezug (x rechts, y oben, z vorn, relativ zum Kopf); der Generator dreht sie mit dem Gierwinkel in die Welt.
// Rauschen: je Bild und Hand ein Versatz der ganzen Hand (Betrag im Mittel RauschenM, Vorgabe 5 mm) und je Gelenk ein Fünftel davon je Achse (~1.7 mm).
// Zeitverlauf: Ruhe, Bewegung mit Minimum-Jerk-Profil (10τ³ − 15τ⁴ + 6τ⁵, wie eine natürliche Armbewegung), Ruhe.

using Hlks.Xr.Gesten;

namespace Hlks.Xr.Tests;

/// <summary>Haltung einer Hand im Körperbezug: Mitte der Handfläche, Fingerrichtung, Normale (aus der Handfläche heraus), Krümmung (Daumen zuerst), Pinch.</summary>
internal sealed record Haltung(Vec3 Mitte, Vec3 Finger, Vec3 Normale, double[] Kruemmung, double Pinch = 0);

/// <summary>Bewegung: Haltung beider Hände zum Fortschritt u (0 … 1); null = Hand nicht verfolgt.</summary>
internal delegate (Haltung? Links, Haltung? Rechts) GestenBewegung(double u);

/// <summary>Wie eine Bewegung vorgezeigt wird: Dauer, Grösse, Körperdrehung, gespiegelt, Rauschen, Ruhe vorher/nachher, Saat, Bildrate, Bogen (Streuung).</summary>
internal sealed record Vorfuehrung
{
    public double DauerS { get; init; } = 0.5;
    public double Groesse { get; init; } = 1;
    public double GierenGrad { get; init; }
    public bool Gespiegelt { get; init; }
    public double RauschenM { get; init; } = 0.005;
    public double RuheVorS { get; init; } = 0.4;
    public double RuheNachS { get; init; } = 0.4;
    public int Saat { get; init; } = 1;
    public double Bildrate { get; init; } = 72;
    public double T0 { get; init; }
    /// <summary>Menschliche Streuung: die Bahn biegt sich um bis zu so viel (m) in eine zufällige Richtung.</summary>
    public double BogenM { get; init; }
}

/// <summary>Eine Testgeste: Id, Name, Funktion, Händigkeit, Bewegung, übliche Dauer.</summary>
internal sealed record Testgeste(string Id, string Name, string Funktion, Haendigkeit Haendigkeit, GestenBewegung Bewegung, double DauerS);

internal static class GestenGenerator
{
    /// <summary>Gierwinkel des Blicks ohne Körperdrehung: nach Norden (−z).</summary>
    public const double BasisGieren = Math.PI / 2;
    public static readonly Vec3 KopfLage = new(1, 1.6, -2);
    const double Grad = Math.PI / 180;

    public static double[] Kr(double wert) => [wert, wert, wert, wert, wert];
    public static Vec3 V(double x, double y, double z) => new(x, y, z);
    public static Vec3 N(double x, double y, double z) => new Vec3(x, y, z).Einheit();

    static readonly (double Ansatz, double Seitlich, double Knoechel, double KnoechelSeitlich, double[] Knochen)[] Finger =
    [
        (0.03, 0.02, 0.088, 0.022, [0.042, 0.024, 0.020]),
        (0.03, 0.005, 0.090, 0.000, [0.046, 0.028, 0.022]),
        (0.03, -0.008, 0.084, -0.018, [0.043, 0.027, 0.021]),
        (0.03, -0.02, 0.076, -0.034, [0.034, 0.020, 0.018]),
    ];
    static readonly string[] FingerNamen = ["index-finger", "middle-finger", "ring-finger", "pinky-finger"];
    static readonly double[] Beugung = [75 * Grad, 95 * Grad, 65 * Grad];

    static Vec3 Lerp(Vec3 a, Vec3 b, double t) => a + (b - a) * t;

    /// <summary>Die 25 WebXR-Gelenke einer Hand in der Welt (siehe Dateikopf).</summary>
    public static Dictionary<string, Vec3> Hand(HandSeite seite, Vec3 mitte, Vec3 finger, Vec3 normale, IReadOnlyList<double> kr, double pinch)
    {
        var f = finger.Einheit();
        var n = (normale - f * normale.Skalar(f)).Einheit();
        var q = seite == HandSeite.Rechts ? f.Kreuz(n) : n.Kreuz(f);   // zum Daumen hin
        var w = mitte - f * 0.045;
        Vec3 P(double a, double b, double c) => w + f * a + q * b + n * c;
        var g = new Dictionary<string, Vec3>(StringComparer.Ordinal) { ["wrist"] = w };
        for (var i = 0; i < 4; i++)
        {
            var (ansatz, seitlich, knoechel, ks, knochen) = Finger[i];
            var k = Math.Clamp(kr[i + 1], 0, 1);
            var name = FingerNamen[i];
            g[name + "-metacarpal"] = P(ansatz, seitlich, 0);
            var p = P(knoechel, ks, 0);
            g[name + "-phalanx-proximal"] = p;
            var phi = 0.0;
            string[] folge = ["-phalanx-intermediate", "-phalanx-distal", "-tip"];
            for (var j = 0; j < 3; j++)
            {
                phi += k * Beugung[j];
                p += (f * Math.Cos(phi) + n * Math.Sin(phi)) * knochen[j];
                g[name + folge[j]] = p;
            }
        }
        // Daumen: offen gestreckt zur Seite, gekrümmt über die Handfläche, Pinch zur Zeigefingerspitze
        var k0 = Math.Clamp(kr[0], 0, 1);
        var t1 = P(0.02, 0.022, 0.01);
        var dT = (f * 0.5 + q * 0.85 + n * 0.15).Einheit();
        Vec3 t2 = t1 + dT * 0.04, t3 = t2 + dT * 0.032, t4 = t3 + dT * 0.028;
        t2 = Lerp(t2, P(0.05, 0.03, 0.025), k0);
        t3 = Lerp(t3, P(0.07, 0.01, 0.035), k0);
        t4 = Lerp(t4, P(0.075, -0.012, 0.035), k0);
        var pz = Math.Clamp(pinch, 0, 1);
        if (pz > 0)
        {
            var ziel = g["index-finger-tip"] + q * 0.006 + n * 0.004;
            t4 = Lerp(t4, ziel, pz);
            t3 = Lerp(t3, Vec3.Mitte(t2, ziel) + n * 0.01, pz * 0.7);
        }
        g["thumb-metacarpal"] = t1;
        g["thumb-phalanx-proximal"] = t2;
        g["thumb-phalanx-distal"] = t3;
        g["thumb-tip"] = t4;
        return g;
    }

    static double Gauss(Random r)
    {
        var u1 = 1 - r.NextDouble();
        var u2 = r.NextDouble();
        return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
    }

    static Vec3 GaussV(Random r, double sigmaAchse) => new(Gauss(r) * sigmaAchse, Gauss(r) * sigmaAchse, Gauss(r) * sigmaAchse);

    public static double MinimumJerk(double tau)
    {
        var t = Math.Clamp(tau, 0, 1);
        return t * t * t * (10 - 15 * t + 6 * t * t);
    }

    static Haltung Spiegel(Haltung h) => h with
    {
        Mitte = h.Mitte with { X = -h.Mitte.X }, Finger = h.Finger with { X = -h.Finger.X }, Normale = h.Normale with { X = -h.Normale.X },
    };

    /// <summary>Haltung zwischen a und b (Anteil t): Lagen linear, Richtungen normiert linear, Krümmung und Pinch linear.</summary>
    public static Haltung Mischen(Haltung a, Haltung b, double t) => new(Lerp(a.Mitte, b.Mitte, t), Lerp(a.Finger, b.Finger, t).Einheit(),
        Lerp(a.Normale, b.Normale, t).Einheit(), [.. a.Kruemmung.Zip(b.Kruemmung, (x, y) => x + (y - x) * t)], a.Pinch + (b.Pinch - a.Pinch) * t);

    /// <summary>Zeitverlauf eines Stücks (Ruhe, Bewegung, Ruhe) als Haltung zur Zeit ab Beginn des Stücks; Länge des Stücks.</summary>
    public static (Func<double, (Haltung? L, Haltung? R)> Verlauf, double Laenge) Stueck(GestenBewegung b, Vorfuehrung v)
    {
        var r = new Random(v.Saat * 7919 + 17);
        var bogenL = GaussV(r, 1).Einheit() * v.BogenM;
        var bogenR = GaussV(r, 1).Einheit() * v.BogenM;
        (Haltung?, Haltung?) Roh(double u)
        {
            var (l, re) = b(u);
            return v.Gespiegelt ? (re is null ? null : Spiegel(re), l is null ? null : Spiegel(l)) : (l, re);
        }
        var (l0, r0) = Roh(0);
        Haltung? Skaliert(Haltung? h, Haltung? h0, Vec3 bogen, double u) =>
            h is null || h0 is null ? h : h with { Mitte = h0.Mitte + (h.Mitte - h0.Mitte) * v.Groesse + bogen * Math.Sin(Math.PI * u) };
        (Haltung?, Haltung?) Verlauf(double t)
        {
            var u = MinimumJerk((t - v.RuheVorS) / v.DauerS);
            var (l, re) = Roh(u);
            return (Skaliert(l, l0, bogenL, u), Skaliert(re, r0, bogenR, u));
        }
        return (Verlauf, v.RuheVorS + v.DauerS + v.RuheNachS);
    }

    /// <summary>Bilder aus einem Zeitverlauf (Körperdrehung, Rauschen wie im Dateikopf).</summary>
    public static Aufnahme Rendern(Func<double, (Haltung? L, Haltung? R)> verlauf, double laenge, double gierenGrad = 0, double rauschenM = 0.005, int saat = 1,
        double bildrate = 72, double t0 = 0)
    {
        var r = new Random(saat);
        var gieren = BasisGieren + gierenGrad * Grad;
        Vec3 re = XrGesten.Rechts(gieren), vo = XrGesten.Vorn(gieren);
        Vec3 Welt(Vec3 k) => re * k.X + Vec3.Hoch * k.Y + vo * k.Z;
        var anzahl = (int)Math.Round(laenge * bildrate) + 1;
        var bilder = new List<GestenBild>(anzahl);
        var sigma = rauschenM / Math.Sqrt(3);
        for (var i = 0; i < anzahl; i++)
        {
            var t = i / bildrate;
            var (l, rh) = verlauf(t);
            HandBild Bild(HandSeite s, Haltung? h)
            {
                if (h is null) return HandBild.Fehlt;
                var g = Hand(s, KopfLage + Welt(h.Mitte), Welt(h.Finger), Welt(h.Normale), h.Kruemmung, h.Pinch);
                var versatz = GaussV(r, sigma);
                foreach (var name in g.Keys.ToList()) g[name] = g[name] + versatz + GaussV(r, rauschenM * 0.2);
                return HandBild.AusGelenken(s, g);
            }
            bilder.Add(new GestenBild(t0 + t, KopfLage, gieren, Bild(HandSeite.Links, l), Bild(HandSeite.Rechts, rh)));
        }
        return new Aufnahme(bilder);
    }

    /// <summary>Rohaufnahme einer vorgezeigten Bewegung (Ruhe, Bewegung, Ruhe).</summary>
    public static Aufnahme Erzeugen(GestenBewegung b, Vorfuehrung v)
    {
        var (verlauf, laenge) = Stueck(b, v);
        return Rendern(verlauf, laenge, v.GierenGrad, v.RauschenM, v.Saat, v.Bildrate, v.T0);
    }

    /// <summary>
    /// Ein Strom aus mehreren Stücken mit weichen Übergängen (Minimum-Jerk, uebergangS) zwischen dem Ende des einen und dem Anfang des nächsten; eine Hand, die
    /// im nächsten Stück fehlt bzw. dazukommt, verschwindet bzw. erscheint in der Mitte des Übergangs. Gleiche Körperdrehung für alles.
    /// </summary>
    public static (Aufnahme Aufnahme, IReadOnlyList<(double Start, double Ende)> Bewegungen) Strom(IReadOnlyList<(GestenBewegung B, Vorfuehrung V)> stuecke,
        double uebergangS = 0.6, double gierenGrad = 0, int saat = 99, double rauschenM = 0.005)
    {
        var teile = new List<(double Start, double Laenge, Func<double, (Haltung?, Haltung?)> F)>();
        var bewegungen = new List<(double, double)>();
        var t = 0.0;
        for (var i = 0; i < stuecke.Count; i++)
        {
            var (f, l) = Stueck(stuecke[i].B, stuecke[i].V);
            if (i > 0)
            {
                var vorher = teile[^1];
                var ende = vorher.F(vorher.Laenge);
                var anfang = f(0);
                var dauer = uebergangS;
                teile.Add((t, dauer, x =>
                {
                    var u = MinimumJerk(x / dauer);
                    return (Uebergang(ende.Item1, anfang.Item1, u), Uebergang(ende.Item2, anfang.Item2, u));
                }));
                t += dauer;
            }
            teile.Add((t, l, f));
            bewegungen.Add((t + stuecke[i].V.RuheVorS, t + stuecke[i].V.RuheVorS + stuecke[i].V.DauerS));
            t += l;
        }
        (Haltung?, Haltung?) Verlauf(double x)
        {
            foreach (var (s, l, f) in teile)
                if (x <= s + l + 1e-9) return f(Math.Max(0, x - s));
            var letzt = teile[^1];
            return letzt.F(letzt.Laenge);
        }
        return (Rendern(Verlauf, t, gierenGrad, rauschenM, saat), bewegungen);
    }

    static Haltung? Uebergang(Haltung? a, Haltung? b, double u) => (a, b) switch
    {
        (null, null) => null,
        (null, _) => u < 0.5 ? null : b,
        (_, null) => u < 0.5 ? a : null,
        _ => Mischen(a!, b!, u),
    };

    // ------------------------------------------------------------------------------------------------ Ruhe und Zufall

    public static readonly Haltung RuheRechts = new(V(0.2, -0.45, 0.3), V(0, 0, 1), V(-1, 0, 0), Kr(0.25));
    public static readonly Haltung RuheLinks = new(V(-0.2, -0.45, 0.3), V(0, 0, 1), V(1, 0, 0), Kr(0.25));

    /// <summary>Ruhende Hände (beide verfolgt), nur Rauschen.</summary>
    public static GestenBewegung Ruhe => _ => (RuheLinks, RuheRechts);

    /// <summary>
    /// Zufällige Bewegung der rechten (mitLinks: beider) Hände: weicher Weg (Catmull-Rom) durch fünf zufällige Punkte in einem Würfel von ± 0.2 m um die
    /// Ruhelage, mit zufälliger Lage der Hand und zufälliger Krümmung bzw. Pinch an jedem Punkt.
    /// </summary>
    public static GestenBewegung Zufall(int saat, bool mitLinks = false)
    {
        var r = new Random(saat);
        Haltung[] Punkte(Haltung basis, double seite)
        {
            var p = new Haltung[5];
            for (var i = 0; i < 5; i++)
            {
                var f = N(r.NextDouble() * 2 - 1, r.NextDouble() * 2 - 1, r.NextDouble() * 2 - 1);
                var n0 = N(r.NextDouble() * 2 - 1, r.NextDouble() * 2 - 1, r.NextDouble() * 2 - 1);
                var n = (n0 - f * n0.Skalar(f)).Einheit();
                var kr = r.NextDouble() < 0.3 ? Kr(r.NextDouble()) : Enumerable.Range(0, 5).Select(_ => r.NextDouble()).ToArray();
                var pinch = r.NextDouble() < 0.2 ? 1 : 0;
                var m = basis.Mitte + V((r.NextDouble() * 2 - 1) * 0.2, (r.NextDouble() * 2 - 1) * 0.2 + 0.15, (r.NextDouble() * 2 - 1) * 0.2);
                p[i] = new Haltung(m with { X = m.X * seite }, f, n, kr, pinch);
            }
            p[0] = basis;
            return p;
        }
        var pr = Punkte(RuheRechts, 1);
        var pl = Punkte(RuheLinks, 1);
        Haltung Spline(Haltung[] p, double u)
        {
            var x = Math.Clamp(u, 0, 1) * (p.Length - 1);
            var i = Math.Min((int)x, p.Length - 2);
            var s = x - i;
            Vec3 Cr(Func<Haltung, Vec3> w)
            {
                Vec3 p0 = w(p[Math.Max(0, i - 1)]), p1 = w(p[i]), p2 = w(p[i + 1]), p3 = w(p[Math.Min(p.Length - 1, i + 2)]);
                return 0.5 * (2 * p1 + (p2 - p0) * s + (2 * p0 - 5 * p1 + 4 * p2 - p3) * (s * s) + (3 * p1 - p0 - 3 * p2 + p3) * (s * s * s));
            }
            var a = p[i];
            var b = p[i + 1];
            var h = Mischen(a, b, s * s * (3 - 2 * s));
            return h with { Mitte = Cr(z => z.Mitte) };
        }
        return u => (mitLinks ? Spline(pl, u) : null, Spline(pr, u));
    }

    // ------------------------------------------------------------------------------------------------ Testgesten (Körperbezug: x rechts, y oben, z vorn)

    static readonly Vec3 VornHoch = N(0, 0.3, 1);

    /// <summary>Hebegeste: beide Handflächen nach oben, beide Hände 30 cm hoch (Christian 24.10.2026, App-Symbole).</summary>
    public static readonly GestenBewegung Hebe = u => (
        new Haltung(V(-0.2, -0.5 + 0.3 * u, 0.35), V(0, 0, 1), V(0, 1, 0), Kr(0.1)),
        new Haltung(V(0.2, -0.5 + 0.3 * u, 0.35), V(0, 0, 1), V(0, 1, 0), Kr(0.1)));

    /// <summary>Wisch links: rechte Hand offen, Handfläche nach links, 35 cm nach links (leicht im Bogen).</summary>
    public static readonly GestenBewegung WischLinks = u => (null, new Haltung(V(0.25 - 0.35 * u, -0.25 + 0.03 * Math.Sin(Math.PI * u), 0.4), VornHoch, V(-1, 0, 0), Kr(0.1)));

    /// <summary>Wisch rechts: rechte Hand offen, Handfläche nach rechts, 35 cm nach rechts.</summary>
    public static readonly GestenBewegung WischRechts = u => (null, new Haltung(V(-0.05 + 0.35 * u, -0.25 + 0.03 * Math.Sin(Math.PI * u), 0.4), VornHoch, V(1, 0, 0), Kr(0.1)));

    /// <summary>Wisch runter: rechte Hand, Handfläche nach unten, 30 cm nach unten.</summary>
    public static readonly GestenBewegung WischRunter = u => (null, new Haltung(V(0.15, -0.1 - 0.3 * u, 0.35), V(0, 0, 1), V(0, -1, 0), Kr(0.1)));

    /// <summary>Kreis: rechte Hand zeigt mit dem Zeigefinger nach vorn und zieht einen Kreis (Radius 12 cm) in der Ebene vor dem Körper.</summary>
    public static readonly GestenBewegung Kreis = u => (null, new Haltung(V(0.15 + 0.12 * Math.Sin(2 * Math.PI * u), -0.15 - 0.12 * Math.Cos(2 * Math.PI * u), 0.4),
        V(0, 0, 1), V(0, -1, 0), [0.6, 0, 0.9, 0.9, 0.9]));

    /// <summary>Stoss nach vorn: Handfläche nach vorn, Finger oben, 25 cm vor.</summary>
    public static readonly GestenBewegung Stoss = u => (null, new Haltung(V(0.15, -0.2, 0.2 + 0.25 * u), V(0, 1, 0), V(0, 0, 1), Kr(0.05)));

    /// <summary>Heranziehen: Handfläche zum Körper, Finger oben, 25 cm zurück.</summary>
    public static readonly GestenBewegung Heranziehen = u => (null, new Haltung(V(0.15, -0.2, 0.5 - 0.25 * u), V(0, 1, 0), V(0, 0, -1), Kr(0.05)));

    /// <summary>Faust schliessen: die Hand bleibt fast an Ort, alle Finger krümmen sich.</summary>
    public static readonly GestenBewegung Faust = u => (null, new Haltung(V(0.15, -0.25 - 0.01 * u, 0.35), VornHoch, V(-1, 0, 0), Kr(0.05 + 0.95 * u)));

    /// <summary>Wisch hoch mit Pinch: rechte Hand hält den Pinch und zieht 30 cm nach oben.</summary>
    public static readonly GestenBewegung WischHochPinch = u => (null, new Haltung(V(0.15, -0.35 + 0.3 * u, 0.35), VornHoch, V(-1, 0, 0), [0.3, 0.35, 0.7, 0.8, 0.8], 1));

    /// <summary>
    /// Auseinander (Christian, Ergänzung 24.10.2026: «aus der mitte heraus seitwärtswischen mit beiden armen und nach aussen gedrehten händen»): beide Hände
    /// starten nahe der Mitte, die Handflächen nach aussen, und wischen gleichzeitig je 30 cm nach links bzw. rechts.
    /// </summary>
    public static readonly GestenBewegung Auseinander = u => (
        new Haltung(V(-0.05 - 0.3 * u, -0.25, 0.35), VornHoch, V(-1, 0, 0), Kr(0.1)),
        new Haltung(V(0.05 + 0.3 * u, -0.25, 0.35), VornHoch, V(1, 0, 0), Kr(0.1)));

    public static readonly Testgeste THebe = new("hebe", "Hebegeste", "AppsEinblenden", Haendigkeit.Beidhaendig, Hebe, 0.7);
    public static readonly Testgeste TWischLinks = new("wisch-links", "Wisch links", "SeiteVor", Haendigkeit.Rechts, WischLinks, 0.4);
    public static readonly Testgeste TKreis = new("kreis", "Kreis", "Drehen", Haendigkeit.Beliebig, Kreis, 1.0);
    public static readonly Testgeste TStoss = new("stoss", "Stoss nach vorn", "AppSchliessen", Haendigkeit.Beliebig, Stoss, 0.45);
    public static readonly Testgeste TFaust = new("faust", "Faust schliessen", "Greifen", Haendigkeit.Beliebig, Faust, 0.5);
    public static readonly Testgeste TWischHochPinch = new("wisch-hoch-pinch", "Wisch hoch mit Pinch", "EtageHoch", Haendigkeit.Rechts, WischHochPinch, 0.35);
    public static readonly Testgeste TAuseinander = new("auseinander", "Auseinander", "AppsAusblenden", Haendigkeit.Beidhaendig, Auseinander, 0.5);
    public static readonly Testgeste TWischRechts = new("wisch-rechts", "Wisch rechts", "SeiteZurueck", Haendigkeit.Rechts, WischRechts, 0.4);
    public static readonly Testgeste TWischRunter = new("wisch-runter", "Wisch runter", "EtageRunter", Haendigkeit.Rechts, WischRunter, 0.4);
    public static readonly Testgeste THeranziehen = new("heranziehen", "Heranziehen", "Heranholen", Haendigkeit.Beliebig, Heranziehen, 0.5);

    /// <summary>Die Testgesten des Auftrags V1b-1 und die Ergänzung «Auseinander».</summary>
    public static readonly Testgeste[] Auftrag = [THebe, TWischLinks, TKreis, TStoss, TFaust, TWischHochPinch, TAuseinander];
    /// <summary>Zehn Gesten (Budget: 10 Gesten × 5 Beispiele).</summary>
    public static readonly Testgeste[] Zehn = [.. Auftrag, TWischRechts, TWischRunter, THeranziehen];

    /// <summary>
    /// Beispiele einer Testgeste wie im Labor vorgezeigt: Tempo ± 15 %, Grösse ± 10 %, Bogen 1.5 cm, Körperdrehung ± 20°, Rauschen 5 mm; jede Rohaufnahme mit
    /// AufnahmeHilfe zugeschnitten (muss Ok sein).
    /// </summary>
    public static EigeneGeste Geste(Testgeste g, int anzahl = 5, int saat = 1000, string? id = null, string? funktion = null)
    {
        var r = new Random(saat);
        var beispiele = new List<Aufnahme>();
        for (var i = 0; i < anzahl; i++)
        {
            var v = new Vorfuehrung
            {
                DauerS = g.DauerS * (1 + (r.NextDouble() * 2 - 1) * 0.15), Groesse = 1 + (r.NextDouble() * 2 - 1) * 0.1, BogenM = 0.015,
                GierenGrad = (r.NextDouble() * 2 - 1) * 20, Saat = saat + 31 * i,
            };
            var z = AufnahmeHilfe.Zuschneiden(Erzeugen(g.Bewegung, v), g.Haendigkeit);
            Assert.True(z.Ok, $"{g.Name}, Beispiel {i + 1}: {z.Rueckmeldung}");
            beispiele.Add(z.Aufnahme!);
        }
        return new EigeneGeste(id ?? g.Id, g.Name, funktion ?? g.Funktion, g.Haendigkeit, beispiele);
    }

    static readonly Dictionary<string, EigeneGeste> Zwischenspeicher = new();

    /// <summary>Die Beispiele einer Testgeste (5, Saat fest), einmal erzeugt.</summary>
    public static EigeneGeste Fertig(Testgeste g)
    {
        lock (Zwischenspeicher)
        {
            if (!Zwischenspeicher.TryGetValue(g.Id, out var e)) Zwischenspeicher[g.Id] = e = Geste(g, saat: 1000 + 97 * Array.IndexOf(Zehn, g));
            return e;
        }
    }

    /// <summary>
    /// Abwandlungen für die Prüfung: alle Paare aus Tempo (× 0.6, 1, 1.4) und Grösse (× 0.75, 1, 1.25) mit Körperdrehungen −90°, 0°, 45°, 90°, 180° im Wechsel;
    /// gespiegelt die drei Tempi bei Grösse 1. Rauschen 5 mm, eigene Saat je Abwandlung.
    /// </summary>
    public static IEnumerable<(string Text, Vorfuehrung V)> Abwandlungen(Testgeste g, bool gespiegelt, int saat = 50_000)
    {
        double[] tempi = [0.6, 1, 1.4], groessen = [0.75, 1, 1.25], drehungen = [-90, 0, 45, 90, 180];
        var i = 0;
        foreach (var tempo in tempi)
            foreach (var gr in groessen)
            {
                var dreh = drehungen[i % drehungen.Length];
                yield return ($"Tempo {tempo}, Grösse {gr}, Drehung {dreh}°", new Vorfuehrung
                {
                    DauerS = g.DauerS / tempo, Groesse = gr, GierenGrad = dreh, Saat = saat + 13 * i, BogenM = 0.015,
                });
                i++;
            }
        if (!gespiegelt) yield break;
        foreach (var tempo in tempi)
        {
            yield return ($"gespiegelt, Tempo {tempo}", new Vorfuehrung { DauerS = g.DauerS / tempo, Gespiegelt = true, GierenGrad = 30, Saat = saat + 13 * i, BogenM = 0.015 });
            i++;
        }
    }
}

public class GestenGeneratorTests
{
    static readonly Vec3 Mitte = new(0.3, 1.2, -0.5);

    [Theory]
    [InlineData(HandSeite.Rechts)]
    [InlineData(HandSeite.Links)]
    public void Offene_Hand_ist_offen_ohne_Pinch_und_hat_die_verlangte_Normale_und_Mitte(HandSeite seite)
    {
        var n = GestenGenerator.N(0.3, -1, 0.2);
        var f = new Vec3(0, 0, -1);
        var g = GestenGenerator.Hand(seite, Mitte, f, n, GestenGenerator.Kr(0), 0);
        Assert.Equal(25, g.Count);
        Assert.All(HandGelenke.WebXr, name => Assert.True(g.ContainsKey(name), name));
        Assert.True(XrTabletLogik.IstOffen(g));
        Assert.False(XrTabletLogik.IstFaust(g));
        Assert.False(XrTabletLogik.IstPinch(g));
        var nIst = XrTabletLogik.HandflaechenNormale(g, seite == HandSeite.Links ? "left" : "right")!.Value;
        var nSoll = (n - f.Einheit() * n.Skalar(f.Einheit())).Einheit();
        Assert.True(nIst.Skalar(nSoll) > 0.99, $"Normale {nIst} statt {nSoll}");
        Assert.True(XrTabletLogik.HandflaechenMitte(g)!.Value.Abstand(Mitte) < 1e-9);
        Assert.InRange(XrTabletLogik.HandGroesse(g), 0.0899, 0.0901);
        var h = HandBild.AusGelenken(seite, g);
        Assert.True(h.Verfolgt && h.Brauchbar);
        // gestreckt: fast 0 (Mittelhand und Knöchel liegen nicht ganz auf einer Linie)
        Assert.All(GestenMerkmale.Kruemmung(h), k => Assert.InRange(k, 0, 0.03));
        Assert.Equal(1.0, GestenMerkmale.PinchNormiert(h));
    }

    [Fact]
    public void Faust_ist_Faust_und_stark_gekruemmt_Pinch_ist_Pinch()
    {
        var f = new Vec3(0, 0, -1);
        var n = new Vec3(0, -1, 0);
        var faust = GestenGenerator.Hand(HandSeite.Rechts, Mitte, f, n, GestenGenerator.Kr(1), 0);
        Assert.True(XrTabletLogik.IstFaust(faust));
        var kr = GestenMerkmale.Kruemmung(HandBild.AusGelenken(HandSeite.Rechts, faust));
        Assert.True(kr.Skip(1).All(k => k > 0.5), string.Join(", ", kr));
        Assert.True(kr[0] > 0.1, "Daumen gekrümmt");

        var pinch = GestenGenerator.Hand(HandSeite.Rechts, Mitte, f, n, [0.3, 0.35, 0.7, 0.8, 0.8], 1);
        Assert.True(XrTabletLogik.IstPinch(pinch));
        Assert.True(GestenMerkmale.PinchNormiert(HandBild.AusGelenken(HandSeite.Rechts, pinch)) < 0.2);
    }

    [Fact]
    public void Erzeugte_Aufnahme_hat_Ruhe_Bewegung_Ruhe_und_72_Bilder_je_Sekunde()
    {
        var a = GestenGenerator.Erzeugen(GestenGenerator.WischLinks, new Vorfuehrung { DauerS = 0.4, RuheVorS = 0.5, RuheNachS = 0.5 });
        Assert.Equal((int)Math.Round(1.4 * 72) + 1, a.Bilder.Count);
        Assert.True(a.ZeitenGueltig());
        Assert.All(a.Bilder, b => Assert.False(b.Links.Verfolgt));
        Assert.All(a.Bilder, b => Assert.True(b.Rechts.Brauchbar));
        // die Handfläche geht in Blickrichtung gesehen nach links (Körperbezug x nimmt ab), rund 35 cm
        var start = GestenMerkmale.Koerperbezug(a.Bilder[0].Rechts.Mitte, a.Bilder[0].Gieren);
        var ende = GestenMerkmale.Koerperbezug(a.Bilder[^1].Rechts.Mitte, a.Bilder[^1].Gieren);
        Assert.InRange(start.X - ende.X, 0.32, 0.38);
    }

    [Fact]
    public void Gespiegelt_macht_aus_der_rechten_die_linke_Hand_mit_Bewegung_zur_anderen_Seite()
    {
        var a = GestenGenerator.Erzeugen(GestenGenerator.WischLinks, new Vorfuehrung { DauerS = 0.4, Gespiegelt = true, RauschenM = 0 });
        Assert.All(a.Bilder, b => Assert.False(b.Rechts.Verfolgt));
        var start = GestenMerkmale.Koerperbezug(a.Bilder[0].Links.Mitte, a.Bilder[0].Gieren);
        var ende = GestenMerkmale.Koerperbezug(a.Bilder[^1].Links.Mitte, a.Bilder[^1].Gieren);
        Assert.True(ende.X - start.X > 0.3, "gespiegelt: nach rechts");
        Assert.True(GestenMerkmale.Koerperbezug(a.Bilder[0].Links.Normale, a.Bilder[0].Gieren).X > 0.9, "Handfläche nach rechts");
    }

    [Fact]
    public void Strom_hat_weiche_Uebergaenge_ohne_Spruenge()
    {
        var (a, bewegungen) = GestenGenerator.Strom([(GestenGenerator.WischLinks, new Vorfuehrung { DauerS = 0.4 }), (GestenGenerator.Kreis, new Vorfuehrung { DauerS = 1 })],
            rauschenM: 0);
        Assert.Equal(2, bewegungen.Count);
        for (var i = 1; i < a.Bilder.Count; i++)
        {
            var (p, q) = (a.Bilder[i - 1].Rechts, a.Bilder[i].Rechts);
            if (p.Verfolgt && q.Verfolgt) Assert.True(p.Mitte.Abstand(q.Mitte) < 0.04, $"Sprung bei Bild {i}");
        }
    }
}
