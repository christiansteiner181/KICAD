using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Hlks.Kern;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Hlks.Web.Tests;

/// <summary>
/// Benutzereinstellung «gesten» (Modul G «Gesten am Arbeitsplatz», entwurf/modul-gesten.md, GestenEinstellung.cs): Rundlauf über die Dateien der Rechner und den Endpunkt,
/// Vorgaben (ohne Eintrag: nichts gespeichert, die Oberfläche setzt aus, linke Hand, Skelett an), Fehlerfälle und dass der Server nur Einstellungen annimmt (nie Bilddaten).
/// Jeder Test hat seinen eigenen Ordner unter %TEMP% – nie die echten Dateien des Benutzers.
/// </summary>
[Collection("BenutzerUmgebung")]
public class GestenEinstellungTests : IDisposable
{
    readonly string wurzel = Path.Combine(Path.GetTempPath(), "hlks_gesten_" + Guid.NewGuid().ToString("N")[..10]);
    readonly List<IDisposable> aufraeumen = [];

    public GestenEinstellungTests() { Directory.CreateDirectory(wurzel); }

    public void Dispose()
    {
        foreach (var d in aufraeumen) { try { d.Dispose(); } catch { /* egal */ } }
        for (int i = 0; i < 5; i++)
        {
            try { if (Directory.Exists(wurzel)) Directory.Delete(wurzel, true); return; }
            catch (IOException) { Thread.Sleep(200); }
            catch (UnauthorizedAccessException) { Thread.Sleep(200); }
        }
    }

    string Ordner(string name = "benutzer") => Path.Combine(wurzel, name);

    BenutzerEinstellungen Rechner(string rechner = "pc-a", string? ordner = null) =>
        new(new RechnerAblage(ordner ?? Ordner(), rechner, true, "einstellungen.json", "Umleitung"));

    static JsonNode Wert(string json) => JsonNode.Parse(json)!;

    const string Voll = """
        { "an": true, "hand": "rechts", "kameraId": "abc123", "skelett": false,
          "stellschrauben": { "anstoss_weg": 0.05, "totbereich": 0.015, "zoom_glaettung": 0.4 },
          "belegung": { "G2": { "links": "ansicht.reiter-zurueck", "rechts": "ansicht.reiter-vor" }, "G6": "ansicht.zoom-alles" } }
        """;

    // ---------------------------------------------------------------- Prüfung (rein)

    [Fact]
    public void Die_Einstellung_ist_bekannt_und_der_Schluessel_gueltig()
    {
        Assert.True(BenutzerEinstellungen.Bekannt.ContainsKey("gesten"));
        Assert.Equal("gesten", GestenEinstellung.Schluessel);
        BenutzerEinstellungen.Pruefen("gesten", Wert(Voll));
    }

    [Theory]
    [InlineData("{}")]                                                                                        // alles wahlweise
    [InlineData("""{ "an": false }""")]
    [InlineData("""{ "an": true, "hand": "links" }""")]
    [InlineData("""{ "kameraId": null }""")]
    [InlineData("""{ "kameraId": "" }""")]
    [InlineData("""{ "stellschrauben": {} }""")]
    [InlineData("""{ "stellschrauben": { "a.b_c": -1.5 } }""")]
    [InlineData("""{ "belegung": {} }""")]
    [InlineData("""{ "belegung": { "G6": "" } }""")]                                                           // leer = keine Wirkung
    [InlineData("""{ "belegung": { "G2": { "rechts": "ansicht.reiter-vor" } } }""")]
    [InlineData("""{ "belegung": { "G2": { "links": null, "rechts": "ansicht.zoom-alle" }, "G6": "ansicht.zoom-alles" } }""")]
    public void Gueltige_Einstellungen_werden_angenommen(string json) => BenutzerEinstellungen.Pruefen("gesten", Wert(json));

    [Theory]
    [InlineData("3")]                                                                                          // kein Objekt
    [InlineData("\"links\"")]
    [InlineData("[1,2]")]
    [InlineData("""{ "an": "ja" }""")]
    [InlineData("""{ "an": 1 }""")]
    [InlineData("""{ "skelett": "nein" }""")]
    [InlineData("""{ "hand": "oben" }""")]
    [InlineData("""{ "hand": 5 }""")]
    [InlineData("""{ "kameraId": 5 }""")]
    [InlineData("""{ "kameraId": { "a": 1 } }""")]
    [InlineData("""{ "stellschrauben": [1] }""")]
    [InlineData("""{ "stellschrauben": { "Gross": 1 } }""")]                                                  // Namen: Kleinbuchstaben
    [InlineData("""{ "stellschrauben": { "a": "x" } }""")]                                                     // nur Zahlen
    [InlineData("""{ "stellschrauben": { "a": null } }""")]
    [InlineData("""{ "stellschrauben": { "a": true } }""")]
    [InlineData("""{ "belegung": { "G3": "ansicht.zoom-alles" } }""")]                                         // nur G2 und G6
    [InlineData("""{ "belegung": { "G6": 5 } }""")]
    [InlineData("""{ "belegung": { "G6": "kein gueltige id!" } }""")]
    [InlineData("""{ "belegung": { "G2": "ansicht.reiter-vor" } }""")]                                         // G2 braucht links/rechts
    [InlineData("""{ "belegung": { "G2": { "oben": "ansicht.reiter-vor" } } }""")]
    [InlineData("""{ "belegung": { "G2": { "links": "../x" } } }""")]
    [InlineData("""{ "belegung": [1] }""")]
    public void Ungueltige_Einstellungen_werden_mit_Meldung_und_Vorschlag_abgelehnt(string json)
    {
        var f = Assert.Throws<HlksFehler>(() => BenutzerEinstellungen.Pruefen("gesten", Wert(json)));
        Assert.Contains("gesten", f.Message);
        Assert.False(string.IsNullOrWhiteSpace(f.Vorschlag));
    }

    [Theory]
    [InlineData("""{ "bild": "data:image/png;base64,AAAA" }""")]
    [InlineData("""{ "kamerabild": "AAAA" }""")]
    [InlineData("""{ "bilder": [{ "t": 0, "punkte": [] }] }""")]
    [InlineData("""{ "an": true, "frame": "AAAA" }""")]
    [InlineData("""{ "punkte": [[0,0]] }""")]
    public void Der_Server_nimmt_nur_Einstellungen_an_nie_Bilddaten_oder_Punkte(string json)
    {
        var f = Assert.Throws<HlksFehler>(() => BenutzerEinstellungen.Pruefen("gesten", Wert(json)));
        Assert.Contains("Unbekanntes Feld", f.Message);
    }

    [Fact]
    public void Zu_viele_Stellschrauben_und_zu_grosse_Werte_werden_abgelehnt()
    {
        var viele = new JsonObject();
        for (int i = 0; i <= GestenEinstellung.StellschraubenMax; i++) viele[$"s{i}"] = i;
        Assert.Throws<HlksFehler>(() => BenutzerEinstellungen.Pruefen("gesten", new JsonObject { ["stellschrauben"] = viele }));
        var lang = new JsonObject { ["kameraId"] = new string('x', 401) };
        Assert.Throws<HlksFehler>(() => BenutzerEinstellungen.Pruefen("gesten", lang));
        var gross = new JsonObject { ["kameraId"] = new string('x', 400), ["stellschrauben"] = new JsonObject { ["a"] = 1 }, ["belegung"] = new JsonObject { ["G6"] = new string('a', 65) } };
        Assert.Throws<HlksFehler>(() => BenutzerEinstellungen.Pruefen("gesten", gross));
        // gesamt über 4000 Zeichen: allgemeine Grenze der Benutzereinstellungen
        var riesig = new JsonObject { ["stellschrauben"] = new JsonObject() };
        for (int i = 0; i < 48; i++) ((JsonObject)riesig["stellschrauben"]!)[$"stellschraube_mit_langem_namen_{i}"] = 1234567.891234 + i;
        BenutzerEinstellungen.Pruefen("gesten", riesig);   // 48 Stellschrauben passen noch (unter 4000 Zeichen)
    }

    // ---------------------------------------------------------------- Rundlauf über die Dateien

    [Fact]
    public void Ohne_Eintrag_gibt_es_keinen_Wert_die_Vorgaben_setzt_die_Oberflaeche()
    {
        var b = Rechner();
        Assert.Null(b.Hole("gesten"));
        Assert.DoesNotContain("gesten", b.Alle().Keys);
    }

    [Fact]
    public void Rundlauf_Setzen_Lesen_und_Zuruecksetzen_ueber_die_Datei_des_Rechners()
    {
        var b = Rechner();
        b.Setzen("gesten", Wert(Voll));
        var gelesen = Rechner().Hole("gesten")!.AsObject();                      // frische Instanz liest die Datei
        Assert.True(gelesen["an"]!.GetValue<bool>());
        Assert.Equal("rechts", gelesen["hand"]!.GetValue<string>());
        Assert.Equal("abc123", gelesen["kameraId"]!.GetValue<string>());
        Assert.False(gelesen["skelett"]!.GetValue<bool>());
        Assert.Equal(0.05, gelesen["stellschrauben"]!["anstoss_weg"]!.GetValue<double>());
        Assert.Equal("ansicht.reiter-zurueck", gelesen["belegung"]!["G2"]!["links"]!.GetValue<string>());
        Assert.Equal("ansicht.reiter-vor", gelesen["belegung"]!["G2"]!["rechts"]!.GetValue<string>());
        Assert.Equal("ansicht.zoom-alles", gelesen["belegung"]!["G6"]!.GetValue<string>());
        Assert.True(File.Exists(Path.Combine(Ordner(), "pc-a.json")));

        b.Setzen("gesten", null);                                                 // zurücksetzen: wieder die Vorgaben
        Assert.Null(Rechner().Hole("gesten"));
    }

    [Fact]
    public void Der_neueste_Wert_gilt_ueber_zwei_Rechner_des_Benutzers()
    {
        var a = Rechner("pc-a");
        var b = Rechner("pc-b");
        a.Uhr = () => new DateTimeOffset(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);
        b.Uhr = () => new DateTimeOffset(2026, 10, 26, 10, 0, 0, TimeSpan.Zero);
        a.Setzen("gesten", Wert("""{ "an": true, "hand": "links" }"""));
        b.Setzen("gesten", Wert("""{ "an": false, "hand": "rechts" }"""));
        foreach (var r in new[] { Rechner("pc-a"), Rechner("pc-b") })
        {
            var g = r.Hole("gesten")!.AsObject();
            Assert.False(g["an"]!.GetValue<bool>());
            Assert.Equal("rechts", g["hand"]!.GetValue<string>());
        }
    }

    [Fact]
    public void Ein_ungueltiger_Wert_aendert_die_Datei_nicht()
    {
        var b = Rechner();
        b.Setzen("gesten", Wert("""{ "an": true }"""));
        var vorher = File.ReadAllText(Path.Combine(Ordner(), "pc-a.json"));
        Assert.Throws<HlksFehler>(() => b.Setzen("gesten", Wert("""{ "an": "vielleicht" }""")));
        Assert.Equal(vorher, File.ReadAllText(Path.Combine(Ordner(), "pc-a.json")));
        Assert.True(Rechner().Hole("gesten")!["an"]!.GetValue<bool>());
    }

    // ---------------------------------------------------------------- Endpunkt

    sealed class Server : IDisposable
    {
        readonly WebApplicationFactory<Program> basis = new();
        readonly WebApplicationFactory<Program> fabrik;
        public HttpClient Http { get; }

        public Server(string benutzerOrdner, string wurzel)
        {
            var e = new Einstellungen("Test", "testpc", Katalog.StandardOrdner(), null)
            {
                BenutzerOrdner = benutzerOrdner, ZuletztOrdner = Path.Combine(wurzel, "zuletzt"), ZuletztMerken = false,
                ProjektAblage = Path.Combine(wurzel, "ablage"), Familienbibliothek = Path.Combine(wurzel, "bibliothek"),
            };
            fabrik = basis.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton(e)));
            Http = fabrik.CreateClient();
        }

        public async Task<(HttpStatusCode Code, JsonNode? Json)> Get()
        {
            var a = await Http.GetAsync("/api/benutzer/einstellungen");
            var t = await a.Content.ReadAsStringAsync();
            return (a.StatusCode, t.Length == 0 ? null : JsonNode.Parse(t));
        }

        public async Task<(HttpStatusCode Code, JsonNode? Json)> Post(string koerper)
        {
            var a = await Http.PostAsync("/api/benutzer/einstellungen", new StringContent(koerper, Encoding.UTF8, "application/json"));
            var t = await a.Content.ReadAsStringAsync();
            return (a.StatusCode, t.Length == 0 ? null : JsonNode.Parse(t));
        }

        public void Dispose() { Http.Dispose(); fabrik.Dispose(); basis.Dispose(); }
    }

    Server Starten(string benutzerOrdner) { var s = new Server(benutzerOrdner, wurzel); aufraeumen.Add(s); return s; }

    static string Koerper(string wertJson) => $$"""{ "schluessel": "gesten", "wert": {{wertJson}} }""";

    [Fact]
    public async Task Endpunkt_Vorgaben_Rundlauf_und_Zuruecksetzen()
    {
        var s = Starten(Ordner());
        var (c0, g0) = await s.Get();
        Assert.Equal(HttpStatusCode.OK, c0);
        Assert.Null(g0!["werte"]!["gesten"]);                                     // ohne Eintrag: Vorgaben der Oberfläche (aus, links, Skelett an)

        var (c1, p) = await s.Post(Koerper(Voll));
        Assert.Equal(HttpStatusCode.OK, c1);
        Assert.Equal("gesten", p!["schluessel"]!.GetValue<string>());
        Assert.Equal("rechts", p["wert"]!["hand"]!.GetValue<string>());

        var (_, g1) = await s.Get();
        var w = g1!["werte"]!["gesten"]!.AsObject();
        Assert.True(w["an"]!.GetValue<bool>());
        Assert.Equal("abc123", w["kameraId"]!.GetValue<string>());
        Assert.Equal(0.4, w["stellschrauben"]!["zoom_glaettung"]!.GetValue<double>());
        Assert.Equal("ansicht.zoom-alles", w["belegung"]!["G6"]!.GetValue<string>());
        Assert.Equal("testpc", g1["zeiten"]!["gesten"]!["rechner"]!.GetValue<string>());

        var (c2, _) = await s.Post("""{ "schluessel": "gesten", "wert": null }""");
        Assert.Equal(HttpStatusCode.OK, c2);
        Assert.Null((await s.Get()).Json!["werte"]!["gesten"]);
    }

    [Theory]
    [InlineData("""{ "an": "ja" }""")]
    [InlineData("""{ "hand": "oben" }""")]
    [InlineData("""{ "belegung": { "G9": "ansicht.zoom-alles" } }""")]
    [InlineData("""{ "bild": "data:image/png;base64,AAAA" }""")]
    [InlineData("""[1,2,3]""")]
    [InlineData("""17""")]
    public async Task Endpunkt_lehnt_Ungueltiges_mit_400_Fehler_und_Vorschlag_ab_und_schreibt_nichts(string wertJson)
    {
        var s = Starten(Ordner());
        var (code, j) = await s.Post(Koerper(wertJson));
        Assert.Equal(HttpStatusCode.BadRequest, code);
        Assert.Contains("gesten", j!["fehler"]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(j["vorschlag"]?.GetValue<string>()));
        Assert.Empty(Assert.IsType<JsonObject>((await s.Get()).Json!["werte"]));
        Assert.False(Directory.Exists(Ordner()) && Directory.GetFiles(Ordner(), "*.json").Length > 0, "nichts geschrieben");
    }

    [Fact]
    public async Task Zwei_Benutzer_behalten_ihre_eigene_Einstellung()
    {
        var anna = Starten(Ordner("anna"));
        var beat = Starten(Ordner("beat"));
        await anna.Post(Koerper("""{ "an": true, "hand": "links" }"""));
        await beat.Post(Koerper("""{ "an": false, "hand": "rechts" }"""));
        Assert.True((await anna.Get()).Json!["werte"]!["gesten"]!["an"]!.GetValue<bool>());
        Assert.Equal("rechts", (await beat.Get()).Json!["werte"]!["gesten"]!["hand"]!.GetValue<string>());
    }
}
