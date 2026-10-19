// HLKS-Grafik, Hilfen: Linienzüge in der Ebene (Länge, Mitte, Strichelung) und DN-gerechte Bänder.
//
// Leitungen sind in der Oberfläche heute so breit wie ihre Nennweite (Strichbreite = max(0.35 mm Papier, 0.6 × DN)).
// Die Plangrafik kennt nur drei Stifte (0.18 / 0.35 / 0.70 mm). Deshalb gilt:
//   – Strichbreite bis 0.35 mm Papier: Polylinie mit Stift «mittel».
//   – breiter: «Band» = ein Polygon je Segment (Füllung in Medienfarbe, ohne Kontur) und ein Kreis an jedem Knick
//     (runde Verbindung wie stroke-linejoin round, Enden stumpf wie stroke-linecap butt). Gestrichelte Bänder
//     werden hier in Stücke geschnitten, weil die Linienart nur am Stift hängt.
// Modul 3 (B4): Versatz = Parallelkurve (Ränder des Bandes als Linien) für Dämmkontur und «oberhalb der Schnittebene».

namespace Hlks.Kern;

public static class HlksLinien
{
    const double Eps = 1e-6;

    /// <summary>Nur [x, y]; aufeinanderfolgende gleiche Punkte entfallen (Senkrechtstücke einer Leitung haben im Grundriss
    /// keine Länge).</summary>
    public static List<double[]> OhneDoppelte(IEnumerable<double[]> punkte)
    {
        var aus = new List<double[]>();
        foreach (var p in punkte)
            if (aus.Count == 0 || Math.Abs(p[0] - aus[^1][0]) > Eps || Math.Abs(p[1] - aus[^1][1]) > Eps) aus.Add([p[0], p[1]]);
        return aus;
    }

    /// <summary>Länge in der Ebene (x, y) in mm.</summary>
    public static double Laenge(IReadOnlyList<double[]> pts)
    {
        double s = 0;
        for (int i = 1; i < pts.Count; i++) s += Math.Sqrt(Math.Pow(pts[i][0] - pts[i - 1][0], 2) + Math.Pow(pts[i][1] - pts[i - 1][1], 2));
        return s;
    }

    /// <summary>Punkt bei Lauflänge s mit Richtungswinkel in Grad (y nach oben), wie punktBeiLaenge in geometrie.js.</summary>
    public static (double X, double Y, double Winkel)? PunktBeiLaenge(IReadOnlyList<double[]> pts, double s)
    {
        double rest = s;
        double[]? a = null; double dx = 0, dy = 0, l = 0;
        for (int i = 1; i < pts.Count; i++)
        {
            var ddx = pts[i][0] - pts[i - 1][0]; var ddy = pts[i][1] - pts[i - 1][1];
            var ll = Math.Sqrt(ddx * ddx + ddy * ddy);
            if (ll < Eps) continue;
            a = pts[i - 1]; dx = ddx; dy = ddy; l = ll;
            if (rest <= ll) return (a[0] + dx * rest / ll, a[1] + dy * rest / ll, Math.Atan2(dy, dx) * 180 / Math.PI);
            rest -= ll;
        }
        if (a is not null) return (a[0] + dx, a[1] + dy, Math.Atan2(dy, dx) * 180 / Math.PI);
        return pts.Count > 0 ? (pts[0][0], pts[0][1], 0) : null;
    }

    /// <summary>Winkel so drehen, dass Text nie auf dem Kopf steht: Ergebnis in (−90°, 90°] (lesbarerWinkel in geometrie.js).</summary>
    public static double LesbarerWinkel(double grad)
    {
        while (grad > 90) grad -= 180;
        while (grad <= -90) grad += 180;
        return grad;
    }

    /// <summary>Schneidet den Linienzug in Striche (an) und Lücken (aus), Längen in Modell-mm; die Strichelung läuft über
    /// die Knicke hinweg wie in SVG. Jeder Strich ist ein Linienzug.</summary>
    public static List<List<double[]>> Striche(IReadOnlyList<double[]> pts, double an, double aus)
    {
        var ergebnis = new List<List<double[]>>();
        an = Math.Max(an, 1e-3); aus = Math.Max(aus, 1e-3);
        bool ein = true; double rest = an;
        List<double[]>? aktuell = null;
        for (int i = 1; i < pts.Count; i++)
        {
            var a = pts[i - 1]; var b = pts[i];
            var dx = b[0] - a[0]; var dy = b[1] - a[1];
            var l = Math.Sqrt(dx * dx + dy * dy);
            if (l < Eps) continue;
            if (ein && aktuell is null) aktuell = [[a[0], a[1]]];
            double s = 0;
            while (l - s > rest)
            {
                s += rest;
                double[] p = [a[0] + dx * s / l, a[1] + dy * s / l];
                if (ein) { aktuell!.Add(p); ergebnis.Add(aktuell); aktuell = null; }
                else aktuell = [p];
                ein = !ein; rest = ein ? an : aus;
            }
            rest -= l - s;
            if (ein) aktuell!.Add([b[0], b[1]]);
        }
        if (ein && aktuell is { Count: >= 2 }) ergebnis.Add(aktuell);
        return ergebnis;
    }

    /// <summary>Band der Breite w (Modell-mm) um den Linienzug: Polygon je Segment, Kreis an jedem Knick; ohne Kontur,
    /// gefüllt mit <paramref name="fuellung"/> (#rrggbb oder «schnitt» für die Planfarbe).</summary>
    public static List<Primitiv> Band(IReadOnlyList<double[]> pts, double w, string fuellung)
    {
        var aus = new List<Primitiv>();
        var h = w / 2;
        Stil Fl() => new() { Stift = null, Fuellung = fuellung };
        for (int i = 1; i < pts.Count; i++)
        {
            var a = pts[i - 1]; var b = pts[i];
            var dx = b[0] - a[0]; var dy = b[1] - a[1];
            var l = Math.Sqrt(dx * dx + dy * dy);
            if (l < Eps) continue;
            double nx = -dy / l * h, ny = dx / l * h;
            aus.Add(new Primitiv
            {
                Art = "polygon",
                Punkte = [[a[0] + nx, a[1] + ny], [b[0] + nx, b[1] + ny], [b[0] - nx, b[1] - ny], [a[0] - nx, a[1] - ny]],
                Stil = Fl(),
            });
            if (i < pts.Count - 1)
                aus.Add(new Primitiv { Art = "kreis", Punkte = [[b[0], b[1]]], Radius = h, Stil = Fl() });
        }
        return aus;
    }

    /// <summary>Gestricheltes Band mit dem stroke-dasharray der Oberfläche: Striche <c>max(3M, 3w)</c>, Lücken
    /// <c>max(1.5M, 1.5w)</c> (M = Massstabsnenner, in Modell-mm).</summary>
    public static List<Primitiv> BandGestrichelt(IReadOnlyList<double[]> pts, double w, string fuellung, double massstab)
    {
        double an = Math.Max(3 * massstab, 3 * w), aus = Math.Max(1.5 * massstab, 1.5 * w);
        // Schutz vor übergrossen Mengen (extrem lange Leitung bei sehr kleinem Massstab): dann durchgezogen
        if (Laenge(pts) / (an + aus) > 20000) return Band(pts, w, fuellung);
        var liste = new List<Primitiv>();
        foreach (var strich in Striche(pts, an, aus)) liste.AddRange(Band(strich, w, fuellung));
        return liste;
    }

    /// <summary>Parallelkurve im Abstand d (positiv = links der Laufrichtung) mit Gehrung an den Knicken (Modul 3: Ränder eines
    /// Bandes als Linien, z. B. gestrichelt oberhalb der Schnittebene oder als Dämmkontur). Die Gehrungslänge ist auf das
    /// Vierfache des Abstands begrenzt (sehr spitze Knicke werden abgeflacht).</summary>
    public static List<double[]> Versatz(IReadOnlyList<double[]> pts, double d)
    {
        var p = OhneDoppelte(pts);
        var aus = new List<double[]>();
        if (p.Count < 2) return aus;
        var n = new List<double[]>();
        for (int i = 1; i < p.Count; i++)
        {
            var dx = p[i][0] - p[i - 1][0]; var dy = p[i][1] - p[i - 1][1];
            var l = Math.Sqrt(dx * dx + dy * dy);
            n.Add([-dy / l, dx / l]);
        }
        for (int i = 0; i < p.Count; i++)
        {
            if (i == 0 || i == p.Count - 1)
            {
                var nn = i == 0 ? n[0] : n[^1];
                aus.Add([p[i][0] + nn[0] * d, p[i][1] + nn[1] * d]);
                continue;
            }
            var a = n[i - 1]; var b = n[i];
            var nenner = Math.Max(1 + a[0] * b[0] + a[1] * b[1], 0.25);
            aus.Add([p[i][0] + (a[0] + b[0]) / nenner * d, p[i][1] + (a[1] + b[1]) / nenner * d]);
        }
        return aus;
    }
}
