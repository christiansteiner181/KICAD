// Schnitt und Ansicht (Modul 2, B1) – Körperteil «koerper» (allgemeines, geschlossenes Dreiecksnetz eines Familienkörpers).
//
// Schnitt: Die Ebene w = 0 schneidet das Netz; die Schnittstrecken der Dreiecke werden über gemeinsame Kanten zu
//   geschlossenen Linienzügen verkettet (Ecken genau auf der Ebene zählen zur Seite w ≥ 0, damit jede Kante eindeutig
//   gekreuzt wird), mit Clipper2 (gerade/ungerade) zu Flächen mit Löchern vereinigt und für die Verdeckung in Dreiecke
//   (konvexe Stücke) zerlegt.
// Ansicht: Sichtkanten sind die Umrisskanten (ein angrenzendes Dreieck zeigt zum Betrachter, das andere nicht) und die
//   Knickkanten (Winkel der Normalen ≥ Knickwinkel) zwischen zwei Dreiecken, die zum Betrachter zeigen – je auf die
//   Sichtschicht 0 ≤ w ≤ Tiefe beschnitten; dazu wie beim Zylinder der Umriss in der Ebene w = 0 (nur Ansicht) und an der
//   Sichttiefe. Ein Netz ist ein Sichtteil mit konvexem Umriss (Hülle der Projektion) und exakter Tiefe aus dem Strahl-
//   schnitt mit den Dreiecken (Raster als Suchhilfe); weil Netze nicht konvex sein müssen, verdeckt ein Netz auch seine
//   eigenen Kanten (SchnittVerdeckung). Knickkanten sind «gerade» (einzeln, fangbar), Umrisskanten glatter Flächen
//   gekrümmt (zu Linienzügen verkettet).
// ANNAHME (technisch, Prüfliste): Knickwinkel 20° – Facetten runder Körper (7.5° je Strecke) zeigen keine Kanten, ein
//   16-Eck und alles Kantigere schon (wie die Kanten im 3D, EdgesGeometry 20°).

using Clipper2Lib;

namespace Hlks.Kern;

internal sealed partial class Schnittzerlegung
{
    /// <summary>Knickwinkel in Grad, ab dem eine Kante zwischen zwei sichtbaren Dreiecken gezeichnet wird.</summary>
    public const double Knickwinkel = 20;

    void Netz(int element, Koerperteil t)
    {
        if (t.Ecken is not { Count: >= 4 } ecken || t.Dreiecke is not { Count: >= 4 } roh) return;
        var raum = Raum;
        var p = new P3[ecken.Count];
        double wmin = double.MaxValue, wmax = double.MinValue;
        for (int i = 0; i < p.Length; i++)
        {
            var e = ecken[i];
            if (e is null || e.Length < 3) return;
            p[i] = raum.Punkt(e);
            wmin = Math.Min(wmin, p[i].W); wmax = Math.Max(wmax, p[i].W);
        }
        if (wmax < 0 || wmin > raum.Tiefe) return;
        var dreiecke = roh.Where(d => d is { Length: 3 } && d.All(i => i >= 0 && i < p.Length) && d[0] != d[1] && d[1] != d[2] && d[0] != d[2]).ToList();
        if (dreiecke.Count < 4) return;

        // Normalen im Modell (das u-v-w-System ist linkshändig): Anteil zum Betrachter = n · Blickrichtung
        var normalen = new V3[dreiecke.Count];
        var nw = new double[dreiecke.Count];
        for (int i = 0; i < dreiecke.Count; i++)
        {
            var d = dreiecke[i];
            var a = V3.Aus(ecken[d[0]]); var b = V3.Aus(ecken[d[1]]); var c = V3.Aus(ecken[d[2]]);
            normalen[i] = (b - a).Kreuz(c - a).Einheit();
            nw[i] = normalen[i].X * raum.Nx + normalen[i].Y * raum.Ny;
        }

        // Lage in der Ebene w = 0
        var inEbene = wmin < 0 ? Schnittzuege(p, dreiecke, 0) : [];
        if (IstSchnitt && inEbene.Count > 0) NetzSchnitt(element, inEbene);

        // Sichtteil
        var huellpunkte = new List<P2>();
        foreach (var q in p) if (q.W >= 0 && q.W <= raum.Tiefe) huellpunkte.Add(q.Projektion);
        foreach (var z in inEbene) huellpunkte.AddRange(z.Select(q => q.Projektion));
        var amEnde = raum.Begrenzt && wmax > raum.Tiefe ? Schnittzuege(p, dreiecke, raum.Tiefe) : [];
        foreach (var z in amEnde) huellpunkte.AddRange(z.Select(q => q.Projektion));
        if (huellpunkte.Count < 3) return;
        var huelle = SchnittGeometrie.Huelle(huellpunkte);
        if (huelle.Count < 3) return;
        var umriss = huelle.Select(i => huellpunkte[i]).ToList();
        if (SchnittGeometrie.Flaeche(umriss) < 1e-6) return;
        var teil = new Netzteil(p, dreiecke, nw) { Element = element, Umriss = umriss, Farbe = t.Farbe, Max = raum.Tiefe };

        // Kanten: Umriss- und Knickkanten
        var anKante = new Dictionary<(int, int), List<int>>();
        for (int i = 0; i < dreiecke.Count; i++)
        {
            var d = dreiecke[i];
            for (int k = 0; k < 3; k++)
            {
                int x = d[k], y = d[(k + 1) % 3];
                var key = x < y ? (x, y) : (y, x);
                if (!anKante.TryGetValue(key, out var l)) anKante[key] = l = [];
                l.Add(i);
            }
        }
        double knick = Math.Cos(Knickwinkel * Math.PI / 180);
        const double Zugewandt = -1e-9;
        foreach (var ((x, y), l) in anKante)
        {
            bool zeichnen, gerade;
            if (l.Count == 2)
            {
                bool v1 = nw[l[0]] < Zugewandt, v2 = nw[l[1]] < Zugewandt;
                bool istKnick = normalen[l[0]].Mal(normalen[l[1]]) < knick;
                zeichnen = v1 != v2 || (v1 && istKnick);
                gerade = istKnick;
            }
            else { zeichnen = l.Any(i => nw[i] < Zugewandt); gerade = true; }   // offener Rand (sollte nicht vorkommen)
            if (!zeichnen) continue;
            if (Beschneiden(p[x], p[y], raum.Tiefe) is { } s) teil.Strecken.Add(new(s.A, s.B, gerade));
        }
        void Zuege(List<List<P3>> zuege)
        {
            foreach (var z in zuege)
                for (int i = 0; i < z.Count; i++) teil.Strecken.Add(new(z[i], z[(i + 1) % z.Count], false));
        }
        if (!IstSchnitt) Zuege(inEbene);
        Zuege(amEnde);
        Teile.Add(teil);
    }

    /// <summary>Strecke auf die Sichtschicht 0 ≤ w ≤ max beschnitten, sonst null.</summary>
    static (P3 A, P3 B)? Beschneiden(P3 a, P3 b, double max)
    {
        double t0 = 0, t1 = 1, dw = b.W - a.W;
        if (Math.Abs(dw) < 1e-12) { if (a.W < 0 || a.W > max) return null; }
        else
        {
            double tn = (0 - a.W) / dw, tf = double.IsPositiveInfinity(max) ? (dw > 0 ? double.PositiveInfinity : double.NegativeInfinity) : (max - a.W) / dw;
            t0 = Math.Max(0, Math.Min(tn, tf)); t1 = Math.Min(1, Math.Max(tn, tf));
            if (t1 - t0 < 1e-12) return null;
        }
        return (a + (b - a) * t0, a + (b - a) * t1);
    }

    /// <summary>Geschlossene Schnittzüge des Netzes mit der Ebene w = c.</summary>
    static List<List<P3>> Schnittzuege(P3[] p, List<int[]> dreiecke, double c)
    {
        var punkte = new List<P3>();
        var anKante = new Dictionary<(int, int), int>();
        int Kreuzung(int a, int b)
        {
            var key = a < b ? (a, b) : (b, a);
            if (anKante.TryGetValue(key, out var i)) return i;
            var pa = p[key.Item1]; var pb = p[key.Item2];
            punkte.Add(pa + (pb - pa) * ((c - pa.W) / (pb.W - pa.W)));
            anKante[key] = punkte.Count - 1;
            return punkte.Count - 1;
        }
        var nachbarn = new Dictionary<int, List<int>>();
        void Verbinden(int a, int b)
        {
            if (!nachbarn.TryGetValue(a, out var la)) nachbarn[a] = la = [];
            if (!nachbarn.TryGetValue(b, out var lb)) nachbarn[b] = lb = [];
            la.Add(b); lb.Add(a);
        }
        foreach (var d in dreiecke)
        {
            bool o0 = p[d[0]].W >= c, o1 = p[d[1]].W >= c, o2 = p[d[2]].W >= c;
            if (o0 == o1 && o1 == o2) continue;
            var k = new List<int>(2);
            if (o0 != o1) k.Add(Kreuzung(d[0], d[1]));
            if (o1 != o2) k.Add(Kreuzung(d[1], d[2]));
            if (o2 != o0) k.Add(Kreuzung(d[2], d[0]));
            if (k.Count == 2 && k[0] != k[1]) Verbinden(k[0], k[1]);
        }
        var zuege = new List<List<P3>>();
        var benutzt = new HashSet<int>();
        foreach (var start in nachbarn.Keys)
        {
            if (!benutzt.Add(start)) continue;
            var zug = new List<int> { start };
            int vorher = -1, jetzt = start;
            while (true)
            {
                int naechster = -1;
                foreach (var n in nachbarn[jetzt]) if (n != vorher && !benutzt.Contains(n)) { naechster = n; break; }
                if (naechster < 0) break;
                benutzt.Add(naechster);
                zug.Add(naechster);
                vorher = jetzt; jetzt = naechster;
            }
            if (zug.Count >= 3) zuege.Add(zug.Select(i => punkte[i]).ToList());
        }
        return zuege;
    }

    /// <summary>Schnittzüge als Schnittfläche (gerade/ungerade vereinigt) und als Dreiecke für die Verdeckung.</summary>
    void NetzSchnitt(int element, List<List<P3>> zuege)
    {
        var c = new ClipperD(Polygone.Praezision) { PreserveCollinear = false };
        c.AddSubject(new PathsD(zuege.Select(z => SchnittGeometrie.Pfad(z.Select(q => q.Projektion)))));
        var flaeche = new PathsD();
        c.Execute(ClipType.Union, FillRule.EvenOdd, flaeche);
        flaeche.RemoveAll(x => Math.Abs(Clipper.Area(x)) < Polygone.Splitter);
        if (flaeche.Count == 0) return;
        if (!Schnitte.TryGetValue(element, out var l)) Schnitte[element] = l = [];
        l.AddRange(flaeche);
        foreach (var f in Polygone.Flaechen(flaeche))
        {
            var punkte = new List<(double X, double Y)>();
            List<int> Ring(List<double[]> r) { var idx = new List<int>(); foreach (var q in r) { idx.Add(punkte.Count); punkte.Add((q[0], q[1])); } return idx; }
            var aussen = Ring(f.Aussen);
            var loecher = f.Loecher.Select(r => (IReadOnlyList<int>)Ring(r)).ToList();
            foreach (var d in Triangulierung.Dreiecke(punkte, aussen, loecher))
            {
                var tri = d.Select(i => new P2(punkte[i].X, punkte[i].Y)).ToList();
                if (SchnittGeometrie.Flaeche(tri) < 0) tri.Reverse();
                if (Math.Abs(SchnittGeometrie.Flaeche(tri)) > 1e-9) Schnittstuecke.Add(tri);
            }
        }
    }
}

/// <summary>Netz als Sichtteil: Tiefe der vorderen Oberfläche aus dem Strahlschnitt (Raster als Suchhilfe).</summary>
internal sealed class Netzteil : Sichtteil
{
    public required double Max { get; init; }

    readonly double[] u0, v0, w0, au, bv, du1, dv1, du2, dv2, det;
    readonly bool[] vorn;
    readonly int[] nutzbar;
    readonly List<int>[] raster;
    readonly double ru0, rv0, zelle;
    readonly int nu, nv;

    public Netzteil(P3[] p, List<int[]> dreiecke, double[] nw)
    {
        int n = dreiecke.Count;
        u0 = new double[n]; v0 = new double[n]; w0 = new double[n]; au = new double[n]; bv = new double[n];
        du1 = new double[n]; dv1 = new double[n]; du2 = new double[n]; dv2 = new double[n]; det = new double[n]; vorn = new bool[n];
        var liste = new List<int>();
        double umin = double.MaxValue, vmin = double.MaxValue, umax = double.MinValue, vmax = double.MinValue;
        var boxen = new double[n][];
        for (int i = 0; i < n; i++)
        {
            var a = p[dreiecke[i][0]]; var b = p[dreiecke[i][1]]; var c = p[dreiecke[i][2]];
            double x1 = b.U - a.U, y1 = b.V - a.V, x2 = c.U - a.U, y2 = c.V - a.V, d = x1 * y2 - x2 * y1;
            double groesse = Math.Max(Math.Abs(x1) + Math.Abs(y1), Math.Abs(x2) + Math.Abs(y2));
            if (Math.Abs(d) <= 1e-9 * groesse * groesse || Math.Abs(nw[i]) < 1e-9) continue;   // streifend: trägt zur Tiefe nichts bei
            u0[i] = a.U; v0[i] = a.V; w0[i] = a.W; du1[i] = x1; dv1[i] = y1; du2[i] = x2; dv2[i] = y2; det[i] = d;
            au[i] = ((b.W - a.W) * y2 - (c.W - a.W) * y1) / d;
            bv[i] = (x1 * (c.W - a.W) - x2 * (b.W - a.W)) / d;
            vorn[i] = nw[i] < 0;
            boxen[i] = [Math.Min(a.U, Math.Min(b.U, c.U)), Math.Min(a.V, Math.Min(b.V, c.V)), Math.Max(a.U, Math.Max(b.U, c.U)), Math.Max(a.V, Math.Max(b.V, c.V))];
            umin = Math.Min(umin, boxen[i][0]); vmin = Math.Min(vmin, boxen[i][1]); umax = Math.Max(umax, boxen[i][2]); vmax = Math.Max(vmax, boxen[i][3]);
            liste.Add(i);
        }
        nutzbar = [.. liste];
        int seiten = Math.Clamp((int)Math.Sqrt(nutzbar.Length / 2.0), 1, 64);
        ru0 = umin; rv0 = vmin;
        zelle = Math.Max(Math.Max(umax - umin, vmax - vmin) / seiten, 1e-6);
        nu = Math.Max(1, (int)Math.Ceiling((umax - umin) / zelle) + 1); nv = Math.Max(1, (int)Math.Ceiling((vmax - vmin) / zelle) + 1);
        raster = new List<int>[nutzbar.Length == 0 ? 0 : nu * nv];
        foreach (var i in nutzbar)
        {
            var bx = boxen[i];
            int i0 = Zelle(bx[0], ru0), i1 = Zelle(bx[2], ru0), j0 = Zelle(bx[1], rv0), j1 = Zelle(bx[3], rv0);
            for (int x = Math.Max(0, i0); x <= Math.Min(nu - 1, i1); x++)
                for (int y = Math.Max(0, j0); y <= Math.Min(nv - 1, j1); y++)
                    (raster[x * nv + y] ??= []).Add(i);
        }
    }

    int Zelle(double wert, double start) => (int)Math.Floor((wert - start) / zelle);

    public override double Tiefe(double u, double v)
    {
        if (raster.Length == 0) return double.NaN;
        int x = Zelle(u, ru0), y = Zelle(v, rv0);
        if (x < 0 || y < 0 || x >= nu || y >= nv || raster[x * nv + y] is not { } kandidaten) return double.NaN;
        double vorne = double.PositiveInfinity, hinten = double.PositiveInfinity;
        foreach (var i in kandidaten)
        {
            // baryzentrisch: (u, v) = A + s·(B − A) + t·(C − A)
            double pu = u - u0[i], pv = v - v0[i];
            double s = (pu * dv2[i] - du2[i] * pv) / det[i], t = (du1[i] * pv - pu * dv1[i]) / det[i];
            const double eps = 1e-9;
            if (s < -eps || t < -eps || s + t > 1 + eps) continue;
            double w = w0[i] + au[i] * pu + bv[i] * pv;
            if (w < -1e-6) continue;
            if (vorn[i]) vorne = Math.Min(vorne, w); else hinten = Math.Min(hinten, w);
        }
        if (hinten < vorne - 1e-6) return 0;   // der Strahl beginnt im Körper (von der Ebene w = 0 angeschnitten)
        if (double.IsPositiveInfinity(vorne) || vorne > Max) return double.NaN;
        return Math.Max(vorne, 0);
    }
}
