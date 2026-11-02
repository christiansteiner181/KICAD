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

    [Fact]
    public void Abschnitt_StatusArtikelHoehe_UndImExport()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        // Abschnitt «Fallstrang F1 ohne Formteile» plus von Hand ein Rohr eines anderen Strangs (Trasse)
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Montageabschnitt", ["kennzeichen"] = "MA-1", ["name"] = "Fallstrang F1",
            ["eigenschaften"] = new JsonObject
            {
                ["kriterien"] = new JsonObject { ["straenge"] = new JsonArray("SW-F1"), ["klassen"] = new JsonArray("Leitung") },
                ["elemente"] = new JsonArray("SW-S1-1"), ["termin"] = "2026-11-16", ["kolonne"] = "Kolonne A",
            },
        }));
        var ma = Hole(p, "MA-1");
        var el = BaustelleDaten.Abschnitt(p.Modell, p.Katalog, ma);
        Assert.Contains(el, e => e.Kennzeichen == "SW-F1-1");
        Assert.Contains(el, e => e.Kennzeichen == "SW-S1-1");
        Assert.All(el.Where(e => e.Kennzeichen != "SW-S1-1"), e => Assert.True(p.Katalog.IstArt(e, "Leitung")));

        Ok(p.Ausfuehren("montagestatus_setzen", new JsonObject { ["status"] = "montiert", ["abschnitt"] = "MA-1" }));
        Assert.Equal("montiert", Hole(p, "SW-F1-1").Text("montagestatus"));
        Assert.Equal("geplant", BaustelleDaten.Montagestatus(Hole(p, "KW-V1-1")));
        var liste = (JsonObject)Tuer.Abfrage(p, "montageabschnitte");
        Assert.Equal(el.Count, Json.Zahl(liste["abschnitte"]![0]!["status"]!["montiert"]));
        Nein(p.Ausfuehren("montagestatus_setzen", new JsonObject { ["status"] = "fertig", ["elemente"] = new JsonArray("SW-F1-1") }));
        Nein(p.Ausfuehren("montagestatus_setzen", new JsonObject { ["status"] = "montiert", ["elemente"] = new JsonArray("E.01") }));   // Raum ist kein Bauteil

        // Artikel am Element geht der Familie vor; Höhe einer senkrechten Leitung: Achse 300…3000, UK/OK um den halben Querschnitt
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "SW-F1-2", ["eigenschaften"] = new JsonObject { ["hersteller"] = "Muster AG", ["artikelnummer"] = "123.456", ["da"] = 110 } }));   // da: Aussendurchmesser für UK/OK (Beispiel ohne Serie)
        var art = BaustelleDaten.Artikel(p, Hole(p, "SW-F1-2"))!;
        Assert.Equal("123.456", art["artikelnummer"]!["wert"]!.GetValue<string>());
        Assert.Equal("element", art["artikelnummer"]!["herkunft"]!.GetValue<string>());
        var h = BaustelleDaten.Hoehe(p, Hole(p, "SW-F1-2"))!;
        Assert.Equal(300, Json.Zahl(h["achseMinMm"]));
        Assert.Equal(3000, Json.Zahl(h["achseMaxMm"]));
        Assert.Equal(245, Json.Zahl(h["ukMm"]));
        Assert.Equal(3055, Json.Zahl(h["okMm"]));

        var datei = Ziel();
        Tuer.Abfrage(p, "baustelle_export", new JsonObject { ["datei"] = datei, ["rolle"] = "bauleitung", ["stand"] = "arbeitsstand" });
        var html = File.ReadAllText(datei);
        Assert.Contains("\"kz\":\"MA-1\"", html);
        Assert.Contains("\"status\":\"montiert\"", html);
        File.Delete(datei);
    }
}
