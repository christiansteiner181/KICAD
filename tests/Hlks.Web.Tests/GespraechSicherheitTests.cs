using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;

namespace Hlks.Web.Tests;

/// <summary>Sicherheitsprüfung 3c (berichte/modul-3c-sicherheitspruefung.md), Umsetzung 20.10.2026: B1 Schutz gegen Fremdaufrufe,
/// B2 gekürztes Gesprächsprotokoll, vorläufige Sperre des Internet-Schalters, B5 Verbotsliste.</summary>
public class GespraechSicherheitTests
{
    static async Task<(HttpStatusCode Code, string Text, HttpResponseMessage Antwort)> Roh(GespraechServer s, HttpMethod m, string pfad, string? koerper,
        string? typ = "application/json", params (string Name, string Wert)[] koepfe)
    {
        var a = new HttpRequestMessage(m, pfad);
        if (koerper is not null)
        {
            a.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(koerper));
            if (typ is not null) a.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(typ);
        }
        foreach (var (n, w) in koepfe) a.Headers.TryAddWithoutValidation(n, w);
        var r = await s.Http.SendAsync(a);
        return (r.StatusCode, await r.Content.ReadAsStringAsync(), r);
    }

    // ------------------------------------------------------------------ B1: Herkunftsschutz (Middleware vor /api und /hilfe)

    [Fact]
    public async Task B1_FremderOrigin_403_MitFehlerUndVorschlag()
    {
        using var s = new GespraechServer();
        var (code, text, r) = await Roh(s, HttpMethod.Post, "/api/ki/gespraeche", "{}", "application/json", ("Origin", "https://evil.example"));
        Assert.Equal(HttpStatusCode.Forbidden, code);
        var j = JsonNode.Parse(text)!;
        Assert.Equal("Anfrage von fremder Seite abgelehnt", (string?)j["fehler"]);
        Assert.False(string.IsNullOrEmpty((string?)j["vorschlag"]));
        Assert.False(r.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.Empty(Directory.Exists(Path.Combine(s.Ordner, "ki", "gespraeche")) ? Directory.GetFiles(Path.Combine(s.Ordner, "ki", "gespraeche")) : []);
        // auch localhost mit anderem Port ist eine fremde Seite, ebenso https und «null»
        foreach (var o in new[] { "http://localhost:5999", "https://localhost", "null", "http://localhost.evil.example" })
            Assert.Equal(HttpStatusCode.Forbidden, (await Roh(s, HttpMethod.Post, "/api/befehl/geschoss_anlegen", "{}", "application/json", ("Origin", o))).Code);
        // DELETE und PUT ebenso
        Assert.Equal(HttpStatusCode.Forbidden, (await Roh(s, HttpMethod.Delete, "/api/ki/gespraeche/x", null, null, ("Origin", "https://evil.example"))).Code);
        Assert.Equal(HttpStatusCode.Forbidden, (await Roh(s, HttpMethod.Put, "/api/hilfe/pruefung/f1", "{}", "application/json", ("Origin", "https://evil.example"))).Code);
    }

    [Fact]
    public async Task B1_SecFetchSite_CrossSite_403()
    {
        using var s = new GespraechServer();
        foreach (var site in new[] { "cross-site", "same-site" })
        {
            var (code, text, _) = await Roh(s, HttpMethod.Post, "/api/ki/gespraeche", "{}", "application/json", ("Sec-Fetch-Site", site));
            Assert.Equal(HttpStatusCode.Forbidden, code);
            Assert.Contains("fremder Seite", text);
        }
        // ohne Körper (z. B. …/abbrechen) ebenso
        Assert.Equal(HttpStatusCode.Forbidden, (await Roh(s, HttpMethod.Post, "/api/ki/gespraeche/x/abbrechen", null, null, ("Sec-Fetch-Site", "cross-site"))).Code);
    }

    [Fact]
    public async Task B1_FremderHost_403_AuchBeimLesen_UndHilfe()
    {
        using var s = new GespraechServer();
        foreach (var (m, pfad) in new[] { (HttpMethod.Get, "/api/status"), (HttpMethod.Get, "/api/ki/einstellungen"), (HttpMethod.Get, "/hilfe/"), (HttpMethod.Post, "/api/ki/gespraeche") })
        {
            var a = new HttpRequestMessage(m, pfad);
            a.Headers.Host = "evil.example:5130";   // DNS-Rebinding: Name zeigt auf 127.0.0.1, Host-Kopf bleibt fremd
            if (m == HttpMethod.Post) a.Content = new StringContent("{}", Encoding.UTF8, "application/json");
            var r = await s.Http.SendAsync(a);
            Assert.True(r.StatusCode == HttpStatusCode.Forbidden, $"{m} {pfad}: {r.StatusCode}");
        }
        // die Oberfläche selbst (statische Dateien) bleibt unberührt; /api mit localhost geht
        Assert.Equal(HttpStatusCode.OK, (await Roh(s, HttpMethod.Get, "/api/status", null)).Code);
    }

    [Fact]
    public async Task B1_TextPlain_415_OhneKoerperOk()
    {
        using var s = new GespraechServer();
        await s.ProjektOeffnen();
        var (code, text, _) = await Roh(s, HttpMethod.Post, "/api/ki/gespraeche", "{}", "text/plain");
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, code);
        Assert.Contains("application/json", (string?)JsonNode.Parse(text)!["fehler"]);
        // ohne Inhaltstyp mit Körper ebenso; Formular (application/x-www-form-urlencoded) ebenso
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, (await Roh(s, HttpMethod.Post, "/api/ki/gespraeche", "{}", null)).Code);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, (await Roh(s, HttpMethod.Post, "/api/ki/gespraeche", "a=b", "application/x-www-form-urlencoded")).Code);
        // multipart nur beim Hochladen der Unterlagen, nicht an der Palette
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, (await Roh(s, HttpMethod.Post, "/api/ki/gespraeche", "x", "multipart/form-data; boundary=x")).Code);
        // ohne Körper geht es ohne Inhaltstyp (Werkzeug)
        Assert.Equal(HttpStatusCode.Created, (await Roh(s, HttpMethod.Post, "/api/ki/gespraeche", null, null)).Code);
    }

    [Fact]
    public async Task B1_EigenerOrigin_UndWerkzeugOhneOrigin_Ok()
    {
        using var s = new GespraechServer();
        await s.ProjektOeffnen();
        // TestServer: Host «localhost» ohne Port = 80
        Assert.Equal(HttpStatusCode.Created, (await Roh(s, HttpMethod.Post, "/api/ki/gespraeche", "{}", "application/json; charset=utf-8",
            ("Origin", "http://localhost"), ("Sec-Fetch-Site", "same-origin"))).Code);
        Assert.Equal(HttpStatusCode.Created, (await Roh(s, HttpMethod.Post, "/api/ki/gespraeche", "{}", "application/json", ("Sec-Fetch-Site", "none"))).Code);
        Assert.Equal(HttpStatusCode.Created, (await Roh(s, HttpMethod.Post, "/api/ki/gespraeche", "{}")).Code);   // PowerShell/Node: weder Origin noch Sec-Fetch-Site
        var (code, _, r) = await Roh(s, HttpMethod.Get, "/api/ki/einstellungen", null, null, ("Origin", "https://evil.example"));
        Assert.Equal(HttpStatusCode.OK, code);   // Lesen: der Browser gibt die Antwort ohne CORS nicht heraus
        Assert.False(r.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Theory]
    [InlineData("http://localhost:5130", "localhost:5130", true)]
    [InlineData("http://127.0.0.1:5130", "localhost:5130", true)]
    [InlineData("http://[::1]:5130", "127.0.0.1:5130", true)]
    [InlineData("http://LOCALHOST:5130", "localhost:5130", true)]
    [InlineData("http://localhost:5131", "localhost:5130", false)]
    [InlineData("https://localhost:5130", "localhost:5130", false)]
    [InlineData("http://evil.example:5130", "localhost:5130", false)]
    [InlineData("null", "localhost:5130", false)]
    [InlineData("http://localhost:5130/pfad", "localhost:5130", false)]
    public void B1_OriginErlaubt(string origin, string host, bool erwartet) =>
        Assert.Equal(erwartet, Herkunftsschutz.OriginErlaubt(origin, new HostString(host)));

    [Theory]
    [InlineData("localhost:5130", true)]
    [InlineData("127.0.0.1", true)]
    [InlineData("[::1]:5200", true)]
    [InlineData("evil.example", false)]
    [InlineData("127.0.0.1.nip.io:5130", false)]
    [InlineData("", false)]
    public void B1_HostErlaubt(string host, bool erwartet) => Assert.Equal(erwartet, Herkunftsschutz.HostErlaubt(new HostString(host)));

    [Theory]
    [InlineData("POST", "/api/unterlagen", "application/octet-stream", true)]
    [InlineData("POST", "/api/unterlagen", "multipart/form-data; boundary=abc", true)]
    [InlineData("POST", "/api/projekt/vorschau", "image/png", true)]
    [InlineData("POST", "/api/befehl/x", "application/json", true)]
    [InlineData("POST", "/api/befehl/x", "application/octet-stream", false)]
    [InlineData("POST", "/api/projekt/vorschau", "text/plain", false)]
    [InlineData("PUT", "/api/unterlagen", "application/octet-stream", false)]
    [InlineData("POST", "/api/ki/gespraeche", "text/plain;charset=UTF-8", false)]
    public void B1_InhaltstypJeEndpunkt(string methode, string pfad, string typ, bool erwartet)
    {
        var c = new DefaultHttpContext();
        c.Request.Method = methode; c.Request.Path = pfad; c.Request.ContentType = typ; c.Request.ContentLength = 3; c.Request.Host = new HostString("localhost:5130");
        Assert.Equal(erwartet, Herkunftsschutz.InhaltstypErlaubt(c.Request));
        Assert.Equal(erwartet ? null : 415, Herkunftsschutz.Pruefen(c.Request)?.Code);
    }

    // ------------------------------------------------------------------ B2: Werkzeugergebnisse im Protokoll gekürzt, Normtext nie

    [Fact]
    public void B2_ProtokollZeile_KuerztErgebnisse_NormwerkzeugeOhneText()
    {
        var normtext = "=== Seite 12 === " + new string('N', 5000);
        var anderes = new string('x', 1000);
        var o = new JsonObject
        {
            ["type"] = "user", ["session_id"] = "s1",
            ["message"] = new JsonObject { ["role"] = "user", ["content"] = new JsonArray(
                new JsonObject { ["type"] = "tool_result", ["tool_use_id"] = "t1", ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = normtext }) },
                new JsonObject { ["type"] = "tool_result", ["tool_use_id"] = "t2", ["content"] = anderes, ["is_error"] = true },
                new JsonObject { ["type"] = "tool_result", ["tool_use_id"] = "t3", ["content"] = "Treffer: SIA 385/1 Ziffer 3.2 …" },
                new JsonObject { ["type"] = "tool_result", ["tool_use_id"] = "t4", ["content"] = "kurz" }) },
            ["tool_use_result"] = normtext,   // Claude Code legt das Ergebnis teils noch einmal oben ab
        };
        var werkzeuge = new Dictionary<string, (string?, JsonObject?)>
        {
            ["t1"] = ("norm_lesen", new JsonObject { ["datei"] = "suissetec/Merkblatt.pdf", ["seite"] = 12, ["bis"] = 13 }),
            ["t2"] = ("projekt_ueberblick", new JsonObject()),
            ["t3"] = ("normen_suchen", new JsonObject { ["frage"] = "Gefälle Abwasser" }),
        };
        var zeile = GespraechDienst.ProtokollZeileWerkzeugergebnis(o, id => id is not null && werkzeuge.TryGetValue(id, out var w) ? w : (null, null));
        Assert.DoesNotContain("NNNN", zeile);
        Assert.DoesNotContain("=== Seite", zeile);
        Assert.DoesNotContain("SIA 385/1", zeile);
        Assert.DoesNotContain("tool_use_result", zeile);
        Assert.True(zeile.Length < 1500, zeile.Length.ToString());
        var b = JsonNode.Parse(zeile)!["message"]!["content"]!.AsArray();
        Assert.Equal("norm_lesen", (string?)b[0]!["werkzeug"]);
        Assert.Equal("suissetec/Merkblatt.pdf", (string?)b[0]!["norm"]);
        Assert.Equal(12, (int)b[0]!["seite"]!);
        Assert.Equal(normtext.Length, (int)b[0]!["laenge"]!);
        Assert.Null(b[0]!["content"]);
        Assert.Equal("Gefälle Abwasser", (string?)b[2]!["norm"]);
        Assert.Null(b[2]!["content"]);
        var gekuerzt = (string)b[1]!["content"]!;
        Assert.StartsWith(new string('x', GespraechDienst.ProtokollMaxZeichen), gekuerzt);
        Assert.EndsWith(" … gekürzt, 1000 Zeichen", gekuerzt);
        Assert.True((bool)b[1]!["is_error"]!);
        Assert.Equal("kurz", (string?)b[3]!["content"]);
        Assert.Equal("s1", (string?)JsonNode.Parse(zeile)!["session_id"]);
    }

    [Fact]
    public async Task B2_Lauf_NormLesen_NormtextNichtImGespraechsprotokoll()
    {
        using var s = new GespraechServer();
        await s.ProjektOeffnen();
        var id = await s.GespraechAnlegen();
        await s.FragenUndWarten(id, "Gefälle? [attrappe:normen]");
        var log = Path.Combine(s.Ordner, "ki", "gespraeche", id + ".log");
        Assert.True(File.Exists(log), log);
        var text = File.ReadAllText(log);
        foreach (var zeile in text.Split('\n').Where(z => z.Contains("\"tool_result\"")))
            Assert.True(zeile.Length < 4000, zeile.Length.ToString());
        Assert.DoesNotContain("=== Seite", text);
    }

    // ------------------------------------------------------------------ Internet-Schalter vorläufig gesperrt (B3/B4/B6 offen)

    [Fact]
    public async Task Internet_Gesperrt_Vorgabe_409_UndEinstellungenMitGrund()
    {
        Assert.False(new GespraechEinstellungen().InternetErlaubt, "Vorgabe: gesperrt");
        using var s = new GespraechServer(e => e with { InternetErlaubt = false });
        await s.ProjektOeffnen();
        var id = await s.GespraechAnlegen();
        var (code, j) = await s.Fragen(id, "Suche im Netz [attrappe:internet]", internet: true);
        Assert.Equal(HttpStatusCode.Conflict, code);
        Assert.Equal("Internet in der Palette vorläufig gesperrt (Sicherheitsprüfung 3c, B3/B4)", (string?)j!["fehler"]);
        var (c2, e) = await s.Get("/api/ki/einstellungen");
        Assert.Equal(HttpStatusCode.OK, c2);
        Assert.False((bool)e!["internetErlaubt"]!);
        Assert.Contains("vorläufig gesperrt", (string?)e["internetGrund"]);
        Assert.False((bool)e["internetVorgabe"]!);
        // ohne Internet geht die Frage
        var antwort = await s.FragenUndWarten(id, "Wie viele Geschosse? [attrappe:ueberblick]", internet: false);
        Assert.NotNull(antwort);
    }

    // ------------------------------------------------------------------ B5: Dokumentenlenkung verboten

    [Fact]
    public void B5_Revisionen_UndPlansatzSetzen_SindVerboten()
    {
        foreach (var w in new[] { "revision_hinzufuegen", "plansatz_revision_hinzufuegen", "plansatz_setzen" })
        {
            Assert.Contains(w, GespraechDienst.Verbotsliste);
            Assert.False(string.IsNullOrWhiteSpace(GespraechDienst.Verbote.Single(v => v.Werkzeug == w).Grund));
        }
    }
}
