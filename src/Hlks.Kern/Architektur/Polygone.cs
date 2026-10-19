// Polygon-Hilfen der Architektur über Clipper2 (Vereinigen, Abziehen, Flächen mit Löchern, Punkt im Polygon).
// Alle Koordinaten in mm (xy). Rechengenauigkeit 3 Dezimalen = 0.001 mm.

using Clipper2Lib;

namespace Hlks.Kern;

/// <summary>Fläche aus Clipper: Aussenring im Gegenuhrzeigersinn, Löcher im Uhrzeigersinn, je [x, y] in mm.</summary>
public sealed record Flaeche(List<double[]> Aussen, List<List<double[]>> Loecher)
{
    public double Inhalt => Math.Abs(Polygone.Flaecheninhalt(Aussen)) - Loecher.Sum(l => Math.Abs(Polygone.Flaecheninhalt(l)));
}

public static class Polygone
{
    /// <summary>Dezimalen, auf die Clipper rechnet (mm → 0.001 mm).</summary>
    public const int Praezision = 3;

    /// <summary>Kleinere Restflächen (mm²) sind Rundungssplitter und fallen weg.</summary>
    public const double Splitter = 1.0;

    public static PathD Pfad(IEnumerable<double[]> punkte) => new(punkte.Select(p => new PointD(p[0], p[1])));

    public static List<double[]> Punkte(PathD pfad) =>
        pfad.Select(q => new[] { Math.Round(q.x, Praezision), Math.Round(q.y, Praezision) }).ToList();

    /// <summary>Ring im Gegenuhrzeigersinn (positive Fläche); Clipper rechnet mit NonZero, gleiche Drehrichtung ist nötig.</summary>
    public static PathD Ccw(PathD p)
    {
        if (Clipper.Area(p) < 0) { var r = new PathD(p); r.Reverse(); return r; }
        return p;
    }

    static PathsD Ausfuehren(ClipType art, PathsD subjekt, PathsD? clip)
    {
        var c = new ClipperD(Praezision) { PreserveCollinear = false };
        c.AddSubject(subjekt);
        if (clip is { Count: > 0 }) c.AddClip(clip);
        var aus = new PathsD();
        c.Execute(art, FillRule.NonZero, aus);
        aus.RemoveAll(p => Math.Abs(Clipper.Area(p)) < Splitter);
        return aus;
    }

    public static PathsD Vereinigen(PathsD a) => a.Count == 0 ? [] : Ausfuehren(ClipType.Union, a, null);

    public static PathsD Vereinigen(PathsD a, PathsD b) => Ausfuehren(ClipType.Union, [.. a, .. b], null);

    public static PathsD Abziehen(PathsD a, PathsD b) => a.Count == 0 ? [] : b.Count == 0 ? Vereinigen(a) : Ausfuehren(ClipType.Difference, a, b);

    /// <summary>Vereinigung als Baum (Aussenringe → Löcher → Inseln in Löchern …).</summary>
    public static PolyTreeD Baum(PathsD a)
    {
        var c = new ClipperD(Praezision) { PreserveCollinear = false };
        c.AddSubject(a);
        var baum = new PolyTreeD();
        c.Execute(ClipType.Union, FillRule.NonZero, baum);
        return baum;
    }

    /// <summary>Flächen mit Löchern; Inseln in Löchern werden eigene Flächen.</summary>
    public static List<Flaeche> Flaechen(PathsD a)
    {
        var aus = new List<Flaeche>();
        if (a.Count > 0) Sammeln(Baum(a), aus);
        return aus;
    }

    static void Sammeln(PolyPathD knoten, List<Flaeche> aus)
    {
        for (int i = 0; i < knoten.Count; i++)
        {
            var kind = knoten[i];
            if (kind.IsHole) continue;
            if (Math.Abs(Clipper.Area(kind.Polygon!)) < Splitter) continue;
            var loecher = new List<List<double[]>>();
            for (int j = 0; j < kind.Count; j++)
            {
                var loch = kind[j];
                if (Math.Abs(Clipper.Area(loch.Polygon!)) >= Splitter) loecher.Add(Ring(loch.Polygon!, false));
                Sammeln(loch, aus);   // Inseln im Loch
            }
            aus.Add(new Flaeche(Ring(kind.Polygon!, true), loecher));
        }
    }

    /// <summary>Ring als Punktliste mit gewünschter Drehrichtung (true = Gegenuhrzeigersinn).</summary>
    public static List<double[]> Ring(PathD p, bool ccw)
    {
        var pts = Punkte(p);
        if ((Flaecheninhalt(pts) > 0) != ccw) pts.Reverse();
        return pts;
    }

    /// <summary>Vorzeichenbehaftete Fläche (Gegenuhrzeigersinn positiv) in mm².</summary>
    public static double Flaecheninhalt(IReadOnlyList<double[]> ring)
    {
        double s = 0;
        for (int i = 0; i < ring.Count; i++)
        {
            var a = ring[i]; var b = ring[(i + 1) % ring.Count];
            s += a[0] * b[1] - b[0] * a[1];
        }
        return s / 2;
    }

    public static bool Innen(PathD ring, double x, double y) =>
        Clipper.PointInPolygon(new PointD(x, y), ring, Praezision) == PointInPolygonResult.IsInside;

    public static bool Innen(IEnumerable<double[]> ring, double x, double y) => Innen(Pfad(ring), x, y);

    /// <summary>Flächenschwerpunkt eines Rings; bei Fläche 0 der Mittelwert der Punkte.</summary>
    public static double[] Schwerpunkt(IReadOnlyList<double[]> ring)
    {
        double a = 0, cx = 0, cy = 0;
        for (int i = 0; i < ring.Count; i++)
        {
            var p = ring[i]; var q = ring[(i + 1) % ring.Count];
            double f = p[0] * q[1] - q[0] * p[1];
            a += f; cx += (p[0] + q[0]) * f; cy += (p[1] + q[1]) * f;
        }
        if (Math.Abs(a) < 1e-9) return [ring.Average(p => p[0]), ring.Average(p => p[1])];
        return [cx / (3 * a), cy / (3 * a)];
    }

    /// <summary>Punkt sicher im Inneren (für Stempel): Schwerpunkt, wenn er innen liegt, sonst die Mitte des breitesten
    /// Innenabschnitts auf waagrechten Linien durch den Ring.</summary>
    public static double[] InnererPunkt(IReadOnlyList<double[]> ring)
    {
        var s = Schwerpunkt(ring);
        if (Innen(ring, s[0], s[1])) return s;
        double ymin = ring.Min(p => p[1]), ymax = ring.Max(p => p[1]);
        double[]? beste = null; double breite = -1;
        for (int i = 1; i < 20; i++)
        {
            double y = ymin + (ymax - ymin) * i / 20.0;
            var xs = new List<double>();
            for (int j = 0; j < ring.Count; j++)
            {
                var p = ring[j]; var q = ring[(j + 1) % ring.Count];
                if ((p[1] > y) != (q[1] > y)) xs.Add(p[0] + (y - p[1]) * (q[0] - p[0]) / (q[1] - p[1]));
            }
            xs.Sort();
            for (int j = 0; j + 1 < xs.Count; j += 2)
                if (xs[j + 1] - xs[j] > breite) { breite = xs[j + 1] - xs[j]; beste = [(xs[j] + xs[j + 1]) / 2, y]; }
        }
        return beste ?? s;
    }

    /// <summary>Punkt im Ring nach dem Strahlverfahren (wie punktInPolygon in js/ansichten/geometrie.js).</summary>
    public static bool ImRing(IReadOnlyList<double[]> ring, double x, double y)
    {
        bool innen = false;
        for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
        {
            var a = ring[i]; var b = ring[j];
            if ((a[1] > y) != (b[1] > y) && x < (b[0] - a[0]) * (y - a[1]) / (b[1] - a[1]) + a[0]) innen = !innen;
        }
        return innen;
    }

    /// <summary>Mitte für den Raumstempel (Rechteck breite × hoehe): möglichst nahe am Schwerpunkt, ganz im Raum und frei
    /// von den Hindernissen [minx, miny, maxx, maxy]. Übertrag von stempelPosition aus js/ansichten/geometrie.js; findet
    /// sich nichts Freies, ein Punkt sicher im Raum (InnererPunkt).</summary>
    public static double[] StempelLage(IReadOnlyList<double[]> ring, double breite, double hoehe, IReadOnlyList<double[]> hindernisse)
    {
        var s = Schwerpunkt(ring);
        if (ring.Count < 3) return s;
        bool Frei(double cx, double cy)
        {
            double x0 = cx - breite / 2, x1 = cx + breite / 2, y0 = cy - hoehe / 2, y1 = cy + hoehe / 2;
            double[][] ecken = [[x0, y0], [x1, y0], [x1, y1], [x0, y1], [cx, cy]];
            if (!ecken.All(e => ImRing(ring, e[0], e[1]))) return false;
            return !hindernisse.Any(h => x0 < h[2] && x1 > h[0] && y0 < h[3] && y1 > h[1]);
        }
        if (Frei(s[0], s[1])) return s;
        double minx = ring.Min(p => p[0]), maxx = ring.Max(p => p[0]), miny = ring.Min(p => p[1]), maxy = ring.Max(p => p[1]);
        double schritt = Math.Max(Math.Min(breite, hoehe) / 3, 50);
        double[]? beste = null; double bestD = double.MaxValue;
        for (double y = miny; y <= maxy; y += schritt)
            for (double x = minx; x <= maxx; x += schritt)
            {
                double d = Math.Sqrt((x - s[0]) * (x - s[0]) + (y - s[1]) * (y - s[1]));
                if (d < bestD && Frei(x, y)) { beste = [x, y]; bestD = d; }
            }
        return beste ?? InnererPunkt(ring);
    }

    /// <summary>Kürzester Abstand des Punkts p von der Strecke a–b (xy).</summary>
    public static double AbstandZurStrecke(double px, double py, double ax, double ay, double bx, double by)
    {
        double dx = bx - ax, dy = by - ay, l2 = dx * dx + dy * dy;
        double t = l2 < 1e-12 ? 0 : Math.Clamp(((px - ax) * dx + (py - ay) * dy) / l2, 0, 1);
        double qx = ax + t * dx - px, qy = ay + t * dy - py;
        return Math.Sqrt(qx * qx + qy * qy);
    }
}
