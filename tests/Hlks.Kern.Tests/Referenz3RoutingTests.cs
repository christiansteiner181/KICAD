using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;
using static Hlks.Kern.Tests.RoutingHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3, B6, Referenzfälle 2 und 6: Routing-Längen (L-, Z-, U-Zug, Grössenwechsel, Abzweig, Steigleitung) für Rohr und
/// rechteckigen Kanal – von Hand aus den FIKTIVEN Testfamilien gerechnet.
/// Rohr: da = DN + 5, Bogen r = 2·da, 90° → La = Lb = r (tan 45° = 1): DN 20 → 50 mm, DN 25 → 60 mm, DN 32 → 74 mm, DN 40 → 90 mm;
/// T-Stück l = lc = da (DN 32: 37 mm); Reduktion l = 40 mm.
/// Kanal: Bogen r = b, 90° → La = b (400x200: 400 mm, 300x200: 300 mm, 500x300: 500 mm); Kanal-T l = 300, lc = 250; Reduktion l = 200 mm.
/// Länge einer Gerade = Abstand der Knickpunkte − La am Anfang (ausser beim ersten Stück) − Lb am Ende (ausser beim letzten).</summary>
public class Referenz3RoutingTests
{
    const double Tol = 1e-3;

    static JsonNode Zug(Projekt p, string leitung) => (JsonNode)Tuer.Abfrage(p, "leitungszug", new JsonObject { ["leitung"] = leitung });
    static double Zahl(JsonNode? n) => Json.Zahl(n) ?? throw new Xunit.Sdk.XunitException("keine Zahl: " + n?.ToJsonString());

    // ------------------------------------------------------------------ Rohr: L-, Z- und U-Zug

    static double[][] P(params double[][] p) => p;

    public static IEnumerable<object[]> Zuege()
    {
        // Name, Typ, Punkte, erwartete Längen der Geraden (Rechnung daneben), Summe
        // L DN 20 (La 50): 3000 − 50 = 2950; 2000 − 50 = 1950 → 4900
        yield return ["L DN 20", "DN 20", P([0, 0, 1000], [3000, 0, 1000], [3000, 2000, 1000]), new[] { 2950.0, 1950 }, 4900.0];
        // L DN 40 (La 90): 2500 − 90 = 2410; 1800 − 90 = 1710 → 4120
        yield return ["L DN 40", "DN 40", P([0, 0, 1000], [2500, 0, 1000], [2500, 1800, 1000]), new[] { 2410.0, 1710 }, 4120.0];
        // Z DN 20 (La 50): 2000 − 50 = 1950; 1500 − 100 = 1400; 2000 − 50 = 1950 → 5300
        yield return ["Z DN 20", "DN 20", P([0, 0, 1000], [2000, 0, 1000], [2000, 1500, 1000], [4000, 1500, 1000]), new[] { 1950.0, 1400, 1950 }, 5300.0];
        // Z DN 40 (La 90): 2000 − 90 = 1910; 1500 − 180 = 1320; 1910 → 5140
        yield return ["Z DN 40", "DN 40", P([0, 0, 1000], [2000, 0, 1000], [2000, 1500, 1000], [4000, 1500, 1000]), new[] { 1910.0, 1320, 1910 }, 5140.0];
        // U DN 20 (La 50): 2000 − 50 = 1950; 800 − 100 = 700; 1950 → 4600
        yield return ["U DN 20", "DN 20", P([0, 0, 1000], [2000, 0, 1000], [2000, 800, 1000], [0, 800, 1000]), new[] { 1950.0, 700, 1950 }, 4600.0];
        // U DN 40 (La 90): 2000 − 90 = 1910; 800 − 180 = 620; 1910 → 4440
        yield return ["U DN 40", "DN 40", P([0, 0, 1000], [2000, 0, 1000], [2000, 800, 1000], [0, 800, 1000]), new[] { 1910.0, 620, 1910 }, 4440.0];
    }

    [Theory]
    [MemberData(nameof(Zuege))]
    public void Zug_LaengenDerGeradenUndSumme(string name, string typ, double[][] punkte, double[] erwartet, double summe)
    {
        var p = MitSerie();
        var e = Ok(Zeichnen(p, typ, punkte));
        Assert.Equal(erwartet.Length, Angelegt(e, "Rohr").Count);
        Assert.Equal(erwartet.Length - 1, Angelegt(e, "Bogen").Count);
        for (int i = 0; i < erwartet.Length; i++)
            Etwa(erwartet[i], Laenge(p, $"RO-{i + 1}"), Tol, $"{name} Gerade {i + 1}");
        // Gerade i läuft zwischen den Anschlüssen der Bögen (Ende der einen = Anschluss des anderen)
        for (int i = 0; i < erwartet.Length - 1; i++)
        {
            Etwa(Stelle(p, $"BO-{i + 1}", "a").Lage!, Pkt(p, $"RO-{i + 1}")[1], 1e-6, $"{name} Ende Gerade {i + 1}");
            Etwa(Stelle(p, $"BO-{i + 1}", "b").Lage!, Pkt(p, $"RO-{i + 2}")[0], 1e-6, $"{name} Anfang Gerade {i + 2}");
        }
        Assert.Contains(e.Hinweise, h => h.Contains($"zusammen {summe} mm"));
        // Abfrage leitungszug: gleiche Summe, ein Eintrag je Grösse
        var z = Zug(p, "RO-1");
        Etwa(summe, Zahl(z["summe"]!["laengeMm"]), 0.06, name + " Summe");
        Etwa(summe / 1000, Zahl(z["summe"]!["laengeM"]), 0.0006, name + " Summe m");
        Assert.Equal(erwartet.Length, (int)Zahl(z["summe"]!["leitungen"]));
        Assert.Equal(erwartet.Length - 1, (int)Zahl(z["summe"]!["formteile"]));
        var je = Assert.Single(z["jeGroesse"]!.AsArray());
        Assert.Equal(typ, je!["groesse"]!.GetValue<string>());
        Assert.Equal(erwartet.Length, (int)Zahl(je["anzahl"]));
        Etwa(summe, Zahl(je["laengeMm"]), 0.06);
        Assert.True(z["anfangOffen"]!.GetValue<bool>() && z["endeOffen"]!.GetValue<bool>());
        Sauber(p);
    }

    // ------------------------------------------------------------------ Grössenwechsel im Zug

    /// <summary>DN 25 (La 60), nach dem Bogen wechselt die Grösse auf DN 20: Reduktion DN 25/20 (l = 40) direkt hinter dem Bogen.
    /// RO-1: 2000 − 60 = 1940; Reduktion von y = 60 bis 60 + 40 = 100; RO-2: 1500 − 100 = 1400; Summe 3340.</summary>
    [Fact]
    public void Groessenwechsel_NachDemBogen_ReduktionAnDerRichtigenStelle()
    {
        var p = MitSerie();
        Ok(Zeichnen(p, "DN 25", [[0, 0, 1000], [2000, 0, 1000], [2000, 1500, 1000]]));
        Ok(p.Ausfuehren("leitung_groesse", new JsonObject { ["leitungen"] = new JsonArray("RO-2"), ["groesse"] = "DN 20" }));
        Assert.Equal("DN 25/20", Familien.Typ(Hole(p, "RD-1")));
        Assert.Equal("DN 25", Familien.Typ(Hole(p, "BO-1")));
        Gerade(p, "RO-1", [0, 0, 1000], [1940, 0, 1000], Tol);
        Etwa([2000, 60, 1000], Stelle(p, "RD-1", "a").Lage, Tol);
        Etwa([2000, 100, 1000], Stelle(p, "RD-1", "b").Lage, Tol);
        Gerade(p, "RO-2", [2000, 100, 1000], [2000, 1500, 1000], Tol);
        Etwa(1400, Laenge(p, "RO-2"), Tol);
        var z = Zug(p, "RO-1");
        Assert.Equal(["RO-1", "BO-1", "RD-1", "RO-2"], z["glieder"]!.AsArray().Select(x => x!["kennzeichen"]!.GetValue<string>()));
        Etwa(3340, Zahl(z["summe"]!["laengeMm"]), 0.06);
        var je = z["jeGroesse"]!.AsArray();
        Assert.Equal(2, je.Count);
        Etwa(1400, Zahl(je.Single(x => x!["groesse"]!.GetValue<string>() == "DN 20")!["laengeMm"]), 0.06);
        Etwa(1940, Zahl(je.Single(x => x!["groesse"]!.GetValue<string>() == "DN 25")!["laengeMm"]), 0.06);
        Sauber(p);
    }

    /// <summary>DN 40 (La 90) mit zwei Bögen, die letzte Gerade wird DN 32: Reduktion DN 40/32 (l = 40) hinter dem zweiten Bogen.
    /// RO-1: 1500 − 90 = 1410; RO-2: 1000 − 180 = 820; zweiter Bogen b bei x = 1500 + 90 = 1590; Reduktion 1590 bis 1630;
    /// RO-3: 3000 − 1630 = 1370; Summe 1410 + 820 + 1370 = 3600.</summary>
    [Fact]
    public void Groessenwechsel_ZweiBoegen_LetzteGeradeKleiner()
    {
        var p = MitSerie();
        Ok(Zeichnen(p, "DN 40", [[0, 0, 0], [1500, 0, 0], [1500, 1000, 0], [3000, 1000, 0]]));
        Ok(p.Ausfuehren("leitung_groesse", new JsonObject { ["leitungen"] = new JsonArray("RO-3"), ["groesse"] = "DN 32" }));
        Assert.Equal("DN 40/32", Familien.Typ(Hole(p, "RD-1")));
        Gerade(p, "RO-1", [0, 0, 0], [1410, 0, 0], Tol);
        Gerade(p, "RO-2", [1500, 90, 0], [1500, 910, 0], Tol);
        Etwa(820, Laenge(p, "RO-2"), Tol);
        Etwa([1590, 1000, 0], Stelle(p, "RD-1", "a").Lage, Tol);
        Etwa([1630, 1000, 0], Stelle(p, "RD-1", "b").Lage, Tol);
        Gerade(p, "RO-3", [1630, 1000, 0], [3000, 1000, 0], Tol);
        Etwa(1370, Laenge(p, "RO-3"), Tol);
        Etwa(3600, Zahl(Zug(p, "RO-1")["summe"]!["laengeMm"]), 0.06);
        Sauber(p);
    }

    // ------------------------------------------------------------------ Abzweig

    /// <summary>Hauptleitung DN 32 (0..5000, z 1000), Abzweig DN 25 bei x = 2200 nach +y bis y = 1500. T-Stück DN 32 (l = lc = 37),
    /// Reduktion DN 32/25 (l = 40) an c. RO-1: 2200 − 37 = 2163; RO-1-2: 5000 − (2200 + 37) = 2763; c bei y = 37; Reduktion 37 bis 77;
    /// Abzweig-Gerade von y = 77 bis 1500 = 1423 mm. Probe: 2163 + 37 + 37 + 2763 = 5000.
    /// (Auftrag: «Abzweig DN 32 auf DN 25 (T + Reduktion an c: lc + l = 37 + 40 mm)» – gemeint ist das T-Stück DN 32 der DN-32-Hauptleitung.)</summary>
    [Fact]
    public void Abzweig_DN25_AufHauptleitungDN32_TStueckPlusReduktionAnC()
    {
        var p = MitSerie();
        Ok(Zeichnen(p, "DN 32", [[0, 0, 1000], [5000, 0, 1000]]));
        var e = Ok(p.Ausfuehren("leitung_abzweigen", new JsonObject
        {
            ["leitung"] = "RO-1", ["punkt"] = new JsonArray(2200, 0, 1000), ["richtung"] = new JsonArray(0, 1, 0), ["laenge"] = 1500, ["groesse"] = "DN 25",
        }));
        Gerade(p, "RO-1", [0, 0, 1000], [2163, 0, 1000], Tol);
        Gerade(p, "RO-1-2", [2237, 0, 1000], [5000, 0, 1000], Tol);
        Etwa(2763, Laenge(p, "RO-1-2"), Tol);
        Assert.Equal("DN 32", Familien.Typ(Hole(p, "TS-1")));
        Etwa([2200, 37, 1000], Stelle(p, "TS-1", "c").Lage, Tol);
        Assert.Equal("DN 32/25", Familien.Typ(Hole(p, "RD-1")));
        Etwa([2200, 37, 1000], Stelle(p, "RD-1", "a").Lage, Tol);
        Etwa([2200, 77, 1000], Stelle(p, "RD-1", "b").Lage, Tol);     // 37 + 40
        Gerade(p, "RO-2", [2200, 77, 1000], [2200, 1500, 1000], Tol);
        Etwa(1423, Laenge(p, "RO-2"), Tol);
        Assert.Equal("DN 25", Familien.Typ(Hole(p, "RO-2")));
        Etwa(5000, Laenge(p, "RO-1") + 74 + Laenge(p, "RO-1-2"), Tol);   // Probe: Teilstücke + 2·37
        Assert.Contains(e.Hinweise, h => h.Contains("RO-1 geteilt in RO-1 (2163 mm) und RO-1-2 (2763 mm)"));
        // Zug des Abzweigs: ab TS-1.c Reduktion und Gerade
        var z = Zug(p, "RO-2");
        Assert.Equal("TS-1", z["anfang"]!["element"]!.GetValue<string>());
        Assert.Equal("c", z["anfang"]!["anschluss"]!.GetValue<string>());
        Etwa(1423, Zahl(z["summe"]!["laengeMm"]), 0.06);
        Sauber(p);
    }

    // ------------------------------------------------------------------ Steigleitung über zwei Geschosse

    /// <summary>Geschosse EG (Kote 0), OG (3000), DG (6000). Steigleitung DN 20 (La 50) von z = 500 auf z = 6500 (zwei Geschosshöhen = 6000 mm):
    /// RO-1: 1000 − 50 = 950; RO-2 senkrecht von z = 550 bis 6450 = 5900; RO-3: 1000 − 50 = 950; Summe 7800 = 7.8 m.</summary>
    [Fact]
    public void Steigleitung_UeberZweiGeschosse()
    {
        var p = MitSerie();
        foreach (var (kz, kote) in new[] { ("OG", 3000), ("DG", 6000) })
            Ok(p.Ausfuehren("element_anlegen", new JsonObject
            {
                ["klasse"] = "Geschoss", ["kennzeichen"] = kz, ["teilVon"] = "GEB", ["eigenschaften"] = new JsonObject { ["kote"] = kote, ["hoehe"] = 3000 },
            }));
        Ok(Zeichnen(p, "DN 20", [[0, 0, 500], [1000, 0, 500], [1000, 0, 6500], [2000, 0, 6500]]));
        Gerade(p, "RO-1", [0, 0, 500], [950, 0, 500], Tol);
        Gerade(p, "RO-2", [1000, 0, 550], [1000, 0, 6450], Tol);
        Etwa(5900, Laenge(p, "RO-2"), Tol);
        Gerade(p, "RO-3", [1050, 0, 6500], [2000, 0, 6500], Tol);
        Etwa([1000, 0, 500], Familien.Einfuegepunkt(Hole(p, "BO-1")), 1e-6);
        Etwa([1000, 0, 6500], Familien.Einfuegepunkt(Hole(p, "BO-2")), 1e-6);
        Etwa([0, 0, 1], Stelle(p, "BO-1", "b").Richtung, 1e-6);          // Steigbogen: b zeigt nach oben
        Etwa([0, 0, -1], Stelle(p, "BO-2", "a").Richtung, 1e-6);         // oben: a zeigt zurück nach unten
        Etwa(7800, Zahl(Zug(p, "RO-1")["summe"]!["laengeMm"]), 0.06);
        Etwa(7.8, Zahl(Zug(p, "RO-3")["summe"]!["laengeM"]), 1e-3);
        Assert.Equal(Hole(p, "EG").Id, Hole(p, "RO-1").Geschoss);       // z = 500 liegt im EG (0 … 3000)
        Assert.Equal(Hole(p, "DG").Id, Hole(p, "RO-3").Geschoss);       // z = 6500 liegt im DG (6000 …)
        Sauber(p);
    }

    // ------------------------------------------------------------------ Kanal rechteckig (Referenzfall 6)

    static BefehlsErgebnis Kanal(Projekt p, string groesse, double[][] punkte) =>
        Zeichnen(p, groesse, punkte, null, serie: "fam-test-kanalserie", system: "ZUL");

    /// <summary>Kanal 400x200: Bogen r = b = 400, 90° → La = 400. (4000 − 400) = 3600; (3000 − 400) = 2600; Summe 6200.</summary>
    [Fact]
    public void KanalLZug_400x200()
    {
        var p = MitKanal();
        var e = Ok(Kanal(p, "400x200", [[0, 0, 2500], [4000, 0, 2500], [4000, 3000, 2500]]));
        Assert.Equal(["KA-1", "KA-2"], Angelegt(e, "Kanal"));
        Gerade(p, "KA-1", [0, 0, 2500], [3600, 0, 2500], Tol);
        Gerade(p, "KA-2", [4000, 400, 2500], [4000, 3000, 2500], Tol);
        Etwa(3600, Laenge(p, "KA-1"), Tol); Etwa(2600, Laenge(p, "KA-2"), Tol);
        Etwa(6200, Zahl(Zug(p, "KA-1")["summe"]!["laengeMm"]), 0.06);
        Assert.Equal(400, Hole(p, "KA-1").Zahl("breite")); Assert.Equal(200, Hole(p, "KA-1").Zahl("hoehe"));
        Sauber(p);
    }

    /// <summary>Kanal 300x200: La = 300. (3500 − 300) = 3200; (2500 − 300) = 2200; Summe 5400.</summary>
    [Fact]
    public void KanalLZug_300x200()
    {
        var p = MitKanal();
        Ok(Kanal(p, "300x200", [[0, 0, 3000], [3500, 0, 3000], [3500, 2500, 3000]]));
        Etwa(3200, Laenge(p, "KA-1"), Tol); Etwa(2200, Laenge(p, "KA-2"), Tol);
        Etwa(5400, Zahl(Zug(p, "KA-1")["summe"]!["laengeMm"]), 0.06);
        Sauber(p);
    }

    /// <summary>Kanal 400x200, 45°: La = 400·tan 22.5° = 400·0.414213562 = 165.6854248 mm (Familienbeschreibung: 165.685).
    /// KA-1: 2000 − 165.6854248 = 1834.3145752; KA-2: 1000·√2 − 165.6854248 = 1414.213562 − 165.685425 = 1248.528137; Summe 3082.842712.</summary>
    [Fact]
    public void Kanal45Grad_400x200()
    {
        var p = MitKanal();
        Ok(Kanal(p, "400x200", [[0, 0, 2500], [2000, 0, 2500], [3000, 1000, 2500]]));
        Etwa(1834.3145752, Laenge(p, "KA-1"), 2e-3);
        Etwa(1248.528137, Laenge(p, "KA-2"), 2e-3);
        Etwa(3082.842712, Zahl(Zug(p, "KA-1")["summe"]!["laengeMm"]), 0.06);
        Sauber(p);
    }

    /// <summary>Kanal 400x200 mit Abzweig 300x200: T «400x200/300x200» (l = 300, lc = 250). Hauptleitung 0..6000, Abzweig bei x = 3000 nach +y
    /// bis y = 1800. KA-1: 3000 − 300 = 2700; KA-1-2: 6000 − 3300 = 2700; Abzweig von y = 250 bis 1800 = 1550; keine Reduktion.</summary>
    [Fact]
    public void KanalAbzweig_300x200_AufHauptleitung400x200()
    {
        var p = MitKanal();
        Ok(Kanal(p, "400x200", [[0, 0, 2500], [6000, 0, 2500]]));
        Ok(p.Ausfuehren("leitung_abzweigen", new JsonObject
        {
            ["leitung"] = "KA-1", ["punkt"] = new JsonArray(3000, 0, 2500), ["richtung"] = new JsonArray(0, 1, 0), ["laenge"] = 1800, ["groesse"] = "300x200",
        }));
        Assert.Equal("400x200/300x200", Familien.Typ(Hole(p, "TS-1")));
        Gerade(p, "KA-1", [0, 0, 2500], [2700, 0, 2500], Tol);
        Gerade(p, "KA-1-2", [3300, 0, 2500], [6000, 0, 2500], Tol);
        Gerade(p, "KA-2", [3000, 250, 2500], [3000, 1800, 2500], Tol);
        Etwa(1550, Laenge(p, "KA-2"), Tol);
        Assert.Null(p.Modell.Finde("RD-1"));
        Sauber(p);
    }

    /// <summary>Kanal 500x300 (La 500), letzte Gerade wird 400x200: Reduktion «500x300/400x200» (l = 200) hinter dem Bogen.
    /// KA-1: 4000 − 500 = 3500; Reduktion von y = 500 bis 700; KA-2: 3000 − 700 = 2300; Summe 5800.</summary>
    [Fact]
    public void KanalZug_MitReduktion_500x300_Auf400x200()
    {
        var p = MitKanal();
        Ok(Kanal(p, "500x300", [[0, 0, 2500], [4000, 0, 2500], [4000, 3000, 2500]]));
        Ok(p.Ausfuehren("leitung_groesse", new JsonObject { ["leitungen"] = new JsonArray("KA-2"), ["groesse"] = new JsonObject { ["b"] = 400, ["h"] = 200 } }));
        Assert.Equal("500x300/400x200", Familien.Typ(Hole(p, "RD-1")));
        Gerade(p, "KA-1", [0, 0, 2500], [3500, 0, 2500], Tol);
        Etwa([4000, 500, 2500], Stelle(p, "RD-1", "a").Lage, Tol);
        Etwa([4000, 700, 2500], Stelle(p, "RD-1", "b").Lage, Tol);
        Gerade(p, "KA-2", [4000, 700, 2500], [4000, 3000, 2500], Tol);
        Etwa(2300, Laenge(p, "KA-2"), Tol);
        Etwa(5800, Zahl(Zug(p, "KA-1")["summe"]!["laengeMm"]), 0.06);
        Sauber(p);
    }
}
