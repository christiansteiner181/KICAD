using System.Net;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Hlks.Kern;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Hlks.Web.Tests;

/// <summary>Uhr für Tests (Fristen, Bremsen); Lesen und Vorstellen sind gesperrt.</summary>
public sealed class XrTestUhr
{
    readonly object sperre = new();
    DateTimeOffset jetzt = DateTimeOffset.UtcNow;
    public DateTimeOffset Jetzt { get { lock (sperre) return jetzt; } }
    public void Vor(TimeSpan d) { lock (sperre) jetzt += d; }
}

/// <summary>
/// KICAD-Server für die Tests des XR-Zugangs (Quest App P3): der Hauptserver als TestServer (localhost), der Netzzugang als echter Kestrel-Server – aber nur auf
/// 127.0.0.1 mit freien Ports (nie im Netz, keine Firewall-Abfrage). Benutzereinstellungen und XR-Ordner liegen unter %TEMP%.
/// </summary>
public sealed class XrServer : IAsyncDisposable
{
    readonly WebApplicationFactory<Program> basis = new();
    readonly WebApplicationFactory<Program> fabrik;
    public string Ordner { get; } = Path.Combine(Path.GetTempPath(), "hlks_xr_" + Guid.NewGuid().ToString("N")[..10]);
    public string BenutzerOrdner => Path.Combine(Ordner, "benutzer");
    public string XrOrdner => Path.Combine(Ordner, "xr");
    public HttpClient Lokal { get; }
    public XrEinstellungen Xr { get; }
    public IServiceProvider Dienste => fabrik.Services;

    public XrServer(bool zugang = true, Func<XrEinstellungen, XrEinstellungen>? anpassen = null, XrTestUhr? uhr = null)
    {
        Directory.CreateDirectory(BenutzerOrdner);
        var e = new Einstellungen("Test", "testpc", Katalog.StandardOrdner(), null) { ZuletztMerken = false, BenutzerOrdner = BenutzerOrdner };
        var xr = new XrEinstellungen
        {
            Adresse = IPAddress.Loopback, Port = 0, SuchPort = 0, Ordner = XrOrdner, DesktopPort = null, PruefAbstand = TimeSpan.FromHours(1),
            Uhr = uhr is null ? null : () => uhr.Jetzt,
        };
        Xr = anpassen is null ? xr : anpassen(xr);
        if (zugang) SchalterSetzen(true);   // der Schalter dieses PCs (zugang.json im XR-Ordner), vor dem Start
        fabrik = basis.WithWebHostBuilder(b => b.ConfigureTestServices(s => { s.AddSingleton(e); s.AddSingleton(Xr); }));
        Lokal = fabrik.CreateClient();
    }

    /// <summary>Schalter dieses PCs direkt in die Datei schreiben (wie ein anderer KICAD-Prozess auf demselben PC).</summary>
    public void SchalterSetzen(bool an, int? port = null, int? suchPort = null) => new XrPcEinstellung(Xr).Setzen(an, port, suchPort);

    /// <summary>Eine Benutzereinstellung setzen (gilt über OneDrive für alle Rechner des Benutzers).</summary>
    public void BenutzerEinstellungSetzen(string schluessel, JsonNode? wert) =>
        new BenutzerEinstellungen(new RechnerAblage(BenutzerOrdner, "testpc", true, "einstellungen.json", "Umleitung")).Setzen(schluessel, wert);

    public string ProjektOrdner => Path.Combine(Ordner, "projekt", "c_Pläne und Modelle", "e_KICAD");

    /// <summary>Beispielprojekt «MFH Musterweg» anlegen und öffnen (für die GLB je Geschoss).</summary>
    public async Task ProjektAnlegen()
    {
        var (code, j) = await LokalPost("/api/projekt/anlegen", new { ordner = ProjektOrdner, name = "MFH Musterweg", beispiel = true });
        Assert.True(code == HttpStatusCode.OK, j?.ToJsonString());
    }

    public async Task<JsonObject> Zugang() => (JsonObject)JsonNode.Parse(await Lokal.GetStringAsync("/api/xr/zugang"))!;

    /// <summary>Wartet, bis der Netzzugang läuft; gibt Port, Suchport und Fingerabdruck zurück.</summary>
    public async Task<(int Port, int SuchPort, string Fingerabdruck)> Aktiv()
    {
        var bis = DateTime.UtcNow.AddSeconds(20);
        while (true)
        {
            var z = await Zugang();
            if ((bool)z["aktiv"]!) return ((int)z["port"]!, (int)z["suchPort"]!, (string)z["fingerabdruck"]!);
            if (DateTime.UtcNow > bis) throw new TimeoutException("Der XR-Zugang startet nicht: " + z.ToJsonString());
            await Task.Delay(50);
        }
    }

    public async Task<(HttpStatusCode Code, JsonNode? Json)> LokalPost(string pfad, object koerper)
    {
        var a = await Lokal.PostAsync(pfad, new StringContent(Json.Text(koerper), Encoding.UTF8, "application/json"));
        var t = await a.Content.ReadAsStringAsync();
        return (a.StatusCode, t.Length == 0 ? null : JsonNode.Parse(t));
    }

    /// <summary>Weg A über localhost: das Kopplungspaket.</summary>
    public async Task<JsonObject> Paket(string brille = "QUEST3-TEST1", string? name = null)
    {
        var (code, j) = await LokalPost("/api/xr/kopplung/paket", new { brille, name });
        Assert.Equal(HttpStatusCode.OK, code);
        return (JsonObject)j!["paket"]!;
    }

    public static string Token(JsonObject paket) => $"{(string)paket["kopplung"]!}.{(string)paket["schluessel"]!}";

    /// <summary>Wie die App: das Zertifikat nur annehmen, wenn sein SHA-256-Fingerabdruck dem gepinnten entspricht.</summary>
    public static bool Pinnen(X509Certificate? zertifikat, string fingerabdruck) =>
        zertifikat is not null && string.Equals(Convert.ToHexStringLower(SHA256.HashData(zertifikat.GetRawCertData())), fingerabdruck, StringComparison.Ordinal);

    /// <summary>HTTP-Client wie die App: HTTPS auf 127.0.0.1, Fingerabdruck gepinnt, optional mit Schlüssel.</summary>
    public static HttpClient Netz(int port, string fingerabdruck, string? token = null)
    {
        var h = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromSeconds(30) };
        h.SslOptions.RemoteCertificateValidationCallback = (_, z, _, _) => Pinnen(z, fingerabdruck);
        var c = new HttpClient(h) { BaseAddress = new Uri($"https://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(20) };
        if (token is not null) c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    /// <summary>HTTP-Client wie die App beim ersten Kontakt (Weg B): nimmt das Zertifikat an und merkt sich dessen SHA-256 (gesehen()).</summary>
    public static (HttpClient Client, Func<string?> Gesehen) NetzErstkontakt(int port)
    {
        string? fp = null;
        var h = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromSeconds(30) };
        h.SslOptions.RemoteCertificateValidationCallback = (_, z, _, _) =>
        {
            if (z is null) return false;
            fp = Convert.ToHexStringLower(SHA256.HashData(z.GetRawCertData()));
            return true;
        };
        return (new HttpClient(h) { BaseAddress = new Uri($"https://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(20) }, () => fp);
    }

    bool entsorgt;

    public async ValueTask DisposeAsync()
    {
        if (entsorgt) return;
        entsorgt = true;
        try { await Lokal.PostAsync("/api/projekt/schliessen", new StringContent("{}", Encoding.UTF8, "application/json")); } catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException) { }
        Lokal.Dispose();
        await fabrik.DisposeAsync();
        await basis.DisposeAsync();
        for (int i = 0; i < 5; i++)
        {
            try { if (Directory.Exists(Ordner)) Directory.Delete(Ordner, true); return; }
            catch (IOException) { await Task.Delay(200); }
            catch (UnauthorizedAccessException) { await Task.Delay(200); }
        }
    }
}

/// <summary>
/// Die Brille bei der Ersatzkopplung (Weg B) – unabhängig vom Server gerechnet, genau wie in XrKopplung.cs beschrieben: Zufallszahl (32 Byte),
/// Verpflichtung = base64url(SHA-256(Zufallszahl)), Vergleichscode = SHA-256("hlks-xr-vergleich|1|fp|zufallBrille|zufallPc"), erste 8 Byte big-endian mod 1 000 000.
/// </summary>
public sealed class XrTestBrille
{
    public byte[] Zufall { get; } = RandomNumberGenerator.GetBytes(32);
    public string ZufallText => System.Buffers.Text.Base64Url.EncodeToString(Zufall);
    public string Verpflichtung => System.Buffers.Text.Base64Url.EncodeToString(SHA256.HashData(Zufall));

    public static string Code(string fingerabdruck, string zufallBrille, string zufallPc)
    {
        var h = SHA256.HashData(Encoding.UTF8.GetBytes("hlks-xr-vergleich|1|" + fingerabdruck + "|" + zufallBrille + "|" + zufallPc));
        ulong z = 0;
        for (int i = 0; i < 8; i++) z = (z << 8) | h[i];
        return (z % 1_000_000).ToString("000000");
    }
}

/// <summary>Ein Teilnehmer im Live-Kanal wie die App: WebSocket über TLS mit Pinning und Schlüssel; liest im Hintergrund in eine Schlange.</summary>
public sealed class XrKlient : IAsyncDisposable
{
    readonly ClientWebSocket ws = new();
    readonly Channel<JsonObject> eingang = Channel.CreateUnbounded<JsonObject>();
    readonly CancellationTokenSource stopp = new();
    Task? leser;
    public WebSocketCloseStatus? Schluss { get; private set; }
    public string? SchlussGrund { get; private set; }
    public string? Id { get; private set; }
    public WebSocketState Zustand => ws.State;

    public static async Task<XrKlient> Verbinden(int port, string fingerabdruck, string token)
    {
        var k = new XrKlient();
        k.ws.Options.RemoteCertificateValidationCallback = (_, z, _, _) => XrServer.Pinnen(z, fingerabdruck);
        k.ws.Options.SetRequestHeader("Authorization", "Bearer " + token);
        using var frist = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await k.ws.ConnectAsync(new Uri($"wss://127.0.0.1:{port}/api/xr/kanal"), frist.Token);
        k.leser = k.Lesen();
        return k;
    }

    async Task Lesen()
    {
        var puffer = new byte[64 * 1024];
        try
        {
            while (ws.State == WebSocketState.Open || ws.State == WebSocketState.CloseSent)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult r;
                do
                {
                    r = await ws.ReceiveAsync(puffer, stopp.Token);
                    if (r.MessageType == WebSocketMessageType.Close)
                    {
                        Schluss = r.CloseStatus;
                        SchlussGrund = r.CloseStatusDescription;
                        if (ws.State == WebSocketState.CloseReceived) await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "ok", CancellationToken.None);
                        return;
                    }
                    ms.Write(puffer, 0, r.Count);
                } while (!r.EndOfMessage);
                eingang.Writer.TryWrite((JsonObject)JsonNode.Parse(Encoding.UTF8.GetString(ms.ToArray()))!);
            }
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException) { }
        finally { eingang.Writer.TryComplete(); }
    }

    public Task Senden(string text) => ws.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, CancellationToken.None);
    public Task Senden(JsonObject o) => Senden(o.ToJsonString());

    public static JsonObject N(string typ, JsonObject? inhalt = null)
    {
        var o = new JsonObject { ["typ"] = typ, ["fassung"] = 1 };
        if (inhalt is not null) foreach (var (k, w) in inhalt) o[k] = w?.DeepClone();
        return o;
    }

    /// <summary>Nächste Nachricht (oder null, wenn die Verbindung zu ist bzw. nach der Frist nichts kam).</summary>
    public async Task<JsonObject?> Naechste(TimeSpan? frist = null)
    {
        using var f = new CancellationTokenSource(frist ?? TimeSpan.FromSeconds(10));
        try { return await eingang.Reader.ReadAsync(f.Token); }
        catch (OperationCanceledException) { return null; }
        catch (ChannelClosedException) { return null; }
    }

    /// <summary>Wartet auf die nächste Nachricht dieses Typs (andere werden übersprungen).</summary>
    public async Task<JsonObject> Warten(string typ, TimeSpan? frist = null)
    {
        var bis = DateTime.UtcNow + (frist ?? TimeSpan.FromSeconds(10));
        while (true)
        {
            var rest = bis - DateTime.UtcNow;
            var n = rest > TimeSpan.Zero ? await Naechste(rest) : null;
            if (n is null) throw new TimeoutException($"Keine Nachricht «{typ}» (Schluss: {Schluss} {SchlussGrund}).");
            if ((string?)n["typ"] == typ) return n;
        }
    }

    /// <summary>Anmelden: hallo senden, willkommen abwarten; gibt willkommen zurück.</summary>
    public async Task<JsonObject> Hallo(string name = "Christian", string geraet = "Quest 3")
    {
        await Senden(N("hallo", new JsonObject { ["name"] = name, ["geraet"] = geraet, ["app"] = "0.1" }));
        var w = await Warten("willkommen");
        Id = (string)w["id"]!;
        return w;
    }

    /// <summary>Wartet, bis der Server die Verbindung schliesst; gibt den Status zurück.</summary>
    public async Task<WebSocketCloseStatus?> GeschlossenAbwarten(TimeSpan? frist = null)
    {
        if (leser is null) return Schluss;
        await Task.WhenAny(leser, Task.Delay(frist ?? TimeSpan.FromSeconds(10)));
        return Schluss;
    }

    public static JsonObject Pose(double x = 1000, double y = 2000, double z = 1700, bool pinch = false) => N("pose", new JsonObject
    {
        ["kopf"] = new JsonObject { ["pos"] = new JsonArray(x, y, z), ["rot"] = new JsonArray(0, 0, 0, 1) },
        ["links"] = new JsonObject
        {
            ["gelenk"] = new JsonObject { ["pos"] = new JsonArray(x - 200, y, z - 400), ["rot"] = new JsonArray(0, 0, 0, 1) },
            ["zeigefinger"] = new JsonArray(x - 150, y + 100, z - 380), ["pinch"] = pinch, ["pinchStaerke"] = pinch ? 0.9 : 0.1,
        },
        ["modus"] = "begehen",
    });

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (ws.State == WebSocketState.Open)
            {
                using var f = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "ende", f.Token);
            }
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException) { }
        stopp.CancelAfter(TimeSpan.FromSeconds(2));
        if (leser is not null) await Task.WhenAny(leser, Task.Delay(3000));
        ws.Dispose();
        stopp.Dispose();
    }
}
