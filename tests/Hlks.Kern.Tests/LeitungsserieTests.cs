using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3, B1: Familienformat «leitung», Leitungsserie (Grössen, Masse, Winkelregel) und Formteilwahl mit Einbaulängen.
/// Handrechnungen aus den FIKTIVEN Testfamilien (Hauptlauf, 18.10.2026): da = DN + 5; Bogen r = 2·da, La = Lb = r·tan(w/2);
/// T-Stück l = lc = da; Reduktion l = 40 mm. Keine Normwerte, keine echten Rohrabmessungen.</summary>
public class LeitungsserieTests
{
    // ------------------------------------------------------------------ Familienformat «leitung»

    static FamilieDef Lesen(JsonObject d) => FamilieDef.Lesen(d, Kat);

    static void Fehler(JsonObject d, string stelle, string text)
    {
        var f = Lesen(d);
        Assert.False(f.Gueltig, "hätte ungültig sein sollen");
        Assert.Contains(f.Fehler, x => x.Stelle.StartsWith(stelle) && x.Meldung.Contains(text));
        Assert.All(f.Fehler.Where(x => x.Stelle.StartsWith(stelle)), x => Assert.False(string.IsNullOrWhiteSpace(x.Vorschlag), $"Vorschlag fehlt: {x}"));
    }

    static JsonObject SerieDef(Action<JsonObject>? aendern = null)
    {
        var d = Familie("test_rohrserie");
        aendern?.Invoke(d);
        return d;
    }

    static JsonObject L(JsonObject d) => d["leitung"]!.AsObject();

    [Fact]
    public void Rohrserie_IstGueltigUndLiestDenBlock()
    {
        var f = Lesen(Familie("test_rohrserie"));
        Assert.True(f.Gueltig, string.Join(" | ", f.Fehler));
        Assert.True(f.IstLeitungsserie);
        var l = f.Leitung!;
        Assert.Equal("rund", l.Querschnitt);
        Assert.Equal("dn", l.Groesse["dn"]);
        Assert.Equal([15.0, 30, 45, 90], l.Winkel!);
        Assert.False(l.WinkelFrei);
        Assert.Equal("tstueck", l.Abzweig);
        Assert.Equal(["bogen", "reduktion", "tstueck"], l.Formteile.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("fam-test-bogen", l.Formteile["bogen"].Familie);
        Assert.Null(l.Formteile["bogen"].TypFormel);
    }

    [Fact]
    public void Block_NurBeiLeitungenUndDortPflicht()
    {
        var hk = FamilieTestHilfe.Testheizkoerper();
        hk["leitung"] = L(Familie("test_rohrserie")).DeepClone();
        Fehler(hk, "leitung", "nur für Leitungsserien");
        Fehler(SerieDef(d => d.Remove("leitung")), "leitung", "braucht den Block «leitung»");
    }

    [Fact]
    public void Block_QuerschnittUndGroessePassendZurKategorie()
    {
        Fehler(SerieDef(d => L(d)["querschnitt"] = "oval"), "leitung.querschnitt", "gibt es nicht");
        Fehler(SerieDef(d => L(d)["querschnitt"] = "rechteck"), "leitung.querschnitt", "Rohre sind rund");
        Fehler(SerieDef(d => L(d)["groesse"] = new JsonObject { ["d"] = "dn" }), "leitung.groesse", "passt nicht zu Rohr");
        Fehler(SerieDef(d => L(d)["groesse"] = new JsonObject { ["dn"] = "gibtsnicht" }), "leitung.groesse.dn", "gibt es nicht");
        Fehler(SerieDef(d => L(d).Remove("groesse")), "leitung.groesse", "fehlt");
        Fehler(SerieDef(d => L(d).Remove("aussen")), "leitung.aussen", "fehlt");
        Fehler(SerieDef(d => L(d)["aussen"] = "dx + 1"), "leitung.aussen", "unbekannter Name «dx»");
        Fehler(SerieDef(d => L(d)["innen"] = "da - 2 * t"), "leitung.innen", "unbekannter Name «t»");
        Fehler(SerieDef(d => L(d)["farbe"] = "rot"), "leitung", "Unbekanntes Feld «farbe»");
        // Grösse muss ein Typparameter sein
        Fehler(SerieDef(d =>
        {
            ((JsonArray)d["parameter"]!).Add(new JsonObject { ["name"] = "nw", ["art"] = "exemplar", ["datentyp"] = "dn" });
            L(d)["groesse"] = new JsonObject { ["dn"] = "nw" };
        }), "leitung.groesse.dn", "Exemplarparameter");
    }

    [Fact]
    public void Block_KanalRundUndRechteckig()
    {
        // FIKTIVE Mini-Kanalserien (keine Herstellerdaten): rund mit d, rechteckig mit b und h
        var rund = JsonNode.Parse("""
            { "format": 1, "name": "Kanal rund (fiktiv)", "kategorie": "Kanal",
              "parameter": [ { "name": "d", "art": "typ", "datentyp": "laenge" } ],
              "typen": [ { "name": "125", "werte": { "d": 125 } }, { "name": "160", "werte": { "d": 160 } } ],
              "koerper": [], "anschluesse": [],
              "leitung": { "querschnitt": "rund", "groesse": { "d": "d" }, "aussen": "d + 1", "regeln": { "winkel": "frei" } } }
            """)!.AsObject();
        var f = Lesen(rund);
        Assert.True(f.Gueltig, string.Join(" | ", f.Fehler));
        Assert.True(f.Leitung!.WinkelFrei);
        Assert.Null(f.Leitung.Winkel);
        var eckig = JsonNode.Parse("""
            { "format": 1, "name": "Kanal eckig (fiktiv)", "kategorie": "Kanal",
              "parameter": [ { "name": "b", "art": "typ", "datentyp": "laenge" }, { "name": "h", "art": "typ", "datentyp": "laenge" } ],
              "typen": [ { "name": "400x200", "werte": { "b": 400, "h": 200 } } ],
              "koerper": [], "anschluesse": [],
              "leitung": { "querschnitt": "rechteck", "groesse": { "b": "b", "h": "h" }, "aussen": 0.001 } }
            """)!.AsObject();
        Assert.True(Lesen(eckig).Gueltig, string.Join(" | ", Lesen(eckig).Fehler));
        var halb = (JsonObject)eckig.DeepClone();
        L(halb)["groesse"] = new JsonObject { ["b"] = "b" };
        Fehler(halb, "leitung.groesse.h", "fehlt");
        var falsch = (JsonObject)rund.DeepClone();
        L(falsch)["groesse"] = new JsonObject { ["dn"] = "d" };
        Fehler(falsch, "leitung.groesse", "passt nicht zu Kanal");
    }

    [Fact]
    public void Block_KeineKoerperUndKeineAnschluesse()
    {
        Fehler(SerieDef(d => d["koerper"] = new JsonArray(new JsonObject { ["id"] = "k", ["art"] = "kugel", ["mitte"] = new JsonArray(0, 0, 0), ["r"] = 5 })), "koerper", "keine Körper");
        Fehler(SerieDef(d => d["anschluesse"] = new JsonArray(new JsonObject
        {
            ["name"] = "x", ["medium"] = "*", ["fluss"] = "beide", ["lage"] = new JsonArray(0, 0, 0), ["richtung"] = new JsonArray(1, 0, 0),
        })), "anschluesse", "keine Anschlüsse");
    }

    [Fact]
    public void Block_FormteileUndRegeln()
    {
        Fehler(SerieDef(d => L(d)["formteile"]!["winkelstueck"] = new JsonObject { ["familie"] = "x" }), "leitung.formteile.winkelstueck", "gibt es nicht");
        Fehler(SerieDef(d => L(d)["formteile"]!["bogen"] = new JsonObject { ["typ"] = "\"DN 20\"" }), "leitung.formteile.bogen.familie", "fehlt");
        Fehler(SerieDef(d => L(d)["formteile"]!["bogen"]!["farbe"] = "rot"), "leitung.formteile.bogen", "Unbekanntes Feld «farbe»");
        Fehler(SerieDef(d => L(d)["formteile"]!["bogen"]!["typ"] = "if(da = 20, \"DN 15\", \"DN 20\")"), "leitung.formteile.bogen.typ", "unbekannter Name «da»");
        Fehler(SerieDef(d => L(d)["formteile"]!["bogen"]!["typ"] = "if(dn_2 = 20, \"DN 15\", \"DN 20\")"), "leitung.formteile.bogen.typ", "unbekannter Name «dn_2»");
        Assert.True(Lesen(SerieDef(d => L(d)["formteile"]!["reduktion"]!["typ"] = "if(dn_2 = 15, \"DN 20/15\", \"DN 25/20\")")).Gueltig);   // _2 bei Reduktion erlaubt
        Fehler(SerieDef(d => L(d)["formteile"]!["bogen"]!["typ"] = "if(dn = , 1)"), "leitung.formteile.bogen.typ", "Formel");
        Fehler(SerieDef(d => L(d)["regeln"]!["winkel"] = new JsonArray(0, 45)), "leitung.regeln.winkel", "0 < w ≤ 180");
        Fehler(SerieDef(d => L(d)["regeln"]!["winkel"] = new JsonArray(45, 200)), "leitung.regeln.winkel", "0 < w ≤ 180");
        Fehler(SerieDef(d => L(d)["regeln"]!["winkel"] = "beliebig"), "leitung.regeln.winkel", "weder eine Liste noch «frei»");
        Fehler(SerieDef(d => L(d)["regeln"]!["abzweig"] = "hosenstueck"), "leitung.regeln.abzweig", "gibt es nicht");
        Fehler(SerieDef(d => L(d)["regeln"]!["neigung"] = 2), "leitung.regeln", "Unbekanntes Feld «neigung»");
        Fehler(SerieDef(d => L(d)["regeln"]!["biegeradius"] = "3 * dx"), "leitung.regeln.biegeradius", "unbekannter Name «dx»");
        Assert.True(Lesen(SerieDef(d => L(d)["regeln"]!["biegeradius"] = "2 * da")).Gueltig);
    }

    // ------------------------------------------------------------------ Leitungsserie: Grössen, Masse, Regeln

    [Fact]
    public void Serie_GroessenMitAussenInnenWand()
    {
        var p = MitSerie(false);
        var s = Serie(p);
        Assert.Equal("Rohr", s.Kategorie);
        Assert.Equal("rund", s.Querschnitt);
        Assert.Equal(["DN 15", "DN 20", "DN 25", "DN 32", "DN 40", "DN 50"], s.Groessen.Select(g => g.Typ));
        Assert.All(s.Groessen, g => Assert.True(g.Gueltig, g.Fehler));
        // DN 20: da = 25, s = 2, di = da − 2·s = 21 (FIKTIV)
        var g20 = s.Groesse("DN 20")!;
        Assert.Equal(20, g20.Groesse.Dn);
        Assert.Equal(25, g20.Aussen); Assert.Equal(21, g20.Innen); Assert.Equal(2, g20.Wand);
        Assert.Null(g20.Biegeradius);
        // DN 32: da = 37, s = 3 → di = 31
        var g32 = s.Groesse("DN 32")!;
        Assert.Equal(37, g32.Aussen); Assert.Equal(31, g32.Innen); Assert.Equal(3, g32.Wand);
        Assert.Equal("DN 25", s.TypFuer(Leitungsgroesse.Rohr(25))!.Typ);
        Assert.Null(s.TypFuer(Leitungsgroesse.Rohr(65)));
        Assert.Null(s.TypFuer(Leitungsgroesse.Rund(25)));
        Assert.True(s.WinkelErlaubt(45));
        Assert.True(s.WinkelErlaubt(90));
        Assert.False(s.WinkelErlaubt(60));
        Assert.Equal("15°, 30°, 45°, 90°", s.WinkelText);
    }

    [Fact]
    public void Serie_KeineLeitungsserieGibtFehlerMitVorschlag()
    {
        var p = MitSerie();
        var f = Assert.Throws<HlksFehler>(() => Leitungsserie.Hole(p.Modell, p.Katalog, "fam-test-bogen"));
        Assert.Contains("keine Leitungsserie", f.Message);
        Assert.Contains("FAM-1", f.Vorschlag);
    }

    [Fact]
    public void Serie_BiegeradiusJeGroesse()
    {
        var p = MitSerie(false);
        var d = Familie("test_rohrserie");
        L(d)["regeln"]!["biegeradius"] = "3 * da";   // FIKTIV
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["familie"] = "FAM-1", ["definition"] = d.DeepClone() }));
        Assert.Equal(75, Serie(p).Groesse("DN 20")!.Biegeradius);   // 3 · 25
    }

    // ------------------------------------------------------------------ Formteilwahl mit Einbaulängen (Handrechnung)

    [Fact]
    public void Bogen_DN20_90Grad_Einbaulaenge50()
    {
        var p = MitSerie();
        var w = Serie(p).Formteil(p.Modell, p.Katalog, "bogen", Leitungsgroesse.Rohr(20), winkel: 90);
        Assert.True(w.Ok, w.Fehler);
        Assert.Equal("FAM-2", w.Familie!.Kennzeichen);
        Assert.Equal("DN 20", w.Typ);
        Assert.Equal(90, w.Exemplar!["winkel"]!.GetValue<double>());
        // r = 2 · da = 50; La = Lb = 50 · tan(45°) = 50 mm
        var g = w.Geometrie!;
        Etwa(50, g.Einbaulaenge("a"), 1e-6);
        Etwa(50, g.Einbaulaenge("b"), 1e-6);
        Etwa([-50, 0, 0], g.Anschluss("a").Lage); Etwa([-1, 0, 0], g.Anschluss("a").Richtung);
        Etwa([0, 50, 0], g.Anschluss("b").Lage); Etwa([0, 1, 0], g.Anschluss("b").Richtung);
        Etwa(90, g.Winkel!.Value, 1e-9);
        Assert.Equal(20, g.Anschluss("b").Groesse.Dn);
    }

    [Fact]
    public void Bogen_DN20_45Grad_Einbaulaenge20711()
    {
        var p = MitSerie();
        var w = Serie(p).Formteil(p.Modell, p.Katalog, "bogen", Leitungsgroesse.Rohr(20), winkel: 45);
        Assert.True(w.Ok, w.Fehler);
        // La = Lb = 50 · tan(22.5°) = 20.7107 mm; b bei Lb · (cos 45°, sin 45°, 0) = (14.6447, 14.6447, 0)
        var lb = 50 * Math.Tan(22.5 * Math.PI / 180);
        Etwa(20.711, lb, 1e-3);
        Etwa(lb, w.Geometrie!.Einbaulaenge("a"), 1e-5);
        Etwa(lb, w.Geometrie.Einbaulaenge("b"), 1e-5);
        Etwa([lb * Math.Sqrt(0.5), lb * Math.Sqrt(0.5), 0], w.Geometrie.Anschluss("b").Lage, 1e-5);
        Etwa(45, w.Geometrie.Winkel!.Value, 1e-6);
    }

    [Fact]
    public void Bogen_60Grad_InDerSerieNichtErlaubt()
    {
        var p = MitSerie();
        var w = Serie(p).Formteil(p.Modell, p.Katalog, "bogen", Leitungsgroesse.Rohr(20), winkel: 60);
        Assert.False(w.Ok);
        Assert.Contains("60°", w.Fehler);
        Assert.Contains("15°, 30°, 45°, 90°", w.Fehler);
        Assert.False(string.IsNullOrWhiteSpace(w.Vorschlag));
    }

    [Fact]
    public void TStueck_DN32_Einbaulaengen37()
    {
        var p = MitSerie();
        var w = Serie(p).Formteil(p.Modell, p.Katalog, "tstueck", Leitungsgroesse.Rohr(32), Leitungsgroesse.Rohr(32));
        Assert.True(w.Ok, w.Fehler);
        Assert.Equal("FAM-3", w.Familie!.Kennzeichen);
        Assert.Equal("DN 32", w.Typ);
        // l = lc = da = 37 mm
        Etwa([-37, 0, 0], w.Geometrie!.Anschluss("a").Lage);
        Etwa([37, 0, 0], w.Geometrie.Anschluss("b").Lage);
        Etwa([0, 37, 0], w.Geometrie.Anschluss("c").Lage);
        Etwa([0, 1, 0], w.Geometrie.Anschluss("c").Richtung);
        Assert.Equal(37, w.Geometrie.Einbaulaenge("c"), 9);
        // Abzweig kleiner: die Testfamilie hat nur gleiche Grössen
        var klein = Serie(p).Formteil(p.Modell, p.Katalog, "tstueck", Leitungsgroesse.Rohr(32), Leitungsgroesse.Rohr(20));
        Assert.False(klein.Ok);
        Assert.Contains("c = DN 20", klein.Fehler);
    }

    [Fact]
    public void Reduktion_25auf20_TypUndEinbaulaenge40()
    {
        var p = MitSerie();
        var w = Serie(p).Formteil(p.Modell, p.Katalog, "reduktion", Leitungsgroesse.Rohr(25), Leitungsgroesse.Rohr(20));
        Assert.True(w.Ok, w.Fehler);
        Assert.Equal("FAM-4", w.Familie!.Kennzeichen);
        Assert.Equal("DN 25/20", w.Typ);
        Assert.False(w.Vertauscht);
        Etwa(0, w.Geometrie!.Einbaulaenge("a"));
        Etwa(40, w.Geometrie.Einbaulaenge("b"));
        Assert.Equal(25, w.Geometrie.Anschluss("a").Groesse.Dn);
        Assert.Equal(20, w.Geometrie.Anschluss("b").Groesse.Dn);
        // klein → gross angegeben: geordnet, gleicher Typ
        var umgekehrt = Serie(p).Formteil(p.Modell, p.Katalog, "reduktion", Leitungsgroesse.Rohr(20), Leitungsgroesse.Rohr(25));
        Assert.True(umgekehrt.Ok, umgekehrt.Fehler);
        Assert.Equal("DN 25/20", umgekehrt.Typ);
        Assert.True(umgekehrt.Vertauscht);
    }

    [Fact]
    public void Reduktion_32auf15_KeinTyp()
    {
        var p = MitSerie();
        var w = Serie(p).Formteil(p.Modell, p.Katalog, "reduktion", Leitungsgroesse.Rohr(32), Leitungsgroesse.Rohr(15));
        Assert.False(w.Ok);
        Assert.Contains("Kein Typ", w.Fehler);
        Assert.Contains("a = DN 32, b = DN 15", w.Fehler);
        Assert.Contains("DN 32/25", w.Vorschlag);   // vorhandene Typen
        var gleich = Serie(p).Formteil(p.Modell, p.Katalog, "reduktion", Leitungsgroesse.Rohr(20), Leitungsgroesse.Rohr(20));
        Assert.False(gleich.Ok);
    }

    [Fact]
    public void Formteilwahl_FamilieFehltImProjekt_VorschlagFamilieLaden()
    {
        var p = MitSerie(false);
        var w = Serie(p).Formteil(p.Modell, p.Katalog, "bogen", Leitungsgroesse.Rohr(20), winkel: 90);
        Assert.False(w.Ok);
        Assert.Contains("fam-test-bogen", w.Fehler);
        Assert.Contains("fehlt im Projekt", w.Fehler);
        Assert.Contains("familie_laden", w.Vorschlag);
        // Art ohne Familie in der Serie
        var r = Serie(p).Formteil(p.Modell, p.Katalog, "reinigungsoeffnung", Leitungsgroesse.Rohr(20));
        Assert.False(r.Ok);
        Assert.Contains("nennt keine Formteil-Familie", r.Fehler);
        Assert.Contains("leitung_setzen", r.Vorschlag);
        // Grösse der falschen Art
        Assert.False(Serie(p).Formteil(p.Modell, p.Katalog, "bogen", Leitungsgroesse.Rund(20)).Ok);
    }

    [Fact]
    public void Formteilwahl_FalscheRichtungVonA_IstFehler()
    {
        var p = MitSerie(false);
        // FIKTIV: Testbogen mit Anschluss a nach +x statt −x
        var d = Familie("test_bogen");
        d["anschluesse"]![0]!["richtung"] = new JsonArray(1, 0, 0);
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d.DeepClone() }));
        var w = Serie(p).Formteil(p.Modell, p.Katalog, "bogen", Leitungsgroesse.Rohr(20), winkel: 90);
        Assert.False(w.Ok);
        Assert.Contains("Anschluss a zeigt nach (1, 0, 0)", w.Fehler);
        Assert.Contains("Grundsatzentscheid 3", w.Vorschlag);
    }

    [Fact]
    public void Formteilwahl_FalscheKategorie()
    {
        var p = MitSerie(false);
        var d = Familie("test_tstueck");
        d["id"] = "fam-test-bogen";   // T-Stück unter der id des Bogens (FIKTIV)
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d.DeepClone() }));
        var w = Serie(p).Formteil(p.Modell, p.Katalog, "bogen", Leitungsgroesse.Rohr(20));
        Assert.False(w.Ok);
        Assert.Contains("Kategorie TStueck", w.Fehler);
    }

    [Fact]
    public void Formteilwahl_MitTypregel()
    {
        var p = MitSerie();
        var d = Familie("test_rohrserie");
        L(d)["formteile"]!["bogen"]!["typ"] = "if(dn = 20, \"DN 20\", \"DN 99\")";   // FIKTIVE Regel
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["familie"] = "FAM-1", ["definition"] = d.DeepClone() }));
        var ok = Serie(p).Formteil(p.Modell, p.Katalog, "bogen", Leitungsgroesse.Rohr(20), winkel: 90);
        Assert.True(ok.Ok, ok.Fehler);
        Assert.Equal("DN 20", ok.Typ);
        var nein = Serie(p).Formteil(p.Modell, p.Katalog, "bogen", Leitungsgroesse.Rohr(25), winkel: 90);
        Assert.False(nein.Ok);
        Assert.Contains("«DN 99»", nein.Fehler);
        // Regel mit _2 (Reduktion klein)
        L(d)["formteile"]!["reduktion"]!["typ"] = "if(and(dn = 25, dn_2 = 20), \"DN 25/20\", \"DN 20/15\")";
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["familie"] = "FAM-1", ["definition"] = d.DeepClone() }));
        Assert.Equal("DN 25/20", Serie(p).Formteil(p.Modell, p.Katalog, "reduktion", Leitungsgroesse.Rohr(25), Leitungsgroesse.Rohr(20)).Typ);
        // Regel ergibt einen Typ, dessen Grössen nicht passen
        var falsch = Serie(p).Formteil(p.Modell, p.Katalog, "reduktion", Leitungsgroesse.Rohr(40), Leitungsgroesse.Rohr(32));
        Assert.False(falsch.Ok);
        Assert.Contains("passt nicht", falsch.Fehler);
    }

    [Fact]
    public void Formteilgeometrie_LokalOhneArt_ZeigtAnschluesse()
    {
        var p = MitSerie();
        var def = Familien.Definition(Hole(p, "FAM-2"), p.Katalog);
        var g = Formteilgeometrie.Lokal(def, "DN 25", new JsonObject { ["winkel"] = 30 });
        // r = 2 · 30 = 60; La = 60 · tan 15° = 16.077 mm
        Etwa(60 * Math.Tan(15 * Math.PI / 180), g.Einbaulaenge("a"), 1e-5);
        Assert.Throws<HlksFehler>(() => Formteilgeometrie.Lokal(def, "DN 99", null));
    }

    // ------------------------------------------------------------------ Abfrage leitungsserie

    [Fact]
    public void Abfrage_Leitungsserie_GroessenUndVerfuegbarkeit()
    {
        var p = MitSerie();
        var o = (JsonObject)Tuer.Abfrage(p, "leitungsserie", new JsonObject { ["familie"] = "FAM-1" });
        Assert.Equal("rund", o["querschnitt"]!.GetValue<string>());
        Assert.Equal("Rohr", o["kategorie"]!.GetValue<string>());
        Assert.Equal([15.0, 30, 45, 90], o["winkel"]!.AsArray().Select(x => x!.GetValue<double>()));
        Assert.Equal("FAM-2", o["formteilFamilien"]!["bogen"]!["imProjekt"]!.GetValue<string>());
        Assert.Null(o["formteilFamilien"]!["reinigungsoeffnung"]);
        var g = o["groessen"]!.AsArray().Select(x => x!.AsObject()).ToDictionary(x => x["typ"]!.GetValue<string>());
        Assert.Equal(6, g.Count);
        Assert.Equal(25, g["DN 20"]["aussen"]!.GetValue<double>());
        Assert.Equal(21, g["DN 20"]["innen"]!.GetValue<double>());
        Assert.Equal(20, g["DN 20"]["dn"]!.GetValue<double>());
        Assert.True(g["DN 20"]["formteile"]!["bogen"]!["verfuegbar"]!.GetValue<bool>());
        Assert.True(g["DN 20"]["formteile"]!["tstueck"]!["verfuegbar"]!.GetValue<bool>());
        Assert.False(g["DN 20"]["formteile"]!["reinigungsoeffnung"]!["verfuegbar"]!.GetValue<bool>());
        Assert.False(string.IsNullOrEmpty(g["DN 20"]["formteile"]!["reinigungsoeffnung"]!["grund"]!.GetValue<string>()));
        var red50 = g["DN 50"]["formteile"]!["reduktion"]!.AsArray().Select(x => x!.AsObject()).ToDictionary(x => x["auf"]!.GetValue<string>());
        Assert.Equal(5, red50.Count);   // auf jede kleinere Grösse
        Assert.True(red50["DN 40"]["verfuegbar"]!.GetValue<bool>());
        Assert.Equal("DN 50/40", red50["DN 40"]["typ"]!.GetValue<string>());
        Assert.False(red50["DN 32"]["verfuegbar"]!.GetValue<bool>());
        Assert.Empty(g["DN 15"]["formteile"]!["reduktion"]!.AsArray());
        // keine Serie
        Assert.Throws<HlksFehler>(() => Tuer.Abfrage(p, "leitungsserie", new JsonObject { ["familie"] = "FAM-2" }));
        Assert.Contains(Abfragen.Liste, a => a.Name == "leitungsserie");
    }

    [Fact]
    public void FamilieAendern_LeitungSetzen()
    {
        var p = MitSerie(false);
        var l = (JsonObject)L(Familie("test_rohrserie")).DeepClone();
        l["regeln"] = new JsonObject { ["winkel"] = "frei" };
        Ok(p.Ausfuehren("familie_aendern", new JsonObject { ["familie"] = "FAM-1", ["ops"] = new JsonArray(new JsonObject { ["op"] = "leitung_setzen", ["leitung"] = l }) }));
        Assert.True(Serie(p).WinkelErlaubt(60));
        Assert.Equal("frei", Serie(p).WinkelText);
        var n = Nein(p.Ausfuehren("familie_aendern", new JsonObject { ["familie"] = "FAM-1", ["ops"] = new JsonArray(new JsonObject { ["op"] = "leitung_setzen" }) }));
        Assert.Contains("leitung_setzen", n.Fehler);
    }

    [Fact]
    public void ParameterUmbenennen_AuchImBlockLeitung()
    {
        var p = MitSerie();
        var d = Familie("test_rohrserie");
        L(d)["formteile"]!["reduktion"]!["typ"] = "if(and(dn = 25, dn_2 = 20), \"DN 25/20\", \"DN 20/15\")";   // FIKTIVE Regel
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["familie"] = "FAM-1", ["definition"] = d.DeepClone() }));
        Ok(p.Ausfuehren("familie_aendern", new JsonObject
        {
            ["familie"] = "FAM-1",
            ["ops"] = new JsonArray(
                new JsonObject { ["op"] = "parameter_umbenennen", ["alt"] = "dn", ["neu"] = "nw" },
                new JsonObject { ["op"] = "parameter_umbenennen", ["alt"] = "da", ["neu"] = "aussendurchmesser" }),
        }));
        var def = Familien.Definition(Hole(p, "FAM-1"), p.Katalog);
        Assert.True(def.Gueltig, string.Join(" | ", def.Fehler));
        Assert.Equal("nw", def.Leitung!.Groesse["dn"]);
        Assert.Equal("aussendurchmesser", def.Leitung.Aussen!.Text);
        Assert.Equal("if(and(nw = 25, nw_2 = 20), \"DN 25/20\", \"DN 20/15\")", def.Leitung.Formteile["reduktion"].TypText);
        Assert.Equal(25, Serie(p).Groesse("DN 20")!.Aussen);
        Assert.Equal("DN 25/20", Serie(p).Formteil(p.Modell, p.Katalog, "reduktion", Leitungsgroesse.Rohr(25), Leitungsgroesse.Rohr(20)).Typ);
    }

    [Fact]
    public void FamiliePlatzieren_LeitungsserieWirdNichtPlatziert()
    {
        var p = MitSerie(false);
        var n = Nein(p.Ausfuehren("familie_platzieren", new JsonObject { ["familie"] = "FAM-1", ["typ"] = "DN 20", ["punkt"] = new JsonArray(0, 0, 0), ["system"] = "KW" }));
        Assert.Contains("Leitungsserie", n.Fehler);
        Assert.Contains("leitung_ziehen", n.Vorschlag);
    }
}
