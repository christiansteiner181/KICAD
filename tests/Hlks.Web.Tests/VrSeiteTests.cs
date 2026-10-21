using System.Net;
using System.Text.Json.Nodes;
using Hlks.Kern;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Hlks.Web.Tests;

/// <summary>
/// VR-Startseite (Modul XR, VrSeite.cs): /vr und /vr.webmanifest werden ausgeliefert, mit Ursprungsprüfung (Host-Kopf nur localhost) wie /api und /hilfe.
/// Die Seite selbst (Startpunkt, Sitzung) prüft die Browserreihe vr-start.browser.mjs.
/// </summary>
public class VrSeiteTests
{
    sealed class Server : IDisposable
    {
        readonly WebApplicationFactory<Program> basis = new();
        readonly WebApplicationFactory<Program> fabrik;
        public HttpClient Http { get; }

        public Server()
        {
            var e = new Einstellungen("Test", "testpc", Katalog.StandardOrdner(), null) { ZuletztMerken = false };
            fabrik = basis.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton(e)));
            Http = fabrik.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        }

        public Task<HttpResponseMessage> Get(string pfad, string? host = null)
        {
            var r = new HttpRequestMessage(HttpMethod.Get, pfad);
            if (host is not null) r.Headers.Host = host;
            return Http.SendAsync(r);
        }

        public void Dispose() { Http.Dispose(); fabrik.Dispose(); basis.Dispose(); }
    }

    [Fact]
    public async Task Vr_WirdAlsHtmlAusgeliefert_OhneCache()
    {
        using var s = new Server();
        var a = await s.Get("/vr");
        Assert.Equal(HttpStatusCode.OK, a.StatusCode);
        Assert.Equal("text/html", a.Content.Headers.ContentType!.MediaType);
        Assert.Contains("no-cache", a.Headers.CacheControl!.ToString());
        var html = await a.Content.ReadAsStringAsync();
        Assert.Contains("id=\"vr-eintreten\"", html);
        Assert.Contains("Eintreten", html);
        Assert.Contains("href=\"/vr.webmanifest\"", html);
        Assert.DoesNotContain("id=\"menueband\"", html);   // nicht die PC-Oberfläche
    }

    [Fact]
    public async Task VrMitSchraegstrich_FuehrtAufVrZurueck()
    {
        using var s = new Server();
        var a = await s.Get("/vr/");
        Assert.Equal(HttpStatusCode.Redirect, a.StatusCode);
        Assert.Equal("/vr", a.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Manifest_WirdAusgeliefert_UndIstGueltig()
    {
        using var s = new Server();
        var a = await s.Get("/vr.webmanifest");
        Assert.Equal(HttpStatusCode.OK, a.StatusCode);
        Assert.Equal("application/manifest+json", a.Content.Headers.ContentType!.MediaType);
        var m = JsonNode.Parse(await a.Content.ReadAsStringAsync())!;
        Assert.Equal("KICAD VR", (string?)m["name"]);
        Assert.Equal("/vr", (string?)m["start_url"]);
        Assert.Equal("standalone", (string?)m["display"]);
        var icons = m["icons"]!.AsArray();
        Assert.Equal(new[] { "192x192", "512x512" }, icons.Select(i => (string)i!["sizes"]!).OrderBy(x => x).ToArray());
        foreach (var i in icons)
        {
            var b = await s.Get((string)i!["src"]!);
            Assert.Equal(HttpStatusCode.OK, b.StatusCode);
            Assert.Equal("image/png", b.Content.Headers.ContentType!.MediaType);
            Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, (await b.Content.ReadAsByteArrayAsync()).Take(4).ToArray());
        }
    }

    [Theory]
    [InlineData("/vr")]
    [InlineData("/vr.html")]
    [InlineData("/vr.webmanifest")]
    public async Task Seiten_LokalerHost_Erlaubt(string pfad)
    {
        using var s = new Server();
        foreach (var host in new[] { "localhost", "localhost:5130", "127.0.0.1:5680", "[::1]:5130" })
            Assert.Equal(HttpStatusCode.OK, (await s.Get(pfad, host)).StatusCode);
    }

    [Theory]
    [InlineData("/vr")]
    [InlineData("/vr.html")]
    [InlineData("/vr.webmanifest")]
    [InlineData("/VR.WEBMANIFEST")]
    public async Task Seiten_FremderHost_Abgelehnt(string pfad)
    {
        using var s = new Server();
        foreach (var host in new[] { "evil.example", "evil.example:5130", "192.168.1.20:5130", "localhost.evil.example" })
        {
            var a = await s.Get(pfad, host);
            Assert.Equal(HttpStatusCode.Forbidden, a.StatusCode);
            Assert.DoesNotContain("vr-eintreten", await a.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public void Geschuetzt_ErfasstNurDieVrSeiten()
    {
        Assert.True(Herkunftsschutz.Geschuetzt("/vr"));
        Assert.True(Herkunftsschutz.Geschuetzt("/vr/"));
        Assert.True(Herkunftsschutz.Geschuetzt("/vr.html"));
        Assert.True(Herkunftsschutz.Geschuetzt("/vr.webmanifest"));
        Assert.True(Herkunftsschutz.Geschuetzt("/api/status"));
        Assert.True(Herkunftsschutz.Geschuetzt("/hilfe/"));
        Assert.False(Herkunftsschutz.Geschuetzt("/vrx"));          // keine Wortverlängerung
        Assert.False(Herkunftsschutz.Geschuetzt("/vr-symbole/vr-192.png"));
        Assert.False(Herkunftsschutz.Geschuetzt("/index.html"));
    }
}
