// Quest App (Modul XR, P2): Umrechnung der KICAD-Koordinaten in glTF 2.0.
//
// KICAD: Millimeter, x Ost, y Nord, z oben (rechtshändig). glTF 2.0 (Khronos-Spezifikation, Abschnitt «Coordinate System and Units»):
// Meter, rechtshändig, +y oben. Abbildung wie die 3D-Ansicht (koerper3d.js `vek`, ansicht3d.js): x = x, y = z, z = −y – eine Drehung um die
// x-Achse (Determinante +1), Dreiecke behalten ihren Umlaufsinn und Normalen drehen gleich mit. Damit gelten die Regeln des
// Browser-Prototyps (vr-logik.js, xr-*-logik.js) in der App unverändert.
//
//   gx = (x − bx) / 1000,  gy = (z − bz) / 1000,  gz = −(y − by) / 1000      (b = Bezugspunkt in KICAD-mm)
//
// Bezugspunkt: Projekt-Nullpunkt (0, 0, 0), solange das Modell (Körper und IFC-Unterlagen) in jeder Achse innerhalb von ±5 km liegt.
// Grund (technisch, kein Normwert): glTF speichert float32; bis 8192 m vom Ursprung ist der Abstand zweier float32-Werte höchstens
// 2^13 · 2^−23 m ≈ 1 mm, unter 5 km höchstens 0.5 mm. Liegt eine Achse weiter weg (Landeskoordinaten), gilt in dieser Achse die Mitte
// der Ausdehnung, auf ganze Meter gerundet. Ein Bezugspunkt gilt für alle Geschosse und Teile eines Stands, damit die GLB ohne Versatz
// übereinander passen; die App liest ihn aus `extras.kicad.bezugspunktMm` der Szene.

namespace Hlks.Kern;

public static class XrKoordinaten
{
    /// <summary>Millimeter je Meter.</summary>
    public const double MmJeMeter = 1000;

    /// <summary>Grenze je Achse (mm), bis zu der der Projekt-Nullpunkt Bezugspunkt bleibt (siehe Dateikopf).</summary>
    public const double NahGrenzeMm = 5_000_000;

    /// <summary>Beschreibung der Achsen für die Szene (extras) und die Schnittstelle.</summary>
    public const string Achsen = "glTF: x = Ost, y = oben, z = −Nord, Meter; glTF = ((x − bx), (z − bz), −(y − by)) / 1000 mit dem Bezugspunkt b in KICAD-mm";

    /// <summary>KICAD-Punkt (mm) → glTF (m), relativ zum Bezugspunkt.</summary>
    public static (float X, float Y, float Z) Punkt(double x, double y, double z, double[] bezug) =>
        ((float)((x - bezug[0]) / MmJeMeter), (float)((z - bezug[2]) / MmJeMeter), (float)(-(y - bezug[1]) / MmJeMeter));

    /// <summary>KICAD-Richtung → glTF-Richtung (Einheitsvektor; ohne Verschiebung und Massstab). Nullvektor bleibt (0, 1, 0).</summary>
    public static (float X, float Y, float Z) Richtung(double x, double y, double z)
    {
        double l = Math.Sqrt(x * x + y * y + z * z);
        if (l < 1e-300 || !double.IsFinite(l)) return (0, 1, 0);
        return ((float)(x / l), (float)(z / l), (float)(-y / l));
    }

    /// <summary>glTF-Punkt (m) → KICAD (mm); Umkehrung von <see cref="Punkt"/> (für die App und die Tests).</summary>
    public static double[] Zurueck(double gx, double gy, double gz, double[] bezug) =>
        [gx * MmJeMeter + bezug[0], -gz * MmJeMeter + bezug[1], gy * MmJeMeter + bezug[2]];

    /// <summary>Bezugspunkt (mm) aus Begrenzungen [minx, miny, minz, maxx, maxy, maxz] (KICAD-mm): je Achse 0, solange alles
    /// innerhalb ±<see cref="NahGrenzeMm"/> liegt, sonst die Mitte der Ausdehnung auf ganze Meter gerundet.</summary>
    public static double[] Bezugspunkt(IEnumerable<double[]> begrenzungen)
    {
        double[] min = [double.MaxValue, double.MaxValue, double.MaxValue], max = [double.MinValue, double.MinValue, double.MinValue];
        bool etwas = false;
        foreach (var b in begrenzungen)
        {
            if (b is not { Length: >= 6 } || !b.All(double.IsFinite)) continue;
            for (int k = 0; k < 3; k++) { min[k] = Math.Min(min[k], b[k]); max[k] = Math.Max(max[k], b[k + 3]); }
            etwas = true;
        }
        var bezug = new double[3];
        if (!etwas) return bezug;
        for (int k = 0; k < 3; k++)
            if (Math.Max(Math.Abs(min[k]), Math.Abs(max[k])) > NahGrenzeMm)
                bezug[k] = Math.Round((min[k] + max[k]) / 2 / MmJeMeter, MidpointRounding.AwayFromZero) * MmJeMeter;
        return bezug;
    }
}
