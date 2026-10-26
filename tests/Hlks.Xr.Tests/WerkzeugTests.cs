// H+K XR E1 (entwurf/xr-empfaenger.md, «Empfänger-Kern», «Schnittstellen im Bau E1»): eine App «H+K XR» (ch.hkplanung.xr) für alle Werkzeuge. Werkzeuge und
// App (Hlks.Xr.Netz.Werkzeug, XrApp; die Übergangsfassade AppMarke), Regeln der Wahl (WerkzeugWahl), Lage der Startseite (Bedienung.Startseite), «hallo.aktiv» und
// «aktiv { aktiv }» im Kanal (gegen einen kleinen WebSocket-Server), die Projektdateien der App (Kennung, Name, keine zweite APK) und die Grenze zwischen
// Empfänger-Kern und Werkzeug-Modul «Labor» in src/Hlks.Quest (Einhängepunkte; der Probebau ohne Labor-Dateien ist -p:HlksOhneLabor=true).
// Bis XR-Labor L1: AppMarkeTests.

using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Hlks.Xr.Bedienung;
using Hlks.Xr.Netz;
using static Hlks.Xr.Tests.T;

namespace Hlks.Xr.Tests;

public class WerkzeugTests
{
    static string Quest => Path.Combine(Repo(), "src", "Hlks.Quest");

    /// <summary>Die Dateien des Werkzeug-Moduls «Labor» in src/Hlks.Quest – dieselbe Liste wie HlksOhneLabor in Hlks.Quest.csproj.</summary>
    static readonly string[] LaborDateien = ["QuestApp.Labor.cs", "QuestApp.LaborPruefen.cs", "AufgabenKarte.cs"];

    // ---------------------------------------------------------------------------------------------- Werkzeuge und App

    [Fact]
    public void Werkzeuge_KICAD_und_XR_Labor()
    {
        var k = Werkzeug.Kicad;
        Assert.Equal(("kicad", "KICAD", "in KICAD", "status"), (k.Kennung, k.Name, k.InWerkzeug, k.FarbId));
        Assert.Equal("in KICAD «Quest verbinden» › «App installieren/aktualisieren»", k.Koppeln);   // wie die KICAD-App bis E1
        var l = Werkzeug.XrLabor;
        Assert.Equal(("xrlabor", "XR-Labor", "im XR-Labor", "labor"), (l.Kennung, l.Name, l.InWerkzeug, l.FarbId));   // Entscheid Christian 26.10.2026
        // L1: nur KICAD führt ein Projekt (Ebenen, Problem-Marker); Werkzeug-Modul «Labor» mit beiden (KICAD bis E3: P5b-Labor in KICAD)
        Assert.True(k.MitProjekt);
        Assert.False(l.MitProjekt);
        Assert.True(k.MitLabor);
        Assert.True(l.MitLabor);
        // «hallo.marke» liest der PC streng (Kleinbuchstaben und Ziffern, bis 16 Zeichen)
        Assert.Equal("kicad", k.HalloMarke);
        Assert.Equal("xrlabor", l.HalloMarke);
        Assert.Null(new Werkzeug("hk-ap", "HKAP").HalloMarke);
        Assert.Same(k, Werkzeug.Von("kicad"));
        Assert.Same(l, Werkzeug.Von("xrlabor"));
        Assert.Null(Werkzeug.Von("hkap"));
        Assert.Null(Werkzeug.Von(null));
        Assert.Equal([k, l], Werkzeug.Bekannt);
    }

    [Fact]
    public void Werkzeug_aus_Kopplungspaket_bekannt_und_unbekannt()
    {
        Assert.Same(Werkzeug.Kicad, Werkzeug.Aus("kicad", "KICAD"));
        Assert.Same(Werkzeug.Kicad, Werkzeug.Aus("kicad", null));
        // der PC weiss, wie er heisst: ein anderer Name aus dem Paket, die Eigenschaften bleiben
        var umbenannt = Werkzeug.Aus("xrlabor", "XR-Labor Büro");
        Assert.Equal(("xrlabor", "XR-Labor Büro", false, true), (umbenannt.Kennung, umbenannt.Name, umbenannt.MitProjekt, umbenannt.MitLabor));
        // unbekannt: allgemein, ohne Projekt und Labor
        var h = Werkzeug.Aus("hkap", "HKAP");
        Assert.Equal(("hkap", "HKAP", false, false, "vorgabe", "in «HKAP»"), (h.Kennung, h.Name, h.MitProjekt, h.MitLabor, h.FarbId, h.InWerkzeug));
        Assert.Contains("H+K XR", h.Koppeln);
        Assert.Equal("hkap", Werkzeug.Aus("hkap", " ").Name);
        // Paket der Fassung 1: KICAD
        Assert.Same(Werkzeug.Kicad, Werkzeug.Aus(NetzHilfen.Paket()));
        Assert.Same(Werkzeug.XrLabor, Werkzeug.Aus(KopplungspaketTests.Paket2("xrlabor", "XR-Labor")));
    }

    [Theory]
    [InlineData("kicad", true)]
    [InlineData("xrlabor", true)]
    [InlineData("hk-ap_2", true)]
    [InlineData("Kicad", false)]
    [InlineData("1kicad", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("k/../x", false)]
    [InlineData("abcdefghijklmnopqrstuvwxyz0123456", false)]   // 33 Zeichen
    public void Kennung_des_Werkzeugs_wie_am_PC(string? kennung, bool gueltig) => Assert.Equal(gueltig, Werkzeug.KennungGueltig(kennung));

    [Fact]
    public void Reihenfolge_bekannte_zuerst_dann_nach_Kennung()
    {
        Assert.Equal(["kicad", "xrlabor", "aaa", "hkap"], Werkzeug.Ordnen(["hkap", "xrlabor", "aaa", "kicad"]));
        Assert.Equal(0, Werkzeug.Rang("kicad"));
        Assert.Equal(Werkzeug.Bekannt.Count, Werkzeug.Rang("hkap"));
    }

    [Fact]
    public void Eine_App_fuer_alle_Werkzeuge()
    {
        Assert.Equal(("ch.hkplanung.xr", "H+K XR", "hk_xr"), (XrApp.Kennung, XrApp.Name, XrApp.Speicher));   // Entscheid Christian 26.10.2026
        Assert.Equal("/sdcard/Android/data/ch.hkplanung.xr/files", XrApp.Eingang);
        // je Werkzeug ein Paket im Eingang (wie am PC, QuestAppMarke.KopplungEingang)
        Assert.Equal("/sdcard/Android/data/ch.hkplanung.xr/files/kopplung_kicad.json", XrApp.KopplungEingang("kicad"));
        Assert.Equal("/sdcard/Android/data/ch.hkplanung.xr/files/kopplung_xrlabor.json", XrApp.KopplungEingang("xrlabor"));
    }

    [Fact]
    public void AppMarke_bleibt_als_Uebergang_mit_der_einen_App()
    {
        // tests/Hlks.Labor.Tests vergleicht Id, Name und Kennung mit dem Labor-Server (LaborMarke): seit E1 dieselbe App für beide Werkzeuge
        Assert.Equal(("kicad", "KICAD", XrApp.Kennung), (AppMarke.Kicad.Id, AppMarke.Kicad.Name, AppMarke.Kicad.Kennung));
        Assert.Equal(("xrlabor", "XR-Labor", XrApp.Kennung), (AppMarke.XrLabor.Id, AppMarke.XrLabor.Name, AppMarke.XrLabor.Kennung));
    }

    // ---------------------------------------------------------------------------------------------- Wahl

    [Fact]
    public void Start_mit_einer_Kopplung_direkt_sonst_Startseite()
    {
        Assert.Equal("kicad", WerkzeugWahl.Start(["kicad"]));        // wie die KICAD-App bis E1 (auch ohne PC)
        Assert.Equal("xrlabor", WerkzeugWahl.Start(["xrlabor"]));
        Assert.Null(WerkzeugWahl.Start(["kicad", "xrlabor"]));       // Startseite
        Assert.Null(WerkzeugWahl.Start([]));
        Assert.Equal("xrlabor", WerkzeugWahl.Start(["kicad", "xrlabor"], "xrlabor"));   // am PC --werkzeug
        Assert.Null(WerkzeugWahl.Start(["kicad", "xrlabor"], "hkap"));                 // nicht gekoppelt: Startseite
        Assert.Equal("kicad", WerkzeugWahl.Start(["kicad"], "hkap"));
    }

    [Fact]
    public void Auftrag_holt_die_App_zum_Werkzeug()
    {
        static JsonElement J(string j) => JsonDocument.Parse(j).RootElement.Clone();
        Assert.True(WerkzeugWahl.IstAuftrag("labor.aufgabe", J("\"drehen-90\"")));
        Assert.False(WerkzeugWahl.IstAuftrag("labor.aufgabe", J("null")));    // beenden holt nicht
        Assert.False(WerkzeugWahl.IstAuftrag("labor.aufgabe", J("\"\"")));
        Assert.False(WerkzeugWahl.IstAuftrag("labor.aufgabe", default));
        Assert.False(WerkzeugWahl.IstAuftrag("anzeige.skala", J("1.2")));
        Assert.False(WerkzeugWahl.IstAuftrag("labor.variante.teleport", J("\"stick\"")));
        Assert.True(WerkzeugWahl.Wechseln("kicad", "xrlabor"));
        Assert.True(WerkzeugWahl.Wechseln(null, "kicad"));                    // Startseite
        Assert.False(WerkzeugWahl.Wechseln("kicad", "kicad"));
        Assert.Equal("Wechsel zu XR-Labor …", WerkzeugWahl.Meldung(Werkzeug.XrLabor));
        Assert.Equal("Wechsel zu KICAD …", WerkzeugWahl.Meldung(Werkzeug.Kicad));
    }

    [Fact]
    public void Zeilen_und_Ampel_der_Startseite()
    {
        Assert.Equal("erreichbar", WerkzeugWahl.ZustandText(Erreichbarkeit.Ja));
        Assert.Equal("nicht erreichbar", WerkzeugWahl.ZustandText(Erreichbarkeit.Nein));
        Assert.Equal("sucht …", WerkzeugWahl.ZustandText(Erreichbarkeit.Sucht));
        Assert.Equal("Kopplung widerrufen", WerkzeugWahl.ZustandText(Erreichbarkeit.Fehler, "Kopplung widerrufen"));
        Assert.Equal("Fehler", WerkzeugWahl.ZustandText(Erreichbarkeit.Fehler));
        Assert.Equal([1, 2, 3, 0, 0], new[] { Erreichbarkeit.Ja, Erreichbarkeit.Nein, Erreichbarkeit.Fehler, Erreichbarkeit.Sucht, Erreichbarkeit.Unbekannt }.Select(WerkzeugWahl.Ampel));
        var e = WerkzeugWahl.Ordnen([new("xrlabor", "XR-Labor", "PC", Erreichbarkeit.Ja, false, "labor"), new("kicad", "KICAD", "PC", Erreichbarkeit.Nein, true, "status")]);
        Assert.Equal(["kicad", "xrlabor"], e.Select(x => x.Kennung));
    }

    // ---------------------------------------------------------------------------------------------- Startseite

    [Fact]
    public void Startseite_eine_Reihe_frontal_gross_ohne_Ueberlappung()
    {
        var m = GlasMasse.Fuer(new AppEinstellungen());
        var auge = new Vec3(1, 1.6, 2);
        var gieren = 30 * Grad;
        var vor = new Vec3(Math.Cos(gieren), 0, -Math.Sin(gieren));
        var p = Startseite.Plaetze(["kicad", "xrlabor"], auge, gieren, m);
        Assert.Equal(["kicad", "xrlabor"], p.Select(x => x.Id));
        var d = Startseite.Durchmesser(m);
        Assert.True(d > 1.5 * AppSymbole.Durchmesser(m), $"gross: {d} m");   // grösser als ein App-Symbol
        foreach (var x in p)
        {
            Nah(x.Lage.B, d);
            Nah((x.Lage.Mitte - auge).Skalar(vor), Startseite.AbstandM, 1e-9, "Abstand vor den Augen");
            Nah(x.Lage.Mitte.Y, auge.Y - Startseite.UnterAugeM, 1e-9, "Höhe");
            Assert.True(x.Lage.Normale.Skalar((auge - x.Lage.Mitte).Einheit()) > 0.999, "zum Auge gedreht");
        }
        // mittig zur Blickrichtung, mit Lücke (eine Hand berührt nie zwei zugleich)
        var rechts = HebeGeste.Rechts(gieren);
        Nah((p[0].Lage.Mitte - auge).Skalar(rechts) + (p[1].Lage.Mitte - auge).Skalar(rechts), 0, 1e-9, "mittig");
        Nah((p[1].Lage.Mitte - p[0].Lage.Mitte).Laenge, d + Startseite.LueckeM, 1e-9, "Lücke");
        // ein Werkzeug: genau vor dem Blick
        var eins = Startseite.Plaetze(["kicad"], auge, gieren, m)[0];
        Nah((eins.Lage.Mitte - auge).Skalar(rechts), 0);
        // fünf: vier in der ersten Reihe, eins darunter (mittig)
        var fuenf = Startseite.Plaetze(["a", "b", "c", "d", "e"], auge, gieren, m);
        Assert.Equal(4, fuenf.Count(x => Math.Abs(x.Lage.Mitte.Y - (auge.Y - Startseite.UnterAugeM)) < 1e-9));
        Nah(fuenf[4].Lage.Mitte.Y, auge.Y - Startseite.UnterAugeM - Startseite.ReihenAbstand(m));
        Nah((fuenf[4].Lage.Mitte - auge).Skalar(rechts), 0);
        Assert.Empty(Startseite.Plaetze([], auge, gieren, m));
    }

    [Fact]
    public void Startseite_Schild_unter_dem_Symbol_Titel_darueber()
    {
        var m = GlasMasse.Fuer(new AppEinstellungen());
        var p = Startseite.Plaetze(["kicad", "xrlabor"], new Vec3(0, 1.6, 0), 90 * Grad, m);
        var s = Startseite.Schild(p[0].Lage, m);
        Assert.True(s.Mitte.Y + s.H / 2 < p[0].Lage.Mitte.Y - p[0].Lage.H / 2, "Schild unter dem Kreis");
        Nah(s.Normale.Skalar(p[0].Lage.Normale), 1, 1e-9, "gleich ausgerichtet");
        Assert.True(s.B > p[0].Lage.B, "breiter als der Kreis (Name, PC)");
        // Schilder zweier Nachbarn überlappen nicht
        var s2 = Startseite.Schild(p[1].Lage, m);
        Assert.True((s2.Mitte - s.Mitte).Laenge > (s.B + s2.B) / 2, "Schilder getrennt");
        var t = Startseite.Titel(p, m)!;
        Assert.True(t.Mitte.Y - t.H / 2 > p[0].Lage.Mitte.Y + p[0].Lage.H / 2, "Titel über der Reihe");
        Assert.Null(Startseite.Titel([], m));
    }

    // ---------------------------------------------------------------------------------------------- Kanal

    [Fact]
    public void Hallo_meldet_Werkzeug_und_aktiv()
    {
        var o = JsonNode.Parse(KanalText.Hallo("Quest Christian", "Meta Quest 3", "0.1+abc1234", Werkzeug.XrLabor.HalloMarke, false))!.AsObject();
        Assert.Equal("xrlabor", (string)o["marke"]!);
        Assert.False((bool)o["aktiv"]!);
        Assert.Equal("0.1+abc1234", (string)o["app"]!);   // «app» bleibt Fassung und Commit
        Assert.Equal(new[] { "typ", "fassung", "name", "geraet", "app", "marke", "aktiv" }, o.Select(p => p.Key));
        // ohne aktiv wie bis E1 (kein Feld; der PC nimmt true an); ohne Marke kein Feld; zu lange Marke gekürzt
        var alt = JsonNode.Parse(KanalText.Hallo("Q", "G", "0.1", "kicad"))!.AsObject();
        Assert.Equal(new[] { "typ", "fassung", "name", "geraet", "app", "marke" }, alt.Select(p => p.Key));
        Assert.True((bool)JsonNode.Parse(KanalText.Hallo("Q", "G", "0.1", "kicad", true))!["aktiv"]!);
        Assert.Null(JsonNode.Parse(KanalText.Hallo("Q", "G", "0.1", null))!["marke"]);
        Assert.Null(JsonNode.Parse(KanalText.Hallo("Q", "G", "0.1", " "))!["marke"]);
        Assert.Equal(20, ((string)JsonNode.Parse(KanalText.Hallo("Q", "G", null, new string('m', 30)))!["marke"]!).Length);
        // aktiv { aktiv } nach dem Schema des PCs (XrNachrichten.Aktiv)
        Assert.Equal("{\"typ\":\"aktiv\",\"fassung\":1,\"aktiv\":true}", KanalText.Aktiv(true));
        Assert.Equal("{\"typ\":\"aktiv\",\"fassung\":1,\"aktiv\":false}", KanalText.Aktiv(false));
        // die Optionen des Kanals tragen beides
        Assert.Null(new KanalOptionen().Marke);
        Assert.Null(new KanalOptionen().Aktiv);
        Assert.Equal(("kicad", (bool?)true), (new KanalOptionen { Marke = "kicad", Aktiv = true }.Marke, new KanalOptionen { Marke = "kicad", Aktiv = true }.Aktiv));
    }

    [Fact]
    public void Kanal_ohne_Verbindung_merkt_aktiv_und_verwirft_Posen()
    {
        var k = new KanalVerbindung(NetzHilfen.Paket(), _ => Task.FromResult<Uri?>(null), new KanalOptionen { Aktiv = false });
        Assert.False(k.Aktiv);
        k.AktivSetzen(true);
        Assert.True(k.Aktiv);
        Assert.Equal(0, k.AktivGesendet);
        Assert.True(new KanalVerbindung(NetzHilfen.Paket(), _ => Task.FromResult<Uri?>(null)).Aktiv);   // ohne Angabe aktiv (wie bis E1)
    }

    [Fact]
    public async Task Kanal_hallo_aktiv_Wechsel_und_Posen_nur_im_aktiven()
    {
        await using var server = new KanalAttrappe();
        var k = new KanalVerbindung(NetzHilfen.Paket(), _ => Task.FromResult<Uri?>(server.Adresse),
            new KanalOptionen { Name = "Q", Geraet = "G", App = "0.1+abc", Marke = "xrlabor", Aktiv = false, PosenJeSekunde = 30 });
        var pose = new KicadPose(new KicadLage([1, 2, 3], [0, 0, 0, 1]), null, null);
        try
        {
            k.Starten();
            var hallo = await server.Empfangen();
            Assert.Equal(("hallo", "xrlabor", false), ((string)hallo["typ"]!, (string)hallo["marke"]!, (bool)hallo["aktiv"]!));
            // E1: wechselt die App während des Verbindens (vor «willkommen»), erfährt es der PC gleich danach
            k.AktivSetzen(true);
            await server.Senden("{\"typ\":\"willkommen\",\"fassung\":1,\"id\":\"t1\",\"teilnehmer\":[],\"stellschrauben\":{}}");
            var a1 = await server.Empfangen();
            Assert.Equal(("aktiv", true), ((string)a1["typ"]!, (bool)a1["aktiv"]!));
            // aktiv: Posen gehen
            k.PoseSetzen(pose);
            Assert.Equal("pose", (string)(await server.Empfangen())["typ"]!);
            // weg davon: «aktiv false», danach keine Posen mehr
            k.AktivSetzen(false);
            var a2 = await server.Empfangen();
            Assert.Equal(("aktiv", false), ((string)a2["typ"]!, (bool)a2["aktiv"]!));
            for (int i = 0; i < 5; i++) { k.PoseSetzen(pose with { Kopf = new KicadLage([i, 0, 0], [0, 0, 0, 1]) }); await Task.Delay(40); }
            Assert.Null(await server.Empfangen(TimeSpan.FromMilliseconds(400)));
            // derselbe Stand nochmals: nichts
            k.AktivSetzen(false);
            Assert.Null(await server.Empfangen(TimeSpan.FromMilliseconds(200)));
            // wieder aktiv: «aktiv true», Posen gehen wieder
            k.AktivSetzen(true);
            Assert.True((bool)(await server.Empfangen())["aktiv"]!);
            k.PoseSetzen(pose);
            Assert.Equal("pose", (string)(await server.Empfangen())["typ"]!);
            Assert.Equal(3, k.AktivGesendet);
            Assert.Equal(KanalZustand.Verbunden, k.Zustand);
        }
        finally { await k.Beenden(); }
    }

    /// <summary>
    /// Kleiner WebSocket-Server auf 127.0.0.1 (TcpListener, Handschlag nach RFC 6455) – nur für diese Tests, ohne TLS. Eine Schleife liest jede Nachricht der App in
    /// eine Warteschlange (ein abgebrochenes ReceiveAsync würde den WebSocket beenden).
    /// </summary>
    sealed class KanalAttrappe : IAsyncDisposable
    {
        readonly TcpListener l = new(IPAddress.Loopback, 0);
        readonly Task<WebSocket> verbunden;
        readonly System.Threading.Channels.Channel<JsonObject> eingang = System.Threading.Channels.Channel.CreateUnbounded<JsonObject>();
        public Uri Adresse { get; }

        public KanalAttrappe()
        {
            l.Start();
            Adresse = new Uri($"ws://127.0.0.1:{((IPEndPoint)l.LocalEndpoint).Port}/api/xr/kanal");
            verbunden = Annehmen();
            _ = Lesen();
        }

        async Task Lesen()
        {
            try
            {
                var ws = await verbunden;
                var puffer = new byte[64 * 1024];
                while (ws.State == WebSocketState.Open)
                {
                    var r = await ws.ReceiveAsync(puffer, CancellationToken.None);
                    if (r.MessageType == WebSocketMessageType.Close) break;
                    await eingang.Writer.WriteAsync(JsonNode.Parse(Encoding.UTF8.GetString(puffer, 0, r.Count))!.AsObject());
                }
            }
            catch (Exception ex) when (ex is WebSocketException or ObjectDisposedException or IOException or SocketException) { }
        }

        async Task<WebSocket> Annehmen()
        {
            var c = await l.AcceptTcpClientAsync();
            var s = c.GetStream();
            var kopf = new StringBuilder();
            var b = new byte[1];
            while (!kopf.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal) && await s.ReadAsync(b) == 1) kopf.Append((char)b[0]);
            var schluessel = Regex.Match(kopf.ToString(), @"Sec-WebSocket-Key:\s*(\S+)", RegexOptions.IgnoreCase).Groups[1].Value;
            var annahme = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(schluessel + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
            await s.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {annahme}\r\n\r\n"));
            return WebSocket.CreateFromStream(s, new WebSocketCreationOptions { IsServer = true, KeepAliveInterval = TimeSpan.Zero });
        }

        public async Task Senden(string text) =>
            await (await verbunden.WaitAsync(TimeSpan.FromSeconds(10))).SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, CancellationToken.None);

        /// <summary>Nächste Nachricht der App; mit Frist null, wenn in der Frist keine kommt (ohne Frist höchstens 10 s, sonst Fehler).</summary>
        public async Task<JsonObject?> Empfangen(TimeSpan? frist = null)
        {
            using var f = new CancellationTokenSource(frist ?? TimeSpan.FromSeconds(10));
            try { return await eingang.Reader.ReadAsync(f.Token); }
            catch (OperationCanceledException) when (frist is not null) { return null; }
        }

        public async ValueTask DisposeAsync()
        {
            l.Stop();
            if (verbunden.IsCompletedSuccessfully) verbunden.Result.Dispose();
            await Task.CompletedTask;
        }
    }

    // ---------------------------------------------------------------------------------------------- Projektdateien und Grenzen der App

    [Fact]
    public void Kennung_und_Name_stehen_gleich_in_den_Projektdateien()
    {
        static string Wert(string datei, string muster) => Regex.Match(File.ReadAllText(datei), muster).Groups[1].Value.Trim();
        var apk = Path.Combine(Quest, "Projects", "Android", "Hlks.Quest.Android.csproj");
        Assert.Equal(XrApp.Kennung, Wert(apk, "<ApplicationId>([^<]+)</ApplicationId>"));
        Assert.Equal(XrApp.Name, Wert(Path.Combine(Quest, "Platforms", "Android", "Resources", "values", "strings.xml"), "<string name=\"app_name\">([^<]+)</string>"));
        Assert.Equal("android-arm64", Wert(apk, "<RuntimeIdentifiers>([^<]+)</RuntimeIdentifiers>"));
        Assert.Equal("29", Wert(apk, "<SupportedOSPlatformVersion>([^<]+)</SupportedOSPlatformVersion>"));
        Assert.Contains("Include=\"StereoKit\" Version=\"0.3.11\"", File.ReadAllText(apk));
        // eine App für alle Werkzeuge: keine zweite APK, keine Marke je Projekt mehr (Ordnername src/Hlks.Quest bleibt, E1)
        Assert.False(Directory.Exists(Path.Combine(Repo(), "src", "Hlks.Labor.Quest")), "src/Hlks.Labor.Quest entfällt (H+K XR E1)");
        Assert.False(File.Exists(Path.Combine(Quest, "Marke.Kicad.cs")));
        Assert.False(File.Exists(Path.Combine(Quest, "Marke.cs")));
        Assert.Contains("XrApp.Name", File.ReadAllText(Path.Combine(Quest, "Program.cs")));
        // nicht in der Lösung (Android-Workload)
        var sln = File.ReadAllText(Path.Combine(Repo(), "HLKS_CAD.slnx"));
        Assert.DoesNotContain("Hlks.Quest", sln);
        Assert.DoesNotContain("Hlks.Labor.Quest", sln);
    }

    /// <summary>Code-Zeilen einer Datei ohne Kommentare und ohne Inhalt der Zeichenketten (für die Suche nach Bezeichnern und Zahlen).</summary>
    static IEnumerable<(int Nr, string Code)> Code(string datei)
    {
        var zk = new Regex("\"(?:[^\"\\\\]|\\\\.)*\"");
        foreach (var (zeile, nr) in File.ReadLines(datei).Select((z, i) => (z, i + 1)))
        {
            var z = zeile.TrimStart();
            if (z.StartsWith("//", StringComparison.Ordinal) || z.StartsWith("///", StringComparison.Ordinal)) continue;
            var ohne = zk.Replace(zeile, "\"\"");
            var k = ohne.IndexOf("//", StringComparison.Ordinal);
            yield return (nr, k >= 0 ? ohne[..k] : ohne);
        }
    }

    static IEnumerable<string> Quellen(string ordner) =>
        Directory.EnumerateFiles(ordner, "*.cs", SearchOption.AllDirectories)
            .Where(d => !d.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !d.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    [Fact]
    public void Ports_stehen_nicht_fest_in_der_App()
    {
        // Ports und Suchport kommen aus dem Kopplungspaket je Werkzeug (KICAD 5140/5141, Labor-PC 5340/5341)
        var zahl = new Regex(@"\b(5140|5141|5340|5341)\b");
        var funde = Quellen(Quest).SelectMany(d => Code(d).Where(c => zahl.IsMatch(c.Code)).Select(c => $"{Path.GetFileName(d)}:{c.Nr}")).ToList();
        Assert.Empty(funde);
    }

    [Fact]
    public void Der_Empfaenger_ruft_das_Werkzeug_Modul_Labor_nur_ueber_Einhaengepunkte()
    {
        Assert.All(LaborDateien, d => Assert.True(File.Exists(Path.Combine(Quest, d)), d));
        // Probebau ohne Labor: dieselben Dateien
        var csproj = File.ReadAllText(Path.Combine(Quest, "Hlks.Quest.csproj"));
        var entfernt = Regex.Match(csproj, "HlksOhneLabor\\)' == 'true'\">\\s*<Compile Remove=\"([^\"]+)\"").Groups[1].Value.Split(';');
        Assert.Equal(LaborDateien.Order(), entfernt.Order());
        // Typen des Labors in keiner anderen Datei, Felder und Methoden des Labors in keinem anderen Teil von QuestApp (L1: dazu die Ziele des Testraums)
        var typen = new Regex(@"\b(PruefAufgaben|PruefLauf|LaufZustand|AufgabenKarte|LaborKatalogLader|LaborKatalogAblage|LaborKatalogStand|ProtokollAblage|StickTeleport|" +
            @"LaborZiele|ZielMarkierung|ZielWandPaar|ZielBauteil|ZielRaum)\b");
        var glieder = new Regex(@"\b(lauf|karte|raumFuerAufgabe|markiertesBauteil|letztesProtokoll|protokollWarten|protokolleGesendet|quittungen|katalogText|katalogQuelle|" +
            @"katalogAblage|protokollAblage|stickZustand|AufgabeBild|AufgabeVomPc|AufgabeBeenden|AufgabeImTablet|AufgabeKurz|Abschliessen|StickSchritt|KatalogVomPc|" +
            @"KatalogUebernehmen|ProtokolleNachsenden|PruefMessenWandWand|PruefAufgabenBegehen|PruefAufgabeSitzung|KarteKandidat|KarteWegwischen|" +
            @"zieleSzene|PruefTestraumBegehen|PruefRaumArOeffnen|TestraumBilder|BildLauf|testraumVorOg)\b");
        var funde = new List<string>();
        foreach (var d in Quellen(Quest).Where(d => !LaborDateien.Contains(Path.GetFileName(d))))
        {
            var name = Path.GetFileName(d);
            var muster = name.StartsWith("QuestApp", StringComparison.Ordinal) ? new[] { typen, glieder } : [typen];
            foreach (var (nr, code) in Code(d))
                foreach (var m in muster)
                    if (m.Match(code) is { Success: true } t) funde.Add($"{name}:{nr} {t.Value}");
        }
        Assert.Empty(funde);
        // die Einhängepunkte sind alle partial void (ohne Umsetzung entfernt der Übersetzer die Aufrufe) und in den Labor-Dateien umgesetzt;
        // E1: dazu Betreten und Verlassen eines Werkzeugs
        var definiert = Regex.Matches(File.ReadAllText(Path.Combine(Quest, "QuestApp.Einhaengepunkte.cs")), @"^\s*partial void (Labor\w+)\(", RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value).ToList();
        Assert.True(definiert.Count >= 27, $"{definiert.Count} Einhängepunkte");
        Assert.Contains("LaborWerkzeugBetreten", definiert);
        Assert.Contains("LaborWerkzeugVerlassen", definiert);
        var laborText = string.Concat(LaborDateien.Select(d => File.ReadAllText(Path.Combine(Quest, d))));
        var umgesetzt = Regex.Matches(laborText, @"^\s*partial void (Labor\w+)\(", RegexOptions.Multiline).Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(definiert.Order(), umgesetzt.Order());
        // E1: das Modul arbeitet nur, wenn das aktive Werkzeug es führt (Werkzeug.MitLabor)
        Assert.Contains("MitLabor", laborText);
        // Röntgen-Hand und flache Hand gehören zur Bedienung (nicht in den Labor-Dateien)
        Assert.DoesNotContain("bool RoentgenSchritt(", laborText);
        Assert.DoesNotContain("void FlacheHandMelden(", laborText);
        Assert.Contains("bool RoentgenSchritt(", File.ReadAllText(Path.Combine(Quest, "QuestApp.Roentgen.cs")));
        Assert.Contains("void FlacheHandMelden(", File.ReadAllText(Path.Combine(Quest, "QuestApp.Schnitt.cs")));
    }
}
