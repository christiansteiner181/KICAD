namespace Hlks.Kern.Tests;

/// <summary>Modul 3b, B1: Zufallsvergleiche der Schmalphase mit unabhängigen Rechnungen – Dreieck–Dreieck-Schnitt gegen den
/// Abstand (kein Schnitt wird übersehen), Innen-Test gegen die exakte Lage in einem gedrehten Quader, Kapsel–Quader-Abstand
/// gegen dichtes Abtasten der Achse, Index-Kandidaten der Prüfkörper gegen Durchprobieren. Feste Saat (wiederholbar).</summary>
public class KollisionGeometrieZufallTests
{
    static V3 Zufall(Random r, double feld) => new((r.NextDouble() - 0.5) * feld, (r.NextDouble() - 0.5) * feld, (r.NextDouble() - 0.5) * feld);

    [Fact]
    public void DreieckDreieck_UebersiehtKeinenSchnitt_SchnittHeisstAbstandNull()
    {
        var r = new Random(11);
        int schnitte = 0, getrennt = 0;
        for (int i = 0; i < 20000; i++)
        {
            var a0 = Zufall(r, 400); var a1 = a0 + Zufall(r, 600); var a2 = a0 + Zufall(r, 600);
            var b0 = Zufall(r, 400); var b1 = b0 + Zufall(r, 600); var b2 = b0 + Zufall(r, 600);
            var lage = Geometrie3.DreieckDreieck(a0, a1, a2, b0, b1, b2);
            double d = Geometrie3.DreieckDreieckAbstand(a0, a1, a2, b0, b1, b2, out _, out _);
            if (d <= 1e-9) Assert.NotEqual(DreieckLage.Getrennt, lage);
            if (lage == DreieckLage.Schneidet) { Assert.True(d <= 1e-6, $"Schnitt mit Abstand {d}"); schnitte++; }
            if (d > Geometrie3.Toleranz + 1e-9) Assert.Equal(DreieckLage.Getrennt, lage);
            if (lage == DreieckLage.Getrennt) getrennt++;
        }
        Assert.True(schnitte > 500 && getrennt > 5000, $"{schnitte} Schnitte, {getrennt} getrennt");
    }

    [Fact]
    public void Lage_GedrehterQuader_WieExakteRechnung()
    {
        var r = new Random(12);
        var ax = new V3(1, 2, 0.5).Einheit();
        var ay = new V3(-2, 1, 0).Einheit();
        var az = ax.Kreuz(ay).Einheit();
        var m = new V3(300, -200, 1000);
        double hx = 800, hy = 300, hz = 150;
        var n = Pruefnetz.Quader(m, ax, ay, az, hx, hy, hz);
        Assert.True(n.Geschlossen);
        int innen = 0;
        for (int i = 0; i < 3000; i++)
        {
            var q = m + Zufall(r, 2000);
            var w = q - m;
            double x = Math.Abs(w.Mal(ax)) - hx, y = Math.Abs(w.Mal(ay)) - hy, z = Math.Abs(w.Mal(az)) - hz;
            double aussen = Math.Max(x, Math.Max(y, z));   // > 0 aussen, < 0 innen (Abstand zur nächsten Seitenebene)
            if (Math.Abs(aussen) < 1) continue;           // nahe der Oberfläche: nicht vergleichen
            var lage = n.Lage(q);
            Assert.Equal(aussen < 0 ? Punktlage.Innen : Punktlage.Aussen, lage);
            if (lage == Punktlage.Innen) innen++;
        }
        Assert.True(innen > 100, $"{innen} innen");
    }

    [Fact]
    public void KapselQuader_Abstand_WieAbtasten()
    {
        var r = new Random(13);
        var kasten = KollisionGeometrieKoerperTests.Kasten(0, 0, 0, 1000, 600, 400);
        var h = Huelle3.Aus(new V3(0, 0, 0), new V3(1000, 600, 400));
        int verglichen = 0;
        for (int i = 0; i < 300; i++)
        {
            var a = new V3(500, 300, 200) + Zufall(r, 3000); var b = a + Zufall(r, 1500);
            double rad = 5 + r.NextDouble() * 50;
            // Abstand Achse–Quader durch Abtasten (exakt je Punkt), dann minus Radius
            double min = double.MaxValue;
            for (int k = 0; k <= 20000; k++) min = Math.Min(min, h.Abstand(a + (b - a) * (k / 20000.0)));
            double soll = min - rad;
            var kontakt = Schmalphase.Pruefen(Achskoerper.Kapsel(a, b, rad), kasten, maxAbstand: 5000);
            if (soll > 1) { Assert.Equal(Kontaktart.Getrennt, kontakt.Art); Assert.Equal(soll, kontakt.Abstand, 0); verglichen++; }
            else if (min > 1e-6 && soll < -1) Assert.Equal(Kontaktart.Schnitt, kontakt.Art);
            else if (min <= 1e-6) Assert.True(kontakt.Hart);
        }
        Assert.True(verglichen > 100, $"{verglichen} Abstände verglichen");
    }

    [Fact]
    public void Pruefkoerperindex_Kandidaten_WieDurchprobieren()
    {
        var r = new Random(14);
        var koerper = new List<Pruefkoerper>();
        for (int i = 0; i < 800; i++)
        {
            var a = Zufall(r, 50000); var b = a + Zufall(r, 4000);
            var k = Achskoerper.Kapsel(a, b, 10 + r.NextDouble() * 100);
            koerper.Add(new Pruefkoerper { Kennung = $"L{i}", Bereich = "hlks", Quelle = "achse", Achse = [a, b], Achsen = [k], Huelle = k.Huelle, HuelleOhneDaemmung = k.Huelle });
        }
        var idx = new Raumindex<Pruefkoerper>(koerper, k => k.Huelle);
        var paare = idx.Paare(25).Select(p => (p.A.Kennung, p.B.Kennung)).Select(p => string.CompareOrdinal(p.Item1, p.Item2) < 0 ? p : (p.Item2, p.Item1)).ToHashSet();
        var soll = new HashSet<(string, string)>();
        for (int i = 0; i < koerper.Count; i++)
            for (int j = i + 1; j < koerper.Count; j++)
                if (koerper[i].Huelle.Schneidet(koerper[j].Huelle, 25))
                {
                    var (x, y) = (koerper[i].Kennung, koerper[j].Kennung);
                    soll.Add(string.CompareOrdinal(x, y) < 0 ? (x, y) : (y, x));
                }
        Assert.True(soll.SetEquals(paare));
        Assert.NotEmpty(soll);
    }
}
