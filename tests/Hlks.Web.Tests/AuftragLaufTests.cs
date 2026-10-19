using System.Net;
using System.Text.Json.Nodes;

namespace Hlks.Web.Tests;

/// <summary>
/// KI-Aufträge (Modul 2, B5): Verhalten während und am Ende eines Laufs – ein Lauf zugleich, Abbrechen, Zeitlimit, Fehlerfälle,
/// Fortschritt, Beenden des Servers. Wie in AuftragTests steht die Attrappe an der Stelle von claude.exe; sie startet den
/// echten MCP-Server und einen Schläfer als Kindprozesse (Modus «haengt»), um zu prüfen, dass wirklich alles endet.
/// </summary>
public class AuftragLaufTests
{
    static string Z(JsonNode? n) => n?.GetValue<string>() ?? "";
    static readonly TimeSpan Lang = TimeSpan.FromSeconds(90);

    [Fact]
    public async Task NurEinLaufGleichzeitigJeProjekt()
    {
        using var s = new AuftragServer();
        await s.ProjektAnlegen();
        var (c1, j1) = await s.Starten("haengt");
        Assert.Equal(HttpStatusCode.Accepted, c1);
        var id1 = AuftragServer.Id(j1);
        await s.AufProzesseWarten(id1, Lang);

        var (c2, j2) = await s.Starten("ok");
        Assert.Equal(HttpStatusCode.Conflict, c2);
        Assert.Contains("läuft schon ein KI-Auftrag", Z(j2!["fehler"]));
        Assert.Contains(id1, Z(j2["fehler"]));
        Assert.Contains("abbrechen", Z(j2["vorschlag"]));
        Assert.Single((await s.Get("/api/auftraege")).Json!["auftraege"]!.AsArray());   // der abgelehnte hat keinen Eintrag hinterlassen

        var (c3, j3) = await s.Post($"/api/auftraege/{id1}/abbrechen");
        Assert.Equal(HttpStatusCode.OK, c3);
        Assert.Equal("abgebrochen", Z(j3!["status"]));

        // danach ist der Platz wieder frei
        var (c4, j4) = await s.Starten("ok");
        Assert.Equal(HttpStatusCode.Accepted, c4);
        var ende = await s.Warten(AuftragServer.Id(j4), AuftragServer.Beendet, Lang, "zweiter Lauf");
        Assert.Equal("fertig", Z(ende["status"]));
        Assert.Equal(2, (await s.Get("/api/auftraege")).Json!["auftraege"]!.AsArray().Count);
    }

    [Fact]
    public async Task Abbrechen_BeendetClaudeUndAlleKindprozesse()
    {
        using var s = new AuftragServer();
        await s.ProjektAnlegen();
        var (_, j) = await s.Starten("haengt");
        var id = AuftragServer.Id(j);
        var p = await s.AufProzesseWarten(id, Lang);
        var claude = p["pid"]!.GetValue<int>();
        var schlaefer = p["schlaeferPid"]!.GetValue<int>();
        var mcp = p["mcpPid"]!.GetValue<int>();
        var laeuft = await s.Status(id);
        Assert.Equal("laeuft", Z(laeuft["status"]));
        Assert.Equal(claude, laeuft["prozessId"]!.GetValue<int>());
        Assert.All(new[] { claude, schlaefer, mcp }, pid => Assert.False(AuftragServer.IstTot(pid), $"Prozess {pid} sollte laufen"));

        var (code, ende) = await s.Post($"/api/auftraege/{id}/abbrechen");

        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Equal("abgebrochen", Z(ende!["status"]));
        Assert.Equal("abgebrochen", Z(ende["fehlerart"]));
        Assert.NotNull(ende["ende"]);
        foreach (var pid in new[] { claude, schlaefer, mcp })
            Assert.True(await AuftragServer.TotWarten(pid, TimeSpan.FromSeconds(10)), $"Prozess {pid} läuft noch");
        Assert.Contains(s.Protokoll(id), x => Z(x["text"]).StartsWith("Auftrag beendet: abgebrochen"));
        Assert.Equal("abgebrochen", Z((await s.Status(id))["status"]));
    }

    [Fact]
    public async Task Zeitlimit_BeendetDenLaufUndAlleKindprozesse()
    {
        using var s = new AuftragServer(e => e with { Zeitlimit = TimeSpan.FromSeconds(10) });
        await s.ProjektAnlegen();
        var (_, j) = await s.Starten("haengt");
        var id = AuftragServer.Id(j);
        var p = await s.AufProzesseWarten(id, Lang);
        var pids = new[] { p["pid"]!.GetValue<int>(), p["schlaeferPid"]!.GetValue<int>(), p["mcpPid"]!.GetValue<int>() };

        var ende = await s.Warten(id, AuftragServer.Beendet, Lang, "Zeitlimit");

        Assert.Equal("fehler", Z(ende["status"]));
        Assert.Equal("zeitlimit", Z(ende["fehlerart"]));
        Assert.Contains("Zeitlimit", Z(ende["fehler"]));
        Assert.True(ende["dauerSekunden"]!.GetValue<double>() >= 9.5, "nicht vor Ablauf des Zeitlimits beendet");
        foreach (var pid in pids) Assert.True(await AuftragServer.TotWarten(pid, TimeSpan.FromSeconds(10)), $"Prozess {pid} läuft noch");
        Assert.NotNull(ende["ergebnis"]);
    }

    [Fact]
    public async Task Zeitlimit_LaesstSichProAuftragEinstellen()
    {
        using var s = new AuftragServer();
        await s.ProjektAnlegen();
        var (c, j) = await s.Starten("haengt", new { zeitlimitMinuten = 0.1 });
        Assert.Equal(HttpStatusCode.Accepted, c);
        Assert.Equal(0.1, j!["zeitlimitMinuten"]!.GetValue<double>());
        var ende = await s.Warten(AuftragServer.Id(j), AuftragServer.Beendet, Lang, "Zeitlimit 6 s");
        Assert.Equal("zeitlimit", Z(ende["fehlerart"]));
    }

    [Fact]
    public async Task LetzteSchritte_ErscheinenWaehrendDesLaufs()
    {
        using var s = new AuftragServer();
        await s.ProjektAnlegen();
        var (_, j) = await s.Starten("langsam");
        var id = AuftragServer.Id(j);
        var mitten = await s.Warten(id, x => Z(x["status"]) == "laeuft" && x["schrittAnzahl"]!.GetValue<int>() >= 2, Lang, "Schritte während des Laufs");
        Assert.Null(mitten["ende"]);
        Assert.True(mitten["dauerSekunden"]!.GetValue<double>() >= 0);
        Assert.Contains(mitten["schritte"]!.AsArray(), x => Z(x!["text"]).StartsWith("KI: Schritt"));
        var ende = await s.Warten(id, AuftragServer.Beendet, Lang, "Ende");
        Assert.Equal("fertig", Z(ende["status"]));
        Assert.True(ende["schrittAnzahl"]!.GetValue<int>() > mitten["schrittAnzahl"]!.GetValue<int>() - 1);
        Assert.Single(ende["ergebnis"]!["familien"]!.AsArray());
    }

    [Fact]
    public async Task FehlerVonClaude_WerdenAlsFehlerMitMeldungGemeldet()
    {
        using var s = new AuftragServer();
        await s.ProjektAnlegen();

        async Task<JsonNode> Lauf(string modus)
        {
            var (c, j) = await s.Starten(modus);
            Assert.Equal(HttpStatusCode.Accepted, c);
            return await s.Warten(AuftragServer.Id(j), AuftragServer.Beendet, Lang, modus);
        }

        var fehler = await Lauf("fehler");
        Assert.Equal("fehler", Z(fehler["status"]));
        Assert.Equal("claude", Z(fehler["fehlerart"]));
        Assert.Contains("simulierter Fehler", Z(fehler["fehler"]));
        Assert.Empty(fehler["ergebnis"]!["familien"]!.AsArray());

        var anmeldung = await Lauf("anmeldung");
        Assert.Equal("fehler", Z(anmeldung["status"]));
        Assert.Equal("anmeldung", Z(anmeldung["fehlerart"]));
        Assert.Contains("nicht angemeldet", Z(anmeldung["fehler"]));

        var leer = await Lauf("leer");
        Assert.Equal("fehler", Z(leer["status"]));
        Assert.Contains("keinen Bericht", Z(leer["fehler"]));

        // ohne Abschlusszeile gilt die letzte Antwort als Bericht
        var ohne = await Lauf("ohne_ergebnis");
        Assert.Equal("fertig", Z(ohne["status"]));
        Assert.Equal("Antwort der Attrappe ohne Abschlusszeile.", Z(ohne["ergebnis"]!["bericht"]));

        // stderr steht im Protokoll
        Assert.Contains(s.Protokoll(Z(fehler["id"])), x => Z(x["type"]) == "stderr" && Z(x["text"]).Contains("simulierter Fehler"));
        Assert.Equal(4, (await s.Get("/api/auftraege")).Json!["auftraege"]!.AsArray().Count);
    }

    [Fact]
    public async Task InternetAus_KeineWebWerkzeugeKeinUnterlageAusUrlUndKeinInternetAbschnitt()
    {
        using var s = new AuftragServer();
        await s.ProjektAnlegen();
        var (c, j) = await s.Starten("ok", new { internet = false });
        Assert.Equal(HttpStatusCode.Accepted, c);
        Assert.False(j!["internet"]!.GetValue<bool>());
        var id = AuftragServer.Id(j);
        var ende = await s.Warten(id, AuftragServer.Beendet, Lang, "Lauf ohne Internet");
        Assert.Equal("fertig", Z(ende["status"]));
        Assert.False(ende["internet"]!.GetValue<bool>());
        Assert.False(ende["ergebnis"]!["internet"]!.GetValue<bool>());
        Assert.Empty(ende["ergebnis"]!["abrufe"]!.AsArray());

        var info = s.Protokoll(id).First(x => Z(x["subtype"]) == "attrappe");
        var o = info["optionen"]!.AsObject();
        Assert.Equal("", Z(o["--tools"]![0]));                         // keine eingebauten Werkzeuge
        var erlaubt = Z(o["--allowedTools"]![0]).Split(',');
        Assert.All(erlaubt, x => Assert.StartsWith("mcp__hlks__", x));
        Assert.DoesNotContain("mcp__hlks__unterlage_aus_url", erlaubt);
        Assert.DoesNotContain(info["argumente"]!.AsArray().Select(x => Z(x)), x => x.Contains("WebSearch") || x.Contains("WebFetch"));
        var prompt = Z(info["prompt"]);
        Assert.Contains("Internet-Recherche: nicht erlaubt", prompt);
        Assert.DoesNotContain("## Internet-Recherche", prompt);
        Assert.DoesNotContain("unterlage_aus_url", prompt);
        Assert.DoesNotContain("<!--", prompt);
    }

    [Fact]
    public async Task Internet_ErgebnisNenntDieAbgerufenenUnterlagen()
    {
        using var s = new AuftragServer();
        await s.ProjektAnlegen();
        var (c, j) = await s.Starten("internet", new { internet = true });
        Assert.Equal(HttpStatusCode.Accepted, c);
        var ende = await s.Warten(AuftragServer.Id(j), AuftragServer.Beendet, Lang, "Lauf mit Abruf");
        Assert.Equal("fertig", Z(ende["status"]));
        var erg = ende["ergebnis"]!;
        var u = Assert.Single(erg["abgerufeneUnterlagen"]!.AsArray())!;
        Assert.Equal("UL-1", Z(u["kennzeichen"]));
        Assert.Equal("https://www.hersteller.example/daten/datenblatt.csv", Z(u["url"]));
        Assert.Equal("csv", Z(u["art"]));
        Assert.True(u["groesse"]!.GetValue<long>() > 10);
        Assert.Matches(@"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\dZ$", Z(u["abgerufen"]));
        Assert.Matches("^[0-9a-f]{12}", Z(u["pruefsumme"]));
        Assert.Single(erg["familien"]!.AsArray());
        Assert.Null(erg["warnung"]);                                   // das Ablegen der Unterlage ist erwartet
        Assert.Equal(1, erg["befehle"]!["unterlage_einfuegen"]!.GetValue<int>());

        // Websuchen und Abrufe: im Ergebnis und als eigene Zeilen im Protokoll
        var abrufe = erg["abrufe"]!.AsArray();
        Assert.Equal(3, abrufe.Count);
        Assert.Equal(("suche", "Belimo R2015-S1 Datenblatt", true), (Z(abrufe[0]!["art"]), Z(abrufe[0]!["text"]), abrufe[0]!["erfolg"]!.GetValue<bool>()));
        Assert.Equal(("abruf", "https://www.hersteller.example/daten/datenblatt.csv", true), (Z(abrufe[1]!["art"]), Z(abrufe[1]!["url"]), abrufe[1]!["erfolg"]!.GetValue<bool>()));
        Assert.Equal(("abruf", "https://www.hersteller.example/gesperrt.pdf", false), (Z(abrufe[2]!["art"]), Z(abrufe[2]!["url"]), abrufe[2]!["erfolg"]!.GetValue<bool>()));
        Assert.All(abrufe, x => Assert.True(DateTimeOffset.TryParse(Z(x!["zeit"]), out _)));
        var log = s.Protokoll(AuftragServer.Id(j));
        Assert.Contains(log, x => Z(x["type"]) == "hlks" && Z(x["text"]) == "Websuche: Belimo R2015-S1 Datenblatt" && Z(x["abruf"]!["art"]) == "suche");
        Assert.Contains(log, x => Z(x["type"]) == "hlks" && Z(x["text"]) == "Abruf: https://www.hersteller.example/daten/datenblatt.csv");
        Assert.Contains(log, x => Z(x["text"]) == "Abruf erfolgreich: https://www.hersteller.example/daten/datenblatt.csv");
        Assert.Contains(log, x => Z(x["text"]) == "Abruf fehlgeschlagen: https://www.hersteller.example/gesperrt.pdf" && Z(x["abruf"]!["meldung"]).Contains("Anmeldung"));
        // die abgerufene Unterlage gehört nicht zu den Unterlagen des Auftraggebers: im Modell sind beide (Ifc und csv)
        var (_, l) = await s.Get("/api/auftraege");
        Assert.Single(l!["auftraege"]!.AsArray()[0]!["ergebnis"]!["abgerufeneUnterlagen"]!.AsArray());
    }

    [Fact]
    public async Task InternetSchalter_MussTrueOderFalseSein()
    {
        using var s = new AuftragServer();
        await s.ProjektAnlegen();
        var (c, j) = await s.Starten("ok", new { internet = "ja" });
        Assert.Equal(HttpStatusCode.BadRequest, c);
        Assert.Contains("true oder false", Z(j!["fehler"]));
        Assert.Empty((await s.Get("/api/auftraege")).Json!["auftraege"]!.AsArray());
    }

    [Fact]
    public async Task BeendenDesServers_BeendetLaufendeAuftraegeSamtKindprozessen()
    {
        var s = new AuftragServer();
        int[] pids;
        try
        {
            await s.ProjektAnlegen();
            var (_, j) = await s.Starten("haengt");
            var p = await s.AufProzesseWarten(AuftragServer.Id(j), Lang);
            pids = [p["pid"]!.GetValue<int>(), p["schlaeferPid"]!.GetValue<int>(), p["mcpPid"]!.GetValue<int>()];
            Assert.All(pids, pid => Assert.False(AuftragServer.IstTot(pid)));
        }
        catch { s.Dispose(); throw; }
        s.Dispose();   // beendet den Server: ApplicationStopping
        foreach (var pid in pids) Assert.True(await AuftragServer.TotWarten(pid, TimeSpan.FromSeconds(15)), $"Prozess {pid} läuft noch");
    }

    [Fact]
    public async Task ZweiProjekte_LaufenUnabhaengigVoneinander_ListeZeigtNurDasOffene()
    {
        using var s = new AuftragServer();
        await s.ProjektAnlegen();
        var (_, j) = await s.Starten("ok");
        await s.Warten(AuftragServer.Id(j), AuftragServer.Beendet, Lang, "Lauf im ersten Projekt");
        Assert.Single((await s.Get("/api/auftraege")).Json!["auftraege"]!.AsArray());

        // anderes Projekt öffnen: die Liste gehört zum offenen Projekt
        var zweites = Path.Combine(s.Wurzel, "zweites", "e_KICAD");
        var (code, _) = await s.Post("/api/projekt/anlegen", new { ordner = zweites, name = "Zweites", beispiel = false });
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Empty((await s.Get("/api/auftraege")).Json!["auftraege"]!.AsArray());
        var (c2, j2) = await s.Starten("ok");
        Assert.Equal(HttpStatusCode.Accepted, c2);
        var ende = await s.Warten(AuftragServer.Id(j2), AuftragServer.Beendet, Lang, "Lauf im zweiten Projekt");
        Assert.Equal("fertig", Z(ende["status"]));
        Assert.Equal("FAM-1", Z(ende["ergebnis"]!["familien"]![0]!["kennzeichen"]));   // eigene Zählung je Projekt
        Assert.True(File.Exists(Path.Combine(zweites, "auftraege", AuftragServer.Id(j2) + ".log")));
    }
}
