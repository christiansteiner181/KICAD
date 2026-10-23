// Quest App P4a (Modul XR): Umrechnung zwischen der Welt der App und den Koordinaten von KICAD.
//
//   App (Szene, wie die GLB aus P2 und alle Regeln in Hlks.Xr): Meter, y oben, x = Ost, z = −Nord, RELATIV zum Bezugspunkt b
//                                                                (scene.extras.kicad.bezugspunktMm bzw. bezugspunktMm der Liste, KICAD-mm).
//   KICAD (Kern, Live-Kanal, Standorte von raum_begehen, Auftrag):  Millimeter, x Ost, y Nord, z oben, absolut.
//
//   KICAD = (x·1000 + bx,  −z·1000 + by,  y·1000 + bz)          App = ((X − bx)/1000,  (Z − bz)/1000,  −(Y − by)/1000)
//
// Die Abbildung der Achsen ist eine Drehung um +90° um x (Matrix M mit den Zeilen [1 0 0], [0 0 −1], [0 1 0], Determinante +1). Eine Drehung R der App
// ist in KICAD M·R·Mᵀ; als Quaternion (x, y, z, w) heisst das: der Vektorteil wird mit M abgebildet, w bleibt:  KICAD = (x, −z, y, w).
// Quelle der Abbildung: src/Hlks.Kern/Xr/XrKoordinaten.cs (Punkt, Zurueck) – hier dieselbe Regel in die Gegenrichtung für die App.

using System.Numerics;

namespace Hlks.Xr.Netz;

public static class KicadUmrechnung
{
    public const double MmJeMeter = 1000;

    static double B(double[]? bezug, int i) => bezug is { Length: >= 3 } ? bezug[i] : 0;

    /// <summary>Punkt der App (m, relativ zum Bezugspunkt) → KICAD (mm, absolut).</summary>
    public static double[] PunktZuKicad(Vec3 p, double[]? bezug) =>
        [p.X * MmJeMeter + B(bezug, 0), -p.Z * MmJeMeter + B(bezug, 1), p.Y * MmJeMeter + B(bezug, 2)];

    /// <summary>KICAD (mm, absolut) → Punkt der App (m, relativ zum Bezugspunkt).</summary>
    public static Vec3 PunktAusKicad(double x, double y, double z, double[]? bezug) =>
        new((x - B(bezug, 0)) / MmJeMeter, (z - B(bezug, 2)) / MmJeMeter, -(y - B(bezug, 1)) / MmJeMeter);

    /// <summary>Drehung der App → KICAD als [x, y, z, w] (normiert; ein Nullquaternion ergibt die Einheit).</summary>
    public static double[] DrehungZuKicad(Quaternion q)
    {
        double x = q.X, y = q.Y, z = q.Z, w = q.W;
        var l = Math.Sqrt(x * x + y * y + z * z + w * w);
        if (!(l > 1e-9) || !double.IsFinite(l)) return [0, 0, 0, 1];
        return [x / l, -z / l, y / l, w / l];
    }

    /// <summary>KICAD [x, y, z, w] → Drehung der App.</summary>
    public static Quaternion DrehungAusKicad(double[] q) => new((float)q[0], (float)q[2], (float)-q[1], (float)q[3]);

    /// <summary>Standort von KICAD (raum_begehen, gemerkt; Projekt-mm absolut) relativ zum Bezugspunkt, damit Begehen.ZustandAusStandort die Szene trifft.</summary>
    public static Standort? StandortRelativ(Standort? s, double[]? bezug)
    {
        if (s is null) return null;
        double bx = B(bezug, 0), by = B(bezug, 1), bz = B(bezug, 2);
        return s with
        {
            Standpunkt = s.Standpunkt is { } p ? p with { X = p.X - bx, Y = p.Y - by, Z = p.Z is { } z ? z - bz : null } : null,
            BodenOk = s.BodenOk is { } b ? b - bz : null,
        };
    }

    /// <summary>Umkehrung von <see cref="StandortRelativ"/>: Standort in Szenen-mm (relativ) → Projekt-mm (absolut).</summary>
    public static Standort? StandortAbsolut(Standort? s, double[]? bezug) =>
        StandortRelativ(s, bezug is { Length: >= 3 } ? [-bezug[0], -bezug[1], -bezug[2]] : null);

    /// <summary>Begehen.ZustandAusStandort mit Bezugspunkt: Standort in Projekt-mm (absolut) → Zustand in der Szene der App.</summary>
    public static BegehenZustand? ZustandAusStandort(Standort? s, double[]? bezug, double? augenhoeheMm = null) =>
        Begehen.ZustandAusStandort(StandortRelativ(s, bezug), augenhoeheMm);

    /// <summary>Sitzungsende in der Szene → absolut (für VrStartLogik.StandpunktMerken, das Projekt-mm ablegt).</summary>
    public static StandpunktEnde EndeAbsolut(StandpunktEnde e, double[]? bezug) =>
        new(e.X + B(bezug, 0) / MmJeMeter, e.Z - B(bezug, 1) / MmJeMeter, e.Boden + B(bezug, 2) / MmJeMeter, e.Gieren);
}
