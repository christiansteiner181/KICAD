// Quest App V1c (Modul XR, XR-I «Desktop in der Brille»): die Verbindung der App zum Bildschirm des PCs (wss://<PC>:5140/api/xr/desktop; Server
// src/Hlks.Web/XrDesktop.cs, Format Hlks.Xr.Desktop.DesktopFormat). Ohne StereoKit: Empfangen und Dekodieren im Hintergrund, die App lädt im Bild nur die
// geänderten Felder hoch (DesktopPuffer.Stand).
//
// Ablauf: nur auf Anforderung (der Bildschirm wird hineingezogen) verbinden – TLS mit Pinning, Bearer wie der Kanal –, «start» senden (Bildrate, grösste Breite,
// Monitor), dann:
//   PC → App: Text «bildschirm» { monitore[{ id, name, breite, hoehe, haupt }], monitor, breite, hoehe, kachel, bildrate } (beim Start und nach jedem Wechsel),
//             Text «fehler» { meldung }, binär ein Bild (DesktopFormat).
//   App → PC: «quittung» { nr } nach dem Dekodieren (der PC hat höchstens zwei Bilder unterwegs – so passt sich die Bildrate an Netz und Brille an), «pause» und
//             «weiter» (ausgeblendet: der PC erfasst nichts), «monitor» { id }, «bildrate» { wert, ruhe }, «neu» (Vollbild verlangen, z. B. nach einem Fehler).
//   «start» und «bildrate» tragen dazu die Ruhe-Bildrate («ruheBildrate» bzw. «ruhe»; Stellschraube desktop.ruhe_bildrate): ändert sich am PC 2 s lang nichts, erfasst
//   er nur noch so oft (Nachtrag V1c 24.10.2026).
// Schluss: 4010 = am PC beendet (nicht neu verbinden, erst wieder nach dem nächsten Hineinziehen), 4001 widerrufen, 4002 durch eine neue Verbindung derselben
// Brille ersetzt, 401 nicht gekoppelt – dann nicht neu verbinden; sonst (Netz weg) nach 1, 2, 4 … s neu, solange der Bildschirm gewünscht ist.
// Nichts geht auf die Platte: die Bilder stehen nur im Speicher (DesktopPuffer).

using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Hlks.Xr.Desktop;

namespace Hlks.Xr.Netz;

/// <summary>Zustand der Verbindung zum Bildschirm des PCs.</summary>
public enum DesktopZustand { Aus, Verbindet, Laeuft, Pausiert, Wartet, AmPcBeendet, Abgewiesen, Beendet }

/// <summary>Ein Monitor des PCs.</summary>
public sealed record DesktopMonitor(string Id, string Name, int Breite, int Hoehe, bool Haupt);

/// <summary>Einstellungen der Verbindung.</summary>
public sealed record DesktopOptionen
{
    public double Bildrate { get; init; } = DesktopEinstellungen.Vorgabe.Bildrate;
    /// <summary>Bilder je Sekunde, solange sich am PC nichts ändert (desktop.ruhe_bildrate).</summary>
    public double RuheBildrate { get; init; } = DesktopEinstellungen.Vorgabe.RuheBildrate;
    public int MaxBreite { get; init; } = DesktopEinstellungen.Vorgabe.MaxBreitePx;
    /// <summary>Gewünschter Monitor (Id des PCs); null = Hauptbildschirm.</summary>
    public string? Monitor { get; init; }
    public TimeSpan Frist { get; init; } = TimeSpan.FromSeconds(10);
    /// <summary>Wartezeit vor dem nächsten Versuch (Tests verkürzen sie); Vorgabe KanalRegeln.Wartezeit.</summary>
    public Func<int, TimeSpan>? Wartezeit { get; init; }
    /// <summary>Grösste Nachricht des PCs (Bytes; ein Vollbild ist meist unter 2 MB).</summary>
    public int NachrichtMax { get; init; } = 32 * 1024 * 1024;
}

/// <summary>Schliess-Codes des Servers für den Bildschirm.</summary>
public static class DesktopRegeln
{
    public const int AmPcBeendet = 4010;

    /// <summary>Nach diesem Schluss neu verbinden? Nein nach 4010 (am PC beendet), 4001 (widerrufen), 4002 (ersetzt).</summary>
    public static bool NeuVerbinden(int? schluss) => schluss is not (AmPcBeendet or KanalRegeln.Widerrufen or KanalRegeln.Ersetzt);

    /// <summary>Adresse des Bildschirms beim PC.</summary>
    public static Uri Adresse(PcAdresse a) => new($"wss://{a.Ip}:{a.Port}/api/xr/desktop");

    public static string Start(double bildrate, int maxBreite, string? monitor, bool pausiert, double? ruheBildrate = null)
    {
        var o = new JsonObject { ["typ"] = "start", ["fassung"] = KanalText.Fassung, ["bildrate"] = Math.Round(bildrate, 2), ["maxBreite"] = maxBreite, ["pausiert"] = pausiert };
        if (ruheBildrate is { } r) o["ruheBildrate"] = Math.Round(r, 2);
        if (monitor is not null) o["monitor"] = monitor;
        return o.ToJsonString();
    }

    public static string Einfach(string typ, string? feld = null, JsonNode? wert = null)
    {
        var o = new JsonObject { ["typ"] = typ, ["fassung"] = KanalText.Fassung };
        if (feld is not null) o[feld] = wert;
        return o.ToJsonString();
    }
}

/// <summary>Die Verbindung zum Bildschirm des PCs (siehe Dateikopf).</summary>
public sealed class DesktopVerbindung : IAsyncDisposable
{
    readonly Kopplungspaket paket;
    readonly Func<CancellationToken, Task<Uri?>> adresse;
    readonly DesktopOptionen optionen;
    readonly CancellationTokenSource stopp = new();
    readonly SemaphoreSlim sendeSperre = new(1, 1);
    readonly object messSperre = new();
    readonly Queue<(double T, long Bytes)> zuletzt = new();
    readonly Stopwatch uhr = Stopwatch.StartNew();
    Task? schleife;
    ClientWebSocket? aktuell;
    volatile DesktopZustand zustand = DesktopZustand.Aus;
    volatile bool pausiert;
    volatile DesktopPuffer? puffer;
    double bildrate, ruheBildrate;
    string? monitor;
    double dekodierSumme;
    int beendet;

    public DesktopVerbindung(Kopplungspaket paket, Func<CancellationToken, Task<Uri?>> adresse, DesktopOptionen? optionen = null)
    {
        this.paket = paket;
        this.adresse = adresse;
        this.optionen = optionen ?? new DesktopOptionen();
        bildrate = this.optionen.Bildrate;
        ruheBildrate = this.optionen.RuheBildrate;
        monitor = this.optionen.Monitor;
    }

    public DesktopZustand Zustand => zustand;
    /// <summary>Die Bildpunkte (null bis zum ersten Vollbild); ein neuer Monitor bzw. eine neue Auflösung gibt einen neuen Puffer.</summary>
    public DesktopPuffer? Puffer => puffer;
    public IReadOnlyList<DesktopMonitor> Monitore { get; private set; } = [];
    /// <summary>Id des Monitors, den der PC gerade sendet.</summary>
    public string? Monitor { get; private set; }
    public int? LetzterSchluss { get; private set; }
    public string? LetzteMeldung { get; private set; }
    public long Bilder => Interlocked.Read(ref bilder);
    public long Bytes => Interlocked.Read(ref bytes);
    long bilder, bytes;
    public double DekodierMsMax { get; private set; }
    public double DekodierMsMittel => Bilder > 0 ? dekodierSumme / Bilder : 0;
    /// <summary>Vollbilder verlangt (nach einem Fehler oder einem fremden Raster).</summary>
    public int Neuanforderungen { get; private set; }
    /// <summary>Wie oft die Verbindung stand («bildschirm» kam).</summary>
    public int Verbindungen { get; private set; }

    /// <summary>Bilder je Sekunde und kbit/s über die letzten zwei Sekunden.</summary>
    public (double BilderJeSekunde, double KbitJeSekunde) Rate()
    {
        lock (messSperre)
        {
            var t = uhr.Elapsed.TotalSeconds;
            while (zuletzt.Count > 0 && zuletzt.Peek().T < t - 2) zuletzt.Dequeue();
            return (zuletzt.Count / 2.0, zuletzt.Sum(x => x.Bytes) * 8 / 2.0 / 1000);
        }
    }

    /// <summary>Verbinden (bzw. nach einer Pause weiter).</summary>
    public void Starten()
    {
        if (Volatile.Read(ref beendet) == 1) return;
        if (pausiert) Weiter();
        schleife ??= Task.Run(() => Schleife(stopp.Token));
    }

    /// <summary>Ausgeblendet: der PC erfasst nichts mehr, die Verbindung bleibt.</summary>
    public void Pausieren()
    {
        if (pausiert) return;
        pausiert = true;
        if (zustand == DesktopZustand.Laeuft) zustand = DesktopZustand.Pausiert;
        _ = Senden(DesktopRegeln.Einfach("pause"));
    }

    public void Weiter()
    {
        if (!pausiert) return;
        pausiert = false;
        if (zustand == DesktopZustand.Pausiert) zustand = DesktopZustand.Laeuft;
        _ = Senden(DesktopRegeln.Einfach("weiter"));
    }

    public bool Pausiert => pausiert;

    public void MonitorWaehlen(string id)
    {
        monitor = id;
        _ = Senden(DesktopRegeln.Einfach("monitor", "id", id));
    }

    /// <summary>Bildrate (und Ruhe-Bildrate) ändern; gesendet nur bei einer Änderung.</summary>
    public void BildrateSetzen(double wert, double? ruhe = null)
    {
        var r = ruhe ?? ruheBildrate;
        if (Math.Abs(wert - bildrate) < 1e-9 && Math.Abs(r - ruheBildrate) < 1e-9) return;
        bildrate = wert;
        ruheBildrate = r;
        _ = Senden(new JsonObject { ["typ"] = "bildrate", ["fassung"] = KanalText.Fassung, ["wert"] = Math.Round(wert, 2), ["ruhe"] = Math.Round(r, 2) }.ToJsonString());
    }

    async Task Schleife(CancellationToken ende)
    {
        int versuch = 0;
        while (!ende.IsCancellationRequested)
        {
            zustand = DesktopZustand.Verbindet;
            Uri? ziel = null;
            try { ziel = await adresse(ende).ConfigureAwait(false); }
            catch (OperationCanceledException) when (ende.IsCancellationRequested) { break; }
            catch (Exception ex) when (ex is not OperationCanceledException) { LetzteMeldung = ex.Message; }
            int? schluss = null;
            bool abgewiesen = false;
            if (ziel is not null)
            {
                bool war;
                try { (schluss, abgewiesen, war) = await EineVerbindung(ziel, ende).ConfigureAwait(false); }
                catch (OperationCanceledException) when (ende.IsCancellationRequested) { break; }
                if (war) versuch = 0;
            }
            LetzterSchluss = schluss;
            if (schluss == DesktopRegeln.AmPcBeendet) { zustand = DesktopZustand.AmPcBeendet; return; }
            if (abgewiesen || !DesktopRegeln.NeuVerbinden(schluss)) { zustand = DesktopZustand.Abgewiesen; return; }
            zustand = DesktopZustand.Wartet;
            try { await Task.Delay((optionen.Wartezeit ?? KanalRegeln.Wartezeit)(versuch), ende).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            versuch++;
        }
        zustand = DesktopZustand.Beendet;
    }

    async Task<(int? Schluss, bool Abgewiesen, bool WarVerbunden)> EineVerbindung(Uri ziel, CancellationToken ende)
    {
        using var ws = new ClientWebSocket();
        ws.Options.RemoteCertificateValidationCallback = XrPinning.Pruefung(paket.Fingerabdruck);
        ws.Options.SetRequestHeader("Authorization", "Bearer " + paket.Token);
        ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);
        ws.Options.CollectHttpResponseDetails = true;
        ws.Options.Proxy = null;
        using (var frist = CancellationTokenSource.CreateLinkedTokenSource(ende))
        {
            frist.CancelAfter(optionen.Frist);
            try { await ws.ConnectAsync(ziel, frist.Token).ConfigureAwait(false); }
            catch (Exception ex) when (ex is WebSocketException or HttpRequestException or OperationCanceledException or IOException)
            {
                if (ende.IsCancellationRequested) throw new OperationCanceledException(ende);
                LetzteMeldung = ex.Message;
                return (null, ws.HttpStatusCode == System.Net.HttpStatusCode.Unauthorized, false);
            }
        }
        aktuell = ws;
        var war = false;
        var eingang = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(4) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
        using var verbindung = CancellationTokenSource.CreateLinkedTokenSource(ende);
        var dekodieren = Task.Run(() => Dekodieren(ws, eingang.Reader, verbindung.Token));
        try
        {
            await SendenRoh(ws, DesktopRegeln.Start(bildrate, optionen.MaxBreite, monitor, pausiert, ruheBildrate), verbindung.Token).ConfigureAwait(false);
            while (true)
            {
                var (typ, daten) = await Empfangen(ws, verbindung.Token).ConfigureAwait(false);
                if (daten is null) break;
                if (typ == WebSocketMessageType.Binary) { await eingang.Writer.WriteAsync(daten, verbindung.Token).ConfigureAwait(false); continue; }
                if (Text(Encoding.UTF8.GetString(daten))) { war = true; Verbindungen++; zustand = pausiert ? DesktopZustand.Pausiert : DesktopZustand.Laeuft; }
            }
            return (Code(ws), false, war);
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or IOException or ObjectDisposedException or ChannelClosedException)
        {
            if (ende.IsCancellationRequested) throw new OperationCanceledException(ende);
            LetzteMeldung = ex.Message;
            return (Code(ws), false, war);
        }
        finally
        {
            eingang.Writer.TryComplete();
            verbindung.Cancel();
            try { await dekodieren.ConfigureAwait(false); } catch (Exception ex) when (ex is OperationCanceledException or WebSocketException or ObjectDisposedException) { }
            aktuell = null;
            if (ws.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                try
                {
                    using var zu = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "ende", zu.Token).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException) { }
            }
        }
    }

    /// <summary>Textnachricht des PCs; true bei «bildschirm».</summary>
    bool Text(string text)
    {
        if (KanalText.Lesen(text) is not { } n) return false;
        var (typ, o) = n;
        if (typ == "fehler")
        {
            LetzteMeldung = o.TryGetProperty("meldung", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : "Fehler ohne Meldung";
            return false;
        }
        if (typ != "bildschirm") return false;
        var liste = new List<DesktopMonitor>();
        if (o.TryGetProperty("monitore", out var ms) && ms.ValueKind == JsonValueKind.Array)
            foreach (var x in ms.EnumerateArray())
                if (x.ValueKind == JsonValueKind.Object && x.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                    liste.Add(new DesktopMonitor(id.GetString()!, x.TryGetProperty("name", out var na) && na.ValueKind == JsonValueKind.String ? na.GetString()! : id.GetString()!,
                        x.TryGetProperty("breite", out var b) && b.TryGetInt32(out var bi) ? bi : 0, x.TryGetProperty("hoehe", out var h) && h.TryGetInt32(out var hi) ? hi : 0,
                        x.TryGetProperty("haupt", out var ha) && ha.ValueKind == JsonValueKind.True));
        Monitore = liste;
        Monitor = o.TryGetProperty("monitor", out var mo) && mo.ValueKind == JsonValueKind.String ? mo.GetString() : null;
        return true;
    }

    async Task Dekodieren(ClientWebSocket ws, ChannelReader<byte[]> eingang, CancellationToken abbruch)
    {
        await foreach (var b in eingang.ReadAllAsync(abbruch).ConfigureAwait(false))
        {
            var t0 = Stopwatch.GetTimestamp();
            uint? nr = null;
            try
            {
                var k = DesktopFormat.KopfLesen(b);
                nr = k.Nr;
                var p = puffer;
                if (p is null || !p.Passt(k))
                {
                    if (k.Voll) { p = new DesktopPuffer(new DesktopRaster(k.Breite, k.Hoehe, k.Kachel)); }
                    else { p = null; Neuanforderungen++; await SendenRoh(ws, DesktopRegeln.Einfach("neu"), abbruch).ConfigureAwait(false); }
                }
                if (p is not null)
                {
                    p.Anwenden(b, k);
                    puffer = p;
                    var ms = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
                    dekodierSumme += ms;
                    DekodierMsMax = Math.Max(DekodierMsMax, ms);
                    Interlocked.Increment(ref bilder);
                    Interlocked.Add(ref bytes, b.Length);
                    lock (messSperre) zuletzt.Enqueue((uhr.Elapsed.TotalSeconds, b.Length));
                }
            }
            catch (DesktopFormatFehler f)
            {
                LetzteMeldung = f.Message;
                Neuanforderungen++;
                await SendenRoh(ws, DesktopRegeln.Einfach("neu"), abbruch).ConfigureAwait(false);
            }
            if (nr is { } n) await SendenRoh(ws, DesktopRegeln.Einfach("quittung", "nr", n), abbruch).ConfigureAwait(false);
        }
    }

    static int? Code(ClientWebSocket ws) => ws.CloseStatus is { } s ? (int)s : null;

    async Task Senden(string text)
    {
        if (aktuell is not { State: WebSocketState.Open } ws) return;
        try
        {
            using var f = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await SendenRoh(ws, text, f.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException) { }
    }

    async Task SendenRoh(ClientWebSocket ws, string text, CancellationToken abbruch)
    {
        await sendeSperre.WaitAsync(abbruch).ConfigureAwait(false);
        try { await ws.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, abbruch).ConfigureAwait(false); }
        finally { sendeSperre.Release(); }
    }

    async Task<(WebSocketMessageType Typ, byte[]? Daten)> Empfangen(ClientWebSocket ws, CancellationToken abbruch)
    {
        var puffer = new byte[64 * 1024];
        using var ms = new MemoryStream();
        while (true)
        {
            var r = await ws.ReceiveAsync(puffer.AsMemory(), abbruch).ConfigureAwait(false);
            if (r.MessageType == WebSocketMessageType.Close) return (r.MessageType, null);
            if (ms.Length + r.Count > optionen.NachrichtMax) throw new WebSocketException("Nachricht des PCs zu gross.");
            ms.Write(puffer, 0, r.Count);
            if (r.EndOfMessage) return (r.MessageType, ms.ToArray());
        }
    }

    /// <summary>Beendet die Verbindung (schliesst normal, verbindet nicht neu).</summary>
    public async Task Beenden()
    {
        if (Interlocked.Exchange(ref beendet, 1) == 1) return;
        if (aktuell is { State: WebSocketState.Open } ws)
        {
            // erst sauber schliessen (der PC beendet die Erfassung sofort), dann die Schleife abbrechen
            try
            {
                using var f = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await sendeSperre.WaitAsync(f.Token).ConfigureAwait(false);
                try { await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "tschuess", f.Token).ConfigureAwait(false); }
                finally { sendeSperre.Release(); }
            }
            catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException) { }
        }
        stopp.Cancel();
        if (schleife is not null)
        {
            try { await schleife.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false); }
            catch (Exception ex) when (ex is TimeoutException or OperationCanceledException) { }
        }
        zustand = DesktopZustand.Beendet;
    }

    public bool IstBeendet => Volatile.Read(ref beendet) == 1;

    public async ValueTask DisposeAsync() => await Beenden().ConfigureAwait(false);
}
