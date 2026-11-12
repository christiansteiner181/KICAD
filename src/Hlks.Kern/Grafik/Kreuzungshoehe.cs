// Höhenlage zweier Achsen an ihrer Kreuzung im Grundriss (Gesamtprüfung 13.11.2026). Entscheid Christian 12.11.2026: «leitungen die in einem
// Grundriss unter einer anderen Leitung durchlaufen (inkl. Dämmung) müssen verdeckt dargestellt werden» – massgebend ist die Höhe an der Stelle der
// Kreuzung, nicht die Höhenspanne der ganzen Leitung (eine Abwasserleitung mit Gefälle bzw. eine Leitung mit Etage überdeckt sonst nie etwas).
// Verfahren je Paar von Teilstücken (Achse a, Achse b, Radius r = halbe Breite a + halbe Breite b):
//   Bereich von a = der Abschnitt von a, der im Grundriss höchstens r neben b liegt (dort überlappen die Bänder), ebenso der Bereich von b.
//   b liegt höher, wenn die tiefste Achshöhe von b in seinem Bereich mehr als 1 mm über der höchsten von a in dessen Bereich liegt.
//   Sonst gleiche Lage (gleich hoch bzw. die Höhen schneiden sich im Kreuzungsbereich) – nichts wird verdeckt.
// Kreuzen sich die Achsen, ist der Bereich das kurze Stück um den Schnittpunkt (Länge 2·r/sin Kreuzungswinkel); laufen sie parallel übereinander, ist
// es die ganze überlappende Länge. Der Abstand eines Punkts von a zur Strecke b ist entlang a konvex – der Bereich ist ein einziges Intervall.

using Clipper2Lib;

namespace Hlks.Kern;

/// <summary>Gerades Teilstück einer Achse (Welt, mm). Spanne: die Höhe gilt nicht linear von A nach B, sondern überall als ganze Spanne Az…Bz
/// (Formteile: gleiche Lage wie die angeschlossene Leitung, auch wenn die Achse im Formteil steigt).</summary>
internal readonly record struct Achsstueck(double Ax, double Ay, double Az, double Bx, double By, double Bz, bool Spanne = false);

internal static class Kreuzungshoehe
{
    public const double Eps = 1.0;   // mm Achshöhe

    /// <summary>Abstand im Grundriss vom Punkt von s beim Parameter t (0…1) zur Strecke d.</summary>
    static double Abstand(in Achsstueck s, double t, in Achsstueck d)
    {
        double px = s.Ax + t * (s.Bx - s.Ax), py = s.Ay + t * (s.By - s.Ay);
        double dx = d.Bx - d.Ax, dy = d.By - d.Ay, l2 = dx * dx + dy * dy;
        double u = l2 < 1e-12 ? 0 : Math.Clamp(((px - d.Ax) * dx + (py - d.Ay) * dy) / l2, 0, 1);
        double ex = px - (d.Ax + u * dx), ey = py - (d.Ay + u * dy);
        return Math.Sqrt(ex * ex + ey * ey);
    }

    /// <summary>Höhenspanne von s über dem Abschnitt, der im Grundriss höchstens r neben d liegt; null, wenn s nirgends so nah an d liegt.</summary>
    public static (double Min, double Max)? Bereich(in Achsstueck s, in Achsstueck d, double r)
    {
        // Vorfilter über die Begrenzungsrechtecke
        if (Math.Min(s.Ax, s.Bx) > Math.Max(d.Ax, d.Bx) + r || Math.Max(s.Ax, s.Bx) < Math.Min(d.Ax, d.Bx) - r
            || Math.Min(s.Ay, s.By) > Math.Max(d.Ay, d.By) + r || Math.Max(s.Ay, s.By) < Math.Min(d.Ay, d.By) - r) return null;
        // nächster Punkt (Abstand konvex in t): Dreiteilung
        double lo = 0, hi = 1;
        for (int i = 0; i < 40; i++)
        {
            double m1 = lo + (hi - lo) / 3, m2 = hi - (hi - lo) / 3;
            if (Abstand(s, m1, d) <= Abstand(s, m2, d)) hi = m2; else lo = m1;
        }
        double tm = (lo + hi) / 2;
        if (Abstand(s, tm, d) > r) return null;
        double t0 = Grenze(s, d, r, 0, tm), t1 = Grenze(s, d, r, 1, tm);
        if (s.Spanne) return (Math.Min(s.Az, s.Bz), Math.Max(s.Az, s.Bz));
        double z0 = s.Az + t0 * (s.Bz - s.Az), z1 = s.Az + t1 * (s.Bz - s.Az);
        return (Math.Min(z0, z1), Math.Max(z0, z1));
    }

    /// <summary>Rand des Bereichs zwischen «aussen» (0 bzw. 1) und dem nächsten Punkt «innen» (dort Abstand ≤ r): Halbierung.</summary>
    static double Grenze(in Achsstueck s, in Achsstueck d, double r, double aussen, double innen)
    {
        if (Abstand(s, aussen, d) <= r) return aussen;
        for (int i = 0; i < 30; i++)
        {
            double m = (aussen + innen) / 2;
            if (Abstand(s, m, d) <= r) innen = m; else aussen = m;
        }
        return innen;
    }

    /// <summary>Lage von b gegenüber a an ihrer Kreuzung: +1 = b liegt höher (verdeckt a), −1 = a liegt höher, 0 = gleiche Lage;
    /// null = die Bänder überlappen im Grundriss nicht (r = Summe der halben Breiten).</summary>
    public static int? Lage(in Achsstueck a, in Achsstueck b, double r)
    {
        if (Bereich(a, b, r) is not { } za || Bereich(b, a, r) is not { } zb) return null;
        return zb.Min > za.Max + Eps ? 1 : za.Min > zb.Max + Eps ? -1 : 0;
    }

    /// <summary>Alle überlappenden Paare (Index in a, Index in b, Lage von b gegenüber a).</summary>
    public static List<(int I, int J, int Lage)> Paare(IReadOnlyList<Achsstueck> a, IReadOnlyList<Achsstueck> b, double r)
    {
        var aus = new List<(int, int, int)>();
        for (int i = 0; i < a.Count; i++)
            for (int j = 0; j < b.Count; j++)
                if (Lage(a[i], b[j], r) is int l) aus.Add((i, j, l));
        return aus;
    }

    /// <summary>Fläche im Abstand r um ein Teilstück (runde Enden).</summary>
    static PathsD Rund(in Achsstueck s, double r) =>
        Math.Abs(s.Bx - s.Ax) + Math.Abs(s.By - s.Ay) < 1e-6
            ? [Clipper.Ellipse(new PointD(s.Ax, s.Ay), r, r, 36)]
            : Clipper.InflatePaths(new PathsD { new PathD { new(s.Ax, s.Ay), new(s.Bx, s.By) } }, r, JoinType.Round, EndType.Round, 2, 2);

    /// <summary>Der Teil der Fläche einer Leitung, der zum Teilstück oben (Radius rOben) gehört und über dem Teilstück unten (Radius rUnten) liegt –
    /// für Leitungen, die eine andere an einer Stelle über- und an einer anderen unterqueren.</summary>
    public static PathsD Ausschnitt(PathsD flaeche, in Achsstueck oben, double rOben, in Achsstueck unten, double rUnten) =>
        Clipper.Intersect(Clipper.Intersect(flaeche, Rund(oben, rOben), FillRule.NonZero), Rund(unten, rUnten), FillRule.NonZero);
}
