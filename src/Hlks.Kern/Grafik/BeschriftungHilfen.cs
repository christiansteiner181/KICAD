// Kleine Vektor- und Primitivhilfen für die Beschriftungsgrafik (nur 2D, Modell-mm der Ansicht).

namespace Hlks.Kern;

internal static class BeschriftungHilfen
{
    /// <summary>Punkte der Geometrie als [x, y] (die dritte Koordinate gehört nicht zur 2D-Beschriftung).</summary>
    public static List<double[]> Punkte2D(Geometrie g) => g.Punkte.Select(p => new[] { p[0], p[1] }).ToList();

    public static double[] Plus(double[] a, double[] b) => [a[0] + b[0], a[1] + b[1]];
    public static double[] Minus(double[] a, double[] b) => [a[0] - b[0], a[1] - b[1]];
    public static double[] Mal(double[] a, double f) => [a[0] * f, a[1] * f];
    public static double Punktprodukt(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1];
    public static double Laenge(double[] a) => Math.Sqrt(a[0] * a[0] + a[1] * a[1]);
    /// <summary>Einheitsvektor; bei Nulllänge die Vorgabe.</summary>
    public static double[] Einheit(double[] a, double[]? vorgabe = null)
    {
        var l = Laenge(a);
        return l < 1e-9 ? vorgabe ?? [1, 0] : [a[0] / l, a[1] / l];
    }
    /// <summary>Normale links der Richtung (90° gegen den Uhrzeigersinn).</summary>
    public static double[] Links(double[] d) => [-d[1], d[0]];
    /// <summary>Vektor um Winkel (Grad, Gegenuhrzeigersinn) gedreht.</summary>
    public static double[] Drehen(double[] v, double grad)
    {
        double w = grad * Math.PI / 180, c = Math.Cos(w), s = Math.Sin(w);
        return [v[0] * c - v[1] * s, v[0] * s + v[1] * c];
    }

    public static Primitiv Linie(double[] a, double[] b, string? stift, string art = "voll") =>
        new() { Art = "linie", Punkte = [a, b], Stil = new Stil { Stift = stift, Linie = art } };

    public static Primitiv Polylinie(List<double[]> pts, string? stift, string art = "voll") =>
        new() { Art = "polylinie", Punkte = pts, Stil = new Stil { Stift = stift, Linie = art } };

    public static Primitiv Polygon(List<double[]> pts, string? stift, string? fuellung = null) =>
        new() { Art = "polygon", Punkte = pts, Stil = new Stil { Stift = stift, Fuellung = fuellung } };

    /// <summary>Text auf dem Plan; Schrifthöhe in Papier-mm. NurBildschirm bleibt false: alles druckt.</summary>
    public static Primitiv Text(double[] p, string text, double groesse, string ausrichtung = "links", double winkel = 0, bool fett = false) =>
        new()
        {
            Art = "text", Punkte = [p], Text = text, Groesse = groesse, Ausrichtung = ausrichtung, Winkel = winkel,
            Fett = fett ? true : null,
        };

    /// <summary>Winkel (Grad) so normieren, dass die Schrift von unten bzw. von rechts lesbar ist: Bereich (−90, 90].</summary>
    public static double LesbarerWinkel(double grad)
    {
        grad %= 360;
        if (grad > 180) grad -= 360;
        if (grad <= -180) grad += 360;
        if (grad > 90 + 1e-9) grad -= 180;
        else if (grad <= -90 + 1e-9) grad += 180;
        return grad;
    }
}
