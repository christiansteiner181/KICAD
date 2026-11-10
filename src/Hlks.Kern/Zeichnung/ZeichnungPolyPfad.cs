// 2D-CAD «Zeichnung», Stufe 2 Teil 5: Polylinien mit Bögen als Pfad – stutzen, brechen und versetzen.
// Ein Pfad besteht aus Stücken in Laufrichtung: Strecke A→B oder Kreisbogen um C ab dem Winkel Von mit vorzeichenbehaftetem Öffnungswinkel
// (Sweep > 0 Gegenuhrzeigersinn, wie eine positive Ausbuchtung). Die Lage auf dem Pfad ist «Stück + Anteil» (z. B. 2.25 = ein Viertel des dritten Stücks).

using ACadSharp.Entities;
using CSMath;

namespace Hlks.Kern;

public static class ZeichnungPolyPfad
{
    readonly record struct Stueck(bool Bogen, double Ax, double Ay, double Bx, double By, double Cx, double Cy, double R, double Von, double Sweep)
    {
        public (double X, double Y) Bei(double u) => Bogen
            ? (Cx + R * Math.Cos(Von + Sweep * u), Cy + R * Math.Sin(Von + Sweep * u))
            : (Ax + (Bx - Ax) * u, Ay + (By - Ay) * u);
        /// <summary>Dasselbe Stück als Schnittgeometrie (Bogen immer im Gegenuhrzeigersinn).</summary>
        public GeoStueck Geo => !Bogen ? GeoStueck.Strecke(Ax, Ay, Bx, By)
            : Sweep >= 0 ? GeoStueck.Kreisbogen(Cx, Cy, R, Von, Sweep) : GeoStueck.Kreisbogen(Cx, Cy, R, Von + Sweep, -Sweep);
        /// <summary>Anteil u eines Punkts auf dem Kreis des Bogens (Winkel w), ohne Begrenzung auf 0…1.</summary>
        public double AnteilBeiWinkel(double w) => Sweep >= 0 ? Norm(w - Von) / Sweep : Norm(Von - w) / -Sweep;
    }

    static double Norm(double w) => ((w % (2 * Math.PI)) + 2 * Math.PI) % (2 * Math.PI);

    static List<Stueck> Stuecke(LwPolyline p)
    {
        var aus = new List<Stueck>();
        var v = p.Vertices;
        int n = p.IsClosed ? v.Count : v.Count - 1;
        for (int i = 0; i < n; i++)
        {
            var a = v[i]; var b = v[(i + 1) % v.Count];
            double ax = a.Location.X, ay = a.Location.Y, bx = b.Location.X, by = b.Location.Y;
            if (Math.Abs(ax - bx) + Math.Abs(ay - by) < 1e-12) continue;
            if (a.Bulge == 0) { aus.Add(new Stueck(false, ax, ay, bx, by, 0, 0, 0, 0, 0)); continue; }
            var (cx, cy, r, w1, _) = ZeichnungGeometrie.BogenAusBulge(ax, ay, bx, by, a.Bulge);
            aus.Add(new Stueck(true, ax, ay, bx, by, cx, cy, r, w1, 4 * Math.Atan(a.Bulge)));
        }
        return aus;
    }

    /// <summary>Nächste Lage auf dem Pfad zum Punkt (px, py).</summary>
    static double Lage(List<Stueck> s, double px, double py)
    {
        double best = double.MaxValue, lage = 0;
        for (int i = 0; i < s.Count; i++)
        {
            double u;
            if (s[i].Bogen) u = Math.Clamp(s[i].AnteilBeiWinkel(Math.Atan2(py - s[i].Cy, px - s[i].Cx)) is var a && a <= 1 ? a : (Norm(Math.Atan2(py - s[i].Cy, px - s[i].Cx) - s[i].Von - s[i].Sweep / 2 + Math.PI) > Math.PI ? 1 : 0), 0, 1);
            else
            {
                double dx = s[i].Bx - s[i].Ax, dy = s[i].By - s[i].Ay;
                u = Math.Clamp(((px - s[i].Ax) * dx + (py - s[i].Ay) * dy) / (dx * dx + dy * dy), 0, 1);
            }
            // beim Bogen beide Enden mitprüfen (die Wahl des Endes oben ist nur eine Näherung)
            foreach (var kandidat in s[i].Bogen ? new[] { u, 0, 1 } : [u])
            {
                var (x, y) = s[i].Bei(kandidat);
                double ab = Math.Pow(x - px, 2) + Math.Pow(y - py, 2);
                if (ab < best) { best = ab; lage = i + kandidat; }
            }
        }
        return lage;
    }

    /// <summary>Lagen aller Schnitte des Pfads mit den Grenzen.</summary>
    static List<double> Schnitte(List<Stueck> s, IEnumerable<GeoStueck> grenzen)
    {
        var aus = new List<double>();
        var liste = grenzen as IList<GeoStueck> ?? grenzen.ToList();
        for (int i = 0; i < s.Count; i++)
            foreach (var g in liste)
            {
                if (!s[i].Bogen)
                {
                    foreach (var q in ZeichnungSchnitt.GeradeMit(s[i].Ax, s[i].Ay, s[i].Bx, s[i].By, g))
                        if (q.T >= -1e-9 && q.T <= 1 + 1e-9) aus.Add(i + Math.Clamp(q.T, 0, 1));
                }
                else
                {
                    foreach (var q in ZeichnungSchnitt.KreisMit(s[i].Cx, s[i].Cy, s[i].R, g))
                    {
                        double u = s[i].AnteilBeiWinkel(q.W);
                        if (u <= 1 + 1e-9) aus.Add(i + Math.Clamp(u, 0, 1));
                    }
                }
            }
        return aus;
    }

    /// <summary>Offene Polylinie von Lage a bis Lage b (b > a; bei geschlossenem Pfad darf b über das Ende hinausgehen).</summary>
    static LwPolyline Teil(List<Stueck> s, double a, double b)
    {
        int n = s.Count;
        var p = new LwPolyline();
        for (int i = (int)Math.Floor(a + 1e-12); i < b - 1e-12; i++)
        {
            var st = s[((i % n) + n) % n];
            double u0 = Math.Max(a - i, 0), u1 = Math.Min(b - i, 1);
            if (u1 - u0 < 1e-12) continue;
            var (x, y) = st.Bei(u0);
            p.Vertices.Add(new LwPolyline.Vertex(new XY(x, y)) { Bulge = st.Bogen ? Math.Tan(st.Sweep * (u1 - u0) / 4) : 0 });
            if (i + 1 >= b - 1e-12) { var (ex, ey) = st.Bei(u1); p.Vertices.Add(new LwPolyline.Vertex(new XY(ex, ey))); }
        }
        return p;
    }

    /// <summary>Stutzen: der Teil zwischen den Schnitten links und rechts der Klickstelle fällt weg; Rückgabe die Reststücke.</summary>
    public static List<LwPolyline> Stutzen(LwPolyline p, IEnumerable<GeoStueck> grenzen, double px, double py)
    {
        var s = Stuecke(p);
        int n = s.Count;
        if (n == 0) throw new HlksFehler("Die Polylinie hat keine Länge.");
        double tc = Lage(s, px, py);
        const double Rand = 1e-7;
        var t = Schnitte(s, grenzen).Where(q => p.IsClosed || (q > Rand && q < n - Rand)).Select(q => Math.Round(q, 9)).Distinct().Order().ToList();
        var neu = new List<LwPolyline>();
        if (p.IsClosed)
        {
            if (t.Count < 2) throw new HlksFehler("Eine geschlossene Polylinie braucht zwei Schnitte mit Grenzen.", "Zwei Schnittkanten wählen oder die Polylinie löschen.");
            double lo = t.Where(q => q <= tc).DefaultIfEmpty(t[^1] - n).Max(), hi = t.Where(q => q > tc).DefaultIfEmpty(t[0] + n).Min();
            neu.Add(Teil(s, hi, lo + n));
        }
        else
        {
            double lo = t.Where(q => q <= tc).DefaultIfEmpty(0).Max(), hi = t.Where(q => q > tc).DefaultIfEmpty(n).Min();
            if (lo == 0 && hi == n) throw new HlksFehler("An dieser Stelle gibt es keinen Schnitt mit einer Grenze: nichts zu stutzen.", "Ein Objekt wählen, das von einer Schnittkante gekreuzt wird, oder es löschen.");
            if (lo > 0) neu.Add(Teil(s, 0, lo));
            if (hi < n) neu.Add(Teil(s, hi, n));
        }
        neu.RemoveAll(q => q.Vertices.Count < 2);
        return neu;
    }

    /// <summary>Bruch an einer Stelle (zwei Teile) bzw. zwischen zwei Stellen (das Stück dazwischen fällt weg).</summary>
    public static List<LwPolyline> Bruch(LwPolyline p, double px, double py, (double X, double Y)? zweiter)
    {
        var s = Stuecke(p);
        int n = s.Count;
        if (n == 0) throw new HlksFehler("Die Polylinie hat keine Länge.");
        double a = Lage(s, px, py), b = zweiter is { } q ? Lage(s, q.X, q.Y) : a;
        if (a > b) (a, b) = (b, a);
        var neu = new List<LwPolyline>();
        if (p.IsClosed)
        {
            // geschlossen: an einer Stelle aufschneiden bzw. das Stück von a nach b entfernen (Rest läuft von b über das Ende nach a)
            neu.Add(Teil(s, b, a + n));
        }
        else
        {
            if (a > 1e-9) neu.Add(Teil(s, 0, a));
            if (b < n - 1e-9) neu.Add(Teil(s, b, n));
            if (zweiter is null && neu.Count < 2) throw new HlksFehler("Die Bruchstelle liegt am Ende der Polylinie.", "Einen Punkt auf der Polylinie wählen.");
        }
        neu.RemoveAll(t => t.Vertices.Count < 2);
        if (neu.Count == 0) throw new HlksFehler("Vom Objekt bliebe nichts übrig.", "Zum Entfernen «löschen» verwenden.");
        return neu;
    }

    /// <summary>Parallele Kopie im Abstand d auf der Seite des Punkts (sx, sy).</summary>
    public static LwPolyline Versetzen(LwPolyline p, double d, double sx, double sy)
    {
        var s = Stuecke(p);
        int n = s.Count;
        if (n == 0) throw new HlksFehler("Die Polylinie hat keine Länge.");
        // Seite: links (+1) oder rechts (−1) der Laufrichtung an der nächsten Stelle
        double lage = Lage(s, sx, sy);
        int k = Math.Min((int)Math.Floor(lage), n - 1);
        double u = lage - k;
        var (fx, fy) = s[k].Bei(u);
        double tx, ty;   // Laufrichtung an dieser Stelle
        if (s[k].Bogen) { double w = s[k].Von + s[k].Sweep * u, vz0 = Math.Sign(s[k].Sweep); tx = -Math.Sin(w) * vz0; ty = Math.Cos(w) * vz0; }
        else { tx = s[k].Bx - s[k].Ax; ty = s[k].By - s[k].Ay; }
        double vz = tx * (sy - fy) - ty * (sx - fx) >= 0 ? 1 : -1;

        // jedes Stück versetzen: Strecke um die linke Normale, Bogen über den Radius (links der Laufrichtung = zur Mitte bei Gegenuhrzeigersinn)
        var o = new List<Stueck>(n);
        foreach (var st in s)
        {
            if (!st.Bogen)
            {
                double dx = st.Bx - st.Ax, dy = st.By - st.Ay, len = Math.Sqrt(dx * dx + dy * dy), nx = -dy / len * d * vz, ny = dx / len * d * vz;
                o.Add(st with { Ax = st.Ax + nx, Ay = st.Ay + ny, Bx = st.Bx + nx, By = st.By + ny });
            }
            else
            {
                double r = st.R - Math.Sign(st.Sweep) * d * vz;
                if (!(r > 1e-9)) throw new HlksFehler("Der Abstand ist grösser als ein Bogenradius der Polylinie: auf dieser Seite bleibt vom Bogen nichts übrig.", "Kleineren Abstand wählen oder auf die andere Seite versetzen.");
                o.Add(st with { R = r, Ax = st.Cx + r * Math.Cos(st.Von), Ay = st.Cy + r * Math.Sin(st.Von), Bx = st.Cx + r * Math.Cos(st.Von + st.Sweep), By = st.Cy + r * Math.Sin(st.Von + st.Sweep) });
            }
        }
        // Ecken: Schnitt benachbarter versetzter Stücke (unbegrenzt gedacht), der dem versetzten Endpunkt am nächsten liegt
        (double X, double Y) Ecke(Stueck a, Stueck b)
        {
            var kandidaten = new List<(double X, double Y)>();
            var gb = b.Bogen ? GeoStueck.Kreisbogen(b.Cx, b.Cy, b.R, 0, 2 * Math.PI) : GeoStueck.Strecke(b.Ax - 1e6 * (b.Bx - b.Ax), b.Ay - 1e6 * (b.By - b.Ay), b.Bx + 1e6 * (b.Bx - b.Ax), b.By + 1e6 * (b.By - b.Ay));
            if (a.Bogen) kandidaten.AddRange(ZeichnungSchnitt.KreisMit(a.Cx, a.Cy, a.R, gb).Select(q => (q.X, q.Y)));
            else kandidaten.AddRange(ZeichnungSchnitt.GeradeMit(a.Ax, a.Ay, a.Bx, a.By, gb).Select(q => (q.X, q.Y)));
            if (kandidaten.Count == 0) return (a.Bx, a.By);   // berührend bzw. parallel: der versetzte Endpunkt
            return kandidaten.MinBy(q => Math.Pow(q.X - a.Bx, 2) + Math.Pow(q.Y - a.By, 2));
        }
        var punkte = new List<(double X, double Y)>();
        if (p.IsClosed) for (int i = 0; i < n; i++) punkte.Add(Ecke(o[(i + n - 1) % n], o[i]));
        else
        {
            punkte.Add((o[0].Ax, o[0].Ay));
            for (int i = 1; i < n; i++) punkte.Add(Ecke(o[i - 1], o[i]));
            punkte.Add((o[n - 1].Bx, o[n - 1].By));
        }
        var neu = new LwPolyline { IsClosed = p.IsClosed };
        for (int i = 0; i < punkte.Count; i++)
        {
            double bulge = 0;
            if (i < n && o[i].Bogen)
            {
                var a = punkte[i]; var b = punkte[(i + 1) % punkte.Count];
                double w1 = Math.Atan2(a.Y - o[i].Cy, a.X - o[i].Cx), w2 = Math.Atan2(b.Y - o[i].Cy, b.X - o[i].Cx);
                double sweep = o[i].Sweep >= 0 ? Norm(w2 - w1) : -Norm(w1 - w2);
                bulge = Math.Tan(sweep / 4);
            }
            neu.Vertices.Add(new LwPolyline.Vertex(new XY(punkte[i].X, punkte[i].Y)) { Bulge = bulge });
        }
        return neu;
    }
}
