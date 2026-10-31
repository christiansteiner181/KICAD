using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Hlks.Kern;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Hlks.Web.Tests;

/// <summary>
/// REST-Schnittstelle des Arbeitsplatz-Servers (dünne Hülle über der Kern-Tür). Jeder Test hat seine eigene Server-
/// Instanz (der Server hält genau ein Projekt) und einen eigenen Temp-Ordner mit «ä» und Leerzeichen im Pfad.
/// </summary>
public class ApiTests
{
    /// <summary>Server + Client + Temp-Ordner; Dispose schliesst das Projekt, beendet den Server und räumt auf.</summary>
    sealed class Server : IDisposable
    {
        readonly WebApplicationFactory<Program> basis = new();
        readonly WebApplicationFactory<Program> fabrik;
        public HttpClient Http { get; }
        public string Wurzel { get; } = Path.Combine(Path.GetTempPath(), "hlks_api_" + Guid.NewGuid().ToString("N")[..10]);
        public string Ordner => Path.Combine(Wurzel, "c_Pläne und Modelle", "e_KICAD");

        public Server()
        {
            // Testprojekte nicht in die Liste «Zuletzt geöffnet» des Benutzers schreiben
            var e = new Einstellungen("Test", "testpc", Katalog.StandardOrdner(), null) { ZuletztMerken = false };
            fabrik = basis.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton(e)));
            Http = fabrik.CreateClient();
        }

        public async Task<(HttpStatusCode Code, string Text, HttpResponseMessage Antwort)> Roh(HttpMethod m, string pfad, string? koerper = null)
        {
            var req = new HttpRequestMessage(m, pfad);
            if (koerper is not null) req.Content = new StringContent(koerper, Encoding.UTF8, "application/json");
            var a = await Http.SendAsync(req);
            return (a.StatusCode, await a.Content.ReadAsStringAsync(), a);
        }

        public async Task<(HttpStatusCode Code, JsonNode? Json)> Post(string pfad, object? koerper = null)
        {
            var text = koerper switch { null => "{}", string s => s, JsonNode n => n.ToJsonString(), _ => Hlks.Kern.Json.Text(koerper) };
            var (code, antwort, _) = await Roh(HttpMethod.Post, pfad, text);
            return (code, antwort.Length == 0 ? null : JsonNode.Parse(antwort));
        }

        public async Task<(HttpStatusCode Code, JsonNode? Json)> Get(string pfad)
        {
            var (code, antwort, _) = await Roh(HttpMethod.Get, pfad);
            return (code, antwort.Length == 0 ? null : JsonNode.Parse(antwort));
        }

        public async Task<JsonNode> Anlegen(bool beispiel = true, string name = "MFH Musterweg")
        {
            var (code, j) = await Post("/api/projekt/anlegen", new { ordner = Ordner, name, beispiel });
            Assert.Equal(HttpStatusCode.OK, code);
            return j!;
        }

        public async Task<string> Pruefsumme() => (await Get("/api/status")).Json!["stand"]!["pruefsumme"]!.GetValue<string>();

        public void Dispose()
        {
            try { Http.PostAsync("/api/projekt/schliessen", new StringContent("{}", System.Text.Encoding.UTF8, "application/json")).GetAwaiter().GetResult(); } catch { /* egal */ }
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

    static JsonObject Par(string json) => JsonNode.Parse(json)!.AsObject();
    static string Z(JsonNode? n) => n?.GetValue<string>() ?? "";
    static void Pruefe(HttpStatusCode code) => Assert.True((int)code < 500, $"Serverfehler {(int)code}");

    // ------------------------------------------------------------------ 1. Oberfläche (statische Dateien)

    [Fact]
    public async Task Startseite_LiefertIndexHtml()
    {
        using var s = new Server();
        var (code, html, a) = await s.Roh(HttpMethod.Get, "/");
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.StartsWith("text/html", a.Content.Headers.ContentType!.MediaType + "");
        Assert.Contains("<title>HLKS-CAD</title>", html);
        Assert.Contains("js/app.js", html);
        Assert.Equal("no-cache", a.Headers.CacheControl?.ToString());
    }

    static readonly Regex Verweis = new(@"[""'](\.{1,2}/[^""'\s?#]+\.(?:js|css))[""']", RegexOptions.Compiled);
    static readonly Regex HtmlVerweis = new(@"(?:href|src)=""([^"":]+\.(?:js|css))""", RegexOptions.Compiled);

    [Fact]
    public async Task Oberflaeche_AlleReferenziertenJsUndCssDateienWerdenMitPassendemInhaltstypGeliefert()
    {
        using var s = new Server();
        var index = await s.Http.GetStringAsync("/");

        var gesehen = new HashSet<string>(StringComparer.Ordinal);
        var offen = new Queue<string>();
        void Merke(string url) { if (gesehen.Add(url)) offen.Enqueue(url); }

        foreach (Match m in HtmlVerweis.Matches(index)) Merke("/" + m.Groups[1].Value);
        foreach (Match m in Verweis.Matches(index)) Merke("/" + m.Groups[1].Value.TrimStart('.', '/'));
        foreach (var vertrag in new[] { "/js/ansichten/filter.js", "/js/oberflaeche/aktionen.js", "/js/oberflaeche/paletten.js" })   // W1: paletten.js ersetzt layout.js
            Merke(vertrag);                                         // Dateien aus OBERFLAECHE.md, auch wenn noch nichts sie importiert
        Merke("/lib/three/three.module.js");
        Merke("/lib/three/addons/controls/OrbitControls.js");

        while (offen.Count > 0)
        {
            var url = offen.Dequeue();
            var (code, text, a) = await s.Roh(HttpMethod.Get, url);
            Assert.True(code == HttpStatusCode.OK, $"{url}: {(int)code}");
            var typ = a.Content.Headers.ContentType?.MediaType ?? "";
            if (url.EndsWith(".css")) Assert.Equal("text/css", typ);
            else Assert.Contains("javascript", typ);                                   // text/javascript oder application/javascript
            Assert.False(string.IsNullOrWhiteSpace(text), $"{url}: leer");
            Assert.DoesNotContain("<html", text, StringComparison.OrdinalIgnoreCase);  // keine Fehlerseite
            if (!url.EndsWith(".js") || url.StartsWith("/lib/")) continue;
            var dir = url[..(url.LastIndexOf('/') + 1)];
            foreach (Match m in Verweis.Matches(text))
                Merke(Aufloesen(dir, m.Groups[1].Value));
        }
        // Gerüst, Bedienung, Ansichten, Bibliothek sind dabei
        foreach (var erwartet in new[] { "/js/app.js", "/js/api.js", "/js/zustand.js", "/js/ereignisse.js", "/js/fenster.js",
                                         "/js/ansichten/filter.js", "/js/ansichten/grundriss.js", "/js/ansichten/ansicht3d.js", "/js/ansichten/liste.js",
                                         "/js/oberflaeche/aktionen.js", "/js/oberflaeche/menueband.js",
                                         "/css/hlks.css", "/css/oberflaeche.css", "/css/ansichten.css",
                                         "/lib/three/three.module.js", "/lib/three/addons/controls/OrbitControls.js" })
            Assert.Contains(erwartet, gesehen);
    }

    static string Aufloesen(string verzeichnis, string relativ)
    {
        var teile = verzeichnis.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
        foreach (var t in relativ.Split('/'))
        {
            if (t == "." || t == "") continue;
            if (t == "..") { if (teile.Count > 0) teile.RemoveAt(teile.Count - 1); }
            else teile.Add(t);
        }
        return "/" + string.Join('/', teile);
    }

    [Fact]
    public async Task Oberflaeche_UnbekannteDateiGibt404()
    {
        using var s = new Server();
        var (code, _, _) = await s.Roh(HttpMethod.Get, "/js/gibt-es-nicht.js");
        Assert.Equal(HttpStatusCode.NotFound, code);
    }

    // ------------------------------------------------------------------ 2. Projekt öffnen, anlegen, schliessen

    [Fact]
    public async Task Status_OhneProjektIstNichtOffen()
    {
        using var s = new Server();
        var (code, j) = await s.Get("/api/status");
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.False(j!["offen"]!.GetValue<bool>());
        Assert.Null(j["projekt"]);
        Assert.Null(j["stand"]);
        Assert.False(string.IsNullOrEmpty(Z(j["akteur"])));
    }

    [Fact]
    public async Task Anlegen_MitBeispielOeffnetDasProjekt()
    {
        using var s = new Server();
        var j = await s.Anlegen(beispiel: true);
        Assert.True(j["offen"]!.GetValue<bool>());
        Assert.StartsWith("MFH Musterweg", Z(j["projekt"]));
        Assert.Equal(s.Ordner, Z(j["ort"]));
        Assert.Equal(187, j["stand"]!["elemente"]!.GetValue<int>());
        Assert.Equal(142, j["stand"]!["verbindungen"]!.GetValue<int>());
        Assert.True(File.Exists(Path.Combine(s.Ordner, "projekt.json")));
        var status = (await s.Get("/api/status")).Json!;
        Assert.True(status["offen"]!.GetValue<bool>());
        Assert.StartsWith("MFH Musterweg", Z(status["projekt"]));
    }

    [Fact]
    public async Task Anlegen_LeeresProjektMitNameUndStandardname()
    {
        using var s = new Server();
        var j = await s.Anlegen(beispiel: false, name: "Mein Neubau");
        Assert.Equal("Mein Neubau", Z(j["projekt"]));
        Assert.Equal(0, j["stand"]!["elemente"]!.GetValue<int>());
        await s.Post("/api/projekt/schliessen");

        using var s2 = new Server();
        var (code, j2) = await s2.Post("/api/projekt/anlegen", new { ordner = s2.Ordner });
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Equal("Neues Projekt", Z(j2!["projekt"]));
    }

    [Fact]
    public async Task Anlegen_ZweitesMalImSelbenOrdnerGibt400MitFehlerUndVorschlag()
    {
        using var s = new Server();
        await s.Anlegen();
        var (code, j) = await s.Post("/api/projekt/anlegen", new { ordner = s.Ordner, name = "Nochmal", beispiel = false });
        Assert.Equal(HttpStatusCode.BadRequest, code);
        Assert.False(string.IsNullOrWhiteSpace(Z(j!["fehler"])));
        Assert.False(string.IsNullOrWhiteSpace(Z(j["vorschlag"])));
        // auch mit Beispiel
        var (code2, j2) = await s.Post("/api/projekt/anlegen", new { ordner = s.Ordner, name = "Nochmal", beispiel = true });
        Assert.Equal(HttpStatusCode.BadRequest, code2);
        Assert.False(string.IsNullOrWhiteSpace(Z(j2!["fehler"])));
    }

    [Fact]
    public async Task Oeffnen_AufDenselbenOrdnerLiefertDasProjekt()
    {
        using var s = new Server();
        await s.Anlegen();
        var vorher = await s.Pruefsumme();
        await s.Post("/api/projekt/schliessen");
        var (code, j) = await s.Post("/api/projekt/oeffnen", new { ordner = s.Ordner });
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.True(j!["offen"]!.GetValue<bool>());
        Assert.StartsWith("MFH Musterweg", Z(j["projekt"]));
        Assert.Equal(vorher, await s.Pruefsumme());                 // gleicher Stand nach dem Neuladen

        // und nochmal, ohne vorher zu schliessen
        var (code2, j2) = await s.Post("/api/projekt/oeffnen", new { ordner = s.Ordner });
        Assert.Equal(HttpStatusCode.OK, code2);
        Assert.True(j2!["offen"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Oeffnen_NichtExistierenderOrdnerGibtFehlerKein500()
    {
        using var s = new Server();
        var (code, j) = await s.Post("/api/projekt/oeffnen", new { ordner = Path.Combine(s.Wurzel, "gibt es nicht", "e_KICAD") });
        Assert.Equal(HttpStatusCode.BadRequest, code);
        Assert.Contains("projekt.json", Z(j!["fehler"]));
        Assert.False((await s.Get("/api/status")).Json!["offen"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Oeffnen_NichtExistierenderOrdnerGibtAuchEinenVorschlag()
    {
        using var s = new Server();
        var (code, j) = await s.Post("/api/projekt/oeffnen", new { ordner = Path.Combine(s.Wurzel, "gibt es nicht", "e_KICAD") });
        Assert.Equal(HttpStatusCode.BadRequest, code);
        Assert.False(string.IsNullOrWhiteSpace(Z(j!["vorschlag"])), j.ToJsonString());
    }

    [Fact]
    public async Task Oeffnen_LeererOrdnerGibt400Statt500()
    {
        using var s = new Server();
        var (code, j) = await s.Post("/api/projekt/oeffnen", new { ordner = "" });
        Assert.Equal(HttpStatusCode.BadRequest, code);
        Assert.False(string.IsNullOrWhiteSpace(Z(j!["fehler"])));
        var (code2, _) = await s.Post("/api/projekt/anlegen", new { ordner = "  ", name = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, code2);
    }

    [Fact]
    public async Task Oeffnen_FehlschlagLaesstDasOffeneProjektOffen()
    {
        using var s = new Server();
        await s.Anlegen();
        var start = await s.Pruefsumme();
        var (code, _) = await s.Post("/api/projekt/oeffnen", new { ordner = Path.Combine(s.Wurzel, "gibt es nicht") });
        Assert.Equal(HttpStatusCode.BadRequest, code);
        var status = (await s.Get("/api/status")).Json!;
        Assert.True(status["offen"]!.GetValue<bool>(), "das bisherige Projekt wurde durch den Fehlschlag geschlossen");
        Assert.Equal(start, await s.Pruefsumme());
    }

    [Fact]
    public async Task Oeffnen_OrdnerOhneProjektJsonGibtFehler()
    {
        using var s = new Server();
        Directory.CreateDirectory(s.Ordner);
        var (code, j) = await s.Post("/api/projekt/oeffnen", new { ordner = s.Ordner });
        Assert.Equal(HttpStatusCode.BadRequest, code);
        Assert.Contains("projekt.json", Z(j!["fehler"]));
    }

    [Fact]
    public async Task Oeffnen_OhneOrdnerParameterGibt400()
    {
        using var s = new Server();
        var (code, j) = await s.Post("/api/projekt/oeffnen", new { });
        Assert.Equal(HttpStatusCode.BadRequest, code);
        Assert.Contains("ordner", Z(j!["fehler"]));
        var (code2, _) = await s.Post("/api/projekt/anlegen", new { name = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, code2);
    }

    [Fact]
    public async Task Schliessen_SetztOffenFalseUndModellGibt409()
    {
        using var s = new Server();
        await s.Anlegen();
        var (code, j) = await s.Post("/api/projekt/schliessen");
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.False(j!["offen"]!.GetValue<bool>());
        Assert.False((await s.Get("/api/status")).Json!["offen"]!.GetValue<bool>());

        var (mc, mj) = await s.Get("/api/modell");
        Assert.Equal(HttpStatusCode.Conflict, mc);
        Assert.Contains("kein Projekt offen", Z(mj!["fehler"]));
        Assert.False(string.IsNullOrWhiteSpace(Z(mj["vorschlag"])));

        // zweites Schliessen ist harmlos
        Assert.Equal(HttpStatusCode.OK, (await s.Post("/api/projekt/schliessen")).Code);
    }

    [Fact]
    public async Task OhneProjekt_AlleProjektEndpunkteGeben409()
    {
        using var s = new Server();
        Assert.Equal(HttpStatusCode.Conflict, (await s.Get("/api/modell")).Code);
        Assert.Equal(HttpStatusCode.Conflict, (await s.Post("/api/elemente", new { ids = new[] { "x" } })).Code);
        Assert.Equal(HttpStatusCode.Conflict, (await s.Post("/api/abfrage/projekt_ueberblick")).Code);
        Assert.Equal(HttpStatusCode.Conflict, (await s.Post("/api/befehl/element_aendern", new { element = "E.02", name = "x" })).Code);
        Assert.Equal(HttpStatusCode.Conflict, (await s.Post("/api/befehl/rueckgaengig")).Code);
        Assert.Equal(HttpStatusCode.Conflict, (await s.Post("/api/freigeben")).Code);
    }

    // ------------------------------------------------------------------ 3. Modell und Elemente

    [Fact]
    public async Task Modell_Liefert175ElementeUnd138Verbindungen()
    {
        using var s = new Server();
        await s.Anlegen();
        var (code, j) = await s.Get("/api/modell");
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.StartsWith("MFH Musterweg", Z(j!["projekt"]));
        Assert.Equal(187, j["elemente"]!.AsArray().Count);
        Assert.Equal(142, j["verbindungen"]!.AsArray().Count);
        var wc = j["elemente"]!.AsArray().First(e => Z(e!["kennzeichen"]) == "WC-E.02")!;
        Assert.Equal("verbraucher", Z(wc["rolle"]));
        Assert.Equal("E.02", Z(wc["raum"]));
        Assert.Equal("EG", Z(wc["geschoss"]));
        Assert.Equal(await s.Pruefsumme(), Z(j["stand"]!["pruefsumme"]));
    }

    [Fact]
    public async Task Elemente_NurDieGenanntenUnbekannteIdUnterGeloescht()
    {
        using var s = new Server();
        await s.Anlegen();
        var modell = (await s.Get("/api/modell")).Json!;
        var ids = modell["elemente"]!.AsArray().Take(3).Select(e => Z(e!["id"])).ToList();

        var (code, j) = await s.Post("/api/elemente", new { ids = ids.Append("gibt-es-nicht").Append(ids[0]).ToArray() });
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Equal(3, j!["elemente"]!.AsArray().Count);
        Assert.Equal(ids.Order(), j["elemente"]!.AsArray().Select(e => Z(e!["id"])).Order());
        Assert.Equal(["gibt-es-nicht"], j["geloescht"]!.AsArray().Select(x => Z(x)).ToArray());
        Assert.NotNull(j["stand"]);
    }

    [Fact]
    public async Task Elemente_OhneIdsGibtLeereListen()
    {
        using var s = new Server();
        await s.Anlegen();
        var (code, j) = await s.Post("/api/elemente", new { });
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Empty(j!["elemente"]!.AsArray());
        Assert.Empty(j["geloescht"]!.AsArray());
    }

    [Fact]
    public async Task Elemente_NichtTextIdsGeben400Statt500()
    {
        using var s = new Server();
        await s.Anlegen();
        var (code, _, _) = await s.Roh(HttpMethod.Post, "/api/elemente", "{\"ids\":[1,2,null]}");
        Assert.True((int)code < 500, $"Status {(int)code}");
    }

    // ------------------------------------------------------------------ 4. Abfragen

    // Vor der Tabelle deklarieren: statische Felder werden in Textreihenfolge belegt (sonst steht in plan_pdf ein leerer Pfad)
    static readonly string PlanPdfDatei = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "api-test-" + Guid.NewGuid().ToString("N")[..8] + ".pdf").Replace('\\', '/');
    static readonly string PlansatzOrdner = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "api-plansatz-" + Guid.NewGuid().ToString("N")[..8]).Replace('\\', '/');
    static readonly Dictionary<string, string> AbfrageParameter = new()
    {
        ["faehigkeiten"] = "{}",
        ["projekt_ueberblick"] = "{}",
        ["element_lesen"] = """{"element":"WT-E.02"}""",
        ["elemente_suchen"] = """{"klasse":"Apparat","geschoss":"EG","seite":1,"proSeite":5}""",
        ["system_lesen"] = """{"system":"SW"}""",
        ["strang_text"] = """{"strang":"SW-F2"}""",
        ["netz_folgen"] = """{"von":"MV-1","richtung":"stromab","medium":"KW"}""",
        ["raum_inhalt"] = """{"raum":"E.02"}""",
        ["mengen"] = """{"system":"SW"}""",
        ["raumliste"] = """{"geschoss":"1.OG"}""",
        ["strangliste"] = "{}",
        ["pruefen"] = "{}",
        ["warum"] = """{"element":"WT-E.02","eigenschaft":"raum"}""",
        ["aenderungen"] = """{"anzahl":5}""",
        ["ausgaben"] = """{"anzahl":5}""",
        ["katalog"] = "{}",
        ["datenpunkte"] = "{}",
        ["zeitreihe"] = """{"datenpunkt":"DP-WZ1-VOL","von":"2026-10-19T00:00","bis":"2026-10-20T00:00","aufloesungMinuten":120}""",
        ["plan_pdf"] = "{\"planblatt\":\"PB-1\",\"datei\":\"" + PlanPdfDatei + "\"}",   // Planblatt legt der Test vorher an
        ["ifc_pruefen"] = "{\"datei\":\"" + IfcTestdatei("minimal_ifc4.ifc") + "\"}",
        ["aufbau_rechnen"] = """{"aufbau":"AUF-1"}""",   // Aufbau legt der Test vorher an (das Beispielprojekt hat keinen)
        ["materialien"] = "{}",
        ["huelle_pruefen"] = "{}",
        ["hkap_verbindung"] = """{"url":"http://127.0.0.1:9"}""",   // Modul 1d: nie die echte HKAP (nicht erreichbar = Antwort)
        ["hkap_abgleich"] = "{}",                                    // nicht verknüpft = Antwort
        ["raumgrenzen"] = """{"geschoss":"EG"}""",                   // Modul 1c, B1
        ["huellbauteile"] = "{}",
        ["familien"] = "{}",                                         // Modul 2, B1: Testfamilie (fiktiv) legt der Test vorher an
        ["familie"] = """{"familie":"FAM-1"}""",
        ["anschlusslagen"] = """{"element":"HK-T1"}""",
        ["familie_ansicht"] = """{"familie":"FAM-1","typ":"22-600-1000","ansicht":"vorne","detailgrad":"mittel"}""",
        ["familien_bibliothek"] = "{}",
        ["unterlagen_lesen"] = "{\"unterlagen\":[\"" + IfcTestdatei("unterlagen/beispiel.pdf") + "\"]}",   // Modul 2, B2: kleines Beispiel-PDF
        ["befunde"] = "{\"unterlage\":\"" + IfcTestdatei("unterlagen/beispiel.pdf") + "\",\"art\":\"wort\"}",
        ["unterlage_bild"] = "{\"unterlage\":\"" + IfcTestdatei("unterlagen/beispiel.pdf") + "\",\"breite\":200}",
        ["familie_pruefen"] = """{"familie":"FAM-1"}""",                  // Nachtrag 21.10.2026: Prüfbefunde der Testfamilie
        ["familie_masse"] = """{"familie":"FAM-1"}""",                    // Modul 2, B4: Testfamilie ohne Massbezüge = leere Liste mit Hinweis
        ["familie_vergleichen"] = "{\"familie\":\"FAM-1\",\"typ\":\"22-600-1000\",\"ansicht\":\"vorne\",\"breite\":300,\"unterlage\":\"" + IfcTestdatei("unterlagen/beispiel.pdf")
            + "\",\"passpunkte\":[{\"familie\":[0,0],\"unterlage\":[100,100]},{\"familie\":[1000,0],\"unterlage\":[300,100]}]}",
        ["leitungsserie"] = """{"familie":"fam-test-rohrserie"}""",   // Modul 3, B1: fiktive Testrohrserie legt der Test vorher an
        ["abzweigwinkel"] = """{"familie":"fam-test-rohrserie","groesse":"DN 20"}""",   // Nachtrag Modul 3, 21.10.2026: Abzweigwinkel der Testrohrserie
        ["materialliste"] = """{"system":"SW"}""",                      // Modul 3, B4
        ["schema"] = """{"ansicht":"AN-SCHEMA-SAN"}""",                  // Modul 3, B3: Schema-Ansicht des Beispielprojekts
        ["leitungszug"] = """{"leitung":"KW-V1-3"}""",                  // Modul 3, B2: Zug einer Polylinie des Beispielprojekts
        ["verlegung"] = """{"art":"einlage"}""",                         // Modul 3, Nachtrag B8c: Beispielprojekt ohne Decken = keine Einlagen
        ["bcf_export"] = "{\"datei\":\"" + BcfDatei() + "\"}",          // Modul 3b, B6: Kollision legt der Test vorher an
        ["bcf_lesen"] = "{\"datei\":\"" + BcfDatei() + "\"}",           // Modul 3b, B6: Datei schreibt der Test vorher mit bcf_export
        ["vorgaben"] = "{}",                                            // Modul 3b, B2: ohne Vorgaben = Antwort mit fehlenden Werten
        ["kollisionen"] = """{"status":"neu"}""",                       // Modul 3b, B2
        ["typmaterial"] = """{"betroffen":false}""",                    // Modul 3b, Nachtrag B3d: leere Typenliste = Antwort mit Hinweis
        ["aussparungsliste"] = """{"geschoss":"EG"}""",                 // Modul 3b, B3 (der Test legt vorher eine Aussparung an: siehe aussparungen_ifc)
        ["aussparungen_ifc"] = "{\"datei\":\"" + AussparungIfcDatei() + "\"}",   // Modul 3b, B4b: freigegebene Testaussparung (fiktiv) legt der Test vorher an
        ["plannummer_vorschlag"] = """{"gewerk":"heizung","geschoss":"EG"}""",   // Modul 7 Teil A
        ["plansaetze"] = "{}",                                          // Serien-PDF
        ["plansatz_pdf"] = "{\"plansatz\":\"allgemein\",\"ordner\":\"" + PlansatzOrdner + "\",\"ueberschreiben\":true}",   // Planblatt T-1 (ohne Fachbuchstabe = allgemein) legt der Test vorher an
        ["normen_suchen"] = """{"frage":"Mindestgefälle Prüfleitung"}""",   // Modul 3c: Test-PDF (erfunden), HLKS_NORMEN setzt der Test kurz
        ["norm_lesen"] = """{"datei":"SIA 9999:2026","seite":3}""",
        ["sanitaer_belastung"] = """{"anlage":"A-SAN"}""",              // Modul 4 Teil A: Belastungssummen des Beispielprojekts
        ["sanitaer_zusammenstellung"] = """{"anlage":"A-SAN"}""",
        ["sanitaer_katalog"] = "{}",
        ["lueftung_varianten"] = "{}",                                 // Modul 6 Teil A: Beispiel ohne Luftmengen → nur Befund
        ["ifc_export"] = "{\"datei\":\"" + IfcExportDatei() + "\"}",   // Modul 7 Teil B2: IFC-Export des Beispielprojekts (Bericht daneben)
        ["planwinkel"] = """{"ansicht":"AN-GR-EG"}""",                   // N9: Planwinkel GEB-1 und Ausrichtung des Grundrisses EG
        ["raum_begehen"] = """{"raum":"E.02"}""",                         // N15: Standort für «Raum begehen» im Raum E.02
        ["schnitthoehe_sia"] = """{"ansicht":"AN-GR-EG"}""",                // N21: Schnitthöhe des Grundrisses EG nach SIA 400
    };
    static string IfcExportDatei() => Path.Combine(Path.GetTempPath(), "HLKS_CAD", $"api-test-{Environment.ProcessId}-export.ifc").Replace('\\', '/');
    static void IfcExportAufraeumen()
    {
        var b = IfcExportDatei()[..^4];
        foreach (var x in new[] { IfcExportDatei(), b + ".bericht.json", b + ".bericht.md" }) File.Delete(x);
    }
    static string AussparungIfcDatei() => Path.Combine(Path.GetTempPath(), "HLKS_CAD", $"api-test-{Environment.ProcessId}-aussparungen.ifc").Replace('\\', '/');
    static string BcfDatei() => Path.Combine(Path.GetTempPath(), "HLKS_CAD", $"api-test-{Environment.ProcessId}.bcf").Replace('\\', '/');
    /// <summary>Kleine IFC-Testdatei aus HLKS_CAD/tests/daten (Modul 1b), Pfad mit /.</summary>
    static string IfcTestdatei(string name)
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            foreach (var p in new[] { Path.Combine(d.FullName, "tests", "daten", name), Path.Combine(d.FullName, "HLKS_CAD", "tests", "daten", name) })
                if (File.Exists(p)) return p.Replace('\\', '/');
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive", "Claude_Code", "HLKS_CAD", "tests", "daten", name).Replace('\\', '/');
    }
    static readonly string[] Textabfragen = ["system_lesen", "strang_text", "norm_lesen"];

    /// <summary>Modul 4 Teil A: Sanitär-Zusammenstellung, Belastungssummen und Apparateart über REST (dieselbe Tür wie Kern und MCP);
    /// Beispielprojekt «MFH Musterweg» (fiktiv), Werte aus dem Katalog sanitaer_belastungswerte.json.</summary>
    [Fact]
    public async Task Sanitaer_ZusammenstellungBelastungUndApparateartUeberRest()
    {
        using var s = new Server();
        await s.Anlegen();
        var (code, z) = await s.Post("/api/abfrage/sanitaer_zusammenstellung", """{"anlage":"A-SAN"}""");
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Equal(16, z!["apparate"]!.GetValue<int>());
        Assert.Equal(42, z["totalLu"]!.GetValue<double>(), 9);
        Assert.Equal(16.7, z["summeDu"]!.GetValue<double>(), 9);
        var (_, l) = await s.Post("/api/abfrage/sanitaer_belastung", """{"leitung":"KW-V1-0"}""");
        Assert.Equal(24, l!["summeLuKw"]!.GetValue<double>());
        var (_, e) = await s.Post("/api/abfrage/element_lesen", """{"element":"WC-E.02"}""");
        Assert.Equal("wc_75", e!["sanitaer"]!["art"]!.GetValue<string>());
        Assert.Equal(2, e["abgeleitet"]!["du"]!.GetValue<double>());
        var (bc, b) = await s.Post("/api/befehl/element_aendern", """{"element":"WC-E.02","eigenschaften":{"sanitaerArt":"wc_90"}}""");
        Assert.Equal(HttpStatusCode.OK, bc);
        Assert.True(b!["angenommen"]!.GetValue<bool>(), b.ToJsonString());
        var (_, z2) = await s.Post("/api/abfrage/sanitaer_zusammenstellung", """{"anlage":"A-SAN"}""");
        Assert.Equal(16.7 + 0.5, z2!["summeDu"]!.GetValue<double>(), 9);   // ein WC 7.5 l (DU 2) → 9 l (DU 2.5)
        var (fc, f) = await s.Post("/api/abfrage/sanitaer_zusammenstellung", """{"anlage":"WC-E.02"}""");
        Assert.True((int)fc is >= 400 and < 500, $"Status {(int)fc}");
        Assert.Contains("keine Anlage", f!["fehler"]!.GetValue<string>());
    }

    [Fact]
    public void Abfragen_ParameterTabelleDecktGenauDieListeAb()
    {
        Assert.Equal(Abfragen.Liste.Select(a => a.Name).Order(), AbfrageParameter.Keys.Order());
    }

    [Fact]
    public async Task Abfrage_JedeAusDerListeLiefertJsonBzwTextOhneFehler()
    {
        using var s = new Server();
        await s.Anlegen();
        await s.Post("/api/befehl/planblatt_anlegen", """{"nummer":"T-1","titel":"Test"}""");   // für plan_pdf
        await s.Post("/api/befehl/material_anlegen", """{"name":"Testmaterial","werte":{"lambda":{"wert":0.5,"quelle":"Testwert"}}}""");   // für aufbau_rechnen (Testwert)
        await s.Post("/api/befehl/aufbau_anlegen", """{"name":"Testaufbau","art":"wand","schichten":[{"material":"MAT-1","dicke":200}]}""");
        await s.Post("/api/befehl/familie_entwurf", "{\"definition\":" + File.ReadAllText(IfcTestdatei("familien/test_heizkoerper.familie.json")) + "}");   // für die Familienabfragen (Modul 2)
        await s.Post("/api/befehl/familie_platzieren", """{"familie":"FAM-1","typ":"22-600-1000","punkt":[5000,3000],"geschoss":"EG","kennzeichen":"HK-T1"}""");
        await s.Post("/api/befehl/familie_entwurf", "{\"definition\":" + File.ReadAllText(IfcTestdatei("familien/test_rohrserie.familie.json")) + "}");   // für leitungsserie (Modul 3)
        await s.Post("/api/befehl/element_anlegen", """{"klasse":"Kollision","kennzeichen":"KOL-T1","eigenschaften":{"art":"hart","beteiligte":["WT-E.02"],"mass":10},"geometrie":{"art":"punkt","punkte":[[1000,1000,500]]}}""");   // für bcf_export, bcf_lesen (Modul 3b, B6)
        await s.Post("/api/abfrage/bcf_export", AbfrageParameter["bcf_export"]);
        await s.Post("/api/befehl/element_anlegen", """{"klasse":"Aussparung","kennzeichen":"AUS-T1","geschoss":"EG","eigenschaften":{"form":"rund","d":95,"tiefe":200,"achse":[0,1,0],"art":"kernbohrung","status":"freigegeben","nummer":"A-EG-001"},"geometrie":{"art":"punkt","punkte":[[1000,1000,1000]]}}""");   // für aussparungen_ifc (Modul 3b, B4b; Testwerte)
        try {
        foreach (var a in Abfragen.Liste)
        {
            // Modul 3c: die Normensuche liest HLKS_NORMEN – nur während des Aufrufs auf eine Kopie der Test-PDF (nie W:; nur die Normensuche liest die Variable)
            var normen = a.Name is "normen_suchen" or "norm_lesen" ? GespraechServer.NormenKopie() : null;
            using var umgebung = normen is null ? null : Hlks.Tests.Gemeinsam.TestUmgebung.Setzen(("HLKS_NORMEN", normen));
            var (code, text, antwort) = await s.Roh(HttpMethod.Post, "/api/abfrage/" + a.Name, AbfrageParameter[a.Name]);
            if (normen is not null) GespraechServer.Loeschen(normen);
            Assert.True(code == HttpStatusCode.OK, $"{a.Name}: {(int)code} {text}");
            Assert.False(string.IsNullOrWhiteSpace(text), $"{a.Name}: leer");
            var typ = antwort.Content.Headers.ContentType!.MediaType;
            if (Textabfragen.Contains(a.Name))
            {
                Assert.Equal("text/plain", typ);
                Assert.Equal("utf-8", antwort.Content.Headers.ContentType!.CharSet);
                Assert.False(text.StartsWith('{'), $"{a.Name}: Text erwartet");
            }
            else
            {
                Assert.Equal("application/json", typ);
                var j = JsonNode.Parse(text);                                       // gültiges JSON
                Assert.NotNull(j);
                Assert.Null(j!.AsObject()["fehler"]);
            }
        }
        } finally { File.Delete(PlanPdfDatei); File.Delete(BcfDatei()); File.Delete(AussparungIfcDatei()); IfcExportAufraeumen(); try { Directory.Delete(PlansatzOrdner, true); } catch (IOException) { } }   // Ausgabe von plan_pdf, bcf_export, aussparungen_ifc, ifc_export und plansatz_pdf aufräumen
    }

    [Fact]
    public async Task Abfrage_InhaltEntsprichtDemKern()
    {
        using var s = new Server();
        await s.Anlegen();
        var (_, ueberblick) = await s.Post("/api/abfrage/projekt_ueberblick");
        Assert.StartsWith("MFH Musterweg", Z(ueberblick!["projekt"]));
        Assert.Equal(await s.Pruefsumme(), Z(ueberblick["modellStand"]!["pruefsumme"]));

        var (_, sl) = await s.Post("/api/abfrage/strangliste");
        Assert.Equal(9, sl!["zeilen"]!.AsArray().Count);   // 7 Sanitär-Stränge + 2 Kälte-Stränge (Beispielprojekt)

        var (_, _, st) = await s.Roh(HttpMethod.Post, "/api/abfrage/strang_text", """{"strang":"SW-F2"}""");
        Assert.Contains("SW-F2", await st.Content.ReadAsStringAsync());

        // Umlaute kommen unmaskiert und als UTF-8
        var (_, roh, _) = await s.Roh(HttpMethod.Post, "/api/abfrage/raumliste", "{}");
        Assert.DoesNotContain("\\u", roh);

        // Körper darf fehlen
        var (c, _, _) = await s.Roh(HttpMethod.Post, "/api/abfrage/pruefen");
        Assert.Equal(HttpStatusCode.OK, c);
    }

    [Fact]
    public async Task Abfrage_UnbekannterNameGibt400MitFehlerUndVorschlag()
    {
        using var s = new Server();
        await s.Anlegen();
        var (code, j) = await s.Post("/api/abfrage/gibt_es_nicht");
        Assert.Equal(HttpStatusCode.BadRequest, code);
        Assert.Contains("gibt_es_nicht", Z(j!["fehler"]));
        var vorschlag = Z(j["vorschlag"]);
        foreach (var a in Abfragen.Liste) Assert.Contains(a.Name, vorschlag);
    }

    [Fact]
    public async Task Abfrage_FachlicherFehlerGibt400MitVorschlag()
    {
        using var s = new Server();
        await s.Anlegen();
        var (code, j) = await s.Post("/api/abfrage/element_lesen", new { element = "GIBT-ES-NICHT" });
        Assert.Equal(HttpStatusCode.BadRequest, code);
        Assert.Contains("GIBT-ES-NICHT", Z(j!["fehler"]));
        Assert.False(string.IsNullOrWhiteSpace(Z(j["vorschlag"])));

        var (c2, j2) = await s.Post("/api/abfrage/element_lesen", new { });             // Pflichtparameter fehlt
        Assert.Equal(HttpStatusCode.BadRequest, c2);
        Assert.Contains("element", Z(j2!["fehler"]));

        var (c3, _) = await s.Post("/api/abfrage/strang_text", new { strang = "WT-E.02" });   // kein Strang
        Assert.Equal(HttpStatusCode.BadRequest, c3);

        var (c4, _) = await s.Post("/api/abfrage/zeitreihe", new { datenpunkt = "DP-WZ1-VOL", von = "gestern", bis = "heute" });
        Assert.Equal(HttpStatusCode.BadRequest, c4);
    }

    [Theory]
    [InlineData("{kaputt")]
    [InlineData("nicht json")]
    [InlineData("[1,2,3]")]
    [InlineData("\"text\"")]
    public async Task Abfrage_UngueltigerKoerperGibt400(string koerper)
    {
        using var s = new Server();
        await s.Anlegen();
        var (code, text, _) = await s.Roh(HttpMethod.Post, "/api/abfrage/pruefen", koerper);
        Assert.Equal(HttpStatusCode.BadRequest, code);
        var j = JsonNode.Parse(text)!;
        Assert.False(string.IsNullOrWhiteSpace(Z(j["fehler"])));
    }

    [Fact]
    public async Task UngueltigesJson_AuchBeiBefehlenProjektUndElementenGibt400()
    {
        using var s = new Server();
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Roh(HttpMethod.Post, "/api/projekt/oeffnen", "{kaputt")).Code);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Roh(HttpMethod.Post, "/api/projekt/anlegen", "{kaputt")).Code);
        await s.Anlegen();
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Roh(HttpMethod.Post, "/api/befehl/element_aendern", "{kaputt")).Code);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Roh(HttpMethod.Post, "/api/elemente", "{kaputt")).Code);
    }

    // ------------------------------------------------------------------ 5. Befehle

    static object Aendern(string name = "Neuer Name") => new { element = "E.02", name };

    [Fact]
    public async Task Befehl_TrockenlaufAendertNichts()
    {
        using var s = new Server();
        await s.Anlegen();
        var start = await s.Pruefsumme();

        var (code, j) = await s.Post("/api/befehl/element_aendern?trockenlauf=true", Aendern());
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.True(j!["angenommen"]!.GetValue<bool>(), j.ToJsonString());
        Assert.True(j["trockenlauf"]!.GetValue<bool>());
        Assert.NotEmpty(j["aenderungen"]!.AsArray());
        Assert.Equal(start, await s.Pruefsumme());

        var modell = (await s.Get("/api/modell")).Json!;
        Assert.NotEqual("Neuer Name", Z(modell["elemente"]!.AsArray().First(e => Z(e!["kennzeichen"]) == "E.02")!["name"]));

        // ausdrücklich false verhält sich wie ohne
        var (_, j2) = await s.Post("/api/befehl/element_aendern?trockenlauf=false", Aendern());
        Assert.True(j2!["angenommen"]!.GetValue<bool>());
        Assert.False(j2["trockenlauf"]!.GetValue<bool>());
        Assert.NotEqual(start, await s.Pruefsumme());
    }

    [Fact]
    public async Task Befehl_OhneTrockenlaufAendertUndMeldetAenderungen()
    {
        using var s = new Server();
        await s.Anlegen();
        var start = await s.Pruefsumme();

        var (code, j) = await s.Post("/api/befehl/element_aendern", Aendern());
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.True(j!["angenommen"]!.GetValue<bool>(), j.ToJsonString());
        Assert.False(j["trockenlauf"]!.GetValue<bool>());
        Assert.Equal("element_aendern", Z(j["befehl"]));
        Assert.False(string.IsNullOrEmpty(Z(j["paketId"])));
        Assert.NotEmpty(j["aenderungen"]!.AsArray());
        Assert.NotEqual(start, await s.Pruefsumme());

        var (_, ersatz) = await s.Post("/api/elemente", new { ids = new[] { Z((await s.Post("/api/abfrage/element_lesen", new { element = "E.02" })).Json!["id"]) } });
        Assert.Equal("Neuer Name", Z(ersatz!["elemente"]![0]!["name"]));
    }

    [Fact]
    public async Task Befehl_AbgelehnterBefehlGibt200MitAngenommenFalseUndVorschlag()
    {
        using var s = new Server();
        await s.Anlegen();
        var start = await s.Pruefsumme();

        var (code, j) = await s.Post("/api/befehl/element_aendern", new { element = "E.02", eigenschaften = new { gibtEsNicht = 1 } });
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.False(j!["angenommen"]!.GetValue<bool>());
        Assert.False(string.IsNullOrWhiteSpace(Z(j["fehler"])));
        Assert.Contains("Erlaubt", Z(j["vorschlag"]));

        var (c2, j2) = await s.Post("/api/befehl/element_loeschen", new { element = "SW-F2" });   // Strang mit Bauteilen
        Assert.Equal(HttpStatusCode.OK, c2);
        Assert.False(j2!["angenommen"]!.GetValue<bool>());
        Assert.False(string.IsNullOrWhiteSpace(Z(j2["vorschlag"])));

        var (c3, j3) = await s.Post("/api/befehl/gibt_es_nicht", new { });
        Assert.Equal(HttpStatusCode.OK, c3);
        Assert.False(j3!["angenommen"]!.GetValue<bool>());

        var (c4, j4) = await s.Post("/api/befehl/element_aendern", new { element = "GIBT-ES-NICHT", name = "x" });
        Assert.Equal(HttpStatusCode.OK, c4);
        Assert.False(j4!["angenommen"]!.GetValue<bool>());

        Assert.Equal(start, await s.Pruefsumme());
    }

    [Fact]
    public async Task Befehl_RueckgaengigUndWiederholenUeberBefehlsEndpunkt()
    {
        using var s = new Server();
        await s.Anlegen();
        var start = await s.Pruefsumme();
        await s.Post("/api/befehl/element_aendern", Aendern());
        var geaendert = await s.Pruefsumme();
        Assert.NotEqual(start, geaendert);

        var (c0, tl) = await s.Post("/api/befehl/rueckgaengig?trockenlauf=true");
        Assert.Equal(HttpStatusCode.OK, c0);
        Assert.True(tl!["trockenlauf"]!.GetValue<bool>());
        Assert.Equal(geaendert, await s.Pruefsumme());

        var (code, j) = await s.Post("/api/befehl/rueckgaengig");
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.True(j!["angenommen"]!.GetValue<bool>(), j.ToJsonString());
        Assert.NotEqual(geaendert, await s.Pruefsumme());
        var el = (await s.Post("/api/abfrage/element_lesen", new { element = "E.02" })).Json!;
        Assert.NotEqual("Neuer Name", Z(el["name"]));

        var (c2, j2) = await s.Post("/api/befehl/wiederholen");
        Assert.Equal(HttpStatusCode.OK, c2);
        Assert.True(j2!["angenommen"]!.GetValue<bool>(), j2.ToJsonString());
        var el2 = (await s.Post("/api/abfrage/element_lesen", new { element = "E.02" })).Json!;
        Assert.Equal("Neuer Name", Z(el2["name"]));
    }

    [Fact]
    public async Task Befehl_RueckgaengigOhneAenderungWirdAbgelehntOhneServerfehler()
    {
        using var s = new Server();
        await s.Anlegen(beispiel: false);
        var (code, j) = await s.Post("/api/befehl/rueckgaengig");
        Assert.True((int)code < 500);
        Assert.False(j!["angenommen"]!.GetValue<bool>());
        Assert.False(string.IsNullOrWhiteSpace(Z(j["fehler"])));
    }

    [Fact]
    public async Task Befehl_SynchronisierenMeldetKeineNeuenPaketeVonAussen()
    {
        using var s = new Server();
        await s.Anlegen();
        var (code, j) = await s.Post("/api/befehl/synchronisieren");
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Equal(0, j!["neuePakete"]!.GetValue<int>());
    }

    [Fact]
    public async Task Befehl_AnlegenLeitungZiehenUndLoeschenUeberRest()
    {
        using var s = new Server();
        await s.Anlegen();
        var vorher = (await s.Get("/api/modell")).Json!["elemente"]!.AsArray().Count;

        var (code, j) = await s.Post("/api/befehl/element_anlegen", Par("""
            {"klasse":"Raum","kennzeichen":"R-TEST","geschoss":"EG","eigenschaften":{"nummer":"T.01","nutzung":"Büro"},
             "geometrie":{"art":"polygon","punkte":[[0,0],[3000,0],[3000,3000],[0,3000]],"hoehe":2500}}
            """));
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.True(j!["angenommen"]!.GetValue<bool>(), j.ToJsonString());
        Assert.Equal(vorher + 1, (await s.Get("/api/modell")).Json!["elemente"]!.AsArray().Count);

        var (c2, j2) = await s.Post("/api/befehl/element_loeschen", new { element = "R-TEST" });
        Assert.Equal(HttpStatusCode.OK, c2);
        Assert.True(j2!["angenommen"]!.GetValue<bool>(), j2.ToJsonString());
        Assert.Equal(vorher, (await s.Get("/api/modell")).Json!["elemente"]!.AsArray().Count);
    }

    // ------------------------------------------------------------------ 6. Freigeben, Zuletzt

    [Fact]
    public async Task Freigeben_MitProjektLiefertJson()
    {
        using var s = new Server();
        await s.Anlegen();
        var (code, j) = await s.Post("/api/freigeben");
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.NotNull(j!["freigegeben"]);
        // Projekt bleibt offen und bearbeitbar
        Assert.True((await s.Get("/api/status")).Json!["offen"]!.GetValue<bool>());
        Assert.True((await s.Post("/api/befehl/element_aendern", Aendern())).Json!["angenommen"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Zuletzt_IstEineListe_TestprojekteErscheinenNicht()
    {
        using var s = new Server();
        var (code0, leer) = await s.Get("/api/zuletzt");
        Assert.Equal(HttpStatusCode.OK, code0);
        Assert.IsAssignableFrom<JsonArray>(leer);

        // Testprojekte (ZuletztMerken aus, Pfad unter %TEMP%) gelangen nie in die Liste des Benutzers
        await s.Anlegen();
        var (code, j) = await s.Get("/api/zuletzt");
        Assert.Equal(HttpStatusCode.OK, code);
        var liste = Assert.IsAssignableFrom<JsonArray>(j);
        Assert.InRange(liste.Count, 0, 10);
        Assert.DoesNotContain(liste, e => (Z(e!["ordner"]) ?? "").StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase));
        foreach (var e in liste) { Assert.False(string.IsNullOrEmpty(Z(e!["name"]))); Assert.False(string.IsNullOrEmpty(Z(e["zeit"]))); }
        // auch im Status
        Assert.IsAssignableFrom<JsonArray>((await s.Get("/api/status")).Json!["zuletzt"]);
    }

    // ------------------------------------------------------------------ 7. Kein 500 bei fachlichen Fehlern

    [Fact]
    public async Task FachlicheFehler_LiefernNie500()
    {
        using var s = new Server();
        // ohne Projekt
        var ohne = new List<(HttpMethod, string, string?)>
        {
            (HttpMethod.Get, "/api/modell", null), (HttpMethod.Post, "/api/elemente", "{}"), (HttpMethod.Post, "/api/abfrage/pruefen", "{}"),
            (HttpMethod.Post, "/api/befehl/wiederholen", "{}"), (HttpMethod.Post, "/api/freigeben", "{}"),
            (HttpMethod.Post, "/api/projekt/oeffnen", "{}"),
            (HttpMethod.Post, "/api/projekt/oeffnen", "{\"ordner\":42}"),
            (HttpMethod.Post, "/api/projekt/oeffnen", "{\"ordner\":\"Z:\\\\gibt\\\\es\\\\nicht\\\\e_KICAD\"}"),
        };
        foreach (var (m, pfad, k) in ohne)
        {
            var (code, text, _) = await s.Roh(m, pfad, k);
            Assert.True((int)code < 500, $"{m} {pfad} {k}: {(int)code} {text}");
            if (text.Length > 0) JsonNode.Parse(text);
        }

        // mit Projekt
        await s.Anlegen();
        var mit = new List<(string, string)>
        {
            ("/api/abfrage/element_lesen", "{\"element\":42}"), ("/api/abfrage/element_lesen", "{\"element\":{\"a\":1}}"),
            ("/api/abfrage/elemente_suchen", "{\"seite\":\"x\"}"), ("/api/abfrage/elemente_suchen", "{\"seite\":-3,\"proSeite\":0}"),
            ("/api/abfrage/netz_folgen", "{\"von\":\"MV-1\",\"richtung\":\"seitwaerts\"}"),
            ("/api/abfrage/warum", "{\"element\":\"E.02\",\"eigenschaft\":\"gibtesnicht\"}"),
            ("/api/abfrage/katalog", "{\"klasse\":\"Gibtsnicht\"}"), ("/api/abfrage/mengen", "{\"system\":\"XYZ\"}"),
            ("/api/abfrage/zeitreihe", "{\"datenpunkt\":\"WZ-1\",\"von\":\"a\",\"bis\":\"b\"}"),
            ("/api/befehl/element_aendern", "{}"), ("/api/befehl/element_aendern", "{\"element\":\"E.02\",\"name\":42}"),
            ("/api/befehl/element_aendern", "{\"element\":\"E.02\",\"geometrie\":{\"art\":\"unsinn\"}}"),
            ("/api/befehl/element_anlegen", "{\"klasse\":\"Unbekannt\"}"), ("/api/befehl/element_anlegen", "{\"klasse\":\"Raum\",\"geometrie\":[1,2]}"),
            ("/api/befehl/verbinden", "{\"von\":\"A\",\"nach\":\"B\"}"), ("/api/befehl/trennen", "{}"),
            ("/api/befehl/leitung_ziehen", "{\"punkte\":[[0,0]]}"), ("/api/befehl/element_loeschen", "{\"element\":\"GIBT-ES-NICHT\"}"),
            ("/api/elemente", "{\"ids\":\"nicht-liste\"}"),

        };
        foreach (var (pfad, k) in mit)
        {
            var (code, text, _) = await s.Roh(HttpMethod.Post, pfad, k);
            Assert.True((int)code < 500, $"{pfad} {k}: {(int)code} {text}");
            if (text.Length > 0) { Assert.StartsWith("{", text); JsonNode.Parse(text); }
        }
    }

    [Fact]
    public async Task UngueltigerTrockenlaufWertGibt400Nicht500()
    {
        using var s = new Server();
        await s.Anlegen();
        var start = await s.Pruefsumme();
        var (code, _, _) = await s.Roh(HttpMethod.Post, "/api/befehl/element_aendern?trockenlauf=vielleicht", """{"element":"E.02","name":"x"}""");
        Assert.Equal(HttpStatusCode.BadRequest, code);
        Assert.Equal(start, await s.Pruefsumme());
    }

    [Fact]
    public async Task FehlerAntwortenHabenJsonUtf8()
    {
        using var s = new Server();
        var (_, text, a) = await s.Roh(HttpMethod.Get, "/api/modell");
        Assert.Equal("application/json", a.Content.Headers.ContentType!.MediaType);
        Assert.Equal("utf-8", a.Content.Headers.ContentType!.CharSet);
        Assert.Contains("fehler", text);
    }

    // ------------------------------------------------------------------ Serien-PDF (Plansätze)

    [Fact]
    public async Task Plansaetze_UeberRest_AngebotenSetzenExportierenNichtUeberschreiben()
    {
        using var s = new Server();
        await s.Anlegen();   // Ablage …\hlks_api_xxx\c_Pläne und Modelle\e_KICAD → Objektordner = Wurzel
        foreach (var (nr, titel, fmt) in new[] { ("1-0768 S02", "Sanitär 1.Obergeschoss", "A3"), ("1-0768 S01", "Sanitär 1.Untergeschoss", "A1"), ("1-0768 H01", "Heizung 1.Untergeschoss", "A1") })
        {
            var (c, j) = await s.Post("/api/befehl/planblatt_anlegen", new { nummer = nr, titel, format = fmt });
            Assert.True(j!["angenommen"]!.GetValue<bool>(), j.ToJsonString());
        }
        var (code, a) = await s.Post("/api/abfrage/plansaetze", new { datum = "2026-10-19", anlass = "Revisionspläne" });
        Assert.Equal(HttpStatusCode.OK, code);
        var saetze = a!["plansaetze"]!.AsArray();
        Assert.Equal(["auto:heizung", "auto:sanitaer"], saetze.Select(x => x!["schluessel"]!.GetValue<string>()).ToArray());
        Assert.Equal(["1-0768 S01", "1-0768 S02"], saetze[1]!["blaetter"]!.AsArray().Select(b => b!["nummer"]!.GetValue<string>()).ToArray());
        var soll = Path.Combine(s.Wurzel, "c_Pläne und Modelle", "d_Ausgang", "b_Pläne", "19.10.2026_Revisionspläne", "Sanitär");
        Assert.Equal(soll, saetze[1]!["vorschlag"]!["ordner"]!.GetValue<string>());

        // Auswahl und Reihenfolge setzen (Befehl mit Trockenlauf), exportieren
        var (_, tl) = await s.Post("/api/befehl/plansatz_setzen?trockenlauf=true", new { plansatz = "auto:sanitaer" });
        Assert.True(tl!["trockenlauf"]!.GetValue<bool>());
        var par = new { plansatz = "auto:sanitaer", anlass = "Revisionspläne", datum = "2026-10-19", einzeln = true, blaetter = new[] { "1-0768 S02", "1-0768 S01" }, abgewaehlt = new[] { "1-0768 S02" } };
        var (code2, e) = await s.Post("/api/abfrage/plansatz_pdf", par);
        Assert.Equal(HttpStatusCode.OK, code2);
        Assert.Equal(soll, e!["ordner"]!.GetValue<string>());
        Assert.Equal(1, e["seiten"]!.GetValue<int>());
        Assert.True(File.Exists(Path.Combine(soll, "1-0768_Plansatz Sanitär_2026-10-19.pdf")), e.ToJsonString());
        Assert.True(File.Exists(Path.Combine(soll, "1-0768-S01-Sanitär 1.Untergeschoss-.pdf")));
        Assert.False(File.Exists(Path.Combine(soll, "1-0768-S02-Sanitär 1.Obergeschoss-.pdf")));   // abgewählt

        // die Auswahl ist beim nächsten Mal wieder da
        var (_, b) = await s.Post("/api/abfrage/plansaetze", new { plansatz = "PS-1" });
        var blaetter = b!["plansaetze"]![0]!["blaetter"]!.AsArray();
        Assert.Equal(["1-0768 S02", "1-0768 S01"], blaetter.Select(x => x!["nummer"]!.GetValue<string>()).ToArray());
        Assert.Equal([false, true], blaetter.Select(x => x!["dabei"]!.GetValue<bool>()).ToArray());

        // nicht überschreiben: 400 mit Fehler und Vorschlag
        var (code3, text3, _) = await s.Roh(HttpMethod.Post, "/api/abfrage/plansatz_pdf", Hlks.Kern.Json.Text(par));
        Assert.Equal(HttpStatusCode.BadRequest, code3);
        var f = JsonNode.Parse(text3)!;
        Assert.Contains("schon vorhanden", f["fehler"]!.GetValue<string>());
        Assert.Contains("ueberschreiben", f["vorschlag"]!.GetValue<string>());
        var (code4, _) = await s.Post("/api/abfrage/plansatz_pdf", new { plansatz = "PS-1", anlass = "Revisionspläne", datum = "2026-10-19", einzeln = true, ueberschreiben = true });
        Assert.Equal(HttpStatusCode.OK, code4);
    }

    [Fact]
    public async Task Oeffnen_AltesProjektMitKuehlwasserUndKaelteGewerk_OeffnetOhneFehlerMitHinweis()
    {
        // «Kälte ist Teil der Heizung» (Entscheid Christian 20.10.2026): alte Kennungen werden beim Lesen umgeschrieben, nie ein Fehler
        using var s = new Server();
        var kat = Katalog.Laden(Katalog.StandardOrdner());
        var speicher = new OrdnerSpeicher(s.Ordner);
        Projekt.Anlegen(kat, speicher, "Altprojekt (fiktiv)", "alt", "pc").Freigeben();
        Wert W(object v) => new() { Inhalt = JsonValue.Create(v), Herkunft = new Herkunft { Art = "mensch", Akteur = "alt", Zeit = DateTimeOffset.Parse("2026-10-01T10:00:00Z") } };
        var sys = new Element { Id = "id-s", Klasse = "System", Kennzeichen = "KUE-VL", Version = 1 };
        sys.Eigenschaften["systemtyp"] = W("Kühlwasser Vorlauf");
        sys.Eigenschaften["medium"] = W("KUE-VL");
        var blatt = new Element { Id = "id-b", Klasse = "Planblatt", Kennzeichen = "PB-1", Version = 1 };
        blatt.Eigenschaften["nummer"] = W("1-0768 H05");
        blatt.Eigenschaften["gewerk"] = W("kaelte");
        speicher.StandSchreiben(new StandAbbild { Zeit = DateTimeOffset.UtcNow, Elemente = [sys, blatt] });

        var (code, j) = await s.Post("/api/projekt/oeffnen", new { ordner = s.Ordner });
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.True(j!["offen"]!.GetValue<bool>());
        Assert.Contains("Kälte gehört zur Heizung", j["aliasHinweis"]!.GetValue<string>());
        Assert.Contains("KUE-VL → KAE-VL", j["aliasHinweis"]!.GetValue<string>());
        var (_, el) = await s.Post("/api/abfrage/element_lesen", new { element = "KUE-VL" });
        var elText = el!.ToJsonString(new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        Assert.Contains("Kälte Vorlauf", elText);
        Assert.Contains("KAE-VL", elText);
        // ein Projekt ohne alte Werte meldet nichts
        var (_, neu) = await s.Post("/api/projekt/anlegen", new { ordner = Path.Combine(s.Wurzel, "zweites", "e_KICAD"), name = "Neu", beispiel = true });
        Assert.Null(neu!["aliasHinweis"]);
    }
}
