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
/// Hilfen der Tests der KI-Palette (Modul 3c, Teil K): ein Server mit offenem Projekt (Vorgabe: Beispielprojekt), bei dem Claude durch die
/// Attrappe (tests/Hlks.ClaudeAttrappe, Gesprächsmodus) ersetzt ist; der MCP-Server ist der echte Hlks.Mcp.dll aus dem Bau. Arbeitsordner von
/// Claude, Projekt und Normen liegen unter einem eigenen Ordner in %TEMP%. Kein echter Claude-Lauf, nie W:.
/// </summary>
public sealed class GespraechServer : IDisposable
{
    public const string Dotnet = @"C:\Program Files\dotnet\dotnet.exe";
    readonly WebApplicationFactory<Program> basis = new();
    readonly WebApplicationFactory<Program> fabrik;
    public HttpClient Http { get; }
    public string Wurzel { get; }
    public string Ordner => Path.Combine(Wurzel, "c_Pläne und Modelle", "e_KICAD");
    public string ArbeitsWurzel => Path.Combine(Wurzel, "claude_arbeit");
    public GespraechEinstellungen Einst { get; }
    public AuftragEinstellungen Auftrag { get; }
    public ProjektDienst Dienst => fabrik.Services.GetRequiredService<ProjektDienst>();
    public ConcurrentQueue<JsonObject> Ereignisse { get; } = new();
    readonly CancellationTokenSource ende = new();
    readonly bool wurzelBehalten;

    /// <param name="wurzel">Gemeinsamer Ordner (für den «Neustart»: zweiter Server auf demselben Projekt); null = neu.</param>
    public GespraechServer(Func<GespraechEinstellungen, GespraechEinstellungen>? anpassen = null, string? wurzel = null)
    {
        Wurzel = wurzel ?? Path.Combine(Path.GetTempPath(), "hlks_ki_" + Guid.NewGuid().ToString("N")[..10]);
        wurzelBehalten = wurzel is not null;
        var dotnet = File.Exists(Dotnet) ? Dotnet : Environment.ProcessPath!;
        var attrappe = Path.Combine(AppContext.BaseDirectory, "Hlks.ClaudeAttrappe.dll");
        var mcp = Path.Combine(AppContext.BaseDirectory, "Hlks.Mcp.dll");
        var g = new GespraechEinstellungen
        {
            ClaudeExe = dotnet, ClaudeVorArgumente = [attrappe], McpDll = mcp, DotnetPfad = dotnet, ArbeitsWurzel = ArbeitsWurzel,
            NormenAutomatischAufbauen = false,
        };
        Einst = anpassen?.Invoke(g) ?? g;
        Auftrag = new AuftragEinstellungen
        {
            ClaudeExe = dotnet, ClaudeVorArgumente = [attrappe], McpDll = mcp, DotnetPfad = dotnet, ArbeitsWurzel = Path.Combine(Wurzel, "auftrag_arbeit"),
        };
        var e = new Einstellungen("Test", "testpc", Katalog.StandardOrdner(), null) { ZuletztMerken = false };
        fabrik = basis.WithWebHostBuilder(b => b.ConfigureTestServices(s => { s.AddSingleton(e); s.AddSingleton(Auftrag); s.AddSingleton(Einst); }));
        Http = fabrik.CreateClient();
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

    public async Task<(HttpStatusCode Code, JsonNode? Json)> Senden(HttpMethod m, string pfad, object? koerper = null)
    {
        var anfrage = new HttpRequestMessage(m, pfad);
        if (m != HttpMethod.Get && m != HttpMethod.Delete)
        {
            var text = koerper switch { null => "{}", string s => s, JsonNode n => n.ToJsonString(), _ => Json.Text(koerper) };
            anfrage.Content = new StringContent(text, Encoding.UTF8, "application/json");
        }
        var a = await Http.SendAsync(anfrage);
        var antwort = await a.Content.ReadAsStringAsync();
        return (a.StatusCode, antwort.Length == 0 ? null : JsonNode.Parse(antwort));
    }

    public Task<(HttpStatusCode Code, JsonNode? Json)> Post(string pfad, object? koerper = null) => Senden(HttpMethod.Post, pfad, koerper);
    public Task<(HttpStatusCode Code, JsonNode? Json)> Get(string pfad) => Senden(HttpMethod.Get, pfad);
    public Task<(HttpStatusCode Code, JsonNode? Json)> Delete(string pfad) => Senden(HttpMethod.Delete, pfad);

    /// <summary>Projekt anlegen (Vorgabe: Beispielprojekt «MFH Musterweg») bzw. das vorhandene öffnen.</summary>
    public async Task ProjektOeffnen(bool beispiel = true)
    {
        var (code, j) = File.Exists(Path.Combine(Ordner, "projekt.json"))
            ? await Post("/api/projekt/oeffnen", new { ordner = Ordner })
            : await Post("/api/projekt/anlegen", new { ordner = Ordner, name = "KI-Palette", beispiel });
        Assert.True(code == HttpStatusCode.OK, j?.ToJsonString());
    }

    public async Task<string> GespraechAnlegen(string? titel = null)
    {
        var (code, j) = await Post("/api/ki/gespraeche", titel is null ? new JsonObject() : new JsonObject { ["titel"] = titel });
        Assert.Equal(HttpStatusCode.Created, code);
        return j!["id"]!.GetValue<string>();
    }

    public Task<(HttpStatusCode Code, JsonNode? Json)> Fragen(string id, string text, JsonObject? kontext = null, string? modell = null, bool? internet = null)
    {
        var o = new JsonObject { ["text"] = text, ["kontext"] = kontext ?? new JsonObject() };
        if (modell is not null) o["modell"] = modell;
        if (internet is not null) o["internet"] = internet;
        return Post($"/api/ki/gespraeche/{id}/frage", o);
    }

    public async Task<JsonNode> Gespraech(string id)
    {
        var (code, j) = await Get("/api/ki/gespraeche/" + id);
        Assert.True(code == HttpStatusCode.OK, j?.ToJsonString());
        return j!;
    }

    /// <summary>Frage stellen (202) und warten, bis sie beendet ist; gibt die letzte Nachricht (Antwort von Claude) zurück.</summary>
    public async Task<JsonObject> FragenUndWarten(string id, string text, JsonObject? kontext = null, string? modell = null, bool? internet = null, TimeSpan? frist = null)
    {
        var (code, j) = await Fragen(id, text, kontext, modell, internet);
        Assert.True(code == HttpStatusCode.Accepted, $"{code}: {j?.ToJsonString()}");
        return await Warten(id, frist);
    }

    public async Task<JsonObject> Warten(string id, TimeSpan? frist = null)
    {
        var uhr = Stopwatch.StartNew();
        JsonNode? g = null;
        while (uhr.Elapsed < (frist ?? TimeSpan.FromMinutes(2)))
        {
            g = await Gespraech(id);
            if (!g["laeuft"]!.GetValue<bool>()) return (JsonObject)g["nachrichten"]!.AsArray()[^1]!;
            await Task.Delay(150);
        }
        throw new Xunit.Sdk.XunitException("Zeit abgelaufen beim Warten auf die Antwort. Letzter Stand: " + g?.ToJsonString());
    }

    public static string Z(JsonNode? n) => n?.GetValue<string>() ?? "";

    public string GespraechDatei(string id) => Path.Combine(Ordner, "ki", "gespraeche", id + ".json");
    public string LogPfad(string id) => Path.Combine(Ordner, "ki", "gespraeche", id + ".log");

    /// <summary>Zeilen des Protokolls eines Gesprächs (gemeinsames Lesen, die Datei kann gerade beschrieben werden).</summary>
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

    /// <summary>Was die Attrappe bekommen hat (Zeile subtype attrappe), je Aufruf in der Reihenfolge des Protokolls.</summary>
    public List<JsonObject> AttrappeInfos(string id) => Protokoll(id).Where(x => Z(x["subtype"]) == "attrappe").ToList();

    public static List<string> Argumente(JsonObject info) => info["argumente"]!.AsArray().Select(x => x!.GetValue<string>()).ToList();

    /// <summary>Wert eines Schalters aus den Argumenten (null = fehlt).</summary>
    public static string? Schalter(JsonObject info, string name)
    {
        var a = Argumente(info);
        var i = a.IndexOf(name);
        return i < 0 ? null : i + 1 < a.Count ? a[i + 1] : "";
    }

    public async Task<JsonObject> AufProzesseWarten(string id, TimeSpan frist)
    {
        var uhr = Stopwatch.StartNew();
        while (uhr.Elapsed < frist)
        {
            var z = Protokoll(id).FirstOrDefault(x => Z(x["subtype"]) == "attrappe_prozesse");
            if (z is not null) return z;
            await Task.Delay(150);
        }
        throw new Xunit.Sdk.XunitException("Die Attrappe hat ihre Prozesse nicht gemeldet.");
    }

    /// <summary>KI-Ereignisse einer Frage in der Reihenfolge des Eingangs.</summary>
    public List<JsonObject> KiEreignisse(string frage) => Ereignisse.Where(e => Z(e["art"]) == "ki" && Z(e["frage"]) == frage).ToList();

    /// <summary>Wert eines Elements im Modell des Servers (nach dem Abgleich).</summary>
    public async Task<string?> ElementName(string kz) => await Dienst.Mit(p => p.Modell.Finde(kz)?.Name);

    /// <summary>Warten, bis der Server ein Paket kennt (Abgleich mit der Ablage).</summary>
    public async Task AufPaketWarten(string paket, TimeSpan? frist = null)
    {
        var uhr = Stopwatch.StartNew();
        while (uhr.Elapsed < (frist ?? TimeSpan.FromSeconds(30)))
        {
            if (await Dienst.Mit(p => p.BekanntePakete.Contains(paket))) return;
            await Dienst.Abgleichen();
            await Task.Delay(100);
        }
        throw new Xunit.Sdk.XunitException($"Paket {paket} kam nicht an.");
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

    /// <summary>Server beenden, ohne den Ordner zu löschen (Neustart-Test).</summary>
    public void Beenden()
    {
        try { Post("/api/projekt/schliessen").GetAwaiter().GetResult(); } catch { /* egal */ }
        ende.Cancel();
        Http.Dispose();
        fabrik.Dispose();
        basis.Dispose();
    }

    public void Dispose()
    {
        Beenden();
        if (wurzelBehalten) return;
        Loeschen(Wurzel);
    }

    public static void Loeschen(string ordner)
    {
        for (int i = 0; i < 10; i++)
        {
            try { if (Directory.Exists(ordner)) Directory.Delete(ordner, true); return; }
            catch (IOException) { Thread.Sleep(300); }
            catch (UnauthorizedAccessException) { Thread.Sleep(300); }
        }
    }

    /// <summary>1×1-PNG als data-URL.</summary>
    public const string PngUrl = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

    /// <summary>Kopie der Test-PDF der Normensuche (Ausgabeordner normen\, aus tests/daten/normen) nach %TEMP%.</summary>
    public static string NormenKopie()
    {
        var quelle = Path.Combine(AppContext.BaseDirectory, "normen");
        var ziel = Path.Combine(Path.GetTempPath(), "hlks_normen_" + Guid.NewGuid().ToString("N")[..10]);
        foreach (var f in Directory.GetFiles(quelle, "*.pdf", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(quelle, f);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(ziel, rel))!);
            File.Copy(f, Path.Combine(ziel, rel));
        }
        return ziel;
    }
}
