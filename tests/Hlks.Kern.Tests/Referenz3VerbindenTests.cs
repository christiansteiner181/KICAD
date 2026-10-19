using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;
using static Hlks.Kern.Tests.RoutingHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3, B6, Referenzfall 7: verbinden – Lage- und Richtungsprüfung an den Grenzen (Annahme: Abstand ≤ 1 mm, Richtungen
/// gegenläufig bis 1°, Grösse gleich). Zwei Geraden RO-A (1000 mm in +x bis (1000, 0, 1000)) und RO-B (ab dem Versatz in +x).
///
/// Richtungsabweichung: RO-B läuft von (1000, 0, 1000) nach (2000, y, 1000) mit y = 1000·tan(Δ); der Anfang von RO-B zeigt nach
/// −(1, tan Δ), gegenläufig zu +x um genau Δ. tan 0.9° = 0.015709255 → y = 15.709255; tan 1.1° = 0.019200981 → y = 19.200981.</summary>
public class Referenz3VerbindenTests
{
    static Projekt Zwei(double[] anfangB, double[] endeB, string typB = "DN 20")
    {
        var p = MitSerie();
        Ok(Rohr(p, "RO-A", "DN 20", [0, 0, 1000], [1000, 0, 1000]));
        Ok(Rohr(p, "RO-B", typB, anfangB, endeB));
        return p;
    }

    static BefehlsErgebnis Verbinden(Projekt p) =>
        p.Ausfuehren("verbinden", new JsonObject { ["von"] = "RO-A.ende", ["nach"] = "RO-B.anfang" });

    // ------------------------------------------------------------------ Lage

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(0.9)]    // ≤ 1 mm
    [InlineData(-0.9)]   // Lücke und Überlappung gleich
    public void Lage_Bis1mm_Angenommen(double versatz)
    {
        var p = Zwei([1000 + versatz, 0, 1000], [2000, 0, 1000]);
        Ok(Verbinden(p));
        IstVerbunden(p, "RO-A", "ende", "RO-B", "anfang");
    }

    [Theory]
    [InlineData(1.1)]    // > 1 mm
    [InlineData(-1.1)]
    [InlineData(5.0)]
    public void Lage_Ueber1mm_AbgelehntMitVorschlag(double versatz)
    {
        var p = Zwei([1000 + versatz, 0, 1000], [2000, 0, 1000]);
        var vorher = Inhalt(p);
        var e = Nein(Verbinden(p));
        Assert.Contains("mm", e.Fehler);
        Assert.Equal(vorher, Inhalt(p));
        Assert.Empty(p.Modell.Verbindungen);
    }

    /// <summary>Abstand ist 3D: (0.5, 0.5, 0.5) → √0.75 = 0.866 mm angenommen; (0.6, 0.6, 0.6) → √1.08 = 1.039 mm abgelehnt.
    /// Querversatz allein: 0.9 mm in y angenommen, 1.1 mm in z abgelehnt.</summary>
    [Theory]
    [InlineData(0.5, 0.5, 0.5, true)]
    [InlineData(0.6, 0.6, 0.6, false)]
    [InlineData(0.0, 0.9, 0.0, true)]
    [InlineData(0.0, 0.0, 1.1, false)]
    public void Lage_AbstandIst3D(double dx, double dy, double dz, bool angenommen)
    {
        var p = Zwei([1000 + dx, dy, 1000 + dz], [2000, dy, 1000 + dz]);
        var r = Verbinden(p);
        if (angenommen) Ok(r); else Nein(r);
    }

    // ------------------------------------------------------------------ Richtung

    [Theory]
    [InlineData(15.709255)]   // Δ = 0.9° → angenommen
    [InlineData(0.0)]
    public void Richtung_Bis1Grad_Angenommen(double y)
    {
        var p = Zwei([1000, 0, 1000], [2000, y, 1000]);
        Ok(Verbinden(p));
        IstVerbunden(p, "RO-A", "ende", "RO-B", "anfang");
    }

    [Theory]
    [InlineData(19.200981)]   // Δ = 1.1° → abgelehnt
    [InlineData(100.0)]       // Δ = 5.7°
    public void Richtung_Ueber1Grad_AbgelehntMitVorschlag(double y)
    {
        var p = Zwei([1000, 0, 1000], [2000, y, 1000]);
        var vorher = Inhalt(p);
        var e = Nein(Verbinden(p));
        Assert.Contains("°", e.Fehler);
        Assert.Equal(vorher, Inhalt(p));
    }

    /// <summary>Gleiche Lage, aber gleichgerichtet: RO-A.ende zeigt nach aussen +x. RO-D läuft von (1000,0,1000) nach −x; sein Anfang liegt auf
    /// RO-A.ende und zeigt nach aussen ebenfalls +x (0° statt 180°) → abgelehnt.
    /// (Ende–Ende zweier Leitungen scheitert unabhängig von der Lage an der Fliessrichtung: beide «raus».)</summary>
    [Fact]
    public void Richtung_GleichgerichteteEnden_Abgelehnt()
    {
        var p = Zwei([1000, 0, 1000], [2000, 0, 1000]);
        Ok(Rohr(p, "RO-D", "DN 20", [1000, 0, 1000], [0, 0, 1000]));
        Nein(p.Ausfuehren("verbinden", new JsonObject { ["von"] = "RO-A.ende", ["nach"] = "RO-D.anfang" }));
    }

    // ------------------------------------------------------------------ Grösse

    [Fact]
    public void Groesse_Verschieden_Abgelehnt_GleicheAngenommen()
    {
        var p = Zwei([1000, 0, 1000], [2000, 0, 1000], typB: "DN 25");
        var e = Nein(Verbinden(p));
        Assert.Contains("DN", e.Fehler + e.Vorschlag);
        Assert.Empty(p.Modell.Verbindungen);
        var q = Zwei([1000, 0, 1000], [2000, 0, 1000]);
        Ok(Verbinden(q));
    }
}
