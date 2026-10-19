using System.Net;
using System.Text.Json.Nodes;
using Hlks.Kern;
using Hlks.Tests.Gemeinsam;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Hlks.Web.Tests;

/// <summary>
/// Testumgebung der Bedienungsanleitung: ein Server (WebApplicationFactory) mit eigener Kopie des Katalogordners unter %TEMP% (buero.json je Test
/// ersetzt), eigenem Anleitungsordner und eigenem Ordner für den Prüfstand – nie die echten Ordner (%USERPROFILE%\HLKS_CAD_anleitung,
/// %OneDrive%\HLKS-CAD\anleitung). Die Ordner werden über Einstellungen.AnleitungOrdner/AnleitungPruefung gesetzt, nicht über Prozessvariablen.
/// </summary>
sealed class AnleitungUmgebung : IDisposable
{
    readonly WebApplicationFactory<Program> basis = new();
    WebApplicationFactory<Program>? fabrik;
    public HttpClient Http { get; private set; } = null!;
    public string Wurzel { get; } = Path.Combine(Path.GetTempPath(), "hlks_anleitung_" + Guid.NewGuid().ToString("N")[..10]);
    public string Kataloge => Path.Combine(Wurzel, "kataloge");
    public string Anleitung => Path.Combine(Wurzel, "anleitung");
    public string Pruefung => Path.Combine(Wurzel, "pruefung");

    /// <summary>Kopiert den Katalogordner und schreibt buero.json mit dem gegebenen Text (null = Datei löschen).</summary>
    public AnleitungUmgebung(string? bueroJson = """{ "projektablage": { "pfad": "X" } }""")
    {
        Directory.CreateDirectory(Kataloge);
        foreach (var d in Directory.GetFiles(Katalog.StandardOrdner(), "*.json")) File.Copy(d, Path.Combine(Kataloge, Path.GetFileName(d)));
        var buero = Path.Combine(Kataloge, "buero.json");
        if (bueroJson is null) File.Delete(buero); else File.WriteAllText(buero, bueroJson);
    }

    /// <summary>Legt index.html (und optional weitere Dateien) im Anleitungsordner an.</summary>
    public AnleitungUmgebung Seite(string html = "<!doctype html><title>Testanleitung</title><p>Hallo Anleitung</p>")
    {
        Directory.CreateDirectory(Path.Combine(Anleitung, "bilder"));
        File.WriteAllText(Path.Combine(Anleitung, "index.html"), html);
        return this;
    }

    /// <param name="anleitungOrdner">Wert für Einstellungen.AnleitungOrdner (null = nicht gesetzt: buero.json bzw. Umgebung gilt).</param>
    /// <param name="pruefung">Wert für Einstellungen.AnleitungPruefung (null = nicht umgeleitet).</param>
    public AnleitungUmgebung Starten(string? anleitungOrdner = "", string? pruefung = "", bool umleiten = false)
    {
        var e = new Einstellungen("Test", "testpc", Kataloge, null)
        {
            ZuletztOrdner = Path.Combine(Wurzel, "zuletzt"), ProjektAblage = Path.Combine(Wurzel, "ablage"),
            Familienbibliothek = Path.Combine(Wurzel, "bibliothek"), StartZeitlimit = TimeSpan.FromSeconds(10),
            BenutzerOrdner = Path.Combine(Wurzel, "benutzer"),
            AnleitungOrdner = anleitungOrdner == "" ? Anleitung : anleitungOrdner,
            AnleitungPruefung = pruefung == "" ? Pruefung : pruefung,
        };
        Directory.CreateDirectory(e.ProjektAblage!);
        fabrik = basis.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton(e)));
        Http = fabrik.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
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
        for (int i = 0; i < 5; i++)
        {
            try { if (Directory.Exists(Wurzel)) Directory.Delete(Wurzel, true); return; }
            catch (IOException) { Thread.Sleep(200); }   // der Server hält evtl. noch eine Datei
            catch (UnauthorizedAccessException) { Thread.Sleep(200); }
        }
    }
}

/// <summary>Prozessvariablen sind global: Tests, die HLKS_ANLEITUNG setzen, laufen allein.</summary>
[CollectionDefinition("AnleitungUmgebung", DisableParallelization = true)]
public sealed class AnleitungUmgebungSammlung;

/// <summary>
/// Bedienungsanleitung für das Hilfe-Symbol «?» und F1 (js/oberflaeche/hilfe-oberflaeche.js): KICAD liefert sie selbst unter /hilfe/ aus
/// (Entscheid Christian Steiner 19.10.2026). Der Ordner steht im Bürowert kataloge/buero.json › anleitung.ordner (HLKS_ANLEITUNG übersteuert);
/// GET /api/start/info meldet «anleitung» { url: "/hilfe/", vorhanden, hinweis, ordner } zur Oberfläche.
/// </summary>
[Collection("AnleitungUmgebung")]
public class AnleitungApiTests
{
    [Fact]
    public async Task Info_MitGebauterSeite_MeldetUrlUndVorhanden()
    {
        using var u = new AnleitungUmgebung().Seite().Starten();
        var a = (await u.Info())["anleitung"]!;
        Assert.Equal("/hilfe/", (string?)a["url"]);
        Assert.True((bool?)a["vorhanden"]);
        Assert.Equal(u.Anleitung, (string?)a["ordner"]);
    }

    [Fact]
    public async Task Info_OhneIndexHtml_IstNichtVorhanden()
    {
        using var u = new AnleitungUmgebung().Starten();   // Ordner gibt es nicht
        var a = (await u.Info())["anleitung"]!;
        Assert.Equal("/hilfe/", (string?)a["url"]);
        Assert.False((bool?)a["vorhanden"]);
        Directory.CreateDirectory(u.Anleitung);            // Ordner da, aber leer
        Assert.False((bool?)(await u.Info())["anleitung"]!["vorhanden"]);
        File.WriteAllText(Path.Combine(u.Anleitung, "index.html"), "<p>x</p>");   // jetzt gebaut: gilt ohne Neustart
        Assert.True((bool?)(await u.Info())["anleitung"]!["vorhanden"]);
        Assert.Equal("KICAD", (string?)(await u.Info())["programm"]);   // der Rest der Antwort bleibt
    }

    [Fact]
    public async Task Info_OhneAnleitungsAbschnitt_IstNichtVorhanden_OrdnerNull()
    {
        using var u = new AnleitungUmgebung("""{ "projektablage": { "pfad": "X" } }""").Starten(anleitungOrdner: null);
        var a = (await u.Info())["anleitung"]!;
        Assert.False((bool?)a["vorhanden"]);
        Assert.Null(a["ordner"]);
        Assert.Equal("/hilfe/", (string?)a["url"]);
    }

    [Fact]
    public async Task Info_OhneBueroDatei_IstNichtVorhanden_NieFehler()
    {
        using var u = new AnleitungUmgebung(null).Starten(anleitungOrdner: null);
        Assert.False((bool?)(await u.Info())["anleitung"]!["vorhanden"]);
    }

    [Theory]
    [InlineData("{ kaputt")]
    [InlineData("""{ "anleitung": "text" }""")]
    [InlineData("""{ "anleitung": { "ordner": "" } }""")]
    [InlineData("""{ "anleitung": { "ordner": 12 } }""")]
    [InlineData("""{ "anleitung": { "hinweis": "nur Hinweis" } }""")]
    public async Task Info_UnbrauchbarerBueroEintrag_IstNichtVorhanden(string buero)
    {
        using var u = new AnleitungUmgebung(buero).Starten(anleitungOrdner: null);
        var a = (await u.Info())["anleitung"]!;
        Assert.False((bool?)a["vorhanden"]);
        Assert.Null(a["ordner"]);
    }

    [Fact]
    public async Task Info_OrdnerAusBuero_LoestUmgebungsvariablenAuf()
    {
        // %TEMP% steht in jeder Windows-Umgebung; der Ordner liegt darunter und passt damit zur Testsperre
        var unter = "hlks_buero_" + Guid.NewGuid().ToString("N")[..8];
        using var u = new AnleitungUmgebung($$"""{ "anleitung": { "ordner": "%TEMP%\\{{unter}}\\anl", "hinweis": "Testhinweis" } }""").Starten(anleitungOrdner: null);
        var erwartet = Path.Combine(Path.GetFullPath(Environment.ExpandEnvironmentVariables("%TEMP%")), unter, "anl");
        try
        {
            var a = (await u.Info())["anleitung"]!;
            Assert.Equal(erwartet, (string?)a["ordner"]);
            Assert.False((bool?)a["vorhanden"]);
            Assert.Equal("Testhinweis", (string?)a["hinweis"]);
            Directory.CreateDirectory(erwartet);
            File.WriteAllText(Path.Combine(erwartet, "index.html"), "<p>x</p>");
            Assert.True((bool?)(await u.Info())["anleitung"]!["vorhanden"]);
        }
        finally { try { Directory.Delete(Path.Combine(Path.GetTempPath(), unter), true); } catch (IOException) { } }
    }

    [Fact]
    public async Task Info_UnbekannteVariableImOrdner_WirdNichtGelesen()
    {
        using var u = new AnleitungUmgebung("""{ "anleitung": { "ordner": "%HLKS_GIBT_ES_NICHT%\\anl" } }""").Starten(anleitungOrdner: null);
        var a = (await u.Info())["anleitung"]!;
        Assert.Null(a["ordner"]);
        Assert.False((bool?)a["vorhanden"]);
    }

    [Fact]
    public async Task Info_Testlauf_LiestKeinenOrdnerAusserhalbTemp()
    {
        // Der Bürowert zeigt auf einen echten Benutzerordner: im Testlauf wird er weder gelesen noch gemeldet
        using var u = new AnleitungUmgebung("""{ "anleitung": { "ordner": "%USERPROFILE%\\HLKS_CAD_anleitung" } }""").Starten(anleitungOrdner: null);
        var a = (await u.Info())["anleitung"]!;
        Assert.Null(a["ordner"]);
        Assert.False((bool?)a["vorhanden"]);
    }

    [Fact]
    public async Task Info_EinstellungUebersteuertBuero()
    {
        using var u = new AnleitungUmgebung("""{ "anleitung": { "ordner": "%TEMP%\\hlks_nicht_hier" } }""").Seite().Starten();
        Assert.Equal(u.Anleitung, (string?)(await u.Info())["anleitung"]!["ordner"]);
    }

    [Fact]
    public async Task Info_UmgebungsvariableHlksAnleitung_UebersteuertBuero()
    {
        using var u = new AnleitungUmgebung("""{ "anleitung": { "ordner": "%TEMP%\\hlks_nicht_hier" } }""").Seite().Starten(anleitungOrdner: null);
        using var env = TestUmgebung.Setzen(("HLKS_ANLEITUNG", u.Anleitung));
        var a = (await u.Info())["anleitung"]!;
        Assert.Equal(u.Anleitung, (string?)a["ordner"]);
        Assert.True((bool?)a["vorhanden"]);
    }

    [Fact]
    public async Task Info_HinweisAusBuero()
    {
        using var u = new AnleitungUmgebung("""{ "anleitung": { "hinweis": " Testhinweis " } }""").Starten();
        Assert.Equal("Testhinweis", (string?)(await u.Info())["anleitung"]!["hinweis"]);
    }

    [Fact]
    public void BueroJson_DesBueros_TraegtOrdnerStattAdresse()
    {
        var datei = Path.Combine(Katalog.StandardOrdner(), "buero.json");
        var a = JsonNode.Parse(File.ReadAllText(datei))!["anleitung"]!;
        Assert.Equal(@"%USERPROFILE%\HLKS_CAD_anleitung", (string?)a["ordner"]);
        Assert.Null(a["url"]);   // die Claude-Seite entfällt
        Assert.Contains("/hilfe/", (string?)a["hinweis"]);
        Assert.Contains("#k-<register>~<knopf>", (string?)a["hinweis"]);
    }
}
