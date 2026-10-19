using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.HuelleTestHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Materialien der Hülle (Modul 1c, B2): Kennwerte nur mit Quelle, Bibliothek, Trockenlauf, Rückgängig.
/// Alle Zahlen sind frei gewählte Testwerte.</summary>
public class HuelleMaterialTests
{
    [Fact]
    public void MaterialAnlegen_MitQuelle_TraegtWertEinheitUndHerkunft()
    {
        var p = Neu();
        Ok(p.Ausfuehren("material_anlegen", new JsonObject
        {
            ["name"] = "Testmaterial A", ["kategorie"] = "Test",
            ["werte"] = new JsonObject
            {
                ["lambda"] = Kw(0.5, "Testquelle 1"),          // Testwert
                ["rohdichte"] = Kw(1000),                      // Testwert
                ["emission"] = Kw(0.9),                        // Testwert
            },
        }));
        var m = Hole(p, "MAT-1");
        Assert.Equal("Material", m.Klasse);
        Assert.Equal("Testmaterial A", m.Name);
        Assert.Equal("Test", m.Text("kategorie"));
        var l = m.Eigenschaft("lambda")!;
        Assert.Equal(0.5, l.AlsZahl());
        Assert.Equal("W/(m·K)", l.Einheit);
        Assert.Equal("Testquelle 1", l.Herkunft.Quelle);
        Assert.Equal("mensch", l.Herkunft.Art);
        Assert.Equal("kg/m³", m.Eigenschaft("rohdichte")!.Einheit);
        Assert.Equal(TestQuelle, m.Eigenschaft("rohdichte")!.Herkunft.Quelle);
        Assert.Equal("MAT-2", Ok(Material(p, "Testmaterial B", 0.04)).Aenderungen.Single().Kennzeichen);
    }

    [Fact]
    public void MaterialAnlegen_DurchKiTraegtHerkunftKi()
    {
        var p = Projekt.Anlegen(Kat, null, "Test KI", "Claude", "pc", "ki");
        Ok(Material(p, "Testmaterial A", 0.5));
        var h = Hole(p, "MAT-1").Eigenschaft("lambda")!.Herkunft;
        Assert.Equal("ki", h.Art);
        Assert.Equal("Claude", h.Akteur);
        Assert.Equal(TestQuelle, h.Quelle);
    }

    [Fact]
    public void MaterialAnlegen_OhneQuelleWirdAbgelehnt()
    {
        var p = Neu();
        // Wert ohne Quelle
        var a = Nein(p.Ausfuehren("material_anlegen", new JsonObject { ["name"] = "X", ["werte"] = new JsonObject { ["lambda"] = Kw(0.5, null) } }));
        Assert.Contains("Quelle", a.Fehler);
        // nackte Zahl
        Nein(p.Ausfuehren("material_anlegen", new JsonObject { ["name"] = "X", ["werte"] = new JsonObject { ["lambda"] = 0.5 } }));
        // leere Quelle
        Nein(p.Ausfuehren("material_anlegen", new JsonObject { ["name"] = "X", ["werte"] = new JsonObject { ["lambda"] = Kw(0.5, "  ") } }));
        // ein Wert von mehreren ohne Quelle: nichts wird angelegt
        Nein(p.Ausfuehren("material_anlegen", new JsonObject { ["name"] = "X", ["werte"] = new JsonObject { ["lambda"] = Kw(0.5), ["mu"] = Kw(10, null) } }));
        Assert.Empty(p.Modell.DerKlasse("Material"));
    }

    [Fact]
    public void MaterialAnlegen_UngueltigeWerteUndNamen()
    {
        var p = Neu();
        Nein(p.Ausfuehren("material_anlegen", new JsonObject { ["name"] = "X", ["werte"] = new JsonObject { ["lambda"] = Kw(0) } }));
        Nein(p.Ausfuehren("material_anlegen", new JsonObject { ["name"] = "X", ["werte"] = new JsonObject { ["lambda"] = Kw(-1) } }));
        Nein(p.Ausfuehren("material_anlegen", new JsonObject { ["name"] = "X", ["werte"] = new JsonObject { ["emission"] = Kw(1.5) } }));
        var unbekannt = Nein(p.Ausfuehren("material_anlegen", new JsonObject { ["name"] = "X", ["werte"] = new JsonObject { ["farbe"] = Kw(1) } }));
        Assert.Contains("lambda", unbekannt.Vorschlag);
        Nein(p.Ausfuehren("material_anlegen", new JsonObject { ["name"] = " " }));
        Ok(Material(p, "Testmaterial A", 0.5));
        var doppelt = Nein(Material(p, "testmaterial a", 0.6));      // Name eindeutig, Gross-/Kleinschreibung egal
        Assert.Contains("MAT-1", doppelt.Fehler);
        Assert.Single(p.Modell.DerKlasse("Material"));
    }

    [Fact]
    public void MaterialAnlegen_OhneLambdaGibtHinweis()
    {
        var p = Neu();
        var e = Ok(Material(p, "Testmaterial A", null));
        Assert.Contains(e.Hinweise, h => h.Contains("λ fehlt"));
    }

    [Fact]
    public void MaterialAendern_WertNurMitQuelle_NullEntfernt_ZweitesMalKeineAenderung()
    {
        var p = Neu();
        Ok(Material(p, "Testmaterial A", 0.5));

        var ohne = Nein(p.Ausfuehren("material_aendern", new JsonObject { ["material"] = "MAT-1", ["werte"] = new JsonObject { ["lambda"] = Kw(0.7, null) } }));
        Assert.Contains("Quelle", ohne.Fehler);
        Assert.Equal(0.5, Zahl(p, "MAT-1", "lambda"));

        Ok(p.Ausfuehren("material_aendern", new JsonObject { ["material"] = "Testmaterial A", ["werte"] = new JsonObject { ["lambda"] = Kw(0.7, "Neue Testquelle") } }));
        Assert.Equal(0.7, Zahl(p, "MAT-1", "lambda"));
        Assert.Equal("Neue Testquelle", Hole(p, "MAT-1").Eigenschaft("lambda")!.Herkunft.Quelle);

        var gleich = p.Ausfuehren("material_aendern", new JsonObject { ["material"] = "MAT-1", ["werte"] = new JsonObject { ["lambda"] = Kw(0.7, "Neue Testquelle") } });
        Assert.True(gleich.Angenommen);
        Assert.Null(gleich.PaketId);                                  // nichts zu speichern
        Assert.Contains(gleich.Hinweise, h => h.Contains("Keine Änderung"));

        Ok(p.Ausfuehren("material_aendern", new JsonObject { ["material"] = "MAT-1", ["name"] = "Umbenannt", ["kategorie"] = "Neu", ["werte"] = new JsonObject { ["lambda"] = null } }));
        Assert.Null(Hole(p, "MAT-1").Eigenschaft("lambda"));
        Assert.Equal("Umbenannt", Hole(p, "MAT-1").Name);
        Assert.Equal("Neu", Hole(p, "MAT-1").Text("kategorie"));

        Nein(p.Ausfuehren("material_aendern", new JsonObject { ["material"] = "MAT-99" }));
        Ok(Material(p, "Zweites", 0.3));
        Nein(p.Ausfuehren("material_aendern", new JsonObject { ["material"] = "MAT-2", ["name"] = "umbenannt" }));   // Name schon vergeben
    }

    [Fact]
    public void MaterialAnlegen_TrockenlaufAendertNichts_RueckgaengigStelltHer()
    {
        var p = Neu();
        var start = p.Modell.Pruefsumme();
        var tl = Ok(Material(p, "Testmaterial A", 0.5, trocken: true));
        Assert.True(tl.Trockenlauf);
        Assert.Equal(start, p.Modell.Pruefsumme());
        Assert.Null(p.Modell.Finde("MAT-1"));

        Ok(Material(p, "Testmaterial A", 0.5));
        Assert.NotNull(p.Modell.Finde("MAT-1"));
        Ok(p.Rueckgaengig());
        Assert.Null(p.Modell.Finde("MAT-1"));
        Ok(p.Wiederholen());
        Assert.Equal(0.5, Zahl(p, "MAT-1", "lambda"));
    }

    // ---------------------------------------------------------------- Bürobibliothek

    static string BibliothekMit(string materialienJson)
    {
        var ordner = Path.Combine(Path.GetTempPath(), "hlks_katalog_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(ordner);
        foreach (var f in Directory.GetFiles(Kat.Ordner, "*.json")) File.Copy(f, Path.Combine(ordner, Path.GetFileName(f)));
        File.WriteAllText(Path.Combine(ordner, "materialien.json"), materialienJson);
        return ordner;
    }

    static void Aufraeumen(string ordner) { try { Directory.Delete(ordner, true); } catch (IOException) { } }

    [Fact]
    public void MaterialUebernehmen_LeereBibliothek_KlareMeldung()
    {
        // die echte Bürobibliothek ist heute leer (entwurf/modul-1c.md, Quellen für Kennwerte)
        var p = Neu();
        var n = Nein(p.Ausfuehren("material_uebernehmen", new JsonObject { ["name"] = "Irgendwas" }));
        Assert.Contains("keine Materialien", n.Fehler);
        Assert.Contains("material_anlegen", n.Vorschlag);
        Assert.Empty(p.Modell.DerKlasse("Material"));
    }

    [Fact]
    public void MaterialUebernehmen_KopiertEintragMitQuelleInsProjekt()
    {
        var ordner = BibliothekMit("""
            { "materialien": [ { "name": "Testbibliotheksstoff", "kategorie": "Test",
                "werte": { "lambda": { "wert": 0.25, "quelle": "Testquelle Bibliothek" }, "rohdichte": { "wert": 500, "quelle": "Testquelle Bibliothek" } } } ] }
            """);
        try
        {
            var p = Projekt.Anlegen(Katalog.Laden(ordner), null, "Test", "T", "pc");
            var e = Ok(p.Ausfuehren("material_uebernehmen", new JsonObject { ["name"] = "testbibliotheksstoff" }));
            var m = Hole(p, "MAT-1");
            Assert.Equal("Testbibliotheksstoff", m.Name);
            Assert.Equal("Test", m.Text("kategorie"));
            Assert.Equal(0.25, m.Zahl("lambda"));
            Assert.Equal("Testquelle Bibliothek", m.Eigenschaft("lambda")!.Herkunft.Quelle);
            Assert.Equal("katalog", m.Eigenschaft("lambda")!.Herkunft.Art);
            Assert.Equal("W/(m·K)", m.Eigenschaft("lambda")!.Einheit);
            Assert.Contains(e.Hinweise, h => h.Contains("Bürobibliothek"));

            var doppelt = Nein(p.Ausfuehren("material_uebernehmen", new JsonObject { ["name"] = "Testbibliotheksstoff" }));   // schon im Projekt
            Assert.Contains("MAT-1", doppelt.Fehler);
            var unbekannt = Nein(p.Ausfuehren("material_uebernehmen", new JsonObject { ["name"] = "Gibtsnicht" }));
            Assert.Contains("Testbibliotheksstoff", unbekannt.Vorschlag);

            // Abfrage zeigt Projekt und Bibliothek
            var abfrage = (JsonObject)Tuer.Abfrage(p, "materialien");
            Assert.Equal(1, abfrage["bibliothek"]!["anzahl"]!.GetValue<int>());
            Assert.Equal("MAT-1", abfrage["projekt"]![0]!["kennzeichen"]!.GetValue<string>());
        }
        finally { Aufraeumen(ordner); }
    }

    [Fact]
    public void MaterialUebernehmen_BibliothekEintragOhneQuelleIstUngueltig()
    {
        var ordner = BibliothekMit("""{ "materialien": [ { "name": "Ohne Beleg", "werte": { "lambda": { "wert": 0.25 } } } ] }""");
        try
        {
            var p = Projekt.Anlegen(Katalog.Laden(ordner), null, "Test", "T", "pc");
            var n = Nein(p.Ausfuehren("material_uebernehmen", new JsonObject { ["name"] = "Ohne Beleg" }));
            Assert.Contains("Quelle", n.Fehler);
            Assert.Empty(p.Modell.DerKlasse("Material"));
            // die Abfrage bricht nicht ab, sondern meldet den Fehler der Bibliothek
            var abfrage = (JsonObject)Tuer.Abfrage(p, "materialien");
            Assert.Contains("Quelle", abfrage["bibliothek"]!["fehler"]!.GetValue<string>());
        }
        finally { Aufraeumen(ordner); }
    }

    [Fact]
    public void MaterialienAbfrage_ZeigtWerteQuellenUndWasFehlt()
    {
        var p = Neu();
        Ok(Material(p, "Testmaterial A", 0.5));
        Ok(Material(p, "Ohne Lambda", null));
        var a = (JsonObject)Tuer.Abfrage(p, "materialien");
        var projekt = a["projekt"]!.AsArray();
        Assert.Equal(2, projekt.Count);
        Assert.Equal(0.5, projekt[0]!["werte"]!["lambda"]!["wert"]!.GetValue<double>());
        Assert.Equal(TestQuelle, projekt[0]!["werte"]!["lambda"]!["quelle"]!.GetValue<string>());
        Assert.Equal("lambda", projekt[1]!["fehlt"]![0]!.GetValue<string>());
        Assert.NotNull(a["bibliothek"]!["hinweis"]);              // leer: Hinweis, woher die Kennwerte kommen
    }
}
