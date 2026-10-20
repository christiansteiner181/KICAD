using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;
using static Hlks.Kern.Tests.RoutingHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Nachtrag Modul 3 «freie Grösse für Kanalserien» (entwurf/freie-groesse-kanal.md, Entscheid Christian 20.10.2026). Alle Werte
/// FIKTIV aus den Testfamilien tests/daten/familien/test_kanalfrei (Raster 500x300, 400x200, 300x200, 200x200; frei aus der Vorlage
/// 400x200, Grenzen 145 … 2500 mm, Schritt 1; aussen = b + 2·s, s = 1), test_kanalbogen_frei (L = (ri + breite/2)·tan(w/2) + lv,
/// ri = 115, lv = 35), test_kanal_t_frei (l = b_c/2 + 100, lc = b_a/2 + 100) und test_kanalreduktion_frei
/// (l = max(breite1 − breite2, hoehe1 − hoehe2) + 100). Handrechnungen stehen bei den Prüfungen.</summary>
public class FreieGroesseKanalTests
{
    const string SerieId = "fam-test-kanalfrei";
    const string SerieName = "Testkanal frei rechteckig (fiktiv)";

    static readonly string[] Familien4 = ["test_kanalfrei", "test_kanalbogen_frei", "test_kanal_t_frei", "test_kanalreduktion_frei"];

    /// <summary>Projekt mit Geschoss EG, System ZUL (Zuluft), der freien Testkanalserie (FAM-1) und ihren Formteilen (FAM-2 … FAM-4).</summary>
    static Projekt MitFrei(IProjektSpeicher? speicher = null)
    {
        var p = Neu(speicher: speicher);
        MitSystem(p, "ZUL", "Zuluft");
        foreach (var f in Familien4) Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Datei(f) }));
        return p;
    }

    static Leitungsserie Frei(Projekt p, string id = SerieId) => Leitungsserie.Hole(p.Modell, p.Katalog, id);

    static Leitungsgroesse R(double b, double h) => Leitungsgroesse.Rechteck(b, h);

    static JsonObject G(double b, double h) => new() { ["b"] = b, ["h"] = h };

    static BefehlsErgebnis Kanal(Projekt p, JsonNode groesse, double[][] punkte, string serie = SerieId, bool trocken = false) =>
        p.Ausfuehren("leitung_zeichnen", new JsonObject { ["serie"] = serie, ["groesse"] = groesse.DeepClone(), ["punkte"] = Pts(punkte), ["system"] = "ZUL" }, trocken);

    /// <summary>Definition aus einer Testfamilie mit neuer id und neuem Namen, abgeändert und mit familie_entwurf angelegt.</summary>
    static BefehlsErgebnis Variante(Projekt p, string datei, string id, Action<JsonObject> aendern)
    {
        var d = Familie(datei);
        d["id"] = id; d["name"] = $"{d["name"]!.GetValue<string>()} – Variante {id}";
        aendern(d);
        return p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d });
    }

    static JsonObject Leitung(JsonObject d) => d["leitung"]!.AsObject();

    static FamilieDef Lesen(JsonObject d) => FamilieDef.Lesen(d, Kat);

    static void HatFehler(FamilieDef d, string stelle, string text) =>
        Assert.True(d.Fehler.Any(f => f.Stelle.StartsWith(stelle, StringComparison.Ordinal) && f.Meldung.Contains(text, StringComparison.Ordinal)),
            $"Fehler «{stelle}: …{text}…» fehlt; Fehler: " + string.Join("; ", d.Fehler));

    // ------------------------------------------------------------------ Grösse: Text, Kurzform

    [Fact]
    public void Leitungsgroesse_KurzUndAusText()
    {
        Assert.Equal("430 × 270", R(430, 270).Kurz);
        Assert.Equal("Ø 250", Leitungsgroesse.Rund(250).Kurz);
        Assert.Equal("DN 20", Leitungsgroesse.Rohr(20).Kurz);
        Assert.Equal(R(430, 270), Leitungsgroesse.AusText("430x270"));
        Assert.Equal(R(430, 270), Leitungsgroesse.AusText(" 430 × 270 mm"));
        Assert.Equal(R(430.5, 270), Leitungsgroesse.AusText("430,5X270"));
        Assert.Equal(Leitungsgroesse.Rund(250), Leitungsgroesse.AusText("Ø 250"));
        Assert.Null(Leitungsgroesse.AusText("DN 20"));
        Assert.Null(Leitungsgroesse.AusText("430x"));
        Assert.Null(Leitungsgroesse.AusText("0x270"));
    }

    // ------------------------------------------------------------------ Definition (Familienformat)

    [Fact]
    public void Definition_TestfamilienGueltig_FreiGelesen_GrenzenOhneQuelleIstHinweis()
    {
        foreach (var f in Familien4)
        {
            var d = Lesen(Familie(f));
            Assert.True(d.Gueltig, f + ": " + string.Join("; ", d.Fehler));
        }
        var s = Lesen(Familie("test_kanalfrei"));
        var fr = s.Leitung!.Frei!;
        Assert.Equal("400x200", fr.Vorlage);
        Assert.Equal(1, fr.Schritt);
        Assert.Equal(new FamGrenze(145, 2500), fr.Grenzen["b"]);
        Assert.Equal(new FamGrenze(145, 2500), fr.Grenzen["h"]);
        Assert.Equal("400x200/300x200", s.Leitung.Formteile["reduktion"].Vorlage);
        Assert.Null(s.Leitung.Formteile["bogen"].Vorlage);
        // Grenzen nur aus der Quelle: ohne frei.quellen ein Hinweis, mit Quelle keiner
        Assert.Contains(s.Hinweise, h => h.Stelle == "leitung.frei.grenzen" && h.Meldung.Contains("keine Quelle"));
        var mitQuelle = Familie("test_kanalfrei");
        Leitung(mitQuelle)["frei"]!["quellen"] = new JsonArray("0123456789ab#1");
        var dq = Lesen(mitQuelle);
        Assert.True(dq.Gueltig, string.Join("; ", dq.Fehler));
        Assert.DoesNotContain(dq.Hinweise, h => h.Stelle == "leitung.frei.grenzen");
        // Form der Befund-Id wird geprüft wie überall
        Leitung(mitQuelle)["frei"]!["quellen"] = new JsonArray("S. 19");
        HatFehler(Lesen(mitQuelle), "leitung.frei.quellen", "nicht die Form");
    }

    [Fact]
    public void Schema_KenntFreiUndFormteilVorlage()
    {
        var schema = JsonNode.Parse(File.ReadAllText(IfcHilfe.Testdaten(Path.Combine("..", "..", "schema", "hlks-familie.schema.json"))))!;
        var frei = schema["properties"]!["leitung"]!["properties"]!["frei"]!.AsObject();
        Assert.Equal(["vorlage"], frei["required"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Equal(["vorlage", "grenzen", "schritt", "quellen"], frei["properties"]!.AsObject().Select(x => x.Key));
        Assert.Equal("integer", frei["properties"]!["schritt"]!["type"]!.GetValue<string>());
        Assert.Contains("nie Rohre", frei["description"]!.GetValue<string>());
        Assert.NotNull(schema["$defs"]!["formteilWahl"]!["properties"]!["vorlage"]);
        Assert.DoesNotContain("frei", schema["properties"]!["leitung"]!["required"]!.AsArray().Select(x => x!.GetValue<string>()));
    }

    [Fact]
    public void Definition_RohrserieMitFrei_IstFehler_AuchBeimEntwurf()
    {
        var d = Familie("test_rohrserie");
        var typ = d["typen"]![0]!["name"]!.GetValue<string>();
        Leitung(d)["frei"] = new JsonObject { ["vorlage"] = typ };
        var def = Lesen(d);
        HatFehler(def, "leitung.frei", "nur bei Kanalserien");
        Assert.Contains("DN", def.Fehler.First(f => f.Stelle == "leitung.frei").Meldung);
        var p = MitSerie(false);
        var nein = Nein(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d }));
        Assert.Contains("nur bei Kanalserien", nein.Fehler);
    }

    [Fact]
    public void Definition_FreiFehler_MitStelleUndVorschlag()
    {
        JsonObject Mit(Action<JsonObject> frei)
        {
            var d = Familie("test_kanalfrei");
            frei(Leitung(d)["frei"]!.AsObject());
            return d;
        }
        HatFehler(Lesen(Mit(f => f["vorlage"] = "999x999")), "leitung.frei.vorlage", "Typ «999x999» gibt es nicht");
        HatFehler(Lesen(Mit(f => f.Remove("vorlage"))), "leitung.frei.vorlage", "vorlage fehlt");
        HatFehler(Lesen(Mit(f => f["schritt"] = 0.5)), "leitung.frei.schritt", "ganze Zahl ≥ 1");
        HatFehler(Lesen(Mit(f => f["schritt"] = 0)), "leitung.frei.schritt", "ganze Zahl ≥ 1");
        HatFehler(Lesen(Mit(f => f["grenzen"]!["b"] = new JsonObject { ["min"] = 300, ["max"] = 200 })), "leitung.frei.grenzen.b", "grösser als max");
        HatFehler(Lesen(Mit(f => f["grenzen"]!["b"] = new JsonObject { ["min"] = -5 })), "leitung.frei.grenzen.b.min", "keine Zahl > 0");
        HatFehler(Lesen(Mit(f => f["grenzen"]!["d"] = new JsonObject { ["min"] = 100 })), "leitung.frei.grenzen.d", "Grössenschlüssel «d»");
        HatFehler(Lesen(Mit(f => f["grenzen"]!["b"] = new JsonObject { ["von"] = 100 })), "leitung.frei.grenzen.b", "Unbekanntes Feld «von»");
        HatFehler(Lesen(Mit(f => f["raster"] = 5)), "leitung.frei", "Unbekanntes Feld «raster»");
        // Grössenparameter mit Formel: nicht ersetzbar
        var mitFormel = Familie("test_kanalfrei");
        var pb = mitFormel["parameter"]!.AsArray().First(x => x!["name"]!.GetValue<string>() == "h")!.AsObject();
        pb["formel"] = "b / 2";
        foreach (var t in mitFormel["typen"]!.AsArray()) t!["werte"]!.AsObject().Remove("h");
        HatFehler(Lesen(mitFormel), "leitung.frei", "«h» (h) hat eine Formel");
        // formteile.<art>.vorlage ohne frei: Hinweis; leer: Fehler
        var ohneFrei = Familie("test_kanalfrei");
        Leitung(ohneFrei).Remove("frei");
        var dh = Lesen(ohneFrei);
        Assert.True(dh.Gueltig, string.Join("; ", dh.Fehler));
        Assert.Contains(dh.Hinweise, h => h.Stelle == "leitung.formteile.reduktion.vorlage");
        Leitung(ohneFrei)["formteile"]!["bogen"]!["vorlage"] = "";
        HatFehler(Lesen(ohneFrei), "leitung.formteile.bogen.vorlage", "Typname");
        // jede Fehlermeldung hat einen Vorschlag
        Assert.All(Lesen(Mit(f => f["schritt"] = 0.5)).Fehler, f => Assert.False(string.IsNullOrWhiteSpace(f.Vorschlag)));
    }

    [Fact]
    public void Definition_KanalRund_Frei_TypFuerSchrittUndProbe()
    {
        var d = new JsonObject
        {
            ["format"] = 1, ["id"] = "fam-test-rundfrei", ["name"] = "Testrundkanal frei (fiktiv)", ["kategorie"] = "Kanal",
            ["parameter"] = new JsonArray(new JsonObject { ["name"] = "d", ["art"] = "typ", ["datentyp"] = "laenge", ["beschreibung"] = "Durchmesser (FIKTIV)" }),
            ["typen"] = new JsonArray(new JsonObject { ["name"] = "Ø 200", ["werte"] = new JsonObject { ["d"] = 200 } }, new JsonObject { ["name"] = "Ø 250", ["werte"] = new JsonObject { ["d"] = 250 } }),
            ["koerper"] = new JsonArray(), ["anschluesse"] = new JsonArray(),
            ["leitung"] = new JsonObject
            {
                ["querschnitt"] = "rund", ["groesse"] = new JsonObject { ["d"] = "d" }, ["aussen"] = "d",
                ["frei"] = new JsonObject { ["vorlage"] = "Ø 250", ["grenzen"] = new JsonObject { ["d"] = new JsonObject { ["min"] = 80, ["max"] = 1250 } }, ["schritt"] = 5 },
            },
        };
        var p = Neu();
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d }));
        var s = Frei(p, "fam-test-rundfrei");
        Assert.Equal("frei d (80 … 1250 mm, Schritt 5 mm)", s.FreiText);
        var v = s.TypFuer(Leitungsgroesse.Rund(315))!;
        Assert.True(v.Frei);
        Assert.Equal("Ø 250", v.Typ);
        Assert.Equal(315, v.Aussen);
        Assert.False(s.TypFuer(Leitungsgroesse.Rund(200))!.Frei);
        Assert.Null(s.TypFuer(Leitungsgroesse.Rund(312), out var grund));
        Assert.Contains("nicht im Schritt", grund);
        Assert.Contains("gerundet Ø 310 mm", grund);
        Assert.Null(s.TypFuer(Leitungsgroesse.Rund(60), out var grund2));
        Assert.Contains("d = 60 mm, erlaubt 80 … 1250 mm", grund2);
        Assert.Equal(Leitungsgroesse.Rund(665), s.Probe());   // (80 + 1250)/2 = 665, im Schritt 5
        // Zahl als Grösse = d (Kanal rund)
        Assert.Equal(Leitungsgroesse.Rund(315), RoutingBefehle.Groesse(s, JsonValue.Create(315)).Groesse);
    }

    // ------------------------------------------------------------------ Auswertung mit ersetzten Typwerten

    [Fact]
    public void Auswertung_TypwerteErsetzen_UndPruefen()
    {
        var def = Lesen(Familie("test_kanalfrei"));
        var a = FamilienAuswertung.Aufloesen(def, "400x200", null, new Dictionary<string, double> { ["b"] = 430, ["h"] = 270 });
        Assert.True(a.Gueltig, string.Join("; ", a.Fehler));
        Assert.Equal(430, a.Leitung!.Groesse["b"]);
        Assert.Equal(270, a.Leitung.Groesse["h"]);
        Assert.Equal(432, a.Leitung.Aussen);                      // aussen = b + 2·s = 430 + 2
        Assert.Equal(1, a.Leitung.Wand);
        // ohne Ersatz die Werte des Typs; Zwischenspeicher je Ersatz
        Assert.Equal(402, FamilienAuswertung.Aufloesen(def, "400x200", null).Leitung!.Aussen);
        Assert.Same(a, FamilienAuswertung.Aufloesen(def, "400x200", null, new Dictionary<string, double> { ["h"] = 270, ["b"] = 430 }));
        Assert.Equal(404, FamilienAuswertung.Aufloesen(def, "400x200", null, new Dictionary<string, double> { ["s"] = 2 }).Leitung!.Aussen);   // auch Vorgabewert
        // nur Typparameter ohne Formel mit Zahl
        Assert.Contains(FamilienAuswertung.Aufloesen(def, "400x200", null, new Dictionary<string, double> { ["x"] = 1 }).Fehler, f => f.Stelle == "typwerte.x");
        var bogen = Lesen(Familie("test_kanalbogen_frei"));
        Assert.Contains(FamilienAuswertung.Aufloesen(bogen, "400x200", null, new Dictionary<string, double> { ["breite"] = 430 }).Fehler, f => f.Meldung.Contains("kein Typparameter"));
        Assert.Contains(FamilienAuswertung.Aufloesen(bogen, "400x200", null, new Dictionary<string, double> { ["r"] = 1 }).Fehler, f => f.Stelle == "typwerte.r");
        var ri = FamilienAuswertung.Aufloesen(bogen, "400x200", null, new Dictionary<string, double> { ["ri"] = 200 });
        Assert.Equal(200 + 200 + 35, ri.Wert("l")!.Value.Zahl, 6);   // (200 + 400/2)·tan 45° + 35
    }

    // ------------------------------------------------------------------ Serie: TypFuer, Texte

    [Fact]
    public void TypFuer_Raster_Frei_Grenzen_Schritt()
    {
        var p = MitFrei();
        var s = Frei(p);
        Assert.Equal("frei b × h (145 … 2500 mm, Schritt 1 mm)", s.FreiText);
        Assert.Equal("frei b × h (145 … 2500 mm, Schritt 1 mm), Vorschlag: 500 × 300, 400 × 200, 300 × 200, 200 × 200", s.GroessenText);
        // Rastertyp hat Vorrang
        var r = s.TypFuer(R(400, 200))!;
        Assert.False(r.Frei);
        Assert.Equal("400x200", r.Typ);
        Assert.Equal(402, r.Aussen);
        // freie Grösse: Typ = Vorlage, Masse aus der Vorlage mit ersetzten Grössenparametern
        var v = s.TypFuer(R(430, 270))!;
        Assert.True(v.Frei);
        Assert.Equal("400x200", v.Typ);
        Assert.Equal(R(430, 270), v.Groesse);
        Assert.Equal(432, v.Aussen);
        Assert.Equal(1, v.Wand);
        Assert.Equal("430 × 270 (frei, Vorlage 400x200)", v.Text);
        // Grenzen
        Assert.Null(s.TypFuer(R(120, 270), out var g1));
        Assert.Contains("ausserhalb der Grenzen", g1);
        Assert.Contains("b = 120 mm, erlaubt 145 … 2500 mm", g1);
        Assert.Null(s.TypFuer(R(430, 2600), out var g2));
        Assert.Contains("h = 2600 mm", g2);
        Assert.NotNull(s.TypFuer(R(145, 2500)));   // Grenzen gehören dazu
        // Schritt: gerundet vorgeschlagen, nie still
        Assert.Null(s.TypFuer(R(430.5, 270), out var g3));
        Assert.Contains("nicht im Schritt", g3);
        Assert.Contains("gerundet 431 × 270 mm", g3);
        // falsche Art
        Assert.Null(s.TypFuer(Leitungsgroesse.Rund(300), out var g4));
        Assert.Contains("passt nicht zur Serie", g4);
        // Fehler mit Vorschlag: gerundete Grösse innerhalb der Grenzen und die nächsten Normgrössen
        var f = Assert.Throws<HlksFehler>(() => s.Typ(R(120, 270)));
        Assert.Contains("Vorschlag: 145 × 270", f.Vorschlag);
        Assert.Contains("Normgrösse 200 × 200, 300 × 200, 400 × 200", f.Vorschlag);
        Assert.Contains("frei b × h (145 … 2500 mm, Schritt 1 mm)", f.Vorschlag);
        // Serie ohne frei: unverändert
        Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Datei("test_kanalserie") }));
        var ohne = Frei(p, "fam-test-kanalserie");
        Assert.Null(ohne.FreiText);
        Assert.Null(ohne.TypFuer(R(430, 270), out var g5));
        Assert.Equal("Die Serie " + ohne.Bezeichnung + " hat keine Grösse 430 × 270 mm.", g5);
        Assert.Equal("500 × 300 mm, 400 × 200 mm, 300 × 200 mm, 200 × 200 mm", ohne.GroessenText);
    }

    // ------------------------------------------------------------------ Formteilwahl

    [Fact]
    public void Formteilwahl_JeArt_FreieGroesse_ExemplarwerteUndEinbaulaengenAusDerFormel()
    {
        var p = MitFrei();
        var m = p.Modell; var k = p.Katalog;
        var s = Frei(p);
        var g = R(430, 270);
        // Bogen 90°: L = (115 + 430/2)·tan 45° + 35 = 365 mm
        var bo = s.Formteil(m, k, "bogen", g, winkel: 90);
        Assert.True(bo.Ok, bo.Fehler);
        Assert.True(bo.Frei);
        Assert.Equal("400x200", bo.Typ);   // Vorlage fehlt = erster Typ
        Assert.Equal(90, bo.Exemplar!["winkel"]!.GetValue<double>());
        Assert.Equal(430, bo.Exemplar["breite"]!.GetValue<double>());
        Assert.Equal(270, bo.Exemplar["hoehe"]!.GetValue<double>());
        Etwa(365, bo.Geometrie!.Einbaulaenge("a"));
        Etwa(365, bo.Geometrie.Einbaulaenge("b"));
        Assert.Equal(R(430, 270), bo.Geometrie.Anschluss("b").Groesse);
        // 45°: L = 330·tan 22.5° + 35 = 171.690 mm
        var bo45 = s.Formteil(m, k, "bogen", g, winkel: 45);
        Etwa(330 * Tan(22.5) + 35, bo45.Geometrie!.Einbaulaenge("a"));
        Etwa(171.690, bo45.Geometrie.Einbaulaenge("a"), 1e-3);
        // T-Stück Durchgang 430 × 270, Abzweig 250 × 200: l = 250/2 + 100 = 225, lc = 430/2 + 100 = 315
        var t = s.Formteil(m, k, "tstueck", g, R(250, 200));
        Assert.True(t.Ok, t.Fehler);
        Assert.True(t.Frei);
        Assert.Equal("400x200", t.Typ);
        Assert.Equal(new[] { 430.0, 270, 250, 200 }, new[] { "b_a", "h_a", "b_c", "h_c" }.Select(n => t.Exemplar![n]!.GetValue<double>()));
        Etwa(225, t.Geometrie!.Einbaulaenge("a")); Etwa(225, t.Geometrie.Einbaulaenge("b")); Etwa(315, t.Geometrie.Einbaulaenge("c"));
        Assert.Equal(R(250, 200), t.Geometrie.Anschluss("c").Groesse);
        Assert.Equal("430 × 270/250 × 200", t.GroessenText);
        // Reduktion 430 × 270 → 400 × 200 (Vorlage der Serie «400x200/300x200»): l = max(30, 70) + 100 = 170
        var r = s.Formteil(m, k, "reduktion", g, R(400, 200));
        Assert.True(r.Ok, r.Fehler);
        Assert.True(r.Frei);
        Assert.Equal("400x200/300x200", r.Typ);
        Assert.False(r.Vertauscht);
        Etwa(170, r.Geometrie!.Einbaulaenge("b"));
        Assert.Equal(new[] { 430.0, 270, 400, 200 }, new[] { "breite1", "hoehe1", "breite2", "hoehe2" }.Select(n => r.Exemplar![n]!.GetValue<double>()));
        var r2 = s.Formteil(m, k, "reduktion", R(400, 200), g);   // klein → gross: geordnet
        Assert.True(r2.Vertauscht);
        Assert.Equal(g, r2.Groesse1);
        // Normgrössen: Typ wie bisher, keine Grössen als Exemplarwerte
        var bn = s.Formteil(m, k, "bogen", R(400, 200), winkel: 90);
        Assert.False(bn.Frei);
        Assert.Equal("400x200", bn.Typ);
        Assert.Equal(["winkel"], bn.Exemplar!.Select(x => x.Key));
        Etwa(350, bn.Geometrie!.Einbaulaenge("a"));   // (115 + 200)·1 + 35
        var tn = s.Formteil(m, k, "tstueck", R(400, 200), R(300, 200));
        Assert.False(tn.Frei);
        Assert.Equal("400x200/300x200", tn.Typ);
        var rn = s.Formteil(m, k, "reduktion", R(500, 300), R(400, 200));
        Assert.False(rn.Frei);
        Assert.Equal("500x300/400x200", rn.Typ);
        // Normgrössen-Paar ohne Typ (400x200 → 200x200): bei freier Serie aus der Vorlage gerechnet
        var rv = s.Formteil(m, k, "reduktion", R(400, 200), R(200, 200));
        Assert.True(rv.Ok, rv.Fehler);
        Assert.True(rv.Frei);
        Etwa(300, rv.Geometrie!.Einbaulaenge("b"));   // max(200, 0) + 100
        // keine Formteil-Familie für die Art
        Assert.Contains("nennt keine Formteil-Familie", s.Formteil(m, k, "reinigungsoeffnung", g).Fehler);
    }

    [Fact]
    public void Formteilwahl_NichtEinstellbar_AbgelehntMitGrundUndVorschlag()
    {
        var p = MitFrei();
        var m = p.Modell; var k = p.Katalog;
        var g = R(430, 270);
        // (a) Formteil-Familie mit Grössen als Typparameter (fam-test-kanalbogen: b, h)
        Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Datei("test_kanalbogen") }));
        Ok(Variante(p, "test_kanalfrei", "fam-test-frei-a", d => Leitung(d)["formteile"]!["bogen"]!["familie"] = "fam-test-kanalbogen"));
        var a = Frei(p, "fam-test-frei-a").Formteil(m, k, "bogen", g, winkel: 90);
        Assert.False(a.Ok);
        Assert.Contains("Kein Typ der Formteil-Familie", a.Fehler);
        Assert.Contains("a.b = «b» ist ein Typparameter", a.Fehler);
        Assert.Contains("Exemplarparameter ohne Formel", a.Vorschlag);
        // die Normgrösse geht weiter über den Typ
        Assert.True(Frei(p, "fam-test-frei-a").Formteil(m, k, "bogen", R(400, 200), winkel: 90).Ok);
        // (b) T-Stück, dessen Abzweig dieselben Parameter wie der Durchgang hat
        Ok(Variante(p, "test_kanal_t_frei", "fam-test-t-gleich", d =>
        {
            var c = d["anschluesse"]!.AsArray().First(x => x!["name"]!.GetValue<string>() == "c")!;
            c["b"] = "b_a"; c["h"] = "h_a";
        }));
        Ok(Variante(p, "test_kanalfrei", "fam-test-frei-b", d => Leitung(d)["formteile"]!["tstueck"]!["familie"] = "fam-test-t-gleich"));
        var b = Frei(p, "fam-test-frei-b").Formteil(m, k, "tstueck", g, R(250, 200));
        Assert.False(b.Ok);
        Assert.Contains("«b_a» müsste zugleich 430 mm (a.b) und 250 mm (c.b) sein", b.Fehler);
        Assert.Contains("Abzweig c mit eigenen Parametern", b.Vorschlag);
        Assert.True(Frei(p, "fam-test-frei-b").Formteil(m, k, "tstueck", g, g).Ok);   // gleiche Grösse geht
        // (c) Grössenwert als Formel
        Ok(Variante(p, "test_kanalbogen_frei", "fam-test-bogen-formel", d =>
        {
            foreach (var an in d["anschluesse"]!.AsArray()) an!["b"] = "breite + 0";
        }));
        Ok(Variante(p, "test_kanalfrei", "fam-test-frei-c", d => Leitung(d)["formteile"]!["bogen"]!["familie"] = "fam-test-bogen-formel"));
        var c = Frei(p, "fam-test-frei-c").Formteil(m, k, "bogen", g, winkel: 90);
        Assert.False(c.Ok);
        Assert.Contains("a.b ist die Formel «breite + 0»", c.Fehler);
        // (d) Vorlage, die es in der Formteil-Familie nicht gibt
        Ok(Variante(p, "test_kanalfrei", "fam-test-frei-d", d => Leitung(d)["formteile"]!["reduktion"]!["vorlage"] = "gibt-es-nicht"));
        var dd = Frei(p, "fam-test-frei-d").Formteil(m, k, "reduktion", g, R(400, 200));
        Assert.False(dd.Ok);
        Assert.Contains("Vorlage «gibt-es-nicht»", dd.Fehler);
        Assert.Contains("400x200/300x200", dd.Vorschlag);
    }

    [Fact]
    public void Formteilwahl_Typregel_BleibtUnterstuetzt()
    {
        var p = MitFrei();
        // Typregel: Normgrösse 400 → «400x200», sonst «500x300» – bei freier Grösse mit Exemplarwerten eingestellt
        Ok(Variante(p, "test_kanalfrei", "fam-test-frei-regel", d => Leitung(d)["formteile"]!["bogen"]!["typ"] = "if(b = 400, \"400x200\", \"500x300\")"));
        var s = Frei(p, "fam-test-frei-regel");
        var frei = s.Formteil(p.Modell, p.Katalog, "bogen", R(430, 270), winkel: 90);
        Assert.True(frei.Ok, frei.Fehler);
        Assert.True(frei.Frei);
        Assert.Equal("500x300", frei.Typ);
        Assert.Equal(430, frei.Exemplar!["breite"]!.GetValue<double>());
        Etwa(365, frei.Geometrie!.Einbaulaenge("a"));
        var norm = s.Formteil(p.Modell, p.Katalog, "bogen", R(400, 200), winkel: 90);
        Assert.False(norm.Frei);
        Assert.Equal("400x200", norm.Typ);
    }

    // ------------------------------------------------------------------ Routing: 430 × 270 mit Bogen, Reduktion, Abzweig

    /// <summary>Kanal 430 × 270 von (0, 0) nach (3000, 0), Bogen 90° nach (3000, 3000), z = 2500.</summary>
    static Projekt GezeichnetMitBogen(IProjektSpeicher? speicher = null)
    {
        var p = MitFrei(speicher);
        Ok(Kanal(p, G(430, 270), [[0, 0, 2500], [3000, 0, 2500], [3000, 3000, 2500]]));
        return p;
    }

    [Fact]
    public void Routing_430x270_Bogen90_Einbaulaenge365()
    {
        var p = MitFrei();
        var vorschau = Ok(Kanal(p, G(430, 270), [[0, 0, 2500], [3000, 0, 2500], [3000, 3000, 2500]], trocken: true));
        Assert.Contains(vorschau.Hinweise, h => h.StartsWith("Gerade KA-1 430 × 270 mm: 2635 mm von") && h.EndsWith(", frei (Vorlage «400x200»)"));
        Assert.Contains(vorschau.Hinweise, h => h.StartsWith("Bogen BO-1 Typ «400x200», 90°") && h.Contains("Einbaulängen a 365 mm, b 365 mm") && h.EndsWith(", frei 430 × 270"));
        Assert.Null(p.Modell.Finde("KA-1"));   // Trockenlauf = Vorschau
        Ok(Kanal(p, G(430, 270), [[0, 0, 2500], [3000, 0, 2500], [3000, 3000, 2500]]));
        // Bogen: L = (115 + 430/2)·tan 45° + 35 = 365 mm
        Gerade(p, "KA-1", [0, 0, 2500], [2635, 0, 2500]);
        Gerade(p, "KA-2", [3000, 365, 2500], [3000, 3000, 2500]);
        var ka = Hole(p, "KA-1");
        Assert.Equal("400x200", Familien.Typ(ka));   // Vorlage
        Assert.Equal(430, ka.Zahl("breite"));
        Assert.Equal(270, ka.Zahl("hoehe"));
        Assert.Equal("familie", ka.Eigenschaft("breite")!.Herkunft.Art);
        Assert.Equal($"{SerieName} Typ 400x200, frei 430 × 270", ka.Eigenschaft("breite")!.Herkunft.Quelle);
        Assert.All(ka.Anschluesse, a => { Assert.Equal(430, a.B); Assert.Equal(270, a.H); });
        Assert.Equal(R(430, 270), Anschlusslagen.LeitungGroesse(p.Modell, p.Katalog, ka));
        var bo = Hole(p, "BO-1");
        Assert.Equal("400x200", Familien.Typ(bo));
        var ex = Familien.Exemplarwerte(bo)!;
        Assert.Equal(430, ex["breite"]!.GetValue<double>());
        Assert.Equal(270, ex["hoehe"]!.GetValue<double>());
        Assert.Equal(90, ex["winkel"]!.GetValue<double>());
        Etwa([2635, 0, 2500], Stelle(p, "BO-1", "a").Lage);
        Etwa([3000, 365, 2500], Stelle(p, "BO-1", "b").Lage);
        Assert.Equal(R(430, 270), Stelle(p, "BO-1", "a").Groesse);
        IstVerbunden(p, "KA-1", "ende", "BO-1", "a");
        IstVerbunden(p, "BO-1", "b", "KA-2", "anfang");
        // Masse aus der Vorlage mit der tatsächlichen Grösse
        var info = HlksMasse.Serie(p, ka)!;
        Assert.True(info.Frei);
        Assert.Equal("430 × 270", info.Typ);
        Assert.Equal(432, info.Werte.Aussen);
        var mass = HlksMasse.Von(p, ka)!;
        Assert.True(mass.Rechteck);
        Assert.Equal(430, mass.Breite); Assert.Equal(270, mass.Hoehe);
        Assert.Equal("430×270", Leitungsbezeichnung.Text(p, null, ka));
        Sauber(p);
        Assert.Empty(Befunde(p, "P29"));
    }

    [Fact]
    public void Routing_Reduktion400x200_UndAbzweig250x200_OhneBefund()
    {
        var p = GezeichnetMitBogen();
        // Reduktion hinter dem Bogen (Lb = 365): l = max(30, 70) + 100 = 170
        var e = Ok(p.Ausfuehren("leitung_groesse", new JsonObject { ["leitungen"] = new JsonArray("KA-2"), ["groesse"] = G(400, 200) }));
        var rd = Angelegt(e, "Reduktion").Single();
        Assert.Equal("400x200/300x200", Familien.Typ(Hole(p, rd)));
        var exr = Familien.Exemplarwerte(Hole(p, rd))!;
        Assert.Equal(new[] { 430.0, 270, 400, 200 }, new[] { "breite1", "hoehe1", "breite2", "hoehe2" }.Select(n => exr[n]!.GetValue<double>()));
        Etwa([3000, 365, 2500], Stelle(p, rd, "a").Lage);
        Etwa([3000, 535, 2500], Stelle(p, rd, "b").Lage);
        Gerade(p, "KA-2", [3000, 535, 2500], [3000, 3000, 2500]);
        var ka2 = Hole(p, "KA-2");
        Assert.Equal("400x200", Familien.Typ(ka2));
        Assert.Equal(400, ka2.Zahl("breite"));
        Assert.Equal(200, ka2.Zahl("hoehe"));
        Assert.Equal($"{SerieName} Typ 400x200", ka2.Eigenschaft("breite")!.Herkunft.Quelle);   // Normgrösse
        Assert.False(HlksMasse.Serie(p, ka2)!.Frei);
        Sauber(p);
        // Abzweig 250 × 200 aus KA-1 (430 × 270) bei x = 1500: T l = 225, lc = 315
        var ea = Ok(p.Ausfuehren("leitung_abzweigen", new JsonObject
        {
            ["leitung"] = "KA-1", ["punkt"] = new JsonArray(1500, 0, 2500), ["richtung"] = new JsonArray(0, -1, 0), ["laenge"] = 1500, ["groesse"] = G(250, 200),
        }));
        var ts = Angelegt(ea, "TStueck").Single();
        Assert.Equal("400x200", Familien.Typ(Hole(p, ts)));
        var ext = Familien.Exemplarwerte(Hole(p, ts))!;
        Assert.Equal(new[] { 430.0, 270, 250, 200 }, new[] { "b_a", "h_a", "b_c", "h_c" }.Select(n => ext[n]!.GetValue<double>()));
        Gerade(p, "KA-1", [0, 0, 2500], [1275, 0, 2500]);          // 1500 − 225
        Gerade(p, "KA-1-2", [1725, 0, 2500], [2635, 0, 2500]);     // 1500 + 225
        var ab = Angelegt(ea, "Kanal").Single(x => x != "KA-1-2");
        Gerade(p, ab, [1500, -315, 2500], [1500, -1500, 2500]);    // 1500 − 315 = 1185 mm
        var kab = Hole(p, ab);
        Assert.Equal("400x200", Familien.Typ(kab));
        Assert.Equal(250, kab.Zahl("breite"));
        Assert.Equal(200, kab.Zahl("hoehe"));
        Assert.Equal(R(250, 200), Stelle(p, ts, "c").Groesse);
        Assert.Contains(ea.Hinweise, h => h.Contains($"Typ «400x200» frei 430 × 270/250 × 200"));
        Sauber(p);
        Assert.Empty(Befunde(p, "P29"));
        // Materialliste nach tatsächlicher Grösse
        var ml = Ableitungen.Materialliste(p);
        var leitungen = ml["leitungen"]!.AsArray().OfType<JsonObject>().Where(x => x["serie"]!.GetValue<string>() == SerieName)
            .ToDictionary(x => x["groesse"]!.GetValue<string>(), x => x["stueck"]!.GetValue<int>());
        Assert.Equal(2, leitungen["430 × 270"]);
        Assert.Equal(1, leitungen["400x200"]);
        Assert.Equal(1, leitungen["250 × 200"]);
        Assert.Equal(3, leitungen.Count);
        var formteile = ml["formteile"]!.AsArray().OfType<JsonObject>().Select(x => (x["klasse"]!.GetValue<string>(), x["typ"]!.GetValue<string>())).ToList();
        Assert.Contains(("Bogen", "430 × 270"), formteile);
        Assert.Contains(("Reduktion", "430 × 270/400 × 200"), formteile);
        Assert.Contains(("TStueck", "430 × 270/250 × 200"), formteile);
        Assert.Equal("430 × 270", FreieGroesse.Text(p, Hole(p, "KA-1")));
        Assert.Null(FreieGroesse.Text(p, Hole(p, "KA-2")));
    }

    [Fact]
    public void Befehle_GroesseAusserhalbOderNichtImSchritt_AbgelehntMitVorschlag()
    {
        var p = MitFrei();
        var n1 = Nein(Kanal(p, G(120, 270), [[0, 0, 2500], [3000, 0, 2500]]));
        Assert.Contains("ausserhalb der Grenzen", n1.Fehler);
        Assert.Contains("b = 120 mm, erlaubt 145 … 2500 mm", n1.Fehler);
        Assert.Contains("145 × 270", n1.Vorschlag);
        Assert.Contains("200 × 200", n1.Vorschlag);
        var n2 = Nein(Kanal(p, JsonValue.Create("430.5x270")!, [[0, 0, 2500], [3000, 0, 2500]]));
        Assert.Contains("nicht im Schritt", n2.Fehler);
        Assert.Contains("431 × 270", n2.Vorschlag);
        // Text «430x270» und Typname gehen
        Ok(Kanal(p, JsonValue.Create("430x270")!, [[0, 0, 2500], [3000, 0, 2500]]));
        Assert.Equal(430, Hole(p, "KA-1").Zahl("breite"));
        Ok(Kanal(p, JsonValue.Create("300x200")!, [[0, 1000, 2500], [3000, 1000, 2500]]));
        Assert.Equal("300x200", Familien.Typ(Hole(p, "KA-2")));
        // unbekannter Text: nennt Typen und die freie Eingabe
        var n3 = Nein(Kanal(p, JsonValue.Create("gross")!, [[0, 2000, 2500], [3000, 2000, 2500]]));
        Assert.Contains("hat keinen Typ «gross»", n3.Fehler);
        Assert.Contains("frei b × h (145 … 2500 mm, Schritt 1 mm)", n3.Vorschlag);
        // leitung_groesse ausserhalb der Grenzen
        var n4 = Nein(p.Ausfuehren("leitung_groesse", new JsonObject { ["leitung"] = "KA-1", ["groesse"] = G(430, 3000) }));
        Assert.Contains("h = 3000 mm, erlaubt 145 … 2500 mm", n4.Fehler);
        // leitung_serie auf eine Serie ohne freie Grösse: 430 × 270 gibt es dort nicht
        Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Datei("test_kanalserie") }));
        var n5 = Nein(p.Ausfuehren("leitung_serie", new JsonObject { ["leitung"] = "KA-1", ["serie"] = "fam-test-kanalserie" }));
        Assert.Contains("hat keine Grösse 430 × 270 mm", n5.Fehler);
    }

    [Fact]
    public void LeitungZiehen_MitSerieUndFreierGroesse_UndAufteilenBehaeltSie()
    {
        var p = MitFrei();
        var e = Ok(p.Ausfuehren("leitung_ziehen", new JsonObject
        {
            ["punkte"] = Pts([0, 0, 2500], [2000, 0, 2500], [2000, 2000, 2500]), ["system"] = "ZUL", ["serie"] = SerieId, ["groesse"] = "430x270",
        }));
        var kz = Angelegt(e, "Kanal").Single();
        var ka = Hole(p, kz);
        Assert.Equal("Kanal", ka.Klasse);   // klasse aus der Serie
        Assert.Equal("400x200", Familien.Typ(ka));
        Assert.Equal(430, ka.Zahl("breite"));
        Assert.Equal(3, ka.Geometrie!.Punkte.Count);   // Polylinie ohne Bögen
        Assert.True(HlksMasse.Serie(p, ka)!.Frei);
        // leitung_aufteilen nimmt die freie Grösse der Leitung (nicht die der Vorlage)
        Ok(p.Ausfuehren("leitung_aufteilen", new JsonObject { ["leitung"] = kz }));
        Gerade(p, kz, [0, 0, 2500], [1635, 0, 2500]);   // 2000 − 365
        Assert.Equal(430, Hole(p, kz).Zahl("breite"));
        Assert.Equal(430, Hole(p, kz + "-2").Zahl("breite"));
        Assert.Equal(430, Familien.Exemplarwerte(Hole(p, "BO-1"))!["breite"]!.GetValue<double>());
        Sauber(p);
        // abgelehnt: groesse ohne serie, Klasse passt nicht zur Serie
        Assert.Contains("groesse wirkt nur mit serie", Nein(p.Ausfuehren("leitung_ziehen", new JsonObject
        {
            ["punkte"] = Pts([0, 5000, 2500], [1000, 5000, 2500]), ["system"] = "ZUL", ["groesse"] = "430x270",
        })).Fehler);
        Assert.Contains("passt nicht zur Serie", Nein(p.Ausfuehren("leitung_ziehen", new JsonObject
        {
            ["punkte"] = Pts([0, 5000, 2500], [1000, 5000, 2500]), ["system"] = "ZUL", ["serie"] = SerieId, ["groesse"] = "430x270", ["klasse"] = "Rohr",
        })).Fehler);
    }

    // ------------------------------------------------------------------ Grösse an der Leitung: Nachführung und Regel

    static BefehlsErgebnis KanalAnlegen(Projekt p, string kz, string typ, JsonObject? eig = null)
    {
        var e = new JsonObject { ["familientyp"] = typ };
        foreach (var (k, v) in eig ?? []) e[k] = v?.DeepClone();
        return p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Kanal", ["kennzeichen"] = kz, ["system"] = "ZUL", ["geschoss"] = "EG",
            ["eigenschaften"] = e, ["bezuege"] = new JsonObject { ["familie"] = "FAM-1" },
            ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Pts([0, 9000, 2500], [2000, 9000, 2500]) },
        });
    }

    [Fact]
    public void Nachfuehrung_FreieGrosseBleibt_RastergroesseFolgtDemTyp()
    {
        var p = MitFrei();
        // Normgrösse 300x200 aus dem Typ
        Ok(KanalAnlegen(p, "KX", "300x200"));
        Assert.Equal(300, Hole(p, "KX").Zahl("breite"));
        // familientyp auf die Vorlage: die alte Normgrösse wird nicht still zur freien Grösse – die Vorlage gilt
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "KX", ["eigenschaften"] = new JsonObject { ["familientyp"] = "400x200" } }));
        Assert.Equal(400, Hole(p, "KX").Zahl("breite"));
        Assert.Equal(200, Hole(p, "KX").Zahl("hoehe"));
        Assert.False(HlksMasse.Serie(p, Hole(p, "KX"))!.Frei);
        // Grösse von Hand bei familientyp = Vorlage: freie Grösse, bleibt bei der Nachführung
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "KX", ["eigenschaften"] = new JsonObject { ["breite"] = 430, ["hoehe"] = 270 } }));
        var kx = Hole(p, "KX");
        Assert.Equal(430, kx.Zahl("breite"));
        Assert.Equal("familie", kx.Eigenschaft("breite")!.Herkunft.Art);
        Assert.Equal($"{SerieName} Typ 400x200, frei 430 × 270", kx.Eigenschaft("breite")!.Herkunft.Quelle);
        Assert.All(kx.Anschluesse, a => Assert.Equal(430, a.B));
        Assert.True(HlksMasse.Serie(p, kx)!.Frei);
        // die Familie ändert sich (Blechdicke): die freie Grösse bleibt, die Masse folgen der Vorlage
        Ok(p.Ausfuehren("familie_aendern", new JsonObject
        {
            ["familie"] = "FAM-1",
            ["ops"] = new JsonArray(new JsonObject { ["op"] = "parameter_setzen", ["parameter"] = new JsonObject { ["name"] = "s", ["art"] = "typ", ["datentyp"] = "laenge", ["gruppe"] = "abmessung", ["wert"] = 2, ["beschreibung"] = "Blechdicke (FIKTIV)" } }),
        }));
        Assert.Equal(430, Hole(p, "KX").Zahl("breite"));
        Assert.Equal(434, HlksMasse.Serie(p, Hole(p, "KX"))!.Werte.Aussen);   // 430 + 2·2
        // anderer Typ: Grösse aus dem Typ
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "KX", ["eigenschaften"] = new JsonObject { ["familientyp"] = "500x300" } }));
        Assert.Equal(500, Hole(p, "KX").Zahl("breite"));
        Assert.Equal(300, Hole(p, "KX").Zahl("hoehe"));
    }

    // ------------------------------------------------------------------ Prüfungen P29

    [Fact]
    public void P29_AusserhalbDerGrenzen_Fehler_NichtImSchritt_Hinweis()
    {
        var p = MitFrei();
        Ok(KanalAnlegen(p, "K-KLEIN", "400x200", new JsonObject { ["breite"] = 120, ["hoehe"] = 270 }));
        Ok(KanalAnlegen(p, "K-SCHRITT", "400x200", new JsonObject { ["breite"] = 430.5, ["hoehe"] = 270 }));
        Ok(KanalAnlegen(p, "K-FREI", "400x200", new JsonObject { ["breite"] = 430, ["hoehe"] = 270 }));
        Ok(KanalAnlegen(p, "K-NORM", "300x200"));
        var b = Befunde(p, "P29");
        var klein = Assert.Single(b, x => x.Element == "K-KLEIN");
        Assert.Equal("fehler", klein.Schwere);
        Assert.Contains("120 × 270 mm liegt ausserhalb der Grenzen", klein.Text);
        Assert.Contains("b = 120 mm, erlaubt 145 … 2500 mm", klein.Text);
        Assert.Contains("145 × 270", klein.Vorschlag);
        var schritt = Assert.Single(b, x => x.Element == "K-SCHRITT");
        Assert.Equal("hinweis", schritt.Schwere);
        Assert.Contains("nicht im Schritt", schritt.Text);
        Assert.Contains("431 × 270", schritt.Vorschlag);
        Assert.DoesNotContain(b, x => x.Element is "K-FREI" or "K-NORM");
        Assert.All(b, x => Assert.True(Pruefungen.Codes.ContainsKey(x.Code)));
    }

    // ------------------------------------------------------------------ Abfrage leitungsserie

    [Fact]
    public void Abfrage_Leitungsserie_FreiUndProbe()
    {
        var p = MitFrei();
        var a = (JsonObject)Hlks.Kern.Tuer.Abfrage(p, "leitungsserie", new JsonObject { ["familie"] = SerieId });
        var fr = a["frei"]!.AsObject();
        Assert.Equal("400x200", fr["vorlage"]!.GetValue<string>());
        Assert.Equal(145, fr["grenzen"]!["b"]!["min"]!.GetValue<double>());
        Assert.Equal(2500, fr["grenzen"]!["h"]!["max"]!.GetValue<double>());
        Assert.Equal(1, fr["schritt"]!.GetValue<double>());
        Assert.Equal("frei b × h (145 … 2500 mm, Schritt 1 mm)", fr["text"]!.GetValue<string>());
        Assert.Equal("400x200/300x200", a["formteilFamilien"]!["reduktion"]!["vorlage"]!.GetValue<string>());
        // Probe: Mitte der Grenzen (145 + 2500)/2 = 1322.5 → 1323 (Schritt 1)
        var pr = a["probe"]!.AsObject();
        Assert.Equal("1323 × 1323", pr["text"]!.GetValue<string>());
        Assert.True(pr["gueltig"]!.GetValue<bool>());
        Assert.True(pr["frei"]!.GetValue<bool>());
        Assert.Equal("400x200", pr["typ"]!.GetValue<string>());
        Assert.Equal(1325, pr["aussen"]!.GetValue<double>());
        var ft = pr["formteile"]!.AsObject();
        Assert.True(ft["bogen"]!["verfuegbar"]!.GetValue<bool>(), ft["bogen"]!.ToJsonString());
        Assert.True(ft["bogen"]!["frei"]!.GetValue<bool>());
        Assert.Equal(1323, ft["bogen"]!["parameter"]!["breite"]!.GetValue<double>());
        Assert.True(ft["tstueck"]!["verfuegbar"]!.GetValue<bool>(), ft["tstueck"]!.ToJsonString());
        var red = ft["reduktion"]!.AsArray().Single()!;
        Assert.Equal("500x300", red["auf"]!.GetValue<string>());
        Assert.True(red["verfuegbar"]!.GetValue<bool>(), red.ToJsonString());
        Assert.False(ft["reinigungsoeffnung"]!["verfuegbar"]!.GetValue<bool>());
        // die Normgrössen stehen weiter als Vorschlag in groessen
        Assert.Equal(["500x300", "400x200", "300x200", "200x200"], a["groessen"]!.AsArray().Select(x => x!["typ"]!.GetValue<string>()));
        Assert.Contains("probe", Leitungsserie.AbfrageText);
        // Serie ohne frei: beides null
        Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Datei("test_kanalserie") }));
        var ohne = (JsonObject)Hlks.Kern.Tuer.Abfrage(p, "leitungsserie", new JsonObject { ["familie"] = "fam-test-kanalserie" });
        Assert.Null(ohne["frei"]);
        Assert.Null(ohne["probe"]);
    }

    // ------------------------------------------------------------------ Speichern / Laden

    [Fact]
    public void SpeichernUndLaden_FreieGroesseBleibt()
    {
        var wurzel = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "frei_" + Guid.NewGuid().ToString("N")[..8]);
        var ordner = Path.Combine(wurzel, "e_KICAD");
        try
        {
            new OrdnerSpeicher(ordner).Anlegen();
            var p = GezeichnetMitBogen(new OrdnerSpeicher(ordner));
            var vorher = Inhalt(p);
            var b = Projekt.Oeffnen(Kat, new OrdnerSpeicher(ordner), "B", "pc-b");
            Assert.Equal(vorher, Inhalt(b));
            var ka = Hole(b, "KA-1");
            Assert.Equal("400x200", Familien.Typ(ka));
            Assert.Equal(430, ka.Zahl("breite"));
            Assert.Equal(270, ka.Zahl("hoehe"));
            Assert.True(HlksMasse.Serie(b, ka)!.Frei);
            Assert.Equal(430, Familien.Exemplarwerte(Hole(b, "BO-1"))!["breite"]!.GetValue<double>());
            Sauber(b);
            // weiterarbeiten im geöffneten Projekt: Grösse des Zugs ändern (freie Grösse auf freie Grösse)
            Ok(b.Ausfuehren("leitung_groesse", new JsonObject { ["leitung"] = "KA-1", ["zug"] = true, ["groesse"] = G(450, 250) }));
            Assert.Equal(450, Hole(b, "KA-2").Zahl("breite"));
            Etwa(3000 - 375, Stelle(b, "BO-1", "a").Lage![0]);   // L = (115 + 450/2)·tan 45° + 35 = 375
            Sauber(b);
        }
        finally
        {
            try { Directory.Delete(wurzel, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    // ------------------------------------------------------------------ IFC-Export

    [Fact]
    public void IfcExport_TypobjektJeTatsaechlicherGroesse()
    {
        var p = GezeichnetMitBogen();
        Ok(p.Ausfuehren("leitung_abzweigen", new JsonObject
        {
            ["leitung"] = "KA-1", ["punkt"] = new JsonArray(1500, 0, 2500), ["richtung"] = new JsonArray(0, -1, 0), ["laenge"] = 1500, ["groesse"] = G(250, 200),
        }));
        Ok(p.Ausfuehren("leitung_groesse", new JsonObject { ["leitungen"] = new JsonArray("KA-2"), ["groesse"] = G(400, 200) }));
        var (aus, _) = IfcExportAufbau.Erstellen(p, "IFC4", new IfcExUmfang(), null, null);
        string TypName(string kz) => aus.Produkte.Single(x => x.Element.Kennzeichen == kz).Typ!.Name;
        Assert.Equal($"{SerieName} 430 × 270", TypName("KA-1"));
        Assert.Equal($"{SerieName} 430 × 270", TypName("KA-1-2"));
        Assert.Same(aus.Produkte.Single(x => x.Element.Kennzeichen == "KA-1").Typ, aus.Produkte.Single(x => x.Element.Kennzeichen == "KA-1-2").Typ);
        Assert.Equal($"{SerieName} 400x200", TypName("KA-2"));   // Normgrösse: Typname
        var ab = p.Modell.DerKlasse("Kanal").Single(x => x.Zahl("breite") == 250).Kennzeichen;
        Assert.Equal($"{SerieName} 250 × 200", TypName(ab));
        Assert.Equal("Testkanalbogen frei rechteckig (fiktiv) 430 × 270", TypName("BO-1"));
        Assert.Equal("Test-Kanal-T frei rechteckig (fiktiv) 430 × 270/250 × 200", TypName(p.Modell.DerKlasse("TStueck").Single().Kennzeichen));
        Assert.Equal("Testkanalreduktion frei rechteckig (fiktiv) 430 × 270/400 × 200", TypName(p.Modell.DerKlasse("Reduktion").Single().Kennzeichen));
        // je tatsächlicher Grösse ein Typobjekt, keines für die Vorlage der freien Kanäle
        var kanaltypen = aus.Typen.Where(t => t.Name.StartsWith(SerieName, StringComparison.Ordinal)).Select(t => t.Name).Order().ToList();
        Assert.Equal([$"{SerieName} 250 × 200", $"{SerieName} 400x200", $"{SerieName} 430 × 270"], kanaltypen);
        // Pset HK_KICAD.Typ am Element = tatsächliche Grösse
        var hk = aus.Produkte.Single(x => x.Element.Kennzeichen == "KA-1").Psets.Single(s => s.Name == IfcExport.PsetKicad);
        Assert.Equal("430 × 270", hk.Werte.Single(w => w.Name == "Typ").Text);
        // die Datei entsteht ohne Fehler
        using var o = new IfcExportOrdner();
        var bericht = IfcExportHilfe.Export(p, o.Datei("frei.ifc"));
        Assert.True(File.Exists(o.Datei("frei.ifc")), bericht.ToJsonString());
    }
}
