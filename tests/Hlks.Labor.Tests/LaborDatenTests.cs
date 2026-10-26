using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Hlks.Kern;
using Hlks.Web;
using Hlks.Web.Tests;

namespace Hlks.Labor.Tests;

/// <summary>
/// Ablage der Labor-Daten in OneDrive (XR-Labor L1): Wahl der Ablage, Schutz im Testlauf, Schreiben über temporäre Datei, Profile-Endpunkte, Übernahme der bisherigen
/// Daten (einmalig, erneut, Doppelte, gewachsene Dateien, geänderte Profile). Alles unter %TEMP%; die echten Ordner im OneDrive und unter %LOCALAPPDATA% werden nie berührt.
/// </summary>
public class LaborDatenTests
{
    static JsonObject Profil(string name, params (string, JsonNode)[] werte)
    {
        var w = new JsonObject();
        foreach (var (k, v) in werte) w[k] = v;
        return new JsonObject { ["name"] = name, ["werte"] = w };
    }

    static string Sha(string datei) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(datei)));

    /// <summary>Ein Ordner, der sicher nicht unter %TEMP% liegt und nicht existiert (ein «echter» OneDrive-Ordner im Testlauf).</summary>
    static string EchterOrdner() => Path.Combine(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))!, "Users", "testnutzer_gibt_es_nicht_" + Guid.NewGuid().ToString("N")[..6], "OneDrive", "HLKS-CAD", "labor");

    // ---------------------------------------------------------------- Ablage

    [Fact]
    public void Ablage_Umleitung_vor_OneDrive_vor_lokal_mit_Unterordnern()
    {
        var u = LaborAblage.Waehlen("PC1", false, @"C:\Temp\labor_x", @"C:\Users\x\OneDrive", @"C:\Users\x\AppData\Roaming\HLKS-CAD");
        Assert.Equal("Umleitung", u.Quelle);
        Assert.Equal(Path.GetFullPath(@"C:\Temp\labor_x"), u.Verzeichnis);
        var o = LaborAblage.Waehlen("PC1", false, null, @"C:\Users\x\OneDrive", @"C:\Users\x\AppData\Roaming\HLKS-CAD");
        Assert.Equal("OneDrive", o.Quelle);
        Assert.Equal(Path.Combine(@"C:\Users\x\OneDrive", "HLKS-CAD", "labor"), o.Verzeichnis);
        Assert.Equal(Path.Combine(o.Verzeichnis, "messungen"), o.Messungen);
        Assert.Equal(Path.Combine(o.Verzeichnis, "fragebogen"), o.Fragebogen);
        Assert.Equal(Path.Combine(o.Verzeichnis, "profile"), o.Profile);
        Assert.Equal(Path.Combine(o.Verzeichnis, "aktiv.json"), o.AktivDatei);
        Assert.Equal(Path.Combine(o.Verzeichnis, "uebernahme_PC1.json"), o.UebernahmeDatei);
        var l = LaborAblage.Waehlen("PC1", false, "  ", null, @"C:\Users\x\AppData\Roaming\HLKS-CAD");
        Assert.Equal("lokal", l.Quelle);
        Assert.Equal(Path.Combine(@"C:\Users\x\AppData\Roaming\HLKS-CAD", "labor"), l.Verzeichnis);
        Assert.Equal("PC_1", LaborAblage.Waehlen("PC/1", false, null, null, @"C:\a").Rechner);   // Dateinamen ohne verbotene Zeichen
    }

    [Fact]
    public void Ablage_im_Testlauf_ausserhalb_von_TEMP_ist_gesperrt_und_legt_nichts_an()
    {
        var echt = EchterOrdner();
        var a = new LaborAblage(echt, "PC1", "OneDrive", testlauf: true);
        Assert.False(a.Erlaubt);
        Assert.Contains("Testlauf", a.SchreibSperre());
        // die Unterordner zeigen auf einen Ort unter %TEMP%, den nie jemand anlegt: gelesen wird nichts aus dem echten Ordner
        foreach (var p in new[] { a.Messungen, a.Fragebogen, a.Profile, a.AktivDatei, a.UebernahmeDatei })
            Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), Path.GetFullPath(p), StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(a.Messungen));
        var e = Assert.Throws<HlksFehler>(() => a.Schreiben(Path.Combine(echt, "profile", "x.json"), "{}"));
        Assert.Contains("Testlauf", e.Message);
        Assert.False(Directory.Exists(echt));
        // ausserhalb eines Tests gilt die Sperre nicht
        Assert.True(new LaborAblage(echt, "PC1", "OneDrive", testlauf: false).Erlaubt);
        // unter %TEMP% im Testlauf erlaubt
        Assert.True(new LaborAblage(Path.Combine(Path.GetTempPath(), "x"), "PC1", "Umleitung", testlauf: true).Erlaubt);
    }

    [Fact]
    public void Ablage_schreibt_ueber_eine_temporaere_Datei_und_ersetzt()
    {
        var ordner = Path.Combine(Path.GetTempPath(), "hlks_labor_ablage_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var a = new LaborAblage(ordner, "PC1", "Umleitung", testlauf: true);
            var ziel = Path.Combine(a.Profile, "a.json");
            a.Schreiben(ziel, "{\"v\":1}");
            a.Schreiben(ziel, "{\"v\":2}");
            Assert.Equal("{\"v\":2}", File.ReadAllText(ziel));
            Assert.Empty(Directory.GetFiles(a.Profile, "*.tmp"));
            Assert.Single(Directory.GetFiles(a.Profile));   // genau eine Datei (a.json)
            Assert.Equal((1, new FileInfo(ziel).Length), LaborAblage.Zaehlen(a.Profile, "*.json"));
            Assert.Equal((0, 0L), LaborAblage.Zaehlen(Path.Combine(ordner, "fehlt"), "*.json"));
        }
        finally { try { Directory.Delete(ordner, true); } catch (IOException) { } }
    }

    [Fact]
    public void Die_Marke_des_Labors_ist_die_der_Brillen_App()
    {
        // H+K XR E1: Id und Name sind die des Werkzeugs «xrlabor» in der App; die App selbst ist für alle Werkzeuge dieselbe (ch.hkplanung.xr, wie bei KICAD).
        // Die App-Kennung in Hlks.Xr (AppMarke.*.Kennung) stellt die App-Seite von E1 um; hier gilt die der PC-Seite.
        Assert.Equal(Hlks.Xr.Netz.AppMarke.XrLabor.Id, LaborMarke.Id);
        Assert.Equal(Hlks.Xr.Netz.AppMarke.XrLabor.Name, LaborMarke.Name);
        Assert.Equal("ch.hkplanung.xr", LaborMarke.Paket);
        Assert.Equal(QuestAppMarke.Kicad.Paket, LaborMarke.Paket);
    }

    // ---------------------------------------------------------------- Profile

    [Fact]
    public async Task Profile_speichern_auflisten_aktiv_setzen_und_loeschen()
    {
        await using var s = new LaborServer();
        var (c0, j0) = await s.Get("/api/labor/profile");
        Assert.Equal(HttpStatusCode.OK, c0);
        Assert.Null(j0!["aktiv"]);
        Assert.Empty(j0["profile"]!.AsArray());

        var (c1, j1) = await s.LokalPost("/api/labor/profile", new { kennung = "sitzung", profil = Profil("Sitzung sitzend", ("anzeige.skala", 1.5), ("labor.variante.teleport", "zeigen_pinch")) });
        Assert.Equal(HttpStatusCode.OK, c1);
        Assert.Equal("sitzung", (string)j1!["kennung"]!);
        Assert.Equal(2, (int)j1["anzahl"]!);
        var datei = Path.Combine(s.Ablage.Profile, "sitzung.json");
        Assert.True(File.Exists(datei));
        var gespeichert = JsonNode.Parse(File.ReadAllText(datei))!;
        Assert.Equal(["name", "werte"], ((JsonObject)gespeichert).Select(p => p.Key).ToArray());   // Form { name, werte } wie die bisherige Benutzereinstellung
        Assert.Equal(1.5, (double)gespeichert["werte"]!["anzeige.skala"]!);
        Assert.Empty(Directory.GetFiles(s.Ablage.Profile, "*.tmp"));

        await s.LokalPost("/api/labor/profile", new { kennung = "alle", profil = Profil("Alle Brillen") });
        var (_, j2) = await s.Get("/api/labor/profile");
        Assert.Equal(["Alle Brillen", "Sitzung sitzend"], j2!["profile"]!.AsArray().Select(p => (string)p!["name"]!).ToArray());   // nach Name sortiert
        Assert.Equal(2, (int)j2["profile"]![1]!["anzahl"]!);
        Assert.Equal(1.5, (double)j2["profile"]![1]!["werte"]!["anzeige.skala"]!);

        // aktiv setzen und wieder löschen
        var (ca, ja) = await s.LokalPost("/api/labor/profile/aktiv", new { kennung = "sitzung" });
        Assert.Equal(HttpStatusCode.OK, ca);
        Assert.Equal("sitzung", (string)ja!["aktiv"]!);
        Assert.Equal("sitzung", (string)(await s.Get("/api/labor/profile")).Json!["aktiv"]!);
        Assert.True(File.Exists(s.Ablage.AktivDatei));
        var (cd, jd) = await s.LokalDelete("/api/labor/profile/sitzung");
        Assert.Equal(HttpStatusCode.OK, cd);
        Assert.True((bool)jd!["geloescht"]!);
        Assert.False(File.Exists(datei));
        var (_, j3) = await s.Get("/api/labor/profile");
        Assert.Null(j3!["aktiv"]);   // das aktive Profil ist weg: keines gilt
        Assert.Single(j3["profile"]!.AsArray());
        Assert.False((bool)(await s.LokalDelete("/api/labor/profile/sitzung")).Json!["geloescht"]!);
        // «kein Profil gilt» setzen
        await s.LokalPost("/api/labor/profile/aktiv", new { kennung = "alle" });
        Assert.Null((await s.LokalPost("/api/labor/profile/aktiv", new { kennung = (string?)null })).Json!["aktiv"]);
        Assert.Null((await s.Get("/api/labor/profile")).Json!["aktiv"]);
    }

    [Fact]
    public async Task Profile_pruefen_Kennung_Name_und_Werte_wie_XrLaborProfil()
    {
        await using var s = new LaborServer();
        async Task<string> Fehler(object koerper)
        {
            var (code, j) = await s.LokalPost("/api/labor/profile", koerper);
            Assert.Equal(HttpStatusCode.BadRequest, code);
            return (string)j!["fehler"]!;
        }
        Assert.Contains("Kennung", await Fehler(new { kennung = "Gross Klein", profil = Profil("X") }));
        Assert.Contains("Name", await Fehler(new { kennung = "x", profil = Profil("") }));
        Assert.Contains("Name", await Fehler(new { kennung = "x", profil = Profil(new string('n', 41)) }));
        Assert.Contains("ungültiger Schlüssel", await Fehler(new { kennung = "x", profil = Profil("X", ("GROSS", 1)) }));
        Assert.Contains("fehlt", await Fehler(new { kennung = "x" }));
        Assert.Contains("werte", await Fehler(new { kennung = "x", profil = new JsonObject { ["name"] = "X", ["werte"] = "kaputt" } }));
        var (cu, ju) = await s.LokalPost("/api/labor/profile/aktiv", new { kennung = "gibt_es_nicht" });
        Assert.Equal(HttpStatusCode.BadRequest, cu);
        Assert.Contains("gibt es nicht", (string)ju!["fehler"]!);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.LokalDelete("/api/labor/profile/Ungueltig!")).Code);
        Assert.False(Directory.Exists(s.Ablage.Profile));   // nichts angelegt
    }

    [Fact]
    public async Task Profile_Konfliktkopien_und_kaputte_Dateien_werden_uebergangen()
    {
        await using var s = new LaborServer(vorher: v =>
        {
            Directory.CreateDirectory(v.Ablage.Profile);
            File.WriteAllText(Path.Combine(v.Ablage.Profile, "gut.json"), Profil("Gut", ("anzeige.skala", 1.2)).ToJsonString());
            File.WriteAllText(Path.Combine(v.Ablage.Profile, "gut-DESKTOP-ABC.json"), Profil("Konfliktkopie").ToJsonString());   // OneDrive-Konfliktkopie: Kennung ungültig
            File.WriteAllText(Path.Combine(v.Ablage.Profile, "kaputt.json"), "{ nicht json");
            File.WriteAllText(Path.Combine(v.Ablage.Profile, "leer.json"), "{\"name\":\"\",\"werte\":{}}");
        });
        var (_, j) = await s.Get("/api/labor/profile");
        Assert.Equal("Gut", (string)Assert.Single(j!["profile"]!.AsArray())!["name"]!);
    }

    [Fact]
    public async Task Katalog_nimmt_das_aktive_Profil_aus_dem_Labor_nicht_aus_den_Benutzereinstellungen()
    {
        await using var s = new LaborServer();
        // KICADs Benutzereinstellung gilt im Labor nicht
        s.BenutzerEinstellungSetzen("labor.profil.kicad_profil", Profil("Von KICAD", ("anzeige.skala", 2.0)));
        s.BenutzerEinstellungSetzen("labor.profil_aktiv", "kicad_profil");
        var (_, ohne) = await s.Get("/api/xr/labor/katalog");
        Assert.Null(ohne!["profil"]);

        // das Profil des Labors: bereinigt (Unbekanntes und Ungültiges fällt weg), ETag ändert sich
        await s.LokalPost("/api/labor/profile", new { kennung = "sitzung", profil = Profil("Sitzung", ("anzeige.skala", 1.5), ("anzeige.gibt_es_nicht", 1), ("apps.groesse_m", 99), ("labor.variante.teleport", "stick")) });
        await s.LokalPost("/api/labor/profile/aktiv", new { kennung = "sitzung" });
        var a1 = await s.Lokal.GetAsync("/api/xr/labor/katalog");
        var mit = JsonNode.Parse(await a1.Content.ReadAsStringAsync())!;
        Assert.Equal("sitzung", (string)mit["profil"]!["kennung"]!);
        Assert.Equal("Sitzung", (string)mit["profil"]!["name"]!);
        var werte = (JsonObject)mit["profil"]!["werte"]!;
        Assert.Equal(1.5, (double)werte["anzeige.skala"]!);
        Assert.Equal("stick", (string)werte["labor.variante.teleport"]!);
        Assert.False(werte.ContainsKey("anzeige.gibt_es_nicht"));
        Assert.False(werte.ContainsKey("apps.groesse_m"));   // 99 liegt ausserhalb von min/max
        Assert.NotEqual((string)ohne["schluessel"]!, (string)mit["schluessel"]!);
        Assert.Equal($"\"{(string)mit["schluessel"]!}\"", a1.Headers.ETag?.ToString());
        // das Profil abschalten: wieder ohne
        await s.LokalPost("/api/labor/profile/aktiv", new { kennung = (string?)null });
        Assert.Null((await s.Get("/api/xr/labor/katalog")).Json!["profil"]);
    }

    [Fact]
    public async Task Katalog_im_Netzzugang_5340_mit_Profil_aus_dem_Labor_und_ETag()
    {
        await using var s = new LaborServer(zugang: true);
        var (port, _, fp) = await s.Aktiv();
        var paket = await s.Paket("QUEST3-LABOR1");
        using var mit = XrServer.Netz(port, fp, XrServer.Token(paket));
        using var ohne = XrServer.Netz(port, fp);
        await s.LokalPost("/api/labor/profile", new { kennung = "sitzung", profil = Profil("Sitzung", ("anzeige.skala", 1.5)) });
        await s.LokalPost("/api/labor/profile/aktiv", new { kennung = "sitzung" });

        var r = await mit.GetAsync("/api/xr/labor/katalog");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var netz = JsonNode.Parse(await r.Content.ReadAsStringAsync())!;
        Assert.Equal(1.5, (double)netz["profil"]!["werte"]!["anzeige.skala"]!);
        var lokal = JsonNode.Parse(await s.Lokal.GetStringAsync("/api/xr/labor/katalog"))!;
        Assert.Equal(lokal.ToJsonString(), netz.ToJsonString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await ohne.GetAsync("/api/xr/labor/katalog")).StatusCode);
        var nm = new HttpRequestMessage(HttpMethod.Get, "/api/xr/labor/katalog");
        nm.Headers.TryAddWithoutValidation("If-None-Match", r.Headers.ETag!.ToString());
        Assert.Equal(HttpStatusCode.NotModified, (await mit.SendAsync(nm)).StatusCode);
        // alles andere der Labor-Endpunkte bleibt am PC (im Netzzugang nicht erreichbar)
        foreach (var pfad in new[] { "/api/xr/labor/auswertung", "/api/xr/labor/fragebogen", "/api/labor/profile", "/api/labor/daten" })
            Assert.Equal(HttpStatusCode.NotFound, (await mit.GetAsync(pfad)).StatusCode);
    }

    // ---------------------------------------------------------------- Schutz im Testlauf (Server)

    [Fact]
    public async Task Gesperrte_Ablage_lehnt_Schreiben_ab_und_legt_nichts_an()
    {
        var echt = EchterOrdner();
        await using var s = new LaborServer(zugang: true, ablage: new LaborAblage(echt, "testpc", "OneDrive", testlauf: true));
        // Lesen: leer, nichts aus dem echten Ordner
        Assert.Empty((await s.Get("/api/labor/profile")).Json!["profile"]!.AsArray());
        var daten = (await s.Get("/api/labor/daten")).Json!;
        Assert.False((bool)daten["ablage"]!["erlaubt"]!);
        Assert.Equal(echt, (string)daten["ablage"]!["verzeichnis"]!);
        // Schreiben: Profile, Übernahme, Fragebogen
        var (cp, jp) = await s.LokalPost("/api/labor/profile", new { kennung = "x", profil = Profil("X") });
        Assert.Equal(HttpStatusCode.BadRequest, cp);
        Assert.Contains("Testlauf", (string)jp!["fehler"]!);
        var (cu, ju) = await s.LokalPost("/api/labor/uebernahme", new { });
        Assert.Equal(HttpStatusCode.BadRequest, cu);
        Assert.Contains("Testlauf", (string)ju!["fehler"]!);
        var (cf, jf) = await s.LokalPost("/api/xr/labor/fragebogen", new { name = "Test", komfort = new { uebelkeit = 1, augen = 2, natuerlich = 3 } });
        Assert.Equal(HttpStatusCode.BadRequest, cf);
        Assert.Contains("Testlauf", (string)jf!["fehler"]!);
        // Messprotokoll der Brille: abgelehnt, die Brille bekommt einen Fehler
        var (port, _, fp) = await s.Aktiv();
        await using var k = await XrKlient.Verbinden(port, fp, XrServer.Token(await s.Paket("QUEST-GESPERRT")));
        await k.Senden(XrKlient.N("hallo", new JsonObject { ["name"] = "A", ["geraet"] = "Quest 3", ["marke"] = "xrlabor" }));
        await k.Warten("willkommen");
        await k.Senden(XrKlient.N("messprotokoll", new JsonObject { ["aufgabe"] = "etage-wechseln", ["zeitMs"] = 1000, ["fehlgriffe"] = 0, ["abbrueche"] = 0 }));
        var fehler = await k.Warten("fehler");
        Assert.Contains("nicht gespeichert", (string)fehler["meldung"]!);
        Assert.False(Directory.Exists(echt));
        Assert.False(Directory.Exists(Path.GetDirectoryName(echt)!));
    }

    // ---------------------------------------------------------------- Messprotokolle, Auswertung, Fragebogen

    static async Task<XrKlient> Brille(LaborServer s, int port, string fp, string brille, string? marke, string name = "Christian")
    {
        var k = await XrKlient.Verbinden(port, fp, XrServer.Token(await s.Paket(brille)));
        var hallo = new JsonObject { ["name"] = name, ["geraet"] = "Quest 3", ["app"] = "0.5+d00d" };
        if (marke is not null) hallo["marke"] = marke;
        await k.Senden(XrKlient.N("hallo", hallo));
        await k.Warten("willkommen");
        return k;
    }

    [Fact]
    public async Task Messprotokoll_der_Brille_landet_in_labor_messungen_mit_Rechnername()
    {
        await using var s = new LaborServer(zugang: true);
        var (port, _, fp) = await s.Aktiv();
        await using var k = await Brille(s, port, fp, "QUEST-LABOR", "xrlabor");
        await k.Senden(XrKlient.N("messprotokoll", new JsonObject { ["aufgabe"] = "etage-wechseln", ["zeitMs"] = 3000, ["fehlgriffe"] = 1, ["abbrueche"] = 0, ["erfolg"] = true }));
        await k.Warten("quittung");
        var datei = Assert.Single(Directory.GetFiles(s.Ablage.Messungen, "*.jsonl"));
        Assert.Matches(new Regex(@"^\d{8}-\d{6}_testpc_t[0-9a-f]{8}\.jsonl$"), Path.GetFileName(datei));
        var zeile = (JsonObject)JsonNode.Parse(File.ReadAllLines(datei).Single())!;
        Assert.Equal("etage-wechseln", (string)zeile["aufgabe"]!);
        Assert.Equal(3000, (double)zeile["zeitMs"]!);
        Assert.Equal("0.5+d00d", (string)zeile["app"]!);
        // nichts in der Ablage von KICAD je PC
        Assert.False(Directory.Exists(Path.Combine(s.Xr.OrdnerPfad(), "messprotokolle")));
        var (_, j) = await s.Get("/api/xr/labor/auswertung");
        Assert.Equal(1, (int)j!["laeufe"]!);
        Assert.Equal(s.Ablage.Messungen, (string)j["ordner"]!);
    }

    [Fact]
    public async Task Auswertung_liest_alle_Dateien_aller_Rechner()
    {
        await using var s = new LaborServer(vorher: v =>
        {
            Directory.CreateDirectory(v.Ablage.Messungen);
            string Z(string zeit, string app, int ms) => new JsonObject { ["zeit"] = zeit, ["app"] = app, ["aufgabe"] = "etage-wechseln", ["zeitMs"] = ms, ["fehlgriffe"] = 0, ["abbrueche"] = 0, ["erfolg"] = true }.ToJsonString();
            File.WriteAllLines(Path.Combine(v.Ablage.Messungen, "20261020-100000_PCA_t11111111.jsonl"), [Z("2026-10-20T10:00:00Z", "0.1+aaa", 2000), Z("2026-10-20T10:01:00Z", "0.1+aaa", 4000)]);
            File.WriteAllLines(Path.Combine(v.Ablage.Messungen, "20261021-090000_PCB_t22222222.jsonl"), [Z("2026-10-21T09:00:00Z", "0.2+bbb", 6000)]);
        });
        var (_, j) = await s.Get("/api/xr/labor/auswertung");
        Assert.Equal(2, (int)j!["dateien"]!);
        Assert.Equal(3, (int)j["laeufe"]!);
        var e = j["aufgaben"]!.AsArray().Single(x => (string)x!["aufgabe"]! == "etage-wechseln")!;
        Assert.Equal(3, (int)e["laeufe"]!);
        Assert.Equal(4000, (double)e["mittelZeitMs"]!);
        Assert.Equal(["0.1+aaa", "0.2+bbb"], e["fassungen"]!.AsArray().Select(f => (string)f!["app"]!).ToArray());
    }

    [Fact]
    public async Task Fragebogen_wird_mit_Rechnername_in_labor_fragebogen_gespeichert_und_in_der_Auswertung_gezeigt()
    {
        await using var s = new LaborServer(vorher: v =>
        {
            Directory.CreateDirectory(v.Ablage.Fragebogen);
            // ein Fragebogen eines anderen Rechners
            File.WriteAllText(Path.Combine(v.Ablage.Fragebogen, "20261020-100000_PCB_anna.json"), new JsonObject { ["fassung"] = 1, ["name"] = "Anna", ["app"] = "0.1+aaa", ["zeit"] = "2026-10-20T10:00:00Z", ["komfort"] = new JsonObject { ["uebelkeit"] = 2 } }.ToJsonString());
        });
        var (code, rec) = await s.LokalPost("/api/xr/labor/fragebogen", new { name = "Christian Müller", app = "0.5+d00d", komfort = new { uebelkeit = 1, augen = 2, natuerlich = 8 } });
        Assert.Equal(HttpStatusCode.OK, code);
        var datei = Path.GetFileName((string)rec!["datei"]!);
        Assert.Matches(new Regex(@"^\d{8}-\d{6}_testpc_christian_mueller\.json$"), datei);
        Assert.True(File.Exists(Path.Combine(s.Ablage.Fragebogen, datei)));
        Assert.False(Directory.Exists(Path.Combine(s.Xr.OrdnerPfad(), "fragebogen")));
        var (_, a) = await s.Get("/api/xr/labor/auswertung");
        var namen = a!["fragebogen"]!.AsArray().Select(f => (string)f!["name"]!).ToArray();
        Assert.Equal(2, namen.Length);   // beide Rechner
        Assert.Contains("Anna", namen);
        Assert.Contains("Christian Müller", namen);
    }

    // ---------------------------------------------------------------- Marke: Prüfaufgaben und Varianten

    [Fact]
    public async Task Teilnehmerliste_nennt_die_Marke_der_Brille()
    {
        await using var s = new LaborServer(zugang: true);
        var (port, _, fp) = await s.Aktiv();
        await using var a = await Brille(s, port, fp, "QUEST-A", "xrlabor", "A");
        await using var b = await Brille(s, port, fp, "QUEST-B", "kicad", "B");
        await using var c = await Brille(s, port, fp, "QUEST-C", null, "C");
        var t = (await s.Get("/api/xr/teilnehmer")).Json!["teilnehmer"]!.AsArray();
        Assert.Equal("xrlabor", (string)t.Single(x => (string)x!["name"]! == "A")!["marke"]!);
        Assert.Equal("kicad", (string)t.Single(x => (string)x!["name"]! == "B")!["marke"]!);
        Assert.Null(t.Single(x => (string)x!["name"]! == "C")!["marke"]);
    }

    static async Task<bool> BekommtStellschraube(XrKlient k, string schluessel)
    {
        var bis = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < bis)
        {
            var n = await k.Naechste(TimeSpan.FromMilliseconds(300));
            if (n is not null && (string?)n["typ"] == "stellschraube" && (string?)n["schluessel"] == schluessel) return true;
        }
        return false;
    }

    [Fact]
    public async Task Aufgabe_und_Variante_gehen_nur_an_Brillen_der_Marke_xrlabor()
    {
        await using var s = new LaborServer(zugang: true);
        var (port, _, fp) = await s.Aktiv();
        await using var labor = await Brille(s, port, fp, "QUEST-LABOR", "xrlabor", "Labor");
        await using var kicad = await Brille(s, port, fp, "QUEST-KICAD", "kicad", "Kicad");

        var (ca, ja) = await s.LokalPost("/api/xr/labor/aufgabe", new { aufgabe = "etage-wechseln" });
        Assert.Equal(HttpStatusCode.OK, ca);
        Assert.Equal(1, (int)ja!["gesendet"]!);
        Assert.Null(ja["hinweis"]);
        Assert.True(await BekommtStellschraube(labor, "labor.aufgabe"));
        Assert.False(await BekommtStellschraube(kicad, "labor.aufgabe"));

        var (cv, jv) = await s.LokalPost("/api/xr/labor/variante", new { name = "teleport", option = "stick" });
        Assert.Equal(HttpStatusCode.OK, cv);
        Assert.Equal(1, (int)jv!["gesendet"]!);
        Assert.Null(jv["hinweis"]);
        Assert.True(await BekommtStellschraube(labor, "labor.variante.teleport"));
        Assert.False(await BekommtStellschraube(kicad, "labor.variante.teleport"));
        // gemerkt für künftige Verbindungen
        Assert.Equal("stick", (string)jv["stellschrauben"]!["labor.variante.teleport"]!);
    }

    [Fact]
    public async Task Ohne_Brille_der_Marke_xrlabor_gehen_Aufgabe_und_Variante_an_alle_mit_Hinweis()
    {
        await using var s = new LaborServer(zugang: true);
        var (port, _, fp) = await s.Aktiv();
        await using (var kicad = await Brille(s, port, fp, "QUEST-KICAD", "kicad", "Kicad"))
        await using (var alt = await Brille(s, port, fp, "QUEST-ALT", null, "Alt"))
        {
            var (ca, ja) = await s.LokalPost("/api/xr/labor/aufgabe", new { aufgabe = "etage-wechseln" });
            Assert.Equal(HttpStatusCode.OK, ca);
            Assert.Equal(2, (int)ja!["gesendet"]!);
            Assert.Contains("Keine Brille mit der App «XR-Labor» verbunden", (string)ja["hinweis"]!);
            Assert.True(await BekommtStellschraube(kicad, "labor.aufgabe"));
            Assert.True(await BekommtStellschraube(alt, "labor.aufgabe"));

            var (_, jv) = await s.LokalPost("/api/xr/labor/variante", new { name = "teleport", option = "stick" });
            Assert.Equal(2, (int)jv!["gesendet"]!);
            Assert.Contains("Keine Brille mit der App «XR-Labor»", (string)jv["hinweis"]!);
        }

        // ganz ohne Brille: die Aufgabe geht nicht, die Meldung nennt das Labor; die Variante wird gemerkt
        var bis = DateTime.UtcNow.AddSeconds(10);
        while (((JsonArray)(await s.Get("/api/xr/teilnehmer")).Json!["teilnehmer"]!).Count > 0 && DateTime.UtcNow < bis) await Task.Delay(100);
        var (cn, jn) = await s.LokalPost("/api/xr/labor/aufgabe", new { aufgabe = "etage-wechseln" });
        Assert.Equal(HttpStatusCode.BadRequest, cn);
        Assert.Contains("Es ist keine Brille verbunden", (string)jn!["fehler"]!);
        Assert.Contains("XR-Labor", (string)jn["vorschlag"]!);
        var (cb, jb) = await s.LokalPost("/api/xr/labor/variante", new { name = "teleport", option = "zeigen_pinch" });
        Assert.Equal(HttpStatusCode.OK, cb);
        Assert.Equal(0, (int)jb!["gesendet"]!);
        Assert.Null(jb["hinweis"]);
    }

    [Fact]
    public async Task Live_Stellschrauben_laufen_ueber_den_Transport_des_Labors()
    {
        await using var s = new LaborServer(zugang: true);
        var (port, _, fp) = await s.Aktiv();
        await using var k = await Brille(s, port, fp, "QUEST-LABOR", "xrlabor");
        var (c, j) = await s.LokalPost("/api/xr/stellschraube", new { schluessel = "anzeige.skala", wert = 1.7 });
        Assert.Equal(HttpStatusCode.OK, c);
        Assert.Equal(1, (int)j!["gesendet"]!);
        Assert.Equal(1.7, (double)j["stellschrauben"]!["anzeige.skala"]!);
        Assert.True(await BekommtStellschraube(k, "anzeige.skala"));
        // zurücksetzen
        var zurueck = await s.Lokal.PostAsync("/api/xr/stellschraube", new StringContent("{\"schluessel\":\"anzeige.skala\",\"wert\":null}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, zurueck.StatusCode);
        Assert.Equal(1, (int)JsonNode.Parse(await zurueck.Content.ReadAsStringAsync())!["gesendet"]!);
        Assert.False(((JsonObject)JsonNode.Parse(await s.Lokal.GetStringAsync("/api/xr/teilnehmer"))!["stellschrauben"]!).ContainsKey("anzeige.skala"));
    }

    // ---------------------------------------------------------------- Übernahme

    /// <summary>Legt Beispieldateien von KICAD an: zwei Messprotokolle, ein Fragebogen. Gibt die Pfade zurück.</summary>
    static (string Mess1, string Mess2, string Fragebogen) AltAnlegen(string xr)
    {
        var m = Path.Combine(xr, "messprotokolle");
        var f = Path.Combine(xr, "fragebogen");
        Directory.CreateDirectory(m);
        Directory.CreateDirectory(f);
        string Z(int ms) => new JsonObject { ["zeit"] = "2026-10-24T10:00:00Z", ["app"] = "0.4+abc", ["aufgabe"] = "etage-wechseln", ["zeitMs"] = ms, ["fehlgriffe"] = 0, ["abbrueche"] = 0, ["erfolg"] = true }.ToJsonString();
        var m1 = Path.Combine(m, "20261024-100000_t0a1b2c3d.jsonl");
        var m2 = Path.Combine(m, "20261025-110000_t4e5f6a7b.jsonl");
        var fb = Path.Combine(f, "20261024-120000_christian.json");
        File.WriteAllLines(m1, [Z(2000), Z(3000)]);
        File.WriteAllLines(m2, [Z(5000)]);
        File.WriteAllText(fb, new JsonObject { ["fassung"] = 1, ["name"] = "Christian", ["app"] = "0.4+abc", ["zeit"] = "2026-10-24T12:00:00Z", ["komfort"] = new JsonObject { ["uebelkeit"] = 3 } }.ToJsonString());
        return (m1, m2, fb);
    }

    [Fact]
    public async Task Uebernahme_beim_ersten_Start_kopiert_Messungen_Fragebogen_und_Profile_ohne_etwas_zu_aendern()
    {
        string m1 = "", m2 = "", fb = "";
        await using var s = new LaborServer(vorher: v =>
        {
            (m1, m2, fb) = AltAnlegen(v.AltOrdner);
            v.BenutzerEinstellungSetzen("labor.profil.sitzung", Profil("Sitzung", ("anzeige.skala", 1.5)));
            v.BenutzerEinstellungSetzen("labor.profil.alle", Profil("Alle"));
            v.BenutzerEinstellungSetzen("labor.profil_aktiv", "sitzung");
        });
        var vorher = new[] { m1, m2, fb }.Select(Sha).ToArray();

        // Messungen und Fragebogen mit dem Rechnernamen im Dateinamen, Inhalt gleich
        Assert.Equal(["20261024-100000_testpc_t0a1b2c3d.jsonl", "20261025-110000_testpc_t4e5f6a7b.jsonl"], Directory.GetFiles(s.Ablage.Messungen).Select(Path.GetFileName).Order().ToArray());
        Assert.Equal(vorher[0], Sha(Path.Combine(s.Ablage.Messungen, "20261024-100000_testpc_t0a1b2c3d.jsonl")));
        Assert.Equal(vorher[1], Sha(Path.Combine(s.Ablage.Messungen, "20261025-110000_testpc_t4e5f6a7b.jsonl")));
        Assert.Equal(vorher[2], Sha(Path.Combine(s.Ablage.Fragebogen, "20261024-120000_testpc_christian.json")));
        // Profile und das aktive Profil
        Assert.Equal(["alle.json", "sitzung.json"], Directory.GetFiles(s.Ablage.Profile).Select(Path.GetFileName).Order().ToArray());
        var (_, p) = await s.Get("/api/labor/profile");
        Assert.Equal("sitzung", (string)p!["aktiv"]!);
        Assert.Equal(1.5, (double)p["profile"]!.AsArray().Single(x => (string)x!["kennung"]! == "sitzung")!["werte"]!["anzeige.skala"]!);
        // nichts verschoben, nichts gelöscht: die Quellen sind unverändert
        Assert.Equal(vorher, new[] { m1, m2, fb }.Select(Sha).ToArray());
        var bu = new BenutzerEinstellungen(new RechnerAblage(s.BenutzerOrdner, "testpc", true, "einstellungen.json", "Umleitung"));
        Assert.NotNull(bu.Hole("labor.profil.sitzung"));
        Assert.Equal("sitzung", (string)bu.Hole("labor.profil_aktiv")!);

        // der Merker mit Zeit und Zahlen
        var merker = (JsonObject)JsonNode.Parse(File.ReadAllText(s.Ablage.UebernahmeDatei))!;
        Assert.Equal("uebernahme_testpc.json", Path.GetFileName(s.Ablage.UebernahmeDatei));
        Assert.Equal("testpc", (string)merker["rechner"]!);
        Assert.True(DateTimeOffset.TryParse((string)merker["zeit"]!, out _));
        Assert.Equal((2, 2, 0, 0), ((int)merker["messungen"]!["gefunden"]!, (int)merker["messungen"]!["kopiert"]!, (int)merker["messungen"]!["aktualisiert"]!, (int)merker["messungen"]!["uebersprungen"]!));
        Assert.Equal((1, 1), ((int)merker["fragebogen"]!["gefunden"]!, (int)merker["fragebogen"]!["kopiert"]!));
        Assert.Equal((2, 2, 0, 0), ((int)merker["profile"]!["gefunden"]!, (int)merker["profile"]!["kopiert"]!, (int)merker["profile"]!["uebersprungen"]!, (int)merker["profile"]!["abweichend"]!));
        Assert.Equal("sitzung", (string)merker["profile"]!["aktivUebernommen"]!);

        // die Auswertung zeigt die übernommenen Läufe
        var (_, a) = await s.Get("/api/xr/labor/auswertung");
        Assert.Equal(3, (int)a!["laeufe"]!);
        Assert.Single(a["fragebogen"]!.AsArray());
    }

    [Fact]
    public async Task Uebernahme_erneut_erkennt_Doppelte_aktualisiert_gewachsene_Dateien_und_laesst_geaenderte_Profile()
    {
        string m1 = "";
        await using var s = new LaborServer(vorher: v =>
        {
            (m1, _, _) = AltAnlegen(v.AltOrdner);
            v.BenutzerEinstellungSetzen("labor.profil.sitzung", Profil("Sitzung", ("anzeige.skala", 1.5)));
        });
        var anzahl = Directory.GetFiles(s.Ablage.Messungen).Length;

        // 1) nochmals: alles doppelt
        var (code, m) = await s.LokalPost("/api/labor/uebernahme", new { });
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Equal((2, 0, 0, 2), ((int)m!["messungen"]!["gefunden"]!, (int)m["messungen"]!["kopiert"]!, (int)m["messungen"]!["aktualisiert"]!, (int)m["messungen"]!["uebersprungen"]!));
        Assert.Equal((1, 0, 1), ((int)m["fragebogen"]!["gefunden"]!, (int)m["fragebogen"]!["kopiert"]!, (int)m["fragebogen"]!["uebersprungen"]!));
        Assert.Equal((1, 0, 1), ((int)m["profile"]!["gefunden"]!, (int)m["profile"]!["kopiert"]!, (int)m["profile"]!["uebersprungen"]!));
        Assert.Equal(anzahl, Directory.GetFiles(s.Ablage.Messungen).Length);

        // 2) gleicher Inhalt unter anderem Namen zählt als doppelt
        var gleich = Path.Combine(s.AltOrdner, "messprotokolle", "20261026-090000_tgleich00.jsonl");
        File.Copy(m1, gleich);
        var (_, m2) = await s.LokalPost("/api/labor/uebernahme", new { });
        Assert.Equal((3, 0, 3), ((int)m2!["messungen"]!["gefunden"]!, (int)m2["messungen"]!["kopiert"]!, (int)m2["messungen"]!["uebersprungen"]!));
        Assert.Equal(anzahl, Directory.GetFiles(s.Ablage.Messungen).Length);
        File.Delete(gleich);

        // 3) die Messdatei ist gewachsen (die Brille war noch verbunden): die Kopie wird länger, es entsteht keine zweite
        File.AppendAllText(m1, new JsonObject { ["zeit"] = "2026-10-24T10:05:00Z", ["app"] = "0.4+abc", ["aufgabe"] = "etage-wechseln", ["zeitMs"] = 4000, ["fehlgriffe"] = 0, ["abbrueche"] = 0 }.ToJsonString() + "\n");
        var (_, m3) = await s.LokalPost("/api/labor/uebernahme", new { });
        Assert.Equal((0, 1), ((int)m3!["messungen"]!["kopiert"]!, (int)m3["messungen"]!["aktualisiert"]!));
        var kopie = Path.Combine(s.Ablage.Messungen, "20261024-100000_testpc_t0a1b2c3d.jsonl");
        Assert.Equal(Sha(m1), Sha(kopie));
        Assert.Equal(anzahl, Directory.GetFiles(s.Ablage.Messungen).Length);
        Assert.Equal(4, (int)(await s.Get("/api/xr/labor/auswertung")).Json!["laeufe"]!);   // 3 Zeilen in der gewachsenen Datei + 1 in der zweiten

        // 4) im Labor geändertes Profil bleibt; das alte (anders) zählt als abweichend
        await s.LokalPost("/api/labor/profile", new { kennung = "sitzung", profil = Profil("Sitzung", ("anzeige.skala", 2.5)) });
        var (_, m4) = await s.LokalPost("/api/labor/uebernahme", new { });
        Assert.Equal((0, 0, 1), ((int)m4!["profile"]!["kopiert"]!, (int)m4["profile"]!["uebersprungen"]!, (int)m4["profile"]!["abweichend"]!));
        Assert.Equal(2.5, (double)JsonNode.Parse(File.ReadAllText(Path.Combine(s.Ablage.Profile, "sitzung.json")))!["werte"]!["anzeige.skala"]!);

        // GET /api/labor/daten: Ablage, bisherige Daten und der Merker des letzten Laufs
        var (_, d) = await s.Get("/api/labor/daten");
        Assert.Equal(2, (int)d!["ablage"]!["messungen"]!["dateien"]!);
        Assert.Equal(1, (int)d["ablage"]!["fragebogen"]!["dateien"]!);
        Assert.Equal(1, (int)d["ablage"]!["profile"]!["anzahl"]!);
        Assert.Equal("testpc", (string)d["ablage"]!["rechner"]!);
        Assert.Equal(2, (int)d["alt"]!["messprotokolle"]!["dateien"]!);
        Assert.Equal(1, (int)d["alt"]!["profile"]!["anzahl"]!);
        Assert.Equal(1, (int)d["uebernahme"]!["profile"]!["abweichend"]!);
    }

    [Fact]
    public void Uebernahme_laeuft_beim_ersten_Start_nur_einmal()
    {
        var wurzel = Path.Combine(Path.GetTempPath(), "hlks_labor_u_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var ablage = new LaborAblage(Path.Combine(wurzel, "labor"), "PC1", "Umleitung", testlauf: true);
            var alt = new LaborAltdaten(Path.Combine(wurzel, "xr"));
            AltAnlegen(alt.XrOrdner);
            var benutzer = new BenutzerEinstellungen(new RechnerAblage(Path.Combine(wurzel, "benutzer"), "pc1", true, "einstellungen.json", "Umleitung"));
            var u = new LaborUebernahme(ablage, new LaborProfile(ablage, () => DateTimeOffset.UtcNow), benutzer, alt, () => DateTimeOffset.UtcNow);
            Assert.Null(u.Merker());
            Assert.True(u.ErsterStart());
            Assert.NotNull(u.Merker());
            Assert.Equal(["20261024-100000_PC1_t0a1b2c3d.jsonl", "20261025-110000_PC1_t4e5f6a7b.jsonl"], Directory.GetFiles(ablage.Messungen).Select(Path.GetFileName).Order().ToArray());
            // gelöscht und neu gestartet: mit Merker kein zweiter Lauf, die Dateien kommen nicht zurück
            foreach (var f in Directory.GetFiles(ablage.Messungen)) File.Delete(f);
            Assert.False(u.ErsterStart());
            Assert.Empty(Directory.GetFiles(ablage.Messungen));
            Assert.Equal("20261024-100000_PC1_x.jsonl", u.ZielName("20261024-100000_x.jsonl"));
            Assert.Equal("00000000-000000_PC1_ohne.jsonl", u.ZielName("ohne.jsonl"));
        }
        finally { try { Directory.Delete(wurzel, true); } catch (IOException) { } }
    }

    [Fact]
    public void Uebernahme_liest_im_Testlauf_nur_Quellen_unter_TEMP()
    {
        var wurzel = Path.Combine(Path.GetTempPath(), "hlks_labor_u2_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var ablage = new LaborAblage(Path.Combine(wurzel, "labor"), "PC1", "Umleitung", testlauf: true);
            var echt = new LaborAltdaten(Path.GetDirectoryName(EchterOrdner())!);   // kein Ordner unter %TEMP%
            var benutzer = new BenutzerEinstellungen(new RechnerAblage(Path.Combine(wurzel, "benutzer"), "pc1", true, "einstellungen.json", "Umleitung"));
            var u = new LaborUebernahme(ablage, new LaborProfile(ablage, () => DateTimeOffset.UtcNow), benutzer, echt, () => DateTimeOffset.UtcNow);
            Assert.False(u.LesenErlaubt);
            Assert.False(u.ErsterStart());   // ohne Lesen kein Start und kein Merker
            Assert.Null(u.Merker());
            Assert.False((bool)u.Vorhanden()["lesenErlaubt"]!);
            // auf Knopfdruck: gelesen wird trotzdem nichts, der Merker sagt es
            var m = u.Ausfuehren();
            Assert.False((bool)m["lesenErlaubt"]!);
            Assert.Equal(0, (int)m["messungen"]!["gefunden"]!);
            // ausserhalb eines Tests liest dieselbe Quelle
            Assert.True(new LaborUebernahme(new LaborAblage(ablage.Verzeichnis, "PC1", "Umleitung", testlauf: false), new LaborProfile(ablage, () => DateTimeOffset.UtcNow), benutzer, echt, () => DateTimeOffset.UtcNow).LesenErlaubt);
        }
        finally { try { Directory.Delete(wurzel, true); } catch (IOException) { } }
    }

    [Fact]
    public async Task Das_Labor_schreibt_nie_in_die_Ablage_von_KICAD_oder_die_Benutzereinstellungen()
    {
        await using var s = new LaborServer(vorher: v =>
        {
            AltAnlegen(v.AltOrdner);
            v.BenutzerEinstellungSetzen("labor.profil.sitzung", Profil("Sitzung"));
        });
        var vorher = Directory.GetFiles(s.BenutzerOrdner, "*", SearchOption.AllDirectories).Select(f => (f, Sha(f))).ToList();
        await s.LokalPost("/api/labor/profile", new { kennung = "neu", profil = Profil("Neu") });
        await s.LokalPost("/api/labor/profile/aktiv", new { kennung = "neu" });
        await s.LokalDelete("/api/labor/profile/sitzung");
        await s.LokalPost("/api/labor/uebernahme", new { });
        Assert.Equal(vorher, Directory.GetFiles(s.BenutzerOrdner, "*", SearchOption.AllDirectories).Select(f => (f, Sha(f))).ToList());
        // die Dateien von KICAD sind unverändert und vollständig
        Assert.Equal(2, Directory.GetFiles(Path.Combine(s.AltOrdner, "messprotokolle")).Length);
        Assert.Single(Directory.GetFiles(Path.Combine(s.AltOrdner, "fragebogen")));
    }
}
