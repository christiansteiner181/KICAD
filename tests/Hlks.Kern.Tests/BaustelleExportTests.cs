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
        // Reiter der Webseite (Christian 12.11.2026: Monteur arbeitet mit Grundriss, Strang- und Prinzipschema): jede Ansicht mit Gruppe
        var gruppen = daten["ansichten"]!.AsArray().Select(x => x!["gruppe"]!.GetValue<string>()).ToHashSet();
        Assert.Contains("grundriss", gruppen);
        Assert.Contains("strangschema", gruppen);
        Assert.Subset(new HashSet<string> { "grundriss", "strangschema", "prinzipschema", "schnitt" }, gruppen);
        // Räume mit Begrenzung für «Raum in 3D» (Christian 12.11.2026): glTF-Meter, min < max je Achse
        var raum = daten["elemente"]!.AsArray().First(x => (string?)x!["klasse"] == "Raum" && x["box3d"] is not null)!;
        var b = raum["box3d"]!.AsArray().Select(v => v!.GetValue<double>()).ToArray();
        Assert.Equal(6, b.Length);
        Assert.True(b[0] < b[3] && b[1] < b[4] && b[2] < b[5]);
        // three.js im selben Modul, ohne Import-Zeilen (läuft von file://)
        Assert.DoesNotContain("from 'three'", html);
        Assert.Contains("class OrbitControls", html);
        Assert.Contains("rel=\"apple-touch-icon\" href=\"data:image/png;base64,iVBOR", html);   // Icon für den Startbildschirm (PNG)
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

    /// <summary>Rückmeldedatei wie sie die Webseite speichert (Format in BaustelleRueckmeldung.cs).</summary>
    static JsonObject Rueckmeldung(Projekt p, string? projektId = null) => new()
    {
        ["format"] = "kicad-baustelle-rueckmeldung", ["fassung"] = 1, ["projektId"] = projektId ?? p.Kopf.Id, ["projekt"] = p.Kopf.Name,
        ["modellPruefsumme"] = p.Stand.Pruefsumme[..12], ["person"] = "Max Monteur, Muster AG", ["gespeichert"] = "2026-11-03T10:00:00Z",
        ["meldungen"] = new JsonArray(
            new JsonObject { ["id"] = "m1", ["zeit"] = "2026-11-03T08:15:00Z", ["art"] = "status", ["element"] = Hole(p, "SW-F1-1").Id, ["kz"] = "SW-F1-1", ["status"] = "montiert" },
            new JsonObject { ["id"] = "m2", ["zeit"] = "2026-11-03T08:20:00Z", ["art"] = "mangel", ["element"] = Hole(p, "SW-F1-2").Id, ["kz"] = "SW-F1-2", ["text"] = "Rohrschelle fehlt",
                ["fotos"] = new JsonArray("data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==") },
            new JsonObject { ["id"] = "m3", ["zeit"] = "2026-11-03T08:30:00Z", ["art"] = "abweichung", ["kz"] = "SW-F1-2", ["text"] = "Fallstrang versetzt",
                ["mass"] = new JsonObject { ["dx"] = 40, ["dy"] = 0, ["dz"] = 0 } }),
    };

    [Fact]
    public void Rueckmeldung_LesenUndUebernehmen_StatusUndMarker_EinzweitesMalNicht()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        var r = Rueckmeldung(p);
        var vorschau = (JsonObject)Tuer.Abfrage(p, "rueckmeldung_lesen", new JsonObject { ["inhalt"] = r.DeepClone() });
        Assert.Equal(3, vorschau["meldungen"]!.AsArray().Count);
        Assert.All(vorschau["meldungen"]!.AsArray(), x => Assert.False(x!["uebernommen"]!.GetValue<bool>()));
        Assert.False(vorschau["modellGeaendert"]!.GetValue<bool>());

        Ok(p.Ausfuehren("rueckmeldung_uebernehmen", new JsonObject { ["inhalt"] = r.DeepClone() }));
        // Status mit Herkunft Baustelle: Person und Zeit der Meldung
        var w = Hole(p, "SW-F1-1").Eigenschaft("montagestatus")!;
        Assert.Equal("montiert", w.Inhalt!.GetValue<string>());
        Assert.Equal("baustelle", w.Herkunft!.Art);
        Assert.Equal("Max Monteur, Muster AG", w.Herkunft.Akteur);
        // Mangel und Abweichung als Problem-Marker an der Lage des Elements; das Element selbst bleibt (keine Lageänderung)
        var marker = p.Modell.DerKlasse("ProblemMarker").ToList();
        Assert.Equal(2, marker.Count);
        var mangel = marker.Single(x => x.Text("kennung") == "m2");
        Assert.Equal("mangel", mangel.Text("art"));
        Assert.Equal("baustelle", mangel.Text("quelle"));
        Assert.Single(mangel.Eigenschaft("fotos")!.Inhalt!.AsArray());
        Assert.Equal(1650, mangel.Geometrie!.Punkte[0][2]);   // Mitte der Achse 300…3000
        Assert.Equal(40, Json.Zahl(marker.Single(x => x.Text("kennung") == "m3").Eigenschaft("mass")!.Inhalt!["dx"]));

        // ein zweites Mal: nichts Neues; fremdes Projekt: abgelehnt
        Ok(p.Ausfuehren("rueckmeldung_uebernehmen", new JsonObject { ["inhalt"] = r.DeepClone() }));
        Assert.Equal(2, p.Modell.DerKlasse("ProblemMarker").Count());
        Assert.All(((JsonObject)Tuer.Abfrage(p, "rueckmeldung_lesen", new JsonObject { ["inhalt"] = r.DeepClone() }))["meldungen"]!.AsArray(),
            x => Assert.True(x!["uebernommen"]!.GetValue<bool>()));
        Nein(p.Ausfuehren("rueckmeldung_uebernehmen", new JsonObject { ["inhalt"] = Rueckmeldung(p, "anderes-projekt") }));
    }
}
