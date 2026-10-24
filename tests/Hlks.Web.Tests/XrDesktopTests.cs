using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Hlks.Kern;
using Hlks.Xr.Desktop;
using Hlks.Xr.Netz;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Hlks.Web.Tests;

/// <summary>Bildschirme zum Steuern: zwei kleine Monitore, deren Bildpunkte der Test setzt; zählt die Erfassungen.</summary>
sealed class SteuerQuelle : IBildschirmQuelle
{
    public readonly List<BildschirmInfo> Liste = [new("A", "Monitor A", 0, 0, 200, 130, true, 144), new("B", "Monitor B", 200, 0, 96, 64, false, 96)];
    public readonly Dictionary<string, byte[]> Bilder = new();
    public volatile bool Gesperrt;
    int erfasst;
    public int Erfasst => Volatile.Read(ref erfasst);
    public string Art => "testbild";

    public SteuerQuelle()
    {
        foreach (var m in Liste) Bilder[m.Id] = Muster(m.Breite, m.Hoehe, m.Id == "A" ? 0 : 50);
    }

    public static byte[] Muster(int b, int h, int versatz)
    {
        var d = new byte[b * h * 4];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < b; x++)
            {
                var o = (y * b + x) * 4;
                d[o] = (byte)(x * 3 + versatz); d[o + 1] = (byte)(y * 5); d[o + 2] = (byte)((x / 8 + y / 8) % 2 == 0 ? 220 : 30); d[o + 3] = 255;
            }
        return d;
    }

    public void Setzen(string id, int x, int y, byte r, byte g, byte b)
    {
        var m = Liste.Single(l => l.Id == id);
        lock (Bilder) { var o = (y * m.Breite + x) * 4; var d = Bilder[id]; d[o] = b; d[o + 1] = g; d[o + 2] = r; }
    }

    public IReadOnlyList<BildschirmInfo> Bildschirme() => [.. Liste];

    public bool Erfassen(string id, int zielBreite, int zielHoehe, byte[] puffer)
    {
        Interlocked.Increment(ref erfasst);
        if (Gesperrt) return false;
        var m = Liste.FirstOrDefault(l => l.Id == id);
        if (m is null) return false;
        lock (Bilder)
        {
            if (zielBreite == m.Breite && zielHoehe == m.Hoehe) Buffer.BlockCopy(Bilder[id], 0, puffer, 0, m.Breite * m.Hoehe * 4);
            else XrDesktopQuellen.Verkleinern(Bilder[id], m.Breite, m.Hoehe, puffer, zielBreite, zielHoehe);
        }
        return true;
    }
}

/// <summary>Ein Teilnehmer wie die App auf Protokollebene (Text und binär), ohne Quittung, wenn der Test es so will.</summary>
sealed class DesktopRohKlient : IAsyncDisposable
{
    readonly ClientWebSocket ws = new();
    public readonly Channel<(WebSocketMessageType Typ, byte[] Daten)> Eingang = Channel.CreateUnbounded<(WebSocketMessageType, byte[])>();
    Task? leser;
    public WebSocketCloseStatus? Schluss { get; private set; }

    public static async Task<DesktopRohKlient> Verbinden(int port, string fp, string token)
    {
        var k = new DesktopRohKlient();
        k.ws.Options.RemoteCertificateValidationCallback = (_, z, _, _) => XrServer.Pinnen(z, fp);
        k.ws.Options.SetRequestHeader("Authorization", "Bearer " + token);
        using var f = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await k.ws.ConnectAsync(new Uri($"wss://127.0.0.1:{port}/api/xr/desktop"), f.Token);
        k.leser = k.Lesen();
        return k;
    }

    async Task Lesen()
    {
        var puffer = new byte[256 * 1024];
        try
        {
            while (ws.State is WebSocketState.Open or WebSocketState.CloseSent)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult r;
                do
                {
                    r = await ws.ReceiveAsync(puffer, CancellationToken.None);
                    if (r.MessageType == WebSocketMessageType.Close)
                    {
                        Schluss = r.CloseStatus;
                        if (ws.State == WebSocketState.CloseReceived) await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "ok", CancellationToken.None);
                        return;
                    }
                    ms.Write(puffer, 0, r.Count);
                } while (!r.EndOfMessage);
                Eingang.Writer.TryWrite((r.MessageType, ms.ToArray()));
            }
        }
        catch (Exception ex) when (ex is WebSocketException or ObjectDisposedException) { }
        finally { Eingang.Writer.TryComplete(); }
    }

    public Task Senden(string typ, JsonObject? inhalt = null)
    {
        var o = new JsonObject { ["typ"] = typ, ["fassung"] = 1 };
        if (inhalt is not null) foreach (var (k, w) in inhalt) o[k] = w?.DeepClone();
        return ws.SendAsync(Encoding.UTF8.GetBytes(o.ToJsonString()), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public async Task<(WebSocketMessageType Typ, byte[] Daten)?> Naechste(TimeSpan? frist = null)
    {
        using var f = new CancellationTokenSource(frist ?? TimeSpan.FromSeconds(10));
        try { return await Eingang.Reader.ReadAsync(f.Token); }
        catch (Exception ex) when (ex is OperationCanceledException or ChannelClosedException) { return null; }
    }

    public async Task<JsonObject> Text(string typ, TimeSpan? frist = null)
    {
        var bis = DateTime.UtcNow + (frist ?? TimeSpan.FromSeconds(10));
        while (DateTime.UtcNow < bis)
        {
            var n = await Naechste(bis - DateTime.UtcNow);
            if (n is null) break;
            if (n.Value.Typ == WebSocketMessageType.Text && JsonNode.Parse(n.Value.Daten) is JsonObject o && (string?)o["typ"] == typ) return o;
        }
        throw new TimeoutException($"keine Nachricht «{typ}» (Schluss {Schluss})");
    }

    public async Task<byte[]> Binaer(TimeSpan? frist = null)
    {
        var bis = DateTime.UtcNow + (frist ?? TimeSpan.FromSeconds(10));
        while (DateTime.UtcNow < bis)
        {
            var n = await Naechste(bis - DateTime.UtcNow);
            if (n is null) break;
            if (n.Value.Typ == WebSocketMessageType.Binary) return n.Value.Daten;
        }
        throw new TimeoutException($"kein Bild (Schluss {Schluss})");
    }

    /// <summary>Zählt die Bilder, die in der Frist ankommen (Text wird übersprungen).</summary>
    public async Task<List<byte[]>> BilderIn(TimeSpan frist)
    {
        var bis = DateTime.UtcNow + frist;
        var aus = new List<byte[]>();
        while (DateTime.UtcNow < bis)
        {
            var n = await Naechste(bis - DateTime.UtcNow);
            if (n is null) break;
            if (n.Value.Typ == WebSocketMessageType.Binary) aus.Add(n.Value.Daten);
        }
        return aus;
    }

    public async Task<WebSocketCloseStatus?> GeschlossenAbwarten(TimeSpan? frist = null)
    {
        if (leser is not null) await Task.WhenAny(leser, Task.Delay(frist ?? TimeSpan.FromSeconds(10)));
        return Schluss;
    }

    public async ValueTask DisposeAsync()
    {
        try { if (ws.State == WebSocketState.Open) await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "ende", new CancellationTokenSource(2000).Token); }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException) { }
        if (leser is not null) await Task.WhenAny(leser, Task.Delay(3000));
        ws.Dispose();
    }
}

/// <summary>
/// Quest App V1c (Modul XR, XR-I «Desktop in der Brille»): Kodierung am PC (Kacheln, nur Geänderte, Vorfilter, Deflate) gegen den Leser der App (Hlks.Xr.Desktop,
/// unabhängige Umsetzung) hin und zurück; Übertragung über den echten Netzzugang (TLS auf 127.0.0.1, Pinning, Kopplung): ohne Kopplung kein Bild, Start, Flusskontrolle,
/// Pause und Ende, Monitorwahl, Ersetzen, Widerruf, Beenden am PC, Anzeige-Zustand; dazu die App-Verbindung (Hlks.Xr.Netz.DesktopVerbindung). Nie der echte Bildschirm
/// (Attrappen SteuerQuelle und XrDesktopTestbild); der echte nur im Echt-Test mit HLKS_XR_DESKTOP_ECHT=1.
/// </summary>
public class XrDesktopTests(ITestOutputHelper aus)
{
    static XrServer Server(IBildschirmQuelle quelle, XrDesktopEinstellungen? e = null) =>
        new(dienste: s => { s.AddSingleton(quelle); if (e is not null) s.AddSingleton(e); });

    static async Task Warte(Func<Task<bool>> bedingung, string was, double sekunden = 10)
    {
        var bis = DateTime.UtcNow.AddSeconds(sekunden);
        while (!await bedingung())
        {
            if (DateTime.UtcNow > bis) throw new TimeoutException(was);
            await Task.Delay(30);
        }
    }

    static async Task<JsonObject> Zustand(XrServer s) => (JsonObject)JsonNode.Parse(await s.Lokal.GetStringAsync("/api/xr/desktop/zustand"))!;

    static DesktopPuffer Dekodieren(DesktopPuffer? p, byte[] bild)
    {
        var k = DesktopFormat.KopfLesen(bild);
        p ??= new DesktopPuffer(new DesktopRaster(k.Breite, k.Hoehe, k.Kachel));
        p.Anwenden(bild, k);
        return p;
    }

    static void Gleich(DesktopPuffer p, byte[] bgra, int b, int h)
    {
        for (int y = 0; y < h; y++)
            for (int x = 0; x < b; x++)
            {
                var o = (y * b + x) * 4;
                var (r, g, bl) = p.Punkt(x, y);
                if (r != bgra[o + 2] || g != bgra[o + 1] || bl != bgra[o]) Assert.Fail($"Punkt {x}/{y}: {r},{g},{bl} statt {bgra[o + 2]},{bgra[o + 1]},{bgra[o]}");
            }
    }

    // ---------------------------------------------------------------- Kodierung

    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(1366, 768)]
    [InlineData(97, 33)]
    public void Kodierung_hin_und_zurueck_Vollbild_dann_nur_geaenderte_Kacheln(int b, int h)
    {
        var q = SteuerQuelle.Muster(b, h, 7);
        var k = new XrDesktopKodierer();
        var v = k.Kodieren(q, b, h, 42)!;
        Assert.True(v.Voll);
        Assert.Equal(1u, v.Nr);
        Assert.Equal(((b + 63) / 64) * ((h + 63) / 64), v.Kacheln);
        var p = Dekodieren(null, v.Daten);
        Assert.True(p.HatVollbild);
        Gleich(p, q, b, h);
        Assert.Equal(42, DesktopFormat.KopfLesen(v.Daten).ZeitMs);
        // unverändert: kein Bild
        Assert.Null(k.Kodieren(q, b, h, 43));
        // drei Punkte in drei Kacheln ändern (einer am Rand unten rechts): genau diese Kacheln
        void S(int x, int y) { var o = (y * b + x) * 4; q[o] ^= 0xFF; q[o + 2] = 1; }
        S(0, 0); S(b / 2, h / 2); S(b - 1, h - 1);
        var t = k.Kodieren(q, b, h, 44)!;
        Assert.False(t.Voll);
        var erwartet = new[] { (0, 0), (b / 2, h / 2), (b - 1, h - 1) }.Select(xy => xy.Item2 / 64 * ((b + 63) / 64) + xy.Item1 / 64).Distinct().Count();
        Assert.Equal(erwartet, t.Kacheln);
        var stand = Enumerable.Range(0, p.Raster.Felder).Select(p.Stand).ToArray();
        p = Dekodieren(p, t.Daten);
        Gleich(p, q, b, h);
        Assert.Equal(2u, p.LetzteNr);
        // nur die Felder mit Änderungen haben einen neuen Stand
        var geaendert = Enumerable.Range(0, p.Raster.Felder).Count(f => p.Stand(f) != stand[f]);
        Assert.InRange(geaendert, 1, erwartet);
        // zurücksetzen: Vollbild
        k.Zuruecksetzen();
        Assert.True(k.Kodieren(q, b, h, 45)!.Voll);
        aus.WriteLine($"{b} × {h}: Vollbild {v.Daten.Length / 1024.0:0.0} KB (roh {v.Roh / 1024.0:0} KB, {v.Ms:0.0} ms), Teilbild {t.Kacheln} Kacheln {t.Daten.Length} Byte");
    }

    [Fact]
    public void Testbild_packt_gut_Zaehler_Uhr_und_Mauszeiger_aendern_nur_wenige_Kacheln_Verkleinern()
    {
        var zeit = 0.0;
        var tb = new XrDesktopTestbild(bewegt: true, sekunden: () => zeit);
        var m = XrDesktopTestbild.Eins;
        var puffer = new byte[m.Breite * m.Hoehe * 4];
        Assert.True(tb.Erfassen(m.Id, m.Breite, m.Hoehe, puffer));
        var k = new XrDesktopKodierer();
        var voll = k.Kodieren(puffer, m.Breite, m.Hoehe, 0)!;
        var p = Dekodieren(null, voll.Daten);
        Gleich(p, puffer, m.Breite, m.Hoehe);
        Assert.True(voll.Daten.Length < 400 * 1024, $"Vollbild {voll.Daten.Length} Byte");   // 1920 × 1080 roh 6 MB
        // 1/12 s später: Zähler, Uhr und Zeiger
        var groessen = new List<int>();
        var kacheln = new List<int>();
        for (int i = 1; i <= 24; i++)
        {
            zeit = i / 12.0;
            tb.Erfassen(m.Id, m.Breite, m.Hoehe, puffer);
            var t = k.Kodieren(puffer, m.Breite, m.Hoehe, i)!;
            p = Dekodieren(p, t.Daten);
            groessen.Add(t.Daten.Length);
            kacheln.Add(t.Kacheln);
        }
        Gleich(p, puffer, m.Breite, m.Hoehe);
        Assert.True(kacheln.Max() <= 16, $"höchstens {kacheln.Max()} Kacheln je Bild");
        var kbit = groessen.Average() * 8 * 12 / 1000;
        aus.WriteLine($"Testbild 1920 × 1080: Vollbild {voll.Daten.Length / 1024.0:0.0} KB in {voll.Ms:0.0} ms; je Bild {kacheln.Average():0.0} Kacheln, {groessen.Average():0} Byte → {kbit:0} kbit/s bei 12 Bildern/s");
        // Zeiger: an der erwarteten Stelle schwarz (Rand des Pfeils)
        var (zx, zy) = XrDesktopTestbild.Zeiger(m, zeit);
        Assert.Equal(((byte)0, (byte)0, (byte)0), p.Punkt(zx, zy));
        // Verkleinern: 3840 × 2160 → 1920 × 1080, kleiner bleibt
        Assert.Equal((1920, 1080), XrDesktopQuellen.Ziel(3840, 2160, 1920));
        Assert.Equal((1280, 1024), XrDesktopQuellen.Ziel(1280, 1024, 1920));
        Assert.Equal((1920, 1200), XrDesktopQuellen.Ziel(2880, 1800, 1920));
        var klein = new byte[960 * 540 * 4];
        Assert.True(tb.Erfassen(m.Id, 960, 540, klein));
        var kk = new XrDesktopKodierer().Kodieren(klein, 960, 540, 0)!;
        Assert.Equal((960, 540), (DesktopFormat.KopfLesen(kk.Daten).Breite, DesktopFormat.KopfLesen(kk.Daten).Hoehe));
        Assert.Equal(2, tb.Bildschirme().Count);
        Assert.True(tb.Bildschirme()[0].Haupt);
        // im Testlauf nimmt die Vorgabe immer das Testbild (nie den echten Bildschirm)
        Assert.Equal("testbild", XrDesktopQuellen.Vorgabe().Art);
    }

    // ---------------------------------------------------------------- Netzzugang

    [Fact]
    public async Task Ohne_Kopplung_kein_Bild_ohne_WebSocket_400_und_nur_der_WebSocket_ist_im_Netz()
    {
        var q = new SteuerQuelle();
        await using var s = Server(q);
        var (port, _, fp) = await s.Aktiv();
        var paket = await s.Paket("QUEST-D0");
        // ohne Schlüssel bzw. mit falschem: 401, es wird nichts erfasst
        var ex = await Assert.ThrowsAsync<WebSocketException>(() => DesktopRohKlient.Verbinden(port, fp, $"{(string)paket["kopplung"]!}.{new string('C', 43)}"));
        Assert.Contains("401", ex.Message);
        using var ohne = XrServer.Netz(port, fp);
        Assert.Equal(HttpStatusCode.Unauthorized, (await ohne.GetAsync("/api/xr/desktop")).StatusCode);
        using var mit = XrServer.Netz(port, fp, XrServer.Token(paket));
        Assert.Equal(HttpStatusCode.BadRequest, (await mit.GetAsync("/api/xr/desktop")).StatusCode);
        // Zustand und Beenden gibt es nur am PC
        Assert.Equal(HttpStatusCode.NotFound, (await mit.GetAsync("/api/xr/desktop/zustand")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await mit.PostAsync("/api/xr/desktop/beenden", new StringContent("{}", Encoding.UTF8, "application/json"))).StatusCode);
        Assert.Equal(0, q.Erfasst);
        // localhost: ohne Schlüssel 401
        Assert.Equal(HttpStatusCode.Unauthorized, (await s.Lokal.GetAsync("/api/xr/desktop")).StatusCode);
        var z = await Zustand(s);
        Assert.Equal("testbild", (string)z["erfassung"]!);
        Assert.Equal(0, (int)z["verbunden"]!);
        Assert.Equal(2, z["bildschirme"]!.AsArray().Count);
    }

    [Fact]
    public async Task Start_Vollbild_Teilbilder_Flusskontrolle_Pause_und_Ende()
    {
        var q = new SteuerQuelle();
        await using var s = Server(q);
        var (port, _, fp) = await s.Aktiv();
        var paket = await s.Paket("QUEST-D1", "Quest Christian");
        await using var k = await DesktopRohKlient.Verbinden(port, fp, XrServer.Token(paket));
        // ohne «start» passiert nichts
        await Task.Delay(300);
        Assert.Equal(0, q.Erfasst);
        await k.Senden("start", new JsonObject { ["bildrate"] = 30 });
        var b = await k.Text("bildschirm");
        Assert.Equal("A", (string)b["monitor"]!);
        Assert.Equal(200, (int)b["breite"]!);
        Assert.Equal(64, (int)b["kachel"]!);
        Assert.Equal(2, b["monitore"]!.AsArray().Count);
        Assert.True((bool)b["monitore"]![0]!["haupt"]!);
        var voll = await k.Binaer();
        var kopf = DesktopFormat.KopfLesen(voll);
        Assert.True(kopf.Voll);
        var p = Dekodieren(null, voll);
        Gleich(p, q.Bilder["A"], 200, 130);
        // Anzeige am PC: die Brille sieht mit
        var z = await Zustand(s);
        Assert.Equal(1, (int)z["aktiv"]!);
        var st = z["stroeme"]![0]!;
        Assert.Equal("QUEST-D1", (string)st["brille"]!);
        Assert.Equal("Quest Christian", (string)st["name"]!);
        Assert.False((bool)st["pausiert"]!);
        Assert.Equal("Monitor A", (string)st["monitorName"]!);
        // unverändert: keine weiteren Bilder (aber es wird erfasst)
        await k.Senden("quittung", new JsonObject { ["nr"] = kopf.Nr });
        Assert.Empty(await k.BilderIn(TimeSpan.FromMilliseconds(400)));
        Assert.True(q.Erfasst > 3);
        // Änderung: ein Teilbild mit einer Kachel
        q.Setzen("A", 150, 100, 255, 0, 0);
        var t = await k.Binaer();
        var kt = DesktopFormat.KopfLesen(t);
        Assert.False(kt.Voll);
        Assert.Equal(1, kt.Anzahl);
        p = Dekodieren(p, t);
        Assert.Equal(((byte)255, (byte)0, (byte)0), p.Punkt(150, 100));
        // Flusskontrolle: ohne Quittung höchstens zwei Bilder unterwegs, auch wenn sich laufend etwas ändert
        using (var aendern = new CancellationTokenSource())
        {
            var i = 0;
            var laeuft = Task.Run(async () => { while (!aendern.IsCancellationRequested) { q.Setzen("A", i++ % 200, 5, (byte)i, 1, 2); await Task.Delay(10); } });
            var ohneQuittung = await k.BilderIn(TimeSpan.FromMilliseconds(800));
            Assert.Single(ohneQuittung);   // eines war noch ohne Quittung (das Teilbild oben): 1 + 1 = 2 unterwegs
            await k.Senden("quittung", new JsonObject { ["nr"] = kt.Nr });
            await k.Senden("quittung", new JsonObject { ["nr"] = DesktopFormat.KopfLesen(ohneQuittung[0]).Nr });
            var weiter = await k.BilderIn(TimeSpan.FromMilliseconds(800));
            Assert.Equal(2, weiter.Count);
            // Pause: nichts mehr erfasst, Anzeige «pausiert»
            await k.Senden("pause");
            foreach (var w in weiter) await k.Senden("quittung", new JsonObject { ["nr"] = DesktopFormat.KopfLesen(w).Nr });
            await Task.Delay(200);
            var vorher = q.Erfasst;
            Assert.Empty(await k.BilderIn(TimeSpan.FromMilliseconds(600)));
            Assert.InRange(q.Erfasst - vorher, 0, 1);
            z = await Zustand(s);
            Assert.True((bool)z["stroeme"]![0]!["pausiert"]!);
            Assert.Equal(0, (int)z["aktiv"]!);
            Assert.Equal(1, (int)z["verbunden"]!);
            // weiter: wieder Bilder
            await k.Senden("weiter");
            Assert.NotEmpty(await k.BilderIn(TimeSpan.FromMilliseconds(600)));
            aendern.Cancel();
            await laeuft;
        }
        // Ende: die Brille trennt, die Anzeige ist leer
        await k.DisposeAsync();
        await Warte(async () => (int)(await Zustand(s))["verbunden"]! == 0, "der Strom bleibt nach der Trennung");
        var nachher = q.Erfasst;
        await Task.Delay(300);
        Assert.Equal(nachher, q.Erfasst);
    }

    [Fact]
    public async Task Monitor_wechseln_neu_verlangen_Fehler_und_kein_start_trennt()
    {
        var q = new SteuerQuelle();
        await using var s = Server(q, new XrDesktopEinstellungen { StartFrist = TimeSpan.FromMilliseconds(500) });
        var (port, _, fp) = await s.Aktiv();
        var token = XrServer.Token(await s.Paket("QUEST-D2"));
        await using (var k = await DesktopRohKlient.Verbinden(port, fp, token))
        {
            await k.Senden("start", new JsonObject { ["monitor"] = "B" });
            Assert.Equal(96, (int)(await k.Text("bildschirm"))["breite"]!);
            var b1 = await k.Binaer();
            Gleich(Dekodieren(null, b1), q.Bilder["B"], 96, 64);
            await k.Senden("quittung", new JsonObject { ["nr"] = DesktopFormat.KopfLesen(b1).Nr });
            // wechseln auf A: neues «bildschirm» und Vollbild
            await k.Senden("monitor", new JsonObject { ["id"] = "A" });
            Assert.Equal("A", (string)(await k.Text("bildschirm"))["monitor"]!);
            var b2 = await k.Binaer();
            Assert.True(DesktopFormat.KopfLesen(b2).Voll);
            Assert.Equal(200, DesktopFormat.KopfLesen(b2).Breite);
            await k.Senden("quittung", new JsonObject { ["nr"] = DesktopFormat.KopfLesen(b2).Nr });
            // «neu»: Vollbild ohne Änderung
            await k.Senden("neu");
            var b3 = await k.Binaer();
            Assert.True(DesktopFormat.KopfLesen(b3).Voll);
            await k.Senden("quittung", new JsonObject { ["nr"] = DesktopFormat.KopfLesen(b3).Nr });
            // Fehler: unbekannter Monitor und Typ; Bildschirm gesperrt
            await k.Senden("monitor", new JsonObject { ["id"] = "Z" });
            Assert.Contains("Monitor", (string)(await k.Text("fehler"))["meldung"]!);
            await k.Senden("gibtsnicht");
            Assert.Contains("Unbekannter Typ", (string)(await k.Text("fehler"))["meldung"]!);
            q.Gesperrt = true;
            Assert.Contains("nicht erfassen", (string)(await k.Text("fehler"))["meldung"]!);
            q.Gesperrt = false;
        }
        // kein «start» in der Frist: getrennt
        await using var k2 = await DesktopRohKlient.Verbinden(port, fp, token);
        Assert.Equal(WebSocketCloseStatus.PolicyViolation, await k2.GeschlossenAbwarten());
    }

    [Fact]
    public async Task Ersetzen_zu_viele_Widerruf_Beenden_am_PC_und_Ausschalten()
    {
        var q = new SteuerQuelle();
        await using var s = Server(q, new XrDesktopEinstellungen { StroemeMax = 2 });
        var (port, _, fp) = await s.Aktiv();
        var pa = await s.Paket("QUEST-A");
        var a1 = await DesktopRohKlient.Verbinden(port, fp, XrServer.Token(pa));
        await a1.Senden("start");
        await a1.Text("bildschirm");
        // dieselbe Brille noch einmal: ersetzt die alte (4002)
        await using var a2 = await DesktopRohKlient.Verbinden(port, fp, XrServer.Token(pa));
        await a2.Senden("start");
        await a2.Text("bildschirm");
        Assert.Equal(XrKanal.Ersetzt, await a1.GeschlossenAbwarten());
        await a1.DisposeAsync();
        // zweite und dritte Brille: die dritte ist zu viel (1013)
        var pb = await s.Paket("QUEST-B");
        await using var b = await DesktopRohKlient.Verbinden(port, fp, XrServer.Token(pb));
        await b.Senden("start");
        await b.Text("bildschirm");
        await using var c = await DesktopRohKlient.Verbinden(port, fp, XrServer.Token(await s.Paket("QUEST-C")));
        Assert.Equal((WebSocketCloseStatus)1013, await c.GeschlossenAbwarten());
        Assert.Equal(2, (int)(await Zustand(s))["verbunden"]!);
        // Widerruf der Kopplung B: 4001
        await s.LokalPost("/api/xr/kopplung/widerrufen", new { kopplung = (string)pb["kopplung"]! });
        Assert.Equal(XrKanal.Widerrufen, await b.GeschlossenAbwarten());
        // Beenden am PC: 4010, die App verbindet nicht neu
        var (code, j) = await s.LokalPost("/api/xr/desktop/beenden", new { });
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Equal(1, (int)j!["beendet"]!);
        Assert.Equal(XrDesktopDienst.AmPcBeendet, await a2.GeschlossenAbwarten());
        await Warte(async () => (int)(await Zustand(s))["verbunden"]! == 0, "Ströme bleiben nach Beenden");
        // Ausschalten des Netzzugangs trennt
        await using var d = await DesktopRohKlient.Verbinden(port, fp, XrServer.Token(await s.Paket("QUEST-D")));
        await d.Senden("start");
        await d.Text("bildschirm");
        await s.LokalPost("/api/xr/zugang", new { an = false });
        Assert.Equal(WebSocketCloseStatus.EndpointUnavailable, await d.GeschlossenAbwarten(TimeSpan.FromSeconds(15)));
        await Warte(async () => (int)(await Zustand(s))["verbunden"]! == 0, "Strom bleibt nach dem Ausschalten", 15);
    }

    [Fact]
    public async Task App_Verbindung_dekodiert_quittiert_pausiert_und_endet_bei_Beenden_am_PC()
    {
        var zeit = 0.0;
        var tb = new XrDesktopTestbild(bewegt: true, sekunden: () => zeit);
        await using var s = Server(tb);
        var (port, _, _) = await s.Aktiv();
        var paket = Kopplungspaket.Lesen(new JsonObject { ["paket"] = (await s.Paket("QUEST-APP", "Quest App")).DeepClone() }.ToJsonString());
        var adresse = new PcAdresse("127.0.0.1", port, "pc", "test");
        await using var v = new DesktopVerbindung(paket, _ => Task.FromResult<Uri?>(DesktopRegeln.Adresse(adresse)),
            new DesktopOptionen { Bildrate = 10, Wartezeit = _ => TimeSpan.FromMilliseconds(100) });
        v.Starten();
        await Warte(() => Task.FromResult(v.Puffer is { HatVollbild: true }), "kein Vollbild in der App");
        Assert.Equal(DesktopZustand.Laeuft, v.Zustand);
        Assert.Equal("TESTBILD1", v.Monitor);
        Assert.Equal(2, v.Monitore.Count);
        Assert.Equal((1920, 1080), (v.Puffer!.Raster.Breite, v.Puffer.Raster.Hoehe));
        // die Zeit läuft: Uhr, Zähler und Zeiger ändern sich, die App bekommt im Takt Bilder und quittiert
        var uhr = Stopwatch.StartNew();
        using (var takt = new CancellationTokenSource())
        {
            var tick = Task.Run(async () => { while (!takt.IsCancellationRequested) { zeit = uhr.Elapsed.TotalSeconds; await Task.Delay(5); } });
            var b0 = v.Bilder;
            await Task.Delay(2000);
            var n = v.Bilder - b0;
            var (bps, kbit) = v.Rate();
            aus.WriteLine($"App-Verbindung, Ziel 10 Bilder/s: {n} Bilder in 2 s ({bps:0.0}/s, {kbit:0} kbit/s), dekodieren im Mittel {v.DekodierMsMittel:0.00} ms, höchstens {v.DekodierMsMax:0.0} ms");
            Assert.InRange(n, 12, 24);
            var z = await Zustand(s);
            aus.WriteLine($"PC: {z["stroeme"]![0]!.ToJsonString()}");
            // Pause: keine Bilder mehr; der PC zeigt «pausiert»
            v.Pausieren();
            await Warte(async () => (bool)(await Zustand(s))["stroeme"]![0]!["pausiert"]!, "PC zeigt nicht pausiert");
            await Task.Delay(300);
            var p0 = v.Bilder;
            await Task.Delay(600);
            Assert.InRange(v.Bilder - p0, 0, 1);
            Assert.Equal(DesktopZustand.Pausiert, v.Zustand);
            v.Starten();   // weiter
            await Warte(() => Task.FromResult(v.Bilder > p0 + 2), "nach der Pause keine Bilder");
            // anderer Monitor
            v.MonitorWaehlen("TESTBILD2");
            await Warte(() => Task.FromResult(v.Puffer?.Raster.Breite == 1280 && v.Puffer.HatVollbild), "kein Bild des zweiten Monitors");
            Assert.Equal("TESTBILD2", v.Monitor);
            takt.Cancel();
            await tick;
        }
        // nichts auf der Platte (ausser den Dateien des Zugangs und der Kopplung)
        Assert.DoesNotContain(Directory.GetFiles(s.Ordner, "*", SearchOption.AllDirectories), f => Path.GetFileName(f).Contains("desktop", StringComparison.OrdinalIgnoreCase));
        // Beenden am PC: die App verbindet nicht neu
        await s.LokalPost("/api/xr/desktop/beenden", new { });
        await Warte(() => Task.FromResult(v.Zustand == DesktopZustand.AmPcBeendet), "App nicht «am PC beendet»");
        await Task.Delay(400);
        Assert.Equal(0, (int)(await Zustand(s))["verbunden"]!);
        Assert.Equal(DesktopRegeln.AmPcBeendet, v.LetzterSchluss);
    }

    [Fact]
    public async Task App_Verbindung_ohne_Kopplung_wird_abgewiesen_und_Beenden_trennt_sauber()
    {
        var q = new SteuerQuelle();
        await using var s = Server(q);
        var (port, _, _) = await s.Aktiv();
        var roh = await s.Paket("QUEST-X");
        var paket = Kopplungspaket.Lesen(new JsonObject { ["paket"] = roh.DeepClone() }.ToJsonString());
        var adresse = new PcAdresse("127.0.0.1", port, "pc", "test");
        await s.LokalPost("/api/xr/kopplung/widerrufen", new { kopplung = (string)roh["kopplung"]! });
        await using (var v = new DesktopVerbindung(paket, _ => Task.FromResult<Uri?>(DesktopRegeln.Adresse(adresse)), new DesktopOptionen { Wartezeit = _ => TimeSpan.FromMilliseconds(50) }))
        {
            v.Starten();
            await Warte(() => Task.FromResult(v.Zustand == DesktopZustand.Abgewiesen), "nicht abgewiesen");
            Assert.Null(v.Puffer);
        }
        Assert.Equal(0, q.Erfasst);
        // gekoppelt: verbinden, dann von der App aus beenden – der PC beendet die Erfassung
        var p2 = Kopplungspaket.Lesen(new JsonObject { ["paket"] = (await s.Paket("QUEST-Y")).DeepClone() }.ToJsonString());
        var v2 = new DesktopVerbindung(p2, _ => Task.FromResult<Uri?>(DesktopRegeln.Adresse(adresse)));
        v2.Starten();
        await Warte(() => Task.FromResult(v2.Puffer is { HatVollbild: true }), "kein Bild");
        await v2.Beenden();
        Assert.Equal(DesktopZustand.Beendet, v2.Zustand);
        await Warte(async () => (int)(await Zustand(s))["verbunden"]! == 0, "Strom bleibt");
    }

    [Fact]
    public void Katalog_fuehrt_die_Stellschrauben_von_XR_I_mit_Vorgaben_und_Grenzen_des_Codes_und_KI_verboten()
    {
        var k = XrLaborKatalog.Laden(Katalog.StandardOrdner());
        var e = DesktopEinstellungen.Vorgabe;
        foreach (var key in DesktopEinstellungen.Schluessel)
        {
            var st = k.Stellschraube(key);
            Assert.True(st is not null, key);
            Assert.Equal(DesktopEinstellungen.Lesen(e, key), st!.Vorgabe.GetValue<double>(), 9);
            Assert.Equal(DesktopEinstellungen.Grenzen[key], (st.Min, st.Max));
            Assert.Equal("app", st.Status);
        }
        Assert.Equal("verboten", KiEinordnung.Von("xr_desktop"));
        Assert.Contains("/api/xr/desktop", KiEinordnung.RechnerFunktionen.Single(x => x.Name == "xr_desktop").Grund);
        Assert.DoesNotContain("xr_desktop", KiEinordnung.Erlaubt);
        Assert.DoesNotContain("xr_desktop", Hlks.Mcp.Werkzeugauswahl.Alle());
    }

    [Fact]
    public void Echt_GDI_erfasst_den_Hauptbildschirm_nur_auf_ausdruecklichen_Wunsch()
    {
        // nur mit HLKS_XR_DESKTOP_ECHT=1 unter Windows: misst Erfassen und Kodieren des echten Bildschirms; nichts wird gespeichert oder ausgegeben ausser Zahlen
        if (Environment.GetEnvironmentVariable("HLKS_XR_DESKTOP_ECHT") != "1" || !OperatingSystem.IsWindows()) { aus.WriteLine("Echt-Test übersprungen (HLKS_XR_DESKTOP_ECHT=1 setzen)."); return; }
        using var gdi = new XrDesktopGdi();
        var liste = gdi.Bildschirme();
        Assert.NotEmpty(liste);
        var m = liste.First(x => x.Haupt);
        aus.WriteLine($"Monitore: {string.Join("; ", liste.Select(x => $"{x.Name} {x.Breite} × {x.Hoehe} bei {x.X}/{x.Y}, {x.Dpi} dpi{(x.Haupt ? ", Haupt" : "")}"))}");
        var (zb, zh) = XrDesktopQuellen.Ziel(m.Breite, m.Hoehe, 1920);
        var puffer = new byte[zb * zh * 4];
        var k = new XrDesktopKodierer();
        var zeiten = new List<double>();
        var groessen = new List<int>();
        for (int i = 0; i < 12; i++)
        {
            var t0 = Stopwatch.GetTimestamp();
            Assert.True(gdi.Erfassen(m.Id, zb, zh, puffer));
            zeiten.Add(Stopwatch.GetElapsedTime(t0).TotalMilliseconds);
            if (k.Kodieren(puffer, zb, zh, i) is { } b) groessen.Add(b.Daten.Length);
        }
        var verschieden = Enumerable.Range(0, 1000).Select(i => puffer[(i * 7919 % (zb * zh)) * 4 + 1]).Distinct().Count();
        aus.WriteLine($"GDI {m.Breite} × {m.Hoehe} → {zb} × {zh}: Erfassen im Mittel {zeiten.Skip(1).Average():0.0} ms (höchstens {zeiten.Max():0.0}), Vollbild {groessen[0] / 1024.0:0} KB, verschiedene Werte {verschieden}");
        Assert.True(verschieden > 1, "Bild einfarbig – keine Sitzung mit Bildschirm?");
        // zum Vergleich: in voller Grösse erfassen (BitBlt) und im Programm verkleinern
        var voll = new byte[m.Breite * m.Hoehe * 4];
        var tv = new List<double>();
        var tk = new List<double>();
        for (int i = 0; i < 6; i++)
        {
            var t0 = Stopwatch.GetTimestamp();
            Assert.True(gdi.Erfassen(m.Id, m.Breite, m.Hoehe, voll));
            tv.Add(Stopwatch.GetElapsedTime(t0).TotalMilliseconds);
            t0 = Stopwatch.GetTimestamp();
            XrDesktopQuellen.Verkleinern(voll, m.Breite, m.Hoehe, puffer, zb, zh);
            tk.Add(Stopwatch.GetElapsedTime(t0).TotalMilliseconds);
        }
        aus.WriteLine($"Vergleich: BitBlt in voller Grösse {tv.Skip(1).Average():0.0} ms, im Programm verkleinern {tk.Skip(1).Average():0.0} ms");
        Array.Clear(puffer);
        Array.Clear(voll);
    }
}
