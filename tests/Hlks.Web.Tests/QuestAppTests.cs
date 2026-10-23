using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Hlks.Kern;
using Microsoft.Extensions.DependencyInjection;

namespace Hlks.Web.Tests;

/// <summary>
/// «Quest App installieren/aktualisieren» im Dialog «Quest verbinden» (Quest App P7): QuestApp.cs, QuestAppNetz.cs, QuestAppEndpunkte.cs. Statt adb.exe läuft die Attrappe
/// tests/Hlks.AdbAttrappe (nie ein echtes adb, nie eine echte Brille); Windows wird nicht befragt (Netz-Attrappe). Der Netzzugang ist der echte (TLS), nur auf 127.0.0.1.
/// </summary>
public class QuestAppTests
{
    const string Seriennummer = "1WMHH000000000";
    const string Usb = Seriennummer + "\tdevice";
    const string WlanWeg = "192.168.1.124:5555";
    const string ApkName = "ch.hkplanung.kicad.quest-Signed.apk";

    // ---------------------------------------------------------------- Umgebung

    sealed class NetzAttrappe : IQuestNetz
    {
        public List<NetzSchnittstelle> Netze { get; } = [new("WLAN", "Intel(R) Wi-Fi 6E", "Wireless80211", "192.168.1.20", 24)];
        public IReadOnlyList<NachbarEintrag> Nachbarn() => [];
        public IReadOnlyList<NetzSchnittstelle> Schnittstellen() => Netze.ToArray();
        public Task<bool> Anpingen(string ip, TimeSpan zeitgrenze, CancellationToken abbruch) => Task.FromResult(false);
    }

    sealed class SystemAttrappe : IQuestAppSystem
    {
        public NetzAuskunft Auskunft { get; set; } = new(true, null, [new NetzProfil("s-net 3", "WLAN", "Private")], [
            new FirewallRegel("KICAD XR Zugang (HTTPS TCP 5140)", true, "Inbound", "Allow", "TCP", "5140", "Private"),
            new FirewallRegel("KICAD XR Suche (UDP 5141)", true, "Inbound", "Allow", "UDP", "5141", "Private")]);
        public int Lesungen;
        public Task<NetzAuskunft> NetzLesen(CancellationToken abbruch) { Interlocked.Increment(ref Lesungen); return Task.FromResult(Auskunft); }
    }

    /// <summary>Die Attrappe als adb.exe; der Zustand steht in Textdateien daneben.</summary>
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
        string D(string n) => Path.Combine(Ordner, n);
        public Adb Geraete(params string[] zeilen) { File.WriteAllLines(D("geraete.txt"), zeilen); return this; }
        public Adb Datei(string name, string inhalt) { File.WriteAllText(D(name), inhalt); return this; }
        public Adb Verhalten(params string[] schalter) { File.WriteAllLines(D("verhalten.txt"), schalter); return this; }
        public string[] Aufrufe() => File.Exists(D("aufrufe.log")) ? File.ReadAllLines(D("aufrufe.log")).Where(z => z.Length > 0).ToArray() : [];
        public string? Gelesen(string name) => File.Exists(D(name)) ? File.ReadAllText(D(name)) : null;
        public string[] Zeilen(string name) => File.Exists(D(name)) ? File.ReadAllLines(D(name)).Where(z => z.Length > 0).ToArray() : [];
    }

    sealed class Umgebung : IAsyncDisposable
    {
        public XrServer Xr { get; }
        public Adb Adb { get; }
        public SystemAttrappe System { get; }
        public NetzAttrappe Lan { get; }
        public string Wurzel { get; }
        public string AblageOrdner => Path.Combine(Wurzel, "quest_app");
        public string PaketOrdner => Path.Combine(Wurzel, "paket_temp");
        public string Apk => Path.Combine(AblageOrdner, ApkName);
        public QuestEinstellungen Quest { get; }

        public Umgebung(bool zugang, bool ablage, string[] geraete, Func<QuestEinstellungen, QuestEinstellungen>? anpassen = null)
        {
            Wurzel = Path.Combine(Path.GetTempPath(), "hlks_questapp_" + Guid.NewGuid().ToString("N")[..10]);
            Directory.CreateDirectory(Wurzel);
            Adb = new Adb(Path.Combine(Wurzel, "adb")).Geraete(geraete);
            System = new SystemAttrappe();
            Lan = new NetzAttrappe();
            if (ablage) AblageAnlegen();
            var q = new QuestEinstellungen
            {
                Zeitlimit = TimeSpan.FromSeconds(10), PfadDurchsuchen = false, Orte = [], WlanWartezeit = TimeSpan.Zero, WlanGrenze = TimeSpan.FromMilliseconds(500),
                WlanAbstand = TimeSpan.FromMilliseconds(50), WeiterleitungGrenze = TimeSpan.FromMilliseconds(500),
                AppAblage = AblageOrdner, PaketOrdner = PaketOrdner, AppWegDatei = Path.Combine(Wurzel, "weg", "weg.json"), InstallZeitlimit = TimeSpan.FromSeconds(30),
            };
            Quest = anpassen is null ? q : anpassen(q);
            Xr = new XrServer(zugang, dienste: s => { s.AddSingleton(Quest); s.AddSingleton<IQuestAppSystem>(System); s.AddSingleton<IQuestNetz>(Lan); });
        }

        public static async Task<Umgebung> Neu(bool zugang = true, bool ablage = true, string[]? geraete = null, Func<QuestEinstellungen, QuestEinstellungen>? anpassen = null)
        {
            var u = new Umgebung(zugang, ablage, geraete ?? [Usb], anpassen);
            var (code, _) = await u.Xr.LokalPost("/api/benutzer/einstellungen", new { schluessel = "quest.adb", wert = u.Adb.Pfad });
            Assert.Equal(HttpStatusCode.OK, code);
            if (zugang) await u.Xr.Aktiv();
            return u;
        }

        public void AblageAnlegen(long groesse = 6000, string? versionName = "0.1", int versionCode = 1)
        {
            Directory.CreateDirectory(AblageOrdner);
            var bytes = RandomNumberGenerator.GetBytes((int)groesse);
            File.WriteAllBytes(Apk, bytes);
            var o = new JsonObject
            {
                ["datei"] = ApkName, ["commit"] = "abc1234", ["zeit"] = "2026-10-23T21:00:00", ["groesse"] = groesse,
                ["sha256"] = Convert.ToHexStringLower(SHA256.HashData(bytes)), ["versionName"] = versionName, ["versionCode"] = versionCode,
            };
            File.WriteAllText(Path.Combine(AblageOrdner, "version.json"), o.ToJsonString());
        }

        public async Task<(HttpStatusCode Code, JsonNode? Json)> Get(string pfad)
        {
            var a = await Xr.Lokal.GetAsync(pfad);
            var t = await a.Content.ReadAsStringAsync();
            return (a.StatusCode, t.Length == 0 ? null : JsonNode.Parse(t));
        }

        public async Task<JsonNode> Installieren(string weg = "wlan", string? name = null)
        {
            var (code, j) = await Xr.LokalPost("/api/quest/app/installieren", name is null ? new { weg } : new { weg, name });
            Assert.Equal(HttpStatusCode.OK, code);
            return j!;
        }

        public async Task<JsonArray> Kopplungen() => (JsonArray)(await Xr.Zugang())["kopplungen"]!;

        public async ValueTask DisposeAsync()
        {
            await Xr.DisposeAsync();
            for (int i = 0; i < 8; i++)
            {
                try { if (Directory.Exists(Wurzel)) Directory.Delete(Wurzel, true); return; }
                catch (IOException) { await Task.Delay(250); }
                catch (UnauthorizedAccessException) { await Task.Delay(250); }
            }
        }
    }

    static readonly Regex[] Erlaubt =
    [
        new(@"^devices$"), new(@"^start-server$"),
        new(@"^-s [A-Za-z0-9._:\-]{1,64} reverse tcp:\d{1,5} tcp:\d{1,5}$"), new(@"^-s [A-Za-z0-9._:\-]{1,64} reverse --list$"),
        new(@"^-s [A-Za-z0-9._:\-]{1,64} shell getprop ro\.product\.model$"), new(@"^-s [A-Za-z0-9._:\-]{1,64} shell getprop ro\.serialno$"),
        new(@"^-s [A-Za-z0-9._:\-]{1,64} shell dumpsys package ch\.hkplanung\.kicad\.quest$"),
        new(@"^-s [A-Za-z0-9._:\-]{1,64} install -r .+\.apk$"),
        new(@"^-s [A-Za-z0-9._:\-]{1,64} shell mkdir -p /sdcard/Android/data/ch\.hkplanung\.kicad\.quest/files$"),
        new(@"^-s [A-Za-z0-9._:\-]{1,64} push .+kopplung\.json /sdcard/Android/data/ch\.hkplanung\.kicad\.quest/files/kopplung\.json$"),
        new(@"^-s [A-Za-z0-9._:\-]{1,64} shell cmd package resolve-activity --brief ch\.hkplanung\.kicad\.quest$"),
        new(@"^-s [A-Za-z0-9._:\-]{1,64} shell am start -n ch\.hkplanung\.kicad\.quest/[A-Za-z0-9_.$]+$"),
    ];

    static void NurFesteAufrufe(Adb adb) => Assert.All(adb.Aufrufe(), z => Assert.True(Erlaubt.Any(r => r.IsMatch(z)), $"Unerwarteter adb-Aufruf: {z}"));

    // ---------------------------------------------------------------- Ablage

    [Fact]
    public async Task Ablage_fehlt_wird_erklaert_und_nichts_gestartet()
    {
        await using var u = await Umgebung.Neu(ablage: false);
        var (code, j) = await u.Get("/api/quest/app");
        Assert.Equal(HttpStatusCode.OK, code);
        var a = j!["ablage"]!;
        Assert.False((bool)a["vorhanden"]!);
        Assert.Contains("quest_app_bereitstellen.ps1", (string)a["fehler"]!);
        Assert.Equal(u.AblageOrdner, (string)a["ordner"]!);
        Assert.Equal("vorgabe", (string)a["quelle"]!);
    }

    [Fact]
    public async Task Ablage_heil_zeigt_Commit_Zeit_Groesse_und_Pruefsumme()
    {
        await using var u = await Umgebung.Neu();
        var a = (await u.Get("/api/quest/app")).Json!["ablage"]!;
        Assert.True((bool)a["vorhanden"]!);
        Assert.Null(a["fehler"]);
        var v = a["version"]!;
        Assert.Equal(ApkName, (string)v["datei"]!);
        Assert.Equal("abc1234", (string)v["commit"]!);
        Assert.Equal("2026-10-23T21:00:00", (string)v["zeit"]!);
        Assert.Equal(6000, (long)v["groesse"]!);
        Assert.Equal(64, ((string)v["sha256"]!).Length);
        Assert.Equal("0.1", (string)v["versionName"]!);
        Assert.Equal(1, (int)v["versionCode"]!);
    }

    [Fact]
    public async Task Ablage_veraenderte_APK_oder_falsche_Groesse_wird_abgelehnt()
    {
        await using var u = await Umgebung.Neu();
        File.AppendAllText(u.Apk, "x");
        var a = (await u.Get("/api/quest/app")).Json!["ablage"]!;
        Assert.Contains("Grösse", (string)a["fehler"]!);
        Assert.Null(a["apkPfad"]);
        // gleiche Grösse, anderer Inhalt: Prüfsumme
        var bytes = File.ReadAllBytes(u.Apk);
        bytes = bytes[..^1];
        bytes[0] ^= 0xFF;
        File.WriteAllBytes(u.Apk, bytes);
        Assert.Contains("Prüfsumme", (string)(await u.Get("/api/quest/app")).Json!["ablage"]!["fehler"]!);
        // die Installation tut dann nichts
        var vorher = u.Adb.Aufrufe().Length;   // der Zustand oben hat adb befragt (devices, getprop, dumpsys); die Installation ruft nichts mehr auf
        var e = await u.Installieren();
        Assert.False((bool)e["ok"]!);
        Assert.Equal("apk", (string)e["schritte"]!.AsArray().Last()!["id"]!);
        Assert.Equal(vorher, u.Adb.Aufrufe().Length);
    }

    [Theory]
    [InlineData("{\"datei\":\"..\\\\x.apk\",\"groesse\":1,\"sha256\":\"AAAA\"}")]
    [InlineData("{\"datei\":\"a.exe\",\"groesse\":1,\"sha256\":\"0000000000000000000000000000000000000000000000000000000000000000\"}")]
    [InlineData("{\"datei\":\"a.apk\",\"groesse\":0,\"sha256\":\"0000000000000000000000000000000000000000000000000000000000000000\"}")]
    [InlineData("{\"datei\":\"a.apk\",\"groesse\":1}")]
    [InlineData("[1]")]
    [InlineData("kein json")]
    public void Versionsdatei_mit_Fehlern_wird_abgelehnt(string text) => Assert.Throws<HlksFehler>(() => QuestDienst.VersionLesen(text));

    [Fact]
    public void Versionsdatei_wie_werkzeuge_quest_app_bereitstellen_sie_schreibt()
    {
        // so lieferte das Skript am 23.10.2026 (Stand 90ecca7) die Datei neben der APK ab; ein Formatwechsel dort muss hier auffallen
        const string vomSkript = "{\"datei\":\"ch.hkplanung.kicad.quest-Signed.apk\",\"commit\":\"90ecca7\",\"zeit\":\"2026-10-23T21:25:31\",\"groesse\":7891448,\"sha256\":\"897244b2a7532fe2d3951d02c7143d4657461ebf87ee3648e757446431f2b194\",\"versionName\":\"0.1\",\"versionCode\":1}";
        var v = QuestDienst.VersionLesen(vomSkript);
        Assert.Equal(ApkName, v.Datei);
        Assert.Equal("90ecca7", v.Commit);
        Assert.Equal("2026-10-23T21:25:31", v.Zeit);
        Assert.Equal(7891448, v.Groesse);
        Assert.Equal("897244b2a7532fe2d3951d02c7143d4657461ebf87ee3648e757446431f2b194", v.Sha256);
        Assert.Equal("0.1", v.VersionName);
        Assert.Equal(1, v.VersionCode);
        // auch mit dem Zusatz «-geaendert» (Quellen nicht eingecheckt) und mit Byte-Order-Mark (PowerShell 5.1 schreibt je nach Weg eines)
        Assert.Equal("90ecca7-geaendert", QuestDienst.VersionLesen(vomSkript.Replace("90ecca7", "90ecca7-geaendert")).Commit);
        Assert.Equal("90ecca7", QuestDienst.VersionLesen("﻿" + vomSkript).Commit);
    }

    [Fact]
    public void Versionsdatei_kurze_Felder_werden_gefiltert()
    {
        var v = QuestDienst.VersionLesen("{\"datei\":\"a.apk\",\"groesse\":10,\"sha256\":\"" + new string('a', 64) + "\",\"commit\":\"<script>\",\"versionName\":\"0.2\",\"versionCode\":3,\"zeit\":\"2026-10-23 21:00\"}");
        Assert.Null(v.Commit);   // «<script>» ist kein Commit
        Assert.Equal("0.2", v.VersionName);
        Assert.Equal(3, v.VersionCode);
        Assert.Equal("2026-10-23 21:00", v.Zeit);
    }

    [Fact]
    public async Task Einstellung_quest_app_ablage_ersetzt_den_Ordner_und_wird_geprueft()
    {
        await using var u = await Umgebung.Neu(ablage: false);
        var anderer = Path.Combine(u.Wurzel, "anderer_ort");
        Directory.CreateDirectory(anderer);
        File.Copy(Path.Combine(u.AblageOrdner, "..", "adb", "adb.exe"), Path.Combine(anderer, "x.bin"), true);   // irgendeine Datei
        Assert.Equal(HttpStatusCode.OK, (await u.Xr.LokalPost("/api/benutzer/einstellungen", new { schluessel = "quest.app_ablage", wert = anderer })).Code);
        var a = (await u.Get("/api/quest/app")).Json!["ablage"]!;
        Assert.Equal("einstellung", (string)a["quelle"]!);
        Assert.Equal(Path.GetFullPath(anderer), (string)a["ordner"]!);
        foreach (var schlecht in new[] { @"\\server\share\app", "relativ\\ordner", @"C:\a\..\b", "", @"C:\a" + "\"" + "b" })
            Assert.Equal(HttpStatusCode.BadRequest, (await u.Xr.LokalPost("/api/benutzer/einstellungen", new { schluessel = "quest.app_ablage", wert = schlecht })).Code);
        Assert.Equal(HttpStatusCode.BadRequest, (await u.Xr.LokalPost("/api/benutzer/einstellungen", new { schluessel = "quest.app_ablage", wert = 5 })).Code);
    }

    [Theory]
    [InlineData(@"C:\Users\x\HLKS_CAD_werkzeuge\quest_app", true)]
    [InlineData(@"D:\Apps\kicad", true)]
    [InlineData(@"\\server\share\app", false)]
    [InlineData("//server/share/app", false)]
    [InlineData(@"relativ\ordner", false)]
    [InlineData(@"C:\a\..\b", false)]
    [InlineData(@"C:\a/../b", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(@"C:\a""b", false)]
    public void Ordner_der_Ablage_nur_vollstaendig_und_lokal(string pfad, bool erlaubt) => Assert.Equal(erlaubt, QuestDienst.AppAblageFehler(pfad) is null);

    [Fact]
    public void Ordner_der_Ablage_zu_lang_wird_abgelehnt() => Assert.NotNull(QuestDienst.AppAblageFehler(@"C:\" + new string('a', 300)));

    [Fact]
    public void Installationsfehler_von_Android_in_einfachen_Worten()
    {
        Assert.Contains("anderen Schlüssel", QuestDienst.InstallFehler("Failure [INSTALL_FAILED_UPDATE_INCOMPATIBLE: x]").Fehler);
        Assert.Contains("anderen Schlüssel", QuestDienst.InstallFehler("Failure [INSTALL_PARSE_FAILED_INCONSISTENT_CERTIFICATES]").Fehler);
        Assert.Contains("neuere Fassung", QuestDienst.InstallFehler("Failure [INSTALL_FAILED_VERSION_DOWNGRADE]").Fehler);
        Assert.Contains("Speicher", QuestDienst.InstallFehler("Failure [INSTALL_FAILED_INSUFFICIENT_STORAGE]").Fehler);
        Assert.Contains("passt nicht zu dieser Brille", QuestDienst.InstallFehler("Failure [INSTALL_FAILED_OLDER_SDK]").Fehler);
        var (f, v) = QuestDienst.InstallFehler("adb: failed to install x.apk: Failure [INSTALL_FAILED_NEU]\n zweite Zeile");
        Assert.Contains("INSTALL_FAILED_NEU", f);
        Assert.DoesNotContain("\n", f);
        Assert.False(string.IsNullOrEmpty(v));
        Assert.Contains("Installation ist fehlgeschlagen", QuestDienst.InstallFehler(null).Fehler);
    }

    [Fact]
    public void Geraetewahl_fuer_die_Installation_bevorzugt_den_passenden_Weg_und_nie_ein_nicht_freigegebenes_Geraet()
    {
        var usb = new QuestGeraet(Seriennummer, "device", false);
        var wlan = new QuestGeraet(WlanWeg, "device", true);
        var offline = new QuestGeraet("192.168.1.99:5555", "offline", true);
        Assert.Equal(usb, QuestDienst.AppGeraetWaehlen([wlan, usb], "kabel"));
        Assert.Equal(wlan, QuestDienst.AppGeraetWaehlen([usb, wlan], "wlan"));
        Assert.Equal(usb, QuestDienst.AppGeraetWaehlen([usb, offline], "wlan"));   // der WLAN-Weg ist nicht bereit: der USB-Weg
        Assert.Null(QuestDienst.AppGeraetWaehlen([offline, new QuestGeraet(Seriennummer, "unauthorized", false)], "kabel"));
    }

    [Fact]
    public async Task Einstellung_brillen_zeigen_ist_Ja_Nein()
    {
        await using var u = await Umgebung.Neu(zugang: false, ablage: false);
        Assert.Equal(HttpStatusCode.OK, (await u.Xr.LokalPost("/api/benutzer/einstellungen", new { schluessel = "ansicht3d.brillen_zeigen", wert = false })).Code);
        Assert.Equal(HttpStatusCode.BadRequest, (await u.Xr.LokalPost("/api/benutzer/einstellungen", new { schluessel = "ansicht3d.brillen_zeigen", wert = "nein" })).Code);
    }

    // ---------------------------------------------------------------- installierte Fassung

    [Fact]
    public void Installierte_Fassung_aus_dumpsys_package()
    {
        var i = QuestDienst.InstalliertLesen("Packages:\n  Package [ch.hkplanung.kicad.quest] (a1):\n    versionCode=7 minSdk=29 targetSdk=32\n    versionName=0.3\n    firstInstallTime=2026-10-23 17:59:10\n    lastUpdateTime=2026-10-24 08:01:02\n");
        Assert.True(i.Installiert);
        Assert.Equal("0.3", i.VersionName);
        Assert.Equal(7, i.VersionCode);
        Assert.Equal("2026-10-24 08:01:02", i.Aktualisiert);
        Assert.Equal("2026-10-23 17:59:10", i.Erstinstalliert);
        Assert.False(QuestDienst.InstalliertLesen("Unable to find package: ch.hkplanung.kicad.quest").Installiert);
        Assert.False(QuestDienst.InstalliertLesen("").Installiert);
        Assert.False(QuestDienst.InstalliertLesen(null).Installiert);
        Assert.False(QuestDienst.InstalliertLesen("Packages:\n  nichts Brauchbares").Installiert);
    }

    [Fact]
    public void Fassungsvergleich()
    {
        var v = new QuestAppVersion(ApkName, "abc", null, 10, new string('a', 64), "0.1", 1);
        Assert.Equal("unbekannt", QuestDienst.FassungVergleich(null, v));
        Assert.Equal("unbekannt", QuestDienst.FassungVergleich(new QuestAppInstalliert(true, "0.1", 1, null, null), null));
        Assert.Equal("nicht_installiert", QuestDienst.FassungVergleich(new QuestAppInstalliert(false, null, null, null, null), v));
        Assert.Equal("gleiche_nummer", QuestDienst.FassungVergleich(new QuestAppInstalliert(true, "0.1", 1, null, null), v));
        Assert.Equal("andere_nummer", QuestDienst.FassungVergleich(new QuestAppInstalliert(true, "0.1", 0, null, null), v));
        Assert.Equal("andere_nummer", QuestDienst.FassungVergleich(new QuestAppInstalliert(true, "0.0", null, null, null), v with { VersionCode = null }));
        Assert.Equal("unbekannt", QuestDienst.FassungVergleich(new QuestAppInstalliert(true, null, null, null, null), v));
    }

    [Fact]
    public async Task Zustand_ohne_Brille_und_ohne_adb()
    {
        await using var u = await Umgebung.Neu(geraete: []);
        var j = (await u.Get("/api/quest/app")).Json!;
        Assert.True((bool)j["adbGefunden"]!);
        Assert.Null(j["brille"]);
        Assert.Equal(["devices"], u.Adb.Aufrufe());

        await using var v = await Umgebung.Neu();
        Assert.Equal(HttpStatusCode.OK, (await v.Xr.LokalPost("/api/benutzer/einstellungen", new { schluessel = "quest.adb", wert = (string?)null })).Code);
        var k = (await v.Get("/api/quest/app")).Json!;
        Assert.False((bool)k["adbGefunden"]!);
        Assert.Contains("adb.exe", (string)k["adbHinweis"]!);
    }

    [Fact]
    public async Task Zustand_mit_Brille_zeigt_installierte_und_abgelegte_Fassung()
    {
        await using var u = await Umgebung.Neu();
        var j = (await u.Get("/api/quest/app")).Json!;
        Assert.Equal(Seriennummer, (string)j["brille"]!["seriennummer"]!);
        Assert.Equal("Quest 3", (string)j["brille"]!["modell"]!);
        Assert.False((bool)j["brille"]!["installiert"]!["installiert"]!);
        Assert.Equal("nicht_installiert", (string)j["vergleich"]!);

        u.Adb.Datei("app_installiert.txt", "0.1 1");
        var k = (await u.Get("/api/quest/app")).Json!;
        Assert.True((bool)k["brille"]!["installiert"]!["installiert"]!);
        Assert.Equal("0.1", (string)k["brille"]!["installiert"]!["versionName"]!);
        Assert.Equal("gleiche_nummer", (string)k["vergleich"]!);

        u.Adb.Datei("app_installiert.txt", "0.0 0");
        Assert.Equal("andere_nummer", (string)(await u.Get("/api/quest/app")).Json!["vergleich"]!);
        NurFesteAufrufe(u.Adb);
    }

    [Fact]
    public async Task Zustand_waehlt_den_WLAN_Weg_vor_USB()
    {
        await using var u = await Umgebung.Neu(geraete: [Usb, Wlan()]);
        Assert.Equal(WlanWeg, (string)(await u.Get("/api/quest/app")).Json!["brille"]!["seriennummer"]!);
    }

    static string Wlan() => WlanWeg + "\tdevice";

    // ---------------------------------------------------------------- Installieren

    static readonly string[] SchritteWlan = ["zugang", "apk", "brille", "installieren", "uebertragen", "starten"];
    static readonly string[] SchritteKabel = ["zugang", "apk", "brille", "installieren", "kabel", "uebertragen", "starten"];

    [Fact]
    public async Task Installieren_ueber_WLAN_Schritt_fuer_Schritt()
    {
        await using var u = await Umgebung.Neu();
        var (port, _, fp) = await u.Xr.Aktiv();
        var e = await u.Installieren("wlan");
        Assert.True((bool)e["ok"]!, e.ToJsonString());
        Assert.Equal(SchritteWlan, e["schritte"]!.AsArray().Select(s => (string)s!["id"]!).ToArray());
        Assert.All(e["schritte"]!.AsArray(), s => Assert.True((bool)s!["ok"]!));
        Assert.Equal("wlan", (string)e["weg"]!);
        Assert.Equal(Seriennummer, (string)e["brille"]!);
        Assert.Equal("Quest 3", (string)e["name"]!);
        Assert.Contains("installiert und gekoppelt", (string)e["meldung"]!);

        // die Reihenfolge der adb-Aufrufe: installieren, Eingang anlegen, übertragen, starten – ohne Weiterleitung
        var aufrufe = u.Adb.Aufrufe();
        NurFesteAufrufe(u.Adb);
        int Pos(string teil) => Array.FindIndex(aufrufe, a => a.Contains(teil));
        Assert.True(Pos(" install -r ") >= 0 && Pos(" install -r ") < Pos(" shell mkdir -p ") && Pos(" shell mkdir -p ") < Pos(" push ") && Pos(" push ") < Pos(" am start -n "), string.Join("\n", aufrufe));
        Assert.DoesNotContain(aufrufe, a => a.Contains(" reverse tcp:"));
        Assert.Contains(u.Adb.Zeilen("installiert.log"), z => z == u.Apk);
        Assert.Equal(["ch.hkplanung.kicad.quest/crc6412ab34cd.MainActivity"], u.Adb.Zeilen("app_start.log"));
        Assert.Equal(["/sdcard/Android/data/ch.hkplanung.kicad.quest/files/kopplung.json"], u.Adb.Zeilen("push_ziel.txt"));

        // das Paket in der Brille ist ein echtes Paket dieses PCs: richtiger Fingerabdruck, Schlüssel gilt am Netzzugang
        var paket = (JsonObject)JsonNode.Parse(u.Adb.Gelesen("gepusht.txt")!)!;
        Assert.Equal("hlks-xr-kopplung", (string)paket["art"]!);
        Assert.Equal(fp, (string)paket["fingerabdruck"]!);
        Assert.Equal(port, (int)paket["port"]!);
        Assert.Equal(Seriennummer, (string)paket["brille"]!);
        using var klient = XrServer.Netz(port, fp, XrServer.Token(paket));
        Assert.Equal(HttpStatusCode.OK, (await klient.GetAsync("/api/xr/verbindung")).StatusCode);
        var k = (await u.Kopplungen()).Single()!;
        Assert.Equal("paket", (string)k["weg"]!);
        Assert.Equal("Quest 3", (string)k["name"]!);
    }

    [Fact]
    public async Task Installieren_ueber_Kabel_setzt_adb_reverse_und_127_0_0_1_zuerst()
    {
        await using var u = await Umgebung.Neu();
        var (port, _, _) = await u.Xr.Aktiv();
        var e = await u.Installieren("kabel", "Quest Christian");
        Assert.True((bool)e["ok"]!, e.ToJsonString());
        Assert.Equal(SchritteKabel, e["schritte"]!.AsArray().Select(s => (string)s!["id"]!).ToArray());
        Assert.Equal("Quest Christian", (string)e["name"]!);
        var aufrufe = u.Adb.Aufrufe();
        NurFesteAufrufe(u.Adb);
        Assert.Contains(aufrufe, a => a == $"-s {Seriennummer} reverse tcp:{port} tcp:{port}");
        Assert.True(Array.FindIndex(aufrufe, a => a.Contains(" reverse tcp:")) < Array.FindIndex(aufrufe, a => a.Contains(" push ")));
        var paket = (JsonObject)JsonNode.Parse(u.Adb.Gelesen("gepusht.txt")!)!;
        Assert.Equal("127.0.0.1", (string)paket["ips"]![0]!);
        Assert.Contains($"tcp:{port} tcp:{port}", u.Adb.Gelesen("reverse.txt"));
    }

    [Fact]
    public void Adressen_im_Paket_je_Weg()
    {
        IReadOnlyList<string> ips = ["192.168.1.20", "10.0.0.5"];
        Assert.Equal(ips, QuestDienst.AdressenFuerWeg("wlan", ips));
        Assert.Equal(["127.0.0.1", "192.168.1.20", "10.0.0.5"], QuestDienst.AdressenFuerWeg("kabel", ips));
        Assert.Equal(["127.0.0.1", "192.168.1.20"], QuestDienst.AdressenFuerWeg("kabel", ["192.168.1.20", "127.0.0.1"]));
        Assert.Equal(["127.0.0.1"], QuestDienst.AdressenFuerWeg("kabel", []));
    }

    [Fact]
    public async Task Das_Paket_bleibt_nicht_am_PC_und_steht_in_keiner_Ausgabe()
    {
        await using var u = await Umgebung.Neu();
        var e = await u.Installieren("kabel");
        Assert.True((bool)e["ok"]!);
        var paket = (JsonObject)JsonNode.Parse(u.Adb.Gelesen("gepusht.txt")!)!;
        var schluessel = (string)paket["schluessel"]!;
        Assert.Equal(43, schluessel.Length);
        // nicht in der Antwort, nicht im Protokoll der adb-Aufrufe, nicht im Zustand
        Assert.DoesNotContain(schluessel, e.ToJsonString());
        Assert.DoesNotContain(schluessel, string.Join("\n", u.Adb.Aufrufe()));
        Assert.DoesNotContain(schluessel, (await u.Get("/api/quest/app")).Json!.ToJsonString());
        Assert.DoesNotContain(schluessel, (await u.Xr.Lokal.GetStringAsync("/api/xr/zugang")));
        // die Datei lag beim «push» da (im eigenen Ordner unter PaketOrdner) und ist danach weg; nichts bleibt liegen
        var quelle = u.Adb.Zeilen("push_quelle.txt").Single();
        Assert.EndsWith(" da=True", quelle);
        Assert.StartsWith(u.PaketOrdner, quelle);
        Assert.Empty(Directory.Exists(u.PaketOrdner) ? Directory.GetFileSystemEntries(u.PaketOrdner, "*", SearchOption.AllDirectories) : []);
        // und der Schlüssel steht auch in keiner Datei der Umgebung am PC (ausser der Attrappe, die den Inhalt des «push» als gepusht.txt festhält = die Brille)
        foreach (var d in Directory.GetFiles(u.Wurzel, "*", SearchOption.AllDirectories).Where(f => !f.EndsWith("gepusht.txt")))
        {
            if (!(Path.GetExtension(d) is ".json" or ".txt" or ".log" or ".dpapi")) continue;
            Assert.DoesNotContain(schluessel, File.ReadAllText(d, Encoding.Latin1));
        }
    }

    [Fact]
    public async Task Eine_neue_Installation_ersetzt_die_Kopplung_derselben_Brille()
    {
        await using var u = await Umgebung.Neu();
        var (port, _, fp) = await u.Xr.Aktiv();
        Assert.True((bool)(await u.Installieren("wlan"))["ok"]!);
        var alt = XrServer.Token((JsonObject)JsonNode.Parse(u.Adb.Gelesen("gepusht.txt")!)!);
        Assert.True((bool)(await u.Installieren("kabel"))["ok"]!);
        var neu = XrServer.Token((JsonObject)JsonNode.Parse(u.Adb.Gelesen("gepusht.txt")!)!);
        Assert.NotEqual(alt, neu);
        Assert.Single(await u.Kopplungen());
        using (var c = XrServer.Netz(port, fp, alt)) Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/xr/verbindung")).StatusCode);
        using (var c = XrServer.Netz(port, fp, neu)) Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/xr/verbindung")).StatusCode);
    }

    [Fact]
    public async Task Weg_waehlt_den_passenden_Geraeteweg_wenn_es_beide_gibt()
    {
        await using var u = await Umgebung.Neu(geraete: [Usb, Wlan()]);
        Assert.True((bool)(await u.Installieren("kabel"))["ok"]!);
        Assert.All(u.Adb.Aufrufe().Where(a => a.StartsWith("-s ")), a => Assert.StartsWith($"-s {Seriennummer} ", a));
        Assert.True((bool)(await u.Installieren("wlan"))["ok"]!);
        Assert.Contains(u.Adb.Aufrufe(), a => a.StartsWith($"-s {WlanWeg} install"));
        Assert.Contains("1WMHH000000000", u.Adb.Gelesen("gepusht.txt")!);   // die Kennung der Brille ist auf beiden Wegen die Seriennummer (getprop), nicht die Adresse
        Assert.DoesNotContain(WlanWeg, u.Adb.Gelesen("gepusht.txt")!);
    }

    [Fact]
    public async Task Ohne_Netzzugang_wird_nichts_getan()
    {
        await using var u = await Umgebung.Neu(zugang: false);
        var e = await u.Installieren("wlan");
        Assert.False((bool)e["ok"]!);
        Assert.Equal("zugang", (string)e["schritte"]!.AsArray().Last()!["id"]!);
        Assert.Contains("Netzzugang", (string)e["fehler"]!);
        Assert.Contains("einschalten", (string)e["vorschlag"]!);
        Assert.Empty(u.Adb.Aufrufe());
        Assert.Empty(await u.Kopplungen());
    }

    [Fact]
    public async Task Ohne_Brille_und_nicht_freigegeben()
    {
        await using (var u = await Umgebung.Neu(geraete: []))
        {
            var e = await u.Installieren();
            Assert.False((bool)e["ok"]!);
            Assert.Equal("brille", (string)e["schritte"]!.AsArray().Last()!["id"]!);
            Assert.Contains("keine Quest", (string)e["fehler"]!);
            Assert.Equal(["devices"], u.Adb.Aufrufe());
            Assert.Empty(await u.Kopplungen());
        }
        await using (var u = await Umgebung.Neu(geraete: [Seriennummer + "\tunauthorized"]))
        {
            var e = await u.Installieren();
            Assert.False((bool)e["ok"]!);
            Assert.Contains("USB-Debugging", (string)e["vorschlag"]!);
        }
    }

    [Fact]
    public async Task Ohne_adb_wird_erklaert()
    {
        await using var u = await Umgebung.Neu();
        await u.Xr.LokalPost("/api/benutzer/einstellungen", new { schluessel = "quest.adb", wert = (string?)null });
        var e = await u.Installieren();
        Assert.False((bool)e["ok"]!);
        Assert.Contains("adb.exe", (string)e["fehler"]!);
    }

    [Fact]
    public async Task Installationsfehler_werden_uebersetzt_und_es_entsteht_keine_Kopplung()
    {
        await using var u = await Umgebung.Neu();
        u.Adb.Datei("install_fehler.txt", "INSTALL_FAILED_UPDATE_INCOMPATIBLE: Package ch.hkplanung.kicad.quest signatures do not match");
        var e = await u.Installieren();
        Assert.False((bool)e["ok"]!);
        Assert.Equal("installieren", (string)e["schritte"]!.AsArray().Last()!["id"]!);
        Assert.Contains("anderen Schlüssel", (string)e["fehler"]!);
        Assert.Contains("deinstallieren", (string)e["vorschlag"]!);
        Assert.DoesNotContain(u.Adb.Aufrufe(), a => a.Contains(" push "));
        Assert.Empty(await u.Kopplungen());

        u.Adb.Datei("install_fehler.txt", "INSTALL_FAILED_INSUFFICIENT_STORAGE");
        Assert.Contains("Speicher", (string)(await u.Installieren())["fehler"]!);
        u.Adb.Datei("install_fehler.txt", "INSTALL_FAILED_SOMETHING_NEW: <b>x</b>");
        var f = (string)(await u.Installieren())["fehler"]!;
        Assert.Contains("INSTALL_FAILED_SOMETHING_NEW", f);
    }

    [Fact]
    public async Task Installation_mit_Zeitueberschreitung()
    {
        await using var u = await Umgebung.Neu(anpassen: q => q with { InstallZeitlimit = TimeSpan.FromSeconds(1) });
        u.Adb.Verhalten("install_haengt");
        var e = await u.Installieren();
        Assert.False((bool)e["ok"]!);
        Assert.Contains("nicht innerhalb", (string)e["fehler"]!);
        Assert.Empty(await u.Kopplungen());
    }

    [Fact]
    public async Task Scheitert_die_Uebertragung_wird_die_Kopplung_zurueckgenommen_und_nichts_bleibt_liegen()
    {
        await using var u = await Umgebung.Neu();
        u.Adb.Verhalten("push_fehler");
        var e = await u.Installieren("kabel");
        Assert.False((bool)e["ok"]!);
        Assert.Equal("uebertragen", (string)e["schritte"]!.AsArray().Last()!["id"]!);
        Assert.Contains("zurückgenommen", (string)e["vorschlag"]!);
        Assert.Empty(await u.Kopplungen());
        Assert.Empty(Directory.Exists(u.PaketOrdner) ? Directory.GetFileSystemEntries(u.PaketOrdner, "*", SearchOption.AllDirectories) : []);
        Assert.DoesNotContain(u.Adb.Aufrufe(), a => a.Contains(" am start"));

        u.Adb.Verhalten("mkdir_fehler");
        var f = await u.Installieren();
        Assert.False((bool)f["ok"]!);
        Assert.Contains("Eingang", (string)f["fehler"]!);
        Assert.Empty(await u.Kopplungen());
    }

    [Fact]
    public async Task Scheitert_nur_der_Start_gilt_die_Installation()
    {
        await using var u = await Umgebung.Neu();
        u.Adb.Verhalten("start_fehler");
        var e = await u.Installieren();
        Assert.True((bool)e["ok"]!);
        var letzter = e["schritte"]!.AsArray().Last()!;
        Assert.Equal("starten", (string)letzter["id"]!);
        Assert.False((bool)letzter["ok"]!);
        Assert.Contains("nicht starten", (string)e["meldung"]!);
        Assert.Single(await u.Kopplungen());

        u.Adb.Verhalten("aktivitaet_fehler");
        var f = await u.Installieren();
        Assert.True((bool)f["ok"]!);
        Assert.False((bool)f["schritte"]!.AsArray().Last()!["ok"]!);
    }

    [Fact]
    public async Task Ungueltige_Eingaben()
    {
        await using var u = await Umgebung.Neu();
        foreach (var weg in new[] { "usb", "", "WLAN; rm -rf" })
            Assert.Equal(HttpStatusCode.BadRequest, (await u.Xr.LokalPost("/api/quest/app/installieren", new { weg })).Code);
        Assert.Equal(HttpStatusCode.BadRequest, (await u.Xr.LokalPost("/api/quest/app/installieren", new { })).Code);
        Assert.Equal(HttpStatusCode.BadRequest, (await u.Xr.LokalPost("/api/quest/app/installieren", new { weg = "wlan", name = new string('x', 61) })).Code);
        Assert.Empty(u.Adb.Aufrufe().Where(a => a.Contains("install")));
        // Name mit Steuerzeichen: abgelehnt, ohne Installation
        Assert.Equal(HttpStatusCode.BadRequest, (await u.Xr.LokalPost("/api/quest/app/installieren", new { weg = "wlan", name = "Brille\nZwei" })).Code);
        Assert.DoesNotContain(u.Adb.Aufrufe(), a => a.Contains(" install "));
        // GET statt POST und fremde Methoden
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await u.Xr.Lokal.GetAsync("/api/quest/app/installieren")).StatusCode);
    }

    [Fact]
    public async Task Der_Name_der_Brille_kommt_aus_dem_Modell_und_wird_nicht_blind_uebernommen()
    {
        await using var u = await Umgebung.Neu();
        u.Adb.Datei("model.txt", "Quest 3S");
        var e = await u.Installieren();
        Assert.Equal("Quest 3S", (string)e["name"]!);
        Assert.Equal("Quest 3S", (string)(await u.Kopplungen()).Single()!["name"]!);
    }

    [Fact]
    public async Task Der_Netzzugang_wird_nicht_von_selbst_eingeschaltet_und_nicht_veraendert()
    {
        await using var u = await Umgebung.Neu(zugang: false);
        await u.Installieren();
        Assert.False((bool)(await u.Xr.Zugang())["an"]!);
        Assert.False(File.Exists(Path.Combine(u.Xr.XrOrdner, "zugang.json")));
    }

    // ---------------------------------------------------------------- Netzprofil und Firewall (nur lesen)

    static NetzAuskunft Regeln(string profile = "Private", int tcp = 5140, int udp = 5141, bool aktiv = true) => new(true, null, [new NetzProfil("s-net 3", "WLAN", "Private")], [
        new FirewallRegel("T", aktiv, "Inbound", "Allow", "TCP", tcp.ToString(), profile), new FirewallRegel("U", aktiv, "Inbound", "Allow", "UDP", udp.ToString(), profile)]);

    static readonly IReadOnlyList<NetzSchnittstelle> Echte = [new("WLAN", "Intel(R) Wi-Fi 6E", "Wireless80211", "192.168.1.20", 24)];

    [Fact]
    public void Bewertung_alles_in_Ordnung()
    {
        var h = QuestAppNetz.Bewerten(Regeln(), Echte, 5140, 5141, true);
        Assert.Equal(["ok"], h.Select(x => x.Stufe).ToArray());
        Assert.Contains("s-net 3", h[0].Text);
    }

    [Fact]
    public void Bewertung_oeffentliches_Netz_warnt_mit_dem_Weg_ueber_das_Kabel()
    {
        var a = Regeln() with { Profile = [new NetzProfil("s-net 3", "WLAN", "Public")] };
        var h = QuestAppNetz.Bewerten(a, Echte, 5140, 5141, true);
        var w = Assert.Single(h, x => x.Stufe == "warnung");
        Assert.Contains("Öffentlich", w.Text);
        Assert.Contains("s-net 3", w.Text);
        Assert.Contains("Kabel", w.Text);
        Assert.DoesNotContain(h, x => x.Stufe == "ok");
        // Domäne zählt auch nicht als «Privat» (die Regeln gelten nur dort)
        Assert.Contains("Domäne", QuestAppNetz.Bewerten(a with { Profile = [new NetzProfil("Firma", "WLAN", "DomainAuthenticated")] }, Echte, 5140, 5141, true).Single(x => x.Stufe == "warnung").Text);
    }

    [Fact]
    public void Bewertung_fehlende_Regeln_nennen_das_Skript_und_ändern_nichts()
    {
        var h = QuestAppNetz.Bewerten(new NetzAuskunft(true, null, [new NetzProfil("s-net 3", "WLAN", "Private")], []), Echte, 5140, 5141, true);
        var w = Assert.Single(h, x => x.Stufe == "warnung");
        Assert.Contains("KICAD XR", w.Text);
        Assert.Contains(@"werkzeuge\xr_firewall.ps1", w.Text);
        Assert.Contains("Administrator", w.Text);
        Assert.Contains("5140", w.Text);
    }

    [Fact]
    public void Bewertung_Regel_fuer_anderen_Port_oder_ausgeschaltet_oder_falsches_Profil()
    {
        Assert.Contains(QuestAppNetz.Bewerten(Regeln(tcp: 5999), Echte, 5140, 5141, true), x => x.Stufe == "warnung" && x.Text.Contains("TCP 5140"));
        Assert.Contains(QuestAppNetz.Bewerten(Regeln(aktiv: false), Echte, 5140, 5141, true), x => x.Stufe == "warnung" && x.Text.Contains("TCP 5140"));
        Assert.Contains(QuestAppNetz.Bewerten(Regeln(profile: "Public"), Echte, 5140, 5141, true), x => x.Stufe == "warnung" && x.Text.Contains("TCP 5140"));
        // fehlt nur UDP: Hinweis, keine Warnung (die App findet den PC auch über die Adressen im Paket)
        var h = QuestAppNetz.Bewerten(Regeln(udp: 5999), Echte, 5140, 5141, true);
        Assert.DoesNotContain(h, x => x.Stufe == "warnung");
        Assert.Contains(h, x => x.Stufe == "info" && x.Text.Contains("UDP 5141"));
        // andere Ports als die Vorgabe (zugang.json)
        Assert.Single(QuestAppNetz.Bewerten(Regeln(tcp: 6000, udp: 6001), Echte, 6000, 6001, true), x => x.Stufe == "ok");
        // Ausgehende und blockierende Regeln zählen nicht
        Assert.False(QuestAppNetz.Deckt(new FirewallRegel("x", true, "Outbound", "Allow", "TCP", "5140", "Private"), "TCP", 5140));
        Assert.False(QuestAppNetz.Deckt(new FirewallRegel("x", true, "Inbound", "Block", "TCP", "5140", "Private"), "TCP", 5140));
        Assert.True(QuestAppNetz.Deckt(new FirewallRegel("x", true, "Inbound", "Allow", "TCP", "5139, 5140", "Any"), "TCP", 5140));
    }

    [Fact]
    public void Bewertung_nicht_lesbar_und_kein_Netz()
    {
        var h = QuestAppNetz.Bewerten(new NetzAuskunft(false, "Windows hat nicht geantwortet", [], []), Echte, 5140, 5141, true);
        var i = Assert.Single(h);
        Assert.Equal("info", i.Stufe);
        Assert.Contains("Kabel", i.Text);
        Assert.Contains(QuestAppNetz.Bewerten(Regeln(), [], 5140, 5141, true), x => x.Stufe == "warnung" && x.Text.Contains("LAN- oder WLAN-Karte"));
        Assert.Contains(QuestAppNetz.Bewerten(Regeln(), Echte, 5140, 5141, false), x => x.Text.Contains("Netzzugang dieses PCs ist aus"));
    }

    [Fact]
    public void Auskunft_von_PowerShell_wird_gelesen_auch_mit_einzelnen_Objekten()
    {
        var a = QuestAppSystemEcht.Lesen("{\"profile\":{\"name\":\"s-net 3\",\"schnittstelle\":\"WLAN\",\"kategorie\":\"Public\"},\"regeln\":[{\"name\":\"KICAD XR Zugang (HTTPS TCP 5140)\",\"aktiv\":true,"
            + "\"richtung\":\"Inbound\",\"aktion\":\"Allow\",\"profile\":\"Private\",\"protokoll\":\"TCP\",\"port\":\"5140\"}]}");
        Assert.True(a.Gelesen);
        Assert.Equal("Public", a.Profile.Single().Kategorie);
        Assert.True(a.Regeln.Single().Aktiv);
        Assert.Equal("5140", a.Regeln.Single().Port);
        var leer = QuestAppSystemEcht.Lesen("{\"profile\":[],\"regeln\":[]}");
        Assert.True(leer.Gelesen);
        Assert.Empty(leer.Profile);
        Assert.False(QuestAppSystemEcht.Lesen("kein json").Gelesen);
        Assert.False(QuestAppSystemEcht.Lesen("").Gelesen);
        Assert.False(QuestAppSystemEcht.Lesen("[1,2]").Gelesen);
    }

    [Fact]
    public void Das_PowerShell_Skript_liest_nur()
    {
        var s = QuestAppSystemEcht.Skript;
        foreach (var befehl in Regex.Matches(s, @"\b([A-Z][a-z]+)-([A-Za-z]+)\b").Select(m => m.Value))
            Assert.True(befehl.StartsWith("Get-") || befehl is "ForEach-Object" or "ConvertTo-Json", $"Unerlaubter Befehl im Skript: {befehl}");
        Assert.DoesNotMatch(@"\b(Set|New|Remove|Enable|Disable|Add|Start|Stop|Invoke|Out|Restart|Clear|Import|Export)-", s);
        Assert.DoesNotContain("\"", s);   // nur einfache Anführungszeichen: die Übergabe als Argument bleibt unverändert
        Assert.Contains("-Group 'KICAD XR'", s);
    }

    [Fact]
    public async Task Echte_Abfrage_von_Windows_stuerzt_nie_ab_und_aendert_nichts()
    {
        if (!OperatingSystem.IsWindows()) return;
        var a = await new QuestAppSystemEcht().NetzLesen(CancellationToken.None);
        if (!a.Gelesen) Assert.False(string.IsNullOrEmpty(a.Fehler));
        else Assert.All(a.Profile, p => Assert.False(string.IsNullOrEmpty(p.Kategorie)));
    }

    [Fact]
    public async Task Netz_Endpunkt_zeigt_Hinweise_Schnittstellen_und_merkt_sich_die_Auskunft()
    {
        await using var u = await Umgebung.Neu();
        var zugang = await u.Xr.Zugang();
        int tcp = (int)zugang["port"]!, udp = (int)zugang["suchPort"]!;   // im Test freie Ports
        u.System.Auskunft = Regeln(tcp: tcp, udp: udp) with { Profile = [new NetzProfil("s-net 3", "WLAN", "Public")] };
        var (code, j) = await u.Get("/api/quest/app/netz");
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.True((bool)j!["gelesen"]!);
        Assert.True((bool)j["zugang"]!["aktiv"]!);
        Assert.True((bool)j["zugang"]!["an"]!);
        Assert.Equal(64 / 4 + 3, ((string)j["zugang"]!["fingerabdruckKurz"]!).Length);   // «AB12 CD34 EF56 7890»
        Assert.Equal("WLAN", (string)j["schnittstellen"]![0]!["name"]!);
        Assert.Equal("Public", (string)j["schnittstellen"]![0]!["profil"]!);
        Assert.Contains(j["hinweise"]!.AsArray(), h => (string)h!["stufe"]! == "warnung" && ((string)h["text"]!).Contains("Öffentlich"));
        Assert.Equal(@"werkzeuge\xr_firewall.ps1", (string)j["skript"]!);
        // 30 s gemerkt; ?neu=1 liest neu
        await u.Get("/api/quest/app/netz");
        Assert.Equal(1, u.System.Lesungen);
        u.System.Auskunft = Regeln(tcp: tcp, udp: udp);
        var k = (await u.Get("/api/quest/app/netz?neu=1")).Json!;
        Assert.Equal(2, u.System.Lesungen);
        Assert.Equal("ok", (string)k["hinweise"]![0]!["stufe"]!);
        Assert.Empty(u.Adb.Aufrufe());   // adb wird dafür nicht gestartet
    }

    [Fact]
    public async Task Netz_Endpunkt_ohne_Zugang_und_ohne_Auskunft()
    {
        await using var u = await Umgebung.Neu(zugang: false, ablage: false);
        u.System.Auskunft = new NetzAuskunft(false, "Im Test nicht befragt", [], []);
        var j = (await u.Get("/api/quest/app/netz")).Json!;
        Assert.False((bool)j["gelesen"]!);
        Assert.False((bool)j["zugang"]!["aktiv"]!);
        Assert.Contains(j["hinweise"]!.AsArray(), h => (string)h!["stufe"]! == "info");
    }

    // ---------------------------------------------------------------- VR-Aufträge und die App

    [Fact]
    public async Task VR_Auftrag_mit_verbundener_App_geht_an_die_App_und_startet_keinen_Browser()
    {
        await using var u = await Umgebung.Neu();
        var (port, _, fp) = await u.Xr.Aktiv();
        Assert.True((bool)(await u.Installieren("kabel"))["ok"]!);
        var token = XrServer.Token((JsonObject)JsonNode.Parse(u.Adb.Gelesen("gepusht.txt")!)!);
        var vorher = u.Adb.Aufrufe().Length;
        await using var app = await XrKlient.Verbinden(port, fp, token);
        await app.Hallo("Quest 3");

        var (code, j) = await u.Xr.LokalPost("/api/vr/auftrag", new { modus = "begehen", raum = "R-1.04", geschoss = "EG", autostart = true });
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Equal("app", (string)j!["autostart"]!);
        var m = await app.Warten("auftrag", TimeSpan.FromSeconds(5));
        Assert.Equal("R-1.04", (string)m["auftrag"]!["raum"]!);
        // KICAD hat in der Brille keinen Browser geöffnet (kein adb-Aufruf, keine Weiterleitung für den Browser, kein Fern-Debugging)
        await Task.Delay(300);
        Assert.Equal(vorher, u.Adb.Aufrufe().Length);
        Assert.Null(u.Adb.Gelesen("am_start.log"));
        // auch ohne Wunsch nach Autostart meldet die Antwort, dass der Auftrag über den Kanal an die App ging
        var (c3, j3) = await u.Xr.LokalPost("/api/vr/auftrag", new { modus = "begehen", raum = "R-1.04" });
        Assert.Equal(HttpStatusCode.OK, c3);
        Assert.Equal("app", (string)j3!["autostart"]!);
    }

    // ---------------------------------------------------------------- Nachtrag P7: Kabelweg der App, Weiterleitung, App starten statt Browser

    static bool HatReverse(Adb adb, int port) => (adb.Gelesen("reverse.txt") ?? "").Contains($"tcp:{port} tcp:{port}");

    [Fact]
    public async Task Im_Kabelweg_stellt_die_Automatik_nicht_auf_WLAN_adb_um_und_haelt_die_Weiterleitung_der_App()
    {
        await using var u = await Umgebung.Neu();
        var (port, _, _) = await u.Xr.Aktiv();
        Assert.True((bool)(await u.Installieren("kabel"))["ok"]!);
        Assert.True(HatReverse(u.Adb, port));
        // adb in der Brille startet neu: die Weiterleitung ist weg
        File.Delete(Path.Combine(u.Adb.Ordner, "reverse.txt"));
        u.Adb.Datei("ip.txt", "    inet 192.168.1.50/24 brd 192.168.1.255 scope global wlan0");
        var (code, j) = await u.Xr.LokalPost("/api/quest/automatik", new { anlass = "klick" });
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Equal("bereit", (string)j!["status"]!);
        Assert.Contains("keine Umstellung auf WLAN-adb", (string)j["meldung"]!);
        Assert.DoesNotContain(u.Adb.Aufrufe(), a => a.Contains("tcpip") || a.StartsWith("connect "));
        Assert.True(HatReverse(u.Adb, port), "die Weiterleitung tcp:<Port> der App ist wieder da");
        Assert.Equal("kabel", (string)(await u.Get("/api/quest/app")).Json!["appWeg"]!);
    }

    [Fact]
    public async Task Ohne_Kabelkopplung_stellt_die_Automatik_weiter_auf_WLAN_um()
    {
        await using var u = await Umgebung.Neu();
        u.Adb.Datei("ip.txt", "    inet 192.168.1.50/24 brd 192.168.1.255 scope global wlan0");
        var (_, j) = await u.Xr.LokalPost("/api/quest/automatik", new { anlass = "klick" });
        Assert.Equal("bereit", (string)j!["status"]!);
        Assert.Contains(u.Adb.Aufrufe(), a => a.Contains("tcpip 5555"));
    }

    [Fact]
    public async Task Nach_dem_Widerruf_gilt_der_Kabelweg_nicht_mehr()
    {
        await using var u = await Umgebung.Neu();
        Assert.True((bool)(await u.Installieren("kabel"))["ok"]!);
        var id = (string)(await u.Kopplungen()).Single()!["id"]!;
        await u.Xr.LokalPost("/api/xr/kopplung/widerrufen", new { kopplung = id });
        u.Adb.Datei("ip.txt", "    inet 192.168.1.50/24 brd 192.168.1.255 scope global wlan0");
        var (_, j) = await u.Xr.LokalPost("/api/quest/automatik", new { anlass = "klick" });
        Assert.Equal("bereit", (string)j!["status"]!);
        Assert.Contains(u.Adb.Aufrufe(), a => a.Contains("tcpip 5555"));
        var z = (await u.Get("/api/quest/app")).Json!;
        Assert.Equal(Seriennummer, (string)z["brille"]!["kennung"]!);   // der Dialog vergleicht sie mit den Kopplungen und meldet «neu koppeln»
        Assert.Empty(await u.Kopplungen());
    }

    [Fact]
    public async Task Die_Weiterleitung_der_App_kommt_nach_Verbinden_und_Zustand_auf_den_USB_Weg_zurueck()
    {
        await using var u = await Umgebung.Neu(geraete: [Usb, Wlan()]);
        u.Adb.Verhalten("je_weg");
        var (port, _, _) = await u.Xr.Aktiv();
        Assert.True((bool)(await u.Installieren("kabel"))["ok"]!);
        string Datei(string serie) => "reverse_" + serie.Replace(':', '_') + ".txt";
        Assert.Contains($"tcp:{port}", u.Adb.Gelesen(Datei(Seriennummer)));
        File.Delete(Path.Combine(u.Adb.Ordner, Datei(Seriennummer)));
        await u.Get("/api/quest");   // der Dialog liest den Zustand
        Assert.Contains($"tcp:{port}", u.Adb.Gelesen(Datei(Seriennummer)) ?? "");
        Assert.Null(u.Adb.Gelesen(Datei(WlanWeg)));   // nicht doppelt auf dem WLAN-Weg
        File.Delete(Path.Combine(u.Adb.Ordner, Datei(Seriennummer)));
        var (code, _) = await u.Xr.LokalPost("/api/quest/verbinden", new { });   // Browser-Teil: reverse für den Browser-Port
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Contains($"tcp:{port}", u.Adb.Gelesen(Datei(Seriennummer)) ?? "");
    }

    [Fact]
    public async Task Ohne_Kabelkopplung_aendert_der_Zustand_nichts_an_der_Weiterleitung()
    {
        await using var u = await Umgebung.Neu();
        var (port, _, _) = await u.Xr.Aktiv();
        await u.Get("/api/quest");
        Assert.False(HatReverse(u.Adb, port));
        Assert.DoesNotContain(u.Adb.Aufrufe(), a => a.Contains($"reverse tcp:{port}"));
    }

    [Fact]
    public async Task VR_Auftrag_ohne_verbundene_App_startet_die_installierte_App_statt_des_Browsers()
    {
        await using var u = await Umgebung.Neu();
        Assert.True((bool)(await u.Installieren("kabel"))["ok"]!);
        File.Delete(Path.Combine(u.Adb.Ordner, "app_start.log"));
        File.Delete(Path.Combine(u.Adb.Ordner, "reverse.txt"));
        var (port, _, _) = await u.Xr.Aktiv();
        var (code, j) = await u.Xr.LokalPost("/api/vr/auftrag", new { modus = "begehen", raum = "R-1.04", autostart = true });
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Equal("app_gestartet", (string)j!["autostart"]!);
        Assert.Equal(["ch.hkplanung.kicad.quest/crc6412ab34cd.MainActivity"], u.Adb.Zeilen("app_start.log"));
        Assert.True(HatReverse(u.Adb, port), "vor dem Start ist die Weiterleitung wieder da");
        Assert.DoesNotContain(u.Adb.Aufrufe(), a => a.Contains("android.intent.action.VIEW") || a.Contains("forward tcp:"));   // kein Browser, kein Fern-Debugging
        // der Auftrag wartet im Server, bis die App verbindet (willkommen.auftrag)
        var token = XrServer.Token((JsonObject)JsonNode.Parse(u.Adb.Gelesen("gepusht.txt")!)!);
        var (p2, _, fp) = await u.Xr.Aktiv();
        await using var app = await XrKlient.Verbinden(p2, fp, token);
        var w = await app.Hallo("Quest 3");
        Assert.Equal("R-1.04", (string)w["auftrag"]!["raum"]!);
    }

    [Fact]
    public async Task VR_Auftrag_startet_keine_App_wenn_keine_installiert_oder_gekoppelt_ist_oder_der_Zugang_aus_ist()
    {
        // keine App installiert
        await using (var u = await Umgebung.Neu())
        {
            var (_, j) = await u.Xr.LokalPost("/api/vr/auftrag", new { modus = "begehen", raum = "R-1.04" });
            Assert.Equal("nicht_angefordert", (string)j!["autostart"]!);
            Assert.Null(u.Adb.Gelesen("app_start.log"));
        }
        // widerrufen: die App würde «neu koppeln» melden
        await using (var u = await Umgebung.Neu())
        {
            Assert.True((bool)(await u.Installieren("kabel"))["ok"]!);
            File.Delete(Path.Combine(u.Adb.Ordner, "app_start.log"));
            await u.Xr.LokalPost("/api/xr/kopplung/widerrufen", new { kopplung = (string)(await u.Kopplungen()).Single()!["id"]! });
            var (_, j) = await u.Xr.LokalPost("/api/vr/auftrag", new { modus = "begehen", raum = "R-1.04" });
            Assert.NotEqual("app_gestartet", (string)j!["autostart"]!);
            Assert.Null(u.Adb.Gelesen("app_start.log"));
        }
        // Netzzugang aus
        await using (var u = await Umgebung.Neu(zugang: false))
        {
            u.Adb.Datei("app_installiert.txt", "0.1 1");
            var (_, j) = await u.Xr.LokalPost("/api/vr/auftrag", new { modus = "begehen", raum = "R-1.04" });
            Assert.Equal("nicht_angefordert", (string)j!["autostart"]!);
            Assert.Null(u.Adb.Gelesen("app_start.log"));
        }
    }

    [Fact]
    public async Task Der_Weg_der_Installation_wird_je_PC_gemerkt_und_ist_nur_mit_Kopplung_wirksam()
    {
        await using var u = await Umgebung.Neu();
        Assert.False(File.Exists(u.Quest.AppWegDatei!));
        Assert.True((bool)(await u.Installieren("wlan"))["ok"]!);
        var o = (JsonObject)JsonNode.Parse(File.ReadAllText(u.Quest.AppWegDatei!))!;
        Assert.Equal("wlan", (string)o["weg"]!);
        Assert.Equal(Seriennummer, (string)o["kennung"]!);
        Assert.DoesNotContain("schluessel", File.ReadAllText(u.Quest.AppWegDatei!), StringComparison.OrdinalIgnoreCase);
        Assert.True((bool)(await u.Installieren("kabel"))["ok"]!);
        Assert.Equal("kabel", (string)((JsonObject)JsonNode.Parse(File.ReadAllText(u.Quest.AppWegDatei!))!)["weg"]!);
    }

    [Fact]
    public async Task VR_Auftrag_ohne_App_bleibt_wie_bisher()
    {
        await using var u = await Umgebung.Neu(geraete: []);
        var (code, j) = await u.Xr.LokalPost("/api/vr/auftrag", new { modus = "begehen", raum = "R-1.04" });
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Equal("nicht_angefordert", (string)j!["autostart"]!);
    }

    // ---------------------------------------------------------------- KI

    [Fact]
    public void KI_Einordnung_Quest_App_ist_verboten_und_es_gibt_kein_MCP_Werkzeug()
    {
        var server = Hlks.Mcp.Werkzeugauswahl.Alle();
        foreach (var name in new[] { "quest_app", "quest_app_netz" })
        {
            Assert.Equal("verboten", KiEinordnung.Von(name));
            var f = Assert.Single(KiEinordnung.RechnerFunktionen, x => x.Name == name);
            Assert.True(f.Grund.Length >= 40);
            Assert.DoesNotContain(name, KiEinordnung.Erlaubt);
            Assert.DoesNotContain(name, server);
            Assert.DoesNotContain(name, AuftragDienst.ErlaubteWerkzeuge);
            Assert.DoesNotContain(name, GespraechDienst.McpWerkzeuge(new GespraechEinstellungen(), true));
        }
        Assert.Contains("PowerShell", KiEinordnung.RechnerFunktionen.Single(x => x.Name == "quest_app_netz").Grund);
        Assert.Contains("Kopplungspaket", KiEinordnung.RechnerFunktionen.Single(x => x.Name == "quest_app").Grund);
        Assert.Contains("xr_kanal", KiEinordnung.RechnerFunktionen.Select(x => x.Name));
        Assert.Contains("/api/xr/teilnehmer", KiEinordnung.RechnerFunktionen.Single(x => x.Name == "xr_kanal").Grund);   // die Figur der Brille am PC
    }
}
