using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using Hlks.Kern;
using Hlks.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Hlks.Labor.Tests;

/// <summary>
/// Labor-Server für die Tests (XR-Labor L0): die Labor-Seite als TestServer (localhost), der Netzzugang der Labor-Brille als echter Kestrel-Server – aber nur auf
/// 127.0.0.1 mit freien Ports. Benutzereinstellungen, Schalter, Kopplungen und Zertifikat liegen unter %TEMP%; der Ordner des Zertifikats lässt sich mit dem eines
/// KICAD-Servers teilen (wie %LOCALAPPDATA%\HLKS_CAD\xr im Betrieb), Schalter und Kopplungen liegen dann in dessen Unterordner «labor».
/// </summary>
public sealed class LaborServer : IAsyncDisposable
{
    readonly WebApplicationFactory<LaborProgramm> basis = new();
    readonly WebApplicationFactory<LaborProgramm> fabrik;
    public string Ordner { get; }
    public string BenutzerOrdner => Path.Combine(Ordner, "benutzer");
    /// <summary>Wie %LOCALAPPDATA%\HLKS_CAD\xr: das Zertifikat je PC (gemeinsam mit KICAD).</summary>
    public string ZertifikatOrdner { get; }
    /// <summary>Wie %LOCALAPPDATA%\HLKS_CAD\xr\labor: Schalter und Kopplungen des Labors.</summary>
    public string LaborOrdner => Path.Combine(ZertifikatOrdner, LaborXr.Unterordner);
    public XrEinstellungen Xr { get; }
    public HttpClient Lokal { get; }
    public IServiceProvider Dienste => fabrik.Services;

    public LaborServer(bool zugang = false, string? zertifikatOrdner = null, Func<XrEinstellungen, XrEinstellungen>? anpassen = null, Action<IServiceCollection>? dienste = null)
    {
        Ordner = Path.Combine(Path.GetTempPath(), "hlks_labor_" + Guid.NewGuid().ToString("N")[..10]);
        ZertifikatOrdner = zertifikatOrdner ?? Path.Combine(Ordner, "xr");
        Directory.CreateDirectory(BenutzerOrdner);
        var e = new Einstellungen("Test", "testpc", Katalog.StandardOrdner(), null) { ZuletztMerken = false, BenutzerOrdner = BenutzerOrdner };
        // die Werte des Labors im Testlauf, nur mit eigenen Ordnern und ohne Sperre auf den Desktop-Port (wie XrServer bei KICAD)
        var xr = LaborXr.Vorgabe(true, _ => null) with { Ordner = LaborOrdner, ZertifikatOrdner = ZertifikatOrdner, DesktopPort = null, PruefAbstand = TimeSpan.FromHours(1) };
        Xr = anpassen is null ? xr : anpassen(xr);
        if (zugang) SchalterSetzen(true);
        fabrik = basis.WithWebHostBuilder(b => b.ConfigureTestServices(s => { s.AddSingleton(e); s.AddSingleton(Xr); dienste?.Invoke(s); }));
        Lokal = fabrik.CreateClient();
    }

    /// <summary>Schalter des Labors direkt in die Datei schreiben (zugang.json im Ordner des Labors).</summary>
    public void SchalterSetzen(bool an, int? port = null, int? suchPort = null) => new XrPcEinstellung(Xr).Setzen(an, port, suchPort);

    /// <summary>Eine Benutzereinstellung setzen (das Labor hat dafür keinen Endpunkt; es liest sie nur).</summary>
    public void BenutzerEinstellungSetzen(string schluessel, JsonNode? wert) =>
        new BenutzerEinstellungen(new RechnerAblage(BenutzerOrdner, "testpc", true, "einstellungen.json", "Umleitung")).Setzen(schluessel, wert);

    public async Task<JsonObject> Zugang() => (JsonObject)JsonNode.Parse(await Lokal.GetStringAsync("/api/xr/zugang"))!;

    /// <summary>Wartet, bis der Netzzugang läuft; gibt Port, Suchport und Fingerabdruck zurück.</summary>
    public async Task<(int Port, int SuchPort, string Fingerabdruck)> Aktiv()
    {
        var bis = DateTime.UtcNow.AddSeconds(20);
        while (true)
        {
            var z = await Zugang();
            if ((bool)z["aktiv"]!) return ((int)z["port"]!, (int)z["suchPort"]!, (string)z["fingerabdruck"]!);
            if (DateTime.UtcNow > bis) throw new TimeoutException("Der Netzzugang des Labors startet nicht: " + z.ToJsonString());
            await Task.Delay(50);
        }
    }

    public async Task<(HttpStatusCode Code, JsonNode? Json)> Get(string pfad)
    {
        var a = await Lokal.GetAsync(pfad);
        var t = await a.Content.ReadAsStringAsync();
        return (a.StatusCode, t.Length == 0 ? null : JsonNode.Parse(t));
    }

    public async Task<(HttpStatusCode Code, JsonNode? Json)> LokalPost(string pfad, object koerper)
    {
        var a = await Lokal.PostAsync(pfad, new StringContent(Json.Text(koerper), Encoding.UTF8, "application/json"));
        var t = await a.Content.ReadAsStringAsync();
        return (a.StatusCode, t.Length == 0 ? null : JsonNode.Parse(t));
    }

    /// <summary>Weg A über localhost: das Kopplungspaket des Labors.</summary>
    public async Task<JsonObject> Paket(string brille = "QUEST3-LABOR1", string? name = null)
    {
        var (code, j) = await LokalPost("/api/xr/kopplung/paket", new { brille, name });
        Assert.Equal(HttpStatusCode.OK, code);
        return (JsonObject)j!["paket"]!;
    }

    bool entsorgt;

    public async ValueTask DisposeAsync()
    {
        if (entsorgt) return;
        entsorgt = true;
        Lokal.Dispose();
        await fabrik.DisposeAsync();
        await basis.DisposeAsync();
        await Loeschen(Ordner);
    }

    public static async Task Loeschen(string ordner)
    {
        for (int i = 0; i < 8; i++)
        {
            try { if (Directory.Exists(ordner)) Directory.Delete(ordner, true); return; }
            catch (IOException) { await Task.Delay(250); }
            catch (UnauthorizedAccessException) { await Task.Delay(250); }
        }
    }
}

/// <summary>Das Labor als eigener Prozess («dotnet Hlks.Labor.dll --port …»), wie das Startskript es startet – im Testlauf (HLKS_TEST=1).</summary>
public sealed class LaborProzess : IAsyncDisposable
{
    readonly Process prozess;
    readonly StringBuilder ausgabe = new();
    public int Port { get; }
    public HttpClient Klient { get; }

    LaborProzess(Process p, int port)
    {
        prozess = p;
        Port = port;
        Klient = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}"), Timeout = TimeSpan.FromSeconds(10) };
    }

    public string Ausgabe { get { lock (ausgabe) return ausgabe.ToString(); } }

    /// <summary>Ein Port, der gerade frei ist (vom System vergeben).</summary>
    public static int FreierPort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var p = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return p;
    }

    public static LaborProzess Starten(int port)
    {
        var dll = Path.Combine(AppContext.BaseDirectory, "Hlks.Labor.dll");
        Assert.True(File.Exists(dll), dll);
        var psi = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } host && File.Exists(host) ? host : "dotnet")
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, WorkingDirectory = Path.GetTempPath(),
        };
        psi.ArgumentList.Add(dll);
        psi.ArgumentList.Add("--port");
        psi.ArgumentList.Add(port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        psi.Environment["HLKS_TEST"] = "1";   // Testlauf: Netzzugang nur 127.0.0.1, alles unter %TEMP%, nie ein echtes adb
        foreach (var n in new[] { "HLKS_BENUTZER", LaborOberflaeche.WebRootVariable, "HLKS_WEBROOT", LaborXr.DesktopPortVariable, XrEinstellungen.DesktopPortVariable, "ASPNETCORE_URLS", "ASPNETCORE_ENVIRONMENT" })
            psi.Environment.Remove(n);
        var p = new Process { StartInfo = psi };
        var l = new LaborProzess(p, port);
        p.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (l.ausgabe) l.ausgabe.AppendLine(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (l.ausgabe) l.ausgabe.AppendLine(e.Data); };
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        return l;
    }

    /// <summary>Wartet, bis GET /api/status antwortet (höchstens frist).</summary>
    public async Task<JsonObject> StatusAbwarten(TimeSpan frist)
    {
        var bis = DateTime.UtcNow + frist;
        while (true)
        {
            if (prozess.HasExited) throw new InvalidOperationException($"Das Labor hat sich beendet (Code {prozess.ExitCode}): {Ausgabe}");
            try { return (JsonObject)JsonNode.Parse(await Klient.GetStringAsync("/api/status"))!; }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) { }
            if (DateTime.UtcNow > bis) throw new TimeoutException("Das Labor antwortet nicht: " + Ausgabe);
            await Task.Delay(200);
        }
    }

    public async ValueTask DisposeAsync()
    {
        Klient.Dispose();
        try { if (!prozess.HasExited) prozess.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
        await prozess.WaitForExitAsync(new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token).ContinueWith(_ => { });
        prozess.Dispose();
    }
}
