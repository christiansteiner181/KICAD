using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using static Hlks.Mcp.Tests.Hilfe;

namespace Hlks.Mcp.Tests;

/// <summary>MCP-Werkzeuge für BCF 3.0 (Modul 3b, B6): bcf_export, bcf_lesen, bcf_import mit Trockenlauf und Rückgängig – derselbe Weg,
/// den die KI geht. Kollision fiktiv über element_anlegen (die Prüfung baut B2).</summary>
public class WerkzeugBcfTests
{
    static JsonElement Elem(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public void ExportLesenImport_UeberDieWerkzeuge()
    {
        var ordner = Path.Combine(Path.GetTempPath(), "hlks_mcp_bcf_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var w = new Werkzeuge(Beispiel());
            var k = Json(w.ElementAnlegen("Kollision", kennzeichen: "KOL-1", eigenschaften: Elem("""{ "art": "hart", "status": "neu", "kennung": "k1", "beteiligte": ["WT-E.02"], "mass": 30 }"""),
                geometrie: Elem("""{ "art": "punkt", "punkte": [[1000, 2000, 900]] }""")));
            Assert.True(k["angenommen"]!.GetValue<bool>(), k.ToJsonString());

            var datei = Path.Combine(ordner, "kollisionen.bcf");
            var a = Json(w.BcfExportieren(datei, status: Elem("""["neu"]"""), autor: "ki@test"));
            Assert.Equal(1, a["topics"]!.GetValue<int>());
            var topic = a["kollisionen"]![0]!["topic"]!.GetValue<string>();

            var l = Json(w.BcfLesenWerkzeug(datei));
            Assert.Equal("KOL-1", l["topics"]![0]!["kollision"]!.GetValue<string>());
            Assert.Equal("ki@test", l["topics"]![0]!["autor"]!.GetValue<string>());

            // Rückmeldung des Partners: Status «In Progress»
            var geaendert = Path.Combine(ordner, "antwort.bcf");
            File.Copy(datei, geaendert);
            using (var z = ZipFile.Open(geaendert, ZipArchiveMode.Update))
            {
                var e = z.GetEntry(topic + "/markup.bcf")!;
                XDocument doc;
                using (var s = e.Open()) doc = XDocument.Load(s);
                doc.Root!.Element("Topic")!.SetAttributeValue("TopicStatus", "In Progress");
                e.Delete();
                using var s2 = z.CreateEntry(topic + "/markup.bcf").Open();
                doc.Save(s2);
            }
            var trocken = Json(w.BcfImportieren(geaendert, trockenlauf: true));
            Assert.True(trocken["trockenlauf"]!.GetValue<bool>());
            Assert.Single(trocken["aenderungen"]!.AsArray());
            Assert.Contains("\"neu\"", w.ElementLesen("KOL-1"));

            var i = Json(w.BcfImportieren(geaendert));
            Assert.True(i["angenommen"]!.GetValue<bool>(), i.ToJsonString());
            Assert.Contains("bestaetigt", w.ElementLesen("KOL-1"));
            Assert.True(Json(w.Rueckgaengig())["angenommen"]!.GetValue<bool>());
            Assert.DoesNotContain("bestaetigt", w.ElementLesen("KOL-1"));
        }
        finally { Aufraeumen(ordner); }
    }

    [Fact]
    public void Fehler_KommenAlsJsonNichtAlsAusnahme()
    {
        var w = new Werkzeuge(Beispiel());
        var e = Json(w.BcfExportieren(Path.Combine(Path.GetTempPath(), "x.zip"))).AsObject();
        Assert.Contains(".bcf", e["fehler"]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(e["vorschlag"]?.GetValue<string>()));
        var l = Json(w.BcfLesenWerkzeug(Path.Combine(Path.GetTempPath(), "gibt-es-nicht-" + Guid.NewGuid().ToString("N") + ".bcf"))).AsObject();
        Assert.Contains("gibt es nicht", l["fehler"]!.GetValue<string>());
        var i = Json(w.BcfImportieren(Path.Combine(Path.GetTempPath(), "gibt-es-nicht.bcf"))).AsObject();
        Assert.False(i["angenommen"]!.GetValue<bool>());
        Assert.False(string.IsNullOrWhiteSpace(i["vorschlag"]?.GetValue<string>()));
        // Beispielprojekt ohne Kollisionen: Export abgelehnt mit Weg zur Lösung
        Assert.Contains("kollision_pruefen", Json(w.BcfExportieren(Path.Combine(Path.GetTempPath(), "x.bcf")))["vorschlag"]!.GetValue<string>());
    }
}
