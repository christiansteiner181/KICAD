using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;

namespace Hlks.Web.Tests;

/// <summary>
/// Modell als GLB je Geschoss (P2, XrModellEndpunkte.cs) über den Zugang im Büronetz (P3-Nachtrag 23.10.2026): mit Schlüssel 200 und dieselben Bytes wie auf
/// localhost, ohne Schlüssel 401, ETag/304 auch dort; Komprimierung mit gzip und Brotli (nur GLB) und unverändert entpackt; localhost-Server gzip.
/// Beispielprojekt «MFH Musterweg». Der Netzzugang bindet nur 127.0.0.1.
/// </summary>
public class XrNetzModellTests
{
    static async Task<(XrServer S, int Port, string Fp, JsonObject Paket, string GlbUrl, byte[] Roh)> Bereit()
    {
        var s = new XrServer();
        var (port, _, fp) = await s.Aktiv();
        await s.ProjektAnlegen();
        var paket = await s.Paket();
        var liste = JsonNode.Parse(await s.Lokal.GetStringAsync("/api/xr/geschosse"))!;
        var url = (string)liste["geschosse"]![0]!["modell"]!["url"]!;
        var roh = await s.Lokal.GetByteArrayAsync(url);   // localhost, ohne Komprimierung
        Assert.Equal("glTF"u8.ToArray(), roh[..4]);
        return (s, port, fp, paket, url, roh);
    }

    static async Task<HttpResponseMessage> Holen(HttpClient c, string url, string? kodierung = null, string? etag = null)
    {
        var r = new HttpRequestMessage(HttpMethod.Get, url);
        if (kodierung is not null) r.Headers.TryAddWithoutValidation("Accept-Encoding", kodierung);
        if (etag is not null) r.Headers.TryAddWithoutValidation("If-None-Match", etag);
        return await c.SendAsync(r);
    }

    static byte[] Entpacken(byte[] daten, string kodierung)
    {
        using var ein = new MemoryStream(daten);
        using Stream s = kodierung == "gzip" ? new GZipStream(ein, CompressionMode.Decompress) : new BrotliStream(ein, CompressionMode.Decompress);
        using var aus = new MemoryStream();
        s.CopyTo(aus);
        return aus.ToArray();
    }

    [Fact]
    public async Task Geschosse_und_GLB_ueber_den_Netzzugang_mit_Schluessel_ohne_Schluessel_401_ETag_und_304()
    {
        var (s, port, fp, paket, url, roh) = await Bereit();
        await using var _s = s;
        using var mit = XrServer.Netz(port, fp, XrServer.Token(paket));
        using var ohne = XrServer.Netz(port, fp);
        using var falsch = XrServer.Netz(port, fp, $"{(string)paket["kopplung"]!}.{new string('A', 43)}");

        // Liste
        var l = await mit.GetAsync("/api/xr/geschosse");
        Assert.Equal(HttpStatusCode.OK, l.StatusCode);
        var liste = JsonNode.Parse(await l.Content.ReadAsStringAsync())!;
        Assert.Equal(["UG", "EG", "1.OG"], liste["geschosse"]!.AsArray().Select(g => (string?)g!["kennzeichen"]));
        Assert.Equal(url, (string)liste["geschosse"]![0]!["modell"]!["url"]!);   // dieselben Adressen (relativ) wie auf localhost
        Assert.Equal(HttpStatusCode.Unauthorized, (await ohne.GetAsync("/api/xr/geschosse")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await falsch.GetAsync("/api/xr/geschosse")).StatusCode);

        // GLB: dieselben Bytes wie auf localhost, Medientyp, ETag
        var g = await Holen(mit, url);
        Assert.Equal(HttpStatusCode.OK, g.StatusCode);
        Assert.Equal("model/gltf-binary", g.Content.Headers.ContentType?.MediaType);
        Assert.Empty(g.Content.Headers.ContentEncoding);   // ohne Accept-Encoding unkomprimiert
        Assert.Equal(roh, await g.Content.ReadAsByteArrayAsync());
        var etag = g.Headers.ETag?.ToString();
        Assert.NotNull(etag);
        Assert.Equal((await s.Lokal.GetAsync(url)).Headers.ETag?.ToString(), etag);

        // 304 mit dem ETag, auch über den Netzzugang
        var n = await Holen(mit, url, etag: etag);
        Assert.Equal(HttpStatusCode.NotModified, n.StatusCode);
        Assert.Empty(await n.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.OK, (await Holen(mit, url, etag: "\"anders\"")).StatusCode);

        // ohne oder mit falschem Schlüssel: 401, auch mit ETag
        Assert.Equal(HttpStatusCode.Unauthorized, (await Holen(ohne, url)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Holen(ohne, url, etag: etag)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Holen(falsch, url)).StatusCode);
        // unbekanntes Geschoss mit Schlüssel: 404 des Endpunkts
        Assert.Equal(HttpStatusCode.NotFound, (await Holen(mit, "/api/xr/geschoss/gibtsnicht.glb")).StatusCode);
    }

    [Theory]
    [InlineData("gzip")]
    [InlineData("br")]
    public async Task GLB_kommt_im_Netz_komprimiert_und_wird_unveraendert_entpackt(string kodierung)
    {
        var (s, port, fp, paket, url, roh) = await Bereit();
        await using var _s = s;
        using var mit = XrServer.Netz(port, fp, XrServer.Token(paket));
        var g = await Holen(mit, url, kodierung);
        Assert.Equal(HttpStatusCode.OK, g.StatusCode);
        Assert.Equal([kodierung], g.Content.Headers.ContentEncoding);
        Assert.Contains("Accept-Encoding", g.Headers.Vary);
        var gepackt = await g.Content.ReadAsByteArrayAsync();
        Assert.True(gepackt.Length < roh.Length, $"{kodierung}: {gepackt.Length} von {roh.Length} Byte");
        Assert.Equal(roh, Entpacken(gepackt, kodierung));
        Assert.NotNull(g.Headers.ETag);
    }

    [Fact]
    public async Task Im_Netz_wird_nur_das_GLB_komprimiert_nie_JSON()
    {
        var (s, port, fp, paket, _, _) = await Bereit();
        await using var _s = s;
        using var mit = XrServer.Netz(port, fp, XrServer.Token(paket));
        foreach (var pfad in new[] { "/api/xr/geschosse", "/api/xr/verbindung" })
        {
            var a = await Holen(mit, pfad, "gzip, br");
            Assert.Equal(HttpStatusCode.OK, a.StatusCode);
            Assert.Empty(a.Content.Headers.ContentEncoding);
            Assert.NotNull(JsonNode.Parse(await a.Content.ReadAsStringAsync()));
        }
        // Kopplungsschritte (Paket mit Schlüssel) bleiben unkomprimiert
        using var brille = XrServer.Netz(port, fp);
        var r = new HttpRequestMessage(HttpMethod.Post, "/api/xr/kopplung/anfrage") { Content = new StringContent(Hlks.Kern.Json.Text(new { brille = "QUEST-K", verpflichtung = new XrTestBrille().Verpflichtung }), System.Text.Encoding.UTF8, "application/json") };
        r.Headers.TryAddWithoutValidation("Accept-Encoding", "gzip, br");
        var k = await brille.SendAsync(r);
        Assert.Equal(HttpStatusCode.OK, k.StatusCode);
        Assert.Empty(k.Content.Headers.ContentEncoding);
    }

    [Fact]
    public async Task GLB_auf_dem_localhost_Server_ebenfalls_mit_gzip()
    {
        // Program.cs: gzip für application/octet-stream und model/gltf-binary, wenn die Anfrage nicht vom selben Rechner kommt (im TestServer gibt es keine Adresse)
        var (s, _, _, _, url, roh) = await Bereit();
        await using var _s = s;
        var g = await Holen(s.Lokal, url, "gzip");
        Assert.Equal(HttpStatusCode.OK, g.StatusCode);
        Assert.Equal(["gzip"], g.Content.Headers.ContentEncoding);
        Assert.Equal(roh, Entpacken(await g.Content.ReadAsByteArrayAsync(), "gzip"));
    }
}
