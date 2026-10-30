using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.AussparungHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3b, B3: aussparungen_vorschlagen – Grösse, Form, Art, Tiefe, Koten, Bezüge, Zusammenfassen, Vorgaben, Trockenlauf
/// und Rückgängig. Handrechnungen mit den FIKTIVEN Vorgaben (Spiel 20, Zusammenfassen 100 mm) und der FIKTIVEN Testrohrserie
/// (DN 50: da = 55 mm). Nachtrag B3b (Entscheid Christian 19.10.2026): der Vorschlag ist immer rechteckig, art aussparung – auch für
/// runde Rohre (umschliessendes Rechteck der Projektion + 2 × Spiel); Kernbohrungen nur von Hand (AussparungFormTests).</summary>
public class AussparungVorschlagTests
{
    /// <summary>DN 50 (da 55) senkrecht durch W-1: b = h = 55 + 2 × 20 = 95 → auf ganze cm aufgerundet 100 (Entscheid Christian 19.10.2026,
    /// Nachtrag B4e; vorher 95); Koten UK/OK = 1000 ∓ 50 = 950 / 1050.</summary>
    [Fact]
    public void RohrDN50_SenkrechtDurchWand200_Rechteckig100x100_Tiefe200_Aussparung()
    {
        var p = Wandprojekt();
        RohrDurchW1(p, "RO-1", 3000);
        var e = Ok(Vorschlagen(p));
        var a = Assert.Single(Aussparungen(p));
        Assert.Equal("AUS-1", a.Kennzeichen);
        Assert.Equal("rechteckig", a.Text("form"));                         // B3b: auch für runde Rohre
        Etwa(100, a.Zahl("b"), was: "b = da + 2 × Spiel = 95 → ganze cm");
        Etwa(100, a.Zahl("h"), was: "h = da + 2 × Spiel = 95 → ganze cm");
        Assert.Null(a.Zahl("d"));
        Etwa(200, a.Zahl("tiefe"), was: "Tiefe = Wanddicke");
        Assert.Equal("aussparung", a.Text("art"));
        Assert.Equal("vorgeschlagen", a.Text("status"));
        Etwa(1000 - 50, a.Zahl("kote_uk"), was: "UK = Achse − 100/2");
        Etwa(1000 + 50, a.Zahl("kote_ok"), was: "OK = Achse + 100/2");
        Assert.Equal([3000, 0, 1000], Mitte(a));
        Assert.Equal("[0,1,0]", a.Eigenschaft("achse")!.Inhalt!.ToJsonString());   // Wandnormale, kanonisch +y
        Assert.Equal(Hole(p, "EG").Id, a.Geschoss);
        Assert.Equal(Hole(p, "W-1").Id, a.Bezuege["bauteil"]);
        Assert.Equal(["RO-1"], LeitungKz(p, a));
        Assert.Equal(Hole(p, "RO-1").Id, a.Bezuege["leitung1"]);
        Assert.Null(a.Kennungen);
        // Herkunft: gerechnet, mit der Quelle der Vorgabe; Form und Art nach der Regel (nicht von Hand)
        Assert.Equal("berechnung", a.Eigenschaft("b")!.Herkunft.Art);
        Assert.Contains(Quelle, a.Eigenschaft("b")!.Herkunft.Quelle);
        Assert.Contains("umschliessendes Rechteck der Projektion", a.Eigenschaft("b")!.Herkunft.Quelle);
        Assert.Equal("berechnung", a.Eigenschaft("form")!.Herkunft.Art);
        Assert.Contains("Kernbohrungen nur von Hand", a.Eigenschaft("art")!.Herkunft.Quelle);
        Assert.False(AussparungForm.VonHand(a));
        Assert.Contains(e.Hinweise, h => h.StartsWith("Aussparungen: 1 neu"));
        Assert.Contains(e.Hinweise, h => h.Contains("AUS-1 neu: rechteckig 100 × 100 mm, aussparung, Tiefe 200 mm, in W-1, Leitungen RO-1"));
        Assert.DoesNotContain(e.Hinweise, h => h.Contains("grenze_kernbohrung"));   // ohne die alte Vorgabe kein Hinweis
        // zweiter Lauf: nichts zu tun
        var z = Ok(Vorschlagen(p));
        Assert.Empty(z.Aenderungen);
        Assert.Contains(z.Hinweise, h => h.StartsWith("Aussparungen: 0 neu, 0 nachgeführt, 1 unverändert"));
    }

    [Fact]
    public void Schraeg45Grad_UmschliessendesRechteck_LaengsDerNeigung()
    {
        var p = Wandprojekt();
        Ok(Modul3Hilfe.Rohr(p, "RO-1", "DN 50", [2500, -500, 1000], [3500, 500, 1000]));   // Richtung (1, 1, 0)/√2
        Ok(Vorschlagen(p));
        var a = Assert.Single(Aussparungen(p));
        // Ellipse in der Wand: 2a = 55/cos 45° = 55·√2 = 77.78 längs der Neigung (x); über die Wanddicke um 200 · tan 45° = 200 in x
        // versetzt → 277.78; + 2 × Spiel = 317.78 → auf ganze cm aufgerundet 320 (b, waagrecht; Nachtrag B4e, vorher 317.8).
        // Quer (z): 55 + 40 = 95 → 100 (h).
        double b = 55 * Math.Sqrt(2) + 200 + 2 * Spiel;
        Assert.Equal("rechteckig", a.Text("form"));
        Etwa(Math.Ceiling(b / 10) * 10, a.Zahl("b"), was: "b = D/cos θ + t · tan θ + 2s");
        Etwa(320, a.Zahl("b"));
        Etwa(100, a.Zahl("h"), was: "h = D + 2s");
        Assert.Null(a.Zahl("d"));
        Etwa(200, a.Zahl("tiefe"), 1e-6, "Tiefe = Bauteildicke, nicht Länge der Achse in der Wand (282.8)");
        Assert.Equal("aussparung", a.Text("art"));
        Assert.Equal([3000, 0, 1000], Mitte(a));
        Assert.Equal("[0,1,0]", a.Eigenschaft("achse")!.Inhalt!.ToJsonString());   // rechtwinklig zur Wand ausgeführt
    }

    [Fact]
    public void MitDaemmung30_PlusSechzig_ImmerAussparung()
    {
        var p = Wandprojekt();
        RohrDurchW1(p, "RO-1", 1000);
        RohrDurchW1(p, "RO-2", 4000, daemmung: 30);
        Ok(Vorschlagen(p));
        var ohne = VonLeitung(p, "RO-1"); var mit = VonLeitung(p, "RO-2");
        Etwa(100, ohne.Zahl("b")); Etwa(100, ohne.Zahl("h"));                       // 95 → ganze cm
        Etwa(160, mit.Zahl("b"), was: "55 + 2 × 30 + 2 × 20 = 155 → ganze cm"); Etwa(160, mit.Zahl("h"));
        Etwa(60, mit.Zahl("b") - ohne.Zahl("b"));
        Assert.All(new[] { ohne, mit }, a => Assert.Equal(("rechteckig", "aussparung"), (a.Text("form"), a.Text("art"))));
    }

    /// <summary>Nachtrag B3b: grenze_kernbohrung ist nicht mehr nötig; ist sie gesetzt (altes Projekt), bleibt der Vorschlag
    /// rechteckig und es gibt einen Hinweis.</summary>
    [Fact]
    public void GrenzeKernbohrung_Gesetzt_WirdNichtVerwendet_Hinweis()
    {
        var p = Wandprojekt(vorgaben: false);
        Vorgaben(p, grenze: 95);   // FIKTIV: früher genau d → Kernbohrung
        RohrDurchW1(p, "RO-1", 3000);
        var e = Ok(Vorschlagen(p));
        var a = Assert.Single(Aussparungen(p));
        Assert.Equal(("rechteckig", "aussparung"), (a.Text("form"), a.Text("art")));
        Assert.Contains(e.Hinweise, h => h.StartsWith("grenze_kernbohrung (VG-1) ist gesetzt, wird aber nicht mehr verwendet (Entscheid Christian 19.10.2026)"));
        // Trockenlauf nennt es ebenso
        Assert.Contains(Ok(Vorschlagen(p, trocken: true)).Hinweise, h => h.Contains("grenze_kernbohrung (VG-1) ist gesetzt"));
    }

    /// <summary>Altes Projekt (vor B3b): ein automatisch runder Vorschlag (Herkunft berechnung) wird beim nächsten Lauf rechteckig –
    /// Kennzeichen bleibt; eine freigegebene runde bleibt unverändert.</summary>
    [Fact]
    public void AltesProjekt_AutomatischRund_WirdRechteckig_FreigegebeneBleibt()
    {
        var p = Wandprojekt();
        RohrDurchW1(p, "RO-1", 1000);
        RohrDurchW1(p, "RO-2", 4000);
        Ok(Vorschlagen(p));
        // Stand vor B3b nachstellen: beide automatisch rund Ø 95, kernbohrung (Herkunft berechnung wie der alte Vorschlag)
        foreach (var kz in new[] { "RO-1", "RO-2" })
        {
            var e = VonLeitung(p, kz).Kopie();
            var h = e.Eigenschaft("b")!.Herkunft;
            e.Eigenschaften["form"] = new Wert { Inhalt = JsonValue.Create("rund"), Herkunft = h };
            e.Eigenschaften["art"] = new Wert { Inhalt = JsonValue.Create("kernbohrung"), Herkunft = h };
            e.Eigenschaften["d"] = new Wert { Inhalt = JsonValue.Create(95.0), Einheit = "mm", Herkunft = h };
            e.Eigenschaften.Remove("b"); e.Eigenschaften.Remove("h"); e.Eigenschaften.Remove("kote_ok");
            e.Eigenschaften["kote_uk"] = new Wert { Inhalt = JsonValue.Create(1000.0), Einheit = "mm", Herkunft = h };
            Assert.True(p.Modell.Anwenden([new Operation { Art = "element.setzen", Id = e.Id, Basis = p.Modell.Finde(e.Id)!.Version, Element = e }]).Count == 0);
        }
        var fest = VonLeitung(p, "RO-2").Kennzeichen;
        Ok(Status(p, "freigegeben", fest));
        Assert.False(AussparungForm.KernbohrungVonHand(VonLeitung(p, "RO-1")));   // automatisch rund ist keine Handwahl
        var vorher = VonLeitung(p, "RO-1").Kennzeichen;
        var z = Ok(Vorschlagen(p));
        var a = VonLeitung(p, "RO-1");
        Assert.Equal(vorher, a.Kennzeichen);
        Assert.Equal(("rechteckig", "aussparung"), (a.Text("form"), a.Text("art")));
        Etwa(100, a.Zahl("b")); Etwa(100, a.Zahl("h")); Assert.Null(a.Zahl("d"));   // 95 → ganze cm (B4e)
        Assert.Contains(z.Hinweise, h => h.Contains($"{vorher} nachgeführt: rechteckig 100 × 100 mm"));
        var f = Hole(p, fest);
        Assert.Equal(("rund", "freigegeben"), (f.Text("form"), f.Text("status")));   // feste bleiben, wie sie sind
        Etwa(95, f.Zahl("d"));
    }

    /// <summary>Geschoss-Regel (Hauptlauf 19.10.2026, SIA 400:2000 B.10.3 / SIA 410/2 Ziff. 2.3): Deckenaussparungen gehören ins
    /// Geschoss unter der Decke – D-1 (im OG, z 2750 … 3000): Unterkante 2750 − 1 = 2749 im Band des EG (0 … 3000) → EG.</summary>
    /// <summary>Rang 5 (31.10.2026, Befund Test_praxis): ein Fallstrang, der in der Decke an einem T-Stück geteilt ist, braucht trotzdem
    /// eine Aussparung – die beiden Teilstücke sind verbunden, keines beginnt «wirklich» in der Decke. Ein offenes Ende in der Decke nicht.</summary>
    [Fact]
    public void Leitungszug_InDerDeckeGeteilt_GibtEineAussparung_OffenesEndeNicht()
    {
        var p = Wandprojekt();
        Decke(p);
        Ok(Modul3Hilfe.Rohr(p, "RO-1", "DN 50", [3000, 2000, 2500], [3000, 2000, 2875]));
        Ok(Modul3Hilfe.Rohr(p, "RO-2", "DN 50", [3000, 2000, 2875], [3000, 2000, 3500]));
        Ok(Modul3Hilfe.Rohr(p, "RO-3", "DN 50", [1000, 2000, 2875], [1000, 2000, 3500]));   // beginnt offen in der Decke
        Ok(p.Ausfuehren("verbinden", new JsonObject { ["von"] = "RO-1.ende", ["nach"] = "RO-2.anfang" }));
        var e = Ok(Vorschlagen(p));
        var a = Aussparungen(p);
        var zug = Assert.Single(a, x => LeitungKz(p, x).Contains("RO-1"));
        Assert.Equal(["RO-1", "RO-2"], LeitungKz(p, zug).Order());
        Etwa(250, zug.Zahl("tiefe"), was: "ganze Deckendicke");
        Assert.DoesNotContain(a, x => LeitungKz(p, x).Contains("RO-3"));
        Assert.Contains(e.Hinweise, h => h.Contains("RO-3 durch D-1: die Leitung beginnt im Bauteil"));
    }

    [Fact]
    public void Kanal400x200_DurchDecke_Rechteckig_Koten_GeschossUnterDerDecke()
    {
        var p = Wandprojekt();
        Decke(p);
        Kanal(p, "KA-1", 400, 200, [3000, 2000, 2500], [3000, 2000, 3500]);   // senkrecht: b in x, h in y (wie HlksKoerper)
        Ok(Vorschlagen(p));
        var a = VonLeitung(p, "KA-1");
        Assert.Equal("rechteckig", a.Text("form"));
        Etwa(440, a.Zahl("b"), was: "b = 400 + 2 × 20 (in x)");
        Etwa(240, a.Zahl("h"), was: "h = 200 + 2 × 20 (in y)");
        Assert.Null(a.Zahl("d"));
        Etwa(250, a.Zahl("tiefe"), was: "Deckendicke");
        Assert.Equal("aussparung", a.Text("art"));
        Etwa(2750, a.Zahl("kote_uk"), was: "UK Decke");
        Etwa(3000, a.Zahl("kote_ok"), was: "OK Decke");
        Assert.Equal([3000, 2000, 2875], Mitte(a));
        Assert.Equal("[0,0,1]", a.Eigenschaft("achse")!.Inhalt!.ToJsonString());
        Assert.Equal(Hole(p, "EG").Id, a.Geschoss);        // Geschoss unter der Decke (UK 2750 − 1 im Band des EG)
        Assert.Equal(Hole(p, "D-1").Id, a.Bezuege["bauteil"]);
    }

    [Fact]
    public void Kanal400x200_WaagrechtDurchWand_KotenUkOk()
    {
        var p = Wandprojekt();
        Kanal(p, "KA-1", 400, 200, [2000, -800, 2000], [2000, 800, 2000]);
        Ok(Vorschlagen(p));
        var a = VonLeitung(p, "KA-1");
        Etwa(440, a.Zahl("b"), was: "b waagrecht in der Wand");
        Etwa(240, a.Zahl("h"), was: "h senkrecht");
        Etwa(2000 - 120, a.Zahl("kote_uk"));
        Etwa(2000 + 120, a.Zahl("kote_ok"));
        Etwa(200, a.Zahl("tiefe"));
    }

    [Fact]
    public void Rundkanal250_DurchWand_RundD290_Aussparung()
    {
        var p = Wandprojekt();
        Kanal(p, "KA-0", 400, 200, [9000, 9000, 2000], [9500, 9000, 2000]);   // legt das System ZUL an (fern der Wände)
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Kanal", ["kennzeichen"] = "KA-1", ["system"] = "ZUL", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["durchmesser"] = 250 },
            ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Pts([2000, -800, 2000], [2000, 800, 2000]) },
        }));
        Ok(Vorschlagen(p));
        var a = VonLeitung(p, "KA-1");
        Assert.Equal("rechteckig", a.Text("form"));   // B3b: auch der Rundkanal
        Etwa(250 + 2 * Spiel, a.Zahl("b")); Etwa(250 + 2 * Spiel, a.Zahl("h"));
        Assert.Equal("aussparung", a.Text("art"));
    }

    [Fact]
    public void Traeger_RohrDurchSteg_Rechteckig()
    {
        var p = Wandprojekt();
        // Unterzug 300 breit, 500 hoch, UK 2400 (z 2400 … 2900), Achse y = 2000
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Traeger", ["kennzeichen"] = "UZ-1", ["geschoss"] = "EG",
            ["geometrie"] = new JsonObject { ["art"] = "linie", ["punkte"] = Pts([500, 2000, 2400], [5500, 2000, 2400]), ["dicke"] = 300, ["hoehe"] = 500 },
        }));
        Ok(Modul3Hilfe.Rohr(p, "RO-1", "DN 20", [1000, 1500, 2600], [1000, 2500, 2600]));
        Ok(Vorschlagen(p));
        var a = VonLeitung(p, "RO-1");
        Assert.Equal(Hole(p, "UZ-1").Id, a.Bezuege["bauteil"]);
        Etwa(70, a.Zahl("b"), was: "DN 20: da 25 + 2 × 20 = 65 → ganze cm"); Etwa(70, a.Zahl("h"));
        Etwa(300, a.Zahl("tiefe"), was: "Trägerbreite");
        Assert.Equal("aussparung", a.Text("art"));
        Etwa(2600 - 35, a.Zahl("kote_uk")); Etwa(2600 + 35, a.Zahl("kote_ok"));
    }

    [Fact]
    public void ZweiRohre_Randabstand60_Sammelaussparung()
    {
        var p = Wandprojekt();
        RohrDurchW1(p, "RO-A", 1000);
        RohrDurchW1(p, "RO-B", 1155);   // Achsabstand 155: Rechtecke 95 × 95 → Randabstand 155 − 95 = 60 < 100
        var e = Ok(Vorschlagen(p));
        var a = Assert.Single(Aussparungen(p));
        Assert.Equal("rechteckig", a.Text("form"));
        Etwa(155 + 95, a.Zahl("b"), was: "umschliessendes Rechteck 250 (schon ganze cm)");
        Etwa(100, a.Zahl("h"), was: "95 → ganze cm");
        Assert.Equal("aussparung", a.Text("art"));
        Assert.Equal([1077.5, 0, 1000], Mitte(a));
        Etwa(1000 - 50, a.Zahl("kote_uk"));
        Etwa(1000 + 50, a.Zahl("kote_ok"));
        Assert.Equal(["RO-A", "RO-B"], LeitungKz(p, a));
        Assert.Contains("umschliessendes Rechteck von 2 Aussparungen", a.Eigenschaft("b")!.Herkunft.Quelle);
        Assert.Contains(e.Hinweise, h => h.Contains("1 Sammelaussparung"));
        Assert.Contains(e.Hinweise, h => h.Contains("(Sammelaussparung aus 2)"));
    }

    [Theory]
    [InlineData(1194, 1)]   // Randabstand 99 < 100 → zusammen
    [InlineData(1196, 2)]   // Randabstand 101 → getrennt
    public void ZweiRohre_GrenzeZusammenfassen(double x2, int anzahl)
    {
        var p = Wandprojekt();
        RohrDurchW1(p, "RO-A", 1000);
        RohrDurchW1(p, "RO-B", x2);
        Ok(Vorschlagen(p));
        Assert.Equal(anzahl, Aussparungen(p).Count);
    }

    [Fact]
    public void DreiRohre_Kette_UndUeberschneidung_ZusammenfassenOhneAbstand()
    {
        var p = Wandprojekt(vorgaben: false);
        Vorgaben(p, zusammenfassen: 0);   // FIKTIV: nur Überschneidungen zusammenfassen
        RohrDurchW1(p, "RO-A", 1000);
        RohrDurchW1(p, "RO-B", 1080);    // Rechtecke 95 × 95 überschneiden sich (Achsabstand 80 < 95)
        RohrDurchW1(p, "RO-C", 1300);    // frei
        Ok(Vorschlagen(p));
        var a = Aussparungen(p);
        Assert.Equal(2, a.Count);
        Assert.All(a, x => Assert.Equal("rechteckig", x.Text("form")));
        var sammel = Assert.Single(a, x => Aussparungsbezug.Leitungen(x).Count == 2);
        Etwa(180, sammel.Zahl("b"), was: "80 + 95 = 175 → ganze cm");
        Assert.Equal(["RO-A", "RO-B"], LeitungKz(p, sammel));
        // Kette mit 100 mm: A–B überschneiden sich (Rechteck x 952.5 … 1127.5); C bei 1220 liegt 1220 − 47.5 − 1127.5 = 45 < 100 vom
        // Rechteck, aber 220 − 95 = 125 von A – erst über die Sammelaussparung A+B kommt C dazu → eine
        var q = Wandprojekt();
        RohrDurchW1(q, "RO-A", 1000); RohrDurchW1(q, "RO-B", 1080); RohrDurchW1(q, "RO-C", 1220);
        Ok(Vorschlagen(q));
        var s = Assert.Single(Aussparungen(q));
        Etwa(320, s.Zahl("b"), was: "220 + 95 = 315 → ganze cm");
        Assert.Equal(["RO-A", "RO-B", "RO-C"], LeitungKz(q, s));
    }

    [Fact]
    public void LeitungEndetInDerWand_KeinVorschlag_Hinweis()
    {
        var p = Wandprojekt();
        Ok(Modul3Hilfe.Rohr(p, "RO-1", "DN 20", [3000, -500, 1000], [3000, 0, 1000]));   // endet in der Wandmitte
        var e = Ok(Vorschlagen(p));
        Assert.Empty(Aussparungen(p));
        Assert.Contains(e.Hinweise, h => h.Contains("RO-1 durch W-1: die Leitung endet im Bauteil"));
    }

    [Fact]
    public void LeitungLaengsInWandUndDecke_KeinVorschlag_QuerungDaneben_Schon()
    {
        var p = Wandprojekt();
        Decke(p);
        // Steigleitung in W-1 (y −50, neben der Decke): tritt durch die Unter- und Oberseite der Wand (z 0 bzw. 3000) – längs, keine Aussparung
        Ok(Modul3Hilfe.Rohr(p, "RO-S", "DN 20", [3000, -50, -500], [3000, -50, 3500]));
        // waagrecht in der Decke (z 2875) von aussen nach aussen: quert W-4 und W-2 durch die Dicke, die Decke über die Stirnseiten
        Ok(Modul3Hilfe.Rohr(p, "RO-D", "DN 20", [-500, 2000, 2875], [6500, 2000, 2875]));
        var e = Ok(Vorschlagen(p));
        Assert.Contains(e.Hinweise, h => h.Contains("RO-S durch W-1: die Leitung liegt längs in der Wand (Ein- bzw. Austritt über Ober- oder Unterseite)"));
        Assert.Contains(e.Hinweise, h => h.Contains("RO-D durch D-1: die Leitung liegt längs in der Decke (Ein- bzw. Austritt über die Stirnseite)"));
        var a = Aussparungen(p);
        Assert.Equal(2, a.Count);
        Assert.Equal(["W-2", "W-4"], a.Select(x => p.Modell.Finde(x.Bezuege["bauteil"])!.Kennzeichen).Order());   // Ids sind zufällig – nach Kennzeichen vergleichen
        Assert.All(a, x => Assert.Equal(["RO-D"], LeitungKz(p, x)));
        Assert.All(a, x => Assert.Equal("[1,0,0]", x.Eigenschaft("achse")!.Inhalt!.ToJsonString()));
        // waagrecht längs in W-1 (y = 0, über die Stirnseiten): W-2 und W-4 werden gequert, W-1 nicht
        var q = Wandprojekt();
        Ok(Modul3Hilfe.Rohr(q, "RO-L", "DN 20", [-500, 0, 1000], [6500, 0, 1000]));
        Ok(Vorschlagen(q));
        Assert.DoesNotContain(Aussparungen(q), x => x.Bezuege["bauteil"] == Hole(q, "W-1").Id);
        // flach durch W-1: 20° zur Wandfläche (Richtung (1, tan 20°, 0)) – keine Aussparung; 45° (Test Schraeg45Grad) schon
        var r = Wandprojekt();
        Ok(Modul3Hilfe.Rohr(r, "RO-F", "DN 20", [500, -500, 1000], [500 + 1000 / Math.Tan(20 * Math.PI / 180), 500, 1000]));
        var f = Ok(Vorschlagen(r));
        Assert.Empty(Aussparungen(r));
        Assert.Contains(f.Hinweise, h => h.Contains("RO-F durch W-1: die Leitung quert flacher als 30° zur Bauteilfläche"));
    }

    [Fact]
    public void KnickInDerWand_EinDurchgang()
    {
        var p = Wandprojekt();
        // Polylinie mit dem Knick in der Wand: (3000, −500) → (3000, 0) → (3100, 500); die Teilstücke enden bzw. beginnen innen
        Ok(Modul3Hilfe.RohrOhneSerie(p, "RO-1", 50, [3000, -500, 1000], [3000, 0, 1000], [3100, 500, 1000]));
        Ok(Vorschlagen(p));
        var a = Assert.Single(Aussparungen(p));
        Assert.Equal("rechteckig", a.Text("form"));
        Etwa(200, a.Zahl("tiefe"), 0.5);
    }

    [Fact]
    public void Vorgaben_Fehlen_AbgelehntMitNamen()
    {
        var p = Wandprojekt(vorgaben: false);
        RohrDurchW1(p, "RO-1", 3000);
        var e = Nein(Vorschlagen(p));
        foreach (var n in AussparungVorgaben.Namen) Assert.Contains(n, e.Fehler);
        Assert.Contains("kein Element der Klasse Vorgaben", e.Fehler);
        Assert.Contains("keine eingebauten Werte", e.Fehler);
        Assert.Contains("vorgaben_setzen", e.Vorschlag);
        Assert.Empty(Aussparungen(p));

        var q = Wandprojekt(vorgaben: false);
        Vorgaben(q, zusammenfassen: null);
        var f = Nein(Vorschlagen(q));
        Assert.DoesNotContain("spiel_aussparung", f.Fehler);
        Assert.DoesNotContain("grenze_kernbohrung", f.Fehler);   // B3b: nicht mehr verlangt
        Assert.Contains("abstand_zusammenfassen", f.Fehler);
        Assert.Contains("VG-1", f.Fehler);
        Assert.DoesNotContain("grenze_kernbohrung", AussparungVorgaben.Namen);

        var r = Wandprojekt(vorgaben: false);
        Vorgaben(r, mitQuelle: false);
        var g = Nein(Vorschlagen(r));
        Assert.Contains("spiel_aussparung (Wert 20 mm ohne Quelle – gilt nicht)", g.Fehler);
    }

    [Fact]
    public void Trockenlauf_ZeigtListe_Ausfuehren_Rueckgaengig()
    {
        var p = Wandprojekt();
        RohrDurchW1(p, "RO-1", 1000);
        RohrDurchW1(p, "RO-2", 4000);
        var vorher = p.Stand.Pruefsumme;
        var t = Ok(Vorschlagen(p, trocken: true));
        Assert.True(t.Trockenlauf);
        Assert.Equal(2, t.Aenderungen.Count);
        Assert.All(t.Aenderungen, x => Assert.Equal(("angelegt", "Aussparung"), (x.Art, x.Klasse)));
        Assert.Contains(t.Hinweise, h => h.Contains("AUS-1 neu: rechteckig 100 × 100 mm"));
        Assert.Contains(t.Hinweise, h => h.Contains("AUS-2 neu: rechteckig 100 × 100 mm"));
        Assert.Empty(Aussparungen(p));
        Assert.Equal(vorher, p.Stand.Pruefsumme);
        var e = Ok(Vorschlagen(p));
        Assert.NotNull(e.PaketId);
        Assert.Equal(2, Aussparungen(p).Count);
        Assert.Single(p.Verlauf, x => x.Befehl == "aussparungen_vorschlagen");   // ein Paket
        Ok(p.Rueckgaengig());
        Assert.Empty(Aussparungen(p));
        Assert.Equal(vorher, p.Stand.Pruefsumme);
    }

    [Fact]
    public void KeinePruefbefundeDurchDieAussparungen()
    {
        var p = Wandprojekt();
        RohrDurchW1(p, "RO-1", 3000);
        var e = Ok(Vorschlagen(p));
        Assert.DoesNotContain(e.NeueBefunde, b => b.Element.StartsWith("AUS-"));   // kein P08, P10, P11
    }

    [Fact]
    public void Entitaeten_GibtEsImXbimSchema()
    {
        // IFC-Klassen nur am Xbim-Schema prüfen (Auftrag): jede Entität der Regel gibt es als Klasse in Xbim.Ifc4 oder Xbim.Ifc2x3
        var namen = typeof(Xbim.Ifc4.Kernel.IfcProject).Assembly.GetTypes().Select(t => t.Name)
            .Concat(typeof(Xbim.Ifc2x3.Kernel.IfcProject).Assembly.GetTypes().Select(t => t.Name)).ToHashSet();
        foreach (var e in Aussparungsregeln.Entitaeten) Assert.Contains(e, namen);
    }

    [Fact]
    public void WandUeberZweiGeschosse_AussparungImGeschossIhrerHoehe()
    {
        // Hauptlauf 19.10.2026 (Befund Bernstrasse 42: Revit-Wände über mehrere Geschosse, Aussparung auf +5.47 im Plan EG):
        // Wand im EG (Kote 0, Höhe 3000) mit 6000 mm Höhe; Rohr bei z 1000 → EG, bei z 4000 → OG (Kote 3000). Werte FIKTIV.
        var p = Wandprojekt();
        Ok(p.Ausfuehren("wand_zeichnen", new JsonObject { ["geschoss"] = "EG", ["punkte"] = Pts([0, 6000], [6000, 6000]), ["dicke"] = 200, ["hoehe"] = 6000 }));
        Ok(Modul3Hilfe.Rohr(p, "RO-U", "DN 50", [1000, 5500, 1000], [1000, 6500, 1000]));
        Ok(Modul3Hilfe.Rohr(p, "RO-O", "DN 50", [2000, 5500, 4000], [2000, 6500, 4000]));
        Ok(Vorschlagen(p));
        Assert.Equal("EG", p.Modell.Finde(VonLeitung(p, "RO-U").Geschoss!)!.Kennzeichen);
        Assert.Equal("OG", p.Modell.Finde(VonLeitung(p, "RO-O").Geschoss!)!.Kennzeichen);
    }
}
