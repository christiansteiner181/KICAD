using System.Text.Json;
using System.Text.Json.Nodes;
using Hlks.Kern;
using static Hlks.Mcp.Tests.Hilfe;

namespace Hlks.Mcp.Tests;

/// <summary>Modul 3b, B4b: MCP-Werkzeug aussparungen_ifc – Fehler ohne freigegebene Aussparung (mit Vorschlag), Ausgabe nach Freigabe,
/// Exportbericht, Datei wieder lesbar, Modell unverändert, Status danach über aussparung_status. FIKTIVE Vorgaben (Spiel 20,
/// Zusammenfassen 100 mm) und Testrohrserie (DN 50: da 55); die Kernbohrung von Hand über aussparung_form (Nachtrag B3b).</summary>
public class WerkzeugAussparungIfcTests : IDisposable
{
    readonly string ordner = Path.Combine(Path.GetTempPath(), "HLKS_CAD", $"mcp-b4b-{Environment.ProcessId}-{Guid.NewGuid().ToString("N")[..8]}");

    public void Dispose()
    {
        try { Directory.Delete(ordner, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    static JsonElement Elem(string json) => JsonDocument.Parse(json).RootElement.Clone();

    static JsonNode Ok(string antwort)
    {
        var j = Json(antwort);
        Assert.True(j["angenommen"]?.GetValue<bool>() == true, antwort);
        return j;
    }

    [Fact]
    public void AussparungenIfc_NachFreigabe_Bericht_DateiLesbar_ModellUnveraendert()
    {
        var p = Beispiel();
        var w = new Werkzeuge(p);
        var datei = Path.Combine(ordner, "aussparungen.ifc");

        // ohne Aussparungen: Fehler mit Vorschlag, keine Datei
        var keine = Json(w.AussparungenIfcSchreiben(datei));
        Assert.Contains("keine Aussparungen", keine["fehler"]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(keine["vorschlag"]?.GetValue<string>()));
        Assert.False(File.Exists(datei));

        Ok(w.ElementAnlegen("Vorgaben", "VG-1", eigenschaften: Elem("""
            { "spiel_aussparung": { "wert": 20, "quelle": "FIKTIV Test B4b" },
              "abstand_zusammenfassen": { "wert": 100, "quelle": "FIKTIV Test B4b" } }
            """)));
        Ok(w.FamilieLadenWerkzeug(Testdaten(Path.Combine("familien", "test_rohrserie.familie.json"))));
        var wand = Ok(w.WandZeichnen("EG", Elem("[[50000, 0], [56000, 0]]"), 200))["aenderungen"]![0]!["kennzeichen"]!.GetValue<string>();
        Ok(w.ElementAnlegen("Rohr", "RO-T1", system: "KW", geschoss: "EG", eigenschaften: Elem("""{ "familientyp": "DN 50" }"""),
            bezuege: Elem("""{ "familie": "FAM-1" }"""), geometrie: Elem("""{ "art": "polylinie", "punkte": [[53000, -500, 1000], [53000, 500, 1000]] }""")));
        var e = Ok(w.AussparungenVorschlagenWerkzeug(bauteile: Elem($"[\"{wand}\"]")));
        var kz = e["aenderungen"]!.AsArray().Single(x => x!["klasse"]!.GetValue<string>() == "Aussparung")!["kennzeichen"]!.GetValue<string>();
        Ok(w.AussparungFormWerkzeug(Elem($"[\"{kz}\"]"), "rund"));   // B3b: Kernbohrung nur von Hand
        Ok(w.AussparungenNummerierenWerkzeug(geschoss: "EG", alle: true));

        // vorgeschlagen ist noch nicht ausgegeben (Vorgabe: freigegeben und versandt)
        var vor = Json(w.AussparungenIfcSchreiben(datei));
        Assert.Contains("Keine Aussparung passt zum Filter", vor["fehler"]!.GetValue<string>());
        Assert.Contains("aussparung_status", vor["vorschlag"]!.GetValue<string>());

        Ok(w.AussparungStatusWerkzeug(Elem($"[\"{kz}\"]"), "freigegeben"));
        int pakete = p.Verlauf.Count;
        var b = Json(w.AussparungenIfcSchreiben(datei, fassung: "IFC2X3", geschoss: "EG", status: Elem("""["freigegeben", "versandt"]""")));
        Assert.Null(b["fehler"]);
        Assert.Equal(Path.GetFullPath(datei), b["datei"]!.GetValue<string>());
        Assert.Equal("IFC2X3", b["fassung"]!.GetValue<string>());
        Assert.Equal(1, b["anzahl"]!.GetValue<int>());
        Assert.Equal(1, b["jeArt"]!["kernbohrung"]!.GetValue<int>());
        Assert.Equal(1, b["jeGeschoss"]!["EG"]!.GetValue<int>());
        Assert.Equal("bestanden", b["rundreise"]!["ergebnis"]!.GetValue<string>());
        Assert.Contains(b["hinweise"]!.AsArray(), h => h!.GetValue<string>().Contains("aussparung_status"));   // noch auf «freigegeben»
        Assert.True(File.Exists(datei));
        Assert.Equal(pakete, p.Verlauf.Count);   // Abfrage: kein Paket

        using (var d = IfcDatei.Oeffnen(datei))
        {
            Assert.Equal("IFC2X3", d.Schema);
            var px = Assert.Single(d.Modell.Instances.OfType<Xbim.Ifc4.Interfaces.IIfcBuildingElementProxy>());
            Assert.Equal("A-EG-001", px.Name?.ToString());
            Assert.Equal("ProvisionForVoid", px.ObjectType?.ToString());
        }

        // nach dem Versand: Status versandt, danach erscheint er im Standardfilter weiter
        Ok(w.AussparungStatusWerkzeug(Elem($"[\"{kz}\"]"), "versandt"));
        var b2 = Json(w.AussparungenIfcSchreiben(datei));
        Assert.Equal(1, b2["jeStatus"]!["versandt"]!.GetValue<int>());
        Assert.Equal("IFC4", b2["fassung"]!.GetValue<string>());
        Assert.Equal(b["aussparungen"]![0]!["globalId"]!.GetValue<string>(), b2["aussparungen"]![0]!["globalId"]!.GetValue<string>());   // dieselbe GlobalId
        Assert.DoesNotContain(b2["hinweise"]!.AsArray(), h => h!.GetValue<string>().Contains("stehen noch auf «freigegeben»"));
    }

    [Fact]
    public void AussparungenIfc_FalscheEingaben_FehlerMitVorschlag()
    {
        var w = new Werkzeuge(Beispiel());
        var f1 = Json(w.AussparungenIfcSchreiben(Path.Combine(ordner, "a.txt")));
        Assert.Contains(".ifc", f1["fehler"]!.GetValue<string>());
        var f2 = Json(w.AussparungenIfcSchreiben(Path.Combine(ordner, "a.ifc"), fassung: "IFC4X3"));
        Assert.Contains("IFC4X3", f2["fehler"]!.GetValue<string>());
        Assert.Contains("IFC2X3", f2["vorschlag"]!.GetValue<string>());
        var f3 = Json(w.AussparungenIfcSchreiben(Path.Combine(ordner, "a.ifc"), koordinaten: "lv95"));
        Assert.Contains("lv95", f3["fehler"]!.GetValue<string>());
        var f4 = Json(w.AussparungenIfcSchreiben(Path.Combine(ordner, "a.ifc"), status: Elem("""["fertig"]""")));
        Assert.Contains("«fertig»", f4["fehler"]!.GetValue<string>());
    }
}
