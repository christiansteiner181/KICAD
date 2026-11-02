using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>BIM to field Stufe 1 (entwurf/bim-to-field.md): Webseite für die Baustelle am Beispielprojekt «MFH Musterweg».</summary>
public class BaustelleExportTests
{
    static string Ziel() => Path.Combine(Path.GetTempPath(), "kicad-baustelle-test", Guid.NewGuid().ToString("N") + ".html");

    [Fact]
    public void Export_EineDateiMitDatenPlaenenUnd3d_StandUndRolleImKopf()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        var datei = Ziel();
        var erg = (JsonObject)Tuer.Abfrage(p, "baustelle_export", new JsonObject { ["datei"] = datei, ["rolle"] = "bauleitung", ["stand"] = "arbeitsstand" });
        Assert.True(File.Exists(datei));
        Assert.True(Json.Zahl(erg["ansichten"]) > 0);
        Assert.True(Json.Zahl(erg["geschosse3d"]) > 0);

        // Daten aus der Seite lesen wie der Betrachter
        var html = File.ReadAllText(datei);
        const string start = "<script type=\"application/json\" id=\"kicad-daten\">";
        var a = html.IndexOf(start, StringComparison.Ordinal) + start.Length;
        var daten = JsonNode.Parse(html[a..html.IndexOf("</script>", a, StringComparison.Ordinal)])!;
        Assert.Equal("arbeitsstand", daten["kopf"]!["stand"]!.GetValue<string>());
        Assert.Null(daten["kopf"]!["freigegebenVon"]);
        var e01 = daten["elemente"]!.AsArray().Single(e => e!["kz"]!.GetValue<string>() == "E.01")!;
        Assert.Equal("EG", e01["geschoss"]!.GetValue<string>());
        // Leitungen tragen Material für den Auszug; Ansichten und Planblätter sind keine Elemente der Seite
        Assert.Contains(daten["elemente"]!.AsArray(), e => e!["mat"]?["art"]?.GetValue<string>() == "leitung" && Json.Zahl(e["mat"]!["laengeM"]) > 0);
        Assert.DoesNotContain(daten["elemente"]!.AsArray(), e => e!["klasse"]!.GetValue<string>() is "Ansicht" or "Planblatt");
        Assert.Contains("<svg", daten["ansichten"]![0]!["svg"]!.GetValue<string>());
        // three.js im selben Modul, ohne Import-Zeilen (läuft von file://)
        Assert.DoesNotContain("from 'three'", html);
        Assert.Contains("class OrbitControls", html);
        File.Delete(datei);
    }

    [Fact]
    public void Monteur_NurFreigegeben_KiGibtNichtFrei()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        var f = Assert.Throws<HlksFehler>(() => Tuer.Abfrage(p, "baustelle_export", new JsonObject { ["datei"] = Ziel(), ["rolle"] = "monteur", ["stand"] = "arbeitsstand" }));
        Assert.Contains("nur freigegebene", f.Message);
        var ki = Projekt.Anlegen(Kat, null, "Test", "claude", "test", akteurArt: "ki");
        Assert.Throws<HlksFehler>(() => Tuer.Abfrage(ki, "baustelle_export", new JsonObject { ["datei"] = Ziel() }));
    }
}
