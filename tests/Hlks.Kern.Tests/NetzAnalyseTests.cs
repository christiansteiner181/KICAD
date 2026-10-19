using System.Text.Json.Nodes;

namespace Hlks.Kern.Tests;

/// <summary>Netzanalyse (Modul 2, Auftrag B3) direkt an im Test gebauten Netzen, ohne Node: Hülle, Flächen, Stutzen,
/// Ansichten, Vereinfachung, Fehler, Zeit. Masse fiktiv (Testgeometrie, keine Herstellerdaten).</summary>
public class NetzAnalyseTests
{
    const double Tol = 0.01; // mm: Lage und Durchmesser an exakt gebauten Netzen

    // ---------- Testkörper ----------

    /// <summary>Baut ein Netz Fläche für Fläche; jede Fläche wird eine B-Rep-Gruppe (Dreiecke im Gegenuhrzeigersinn von aussen).</summary>
    internal sealed class Bauer
    {
        readonly List<double> ecken = [];
        readonly List<int> dreiecke = [];
        readonly List<NetzFlaechengruppe> gruppen = [];
        int start;

        int E(double[] p) { ecken.AddRange(p); return ecken.Count / 3 - 1; }
        void D(int a, int b, int c) { dreiecke.Add(a); dreiecke.Add(b); dreiecke.Add(c); }
        void Beginn() => start = dreiecke.Count / 3;
        void Ende() => gruppen.Add(new NetzFlaechengruppe(start, dreiecke.Count / 3 - 1));

        public AnalyseNetz Netz(string name = "Test", bool mitGruppen = true) =>
            new() { Name = name, Ecken = [.. ecken], Dreiecke = [.. dreiecke], Flaechen = mitGruppen ? [.. gruppen] : null };

        public int AnzahlDreiecke => dreiecke.Count / 3;

        public Bauer Viereck(double[] p0, double[] p1, double[] p2, double[] p3)
        {
            Beginn();
            int a = E(p0), b = E(p1), c = E(p2), d = E(p3);
            D(a, b, c); D(a, c, d);
            Ende();
            return this;
        }

        /// <summary>Viereck als n × n Raster (für grosse Netze).</summary>
        public Bauer Raster(double[] p0, double[] p1, double[] p3, int n)
        {
            Beginn();
            var id = new int[n + 1, n + 1];
            for (int i = 0; i <= n; i++)
                for (int j = 0; j <= n; j++)
                    id[i, j] = E([p0[0] + (p1[0] - p0[0]) * i / n + (p3[0] - p0[0]) * j / n,
                                  p0[1] + (p1[1] - p0[1]) * i / n + (p3[1] - p0[1]) * j / n,
                                  p0[2] + (p1[2] - p0[2]) * i / n + (p3[2] - p0[2]) * j / n]);
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++) { D(id[i, j], id[i + 1, j], id[i + 1, j + 1]); D(id[i, j], id[i + 1, j + 1], id[i, j + 1]); }
            Ende();
            return this;
        }

        /// <summary>Quader [x0,x1]×[y0,y1]×[z0,z1]; <paramref name="ohne"/> lässt Seiten weg (−x, +x, −y, +y, −z, +z).</summary>
        public Bauer Quader(double x0, double y0, double z0, double x1, double y1, double z1, params string[] ohne)
        {
            if (!ohne.Contains("-z")) Viereck([x0, y0, z0], [x0, y1, z0], [x1, y1, z0], [x1, y0, z0]);
            if (!ohne.Contains("+z")) Viereck([x0, y0, z1], [x1, y0, z1], [x1, y1, z1], [x0, y1, z1]);
            if (!ohne.Contains("-y")) Viereck([x0, y0, z0], [x1, y0, z0], [x1, y0, z1], [x0, y0, z1]);
            if (!ohne.Contains("+y")) Viereck([x0, y1, z0], [x0, y1, z1], [x1, y1, z1], [x1, y1, z0]);
            if (!ohne.Contains("-x")) Viereck([x0, y0, z0], [x0, y0, z1], [x0, y1, z1], [x0, y1, z0]);
            if (!ohne.Contains("+x")) Viereck([x1, y0, z0], [x1, y1, z0], [x1, y1, z1], [x1, y0, z1]);
            return this;
        }

        static (double[] b1, double[] b2) Basis(double[] a)
        {
            double[] h = Math.Abs(a[0]) < 0.9 ? [1, 0, 0] : [0, 1, 0];
            var b1 = Einheit(Kreuz(a, h));
            return (b1, Kreuz(a, b1));
        }

        static double[] Kreis(double[] m, double[] b1, double[] b2, double r, double w) =>
            [m[0] + r * (Math.Cos(w) * b1[0] + Math.Sin(w) * b2[0]),
             m[1] + r * (Math.Cos(w) * b1[1] + Math.Sin(w) * b2[1]),
             m[2] + r * (Math.Cos(w) * b1[2] + Math.Sin(w) * b2[2])];

        /// <summary>Zylindermantel von m0 entlang a (Einheitsvektor) über l; aussen = Normalen von der Achse weg.</summary>
        public Bauer Zylinder(double[] m0, double[] a, double r, double l, int seg, bool aussen, int laengs = 1, Func<double, double, double>? radius = null,
            double von = 0, double bis = 2 * Math.PI)
        {
            Beginn();
            var (b1, b2) = Basis(a);
            var id = new int[seg + 1, laengs + 1];
            for (int i = 0; i <= seg; i++)
                for (int j = 0; j <= laengs; j++)
                {
                    double w = von + (bis - von) * i / seg, t = l * j / laengs;
                    double rr = radius?.Invoke(w, t) ?? r;
                    id[i, j] = E(Kreis([m0[0] + t * a[0], m0[1] + t * a[1], m0[2] + t * a[2]], b1, b2, rr, w));
                }
            for (int i = 0; i < seg; i++)
                for (int j = 0; j < laengs; j++)
                {
                    int p0 = id[i, j], p1 = id[i + 1, j], q0 = id[i, j + 1], q1 = id[i + 1, j + 1];
                    if (aussen) { D(p0, p1, q1); D(p0, q1, q0); }
                    else { D(p0, q1, p1); D(p0, q0, q1); }
                }
            Ende();
            return this;
        }

        /// <summary>Kreisscheibe mit Normale n.</summary>
        public Bauer Scheibe(double[] m, double[] n, double r, int seg)
        {
            Beginn();
            var (b1, b2) = Basis(n);
            int c = E(m);
            var p = Enumerable.Range(0, seg).Select(i => E(Kreis(m, b1, b2, r, 2 * Math.PI * i / seg))).ToArray();
            for (int i = 0; i < seg; i++) D(c, p[i], p[(i + 1) % seg]);
            Ende();
            return this;
        }

        /// <summary>Ringfläche mit Normale n.</summary>
        public Bauer Ring(double[] m, double[] n, double ri, double ra, int seg)
        {
            Beginn();
            var (b1, b2) = Basis(n);
            var pi = Enumerable.Range(0, seg).Select(i => E(Kreis(m, b1, b2, ri, 2 * Math.PI * i / seg))).ToArray();
            var pa = Enumerable.Range(0, seg).Select(i => E(Kreis(m, b1, b2, ra, 2 * Math.PI * i / seg))).ToArray();
            for (int i = 0; i < seg; i++) { int j = (i + 1) % seg; D(pi[i], pa[i], pa[j]); D(pi[i], pa[j], pi[j]); }
            Ende();
            return this;
        }

        /// <summary>Ebenes Rechteck mit Kreisloch: Ebene durch o mit Normale n (= b1 × b2 der Kreisbasis), Rechteck
        /// [u0,u1]×[v0,v1] in den Koordinaten (b1, b2) um o, Loch um o.</summary>
        public Bauer RechteckMitLoch(double[] o, double[] n, double u0, double u1, double v0, double v1, double r, int seg)
        {
            Beginn();
            var (b1, b2) = Basis(n);
            double[] P(double u, double v) => [o[0] + u * b1[0] + v * b2[0], o[1] + u * b1[1] + v * b2[1], o[2] + u * b1[2] + v * b2[2]];
            (double u, double v) Strahl(double w)
            {
                double du = Math.Cos(w), dv = Math.Sin(w), t = double.PositiveInfinity;
                if (du > 1e-12) t = Math.Min(t, u1 / du);
                if (du < -1e-12) t = Math.Min(t, u0 / du);
                if (dv > 1e-12) t = Math.Min(t, v1 / dv);
                if (dv < -1e-12) t = Math.Min(t, v0 / dv);
                return (t * du, t * dv);
            }
            var ecken = new[] { (u1, v1), (u0, v1), (u0, v0), (u1, v0) }
                .Select(e => (w: (Math.Atan2(e.Item2, e.Item1) + 2 * Math.PI) % (2 * Math.PI), e.Item1, e.Item2)).ToList();
            for (int i = 0; i < seg; i++)
            {
                double w0 = 2 * Math.PI * i / seg, w1 = 2 * Math.PI * (i + 1) / seg;
                var (ru0, rv0) = Strahl(w0);
                var (ru1, rv1) = Strahl(w1);
                int c0 = E(P(r * Math.Cos(w0), r * Math.Sin(w0))), c1 = E(P(r * Math.Cos(w1), r * Math.Sin(w1)));
                int r0 = E(P(ru0, rv0)), r1 = E(P(ru1, rv1));
                D(c0, r0, r1);
                D(c0, r1, c1);
                foreach (var k in ecken.Where(k => k.w > w0 && k.w < w1)) D(r0, E(P(k.Item2, k.Item3)), r1);
            }
            Ende();
            return this;
        }
    }

    static double[] Kreuz(double[] a, double[] b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
    static double[] Einheit(double[] a) { double l = Math.Sqrt(a[0] * a[0] + a[1] * a[1] + a[2] * a[2]); return [a[0] / l, a[1] / l, a[2] / l]; }

    /// <summary>Quader 200 × 100 × 50 mit drei Anschlüssen (Testgeometrie):
    /// rechts (x = 200) Rohrstutzen aussen 21.3, innen 16.1, 30 lang, Achse +x durch (y 50, z 25);
    /// links (x = 0) voller Zapfen D 12, 15 lang, Achse −x durch (y 50, z 25);
    /// oben (z = 50) Bohrung D 16, 15 tief, bei (x 100, y 50).</summary>
    internal static Bauer QuaderMitAnschluessen(int seg = 48)
    {
        var b = new Bauer().Quader(0, 0, 0, 200, 100, 50, "+x", "-x", "+z");
        double[] x = [1, 0, 0], mx = [-1, 0, 0], z = [0, 0, 1];
        // Basis zu +x: b1 = z, b2 = −y → (u, v) = (z − 25, 50 − y)
        b.RechteckMitLoch([200, 50, 25], x, -25, 25, -50, 50, 10.65, seg)
         .Scheibe([200, 50, 25], x, 8.05, seg)
         .Zylinder([200, 50, 25], x, 10.65, 30, seg, aussen: true)
         .Zylinder([200, 50, 25], x, 8.05, 30, seg, aussen: false)
         .Ring([230, 50, 25], x, 8.05, 10.65, seg);
        // Basis zu −x: b1 = −z, b2 = −y → (u, v) = (25 − z, 50 − y)
        b.RechteckMitLoch([0, 50, 25], mx, -25, 25, -50, 50, 6, seg)
         .Zylinder([0, 50, 25], mx, 6, 15, seg, aussen: true)
         .Scheibe([-15, 50, 25], mx, 6, seg);
        // Basis zu +z: b1 = z × x = y, b2 = z × y = −x → (u, v) = (y − 50, 100 − x)
        b.RechteckMitLoch([100, 50, 50], z, -50, 50, -100, 100, 8, seg)
         .Zylinder([100, 50, 35], z, 8, 15, seg, aussen: false)
         .Scheibe([100, 50, 35], z, 8, seg);
        return b;
    }

    static void Nahe(double[] soll, double[] ist, double tol)
    {
        Assert.Equal(soll.Length, ist.Length);
        for (int i = 0; i < soll.Length; i++) Assert.True(Math.Abs(soll[i] - ist[i]) <= tol, $"[{i}] soll {soll[i]}, ist {ist[i]} (Toleranz {tol})");
    }

    static NetzStutzenInfo Stutzen(NetzAnalyseErgebnis e, string art) => Assert.Single(e.Stutzen, s => s.Art == art);

    // ---------- Tests ----------

    [Fact]
    public void Quader_Huelle_und_ebene_Flaechen()
    {
        var e = NetzAnalyse.Analysieren([new Bauer().Quader(0, 0, 0, 200, 100, 50).Netz()]);
        Nahe([0, 0, 0], e.Min, 1e-9);
        Nahe([200, 100, 50], e.Max, 1e-9);
        Assert.True(e.HuelleExakt);
        Assert.Equal(6, e.Flaechen.Count);
        Assert.All(e.Flaechen, f => { Assert.Equal("eben", f.Form); Assert.Equal(1, f.Sicherheit, 6); });
        var oben = Assert.Single(e.Flaechen, f => f.Normale![2] > 0.99);
        Assert.Equal(200 * 100, oben.Inhalt, 6);
        Assert.Equal(50, oben.Abstand!.Value, 6);
        Assert.Empty(e.Stutzen);
    }

    [Fact]
    public void Zylinderflaechen_Achse_Radius_und_Richtung_eingepasst()
    {
        var e = NetzAnalyse.Analysieren([QuaderMitAnschluessen().Netz()]);
        var aussen = Assert.Single(e.Flaechen, f => f.Form == "zylindrisch" && Math.Abs(f.Radius!.Value - 10.65) < 0.01);
        Assert.Equal(10.65, aussen.Radius!.Value, 6);
        Nahe([1, 0, 0], aussen.Achse!, 1e-9);
        Nahe([200, 50, 25], aussen.AchsPunkt!, 1e-6);
        Assert.Equal(30, aussen.Laenge!.Value, 6);
        Assert.Equal(360, aussen.Bogen!.Value);
        Assert.True(aussen.Konvex);
        Assert.True(aussen.Sicherheit > 0.99);
        var bohrung = Assert.Single(e.Flaechen, f => f.Form == "zylindrisch" && Math.Abs(f.Radius!.Value - 8.05) < 0.01);
        Assert.False(bohrung.Konvex);
        Assert.Equal(4, e.Flaechen.Count(f => f.Form == "zylindrisch"));
        Assert.Equal(2, e.Flaechen.Count(f => f.Form == "zylindrisch" && f.Konvex == true));
    }

    [Fact]
    public void Stutzen_Rohr_Zapfen_und_Muffe_mit_Mitte_Normale_Durchmesser()
    {
        var e = NetzAnalyse.Analysieren([QuaderMitAnschluessen().Netz()]);
        Assert.Equal(3, e.Stutzen.Count);

        var rohr = Stutzen(e, "rohr");
        Nahe([230, 50, 25], rohr.Mitte, Tol);
        Nahe([1, 0, 0], rohr.Normale, 1e-9);
        Assert.Equal(21.3, rohr.Aussendurchmesser!.Value, Tol);
        Assert.Equal(16.1, rohr.Innendurchmesser!.Value, Tol);
        Assert.Equal(30, rohr.Laenge, Tol);
        Assert.Equal(30, rohr.Bohrungstiefe!.Value, Tol);
        Assert.Equal("flaeche", rohr.AnderesEnde);
        Assert.False(rohr.Verdeckt);
        Assert.Equal(0, rohr.AbstandRand, Tol);
        Assert.True(rohr.Sicherheit >= 0.9, $"Sicherheit {rohr.Sicherheit}");

        var zapfen = Stutzen(e, "zapfen");
        Nahe([-15, 50, 25], zapfen.Mitte, Tol);
        Nahe([-1, 0, 0], zapfen.Normale, 1e-9);
        Assert.Equal(12, zapfen.Aussendurchmesser!.Value, Tol);
        Assert.Null(zapfen.Innendurchmesser);
        Assert.Equal(15, zapfen.Laenge, Tol);

        var muffe = Stutzen(e, "muffe");
        Nahe([100, 50, 50], muffe.Mitte, Tol);
        Nahe([0, 0, 1], muffe.Normale, 1e-9);
        Assert.Equal(16, muffe.Innendurchmesser!.Value, Tol);
        Assert.Null(muffe.Aussendurchmesser); // Rand der Deckfläche ist ein Rechteck, kein Kreis
        Assert.Equal(15, muffe.Laenge, Tol);
        Assert.True(rohr.Sicherheit > zapfen.Sicherheit && zapfen.Sicherheit > muffe.Sicherheit);
    }

    [Fact]
    public void Ohne_Brep_Flaechen_wird_segmentiert_und_Stutzen_bleiben_erkannt()
    {
        var mit = NetzAnalyse.Analysieren([QuaderMitAnschluessen().Netz()]);
        var ohne = NetzAnalyse.Analysieren([QuaderMitAnschluessen().Netz(mitGruppen: false)]);
        Assert.All(ohne.Flaechen, f => Assert.Equal("segmentiert", f.Quelle));
        Assert.Contains(ohne.Hinweise, h => h.Contains("segmentiert"));
        Assert.Equal(3, ohne.Stutzen.Count);
        var rohr = Stutzen(ohne, "rohr");
        Nahe([230, 50, 25], rohr.Mitte, Tol);
        Assert.Equal(21.3, rohr.Aussendurchmesser!.Value, Tol);
        Assert.Equal(16.1, rohr.Innendurchmesser!.Value, Tol);
        Assert.True(rohr.Sicherheit < Stutzen(mit, "rohr").Sicherheit, "segmentiert = weniger sicher");
    }

    [Fact]
    public void Ansicht_vorne_hinten_oben_Umriss_und_Ausdehnung()
    {
        var e = NetzAnalyse.Analysieren([QuaderMitAnschluessen().Netz()]);
        Assert.Equal(new[] { "vorne", "hinten", "links", "rechts", "oben", "unten" }, e.Ansichten.Select(a => a.Name));
        var vorne = e.Ansichten[0];
        Assert.Equal("x", vorne.U);
        Assert.Equal("z", vorne.V);
        Nahe([-15, 0, 230, 50], vorne.Box, 0.001);
        // Quader 200 × 50 + Rohrstutzen 30 × 21.3 + Zapfen 15 × 12, alles eine Kontur ohne Löcher
        Assert.Equal(200 * 50 + 30 * 21.3 + 15 * 12, vorne.Inhalt, 0.05);
        var k = Assert.Single(vorne.Konturen);
        Assert.Empty(k.Loecher);
        var hinten = e.Ansichten[1];
        Nahe([-230, 0, 15, 50], hinten.Box, 0.001); // gespiegelt: u = −x
        Assert.Equal(vorne.Inhalt, hinten.Inhalt, 6);
        var oben = e.Ansichten[4];
        Nahe([-15, 0, 230, 100], oben.Box, 0.001);
        Assert.Equal(200 * 100 + 30 * 21.3 + 15 * 12, oben.Inhalt, 0.05);
        var rechts = e.Ansichten[3];
        Assert.Equal("y", rechts.U);
        Nahe([0, 0, 100, 50], rechts.Box, 0.001);
    }

    [Fact]
    public void Ansicht_vorne_eines_Quaders_ist_Rechteck_200_x_50()
    {
        var e = NetzAnalyse.Analysieren([new Bauer().Quader(0, 0, 0, 200, 100, 50).Netz()]);
        var k = Assert.Single(e.Ansichten[0].Konturen);
        Assert.Equal(4, k.Aussen.Count);
        foreach (var p in k.Aussen) Assert.Contains(new[] { (0.0, 0.0), (200.0, 0.0), (200.0, 50.0), (0.0, 50.0) }, q => Math.Abs(q.Item1 - p[0]) < 1e-9 && Math.Abs(q.Item2 - p[1]) < 1e-9);
        Assert.Equal(10000, e.Ansichten[0].Inhalt, 6);
    }

    [Fact]
    public void Durchgehende_Bohrung_erscheint_als_Loch_in_der_Ansicht()
    {
        // Platte 100 × 100 × 20 mit Bohrung D 30 von oben nach unten (Testgeometrie)
        var b = new Bauer().Quader(0, 0, 0, 100, 100, 20, "+z", "-z");
        b.RechteckMitLoch([50, 50, 20], [0, 0, 1], -50, 50, -50, 50, 15, 64)
         .RechteckMitLoch([50, 50, 0], [0, 0, -1], -50, 50, -50, 50, 15, 64)
         .Zylinder([50, 50, 0], [0, 0, 1], 15, 20, 64, aussen: false);
        var e = NetzAnalyse.Analysieren([b.Netz()]);
        var oben = e.Ansichten[4];
        var k = Assert.Single(oben.Konturen);
        Assert.Single(k.Loecher);
        Assert.Equal(100 * 100 - 64 / 2.0 * 15 * 15 * Math.Sin(2 * Math.PI / 64), oben.Inhalt, 0.05);
        // beide Enden offen: zwei Muffen-Kandidaten (oben +z, unten −z)
        Assert.Equal(2, e.Stutzen.Count(s => s.Art == "muffe"));
    }

    [Fact]
    public void Stutzen_weit_innen_wird_nicht_gemeldet()
    {
        // Sockel 200 × 100 × 50 mit Zapfen oben bei x 150, daneben ein Turm bis z 300: Zapfenende liegt 240 mm unter dem Rand
        var b = new Bauer().Quader(0, 0, 0, 200, 100, 50, "+z");
        b.RechteckMitLoch([150, 50, 50], [0, 0, 1], -50, 50, -50, 150, 6, 32)
         .Zylinder([150, 50, 50], [0, 0, 1], 6, 10, 32, aussen: true)
         .Scheibe([150, 50, 60], [0, 0, 1], 6, 32)
         .Quader(0, 0, 50, 50, 100, 300, "-z");
        var e = NetzAnalyse.Analysieren([b.Netz()]);
        Assert.Empty(e.Stutzen);
        Assert.Contains(e.Hinweise, h => h.Contains("nicht als Stutzen gemeldet"));
    }

    [Fact]
    public void Verdeckter_Stutzen_bekommt_kleine_Sicherheit()
    {
        var frei = NetzAnalyse.Analysieren([QuaderMitAnschluessen().Netz()]);
        var b = QuaderMitAnschluessen();
        b.Quader(250, 20, 0, 255, 80, 50); // Platte vor dem Rohrstutzen
        var e = NetzAnalyse.Analysieren([b.Netz()]);
        var rohr = Stutzen(e, "rohr");
        Assert.True(rohr.Verdeckt);
        Assert.True(rohr.Sicherheit < 0.6 * Stutzen(frei, "rohr").Sicherheit);
    }

    [Fact]
    public void Zylindrischer_Behaelter_ist_Geraetekoerper_nicht_Stutzen()
    {
        var b = new Bauer()
            .Zylinder([0, 0, 0], [0, 0, 1], 200, 500, 64, aussen: true)
            .Scheibe([0, 0, 500], [0, 0, 1], 200, 64)
            .Scheibe([0, 0, 0], [0, 0, -1], 200, 64);
        var e = NetzAnalyse.Analysieren([b.Netz()]);
        Assert.Equal(2, e.Stutzen.Count);
        Assert.All(e.Stutzen, s => { Assert.True(s.Geraetekoerper); Assert.True(s.Sicherheit < 0.4); Assert.Equal("frei", s.AnderesEnde); });
    }

    [Fact]
    public void Huelle_wird_auf_exakte_Zylinderkreise_korrigiert()
    {
        // 7 Segmente: die Ecken treffen y = −r und die x-Extreme nicht
        var b = new Bauer()
            .Zylinder([0, 0, 0], [0, 0, 1], 50, 100, 7, aussen: true)
            .Scheibe([0, 0, 100], [0, 0, 1], 50, 7)
            .Scheibe([0, 0, 0], [0, 0, -1], 50, 7);
        var e = NetzAnalyse.Analysieren([b.Netz()]);
        Assert.True(e.HuelleKorrigiert);
        Nahe([-50, -50, 0], e.Min, 1e-6);
        Nahe([50, 50, 100], e.Max, 1e-6);
    }

    [Fact]
    public void Einpassfehler_senkt_Sicherheit_und_grober_Fehler_gibt_sonst()
    {
        var exakt = NetzAnalyse.Analysieren([new Bauer().Zylinder([0, 0, 0], [0, 0, 1], 50, 100, 64, true, 4).Netz()]).Flaechen.Single();
        // Radius schwankt um ±0.3 % (Welligkeit): bleibt zylindrisch, Sicherheit deutlich kleiner
        var wellig = NetzAnalyse.Analysieren([new Bauer().Zylinder([0, 0, 0], [0, 0, 1], 50, 100, 64, true, 4,
            (w, t) => 50 * (1 + 0.003 * Math.Sin(5 * w))).Netz()]).Flaechen.Single();
        // Ellipse 50 / 40: kein Zylinder
        var oval = NetzAnalyse.Analysieren([new Bauer().Zylinder([0, 0, 0], [0, 0, 1], 50, 100, 64, true, 4,
            (w, t) => 50 * 40 / Math.Sqrt(Math.Pow(40 * Math.Cos(w), 2) + Math.Pow(50 * Math.Sin(w), 2))).Netz()]).Flaechen.Single();
        Assert.Equal("zylindrisch", exakt.Form);
        Assert.True(exakt.Sicherheit > 0.999);
        Assert.Equal("zylindrisch", wellig.Form);
        Assert.True(wellig.Fehler > 0.05, $"Fehler {wellig.Fehler}");
        Assert.True(wellig.Sicherheit < 0.7 && wellig.Sicherheit > 0.2, $"Sicherheit {wellig.Sicherheit}");
        Assert.Equal("sonst", oval.Form);
        Assert.True(oval.Sicherheit <= 0.5);
    }

    [Fact]
    public void Vereinfachtes_Netz_bleibt_unter_der_Grenze()
    {
        var b = new Bauer();
        b.Raster([0, 0, 0], [0, 100, 0], [100, 0, 0], 60).Raster([0, 0, 50], [100, 0, 50], [0, 100, 50], 60);
        var e = NetzAnalyse.Analysieren([b.Netz()], new NetzAnalyseOptionen { VereinfachtMaxDreiecke = 500 });
        Assert.Equal(2 * 2 * 60 * 60, e.Dreiecke.Length / 3);
        Assert.InRange(e.VereinfachtDreiecke.Length / 3, 1, 500);
        Assert.True(e.Rasterweite > 0);
        // Vereinfachte Ecken bleiben in der Hülle (Raster-Mittel)
        for (int i = 0; i < e.VereinfachtEcken.Length; i += 3)
            for (int j = 0; j < 3; j++) Assert.InRange(e.VereinfachtEcken[i + j], e.Min[j] - 1e-9, e.Max[j] + 1e-9);
    }

    [Fact]
    public void Ungueltiges_Netz_gibt_klaren_Fehler()
    {
        var f = Assert.Throws<HlksFehler>(() => NetzAnalyse.Analysieren([new AnalyseNetz { Name = "kaputt", Ecken = [0, 0, 0, 1, 0, 0, 0, 1, 0], Dreiecke = [0, 1, 5] }]));
        Assert.Contains("verweist auf Ecke 5", f.Message);
        Assert.Throws<HlksFehler>(() => NetzAnalyse.Analysieren([new AnalyseNetz { Ecken = [0, 0], Dreiecke = [] }]));
        var leer = NetzAnalyse.Analysieren([new AnalyseNetz()]);
        Assert.Contains(leer.Hinweise, h => h.Contains("keine Dreiecke"));
        Assert.Empty(NetzAnalyse.Befunde(leer, "x.stp", "abcdef0123456789", "step 1"));
    }

    [Fact]
    public void Befunde_in_fester_Reihenfolge_mit_Ids_Einheit_und_ohne_DN()
    {
        var e = NetzAnalyse.Analysieren([QuaderMitAnschluessen(24).Netz("Geraet")]);
        var bf = NetzAnalyse.Befunde(e, "geraet.stp", "0123456789abcdef", "step 1");
        Assert.Equal(1 + e.Flaechen.Count + e.Stutzen.Count + 6, bf.Count);
        for (int i = 0; i < bf.Count; i++) Assert.Equal($"0123456789ab#{i + 1}", bf[i].Id);
        Assert.Equal("form3d", bf[0].Art);
        Assert.All(bf, b => { Assert.Equal("mm", b.Einheit); Assert.Equal("step 1", b.Leser); Assert.Equal("geraet.stp", b.Datei); });
        Assert.Equal(new[] { "form3d", "flaeche", "stutzen", "ansicht" }, bf.Select(b => b.Art).Distinct());
        var huelle = bf[0].Wert!["huelle"]!;
        Assert.Equal(245, huelle["groesse"]![0]!.GetValue<double>(), 6);
        Assert.Equal(e.Dreiecke.Length / 3, bf[0].Wert!["dreiecke"]!.AsArray().Count);
        Assert.NotNull(bf[0].Wert!["vereinfacht"]);
        var rohr = bf.First(b => b.Art == "stutzen" && b.Wert!["art"]!.GetValue<string>() == "rohr");
        Assert.Equal(21.3, rohr.Wert!["aussendurchmesser"]!.GetValue<double>(), 3);
        Assert.Null(rohr.Wert!["dn"]);
        Assert.DoesNotContain("\"dn\"", rohr.Wert!.ToJsonString());
        Assert.All(rohr.Zusatz!["flaechen"]!.AsArray(), id => Assert.Contains(bf, b => b.Id == id!.GetValue<string>() && b.Art == "flaeche"));
        var vorne = bf.First(b => b.Art == "ansicht");
        Assert.Equal("vorne", vorne.Wert!["ansicht"]!.GetValue<string>());
        Assert.Equal(245, vorne.Wert!["breite"]!.GetValue<double>(), 6);
        Assert.Equal(50, vorne.Wert!["hoehe"]!.GetValue<double>(), 6);
        // gleiche Eingabe → gleiche Befunde (stabile Ids und Werte)
        var nochmal = NetzAnalyse.Befunde(NetzAnalyse.Analysieren([QuaderMitAnschluessen(24).Netz("Geraet")]), "geraet.stp", "0123456789abcdef", "step 1");
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(bf, Json.Optionen), System.Text.Json.JsonSerializer.Serialize(nochmal, Json.Optionen));
    }

    [Fact]
    public void IFC_Netz_laesst_sich_uebernehmen()
    {
        var ifc = new Dreiecksnetz();
        int a = ifc.Punkt(0, 0, 0), b = ifc.Punkt(10, 0, 0), c = ifc.Punkt(10, 10, 0);
        ifc.Dreieck(a, b, c);
        var e = NetzAnalyse.Analysieren([AnalyseNetz.AusIfc(ifc, "Ifc")]);
        Assert.Equal("eben", Assert.Single(e.Flaechen).Form);
        Assert.Equal(50, e.Flaechen[0].Inhalt, 9);
    }

    [Fact]
    public void Halbzylinder_aus_dem_Export_werden_zu_einem_Stutzen_zusammengefasst()
    {
        // wie QuaderMitAnschluessen, aber Aussen- und Innenzylinder des Rohrstutzens je als zwei Halbflächen (übliche Teilung beim Export)
        var b = new Bauer().Quader(0, 0, 0, 200, 100, 50, "+x");
        double[] x = [1, 0, 0];
        b.RechteckMitLoch([200, 50, 25], x, -25, 25, -50, 50, 10.65, 48)
         .Scheibe([200, 50, 25], x, 8.05, 48)
         .Zylinder([200, 50, 25], x, 10.65, 30, 24, aussen: true, von: 0, bis: Math.PI)
         .Zylinder([200, 50, 25], x, 10.65, 30, 24, aussen: true, von: Math.PI, bis: 2 * Math.PI)
         .Zylinder([200, 50, 25], x, 8.05, 30, 24, aussen: false, von: 0, bis: Math.PI)
         .Zylinder([200, 50, 25], x, 8.05, 30, 24, aussen: false, von: Math.PI, bis: 2 * Math.PI)
         .Ring([230, 50, 25], x, 8.05, 10.65, 48);
        var e = NetzAnalyse.Analysieren([b.Netz()]);
        Assert.Equal(4, e.Flaechen.Count(f => f.Form == "zylindrisch"));
        Assert.All(e.Flaechen.Where(f => f.Form == "zylindrisch"), f => Assert.InRange(f.Bogen!.Value, 179.5, 181));
        var rohr = Assert.Single(e.Stutzen);
        Assert.Equal("rohr", rohr.Art);
        Assert.Equal(360, rohr.Bogen);
        Nahe([230, 50, 25], rohr.Mitte, Tol);
        Assert.Equal(21.3, rohr.Aussendurchmesser!.Value, Tol);
        Assert.Equal(16.1, rohr.Innendurchmesser!.Value, Tol);
        Assert.Equal(4, rohr.Flaechen.Count(i => e.Flaechen[i].Form == "zylindrisch"));
    }

    [Fact]
    public void Flanschloecher_sind_keine_eigenen_Stutzen()
    {
        // Quader 200 × 200 × 150, rechts Flanschscheibe D 100, 10 dick, um (y 100, z 75); im Flansch vier Sacklöcher D 8,
        // 5 tief auf Teilkreis r 35 (Testgeometrie)
        var b = new Bauer().Quader(0, 0, 0, 200, 200, 150, "+x");
        double[] x = [1, 0, 0];
        b.Viereck([200, 0, 0], [200, 200, 0], [200, 200, 150], [200, 0, 150])
         .Zylinder([200, 100, 75], x, 50, 10, 64, aussen: true)
         .Ring([210, 100, 75], x, 42, 50, 64)
         .Scheibe([210, 100, 75], x, 28, 64);
        foreach (var w in new[] { 45.0, 135, 225, 315 })
        {
            double[] m = [210, 100 + 35 * Math.Cos(w * Math.PI / 180), 75 + 35 * Math.Sin(w * Math.PI / 180)];
            b.RechteckMitLoch(m, x, -6, 6, -6, 6, 4, 32)
             .Zylinder([205, m[1], m[2]], x, 4, 5, 32, aussen: false)
             .Scheibe([205, m[1], m[2]], x, 4, 32);
        }
        var e = NetzAnalyse.Analysieren([b.Netz()]);
        var flansch = Assert.Single(e.Stutzen);
        Assert.Equal("zapfen", flansch.Art);
        Assert.False(flansch.Geraetekoerper);
        Assert.Equal(100, flansch.Aussendurchmesser!.Value, Tol);
        Nahe([210, 100, 75], flansch.Mitte, Tol);
        Assert.Contains(e.Hinweise, h => h.StartsWith("4 Bohrungen in der Anschlussebene"));
    }
}
