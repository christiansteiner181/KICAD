// Quest App V1b-1 (Modul XR, Gestenlabor): Dynamic Time Warping zweier gleich langer Merkmalsfolgen (n Stützpunkte × d Werte) mit Sakoe-Chiba-Band.
//   - Kosten eines Paars (i, j) = euklidischer Abstand der beiden Zeilen (die Gewichte stecken schon in den Werten, GestenMerkmale).
//   - Schritte (i−1, j), (i, j−1), (i−1, j−1); nur |i − j| ≤ Band. Ergebnis = Summe entlang des günstigsten Wegs / n (mittlerer Abstand je Stützpunkt).
//   - Vorzeitiger Abbruch: Liegt das Minimum einer Zeile schon über grenze × n, kann das Ergebnis nicht unter grenze fallen (Kosten ≥ 0) → +∞.
//   - Untere Schranke (LB_Keogh, euklidisch je Zeile): Jede Zeile i der Folge a liegt auf dem Weg mindestens einmal einem b[j] mit |i − j| ≤ Band gegenüber; dessen
//     Abstand ist mindestens der Abstand von a[i] zur achsparallelen Hülle dieser b[j]. Die Summe davon / n ist also nie grösser als der DTW-Abstand und kostet nur
//     n × d Schritte statt etwa n × (2·Band + 1) × d – damit fallen die meisten Kandidaten im Live-Erkenner ohne DTW weg.

namespace Hlks.Xr.Gesten;

public static class GestenDtw
{
    /// <summary>Höchstzahl Stützpunkte (Grenze der Puffer auf dem Stapel).</summary>
    public const int NMax = 128;

    /// <summary>
    /// DTW-Abstand (mittlerer Abstand je Stützpunkt) von a und b, je n × d Werte, mit Band. +∞, wenn er sicher über <paramref name="grenze"/> liegt (vorzeitiger Abbruch).
    /// </summary>
    public static double Abstand(ReadOnlySpan<double> a, ReadOnlySpan<double> b, int n, int d, int band, double grenze = double.PositiveInfinity)
    {
        if (n < 1 || n > NMax || a.Length < n * d || b.Length < n * d) throw new ArgumentException("Folgen passen nicht zu n und d.");
        band = Math.Clamp(band, 0, n - 1);
        var abbruch = double.IsPositiveInfinity(grenze) ? double.PositiveInfinity : grenze * n;
        Span<double> vorher = stackalloc double[n];
        Span<double> jetzt = stackalloc double[n];
        vorher.Fill(double.PositiveInfinity);
        for (var i = 0; i < n; i++)
        {
            jetzt.Fill(double.PositiveInfinity);
            int jlo = Math.Max(0, i - band), jhi = Math.Min(n - 1, i + band);
            var zeileMin = double.PositiveInfinity;
            var ai = a.Slice(i * d, d);
            for (var j = jlo; j <= jhi; j++)
            {
                var bj = b.Slice(j * d, d);
                double s = 0;
                for (var k = 0; k < d; k++)
                {
                    var x = ai[k] - bj[k];
                    s += x * x;
                }
                var kosten = Math.Sqrt(s);
                double best;
                if (i == 0 && j == 0) best = 0;
                else
                {
                    best = double.PositiveInfinity;
                    if (i > 0) best = vorher[j];
                    if (j > 0 && jetzt[j - 1] < best) best = jetzt[j - 1];
                    if (i > 0 && j > 0 && vorher[j - 1] < best) best = vorher[j - 1];
                }
                var w = kosten + best;
                jetzt[j] = w;
                if (w < zeileMin) zeileMin = w;
            }
            if (zeileMin > abbruch) return double.PositiveInfinity;
            var tausch = vorher;
            vorher = jetzt;
            jetzt = tausch;
        }
        return vorher[n - 1] / n;
    }

    /// <summary>Hülle von b für die untere Schranke: je Zeile i und Wert k Minimum und Maximum von b[j, k] über |i − j| ≤ Band.</summary>
    public static void Huelle(ReadOnlySpan<double> b, int n, int d, int band, Span<double> unten, Span<double> oben)
    {
        band = Math.Clamp(band, 0, Math.Max(0, n - 1));
        for (var i = 0; i < n; i++)
        {
            int jlo = Math.Max(0, i - band), jhi = Math.Min(n - 1, i + band);
            for (var k = 0; k < d; k++)
            {
                double lo = double.PositiveInfinity, hi = double.NegativeInfinity;
                for (var j = jlo; j <= jhi; j++)
                {
                    var v = b[j * d + k];
                    if (v < lo) lo = v;
                    if (v > hi) hi = v;
                }
                unten[i * d + k] = lo;
                oben[i * d + k] = hi;
            }
        }
    }

    /// <summary>
    /// Untere Schranke des DTW-Abstands von a zur Folge mit dieser Hülle (siehe Dateikopf); +∞, sobald sie über <paramref name="grenze"/> liegt. Gerechnet vom letzten
    /// Stützpunkt her: Die Lage startet in jeder Folge bei null, die Unterschiede liegen am Ende – so bricht die Schranke früher ab.
    /// </summary>
    public static double UntereSchranke(ReadOnlySpan<double> a, ReadOnlySpan<double> unten, ReadOnlySpan<double> oben, int n, int d,
        double grenze = double.PositiveInfinity)
    {
        if (n < 1 || a.Length < n * d || unten.Length < n * d || oben.Length < n * d) throw new ArgumentException("Folgen passen nicht zu n und d.");
        var abbruch = double.IsPositiveInfinity(grenze) ? double.PositiveInfinity : grenze * n;
        double summe = 0;
        for (var i = n - 1; i >= 0; i--)
        {
            double s = 0;
            var o = i * d;
            for (var k = 0; k < d; k++)
            {
                var x = a[o + k];
                var u = unten[o + k];
                var h = oben[o + k];
                var z = x > h ? x - h : x < u ? u - x : 0;
                s += z * z;
            }
            summe += Math.Sqrt(s);
            if (summe > abbruch) return double.PositiveInfinity;
        }
        return summe / n;
    }
}
