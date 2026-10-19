using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Hlks.Kern;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Hlks.Web.Tests;

/// <summary>
/// Nachbearbeitung N3 (fehlende Bedienorte): Die Oberfläche braucht keine neuen Endpunkte – sie erreicht den Kernbefehl einlageplaene_anlegen und die Abfrage verlegung
/// über die allgemeine Tür /api/befehl/{name} bzw. /api/abfrage/{name}, die Projektinformation über element_anlegen/element_aendern und die Listen über
/// /api/abfrage/kollisionen bzw. aussparungsliste. Diese Tests prüfen genau diese Wege (Trockenlauf, keine Doppel, Rückgängig) und dass die neuen Dateien der Oberfläche
/// ausgeliefert werden. Jeder Test hat seinen eigenen Server und Temp-Ordner; alle Werte des Testhauses sind FIKTIV.
/// </summary>
public class NachbearbeitungN3Tests
{
    sealed class Server : IDisposable
    {
        readonly WebApplicationFactory<Program> basis = new();
        readonly WebApplicationFactory<Program> fabrik;
        public HttpClient Http { get; }
        public string Wurzel { get; } = Path.Combine(Path.GetTempPath(), "hlks_n3_" + Guid.NewGuid().ToString("N")[..10]);
        public string Ordner => Path.Combine(Wurzel, "c_Pläne und Modelle", "e_KICAD");

        public Server()
        {
            var e = new Einstellungen("Test", "testpc", Katalog.StandardOrdner(), null) { ZuletztMerken = false };
            fabrik = basis.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton(e)));
            Http = fabrik.CreateClient();
        }

        public async Task<(HttpStatusCode Code, JsonNode? Json)> Post(string pfad, JsonNode? koerper = null)
        {
            var a = await Http.PostAsync(pfad, new StringContent((koerper ?? new JsonObject()).ToJsonString(), Encoding.UTF8, "application/json"));
            var text = await a.Content.ReadAsStringAsync();
            return (a.StatusCode, text.Length == 0 ? null : JsonNode.Parse(text));
        }

        public async Task Anlegen()
        {
            var (code, _) = await Post("/api/projekt/anlegen", new JsonObject { ["ordner"] = Ordner, ["name"] = "N3 Test", ["beispiel"] = true });
            Assert.Equal(HttpStatusCode.OK, code);
        }

        /// <summary>Befehl über die Tür der Oberfläche (/api/befehl); der Befehl muss angenommen werden, außer erwartet = false.</summary>
        public async Task<JsonNode> Befehl(string name, JsonNode? p = null, bool trockenlauf = false, bool erwartet = true)
        {
            var (code, j) = await Post($"/api/befehl/{name}?trockenlauf={trockenlauf.ToString().ToLowerInvariant()}", p);
            Assert.Equal(HttpStatusCode.OK, code);
            Assert.Equal(erwartet, j!["angenommen"]!.GetValue<bool>());
            return j!;
        }

        public async Task<JsonNode> Abfrage(string name, JsonNode? p = null)
        {
            var (code, j) = await Post($"/api/abfrage/{name}", p);
            Assert.Equal(HttpStatusCode.OK, code);
            return j!;
        }

        public async Task<JsonArray> Elemente() => (JsonArray)(JsonNode.Parse(await Http.GetStringAsync("/api/modell"))!["elemente"]!);

        public async Task<JsonNode?> Element(string kennzeichen) => (await Elemente()).FirstOrDefault(e => e?["kennzeichen"]?.GetValue<string>() == kennzeichen);

        /// <summary>Betondecke über dem UG (z −300 … 0), ein Rohr darin (Einlage D.ü.UG), Bodenplatte und ein Rohr darunter (Grundleitung).</summary>
        public async Task Haus()
        {
            var umriss = (double z) => new JsonArray(new JsonArray(0, 0, z), new JsonArray(12000, 0, z), new JsonArray(12000, 10000, z), new JsonArray(0, 10000, z));
            await Befehl("element_anlegen", new JsonObject { ["klasse"] = "Decke", ["kennzeichen"] = "D-EG", ["geschoss"] = "EG", ["eigenschaften"] = new JsonObject { ["aufbau"] = "Beton 300" },
                ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = umriss(0), ["dicke"] = 300 } });
            await Befehl("element_anlegen", new JsonObject { ["klasse"] = "Decke", ["kennzeichen"] = "D-BP", ["geschoss"] = "UG", ["eigenschaften"] = new JsonObject { ["aufbau"] = "Beton 400", ["art"] = "boden" },
                ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = umriss(-2700), ["dicke"] = 400 } });
            foreach (var (kz, z) in new[] { ("R-N3-1", -150.0), ("R-N3-2", -3300.0) })
                await Befehl("element_anlegen", new JsonObject { ["klasse"] = "Rohr", ["kennzeichen"] = kz, ["system"] = "SW", ["geschoss"] = "UG", ["eigenschaften"] = new JsonObject { ["dn"] = 100, ["da"] = 110 },
                    ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = new JsonArray(new JsonArray(1000, 1000, z), new JsonArray(5000, 1000, z)) } });
        }

        public void Dispose()
        {
            try { Http.PostAsync("/api/projekt/schliessen", new StringContent("{}")).GetAwaiter().GetResult(); } catch { /* egal */ }
            Http.Dispose();
            fabrik.Dispose();
            basis.Dispose();
            for (int i = 0; i < 5; i++)
            {
                try { if (Directory.Exists(Wurzel)) Directory.Delete(Wurzel, true); return; }
                catch (IOException) { Thread.Sleep(200); }
                catch (UnauthorizedAccessException) { Thread.Sleep(200); }
            }
        }
    }

    static IEnumerable<string> Namen(JsonNode antwort, string klasse) =>
        antwort["aenderungen"]!.AsArray().Where(a => a!["art"]!.GetValue<string>() == "angelegt" && a["klasse"]!.GetValue<string>() == klasse).Select(a => a!["kennzeichen"]!.GetValue<string>());

    [Fact]
    public async Task Oberflaeche_NeueDateienWerdenAusgeliefert_IndexBindetDasStylesheetEin()
    {
        using var s = new Server();
        foreach (var pfad in new[]
        {
            "/css/nachbearbeitung-n3.css", "/js/oberflaeche/einlageplaene-dialog.js", "/js/oberflaeche/einlageplaene-text.js", "/js/oberflaeche/projektinfo-dialog.js",
            "/js/oberflaeche/projektinfo-text.js", "/js/oberflaeche/kuerzel-liste.js", "/js/ansichten/liste-csv.js", "/js/ansichten/liste-csv-dialog.js",
        })
        {
            var a = await s.Http.GetAsync(pfad);
            Assert.Equal(HttpStatusCode.OK, a.StatusCode);
            Assert.True((await a.Content.ReadAsStringAsync()).Length > 200, pfad);
        }
        Assert.Contains("css/nachbearbeitung-n3.css", await s.Http.GetStringAsync("/"));
    }

    [Fact]
    public async Task Einlageplaene_UeberDieTuerDerOberflaeche_AbfrageTrockenlaufAnlegenKeineDoppel_RueckgaengigEinBefehl()
    {
        using var s = new Server();
        await s.Anlegen();
        await s.Haus();

        // Abfrage verlegung (Planliste des Dialogs): Planschlüssel, Bauteil, Geschoss
        var v = await s.Abfrage("verlegung", new JsonObject { ["art"] = "einlage" });
        var plaene = v["plaene"]!.AsArray().Select(p => (p!["plan"]!.GetValue<string>(), p["bauteilart"]!.GetValue<string>(), p["geschoss"]!.GetValue<string>())).OrderBy(x => x.Item1).ToList();
        Assert.Equal([("Einlagen Bodenplatte UG", "bodenplatte", "UG"), ("Einlagen D.ü.UG", "decke", "UG")], plaene);
        var erdreich = await s.Abfrage("verlegung", new JsonObject { ["art"] = "erdreich" });
        Assert.Contains(erdreich["plaene"]!.AsArray(), p => p!["plan"]!.GetValue<string>() == "Grundleitungen UG");

        // Trockenlauf (Vorschau im Dialog): nennt, was entsteht, und legt nichts an
        var vorher = (await s.Elemente()).Count;
        var trocken = await s.Befehl("einlageplaene_anlegen", new JsonObject { ["art"] = "einlage", ["geschoss"] = "UG" }, trockenlauf: true);
        Assert.True(trocken["trockenlauf"]!.GetValue<bool>());
        Assert.Equal(["EL-BP-UG", "EL-D.ü.UG"], Namen(trocken, "Ansicht").Order().ToArray());
        Assert.Equal(2, Namen(trocken, "Planblatt").Count());
        Assert.Contains(trocken["hinweise"]!.AsArray(), h => h!.GetValue<string>().Contains("Sanitär Einlagen D.ü.UG"));
        Assert.Equal(vorher, (await s.Elemente()).Count);

        // Anlegen mit Massstab und Format (Wahl im Dialog)
        var echt = await s.Befehl("einlageplaene_anlegen", new JsonObject { ["art"] = "einlage", ["geschoss"] = "UG", ["massstab"] = 100, ["format"] = "A2" });
        Assert.Equal(2, Namen(echt, "Ansicht").Count());
        var ansicht = (await s.Element("EL-D.ü.UG"))!;
        Assert.Equal("einlagen", ansicht["werte"]!["darstellung"]!.GetValue<string>());
        Assert.Equal("decke", ansicht["werte"]!["einlagen"]!.GetValue<string>());
        Assert.Equal(100, ansicht["werte"]!["massstab"]!.GetValue<double>());
        Assert.Contains((await s.Elemente()), e => e!["klasse"]!.GetValue<string>() == "Planblatt" && e["werte"]!["titel"]!.GetValue<string>() == "Sanitär Einlagen D.ü.UG" && e["werte"]!["format"]!.GetValue<string>() == "A2");

        // zweites Mal: keine Doppel (Dialog zeigt «nichts Neues»)
        var nochmal = await s.Befehl("einlageplaene_anlegen", new JsonObject { ["art"] = "einlage" });
        Assert.Empty(Namen(nochmal, "Ansicht"));
        Assert.Empty(Namen(nochmal, "Planblatt"));

        // Grundleitungsplan, dann Rückgängig nimmt Ansicht, Blatt und Fenster zusammen zurück (ein Befehl)
        var gl = await s.Befehl("einlageplaene_anlegen", new JsonObject { ["art"] = "erdreich" });
        Assert.Equal(["GL-UG"], Namen(gl, "Ansicht").ToArray());
        Assert.NotNull(await s.Element("GL-UG"));
        await s.Befehl("rueckgaengig");
        Assert.Null(await s.Element("GL-UG"));
        Assert.NotNull(await s.Element("EL-D.ü.UG"));

        // unbekannte Art: abgelehnt mit Vorschlag, nie ein Absturz
        var falsch = await s.Befehl("einlageplaene_anlegen", new JsonObject { ["art"] = "gibtsnicht" }, erwartet: false);
        Assert.False(string.IsNullOrEmpty(falsch["fehler"]?.GetValue<string>()));
    }

    [Fact]
    public async Task Projektinformation_UeberElementBefehle_AnlegenAendernRueckgaengig_HerkunftImVerlauf()
    {
        using var s = new Server();
        await s.Anlegen();
        Assert.Null(await s.Element("PI-1"));   // das Beispielprojekt hat noch keine

        // der Dialog legt an, wenn es keine gibt (Kennzeichen vergibt der Kern)
        var an = await s.Befehl("element_anlegen", new JsonObject
        {
            ["klasse"] = "Projektinformation", ["name"] = "Projektinformation",
            ["eigenschaften"] = new JsonObject { ["zeile1"] = "Testobjekt", ["rohrbezeichnung"] = "DN", ["nordwinkel"] = 12.5, ["bemassungEinheiten"] = new JsonObject { ["sanitaer"] = "mm" } },
        });
        Assert.Equal(["PI-1"], an["aenderungen"]!.AsArray().Select(a => a!["kennzeichen"]!.GetValue<string>()).ToArray());
        var w = (await s.Element("PI-1"))!["werte"]!;
        Assert.Equal("DN", w["rohrbezeichnung"]!.GetValue<string>());
        Assert.Equal(12.5, w["nordwinkel"]!.GetValue<double>());
        Assert.Equal("mm", w["bemassungEinheiten"]!["sanitaer"]!.GetValue<string>());

        // Ändern nur der geänderten Felder; null entfernt
        await s.Befehl("element_aendern", new JsonObject { ["element"] = "PI-1", ["eigenschaften"] = new JsonObject { ["phase"] = "Bauprojekt", ["nordwinkel"] = null } });
        w = (await s.Element("PI-1"))!["werte"]!;
        Assert.Equal("Bauprojekt", w["phase"]!.GetValue<string>());
        Assert.Null(w["nordwinkel"]);
        Assert.Equal("DN", w["rohrbezeichnung"]!.GetValue<string>());

        // Herkunft: der Verlauf kennt den Befehl und den Akteur
        var warum = await s.Abfrage("warum", new JsonObject { ["element"] = "PI-1", ["eigenschaft"] = "phase" });
        Assert.Equal("element_aendern", warum["herkunft"]!["befehl"]!.GetValue<string>());

        // ungültiger Wert (Auswahl): abgelehnt, mit Fehlertext für den Dialog
        var falsch = await s.Befehl("element_aendern", new JsonObject { ["element"] = "PI-1", ["eigenschaften"] = new JsonObject { ["rohrbezeichnung"] = "XX" } }, erwartet: false);
        Assert.False(string.IsNullOrEmpty(falsch["fehler"]?.GetValue<string>()));

        // Rückgängig stellt den vorigen Stand her (ein Befehl)
        await s.Befehl("rueckgaengig");
        w = (await s.Element("PI-1"))!["werte"]!;
        Assert.Null(w["phase"]);
        Assert.Equal(12.5, w["nordwinkel"]!.GetValue<double>());
    }

    [Fact]
    public async Task Listen_KollisionenUndAussparungsliste_LiefernDieZeilenDerListenansicht_ListenAnsichtenLassenSichAnlegen()
    {
        using var s = new Server();
        await s.Anlegen();
        var ids = (await s.Elemente()).ToDictionary(e => e!["kennzeichen"]!.GetValue<string>(), e => e!["id"]!.GetValue<string>());
        await s.Befehl("element_anlegen", new JsonObject
        {
            ["klasse"] = "Kollision", ["kennzeichen"] = "KOL-N3-1",
            ["eigenschaften"] = new JsonObject { ["art"] = "hart", ["typ"] = "durchdringung", ["status"] = "neu", ["mass"] = 12.5, ["kennung"] = "n3-1", ["kommentar"] = "Rohr \"verschieben\"; prüfen",
                ["beteiligte"] = new JsonArray(ids["W-EG-S"], ids["W-EG-N"]) },
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = new JsonArray(new JsonArray(3000, 1000, -150)) },
        });
        await s.Befehl("element_anlegen", new JsonObject
        {
            ["klasse"] = "Aussparung", ["kennzeichen"] = "AUS-N3-1", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["form"] = "rechteckig", ["b"] = 440, ["h"] = 240, ["tiefe"] = 200, ["art"] = "aussparung", ["status"] = "vorgeschlagen", ["nummer"] = "A-EG-001", ["kote_uk"] = 1880, ["kote_ok"] = 2120 },
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = new JsonArray(new JsonArray(2000, 0, 2000)) },
        });

        // die Abfragen, die liste.js für Ansicht.liste = kollisionen bzw. aussparungsliste stellt (ohne und mit Geschoss)
        var k = await s.Abfrage("kollisionen");
        Assert.Equal("Kollisionsliste", k["ableitung"]!.GetValue<string>());
        var kz = k["zeilen"]!.AsArray();
        Assert.Single(kz);
        Assert.Equal("KOL-N3-1", kz[0]!["kollision"]!.GetValue<string>());
        Assert.Equal(12.5, kz[0]!["mass_mm"]!.GetValue<double>());
        var a = await s.Abfrage("aussparungsliste", new JsonObject { ["geschoss"] = "EG" });
        Assert.Equal("Aussparungsliste", a["ableitung"]!.GetValue<string>());
        Assert.Equal("A-EG-001", a["zeilen"]![0]!["nummer"]!.GetValue<string>());
        Assert.Equal("440 × 240", a["zeilen"]![0]!["masse"]!.GetValue<string>());

        // «Liste anlegen»: die zwei neuen Listenarten sind gültige Werte der Katalogeigenschaft liste
        foreach (var (kennzeichen, liste) in new[] { ("AN-LI-KOLLISIONEN", "kollisionen"), ("AN-LI-AUSSPARUNGEN", "aussparungsliste") })
        {
            await s.Befehl("element_anlegen", new JsonObject { ["klasse"] = "Ansicht", ["kennzeichen"] = kennzeichen, ["name"] = liste, ["eigenschaften"] = new JsonObject { ["art"] = "liste", ["liste"] = liste } });
            Assert.Equal(liste, (await s.Element(kennzeichen))!["werte"]!["liste"]!.GetValue<string>());
        }
        var ungueltig = await s.Befehl("element_anlegen", new JsonObject { ["klasse"] = "Ansicht", ["kennzeichen"] = "AN-LI-X", ["eigenschaften"] = new JsonObject { ["art"] = "liste", ["liste"] = "gibtsnicht" } }, erwartet: false);
        Assert.False(string.IsNullOrEmpty(ungueltig["fehler"]?.GetValue<string>()));
    }
}
