using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;
using static Hlks.Kern.Tests.RoutingHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3, B2: leitung_abzweigen (T-Stück, Teilen, Reduktion an c) und Anschluss an Geräte (von/nach) mit Reduktion.
/// Handrechnungen aus den FIKTIVEN Testfamilien: T-Stück l = lc = da (DN 32: 37 mm, DN 25: 30 mm), Reduktion l = 40 mm.</summary>
public class RoutingAbzweigTests
{
    static BefehlsErgebnis Abzweigen(Projekt p, JsonObject par, bool trocken = false) => p.Ausfuehren("leitung_abzweigen", par, trocken);

    [Fact]
    public void AbzweigDN32_MitAbzweigDN25_ReduktionAnC()
    {
        var p = MitSerie();
        Ok(Zeichnen(p, "DN 32", [[0, 0, 1000], [4000, 0, 1000]]));
        var e = Ok(Abzweigen(p, new JsonObject
        {
            ["leitung"] = "RO-1", ["punkt"] = Punkte([1500, 0, 1000])[0]!.DeepClone(), ["richtung"] = new JsonArray(0, 1, 0), ["laenge"] = 1000, ["groesse"] = "DN 25",
        }));
        // T-Stück DN 32: l = lc = da = 37 mm
        Gerade(p, "RO-1", [0, 0, 1000], [1463, 0, 1000]);        // 1500 − 37 = 1463 mm
        Gerade(p, "RO-1-2", [1537, 0, 1000], [4000, 0, 1000]);   // 4000 − 1537 = 2463 mm
        Etwa(2463, Laenge(p, "RO-1-2"));
        var ts = Hole(p, "TS-1");
        Etwa([1500, 0, 1000], Familien.Einfuegepunkt(ts));
        Assert.Equal("DN 32", Familien.Typ(ts));   // Testserie: nur gleiche T-Stücke → T der Hauptgrösse
        Etwa([1500, 37, 1000], Stelle(p, "TS-1", "c").Lage);
        // Reduktion 32/25 an c: a (DN 32) bei c, b (DN 25) 40 mm weiter
        var rd = Hole(p, "RD-1");
        Assert.Equal("DN 32/25", Familien.Typ(rd));
        Etwa([1500, 37, 1000], Stelle(p, "RD-1", "a").Lage);
        Etwa([1500, 77, 1000], Stelle(p, "RD-1", "b").Lage);     // 37 + 40 = 77
        Gerade(p, "RO-2", [1500, 77, 1000], [1500, 1000, 1000]);  // 1000 − 77 = 923 mm
        Assert.Equal("DN 25", Familien.Typ(Hole(p, "RO-2")));
        IstVerbunden(p, "RO-1", "ende", "TS-1", "a");
        IstVerbunden(p, "TS-1", "b", "RO-1-2", "anfang");
        IstVerbunden(p, "TS-1", "c", "RD-1", "a");
        IstVerbunden(p, "RD-1", "b", "RO-2", "anfang");
        Sauber(p);
        Assert.Contains(e.Hinweise, h => h.Contains("RO-1 geteilt in RO-1 (1463 mm) und RO-1-2 (2463 mm)"));
        // RO-1-2 übernimmt Serie, Grösse und System
        var r2 = Hole(p, "RO-1-2");
        Assert.Equal("DN 32", Familien.Typ(r2));
        Assert.Equal(Hole(p, "KW").Id, r2.System);
    }

    [Fact]
    public void Abzweig_GleicheGroesse_UmhaengenDerVerbindungAmEnde()
    {
        var p = MitSerie();
        Ok(Zeichnen(p, "DN 32", [[0, 0, 1000], [4000, 0, 1000], [4000, 2000, 1000]]));   // RO-1 – BO-1 – RO-2
        IstVerbunden(p, "RO-1", "ende", "BO-1", "a");
        Ok(Abzweigen(p, new JsonObject { ["leitung"] = "RO-1", ["punkt"] = new JsonArray(2000, 0, 1000), ["punkte"] = Punkte([2000, -1500, 1000]) }));
        // Abzweig nach −y: c bei (2000, −37, 1000), Gerade bis (2000, −1500, 1000) = 1463 mm
        Gerade(p, "RO-3", [2000, -37, 1000], [2000, -1500, 1000]);
        IstVerbunden(p, "TS-1", "c", "RO-3", "anfang");
        // die Verbindung RO-1.ende – BO-1.a hängt jetzt am zweiten Teilstück
        IstVerbunden(p, "RO-1-2", "ende", "BO-1", "a");
        Gerade(p, "RO-1-2", [2037, 0, 1000], [3926, 0, 1000]);   // Bogen DN 32: La = 2·37 = 74 → 4000 − 74 = 3926
        Sauber(p);
        // T-Stück: x = Leitung (1, 0, 0), y = Abzweig (0, −1, 0) → z = x × y = (0, 0, −1)
        var l = Familien.Einbaulage(Hole(p, "TS-1"))!;
        Etwa([1, 0, 0], l.X.Feld(), 1e-9); Etwa([0, 0, -1], l.Z.Feld(), 1e-9);
    }

    [Fact]
    public void Abzweig_ZuNahAmEnde_Schraeg_Polylinie_Abgelehnt()
    {
        var p = MitSerie();
        Ok(Zeichnen(p, "DN 32", [[0, 0, 1000], [4000, 0, 1000]]));
        var vorher = Inhalt(p);
        var e = Nein(Abzweigen(p, new JsonObject { ["leitung"] = "RO-1", ["punkt"] = new JsonArray(20, 0, 1000), ["richtung"] = new JsonArray(0, 1, 0), ["laenge"] = 500 }));
        Assert.Contains("passt es nicht", e.Fehler);
        Assert.Contains("zwischen 38 und 3962 mm", e.Vorschlag);   // 37 + 1 bzw. 4000 − 37 − 1
        e = Nein(Abzweigen(p, new JsonObject { ["leitung"] = "RO-1", ["punkt"] = new JsonArray(2000, 0, 1000), ["richtung"] = new JsonArray(1, 1, 0), ["laenge"] = 500 }));
        Assert.Contains("unter 45°", e.Fehler);
        Assert.Equal(vorher, Inhalt(p));
        Ok(RohrOhneSerie(p, "RO-ALT", 20, [0, 1000, 0], [1000, 1000, 0], [1000, 2000, 0]));
        e = Nein(Abzweigen(p, new JsonObject { ["leitung"] = "RO-ALT", ["punkt"] = new JsonArray(500, 1000, 0), ["richtung"] = new JsonArray(0, 1, 0) }));
        Assert.Contains("leitung_aufteilen", e.Vorschlag);
    }

    [Fact]
    public void Abzweig_OhneVerlauf_NurTStueck_DannZeichnenAbC()
    {
        var p = MitSerie();
        Ok(Zeichnen(p, "DN 20", [[0, 0, 500], [3000, 0, 500]]));
        var e = Ok(Abzweigen(p, new JsonObject { ["leitung"] = "RO-1", ["punkt"] = new JsonArray(1000, 0), ["richtung"] = new JsonArray(0, 0, 1) }));   // ohne z: Lage im Grundriss
        Assert.Contains(e.Hinweise, h => h.Contains("TS-1.c ist frei"));
        Etwa([1000, 0, 525], Stelle(p, "TS-1", "c").Lage);   // lc = 25 nach oben
        Ok(Zeichnen(p, "DN 20", [[1000, 0, 525], [1000, 0, 2500]], new JsonObject { ["von"] = "TS-1.c" }));
        Gerade(p, "RO-2", [1000, 0, 525], [1000, 0, 2500]);   // RO-1-2 zählt nicht als «RO-n»
        IstVerbunden(p, "TS-1", "c", "RO-2", "anfang");
        Sauber(p);
    }

    [Fact]
    public void Abzweig_Schmutzwasser_FliesstZumTStueckHin()
    {
        var p = MitSerie();
        MitSystem(p, "SW", "Schmutzwasser");
        Ok(Zeichnen(p, "DN 50", [[0, 0, 0], [4000, 0, 0]], system: "SW"));
        Ok(Abzweigen(p, new JsonObject { ["leitung"] = "RO-1", ["punkt"] = new JsonArray(2000, 0, 0), ["richtung"] = new JsonArray(0, 1, 0), ["laenge"] = 1000 }));
        // Entsorgung: der Abzweig fliesst zum T-Stück hin → Anfang weit weg, Ende an c
        Gerade(p, "RO-2", [2000, 1000, 0], [2000, 55, 0]);   // lc = da = 55 (DN 50)
        IstVerbunden(p, "RO-2", "ende", "TS-1", "c");
    }

    [Fact]
    public void AnschlussAnHeizkoerper_DN15_Reduktion_bzwAblehnung()
    {
        var p = MitSerie();
        MitSystem(p, "HZV", "Heizung Vorlauf");
        Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Datei("test_heizkoerper") }));   // FAM-5
        Ok(p.Ausfuehren("familie_platzieren", new JsonObject { ["familie"] = "fam-test-heizkoerper", ["typ"] = "22-600-1000", ["punkt"] = new JsonArray(0, 0, 0), ["geschoss"] = "EG", ["kennzeichen"] = "HK-1" }));
        // vl: (laenge − randabstand, tiefe/2, bodenabstand − stutzen) = (1000 − 50, 100/2, 150 − 30) = (950, 50, 120), Richtung (0, 0, −1), DN 15
        Etwa([950, 50, 120], Stelle(p, "HK-1", "vl").Lage);
        var vorher = Inhalt(p);
        // DN 32 → Reduktion 32/15 gibt es nicht → abgelehnt mit Vorschlag
        var nein = Nein(Zeichnen(p, "DN 32", [[3000, 50, -500], [950, 50, -500], [950, 50, 120]], new JsonObject { ["nach"] = "HK-1.vl" }, system: "HZV"));
        Assert.Contains("DN 32", nein.Fehler); Assert.Contains("DN 15", nein.Fehler);
        Assert.Contains("zwei Schritten", nein.Vorschlag);
        Assert.Equal(vorher, Inhalt(p));
        // DN 20 → Reduktion 20/15 direkt am Anschluss
        Ok(Zeichnen(p, "DN 20", [[3000, 50, -500], [950, 50, -500], [950, 50, 120]], new JsonObject { ["nach"] = "HK-1.vl" }, system: "HZV"));
        Gerade(p, "RO-1", [3000, 50, -500], [1000, 50, -500]);   // Bogen DN 20: 950 + 50 = 1000 → 2000 mm
        Gerade(p, "RO-2", [950, 50, -450], [950, 50, 80]);       // −500 + 50 = −450 bis 120 − 40 = 80 → 530 mm
        Etwa([950, 50, 80], Stelle(p, "RD-1", "a").Lage);        // a (DN 20) stromauf
        Etwa([950, 50, 120], Stelle(p, "RD-1", "b").Lage);       // b (DN 15) am Anschluss
        Assert.Equal("DN 20/15", Familien.Typ(Hole(p, "RD-1")));
        IstVerbunden(p, "RD-1", "b", "HK-1", "vl");
        IstVerbunden(p, "RO-2", "ende", "RD-1", "a");
        Sauber(p);
    }

    [Fact]
    public void AnschlussAnHeizkoerper_GleicheGroesse_ErsterPunktVorangestellt_RichtungGeprueft()
    {
        var p = MitSerie();
        MitSystem(p, "HZR", "Heizung Rücklauf");
        Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Datei("test_heizkoerper") }));
        Ok(p.Ausfuehren("familie_platzieren", new JsonObject { ["familie"] = "fam-test-heizkoerper", ["typ"] = "22-600-1000", ["punkt"] = new JsonArray(0, 0, 0), ["geschoss"] = "EG", ["kennzeichen"] = "HK-1" }));
        // rl bei (900, 50, 120), Richtung (0, 0, −1); der Verlauf beginnt weiter unten auf der Achse → Anschlusslage vorangestellt
        var e = Ok(Zeichnen(p, "DN 15", [[900, 50, -300], [3000, 50, -300]], new JsonObject { ["von"] = "HK-1.rl" }, system: "HZR"));
        Assert.Contains(e.Hinweise, h => h.Contains("Anfang an HK-1.rl gesetzt"));
        Gerade(p, "RO-1", [900, 50, 120], [900, 50, -260]);   // Bogen DN 15: r = 2·20 = 40 → −300 + 40 = −260
        IstVerbunden(p, "HK-1", "rl", "RO-1", "anfang");
        Sauber(p);
        // Verlauf nicht auf der Achse des Anschlusses → abgelehnt mit Punkt auf der Achse
        p = MitSerie();
        MitSystem(p, "HZR", "Heizung Rücklauf");
        Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Datei("test_heizkoerper") }));
        Ok(p.Ausfuehren("familie_platzieren", new JsonObject { ["familie"] = "fam-test-heizkoerper", ["typ"] = "22-600-1000", ["punkt"] = new JsonArray(0, 0, 0), ["geschoss"] = "EG", ["kennzeichen"] = "HK-1" }));
        var nein = Nein(Zeichnen(p, "DN 15", [[900, 50, 120], [3000, 50, 120]], new JsonObject { ["von"] = "HK-1.rl" }, system: "HZR"));
        Assert.Contains("unter 90°", nein.Fehler);
        Assert.Contains("[900, 50, -1980]", nein.Vorschlag);   // Lage + Richtung · 2100 mm
    }

    [Fact]
    public void AnschlussOhneLage_GeraetOhneFamilie_LogischVerbunden()
    {
        var p = MitSerie();
        MitSystem(p, "HZV", "Heizung Vorlauf");
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Heizkoerper", ["kennzeichen"] = "HK-9", ["geschoss"] = "EG",
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Punkte([5000, 0, 0]) },
        }));
        Ok(Zeichnen(p, "DN 20", [[0, 0, 300], [5000, 0, 300]], new JsonObject { ["nach"] = "HK-9.vl" }, system: "HZV"));
        Gerade(p, "RO-1", [0, 0, 300], [5000, 0, 300]);   // Punkte bleiben, keine Lage am Gerät
        IstVerbunden(p, "RO-1", "ende", "HK-9", "vl");
    }

    [Fact]
    public void Abzweig_Trockenlauf_UndRueckgaengig()
    {
        var p = MitSerie();
        Ok(Zeichnen(p, "DN 32", [[0, 0, 1000], [4000, 0, 1000]]));
        var vorher = Inhalt(p);
        var par = new JsonObject { ["leitung"] = "RO-1", ["punkt"] = new JsonArray(1500, 0, 1000), ["richtung"] = new JsonArray(0, 1, 0), ["laenge"] = 1000, ["groesse"] = "DN 25" };
        var t = Ok(Abzweigen(p, par, trocken: true));
        Assert.Equal(vorher, Inhalt(p));
        Assert.Contains(t.Hinweise, h => h.StartsWith("Reduktion RD-1 Typ «DN 32/25»"));
        Ok(Abzweigen(p, (JsonObject)par.DeepClone()));
        Assert.NotEqual(vorher, Inhalt(p));
        Ok(p.Rueckgaengig());
        Assert.Equal(vorher, Inhalt(p));
        Gerade(p, "RO-1", [0, 0, 1000], [4000, 0, 1000]);
    }
}
