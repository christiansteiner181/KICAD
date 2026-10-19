using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3, Auftrag B9b (Entscheide Christian Steiner 19.10.2026): die eine gemeinsame Funktion für die Grösse der Rohre
/// (<see cref="Rohrgroessen"/>). Abwasser (Schmutz- und Regenwasser) wird mit dem Aussendurchmesser bezeichnet («PE-S 110», Grösse
/// 110, ohne Ø, ohne NW/DN); alle anderen Rohre mit NW bzw. DN («CNS NW 20»). Nie da aus DN gerechnet und nie DN aus da; fehlt die
/// Zahl, gibt es keine Grösse und einen Hinweis. Alle Werte FIKTIV (Testrohrserie da = DN + 5; Leitungen mit frei gewähltem da).</summary>
public class RohrgroesseTests
{
    // ------------------------------------------------------------------ Hilfen

    /// <summary>Projekt mit EG, Systemen KW, HZ-VL, SW, RW, ZUL, den Serien FAM-1 «PE-S» (Testabwasserserie, DN 50|75|100|125 mit da
    /// 60|85|110|135 FIKTIV), FAM-2 (dieselbe ohne Kürzel) und FAM-3 «CNS» (Testrohrserie DN 15…50, da = DN + 5) und der Ansicht AN-EG.</summary>
    static Projekt Haus()
    {
        var p = Neu();
        foreach (var (kz, typ) in new[] { ("KW", "Trinkwasser kalt"), ("HZ-VL", "Heizung Vorlauf"), ("SW", "Schmutzwasser"), ("RW", "Regenwasser"), ("ZUL", "Zuluft") })
            Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = kz, ["eigenschaften"] = new JsonObject { ["systemtyp"] = typ } }));
        JsonObject OhneFormteile(JsonObject d) { var l = d["leitung"]!.AsObject(); l.Remove("formteile"); l.Remove("regeln"); return d; }
        var mit = OhneFormteile(Familie("test_abwasserserie")); mit["leitung"]!["kuerzel"] = "PE-S";
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = mit }));          // FAM-1
        var ohne = OhneFormteile(Familie("test_abwasserserie")); ohne["id"] = "fam-test-abwasserserie-ohne"; ohne["name"] = "Testabwasserserie ohne Kürzel (fiktiv)";
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = ohne }));         // FAM-2
        var cns = OhneFormteile(Familie("test_rohrserie")); cns["leitung"]!["kuerzel"] = "CNS";
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = cns }));          // FAM-3
        return p;
    }

    static void Rohr(Projekt p, string kz, string system, string? serie, string? typ, double? dn, double? da, string? material = null)
    {
        var eig = new JsonObject();
        if (typ is not null) eig["familientyp"] = typ;
        if (dn is not null) eig["dn"] = dn;
        if (da is not null) eig["da"] = da;
        if (material is not null) eig["material"] = material;
        var o = new JsonObject
        {
            ["klasse"] = "Rohr", ["kennzeichen"] = kz, ["system"] = system, ["geschoss"] = "EG", ["eigenschaften"] = eig,
            ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Pts([0, p.Modell.DerKlasse("Rohr").Count() * 100.0, 500], [3000, p.Modell.DerKlasse("Rohr").Count() * 100.0, 500]) },
        };
        if (serie is not null) o["bezuege"] = new JsonObject { ["familie"] = serie };
        Ok(p.Ausfuehren("element_anlegen", o));
    }

    static string? Text(Projekt p, string kz) => Leitungsbezeichnung.Text(p, Hole(p, "AN-EG"), Hole(p, kz));
    static Rohrgroesse G(Projekt p, string kz) => Rohrgroessen.Von(p, Hole(p, "AN-EG"), Hole(p, kz));
    static void Einstellung(Projekt p, string art) =>
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "AN-EG", ["eigenschaften"] = new JsonObject { ["rohrbezeichnung"] = art } }));

    // ------------------------------------------------------------------ Abwasser: Beschriftung

    [Fact]
    public void Abwasser_MitKuerzel_KuerzelUndAussendurchmesser()
    {
        var p = Haus();
        Rohr(p, "R-1", "SW", "FAM-1", "DN 100", null, null);
        Assert.Equal("PE-S 110", Text(p, "R-1"));      // da 110 aus dem Typ «DN 100» der Serie (FIKTIV), nicht aus DN gerechnet
        var g = G(p, "R-1");
        Assert.True(g.Abwasser);
        Assert.Equal("da", g.Art);
        Assert.Equal("PE-S", g.Kuerzel);
        Assert.Contains("Serie", g.Quelle);
    }

    [Fact]
    public void Abwasser_OhneKuerzel_NurAussendurchmesser()
    {
        var p = Haus();
        Rohr(p, "R-1", "SW", "FAM-2", "DN 100", null, null);
        Assert.Equal("110", Text(p, "R-1"));
        Assert.Equal("110", G(p, "R-1").Groesse);
    }

    [Fact]
    public void Abwasser_Regenwasser_ebenfalls_UndNurDieEntwaesserung()
    {
        var p = Haus();
        Rohr(p, "R-RW", "RW", "FAM-1", "DN 100", null, null);
        Assert.Equal("PE-S 110", Text(p, "R-RW"));
        var k = p.Katalog;
        Assert.Equal(new[] { "RW", "SW" }, Rohrgroessen.AbwasserMedien(k).Order().ToArray());   // Schmutz- und Regenwasser, mehr kennt der Katalog nicht
        Assert.True(Rohrgroessen.IstAbwasser(p, Hole(p, "R-RW")));
        Rohr(p, "R-KW", "KW", "FAM-1", "DN 100", null, null);
        Rohr(p, "R-HZ", "HZ-VL", "FAM-1", "DN 100", null, null);
        Assert.False(Rohrgroessen.IstAbwasser(p, Hole(p, "R-KW")));
        Assert.False(Rohrgroessen.IstAbwasser(p, Hole(p, "R-HZ")));
    }

    [Fact]
    public void Abwasser_OhneSerie_AussendurchmesserAusLeitungDa_SerieGehtVor()
    {
        var p = Haus();
        Rohr(p, "R-1", "SW", null, null, 100, 90);                  // dn 100, da 90: die Leitung nennt beides, keines wird gerechnet
        Assert.Equal("90", Text(p, "R-1"));
        Assert.Equal("Leitung.da", G(p, "R-1").Quelle);
        Rohr(p, "R-2", "SW", "FAM-1", "DN 100", null, 75);          // Serie 110 und Leitung.da 75: die Serie gilt
        Assert.Equal("PE-S 110", Text(p, "R-2"));
        Assert.Equal(110, Rohrgroessen.Aussendurchmesser(p, Hole(p, "R-2"))!.Value.Da);
        Assert.Equal(90, Rohrgroessen.Aussendurchmesser(p, Hole(p, "R-1"))!.Value.Da);
        Rohr(p, "R-3", "SW", null, null, 100, 22.5);
        Assert.Equal("22.5", Text(p, "R-3"));                       // eine Nachkommastelle mit Punkt
    }

    [Fact]
    public void Abwasser_OhneDa_KeineGroesse_KeinDnAlsErsatz_MitHinweis()
    {
        var p = Haus();
        Rohr(p, "R-1", "SW", null, null, 100, null);                // nur DN 100
        Assert.Null(Text(p, "R-1"));
        var g = G(p, "R-1");
        Assert.Null(g.Groesse);
        Assert.Null(g.Wert);
        Assert.Contains("ohne Aussendurchmesser", g.Hinweis);
        Assert.Contains("kein DN als Ersatz", g.Hinweis);
        Assert.Contains(Rohrgroessen.Hinweise(p, [Hole(p, "R-1")]), h => h.Contains("1 Abwasserleitung ohne Aussendurchmesser (da)"));
    }

    [Fact]
    public void Abwasser_EinstellungNwDnGiltNichtMehr()
    {
        var p = Haus();
        Rohr(p, "R-1", "SW", "FAM-1", "DN 100", null, null);
        Einstellung(p, "DN");
        Assert.Equal("PE-S 110", Text(p, "R-1"));
    }

    [Fact]
    public void Abwasser_KuerzelNurWennDieDateiEsNennt_ManuellesMaterialZaehltNicht()
    {
        var p = Haus();
        Rohr(p, "R-1", "SW", null, null, 100, 110, material: "PE-S");   // von Hand eingetragen
        Assert.Equal("110", Text(p, "R-1"));
        Assert.Null(Rohrgroessen.MaterialKuerzel(Hole(p, "R-1")));
        // dasselbe Material aus einer IFC-Datei (Herkunft ifc): jetzt nennt es die Datei
        var w = Hole(p, "R-1").Eigenschaft("material")!;
        w.Herkunft = w.Herkunft with { Art = "ifc" };
        Assert.Equal("PE-S", Rohrgroessen.MaterialKuerzel(Hole(p, "R-1")));
        Assert.Equal("PE-S 110", Text(p, "R-1"));
        // ein Material, das kein Kürzel ist, bleibt ohne
        Hole(p, "R-1").Eigenschaft("material")!.Inhalt = JsonValue.Create("Polyethylen");
        Assert.Equal("110", Text(p, "R-1"));
    }

    // ------------------------------------------------------------------ Wasser und Heizung: NW/DN

    [Fact]
    public void WasserUndHeizung_NwOderDn_MitUndOhneKuerzel_NieAussendurchmesser()
    {
        var p = Haus();
        Rohr(p, "R-KW", "KW", "FAM-3", "DN 20", null, null);
        Rohr(p, "R-HZ", "HZ-VL", "FAM-2", "DN 50", null, null);
        Rohr(p, "R-OS", "KW", null, null, 32, null);
        Assert.Equal("CNS NW 20", Text(p, "R-KW"));     // Entscheid Christian 19.10.2026: «CNS NW 20», nicht mehr «CNS Ø25»
        Assert.Equal("NW 50", Text(p, "R-HZ"));
        Assert.Equal("NW 32", Text(p, "R-OS"));
        Einstellung(p, "DN");
        Assert.Equal("CNS DN 20", Text(p, "R-KW"));
        Assert.Equal("DN 50", Text(p, "R-HZ"));
        Assert.Equal("DN 32", Text(p, "R-OS"));
        Assert.DoesNotContain("Ø", Text(p, "R-KW"));
        Assert.Equal("DN 20", G(p, "R-KW").Groesse);
        // Quelle der Zahl: Typ der Serie bzw. Leitung.dn
        Assert.Contains("Serie", G(p, "R-KW").Quelle);
        Assert.Equal("Leitung.dn", G(p, "R-OS").Quelle);
    }

    [Fact]
    public void Wasser_OhneDn_KeineGroesse_KeinAussendurchmesserAlsErsatz_MitHinweis()
    {
        var p = Haus();
        Rohr(p, "R-1", "KW", null, null, null, 22);               // nur da 22 (Aussendurchmesser)
        Assert.Null(Text(p, "R-1"));
        var g = G(p, "R-1");
        Assert.Null(g.Groesse);
        Assert.Contains("ohne Nennweite", g.Hinweis);
        Assert.Contains("kein Ø als Ersatz", g.Hinweis);
        Assert.Contains(Rohrgroessen.Hinweise(p, [Hole(p, "R-1")]), h => h.Contains("1 Rohr ohne Nennweite (DN)"));
    }

    // ------------------------------------------------------------------ Exporte und Listen: überall dieselbe Grösse

    static Projekt MitLeitungen()
    {
        var p = Haus();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Strang", ["kennzeichen"] = "SW-1", ["teilVon"] = "SW", ["eigenschaften"] = new JsonObject { ["art"] = "fallstrang" } }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Strang", ["kennzeichen"] = "KW-1", ["teilVon"] = "KW", ["eigenschaften"] = new JsonObject { ["art"] = "steigstrang" } }));
        Rohr(p, "R-A1", "SW", "FAM-1", "DN 100", null, null);
        Rohr(p, "R-A2", "SW", null, null, 50, 56);
        Rohr(p, "R-A3", "SW", null, null, 100, null);              // ohne da
        Rohr(p, "R-W1", "KW", "FAM-3", "DN 20", null, null);
        Rohr(p, "R-W2", "KW", null, null, 32, null);
        foreach (var (kz, st) in new[] { ("R-A1", "SW-1"), ("R-A2", "SW-1"), ("R-A3", "SW-1"), ("R-W1", "KW-1"), ("R-W2", "KW-1") })
            Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = kz, ["teilVon"] = st }));
        return p;
    }

    [Fact]
    public void Export_Leitungslaengen_UndMateriallisteUndHkapBlock_GleicheGroesse()
    {
        var p = MitLeitungen();
        var l = Ableitungen.Leitungslaengen(p).Select(x => x.Groesse).Order().ToArray();
        Assert.Equal(new[] { "110", "56", "NW 20", "NW 32", "ohne Grösse" }.Order().ToArray(), l);
        // Materialliste: dieselben Texte, dazu der Hinweis auf die Abwasserleitung ohne da
        var ml = Ableitungen.Materialliste(p);
        Assert.Equal(l, ml["leitungen"]!.AsArray().Select(z => z!["groesse"]!.GetValue<string>()).Order().ToArray());
        Assert.Contains("Abwasserleitung ohne Aussendurchmesser", ml["hinweis"]!.GetValue<string>());
        // HKAP «straenge»: laengen je Serie und Grösse aus derselben Funktion (Testattrappe, nie die echte HKAP)
        var link = new HkapVerknuepfung("PJ-TEST", null, null, null, null);
        var sw = HkapStraenge.Block(p.Modell, p.Katalog, p, link, Hole(p, "SW-1"));
        Assert.Equal(new[] { "110", "56", "ohne Grösse" }.Order().ToArray(), sw["laengen"]!.AsArray().Select(z => z!["groesse"]!.GetValue<string>()).Order().ToArray());
        var kw = HkapStraenge.Block(p.Modell, p.Katalog, p, link, Hole(p, "KW-1"));
        Assert.Equal(new[] { "NW 20", "NW 32" }, kw["laengen"]!.AsArray().Select(z => z!["groesse"]!.GetValue<string>()).Order().ToArray());
    }

    [Fact]
    public void Export_MengenUndStrangliste_Groessenspalte()
    {
        var p = MitLeitungen();
        var m = Ableitungen.Mengen(p);
        var gruppen = m["leitungen"]!.AsArray().Select(g => g!["groesse"]!.GetValue<string>()).ToArray();
        Assert.Contains("110", gruppen); Assert.Contains("56", gruppen); Assert.Contains("NW 20", gruppen); Assert.Contains("NW 32", gruppen);
        Assert.DoesNotContain(m["leitungen"]!.AsArray(), g => g!.AsObject().ContainsKey("dn"));   // die Spalte heisst jetzt groesse
        Assert.Contains("Abwasserleitung", m["hinweis"]!.GetValue<string>());
        var sl = Ableitungen.Strangliste(p)["zeilen"]!.AsArray();
        string Gr(string strang) => sl.Single(z => z!["strang"]!.GetValue<string>() == strang)!["groesse"]!.GetValue<string>();
        Assert.Equal("56–110", Gr("SW-1"));       // Abwasser nur die Zahlen
        Assert.Equal("NW 20–32", Gr("KW-1"));
    }

    [Fact]
    public void Export_ListenNachProjekteinstellungDn()
    {
        var p = MitLeitungen();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Projektinformation", ["kennzeichen"] = "PI-1", ["eigenschaften"] = new JsonObject { ["rohrbezeichnung"] = "DN" } }));
        Assert.Contains("DN 20", Ableitungen.Leitungslaengen(p).Select(x => x.Groesse));
        Assert.Contains("110", Ableitungen.Leitungslaengen(p).Select(x => x.Groesse));   // Abwasser unverändert
        var kw = Ableitungen.Strangliste(p)["zeilen"]!.AsArray().Single(z => z!["strang"]!.GetValue<string>() == "KW-1")!;
        Assert.Equal("DN 20–32", kw["groesse"]!.GetValue<string>());
    }

    [Fact]
    public void Abfragen_StrangTextUndElementLesen_MitGroesseUndHerkunft()
    {
        var p = MitLeitungen();
        var a = new Abfragen(p);
        var text = a.StrangText("SW-1");
        Assert.Matches(@"R-A1 Rohr 110\b", text);
        Assert.Matches(@"R-A2 Rohr 56\b", text);
        Assert.DoesNotContain("R-A3 Rohr DN", text);
        var kwText = a.StrangText("KW-1");
        Assert.Matches(@"R-W2 Rohr NW 32\b", kwText);
        var abg = (JsonObject)a.ElementLesen("R-A1")["abgeleitet"]!;
        Assert.Equal("110", abg["rohrgroesse"]!.GetValue<string>());
        Assert.Contains("Serie", abg["rohrgroesseQuelle"]!.GetValue<string>());
        var ohne = (JsonObject)a.ElementLesen("R-A3")["abgeleitet"]!;
        Assert.Null(ohne["rohrgroesse"]);
        Assert.Contains("da", ohne["rohrgroesseHinweis"]!.GetValue<string>());
    }

    [Fact]
    public void Abfrage_Leitungsserie_BeschriftungJeArt()
    {
        var p = Haus();
        var s = (JsonObject)Tuer.Abfrage(p, "leitungsserie", new JsonObject { ["familie"] = "FAM-3" });
        var g = s["groessen"]!.AsArray().Single(x => x!["typ"]!.GetValue<string>() == "DN 20")!;
        Assert.Equal("CNS NW 20", g["beschriftung"]!.GetValue<string>());        // Rohre ausser Abwasser
        Assert.Equal("CNS 25", g["beschriftungAbwasser"]!.GetValue<string>());   // Abwasser: Aussendurchmesser (FIKTIV da = DN + 5)
    }

    // ------------------------------------------------------------------ Schema

    [Fact]
    public void Schema_AbwasserTexteAussendurchmesser_HinweisOhneDa()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        Ok(p.Ausfuehren("schema_anlegen", new JsonObject { ["vorlage"] = "abwasser" }));
        var s = Schemaplan.Rechnen(p, "AN-SCHEMA-ABWASSER");
        // das Beispielprojekt nennt für Abwasser nur DN → keine Grössentexte, dafür ein Hinweis (kein DN als Ersatz)
        Assert.All(s.Graph.Linien, l => Assert.Empty(l.Groessen));
        Assert.Contains(s.Hinweise, h => h.Contains("Abwasserleitungen ohne Aussendurchmesser (da)"));
        // mit da an den Leitungen erscheint der Aussendurchmesser, ohne NW/DN und ohne Ø
        foreach (var r in p.Modell.DerKlasse("Rohr").Where(r => p.Modell.Finde(r.System)?.Kennzeichen == "SW").ToList())
            Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = r.Kennzeichen, ["eigenschaften"] = new JsonObject { ["da"] = 110 } }));
        var s2 = Schemaplan.Rechnen(p, "AN-SCHEMA-ABWASSER");
        Assert.NotEmpty(s2.Graph.Linien.SelectMany(l => l.Groessen));
        Assert.All(s2.Graph.Linien.SelectMany(l => l.Groessen), t => Assert.Equal("110", t));
        Assert.DoesNotContain(s2.Hinweise, h => h.Contains("Abwasserleitungen ohne Aussendurchmesser"));
    }
}
