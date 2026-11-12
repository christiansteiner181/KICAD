using System.Net;
using System.Text.Json.Nodes;
using Hlks.Kern;
using Hlks.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace Hlks.Web.Tests;

/// <summary>Korrekturen der Gesamtprüfung vom 13.11.2026 (Bereich Oberfläche und Web-Server): Rückmeldungen der Baustelle überschreiben einander
/// nicht, eine unlesbare zugaenge.json gibt eine Meldung, unerwartete Ausnahmen unter /api kommen als { fehler, vorschlag }.</summary>
public class GesamtpruefungOberflaecheTests
{
    [Fact]
    public void Rueckmeldung_ZweiInDerselbenSekunde_BekommenVerschiedeneDateien_NameBegrenzt()
    {
        var ordner = Path.Combine(Path.GetTempPath(), "hlks_gp_" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(ordner);
        try
        {
            var zeit = new DateTime(2026, 11, 13, 7, 30, 15);
            var a = BaustelleZugang.RueckmeldungDatei(ordner, zeit, "Hans Muster");
            Assert.Equal("Rueckmeldung_20261113-073015_Hans_Muster.json", a);
            File.WriteAllText(Path.Combine(ordner, a), "{}");
            var b = BaustelleZugang.RueckmeldungDatei(ordner, zeit, "Hans Muster");
            Assert.Equal("Rueckmeldung_20261113-073015_Hans_Muster_2.json", b);
            File.WriteAllText(Path.Combine(ordner, b), "{}");
            Assert.Equal("Rueckmeldung_20261113-073015_Hans_Muster_3.json", BaustelleZugang.RueckmeldungDatei(ordner, zeit, "Hans Muster"));
            // ein sehr langer oder böser Name sprengt den Pfad nicht und verlässt den Ordner nicht
            var lang = BaustelleZugang.RueckmeldungDatei(ordner, zeit, new string('x', 400) + "..\\..\\boese");
            Assert.True(lang.Length < 80, lang);
            Assert.Equal(lang, Path.GetFileName(lang));
        }
        finally { try { Directory.Delete(ordner, true); } catch (IOException) { } }
    }

    [Fact]
    public void Zugaenge_UnlesbareDatei_MeldungMitAusweg_UngueltigeEintraegeFallenWeg()
    {
        var f = Assert.Throws<HlksFehler>(() => BaustelleZugaenge.AusText("{ \"zugaenge\": [ "));
        Assert.Contains("zugaenge.json", f.Message);
        Assert.False(string.IsNullOrWhiteSpace(f.Vorschlag));
        // leer bzw. ohne gültigen Eintrag: gesperrt (leere Liste), nicht offen (null gibt es nur ohne Datei)
        Assert.Empty(BaustelleZugaenge.AusText("{}"));
        var l = BaustelleZugaenge.AusText("""
            { "zugaenge": [ { "name": "A", "rolle": "Monteur", "schluessel": "abcdefgh2345" }, { "name": "B", "rolle": "chef", "schluessel": "abcdefgh2345" },
                            { "name": "C", "rolle": "monteur", "schluessel": "kurz" } ] }
            """);
        Assert.Equal("A", Assert.Single(l).Name);
        Assert.Equal(BaustelleZugaenge.Monteur, l[0].Rolle);
    }

    [Fact]
    public async Task UnerwarteteAusnahme_UnterApi_KommtAlsFehlerMitVorschlag()
    {
        var b = WebApplication.CreateBuilder();
        b.WebHost.UseTestServer();
        await using var app = b.Build();
        app.UseFehlerschutz();
        app.MapGet("/api/kaputt", IResult () => throw new InvalidOperationException("bumm"));
        app.MapGet("/api/gut", () => Results.Text("ok"));
        await app.StartAsync();
        var http = app.GetTestClient();
        var r = await http.GetAsync("/api/kaputt");
        Assert.Equal(HttpStatusCode.InternalServerError, r.StatusCode);
        var j = JsonNode.Parse(await r.Content.ReadAsStringAsync())!;
        Assert.Contains("bumm", (string)j["fehler"]!);
        Assert.Equal(Fehlerschutz.Vorschlag, (string)j["vorschlag"]!);
        Assert.Equal("ok", await http.GetStringAsync("/api/gut"));
    }
}
