using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Verbindungstabelle (Christian 03.11.2026): andere Verbindungstechnik am Gerät → Übergang automatisch, bei mehreren eine Auswahl.
/// Alle Familien FIKTIV (Testrohrserie mit verbindung «press», Übergang Press → Innengewinde, Pumpe mit Aussengewinde).</summary>
public class VerbindungenTests
{
    const string Uebergang = """
        { "format": 1, "name": "Übergang Press-Rp (fiktiv)", "kategorie": "Uebergang",
          "parameter": [ { "name": "dn_a", "art": "typ", "datentyp": "dn" }, { "name": "gw", "art": "typ", "datentyp": "text" },
                         { "name": "l", "art": "typ", "datentyp": "laenge" } ],
          "typen": [ { "name": "T1", "werte": { "dn_a": 20, "gw": "Rp 3/4", "l": 40 } }, { "name": "T2", "werte": { "dn_a": 20, "gw": "Rp 1", "l": 45 } },
                     { "name": "T2b", "werte": { "dn_a": 20, "gw": "Rp 1", "l": 50 } }, { "name": "T3", "werte": { "dn_a": 25, "gw": "Rp 1", "l": 45 } } ],
          "koerper": [ { "id": "k", "art": "zylinder", "von": [0, 0, 0], "nach": ["l", 0, 0], "r": 15 } ],
          "anschluesse": [ { "name": "a", "medium": "*", "fluss": "beide", "art": "rohr", "dn": "dn_a", "verbindung": "press", "lage": [0, 0, 0], "richtung": [-1, 0, 0] },
                           { "name": "b", "medium": "*", "fluss": "beide", "art": "rohr", "gewinde": "gw", "verbindung": "gewinde-innen", "lage": ["l", 0, 0], "richtung": [1, 0, 0] } ] }
        """;

    const string Pumpe = """
        { "format": 1, "name": "Musterpumpe (fiktiv)", "kategorie": "Pumpe", "parameter": [], "typen": [ { "name": "A", "werte": {} } ],
          "koerper": [ { "id": "k", "art": "quader", "von": [-100, -50, -50], "bis": [100, 50, 50] } ],
          "anschluesse": [ { "name": "saug", "medium": "*", "fluss": "rein", "art": "rohr", "gewinde": "G 3/4", "verbindung": "gewinde-aussen", "lage": [-100, 0, 0], "richtung": [-1, 0, 0] },
                           { "name": "druck", "medium": "*", "fluss": "raus", "art": "rohr", "gewinde": "G 1", "verbindung": "gewinde-aussen", "lage": [100, 0, 0], "richtung": [1, 0, 0] } ] }
        """;

    static Projekt MitPumpe()
    {
        var p = Modul3Hilfe.MitSerie(false);   // System KW, Testrohrserie
        foreach (var f in new[] { "test_bogen", "test_tstueck", "test_reduktion" })
            Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Modul3Hilfe.Datei(f) }));
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = FamilieTestHilfe.JsonAus(Uebergang) }));
        var serie = Modul3Hilfe.Familie("test_rohrserie");
        serie["leitung"]!["verbindung"] = "press";
        serie["leitung"]!["uebergaenge"] = new JsonArray("Übergang Press-Rp (fiktiv)");
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = serie, ["familie"] = "fam-test-rohrserie" }));
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = FamilieTestHilfe.JsonAus(Pumpe) }));
        Ok(p.Ausfuehren("familie_platzieren", new JsonObject
        {
            ["familie"] = "Musterpumpe (fiktiv)", ["typ"] = "A", ["punkt"] = new JsonArray(0, 0, 0), ["kennzeichen"] = "P-1", ["system"] = "KW",
        }));
        return p;
    }

    static JsonObject Zeichnen(string anschluss, double x, JsonObject? uebergang = null)
    {
        var par = new JsonObject
        {
            ["serie"] = "fam-test-rohrserie", ["groesse"] = new JsonObject { ["dn"] = 20 }, ["system"] = "KW",
            ["punkte"] = new JsonArray(new JsonArray(x * 11, 0, 0), new JsonArray(x, 0, 0)),
            ["nach"] = new JsonObject { ["element"] = "P-1", ["anschluss"] = anschluss },
        };
        if (uebergang is not null) par["uebergang"] = uebergang;
        return par;
    }

    [Theory]
    [InlineData("Rp 1 1/4", "1 1/4")]
    [InlineData("G1-1/4", "1 1/4")]
    [InlineData("1 1/4\"", "1 1/4")]
    [InlineData("Rp 3/4", "3/4")]
    [InlineData("R 2", "2")]
    public void Zoll_Normalisiert(string text, string zoll) => Assert.Equal(zoll, Verbindungen.Zoll(text));

    [Fact]
    public void Direkt_InnenAnAussen_SonstGleicheTechnik()
    {
        Assert.True(Verbindungen.Direkt("gewinde-innen", "gewinde-aussen"));
        Assert.False(Verbindungen.Direkt("gewinde-innen", "gewinde-innen"));
        Assert.False(Verbindungen.Direkt("press", "gewinde-aussen"));
        Assert.True(Verbindungen.Direkt("flansch", "flansch"));
        Assert.Null(Verbindungen.Direkt(null, "flansch"));
    }

    [Fact]
    public void Routing_SetztDenEinzigPassendenUebergang()
    {
        var p = MitPumpe();
        Ok(p.Ausfuehren("leitung_zeichnen", Zeichnen("saug", -100)));
        var ue = Assert.Single(p.Modell.DerKlasse("Uebergang"));
        Assert.Equal("T1", ue.Text("familientyp"));   // Rp 3/4 an G 3/4; T2/T3 haben Rp 1
    }

    [Fact]
    public void Routing_MehrereUebergaenge_AuswahlVerlangt()
    {
        var p = MitPumpe();
        var e = Nein(p.Ausfuehren("leitung_zeichnen", Zeichnen("druck", 100)));
        Assert.Contains("Bitte wählen", e.Fehler);
        Assert.Contains("T2b", e.Fehler);
        Assert.DoesNotContain("T3", e.Fehler);   // T3 hat a = DN 25
        Ok(p.Ausfuehren("leitung_zeichnen", Zeichnen("druck", 100, new JsonObject { ["typ"] = "T2b" })));
        Assert.Equal("T2b", Assert.Single(p.Modell.DerKlasse("Uebergang")).Text("familientyp"));
    }

    [Fact]
    public void Abfrage_OhneGroesse_SchlaegtAlleGroessenVor()
    {
        var p = MitPumpe();
        var o = (JsonObject)Tuer.Abfrage(p, "verbindung_pruefen", new JsonObject { ["element"] = "P-1", ["anschluss"] = "druck", ["serie"] = "fam-test-rohrserie" });
        Assert.Equal("uebergang", o["status"]!.GetValue<string>());
        Assert.Equal(["T2", "T2b", "T3"], (o["auswahl"] as JsonArray)!.Select(x => x!["typ"]!.GetValue<string>()).Order());
    }
}
