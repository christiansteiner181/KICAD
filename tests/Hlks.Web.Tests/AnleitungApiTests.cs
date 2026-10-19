using System.Net;
using System.Text.Json.Nodes;
using Hlks.Kern;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Hlks.Web.Tests;

/// <summary>
/// Adresse der Bedienungsanleitung für das Hilfe-Symbol «?» und F1 (js/oberflaeche/hilfe-oberflaeche.js): Sie steht im Bürowert
/// kataloge/buero.json › anleitung und kommt mit GET /api/start/info als «anleitung» { url, hinweis } zur Oberfläche.
/// Die Tests arbeiten mit einer Kopie des Katalogordners unter %TEMP% (buero.json je Test ersetzt) – nie mit der echten Ablage auf W:.
/// </summary>
public class AnleitungApiTests
{
    sealed class Umgebung : IDisposable
    {
        readonly WebApplicationFactory<Program> basis = new();
        WebApplicationFactory<Program>? fabrik;
        public HttpClient Http { get; private set; } = null!;
        public string Wurzel { get; } = Path.Combine(Path.GetTempPath(), "hlks_anleitung_" + Guid.NewGuid().ToString("N")[..10]);
        public string Kataloge => Path.Combine(Wurzel, "kataloge");

        /// <summary>Kopiert den Katalogordner und schreibt buero.json mit dem gegebenen Text (null = Datei löschen).</summary>
        public Umgebung(string? bueroJson)
        {
            Directory.CreateDirectory(Kataloge);
            foreach (var d in Directory.GetFiles(Katalog.StandardOrdner(), "*.json")) File.Copy(d, Path.Combine(Kataloge, Path.GetFileName(d)));
            var buero = Path.Combine(Kataloge, "buero.json");
            if (bueroJson is null) File.Delete(buero); else File.WriteAllText(buero, bueroJson);
        }

        public Umgebung Starten()
        {
            var e = new Einstellungen("Test", "testpc", Kataloge, null)
            {
                ZuletztOrdner = Path.Combine(Wurzel, "zuletzt"), ProjektAblage = Path.Combine(Wurzel, "ablage"),
                Familienbibliothek = Path.Combine(Wurzel, "bibliothek"), StartZeitlimit = TimeSpan.FromSeconds(10),
            };
            Directory.CreateDirectory(e.ProjektAblage!);
            fabrik = basis.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton(e)));
            Http = fabrik.CreateClient();
            return this;
        }

        public async Task<JsonNode> Info()
        {
            var a = await Http.GetAsync("/api/start/info");
            Assert.Equal(HttpStatusCode.OK, a.StatusCode);
            return JsonNode.Parse(await a.Content.ReadAsStringAsync())!;
        }

        public void Dispose()
        {
            Http?.Dispose();
            fabrik?.Dispose();
            basis.Dispose();
            try { Directory.Delete(Wurzel, true); } catch (IOException) { /* der Server hält evtl. noch eine Datei */ }
        }
    }

    [Fact]
    public async Task Info_LiefertAdresseUndHinweisAusBuero_Json()
    {
        using var u = new Umgebung("""
            { "anleitung": { "url": "https://example.test/anleitung", "hinweis": "Testhinweis" } }
            """).Starten();
        var j = await u.Info();
        Assert.Equal("https://example.test/anleitung", (string?)j["anleitung"]!["url"]);
        Assert.Equal("Testhinweis", (string?)j["anleitung"]!["hinweis"]);
    }

    [Fact]
    public async Task Info_OhneAnleitungsAbschnitt_IstAnleitungNull()
    {
        using var u = new Umgebung("""{ "projektablage": { "pfad": "X" } }""").Starten();
        var j = await u.Info();
        Assert.Null(j["anleitung"]);
        Assert.Equal("KICAD", (string?)j["programm"]);   // der Rest der Antwort bleibt
    }

    [Fact]
    public async Task Info_OhneBueroDatei_IstAnleitungNull_NieFehler()
    {
        using var u = new Umgebung(null).Starten();
        Assert.Null((await u.Info())["anleitung"]);
    }

    [Theory]
    [InlineData("""{ "anleitung": { "url": "" } }""")]
    [InlineData("""{ "anleitung": { "url": "   " } }""")]
    [InlineData("""{ "anleitung": { "hinweis": "nur Hinweis" } }""")]
    [InlineData("""{ "anleitung": { "url": "javascript:alert(1)" } }""")]
    [InlineData("""{ "anleitung": { "url": "file:///C:/geheim.txt" } }""")]
    [InlineData("""{ "anleitung": { "url": "kein url" } }""")]
    [InlineData("""{ "anleitung": "text" }""")]
    [InlineData("{ kaputt")]
    public async Task Info_UnbrauchbareAdresse_IstAnleitungNull(string buero)
    {
        using var u = new Umgebung(buero).Starten();
        Assert.Null((await u.Info())["anleitung"]);
    }

    [Fact]
    public async Task Info_AdresseOhneHinweis_HatHinweisNull()
    {
        using var u = new Umgebung("""{ "anleitung": { "url": " http://localhost/x " } }""").Starten();
        var a = (await u.Info())["anleitung"]!;
        Assert.Equal("http://localhost/x", (string?)a["url"]);   // getrimmt
        Assert.Null(a["hinweis"]);
    }

    [Fact]
    public void BueroJson_DesBueros_TraegtDieAdresseDerAnleitung()
    {
        var datei = Path.Combine(Katalog.StandardOrdner(), "buero.json");
        var a = JsonNode.Parse(File.ReadAllText(datei))!["anleitung"]!;
        Assert.Equal("https://claude.ai/artifact/5NEkGw76J5tk5sxFrbfbkq", (string?)a["url"]);
        Assert.Contains("#k-<register>~<knopf>", (string?)a["hinweis"]);
    }
}
