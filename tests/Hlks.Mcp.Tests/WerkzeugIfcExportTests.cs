using System.Text.Json;
using Hlks.Kern;
using static Hlks.Mcp.Tests.Hilfe;

namespace Hlks.Mcp.Tests;

/// <summary>Modul 7 Teil B2: MCP-Werkzeug ifc_export – Export des Beispielprojekts «MFH Musterweg» (fiktiv) mit Umfang, Räumen und einer
/// weiteren IDS-Datei, Bericht als Antwort und daneben, Modell unverändert (kein Paket); falsche Eingaben als Fehler mit Vorschlag.</summary>
public class WerkzeugIfcExportTests : IDisposable
{
    readonly string ordner = Path.Combine(Path.GetTempPath(), "HLKS_CAD", $"mcp-b2-{Environment.ProcessId}-{Guid.NewGuid().ToString("N")[..8]}");

    public WerkzeugIfcExportTests() => Directory.CreateDirectory(ordner);
    public void Dispose() { try { Directory.Delete(ordner, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }

    static JsonElement Elem(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public void IfcExport_UmfangRaeumeIds_BerichtUndDatei_ModellUnveraendert()
    {
        var p = Beispiel();
        var w = new Werkzeuge(p);
        var ids = Path.Combine(ordner, "pumpen.ids");
        File.WriteAllText(ids, """
            <?xml version="1.0" encoding="UTF-8"?>
            <ids:ids xmlns:ids="http://standards.buildingsmart.org/IDS" xmlns:xs="http://www.w3.org/2001/XMLSchema">
              <ids:info><ids:title>Pumpen (fiktiv)</ids:title></ids:info>
              <ids:specifications>
                <ids:specification name="Pumpe vorhanden" ifcVersion="IFC4">
                  <ids:applicability minOccurs="1" maxOccurs="unbounded"><ids:entity><ids:name><ids:simpleValue>IFCPUMP</ids:simpleValue></ids:name></ids:entity></ids:applicability>
                </ids:specification>
              </ids:specifications>
            </ids:ids>
            """);
        int pakete = p.Verlauf.Count;
        var datei = Path.Combine(ordner, "export.ifc");
        var b = Json(w.IfcExportieren(datei, "IFC4", Elem("""{ "gewerke": ["heizung", "sanitaer"] }"""), raeume: true, ids: Elem($"[{JsonSerializer.Serialize(ids)}]")));
        Assert.Null(b["fehler"]);
        Assert.Equal(datei, b["datei"]!.GetValue<string>());
        Assert.Equal("fehlerfrei", b["schemapruefung"]!["ergebnis"]!.GetValue<string>());
        Assert.Equal(13, b["anzahl"]!["raeume"]!.GetValue<int>());
        Assert.Equal(2, b["ids"]!.AsArray().Count);
        Assert.Equal("bestanden", b["ids"]![1]!["ergebnis"]!.GetValue<string>());
        Assert.True(File.Exists(datei));
        Assert.True(File.Exists(Path.Combine(ordner, "export.bericht.json")));
        Assert.True(File.Exists(Path.Combine(ordner, "export.bericht.md")));
        Assert.Equal(pakete, p.Verlauf.Count);   // Abfrage: kein Paket
        using var d = IfcDatei.Oeffnen(datei);
        Assert.Equal("IFC4", d.Schema);
        Assert.DoesNotContain(d.Modell.Instances.OfType<Xbim.Ifc4.Interfaces.IIfcProduct>(), x => x.ExpressType.Name == "IfcWall");   // Architektur nicht im Umfang
    }

    [Fact]
    public void IfcExport_FalscheEingaben_FehlerMitVorschlag()
    {
        var w = new Werkzeuge(Beispiel());
        var f1 = Json(w.IfcExportieren(Path.Combine(ordner, "a.txt")));
        Assert.Contains(".ifc", f1["fehler"]!.GetValue<string>());
        var f2 = Json(w.IfcExportieren(Path.Combine(ordner, "a.ifc"), fassung: "IFC4X3"));
        Assert.Contains("IFC2X3", f2["vorschlag"]!.GetValue<string>());
        var f3 = Json(w.IfcExportieren(Path.Combine(ordner, "a.ifc"), umfang: Elem("""{ "gewerke": ["elektrisch"] }""")));
        Assert.Contains("elektrisch", f3["fehler"]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(f3["vorschlag"]?.GetValue<string>()));
        var f4 = Json(w.IfcExportieren(Path.Combine(ordner, "a.ifc"), ids: Elem("""["C:/gibt/es/nicht.ids"]""")));
        Assert.Contains("IDS-Datei", f4["fehler"]!.GetValue<string>());
        Assert.False(File.Exists(Path.Combine(ordner, "a.ifc")));
    }
}
