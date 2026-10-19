using System.Net;
using System.Text.Json.Nodes;
using Hlks.Kern;

namespace Hlks.Web.Tests;

/// <summary>
/// KI-Aufträge (Modul 2, B5): Start, Status, Abbruch, Zeitlimit, Protokoll, MCP-Konfiguration und Ergebnis – mit der
/// Attrappe statt claude.exe. Kein echter Claude-Lauf. Die Attrappe ruft den echten MCP-Server (Hlks.Mcp) mit --art ki auf,
/// so dass die Änderung der KI als Paket in die Ablage kommt und der Server sie über den Abgleich sieht.
/// </summary>
public sealed class OkLauf : IAsyncLifetime
{
    public AuftragServer Server = null!;
    public HttpStatusCode StartCode;
    public JsonNode StartAntwort = null!;
    public TimeSpan StartDauer;
    public JsonNode Ende = null!;
    public string Id = "";
    public List<JsonObject> Protokoll = [];
    public JsonObject Info = null!;
    string? alterKey, alterToken, alterClaudecode;

    public async Task InitializeAsync()
    {
        // Umgebung des Servers mit Schlüssel und CLAUDECODE: der Kindprozess darf sie nicht erben
        alterKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        alterToken = Environment.GetEnvironmentVariable("ANTHROPIC_AUTH_TOKEN");
        alterClaudecode = Environment.GetEnvironmentVariable("CLAUDECODE");
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", "sk-ant-test-nicht-weitergeben");
        Environment.SetEnvironmentVariable("ANTHROPIC_AUTH_TOKEN", "token-test-nicht-weitergeben");
        Environment.SetEnvironmentVariable("CLAUDECODE", "1");
        Server = new AuftragServer();
        await Server.ProjektAnlegen();
        var uhr = System.Diagnostics.Stopwatch.StartNew();
        var (code, j) = await Server.Starten("ok");
        StartDauer = uhr.Elapsed;
        StartCode = code;
        StartAntwort = j!;
        Id = AuftragServer.Id(j);
        Ende = await Server.Warten(Id, AuftragServer.Beendet, TimeSpan.FromMinutes(2), "Ende des Laufs");
        // Ereignisse können dem Ende knapp hinterherlaufen
        await Task.Delay(500);
        Protokoll = Server.Protokoll(Id);
        Info = Protokoll.First(x => x["subtype"]?.GetValue<string>() == "attrappe");
    }

    public Task DisposeAsync()
    {
        Server.Dispose();
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", alterKey);
        Environment.SetEnvironmentVariable("ANTHROPIC_AUTH_TOKEN", alterToken);
        Environment.SetEnvironmentVariable("CLAUDECODE", alterClaudecode);
        return Task.CompletedTask;
    }
}

public class AuftragTests(OkLauf lauf) : IClassFixture<OkLauf>
{
    static string Z(JsonNode? n) => n?.GetValue<string>() ?? "";

    // ------------------------------------------------------------------ Start, Status, Ergebnis

    [Fact]
    public void Start_AntwortetSofortMit202UndAuftragsId()
    {
        Assert.Equal(HttpStatusCode.Accepted, lauf.StartCode);
        Assert.Matches(@"^\d{8}-\d{6}-[0-9a-f]{4}$", lauf.Id);
        Assert.Equal("familie_aus_unterlagen", Z(lauf.StartAntwort["art"]));
        Assert.Contains(Z(lauf.StartAntwort["status"]), new[] { "wartet", "laeuft" });
        Assert.True(lauf.StartDauer < TimeSpan.FromSeconds(10), $"Antwort nach {lauf.StartDauer.TotalSeconds:0.0} s");
    }

    [Fact]
    public void Status_Fertig_MitStartEndeDauerUndSchritten()
    {
        var s = lauf.Ende;
        Assert.Equal("fertig", Z(s["status"]));
        Assert.Null(s["fehler"]);
        Assert.True(DateTimeOffset.TryParse(Z(s["start"]), out var start));
        Assert.True(DateTimeOffset.TryParse(Z(s["ende"]), out var ende));
        Assert.True(ende > start);
        Assert.True(s["dauerSekunden"]!.GetValue<double>() > 0);
        Assert.Equal(20, s["zeitlimitMinuten"]!.GetValue<double>());
        var schritte = s["schritte"]!.AsArray().Select(x => Z(x!["text"])).ToList();
        Assert.Contains(schritte, x => x.StartsWith("MCP-Server hlks: connected"));
        Assert.Contains(schritte, x => x.StartsWith("familie_entwurf "));
        Assert.Contains(schritte, x => x.StartsWith("KI: Ich lese die Unterlagen"));
        Assert.True(s["schrittAnzahl"]!.GetValue<int>() >= schritte.Count);
        Assert.Contains("Heizkoerper", Z(s["eingabe"]!["kategorie"]));
    }

    [Fact]
    public async Task Liste_ZeigtDenAuftragOhneBericht()
    {
        var (code, j) = await lauf.Server.Get("/api/auftraege");
        Assert.Equal(HttpStatusCode.OK, code);
        var a = j!["auftraege"]!.AsArray().Single(x => Z(x!["id"]) == lauf.Id)!;
        Assert.Equal("fertig", Z(a["status"]));
        Assert.Null(a["ergebnis"]!["bericht"]);
        Assert.Single(a["ergebnis"]!["familien"]!.AsArray());
    }

    [Fact]
    public void Ergebnis_BerichtUndFamilieAusDenPaketenDerKi()
    {
        var erg = lauf.Ende["ergebnis"]!;
        Assert.Equal("Bericht der Attrappe: Familie «Attrappe-Heizkörper» als Entwurf angelegt.", Z(erg["bericht"]));
        var fam = erg["familien"]!.AsArray();
        var f = Assert.Single(fam)!;
        Assert.Equal("angelegt", Z(f["aktion"]));
        Assert.Equal("FAM-1", Z(f["kennzeichen"]));
        Assert.Equal("Attrappe-Heizkörper", Z(f["name"]));
        Assert.Equal("Heizkoerper", Z(f["kategorie"]));
        Assert.Equal("entwurf", Z(f["status"]));
        Assert.Equal(3, f["typen"]!.AsArray().Count);
        Assert.True(f["imModell"]!.GetValue<bool>(), "der Server hat die Familie über den Abgleich übernommen");
        Assert.Equal(0, f["offeneKonflikte"]!.GetValue<int>());
        Assert.Equal(1, erg["pakete"]!.GetValue<int>());
        Assert.Equal(1, erg["befehle"]!["familie_entwurf"]!.GetValue<int>());
        Assert.Equal(3, erg["runden"]!.GetValue<int>());
        Assert.True(erg["internet"]!.GetValue<bool>());
        Assert.Empty(erg["abgerufeneUnterlagen"]!.AsArray());
        Assert.True(lauf.Ende["internet"]!.GetValue<bool>());
        Assert.Null(erg["warnung"]);
    }

    [Fact]
    public async Task AenderungDerKi_KommtImWebserverAn_MitHerkunftKi()
    {
        var (code, j) = await lauf.Server.Post("/api/abfrage/familien");
        Assert.Equal(HttpStatusCode.OK, code);
        var namen = j!["familien"]!.AsArray().Select(x => Z(x!["name"])).ToList();
        Assert.Equal(["Attrappe-Heizkörper"], namen);

        var herkunft = await lauf.Server.Dienst.Mit(p =>
        {
            var e = p.Modell.DerKlasse("Familie").Single();
            return (e.Angelegt?.Art, e.Angelegt?.Akteur);
        });
        Assert.Equal(("ki", "KI"), herkunft);

        // und im Ereignisstrom: die Änderung kam als Änderung der KI
        var ereignisse = lauf.Server.Ereignisse.ToList();
        Assert.Contains(ereignisse, o => Z(o["art"]) == "aenderung" && Z(o["akteurArt"]) == "ki" && Z(o["akteur"]) == "KI");
    }

    [Fact]
    public void Ereignisstrom_MeldetFortschrittUndEnde()
    {
        var auftraege = lauf.Server.Ereignisse.Where(o => Z(o["art"]) == "auftrag").Select(o => o["auftrag"]!).Where(a => Z(a["id"]) == lauf.Id).ToList();
        Assert.NotEmpty(auftraege);
        var stati = auftraege.Select(a => Z(a["status"])).ToList();
        Assert.Contains(stati.First(), new[] { "wartet", "laeuft" });
        Assert.Equal("fertig", stati.Last());
        Assert.Contains("laeuft", stati);
        Assert.Contains(auftraege, a => a["schritte"]!.AsArray().Count > 0);
        Assert.NotNull(auftraege.Last()["ergebnis"]);
    }

    // ------------------------------------------------------------------ Protokoll

    [Fact]
    public void Protokoll_StehtImProjektordnerUnterAuftraege()
    {
        var log = lauf.Server.LogPfad(lauf.Id);
        Assert.True(File.Exists(log), log);
        Assert.Equal(Path.Combine(lauf.Server.Ordner, "auftraege", lauf.Id + ".log"), Z(lauf.Ende["protokoll"]));
        // jede Zeile ist JSON; Claude-Zeilen unverändert, eigene Zeilen als type hlks
        var typen = lauf.Protokoll.Select(x => Z(x["type"])).ToList();
        Assert.Equal("hlks", typen[0]);
        Assert.Equal("Auftrag gestartet", Z(lauf.Protokoll[0]["text"]));
        Assert.Contains("system", typen);
        Assert.Contains("assistant", typen);
        Assert.Contains("user", typen);
        Assert.Contains("result", typen);
        Assert.Contains(lauf.Protokoll, x => Z(x["text"]).StartsWith("Auftrag beendet: fertig"));
        Assert.True(File.Exists(Path.Combine(lauf.Server.Ordner, "auftraege", lauf.Id + ".prompt.md")));
    }

    // ------------------------------------------------------------------ Aufruf von Claude

    [Fact]
    public void Schalter_StreamJsonOhneSitzungOhneEingebauteWerkzeuge()
    {
        var o = lauf.Info["optionen"]!.AsObject();
        foreach (var schalter in new[] { "-p", "--verbose", "--strict-mcp-config", "--no-session-persistence", "--disable-slash-commands" })
            Assert.True(o.ContainsKey(schalter), schalter + " fehlt");
        Assert.Equal("stream-json", Z(o["--output-format"]![0]));
        Assert.Equal("dontAsk", Z(o["--permission-mode"]![0]));
        Assert.Equal("WebSearch", Z(o["--tools"]![0]));               // Vorgabe internet = true: nur die Websuche, kein WebFetch
        Assert.Equal("local", Z(o["--setting-sources"]![0]));      // keine Benutzereinstellungen (Plugins, Hooks)
        Assert.False(o.ContainsKey("--max-turns"));               // gibt es in «claude --help» nicht
        Assert.False(o.ContainsKey("--add-dir"));
        // der Auftrag kommt über die Standardeingabe, nicht als Argument
        var args = lauf.Info["argumente"]!.AsArray().Select(x => Z(x)).ToList();
        Assert.DoesNotContain(args, x => x.Contains("Auftrag:"));
    }

    [Fact]
    public void ErlaubtSindNurDieListeterWerkzeugeDesServersHlks()
    {
        var erlaubt = Z(lauf.Info["optionen"]!["--allowedTools"]![0]).Split(',');
        Assert.All(erlaubt, x => Assert.True(x.StartsWith("mcp__hlks__") || x == "WebSearch", x));
        Assert.Contains("WebSearch", erlaubt);
        Assert.DoesNotContain("WebFetch", erlaubt);                   // Abrufe nur über unterlage_aus_url
        foreach (var allgemein in new[] { "projekt_ueberblick", "elemente_suchen", "element_lesen", "raum_inhalt", "system_lesen", "mengen", "raumliste", "strangliste", "aenderungen", "warum", "datenpunkte", "zeitreihe", "faehigkeiten" })
            Assert.DoesNotContain("mcp__hlks__" + allgemein, erlaubt);
        Assert.Contains("mcp__hlks__unterlage_aus_url", erlaubt);
        Assert.Contains("mcp__hlks__familie_masse", erlaubt);
        Assert.Contains("mcp__hlks__familie_entwurf", erlaubt);
        Assert.Contains("mcp__hlks__unterlagen_lesen", erlaubt);
        Assert.Contains("mcp__hlks__unterlage_bild", erlaubt);
        Assert.Contains("mcp__hlks__befunde", erlaubt);
        foreach (var verboten in new[] { "familie_freigeben", "konflikt_entscheiden", "wert_bestaetigen", "familie_speichern", "familie_platzieren",
                                         "element_anlegen", "element_aendern", "element_loeschen", "hkap_senden", "unterlage_einfuegen" })
            Assert.DoesNotContain("mcp__hlks__" + verboten, erlaubt);
    }

    [Fact]
    public void McpKonfiguration_EnthaeltNurHlksMitAkteurKiUndArtKi()
    {
        var k = lauf.Info["mcpKonfiguration"]!["mcpServers"]!.AsObject();
        var name = Assert.Single(k).Key;
        Assert.Equal("hlks", name);
        var hlks = k["hlks"]!;
        Assert.Equal(AuftragServer.Dotnet, Z(hlks["command"]));
        var args = hlks["args"]!.AsArray().Select(x => Z(x)).ToList();
        Assert.EndsWith("Hlks.Mcp.dll", args[0]);
        string Nach(string schalter) => args[args.IndexOf(schalter) + 1];
        Assert.Equal("ki", Nach("--art"));
        Assert.Equal("KI", Nach("--akteur"));
        Assert.Equal(lauf.Server.Ordner, Nach("--projekt"));
        Assert.Equal("testpc", Nach("--geraet"));
        Assert.True(Directory.Exists(Nach("--katalog")));
        // die Konfiguration kommt per --mcp-config, und nur diese
        Assert.Single(lauf.Info["optionen"]!["--mcp-config"]!.AsArray());
    }

    [Fact]
    public void Umgebung_ApiSchluesselUndClaudecodeSindEntfernt()
    {
        // der Test hat sie gesetzt – sonst sagte die Prüfung nichts
        Assert.Equal("sk-ant-test-nicht-weitergeben", Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"));
        Assert.False(lauf.Info["apiKeyGesetzt"]!.GetValue<bool>());
        Assert.False(lauf.Info["authTokenGesetzt"]!.GetValue<bool>());
        Assert.False(lauf.Info["claudecodeGesetzt"]!.GetValue<bool>());
        Assert.Equal("60000", Z(lauf.Info["maxMcpOutputTokens"]));
    }

    [Fact]
    public void Arbeitsordner_IstLeerUndNichtImProjekt_UndWirdAufgeraeumt()
    {
        var ordner = Z(lauf.Info["arbeitsordner"]);
        Assert.StartsWith(lauf.Server.ArbeitsWurzel, ordner, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(lauf.Server.Ordner, ordner, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, lauf.Info["arbeitsordnerEintraege"]!.GetValue<int>());
        Assert.False(Directory.Exists(Path.GetDirectoryName(ordner)), "Arbeitsordner und MCP-Konfiguration nach dem Lauf gelöscht");
    }

    [Fact]
    public void Auftragstext_NenntUnterlagenKategorieHinweisUndSchema()
    {
        var prompt = Z(lauf.Info["prompt"]);
        Assert.Contains("minimal_ifc4.ifc", prompt);
        Assert.Contains("Heizkoerper", prompt);
        Assert.Contains("Bitte genau lesen.", prompt);
        Assert.Contains("keine – lege eine neue Familie an", prompt);
        Assert.Contains("hlks-familie.schema.json", prompt);            // Schema eingesetzt
        Assert.Contains("Internet-Recherche: erlaubt", prompt);
        Assert.Contains("## Internet-Recherche (erlaubt)", prompt);
        Assert.DoesNotContain("<!--", prompt);
        Assert.DoesNotContain("{{", prompt);
        Assert.Contains("Nichts erfinden", prompt);
        Assert.Equal(prompt, File.ReadAllText(Path.Combine(lauf.Server.Ordner, "auftraege", lauf.Id + ".prompt.md")));
    }

    // ------------------------------------------------------------------ Anfragen, die abgelehnt werden

    [Fact]
    public async Task UngueltigeAnfragen_BekommenKlareMeldung()
    {
        var s = lauf.Server;
        var u = new JsonArray(AuftragServer.Unterlage());

        var (c1, j1) = await s.Post("/api/auftraege", new JsonObject { ["art"] = "kaffee", ["unterlagen"] = u.DeepClone() });
        Assert.Equal(HttpStatusCode.BadRequest, c1);
        Assert.Contains("familie_aus_unterlagen", j1!["vorschlag"]!.GetValue<string>());

        var (c2, _) = await s.Post("/api/auftraege", new JsonObject { ["art"] = "familie_aus_unterlagen" });
        Assert.Equal(HttpStatusCode.BadRequest, c2);

        var (c3, j3) = await s.Post("/api/auftraege", new JsonObject { ["art"] = "familie_aus_unterlagen", ["unterlagen"] = new JsonArray("gibt_es_nicht.pdf") });
        Assert.Equal(HttpStatusCode.BadRequest, c3);
        Assert.Contains("gibt es nicht", Z(j3!["fehler"]));

        var (c4, j4) = await s.Post("/api/auftraege", new JsonObject { ["art"] = "familie_aus_unterlagen", ["unterlagen"] = u.DeepClone(), ["familie"] = "FAM-99" });
        Assert.Equal(HttpStatusCode.BadRequest, c4);
        Assert.Contains("FAM-99", Z(j4!["fehler"]));

        var (c5, _) = await s.Post("/api/auftraege", new JsonObject { ["art"] = "familie_aus_unterlagen", ["unterlagen"] = u.DeepClone(), ["zeitlimitMinuten"] = 9999 });
        Assert.Equal(HttpStatusCode.BadRequest, c5);

        Assert.Equal(HttpStatusCode.NotFound, (await s.Get("/api/auftraege/unbekannt")).Code);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Post("/api/auftraege/unbekannt/abbrechen")).Code);

        // ein beendeter Auftrag lässt sich nicht mehr abbrechen
        var (c6, j6) = await s.Post($"/api/auftraege/{lauf.Id}/abbrechen");
        Assert.Equal(HttpStatusCode.Conflict, c6);
        Assert.Contains("schon beendet", Z(j6!["fehler"]));
    }

    [Fact]
    public async Task OhneOffenesProjekt_409()
    {
        using var s = new AuftragServer();
        var (code, j) = await s.Starten();
        Assert.Equal(HttpStatusCode.Conflict, code);
        Assert.StartsWith("Es ist kein Projekt offen", Z(j!["fehler"]));
        var (c2, l) = await s.Get("/api/auftraege");
        Assert.Equal(HttpStatusCode.OK, c2);
        Assert.Empty(l!["auftraege"]!.AsArray());
    }

    [Fact]
    public async Task ClaudeNichtGefunden_MeldetEsBeimStartOhneProtokollzuhinterlassen()
    {
        using var s = new AuftragServer(e => e with { ClaudeExe = Path.Combine(Path.GetTempPath(), "gibt_es_nicht", "claude.exe") });
        await s.ProjektAnlegen();
        var (code, j) = await s.Starten();
        Assert.Equal(HttpStatusCode.BadRequest, code);
        Assert.Contains("Claude Code wurde nicht gefunden", Z(j!["fehler"]));
        Assert.False(Directory.Exists(Path.Combine(s.Ordner, "auftraege")) && Directory.GetFiles(Path.Combine(s.Ordner, "auftraege")).Length > 0);
        // und der Platz bleibt frei: kein halber Auftrag in der Liste
        Assert.Empty((await s.Get("/api/auftraege")).Json!["auftraege"]!.AsArray());
    }
}
