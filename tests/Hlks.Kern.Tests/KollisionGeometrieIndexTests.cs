namespace Hlks.Kern.Tests;

/// <summary>Modul 3b, B1: räumlicher Index (Hüllquader-Baum) – findet genau die Kandidaten (Vergleich mit dem Durchprobieren
/// aller Hüllquader), Paare zwischen zwei Mengen und innerhalb einer Menge, mit Abstand, Strahl, nächster Eintrag, nächstes
/// Paar, leere und entartete Mengen.</summary>
public class KollisionGeometrieIndexTests
{
    static List<Huelle3> Zufall(int n, int saat, double feld = 100000, double gross = 2000)
    {
        var r = new Random(saat);
        var aus = new List<Huelle3>(n);
        for (int i = 0; i < n; i++)
        {
            var a = new V3(r.NextDouble() * feld, r.NextDouble() * feld, r.NextDouble() * feld / 10);
            aus.Add(Huelle3.Aus(a, a + new V3(r.NextDouble() * gross, r.NextDouble() * gross, r.NextDouble() * gross / 4)));
        }
        return aus;
    }

    [Fact]
    public void Kandidaten_GenauWieDurchprobieren_MitUndOhneAbstand()
    {
        var boxen = Zufall(5000, 1);
        var idx = new Raumindex(boxen);
        Assert.Equal(5000, idx.Anzahl);
        foreach (var q in Zufall(200, 2, gross: 5000))
            foreach (var abstand in new[] { 0.0, 50, 1000 })
            {
                var erwartet = Enumerable.Range(0, boxen.Count).Where(i => boxen[i].Schneidet(q, abstand)).ToHashSet();
                var ist = idx.Kandidaten(q, abstand);
                Assert.Equal(ist.Count, ist.Distinct().Count());
                Assert.True(erwartet.SetEquals(ist), $"Abstand {abstand}: erwartet {erwartet.Count}, ist {ist.Count}");
            }
    }

    [Fact]
    public void Kandidaten_Handfall_GenauDieBeruehrtenUndDieImAbstand()
    {
        var boxen = new List<Huelle3>
        {
            Huelle3.Aus(new V3(0, 0, 0), new V3(100, 100, 100)),        // 0: schneidet
            Huelle3.Aus(new V3(150, 0, 0), new V3(250, 100, 100)),      // 1: 50 daneben (x)
            Huelle3.Aus(new V3(100, 100, 100), new V3(200, 200, 200)),  // 2: Ecke an Ecke (berührt)
            Huelle3.Aus(new V3(0, 0, 400), new V3(100, 100, 500)),      // 3: 300 darüber
        };
        var idx = new Raumindex(boxen);
        var q = Huelle3.Aus(new V3(50, 50, 50), new V3(100, 100, 100));
        Assert.Equal([0, 2], idx.Kandidaten(q).Order());
        Assert.Equal([0, 1, 2], idx.Kandidaten(q, 50).Order());
        Assert.Equal([0, 1, 2], idx.Kandidaten(q, 299).Order());
        Assert.Equal([0, 1, 2, 3], idx.Kandidaten(q, 300).Order());
    }

    [Fact]
    public void Paare_ZweiMengen_UndInnerhalb_GenauWieDurchprobieren()
    {
        var a = Zufall(3000, 3); var b = Zufall(2000, 4);
        var ia = new Raumindex(a); var ib = new Raumindex(b);
        foreach (var abstand in new[] { 0.0, 100 })
        {
            var erwartet = new HashSet<(int, int)>();
            for (int i = 0; i < a.Count; i++) for (int j = 0; j < b.Count; j++) if (a[i].Schneidet(b[j], abstand)) erwartet.Add((i, j));
            var ist = ia.Paare(ib, abstand);
            Assert.Equal(ist.Count, ist.Distinct().Count());
            Assert.True(erwartet.SetEquals(ist), $"zwei Mengen, Abstand {abstand}: erwartet {erwartet.Count}, ist {ist.Count}");
            var selbst = new HashSet<(int, int)>();
            for (int i = 0; i < a.Count; i++) for (int j = i + 1; j < a.Count; j++) if (a[i].Schneidet(a[j], abstand)) selbst.Add((i, j));
            var istSelbst = ia.Paare(abstand);
            Assert.Equal(istSelbst.Count, istSelbst.Distinct().Count());
            Assert.All(istSelbst, x => Assert.True(x.A < x.B));
            Assert.True(selbst.SetEquals(istSelbst), $"innerhalb, Abstand {abstand}: erwartet {selbst.Count}, ist {istSelbst.Count}");
        }
    }

    [Fact]
    public void Strahl_Naechster_NaechstesPaar()
    {
        var boxen = Zufall(2000, 5);
        var idx = new Raumindex(boxen);
        // Strahl/Strecke: genau die Quader, die die Strecke trifft (Vergleich mit dem Schichtenverfahren je Quader)
        var o = new V3(0, 50000, 2000); var r = new V3(100000, 3000, 500);
        var ist = new List<int>();
        idx.Strahl(o, r, 0, 1, 0, ist);
        var erwartet = Enumerable.Range(0, boxen.Count).Where(i => Trifft(boxen[i], o, r)).ToHashSet();
        Assert.True(erwartet.SetEquals(ist));
        // nächster Quader zu einem Punkt (Wert = Quaderabstand)
        var p = new V3(42000, 17000, 3000);
        var (e, w) = idx.Naechster(p, i => boxen[i].Abstand(p));
        Assert.Equal(boxen.Min(x => x.Abstand(p)), w, 9);
        Assert.Equal(w, boxen[e].Abstand(p), 9);
        Assert.Equal((-1, double.PositiveInfinity), idx.Naechster(new V3(-1e7, 0, 0), i => boxen[i].Abstand(new V3(-1e7, 0, 0)), max: 10));
        // nächstes Paar zweier Mengen
        var b = Zufall(500, 6);
        var ib = new Raumindex(b);
        var (x, y, d) = idx.NaechstesPaar(ib, (i, j) => boxen[i].Abstand(b[j]));
        double min = double.MaxValue;
        foreach (var h in boxen) foreach (var g in b) min = Math.Min(min, h.Abstand(g));
        Assert.Equal(min, d, 9);
        Assert.Equal(d, boxen[x].Abstand(b[y]), 9);
    }

    static bool Trifft(Huelle3 h, V3 o, V3 r)
    {
        double t0 = 0, t1 = 1;
        for (int k = 0; k < 3; k++)
        {
            double oo = k == 0 ? o.X : k == 1 ? o.Y : o.Z, rr = k == 0 ? r.X : k == 1 ? r.Y : r.Z;
            double lo = k == 0 ? h.Min.X : k == 1 ? h.Min.Y : h.Min.Z, hi = k == 0 ? h.Max.X : k == 1 ? h.Max.Y : h.Max.Z;
            if (Math.Abs(rr) < 1e-300) { if (oo < lo || oo > hi) return false; continue; }
            double a = (lo - oo) / rr, c = (hi - oo) / rr;
            if (a > c) (a, c) = (c, a);
            t0 = Math.Max(t0, a); t1 = Math.Min(t1, c);
            if (t0 > t1) return false;
        }
        return true;
    }

    [Fact]
    public void Leer_Einzeln_GleicheMittelpunkte()
    {
        var leer = new Raumindex([]);
        Assert.Empty(leer.Kandidaten(Huelle3.Aus(default, new V3(1, 1, 1))));
        Assert.Empty(leer.Paare());
        Assert.True(leer.Gesamt.IstLeer);
        var eins = new Raumindex([Huelle3.Aus(default, new V3(1, 1, 1))]);
        Assert.Equal([0], eins.Kandidaten(Huelle3.Aus(new V3(0.5, 0.5, 0.5), new V3(2, 2, 2))));
        Assert.Empty(eins.Paare());
        // 1000 gleiche Quader: Baum entsteht (Teilung nach Reihenfolge), alle Paare gefunden
        var gleich = new Raumindex(Enumerable.Repeat(Huelle3.Aus(default, new V3(1, 1, 1)), 1000).ToList());
        Assert.Equal(1000, gleich.Kandidaten(Huelle3.Aus(default, new V3(1, 1, 1))).Count);
        Assert.Equal(1000 * 999 / 2, gleich.Paare().Count);
    }

    [Fact]
    public void Generisch_UeberObjekten()
    {
        var namen = new[] { "a", "b", "c" };
        var lage = new Dictionary<string, Huelle3>
        {
            ["a"] = Huelle3.Aus(default, new V3(10, 10, 10)), ["b"] = Huelle3.Aus(new V3(5, 5, 5), new V3(20, 20, 20)), ["c"] = Huelle3.Aus(new V3(100, 0, 0), new V3(110, 10, 10)),
        };
        var idx = new Raumindex<string>(namen, n => lage[n]);
        Assert.Equal(["a", "b"], idx.Kandidaten(Huelle3.Aus(new V3(6, 6, 6), new V3(7, 7, 7))).Order());
        Assert.Equal([("a", "b")], idx.Paare().Select(p => string.CompareOrdinal(p.A, p.B) < 0 ? (p.A, p.B) : (p.B, p.A)));
        var andere = new Raumindex<int>([1], _ => Huelle3.Aus(new V3(95, 0, 0), new V3(99, 1, 1)));
        Assert.Empty(idx.Paare(andere));
        Assert.Equal([("c", 1)], idx.Paare(andere, 1));
    }
}
