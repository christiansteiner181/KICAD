using System.Diagnostics;
using System.Net;
using System.Text;

namespace Hlks.Web.Tests;

/// <summary>
/// Auslieferung der Bedienungsanleitung unter /hilfe/ (AnleitungEndpunkte.cs, Entscheid Christian Steiner 19.10.2026): Datei, Bild, Weiterleitung,
/// fehlender Ordner, Pfad-Ausbruch (.., %2e%2e, absolute Pfade, Laufwerke, Datenströme, Junctions), nur GET, nur bekannte Dateitypen.
/// Jeder Test hat seinen Ordner unter %TEMP%; ein «Geheimnis» ausserhalb des Anleitungsordners darf nie ausgeliefert werden.
/// </summary>
[Collection("AnleitungUmgebung")]
public class AnleitungAuslieferungTests
{
    const string Geheimnis = "GEHEIMER-INHALT-AUSSERHALB";
    static readonly byte[] BildBytes = [0x52, 0x49, 0x46, 0x46, 1, 2, 3, 4, 0x57, 0x45, 0x42, 0x50];

    static AnleitungUmgebung MitSeite()
    {
        var u = new AnleitungUmgebung().Seite();
        File.WriteAllBytes(Path.Combine(u.Anleitung, "bilder", "funktion-1.webp"), BildBytes);
        File.WriteAllBytes(Path.Combine(u.Anleitung, "bilder", "foto.png"), [0x89, 0x50, 0x4E, 0x47]);
        File.WriteAllText(Path.Combine(u.Anleitung, "stand.txt"), "abcd1234\n");
        // Geheimnisse neben und über dem Anleitungsordner
        File.WriteAllText(Path.Combine(u.Wurzel, "geheim.html"), Geheimnis);
        File.WriteAllText(Path.Combine(u.Wurzel, "geheim.json"), Geheimnis);
        File.WriteAllText(Path.Combine(u.Wurzel, "geheim.txt"), Geheimnis);
        return u.Starten();
    }

    // ---------------------------------------------------------------- Auslieferung

    [Fact]
    public async Task Index_WirdAlsHtmlAusgeliefert_OhneCache()
    {
        using var u = MitSeite();
        foreach (var url in new[] { "/hilfe/", "/hilfe/index.html" })
        {
            var a = await u.Http.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, a.StatusCode);
            Assert.Equal("text/html", a.Content.Headers.ContentType!.MediaType);
            Assert.Equal("utf-8", a.Content.Headers.ContentType.CharSet);
            Assert.Contains("no-cache", a.Headers.CacheControl!.ToString());
            Assert.Contains("Hallo Anleitung", await a.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task Index_ZeigtDenNeuenStandOhneNeustart()
    {
        using var u = MitSeite();
        Assert.Contains("Hallo Anleitung", await u.Http.GetStringAsync("/hilfe/"));
        File.WriteAllText(Path.Combine(u.Anleitung, "index.html"), "<p>Zweiter Bau</p>");
        Assert.Contains("Zweiter Bau", await u.Http.GetStringAsync("/hilfe/"));
    }

    [Fact]
    public async Task Bild_WirdMitInhaltstypAusgeliefert()
    {
        using var u = MitSeite();
        var a = await u.Http.GetAsync("/hilfe/bilder/funktion-1.webp");
        Assert.Equal(HttpStatusCode.OK, a.StatusCode);
        Assert.Equal("image/webp", a.Content.Headers.ContentType!.MediaType);
        Assert.Equal(BildBytes, await a.Content.ReadAsByteArrayAsync());
        Assert.Contains("no-cache", a.Headers.CacheControl!.ToString());
        Assert.Equal("image/png", (await u.Http.GetAsync("/hilfe/bilder/foto.png")).Content.Headers.ContentType!.MediaType);
    }

    [Theory]
    [InlineData("a.html", "text/html")]
    [InlineData("a.webp", "image/webp")]
    [InlineData("a.png", "image/png")]
    [InlineData("a.jpg", "image/jpeg")]
    [InlineData("a.JPG", "image/jpeg")]
    [InlineData("a.svg", "image/svg+xml")]
    [InlineData("a.css", "text/css")]
    [InlineData("a.js", "text/javascript")]
    [InlineData("a.json", "application/json")]
    public async Task Inhaltstypen_nachEndung(string name, string typ)
    {
        using var u = MitSeite();
        File.WriteAllText(Path.Combine(u.Anleitung, name), "x");
        var a = await u.Http.GetAsync("/hilfe/" + name);
        Assert.Equal(HttpStatusCode.OK, a.StatusCode);
        Assert.Equal(typ, a.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Bedingte_Anfrage_mit_ETag_gibt_304()
    {
        using var u = MitSeite();
        var a = await u.Http.GetAsync("/hilfe/bilder/funktion-1.webp");
        var etag = a.Headers.ETag;
        Assert.NotNull(etag);
        var b = new HttpRequestMessage(HttpMethod.Get, "/hilfe/bilder/funktion-1.webp");
        b.Headers.IfNoneMatch.Add(etag);
        Assert.Equal(HttpStatusCode.NotModified, (await u.Http.SendAsync(b)).StatusCode);
    }

    [Fact]
    public async Task Hilfe_ohne_Schrägstrich_leitet_auf_hilfe_weiter()
    {
        using var u = MitSeite();
        var a = await u.Http.GetAsync("/hilfe");
        Assert.Equal(HttpStatusCode.Redirect, a.StatusCode);
        Assert.Equal("/hilfe/", a.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Nur_GET()
    {
        using var u = MitSeite();
        foreach (var m in new[] { HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete, HttpMethod.Patch })
        {
            var a = await u.Http.SendAsync(new HttpRequestMessage(m, "/hilfe/index.html") { Content = new StringContent("x") });
            Assert.True(a.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotFound or HttpStatusCode.UnsupportedMediaType, $"{m}: {a.StatusCode}");   // 415: Herkunftsschutz (text/plain)
        }
        Assert.Contains("Hallo Anleitung", File.ReadAllText(Path.Combine(u.Anleitung, "index.html")));   // unverändert
    }

    [Fact]
    public async Task Fehlende_Datei_ist_404_Text()
    {
        using var u = MitSeite();
        var a = await u.Http.GetAsync("/hilfe/bilder/gibt-es-nicht.webp");
        Assert.Equal(HttpStatusCode.NotFound, a.StatusCode);
        Assert.Equal("text/plain", a.Content.Headers.ContentType!.MediaType);
        Assert.Equal(HttpStatusCode.NotFound, (await u.Http.GetAsync("/hilfe/bilder/")).StatusCode);   // ein Ordner ist keine Datei
        Assert.Equal(HttpStatusCode.NotFound, (await u.Http.GetAsync("/hilfe/bilder")).StatusCode);
    }

    [Theory]
    [InlineData("/hilfe/stand.txt")]
    [InlineData("/hilfe/index.html.tmp")]
    [InlineData("/hilfe/bilder/x.exe")]
    [InlineData("/hilfe/bilder/x")]
    public async Task Andere_Dateitypen_werden_nicht_ausgeliefert(string url)
    {
        using var u = MitSeite();
        File.WriteAllText(Path.Combine(u.Anleitung, "index.html.tmp"), "x");
        File.WriteAllText(Path.Combine(u.Anleitung, "bilder", "x.exe"), "x");
        File.WriteAllText(Path.Combine(u.Anleitung, "bilder", "x"), "x");
        Assert.Equal(HttpStatusCode.NotFound, (await u.Http.GetAsync(url)).StatusCode);
    }

    // ---------------------------------------------------------------- noch nicht erstellt

    [Fact]
    public async Task Fehlender_Ordner_zeigt_Seite_noch_nicht_erstellt_mit_Befehl()
    {
        using var u = new AnleitungUmgebung().Starten();   // Ordner gibt es nicht
        foreach (var url in new[] { "/hilfe/", "/hilfe/index.html" })
        {
            var a = await u.Http.GetAsync(url);
            Assert.Equal(HttpStatusCode.NotFound, a.StatusCode);
            Assert.Equal("text/html", a.Content.Headers.ContentType!.MediaType);
            var s = await a.Content.ReadAsStringAsync();
            Assert.Contains("Die Bedienungsanleitung ist auf diesem Rechner noch nicht erstellt", s);
            Assert.Contains("anleitung_bauen.mjs", s);
            Assert.Contains(WebUtility.HtmlEncode(u.Anleitung), s);   // sagt, wo sie hingehört
        }
        Assert.Equal(HttpStatusCode.NotFound, (await u.Http.GetAsync("/hilfe/bilder/x.webp")).StatusCode);
    }

    [Fact]
    public async Task Ordner_ohne_index_html_zeigt_dieselbe_Seite()
    {
        using var u = new AnleitungUmgebung().Starten();
        Directory.CreateDirectory(Path.Combine(u.Anleitung, "bilder"));
        var a = await u.Http.GetAsync("/hilfe/");
        Assert.Equal(HttpStatusCode.NotFound, a.StatusCode);
        Assert.Contains("noch nicht erstellt", await a.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Ohne_Ordner_in_der_Konfiguration_zeigt_die_Seite_ebenfalls()
    {
        using var u = new AnleitungUmgebung("""{ "projektablage": { "pfad": "X" } }""").Starten(anleitungOrdner: null);
        var a = await u.Http.GetAsync("/hilfe/");
        Assert.Equal(HttpStatusCode.NotFound, a.StatusCode);
        var s = await a.Content.ReadAsStringAsync();
        Assert.Contains("noch nicht erstellt", s);
        Assert.Contains("nicht eingestellt", s);
    }

    // ---------------------------------------------------------------- Pfad-Ausbruch

    [Theory]
    [InlineData("/hilfe/../geheim.html")]
    [InlineData("/hilfe/%2e%2e/geheim.html")]
    [InlineData("/hilfe/%2E%2E/geheim.html")]
    [InlineData("/hilfe/..%2fgeheim.html")]
    [InlineData("/hilfe/%2e%2e%2fgeheim.html")]
    [InlineData("/hilfe/..%5cgeheim.html")]
    [InlineData("/hilfe/%2e%2e%5cgeheim.html")]
    [InlineData("/hilfe/bilder/../../geheim.html")]
    [InlineData("/hilfe/bilder/%2e%2e/%2e%2e/geheim.json")]
    [InlineData("/hilfe/bilder/..%2f..%2fgeheim.json")]
    [InlineData("/hilfe/....//geheim.html")]
    [InlineData("/hilfe/..;/geheim.html")]
    [InlineData("/hilfe//geheim.html")]
    [InlineData("/hilfe/%5Cgeheim.html")]
    [InlineData("/hilfe/C:/Windows/win.ini")]
    [InlineData("/hilfe/C%3A%5CWindows%5Cwin.ini")]
    [InlineData("/hilfe/c:geheim.html")]
    [InlineData("/hilfe/%5C%5Cserver%5Cshare%5Cx.html")]
    [InlineData("/hilfe/index.html::$DATA")]
    [InlineData("/hilfe/index.html%3A%3A%24DATA")]
    [InlineData("/hilfe/index.html:geheim")]
    [InlineData("/hilfe/index.html%00")]
    [InlineData("/hilfe/index.html.")]
    [InlineData("/hilfe/index.html%20")]
    [InlineData("/hilfe/%252e%252e/geheim.html")]
    public async Task Pfad_Ausbruch_liefert_nie_etwas_ausserhalb(string url)
    {
        using var u = MitSeite();
        HttpResponseMessage a;
        try { a = await u.Http.SendAsync(new HttpRequestMessage(HttpMethod.Get, new Uri(url, UriKind.Relative))); }
        catch (Exception ex) when (ex is UriFormatException or InvalidOperationException) { return; }   // schon der Client bzw. der Testserver lehnt die Adresse ab (z. B. %00)
        var s = await a.Content.ReadAsStringAsync();
        Assert.DoesNotContain(Geheimnis, s);
        Assert.DoesNotContain("[fonts]", s);   // win.ini
        // Nie 200 mit Inhalt ausserhalb; erlaubt sind Ablehnung (400), Fehlen (404) oder – bei ungefährlichen Formen – die Anleitung selbst
        Assert.True(a.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.OK, $"{url}: {a.StatusCode}");
        if (a.StatusCode == HttpStatusCode.OK) Assert.Contains("Hallo Anleitung", s);
    }

    [Theory]
    [InlineData("..\\geheim.html")]
    [InlineData("../geheim.html")]
    [InlineData("bilder/../../geheim.html")]
    [InlineData("bilder\\..\\..\\geheim.html")]
    [InlineData("./index.html")]
    [InlineData("bilder/./foto.png")]
    [InlineData("/geheim.html")]
    [InlineData("\\geheim.html")]
    [InlineData("//server/share/x.html")]
    [InlineData("C:\\Windows\\win.ini")]
    [InlineData("C:/Windows/win.ini")]
    [InlineData("C:geheim.html")]
    [InlineData("bilder/C:/x.png")]
    [InlineData("index.html:strom")]
    [InlineData("index.html::$DATA")]
    [InlineData("bilder//foto.png")]
    [InlineData("../")]
    [InlineData("a|b.html")]
    [InlineData("a<b.html")]
    [InlineData("a\0b.html")]
    [InlineData("a\nb.html")]
    [InlineData("index.html.")]
    [InlineData("index.html ")]
    [InlineData("")]
    [InlineData(null)]
    public void Aufloesen_lehnt_gefaehrliche_Pfade_ab(string? pfad)
    {
        using var u = MitSeite();
        var r = AnleitungDienst.Aufloesen(u.Anleitung, pfad);
        Assert.Null(r.Pfad);
        Assert.True(r.Abgelehnt, $"«{pfad}» wurde nicht abgelehnt");
        Assert.False(string.IsNullOrEmpty(r.Grund));
    }

    [Theory]
    [InlineData("index.html")]
    [InlineData("INDEX.HTML")]
    [InlineData("bilder/funktion-1.webp")]
    [InlineData("bilder\\foto.png")]
    public void Aufloesen_findet_gueltige_Dateien_im_Ordner(string pfad)
    {
        using var u = MitSeite();
        var r = AnleitungDienst.Aufloesen(u.Anleitung, pfad);
        Assert.False(r.Abgelehnt);
        Assert.NotNull(r.Pfad);
        Assert.StartsWith(Path.GetFullPath(u.Anleitung) + Path.DirectorySeparatorChar, Path.GetFullPath(r.Pfad), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Aufloesen_fehlende_Datei_ist_nicht_abgelehnt_sondern_fehlt()
    {
        using var u = MitSeite();
        var r = AnleitungDienst.Aufloesen(u.Anleitung, "bilder/nein.png");
        Assert.Null(r.Pfad);
        Assert.False(r.Abgelehnt);
        Assert.False(AnleitungDienst.Aufloesen(u.Anleitung, "bilder/").Abgelehnt);   // Ordneradresse: keine Datei, aber harmlos
        Assert.Null(AnleitungDienst.Aufloesen(u.Anleitung, "bilder/").Pfad);
        Assert.Null(AnleitungDienst.Aufloesen(u.Anleitung, "stand.txt").Pfad);   // Typ nicht freigegeben
        Assert.False(AnleitungDienst.Aufloesen(u.Anleitung, "stand.txt").Abgelehnt);
    }

    [Fact]
    public async Task Junction_im_Ordner_fuehrt_nicht_hinaus()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var u = MitSeite();
        var aussen = Path.Combine(u.Wurzel, "aussen");
        Directory.CreateDirectory(aussen);
        File.WriteAllText(Path.Combine(aussen, "geheim.json"), Geheimnis);
        File.WriteAllText(Path.Combine(aussen, "geheim.html"), Geheimnis);
        var link = Path.Combine(u.Anleitung, "bilder-link");
        var p = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{aussen}\"") { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true })!;
        p.WaitForExit();
        Assert.True(Directory.Exists(link), "Die Junction liess sich nicht anlegen");
        try
        {
            var a = await u.Http.GetAsync("/hilfe/bilder-link/geheim.json");
            Assert.Equal(HttpStatusCode.BadRequest, a.StatusCode);
            Assert.DoesNotContain(Geheimnis, await a.Content.ReadAsStringAsync());
            var r = AnleitungDienst.Aufloesen(u.Anleitung, "bilder-link/geheim.html");
            Assert.True(r.Abgelehnt);
            Assert.Null(r.Pfad);
        }
        finally { try { Directory.Delete(link); } catch (IOException) { } }   // nur die Verknüpfung entfernen
    }
}
