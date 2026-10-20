using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.FamilieTestHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 2, B1: Lesen und Prüfen der Familiendefinition – Namen, Arten, Datentypen, Kategorie, Medium, Anschlüsse,
/// Körper, Ziele, Einhängepunkt für zusätzliche Prüfungen (B4). Alle Werte fiktiv.</summary>
public class FamilieDefinitionTests
{
    static FamFehler Einer(FamilieDef f, string stelle) =>
        f.Fehler.FirstOrDefault(x => x.Stelle == stelle) ?? throw new Xunit.Sdk.XunitException($"Kein Fehler bei «{stelle}»: {string.Join(" | ", f.Fehler)}");

    [Fact]
    public void Testfamilie_IstGueltig()
    {
        var f = Def(Testheizkoerper());
        Assert.True(f.Gueltig, string.Join(" | ", f.Fehler));
        Assert.Equal("Heizkoerper", f.Kategorie);
        Assert.Equal(11, f.Parameter.Count);
        Assert.Equal(3, f.Typen.Count);
        Assert.Equal(4, f.Koerper.Count);
        Assert.Equal(2, f.Anschluesse.Count);
        Assert.Equal("fam-test-heizkoerper", f.Id);
    }

    [Fact]
    public void Namen_Eindeutig()
    {
        var f = Def(Mini("""
            [{ "name": "a", "art": "typ", "datentyp": "zahl" }, { "name": "a", "art": "typ", "datentyp": "zahl" }, { "name": "A", "art": "typ", "datentyp": "zahl" },
             { "name": "1x", "art": "typ", "datentyp": "zahl" }, { "name": "mit leer", "art": "typ", "datentyp": "zahl" }]
            """,
            """[{ "name": "T", "werte": {} }, { "name": "T", "werte": {} }]""",
            """[{ "id": "k", "art": "kugel", "mitte": [0, 0, 0], "r": 10 }, { "id": "k", "art": "kugel", "mitte": [0, 0, 0], "r": 20 }]""",
            """
            [{ "name": "vl", "medium": "HZ-VL", "fluss": "rein", "lage": [0, 0, 0], "richtung": [0, 0, -1] },
             { "name": "VL", "medium": "HZ-VL", "fluss": "rein", "lage": [0, 0, 0], "richtung": [0, 0, -1] }]
            """));
        Assert.Contains(f.Fehler, x => x.Meldung.Contains("«a» gibt es zweimal"));
        Assert.Contains(f.Fehler, x => x.Meldung.Contains("nur in Gross/Kleinschreibung"));
        Assert.Contains(f.Fehler, x => x.Meldung.Contains("«1x» ist ungültig"));
        Assert.Contains(f.Fehler, x => x.Meldung.Contains("«mit leer» ist ungültig"));
        Assert.Contains(f.Fehler, x => x.Meldung.Contains("Typ «T» gibt es zweimal"));
        Assert.Contains(f.Fehler, x => x.Meldung.Contains("Körper-id «k» gibt es zweimal"));
        Assert.Contains(f.Fehler, x => x.Meldung.Contains("Anschluss «VL» gibt es zweimal"));
        Assert.All(f.Fehler, x => Assert.False(string.IsNullOrWhiteSpace(x.Vorschlag), x.ToString()));
    }

    [Fact]
    public void ArtUndDatentyp_Gueltig()
    {
        var f = Def(Mini("""[{ "name": "a", "art": "instanz", "datentyp": "laenge" }, { "name": "b", "art": "typ", "datentyp": "fläche" }, { "name": "c", "art": "typ", "datentyp": "laenge", "einheit": "m" }]"""));
        Assert.Contains("typ (je Typ fest) oder exemplar", Einer(f, "parameter «a».art").Vorschlag);
        Assert.Contains("laenge, winkel", Einer(f, "parameter «b».datentyp").Vorschlag);
        Assert.Contains("immer in mm", Einer(f, "parameter «c».einheit").Meldung);
    }

    [Theory]
    [InlineData("Raum", "nicht familienfähig")]   // Rohr ist seit Modul 3 familienfähig (Leitungsserie)
    [InlineData("Gibtsnicht", "gibt es nicht")]
    public void Kategorie_MussFamilienfaehigSein(string kategorie, string teil)
    {
        var d = Mini(); d["kategorie"] = kategorie;
        var x = Einer(Def(d), "kategorie");
        Assert.Contains(teil, x.Meldung);
        Assert.Contains("Heizkoerper", x.Vorschlag);   // Liste der familienfähigen Klassen
    }

    [Fact]
    public void Kategorie_GrossKleinWirdAufDenKatalognamenGesetzt()
    {
        var d = Mini(); d["kategorie"] = "heizkoerper";
        var f = Def(d);
        Assert.True(f.Gueltig);
        Assert.Equal("Heizkoerper", f.Kategorie);
    }

    [Fact]
    public void Anschluss_MediumRichtungFluss()
    {
        var f = Def(Mini(anschluesse: """
            [{ "name": "a", "medium": "XY", "fluss": "rein", "lage": [0, 0, 0], "richtung": [0, 0, -1] },
             { "name": "b", "medium": "*", "fluss": "rein", "lage": [0, 0, 0], "richtung": [0, 0, 0] },
             { "name": "c", "medium": "HZ-VL", "fluss": "hin", "lage": [0, 0, 0], "richtung": [1, 0, 0], "farbe": "rot" },
             { "name": "d", "medium": "hz-rl", "fluss": "raus", "lage": [0, 0], "richtung": [1, 0, 0] }]
            """));
        Assert.Contains("Medien:", Einer(f, "anschluss «a».medium").Vorschlag);
        Assert.Contains("[0, 0, 0]", Einer(f, "anschluss «b».richtung").Meldung);
        Assert.Contains("rein, raus oder beide", Einer(f, "anschluss «c».fluss").Vorschlag);
        Assert.Contains("Unbekanntes Feld «farbe»", Einer(f, "anschluss «c»").Meldung);
        Assert.Contains("3 Zahlen", Einer(f, "anschluss «d».lage").Meldung);
        Assert.Equal("HZ-RL", f.Anschluesse.Single(x => x.Name == "d").Medium);   // Kennung wie im Katalog
    }

    [Fact]
    public void Anschluss_AlteKaelteUndSoleKennungen_WerdenGelesenUndUmgeschrieben()
    {
        // Kälte ist Teil der Heizung (Entscheid Christian 20.10.2026): KUE-VL → KAE-VL, SOLE-RL → HZ-RL-Glyk, nie ein Fehler
        var f = Def(Mini(anschluesse: """
            [{ "name": "a", "medium": "KUE-VL", "fluss": "rein", "lage": [0, 0, 0], "richtung": [0, 0, -1] },
             { "name": "b", "medium": "kue-rl", "fluss": "raus", "lage": [0, 0, 0], "richtung": [0, 1, 0] },
             { "name": "c", "medium": "SOLE-VL", "fluss": "rein", "lage": [0, 0, 0], "richtung": [1, 0, 0] },
             { "name": "d", "medium": "SOLE-RL", "fluss": "raus", "lage": [0, 0, 0], "richtung": [-1, 0, 0] },
             { "name": "e", "medium": "KAE_RL", "fluss": "raus", "lage": [0, 0, 0], "richtung": [0, -1, 0] }]
            """));
        Assert.True(f.Gueltig, string.Join(" | ", f.Fehler));
        Assert.Equal(["KAE-VL", "KAE-RL", "HZ-VL-Glyk", "HZ-RL-Glyk", "KAE-RL"], f.Anschluesse.Select(x => x.Medium).ToArray());
        // als Wert eines Medium-Parameters ebenso
        var g = Def(Mini("""[{ "name": "med", "art": "typ", "datentyp": "medium", "wert": "KUE-VL" }]""",
            """[{ "name": "A", "werte": {} }, { "name": "B", "werte": { "med": "SOLE-RL" } }]""",
            anschluesse: """[{ "name": "a", "medium": "med", "fluss": "rein", "lage": [0, 0, 0], "richtung": [0, 0, -1] }]"""));
        Assert.True(g.Gueltig, string.Join(" | ", g.Fehler));
        Assert.Equal("KAE-VL", FamilienAuswertung.Aufloesen(g, "A", null).Anschluesse[0].Medium);
        Assert.Equal("HZ-RL-Glyk", FamilienAuswertung.Aufloesen(g, "B", null).Anschluesse[0].Medium);
    }

    [Fact]
    public void Anschluss_MediumAusParameter()
    {
        var f = Def(Mini("""[{ "name": "med", "art": "typ", "datentyp": "medium", "wert": "KW" }]""",
            """[{ "name": "A", "werte": {} }, { "name": "B", "werte": { "med": "WW" } }]""",
            anschluesse: """[{ "name": "a", "medium": "med", "fluss": "rein", "lage": [0, 0, 0], "richtung": [0, 0, -1] }]"""));
        Assert.True(f.Gueltig, string.Join(" | ", f.Fehler));
        Assert.Equal("KW", FamilienAuswertung.Aufloesen(f, "A", null).Anschluesse[0].Medium);
        Assert.Equal("WW", FamilienAuswertung.Aufloesen(f, "B", null).Anschluesse[0].Medium);
        // ein unbekanntes Medium im Typ ist schon ein Fehler der Definition, als Exemplarwert ein Fehler der Exemplarwerte
        var g = Def(Mini("""[{ "name": "med", "art": "exemplar", "datentyp": "medium", "wert": "KW" }]""", """[{ "name": "C", "werte": { "med": "Bier" } }]"""));
        Assert.Contains("«Bier» gibt es nicht", Einer(g, "typ «C».werte.med").Meldung);
        Assert.Contains("«Bier» gibt es nicht", Assert.Single(FamilienAuswertung.ExemplarPruefen(g, new JsonObject { ["med"] = "Bier" })).Meldung);
    }

    [Fact]
    public void Koerper_PflichtfelderUndArten()
    {
        var f = Def(Mini(koerper: """
            [{ "id": "a", "art": "pyramide" },
             { "id": "b", "art": "quader", "von": [0, 0, 0] },
             { "id": "c", "art": "zylinder", "von": [0, 0, 0], "nach": [0, 0, 1], "r": 5, "bis": [1, 1, 1] },
             { "id": "d", "art": "extrusion", "ebene": "ab", "kontur": [[0, 0], [1, 0]], "von": 0, "bis": 1 },
             { "id": "e", "art": "rotation", "ebene": "xz", "kontur": [[1, 0], [2, 0], [2, 1]], "achse": { "punkt": [0, 0], "richtung": "w" } },
             { "id": "f", "art": "sweep", "profil": { "kreis": 5, "rechteck": [1, 2] }, "pfad": [[0, 0, 0]] },
             { "id": "g", "art": "kugel", "mitte": [0, 0, 0], "r": 5, "detailgrad": ["grob", "sehrfein"], "material": "gold" }]
            """));
        Assert.Contains("Erlaubt: quader", Einer(f, "koerper «a».art").Vorschlag);
        Assert.Contains("fehlt", Einer(f, "koerper «b».bis").Meldung);
        Assert.Contains("gehört nicht zu einem zylinder", Einer(f, "koerper «c»").Meldung);
        Assert.Contains("xy, xz oder yz", Einer(f, "koerper «d».ebene").Vorschlag);
        Assert.Contains("zwei Punkten ohne Wölbung", Einer(f, "koerper «d».kontur").Meldung);   // 2 Punkte nur mit Wölbung (Kreis)
        Assert.Contains("u oder v", Einer(f, "koerper «e».achse.richtung").Vorschlag);
        Assert.Contains("kreis und rechteck", Einer(f, "koerper «f».profil").Meldung);
        Assert.Contains("mindestens 2 Punkte", Einer(f, "koerper «f».pfad").Meldung);
        Assert.Contains("sehrfein", Einer(f, "koerper «g».detailgrad").Meldung);
        Assert.Contains("gold", Einer(f, "koerper «g».material").Meldung);
    }

    [Fact]
    public void Ziel_MussEigenschaftDerKategorieSein()
    {
        var f = Def(Mini("""
            [{ "name": "a", "art": "typ", "datentyp": "zahl", "einheit": "W", "ziel": "leistung" },
             { "name": "b", "art": "typ", "datentyp": "zahl", "ziel": "volumenstrom" },
             { "name": "c", "art": "typ", "datentyp": "text", "ziel": "leistung" },
             { "name": "d", "art": "typ", "datentyp": "zahl", "einheit": "kW", "ziel": "leistung" },
             { "name": "e", "art": "typ", "datentyp": "text", "ziel": "familientyp" }]
            """));
        Assert.DoesNotContain(f.Fehler, x => x.Stelle.StartsWith("parameter «a»"));
        Assert.Contains("hat keine Eigenschaft «volumenstrom»", Einer(f, "parameter «b».ziel").Meldung);
        Assert.Contains("ist zahl, der Parameter text", Einer(f, "parameter «c».ziel").Meldung);
        Assert.Contains("in W, der Parameter in kW", Einer(f, "parameter «d».ziel").Meldung);
        Assert.Contains("Familieneigenschaft", Einer(f, "parameter «e».ziel").Meldung);
    }

    [Fact]
    public void Kopf_UnbekanntesFeldFormatOhneTyp()
    {
        var d = Mini(typen: "[]"); d["format"] = 2; d["farbe"] = "rot";
        var f = Def(d);
        Assert.Contains("format muss 1 sein", Einer(f, "format").Meldung);
        Assert.Contains("Unbekanntes Feld", Einer(f, "farbe").Meldung);
        Assert.Contains("keinen Typ", Einer(f, "typen").Meldung);
    }

    [Fact]
    public void Quellen_FormDerBefundIds()
    {
        var f = Def(Mini("""[{ "name": "a", "art": "typ", "datentyp": "zahl", "quellen": ["0123456789ab#4", "kaputt"] }]"""));
        Assert.Contains("«\"kaputt\"»", Einer(f, "parameter[1].quellen").Meldung);
        Assert.Single(f.Fehler);
    }

    [Fact]
    public void Vorgabewert_PasstZumDatentyp()
    {
        var f = Def(Mini("""
            [{ "name": "a", "art": "typ", "datentyp": "laenge", "wert": "hoehe/2" },
             { "name": "b", "art": "typ", "datentyp": "ja_nein", "wert": "ja" },
             { "name": "c", "art": "typ", "datentyp": "medium", "wert": "Saft" },
             { "name": "d", "art": "typ", "datentyp": "laenge", "wert": "600" }]
            """));
        Assert.Contains("für eine Formel das Feld «formel»", Einer(f, "parameter «a».wert").Meldung);
        Assert.Contains("true oder false", Einer(f, "parameter «b».wert").Meldung);
        Assert.Contains("«Saft» gibt es nicht", Einer(f, "parameter «c».wert").Meldung);
        Assert.DoesNotContain(f.Fehler, x => x.Stelle.StartsWith("parameter «d»"));   // Zahl als Text wird gelesen
    }

    // ------------------------------------------------------------------ Einhängepunkt B4

    [Fact]
    public void Zusatzpruefung_WirdGefundenUndKannErgaenzenUndAblehnen()
    {
        Assert.Contains(FamilienPruefung.Zusatz, z => z is TestFamilienPruefung);
        var p = Neu();
        var d = Mini(); d["name"] = "Prüfhaken";
        var e = Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d }));
        Assert.Contains("Testprüfung gelaufen", e.Hinweise);
        var def = (JsonObject)Hole(p, "FAM-1").Eigenschaft("definition")!.Inhalt!;
        Assert.True(def["abdeckung"]!["hoehe"]!["fehlt"]!.GetValue<bool>());   // von der Zusatzprüfung ergänzt und so gespeichert

        var d2 = Mini(typen: """[{ "name": "verboten", "werte": {} }]"""); d2["name"] = "Prüfhaken";
        var n = Nein(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d2, ["familie"] = "FAM-1" }));
        Assert.Contains("Testfehler aus der Zusatzprüfung", n.Fehler);
        Assert.Contains("Typ umbenennen", n.Vorschlag);
    }

    [Fact]
    public void Fehlermeldung_ListetAlleFehlerMitVorschlag()
    {
        var d = Mini("""[{ "name": "a", "art": "x", "datentyp": "y" }]"""); d["kategorie"] = "Raum";
        var p = Neu();
        var n = Nein(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d }));
        Assert.Contains("3 Fehler", n.Fehler);
        Assert.Contains("(1) kategorie", n.Fehler);
        Assert.Contains("(2)", n.Vorschlag);
        Assert.Empty(p.Modell.DerKlasse("Familie"));
    }
}
