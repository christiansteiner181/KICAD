using System.Text.Json;
using System.Text.Json.Nodes;
using Hlks.Kern;
using static Hlks.Mcp.Tests.Hilfe;

namespace Hlks.Mcp.Tests;

/// <summary>MCP-Werkzeuge der Gebäudehülle (Modul 1c, B2): Ablauf Material → Aufbau → Rsi/Rse → Zuweisen → Rechnen.
/// Alle Zahlen sind frei gewählte Testwerte (keine Norm).</summary>
public class WerkzeugHuelleTests
{
    static JsonElement Elem(string json) => JsonDocument.Parse(json).RootElement.Clone();

    static JsonNode Ok(string antwort)
    {
        var j = Json(antwort);
        Assert.True(j["angenommen"]?.GetValue<bool>() == true, antwort);
        return j;
    }

    [Fact]
    public void Ablauf_VonMaterialZumUWert()
    {
        var p = Beispiel();
        var w = new Werkzeuge(p);
        Ok(w.MaterialAnlegen("Testmaterial", werte: Elem("""{ "lambda": { "wert": 0.5, "quelle": "Testwert" } }""")));
        Ok(w.AufbauAnlegen("Testaufbau", "wand", Elem("""[{ "material": "MAT-1", "dicke": 200 }]""")));
        Ok(w.UebergangSetzen("Testwert", horizontal: Elem("""{ "rsi": 0.1, "rse": 0.05 }""")));

        var a = Json(w.AufbauRechnen("AUF-1"));
        Assert.Equal(0.4, a["r"]!.GetValue<double>(), 6);
        Assert.Equal(1 / 0.55, a["u"]!.GetValue<double>(), 5);
        Assert.Empty(a["fehlt"]!.AsArray());

        var wand = p.Modell.DerKlasse("Wand").OrderBy(x => x.Kennzeichen).First().Kennzeichen;
        Ok(w.AufbauZuweisen(Elem($"[\"{wand}\"]"), "AUF-1"));
        Assert.Equal(p.Modell.Hole("AUF-1").Id, p.Modell.Hole(wand).Bezuege["aufbau"]);

        // Fenster: Zahlenwerte nur mit Quelle
        Ok(w.OeffnungSetzen("Fenster", "W-EG-S", 1200, 1200, abstand: 3000, bruestung: 900));
        var fenster = p.Modell.DerKlasse("Fenster").OrderBy(x => x.Kennzeichen).First().Kennzeichen;
        var ohne = Json(w.FensterKennwerte(Elem($"[\"{fenster}\"]"), ug: 1.0));
        Assert.False(ohne["angenommen"]!.GetValue<bool>());
        Assert.Contains("Quelle", ohne["fehler"]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(ohne["vorschlag"]?.GetValue<string>()));
        Ok(w.FensterKennwerte(Elem($"[\"{fenster}\"]"), ug: 1.0, quelle: "Testwert"));
        Assert.Equal(1.0, p.Modell.Hole(fenster).Zahl("ug"));

        var pruefen = Json(w.HuellePruefen());
        Assert.NotNull(pruefen["bauteileOhneAufbau"]);
        Assert.Contains("Offen", pruefen["zusammenfassung"]!.GetValue<string>());
        Assert.Contains("MAT-1", w.Materialien());
    }

    [Fact]
    public void Material_OhneQuelleAbgelehnt_TrockenlaufUndRueckgaengig()
    {
        var p = Beispiel();
        var w = new Werkzeuge(p);
        var start = p.Modell.Pruefsumme();
        var nein = Json(w.MaterialAnlegen("Testmaterial", werte: Elem("""{ "lambda": { "wert": 0.5 } }""")));
        Assert.False(nein["angenommen"]!.GetValue<bool>());
        Assert.Contains("Quelle", nein["fehler"]!.GetValue<string>());
        Assert.Equal(start, p.Modell.Pruefsumme());

        var tl = Json(w.MaterialAnlegen("Testmaterial", werte: Elem("""{ "lambda": { "wert": 0.5, "quelle": "Testwert" } }"""), trockenlauf: true));
        Assert.True(tl["trockenlauf"]!.GetValue<bool>());
        Assert.Equal(start, p.Modell.Pruefsumme());

        Ok(w.MaterialAnlegen("Testmaterial", werte: Elem("""{ "lambda": { "wert": 0.5, "quelle": "Testwert" } }""")));
        Assert.NotEqual(start, p.Modell.Pruefsumme());
        Ok(w.Rueckgaengig());
        Assert.Null(p.Modell.Finde("MAT-1"));
    }

    [Fact]
    public void MaterialUebernehmen_LeereBibliothekGibtKlareMeldung()
    {
        var w = new Werkzeuge(Beispiel());
        var j = Json(w.MaterialUebernehmen("Irgendwas"));
        Assert.False(j["angenommen"]!.GetValue<bool>());
        Assert.Contains("keine Materialien", j["fehler"]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(j["vorschlag"]?.GetValue<string>()));
    }
}
