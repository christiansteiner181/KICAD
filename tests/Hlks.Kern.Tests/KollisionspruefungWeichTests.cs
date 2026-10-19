using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.KollisionspruefungHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3b, B2: weiche Kollisionen mit Handrechnung – Abstand mit Dämmungshülle unter der Vorgabe (abstand_leitungen für
/// Leitung–Leitung, abstand_weich je Gewerkpaar), fehlende Vorgabe übersprungen und genannt. Masse und Vorgaben FIKTIV
/// (Testrohrserie da = DN + 5: DN 20 → r 12.5).</summary>
public class KollisionspruefungWeichTests
{
    [Fact]
    public void ZweiLeitungen30mm_AbstandLeitungen50_Weich_Mass20()
    {
        var p = MitSerie(false);
        Ok(Rohr(p, "RO-1", "DN 20", [0, 0, 1000], [3000, 0, 1000]));
        Ok(Rohr(p, "RO-2", "DN 20", [0, 55, 1000], [3000, 55, 1000]));       // Achsabstand 55 → Oberflächen 55 − 25 = 30 mm
        // ohne Vorgabe: keine weiche Prüfung – Nachtrag B3c (Entscheid Christian 19.10.2026, keine Abstandsprüfung): kein eigener
        // Hinweis mehr, nur knapp «Abstandsprüfung aus» in der Zusammenfassung
        var ohne = Ok(Pruefen(p));
        Assert.Empty(Kollisionen(p));
        Assert.DoesNotContain(ohne.Hinweise, h => h.Contains("Weiche Prüfung"));
        Assert.EndsWith("Abstandsprüfung aus.", Hinweis(ohne, "Prüflauf 1"));
        Assert.Contains("Keine Projektvorgaben", Hinweis(ohne, "VG-1"));
        // nur toleranz_hart gesetzt: weiterhin ohne weiche Prüfung, ohne Hinweis
        Ok(Vorgaben(p, (Projektvorgaben.ToleranzHart, 1)));
        var nurTol = Ok(Pruefen(p));
        Assert.DoesNotContain(nurTol.Hinweise, h => h.Contains("Weiche Prüfung"));
        Assert.Single(nurTol.Hinweise, h => h.Contains("Abstandsprüfung aus"));
        Ok(Vorgaben(p, (Projektvorgaben.AbstandLeitungen, 50)));
        var e = Ok(Pruefen(p));
        var k = Assert.Single(Kollisionen(p));
        Assert.Equal("weich", k.Text("art"));
        Assert.Equal("kontakt", k.Text("typ"));
        Assert.Equal(20, k.Zahl("mass"));                                   // 50 − 30
        Assert.Contains("abstand_leitungen 50 mm (Quelle: FIKTIV", k.Eigenschaft("mass")!.Herkunft.Quelle);
        Assert.Contains("Abstand 30 mm", k.Eigenschaft("mass")!.Herkunft.Quelle);
        Assert.Equal([0, 27.5, 1000], Lage(k));                             // Mitte zwischen den nächsten Punkten
        Assert.Contains("weich 1", Hinweis(e, "  hlks-hlks:"));
        // weich = false: keine weiche Prüfung, die weiche Kollision bleibt unverändert (nicht behoben)
        var aus = Ok(Pruefen(p, new JsonObject { ["weich"] = false }));
        Assert.Equal("neu", Hole(p, k.Kennzeichen).Text("status"));
        Assert.Contains("weich = false", Hinweis(aus, "ausgeschaltet"));
    }

    [Fact]
    public void Daemmung_MachtAusGetrenntWeich_UeberschneidungZaehltZumMass()
    {
        var p = MitSerie(false);
        Ok(Rohr(p, "RO-3", "DN 20", [0, 0, 1000], [3000, 0, 1000]));
        Ok(Rohr(p, "RO-4", "DN 20", [0, 60, 1000], [3000, 60, 1000]));       // Oberflächen 60 − 25 = 35 mm
        Ok(Vorgaben(p, (Projektvorgaben.AbstandLeitungen, 30)));
        Ok(Pruefen(p));
        Assert.Empty(Kollisionen(p));                                       // 35 ≥ 30: getrennt
        Daemmung(p, "RO-3", 10);                                            // 35 − 10 = 25 < 30 → weich, Mass 5
        Ok(Pruefen(p));
        var k = Assert.Single(Kollisionen(p));
        Assert.Equal(("weich", 5.0), (k.Text("art"), k.Zahl("mass")!.Value));
        Assert.Contains("mit Dämmung 25 mm", k.Eigenschaft("mass")!.Herkunft.Quelle);
        Daemmung(p, "RO-3", 20); Daemmung(p, "RO-4", 20);                  // Hüllen r 32.5: 60 − 65 = −5 → Mass 30 + 5 = 35
        Ok(Pruefen(p));
        var k2 = Assert.Single(Kollisionen(p));
        Assert.Equal(k.Kennzeichen, k2.Kennzeichen);                        // dieselbe Kollision (gleiche Beteiligte und Lage)
        Assert.Equal(("weich", 35.0), (k2.Text("art"), k2.Zahl("mass")!.Value));
        Assert.Contains("Abstand mit Dämmung -5 mm", k2.Eigenschaft("mass")!.Herkunft.Quelle);
    }

    [Fact]
    public void AbstandWeich_JeGewerkpaar_MitHlksAlsErsatz_FehlendeGenannt()
    {
        var p = Wandprojekt();
        // Rohr 10 mm vor der Innenseite von W-1 (y = 100): Achse y = 100 + 12.5 + 10 = 122.5
        Ok(Rohr(p, "RO-1", "DN 20", [1000, 122.5, 1000], [2000, 122.5, 1000]));
        Ok(Vorgaben(p, (Projektvorgaben.AbstandLeitungen, 50)));            // weich aktiv, aber ohne abstand_weich
        var e = Ok(Pruefen(p));
        Assert.Empty(Kollisionen(p));
        Assert.Contains("abstand_weich «architektur-sanitaer»", Hinweis(e, "Weiche Prüfung übersprungen"));
        // «hlks-architektur» gilt für jedes HLKS-Gewerk: 25 − 10 = 15
        Ok(Vorgaben(p, (Projektvorgaben.AbstandWeich, new JsonObject { ["hlks-architektur"] = 25 })));
        Ok(Pruefen(p));
        var k = Assert.Single(Kollisionen(p));
        Assert.Equal(15, k.Zahl("mass"));
        Assert.Contains("abstand_weich «architektur-hlks» 25 mm", k.Eigenschaft("mass")!.Herkunft.Quelle);
        // der genaue Schlüssel geht vor: «sanitaer-architektur» 20 → 20 − 10 = 10
        Ok(Vorgaben(p, (Projektvorgaben.AbstandWeich, new JsonObject { ["hlks-architektur"] = 25, ["sanitaer-architektur"] = 20 })));
        Ok(Pruefen(p));
        Assert.Equal(10, Assert.Single(Kollisionen(p)).Zahl("mass"));
    }

    [Fact]
    public void AbstandWeichFuer_Suchreihenfolge()
    {
        var p = MitSerie(false);
        Ok(Vorgaben(p, (Projektvorgaben.AbstandWeich, new JsonObject { ["heizung-sanitaer"] = 40, ["hlks-sanitaer"] = 30, ["hlks-hlks"] = 20, ["fremd-hlks"] = 10 })));
        var v = Projektvorgaben.Lesen(p.Modell);
        Assert.Equal((40.0, "heizung-sanitaer"), v.AbstandWeichFuer("sanitaer", true, "heizung", true));   // genau, Reihenfolge egal
        Assert.Equal((30.0, "hlks-sanitaer"), v.AbstandWeichFuer("lueftung", true, "sanitaer", true));     // lueftung → hlks
        Assert.Equal((20.0, "hlks-hlks"), v.AbstandWeichFuer("lueftung", true, "heizung", true));
        Assert.Equal((10.0, "fremd-hlks"), v.AbstandWeichFuer("heizung", true, "fremd", false));
        Assert.Null(v.AbstandWeichFuer("heizung", true, "architektur", false));
        Assert.Equal("architektur-sanitaer", Projektvorgaben.Schluessel(" Sanitaer - Architektur "));
        Assert.Null(Projektvorgaben.Schluessel("sanitaer"));
    }
}
