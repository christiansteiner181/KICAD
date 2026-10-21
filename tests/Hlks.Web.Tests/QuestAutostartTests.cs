using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Hlks.Web.Tests;

/// <summary>
/// «Begehung beim Aufsetzen automatisch starten» (Modul XR, QuestStarter.cs). Statt adb.exe die Attrappe tests/Hlks.AdbAttrappe, statt des Browsers der Brille ein
/// kleiner CDP-WebSocket-Server (CdpAttrappe, Kestrel auf 127.0.0.1, freier Port; KICAD erreicht ihn über die Naht QuestEinstellungen.CdpPortWahl). Nie ein echtes adb,
/// nie die echte Brille.
/// </summary>
public partial class QuestTests
{
    const int AutoPort = 5731;   // nur der Host-Kopf der Testanfragen; es lauscht niemand darauf

    /// <summary>Die einzigen Ausdrücke, die KICAD im Browser der Brille auswerten darf (bewusst als eigene Kopie der Literale: ändert jemand den Code, fällt es hier auf).</summary>
    static readonly string[] FesteAusdruecke =
    [
        "document.querySelector('#vr-eintreten')?.dataset.status ?? ''",
        "document.querySelector('#vr-eintreten')?.click()",
        "(() => { const b = document.querySelector('#vr-eintreten'); if (!b) return null; const r = b.getBoundingClientRect(); return { x: r.left + r.width / 2, y: r.top + r.height / 2 }; })()",
    ];

    sealed class Tab(string id, string url, string typ = "page") { public string Id { get; } = id; public string Url { get; set; } = url; public string Typ { get; } = typ; }

    /// <summary>Der Browser der Brille: beantwortet Target.getTargets, attachToTarget, Runtime.evaluate, Page.navigate und Input.dispatchMouseEvent und protokolliert alles.</summary>
    sealed class CdpAttrappe : IDisposable
    {
        readonly WebApplication app;
        readonly object schloss = new();
        public int Port { get; private set; }
        public List<Tab> Tabs { get; } = [];
        public List<string> Log { get; } = [];
        public List<string> Angehaengt { get; } = [];
        public string Status { get; set; } = "bereit";
        public int LaedtNoch { get; set; }
        public bool Haengt { get; set; }
        public bool KlickWirkt { get; set; } = true;
        public bool MausWirkt { get; set; } = true;

        public CdpAttrappe()
        {
            var b = WebApplication.CreateSlimBuilder();
            b.WebHost.UseUrls("http://127.0.0.1:0");
            app = b.Build();
            app.UseWebSockets();
            app.Map("/devtools/browser", async ctx =>
            {
                if (!ctx.WebSockets.IsWebSocketRequest) { ctx.Response.StatusCode = 400; return; }
                using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
                await Schleife(ws, ctx.RequestAborted);
            });
            app.StartAsync().GetAwaiter().GetResult();
            Port = new Uri(app.Urls.First()).Port;
        }

        public void Dispose() { try { app.StopAsync().GetAwaiter().GetResult(); } catch { /* egal */ } }

        public string[] Eintraege() { lock (schloss) return [.. Log]; }
        public int Zahl(string anfang) { lock (schloss) return Log.Count(z => z.StartsWith(anfang, StringComparison.Ordinal)); }

        async Task Schleife(WebSocket ws, CancellationToken abbruch)
        {
            var puffer = new byte[64 * 1024];
            try
            {
                while (ws.State == WebSocketState.Open)
                {
                    using var ms = new MemoryStream();
                    WebSocketReceiveResult r;
                    do { r = await ws.ReceiveAsync(puffer, abbruch); if (r.MessageType == WebSocketMessageType.Close) return; ms.Write(puffer, 0, r.Count); } while (!r.EndOfMessage);
                    var m = JsonNode.Parse(Encoding.UTF8.GetString(ms.ToArray()))!.AsObject();
                    var antwort = Verarbeiten(m);
                    if (antwort is not null) await ws.SendAsync(Encoding.UTF8.GetBytes(antwort.ToJsonString()), WebSocketMessageType.Text, true, abbruch);
                }
            }
            catch (Exception ex) when (ex is WebSocketException or OperationCanceledException) { /* Gegenstelle zu */ }
        }

        JsonObject Ergebnis(JsonObject m, JsonNode? result)
        {
            var o = new JsonObject { ["id"] = m["id"]!.GetValue<int>(), ["result"] = result };
            if (m["sessionId"] is JsonNode s) o["sessionId"] = s.GetValue<string>();
            return o;
        }

        JsonObject Fehler(JsonObject m, string text) => new() { ["id"] = m["id"]!.GetValue<int>(), ["error"] = new JsonObject { ["code"] = -32000, ["message"] = text } };

        JsonObject? Verarbeiten(JsonObject m)
        {
            var methode = m["method"]!.GetValue<string>();
            var par = m["params"] as JsonObject;
            var sitzung = m["sessionId"]?.GetValue<string>();
            lock (schloss)
            {
                switch (methode)
                {
                    case "Target.getTargets":
                        Log.Add("Target.getTargets");
                        return Ergebnis(m, new JsonObject { ["targetInfos"] = new JsonArray([.. Tabs.Select(t => (JsonNode)new JsonObject { ["targetId"] = t.Id, ["type"] = t.Typ, ["url"] = t.Url, ["title"] = "" })]) });
                    case "Target.attachToTarget":
                    {
                        var id = par!["targetId"]!.GetValue<string>();
                        Log.Add("Target.attachToTarget:" + id);
                        Angehaengt.Add(id);
                        if (par["flatten"]?.GetValue<bool>() != true) return Fehler(m, "flatten fehlt");
                        return Ergebnis(m, new JsonObject { ["sessionId"] = "S-" + id });
                    }
                    case "Page.navigate":
                    {
                        var url = par!["url"]!.GetValue<string>();
                        Log.Add("Page.navigate:" + url);
                        if (Haengt) return null;
                        var tab = Tabs.FirstOrDefault(t => "S-" + t.Id == sitzung);
                        if (tab is null) return Fehler(m, "Sitzung unbekannt");
                        tab.Url = url;
                        return Ergebnis(m, new JsonObject { ["frameId"] = tab.Id });
                    }
                    case "Runtime.evaluate":
                    {
                        var ausdruck = par!["expression"]!.GetValue<string>();
                        var geste = par["userGesture"]?.GetValue<bool>() == true;
                        if (!FesteAusdruecke.Contains(ausdruck)) { Log.Add("UNERLAUBT:" + ausdruck); return Fehler(m, "nicht erlaubt"); }
                        Log.Add($"evaluate:{Array.IndexOf(FesteAusdruecke, ausdruck)}|geste={geste}");
                        if (Haengt) return null;
                        if (ausdruck == FesteAusdruecke[0])
                        {
                            var s = LaedtNoch > 0 ? "laedt" : Status;
                            if (LaedtNoch > 0) LaedtNoch--;
                            return Ergebnis(m, new JsonObject { ["result"] = new JsonObject { ["type"] = "string", ["value"] = s } });
                        }
                        if (ausdruck == FesteAusdruecke[1])
                        {
                            if (KlickWirkt && geste) Status = "aktiv";
                            return Ergebnis(m, new JsonObject { ["result"] = new JsonObject { ["type"] = "undefined" } });
                        }
                        return Ergebnis(m, new JsonObject { ["result"] = new JsonObject { ["type"] = "object", ["value"] = new JsonObject { ["x"] = 100.5, ["y"] = 50 } } });
                    }
                    case "Input.dispatchMouseEvent":
                        Log.Add($"mouse:{par!["type"]!.GetValue<string>()}:{par["x"]!.GetValue<double>()}:{par["y"]!.GetValue<double>()}");
                        if (MausWirkt && par["type"]!.GetValue<string>() == "mouseReleased") Status = "aktiv";
                        return Ergebnis(m, new JsonObject());
                    default:
                        Log.Add("UNERLAUBT:" + methode);
                        return Fehler(m, "unbekannt");
                }
            }
        }
    }

    // ---------------------------------------------------------------- Hilfen

    sealed record Auto(Server S, Adb Adb, CdpAttrappe Cdp);

    /// <summary>Server, adb-Attrappe mit einer USB-Quest (Weiterleitung für den Server-Port steht schon) und CDP-Attrappe mit einem /vr-Tab.</summary>
    async Task<Auto> AutoStart(Action<Adb>? adbEinrichten = null, Action<CdpAttrappe>? cdpEinrichten = null, Func<QuestEinstellungen, QuestEinstellungen>? anp = null)
    {
        var cdp = new CdpAttrappe();
        aufraeumen.Add(cdp);
        cdp.Tabs.Add(new Tab("tab-vr", $"http://localhost:{AutoPort}/vr"));
        cdpEinrichten?.Invoke(cdp);
        var adb = NeueAttrappe().Geraete(Usb).Reverse($"UsbFfs tcp:{AutoPort} tcp:{AutoPort}");
        adbEinrichten?.Invoke(adb);
        var q0 = Ohne() with
        {
            AutostartGrenze = TimeSpan.FromSeconds(20), AutostartAbstand = TimeSpan.FromMilliseconds(30), CdpZeitlimit = TimeSpan.FromSeconds(1),
            BereitGrenze = TimeSpan.FromSeconds(3), KlickGrenze = TimeSpan.FromMilliseconds(400), CdpPortWahl = () => cdp.Port,
        };
        var s = await Start(adb, anp is null ? q0 : anp(q0), AutoPort);
        return new Auto(s, adb, cdp);
    }

    static Task<(HttpStatusCode Code, JsonNode? Json)> Senden(Auto a, bool autostart = true) =>
        a.S.Post("/api/vr/auftrag", autostart ? new { modus = "sitzungsraum", autostart = true } : new { modus = "sitzungsraum" });

    static async Task<JsonNode> BisEnde(Auto a, int sekunden = 30)
    {
        var uhr = Stopwatch.StartNew();
        while (true)
        {
            var (_, j) = await a.S.Get("/api/quest/autostart");
            if (j!["status"]!.GetValue<string>() != "laeuft") return j;
            if (uhr.Elapsed.TotalSeconds > sekunden) Assert.Fail("Der Autostart ist nicht fertig geworden: " + j.ToJsonString());
            await Task.Delay(40);
        }
    }

    static string[] Zeilen(Adb adb, string datei)
    {
        var d = Path.Combine(adb.Ordner, datei);
        return File.Exists(d) ? File.ReadAllLines(d).Where(z => z.Length > 0).ToArray() : [];
    }

    static void KeinUnerlaubtes(CdpAttrappe cdp) => Assert.DoesNotContain(cdp.Eintraege(), z => z.StartsWith("UNERLAUBT", StringComparison.Ordinal));

    // ---------------------------------------------------------------- Ablauf

    [Fact]
    public async Task Autostart_voller_Ablauf_wartet_aufs_Aufsetzen_und_klickt_genau_einmal()
    {
        var a = await AutoStart(adb => adb.Wach(3), cdp =>
        {
            cdp.Tabs.Insert(0, new Tab("tab-fremd", "https://example.com/"));
            cdp.Tabs.Add(new Tab("tab-ahnlich", $"http://localhost:{AutoPort}/vrx"));
            cdp.Tabs.Add(new Tab("tab-anderer-port", $"http://localhost:{AutoPort + 1}/vr"));
            cdp.Tabs.Add(new Tab("sw", $"http://localhost:{AutoPort}/vr", "service_worker"));
            cdp.LaedtNoch = 2;
        });
        var (code, j) = await Senden(a);
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Equal("gestartet", j!["autostart"]!.GetValue<string>());
        var (_, unterwegs) = await a.S.Get("/api/quest/autostart");
        Assert.Equal("laeuft", unterwegs!["status"]!.GetValue<string>());
        Assert.Equal(1, unterwegs["nr"]!.GetValue<long>());

        var ende = await BisEnde(a);
        Assert.Equal("aktiv", ende["status"]!.GetValue<string>());

        // der Browser wird mit genau diesen Argumenten geöffnet
        Assert.Equal([$"http://localhost:{AutoPort}/vr"], Zeilen(a.Adb, "am_start.log"));
        Assert.Contains($"-s {Seriennummer} shell am start -a android.intent.action.VIEW -d http://localhost:{AutoPort}/vr com.oculus.browser", a.Adb.Aufrufe());
        // gewartet, solange Asleep: mindestens 3 Abfragen davor + eine, die Awake meldet
        Assert.True(a.Adb.Aufrufe().Count(z => z.EndsWith("shell dumpsys power")) >= 4);
        Assert.Contains(a.Adb.Aufrufe(), z => z.EndsWith("shell dumpsys activity activities"));
        // Fern-Debugging nur für die Dauer: gesetzt auf den Port der Attrappe und wieder entfernt
        Assert.Contains($"-s {Seriennummer} forward tcp:{a.Cdp.Port} localabstract:chrome_devtools_remote", a.Adb.Aufrufe());
        Assert.Contains($"-s {Seriennummer} forward --remove tcp:{a.Cdp.Port}", a.Adb.Aufrufe());
        Assert.Empty(Zeilen(a.Adb, "forward.txt"));
        // nur der /vr-Tab wird angehängt; fremde, ähnliche und Nicht-Seiten-Ziele nie
        Assert.Equal(["tab-vr"], a.Cdp.Angehaengt.Distinct());
        Assert.Equal(0, a.Cdp.Zahl("Page.navigate"));
        // genau ein Klick-Ausdruck, als Benutzerhandlung
        Assert.Equal(1, a.Cdp.Zahl("evaluate:1|"));
        Assert.Contains("evaluate:1|geste=True", a.Cdp.Eintraege());
        Assert.Equal(0, a.Cdp.Zahl("mouse:"));
        KeinUnerlaubtes(a.Cdp);
        NurFesteAufrufe(a.Adb);
    }

    [Fact]
    public async Task Autostart_ohne_vr_Tab_navigiert_einmal_auf_genau_die_Adresse()
    {
        var a = await AutoStart(cdpEinrichten: cdp => { cdp.Tabs.Clear(); cdp.Tabs.Add(new Tab("tab-fremd", "https://example.com/")); });
        await Senden(a);
        Assert.Equal("aktiv", (await BisEnde(a))["status"]!.GetValue<string>());
        Assert.Equal([$"Page.navigate:http://localhost:{AutoPort}/vr"], a.Cdp.Eintraege().Where(z => z.StartsWith("Page.navigate")).ToArray());
        Assert.Equal(1, a.Cdp.Zahl("evaluate:1|"));
        KeinUnerlaubtes(a.Cdp);
    }

    [Fact]
    public async Task Autostart_Status_schon_aktiv_klickt_nicht()
    {
        var a = await AutoStart(cdpEinrichten: cdp => cdp.Status = "aktiv");
        await Senden(a);
        Assert.Equal("aktiv", (await BisEnde(a))["status"]!.GetValue<string>());
        Assert.Equal(0, a.Cdp.Zahl("evaluate:1|"));
        Assert.Equal(0, a.Cdp.Zahl("mouse:"));
    }

    [Fact]
    public async Task Autostart_zweiter_Versuch_per_Mausklick_in_der_Mitte_des_Knopfs()
    {
        var a = await AutoStart(cdpEinrichten: cdp => cdp.KlickWirkt = false);
        await Senden(a);
        Assert.Equal("aktiv", (await BisEnde(a))["status"]!.GetValue<string>());
        Assert.Equal(1, a.Cdp.Zahl("evaluate:1|"));
        Assert.Equal(["mouse:mousePressed:100.5:50", "mouse:mouseReleased:100.5:50"], a.Cdp.Eintraege().Where(z => z.StartsWith("mouse:")).ToArray());
        KeinUnerlaubtes(a.Cdp);
    }

    [Fact]
    public async Task Autostart_ohne_Wirkung_meldet_Fehler_mit_Hinweis_auf_VR_erlauben()
    {
        var a = await AutoStart(cdpEinrichten: cdp => { cdp.KlickWirkt = false; cdp.MausWirkt = false; });
        await Senden(a);
        var ende = await BisEnde(a);
        Assert.Equal("fehler", ende["status"]!.GetValue<string>());
        Assert.Contains("nicht gestartet", ende["meldung"]!.GetValue<string>());
        Assert.Contains("VR erlauben", ende["hinweis"]!.GetValue<string>());
        Assert.Empty(Zeilen(a.Adb, "forward.txt"));   // auch beim Fehler aufgeräumt
    }

    // ---------------------------------------------------------------- Zeitgrenzen

    [Fact]
    public async Task Autostart_Zeitgrenze_beim_Aufsetzen_bricht_ab_ohne_Fern_Debugging()
    {
        var a = await AutoStart(adb => adb.WachDauer("Asleep"), anp: q => q with { AutostartGrenze = TimeSpan.FromMilliseconds(500) });
        await Senden(a, true);
        var ende = await BisEnde(a);
        Assert.Equal("fehler", ende["status"]!.GetValue<string>());
        Assert.Contains("nicht aufgesetzt", ende["meldung"]!.GetValue<string>());
        Assert.DoesNotContain(a.Adb.Aufrufe(), z => z.Contains(" forward "));
        Assert.Equal(0, a.Cdp.Zahl("Target."));
    }

    [Fact]
    public async Task Autostart_wartet_auch_dann_wenn_der_Browser_nicht_vorne_ist()
    {
        var a = await AutoStart(adb => File.WriteAllText(Path.Combine(adb.Ordner, "vorne.txt"), "com.oculus.vrshell"), anp: q => q with { AutostartGrenze = TimeSpan.FromMilliseconds(500) });
        await Senden(a);
        var ende = await BisEnde(a);
        Assert.Equal("fehler", ende["status"]!.GetValue<string>());
        Assert.Contains("Browser nicht vorne", ende["meldung"]!.GetValue<string>());
        Assert.Equal(0, a.Cdp.Zahl("evaluate:"));
    }

    [Fact]
    public async Task Autostart_CDP_antwortet_nicht_Zeitgrenze_und_Weiterleitung_wird_entfernt()
    {
        var a = await AutoStart(cdpEinrichten: cdp => cdp.Haengt = true,
            anp: q => q with { CdpZeitlimit = TimeSpan.FromMilliseconds(200), BereitGrenze = TimeSpan.FromMilliseconds(900) });
        await Senden(a);
        var ende = await BisEnde(a);
        Assert.Equal("fehler", ende["status"]!.GetValue<string>());
        Assert.Contains("nicht bereit", ende["meldung"]!.GetValue<string>());
        Assert.Contains($"-s {Seriennummer} forward --remove tcp:{a.Cdp.Port}", a.Adb.Aufrufe());
        Assert.Empty(Zeilen(a.Adb, "forward.txt"));
        Assert.Equal(0, a.Cdp.Zahl("evaluate:1|"));   // kein Klick ins Leere
    }

    // ---------------------------------------------------------------- Abbruch, Einstellung, Sicherheit

    [Fact]
    public async Task Autostart_neuer_Auftrag_bricht_den_alten_ab()
    {
        var a = await AutoStart(adb => adb.WachDauer("Asleep"), anp: q => q with { AutostartGrenze = TimeSpan.FromSeconds(60) });
        await Senden(a);
        var uhr = Stopwatch.StartNew();
        while (Zeilen(a.Adb, "am_start.log").Length < 1) { Assert.True(uhr.Elapsed.TotalSeconds < 20); await Task.Delay(30); }
        var (_, zweiter) = await Senden(a);
        Assert.Equal("gestartet", zweiter!["autostart"]!.GetValue<string>());
        while (Zeilen(a.Adb, "am_start.log").Length < 2) { Assert.True(uhr.Elapsed.TotalSeconds < 20); await Task.Delay(30); }
        a.Adb.WachDauer("Awake");   // jetzt wird die Brille aufgesetzt: nur der neue Lauf klickt
        var ende = await BisEnde(a);
        Assert.Equal("aktiv", ende["status"]!.GetValue<string>());
        Assert.Equal(2, ende["nr"]!.GetValue<long>());
        Assert.Equal(2, Zeilen(a.Adb, "am_start.log").Length);
        Assert.Equal(1, a.Cdp.Zahl("evaluate:1|"));
        Assert.Empty(Zeilen(a.Adb, "forward.txt"));
    }

    [Fact]
    public async Task Autostart_ausgeschaltet_oder_nicht_angefordert_tut_nichts()
    {
        var a = await AutoStart();
        // ohne Wunsch im Auftrag
        var (_, ohne) = await Senden(a, false);
        Assert.Equal("nicht_angefordert", ohne!["autostart"]!.GetValue<string>());
        // Einstellung aus
        var (c, _) = await a.S.Post("/api/benutzer/einstellungen", new { schluessel = "quest.autostart", wert = false });
        Assert.Equal(HttpStatusCode.OK, c);
        var (_, aus) = await Senden(a);
        Assert.Equal("aus", aus!["autostart"]!.GetValue<string>());
        var (_, z) = await a.S.Get("/api/quest/autostart");
        Assert.Equal("leer", z!["status"]!.GetValue<string>());
        Assert.False(z["autostart"]!.GetValue<bool>());
        Assert.Empty(a.Adb.Aufrufe());
        Assert.Empty(a.Cdp.Eintraege());
    }

    [Fact]
    public async Task Autostart_Einstellung_ist_vorgabe_an_und_prueft_den_Wert()
    {
        var a = await AutoStart();
        var (_, j) = await a.S.Get();
        Assert.True(j!["autostart"]!.GetValue<bool>());
        var (code, _) = await a.S.Post("/api/benutzer/einstellungen", new { schluessel = "quest.autostart", wert = "vielleicht" });
        Assert.Equal(HttpStatusCode.BadRequest, code);
        await a.S.Post("/api/benutzer/einstellungen", new { schluessel = "quest.autostart", wert = false });
        var (_, k) = await a.S.Get();
        Assert.False(k!["autostart"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Autostart_ohne_adb_meldet_Fehler_und_laeuft_nicht_weiter()
    {
        var a = await AutoStart();
        await a.S.AdbSetzen(null);
        await Senden(a);
        var ende = await BisEnde(a);
        Assert.Equal("fehler", ende["status"]!.GetValue<string>());
        Assert.Contains("adb", ende["meldung"]!.GetValue<string>());
        Assert.Empty(a.Cdp.Eintraege());
    }

    [Fact]
    public void Autostart_Ausdruecke_sind_fest_und_Ausgaben_werden_streng_gelesen()
    {
        Assert.Equal(FesteAusdruecke.AsEnumerable(), new[] { QuestStarter.Ausdruecke.Lesen, QuestStarter.Ausdruecke.Klick, QuestStarter.Ausdruecke.Mitte });
        Assert.True(QuestStarter.WachLesen("Power Manager State:\n  mWakefulness=Awake\n"));
        Assert.False(QuestStarter.WachLesen("  mWakefulness=Asleep"));
        Assert.False(QuestStarter.WachLesen("  mWakefulness=Dozing"));
        Assert.False(QuestStarter.WachLesen("  mWakefulnessChanging=false"));
        Assert.False(QuestStarter.WachLesen("xmWakefulness=Awake und mehr"));
        Assert.False(QuestStarter.WachLesen(null));
        Assert.True(QuestStarter.BrowserVorne("  mResumedActivity: ActivityRecord{1a2b u0 com.oculus.browser/.Main t7}"));
        Assert.True(QuestStarter.BrowserVorne("  topResumedActivity=ActivityRecord{1a2b u0 com.oculus.browser/com.oculus.browser.Main t7}"));
        Assert.False(QuestStarter.BrowserVorne("  mResumedActivity: ActivityRecord{1a2b u0 com.oculus.vrshell/.Main t7}"));
        Assert.False(QuestStarter.BrowserVorne("  mResumedActivity: ActivityRecord{1a2b u0 com.oculus.browser.evil/.Main t7}"));
        Assert.False(QuestStarter.BrowserVorne("  mFocusedApp=ActivityRecord{1a2b u0 com.oculus.browser/.Main t7}"));   // nur der fortgesetzte Eintrag zählt
        Assert.False(QuestStarter.BrowserVorne("  mResumedActivity: ActivityRecord{1a2b u0 com.oculus.browser/.Main t7}\n  topResumedActivity=ActivityRecord{1a2b u0 com.oculus.vrshell/.Main t7}"));
        Assert.False(QuestStarter.BrowserVorne(""));
    }

    [Fact]
    public void KI_Einordnung_Quest_Autostart_ist_verboten_und_es_gibt_kein_MCP_Werkzeug()
    {
        Assert.Equal("verboten", KiEinordnung.Von("quest_autostart"));
        var f = Assert.Single(KiEinordnung.RechnerFunktionen, x => x.Name == "quest_autostart");
        Assert.Contains("Browser", f.Grund);
        Assert.DoesNotContain("quest_autostart", KiEinordnung.Erlaubt);
        Assert.DoesNotContain("quest_autostart", AuftragDienst.ErlaubteWerkzeuge);
        Assert.DoesNotContain(Hlks.Mcp.Werkzeugauswahl.Alle(), w => w.Contains("autostart", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("quest_autostart", GespraechDienst.McpWerkzeuge(new GespraechEinstellungen(), true));
    }
}
