// Schnitt und Ansicht (Modul 1a, C1) – sichtbare Teile der Körper in der Ansicht (Sichtteile).
// Ein Sichtteil ist eine ebene senkrechte Körperfläche (Quader, Prisma, Umriss in der Ebene w = 0), ein ganzer Zylinder
// oder eine ganze Kugel – je mit der Projektion als konvexem Umriss in (u, v), den Kanten als räumliche Strecken (u, v, w)
// und der Tiefe w der vorderen Oberfläche an einem Punkt (u, v). Schnittverdeckung prüft damit jeden Kantenpunkt.

namespace Hlks.Kern;

/// <summary>Kante eines Sichtteils als räumliche Strecke. Gerade = ebene Körperkante oder Mantellinie (einzeln, fangbar);
/// sonst Stück einer Kurve (Kreis, Ellipse). Aussen = Normale der Kante in (u, v) aus der Fläche hinaus (nur ebene
/// Flächen; (0, 0) sonst) – für das Erkennen von Nähten zwischen gleich liegenden Flächen.</summary>
internal readonly record struct Strecke(P3 A, P3 B, bool Gerade, P2 Aussen = default);

internal abstract class Sichtteil
{
    /// <summary>Index des Elements in der Liste der gezeigten Körper.</summary>
    public required int Element { get; init; }
    /// <summary>Projektion in (u, v): konvex, Gegenuhrzeigersinn.</summary>
    public required List<P2> Umriss { get; init; }
    /// <summary>Linienfarbe #rrggbb (Medienfarbe) oder null = Planfarbe.</summary>
    public string? Farbe { get; init; }
    public List<Strecke> Strecken { get; } = [];
    /// <summary>[minu, minv, maxu, maxv] des Umrisses.</summary>
    public double[] Box { get; private set; } = [0, 0, 0, 0];
    /// <summary>Gruppe gleich liegender Flächen (gleiche Ebene im Raum, gleiche Zeichenebene) oder −1.</summary>
    public int Gruppe { get; set; } = -1;
    /// <summary>Laufende Nummer in der Liste der Sichtteile (Reihenfolge innerhalb einer Gruppe).</summary>
    public int Nummer { get; set; }

    /// <summary>Tiefe w der vorderen Oberfläche bei (u, v) innerhalb der Sichtschicht; NaN, wenn der Strahl das Teil verfehlt.</summary>
    public abstract double Tiefe(double u, double v);

    public void Begrenzen() => Box = SchnittGeometrie.Box(Umriss);

    /// <summary>Eintritt des Strahls in die Sichtschicht 0 ≤ w ≤ max: max(ein, 0), sofern der Austritt nicht davor liegt.</summary>
    protected static double ImBereich(double ein, double aus, double max)
    {
        double e = Math.Max(ein, 0), a = Math.Min(aus, max);
        return e <= a + 1e-6 ? e : double.NaN;
    }
}

/// <summary>Senkrechte ebene Fläche: Tiefe w = W0 + Steigung · (u − U0). Nu, Nw, Abstand beschreiben ihre Ebene im Raum
/// (Normale nach aussen in (u, w), zum Betrachter hin).</summary>
internal sealed class Flaechenteil : Sichtteil
{
    public required string Ebene { get; init; }
    public double U0 { get; init; }
    public double W0 { get; init; }
    public double Steigung { get; init; }
    public double Nu { get; init; }
    public double Nw { get; init; }
    public double Abstand { get; init; }

    public override double Tiefe(double u, double v) => W0 + Steigung * (u - U0);
}

/// <summary>Endlicher gerader Kreiszylinder (Rohrstück, Armatur), Tiefe exakt aus dem Strahlschnitt.</summary>
internal sealed class Zylinderteil : Sichtteil
{
    public required P3 Anfang { get; init; }
    /// <summary>Einheitsvektor der Achse.</summary>
    public required P3 Richtung { get; init; }
    public required double Laenge { get; init; }
    public required double Radius { get; init; }
    public required double Max { get; init; }

    public override double Tiefe(double u, double v)
    {
        var q = new P3(u - Anfang.U, v - Anfang.V, -Anfang.W);
        double qa = q.Mal(Richtung), da = Richtung.W;          // axial: s(t) = qa + t · da
        var qs = q - Richtung * qa;                              // quer zur Achse
        var ds = new P3(0, 0, 1) - Richtung * da;
        double a = ds.Mal(ds), b = 2 * qs.Mal(ds), c = qs.Mal(qs) - Radius * Radius;
        double ein = double.NegativeInfinity, aus = double.PositiveInfinity;
        if (a < 1e-12)
        {
            if (c > 1e-6) return double.NaN;                     // Strahl parallel zur Achse, ausserhalb
        }
        else
        {
            double disk = b * b - 4 * a * c;
            if (disk < 0) return double.NaN;
            double s = Math.Sqrt(disk);
            ein = (-b - s) / (2 * a); aus = (-b + s) / (2 * a);
        }
        if (Math.Abs(da) < 1e-12)
        {
            if (qa < -1e-6 || qa > Laenge + 1e-6) return double.NaN;
        }
        else
        {
            double t0 = -qa / da, t1 = (Laenge - qa) / da;
            ein = Math.Max(ein, Math.Min(t0, t1));
            aus = Math.Min(aus, Math.Max(t0, t1));
        }
        return ein > aus + 1e-6 ? double.NaN : ImBereich(ein, aus, Max);
    }
}

/// <summary>Kugel (Formteil, Rohrknick).</summary>
internal sealed class Kugelteil : Sichtteil
{
    public required P3 Mitte { get; init; }
    public required double Radius { get; init; }
    public required double Max { get; init; }

    public override double Tiefe(double u, double v)
    {
        double du = u - Mitte.U, dv = v - Mitte.V, h2 = Radius * Radius - du * du - dv * dv;
        if (h2 < 0) return double.NaN;
        double h = Math.Sqrt(h2);
        return ImBereich(Mitte.W - h, Mitte.W + h, Max);
    }
}
