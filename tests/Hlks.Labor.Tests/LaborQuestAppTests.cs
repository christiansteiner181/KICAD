using System.Net;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Hlks.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Hlks.Labor.Tests;

/// <summary>
/// «App installieren» im Labor (L0): dieselbe Installation wie in KICAD (QuestApp.cs), aber mit der Marke des Labors – Paket ch.hkplanung.xrlabor, Eingang
/// /sdcard/Android/data/ch.hkplanung.xrlabor/files, eigene Ablage, Port des Labors. Statt adb.exe läuft die Attrappe tests/Hlks.AdbAttrappe (nie ein echtes adb).
/// Die Attrappe kennt Eingang, Start und dumpsys nur für die KICAD-App: Bei der Labor-App scheitert darum das Anlegen des Eingangs – geprüft wird, dass alle adb-Aufrufe
/// die Kennung des Labors tragen, keiner die von KICAD, und dass die Kopplung nach dem gescheiterten Übertragen zurückgenommen ist.
/// </summary>
public class LaborQuestAppTests
{
    const string Seriennummer = "1WMHH000000000";
    const string ApkName = "ch.hkplanung.xrlabor-Signed.apk";

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
        public string[] Aufrufe() => File.Exists(Path.Combine(Ordner, "aufrufe.log")) ? File.ReadAllLines(Path.Combine(Ordner, "aufrufe.log")).Where(z => z.Length > 0).ToArray() : [];
    }

    sealed class Umgebung : IAsyncDisposable
    {
        public string Wurzel { get; } = Path.Combine(Path.GetTempPath(), "hlks_laborapp_" + Guid.NewGuid().ToString("N")[..10]);
        public Adb Adb { get; }
        public string Ablage => Path.Combine(Wurzel, "labor_app");
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
    public async Task Zustand_nennt_die_Labor_App_und_fragt_die_Brille_nach_ihr()
    {
        await using var u = new Umgebung();
        var (code, j) = await u.Labor.Get("/api/quest/app");
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Equal("ch.hkplanung.xrlabor", (string)j!["paket"]!);
        Assert.Equal(u.Ablage, (string)j["ablage"]!["ordner"]!);
        Assert.Null(j["ablage"]!["fehler"]);
        Assert.Equal(ApkName, (string)j["ablage"]!["version"]!["datei"]!);
        Assert.True((bool)j["adbGefunden"]!);
        Assert.Equal(Seriennummer, (string)j["brille"]!["seriennummer"]!);
        var aufrufe = u.Adb.Aufrufe();
        Assert.Contains(aufrufe, a => a.EndsWith(" shell dumpsys package ch.hkplanung.xrlabor", StringComparison.Ordinal));
        Assert.DoesNotContain(aufrufe, a => a.Contains("kicad.quest", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ohne_abgelegte_App_nennt_die_Meldung_das_Werkzeug_mit_Labor()
    {
        await using var u = new Umgebung();
        File.Delete(Path.Combine(u.Ablage, "version.json"));
        var (_, j) = await u.Labor.Get("/api/quest/app");
        Assert.Contains(@"werkzeuge\quest_app_bereitstellen.ps1 -Labor", (string)j!["ablage"]!["fehler"]!);
    }

    [Fact]
    public async Task Installieren_ueber_Kabel_mit_Paket_Eingang_und_Port_des_Labors()
    {
        await using var u = new Umgebung();
        var (port, _, _) = await u.Labor.Aktiv();
        var (code, e) = await u.Labor.LokalPost("/api/quest/app/installieren", new { weg = "kabel" });
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.False((bool)e!["ok"]!, e.ToJsonString());
        Assert.Equal(["zugang", "apk", "brille", "installieren", "kabel", "uebertragen"], e["schritte"]!.AsArray().Select(s => (string)s!["id"]!).ToArray());
        Assert.False((bool)e["schritte"]!.AsArray().Last()!["ok"]!);
        Assert.Contains("Eingang der App", (string)e["fehler"]!);
        var aufrufe = u.Adb.Aufrufe();
        Assert.Contains(aufrufe, a => a.EndsWith(" install -r " + u.Apk, StringComparison.Ordinal));
        Assert.Contains(aufrufe, a => a.EndsWith($" reverse tcp:{port} tcp:{port}", StringComparison.Ordinal));   // der Netzzugang des Labors über das Kabel
        Assert.Contains(aufrufe, a => a.EndsWith(" shell mkdir -p /sdcard/Android/data/ch.hkplanung.xrlabor/files", StringComparison.Ordinal));
        Assert.DoesNotContain(aufrufe, a => a.Contains("kicad.quest", StringComparison.Ordinal));
        // kein Schlüssel ohne Brille: die frisch angelegte Kopplung ist zurückgenommen, und kein Paket liegt mehr am PC
        Assert.Empty((await u.Labor.Zugang())["kopplungen"]!.AsArray());
        Assert.Empty(Directory.Exists(Path.Combine(u.Wurzel, "paket_temp")) ? Directory.GetFiles(Path.Combine(u.Wurzel, "paket_temp"), "*", SearchOption.AllDirectories) : []);
        Assert.False(File.Exists(Path.Combine(u.Wurzel, "weg", "weg.json")));
    }
}
