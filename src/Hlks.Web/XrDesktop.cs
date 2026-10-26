// Quest App V1c (Modul XR, XR-I «Desktop in der Brille»; Christian, 24.10.2026: «ich habe an die ansicht von der metafunktion gedacht.» – «bedienung erfolgt über
// maus und tastatur des pc's»): KICAD schickt den ganzen Bildschirm des PCs live an die gekoppelte Brille. Bedient wird mit Maus und Tastatur am PC; die Brille
// zeigt nur an (Mauszeiger im Bild). Es gibt KEINE Eingaben aus der Brille an den PC.
//
//   GET  /api/xr/desktop               WebSocket, nur über den Netzzugang (TLS, Pforte, Kopplung; Marke XrImNetz) – ohne gültigen Schlüssel 401, ohne WebSocket 400.
//   GET  /api/xr/desktop/zustand       nur am PC (localhost): { erfassung, bildschirme[], verbunden, aktiv, stroeme[{ id, kopplung, brille, name, seit, monitor,
//                                      monitorName, breite, hoehe, pausiert, bildrate, ruheBildrate, ruhig, bilderJeSekunde, kbitJeSekunde, bilder, bytes, erfassungen,
//                                      erfassMs, kodierMs }] }
//   POST /api/xr/desktop/beenden       nur am PC: beendet alle Übertragungen (Schluss 4010 «am PC beendet»; die Brille verbindet erst nach dem nächsten Hineinziehen
//                                      wieder) → { beendet, zustand }
// Nachrichten (Text, je ein JSON-Objekt mit typ und fassung 1):
//   Brille → PC: start { bildrate?, ruheBildrate?, maxBreite?, monitor?, pausiert? } (innert 10 s), pause, weiter, quittung { nr }, monitor { id },
//                bildrate { wert, ruhe? }, neu, tschuess
//   PC → Brille: bildschirm { monitore[{ id, name, breite, hoehe, haupt }], monitor, breite, hoehe, kachel, bildrate }, fehler { meldung }; binär die Bilder
//                (Format src/Hlks.Xr/Desktop/DesktopFormat.cs, Kodierung XrDesktopKodierung.cs).
// Ablauf je Verbindung: erst «start», dann im Takt der Bildrate (1 … 30, Vorgabe der App 12) erfassen, mit dem zuletzt gesendeten Bild vergleichen und nur
// geänderte Kacheln senden; nie mehr als ImFlugMax (2) Bilder ohne Quittung – so passt sich die Bildrate an WLAN und Brille an. «pause» (Bildschirm in der Brille
// ausgeblendet): nichts erfassen. Ruhiger Bildschirm (Nachtrag V1c 24.10.2026: «Ändert sich nichts, erfasst sie seltener: z. B. nach 2 s ohne Änderung nur noch
// 2–3 Bilder/s, bei Änderung sofort wieder voll»): RuheNach (2 s) ohne geänderte Kachel → nur noch mit der Ruhe-Bildrate erfassen (Stellschraube der App
// desktop.ruhe_bildrate, Vorgabe 2.5); die erste Änderung sendet sofort und stellt den vollen Takt wieder her (Takt). GDI bleibt (DXGI Desktop Duplication: Vorrat).
// Trennung: Ende. Eine neue Verbindung derselben Kopplung ersetzt die alte (4002); Widerruf der Kopplung trennt (4001); höchstens
// StroemeMax Brillen zugleich (sonst 1013). Nichts wird aufgezeichnet: nur das zuletzt gesendete Bild steht im Speicher (für den Vergleich).
// Anzeige am PC: Statusleiste «Brille sieht den Bildschirm» mit «Beenden» (wwwroot/js/oberflaeche/xr-desktop-status.js). Für die KI verboten (KiEinordnung
// «xr_desktop»), kein MCP-Werkzeug.

using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Hlks.Web.EndpunktHilfe;

namespace Hlks.Web;

/// <summary>Grenzen und Vorgaben der Übertragung (Tests setzen eine eigene Fassung im Dienstcontainer).</summary>
public sealed record XrDesktopEinstellungen
{
    public double BildrateVorgabe { get; init; } = 12;
    public double BildrateMax { get; init; } = 30;
    /// <summary>Bilder je Sekunde bei ruhigem Bildschirm, wenn die Brille nichts angibt (wie desktop.ruhe_bildrate); Untergrenze 0.5.</summary>
    public double RuheBildrateVorgabe { get; init; } = 2.5;
    public double RuheBildrateMin { get; init; } = 0.5;
    /// <summary>So lange ohne geänderte Kachel, bis nur noch mit der Ruhe-Bildrate erfasst wird (Nachtrag V1c: «nach 2 s ohne Änderung»).</summary>
    public TimeSpan RuheNach { get; init; } = TimeSpan.FromSeconds(2);
    public int MaxBreiteVorgabe { get; init; } = 1920;
    public int MaxBreiteMin { get; init; } = 320;
    public int MaxBreiteMax { get; init; } = 3840;
    public int Kachel { get; init; } = XrDesktopKodierer.KachelVorgabe;
    /// <summary>Höchstens so viele Bilder ohne Quittung unterwegs (Flusskontrolle).</summary>
    public int ImFlugMax { get; init; } = 2;
    /// <summary>Höchstens so viele Brillen sehen gleichzeitig den Bildschirm.</summary>
    public int StroemeMax { get; init; } = 2;
    /// <summary>Grösste Textnachricht der Brille (Bytes).</summary>
    public int NachrichtMax { get; init; } = 4096;
    /// <summary>So lange hat die Brille nach dem Verbinden Zeit für «start».</summary>
    public TimeSpan StartFrist { get; init; } = TimeSpan.FromSeconds(10);
    /// <summary>So lange darf das Senden eines Bildes dauern, dann gilt die Brille als weg.</summary>
    public TimeSpan SendeFrist { get; init; } = TimeSpan.FromSeconds(15);
    /// <summary>So oft werden die Monitore neu gelesen (Auflösung geändert, Monitor ab- oder angesteckt).</summary>
    public TimeSpan MonitorePruefen { get; init; } = TimeSpan.FromSeconds(2);
}

public sealed class XrDesktopDienst
{
    /// <summary>Schliess-Codes: am PC beendet (die App verbindet nicht neu), Kopplung widerrufen und ersetzt wie im Kanal, kein Bildschirm.</summary>
    public const WebSocketCloseStatus AmPcBeendet = (WebSocketCloseStatus)4010;
    public const WebSocketCloseStatus KeinBildschirm = (WebSocketCloseStatus)4011;

    sealed class Strom
    {
        public required string Id;
        public required XrKopplung Kopplung;
        public required DateTimeOffset Seit;
        public required WebSocket Ws;
        public readonly CancellationTokenSource Abbruch = new();
        public readonly TaskCompletionSource Start = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly SemaphoreSlim Signal = new(0);
        public readonly SemaphoreSlim SendeSperre = new(1, 1);
        public readonly Queue<(double T, int Bytes)> Zuletzt = new();
        public volatile bool Pausiert, Neu, MonitorGewechselt;
        public string? Monitor, MonitorName;
        public int Breite, Hoehe, MaxBreite, ImFlug;
        public double Bildrate, RuheBildrate, LetzteAenderung;
        public volatile bool Ruhig;
        public long Bilder, Bytes, Kacheln, Erfassungen;
        public double ErfassMs, KodierMs;
        public (WebSocketCloseStatus Status, string Grund)? Schluss;
    }

    readonly XrDesktopEinstellungen e;
    readonly object sperre = new();
    readonly List<Strom> stroeme = [];
    readonly Stopwatch uhr = Stopwatch.StartNew();

    public XrDesktopDienst(XrDesktopEinstellungen einstellungen, IBildschirmQuelle quelle, XrKopplungen kopplungen)
    {
        e = einstellungen;
        Quelle = quelle;
        kopplungen.KopplungEntfernt += k => Trennen(s => s.Kopplung.Id == k, XrKanal.Widerrufen, "Kopplung widerrufen");
    }

    public IBildschirmQuelle Quelle { get; }

    /// <summary>Verbundene Brillen bzw. davon die, die gerade mitsehen (gestartet, nicht pausiert).</summary>
    public int Verbunden { get { lock (sperre) return stroeme.Count; } }
    public int Aktiv { get { lock (sperre) return stroeme.Count(s => s.Start.Task.IsCompleted && !s.Pausiert); } }

    // ---------------------------------------------------------------- Verbindung

    public async Task Bedienen(HttpContext ctx, XrKopplung kopplung)
    {
        using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
        var s = new Strom { Id = "d" + Guid.NewGuid().ToString("N")[..8], Kopplung = kopplung, Seit = DateTimeOffset.UtcNow, Ws = ws, Bildrate = e.BildrateVorgabe, RuheBildrate = e.RuheBildrateVorgabe, MaxBreite = e.MaxBreiteVorgabe };
        Strom? alt;
        bool voll;
        lock (sperre)
        {
            alt = stroeme.FirstOrDefault(x => x.Kopplung.Id == kopplung.Id);
            if (alt is not null) stroeme.Remove(alt);
            voll = stroeme.Count >= e.StroemeMax;
            if (!voll) stroeme.Add(s);
        }
        if (alt is not null) Schliessen(alt, XrKanal.Ersetzt, "durch eine neue Verbindung derselben Brille ersetzt");
        if (voll)
        {
            await Zu(ws, (WebSocketCloseStatus)1013, "zu viele Brillen sehen den Bildschirm");
            return;
        }
        var empfang = Empfangen(s, ctx.RequestAborted);
        try
        {
            using var startFrist = CancellationTokenSource.CreateLinkedTokenSource(s.Abbruch.Token);
            startFrist.CancelAfter(e.StartFrist);
            try { await s.Start.Task.WaitAsync(startFrist.Token); }
            catch (OperationCanceledException) when (!s.Abbruch.IsCancellationRequested) { s.Schluss ??= (WebSocketCloseStatus.PolicyViolation, "kein «start»"); }
            if (s.Start.Task.IsCompleted) await Schleife(s, ctx.RequestAborted);
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or IOException or ObjectDisposedException) { /* Brille weg */ }
        finally
        {
            lock (sperre) stroeme.Remove(s);
            s.Abbruch.Cancel();
            if (ws.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                var (st, gr) = s.Schluss ?? (WebSocketCloseStatus.NormalClosure, "ende");
                try
                {
                    using var f = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await s.SendeSperre.WaitAsync(f.Token);
                    try { await ws.CloseOutputAsync(st, gr, f.Token); } finally { s.SendeSperre.Release(); }
                }
                catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException or IOException) { }
            }
            // die Antwort der Brille auf das Schliessen abwarten (höchstens 2 s)
            await Task.WhenAny(empfang, Task.Delay(TimeSpan.FromSeconds(2)));
        }
    }

    static async Task Zu(WebSocket ws, WebSocketCloseStatus status, string grund)
    {
        try
        {
            using var f = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await ws.CloseAsync(status, grund, f.Token);
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException or IOException) { }
    }

    /// <summary>Beendet eine Verbindung vom PC aus: Grund merken, Senden stoppen; das Schliessen macht Bedienen (mit diesem Code).</summary>
    static void Schliessen(Strom s, WebSocketCloseStatus status, string grund)
    {
        s.Schluss ??= (status, grund);
        try { s.Abbruch.Cancel(); } catch (ObjectDisposedException) { }
        s.Signal.Release();
    }

    int Trennen(Func<Strom, bool> welche, WebSocketCloseStatus status, string grund)
    {
        List<Strom> weg;
        lock (sperre) weg = [.. stroeme.Where(welche)];
        foreach (var s in weg) Schliessen(s, status, grund);
        return weg.Count;
    }

    /// <summary>Alle Übertragungen beenden (Knopf «Beenden» am PC). Gibt die Zahl der beendeten zurück.</summary>
    public int Beenden() => Trennen(_ => true, AmPcBeendet, "am PC beendet");

    /// <summary>Der Netzzugang wird beendet (XrNetzDienst): alle trennen (1001; die App verbindet später neu, wenn der Zugang wieder offen ist).</summary>
    public int AlleTrennen() => Trennen(_ => true, WebSocketCloseStatus.EndpointUnavailable, "XR-Zugang beendet");

    // ---------------------------------------------------------------- Nachrichten der Brille

    async Task Empfangen(Strom s, CancellationToken verbindung)
    {
        var puffer = new byte[1024];
        try
        {
            while (s.Ws.State is WebSocketState.Open or WebSocketState.CloseSent)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult r;
                do
                {
                    r = await s.Ws.ReceiveAsync(puffer, verbindung);
                    if (r.MessageType == WebSocketMessageType.Close) { Schliessen(s, WebSocketCloseStatus.NormalClosure, "tschuess"); return; }
                    if (r.MessageType != WebSocketMessageType.Text) { Schliessen(s, WebSocketCloseStatus.InvalidMessageType, "nur Textnachrichten"); return; }
                    if (ms.Length + r.Count > e.NachrichtMax) { Schliessen(s, WebSocketCloseStatus.MessageTooBig, "Nachricht zu gross"); return; }
                    ms.Write(puffer, 0, r.Count);
                } while (!r.EndOfMessage);
                await Verarbeiten(s, Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length));
            }
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or IOException or ObjectDisposedException)
        {
            Schliessen(s, WebSocketCloseStatus.NormalClosure, "weg");
        }
    }

    async Task Verarbeiten(Strom s, string text)
    {
        try
        {
            var (typ, o) = XrNachrichten.Lesen(text);
            switch (typ)
            {
                case "start":
                    if (Zahl(o, "bildrate") is { } br) s.Bildrate = Math.Clamp(br, 1, e.BildrateMax);
                    if (Zahl(o, "ruheBildrate") is { } rb) s.RuheBildrate = Math.Clamp(rb, e.RuheBildrateMin, e.BildrateMax);
                    if (Zahl(o, "maxBreite") is { } mb) s.MaxBreite = (int)Math.Clamp(mb, e.MaxBreiteMin, e.MaxBreiteMax);
                    if (o["monitor"] is JsonValue mv && mv.TryGetValue<string>(out var mid) && mid.Length <= 64) s.Monitor = mid;
                    if (o["pausiert"] is JsonValue pv && pv.TryGetValue<bool>(out var p)) s.Pausiert = p;
                    s.Start.TrySetResult();
                    break;
                case "pause": s.Pausiert = true; break;
                case "weiter": s.Pausiert = false; break;
                case "quittung":
                    if (Volatile.Read(ref s.ImFlug) > 0) Interlocked.Decrement(ref s.ImFlug);
                    break;
                case "monitor":
                    if (o["id"] is JsonValue iv && iv.TryGetValue<string>(out var id) && Quelle.Bildschirme().Any(m => m.Id == id)) { s.Monitor = id; s.MonitorGewechselt = true; }
                    else throw new XrNachrichtFehler("Diesen Monitor gibt es nicht.");
                    break;
                case "bildrate":
                    s.Bildrate = Zahl(o, "wert") is { } w ? Math.Clamp(w, 1, e.BildrateMax) : throw new XrNachrichtFehler("«wert» fehlt.");
                    if (Zahl(o, "ruhe") is { } ru) s.RuheBildrate = Math.Clamp(ru, e.RuheBildrateMin, e.BildrateMax);
                    break;
                case "neu": s.Neu = true; break;
                case "tschuess": Schliessen(s, WebSocketCloseStatus.NormalClosure, "tschuess"); break;
                default: throw new XrNachrichtFehler($"Unbekannter Typ «{(typ.Length > 20 ? typ[..20] + "…" : typ)}».");
            }
            s.Signal.Release();
        }
        catch (XrNachrichtFehler f) { await TextSenden(s, XrNachrichten.Bauen("fehler", new JsonObject { ["meldung"] = f.Message })); }
    }

    /// <summary>
    /// Abstand bis zur nächsten Erfassung (s): voller Takt (bildrate), bei ruhigem Bildschirm (ruhig = RuheNach ohne Änderung) der Ruhe-Takt – nie schneller als der
    /// volle; beide auf 1/max begrenzt.
    /// </summary>
    public static double Takt(double bildrate, double ruheBildrate, bool ruhig, double max)
    {
        var voll = Math.Clamp(bildrate, 1, max);
        return 1 / (ruhig ? Math.Min(voll, Math.Clamp(ruheBildrate, 0.01, max)) : voll);
    }

    static double? Zahl(JsonObject o, string name) => o[name] is JsonValue v && v.TryGetValue<double>(out var d) && double.IsFinite(d) ? d : null;

    // ---------------------------------------------------------------- Erfassen und Senden

    async Task Schleife(Strom s, CancellationToken verbindung)
    {
        var kodierer = new XrDesktopKodierer(e.Kachel);
        BildschirmInfo? m = null;
        byte[] puffer = [];
        double naechste = 0, geprueft = double.NegativeInfinity;
        s.LetzteAenderung = uhr.Elapsed.TotalSeconds;   // die ersten RuheNach nach dem Start: voller Takt
        bool fehlerGemeldet = false;
        while (!s.Abbruch.IsCancellationRequested)
        {
            var jetzt = uhr.Elapsed.TotalSeconds;
            if (m is null || s.MonitorGewechselt || jetzt - geprueft >= e.MonitorePruefen.TotalSeconds)
            {
                geprueft = jetzt;
                var liste = Quelle.Bildschirme();
                var neu = liste.FirstOrDefault(x => x.Id == s.Monitor) ?? liste.FirstOrDefault(x => x.Haupt) ?? liste.FirstOrDefault();
                if (neu is null)
                {
                    await TextSenden(s, XrNachrichten.Bauen("fehler", new JsonObject { ["meldung"] = $"Kein Bildschirm zu erfassen (Erfassung «{Quelle.Art}»)." }));
                    s.Schluss ??= (KeinBildschirm, "kein Bildschirm");
                    return;
                }
                var (zb, zh) = XrDesktopQuellen.Ziel(neu.Breite, neu.Hoehe, s.MaxBreite);
                if (m is null || s.MonitorGewechselt || neu.Id != m.Id || zb != s.Breite || zh != s.Hoehe)
                {
                    m = neu;
                    lock (sperre) { s.Monitor = m.Id; s.MonitorName = m.Name; s.Breite = zb; s.Hoehe = zh; }
                    puffer = new byte[zb * zh * 4];
                    kodierer.Zuruecksetzen();
                    s.MonitorGewechselt = false;
                    s.LetzteAenderung = jetzt;
                    await TextSenden(s, Bildschirm(liste, m, zb, zh, s.Bildrate, kodierer.Kachel));
                }
            }
            if (s.Pausiert || Volatile.Read(ref s.ImFlug) >= e.ImFlugMax)
            {
                if (s.Pausiert) s.LetzteAenderung = jetzt;   // nach «weiter» zuerst im vollen Takt
                await Warten(s, TimeSpan.FromMilliseconds(500));
                continue;
            }
            var ruhig = jetzt - s.LetzteAenderung >= e.RuheNach.TotalSeconds;
            s.Ruhig = ruhig;
            var abstand = Takt(s.Bildrate, s.RuheBildrate, ruhig, e.BildrateMax);
            if (jetzt < naechste) { await Warten(s, TimeSpan.FromSeconds(Math.Min(naechste - jetzt, 1))); continue; }
            naechste = jetzt - naechste < abstand ? naechste + abstand : jetzt + abstand;   // im Takt bleiben, nach einer Pause nicht aufholen
            if (s.Neu) { s.Neu = false; kodierer.Zuruecksetzen(); s.LetzteAenderung = jetzt; }
            var t0 = Stopwatch.GetTimestamp();
            var ok = Quelle.Erfassen(m.Id, s.Breite, s.Hoehe, puffer);
            var erfassMs = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
            Interlocked.Increment(ref s.Erfassungen);
            if (!ok)
            {
                if (!fehlerGemeldet) await TextSenden(s, XrNachrichten.Bauen("fehler", new JsonObject { ["meldung"] = "Der Bildschirm lässt sich gerade nicht erfassen (gesperrt oder abgemeldet?)." }));
                fehlerGemeldet = true;
                continue;
            }
            fehlerGemeldet = false;
            var bild = kodierer.Kodieren(puffer, s.Breite, s.Hoehe, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            if (bild is null) continue;
            // geändert: sofort senden, und war der Bildschirm ruhig, gleich wieder im vollen Takt weiter
            var nun = uhr.Elapsed.TotalSeconds;
            s.LetzteAenderung = nun;
            if (ruhig) { s.Ruhig = false; naechste = nun + Takt(s.Bildrate, s.RuheBildrate, false, e.BildrateMax); }
            Interlocked.Increment(ref s.ImFlug);
            await Senden(s, bild.Daten, WebSocketMessageType.Binary, verbindung);
            lock (sperre)
            {
                s.Bilder++;
                s.Bytes += bild.Daten.Length;
                s.Kacheln += bild.Kacheln;
                s.ErfassMs = s.Bilder == 1 ? erfassMs : s.ErfassMs * 0.9 + erfassMs * 0.1;
                s.KodierMs = s.Bilder == 1 ? bild.Ms : s.KodierMs * 0.9 + bild.Ms * 0.1;
                s.Zuletzt.Enqueue((uhr.Elapsed.TotalSeconds, bild.Daten.Length));
            }
        }
    }

    static async Task Warten(Strom s, TimeSpan dauer)
    {
        try { await s.Signal.WaitAsync(dauer, s.Abbruch.Token); }
        catch (OperationCanceledException) { }
    }

    static string Bildschirm(IReadOnlyList<BildschirmInfo> liste, BildschirmInfo m, int b, int h, double bildrate, int kachel) =>
        XrNachrichten.Bauen("bildschirm", new JsonObject
        {
            ["monitore"] = new JsonArray([.. liste.Select(x => (JsonNode)new JsonObject { ["id"] = x.Id, ["name"] = x.Name, ["breite"] = x.Breite, ["hoehe"] = x.Hoehe, ["haupt"] = x.Haupt })]),
            ["monitor"] = m.Id, ["breite"] = b, ["hoehe"] = h, ["kachel"] = kachel, ["bildrate"] = bildrate,
        });

    Task TextSenden(Strom s, string text) => Senden(s, Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, CancellationToken.None);

    /// <summary>Senden mit eigener Frist (nicht mit dem Abbruch des Stroms: ein abgebrochenes Senden zerstörte die Verbindung, und das Schliessen mit Grund ginge verloren).</summary>
    async Task Senden(Strom s, byte[] daten, WebSocketMessageType typ, CancellationToken verbindung)
    {
        using var frist = CancellationTokenSource.CreateLinkedTokenSource(verbindung);
        frist.CancelAfter(e.SendeFrist);
        await s.SendeSperre.WaitAsync(frist.Token);
        try
        {
            if (s.Ws.State == WebSocketState.Open) await s.Ws.SendAsync(daten, typ, true, frist.Token);
        }
        finally { s.SendeSperre.Release(); }
    }

    // ---------------------------------------------------------------- Für den PC

    public JsonObject Zustand()
    {
        IReadOnlyList<BildschirmInfo> monitore;
        try { monitore = Quelle.Bildschirme(); }
        catch (Exception ex) when (ex is InvalidOperationException or DllNotFoundException or EntryPointNotFoundException) { monitore = []; }
        lock (sperre)
        {
            var t = uhr.Elapsed.TotalSeconds;
            var liste = new JsonArray();
            foreach (var s in stroeme)
            {
                while (s.Zuletzt.Count > 0 && s.Zuletzt.Peek().T < t - 2) s.Zuletzt.Dequeue();
                liste.Add(new JsonObject
                {
                    ["id"] = s.Id, ["kopplung"] = s.Kopplung.Id, ["brille"] = s.Kopplung.Brille, ["name"] = s.Kopplung.Name, ["seit"] = s.Seit.ToString("o"),
                    ["gestartet"] = s.Start.Task.IsCompleted, ["pausiert"] = s.Pausiert, ["monitor"] = s.Monitor, ["monitorName"] = s.MonitorName,
                    ["breite"] = s.Breite, ["hoehe"] = s.Hoehe, ["bildrate"] = s.Bildrate, ["ruheBildrate"] = s.RuheBildrate, ["ruhig"] = s.Ruhig,
                    ["erfassungen"] = Interlocked.Read(ref s.Erfassungen),
                    ["bilderJeSekunde"] = Math.Round(s.Zuletzt.Count / 2.0, 1), ["kbitJeSekunde"] = Math.Round(s.Zuletzt.Sum(x => x.Bytes) * 8 / 2.0 / 1000, 1),
                    ["bilder"] = s.Bilder, ["bytes"] = s.Bytes, ["kacheln"] = s.Kacheln, ["erfassMs"] = Math.Round(s.ErfassMs, 2), ["kodierMs"] = Math.Round(s.KodierMs, 2),
                });
            }
            return new JsonObject
            {
                ["erfassung"] = Quelle.Art,
                ["bildschirme"] = new JsonArray([.. monitore.Select(m => (JsonNode)new JsonObject
                {
                    ["id"] = m.Id, ["name"] = m.Name, ["breite"] = m.Breite, ["hoehe"] = m.Hoehe, ["haupt"] = m.Haupt, ["dpi"] = m.Dpi,
                })]),
                ["verbunden"] = stroeme.Count,
                ["aktiv"] = stroeme.Count(s => s.Start.Task.IsCompleted && !s.Pausiert),
                ["stroeme"] = liste,
            };
        }
    }
}

public static class XrDesktopEndpunkte
{
    /// <summary>Dienste (aus AddXrZugang): Einstellungen und Quelle nur, wenn kein Test eine eigene registriert hat.</summary>
    public static IServiceCollection AddXrDesktop(this IServiceCollection s)
    {
        s.TryAddSingleton(new XrDesktopEinstellungen());
        s.TryAddSingleton<IBildschirmQuelle>(_ => XrDesktopQuellen.Vorgabe());
        s.TryAddSingleton<XrDesktopDienst>();
        return s;
    }

    /// <summary>Endpunkte (aus MapXr).</summary>
    public static WebApplication MapXrDesktop(this WebApplication app)
    {
        var dienst = app.Services.GetRequiredService<XrDesktopDienst>();
        var kopplungen = app.Services.GetRequiredService<XrKopplungen>();
        var g = app.MapGroup("/api/xr");
        g.MapGet("/desktop", async (HttpContext ctx) =>
        {
            var k = XrPforte.Anmeldung(ctx, kopplungen);
            if (k is null)
            {
                ctx.Response.Headers.WWWAuthenticate = "Bearer";
                await Antwort(new { fehler = "Schlüssel fehlt oder gilt nicht.", vorschlag = "Die Brille mit KICAD koppeln (Kopplungspaket oder Code am PC)." }, 401).ExecuteAsync(ctx);
                return;
            }
            if (!ctx.WebSockets.IsWebSocketRequest)
            {
                await Antwort(new { fehler = "Hier gibt es nur den Bildschirm des PCs (WebSocket) über den XR-Zugang.", vorschlag = "wss://<PC>:<Port>/api/xr/desktop mit dem Schlüssel öffnen." }, 400).ExecuteAsync(ctx);
                return;
            }
            await dienst.Bedienen(ctx, k);
        }).XrImNetz();
        g.MapGet("/desktop/zustand", () => Antwort(dienst.Zustand()));
        g.MapPost("/desktop/beenden", () => Antwort(new JsonObject { ["beendet"] = dienst.Beenden(), ["zustand"] = dienst.Zustand() }));
        return app;
    }
}
