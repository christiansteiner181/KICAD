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
        // ΔTn logarithmisch (N24): (75 − 65) / ln(55/45) = 49.83 K; bei Normbedingung wäre f = 1.000
        double dtN = (75 - 65) / Math.Log(55.0 / 45), dt = (55 - 45) / Math.Log((55.0 - 22) / (45 - 22)), f = Math.Pow(dt / dtN, 1.3);
        Assert.Equal(49.83, z["deltaTn_K"]!.GetValue<double>(), 2);
        Assert.Equal(Math.Round(1000 * f), z["leistungBetrieb_W"]!.GetValue<double>(), 0);
        Assert.Equal(Math.Round(400 / f), z["normleistungErforderlich_W"]!.GetValue<double>(), 0);
        Assert.Equal(1000 * f >= 400, z["ok"]!.GetValue<bool>());
        Assert.True(z["massenstrom_kgh"]!.GetValue<double>() is > 30 and < 40);   // 400 W / (≈4.18 kJ/(kg·K) · 10 K) ≈ 34 kg/h
        Assert.Null(HeizkoerperAuslegung.DeltaTLog(35, 30, 32));   // RL unter θi: keine Übertemperatur
        // Herstellerangabe ohne Übertemperatur (ΔTn = 70 − 70 = 0): keine Umrechnung statt Division durch 0 (unendlich in der Antwort)
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "HK-E.02", ["eigenschaften"] = new JsonObject { ["normRaum"] = new JsonObject { ["wert"] = 70, ["quelle"] = "fiktiv" } } }));
        var r0 = (JsonObject)Tuer.Abfrage(p, "heizkoerper_auslegung", new JsonObject { ["raum"] = "E.02" });
        r0.ToJsonString();
        var z0 = r0["heizkoerper"]!.AsArray().OfType<JsonObject>().Single();
        Assert.Null(z0["leistungBetrieb_W"]);
        Assert.Contains(z0["annahmen"]!.AsArray(), x => x!.ToString().Contains("ohne Übertemperatur"));
    }
}

/// <summary>Modul 5 Teil B: Rohrnetz-Druckverlust – Wasserwerte (NIST) und Verhalten ohne Innendurchmesser.</summary>
public class RohrDruckverlustTests
{
    [Fact]
    public void Wasser_linear_zwischen_NIST_Werten_und_ohne_Serie_kein_Druckverlust()
    {
        var kat = KanalDruckverlust.Katalog(Kat)!;
        var w = RohrDruckverlust.Wasser(kat, 15)!.Value;
        Assert.Equal((0.0013058 + 0.0010016) / 2, w.Eta, 9);   // 10 und 20 °C, NIST 0.2 MPa
        Assert.Equal((999.75 + 998.25) / 2, w.Rho, 6);
        Assert.Null(RohrDruckverlust.Wasser(kat, 95));          // ausserhalb der Tabelle: kein Wert
        var p = Beispielprojekt.Erzeugen(Kat, null);             // Heizungsleitungen ohne Serie
        var r = (JsonObject)Tuer.Abfrage(p, "rohr_druckverlust", new JsonObject());
        var l = r["leitungen"]!.AsArray().OfType<JsonObject>().ToList();
        Assert.NotEmpty(l);
        Assert.All(l.Where(x => x["dp_Pa"] is null), x => Assert.NotNull(x["hinweis"]));
    }
}

/// <summary>Gesamtprüfung 13.11.2026, Fragen 20 und 21: Rohrnetz rechnet den Massenstrom aus dem Bedarf, Hauptleitungen mit der höchsten Gruppe.</summary>
public class HeizungBedarfTests
{
    [Fact]
    public void Netz_Massenstrom_aus_Raumbedarf_wie_Auslegung_ohne_Raumwert_Normleistung_mit_Hinweis()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "HK-E.02", ["eigenschaften"] = new JsonObject { ["leistung"] = new JsonObject { ["wert"] = 1000, ["quelle"] = "fiktiv" } } }));
        var hk = p.Modell.Finde("HK-E.02")!;
        var vorher = HeizungLeistung.Ergebnis(p.Modell, p.Katalog).Strom[hk.Id];
        Assert.Equal("normleistung", vorher.Basis);   // Raum ohne Heizleistung: Rückfall auf die Normleistung
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "E.02", ["eigenschaften"] = new JsonObject { ["heizleistung"] = new JsonObject { ["wert"] = 400, ["quelle"] = "fiktiv" } } }));
        var st = HeizungLeistung.Ergebnis(p.Modell, p.Katalog).Strom[hk.Id];
        var z = ((JsonObject)Tuer.Abfrage(p, "heizkoerper_auslegung", new JsonObject { ["raum"] = "E.02" }))["heizkoerper"]!.AsArray().OfType<JsonObject>().Single();
        // ein Massenstrom für denselben Heizkörper: Netz = Auslegung (400 W / (c_p · 10 K) ≈ 34 kg/h statt ≈ 86 kg/h aus 1000 W Norm)
        Assert.Equal("bedarf", st.Basis);
        Assert.Equal(z["massenstrom_kgh"]!.GetValue<double>(), st.Kgh!.Value, 1);
        Assert.Equal(vorher.Kgh!.Value * 0.4, st.Kgh!.Value, 1);
    }

    [Fact]
    public void Hauptleitung_ohne_Gruppe_rechnet_mit_hoechster_Vorlauftemperatur_plus_Hysterese()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        List<JsonObject> Zeilen() => ((JsonObject)Tuer.Abfrage(p, "rohr_druckverlust", new JsonObject()))["leitungen"]!.AsArray().OfType<JsonObject>()
            .Where(x => x["temperaturAus"] is not null).ToList();
        var haupt = Zeilen();
        Assert.NotEmpty(haupt);
        var vl0 = haupt.First(x => x["weg"]!.GetValue<string>() == "vl");
        double t0 = vl0["t"]!.GetValue<double>();
        var anlage = p.Modell.DerKlasse("Anlage").First(a => p.Modell.Elemente.Any(s => s.Klasse == "System" && s.TeilVon == a.Id && s.Kennzeichen.StartsWith("HZ")));
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = anlage.Kennzeichen, ["eigenschaften"] = new JsonObject { ["vlHysterese"] = new JsonObject { ["wert"] = 5, ["quelle"] = "fiktiv" } } }));
        var vl1 = Zeilen().First(x => x["leitung"]!.GetValue<string>() == vl0["leitung"]!.GetValue<string>());
        Assert.Equal(t0 + 5, vl1["t"]!.GetValue<double>(), 6);
        Assert.Contains("Hysterese 5 K", vl1["temperaturAus"]!.GetValue<string>());
    }
}
