using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Hlks.Kern;
using Hlks.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Hlks.Web.Tests;

/// <summary>BIM to field Testserver (BaustelleZugang.cs, Christian 04.11.2026): auf dem Baustellen-Port antwortet nur /baustelle, Rückmeldungen nur
/// über diesen Port und landen in eingang\baustelle. Der Port des TestServers wird je Anfrage gesetzt (Connection.LocalPort).</summary>
public class BaustelleZugangTests
{
    const int Bp = 5135;

    [Fact]
    public async Task NurBaustelleImBueronetz_RueckmeldungLandetImEingang()
    {
        Environment.SetEnvironmentVariable("HLKS_BAUSTELLE_PORT", Bp.ToString());
        var wurzel = Path.Combine(Path.GetTempPath(), "hlks_bs_" + Guid.NewGuid().ToString("N")[..10]);
        var ordner = Path.Combine(wurzel, "e_KICAD");
        using var basis = new WebApplicationFactory<Program>();
        var e = new Einstellungen("Test", "testpc", Katalog.StandardOrdner(), null) { ZuletztMerken = false };
        using var fabrik = basis.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton(e)));
        try
        {
            var http = fabrik.CreateClient();
            HttpContent Json(object o) => new StringContent(Hlks.Kern.Json.Text(o), Encoding.UTF8, "application/json");
            Assert.Equal(HttpStatusCode.OK, (await http.PostAsync("/api/projekt/anlegen", Json(new { ordner, name = "MFH Musterweg", beispiel = true }))).StatusCode);
            var ex = await http.PostAsync("/api/abfrage/baustelle_export", Json(new { rolle = "bauleitung", stand = "arbeitsstand" }));
            Assert.Equal(HttpStatusCode.OK, ex.StatusCode);
            var datei = Path.GetFileName((string)JsonNode.Parse(await ex.Content.ReadAsStringAsync())!["datei"]!);

            async Task<HttpContext> Netz(string methode, string pfad, string? koerper = null) => await fabrik.Server.SendAsync(c =>
            {
                c.Connection.LocalPort = Bp;
                c.Request.Method = methode; c.Request.Path = pfad;
                if (koerper is not null) { c.Request.ContentType = "application/json"; c.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(koerper)); }
            });
            static string Text(HttpContext c) => new StreamReader(c.Response.Body).ReadToEnd();

            // Büronetz: Oberfläche und /api gesperrt, /baustelle offen
            Assert.Equal(404, (await Netz("GET", "/api/status")).Response.StatusCode);
            Assert.Equal(404, (await Netz("GET", "/")).Response.StatusCode);
            var index = await Netz("GET", "/baustelle/");
            Assert.Equal(200, index.Response.StatusCode);
            Assert.Contains(Uri.EscapeDataString(datei), Text(index));
            var seite = await Netz("GET", "/baustelle/seite/" + Uri.EscapeDataString(datei));
            Assert.Contains("<link rel=\"manifest\" href=\"/baustelle/manifest.webmanifest\">", Text(seite));
            Assert.Equal(200, (await Netz("GET", "/baustelle/icon.png")).Response.StatusCode);

            // Rückmeldung: über localhost abgelehnt, über das Büronetz abgelegt
            var r = new JsonObject { ["format"] = BaustelleRueckmeldung.Format, ["fassung"] = 1, ["person"] = "Test Monteur", ["meldungen"] = new JsonArray() }.ToJsonString();
            Assert.Equal(HttpStatusCode.Forbidden, (await http.PostAsync("/baustelle/rueckmeldung", new StringContent(r, Encoding.UTF8, "application/json"))).StatusCode);
            Assert.Equal(400, (await Netz("POST", "/baustelle/rueckmeldung", "{\"format\":\"anders\"}")).Response.StatusCode);
            var ok = await Netz("POST", "/baustelle/rueckmeldung", r);
            Assert.Equal(200, ok.Response.StatusCode);
            var eingang = JsonNode.Parse(await http.GetStringAsync("/api/baustelle/eingang"))!["dateien"]!.AsArray();
            Assert.Equal("Test Monteur", (string)Assert.Single(eingang)!["person"]!);
            Assert.True(File.Exists(Path.Combine(ordner, "eingang", "baustelle", (string)eingang[0]!["datei"]!)));
            await http.PostAsync("/api/projekt/schliessen", Json(new { }));
        }
        finally
        {
            Environment.SetEnvironmentVariable("HLKS_BAUSTELLE_PORT", null);
            try { Directory.Delete(wurzel, true); } catch (IOException) { }
        }
    }

    [Fact]
    public async Task Zugaenge_PersoenlicherLink_MonteurSiehtNurMonteurseiten_RueckmeldungMitSeinemNamen()
    {
        // Christian 12.11.2026: Monteur und Projektleiter des Unternehmers, Anmeldung über persönlichen Link, Projektleiter auch Arbeitsstand, je Projekt
        Environment.SetEnvironmentVariable("HLKS_BAUSTELLE_PORT", Bp.ToString());
        var wurzel = Path.Combine(Path.GetTempPath(), "hlks_bs_" + Guid.NewGuid().ToString("N")[..10]);
        var ordner = Path.Combine(wurzel, "e_KICAD");
        using var basis = new WebApplicationFactory<Program>();
        var e = new Einstellungen("Test", "testpc", Katalog.StandardOrdner(), null) { ZuletztMerken = false };
        using var fabrik = basis.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton(e)));
        try
        {
            var http = fabrik.CreateClient();
            HttpContent Json(object o) => new StringContent(Hlks.Kern.Json.Text(o), Encoding.UTF8, "application/json");
            Assert.Equal(HttpStatusCode.OK, (await http.PostAsync("/api/projekt/anlegen", Json(new { ordner, name = "MFH Musterweg", beispiel = true }))).StatusCode);
            var ex = await http.PostAsync("/api/abfrage/baustelle_export", Json(new { rolle = "bauleitung", stand = "arbeitsstand" }));
            var arbeitsstand = Path.GetFileName((string)JsonNode.Parse(await ex.Content.ReadAsStringAsync())!["datei"]!);
            var monteurSeite = Path.Combine(ordner, "ausgabe", "baustelle", "MFH_monteur_test.html");
            File.WriteAllText(monteurSeite, "<html><head></head><body>Monteur</body></html>");
            Directory.CreateDirectory(Path.Combine(ordner, "baustelle"));
            File.WriteAllText(Path.Combine(ordner, "baustelle", "zugaenge.json"), """
                { "zugaenge": [ { "name": "Monteur Test", "rolle": "monteur", "schluessel": "mmmmmmmm2222" },
                                { "name": "Projektleiter Test", "rolle": "projektleiter", "schluessel": "pppppppp3333" } ] }
                """);

            async Task<HttpContext> Netz(string methode, string pfad, string? keks = null, string? koerper = null) => await fabrik.Server.SendAsync(c =>
            {
                c.Connection.LocalPort = Bp;
                c.Request.Method = methode; c.Request.Path = pfad;
                if (keks is not null) c.Request.Headers.Cookie = $"{BaustelleZugaenge.Keks}={keks}";
                if (koerper is not null) { c.Request.ContentType = "application/json"; c.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(koerper)); }
            });
            static string Text(HttpContext c) => new StreamReader(c.Response.Body).ReadToEnd();

            // ohne Anmeldung: nur der Hinweis, keine Webseite
            var ohne = Text(await Netz("GET", "/baustelle/"));
            Assert.Contains("Nicht angemeldet", ohne);
            Assert.DoesNotContain("monteur_test", ohne);
            Assert.Equal(403, (await Netz("GET", "/baustelle/seite/MFH_monteur_test.html")).Response.StatusCode);
            // persönlicher Link: Cookie und Weiterleitung; falscher Link abgelehnt
            var link = await Netz("GET", "/baustelle/z/mmmmmmmm2222");
            Assert.Equal(302, link.Response.StatusCode);
            Assert.Contains("mmmmmmmm2222", link.Response.Headers.SetCookie.ToString());
            Assert.Equal(403, (await Netz("GET", "/baustelle/z/falsch12345678")).Response.StatusCode);
            // Monteur: nur Monteur-Seiten
            var m = Text(await Netz("GET", "/baustelle/", "mmmmmmmm2222"));
            Assert.Contains("MFH_monteur_test", m);
            Assert.DoesNotContain(Path.GetFileNameWithoutExtension(arbeitsstand), m);
            Assert.Equal(403, (await Netz("GET", "/baustelle/seite/" + Uri.EscapeDataString(arbeitsstand), "mmmmmmmm2222")).Response.StatusCode);
            Assert.Equal(200, (await Netz("GET", "/baustelle/seite/MFH_monteur_test.html", "mmmmmmmm2222")).Response.StatusCode);
            // Projektleiter: alles, auch den Arbeitsstand
            var pl = Text(await Netz("GET", "/baustelle/", "pppppppp3333"));
            Assert.Contains(Path.GetFileNameWithoutExtension(arbeitsstand), pl);
            Assert.Contains("Projektleiter Test", pl);
            Assert.Equal(200, (await Netz("GET", "/baustelle/seite/" + Uri.EscapeDataString(arbeitsstand), "pppppppp3333")).Response.StatusCode);
            // Rückmeldung: ohne Anmeldung abgelehnt, mit Anmeldung unter dem Namen des Zugangs (nicht dem eingetippten)
            var r = new JsonObject { ["format"] = BaustelleRueckmeldung.Format, ["fassung"] = 1, ["person"] = "irgendwer", ["meldungen"] = new JsonArray() }.ToJsonString();
            Assert.Equal(403, (await Netz("POST", "/baustelle/rueckmeldung", null, r)).Response.StatusCode);
            Assert.Equal(200, (await Netz("POST", "/baustelle/rueckmeldung", "mmmmmmmm2222", r)).Response.StatusCode);
            var eingang = JsonNode.Parse(await http.GetStringAsync("/api/baustelle/eingang"))!["dateien"]!.AsArray();
            Assert.Equal("Monteur Test", (string)Assert.Single(eingang)!["person"]!);
            await http.PostAsync("/api/projekt/schliessen", Json(new { }));
        }
        finally
        {
            Environment.SetEnvironmentVariable("HLKS_BAUSTELLE_PORT", null);
            try { Directory.Delete(wurzel, true); } catch (IOException) { }
        }
    }
}
