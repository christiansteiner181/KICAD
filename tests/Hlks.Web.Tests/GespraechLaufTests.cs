using System.Net;
using System.Text.Json.Nodes;
using static Hlks.Web.Tests.GespraechServer;

namespace Hlks.Web.Tests;

/// <summary>
/// KI-Palette (Modul 3c, Teil K): Verhalten während einer Frage – höchstens ein KI-Lauf je Projekt (auch gegenüber einem Auftrag),
/// Abbrechen, Zeitlimit, Ereignisse in der richtigen Reihenfolge, Internet-Schalter. Attrappe statt claude.exe.
/// </summary>
public class GespraechLaufTests
{
    static readonly TimeSpan Lang = TimeSpan.FromSeconds(90);

    [Fact]
    public async Task ZweiterLauf_409_ImSelbenUndInEinemAnderenGespraech_UndFuerAuftraege()
    {
        using var s = new GespraechServer();
        await s.ProjektOeffnen(beispiel: false);
        var id = await s.GespraechAnlegen("Erstes");
        var anderes = await s.GespraechAnlegen("Zweites");
        var (c1, j1) = await s.Fragen(id, "Bitte warten [attrappe:haengt]");
        Assert.Equal(HttpStatusCode.Accepted, c1);
        await s.AufProzesseWarten(id, Lang);
        var laufend = await s.Gespraech(id);
        Assert.True(laufend["laeuft"]!.GetValue<bool>());
        Assert.Equal(Z(j1!["frage"]), Z(laufend["frage"]));
        Assert.Equal("laeuft", Z(laufend["nachrichten"]!.AsArray()[^1]!["status"]));
        Assert.Contains((await s.Get("/api/ki/gespraeche")).Json!["gespraeche"]!.AsArray(), g => Z(g!["id"]) == id && g["laeuft"]!.GetValue<bool>());

        var (c2, j2) = await s.Fragen(id, "Noch eine");
        Assert.Equal(HttpStatusCode.Conflict, c2);
        Assert.StartsWith("Claude arbeitet gerade an einer Frage im Gespräch «Erstes»", Z(j2!["fehler"]));
        Assert.Contains("abbrechen", Z(j2["vorschlag"]));
        var (c3, j3) = await s.Fragen(anderes, "Und hier?");
        Assert.Equal(HttpStatusCode.Conflict, c3);
        Assert.Contains("Claude arbeitet gerade", Z(j3!["fehler"]));
        var (c4, j4) = await s.Post("/api/auftraege", new JsonObject
        {
            ["art"] = "familie_aus_unterlagen", ["unterlagen"] = new JsonArray(AuftragServer.Unterlage()), ["kategorie"] = "Heizkoerper",
        });
        Assert.Equal(HttpStatusCode.Conflict, c4);
        Assert.Contains("Claude arbeitet gerade an einer Frage", Z(j4!["fehler"]));
        Assert.Empty((await s.Get("/api/auftraege")).Json!["auftraege"]!.AsArray());
        Assert.Equal(HttpStatusCode.Conflict, (await s.Delete("/api/ki/gespraeche/" + id)).Code);   // nicht während der Frage

        var (ca, ja) = await s.Post($"/api/ki/gespraeche/{id}/abbrechen");
        Assert.Equal(HttpStatusCode.OK, ca);
        Assert.False(ja!["laeuft"]!.GetValue<bool>());
        Assert.Equal("abgebrochen", Z(ja["nachrichten"]!.AsArray()[^1]!["status"]));
        // jetzt ist der Platz frei
        var nachher = await s.FragenUndWarten(anderes, "Jetzt?");
        Assert.Equal("fertig", Z(nachher["status"]));
    }

    [Fact]
    public async Task LaufenderAuftrag_Frage_409_DanachFrei()
    {
        using var s = new GespraechServer();
        await s.ProjektOeffnen(beispiel: false);
        var (ca, ja) = await s.Post("/api/auftraege", new JsonObject
        {
            ["art"] = "familie_aus_unterlagen", ["unterlagen"] = new JsonArray(AuftragServer.Unterlage()), ["kategorie"] = "Heizkoerper",
            ["hinweis"] = "[attrappe:haengt]",
        });
        Assert.Equal(HttpStatusCode.Accepted, ca);
        var auftrag = Z(ja!["id"]);
        await WartenAufAuftrag(s, auftrag, j => Z(j["status"]) == "laeuft");
        var id = await s.GespraechAnlegen();

        var (c, j) = await s.Fragen(id, "Darf ich?");
        Assert.Equal(HttpStatusCode.Conflict, c);
        Assert.Equal($"Claude arbeitet gerade an dem KI-Auftrag {auftrag} (Familie aus Unterlagen).", Z(j!["fehler"]));
        Assert.Contains($"/api/auftraege/{auftrag}/abbrechen", Z(j["vorschlag"]));
        Assert.Empty((await s.Gespraech(id))["nachrichten"]!.AsArray());   // nichts hinterlassen

        Assert.Equal(HttpStatusCode.OK, (await s.Post($"/api/auftraege/{auftrag}/abbrechen")).Code);
        var antwort = await s.FragenUndWarten(id, "Jetzt?");
        Assert.Equal("fertig", Z(antwort["status"]));
    }

    static async Task WartenAufAuftrag(GespraechServer s, string id, Func<JsonNode, bool> bedingung)
    {
        var uhr = System.Diagnostics.Stopwatch.StartNew();
        while (uhr.Elapsed < Lang)
        {
            var j = (await s.Get("/api/auftraege/" + id)).Json!;
            if (bedingung(j)) return;
            await Task.Delay(150);
        }
        throw new Xunit.Sdk.XunitException("Auftrag nicht im erwarteten Zustand.");
    }

    [Fact]
    public async Task Abbrechen_BeendetClaudeUndKindprozesse_EreignisAbgebrochen()
    {
        using var s = new GespraechServer();
        await s.ProjektOeffnen(beispiel: false);
        var id = await s.GespraechAnlegen();
        var (_, j) = await s.Fragen(id, "Lange Arbeit [attrappe:haengt]");
        var frage = Z(j!["frage"]);
        var p = await s.AufProzesseWarten(id, Lang);
        var pids = new[] { p["pid"]!.GetValue<int>(), p["schlaeferPid"]!.GetValue<int>(), p["mcpPid"]!.GetValue<int>() };
        Assert.All(pids, pid => Assert.False(IstTot(pid)));

        var (code, g) = await s.Post($"/api/ki/gespraeche/{id}/abbrechen");

        Assert.Equal(HttpStatusCode.OK, code);
        var antwort = g!["nachrichten"]!.AsArray()[^1]!;
        Assert.Equal("abgebrochen", Z(antwort["status"]));
        Assert.Equal("abgebrochen", Z(antwort["fehlerart"]));
        foreach (var pid in pids) Assert.True(await TotWarten(pid, TimeSpan.FromSeconds(10)), $"Prozess {pid} läuft noch");
        var ereignisse = s.KiEreignisse(frage);
        Assert.Equal("frage", Z(ereignisse[0]["typ"]));
        Assert.Equal("abgebrochen", Z(ereignisse[^1]["typ"]));
        Assert.Equal("abgebrochen", Z(ereignisse[^1]["nachricht"]!["status"]));
        Assert.Contains(s.Protokoll(id), x => Z(x["text"]).StartsWith("Frage beendet: abgebrochen"));
        // schon beendet: 409; unbekanntes Gespräch: 404
        Assert.Equal(HttpStatusCode.Conflict, (await s.Post($"/api/ki/gespraeche/{id}/abbrechen")).Code);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Post("/api/ki/gespraeche/20261019-120000-abcd/abbrechen")).Code);
        // die Sitzung bleibt: die nächste Frage setzt sie fort
        await s.FragenUndWarten(id, "Weiter");
        Assert.NotNull(Schalter(s.AttrappeInfos(id)[^1], "--resume"));
    }

    [Fact]
    public async Task Zeitlimit_BeendetDieFrage()
    {
        using var s = new GespraechServer(e => e with { Zeitlimit = TimeSpan.FromSeconds(8) });
        await s.ProjektOeffnen(beispiel: false);
        var id = await s.GespraechAnlegen();
        var (_, j) = await s.Fragen(id, "Endlos [attrappe:haengt]");
        var p = await s.AufProzesseWarten(id, Lang);
        var antwort = await s.Warten(id, Lang);

        Assert.Equal("fehler", Z(antwort["status"]));
        Assert.Equal("zeitlimit", Z(antwort["fehlerart"]));
        Assert.Contains("Zeitlimit", Z(antwort["fehler"]));
        Assert.True(await TotWarten(p["mcpPid"]!.GetValue<int>(), TimeSpan.FromSeconds(10)));
        var letztes = s.KiEreignisse(Z(j!["frage"]))[^1];
        Assert.Equal("fehler", Z(letztes["typ"]));
        Assert.Equal("zeitlimit", Z(letztes["fehlerart"]));
    }

    [Fact]
    public async Task Ereignisse_InDerReihenfolgeDesLaufs_TextGestreamt()
    {
        using var s = new GespraechServer();
        await s.ProjektOeffnen();
        var id = await s.GespraechAnlegen();
        var (_, j) = await s.Fragen(id, "Bitte WT-E.02 umbenennen [attrappe:aendern=WT-E.02]");
        var frage = Z(j!["frage"]);
        var antwort = await s.Warten(id, Lang);
        await Task.Delay(300);
        var e = s.KiEreignisse(frage);
        var typen = e.Select(x => Z(x["typ"])).ToList();

        Assert.Equal("frage", typen[0]);
        Assert.Equal("fertig", typen[^1]);
        Assert.Single(typen, t => t is "fertig" or "fehler" or "abgebrochen");
        Assert.All(e, x => Assert.Equal(id, Z(x["gespraech"])));
        // text … werkzeug (begonnen) … werkzeug (ok) … aenderung … text … fertig
        int wStart = typen.IndexOf("werkzeug"), wEnde = typen.LastIndexOf("werkzeug"), aend = typen.IndexOf("aenderung");
        Assert.True(typen.IndexOf("text") < wStart, string.Join(",", typen));
        Assert.True(wStart < wEnde);
        Assert.Null(e[wStart]["werkzeug"]!["ok"]);
        Assert.True(e[wEnde]["werkzeug"]!["ok"]!.GetValue<bool>());
        Assert.Equal(Z(e[wStart]["werkzeug"]!["id"]), Z(e[wEnde]["werkzeug"]!["id"]));
        Assert.Equal("element_aendern", Z(e[wStart]["werkzeug"]!["name"]));
        Assert.True(wEnde < aend && aend < typen.LastIndexOf("text"), string.Join(",", typen));
        Assert.Single(typen, t => t == "aenderung");
        // die Teile ergeben zusammen die Antwort; die Antwort steht vollständig in «fertig»
        var gestreamt = string.Concat(e.Where(x => Z(x["typ"]) == "text").Select(x => Z(x["teil"])));
        Assert.Equal(Z(antwort["text"]), gestreamt.Trim());
        Assert.Equal("Ich ändere WT-E.02.\n\nIch habe WT-E.02 umbenannt.", Z(antwort["text"]));
        Assert.Equal(Z(antwort["text"]), Z(e[^1]["nachricht"]!["text"]));
        Assert.Equal("mensch", Z(e[0]["nachricht"]!["rolle"]));
    }

    [Fact]
    public async Task Langsam_TextKommtInTeilen()
    {
        using var s = new GespraechServer();
        await s.ProjektOeffnen(beispiel: false);
        var id = await s.GespraechAnlegen();
        var (_, j) = await s.Fragen(id, "Zähle [attrappe:langsam]");
        var antwort = await s.Warten(id, Lang);
        var teile = s.KiEreignisse(Z(j!["frage"])).Where(x => Z(x["typ"]) == "text").Select(x => Z(x["teil"])).ToList();
        Assert.True(teile.Count >= 2, $"nur {teile.Count} Teil(e): Text kam nicht gestreamt");   // fünf Teile mit 400 ms Abstand
        Assert.Equal("Eins. Zwei. Drei. Vier. Fünf.", string.Concat(teile));
        Assert.Equal("Eins. Zwei. Drei. Vier. Fünf.", Z(antwort["text"]));
    }

    [Fact]
    public async Task Internet_Schalter_WebSucheUndAbrufeProtokolliert()
    {
        using var s = new GespraechServer();
        await s.ProjektOeffnen(beispiel: false);
        var id = await s.GespraechAnlegen();
        var antwort = await s.FragenUndWarten(id, "Suche im Netz [attrappe:internet]", internet: true);

        var info = s.AttrappeInfos(id)[^1];
        Assert.Equal("WebSearch", Schalter(info, "--tools"));
        Assert.Equal("mcp__hlks,WebSearch", Schalter(info, "--allowedTools"));
        Assert.DoesNotContain("mcp__hlks__unterlage_aus_url", Schalter(info, "--disallowedTools")!.Split(','));
        Assert.Contains("mcp__hlks__familie_freigeben", Schalter(info, "--disallowedTools")!.Split(','));
        Assert.Contains("Internet ist bei dieser Frage erlaubt", Z(info["prompt"]));
        Assert.True(antwort["internet"]!.GetValue<bool>());
        var abrufe = antwort["abrufe"]!.AsArray();
        Assert.Contains(abrufe, a => Z(a!["art"]) == "suche" && Z(a["text"]) == "Prüfleitung Datenblatt" && a["erfolg"]!.GetValue<bool>());
        Assert.Contains(abrufe, a => Z(a!["art"]) == "abruf" && Z(a["url"]) == "https://www.hersteller.example/gesperrt.pdf" && !a["erfolg"]!.GetValue<bool>());
        var log = s.Protokoll(id);
        Assert.Contains(log, x => Z(x["text"]) == "Websuche: Prüfleitung Datenblatt");
        Assert.Contains(log, x => Z(x["text"]).StartsWith("Abruf fehlgeschlagen: https://www.hersteller.example/gesperrt.pdf"));
        Assert.True((await s.Gespraech(id))["nachrichten"]![0]!["internet"]!.GetValue<bool>());

        // Vorgabe ohne Internet
        await s.FragenUndWarten(id, "Ohne Netz");
        var ohne = s.AttrappeInfos(id)[^1];
        Assert.Equal("", Schalter(ohne, "--tools"));
        Assert.Equal("mcp__hlks", Schalter(ohne, "--allowedTools"));
        Assert.Contains("mcp__hlks__unterlage_aus_url", Schalter(ohne, "--disallowedTools")!.Split(','));
    }
}
