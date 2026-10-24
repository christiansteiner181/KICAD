using System.Net;
using System.Net.WebSockets;
using System.Text.Json.Nodes;

namespace Hlks.Web.Tests;

/// <summary>
/// Live-Kanal (Quest App P3, XrKanal.cs): hallo/willkommen, Teilnehmer am PC sichtbar, Posen gegenseitig weitergereicht und auf 30/s gedrosselt, Auftrag vom PC
/// sofort, Stellschrauben, Messprotokolle, Fehler, Widerruf, Ersetzen, Ausschalten. Über echtes TLS auf 127.0.0.1 mit gepinntem Fingerabdruck.
/// </summary>
public class XrKanalTests
{
    static async Task<JsonObject> Teilnehmer(XrServer s) => (JsonObject)JsonNode.Parse(await s.Lokal.GetStringAsync("/api/xr/teilnehmer"))!;

    static async Task Warte(Func<Task<bool>> bedingung, string was)
    {
        var bis = DateTime.UtcNow.AddSeconds(10);
        while (!await bedingung())
        {
            if (DateTime.UtcNow > bis) throw new TimeoutException(was);
            await Task.Delay(30);
        }
    }

    [Fact]
    public async Task Hallo_macht_den_Teilnehmer_am_PC_sichtbar_mit_letzter_Pose_und_Zeigen()
    {
        await using var s = new XrServer();
        var (port, _, fp) = await s.Aktiv();
        var paket = await s.Paket("QUEST-1", "Quest Christian");
        await using var a = await XrKlient.Verbinden(port, fp, XrServer.Token(paket));
        var w = await a.Hallo("Christian", "Quest 3");
        Assert.Equal(1, (int)w["fassung"]!);
        Assert.Matches("^t[0-9a-f]{8}$", a.Id!);
        Assert.Empty(w["teilnehmer"]!.AsArray());
        Assert.Null(w["auftrag"]);
        Assert.Empty(w["stellschrauben"]!.AsObject());

        var t = Assert.Single((await Teilnehmer(s))["teilnehmer"]!.AsArray())!;
        Assert.Equal(a.Id, (string)t["id"]!);
        Assert.Equal("Christian", (string)t["name"]!);
        Assert.Equal("Quest 3", (string)t["geraet"]!);
        Assert.Equal("0.1", (string)t["app"]!);
        Assert.Equal("QUEST-1", (string)t["brille"]!);
        Assert.Equal((string)paket["kopplung"]!, (string)t["kopplung"]!);
        Assert.Null(t["pose"]);
        Assert.True((bool)Assert.Single((await s.Zugang())["kopplungen"]!.AsArray())!["verbunden"]!);

        await a.Senden(XrKlient.Pose(1234, 5678, 1700, pinch: true));
        await a.Senden(XrKlient.N("zeigen", new JsonObject { ["element"] = "R-1.04", ["punkt"] = new JsonArray(1, 2, 3) }));
        await Warte(async () => (await Teilnehmer(s))["teilnehmer"]![0]!["zeigen"] is not null, "zeigen kommt nicht an");
        var t2 = (await Teilnehmer(s))["teilnehmer"]![0]!;
        Assert.Equal(1234, (double)t2["pose"]!["kopf"]!["pos"]![0]!);
        Assert.True((bool)t2["pose"]!["links"]!["pinch"]!);
        Assert.Equal("begehen", (string)t2["pose"]!["modus"]!);
        Assert.Equal("R-1.04", (string)t2["zeigen"]!["element"]!);
        Assert.Equal(1, (long)t2["posen"]!);
        Assert.Equal("strich", (string)w["symbole"]!);   // V1a: ohne Benutzereinstellung die Strichsymbole
    }

    [Fact]
    public async Task Willkommen_nennt_den_Symbolsatz_des_Benutzers_wie_in_KICAD()
    {
        // Quest App V1a (Christian 24.10.2026: «nutze die gleiche symbolsprache wie im cad»): oberflaeche.symbole = revit gilt auch in der Brille
        await using var s = new XrServer();
        s.BenutzerEinstellungSetzen("oberflaeche.symbole", JsonValue.Create("revit"));
        var (port, _, fp) = await s.Aktiv();
        var paket = await s.Paket("QUEST-1", "Quest Christian");
        await using var a = await XrKlient.Verbinden(port, fp, XrServer.Token(paket));
        var w = await a.Hallo();
        Assert.Equal("revit", (string)w["symbole"]!);
    }

    [Fact]
    public async Task Posen_zweier_Teilnehmer_werden_gegenseitig_weitergereicht()
    {
        await using var s = new XrServer();
        var (port, _, fp) = await s.Aktiv();
        await using var a = await XrKlient.Verbinden(port, fp, XrServer.Token(await s.Paket("QUEST-A")));
        await a.Hallo("Anna", "Quest 3");
        await using var b = await XrKlient.Verbinden(port, fp, XrServer.Token(await s.Paket("QUEST-B")));
        var wb = await b.Hallo("Beat", "Quest 3S");
        var andere = Assert.Single(wb["teilnehmer"]!.AsArray())!;
        Assert.Equal(a.Id, (string)andere["id"]!);
        var hallo = await a.Warten("hallo");
        Assert.Equal(b.Id, (string)hallo["id"]!);
        Assert.Equal("Beat", (string)hallo["name"]!);

        await a.Senden(XrKlient.Pose(100, 200, 1700));
        var pb = await b.Warten("pose");
        Assert.Equal(a.Id, (string)pb["von"]!);
        Assert.Equal(100, (double)pb["kopf"]!["pos"]![0]!);
        Assert.Equal(4, pb["kopf"]!["rot"]!.AsArray().Count);
        Assert.True((long)pb["zeit"]! > 0);

        await b.Senden(XrKlient.Pose(300, 400, 1600, pinch: true));
        var pa = await a.Warten("pose");
        Assert.Equal(b.Id, (string)pa["von"]!);
        Assert.True((bool)pa["links"]!["pinch"]!);

        // zeigen geht ebenfalls an die anderen
        await a.Senden(XrKlient.N("zeigen", new JsonObject { ["element"] = "3f2b8c1e-5a47-4d9e-b1c2-0a9d8e7f6b5a" }));
        Assert.Equal(a.Id, (string)(await b.Warten("zeigen"))["von"]!);

        // tschuess: B ist weg, A erfährt es
        await b.Senden(XrKlient.N("tschuess"));
        Assert.Equal(b.Id, (string)(await a.Warten("tschuess"))["id"]!);
        Assert.Equal(WebSocketCloseStatus.NormalClosure, await b.GeschlossenAbwarten());
        await Warte(async () => (await Teilnehmer(s))["teilnehmer"]!.AsArray().Count == 1, "B bleibt in der Liste");
    }

    [Fact]
    public async Task Posen_werden_auf_30_je_Sekunde_gedrosselt_die_letzte_gilt_fuer_den_PC()
    {
        var uhr = new XrTestUhr();   // steht still, bis der Test sie vorstellt
        await using var s = new XrServer(uhr: uhr);
        var (port, _, fp) = await s.Aktiv();
        await using var a = await XrKlient.Verbinden(port, fp, XrServer.Token(await s.Paket("QUEST-A")));
        await a.Hallo("Anna");
        await using var b = await XrKlient.Verbinden(port, fp, XrServer.Token(await s.Paket("QUEST-B")));
        await b.Hallo("Beat");
        await a.Warten("hallo");

        async Task<int> Runde(int start)
        {
            for (int i = 0; i < 10; i++) await a.Senden(XrKlient.Pose(start + i, 0, 1700));
            await a.Senden(XrKlient.N("zeigen", new JsonObject { ["element"] = $"M-{start}" }));   // Marke: kommt nach allen Posen dieser Runde
            int posen = 0;
            while (true)
            {
                var n = await b.Naechste() ?? throw new TimeoutException("Marke fehlt");
                if ((string?)n["typ"] == "pose") posen++;
                if ((string?)n["typ"] == "zeigen") return posen;
            }
        }

        Assert.Equal(2, await Runde(1000));   // Vorrat 2, die Uhr steht: der Rest wird nicht weitergereicht
        var t = (await Teilnehmer(s))["teilnehmer"]!.AsArray().First(x => (string)x!["name"]! == "Anna")!;
        Assert.Equal(10, (long)t["posen"]!);
        Assert.Equal(2, (long)t["weitergereicht"]!);
        Assert.Equal(1009, (double)t["pose"]!["kopf"]!["pos"]![0]!);   // der PC sieht die letzte

        uhr.Vor(TimeSpan.FromMilliseconds(34));   // gut eine dreissigstel Sekunde: eine Pose mehr
        Assert.Equal(1, await Runde(2000));
        uhr.Vor(TimeSpan.FromSeconds(1));   // der Vorrat füllt sich höchstens auf 2
        Assert.Equal(2, await Runde(3000));
    }

    [Fact]
    public async Task Auftrag_vom_PC_kommt_sofort_an()
    {
        await using var s = new XrServer();
        var (port, _, fp) = await s.Aktiv();
        await using var a = await XrKlient.Verbinden(port, fp, XrServer.Token(await s.Paket()));
        await a.Hallo();
        var (code, _) = await s.LokalPost("/api/vr/auftrag", new { modus = "begehen", raum = "R-1.04", geschoss = "EG" });
        Assert.Equal(HttpStatusCode.OK, code);
        var m = await a.Warten("auftrag", TimeSpan.FromSeconds(5));
        Assert.Equal("begehen", (string)m["auftrag"]!["modus"]!);
        Assert.Equal("R-1.04", (string)m["auftrag"]!["raum"]!);
        Assert.Equal(1, (long)m["auftrag"]!["nr"]!);

        // wer später kommt, bekommt den aktuellen Auftrag im willkommen
        await using var b = await XrKlient.Verbinden(port, fp, XrServer.Token(await s.Paket("QUEST-B")));
        var w = await b.Hallo("Beat");
        Assert.Equal("begehen", (string)w["auftrag"]!["modus"]!);
        Assert.Equal(1, (long)w["auftrag"]!["nr"]!);
    }

    [Fact]
    public async Task Stellschrauben_vom_PC()
    {
        await using var s = new XrServer();
        var (port, _, fp) = await s.Aktiv();
        await using var a = await XrKlient.Verbinden(port, fp, XrServer.Token(await s.Paket()));
        await a.Hallo();
        var (c1, j1) = await s.LokalPost("/api/xr/stellschraube", new { schluessel = "pinch.schwelle", wert = 0.7 });
        Assert.Equal(HttpStatusCode.OK, c1);
        Assert.Equal(1, (int)j1!["gesendet"]!);
        var m = await a.Warten("stellschraube");
        Assert.Equal("pinch.schwelle", (string)m["schluessel"]!);
        Assert.Equal(0.7, (double)m["wert"]!);
        Assert.Equal(0.7, (double)(await Teilnehmer(s))["stellschrauben"]!["pinch.schwelle"]!);

        // nur an einen Teilnehmer: nicht gemerkt
        var (c2, _) = await s.LokalPost("/api/xr/stellschraube", new { schluessel = "ton", wert = false, teilnehmer = a.Id });
        Assert.Equal(HttpStatusCode.OK, c2);
        Assert.False((bool)(await a.Warten("stellschraube"))["wert"]!);
        Assert.Null((await Teilnehmer(s))["stellschrauben"]!["ton"]);

        // neue Teilnehmer bekommen die gemerkten im willkommen
        await using var b = await XrKlient.Verbinden(port, fp, XrServer.Token(await s.Paket("QUEST-B")));
        Assert.Equal(0.7, (double)(await b.Hallo("Beat"))["stellschrauben"]!["pinch.schwelle"]!);

        // zurücksetzen mit null; ungültige Werte
        var (c3, j3) = await s.LokalPost("/api/xr/stellschraube", new JsonObject { ["schluessel"] = "pinch.schwelle", ["wert"] = null });
        Assert.Equal(HttpStatusCode.OK, c3);
        Assert.Empty(j3!["stellschrauben"]!.AsObject());
        Assert.Equal(HttpStatusCode.BadRequest, (await s.LokalPost("/api/xr/stellschraube", new { schluessel = "Gross", wert = 1 })).Code);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.LokalPost("/api/xr/stellschraube", new { schluessel = "a", wert = new { x = 1 } })).Code);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.LokalPost("/api/xr/stellschraube", new { schluessel = "a", wert = new string('x', 201) })).Code);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.LokalPost("/api/xr/stellschraube", new { schluessel = "a", wert = 1, teilnehmer = "t00000000" })).Code);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.LokalPost("/api/xr/stellschraube", new { schluessel = "a" })).Code);
    }

    [Fact]
    public async Task Messprotokoll_wird_je_Sitzung_als_JSON_Zeilen_gespeichert()
    {
        await using var s = new XrServer();
        var (port, _, fp) = await s.Aktiv();
        await using var a = await XrKlient.Verbinden(port, fp, XrServer.Token(await s.Paket("QUEST-M")));
        await a.Hallo("Christian");
        await a.Senden(XrKlient.N("messprotokoll", new JsonObject
        {
            ["aufgabe"] = "teleport-markierung", ["variante"] = "apps", ["zeitMs"] = 4120.5, ["fehlgriffe"] = 1, ["abbrueche"] = 0, ["erfolg"] = true,
            ["stellschrauben"] = new JsonObject { ["pinch.schwelle"] = 0.7, ["ton"] = true },
        }));
        var q1 = await a.Warten("quittung");
        Assert.Equal("messprotokoll", (string)q1["bezug"]!);
        Assert.Equal(1, (long)q1["nr"]!);
        await a.Senden(XrKlient.N("messprotokoll", new JsonObject { ["aufgabe"] = "drehen-90", ["zeitMs"] = 900, ["fehlgriffe"] = 0, ["abbrueche"] = 1 }));
        Assert.Equal(2, (long)(await a.Warten("quittung"))["nr"]!);

        var ordner = Path.Combine(s.XrOrdner, "messprotokolle");
        var datei = Assert.Single(Directory.GetFiles(ordner, "*.jsonl"));
        Assert.EndsWith($"_{a.Id}.jsonl", datei);
        var zeilen = File.ReadAllLines(datei);
        Assert.Equal(2, zeilen.Length);
        var z1 = JsonNode.Parse(zeilen[0])!;
        Assert.Equal("teleport-markierung", (string)z1["aufgabe"]!);
        Assert.Equal("apps", (string)z1["variante"]!);
        Assert.Equal(4120.5, (double)z1["zeitMs"]!);
        Assert.Equal(1, (int)z1["fehlgriffe"]!);
        Assert.Equal(0.7, (double)z1["stellschrauben"]!["pinch.schwelle"]!);
        Assert.Equal("QUEST-M", (string)z1["brille"]!);
        Assert.Equal("Christian", (string)z1["name"]!);
        Assert.Equal("drehen-90", (string)JsonNode.Parse(zeilen[1])!["aufgabe"]!);
        Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), Path.GetFullPath(datei));   // im Test unter %TEMP%, im Betrieb %LOCALAPPDATA% – nie im Repository

        // ungültig: Fehler an die Brille, nichts gespeichert
        await a.Senden(XrKlient.N("messprotokoll", new JsonObject { ["aufgabe"] = "x", ["zeitMs"] = -1, ["fehlgriffe"] = 0, ["abbrueche"] = 0 }));
        Assert.Contains("zeitMs", (string)(await a.Warten("fehler"))["meldung"]!);
        Assert.Equal(2, File.ReadAllLines(datei).Length);
    }

    [Fact]
    public async Task Ungueltige_Nachrichten_ergeben_Fehler_ohne_Trennen()
    {
        await using var s = new XrServer();
        var (port, _, fp) = await s.Aktiv();
        await using var a = await XrKlient.Verbinden(port, fp, XrServer.Token(await s.Paket()));
        await a.Hallo();
        await a.Senden("kein json");
        Assert.Contains("JSON", (string)(await a.Warten("fehler"))["meldung"]!);
        await a.Senden(XrKlient.N("gibtsnicht"));
        Assert.Contains("Unbekannter Typ", (string)(await a.Warten("fehler"))["meldung"]!);
        await a.Senden(XrKlient.N("auftrag", new JsonObject { ["auftrag"] = new JsonObject { ["modus"] = "begehen" } }));
        Assert.Contains("nur der PC", (string)(await a.Warten("fehler"))["meldung"]!);
        await a.Senden(new JsonObject { ["typ"] = "pose", ["fassung"] = 2 });
        Assert.Contains("Fassung 2", (string)(await a.Warten("fehler"))["meldung"]!);
        await a.Senden(XrKlient.N("pose", new JsonObject { ["kopf"] = new JsonObject { ["pos"] = new JsonArray(1, 2), ["rot"] = new JsonArray(0, 0, 0, 1) } }));
        Assert.Contains("kopf.pos", (string)(await a.Warten("fehler"))["meldung"]!);
        await a.Senden(XrKlient.N("pose", new JsonObject { ["kopf"] = new JsonObject { ["pos"] = new JsonArray(1e9, 2, 3), ["rot"] = new JsonArray(0, 0, 0, 1) } }));
        Assert.Contains("ausserhalb", (string)(await a.Warten("fehler"))["meldung"]!);
        await a.Senden(XrKlient.N("zeigen", new JsonObject { ["element"] = "<script>" }));
        Assert.Contains("element", (string)(await a.Warten("fehler"))["meldung"]!);
        await a.Senden(XrKlient.N("hallo", new JsonObject { ["name"] = "x", ["geraet"] = "y" }));
        Assert.Contains("Schon angemeldet", (string)(await a.Warten("fehler"))["meldung"]!);
        Assert.Equal(WebSocketState.Open, a.Zustand);
        Assert.Null((await Teilnehmer(s))["teilnehmer"]![0]!["pose"]);
    }

    [Fact]
    public async Task Ohne_hallo_oder_mit_zu_grosser_Nachricht_wird_getrennt()
    {
        await using var s = new XrServer();
        var (port, _, fp) = await s.Aktiv();
        var token = XrServer.Token(await s.Paket());
        await using (var a = await XrKlient.Verbinden(port, fp, token))
        {
            await a.Senden(XrKlient.Pose());
            Assert.Contains("hallo", (string)(await a.Warten("fehler"))["meldung"]!);
            Assert.Equal(WebSocketCloseStatus.PolicyViolation, await a.GeschlossenAbwarten());
        }
        await using (var b = await XrKlient.Verbinden(port, fp, token))
        {
            await b.Hallo();
            await b.Senden("{\"typ\":\"zeigen\",\"fassung\":1,\"notiz\":\"" + new string('x', 17 * 1024) + "\"}");
            Assert.Equal(WebSocketCloseStatus.MessageTooBig, await b.GeschlossenAbwarten());
        }
        await Warte(async () => (await Teilnehmer(s))["teilnehmer"]!.AsArray().Count == 0, "Teilnehmer bleibt");
    }

    [Fact]
    public async Task Kanal_ohne_oder_mit_falschem_Schluessel_401()
    {
        await using var s = new XrServer();
        var (port, _, fp) = await s.Aktiv();
        var paket = await s.Paket();
        var ex = await Assert.ThrowsAsync<WebSocketException>(() => XrKlient.Verbinden(port, fp, $"{(string)paket["kopplung"]!}.{new string('C', 43)}"));
        Assert.Contains("401", ex.Message);
        using var ohne = XrServer.Netz(port, fp);
        Assert.Equal(HttpStatusCode.Unauthorized, (await ohne.GetAsync("/api/xr/kanal")).StatusCode);
    }

    [Fact]
    public async Task Widerruf_trennt_sofort_und_eine_neue_Verbindung_ersetzt_die_alte()
    {
        await using var s = new XrServer();
        var (port, _, fp) = await s.Aktiv();
        var paket = await s.Paket("QUEST-W");
        var token = XrServer.Token(paket);
        await using var a1 = await XrKlient.Verbinden(port, fp, token);
        await a1.Hallo("erste");
        await using var a2 = await XrKlient.Verbinden(port, fp, token);
        await a2.Hallo("zweite");
        Assert.Equal(XrKanal.Ersetzt, await a1.GeschlossenAbwarten());
        var t = Assert.Single((await Teilnehmer(s))["teilnehmer"]!.AsArray())!;
        Assert.Equal("zweite", (string)t["name"]!);

        await s.LokalPost("/api/xr/kopplung/widerrufen", new { kopplung = (string)paket["kopplung"]! });
        Assert.Equal(XrKanal.Widerrufen, await a2.GeschlossenAbwarten());
        await Warte(async () => (await Teilnehmer(s))["teilnehmer"]!.AsArray().Count == 0, "Teilnehmer bleibt nach Widerruf");
    }

    [Fact]
    public async Task Ausschalten_trennt_alle()
    {
        await using var s = new XrServer();
        var (port, _, fp) = await s.Aktiv();
        await using var a = await XrKlient.Verbinden(port, fp, XrServer.Token(await s.Paket()));
        await a.Hallo();
        await s.LokalPost("/api/xr/zugang", new { an = false });
        Assert.Equal(WebSocketCloseStatus.EndpointUnavailable, await a.GeschlossenAbwarten());
        Assert.Empty((await Teilnehmer(s))["teilnehmer"]!.AsArray());
    }

    [Fact]
    public void Nachrichten_pruefen_und_neu_bauen()
    {
        var (typ, o) = XrNachrichten.Lesen(XrKlient.Pose(1, 2, 3, pinch: true).ToJsonString());
        Assert.Equal("pose", typ);
        var p = XrNachrichten.Pose(o);
        Assert.Equal([1.0, 2, 3], p.Kopf.Pos);
        Assert.True(p.Links!.Pinch);
        Assert.Null(p.Rechts);
        var k = XrNachrichten.PoseKnoten(p);
        Assert.Null(k["unbekannt"]);
        // Felder, die der Server nicht kennt, gehen nicht weiter
        var mitZusatz = XrKlient.Pose();
        mitZusatz["geheim"] = "x";
        ((JsonObject)mitZusatz["kopf"]!)["extra"] = 1;
        var gebaut = XrNachrichten.Bauen("pose", XrNachrichten.PoseKnoten(XrNachrichten.Pose(mitZusatz)));
        Assert.DoesNotContain("geheim", gebaut);
        Assert.DoesNotContain("extra", gebaut);
        Assert.Throws<XrNachrichtFehler>(() => XrNachrichten.Lesen("[1,2]"));
        Assert.Throws<XrNachrichtFehler>(() => XrNachrichten.Lesen("{\"typ\":\"pose\"}"));
        Assert.Throws<XrNachrichtFehler>(() => XrNachrichten.Pose(new JsonObject { ["kopf"] = new JsonObject { ["pos"] = new JsonArray(1, 2, 3), ["rot"] = new JsonArray(0, 0, 0, "1") } }));
        Assert.Throws<XrNachrichtFehler>(() => XrNachrichten.Pose(new JsonObject { ["kopf"] = new JsonObject { ["pos"] = new JsonArray(1, 2, 3), ["rot"] = new JsonArray(0, 0, 0, 1) }, ["modus"] = "fliegen" }));
        Assert.Throws<XrNachrichtFehler>(() => XrNachrichten.Hallo(new JsonObject { ["name"] = new string('n', 41), ["geraet"] = "q" }));
        Assert.Throws<XrNachrichtFehler>(() => XrNachrichten.Messprotokoll(new JsonObject { ["aufgabe"] = "a", ["zeitMs"] = 1, ["fehlgriffe"] = 1.5, ["abbrueche"] = 0 }));
        Assert.Contains("pose", XrNachrichten.VonBrille);
        Assert.Contains("auftrag", XrNachrichten.VomPc);
    }
}
