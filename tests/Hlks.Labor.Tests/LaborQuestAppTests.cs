using System.Net;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Hlks.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Hlks.Labor.Tests;

/// <summary>
/// «App installieren» im Labor (H+K XR E1, entwurf/xr-empfaenger.md): dieselbe Installation wie in KICAD (QuestApp.cs) mit derselben App «H+K XR» (ch.hkplanung.xr) und
/// derselben Ablage (hk_xr); das Labor ist nur ein anderes Werkzeug der App: Eingangsdatei kopplung_xrlabor.json, Paket mit werkzeug «xrlabor» (Fassung 2), Port des Labors.
/// Statt adb.exe läuft die Attrappe tests/Hlks.AdbAttrappe (nie ein echtes adb).
/// </summary>
public class LaborQuestAppTests
{
    const string Seriennummer = "1WMHH000000000";
    const string ApkName = "ch.hkplanung.xr-Signed.apk";
    const string Eingang = "/sdcard/Android/data/ch.hkplanung.xr/files";

    sealed class Adb
    {
        public string Ordner { get; }
        public string Pfad => Path.Combine(Ordner, "adb.exe");
        public Adb(string ordner)
        {
            Ordner = ordner;
            Directory.CreateDirectory(ordner);
            File.Copy(Path.Combine(AppContext.BaseDirectory, "Hlks.AdbAttrappe.exe"), Pfad, true);
            foreach (var n in new[] { "Hlks.AdbAttrappe.dll", "Hlks.AdbAttrappe.runtimeconfig.json", "Hlks.AdbAttrappe.deps.json" })
                File.Copy(Path.Combine(AppContext.BaseDirectory, n), Path.Combine(ordner, n), true);
            File.WriteAllLines(Path.Combine(ordner, "geraete.txt"), [Seriennummer + "\tdevice"]);
        }
        public string[] Aufrufe() => Zeilen("aufrufe.log");
        public string[] Zeilen(string name) => File.Exists(Path.Combine(Ordner, name)) ? File.ReadAllLines(Path.Combine(Ordner, name)).Where(z => z.Length > 0).ToArray() : [];
        public string? Gelesen(string name) => File.Exists(Path.Combine(Ordner, name)) ? File.ReadAllText(Path.Combine(Ordner, name)) : null;
        public Adb Datei(string name, string inhalt) { File.WriteAllText(Path.Combine(Ordner, name), inhalt); return this; }
    }

    sealed class Umgebung : IAsyncDisposable
    {
        public string Wurzel { get; } = Path.Combine(Path.GetTempPath(), "hlks_laborapp_" + Guid.NewGuid().ToString("N")[..10]);
        public Adb Adb { get; }
        public string Ablage => Path.Combine(Wurzel, "hk_xr");
        public string Apk => Path.Combine(Ablage, ApkName);
        public LaborServer Labor { get; }

        public Umgebung()
        {
            Directory.CreateDirectory(Wurzel);
            Adb = new Adb(Path.Combine(Wurzel, "adb"));
            Directory.CreateDirectory(Ablage);
            var bytes = RandomNumberGenerator.GetBytes(6000);
            File.WriteAllBytes(Apk, bytes);
            File.WriteAllText(Path.Combine(Ablage, "version.json"), new JsonObject
            {
                ["datei"] = ApkName, ["commit"] = "abc1234", ["zeit"] = "2026-10-26T10:00:00", ["groesse"] = bytes.Length,
                ["sha256"] = Convert.ToHexStringLower(SHA256.HashData(bytes)), ["versionName"] = "0.1", ["versionCode"] = 1,
            }.ToJsonString());
            Labor = new LaborServer(zugang: true, dienste: s => s.AddSingleton(sp => LaborQuest.Vorgabe(true, sp.GetRequiredService<XrEinstellungen>()) with
            {
                Zeitlimit = TimeSpan.FromSeconds(10), PfadDurchsuchen = false, Orte = [], WeiterleitungGrenze = TimeSpan.FromMilliseconds(500),
                AppAblage = Ablage, PaketOrdner = Path.Combine(Wurzel, "paket_temp"), AppWegDatei = Path.Combine(Wurzel, "weg", "weg.json"), InstallZeitlimit = TimeSpan.FromSeconds(30),
            }));
            Labor.BenutzerEinstellungSetzen("quest.adb", JsonValue.Create(Adb.Pfad));   // dieselbe Einstellung wie in KICAD; das Labor liest sie nur
        }

        public async ValueTask DisposeAsync()
        {
            await Labor.DisposeAsync();
            await LaborServer.Loeschen(Wurzel);
        }
    }

    [Fact]
    public async Task Zustand_nennt_die_App_H_K_XR_mit_dem_Werkzeug_Labor_und_fragt_die_Brille_nach_ihr()
    {
        await using var u = new Umgebung();
        var (code, j) = await u.Labor.Get("/api/quest/app");
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Equal("ch.hkplanung.xr", (string)j!["paket"]!);
        Assert.Equal("H+K XR", (string)j["app"]!);
        Assert.Equal("xrlabor", (string)j["werkzeug"]!);
        Assert.Equal("XR-Labor", (string)j["werkzeugName"]!);
        Assert.Equal(u.Ablage, (string)j["ablage"]!["ordner"]!);
        Assert.Null(j["ablage"]!["fehler"]);
        Assert.Equal(ApkName, (string)j["ablage"]!["version"]!["datei"]!);
        Assert.True((bool)j["adbGefunden"]!);
        Assert.Equal(Seriennummer, (string)j["brille"]!["seriennummer"]!);
        Assert.Empty(j["brille"]!["alteApps"]!.AsArray());
        var aufrufe = u.Adb.Aufrufe();
        Assert.Contains(aufrufe, a => a.EndsWith(" shell dumpsys package ch.hkplanung.xr", StringComparison.Ordinal));
        Assert.DoesNotContain(aufrufe, a => a.Contains("kicad.quest", StringComparison.Ordinal));
        Assert.DoesNotContain(aufrufe, a => a.Contains("xrlabor", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ohne_abgelegte_App_nennt_die_Meldung_das_Bereitstellungsskript_ohne_Schalter_Labor()
    {
        await using var u = new Umgebung();
        File.Delete(Path.Combine(u.Ablage, "version.json"));
        var (_, j) = await u.Labor.Get("/api/quest/app");
        var fehler = (string)j!["ablage"]!["fehler"]!;
        Assert.Contains(@"werkzeuge\quest_app_bereitstellen.ps1", fehler);
        Assert.DoesNotContain("-Labor", fehler);
    }

    [Fact]
    public async Task Installieren_ueber_Kabel_legt_kopplung_xrlabor_json_ab_und_koppelt_das_Labor()
    {
        await using var u = new Umgebung();
        var (port, _, fp) = await u.Labor.Aktiv();
        var (code, e) = await u.Labor.LokalPost("/api/quest/app/installieren", new { weg = "kabel" });
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.True((bool)e!["ok"]!, e.ToJsonString());
        Assert.Equal(["zugang", "apk", "brille", "installieren", "kabel", "uebertragen", "starten"], e["schritte"]!.AsArray().Select(s => (string)s!["id"]!).ToArray());
        var aufrufe = u.Adb.Aufrufe();
        Assert.Contains(aufrufe, a => a.EndsWith(" install -r " + u.Apk, StringComparison.Ordinal));
        Assert.Contains(aufrufe, a => a.EndsWith($" reverse tcp:{port} tcp:{port}", StringComparison.Ordinal));   // der Netzzugang des Labors über das Kabel
        Assert.Contains(aufrufe, a => a.EndsWith($" shell mkdir -p {Eingang}", StringComparison.Ordinal));
        Assert.Equal([Eingang + "/kopplung_xrlabor.json"], u.Adb.Zeilen("push_ziel.txt"));
        Assert.DoesNotContain(aufrufe, a => a.Contains("kicad.quest", StringComparison.Ordinal) || a.Contains("ch.hkplanung.xrlabor", StringComparison.Ordinal));
        Assert.Equal(["ch.hkplanung.xr/crc6412ab34cd.MainActivity"], u.Adb.Zeilen("app_start.log"));
        // das Paket in der Brille: Fassung 2, Werkzeug des Labors, Fingerabdruck und Port des Labors
        var paket = (JsonObject)JsonNode.Parse(u.Adb.Gelesen("gepusht_xrlabor.txt")!)!;
        Assert.Equal(2, (int)paket["fassung"]!);
        Assert.Equal("xrlabor", (string)paket["werkzeug"]!);
        Assert.Equal("XR-Labor", (string)paket["werkzeugName"]!);
        Assert.Equal(port, (int)paket["port"]!);
        Assert.Equal(fp, (string)paket["fingerabdruck"]!);
        Assert.Null(u.Adb.Gelesen("gepusht_kicad.txt"));   // KICADs Paket bleibt unberührt
        // die Kopplung steht im Ordner des Labors, am PC bleibt kein Paket liegen
        Assert.Single((await u.Labor.Zugang())["kopplungen"]!.AsArray());
        Assert.Empty(Directory.Exists(Path.Combine(u.Wurzel, "paket_temp")) ? Directory.GetFiles(Path.Combine(u.Wurzel, "paket_temp"), "*", SearchOption.AllDirectories) : []);
        Assert.True(File.Exists(Path.Combine(u.Wurzel, "weg", "weg.json")));
    }

    [Fact]
    public async Task Scheitert_das_Uebertragen_ist_die_Kopplung_zurueckgenommen_und_kein_Paket_liegt_am_PC()
    {
        await using var u = new Umgebung();
        u.Adb.Datei("verhalten.txt", "push_fehler\n");
        await u.Labor.Aktiv();
        var (code, e) = await u.Labor.LokalPost("/api/quest/app/installieren", new { weg = "kabel" });
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.False((bool)e!["ok"]!, e.ToJsonString());
        Assert.Equal("uebertragen", (string)e["schritte"]!.AsArray().Last()!["id"]!);
        Assert.Empty((await u.Labor.Zugang())["kopplungen"]!.AsArray());
        Assert.Empty(Directory.Exists(Path.Combine(u.Wurzel, "paket_temp")) ? Directory.GetFiles(Path.Combine(u.Wurzel, "paket_temp"), "*", SearchOption.AllDirectories) : []);
        Assert.False(File.Exists(Path.Combine(u.Wurzel, "weg", "weg.json")));
    }

    [Fact]
    public async Task Alte_Apps_entfernen_geht_auch_vom_Labor_aus_nur_mit_Bestaetigung_und_nur_die_zwei_Pakete()
    {
        await using var u = new Umgebung();
        u.Adb.Datei("alte_apps.txt", "ch.hkplanung.kicad.quest\nch.hkplanung.xrlabor\nch.hkplanung.fremd\n");
        u.Adb.Datei("app_installiert.txt", "0.1 1");
        var (_, z) = await u.Labor.Get("/api/quest/app");
        Assert.Equal(["ch.hkplanung.kicad.quest", "ch.hkplanung.xrlabor"], z!["brille"]!["alteApps"]!.AsArray().Select(a => (string)a!["paket"]!).ToArray());
        var (ohne, _) = await u.Labor.LokalPost("/api/quest/app/alte-entfernen", new { });
        Assert.Equal(HttpStatusCode.BadRequest, ohne);
        Assert.Null(u.Adb.Gelesen("uninstall.log"));
        var (code, j) = await u.Labor.LokalPost("/api/quest/app/alte-entfernen", new { bestaetigt = true });
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.True((bool)j!["ok"]!, j.ToJsonString());
        Assert.Equal(["ch.hkplanung.kicad.quest", "ch.hkplanung.xrlabor"], u.Adb.Zeilen("uninstall.log"));
        Assert.Equal(["ch.hkplanung.fremd"], u.Adb.Zeilen("alte_apps.txt"));
        Assert.NotNull(u.Adb.Gelesen("app_installiert.txt"));   // «H+K XR» bleibt
    }
}
