using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.AussparungHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3b, B5: Referenzfälle von Hand zu automatischen Aussparungen und BCF-Guid. Die Rechnung steht als Kommentar im
/// Test und folgt der REGEL der Spezifikation (entwurf/modul-3b.md, Grundsatzentscheid 6, B3), nicht dem Code.
///
/// ALLE WERTE FIKTIV: Rohrdurchmesser der Testrohrserie (da = DN + 5; DN 50 → 55 mm), Dämmdicken, Kanalmasse, Spiel 20 mm,
/// Zusammenfassen 100 mm (AussparungHilfe) und die Wanddicke 200 bzw. Deckendicke 250 sind Testwerte – keine Norm-, Hersteller- oder
/// Büroangaben.
///
/// Aufbau (Wandprojekt): W-1 (Süd) Achse y = 0, Dicke 200 → y −100 … +100; Geschoss EG Kote 0 (Höhe 3000), OG Kote 3000;
/// Decke D-1 im OG: z 2750 … 3000. Die Aussparung wird rechtwinklig zum Bauteil ausgeführt (Achse = Bauteilnormale), b entlang der
/// Wand (x), h senkrecht (z).
///
/// Nachtrag B3b (Entscheid Christian 19.10.2026, «Rund (Kernbohrung) nur durch manuelle Auswahl»): aussparungen_vorschlagen schlägt
/// immer RECHTECKIG vor (art aussparung) – umschliessendes Rechteck der Projektion + 2 × Spiel, auch für runde Rohre; rund
/// (art kernbohrung) nur von Hand mit aussparung_form, d = umschliessender Kreis der Projektion + 2 × Spiel (die bisherige Rundformel).
/// Die Grenze Kernbohrung gibt es nicht mehr.
///
/// Nachtrag B3c (Entscheid Christian 19.10.2026, «Aussparungen nur in Beton und Mauerwerk»): die Wände und Decken dieser Fälle haben
/// kein Material und keinen Aufbau → Material «unbekannt» → die Aussparung wird trotzdem vorgeschlagen (Kennzeichen materialUnbekannt,
/// ANNAHME Prüfliste). Alle Masse und Koten unten bleiben gleich: das Material entscheidet nur OB, nicht WIE GROSS. Wäre W-1 aus
/// Gips, Holz oder Stahl, gäbe es keine. Referenzfall «zwei Schichten direkt hintereinander» (Tiefe über beide):
/// AussparungNachtragB3cTests.ZweiSchichtenDirektHintereinander_EineDurchgehendeAussparung_TiefeUeberBeide_Referenz.</summary>
public class Referenz3bAussparungTests
{
    // ------------------------------------------------------------------ 8 rundes Rohr senkrecht

    /// <summary>DN 50 (da 55) senkrecht durch W-1 (Dicke 200), Spiel 20. Querschnitt mit Dämmung D = da + 2·Dämmdicke.
    /// AUTOMATISCH: rechteckig, b = h = D + 2·Spiel (Quadrat), auf ganze cm AUFGERUNDET (Entscheid Christian 19.10.2026, Nachtrag B4e;
    /// vorher 0.1 mm), art aussparung; Koten UK/OK = 1000 ∓ h/2.
    /// VON HAND RUND (aussparung_form rund): d = D + 2·Spiel (Kreis = Querschnitt bei senkrechter Querung), ebenfalls auf ganze cm
    /// aufgerundet, art kernbohrung, Kote Achse 1000.
    /// 0:    D = 55;           55 + 40 = 95       → 100 → automatisch 100 × 100, UK/OK 950 / 1050; von Hand Ø 100   (vorher 95)
    /// 10:   D = 55 + 20 = 75; 75 + 40 = 115      → 120 → automatisch 120 × 120, UK/OK 940 / 1060; von Hand Ø 120   (vorher 115)
    /// 27.5: D = 110;          110 + 40 = 150     → 150 (schon ganze cm), UK/OK 925 / 1075;             von Hand Ø 150
    /// 27.6: D = 110.2;        110.2 + 40 = 150.2 → 160 → automatisch 160 × 160, UK/OK 920 / 1080; von Hand Ø 160   (vorher 150.2)
    /// Es gibt keine Grenze mehr: automatisch immer aussparung, von Hand immer kernbohrung.</summary>
    [Theory]
    [InlineData(0.0, 100.0)]
    [InlineData(10.0, 120.0)]
    [InlineData(27.5, 150.0)]
    [InlineData(27.6, 160.0)]
    public void F08_RundesRohrSenkrecht_AutomatischQuadrat_VonHandKreis(double daemmung, double erwartet)
    {
        var p = Wandprojekt();
        RohrDurchW1(p, "RO-1", 3000, daemmung: daemmung);
        Ok(Vorschlagen(p));
        var a = Assert.Single(Aussparungen(p));
        Assert.Equal(("rechteckig", "aussparung"), (a.Text("form"), a.Text("art")));
        Etwa(erwartet, a.Zahl("b"), 1e-6, "b"); Etwa(erwartet, a.Zahl("h"), 1e-6, "h");
        Assert.Null(a.Zahl("d"));
        Etwa(1000 - erwartet / 2, a.Zahl("kote_uk"), 1e-6, "UK"); Etwa(1000 + erwartet / 2, a.Zahl("kote_ok"), 1e-6, "OK");
        Etwa(200, a.Zahl("tiefe"), 1e-6, "Tiefe = Wanddicke");
        Assert.Equal([3000, 0, 1000], Mitte(a));

        Ok(Form(p, "rund", a.Kennzeichen));
        var r = Assert.Single(Aussparungen(p));
        Assert.Equal(("rund", "kernbohrung"), (r.Text("form"), r.Text("art")));
        Etwa(erwartet, r.Zahl("d"), 1e-6, "d von Hand");
        Assert.Null(r.Zahl("b")); Assert.Null(r.Zahl("h")); Assert.Null(r.Zahl("kote_ok"));
        Etwa(1000, r.Zahl("kote_uk"), 1e-6, "Kote Achse");
        Etwa(200, r.Zahl("tiefe"), 1e-6, "Tiefe bleibt");
        Assert.Equal([3000, 0, 1000], Mitte(r));
    }

    // ------------------------------------------------------------------ 9 schräge Querung

    /// <summary>DN 50 unter dem Winkel θ gegen die Wandnormale (Normale = y) horizontal durch W-1 (Dicke t = 200), Mitte (3000, 0, 1000).
    /// Projektion des Querschnitts über die ganze Tiefe: in Neigungsrichtung (waagrecht, x = b) Länge L = D/cos θ + t·tan θ, quer dazu
    /// (senkrecht, z = h) D.
    /// AUTOMATISCH rechteckig: b = L + 2·Spiel, h = D + 2·Spiel, je auf ganze cm aufgerundet (Nachtrag B4e); Koten UK/OK = 1000 ∓ h/2.
    /// VON HAND RUND: umschliessender Kreis Ø = L (L ≥ D) + 2·Spiel, auf ganze cm aufgerundet.
    /// 30°, D = 55:  55/0.866025 = 63.5085; 200·0.577350 = 115.4701; L = 178.9786; + 40 = 218.9786 → auf ganze cm 220 (vorher 219.0)
    ///               h = 55 + 40 = 95 → 100 → automatisch 220 × 100, UK/OK 950 / 1050; von Hand Ø 220
    /// 45°, D = 55:  55/0.707107 = 77.7817; 200·1 = 200;             L = 277.7817; + 40 = 317.7817 → 320 (vorher 317.8)
    ///               h = 100 → automatisch 320 × 100, UK/OK 950 / 1050; von Hand Ø 320
    /// 30°, Dämmung 10, D = 55 + 20 = 75:  75/0.866025 = 86.6025; + 115.4701 = 202.0726; + 40 = 242.0726 → 250 (vorher 242.1)
    ///               h = 75 + 40 = 115 → 120 → automatisch 250 × 120, UK/OK 940 / 1060; von Hand Ø 250
    /// Tiefe 200 (Bauteildicke, nicht Weglänge der Achse; nicht auf cm gerundet).</summary>
    [Theory]
    [InlineData(30.0, 0.0, 220.0, 100.0)]
    [InlineData(45.0, 0.0, 320.0, 100.0)]
    [InlineData(30.0, 10.0, 250.0, 120.0)]
    public void F09_SchraegeQuerung_AutomatischRechteck_VonHandKreisDerProjektion(double grad, double daemmung, double erwartetL, double erwartetH)
    {
        var p = Wandprojekt();
        double w = grad * Math.PI / 180, s = Math.Sin(w), c = Math.Cos(w);
        Ok(Modul3Hilfe.Rohr(p, "RO-1", "DN 50", [3000 - 500 * s, -500 * c, 1000], [3000 + 500 * s, 500 * c, 1000]));
        if (daemmung > 0)
            Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "RO-1", ["eigenschaften"] = new JsonObject { ["daemmdicke"] = daemmung } }));
        Ok(Vorschlagen(p));
        var a = Assert.Single(Aussparungen(p));
        Assert.Equal(("rechteckig", "aussparung"), (a.Text("form"), a.Text("art")));
        Etwa(erwartetL, a.Zahl("b"), 1e-6, "b in Neigungsrichtung");
        Etwa(erwartetH, a.Zahl("h"), 1e-6, "h quer");
        Etwa(1000 - erwartetH / 2, a.Zahl("kote_uk"), 1e-6, "UK"); Etwa(1000 + erwartetH / 2, a.Zahl("kote_ok"), 1e-6, "OK");
        Etwa(200, a.Zahl("tiefe"), 1e-6, "Tiefe");
        Etwa(3000, Mitte(a)[0], 1e-6, "Mitte x"); Etwa(0, Mitte(a)[1], 1e-6, "Mitte y"); Etwa(1000, Mitte(a)[2], 1e-6, "Mitte z");

        Ok(Form(p, "rund", a.Kennzeichen));
        var r = Assert.Single(Aussparungen(p));
        Assert.Equal(("rund", "kernbohrung"), (r.Text("form"), r.Text("art")));
        Etwa(erwartetL, r.Zahl("d"), 1e-6, "d von Hand = umschliessender Kreis");
        Etwa(200, r.Zahl("tiefe"), 1e-6, "Tiefe");
        // zurück zum Rechteck: wieder die automatischen Masse
        Ok(Form(p, "rechteckig", a.Kennzeichen));
        var z = Assert.Single(Aussparungen(p));
        Etwa(erwartetL, z.Zahl("b"), 1e-6, "b"); Etwa(erwartetH, z.Zahl("h"), 1e-6, "h");
    }

    // ------------------------------------------------------------------ 10 Rechteckkanal senkrecht

    /// <summary>Rechteckkanal 500 × 300 (Breite waagrecht, Höhe senkrecht) quert W-1 senkrecht, Achse z = 2000.
    /// b = 500 + 2·20 = 540; h = 300 + 2·20 = 340; rechteckig → Art aussparung. Koten UK/OK = 2000 ∓ 340/2 = 1830 / 2170.
    /// Mit Dämmung 15 (je Seite): b = 500 + 30 + 40 = 570; h = 300 + 30 + 40 = 370; UK/OK = 2000 ∓ 185 = 1815 / 2185.</summary>
    [Theory]
    [InlineData(0.0, 540.0, 340.0, 1830.0, 2170.0)]
    [InlineData(15.0, 570.0, 370.0, 1815.0, 2185.0)]
    public void F10_RechteckkanalSenkrecht_BreiteHoehePlusSpiel(double daemmung, double b, double h, double uk, double ok)
    {
        var p = Wandprojekt();
        Kanal(p, "KA-1", 500, 300, [2000, -800, 2000], [2000, 800, 2000]);
        if (daemmung > 0)
            Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "KA-1", ["eigenschaften"] = new JsonObject { ["daemmdicke"] = daemmung } }));
        Ok(Vorschlagen(p));
        var a = Assert.Single(Aussparungen(p));
        Assert.Equal("rechteckig", a.Text("form"));
        Etwa(b, a.Zahl("b"), 1e-6, "b"); Etwa(h, a.Zahl("h"), 1e-6, "h");
        Assert.Null(a.Zahl("d"));
        Assert.Equal("aussparung", a.Text("art"));
        Etwa(uk, a.Zahl("kote_uk"), 1e-6, "UK"); Etwa(ok, a.Zahl("kote_ok"), 1e-6, "OK");
        Etwa(200, a.Zahl("tiefe"), 1e-6, "Tiefe");
    }

    // ------------------------------------------------------------------ 11 Zusammenfassen

    /// <summary>Zwei DN 50 nebeneinander in W-1 (z 1000), Bedarf je 95 × 95 (55 + 40, B3b rechteckig). Randabstand = Achsabstand − 95
    /// (zusammengefasst wird mit dem gerechneten Bedarf, gerundet erst das Ergebnis).
    /// Achsabstand 194 (x 2000 und 2194): Randabstand 99 &lt; 100 → eine Sammelaussparung. Umschliessendes Rechteck:
    /// b = 194 + 95 = 289 → auf ganze cm 290 (Nachtrag B4e; vorher 289), h = 95 → 100 (vorher 95); Mitte bleibt x = 2097, z = 1000;
    /// rechteckig, aussparung, Koten UK/OK = 1000 ∓ 50 = 950 / 1050 (vorher 952.5 / 1047.5), Tiefe 200, beide Leitungen als Bezug.</summary>
    [Fact]
    public void F11a_RandabstandKnappUnterGrenze_EineSammelaussparung()
    {
        var p = Wandprojekt();
        RohrDurchW1(p, "RO-1", 2000);
        RohrDurchW1(p, "RO-2", 2194);
        Ok(Vorschlagen(p));
        var a = Assert.Single(Aussparungen(p));
        Assert.Equal("rechteckig", a.Text("form"));
        Etwa(290, a.Zahl("b"), 1e-6, "b"); Etwa(100, a.Zahl("h"), 1e-6, "h");
        Assert.Equal("aussparung", a.Text("art"));
        Assert.Equal([2097, 0, 1000], Mitte(a));
        Etwa(950, a.Zahl("kote_uk"), 1e-6, "UK"); Etwa(1050, a.Zahl("kote_ok"), 1e-6, "OK");
        Etwa(200, a.Zahl("tiefe"), 1e-6, "Tiefe");
        Assert.Equal(["RO-1", "RO-2"], LeitungKz(p, a));
    }

    /// <summary>Achsabstand 196 (x 2000 und 2196): Randabstand 101 ≥ 100 → zwei Einzelaussparungen, je 95 × 95 → auf ganze cm 100 × 100
    /// (Nachtrag B4e) an der Leitung.</summary>
    [Fact]
    public void F11b_RandabstandKnappUeberGrenze_ZweiEinzelne()
    {
        var p = Wandprojekt();
        RohrDurchW1(p, "RO-1", 2000);
        RohrDurchW1(p, "RO-2", 2196);
        Ok(Vorschlagen(p));
        Assert.Equal(2, Aussparungen(p).Count);
        foreach (var kz in new[] { "RO-1", "RO-2" })
        {
            var a = VonLeitung(p, kz);
            Assert.Equal("rechteckig", a.Text("form"));
            Etwa(100, a.Zahl("b"), 1e-6, kz); Etwa(100, a.Zahl("h"), 1e-6, kz);
        }
        Etwa(2000, Mitte(VonLeitung(p, "RO-1"))[0], 1e-6, "x RO-1"); Etwa(2196, Mitte(VonLeitung(p, "RO-2"))[0], 1e-6, "x RO-2");
    }

    /// <summary>Senkrecht übereinander statt nebeneinander (x 2000, z 1000 und 1194): Randabstand 194 − 95 = 99 &lt; 100 → Sammel.
    /// Rechteck b = 95 → 100, h = 289 → 290 (ganze cm, Nachtrag B4e); Mitte z = (1000 + 1194)/2 = 1097; UK/OK = 1097 ∓ 145 = 952 / 1242
    /// (vorher 95 × 289, 952.5 / 1241.5).</summary>
    [Fact]
    public void F11c_UebereinanderKnappUnterGrenze_SammelaussparungHochformat()
    {
        var p = Wandprojekt();
        RohrDurchW1(p, "RO-1", 2000, 1000);
        RohrDurchW1(p, "RO-2", 2000, 1194);
        Ok(Vorschlagen(p));
        var a = Assert.Single(Aussparungen(p));
        Assert.Equal("rechteckig", a.Text("form"));
        Etwa(100, a.Zahl("b"), 1e-6, "b"); Etwa(290, a.Zahl("h"), 1e-6, "h");
        Assert.Equal([2000, 0, 1097], Mitte(a));
        Etwa(952, a.Zahl("kote_uk"), 1e-6, "UK"); Etwa(1242, a.Zahl("kote_ok"), 1e-6, "OK");
    }

    // ------------------------------------------------------------------ 12 Koten

    /// <summary>Koten über ±0.00 (Projekt-z) und über OK Geschoss (z − Kote des Geschosses). EG Kote 0, OG Kote 3000.
    /// EG, W-1: Rohr DN 50 bei z 1500 → automatisch 95 × 95 → ganze cm 100 × 100 (B3b, B4e), UK/OK = 1500 ∓ 50 = 1450 / 1550 (vorher
    ///          1452.5 / 1547.5); über OKG(EG) gleich.
    ///          Kanal 400 × 200 bei z 2000 → 440 × 240, UK/OK = 2000 ∓ 120 = 1880 / 2120; über OKG 1880 / 2120.
    /// OG, Wand W-5 (z 3000 … 6000, Achse y = 6000): Rohr DN 50 bei z 4000, von Hand rund → Ø 95 → 100, Achse 4000; über OKG(OG)
    ///          4000 − 3000 = 1000.
    ///          Kanal 400 × 200 bei z 4500 → UK/OK = 4500 ∓ 120 = 4380 / 4620; über OKG 4380 − 3000 = 1380 / 4620 − 3000 = 1620.</summary>
    [Fact]
    public void F12_Koten_UeberNullUndUeberOkGeschoss_EgUndOg()
    {
        var p = Wandprojekt();
        Ok(p.Ausfuehren("wand_zeichnen", new JsonObject { ["geschoss"] = "OG", ["punkte"] = Pts([0, 6000], [6000, 6000]), ["dicke"] = 200 }));
        RohrDurchW1(p, "RO-1", 1000, 1500);
        Kanal(p, "KA-1", 400, 200, [3000, -800, 2000], [3000, 800, 2000]);
        Ok(Modul3Hilfe.Rohr(p, "RO-2", "DN 50", [1000, 5500, 4000], [1000, 6500, 4000]));
        Kanal(p, "KA-2", 400, 200, [3000, 5500, 4500], [3000, 6500, 4500]);
        Ok(Vorschlagen(p));
        RundVonHand(p, "RO-2");
        Assert.Equal(4, Aussparungen(p).Count);
        var l = (JsonObject)Tuer.Abfrage(p, "aussparungsliste", new JsonObject());
        JsonObject Zeile(string leitung)
        {
            var a = VonLeitung(p, leitung);
            return l["zeilen"]!.AsArray().Select(z => z!.AsObject()).Single(z => z["aussparung"]!.GetValue<string>() == a.Kennzeichen);
        }
        double D(JsonObject z, string n) => z[n]!.GetValue<double>();
        var ro1 = Zeile("RO-1");
        Assert.Equal(("EG", "rechteckig"), (ro1["geschoss"]!.GetValue<string>(), ro1["form"]!.GetValue<string>()));
        Assert.Equal((1450.0, 1550.0, 1450.0, 1550.0), (D(ro1, "koteUk"), D(ro1, "koteOk"), D(ro1, "koteUkGeschoss"), D(ro1, "koteOkGeschoss")));
        Assert.Null(ro1["koteAchse"]);
        var ka1 = Zeile("KA-1");
        Assert.Equal("EG", ka1["geschoss"]!.GetValue<string>());
        Assert.Equal((1880.0, 2120.0, 1880.0, 2120.0), (D(ka1, "koteUk"), D(ka1, "koteOk"), D(ka1, "koteUkGeschoss"), D(ka1, "koteOkGeschoss")));
        var ro2 = Zeile("RO-2");
        Assert.Equal(("OG", "rund"), (ro2["geschoss"]!.GetValue<string>(), ro2["form"]!.GetValue<string>()));
        Assert.Equal((4000.0, 1000.0), (D(ro2, "koteAchse"), D(ro2, "koteAchseGeschoss")));
        var ka2 = Zeile("KA-2");
        Assert.Equal("OG", ka2["geschoss"]!.GetValue<string>());
        Assert.Equal((4380.0, 4620.0, 1380.0, 1620.0), (D(ka2, "koteUk"), D(ka2, "koteOk"), D(ka2, "koteUkGeschoss"), D(ka2, "koteOkGeschoss")));
    }

    // ------------------------------------------------------------------ 13 Nummern

    static void Aus(Projekt p, string kz, string geschoss, double x, double y, double z) =>
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Aussparung", ["kennzeichen"] = kz, ["geschoss"] = geschoss,
            ["eigenschaften"] = new JsonObject { ["form"] = "rund", ["d"] = 95, ["tiefe"] = 200, ["achse"] = new JsonArray(0, 1, 0) },
            ["bezuege"] = new JsonObject { ["bauteil"] = "W-1" },
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([x, y, z]) },
        }));

    /// <summary>Fünf Aussparungen im EG, angelegt in der Reihenfolge der Kennzeichen, die NICHT der Ordnung entspricht.
    /// Regel: Reihenfolge Mitte x, dann y, dann z; Format «A-&lt;Geschoss&gt;-&lt;001&gt;».
    ///   AUS-1 (4000, 0, 1000)   AUS-2 (1000, 0, 1000)   AUS-3 (2500, 300, 1000)   AUS-4 (2500, 100, 1000)   AUS-5 (2500, 100, 500)
    /// Sortiert: x 1000 → AUS-2 (001); x 2500: y 100 → z 500 → AUS-5 (002), z 1000 → AUS-4 (003); y 300 → AUS-3 (004); x 4000 → AUS-1 (005).
    /// Eine Aussparung im OG zählt für sich: A-OG-001.</summary>
    [Fact]
    public void F13_Nummern_ReihenfolgeXYZ_FormatJeGeschoss()
    {
        var p = Wandprojekt();
        Aus(p, "AUS-1", "EG", 4000, 0, 1000);
        Aus(p, "AUS-2", "EG", 1000, 0, 1000);
        Aus(p, "AUS-3", "EG", 2500, 300, 1000);
        Aus(p, "AUS-4", "EG", 2500, 100, 1000);
        Aus(p, "AUS-5", "EG", 2500, 100, 500);
        Aus(p, "AUS-6", "OG", 5000, 0, 3500);
        Ok(p.Ausfuehren("aussparungen_nummerieren"));
        Assert.Equal("A-EG-005", Hole(p, "AUS-1").Text("nummer"));
        Assert.Equal("A-EG-001", Hole(p, "AUS-2").Text("nummer"));
        Assert.Equal("A-EG-004", Hole(p, "AUS-3").Text("nummer"));
        Assert.Equal("A-EG-003", Hole(p, "AUS-4").Text("nummer"));
        Assert.Equal("A-EG-002", Hole(p, "AUS-5").Text("nummer"));
        Assert.Equal("A-OG-001", Hole(p, "AUS-6").Text("nummer"));
        // ein zweiter Lauf ändert nichts
        var z = Ok(p.Ausfuehren("aussparungen_nummerieren"));
        Assert.Empty(z.Aenderungen);
    }

    // ------------------------------------------------------------------ 14 Decke gehört zum Geschoss darunter

    /// <summary>Decke D-1 ohne Geschossangabe (Oberkante 3000, Dicke 250, z 2750 … 3000); Geschoss EG 0 … 3000, OG ab 3000.
    /// REGEL (Hauptlauf 19.10.2026 nach SIA 400:2000 B.10.3 und SIA 410/2 Ziff. 2.3: «Deckenaussparungen werden im unterhalb der
    /// entsprechenden Decke liegenden Geschoss eingetragen»): Decke/Dach → Geschoss, in dessen Höhenband die Unterkante − 1 mm liegt
    /// (Band kote − 1 ≤ z &lt; kote + hoehe). Rohr DN 50 senkrecht bei (3000, 2000) → Aussparung 95 × 95 → ganze cm 100 × 100 (B3b rechteckig, B4e), Mitte
    /// z = 2875, Unterkante 2750 → z = 2749: EG (−1 ≤ 2749 &lt; 0 + 3000), nicht OG (2749 &lt; 3000 − 1). Die Decke selbst bleibt im OG.
    /// Nummern im EG nach x: RO-2 in W-1 (x 1000) A-EG-001, RO-1 in D-1 (x 3000) A-EG-002; im OG keine.</summary>
    [Fact]
    public void F14_AussparungInDecke_GehoertZumGeschossDarunter()
    {
        var p = Wandprojekt();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Decke", ["kennzeichen"] = "D-1",
            ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = Pts([0, 0, 3000], [6000, 0, 3000], [6000, 4000, 3000], [0, 4000, 3000]), ["dicke"] = 250 },
        }));
        Assert.Equal(Hole(p, "OG").Id, Hole(p, "D-1").Geschoss);
        Ok(Modul3Hilfe.Rohr(p, "RO-1", "DN 50", [3000, 2000, 2400], [3000, 2000, 3600]));
        RohrDurchW1(p, "RO-2", 1000);
        Ok(Vorschlagen(p));
        var a = VonLeitung(p, "RO-1");
        Assert.Equal(Hole(p, "D-1").Id, a.Bezuege["bauteil"]);
        Assert.Equal(Hole(p, "EG").Id, a.Geschoss);                 // Geschoss unter der Decke
        Etwa(100, a.Zahl("b"), 1e-6, "b"); Etwa(100, a.Zahl("h"), 1e-6, "h");
        Assert.Equal([3000, 2000, 2875], Mitte(a));
        Etwa(250, a.Zahl("tiefe"), 1e-6, "Tiefe");
        Etwa(2750, a.Zahl("kote_uk"), 1e-6, "UK = 2875 − 250/2");
        Assert.Equal(Hole(p, "EG").Id, VonLeitung(p, "RO-2").Geschoss);
        Ok(p.Ausfuehren("aussparungen_nummerieren"));
        Assert.Equal("A-EG-002", VonLeitung(p, "RO-1").Text("nummer"));
        Assert.Equal("A-EG-001", VonLeitung(p, "RO-2").Text("nummer"));
        Assert.DoesNotContain(Aussparungen(p), x => x.Text("nummer")!.StartsWith("A-OG-", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------ 15 BCF: Topic-Guid UUID v5

    /// <summary>UUID v5 (RFC 4122 Abschnitt 4.3): SHA-1 über Namensraum-Bytes (Netzwerk-Bytefolge) und Namen in UTF-8, Version 5,
    /// Variante 10. Die festen Zeichenketten unten wurden UNABHÄNGIG vom Code mit einem PowerShell-Skript (System.Security.
    /// Cryptography.SHA1, eigene Bitoperationen) berechnet; das Skript liefert für den bekannten Wert der Python-Dokumentation
    /// (Namensraum DNS, «python.org») 886313e1-3b8a-5372-9b90-0c9aee199e5d – wie die Referenz.
    ///   URL-Namensraum 6ba7b811-9dad-11d1-80b4-00c04fd430c8 (RFC 4122 Anhang C)
    ///   KICAD-Namensraum = v5(URL, "https://hkplanung.ch/kicad/bcf") = 88027053-ba3b-5878-8c32-e0eb40fc5dd0
    ///   Topic = v5(KICAD, "projekt-0001/kollision/ELEM-A|ELEM-B@3000,0,1000") = 1aa6c016-7810-55dd-b860-faf5fe03754b
    ///   Viewpoint = v5(KICAD, "&lt;Topic&gt;/viewpoint") = 471a2f1a-dbc5-5b94-bc3a-118c313d7474
    ///   Kommentar = v5(KICAD, "&lt;Topic&gt;/kommentar") = 1ab7ac5e-7595-5810-ab9e-f557faa62832
    ///   Topic bei Lage 3100 statt 3000 = 98d2e92e-60f7-5938-9053-3444b1f37f53 (andere Kennung → anderes Topic)
    /// Die Namensbildung «&lt;Projekt-Id&gt;/kollision/&lt;kennung&gt;» steht in der Spezifikation (B6: Topic-Guid = UUID v5 aus Projekt-Id
    /// und Kollisionskennung); die genaue Zeichenfolge und der Namensraum sind die ANNAHME aus BcfFormat.cs.</summary>
    [Fact]
    public void F15_TopicGuid_UuidV5_GegenUnabhaengigBerechneteWerte()
    {
        Assert.Equal("88027053-ba3b-5878-8c32-e0eb40fc5dd0", BcfFormat.Text(BcfFormat.Namensraum));
        var p = Rechteck();
        var w1 = Hole(p, "W-1").Id;
        BcfHilfe.Kollision(p, "KOL-1", "hart", "neu", "ELEM-A|ELEM-B@3000,0,1000", new JsonArray(w1, "UL-X:abc"), 25, [3000, 0, 1000]);
        BcfHilfe.Kollision(p, "KOL-2", "hart", "neu", "ELEM-A|ELEM-B@3100,0,1000", new JsonArray(w1, "UL-X:abc"), 25, [3100, 0, 1000]);
        var topic = BcfFormat.TopicGuid("projekt-0001", Hole(p, "KOL-1"));
        Assert.Equal("1aa6c016-7810-55dd-b860-faf5fe03754b", BcfFormat.Text(topic));
        Assert.Equal("471a2f1a-dbc5-5b94-bc3a-118c313d7474", BcfFormat.Text(BcfFormat.ViewpointGuid(topic)));
        Assert.Equal("1ab7ac5e-7595-5810-ab9e-f557faa62832", BcfFormat.Text(BcfFormat.KommentarGuid(topic)));
        Assert.Equal("98d2e92e-60f7-5938-9053-3444b1f37f53", BcfFormat.Text(BcfFormat.TopicGuid("projekt-0001", Hole(p, "KOL-2"))));
    }
}
