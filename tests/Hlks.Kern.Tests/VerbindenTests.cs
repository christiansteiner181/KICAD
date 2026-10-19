using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3, B1: verbinden mit Lage-, Richtungs- und Grössenprüfung (Abstand ≤ 1 mm, gegenläufig ≤ 1°, gleiche Grösse);
/// ohne Lage auf einer Seite logisch wie bisher. Testbogen DN 20, 90° (FIKTIV) bei P = (1000, 2000, 500): a bei (950, 2000, 500)
/// Richtung (−1, 0, 0), b bei (1000, 2050, 500) Richtung (0, 1, 0).</summary>
public class VerbindenTests
{
    static Projekt MitBogen()
    {
        var p = MitSerie();
        Ok(Formteil(p, "fam-test-bogen", "DN 20", "BO-1", [1000, 2000, 500], winkel: 90));
        return p;
    }

    static BefehlsErgebnis Verbinden(Projekt p, string von, string nach, bool trocken = false) =>
        p.Ausfuehren("verbinden", new JsonObject { ["von"] = von, ["nach"] = nach }, trocken);

    [Fact]
    public void Genau_Angenommen()
    {
        var p = MitBogen();
        Ok(Rohr(p, "RO-1", "DN 20", [500, 2000, 500], [950, 2000, 500]));
        Ok(Verbinden(p, "RO-1.ende", "BO-1.a"));
        Assert.NotNull(p.Modell.VerbindungAn(Hole(p, "RO-1").Id, "ende"));
    }

    [Fact]
    public void HalberMillimeter_Angenommen()
    {
        var p = MitBogen();
        Ok(Rohr(p, "RO-1", "DN 20", [500, 2000, 500], [949.5, 2000, 500]));
        Ok(Verbinden(p, "RO-1.ende", "BO-1.a"));
    }

    [Fact]
    public void ZweiMillimeter_Abgelehnt()
    {
        var p = MitBogen();
        Ok(Rohr(p, "RO-1", "DN 20", [500, 2000, 500], [948, 2000, 500]));
        var n = Nein(Verbinden(p, "RO-1.ende", "BO-1.a"));
        Assert.Contains("2 mm auseinander", n.Fehler);
        Assert.Contains("[948, 2000, 500]", n.Fehler);
        Assert.Contains("leitung_zeichnen", n.Vorschlag);
        Assert.Contains("element_aendern", n.Vorschlag);
        Assert.Empty(p.Modell.Verbindungen);
    }

    [Fact]
    public void Gegenrichtung_Abgelehnt()
    {
        var p = MitBogen();
        // Rohr kommt von Süden an a: Ende zeigt nach (0, 1, 0), a nach (−1, 0, 0) → 90° von gegenläufig
        Ok(Rohr(p, "RO-1", "DN 20", [950, 1500, 500], [950, 2000, 500]));
        var n = Nein(Verbinden(p, "RO-1.ende", "BO-1.a"));
        Assert.Contains("nicht gegeneinander", n.Fehler);
        Assert.Contains("90°", n.Fehler);
        Assert.Contains("Bogen", n.Vorschlag);
        // Rohr liegt im Bogen (kommt von +x): Ende zeigt nach (−1, 0, 0) wie a → 180°
        Ok(Rohr(p, "RO-2", "DN 20", [1500, 2000, 500], [950, 2000, 500]));
        Assert.Contains("180°", Nein(Verbinden(p, "RO-2.ende", "BO-1.a")).Fehler);
    }

    [Fact]
    public void DN20anDN25_Abgelehnt_ReduktionEinsetzen()
    {
        var p = MitBogen();
        Ok(Rohr(p, "RO-1", "DN 25", [500, 2000, 500], [950, 2000, 500]));
        var n = Nein(Verbinden(p, "RO-1.ende", "BO-1.a"));
        Assert.Contains("Grössen passen nicht", n.Fehler);
        Assert.Contains("DN 25", n.Fehler); Assert.Contains("DN 20", n.Fehler);
        Assert.Contains("Reduktion einsetzen", n.Vorschlag);
    }

    [Fact]
    public void GeraetOhneFamilie_LogischAngenommen()
    {
        var p = MitSerie(false);
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "WC", ["kennzeichen"] = "WC-1", ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([5000, 5000, 0]) },
        }));
        Ok(Rohr(p, "RO-1", "DN 20", [0, 0, 500], [100, 100, 500]));   // weit weg vom WC: ohne Familie keine Lage
        Ok(Verbinden(p, "RO-1.ende", "WC-1.kw"));
        Assert.Single(p.Modell.Verbindungen);
    }

    [Fact]
    public void NichtPlatziert_LogischAberGroesseGeprueft()
    {
        var p = MitSerie();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Bogen", ["kennzeichen"] = "BO-N", ["system"] = "KW",
            ["eigenschaften"] = new JsonObject { ["familientyp"] = "DN 20" }, ["bezuege"] = new JsonObject { ["familie"] = "FAM-2" },
        }));
        Ok(Rohr(p, "RO-1", "DN 20", [0, 0, 500], [100, 0, 500]));
        Ok(Rohr(p, "RO-2", "DN 25", [0, 500, 500], [100, 500, 500]));
        Ok(Verbinden(p, "RO-1.ende", "BO-N.a"));
        Assert.Contains("Grössen passen nicht", Nein(Verbinden(p, "RO-2.ende", "BO-N.b")).Fehler);
    }

    [Fact]
    public void OhneAnschlussnamen_GiltDasRaeumlichPassendePaar()
    {
        var p = MitBogen();
        Ok(Rohr(p, "RO-1", "DN 20", [500, 2000, 500], [950, 2000, 500]));
        Ok(Rohr(p, "RO-2", "DN 20", [1000, 2500, 500], [1000, 2050, 500]));   // endet an b
        Ok(Verbinden(p, "RO-1", "BO-1"));
        Ok(Verbinden(p, "RO-2", "BO-1"));
        Assert.Equal("a", p.Modell.VerbindungAn(Hole(p, "RO-1").Id, "ende")!.Gegenueber(Hole(p, "RO-1").Id, "ende")!.Anschluss);
        Assert.Equal("b", p.Modell.VerbindungAn(Hole(p, "RO-2").Id, "ende")!.Gegenueber(Hole(p, "RO-2").Id, "ende")!.Anschluss);
        // ohne passende Lage bleibt es mehrdeutig
        Ok(Formteil(p, "fam-test-bogen", "DN 20", "BO-2", [5000, 2000, 500], winkel: 90));
        Ok(Rohr(p, "RO-3", "DN 20", [0, 0, 0], [10, 0, 0]));
        Assert.Contains("Mehrere passende Anschlusspaare", Nein(Verbinden(p, "RO-3", "BO-2")).Fehler);
    }

    [Fact]
    public void ZweiLeitungenGeradeweiter_UndGroessenwechsel()
    {
        var p = MitSerie(false);
        Ok(Rohr(p, "RO-1", "DN 20", [0, 0, 500], [1000, 0, 500]));
        Ok(Rohr(p, "RO-2", "DN 20", [1000, 0, 500], [2000, 0, 500]));
        Ok(Rohr(p, "RO-3", "DN 25", [2000, 0, 500], [3000, 0, 500]));
        Ok(Verbinden(p, "RO-1.ende", "RO-2.anfang"));
        Assert.Contains("DN 20, RO-3.anfang ist DN 25", Nein(Verbinden(p, "RO-2.ende", "RO-3.anfang")).Fehler);
    }

    [Fact]
    public void Trockenlauf_UndRueckgaengig()
    {
        var p = MitBogen();
        Ok(Rohr(p, "RO-1", "DN 20", [500, 2000, 500], [950, 2000, 500]));
        Ok(Verbinden(p, "RO-1.ende", "BO-1.a", trocken: true));
        Assert.Empty(p.Modell.Verbindungen);
        Assert.False(Verbinden(p, "RO-1.ende", "BO-1.b", trocken: true).Angenommen);   // b liegt 50 mm daneben
        Ok(Verbinden(p, "RO-1.ende", "BO-1.a"));
        Assert.Single(p.Modell.Verbindungen);
        Ok(p.Rueckgaengig());
        Assert.Empty(p.Modell.Verbindungen);
        Ok(p.Wiederholen());
        Assert.Single(p.Modell.Verbindungen);
    }

    [Fact]
    public void LeitungZiehen_VonNach_StrengGeprueft()
    {
        var p = MitBogen();
        JsonObject Ziehen(double x) => new()
        {
            ["system"] = "KW", ["dn"] = 20, ["punkte"] = Pts([500, 2000, 500], [x, 2000, 500]), ["nach"] = "BO-1.a", ["geschoss"] = "EG",
        };
        var n = Nein(p.Ausfuehren("leitung_ziehen", Ziehen(945)));
        Assert.Contains("5 mm auseinander", n.Fehler);
        Assert.Empty(p.Modell.DerKlasse("Rohr"));   // ganzer Befehl abgelehnt
        Ok(p.Ausfuehren("leitung_ziehen", Ziehen(950)));
        Assert.Single(p.Modell.Verbindungen);
    }
}
