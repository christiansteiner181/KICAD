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
/// «Quest verbinden» (Modul XR): QuestDienst und die Endpunkte /api/quest. Statt adb.exe läuft die Attrappe tests/Hlks.AdbAttrappe (als adb.exe unter %TEMP%
/// angelegt, feste Ausgaben aus Dateien, jeder Aufruf im Protokoll) – nie ein echtes adb. Die Benutzereinstellungen (quest.adb) liegen unter %TEMP%.
/// </summary>
public class QuestTests : IDisposable
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
        new(@"^-s [A-Za-z0-9._:\-]{1,64} tcpip 5555$"),
        new(@"^connect (\d{1,3}\.){3}\d{1,3}:\d{1,5}$"),
    ];

    static void NurFesteAufrufe(Adb adb) => Assert.All(adb.Aufrufe(), z => Assert.True(Erlaubt.Any(r => r.IsMatch(z)), $"Unerwarteter adb-Aufruf: {z}"));

    sealed class Server : IDisposable
    {
        readonly WebApplicationFactory<Program> basis = new();
        readonly WebApplicationFactory<Program> fabrik;
        public HttpClient Http { get; }
        public Server(string wurzel, QuestEinstellungen quest, int port = 5130, string geraet = "testpc")
        {
            var e = new Einstellungen("Test", geraet, Katalog.StandardOrdner(), null)
            {
                BenutzerOrdner = Path.Combine(wurzel, "benutzer"), ZuletztOrdner = Path.Combine(wurzel, "zuletzt"), ZuletztMerken = false,
                ProjektAblage = Path.Combine(wurzel, "ablage"), Familienbibliothek = Path.Combine(wurzel, "bibliothek"),
            };
            fabrik = basis.WithWebHostBuilder(b => b.ConfigureTestServices(s => { s.AddSingleton(e); s.AddSingleton(quest); }));
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

    static QuestEinstellungen Ohne(TimeSpan? limit = null) => new()
    {
        Zeitlimit = limit ?? TimeSpan.FromSeconds(10), PfadDurchsuchen = false, Orte = [], WlanWartezeit = TimeSpan.Zero,
        WlanGrenze = TimeSpan.FromMilliseconds(500), WlanAbstand = TimeSpan.FromMilliseconds(50), WeiterleitungGrenze = TimeSpan.FromMilliseconds(500),
    };

    /// <summary>Wie Ohne(), aber mit grosszügigen Grenzen für Fälle, die erst nach mehreren Versuchen gelingen (jeder Aufruf der Attrappe braucht ~0,1 s).</summary>
    static QuestEinstellungen Geduldig() => Ohne() with { WlanGrenze = TimeSpan.FromSeconds(20), WeiterleitungGrenze = TimeSpan.FromSeconds(10) };

    const string WlanWeg = "192.168.1.124:5555";
    const string Wlan = WlanWeg + "\tdevice";

    async Task<Server> Start(Adb? adb, QuestEinstellungen? quest = null, int port = 5130)
    {
        var s = new Server(wurzel, quest ?? Ohne(), port);
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
        var s = await Start(adb, new QuestEinstellungen
        {
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
        var s = await Start(adb, new QuestEinstellungen
        {
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
        var orte = QuestDienst.BekannteOrte();
        var profil = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.Equal(Path.Combine(profil, "platform-tools", "adb.exe"), orte[0]);   // dorthin lässt der Dialog entpacken (Schritt 2)
        Assert.Contains(@"C:\platform-tools\adb.exe", orte);
        Assert.All(orte, o => Assert.Null(QuestDienst.AdbPfadFehler(o)));
    }

    [Fact]
    public async Task Ohne_adb_nennt_der_Zustand_die_Suchorte_fuer_die_Einrichtung()
    {
        var s = await Start(null, new QuestEinstellungen { PfadDurchsuchen = false, Orte = [Path.Combine(wurzel, "nix", "adb.exe")] });
        var (_, j) = await s.Get();
        Assert.False(j!["adbGefunden"]!.GetValue<bool>());
        Assert.Equal([Path.Combine(wurzel, "nix", "adb.exe")], j["suchorte"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray());
    }

    [Fact]
    public void IpPruefen_rein()
    {
        Assert.Equal(("192.168.1.50", 5555), QuestDienst.IpPruefen("192.168.1.50"));
        Assert.Equal(("192.168.1.50", 5556), QuestDienst.IpPruefen("  192.168.1.50:5556 "));
        Assert.Equal(("10.0.0.7", 5555), QuestDienst.IpPruefen("010.000.000.007".Replace("010", "10").Replace("000", "0").Replace("007", "7")));
        Assert.Throws<HlksFehler>(() => QuestDienst.IpPruefen(null));
        Assert.Throws<HlksFehler>(() => QuestDienst.IpPruefen("192.168.1.256"));
        Assert.Throws<HlksFehler>(() => QuestDienst.IpPruefen("192.168.1.5:65536"));
    }

    [Fact]
    public void GeraeteLesen_ignoriert_fremde_Zeilen_und_gefaehrliche_Seriennummern()
    {
        var aus = "* daemon not running; starting now at tcp:5037\n* daemon started successfully\nList of devices attached\n"
            + "1WMHH000000000\tdevice\n192.168.1.50:5555\tdevice\nAAAA\tunauthorized\nevil;calc\tdevice\n-s x\tdevice\n$(x)\tdevice\nschrott\n\n";
        var g = QuestDienst.GeraeteLesen(aus);
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
        var dienst = new QuestDienst(b, Ohne());
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
        var nurOrte = new QuestDienst(b, new QuestEinstellungen { PfadDurchsuchen = false, Orte = [Path.Combine(wurzel, "nix", "adb.exe"), a1.Pfad, a2.Pfad] });
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
            var fund = new QuestDienst(b, new QuestEinstellungen { Orte = [a2.Pfad] }).AdbFinden(out _);
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

        foreach (var pfad in new[] { "/api/quest/verbinden", "/api/quest/wlan", "/api/quest/ip", "/api/quest/trennen" })
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

    // ---------------------------------------------------------------- Für die KI nicht freigegeben

    [Fact]
    public void KI_Einordnung_Quest_verbinden_ist_verboten_und_es_gibt_kein_MCP_Werkzeug()
    {
        Assert.Equal("verboten", KiEinordnung.Von("quest_verbinden"));
        var f = Assert.Single(KiEinordnung.RechnerFunktionen, x => x.Name == "quest_verbinden");
        Assert.Contains("startet ein Programm", f.Grund, StringComparison.OrdinalIgnoreCase);
        Assert.True(f.Grund.Length >= 40);
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
