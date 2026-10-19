using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Hlks.Kern;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Hlks.Web.Tests;

/// <summary>
/// Hilfen der Tests der KI-Aufträge (Modul 2, B5): ein Server mit offenem Projekt, bei dem Claude durch die Attrappe
/// (tests/Hlks.ClaudeAttrappe) ersetzt ist. Der MCP-Server ist der echte Hlks.Mcp.dll aus dem Bau.
/// </summary>
public sealed class AuftragServer : IDisposable
{
    public const string Dotnet = @"C:\Program Files\dotnet\dotnet.exe";
    readonly WebApplicationFactory<Program> basis = new();
    readonly WebApplicationFactory<Program> fabrik;
    public HttpClient Http { get; }
    public string Wurzel { get; } = Path.Combine(Path.GetTempPath(), "hlks_auf_" + Guid.NewGuid().ToString("N")[..10]);
    public string Ordner => Path.Combine(Wurzel, "c_Pläne und Modelle", "e_KICAD");
    public string ArbeitsWurzel => Path.Combine(Wurzel, "claude_arbeit");
    public AuftragEinstellungen Auftrag { get; }
    public ProjektDienst Dienst => fabrik.Services.GetRequiredService<ProjektDienst>();
    public ConcurrentQueue<JsonObject> Ereignisse { get; } = new();
    readonly CancellationTokenSource ende = new();

    public AuftragServer(Func<AuftragEinstellungen, AuftragEinstellungen>? anpassen = null)
    {
        var dotnet = File.Exists(Dotnet) ? Dotnet : Environment.ProcessPath!;
        var einst = new AuftragEinstellungen
        {
            ClaudeExe = dotnet,
            ClaudeVorArgumente = [Path.Combine(AppContext.BaseDirectory, "Hlks.ClaudeAttrappe.dll")],
            McpDll = Path.Combine(AppContext.BaseDirectory, "Hlks.Mcp.dll"),
            DotnetPfad = dotnet,
            ArbeitsWurzel = ArbeitsWurzel,
        };
        Auftrag = anpassen?.Invoke(einst) ?? einst;
        var e = new Einstellungen("Test", "testpc", Katalog.StandardOrdner(), null) { ZuletztMerken = false };
        fabrik = basis.WithWebHostBuilder(b => b.ConfigureTestServices(s => { s.AddSingleton(e); s.AddSingleton(Auftrag); }));
        Http = fabrik.CreateClient();
        // Ereignisstrom mitlesen (wie ein Browserfenster)
        var leser = Dienst.Abonnieren(ende.Token);
        _ = Task.Run(async () =>
        {
            try
            {
                while (await leser.WaitToReadAsync(ende.Token))
                    while (leser.TryRead(out var t)) if (JsonNode.Parse(t) is JsonObject o) Ereignisse.Enqueue(o);
            }
            catch (OperationCanceledException) { }
        });
    }

    public async Task<(HttpStatusCode Code, JsonNode? Json)> Post(string pfad, object? koerper = null)
    {
        var text = koerper switch { null => "{}", string s => s, JsonNode n => n.ToJsonString(), _ => Json.Text(koerper) };
        var a = await Http.PostAsync(pfad, new StringContent(text, Encoding.UTF8, "application/json"));
        var antwort = await a.Content.ReadAsStringAsync();
        return (a.StatusCode, antwort.Length == 0 ? null : JsonNode.Parse(antwort));
    }

    public async Task<(HttpStatusCode Code, JsonNode? Json)> Get(string pfad)
    {
        var a = await Http.GetAsync(pfad);
        var antwort = await a.Content.ReadAsStringAsync();
        return (a.StatusCode, antwort.Length == 0 ? null : JsonNode.Parse(antwort));
    }

    public async Task ProjektAnlegen()
    {
        var (code, _) = await Post("/api/projekt/anlegen", new { ordner = Ordner, name = "Auftragstest", beispiel = false });
        Assert.Equal(HttpStatusCode.OK, code);
    }

    /// <summary>Kleine Testdatei als Unterlage (ein Dateipfad: der Kern legt sie im Projekt ab).</summary>
    public static string Unterlage() => Testdatei("minimal_ifc4.ifc");

    public static string Testdatei(string name) => Quelldatei("tests", "daten", name);

    /// <summary>Datei im Quellordner HLKS_CAD (aufwärts vom Testordner, sonst der bekannte OneDrive-Pfad).</summary>
    public static string Quelldatei(params string[] teile)
    {
        var rel = Path.Combine(teile);
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            foreach (var p in new[] { Path.Combine(d.FullName, rel), Path.Combine(d.FullName, "HLKS_CAD", rel) })
                if (File.Exists(p)) return p;
        var bekannt = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive", "Claude_Code", "HLKS_CAD", rel);
        return File.Exists(bekannt) ? bekannt : throw new FileNotFoundException("Datei fehlt: " + rel);
    }

    /// <summary>Auftrag starten; modus steht als [attrappe:modus] im Hinweis.</summary>
    public Task<(HttpStatusCode Code, JsonNode? Json)> Starten(string modus = "ok", object? mehr = null)
    {
        var o = new JsonObject
        {
            ["art"] = "familie_aus_unterlagen",
            ["unterlagen"] = new JsonArray(Unterlage()),
            ["kategorie"] = "Heizkoerper",
            ["hinweis"] = $"Bitte genau lesen. [attrappe:{modus}]",
        };
        if (mehr is not null) foreach (var (k, v) in JsonNode.Parse(Json.Text(mehr))!.AsObject().ToList()) o[k] = v?.DeepClone();
        return Post("/api/auftraege", o);
    }

    public static string Id(JsonNode? j) => j!["id"]!.GetValue<string>();
    public static string St(JsonNode? j) => j!["status"]!.GetValue<string>();

    public async Task<JsonNode> Status(string id)
    {
        var (code, j) = await Get("/api/auftraege/" + id);
        Assert.Equal(HttpStatusCode.OK, code);
        return j!;
    }

    public async Task<JsonNode> Warten(string id, Func<JsonNode, bool> bedingung, TimeSpan frist, string was)
    {
        var uhr = Stopwatch.StartNew();
        JsonNode? s = null;
        while (uhr.Elapsed < frist)
        {
            s = await Status(id);
            if (bedingung(s)) return s;
            await Task.Delay(150);
        }
        throw new Xunit.Sdk.XunitException($"Zeit abgelaufen beim Warten auf: {was}. Letzter Status: {s?.ToJsonString()}");
    }

    public static bool Beendet(JsonNode s) => St(s) is "fertig" or "fehler" or "abgebrochen";

    public string LogPfad(string id) => Path.Combine(Ordner, "auftraege", id + ".log");

    /// <summary>Zeilen des Protokolls (Datei wird gerade beschrieben: gemeinsames Lesen).</summary>
    public List<JsonObject> Protokoll(string id)
    {
        var pfad = LogPfad(id);
        if (!File.Exists(pfad)) return [];
        using var fs = new FileStream(pfad, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var r = new StreamReader(fs, Encoding.UTF8);
        var l = new List<JsonObject>();
        while (r.ReadLine() is { } z)
            if (z.Length > 0 && JsonNode.Parse(z) is JsonObject o) l.Add(o);
        return l;
    }

    public async Task<JsonObject> AufProzesseWarten(string id, TimeSpan frist)
    {
        var uhr = Stopwatch.StartNew();
        while (uhr.Elapsed < frist)
        {
            var z = Protokoll(id).FirstOrDefault(x => x["subtype"]?.GetValue<string>() == "attrappe_prozesse");
            if (z is not null) return z;
            await Task.Delay(150);
        }
        throw new Xunit.Sdk.XunitException("Die Attrappe hat ihre Prozesse nicht gemeldet.");
    }

    public static bool IstTot(int pid)
    {
        try { using var p = Process.GetProcessById(pid); return p.HasExited; }
        catch (ArgumentException) { return true; }
    }

    public static async Task<bool> TotWarten(int pid, TimeSpan frist)
    {
        var uhr = Stopwatch.StartNew();
        while (uhr.Elapsed < frist) { if (IstTot(pid)) return true; await Task.Delay(100); }
        return IstTot(pid);
    }

    public void Dispose()
    {
        try { Post("/api/projekt/schliessen").GetAwaiter().GetResult(); } catch { /* egal */ }
        ende.Cancel();
        Http.Dispose();
        fabrik.Dispose();
        basis.Dispose();
        for (int i = 0; i < 10; i++)
        {
            try { if (Directory.Exists(Wurzel)) Directory.Delete(Wurzel, true); return; }
            catch (IOException) { Thread.Sleep(300); }
            catch (UnauthorizedAccessException) { Thread.Sleep(300); }
        }
    }
}
