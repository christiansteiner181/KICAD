using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3, B1: Prüfungen P13 (Lage/Richtung verbundener Anschlüsse), P14 (Grössensprung), P15 (Knick ohne Formteil,
/// Polylinie mit Serie, Bogenwinkel nicht in der Serie), P18 (Leitung ohne Serie). Werte FIKTIV (Testfamilien).</summary>
public class LeitungenPruefungTests
{
    static Projekt MitBogenUndRohr(double winkel = 90)
    {
        var p = MitSerie();
        Ok(Formteil(p, "fam-test-bogen", "DN 20", "BO-1", [1000, 2000, 500], winkel: winkel));
        var la = 50 * Math.Tan(winkel / 2 * Math.PI / 180);   // r = 50 (DN 20, da 25)
        Ok(Rohr(p, "RO-1", "DN 20", [500, 2000, 500], [1000 - la, 2000, 500]));
        Ok(p.Ausfuehren("verbinden", new JsonObject { ["von"] = "RO-1.ende", ["nach"] = "BO-1.a" }));
        return p;
    }

    [Fact]
    public void Sauber_KeineBefundeP13BisP15()
    {
        var p = MitBogenUndRohr();
        Assert.DoesNotContain(Pruefungen.Alle(p.Modell, p.Katalog), b => b.Code is "P13" or "P14" or "P15" or "P18");
    }

    [Fact]
    public void P13_NachVerschiebenDesRohrendes()
    {
        // Seit B2 folgt ein Leitungsende seinem Formteil (Nachführung beim Verschieben) – eine Lücke entsteht deshalb,
        // wenn das Rohrende selbst verschoben wird: RO-1 endet 5 mm vor BO-1.a (950 → 945)
        var p = MitBogenUndRohr();
        Ok(p.Ausfuehren("element_aendern", new JsonObject
        {
            ["element"] = "RO-1", ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Pts([500, 2000, 500], [945, 2000, 500]) },
        }));
        var b = Assert.Single(Befunde(p, "P13"));
        Assert.Equal("warnung", b.Schwere);
        Assert.Contains("5 mm auseinander", b.Text);
        Assert.Contains("element_aendern", b.Vorschlag);
    }

    [Fact]
    public void P13_FormteilVerschoben_LeitungsendeFolgt_KeinBefund()
    {
        // B2: BO-1 um 5 mm nach +x → a bei 1005 − 50 = 955, RO-1 endet dort (Nachführung), kein P13
        var p = MitBogenUndRohr();
        var e = Ok(p.Ausfuehren("element_aendern", new JsonObject
        {
            ["element"] = "BO-1", ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([1005, 2000, 500]) },
        }));
        Assert.Contains(e.Hinweise, h => h.StartsWith("RO-1: Ende folgt BO-1.a (5 mm verschoben)"));
        Assert.Empty(Befunde(p, "P13"));
    }

    [Fact]
    public void P13_RichtungNachDrehen()
    {
        var p = MitBogenUndRohr();
        var e = Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "BO-1", ["eigenschaften"] = new JsonObject { ["drehung"] = 10 } }));
        // a = P + R(10°)·(−50, 0, 0) = (950.76, 1991.32): Abstand zur alten Lage 2·50·sin 5° = 8.7 mm; das Rohrende folgt
        Assert.Contains(e.Hinweise, h => h.StartsWith("RO-1: Ende folgt BO-1.a (8.7 mm verschoben)"));
        // RO-1 zeigt danach unter atan(−8.68 / 450.76) = −1.10°, gegenläufig zu a wäre +10° → 11.1° Abweichung
        var p13 = Befunde(p, "P13");
        Assert.Contains(p13, b => b.Text.Contains("nicht gegeneinander") && b.Text.Contains("11.1°"));
        Assert.DoesNotContain(p13, b => b.Text.Contains("auseinander"));
    }

    [Fact]
    public void P14_GroessenwechselOhneReduktion()
    {
        var p = MitBogenUndRohr();
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "RO-1", ["eigenschaften"] = new JsonObject { ["familientyp"] = "DN 25" } }));
        var b = Assert.Single(Befunde(p, "P14"));
        Assert.Equal("fehler", b.Schwere);
        Assert.Contains("DN 25 an DN 20", b.Text);
        Assert.Contains("Reduktion", b.Vorschlag);
    }

    [Fact]
    public void P15_KnickZwischenZweiLeitungen()
    {
        var p = MitSerie(false);
        Ok(Rohr(p, "RO-A", "DN 20", [0, 0, 500], [1000, 0, 500]));
        Ok(Rohr(p, "RO-B", "DN 20", [1000, 0, 500], [2000, 0, 500]));
        Ok(p.Ausfuehren("verbinden", new JsonObject { ["von"] = "RO-A.ende", ["nach"] = "RO-B.anfang" }));
        Assert.Empty(Befunde(p, "P15"));
        Ok(p.Ausfuehren("element_aendern", new JsonObject
        {
            ["element"] = "RO-B", ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Pts([1000, 0, 500], [1000, 1000, 500]) },
        }));
        var b = Assert.Single(Befunde(p, "P15"));
        Assert.Equal("fehler", b.Schwere);
        Assert.Equal("RO-A", b.Element);
        Assert.Contains("Knick von 90°", b.Text);
        Assert.Contains("Bogen", b.Vorschlag);
        Assert.Empty(Befunde(p, "P13"));   // ein Knick zwischen zwei Leitungen ist P15, nicht zusätzlich P13
    }

    [Fact]
    public void P15_PolylinieMitSerie_Hinweis()
    {
        var p = MitSerie(false);
        Ok(Rohr(p, "RO-1", "DN 20", [0, 0, 500], [1000, 0, 500], [1000, 1000, 500], [1000, 2000, 500]));   // ein Knick, ein gerader Zwischenpunkt
        var b = Assert.Single(Befunde(p, "P15"));
        Assert.Equal("hinweis", b.Schwere);
        Assert.Contains("1 Knick ", b.Text);
        Assert.Contains("90°", b.Text);
        Assert.Contains("leitung_aufteilen", b.Vorschlag);
        // ohne Serie: kein P15, aber P18
        Ok(RohrOhneSerie(p, "RO-2", 20, [0, 0, 0], [1000, 0, 0], [1000, 1000, 0]));
        Assert.DoesNotContain(Befunde(p, "P15"), x => x.Element == "RO-2");
    }

    [Fact]
    public void P15_BogenwinkelNichtInDerSerie()
    {
        var p = MitBogenUndRohr(60);   // 60° gibt es in der Testserie nicht (15, 30, 45, 90)
        var b = Assert.Single(Befunde(p, "P15"));
        Assert.Equal("warnung", b.Schwere);
        Assert.Equal("BO-1", b.Element);
        Assert.Contains("60°", b.Text);
        Assert.Contains("15°, 30°, 45°, 90°", b.Text);
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "BO-1", ["eigenschaften"] = new JsonObject { ["parameter"] = new JsonObject { ["winkel"] = 45 } } }));
        Assert.Empty(Befunde(p, "P15"));
    }

    [Fact]
    public void P18_LeitungOhneSerie_Hinweis()
    {
        var p = MitSerie(false);
        Ok(RohrOhneSerie(p, "RO-1", 20, [0, 0, 500], [1000, 0, 500]));
        Ok(Rohr(p, "RO-2", "DN 20", [0, 500, 500], [1000, 500, 500]));
        // je Strang bzw. System zusammengefasst (Entscheid Christian 19.10.2026, Prüfliste 26): RO-1 hat keinen Strang → System
        var b = Assert.Single(Befunde(p, "P18"));
        var sys = p.Modell.Finde(Hole(p, "RO-1").System);
        Assert.Equal(sys?.Kennzeichen ?? "RO-1", b.Element);
        Assert.Contains("RO-1", b.Text);
        Assert.Equal("hinweis", b.Schwere);
        Assert.Contains("bezuege.familie", b.Vorschlag);
        Assert.Contains("leitungsserie", b.Vorschlag);
    }

    [Fact]
    public void P18_JeStrangEineMeldungMitAnzahl()
    {
        var p = MitSerie(false);
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Strang", ["kennzeichen"] = "KW-S9", ["teilVon"] = "KW", ["eigenschaften"] = new JsonObject { ["art"] = "steigstrang" } }));
        Ok(RohrOhneSerie(p, "RO-1", 20, [0, 0, 500], [1000, 0, 500]));
        Ok(RohrOhneSerie(p, "RO-2", 20, [0, 500, 500], [1000, 500, 500]));
        foreach (var kz in new[] { "RO-1", "RO-2" })
            Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = kz, ["teilVon"] = "KW-S9" }));
        var b = Assert.Single(Befunde(p, "P18"));
        Assert.Equal("KW-S9", b.Element);
        Assert.StartsWith("2 Leitungen ohne Serie in KW-S9: RO-1, RO-2", b.Text);
    }

    [Fact]
    public void Beispielprojekt_NurP18Hinweise()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        var alle = Pruefungen.Alle(p.Modell, p.Katalog);
        Assert.DoesNotContain(alle, b => b.Code is "P13" or "P14" or "P15");
        // P18 je Strang (sonst System) zusammengefasst: so viele Meldungen wie Gruppen, die Anzahlen ergeben alle Rohre
        var p18 = alle.Where(b => b.Code == "P18").ToList();
        var gruppen = p.Modell.DerKlasse("Rohr").Select(r => p.Modell.Finde(r.TeilVon) is { } s && p.Katalog.IstArt(s, "Strang") ? s.Kennzeichen : p.Modell.Finde(r.System)?.Kennzeichen ?? r.Kennzeichen).Distinct().Count();
        Assert.Equal(gruppen, p18.Count);
        Assert.Equal(p.Modell.DerKlasse("Rohr").Count(), p18.Sum(b => char.IsDigit(b.Text[0]) ? int.Parse(b.Text.Split(' ')[0]) : 1));
        Assert.All(alle.Where(b => b.Code == "P18"), b => Assert.Equal("hinweis", b.Schwere));
    }

    [Fact]
    public void IfcUebernahme_VerbindetNichtUeberDenBefehl()
    {
        // Entscheid Hauptlauf: der IFC-Import verbindet nach seinen Ports ohne Lageprüfung – er ruft Kontext.Verbinden, nicht verbinden
        var quellen = Directory.GetFiles(Path.Combine(Katalog.StandardOrdner(), "..", "src", "Hlks.Kern", "Import"), "*.cs");
        Assert.NotEmpty(quellen);
        Assert.All(quellen, f => Assert.DoesNotContain("Verbinden.Ausfuehren", File.ReadAllText(f)));
    }
}
