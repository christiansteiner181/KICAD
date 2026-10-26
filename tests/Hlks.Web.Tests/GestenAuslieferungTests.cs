using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Hlks.Web.Tests;

/// <summary>
/// Modul G (Gesten am Arbeitsplatz): Der Server liefert die lokale Kopie von MediaPipe (/lib/mediapipe/, kein CDN) mit passenden Inhaltstypen aus – das Modell
/// .task (sonst unbekannt, 404), WASM und das Modul-Bündel .mjs – und den Worker der Gesten mit «Content-Security-Policy: connect-src 'self'», damit er nur den
/// eigenen Server erreicht (MediaPipe 1.1.1 meldet sonst Nutzungsdaten an Google). Prüfsummen wie in lib/mediapipe/LIESMICH.md.
/// </summary>
public class GestenAuslieferungTests(WebApplicationFactory<Program> fabrik) : IClassFixture<WebApplicationFactory<Program>>
{
    [Theory]
    [InlineData("/lib/mediapipe/hand_landmarker.task", "application/octet-stream", 7819105L, "fbc2a30080c3c557093b5ddfc334698132eb341044ccee322ccf8bcf3607cde1")]
    [InlineData("/lib/mediapipe/wasm/vision_wasm_module_internal.wasm", "application/wasm", 13519940L, "3b57e4b55ac6346bf940212e3e11250b308a6a474077385e2f527b119cf74525")]
    [InlineData("/lib/mediapipe/vision_bundle.mjs", "text/javascript", 156139L, "a84921e02321e582484c1f8126d344b5f33b791fa09c640a52e20ee8632068f1")]
    [InlineData("/lib/mediapipe/wasm/vision_wasm_module_internal.js", "text/javascript", 334166L, "539a4386fd12d0723d6e900ec80467d168ba7290dda2f79e77a2181b8e906115")]
    public async Task MediaPipe_WirdLokalMitInhaltstypUndUnveraendertAusgeliefert(string pfad, string typ, long laenge, string sha256)
    {
        var c = fabrik.CreateClient();
        var a = await c.GetAsync(pfad);
        Assert.Equal(HttpStatusCode.OK, a.StatusCode);
        Assert.Equal(typ, a.Content.Headers.ContentType?.MediaType);
        var bytes = await a.Content.ReadAsByteArrayAsync();
        Assert.Equal(laenge, bytes.LongLength);
        Assert.Equal(sha256, Convert.ToHexStringLower(SHA256.HashData(bytes)));
        Assert.False(a.Headers.Contains("Content-Security-Policy"), "die CSP gilt nur für den Worker");
    }

    [Fact]
    public async Task Worker_HatContentSecurityPolicy_NurEigenerServer()
    {
        var c = fabrik.CreateClient();
        var a = await c.GetAsync("/js/gesten/gesten-worker.js");
        Assert.Equal(HttpStatusCode.OK, a.StatusCode);
        Assert.Contains("javascript", a.Content.Headers.ContentType?.MediaType);
        Assert.Equal("connect-src 'self'", Assert.Single(a.Headers.GetValues("Content-Security-Policy")));
        Assert.Contains("no-cache", a.Headers.CacheControl!.ToString());
        // die übrige Oberfläche bleibt ohne diese Kopfzeile
        foreach (var p in new[] { "/js/gesten/gesten.js", "/js/gesten/gesten-logik.js", "/js/app.js", "/" })
        {
            var b = await c.GetAsync(p);
            Assert.Equal(HttpStatusCode.OK, b.StatusCode);
            Assert.False(b.Headers.Contains("Content-Security-Policy"), p);
        }
    }
}
