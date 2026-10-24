// Tests der Quest App V1b-1 (Modul XR, Gestenlabor): Dynamic Time Warping mit Band, untere Schranke (LB_Keogh) und vorzeitiger Abbruch.

using Hlks.Xr.Gesten;

namespace Hlks.Xr.Tests;

public class GestenDtwTests
{
    /// <summary>Zufallsweg mit n × d Werten (Saat fest).</summary>
    static double[] Weg(Random r, int n, int d)
    {
        var w = new double[n * d];
        for (var k = 0; k < d; k++)
        {
            double x = 0;
            for (var i = 0; i < n; i++) { x += r.NextDouble() - 0.5; w[i * d + k] = x; }
        }
        return w;
    }

    static double Euklid(double[] a, double[] b, int n, int d)
    {
        double s = 0;
        for (var i = 0; i < n; i++)
        {
            double q = 0;
            for (var k = 0; k < d; k++) q += (a[i * d + k] - b[i * d + k]) * (a[i * d + k] - b[i * d + k]);
            s += Math.Sqrt(q);
        }
        return s / n;
    }

    [Fact]
    public void Gleiche_Folgen_haben_Abstand_null_und_der_Abstand_ist_symmetrisch()
    {
        var r = new Random(1);
        var a = Weg(r, 32, 5);
        var b = Weg(r, 32, 5);
        Assert.Equal(0, GestenDtw.Abstand(a, a, 32, 5, 6));
        T.Nah(GestenDtw.Abstand(a, b, 32, 5, 6), GestenDtw.Abstand(b, a, 32, 5, 6), 1e-12);
    }

    [Fact]
    public void Band_null_ist_der_mittlere_euklidische_Abstand_ein_Band_gleicht_Verschiebungen_aus()
    {
        var r = new Random(2);
        var a = Weg(r, 32, 3);
        var b = Weg(r, 32, 3);
        T.Nah(GestenDtw.Abstand(a, b, 32, 3, 0), Euklid(a, b, 32, 3), 1e-12);
        // dieselbe Bewegung zwei Stützpunkte später: DTW mit Band viel kleiner als ohne
        const int n = 32;
        var s1 = new double[n];
        var s2 = new double[n];
        for (var i = 0; i < n; i++) { s1[i] = Math.Sin(i * 0.3); s2[i] = Math.Sin(Math.Max(0, i - 2) * 0.3); }
        var ohne = GestenDtw.Abstand(s1, s2, n, 1, 0);
        var mit = GestenDtw.Abstand(s1, s2, n, 1, 3);
        Assert.True(mit < ohne * 0.3, $"{mit} gegen {ohne}");
        // ein breiteres Band ist nie schlechter
        Assert.True(GestenDtw.Abstand(a, b, 32, 3, 8) <= GestenDtw.Abstand(a, b, 32, 3, 4) + 1e-12);
    }

    [Fact]
    public void Untere_Schranke_ist_nie_groesser_als_der_DTW_Abstand()
    {
        var r = new Random(3);
        foreach (var (n, d, band) in new[] { (32, 13, 6), (24, 29, 3), (16, 4, 0), (32, 2, 31) })
            for (var i = 0; i < 100; i++)
            {
                var a = Weg(r, n, d);
                var b = Weg(r, n, d);
                var unten = new double[n * d];
                var oben = new double[n * d];
                GestenDtw.Huelle(b, n, d, band, unten, oben);
                var lb = GestenDtw.UntereSchranke(a, unten, oben, n, d);
                var dtw = GestenDtw.Abstand(a, b, n, d, band);
                Assert.True(lb <= dtw + 1e-12, $"n={n} d={d} Band={band}: {lb} > {dtw}");
                Assert.Equal(0, GestenDtw.UntereSchranke(b, unten, oben, n, d));   // die Folge selbst liegt in ihrer Hülle
            }
    }

    [Fact]
    public void Vorzeitiger_Abbruch_nur_wenn_das_Ergebnis_ueber_der_Grenze_liegt()
    {
        var r = new Random(4);
        for (var i = 0; i < 100; i++)
        {
            var a = Weg(r, 32, 6);
            var b = Weg(r, 32, 6);
            var voll = GestenDtw.Abstand(a, b, 32, 6, 6);
            Assert.Equal(voll, GestenDtw.Abstand(a, b, 32, 6, 6, voll * 1.01));
            Assert.True(GestenDtw.Abstand(a, b, 32, 6, 6, voll * 0.5) >= voll * 0.5);
            var unten = new double[32 * 6];
            var oben = new double[32 * 6];
            GestenDtw.Huelle(b, 32, 6, 6, unten, oben);
            var lb = GestenDtw.UntereSchranke(a, unten, oben, 32, 6);
            if (lb > 0) Assert.True(double.IsPositiveInfinity(GestenDtw.UntereSchranke(a, unten, oben, 32, 6, lb * 0.5)));
            Assert.Equal(lb, GestenDtw.UntereSchranke(a, unten, oben, 32, 6, lb * 2 + 1));
        }
    }

    [Fact]
    public void Falsche_Laengen_werfen()
    {
        var a = new double[10];
        Assert.Throws<ArgumentException>(() => GestenDtw.Abstand(a, a, 5, 3, 1));
        Assert.Throws<ArgumentException>(() => GestenDtw.Abstand(new double[200], new double[200], 200, 1, 1));
        Assert.Throws<ArgumentException>(() => GestenDtw.UntereSchranke(a, a, a, 5, 3));
    }
}
