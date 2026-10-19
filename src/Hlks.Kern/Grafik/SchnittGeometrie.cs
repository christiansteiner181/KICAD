// Schnitt und Ansicht (Modul 1a, C1) – kleine Geometriehilfen in der Ansichtsebene (u, v): Fläche, konvexe Hülle, Rechteck,
// Kreis, Begrenzung, Beschneiden räumlicher Polygone auf die Sichtschicht, Verketten von Kantenstücken zu Linienzügen.

using Clipper2Lib;

namespace Hlks.Kern;

internal static class SchnittGeometrie
{
    /// <summary>Kreise, Zylinder und Kugeln als regelmässiges 48-Eck (Annahme, technisch: Sehnenfehler 0.2 % des Radius,
    /// bei Ø 1000 mm 1 mm im Modell = 0.02 mm auf dem Papier bei 1:50).</summary>
    public const int Teilung = 48;

    /// <summary>Vorzeichenbehaftete Fläche (Gegenuhrzeigersinn positiv).</summary>
    public static double Flaeche(IReadOnlyList<P2> r)
    {
        double s = 0;
        for (int i = 0; i < r.Count; i++)
        {
            var a = r[i]; var b = r[(i + 1) % r.Count];
            s += a.U * b.V - b.U * a.V;
        }
        return s / 2;
    }

    /// <summary>Konvexe Hülle (Andrew) im Gegenuhrzeigersinn ohne kollineare Punkte; Rückgabe: Indizes in <paramref name="p"/>.</summary>
    public static List<int> Huelle(IReadOnlyList<P2> p)
    {
        var idx = Enumerable.Range(0, p.Count).OrderBy(i => p[i].U).ThenBy(i => p[i].V).ToList();
        if (idx.Count < 3) return idx;
        var h = new List<int>(2 * idx.Count);
        bool Links(int o, int a, int b)
        {
            double au = p[a].U - p[o].U, av = p[a].V - p[o].V, bu = p[b].U - p[o].U, bv = p[b].V - p[o].V;
            double k = au * bv - av * bu;
            return k > 1e-10 * Math.Sqrt((au * au + av * av) * (bu * bu + bv * bv)) && k > 1e-12;
        }
        foreach (var i in idx)
        {
            while (h.Count >= 2 && !Links(h[^2], h[^1], i)) h.RemoveAt(h.Count - 1);
            h.Add(i);
        }
        int unten = h.Count + 1;
        for (int k = idx.Count - 2; k >= 0; k--)
        {
            var i = idx[k];
            while (h.Count >= unten && !Links(h[^2], h[^1], i)) h.RemoveAt(h.Count - 1);
            h.Add(i);
        }
        h.RemoveAt(h.Count - 1);
        return h;
    }

    /// <summary>Achsparalleles Rechteck im Gegenuhrzeigersinn.</summary>
    public static List<P2> Rechteck(double u0, double u1, double v0, double v1)
    {
        if (u1 < u0) (u0, u1) = (u1, u0);
        if (v1 < v0) (v0, v1) = (v1, v0);
        return [new(u0, v0), new(u1, v0), new(u1, v1), new(u0, v1)];
    }

    /// <summary>Kreis als regelmässiges Vieleck im Gegenuhrzeigersinn (Ecken auf dem Kreis).</summary>
    public static List<P2> Kreis(P2 m, double r)
    {
        var aus = new List<P2>(Teilung);
        for (int k = 0; k < Teilung; k++)
        {
            double a = 2 * Math.PI * k / Teilung;
            aus.Add(new(m.U + r * Math.Cos(a), m.V + r * Math.Sin(a)));
        }
        return aus;
    }

    public static PathD Pfad(IEnumerable<P2> pts) => new(pts.Select(p => new PointD(p.U, p.V)));

    /// <summary>[minu, minv, maxu, maxv] der Punkte.</summary>
    public static double[] Box(IEnumerable<P2> pts)
    {
        double u0 = double.MaxValue, v0 = double.MaxValue, u1 = double.MinValue, v1 = double.MinValue;
        foreach (var q in pts) { u0 = Math.Min(u0, q.U); v0 = Math.Min(v0, q.V); u1 = Math.Max(u1, q.U); v1 = Math.Max(v1, q.V); }
        return [u0, v0, u1, v1];
    }

    /// <summary>Überlappen sich zwei Begrenzungen (mit Zugabe <paramref name="rand"/>)?</summary>
    public static bool Ueberlappt(double[] a, double[] b, double rand = 0) =>
        a[0] <= b[2] + rand && b[0] <= a[2] + rand && a[1] <= b[3] + rand && b[1] <= a[3] + rand;

    /// <summary>Räumliches Polygon auf die Sichtschicht 0 ≤ w ≤ <paramref name="max"/> beschneiden.</summary>
    public static List<P3> InSchicht(IReadOnlyList<P3> poly, double max)
    {
        var a = Halb(poly, 0, true);
        return double.IsPositiveInfinity(max) ? a : Halb(a, max, false);
    }

    static List<P3> Halb(IReadOnlyList<P3> poly, double grenze, bool darueber)
    {
        var aus = new List<P3>(poly.Count + 2);
        for (int k = 0; k < poly.Count; k++)
        {
            var p = poly[k]; var q = poly[(k + 1) % poly.Count];
            double sp = darueber ? p.W - grenze : grenze - p.W, sq = darueber ? q.W - grenze : grenze - q.W;
            if (sp >= 0) aus.Add(p);
            if ((sp >= 0) != (sq >= 0)) aus.Add(p + (q - p) * (sp / (sp - sq)));
        }
        return aus;
    }

    /// <summary>Kantenstücke ohne doppelte zu Linienzügen verketten. Gerade Stücke (ebene Körperkanten, Mantellinien) bleiben
    /// einzeln, damit sie als Fangkanten taugen; gekrümmte (Kreise, Ellipsen) werden an gemeinsamen Endpunkten verbunden.</summary>
    public static List<List<P2>> Verketten(IEnumerable<(P2 A, P2 B, bool Gerade)> stuecke)
    {
        var aus = new List<List<P2>>();
        var gesehen = new HashSet<(long, long, long, long)>();
        var krumm = new List<(P2 A, P2 B)>();
        foreach (var (a, b, gerade) in stuecke)
        {
            var ka = Schluessel(a); var kb = Schluessel(b);
            if (ka == kb) continue;
            var k = ka.CompareTo(kb) < 0 ? (ka.Item1, ka.Item2, kb.Item1, kb.Item2) : (kb.Item1, kb.Item2, ka.Item1, ka.Item2);
            if (!gesehen.Add(k)) continue;
            if (gerade) aus.Add([a, b]);
            else krumm.Add((a, b));
        }
        var anPunkt = new Dictionary<(long, long), List<int>>();
        for (int i = 0; i < krumm.Count; i++)
            foreach (var p in new[] { krumm[i].A, krumm[i].B })
            {
                var k = Schluessel(p);
                if (!anPunkt.TryGetValue(k, out var l)) anPunkt[k] = l = [];
                l.Add(i);
            }
        var benutzt = new bool[krumm.Count];
        bool Weiter(P2 p, out P2 andere)
        {
            andere = p;
            foreach (var j in anPunkt[Schluessel(p)])
            {
                if (benutzt[j]) continue;
                benutzt[j] = true;
                andere = Schluessel(krumm[j].A) == Schluessel(p) ? krumm[j].B : krumm[j].A;
                return true;
            }
            return false;
        }
        for (int i = 0; i < krumm.Count; i++)
        {
            if (benutzt[i]) continue;
            benutzt[i] = true;
            var kette = new List<P2> { krumm[i].A, krumm[i].B };
            while (Weiter(kette[^1], out var n)) kette.Add(n);
            var vorne = new List<P2>();
            while (Weiter(vorne.Count > 0 ? vorne[^1] : kette[0], out var n)) vorne.Add(n);
            if (vorne.Count > 0) { vorne.Reverse(); kette.InsertRange(0, vorne); }
            aus.Add(kette);
        }
        return aus;
    }

    /// <summary>Punkt auf 0.001 mm gerundet als Schlüssel.</summary>
    static (long, long) Schluessel(P2 p) => ((long)Math.Round(p.U * 1000), (long)Math.Round(p.V * 1000));
}
