using System.Text.Json.Nodes;

namespace Hlks.Kern.Tests;

/// <summary>Modul 5 Teil B-H, Norm-Heizlast je Raum: Rechenweg mit FIKTIVEN Normtabellen (die echten Werte liegen nie im Repository).
/// Raum 6.0 × 4.0 × 2.6 m mit vier Aussenwänden und einem Fenster wie in RaumgrenzenTests (Handrechnung der Flächen dort).</summary>
[Collection("Normtabellen")]
public class HeizlastTests
{
    // Fiktiv – absichtlich andere Zahlen als die Norm
    const string Heizlast = """
    { "einzelwerte": [ { "id": "ND.1.1.1_hoehe", "wert": -0.01 }, { "id": "ND.5.2_nmin", "wert": 0.5 }, { "id": "ND.5.1_nInfMech", "wert": 0.2 },
                       { "id": "ND.5.7_faktorNatuerlich", "wert": 0.7 }, { "id": "ND.5.7_faktorMechanisch", "wert": 1.0 } ],
      "tabellen": { "ND2": { "werte": [ { "gruppe": "Testgruppe", "name": "Testnutzung", "thetaInt_C": 17, "seite": 4 } ] },
                    "ND1": { "werte": [ { "tauVon_h": 200, "tauBis_h": null, "korrektur_K": 0 }, { "tauVon_h": 0, "tauBis_h": 200, "korrektur_K": -4 } ] },
                    "ND5": { "werte": [ { "zBis_m": 2, "fg1": 1.1 }, { "zBis_m": null, "fg1": 1.0 } ] },
                    "ND8": { "werte": [ { "klasse": "Moderate Abschirmung", "e_ohneAussenwand": 0, "e_eineAusrichtung": 0.1, "e_mehrereAusrichtungen": 0.2 } ] },
                    "ND9": { "werte": [ { "hoeheBis_m": 10, "epsilon": 1.0 }, { "hoeheBis_m": null, "epsilon": 2.0 } ] } },
      "uequiv": { "tabelle4_z0m": { "tabelle": "Tabelle 4 fiktiv", "tiefeZ_m": 0, "werte": [
          { "bPrime_m": 2, "keineDaemmung": 1.0, "uBoden_2_0": 0.8, "uBoden_1_0": 0.6, "uBoden_0_5": 0.4, "uBoden_0_25": 0.2 },
          { "bPrime_m": 20, "keineDaemmung": 0.5, "uBoden_2_0": 0.4, "uBoden_1_0": 0.3, "uBoden_0_5": 0.2, "uBoden_0_25": 0.1 } ] } } }
    """;
    const string Klima = """
    { "tabelle6": { "werte": [ { "station": "Teststadt", "thetaE": -8, "hoehe": 500, "seite": 1 } ] },
      "tabelle2": { "werte": [ { "station": "Teststadt", "jahr": 9, "seite": 2 } ] } }
    """;
    const string Nutzung = """{ "tabelle": { "werte": [ { "nr": "9.9", "nutzung": "Testnutzung", "raumtempWinter_C": 19, "seite": 3 } ] } }""";

    static JsonObject Mit(Func<JsonObject> f, bool tabellen = true)
    {
        var ordner = Path.Combine(Path.GetTempPath(), "hlks-normtabellen-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(ordner, "heizung"));
        if (tabellen)
        {
            File.WriteAllText(Path.Combine(ordner, "heizung", "heizlast.json"), Heizlast);
            File.WriteAllText(Path.Combine(ordner, "heizung", "sia2028_auslegung.json"), Klima);
            File.WriteAllText(Path.Combine(ordner, "heizung", "raumnutzung_sia2024.json"), Nutzung);
        }
        var alt = Environment.GetEnvironmentVariable(SanitaerNormtabellen.Umgebungsvariable);
        Environment.SetEnvironmentVariable(SanitaerNormtabellen.Umgebungsvariable, ordner);
        try { return f(); }
        finally { Environment.SetEnvironmentVariable(SanitaerNormtabellen.Umgebungsvariable, alt); Directory.Delete(ordner, true); }
    }

    /// <summary>EG mit einem Raum 6.0 × 4.0 m, lichte Höhe 2.6 m; Aussenwände U 0.25, Fenster 1.2 × 1.2 m Uw 1.0 (Testwerte).</summary>
    static Projekt Raum(out string raum, JsonObject raumEig)
    {
        var p = RaumgrenzenHilfe.Leer();
        RaumgrenzenHilfe.Geschoss(p, "EG", 0, 3000);
        RaumgrenzenHilfe.Waende(p, "EG", -100, -100, 6100, 4100, 200);
        raum = RaumgrenzenHilfe.Raum(p, "EG", 3000, 2000, 2600, "0.01");
        RaumgrenzenHilfe.Fenster(p, "W-1", 3100, 1200, 1200, 900);
        RaumgrenzenHilfe.Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Aufbau", ["kennzeichen"] = "AUF-W",
            ["eigenschaften"] = new JsonObject { ["art"] = "wand", ["uWert"] = new JsonObject { ["wert"] = 0.25, ["quelle"] = "Testwert" } },
        }));
        foreach (var w in new[] { "W-1", "W-2", "W-3", "W-4" })
            RaumgrenzenHilfe.Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = w, ["bezuege"] = new JsonObject { ["aufbau"] = "AUF-W" } }));
        RaumgrenzenHilfe.Eigenschaften(p, "F-1", new JsonObject { ["uw"] = new JsonObject { ["wert"] = 1.0, ["quelle"] = "Testwert" } });
        RaumgrenzenHilfe.Eigenschaften(p, raum, raumEig);
        var geb = p.Modell.DerKlasse("Gebaeude").First().Kennzeichen;
        RaumgrenzenHilfe.Eigenschaften(p, geb, new JsonObject { ["klimastation"] = "Teststadt", ["hoeheUeberMeer"] = 700 });
        return p;
    }

    static double Z(JsonNode? n) => n!.GetValue<double>();

    [Fact]
    public void Heizlast_Raum_Aussentemperatur_Transmission_und_Mindestluftwechsel_wie_von_Hand()
    {
        var p = Raum(out var raum, new JsonObject { ["tempWinter"] = new JsonObject { ["wert"] = 20, ["quelle"] = "Testwert" } });
        var r = Mit(() => SanitaerHilfe.Abfrage(p, "heizlast"));
        // θe = −8 (Station) + round(−0.01 · (700 − 500)) = −10
        Assert.Equal(-10, Z(r["aussen"]!["thetaE"]));
        var z = r["raeume"]!.AsArray().OfType<JsonObject>().Single(x => (string?)x["raum"] == raum);
        // HT,ie = Wände (14.16 + 10.4 + 15.6 + 10.4) · 0.25 + Fenster 1.44 · 1.0; Boden und Decke ohne U
        double hie = (14.16 + 10.4 + 15.6 + 10.4) * 0.25 + 1.44 * 1.0;
        Assert.Equal(hie, Z(z["hTie_WK"]), 2);
        Assert.Equal(Math.Round(hie * 30), Z(z["phiT"]), 0);
        // ohne Anlage, n50 fehlt (0): V̇ = V̇min = 0.5 · 62.4 m³/h; ρc nach (37) mit h = 700 m
        double v = 0.5 * 6 * 4 * 2.6, rhoC = (1220 - 0.14 * 700) / 3600;
        Assert.Equal(v, Z(z["vPunkt_m3h"]), 2);
        Assert.Equal(Math.Round(v * rhoC * 30), Z(z["phiV"]), 0);
        Assert.Equal(Math.Round(hie * 30 + v * rhoC * 30), Z(z["phiHL"]), 0);
        // Gebäude: Σ ΦT + f · Σ ΦV mit f (natürlich) aus der Tabelle
        Assert.Equal(Math.Round(hie * 30 + 0.7 * v * rhoC * 30), Z(r["gebaeude"]!["phiHL"]), 0);
    }

    [Fact]
    public void Heizlast_Erdreich_mit_B_Strich_fg1_fg2_und_Lueftungsanlage_mindestens_Vmin_und_Tau_Grenze()
    {
        var p = Raum(out var raum, new JsonObject { ["tempWinter"] = new JsonObject { ["wert"] = 20, ["quelle"] = "Testwert" }, ["zuluft"] = 30 });
        // Bodenplatte OK 0 auf Terrain ±0 → Boden gegen Erdreich, U 0.5 (Testwert)
        RaumgrenzenHilfe.Platte(p, "Decke", "D-0", "EG", -200, -200, 6200, 4200, 0, 300, "boden");
        RaumgrenzenHilfe.Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Aufbau", ["kennzeichen"] = "AUF-B",
            ["eigenschaften"] = new JsonObject { ["art"] = "boden", ["uWert"] = new JsonObject { ["wert"] = 0.5, ["quelle"] = "Testwert" } },
        }));
        RaumgrenzenHilfe.Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "D-0", ["bezuege"] = new JsonObject { ["aufbau"] = "AUF-B" } }));
        var geb = p.Modell.DerKlasse("Gebaeude").First().Kennzeichen;
        RaumgrenzenHilfe.Eigenschaften(p, geb, new JsonObject { ["zeitkonstante"] = new JsonObject { ["wert"] = 200, ["quelle"] = "Testwert" } });
        var r = Mit(() => SanitaerHilfe.Abfrage(p, "heizlast", new JsonObject { ["raum"] = raum }));
        // τ = 200 h gehört zur Zeile «0 < τ ≤ 200» (−4 K, fiktiv): θe = −8 − 2 − 4
        Assert.Equal(-14, Z(r["aussen"]!["thetaE"]));
        var z = r["raeume"]!.AsArray().OfType<JsonObject>().Single();
        // B' = 24 m² / (0.5 · 20 m Wandumfang, Öffnungen nicht doppelt) = 2.4; Uequiv (Spalte U 0.5) linear zwischen B' 2 und 20
        double ue = 0.4 + (0.2 - 0.4) * (2.4 - 2) / 18;
        double fg2 = (20 - 9.0) / (20 + 14);
        Assert.Equal(1.1 * fg2 * 24 * ue, Z(z["hTig_WK"]), 3);
        // Lüftungsanlage ohne Zulufttemperatur: wie ohne Anlage, V̇ = max(0.2·V, 0.5·V) = V̇min
        Assert.True(z["lueftungsanlage"]!.GetValue<bool>());
        Assert.Equal(0.5 * 6 * 4 * 2.6, Z(z["vPunkt_m3h"]), 2);
    }

    [Fact]
    public void Heizlast_Innentemperatur_aus_ND2_vor_SIA_2024_und_Zeitkonstante_und_ohne_Tabellen_Hinweis()
    {
        var p = Raum(out var raum, new JsonObject { ["nutzung"] = "Testnutzung" });
        var geb = p.Modell.DerKlasse("Gebaeude").First().Kennzeichen;
        RaumgrenzenHilfe.Eigenschaften(p, geb, new JsonObject { ["zeitkonstante"] = new JsonObject { ["wert"] = 80, ["quelle"] = "Testwert" } });
        var r = Mit(() => SanitaerHilfe.Abfrage(p, "heizlast", new JsonObject { ["raum"] = raum }));
        Assert.Equal(-14, Z(r["aussen"]!["thetaE"]));   // −8 − 2 (Höhe) − 4 (τ ≤ 200 h)
        var z = r["raeume"]!.AsArray().OfType<JsonObject>().Single();
        // Auslegungsnorm vor SIA 2024 (Christian 04.11.2026: «2024 ist eine norm zur projektierung. 384.201, 384/2 ist für die auslegung.»)
        Assert.Equal(17, Z(z["thetaInt"]));
        Assert.Contains("ND.2", (string?)z["thetaIntQuelle"]);
        var ohne = Mit(() => SanitaerHilfe.Abfrage(p, "heizlast"), tabellen: false);
        Assert.Contains(ohne["hinweise"]!.AsArray(), h => h!.GetValue<string>().Contains("Normtabellen Heizlast fehlen"));
    }
}
