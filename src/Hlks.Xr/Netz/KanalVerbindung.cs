// Quest App P4a (Modul XR): der Live-Kanal der App zu KICAD (wss://<PC>:5140/api/xr/kanal; Server src/Hlks.Web/XrKanal.cs, Schema XrNachrichten.cs,
// OBERFLAECHE.md «Modul XR: Zugang im Büronetz»). Ohne StereoKit; Ereignisse kommen auf einem Hintergrund-Thread (die App reicht sie an ihr Bild weiter).
//
// Ablauf: verbinden (TLS mit Pinning, Bearer), «hallo» senden, «willkommen» abwarten (eigene Id, Auftrag, Stellschrauben). Danach:
//   - Posen: die App setzt in jedem Bild die neueste Pose (PoseSetzen); gesendet wird höchstens PosenJeSekunde (Vorgabe 25, im Bereich 20–30 des
//     Auftrags; der Server reicht höchstens 30 je Sekunde weiter) und nur, wenn seit dem letzten Senden eine neue Pose kam.
//   - Empfang: auftrag, stellschraube, fehler, quittung (und Posen anderer Teilnehmer, für später).
//   - Wiederverbinden mit wachsender Wartezeit (KanalRegeln.Wartezeit), vor jedem Versuch die Adresse neu bestimmen (der PC kann eine neue IP haben).
//     NICHT neu verbinden nach Code 4001 (Kopplung widerrufen) und 4002 (durch eine neue Verbindung derselben Brille ersetzt) und nach HTTP 401 beim
//     Verbinden (Schlüssel gilt nicht mehr; der PC sperrt die Adresse nach 10 Fehlversuchen).
// Masse im Kanal: KICAD-Koordinaten (mm, z oben, Quaternion [x, y, z, w]); die Umrechnung macht KicadUmrechnung.

using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;

namespace Hlks.Xr.Netz;

/// <summary>Lage in KICAD: Position [x, y, z] in mm, Drehung als Quaternion [x, y, z, w].</summary>
public sealed record KicadLage(double[] Pos, double[] Rot);

/// <summary>Eine Hand in KICAD: Handgelenk, Spitze des Zeigefingers (mm), Pinch und seine Stärke 0…1.</summary>
public sealed record KicadHand(KicadLage Gelenk, double[] Zeigefinger, bool Pinch, double? PinchStaerke);

/// <summary>Pose für den Kanal: Kopf, Hände (fehlend = nicht erkannt), Modus («begehen», «sitzungsraum»), Massstab (1 = 1:1).</summary>
public sealed record KicadPose(KicadLage Kopf, KicadHand? Links, KicadHand? Rechts, string? Modus = "begehen", double? Massstab = 1);

/// <summary>Zustand des Kanals für die Anzeige.</summary>
public enum KanalZustand { Aus, Verbindet, Verbunden, Wartet, Widerrufen, Ersetzt, NichtGekoppelt, Beendet }

/// <summary>Regeln des Kanals (rein, getestet).</summary>
public static class KanalRegeln
{
    /// <summary>Schliess-Code des Servers: Kopplung widerrufen (XrKanal.Widerrufen).</summary>
    public const int Widerrufen = 4001;
    /// <summary>Schliess-Code des Servers: durch eine neue Verbindung derselben Brille ersetzt (XrKanal.Ersetzt).</summary>
    public const int Ersetzt = 4002;
    /// <summary>Schliess-Code des Servers: Teilnehmer zu langsam (XrKanal.ZuLangsam) – neu verbinden.</summary>
    public const int ZuLangsam = 4003;

    /// <summary>Wartezeiten vor dem nächsten Versuch: 1, 2, 4, 8, 15, dann 30 s (Annahme P4a; Rücksicht auf das Büronetz und die Bremse des Servers).</summary>
    static readonly double[] Stufen = [1, 2, 4, 8, 15, 30];

    /// <summary>Wartezeit vor Versuch Nummer <paramref name="versuch"/> (0 = erster Versuch nach einer Trennung).</summary>
    public static TimeSpan Wartezeit(int versuch) => TimeSpan.FromSeconds(Stufen[Math.Clamp(versuch, 0, Stufen.Length - 1)]);

    /// <summary>Nach diesem Schluss neu verbinden? Nein nach 4001 (widerrufen) und 4002 (ersetzt), sonst ja (auch ohne Code: Netz weg).</summary>
    public static bool NeuVerbinden(int? schlussCode) => schlussCode is not (Widerrufen or Ersetzt);

    /// <summary>Zustand nach einem Schluss ohne Neuverbinden.</summary>
    public static KanalZustand ZustandNach(int? schlussCode) => schlussCode switch
    {
        Widerrufen => KanalZustand.Widerrufen,
        Ersetzt => KanalZustand.Ersetzt,
        _ => KanalZustand.Wartet,
    };
}

/// <summary>Takt der Posen: höchstens <c>jeSekunde</c> je Sekunde (Zeit in Sekunden, monoton).</summary>
public sealed class PosenTakt(double jeSekunde = PosenTakt.Vorgabe)
{
    /// <summary>Annahme P4a: 25 Posen je Sekunde (Auftrag 20–30; der Server reicht höchstens 30 weiter).</summary>
    public const double Vorgabe = 25;
    readonly double abstand = 1 / Math.Clamp(jeSekunde, 1, 30);
    double letzte = double.NegativeInfinity;

    /// <summary>Ist zur Zeit t eine Pose fällig? Wenn ja, zählt sie als gesendet.</summary>
    public bool Faellig(double t)
    {
        if (t - letzte < abstand - 1e-9) return false;
        // im Takt bleiben, ohne nach einer Pause aufzuholen
        letzte = t - letzte < 2 * abstand ? letzte + abstand : t;
        return true;
    }
}

/// <summary>Nachrichten des Kanals bauen und lesen (Format XrNachrichten.cs am PC).</summary>
public static class KanalText
{
    public const int Fassung = 1;

    static string Kurz(string s, int max)
    {
        var t = new string((s ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();
        return t.Length == 0 ? "?" : t.Length <= max ? t : t[..max];
    }

    static JsonObject Kopf(string typ) => new() { ["typ"] = typ, ["fassung"] = Fassung };

    /// <summary>hallo { name, geraet, app } – Name und Gerät höchstens 40 Zeichen, ohne Steuerzeichen.</summary>
    public static string Hallo(string name, string geraet, string? app)
    {
        var o = Kopf("hallo");
        o["name"] = Kurz(name, 40);
        o["geraet"] = Kurz(geraet, 40);
        if (!string.IsNullOrWhiteSpace(app)) o["app"] = Kurz(app, 40);
        return o.ToJsonString();
    }

    public static string Tschuess() => Kopf("tschuess").ToJsonString();

    static JsonArray Feld(double[] w, int stellen) =>
        [.. w.Select(x => (JsonNode)JsonValue.Create(Math.Round(double.IsFinite(x) ? x : 0, stellen)))];

    static JsonObject Lage(KicadLage l) => new() { ["pos"] = Feld(l.Pos, 1), ["rot"] = Feld(l.Rot, 5) };

    static JsonObject Hand(KicadHand h)
    {
        var o = new JsonObject { ["gelenk"] = Lage(h.Gelenk), ["zeigefinger"] = Feld(h.Zeigefinger, 1), ["pinch"] = h.Pinch };
        if (h.PinchStaerke is { } s && double.IsFinite(s)) o["pinchStaerke"] = Math.Round(Math.Clamp(s, 0, 1), 3);
        return o;
    }

    /// <summary>pose { kopf, links?, rechts?, modus?, massstab? } in KICAD-mm (auf 0.1 mm gerundet, Drehung auf 5 Stellen).</summary>
    public static string Pose(KicadPose p)
    {
        var o = Kopf("pose");
        o["kopf"] = Lage(p.Kopf);
        if (p.Links is not null) o["links"] = Hand(p.Links);
        if (p.Rechts is not null) o["rechts"] = Hand(p.Rechts);
        if (p.Modus is not null) o["modus"] = p.Modus;
        if (p.Massstab is { } m && double.IsFinite(m) && m > 0) o["massstab"] = m;
        return o.ToJsonString();
    }

    /// <summary>zeigen { element, punkt? } (für «Claude sieht mit»; in P4a noch nicht benutzt).</summary>
    public static string Zeigen(string? element, double[]? punktMm)
    {
        var o = Kopf("zeigen");
        o["element"] = element;
        if (punktMm is not null) o["punkt"] = Feld(punktMm, 1);
        return o.ToJsonString();
    }

    /// <summary>
    /// P5b: messprotokoll { aufgabe, zeitMs, fehlgriffe, abbrueche, erfolg?, varianten?, stellschrauben?, notiz? } – Inhalt aus
    /// Hlks.Xr.Labor.PruefAufgaben.Messprotokoll (Schema XrNachrichten.Messprotokoll am PC).
    /// </summary>
    public static string Messprotokoll(JsonObject inhalt)
    {
        var o = Kopf("messprotokoll");
        foreach (var (k, w) in inhalt) if (k is not ("typ" or "fassung")) o[k] = w?.DeepClone();
        return o.ToJsonString();
    }

    /// <summary>Liest Typ und Inhalt einer Nachricht des PCs; null bei ungültigem JSON, fehlendem Typ oder anderer Fassung.</summary>
    public static (string Typ, JsonElement Inhalt)? Lesen(string text)
    {
        try
        {
            using var d = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 32 });
            var r = d.RootElement;
            if (r.ValueKind != JsonValueKind.Object || !r.TryGetProperty("typ", out var t) || t.ValueKind != JsonValueKind.String) return null;
            if (!r.TryGetProperty("fassung", out var f) || f.ValueKind != JsonValueKind.Number || !f.TryGetInt32(out var fa) || fa != Fassung) return null;
            return (t.GetString()!, r.Clone());
        }
        catch (JsonException) { return null; }
    }
}

/// <summary>Einstellungen des Kanals.</summary>
public sealed record KanalOptionen
{
    public string Name { get; init; } = "Quest";
    public string Geraet { get; init; } = "Meta Quest";
    public string? App { get; init; }
    public double PosenJeSekunde { get; init; } = PosenTakt.Vorgabe;
    /// <summary>Frist für Verbinden und «willkommen».</summary>
    public TimeSpan Frist { get; init; } = TimeSpan.FromSeconds(10);
    /// <summary>Wartezeit je Versuch (Tests verkürzen sie); Vorgabe <see cref="KanalRegeln.Wartezeit"/>.</summary>
    public Func<int, TimeSpan>? Wartezeit { get; init; }
}

/// <summary>Der Live-Kanal mit Wiederverbinden (siehe Dateikopf).</summary>
public sealed class KanalVerbindung : IAsyncDisposable
{
    readonly Kopplungspaket paket;
    readonly Func<CancellationToken, Task<Uri?>> adresse;
    readonly KanalOptionen optionen;
    readonly CancellationTokenSource stopp = new();
    readonly Channel<string> ausgang = Channel.CreateBounded<string>(new BoundedChannelOptions(64) { FullMode = BoundedChannelFullMode.DropOldest });
    string? pose;
    long poseNr, poseGesendetNr;
    Task? schleife;
    ClientWebSocket? aktuell;
    volatile KanalZustand zustand = KanalZustand.Aus;

    /// <param name="adresse">Liefert vor jedem Versuch die Adresse des Kanals (wss://…/api/xr/kanal) oder null (PC nicht gefunden: warten).</param>
    public KanalVerbindung(Kopplungspaket paket, Func<CancellationToken, Task<Uri?>> adresse, KanalOptionen? optionen = null)
    {
        this.paket = paket;
        this.adresse = adresse;
        this.optionen = optionen ?? new KanalOptionen();
    }

    public KanalZustand Zustand => zustand;
    /// <summary>Teilnehmer-Id aus «willkommen» (null, solange nicht verbunden).</summary>
    public string? TeilnehmerId { get; private set; }
    /// <summary>Code des letzten Schlusses durch den Server (null: Netz weg bzw. noch keiner).</summary>
    public int? LetzterSchluss { get; private set; }
    public string? LetzteMeldung { get; private set; }
    public long PosenGesendet => Interlocked.Read(ref gesendet);
    long gesendet;
    /// <summary>Wie oft «willkommen» kam (erste Verbindung = 1).</summary>
    public int Verbindungen { get; private set; }
    /// <summary>Nummer des laufenden Versuchs nach einer Trennung (0 = verbunden).</summary>
    public int Versuch { get; private set; }

    /// <summary>«willkommen» { id, teilnehmer[], auftrag?, stellschrauben{} }.</summary>
    public event Action<JsonElement>? Willkommen;
    /// <summary>«auftrag» { auftrag } – das Objekt des Auftrags (VrStartLogik.AuftragPruefen(JsonElement) prüft es).</summary>
    public event Action<JsonElement>? Auftrag;
    /// <summary>«stellschraube» (Schlüssel, Wert; Wert Null = zurückgesetzt).</summary>
    public event Action<string, JsonElement>? Stellschraube;
    /// <summary>«fehler» { meldung, bezug? }.</summary>
    public event Action<string>? Fehler;
    /// <summary>Jede andere Nachricht (Typ, Inhalt), z. B. Posen anderer Teilnehmer.</summary>
    public event Action<string, JsonElement>? Nachricht;
    public event Action<KanalZustand>? ZustandGeaendert;

    void Setzen(KanalZustand z)
    {
        if (zustand == z) return;
        zustand = z;
        try { ZustandGeaendert?.Invoke(z); } catch (Exception ex) when (ex is InvalidOperationException or ArgumentException) { }
    }

    /// <summary>Startet die Schleife (verbinden, empfangen, wiederverbinden) im Hintergrund.</summary>
    public void Starten() => schleife ??= Task.Run(() => Schleife(stopp.Token));

    /// <summary>Neueste Pose; gesendet im Takt (siehe Dateikopf).</summary>
    public void PoseSetzen(KicadPose p)
    {
        Volatile.Write(ref pose, KanalText.Pose(p));
        Interlocked.Increment(ref poseNr);
    }

    /// <summary>Eine andere Nachricht senden (wird verworfen, wenn gerade keine Verbindung besteht).</summary>
    public bool Senden(string text) => zustand == KanalZustand.Verbunden && ausgang.Writer.TryWrite(text);

    async Task Schleife(CancellationToken ende)
    {
        int versuch = 0;
        while (!ende.IsCancellationRequested)
        {
            Versuch = versuch;
            Setzen(KanalZustand.Verbindet);
            int? schluss = null;
            bool abgewiesen = false, warVerbunden = false;
            Uri? ziel = null;
            try { ziel = await adresse(ende).ConfigureAwait(false); }
            catch (OperationCanceledException) when (ende.IsCancellationRequested) { break; }
            catch (Exception ex) when (ex is not OperationCanceledException) { LetzteMeldung = ex.Message; }
            if (ziel is not null)
            {
                try { (schluss, abgewiesen, warVerbunden) = await EineVerbindung(ziel, ende).ConfigureAwait(false); }
                catch (OperationCanceledException) when (ende.IsCancellationRequested) { break; }
                if (warVerbunden) versuch = 0;   // nach einer gelungenen Verbindung beginnt das Warten wieder kurz
            }
            LetzterSchluss = schluss;
            if (abgewiesen) { Setzen(KanalZustand.NichtGekoppelt); return; }
            if (!KanalRegeln.NeuVerbinden(schluss)) { Setzen(KanalRegeln.ZustandNach(schluss)); return; }
            Setzen(KanalZustand.Wartet);
            try { await Task.Delay((optionen.Wartezeit ?? KanalRegeln.Wartezeit)(versuch), ende).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            versuch++;
        }
        Setzen(KanalZustand.Beendet);
    }

    /// <summary>Eine Verbindung bis zu ihrem Ende. Rückgabe: Schliess-Code des Servers (null = ohne), ob der Server den Schlüssel abwies (401) und ob
    /// «willkommen» kam.</summary>
    async Task<(int? Schluss, bool Abgewiesen, bool WarVerbunden)> EineVerbindung(Uri ziel, CancellationToken ende)
    {
        bool war = false;
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
        using var verbindung = CancellationTokenSource.CreateLinkedTokenSource(ende);
        try
        {
            await SendenRoh(ws, KanalText.Hallo(optionen.Name, optionen.Geraet, optionen.App), verbindung.Token).ConfigureAwait(false);
            using (var frist = CancellationTokenSource.CreateLinkedTokenSource(verbindung.Token))
            {
                frist.CancelAfter(optionen.Frist);
                var erste = await Empfangen(ws, frist.Token).ConfigureAwait(false);
                if (erste is null) return (Code(ws), false, false);
                if (KanalText.Lesen(erste) is not { Typ: "willkommen" } w)
                {
                    LetzteMeldung = "Der PC hat «hallo» nicht mit «willkommen» beantwortet.";
                    Verteilen(erste);
                    return (Code(ws), false, false);
                }
                TeilnehmerId = w.Inhalt.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString() : null;
                Verbindungen++;
                war = true;
                Versuch = 0;
                Setzen(KanalZustand.Verbunden);
                Ausloesen(() => Willkommen?.Invoke(w.Inhalt));
            }
            var senden = Senden(ws, verbindung.Token);
            try
            {
                while (true)
                {
                    var text = await Empfangen(ws, verbindung.Token).ConfigureAwait(false);
                    if (text is null) break;
                    Verteilen(text);
                }
            }
            finally
            {
                verbindung.Cancel();
                try { await senden.ConfigureAwait(false); } catch (Exception ex) when (ex is OperationCanceledException or WebSocketException or ObjectDisposedException) { }
            }
            return (Code(ws), false, war);
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or IOException or ObjectDisposedException)
        {
            if (ende.IsCancellationRequested) throw new OperationCanceledException(ende);
            LetzteMeldung = ex.Message;
            return (Code(ws), false, war);
        }
        finally
        {
            aktuell = null;
            TeilnehmerId = null;
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

    static int? Code(ClientWebSocket ws) => ws.CloseStatus is { } s ? (int)s : null;

    void Verteilen(string text)
    {
        if (KanalText.Lesen(text) is not { } n) return;
        var (typ, o) = n;
        switch (typ)
        {
            case "auftrag" when o.TryGetProperty("auftrag", out var a):
                Ausloesen(() => Auftrag?.Invoke(a));
                break;
            case "stellschraube" when o.TryGetProperty("schluessel", out var s) && s.ValueKind == JsonValueKind.String:
                var wert = o.TryGetProperty("wert", out var w) ? w : default;
                Ausloesen(() => Stellschraube?.Invoke(s.GetString()!, wert));
                break;
            case "fehler":
                var m = o.TryGetProperty("meldung", out var mm) && mm.ValueKind == JsonValueKind.String ? mm.GetString()! : "Fehler ohne Meldung";
                LetzteMeldung = m;
                Ausloesen(() => Fehler?.Invoke(m));
                break;
            default:
                Ausloesen(() => Nachricht?.Invoke(typ, o));
                break;
        }
    }

    static void Ausloesen(Action a)
    {
        try { a(); }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or KeyNotFoundException or FormatException) { /* Fehler der App nicht in den Kanal tragen */ }
    }

    async Task Senden(ClientWebSocket ws, CancellationToken abbruch)
    {
        var takt = new PosenTakt(optionen.PosenJeSekunde);
        var uhr = System.Diagnostics.Stopwatch.StartNew();
        var abstand = TimeSpan.FromSeconds(1 / Math.Clamp(optionen.PosenJeSekunde, 1, 30) / 2);
        while (!abbruch.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            while (ausgang.Reader.TryRead(out var text)) await SendenRoh(ws, text, abbruch).ConfigureAwait(false);
            var nr = Interlocked.Read(ref poseNr);
            if (nr != poseGesendetNr && Volatile.Read(ref pose) is { } p && takt.Faellig(uhr.Elapsed.TotalSeconds))
            {
                await SendenRoh(ws, p, abbruch).ConfigureAwait(false);
                poseGesendetNr = nr;
                Interlocked.Increment(ref gesendet);
            }
            await Task.Delay(abstand, abbruch).ConfigureAwait(false);
        }
    }

    readonly SemaphoreSlim sendeSperre = new(1, 1);

    async Task SendenRoh(ClientWebSocket ws, string text, CancellationToken abbruch)
    {
        await sendeSperre.WaitAsync(abbruch).ConfigureAwait(false);
        try { await ws.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, abbruch).ConfigureAwait(false); }
        finally { sendeSperre.Release(); }
    }

    static async Task<string?> Empfangen(ClientWebSocket ws, CancellationToken abbruch)
    {
        var puffer = new byte[16 * 1024];
        using var ms = new MemoryStream();
        while (true)
        {
            var r = await ws.ReceiveAsync(puffer.AsMemory(), abbruch).ConfigureAwait(false);
            if (r.MessageType == WebSocketMessageType.Close) return null;
            if (ms.Length + r.Count > 1024 * 1024) throw new WebSocketException("Nachricht des PCs zu gross.");
            ms.Write(puffer, 0, r.Count);
            if (r.EndOfMessage) break;
        }
        return Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
    }

    /// <summary>Beendet den Kanal: «tschuess» senden (wenn verbunden), schliessen, nicht neu verbinden.</summary>
    public async Task Beenden()
    {
        if (Interlocked.Exchange(ref beendet, 1) == 1) return;
        if (aktuell is { State: WebSocketState.Open } ws)
        {
            try
            {
                using var f = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await SendenRoh(ws, KanalText.Tschuess(), f.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException) { }
        }
        stopp.Cancel();
        if (schleife is not null)
        {
            try { await schleife.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false); }
            catch (Exception ex) when (ex is TimeoutException or OperationCanceledException) { }
        }
        Setzen(KanalZustand.Beendet);
    }

    int beendet;

    /// <summary>Wie <see cref="Beenden"/>; die Mittel bleiben bis zum Ende des Prozesses (ein Hintergrund-Sender könnte sie noch benutzen).</summary>
    public async ValueTask DisposeAsync() => await Beenden().ConfigureAwait(false);
}
