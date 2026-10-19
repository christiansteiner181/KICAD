// 4×4-Matrix für Lagen aus IFC (Platzierungen, Achsensysteme). Spaltenvektoren: p' = M · p.
// Bewusst klein gehalten – keine fremde Mathematik-Bibliothek.

namespace Hlks.Kern;

public readonly struct Matrix4
{
    // Zeilenweise: m[r * 4 + c]
    readonly double[] m;
    Matrix4(double[] werte) => m = werte;

    public static readonly Matrix4 Einheit = new([1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]);

    public double this[int r, int c] => (m ?? Einheit.m)[r * 4 + c];

    /// <summary>Achsensystem aus Ursprung o, x-Richtung x und z-Richtung z (normiert und rechtwinklig gemacht).</summary>
    public static Matrix4 Achsen(double[] o, double[]? x, double[]? z)
    {
        var zz = Norm(z ?? [0, 0, 1]);
        var xx = x is null ? (Math.Abs(zz[0]) < 0.9 ? new double[] { 1, 0, 0 } : [0, 1, 0]) : x;
        // x rechtwinklig zu z machen
        double d = xx[0] * zz[0] + xx[1] * zz[1] + xx[2] * zz[2];
        xx = Norm([xx[0] - d * zz[0], xx[1] - d * zz[1], xx[2] - d * zz[2]]);
        var yy = Kreuz(zz, xx);
        return new([xx[0], yy[0], zz[0], o[0], xx[1], yy[1], zz[1], o[1], xx[2], yy[2], zz[2], o[2], 0, 0, 0, 1]);
    }

    public static Matrix4 Verschiebung(double dx, double dy, double dz) => new([1, 0, 0, dx, 0, 1, 0, dy, 0, 0, 1, dz, 0, 0, 0, 1]);

    /// <summary>Drehung um die z-Achse (Grad, Gegenuhrzeigersinn).</summary>
    public static Matrix4 DrehungZ(double grad)
    {
        double w = grad * Math.PI / 180, c = Math.Cos(w), s = Math.Sin(w);
        return new([c, -s, 0, 0, s, c, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]);
    }

    public static Matrix4 Skalierung(double f) => new([f, 0, 0, 0, 0, f, 0, 0, 0, 0, f, 0, 0, 0, 0, 1]);

    public static Matrix4 operator *(Matrix4 a, Matrix4 b)
    {
        var r = new double[16];
        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 4; j++)
            {
                double s = 0;
                for (int k = 0; k < 4; k++) s += a[i, k] * b[k, j];
                r[i * 4 + j] = s;
            }
        return new(r);
    }

    public double[] Punkt(double[] p)
    {
        double x = p[0], y = p.Length > 1 ? p[1] : 0, z = p.Length > 2 ? p[2] : 0;
        return [this[0, 0] * x + this[0, 1] * y + this[0, 2] * z + this[0, 3],
                this[1, 0] * x + this[1, 1] * y + this[1, 2] * z + this[1, 3],
                this[2, 0] * x + this[2, 1] * y + this[2, 2] * z + this[2, 3]];
    }

    public double[] Richtung(double[] v)
    {
        double x = v[0], y = v.Length > 1 ? v[1] : 0, z = v.Length > 2 ? v[2] : 0;
        return [this[0, 0] * x + this[0, 1] * y + this[0, 2] * z,
                this[1, 0] * x + this[1, 1] * y + this[1, 2] * z,
                this[2, 0] * x + this[2, 1] * y + this[2, 2] * z];
    }

    public double[] Ursprung => [this[0, 3], this[1, 3], this[2, 3]];
    public double[] AchseX => [this[0, 0], this[1, 0], this[2, 0]];
    public double[] AchseY => [this[0, 1], this[1, 1], this[2, 1]];
    public double[] AchseZ => [this[0, 2], this[1, 2], this[2, 2]];

    /// <summary>Drehwinkel der x-Achse in der xy-Ebene (Grad, Gegenuhrzeigersinn).</summary>
    public double WinkelZ => Math.Atan2(this[1, 0], this[0, 0]) * 180 / Math.PI;

    /// <summary>Inverse einer starren Bewegung (Drehung + Verschiebung, ohne Skalierung).</summary>
    public Matrix4 InverseStarr()
    {
        var r = new double[16];
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++) r[i * 4 + j] = this[j, i];
        for (int i = 0; i < 3; i++) r[i * 4 + 3] = -(r[i * 4] * this[0, 3] + r[i * 4 + 1] * this[1, 3] + r[i * 4 + 2] * this[2, 3]);
        r[15] = 1;
        return new(r);
    }

    static double[] Norm(double[] v)
    {
        double l = Math.Sqrt(v[0] * v[0] + v[1] * v[1] + (v.Length > 2 ? v[2] * v[2] : 0));
        return l < 1e-12 ? [0, 0, 1] : [v[0] / l, v[1] / l, (v.Length > 2 ? v[2] : 0) / l];
    }

    static double[] Kreuz(double[] a, double[] b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
}
