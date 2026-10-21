using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.SchemaansichtHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3, Teil B3: Befehle schema_lage und schema_anordnen (Trockenlauf, Rückgängig, Ablehnungen), Abfrage
/// «schema», nicht platzierte Elemente und Prüfung P17, Schema-Ansicht an den übrigen Stellen des Kerns.</summary>
public class SchemaansichtBefehleTests
{
    static JsonObject? LagenVon(Projekt p) => p.Modell.Finde(Ansicht)!.Eigenschaft("lagen")?.Inhalt as JsonObject;

    static BefehlsErgebnis HandLage(Projekt p, string element, double x, double y, bool trocken = false) =>
        p.Ausfuehren("schema_lage", Par(("ansicht", Ansicht), ("element", element), ("x", x), ("y", y)), trocken);

    // ------------------------------------------------------------------ schema_lage

    [Fact]
    public void SchemaLage_SetztHandLage_UndLinieWirdOrthogonalNeuGefuehrt()
    {
        var p = Testhaus();
        Ok(HandLage(p, "WT-O", 80, 140));
        var s = Plan(p);
        var k = Knoten(s, "WT-O");
        Assert.True(k.Manuell);
        Assert.Equal((80.0, 140.0), (k.X, k.Y));
        Assert.Equal("frei", k.Ausrichtung);
        Assert.Equal("(40,120) (80,120) (80,140)", Zug(LinieMit(s, "R-6")));   // V-1 auf der Stranglinie, gleiche Zeile: zuerst waagrecht
        var lagen = LagenVon(p)!;
        var id = Hole(p, "WT-O").Id;
        Assert.Equal(80, lagen[id]!["x"]!.GetValue<double>());
        Assert.Equal(140, lagen[id]!["y"]!.GetValue<double>());
        Assert.True(lagen[id]!["manuell"]!.GetValue<bool>());
        Assert.Single(lagen);                                   // nur Hand-Lagen werden gespeichert, keine automatischen
        Assert.Equal((55.0, 75.625), Lage(s, "WT-E"));          // übrige Lagen unverändert (B8d: auf dem Boden der Zeile)
    }

    [Fact]
    public void SchemaLage_BleibtBeiModellaenderung()
    {
        var p = Testhaus();
        Ok(HandLage(p, "WT-O", 80, 140));
        Ok(p.Ausfuehren("element_aendern", Par(("element", "WT-O"), ("name", "Waschtisch Bad OG"), ("geometrie", new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([1600, 600, 2900]) }))));
        Ok(p.Ausfuehren("element_anlegen", Par(("klasse", "Ausgussbecken"), ("kennzeichen", "AB-O"), ("geschoss", "OG"))));
        Ok(p.Ausfuehren("element_loeschen", Par(("element", "DU-E"))));
        var s = Plan(p);
        Assert.Equal((80.0, 140.0), Lage(s, "WT-O"));
        Assert.True(Knoten(s, "WT-O").Manuell);
    }

    [Fact]
    public void SchemaLage_Zuruecksetzen_GiltWiederDieAutomatischeAnordnung()
    {
        var p = Testhaus();
        Ok(HandLage(p, "WT-O", 80, 140));
        Ok(HandLage(p, "TS-3", 100, 10));
        Ok(p.Ausfuehren("schema_lage", Par(("ansicht", Ansicht), ("element", "WT-O"), ("zuruecksetzen", true))));
        var s = Plan(p);
        Assert.Equal((55.0, 125.625), Lage(s, "WT-O"));
        Assert.False(Knoten(s, "WT-O").Manuell);
        Assert.True(Knoten(s, "TS-3").Manuell);
        Ok(p.Ausfuehren("schema_lage", Par(("ansicht", Ansicht), ("element", "TS-3"), ("zuruecksetzen", true))));
        Assert.Null(LagenVon(p));                                // leer = Eigenschaft entfernt
        var nochmal = Ok(p.Ausfuehren("schema_lage", Par(("ansicht", Ansicht), ("element", "TS-3"), ("zuruecksetzen", true))));
        Assert.Null(nochmal.PaketId);                            // nichts zu tun
        Assert.Contains(nochmal.Hinweise, h => h.Contains("automatische Anordnung"));
    }

    [Fact]
    public void SchemaLage_GleicheLage_KeineAenderung()
    {
        var p = Testhaus();
        Ok(HandLage(p, "WT-O", 80, 140));
        var e = Ok(HandLage(p, "WT-O", 80, 140));
        Assert.Null(e.PaketId);
    }

    [Fact]
    public void SchemaLage_Trockenlauf_AendertNichts()
    {
        var p = Testhaus();
        var stand = p.Modell.Pruefsumme();
        var e = Ok(HandLage(p, "WT-O", 80, 140, trocken: true));
        Assert.True(e.Trockenlauf);
        Assert.Contains(e.Aenderungen, a => a.Kennzeichen == Ansicht && a.Art == "geaendert");
        Assert.Equal(stand, p.Modell.Pruefsumme());
        Assert.Null(LagenVon(p));
        Assert.Equal((55.0, 125.625), Lage(Plan(p), "WT-O"));
    }

    [Fact]
    public void SchemaLage_Rueckgaengig_UndWiederholen()
    {
        var p = Testhaus();
        var stand = p.Modell.Pruefsumme();
        Ok(HandLage(p, "WT-O", 80, 140));
        Ok(p.Rueckgaengig());
        Assert.Equal(stand, p.Modell.Pruefsumme());
        Assert.Equal((55.0, 125.625), Lage(Plan(p), "WT-O"));
        Ok(p.Wiederholen());
        Assert.Equal((80.0, 140.0), Lage(Plan(p), "WT-O"));
    }

    [Fact]
    public void SchemaLage_AnsichtIstKeinSchema_AbgelehntMitVorschlag()
    {
        var p = Testhaus();
        Ok(p.Ausfuehren("element_anlegen", Par(("klasse", "Ansicht"), ("kennzeichen", "AN-GR-EG"), ("geschoss", "EG"), ("eigenschaften", new JsonObject { ["art"] = "grundriss" }))));
        var e = Nein(p.Ausfuehren("schema_lage", Par(("ansicht", "AN-GR-EG"), ("element", "WT-O"), ("x", 1), ("y", 1))));
        Assert.Contains("kein Schema", e.Fehler);
        Assert.Contains(Ansicht, e.Vorschlag);
        Nein(p.Ausfuehren("schema_lage", Par(("ansicht", "WT-O"), ("element", "WT-O"), ("x", 1), ("y", 1))));
    }

    [Fact]
    public void SchemaLage_ElementNichtImSchema_AbgelehntMitVorschlag()
    {
        var p = Testhaus(mitHeizung: true);
        // Leitung: folgt ihren Endknoten
        var leitung = Nein(HandLage(p, "R-2", 10, 10));
        Assert.Contains("TS-1", leitung.Vorschlag);
        Assert.Contains("TS-2", leitung.Vorschlag);
        // Bogen ebenso
        Assert.Contains("V-1", Nein(HandLage(p, "BO-1", 10, 10)).Vorschlag);
        // kein Netzelement
        Assert.Contains("Netzelemente", Nein(HandLage(p, "EG", 10, 10)).Vorschlag);
        // anderes Gewerk
        Assert.Contains("gewerke", Nein(HandLage(p, "HK-E", 10, 10)).Vorschlag);
        // x, y fehlen
        Nein(p.Ausfuehren("schema_lage", Par(("ansicht", Ansicht), ("element", "WT-O"))));
        Assert.Null(LagenVon(p));
    }

    // ------------------------------------------------------------------ schema_anordnen

    [Fact]
    public void SchemaAnordnen_Alle_EntferntAlleHandLagen()
    {
        var p = Testhaus();
        Ok(HandLage(p, "WT-O", 80, 140));
        Ok(HandLage(p, "TS-3", 100, 10));
        var trocken = Ok(p.Ausfuehren("schema_anordnen", Par(("ansicht", Ansicht), ("alle", true)), trockenlauf: true));
        Assert.True(trocken.Trockenlauf);
        Assert.Equal(2, LagenVon(p)!.Count);
        var e = Ok(p.Ausfuehren("schema_anordnen", Par(("ansicht", Ansicht), ("alle", true))));
        Assert.NotNull(e.PaketId);
        Assert.Null(LagenVon(p));
        var s = Plan(p);
        Assert.Equal((55.0, 125.625), Lage(s, "WT-O"));
        Assert.Equal((75.0, 20.0), Lage(s, "TS-3"));
        Assert.DoesNotContain(s.Graph.Knoten, k => k.Manuell);
        Ok(p.Rueckgaengig());
        Assert.Equal(2, LagenVon(p)!.Count);
        Assert.Equal((80.0, 140.0), Lage(Plan(p), "WT-O"));
    }

    [Fact]
    public void SchemaAnordnen_RaeumtLagenGeloeschterUndAusgeblendeterElementeAuf()
    {
        var p = Testhaus();
        Ok(HandLage(p, "WT-O", 80, 140));
        Ok(HandLage(p, "WC-O", 130, 140));
        Ok(HandLage(p, "V-1", 45, 125));
        var wc = Hole(p, "WC-O").Id; var v1 = Hole(p, "V-1").Id;
        Ok(p.Ausfuehren("element_loeschen", Par(("element", "WC-O"))));
        Ok(p.Ausfuehren("element_aendern", Par(("element", Ansicht), ("eigenschaften", new JsonObject { ["klassenAusblenden"] = new JsonArray("Ventil") }))));
        var s = Plan(p);
        Assert.Equal(new[] { v1, wc }.Order(StringComparer.Ordinal), s.VerwaisteLagen.Order(StringComparer.Ordinal));
        Assert.Contains(s.Hinweise, h => h.Contains("schema_anordnen"));
        var e = Ok(p.Ausfuehren("schema_anordnen", Par(("ansicht", Ansicht))));
        Assert.Contains(e.Hinweise, h => h.Contains("2 Hand-Lage(n) entfernt") && h.Contains("V-1"));
        var lagen = LagenVon(p)!;
        Assert.Single(lagen);
        Assert.NotNull(lagen[Hole(p, "WT-O").Id]);
        Assert.Empty(Plan(p).VerwaisteLagen);
        // nichts mehr aufzuräumen
        Assert.Null(Ok(p.Ausfuehren("schema_anordnen", Par(("ansicht", Ansicht)))).PaketId);
    }

    [Fact]
    public void SchemaAnordnen_OhneHandLagen_KeineAenderung()
    {
        var p = Testhaus();
        var e = Ok(p.Ausfuehren("schema_anordnen", Par(("ansicht", Ansicht), ("alle", true))));
        Assert.Null(e.PaketId);
        Assert.Contains(e.Hinweise, h => h.Contains("keine Hand-Lagen"));
        Nein(p.Ausfuehren("schema_anordnen", Par(("ansicht", "EG"))));
    }

    // ------------------------------------------------------------------ Abfrage «schema»

    [Fact]
    public void Abfrage_Schema_LiefertKnotenLinienSpaltenZeilen()
    {
        var p = Testhaus();
        Ok(HandLage(p, "WT-O", 80, 140));
        var o = (JsonObject)Tuer.Abfrage(p, "schema", Par(("ansicht", Ansicht)));
        Assert.Equal(Ansicht, o["ansicht"]!.GetValue<string>());
        Assert.Equal("strangschema", o["schemaart"]!.GetValue<string>());
        Assert.Equal(p.Modell.Pruefsumme(), o["stand"]!.GetValue<string>());
        Assert.Equal(3, o["zeilen"]!.AsArray().Count);
        Assert.Equal(2, o["spalten"]!.AsArray().Count);
        Assert.Equal("KW-1", o["spalten"]![0]!["kennzeichen"]!.GetValue<string>());
        Assert.Equal(40, o["spalten"]![0]!["linie"]!.GetValue<double>());
        var knoten = o["knoten"]!.AsArray();
        Assert.Equal(11, knoten.Count);
        foreach (var k in knoten)
            foreach (var feld in new[] { "element", "kennzeichen", "klasse", "x", "y", "manuell", "platziert", "symbol" })
                Assert.True(k!.AsObject().ContainsKey(feld), $"{feld} fehlt");
        var wto = knoten.Single(k => k!["kennzeichen"]!.GetValue<string>() == "WT-O")!;
        Assert.Equal(Hole(p, "WT-O").Id, wto["element"]!.GetValue<string>());
        Assert.True(wto["manuell"]!.GetValue<bool>());
        Assert.Equal("sinnbild", wto["symbol"]!.GetValue<string>());   // Waschtisch: HKAP-Sinnbild (B8b)
        var wco = knoten.Single(k => k!["kennzeichen"]!.GetValue<string>() == "WC-O")!;
        Assert.False(wco["platziert"]!.GetValue<bool>());
        Assert.Equal("heizsymbol", knoten.Single(k => k!["kennzeichen"]!.GetValue<string>() == "V-1")!["symbol"]!.GetValue<string>());   // Absperrventil: Heizungssymbol der HKAP (Modul 5 A-S 2; vorher «ventil»)
        var linien = o["linien"]!.AsArray();
        Assert.Equal(10, linien.Count);
        var bogen = linien.Single(l => l!["kennzeichen"]!.AsArray().Any(x => x!.GetValue<string>() == "BO-1"))!;
        Assert.Equal(["R-6", "BO-1", "R-7"], bogen["kennzeichen"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray());
        Assert.Equal(new[] { "R-6", "BO-1", "R-7" }.Select(kz => Hole(p, kz).Id).ToArray(), bogen["elemente"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray());
        Assert.Equal(Hole(p, "V-1").Id, bogen["von"]!.GetValue<string>());
        Assert.Equal(Hole(p, "WT-O").Id, bogen["nach"]!.GetValue<string>());
        Assert.Equal("KW", bogen["medium"]!.GetValue<string>());
        Assert.Equal(Gruen, bogen["farbe"]!.GetValue<string>());
        Assert.Equal(["NW 15"], bogen["groessen"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray());
        Assert.Equal(3, bogen["punkte"]!.AsArray().Count);
        Assert.Contains(o["hinweise"]!.AsArray(), h => h!.GetValue<string>().StartsWith("Nur im Schema") && h.GetValue<string>().Contains("WC-O"));
    }

    [Fact]
    public void Abfrage_Schema_OffeneEndenUndFehler()
    {
        var p = Testhaus();
        Ok(p.Ausfuehren("element_anlegen", Par(("klasse", "Rohr"), ("kennzeichen", "R-99"), ("teilVon", "KW-1"), ("geschoss", "EG"))));
        var o = (JsonObject)Tuer.Abfrage(p, "schema", Par(("ansicht", Ansicht)));
        var frei = o["linien"]!.AsArray().Single(l => l!["kennzeichen"]![0]!.GetValue<string>() == "R-99")!;
        Assert.Null(frei["von"]);
        Assert.Null(frei["nach"]);
        Assert.Equal(2, frei["offen"]!.AsArray().Count);
        Assert.Equal("offen", frei["offen"]![0]!["grund"]!.GetValue<string>());
        var f = Assert.Throws<HlksFehler>(() => Tuer.Abfrage(p, "schema", Par(("ansicht", "EG"))));
        Assert.NotNull(f.Vorschlag);
        Assert.Contains(Abfragen.Liste, a => a.Name == "schema");
        Assert.Contains(p.Befehle.Alle, b => b.Name == "schema_lage");
        Assert.Contains(p.Befehle.Alle, b => b.Name == "schema_anordnen");
    }

    // ------------------------------------------------------------------ nicht platzierte Elemente, P17

    [Fact]
    public void NichtPlatziert_AnlegenUndVerbindenOhneGeometrie()
    {
        var p = Testhaus();
        Ok(p.Ausfuehren("element_anlegen", Par(("klasse", "Ventil"), ("kennzeichen", "V-7"), ("teilVon", "KW-2"), ("geschoss", "OG"))));
        Ok(p.Ausfuehren("element_anlegen", Par(("klasse", "Bogen"), ("kennzeichen", "BO-7"), ("teilVon", "KW-2"), ("geschoss", "OG"))));
        Ok(p.Ausfuehren("element_anlegen", Par(("klasse", "Rohr"), ("kennzeichen", "R-70"), ("teilVon", "KW-2"), ("geschoss", "OG"))));
        Ok(p.Ausfuehren("element_anlegen", Par(("klasse", "Spueltisch"), ("kennzeichen", "SP-O"), ("geschoss", "OG"))));
        Ok(p.Ausfuehren("verbinden", Par(("von", "V-7.aus"), ("nach", "BO-7.a"))));
        Ok(p.Ausfuehren("verbinden", Par(("von", "BO-7.b"), ("nach", "R-70.anfang"))));
        Ok(p.Ausfuehren("verbinden", Par(("von", "R-70.ende"), ("nach", "SP-O.kw"))));
        var s = Plan(p);
        Assert.False(Knoten(s, "V-7").Platziert);
        Assert.False(Knoten(s, "SP-O").Platziert);
        var l = LinieMit(s, "BO-7");
        Assert.Equal(["BO-7", "R-70"], l.Elemente.Select(e => e.Kennzeichen));
        Assert.Equal("V-7", Ende(l.Von));
        Assert.Equal("SP-O", Ende(l.Nach));
        Assert.Equal("KW-2", Knoten(s, "SP-O").Spalte!.Titel);
        Assert.Equal("OG", Knoten(s, "SP-O").Zeile!.Titel);
    }

    [Fact]
    public void P17_NurNetzelementeOhneGeometrie_AlsHinweis()
    {
        var p = Testhaus();
        var p17 = Pruefungen.Alle(p.Modell, Kat).Where(b => b.Code == "P17").ToList();
        Assert.Equal(["R-14", "WC-O"], p17.Select(b => b.Element).Order(StringComparer.Ordinal).ToArray());
        Assert.All(p17, b =>
        {
            Assert.Equal("hinweis", b.Schwere);
            Assert.Contains("nur im Schema", b.Text);
            Assert.Contains("element_aendern mit geometrie", b.Vorschlag);
            Assert.Contains("familie_platzieren", b.Vorschlag);
        });
        Assert.Equal("Nur im Schema (nicht platziert)", Pruefungen.Codes["P17"]);
    }

    [Fact]
    public void P17_NachPlatzierenBehoben()
    {
        var p = Testhaus();
        var e = Ok(p.Ausfuehren("element_aendern", Par(("element", "WC-O"), ("geometrie", new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([3500, 500, 2900]) }))));
        Assert.Contains(e.BehobeneBefunde, b => b.Code == "P17" && b.Element == "WC-O");
        Assert.True(Knoten(Plan(p), "WC-O").Platziert);
        var neu = Ok(p.Ausfuehren("element_anlegen", Par(("klasse", "WC"), ("kennzeichen", "WC-8"), ("geschoss", "EG"))));
        Assert.Contains(neu.NeueBefunde, b => b.Code == "P17" && b.Element == "WC-8");
    }

    // ------------------------------------------------------------------ Schema-Ansicht an den übrigen Stellen

    [Fact]
    public void SchemaAnsicht_OhneGeschossUndSchnittlinie_UeberallGueltig()
    {
        var p = Testhaus();
        var e = Ok(p.Ausfuehren("element_anlegen", Par(("klasse", "Ansicht"), ("kennzeichen", "AN-S2"), ("eigenschaften", new JsonObject { ["art"] = "schema" }))));
        Assert.DoesNotContain(e.NeueBefunde, b => b.Element == "AN-S2");
        var g = Grafik.Plan(p, "AN-S2");                         // ohne schemaart = Strangschema, ohne gewerke = alles
        Assert.Equal("schema", g.Art);
        Assert.Equal(1, g.Massstab);
        Assert.NotNull(g.Begrenzung);
        Assert.NotNull(Darstellung.Modell(p));
        Assert.NotNull(Darstellung.Elemente(p, [Hole(p, "AN-S2").Id]));
        var a = new Abfragen(p);
        Assert.Contains("AN-S2 (schema)", a.ProjektUeberblick().ToJsonString());
        Assert.NotNull(a.ElementLesen("AN-S2"));
        Assert.NotNull(Ableitungen.Strangliste(p, null));
        Assert.NotNull(Ableitungen.Mengen(p, null, null));
        // Grundriss-Kontext gibt es für ein Schema nicht – Grafik.Plan leitet vorher weiter
        Assert.Throws<HlksFehler>(() => Grafik.Kontext(p, Hole(p, "AN-S2")));
        // ein Schema mit Hand-Lage im Modell bleibt gültig
        Ok(p.Ausfuehren("schema_lage", Par(("ansicht", "AN-S2"), ("element", "TS-1"), ("x", 0), ("y", 0))));
        Assert.DoesNotContain(Pruefungen.Alle(p.Modell, Kat), b => b.Element == "AN-S2" && b.Schwere != "hinweis");
    }

    [Fact]
    public void Prinzipschema_WieStrangschemaMitHinweis()
    {
        var p = Testhaus();
        Ok(p.Ausfuehren("element_aendern", Par(("element", Ansicht), ("eigenschaften", new JsonObject { ["schemaart"] = "prinzipschema" }))));
        var s = Plan(p);
        Assert.Equal("prinzipschema", s.Schemaart);
        Assert.Contains(s.Hinweise, h => h.Contains("prinzipschema"));
        Assert.Equal((40.0, 20.0), Lage(s, "TS-1"));
    }
}
