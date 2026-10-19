using System.Text.Json;
using System.Text.Json.Nodes;
using Hlks.Kern;
using static Hlks.Mcp.Tests.Hilfe;

namespace Hlks.Mcp.Tests;

/// <summary>Modul 3b, B3: MCP-Werkzeuge der Aussparungen – vorschlagen (Trockenlauf mit Liste), Form von Hand (Nachtrag B3b:
/// aussparung_form mit Trockenlauf, Rückgängig und Ablehnung), Status, Nummern, Liste, Abgleich mit Fehler. FIKTIVE Vorgaben (Spiel 20,
/// Zusammenfassen 100 mm) und Testrohrserie (DN 50: da 55).</summary>
public class WerkzeugAussparungTests
{
    static JsonElement Elem(string json) => JsonDocument.Parse(json).RootElement.Clone();

    static JsonNode Ok(string antwort)
    {
        var j = Json(antwort);
        Assert.True(j["angenommen"]?.GetValue<bool>() == true, antwort);
        return j;
    }

    [Fact]
    public void Vorschlagen_Status_Nummerieren_Liste_UeberMcp()
    {
        var w = new Werkzeuge(Beispiel());
        var nein = Json(w.AussparungenVorschlagenWerkzeug());
        Assert.False(nein["angenommen"]!.GetValue<bool>());
        foreach (var n in AussparungVorgaben.Namen) Assert.Contains(n, nein["fehler"]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(nein["vorschlag"]?.GetValue<string>()));

        Assert.DoesNotContain("grenze_kernbohrung", nein["fehler"]!.GetValue<string>());   // B3b: nicht mehr verlangt
        Ok(w.ElementAnlegen("Vorgaben", "VG-1", eigenschaften: Elem("""
            { "spiel_aussparung": { "wert": 20, "quelle": "FIKTIV Test B3" },
              "abstand_zusammenfassen": { "wert": 100, "quelle": "FIKTIV Test B3" } }
            """)));
        Ok(w.FamilieLadenWerkzeug(Testdaten(Path.Combine("familien", "test_rohrserie.familie.json"))));
        var wand = Ok(w.WandZeichnen("EG", Elem("[[50000, 0], [56000, 0]]"), 200))["aenderungen"]![0]!["kennzeichen"]!.GetValue<string>();
        Ok(w.ElementAnlegen("Rohr", "RO-T1", system: "KW", geschoss: "EG", eigenschaften: Elem("""{ "familientyp": "DN 50" }"""),
            bezuege: Elem("""{ "familie": "FAM-1" }"""), geometrie: Elem("""{ "art": "polylinie", "punkte": [[53000, -500, 1000], [53000, 500, 1000]] }""")));

        var t = Ok(w.AussparungenVorschlagenWerkzeug(bauteile: Elem($"[\"{wand}\"]"), trockenlauf: true));
        Assert.True(t["trockenlauf"]!.GetValue<bool>());
        // DN 50 (da 55, FIKTIV) + 2 × Spiel 20 = 95 → auf ganze cm aufgerundet 100 (Entscheid Christian 19.10.2026, Nachtrag B4e)
        Assert.Contains(t["hinweise"]!.AsArray(), h => h!.GetValue<string>().Contains("neu: rechteckig 100 × 100 mm, aussparung, Tiefe 200 mm"));
        Assert.Empty(Json(w.AussparungslisteLesen())["zeilen"]!.AsArray());

        var e = Ok(w.AussparungenVorschlagenWerkzeug(bauteile: Elem($"[\"{wand}\"]")));
        var kz = e["aenderungen"]!.AsArray().Single(x => x!["klasse"]!.GetValue<string>() == "Aussparung")!["kennzeichen"]!.GetValue<string>();

        // B3b: Form von Hand – Trockenlauf ändert nichts, ausführen, rückgängig, wieder ausführen
        var ft = Ok(w.AussparungFormWerkzeug(Elem($"[\"{kz}\"]"), "rund", trockenlauf: true));
        Assert.True(ft["trockenlauf"]!.GetValue<bool>());
        Assert.Contains(ft["hinweise"]!.AsArray(), h => h!.GetValue<string>().Contains("rechteckig 100 × 100 mm → rund Ø 100 mm, Kernbohrung (von Hand gewählt)"));
        Assert.Equal("100 × 100", Json(w.AussparungslisteLesen())["zeilen"]![0]!["masse"]!.GetValue<string>());
        Ok(w.AussparungFormWerkzeug(Elem($"[\"{kz}\"]"), "rund"));
        var zr = Json(w.AussparungslisteLesen())["zeilen"]![0]!;
        Assert.Equal(("Ø 100", "kernbohrung", true), (zr["masse"]!.GetValue<string>(), zr["art"]!.GetValue<string>(), zr["formVonHand"]!.GetValue<bool>()));
        Ok(w.Rueckgaengig());
        Assert.Equal("100 × 100", Json(w.AussparungslisteLesen())["zeilen"]![0]!["masse"]!.GetValue<string>());
        var oval = Json(w.AussparungFormWerkzeug(Elem($"[\"{kz}\"]"), "oval"));
        Assert.False(oval["angenommen"]!.GetValue<bool>());
        Assert.Contains("rechteckig", oval["vorschlag"]!.GetValue<string>());
        Ok(w.AussparungFormWerkzeug(Elem($"[\"{kz}\"]"), "rund"));
        Ok(w.AussparungenNummerierenWerkzeug(geschoss: "EG", alle: true));
        Ok(w.AussparungStatusWerkzeug(Elem($"[\"{kz}\"]"), "freigegeben"));
        var falsch = Json(w.AussparungStatusWerkzeug(Elem($"[\"{kz}\"]"), "ausgefuehrt"));
        Assert.False(falsch["angenommen"]!.GetValue<bool>());
        Assert.Contains("freigegeben → versandt", falsch["vorschlag"]!.GetValue<string>());
        var gesperrt = Json(w.AussparungFormWerkzeug(Elem($"[\"{kz}\"]"), "rechteckig"));   // freigegeben: Form nicht mehr wählbar
        Assert.False(gesperrt["angenommen"]!.GetValue<bool>());
        Assert.Contains("aussparung_status", gesperrt["vorschlag"]!.GetValue<string>());

        var z = Assert.Single(Json(w.AussparungslisteLesen(geschoss: "EG"))["zeilen"]!.AsArray())!;
        Assert.Equal(kz, z["aussparung"]!.GetValue<string>());
        Assert.Equal("A-EG-001", z["nummer"]!.GetValue<string>());
        Assert.Equal(wand, z["bauteil"]!.GetValue<string>());
        Assert.Equal("Ø 100", z["masse"]!.GetValue<string>());
        Assert.Equal("freigegeben", z["status"]!.GetValue<string>());
        Assert.Equal("RO-T1", z["leitungen"]![0]!.GetValue<string>());

        // Abgleich ohne IFC-Unterlage: Fehler mit Vorschlag
        var ab = Json(w.AussparungenAbgleichenWerkzeug("UL-99"));
        Assert.False(ab["angenommen"]!.GetValue<bool>());
        Assert.False(string.IsNullOrWhiteSpace(ab["vorschlag"]?.GetValue<string>()));
    }
}
