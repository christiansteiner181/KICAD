using static Hlks.Kern.Tests.KollisionGeometrieKoerperTests;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3b, B1: Durchgänge einer Leitungsachse durch ein Bauteil (Ein- und Austritt, Tiefe, Dicke, Winkel, Mitte),
/// Projektion des Querschnitts auf die Bauteilebene (umschliessendes Rechteck bzw. Kreis, schräg = Ellipse) und Öffnung im
/// Bauteil prüfen (Grundsatzentscheid 8). Handrechnungen in mm; Durchmesser der Testrohrserie FIKTIV (da = DN + 5).</summary>
public class KollisionGeometrieDurchgangTests
{
    static void Nahe(V3 erwartet, V3? ist, double tol = 1e-6)
    {
        Assert.NotNull(ist);
        Assert.True((erwartet - ist!.Value).Laenge <= tol, $"erwartet {erwartet}, ist {ist}");
    }

    /// <summary>Decke 6000 × 4000, Oberkante 3000, Dicke 250 (z 2750 … 3000).</summary>
    static Pruefnetz Decke() => Kasten(0, 0, 2750, 6000, 4000, 3000);

    /// <summary>Wand 200 (y −100 … 100) längs x, 0 … 6000, Höhe 3000.</summary>
    static Pruefnetz Wand() => Kasten(0, -100, 0, 6000, 100, 3000);

    [Fact]
    public void RohrSenkrechtDurchDecke_EinUndAustritt_Tiefe250_Mitte()
    {
        var a = new V3(1500, 2500, 2000); var b = new V3(1500, 2500, 4000);
        // zwei Durchstosspunkte: Unterseite z = 2750 (t = 0.375), Oberseite z = 3000 (t = 0.5)
        var tr = Decke().Treffer(a, b);
        Assert.Equal(2, tr.Count);
        Assert.Equal(0.375, tr[0].T, 12); Assert.Equal(0.5, tr[1].T, 12);
        var g = Assert.Single(Durchgaenge.Finden(Decke(), a, b));
        Assert.Equal(0.375, g.T0, 9); Assert.Equal(0.5, g.T1, 9);
        Nahe(new V3(1500, 2500, 2750), g.Eintritt); Nahe(new V3(1500, 2500, 3000), g.Austritt);
        Nahe(new V3(0, 0, -1), g.NormaleEin); Nahe(new V3(0, 0, 1), g.NormaleAus);
        Assert.Equal(250, g.Tiefe, 9);
        Assert.Equal(250, g.Dicke, 9);
        Assert.Equal(90, g.Winkel, 9);
        Nahe(new V3(1500, 2500, 2875), g.Mitte);
        Assert.False(g.BeginntInnen); Assert.False(g.EndetInnen);
        // Projektion: DN 20 der Testserie da = 25 (FIKTIV), Dämmung 20 → Ø 65, rund
        var p = Durchgaenge.Projektion(g, Achskoerper.Kapsel(a, b, 12.5), zuschlag: 20);
        Assert.True(p.Rund);
        Assert.Equal(65, p.Kreis, 9); Assert.Equal(65, p.Breite, 9); Assert.Equal(65, p.Hoehe, 9);
        Assert.Equal(65, p.DurchbruchBreite, 9); Assert.Equal(65, p.DurchbruchHoehe, 9);
        // flache Fläche: U = x, V = y (unabhängig von der Eintrittsseite)
        Nahe(new V3(1, 0, 0), p.AchseU); Nahe(new V3(0, 1, 0), p.AchseV);
        var von_oben = Assert.Single(Durchgaenge.Finden(Decke(), b, a));
        Nahe(new V3(0, 0, 1), von_oben.NormaleEin);
        Nahe(new V3(1, 0, 0), Durchgaenge.Projektion(von_oben, Achskoerper.Kapsel(b, a, 12.5)).AchseU);
    }

    [Fact]
    public void Leitung_ZweiDurchgaenge_DurchDieselbeDecke_UndBeginntInnen()
    {
        // U-förmige Leitung: hinunter durch die Decke, unten quer, wieder hinauf → zwei Durchgänge (Teilstücke 0 und 2)
        var n = Decke();
        var achse = new List<V3> { new(1000, 1000, 3500), new(1000, 1000, 2500), new(3000, 1000, 2500), new(3000, 1000, 3500) };
        var leitung = new Pruefkoerper
        {
            Kennung = "L", Bereich = "hlks", Quelle = "achse", Achse = achse,
            Achsen = [Achskoerper.Kapsel(achse[0], achse[1], 15), Achskoerper.Kapsel(achse[1], achse[2], 15), Achskoerper.Kapsel(achse[2], achse[3], 15)],
        };
        var bauteil = new Pruefkoerper { Kennung = "D", Bereich = "architektur", Quelle = "koerper", Netz = n, Huelle = n.Huelle, HuelleOhneDaemmung = n.Huelle };
        var g = Durchgaenge.Finden(leitung, bauteil);
        Assert.Equal(2, g.Count);
        Assert.Equal([0, 2], g.Select(x => x.Teilstueck));
        Assert.All(g, x => Assert.Equal(250, x.Tiefe, 9));
        Nahe(new V3(1000, 1000, 2875), g[0].Mitte); Nahe(new V3(3000, 1000, 2875), g[1].Mitte);
        // Achse beginnt in der Decke: Durchgang ab t = 0, ohne Eintrittsnormale
        var halb = Assert.Single(Durchgaenge.Finden(n, new V3(500, 500, 2900), new V3(500, 500, 3500)));
        Assert.True(halb.BeginntInnen);
        Assert.Null(halb.NormaleEin);
        Assert.Equal(100, halb.Tiefe, 9);
        // keine Querung neben der Decke
        Assert.Empty(Durchgaenge.Finden(n, new V3(7000, 500, 2000), new V3(7000, 500, 4000)));
    }

    [Fact]
    public void RohrSchraeg45DurchWand200_TiefeWurzel2_Ellipse_UmschliessendesRechteck()
    {
        // waagrecht in z = 1000, Richtung (1, 1, 0)/√2, quert y = −100 … 100 bei t = 1/3 … 2/3
        var a = new V3(2700, -300, 1000); var b = new V3(3300, 300, 1000);
        var g = Assert.Single(Durchgaenge.Finden(Wand(), a, b));
        Assert.Equal(1.0 / 3, g.T0, 9); Assert.Equal(2.0 / 3, g.T1, 9);
        Assert.Equal(200 * Math.Sqrt(2), g.Tiefe, 9);   // 282.84 mm entlang der Achse
        Assert.Equal(200, g.Dicke, 9);                  // Wanddicke senkrecht zur Fläche
        Assert.Equal(45, g.Winkel, 9);
        Nahe(new V3(3000, 0, 1000), g.Mitte);
        Nahe(new V3(0, -1, 0), g.NormaleEin); Nahe(new V3(0, 1, 0), g.NormaleAus);
        // Rohr DN 20 (da 25, FIKTIV) + Dämmung 20 → R = 32.5; Ellipse a = R/cos 45° = 32.5·√2, b = 32.5
        var p = Durchgaenge.Projektion(g, Achskoerper.Kapsel(a, b, 12.5), zuschlag: 20);
        Assert.False(p.Rund);
        Assert.Equal(32.5 * Math.Sqrt(2), p.HalbachseA, 9); Assert.Equal(32.5, p.HalbachseB, 9);
        Nahe(new V3(1, 0, 0), p.RichtungA);
        // Wand: U waagrecht (x), V nach oben (z)
        Nahe(new V3(1, 0, 0), p.AchseU); Nahe(new V3(0, 0, 1), p.AchseV);
        Assert.Equal(65 * Math.Sqrt(2), p.Breite, 9);   // 91.92
        Assert.Equal(65, p.Hoehe, 9);
        Assert.Equal(65 * Math.Sqrt(2), p.Kreis, 9);
        // Durchbruch über die ganze Wanddicke: Ein- und Austritt um 200 · tan 45° = 200 versetzt → 91.92 + 200
        Assert.Equal(65 * Math.Sqrt(2) + 200, p.DurchbruchBreite, 9);
        Assert.Equal(65, p.DurchbruchHoehe, 9);
        // Rechteckkanal 400 (b waagrecht) × 200 schräg 45°: Breite in der Wand 400·√2, Höhe 200
        var k = Durchgaenge.Projektion(g, Achskoerper.Quader(a, b, 400, 200));
        Assert.Equal(400 * Math.Sqrt(2), k.Breite, 9);
        Assert.Equal(200, k.Hoehe, 9);
        Assert.Equal(400 * Math.Sqrt(2) + 200, k.DurchbruchBreite, 9);
        // vorgegebene Ebenenachse (in die Ebene projiziert)
        var v = Durchgaenge.Projektion(g, Achskoerper.Kapsel(a, b, 12.5), 20, achseU: new V3(0, 0, 1));
        Assert.Equal(65, v.Breite, 9); Assert.Equal(65 * Math.Sqrt(2), v.Hoehe, 9);
    }

    [Fact]
    public void Wand_AusZweiSchichten_EinDurchgang()
    {
        var n = Pruefnetz.Aus([NetzBau.Quader([0, -100, 0], [6000, 50, 3000]), NetzBau.Quader([0, 50, 0], [6000, 100, 3000])]);
        var g = Assert.Single(Durchgaenge.Finden(n, new V3(3000, -500, 1000), new V3(3000, 500, 1000)));
        Assert.Equal(200, g.Tiefe, 9);
    }

    // ------------------------------------------------------------------ Öffnung prüfen

    /// <summary>Decke wie oben mit Loch 1000 … 2000 × 1000 … 2000 (Profil mit Loch, wie die IFC-Testdatei).</summary>
    static Pruefnetz DeckeMitLoch() => Pruefnetz.Aus(NetzBau.Extrusion("xy", [(0, 0), (6000, 0), (6000, 4000), (0, 4000)],
        [[(1000, 1000), (2000, 1000), (2000, 2000), (1000, 2000)]], 2750, 3000));

    [Fact]
    public void Oeffnung_RundUndRechteck_ImLochOffen_SonstNicht()
    {
        var n = DeckeMitLoch();
        Assert.True(n.Geschlossen);
        var z = new V3(0, 0, 1);
        var m = new V3(1500, 1500, 2875);
        var o = Durchgaenge.OeffnungRund(n, m, z, 600, 800);
        Assert.True(o.Offen); Assert.Equal(1, o.AnteilFrei); Assert.Null(o.Treffer);
        Assert.Equal(49, o.Strahlen);
        // Ø 1200 > Loch 1000: die Leibung stört; ein Teil der Strahlen ist frei
        var z12 = Durchgaenge.OeffnungRund(n, m, z, 600, 1200);
        Assert.False(z12.Offen); Assert.NotNull(z12.Treffer);
        Assert.InRange(z12.AnteilFrei, 0.01, 0.99);
        // massive Stelle
        var voll = Durchgaenge.OeffnungRund(n, new V3(4000, 3000, 2875), z, 600, 300);
        Assert.False(voll.Offen); Assert.Equal(0, voll.AnteilFrei);
        // Rechteck genau so gross wie das Loch: offen (Rand 0.1 mm), etwas grösser: nicht
        Assert.True(Durchgaenge.OeffnungRechteck(n, m, z, 600, 1000, 1000, new V3(1, 0, 0)).Offen);
        var gross = Durchgaenge.OeffnungRechteck(n, m, z, 600, 1001, 1000, new V3(1, 0, 0));
        Assert.False(gross.Offen);
        // Strahlen 7 × 7: die beiden äusseren Spalten liegen bei x = 999.6 bzw. 2000.4 im Beton, die übrigen 5 × 7 frei
        Assert.Equal(35.0 / 49, gross.AnteilFrei, 12);
        // um 45° gedrehtes Quadrat passt nicht ins Loch
        Assert.False(Durchgaenge.OeffnungRechteck(n, m, z, 600, 900, 900, new V3(1, 1, 0)).Offen);
    }

    [Fact]
    public void Oeffnung_InWand_WaagrechteAchse()
    {
        // Wand mit Aussparung 400 × 300 (x 2800 … 3200, z 900 … 1200), als drei Quader darum herum und zwei darüber/darunter
        var n = Pruefnetz.Aus(
        [
            NetzBau.Quader([0, -100, 0], [2800, 100, 3000]), NetzBau.Quader([3200, -100, 0], [6000, 100, 3000]),
            NetzBau.Quader([2800, -100, 0], [3200, 100, 900]), NetzBau.Quader([2800, -100, 1200], [3200, 100, 3000]),
        ]);
        var y = new V3(0, 1, 0);
        Assert.True(Durchgaenge.OeffnungRechteck(n, new V3(3000, 0, 1050), y, 400, 400, 300, new V3(1, 0, 0)).Offen);
        Assert.True(Durchgaenge.OeffnungRund(n, new V3(3000, 0, 1050), y, 400, 300).Offen);
        Assert.False(Durchgaenge.OeffnungRund(n, new V3(3000, 0, 1050), y, 400, 301).Offen);
        Assert.False(Durchgaenge.OeffnungRechteck(n, new V3(3000, 0, 1100), y, 400, 400, 300, new V3(1, 0, 0)).Offen);   // 50 zu hoch
    }
}
