using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Hlks.Kern;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Hlks.Web.Tests;

/// <summary>
/// REST für Unterlagen (Modul 1b, C1): POST /api/unterlagen (Hochladen in den Ordner unterlagen des offenen Projekts) und
/// GET /api/unterlagen/{id}/netz (Netzdatei einer IFC-Unterlage roh). Fachliche Fehler kommen als 400/404 mit Text, nie als 500.
/// </summary>
public class UnterlagenApiTests
{
    sealed class Server : IDisposable
    {
        readonly WebApplicationFactory<Program> basis = new();
        readonly WebApplicationFactory<Program> fabrik;
        public HttpClient Http { get; }
        public string Wurzel { get; } = Path.Combine(Path.GetTempPath(), "hlks_unterl_" + Guid.NewGuid().ToString("N")[..10]);
        public string Ordner => Path.Combine(Wurzel, "c_Pläne und Modelle", "e_KICAD");
        public string Unterlagen => Path.Combine(Ordner, "unterlagen");

        public Server()
        {
            var e = new Einstellungen("Test", "testpc", Katalog.StandardOrdner(), null) { ZuletztMerken = false };
            fabrik = basis.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton(e)));
            Http = fabrik.CreateClient();
        }

        public async Task<(HttpStatusCode Code, string Text, string? Typ)> Senden(HttpMethod m, string pfad, HttpContent? inhalt = null)
        {
            var a = await Http.SendAsync(new HttpRequestMessage(m, pfad) { Content = inhalt });
            return (a.StatusCode, await a.Content.ReadAsStringAsync(), a.Content.Headers.ContentType?.MediaType);
        }

        public async Task<(HttpStatusCode Code, JsonNode? Json)> Post(string pfad, object? koerper = null)
        {
            var text = koerper switch { null => "{}", JsonNode n => n.ToJsonString(), _ => Hlks.Kern.Json.Text(koerper) };
            var (code, antwort, _) = await Senden(HttpMethod.Post, pfad, new StringContent(text, Encoding.UTF8, "application/json"));
            return (code, antwort.Length == 0 ? null : JsonNode.Parse(antwort));
        }

        public async Task<(HttpStatusCode Code, JsonNode? Json)> Hochladen(string name, byte[] bytes)
        {
            var inhalt = new ByteArrayContent(bytes);
            inhalt.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var (code, text, _) = await Senden(HttpMethod.Post, "/api/unterlagen?name=" + Uri.EscapeDataString(name), inhalt);
            return (code, text.Length == 0 ? null : JsonNode.Parse(text));
        }

        public async Task Anlegen()
        {
            var (code, _) = await Post("/api/projekt/anlegen", new { ordner = Ordner, name = "Unterlagen-Test", beispiel = true });
            Assert.Equal(HttpStatusCode.OK, code);
        }

        public void Dispose()
        {
            try { Http.PostAsync("/api/projekt/schliessen", new StringContent("{}")).GetAwaiter().GetResult(); } catch { /* egal */ }
            Http.Dispose();
            fabrik.Dispose();
            basis.Dispose();
            for (int i = 0; i < 5; i++)
            {
                try { if (Directory.Exists(Wurzel)) Directory.Delete(Wurzel, true); return; }
                catch (IOException) { Thread.Sleep(200); }
                catch (UnauthorizedAccessException) { Thread.Sleep(200); }
            }
        }
    }

    static string Z(JsonNode? n) => n?.GetValue<string>() ?? "";

    static string Testdatei(string name)
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            foreach (var p in new[] { Path.Combine(d.FullName, "tests", "daten", name), Path.Combine(d.FullName, "HLKS_CAD", "tests", "daten", name) })
                if (File.Exists(p)) return p;
        var bekannt = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive", "Claude_Code", "HLKS_CAD", "tests", "daten", name);
        if (File.Exists(bekannt)) return bekannt;
        throw new FileNotFoundException("Testdatei fehlt: " + name);
    }

    // ------------------------------------------------------------------ Hochladen

    [Fact]
    public async Task Hochladen_RohLegtDieDateiMitPruefsummeImUnterlagenordnerAb()
    {
        using var s = new Server();
        await s.Anlegen();
        var inhalt = Encoding.UTF8.GetBytes("ISO-10303-21;\nHEADER;\nENDSEC;\n");
        var (code, j) = await s.Hochladen("Architekt äö.ifc", inhalt);
        Assert.Equal(HttpStatusCode.OK, code);
        var name = Z(j!["name"]);
        var stand = Z(j["stand"]);
        Assert.Matches("^[0-9a-f]{12}$", stand);
        Assert.Equal($"{stand}_Architekt äö.ifc", name);
        Assert.Equal(inhalt.Length, j["bytes"]!.GetValue<long>());
        var abgelegt = Path.Combine(s.Unterlagen, name);
        Assert.True(File.Exists(abgelegt), "Datei liegt im Ordner unterlagen des Projekts");
        Assert.Equal(inhalt, File.ReadAllBytes(abgelegt));
    }

    [Fact]
    public async Task Hochladen_GleicherInhaltGibtDenselbenNamen_AndererInhaltEinenAnderen()
    {
        using var s = new Server();
        await s.Anlegen();
        var (_, a) = await s.Hochladen("plan.pdf", Encoding.UTF8.GetBytes("eins"));
        var (_, b) = await s.Hochladen("plan.pdf", Encoding.UTF8.GetBytes("eins"));
        var (_, c) = await s.Hochladen("plan.pdf", Encoding.UTF8.GetBytes("zwei"));
        Assert.Equal(Z(a!["name"]), Z(b!["name"]));
        Assert.NotEqual(Z(a["name"]), Z(c!["name"]));
        Assert.Equal(2, Directory.GetFiles(s.Unterlagen, "*plan.pdf").Length);   // nie überschrieben, nichts doppelt
    }

    [Fact]
    public async Task Hochladen_Multipart()
    {
        using var s = new Server();
        await s.Anlegen();
        var form = new MultipartFormDataContent();
        var datei = new ByteArrayContent(Encoding.UTF8.GetBytes("Bildinhalt"));
        datei.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(datei, "datei", "grundriss.png");
        var (code, text, _) = await s.Senden(HttpMethod.Post, "/api/unterlagen", form);
        Assert.Equal(HttpStatusCode.OK, code);
        var j = JsonNode.Parse(text)!;
        Assert.EndsWith("_grundriss.png", Z(j["name"]));
        Assert.True(File.Exists(Path.Combine(s.Unterlagen, Z(j["name"]))));
    }

    [Fact]
    public async Task Hochladen_PfadImNamenWirdAbgeschnitten_DieDateiBleibtImUnterlagenordner()
    {
        using var s = new Server();
        await s.Anlegen();
        var (code, j) = await s.Hochladen(@"..\..\ausserhalb.ifc", Encoding.UTF8.GetBytes("x"));
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.EndsWith("_ausserhalb.ifc", Z(j!["name"]));
        Assert.DoesNotContain("..", Z(j["name"]));
        Assert.False(File.Exists(Path.Combine(s.Ordner, "ausserhalb.ifc")));
        Assert.Single(Directory.GetFiles(s.Unterlagen, "*ausserhalb.ifc"));
    }

    [Fact]
    public async Task Hochladen_OhneProjektGibtFachlicheMeldung_NichtFuenfhundert()
    {
        using var s = new Server();
        var (code, j) = await s.Hochladen("a.ifc", Encoding.UTF8.GetBytes("x"));
        Assert.Equal(HttpStatusCode.Conflict, code);
        Assert.Contains("kein Projekt", Z(j!["fehler"]));
        Assert.False(string.IsNullOrEmpty(Z(j["vorschlag"])));
    }

    [Fact]
    public async Task Hochladen_OhneNamenOderLeer_GibtVierhundertMitText()
    {
        using var s = new Server();
        await s.Anlegen();
        var (code, text, _) = await s.Senden(HttpMethod.Post, "/api/unterlagen", new ByteArrayContent(Encoding.UTF8.GetBytes("x")));
        Assert.Equal(HttpStatusCode.BadRequest, code);
        Assert.Contains("Dateiname", JsonNode.Parse(text)!["fehler"]!.GetValue<string>());

        var (code2, j2) = await s.Hochladen("leer.ifc", []);
        Assert.Equal(HttpStatusCode.BadRequest, code2);
        Assert.Contains("leer", Z(j2!["fehler"]));
        Assert.Empty(Directory.GetFiles(s.Unterlagen, "*leer.ifc"));
    }

    [Fact]
    public async Task Hochladen_GrosseDatei_ueberDemKestrelStandardWirdAngenommen()
    {
        using var s = new Server();
        await s.Anlegen();
        var gross = new byte[33 * 1024 * 1024];   // über 30 MB (Kestrel-Vorgabe)
        new Random(7).NextBytes(gross);
        var (code, j) = await s.Hochladen("gross.dwg", gross);
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Equal(gross.Length, new FileInfo(Path.Combine(s.Unterlagen, Z(j!["name"]))).Length);
    }

    [Fact]
    public async Task Hochladen_DanachLiestIfcPruefenDieDatei_UeberDenNamenDerAblage()
    {
        using var s = new Server();
        await s.Anlegen();
        var (code, j) = await s.Hochladen("minimal_ifc4.ifc", File.ReadAllBytes(Testdatei("minimal_ifc4.ifc")));
        Assert.Equal(HttpStatusCode.OK, code);
        var (c2, p) = await s.Post("/api/abfrage/ifc_pruefen", new { datei = Z(j!["name"]) });
        Assert.Equal(HttpStatusCode.OK, c2);
        Assert.Equal("IFC4", Z(p!["schema"]).ToUpperInvariant());
        Assert.Equal(2, p["geschosse"]!.AsArray().Count);
        Assert.NotNull(p["lage"]);
        Assert.NotNull(p["lage"]!["bewertung"]);
    }

    // ------------------------------------------------------------------ Oberfläche

    [Fact]
    public async Task Oberflaeche_DieDateienDesRegistersEinfuegenWerdenGeliefert()
    {
        using var s = new Server();
        foreach (var url in new[] { "/js/oberflaeche/aktionen-einfuegen.js", "/js/oberflaeche/einfuegen-text.js", "/js/werkzeuge/kalibrieren.js", "/js/werkzeuge/kalibrierung.js" })
        {
            var (code, text, typ) = await s.Senden(HttpMethod.Get, url);
            Assert.Equal(HttpStatusCode.OK, code);
            Assert.Contains("javascript", typ);
            Assert.False(string.IsNullOrWhiteSpace(text), url);
        }
        // eingebunden: aktionen.js importiert aktionen-einfuegen.js, das Menüband kennt das Register
        Assert.Contains("aktionen-einfuegen.js", await s.Http.GetStringAsync("/js/oberflaeche/aktionen.js"));
        Assert.Contains("['einfuegen', 'Einfügen']", await s.Http.GetStringAsync("/js/oberflaeche/menueband.js"));
    }

    // ------------------------------------------------------------------ Netz

    [Fact]
    public async Task Netz_UnbekannteUnterlageUndOhneNetz_GebenVierhundertvierMitText()
    {
        using var s = new Server();
        await s.Anlegen();
        var (c1, t1, typ1) = await s.Senden(HttpMethod.Get, "/api/unterlagen/gibt-es-nicht/netz");
        Assert.Equal(HttpStatusCode.NotFound, c1);
        Assert.Equal("text/plain", typ1);
        Assert.Contains("gibt es nicht", t1);

        // ein Element, das keine Unterlage ist, zählt nicht
        var (c2, t2, _) = await s.Senden(HttpMethod.Get, "/api/unterlagen/WC-E.02/netz");
        Assert.Equal(HttpStatusCode.NotFound, c2);
        Assert.Contains("gibt es nicht", t2);

        var (code, j) = await s.Hochladen("plan.dxf", Encoding.UTF8.GetBytes("0\nSECTION\n"));
        var (cu, ju) = await s.Post("/api/befehl/element_anlegen", new
        {
            klasse = "Unterlage", kennzeichen = "UL-T1",
            eigenschaften = new { datei = Z(j!["name"]), art = "dxf" },
            geometrie = new { art = "polygon", punkte = new[] { new[] { 0, 0, 0 }, new[] { 1000, 0, 0 }, new[] { 1000, 1000, 0 }, new[] { 0, 1000, 0 } } },
        });
        Assert.Equal(HttpStatusCode.OK, cu);
        Assert.True(ju!["angenommen"]!.GetValue<bool>(), Z(ju["fehler"]));
        var (c3, t3, _) = await s.Senden(HttpMethod.Get, "/api/unterlagen/UL-T1/netz");
        Assert.Equal(HttpStatusCode.NotFound, c3);
        Assert.Contains("kein Netz", t3);
    }

    [Fact]
    public async Task Netz_LiefertDieDateiRoh_NachKennzeichenUndId()
    {
        using var s = new Server();
        await s.Anlegen();
        var netz = new byte[] { 0x48, 0x4C, 0x4B, 0x4E, 0, 1, 2, 3, 255, 254, 253 };   // beliebige Binärdaten
        var (_, j) = await s.Hochladen("modell.netz", netz);
        var name = Z(j!["name"]);
        var (cu, ju) = await s.Post("/api/befehl/element_anlegen", new
        {
            klasse = "Unterlage", kennzeichen = "UL-T2",
            eigenschaften = new { datei = name, art = "ifc", netz = name },
            geometrie = new { art = "polygon", punkte = new[] { new[] { 0, 0, 0 }, new[] { 1000, 0, 0 }, new[] { 1000, 1000, 0 }, new[] { 0, 1000, 0 } } },
        });
        Assert.Equal(HttpStatusCode.OK, cu);
        Assert.True(ju!["angenommen"]!.GetValue<bool>(), Z(ju["fehler"]));

        var a = await s.Http.GetAsync("/api/unterlagen/UL-T2/netz");
        Assert.Equal(HttpStatusCode.OK, a.StatusCode);
        Assert.Equal("application/octet-stream", a.Content.Headers.ContentType?.MediaType);
        Assert.Equal(netz, await a.Content.ReadAsByteArrayAsync());

        // über die Element-Id
        var modell = JsonNode.Parse(await s.Http.GetStringAsync("/api/modell"))!;
        var id = modell["elemente"]!.AsArray().First(e => Z(e!["kennzeichen"]) == "UL-T2")!["id"]!.GetValue<string>();
        var b = await s.Http.GetAsync($"/api/unterlagen/{id}/netz");
        Assert.Equal(HttpStatusCode.OK, b.StatusCode);
        Assert.Equal(netz, await b.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Netz_FehlendeDateiImOrdnerGibtVierhundertvierMitText()
    {
        using var s = new Server();
        await s.Anlegen();
        var (cu, ju) = await s.Post("/api/befehl/element_anlegen", new
        {
            klasse = "Unterlage", kennzeichen = "UL-T3",
            eigenschaften = new { datei = "x_a.ifc", art = "ifc", netz = "x_a.ifc.netz-123.bin" },
            geometrie = new { art = "polygon", punkte = new[] { new[] { 0, 0, 0 }, new[] { 1000, 0, 0 }, new[] { 1000, 1000, 0 }, new[] { 0, 1000, 0 } } },
        });
        Assert.True(ju!["angenommen"]!.GetValue<bool>(), Z(ju["fehler"]));
        var (code, text, _) = await s.Senden(HttpMethod.Get, "/api/unterlagen/UL-T3/netz");
        Assert.Equal(HttpStatusCode.NotFound, code);
        Assert.Contains("fehlt", text);
    }

    [Fact]
    public async Task Netz_OhneProjektGibtFachlicheMeldung()
    {
        using var s = new Server();
        var (code, text, _) = await s.Senden(HttpMethod.Get, "/api/unterlagen/UL-1/netz");
        Assert.True((int)code is >= 400 and < 500, $"Status {(int)code}");
        Assert.Contains("kein Projekt", text);
    }

    // ------------------------------------------------------------------ Netz: gzip

    static byte[] ZufallsNetz(int bytes)
    {
        // gut komprimierbar wie eine echte Netzdatei (viele gleiche Float-Muster), mit Kopf «HLKSNETZ»
        var b = new byte[bytes];
        Encoding.ASCII.GetBytes("HLKSNETZ").CopyTo(b, 0);
        for (int i = 8; i < b.Length; i++) b[i] = (byte)((i / 12) % 7 * 3);
        return b;
    }

    async Task<string> NetzUnterlageAnlegen(Server s, string kennzeichen, byte[] netz)
    {
        var (_, j) = await s.Hochladen("modell.netz", netz);
        var name = Z(j!["name"]);
        var (_, ju) = await s.Post("/api/befehl/element_anlegen", new
        {
            klasse = "Unterlage", kennzeichen,
            eigenschaften = new { datei = name, art = "ifc", netz = name },
            geometrie = new { art = "polygon", punkte = new[] { new[] { 0, 0, 0 }, new[] { 1000, 0, 0 }, new[] { 1000, 1000, 0 }, new[] { 0, 1000, 0 } } },
        });
        Assert.True(ju!["angenommen"]!.GetValue<bool>(), Z(ju["fehler"]));
        return name;
    }

    [Fact]
    public async Task Netz_MitAcceptEncodingGzipKommtKomprimiert_OhneRoh()
    {
        using var s = new Server();
        await s.Anlegen();
        var netz = ZufallsNetz(2 * 1024 * 1024);
        await NetzUnterlageAnlegen(s, "UL-G1", netz);

        // Browser: nimmt gzip an → komprimiert, entpackt gleich dem Original, deutlich kleiner
        var anfrage = new HttpRequestMessage(HttpMethod.Get, "/api/unterlagen/UL-G1/netz");
        anfrage.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
        var a = await s.Http.SendAsync(anfrage);
        Assert.Equal(HttpStatusCode.OK, a.StatusCode);
        Assert.Contains("gzip", a.Content.Headers.ContentEncoding);
        var gz = await a.Content.ReadAsByteArrayAsync();
        Assert.True(gz.Length < netz.Length / 2, $"komprimiert {gz.Length} von {netz.Length} Byte");
        using (var entpackt = new MemoryStream())
        {
            using (var z = new GZipStream(new MemoryStream(gz), CompressionMode.Decompress)) await z.CopyToAsync(entpackt);
            Assert.Equal(netz, entpackt.ToArray());
        }

        // ohne Accept-Encoding: roh, keine Kodierung
        var b = await s.Http.GetAsync("/api/unterlagen/UL-G1/netz");
        Assert.Empty(b.Content.Headers.ContentEncoding);
        Assert.Equal(netz, await b.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Antwortkomprimierung_GiltNurFuerBinaerdateien_NichtFuerJsonUndOberflaeche()
    {
        using var s = new Server();
        await s.Anlegen();
        foreach (var url in new[] { "/api/status", "/api/modell", "/js/ansichten/plangrafik.js", "/index.html" })
        {
            var anfrage = new HttpRequestMessage(HttpMethod.Get, url);
            anfrage.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
            var a = await s.Http.SendAsync(anfrage);
            Assert.Equal(HttpStatusCode.OK, a.StatusCode);
            Assert.Empty(a.Content.Headers.ContentEncoding);
        }
    }

    // ------------------------------------------------------------------ Bild

    /// <summary>Legt ein einfarbiges PNG hoch und fügt es als Unterlage ins Geschoss ein; liefert Kennzeichen, Id und die Bytes der Datei.</summary>
    static async Task<(string Kennzeichen, string Id, byte[] Png)> BildEinfuegen(Server s, int breite = 40, int hoehe = 20, double dpi = 25.4)
    {
        var bgra = new byte[breite * hoehe * 4];
        for (int i = 0; i < breite * hoehe; i++) { bgra[4 * i] = 40; bgra[4 * i + 1] = (byte)(90 + i % breite); bgra[4 * i + 2] = 200; bgra[4 * i + 3] = 255; }
        var tmp = Path.Combine(Path.GetTempPath(), "hlks_png_" + Guid.NewGuid().ToString("N")[..8] + ".png");
        try { PngSchreiber.Schreiben(tmp, breite, hoehe, bgra, dpi); var png = File.ReadAllBytes(tmp);
            var (code, j) = await s.Hochladen("grundriss.png", png);
            Assert.Equal(HttpStatusCode.OK, code);
            var (c2, r) = await s.Post("/api/befehl/unterlage_einfuegen", new { datei = Z(j!["name"]), geschoss = "EG" });
            Assert.Equal(HttpStatusCode.OK, c2);
            Assert.True(r!["angenommen"]!.GetValue<bool>(), Z(r["fehler"]) + " " + Z(r["vorschlag"]));
            var modell = JsonNode.Parse(await s.Http.GetStringAsync("/api/modell"))!;
            var ul = modell["elemente"]!.AsArray().Single(e => Z(e!["klasse"]) == "Unterlage")!;
            return (Z(ul["kennzeichen"]), Z(ul["id"]), png);
        }
        finally { File.Delete(tmp); }
    }

    [Fact]
    public async Task Bild_PngUnterlage_LiefertDieDateiMitMedientyp_NachKennzeichenUndId_Und304()
    {
        using var s = new Server();
        await s.Anlegen();
        var (kz, id, png) = await BildEinfuegen(s);

        foreach (var schluessel in new[] { kz, id })
        {
            var a = await s.Http.GetAsync($"/api/unterlagen/{schluessel}/bild");
            Assert.Equal(HttpStatusCode.OK, a.StatusCode);
            Assert.Equal("image/png", a.Content.Headers.ContentType?.MediaType);
            Assert.Equal(png, await a.Content.ReadAsByteArrayAsync());
            Assert.Contains("no-cache", a.Headers.CacheControl?.ToString());
        }

        // unveränderte Datei: Nachfragen mit Änderungsdatum → 304 ohne Inhalt
        var erste = await s.Http.GetAsync($"/api/unterlagen/{kz}/bild");
        var anfrage = new HttpRequestMessage(HttpMethod.Get, $"/api/unterlagen/{kz}/bild") { Headers = { IfModifiedSince = erste.Content.Headers.LastModified } };
        var nach = await s.Http.SendAsync(anfrage);
        Assert.Equal(HttpStatusCode.NotModified, nach.StatusCode);
    }

    [Fact]
    public async Task Bild_FehlerfaelleGebenVierhundertvierMitText()
    {
        using var s = new Server();
        var (c0, t0, _) = await s.Senden(HttpMethod.Get, "/api/unterlagen/UL-1/bild");
        Assert.True((int)c0 is >= 400 and < 500, $"Status {(int)c0}");   // ohne Projekt
        Assert.Contains("kein Projekt", t0);

        await s.Anlegen();
        var (c1, t1, typ1) = await s.Senden(HttpMethod.Get, "/api/unterlagen/gibt-es-nicht/bild");
        Assert.Equal(HttpStatusCode.NotFound, c1);
        Assert.Equal("text/plain", typ1);
        Assert.Contains("gibt es nicht", t1);

        var (c2, t2, _) = await s.Senden(HttpMethod.Get, "/api/unterlagen/WC-E.02/bild");   // kein Unterlage-Element
        Assert.Equal(HttpStatusCode.NotFound, c2);
        Assert.Contains("gibt es nicht", t2);

        // DXF-Unterlage hat kein Bild
        var (_, j) = await s.Hochladen("plan.dxf", Encoding.UTF8.GetBytes("0\nSECTION\n"));
        var (_, ju) = await s.Post("/api/befehl/element_anlegen", new
        {
            klasse = "Unterlage", kennzeichen = "UL-D1",
            eigenschaften = new { datei = Z(j!["name"]), art = "dxf" },
            geometrie = new { art = "polygon", punkte = new[] { new[] { 0, 0, 0 }, new[] { 1000, 0, 0 }, new[] { 1000, 1000, 0 }, new[] { 0, 1000, 0 } } },
        });
        Assert.True(ju!["angenommen"]!.GetValue<bool>(), Z(ju["fehler"]));
        var (c3, t3, _) = await s.Senden(HttpMethod.Get, "/api/unterlagen/UL-D1/bild");
        Assert.Equal(HttpStatusCode.NotFound, c3);
        Assert.Contains("kein Bild", t3);

        // Bild-Unterlage, deren Datei fehlt
        var (_, jb) = await s.Post("/api/befehl/element_anlegen", new
        {
            klasse = "Unterlage", kennzeichen = "UL-B1",
            eigenschaften = new { datei = "weg_bild.png", art = "bild" },
            geometrie = new { art = "polygon", punkte = new[] { new[] { 0, 0, 0 }, new[] { 1000, 0, 0 }, new[] { 1000, 1000, 0 }, new[] { 0, 1000, 0 } } },
        });
        Assert.True(jb!["angenommen"]!.GetValue<bool>(), Z(jb["fehler"]));
        var (c4, t4, typ4) = await s.Senden(HttpMethod.Get, "/api/unterlagen/UL-B1/bild");
        Assert.Equal(HttpStatusCode.NotFound, c4);
        Assert.Equal("text/plain", typ4);
        Assert.False(string.IsNullOrWhiteSpace(t4));
    }

    [Fact]
    public async Task Bild_PlangrafikFuehrtDasBildPrimitivMitQuelle_AusgeblendetLiefertNichts()
    {
        using var s = new Server();
        await s.Anlegen();
        var (kz, id, _) = await BildEinfuegen(s);

        JsonNode? Bild(JsonNode plan) => plan["teile"]!.AsArray()
            .Where(t => Z(t!["ebene"]) == "grundlage")
            .SelectMany(t => t!["primitive"]!.AsArray())
            .FirstOrDefault(p => Z(p!["art"]) == "bild");

        var plan = JsonNode.Parse(await s.Http.GetStringAsync("/api/plangrafik/AN-GR-EG"))!;
        var bild = Bild(plan);
        Assert.NotNull(bild);
        Assert.Equal(id, Z(bild!["quelle"]));
        Assert.Equal(4, bild["punkte"]!.AsArray().Count);
        Assert.Equal(0.5, bild["stil"]!["deckkraft"]!.GetValue<double>(), 3);

        // ausgeblendet (anzeigen = false): der Kern liefert nichts – weder im Grundriss noch das Bild
        var (_, r) = await s.Post("/api/befehl/element_aendern", new { element = kz, eigenschaften = new { anzeigen = false } });
        Assert.True(r!["angenommen"]!.GetValue<bool>(), Z(r["fehler"]));
        var plan2 = JsonNode.Parse(await s.Http.GetStringAsync("/api/plangrafik/AN-GR-EG"))!;
        Assert.Null(Bild(plan2));
        Assert.DoesNotContain(plan2["teile"]!.AsArray(), t => Z(t!["ebene"]) == "grundlage");

        // wieder eingeblendet: da
        await s.Post("/api/befehl/element_aendern", new { element = kz, eigenschaften = new { anzeigen = true } });
        Assert.NotNull(Bild(JsonNode.Parse(await s.Http.GetStringAsync("/api/plangrafik/AN-GR-EG"))!));
    }

    [Fact]
    public async Task Oberflaeche_PlangrafikUndNetzleserWerdenGeliefert()
    {
        using var s = new Server();
        foreach (var url in new[] { "/js/ansichten/plangrafik.js", "/js/ansichten/netzdatei.js", "/js/ansichten/koerper3d.js" })
        {
            var (code, text, typ) = await s.Senden(HttpMethod.Get, url);
            Assert.Equal(HttpStatusCode.OK, code);
            Assert.Contains("javascript", typ);
            Assert.False(string.IsNullOrWhiteSpace(text), url);
        }
        Assert.Contains("netzdatei.js", await s.Http.GetStringAsync("/js/ansichten/ansicht3d.js"));
    }
}
