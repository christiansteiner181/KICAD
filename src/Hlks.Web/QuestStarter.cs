// «Begehung beim Aufsetzen automatisch starten» (Modul XR, Christian Steiner 21.10.2026: «ich möchte nach dem klick am pc nur noch die brille anziehen, und dann
// startet die begehung direkt.»). Nach dem Klick am PC (VR-Auftrag mit autostart: true) öffnet KICAD die VR-Startseite /vr im Browser der Meta Quest, wartet, bis die
// Brille getragen wird, und klickt dann im Browser der Brille (Fern-Debugging, CDP) den Knopf «Eintreten» – so startet die Sitzung ohne Handgriff in der Brille.
//
// Ablauf eines Autostarts (ein Lauf zur Zeit; ein neuer Auftrag bricht den alten ab):
//   1. Brille verbinden wie die Automatik (QuestDienst.BrilleFuerStart: Weg wählen, Weiterleitung für den KICAD-Port sicherstellen).
//   2. adb shell am start -a android.intent.action.VIEW -d http://localhost:<Port>/vr com.oculus.browser
//   3. Warten (höchstens QuestEinstellungen.AutostartGrenze, 10 min), bis die Brille getragen wird: «dumpsys power» → mWakefulness=Awake UND «dumpsys activity activities»
//      → der fortgesetzte Vordergrund-Eintrag (mResumedActivity / topResumedActivity) gehört zu com.oculus.browser. Im Ruhezustand reisst adb über WLAN unter Umständen ab;
//      dann wird die Verbindung wie in Schritt 1 neu aufgebaut.
//   4. adb forward tcp:<lokaler Port> localabstract:chrome_devtools_remote; CDP-WebSocket ws://127.0.0.1:<lokaler Port>/devtools/browser. Warten, bis #vr-eintreten
//      den Status «bereit» meldet; dann EIN Klick (Runtime.evaluate mit userGesture: true). Ist der Status danach nicht «aktiv», ein zweiter Versuch mit einem Mausklick
//      (Input.dispatchMouseEvent) in der Mitte des Knopfs; steht der Status auf «startet» (die Brille fragt «VR erlauben»), wird stattdessen auf den Menschen gewartet.
//      Steht der Status schon auf «aktiv», wird nicht geklickt.
//   5. Am Ende (auch bei Fehler, Zeitgrenze und Abbruch) wird «adb forward --remove» ausgeführt.
// Beim ersten Mal fragt der Browser der Brille, ob die Seite VR nutzen darf – das muss der Mensch einmal bestätigen.
//
// SICHERHEIT (dieser Dienst steuert den Browser der Brille):
// - Ausgewertet werden NUR die drei festen Ausdrücke in QuestStarter.Ausdruecke (Status lesen, Knopf klicken, Mitte des Knopfs lesen); nie Text aus einer Anfrage, nie ein
//   Wert aus der Brille. Gesendet werden nur die fünf Methoden in ErlaubteMethoden; jede andere wirft, bevor etwas das Programm verlässt.
// - Angefasst wird nur ein Tab, dessen URL genau mit http://localhost:<Port>/vr beginnt (danach nichts oder ? # /). Gibt es keinen, wird einmal Page.navigate auf genau
//   diese Adresse im ersten Tab gesendet. Andere Tabs werden nie angehängt. Der Port ist der des Servers (Host-Kopf der geprüften Anfrage), nicht aus dem Körper.
// - Fern-Debugging nur für die Dauer des Autostarts: lokaler Port nur auf 127.0.0.1 (von KICAD gewählt), am Ende wieder entfernt. Jede CDP-Anfrage und jeder adb-Aufruf hat eine Zeitgrenze.
// - Nur auf eine Handlung des Menschen (POST /api/vr/auftrag mit autostart: true aus Kontextmenü bzw. Knopf), nie aus dem Hintergrund-Takt; ohne die Einstellung
//   quest.autostart (Vorgabe an) geschieht nichts.
// - Für die KI nicht freigegeben: KiEinordnung.RechnerFunktionen («quest_autostart»), kein MCP-Werkzeug. Wie beim Quest-Verbinder startet nur adb.exe mit festen Argumenten.
// - CDP über System.Net.WebSockets.ClientWebSocket, ohne neue Pakete. Antworten werden auf 1 MB begrenzt und streng gelesen (Status nur aus vier Werten, Koordinaten nur Zahlen).

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Hlks.Kern;

namespace Hlks.Web;

/// <summary>Zustand des Autostarts für die Statusanzeige am PC. Status: leer | laeuft | aktiv | fehler | abgebrochen. Phase: verbinden | browser | aufsetzen | debugging | klick.</summary>
public sealed record AutostartStand(string Status, string? Phase, string? Meldung, string? Hinweis, long Nr, int Port, string? Seit, string? Ende);

public sealed class QuestStarter(QuestDienst quest, QuestEinstellungen einst)
{
    public QuestDienst Quest => quest;

    /// <summary>Die einzigen Ausdrücke, die im Browser der Brille ausgewertet werden.</summary>
    public static class Ausdruecke
    {
        public const string Lesen = "document.querySelector('#vr-eintreten')?.dataset.status ?? ''";
        public const string Klick = "document.querySelector('#vr-eintreten')?.click()";
        public const string Mitte = "(() => { const b = document.querySelector('#vr-eintreten'); if (!b) return null; const r = b.getBoundingClientRect(); return { x: r.left + r.width / 2, y: r.top + r.height / 2 }; })()";
    }

    enum Ausdruck { Lesen, Klick, Mitte }
    static string Text(Ausdruck a) => a switch { Ausdruck.Lesen => Ausdruecke.Lesen, Ausdruck.Klick => Ausdruecke.Klick, _ => Ausdruecke.Mitte };

    static readonly string[] ErlaubteMethoden = ["Target.getTargets", "Target.attachToTarget", "Page.navigate", "Runtime.evaluate", "Input.dispatchMouseEvent"];
    static readonly string[] Stati = ["laedt", "bereit", "startet", "aktiv"];
    const string Browser = "com.oculus.browser";

    static readonly Regex WachMuster = new(@"^\s*mWakefulness=(?<w>[A-Za-z]+)\s*$", RegexOptions.CultureInvariant | RegexOptions.Multiline);
    static readonly Regex VorneMuster = new(@"^\s*(?:mResumedActivity|topResumedActivity|ResumedActivity)\s*[:=]\s*ActivityRecord\{[0-9a-fA-F]+ u\d+ (?<pkg>[A-Za-z0-9_.]+)/",
        RegexOptions.CultureInvariant | RegexOptions.Multiline);

    // ---------------------------------------------------------------- Zustand

    readonly object schloss = new();
    CancellationTokenSource? aktuell;
    Task lauf = Task.CompletedTask;
    AutostartStand stand = new("leer", null, null, null, 0, 0, null, null);

    public AutostartStand Zustand() { lock (schloss) return stand; }

    string Zeit() => (einst.Uhr ?? (() => DateTimeOffset.UtcNow))().UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture);

    void Melden(CancellationTokenSource ich, string status, string? phase, string? meldung, string? hinweis = null)
    {
        lock (schloss)
        {
            if (!ReferenceEquals(aktuell, ich)) return;   // ein neuer Auftrag hat übernommen
            var beendet = status != "laeuft";
            stand = stand with { Status = status, Phase = phase, Meldung = meldung, Hinweis = hinweis, Ende = beendet ? Zeit() : null };
        }
    }

    // ---------------------------------------------------------------- Start

    /// <summary>Startet den Autostart im Hintergrund (nur auf eine Handlung des Menschen). Gibt false zurück, wenn die Einstellung quest.autostart aus ist.
    /// Ein laufender Autostart wird abgebrochen; der neue beginnt erst, wenn der alte aufgeräumt hat (Weiterleitung entfernt).</summary>
    public bool Starten(int port, long nr)
    {
        if (!quest.AutostartAn()) return false;
        if (port is < 1 or > 65535) throw new HlksFehler($"Der Port {port} ist ungültig.", "KICAD läuft auf einem Port von 1 bis 65535.");
        var ich = new CancellationTokenSource();
        CancellationTokenSource? alt;
        lock (schloss)
        {
            alt = aktuell;
            aktuell = ich;
            var vorher = lauf;
            stand = new AutostartStand("laeuft", "verbinden", "Die Brille wird verbunden …", null, nr, port, Zeit(), null);
            lauf = Task.Run(() => Ablauf(port, ich, vorher));
        }
        try { alt?.Cancel(); } catch (ObjectDisposedException) { /* schon fertig */ }
        return true;
    }

    /// <summary>Wartet auf das Ende des laufenden Autostarts (Tests).</summary>
    public Task Fertig() { lock (schloss) return lauf; }

    sealed class CdpFehler(string text) : Exception(text);

    async Task Ablauf(int port, CancellationTokenSource ich, Task vorher)
    {
        try { await vorher; } catch (Exception) { /* der alte Lauf meldet sich selbst */ }
        var abbruch = ich.Token;
        QuestDienst.AdbFund? adb = null;
        string serie = "";
        int? cdpPort = null;
        Action? schluss = null;
        try
        {
            abbruch.ThrowIfCancellationRequested();
            (adb, serie) = await quest.BrilleFuerStart(port, abbruch);

            Melden(ich, "laeuft", "browser", "Der Browser der Brille öffnet die VR-Startseite …");
            var start = await quest.Aufruf(adb, "-s", serie, "shell", "am", "start", "-a", "android.intent.action.VIEW", "-d", $"http://localhost:{port}/vr", Browser);
            if (start.Zeitueberschreitung) throw new HlksFehler("adb hat beim Öffnen des Browsers nicht geantwortet.", "Brille aufsetzen und erneut senden.");
            if (start.Code != 0 || Regex.IsMatch(start.Ausgabe, @"\bError\b")) throw new HlksFehler($"Der Browser der Brille liess sich nicht öffnen: {Kuerzen(start.Ausgabe, 200)}", "Prüfen, ob der Browser (com.oculus.browser) in der Brille vorhanden ist.");

            Melden(ich, "laeuft", "aufsetzen", "Brille aufsetzen – die Begehung startet automatisch.");
            serie = await AufsetzenWarten(adb, serie, port, abbruch);

            Melden(ich, "laeuft", "debugging", "Die Brille ist aufgesetzt; die Begehung wird gestartet …");
            cdpPort = einst.CdpPortWahl?.Invoke() ?? FreierPort();
            var fw = await quest.Aufruf(adb, "-s", serie, "forward", $"tcp:{cdpPort}", "localabstract:chrome_devtools_remote");
            if (fw.Zeitueberschreitung || fw.Code != 0) { cdpPort = null; throw new HlksFehler($"Das Fern-Debugging der Brille liess sich nicht öffnen: {Kuerzen(fw.Ausgabe, 200)}", "Brille aufsetzen und erneut senden."); }

            await BrowserSteuern(cdpPort.Value, port, ich, abbruch);
            schluss = () => Melden(ich, "aktiv", null, "Die Begehung läuft.");
        }
        catch (OperationCanceledException) { schluss = () => Melden(ich, "abgebrochen", null, "Der Autostart wurde durch einen neuen Auftrag abgebrochen."); }
        catch (HlksFehler f) { schluss = () => Melden(ich, "fehler", null, f.Message, f.Vorschlag); }
        catch (Exception ex) when (ex is WebSocketException or IOException or CdpFehler or TimeoutException or System.Text.Json.JsonException or InvalidOperationException)
        { schluss = () => Melden(ich, "fehler", null, $"Der Browser der Brille antwortet nicht: {Kuerzen(ex.Message, 200)}", "Brille aufsetzen und erneut senden."); }
        catch (Exception ex)
        { schluss = () => Melden(ich, "fehler", null, $"Der Autostart ist fehlgeschlagen: {Kuerzen(ex.Message, 200)}", "Brille aufsetzen und erneut senden."); }
        // Aufräumen vor der Schlussmeldung: wer «aktiv» oder «fehler» liest, darf sich darauf verlassen, dass das Fern-Debugging wieder zu ist
        if (adb is not null && cdpPort is { } p)
        {
            try { await quest.Aufruf(adb, "-s", serie, "forward", "--remove", $"tcp:{p}"); }
            catch (HlksFehler) { /* adb weg: nichts mehr zu entfernen */ }
        }
        schluss?.Invoke();
    }

    static string Kuerzen(string s, int n) => s.Length <= n ? s : s[..n] + "…";

    static int FreierPort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        try { return ((IPEndPoint)l.LocalEndpoint).Port; }
        finally { l.Stop(); }
    }

    // ---------------------------------------------------------------- Brille aufgesetzt?

    /// <summary>Wach? Streng gelesen: nur die Zeile mWakefulness=Awake zählt.</summary>
    public static bool WachLesen(string? ausgabe)
    {
        if (string.IsNullOrEmpty(ausgabe)) return false;
        var m = WachMuster.Match(ausgabe);
        return m.Success && m.Groups["w"].Value == "Awake";
    }

    /// <summary>Gehört der fortgesetzte Vordergrund-Eintrag zum Quest-Browser? Nur Zeilen mResumedActivity / topResumedActivity zählen; sind mehrere da, müssen alle den Browser nennen.</summary>
    public static bool BrowserVorne(string? ausgabe)
    {
        if (string.IsNullOrEmpty(ausgabe)) return false;
        var treffer = VorneMuster.Matches(ausgabe);
        return treffer.Count > 0 && treffer.All(m => m.Groups["pkg"].Value == Browser);
    }

    async Task<string> AufsetzenWarten(QuestDienst.AdbFund adb, string serie, int port, CancellationToken abbruch)
    {
        var uhr = Stopwatch.StartNew();
        int ohneAntwort = 0;
        bool wach = false, vorne = false;
        while (true)
        {
            abbruch.ThrowIfCancellationRequested();
            var p = await quest.Aufruf(adb, "-s", serie, "shell", "dumpsys", "power");
            var antwortet = !p.Zeitueberschreitung && p.Code == 0;
            wach = vorne = false;
            if (antwortet)
            {
                wach = WachLesen(p.Ausgabe);
                if (wach)
                {
                    var a = await quest.AufrufGross(adb, "-s", serie, "shell", "dumpsys", "activity", "activities");
                    antwortet = !a.Zeitueberschreitung && a.Code == 0;
                    vorne = antwortet && BrowserVorne(a.Ausgabe);
                }
            }
            if (wach && vorne) return serie;
            ohneAntwort = antwortet ? 0 : ohneAntwort + 1;
            if (ohneAntwort > 0 && ohneAntwort % 5 == 0)   // adb über WLAN kann im Ruhezustand abreissen: wie die Automatik neu verbinden
            {
                try { (adb, serie) = await quest.BrilleFuerStart(port, abbruch); }
                catch (HlksFehler) { /* weiter warten */ }
            }
            if (uhr.Elapsed + einst.AutostartAbstand >= einst.AutostartGrenze)
                throw new HlksFehler($"Die Brille wurde innerhalb von {einst.AutostartGrenze.TotalMinutes:0.#} min nicht aufgesetzt ({(wach ? "wach" : "im Ruhezustand")}, Browser {(vorne ? "vorne" : "nicht vorne")}).",
                    "Brille aufsetzen und den Auftrag erneut senden (Rechtsklick auf den Raum › «In VR begehen (Brille)»).");
            await Task.Delay(einst.AutostartAbstand, abbruch);
        }
    }

    // ---------------------------------------------------------------- Fern-Debugging (CDP)

    static bool IstVrTab(string? url, int port)
    {
        var vr = $"http://localhost:{port}/vr";
        if (url is null || !url.StartsWith(vr, StringComparison.Ordinal)) return false;
        return url.Length == vr.Length || url[vr.Length] is '/' or '?' or '#';
    }

    async Task BrowserSteuern(int cdpPort, int port, CancellationTokenSource ich, CancellationToken abbruch)
    {
        var uhr = Stopwatch.StartNew();
        Cdp? cdp = null;
        string? ziel = null, sitzung = null;
        var navigiert = false;
        try
        {
            while (true)
            {
                abbruch.ThrowIfCancellationRequested();
                try
                {
                    cdp ??= await Cdp.Oeffnen(cdpPort, einst.CdpZeitlimit, abbruch);
                    var tabs = (await cdp.Senden("Target.getTargets", null, null, abbruch))["result"]?["targetInfos"] as JsonArray ?? [];
                    var seiten = tabs.OfType<JsonObject>().Where(t => t["type"]?.GetValue<string>() == "page" && t["targetId"]?.GetValue<string>() is { Length: > 0 }).ToList();
                    var vr = seiten.LastOrDefault(t => IstVrTab(t["url"]?.GetValue<string>(), port));   // der zuletzt geöffnete passende Tab
                    if (vr is null)
                    {
                        if (!navigiert && seiten.Count > 0)
                        {
                            navigiert = true;
                            var erste = seiten[0]["targetId"]!.GetValue<string>();
                            sitzung = await cdp.Anhaengen(erste, abbruch);
                            ziel = erste;
                            var par = new JsonObject { ["url"] = $"http://localhost:{port}/vr" };
                            await cdp.Senden("Page.navigate", par, sitzung, abbruch);
                        }
                    }
                    else
                    {
                        var id = vr["targetId"]!.GetValue<string>();
                        if (id != ziel || sitzung is null) { sitzung = await cdp.Anhaengen(id, abbruch); ziel = id; }
                        var status = await cdp.Status(sitzung, abbruch);
                        if (status == "aktiv") return;
                        if (status == "bereit") { Melden(ich, "laeuft", "klick", "Die Begehung wird gestartet …"); await Klicken(cdp, sitzung, abbruch); return; }
                    }
                }
                catch (TimeoutException) { /* Browser im Ruhezustand: weiter versuchen, bis die Gesamtgrenze erreicht ist */ }
                catch (CdpFehler) { sitzung = null; ziel = null; }
                catch (Exception ex) when (ex is WebSocketException or IOException) { cdp?.Dispose(); cdp = null; sitzung = null; ziel = null; }
                if (uhr.Elapsed + einst.AutostartAbstand >= einst.BereitGrenze)
                    throw new HlksFehler($"Die VR-Startseite wurde im Browser der Brille nicht bereit (nach {einst.BereitGrenze.TotalSeconds:0.#} s).",
                        "Brille aufsetzen und den Auftrag erneut senden; hilft das nicht, in der Brille http://localhost:<Port>/vr von Hand öffnen.");
                await Task.Delay(einst.AutostartAbstand, abbruch);
            }
        }
        finally { cdp?.Dispose(); }
    }

    async Task<string?> StatusBis(Cdp cdp, string sitzung, TimeSpan grenze, CancellationToken abbruch)
    {
        var uhr = Stopwatch.StartNew();
        string? status = null;
        while (true)
        {
            try { status = await cdp.Status(sitzung, abbruch); }
            catch (TimeoutException) { status = null; }
            if (status == "aktiv" || uhr.Elapsed + einst.AutostartAbstand >= grenze) return status;
            await Task.Delay(einst.AutostartAbstand, abbruch);
        }
    }

    async Task Klicken(Cdp cdp, string sitzung, CancellationToken abbruch)
    {
        await cdp.Auswerten(Ausdruck.Klick, sitzung, true, abbruch);
        var s = await StatusBis(cdp, sitzung, einst.KlickGrenze, abbruch);
        if (s != "aktiv")
        {
            if (s == "startet") s = await StatusBis(cdp, sitzung, einst.KlickGrenze, abbruch);   // die Brille fragt «VR erlauben»: auf den Menschen warten
            else
            {
                var mitte = await cdp.Auswerten(Ausdruck.Mitte, sitzung, false, abbruch);
                if (mitte is JsonObject o && Koordinate(o["x"]) is { } x && Koordinate(o["y"]) is { } y)
                {
                    foreach (var typ in new[] { "mousePressed", "mouseReleased" })
                        await cdp.Senden("Input.dispatchMouseEvent", new JsonObject { ["type"] = typ, ["x"] = x, ["y"] = y, ["button"] = "left", ["clickCount"] = 1 }, sitzung, abbruch);
                }
                s = await StatusBis(cdp, sitzung, einst.KlickGrenze, abbruch);
            }
        }
        if (s != "aktiv")
            throw new HlksFehler("Die Begehung wurde nicht gestartet: die Seite meldet nicht «aktiv».",
                "Fragt die Brille «VR erlauben», einmal bestätigen (danach startet es von selbst); sonst in der Brille den Knopf «Eintreten» tippen.");
    }

    static double? Koordinate(JsonNode? n) => n is JsonValue v && v.TryGetValue<double>(out var d) && double.IsFinite(d) && d is >= 0 and <= 20000 ? d : null;

    /// <summary>Eine Verbindung zum Browser-WebSocket (CDP). Jede Anfrage hat eine Zeitgrenze (TimeoutException); Antworten höchstens 1 MB.</summary>
    sealed class Cdp : IDisposable
    {
        const int MaxNachricht = 1024 * 1024;
        readonly ClientWebSocket ws;
        readonly TimeSpan zeit;
        readonly Dictionary<int, TaskCompletionSource<JsonNode>> offen = [];
        readonly object schloss = new();
        readonly CancellationTokenSource ende = new();
        int naechste;

        Cdp(ClientWebSocket ws, TimeSpan zeit) { this.ws = ws; this.zeit = zeit; _ = Lesen(); }

        public static async Task<Cdp> Oeffnen(int port, TimeSpan zeit, CancellationToken abbruch)
        {
            var ws = new ClientWebSocket();
            using var grenze = CancellationTokenSource.CreateLinkedTokenSource(abbruch);
            grenze.CancelAfter(zeit);
            try { await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/devtools/browser"), grenze.Token); }
            catch (OperationCanceledException) when (!abbruch.IsCancellationRequested) { ws.Dispose(); throw new TimeoutException("Keine Verbindung zum Browser der Brille."); }
            catch { ws.Dispose(); throw; }
            return new Cdp(ws, zeit);
        }

        async Task Lesen()
        {
            var puffer = new byte[16 * 1024];
            try
            {
                while (!ende.IsCancellationRequested && ws.State == WebSocketState.Open)
                {
                    using var ms = new MemoryStream();
                    WebSocketReceiveResult r;
                    do
                    {
                        r = await ws.ReceiveAsync(puffer, ende.Token);
                        if (r.MessageType == WebSocketMessageType.Close) return;
                        ms.Write(puffer, 0, r.Count);
                        if (ms.Length > MaxNachricht) return;
                    } while (!r.EndOfMessage);
                    JsonNode? n;
                    try { n = JsonNode.Parse(Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length)); }
                    catch (System.Text.Json.JsonException) { continue; }
                    if (n is JsonObject o && o["id"] is JsonValue iv && iv.TryGetValue<int>(out var id))
                    {
                        TaskCompletionSource<JsonNode>? t;
                        lock (schloss) { offen.Remove(id, out t); }
                        t?.TrySetResult(o);
                    }
                }
            }
            catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException or IOException) { /* Verbindung zu */ }
            finally
            {
                lock (schloss) { foreach (var t in offen.Values) t.TrySetException(new WebSocketException("Die Verbindung zum Browser der Brille ist geschlossen.")); offen.Clear(); }
            }
        }

        /// <summary>Sendet eine erlaubte Methode und wartet auf die Antwort. CdpFehler, wenn der Browser einen Fehler meldet.</summary>
        public async Task<JsonNode> Senden(string methode, JsonObject? par, string? sitzung, CancellationToken abbruch)
        {
            if (!ErlaubteMethoden.Contains(methode, StringComparer.Ordinal)) throw new InvalidOperationException($"Die CDP-Methode {methode} ist nicht erlaubt.");
            var tcs = new TaskCompletionSource<JsonNode>(TaskCreationOptions.RunContinuationsAsynchronously);
            int id;
            lock (schloss) { id = ++naechste; offen[id] = tcs; }
            var m = new JsonObject { ["id"] = id, ["method"] = methode };
            if (par is not null) m["params"] = par;
            if (sitzung is not null) m["sessionId"] = sitzung;
            using var grenze = CancellationTokenSource.CreateLinkedTokenSource(abbruch);
            grenze.CancelAfter(zeit);
            try
            {
                await ws.SendAsync(Encoding.UTF8.GetBytes(m.ToJsonString()), WebSocketMessageType.Text, true, grenze.Token);
                var fertig = await tcs.Task.WaitAsync(grenze.Token);
                if (fertig["error"] is JsonNode err) throw new CdpFehler(err["message"]?.GetValue<string>() ?? "Fehler im Browser der Brille.");
                return fertig;
            }
            catch (OperationCanceledException) when (!abbruch.IsCancellationRequested) { throw new TimeoutException($"Keine Antwort auf {methode} innerhalb von {zeit.TotalSeconds:0.#} s."); }
            finally { lock (schloss) offen.Remove(id); }
        }

        public async Task<string> Anhaengen(string zielId, CancellationToken abbruch)
        {
            var a = await Senden("Target.attachToTarget", new JsonObject { ["targetId"] = zielId, ["flatten"] = true }, null, abbruch);
            return a["result"]?["sessionId"]?.GetValue<string>() is { Length: > 0 } s ? s : throw new CdpFehler("Der Tab liess sich nicht anhängen.");
        }

        /// <summary>Wertet einen der drei festen Ausdrücke aus und gibt dessen Wert zurück (null, wenn keiner).</summary>
        public async Task<JsonNode?> Auswerten(Ausdruck was, string sitzung, bool benutzerHandlung, CancellationToken abbruch)
        {
            var par = new JsonObject { ["expression"] = Text(was), ["returnByValue"] = true, ["userGesture"] = benutzerHandlung };
            var a = await Senden("Runtime.evaluate", par, sitzung, abbruch);
            if (a["result"]?["exceptionDetails"] is not null) throw new CdpFehler("Der Ausdruck ist im Browser der Brille fehlgeschlagen.");
            return a["result"]?["result"]?["value"];
        }

        /// <summary>Status des Knopfs #vr-eintreten: nur laedt | bereit | startet | aktiv, sonst null.</summary>
        public async Task<string?> Status(string sitzung, CancellationToken abbruch)
        {
            var w = await Auswerten(Ausdruck.Lesen, sitzung, false, abbruch);
            return w is JsonValue v && v.TryGetValue<string>(out var s) && Stati.Contains(s, StringComparer.Ordinal) ? s : null;
        }

        public void Dispose()
        {
            try { ende.Cancel(); } catch (ObjectDisposedException) { }
            try { ws.Abort(); ws.Dispose(); } catch (Exception) { /* schon zu */ }
        }
    }
}
