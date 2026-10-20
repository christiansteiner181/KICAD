using System.Text.Json;
using System.Text.Json.Nodes;
using Hlks.Kern;
using static Hlks.Mcp.Tests.Hilfe;

namespace Hlks.Mcp.Tests;

/// <summary>Modul 3, B1: MCP-Werkzeuge leitungsserie, anschlusslagen (alle Elemente) und familie_platzieren mit ausrichtung –
/// mit den FIKTIVEN Testfamilien (tests/daten/familien/test_rohrserie, test_bogen).</summary>
public class WerkzeugLeitungenTests
{
    static JsonElement Elem(string json) => JsonDocument.Parse(json).RootElement.Clone();
    static string Datei(string name) => Testdaten(Path.Combine("familien", name + ".familie.json"));

    static Werkzeuge MitSerie()
    {
        var w = new Werkzeuge(Beispiel());
        foreach (var f in new[] { "test_rohrserie", "test_bogen" })
        {
            var e = Json(w.FamilieLadenWerkzeug(Datei(f)));
            Assert.True(e["angenommen"]!.GetValue<bool>(), e.ToJsonString());
        }
        return w;
    }

    [Fact]
    public void Leitungsserie_GroessenUndFormteile()
    {
        var w = MitSerie();
        var o = Json(w.LeitungsserieLesen("fam-test-rohrserie"));
        Assert.Equal("rund", o["querschnitt"]!.GetValue<string>());
        Assert.Equal(6, o["groessen"]!.AsArray().Count);
        var dn20 = o["groessen"]!.AsArray().Single(g => g!["typ"]!.GetValue<string>() == "DN 20")!;
        Assert.Equal(25, dn20["aussen"]!.GetValue<double>());
        Assert.True(dn20["formteile"]!["bogen"]!["verfuegbar"]!.GetValue<bool>());
        Assert.False(dn20["formteile"]!["tstueck"]!["verfuegbar"]!.GetValue<bool>());   // T-Stück-Familie nicht geladen
        Assert.Contains("familie_laden", dn20["formteile"]!["tstueck"]!["vorschlag"]!.GetValue<string>());
        // keine Serie: Fehler als JSON mit Vorschlag
        var f = Json(w.LeitungsserieLesen("WT-E.02"));
        Assert.NotNull(f["fehler"]);
    }

    [Fact]
    public void FamiliePlatzieren_MitAusrichtung_UndAnschlusslagenFuerAlleElemente()
    {
        var w = MitSerie();
        var e = Json(w.FamiliePlatzierenWerkzeug("fam-test-bogen", "DN 20", Elem("[1000, 2000, 500]"), parameter: Elem("""{ "winkel": 90 }"""),
            ausrichtung: Elem("""{ "x": [1, 0, 0], "z": [0, -1, 0] }"""), kennzeichen: "BO-1", system: "KW", geschoss: "EG"));
        Assert.True(e["angenommen"]!.GetValue<bool>(), e.ToJsonString());
        var b = Json(w.Anschlusslagen("BO-1"))["anschluesse"]!.AsArray().Single(x => x!["name"]!.GetValue<string>() == "b")!;
        Assert.Equal([0.0, 0, 1], b["richtung"]!.AsArray().Select(x => x!.GetValue<double>()));
        // Rohr ohne Familie aus dem Beispielprojekt: Lage aus dem Verlauf
        var ro = Json(w.Anschlusslagen("KW-V1-0"))["anschluesse"]!.AsArray().Single(x => x!["name"]!.GetValue<string>() == "anfang")!;
        Assert.Equal([500.0, 2500, -2400], ro["lage"]!.AsArray().Select(x => x!.GetValue<double>()));
        Assert.Equal("leitung", ro["quelle"]!.GetValue<string>());
        // Apparat ohne Familie: ohne Lage
        var wc = Json(w.Anschlusslagen("WC-E.02"));
        Assert.Null(wc["anschluesse"]![0]!["lage"]);
        // ungültige Ausrichtung: abgelehnt mit Vorschlag
        var n = Json(w.FamiliePlatzierenWerkzeug("fam-test-bogen", "DN 20", Elem("[0, 0, 0]"), ausrichtung: Elem("""{ "x": [1, 0, 0], "z": [1, 1, 0] }"""), system: "KW"));
        Assert.False(n["angenommen"]!.GetValue<bool>());
        Assert.Contains("rechtwinklig", n["fehler"]!.GetValue<string>());
    }

    [Fact]
    public void FreieGroesse_LeitungsserieMitProbe_LeitungZiehenUndZeichnen()
    {
        // Nachtrag freie Grösse (entwurf/freie-groesse-kanal.md): FIKTIVE Testfamilien test_kanalfrei und Formteile mit Exemplar-Grössen
        var w = new Werkzeuge(Beispiel());
        foreach (var f in new[] { "test_kanalfrei", "test_kanalbogen_frei", "test_kanal_t_frei", "test_kanalreduktion_frei" })
            Assert.True(Json(w.FamilieLadenWerkzeug(Datei(f)))["angenommen"]!.GetValue<bool>(), f);
        Assert.True(Json(w.ElementAnlegen("System", "ZUL", eigenschaften: Elem("""{ "systemtyp": "Zuluft" }""")))["angenommen"]!.GetValue<bool>());
        var s = Json(w.LeitungsserieLesen("fam-test-kanalfrei"));
        Assert.Equal("400x200", s["frei"]!["vorlage"]!.GetValue<string>());
        Assert.True(s["probe"]!["formteile"]!["bogen"]!["verfuegbar"]!.GetValue<bool>());
        // leitung_ziehen mit Serie und freier Grösse
        var z = Json(w.LeitungZiehen(Elem("[[0, 90000, 2500], [3000, 90000, 2500]]"), system: "ZUL", kennzeichen: "KA-Z", serie: "fam-test-kanalfrei", groesse: Elem("\"430x270\"")));
        Assert.True(z["angenommen"]!.GetValue<bool>(), z.ToJsonString());
        var ka = Json(w.ElementLesen("KA-Z"));
        Assert.Contains("430", ka.ToJsonString());
        // leitung_zeichnen 430 × 270 mit Bogen 90°: Einbaulänge 365 mm (Hinweis der Vorschau)
        var e = Json(w.LeitungZeichnenWerkzeug("fam-test-kanalfrei", Elem("""{ "b": 430, "h": 270 }"""), Elem("[[0, 95000, 2500], [3000, 95000, 2500], [3000, 98000, 2500]]"), system: "ZUL", trockenlauf: true));
        Assert.True(e["angenommen"]!.GetValue<bool>(), e.ToJsonString());
        Assert.Contains(e["hinweise"]!.AsArray(), h => h!.GetValue<string>().Contains("Einbaulängen a 365 mm, b 365 mm") && h.GetValue<string>().EndsWith(", frei 430 × 270"));
        // ausserhalb der Grenzen: abgelehnt mit Vorschlag
        var nein = Json(w.LeitungZeichnenWerkzeug("fam-test-kanalfrei", Elem("""{ "b": 120, "h": 270 }"""), Elem("[[0, 99000, 2500], [3000, 99000, 2500]]"), system: "ZUL"));
        Assert.False(nein["angenommen"]!.GetValue<bool>());
        Assert.Contains("ausserhalb der Grenzen", nein["fehler"]!.GetValue<string>());
        Assert.Contains("145 × 270", nein["vorschlag"]!.GetValue<string>());
    }
}
