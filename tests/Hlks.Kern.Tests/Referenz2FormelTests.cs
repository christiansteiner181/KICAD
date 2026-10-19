using System.Text.Json.Nodes;

namespace Hlks.Kern.Tests;

/// <summary>Modul 2, Auftrag B6: unabhängige Referenzfälle von Hand – Formeln und Reihenfolge der Werte. Jeder Erwartungswert ist
/// von Hand gerechnet (Rechenweg im Kommentar über der Prüfung), nie durch einen Aufruf der Produktfunktion bestimmt. Die
/// Regeln stammen aus entwurf/modul-2.md («Formeln», «Entscheide während des Baus»): ^ rechtsassoziativ und stärker als das
/// Vorzeichen, round .5 von null weg, if rechnet nur den gewählten Zweig, Winkel in Grad, ja_nein als 1/0, Text nur mit = und
/// <>. Alle Zahlen frei gewählt (fiktiv), keine Normwerte.</summary>
public class Referenz2FormelTests
{
    sealed class Werte(Dictionary<string, FWert> w) : IFormelUmgebung
    {
        public FWert Wert(string name, int stelle) =>
            w.TryGetValue(name, out var x) ? x : throw new FormelFehler($"unbekannter Name «{name}»", stelle, "Test");
    }

    static readonly Werte Umgebung = new(new()
    {
        ["breite"] = FWert.Z(40), ["hoehe"] = FWert.Z(600), ["höhe"] = FWert.Z(3),
        ["ja"] = FWert.Wahr(true), ["nein"] = FWert.Wahr(false), ["typ"] = FWert.T("A"),
    });

    static FWert Rechne(string formel) => Formel.Parsen(formel).Werten(Umgebung);

    static double Z(string formel)
    {
        var w = Rechne(formel);
        Assert.False(w.IstText, formel);
        return w.Zahl;
    }

    static void Nahe(double erwartet, double ist, double tol, string was) =>
        Assert.True(Math.Abs(erwartet - ist) <= tol, $"{was}: erwartet {erwartet}, erhalten {ist} (Toleranz {tol})");

    // ------------------------------------------------------------------ Vorrang und Vorzeichen

    [Theory]
    [InlineData("2 + 3 * 4", 14)]                 // Punkt vor Strich: 2 + 12
    [InlineData("(2 + 3) * 4", 20)]               // Klammer: 5 · 4
    [InlineData("10 - 4 - 3", 3)]                 // links nach rechts: (10 − 4) − 3 = 3 (rechts nach links gäbe 9)
    [InlineData("100 / 5 / 2", 10)]               // (100 / 5) / 2 = 10 (rechts nach links gäbe 40)
    [InlineData("8 / 2 * 4", 16)]                 // (8 / 2) · 4 = 16
    [InlineData("2 ^ 3 ^ 2", 512)]                // rechtsassoziativ: 2^(3^2) = 2^9 = 512 (links nach rechts: 8^2 = 64)
    [InlineData("1 - 2 ^ 2 ^ 0", -1)]             // 2^(2^0) = 2^1 = 2, 1 − 2 = −1 (linksassoziativ: (2^2)^0 = 1, Ergebnis 0)
    [InlineData("-2 ^ 2", -4)]                    // ^ bindet stärker als das Vorzeichen: −(2²) = −4
    [InlineData("(-2) ^ 2", 4)]                   // Klammer: (−2)² = 4
    [InlineData("2 ^ -2", 0.25)]                  // Vorzeichen im Exponenten: 2^(−2) = 1/4
    [InlineData("2 * 3 ^ 2", 18)]                 // Potenz vor Mal: 2 · 9
    [InlineData("-3 ^ 2 + 10", 1)]                // −(9) + 10 = 1
    [InlineData("-(3 + 4) * 2", -14)]             // −7 · 2
    [InlineData("--5", 5)]                        // doppeltes Vorzeichen: −(−5)
    [InlineData("+5 - -3", 8)]                    // 5 − (−3)
    [InlineData("2 ^ 0.5 * 2 ^ 0.5", 2)]          // √2 · √2 = 2
    [InlineData("1.5e3", 1500)]                   // 1.5 · 10³
    [InlineData(".5 * 4", 2)]                     // 0.5 · 4
    [InlineData("breite * 2 - 10", 70)]           // 40 · 2 − 10
    [InlineData("höhe + 1", 4)]                   // Umlaut im Namen: 3 + 1
    public void Vorrang_Vorzeichen_Zahlen_Namen(string formel, double erwartet) => Nahe(erwartet, Z(formel), 1e-9, formel);

    // ------------------------------------------------------------------ Funktionen

    [Theory]
    [InlineData("if(3 > 2, 10, 20)", 10)]                // Bedingung wahr → erster Zweig
    [InlineData("if(3 < 2, 10, 20)", 20)]                // falsch → zweiter Zweig
    [InlineData("if(1, 8, 1 / 0)", 8)]                   // nur der gewählte Zweig wird gerechnet: 1/0 kommt nie dran
    [InlineData("if(0, 1 / 0, 9)", 9)]
    [InlineData("and(1, 1, 0)", 0)]                      // eine 0 genügt
    [InlineData("and(2 > 1, 3 > 2)", 1)]
    [InlineData("or(0, 0, 5)", 1)]                       // ein Wert ungleich 0 ist wahr
    [InlineData("or(0, 0, 0)", 0)]
    [InlineData("not(0)", 1)]
    [InlineData("not(7)", 0)]
    [InlineData("min(4, -2, 9)", -2)]
    [InlineData("max(4, -2, 9)", 9)]
    [InlineData("max(5)", 5)]
    [InlineData("abs(-3.5)", 3.5)]
    [InlineData("abs(2)", 2)]
    [InlineData("sqrt(144)", 12)]                        // 12 · 12 = 144
    [InlineData("sqrt(2)", 1.4142135623730951)]          // bekannter Wert √2
    [InlineData("round(0.5)", 1)]                        // .5 von null weg
    [InlineData("round(1.5)", 2)]
    [InlineData("round(2.5)", 3)]                        // nicht auf gerade (Bankers) gerundet
    [InlineData("round(-0.5)", -1)]                      // von null weg: −0.5 → −1
    [InlineData("round(-2.5)", -3)]
    [InlineData("round(2.4999)", 2)]
    [InlineData("round(-2.6)", -3)]
    [InlineData("roundup(2.0001)", 3)]                   // nächste ganze Zahl aufwärts
    [InlineData("roundup(-2.5)", -2)]                    // aufwärts heisst grösser: −2.5 → −2
    [InlineData("roundup(7)", 7)]
    [InlineData("rounddown(2.999)", 2)]                  // abwärts
    [InlineData("rounddown(-0.5)", -1)]                  // abwärts heisst kleiner: −0.5 → −1
    [InlineData("rounddown(7)", 7)]
    [InlineData("sin(30)", 0.5)]                         // Grad
    [InlineData("sin(90)", 1)]
    [InlineData("sin(0)", 0)]
    [InlineData("cos(60)", 0.5)]
    [InlineData("cos(180)", -1)]
    [InlineData("cos(0)", 1)]
    [InlineData("tan(45)", 1)]
    [InlineData("tan(135)", -1)]                         // tan(180° − 45°) = −tan 45°
    [InlineData("asin(1)", 90)]                          // Ergebnis in Grad
    [InlineData("asin(-0.5)", -30)]
    [InlineData("acos(0)", 90)]
    [InlineData("acos(-1)", 180)]
    [InlineData("atan(1)", 45)]
    [InlineData("atan(-1)", -45)]
    [InlineData("atan(0)", 0)]
    [InlineData("pi()", 3.141592653589793)]
    [InlineData("2 * pi() * 50", 314.1592653589793)]     // Umfang r 50: 2 · π · 50 = 100 · π
    [InlineData("sin(pi())", 0.0548036651)]              // pi() ist eine Zahl; sin rechnet in Grad: π° = 0.0548311 rad, sin ≈ x − x³/6 = 0.0548311 − 0.0000275 = 0.0548037
    [InlineData("sqrt(3 ^ 2 + 4 ^ 2)", 5)]               // 9 + 16 = 25
    [InlineData("round(10 / 3 * 3)", 10)]                // 10/3·3 = 10 (bis auf Rundungsfehler)
    [InlineData("round(2.5) + roundup(2.1) + rounddown(2.9)", 8)]   // 3 + 3 + 2
    [InlineData("max(1, 2, 3) - min(1, 2, 3)", 2)]       // 3 − 1
    public void Funktionen_von_Hand(string formel, double erwartet) => Nahe(erwartet, Z(formel), 1e-9, formel);

    // ------------------------------------------------------------------ Vergleiche, Text, ja_nein

    [Theory]
    [InlineData("3 >= 3", 1)]
    [InlineData("3 <= 2", 0)]
    [InlineData("2 <> 2", 0)]
    [InlineData("2 <> 3", 1)]
    [InlineData("1 + 1 = 2", 1)]                  // Vergleich bindet schwächer als +: (1 + 1) = 2
    [InlineData("5 < 5", 0)]
    [InlineData("5 > 4", 1)]
    [InlineData("0.1 + 0.2 = 0.3", 1)]            // Gleichheit mit relativer Toleranz 1e-9 (Annahme im Dateikopf)
    [InlineData("if(ja, 100, 50)", 100)]          // ja_nein ist 1
    [InlineData("if(nein, 100, 50)", 50)]         // ja_nein ist 0
    [InlineData("not(ja)", 0)]
    [InlineData("ja + ja", 2)]                    // 1 + 1
    [InlineData("and(ja, not(nein))", 1)]
    [InlineData("if(and(hoehe > 500, breite < 50), min(hoehe, 1000), max(hoehe, 1000))", 600)]   // 600 > 500 und 40 < 50 → min(600, 1000)
    public void Vergleiche_und_JaNein(string formel, double erwartet) => Nahe(erwartet, Z(formel), 1e-9, formel);

    [Fact]
    public void Text_nur_mit_Gleich_und_Ungleich()
    {
        Assert.Equal(1, Z("typ = \"A\""));                   // gleicher Text
        Assert.Equal(0, Z("typ = \"a\""));                   // Gross/Klein zählt (Ordinal)
        Assert.Equal(1, Z("typ <> \"B\""));
        Assert.Equal(1, Z("\"A\" = \"A\""));
        Assert.Equal("ja", Rechne("if(typ = \"A\", \"ja\", \"nein\")").Text);
        // «typ + 1»: das + steht an Stelle 5 (t1 y2 p3 Leerzeichen4 +5)
        var f = Assert.Throws<FormelFehler>(() => Rechne("typ + 1"));
        Assert.Equal(5, f.Stelle);
        Assert.Contains("Text", f.Message);
        // «typ < "B"»: das < steht an Stelle 5
        Assert.Equal(5, Assert.Throws<FormelFehler>(() => Rechne("typ < \"B\"")).Stelle);
        Assert.Contains("Text mit Zahl", Assert.Throws<FormelFehler>(() => Rechne("typ = 1")).Message);
    }

    // ------------------------------------------------------------------ Fehler mit Stelle

    [Theory]
    [InlineData("10 / (5 - 5)", 4, "Division durch 0")]       // Zeichen: 1 0 Leerzeichen / → das / ist das 4. Zeichen
    [InlineData("1 + 8 / 0", 7, "Division durch 0")]          // 1 Leerzeichen + Leerzeichen 8 Leerzeichen / → 7. Zeichen
    [InlineData("hoehe / (breite - 40)", 7, "Division durch 0")]   // hoehe hat 5 Zeichen, Leerzeichen, / ist das 7.
    [InlineData("0 ^ -1", 3, "Division durch 0")]             // 0 hoch negativ = 1 / 0; das ^ ist das 3. Zeichen
    [InlineData("breite + hoehee", 10, "unbekannter Name")]   // «breite + » sind 9 Zeichen, der Name beginnt beim 10.
    [InlineData("2 * unbekannt", 5, "unbekannter Name")]      // 2 Leerzeichen * Leerzeichen → Name ab dem 5. Zeichen
    [InlineData("sqrt(-4)", 1, "negativen")]                  // Wurzel aus negativer Zahl; der Aufruf beginnt beim 1. Zeichen
    [InlineData("tan(90)", 1, "nicht definiert")]
    [InlineData("asin(1.5)", 1, "zwischen")]                  // asin nur für −1 … 1
    public void Auswerten_Fehler_mit_Stelle(string formel, int stelle, string teil)
    {
        var f = Assert.Throws<FormelFehler>(() => Rechne(formel));
        Assert.Equal(stelle, f.Stelle);
        Assert.Contains(teil, f.Message);
    }

    [Theory]
    [InlineData("2 *", 4, "endet")]                  // nach dem * fehlt der Wert; die Formel hat 3 Zeichen, die Stelle liegt dahinter (4)
    [InlineData("(1 + 2", 7, "fehlt")]               // 6 Zeichen, die schliessende Klammer fehlt an Stelle 7
    [InlineData("1 + 2)", 6, "ohne öffnende")]       // die überzählige ) ist das 6. Zeichen
    [InlineData("max(1, 2", 9, "fehlt")]             // 8 Zeichen, Funktionsaufruf nicht geschlossen → Stelle 9
    [InlineData("sinn(30)", 1, "Unbekannte Funktion")]
    [InlineData("if(1, 2)", 1, "braucht")]           // if braucht drei Argumente
    [InlineData("1 < 2 < 3", 7, "verketten")]        // zweites < ist das 7. Zeichen
    [InlineData("", 0, "leer")]
    public void Zerlegen_Fehler_mit_Stelle(string formel, int stelle, string teil)
    {
        var f = Assert.Throws<FormelFehler>(() => Formel.Parsen(formel));
        Assert.Equal(stelle, f.Stelle);
        Assert.Contains(teil, f.Message);
    }

    // ------------------------------------------------------------------ Fehler in der Familie: unbekannter Name, Zyklus, Division durch 0

    [Fact]
    public void Familie_unbekannter_Name_in_Parameterformel_mit_Stelle_und_Vorschlag()
    {
        var f = FamilieTestHilfe.Def(FamilieTestHilfe.Mini("""
            [{ "name": "breite", "art": "typ", "datentyp": "laenge", "wert": 40 },
             { "name": "x", "art": "typ", "datentyp": "laenge", "formel": "breite + hoehee" }]
            """));
        var fehler = Assert.Single(f.Fehler);
        Assert.Equal("parameter «x».formel", fehler.Stelle);
        // «breite + » = 9 Zeichen → der unbekannte Name beginnt bei Stelle 10
        Assert.Contains("Stelle 10: unbekannter Name «hoehee»", fehler.Meldung);
        Assert.NotNull(fehler.Vorschlag);
    }

    [Fact]
    public void Familie_Zyklus_a_b_a_wird_mit_dem_Weg_gemeldet()
    {
        var f = FamilieTestHilfe.Def(FamilieTestHilfe.Mini("""
            [{ "name": "a", "art": "typ", "datentyp": "laenge", "formel": "b + 1" },
             { "name": "b", "art": "typ", "datentyp": "laenge", "formel": "a * 2" }]
            """));
        var z = Assert.Single(f.Fehler, x => x.Meldung.Contains("Zirkuläre"));
        Assert.Contains("a → b → a", z.Meldung);
    }

    [Fact]
    public void Familie_Selbstbezug_ist_ein_Zyklus()
    {
        var f = FamilieTestHilfe.Def(FamilieTestHilfe.Mini("""[{ "name": "c", "art": "typ", "datentyp": "laenge", "formel": "c + 1" }]"""));
        Assert.Contains(f.Fehler, x => x.Meldung.Contains("Zirkuläre"));
    }

    [Fact]
    public void Familie_Division_durch_0_nur_beim_Typ_mit_dem_Nullwert()
    {
        var f = FamilieTestHilfe.Def(FamilieTestHilfe.Mini("""
            [{ "name": "n", "art": "typ", "datentyp": "zahl" },
             { "name": "k", "art": "typ", "datentyp": "zahl", "formel": "100 / n" }]
            """, """[{ "name": "vier", "werte": { "n": 4 } }, { "name": "null", "werte": { "n": 0 } }]"""));
        Assert.True(f.Gueltig, string.Join("; ", f.Fehler));
        // 100 / 4 = 25
        Assert.Equal(25, FamilienAuswertung.Aufloesen(f, "vier", null).Wert("k")!.Value.Zahl);
        // «100 / n»: das / ist das 5. Zeichen (1 0 0 Leerzeichen /)
        var x = Assert.Single(FamilienAuswertung.Aufloesen(f, "null", null).Fehler);
        Assert.Contains("Stelle 5: Division durch 0", x.Meldung);
    }

    // ------------------------------------------------------------------ Reihenfolge der Werte: Exemplar › Typ › Formel › Vorgabe

    /// <summary>Kleine eigene Familie: a (Exemplar, Vorgabe 10), b (Typ, Vorgabe 20), c (Typ, Vorgabe 30), d (Exemplar, Formel a + b).
    /// Typ T1 setzt a = 11 und b = 21, Typ T2 setzt nichts.</summary>
    static FamilieDef Reihenfolge() => FamilieTestHilfe.Def(FamilieTestHilfe.Mini("""
        [{ "name": "a", "art": "exemplar", "datentyp": "laenge", "wert": 10 },
         { "name": "b", "art": "typ", "datentyp": "laenge", "wert": 20 },
         { "name": "c", "art": "typ", "datentyp": "laenge", "wert": 30 },
         { "name": "d", "art": "exemplar", "datentyp": "laenge", "formel": "a + b" }]
        """, """[{ "name": "T1", "werte": { "a": 11, "b": 21 } }, { "name": "T2", "werte": {} }]"""));

    static double W(FamAufloesung a, string name) => a.Wert(name)!.Value.Zahl;

    [Fact]
    public void Reihenfolge_Vorgabe_Typ_Exemplar_und_Formel_von_Hand()
    {
        var f = Reihenfolge();
        Assert.True(f.Gueltig, string.Join("; ", f.Fehler));

        // T2 ohne Exemplarwerte: alles aus der Vorgabe: a 10, b 20, c 30; Formel d = a + b = 10 + 20 = 30
        var t2 = FamilienAuswertung.Aufloesen(f, "T2", null);
        Assert.True(t2.Gueltig, string.Join("; ", t2.Fehler));
        Assert.Equal(new[] { 10.0, 20.0, 30.0, 30.0 }, new[] { W(t2, "a"), W(t2, "b"), W(t2, "c"), W(t2, "d") });

        // T1: Typwert vor Vorgabe: a 11, b 21, c bleibt 30; d = 11 + 21 = 32 (die Formel sieht die Typwerte)
        var t1 = FamilienAuswertung.Aufloesen(f, "T1", null);
        Assert.Equal(new[] { 11.0, 21.0, 30.0, 32.0 }, new[] { W(t1, "a"), W(t1, "b"), W(t1, "c"), W(t1, "d") });

        // T1 mit Exemplarwert a = 5: Exemplar vor Typ: a 5, b 21; d = 5 + 21 = 26
        var e1 = FamilienAuswertung.Aufloesen(f, "T1", new JsonObject { ["a"] = 5 });
        Assert.Equal(new[] { 5.0, 21.0, 30.0, 26.0 }, new[] { W(e1, "a"), W(e1, "b"), W(e1, "c"), W(e1, "d") });

        // T2 mit Exemplarwert a = 7: Exemplar vor Vorgabe: a 7, b 20; d = 7 + 20 = 27
        var e2 = FamilienAuswertung.Aufloesen(f, "T2", new JsonObject { ["a"] = 7 });
        Assert.Equal(new[] { 7.0, 20.0, 30.0, 27.0 }, new[] { W(e2, "a"), W(e2, "b"), W(e2, "c"), W(e2, "d") });
    }

    [Fact]
    public void Reihenfolge_Exemplarwert_fuer_Typparameter_oder_Formelparameter_ist_Fehler()
    {
        var f = Reihenfolge();
        // b ist ein Typparameter: pro Exemplar nicht einstellbar
        var b = FamilienAuswertung.Aufloesen(f, "T1", new JsonObject { ["b"] = 99 });
        Assert.False(b.Gueltig);
        Assert.Contains(b.Fehler, x => x.Meldung.Contains("«b» ist ein Typparameter"));
        // d hat eine Formel: weder pro Exemplar noch pro Typ einstellbar
        var d = FamilienAuswertung.Aufloesen(f, "T1", new JsonObject { ["d"] = 99 });
        Assert.False(d.Gueltig);
        Assert.Contains(d.Fehler, x => x.Meldung.Contains("«d» hat eine Formel"));
        // ein Typwert für den Formelparameter ist schon in der Definition ein Fehler
        var g = FamilieTestHilfe.Def(FamilieTestHilfe.Mini("""[{ "name": "q", "art": "typ", "datentyp": "zahl", "formel": "1 + 1" }]""",
            """[{ "name": "A", "werte": { "q": 5 } }]"""));
        Assert.Contains(g.Fehler, x => x.Stelle == "typ «A».werte.q" && x.Meldung.Contains("nicht pro Typ"));
        // Formel und Vorgabe zugleich: nicht erlaubt (die Formel wäre sonst nie die letzte Instanz)
        var h = FamilieTestHilfe.Def(FamilieTestHilfe.Mini("""[{ "name": "q", "art": "typ", "datentyp": "zahl", "formel": "1", "wert": 2 }]"""));
        Assert.Contains(h.Fehler, x => x.Meldung.Contains("formel und wert zugleich"));
    }

    [Fact]
    public void Typregel_Typformel_mit_Exemplarparameter_ist_Fehler_Exemplarformel_mit_Typparameter_nicht()
    {
        // e ist ein Typparameter, seine Formel «a + 1» benutzt den Exemplarparameter a → Fehler an Stelle 1 der Formel
        var f = FamilieTestHilfe.Def(FamilieTestHilfe.Mini("""
            [{ "name": "a", "art": "exemplar", "datentyp": "laenge", "wert": 10 },
             { "name": "t", "art": "typ", "datentyp": "laenge", "wert": 20 },
             { "name": "e", "art": "typ", "datentyp": "laenge", "formel": "a + 1" },
             { "name": "ok1", "art": "exemplar", "datentyp": "laenge", "formel": "a + t" },
             { "name": "ok2", "art": "typ", "datentyp": "laenge", "formel": "t * 2" }]
            """));
        var x = Assert.Single(f.Fehler);
        Assert.Equal("parameter «e».formel", x.Stelle);
        Assert.Contains("Stelle 1", x.Meldung);
        Assert.Contains("Exemplarparameter «a»", x.Meldung);
        Assert.Contains("nur Typparameter", x.Vorschlag);
    }
}
