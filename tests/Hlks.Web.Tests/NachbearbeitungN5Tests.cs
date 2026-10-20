using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Hlks.Kern;
using static Hlks.Web.Tests.GespraechServer;

namespace Hlks.Web.Tests;

/// <summary>Nachbearbeitung N5 (20.10.2026, Befund Abnahmelauf B6): lehnt der Kern einen Werkzeugaufruf ab, zeigen KI-Palette und Auftragsprotokoll ihn als
/// abgelehnt – auch unterlagen_lesen (vorher «ok»). Mit der Attrappe unter %TEMP%; sie gibt isError des MCP-Ergebnisses wie Claude Code als is_error weiter.</summary>
public class NachbearbeitungN5Tests
{
    static readonly TimeSpan Lang = TimeSpan.FromSeconds(90);

    static async Task<string> Hochladen(HttpClient http, string name, byte[] bytes)
    {
        var a = new HttpRequestMessage(HttpMethod.Post, "/api/unterlagen?name=" + Uri.EscapeDataString(name)) { Content = new ByteArrayContent(bytes) };
        a.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        var r = await http.SendAsync(a);
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == HttpStatusCode.OK, text);
        return JsonNode.Parse(text)!["name"]!.GetValue<string>();
    }

    [Fact]
    public async Task Palette_unterlagen_lesen_abgelehnt_mit_Internet_ok_ohne()
    {
        using var s = new GespraechServer();
        await s.ProjektOeffnen(beispiel: false);
        await Hochladen(s.Http, "liste.csv", Encoding.UTF8.GetBytes("Raum;Fläche\nE.01;20\n"));   // hochgeladen, keine Herstellerunterlage
        var id = await s.GespraechAnlegen();

        var nein = await s.FragenUndWarten(id, "Liste? [attrappe:lesen=liste.csv]", internet: true, frist: Lang);
        Assert.Equal("Unterlage abgelehnt (isError)", Z(nein["text"]));
        var w = Assert.Single(nein["werkzeuge"]!.AsArray())!;
        Assert.Equal("unterlagen_lesen", Z(w["name"]));
        Assert.False(w["ok"]!.GetValue<bool>(), w.ToJsonString());
        Assert.Contains("Mit Internet liest die KI nur Herstellerunterlagen", Z(w["meldung"]));

        var ja = await s.FragenUndWarten(id, "Liste ohne Internet? [attrappe:lesen=liste.csv]", frist: Lang);
        Assert.Equal("Unterlage gelesen", Z(ja["text"]));
        Assert.True(Assert.Single(ja["werkzeuge"]!.AsArray())!["ok"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Palette_Ablehnung_ohne_is_error_wird_am_Inhalt_erkannt_und_synchronisieren_bleibt_ok()
    {
        // Werkzeugfehler (element_lesen auf Unbekanntes) gibt die Attrappe ohne is_error weiter: der Inhalt {fehler} genügt (gleiche Regel wie MCP)
        using var s = new GespraechServer();
        await s.ProjektOeffnen();
        var id = await s.GespraechAnlegen();
        var w = await s.FragenUndWarten(id, "Lies GIBT-ES-NICHT [attrappe:werkzeugfehler]", frist: Lang);
        Assert.False(Assert.Single(w["werkzeuge"]!.AsArray())!["ok"]!.GetValue<bool>());
        // die Regel selbst: «fehler»: [] (synchronisieren ohne Fehler) ist keine Ablehnung – die frühere Regel der Palette zählte es als abgelehnt
        Assert.False(Werkzeugergebnis.MeldetFehler("{\"neuePakete\":0,\"konflikte\":[],\"betroffeneIds\":[],\"fehler\":[]}"));
    }

    [Fact]
    public async Task Auftragsprotokoll_nennt_abgelehnten_Werkzeugaufruf()
    {
        using var s = new AuftragServer();
        await s.ProjektAnlegen();
        var typen = await Hochladen(s.Http, "typen.csv", Encoding.UTF8.GetBytes("Typ;L [mm];H [mm]\nA-1;80;60\nA-2;90;70\n"));
        await Hochladen(s.Http, "fremd.csv", Encoding.UTF8.GetBytes("Lohn;Betrag\nX;1\n"));

        async Task<(JsonNode Ende, string Id)> Lauf(bool internet)
        {
            var (code, j) = await s.Starten("lesen", new JsonObject
            {
                ["unterlagen"] = new JsonArray("typen.csv"),
                ["hinweis"] = "Bitte lesen. [attrappe:lesen] [attrappe:fremd=fremd.csv]",
                ["internet"] = internet,
            });
            Assert.True(code == HttpStatusCode.Accepted, j?.ToJsonString());
            var ende = await s.Warten(AuftragServer.Id(j), AuftragServer.Beendet, Lang, "Lauf lesen");
            Assert.True(AuftragServer.St(ende) == "fertig", ende.ToJsonString());
            return (ende, AuftragServer.Id(j));
        }

        // mit Internet: fremd.csv abgelehnt → der Aufruf unterlagen_lesen steht als abgelehnt im Protokoll und in den Schritten
        var (mit, idMit) = await Lauf(true);
        Assert.Contains("fremd.csv: abgelehnt – Mit Internet liest die KI nur Herstellerunterlagen", mit["ergebnis"]!["bericht"]!.GetValue<string>());
        var zeile = s.Protokoll(idMit).Single(x => x["type"]?.GetValue<string>() == "hlks" && x["werkzeug"] is not null);
        Assert.Equal("Werkzeug abgelehnt: unterlagen_lesen", zeile["text"]!.GetValue<string>());
        Assert.Equal("unterlagen_lesen", zeile["werkzeug"]!["name"]!.GetValue<string>());
        Assert.False(zeile["werkzeug"]!["ok"]!.GetValue<bool>());
        Assert.Contains("Mit Internet liest die KI nur Herstellerunterlagen", zeile["werkzeug"]!["meldung"]!.GetValue<string>());
        Assert.Contains(mit["schritte"]!.AsArray(), x => x!["text"]!.GetValue<string>().StartsWith("Werkzeug abgelehnt: unterlagen_lesen – ", StringComparison.Ordinal));
        // die tool_result-Zeile trägt is_error (MCP isError, wie bei Claude Code)
        Assert.Contains(s.Protokoll(idMit), x => x["type"]?.GetValue<string>() == "user"
            && x["message"]!["content"]!.AsArray().Any(b => b!["is_error"]?.GetValue<bool>() == true));

        // ohne Internet: alles gelesen, keine Ablehnung
        var (ohne, idOhne) = await Lauf(false);
        Assert.Contains("fremd.csv: gelesen", ohne["ergebnis"]!["bericht"]!.GetValue<string>());
        Assert.DoesNotContain(s.Protokoll(idOhne), x => x["werkzeug"] is not null);
        _ = typen;
    }
}
