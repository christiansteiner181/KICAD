using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Hlks.Kern;
using Hlks.Kern.Tests;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Hlks.Web.Tests;

/// <summary>Quest App (Modul XR, P2): GET /api/xr/geschosse und GET /api/xr/geschoss/{id}.glb (XrModellEndpunkte.cs) mit dem Beispielprojekt
/// «MFH Musterweg» und der Testdatei n12_architektur_ifc4.ifc: Liste, GLB, ETag und 304, neuer Schlüssel nach Änderung, Fehler, Herkunftsschutz,
/// KI-Einordnung.</summary>
public class XrModellApiTests
{
    sealed class Server : IDisposable
    {
        readonly WebApplicationFactory<Program> basis = new();
        readonly WebApplicationFactory<Program> fabrik;
        public HttpClient Http { get; }
        public string Wurzel { get; } = Path.Combine(Path.GetTempPath(), "hlks_xr_" + Guid.NewGuid().ToString("N")[..10]);
        public string Ordner => Path.Combine(Wurzel, "c_Pläne und Modelle", "e_KICAD");

        public Server()
        {
            var e = new Einstellungen("Test", "testpc", Katalog.StandardOrdner(), null) { ZuletztMerken = false, BenutzerOrdner = Path.Combine(Wurzel, "benutzer") };
            fabrik = basis.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton(e)));
            Http = fabrik.CreateClient();
        }

        public async Task<(HttpStatusCode Code, JsonNode? Json)> Post(string pfad, object koerper)
        {
            var a = await Http.PostAsync(pfad, new StringContent(koerper is JsonNode n ? n.ToJsonString() : Json.Text(koerper), Encoding.UTF8, "application/json"));
            var t = await a.Content.ReadAsStringAsync();
            return (a.StatusCode, t.Length == 0 ? null : JsonNode.Parse(t));
        }

        public async Task Anlegen(bool beispiel = true)
        {
            var (code, j) = await Post("/api/projekt/anlegen", new { ordner = Ordner, name = "MFH Musterweg", beispiel });
            Assert.True(code == HttpStatusCode.OK, j?.ToJsonString());
        }

        public async Task<JsonObject> Liste()
        {
            var a = await Http.GetAsync("/api/xr/geschosse");
            var t = await a.Content.ReadAsStringAsync();
            Assert.True(a.StatusCode == HttpStatusCode.OK, t);
            return JsonNode.Parse(t)!.AsObject();
        }

        public async Task<HttpResponseMessage> Glb(string url, string? etag = null)
        {
            var r = new HttpRequestMessage(HttpMethod.Get, url);
            if (etag is not null) r.Headers.TryAddWithoutValidation("If-None-Match", etag);
            return await Http.SendAsync(r);
        }

        public void Dispose()
        {
            try { Http.PostAsync("/api/projekt/schliessen", new StringContent("{}", Encoding.UTF8, "application/json")).GetAwaiter().GetResult(); } catch { /* egal */ }
            Http.Dispose(); fabrik.Dispose(); basis.Dispose();
            for (int i = 0; i < 5; i++)
            {
                try { if (Directory.Exists(Wurzel)) Directory.Delete(Wurzel, true); return; }
                catch (IOException) { Thread.Sleep(200); }
                catch (UnauthorizedAccessException) { Thread.Sleep(200); }
            }
        }
    }

    static JsonObject Geschoss(JsonObject liste, string kz) => liste["geschosse"]!.AsArray().Single(g => (string?)g!["kennzeichen"] == kz)!.AsObject();

    [Fact]
    public async Task Geschosse_Liste_MitSchluesselnAdressenUndBezugspunkt()
    {
        using var s = new Server();
        await s.Anlegen();
        var l = await s.Liste();
        Assert.Equal(XrModell.Fassung, (int)l["fassung"]!);
        Assert.Equal([0.0, 0, 0], l["bezugspunktMm"]!.AsArray().Select(x => (double)x!));
        Assert.Contains("Meter", (string?)l["achsen"]);
        var gs = l["geschosse"]!.AsArray();
        Assert.Equal(["UG", "EG", "1.OG"], gs.Select(g => (string?)g!["kennzeichen"]));
        Assert.Equal([-2700.0, 0, 2900], gs.Select(g => (double)g!["koteMm"]!));
        foreach (var g in gs)
        {
            var m = g!["modell"]!;
            Assert.Matches("^[0-9a-f]{24}$", (string?)m["schluessel"]);
            Assert.Equal($"/api/xr/geschoss/{(string?)g["id"]}.glb?teil=modell", (string?)m["url"]);
            Assert.True((int)m["elemente"]! > 0);
            Assert.Null(g["unterlage"]);   // das Beispiel hat keine IFC-Unterlage
        }
    }

    [Fact]
    public async Task Glb_GueltigMitEtag_IfNoneMatchGibt304()
    {
        using var s = new Server();
        await s.Anlegen();
        var eg = Geschoss(await s.Liste(), "EG");
        var url = (string)eg["modell"]!["url"]!;
        var schluessel = (string)eg["modell"]!["schluessel"]!;
        using var a = await s.Glb(url);
        Assert.Equal(HttpStatusCode.OK, a.StatusCode);
        Assert.Equal("model/gltf-binary", a.Content.Headers.ContentType?.MediaType);
        Assert.Equal($"\"{schluessel}\"", a.Headers.ETag?.ToString());
        Assert.True(a.Headers.CacheControl?.NoCache);
        var daten = await a.Content.ReadAsByteArrayAsync();
        var p = GlbPruefer.Pruefen(daten);
        Assert.Equal(schluessel, (string?)p.SzeneExtras["schluessel"]);
        Assert.Equal("EG", (string?)p.SzeneExtras["geschoss"]!["kennzeichen"]);
        Assert.True(p.Meshes.Count is > 0 and < 50);

        foreach (var etag in new[] { $"\"{schluessel}\"", $"W/\"{schluessel}\"", $"\"anders\", \"{schluessel}\"", "*" })
        {
            using var b = await s.Glb(url, etag);
            Assert.Equal(HttpStatusCode.NotModified, b.StatusCode);
            Assert.Empty(await b.Content.ReadAsByteArrayAsync());
            Assert.Equal($"\"{schluessel}\"", b.Headers.ETag?.ToString());
        }
        using var c = await s.Glb(url, "\"anders\"");
        Assert.Equal(HttpStatusCode.OK, c.StatusCode);
        Assert.Equal(daten, await c.Content.ReadAsByteArrayAsync());   // gleicher Schlüssel, gleiche Datei (Zwischenspeicher)
        // ohne teil: modell; Kennzeichen statt Id (auch mit Punkt im Kennzeichen)
        using var d = await s.Glb("/api/xr/geschoss/EG.glb");
        Assert.Equal(HttpStatusCode.OK, d.StatusCode);
        Assert.Equal(daten, await d.Content.ReadAsByteArrayAsync());
        var og = Geschoss(await s.Liste(), "1.OG");
        using var e = await s.Glb("/api/xr/geschoss/1.OG.glb?teil=modell");
        Assert.Equal(HttpStatusCode.OK, e.StatusCode);
        Assert.Equal($"\"{(string)og["modell"]!["schluessel"]!}\"", e.Headers.ETag?.ToString());
    }

    [Fact]
    public async Task NachAenderung_NeuerSchluesselNurImGeschoss_AlterEtagGibtNeueDatei()
    {
        using var s = new Server();
        await s.Anlegen();
        var vorher = await s.Liste();
        var (code, r) = await s.Post("/api/befehl/element_aendern", new JsonObject { ["element"] = "E.01", ["eigenschaften"] = new JsonObject { ["lichteHoehe"] = 2400 } });
        Assert.True(code == HttpStatusCode.OK && (bool)r!["angenommen"]!, r?.ToJsonString());
        var nachher = await s.Liste();
        string K(JsonObject l, string kz) => (string)Geschoss(l, kz)["modell"]!["schluessel"]!;
        Assert.NotEqual(K(vorher, "EG"), K(nachher, "EG"));
        Assert.Equal(K(vorher, "UG"), K(nachher, "UG"));
        Assert.Equal(K(vorher, "1.OG"), K(nachher, "1.OG"));
        using var eg = await s.Glb((string)Geschoss(nachher, "EG")["modell"]!["url"]!, $"\"{K(vorher, "EG")}\"");
        Assert.Equal(HttpStatusCode.OK, eg.StatusCode);
        Assert.Equal($"\"{K(nachher, "EG")}\"", eg.Headers.ETag?.ToString());
        using var ug = await s.Glb((string)Geschoss(nachher, "UG")["modell"]!["url"]!, $"\"{K(vorher, "UG")}\"");
        Assert.Equal(HttpStatusCode.NotModified, ug.StatusCode);
    }

    [Fact]
    public async Task Fehler_UnbekanntesGeschoss404_Teil400_OhneUnterlage404_OhneProjekt409()
    {
        using var s = new Server();
        using (var a = await s.Glb("/api/xr/geschoss/EG.glb")) Assert.Equal(HttpStatusCode.Conflict, a.StatusCode);
        using (var a = await s.Http.GetAsync("/api/xr/geschosse")) Assert.Equal(HttpStatusCode.Conflict, a.StatusCode);
        await s.Anlegen();
        using (var a = await s.Glb("/api/xr/geschoss/XX.glb"))
        {
            Assert.Equal(HttpStatusCode.NotFound, a.StatusCode);
            Assert.Contains("XX", (string?)JsonNode.Parse(await a.Content.ReadAsStringAsync())!["fehler"]);
        }
        using (var a = await s.Glb("/api/xr/geschoss/EG.glb?teil=alles")) Assert.Equal(HttpStatusCode.BadRequest, a.StatusCode);
        using (var a = await s.Glb("/api/xr/geschoss/EG.glb?teil=unterlage"))
        {
            Assert.Equal(HttpStatusCode.NotFound, a.StatusCode);
            Assert.Contains("keine IFC-Unterlage", (string?)JsonNode.Parse(await a.Content.ReadAsStringAsync())!["fehler"]);
        }
    }

    [Fact]
    public async Task Unterlage_MitIfc_EigeneGlbJeGeschoss()
    {
        using var s = new Server();
        await s.Anlegen(beispiel: false);
        var (code, r) = await s.Post("/api/befehl/ifc_uebernehmen", new JsonObject { ["datei"] = AuftragServer.Testdatei("n12_architektur_ifc4.ifc") });
        Assert.True(code == HttpStatusCode.OK && (bool)r!["angenommen"]!, r?.ToJsonString());
        var l = await s.Liste();
        var mit = l["geschosse"]!.AsArray().Where(g => g!["unterlage"] is not null).ToList();
        Assert.NotEmpty(mit);
        foreach (var g in mit)
        {
            var u = g!["unterlage"]!;
            Assert.Equal($"/api/xr/geschoss/{(string?)g["id"]}.glb?teil=unterlage", (string?)u["url"]);
            using var a = await s.Glb((string)u["url"]!);
            Assert.Equal(HttpStatusCode.OK, a.StatusCode);
            Assert.Equal($"\"{(string)u["schluessel"]!}\"", a.Headers.ETag?.ToString());
            var p = GlbPruefer.Pruefen(await a.Content.ReadAsByteArrayAsync());
            Assert.Equal("unterlage", (string?)p.SzeneExtras["teil"]);
            Assert.Equal((long)u["dreiecke"]!, (long)p.SzeneExtras["dreiecke"]!);
            Assert.Equal((int)u["produkte"]!, p.Zuordnung(0).Count);
            Assert.NotEqual((string)g["modell"]!["schluessel"]!, (string)u["schluessel"]!);
        }
    }

    [Fact]
    public async Task Herkunftsschutz_FremderHost403_NurGet()
    {
        using var s = new Server();
        await s.Anlegen();
        var r = new HttpRequestMessage(HttpMethod.Get, "/api/xr/geschosse");
        r.Headers.Host = "boese.example";
        using var a = await s.Http.SendAsync(r);
        Assert.Equal(HttpStatusCode.Forbidden, a.StatusCode);
        using var b = await s.Http.PostAsync("/api/xr/geschosse", new StringContent("{}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.MethodNotAllowed, b.StatusCode);
    }

    [Fact]
    public void KiEinordnung_XrModellIstRechnerfunktion_KeinWerkzeug()
    {
        var f = Assert.Single(KiEinordnung.RechnerFunktionen, x => x.Name == "xr_modell");
        Assert.True(f.Grund.Length > 40);
        Assert.Contains("/api/xr/geschosse", f.Grund);
        Assert.Equal("verboten", KiEinordnung.Von("xr_modell"));
        Assert.DoesNotContain("xr_modell", KiEinordnung.Erlaubt);
        var server = Hlks.Mcp.Werkzeugauswahl.Alle();
        Assert.DoesNotContain(server, w => w.StartsWith("xr", StringComparison.OrdinalIgnoreCase) || w.Contains("glb", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("xr_modell", GespraechDienst.McpWerkzeuge(new GespraechEinstellungen(), false));
        Assert.DoesNotContain("xr_modell", AuftragDienst.ErlaubteWerkzeuge);
    }

    [Fact]
    public async Task GlbSpeicher_RechnetEinmal_BegrenztDieGroesse()
    {
        var sp = new XrGlbSpeicher(100);
        int n = 0;
        byte[] Erzeugen() { Interlocked.Increment(ref n); Thread.Sleep(50); return new byte[60]; }
        var a = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => sp.Holen("a", Erzeugen)));
        Assert.Equal(1, n);
        Assert.All(a, x => Assert.Same(a[0], x));
        await sp.Holen("b", Erzeugen);   // 120 Byte > 100: der älteste Eintrag fällt weg
        Assert.Equal(1, sp.Anzahl);
        await sp.Holen("a", Erzeugen);
        Assert.Equal(3, n);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sp.Holen("c", () => throw new InvalidOperationException("x")));
        Assert.Equal(1, sp.Anzahl);   // Fehler werden nicht gemerkt
    }
}
