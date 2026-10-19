using System.Globalization;
using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.FamiliePruefungHilfe;
using static Hlks.Kern.Tests.GrundlagenHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 2, Auftrag B6: unabhängige Referenzfälle von Hand – Quellenprüfung: Zahlen aus Unterlagen lesen (Dezimalkomma,
/// Tausenderapostroph, Brüche, Vorsätze, Einheiten) und die Toleranzgrenzen genau auf der Grenze und knapp darüber.
/// Toleranzen nach entwurf/modul-2.md («Zusammenführen, Konflikte, Abdeckung»): Längen max(1 mm; 0.5 % des Quellwerts), Durchmesser
/// 1 mm, Anschlusslage 2 mm, Kennwerte ± halbe letzte Stelle der Unterlage. Alle Werte fiktiv.</summary>
public class Referenz2QuellenTests
{
    // ------------------------------------------------------------------ Zahlen lesen

    [Theory]
    [InlineData("1,5", 1.5, 1)]          // Dezimalkomma: eine Nachkommastelle
    [InlineData("1.5", 1.5, 1)]          // Dezimalpunkt: gleicher Wert
    [InlineData("0,30", 0.30, 2)]        // zwei Stellen (die Null am Ende zählt)
    [InlineData("1'250", 1250, 0)]       // Tausenderapostroph: 1 · 1000 + 250
    [InlineData("1’250", 1250, 0)]       // typografischer Apostroph
    [InlineData("12'345,5", 12345.5, 1)] // 12 345 und ein halbes
    [InlineData("DN 25", 25, 0)]         // Vorsatz DN zählt nicht
    [InlineData("DN25", 25, 0)]
    [InlineData("Ø 28", 28, 0)]          // Vorsatz Ø
    [InlineData("Ø28,5", 28.5, 1)]
    [InlineData("−12", -12, 0)]          // Minuszeichen U+2212
    public void Zahl_lesen_Wert_und_Nachkommastellen(string text, double wert, int stellen)
    {
        var z = QuellZahlen.Lesen(text)!;
        Assert.NotNull(z);
        Assert.Equal(wert, z.Wert, 9);
        Assert.Equal(stellen, z.Stellen);
        Assert.Null(z.Einheit);
        Assert.False(z.Bruch);
    }

    [Theory]
    [InlineData("400 mm", 400, "mm")]
    [InlineData("0,4 m", 0.4, "m")]
    [InlineData("2,5 kPa", 2.5, "kPa")]
    [InlineData("10 l/s", 10, "l/s")]
    [InlineData("36 m³/h", 36, "m³/h")]
    [InlineData("36 m3/h", 36, "m³/h")]   // 3 als Hochzahl wird vereinheitlicht
    [InlineData("0,30 kg", 0.30, "kg")]
    public void Zahl_lesen_mit_Einheit_dahinter(string text, double wert, string einheit)
    {
        var z = QuellZahlen.Lesen(text)!;
        Assert.NotNull(z);
        Assert.Equal(wert, z.Wert, 9);
        Assert.Equal(einheit, z.Einheit);
    }

    [Theory]
    [InlineData("1 1/4", 1.25)]       // 1 + 1/4
    [InlineData("3/4", 0.75)]         // 3 : 4
    [InlineData("1-1/2", 1.5)]        // 1 + 1/2
    [InlineData("Rp 1 1/4", 1.25)]    // Vorsatz Rp
    [InlineData("G 3/4", 0.75)]       // Vorsatz G
    public void Bruch_ist_nur_mit_Bruchlesung_eine_Zahl(string text, double wert)
    {
        var z = QuellZahlen.Lesen(text)!;
        Assert.NotNull(z);
        Assert.Equal(wert, z.Wert, 9);
        Assert.True(z.Bruch);
        Assert.Null(QuellZahlen.Lesen(text, bruch: false));   // ohne Bruchlesung keine Zahl
    }

    [Theory]
    [InlineData("200 x 100")]
    [InlineData("Messing")]
    [InlineData("")]
    public void Keine_einzelne_Zahl(string text) => Assert.Null(QuellZahlen.Lesen(text));

    [Fact]
    public void Einheiten_umrechnen_von_Hand()
    {
        // 1 m = 1000 mm
        Assert.Equal(1000, QuellEinheiten.Faktor("m", "mm")!.Value, 12);
        Assert.Equal(0.001, QuellEinheiten.Faktor("mm", "m")!.Value, 12);
        // 1 l/s = 0.001 m³ je s = 0.001 · 3600 m³/h = 3.6 m³/h; umgekehrt 1/3.6
        Assert.Equal(3.6, QuellEinheiten.Faktor("l/s", "m³/h")!.Value, 12);
        Assert.Equal(1 / 3.6, QuellEinheiten.Faktor("m³/h", "l/s")!.Value, 12);
        // 1 kPa = 1000 Pa
        Assert.Equal(1000, QuellEinheiten.Faktor("kPa", "Pa")!.Value, 12);
        Assert.Equal(0.001, QuellEinheiten.Faktor("Pa", "kPa")!.Value, 12);
        // 1 kg = 1000 g
        Assert.Equal(1000, QuellEinheiten.Faktor("kg", "g")!.Value, 12);
        // gleiche Einheit: 1; verschiedene Grössen: nicht umrechenbar
        Assert.Equal(1, QuellEinheiten.Faktor("kg", "kg")!.Value, 12);
        Assert.Null(QuellEinheiten.Faktor("kg", "mm"));
        Assert.Null(QuellEinheiten.Faktor("kPa", "m³/h"));
        Assert.Null(QuellEinheiten.Faktor("Zoll", "mm"));   // Gewindegrösse ist keine Länge
    }

    // ------------------------------------------------------------------ Toleranzgrenzen durch die ganze Prüfung

    /// <summary>12 Spalten, 3 Zeilen (Z1 mit den Prüfwerten, Z2 und Z3 für andere Längen):
    /// 1 Typ, 2 L [mm], 3 Durchmesser [mm], 4 Laenge [m], 5 Kennwert, 6 Genau, 7 Strom [l/s], 8 Druck [kPa], 9 Masse [g], 10 Rp, 11 Anschluss, 12 Aussen.</summary>
    const string Grenzen =
        "Typ;L [mm];Durchmesser [mm];Laenge [m];Kennwert;Genau;Strom [l/s];Druck [kPa];Masse [g];Rp;Anschluss;Aussen\r\n" +
        "Z1;400;400;0,4;250;12,50;10;2,5;300;1 1/4;DN 25;Ø 28,5\r\n" +
        "Z2;100;100;0,1;1;1;1;1;1;1/2;DN 15;Ø 12\r\n" +
        "Z3;1000;1000;1;1;1;1;1;1;1;DN 50;Ø 60\r\n";

    static BefehlsErgebnis Trocken(string name, string datentyp, string? einheit, int spalte, int zeile, JsonNode wert)
    {
        using var o = new Ordner();
        var p = ProjektB4();
        var csv = Ablegen(p, UnterlagenHilfe.Datei(o, "grenzen.csv", Grenzen));
        var par = new JsonObject { ["name"] = name, ["art"] = "typ", ["datentyp"] = datentyp, ["gruppe"] = "abmessung" };
        if (einheit is not null) par["einheit"] = einheit;
        var def = new JsonObject
        {
            ["format"] = 1, ["name"] = "Grenze (fiktiv)", ["kategorie"] = "Heizkoerper",
            ["parameter"] = new JsonArray(par),
            ["typen"] = new JsonArray(new JsonObject
            {
                ["name"] = "A",
                ["werte"] = new JsonObject { [name] = wert },
                ["quellen"] = new JsonObject { [name] = new JsonArray((JsonNode)JsonValue.Create(CsvZelle(csv, zeile, spalte))) },
            }),
            ["koerper"] = new JsonArray(), ["anschluesse"] = new JsonArray(),
        };
        return p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = def }, trockenlauf: true);
    }

    [Theory]
    // Länge max(1 mm; 0.5 %): Quelle 400 → 0.5 % = 2 mm (> 1 mm): 402 auf der Grenze, 402.01 knapp darüber, ebenso nach unten
    [InlineData("l", "laenge", null, 2, 1, 402, true, "±2 mm")]
    [InlineData("l", "laenge", null, 2, 1, 402.01, false, "±2 mm")]
    [InlineData("l", "laenge", null, 2, 1, 398, true, "±2 mm")]
    [InlineData("l", "laenge", null, 2, 1, 397.99, false, "±2 mm")]
    // Quelle 100 → 0.5 % = 0.5 mm, es gilt die Untergrenze 1 mm
    [InlineData("l", "laenge", null, 2, 2, 101, true, "±1 mm")]
    [InlineData("l", "laenge", null, 2, 2, 101.01, false, "±1 mm")]
    // Quelle 1000 → 0.5 % = 5 mm
    [InlineData("l", "laenge", null, 2, 3, 1005, true, "±5 mm")]
    [InlineData("l", "laenge", null, 2, 3, 1005.01, false, "±5 mm")]
    // Durchmesser immer 1 mm, auch bei 400 (die Länge hätte dort 2 mm)
    [InlineData("durchmesser", "laenge", null, 3, 1, 401, true, "Durchmesser ±1 mm")]
    [InlineData("durchmesser", "laenge", null, 3, 1, 401.01, false, "Durchmesser ±1 mm")]
    [InlineData("durchmesser", "laenge", null, 3, 2, 101, true, "Durchmesser ±1 mm")]
    [InlineData("durchmesser", "laenge", null, 3, 2, 101.01, false, "Durchmesser ±1 mm")]
    // Quelle in Metern: 0,4 m = 400 mm → Toleranz 2 mm
    [InlineData("l", "laenge", null, 4, 1, 402, true, "±2 mm")]
    [InlineData("l", "laenge", null, 4, 1, 402.01, false, "±2 mm")]
    // Kennwert ohne Nachkommastelle («250»): ± halbe Einheit = 0.5
    [InlineData("kennwert", "zahl", null, 5, 1, 250.5, true, "= ±0.5")]
    [InlineData("kennwert", "zahl", null, 5, 1, 250.6, false, "= ±0.5")]
    // zwei Nachkommastellen («12,50»): ±0.005
    [InlineData("genau", "zahl", null, 6, 1, 12.505, true, "= ±0.005")]
    [InlineData("genau", "zahl", null, 6, 1, 12.51, false, "= ±0.005")]
    // Volumenstrom 10 l/s = 36 m³/h; halbe Stelle 0.5 l/s = 1.8 m³/h → 34.2 … 37.8
    [InlineData("strom", "zahl", "m3/h", 7, 1, 36, true, "= ±1.8 m³/h")]
    [InlineData("strom", "zahl", "m3/h", 7, 1, 37.8, true, "= ±1.8 m³/h")]
    [InlineData("strom", "zahl", "m3/h", 7, 1, 34.2, true, "= ±1.8 m³/h")]
    [InlineData("strom", "zahl", "m3/h", 7, 1, 37.9, false, "= ±1.8 m³/h")]
    [InlineData("strom", "zahl", "m3/h", 7, 1, 34.1, false, "= ±1.8 m³/h")]
    // Druck 2,5 kPa = 2500 Pa; halbe Stelle 0.05 kPa = 50 Pa
    [InlineData("druck", "zahl", "Pa", 8, 1, 2500, true, "= ±50 Pa")]
    [InlineData("druck", "zahl", "Pa", 8, 1, 2550, true, "= ±50 Pa")]
    [InlineData("druck", "zahl", "Pa", 8, 1, 2551, false, "= ±50 Pa")]
    // Masse 300 g = 0.3 kg; halbe Einheit 0.5 g = 0.0005 kg
    [InlineData("masse", "zahl", "kg", 9, 1, 0.3, true, "= ±0.0005 kg")]
    [InlineData("masse", "zahl", "kg", 9, 1, 0.3005, true, "= ±0.0005 kg")]
    [InlineData("masse", "zahl", "kg", 9, 1, 0.3006, false, "= ±0.0005 kg")]
    // DN genau (auch wenn die Zelle «DN 25» heisst)
    [InlineData("dn", "dn", null, 11, 1, 25, true, "DN genau")]
    [InlineData("dn", "dn", null, 11, 1, 26, false, "DN genau")]
    // Durchmesser mit Vorsatz «Ø 28,5»: 28.5 ± 1
    [InlineData("aussendurchmesser", "laenge", null, 12, 1, 28, true, "Durchmesser ±1 mm")]
    [InlineData("aussendurchmesser", "laenge", null, 12, 1, 29.5, true, "Durchmesser ±1 mm")]
    [InlineData("aussendurchmesser", "laenge", null, 12, 1, 29.6, false, "Durchmesser ±1 mm")]
    // Bruch «1 1/4» = 1.25 gilt für Zahlparameter, vier Stellen gerechnet (± 0.00005)
    [InlineData("zoll", "zahl", null, 10, 1, 1.25, true, "= ±0.00005")]
    [InlineData("zoll", "zahl", null, 10, 1, 1.3, false, "= ±0.00005")]
    public void Toleranzgrenzen_auf_der_Grenze_und_knapp_darueber(string name, string datentyp, string? einheit, int spalte, int zeile, double wert, bool ok, string tolText)
    {
        var e = Trocken(name, datentyp, einheit, spalte, zeile, wert);
        if (ok) Assert.True(e.Angenommen, $"{name} = {wert} gegen Zelle {zeile}/{spalte} hätte passen müssen: {e.Fehler}");
        else
        {
            Assert.False(e.Angenommen, $"{name} = {wert} gegen Zelle {zeile}/{spalte} hätte abgelehnt werden müssen");
            Assert.Contains("Wert weicht von der genannten Quelle ab", e.Fehler);
            Assert.Contains(tolText, e.Fehler);
        }
    }

    [Fact]
    public void Bruch_ist_keine_Laenge_und_Einheit_muss_zur_Groesse_passen()
    {
        // der Bruch «1 1/4» (Gewinde in Zoll) ist für einen Längenparameter keine Zahl
        var e = Trocken("l", "laenge", null, 10, 1, 1.25);
        Assert.False(e.Angenommen);
        Assert.Contains("Wert weicht von der genannten Quelle ab", e.Fehler);
        Assert.Contains("keine vergleichbare Zahl", e.Fehler);
        // «2,5 kPa» lässt sich nicht in m³/h umrechnen
        var d = Trocken("strom", "zahl", "m3/h", 8, 1, 2.5);
        Assert.False(d.Angenommen);
        Assert.Contains("lässt sich nicht in «m³/h» umrechnen", d.Fehler);
    }

    [Theory]
    [InlineData("Rp 1 1/4", true)]    // Textparameter vergleicht den Text: Vorsatz Rp vor «1 1/4»
    [InlineData("Rp 1/4", false)]     // «1/4» ist nicht «1 1/4»
    [InlineData("1 1/4", true)]
    public void Textparameter_vergleicht_Text_mit_Vorsatz(string wert, bool ok)
    {
        var e = Trocken("anschluss", "text", null, 10, 1, wert);
        Assert.Equal(ok, e.Angenommen);
        if (!ok) Assert.Contains("Wert weicht von der genannten Quelle ab", e.Fehler);
    }

    // ------------------------------------------------------------------ Anschlusslage 2 mm (Kontrollbild gegen Port)

    [Fact]
    public void Anschlusslage_2_mm_auf_der_Grenze_und_knapp_darueber()
    {
        using var o = new Ordner();
        var p = ProjektB4();
        Ablegen(p, UnterlagenHilfe.TestIfc(o));   // Quader 100 × 60 × 40, Ports A (−50, 0, 20) und B (50, 0, 20)
        JsonObject Familie(double xb) => FamilieTestHilfe.JsonAus($$"""
            { "format": 1, "name": "Ventil 3D (fiktiv)", "kategorie": "Ventil", "parameter": [],
              "typen": [{ "name": "V-15", "werte": {} }],
              "koerper": [{ "id": "k", "art": "quader", "von": [-50, -30, 0], "bis": [50, 30, 40] }],
              "anschluesse": [
                { "name": "A", "medium": "*", "fluss": "rein", "art": "rohr", "dn": 15, "lage": [-50, 0, 20], "richtung": [-1, 0, 0] },
                { "name": "B", "medium": "*", "fluss": "raus", "art": "rohr", "dn": 15, "lage": [{{xb.ToString(CultureInfo.InvariantCulture)}}, 0, 20], "richtung": [1, 0, 0] }] }
            """);
        var par = new JsonObject { ["familie"] = "FAM-1", ["typ"] = "V-15", ["unterlage"] = "ventil.ifc", ["ansicht"] = "vorne", ["breite"] = 400 };
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Familie(52) }));

        // B liegt 2.000 mm neben dem Port (x = 52 statt 50): genau auf der Toleranz 2 mm → bestanden; A liegt genau auf dem Port
        var r = FamilienKontrollbild.Erzeugen(p, par, out _);
        var b = r["geometrisch"]!["anschluesse"]!.AsArray().Single(a => a!["anschluss"]!.GetValue<string>() == "B")!;
        Assert.Equal(2.0, b["abstand"]!.GetValue<double>(), 6);
        Assert.Equal(2.0, b["toleranz"]!.GetValue<double>(), 9);
        Assert.True(b["bestanden"]!.GetValue<bool>());
        Assert.True(r["geometrisch"]!["anschluesse"]!.AsArray().Single(a => a!["anschluss"]!.GetValue<string>() == "A")!["bestanden"]!.GetValue<bool>());

        // 2.01 mm daneben (x = 52.01): knapp darüber → nicht bestanden
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Familie(52.01), ["familie"] = "FAM-1" }));
        var r2 = FamilienKontrollbild.Erzeugen(p, par, out _);
        var b2 = r2["geometrisch"]!["anschluesse"]!.AsArray().Single(a => a!["anschluss"]!.GetValue<string>() == "B")!;
        Assert.Equal(2.01, b2["abstand"]!.GetValue<double>(), 3);
        Assert.False(b2["bestanden"]!.GetValue<bool>());
    }
}
