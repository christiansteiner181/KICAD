using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;
using static Hlks.Kern.Tests.RoutingHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3, Nachtrag B7: T-Stücke mit Abzweigwinkel (entwurf/modul-3.md «Lücke Abzweigwinkel») – regeln.abzweigwinkel,
/// lokale Lage von c bei Lc·(−cos w, sin w, 0), Formteilwahl mit Abzweigwinkel, leitung_abzweigen schräg und mit Gefälle,
/// leitung_zeichnen an c, P15 Abzweigwinkel. Handrechnungen aus den FIKTIVEN Testfamilien (tests/daten/familien, B7):
/// Testabwasserserie da = DN + 10 (DN 100: 110), Bogenwinkel 15/30/45/88.5, Abzweigwinkel 45/88.5; Abwasserbogen la = lb = z je Winkel
/// (DN 100: z88 = 90, z45 = 45); Abwasser-T unter 60° l = 1.5·da1, lc = 2·da1, sonst l = lc = da1 (DN 100: 45° → 165/220,
/// 88.5° → 110/110); Abwasserreduktion l = 60. Konstanten: cos 88.5° = 0.026176948, sin 88.5° = 0.999657325, cos 45° = 0.707106781,
/// arctan(0.02) = 1.1457628°. Keine Normwerte, keine echten Rohrabmessungen.</summary>
public class RoutingAbzweigwinkelTests
{
    const string Aw = "fam-test-abwasserserie";
    /// <summary>Lagen auf 0.001 mm: Anschlusslagen rundet der Kern auf 3 Stellen, das Routing rechnet ab diesen Lagen weiter.</summary>
    const double Tol = 1e-3;

    /// <summary>Projekt mit System SW (Schmutzwasser) und der Testabwasserserie (FAM-1) samt Bogen (FAM-2), T (FAM-3), Reduktion (FAM-4).</summary>
    static Projekt MitAbwasser(bool tstueck = true)
    {
        var p = Neu();
        MitSystem(p, "SW", "Schmutzwasser");
        foreach (var f in new[] { "test_abwasserserie", "test_abwasserbogen", "test_abwasser_t", "test_abwasserreduktion" })
            if (tstueck || f != "test_abwasser_t") Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Datei(f) }));
        return p;
    }

    static BefehlsErgebnis AwZeichnen(Projekt p, string groesse, double[][] punkte, JsonObject? extra = null, string system = "SW", bool trocken = false) =>
        Zeichnen(p, groesse, punkte, extra, serie: Aw, system: system, trocken: trocken);

    static BefehlsErgebnis Abzweigen(Projekt p, JsonObject par, bool trocken = false) => p.Ausfuehren("leitung_abzweigen", par, trocken);

    static Leitungsserie AwSerie(Projekt p) => Leitungsserie.Hole(p.Modell, p.Katalog, Aw);

    static double Grad(V3 a, V3 b) => Math.Acos(Math.Clamp(a.Einheit().Mal(b.Einheit()), -1, 1)) * 180 / Math.PI;

    static double? ExemplarWinkel(Projekt p, string kz) => Json.Zahl(Familien.Exemplarwerte(Hole(p, kz))?["winkel"]);

    static void Fehler(JsonObject d, string stelle, string text)
    {
        var f = FamilieDef.Lesen(d, Kat);
        Assert.False(f.Gueltig, "hätte ungültig sein sollen");
        Assert.Contains(f.Fehler, x => x.Stelle.StartsWith(stelle) && x.Meldung.Contains(text));
        Assert.All(f.Fehler.Where(x => x.Stelle.StartsWith(stelle)), x => Assert.False(string.IsNullOrWhiteSpace(x.Vorschlag), $"Vorschlag fehlt: {x}"));
    }

    static JsonObject SerieMit(JsonNode? abzweigwinkel)
    {
        var d = Familie("test_abwasserserie");
        d["leitung"]!["regeln"]!["abzweigwinkel"] = abzweigwinkel;
        return d;
    }

    // ------------------------------------------------------------------ Familien und regeln.abzweigwinkel

    [Theory]
    [InlineData("test_abwasserserie", "Rohr", "fam-test-abwasserserie", 4)]
    [InlineData("test_abwasserbogen", "Bogen", "fam-test-abwasserbogen", 4)]
    [InlineData("test_abwasser_t", "TStueck", "fam-test-abwasser-t", 6)]
    [InlineData("test_abwasserreduktion", "Reduktion", "fam-test-abwasserreduktion", 4)]
    public void Testfamilien_SindGueltigUndFiktiv(string datei, string kategorie, string id, int typen)
    {
        var d = Familie(datei);
        Assert.Equal(id, d["id"]!.GetValue<string>());
        Assert.Equal(kategorie, d["kategorie"]!.GetValue<string>());
        Assert.Contains("FIKTIV", d["beschreibung"]!.GetValue<string>());
        var def = FamilieDef.Lesen(d, Kat);
        Assert.True(def.Gueltig, string.Join(" | ", def.Fehler));
        Assert.Equal(typen, def.Typen.Count);
    }

    [Fact]
    public void Serie_LiestAbzweigwinkel_OhneAngabeGilt90()
    {
        var l = FamilieDef.Lesen(Familie("test_abwasserserie"), Kat).Leitung!;
        Assert.Equal([45.0, 88.5], l.Abzweigwinkel!);
        Assert.Equal([15.0, 30, 45, 88.5], l.Winkel!);
        var p = MitAbwasser();
        Assert.Equal("45°, 88.5°", AwSerie(p).AbzweigwinkelText);
        Assert.True(AwSerie(p).AbzweigwinkelErlaubt(88.5));
        Assert.False(AwSerie(p).AbzweigwinkelErlaubt(90));
        // bestehende Testrohrserie ohne abzweigwinkel: null gelesen, als [90] verwendet
        Assert.Null(FamilieDef.Lesen(Familie("test_rohrserie"), Kat).Leitung!.Abzweigwinkel);
        var t = MitSerie();
        Assert.Equal([90.0], Serie(t).Abzweigwinkel);
        Assert.Equal("90°", Serie(t).AbzweigwinkelText);
    }

    [Fact]
    public void Abzweigwinkel_UngueltigeAngaben_MitVorschlag()
    {
        const string Ort = "leitung.regeln.abzweigwinkel";
        Fehler(SerieMit(new JsonArray(0, 45)), Ort, "0 < w ≤ 90");
        Fehler(SerieMit(new JsonArray(45, 95)), Ort, "0 < w ≤ 90");
        Fehler(SerieMit(new JsonArray(45, 45)), Ort, "doppelt");
        Fehler(SerieMit(new JsonArray("x")), Ort, "keine Zahl");
        Fehler(SerieMit(new JsonArray()), Ort, "leer");
        Fehler(SerieMit("frei"), Ort, "keine Liste");
        Assert.True(FamilieDef.Lesen(SerieMit(new JsonArray(90)), Kat).Gueltig);
        Assert.True(FamilieDef.Lesen(SerieMit(new JsonArray(30, 45, 60, 88.5, 90)), Kat).Gueltig);
    }

    // ------------------------------------------------------------------ Lokale Lage von c

    /// <summary>DN 100, w = 45: da1 = 110, l = 1.5·110 = 165, lc = 2·110 = 220 → c = 220·(−cos 45°, sin 45°, 0) = (−155.563492, 155.563492, 0).
    /// w = 88.5: l = lc = 110 → c = 110·(−0.026176948, 0.999657325, 0) = (−2.879464, 109.962306, 0). Ohne Parameter winkel
    /// (Test-T-Stück, DN 20, lc = da = 25): w = 90, c (0, 25, 0).</summary>
    [Fact]
    public void LokaleLage_C_Bei45Und88_5_OhneWinkel90()
    {
        var def = FamilieDef.Lesen(Familie("test_abwasser_t"), Kat);
        var g45 = Formteilgeometrie.Lokal(def, "DN 100", new JsonObject { ["winkel"] = 45 }, "tstueck");
        Etwa(45, g45.Winkel!.Value, 1e-9);
        Etwa([-165, 0, 0], g45.Anschluss("a").Lage);
        Etwa([165, 0, 0], g45.Anschluss("b").Lage);
        Etwa([-155.563492, 155.563492, 0], g45.Anschluss("c").Lage, 1e-6);
        Etwa([-0.707106781, 0.707106781, 0], g45.Anschluss("c").Richtung, 1e-9);
        Etwa(220, g45.Einbaulaenge("c"), 1e-6);
        Etwa(45, Formteilgeometrie.Abzweigwinkel(g45.Anschluss("c").Richtung), 1e-9);

        var g88 = Formteilgeometrie.Lokal(def, "DN 100", new JsonObject { ["winkel"] = 88.5 }, "tstueck");
        Etwa(88.5, g88.Winkel!.Value, 1e-9);
        Etwa([-110, 0, 0], g88.Anschluss("a").Lage);
        Etwa([-2.879464, 109.962306, 0], g88.Anschluss("c").Lage, 1e-6);
        Etwa([-0.026176948, 0.999657325, 0], g88.Anschluss("c").Richtung, 1e-9);
        Etwa(110, g88.Einbaulaenge("c"), 1e-6);

        // reduzierter Abzweig DN 100/50: c DN 50 an derselben Lage (lc = da1)
        var red = Formteilgeometrie.Lokal(def, "DN 100/50", new JsonObject { ["winkel"] = 88.5 }, "tstueck");
        Assert.Equal(50.0, red.Anschluss("c").Groesse.Dn);
        Assert.Equal(100.0, red.Anschluss("a").Groesse.Dn);
        Etwa([-2.879464, 109.962306, 0], red.Anschluss("c").Lage, 1e-6);

        // ohne Parameter winkel: 90°, c (0, lc, 0)/(0, 1, 0) wie bisher
        var alt = Formteilgeometrie.Lokal(FamilieDef.Lesen(Familie("test_tstueck"), Kat), "DN 20", null, "tstueck");
        Etwa(90, alt.Winkel!.Value, 1e-9);
        Etwa([0, 25, 0], alt.Anschluss("c").Lage);
        Etwa([0, 1, 0], alt.Anschluss("c").Richtung);
    }

    [Fact]
    public void LokaleLage_WinkelUndGeometriePassenNicht_Abgelehnt()
    {
        // FIKTIV: Richtung von c fest auf 45°, winkel aber 88.5 → c liegt nicht nach der Formel
        var d = Familie("test_abwasser_t");
        d["anschluesse"]![2]!["richtung"] = new JsonArray(-1, 1, 0);
        var def = FamilieDef.Lesen(d, Kat);
        Assert.True(def.Gueltig, string.Join(" | ", def.Fehler));
        var f = Assert.Throws<HlksFehler>(() => Formteilgeometrie.Lokal(def, "DN 100", new JsonObject { ["winkel"] = 88.5 }, "tstueck"));
        Assert.Contains("Anschluss c zeigt nach", f.Message);
        Assert.Contains("Lc·(−cos w, sin w, 0)", f.Vorschlag);
        // winkel über 90 → nicht erlaubt
        var f2 = Assert.Throws<HlksFehler>(() => Formteilgeometrie.Lokal(FamilieDef.Lesen(Familie("test_abwasser_t"), Kat), "DN 100", new JsonObject { ["winkel"] = 120 }, "tstueck"));
        Assert.Contains("0 < w ≤ 90", f2.Message);
    }

    // ------------------------------------------------------------------ Formteilwahl

    /// <summary>regeln.mischbar (30.10.2026, Christian: «geberit silent und pe können gemischt werden»): fehlt das eigene T-Stück, nimmt die
    /// Formteilwahl das der mischbaren Serie; ohne mischbar bleibt es abgelehnt.</summary>
    [Fact]
    public void Formteilwahl_MischbareSerie_LiefertFehlendesFormteil()
    {
        var p = MitAbwasser();
        var a = Familie("test_abwasserserie");
        a["id"] = "fam-test-abwasserserie-ohne-t"; a["name"] = "Testabwasserserie ohne T (fiktiv)";
        a["leitung"]!["formteile"]!["tstueck"] = new JsonObject { ["familie"] = "fam-test-fehlt" };
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = a.DeepClone() }));
        var dn100 = Leitungsgroesse.Rohr(100);
        var ohne = Leitungsserie.Hole(p.Modell, p.Katalog, "fam-test-abwasserserie-ohne-t").Formteil(p.Modell, p.Katalog, "tstueck", dn100, dn100, 88.5);
        Assert.False(ohne.Ok);

        a["leitung"]!["regeln"]!["mischbar"] = new JsonArray("fam-test-abwasserserie");
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = a, ["familie"] = "fam-test-abwasserserie-ohne-t" }));
        var w = Leitungsserie.Hole(p.Modell, p.Katalog, "fam-test-abwasserserie-ohne-t").Formteil(p.Modell, p.Katalog, "tstueck", dn100, dn100, 88.5);
        Assert.True(w.Ok, w.Fehler);
        Assert.Equal("fam-test-abwasser-t", Familien.DefinitionsId(w.Familie!));
        Assert.Equal("DN 100", w.Typ);
        Assert.Contains("mischbaren Serie", w.Hinweis);
    }

    [Fact]
    public void Formteilwahl_Abzweigwinkel_ToleranzExemplarUndAblehnung()
    {
        var p = MitAbwasser();
        var s = AwSerie(p);
        var dn100 = Leitungsgroesse.Rohr(100);
        // 88.854° (Zulauf 2 % an senkrechten Fallstrang) → 88.5° (Abweichung 0.354° ≤ 1°)
        var w = s.Formteil(p.Modell, p.Katalog, "tstueck", dn100, dn100, 88.8542372);
        Assert.True(w.Ok, w.Fehler);
        Assert.Equal("DN 100", w.Typ);
        Etwa(88.5, w.Abzweigwinkel!.Value, 1e-9);
        Assert.Equal(88.5, w.Exemplar!["winkel"]!.GetValue<double>());
        Etwa(110, w.Geometrie!.Einbaulaenge("a")); Etwa(110, w.Geometrie.Einbaulaenge("c"));
        // 45.3° → 45°: l = 165, lc = 220
        w = s.Formteil(p.Modell, p.Katalog, "tstueck", dn100, dn100, 45.3);
        Assert.True(w.Ok, w.Fehler);
        Etwa(45, w.Abzweigwinkel!.Value, 1e-9);
        Etwa(165, w.Geometrie!.Einbaulaenge("b")); Etwa(220, w.Geometrie.Einbaulaenge("c"));
        // reduzierter Abzweig DN 100/50
        w = s.Formteil(p.Modell, p.Katalog, "tstueck", dn100, Leitungsgroesse.Rohr(50), 88.5);
        Assert.True(w.Ok, w.Fehler);
        Assert.Equal("DN 100/50", w.Typ);
        // 60° bzw. 89.6° (1.1° neben 88.5, Toleranz seit 19.10.2026 ±1°) → abgelehnt mit den erlaubten Winkeln
        var nein = s.Formteil(p.Modell, p.Katalog, "tstueck", dn100, dn100, 60);
        Assert.False(nein.Ok);
        Assert.Contains("unter 60°", nein.Fehler);
        Assert.Contains("45°, 88.5°", nein.Fehler);
        Assert.Contains("abzweigwinkel", nein.Vorschlag);
        Assert.False(s.Formteil(p.Modell, p.Katalog, "tstueck", dn100, dn100, 89.6).Ok);
        Assert.True(s.Formteil(p.Modell, p.Katalog, "tstueck", dn100, dn100, 89.1).Ok);   // 0.6° neben 88.5 – innerhalb ±1°
        // ohne Winkel = rechtwinklig 90° – in dieser Serie nicht erlaubt
        Assert.Contains("unter 90°", s.Formteil(p.Modell, p.Katalog, "tstueck", dn100, dn100).Fehler);
        // reduzierter Abzweig ohne Typ (DN 125/50 gibt es nicht) → Meldung nennt den Winkel
        nein = s.Formteil(p.Modell, p.Katalog, "tstueck", Leitungsgroesse.Rohr(125), Leitungsgroesse.Rohr(50), 45);
        Assert.False(nein.Ok);
        Assert.Contains("Abzweig 45°", nein.Fehler);
    }

    [Fact]
    public void Formteilwahl_Trinkwasser_Unveraendert90()
    {
        var p = MitSerie();
        var w = Serie(p).Formteil(p.Modell, p.Katalog, "tstueck", Leitungsgroesse.Rohr(20), Leitungsgroesse.Rohr(20));
        Assert.True(w.Ok, w.Fehler);
        Assert.Null(w.Exemplar);   // Test-T-Stück ohne Parameter winkel
        Etwa(90, w.Abzweigwinkel!.Value);
        Etwa([0, 25, 0], w.Geometrie!.Anschluss("c").Lage);
        var nein = Serie(p).Formteil(p.Modell, p.Katalog, "tstueck", Leitungsgroesse.Rohr(20), Leitungsgroesse.Rohr(20), 45);
        Assert.False(nein.Ok);
        Assert.Contains("Abzweigwinkel: 90°", nein.Fehler);
    }

    /// <summary>FIKTIVE T-Familie mit dem Abzweigwinkel als Typparameter (ein Typ je Winkel): gewählt wird der Typ, dessen c unter
    /// dem Winkel liegt; ein Exemplarwert entfällt.</summary>
    [Fact]
    public void Formteilwahl_WinkelAlsTypparameter()
    {
        var p = MitAbwasser(tstueck: false);
        var d = Familie("test_abwasser_t");
        foreach (var par in d["parameter"]!.AsArray())
            if (par!["name"]!.GetValue<string>() is "winkel" or "l" or "lc") par["art"] = "typ";
        d["parameter"]!.AsArray().First(x => x!["name"]!.GetValue<string>() == "winkel")!.AsObject().Remove("wert");
        d["typen"] = new JsonArray(
            new JsonObject { ["name"] = "DN 100 45°", ["werte"] = new JsonObject { ["dn1"] = 100, ["dn2"] = 100, ["winkel"] = 45 } },
            new JsonObject { ["name"] = "DN 100 88.5°", ["werte"] = new JsonObject { ["dn1"] = 100, ["dn2"] = 100, ["winkel"] = 88.5 } });
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d.DeepClone() }));
        var s = AwSerie(p);
        var dn100 = Leitungsgroesse.Rohr(100);
        var w45 = s.Formteil(p.Modell, p.Katalog, "tstueck", dn100, dn100, 45);
        Assert.True(w45.Ok, w45.Fehler);
        Assert.Equal("DN 100 45°", w45.Typ);
        Assert.Null(w45.Exemplar);
        Assert.Null(w45.Hinweis);   // eindeutig, nicht «mehrere Typen passen»
        var w88 = s.Formteil(p.Modell, p.Katalog, "tstueck", dn100, dn100, 88.7);
        Assert.True(w88.Ok, w88.Fehler);
        Assert.Equal("DN 100 88.5°", w88.Typ);
        Etwa([-2.879464, 109.962306, 0], w88.Geometrie!.Anschluss("c").Lage, 1e-6);
    }

    // ------------------------------------------------------------------ leitung_abzweigen: Hauptfall Fallstrang

    /// <summary>Hauptfall: senkrechter Fallstrang DN 100 von (0, 0, 2800) nach (0, 0, 0) (Fliessrichtung abwärts, d = (0, 0, −1)), Zulauf
    /// DN 100 bei z = 1500 waagrecht nach +x gezeichnet bis x = 2000 mit 2 % Gefälle (zum T-Stück hin fallend).
    /// Zulauf in Fliessrichtung (1, 0, −0.02)/|…|: Winkel zum Fallstrang = 90° − arctan(0.02) = 88.854° → Abzweigwinkel 88.5° (±0.5°).
    /// T DN 100, 88.5°: l = lc = 110. Ausrichtung x = d = (0, 0, −1), y = (1, 0, 0), z = x × y = (0, −1, 0).
    /// RO-1: 2800 → 1500 + 110 = 1610; RO-1-2: 1500 − 110 = 1390 → 0.
    /// c = q + 110·(−cos 88.5°·x + sin 88.5°·y) = (0, 0, 1500) + 110·(0.999657325, 0, 0.026176948) = (109.962306, 0, 1502.879464).
    /// Zulauf RO-2 ab c mit 2 %: waagrechte Länge 2000 − 109.962306 = 1890.037694, Δz = 0.02 · 1890.037694 = 37.800754
    /// → Anfang (2000, 0, 1540.680218). Achse von c steigt 1.5°, der Zulauf 1.146° → Abweichung 0.354° (verbunden bis 1°).</summary>
    [Fact]
    public void Fallstrang_ZulaufMit2Prozent_Abzweig88_5()
    {
        var p = MitAbwasser();
        Ok(AwZeichnen(p, "DN 100", [[0, 0, 2800], [0, 0, 0]]));
        var e = Ok(Abzweigen(p, new JsonObject
        {
            ["leitung"] = "RO-1", ["punkt"] = new JsonArray(0, 0, 1500), ["punkte"] = Punkte([2000, 0, 1500]), ["gefaelle"] = 2,
        }));
        var ts = Hole(p, "TS-1");
        Assert.Equal("DN 100", Familien.Typ(ts));
        Etwa(88.5, ExemplarWinkel(p, "TS-1")!.Value, 1e-9);
        Etwa([0, 0, 1500], Familien.Einfuegepunkt(ts));
        var lage = Familien.Einbaulage(ts)!;
        Etwa([0, 0, -1], lage.X.Feld(), 1e-9);
        Etwa([0, -1, 0], lage.Z.Feld(), 1e-9);
        Gerade(p, "RO-1", [0, 0, 2800], [0, 0, 1610], Tol);
        Gerade(p, "RO-1-2", [0, 0, 1390], [0, 0, 0], Tol);
        var c = Stelle(p, "TS-1", "c");
        Etwa([109.962306, 0, 1502.879464], c.Lage, Tol);
        Etwa([0.999657325, 0, 0.026176948], c.Richtung, 1e-6);
        Gerade(p, "RO-2", [2000, 0, 1540.680218], [109.962306, 0, 1502.879464], Tol);
        Assert.Equal("DN 100", Familien.Typ(Hole(p, "RO-2")));
        Etwa(2, Hole(p, "RO-2").Zahl("gefaelle")!.Value);
        // Gefälle aus dem z-Verlauf: 37.800754 / 1890.037694 = 2 %
        var pt = Pkt(p, "RO-2");
        Etwa(2, (pt[0][2] - pt[1][2]) / (pt[0][0] - pt[1][0]) * 100, 1e-6);
        // Zulauf gegen die Achse von c: 1.5° − 1.1457628° = 0.354° ≤ 1°
        Etwa(0.3542372, Grad(V3.Aus(pt[0]) - V3.Aus(pt[1]), V3.Aus(c.Richtung!)), 1e-5);
        IstVerbunden(p, "RO-1", "ende", "TS-1", "a");
        IstVerbunden(p, "TS-1", "b", "RO-1-2", "anfang");
        IstVerbunden(p, "RO-2", "ende", "TS-1", "c");
        Sauber(p);
        Assert.Empty(Befunde(p, "P16"));
        Assert.Contains(e.Hinweise, h => h.Contains("T-Stück TS-1 Typ «DN 100» bei [0, 0, 1500], Abzweigwinkel 88.5° (Verlauf 88.854°)"));
        Assert.Contains(e.Hinweise, h => h.Contains("RO-1 geteilt in RO-1 (1190 mm) und RO-1-2 (1390 mm)"));
    }

    [Fact]
    public void Fallstrang_Trockenlauf_UndRueckgaengig()
    {
        var p = MitAbwasser();
        Ok(AwZeichnen(p, "DN 100", [[0, 0, 2800], [0, 0, 0]]));
        var vorher = Inhalt(p);
        var par = new JsonObject { ["leitung"] = "RO-1", ["punkt"] = new JsonArray(0, 0, 1500), ["punkte"] = Punkte([2000, 0, 1500]), ["gefaelle"] = 2 };
        var t = Ok(Abzweigen(p, par, trocken: true));
        Assert.Equal(vorher, Inhalt(p));
        Assert.Contains(t.Hinweise, h => h.Contains("Abzweigwinkel 88.5°"));
        Ok(Abzweigen(p, (JsonObject)par.DeepClone()));
        Assert.NotEqual(vorher, Inhalt(p));
        Ok(p.Rueckgaengig());
        Assert.Equal(vorher, Inhalt(p));
    }

    /// <summary>Reduzierter Abzweig: Fallstrang DN 100, Zulauf DN 50 von −y mit 2 % → T «DN 100/50», 88.5°, lc = da1 = 110 (c DN 50).
    /// y = (0, −1, 0), z = x × y = (0, 0, −1) × (0, −1, 0) = (−1, 0, 0). c = (0, −109.962306, 1502.879464); Zulauf ab y = −1500:
    /// Δz = 0.02 · (1500 − 109.962306) = 27.800754 → Anfang (0, −1500, 1530.680218). Keine Reduktion.</summary>
    [Fact]
    public void Fallstrang_ReduzierterAbzweig_DN100_50()
    {
        var p = MitAbwasser();
        Ok(AwZeichnen(p, "DN 100", [[0, 0, 2800], [0, 0, 0]]));
        Ok(Abzweigen(p, new JsonObject
        {
            ["leitung"] = "RO-1", ["punkt"] = new JsonArray(0, 0, 1500), ["punkte"] = Punkte([0, -1500, 1500]), ["groesse"] = "DN 50", ["gefaelle"] = 2,
        }));
        Assert.Equal("DN 100/50", Familien.Typ(Hole(p, "TS-1")));
        Etwa(88.5, ExemplarWinkel(p, "TS-1")!.Value, 1e-9);
        Etwa([-1, 0, 0], Familien.Einbaulage(Hole(p, "TS-1"))!.Z.Feld(), 1e-9);
        var c = Stelle(p, "TS-1", "c");
        Assert.Equal(50.0, c.Groesse.Dn);
        Etwa([0, -109.962306, 1502.879464], c.Lage, Tol);
        Gerade(p, "RO-2", [0, -1500, 1530.680218], [0, -109.962306, 1502.879464], Tol);
        Assert.Equal("DN 50", Familien.Typ(Hole(p, "RO-2")));
        Assert.Null(p.Modell.Finde("RD-1"));
        IstVerbunden(p, "RO-2", "ende", "TS-1", "c");
        Sauber(p);
        Assert.Empty(Befunde(p, "P16"));
    }

    // ------------------------------------------------------------------ leitung_abzweigen: 45° in geneigte Sammelleitung

    /// <summary>Sammelleitung DN 100 mit 1 % von (0, 0, 1000) nach (6000, 0, 940): d = (6000, 0, −60)/6000.3 = (0.999950, 0, −0.010000).
    /// Abzweig bei x = 3000 (Punkt ohne z, auf die Achse projiziert: q = (3000, 0, 970)), Zulauf von hinten links: erster Punkt
    /// (2000, 1000) waagrecht, 2 % Gefälle → u = (−cos 45°·cos α, cos 45°·cos α, sin α) = (−0.706965, 0.706965, 0.019996), α = 1.1457628°.
    /// Winkel d ↔ −u = 44.998° → Abzweigwinkel 45° (T DN 100: l = 165, lc = 220). T um die Leitungsachse gedreht: y = Anteil von u
    /// senkrecht zu d = (0.000183, 0.999833, 0.018279); c-Richtung = −cos 45°·d + sin 45°·y = (−0.706942, 0.706989, 0.019996);
    /// c = q + 220·c-Richtung = (2844.472722, 155.537497, 974.399163). RO-1 bis q − 165·d = (2835.008249, 0, 971.649918),
    /// RO-1-2 ab q + 165·d = (3164.991751, 0, 968.350082). Zulauf: waagrechte Länge (2000, 1000) ↔ c = 1194.257550,
    /// Δz = 0.02 · 1194.257550 = 23.885151 → Anfang (2000, 1000, 998.284314).</summary>
    [Fact]
    public void Sammelleitung1Prozent_Abzweig45_Zulauf2Prozent()
    {
        var p = MitAbwasser();
        Ok(AwZeichnen(p, "DN 100", [[0, 0, 1000], [6000, 0, 1000]], new JsonObject { ["gefaelle"] = 1 }));
        Gerade(p, "RO-1", [0, 0, 1000], [6000, 0, 940], Tol);
        var e = Ok(Abzweigen(p, new JsonObject
        {
            ["leitung"] = "RO-1", ["punkt"] = new JsonArray(3000, 0), ["punkte"] = Punkte([2000, 1000]), ["gefaelle"] = 2,
        }));
        var ts = Hole(p, "TS-1");
        Assert.Equal("DN 100", Familien.Typ(ts));
        Etwa(45, ExemplarWinkel(p, "TS-1")!.Value, 1e-9);
        var q = new V3(3000, 0, 970);
        Etwa(q.Feld(), Familien.Einfuegepunkt(ts), Tol);
        Gerade(p, "RO-1", [0, 0, 1000], [2835.008249, 0, 971.649918], Tol);
        Gerade(p, "RO-1-2", [3164.991751, 0, 968.350082], [6000, 0, 940], Tol);
        var c = Stelle(p, "TS-1", "c");
        Etwa([2844.472722, 155.537497, 974.399163], c.Lage, Tol);
        Etwa([-0.706942, 0.706989, 0.019996], c.Richtung, 1e-6);
        // genau 45° zwischen Leitung in Fliessrichtung und Zulauf (gegen c) und c in der Ebene von Leitung und Abzweig
        var d = new V3(6000, 0, -60).Einheit();
        Etwa(45, Grad(d, -V3.Aus(c.Richtung!)), 1e-6);
        var u = new V3(-Math.Sqrt(0.5) * Math.Cos(Math.Atan(0.02)), Math.Sqrt(0.5) * Math.Cos(Math.Atan(0.02)), Math.Sin(Math.Atan(0.02)));
        var normale = d.Kreuz(u).Einheit();
        Etwa(0, V3.Aus(c.Richtung!).Mal(normale), 1e-9);
        Etwa(0, (V3.Aus(c.Lage!) - q).Mal(normale), Tol);   // Lage auf 0.001 mm gerundet
        Gerade(p, "RO-2", [2000, 1000, 998.284314], [2844.472722, 155.537497, 974.399163], Tol);
        IstVerbunden(p, "RO-1", "ende", "TS-1", "a");
        IstVerbunden(p, "TS-1", "b", "RO-1-2", "anfang");
        IstVerbunden(p, "RO-2", "ende", "TS-1", "c");
        Sauber(p);
        Assert.Empty(Befunde(p, "P16"));   // RO-1, RO-1-2 mit 1 %, RO-2 mit 2 %
        Assert.Contains(e.Hinweise, h => h.Contains("Abzweigwinkel 45° (Verlauf 44.998°)"));
    }

    // ------------------------------------------------------------------ Bogen statt T: Zulauf über 88.5°-Bogen auf den Fallstrang

    /// <summary>leitung_zeichnen DN 100, 2 %: (0, 0, 2900) → (2000, 0, 2900) → (2000, 0, 100). Waagrecht fällt um 40 → P1 (2000, 0, 2860),
    /// das senkrechte Stück behält −2800 → P2 (2000, 0, 60). Knick = arccos(40 / 2000.4) = 88.854° → Bogen 88.5° (la = lb = z88 = 90).
    /// x = (0.999800, 0, −0.019996), y = (−0.019996, 0, −0.999800): a = P1 − 90·x = (1910.017995, 0, 2861.799640);
    /// b = P1 + 90·(cos 88.5°·x + sin 88.5°·y) = P1 + 90·(0.006183, 0, −0.999981) = (2000.556431, 0, 2770.001720).</summary>
    [Fact]
    public void Zulauf2Prozent_Ueber88_5Bogen_AufFallstrang()
    {
        var p = MitAbwasser();
        var e = Ok(AwZeichnen(p, "DN 100", [[0, 0, 2900], [2000, 0, 2900], [2000, 0, 100]], new JsonObject { ["gefaelle"] = 2 }));
        var bo = Hole(p, "BO-1");
        Assert.Equal("DN 100", Familien.Typ(bo));
        Etwa(88.5, ExemplarWinkel(p, "BO-1")!.Value, 1e-9);
        Etwa([2000, 0, 2860], Familien.Einfuegepunkt(bo), Tol);
        Etwa([1910.017995, 0, 2861.799640], Stelle(p, "BO-1", "a").Lage, Tol);
        Etwa([2000.556431, 0, 2770.001720], Stelle(p, "BO-1", "b").Lage, Tol);
        Gerade(p, "RO-1", [0, 0, 2900], [1910.017995, 0, 2861.799640], Tol);
        Gerade(p, "RO-2", [2000.556431, 0, 2770.001720], [2000, 0, 60], Tol);
        Assert.Null(p.Modell.Finde("TS-1"));
        IstVerbunden(p, "RO-1", "ende", "BO-1", "a");
        IstVerbunden(p, "BO-1", "b", "RO-2", "anfang");
        Sauber(p);
        Assert.Empty(Befunde(p, "P16"));
        Assert.Contains(e.Hinweise, h => h.Contains("Bogen BO-1 Typ «DN 100», 88.5° (Knick 88.854°)"));
    }

    // ------------------------------------------------------------------ Ablehnungen

    [Fact]
    public void Ablehnung_NichtErlaubterWinkel_UndGegenDieFliessrichtung()
    {
        var p = MitAbwasser();
        Ok(AwZeichnen(p, "DN 100", [[0, 0, 1000], [6000, 0, 1000]]));
        var vorher = Inhalt(p);
        // Zulauf unter 60°: u = (−0.5, 0.866, 0) → Winkel d ↔ −u = 60°
        var nein = Nein(Abzweigen(p, new JsonObject
        {
            ["leitung"] = "RO-1", ["punkt"] = new JsonArray(3000, 0, 1000), ["richtung"] = new JsonArray(-0.5, Math.Sqrt(3) / 2, 0), ["laenge"] = 1000,
        }));
        Assert.Contains("unter 60°", nein.Fehler);
        Assert.Contains("Abzweigwinkel 45°, 88.5°", nein.Fehler);
        Assert.Contains("erlaubten Abzweigwinkel", nein.Vorschlag);
        // Zulauf gegen die Fliessrichtung (zeigt in Fliessrichtung vom T weg): Winkel 135°
        nein = Nein(Abzweigen(p, new JsonObject
        {
            ["leitung"] = "RO-1", ["punkt"] = new JsonArray(3000, 0, 1000), ["richtung"] = new JsonArray(1, 1, 0), ["laenge"] = 1000,
        }));
        Assert.Contains("unter 135°", nein.Fehler);
        Assert.Contains("mündet in Fliessrichtung ein", nein.Fehler);
        // rechtwinklig (90°) gibt es in der Testabwasserserie nicht (nur 45 und 88.5; 90 − 88.5 = 1.5° > 0.5°)
        nein = Nein(Abzweigen(p, new JsonObject
        {
            ["leitung"] = "RO-1", ["punkt"] = new JsonArray(3000, 0, 1000), ["richtung"] = new JsonArray(0, 1, 0), ["laenge"] = 1000,
        }));
        Assert.Contains("unter 90°", nein.Fehler);
        Assert.Equal(vorher, Inhalt(p));
    }

    // ------------------------------------------------------------------ Trinkwasser und zuführende Systeme

    [Fact]
    public void Trinkwasser_AbzweigUnveraendertRechtwinklig()
    {
        var p = MitSerie();
        Ok(Zeichnen(p, "DN 32", [[0, 0, 1000], [4000, 0, 1000]]));
        var vorher = Inhalt(p);
        var nein = Nein(Abzweigen(p, new JsonObject { ["leitung"] = "RO-1", ["punkt"] = new JsonArray(1500, 0, 1000), ["richtung"] = new JsonArray(1, 1, 0), ["laenge"] = 500 }));
        Assert.Contains("unter 45°", nein.Fehler);
        Assert.Contains("Abzweigwinkel 90°", nein.Fehler);
        Assert.Equal(vorher, Inhalt(p));
        var e = Ok(Abzweigen(p, new JsonObject { ["leitung"] = "RO-1", ["punkt"] = new JsonArray(1500, 0, 1000), ["richtung"] = new JsonArray(0, 1, 0), ["laenge"] = 1000 }));
        // wie bisher: T DN 32 (l = lc = 37), c bei (1500, 37, 1000), Ausrichtung x = (1, 0, 0), z = (0, 0, 1), ohne Exemplarwerte
        var ts = Hole(p, "TS-1");
        Assert.Null(Familien.Exemplarwerte(ts));
        Etwa([1500, 37, 1000], Stelle(p, "TS-1", "c").Lage);
        Etwa([1, 0, 0], Familien.Einbaulage(ts)!.X.Feld(), 1e-9);
        Etwa([0, 0, 1], Familien.Einbaulage(ts)!.Z.Feld(), 1e-9);
        Gerade(p, "RO-1", [0, 0, 1000], [1463, 0, 1000]);
        Gerade(p, "RO-1-2", [1537, 0, 1000], [4000, 0, 1000]);
        Gerade(p, "RO-2", [1500, 37, 1000], [1500, 1000, 1000]);
        Assert.DoesNotContain(e.Hinweise, h => h.Contains("Abzweigwinkel"));
        Sauber(p);
    }

    /// <summary>Zuführendes System (Trinkwasser kalt) an der Abwasser-Testserie (FIKTIV), Leitung (0, 0, 1000) → (6000, 0, 1000), Abzweig bei
    /// q = (3000, 0, 1000) unter 45° in Fliessrichtung (u = (0.707107, 0.707107, 0)): das T-Stück wird gespiegelt (x = −d = (−1, 0, 0),
    /// Durchgang b → a, l = 165, lc = 220): b bei q + 165·x = (2835, 0, 1000) an RO-1, a bei q − 165·x = (3165, 0, 1000) an RO-1-2;
    /// y = (0, 1, 0); c-Richtung = −cos 45°·x + sin 45°·y = (0.707107, 0.707107, 0) = u; c = q + 220·u = (3155.563492, 155.563492, 1000);
    /// Abzweig RO-2 von c bis q + 1500·u = (4060.660172, 1060.660172, 1000).</summary>
    [Fact]
    public void Zufuehrend_Abzweig45_Gespiegelt()
    {
        var p = MitAbwasser();
        MitSystem(p, "KW", "Trinkwasser kalt");
        Ok(AwZeichnen(p, "DN 100", [[0, 0, 1000], [6000, 0, 1000]], system: "KW"));
        var e = Ok(Abzweigen(p, new JsonObject
        {
            ["leitung"] = "RO-1", ["punkt"] = new JsonArray(3000, 0, 1000), ["richtung"] = new JsonArray(1, 1, 0), ["laenge"] = 1500,
        }));
        var ts = Hole(p, "TS-1");
        Etwa(45, ExemplarWinkel(p, "TS-1")!.Value, 1e-9);
        Etwa([-1, 0, 0], Familien.Einbaulage(ts)!.X.Feld(), 1e-9);
        Etwa([2835, 0, 1000], Stelle(p, "TS-1", "b").Lage, Tol);
        Etwa([3165, 0, 1000], Stelle(p, "TS-1", "a").Lage, Tol);
        Etwa([3155.563492, 155.563492, 1000], Stelle(p, "TS-1", "c").Lage, Tol);
        Gerade(p, "RO-1", [0, 0, 1000], [2835, 0, 1000], Tol);
        Gerade(p, "RO-1-2", [3165, 0, 1000], [6000, 0, 1000], Tol);
        Gerade(p, "RO-2", [3155.563492, 155.563492, 1000], [4060.660172, 1060.660172, 1000], Tol);
        IstVerbunden(p, "RO-1", "ende", "TS-1", "b");
        IstVerbunden(p, "TS-1", "a", "RO-1-2", "anfang");
        IstVerbunden(p, "TS-1", "c", "RO-2", "anfang");   // Versorgung: vom T-Stück weg
        Sauber(p);
        Assert.Contains(e.Hinweise, h => h.Contains("gespiegelt"));
    }

    // ------------------------------------------------------------------ leitung_zeichnen an c (nach bzw. von)

    /// <summary>Zuerst nur das T-Stück (richtung ohne laenge, gefaelle 2 → 88.5°, c wie im Hauptfall bei (109.962306, 0, 1502.879464)),
    /// dann leitung_zeichnen nach = TS-1.c mit 2 %, der ferne Punkt waagrecht auf der Höhe des Abzweigpunkts (z = 1500, also 2.879 mm
    /// unter c): das Stück liegt zwischen der Waagrechten durch c und der Achse von c → gilt als waagrecht gezeichnet, Höhe ab c:
    /// 1502.879464 + 0.02 · 1890.037694 = 1540.680218.</summary>
    [Fact]
    public void Zeichnen_NachAbzweigC_MitGefaelle()
    {
        var p = MitAbwasser();
        Ok(AwZeichnen(p, "DN 100", [[0, 0, 2800], [0, 0, 0]]));
        var t = Ok(Abzweigen(p, new JsonObject { ["leitung"] = "RO-1", ["punkt"] = new JsonArray(0, 0, 1500), ["richtung"] = new JsonArray(1, 0, 0), ["gefaelle"] = 2 }));
        Assert.Contains(t.Hinweise, h => h.Contains("TS-1.c ist frei"));
        Assert.Contains(t.Hinweise, h => h.Contains("Achse 2.619 % gegen die Waagrechte geneigt"));   // tan 1.5° = 0.0261859 → 2.619 %
        Etwa(88.5, ExemplarWinkel(p, "TS-1")!.Value, 1e-9);
        Etwa([109.962306, 0, 1502.879464], Stelle(p, "TS-1", "c").Lage, Tol);
        Ok(AwZeichnen(p, "DN 100", [[2000, 0, 1500]], new JsonObject { ["nach"] = "TS-1.c", ["gefaelle"] = 2 }));
        Gerade(p, "RO-2", [2000, 0, 1540.680218], [109.962306, 0, 1502.879464], Tol);
        IstVerbunden(p, "RO-2", "ende", "TS-1", "c");
        Sauber(p);
        Assert.Empty(Befunde(p, "P16"));
    }

    /// <summary>Zuführend (Trinkwasser kalt, Leitung senkrecht abwärts) mit 2 %: Abzweig fällt vom T-Stück weg, gemessen
    /// = Winkel d ↔ u = 88.854° → 88.5°, gespiegelt (x = (0, 0, 1)); c = (109.962306, 0, 1500 − 2.879464) = (109.962306, 0, 1497.120536).
    /// leitung_zeichnen von = TS-1.c, Punkt (2000, 0, 1500) → erstes Stück gilt als waagrecht, Ende 1497.120536 − 37.800754 = 1459.319782.</summary>
    [Fact]
    public void Zeichnen_VonAbzweigC_Zufuehrend_MitGefaelle()
    {
        var p = MitAbwasser();
        MitSystem(p, "KW", "Trinkwasser kalt");
        Ok(AwZeichnen(p, "DN 100", [[0, 0, 2800], [0, 0, 0]], system: "KW"));
        Ok(Abzweigen(p, new JsonObject { ["leitung"] = "RO-1", ["punkt"] = new JsonArray(0, 0, 1500), ["richtung"] = new JsonArray(1, 0, 0), ["gefaelle"] = 2 }));
        Etwa(88.5, ExemplarWinkel(p, "TS-1")!.Value, 1e-9);
        Etwa([0, 0, 1], Familien.Einbaulage(Hole(p, "TS-1"))!.X.Feld(), 1e-9);
        Etwa([109.962306, 0, 1497.120536], Stelle(p, "TS-1", "c").Lage, Tol);
        IstVerbunden(p, "RO-1", "ende", "TS-1", "b");
        Ok(AwZeichnen(p, "DN 100", [[2000, 0, 1500]], new JsonObject { ["von"] = "TS-1.c", ["gefaelle"] = 2 }, system: "KW"));
        Gerade(p, "RO-2", [109.962306, 0, 1497.120536], [2000, 0, 1459.319782], Tol);
        IstVerbunden(p, "TS-1", "c", "RO-2", "anfang");
        Sauber(p);
    }

    /// <summary>Rechtwinkliges T-Stück (Testrohrserie, 90°) am senkrechten Rohr ohne Gefälle gesetzt: c waagrecht. Eine Leitung mit 2 %
    /// weicht um arctan(0.02) = 1.15° von der Achse ab (verbunden bis 1°) → abgelehnt mit Vorschlag zum Abzweigwinkel.</summary>
    [Fact]
    public void Zeichnen_NachWaagrechtemC_MitGefaelle_AbgelehntMitVorschlag()
    {
        var p = MitSerie();
        MitSystem(p, "SW", "Schmutzwasser");
        Ok(Zeichnen(p, "DN 50", [[0, 0, 2800], [0, 0, 0]], system: "SW"));
        Ok(Abzweigen(p, new JsonObject { ["leitung"] = "RO-1", ["punkt"] = new JsonArray(0, 0, 1500), ["richtung"] = new JsonArray(1, 0, 0) }));
        var c = Stelle(p, "TS-1", "c");
        Etwa([55, 0, 1500], c.Lage);   // lc = da = 55 (DN 50)
        var nein = Nein(Zeichnen(p, "DN 50", [[2000, 0, 1500]], new JsonObject { ["nach"] = "TS-1.c", ["gefaelle"] = 2 }, system: "SW"));
        Assert.Contains("unter 1.1°", nein.Fehler);
        Assert.Contains("Mit 2 % Gefälle weicht die Gerade um 1.15°", nein.Vorschlag);
        Assert.Contains("Abzweigwinkel", nein.Vorschlag);
    }

    // ------------------------------------------------------------------ P15 Abzweigwinkel

    [Fact]
    public void P15_AbzweigwinkelNichtInDerSerie_Warnung()
    {
        var p = MitAbwasser();
        Ok(AwZeichnen(p, "DN 100", [[0, 0, 2800], [0, 0, 0]]));
        Ok(Abzweigen(p, new JsonObject { ["leitung"] = "RO-1", ["punkt"] = new JsonArray(0, 0, 1500), ["punkte"] = Punkte([2000, 0, 1500]), ["gefaelle"] = 2 }));
        Assert.Empty(Befunde(p, "P15"));
        Etwa(88.5, Leitungspruefungen.Abzweigwinkel(p.Modell, p.Katalog, Hole(p, "TS-1"))!.Value, 1e-9);
        // Abzweigwinkel auf 60° (FIKTIV, nicht in 45/88.5) – l und lc bleiben 110 (60 ist nicht < 60)
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "TS-1", ["eigenschaften"] = new JsonObject { ["parameter"] = new JsonObject { ["winkel"] = 60 } } }));
        var b = Assert.Single(Befunde(p, "P15"), x => x.Element == "TS-1");
        Assert.Equal("warnung", b.Schwere);
        Assert.Contains("Abzweigwinkel 60°", b.Text);
        Assert.Contains("45°, 88.5°", b.Text);
        Assert.Contains("leitung_abzweigen", b.Vorschlag);
        // zurück auf 88.5 → keine Warnung mehr
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "TS-1", ["eigenschaften"] = new JsonObject { ["parameter"] = new JsonObject { ["winkel"] = 88.5 } } }));
        Assert.DoesNotContain(Befunde(p, "P15"), x => x.Element == "TS-1");
        // T-Stück ohne Familie: kein Abzweigwinkel
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "TStueck", ["kennzeichen"] = "TS-9", ["system"] = "SW", ["geschoss"] = "EG",
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Punkte([5000, 0, 0]) },
        }));
        Assert.Null(Leitungspruefungen.Abzweigwinkel(p.Modell, p.Katalog, Hole(p, "TS-9")));
    }

    // ------------------------------------------------------------------ Abfrage leitungsserie

    [Fact]
    public void Abfrage_Leitungsserie_NenntAbzweigwinkel()
    {
        var p = MitAbwasser();
        var o = Leitungsserie.Abfrage(p, Aw);
        Assert.Equal([45.0, 88.5], o["abzweigwinkel"]!.AsArray().Select(x => x!.GetValue<double>()).ToArray());
        var dn100 = o["groessen"]!.AsArray().First(g => g!["typ"]!.GetValue<string>() == "DN 100")!;
        var je = dn100["formteile"]!["tstueckJeWinkel"]!.AsArray();
        Assert.Equal(2, je.Count);
        Assert.All(je, x => Assert.True(x!["verfuegbar"]!.GetValue<bool>()));
        Assert.Equal(88.5, je[1]!["winkel"]!.GetValue<double>());
        Assert.True(dn100["formteile"]!["tstueck"]!["verfuegbar"]!.GetValue<bool>());   // erster Abzweigwinkel (45°)
        // Testrohrserie: [90], ohne tstueckJeWinkel
        var t = MitSerie();
        var ot = Leitungsserie.Abfrage(t, "fam-test-rohrserie");
        Assert.Equal([90.0], ot["abzweigwinkel"]!.AsArray().Select(x => x!.GetValue<double>()).ToArray());
        Assert.Null(ot["groessen"]![0]!["formteile"]!["tstueckJeWinkel"]);
    }
}
