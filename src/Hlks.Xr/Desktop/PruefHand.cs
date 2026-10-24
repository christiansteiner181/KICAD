// Quest App V1c (Modul XR, XR-I): eine gedachte offene Hand für den Prüflauf der App am PC (werkzeuge/quest_pruefen.ps1, Lauf «Desktop»): Der Simulator hat
// keine echten Hände; der Prüflauf führt damit die Gesten Hineinziehen und Wegwischen vor und zeichnet die Hand ins Prüfbild. Gleiche Geometrie wie der
// synthetische Generator der Tests (tests/Hlks.Xr.Tests/GestenGeneratorTests.cs, Knochenlängen eines Erwachsenen, gerundet, Annahme), nur offen.

namespace Hlks.Xr.Desktop;

public static class PruefHand
{
    static readonly (double Ansatz, double Seitlich, double Knoechel, double KnoechelSeitlich, double[] Knochen)[] Finger =
    [
        (0.03, 0.02, 0.088, 0.022, [0.042, 0.024, 0.020]),
        (0.03, 0.005, 0.090, 0.000, [0.046, 0.028, 0.022]),
        (0.03, -0.008, 0.084, -0.018, [0.043, 0.027, 0.021]),
        (0.03, -0.02, 0.076, -0.034, [0.034, 0.020, 0.018]),
    ];
    static readonly string[] FingerNamen = ["index-finger", "middle-finger", "ring-finger", "pinky-finger"];

    /// <summary>
    /// Die 25 WebXR-Gelenke einer offenen Hand (leicht gekrümmt): Mitte der Handfläche, Fingerrichtung, Normale (aus der Handfläche heraus), rechte oder linke Hand.
    /// </summary>
    public static Dictionary<string, Vec3> Offen(Vec3 mitte, Vec3 finger, Vec3 normale, bool rechts = true, double kruemmung = 0.1)
    {
        var f = finger.Einheit();
        var n = (normale - f * normale.Skalar(f)).Einheit();
        var q = rechts ? f.Kreuz(n) : n.Kreuz(f);   // zum Daumen hin
        var w = mitte - f * 0.045;
        Vec3 P(double a, double b, double c) => w + f * a + q * b + n * c;
        var g = new Dictionary<string, Vec3>(StringComparer.Ordinal) { ["wrist"] = w };
        double[] beugung = [75 * Num.Grad, 95 * Num.Grad, 65 * Num.Grad];
        for (var i = 0; i < 4; i++)
        {
            var (ansatz, seitlich, knoechel, ks, knochen) = Finger[i];
            var name = FingerNamen[i];
            g[name + "-metacarpal"] = P(ansatz, seitlich, 0);
            var p = P(knoechel, ks, 0);
            g[name + "-phalanx-proximal"] = p;
            var phi = 0.0;
            string[] folge = ["-phalanx-intermediate", "-phalanx-distal", "-tip"];
            for (var j = 0; j < 3; j++)
            {
                phi += kruemmung * beugung[j];
                p += (f * Math.Cos(phi) + n * Math.Sin(phi)) * knochen[j];
                g[name + folge[j]] = p;
            }
        }
        var t1 = P(0.02, 0.022, 0.01);
        var dT = (f * 0.5 + q * 0.85 + n * 0.15).Einheit();
        g["thumb-metacarpal"] = t1;
        g["thumb-phalanx-proximal"] = t1 + dT * 0.04;
        g["thumb-phalanx-distal"] = t1 + dT * 0.072;
        g["thumb-tip"] = t1 + dT * 0.1;
        return g;
    }

    /// <summary>Lage im Körperbezug (x rechts, y oben, z vorn; relativ zum Kopf) → Raum der Brille.</summary>
    public static Vec3 Welt(Vec3 kopf, double blickGieren, Vec3 koerper) =>
        kopf + XrGesten.Rechts(blickGieren) * koerper.X + Vec3.Hoch * koerper.Y + XrGesten.Vorn(blickGieren) * koerper.Z;

    /// <summary>Rechte Hand des Prüflaufs: offen, Handfläche nach links, Finger nach vorn und leicht oben, bei x (m rechts vom Kopf), 0.25 m unter und 0.4 m vor den Augen.</summary>
    public static Dictionary<string, Vec3> RechtsHandflaecheLinks(Vec3 kopf, double blickGieren, double x)
    {
        var r = XrGesten.Rechts(blickGieren);
        var v = XrGesten.Vorn(blickGieren);
        return Offen(Welt(kopf, blickGieren, new Vec3(x, -0.25, 0.4)), (v + Vec3.Hoch * 0.3).Einheit(), r * -1);
    }
}
