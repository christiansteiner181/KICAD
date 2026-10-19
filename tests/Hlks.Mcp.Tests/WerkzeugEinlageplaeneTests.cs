using System.Text.Json.Nodes;
using Hlks.Kern;
using static Hlks.Mcp.Tests.Hilfe;

namespace Hlks.Mcp.Tests;

/// <summary>Modul 3, B10: MCP-Werkzeug einlageplaene_anlegen (Trockenlauf, Anlegen, keine Doppel). Testhaus FIKTIV wie WerkzeugVerlegungTests:
/// Geschosse UG (−3000) und EG (0), Decke über UG D-EG 10000 × 8000, Oberkante 0, Dicke 300, «Beton 300»; Rohr R-1 waagrecht in der Decke
/// (z −150, System KW → Gewerk Sanitär).</summary>
public class WerkzeugEinlageplaeneTests
{
    static Werkzeuge Haus()
    {
        var p = Projekt.Anlegen(Kat, null, "B10 MCP (fiktiv)", "T", "pc");
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
        return new Werkzeuge(p);
    }

    [Fact]
    public void Einlageplaene_TrockenlaufAnlegenKeineDoppel()
    {
        var w = Haus();
        var t = Json(w.EinlageplaeneAnlegenWerkzeug(trockenlauf: true));
        Assert.True(t["angenommen"]!.GetValue<bool>(), t.ToJsonString());
        Assert.True(t["trockenlauf"]!.GetValue<bool>());
        Assert.Contains(t["hinweise"]!.AsArray(), h => h!.GetValue<string>().StartsWith("Einlagen D.ü.UG: Ansicht EL-D.ü.UG angelegt, Planblatt"));
        Assert.NotNull(Json(w.ElementLesen("EL-D.ü.UG"))["fehler"]);   // Trockenlauf: nichts angelegt

        var e = Json(w.EinlageplaeneAnlegenWerkzeug(massstab: 50, format: "A1"));
        Assert.True(e["angenommen"]!.GetValue<bool>(), e.ToJsonString());
        var a = Json(w.ElementLesen("EL-D.ü.UG"));
        Assert.Equal("Sanitär Einlagen D.ü.UG", a["name"]!.GetValue<string>());
        Assert.Equal("einlagen", a["eigenschaften"]!["darstellung"]!["wert"]!.GetValue<string>());

        var zwei = Json(w.EinlageplaeneAnlegenWerkzeug());
        Assert.True(zwei["angenommen"]!.GetValue<bool>());
        Assert.Contains(zwei["hinweise"]!.AsArray(), h => h!.GetValue<string>().Contains("steht schon auf"));
        // ungültiges Format: abgelehnt mit Vorschlag
        var f = Json(w.EinlageplaeneAnlegenWerkzeug(format: "B5"));
        Assert.False(f["angenommen"]!.GetValue<bool>());
        Assert.Contains("A1", f["vorschlag"]!.GetValue<string>());
    }
}
