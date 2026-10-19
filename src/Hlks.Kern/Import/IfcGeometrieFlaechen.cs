// Flächenhilfen der IFC-Geometrie (Modul 1b, B1): ebene Vielecke mit Löchern in Dreiecke zerlegen (Ohrenschneiden,
// Löcher über Brücken nach D. Eberly «Triangulation by Ear Clipping»), Grundfläche eines Netzes (Vereinigung der
// projizierten Dreiecke mit Clipper2), kleinstes umschliessendes Rechteck (Längsrichtung von Wänden und Trägern ohne
// Achse) und der waagrechte Schnitt eines Netzes (Hintergrund im Grundriss).

using Clipper2Lib;

namespace Hlks.Kern;

public static class Dreieckszerlegung
{
    /// <summary>Zerlegt ein ebenes Vieleck (Aussenring, Löcher; 2D) in Dreiecke. Rückgabe: Indizes in die Punktfolge
    /// «Aussenring, dann Löcher der Reihe nach», je drei ein Dreieck im Gegenuhrzeigersinn.</summary>
    public static List<int> Zerlegen(IReadOnlyList<double[]> aussen, IReadOnlyList<List<double[]>>? loecher)
    {
        var pts = new List<double[]>(aussen);
        var aus = new List<int>();
        if (aussen.Count < 3) return aus;
        var ring = Enumerable.Range(0, aussen.Count).ToList();
        if (Inhalt(pts, ring) < 0) ring.Reverse();
        if (loecher is { Count: > 0 })
        {
            var lr = new List<List<int>>();
            foreach (var l in loecher)
            {
                int b = pts.Count;
                pts.AddRange(l);
                if (l.Count < 3) continue;
                var r = Enumerable.Range(b, l.Count).ToList();
                if (Inhalt(pts, r) > 0) r.Reverse();
                lr.Add(r);
            }
            foreach (var r in lr.OrderByDescending(r => r.Max(i => pts[i][0])))
                Bruecke(pts, ring, r);
        }
        if (ring.Count == 3) { if (Kreuz(pts[ring[0]], pts[ring[1]], pts[ring[2]]) > 0) aus.AddRange(ring); return aus; }
        if (loecher is not { Count: > 0 } && Konvex(pts, ring))
        {
            for (int i = 1; i + 1 < ring.Count; i++) { aus.Add(ring[0]); aus.Add(ring[i]); aus.Add(ring[i + 1]); }
            return aus;
        }
        Ohren(pts, ring, aus);
        return aus;
    }

    static double Inhalt(List<double[]> p, List<int> r)
    {
        double s = 0;
        for (int i = 0; i < r.Count; i++) { var a = p[r[i]]; var b = p[r[(i + 1) % r.Count]]; s += a[0] * b[1] - b[0] * a[1]; }
        return s / 2;
    }

    static double Kreuz(double[] a, double[] b, double[] c) => (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0]);

    static bool Konvex(List<double[]> p, List<int> r)
    {
        int n = r.Count;
        for (int i = 0; i < n; i++)
            if (Kreuz(p[r[i]], p[r[(i + 1) % n]], p[r[(i + 2) % n]]) < 0) return false;
        return true;
    }

    static bool Gleich(double[] a, double[] b) => a[0] == b[0] && a[1] == b[1];

    /// <summary>Verbindet ein Loch (im Uhrzeigersinn) mit dem Ring über eine sichtbare Brücke vom rechtesten Lochpunkt aus.</summary>
    static void Bruecke(List<double[]> pts, List<int> ring, List<int> loch)
    {
        int mi = 0;
        for (int i = 1; i < loch.Count; i++) if (pts[loch[i]][0] > pts[loch[mi]][0]) mi = i;
        var m = pts[loch[mi]];
        int n = ring.Count, kante = -1;
        double bestX = double.MaxValue;
        for (int k = 0; k < n; k++)
        {
            var a = pts[ring[k]]; var b = pts[ring[(k + 1) % n]];
            if ((a[1] > m[1]) == (b[1] > m[1])) continue;
            double x = a[0] + (m[1] - a[1]) * (b[0] - a[0]) / (b[1] - a[1]);
            if (x >= m[0] - 1e-9 && x < bestX) { bestX = x; kante = k; }
        }
        if (kante < 0) return;   // Loch nicht verbindbar (liegt ausserhalb): weglassen
        int ka = kante, kb = (kante + 1) % n;
        int p = pts[ring[ka]][0] > pts[ring[kb]][0] ? ka : kb;
        double[] ip = [bestX, m[1]];
        var pp = pts[ring[p]];
        // Liegen Punkte des Rings im Dreieck (m, ip, pp), den mit dem kleinsten Winkel zum Strahl nehmen
        double bestW = double.MaxValue, bestD = double.MaxValue;
        if (!Gleich(ip, pp))
            for (int k = 0; k < n; k++)
            {
                if (k == p) continue;
                var v = pts[ring[k]];
                if (Gleich(v, pp) || !ImDreieck(m, ip, pp, v)) continue;
                double w = Math.Atan2(Math.Abs(v[1] - m[1]), v[0] - m[0]), d = (v[0] - m[0]) * (v[0] - m[0]) + (v[1] - m[1]) * (v[1] - m[1]);
                if (w < bestW - 1e-12 || (Math.Abs(w - bestW) <= 1e-12 && d < bestD)) { bestW = w; bestD = d; p = k; }
            }
        var neu = new List<int>(ring.Count + loch.Count + 2);
        for (int k = 0; k <= p; k++) neu.Add(ring[k]);
        for (int i = 0; i <= loch.Count; i++) neu.Add(loch[(mi + i) % loch.Count]);
        neu.Add(ring[p]);
        for (int k = p + 1; k < n; k++) neu.Add(ring[k]);
        ring.Clear();
        ring.AddRange(neu);
    }

    static bool ImDreieck(double[] a, double[] b, double[] c, double[] p)
    {
        double d1 = Kreuz(a, b, p), d2 = Kreuz(b, c, p), d3 = Kreuz(c, a, p);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(neg && pos);
    }

    /// <summary>Ohrenschneiden; findet sich kein Ohr mehr (entartete Eingabe), wird der Rest als Fächer zerlegt.</summary>
    static void Ohren(List<double[]> pts, List<int> ring, List<int> aus)
    {
        var poly = new List<int>(ring.Count);
        foreach (var i in ring) if (poly.Count == 0 || !Gleich(pts[poly[^1]], pts[i])) poly.Add(i);
        while (poly.Count > 1 && Gleich(pts[poly[0]], pts[poly[^1]])) poly.RemoveAt(poly.Count - 1);
        double x0 = double.MaxValue, x1 = double.MinValue, y0 = double.MaxValue, y1 = double.MinValue;
        foreach (var i in poly) { x0 = Math.Min(x0, pts[i][0]); x1 = Math.Max(x1, pts[i][0]); y0 = Math.Min(y0, pts[i][1]); y1 = Math.Max(y1, pts[i][1]); }
        double eps = 1e-12 * ((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
        int start = 0;
        while (poly.Count > 3)
        {
            int n = poly.Count;
            bool gefunden = false;
            for (int s = 0; s < n; s++)
            {
                int ic = (start + s) % n, ip = (ic + n - 1) % n, inx = (ic + 1) % n;
                var a = pts[poly[ip]]; var b = pts[poly[ic]]; var c = pts[poly[inx]];
                double kr = Kreuz(a, b, c);
                if (Math.Abs(kr) <= eps)
                {
                    // gerade oder Spitze: Punkt ohne Dreieck entfernen
                    poly.RemoveAt(ic);
                    start = Math.Max(0, ic - 1);
                    gefunden = true;
                    break;
                }
                if (kr < 0) continue;
                bool frei = true;
                for (int j = 0; j < n && frei; j++)
                {
                    if (j == ip || j == ic || j == inx) continue;
                    var v = pts[poly[j]];
                    if (Gleich(v, a) || Gleich(v, b) || Gleich(v, c)) continue;
                    if (ImDreieck(a, b, c, v)) frei = false;
                }
                if (!frei) continue;
                aus.Add(poly[ip]); aus.Add(poly[ic]); aus.Add(poly[inx]);
                poly.RemoveAt(ic);
                start = Math.Max(0, ic - 1);
                gefunden = true;
                break;
            }
            if (!gefunden)
            {
                for (int i = 1; i + 1 < poly.Count; i++) { aus.Add(poly[0]); aus.Add(poly[i]); aus.Add(poly[i + 1]); }
                return;
            }
        }
        if (poly.Count == 3 && Kreuz(pts[poly[0]], pts[poly[1]], pts[poly[2]]) > eps) aus.AddRange(poly);
    }
}

/// <summary>Kleinstes umschliessendes Rechteck: Mitte, Längsrichtung (Einheitsvektor), Länge (lange Seite), Breite.</summary>
public sealed record Huellrechteck(double[] Mitte, double[] Richtung, double Laenge, double Breite, double Flaeche);

public static class Netzflaechen
{
    /// <summary>Grundfläche eines Netzes: Vereinigung aller nicht senkrechten Dreiecke, auf xy projiziert (Clipper2).</summary>
    public static List<Flaeche> Grundflaeche(Dreiecksnetz n)
    {
        var pfade = new PathsD();
        for (int i = 0; i < n.D.Count; i += 3)
        {
            var (ax, ay, az) = n.Lage(n.D[i]); var (bx, by, bz) = n.Lage(n.D[i + 1]); var (cx, cy, cz) = n.Lage(n.D[i + 2]);
            double kr = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
            if (Math.Abs(kr) < 1e-3) continue;   // senkrecht oder winzig
            var pfad = kr > 0 ? new PathD { new(ax, ay), new(bx, by), new(cx, cy) } : new PathD { new(ax, ay), new(cx, cy), new(bx, by) };
            pfade.Add(pfad);
        }
        return pfade.Count == 0 ? [] : Polygone.Flaechen(pfade);
    }

    /// <summary>Konvexe Hülle (monotone Kette) der Punkte in xy, Gegenuhrzeigersinn.</summary>
    public static List<double[]> Huelle(IEnumerable<double[]> punkte)
    {
        var p = punkte.Select(q => new[] { q[0], q[1] }).OrderBy(q => q[0]).ThenBy(q => q[1]).ToList();
        if (p.Count < 3) return p;
        static double K(double[] o, double[] a, double[] b) => (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0]);
        var h = new List<double[]>();
        foreach (var q in p) { while (h.Count >= 2 && K(h[^2], h[^1], q) <= 0) h.RemoveAt(h.Count - 1); h.Add(q); }
        int unten = h.Count + 1;
        for (int i = p.Count - 2; i >= 0; i--) { var q = p[i]; while (h.Count >= unten && K(h[^2], h[^1], q) <= 0) h.RemoveAt(h.Count - 1); h.Add(q); }
        h.RemoveAt(h.Count - 1);
        return h;
    }

    /// <summary>Kleinstes Rechteck um die Punkte (xy), über die Kanten der konvexen Hülle gesucht.</summary>
    public static Huellrechteck? Rechteck(IEnumerable<double[]> punkte)
    {
        var h = Huelle(punkte);
        if (h.Count < 2) return null;
        Huellrechteck? best = null;
        for (int i = 0; i < h.Count; i++)
        {
            var a = h[i]; var b = h[(i + 1) % h.Count];
            double dx = b[0] - a[0], dy = b[1] - a[1], l = Math.Sqrt(dx * dx + dy * dy);
            if (l < 1e-9) continue;
            double ux = dx / l, uy = dy / l;
            double s0 = double.MaxValue, s1 = double.MinValue, q0 = double.MaxValue, q1 = double.MinValue;
            foreach (var p in h)
            {
                double s = p[0] * ux + p[1] * uy, q = -p[0] * uy + p[1] * ux;
                s0 = Math.Min(s0, s); s1 = Math.Max(s1, s); q0 = Math.Min(q0, q); q1 = Math.Max(q1, q);
            }
            double f = (s1 - s0) * (q1 - q0);
            if (best is null || f < best.Flaeche - 1e-6)
            {
                double sm = (s0 + s1) / 2, qm = (q0 + q1) / 2;
                double[] mitte = [sm * ux - qm * uy, sm * uy + qm * ux];
                best = s1 - s0 >= q1 - q0
                    ? new Huellrechteck(mitte, [ux, uy], s1 - s0, q1 - q0, f)
                    : new Huellrechteck(mitte, [-uy, ux], q1 - q0, s1 - s0, f);
            }
        }
        return best;
    }

    /// <summary>Waagrechter Schnitt der Dreiecke (Punkte float x, y, z; Dreiecke int) mit der Ebene z = h: Strecken
    /// [x1, y1, x2, y2]. Punkte genau auf der Ebene zählen als darüber (keine doppelten Strecken an Kanten).</summary>
    public static void Schneiden(float[] punkte, int[] dreiecke, double h, List<double[]> aus)
    {
        for (int i = 0; i + 2 < dreiecke.Length; i += 3)
        {
            int a = dreiecke[i] * 3, b = dreiecke[i + 1] * 3, c = dreiecke[i + 2] * 3;
            double za = punkte[a + 2] - h, zb = punkte[b + 2] - h, zc = punkte[c + 2] - h;
            bool oa = za >= 0, ob = zb >= 0, oc = zc >= 0;
            if (oa == ob && ob == oc) continue;
            var pkt = new List<double>(4);
            void Kante(int p, double zp, int q, double zq)
            {
                if ((zp >= 0) == (zq >= 0)) return;
                double t = zp / (zp - zq);
                pkt.Add(punkte[p] + t * (punkte[q] - punkte[p]));
                pkt.Add(punkte[p + 1] + t * (punkte[q + 1] - punkte[p + 1]));
            }
            Kante(a, za, b, zb); Kante(b, zb, c, zc); Kante(c, zc, a, za);
            if (pkt.Count == 4 && (Math.Abs(pkt[0] - pkt[2]) > 1e-3 || Math.Abs(pkt[1] - pkt[3]) > 1e-3))
                aus.Add([pkt[0], pkt[1], pkt[2], pkt[3]]);
        }
    }

    /// <summary>Strecken zu Linienzügen verbinden (Endpunkte auf 0.1 mm gleich); Rückgabe Punktfolgen [x, y].</summary>
    public static List<List<double[]>> Verbinden(List<double[]> strecken)
    {
        static (long, long) S(double x, double y) => ((long)Math.Round(x * 10), (long)Math.Round(y * 10));
        var anEnde = new Dictionary<(long, long), List<int>>();
        for (int i = 0; i < strecken.Count; i++)
        {
            foreach (var k in new[] { S(strecken[i][0], strecken[i][1]), S(strecken[i][2], strecken[i][3]) })
            {
                if (!anEnde.TryGetValue(k, out var l)) anEnde[k] = l = [];
                l.Add(i);
            }
        }
        var benutzt = new bool[strecken.Count];
        var aus = new List<List<double[]>>();
        int? Naechste((long, long) k)
        {
            if (!anEnde.TryGetValue(k, out var l)) return null;
            foreach (var j in l) if (!benutzt[j]) return j;
            return null;
        }
        for (int i = 0; i < strecken.Count; i++)
        {
            if (benutzt[i]) continue;
            benutzt[i] = true;
            var zug = new LinkedList<double[]>();
            zug.AddLast([strecken[i][0], strecken[i][1]]);
            zug.AddLast([strecken[i][2], strecken[i][3]]);
            // nach vorne und nach hinten verlängern
            for (int richtung = 0; richtung < 2; richtung++)
            {
                while (true)
                {
                    var ende = richtung == 0 ? zug.Last!.Value : zug.First!.Value;
                    var k = S(ende[0], ende[1]);
                    if (Naechste(k) is not int j) break;
                    benutzt[j] = true;
                    var s = strecken[j];
                    double[] weiter = S(s[0], s[1]) == k ? [s[2], s[3]] : [s[0], s[1]];
                    if (richtung == 0) zug.AddLast(weiter); else zug.AddFirst(weiter);
                }
            }
            var punkte = zug.Select(p => new[] { Math.Round(p[0], 1), Math.Round(p[1], 1) }).ToList();
            if (punkte.Count > 2 && S(zug.First!.Value[0], zug.First.Value[1]) == S(zug.Last!.Value[0], zug.Last.Value[1])) punkte[^1] = (double[])punkte[0].Clone();   // geschlossen
            // Zwischenpunkte auf einer Geraden weglassen (die Dreiecke einer ebenen Fläche liefern je eine Strecke)
            var gerade = new List<double[]> { punkte[0] };
            for (int k = 1; k + 1 < punkte.Count; k++)
            {
                var a = gerade[^1]; var b = punkte[k]; var c = punkte[k + 1];
                double dx = c[0] - a[0], dy = c[1] - a[1], l = Math.Sqrt(dx * dx + dy * dy);
                double abw = l < 1e-9 ? double.MaxValue : Math.Abs((b[0] - a[0]) * dy - (b[1] - a[1]) * dx) / l;
                bool zwischen = (b[0] - a[0]) * dx + (b[1] - a[1]) * dy > 0 && (c[0] - b[0]) * dx + (c[1] - b[1]) * dy > 0;
                if (abw > 0.05 || !zwischen) gerade.Add(b);
            }
            gerade.Add(punkte[^1]);
            aus.Add(gerade);
        }
        return aus;
    }
}
