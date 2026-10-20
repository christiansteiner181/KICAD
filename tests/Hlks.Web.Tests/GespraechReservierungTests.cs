using System.Text.Json.Nodes;
using Hlks.Kern;
using static Hlks.Web.Tests.GespraechServer;

namespace Hlks.Web.Tests;

/// <summary>
/// KI-Palette (Modul 3c, Teil O, Entscheid Hauptlauf 20.10.2026): Beim Start einer Frage ohne Internet gibt der Server die Reservierungen frei, die der Mensch auf
/// DIESEM Gerät für die Elemente der mitgeschickten Auswahl hält – sonst könnte Claude (anderer Akteur) das gewählte Element nicht ändern. Reservierungen anderer
/// Arbeitsplätze bleiben; Claude bekommt dort einen lesbaren Fehler. Mensch = Akteur «Test», Gerät «testpc» (wie GespraechServer).
/// </summary>
public class GespraechReservierungTests
{
    static readonly TimeSpan Lang = TimeSpan.FromSeconds(90);

    static async Task<string> ElementId(GespraechServer s, string kz) => await s.Dienst.Mit(p => p.Modell.Finde(kz)!.Id);

    static JsonObject Auswahl(params string[] kz) => new() { ["auswahl"] = new JsonArray(kz.Select(x => (JsonNode)JsonValue.Create(x)).ToArray()) };

    [Fact]
    public async Task EigeneReservierung_FuerDieAuswahl_WirdFreigegeben_KiAendertErfolgreich()
    {
        using var s = new GespraechServer();
        await s.ProjektOeffnen();
        var id = await ElementId(s, "WT-E.02");
        var speicher = new OrdnerSpeicher(s.Ordner);
        Assert.Empty(speicher.Reservieren([id], "Test", "testpc"));
        Assert.Contains(speicher.Reservierungen(), r => r.Element == id && r.Akteur == "Test");

        var g = await s.GespraechAnlegen();
        var antwort = await s.FragenUndWarten(g, "Benenne WT-E.02 um [attrappe:aendern=WT-E.02]", Auswahl("WT-E.02"), frist: Lang);

        Assert.Equal("fertig", Z(antwort["status"]));
        var w = Assert.Single(antwort["werkzeuge"]!.AsArray())!;
        Assert.True(w["ok"]!.GetValue<bool>(), w.ToJsonString());
        Assert.Equal("Von der Attrappe geändert", await s.ElementName("WT-E.02"));
        Assert.Single(antwort["aenderungen"]!.AsArray());
        var mensch = (await s.Gespraech(g))["nachrichten"]!.AsArray()[0]!;
        Assert.Equal(["WT-E.02"], mensch["auswahlFreigegeben"]!.AsArray().Select(x => Z(x)));
        // das Ereignis «frage» trägt den Vermerk für die Palette
        Assert.Contains(s.Ereignisse, e => Z(e["typ"]) == "frage" && e["nachricht"]!["auswahlFreigegeben"] is JsonArray a && a.Count == 1);
        Assert.DoesNotContain(speicher.Reservierungen(), r => r.Akteur == "Test" && r.Element == id);
    }

    [Fact]
    public async Task FremdeReservierung_Bleibt_KiBekommtLesbarenFehler()
    {
        using var s = new GespraechServer();
        await s.ProjektOeffnen();
        var id = await ElementId(s, "WT-E.02");
        var speicher = new OrdnerSpeicher(s.Ordner);
        Assert.Empty(speicher.Reservieren([id], "Anderer", "anderes-pc"));   // anderer Arbeitsplatz

        var g = await s.GespraechAnlegen();
        var antwort = await s.FragenUndWarten(g, "Benenne WT-E.02 um [attrappe:aendern=WT-E.02]", Auswahl("WT-E.02"), frist: Lang);

        var w = Assert.Single(antwort["werkzeuge"]!.AsArray())!;
        Assert.False(w["ok"]!.GetValue<bool>(), w.ToJsonString());
        Assert.Contains("bearbeitet", Z(w["meldung"]));
        Assert.Contains("Anderer", Z(w["meldung"]));
        Assert.Empty(antwort["aenderungen"]!.AsArray());
        Assert.Null(await s.ElementName("WT-E.02"));
        Assert.Contains(speicher.Reservierungen(), r => r.Element == id && r.Akteur == "Anderer" && r.Geraet == "anderes-pc");   // unangetastet
        Assert.Null((await s.Gespraech(g))["nachrichten"]!.AsArray()[0]!["auswahlFreigegeben"]);
    }

    [Fact]
    public async Task OhneAuswahl_UndMitInternet_WirdNichtsFreigegeben()
    {
        using var s = new GespraechServer();
        await s.ProjektOeffnen();
        var id = await ElementId(s, "WT-E.02");
        var speicher = new OrdnerSpeicher(s.Ordner);
        Assert.Empty(speicher.Reservieren([id], "Test", "testpc"));

        // ohne Auswahl: die Reservierung des Menschen bleibt, Claude scheitert
        var g = await s.GespraechAnlegen();
        var a1 = await s.FragenUndWarten(g, "Benenne um [attrappe:aendern=WT-E.02]", frist: Lang);
        Assert.False(Assert.Single(a1["werkzeuge"]!.AsArray())!["ok"]!.GetValue<bool>());
        Assert.Contains(speicher.Reservierungen(), r => r.Element == id && r.Akteur == "Test");
        Assert.Null((await s.Gespraech(g))["nachrichten"]!.AsArray()[0]!["auswahlFreigegeben"]);

        // mit Internet: der Server verwirft die Auswahl (kein Projektkontext) – und gibt deshalb nichts frei
        var g2 = await s.GespraechAnlegen();
        await s.FragenUndWarten(g2, "Suche [attrappe:antwort]", Auswahl("WT-E.02"), internet: true, frist: Lang);
        Assert.Contains(speicher.Reservierungen(), r => r.Element == id && r.Akteur == "Test");
        Assert.Null((await s.Gespraech(g2))["nachrichten"]!.AsArray()[0]!["auswahlFreigegeben"]);
    }

    [Fact]
    public async Task Strang_BeiTeilEinerAuswahl_GibtAuchDenStrangFrei()
    {
        using var s = new GespraechServer();
        await s.ProjektOeffnen();
        // ein Element mit TeilVon im Beispielprojekt: sein Strang und dessen Teile werden mit freigegeben
        var (teil, strang) = await s.Dienst.Mit(p =>
        {
            var e = p.Modell.Elemente.First(x => x.TeilVon is not null && p.Modell.Finde(x.TeilVon) is not null);
            return (e.Kennzeichen, p.Modell.Finde(e.TeilVon!)!.Id);
        });
        var speicher = new OrdnerSpeicher(s.Ordner);
        Assert.Empty(speicher.Reservieren([strang], "Test", "testpc"));
        var g = await s.GespraechAnlegen();
        await s.FragenUndWarten(g, "Nur lesen [attrappe:antwort]", Auswahl(teil), frist: Lang);
        Assert.DoesNotContain(speicher.Reservierungen(), r => r.Element == strang);
    }
}
