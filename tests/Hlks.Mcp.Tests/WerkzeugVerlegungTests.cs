using System.Text.Json;
using System.Text.Json.Nodes;
using Hlks.Kern;
using static Hlks.Mcp.Tests.Hilfe;

namespace Hlks.Mcp.Tests;

/// <summary>Modul 3, Nachtrag B8c: MCP-Werkzeug verlegung und das Setzen von Hand über element_aendern (mit Quelle → Herkunft). Testhaus
/// FIKTIV: Geschosse UG (−3000) und EG (0), Decke über UG D-EG 10000 × 8000, Oberkante 0, Dicke 300, «Beton 300»; Rohr R-1 waagrecht
/// in der Decke (z −150), Rohr R-2 im UG (z −2500).</summary>
public class WerkzeugVerlegungTests
{
    static JsonElement Elem(string json) => JsonDocument.Parse(json).RootElement.Clone();

    static Werkzeuge Haus()
    {
        var p = Projekt.Anlegen(Kat, null, "B8c MCP (fiktiv)", "T", "pc");
        void A(string b, string json)
        {
            var e = p.Ausfuehren(b, (JsonObject)JsonNode.Parse(json)!);
            Assert.True(e.Angenommen, $"{b}: {e.Fehler}");
        }
        A("element_anlegen", """{ "klasse": "Gebaeude", "kennzeichen": "GEB" }""");
        A("element_anlegen", """{ "klasse": "Geschoss", "kennzeichen": "UG", "teilVon": "GEB", "eigenschaften": { "kote": -3000, "hoehe": 3000 } }""");
        A("element_anlegen", """{ "klasse": "Geschoss", "kennzeichen": "EG", "teilVon": "GEB", "eigenschaften": { "kote": 0, "hoehe": 3000 } }""");
        A("element_anlegen", """{ "klasse": "System", "kennzeichen": "KW", "eigenschaften": { "systemtyp": "Trinkwasser kalt" } }""");
        A("element_anlegen", """{ "klasse": "Decke", "kennzeichen": "D-EG", "geschoss": "EG", "eigenschaften": { "aufbau": "Beton 300" }, "geometrie": { "art": "polygon", "punkte": [[0,0,0],[10000,0,0],[10000,8000,0],[0,8000,0]], "dicke": 300 } }""");
        A("element_anlegen", """{ "klasse": "Rohr", "kennzeichen": "R-1", "system": "KW", "geschoss": "UG", "eigenschaften": { "dn": 50 }, "geometrie": { "art": "polylinie", "punkte": [[1000,2000,-150],[5000,2000,-150]] } }""");
        A("element_anlegen", """{ "klasse": "Rohr", "kennzeichen": "R-2", "system": "KW", "geschoss": "UG", "eigenschaften": { "dn": 50 }, "geometrie": { "art": "polylinie", "punkte": [[1000,7000,-2500],[6000,7000,-2500]] } }""");
        return new Werkzeuge(p);
    }

    [Fact]
    public void Verlegung_AbfrageUndVonHandMitQuelle()
    {
        var w = Haus();
        var o = Json(w.VerlegungLesen());
        Assert.Equal(1, o["zaehlung"]!["einlage"]!.GetValue<int>());
        var plan = o["plaene"]!.AsArray().Single()!;
        Assert.Equal("Einlagen D.ü.UG", plan["plan"]!.GetValue<string>());
        Assert.Equal(("R-1", "auto"), (plan["elemente"]![0]!["kennzeichen"]!.GetValue<string>(), plan["elemente"]![0]!["herkunft"]!.GetValue<string>()));
        // von Hand über element_aendern, mit Quelle: gilt, Herkunft der Eigenschaft trägt die Quelle
        var e = Json(w.ElementAendern("R-2", eigenschaften: Elem("""{ "verlegung": { "wert": "einlage", "quelle": "Test B8c, fiktiv" } }""")));
        Assert.True(e["angenommen"]!.GetValue<bool>(), e.ToJsonString());
        var r2 = Json(w.ElementLesen("R-2"));
        Assert.Contains("Test B8c, fiktiv", r2.ToJsonString());
        var o2 = Json(w.VerlegungLesen(geschoss: "UG", art: "einlage"));
        var hand = o2["plaene"]!.AsArray().SelectMany(x => x!["elemente"]!.AsArray()).Single(x => x!["kennzeichen"]!.GetValue<string>() == "R-2")!;
        Assert.Equal(("hand", "frei"), (hand["herkunft"]!.GetValue<string>(), hand["erkannt"]!.GetValue<string>()));
        Assert.Equal(1, o2["zaehlung"]!["vonHand"]!.GetValue<int>());
        // auto: wieder erkannt
        Assert.True(Json(w.ElementAendern("R-2", eigenschaften: Elem("""{ "verlegung": "auto" }""")))["angenommen"]!.GetValue<bool>());
        Assert.Equal(0, Json(w.VerlegungLesen())["zaehlung"]!["vonHand"]!.GetValue<int>());
        // falsche Art: Fehler als JSON mit Vorschlag
        var f = Json(w.VerlegungLesen(art: "beton"));
        Assert.NotNull(f["fehler"]);
        Assert.Contains("einlage oder erdreich", f["vorschlag"]!.GetValue<string>());
    }
}
