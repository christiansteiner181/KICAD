// Gemeinsame Grundbausteine der Bibliothek Hlks.Xr (ohne eigene JavaScript-Vorlage; ersetzt die kleinen Hilfen aus xr-mess-logik.js:
// v3, plus, minus, mal, skalar, kreuz, laenge, abstand3, einheit, mitteVon3).
//
// Konventionen (für alle Dateien dieser Bibliothek):
//   - Rechtssystem wie WebXR und StereoKit: Y nach oben, Einheit Meter, Blickrichtung nach -Z.
//   - Szene wie die 3D-Ansicht von KICAD (three): x = Ost, y = oben, z = -Nord. Modellpunkte des Kerns (mm) sind x Ost, y Nord, z oben.
//   - Winkel im Bogenmass; «Gieren» zählt gegen den Uhrzeigersinn von oben, ab +x (90 Grad = Nord = -z).
//   - Vektoren rechnen in doppelter Genauigkeit (Vec3), damit die Ergebnisse die der JavaScript-Vorlage treffen. Die Umwandlung von und zu
//     System.Numerics.Vector3 (float, wie in StereoKit) geschieht an der Grenze zur App: implizit hinein, ausdrücklich (Vector3) hinaus.

using System.Globalization;
using System.Numerics;

namespace Hlks.Xr;

/// <summary>Punkt oder Richtung im Raum, Y oben, in Metern (doppelte Genauigkeit).</summary>
public readonly record struct Vec3(double X, double Y, double Z)
{
    public static readonly Vec3 Null = new(0, 0, 0);
    public static readonly Vec3 Hoch = new(0, 1, 0);

    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vec3 operator -(Vec3 a) => new(-a.X, -a.Y, -a.Z);
    public static Vec3 operator *(Vec3 a, double s) => new(a.X * s, a.Y * s, a.Z * s);
    public static Vec3 operator *(double s, Vec3 a) => new(a.X * s, a.Y * s, a.Z * s);

    /// <summary>xr-mess-logik.js skalar.</summary>
    public double Skalar(Vec3 b) => X * b.X + Y * b.Y + Z * b.Z;

    /// <summary>xr-mess-logik.js kreuz.</summary>
    public Vec3 Kreuz(Vec3 b) => new(Y * b.Z - Z * b.Y, Z * b.X - X * b.Z, X * b.Y - Y * b.X);

    /// <summary>xr-mess-logik.js laenge.</summary>
    public double Laenge => Math.Sqrt(X * X + Y * Y + Z * Z);

    /// <summary>xr-mess-logik.js abstand3.</summary>
    public double Abstand(Vec3 b) => (this - b).Laenge;

    /// <summary>xr-mess-logik.js einheit: Einheitsvektor; ein Vektor unter 1e-9 Länge ergibt den Nullvektor.</summary>
    public Vec3 Einheit()
    {
        var l = Laenge;
        return l < 1e-9 ? Null : this * (1 / l);
    }

    /// <summary>xr-mess-logik.js mitteVon3.</summary>
    public static Vec3 Mitte(Vec3 a, Vec3 b) => (a + b) * 0.5;

    /// <summary>Alle drei Werte endlich (keine NaN, keine Unendlichen).</summary>
    public bool IstEndlich => double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Z);

    /// <summary>Wert einer Achse (0 = x, 1 = y, 2 = z).</summary>
    public double Komp(int achse) => achse switch { 0 => X, 1 => Y, _ => Z };

    public static implicit operator Vec3(Vector3 v) => new(v.X, v.Y, v.Z);
    public static explicit operator Vector3(Vec3 v) => new((float)v.X, (float)v.Y, (float)v.Z);
    public Vector3 ZuVector3() => (Vector3)this;
}

/// <summary>Waagrechter Punkt oder Vektor (x, z) in Metern; Höhe wird nicht geführt.</summary>
public readonly record struct Xz(double X, double Z);

/// <summary>Achsenparallele Box (Minimum und Maximum), in Metern der Szene. Die leere Box hat Minimum +unendlich, Maximum -unendlich.</summary>
public readonly record struct Box3(Vec3 Min, Vec3 Max)
{
    public static readonly Box3 Leer = new(new Vec3(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity),
        new Vec3(double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity));

    public bool IstLeer => double.IsPositiveInfinity(Min.X);

    /// <summary>xr-mess-logik.js boxPlus: die Box, erweitert um einen Punkt.</summary>
    public Box3 Plus(Vec3 p) => new(
        new Vec3(Math.Min(Min.X, p.X), Math.Min(Min.Y, p.Y), Math.Min(Min.Z, p.Z)),
        new Vec3(Math.Max(Max.X, p.X), Math.Max(Max.Y, p.Y), Math.Max(Max.Z, p.Z)));

    /// <summary>begehen-logik.js vereinigt.</summary>
    public Box3 Vereinigt(Box3 b) => new(
        new Vec3(Math.Min(Min.X, b.Min.X), Math.Min(Min.Y, b.Min.Y), Math.Min(Min.Z, b.Min.Z)),
        new Vec3(Math.Max(Max.X, b.Max.X), Math.Max(Max.Y, b.Max.Y), Math.Max(Max.Z, b.Max.Z)));

    /// <summary>Abstand eines Punktes zur Box (0 innen). xr-mess-logik.js boxAbstand bzw. begehen-logik.js abstandZurBox.</summary>
    public double Abstand(Vec3 p)
    {
        var dx = Math.Max(Math.Max(Min.X - p.X, 0), p.X - Max.X);
        var dy = Math.Max(Math.Max(Min.Y - p.Y, 0), p.Y - Max.Y);
        var dz = Math.Max(Math.Max(Min.Z - p.Z, 0), p.Z - Max.Z);
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>Kleinste Box um die Punkte (leer ohne Punkte). begehen-logik.js boxVon.</summary>
    public static Box3 Von(IEnumerable<Vec3> punkte)
    {
        var b = Leer;
        foreach (var p in punkte) b = b.Plus(p);
        return b;
    }
}

/// <summary>Kleine Zahlenhilfen, die das Verhalten von JavaScript nachbilden (Math.round, toFixed, Endlichkeit).</summary>
internal static class Num
{
    public const double Grad = Math.PI / 180;

    public static bool Ok(double x) => double.IsFinite(x);
    public static bool Ok(double? x) => x.HasValue && double.IsFinite(x.Value);

    public static double Klemme(double x, double a, double b) => Math.Min(b, Math.Max(a, x));

    /// <summary>JavaScript Math.round: halbe Werte werden aufgerundet (auch bei negativen Zahlen: -2.5 gibt -2).</summary>
    public static double Round(double x)
    {
        if (!double.IsFinite(x)) return x;
        var r = Math.Floor(x);
        return x - r >= 0.5 ? r + 1 : r;
    }

    public static double Hypot(double a, double b) => Math.Sqrt(a * a + b * b);
    public static double Hypot(double a, double b, double c) => Math.Sqrt(a * a + b * b + c * c);

    /// <summary>JavaScript toFixed: feste Stellen, Punkt als Dezimaltrenner; minus null wird zu null.</summary>
    public static string ToFixed(double x, int stellen)
    {
        if (x == 0) x = 0;
        return x.ToString("F" + stellen, CultureInfo.InvariantCulture);
    }

    /// <summary>Zahl wie JavaScript String(number) für gewöhnliche Werte (ganze Zahlen ohne Nachkommastellen).</summary>
    public static string Text(double x) => x.ToString("R", CultureInfo.InvariantCulture);
}
