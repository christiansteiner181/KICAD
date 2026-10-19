using System.Text.Json;
using Hlks.Kern;
using static Hlks.Mcp.Tests.Hilfe;

namespace Hlks.Mcp.Tests;

/// <summary>Modul 4 Teil A-K: die Sanitär-Abfragen als MCP-Werkzeuge (gleiche Antwort wie die Kern-Tür) und die Apparateart über element_aendern.
/// Beispielprojekt «MFH Musterweg» (fiktiv); Belastungswerte aus dem Katalog sanitaer_belastungswerte.json.</summary>
public class WerkzeugSanitaerTests
{
    static JsonElement Elem(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public void Zusammenstellung_WieDieKernTuer()
    {
        var p = Beispiel();
        var w = new Werkzeuge(p);
        var z = Json(w.SanitaerZusammenstellungLesen("A-SAN"));
        Assert.Equal(16, z["apparate"]!.GetValue<int>());
        Assert.Equal(24, z["summeLuKw"]!.GetValue<double>());
        Assert.Equal(3, z["massgebendeKurve"]!.GetValue<int>());
        Assert.Equal(Json(Kern.Json.Text(Tuer.Abfrage(p, "sanitaer_zusammenstellung", new() { ["anlage"] = "A-SAN" }))).ToJsonString(), z.ToJsonString());
        Assert.Contains("Zeile", z["zeilen"]![0]!["quelle"]!.GetValue<string>());
    }

    [Fact]
    public void Belastung_Leitung_UndFehlerAlsJson()
    {
        var w = new Werkzeuge(Beispiel());
        var l = Json(w.SanitaerBelastungLesen(leitung: "SW-G1-1"));
        Assert.Equal(16.7, l["summeDu"]!.GetValue<double>(), 9);
        Assert.Equal(16, l["beitraege"]!.AsArray().Count);
        var st = Json(w.SanitaerBelastungLesen(strang: "KW-S2"));
        Assert.Equal(8, st["summeLuKw"]!.GetValue<double>());
        var f = Json(w.SanitaerBelastungLesen(leitung: "TS-G1"));
        Assert.Contains("keine Leitung", f["fehler"]!.GetValue<string>());
        Assert.NotNull(f["vorschlag"]);
        var k = Json(w.SanitaerKatalogLesen());
        Assert.Equal(34, k["arten"]!.AsArray().Count);
    }

    [Fact]
    public void ApparateartUeberElementAendern_TrockenlaufZeigtBefund()
    {
        var p = Beispiel();
        var w = new Werkzeuge(p);
        var t = Json(w.ElementAendern("BA-U.02", eigenschaften: Elem("""{ "sanitaerArt": null }"""), trockenlauf: true));
        Assert.True(t["angenommen"]!.GetValue<bool>(), t.ToJsonString());
        Assert.Contains(t["neueBefunde"]!.AsArray(), b => b!["code"]!.GetValue<string>() == "P21");
        Assert.Equal("bodenablauf_70", p.Modell.Hole("BA-U.02").Text("sanitaerArt"));   // Trockenlauf ändert nichts
        var e = Json(w.ElementAendern("BA-U.02", eigenschaften: Elem("""{ "sanitaerArt": "bodenablauf_100" }""")));
        Assert.True(e["angenommen"]!.GetValue<bool>(), e.ToJsonString());
        var l = Json(w.SanitaerBelastungLesen(leitung: "SW-U.02-BA"));
        Assert.Equal(2, l["summeDu"]!.GetValue<double>());
        var n = Json(w.ElementAendern("BA-U.02", eigenschaften: Elem("""{ "sanitaerArt": "bodenablauf_99" }""")));
        Assert.False(n["angenommen"]!.GetValue<bool>());
        Assert.Contains("bodenablauf_50", n["vorschlag"]!.GetValue<string>());
    }
}
