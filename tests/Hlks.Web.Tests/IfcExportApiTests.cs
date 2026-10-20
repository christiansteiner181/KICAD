using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Hlks.Kern;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Hlks.Web.Tests;

/// <summary>Modul 7 Teil B2: IFC-Export über REST – POST /api/ifc/export (nur JSON, Herkunftsschutz) legt ohne datei unter
/// &lt;Projektordner&gt;\ausgabe\ifc ab und liefert { datei, bericht }; GET /api/ifc/export/datei lädt nur aus diesem Ordner
/// (IFC und Bericht), alles andere 403/404/400. Testserver im Speicher (TestServer, kein Port), Projekt im Temp-Ordner.</summary>
public class IfcExportApiTests
{
    sealed class Server : IDisposable
    {
        readonly WebApplicationFactory<Program> basis = new();
        readonly WebApplicationFactory<Program> fabrik;
        public HttpClient Http { get; }
        public string Wurzel { get; } = Path.Combine(Path.GetTempPath(), "hlks_ifcexp_" + Guid.NewGuid().ToString("N")[..10]);
        public string Ordner => Path.Combine(Wurzel, "c_Pläne und Modelle", "e_KICAD");

        public Server()
        {
            var e = new Einstellungen("Test", "testpc", Katalog.StandardOrdner(), null) { ZuletztMerken = false };
            fabrik = basis.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton(e)));
            Http = fabrik.CreateClient();
        }

        public async Task<(HttpStatusCode Code, string Text, HttpResponseMessage Antwort)> Senden(HttpMethod m, string pfad, string? koerper = null, string typ = "application/json")
        {
            var req = new HttpRequestMessage(m, pfad);
            if (koerper is not null) req.Content = new StringContent(koerper, Encoding.UTF8, typ);
            var a = await Http.SendAsync(req);
            return (a.StatusCode, await a.Content.ReadAsStringAsync(), a);
        }

        public async Task Anlegen()
        {
            var (code, text, _) = await Senden(HttpMethod.Post, "/api/projekt/anlegen", Hlks.Kern.Json.Text(new { ordner = Ordner, name = "MFH Musterweg", beispiel = true }));
            Assert.True(code == HttpStatusCode.OK, text);
        }

        public void Dispose()
        {
            try { Http.PostAsync("/api/projekt/schliessen", new StringContent("{}", Encoding.UTF8, "application/json")).GetAwaiter().GetResult(); } catch { /* egal */ }
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

    [Fact]
    public async Task Export_OhneDatei_ImProjektordnerAusgabeIfc_DateiUndBerichtHerunterladen()
    {
        using var s = new Server();
        await s.Anlegen();
        var (code, text, _) = await s.Senden(HttpMethod.Post, "/api/ifc/export", """{"fassung":"IFC2X3"}""");
        Assert.True(code == HttpStatusCode.OK, text);
        var j = JsonNode.Parse(text)!;
        var datei = j["datei"]!.GetValue<string>();
        Assert.Equal(Path.Combine(s.Ordner, "ausgabe", "ifc"), Path.GetDirectoryName(datei));
        Assert.Matches(@"^MFH Musterweg \(Beispiel, fiktiv\)_IFC2X3_\d{8}-\d{4}\.ifc$", Path.GetFileName(datei));
        Assert.Equal("fehlerfrei", j["bericht"]!["schemapruefung"]!["ergebnis"]!.GetValue<string>());
        Assert.Equal("bestanden", j["bericht"]!["ergebnis"]!["ids"]!.GetValue<string>());
        // Herunterladen: voller Pfad und blosser Dateiname
        var (c1, inhalt, a1) = await s.Senden(HttpMethod.Get, "/api/ifc/export/datei?pfad=" + Uri.EscapeDataString(datei));
        Assert.Equal(HttpStatusCode.OK, c1);
        Assert.StartsWith("ISO-10303-21;", inhalt);
        Assert.Equal("application/x-step", a1.Content.Headers.ContentType!.MediaType);
        Assert.Equal(new FileInfo(datei).Length, a1.Content.Headers.ContentLength);
        var md = Path.GetFileNameWithoutExtension(datei) + ".bericht.md";
        var (c2, mdText, _) = await s.Senden(HttpMethod.Get, "/api/ifc/export/datei?pfad=" + Uri.EscapeDataString(md));
        Assert.Equal(HttpStatusCode.OK, c2);
        Assert.Contains("# IFC-Exportbericht", mdText);
        var (c3, jsonText, a3) = await s.Senden(HttpMethod.Get, "/api/ifc/export/datei?pfad=" + Uri.EscapeDataString(Path.ChangeExtension(datei, null) + ".bericht.json"));
        Assert.Equal(HttpStatusCode.OK, c3);
        Assert.Equal("application/json", a3.Content.Headers.ContentType!.MediaType);
        Assert.Equal(datei, JsonNode.Parse(jsonText)!["datei"]!.GetValue<string>());
    }

    [Fact]
    public async Task Export_NurJson_SonstHerkunftsschutz415()
    {
        using var s = new Server();
        await s.Anlegen();
        var (code, text, _) = await s.Senden(HttpMethod.Post, "/api/ifc/export", """{"fassung":"IFC4"}""", "text/plain");
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, code);
        Assert.Contains("application/json", text);
        Assert.False(Directory.Exists(Path.Combine(s.Ordner, "ausgabe", "ifc")) && Directory.EnumerateFiles(Path.Combine(s.Ordner, "ausgabe", "ifc")).Any());
        var (c2, _, _) = await s.Senden(HttpMethod.Post, "/api/ifc/export", "fassung=IFC4", "application/x-www-form-urlencoded");
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, c2);
        // fachlicher Fehler: 400 mit Vorschlag, nie 500
        var (c3, t3, _) = await s.Senden(HttpMethod.Post, "/api/ifc/export", """{"fassung":"IFC4X3"}""");
        Assert.Equal(HttpStatusCode.BadRequest, c3);
        Assert.Contains("IFC2X3", JsonNode.Parse(t3)!["vorschlag"]!.GetValue<string>());
    }

    [Fact]
    public async Task Herunterladen_NurAusAusgabeIfcDesOffenenProjekts()
    {
        using var s = new Server();
        await s.Anlegen();
        var (code, text, _) = await s.Senden(HttpMethod.Post, "/api/ifc/export", "{}");
        Assert.True(code == HttpStatusCode.OK, text);
        var ausgabe = Path.Combine(s.Ordner, "ausgabe", "ifc");
        // ausserhalb: projekt.json, Temp, Pfad mit «..», Unterordner, andere Endung im Ordner
        File.WriteAllText(Path.Combine(ausgabe, "notiz.txt"), "fremd");
        Directory.CreateDirectory(Path.Combine(ausgabe, "unter"));
        File.WriteAllText(Path.Combine(ausgabe, "unter", "x.ifc"), "ISO-10303-21;");
        var fremd = Path.Combine(Path.GetTempPath(), "hlks_fremd_" + Guid.NewGuid().ToString("N")[..8] + ".ifc");
        File.WriteAllText(fremd, "ISO-10303-21;");
        try
        {
            foreach (var pfad in new[] { Path.Combine(s.Ordner, "projekt.json"), fremd, Path.Combine(ausgabe, "..", "..", "projekt.json"), "..\\..\\projekt.json",
                         Path.Combine(ausgabe, "notiz.txt"), Path.Combine(ausgabe, "unter", "x.ifc") })
            {
                var (c, t, _) = await s.Senden(HttpMethod.Get, "/api/ifc/export/datei?pfad=" + Uri.EscapeDataString(pfad));
                Assert.True(c == HttpStatusCode.Forbidden, $"{pfad}: {(int)c} {t}");
                Assert.NotNull(JsonNode.Parse(t)!["fehler"]);
            }
            var (c404, _, _) = await s.Senden(HttpMethod.Get, "/api/ifc/export/datei?pfad=" + Uri.EscapeDataString(Path.Combine(ausgabe, "gibtsnicht.ifc")));
            Assert.Equal(HttpStatusCode.NotFound, c404);
            var (c400, _, _) = await s.Senden(HttpMethod.Get, "/api/ifc/export/datei");
            Assert.Equal(HttpStatusCode.BadRequest, c400);
        }
        finally { File.Delete(fremd); }
    }

    [Fact]
    public async Task OhneProjekt_409()
    {
        using var s = new Server();
        var (code, _, _) = await s.Senden(HttpMethod.Post, "/api/ifc/export", "{}");
        Assert.Equal(HttpStatusCode.Conflict, code);
        var (c2, _, _) = await s.Senden(HttpMethod.Get, "/api/ifc/export/datei?pfad=x.ifc");
        Assert.Equal(HttpStatusCode.Conflict, c2);
    }
}
