using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Hlks.Web.Tests;

/// <summary>
/// Zweiter Zugang im Büronetz (Quest App P3, XrZugang.cs): nur bei xr.zugang an, nur /api/xr, nur mit Schlüssel, TLS mit gepinntem Fingerabdruck, Grenzen,
/// Ein-/Ausschalten, Kopplung über das Netz. Der Netzzugang bindet in den Tests nur 127.0.0.1 mit freien Ports.
/// </summary>
public class XrZugangTests
{
    static int FreierPort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var p = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return p;
    }

    static async Task<bool> Erreichbar(int port)
    {
        using var c = new TcpClient();
        try { await c.ConnectAsync(IPAddress.Loopback, port).WaitAsync(TimeSpan.FromSeconds(3)); return true; }
        catch (Exception ex) when (ex is SocketException or TimeoutException) { return false; }
    }

    static async Task<(HttpStatusCode Code, JsonNode? Json, HttpResponseMessage Antwort)> Holen(HttpClient c, HttpMethod m, string pfad, string? koerper = null, string typ = "application/json")
    {
        var r = new HttpRequestMessage(m, pfad);
        if (koerper is not null) r.Content = new StringContent(koerper, Encoding.UTF8, typ);
        var a = await c.SendAsync(r);
        var t = await a.Content.ReadAsStringAsync();
        JsonNode? j = null;
        try { if (t.Length > 0) j = JsonNode.Parse(t); } catch (System.Text.Json.JsonException) { }
        return (a.StatusCode, j, a);
    }

    // ---------------------------------------------------------------- aus / an

    [Fact]
    public async Task Bei_xr_zugang_aus_gibt_es_keinen_Netzendpunkt()
    {
        int port = FreierPort(), suchPort = FreierPort();
        await using var s = new XrServer(zugang: false, anpassen: x => x with { Port = port, SuchPort = suchPort });
        var z = await s.Zugang();
        Assert.False((bool)z["an"]!);
        Assert.False((bool)z["aktiv"]!);
        Assert.False((bool)z["sucheAktiv"]!);
        Assert.Null(z["fehler"]);
        Assert.False(s.Dienste.GetRequiredService<XrNetzDienst>().Aktiv);
        Assert.False(await Erreichbar(port));
        // die Oberfläche läuft wie immer
        Assert.Equal(HttpStatusCode.OK, (await s.Lokal.GetAsync("/api/status")).StatusCode);
    }

    [Fact]
    public async Task Vorgabe_ist_aus()
    {
        await using var s = new XrServer(zugang: false);
        var z = await s.Zugang();
        Assert.False((bool)z["an"]!);
        Assert.Equal(Path.Combine(s.XrOrdner, "zugang.json"), (string)z["schalter"]!);
        Assert.False(File.Exists(Path.Combine(s.XrOrdner, "zugang.json")));
    }

    [Fact]
    public async Task Eine_Benutzereinstellung_xr_zugang_oeffnet_nichts_der_Schalter_gilt_je_PC()
    {
        // Christian 23.10.2026 «Je PC»: die Benutzereinstellungen gelten über OneDrive auf allen Rechnern – sie dürfen den Zugang nicht öffnen
        await using var s = new XrServer(zugang: false);
        s.BenutzerEinstellungSetzen("xr.zugang", JsonValue.Create(true));
        await s.Dienste.GetRequiredService<XrNetzDienst>().Abgleichen();
        var z = await s.Zugang();
        Assert.False((bool)z["an"]!);
        Assert.False((bool)z["aktiv"]!);
        // Schalten schreibt nur die Datei dieses PCs, nie die Benutzereinstellungen
        await s.LokalPost("/api/xr/zugang", new { an = true });
        Assert.True(File.Exists(Path.Combine(s.XrOrdner, "zugang.json")));
        Assert.DoesNotContain(Directory.GetFiles(s.BenutzerOrdner, "*.json").Select(File.ReadAllText), t => t.Contains("\"an\"", StringComparison.Ordinal));
        Assert.True((bool)(await s.Zugang())["aktiv"]!);
    }

    [Fact]
    public async Task Bei_an_nur_api_xr_sonst_404_ohne_oder_mit_falschem_Schluessel_401_mit_Schluessel_200()
    {
        // viele Anfragen und Fehlversuche von 127.0.0.1: die Bremse hier weit (sie hat eigene Tests)
        await using var s = new XrServer(anpassen: x => x with { FehlversucheMax = 1000, AnfragenVorrat = 1000 });
        var (port, _, fp) = await s.Aktiv();
        var paket = await s.Paket();
        using var ohne = XrServer.Netz(port, fp);
        using var falsch = XrServer.Netz(port, fp, $"{(string)paket["kopplung"]!}.{new string('A', 43)}");
        using var mit = XrServer.Netz(port, fp, XrServer.Token(paket));

        // ausserhalb /api/xr: 404, auch mit Schlüssel (Oberfläche, Dateien, übriges /api, Hilfe, VR-Seite)
        foreach (var pfad in new[] { "/", "/index.html", "/api/status", "/api/modell", "/hilfe/", "/vr", "/vr.html", "/api/vr/auftrag", "/api/quest", "/api/benutzer/einstellungen",
                     "/api/xr/../status", "/api/xrx", "/api/xr%2F..%2Fstatus", "/api" })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await Holen(ohne, HttpMethod.Get, pfad)).Code);
            Assert.Equal(HttpStatusCode.NotFound, (await Holen(mit, HttpMethod.Get, pfad)).Code);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await Holen(mit, HttpMethod.Post, "/api/befehl/element_anlegen", "{}")).Code);

        // unter /api/xr: ohne bzw. mit falschem Schlüssel 401 (mit WWW-Authenticate), mit Schlüssel 200
        var a1 = await Holen(ohne, HttpMethod.Get, "/api/xr/verbindung");
        Assert.Equal(HttpStatusCode.Unauthorized, a1.Code);
        Assert.Equal("Bearer", a1.Antwort.Headers.WwwAuthenticate.ToString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await Holen(falsch, HttpMethod.Get, "/api/xr/verbindung")).Code);
        var a3 = await Holen(mit, HttpMethod.Get, "/api/xr/verbindung");
        Assert.Equal(HttpStatusCode.OK, a3.Code);
        Assert.True((bool)a3.Json!["ok"]!);
        Assert.Equal((string)paket["kopplung"]!, (string)a3.Json["kopplung"]!["id"]!);
        Assert.Equal(HttpStatusCode.OK, (await Holen(mit, HttpMethod.Get, "/API/XR/Verbindung")).Code);   // Gross-/Kleinschreibung wie im Routing

        // PC-Endpunkte unter /api/xr: ohne Schlüssel 401 (verrät nichts), mit Schlüssel 404
        foreach (var (m, pfad) in new[] { (HttpMethod.Get, "/api/xr/zugang"), (HttpMethod.Get, "/api/xr/teilnehmer"), (HttpMethod.Post, "/api/xr/stellschraube"),
                     (HttpMethod.Post, "/api/xr/kopplung/paket"), (HttpMethod.Post, "/api/xr/zugang"), (HttpMethod.Post, "/api/xr/kopplung/bestaetigen"),
                     (HttpMethod.Post, "/api/xr/kopplung/widerrufen"), (HttpMethod.Get, "/api/xr/gibtsnicht") })
        {
            var k = m == HttpMethod.Post ? "{\"brille\":\"BOESE\",\"an\":true}" : null;
            Assert.Equal(HttpStatusCode.Unauthorized, (await Holen(ohne, m, pfad, k)).Code);
            Assert.Equal(HttpStatusCode.NotFound, (await Holen(mit, m, pfad, k)).Code);
        }
        Assert.Single((await s.Zugang())["kopplungen"]!.AsArray());   // keine Kopplung über das Netz angelegt
        Assert.Equal(HttpStatusCode.NotFound, (await Holen(mit, HttpMethod.Post, "/api/xr/verbindung", "{}")).Code);   // falsche Methode
    }

    [Fact]
    public async Task TLS_mit_gepinntem_Fingerabdruck_ab_TLS_1_2()
    {
        await using var s = new XrServer();
        var (port, _, fp) = await s.Aktiv();
        Assert.Equal(fp, (string)(await s.Paket())["fingerabdruck"]!);

        // falscher Fingerabdruck: die App bricht ab
        using (var fremd = XrServer.Netz(port, new string('0', 64)))
            await Assert.ThrowsAsync<HttpRequestException>(() => fremd.GetAsync("/api/xr/verbindung"));

        // ausgehandelt wird TLS 1.2 oder 1.3; das Zertifikat ist das aus GET /api/xr/zugang
        using (var tcp = new TcpClient())
        {
            await tcp.ConnectAsync(IPAddress.Loopback, port);
            await using var ssl = new SslStream(tcp.GetStream(), false, (_, z, _, _) => XrServer.Pinnen(z, fp));
            await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = "kicad", EnabledSslProtocols = SslProtocols.None });
            Assert.True(ssl.SslProtocol is SslProtocols.Tls12 or SslProtocols.Tls13, ssl.SslProtocol.ToString());
            Assert.True(XrServer.Pinnen(ssl.RemoteCertificate, fp));
        }
#pragma warning disable SYSLIB0039 // TLS 1.1 absichtlich: muss scheitern
        using (var tcp = new TcpClient())
        {
            await tcp.ConnectAsync(IPAddress.Loopback, port);
            await using var ssl = new SslStream(tcp.GetStream(), false, (_, _, _, _) => true);
            await Assert.ThrowsAnyAsync<Exception>(() => ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = "kicad", EnabledSslProtocols = SslProtocols.Tls11 }));
        }
#pragma warning restore SYSLIB0039
        // ohne TLS: keine Antwort im Klartext
        using (var klar = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(10) })
        {
            var ok = false;
            try { ok = (await klar.GetAsync("/api/xr/verbindung")).IsSuccessStatusCode; }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { }
            Assert.False(ok);
        }
    }

    [Fact]
    public async Task Koerper_zu_gross_413_und_nur_JSON_415()
    {
        await using var s = new XrServer();
        var (port, _, fp) = await s.Aktiv();
        using var ohne = XrServer.Netz(port, fp);
        var gross = "{\"brille\":\"" + new string('x', 70 * 1024) + "\"}";
        // «Expect: 100-continue»: der Client wartet mit dem Körper, bis der Server ihn will – so kommt die frühe Ablehnung sicher an (sonst kann die Verbindung beim Hochladen abreissen)
        var r = new HttpRequestMessage(HttpMethod.Post, "/api/xr/kopplung/anfrage") { Content = new StringContent(gross, Encoding.UTF8, "application/json") };
        r.Headers.ExpectContinue = true;
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await ohne.SendAsync(r)).StatusCode);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, (await Holen(ohne, HttpMethod.Post, "/api/xr/kopplung/anfrage", "brille=Q", "text/plain")).Code);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, (await Holen(ohne, HttpMethod.Post, "/api/xr/kopplung/anfrage", "brille=Q", "application/x-www-form-urlencoded")).Code);
    }

    [Fact]
    public async Task Nach_zehn_Fehlversuchen_ist_die_Adresse_gesperrt()
    {
        await using var s = new XrServer();
        var (port, _, fp) = await s.Aktiv();
        var paket = await s.Paket();
        using var falsch = XrServer.Netz(port, fp, $"{(string)paket["kopplung"]!}.{new string('B', 43)}");
        using var mit = XrServer.Netz(port, fp, XrServer.Token(paket));
        Assert.Equal(HttpStatusCode.OK, (await Holen(mit, HttpMethod.Get, "/api/xr/verbindung")).Code);
        for (int i = 0; i < 10; i++) Assert.Equal(HttpStatusCode.Unauthorized, (await Holen(falsch, HttpMethod.Get, "/api/xr/verbindung")).Code);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Holen(falsch, HttpMethod.Get, "/api/xr/verbindung")).Code);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Holen(mit, HttpMethod.Get, "/api/xr/verbindung")).Code);   // gilt für die Adresse
        Assert.Equal(HttpStatusCode.OK, (await s.Lokal.GetAsync("/api/xr/zugang")).StatusCode);   // localhost unberührt
    }

    [Fact]
    public async Task Zu_viele_Anfragen_ergeben_429()
    {
        var uhr = new XrTestUhr();
        await using var s = new XrServer(anpassen: x => x with { AnfragenVorrat = 5, AnfragenJeSekunde = 1 }, uhr: uhr);
        var (port, _, fp) = await s.Aktiv();
        using var mit = XrServer.Netz(port, fp, XrServer.Token(await s.Paket()));
        for (int i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.OK, (await Holen(mit, HttpMethod.Get, "/api/xr/verbindung")).Code);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Holen(mit, HttpMethod.Get, "/api/xr/verbindung")).Code);
        uhr.Vor(TimeSpan.FromSeconds(2));
        Assert.Equal(HttpStatusCode.OK, (await Holen(mit, HttpMethod.Get, "/api/xr/verbindung")).Code);
    }

    [Fact]
    public async Task Ein_und_Ausschalten_ohne_Neustart()
    {
        int port = FreierPort();
        await using var s = new XrServer(zugang: false, anpassen: x => x with { Port = port });
        Assert.False(await Erreichbar(port));
        var (c1, j1) = await s.LokalPost("/api/xr/zugang", new { an = true });
        Assert.Equal(HttpStatusCode.OK, c1);
        Assert.True((bool)j1!["an"]!);
        Assert.True((bool)j1["aktiv"]!);
        Assert.Equal(port, (int)j1["port"]!);
        Assert.True(await Erreichbar(port));
        Assert.True(new XrPcEinstellung(s.Xr).Lesen().An);   // in der Datei dieses PCs
        Assert.Null(s.Dienste.GetRequiredService<ProjektDienst>().Benutzer.Hole("xr.zugang"));   // nicht in den Benutzereinstellungen

        var (_, j2) = await s.LokalPost("/api/xr/zugang", new { an = false });
        Assert.False((bool)j2!["aktiv"]!);
        Assert.False(await Erreichbar(port));
        Assert.False(new XrPcEinstellung(s.Xr).Lesen().An);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.LokalPost("/api/xr/zugang", new { an = "ja" })).Code);
    }

    [Fact]
    public async Task Ports_aus_dem_Schalter_dieses_PCs_geaenderte_Ports_starten_neu()
    {
        int p1 = FreierPort(), s1 = FreierPort(), p2 = FreierPort();
        await using var s = new XrServer(zugang: false, anpassen: x => x with { Port = null, SuchPort = null });
        var (c1, j1) = await s.LokalPost("/api/xr/zugang", new { an = true, port = p1, suchPort = s1 });
        Assert.Equal(HttpStatusCode.OK, c1);
        Assert.Equal((p1, s1), ((int)j1!["port"]!, (int)j1["suchPort"]!));
        Assert.True(await Erreichbar(p1));
        var (_, j2) = await s.LokalPost("/api/xr/zugang", new { an = true, port = p2 });   // Suchport bleibt
        Assert.Equal((p2, s1), ((int)j2!["port"]!, (int)j2["suchPort"]!));
        Assert.True(await Erreichbar(p2));
        Assert.False(await Erreichbar(p1));
        Assert.Equal(HttpStatusCode.BadRequest, (await s.LokalPost("/api/xr/zugang", new { an = true, port = 80 })).Code);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.LokalPost("/api/xr/zugang", new { an = true, suchPort = "x" })).Code);
        await s.LokalPost("/api/xr/zugang", new { an = false });
    }

    [Fact]
    public async Task Abgleich_nach_Aenderung_durch_einen_anderen_KICAD_Prozess_auf_diesem_PC()
    {
        await using var s = new XrServer(zugang: false);
        s.SchalterSetzen(true);   // z. B. das Desktop-KICAD hat geschaltet; dieser Prozess liest dieselbe Datei
        await s.Dienste.GetRequiredService<XrNetzDienst>().Abgleichen();   // sonst im Takt von 10 s
        await s.Aktiv();
        s.SchalterSetzen(false);
        await s.Dienste.GetRequiredService<XrNetzDienst>().Abgleichen();
        Assert.False((bool)(await s.Zugang())["aktiv"]!);
    }

    [Fact]
    public async Task Nur_das_KICAD_auf_dem_Desktop_Port_oeffnet_den_Zugang()
    {
        // der Testserver läuft (ohne --port) mit dem Hauptport 5130; verlangt ist hier 5999
        await using var s = new XrServer(anpassen: x => x with { DesktopPort = 5999 });
        await s.Dienste.GetRequiredService<XrNetzDienst>().Abgleichen();
        var z = await s.Zugang();
        Assert.True((bool)z["an"]!);
        Assert.False((bool)z["aktiv"]!);
        Assert.Contains("5999", (string)z["fehler"]!);
    }

    [Fact]
    public async Task Belegter_Port_laesst_den_Zugang_aus_und_die_Oberflaeche_laeuft()
    {
        var belegt = new TcpListener(IPAddress.Loopback, 0) { ExclusiveAddressUse = true };
        belegt.Start();
        try
        {
            var port = ((IPEndPoint)belegt.LocalEndpoint).Port;
            await using var s = new XrServer(anpassen: x => x with { Port = port });
            await s.Dienste.GetRequiredService<XrNetzDienst>().Abgleichen();
            var z = await s.Zugang();
            Assert.False((bool)z["aktiv"]!);
            Assert.Contains("nicht öffnen", (string)z["fehler"]!);
            Assert.Equal(HttpStatusCode.OK, (await s.Lokal.GetAsync("/api/status")).StatusCode);
        }
        finally { belegt.Stop(); }
    }

    [Fact]
    public async Task Zustand_zeigt_Zertifikat_Adressen_Kopplungen_und_nie_Schluessel()
    {
        await using var s = new XrServer();
        var (port, suchPort, fp) = await s.Aktiv();
        var paket = await s.Paket("QUEST-Z", "Quest Zimmer");
        var z = await s.Zugang();
        Assert.Equal("127.0.0.1", (string)z["adresse"]!);
        Assert.Equal(["127.0.0.1"], z["ips"]!.AsArray().Select(x => (string)x!));
        Assert.True((bool)z["sucheAktiv"]!);
        Assert.Matches("^[0-9A-F]{4} [0-9A-F]{4} [0-9A-F]{4} [0-9A-F]{4}$", (string)z["fingerabdruckKurz"]!);
        var k = Assert.Single(z["kopplungen"]!.AsArray());
        Assert.Equal("QUEST-Z", (string)k!["brille"]!);
        Assert.Equal("Quest Zimmer", (string)k["name"]!);
        Assert.False((bool)k["verbunden"]!);
        Assert.DoesNotContain((string)paket["schluessel"]!, z.ToJsonString());
        Assert.Equal(port, (int)paket["port"]!);
        Assert.Equal(suchPort, (int)paket["suchPort"]!);
        Assert.Equal(["127.0.0.1"], paket["ips"]!.AsArray().Select(x => (string)x!));
        Assert.Equal(Environment.MachineName, (string)paket["pc"]!);
        var roh = await s.Lokal.PostAsync("/api/xr/kopplung/paket", new StringContent("{\"brille\":\"QUEST-Y\"}", Encoding.UTF8, "application/json"));
        Assert.Equal("no-store", roh.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Localhost_bleibt_hinter_dem_Herkunftsschutz()
    {
        await using var s = new XrServer(zugang: false);
        var r = new HttpRequestMessage(HttpMethod.Get, "/api/xr/zugang");
        r.Headers.Host = "boese.example";
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Lokal.SendAsync(r)).StatusCode);
        var p = new HttpRequestMessage(HttpMethod.Post, "/api/xr/kopplung/paket") { Content = new StringContent("{\"brille\":\"Q\"}", Encoding.UTF8, "application/json") };
        p.Headers.TryAddWithoutValidation("Origin", "https://boese.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Lokal.SendAsync(p)).StatusCode);
        var t = new HttpRequestMessage(HttpMethod.Post, "/api/xr/zugang") { Content = new StringContent("{\"an\":true}", Encoding.UTF8, "text/plain") };
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, (await s.Lokal.SendAsync(t)).StatusCode);
        Assert.False((bool)(await s.Zugang())["an"]!);
        // Brillen-Endpunkte auch auf localhost nur mit Schlüssel
        Assert.Equal(HttpStatusCode.Unauthorized, (await s.Lokal.GetAsync("/api/xr/verbindung")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await s.Lokal.GetAsync("/api/xr/kanal")).StatusCode);
        var paket = await s.Paket();
        var mit = new HttpRequestMessage(HttpMethod.Get, "/api/xr/kanal");
        mit.Headers.Authorization = new AuthenticationHeaderValue("Bearer", XrServer.Token(paket));
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Lokal.SendAsync(mit)).StatusCode);   // kein WebSocket auf localhost
    }

    // ---------------------------------------------------------------- Ersatzkopplung über das Netz (Weg B: Codes vergleichen, nur am PC bestätigen)

    /// <summary>Schritte 1 und 2 wie die App beim ersten Kontakt: anfrage mit Verpflichtung, aufdecken; der Code wird aus dem in TLS gesehenen Zertifikat gerechnet.</summary>
    static async Task<(string Anfrage, string CodeBrille, string ZufallPc, string ZufallBrille)> AnfragenUndAufdecken(HttpClient brille, Func<string?> gesehen, string kennung = "QUEST-B", string? name = null)
    {
        var b = new XrTestBrille();
        var (c1, j1, _) = await Holen(brille, HttpMethod.Post, "/api/xr/kopplung/anfrage", Hlks.Kern.Json.Text(new { brille = kennung, name, verpflichtung = b.Verpflichtung }));
        Assert.Equal(HttpStatusCode.OK, c1);
        var anfrage = (string)j1!["anfrage"]!;
        var zufallPc = (string)j1["zufallPc"]!;
        Assert.Equal(5, (int)j1["versucheMax"]!);
        Assert.Null(j1["code"]);
        var (c2, j2, _) = await Holen(brille, HttpMethod.Post, "/api/xr/kopplung/aufdecken", Hlks.Kern.Json.Text(new { anfrage, zufallBrille = b.ZufallText }));
        Assert.Equal(HttpStatusCode.OK, c2);
        Assert.Equal("vergleichen", (string)j2!["status"]!);
        Assert.Null(j2["code"]);   // der Code geht nie an die Brille
        return (anfrage, XrTestBrille.Code(gesehen()!, b.ZufallText, zufallPc), zufallPc, b.ZufallText);
    }

    [Fact]
    public async Task Weg_B_ueber_das_Netz_gleicher_Code_auf_beiden_Seiten_Paket_erst_nach_Bestaetigung_bis_zum_Widerruf()
    {
        await using var s = new XrServer();
        var (port, _, fp) = await s.Aktiv();
        var (brille, gesehen) = XrServer.NetzErstkontakt(port);
        using var _b = brille;
        var (anfrage, codeBrille, _, _) = await AnfragenUndAufdecken(brille, gesehen, name: "Quest Gast");
        Assert.Equal(fp, gesehen());   // ohne jemanden dazwischen sieht die Brille das Zertifikat von KICAD

        var offen = Assert.Single((await s.Zugang())["anfragen"]!.AsArray())!;
        Assert.Equal("127.0.0.1", (string)offen["ip"]!);
        Assert.Equal(codeBrille, (string)offen["code"]!);   // derselbe Code in der Brille und am PC

        var (c3, j3, _) = await Holen(brille, HttpMethod.Post, "/api/xr/kopplung/abholen", Hlks.Kern.Json.Text(new { anfrage }));
        Assert.Equal(HttpStatusCode.Accepted, c3);   // erst nach der Bestätigung am PC
        Assert.Equal("wartet", (string)j3!["status"]!);
        Assert.Null(j3["paket"]);

        Assert.Equal(HttpStatusCode.OK, (await s.LokalPost("/api/xr/kopplung/bestaetigen", new { anfrage })).Code);
        var (c4, j4, a4) = await Holen(brille, HttpMethod.Post, "/api/xr/kopplung/abholen", Hlks.Kern.Json.Text(new { anfrage }));
        Assert.Equal(HttpStatusCode.OK, c4);
        Assert.Equal("no-store", a4.Headers.CacheControl?.ToString());
        var paket = (JsonObject)j4!["paket"]!;
        Assert.Equal(fp, (string)paket["fingerabdruck"]!);   // die App prüft: gleich dem in TLS gesehenen, dann pinnen
        Assert.Equal("QUEST-B", (string)paket["brille"]!);
        Assert.Equal("Quest Gast", (string)paket["name"]!);
        Assert.Equal(HttpStatusCode.NotFound, (await Holen(brille, HttpMethod.Post, "/api/xr/kopplung/abholen", Hlks.Kern.Json.Text(new { anfrage }))).Code);   // nur einmal

        using var mit = XrServer.Netz(port, fp, XrServer.Token(paket));
        Assert.Equal(HttpStatusCode.OK, (await Holen(mit, HttpMethod.Get, "/api/xr/verbindung")).Code);
        var (cw, jw) = await s.LokalPost("/api/xr/kopplung/widerrufen", new { kopplung = (string)paket["kopplung"]! });
        Assert.Equal(HttpStatusCode.OK, cw);
        Assert.True((bool)jw!["widerrufen"]!);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Holen(mit, HttpMethod.Get, "/api/xr/verbindung")).Code);
    }

    [Fact]
    public async Task Weg_B_bestaetigen_erst_mit_Code_und_ablehnen_am_PC()
    {
        await using var s = new XrServer();
        var (port, _, _) = await s.Aktiv();
        var (brille, gesehen) = XrServer.NetzErstkontakt(port);
        using var _b = brille;
        // nur angefragt, noch nicht aufgedeckt: kein Code am PC, Bestätigen geht nicht, Abholen verlangt das Aufdecken
        var b = new XrTestBrille();
        var (_, j1, _) = await Holen(brille, HttpMethod.Post, "/api/xr/kopplung/anfrage", Hlks.Kern.Json.Text(new { brille = "QUEST-B", verpflichtung = b.Verpflichtung }));
        var anfrage = (string)j1!["anfrage"]!;
        Assert.Null(Assert.Single((await s.Zugang())["anfragen"]!.AsArray())!["code"]);
        Assert.Equal(HttpStatusCode.Conflict, (await s.LokalPost("/api/xr/kopplung/bestaetigen", new { anfrage })).Code);
        Assert.Equal(HttpStatusCode.Conflict, (await Holen(brille, HttpMethod.Post, "/api/xr/kopplung/abholen", Hlks.Kern.Json.Text(new { anfrage }))).Code);
        // falsche Zufallszahl: 403 mit Restversuchen
        var (cf, jf, _) = await Holen(brille, HttpMethod.Post, "/api/xr/kopplung/aufdecken", Hlks.Kern.Json.Text(new { anfrage, zufallBrille = new XrTestBrille().ZufallText }));
        Assert.Equal(HttpStatusCode.Forbidden, cf);
        Assert.Equal(4, (int)jf!["versucheUebrig"]!);
        Assert.Equal(HttpStatusCode.OK, (await Holen(brille, HttpMethod.Post, "/api/xr/kopplung/aufdecken", Hlks.Kern.Json.Text(new { anfrage, zufallBrille = b.ZufallText }))).Code);
        // der Mensch sieht verschiedene Codes und lehnt ab
        Assert.Equal(HttpStatusCode.OK, (await s.LokalPost("/api/xr/kopplung/ablehnen", new { anfrage })).Code);
        var (c, j, _) = await Holen(brille, HttpMethod.Post, "/api/xr/kopplung/abholen", Hlks.Kern.Json.Text(new { anfrage }));
        Assert.Equal(HttpStatusCode.Forbidden, c);
        Assert.Equal("abgelehnt", (string)j!["status"]!);
        Assert.Equal(HttpStatusCode.NotFound, (await s.LokalPost("/api/xr/kopplung/bestaetigen", new { anfrage })).Code);
        Assert.Empty((await s.Zugang())["kopplungen"]!.AsArray());
        Assert.NotNull(gesehen());
    }

    [Fact]
    public async Task Weg_B_mit_anderem_Zertifikat_dazwischen_zeigt_die_Brille_einen_anderen_Code()
    {
        // Wer dazwischen sitzt, kann der Brille nur ein eigenes Zertifikat zeigen: die Brille rechnet mit dessen Fingerabdruck und sieht einen anderen Code als der PC.
        await using var s = new XrServer();
        var (port, _, fp) = await s.Aktiv();
        var (brille, _) = XrServer.NetzErstkontakt(port);
        using var _b = brille;
        using var fremd = XrZertifikat.Neu("ANGREIFER", DateTimeOffset.UtcNow);
        var fpFremd = XrZertifikat.FingerabdruckVon(fremd);
        Assert.NotEqual(fp, fpFremd);
        var (anfrage, codeEcht, zufallPc, zufallBrille) = await AnfragenUndAufdecken(brille, () => fp);
        var amPc = (string)(await s.Zugang())["anfragen"]!.AsArray().Single(a => (string)a!["anfrage"]! == anfrage)!["code"]!;
        Assert.Equal(amPc, codeEcht);
        // dieselben Zufallszahlen, aber das Zertifikat dessen dazwischen: anderer Code in der Brille → der Mensch lehnt ab
        Assert.NotEqual(amPc, XrTestBrille.Code(fpFremd, zufallBrille, zufallPc));
    }

    // ---------------------------------------------------------------- Pfadsperre und Freigabe (rein)

    [Fact]
    public void Pfadsperre_nur_api_xr_mit_ganzen_Segmenten()
    {
        Assert.True(XrPforte.PfadErlaubt("/api/xr"));
        Assert.True(XrPforte.PfadErlaubt("/api/xr/kanal"));
        Assert.True(XrPforte.PfadErlaubt("/API/XR/kanal"));
        Assert.False(XrPforte.PfadErlaubt("/api/xrx"));
        Assert.False(XrPforte.PfadErlaubt("/api/xr%2F..%2Fstatus"));
        Assert.False(XrPforte.PfadErlaubt("/api/status"));
        Assert.False(XrPforte.PfadErlaubt("/"));
        Assert.False(XrPforte.PfadErlaubt("/xr/api/xr"));
    }

    [Fact]
    public async Task Freigegeben_sind_nur_markierte_Endpunkte_unter_api_xr()
    {
        await using var s = new XrServer(zugang: false);
        await s.Lokal.GetAsync("/api/status");   // Endpunkte stehen fest
        var alle = s.Dienste.GetRequiredService<EndpointDataSource>().Endpoints;
        var frei = XrPforte.Freigegeben(alle).OfType<RouteEndpoint>()
            .Select(e => $"{string.Join(",", e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [])} {e.RoutePattern.RawText}").OrderBy(x => x, StringComparer.Ordinal).ToList();
        Assert.Equal(["GET /api/xr/ebene/{name}", "GET /api/xr/ebenen",   // P4b: Ebenen der Fachmodule (XrEbenenEndpunkte.cs)
            "GET /api/xr/geschoss/{id}.glb", "GET /api/xr/geschosse", "GET /api/xr/kanal", "GET /api/xr/verbindung",
            "POST /api/xr/kopplung/abholen", "POST /api/xr/kopplung/anfrage", "POST /api/xr/kopplung/aufdecken"], frei);
        // ohne Schlüssel nur die drei Schritte der Ersatzkopplung
        var ohne = XrPforte.Freigegeben(alle).Where(e => e.Metadata.GetMetadata<XrNetzFreigabe>()!.OhneSchluessel).OfType<RouteEndpoint>()
            .Select(e => e.RoutePattern.RawText).OrderBy(x => x, StringComparer.Ordinal);
        Assert.Equal(["/api/xr/kopplung/abholen", "/api/xr/kopplung/anfrage", "/api/xr/kopplung/aufdecken"], ohne);
        // eine Marke ausserhalb /api/xr zählt nicht
        var fremd = new RouteEndpoint(_ => Task.CompletedTask, Microsoft.AspNetCore.Routing.Patterns.RoutePatternFactory.Parse("/api/status"), 0,
            new EndpointMetadataCollection(new XrNetzFreigabe()), "fremd");
        Assert.Empty(XrPforte.Freigegeben([fremd]));
    }

    // ---------------------------------------------------------------- KI

    [Fact]
    public void KI_Einordnung_XR_ist_verboten_und_es_gibt_kein_MCP_Werkzeug()
    {
        var server = Hlks.Mcp.Werkzeugauswahl.Alle();
        foreach (var name in new[] { "xr_zugang", "xr_kopplung", "xr_kanal" })
        {
            Assert.Equal("verboten", KiEinordnung.Von(name));
            var f = Assert.Single(KiEinordnung.RechnerFunktionen, x => x.Name == name);
            Assert.True(f.Grund.Length >= 40);
            Assert.DoesNotContain(name, KiEinordnung.Erlaubt);
            Assert.DoesNotContain(name, server);
            Assert.DoesNotContain(name, AuftragDienst.ErlaubteWerkzeuge);
            Assert.DoesNotContain(name, GespraechDienst.McpWerkzeuge(new GespraechEinstellungen(), true));
        }
        Assert.DoesNotContain(server, w => w.StartsWith("xr", StringComparison.OrdinalIgnoreCase) || w.Contains("kopplung", StringComparison.OrdinalIgnoreCase));
    }
}
