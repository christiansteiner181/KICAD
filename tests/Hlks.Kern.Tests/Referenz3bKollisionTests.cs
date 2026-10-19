using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.KollisionspruefungHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3b, B5: Referenzfälle von Hand zur Kollisionsprüfung. Jeder Fall hat einen einfachen Aufbau; die Rechnung steht
/// als Kommentar im Test und folgt der REGEL der Spezifikation (entwurf/modul-3b.md, Grundsatzentscheide 2–4, B2/B2b), nicht dem Code.
///
/// ALLE WERTE FIKTIV: Rohrdurchmesser aus der Testrohrserie (da = DN + 5, DN 20 → r 12.5, DN 50 → r 27.5), Dämmdicken, Achsabstände,
/// Vorgaben (abstand_leitungen, toleranz_hart, Spiel, Grenzen) und Wanddicke 200 sind Testwerte – keine Norm-, Hersteller- oder
/// Büroangaben. Die Vorgaben tragen die Quelle «FIKTIV».
///
/// Aufbau (Wandprojekt): Wand W-1 (Süd) mit Achse y = 0, Dicke 200 → y −100 … +100, Höhe 0 … 3000, x von −100 bis 6100.
/// Konvention «Eindringtiefe»: kleinster Weg, um den Körper aus dem Bauteil zu schieben (Rohr in der Wandmitte: r + Dicke/2).</summary>
public class Referenz3bKollisionTests
{
    // ------------------------------------------------------------------ 1 Rohr quert Wand senkrecht

    /// <summary>DN 50 (da 55, r 27.5) quert W-1 senkrecht bei x = 1500, Höhe z = 1500, von y = −300 bis y = +400 (nicht symmetrisch
    /// zur Wand). Wand y −100 … +100.
    /// Regel: Durchdringung eines Bauteils durch eine Leitung = hart; Lage = Mitte des Durchgangs (nicht Mitte der Leitung).
    /// Durchgang auf der Achse: y −100 … +100 → Mitte y = 0 → Lage (1500, 0, 1500).
    /// Eindringtiefe (Achse in der Wandmitte): r + Dicke/2 = 27.5 + 200/2 = 127.5 mm.</summary>
    [Fact]
    public void F01_RohrQuertWandSenkrecht_HartDurchdringung_LageDurchgangsmitte()
    {
        var p = Wandprojekt();
        Ok(Rohr(p, "RO-1", "DN 50", [1500, -300, 1500], [1500, 400, 1500]));
        Ok(Pruefen(p));
        var k = Assert.Single(Kollisionen(p));
        Assert.Equal("hart", k.Text("art"));
        Assert.Equal("durchdringung", k.Text("typ"));
        Assert.Equal(Hole(p, "W-1").Id, k.Text("bauteil"));
        Assert.Equal(127.5, k.Zahl("mass"));
        Assert.Equal([1500, 0, 1500], Lage(k));
        Assert.Equal("RO-1 ↔ W-1", k.Name);
    }

    /// <summary>Senkrechtes Rohr DN 20 (r 12.5) durch die Decke D-1 (z 2750 … 3000, Dicke 250) bei (3000, 2000).
    /// Durchgang z 2750 … 3000 → Mitte z = 2875 → Lage (3000, 2000, 2875). Eindringtiefe = 12.5 + 250/2 = 137.5.</summary>
    [Fact]
    public void F01b_RohrQuertDecke_Durchdringung_Mitte2875_Mass137_5()
    {
        var p = AussparungHilfe.Wandprojekt(vorgaben: false);
        AussparungHilfe.Decke(p);
        Ok(Rohr(p, "RO-1", "DN 20", [3000, 2000, 2500], [3000, 2000, 3500]));
        Ok(Pruefen(p));
        var k = Assert.Single(Kollisionen(p));
        Assert.Equal(("hart", "durchdringung"), (k.Text("art"), k.Text("typ")));
        Assert.Equal(Hole(p, "D-1").Id, k.Text("bauteil"));
        Assert.Equal(137.5, k.Zahl("mass"));
        Assert.Equal([3000, 2000, 2875], Lage(k));
    }

    // ------------------------------------------------------------------ 2 seitliche Berührung, toleranz_hart

    /// <summary>DN 50 (r 27.5) liegt längs an der Innenseite von W-1 (Innenfläche y = +100), Achse y = 125.5, x 1000 … 2000, z 1000.
    /// Nächster Punkt des Rohrs zur Wand: 125.5 − 27.5 = 98.0 → ragt 100 − 98.0 = 2.0 mm in die Wand. Die Achse liegt nicht in der
    /// Wand (y 125.5 > 100) → keine Querung, also Kontakt statt Durchdringung; Mass = Tiefe = 2.0.
    /// toleranz_hart 1.9: 2.0 &gt; 1.9 → hart. toleranz_hart 2.1: 2.0 ≤ 2.1 → Berührung, keine Kollision (KOL behoben).</summary>
    [Fact]
    public void F02_SeitlicheBeruehrung2mm_ToleranzHart_KnappDarunterKnappDarueber()
    {
        var p = Wandprojekt();
        Ok(Rohr(p, "RO-1", "DN 50", [1000, 125.5, 1000], [2000, 125.5, 1000]));
        Ok(Vorgaben(p, (Projektvorgaben.ToleranzHart, 1.9)));
        Ok(Pruefen(p));
        var k = Assert.Single(Kollisionen(p));
        Assert.Equal(("hart", "kontakt", "neu"), (k.Text("art"), k.Text("typ"), k.Text("status")));
        Assert.Null(k.Text("bauteil"));
        Assert.Equal(2.0, k.Zahl("mass"));
        // toleranz_hart 2.1 → nicht mehr gemeldet
        Ok(Vorgaben(p, (Projektvorgaben.ToleranzHart, 2.1)));
        Ok(Pruefen(p));
        Assert.Equal("behoben", Assert.Single(Kollisionen(p)).Text("status"));
        // wieder 1.9 → wieder hart, dieselbe Kollision wieder «neu»
        Ok(Vorgaben(p, (Projektvorgaben.ToleranzHart, 1.9)));
        Ok(Pruefen(p));
        var wieder = Assert.Single(Kollisionen(p));
        Assert.Equal(("KOL-1", "neu"), (wieder.Kennzeichen, wieder.Text("status")));
    }

    /// <summary>Gleiches Rohr, aber die Achse liegt 3 mm weiter draussen (y = 128.5): 128.5 − 27.5 = 101.0 &gt; 100 → Abstand 1.0 mm
    /// zur Wand, keine Berührung → bei fehlender Vorgabe weich entfällt, hart: nichts.</summary>
    [Fact]
    public void F02b_RohrEinMillimeterVorDerWand_KeineHarteKollision()
    {
        var p = Wandprojekt();
        Ok(Rohr(p, "RO-1", "DN 50", [1000, 128.5, 1000], [2000, 128.5, 1000]));
        Ok(Pruefen(p));
        Assert.Empty(Kollisionen(p));
    }

    // ------------------------------------------------------------------ 3 parallele Rohre mit Dämmung, weich

    /// <summary>Zwei parallele DN 20 (r 12.5), Achsabstand 80 mm (y = 500 und 580, z 1000, x 0 … 3000 im Raum), beide mit Dämmung 10.
    /// Dämmhülle r = 12.5 + 10 = 22.5. Abstand der Hüllen = 80 − 22.5 − 22.5 = 35.0 mm. Ohne Dämmung wäre er 80 − 25 = 55.
    /// Regel: weich genau dann, wenn Abstand der Hüllen &lt; Vorgabe abstand_leitungen; Mass = Vorgabe − Abstand.
    /// Vorgabe 40 → 35 &lt; 40 → weich, Mass 5. Vorgabe 34.9 → 35 ≥ 34.9 → nichts. Vorgabe 35.1 → weich, Mass 0.1.
    /// Ohne Dämmung (Abstand 55) und Vorgabe 40 → nichts: erst die Dämmung macht es weich.</summary>
    [Fact]
    public void F03_ParalleleRohreMitDaemmung_WeichGenauWennHuellenabstandUnterVorgabe()
    {
        var p = Wandprojekt();
        Ok(Rohr(p, "RO-1", "DN 20", [1000, 1500, 1000], [3000, 1500, 1000]));
        Ok(Rohr(p, "RO-2", "DN 20", [1000, 1580, 1000], [3000, 1580, 1000]));
        // ohne Dämmung: Abstand 55 ≥ 40
        Ok(Vorgaben(p, (Projektvorgaben.AbstandLeitungen, 40)));
        Ok(Pruefen(p));
        Assert.Empty(Kollisionen(p));
        // mit Dämmung 10 beidseitig: Hüllenabstand 35 < 40
        Daemmung(p, "RO-1", 10); Daemmung(p, "RO-2", 10);
        Ok(Pruefen(p));
        var k = Assert.Single(Kollisionen(p));
        Assert.Equal(("weich", "RO-1 ↔ RO-2"), (k.Text("art"), k.Name));
        Assert.Equal(5.0, k.Zahl("mass"));
        // knapp unter der Vorgabe: 34.9 < 35 → kein Befund, KOL-1 behoben
        Ok(Vorgaben(p, (Projektvorgaben.AbstandLeitungen, 34.9)));
        Ok(Pruefen(p));
        Assert.Equal("behoben", Assert.Single(Kollisionen(p)).Text("status"));
        // knapp über der Vorgabe: 35.1 > 35 → weich, Mass 0.1
        Ok(Vorgaben(p, (Projektvorgaben.AbstandLeitungen, 35.1)));
        Ok(Pruefen(p));
        var wieder = Assert.Single(Kollisionen(p));
        Assert.Equal("neu", wieder.Text("status"));
        Assert.Equal(0.1, wieder.Zahl("mass"));
    }

    // ------------------------------------------------------------------ 4 Aussparung deckt die Leitung

    /// <summary>DN 50 (da 55) mit Dämmung 10 → Querschnitt mit Dämmung Ø 75 quert W-1 bei x = 3000.
    /// Ohne Aussparung: hart, Durchdringung, Mass 27.5 + 100 = 127.5.
    /// Vorgeschlagene Aussparung (Spiel 20, B3b rechteckig): b = h = 75 + 2·20 = 115 → auf ganze cm 120 (Nachtrag B4e; vorher 115) ≥ 75
    /// → deckt → keine Kollision (KOL-1 behoben).
    /// Von Hand rund (aussparung_form): d = 115 → 120 → deckt weiter.
    /// Aussparung d = 74 &lt; 75: die Dämmhülle ragt in die Wand → wieder Durchdringung. d = 76 ≥ 75 → wieder gedeckt.</summary>
    [Fact]
    public void F04_RohrDurchWandMitAussparung_KeineKollision_ZuKleineAussparung_WiederKollision()
    {
        var p = Wandprojekt();
        AussparungHilfe.Vorgaben(p);
        Ok(Rohr(p, "RO-1", "DN 50", [3000, -500, 1000], [3000, 500, 1000]));
        Daemmung(p, "RO-1", 10);
        Ok(Pruefen(p));
        var k = Assert.Single(Kollisionen(p));
        Assert.Equal(("durchdringung", 127.5), (k.Text("typ"), k.Zahl("mass")!.Value));
        // Aussparung vorschlagen: 115 × 115 → 120 × 120
        Ok(p.Ausfuehren("aussparungen_vorschlagen", new JsonObject()));
        Assert.Equal((120.0, 120.0), (Hole(p, "AUS-1").Zahl("b")!.Value, Hole(p, "AUS-1").Zahl("h")!.Value));
        Ok(Pruefen(p));
        Assert.Equal("behoben", Assert.Single(Kollisionen(p)).Text("status"));
        // von Hand rund: d = 115 → 120
        Ok(AussparungHilfe.Form(p, "rund", "AUS-1"));
        Assert.Equal(120.0, Hole(p, "AUS-1").Zahl("d"));
        Ok(Pruefen(p));
        Assert.Equal("behoben", Assert.Single(Kollisionen(p)).Text("status"));
        // zu klein
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "AUS-1", ["eigenschaften"] = new JsonObject { ["d"] = 74 } }));
        Ok(Pruefen(p));
        var wieder = Assert.Single(Kollisionen(p));
        Assert.Equal(("neu", "durchdringung"), (wieder.Text("status"), wieder.Text("typ")));
        // gerade gross genug
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "AUS-1", ["eigenschaften"] = new JsonObject { ["d"] = 76 } }));
        Ok(Pruefen(p));
        Assert.Equal("behoben", Assert.Single(Kollisionen(p)).Text("status"));
    }

    // ------------------------------------------------------------------ 5 verbundene Elemente

    /// <summary>Zwei fluchtende DN 20 (y = 1000, z = 1000): RO-1 x 500 … 1500, RO-2 x 1500 … 2500. Die Enden treffen sich im Punkt
    /// (1500, 1000, 1000): Abstand der Achsen 0 &lt; r + r = 25 → hart, Mass 25 − 0 = 25 (HLKS gegen HLKS).
    /// Nach verbinden(RO-1.ende, RO-2.anfang) sind sie verbunden → keine Kollision.</summary>
    [Fact]
    public void F05_VerbundeneRohre_KeineKollision_GetrenntHartMass25()
    {
        var p = Wandprojekt();
        Ok(Rohr(p, "RO-1", "DN 20", [500, 1000, 1000], [1500, 1000, 1000]));
        Ok(Rohr(p, "RO-2", "DN 20", [1500, 1000, 1000], [2500, 1000, 1000]));
        Ok(Pruefen(p));
        var k = Assert.Single(Kollisionen(p));
        Assert.Equal(("hart", 25.0), (k.Text("art"), k.Zahl("mass")!.Value));
        Ok(p.Ausfuehren("verbinden", new JsonObject { ["von"] = "RO-1.ende", ["nach"] = "RO-2.anfang" }));
        Ok(Pruefen(p));
        Assert.Equal("behoben", Assert.Single(Kollisionen(p)).Text("status"));
    }

    /// <summary>Winkelzug DN 20 mit Bogen (leitung_zeichnen: RO – BO – RO, alle verbunden). Die Rohrenden am Bogen liegen
    /// 50·√2 = 70.7 mm auseinander (Bogenradius r = 2·da = 50, Einbaulänge r·tan 45° = 50 je Schenkel). Mit abstand_leitungen 50
    /// wäre RO-1 ↔ RO-2 weich (70.7 − 25 = 45.7 &lt; 50), wenn die Verbindung über das Formteil nicht zählte → keine Kollision.</summary>
    [Fact]
    public void F05b_RohrBogenRohr_KeineKollision_AuchNichtWeich()
    {
        var p = MitSerie();
        Ok(RoutingHilfe.Zeichnen(p, "DN 20", [[0, 0, 1000], [1800, 0, 1000], [1800, 1300, 1000]]));
        Ok(Vorgaben(p, (Projektvorgaben.AbstandLeitungen, 50)));
        Ok(Pruefen(p));
        Assert.Empty(Kollisionen(p));
    }

    // ------------------------------------------------------------------ 6 stabile Kennung

    /// <summary>Regel (B2): ein Befund gehört zur bestehenden Kollision, wenn die Kennung gleich ist oder – bei denselben
    /// Beteiligten – die gespeicherte Lage näher als der Fang 50 mm (halbes 100-mm-Raster) liegt.
    /// Rohr bei x = 3030 → Lage (3030, 0, 1000), gerundet 3000. Verschoben um 49 mm → 3079 (gerundet 3100, also andere Rasterzelle):
    /// 49 &lt; 50 → dieselbe Kollision (KOL-1), Status bleibt «bestaetigt», Kennung bleibt.</summary>
    [Fact]
    public void F06a_VerschiebungUm49mm_DieselbeKollision_StatusBleibt()
    {
        var p = Wandprojekt();
        Ok(Rohr(p, "RO-1", "DN 20", [3030, -500, 1000], [3030, 500, 1000]));
        Ok(Pruefen(p));
        var kennung = Assert.Single(Kollisionen(p)).Text("kennung")!;
        Assert.EndsWith("@3000,0,1000", kennung);
        Ok(Status(p, "bestaetigt", "gesehen", "KOL-1"));
        Geometrie(p, "RO-1", [3079, -500, 1000], [3079, 500, 1000]);
        Ok(Pruefen(p));
        var k = Assert.Single(Kollisionen(p));
        Assert.Equal(("KOL-1", "bestaetigt", kennung), (k.Kennzeichen, k.Text("status"), k.Text("kennung")));
        Assert.Equal([3079, 0, 1000], Lage(k));
    }

    /// <summary>Wie oben, aber um 51 mm verschoben (3030 → 3081): 51 &gt; 50 → neue Kollision KOL-2 (Lage gerundet 3100, Kennung
    /// endet «@3100,0,1000»), KOL-1 wird «behoben», bleibt aber bestehen.</summary>
    [Fact]
    public void F06b_VerschiebungUm51mm_NeueKollision_AlteBehoben()
    {
        var p = Wandprojekt();
        Ok(Rohr(p, "RO-1", "DN 20", [3030, -500, 1000], [3030, 500, 1000]));
        Ok(Pruefen(p));
        Ok(Status(p, "bestaetigt", null, "KOL-1"));
        Geometrie(p, "RO-1", [3081, -500, 1000], [3081, 500, 1000]);
        Ok(Pruefen(p));
        Assert.Equal(2, Kollisionen(p).Count);
        Assert.Equal("behoben", Hole(p, "KOL-1").Text("status"));
        var neu = Hole(p, "KOL-2");
        Assert.Equal("neu", neu.Text("status"));
        Assert.EndsWith("@3100,0,1000", neu.Text("kennung"));
        Assert.Equal([3081, 0, 1000], Lage(neu));
    }

    // ------------------------------------------------------------------ 7 zwei Läufe ohne Änderung

    /// <summary>Regel (B2b): pruefung = Lauf, der die Kollision zuerst fand; ein unveränderter Lauf schreibt nur die
    /// Projektinformation (kollisionslauf = Lauf 2).</summary>
    [Fact]
    public void F07_ZweiLaeufeOhneAenderung_NurProjektinformationAendertSich()
    {
        var p = Wandprojekt();
        Ok(Rohr(p, "RO-1", "DN 20", [3000, -500, 1000], [3000, 500, 1000]));
        Ok(Pruefen(p));
        var k1 = Assert.Single(Kollisionen(p));
        var vorher = (k1.Version, Json.Text(k1), k1.Text("pruefung"));
        Assert.Contains("Lauf 1", k1.Text("pruefung"));
        var l2 = Ok(Pruefen(p));
        var k2 = Assert.Single(Kollisionen(p));
        Assert.Equal(vorher, (k2.Version, Json.Text(k2), k2.Text("pruefung")));
        Assert.Contains("Lauf 1", k2.Text("pruefung"));
        Assert.DoesNotContain("Lauf 2", k2.Text("pruefung"));
        var ops = p.Verlauf.Single(x => x.Id == l2.PaketId).Operationen;
        var op = Assert.Single(ops);
        Assert.Equal("Projektinformation", op.Element!.Klasse);
        Assert.Contains("Lauf 2", HuelleHilfe.Projektinfo(p.Modell)!.Text("kollisionslauf"));
    }
}
