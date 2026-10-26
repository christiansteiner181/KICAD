using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Hlks.Kern;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Hlks.Web.Tests;

/// <summary>
/// «Quest verbinden» (Modul XR): BrillenDienst und die Endpunkte /api/quest. Statt adb.exe läuft die Attrappe tests/Hlks.AdbAttrappe (als adb.exe unter %TEMP%
/// angelegt, feste Ausgaben aus Dateien, jeder Aufruf im Protokoll) – nie ein echtes adb. Die Benutzereinstellungen (quest.adb) liegen unter %TEMP%.
/// </summary>
public partial class QuestTests : IDisposable
{
    readonly string wurzel = Path.Combine(Path.GetTempPath(), "hlks_quest_" + Guid.NewGuid().ToString("N")[..10]);
    readonly List<IDisposable> aufraeumen = [];

    public QuestTests() { Directory.CreateDirectory(wurzel); }

    public void Dispose()
    {
        foreach (var d in aufraeumen) { try { d.Dispose(); } catch { /* egal */ } }
        for (int i = 0; i < 8; i++)
        {
            try { if (Directory.Exists(wurzel)) Directory.Delete(wurzel, true); return; }
            catch (IOException) { Thread.Sleep(250); }
            catch (UnauthorizedAccessException) { Thread.Sleep(250); }
        }
    }

    // ---------------------------------------------------------------- Hilfen

    /// <summary>Die Attrappe als adb.exe in einem eigenen Ordner; der Zustand steht in Textdateien daneben.</summary>
    sealed class Adb
    {
        public string Ordner { get; }
        public string Pfad => Path.Combine(Ordner, "adb.exe");
        public Adb(string ordner)
        {
            Ordner = ordner;
            Directory.CreateDirectory(ordner);
            var basis = AppContext.BaseDirectory;
            File.Copy(Path.Combine(basis, "Hlks.AdbAttrappe.exe"), Pfad, true);
            foreach (var n in new[] { "Hlks.AdbAttrappe.dll", "Hlks.AdbAttrappe.runtimeconfig.json", "Hlks.AdbAttrappe.deps.json" })
                File.Copy(Path.Combine(basis, n), Path.Combine(ordner, n), true);
        }
        void Schreibe(string datei, params string[] zeilen) => File.WriteAllLines(Path.Combine(Ordner, datei), zeilen);
        public Adb Geraete(params string[] zeilen) { Schreibe("geraete.txt", zeilen); return this; }
        public Adb Reverse(params string[] zeilen) { Schreibe("reverse.txt", zeilen); return this; }
        public Adb Ip(string ausgabe) { File.WriteAllText(Path.Combine(Ordner, "ip.txt"), ausgabe); return this; }
        public Adb Mac(string mac) { File.WriteAllText(Path.Combine(Ordner, "mac.txt"), mac); return this; }
        public Adb Verbindbar(params string[] ips) { Schreibe("verbindbar.txt", ips); return this; }
        public Adb Verhalten(params string[] schalter) { Schreibe("verhalten.txt", schalter); return this; }
        // Weiterleitungen getrennt je Weg: Schalter «je_weg» in Verhalten(); tcpip löscht die des Weges, auf dem es läuft.
        static string Datei(string serie) => "reverse_" + serie.Replace(':', '_') + ".txt";
        public Adb ReverseJe(string serie, params string[] zeilen) { Schreibe(Datei(serie), zeilen); return this; }
        public string[] WeiterleitungenJe(string serie)
        {
            var d = Path.Combine(Ordner, Datei(serie));
            return File.Exists(d) ? File.ReadAllLines(d).Where(z => z.Length > 0).ToArray() : [];
        }
        /// <summary>Die ersten n «connect» scheitern (adbd startet in der Quest neu).</summary>
        public Adb ConnectScheitertNoch(int n) { Schreibe("connect_fehlschlaege.txt", n.ToString()); return this; }
        /// <summary>Ein neu verbundener WLAN-Weg steht für die nächsten n «devices» als offline.</summary>
        public Adb WlanOfflineFuer(int n) { Schreibe("wlan_offline.txt", n.ToString()); return this; }
        /// <summary>Die ersten n «reverse tcp:…» scheitern mit «device offline».</summary>
        public Adb ReverseScheitertNoch(int n) { Schreibe("reverse_fehlschlaege.txt", n.ToString()); return this; }
        /// <summary>Autostart: die ersten n «dumpsys power» melden Asleep.</summary>
        public Adb Wach(int n) { Schreibe("wach_ab.txt", n.ToString()); return this; }
        /// <summary>Autostart: Wachzustand für «dumpsys power» (Awake | Asleep …).</summary>
        public Adb WachDauer(string zustand) { File.WriteAllText(Path.Combine(Ordner, "wach.txt"), zustand); return this; }
        public string[] Aufrufe()
        {
            var d = Path.Combine(Ordner, "aufrufe.log");
            return File.Exists(d) ? File.ReadAllLines(d).Where(z => z.Length > 0).ToArray() : [];
        }
        public string[] Weiterleitungen()
        {
            var d = Path.Combine(Ordner, "reverse.txt");
            return File.Exists(d) ? File.ReadAllLines(d).Where(z => z.Length > 0).ToArray() : [];
        }
    }

    const string Seriennummer = "1WMHH000000000";
    const string Usb = Seriennummer + "\tdevice";

    Adb NeueAttrappe(string name = "adb") => new(Path.Combine(wurzel, name));

    /// <summary>Alle Aufrufe sind feste Befehle von adb (nichts anderes, keine fremden Zeichen).</summary>
    static readonly Regex[] Erlaubt =
    [
        new(@"^devices$"), new(@"^start-server$"),
        new(@"^-s [A-Za-z0-9._:\-]{1,64} reverse tcp:\d{1,5} tcp:\d{1,5}$"),
        new(@"^-s [A-Za-z0-9._:\-]{1,64} reverse --list$"),
        new(@"^-s [A-Za-z0-9._:\-]{1,64} reverse --remove tcp:\d{1,5}$"),
        new(@"^-s [A-Za-z0-9._:\-]{1,64} shell ip -f inet addr show wlan0$"),
        new(@"^-s [A-Za-z0-9._:\-]{1,64} shell ip link show wlan0$"),
        new(@"^-s [A-Za-z0-9._:\-]{1,64} tcpip 5555$"),
        new(@"^connect (\d{1,3}\.){3}\d{1,3}:\d{1,5}$"),
        // Autostart (QuestStarter.cs)
        new(@"^-s [A-Za-z0-9._:\-]{1,64} shell am start -a android.intent.action.VIEW -d http://localhost:\d{1,5}/vr com.oculus.browser$"),
        new(@"^-s [A-Za-z0-9._:\-]{1,64} shell dumpsys power$"), new(@"^-s [A-Za-z0-9._:\-]{1,64} shell dumpsys activity activities$"),
        new(@"^-s [A-Za-z0-9._:\-]{1,64} forward tcp:\d{1,5} localabstract:chrome_devtools_remote$"),
        new(@"^-s [A-Za-z0-9._:\-]{1,64} forward --remove tcp:\d{1,5}$"),
    ];

    static void NurFesteAufrufe(Adb adb) => Assert.All(adb.Aufrufe(), z => Assert.True(Erlaubt.Any(r => r.IsMatch(z)), $"Unerwarteter adb-Aufruf: {z}"));

    sealed class Server : IDisposable
    {
        readonly WebApplicationFactory<Program> basis = new();
        readonly WebApplicationFactory<Program> fabrik;
        public HttpClient Http { get; }
        public Server(string wurzel, BrillenEinstellungen quest, int port = 5130, string geraet = "testpc", IBrillenNetz? netz = null)
        {
            var e = new Einstellungen("Test", geraet, Katalog.StandardOrdner(), null)
            {
                BenutzerOrdner = Path.Combine(wurzel, "benutzer"), ZuletztOrdner = Path.Combine(wurzel, "zuletzt"), ZuletztMerken = false,
                ProjektAblage = Path.Combine(wurzel, "ablage"), Familienbibliothek = Path.Combine(wurzel, "bibliothek"),
            };
            fabrik = basis.WithWebHostBuilder(b => b.ConfigureTestServices(s => { s.AddSingleton(e); s.AddSingleton(quest); if (netz is not null) s.AddSingleton(netz); }));
            Http = fabrik.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri($"http://localhost:{port}") });
        }
        public void Dispose() { Http.Dispose(); fabrik.Dispose(); basis.Dispose(); }

        public async Task<(HttpStatusCode Code, JsonNode? Json)> Get(string pfad = "/api/quest")
        {
            var a = await Http.GetAsync(pfad);
            var t = await a.Content.ReadAsStringAsync();
            return (a.StatusCode, t.Length == 0 ? null : JsonNode.Parse(t));
        }
        public async Task<(HttpStatusCode Code, JsonNode? Json)> Post(string pfad, object? koerper = null)
        {
            var a = await Http.PostAsync(pfad, new StringContent(koerper is null ? "{}" : Json.Text(koerper), Encoding.UTF8, "application/json"));
            var t = await a.Content.ReadAsStringAsync();
            return (a.StatusCode, t.Length == 0 ? null : JsonNode.Parse(t));
        }
        public Task<(HttpStatusCode Code, JsonNode? Json)> AdbSetzen(string? pfad) => Post("/api/benutzer/einstellungen", new { schluessel = "quest.adb", wert = pfad });
    }

    static BrillenEinstellungen Ohne(TimeSpan? limit = null) => new BrillenEinstellungen { AppMarke = KicadXr.App } with
    {
        Zeitlimit = limit ?? TimeSpan.FromSeconds(10), PfadDurchsuchen = false, Orte = [], WlanWartezeit = TimeSpan.Zero,
        WlanGrenze = TimeSpan.FromMilliseconds(500), WlanAbstand = TimeSpan.FromMilliseconds(50), WeiterleitungGrenze = TimeSpan.FromMilliseconds(500),
    };

    /// <summary>Wie Ohne(), aber mit grosszügigen Grenzen für Fälle, die erst nach mehreren Versuchen gelingen (jeder Aufruf der Attrappe braucht ~0,1 s).</summary>
    static BrillenEinstellungen Geduldig() => Ohne() with { WlanGrenze = TimeSpan.FromSeconds(20), WeiterleitungGrenze = TimeSpan.FromSeconds(10) };

    const string WlanWeg = "192.168.1.124:5555";
    const string Wlan = WlanWeg + "\tdevice";

    async Task<Server> Start(Adb? adb, BrillenEinstellungen? quest = null, int port = 5130, IBrillenNetz? netz = null)
    {
        var s = new Server(wurzel, quest ?? Ohne(), port, netz: netz);
        aufraeumen.Add(s);
        if (adb is not null) Assert.Equal(HttpStatusCode.OK, (await s.AdbSetzen(adb.Pfad)).Code);
        return s;
    }

    // ---------------------------------------------------------------- Zustand

    [Fact]
    public async Task Zustand_ohne_adb_sagt_wo_es_fehlt_und_startet_nichts()
    {
        var s = await Start(null);
        var (code, j) = await s.Get();
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.False(j!["adbGefunden"]!.GetValue<bool>());
        Assert.Contains("nicht gefunden", j["adbHinweis"]!.GetValue<string>());
        Assert.False(j["questVerbunden"]!.GetValue<bool>());
        Assert.Equal("http://localhost:5130", j["url"]!.GetValue<string>());
        // Verbinden ohne adb: verständlicher Fehler mit Vorschlag
        var (c2, f) = await s.Post("/api/quest/verbinden");
        Assert.Equal(HttpStatusCode.BadRequest, c2);
        Assert.Contains("adb.exe", f!["fehler"]!.GetValue<string>());
        Assert.NotNull(f["vorschlag"]);
    }

    [Fact]
    public async Task Zustand_ohne_Geraet()
    {
        var adb = NeueAttrappe().Geraete();
        var s = await Start(adb);
        var (code, j) = await s.Get();
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.True(j!["adbGefunden"]!.GetValue<bool>());
        Assert.Equal("einstellung", j["adbQuelle"]!.GetValue<string>());
        Assert.Empty(j["geraete"]!.AsArray());
        Assert.False(j["questVerbunden"]!.GetValue<bool>());
        Assert.False(j["freigegeben"]!.GetValue<bool>());
        Assert.False(j["weiterleitung"]!.GetValue<bool>());
        Assert.Equal(["devices"], adb.Aufrufe());
    }

    [Fact]
    public async Task Zustand_unauthorized_ist_verbunden_aber_nicht_freigegeben()
    {
        var adb = NeueAttrappe().Geraete(Seriennummer + "\tunauthorized");
        var s = await Start(adb);
        var (_, j) = await s.Get();
        Assert.True(j!["questVerbunden"]!.GetValue<bool>());
        Assert.False(j["freigegeben"]!.GetValue<bool>());
        Assert.True(j["nichtFreigegeben"]!.GetValue<bool>());
        Assert.Equal("unauthorized", j["geraete"]![0]!["zustand"]!.GetValue<string>());
        // Verbinden: Erklärung statt Weiterleitung
        var (code, f) = await s.Post("/api/quest/verbinden");
        Assert.Equal(HttpStatusCode.BadRequest, code);
        Assert.Contains("nicht freigegeben", f!["fehler"]!.GetValue<string>());
        Assert.Contains("USB-Debugging erlauben", f["vorschlag"]!.GetValue<string>());
        Assert.DoesNotContain(adb.Aufrufe(), z => z.Contains("reverse tcp:"));
        NurFesteAufrufe(adb);
    }

    [Fact]
    public async Task Zustand_device_mit_Weiterleitung_und_WLAN_Adresse()
    {
        var adb = NeueAttrappe().Geraete(Usb).Reverse("UsbFfs tcp:5130 tcp:5130")
            .Ip("21: wlan0: <BROADCAST,MULTICAST,UP,LOWER_UP>\n    inet 192.168.1.50/24 brd 192.168.1.255 scope global wlan0");
        var s = await Start(adb);
        var (_, j) = await s.Get();
        Assert.True(j!["questVerbunden"]!.GetValue<bool>());
        Assert.True(j["freigegeben"]!.GetValue<bool>());
        Assert.False(j["nichtFreigegeben"]!.GetValue<bool>());
        Assert.Equal(Seriennummer, j["seriennummer"]!.GetValue<string>());
        Assert.False(j["wlan"]!.GetValue<bool>());
        Assert.Equal("192.168.1.50", j["ip"]!.GetValue<string>());
        Assert.True(j["weiterleitung"]!.GetValue<bool>());
        NurFesteAufrufe(adb);
    }

    [Fact]
    public async Task Weiterleitung_fuer_einen_anderen_Port_zaehlt_nicht()
    {
        var adb = NeueAttrappe().Geraete(Usb).Reverse("UsbFfs tcp:51300 tcp:51300", "UsbFfs tcp:5131 tcp:5130");
        var s = await Start(adb);
        var (_, j) = await s.Get();
        Assert.False(j!["weiterleitung"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Mehrere_Geraete_die_freigegebene_Quest_wird_gewaehlt()
    {
        var adb = NeueAttrappe().Geraete("AAAA1111\tunauthorized", Seriennummer + "\tdevice", "BBBB2222\toffline");
        var s = await Start(adb);
        var (_, j) = await s.Get();
        Assert.Equal(3, j!["geraete"]!.AsArray().Count);
        Assert.Equal(Seriennummer, j["seriennummer"]!.GetValue<string>());
    }

    // ---------------------------------------------------------------- Verbinden und Trennen

    [Fact]
    public async Task Verbinden_setzt_reverse_fuer_den_Port_dieses_Servers_und_Trennen_nimmt_es_weg()
    {
        var adb = NeueAttrappe().Geraete(Usb);
        var s = await Start(adb, port: 5642);
        var (code, j) = await s.Post("/api/quest/verbinden");
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Contains($"-s {Seriennummer} reverse tcp:5642 tcp:5642", adb.Aufrufe());
        Assert.True(j!["weiterleitung"]!.GetValue<bool>());
        Assert.Equal(5642, j["port"]!.GetValue<int>());
        Assert.Contains("http://localhost:5642", j["meldung"]!.GetValue<string>());
        Assert.Single(adb.Weiterleitungen());

        var (c2, k) = await s.Post("/api/quest/trennen");
        Assert.Equal(HttpStatusCode.OK, c2);
        Assert.False(k!["weiterleitung"]!.GetValue<bool>());
        Assert.Contains($"-s {Seriennummer} reverse --remove tcp:5642", adb.Aufrufe());
        Assert.Empty(adb.Weiterleitungen());

        // noch einmal trennen: adb meldet den Fehler, KICAD gibt ihn verständlich weiter
        var (c3, f) = await s.Post("/api/quest/trennen");
        Assert.Equal(HttpStatusCode.BadRequest, c3);
        Assert.Contains("not found", f!["fehler"]!.GetValue<string>());
        NurFesteAufrufe(adb);
    }

    [Fact]
    public async Task Verbinden_ohne_Quest_und_bei_adb_Fehler()
    {
        var adb = NeueAttrappe().Geraete();
        var s = await Start(adb);
        var (code, f) = await s.Post("/api/quest/verbinden");
        Assert.Equal(HttpStatusCode.BadRequest, code);
        Assert.Contains("keine Quest", f!["fehler"]!.GetValue<string>());
        // adb reverse schlägt fehl (Code 1): die Meldung von adb ist dabei
        adb.Geraete(Usb).Verhalten("reverse_fehler");
        var (c2, g) = await s.Post("/api/quest/verbinden");
        Assert.Equal(HttpStatusCode.BadRequest, c2);
        Assert.Contains("more than one device", g!["fehler"]!.GetValue<string>());
    }

    // ---------------------------------------------------------------- WLAN

    [Fact]
    public async Task Auf_WLAN_umstellen_tcpip_connect_und_Weiterleitung_ueber_die_neue_Adresse()
    {
        var adb = NeueAttrappe().Geraete(Usb).Ip("    inet 192.168.1.50/24 brd 192.168.1.255 scope global wlan0");
        var s = await Start(adb);
        var (code, j) = await s.Post("/api/quest/wlan");
        Assert.Equal(HttpStatusCode.OK, code);
        var a = adb.Aufrufe();
        var iTcpip = Array.IndexOf(a, $"-s {Seriennummer} tcpip 5555");
        var iConnect = Array.IndexOf(a, "connect 192.168.1.50:5555");
        var iReverse = Array.IndexOf(a, "-s 192.168.1.50:5555 reverse tcp:5130 tcp:5130");
        Assert.True(iTcpip >= 0 && iConnect > iTcpip && iReverse > iConnect, string.Join(" | ", a));
        Assert.Contains("192.168.1.50", j!["meldung"]!.GetValue<string>());
        NurFesteAufrufe(adb);
    }

    // ---- Zwei Wege derselben Quest (USB und WLAN), Befund Meta Quest 3 am 21.10.2026

    [Fact]
    public async Task Zustand_beide_Wege_da_waehlt_den_WLAN_Weg()
    {
        var adb = NeueAttrappe().Geraete(Usb, Wlan).Verhalten("je_weg");
        var s = await Start(adb);
        var (_, j) = await s.Get();
        Assert.Equal(2, j!["geraete"]!.AsArray().Count);   // die Liste bleibt, wie sie ist
        Assert.Equal(WlanWeg, j["seriennummer"]!.GetValue<string>());
        Assert.True(j["wlan"]!.GetValue<bool>());
        Assert.Equal("192.168.1.124", j["ip"]!.GetValue<string>());
        Assert.False(j["weiterleitung"]!.GetValue<bool>());
        Assert.DoesNotContain(adb.Aufrufe(), z => z.Contains("shell"));   // die Adresse steht in der Seriennummer
        NurFesteAufrufe(adb);
    }

    [Fact]
    public async Task Zustand_Weiterleitung_nur_auf_WLAN_ist_true()
    {
        var adb = NeueAttrappe().Geraete(Usb, Wlan).Verhalten("je_weg").ReverseJe(WlanWeg, "host-35 tcp:5130 tcp:5130");
        var s = await Start(adb);
        var (_, j) = await s.Get();
        Assert.Equal(WlanWeg, j!["seriennummer"]!.GetValue<string>());
        Assert.True(j["weiterleitung"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Zustand_Weg_mit_der_Weiterleitung_geht_vor_WLAN()
    {
        var adb = NeueAttrappe().Geraete(Usb, Wlan).Verhalten("je_weg").ReverseJe(Seriennummer, "UsbFfs tcp:5130 tcp:5130")
            .Ip("    inet 192.168.1.124/24 brd 192.168.1.255 scope global wlan0");
        var s = await Start(adb);
        var (_, j) = await s.Get();
        Assert.Equal(Seriennummer, j!["seriennummer"]!.GetValue<string>());
        Assert.False(j["wlan"]!.GetValue<bool>());
        Assert.True(j["weiterleitung"]!.GetValue<bool>());
        Assert.Equal("192.168.1.124", j["ip"]!.GetValue<string>());
        // Haben beide die Weiterleitung, gilt WLAN
        adb.ReverseJe(WlanWeg, "host-35 tcp:5130 tcp:5130");
        var (_, k) = await s.Get();
        Assert.Equal(WlanWeg, k!["seriennummer"]!.GetValue<string>());
    }

    [Fact]
    public async Task Verbinden_mit_zwei_Wegen_setzt_die_Weiterleitung_auf_dem_WLAN_Weg()
    {
        var adb = NeueAttrappe().Geraete(Usb, Wlan).Verhalten("je_weg");
        var s = await Start(adb);
        var (code, j) = await s.Post("/api/quest/verbinden");
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Single(adb.WeiterleitungenJe(WlanWeg));
        Assert.Empty(adb.WeiterleitungenJe(Seriennummer));
        Assert.True(j!["weiterleitung"]!.GetValue<bool>());
        Assert.Equal(WlanWeg, j["seriennummer"]!.GetValue<string>());
        NurFesteAufrufe(adb);
    }

    [Fact]
    public async Task Auf_WLAN_umstellen_wiederholt_connect_bis_der_WLAN_Weg_device_ist()
    {
        // adbd startet nach tcpip neu: zwei Mal abgelehnt, dann kurz offline, dazu schlägt auch die erste Weiterleitung fehl
        var adb = NeueAttrappe().Geraete(Usb).Verhalten("je_weg").ReverseJe(Seriennummer, "UsbFfs tcp:5130 tcp:5130")
            .Ip("    inet 192.168.1.124/24 brd 192.168.1.255 scope global wlan0")
            .ConnectScheitertNoch(2).WlanOfflineFuer(2).ReverseScheitertNoch(1);
        var s = await Start(adb, Geduldig());
        var (code, j) = await s.Post("/api/quest/wlan");
        Assert.Equal(HttpStatusCode.OK, code);
        var a = adb.Aufrufe();
        Assert.True(a.Count(z => z == $"connect {WlanWeg}") >= 4, string.Join(" | ", a));   // 2 abgelehnt, 2 mal verbunden aber offline, dann device
        Assert.Single(a, z => z == $"-s {Seriennummer} tcpip 5555");
        Assert.Empty(adb.WeiterleitungenJe(Seriennummer));   // tcpip hat sie auf USB gelöscht
        Assert.Single(adb.WeiterleitungenJe(WlanWeg));
        Assert.True(j!["wlan"]!.GetValue<bool>());
        Assert.True(j["weiterleitung"]!.GetValue<bool>());
        Assert.Equal(WlanWeg, j["seriennummer"]!.GetValue<string>());
        Assert.Contains("192.168.1.124", j["meldung"]!.GetValue<string>());
        NurFesteAufrufe(adb);
    }

    [Fact]
    public async Task Auf_WLAN_umstellen_Weiterleitung_scheitert_dauernd_meldet_den_Fehler_von_adb()
    {
        var adb = NeueAttrappe().Geraete(Usb).Verhalten("je_weg").Ip("    inet 192.168.1.124/24 scope global wlan0").ReverseScheitertNoch(1000);
        var s = await Start(adb);
        var (code, f) = await s.Post("/api/quest/wlan");
        Assert.Equal(HttpStatusCode.BadRequest, code);
        Assert.Contains("device offline", f!["fehler"]!.GetValue<string>());
        Assert.True(adb.Aufrufe().Count(z => z.Contains("reverse tcp:5130")) >= 2);   // wiederholt, nicht nur einmal
    }

    [Fact]
    public async Task Auf_WLAN_umstellen_bei_bestehendem_WLAN_Weg_fuehrt_kein_zweites_tcpip_aus()
    {
        var adb = NeueAttrappe().Geraete(Usb, Wlan).Verhalten("je_weg").Ip("    inet 192.168.1.124/24 scope global wlan0");
        var s = await Start(adb);
        var (code, j) = await s.Post("/api/quest/wlan");
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.DoesNotContain(adb.Aufrufe(), z => z.Contains("tcpip") || z.StartsWith("connect"));
        Assert.Single(adb.WeiterleitungenJe(WlanWeg));
        Assert.True(j!["weiterleitung"]!.GetValue<bool>());
        Assert.True(j["wlan"]!.GetValue<bool>());
        Assert.Contains("schon über WLAN", j["meldung"]!.GetValue<string>());
        // zweiter Klick: wieder kein tcpip/connect, die bestehende Weiterleitung bleibt (nichts wird neu gesetzt)
        var vorher = adb.Aufrufe().Count(z => z.Contains("reverse tcp:"));
        Assert.Equal(HttpStatusCode.OK, (await s.Post("/api/quest/wlan")).Code);
        Assert.DoesNotContain(adb.Aufrufe(), z => z.Contains("tcpip") || z.StartsWith("connect"));
        Assert.Equal(vorher, adb.Aufrufe().Count(z => z.Contains("reverse tcp:")));
        Assert.Single(adb.WeiterleitungenJe(WlanWeg));
        NurFesteAufrufe(adb);
    }

    [Fact]
    public async Task Trennen_entfernt_die_Weiterleitung_auf_beiden_Wegen_und_ignoriert_Wege_ohne()
    {
        var adb = NeueAttrappe().Geraete(Usb, Wlan).Verhalten("je_weg")
            .ReverseJe(Seriennummer, "UsbFfs tcp:5130 tcp:5130").ReverseJe(WlanWeg, "host-35 tcp:5130 tcp:5130");
        var s = await Start(adb);
        var (code, j) = await s.Post("/api/quest/trennen");
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Empty(adb.WeiterleitungenJe(Seriennummer));
        Assert.Empty(adb.WeiterleitungenJe(WlanWeg));
        Assert.False(j!["weiterleitung"]!.GetValue<bool>());
        Assert.Contains($"-s {Seriennummer} reverse --remove tcp:5130", adb.Aufrufe());
        Assert.Contains($"-s {WlanWeg} reverse --remove tcp:5130", adb.Aufrufe());
        // nur ein Weg hat sie: der andere meldet «not found», das wird ignoriert
        adb.ReverseJe(WlanWeg, "host-35 tcp:5130 tcp:5130");
        var (c2, k) = await s.Post("/api/quest/trennen");
        Assert.Equal(HttpStatusCode.OK, c2);
        Assert.False(k!["weiterleitung"]!.GetValue<bool>());
        Assert.Empty(adb.WeiterleitungenJe(WlanWeg));
        // auf keinem Weg: die Meldung von adb kommt
        var (c3, f) = await s.Post("/api/quest/trennen");
        Assert.Equal(HttpStatusCode.BadRequest, c3);
        Assert.Contains("not found", f!["fehler"]!.GetValue<string>());
        NurFesteAufrufe(adb);
    }

    [Fact]
    public async Task Auf_WLAN_umstellen_Zeitgrenze_ausgeschoepft_meldet_klar_mit_Hinweis()
    {
        var adb = NeueAttrappe().Geraete(Usb).Verhalten("je_weg").Ip("    inet 192.168.1.124/24 scope global wlan0").ConnectScheitertNoch(1000);
        var s = await Start(adb, new BrillenEinstellungen { AppMarke = KicadXr.App,
            Zeitlimit = TimeSpan.FromSeconds(10), PfadDurchsuchen = false, Orte = [], WlanWartezeit = TimeSpan.Zero,
            WlanGrenze = TimeSpan.FromMilliseconds(1500), WlanAbstand = TimeSpan.FromMilliseconds(80), WeiterleitungGrenze = TimeSpan.FromMilliseconds(300),
        });
        var uhr = Stopwatch.StartNew();
        var (code, f) = await s.Post("/api/quest/wlan");
        uhr.Stop();
        Assert.Equal(HttpStatusCode.BadRequest, code);
        Assert.True(uhr.Elapsed < TimeSpan.FromSeconds(8), $"dauerte {uhr.Elapsed}");
        Assert.Contains("fehlgeschlagen", f!["fehler"]!.GetValue<string>());
        Assert.Contains("Connection refused", f["fehler"]!.GetValue<string>());
        Assert.Contains("Versuche", f["fehler"]!.GetValue<string>());
        Assert.Contains("gleiches WLAN", f["vorschlag"]!.GetValue<string>());
        Assert.Contains("Adresse von Hand", f["vorschlag"]!.GetValue<string>());
        var a = adb.Aufrufe();
        Assert.True(a.Count(z => z.StartsWith("connect")) >= 3, string.Join(" | ", a));
        Assert.DoesNotContain(a, z => z.Contains("reverse tcp:"));   // ohne WLAN-Weg keine Weiterleitung
    }

    [Fact]
    public async Task Auf_WLAN_umstellen_WLAN_Weg_bleibt_offline_meldet_den_Zustand()
    {
        var adb = NeueAttrappe().Geraete(Usb).Verhalten("je_weg").Ip("    inet 192.168.1.124/24 scope global wlan0").WlanOfflineFuer(1000);
        var s = await Start(adb, new BrillenEinstellungen { AppMarke = KicadXr.App,
            PfadDurchsuchen = false, Orte = [], WlanWartezeit = TimeSpan.Zero,
            WlanGrenze = TimeSpan.FromMilliseconds(400), WlanAbstand = TimeSpan.FromMilliseconds(60), WeiterleitungGrenze = TimeSpan.FromMilliseconds(300),
        });
        var (code, f) = await s.Post("/api/quest/wlan");
        Assert.Equal(HttpStatusCode.BadRequest, code);
        Assert.Contains("offline", f!["fehler"]!.GetValue<string>());
        Assert.DoesNotContain(adb.Aufrufe(), z => z.Contains("reverse tcp:"));
    }

    [Fact]
    public async Task Auf_WLAN_umstellen_ohne_Adresse_der_Quest_aendert_nichts()
    {
        var adb = NeueAttrappe().Geraete(Usb);   // keine ip.txt: wlan0 gibt es nicht
        var s = await Start(adb);
        var (code, f) = await s.Post("/api/quest/wlan");
        Assert.Equal(HttpStatusCode.BadRequest, code);
        Assert.Contains("WLAN-Adresse", f!["fehler"]!.GetValue<string>());
        Assert.DoesNotContain(adb.Aufrufe(), z => z.Contains("tcpip") || z.StartsWith("connect"));
    }

    [Fact]
    public async Task Auf_WLAN_umstellen_Fehler_von_tcpip_und_connect_werden_gemeldet()
    {
        var adb = NeueAttrappe().Geraete(Usb).Ip("    inet 192.168.1.50/24 brd 192.168.1.255 scope global wlan0").Verhalten("tcpip_fehler");
        var s = await Start(adb);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Post("/api/quest/wlan")).Code);
        Assert.DoesNotContain(adb.Aufrufe(), z => z.StartsWith("connect"));
        adb.Verhalten("connect_fehler");   // adb meldet «failed to connect» mit Code 0
        var (code, f) = await s.Post("/api/quest/wlan");
        Assert.Equal(HttpStatusCode.BadRequest, code);
        Assert.Contains("fehlgeschlagen", f!["fehler"]!.GetValue<string>());
        Assert.DoesNotContain(adb.Aufrufe(), z => z.Contains("192.168.1.50:5555 reverse"));
    }

    [Fact]
    public async Task Mit_IP_verbinden_setzt_die_Weiterleitung_und_nimmt_den_Vorgabeport_5555()
    {
        var adb = NeueAttrappe().Geraete();
        var s = await Start(adb);
        var (code, j) = await s.Post("/api/quest/ip", new { ip = " 192.168.1.50 " });
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Contains("connect 192.168.1.50:5555", adb.Aufrufe());
        Assert.Contains("-s 192.168.1.50:5555 reverse tcp:5130 tcp:5130", adb.Aufrufe());
        Assert.True(j!["weiterleitung"]!.GetValue<bool>());
        Assert.True(j["wlan"]!.GetValue<bool>());
        // mit eigenem Port
        var (c2, _) = await s.Post("/api/quest/ip", new { ip = "10.0.0.7:5556" });
        Assert.Equal(HttpStatusCode.OK, c2);
        Assert.Contains("-s 10.0.0.7:5556 reverse tcp:5130 tcp:5130", adb.Aufrufe());
        NurFesteAufrufe(adb);
    }

    [Fact]
    public async Task Mit_IP_verbinden_Verbindungsfehler_setzt_keine_Weiterleitung()
    {
        var adb = NeueAttrappe().Geraete().Verhalten("connect_fehler");
        var s = await Start(adb);
        var (code, f) = await s.Post("/api/quest/ip", new { ip = "192.168.1.50" });
        Assert.Equal(HttpStatusCode.BadRequest, code);
        Assert.Contains("fehlgeschlagen", f!["fehler"]!.GetValue<string>());
        Assert.DoesNotContain(adb.Aufrufe(), z => z.Contains("reverse"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("localhost")]
    [InlineData("999.1.1.1")]
    [InlineData("1.2.3")]
    [InlineData("1.2.3.4.5")]
    [InlineData("192.168.1.5:99999")]
    [InlineData("192.168.1.5:0")]
    [InlineData("192.168.1.5:abc")]
    [InlineData("0.0.0.0")]
    [InlineData("127.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("192.168.1.5; calc")]
    [InlineData("192.168.1.5 -x")]
    [InlineData("-s evil")]
    [InlineData("192.168.1.5\nshell rm")]
    [InlineData("$(calc)")]
    [InlineData("192.168.1.5\\..\\x")]
    public async Task IP_Pruefung_lehnt_Unsinn_ab_ohne_adb_zu_starten(string ip)
    {
        var adb = NeueAttrappe().Geraete();
        var s = await Start(adb);
        var (code, f) = await s.Post("/api/quest/ip", new { ip });
        Assert.Equal(HttpStatusCode.BadRequest, code);
        Assert.NotNull(f!["fehler"]);
        Assert.Empty(adb.Aufrufe());
    }

    [Fact]
    public async Task IP_fehlt_im_Koerper_ist_ein_Fehler()
    {
        var adb = NeueAttrappe().Geraete();
        var s = await Start(adb);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Post("/api/quest/ip", new { })).Code);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Post("/api/quest/ip", new { ip = 12345 })).Code);
        Assert.Empty(adb.Aufrufe());
    }

    [Fact]
    public void Bekannte_Orte_nennen_zuerst_USERPROFILE_platform_tools()
    {
        var orte = BrillenDienst.BekannteOrte();
        var profil = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.Equal(Path.Combine(profil, "platform-tools", "adb.exe"), orte[0]);   // dorthin lässt der Dialog entpacken (Schritt 2)
        Assert.Contains(@"C:\platform-tools\adb.exe", orte);
        Assert.All(orte, o => Assert.Null(BrillenDienst.AdbPfadFehler(o)));
    }

    [Fact]
    public async Task Ohne_adb_nennt_der_Zustand_die_Suchorte_fuer_die_Einrichtung()
    {
        var s = await Start(null, new BrillenEinstellungen { AppMarke = KicadXr.App, PfadDurchsuchen = false, Orte = [Path.Combine(wurzel, "nix", "adb.exe")] });
        var (_, j) = await s.Get();
        Assert.False(j!["adbGefunden"]!.GetValue<bool>());
        Assert.Equal([Path.Combine(wurzel, "nix", "adb.exe")], j["suchorte"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray());
    }

    [Fact]
    public void IpPruefen_rein()
    {
        Assert.Equal(("192.168.1.50", 5555), BrillenDienst.IpPruefen("192.168.1.50"));
        Assert.Equal(("192.168.1.50", 5556), BrillenDienst.IpPruefen("  192.168.1.50:5556 "));
        Assert.Equal(("10.0.0.7", 5555), BrillenDienst.IpPruefen("010.000.000.007".Replace("010", "10").Replace("000", "0").Replace("007", "7")));
        Assert.Throws<HkXrFehler>(() => BrillenDienst.IpPruefen(null));
        Assert.Throws<HkXrFehler>(() => BrillenDienst.IpPruefen("192.168.1.256"));
        Assert.Throws<HkXrFehler>(() => BrillenDienst.IpPruefen("192.168.1.5:65536"));
    }

    [Fact]
    public void GeraeteLesen_ignoriert_fremde_Zeilen_und_gefaehrliche_Seriennummern()
    {
        var aus = "* daemon not running; starting now at tcp:5037\n* daemon started successfully\nList of devices attached\n"
            + "1WMHH000000000\tdevice\n192.168.1.50:5555\tdevice\nAAAA\tunauthorized\nevil;calc\tdevice\n-s x\tdevice\n$(x)\tdevice\nschrott\n\n";
        var g = BrillenDienst.GeraeteLesen(aus);
        Assert.Equal(["1WMHH000000000", "192.168.1.50:5555", "AAAA"], g.Select(x => x.Seriennummer).ToArray());
        Assert.Equal([false, true, false], g.Select(x => x.Wlan).ToArray());
        Assert.Equal("unauthorized", g[2].Zustand);
    }

    // ---------------------------------------------------------------- Zeitgrenze

    [Fact]
    public async Task Zeitgrenze_beendet_den_haengenden_adb_ueber_seine_PID_und_meldet_es()
    {
        var adb = NeueAttrappe().Geraete(Usb).Verhalten("haengt");
        var s = await Start(adb, Ohne(TimeSpan.FromSeconds(1)));
        var uhr = Stopwatch.StartNew();
        var (code, j) = await s.Get();
        uhr.Stop();
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.True(uhr.Elapsed < TimeSpan.FromSeconds(15), $"dauerte {uhr.Elapsed}");
        Assert.Contains("nicht innerhalb", j!["fehler"]!.GetValue<string>());
        Assert.False(j["freigegeben"]!.GetValue<bool>());
        // die Aktionen melden dasselbe als Fehler
        var (c2, f) = await s.Post("/api/quest/verbinden");
        Assert.Equal(HttpStatusCode.BadRequest, c2);
        Assert.Contains("nicht innerhalb", f!["fehler"]!.GetValue<string>());
        // kein adb.exe der Attrappe läuft mehr
        for (int i = 0; i < 20 && AttrappeLaeuft(adb); i++) await Task.Delay(250);
        Assert.False(AttrappeLaeuft(adb), "Die Attrappe läuft noch");
    }

    static bool AttrappeLaeuft(Adb adb)
    {
        foreach (var p in Process.GetProcessesByName("adb"))
        {
            try { if (string.Equals(p.MainModule?.FileName, adb.Pfad, StringComparison.OrdinalIgnoreCase)) return true; }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { /* schon beendet */ }
            finally { p.Dispose(); }
        }
        return false;
    }

    // ---------------------------------------------------------------- Sicherheit: nur adb.exe

    [Theory]
    [InlineData(@"C:\Windows\System32\cmd.exe")]
    [InlineData(@"C:\platform-tools\adb.bat")]
    [InlineData(@"C:\platform-tools\notadb.exe")]
    [InlineData(@"C:\platform-tools\adb.exe.exe")]
    [InlineData(@"\\server\freigabe\adb.exe")]
    [InlineData("//server/freigabe/adb.exe")]
    [InlineData("adb.exe")]
    [InlineData(@"platform-tools\adb.exe")]
    [InlineData(@"C:\platform-tools\..\Windows\adb.exe")]
    [InlineData(@"C:adb.exe")]
    [InlineData("C:\\platform-tools\\adb.exe\" & calc")]
    [InlineData("")]
    public async Task Einstellung_quest_adb_nimmt_nur_adb_exe_mit_vollem_lokalem_Pfad(string pfad)
    {
        var s = await Start(null);
        var (code, f) = await s.AdbSetzen(pfad);
        Assert.Equal(HttpStatusCode.BadRequest, code);
        Assert.Contains("quest.adb", f!["fehler"]!.GetValue<string>());
    }

    [Fact]
    public async Task Einstellung_quest_adb_gueltiger_Pfad_und_Zuruecksetzen()
    {
        var adb = NeueAttrappe();
        var s = await Start(null);
        Assert.Equal(HttpStatusCode.OK, (await s.AdbSetzen(adb.Pfad)).Code);
        Assert.Equal(HttpStatusCode.OK, (await s.AdbSetzen(@"C:\platform-tools\ADB.EXE")).Code);   // Gross-/Kleinschreibung egal
        Assert.Equal(HttpStatusCode.OK, (await s.AdbSetzen(null)).Code);   // zurücksetzen
        var (_, j) = await s.Get();
        Assert.False(j!["adbGefunden"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Gesetzter_Pfad_ohne_Datei_wird_gemeldet_nicht_still_ersetzt()
    {
        var s = await Start(null);
        Assert.Equal(HttpStatusCode.OK, (await s.AdbSetzen(Path.Combine(wurzel, "gibtsnicht", "adb.exe"))).Code);
        var (_, j) = await s.Get();
        Assert.False(j!["adbGefunden"]!.GetValue<bool>());
        Assert.Contains("gibt es keine adb.exe", j["adbHinweis"]!.GetValue<string>());
    }

    [Fact]
    public void Von_Hand_in_die_Datei_geschriebener_falscher_Pfad_wird_nicht_gestartet()
    {
        var ordner = Path.Combine(wurzel, "hand");
        var ablage = new RechnerAblage(ordner, "pc", true, "einstellungen.json", "Umleitung");
        var b = new BenutzerEinstellungen(ablage);
        var rechner = Path.Combine(wurzel, "eingeschleust.exe");
        File.WriteAllText(rechner, "kein Programm");
        var d = new EinstellungsDatei { Rechner = "pc" };
        d.Werte["quest.adb"] = new EinstellungsWert { Wert = JsonValue.Create(rechner), Zeit = DateTimeOffset.UtcNow };
        Directory.CreateDirectory(ordner);
        File.WriteAllText(ablage.EigenerPfad, Json.Text(d));
        var dienst = new BrillenDienst(new KicadBenutzerWerte(b), Ohne());
        Assert.Null(dienst.AdbFinden(out var hinweis));
        Assert.Contains("ungültig", hinweis);
        Assert.Contains("adb.exe", hinweis);
    }

    [Fact]
    public void Suche_nach_Reihenfolge_Einstellung_PATH_Orte()
    {
        var a1 = NeueAttrappe("ort1");
        var a2 = NeueAttrappe("ort2");
        var ordner = Path.Combine(wurzel, "reihe");
        var b = new BenutzerEinstellungen(new RechnerAblage(ordner, "pc", true, "einstellungen.json", "Umleitung"));
        var nurOrte = new BrillenDienst(new KicadBenutzerWerte(b), new BrillenEinstellungen { AppMarke = KicadXr.App, PfadDurchsuchen = false, Orte = [Path.Combine(wurzel, "nix", "adb.exe"), a1.Pfad, a2.Pfad] });
        var fund = nurOrte.AdbFinden(out _);
        Assert.Equal((a1.Pfad, "ort"), (fund!.Pfad, fund.Quelle));
        // Einstellung geht vor den Orten
        b.Setzen("quest.adb", JsonValue.Create(a2.Pfad));
        fund = nurOrte.AdbFinden(out _);
        Assert.Equal((a2.Pfad, "einstellung"), (fund!.Pfad, fund.Quelle));
    }

    [Fact]
    public void Suche_in_PATH_findet_adb_exe_vor_den_Orten()
    {
        var a1 = NeueAttrappe("imPfad");
        var a2 = NeueAttrappe("ort");
        var alt = Environment.GetEnvironmentVariable("PATH");
        var b = new BenutzerEinstellungen(new RechnerAblage(Path.Combine(wurzel, "pfadreihe"), "pc", true, "einstellungen.json", "Umleitung"));
        try
        {
            Environment.SetEnvironmentVariable("PATH", "relativ;" + Path.GetDirectoryName(a1.Pfad) + ";C:\\gibtsnicht");
            var fund = new BrillenDienst(new KicadBenutzerWerte(b), new BrillenEinstellungen { AppMarke = KicadXr.App, Orte = [a2.Pfad] }).AdbFinden(out _);
            Assert.Equal((a1.Pfad, "pfad"), (fund!.Pfad, fund.Quelle));
        }
        finally { Environment.SetEnvironmentVariable("PATH", alt); }
    }

    // ---------------------------------------------------------------- Sicherheit: Ursprung

    [Fact]
    public async Task Ursprungspruefung_gilt_fuer_alle_Quest_Endpunkte()
    {
        var adb = NeueAttrappe().Geraete(Usb);
        var s = await Start(adb);

        async Task<HttpStatusCode> Senden(HttpMethod m, string pfad, Action<HttpRequestMessage> aendern, string inhalt = "{}", string typ = "application/json")
        {
            var r = new HttpRequestMessage(m, pfad);
            if (m != HttpMethod.Get) r.Content = new StringContent(inhalt, Encoding.UTF8, typ);
            aendern(r);
            return (await s.Http.SendAsync(r)).StatusCode;
        }

        foreach (var pfad in new[] { "/api/quest/verbinden", "/api/quest/wlan", "/api/quest/ip", "/api/quest/trennen", "/api/quest/automatik" })
        {
            Assert.Equal(HttpStatusCode.Forbidden, await Senden(HttpMethod.Post, pfad, r => r.Headers.TryAddWithoutValidation("Origin", "http://boese.example")));
            Assert.Equal(HttpStatusCode.Forbidden, await Senden(HttpMethod.Post, pfad, r => r.Headers.TryAddWithoutValidation("Origin", "http://localhost:6666")));   // anderer Port
            Assert.Equal(HttpStatusCode.Forbidden, await Senden(HttpMethod.Post, pfad, r => r.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "cross-site")));
            Assert.Equal(HttpStatusCode.Forbidden, await Senden(HttpMethod.Post, pfad, r => r.Headers.Host = "boese.example"));   // DNS-Rebinding
            Assert.Equal(HttpStatusCode.UnsupportedMediaType, await Senden(HttpMethod.Post, pfad, _ => { }, "{\"ip\":\"192.168.1.5\"}", "text/plain"));
        }
        Assert.Equal(HttpStatusCode.Forbidden, await Senden(HttpMethod.Get, "/api/quest", r => r.Headers.Host = "boese.example"));
        Assert.Empty(adb.Aufrufe());   // keiner dieser Aufrufe hat adb erreicht
        // die eigene Oberfläche (Origin = dieser Server) darf
        Assert.Equal(HttpStatusCode.OK, await Senden(HttpMethod.Post, "/api/quest/verbinden", r => r.Headers.TryAddWithoutValidation("Origin", "http://localhost:5130")));
        Assert.Single(adb.Weiterleitungen());
    }

    [Fact]
    public async Task Der_Port_kommt_nur_aus_dem_Host_nie_aus_dem_Koerper()
    {
        var adb = NeueAttrappe().Geraete(Usb);
        var s = await Start(adb, port: 5641);
        var (code, _) = await s.Post("/api/quest/verbinden", new { port = 22 });
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Equal(["UsbFfs tcp:5641 tcp:5641"], adb.Weiterleitungen());
    }

    // ---------------------------------------------------------------- Brille automatisch verbinden (MAC statt IP)

    const string BrillenMac = "aa:bb:cc:dd:ee:01";
    const string QuestIp = "192.168.1.50";
    const string WlanText = "    inet 192.168.1.50/24 brd 192.168.1.255 scope global wlan0";

    /// <summary>Netz-Attrappe: Nachbartabelle, Schnittstellen, Pings (mit Zählung, auch der gleichzeitigen). Wer auf einen Ping antwortet, landet in der Tabelle.</summary>
    sealed class NetzAttrappe : IBrillenNetz
    {
        public List<NachbarEintrag> Tabelle { get; } = [];
        public List<NetzSchnittstelle> Netze { get; } = [new("Ethernet", "Intel(R) Ethernet Connection", "Ethernet", "192.168.1.20", 24)];
        public System.Collections.Concurrent.ConcurrentBag<string> Pings { get; } = [];
        public string? AntwortIp { get; set; }
        public string? AntwortMac { get; set; }
        public int Lesungen;
        int gleichzeitig, hoechstens;
        public int Hoechstens => hoechstens;
        public IReadOnlyList<NachbarEintrag> Nachbarn() { Interlocked.Increment(ref Lesungen); lock (Tabelle) return Tabelle.ToArray(); }
        public IReadOnlyList<NetzSchnittstelle> Schnittstellen() => Netze.ToArray();
        public async Task<bool> Anpingen(string ip, TimeSpan zeitgrenze, CancellationToken abbruch)
        {
            Pings.Add(ip);
            var n = Interlocked.Increment(ref gleichzeitig);
            int alt;
            while ((alt = Volatile.Read(ref hoechstens)) < n && Interlocked.CompareExchange(ref hoechstens, n, alt) != alt) { }
            await Task.Delay(3, abbruch);
            Interlocked.Decrement(ref gleichzeitig);
            if (ip != AntwortIp) return false;
            lock (Tabelle) Tabelle.Add(new NachbarEintrag(ip, AntwortMac!));
            return true;
        }
    }

    sealed class Uhr { public DateTimeOffset Jetzt { get; set; } = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero); }

    static BrillenEinstellungen MitUhr(Uhr u) => Ohne() with { Uhr = () => u.Jetzt };

    static object Brille(string mac = BrillenMac, string serie = Seriennummer, string? ip = "192.168.1.40") => new { seriennummer = serie, mac, letzteIp = ip, zuletzt = "2026-10-20T08:00:00Z" };

    /// <summary>Schaltet «Brille automatisch verbinden» ein und merkt eine Brille (über die Benutzereinstellungen, wie die Oberfläche).</summary>
    static async Task Vorbereiten(Server s, bool automatisch = true, bool brille = true)
    {
        if (automatisch) Assert.Equal(HttpStatusCode.OK, (await s.Post("/api/benutzer/einstellungen", new { schluessel = "quest.automatisch", wert = true })).Code);
        if (brille) Assert.Equal(HttpStatusCode.OK, (await s.Post("/api/benutzer/einstellungen", new { schluessel = "quest.brillen", wert = new[] { Brille() } })).Code);
    }

    static async Task<JsonNode?> Einstellung(Server s, string schluessel) => (await s.Get("/api/benutzer/einstellungen")).Json!["werte"]![schluessel];

    static Task<(HttpStatusCode Code, JsonNode? Json)> Automatik(Server s, string anlass) => s.Post("/api/quest/automatik", new { anlass });

    [Fact]
    public async Task MAC_wird_nach_dem_Verbinden_gemerkt_mit_Seriennummer_und_IP()
    {
        var adb = NeueAttrappe().Geraete(Usb).Ip(WlanText).Mac(BrillenMac);
        var s = await Start(adb);
        Assert.Equal(HttpStatusCode.OK, (await s.Post("/api/quest/verbinden")).Code);
        Assert.Contains($"-s {Seriennummer} shell ip link show wlan0", adb.Aufrufe());
        var liste = (await Einstellung(s, "quest.brillen"))!.AsArray();
        var b = Assert.Single(liste)!;
        Assert.Equal(Seriennummer, b["seriennummer"]!.GetValue<string>());
        Assert.Equal(BrillenMac, b["mac"]!.GetValue<string>());
        Assert.Equal(QuestIp, b["letzteIp"]!.GetValue<string>());
        Assert.NotNull(b["zuletzt"]);
        // der Zustand nennt sie ebenfalls; die Automatik ist noch aus (Vorgabe)
        var (_, z) = await s.Get();
        Assert.False(z!["automatisch"]!.GetValue<bool>());
        Assert.Equal(BrillenMac, z["brillen"]![0]!["mac"]!.GetValue<string>());
        NurFesteAufrufe(adb);
    }

    [Fact]
    public async Task MAC_aus_der_Nachbartabelle_wenn_die_Quest_ip_link_sperrt()
    {
        // Befund Quest 3 (21.10.2026): «ip link show wlan0» meldet «Permission denied»; nach dem connect über WLAN kennt der PC die MAC
        var adb = NeueAttrappe().Geraete(Usb).Ip(WlanText);   // ohne mac.txt: ip link scheitert
        var netz = new NetzAttrappe();
        netz.Tabelle.Add(new NachbarEintrag(QuestIp, BrillenMac));
        var s = await Start(adb, netz: netz);
        Assert.Equal(HttpStatusCode.OK, (await s.Post("/api/quest/wlan")).Code);
        var b = Assert.Single((await Einstellung(s, "quest.brillen"))!.AsArray())!;
        Assert.Equal(BrillenMac, b["mac"]!.GetValue<string>());
        Assert.Equal(QuestIp, b["letzteIp"]!.GetValue<string>());
        NurFesteAufrufe(adb);
    }

    [Fact]
    public async Task MAC_nach_Auf_WLAN_umstellen_und_ueber_WLAN_Adresse_bleibt_die_USB_Seriennummer()
    {
        var adb = NeueAttrappe().Geraete(Usb).Ip(WlanText).Mac(BrillenMac);
        var s = await Start(adb);
        Assert.Equal(HttpStatusCode.OK, (await s.Post("/api/quest/wlan")).Code);
        Assert.Equal(Seriennummer, (await Einstellung(s, "quest.brillen"))![0]!["seriennummer"]!.GetValue<string>());
        // später über die WLAN-Adresse verbunden: gleiche MAC, derselbe Eintrag, die USB-Seriennummer bleibt
        Assert.Equal(HttpStatusCode.OK, (await s.Post("/api/quest/ip", new { ip = QuestIp })).Code);
        var liste = (await Einstellung(s, "quest.brillen"))!.AsArray();
        Assert.Single(liste);
        Assert.Equal(Seriennummer, liste[0]!["seriennummer"]!.GetValue<string>());
        NurFesteAufrufe(adb);
    }

    [Theory]
    [InlineData("00:00:00:00:00:00")]
    [InlineData("02:00:00:00:00:00")]
    [InlineData("ff:ff:ff:ff:ff:ff")]
    [InlineData("01:00:5e:00:00:01")]
    [InlineData("aa:bb:cc:dd:ee")]
    [InlineData("zz:bb:cc:dd:ee:01")]
    [InlineData("aa:bb:cc:dd:ee:01:02")]
    [InlineData("aabbccddee01")]
    public async Task Ungueltige_MAC_der_Quest_wird_verworfen_und_nicht_gemerkt(string mac)
    {
        var adb = NeueAttrappe().Geraete(Usb).Ip(WlanText).Mac(mac);
        var s = await Start(adb);
        Assert.Equal(HttpStatusCode.OK, (await s.Post("/api/quest/verbinden")).Code);   // das Verbinden selbst gelingt
        Assert.Null(await Einstellung(s, "quest.brillen"));
    }

    [Fact]
    public async Task Ungueltige_MAC_in_den_Einstellungen_wird_abgelehnt_und_in_der_Datei_ignoriert()
    {
        var s = await Start(null);
        foreach (var schlecht in new[] { "00:00:00:00:00:00", "AA:BB:CC:DD:EE:01", "aa:bb:cc:dd:ee:0g", "aa:bb:cc:dd:ee:01; calc" })
            Assert.Equal(HttpStatusCode.BadRequest, (await s.Post("/api/benutzer/einstellungen", new { schluessel = "quest.brillen", wert = new[] { Brille(schlecht) } })).Code);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Post("/api/benutzer/einstellungen", new { schluessel = "quest.brillen", wert = new[] { Brille(serie: "-s evil") } })).Code);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Post("/api/benutzer/einstellungen", new { schluessel = "quest.brillen", wert = "text" })).Code);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Post("/api/benutzer/einstellungen", new { schluessel = "quest.automatisch", wert = "ja" })).Code);
        // von Hand in die Datei geschrieben: der ungültige Eintrag fällt beim Lesen weg
        var ordner = Path.Combine(wurzel, "handbrille");
        var ablage = new RechnerAblage(ordner, "pc", true, "einstellungen.json", "Umleitung");
        var d = new EinstellungsDatei { Rechner = "pc" };
        d.Werte["quest.brillen"] = new EinstellungsWert
        {
            Wert = JsonNode.Parse("[{\"seriennummer\":\"A1\",\"mac\":\"00:00:00:00:00:00\"},{\"seriennummer\":\"A2\",\"mac\":\"aa:bb:cc:dd:ee:02\",\"letzteIp\":\"999.1.1.1\"},{\"seriennummer\":\"A2\",\"mac\":\"aa:bb:cc:dd:ee:02\"}]"),
            Zeit = DateTimeOffset.UtcNow,
        };
        Directory.CreateDirectory(ordner);
        File.WriteAllText(ablage.EigenerPfad, Json.Text(d));
        var brillen = new BrillenDienst(new KicadBenutzerWerte(new BenutzerEinstellungen(ablage)), Ohne()).Brillen();
        var b = Assert.Single(brillen);
        Assert.Equal(("A2", "aa:bb:cc:dd:ee:02", null), (b.Seriennummer, b.Mac, b.LetzteIp));   // die ungültige IP fällt weg, der Doppelte auch
    }

    [Fact]
    public void MacPruefen_und_MacLesen_rein()
    {
        Assert.Equal(BrillenMac, BrillenDienst.MacPruefen(BrillenMac));
        Assert.Null(BrillenDienst.MacPruefen(null));
        Assert.Null(BrillenDienst.MacPruefen(" aa:bb:cc:dd:ee:01"));
        Assert.Null(BrillenDienst.MacPruefen("AA:BB:CC:DD:EE:01"));   // nur die Schreibweise der Nachbartabelle und von adb nach ToLower
        Assert.Equal(BrillenMac, BrillenDienst.MacLesen("21: wlan0: <UP>\n    link/ether AA:BB:CC:DD:EE:01 brd ff:ff:ff:ff:ff:ff"));
        Assert.Null(BrillenDienst.MacLesen("21: wlan0: <UP>\n    link/ether 00:00:00:00:00:00 brd ff:ff:ff:ff:ff:ff"));
        Assert.Null(BrillenDienst.MacLesen("link/ether aa:bb:cc:dd:ee:01:02:03"));
        Assert.Null(BrillenDienst.MacLesen("Device \"wlan0\" does not exist."));
        Assert.Null(BrillenDienst.MacLesen(null));
    }

    [Fact]
    public async Task Automatik_findet_die_IP_ueber_die_Nachbartabelle_und_verbindet_ohne_Ping()
    {
        var adb = NeueAttrappe().Geraete().Verbindbar(QuestIp);
        var netz = new NetzAttrappe();
        netz.Tabelle.Add(new NachbarEintrag("192.168.1.1", "11:22:33:44:55:66"));
        netz.Tabelle.Add(new NachbarEintrag(QuestIp, BrillenMac));
        var s = await Start(adb, netz: netz);
        await Vorbereiten(s);
        var (code, j) = await Automatik(s, "takt");
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Equal("bereit", j!["status"]!.GetValue<string>());
        Assert.Equal(QuestIp, j["ip"]!.GetValue<string>());
        Assert.Equal("nein", j["ping"]!.GetValue<string>());
        Assert.Empty(netz.Pings);
        var a = adb.Aufrufe();
        var iConnect = Array.IndexOf(a, $"connect {QuestIp}:5555");
        Assert.True(iConnect >= 0 && Array.IndexOf(a, $"-s {QuestIp}:5555 reverse tcp:5130 tcp:5130") > iConnect, string.Join(" | ", a));
        // gemerkt: neue IP; die Seriennummer der Brille bleibt
        var b = (await Einstellung(s, "quest.brillen"))![0]!;
        Assert.Equal(QuestIp, b["letzteIp"]!.GetValue<string>());
        Assert.Equal(Seriennummer, b["seriennummer"]!.GetValue<string>());
        // noch einmal: es besteht schon eine Weiterleitung, nichts wird neu gesetzt (idempotent)
        var vorher = adb.Aufrufe().Count(x => x.StartsWith("connect") || x.Contains("reverse tcp:"));
        var (_, j2) = await Automatik(s, "takt");
        Assert.Equal("bereit", j2!["status"]!.GetValue<string>());
        Assert.Equal(vorher, adb.Aufrufe().Count(x => x.StartsWith("connect") || x.Contains("reverse tcp:")));
        NurFesteAufrufe(adb);
    }

    [Fact]
    public async Task Automatik_ohne_Eintrag_in_der_Tabelle_pingt_nur_auf_Anlass_im_eigenen_Netz_und_findet_die_Brille()
    {
        var adb = NeueAttrappe().Geraete().Verbindbar(QuestIp);
        var netz = new NetzAttrappe { AntwortIp = QuestIp, AntwortMac = BrillenMac };
        var s = await Start(adb, netz: netz);
        await Vorbereiten(s);
        // im Takt: nur die Tabelle, kein Ping
        var (_, t) = await Automatik(s, "takt");
        Assert.Equal("nicht_gefunden", t!["status"]!.GetValue<string>());
        Assert.Empty(netz.Pings);
        Assert.Contains("Kabel", t["hinweis"]!.GetValue<string>());   // nach einem Neustart der Brille
        Assert.DoesNotContain(adb.Aufrufe(), z => z.StartsWith("connect"));
        // auf Anlass: Ping-Durchlauf, danach die Tabelle nochmals gelesen
        var (_, j) = await Automatik(s, "start");
        Assert.Equal("bereit", j!["status"]!.GetValue<string>());
        Assert.Equal("gelaufen", j["ping"]!.GetValue<string>());
        Assert.Equal(QuestIp, j["ip"]!.GetValue<string>());
        Assert.Equal(253, netz.Pings.Count);   // 192.168.1.1 – .254 ohne die eigene .20
        Assert.All(netz.Pings, ip => Assert.StartsWith("192.168.1.", ip));
        Assert.DoesNotContain("192.168.1.20", netz.Pings);
        Assert.True(netz.Hoechstens is > 1 and <= 32, $"gleichzeitig höchstens {netz.Hoechstens}");
        Assert.Contains($"-s {QuestIp}:5555 reverse tcp:5130 tcp:5130", adb.Aufrufe());
        NurFesteAufrufe(adb);
    }

    [Fact]
    public async Task Ping_Durchlauf_hoechstens_einmal_je_2_Minuten()
    {
        var adb = NeueAttrappe().Geraete();
        var netz = new NetzAttrappe();   // niemand antwortet
        var uhr = new Uhr();
        var s = await Start(adb, MitUhr(uhr), netz: netz);
        await Vorbereiten(s);
        var (_, a) = await Automatik(s, "start");
        Assert.Equal(("nicht_gefunden", "gelaufen", 253), (a!["status"]!.GetValue<string>(), a["ping"]!.GetValue<string>(), netz.Pings.Count));
        uhr.Jetzt += TimeSpan.FromSeconds(60);
        var (_, b) = await Automatik(s, "klick");
        Assert.Equal(("gesperrt", 253), (b!["ping"]!.GetValue<string>(), netz.Pings.Count));
        var (_, c) = await Automatik(s, "ansicht3d");
        Assert.Equal(("gesperrt", 253), (c!["ping"]!.GetValue<string>(), netz.Pings.Count));
        uhr.Jetzt += TimeSpan.FromSeconds(61);   // jetzt sind es 121 s
        var (_, d) = await Automatik(s, "ansicht3d");
        Assert.Equal(("gelaufen", 506), (d!["ping"]!.GetValue<string>(), netz.Pings.Count));
        // der Takt pingt nie, auch nach Ablauf der Frist
        uhr.Jetzt += TimeSpan.FromMinutes(10);
        await Automatik(s, "takt");
        Assert.Equal(506, netz.Pings.Count);
    }

    [Fact]
    public async Task Veralteter_Tabelleneintrag_fuehrt_zu_einem_Ping_Durchlauf_und_zur_neuen_Adresse()
    {
        var adb = NeueAttrappe().Geraete().Verbindbar(QuestIp);
        var netz = new NetzAttrappe { AntwortIp = QuestIp, AntwortMac = BrillenMac };
        netz.Tabelle.Add(new NachbarEintrag("192.168.1.99", BrillenMac));   // alte Adresse
        var s = await Start(adb, netz: netz);
        await Vorbereiten(s);
        var (_, j) = await Automatik(s, "klick");
        Assert.Equal("bereit", j!["status"]!.GetValue<string>());
        Assert.Equal(QuestIp, j["ip"]!.GetValue<string>());
        Assert.Contains("connect 192.168.1.99:5555", adb.Aufrufe());
        Assert.Contains($"connect {QuestIp}:5555", adb.Aufrufe());
    }

    [Fact]
    public async Task USB_Quest_ohne_WLAN_Weg_wird_automatisch_auf_WLAN_umgestellt()
    {
        var adb = NeueAttrappe().Geraete(Usb).Ip(WlanText).Mac(BrillenMac);
        var netz = new NetzAttrappe();
        var s = await Start(adb, netz: netz);
        await Vorbereiten(s, brille: false);
        var (_, j) = await Automatik(s, "takt");
        Assert.Equal("bereit", j!["status"]!.GetValue<string>());
        Assert.Contains("Kabel kann ab", j["meldung"]!.GetValue<string>());
        var a = adb.Aufrufe();
        var iTcpip = Array.IndexOf(a, $"-s {Seriennummer} tcpip 5555");
        var iConnect = Array.IndexOf(a, $"connect {QuestIp}:5555");
        Assert.True(iTcpip >= 0 && iConnect > iTcpip && Array.IndexOf(a, $"-s {QuestIp}:5555 reverse tcp:5130 tcp:5130") > iConnect, string.Join(" | ", a));
        Assert.Equal(BrillenMac, (await Einstellung(s, "quest.brillen"))![0]!["mac"]!.GetValue<string>());
        Assert.Empty(netz.Pings);
        // zweiter Schritt: nichts mehr zu tun (die Weiterleitung steht), kein zweites tcpip
        var (_, j2) = await Automatik(s, "takt");
        Assert.Equal("bereit", j2!["status"]!.GetValue<string>());
        Assert.Single(adb.Aufrufe(), z => z.Contains("tcpip"));
        NurFesteAufrufe(adb);
    }

    [Fact]
    public async Task USB_Quest_ohne_WLAN_Adresse_wird_wenigstens_ueber_das_Kabel_verbunden()
    {
        var adb = NeueAttrappe().Geraete(Usb);   // kein wlan0
        var s = await Start(adb);
        await Vorbereiten(s, brille: false);
        var (_, j) = await Automatik(s, "takt");
        Assert.Equal("bereit", j!["status"]!.GetValue<string>());
        Assert.Contains("Kabel", j["meldung"]!.GetValue<string>());
        Assert.Contains($"-s {Seriennummer} reverse tcp:5130 tcp:5130", adb.Aufrufe());
        Assert.DoesNotContain(adb.Aufrufe(), z => z.Contains("tcpip") || z.StartsWith("connect"));
    }

    [Fact]
    public async Task Quest_mit_Weiterleitung_ist_bereit_ohne_etwas_zu_tun()
    {
        var adb = NeueAttrappe().Geraete(Usb).Reverse("UsbFfs tcp:5130 tcp:5130");
        var netz = new NetzAttrappe();
        var s = await Start(adb, netz: netz);
        await Vorbereiten(s, brille: false);
        var (_, j) = await Automatik(s, "start");
        Assert.Equal("bereit", j!["status"]!.GetValue<string>());
        Assert.DoesNotContain(adb.Aufrufe(), z => z.Contains("tcpip") || z.StartsWith("connect") || z.Contains("reverse tcp:"));
        Assert.Equal(0, netz.Lesungen);
        Assert.Empty(netz.Pings);
    }

    [Fact]
    public async Task Ausgeschaltete_Einstellung_laesst_Takt_Start_und_3D_Ansicht_nichts_tun()
    {
        var adb = NeueAttrappe().Geraete(Usb).Ip(WlanText).Mac(BrillenMac);
        var netz = new NetzAttrappe { AntwortIp = QuestIp, AntwortMac = BrillenMac };
        var s = await Start(adb, netz: netz);
        await Vorbereiten(s, automatisch: false);   // eine Brille ist gemerkt, die Automatik aber aus
        foreach (var anlass in new[] { "takt", "start", "ansicht3d" })
        {
            var (code, j) = await Automatik(s, anlass);
            Assert.Equal(HttpStatusCode.OK, code);
            Assert.Equal("aus", j!["status"]!.GetValue<string>());
            Assert.False(j["automatisch"]!.GetValue<bool>());
        }
        Assert.Empty(adb.Aufrufe());
        Assert.Empty(netz.Pings);
        Assert.Equal(0, netz.Lesungen);
        // ein ausdrücklicher Klick des Menschen darf trotzdem einen Schritt machen
        var (_, k) = await Automatik(s, "klick");
        Assert.Equal("bereit", k!["status"]!.GetValue<string>());
        Assert.Contains("devices", adb.Aufrufe());
    }

    [Fact]
    public async Task Unbekannter_Anlass_ist_ein_Fehler_und_startet_nichts()
    {
        var adb = NeueAttrappe().Geraete(Usb);
        var s = await Start(adb);
        await Vorbereiten(s);
        foreach (var schlecht in new[] { "immer", "klick; calc", "-s evil" })
            Assert.Equal(HttpStatusCode.BadRequest, (await Automatik(s, schlecht)).Code);
        Assert.Empty(adb.Aufrufe());
        // ohne Anlass gilt der Takt
        var (code, j) = await s.Post("/api/quest/automatik");
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Equal("bereit", j!["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task Nicht_freigegebene_Quest_und_fehlendes_adb_werden_gemeldet()
    {
        var s0 = await Start(null);
        await Vorbereiten(s0, brille: false);
        Assert.Equal("kein_adb", (await Automatik(s0, "takt")).Json!["status"]!.GetValue<string>());
        var adb = NeueAttrappe("frei").Geraete(Seriennummer + "\tunauthorized");
        var s = await Start(adb);
        await Vorbereiten(s, brille: false);
        var (_, j) = await Automatik(s, "takt");
        Assert.Equal("nicht_freigegeben", j!["status"]!.GetValue<string>());
        Assert.Contains("USB-Debugging erlauben", j["hinweis"]!.GetValue<string>());
    }

    [Fact]
    public async Task Ohne_gemerkte_Brille_wird_nicht_gesucht_und_gesagt_was_zu_tun_ist()
    {
        var adb = NeueAttrappe().Geraete();
        var netz = new NetzAttrappe();
        var s = await Start(adb, netz: netz);
        await Vorbereiten(s, brille: false);
        var (_, j) = await Automatik(s, "start");
        Assert.Equal("nicht_gefunden", j!["status"]!.GetValue<string>());
        Assert.Contains("Kabel", j["hinweis"]!.GetValue<string>());
        Assert.Empty(netz.Pings);
    }

    [Fact]
    public void VPN_und_TAP_Adapter_werden_beim_Ping_Durchlauf_ausgelassen()
    {
        NetzSchnittstelle[] netze =
        [
            new("Ethernet", "Intel(R) Ethernet Connection I219-LM", "Ethernet", "192.168.1.20", 24),
            new("Ethernet 2", "TAP-Windows Adapter V9", "Ethernet", "10.9.0.5", 24),
            new("OpenVPN Data Channel Offload", "OpenVPN Data Channel Offload", "Ethernet", "10.8.0.2", 24),
            new("Firmen-VPN", "Cisco AnyConnect Secure Mobility Client Virtual Miniport Adapter", "Ethernet", "10.20.0.2", 24),
            new("WireGuard", "WireGuard Tunnel", "Tunnel", "10.7.0.2", 24),
            new("vEthernet (WSL)", "Hyper-V Virtual Ethernet Adapter", "Ethernet", "172.20.0.1", 20),
            new("Mobilfunk", "Mobilfunk", "Ethernet", "85.1.2.3", 24),   // nicht privat
            new("Ethernet 3", "Realtek", "Ethernet", "169.254.4.4", 16),   // Link-lokal
        ];
        Assert.Equal(["Ethernet"], netze.Where(BrillenDienst.EchteSchnittstelle).Select(n => n.Name));
        var ziele = BrillenDienst.PingZiele(netze);
        Assert.Equal(253, ziele.Count);
        Assert.All(ziele, z => Assert.StartsWith("192.168.1.", z));
    }

    [Fact]
    public void Ping_Ziele_bei_engerer_Maske_nur_im_eigenen_Netz_und_ohne_Doppelte()
    {
        var ziele = BrillenDienst.PingZiele([
            new("a", "Wi-Fi", "Wireless80211", "192.168.5.20", 28),
            new("b", "Wi-Fi 2", "Wireless80211", "192.168.5.21", 28),
            new("c", "Ethernet", "Ethernet", "10.1.2.3", 16)]);   // weitere Maske: trotzdem nur das /24
        // 192.168.5.16/28: .17 – .30 ohne die eigenen .20 und .21 = 12; dazu 10.1.2.1 – .254 ohne .3 = 253
        Assert.Equal(12 + 253, ziele.Count);
        Assert.Equal(ziele.Count, ziele.Distinct().Count());
        Assert.DoesNotContain("192.168.5.20", ziele);
        Assert.DoesNotContain("192.168.5.21", ziele);
        Assert.DoesNotContain("192.168.5.31", ziele);   // Broadcast
        Assert.DoesNotContain("192.168.5.16", ziele);   // Netzadresse
        Assert.DoesNotContain("10.1.3.1", ziele);       // kein weiteres /24 aus der weiten Maske
        Assert.Contains("10.1.2.254", ziele);
    }

    [Fact]
    public async Task VPN_und_TAP_Adapter_bekommen_im_Ablauf_keinen_Ping()
    {
        var adb = NeueAttrappe().Geraete();
        var netz = new NetzAttrappe();
        netz.Netze.Add(new("Ethernet 2", "TAP-Windows Adapter V9", "Ethernet", "10.9.0.5", 24));
        netz.Netze.Add(new("WireGuard", "WireGuard Tunnel", "Tunnel", "10.7.0.2", 24));
        var s = await Start(adb, netz: netz);
        await Vorbereiten(s);
        await Automatik(s, "start");
        Assert.Equal(253, netz.Pings.Count);
        Assert.All(netz.Pings, ip => Assert.StartsWith("192.168.1.", ip));
        // nur VPN-Adapter da: kein einziger Ping
        var netz2 = new NetzAttrappe();
        netz2.Netze.Clear();
        netz2.Netze.Add(new("Ethernet 2", "TAP-Windows Adapter V9", "Ethernet", "10.9.0.5", 24));
        var s2 = await Start(NeueAttrappe("adb2").Geraete(), netz: netz2);
        await Vorbereiten(s2);
        Assert.Equal("nicht_gefunden", (await Automatik(s2, "klick")).Json!["status"]!.GetValue<string>());
        Assert.Empty(netz2.Pings);
    }

    [Fact]
    public void Im_Testlauf_ist_das_echte_Netz_leer()
    {
        var netz = IBrillenNetz.Vorgabe();
        Assert.IsType<BrillenNetzLeer>(netz);
        Assert.Empty(netz.Nachbarn());
        Assert.Empty(netz.Schnittstellen());
        Assert.False(netz.Anpingen("192.168.1.1", TimeSpan.FromMilliseconds(100), CancellationToken.None).Result);
    }

    // ---------------------------------------------------------------- Für die KI nicht freigegeben

    [Fact]
    public void KI_Einordnung_Quest_verbinden_ist_verboten_und_es_gibt_kein_MCP_Werkzeug()
    {
        Assert.Equal("verboten", KiEinordnung.Von("quest_verbinden"));
        var f = Assert.Single(KiEinordnung.RechnerFunktionen, x => x.Name == "quest_verbinden");
        Assert.Contains("startet ein Programm", f.Grund, StringComparison.OrdinalIgnoreCase);
        Assert.True(f.Grund.Length >= 40);
        // «Brille automatisch verbinden»: ebenso verboten, kein MCP-Werkzeug, in keiner Liste
        Assert.Equal("verboten", KiEinordnung.Von("quest_automatik"));
        Assert.Single(KiEinordnung.RechnerFunktionen, x => x.Name == "quest_automatik");
        Assert.DoesNotContain("quest_automatik", KiEinordnung.Erlaubt);
        Assert.DoesNotContain("quest_automatik", AuftragDienst.ErlaubteWerkzeuge);
        Assert.DoesNotContain("quest_automatik", GespraechDienst.McpWerkzeuge(new GespraechEinstellungen(), true));
        // kein MCP-Werkzeug zu einer Rechner-Funktion; keines mit quest/adb im Namen; nichts davon erlaubt
        var server = Hlks.Mcp.Werkzeugauswahl.Alle();
        Assert.All(KiEinordnung.RechnerFunktionen, r => Assert.DoesNotContain(r.Name, server));
        Assert.DoesNotContain(server, w => w.Contains("quest", StringComparison.OrdinalIgnoreCase) || w.Contains("adb", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("quest_verbinden", KiEinordnung.Erlaubt);
        Assert.DoesNotContain("quest_verbinden", GespraechDienst.McpWerkzeuge(new GespraechEinstellungen(), false));
        Assert.DoesNotContain("quest_verbinden", GespraechDienst.McpWerkzeuge(new GespraechEinstellungen(), true));
        // die Listen der Aufträge und der Palette kennen es nicht
        Assert.DoesNotContain("quest_verbinden", AuftragDienst.ErlaubteWerkzeuge);
        Assert.DoesNotContain("quest_verbinden", AuftragDienst.InternetWerkzeuge);
    }
}
