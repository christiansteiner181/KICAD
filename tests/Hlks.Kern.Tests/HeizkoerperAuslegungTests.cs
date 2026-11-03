using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 5 Teil B: Heizkörper auslegen – Umrechnung der Normleistung mit logarithmischer Übertemperatur und Herstellerexponent (fiktive
/// Herstellerangaben, Beispielprojekt: Heizkörper HK-E.02 im Bad E.02, Heizgruppe HG-2 55/45 °C).</summary>
public class HeizkoerperAuslegungTests
{
    [Fact]
    public void Heizkoerper_Normleistung_umgerechnet_Deckung_und_Massenstrom_wie_von_Hand()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        Ok(p.Ausfuehren("element_aendern", new JsonObject
        {
            ["element"] = "HK-E.02",
            ["eigenschaften"] = new JsonObject
            {
                ["leistung"] = new JsonObject { ["wert"] = 1000, ["quelle"] = "fiktiv" }, ["normVl"] = new JsonObject { ["wert"] = 75, ["quelle"] = "fiktiv" },
                ["normRl"] = new JsonObject { ["wert"] = 65, ["quelle"] = "fiktiv" }, ["normRaum"] = new JsonObject { ["wert"] = 20, ["quelle"] = "fiktiv" },
                ["exponent"] = new JsonObject { ["wert"] = 1.3, ["quelle"] = "fiktiv" },
            },
        }));
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "E.02", ["eigenschaften"] = new JsonObject { ["heizleistung"] = new JsonObject { ["wert"] = 400, ["quelle"] = "fiktiv" } } }));
        var z = ((JsonObject)Tuer.Abfrage(p, "heizkoerper_auslegung", new JsonObject { ["raum"] = "E.02" }))["heizkoerper"]!.AsArray().OfType<JsonObject>().Single();
        // Bad: Bürostandard 22 °C; HG-2 55/45 °C
        Assert.Equal(22, z["thetaInt"]!.GetValue<double>());
        double dt = (55 - 45) / Math.Log((55.0 - 22) / (45 - 22)), f = Math.Pow(dt / 50, 1.3);
        Assert.Equal(Math.Round(1000 * f), z["leistungBetrieb_W"]!.GetValue<double>(), 0);
        Assert.Equal(Math.Round(400 / f), z["normleistungErforderlich_W"]!.GetValue<double>(), 0);
        Assert.Equal(1000 * f >= 400, z["ok"]!.GetValue<bool>());
        Assert.True(z["massenstrom_kgh"]!.GetValue<double>() is > 30 and < 40);   // 400 W / (≈4.18 kJ/(kg·K) · 10 K) ≈ 34 kg/h
        Assert.Null(HeizkoerperAuslegung.DeltaTLog(35, 30, 32));   // RL unter θi: keine Übertemperatur
    }
}
