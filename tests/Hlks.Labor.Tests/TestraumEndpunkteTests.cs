// XR-Labor L1: Endpunkte des Testraums im Labor-Server – derselbe Vertrag wie KICAD (XrModellEndpunkte): Liste, GLB mit ETag/304, Fehler 400/404, Katalog fehlt 409,
// neuer Katalog = neuer Schlüssel; über den Netzzugang der Labor-Brille nur mit Schlüssel (wie .XrImNetz()), komprimiert.

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using Hlks.Web;
using Hlks.Web.Tests;
using Microsoft.Extensions.DependencyInjection;

namespace Hlks.Labor.Tests;

public class TestraumEndpunkteTests
{
    /// <summary>Labor-Server mit den Katalogen dieses Ordners (Einstellungen.KatalogOrdner).</summary>
    static LaborServer Server(string katalogOrdner, bool zugang = false) => new(zugang, dienste: s =>
    {
        var alt = (Einstellungen)s.Last(d => d.ServiceType == typeof(Einstellungen)).ImplementationInstance!;
        s.AddSingleton(alt with { KatalogOrdner = katalogOrdner });
    });

    [Fact]
    public async Task Liste_und_GLB_wie_KICAD_mit_ETag_und_304()
    {
        await using var s = Server(TestraumHilfe.KatalogOrdner());
        var (code, j) = await s.Get("/api/xr/geschosse");
        Assert.Equal(HttpStatusCode.OK, code);
        var liste = (JsonObject)j!;
        Assert.Equal("labor-testraum", (string)liste["projekt"]!);
        Assert.Equal(["fassung", "projekt", "bezugspunktMm", "achsen", "geschosse", "hinweise"], liste.Select(p => p.Key).ToArray());
        var eg = liste["geschosse"]![0]!;
        var url = (string)eg["modell"]!["url"]!;
        var schluessel = (string)eg["modell"]!["schluessel"]!;
        Assert.Equal("/api/xr/geschoss/EG.glb?teil=modell", url);
        Assert.Null(eg["unterlage"]);

        var a = await s.Lokal.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, a.StatusCode);
        Assert.Equal("model/gltf-binary", a.Content.Headers.ContentType?.MediaType);
        Assert.Equal($"\"{schluessel}\"", a.Headers.ETag?.ToString());
        Assert.Equal("no-cache", a.Headers.CacheControl?.ToString());
        var glb = TestraumGlb.Lesen(await a.Content.ReadAsByteArrayAsync());
        Assert.Equal(schluessel, (string)glb.Szene["schluessel"]!);
        Assert.Equal("EG", (string)glb.Labor["geschoss"]!);
        // ohne «teil» = modell; nach Kennzeichen wie KICAD
        Assert.Equal(HttpStatusCode.OK, (await s.Lokal.GetAsync("/api/xr/geschoss/eg.glb")).StatusCode);

        foreach (var etag in new[] { $"\"{schluessel}\"", $"W/\"{schluessel}\"", "*", $"\"anders\", \"{schluessel}\"" })
        {
            using var r = new HttpRequestMessage(HttpMethod.Get, url);
            r.Headers.TryAddWithoutValidation("If-None-Match", etag);
            var b = await s.Lokal.SendAsync(r);
            Assert.Equal(HttpStatusCode.NotModified, b.StatusCode);
            Assert.Empty(await b.Content.ReadAsByteArrayAsync());
        }
        using (var r = new HttpRequestMessage(HttpMethod.Get, url))
        {
            r.Headers.TryAddWithoutValidation("If-None-Match", "\"aaaaaaaaaaaaaaaaaaaaaaaa\"");
            Assert.Equal(HttpStatusCode.OK, (await s.Lokal.SendAsync(r)).StatusCode);
        }
    }

    [Fact]
    public async Task Fehler_wie_KICAD_Teil_Geschoss_keine_Unterlage()
    {
        await using var s = Server(TestraumHilfe.KatalogOrdner());
        var (c1, j1) = await s.Get("/api/xr/geschoss/EG.glb?teil=xyz");
        Assert.Equal(HttpStatusCode.BadRequest, c1);
        Assert.Contains("xyz", (string)j1!["fehler"]!);
        var (c2, j2) = await s.Get("/api/xr/geschoss/UG.glb?teil=modell");
        Assert.Equal(HttpStatusCode.NotFound, c2);
        Assert.Contains("UG", (string)j2!["fehler"]!);
        var (c3, j3) = await s.Get("/api/xr/geschoss/EG.glb?teil=unterlage");
        Assert.Equal(HttpStatusCode.NotFound, c3);
        Assert.Contains("keine IFC-Unterlage", (string)j3!["fehler"]!);
        // was es im Labor nicht gibt (kein Projekt): Marker und Ebenen → 404, damit die App sie übergeht
        foreach (var pfad in new[] { "/api/xr/marker", "/api/xr/ebenen", "/api/xr/ebene/fliessrichtung" })
            Assert.Equal(HttpStatusCode.NotFound, (await s.Lokal.GetAsync(pfad)).StatusCode);
        // nur GET
        Assert.NotEqual(HttpStatusCode.OK, (await s.Lokal.PostAsync("/api/xr/geschosse", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"))).StatusCode);
    }

    [Fact]
    public async Task Ohne_Katalog_409_neuer_Katalog_neuer_Schluessel()
    {
        var ordner = TestraumHilfe.Kopie();
        try
        {
            await using var s = Server(ordner);
            var (_, j) = await s.Get("/api/xr/geschosse");
            var vorher = (string)j!["geschosse"]![1]!["modell"]!["schluessel"]!;
            var datei = Path.Combine(ordner, TestraumKatalog.Datei);
            var k = (JsonObject)JsonNode.Parse(File.ReadAllText(datei))!;
            k["elemente"]!.AsArray().Single(e => (string)e!["id"]! == "markierung-og-nord")!["mitteMm"] = new JsonArray(4000, 4500);
            File.WriteAllText(datei, k.ToJsonString());
            File.SetLastWriteTimeUtc(datei, DateTime.UtcNow.AddSeconds(5));   // Stand = Zeit und Grösse
            var (_, j2) = await s.Get("/api/xr/geschosse");
            Assert.NotEqual(vorher, (string)j2!["geschosse"]![1]!["modell"]!["schluessel"]!);
            Assert.Equal((string)j!["geschosse"]![0]!["modell"]!["schluessel"]!, (string)j2["geschosse"]![0]!["modell"]!["schluessel"]!);
            // ungültig → 409 mit Ort im Katalog, die App meldet die Liste als nicht gekommen
            k["ziele"]!["messen-wand-wand"]!["wandpaare"]![0]!["sollMm"] = 5999;
            File.WriteAllText(datei, k.ToJsonString());
            File.SetLastWriteTimeUtc(datei, DateTime.UtcNow.AddSeconds(10));
            var (c3, j3) = await s.Get("/api/xr/geschosse");
            Assert.Equal(HttpStatusCode.Conflict, c3);
            Assert.Contains("wandpaare[0].sollMm", (string)j3!["fehler"]!);
            Assert.Contains("labor_testraum.json", (string)j3["vorschlag"]!);
            File.Delete(datei);
            var (c4, j4) = await s.Get("/api/xr/geschoss/EG.glb");
            Assert.Equal(HttpStatusCode.Conflict, c4);
            Assert.Contains("fehlt", (string)j4!["fehler"]!);
        }
        finally { await LaborServer.Loeschen(ordner); }
    }

    [Fact]
    public void Der_Testraum_wird_nur_bei_geaendertem_Katalog_neu_gebaut()
    {
        var ordner = TestraumHilfe.Kopie();
        try
        {
            var q = new TestraumQuelle(ordner);
            var a = q.Holen();
            Assert.Same(a, q.Holen());
            Assert.Equal(1, q.Gebaut);
            File.SetLastWriteTimeUtc(Path.Combine(ordner, TestraumKatalog.Datei), DateTime.UtcNow.AddSeconds(5));
            Assert.NotSame(a, q.Holen());
            Assert.Equal(2, q.Gebaut);
        }
        finally { Directory.Delete(ordner, true); }
    }

    [Fact]
    public async Task Ueber_den_Netzzugang_der_Labor_Brille_nur_mit_Schluessel_und_komprimiert()
    {
        await using var s = Server(TestraumHilfe.KatalogOrdner(), zugang: true);
        var (port, _, fp) = await s.Aktiv();
        var paket = await s.Paket("QUEST3-TESTRAUM");
        using (var ohne = XrServer.Netz(port, fp))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await ohne.GetAsync("/api/xr/geschosse")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await ohne.GetAsync("/api/xr/geschoss/EG.glb?teil=modell")).StatusCode);
        }
        using var mit = XrServer.Netz(port, fp, XrServer.Token(paket));
        var liste = (JsonObject)JsonNode.Parse(await mit.GetStringAsync("/api/xr/geschosse"))!;
        Assert.Equal(2, liste["geschosse"]!.AsArray().Count);
        using var r = new HttpRequestMessage(HttpMethod.Get, "/api/xr/geschoss/OG.glb?teil=modell");
        r.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
        var a = await mit.SendAsync(r);
        Assert.Equal(HttpStatusCode.OK, a.StatusCode);
        Assert.Contains("gzip", a.Content.Headers.ContentEncoding);
        await using var z = new System.IO.Compression.GZipStream(await a.Content.ReadAsStreamAsync(), System.IO.Compression.CompressionMode.Decompress);
        using var ms = new MemoryStream();
        await z.CopyToAsync(ms);
        Assert.Equal("OG", (string)TestraumGlb.Lesen(ms.ToArray()).Labor["geschoss"]!);
        // die Seite des Labors (localhost) gibt es im Netzzugang nicht
        Assert.Equal(HttpStatusCode.NotFound, (await mit.GetAsync("/api/status")).StatusCode);
    }
}
