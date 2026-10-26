using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Hlks.Kern;
using Microsoft.Extensions.DependencyInjection;

namespace Hlks.Web.Tests;

/// <summary>
/// «Netz einrichten …» per Knopf (Christian 26.10.2026): QuestAppEinrichten.cs, Endpunkt POST /api/quest/app/netz/einrichten, Skripte im Bauordner. Es startet nie ein Programm:
/// der Prozessstart ist durch eine Attrappe ersetzt (IQuestAppEinrichten bzw. der Startdelegat von QuestAppEinrichtenEcht); Netz und Firewall dieses PCs bleiben unberührt.
/// </summary>
public class NetzEinrichtenTests
{
    sealed class Einrichten : IQuestAppEinrichten
    {
        public List<EinrichtenAuftrag> Auftraege { get; } = [];
        public EinrichtenErgebnis Antwort { get; set; } = new(true, null);
        public Task<EinrichtenErgebnis>? Warten { get; set; }
        public async Task<EinrichtenErgebnis> Starten(EinrichtenAuftrag auftrag, CancellationToken abbruch)
        {
            lock (Auftraege) Auftraege.Add(auftrag);
            if (Warten is not null) await Warten;
            return Antwort;
        }
    }

    sealed class NetzSystem : IQuestAppSystem
    {
        public NetzAuskunft Auskunft { get; set; } = new(true, null, [new NetzProfil("s-net 3", "WLAN", "Public")], []);
        public Task<NetzAuskunft> NetzLesen(CancellationToken abbruch) => Task.FromResult(Auskunft);
    }

    sealed class Lan : IQuestNetz
    {
        public IReadOnlyList<NachbarEintrag> Nachbarn() => [];
        public IReadOnlyList<NetzSchnittstelle> Schnittstellen() => [new("WLAN", "Intel(R) Wi-Fi 6E", "Wireless80211", "192.168.1.20", 24)];
        public Task<bool> Anpingen(string ip, TimeSpan zeitgrenze, CancellationToken abbruch) => Task.FromResult(false);
    }

    static XrServer Server(Einrichten e, NetzSystem? s = null) =>
        new(zugang: false, dienste: d => { d.AddSingleton<IQuestAppEinrichten>(e); d.AddSingleton<IQuestAppSystem>(s ?? new NetzSystem()); d.AddSingleton<IQuestNetz>(new Lan()); });

    const string Pfad = "/api/quest/app/netz/einrichten";

    static readonly IReadOnlyList<NetzSchnittstelle> Echte = [new("WLAN", "Intel(R) Wi-Fi 6E", "Wireless80211", "192.168.1.20", 24)];

    // ---------------------------------------------------------------- Befehlszeile, Pfad, Skripte im Bauordner

    [Fact]
    public void Befehlszeile_und_Pfad_sind_fest()
    {
        var skript = QuestAppEinrichten.SkriptPfad(@"C:\Programme\HLKS");
        Assert.Equal(@"C:\Programme\HLKS\werkzeuge\xr_netz_einrichten.ps1", skript.Replace('/', '\\'));
        var a = QuestAppEinrichten.Auftrag(skript);
        Assert.EndsWith("powershell.exe", a.Programm, StringComparison.OrdinalIgnoreCase);
        Assert.Equal($"-NoProfile -ExecutionPolicy Bypass -File \"{skript}\" -Erhoeht -Ja", a.Argumente);
        Assert.Equal(skript, a.Skript);
        Assert.Equal(Path.GetDirectoryName(skript), a.Ordner);
        // ohne Angabe: der Programmordner (AppContext.BaseDirectory)
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "werkzeuge", "xr_netz_einrichten.ps1"), QuestAppEinrichten.SkriptPfad());
    }

    [Fact]
    public void Die_Skripte_liegen_im_Bauordner_und_sind_die_aus_dem_Repository()
    {
        foreach (var name in new[] { "xr_netz_einrichten.ps1", "xr_firewall.ps1" })
        {
            var datei = Path.Combine(AppContext.BaseDirectory, "werkzeuge", name);
            Assert.True(File.Exists(datei), datei);
            Assert.All(File.ReadAllBytes(datei), b => Assert.True(b < 128, $"{name}: nur ASCII (PowerShell 5.1)"));
        }
        var text = File.ReadAllText(QuestAppEinrichten.SkriptPfad());
        Assert.Contains("[switch]$Erhoeht", text);   // die Schalter der Befehlszeile gibt es im Skript
        Assert.Contains("[switch]$Ja", text);
        // xr_netz_einrichten.ps1 ruft xr_firewall.ps1 aus seinem eigenen Ordner: beide liegen nebeneinander
        Assert.Contains("Join-Path $PSScriptRoot 'xr_firewall.ps1'", text);
        // und die Kopie im Bauordner ist die aktuelle (nicht eine alte Fassung)
        var wurzel = AppContext.BaseDirectory;
        while (wurzel is not null && !File.Exists(Path.Combine(wurzel, "HLKS_CAD.slnx"))) wurzel = Path.GetDirectoryName(wurzel);
        if (wurzel is null) return;   // Bauordner ausserhalb des Repositorys (HLKS_BUILD): nicht vergleichbar
        Assert.Equal(File.ReadAllText(Path.Combine(wurzel, "werkzeuge", "xr_netz_einrichten.ps1")), text);
    }

    // ---------------------------------------------------------------- Endpunkt

    [Fact]
    public async Task Ohne_Bestaetigung_wird_nichts_gestartet()
    {
        var e = new Einrichten();
        await using var s = Server(e);
        foreach (var k in new object[] { new { }, new { bestaetigt = false }, new { bestaetigt = "true" }, new { bestaetigt = 1 }, new { bestaet = true } })
            Assert.Equal(HttpStatusCode.BadRequest, (await s.LokalPost(Pfad, k)).Code);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await s.Lokal.GetAsync(Pfad)).StatusCode);
        Assert.Empty(e.Auftraege);
    }

    [Fact]
    public async Task Mit_Bestaetigung_startet_der_Server_das_Skript_mit_der_festen_Befehlszeile()
    {
        var e = new Einrichten();
        await using var s = Server(e);
        var (code, j) = await s.LokalPost(Pfad, new { bestaetigt = true });
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.True((bool)j!["gestartet"]!);
        Assert.Null(j["grund"]);
        var a = Assert.Single(e.Auftraege);
        var skript = QuestAppEinrichten.SkriptPfad();
        Assert.Equal($"-NoProfile -ExecutionPolicy Bypass -File \"{skript}\" -Erhoeht -Ja", a.Argumente);
        Assert.Equal(skript, a.Skript);
        Assert.True(File.Exists(a.Skript));
    }

    [Fact]
    public async Task Abgelehnte_Windows_Rueckfrage_ergibt_gestartet_false_mit_Grund()
    {
        var e = new Einrichten { Antwort = new EinrichtenErgebnis(false, QuestAppEinrichten.GrundAbgebrochen) };
        await using var s = Server(e);
        var (code, j) = await s.LokalPost(Pfad, new { bestaetigt = true });
        Assert.Equal(HttpStatusCode.OK, code);   // ein abgelehntes Einrichten ist keine Störung des Servers
        Assert.False((bool)j!["gestartet"]!);
        Assert.Equal("Administratorrechte nicht erteilt. Es wurde nichts geändert.", (string)j["grund"]!);
    }

    [Fact]
    public async Task Ein_zweiter_Start_waehrend_die_Rueckfrage_offen_ist_wird_nicht_gestartet()
    {
        var offen = new TaskCompletionSource<EinrichtenErgebnis>();
        var e = new Einrichten { Warten = offen.Task };
        await using var s = Server(e);
        var erster = s.LokalPost(Pfad, new { bestaetigt = true });
        var bis = DateTime.UtcNow.AddSeconds(10);
        while (e.Auftraege.Count == 0 && DateTime.UtcNow < bis) await Task.Delay(10);
        var (code, j) = await s.LokalPost(Pfad, new { bestaetigt = true });
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.False((bool)j!["gestartet"]!);
        Assert.Contains("gerade gestartet", (string)j["grund"]!);
        Assert.Single(e.Auftraege);
        offen.SetResult(new EinrichtenErgebnis(true, null));
        Assert.True((bool)(await erster).Json!["gestartet"]!);
        // danach geht es wieder
        Assert.True((bool)(await s.LokalPost(Pfad, new { bestaetigt = true })).Json!["gestartet"]!);
    }

    [Fact]
    public async Task Nicht_von_ausserhalb_von_localhost()
    {
        var e = new Einrichten();
        await using var s = Server(e);
        const string koerper = "{\"bestaetigt\":true}";
        var fremderHost = new HttpRequestMessage(HttpMethod.Post, Pfad) { Content = new StringContent(koerper, Encoding.UTF8, "application/json") };
        fremderHost.Headers.Host = "boese.example";   // DNS-Rebinding
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Lokal.SendAsync(fremderHost)).StatusCode);
        var fremderUrsprung = new HttpRequestMessage(HttpMethod.Post, Pfad) { Content = new StringContent(koerper, Encoding.UTF8, "application/json") };
        fremderUrsprung.Headers.TryAddWithoutValidation("Origin", "https://boese.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Lokal.SendAsync(fremderUrsprung)).StatusCode);
        var einfach = new HttpRequestMessage(HttpMethod.Post, Pfad) { Content = new StringContent(koerper, Encoding.UTF8, "text/plain") };   // «einfache» Anfrage einer fremden Seite
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, (await s.Lokal.SendAsync(einfach)).StatusCode);
        Assert.Empty(e.Auftraege);
    }

    [Fact]
    public async Task Das_Skript_fehlt_ergibt_gestartet_false_ohne_Start()
    {
        var e = new Einrichten();
        var r = await QuestAppEinrichten.Starten(e, Path.Combine(Path.GetTempPath(), "gibt_es_nicht", QuestAppEinrichten.SkriptName), new SemaphoreSlim(1, 1), CancellationToken.None);
        Assert.False(r.Gestartet);
        Assert.Contains(QuestAppEinrichten.SkriptName, r.Grund);
        Assert.Empty(e.Auftraege);
    }

    // ---------------------------------------------------------------- Prozessstart (ohne je ein Programm zu starten)

    [Fact]
    public async Task Prozessstart_mit_runas_und_UAC_Abbruch_1223_wird_zu_Administratorrechte_nicht_erteilt()
    {
        if (!OperatingSystem.IsWindows()) return;
        ProcessStartInfo? gesehen = null;
        var auftrag = QuestAppEinrichten.Auftrag(QuestAppEinrichten.SkriptPfad());
        var ok = new QuestAppEinrichtenEcht(psi => { gesehen = psi; return null; });
        var r = await ok.Starten(auftrag, CancellationToken.None);
        Assert.True(r.Gestartet);
        Assert.NotNull(gesehen);
        Assert.Equal("runas", gesehen!.Verb);   // Windows fragt nach Administratorrechten
        Assert.True(gesehen.UseShellExecute);
        Assert.Equal(auftrag.Programm, gesehen.FileName);
        Assert.Equal(auftrag.Argumente, gesehen.Arguments);

        var abgelehnt = new QuestAppEinrichtenEcht(_ => throw new Win32Exception(1223, "Der Vorgang wurde durch den Benutzer abgebrochen."));
        var n = await abgelehnt.Starten(auftrag, CancellationToken.None);
        Assert.False(n.Gestartet);
        Assert.Equal("Administratorrechte nicht erteilt. Es wurde nichts geändert.", n.Grund);

        var anders = new QuestAppEinrichtenEcht(_ => throw new Win32Exception(2, "Datei nicht gefunden."));
        var f = await anders.Starten(auftrag, CancellationToken.None);
        Assert.False(f.Gestartet);
        Assert.Contains("liess sich nicht starten", f.Grund);
    }

    [Fact]
    public async Task Im_Testlauf_startet_die_Vorgabe_nie_ein_Programm()
    {
        var naht = IQuestAppEinrichten.Vorgabe();   // im Testrunner: Testlauf
        Assert.IsType<QuestAppEinrichtenLeer>(naht);
        var r = await naht.Starten(QuestAppEinrichten.Auftrag(QuestAppEinrichten.SkriptPfad()), CancellationToken.None);
        Assert.False(r.Gestartet);
        // und auch der Server ohne eigene Attrappe: nichts startet, die Antwort ist gestartet: false
        await using var s = new XrServer(zugang: false);
        var (code, j) = await s.LokalPost(Pfad, new { bestaetigt = true });
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.False((bool)j!["gestartet"]!);
    }

    // ---------------------------------------------------------------- Netzstand: was «Netz einrichten …» ändern würde

    [Fact]
    public void Bedarf_nennt_Netze_und_fehlende_Regeln()
    {
        var regeln = new[]
        {
            new FirewallRegel("t", true, "Inbound", "Allow", "TCP", "5140", "Private"),
            new FirewallRegel("u", true, "Inbound", "Allow", "UDP", "5141", "Private"),
        };
        var privat = new NetzAuskunft(true, null, [new NetzProfil("s-net 3", "WLAN", "Private")], regeln);
        var b = QuestAppNetz.EinrichtenBedarf(privat, Echte, 5140, 5141);
        Assert.False(b.Noetig);
        Assert.Empty(b.Netze);
        Assert.False(b.RegelnFehlen);

        var oeffentlich = QuestAppNetz.EinrichtenBedarf(privat with { Profile = [new NetzProfil("s-net 3", "WLAN", "Public")] }, Echte, 5140, 5141);
        Assert.True(oeffentlich.Noetig);
        Assert.Equal(["s-net 3"], oeffentlich.Netze);
        Assert.False(oeffentlich.RegelnFehlen);

        var ohneRegeln = QuestAppNetz.EinrichtenBedarf(privat with { Regeln = [] }, Echte, 5140, 5141);
        Assert.True(ohneRegeln.Noetig);
        Assert.True(ohneRegeln.RegelnFehlen);
        Assert.Empty(ohneRegeln.Netze);

        var nurTcp = QuestAppNetz.EinrichtenBedarf(privat with { Regeln = [regeln[0]] }, Echte, 5140, 5141);   // UDP fehlt: unvollständig
        Assert.True(nurTcp.Noetig);

        // ein VPN-Netz auf einer anderen Schnittstelle zählt nicht
        var vpn = privat with { Profile = [new NetzProfil("s-net 3", "WLAN", "Private"), new NetzProfil("VPN", "Endian", "Public")] };
        Assert.False(QuestAppNetz.EinrichtenBedarf(vpn, Echte, 5140, 5141).Noetig);

        // nicht lesbar: unbekannt, was fehlt – nichts anbieten
        Assert.False(QuestAppNetz.EinrichtenBedarf(new NetzAuskunft(false, "nicht befragt", [], []), Echte, 5140, 5141).Noetig);
    }

    [Fact]
    public async Task Netz_Endpunkt_meldet_den_Bedarf_fuer_den_Knopf()
    {
        var sys = new NetzSystem();   // «s-net 3» ist öffentlich, Regeln fehlen
        await using var s = Server(new Einrichten(), sys);
        var j = JsonNode.Parse(await s.Lokal.GetStringAsync("/api/quest/app/netz?neu=1"))!;
        Assert.True((bool)j["einrichten"]!["noetig"]!);
        Assert.Equal(["s-net 3"], j["einrichten"]!["netze"]!.AsArray().Select(n => (string)n!).ToArray());
        Assert.True((bool)j["einrichten"]!["regelnFehlen"]!);
        // nach dem Einrichten (Windows meldet privat und die Regeln): nichts mehr nötig, die Hinweise sind weg
        var zugang = await s.Zugang();
        int tcp = (int)zugang["port"]!, udp = (int)zugang["suchPort"]!;
        sys.Auskunft = new NetzAuskunft(true, null, [new NetzProfil("s-net 3", "WLAN", "Private")], [
            new FirewallRegel("t", true, "Inbound", "Allow", "TCP", tcp.ToString(), "Private"),
            new FirewallRegel("u", true, "Inbound", "Allow", "UDP", udp.ToString(), "Private")]);
        var k = JsonNode.Parse(await s.Lokal.GetStringAsync("/api/quest/app/netz?neu=1"))!;
        Assert.False((bool)k["einrichten"]!["noetig"]!);
        Assert.DoesNotContain(k["hinweise"]!.AsArray(), h => (string)h!["stufe"]! == "warnung");
    }

    // ---------------------------------------------------------------- Texte

    [Fact]
    public void Die_Hinweise_nennen_den_Knopf_und_das_Skript_als_Ersatzweg()
    {
        var oeffentlich = QuestAppNetz.Bewerten(new NetzAuskunft(true, null, [new NetzProfil("s-net 3", "WLAN", "Public")], []), Echte, 5140, 5141, true);
        foreach (var h in oeffentlich.Where(x => x.Stufe == "warnung"))
        {
            Assert.Contains("«Netz einrichten …»", h.Text);
            Assert.Contains(@"werkzeuge\xr_netz_einrichten.cmd", h.Text);   // Ersatzweg
            Assert.DoesNotContain("ändert die Firewall nie", h.Text);
        }
        var fehlt = QuestAppNetz.Bewerten(new NetzAuskunft(true, null, [new NetzProfil("s-net 3", "WLAN", "Private")], []), Echte, 5140, 5141, true);
        var w = Assert.Single(fehlt, x => x.Stufe == "warnung");
        Assert.Contains("«Netz einrichten …»", w.Text);
        Assert.Contains("nur auf Knopfdruck", w.Text);
        Assert.Contains("Administratorrechten", w.Text);
        // der Name des Servers kommt aus der Marke (XR-Labor)
        Assert.Contains("XR-Labor ändert die Firewall nur auf Knopfdruck", QuestAppNetz.Bewerten(new NetzAuskunft(true, null, [new NetzProfil("s-net 3", "WLAN", "Private")], []), Echte, 5340, 5341, true, "XR-Labor").Single(x => x.Stufe == "warnung").Text);
        // unvollständige Regeln verweisen ebenfalls auf den Knopf
        var udpFehlt = QuestAppNetz.Bewerten(new NetzAuskunft(true, null, [new NetzProfil("s-net 3", "WLAN", "Private")], [new FirewallRegel("t", true, "Inbound", "Allow", "TCP", "5140", "Private")]), Echte, 5140, 5141, true);
        Assert.Contains(udpFehlt, x => x.Text.Contains("«Netz einrichten …»"));
    }

    [Fact]
    public void Die_KI_darf_das_Einrichten_nicht()
    {
        Assert.Equal("verboten", KiEinordnung.Von("quest_app_netz"));
        var f = Assert.Single(KiEinordnung.RechnerFunktionen, x => x.Name == "quest_app_netz");
        Assert.Contains("/api/quest/app/netz/einrichten", f.Grund);
        Assert.Contains("Administratorrechten", f.Grund);
        Assert.DoesNotContain("quest_app_netz", KiEinordnung.Erlaubt);
        Assert.DoesNotContain(Hlks.Mcp.Werkzeugauswahl.Alle(), n => n.Contains("einrichten", StringComparison.OrdinalIgnoreCase) && n.Contains("netz", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("quest_app_netz", AuftragDienst.ErlaubteWerkzeuge);
        Assert.DoesNotContain("quest_app_netz", GespraechDienst.McpWerkzeuge(new GespraechEinstellungen(), true));
    }
}
