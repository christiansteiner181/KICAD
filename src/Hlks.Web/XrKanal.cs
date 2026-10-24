// Live-Kanal mit Teilnehmern (Modul XR, Quest App P3; Entscheid Christian 23.10.2026 «Protokoll mehrbenutzerfähig, PC sieht die Brille»).
//
//   GET /api/xr/kanal   WebSocket, nur mit gültigem Kopplungsschlüssel und nur über den Netzzugang (dort steht die WebSocket-Middleware).
//
// Ablauf: Die Brille sendet zuerst «hallo» (Frist 10 s), der Server antwortet «willkommen» (eigene Teilnehmer-Id, die anderen Teilnehmer, aktueller
// VR-Auftrag, aktuelle Stellschrauben) und meldet den anderen «hallo». Danach: pose (Kopf und Hände, 20–30 je Sekunde), zeigen (Element, auf das die
// Brille zeigt; für «Claude sieht mit»), messprotokoll (Interface-Labor). Der PC sendet: auftrag (sofort, sobald POST /api/vr/auftrag ihn setzt; statt
// der Abfrage alle 2 s) und stellschraube (POST /api/xr/stellschraube). Am Ende «tschuess» an die anderen. Schema: XrNachrichten.cs und OBERFLAECHE.md.
//
// Grenzen: eine Verbindung je Kopplung (eine neue derselben Brille ersetzt die alte, Code 4002), höchstens 8 Teilnehmer, Nachrichten nur als Text bis
// 16 KB, je Teilnehmer höchstens 60 Nachrichten je Sekunde (darüber verworfen, bei Flut getrennt), Posen werden höchstens 30 je Sekunde weitergereicht
// (die letzte Pose für den PC gilt immer), Sendeschlange 256 (läuft sie voll, wird der Teilnehmer getrennt, Code 4003). Widerruf der Kopplung trennt
// sofort (Code 4001). Der Server reicht nie Rohtext weiter, nur geprüfte und neu gebaute Nachrichten.
//
// Messprotokolle: je Sitzung (Verbindung) eine Datei JSON-Zeilen in <XrEinstellungen.Ordner>\messprotokolle\<JJJJMMTT-hhmmss>_<Teilnehmer>.jsonl
// (Vorgabe %LOCALAPPDATA%\HLKS_CAD\xr\messprotokolle; ausserhalb von Repository und OneDrive), höchstens 5 MB je Sitzung.

using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Hlks.Kern;

namespace Hlks.Web;

/// <summary>Schliesst die Verbindung mit Status und Grund.</summary>
public sealed class XrKanalSchluss(WebSocketCloseStatus status, string grund) : Exception(grund)
{
    public WebSocketCloseStatus Status { get; } = status;
}

public sealed class XrKanal
{
    /// <summary>Eigene Schliess-Codes (4000–4999, RFC 6455): Kopplung widerrufen (App: nicht neu verbinden), durch neue Verbindung ersetzt, zu langsam (neu verbinden).</summary>
    public const WebSocketCloseStatus Widerrufen = (WebSocketCloseStatus)4001;
    public const WebSocketCloseStatus Ersetzt = (WebSocketCloseStatus)4002;
    public const WebSocketCloseStatus ZuLangsam = (WebSocketCloseStatus)4003;
    const int FehlerMax = 100;
    static readonly UTF8Encoding Utf8Streng = new(false, true);

    sealed class Teilnehmer
    {
        public required string Id;
        public required XrHallo Hallo;
        public required XrKopplung Kopplung;
        public required DateTimeOffset Seit;
        public required WebSocket Socket;
        public required Channel<string> Schlange;
        public required CancellationTokenSource Abbruch;
        public XrPose? Pose;
        public DateTimeOffset? PoseZeit;
        public XrZeigen? Zeigen;
        public DateTimeOffset? ZeigenZeit;
        public long Posen, Weitergereicht, Verworfen, Fehler;
        public (WebSocketCloseStatus Status, string Grund)? Schluss;
        public volatile bool Beendet;
        public string? Protokolldatei;
        public long ProtokollBytes, ProtokollNr;
    }

    readonly XrEinstellungen einstellungen;
    readonly VrAuftragSpeicher auftraege;
    readonly XrBremse posenBremse, nachrichtenBremse;
    readonly object sperre = new();
    readonly Dictionary<string, Teilnehmer> teilnehmer = new(StringComparer.Ordinal);
    readonly Dictionary<string, JsonNode> stellschrauben = new(StringComparer.Ordinal);

    public XrKanal(XrEinstellungen einstellungen, XrKopplungen kopplungen, VrAuftragSpeicher auftraege)
    {
        this.einstellungen = einstellungen;
        this.auftraege = auftraege;
        posenBremse = new XrBremse(einstellungen.PosenJeSekunde, einstellungen.PosenVorrat, einstellungen.Jetzt);
        nachrichtenBremse = new XrBremse(einstellungen.NachrichtenJeSekunde, einstellungen.NachrichtenJeSekunde, einstellungen.Jetzt);
        kopplungen.KopplungEntfernt += KopplungTrennen;
        auftraege.Gesetzt += AuftragSenden;
    }

    public int Anzahl { get { lock (sperre) return teilnehmer.Count; } }

    public string ProtokollOrdner => Path.Combine(einstellungen.OrdnerPfad(), "messprotokolle");

    // ---------------------------------------------------------------- Verbindung

    /// <summary>Bedient eine WebSocket-Verbindung der Kopplung bis zum Ende.</summary>
    public async Task Bedienen(HttpContext ctx, XrKopplung kopplung)
    {
        using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
        using var abbruch = CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted);
        Teilnehmer? t = null;
        Task? sender = null;
        var status = WebSocketCloseStatus.NormalClosure;
        var grund = "tschuess";
        try
        {
            string? erste;
            using (var frist = CancellationTokenSource.CreateLinkedTokenSource(abbruch.Token))
            {
                frist.CancelAfter(einstellungen.HalloFrist);
                erste = await Empfangen(ws, frist.Token);
            }
            if (erste is null) return;
            XrHallo hallo;
            try
            {
                var (typ, o) = XrNachrichten.Lesen(erste);
                if (typ != "hallo") throw new XrNachrichtFehler("Zuerst «hallo» senden.");
                hallo = XrNachrichten.Hallo(o);
            }
            catch (XrNachrichtFehler f)
            {
                await ws.SendAsync(Encoding.UTF8.GetBytes(FehlerText(f.Message, "hallo")), WebSocketMessageType.Text, true, abbruch.Token);
                throw new XrKanalSchluss(WebSocketCloseStatus.PolicyViolation, "kein gültiges hallo");
            }
            t = Anmelden(ws, kopplung, hallo, abbruch);
            sender = Senden(t);
            while (true)
            {
                var text = await Empfangen(ws, t.Abbruch.Token);
                if (text is null) break;
                Verarbeiten(t, text);
            }
        }
        catch (XrKanalSchluss s) { status = s.Status; grund = s.Message; }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException or IOException) { /* Gegenstelle weg oder Frist: nichts mehr zu senden */ }
        finally
        {
            if (t is not null)
            {
                Abmelden(t);
                t.Schlange.Writer.TryComplete();
                if (sender is not null && await Task.WhenAny(sender, Task.Delay(TimeSpan.FromSeconds(3))) != sender) t.Abbruch.Cancel();
            }
            if (ws.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                try
                {
                    using var zu = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await ws.CloseAsync(status, grund, zu.Token);
                }
                catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException or IOException) { /* Gegenstelle weg */ }
            }
            t?.Abbruch.Dispose();
        }
    }

    async Task<string?> Empfangen(WebSocket ws, CancellationToken abbruch)
    {
        var puffer = new byte[4096];
        using var ms = new MemoryStream();
        while (true)
        {
            var r = await ws.ReceiveAsync(puffer.AsMemory(), abbruch);
            if (r.MessageType == WebSocketMessageType.Close) return null;
            if (r.MessageType != WebSocketMessageType.Text) throw new XrKanalSchluss(WebSocketCloseStatus.InvalidMessageType, "nur Textnachrichten");
            if (ms.Length + r.Count > einstellungen.NachrichtMax) throw new XrKanalSchluss(WebSocketCloseStatus.MessageTooBig, "Nachricht zu gross");
            ms.Write(puffer, 0, r.Count);
            if (r.EndOfMessage) break;
        }
        try { return Utf8Streng.GetString(ms.GetBuffer(), 0, (int)ms.Length); }
        catch (DecoderFallbackException) { throw new XrKanalSchluss(WebSocketCloseStatus.InvalidPayloadData, "kein UTF-8"); }
    }

    async Task Senden(Teilnehmer t)
    {
        try
        {
            await foreach (var text in t.Schlange.Reader.ReadAllAsync(t.Abbruch.Token))
                await t.Socket.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, t.Abbruch.Token);
            if (t.Schluss is { } s && t.Socket.State == WebSocketState.Open)
            {
                t.Abbruch.CancelAfter(TimeSpan.FromSeconds(3));   // antwortet die Brille nicht auf das Schliessen, wird abgebrochen
                await t.Socket.CloseOutputAsync(s.Status, s.Grund, t.Abbruch.Token);
            }
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException or IOException)
        {
            try { t.Abbruch.Cancel(); } catch (ObjectDisposedException) { /* schon zu */ }
        }
    }

    Teilnehmer Anmelden(WebSocket ws, XrKopplung kopplung, XrHallo hallo, CancellationTokenSource abbruch)
    {
        Teilnehmer? alt;
        Teilnehmer neu;
        var symbolsatz = auftraege.Symbolsatz();   // liest die Benutzereinstellungen, darum vor der Sperre
        lock (sperre)
        {
            alt = teilnehmer.Values.FirstOrDefault(x => x.Kopplung.Id == kopplung.Id);
            if (alt is not null)
            {
                teilnehmer.Remove(alt.Id);
                alt.Beendet = true;
                AnAndereUnterSperre(alt, XrNachrichten.Bauen("tschuess", new JsonObject { ["id"] = alt.Id }));
            }
            if (teilnehmer.Count >= einstellungen.TeilnehmerMax)
                throw new XrKanalSchluss((WebSocketCloseStatus)1013, "Kanal voll");   // 1013: später erneut versuchen
            neu = new Teilnehmer
            {
                Id = "t" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4)),
                Hallo = hallo, Kopplung = kopplung, Seit = einstellungen.Jetzt(), Socket = ws,
                Schlange = Channel.CreateBounded<string>(new BoundedChannelOptions(einstellungen.SchlangeMax) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait }),
                Abbruch = CancellationTokenSource.CreateLinkedTokenSource(abbruch.Token),
            };
            var andere = new JsonArray([.. teilnehmer.Values.Select(x => (JsonNode)XrNachrichten.TeilnehmerKnoten(Info(x)))]);
            var schrauben = new JsonObject();
            foreach (var (k, w) in stellschrauben) schrauben[k] = w.DeepClone();
            var auftrag = auftraege.Lesen() is { } a ? VrAuftragSpeicher.Zuobjekt(a) : null;
            // V1a: «symbole» = Symbolsatz des Benutzers (oberflaeche.symbole), damit die App dieselben Symbole zeigt wie KICAD
            neu.Schlange.Writer.TryWrite(XrNachrichten.Bauen("willkommen", new JsonObject
            {
                ["id"] = neu.Id, ["teilnehmer"] = andere, ["auftrag"] = auftrag, ["stellschrauben"] = schrauben, ["symbole"] = symbolsatz,
            }));
            AnAndereUnterSperre(neu, XrNachrichten.Bauen("hallo", XrNachrichten.TeilnehmerKnoten(Info(neu))));
            teilnehmer[neu.Id] = neu;
        }
        if (alt is not null) Trennen(alt, Ersetzt, "durch eine neue Verbindung derselben Brille ersetzt", sofort: false);
        return neu;
    }

    void Abmelden(Teilnehmer t)
    {
        t.Beendet = true;
        lock (sperre)
        {
            if (!teilnehmer.TryGetValue(t.Id, out var x) || !ReferenceEquals(x, t)) return;
            teilnehmer.Remove(t.Id);
            AnAndereUnterSperre(t, XrNachrichten.Bauen("tschuess", new JsonObject { ["id"] = t.Id }));
        }
        posenBremse.Vergessen(t.Id);
        nachrichtenBremse.Vergessen(t.Id);
    }

    /// <summary>Trennt einen Teilnehmer: sofort (Abbruch) oder mit Schliess-Nachricht nach den wartenden Nachrichten.</summary>
    void Trennen(Teilnehmer t, WebSocketCloseStatus status, string grund, bool sofort)
    {
        t.Beendet = true;
        try
        {
            if (sofort) { t.Abbruch.Cancel(); return; }
            t.Schluss ??= (status, grund);
            t.Schlange.Writer.TryComplete();
            t.Abbruch.CancelAfter(TimeSpan.FromSeconds(5));
        }
        catch (ObjectDisposedException) { /* Verbindung ist schon zu */ }
    }

    // ---------------------------------------------------------------- Nachrichten der Brille

    void Verarbeiten(Teilnehmer t, string text)
    {
        if (!nachrichtenBremse.Nehmen(t.Id))
        {
            if (Interlocked.Increment(ref t.Verworfen) > einstellungen.VerworfenMax) throw new XrKanalSchluss(WebSocketCloseStatus.PolicyViolation, "zu viele Nachrichten");
            return;
        }
        string? typ = null;
        try
        {
            JsonObject o;
            (typ, o) = XrNachrichten.Lesen(text);
            switch (typ)
            {
                case "pose":
                {
                    var p = XrNachrichten.Pose(o);
                    var jetzt = einstellungen.Jetzt();
                    lock (sperre) { t.Pose = p; t.PoseZeit = jetzt; t.Posen++; }
                    if (!posenBremse.Nehmen(t.Id)) break;   // gedrosselt: die letzte Pose gilt für den PC, weitergereicht wird sie nicht
                    var k = XrNachrichten.PoseKnoten(p);
                    k["von"] = t.Id;
                    k["zeit"] = jetzt.ToUnixTimeMilliseconds();
                    lock (sperre) { t.Weitergereicht++; AnAndereUnterSperre(t, XrNachrichten.Bauen("pose", k)); }
                    break;
                }
                case "zeigen":
                {
                    var z = XrNachrichten.Zeigen(o);
                    var jetzt = einstellungen.Jetzt();
                    var k = XrNachrichten.ZeigenKnoten(z);
                    k["von"] = t.Id;
                    k["zeit"] = jetzt.ToUnixTimeMilliseconds();
                    lock (sperre) { t.Zeigen = z; t.ZeigenZeit = jetzt; AnAndereUnterSperre(t, XrNachrichten.Bauen("zeigen", k)); }
                    break;
                }
                case "messprotokoll":
                    Protokollieren(t, XrNachrichten.Messprotokoll(o));
                    break;
                case "tschuess":
                    throw new XrKanalSchluss(WebSocketCloseStatus.NormalClosure, "tschuess");
                case "hallo":
                    throw new XrNachrichtFehler("Schon angemeldet.");
                default:
                    if (XrNachrichten.VomPc.Contains(typ)) throw new XrNachrichtFehler($"«{typ}» sendet nur der PC.");
                    throw new XrNachrichtFehler($"Unbekannter Typ «{(typ.Length > 20 ? typ[..20] + "…" : typ)}».");
            }
        }
        catch (XrNachrichtFehler f)
        {
            if (Interlocked.Increment(ref t.Fehler) > FehlerMax) throw new XrKanalSchluss(WebSocketCloseStatus.PolicyViolation, "zu viele ungültige Nachrichten");
            Einreihen(t, FehlerText(f.Message, typ));
        }
    }

    void Protokollieren(Teilnehmer t, XrMessprotokoll m)
    {
        long nr;
        try
        {
            lock (t)
            {
                var ordner = ProtokollOrdner;
                t.Protokolldatei ??= Path.Combine(ordner, $"{t.Seit.UtcDateTime:yyyyMMdd-HHmmss}_{t.Id}.jsonl");
                var zeile = Json.Text(new
                {
                    zeit = einstellungen.Jetzt().ToString("o"), teilnehmer = t.Id, name = t.Hallo.Name, geraet = t.Hallo.Geraet, app = t.Hallo.App,
                    kopplung = t.Kopplung.Id, brille = t.Kopplung.Brille, nr = t.ProtokollNr + 1,
                    aufgabe = m.Aufgabe, variante = m.Variante, varianten = m.Varianten, zeitMs = m.ZeitMs, fehlgriffe = m.Fehlgriffe, abbrueche = m.Abbrueche, erfolg = m.Erfolg,
                    stellschrauben = m.Stellschrauben, notiz = m.Notiz,
                }) + "\n";
                var bytes = Encoding.UTF8.GetByteCount(zeile);
                if (t.ProtokollBytes + bytes > einstellungen.ProtokollMax) throw new XrNachrichtFehler("Das Messprotokoll dieser Sitzung ist voll (5 MB); neu verbinden.");
                XrSchutz.OrdnerAnlegen(ordner);
                File.AppendAllText(t.Protokolldatei, zeile, new UTF8Encoding(false));
                t.ProtokollBytes += bytes;
                nr = ++t.ProtokollNr;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new XrNachrichtFehler("Das Messprotokoll konnte nicht gespeichert werden: " + ex.Message);
        }
        Einreihen(t, XrNachrichten.Bauen("quittung", new JsonObject { ["bezug"] = "messprotokoll", ["nr"] = nr }));
    }

    static string FehlerText(string meldung, string? bezug)
    {
        var o = new JsonObject { ["meldung"] = meldung };
        if (bezug is not null && bezug.Length <= 20) o["bezug"] = bezug;
        return XrNachrichten.Bauen("fehler", o);
    }

    // ---------------------------------------------------------------- Senden an Teilnehmer

    void AnAndereUnterSperre(Teilnehmer von, string text)
    {
        foreach (var x in teilnehmer.Values)
            if (!ReferenceEquals(x, von)) Einreihen(x, text);
    }

    void Einreihen(Teilnehmer x, string text)
    {
        if (x.Schlange.Writer.TryWrite(text) || x.Beendet) return;
        Trennen(x, ZuLangsam, "zu langsam", sofort: true);
    }

    /// <summary>Neuer VR-Auftrag vom PC (VrAuftragSpeicher.Gesetzt): sofort an alle Teilnehmer.</summary>
    public void AuftragSenden(VrAuftragWert a)
    {
        var text = XrNachrichten.Bauen("auftrag", new JsonObject { ["auftrag"] = VrAuftragSpeicher.Zuobjekt(a) });
        lock (sperre) foreach (var x in teilnehmer.Values) Einreihen(x, text);
    }

    /// <summary>Stellschraube setzen (PC → Brille). an = Teilnehmer-Id (nur diesem, nicht gemerkt) oder null (alle, gemerkt für neue Teilnehmer;
    /// wert null setzt zurück). Gibt die Zahl der Empfänger zurück. Wirft HlksFehler bei ungültigen Werten oder unbekanntem Teilnehmer.</summary>
    public int StellschraubeSetzen(string? schluessel, JsonNode? wert, string? an)
    {
        if (!XrNachrichten.StellschraubeSchluesselGueltig(schluessel))
            throw new HlksFehler("Der Schlüssel der Stellschraube ist ungültig.", "Kleinbuchstaben, Ziffern, Punkt, Unterstrich, Bindestrich (bis 64 Zeichen), z. B. «pinch.schwelle».");
        if (wert is not null && XrNachrichten.StellschraubeFehler(wert) is { } fehler)
            throw new HlksFehler($"Stellschraube «{schluessel}»: {fehler}", "Eine Zahl, true/false oder einen kurzen Text senden; null setzt zurück.");
        var text = XrNachrichten.Bauen("stellschraube", new JsonObject { ["schluessel"] = schluessel, ["wert"] = wert?.DeepClone() });
        lock (sperre)
        {
            if (an is not null)
            {
                if (!teilnehmer.TryGetValue(an, out var x)) throw new HlksFehler($"Den Teilnehmer «{(an.Length > 20 ? an[..20] : an)}» gibt es nicht.", "GET /api/xr/teilnehmer listet die verbundenen Teilnehmer.");
                Einreihen(x, text);
                return 1;
            }
            if (wert is null) stellschrauben.Remove(schluessel!);
            else
            {
                if (!stellschrauben.ContainsKey(schluessel!) && stellschrauben.Count >= XrNachrichten.StellschraubenMax)
                    throw new HlksFehler($"Es gibt schon {XrNachrichten.StellschraubenMax} Stellschrauben.", "Nicht mehr gebrauchte mit wert null zurücksetzen.");
                stellschrauben[schluessel!] = wert.DeepClone();
            }
            foreach (var x in teilnehmer.Values) Einreihen(x, text);
            return teilnehmer.Count;
        }
    }

    public JsonObject Stellschrauben()
    {
        lock (sperre)
        {
            var o = new JsonObject();
            foreach (var (k, w) in stellschrauben.OrderBy(p => p.Key, StringComparer.Ordinal)) o[k] = w.DeepClone();
            return o;
        }
    }

    /// <summary>Kopplung widerrufen oder ersetzt: deren Verbindung trennen (Code 4001).</summary>
    public void KopplungTrennen(string kopplung)
    {
        List<Teilnehmer> weg;
        lock (sperre) weg = [.. teilnehmer.Values.Where(x => x.Kopplung.Id == kopplung)];
        foreach (var x in weg) Trennen(x, Widerrufen, "Kopplung widerrufen", sofort: false);
    }

    /// <summary>Netzzugang wird beendet: alle trennen (Code 1001).</summary>
    public void AlleTrennen()
    {
        List<Teilnehmer> alle;
        lock (sperre) alle = [.. teilnehmer.Values];
        foreach (var x in alle) Trennen(x, WebSocketCloseStatus.EndpointUnavailable, "XR-Zugang beendet", sofort: false);
    }

    // ---------------------------------------------------------------- Für den PC

    static XrTeilnehmerInfo Info(Teilnehmer t) => new(t.Id, t.Hallo.Name, t.Hallo.Geraet);

    /// <summary>Die verbundenen Teilnehmer mit letzter Pose und letztem Zeigen (für GET /api/xr/teilnehmer).</summary>
    public JsonArray TeilnehmerListe()
    {
        lock (sperre)
        {
            return [.. teilnehmer.Values.OrderBy(t => t.Seit).Select(t => (JsonNode)new JsonObject
            {
                ["id"] = t.Id, ["name"] = t.Hallo.Name, ["geraet"] = t.Hallo.Geraet, ["app"] = t.Hallo.App,
                ["kopplung"] = t.Kopplung.Id, ["brille"] = t.Kopplung.Brille, ["brillenName"] = t.Kopplung.Name, ["seit"] = t.Seit.ToString("o"),
                ["pose"] = t.Pose is null ? null : XrNachrichten.PoseKnoten(t.Pose), ["poseZeit"] = t.PoseZeit?.ToString("o"),
                ["zeigen"] = t.Zeigen is null ? null : XrNachrichten.ZeigenKnoten(t.Zeigen), ["zeigenZeit"] = t.ZeigenZeit?.ToString("o"),
                ["posen"] = t.Posen, ["weitergereicht"] = t.Weitergereicht, ["verworfen"] = Interlocked.Read(ref t.Verworfen),
            })];
        }
    }
}
