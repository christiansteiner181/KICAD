// 2D-CAD «Zeichnung»: Geometrie ohne Bezug zur Datei – Bogen aus Ausbuchtung, Ellipse und Spline als Punktfolge, Länge und Fläche
// von Polylinien mit Bögen (exakt), Schraffurlinien eines Musters auf Ringe zugeschnitten.

using ACadSharp.Entities;
using CSMath;

namespace Hlks.Kern;

public static class ZeichnungGeometrie
{
    /// <summary>Bogen einer Polylinienstrecke mit Ausbuchtung (bulge = tan(Öffnungswinkel/4), > 0 Gegenuhrzeigersinn):
    /// Mittelpunkt, Radius, Winkel am Anfangs- und Endpunkt (Bogenmass).</summary>
    public static (double Cx, double Cy, double R, double W1, double W2) BogenAusBulge(double ax, double ay, double bx, double by, double bulge)
    {
        double dx = bx - ax, dy = by - ay, c = Math.Sqrt(dx * dx + dy * dy);
        double r = c * (bulge * bulge + 1) / (4 * Math.Abs(bulge)), pfeil = Math.Abs(bulge) * c / 2;
        double mx = (ax + bx) / 2, my = (ay + by) / 2, vz = Math.Sign(bulge) * (r - pfeil);
        double cx = mx + -dy / c * vz, cy = my + dx / c * vz;
        return (cx, cy, r, Math.Atan2(ay - cy, ax - cx), Math.Atan2(by - cy, bx - cx));
    }

    /// <summary>Vorzeichenbehaftete Fläche eines Rings (Gegenuhrzeigersinn positiv).</summary>
    public static double Flaeche(IReadOnlyList<(double X, double Y)> ring)
    {
        double a = 0;
        for (int i = 0; i < ring.Count; i++)
        {
            var p = ring[i]; var q = ring[(i + 1) % ring.Count];
            a += p.X * q.Y - q.X * p.Y;
        }
        return a / 2;
    }

    /// <summary>Länge und (bei geschlossener Polylinie) Fläche einer Polylinie mit Ausbuchtungen, exakt über die Kreisabschnitte.</summary>
    public static (double Laenge, double? Flaeche) Polylinie(IReadOnlyList<(double X, double Y, double Bulge)> v, bool geschlossen)
    {
        if (v.Count < 2) return (0, null);
        double laenge = 0, flaeche = 0;
        int n = geschlossen ? v.Count : v.Count - 1;
        for (int i = 0; i < n; i++)
        {
            var a = v[i]; var b = v[(i + 1) % v.Count];
            double c = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
            flaeche += (a.X * b.Y - b.X * a.Y) / 2;
            if (a.Bulge == 0 || c == 0) { laenge += c; continue; }
            double winkel = 4 * Math.Atan(Math.Abs(a.Bulge)), r = c / (2 * Math.Sin(winkel / 2));
            laenge += r * winkel;
            flaeche += Math.Sign(a.Bulge) * r * r / 2 * (winkel - Math.Sin(winkel));   // Kreisabschnitt zwischen Sehne und Bogen
        }
        return (laenge, geschlossen && v.Count >= 3 ? Math.Abs(flaeche) : null);
    }

    public static (List<(double X, double Y)> Punkte, bool Voll) EllipsePunkte(Ellipse el)
    {
        var a = el.MajorAxisEndPoint;
        double ax = a.X, ay = a.Y, r = el.RadiusRatio;
        // Nebenachse senkrecht zur Hauptachse; bei Normale −z läuft der Parameter im Uhrzeigersinn
        double bx = (el.Normal.Z < 0 ? ay : -ay) * r, by = (el.Normal.Z < 0 ? -ax : ax) * r;
        double t0 = el.StartParameter, t1 = el.EndParameter;
        if (el.IsFullEllipse) { t0 = 0; t1 = 2 * Math.PI; } else if (t1 <= t0) t1 += 2 * Math.PI;
        int n = Math.Max(8, (int)Math.Ceiling(64 * (t1 - t0) / (2 * Math.PI)));
        var pts = new List<(double, double)>(n + 1);
        for (int i = 0; i <= n; i++)
        {
            double w = t0 + (t1 - t0) * i / n;
            pts.Add((el.Center.X + Math.Cos(w) * ax + Math.Sin(w) * bx, el.Center.Y + Math.Cos(w) * ay + Math.Sin(w) * by));
        }
        bool voll = el.IsFullEllipse || Math.Abs(t1 - t0 - 2 * Math.PI) < 1e-9;
        if (voll) pts.RemoveAt(pts.Count - 1);
        return (pts, voll);
    }

    public static List<(double X, double Y)> SplinePunkte(Spline sp)
    {
        List<XYZ>? punkte = null;
        try { punkte = sp.PolygonalVertexes(Math.Clamp((sp.ControlPoints.Count + sp.FitPoints.Count) * 8, 32, 400)); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { }
        if (punkte is { Count: >= 2 } && sp.ControlPoints.Count > 0) return punkte.Select(v => (v.X, v.Y)).ToList();
        // Spline nur mit Stützpunkten (so schreibt ihn die Bibliothek ins DWG): die Kurve an einer Kopie aus den Stützpunkten rechnen
        if (sp.FitPoints.Count >= 3)
        {
            try
            {
                var kopie = new Spline { Degree = sp.Degree > 0 ? sp.Degree : 3 };
                kopie.FitPoints.AddRange(sp.FitPoints);
                if (kopie.UpdateFromFitPoints(100) && kopie.PolygonalVertexes(Math.Clamp(sp.FitPoints.Count * 12, 32, 400)) is { Count: >= 2 } kurve)
                    return kurve.Select(v => (v.X, v.Y)).ToList();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException) { }
        }
        if (punkte is { Count: >= 2 }) return punkte.Select(v => (v.X, v.Y)).ToList();
        return (sp.FitPoints.Count >= 2 ? sp.FitPoints : sp.ControlPoints).Select(v => (v.X, v.Y)).ToList();
    }

    /// <summary>Linien eines Schraffurmusters, auf die Ringe zugeschnitten (gerade/ungerade: Inseln bleiben frei).
    /// Je Musterlinie: Winkel (Bogenmass), Basispunkt, Versatz zur nächsten Linie, Strichlängen (> 0 Strich, &lt; 0 Lücke, 0 Punkt; leer = durchgezogen).
    /// null, wenn mehr als <paramref name="max"/> Strecken entstünden.</summary>
    public static List<(double X0, double Y0, double X1, double Y1)>? SchraffurLinien(
        IReadOnlyList<List<(double X, double Y)>> ringe,
        IReadOnlyList<(double Winkel, double Bx, double By, double Ox, double Oy, IReadOnlyList<double> Striche)> muster, int max)
    {
        var aus = new List<(double, double, double, double)>();
        var schnitte = new List<double>();
        foreach (var m in muster)
        {
            double dx = Math.Cos(m.Winkel), dy = Math.Sin(m.Winkel), nx = -dy, ny = dx;
            double d = m.Ox * nx + m.Oy * ny;            // Abstand benachbarter Linien
            if (Math.Abs(d) < 1e-9) continue;
            double schub = m.Ox * dx + m.Oy * dy;        // Versatz des Strichmusters von Linie zu Linie
            double basisN = m.Bx * nx + m.By * ny, basisU = m.Bx * dx + m.By * dy;
            double min = double.MaxValue, maxN = double.MinValue;
            foreach (var r in ringe) foreach (var p in r) { double v = p.X * nx + p.Y * ny; min = Math.Min(min, v); maxN = Math.Max(maxN, v); }
            double k0 = (min - basisN) / d, k1 = (maxN - basisN) / d;
            if (k0 > k1) (k0, k1) = (k1, k0);
            long von = (long)Math.Ceiling(k0), bis = (long)Math.Floor(k1);
            if (bis - von > max) return null;
            double periode = m.Striche.Sum(Math.Abs);
            for (long k = von; k <= bis; k++)
            {
                double hoehe = basisN + k * d;
                schnitte.Clear();
                foreach (var r in ringe)
                    for (int i = 0; i < r.Count; i++)
                    {
                        var p = r[i]; var q = r[(i + 1) % r.Count];
                        double vp = p.X * nx + p.Y * ny - hoehe, vq = q.X * nx + q.Y * ny - hoehe;
                        if ((vp < 0) == (vq < 0)) continue;   // halboffen: jede Ecke zählt genau einmal
                        double f = vp / (vp - vq);
                        schnitte.Add((p.X + (q.X - p.X) * f) * dx + (p.Y + (q.Y - p.Y) * f) * dy);
                    }
                schnitte.Sort();
                for (int i = 0; i + 1 < schnitte.Count; i += 2)
                {
                    double u0 = schnitte[i], u1 = schnitte[i + 1];
                    if (u1 - u0 < 1e-9) continue;
                    if (m.Striche.Count == 0 || periode < 1e-9)
                    {
                        aus.Add((u0 * dx + hoehe * nx, u0 * dy + hoehe * ny, u1 * dx + hoehe * nx, u1 * dy + hoehe * ny));
                    }
                    else
                    {
                        double start = basisU + k * schub;
                        double u = start + Math.Floor((u0 - start) / periode) * periode;
                        if ((u1 - u) / periode * m.Striche.Count > max) return null;
                        while (u < u1)
                            foreach (var st in m.Striche)
                            {
                                double a = Math.Max(u, u0), b = Math.Min(u + Math.Abs(st), u1);
                                if (st >= 0 && b >= a && u + Math.Abs(st) > u0 && u < u1)
                                {
                                    if (st == 0) b = a + periode * 0.01;   // Punkt als sehr kurzer Strich
                                    aus.Add((a * dx + hoehe * nx, a * dy + hoehe * ny, b * dx + hoehe * nx, b * dy + hoehe * ny));
                                }
                                u += Math.Abs(st);
                            }
                    }
                    if (aus.Count > max) return null;
                }
            }
        }
        return aus;
    }
}
