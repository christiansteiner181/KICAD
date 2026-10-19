using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.FamilieTestHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 2, B1: Formelsprache der Familien (wie Revit) – Operatoren und Vorrang, alle Funktionen, Text und ja_nein,
/// Fehler mit Stelle, Zyklus, Typregel, Reihenfolge der Werte. Alle Zahlen frei gewählt.</summary>
public class FamilieFormelTests
{
    sealed class Werte(Dictionary<string, FWert> w) : IFormelUmgebung
    {
        public FWert Wert(string name, int stelle) =>
            w.TryGetValue(name, out var x) ? x : throw new FormelFehler($"unbekannter Name «{name}»", stelle, "Test");
    }

    static readonly Werte Umgebung = new(new()
    {
        ["hoehe"] = FWert.Z(600), ["laenge"] = FWert.Z(1000), ["x"] = FWert.Z(0), ["länge_ü"] = FWert.Z(3),
        ["mit_abdeckung"] = FWert.Z(1), ["ohne"] = FWert.Z(0), ["typ"] = FWert.T("A"), ["Gross"] = FWert.Z(7),
    });

    static double Z(string formel) { var w = Formel.Parsen(formel).Werten(Umgebung); Assert.False(w.IstText, formel); return w.Zahl; }

    static FormelFehler Fehler(string formel) => Assert.Throws<FormelFehler>(() => Formel.Parsen(formel).Werten(Umgebung));

    // ------------------------------------------------------------------ Vorrang

    [Theory]
    [InlineData("1 + 2 * 3", 7)]                // * vor +
    [InlineData("(1 + 2) * 3", 9)]              // Klammern
    [InlineData("10 - 4 - 3", 3)]               // linksbündig
    [InlineData("12 / 4 / 3", 1)]               // linksbündig
    [InlineData("2 ^ 3 ^ 2", 512)]              // ^ rechtsbündig: 2^(3^2)
    [InlineData("-2 ^ 2", -4)]                  // ^ vor Vorzeichen (Annahme, wie Mathematik)
    [InlineData("(-2) ^ 2", 4)]
    [InlineData("2 ^ -1", 0.5)]
    [InlineData("2 * 3 ^ 2", 18)]               // ^ vor *
    [InlineData("1 + 2 < 4", 1)]                // Vergleich zuletzt: (1+2) < 4
    [InlineData("2 * 3 = 6", 1)]
    [InlineData("1 <> 1", 0)]
    [InlineData("3 >= 3", 1)]
    [InlineData("3 <= 2", 0)]
    [InlineData("3 > 2", 1)]
    [InlineData("0.1 + 0.2 = 0.3", 1)]          // Gleichheit mit Toleranz 1e-9 (Annahme)
    [InlineData("hoehe - 2 * 30", 540)]
    [InlineData("+5 - -3", 8)]
    [InlineData("1.5e3 / .5", 3000)]
    [InlineData("länge_ü * 2", 6)]              // Umlaute in Namen
    [InlineData("Gross + 1", 8)]
    public void Vorrang_UndGrundrechenarten(string formel, double erwartet) => Etwa(erwartet, Z(formel), 1e-12, formel);

    // ------------------------------------------------------------------ Funktionen

    [Theory]
    [InlineData("if(hoehe > 500, 10, 20)", 10)]
    [InlineData("if(hoehe < 500, 10, 20)", 20)]
    [InlineData("if(x <> 0, 1 / x, -1)", -1)]   // nur der gewählte Zweig wird gerechnet (keine Division durch 0)
    [InlineData("and(1, 1, 1)", 1)]
    [InlineData("and(hoehe > 500, laenge < 900)", 0)]
    [InlineData("or(0, 0, 1)", 1)]
    [InlineData("or(ohne, x)", 0)]
    [InlineData("not(ohne)", 1)]
    [InlineData("not(mit_abdeckung)", 0)]       // ja_nein als 1/0
    [InlineData("min(3, 1, 2)", 1)]
    [InlineData("max(3, 1, 2)", 3)]
    [InlineData("abs(-5)", 5)]
    [InlineData("sqrt(16)", 4)]
    [InlineData("round(2.5)", 3)]               // .5 von null weg (Annahme)
    [InlineData("round(-2.5)", -3)]
    [InlineData("round(2.4)", 2)]
    [InlineData("roundup(2.1)", 3)]
    [InlineData("roundup(-2.1)", -2)]
    [InlineData("rounddown(2.9)", 2)]
    [InlineData("rounddown(-2.1)", -3)]
    [InlineData("sin(30)", 0.5)]                // Grad
    [InlineData("cos(60)", 0.5)]
    [InlineData("tan(45)", 1)]
    [InlineData("asin(0.5)", 30)]               // Ergebnis in Grad
    [InlineData("acos(0.5)", 60)]
    [InlineData("atan(1)", 45)]
    [InlineData("pi()", Math.PI)]
    [InlineData("2 * pi() * 100", 2 * Math.PI * 100)]
    [InlineData("IF(1, 2, 3)", 2)]              // Funktionsnamen gross/klein egal (Annahme)
    [InlineData("max (1, 2)", 2)]
    public void Funktionen(string formel, double erwartet) => Etwa(erwartet, Z(formel), 1e-9, formel);

    [Fact]
    public void Text_NurGleichUndUngleich()
    {
        Assert.Equal(1, Z("typ = \"A\""));
        Assert.Equal(1, Z("typ <> \"B\""));
        Assert.Equal("ja", Formel.Parsen("if(typ = \"A\", \"ja\", \"nein\")").Werten(Umgebung).Text);
        var f = Fehler("typ + 1");
        Assert.Equal(5, f.Stelle);
        Assert.Contains("Text", f.Message);
        Assert.Equal(5, Fehler("typ < \"B\"").Stelle);
        Assert.Contains("Text mit Zahl", Fehler("typ = 1").Message);
        Assert.Contains("Text", Fehler("if(typ, 1, 2)").Message);
        Assert.Contains("Text", Fehler("-typ").Message);
    }

    // ------------------------------------------------------------------ Fehler mit Stelle

    [Theory]
    [InlineData("hoehe + 2 *", 12, "endet")]
    [InlineData("(1 + 2", 7, "«)» fehlt")]
    [InlineData("1 + 2)", 6, "ohne öffnende")]
    [InlineData("1 + )", 5, "fehlt ein Wert")]
    [InlineData("2 $ 3", 3, "Unerwartetes")]
    [InlineData("$", 1, "Unerlaubtes Zeichen")]
    [InlineData("sinn(30)", 1, "Unbekannte Funktion")]
    [InlineData("if(1, 2)", 1, "braucht 3")]
    [InlineData("pi(1)", 1, "braucht 0")]
    [InlineData("1 < 2 < 3", 7, "nicht verketten")]
    [InlineData("600mm", 1, "ohne Operator")]
    [InlineData("\"offen", 1, "Anführungszeichen")]
    [InlineData("max(1, 2", 9, "«)» fehlt")]
    [InlineData("", 0, "leer")]
    public void Zerlegen_FehlerMitStelle(string formel, int stelle, string teil)
    {
        var f = Assert.Throws<FormelFehler>(() => Formel.Parsen(formel));
        Assert.Equal(stelle, f.Stelle);
        Assert.Contains(teil, f.Message);
        Assert.False(string.IsNullOrWhiteSpace(f.Vorschlag));
    }

    [Fact]
    public void Zerlegen_TippfehlerInFunktionNenntVorschlag() =>
        Assert.Contains("sin", Assert.Throws<FormelFehler>(() => Formel.Parsen("sinn(30)")).Vorschlag);

    [Theory]
    [InlineData("hoehee + 1", 1, "unbekannter Name")]
    [InlineData("1 / (2 - 2)", 3, "Division durch 0")]
    [InlineData("hoehe / x", 7, "Division durch 0")]
    [InlineData("0 ^ -1", 3, "Division durch 0")]
    [InlineData("sqrt(-1)", 1, "negativen")]
    [InlineData("asin(2)", 1, "zwischen")]
    [InlineData("tan(90)", 1, "nicht definiert")]
    [InlineData("(-8) ^ 0.5", 6, "keine endliche Zahl")]
    public void Auswerten_FehlerMitStelle(string formel, int stelle, string teil)
    {
        var f = Fehler(formel);
        Assert.Equal(stelle, f.Stelle);
        Assert.Contains(teil, f.Message);
        Assert.False(string.IsNullOrWhiteSpace(f.Vorschlag));
    }

    [Fact]
    public void Namen_WerdenMitStelleGesammelt()
    {
        var namen = Formel.Parsen("if(mit_abdeckung, hoehe + 10, laenge)").Namen();
        Assert.Equal(new List<(string, int)> { ("mit_abdeckung", 4), ("hoehe", 19), ("laenge", 31) }, namen);
    }

    // ------------------------------------------------------------------ in der Familie: Zyklus, Typregel, unbekannte Namen

    [Fact]
    public void Familie_ZirkulaereFormelIstFehlerMitWeg()
    {
        var f = Def(Mini("""
            [{ "name": "a", "art": "typ", "datentyp": "laenge", "formel": "b + 1" },
             { "name": "b", "art": "typ", "datentyp": "laenge", "formel": "c * 2" },
             { "name": "c", "art": "typ", "datentyp": "laenge", "formel": "a - 3" }]
            """));
        var z = Assert.Single(f.Fehler, x => x.Meldung.Contains("Zirkuläre"));
        Assert.Contains("a → b → c → a", z.Meldung);
        Assert.NotNull(z.Vorschlag);
    }

    [Fact]
    public void Familie_TypformelDarfNurTypparameterVerwenden()
    {
        var f = Def(Mini("""
            [{ "name": "hoehe", "art": "typ", "datentyp": "laenge", "wert": 600 },
             { "name": "boden", "art": "exemplar", "datentyp": "laenge", "wert": 150 },
             { "name": "ok_typ", "art": "typ", "datentyp": "laenge", "formel": "boden + hoehe" },
             { "name": "ok_exemplar", "art": "exemplar", "datentyp": "laenge", "formel": "boden + hoehe" }]
            """));
        var x = Assert.Single(f.Fehler);
        Assert.Equal("parameter «ok_typ».formel", x.Stelle);
        Assert.Contains("Stelle 1", x.Meldung);
        Assert.Contains("Exemplarparameter «boden»", x.Meldung);
        Assert.Contains("nur Typparameter", x.Vorschlag);
    }

    [Fact]
    public void Familie_FormelparameterNichtProTypEinstellbar()
    {
        var f = Def(Mini("""[{ "name": "a", "art": "typ", "datentyp": "zahl", "formel": "1 + 1" }]""", """[{ "name": "A", "werte": { "a": 5 } }]"""));
        Assert.Contains(f.Fehler, x => x.Stelle == "typ «A».werte.a" && x.Meldung.Contains("nicht pro Typ"));
        var g = Def(Mini("""[{ "name": "a", "art": "typ", "datentyp": "zahl", "formel": "1", "wert": 2 }]"""));
        Assert.Contains(g.Fehler, x => x.Meldung.Contains("formel und wert zugleich"));
    }

    [Fact]
    public void Familie_UnbekannterNameInKoerperformelMitVorschlag()
    {
        var f = Def(Mini("""[{ "name": "hoehe", "art": "typ", "datentyp": "laenge", "wert": 600 }]""",
            koerper: """[{ "id": "k", "art": "quader", "von": [0, 0, 0], "bis": [100, 100, "hoehee"] }]"""));
        var x = Assert.Single(f.Fehler);
        Assert.Equal("koerper «k».bis[3]", x.Stelle);
        Assert.Contains("Stelle 1: unbekannter Name «hoehee»", x.Meldung);
        Assert.Contains("Gemeint: hoehe", x.Vorschlag);
    }

    [Fact]
    public void Familie_SyntaxfehlerInParameterformelMitStelle()
    {
        var f = Def(Mini("""[{ "name": "a", "art": "typ", "datentyp": "zahl", "formel": "(1 + 2" }]"""));
        var x = Assert.Single(f.Fehler);
        Assert.Equal("parameter «a».formel", x.Stelle);
        Assert.Contains("Stelle 7", x.Meldung);
    }

    // ------------------------------------------------------------------ Reihenfolge der Werte

    [Fact]
    public void Reihenfolge_ExemplarwertVorTypwertVorFormelVorVorgabe()
    {
        var f = Def(Mini("""
            [{ "name": "p", "art": "exemplar", "datentyp": "laenge", "wert": 1 },
             { "name": "q", "art": "typ", "datentyp": "laenge", "wert": 10 },
             { "name": "r", "art": "exemplar", "datentyp": "laenge", "formel": "p + q" }]
            """, """[{ "name": "A", "werte": { "p": 2, "q": 20 } }, { "name": "B", "werte": {} }]"""));
        Assert.True(f.Gueltig, string.Join("; ", f.Fehler));
        var mitExemplar = FamilienAuswertung.Aufloesen(f, "A", new JsonObject { ["p"] = 3 });
        Assert.Equal(3, mitExemplar.Wert("p")!.Value.Zahl);        // Exemplarwert
        Assert.Equal(23, mitExemplar.Wert("r")!.Value.Zahl);       // Formel mit Exemplar- und Typwert
        var typ = FamilienAuswertung.Aufloesen(f, "A", null);
        Assert.Equal(2, typ.Wert("p")!.Value.Zahl);                // Typwert (typbezogener Vorgabewert, Annahme)
        Assert.Equal(22, typ.Wert("r")!.Value.Zahl);
        var vorgabe = FamilienAuswertung.Aufloesen(f, "B", null);
        Assert.Equal(1, vorgabe.Wert("p")!.Value.Zahl);            // Vorgabewert
        Assert.Equal(11, vorgabe.Wert("r")!.Value.Zahl);
    }

    [Fact]
    public void Auswertung_FehlenderTypwertIstFehlerMitVorschlag()
    {
        var f = Def(Mini("""
            [{ "name": "hoehe", "art": "typ", "datentyp": "laenge" },
             { "name": "ok", "art": "typ", "datentyp": "laenge", "formel": "hoehe + 10" }]
            """, """[{ "name": "A", "werte": { "hoehe": 600 } }, { "name": "B", "werte": {} }]"""));
        Assert.True(f.Gueltig);
        Assert.True(FamilienAuswertung.Aufloesen(f, "A", null).Gueltig);
        Assert.Equal(610, FamilienAuswertung.Aufloesen(f, "A", null).Wert("ok")!.Value.Zahl);
        var b = FamilienAuswertung.Aufloesen(f, "B", null);
        var x = Assert.Single(b.Fehler);
        Assert.Equal("parameter «ok».formel", x.Stelle);
        Assert.Contains("Stelle 1: Parameter «hoehe» hat keinen Wert", x.Meldung);
        Assert.Contains("Typ «B»", x.Vorschlag);
    }

    [Fact]
    public void Auswertung_DivisionDurchNullNurBeiDiesemTyp()
    {
        var f = Def(Mini("""
            [{ "name": "n", "art": "typ", "datentyp": "zahl" },
             { "name": "k", "art": "typ", "datentyp": "zahl", "formel": "100 / n" }]
            """, """[{ "name": "gut", "werte": { "n": 4 } }, { "name": "null", "werte": { "n": 0 } }]"""));
        Assert.Equal(25, FamilienAuswertung.Aufloesen(f, "gut", null).Wert("k")!.Value.Zahl);
        var x = Assert.Single(FamilienAuswertung.Aufloesen(f, "null", null).Fehler);
        Assert.Contains("Stelle 5: Division durch 0", x.Meldung);
    }

    [Fact]
    public void Auswertung_JaNeinUndTextWerte()
    {
        var f = Def(Mini("""
            [{ "name": "mit", "art": "typ", "datentyp": "ja_nein", "wert": true },
             { "name": "art", "art": "typ", "datentyp": "text", "wert": "A" },
             { "name": "breite", "art": "typ", "datentyp": "laenge", "formel": "if(and(mit, art = \"A\"), 100, 50)" },
             { "name": "bez", "art": "typ", "datentyp": "text", "formel": "if(mit, \"mit\", \"ohne\")" }]
            """, """[{ "name": "A", "werte": {} }, { "name": "B", "werte": { "mit": false } }]"""));
        Assert.True(f.Gueltig, string.Join("; ", f.Fehler));
        var a = FamilienAuswertung.Aufloesen(f, "A", null);
        Assert.Equal(1, a.Wert("mit")!.Value.Zahl);
        Assert.Equal(100, a.Wert("breite")!.Value.Zahl);
        Assert.Equal("mit", a.Wert("bez")!.Value.Text);
        var b = FamilienAuswertung.Aufloesen(f, "B", null);
        Assert.Equal(0, b.Wert("mit")!.Value.Zahl);
        Assert.Equal(50, b.Wert("breite")!.Value.Zahl);
        Assert.Equal("ohne", b.Wert("bez")!.Value.Text);
    }

    [Fact]
    public void Auswertung_TextformelFuerZahlparameterIstTypfehler()
    {
        var f = Def(Mini("""[{ "name": "a", "art": "typ", "datentyp": "laenge", "formel": "\"x\"" }]"""));
        Assert.True(f.Gueltig);
        Assert.Contains("ergibt aber den Text", Assert.Single(FamilienAuswertung.Aufloesen(f, "A", null).Fehler).Meldung);
    }

    [Fact]
    public void Auswertung_ZwischenspeicherJeTypUndExemplarwerten()
    {
        var f = Def(Testheizkoerper());
        var a1 = FamilienAuswertung.Aufloesen(f, "22-600-1000", null);
        Assert.Same(a1, FamilienAuswertung.Aufloesen(f, "22-600-1000", null));
        var e1 = FamilienAuswertung.Aufloesen(f, "22-600-1000", new JsonObject { ["bodenabstand"] = 300 });
        Assert.NotSame(a1, e1);
        Assert.Same(e1, FamilienAuswertung.Aufloesen(f, "22-600-1000", new JsonObject { ["bodenabstand"] = 300 }));
        // Handrechnung: oberkante = bodenabstand + hoehe = 150 + 600 = 750 bzw. 300 + 600 = 900
        Assert.Equal(750, a1.Wert("oberkante")!.Value.Zahl);
        Assert.Equal(900, e1.Wert("oberkante")!.Value.Zahl);
    }
}
