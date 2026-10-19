using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;
using static Hlks.Kern.Tests.RoutingHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3, B2: Nachführung beim Verschieben (Leitungsenden folgen den Anschlüssen) und P16 Gefälle.
/// Werte FIKTIV (Testfamilien).</summary>
public class RoutingNachfuehrungTests
{
    /// <summary>Z-Zug DN 20: RO-1 (0 → 950), BO-1 bei (1000, 0), RO-2 (1000, 50) → (1000, 950), BO-2 bei (1000, 1000), RO-3.</summary>
    static Projekt ZZug()
    {
        var p = MitSerie();
        Ok(Zeichnen(p, "DN 20", [[0, 0, 0], [1000, 0, 0], [1000, 1000, 0], [2000, 1000, 0]]));
        return p;
    }

    [Fact]
    public void FormteilVerschoben_BeideLeitungsendenFolgen_HinweisZurRichtung()
    {
        var p = ZZug();
        var e = Ok(p.Ausfuehren("elemente_verschieben", new JsonObject { ["elemente"] = new JsonArray("BO-1"), ["dx"] = 100 }));
        // BO-1 nach (1100, 0, 0): a bei 1100 − 50 = 1050, b bei (1100, 50, 0)
        Gerade(p, "RO-1", [0, 0, 0], [1050, 0, 0]);                 // Richtung bleibt
        Gerade(p, "RO-2", [1100, 50, 0], [1000, 950, 0]);           // Anfang folgt, Ende bleibt an BO-2
        Assert.Contains(e.Hinweise, h => h.StartsWith("RO-1: Ende folgt BO-1.a (100 mm verschoben)"));
        Assert.Contains(e.Hinweise, h => h.StartsWith("RO-2: Anfang folgt BO-1.b"));
        // RO-2 dreht um atan(100/900) = 6.34° und hängt am anderen Ende an BO-2 → Hinweis, keine Neuverlegung
        Assert.Contains(e.Hinweise, h => h.StartsWith("RO-2: Richtung um 6.34° geändert") && h.Contains("BO-2.a"));
        Assert.DoesNotContain(e.Hinweise, h => h.StartsWith("RO-1: Richtung"));
        Assert.DoesNotContain(Befunde(p, "P13"), b => b.Text.Contains("auseinander"));   // Lagen stimmen
        Assert.Contains(Befunde(p, "P13"), b => b.Text.Contains("RO-2.ende und BO-2.a") && b.Text.Contains("nicht gegeneinander"));
        // Rückgängig in einem Schritt
        Ok(p.Rueckgaengig());
        Gerade(p, "RO-2", [1000, 50, 0], [1000, 950, 0]);
        Sauber(p);
    }

    [Fact]
    public void FormteilGedreht_ElementAendern_EndeFolgt()
    {
        var p = ZZug();
        // BO-1 um z gedreht ist hier eine neue Ausrichtung: x = (cos 10°, sin 10°, 0), z = (0, 0, 1)
        double c = Math.Cos(10 * Math.PI / 180), s = Math.Sin(10 * Math.PI / 180);
        Ok(p.Ausfuehren("element_aendern", new JsonObject
        {
            ["element"] = "BO-1", ["eigenschaften"] = new JsonObject { ["ausrichtung"] = Ausrichtung([c, s, 0], [0, 0, 1]) },
        }));
        // a = P − 50·x = (1000 − 50 cos 10°, −50 sin 10°, 0)
        Etwa([1000 - 50 * c, -50 * s, 0], Pkt(p, "RO-1")[1], 1e-3);
        Etwa(Stelle(p, "BO-1", "b").Lage!, Pkt(p, "RO-2")[0], 1e-3);
        Assert.DoesNotContain(Befunde(p, "P13"), b => b.Text.Contains("auseinander"));
    }

    [Fact]
    public void GeraetVerschoben_DirektAngeschlosseneGeradeFolgt()
    {
        var p = MitSerie();
        MitSystem(p, "HZV", "Heizung Vorlauf");
        Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Datei("test_heizkoerper") }));
        Ok(p.Ausfuehren("familie_platzieren", new JsonObject { ["familie"] = "fam-test-heizkoerper", ["typ"] = "22-600-1000", ["punkt"] = new JsonArray(0, 0, 0), ["geschoss"] = "EG", ["kennzeichen"] = "HK-1" }));
        // DN 15 wie der Anschluss: keine Reduktion, RO-2 endet direkt an vl (950, 50, 120)
        Ok(Zeichnen(p, "DN 15", [[3000, 50, -500], [950, 50, -500], [950, 50, 120]], new JsonObject { ["nach"] = "HK-1.vl" }, system: "HZV"));
        IstVerbunden(p, "RO-2", "ende", "HK-1", "vl");
        var e = Ok(p.Ausfuehren("elemente_verschieben", new JsonObject { ["elemente"] = new JsonArray("HK-1"), ["dz"] = 100 }));
        Gerade(p, "RO-2", [950, 50, -460], [950, 50, 220]);   // Bogen DN 15: −500 + 40; Ende 120 + 100
        Assert.DoesNotContain(e.Hinweise, h => h.Contains("Richtung um"));   // senkrecht bleibt senkrecht
        Sauber(p);
    }

    [Fact]
    public void MitVerschoben_KeineDoppelteNachfuehrung()
    {
        var p = ZZug();
        var e = Ok(p.Ausfuehren("elemente_verschieben", new JsonObject { ["elemente"] = new JsonArray("RO-1", "BO-1", "RO-2", "BO-2", "RO-3"), ["dy"] = 500 }));
        Assert.DoesNotContain(e.Hinweise, h => h.Contains("folgt"));
        Gerade(p, "RO-2", [1000, 550, 0], [1000, 1450, 0]);
        Sauber(p);
    }

    // ------------------------------------------------------------------ P16

    static BefehlsErgebnis Rohr(Projekt p, string kz, string system, double? gefaelle, params double[][] punkte)
    {
        var eig = new JsonObject { ["familientyp"] = "DN 50" };
        if (gefaelle is double g) eig["gefaelle"] = g;
        return p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Rohr", ["kennzeichen"] = kz, ["system"] = system, ["geschoss"] = "EG", ["eigenschaften"] = eig,
            ["bezuege"] = new JsonObject { ["familie"] = "FAM-1" },
            ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Punkte(punkte) },
        });
    }

    [Fact]
    public void P16_Gefaelle_FehltFalschSteigt_SenkrechtUndFallstreckeAusgenommen()
    {
        var p = MitSerie();
        MitSystem(p, "SW", "Schmutzwasser");
        // 2 % über 1000 mm = 20 mm → stimmt
        Ok(Rohr(p, "RO-OK", "SW", 2, [0, 0, 1000], [1000, 0, 980]));
        // 1 % statt 2 % (10 mm auf 1000 mm): Abweichung 1 %-Punkt > 0.1 → fehler
        Ok(Rohr(p, "RO-FLACH", "SW", 2, [0, 500, 1000], [1000, 500, 990]));
        // 2.08 % statt 2 % (20.8 mm auf 1000 mm): Abweichung 0.08 ≤ 0.1 → in Ordnung
        Ok(Rohr(p, "RO-TOL", "SW", 2, [0, 600, 1000], [1000, 600, 979.2]));
        // steigt um 10 mm in Fliessrichtung → fehler
        Ok(Rohr(p, "RO-STEIGT", "SW", 2, [0, 1000, 1000], [1000, 1000, 1010]));
        // ohne gefaelle, waagrecht → warnung
        Ok(Rohr(p, "RO-OHNE", "SW", null, [0, 1500, 1000], [1000, 1500, 1000]));
        // senkrecht ohne gefaelle → ausgenommen
        Ok(Rohr(p, "RO-FALL", "SW", null, [0, 2000, 3000], [0, 2000, 0]));
        // 45° fallend (Verzug) mit gefaelle 2 → ausgenommen (Fallstrecke)
        Ok(Rohr(p, "RO-45", "SW", 2, [0, 2500, 3000], [1000, 2500, 2000]));
        // Druckleitung (KW) ohne Gefälle → keine Prüfung
        Ok(Rohr(p, "RO-KW", "KW", null, [0, 3000, 1000], [1000, 3000, 1000]));
        // Altbestand ohne Serie im Schwerkraftsystem → nicht geprüft (ANNAHME, P18)
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Rohr", ["kennzeichen"] = "RO-ALT", ["system"] = "SW", ["geschoss"] = "EG", ["eigenschaften"] = new JsonObject { ["dn"] = 50 },
            ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Punkte([0, 3500, 1000], [1000, 3500, 1000]) },
        }));
        var p16 = Befunde(p, "P16");
        Assert.Equal(["RO-FLACH", "RO-OHNE", "RO-STEIGT"], p16.Select(b => b.Element).Order().ToList());
        var flach = p16.Single(b => b.Element == "RO-FLACH");
        Assert.Equal("fehler", flach.Schwere);
        Assert.Contains("1 % statt Sollgefälle 2 %", flach.Text);
        Assert.Contains("Abweichung 1 %-Punkte", flach.Text);
        Assert.Equal("fehler", p16.Single(b => b.Element == "RO-STEIGT").Schwere);
        Assert.Contains("steigt in Fliessrichtung", p16.Single(b => b.Element == "RO-STEIGT").Text);
        Assert.Equal("warnung", p16.Single(b => b.Element == "RO-OHNE").Schwere);
        Assert.All(p16, b => Assert.False(string.IsNullOrWhiteSpace(b.Vorschlag)));
        Assert.Equal(0.1, Leitungspruefungen.GefaelleToleranz);
    }

    [Fact]
    public void P16_NachLeitungZeichnenMitGefaelle_Sauber_NachVerschiebenFalsch()
    {
        var p = MitSerie();
        MitSystem(p, "SW", "Schmutzwasser");
        // Fallleitung 3000 → 500 (senkrecht, behält die Länge), unten Bogen, dann 0.5 % über 2000 mm waagrecht (10 mm)
        Ok(Zeichnen(p, "DN 50", [[0, 0, 3000], [0, 0, 500], [2000, 0, 500]], new JsonObject { ["gefaelle"] = 0.5 }, system: "SW"));
        Etwa(490, Pkt(p, "RO-2")[1][2], 1e-6);
        // Knick senkrecht → 0.5 % fallend: 90° − atan(0.005) = 89.71° liegt innerhalb ±1° von 90°
        Assert.Empty(Befunde(p, "P16"));
        Sauber(p);
        // 2 %: 90° − atan(0.02) = 88.85° liegt ausserhalb ±1° → abgelehnt (ANNAHME Toleranz ±1°, Prüfliste; die Testserie hat keinen 88.5°-Bogen)
        var nein = Nein(Zeichnen(p, "DN 50", [[5000, 0, 3000], [5000, 0, 500], [7000, 0, 500]], new JsonObject { ["gefaelle"] = 2 }, system: "SW"));
        Assert.Contains("Knick von 88.85°", nein.Fehler);
        Ok(p.Ausfuehren("element_aendern", new JsonObject
        {
            ["element"] = "RO-2", ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Punkte(Pkt(p, "RO-2")[0], [2000, 0, 500]) },
        }));
        Assert.Contains(Befunde(p, "P16"), b => b.Element == "RO-2" && b.Schwere == "fehler");
    }
}
