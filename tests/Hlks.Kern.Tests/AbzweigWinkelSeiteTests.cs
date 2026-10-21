using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;
using static Hlks.Kern.Tests.RoutingHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Nachtrag Modul 3, 21.10.2026 (hlks-kern): leitung_abzweigen mit winkel und seite – der Kern legt den Abzweig unter einem Abzweigwinkel
/// der Serie an (T-Stück 45° bzw. 88.5° nach Familie, Einbaulängen aus deren Formel), zulaufend schräg in Fliessrichtung, zuführend gespiegelt;
/// Abfrage abzweigwinkel für die Oberfläche. Handrechnungen aus den FIKTIVEN Testfamilien (tests/daten/familien): Abwasser-Testserie mit Grösse d
/// (test_abwasser_d_serie, Abzweigwinkel 45 und 88.5), T d 110: unter 60° l = 1.5·d1 = 165, lc = 2·d1 = 220, sonst l = lc = d1 = 110.
/// cos 45° = sin 45° = 0.707106781; cos 88.5° = 0.026176948, sin 88.5° = 0.999657325. Keine Normwerte.</summary>
public class AbzweigWinkelSeiteTests
{
    const string Aw = "fam-test-abwasser-d";
    const double Tol = 1e-3;

    /// <summary>Projekt mit System SW (Schmutzwasser) und der Abwasser-Testserie d (FAM-1) samt Bogen, T, Reduktion.</summary>
    static Projekt MitAbwasser()
    {
        var p = Neu();
        MitSystem(p, "SW", "Schmutzwasser");
        foreach (var f in new[] { "test_abwasser_d_serie", "test_abwasser_d_bogen", "test_abwasser_d_t", "test_abwasser_d_reduktion" })
            Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Datei(f) }));
        return p;
    }

    static BefehlsErgebnis Aw110(Projekt p, double[][] punkte, JsonObject? extra = null, string system = "SW") =>
        Zeichnen(p, "d 110", punkte, extra, serie: Aw, system: system);

    static BefehlsErgebnis Abzweigen(Projekt p, JsonObject par, bool trocken = false) => p.Ausfuehren("leitung_abzweigen", par, trocken);

    static double? ExemplarWinkel(Projekt p, string kz) => Json.Zahl(Familien.Exemplarwerte(Hole(p, kz))?["winkel"]);

    static double Grad(V3 a, V3 b) => Math.Acos(Math.Clamp(a.Einheit().Mal(b.Einheit()), -1, 1)) * 180 / Math.PI;

    /// <summary>Sammelleitung d 110 waagrecht (0, 0, 1000) → (6000, 0, 1000), Schmutzwasser (zulaufend), ohne Gefälle. winkel 45, seite links
    /// (in Fliessrichtung +x gesehen: +y), laenge 1500 bei x = 3000: u = −cos 45°·(1, 0, 0) + sin 45°·(0, 1, 0) = (−0.707107, 0.707107, 0) – der
    /// Zulauf mündet in Fliessrichtung ein. T d 110 45° (l = 165, lc = 220): RO-1 bis (2835, 0, 1000), RO-1-2 ab (3165, 0, 1000),
    /// c = (3000, 0, 1000) + 220·u = (2844.436508, 155.563492, 1000); Zulauf RO-2 von (3000, 0, 1000) + 1500·u = (1939.339828, 1060.660172, 1000)
    /// nach c (Fliessrichtung zum T-Stück).</summary>
    [Fact]
    public void Abzweig45_Links_Waagrecht_ZulaufMuendetInFliessrichtung()
    {
        var p = MitAbwasser();
        Ok(Aw110(p, [[0, 0, 1000], [6000, 0, 1000]]));
        var e = Ok(Abzweigen(p, new JsonObject
        {
            ["leitung"] = "RO-1", ["punkt"] = new JsonArray(3000, 0, 1000), ["winkel"] = 45, ["seite"] = "links", ["laenge"] = 1500,
        }));
        var ts = Hole(p, "TS-1");
        Assert.Equal("d 110", Familien.Typ(ts));
        Etwa(45, ExemplarWinkel(p, "TS-1")!.Value, 1e-9);
        Gerade(p, "RO-1", [0, 0, 1000], [2835, 0, 1000], Tol);
        Gerade(p, "RO-1-2", [3165, 0, 1000], [6000, 0, 1000], Tol);
        var c = Stelle(p, "TS-1", "c");
        Etwa([2844.436508, 155.563492, 1000], c.Lage, Tol);
        Etwa([-0.707107, 0.707107, 0], c.Richtung, 1e-6);
        Gerade(p, "RO-2", [1939.339828, 1060.660172, 1000], [2844.436508, 155.563492, 1000], Tol);
        Assert.Equal("d 110", Familien.Typ(Hole(p, "RO-2")));
        // Netz verbunden: Fliessrichtung zulaufend, weiter durch das T-Stück in die Sammelleitung
        IstVerbunden(p, "RO-1", "ende", "TS-1", "a");
        IstVerbunden(p, "TS-1", "b", "RO-1-2", "anfang");
        IstVerbunden(p, "RO-2", "ende", "TS-1", "c");
        var stromab = Netz.Folgen(p.Modell, p.Katalog, Hole(p, "RO-2"), "stromab").Select(s => s.Element.Kennzeichen).ToList();
        Assert.Contains("TS-1", stromab);
        Assert.Contains("RO-1-2", stromab);
        Assert.DoesNotContain("RO-1", stromab);
        // Abzweigwinkel: Zulauf in seiner Fliessrichtung (zum T hin) gegen die Leitung in Fliessrichtung genau 45°
        var pt = Pkt(p, "RO-2");
        Etwa(45, Grad(new V3(1, 0, 0), V3.Aus(pt[1]) - V3.Aus(pt[0])), 1e-6);
        Sauber(p);
        Assert.Contains(e.Hinweise, h => h.StartsWith("Abzweig unter 45° nach links (zulaufend"));
        Assert.Contains(e.Hinweise, h => h.Contains("T-Stück TS-1 Typ «d 110»") && h.Contains("Abzweigwinkel 45°") && h.Contains("Einbaulängen a 165 mm, b 165 mm, c 220 mm"));
    }

    /// <summary>Fangpunkte im Grundriss: das T-Stück fängt an seinem Anschluss c, der Zulauf endet genau dort (Werkzeuge rasten ein).</summary>
    [Fact]
    public void Abzweig45_FangpunkteAnC()
    {
        var p = MitAbwasser();
        Ok(Aw110(p, [[0, 0, 1000], [6000, 0, 1000]]));
        Ok(Abzweigen(p, new JsonObject { ["leitung"] = "RO-1", ["punkt"] = new JsonArray(3000, 0, 1000), ["winkel"] = 45, ["seite"] = "rechts", ["laenge"] = 1500 }));
        var plan = Grafik.Plan(p, "AN-EG");
        var c = Stelle(p, "TS-1", "c").Lage!;
        Etwa([2844.436508, -155.563492, 1000], c, Tol);   // rechts = −y
        bool Faengt(string kz) => plan.Teile.Where(t => t.Id == Hole(p, kz).Id).SelectMany(t => t.Fangpunkte ?? [])
            .Any(f => Math.Abs(f[0] - c[0]) <= Tol && Math.Abs(f[1] - c[1]) <= Tol);
        Assert.True(Faengt("TS-1"), "T-Stück ohne Fangpunkt an c");
        Assert.True(Faengt("RO-2"), "Zulauf ohne Fangpunkt an c");
        Sauber(p);
    }

    /// <summary>Senkrechter Fallstrang d 110 (0, 0, 2800) → (0, 0, 0), Zulauf unter 88.5° nach +x (seite als Vektor) mit 2 % Gefälle und laenge 2000:
    /// der Kern zeichnet den Zulauf waagrecht und neigt ihn nach dem Gefälle (88.854° → T 88.5°) – dieselben Zahlen wie mit punkte
    /// (RoutingAbzweigwinkelTests, Hauptfall): RO-1 bis z 1610, RO-1-2 ab z 1390, c = (109.962306, 0, 1502.879464), Zulauf ab (2000, 0, 1540.680218).</summary>
    [Fact]
    public void Abzweig88_5_Fallstrang_MitGefaelle()
    {
        var p = MitAbwasser();
        Ok(Aw110(p, [[0, 0, 2800], [0, 0, 0]]));
        Ok(Abzweigen(p, new JsonObject
        {
            ["leitung"] = "RO-1", ["punkt"] = new JsonArray(0, 0, 1500), ["winkel"] = 88.5, ["seite"] = new JsonArray(1, 0, 0), ["laenge"] = 2000, ["gefaelle"] = 2,
        }));
        Etwa(88.5, ExemplarWinkel(p, "TS-1")!.Value, 1e-9);
        Gerade(p, "RO-1", [0, 0, 2800], [0, 0, 1610], Tol);
        Gerade(p, "RO-1-2", [0, 0, 1390], [0, 0, 0], Tol);
        Etwa([109.962306, 0, 1502.879464], Stelle(p, "TS-1", "c").Lage, Tol);
        Gerade(p, "RO-2", [2000, 0, 1540.680218], [109.962306, 0, 1502.879464], Tol);
        IstVerbunden(p, "RO-2", "ende", "TS-1", "c");
        Sauber(p);
        Assert.Empty(Befunde(p, "P16"));
        Assert.Empty(Befunde(p, "P30"));
    }

    /// <summary>Fallstrang ohne Gefälle, 45° nach −y: u = −cos 45°·(0, 0, −1) + sin 45°·(0, −1, 0) = (0, −0.707107, 0.707107) – der Zulauf steigt vom
    /// T-Stück weg (fällt zum Fallstrang hin) genau in der Achse von c. T 45°: l = 165, lc = 220: c = (0, −155.563492, 1655.563492); laenge 1000:
    /// Zulauf ab (0, −707.106781, 2207.106781).</summary>
    [Fact]
    public void Abzweig45_Fallstrang_OhneGefaelle_InDerAchseVonC()
    {
        var p = MitAbwasser();
        Ok(Aw110(p, [[0, 0, 2800], [0, 0, 0]]));
        Ok(Abzweigen(p, new JsonObject { ["leitung"] = "RO-1", ["punkt"] = new JsonArray(0, 0, 1500), ["winkel"] = 45, ["seite"] = new JsonArray(0, -1, 0), ["laenge"] = 1000 }));
        Etwa(45, ExemplarWinkel(p, "TS-1")!.Value, 1e-9);
        var c = Stelle(p, "TS-1", "c");
        Etwa([0, -155.563492, 1655.563492], c.Lage, Tol);
        Etwa([0, -0.707107, 0.707107], c.Richtung, 1e-6);
        Gerade(p, "RO-2", [0, -707.106781, 2207.106781], [0, -155.563492, 1655.563492], Tol);
        Gerade(p, "RO-1", [0, 0, 2800], [0, 0, 1665], Tol);
        Gerade(p, "RO-1-2", [0, 0, 1335], [0, 0, 0], Tol);
        Sauber(p);
        // «links» ist am senkrechten Fallstrang nicht bestimmt
        var nein = Nein(Abzweigen(p, new JsonObject { ["leitung"] = "RO-1-2", ["punkt"] = new JsonArray(0, 0, 700), ["winkel"] = 45, ["seite"] = "links" }));
        Assert.Contains("senkrecht", nein.Fehler);
        Assert.Contains("Vektor", nein.Vorschlag);
    }

    /// <summary>Zuführend (Trinkwasser kalt, FIKTIV mit der Abwasser-Testserie – P30 meldet das, hier egal): 45° links geht in Fliessrichtung ab,
    /// das T-Stück wird gespiegelt (Durchgang b → a): b bei (2835, 0, 1000), a bei (3165, 0, 1000), c = (3155.563492, 155.563492, 1000), Abzweig
    /// RO-2 von c bis (3000, 0, 1000) + 1500·(0.707107, 0.707107, 0) = (4060.660172, 1060.660172, 1000).</summary>
    [Fact]
    public void Abzweig45_Zufuehrend_Gespiegelt()
    {
        var p = MitAbwasser();
        MitSystem(p, "KW", "Trinkwasser kalt");
        Ok(Aw110(p, [[0, 0, 1000], [6000, 0, 1000]], system: "KW"));
        var e = Ok(Abzweigen(p, new JsonObject { ["leitung"] = "RO-1", ["punkt"] = new JsonArray(3000, 0, 1000), ["winkel"] = 45, ["seite"] = "links", ["laenge"] = 1500 }));
        Etwa([2835, 0, 1000], Stelle(p, "TS-1", "b").Lage, Tol);
        Etwa([3165, 0, 1000], Stelle(p, "TS-1", "a").Lage, Tol);
        Etwa([3155.563492, 155.563492, 1000], Stelle(p, "TS-1", "c").Lage, Tol);
        Gerade(p, "RO-2", [3155.563492, 155.563492, 1000], [4060.660172, 1060.660172, 1000], Tol);
        IstVerbunden(p, "TS-1", "c", "RO-2", "anfang");
        Sauber(p);
        Assert.Contains(e.Hinweise, h => h.Contains("zuführend: geht in Fliessrichtung ab"));
        Assert.Contains(e.Hinweise, h => h.Contains("gespiegelt"));
    }

    /// <summary>Ablehnungen: ein Winkel, den die Serie nicht hat (60°), nennt die verfügbaren (45°, 88.5°) samt Vorschlag; ohne winkel gilt die
    /// Vorgabe 90° – in der Abwasser-Testserie nicht vorhanden; seite in der Achse; laenge fehlt zu punkte. Das Modell bleibt unverändert.</summary>
    [Fact]
    public void Abzweig_WinkelNichtInDerSerie_AbgelehntMitDenVerfuegbaren()
    {
        var p = MitAbwasser();
        Ok(Aw110(p, [[0, 0, 1000], [6000, 0, 1000]]));
        var vorher = Inhalt(p);
        JsonObject Par(JsonObject extra)
        {
            var o = new JsonObject { ["leitung"] = "RO-1", ["punkt"] = new JsonArray(3000, 0, 1000), ["laenge"] = 1000 };
            foreach (var (k, v) in extra) o[k] = v?.DeepClone();
            return o;
        }
        var n60 = Nein(Abzweigen(p, Par(new JsonObject { ["winkel"] = 60, ["seite"] = "links" })));
        Assert.Contains("Abzweigwinkel von 60°", n60.Fehler);
        Assert.Contains("verfügbar: 45°, 88.5°", n60.Fehler);
        Assert.Contains("winkel = 45 bzw. 88.5", n60.Vorschlag);
        Assert.Contains("abzweigwinkel", n60.Vorschlag);
        var n90 = Nein(Abzweigen(p, Par(new JsonObject { ["seite"] = "rechts" })));
        Assert.Contains("Abzweigwinkel von 90° (Vorgabe ohne winkel)", n90.Fehler);
        Assert.Contains("verfügbar: 45°, 88.5°", n90.Fehler);
        var nAchse = Nein(Abzweigen(p, Par(new JsonObject { ["winkel"] = 45, ["seite"] = new JsonArray(1, 0, 0) })));
        Assert.Contains("in der Achse", nAchse.Fehler);
        var nSeite = Nein(Abzweigen(p, Par(new JsonObject { ["winkel"] = 45, ["seite"] = "vorne" })));
        Assert.Contains("«vorne» gibt es nicht", nSeite.Fehler);
        var nLaenge = Nein(p.Ausfuehren("leitung_abzweigen", new JsonObject
        {
            ["leitung"] = "RO-1", ["punkt"] = new JsonArray(3000, 0, 1000), ["winkel"] = 45, ["seite"] = "links", ["punkte"] = Punkte([1000, 2000, 1000]),
        }));
        Assert.Contains("laenge", nLaenge.Fehler);
        Assert.Equal(vorher, Inhalt(p));
    }

    /// <summary>Vorgabe ohne winkel: der einzige Winkel der Serie (FIKTIVE Variante der Abwasser-Testserie mit nur 45°) bzw. 90° (Testrohrserie,
    /// Trinkwasser: rechts = −y, T DN 32 rechtwinklig mit lc = 37, Abzweig RO-2 von (1500, −37, 1000) bis (1500, −1000, 1000)). Nur das T-Stück ohne
    /// laenge; Trockenlauf ändert nichts.</summary>
    [Fact]
    public void Abzweig_Vorgabe_EinzigerWinkelBzw90_TrockenlaufUndNurTStueck()
    {
        var t = MitSerie();
        Ok(Zeichnen(t, "DN 32", [[0, 0, 1000], [4000, 0, 1000]]));
        var vorher = Inhalt(t);
        var tl = Ok(Abzweigen(t, new JsonObject { ["leitung"] = "RO-1", ["punkt"] = new JsonArray(1500, 0, 1000), ["seite"] = "rechts", ["laenge"] = 1000 }, trocken: true));
        Assert.Equal(vorher, Inhalt(t));
        Assert.Contains(tl.Hinweise, h => h.StartsWith("Abzweig unter 90° nach rechts"));
        Ok(Abzweigen(t, new JsonObject { ["leitung"] = "RO-1", ["punkt"] = new JsonArray(1500, 0, 1000), ["seite"] = "rechts", ["laenge"] = 1000 }));
        Etwa([1500, -37, 1000], Stelle(t, "TS-1", "c").Lage, Tol);
        Gerade(t, "RO-2", [1500, -37, 1000], [1500, -1000, 1000], Tol);
        Sauber(t);

        var p = Neu();
        MitSystem(p, "SW", "Schmutzwasser");
        foreach (var f in new[] { "test_abwasser_d_bogen", "test_abwasser_d_t", "test_abwasser_d_reduktion" })
            Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Datei(f) }));
        var d = Familie("test_abwasser_d_serie");
        d["leitung"]!["regeln"]!["abzweigwinkel"] = new JsonArray(45);
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d }));
        Assert.Equal(45, Leitungsserie.Hole(p.Modell, p.Katalog, Aw).AbzweigwinkelVorgabe);
        Ok(Aw110(p, [[0, 0, 1000], [6000, 0, 1000]]));
        var e = Ok(Abzweigen(p, new JsonObject { ["leitung"] = "RO-1", ["punkt"] = new JsonArray(3000, 0, 1000), ["seite"] = "oben" }));   // nur das T-Stück
        Etwa(45, ExemplarWinkel(p, "TS-1")!.Value, 1e-9);
        Etwa([2844.436508, 0, 1155.563492], Stelle(p, "TS-1", "c").Lage, Tol);   // oben: c = q + 220·(−0.707107, 0, 0.707107)
        Assert.Contains(e.Hinweise, h => h.Contains("TS-1.c ist frei"));
    }

    // ------------------------------------------------------------------ Abfrage abzweigwinkel

    [Fact]
    public void Abfrage_Abzweigwinkel_LeitungUndSerie()
    {
        var p = MitAbwasser();
        Ok(Aw110(p, [[0, 0, 2800], [0, 0, 0]]));
        Ok(Aw110(p, [[0, 2000, 1000], [3000, 2000, 1000]]));
        var o = Leitungsserie.AbzweigwinkelAbfrage(p, "RO-1", null, null);
        Assert.Equal("FAM-1", o["familie"]!.GetValue<string>());
        Assert.Equal([45.0, 88.5], o["abzweigwinkel"]!.AsArray().Select(x => x!.GetValue<double>()).ToArray());
        Assert.Equal(90, o["vorgabe"]!.GetValue<double>());
        Assert.False(o["vorgabeErlaubt"]!.GetValue<bool>());
        Assert.Equal("zulaufend", o["fliessrichtung"]!.GetValue<string>());
        Assert.True(o["senkrecht"]!.GetValue<bool>());
        Assert.Empty(o["seiten"]!.AsArray());
        Assert.Equal([0.0, 0, -1], o["richtungLeitung"]!.AsArray().Select(x => x!.GetValue<double>()).ToArray());
        Assert.Equal("d 110", o["groesseText"]!.GetValue<string>());
        var je = o["jeWinkel"]!.AsArray();
        Assert.Equal(2, je.Count);
        Assert.All(je, x => Assert.True(x!["verfuegbar"]!.GetValue<bool>()));
        Assert.Equal("d 110", je[0]!["typ"]!.GetValue<string>());
        Assert.Equal([165.0, 165, 220], new[] { "a", "b", "c" }.Select(n => je[0]!["einbaulaengen"]![n]!.GetValue<double>()));
        Assert.Equal([110.0, 110, 110], new[] { "a", "b", "c" }.Select(n => je[1]!["einbaulaengen"]![n]!.GetValue<double>()));
        // waagrechte Leitung: alle Seiten
        var w = Leitungsserie.AbzweigwinkelAbfrage(p, "RO-2", null, null);
        Assert.False(w["senkrecht"]!.GetValue<bool>());
        Assert.Equal(["links", "rechts", "oben", "unten"], w["seiten"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray());
        // über die Tür, mit Serie und Grösse (ohne Leitung): T d 32 gibt es, aber nur für 88.5 und 45 je d 32
        var s = (JsonObject)Tuer.Abfrage(p, "abzweigwinkel", new JsonObject { ["familie"] = Aw, ["groesse"] = "d 32" });
        Assert.Null(s["leitung"]);
        Assert.Equal("d 32", s["groesseText"]!.GetValue<string>());
        Assert.Equal(2, s["jeWinkel"]!.AsArray().Count);
        // Leitung ohne Serie bzw. nichts angegeben: Fehler mit Vorschlag
        Assert.Contains("leitung bzw. familie", Assert.Throws<HlksFehler>(() => Leitungsserie.AbzweigwinkelAbfrage(p, null, null, null)).Message);
        Assert.Contains(Abfragen.Liste, a => a.Name == "abzweigwinkel" && a.Parameter.Select(x => x.Name).SequenceEqual(new[] { "leitung", "familie", "groesse" }));
    }
}
