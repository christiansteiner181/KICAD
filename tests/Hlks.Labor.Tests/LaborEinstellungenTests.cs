using System.Net;
using Hlks.Web;

namespace Hlks.Labor.Tests;

/// <summary>
/// Werte des XR-Labors (L0, entwurf/labor-app.md «Ports und Prozesse»): Ports 5430 / 5340 / 5341, Ordner …\xr\labor mit dem Zertifikat von KICAD, eigene App-Marke,
/// eigene Umgebungsvariable für Testserver – und KICADs Vorgaben unverändert daneben.
/// </summary>
public class LaborEinstellungenTests
{
    static string Lokal => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    static string Temp => Path.GetFullPath(Path.GetTempPath());

    [Fact]
    public void Betrieb_Ports_des_Labors_eigener_Ordner_Zertifikat_von_KICAD()
    {
        var b = LaborXr.Vorgabe(false, _ => "5439");   // im Betrieb wirkt keine Umgebungsvariable
        Assert.Equal(IPAddress.Any, b.Adresse);
        Assert.Null(b.Port);
        Assert.Null(b.SuchPort);
        Assert.Equal(5340, b.PortStandard);
        Assert.Equal(5341, b.SuchPortStandard);
        Assert.Equal(5430, b.DesktopPort);
        Assert.Equal("XR-Labor", b.ServerName);
        Assert.Equal(Path.Combine(Lokal, "HLKS_CAD", "xr", "labor"), b.OrdnerPfad());
        Assert.Equal(Path.Combine(Lokal, "HLKS_CAD", "xr"), b.ZertifikatOrdnerPfad());
        Assert.Equal(XrEinstellungen.StandardOrdner(), b.ZertifikatOrdnerPfad());   // dasselbe Zertifikat je PC wie KICAD
        Assert.NotEqual(XrEinstellungen.Betrieb().OrdnerPfad(), b.OrdnerPfad());    // eigene zugang.json und kopplungen.json
        using (var z = new XrZertifikat(b)) Assert.Equal(Path.Combine(Lokal, "HLKS_CAD", "xr"), Path.GetDirectoryName(z.DateiPfad));   // nur der Pfad, nichts angelegt
        Assert.Equal(Path.Combine(Lokal, "HLKS_CAD", "xr", "labor", "zugang.json"), new XrPcEinstellung(b).DateiPfad);
        Assert.Equal(Path.Combine(Lokal, "HLKS_CAD", "xr", "labor", "kopplungen.json"), new XrKopplungen(b).DateiPfad);
        Assert.Equal(LaborXr.DesktopPort, LaborProgramm.PortVorgabe);
    }

    [Fact]
    public void Testlauf_nur_127_0_0_1_freie_Ports_alles_unter_TEMP()
    {
        var t = LaborXr.Vorgabe();   // der Testrunner gilt als Testlauf
        Assert.Equal(IPAddress.Loopback, t.Adresse);
        Assert.Equal(0, t.Port);
        Assert.Equal(0, t.SuchPort);
        Assert.StartsWith(Temp, t.OrdnerPfad());
        Assert.StartsWith(Temp, t.ZertifikatOrdnerPfad());
        Assert.Equal(Path.Combine(Temp, "HLKS_CAD", "xr_test", "labor"), t.OrdnerPfad());
        Assert.Equal(Path.Combine(Temp, "HLKS_CAD", "xr_test"), t.ZertifikatOrdnerPfad());
        Assert.DoesNotContain(Path.Combine(Lokal, "HLKS_CAD"), t.OrdnerPfad());   // %TEMP% liegt oft unter %LOCALAPPDATA%, aber nie in …\HLKS_CAD
        Assert.DoesNotContain(Path.Combine(Lokal, "HLKS_CAD"), t.ZertifikatOrdnerPfad());
        Assert.Equal(5340, t.PortStandard);
        Assert.Equal("XR-Labor", t.ServerName);
    }

    [Fact]
    public void Testserver_eigene_Umgebungsvariable_fuer_den_Desktop_Port_KICADs_gilt_nicht()
    {
        Func<string, string?> Mit(string name, string? wert) => n => n == name ? wert : null;
        Assert.Equal("HLKS_LABOR_DESKTOP_PORT", LaborXr.DesktopPortVariable);
        Assert.Equal(5439, LaborXr.Vorgabe(true, Mit(LaborXr.DesktopPortVariable, "5439")).DesktopPort);
        Assert.Equal(5430, LaborXr.Vorgabe(true, Mit(LaborXr.DesktopPortVariable, null)).DesktopPort);
        foreach (var w in new[] { "", "abc", "80", "70000", "+5439" })
            Assert.Equal(5430, LaborXr.Vorgabe(true, Mit(LaborXr.DesktopPortVariable, w)).DesktopPort);
        // KICADs Variable (quest_pruefen.ps1) bewegt das Labor nicht – und die des Labors nicht KICAD
        Assert.Equal(5430, LaborXr.Vorgabe(true, Mit(XrEinstellungen.DesktopPortVariable, "5139")).DesktopPort);
        Assert.Equal(5130, XrEinstellungen.Vorgabe(true, Mit(LaborXr.DesktopPortVariable, "5439")).DesktopPort);
    }

    [Fact]
    public void KICAD_Vorgaben_bleiben_wie_sie_waren()
    {
        var k = XrEinstellungen.Betrieb();
        Assert.Equal(5140, k.PortStandard);
        Assert.Equal(5141, k.SuchPortStandard);
        Assert.Equal(5130, k.DesktopPort);
        Assert.Equal("KICAD", k.ServerName);
        Assert.Null(k.ZertifikatOrdner);
        Assert.Equal(Path.Combine(Lokal, "HLKS_CAD", "xr"), k.OrdnerPfad());
        Assert.Equal(k.OrdnerPfad(), k.ZertifikatOrdnerPfad());
        Assert.Equal(Path.Combine(Temp, "HLKS_CAD", "xr_test"), XrEinstellungen.Vorgabe(true, _ => null).OrdnerPfad());
        var q = new QuestEinstellungen();
        Assert.Equal(QuestAppMarke.Kicad, q.AppMarke);
        Assert.Equal("ch.hkplanung.kicad.quest", QuestAppMarke.Kicad.Paket);
        Assert.Equal("quest.app_ablage", QuestAppMarke.Kicad.AblageSchluessel);
        Assert.Equal("quest_app", QuestAppMarke.Kicad.AblageOrdner);
        Assert.Equal(QuestDienst.AppEingang, QuestAppMarke.Kicad.Eingang);
        Assert.Equal(QuestAppMarke.Kicad, QuestEinstellungen.Vorgabe(true).AppMarke);
        Assert.Equal(QuestAppMarke.Kicad, QuestEinstellungen.Vorgabe(false).AppMarke);
    }

    [Fact]
    public void App_Marke_des_Labors()
    {
        var m = LaborMarke.App;
        Assert.Equal("ch.hkplanung.xrlabor", m.Paket);
        Assert.Equal("XR-Labor", m.Name);
        Assert.Equal("labor.app_ablage", m.AblageSchluessel);
        Assert.Equal("labor_app", m.AblageOrdner);
        Assert.Equal("/sdcard/Android/data/ch.hkplanung.xrlabor/files", m.Eingang);
        Assert.Contains("-Labor", m.Bereitstellen);
        Assert.NotEqual(QuestAppMarke.Kicad.Paket, m.Paket);
        Assert.NotEqual(QuestAppMarke.Kicad.AblageSchluessel, m.AblageSchluessel);
        Assert.NotEqual(QuestAppMarke.Kicad.AblageOrdner, m.AblageOrdner);
    }

    [Theory]
    [InlineData("ch.hkplanung.xrlabor; rm -rf /")]
    [InlineData("-s ch.x")]
    [InlineData("ohnepunkt")]
    [InlineData("Ch.Gross.Buchstaben")]
    [InlineData("")]
    public void App_Marke_nimmt_nur_gueltige_Paketnamen(string paket) =>
        Assert.Throws<ArgumentException>(() => new QuestAppMarke(paket, "X", "labor.app_ablage", "labor_app", "x"));

    [Fact]
    public void App_Marke_prueft_Ordnername_und_Schluessel()
    {
        Assert.Throws<ArgumentException>(() => new QuestAppMarke("ch.a.b", "X", "labor.app_ablage", @"..\weg", "x"));
        Assert.Throws<ArgumentException>(() => new QuestAppMarke("ch.a.b", "X", "Gross", "labor_app", "x"));
    }

    [Fact]
    public void Installation_im_Betrieb_eigene_Ablage_und_eigener_Weg_neben_dem_Schalter_des_Labors()
    {
        var xr = LaborXr.Vorgabe(false, _ => null);
        var q = LaborQuest.Vorgabe(false, xr);
        Assert.Equal(LaborMarke.App, q.AppMarke);
        Assert.Null(q.AppAblage);   // Vorgabe aus der Marke: %USERPROFILE%\HLKS_CAD_werkzeuge\labor_app
        Assert.Equal(Path.Combine(Lokal, "HLKS_CAD", "xr", "labor", "quest_app_weg.json"), q.AppWegDatei);
        Assert.True(q.PfadDurchsuchen);
        var ablage = Path.Combine(Path.GetTempPath(), "hlks_labor_ablage_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Directory.CreateDirectory(ablage);
            var dienst = new QuestDienst(new BenutzerEinstellungen(new RechnerAblage(ablage, "testpc", true, "einstellungen.json", "Umleitung")), q);
            var profil = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            Assert.Equal((Path.Combine(profil, "HLKS_CAD_werkzeuge", "labor_app"), "vorgabe"), dienst.AppOrdner());
            Assert.Equal(LaborMarke.App, dienst.Marke);
        }
        finally { Directory.Delete(ablage, true); }
    }

    [Fact]
    public void Installation_im_Testlauf_nie_ein_echtes_adb_alles_unter_TEMP()
    {
        var q = LaborQuest.Vorgabe(true, LaborXr.Vorgabe(true, _ => null));
        Assert.Equal(LaborMarke.App, q.AppMarke);
        Assert.False(q.PfadDurchsuchen);
        Assert.All(q.Orte!, o => Assert.StartsWith(Temp, Path.GetFullPath(o)));
        Assert.StartsWith(Temp, Path.GetFullPath(q.AppAblage!));
        Assert.NotEqual(QuestEinstellungen.Vorgabe(true).AppAblage, q.AppAblage);
        Assert.Null(q.AppWegDatei);   // je Prozess unter %TEMP% (QuestDienst), wie bei KICAD
    }

    [Theory]
    [InlineData("0.1.0+3de90521ac09fedb2618a3eb82e79a6b59e148d3", "0.1.0", "3de90521ac09fedb2618a3eb82e79a6b59e148d3")]
    [InlineData("0.1.0+ABC1234", "0.1.0", "abc1234")]
    [InlineData("0.1.0+abc1234.dirty", "0.1.0", "abc1234")]
    [InlineData("0.1.0", "0.1.0", null)]
    [InlineData("0.1.0+kein-commit", "0.1.0", null)]
    [InlineData(null, "0.1.0", null)]
    public void Fassung_und_Commit_aus_der_Programmdatei(string? informationell, string fassung, string? commit) =>
        Assert.Equal((fassung, commit), LaborStatus.Version(informationell, "0.1.0"));

    [Theory]
    [InlineData("/css/hlks.css", true)]
    [InlineData("/js/oberflaeche/quest-app-dialog.js", true)]
    [InlineData("/JS/api.js", true)]
    [InlineData("/index.html", false)]
    [InlineData("/vr.html", false)]
    [InlineData("/lib/three/three.module.js", false)]
    [InlineData("/vr-symbole/x.svg", false)]
    [InlineData("/css/", false)]
    [InlineData("/css", false)]
    [InlineData("/css/../index.html", false)]
    [InlineData("/css\\..\\index.html", false)]
    [InlineData("/js/C:/x.js", false)]
    [InlineData("", false)]
    public void Aus_KICAD_nur_css_und_js(string pfad, bool erlaubt) =>
        Assert.Equal(erlaubt, NurOrdner.Erlaubt(pfad, LaborOberflaeche.KicadOrdner));

    [Fact]
    public void Wurzeln_der_Oberflaeche_Umgebung_Quelle_Kopie_und_KICAD_aus_derselben_Arbeitskopie()
    {
        var w = Path.Combine(Path.GetTempPath(), "hlks_labor_wurzel_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            string Seite(params string[] teile)
            {
                var o = Path.Combine([w, .. teile]);
                Directory.CreateDirectory(o);
                File.WriteAllText(Path.Combine(o, "index.html"), "<!doctype html>");
                return o;
            }
            var kopie = Path.Combine(w, "bin");
            var umgebung = Seite("arbeitskopie", "src", "Hlks.Labor", "wwwroot");
            var kicadDaneben = Seite("arbeitskopie", "src", "Hlks.Web", "wwwroot");
            var quelle = Seite("quelle", "src", "Hlks.Labor", "wwwroot");
            var kicadQuelle = Seite("quelle_kicad");
            Assert.Equal(umgebung, LaborOberflaeche.LaborWurzel(umgebung, quelle, kopie));
            Assert.Equal(quelle, LaborOberflaeche.LaborWurzel(Path.Combine(w, "fehlt"), quelle, kopie));
            Assert.Equal(quelle, LaborOberflaeche.LaborWurzel(null, quelle, kopie));
            Assert.Equal(Path.Combine(kopie, "labor_wwwroot"), LaborOberflaeche.LaborWurzel(null, Path.Combine(w, "fehlt"), kopie));
            // KICAD: zuerst dieselbe Arbeitskopie wie das Labor, dann die Quelle von Hlks.Web beim Bau, dann die Kopie neben dem Programm
            Assert.Equal(kicadDaneben, LaborOberflaeche.KicadWurzel(umgebung, kicadQuelle, kopie));
            Assert.Equal(kicadQuelle, LaborOberflaeche.KicadWurzel(quelle, kicadQuelle, kopie));
            Assert.Equal(Path.Combine(kopie, "wwwroot"), LaborOberflaeche.KicadWurzel(Path.Combine(kopie, "labor_wwwroot"), null, kopie));
        }
        finally { Directory.Delete(w, true); }
    }
}
