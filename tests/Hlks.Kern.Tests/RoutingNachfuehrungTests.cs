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
    public void FormteilQuerVerschoben_NachbarMitVersatz_ZweiBoegen45()
    {
        var p = ZZug();
        var e = Ok(p.Ausfuehren("elemente_verschieben", new JsonObject { ["elemente"] = new JsonArray("BO-1"), ["dx"] = 100 }));
        // BO-1 nach (1100, 0, 0): a bei 1100 − 50 = 1050, b bei (1100, 50, 0)
        Gerade(p, "RO-1", [0, 0, 0], [1050, 0, 0]);                 // Richtung bleibt: nur länger
        Assert.Contains(e.Hinweise, h => h.StartsWith("RO-1: Ende folgt BO-1.a (100 mm verschoben)"));
        // A1 (Entscheid Christian 03.11.2026): RO-2 hängt am anderen Ende an BO-2 → Versatz 100 mm in der Mitte mit zwei 45°-Bögen.
        // Achse von BO-2 (1000, 950) zurück: 900 mm, schräg 100 mm → je 400 mm gerade; Bögen bei (1100, 450) und (1000, 550),
        // Einbaulänge 45° = 50 · tan 22.5° = 20.71 mm
        Assert.Contains(e.Hinweise, h => h.StartsWith("RO-2: Versatz 100 mm mit zwei Bögen 45°"));
        Gerade(p, "RO-2", [1100, 50, 0], [1100, 450 - 50 * Tan(22.5), 0]);
        Assert.Equal(2, Angelegt(e, "Bogen").Count);
        Sauber(p);
        // Rückgängig in einem Schritt
        Ok(p.Rueckgaengig());
        Gerade(p, "RO-2", [1000, 50, 0], [1000, 950, 0]);
        Sauber(p);
    }

    [Fact]
    public void GeradeQuerVerschoben_FormteileFolgen_NachbarnDehnenSich()
    {
        var p = ZZug();
        var e = Ok(p.Ausfuehren("elemente_verschieben", new JsonObject { ["elemente"] = new JsonArray("RO-2"), ["dx"] = 100 }));
        Assert.Contains(e.Hinweise, h => h.StartsWith("BO-1 folgt RO-2 (100 mm"));
        Assert.Contains(e.Hinweise, h => h.StartsWith("BO-2 folgt RO-2 (100 mm"));
        Gerade(p, "RO-1", [0, 0, 0], [1050, 0, 0]);
        Gerade(p, "RO-2", [1100, 50, 0], [1100, 950, 0]);
        Gerade(p, "RO-3", [1150, 1000, 0], [2000, 1000, 0]);
        Sauber(p);
    }

    [Fact]
    public void GeradeEntlangAchseVerschoben_NachbarMitFreiemEnde_FolgtGanz()
    {
        var p = MitSerie();
        Ok(Zeichnen(p, "DN 20", [[0, 0, 0], [1000, 0, 0], [1000, 1000, 0]]));   // RO-1, BO-1, RO-2 (Ende frei)
        var e = Ok(p.Ausfuehren("elemente_verschieben", new JsonObject { ["elemente"] = new JsonArray("RO-1"), ["dx"] = 100 }));
        Assert.Contains(e.Hinweise, h => h.StartsWith("RO-2 folgt BO-1.b ganz"));
        Gerade(p, "RO-2", [1100, 50, 0], [1100, 1000, 0]);
        Sauber(p);
    }

    [Fact]
    public void Verschieben_MitTrennen_NichtsFolgt()
    {
        var p = ZZug();
        var e = Ok(p.Ausfuehren("elemente_verschieben", new JsonObject { ["elemente"] = new JsonArray("RO-2"), ["dx"] = 100, ["trennen"] = true }));
        Assert.Contains(e.Hinweise, h => h.StartsWith("2 Verbindung(en)"));
        Assert.DoesNotContain(e.Hinweise, h => h.Contains("folgt"));
        Gerade(p, "RO-1", [0, 0, 0], [950, 0, 0]);
        Assert.False(Verbunden(p, "RO-2", "anfang", "BO-1", "b"));
    }

    [Fact]
    public void DirektVerbundeneGerade_FolgtWieAnEinemFormteil()
    {
        var p = MitSerie();
        Ok(Zeichnen(p, "DN 20", [[0, 0, 0], [1000, 0, 0]]));      // RO-1
        Ok(Zeichnen(p, "DN 20", [[1000, 0, 0], [2000, 0, 0]], new JsonObject { ["von"] = "RO-1.ende" }));   // RO-2 direkt an RO-1, Ende frei
        IstVerbunden(p, "RO-1", "ende", "RO-2", "anfang");
        var e = Ok(p.Ausfuehren("elemente_verschieben", new JsonObject { ["elemente"] = new JsonArray("RO-1"), ["dy"] = 100 }));
        Gerade(p, "RO-2", [1000, 100, 0], [2000, 100, 0]);    // anderes Ende frei → ganz mit
        Assert.Contains(e.Hinweise, h => h.StartsWith("RO-2 folgt RO-1.ende ganz"));
        Sauber(p);
    }

    [Fact]
    public void Versatz_ZuKurz_BefehlAbgelehnt()
    {
        var p = ZZug();
        var vorher = Inhalt(p);
        var e = p.Ausfuehren("elemente_verschieben", new JsonObject { ["elemente"] = new JsonArray("BO-1"), ["dx"] = 1000 });
        Assert.False(e.Angenommen);
        Assert.Contains("RO-2 müsste 1000 mm versetzt werden", e.Fehler);
        Assert.Equal(vorher, Inhalt(p));
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

    /// <summary>Gesamtprüfung 12.11.2026: sechs Reduktionen DN 25/20 (l = 40, FIKTIV) direkt aneinander, abwechselnd gewendet (0 … 240 mm),
    /// davor RO-1 (−1000 … 0), dahinter RO-2 (240 … 1240, Ende frei). Bis dahin endete die Nachführung nach 5 Runden: RE-6 und RO-2 blieben
    /// stehen, das Netz riss ohne Hinweis.</summary>
    [Fact]
    public void KetteAusSechsFormteilen_FolgtGanz_BisZurLeitungDahinter()
    {
        var p = MitSerie();
        Ok(Modul3Hilfe.Rohr(p, "RO-1", "DN 25", [-1000, 0, 0], [0, 0, 0]));
        Ok(Modul3Hilfe.Rohr(p, "RO-2", "DN 25", [240, 0, 0], [1240, 0, 0]));
        for (int i = 1; i <= 6; i++)
        {
            bool gewendet = i % 2 == 0;   // ungerade: a (DN 25) vorn bei 40·(i−1); gerade: a hinten bei 40·i
            double x = gewendet ? 40 * i : 40 * (i - 1);
            Ok(Formteil(p, "fam-test-reduktion", "DN 25/20", $"RE-{i}", [x, 0, 0], x: gewendet ? [-1, 0, 0] : [1, 0, 0], z: [0, 0, 1]));
        }
        void V(string von, string nach) => Ok(p.Ausfuehren("verbinden", new JsonObject { ["von"] = von, ["nach"] = nach }));
        V("RO-1.ende", "RE-1.a");
        for (int i = 1; i < 6; i++) V($"RE-{i}.{(i % 2 == 1 ? "b" : "a")}", $"RE-{i + 1}.{(i % 2 == 1 ? "b" : "a")}");
        V("RE-6.a", "RO-2.anfang");

        var e = Ok(p.Ausfuehren("elemente_verschieben", new JsonObject { ["elemente"] = new JsonArray("RO-1"), ["dy"] = 100 }));
        for (int i = 1; i <= 6; i++) Etwa(100, Hole(p, $"RE-{i}").Geometrie!.Punkte[0][1], 1e-6, $"RE-{i}");
        Gerade(p, "RO-2", [240, 100, 0], [1240, 100, 0]);
        Assert.DoesNotContain(e.Hinweise, h => h.Contains("abgebrochen"));
        Assert.Empty(Befunde(p, "P13"));
        Ok(p.Rueckgaengig());
        Gerade(p, "RO-2", [240, 0, 0], [1240, 0, 0]);
    }

    /// <summary>Regel, die sich selbst endlos auslöst: setzt jedes geänderte Element erneut – mit immer neuem Inhalt (Zähler im Namen)
    /// bzw. als Pendel zwischen zwei Namen.</summary>
    sealed class Endlos(bool pendel = false) : INachfuehrung
    {
        public string Name => "endlos";
        public int Aufrufe;
        public void Nachfuehren(Kontext k, IReadOnlySet<string> geaendert, IReadOnlySet<string> geloescht)
        {
            Aufrufe++;
            foreach (var id in geaendert)
            {
                var e = k.Arbeit.Finde(id)!.Kopie();
                e.Name = pendel ? (e.Name == "hin" ? "her" : "hin") : "Runde " + Aufrufe;
                k.Setzen(e);
            }
        }
    }

    [Fact]
    public void Pendeln_WirdErkanntUndGemeldet_StattBisZurGrenzeZuRechnen()
    {
        // Gegenprüfung B9: Die Regel setzt den Namen abwechselnd «hin» und «her». Runde 1: (alt → hin), Runde 2: (hin → her), Runde 3: (her → hin),
        // Runde 4: (hin → her) = Runde 2 → Abbruch nach 4 Aufrufen statt 500.
        var p = MitSerie();
        Ok(Modul3Hilfe.Rohr(p, "RO-1", "DN 25", [0, 0, 0], [1000, 0, 0]));
        var k = new Kontext(p, p.Modell.Kopie(), new Herkunft { Art = "mensch", Akteur = "T", Zeit = DateTimeOffset.Now });
        k.Setzen(k.Arbeit.Hole("RO-1").Kopie());
        var regel = new Endlos(pendel: true);
        Nachfuehrungen.Ausfuehren(k, [regel], 500);
        Assert.Equal(4, regel.Aufrufe);
        Assert.Contains(k.Hinweise, h => h.Contains("abgebrochen") && h.Contains("pendeln") && h.Contains("RO-1"));
    }

    [Fact]
    public void Rundengrenze_EndlosRegel_HoertAufUndMeldetEs()
    {
        var p = MitSerie();
        Ok(Modul3Hilfe.Rohr(p, "RO-1", "DN 25", [0, 0, 0], [1000, 0, 0]));
        var k = new Kontext(p, p.Modell.Kopie(), new Herkunft { Art = "mensch", Akteur = "T", Zeit = DateTimeOffset.Now });
        k.Setzen(k.Arbeit.Hole("RO-1").Kopie());
        var regel = new Endlos();
        Nachfuehrungen.Ausfuehren(k, [regel], 12);
        Assert.Equal(12, regel.Aufrufe);
        Assert.Contains(k.Hinweise, h => h.StartsWith("Nachführung nach 12 Runden abgebrochen") && h.Contains("RO-1"));
        Assert.Equal(8, Nachfuehrungen.Rundengrenze(0));
        Assert.Equal(502, Nachfuehrungen.Rundengrenze(500));
        Assert.Equal(2000, Nachfuehrungen.Rundengrenze(100000));
    }
}
