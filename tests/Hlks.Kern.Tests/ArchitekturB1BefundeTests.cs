using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Befunde aus Auftrag B1 (Modul 1b, 17.10.2026): Brüstung ab OK Boden statt ab Wandunterkante,
/// zusammengefasste Hinweise der Nachführung, Zwischenspeicher der Wandlagen.</summary>
public class ArchitekturB1BefundeTests
{
    [Fact]
    public void Bruestung_GiltAbOkBoden_AuchWennDieWandDarunterBeginnt()
    {
        // IFC-Wände beginnen oft auf der Rohdecke darunter: hier 300 mm unter dem Geschossboden
        var p = Rechteck();
        Ok(p.Ausfuehren("element_aendern", new JsonObject
        {
            ["element"] = "W-1", ["geometrie"] = new JsonObject { ["art"] = "linie", ["punkte"] = Pts([0, 0, -300], [6000, 0, -300]), ["dicke"] = 200, ["hoehe"] = 3300 },
        }));
        Ok(Fenster(p, bruestung: 900, hoehe: 1200));
        Ok(Tuer(p, abstand: 1000));
        var w = Wandgeometrie.Lage(p.Modell, Hole(p, "W-1"))!;
        Assert.Equal(300, w.Boden);
        var f = Wandgeometrie.Oeffnung(p.Katalog, Hole(p, "F-1"), w)!;
        Assert.Equal(1200, f.Unten);   // 300 Boden + 900 Brüstung über der Wandunterkante
        Assert.Equal(2400, f.Oben);
        var t = Wandgeometrie.Oeffnung(p.Katalog, Hole(p, "T-1"), w)!;
        Assert.Equal(300, t.Unten);    // Tür ab OK Boden
        // Körper: Glas absolut von 900 bis 2100
        var glas = Grafik.Koerper(p, [Hole(p, "F-1").Id]).Single().Teile.Single();
        Assert.Equal(1500, glas.Mitte![2], 3);
        Assert.Equal(1200, glas.Masse![2], 3);
    }

    [Fact]
    public void Bruestung_WandAufDemBoden_UnveraendertWieModul1a()
    {
        var p = Rechteck();
        Ok(Fenster(p));
        var w = Wandgeometrie.Lage(p.Modell, Hole(p, "W-1"))!;
        Assert.Equal(0, w.Boden);
        Assert.Equal(900, Wandgeometrie.Oeffnung(p.Katalog, Hole(p, "F-1"), w)!.Unten);
    }

    [Fact]
    public void Nachfuehrung_VielePasstNichtMehr_EinHinweisMitZahlUndGrund()
    {
        var p = Rechteck();
        foreach (var a in new double[] { 600, 1500, 2500, 3500, 4500, 5400 })
            Ok(Fenster(p, abstand: a, breite: 500));
        var e = Ok(p.Ausfuehren("element_aendern", new JsonObject
        {
            ["element"] = "W-1", ["geometrie"] = new JsonObject { ["art"] = "linie", ["punkte"] = Pts([0, 0], [6000, 0]), ["dicke"] = 200, ["hoehe"] = 1000 },
        }));
        var h = Assert.Single(e.Hinweise, x => x.Contains("passt nicht"));
        Assert.StartsWith("6 Öffnungen passen nicht (mehr) in ihre Wand: 6× höher als die Wand.", h);
        Assert.Contains("Beispiele: F-1 passt nicht mehr in W-1", h);
    }

    [Fact]
    public void Nachfuehrung_WenigePasstNichtMehr_BleibenEinzeln()
    {
        var p = Rechteck();
        Ok(Fenster(p, abstand: 1500, breite: 500));
        Ok(Fenster(p, abstand: 4500, breite: 500));
        var e = Ok(p.Ausfuehren("element_aendern", new JsonObject
        {
            ["element"] = "W-1", ["geometrie"] = new JsonObject { ["art"] = "linie", ["punkte"] = Pts([0, 0], [6000, 0]), ["dicke"] = 200, ["hoehe"] = 1000 },
        }));
        Assert.Equal(2, e.Hinweise.Count(x => x.Contains("passt nicht mehr in W-1")));
    }

    [Fact]
    public void Koerper_MitZwischenspeicher_GleichWieEinzeln()
    {
        var p = Rechteck();
        Ok(Fenster(p));
        Ok(Tuer(p, wand: "W-3"));
        var alle = Grafik.Koerper(p).ToDictionary(k => k.Id);
        foreach (var kz in new[] { "W-1", "W-2", "W-3", "W-4", "F-1", "T-1" })
        {
            var id = Hole(p, kz).Id;
            var einzeln = new ArchitekturGrafik().Koerper(p, Hole(p, kz))!;   // ohne Lauf: kein Zwischenspeicher
            Assert.Equal(System.Text.Json.JsonSerializer.Serialize(einzeln), System.Text.Json.JsonSerializer.Serialize(alle[id]));
        }
    }
}
