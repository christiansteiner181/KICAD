using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Hlks.Kern;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Hlks.Web.Tests;

/// <summary>
/// Endpunkte des Startbildschirms (S1): /api/zuletzt (Anheften, Entfernen, Prüfen), /api/projekte/ablage, /api/ordner,
/// /api/familien/zuletzt, /api/projekt/vorschau, /api/start/info. Jeder Test hat seinen eigenen Ordner unter %TEMP% mit
/// Projektablage, Familienbibliothek und Liste «zuletzt» – nie die echte Ablage auf W:, nie die echte Liste des Benutzers.
/// </summary>
public class StartApiTests
{
    const string KICAD = @"c_Pläne und Modelle\e_KICAD";

    sealed class Umgebung : IDisposable
    {
        readonly WebApplicationFactory<Program> basis = new();
        WebApplicationFactory<Program>? fabrik;
        public HttpClient Http { get; private set; } = null!;
        public string Wurzel { get; } = Path.Combine(Path.GetTempPath(), "hlks_start_" + Guid.NewGuid().ToString("N")[..10]);
        public string Ablage => Path.Combine(Wurzel, "ablage");
        public string Bibliothek => Path.Combine(Wurzel, "bibliothek");
        public string Zuletzt => Path.Combine(Wurzel, "zuletzt");

        public Umgebung() { Directory.CreateDirectory(Ablage); }

        /// <summary>Server starten (nach dem Anlegen der Testdaten, damit die Suche beim Start sie schon sieht).</summary>
        public Umgebung Starten(Func<Einstellungen, Einstellungen>? aendern = null)
        {
            var e = new Einstellungen("Test", "testpc", Katalog.StandardOrdner(), null)
            {
                ZuletztOrdner = Zuletzt, ProjektAblage = Ablage, Familienbibliothek = Bibliothek, StartZeitlimit = TimeSpan.FromSeconds(10),
            };
            e = aendern?.Invoke(e) ?? e;
            fabrik = basis.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton(e)));
            Http = fabrik.CreateClient();
            return this;
        }

        /// <summary>Legt ein Projekt <ablage>\&lt;objekt&gt;\c_Pläne und Modelle\e_KICAD an (Kern, kein Server) und liefert den Projektordner.</summary>
        public string Projekt(string objekt, string name, string? unter = null)
        {
            var ordner = Path.Combine(Ablage, objekt, unter ?? KICAD);
            var p = Hlks.Kern.Projekt.Anlegen(Katalog.Laden(Katalog.StandardOrdner()), new OrdnerSpeicher(ordner), name, "Test", "testpc", "mensch");
            p.Freigeben();
            return ordner;
        }

        public async Task<(HttpStatusCode Code, JsonNode? Json)> Get(string pfad)
        {
            var a = await Http.GetAsync(pfad);
            var t = await a.Content.ReadAsStringAsync();
            return (a.StatusCode, t.Length == 0 || !(a.Content.Headers.ContentType?.MediaType ?? "").Contains("json") ? null : JsonNode.Parse(t));
        }

        public async Task<(HttpStatusCode Code, JsonNode? Json)> Post(string pfad, object? koerper = null)
        {
            var text = koerper is null ? "{}" : koerper is string s ? s : Hlks.Kern.Json.Text(koerper);
            var a = await Http.PostAsync(pfad, new StringContent(text, Encoding.UTF8, "application/json"));
            var t = await a.Content.ReadAsStringAsync();
            return (a.StatusCode, t.Length == 0 ? null : JsonNode.Parse(t));
        }

        public async Task<(HttpStatusCode Code, JsonNode? Json)> PostPng(string pfad, byte[] daten, string typ = "image/png")
        {
            var inhalt = new ByteArrayContent(daten);
            inhalt.Headers.ContentType = new MediaTypeHeaderValue(typ);
            var a = await Http.PostAsync(pfad, inhalt);
            var t = await a.Content.ReadAsStringAsync();
            return (a.StatusCode, t.Length == 0 ? null : JsonNode.Parse(t));
        }

        public async Task Oeffnen(string ordner)
        {
            var (code, j) = await Post("/api/projekt/oeffnen", new { ordner });
            Assert.True(code == HttpStatusCode.OK, j?.ToJsonString());
        }

        public void Dispose()
        {
            try { Http?.PostAsync("/api/projekt/schliessen", new StringContent("{}", System.Text.Encoding.UTF8, "application/json")).GetAwaiter().GetResult(); } catch { /* egal */ }
            Http?.Dispose();
            fabrik?.Dispose();
            basis.Dispose();
            for (int i = 0; i < 5; i++)
            {
                try { if (Directory.Exists(Wurzel)) Directory.Delete(Wurzel, true); return; }
                catch (IOException) { Thread.Sleep(200); }
                catch (UnauthorizedAccessException) { Thread.Sleep(200); }
            }
        }
    }

    static string Z(JsonNode? n) => n?.GetValue<string>() ?? "";
    static JsonArray Liste(JsonNode? n) => Assert.IsAssignableFrom<JsonArray>(n);

    /// <summary>Ein gültiges PNG (RGBA, einfarbig) – der Server prüft Signatur und Grösse, die Datei ist aber auch wirklich lesbar.</summary>
    static byte[] Png(int breite = 64, int hoehe = 40)
    {
        static uint Crc(ReadOnlySpan<byte> d)
        {
            uint c = 0xFFFFFFFF;
            foreach (var b in d) { c ^= b; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? (c >> 1) ^ 0xEDB88320 : c >> 1; }
            return ~c;
        }
        static void Chunk(Stream s, string typ, byte[] daten)
        {
            Span<byte> l = stackalloc byte[4];
            System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(l, daten.Length);
            s.Write(l);
            var t = Encoding.ASCII.GetBytes(typ);
            var tm = t.Concat(daten).ToArray();
            s.Write(tm);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(l, Crc(tm));
            s.Write(l);
        }
        using var ms = new MemoryStream();
        ms.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var kopf = new byte[13];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(kopf.AsSpan(0), breite);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(kopf.AsSpan(4), hoehe);
        kopf[8] = 8; kopf[9] = 6;
        Chunk(ms, "IHDR", kopf);
        var roh = new byte[(1 + breite * 4) * hoehe];
        for (int y = 0; y < hoehe; y++) for (int x = 0; x < breite; x++) { var o = y * (1 + breite * 4) + 1 + x * 4; roh[o] = 40; roh[o + 1] = 120; roh[o + 2] = 200; roh[o + 3] = 255; }
        using var z = new MemoryStream();
        using (var zl = new System.IO.Compression.ZLibStream(z, System.IO.Compression.CompressionLevel.Fastest, true)) zl.Write(roh);
        Chunk(ms, "IDAT", z.ToArray());
        Chunk(ms, "IEND", []);
        return ms.ToArray();
    }

    // ------------------------------------------------------------------ /api/projekte/ablage

    [Fact]
    public async Task Ablage_ListetNurProjekteInDerFestenTiefe_MitNameAusProjektJson()
    {
        using var u = new Umgebung();
        var o1 = u.Projekt("Worb_BES_Bernstrasse", "Bernstrasse");
        u.Projekt("Bern_ARK_Murtenstrasse", "Murtenstrasse");
        u.Projekt("Tief_Versteckt", "Zu tief", @"extra\c_Pläne und Modelle\e_KICAD");        // eine Ebene zu tief: nie gesucht
        Directory.CreateDirectory(Path.Combine(u.Ablage, "Ohne_Projekt", "c_Pläne und Modelle"));
        Directory.CreateDirectory(Path.Combine(u.Ablage, ".versteckt", "c_Pläne und Modelle", "e_KICAD"));
        File.WriteAllText(Path.Combine(u.Ablage, "lose_datei.txt"), "x");
        u.Starten();

        var (code, j) = await u.Get("/api/projekte/ablage");
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.True(j!["erreichbar"]!.GetValue<bool>());
        Assert.Equal(u.Ablage, Z(j["pfad"]));
        Assert.Equal(KICAD, Z(j["kicadOrdner"]));
        var p = Liste(j["projekte"]);
        Assert.Equal(["Bern_ARK_Murtenstrasse", "Worb_BES_Bernstrasse"], p.Select(x => Z(x!["objekt"])));   // nach Objektordner sortiert
        Assert.Equal(["Murtenstrasse", "Bernstrasse"], p.Select(x => Z(x!["name"])));
        Assert.Equal(o1, Z(p[1]!["ordner"]));
        Assert.False(p[0]!["vorschau"]!.GetValue<bool>());
        Assert.True(DateTimeOffset.TryParse(Z(p[0]!["geaendert"]), out var g) && g > DateTimeOffset.Now.AddHours(-1));
        Assert.True(j["dauerMs"]!.GetValue<long>() >= 0);
    }

    [Fact]
    public async Task Ablage_ZwischenspeicherHaeltZehnMinuten_NeuSuchenUndAblaufZeigenNeues()
    {
        using var u = new Umgebung();
        u.Projekt("A_Eins", "Eins");
        u.Starten(e => e with { });
        Assert.Single(Liste((await u.Get("/api/projekte/ablage")).Json!["projekte"]));

        u.Projekt("B_Zwei", "Zwei");
        var (_, cache) = await u.Get("/api/projekte/ablage");
        Assert.Single(Liste(cache!["projekte"]));                       // noch aus dem Zwischenspeicher
        Assert.True(cache["zwischengespeichert"]!.GetValue<bool>());
        var (_, neu) = await u.Get("/api/projekte/ablage?neu=1");
        Assert.Equal(2, Liste(neu!["projekte"]).Count);                 // «Neu suchen»
        Assert.False(neu["zwischengespeichert"]!.GetValue<bool>());
        u.Projekt("C_Drei", "Drei");
        Assert.Equal(2, Liste((await u.Get("/api/projekte/ablage")).Json!["projekte"]).Count);
        Assert.Equal(3, Liste((await u.Get("/api/projekte/ablage?neu=true")).Json!["projekte"]).Count);
    }

    [Fact]
    public async Task Ablage_ZwischenspeicherLaeuftAb()
    {
        using var u = new Umgebung();
        u.Projekt("A_Eins", "Eins");
        u.Starten(e => e with { AblageZwischenspeicher = TimeSpan.FromMilliseconds(300) });
        Assert.Single(Liste((await u.Get("/api/projekte/ablage")).Json!["projekte"]));
        u.Projekt("B_Zwei", "Zwei");
        await Task.Delay(700);
        Assert.Equal(2, Liste((await u.Get("/api/projekte/ablage")).Json!["projekte"]).Count);
    }

    [Fact]
    public async Task Ablage_NichtErreichbar_IstKeinFehler_SondernEinHinweis()
    {
        using var u = new Umgebung();
        var weg = Path.Combine(u.Wurzel, "gibt-es-nicht", "W");
        u.Starten(e => e with { ProjektAblage = weg });
        var (code, j) = await u.Get("/api/projekte/ablage?neu=1");
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.False(j!["erreichbar"]!.GetValue<bool>());
        Assert.Contains(weg, Z(j["hinweis"]));
        Assert.Contains("nicht erreichbar", Z(j["hinweis"]));
        Assert.Empty(Liste(j["projekte"]));
        Assert.False(Directory.Exists(weg));                              // gelesen, nie angelegt
        // der Rest des Servers geht weiter
        Assert.Equal(HttpStatusCode.OK, (await u.Get("/api/status")).Code);
        Assert.Equal(HttpStatusCode.OK, (await u.Get("/api/zuletzt")).Code);
    }

    [Fact]
    public async Task Ablage_AuchWennDieAblageEineDateiIst_KeinAbsturz()
    {
        using var u = new Umgebung();
        var datei = Path.Combine(u.Wurzel, "datei.txt");
        File.WriteAllText(datei, "x");
        u.Starten(e => e with { ProjektAblage = datei });
        var (code, j) = await u.Get("/api/projekte/ablage?neu=1");
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.False(j!["erreichbar"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Ablage_ImTestlaufNieAusserhalbVonTemp_AuchNichtDieEchteAblageAusBueroJson()
    {
        using var ohne = Hlks.Tests.Gemeinsam.TestUmgebung.Ohne();   // T1: Variable selbst löschen statt voraussetzen
        using var u = new Umgebung();
        // keine Umleitung: buero.json zeigt auf W:\02_Projektdateien Objekte – im Testlauf wird sie nicht gelesen
        u.Starten(e => e with { ProjektAblage = null });
        Assert.True(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HLKS_PROJEKTABLAGE")), "Test setzt voraus, dass HLKS_PROJEKTABLAGE nicht gesetzt ist");
        var (code, j) = await u.Get("/api/projekte/ablage?neu=1");
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.False(j!["erreichbar"]!.GetValue<bool>());
        Assert.Contains("Testlauf", Z(j["hinweis"]));
        Assert.Contains("02_Projektdateien Objekte", Z(j["pfad"]));
        Assert.Empty(Liste(j["projekte"]));
    }

    // ------------------------------------------------------------------ /api/ordner

    [Fact]
    public async Task Ordner_ListetUnterordner_ErkenntKicadProjekteUndObjektordnerMitProjekt()
    {
        using var u = new Umgebung();
        var wurzel = Path.Combine(u.Wurzel, "browser");
        Directory.CreateDirectory(Path.Combine(wurzel, "A_normal"));
        Directory.CreateDirectory(Path.Combine(wurzel, "b_zweiter"));
        File.WriteAllText(Path.Combine(wurzel, "datei.txt"), "x");
        Directory.CreateDirectory(Path.Combine(wurzel, ".versteckt"));
        Directory.CreateDirectory(Path.Combine(wurzel, "$Papierkorb"));
        var direkt = Path.Combine(wurzel, "C_Projekt");
        Directory.CreateDirectory(direkt); File.WriteAllText(Path.Combine(direkt, "projekt.json"), "{}");
        var objekt = Path.Combine(wurzel, "D_Objekt");
        Directory.CreateDirectory(Path.Combine(objekt, KICAD)); File.WriteAllText(Path.Combine(objekt, KICAD, "projekt.json"), "{}");
        u.Starten();

        var (code, j) = await u.Get("/api/ordner?pfad=" + Uri.EscapeDataString(wurzel));
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Equal(wurzel, Z(j!["pfad"]));
        Assert.Equal(Path.GetDirectoryName(wurzel), Z(j["eltern"]));
        var o = Liste(j["ordner"]);
        Assert.Equal(["A_normal", "b_zweiter", "C_Projekt", "D_Objekt"], o.Select(x => Z(x!["name"])));
        Assert.Equal([false, false, true, false], o.Select(x => x!["projekt"]!.GetValue<bool>()));
        Assert.Equal([false, false, false, true], o.Select(x => x!["enthaeltProjekt"]!.GetValue<bool>()));
        Assert.Equal(Path.Combine(wurzel, "C_Projekt"), Z(o[2]!["pfad"]));
        Assert.False(j["istProjekt"]!.GetValue<bool>());
        Assert.Equal(KICAD, Z(j["kicadOrdner"]));

        // ein Projektordner selbst
        var (_, p) = await u.Get("/api/ordner?pfad=" + Uri.EscapeDataString(direkt + "\\"));
        Assert.True(p!["istProjekt"]!.GetValue<bool>());
        Assert.Equal(direkt, Z(p["pfad"]));                                   // Schlusstrenner entfernt
        // ein Objektordner nennt das Projekt darunter
        var (_, q) = await u.Get("/api/ordner?pfad=" + Uri.EscapeDataString(objekt));
        Assert.Equal(Path.Combine(objekt, KICAD), Z(q!["projektUnter"]));
    }

    [Fact]
    public async Task Ordner_OhnePfad_ListetLaufwerkeUndDieProjektablage()
    {
        using var u = new Umgebung().Starten();
        var (code, j) = await u.Get("/api/ordner");
        Assert.Equal(HttpStatusCode.OK, code);
        var l = Liste(j!["laufwerke"]);
        Assert.NotEmpty(l);
        Assert.All(l, x => { Assert.False(string.IsNullOrEmpty(Z(x!["name"]))); Assert.Equal(Z(x["name"]), Z(x["pfad"])); Assert.False(string.IsNullOrEmpty(Z(x["art"]))); });
        Assert.Equal(u.Ablage, Z(j["ablage"]));
        Assert.Equal("", Z(j["pfad"]));
        Assert.Empty(Liste(j["ordner"]));
        // das übergeordnete Verzeichnis einer Wurzel ist die Laufwerksliste
        var wurzel = Path.GetPathRoot(u.Ablage)!;
        var (_, w) = await u.Get("/api/ordner?pfad=" + Uri.EscapeDataString(wurzel));
        Assert.Equal("", Z(w!["eltern"]));
    }

    [Theory]
    [InlineData("gibt-es-hier-nicht-xyz")]
    [InlineData("ä|<>?*")]
    [InlineData("zzz_nichts\\unter\\hier")]
    public async Task Ordner_FehlenderOderUngueltigerPfad_Liefert400MitVorschlag_Nie500(string pfad)
    {
        using var u = new Umgebung().Starten(e => e with { StartZeitlimit = TimeSpan.FromSeconds(20) });
        var (code, j) = await u.Get("/api/ordner?pfad=" + Uri.EscapeDataString(pfad));
        Assert.Equal(HttpStatusCode.BadRequest, code);
        Assert.False(string.IsNullOrEmpty(Z(j!["fehler"])));
        Assert.False(string.IsNullOrEmpty(Z(j["vorschlag"])));
    }

    [Fact]
    public async Task Ordner_EineDateiIstKeinOrdner()
    {
        using var u = new Umgebung().Starten();
        var datei = Path.Combine(u.Ablage, "datei.txt");
        File.WriteAllText(datei, "x");
        var (code, _) = await u.Get("/api/ordner?pfad=" + Uri.EscapeDataString(datei));
        Assert.Equal(HttpStatusCode.BadRequest, code);
    }

    [Fact]
    public async Task OrdnerZeigen_ImTestlaufStartetKeinenExplorer_UndPruefteDenOrdner()
    {
        using var u = new Umgebung().Starten();
        var (code, j) = await u.Post("/api/ordner/zeigen", new { pfad = u.Ablage });
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.False(j!["gezeigt"]!.GetValue<bool>());
        Assert.True(j["testlauf"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.BadRequest, (await u.Post("/api/ordner/zeigen", new { pfad = Path.Combine(u.Wurzel, "weg") })).Code);
        Assert.Equal(HttpStatusCode.BadRequest, (await u.Post("/api/ordner/zeigen", new { })).Code);
    }

    // ------------------------------------------------------------------ /api/zuletzt: Anheften, Entfernen, Prüfen

    [Fact]
    public async Task Zuletzt_OeffnenMerktInDerListeDesRechners_AnheftenEntfernenWirken()
    {
        using var u = new Umgebung();
        var p1 = u.Projekt("Worb_BES_Bernstrasse", "Bernstrasse");
        var p2 = u.Projekt("Bern_ARK_Murtenstrasse", "Murtenstrasse");
        u.Starten();
        Assert.Empty(Liste((await u.Get("/api/zuletzt")).Json));

        await u.Oeffnen(p1);
        await Task.Delay(50);
        await u.Oeffnen(p2);
        Assert.Equal(["testpc.json"], Directory.EnumerateFiles(u.Zuletzt).Select(Path.GetFileName));
        var (_, l) = await u.Get("/api/zuletzt?pruefen=1");
        var z = Liste(l);
        Assert.Equal(["Murtenstrasse", "Bernstrasse"], z.Select(x => Z(x!["name"])));
        Assert.Equal(p2, Z(z[0]!["ordner"]));
        Assert.Equal("Bern_ARK_Murtenstrasse", Z(z[0]!["objekt"]));
        Assert.Equal("testpc", Z(z[0]!["rechner"]));
        Assert.False(z[0]!["angeheftet"]!.GetValue<bool>());
        Assert.True(z[0]!["vorhanden"]!.GetValue<bool>());
        Assert.False(z[0]!["vorschau"]!.GetValue<bool>());
        Assert.True(DateTimeOffset.TryParse(Z(z[0]!["zeitIso"]), out _));

        // anheften: das ältere Projekt steht zuerst
        var (c1, a) = await u.Post("/api/zuletzt/anheften", new { ordner = p1 });
        Assert.Equal(HttpStatusCode.OK, c1);
        Assert.True(a!["angeheftet"]!.GetValue<bool>());
        z = Liste((await u.Get("/api/zuletzt")).Json);
        Assert.Equal(["Bernstrasse", "Murtenstrasse"], z.Select(x => Z(x!["name"])));
        Assert.True(z[0]!["angeheftet"]!.GetValue<bool>());
        Assert.Single(Liste((await u.Get("/api/zuletzt?max=1")).Json));
        // der Status zeigt die Liste nach Zeit (Menüband › Datei)
        Assert.Equal(2, Liste((await u.Get("/api/status")).Json!["zuletzt"]).Count);
        // lösen
        await u.Post("/api/zuletzt/anheften", new { ordner = p1, an = false });
        Assert.All(Liste((await u.Get("/api/zuletzt")).Json), x => Assert.False(x!["angeheftet"]!.GetValue<bool>()));
        // entfernen
        var (c2, e) = await u.Post("/api/zuletzt/entfernen", new { ordner = p1 });
        Assert.Equal(HttpStatusCode.OK, c2);
        Assert.True(e!["entfernt"]!.GetValue<bool>());
        Assert.Equal(["Murtenstrasse"], Liste((await u.Get("/api/zuletzt")).Json).Select(x => Z(x!["name"])));
        // wieder öffnen: erscheint erneut
        await u.Oeffnen(p1);
        Assert.Equal(2, Liste((await u.Get("/api/zuletzt")).Json).Count);
    }

    [Fact]
    public async Task Zuletzt_AngehefteteAusDerListeEinesZweitenRechnersErscheinenAngeheftet()
    {
        using var u = new Umgebung();
        var p1 = u.Projekt("Worb_BES_Bernstrasse", "Bernstrasse");
        u.Starten(e => e with { Geraet = "pc-eins" });
        await u.Oeffnen(p1);
        await u.Post("/api/zuletzt/anheften", new { ordner = p1 });

        // zweiter Rechner: eigener Server mit derselben Ablage der Liste (OneDrive), eigene Datei
        var zweiter = new ZuletztListe(u.Zuletzt, "pc-zwei", true, null, false, "Test") { Zwischenspeicher = TimeSpan.Zero };
        var z = zweiter.Alle().Single();
        Assert.True(z.Angeheftet);
        Assert.Equal("pc-eins", z.Rechner);
        await Task.Delay(30);
        zweiter.Merken(p1, "Bernstrasse");
        Assert.Equal(["pc-eins.json", "pc-zwei.json"], Directory.EnumerateFiles(u.Zuletzt).Select(Path.GetFileName).Order());
        // der erste Server sieht das Öffnen des zweiten (Rechner wechselt), die Anheftung bleibt
        var (_, l) = await u.Get("/api/zuletzt");
        var e0 = Liste(l)[0]!;
        Assert.Equal("pc-zwei", Z(e0["rechner"]));
        Assert.True(e0["angeheftet"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Zuletzt_NichtMehrVorhandenerOrdnerWirdMitVorhandenFalseGemeldet()
    {
        using var u = new Umgebung();
        var p1 = u.Projekt("Worb_BES_Bernstrasse", "Bernstrasse");
        u.Starten();
        await u.Oeffnen(p1);
        await u.Post("/api/projekt/schliessen");
        Directory.Delete(Path.Combine(u.Ablage, "Worb_BES_Bernstrasse"), true);
        var (_, l) = await u.Get("/api/zuletzt?pruefen=1");
        var e = Liste(l).Single()!;
        Assert.False(e["vorhanden"]!.GetValue<bool>());
        Assert.False(e["vorschau"]!.GetValue<bool>());
        // ohne «pruefen» keine NAS-Zugriffe: die Felder fehlen
        Assert.Null(Liste((await u.Get("/api/zuletzt")).Json).Single()!["vorhanden"]);
    }

    [Fact]
    public async Task Zuletzt_FehlerWerden400MitVorschlag()
    {
        using var u = new Umgebung().Starten();
        foreach (var pfad in new[] { "/api/zuletzt/anheften", "/api/zuletzt/entfernen" })
        {
            var (code, j) = await u.Post(pfad, new { });
            Assert.Equal(HttpStatusCode.BadRequest, code);
            Assert.False(string.IsNullOrEmpty(Z(j!["fehler"])));
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await u.Post("/api/zuletzt/anheften", "kein json")).Code);
        Assert.Equal(HttpStatusCode.OK, (await u.Get("/api/zuletzt?max=-5")).Code);
        Assert.True((int)(await u.Get("/api/zuletzt?max=abc")).Code < 500);       // nicht lesbarer Wert: 400 oder Vorgabe, nie 500
    }

    [Fact]
    public async Task Zuletzt_ListeNichtSchreibbar_MeldetFehlerBeimAnheften_NichtBeimOeffnen()
    {
        using var u = new Umgebung();
        var p1 = u.Projekt("Worb_BES_Bernstrasse", "Bernstrasse");
        // «Ordner» der Liste ist eine Datei: Schreiben unmöglich
        var datei = Path.Combine(u.Wurzel, "blockiert");
        File.WriteAllText(datei, "x");
        u.Starten(e => e with { ZuletztOrdner = datei });
        await u.Oeffnen(p1);                                           // Komfortfunktion: das Öffnen gelingt trotzdem
        var (code, j) = await u.Post("/api/zuletzt/anheften", new { ordner = p1 });
        Assert.Equal(HttpStatusCode.BadRequest, code);
        Assert.Contains("nicht gespeichert", Z(j!["fehler"]));
    }

    // ------------------------------------------------------------------ Vorschaubild

    [Fact]
    public async Task Vorschau_SpeichernLegtPngUndInfoImProjektordnerAb_UndDieListenZeigenSie()
    {
        using var u = new Umgebung();
        var p1 = u.Projekt("Worb_BES_Bernstrasse", "Bernstrasse");
        u.Starten();
        await u.Oeffnen(p1);
        var png = Png(640, 400);

        var (code, j) = await u.PostPng("/api/projekt/vorschau?stand=abc123&ort=" + Uri.EscapeDataString(p1), png);
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.True(j!["gespeichert"]!.GetValue<bool>());
        Assert.Equal(640, j["breite"]!.GetValue<int>());
        Assert.Equal(png, File.ReadAllBytes(Path.Combine(p1, "vorschau.png")));
        var info = JsonNode.Parse(File.ReadAllText(Path.Combine(p1, "vorschau.json")))!;
        Assert.Equal("Bernstrasse", Z(info["name"]));
        Assert.Equal("abc123", Z(info["stand"]));
        Assert.Empty(Directory.EnumerateFiles(p1, ".*.tmp"));                   // atomar, keine Reste

        // das Projekt bleibt benutzbar: die Dateien im Ordner stören die Ablage nicht
        Assert.Equal(HttpStatusCode.OK, (await u.Get("/api/modell")).Code);

        // Liste «zuletzt» und «Alle Projekte» kennen das Bild
        var z = Liste((await u.Get("/api/zuletzt?pruefen=1")).Json).Single()!;
        Assert.True(z["vorschau"]!.GetValue<bool>());
        Assert.True(z["vorschauV"]!.GetValue<long>() > 0);
        var a = Liste((await u.Get("/api/projekte/ablage")).Json!["projekte"]).Single()!;   // Speichern macht den Zwischenspeicher ungültig
        Assert.True(a["vorschau"]!.GetValue<bool>());

        // das Bild selbst
        var r = await u.Http.GetAsync("/api/projekt/vorschau?ordner=" + Uri.EscapeDataString(p1));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("image/png", r.Content.Headers.ContentType!.MediaType);
        Assert.Equal(png, await r.Content.ReadAsByteArrayAsync());
        Assert.Equal("no-cache", r.Headers.CacheControl?.ToString());
        Assert.NotNull(r.Content.Headers.LastModified);
        var bedingt = new HttpRequestMessage(HttpMethod.Get, "/api/projekt/vorschau?ordner=" + Uri.EscapeDataString(p1));
        bedingt.Headers.IfModifiedSince = r.Content.Headers.LastModified;
        Assert.Equal(HttpStatusCode.NotModified, (await u.Http.SendAsync(bedingt)).StatusCode);

        // nochmal speichern ersetzt das Bild
        var neu = Png(640, 400)[..^0].ToArray(); neu[40] ^= 0x01;
        Assert.Equal(HttpStatusCode.OK, (await u.PostPng("/api/projekt/vorschau?stand=def", neu)).Code);   // ohne «ort» geht es auch
        Assert.Equal(neu, File.ReadAllBytes(Path.Combine(p1, "vorschau.png")));
    }

    [Fact]
    public async Task Vorschau_Fehler_KeinProjekt409_KeinPng400_ZuGross400_FalschesProjekt400()
    {
        using var u = new Umgebung();
        var p1 = u.Projekt("Worb_BES_Bernstrasse", "Bernstrasse");
        u.Starten();
        // kein Projekt offen
        var (c0, j0) = await u.PostPng("/api/projekt/vorschau?stand=x", Png());
        Assert.Equal(HttpStatusCode.Conflict, c0);
        Assert.False(string.IsNullOrEmpty(Z(j0!["vorschlag"])));

        await u.Oeffnen(p1);
        string Ziel(string ort = "") => "/api/projekt/vorschau?stand=x" + (ort == "" ? "" : "&ort=" + Uri.EscapeDataString(ort));
        // kein PNG
        // als text/plain weist schon der Herkunftsschutz ab (415, Sicherheitsprüfung 3c B1); als image/png prüft der Endpunkt den Inhalt (400)
        var (c415, _) = await u.PostPng(Ziel(), Encoding.UTF8.GetBytes("das ist kein Bild, nur Text mit genug Zeichen für den Kopf."), "text/plain");
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, c415);
        var (c1, j1) = await u.PostPng(Ziel(), Encoding.UTF8.GetBytes("das ist kein Bild, nur Text mit genug Zeichen für den Kopf."), "image/png");
        Assert.Equal(HttpStatusCode.BadRequest, c1);
        Assert.Contains("PNG", Z(j1!["fehler"]));
        Assert.Equal(HttpStatusCode.BadRequest, (await u.PostPng(Ziel(), [])).Code);                      // leer
        Assert.Equal(HttpStatusCode.BadRequest, (await u.PostPng(Ziel(), Png(8, 8))).Code);               // zu klein
        Assert.Equal(HttpStatusCode.BadRequest, (await u.PostPng(Ziel(), Png(5000, 16))).Code);           // zu breit
        // zu gross (über 4 MB)
        var gross = Png(); Array.Resize(ref gross, StartDienst.VorschauMaxBytes + 10);
        var (c2, j2) = await u.PostPng(Ziel(), gross);
        Assert.Equal(HttpStatusCode.BadRequest, c2);
        Assert.Contains("zu gross", Z(j2!["fehler"]));
        // Bild eines anderen Projekts
        var (c3, j3) = await u.PostPng(Ziel(Path.Combine(u.Ablage, "Anderes", KICAD)), Png());
        Assert.Equal(HttpStatusCode.BadRequest, c3);
        Assert.Contains("gewechselt", Z(j3!["fehler"]));
        Assert.False(File.Exists(Path.Combine(p1, "vorschau.png")));         // nichts geschrieben
    }

    [Fact]
    public async Task Vorschau_BildLesen_NurVonKicadProjektordnern_404SonstUndOhneBild()
    {
        using var u = new Umgebung();
        var p1 = u.Projekt("Worb_BES_Bernstrasse", "Bernstrasse");
        u.Starten();
        Task<HttpStatusCode> Hole(string? ordner) => u.Http.GetAsync("/api/projekt/vorschau" + (ordner is null ? "" : "?ordner=" + Uri.EscapeDataString(ordner))).ContinueWith(t => t.Result.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, await Hole(p1));                 // Projekt ohne Bild
        Assert.Equal(HttpStatusCode.NotFound, await Hole(null));
        Assert.Equal(HttpStatusCode.NotFound, await Hole(""));
        Assert.Equal(HttpStatusCode.NotFound, await Hole(Path.Combine(u.Wurzel, "gibt-es-nicht")));
        // ein Ordner mit vorschau.png, aber ohne projekt.json ist kein Projekt: kein Dateiabruf beliebiger Ordner
        var fremd = Path.Combine(u.Wurzel, "fremd");
        Directory.CreateDirectory(fremd);
        File.WriteAllBytes(Path.Combine(fremd, "vorschau.png"), Png());
        Assert.Equal(HttpStatusCode.NotFound, await Hole(fremd));
        Assert.Equal(HttpStatusCode.NotFound, await Hole("\0"));
    }

    // ------------------------------------------------------------------ /api/familien/zuletzt

    static void FamilieSchreiben(string bib, string kategorie, string id, string name, string zeit, string projekt, string nummer, string ordner, string status = "freigegeben", int version = 1, string hersteller = "Fiktiv AG")
    {
        var dir = Path.Combine(bib, kategorie, $"{name.Replace(' ', '_')}__{id}");
        Directory.CreateDirectory(dir);
        var log = new JsonObject
        {
            ["format"] = 1, ["id"] = id,
            ["eintraege"] = new JsonArray(new JsonObject
            {
                ["art"] = "version", ["version"] = version, ["datei"] = $"versionen/v{version:D4}.familie.json", ["pruefsumme"] = "p" + id, ["status"] = status,
                ["name"] = name, ["kategorie"] = kategorie, ["hersteller"] = hersteller, ["typen"] = 2,
                ["projekt"] = new JsonObject { ["id"] = "pj-" + id, ["name"] = projekt, ["nummer"] = nummer, ["ordner"] = ordner },
                ["akteur"] = "CS", ["akteurArt"] = "mensch", ["zeit"] = zeit,
            }),
        };
        File.WriteAllText(Path.Combine(dir, "log.json"), log.ToJsonString());
    }

    [Fact]
    public async Task Familien_ZuletztBearbeitet_NeuesteZuerst_MitProjektDerLetztenAenderung()
    {
        using var u = new Umgebung();
        FamilieSchreiben(u.Bibliothek, "Heizkoerper", "fam-alt", "Heizkörper alt", "2026-10-01T10:00:00.000Z", "Altbau", "2025-001", @"W:\A\e_KICAD", "entwurf");
        FamilieSchreiben(u.Bibliothek, "Heizkoerper", "fam-neu", "Heizkörper neu", "2026-10-18T08:30:00.000Z", "Bernstrasse", "2026-007", @"W:\02_Projektdateien Objekte\Worb_BES\c_Pläne und Modelle\e_KICAD", version: 3);
        FamilieSchreiben(u.Bibliothek, "Sanitaer", "fam-mitte", "Spültisch", "2026-10-10T12:00:00.000Z", "Ostring", "2026-011", @"W:\O\e_KICAD", hersteller: "Muster GmbH");
        u.Starten();

        var (code, j) = await u.Get("/api/familien/zuletzt?n=2");
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.True(j!["erreichbar"]!.GetValue<bool>());
        Assert.False(j["offline"]!.GetValue<bool>());
        Assert.Equal(3, j["anzahl"]!.GetValue<int>());
        Assert.Equal(u.Bibliothek, Z(j["bibliothek"]!["pfad"]));
        var f = Liste(j["familien"]);
        Assert.Equal(["Heizkörper neu", "Spültisch"], f.Select(x => Z(x!["name"])));
        var neu = f[0]!;
        Assert.Equal("fam-neu", Z(neu["id"]));
        Assert.Equal("Heizkoerper", Z(neu["kategorie"]));
        Assert.Equal("Fiktiv AG", Z(neu["hersteller"]));
        Assert.Equal("freigegeben", Z(neu["status"]));
        Assert.Equal(3, neu["version"]!.GetValue<int>());
        Assert.Equal("Bernstrasse", Z(neu["projekt"]));
        Assert.Equal("2026-007", Z(neu["projektNummer"]));
        Assert.Equal(@"W:\02_Projektdateien Objekte\Worb_BES\c_Pläne und Modelle\e_KICAD", Z(neu["projektOrdner"]));
        Assert.True(DateTimeOffset.TryParse(Z(neu["zeitIso"]), out var t) && t.Year == 2026 && t.Month == 10);
        // alle drei, wenn n gross ist; Standard 12
        Assert.Equal(3, Liste((await u.Get("/api/familien/zuletzt")).Json!["familien"]).Count);
        Assert.Single(Liste((await u.Get("/api/familien/zuletzt?n=1")).Json!["familien"]));
    }

    [Fact]
    public async Task Familien_BibliothekFehltOderIstLeer_ErgibtListeOhneFehler()
    {
        using var u = new Umgebung();
        u.Starten();   // Bibliotheksordner existiert nicht
        var (code, j) = await u.Get("/api/familien/zuletzt");
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Empty(Liste(j!["familien"]));
        // leer, aber vorhanden
        Directory.CreateDirectory(u.Bibliothek);
        var (_, l) = await u.Get("/api/familien/zuletzt");
        Assert.True(l!["erreichbar"]!.GetValue<bool>());
        Assert.Empty(Liste(l["familien"]));
    }

    [Fact]
    public async Task Familien_BibliothekAusserhalbVonTempWirdImTestlaufNichtGelesen()
    {
        using var u = new Umgebung();
        u.Starten(e => e with { Familienbibliothek = @"W:\03_CAD Bibliotheken\d_KICAD Familien" });
        var (code, j) = await u.Get("/api/familien/zuletzt");
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.False(j!["erreichbar"]!.GetValue<bool>());
        Assert.Contains("Testlauf", Z(j["hinweis"]));
        Assert.Empty(Liste(j["familien"]));
    }

    [Fact]
    public async Task Familien_KaputtesLogUndOrdnerOhneLogStoerenNicht()
    {
        using var u = new Umgebung();
        FamilieSchreiben(u.Bibliothek, "Heizkoerper", "fam-ok", "Heizkörper ok", "2026-10-18T08:30:00.000Z", "Bernstrasse", "2026-007", @"W:\A\e_KICAD");
        var kaputt = Path.Combine(u.Bibliothek, "Heizkoerper", "Kaputt__fam-x"); Directory.CreateDirectory(kaputt);
        File.WriteAllText(Path.Combine(kaputt, "log.json"), "{ nicht json");
        Directory.CreateDirectory(Path.Combine(u.Bibliothek, "Heizkoerper", "OhneLog"));
        Directory.CreateDirectory(Path.Combine(u.Bibliothek, "_unterlagen"));
        u.Starten();
        var (code, j) = await u.Get("/api/familien/zuletzt");
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Equal(["Heizkörper ok"], Liste(j!["familien"]).Select(x => Z(x!["name"])));
    }

    // ------------------------------------------------------------------ Programmstand, Gesamtbild

    [Fact]
    public async Task Info_LiefertVersionStandRechnerUndOrteDerListen()
    {
        using var u = new Umgebung().Starten(e => e with { Geraet = "hk-pc7", Akteur = "CS" });
        var (code, j) = await u.Get("/api/start/info");
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Equal("KICAD", Z(j!["programm"]));
        Assert.Matches(@"^\d+\.\d+\.\d+$", Z(j["version"]));
        Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}$", Z(j["stand"]));          // Build-Datum
        Assert.Equal("hk-pc7", Z(j["rechner"]));
        Assert.Equal("CS", Z(j["akteur"]));
        Assert.Equal("Umleitung", Z(j["zuletztQuelle"]));
        Assert.Equal(u.Zuletzt, Z(j["zuletztOrt"]));
        Assert.Equal(u.Ablage, Z(j["projektablage"]));
        Assert.Equal(KICAD, Z(j["kicadOrdner"]));
    }

    [Fact]
    public async Task AlleStartEndpunkteAntwortenOhneProjekt_NieMit500()
    {
        using var u = new Umgebung().Starten();
        foreach (var pfad in new[] { "/api/zuletzt", "/api/zuletzt?pruefen=1", "/api/projekte/ablage", "/api/ordner", "/api/familien/zuletzt", "/api/start/info", "/api/projekt/vorschau" })
        {
            var a = await u.Http.GetAsync(pfad);
            Assert.True((int)a.StatusCode < 500, $"{pfad}: {(int)a.StatusCode}");
        }
    }

    [Fact]
    public async Task Oberflaeche_StartbildschirmDateienWerdenAusgeliefert()
    {
        using var u = new Umgebung().Starten();
        foreach (var pfad in new[] { "/css/start.css", "/js/oberflaeche/startbildschirm.js", "/js/oberflaeche/start-text.js", "/js/oberflaeche/ordner-browser.js", "/js/oberflaeche/vorschau.js", "/js/oberflaeche/vorschau-logik.js" })
        {
            var a = await u.Http.GetAsync(pfad);
            Assert.Equal(HttpStatusCode.OK, a.StatusCode);
            Assert.True((await a.Content.ReadAsStringAsync()).Length > 200, pfad);
        }
        var index = await u.Http.GetStringAsync("/");
        Assert.Contains("css/start.css", index);
    }
}
