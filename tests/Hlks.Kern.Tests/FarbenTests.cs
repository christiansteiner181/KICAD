using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Heizkörperfarben (Christian 04.11.2026): im Grundriss grün, im 3D in der eingestellten Oberflächenfarbe (RAL, NCS, #RRGGBB).</summary>
public class FarbenTests
{
    [Fact]
    public void Heizkoerper_GrundrissGruen_3dInEingestellterFarbe()
    {
        var p = Rechteck();
        Ok(p.Ausfuehren("raum_erkennen", new JsonObject { ["geschoss"] = "EG", ["punkt"] = Pt(3000, 2000) }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Heizkoerper", ["kennzeichen"] = "HK-1", ["geschoss"] = "EG", ["raum"] = "R-1", ["eigenschaften"] = new JsonObject { ["farbe"] = "#123456" },
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = new JsonArray(new JsonArray(1000, 300, 150)) },
        }));
        var hk = Hole(p, "HK-1");
        Assert.Equal(Farben.Plan(p.Katalog, hk), Teil(Grafik.Plan(p, "AN-EG"), p, "HK-1").Primitive.First(x => x.Stil.Farbe is not null).Stil.Farbe);
        Assert.StartsWith("#", Farben.Plan(p.Katalog, hk));
        Assert.All(Grafik.Koerper(p, [hk.Id]).Single().Teile, t => Assert.Equal("#123456", t.Farbe));
        // Codes aus der Tabelle: jede RAL-Farbe der Tabelle wird aufgelöst, auch in anderer Schreibweise
        var ral = ((JsonObject)Tuer.Abfrage(p, "farben", new JsonObject()))["ral"]?["farben"]?.AsArray().OfType<JsonObject>().FirstOrDefault();
        if (ral is not null)
        {
            string code = (string)ral["code"]!, hex = ((string)ral["hex"]!).ToLowerInvariant();
            Assert.Equal(hex, Farben.Hex(p.Katalog, code.Replace("RAL ", "ral")));
            Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "HK-1", ["eigenschaften"] = new JsonObject { ["farbe"] = code } }));
            Assert.All(Grafik.Koerper(p, [hk.Id]).Single().Teile, t => Assert.Equal(hex, t.Farbe));
        }
        // Andere Bauteile bleiben in der Farbe ihres Mediums
        Assert.Null(Farben.Plan(p.Katalog, Hole(p, "W-1")));
    }
}
