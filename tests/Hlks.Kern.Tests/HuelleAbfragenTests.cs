using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.HuelleTestHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Abfragen huelle_pruefen, materialien, aufbau_rechnen und ihre Eintragung in Befehle und Abfragen (Modul 1c, B2).
/// Alle Zahlen sind frei gewählte Testwerte.</summary>
public class HuelleAbfragenTests
{
    static JsonObject Pruefen(Projekt p) => (JsonObject)Tuer.Abfrage(p, "huelle_pruefen");

    [Fact]
    public void HuellePruefen_ListetWasFehlt()
    {
        var p = Rechteck();                                  // W-1 … W-4 ohne Aufbau
        Ok(Fenster(p, "W-1"));                               // F-1
        Ok(Tuer(p, "W-2"));                                  // T-1
        Ok(Material(p, "Ohne Lambda", null));                // MAT-1
        Ok(Material(p, "Testmaterial A", 0.5));              // MAT-2
        Ok(Aufbau(p, "Testaufbau A", "wand", Schichten(("MAT-2", 200))));    // AUF-1: R da, Rsi/Rse fehlen
        Ok(Aufbau(p, "Unfertig", "decke", Schichten(("MAT-1", 100))));        // AUF-2: λ fehlt
        Ok(p.Ausfuehren("aufbau_zuweisen", new JsonObject { ["elemente"] = new JsonArray("W-1"), ["aufbau"] = "AUF-1" }));

        var o = Pruefen(p);
        Assert.Equal(4, o["bauteileOhneAufbau"]!["anzahl"]!.GetValue<int>());            // W-2, W-3, W-4 und die Tür T-1
        Assert.Equal(3, o["bauteileOhneAufbau"]!["nachKlasse"]!["Wand"]!.GetValue<int>());
        Assert.Equal(1, o["bauteileOhneAufbau"]!["nachKlasse"]!["Tuer"]!.GetValue<int>());
        Assert.Contains("T-1", o["bauteileOhneAufbau"]!["liste"]!.ToJsonString());
        Assert.Equal(2, o["aufbautenOhneU"]!["anzahl"]!.GetValue<int>());
        Assert.Contains("Rsi/Rse", o["aufbautenOhneU"]!["liste"]![0]!["fehlt"]!.ToJsonString());
        Assert.Contains("λ", Json.Text(o["aufbautenOhneU"]!["liste"]![1]!["fehlt"]!));
        Assert.Equal(2, o["uebergangFehlt"]!["anzahl"]!.GetValue<int>());               // horizontal und aufwaerts
        Assert.Equal(1, o["fensterOhneKennwerte"]!["anzahl"]!.GetValue<int>());
        Assert.Equal(1, o["tuerenOhneKennwerte"]!["anzahl"]!.GetValue<int>());
        Assert.Equal("MAT-1", o["materialienOhneLambda"]!["liste"]![0]!["material"]!.GetValue<string>());
        Assert.Contains("Offen", o["zusammenfassung"]!.GetValue<string>());
    }

    [Fact]
    public void HuellePruefen_VollstaendigeHuelle()
    {
        var p = Rechteck();
        Ok(Fenster(p, "W-1", 3000, 1000, 1000));
        Ok(Tuer(p, "W-2"));
        Ok(Material(p, "Testmaterial A", 0.5));
        Ok(Uebergang(p, "horizontal", 0.1, 0.05));
        Ok(Aufbau(p, "Wand", "wand", Schichten(("MAT-1", 200))));
        Ok(Aufbau(p, "Tür", "tuer", Schichten(("MAT-1", 40))));
        Ok(p.Ausfuehren("aufbau_zuweisen", new JsonObject { ["elemente"] = new JsonArray("W-1", "W-2", "W-3", "W-4"), ["aufbau"] = "AUF-1" }));
        Ok(p.Ausfuehren("aufbau_zuweisen", new JsonObject { ["elemente"] = new JsonArray("T-1"), ["aufbau"] = "AUF-2" }));
        Ok(p.Ausfuehren("fenster_kennwerte", new JsonObject
        {
            ["elemente"] = new JsonArray("F-1"), ["ug"] = 1.0, ["uf"] = 2.0, ["psiG"] = 0.1, ["rahmenanteil"] = 0.25, ["g"] = 0.5, ["quelle"] = TestQuelle,
        }));
        var o = Pruefen(p);
        Assert.Equal(0, o["bauteileOhneAufbau"]!["anzahl"]!.GetValue<int>());
        Assert.Equal(0, o["aufbautenOhneU"]!["anzahl"]!.GetValue<int>());
        Assert.Equal(0, o["uebergangFehlt"]!["anzahl"]!.GetValue<int>());
        Assert.Equal(0, o["fensterOhneKennwerte"]!["anzahl"]!.GetValue<int>());
        Assert.Equal(0, o["tuerenOhneKennwerte"]!["anzahl"]!.GetValue<int>());
        Assert.Equal(0, o["materialienOhneLambda"]!["anzahl"]!.GetValue<int>());
        Assert.Contains("vollständig", o["zusammenfassung"]!.GetValue<string>());
    }

    [Fact]
    public void HuellePruefen_TuerMitUdZaehltAlsVersorgt_FensterOhneGBleibtOffen()
    {
        var p = Rechteck();
        Ok(Fenster(p, "W-1", 3000, 1000, 1000));
        Ok(Tuer(p, "W-2"));
        Ok(p.Ausfuehren("fenster_kennwerte", new JsonObject { ["elemente"] = new JsonArray("T-1"), ["ud"] = 1.3, ["quelle"] = TestQuelle }));
        Ok(p.Ausfuehren("fenster_kennwerte", new JsonObject
        {
            ["elemente"] = new JsonArray("F-1"), ["uw"] = 1.4, ["quelle"] = TestQuelle,                 // Uw ausdrücklich, aber g fehlt
        }));
        var o = Pruefen(p);
        Assert.Equal(0, o["tuerenOhneKennwerte"]!["anzahl"]!.GetValue<int>());
        Assert.Equal(1, o["fensterOhneKennwerte"]!["anzahl"]!.GetValue<int>());
        Assert.Contains("g fehlt", o["fensterOhneKennwerte"]!["liste"]![0]!["fehlt"]!.ToJsonString());
        Assert.Equal(1.4, o["fensterOhneKennwerte"]!["liste"]![0]!["uw"]!.GetValue<double>());
    }

    [Fact]
    public void HuellePruefen_AufbauMitFalscherArtAusDemIfc()
    {
        // am Befehl vorbei zugewiesen (z. B. aus einem Import): die Abfrage meldet es
        var p = MitAufbau();
        Ok(Decke(p));
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "D-1", ["bezuege"] = new JsonObject { ["aufbau"] = "AUF-1" } }));   // Decke mit Wandaufbau
        var o = Pruefen(p);
        Assert.Equal(1, o["aufbauUnpassendeArt"]!["anzahl"]!.GetValue<int>());
        Assert.Equal("D-1", o["aufbauUnpassendeArt"]!["liste"]![0]!["bauteil"]!.GetValue<string>());
    }

    [Fact]
    public void Eintragung_BefehleUndAbfragenSindImKatalogDerFaehigkeiten()
    {
        var p = Neu();
        foreach (var n in new[] { "material_anlegen", "material_aendern", "material_uebernehmen", "aufbau_anlegen", "aufbau_aendern", "aufbau_zuweisen", "fenster_kennwerte", "uebergang_setzen" })
            Assert.Contains(p.Befehle.Alle, b => b.Name == n);
        foreach (var n in new[] { "aufbau_rechnen", "materialien", "huelle_pruefen" })
            Assert.Contains(Abfragen.Liste, a => a.Name == n);
        Assert.Contains("HuelleNachfuehren", string.Join(",", Nachfuehrungen.Liste.Select(x => x.GetType().Name)));
        var f = new Abfragen(p).Faehigkeiten().ToJsonString();
        Assert.Contains("uebergang_setzen", f);
        Assert.Contains("huelle_pruefen", f);
        // unbekannte Abfrage nennt die Liste, darunter die neuen
        var e = Assert.Throws<HlksFehler>(() => Tuer.Abfrage(p, "gibtsnicht"));
        Assert.Contains("aufbau_rechnen", e.Vorschlag);
        // fehlender Aufbau: Fehler mit Vorschlag
        var fehler = Assert.Throws<HlksFehler>(() => Tuer.Abfrage(p, "aufbau_rechnen", new JsonObject { ["aufbau"] = "AUF-9" }));
        Assert.False(string.IsNullOrWhiteSpace(fehler.Vorschlag));
    }

    [Fact]
    public void ParameterDerBefehleSindBeschrieben()
    {
        var p = Neu();
        foreach (var n in new[] { "material_anlegen", "material_aendern", "material_uebernehmen", "aufbau_anlegen", "aufbau_aendern", "aufbau_zuweisen", "fenster_kennwerte", "uebergang_setzen" })
        {
            var b = p.Befehle.Hole(n);
            Assert.False(string.IsNullOrWhiteSpace(b.Beschreibung), n);
            Assert.All(b.Parameter, d => Assert.False(string.IsNullOrWhiteSpace(d.Beschreibung), $"{n}.{d.Name}"));
        }
    }

    [Fact]
    public void KeineKennwerteImBeispielprojekt()
    {
        // Grundsatz: keine Kennwerte aus dem Gedächtnis – das Beispielprojekt enthält weder Materialien noch Aufbauten
        var p = Beispielprojekt.Erzeugen(Kat, null);
        Assert.Empty(p.Modell.DerKlasse("Material"));
        Assert.Empty(p.Modell.DerKlasse("Aufbau"));
        Assert.DoesNotContain(p.Modell.Elemente, e => e.Eigenschaften.ContainsKey("lambda") || e.Eigenschaften.ContainsKey("uWert") || e.Eigenschaften.ContainsKey("ug"));
    }
}
