using System.Text.Json;
using Hlks.Kern;
using static Hlks.Mcp.Tests.Hilfe;

namespace Hlks.Mcp.Tests;

/// <summary>Modul 3, B4: MCP-Werkzeug materialliste – Beschreibung und Parameter wie im Kern, Antwort mit Leitungen je Serie und Grösse,
/// Fehler mit Vorschlag bei unbekanntem Filter. Mit der FIKTIVEN Testrohrserie (tests/daten/familien/test_rohrserie).</summary>
public class WerkzeugMengenTests
{
    static JsonElement Elem(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public void Materialliste_LeitungenJeSerieUndGroesse_UndFilter()
    {
        var w = new Werkzeuge(Beispiel());
        var l = Json(w.FamilieLadenWerkzeug(Testdaten(Path.Combine("familien", "test_rohrserie.familie.json"))));
        Assert.True(l["angenommen"]!.GetValue<bool>(), l.ToJsonString());
        var r = Json(w.ElementAnlegen("Rohr", "RO-T1", system: "KW", geschoss: "EG", eigenschaften: Elem("""{ "familientyp": "DN 20" }"""),
            bezuege: Elem("""{ "familie": "FAM-1" }"""), geometrie: Elem("""{ "art": "polylinie", "punkte": [[0, 0, 500], [3000, 0, 500]] }""")));
        Assert.True(r["angenommen"]!.GetValue<bool>(), r.ToJsonString());

        var o = Json(w.MateriallisteLesen(system: "KW"));
        Assert.Equal("Materialliste", o["ableitung"]!.GetValue<string>());
        var z = o["leitungen"]!.AsArray().Single(x => x!["serie"]!.GetValue<string>() == "Testrohrserie (fiktiv)")!;
        Assert.Equal("NW 20", z["groesse"]!.GetValue<string>());   // B9b (Entscheid Christian 19.10.2026): Rohre ausser Abwasser mit NW (Vorgabe) statt Typname
        Assert.Equal(3.0, z["laenge_m"]!.GetValue<double>());
        Assert.Contains(o["leitungen"]!.AsArray(), x => x!["serie"]!.GetValue<string>() == "ohne Serie");   // Altbestand des Beispielprojekts
        Assert.Equal("KW", o["filter"]!["system"]!.GetValue<string>());

        // Filter wirkt: SW hat die Leitung nicht
        var sw = Json(w.MateriallisteLesen(system: "SW"));
        Assert.DoesNotContain(sw["leitungen"]!.AsArray(), x => x!["serie"]!.GetValue<string>() == "Testrohrserie (fiktiv)");
        Assert.True(Json(w.MateriallisteLesen())["leitungslaengeTotal_m"]!.GetValue<double>() > 0);
    }

    [Fact]
    public void Materialliste_UnbekannterFilter_FehlerMitVorschlag()
    {
        var w = new Werkzeuge(Beispiel());
        foreach (var antwort in new[] { w.MateriallisteLesen(system: "GIBT-ES-NICHT"), w.MateriallisteLesen(strang: "ST-99"), w.MateriallisteLesen(geschoss: "99.OG") })
        {
            var o = Json(antwort);
            Assert.NotNull(o["fehler"]);
            Assert.False(string.IsNullOrWhiteSpace(o["vorschlag"]?.GetValue<string>()), antwort);
        }
    }
}
