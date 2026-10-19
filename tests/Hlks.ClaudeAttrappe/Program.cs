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
//
// GESPRÄCHSMODUS (KI-Palette, Modul 3c): mit --session-id oder --resume. Sitzungen liegen wie bei Claude Code je Arbeitsordner
// (<Arbeitsordner>\.attrappe\sitzungen\<id>.json mit den bisherigen Fragen): --resume einer unbekannten Sitzung endet ohne Ausgabe mit
// «No conversation found with session ID: …» (Code 1), --session-id einer vorhandenen mit «already in use». Mit --include-partial-messages
// kommt der Text als stream_event-Teilstücke (text_delta), sonst als ganze assistant-Nachricht. Werkzeuge prüft die Attrappe wie Claude Code
// mit dontAsk gegen --allowedTools/--disallowedTools/--tools: ein nicht erlaubtes wird abgelehnt («Werkzeug verweigert: …», kein MCP-Aufruf).
// Der Modus steht in der FRAGE (Teil nach
// «# Frage») als [attrappe:<modus>]:
//   antwort (Vorgabe)  Text in drei Teilen: «Antwort der Attrappe (Frage n der Sitzung): <Frage>»
//   aendern=KZ         element_aendern über den MCP-Server: Name von KZ auf «Von der Attrappe geändert» (ein Paket), dann Text
//   zweimal=KZ         zwei element_aendern an KZ (zwei Pakete)
//   ueberblick         projekt_ueberblick über den MCP-Server, Antwort nennt die Zahl der Geschosse
//   bild               kontext_bild über den MCP-Server; der Text nennt, was zurückkam (Bildart und Grösse bzw. den Hinweis)
//   normen             normen_suchen {frage: «Mindestgefälle Prüfleitung»}; der Text nennt Norm, Seite und Ziffer des ersten Treffers
//   werkzeugfehler     element_lesen auf ein unbekanntes Element (Werkzeug meldet Fehler), dann Text
//   internet           WebSearch und ein unterlage_aus_url mit Fehler (ohne MCP), dann Text
//   langsam            fünf Textteile mit Pausen von 400 ms
//   haengt, fehler, anmeldung  wie bei den Aufträgen

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
var ohneWert = new HashSet<string> { "-p", "--print", "--verbose", "--strict-mcp-config", "--no-session-persistence", "--disable-slash-commands", "--include-partial-messages" };
var mitWert = new HashSet<string> { "--output-format", "--mcp-config", "--tools", "--allowedTools", "--allowed-tools", "--permission-mode", "--model", "--effort", "--setting-sources",
    "--disallowedTools", "--disallowed-tools", "--resume", "--session-id" };
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

if (optionen.ContainsKey("--session-id") || optionen.ContainsKey("--resume")) return await Gespraech();

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

/// <summary>Gesprächsmodus (KI-Palette, Modul 3c): Sitzungen je Arbeitsordner, Text in Teilstücken, Werkzeuge über den echten MCP-Server.</summary>
async Task<int> Gespraech()
{
    var sitzungen = Path.Combine(Directory.GetCurrentDirectory(), ".attrappe", "sitzungen");
    string sitzung;
    List<string> bisher;
    if (optionen.TryGetValue("--resume", out var res) && res.Count > 0)
    {
        sitzung = res[0];
        var d = Path.Combine(sitzungen, sitzung + ".json");
        if (!File.Exists(d))
        {
            // wie Claude Code 2.1.299 (geprüft 20.10.2026): Abschlusszeile mit errors, keine init-Zeile, Meldung auf stderr, Code 1, kein Modellaufruf
            Zeile(new JsonObject { ["type"] = "result", ["subtype"] = "error_during_execution", ["is_error"] = true, ["num_turns"] = 0, ["session_id"] = sitzung,
                ["total_cost_usd"] = 0, ["errors"] = new JsonArray($"No conversation found with session ID: {sitzung}") });
            Console.Error.WriteLine($"No conversation found with session ID: {sitzung}");
            return 1;
        }
        bisher = JsonNode.Parse(File.ReadAllText(d, Encoding.UTF8))!.AsArray().Select(x => x!.GetValue<string>()).ToList();
    }
    else
    {
        sitzung = optionen["--session-id"].FirstOrDefault() ?? "";
        if (!Guid.TryParse(sitzung, out _)) { Console.Error.WriteLine("Error: Invalid session ID. Must be a valid UUID."); return 1; }
        if (File.Exists(Path.Combine(sitzungen, sitzung + ".json"))) { Console.Error.WriteLine($"Error: Session ID {sitzung} is already in use."); return 1; }
        bisher = [];
    }
    var stelle = prompt.LastIndexOf("# Frage", StringComparison.Ordinal);
    var frage = (stelle >= 0 ? prompt[(stelle + "# Frage".Length)..] : prompt).Trim();
    var m = Regex.Match(frage, @"\[attrappe:(\w+)(?:=([^\]]+))?\]");
    var gmodus = m.Success ? m.Groups[1].Value : "antwort";
    var wert = m.Success && m.Groups[2].Success ? m.Groups[2].Value.Trim() : null;
    if (gmodus == "anmeldung") { Console.Error.WriteLine("Invalid API key · Please run /login"); return 1; }

    Directory.CreateDirectory(sitzungen);
    bisher.Add(frage);
    File.WriteAllText(Path.Combine(sitzungen, sitzung + ".json"), new JsonArray(bisher.Select(x => (JsonNode)JsonValue.Create(x)).ToArray()).ToJsonString(), utf8);

    var teile = optionen.ContainsKey("--include-partial-messages");
    Zeile(new JsonObject
    {
        ["type"] = "system", ["subtype"] = "init", ["session_id"] = sitzung,
        ["mcp_servers"] = new JsonArray(new JsonObject { ["name"] = "hlks", ["status"] = "connected" }),
    });
    Zeile(new JsonObject
    {
        ["type"] = "system", ["subtype"] = "attrappe", ["pid"] = Environment.ProcessId, ["modus"] = gmodus, ["wert"] = wert,
        ["sitzung"] = sitzung, ["fortgesetzt"] = optionen.ContainsKey("--resume"), ["fruehereFragen"] = bisher.Count - 1,
        ["argumente"] = new JsonArray(args.Select(x => (JsonNode)JsonValue.Create(x)).ToArray()),
        ["optionen"] = new JsonObject(optionen.Select(o => KeyValuePair.Create(o.Key, (JsonNode?)new JsonArray(o.Value.Select(x => (JsonNode)JsonValue.Create(x)).ToArray())))),
        ["apiKeyGesetzt"] = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") is not null,
        ["authTokenGesetzt"] = Environment.GetEnvironmentVariable("ANTHROPIC_AUTH_TOKEN") is not null,
        ["claudecodeGesetzt"] = Environment.GetEnvironmentVariable("CLAUDECODE") is not null,
        ["arbeitsordner"] = Directory.GetCurrentDirectory(),
        ["mcpKonfiguration"] = konfig?.DeepClone(),
        ["prompt"] = prompt, ["frage"] = frage,
    });

    var letzter = "";
    async Task Sprechen(string text, int stuecke = 3, int pause = 0)
    {
        letzter = text;
        if (teile)
        {
            Zeile(new JsonObject { ["type"] = "stream_event", ["session_id"] = sitzung, ["event"] = new JsonObject { ["type"] = "message_start", ["message"] = new JsonObject { ["role"] = "assistant" } } });
            Zeile(new JsonObject { ["type"] = "stream_event", ["session_id"] = sitzung, ["event"] = new JsonObject { ["type"] = "content_block_start", ["index"] = 0, ["content_block"] = new JsonObject { ["type"] = "text", ["text"] = "" } } });
            var laenge = (int)Math.Ceiling(text.Length / (double)Math.Max(1, stuecke));
            for (int i = 0; i < text.Length; i += laenge)
            {
                Zeile(new JsonObject { ["type"] = "stream_event", ["session_id"] = sitzung, ["event"] = new JsonObject
                    { ["type"] = "content_block_delta", ["index"] = 0, ["delta"] = new JsonObject { ["type"] = "text_delta", ["text"] = text.Substring(i, Math.Min(laenge, text.Length - i)) } } });
                if (pause > 0) await Task.Delay(pause);
            }
            Zeile(new JsonObject { ["type"] = "stream_event", ["session_id"] = sitzung, ["event"] = new JsonObject { ["type"] = "content_block_stop", ["index"] = 0 } });
        }
        Antwort(text);
        if (teile) Zeile(new JsonObject { ["type"] = "stream_event", ["session_id"] = sitzung, ["event"] = new JsonObject { ["type"] = "message_stop" } });
    }
    int Ende(int runden = 1)
    {
        Zeile(new JsonObject { ["type"] = "result", ["subtype"] = "success", ["is_error"] = false, ["result"] = letzter, ["session_id"] = sitzung,
            ["num_turns"] = runden, ["total_cost_usd"] = 0.0, ["duration_ms"] = 500 });
        return 0;
    }
    McpSitzung Mcp() => McpSitzung.Starten(konfig ?? throw new InvalidOperationException("Keine MCP-Konfiguration"));
    static bool IstFehler(string s) { try { return JsonNode.Parse(s) is JsonObject o && (o["fehler"] is not null || o["angenommen"]?.GetValue<bool>() == false); } catch (JsonException) { return false; } }

    // Berechtigungen wie Claude Code mit --permission-mode dontAsk: verboten (--disallowedTools) schlägt alles; MCP-Werkzeuge nur, wenn sie
    // (oder ihr Server «mcp__hlks») in --allowedTools stehen; eingebaute nur, wenn sie in --tools und --allowedTools stehen. Sonst verweigert.
    static IEnumerable<string> Werte(List<string>? l) => (l ?? []).SelectMany(x => x.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries));
    var erlaubtListe = Werte(optionen.GetValueOrDefault("--allowedTools") ?? optionen.GetValueOrDefault("--allowed-tools")).ToHashSet();
    var verbotenListe = Werte(optionen.GetValueOrDefault("--disallowedTools") ?? optionen.GetValueOrDefault("--disallowed-tools")).ToHashSet();
    var eingebautListe = Werte(optionen.GetValueOrDefault("--tools")).ToHashSet();
    bool Erlaubt(string w) =>
        !verbotenListe.Contains(w) && (w.StartsWith("mcp__", StringComparison.Ordinal)
            ? erlaubtListe.Contains(w) || erlaubtListe.Any(e => w.StartsWith(e + "__", StringComparison.Ordinal))
            : eingebautListe.Contains(w) && erlaubtListe.Contains(w));
    // Werkzeug nicht erlaubt: Aufruf und Ablehnung wie bei Claude Code, kurze Antwort, Ende (Rückgabe = Exit-Code); erlaubt: null
    async Task<int?> Verweigert(string id, string werkzeug, JsonObject eingabe)
    {
        if (Erlaubt(werkzeug)) return null;
        Aufruf(id, werkzeug, eingabe);
        Antwort2(id, $"Permission to use {werkzeug} has been denied.", true);
        await Sprechen($"Werkzeug verweigert: {werkzeug}");
        return Ende(2);
    }

    switch (gmodus)
    {
        case "fehler":
            Console.Error.WriteLine("Attrappe: simulierter Fehler");
            Zeile(new JsonObject { ["type"] = "result", ["subtype"] = "error_during_execution", ["is_error"] = true, ["result"] = "Attrappe: simulierter Fehler", ["session_id"] = sitzung });
            return 1;
        case "haengt":
            return await Haengt();
        case "langsam":
            await Sprechen("Eins. Zwei. Drei. Vier. Fünf.", 5, 400);
            return Ende();
        case "aendern":
        case "zweimal":
        {
            var kz = wert ?? throw new InvalidOperationException("aendern braucht ein Element: [attrappe:aendern=KZ]");
            await Sprechen("Ich ändere " + kz + ".");
            if (await Verweigert("a1", "mcp__hlks__element_aendern", new JsonObject { ["element"] = kz }) is int nein) return nein;
            using var mcp = Mcp();
            await mcp.Werkzeuge();
            for (int i = 1; i <= (gmodus == "zweimal" ? 2 : 1); i++)
            {
                var eingabe = new JsonObject { ["element"] = kz, ["name"] = gmodus == "zweimal" ? $"Von der Attrappe geändert {i}" : "Von der Attrappe geändert" };
                Aufruf("a" + i, "mcp__hlks__element_aendern", eingabe);
                var erg = await mcp.Aufrufen("element_aendern", eingabe);
                Antwort2("a" + i, erg, IstFehler(erg));
            }
            await Sprechen($"Ich habe {kz} umbenannt.");
            return Ende(3);
        }
        case "ueberblick":
        {
            if (await Verweigert("u1", "mcp__hlks__projekt_ueberblick", new JsonObject()) is int nein) return nein;
            using var mcp = Mcp();
            await mcp.Werkzeuge();
            Aufruf("u1", "mcp__hlks__projekt_ueberblick", new JsonObject());
            var erg = await mcp.Aufrufen("projekt_ueberblick", new JsonObject());
            Antwort2("u1", Kuerzen(erg));
            var n = JsonNode.Parse(erg)?["gebaeude"]?.AsArray().Sum(g => g?["geschosse"]?.AsArray().Count ?? 0) ?? 0;
            await Sprechen($"Das Projekt hat {n} Geschosse.");
            return Ende(2);
        }
        case "bild":
        {
            if (await Verweigert("b1", "mcp__hlks__kontext_bild", new JsonObject()) is int nein) return nein;
            using var mcp = Mcp();
            var liste = await mcp.Werkzeuge();
            if (!liste.Contains("kontext_bild")) throw new InvalidOperationException("Der MCP-Server kennt kontext_bild nicht: " + string.Join(", ", liste));
            Aufruf("b1", "mcp__hlks__kontext_bild", new JsonObject());
            var roh = await mcp.AufrufenRoh("kontext_bild", new JsonObject());
            var bild = roh["content"]!.AsArray().OfType<JsonObject>().FirstOrDefault(c => c["type"]?.GetValue<string>() == "image");
            var text = string.Concat(roh["content"]!.AsArray().OfType<JsonObject>().Select(c => c["text"]?.GetValue<string>() ?? ""));
            Antwort2("b1", text);
            await Sprechen(bild is null ? "Kein Bild erhalten: " + text
                : $"Bild erhalten: {bild["mimeType"]?.GetValue<string>()}, {Convert.FromBase64String(bild["data"]!.GetValue<string>()).Length} Bytes.");
            return Ende(2);
        }
        case "normen":
        {
            var eingabe = new JsonObject { ["frage"] = wert ?? "Mindestgefälle Prüfleitung" };
            if (await Verweigert("n1", "mcp__hlks__normen_suchen", eingabe) is int nein) return nein;
            using var mcp = Mcp();
            await mcp.Werkzeuge();
            Aufruf("n1", "mcp__hlks__normen_suchen", eingabe);
            var erg = await mcp.Aufrufen("normen_suchen", eingabe);
            Antwort2("n1", erg, IstFehler(erg));
            var t = JsonNode.Parse(erg)?["treffer"]?.AsArray().FirstOrDefault();
            await Sprechen(t is null ? "Keine Treffer: " + JsonNode.Parse(erg)?["hinweis"]
                : $"Treffer: {t["norm"]}, Seite {t["seite"]}, Ziffer {t["ziffer"]}: {t["auszug"]}");
            return Ende(2);
        }
        case "werkzeugfehler":
        {
            var eingabe = new JsonObject { ["element"] = "GIBT-ES-NICHT" };
            if (await Verweigert("w1", "mcp__hlks__element_lesen", eingabe) is int nein) return nein;
            using var mcp = Mcp();
            await mcp.Werkzeuge();
            Aufruf("w1", "mcp__hlks__element_lesen", eingabe);
            Antwort2("w1", await mcp.Aufrufen("element_lesen", eingabe));
            await Sprechen("Das Element gibt es nicht.");
            return Ende(2);
        }
        case "internet":
            if (await Verweigert("s1", "WebSearch", new JsonObject { ["query"] = "Prüfleitung Datenblatt" }) is int neinSuche) return neinSuche;
            if (await Verweigert("u1", "mcp__hlks__unterlage_aus_url", new JsonObject { ["url"] = "https://www.hersteller.example/gesperrt.pdf" }) is int neinAbruf) return neinAbruf;
            Aufruf("s1", "WebSearch", new JsonObject { ["query"] = "Prüfleitung Datenblatt" });
            Antwort2("s1", "Web search results for query: \"Prüfleitung Datenblatt\"");
            Aufruf("u1", "mcp__hlks__unterlage_aus_url", new JsonObject { ["url"] = "https://www.hersteller.example/gesperrt.pdf" });
            Antwort2("u1", "{\"fehler\":\"nur mit Anmeldung erhältlich\"}");
            await Sprechen("Ich habe im Internet gesucht.");
            return Ende(3);
        default:
            await Sprechen($"Antwort der Attrappe (Frage {bisher.Count} der Sitzung): {frage}");
            return Ende();
    }
}

static string Kuerzen(string s) => s.Length <= 2000 ? s : s[..2000];

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

    /// <summary>Ergebnis von tools/call unverändert (mit Bildinhalten).</summary>
    public async Task<JsonObject> AufrufenRoh(string werkzeug, JsonObject argumente)
    {
        await Bereit();
        return await Anfrage("tools/call", new JsonObject { ["name"] = werkzeug, ["arguments"] = argumente.DeepClone() });
    }

    public void Dispose()
    {
        try { p.StandardInput.Close(); } catch { }
        try { if (!p.WaitForExit(5000)) p.Kill(true); } catch { }
        p.Dispose();
    }
}
