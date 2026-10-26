using System.Net;
using System.Net.WebSockets;
using System.Text.Json.Nodes;
using Hlks.Kern;

namespace Hlks.Web.Tests;

/// <summary>
/// H+K XR E1, Teil PC-Seite (entwurf/xr-empfaenger.md, «Schnittstellen im Bau E1»): Kopplungspaket Fassung 2 mit «werkzeug» und «werkzeugName» (XrKopplung.cs)
/// und der Kanal-Zustand «aktiv» (XrKanal.cs, XrNachrichten.cs): hallo mit und ohne «aktiv», Nachricht «aktiv», Teilnehmerliste. Dass die App ein Paket der Fassung 1
/// als «kicad» liest, prüft die App-Seite (Hlks.Xr.Tests); der PC gibt nur noch Fassung 2 aus.
/// </summary>
public class XrKopplungWerkzeugTests
{
    static readonly XrZugangInfo Info = new("PC-TEST", ["192.168.1.20"], 5140, 5141, new string('a', 64));

    sealed class Umgebung : IDisposable
    {
        public string Ordner { get; } = Path.Combine(Path.GetTempPath(), "hlks_xrw_" + Guid.NewGuid().ToString("N")[..10]);
        public XrKopplungen Kopplungen { get; }
        public Umgebung(string werkzeug = "kicad", string name = "KICAD")
        {
            Kopplungen = new XrKopplungen(new XrEinstellungen { Ordner = Ordner, Werkzeug = werkzeug, ServerName = name });
        }
        public void Dispose() { try { if (Directory.Exists(Ordner)) Directory.Delete(Ordner, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }

    [Fact]
    public void Paket_Fassung_2_nennt_das_Werkzeug_dieses_Servers()
    {
        Assert.Equal(2, XrPaket.FassungAktuell);
        using var k = new Umgebung();
        var p = k.Kopplungen.PaketErzeugen("QUEST-A", "Quest Christian", Info);
        Assert.Equal(2, p.Fassung);
        Assert.Equal("kicad", p.Werkzeug);
        Assert.Equal("KICAD", p.WerkzeugName);
        var j = (JsonObject)JsonNode.Parse(Json.Text(p))!;   // so geht es an die App (camelCase)
        Assert.Equal(2, (int)j["fassung"]!);
        Assert.Equal("kicad", (string)j["werkzeug"]!);
        Assert.Equal("KICAD", (string)j["werkzeugName"]!);
        // alles, was Fassung 1 hatte, ist weiter da
        foreach (var feld in new[] { "art", "pc", "ips", "port", "suchPort", "fingerabdruck", "kopplung", "schluessel", "brille", "name" })
            Assert.NotNull(j[feld]);
        Assert.Equal("hlks-xr-kopplung", (string)j["art"]!);
    }

    [Fact]
    public void Das_Labor_gibt_sein_Werkzeug_aus_den_Einstellungen_des_Servers()
    {
        using var k = new Umgebung("xrlabor", "XR-Labor");
        var p = k.Kopplungen.PaketErzeugen("QUEST-A", null, Info);
        Assert.Equal("xrlabor", p.Werkzeug);
        Assert.Equal("XR-Labor", p.WerkzeugName);
        Assert.Equal(2, p.Fassung);
    }

    [Fact]
    public void Ausdrueckliche_Angabe_gilt_vor_den_Einstellungen()
    {
        using var k = new Umgebung();
        var p = k.Kopplungen.PaketErzeugen("QUEST-A", null, Info, "paket", "xrlabor", "XR-Labor");
        Assert.Equal("xrlabor", p.Werkzeug);
        Assert.Equal("XR-Labor", p.WerkzeugName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("Kicad")]
    [InlineData("1kicad")]
    [InlineData("-kicad")]
    [InlineData("ki cad")]
    [InlineData("kicad.json")]
    [InlineData("../kicad")]
    [InlineData("kicad/x")]
    [InlineData("kic\nad")]
    public void Ungueltige_Werkzeugkennung_wird_abgelehnt_und_es_entsteht_keine_Kopplung(string werkzeug)
    {
        using var k = new Umgebung();
        Assert.Throws<HlksFehler>(() => k.Kopplungen.PaketErzeugen("QUEST-A", null, Info, "paket", werkzeug, "KICAD"));
        Assert.Empty(k.Kopplungen.Liste());
    }

    [Fact]
    public void Werkzeugkennung_zu_lang_und_Werkzeugname_ungueltig()
    {
        using var k = new Umgebung();
        Assert.Throws<HlksFehler>(() => k.Kopplungen.PaketErzeugen("QUEST-A", null, Info, "paket", "a" + new string('b', 32), "KICAD"));   // 33 Zeichen
        Assert.Equal("a" + new string('b', 31), k.Kopplungen.PaketErzeugen("QUEST-A", null, Info, "paket", "a" + new string('b', 31), "KICAD").Werkzeug);   // 32 sind erlaubt
        foreach (var name in new[] { "", "   ", "Zeile\nzwei", new string('n', 41) })
            Assert.Throws<HlksFehler>(() => k.Kopplungen.PaketErzeugen("QUEST-B", null, Info, "paket", "kicad", name));
        Assert.Equal("mein_werkzeug-2", XrKopplungen.WerkzeugPruefen(" mein_werkzeug-2 "));
        Assert.Single(k.Kopplungen.Liste());   // nur QUEST-A (der zweite gültige Aufruf ersetzt den ersten); für QUEST-B entstand nichts
    }

    [Fact]
    public async Task Der_Endpunkt_gibt_das_Paket_Fassung_2_mit_Werkzeug_aus()
    {
        await using var s = new XrServer();
        var paket = await s.Paket("QUEST-1", "Quest Christian");
        Assert.Equal(2, (int)paket["fassung"]!);
        Assert.Equal("kicad", (string)paket["werkzeug"]!);
        Assert.Equal("KICAD", (string)paket["werkzeugName"]!);

        await using var l = new XrServer(anpassen: x => x with { Werkzeug = "xrlabor", ServerName = "XR-Labor" });
        var lp = await l.Paket("QUEST-1");
        Assert.Equal("xrlabor", (string)lp["werkzeug"]!);
        Assert.Equal("XR-Labor", (string)lp["werkzeugName"]!);
    }

    [Fact]
    public void XrEinstellungen_Vorgabe_ist_KICAD()
    {
        var e = new XrEinstellungen();
        Assert.Equal("kicad", e.Werkzeug);
        Assert.Equal("KICAD", e.ServerName);
    }
}

/// <summary>Der Kanal-Zustand «aktiv» (H+K XR E1): Ein Werkzeug, das nicht vorn gezeigt wird, bekommt weiter Aufträge, steht aber als inaktiv in der Teilnehmerliste.</summary>
public class XrKanalAktivTests
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

    static async Task<bool> AktivVon(XrServer s, string id) => (bool)(await Teilnehmer(s))["teilnehmer"]!.AsArray().Single(t => (string)t!["id"]! == id)!["aktiv"]!;

    static async Task<JsonObject> Anmelden(XrKlient k, JsonObject hallo)
    {
        await k.Senden(XrKlient.N("hallo", hallo));
        var w = await k.Warten("willkommen");
        return w;
    }

    [Fact]
    public async Task Hallo_ohne_aktiv_gilt_als_aktiv()
    {
        await using var s = new XrServer();
        var (port, _, fp) = await s.Aktiv();
        await using var a = await XrKlient.Verbinden(port, fp, XrServer.Token(await s.Paket("QUEST-1")));
        var w = await a.Hallo("Christian");   // ältere Apps kennen das Feld nicht
        var t = Assert.Single((await Teilnehmer(s))["teilnehmer"]!.AsArray())!;
        Assert.True((bool)t["aktiv"]!);
        Assert.Equal((string)w["id"]!, (string)t["id"]!);
    }

    [Fact]
    public async Task Hallo_mit_aktiv_false_gilt_als_inaktiv_und_true_als_aktiv()
    {
        await using var s = new XrServer();
        var (port, _, fp) = await s.Aktiv();
        await using var a = await XrKlient.Verbinden(port, fp, XrServer.Token(await s.Paket("QUEST-1")));
        var w = await Anmelden(a, new JsonObject { ["name"] = "A", ["geraet"] = "Quest 3", ["marke"] = "xrlabor", ["aktiv"] = false });
        Assert.False(await AktivVon(s, (string)w["id"]!));
        var t = Assert.Single((await Teilnehmer(s))["teilnehmer"]!.AsArray())!;
        Assert.Equal("xrlabor", (string)t["marke"]!);

        await using var s2 = new XrServer();
        var (port2, _, fp2) = await s2.Aktiv();
        await using var b = await XrKlient.Verbinden(port2, fp2, XrServer.Token(await s2.Paket("QUEST-2")));
        var w2 = await Anmelden(b, new JsonObject { ["name"] = "B", ["geraet"] = "Quest 3", ["aktiv"] = true });
        Assert.True(await AktivVon(s2, (string)w2["id"]!));
    }

    [Fact]
    public async Task Die_Nachricht_aktiv_wechselt_den_Zustand_in_beide_Richtungen()
    {
        await using var s = new XrServer();
        var (port, _, fp) = await s.Aktiv();
        await using var a = await XrKlient.Verbinden(port, fp, XrServer.Token(await s.Paket("QUEST-1")));
        var id = (string)(await a.Hallo("Christian"))["id"]!;
        Assert.True(await AktivVon(s, id));
        await a.Senden(XrKlient.N("aktiv", new JsonObject { ["aktiv"] = false }));
        await Warte(async () => !await AktivVon(s, id), "aktiv=false kommt nicht an");
        await a.Senden(XrKlient.N("aktiv", new JsonObject { ["aktiv"] = true }));
        await Warte(() => AktivVon(s, id), "aktiv=true kommt nicht an");
        Assert.Equal(WebSocketState.Open, a.Zustand);
    }

    [Fact]
    public async Task Ein_inaktiver_Teilnehmer_bekommt_weiter_Auftraege_und_Stellschrauben()
    {
        await using var s = new XrServer();
        var (port, _, fp) = await s.Aktiv();
        await using var a = await XrKlient.Verbinden(port, fp, XrServer.Token(await s.Paket("QUEST-1")));
        await Anmelden(a, new JsonObject { ["name"] = "A", ["geraet"] = "Quest 3", ["aktiv"] = false });
        var (code, _) = await s.LokalPost("/api/vr/auftrag", new { modus = "begehen", raum = "R-1.04", geschoss = "EG" });
        Assert.Equal(HttpStatusCode.OK, code);
        var m = await a.Warten("auftrag", TimeSpan.FromSeconds(5));
        Assert.Equal("R-1.04", (string)m["auftrag"]!["raum"]!);
        var (c2, _) = await s.LokalPost("/api/xr/stellschraube", new { schluessel = "pinch.schwelle", wert = 0.7 });
        Assert.Equal(HttpStatusCode.OK, c2);
        await a.Warten("stellschraube", TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData("ja")]
    [InlineData("1")]
    [InlineData("null")]
    public async Task Ungueltiges_aktiv_gibt_einen_Fehler_ohne_Trennen_und_aendert_nichts(string wert)
    {
        await using var s = new XrServer();
        var (port, _, fp) = await s.Aktiv();
        await using var a = await XrKlient.Verbinden(port, fp, XrServer.Token(await s.Paket("QUEST-1")));
        var id = (string)(await a.Hallo("Christian"))["id"]!;
        var o = XrKlient.N("aktiv");
        o["aktiv"] = wert == "null" ? null : wert == "1" ? JsonValue.Create(1) : JsonValue.Create(wert);
        await a.Senden(o);
        Assert.Contains("aktiv", (string)(await a.Warten("fehler"))["meldung"]!);
        Assert.Equal(WebSocketState.Open, a.Zustand);
        Assert.True(await AktivVon(s, id));
        // auch im hallo
        await using var b = await XrKlient.Verbinden(port, fp, XrServer.Token(await s.Paket("QUEST-2")));
        await b.Senden(XrKlient.N("hallo", new JsonObject { ["name"] = "B", ["geraet"] = "q", ["aktiv"] = "ja" }));
        Assert.Contains("aktiv", (string)(await b.Warten("fehler"))["meldung"]!);
    }

    [Fact]
    public async Task Die_Teilnehmerliste_fuehrt_aktiv_je_Teilnehmer()
    {
        await using var s = new XrServer();
        var (port, _, fp) = await s.Aktiv();
        await using var a = await XrKlient.Verbinden(port, fp, XrServer.Token(await s.Paket("QUEST-A", "Anna")));
        await using var b = await XrKlient.Verbinden(port, fp, XrServer.Token(await s.Paket("QUEST-B", "Beat")));
        var ia = (string)(await Anmelden(a, new JsonObject { ["name"] = "A", ["geraet"] = "Quest 3" }))["id"]!;
        var ib = (string)(await Anmelden(b, new JsonObject { ["name"] = "B", ["geraet"] = "Quest 3", ["aktiv"] = false }))["id"]!;
        Assert.True(await AktivVon(s, ia));
        Assert.False(await AktivVon(s, ib));
        var liste = (await Teilnehmer(s))["teilnehmer"]!.AsArray();
        Assert.Equal(2, liste.Count);
    }

    [Fact]
    public void Nachrichten_aktiv_pruefen()
    {
        Assert.Contains("aktiv", XrNachrichten.VonBrille);
        Assert.DoesNotContain("aktiv", XrNachrichten.VomPc);   // der PC sagt der App nicht, was sie vorn zeigt
        var (typ, o) = XrNachrichten.Lesen(XrKlient.N("aktiv", new JsonObject { ["aktiv"] = true }).ToJsonString());
        Assert.Equal("aktiv", typ);
        Assert.True(XrNachrichten.Aktiv(o));
        Assert.False(XrNachrichten.Aktiv(new JsonObject { ["aktiv"] = false }));
        Assert.Throws<XrNachrichtFehler>(() => XrNachrichten.Aktiv(new JsonObject()));
        Assert.Throws<XrNachrichtFehler>(() => XrNachrichten.Aktiv(new JsonObject { ["aktiv"] = "true" }));
        Assert.True(XrNachrichten.Hallo(new JsonObject { ["name"] = "A", ["geraet"] = "q" }).Aktiv);
        Assert.False(XrNachrichten.Hallo(new JsonObject { ["name"] = "A", ["geraet"] = "q", ["aktiv"] = false }).Aktiv);
        Assert.Throws<XrNachrichtFehler>(() => XrNachrichten.Hallo(new JsonObject { ["name"] = "A", ["geraet"] = "q", ["aktiv"] = 3 }));
    }
}
