// Quest App P5b: Ergänzungen der Bedienung – Wegwischen bricht eine laufende Prüfaufgabe ab (seit V1a die eigene Geste Wegwischen, V1aSymboleTests), «Antippen daneben» als
// Fehlgriff, die neuen Stellschrauben (Röntgen-Hand, flache Hand) und Varianten (teleport, messen), die Tabelle der Gesten.

using System.Text.Json;
using Hlks.Xr.Bedienung;
using static Hlks.Xr.Tests.T;

namespace Hlks.Xr.Tests;

public class P5bBedienungTests
{
    static JsonElement W(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Pinch_Wisch_nach_vorn_löst_seit_V1a_nichts_mehr_aus_das_Wegwischen_schliesst()
    {
        // V1a (Christian 24.10.2026: «Ja, auch offene Glas-Apps werden mit dem Wegwischen geschlossen; Pinch + Wisch nach vorn entfällt»): weder App noch Aufgabe noch Apps
        foreach (var k in new[] { new GestenKontext(false, true, true, true, AufgabeOffen: true), new GestenKontext(false, false, true, true, AufgabeOffen: true),
            new GestenKontext(true, false, false, true, AufgabeOffen: true), new GestenKontext(false, false, true) })
            Assert.Equal(GestenAktion.Nichts, Gestenbedeutung.Wisch(WischRichtung.Vor, k));
        Assert.Equal(GestenAktion.EtageHoch, Gestenbedeutung.Wisch(WischRichtung.Hoch, new GestenKontext(false, false, true, true, AufgabeOffen: true)));   // sonst unverändert
        Assert.DoesNotContain(Gestenbedeutung.Tabelle, x => x.Geste.Contains("Wisch nach vorn", StringComparison.Ordinal));
        var text = string.Join(" ", Gestenbedeutung.Tabelle.Select(x => x.Geste + " " + x.Bedeutung));
        Assert.Contains("Flache Hand", text);
        Assert.Contains("Röntgen-Hand", text);
        Assert.Contains("Prüfaufgabe", text);
    }

    [Fact]
    public void Antippen_daneben_knapp_neben_der_Kachel_aber_nicht_weit_weg_und_nicht_auf_ihr()
    {
        var kachel = XrTabletLogik.TabletLageAus(V(0, 1.2, -0.45), V(0, 0, 1)) with { B = 0.18, H = 0.18 };
        List<BeruehrErgebnis> Stoss(double x)
        {
            var z = new BeruehrHand();
            return [.. Enumerable.Range(0, 11).Select(i => AppBeruehrung.Schritt(z, V(x, 1.2, -0.45 + 0.05 - 0.01 * i), false, [kachel]))];
        }
        var daneben = Stoss(0.09 + 0.05);                                        // 5 cm neben dem Rand: ausserhalb des Fangs (3 cm), innerhalb 9 cm
        Assert.Single(daneben, r => r.Daneben);
        Assert.DoesNotContain(daneben, r => r.Ausgeloest is not null);
        var drauf = Stoss(0.05);
        Assert.Contains(drauf, r => r.Ausgeloest == 0);
        Assert.DoesNotContain(drauf, r => r.Daneben);
        Assert.DoesNotContain(Stoss(0.09 + 0.12), r => r.Daneben);               // 12 cm daneben: keine Absicht
        Assert.DoesNotContain(Stoss(0.09 + 0.02), r => r.Daneben);               // im Fang: löst aus statt daneben
    }

    [Fact]
    public void Textumbruch_an_Wortgrenzen_lange_Woerter_hart_Zeilenwechsel_bleiben()
    {
        Assert.Equal("Spring auf die\nleuchtende\nMarkierung.", Textumbruch.Umbrechen("Spring auf die leuchtende Markierung.", 14));
        Assert.Equal("ab\ncdefghij\nkl", Textumbruch.Umbrechen("ab cdefghijkl", 8));
        Assert.Equal("eins\nzwei drei", Textumbruch.Umbrechen("eins\nzwei drei", 20));
        Assert.Equal("", Textumbruch.Umbrechen("", 10));
        Assert.Equal(52, Textumbruch.ZeichenJeZeile(0.276, 0.0095));
        Assert.Equal(8, Textumbruch.ZeichenJeZeile(0.01, 0.0095));                  // mindestens 8
        Assert.All(Textumbruch.Umbrechen(new string('x', 30) + " kurz", 10).Split('\n'), z => Assert.True(z.Length <= 10));
    }

    [Fact]
    public void Neue_Stellschrauben_und_Varianten_mit_Grenzen()
    {
        var e = XrBedienEinstellungen.Vorgabe;
        Assert.Equal(40, e.Roentgen.WinkelGrad);
        Assert.Equal(6, e.Roentgen.ReichweiteM);
        Assert.Equal(0.3, e.FlacheHand.RuheS);
        Assert.Equal(25, e.FlacheHand.ToleranzGrad);
        Assert.Equal(60, e.MitStellschraube("roentgen.winkel_grad", W("60"))!.Roentgen.WinkelGrad);
        Assert.Null(e.MitStellschraube("roentgen.winkel_grad", W("150")));
        Assert.Equal(12, e.MitStellschraube("roentgen.reichweite_m", W("12"))!.Roentgen.ReichweiteM);
        Assert.Null(e.MitStellschraube("roentgen.reichweite_m", W("0.5")));
        Assert.Equal(0.6, e.MitStellschraube("sitzung.schnitt_ruhe_s", W("0.6"))!.FlacheHand.RuheS);
        Assert.Equal(35, e.MitStellschraube("sitzung.schnitt_toleranz_grad", W("35"))!.FlacheHand.ToleranzGrad);
        Assert.Null(e.MitStellschraube("sitzung.schnitt_toleranz_grad", W("60")));
        Assert.True(e.MitStellschraube("labor.variante.teleport", W("\"stick\""))!.MitStick);
        Assert.Null(e.MitStellschraube("labor.variante.teleport", W("\"joystick\"")));
        Assert.Equal("zwei_punkte", e.MitStellschraube("labor.variante.messen", W("\"zwei_punkte\""))!.Messen);
        Assert.Equal("stick", (e with { Teleport = Varianten.Stick }).Variante("teleport"));
        Assert.Null(e.Variante("greifen"));
        Assert.Equal(["bedienung", "teleport", "etage", "messen", "umgebung", "symbole", "symbolwahl"], Varianten.Bekannt.Keys);   // V1a: «umgebung», «symbole», «symbolwahl»
        Assert.Equal(XrBedienEinstellungen.Vorgabe, (e with { Roentgen = e.Roentgen with { WinkelGrad = 90 } }).MitStellschraube("roentgen.winkel_grad", W("null")));
        foreach (var k in XrBedienEinstellungen.Schluessel) Assert.NotNull(e.MitStellschraube(k, W("null")));
        Nah(e.MitStellschrauben([new("roentgen.winkel_grad", W("30")), new("x.y", W("1"))]).Satz.Roentgen.WinkelGrad, 30);
    }
}
