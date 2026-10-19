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
        var ereignisse = await s.KiEreignisseBisEnde(frage);
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
        var letztes = (await s.KiEreignisseBisEnde(Z(j!["frage"])))[^1];
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
        var e = await s.KiEreignisseBisEnde(frage);
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
        var textEreignisse = (await s.KiEreignisseBisEnde(Z(j!["frage"]))).Where(x => Z(x["typ"]) == "text").ToList();
        var teile = textEreignisse.Select(x => Z(x["teil"])).ToList();
        Assert.True(teile.Count >= 2, $"nur {teile.Count} Teil(e): Text kam nicht gestreamt");   // fünf Teile mit 400 ms Abstand
        Assert.Equal("Eins. Zwei. Drei. Vier. Fünf.", string.Concat(teile));
        // «ab» = Zeichen vor dem Teil (die Palette erkennt damit Teile, die ein nachgeladener Stand schon enthält)
        var davor = 0;
        foreach (var ev in textEreignisse)
        {
            Assert.Equal(davor, ev["ab"]!.GetValue<int>());
            davor += Z(ev["teil"]).Length;
        }
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
        // Entscheid Hauptlauf 20.10.2026: mit Internet ohne Projektzugriff – nur die enge Liste ist erlaubt, alles andere verboten
        var erlaubt = Schalter(info, "--allowedTools")!.Split(',');
        Assert.Equal(GespraechDienst.InternetWerkzeuge.Select(w => "mcp__hlks__" + w).Prepend("WebSearch").Order(), erlaubt.Order());
        Assert.DoesNotContain("mcp__hlks", erlaubt);
        var verboten = Schalter(info, "--disallowedTools")!.Split(',');
        Assert.DoesNotContain("mcp__hlks__unterlage_aus_url", verboten);
        foreach (var w in new[] { "familie_freigeben", "projekt_ueberblick", "element_lesen", "element_aendern", "leitung_ziehen", "rueckgaengig", "elemente_suchen", "aenderungen" })
            Assert.Contains("mcp__hlks__" + w, verboten);
        Assert.Contains("Internet ist bei dieser Frage erlaubt – dafür arbeitest du OHNE Projektzugriff", Z(info["prompt"]));
        Assert.Contains("müssen ohne Internet gestellt werden", Z(info["prompt"]));
        Assert.True(antwort["internet"]!.GetValue<bool>());
        Assert.Equal("Internet: ohne Projektzugriff", Z(antwort["internetHinweis"]));
        Assert.Equal("Internet: ohne Projektzugriff", Z((await s.Gespraech(id))["nachrichten"]![0]!["internetHinweis"]));
        var abrufe = antwort["abrufe"]!.AsArray();
        Assert.Contains(abrufe, a => Z(a!["art"]) == "suche" && Z(a["text"]) == "Prüfleitung Datenblatt" && a["erfolg"]!.GetValue<bool>());
        Assert.Contains(abrufe, a => Z(a!["art"]) == "abruf" && Z(a["url"]) == "https://www.hersteller.example/gesperrt.pdf" && !a["erfolg"]!.GetValue<bool>());
        var log = s.Protokoll(id);
        Assert.Contains(log, x => Z(x["text"]) == "Websuche: Prüfleitung Datenblatt");
        Assert.Contains(log, x => Z(x["text"]).StartsWith("Abruf fehlgeschlagen: https://www.hersteller.example/gesperrt.pdf"));
        Assert.True((await s.Gespraech(id))["nachrichten"]![0]!["internet"]!.GetValue<bool>());

        // Vorgabe ohne Internet
        var ohneAntwort = await s.FragenUndWarten(id, "Ohne Netz");
        var ohne = s.AttrappeInfos(id)[^1];
        Assert.Equal("", Schalter(ohne, "--tools"));
        Assert.Equal("mcp__hlks", Schalter(ohne, "--allowedTools"));
        Assert.Contains("mcp__hlks__unterlage_aus_url", Schalter(ohne, "--disallowedTools")!.Split(','));
        Assert.Null(ohneAntwort["internetHinweis"]);
        // ohne Internet verweigert die Attrappe (wie Claude Code mit dontAsk) Websuche und Abruf
        var netzOhneSchalter = await s.FragenUndWarten(id, "Suche [attrappe:internet]");
        Assert.Equal("Werkzeug verweigert: WebSearch", Z(netzOhneSchalter["text"]));
        Assert.All(netzOhneSchalter["abrufe"]?.AsArray() ?? [], a => Assert.False(a!["erfolg"]!.GetValue<bool>()));   // versucht, abgelehnt
    }

    [Fact]
    public async Task Internet_OhneProjektzugriff_AttrappeVerweigertProjektwerkzeuge()
    {
        var normen = NormenKopie();
        try
        {
            using var s = new GespraechServer(e => e with { NormenOrdner = normen });
            await s.ProjektOeffnen();
            var vorher = await s.ElementName("WT-E.02");
            var id = await s.GespraechAnlegen();

            // Projekt lesen: verweigert
            var lesen = await s.FragenUndWarten(id, "Wie viele Geschosse? [attrappe:ueberblick]", internet: true);
            Assert.Equal("fertig", Z(lesen["status"]));
            Assert.Equal("Werkzeug verweigert: mcp__hlks__projekt_ueberblick", Z(lesen["text"]));
            var w = Assert.Single(lesen["werkzeuge"]!.AsArray())!;
            Assert.Equal("projekt_ueberblick", Z(w["name"]));
            Assert.False(w["ok"]!.GetValue<bool>());
            Assert.Contains("denied", Z(w["meldung"]));

            // Projekt ändern: verweigert, kein Paket, nichts geändert
            var pakete = new Hlks.Kern.OrdnerSpeicher(s.Ordner).PaketListe().Count;
            var aendern = await s.FragenUndWarten(id, "Umbenennen [attrappe:aendern=WT-E.02]", internet: true);
            Assert.EndsWith("Werkzeug verweigert: mcp__hlks__element_aendern", Z(aendern["text"]));
            Assert.Empty(aendern["aenderungen"]!.AsArray());
            Assert.Equal(pakete, new Hlks.Kern.OrdnerSpeicher(s.Ordner).PaketListe().Count);
            Assert.Equal(vorher, await s.ElementName("WT-E.02"));

            // erlaubt bleiben die Normen; kontext_bild ist erlaubt, liefert mit Internet aber nie ein Bild (kein Projektkontext)
            var norm = await s.FragenUndWarten(id, "Gefälle? [attrappe:normen]", internet: true);
            Assert.StartsWith("Treffer: SIA 9999:2026, Seite 3, Ziffer 4.2.1", Z(norm["text"]));
            var bild = await s.FragenUndWarten(id, "Bild? [attrappe:bild]", new JsonObject { ["bild"] = PngUrl }, internet: true);
            Assert.StartsWith("Kein Bild erhalten: Kein Bild – mit Internet ohne Projektkontext", Z(bild["text"]));

            // dieselbe Frage ohne Internet liest das Projekt
            var ohne = await s.FragenUndWarten(id, "Wie viele Geschosse? [attrappe:ueberblick]");
            Assert.Equal("Das Projekt hat 3 Geschosse.", Z(ohne["text"]));
        }
        finally { Loeschen(normen); }
    }

    /// <summary>Entscheid Hauptlauf 20.10.2026: mit Internet geht KEIN Projektkontext an Claude – der Server verwirft Ansicht, Geschoss, Auswahl und
    /// Bild selbst; der Auftragstext nennt weder Projektname noch Ordner noch Kennzeichen; die Frage läuft in einer eigenen Sitzung ohne Verlauf.</summary>
    [Fact]
    public async Task Internet_KeinProjektkontext_ServerVerwirftKontextUndBild_EigeneSitzung()
    {
        using var s = new GespraechServer();
        await s.ProjektOeffnen();
        var (projektName, ansicht) = await s.Dienst.Mit(p => (p.Kopf.Name, p.Modell.DerKlasse("Ansicht").First(x => x.Text("art") == "grundriss" && x.Geschoss is not null).Kennzeichen));
        var id = await s.GespraechAnlegen();
        await s.FragenUndWarten(id, "Erste Frage mit Projekt");                       // Sitzung des Gesprächs (mit Projektzugriff)
        var sitzung = Z(JsonNode.Parse(File.ReadAllText(s.GespraechDatei(id)))!["sitzung"]);
        var arbeit = Z(s.AttrappeInfos(id)[0]["arbeitsordner"]);

        var antwort = await s.FragenUndWarten(id, "Datenblatt suchen [attrappe:bild]",
            new JsonObject { ["ansicht"] = ansicht, ["geschoss"] = "EG", ["auswahl"] = new JsonArray("WT-E.02", "WC-E.02"), ["bild"] = PngUrl }, internet: true);

        var info = s.AttrappeInfos(id)[^1];
        var prompt = Z(info["prompt"]);
        Assert.Contains("Kein Projektkontext: Frage mit Internet, ohne Projektzugriff.", prompt);
        Assert.Contains("in einem Gespräch in KICAD,\nohne Projektzugriff.", prompt);
        Assert.DoesNotContain("an diesem Projekt", prompt);
        Assert.DoesNotContain(projektName, prompt);
        Assert.DoesNotContain(s.Ordner, prompt);
        Assert.DoesNotContain("e_KICAD", prompt);
        Assert.DoesNotContain("WT-E.02", prompt);
        Assert.DoesNotContain(ansicht, prompt);
        Assert.DoesNotContain("Aktive Ansicht", prompt);
        Assert.DoesNotContain("Auswahl", prompt);
        Assert.DoesNotContain("# Bisheriges Gespräch", prompt);
        Assert.DoesNotContain("Erste Frage mit Projekt", prompt);
        // eigene Sitzung ohne Verlauf, die Sitzung des Gesprächs bleibt
        Assert.Null(Schalter(info, "--resume"));
        Assert.NotEqual(sitzung, Schalter(info, "--session-id"));
        Assert.Equal(sitzung, Z(JsonNode.Parse(File.ReadAllText(s.GespraechDatei(id)))!["sitzung"]));
        // Bild verworfen: nicht abgelegt, nicht an den MCP-Server; kontext_bild meldet es
        var margs = info["mcpKonfiguration"]!["mcpServers"]!["hlks"]!["args"]!.AsArray().Select(x => x!.GetValue<string>()).ToList();
        Assert.DoesNotContain("--kontext-bild", margs);
        Assert.Contains("--ohne-projektkontext", margs);
        var bildOrdner = Path.Combine(arbeit, "bild");
        Assert.True(!Directory.Exists(bildOrdner) || Directory.GetFiles(bildOrdner).Length == 0);
        Assert.StartsWith("Kein Bild erhalten: Kein Bild – mit Internet ohne Projektkontext", Z(antwort["text"]));
        // Vermerk in der Mensch-Nachricht, Kontext leer
        var mensch = (await s.Gespraech(id))["nachrichten"]![2]!;
        Assert.True(mensch["kontextVerworfen"]!.GetValue<bool>());
        Assert.Empty(mensch["kontext"]!.AsObject());
        Assert.Equal("Internet: ohne Projektzugriff", Z(mensch["internetHinweis"]));
        Assert.Null((await s.Gespraech(id))["nachrichten"]![0]!["kontextVerworfen"]);   // ohne Internet kein Vermerk

        // die nächste Frage ohne Internet setzt die Sitzung des Gesprächs fort
        await s.FragenUndWarten(id, "Zweite Frage mit Projekt");
        Assert.Equal(sitzung, Schalter(s.AttrappeInfos(id)[^1], "--resume"));
        // Verlauf für eine neue Sitzung: ohne die Frage mit Internet und ihre Antwort
        Directory.Delete(Path.Combine(arbeit, ".attrappe"), true);
        await s.FragenUndWarten(id, "Dritte Frage mit Projekt");
        var p3 = Z(s.AttrappeInfos(id)[^1]["prompt"]);
        Assert.Contains("# Bisheriges Gespräch", p3);
        Assert.Contains("Erste Frage mit Projekt", p3);
        Assert.Contains("Zweite Frage mit Projekt", p3);
        Assert.DoesNotContain("Datenblatt suchen", p3);
        Assert.DoesNotContain("Kein Bild erhalten", p3);
    }
}
