using System.Net;
using System.Text.Json.Nodes;
using Hlks.Web;
using Hlks.Web.Tests;

namespace Hlks.Labor.Tests;

/// <summary>
/// Der Labor-Server (L0) im Testserver: /api/status, Seite und verlinkte KICAD-Oberfläche, Herkunftsschutz, Kopplungspaket mit den Ports des Labors, Kanal ohne
/// KICADs VR-Auftrag, und dass das Labor keine Benutzer-, Projekt- oder Auftrags-Endpunkte hat.
/// </summary>
public class LaborApiTests
{
    static async Task Warte(Func<Task<bool>> bedingung, string meldung)
    {
        var bis = DateTime.UtcNow.AddSeconds(15);
        while (!await bedingung())
        {
            if (DateTime.UtcNow > bis) throw new TimeoutException(meldung);
            await Task.Delay(50);
        }
    }

    [Fact]
    public async Task Status_nennt_App_Fassung_Commit_und_ist_nie_beschaeftigt()
    {
        await using var s = new LaborServer();
        var (code, j) = await s.Get("/api/status");
        Assert.Equal(HttpStatusCode.OK, code);
        var o = (JsonObject)j!;
        Assert.Equal("xr-labor", (string)o["app"]!);
        Assert.False((bool)o["beschaeftigt"]!);
        Assert.Equal("0.1.0", (string)o["fassung"]!);
        Assert.True(o.ContainsKey("commit"));
        if (o["commit"] is { } c) Assert.Matches("^[0-9a-f]{7,40}$", (string)c!);
        Assert.Equal(["app", "fassung", "commit", "beschaeftigt"], o.Select(p => p.Key).ToArray());
    }

    [Fact]
    public async Task Status_und_Zugang_nur_mit_Host_localhost()
    {
        await using var s = new LaborServer();
        foreach (var pfad in new[] { "/api/status", "/api/xr/zugang", "/api/quest/app" })
        {
            using var r = new HttpRequestMessage(HttpMethod.Get, pfad);
            r.Headers.Host = "boese.example";
            Assert.Equal(HttpStatusCode.Forbidden, (await s.Lokal.SendAsync(r)).StatusCode);
        }
        using var p = new HttpRequestMessage(HttpMethod.Post, "/api/xr/zugang") { Content = new StringContent("{\"an\":true}", System.Text.Encoding.UTF8, "application/json") };
        p.Headers.Add("Origin", "http://boese.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Lokal.SendAsync(p)).StatusCode);
        Assert.False(File.Exists(Path.Combine(s.LaborOrdner, "zugang.json")));
    }

    [Fact]
    public async Task Seite_aus_dem_Labor_Stil_und_Quest_App_aus_KICAD_sonst_nichts_von_KICAD()
    {
        await using var s = new LaborServer();
        var start = await s.Lokal.GetStringAsync("/");
        Assert.Contains("<title>XR-Labor</title>", start);
        Assert.Contains("labor/labor.js", start);
        Assert.DoesNotContain("Labor folgt mit L1", start);   // seit L1 baut labor.js die Register (Brille, Stellschrauben, Profile, Varianten, Prüfaufgaben, Auswertung, Fragebogen, Daten)
        Assert.Contains("id=\"labor-xl\"", start);
        Assert.Equal(start, await s.Lokal.GetStringAsync("/index.html"));
        var a = await s.Lokal.GetAsync("/labor/labor.js");
        Assert.Equal(HttpStatusCode.OK, a.StatusCode);
        Assert.Equal("no-cache", a.Headers.CacheControl?.ToString());
        // aus KICAD, dieselben Dateien (nicht kopiert)
        var kicad = LaborOberflaeche.KicadWurzel(LaborOberflaeche.LaborWurzel());
        foreach (var pfad in new[] { "css/hlks.css", "css/quest.css", "js/api.js", "js/oberflaeche/quest-app-dialog.js", "js/oberflaeche/quest-app-text.js", "js/oberflaeche/design-start.js" })
            Assert.Equal(File.ReadAllText(Path.Combine(kicad, pfad.Replace('/', Path.DirectorySeparatorChar))), await s.Lokal.GetStringAsync("/" + pfad));
        // KICADs Seiten und Bibliotheken nicht
        foreach (var pfad in new[] { "/vr.html", "/vr.webmanifest", "/lib/three/three.module.js", "/css/../vr.html" })
            Assert.Equal(HttpStatusCode.NotFound, (await s.Lokal.GetAsync(pfad)).StatusCode);
        Assert.DoesNotContain("js/app.js", start);
    }

    [Fact]
    public async Task Die_Seite_laedt_nur_Module_die_es_gibt()
    {
        // Die Importe der Labor-Seite zeigen auf KICADs Module (../js/…): jedes muss der Server ausliefern
        await using var s = new LaborServer();
        var js = await s.Lokal.GetStringAsync("/labor/labor.js");
        var importe = System.Text.RegularExpressions.Regex.Matches(js, @"from '(\.\.?/[^']+)'").Select(m => m.Groups[1].Value).ToList();
        Assert.Contains("../js/oberflaeche/quest-app-dialog.js", importe);
        Assert.Contains("../js/oberflaeche/xr-labor-dialog.js", importe);   // L1: der Dialog «XR-Labor» von KICAD als Seite
        foreach (var i in importe)
        {
            var pfad = new Uri(new Uri("http://localhost/labor/labor.js"), i).AbsolutePath;
            Assert.True((await s.Lokal.GetAsync(pfad)).IsSuccessStatusCode, pfad);
        }
        // L1: auch alle Module, die diese Module ihrerseits laden (statische Importe, in beliebiger Tiefe), gibt es
        var gesehen = new HashSet<string>(StringComparer.Ordinal);
        var offen = new Queue<string>(["/labor/labor.js"]);
        while (offen.Count > 0)
        {
            var pfad = offen.Dequeue();
            if (!gesehen.Add(pfad)) continue;
            var antwort = await s.Lokal.GetAsync(pfad);
            Assert.True(antwort.IsSuccessStatusCode, pfad);
            var text = await antwort.Content.ReadAsStringAsync();
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, @"^\s*(?:import|export)\b[^;'""]*?from\s+'(\.\.?/[^']+)'", System.Text.RegularExpressions.RegexOptions.Multiline))
                offen.Enqueue(new Uri(new Uri("http://localhost" + pfad), m.Groups[1].Value).AbsolutePath);
        }
        Assert.Contains("/js/oberflaeche/xr-labor-text.js", gesehen);
        Assert.Contains("/js/oberflaeche/dialoge.js", gesehen);
        Assert.Contains("/labor/labor-profile.js", gesehen);
        Assert.Contains("/labor/labor-daten.js", gesehen);
    }

    [Fact]
    public async Task Kein_Projekt_keine_Benutzereinstellungen_kein_VR_Auftrag_im_Labor()
    {
        await using var s = new LaborServer();
        Assert.Equal(HttpStatusCode.NotFound, (await s.LokalPost("/api/benutzer/einstellungen", new { schluessel = "quest.adb", wert = "C:\\x\\adb.exe" })).Code);
        Assert.Equal(HttpStatusCode.NotFound, (await s.LokalPost("/api/vr/auftrag", new { modus = "begehen" })).Code);
        Assert.Equal(HttpStatusCode.NotFound, (await s.LokalPost("/api/projekt/oeffnen", new { ordner = "C:\\x" })).Code);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Lokal.GetAsync("/api/quest")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Lokal.GetAsync("/hilfe/")).StatusCode);
        Assert.Empty(Directory.GetFiles(s.BenutzerOrdner, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Kopplungspaket_hat_Port_5340_und_Suchport_5341_und_liegt_im_Ordner_des_Labors()
    {
        // ohne Ports in den Einstellungen gelten die des Labors (Netzzugang aus: das Paket nennt die eingestellten Ports)
        await using var s = new LaborServer(anpassen: x => x with { Port = null, SuchPort = null });
        var z = await s.Zugang();
        Assert.False((bool)z["an"]!);
        Assert.False((bool)z["aktiv"]!);
        Assert.Equal(5340, (int)z["port"]!);
        Assert.Equal(5341, (int)z["suchPort"]!);
        Assert.Equal(Path.Combine(Path.GetFullPath(s.LaborOrdner), "zugang.json"), (string)z["schalter"]!);
        var paket = await s.Paket("QUEST3-LABOR1", "Quest Labor");
        Assert.Equal(XrPaket.ArtName, (string)paket["art"]!);
        Assert.Equal(5340, (int)paket["port"]!);
        Assert.Equal(5341, (int)paket["suchPort"]!);
        Assert.Equal("QUEST3-LABOR1", (string)paket["brille"]!);
        Assert.Equal((string)z["fingerabdruck"]!, (string)paket["fingerabdruck"]!);
        // Kopplung im Ordner des Labors, das Zertifikat im gemeinsamen Ordner – und dort keine Kopplung
        Assert.True(File.Exists(Path.Combine(s.LaborOrdner, "kopplungen.json")));
        Assert.False(File.Exists(Path.Combine(s.ZertifikatOrdner, "kopplungen.json")));
        Assert.False(File.Exists(Path.Combine(s.ZertifikatOrdner, "zugang.json")));
        Assert.True(File.Exists(new XrZertifikat(s.Xr).DateiPfad));
        Assert.Equal(s.ZertifikatOrdner, Path.GetDirectoryName(new XrZertifikat(s.Xr).DateiPfad));
        Assert.False(Directory.Exists(Path.Combine(s.LaborOrdner, "zertifikat.dpapi")) || File.Exists(Path.Combine(s.LaborOrdner, "zertifikat.dpapi")));
        Assert.Equal("Quest Labor", (string)Assert.Single((await s.Zugang())["kopplungen"]!.AsArray())!["name"]!);
    }

    [Fact]
    public async Task Schalter_des_Labors_in_der_eigenen_zugang_json()
    {
        await using var s = new LaborServer();
        var (code, z) = await s.LokalPost("/api/xr/zugang", new { an = true });
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.True((bool)z!["an"]!);
        Assert.True(File.Exists(Path.Combine(s.LaborOrdner, "zugang.json")));
        Assert.False(File.Exists(Path.Combine(s.ZertifikatOrdner, "zugang.json")));
        await s.Aktiv();
        var (code2, z2) = await s.LokalPost("/api/xr/zugang", new { an = false });
        Assert.Equal(HttpStatusCode.OK, code2);
        Assert.False((bool)z2!["aktiv"]!);
        // ungültiger Port: die Meldung nennt die Vorgabe des Labors
        var (code3, f) = await s.LokalPost("/api/xr/zugang", new { an = true, port = 80 });
        Assert.Equal(HttpStatusCode.BadRequest, code3);
        Assert.Contains("5340", (string)f!["vorschlag"]!);
    }

    [Fact]
    public async Task Netzzugang_oeffnet_sich_nur_im_Labor_auf_seinem_Desktop_Port()
    {
        // Der Testserver läuft ohne --port auf der Vorgabe 5430 (nur als TestServer, nichts wird gebunden); ein Desktop-Port 5431 sperrt den Zugang
        await using var s = new LaborServer(zugang: true, anpassen: x => x with { DesktopPort = 5431 });
        await Warte(async () => (await s.Zugang())["fehler"] is not null, "keine Meldung zur Sperre");
        var z = await s.Zugang();
        Assert.True((bool)z["an"]!);
        Assert.False((bool)z["aktiv"]!);
        Assert.Equal("Der XR-Zugang öffnet sich nur im XR-Labor auf Port 5431 (Desktop). Dieses XR-Labor läuft auf Port 5430 (Test- oder Abnahmeserver).", (string)z["fehler"]!);
    }

    [Fact]
    public async Task Kanal_willkommen_ohne_den_VR_Auftrag_von_KICAD()
    {
        await using var s = new LaborServer(zugang: true);
        // KICAD hat einen VR-Auftrag gespeichert (Benutzereinstellung vr.auftrag): im Labor gilt er nicht
        s.BenutzerEinstellungSetzen(VrAuftragSpeicher.Schluessel, new JsonObject { ["modus"] = "begehen", ["raum"] = "R-1.04", ["nr"] = 7, ["zeit"] = "2026-10-26T08:00:00Z" });
        s.BenutzerEinstellungSetzen("oberflaeche.symbole", JsonValue.Create("revit"));
        var (port, _, fp) = await s.Aktiv();
        var paket = await s.Paket("QUEST3-LABOR1");
        await using var k = await XrKlient.Verbinden(port, fp, XrServer.Token(paket));
        var w = await k.Hallo("Christian", "Quest 3");
        Assert.Null(w["auftrag"]);
        Assert.Equal("revit", (string)w["symbole"]!);   // Symbolsatz wie in KICAD
        var t = (await s.Get("/api/xr/teilnehmer")).Json!["teilnehmer"]!.AsArray();
        Assert.Equal("QUEST3-LABOR1", (string)Assert.Single(t)!["brille"]!);
        Assert.True((bool)Assert.Single((await s.Zugang())["kopplungen"]!.AsArray())!["verbunden"]!);
        // die Benutzereinstellung bleibt, wie KICAD sie geschrieben hat
        Assert.Equal(7, (int)new BenutzerEinstellungen(new RechnerAblage(s.BenutzerOrdner, "testpc", true, "einstellungen.json", "Umleitung")).Hole(VrAuftragSpeicher.Schluessel)!["nr"]!);
    }
}
