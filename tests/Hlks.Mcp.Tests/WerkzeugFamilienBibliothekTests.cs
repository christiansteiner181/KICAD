using System.Text.Json;
using System.Text.Json.Nodes;
using Hlks.Kern;
using static Hlks.Mcp.Tests.Hilfe;

namespace Hlks.Mcp.Tests;

/// <summary>Modul 2, Nachtrag B7: MCP-Werkzeuge der Familienbibliothek (familien_bibliothek mit Parametern, familie_laden mit id,
/// familie_in_bibliothek, familie_aktualisieren, familienauswahl_setzen). Ohne HLKS_FAMILIENBIBLIOTHEK ist die Bibliothek im Testlauf
/// gesperrt: Antworten statt Ausnahmen, klare Fehler mit Vorschlag, nie Schreiben auf W:. (Die Bibliothek selbst prüfen die
/// Kern-Tests FamilienBibliothek* mit einem Ordner unter %TEMP%.)</summary>
public class WerkzeugFamilienBibliothekTests
{
    static JsonElement Elem(string json) => JsonDocument.Parse(json).RootElement.Clone();

    static Werkzeuge MitFamilie()
    {
        var p = Projekt.Anlegen(Kat, null, "MCP B7", "Christian", "pc");
        var w = new Werkzeuge(p);
        var e = Json(w.FamilieEntwurfen(Elem(File.ReadAllText(Testdaten(Path.Combine("familien", "test_heizkoerper.familie.json"))))));
        Assert.True(e["angenommen"]!.GetValue<bool>(), e.ToJsonString());
        Assert.Contains(e["hinweise"]!.AsArray(), h => h!.GetValue<string>().StartsWith("Familienbibliothek: nicht geschrieben"));
        return w;
    }

    [Fact]
    public void Testlauf_ist_erkannt_und_an_Kindprozesse_weitergegeben()
    {
        Assert.True(FamilienBibliothekDienst.Testlauf());
        _ = FamilienBibliothekDienst.Ort(Kat);
        Assert.Equal("1", Environment.GetEnvironmentVariable(FamilienBibliothekDienst.TestVariable));   // erbt jeder Kindprozess (z. B. MCP-Server der Attrappe)
    }

    [Fact]
    public void Abfrage_mit_Parametern_und_Befehle_antworten_klar()
    {
        var w = MitFamilie();
        var l = Json(w.FamilienBibliothekLesen(filter: "heiz", auswahl: true)).AsObject();
        Assert.False(l.ContainsKey("fehler"), l.ToJsonString());
        Assert.False(l["bibliothek"]!["aktiv"]!.GetValue<bool>());
        Assert.Equal("gesperrt", Json(w.FamilienLesen())["familien"]![0]!["bibliothekStand"]!["art"]!.GetValue<string>());
        var a = Json(w.FamilienauswahlSetzenWerkzeug(Elem("""{ "kategorien": ["Heizkoerper"], "status": ["freigegeben"] }""")));
        Assert.True(a["angenommen"]!.GetValue<bool>(), a.ToJsonString());
        var f = Json(w.FamilienauswahlSetzenWerkzeug(Elem("""{ "status": ["alt"] }""")));
        Assert.False(f["angenommen"]!.GetValue<bool>());
        Assert.Contains("entwurf, freigegeben", f["vorschlag"]!.GetValue<string>());
        foreach (var antwort in new[] { w.FamilieInBibliothekWerkzeug(), w.FamilieAktualisierenWerkzeug("FAM-1"), w.FamilieLadenWerkzeug(id: "fam-test-heizkoerper") })
        {
            var j = Json(antwort);
            Assert.False(j["angenommen"]!.GetValue<bool>(), j.ToJsonString());
            Assert.Contains("Testlauf", j["fehler"]!.GetValue<string>());
            Assert.Contains(FamilienBibliothekDienst.Umgebungsvariable, j["vorschlag"]!.GetValue<string>());
        }
        // als_aktuell: die KI wird abgelehnt, bevor die Bibliothek gefragt wird
        var ki = new Werkzeuge(Projekt.Anlegen(Kat, null, "MCP B7 KI", "KI", "pc", "ki"));
        var k = Json(ki.FamilieInBibliothekWerkzeug(als_aktuell: true));
        Assert.Contains("nur ein Mensch", k["fehler"]!.GetValue<string>());
    }
}
