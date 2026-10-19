using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;
using static Hlks.Kern.Tests.RoutingHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3, B2: leitung_zeichnen – Geraden, Bögen, Einbaulängen, Gefälle, Ablehnungen, Trockenlauf und Rückgängig.
/// Handrechnungen aus den FIKTIVEN Testfamilien (Testrohrserie da = DN + 5, Testbogen r = 2·da, Einbaulänge r·tan(w/2)).</summary>
public class RoutingZeichnenTests
{
    [Fact]
    public void LZug_DN20_HandrechnungDerEinbaulaengen()
    {
        var p = MitSerie();
        var e = Ok(Zeichnen(p, "DN 20", [[0, 0, 1000], [2000, 0, 1000], [2000, 1500, 1000]]));
        Assert.Equal(["RO-1", "RO-2"], Angelegt(e, "Rohr"));
        Assert.Equal(["BO-1"], Angelegt(e, "Bogen"));
        // DN 20: da = 25, r = 2·25 = 50, La = Lb = 50·tan(90°/2) = 50 mm
        Gerade(p, "RO-1", [0, 0, 1000], [1950, 0, 1000]);     // 2000 − 50 = 1950 mm
        Gerade(p, "RO-2", [2000, 50, 1000], [2000, 1500, 1000]); // 1500 − 50 = 1450 mm
        Etwa(1950, Laenge(p, "RO-1"));
        Etwa(1450, Laenge(p, "RO-2"));
        var bo = Hole(p, "BO-1");
        Etwa([2000, 0, 1000], Familien.Einfuegepunkt(bo));     // Bogen im Knick (Achsschnittpunkt)
        Assert.Equal("DN 20", Familien.Typ(bo));
        Assert.Equal(90, Leitungspruefungen.Bogenwinkel(p.Modell, p.Katalog, bo));
        Etwa([1950, 0, 1000], Stelle(p, "BO-1", "a").Lage);
        Etwa([2000, 50, 1000], Stelle(p, "BO-1", "b").Lage);
        // verbunden in Fliessrichtung
        IstVerbunden(p, "RO-1", "ende", "BO-1", "a");
        IstVerbunden(p, "BO-1", "b", "RO-2", "anfang");
        // Serie und Grösse an den Geraden (dn aus der Serie, Herkunft familie)
        var ro = Hole(p, "RO-1");
        Assert.Equal(Hole(p, "FAM-1").Id, ro.Bezuege["familie"]);
        Assert.Equal("DN 20", Familien.Typ(ro));
        Assert.Equal(20, ro.Zahl("dn"));
        Assert.Equal("familie", ro.Eigenschaft("dn")!.Herkunft.Art);
        Assert.All(ro.Anschluesse, a => Assert.Equal(20, a.Dn));
        Assert.Equal(Hole(p, "KW").Id, ro.System);
        Assert.Equal(Hole(p, "EG").Id, ro.Geschoss);   // aus der Höhe
        Sauber(p);
        // Übersicht in den Hinweisen
        Assert.Contains(e.Hinweise, h => h.Contains("2 Gerade(n), zusammen 3400 mm"));
        Assert.Contains(e.Hinweise, h => h.StartsWith("Gerade RO-1 DN 20: 1950 mm"));
        Assert.Contains(e.Hinweise, h => h.StartsWith("Bogen BO-1 Typ «DN 20», 90°") && h.Contains("a 50 mm, b 50 mm"));
    }

    [Fact]
    public void Steigleitung_ZweiBoegen_VollAusgerichtet()
    {
        var p = MitSerie();
        Ok(Zeichnen(p, "DN 20", [[0, 0, 500], [1000, 0, 500], [1000, 0, 3500], [2000, 0, 3500]]));
        // La = Lb = 50 (DN 20, 90°)
        Gerade(p, "RO-1", [0, 0, 500], [950, 0, 500]);          // 1000 − 50 = 950
        Gerade(p, "RO-2", [1000, 0, 550], [1000, 0, 3450]);     // 3000 − 2·50 = 2900 (senkrecht)
        Gerade(p, "RO-3", [1050, 0, 3500], [2000, 0, 3500]);    // 1000 − 50 = 950
        Etwa(2900, Laenge(p, "RO-2"));
        // Steigbogen: durch (1, 0, 0), seitlich (0, 0, 1) → x = (1, 0, 0), z = x × y = (0, −1, 0)
        var l1 = Familien.Einbaulage(Hole(p, "BO-1"))!;
        Etwa([1, 0, 0], l1.X.Feld(), 1e-9); Etwa([0, -1, 0], l1.Z.Feld(), 1e-9);
        // oben: durch (0, 0, 1), seitlich (1, 0, 0) → x = (0, 0, 1), z = (0, 0, 1) × (1, 0, 0) = (0, 1, 0)
        var l2 = Familien.Einbaulage(Hole(p, "BO-2"))!;
        Etwa([0, 0, 1], l2.X.Feld(), 1e-9); Etwa([0, 1, 0], l2.Z.Feld(), 1e-9);
        Etwa([1000, 0, 550], Stelle(p, "BO-1", "b").Lage);
        Etwa([1050, 0, 3500], Stelle(p, "BO-2", "b").Lage);
        IstVerbunden(p, "RO-2", "ende", "BO-2", "a");
        Sauber(p);
    }

    [Fact]
    public void Versatz45Grad_ZweiBoegen()
    {
        var p = MitSerie();
        Ok(Zeichnen(p, "DN 20", [[0, 0, 0], [1000, 0, 0], [2000, 1000, 0], [3000, 1000, 0]]));
        // 45°: La = Lb = 50·tan(22.5°) = 20.7107 mm
        double l = 50 * Tan(22.5);
        Etwa(20.7107, l, 1e-4);
        double s = Math.Sqrt(0.5);
        Gerade(p, "RO-1", [0, 0, 0], [1000 - l, 0, 0]);                                   // 979.289 mm
        Gerade(p, "RO-2", [1000 + l * s, l * s, 0], [2000 - l * s, 1000 - l * s, 0]);     // √2·1000 − 2·20.711 = 1372.793 mm
        Gerade(p, "RO-3", [2000 + l, 1000, 0], [3000, 1000, 0]);
        Etwa(Math.Sqrt(2) * 1000 - 2 * l, Laenge(p, "RO-2"), 1e-3);
        Etwa(1372.793, Laenge(p, "RO-2"), 1e-3);
        Assert.Equal(45, Leitungspruefungen.Bogenwinkel(p.Modell, p.Katalog, Hole(p, "BO-1"))!.Value, 6);
        Assert.Equal(45, Leitungspruefungen.Bogenwinkel(p.Modell, p.Katalog, Hole(p, "BO-2"))!.Value, 6);
        Assert.Equal(45, Hole(p, "BO-1").Zahl("winkel"));   // Exemplarwert mit ziel winkel
        Sauber(p);
    }

    [Fact]
    public void Winkel60_NichtInDerSerie_Abgelehnt()
    {
        var p = MitSerie();
        var vorher = Inhalt(p);
        var e = Nein(Zeichnen(p, "DN 20", [[0, 0, 0], [1000, 0, 0], [1500, 866.0254, 0]]));   // 60° (cos 60°, sin 60°)·1000
        Assert.Contains("Knick von 60°", e.Fehler);
        Assert.Contains("15°, 30°, 45°, 90°", e.Fehler);
        Assert.Contains("Punkt 2 [1000, 0, 0]", e.Fehler);
        Assert.Contains("2 × 45°", e.Vorschlag);
        Assert.Equal(vorher, Inhalt(p));
    }

    [Fact]
    public void ZuKurzeGerade_AbgelehntMitMindestlaenge_GenauPasstDirekt()
    {
        var p = MitSerie();
        // Abstand der Knicke 80 mm < 50 + 50 = 100 mm (zwei Bögen DN 20, 90°)
        var e = Nein(Zeichnen(p, "DN 20", [[0, 0, 0], [1000, 0, 0], [1000, 80, 0], [2000, 80, 0]]));
        Assert.Contains("80 mm lang", e.Fehler);
        Assert.Contains("mindestens 100 mm", e.Fehler);
        Assert.Contains("mindestens 100 mm", e.Vorschlag);
        // genau 100 mm: die Bögen werden direkt verbunden (keine Gerade dazwischen)
        var ok = Ok(Zeichnen(p, "DN 20", [[0, 0, 0], [1000, 0, 0], [1000, 100, 0], [2000, 100, 0]]));
        Assert.Equal(["RO-1", "RO-2"], Angelegt(ok, "Rohr"));
        IstVerbunden(p, "BO-1", "b", "BO-2", "a");
        Gerade(p, "RO-2", [1050, 100, 0], [2000, 100, 0]);
        Sauber(p);
    }

    [Fact]
    public void ZweiDimensionalePunkte_KoteDesGeschosses_GeraderZwischenpunktEntfaellt()
    {
        var p = MitSerie();
        var e = Ok(Zeichnen(p, "DN 20", [[0, 0], [500, 0], [1000, 0], [1000, 1000]], new JsonObject { ["geschoss"] = "EG" }));
        Assert.Equal(["RO-1", "RO-2"], Angelegt(e, "Rohr"));   // (500, 0) liegt auf der Geraden
        Gerade(p, "RO-1", [0, 0, 0], [950, 0, 0]);             // z = Kote EG = 0
    }

    [Fact]
    public void Gefaelle2Prozent_3000mmWaagrecht_60mm()
    {
        var p = MitSerie();
        MitSystem(p, "SW", "Schmutzwasser");
        // gerade 3000 mm mit 2 %: Δz = 0.02 · 3000 = 60 mm
        Ok(Zeichnen(p, "DN 50", [[0, 0, 1000], [3000, 0, 1000]], new JsonObject { ["gefaelle"] = 2 }, system: "SW"));
        Gerade(p, "RO-1", [0, 0, 1000], [3000, 0, 940]);
        Assert.Equal(2, Hole(p, "RO-1").Zahl("gefaelle"));
        Assert.Empty(Befunde(p, "P16"));
    }

    [Fact]
    public void Gefaelle_LZug_BogenwinkelInnerhalbDerToleranz()
    {
        var p = MitSerie();
        MitSystem(p, "SW", "Schmutzwasser");
        // Knoten: z1 = 1000 − 0.02·2000 = 960, z2 = 960 − 0.02·1000 = 940 (zusammen 3000 mm waagrecht → 60 mm)
        var e = Ok(Zeichnen(p, "DN 50", [[0, 0, 1000], [2000, 0, 1000], [2000, 1000, 1000]], new JsonObject { ["gefaelle"] = 2 }, system: "SW"));
        Etwa([2000, 0, 960], Familien.Einfuegepunkt(Hole(p, "BO-1")), 1e-6);
        Etwa(940, Pkt(p, "RO-2")[1][2], 1e-6);
        // Knick: cos = (0.02·0.02)/(√1.0004·√1.0004) → 89.977° – innerhalb ±0.5° von 90°, der Bogen bekommt 90° (Liste)
        Assert.Equal(90, Leitungspruefungen.Bogenwinkel(p.Modell, p.Katalog, Hole(p, "BO-1"))!.Value, 6);
        Assert.Contains(e.Hinweise, h => h.Contains("(Knick 89.977°)"));
        Assert.Contains(e.Hinweise, h => h.Contains("Gefälle 2 %: z von 1000 bis 940 mm"));
        // DN 50: r = 2·55 = 110 = La; RO-1 endet bei P1 − 110·d0, d0 = (2000, 0, −40)/2000.4 → z = 960 + 110·40/2000.4
        Etwa(960 + 110 * 40 / Math.Sqrt(2000 * 2000 + 40 * 40), Pkt(p, "RO-1")[1][2], 1e-6);
        Sauber(p);
        Assert.Empty(Befunde(p, "P16"));
    }

    [Fact]
    public void Gefaelle_SenkrechtesStueckBehaeltLaenge()
    {
        var p = MitSerie();
        MitSystem(p, "SW", "Schmutzwasser");
        // waagrecht 1000 mm (0.5 % → −5 mm), dann senkrecht 1000 mm hinunter (bleibt), dann waagrecht 1000 mm (−5 mm);
        // Knick senkrecht ↔ 0.5 %: 90° ± atan(0.005) = 0.29° – innerhalb ±0.5° (mit 2 % wären es 1.15°: abgelehnt, siehe P16-Test)
        Ok(Zeichnen(p, "DN 50", [[0, 0, 3000], [1000, 0, 3000], [1000, 0, 2000], [2000, 0, 2000]], new JsonObject { ["gefaelle"] = 0.5 }, system: "SW"));
        Etwa([1000, 0, 2995], Familien.Einfuegepunkt(Hole(p, "BO-1")), 1e-6);
        Etwa([1000, 0, 1995], Familien.Einfuegepunkt(Hole(p, "BO-2")), 1e-6);
        Etwa(1990, Pkt(p, "RO-3")[1][2], 1e-6);
        Assert.Null(Hole(p, "RO-2").Zahl("gefaelle"));   // senkrecht: kein Gefälle
        Assert.Equal(0.5, Hole(p, "RO-3").Zahl("gefaelle"));
        Sauber(p);
        Assert.Empty(Befunde(p, "P16"));
    }

    [Fact]
    public void Ablehnungen_MitVorschlag()
    {
        var p = MitSerie(formteile: false);
        // fehlende Formteil-Familie → Vorschlag aus der Formteilwahl (familie_laden)
        var e = Nein(Zeichnen(p, "DN 20", [[0, 0, 0], [1000, 0, 0], [1000, 1000, 0]]));
        Assert.Contains("fam-test-bogen", e.Fehler);
        Assert.Contains("familie_laden", e.Vorschlag);
        // Grösse gibt es nicht
        e = Nein(Zeichnen(p, "DN 65", [[0, 0, 0], [1000, 0, 0]]));
        Assert.Contains("DN 15, DN 20", e.Vorschlag);
        // Umkehr
        p = MitSerie();
        e = Nein(Zeichnen(p, "DN 20", [[0, 0, 0], [1000, 0, 0], [500, 0, 0]]));
        Assert.Contains("kehrt", e.Fehler);
        // ohne System
        e = Nein(Zeichnen(p, "DN 20", [[0, 0, 0], [1000, 0, 0]], system: null));
        Assert.Contains("System fehlt", e.Fehler);
        // Grösse als Zahl und als Objekt
        Ok(Zeichnen(p, "20", [[0, 0, 0], [1000, 0, 0]]));
        var par = new JsonObject { ["serie"] = "FAM-1", ["groesse"] = new JsonObject { ["dn"] = 25 }, ["punkte"] = Punkte([0, 500, 0], [1000, 500, 0]), ["system"] = "KW" };
        Ok(p.Ausfuehren("leitung_zeichnen", par));
        Assert.Equal("DN 25", Familien.Typ(Hole(p, "RO-2")));
    }

    [Fact]
    public void Trockenlauf_AendertNichts_RueckgaengigStelltAllesHer()
    {
        var p = MitSerie();
        var vorher = Inhalt(p);
        var t = Ok(Zeichnen(p, "DN 20", [[0, 0, 1000], [2000, 0, 1000], [2000, 1500, 1000]], trocken: true));
        Assert.True(t.Trockenlauf);
        Assert.Equal(vorher, Inhalt(p));
        Assert.Null(p.Modell.Finde("RO-1"));
        Assert.Contains(t.Hinweise, h => h.StartsWith("Gerade RO-1 DN 20: 1950 mm"));   // Vorschau mit Längen
        Assert.Contains(t.Hinweise, h => h.StartsWith("Bogen BO-1 Typ «DN 20», 90°"));
        var e = Ok(Zeichnen(p, "DN 20", [[0, 0, 1000], [2000, 0, 1000], [2000, 1500, 1000]]));
        Assert.NotNull(e.PaketId);
        Assert.Equal(1, p.Verlauf.Count(x => x.Id == e.PaketId));   // ein Befehl = ein Paket
        Assert.NotEqual(vorher, Inhalt(p));
        Ok(p.Rueckgaengig());
        Assert.Equal(vorher, Inhalt(p));
        Assert.Null(p.Modell.Finde("BO-1"));
        Assert.Empty(p.Modell.Verbindungen);
    }

    [Fact]
    public void Praefix_StrangUndEigenschaften()
    {
        var p = MitSerie();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Strang", ["kennzeichen"] = "KW-V1", ["teilVon"] = "KW" }));
        Ok(Zeichnen(p, "DN 20", [[0, 0, 1000], [2000, 0, 1000], [2000, 1500, 1000]], new JsonObject
        {
            ["strang"] = "KW-V1", ["praefix"] = "KW-V1", ["eigenschaften"] = new JsonObject { ["material"] = "Edelstahl", ["daemmdicke"] = 20 },
        }, system: null));
        var ro = Hole(p, "KW-V1-1");
        Assert.Equal(Hole(p, "KW-V1").Id, ro.TeilVon);
        Assert.Equal(Hole(p, "KW").Id, ro.System);
        Assert.Equal("Edelstahl", ro.Text("material"));
        Assert.Equal(20, ro.Zahl("daemmdicke"));
        Assert.NotNull(p.Modell.Finde("KW-V1-2"));
        Assert.Equal(Hole(p, "KW-V1").Id, Hole(p, "BO-1").TeilVon);   // Formteile im Strang
    }
}
