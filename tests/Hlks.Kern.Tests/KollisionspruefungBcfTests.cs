using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.IfcNetzFassung2Testdatei;
using static Hlks.Kern.Tests.KollisionspruefungHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3b, B2 mit B6: BCF-Rundreise mit echten Kollisionen aus einem Prüflauf – Beteiligte im Format von B2 (Element-Id
/// bzw. «Unterlage-Id:GlobalId») werden zu Komponenten (KICAD-Id bzw. IfcGuid), bcf_export → bcf_import ohne Änderung ändert nichts,
/// ein im BCF geänderter Status kommt zurück. Testdatei von B1b (Wände als Hintergrund), Masse FIKTIV.</summary>
public sealed class KollisionspruefungBcfTests : IDisposable
{
    readonly List<string> dateien = [];
    Projekt? p;

    public void Dispose()
    {
        foreach (var d in dateien) try { File.Delete(d); } catch (IOException) { }
        if (p is not null) try { Directory.Delete(Unterlagen.Ordner(p), true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    string NeueDatei()
    {
        var d = Path.Combine(Path.GetTempPath(), "HLKS_CAD", $"b2-bcf-{Guid.NewGuid():N}.bcf");
        dateien.Add(d);
        return d;
    }

    [Fact]
    public void Rundreise_EchteKollisionen_ExportImportOhneAenderung_StatusZurueck()
    {
        p = IfcHilfe.Leer();
        Ok(IfcHilfe.Uebernehmen(p, Datei("IFC4"), new JsonObject { ["bereiche"] = new JsonArray("raeume") }));   // Wände als Hintergrund
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "KW", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Trinkwasser kalt" } }));
        Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Modul3Hilfe.Datei("test_rohrserie") }));
        var fam = p.Modell.DerKlasse("Familie").Single().Kennzeichen;
        foreach (var (kz, x) in new[] { ("RO-1", 2500.0), ("RO-2", 4800.0) })
            Ok(p.Ausfuehren("element_anlegen", new JsonObject
            {
                ["klasse"] = "Rohr", ["kennzeichen"] = kz, ["system"] = "KW", ["eigenschaften"] = new JsonObject { ["familientyp"] = "DN 20" },
                ["bezuege"] = new JsonObject { ["familie"] = fam }, ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Pts([x, -500, 600], [x, 700, 600]) },
            }));
        Ok(Pruefen(p));
        var ks = Kollisionen(p);
        Assert.Equal(2, ks.Count);
        var ul = p.Modell.DerKlasse("Unterlage").Single();
        Assert.All(ks, k => Assert.Contains($"{ul.Id}:{Gid("W-A")}", Beteiligte(k)));

        // Export
        var datei = NeueDatei();
        var ex = (JsonObject)Tuer.Abfrage(p, "bcf_export", new JsonObject { ["datei"] = datei });
        Assert.Equal(2, ex["topics"]!.GetValue<int>());
        Assert.StartsWith("bestanden", ex["schemapruefung"]!.GetValue<string>());
        var komp = ex["kollisionen"]![0]!["komponenten"]!.AsArray().Select(x => x!.GetValue<string>()).ToList();
        Assert.Contains($"IfcGuid {Gid("W-A")}", komp);
        Assert.Contains(komp, x => x.StartsWith("KICAD "));
        // Lesen: alle zugeordnet, Komponenten auflösbar
        var le = (JsonObject)Tuer.Abfrage(p, "bcf_lesen", new JsonObject { ["datei"] = datei });
        Assert.Equal(2, le["zugeordnet"]!.GetValue<int>());
        Assert.Equal(0, le["unbekannt"]!.GetValue<int>());
        Assert.All(le["topics"]!.AsArray(), t => Assert.Contains(t!["viewpoints"]![0]!["komponenten"]!.AsArray(), c => c!["element"]?.GetValue<string>() is "RO-1" or "RO-2"));
        // Import ohne Änderung: nichts
        var start = p.Modell.Pruefsumme();
        var im = Ok(p.Ausfuehren("bcf_import", new JsonObject { ["datei"] = datei }));
        Assert.Empty(im.Aenderungen);
        Assert.Equal(start, p.Modell.Pruefsumme());
        Assert.Contains(im.Hinweise, h => h.Contains("2 zugeordnet") && h.Contains("0 Status übernommen"));
        // Status im BCF «Bestätigt» (über einen Export nach kollision_status), zurück auf neu, dann Import → bestaetigt
        Ok(Status(p, "bestaetigt", null, "KOL-1"));
        var datei2 = NeueDatei();
        Tuer.Abfrage(p, "bcf_export", new JsonObject { ["datei"] = datei2 });
        Ok(Status(p, "neu", null, "KOL-1"));
        Ok(p.Ausfuehren("bcf_import", new JsonObject { ["datei"] = datei2 }));
        Assert.Equal("bestaetigt", Hole(p, "KOL-1").Text("status"));
        // ein weiterer Prüflauf behält den Status und die Topic-Guid (Kennung unverändert)
        var guid = BcfFormat.TopicGuid(p.Kopf.Id, Hole(p, "KOL-1"));
        Ok(Pruefen(p));
        Assert.Equal("bestaetigt", Hole(p, "KOL-1").Text("status"));
        Assert.Equal(guid, BcfFormat.TopicGuid(p.Kopf.Id, Hole(p, "KOL-1")));
    }
}
