// HLKS-Grafik, Hilfen: Sinnbilder der Apparate und Geräte als Primitive. Übertrag von js/ansichten/sinnbilder.js
// (APPARATE, GERAETE, VORGABE_APPARAT) – gleiche Koordinaten um den Einfügepunkt (Mitte), dort mit y nach unten (SVG),
// hier mit y nach oben (Modell): die Umrechnung steckt in <see cref="Skizze"/>.
// Rechtecke mit Rundung, Ellipsen und Kurven gibt es in der Plangrafik nicht; sie werden als Polygone aus kurzen
// Strecken angenähert (Rundung 6 Strecken je Viertelkreis, Ellipse 48 Strecken, Kurve 10 Strecken).
// Die Masse b/t/h kommen aus dem Katalog (Klassenattribut «darstellung»); die Zeichen selbst sind wie heute fest.

namespace Hlks.Kern;

/// <summary>Sammelt Primitive in lokalen Koordinaten (Mitte = Einfügepunkt, y nach unten wie im SVG der Oberfläche) und gibt
/// sie in Modell-mm (y nach oben) aus. Alles, was «gefüllt» ist, bekommt die Hintergrundfüllung «papier» (CSS gr-fuell).</summary>
public sealed class Skizze(double cx, double cy, string stift, string? farbe, string linie = "voll")
{
    public List<Primitiv> Primitive { get; } = [];

    Stil Stil(bool fuellen) => new() { Stift = stift, Farbe = farbe, Linie = linie, Fuellung = fuellen ? "papier" : null };
    double[] P(double x, double y) => [cx + x, cy - y];

    void Polygon(IEnumerable<(double X, double Y)> pts, bool fuellen) =>
        Primitive.Add(new Primitiv { Art = "polygon", Punkte = pts.Select(p => P(p.X, p.Y)).ToList(), Stil = Stil(fuellen) });

    public Skizze Rechteck(double x, double y, double b, double t, double rx = 0, bool fuellen = true)
    {
        // SVG: rx begrenzt auf die halbe Breite, ry = rx begrenzt auf die halbe Höhe
        var r = Math.Min(rx, Math.Min(b / 2, t / 2));
        if (r <= 0) { Polygon([(x, y), (x + b, y), (x + b, y + t), (x, y + t)], fuellen); return this; }
        var pts = new List<(double, double)>();
        void Ecke(double mx, double my, double von)
        {
            for (int i = 0; i <= 6; i++)
            {
                var w = (von + 90.0 * i / 6) * Math.PI / 180;
                pts.Add((mx + r * Math.Cos(w), my + r * Math.Sin(w)));
            }
        }
        Ecke(x + b - r, y + r, -90);      // oben rechts (SVG: y nach unten, Winkel im lokalen System)
        Ecke(x + b - r, y + t - r, 0);    // unten rechts
        Ecke(x + r, y + t - r, 90);       // unten links
        Ecke(x + r, y + r, 180);          // oben links
        Polygon(pts, fuellen);
        return this;
    }

    public Skizze Ellipse(double mx, double my, double rx, double ry, bool fuellen = true)
    {
        const int n = 48;
        Polygon(Enumerable.Range(0, n).Select(i => (mx + rx * Math.Cos(2 * Math.PI * i / n), my + ry * Math.Sin(2 * Math.PI * i / n))), fuellen);
        return this;
    }

    public Skizze Kreis(double mx, double my, double r, bool fuellen = true)
    {
        Primitive.Add(new Primitiv { Art = "kreis", Punkte = [P(mx, my)], Radius = r, Stil = Stil(fuellen) });
        return this;
    }

    public Skizze Linie(double x1, double y1, double x2, double y2)
    {
        Primitive.Add(new Primitiv { Art = "linie", Punkte = [P(x1, y1), P(x2, y2)], Stil = Stil(false) });
        return this;
    }

    /// <summary>Geschlossener Pfad aus Eckpunkten und quadratischen Kurven (SVG «Q»): Segmente (x, y) oder (qx, qy, x, y).</summary>
    public Skizze Pfad(double startX, double startY, params double[][] segmente)
    {
        var pts = new List<(double, double)> { (startX, startY) };
        foreach (var s in segmente)
        {
            var (x0, y0) = pts[^1];
            if (s.Length == 2) pts.Add((s[0], s[1]));
            else
                for (int i = 1; i <= 10; i++)
                {
                    double t = i / 10.0, u = 1 - t;
                    pts.Add((u * u * x0 + 2 * u * t * s[0] + t * t * s[2], u * u * y0 + 2 * u * t * s[1] + t * t * s[3]));
                }
        }
        Polygon(pts, true);
        return this;
    }
}

public static class HlksSinnbilder
{
    /// <summary>Zeichen eines Apparats oder Geräts um (cx, cy). Rolle quelle/senke/zentrale: Gerät; sonst Apparat nach Klasse
    /// (WC, Waschtisch, Dusche, Badewanne, Spueltisch, Waschmaschine, Bodenablauf), alles andere: Rechteck b × t.</summary>
    public static List<Primitiv> Zeichen(string klasse, string? rolle, double b, double t, double cx, double cy, string stift, string? farbe, string linie)
    {
        var s = new Skizze(cx, cy, stift, farbe, linie);
        switch (rolle is "quelle" or "senke" or "zentrale" ? rolle : klasse)
        {
            // Geräte (rolle quelle, senke, zentrale)
            case "quelle":
                s.Kreis(0, 0, 250).Pfad(-90, -110, [130, 0], [-90, 110]);
                break;
            case "senke":
                s.Kreis(0, 0, 300).Kreis(0, 0, 200, fuellen: false).Linie(-141, -141, 141, 141);
                break;
            case "zentrale":
                s.Rechteck(-350, -350, 700, 700).Linie(-350, -350, 350, 350).Linie(-350, 350, 350, -350);
                break;
            // Apparate
            case "WC":
                s.Rechteck(-200, -350, 400, 180, 30)
                 .Pfad(-190, -170, [190, -170], [190, 40], [190, 300, 0, 300], [-190, 300, -190, 40])
                 .Ellipse(0, 50, 120, 160, fuellen: false);
                break;
            case "Waschtisch":
                s.Rechteck(-300, -225, 600, 450, 40).Ellipse(0, 25, 220, 150, fuellen: false).Kreis(0, -165, 18);
                break;
            case "Dusche":
                s.Rechteck(-450, -450, 900, 900).Linie(-450, -450, 450, 450).Linie(-450, 450, 450, -450).Kreis(0, 0, 45);
                break;
            case "Badewanne":
                s.Rechteck(-850, -375, 1700, 750, 70).Rechteck(-770, -295, 1540, 590, 230, fuellen: false).Kreis(-640, 0, 35);
                break;
            case "Spueltisch":
                s.Rechteck(-600, -300, 1200, 600)
                 .Rechteck(-540, -240, 440, 400, 30, fuellen: false)
                 .Rechteck(-60, -240, 440, 400, 30, fuellen: false)
                 .Rechteck(430, -240, 140, 400, fuellen: false)
                 .Kreis(-110, -265, 18);
                break;
            case "Waschmaschine":
                s.Rechteck(-300, -300, 600, 600, 20).Kreis(0, 40, 190, fuellen: false).Kreis(0, 40, 120, fuellen: false).Rechteck(-250, -270, 150, 60);
                break;
            case "Bodenablauf":
                s.Rechteck(-100, -100, 200, 200).Kreis(0, 0, 70, fuellen: false).Linie(-70, 0, 70, 0).Linie(0, -70, 0, 70);
                break;
            default:
                s.Rechteck(-b / 2, -t / 2, b, t);   // Vorgabe: Rechteck b × t (VORGABE_APPARAT: 500 × 400)
                break;
        }
        return s.Primitive;
    }

    /// <summary>Nur der Umriss (Detailgrad grob): Rechteck b × t um (cx, cy).</summary>
    public static List<Primitiv> Umriss(double b, double t, double cx, double cy, string stift, string? farbe, string linie) =>
        new Skizze(cx, cy, stift, farbe, linie).Rechteck(-b / 2, -t / 2, b, t).Primitive;
}
