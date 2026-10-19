// Attrappe statt claude.exe (Tests der KI-Aufträge, Modul 2 B5).
//
// Verhält sich wie «claude -p --output-format stream-json»: prüft die Schalter (nur solche, die es in «claude --help» gibt;
// ein unbekannter Schalter endet mit Code 2 wie bei Claude Code), liest den Auftrag von der Standardeingabe und schreibt
// stream-json auf die Standardausgabe. Die Betriebsart steht im Auftragstext als [attrappe:<modus>]:
//   ok (Vorgabe)   startet den MCP-Server «hlks» aus der MCP-Konfiguration, ruft familie_entwurf auf (Akteur und Art stehen
//                  in der Konfiguration), schreibt Bericht und Abschlusszeile
//   langsam        wie ok, aber mit einigen Schritten und Pausen vorher
//   gleich/aendern/ungueltig  familie_entwurf auf die vom Test angelegte Familie «Attrappe-Heizkörper» (gleich: zweimal dieselbe
//                  Definition; aendern: geänderte Beschreibung; ungueltig: neue Familie mit unbekannter Kategorie)
//   internet       wie ok, dazu eine Unterlage mit original = URL und abgerufen (wie nach unterlage_aus_url)
//   haengt         startet MCP-Server und einen Schläfer als Kindprozesse, meldet ihre Prozessnummern und wartet ewig
//   fehler         Abschlusszeile mit is_error, Code 1
//   anmeldung      Meldung «Please run /login» auf stderr, Code 1
//   ohne_ergebnis  nur eine Antwort, keine Abschlusszeile, Code 0
//   leer           keine Ausgabe, Code 0
// Die Zeile {"type":"system","subtype":"attrappe",…} meldet, was Claude Code bekommen hätte (Argumente, MCP-Konfiguration,
// Umgebung, Arbeitsordner, Auftragstext), damit die Tests es im Protokoll des Auftrags prüfen können.

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

if (args.Length == 1 && args[0] == "--schlafen") { Thread.Sleep(Timeout.Infinite); return 0; }

var utf8 = new UTF8Encoding(false);
var aus = new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true };
void Zeile(JsonNode n) { lock (aus) aus.WriteLine(n.ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping })); }

// ---------- Schalter wie in «claude --help» (Fassung 2.1.299), soweit die Aufträge sie brauchen
var ohneWert = new HashSet<string> { "-p", "--print", "--verbose", "--strict-mcp-config", "--no-session-persistence", "--disable-slash-commands" };
var mitWert = new HashSet<string> { "--output-format", "--mcp-config", "--tools", "--allowedTools", "--allowed-tools", "--permission-mode", "--model", "--effort", "--setting-sources" };
var optionen = new Dictionary<string, List<string>>();
List<string>? aktuell = null;
foreach (var a in args)
{
    if (a.StartsWith('-') && a.Length > 1)
    {
        if (ohneWert.Contains(a)) { optionen[a] = []; aktuell = null; }
        else if (mitWert.Contains(a)) optionen[a] = aktuell = [];
        else { Console.Error.WriteLine($"error: unknown option '{a}'"); return 2; }
    }
    else if (aktuell is not null) aktuell.Add(a);
    else { Console.Error.WriteLine($"error: unerwartetes Argument '{a}' (der Auftrag gehört auf die Standardeingabe)"); return 2; }
}
if (!optionen.ContainsKey("-p")) { Console.Error.WriteLine("error: -p fehlt"); return 2; }

var prompt = new StreamReader(Console.OpenStandardInput(), utf8).ReadToEnd();
var modus = Regex.Match(prompt, @"\[attrappe:(\w+)\]") is { Success: true } mm ? mm.Groups[1].Value : "ok";

JsonObject? konfig = null;
if (optionen.TryGetValue("--mcp-config", out var cfgListe) && cfgListe.Count > 0 && File.Exists(cfgListe[0]))
    konfig = JsonNode.Parse(File.ReadAllText(cfgListe[0], Encoding.UTF8)) as JsonObject;

Zeile(new JsonObject
{
    ["type"] = "system", ["subtype"] = "init", ["session_id"] = "attrappe",
    ["mcp_servers"] = new JsonArray(new JsonObject { ["name"] = "hlks", ["status"] = "connected" }),
});
Zeile(new JsonObject
{
    ["type"] = "system", ["subtype"] = "attrappe",
    ["pid"] = Environment.ProcessId,
    ["modus"] = modus,
    ["argumente"] = new JsonArray(args.Select(x => (JsonNode)JsonValue.Create(x)).ToArray()),
    ["optionen"] = new JsonObject(optionen.Select(o => KeyValuePair.Create(o.Key, (JsonNode?)new JsonArray(o.Value.Select(x => (JsonNode)JsonValue.Create(x)).ToArray())))),
    ["apiKeyGesetzt"] = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") is not null,
    ["authTokenGesetzt"] = Environment.GetEnvironmentVariable("ANTHROPIC_AUTH_TOKEN") is not null,
    ["claudecodeGesetzt"] = Environment.GetEnvironmentVariable("CLAUDECODE") is not null,
    ["maxMcpOutputTokens"] = Environment.GetEnvironmentVariable("MAX_MCP_OUTPUT_TOKENS"),
    ["arbeitsordner"] = Directory.GetCurrentDirectory(),
    ["arbeitsordnerEintraege"] = Directory.GetFileSystemEntries(Directory.GetCurrentDirectory()).Length,
    ["mcpKonfiguration"] = konfig?.DeepClone(),
    ["prompt"] = prompt,
});

switch (modus)
{
    case "anmeldung":
        Console.Error.WriteLine("Invalid API key · Please run /login");
        return 1;
    case "leer":
        return 0;
    case "fehler":
        Console.Error.WriteLine("Attrappe: simulierter Fehler");
        Zeile(new JsonObject { ["type"] = "result", ["subtype"] = "error_during_execution", ["is_error"] = true, ["result"] = "Attrappe: simulierter Fehler", ["num_turns"] = 1 });
        return 1;
    case "ohne_ergebnis":
        Antwort("Antwort der Attrappe ohne Abschlusszeile.");
        return 0;
    case "haengt":
        return await Haengt();
    case "gleich":
    case "aendern":
    case "ungueltig":
        return await Familien();
    case "langsam":
        for (int i = 1; i <= 4; i++) { Antwort($"Schritt {i} der Attrappe"); await Task.Delay(500); }
        return await Ok();
    default:
        return await Ok();
}

void Antwort(string text) => Zeile(new JsonObject
{
    ["type"] = "assistant",
    ["message"] = new JsonObject { ["role"] = "assistant", ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = text }) },
});

void Aufruf(string id, string name, JsonObject eingabe) => Zeile(new JsonObject
{
    ["type"] = "assistant",
    ["message"] = new JsonObject { ["role"] = "assistant", ["content"] = new JsonArray(new JsonObject
    { ["type"] = "tool_use", ["id"] = id, ["name"] = name, ["input"] = eingabe }) },
});

void Antwort2(string id, string inhalt, bool fehler = false) => Zeile(new JsonObject
{
    ["type"] = "user",
    ["message"] = new JsonObject { ["role"] = "user", ["content"] = new JsonArray(new JsonObject
    { ["type"] = "tool_result", ["tool_use_id"] = id, ["content"] = inhalt, ["is_error"] = fehler }) },
});

async Task<int> Ok()
{
    var werkzeug = "familie_entwurf";
    // Umgebung HLKS_ATTRAPPE_DEFINITION (C2, Browser-Test «Familie aus Unterlagen»): Datei mit einer fertigen Definition, die statt der
    // Testfamilie mit familie_entwurf angelegt wird (Name und Quellen bestimmt der Test, z. B. mit einem absichtlichen Widerspruch).
    // Die Datei wird erst beim Lauf gelesen: der Test darf sie nach dem Hochladen der Unterlagen schreiben (Befund-Ids stehen erst dann fest).
    var defDatei = Environment.GetEnvironmentVariable("HLKS_ATTRAPPE_DEFINITION");
    var eigene = !string.IsNullOrWhiteSpace(defDatei) && File.Exists(defDatei);
    JsonObject def;
    if (eigene) def = JsonNode.Parse(File.ReadAllText(defDatei!, Encoding.UTF8))!.AsObject();
    else
    {
        def = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "test_heizkoerper.familie.json"), Encoding.UTF8))!.AsObject();
        def["name"] = "Attrappe-Heizkörper";
        def["id"] = "fam-attrappe";
    }
    var eingabe = new JsonObject { ["definition"] = def };
    // Soll eine bestehende Familie ersetzt werden, steht sie im Hinweis als [attrappe:familie=FAM-1]
    if (Regex.Match(prompt, @"\[attrappe:familie=([^\]]+)\]") is { Success: true } fm) eingabe["familie"] = fm.Groups[1].Value;
    Antwort("Ich lese die Unterlagen und lege die Familie an.");
    Zeile(new JsonObject
    {
        ["type"] = "assistant",
        ["message"] = new JsonObject { ["role"] = "assistant", ["content"] = new JsonArray(new JsonObject
        { ["type"] = "tool_use", ["id"] = "t1", ["name"] = "mcp__hlks__" + werkzeug, ["input"] = eingabe.DeepClone() }) },
    });
    string ergebnis;
    try
    {
        using var mcp = McpSitzung.Starten(konfig ?? throw new InvalidOperationException("Keine MCP-Konfiguration"));
        var werkzeuge = await mcp.Werkzeuge();
        if (!werkzeuge.Contains(werkzeug)) throw new InvalidOperationException($"Der MCP-Server kennt {werkzeug} nicht: {string.Join(", ", werkzeuge)}");
        ergebnis = await mcp.Aufrufen(werkzeug, eingabe);
        if (modus == "internet") ergebnis += "\n" + await Abruf(mcp);
    }
    catch (Exception ex)
    {
        Zeile(new JsonObject { ["type"] = "result", ["subtype"] = "error_during_execution", ["is_error"] = true, ["result"] = "Attrappe: MCP-Aufruf gescheitert: " + ex.Message });
        return 1;
    }
    Zeile(new JsonObject
    {
        ["type"] = "user",
        ["message"] = new JsonObject { ["role"] = "user", ["content"] = new JsonArray(new JsonObject
        { ["type"] = "tool_result", ["tool_use_id"] = "t1", ["content"] = ergebnis }) },
    });
    var bericht = $"Bericht der Attrappe: Familie «{def["name"]?.GetValue<string>() ?? "?"}» als Entwurf angelegt.";
    Antwort(bericht);
    Zeile(new JsonObject { ["type"] = "result", ["subtype"] = "success", ["is_error"] = false, ["result"] = bericht, ["num_turns"] = 3, ["total_cost_usd"] = 0.0, ["duration_ms"] = 1000 });
    return 0;
}


/// <summary>Stellt einen Abruf aus dem Internet nach (kein echter Download): legt über den MCP-Server eine lokale Datei als
/// Herstellerunterlage ab (unterlage_einfuegen, zweck familie) und setzt original auf eine URL und abgerufen auf die Zeit –
/// so sieht das Paket aus, das unterlage_aus_url schreibt.</summary>
async Task<string> Abruf(McpSitzung mcp)
{
    Aufruf("s1", "WebSearch", new JsonObject { ["query"] = "Belimo R2015-S1 Datenblatt" });
    Antwort2("s1", "Web search results for query: \"Belimo R2015-S1 Datenblatt\"");
    Aufruf("u1", "mcp__hlks__unterlage_aus_url", new JsonObject { ["url"] = "https://www.hersteller.example/daten/datenblatt.csv" });
    var datei = Path.Combine(Path.GetTempPath(), $"attrappe_datenblatt_{Guid.NewGuid():N}.csv");
    File.WriteAllText(datei, "Typ;L;H\nA-1;80;60\nA-2;90;70\n", utf8);
    try
    {
        var a = await mcp.Aufrufen("unterlage_einfuegen", new JsonObject { ["datei"] = datei, ["zweck"] = "familie", ["art"] = "csv" });
        var kennzeichen = JsonNode.Parse(a)?["aenderungen"]?[0]?["kennzeichen"]?.GetValue<string>() ?? throw new InvalidOperationException("unterlage_einfuegen: " + a);
        var b = await mcp.Aufrufen("element_aendern", new JsonObject
        {
            ["element"] = kennzeichen,
            ["eigenschaften"] = new JsonObject { ["original"] = "https://www.hersteller.example/daten/datenblatt.csv", ["abgerufen"] = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'") },
        });
        if (JsonNode.Parse(b)?["fehler"] is not null) throw new InvalidOperationException("element_aendern: " + b);
        Antwort2("u1", b);
        Aufruf("u2", "mcp__hlks__unterlage_aus_url", new JsonObject { ["url"] = "https://www.hersteller.example/gesperrt.pdf" });
        Antwort2("u2", "{\"fehler\":\"nur mit Anmeldung erhältlich\"}");
        return b;
    }
    finally { try { File.Delete(datei); } catch { } }
}

/// <summary>Modi gleich, aendern, ungueltig: familie_entwurf auf eine bestehende Familie «Attrappe-Heizkörper» (vom Test vorher
/// angelegt, evtl. im Editor reserviert) bzw. mit ungültiger Definition. Ergebnisse stehen im Strom wie bei Claude Code.</summary>
async Task<int> Familien()
{
    var def = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "test_heizkoerper.familie.json"), Encoding.UTF8))!.AsObject();
    def["name"] = "Attrappe-Heizkörper";
    def["id"] = "fam-attrappe";
    string? familie = "Attrappe-Heizkörper";
    var aufrufe = 1;
    switch (modus)
    {
        case "gleich": aufrufe = 2; break;                                              // dieselbe Definition zweimal
        case "aendern": def["beschreibung"] = "Von der Attrappe geändert."; break;
        case "ungueltig": def["name"] = "Attrappe-Ungültig"; def["id"] = "fam-ungueltig"; def["kategorie"] = "GibtEsNicht"; familie = null; break;
    }
    Antwort("Ich ändere die Familie.");
    using var mcp = McpSitzung.Starten(konfig ?? throw new InvalidOperationException("Keine MCP-Konfiguration"));
    await mcp.Werkzeuge();
    for (int i = 1; i <= aufrufe; i++)
    {
        var eingabe = new JsonObject { ["definition"] = def.DeepClone() };
        if (familie is not null) eingabe["familie"] = familie;
        Aufruf("f" + i, "mcp__hlks__familie_entwurf", eingabe);
        Antwort2("f" + i, await mcp.Aufrufen("familie_entwurf", eingabe));
    }
    var bericht = "Bericht der Attrappe: Familie bearbeitet.";
    Antwort(bericht);
    Zeile(new JsonObject { ["type"] = "result", ["subtype"] = "success", ["is_error"] = false, ["result"] = bericht, ["num_turns"] = 3, ["total_cost_usd"] = 0.0, ["duration_ms"] = 1000 });
    return 0;
}
async Task<int> Haengt()
{
    var psi = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
    // Als «dotnet Attrappe.dll» gestartet ist ProcessPath dotnet.exe: die dll als erstes Argument mitgeben
    if (Path.GetFileNameWithoutExtension(Environment.ProcessPath!).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        psi.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Hlks.ClaudeAttrappe.dll"));
    psi.ArgumentList.Add("--schlafen");
    var schlaefer = Process.Start(psi)!;
    using var mcp = McpSitzung.Starten(konfig ?? throw new InvalidOperationException("Keine MCP-Konfiguration"));
    await mcp.Werkzeuge();   // der Server läuft und antwortet
    Zeile(new JsonObject { ["type"] = "system", ["subtype"] = "attrappe_prozesse", ["pid"] = Environment.ProcessId, ["schlaeferPid"] = schlaefer.Id, ["mcpPid"] = mcp.Pid });
    await Task.Delay(Timeout.Infinite);
    return 0;
}

/// <summary>Minimaler MCP-Client über die Standardein-/ausgabe des Servers (Zeilen mit JSON-RPC).</summary>
sealed class McpSitzung : IDisposable
{
    readonly Process p;
    int nr;
    public int Pid => p.Id;
    McpSitzung(Process p) { this.p = p; }

    public static McpSitzung Starten(JsonObject konfig)
    {
        var s = konfig["mcpServers"]?["hlks"] as JsonObject ?? throw new InvalidOperationException("mcpServers.hlks fehlt");
        var psi = new ProcessStartInfo(s["command"]!.GetValue<string>())
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = new UTF8Encoding(false),
        };
        foreach (var a in s["args"]!.AsArray()) psi.ArgumentList.Add(a!.GetValue<string>());
        var p = Process.Start(psi)!;
        p.ErrorDataReceived += (_, _) => { };
        p.BeginErrorReadLine();
        return new McpSitzung(p);
    }

    async Task<JsonObject> Anfrage(string methode, JsonObject? par)
    {
        var id = ++nr;
        await p.StandardInput.WriteLineAsync(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = methode, ["params"] = par }.ToJsonString());
        await p.StandardInput.FlushAsync();
        while (true)
        {
            var z = await p.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(90)) ?? throw new InvalidOperationException("MCP-Server beendet");
            if (z.Length == 0 || JsonNode.Parse(z) is not JsonObject o || o["id"] is null) continue;
            if (o["id"]!.GetValue<int>() != id) continue;
            if (o["error"] is { } e) throw new InvalidOperationException("MCP-Fehler: " + e.ToJsonString());
            return o["result"]!.AsObject();
        }
    }

    bool bereit;
    async Task Bereit()
    {
        if (bereit) return;
        await Anfrage("initialize", new JsonObject
        {
            ["protocolVersion"] = "2024-11-05", ["capabilities"] = new JsonObject(),
            ["clientInfo"] = new JsonObject { ["name"] = "attrappe", ["version"] = "1" },
        });
        await p.StandardInput.WriteLineAsync(new JsonObject { ["jsonrpc"] = "2.0", ["method"] = "notifications/initialized" }.ToJsonString());
        await p.StandardInput.FlushAsync();
        bereit = true;
    }

    public async Task<List<string>> Werkzeuge()
    {
        await Bereit();
        var r = await Anfrage("tools/list", new JsonObject());
        return r["tools"]!.AsArray().Select(t => t!["name"]!.GetValue<string>()).ToList();
    }

    public async Task<string> Aufrufen(string werkzeug, JsonObject argumente)
    {
        await Bereit();
        var r = await Anfrage("tools/call", new JsonObject { ["name"] = werkzeug, ["arguments"] = argumente.DeepClone() });
        return string.Concat(r["content"]!.AsArray().Select(c => c?["text"]?.GetValue<string>() ?? ""));
    }

    public void Dispose()
    {
        try { p.StandardInput.Close(); } catch { }
        try { if (!p.WaitForExit(5000)) p.Kill(true); } catch { }
        p.Dispose();
    }
}
