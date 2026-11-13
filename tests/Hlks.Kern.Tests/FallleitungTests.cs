using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;

namespace Hlks.Kern.Tests;

/// <summary>Fallleitung platzieren (Christian 06.11.2026: «es muss ein werkzeug geben, Fallleitung platzieren», «die fallleitung wird über alle
/// etagen gezeichnet»). Kleines Beispiel (3 Geschosse UG −2700/2700, EG 0/2900, OG 2900/2900) mit der fiktiven Testabwasserserie.</summary>
public class FallleitungTests
{
    static readonly Katalog Kat = Katalog.Laden(Katalog.StandardOrdner());

    [Fact]
    public void UeberAlleGeschosse_JeGeschossEinRohr_Verbunden()
    {
        var p = BeispielKlein.Erzeugen(Kat, null);
        foreach (var f in new[] { "test_abwasserserie", "test_abwasserbogen", "test_abwasser_t", "test_abwasserreduktion" })
            Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Datei(f) }));
        var e = Ok(p.Ausfuehren("fallleitung_platzieren", new JsonObject
        {
            ["punkt"] = new JsonArray(500, 500), ["serie"] = "fam-test-abwasserserie", ["groesse"] = "DN 100",   // ohne system: immer Abwasser (SW)
        }));
        var rohre = p.Modell.DerKlasse("Rohr").ToList();
        Assert.Equal(3, rohre.Count);
        foreach (var (g, zu, zo) in new[] { ("UG", -2700.0, 0.0), ("EG", 0.0, 2900.0), ("OG", 2900.0, 5800.0) })
        {
            var r = Assert.Single(rohre, x => x.Geschoss == Hole(p, g).Id);
            var z = r.Geometrie!.Punkte.Select(q => q[2]).Order().ToList();
            Assert.Equal(zu, z[0], 3);
            Assert.Equal(zo, z[^1], 3);
            Assert.All(r.Geometrie.Punkte, q => { Assert.Equal(500, q[0], 3); Assert.Equal(500, q[1], 3); });
        }
        // von oben nach unten verbunden: OG → EG → UG
        Assert.Equal(2, p.Modell.Verbindungen.Count(v => rohre.Any(r => r.Id == v.Von.Element) && rohre.Any(r => r.Id == v.Nach.Element)));
        Assert.Contains(e.Hinweise, h => h.StartsWith("Fallleitung bei [500, 500] von OG bis UG"));
        Assert.All(rohre, r => Assert.Equal(Hole(p, "SW").Id, r.System));

        // Christian 06.11.2026: «diese wird immer mit 88.5° abgang angeschlossen» – auch ein unter 45° gezeichneter Abgang
        var ab = Ok(p.Ausfuehren("leitung_abzweigen", new JsonObject
        {
            ["leitung"] = rohre.Single(r => r.Geschoss == Hole(p, "EG").Id).Kennzeichen, ["punkt"] = new JsonArray(500, 500, 1500),
            ["richtung"] = new JsonArray(1, 0, 1), ["laenge"] = 800,
        }));
        Assert.Contains(ab.Hinweise, h => h.Contains("gezeichnet 45°, gesetzt 88.5°"));
    }

    [Fact]
    public void NurAbwasser_AnderesSystemAbgelehnt()
    {
        var p = BeispielKlein.Erzeugen(Kat, null);
        Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Datei("test_abwasserserie") }));
        var e = Nein(p.Ausfuehren("fallleitung_platzieren", new JsonObject
        {
            ["punkt"] = new JsonArray(500, 500), ["serie"] = "fam-test-abwasserserie", ["groesse"] = "DN 100", ["system"] = "KW",
        }));
        Assert.Contains("immer Abwasser", e.Fehler);
    }

    /// <summary>Christian 06.11.2026: «weiteres modul steigleitung in gleicher Art für KW, WW, Zirk, VL, RL»: KW und WW in einer Reihe, 100 mm Achsabstand.</summary>
    [Fact]
    public void Steigleitung_KwUndWw_InEinerReihe_Steigend()
    {
        var p = BeispielKlein.Erzeugen(Kat, null);
        Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Datei("test_rohrserie") }));
        JsonObject L(string m) => new() { ["medium"] = m, ["serie"] = "fam-test-rohrserie", ["groesse"] = "DN 20" };
        Ok(p.Ausfuehren("steigleitung_platzieren", new JsonObject
        {
            ["punkt"] = new JsonArray(5000, 2500), ["richtung"] = new JsonArray(0, -1), ["abstand"] = 100, ["leitungen"] = new JsonArray(L("KW"), L("WW")),
        }));
        var rohre = p.Modell.DerKlasse("Rohr").ToList();
        Assert.Equal(6, rohre.Count);
        foreach (var (sys, y) in new[] { ("KW", 2500.0), ("WW", 2400.0) })
        {
            var r = rohre.Where(x => x.System == Hole(p, sys).Id).ToList();
            Assert.Equal(3, r.Count);
            Assert.All(r, x => Assert.All(x.Geometrie!.Punkte, q => { Assert.Equal(5000, q[0], 3); Assert.Equal(y, q[1], 3); }));
            Assert.All(r, x => Assert.True(x.Geometrie!.Punkte[0][2] < x.Geometrie.Punkte[^1][2], $"{x.Kennzeichen} steigt nicht"));   // in Fliessrichtung nach oben
        }
        Assert.Equal(4, p.Modell.Verbindungen.Count(v => rohre.Any(r => r.Id == v.Von.Element) && rohre.Any(r => r.Id == v.Nach.Element)));
        // ohne System für das Medium: Meldung
        var nein = Nein(p.Ausfuehren("steigleitung_platzieren", new JsonObject { ["punkt"] = new JsonArray(6000, 2500), ["abstand"] = 100, ["leitungen"] = new JsonArray(L("HZ-VL")) }));
        Assert.Contains("kein System mit dem Medium HZ-VL", nein.Fehler);
    }

    /// <summary>Christian 06.11.2026: «es soll auch steigleitung lüftung geben AUL, FOL, ZUL, ABL rund und Eckig»: ZUL eckig steigt, ABL rund fällt
    /// (Zentrale unten); mit richtung lässt sich das umkehren (Zentrale oben).</summary>
    [Fact]
    public void Steigleitung_Lueftung_EckigUndRund_RichtungNachZentrale()
    {
        var p = BeispielKlein.Erzeugen(Kat, null);
        foreach (var f in new[] { "test_kanalserie", "test_kanalbogen", "test_rohrserie" }) Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Datei(f) }));
        foreach (var (kz, typ) in new[] { ("ZUL", "Zuluft"), ("ABL", "Abluft") })
            Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = kz, ["eigenschaften"] = new JsonObject { ["systemtyp"] = typ } }));
        Ok(p.Ausfuehren("steigleitung_platzieren", new JsonObject
        {
            ["punkt"] = new JsonArray(6000, 1500), ["abstand"] = 600, ["leitungen"] = new JsonArray(
                new JsonObject { ["medium"] = "ZUL", ["serie"] = "fam-test-kanalserie", ["groesse"] = "400x200" },
                new JsonObject { ["medium"] = "ABL", ["serie"] = "fam-test-rohrserie", ["groesse"] = "DN 50" }),
        }));
        var zul = p.Modell.Elemente.Where(x => x.System == Hole(p, "ZUL").Id && x.Klasse == "Kanal").ToList();
        var abl = p.Modell.Elemente.Where(x => x.System == Hole(p, "ABL").Id && x.Klasse == "Rohr").ToList();
        Assert.Equal(3, zul.Count);
        Assert.Equal(3, abl.Count);
        Assert.All(zul, x => Assert.True(x.Geometrie!.Punkte[0][2] < x.Geometrie.Punkte[^1][2], $"{x.Kennzeichen} steigt nicht"));
        Assert.All(abl, x => Assert.True(x.Geometrie!.Punkte[0][2] > x.Geometrie.Punkte[^1][2], $"{x.Kennzeichen} fällt nicht"));
        Assert.All(abl, x => Assert.Equal(6600, x.Geometrie!.Punkte[0][0], 3));
        // Zentrale oben: ZUL fällt
        Ok(p.Ausfuehren("steigleitung_platzieren", new JsonObject
        {
            ["punkt"] = new JsonArray(7500, 1500), ["abstand"] = 600, ["richtung"] = new JsonArray(0, 1), ["leitungen"] = new JsonArray(
                new JsonObject { ["medium"] = "ZUL", ["serie"] = "fam-test-kanalserie", ["groesse"] = "400x200", ["richtung"] = "ab" }),
        }));
        var oben = p.Modell.Elemente.Where(x => x.Klasse == "Kanal" && x.Geometrie!.Punkte[0][0] > 7000).ToList();
        Assert.Equal(3, oben.Count);
        Assert.All(oben, x => Assert.True(x.Geometrie!.Punkte[0][2] > x.Geometrie.Punkte[^1][2]));
        // Reihe in y: die Breite des eckigen Kanals liegt entlang der Reihe (querDrehung 90), bei der Reihe in x bleibt sie ohne Drehung
        Assert.All(oben, x => Assert.Equal(90, x.Zahl("querDrehung") ?? 0, 3));
        Assert.All(zul, x => Assert.Null(x.Zahl("querDrehung")));
        // kanal_lage = h (Entscheid Christian 13.11.2026, Frage N3): die Höhe liegt entlang der Reihe in x → querDrehung 90
        Ok(p.Ausfuehren("steigleitung_platzieren", new JsonObject
        {
            ["punkt"] = new JsonArray(9000, 1500), ["kanal_lage"] = "h", ["leitungen"] = new JsonArray(
                new JsonObject { ["medium"] = "ZUL", ["serie"] = "fam-test-kanalserie", ["groesse"] = "400x200" }),
        }));
        Assert.All(p.Modell.Elemente.Where(x => x.Klasse == "Kanal" && x.Geometrie!.Punkte[0][0] > 8500), x => Assert.Equal(90, x.Zahl("querDrehung") ?? 0, 3));
    }

    /// <summary>Christian 06.11.2026: «bei allen seteigleitungen soll eine Steigzonen Grösse angegeben werden können. die Rohrabstände sollen daraus
    /// berechnet werden»: KW und WW DN 20 (Testrohrserie da = 25) in einer Steigzone 300 × 200. Seit dem Entscheid Christian 13.11.2026 (Frage N2)
    /// gelten bei Rohren gleiche Achsabstände, auch zum Rand: 300 / 3 = 100 mm (vorher lichter Abstand 83.3 mm).</summary>
    [Fact]
    public void Steigleitung_AbstaendeAusDerSteigzone()
    {
        var p = BeispielKlein.Erzeugen(Kat, null);
        Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Datei("test_rohrserie") }));
        JsonObject L(string m) => new() { ["medium"] = m, ["serie"] = "fam-test-rohrserie", ["groesse"] = "DN 20" };
        var e = Ok(p.Ausfuehren("steigleitung_platzieren", new JsonObject
        {
            ["punkt"] = new JsonArray(5000, 2000), ["steigzone_breite"] = 300, ["steigzone_tiefe"] = 200, ["leitungen"] = new JsonArray(L("KW"), L("WW")),
        }));
        double x(string sys) => p.Modell.DerKlasse("Rohr").First(r => r.System == Hole(p, sys).Id).Geometrie!.Punkte[0][0];
        double y(string sys) => p.Modell.DerKlasse("Rohr").First(r => r.System == Hole(p, sys).Id).Geometrie!.Punkte[0][1];
        Assert.Equal(5100, x("KW"), 3);
        Assert.Equal(5200, x("WW"), 3);
        Assert.Equal(2100, y("KW"), 3);   // Mitte der Tiefe, links der Reihe (Richtung +x)
        Assert.Contains(e.Hinweise, h => h.Contains("Achsabstand 100 mm"));
        var nein = Nein(p.Ausfuehren("steigleitung_platzieren", new JsonObject { ["punkt"] = new JsonArray(7000, 2000), ["steigzone_breite"] = 40, ["leitungen"] = new JsonArray(L("KW"), L("WW")) }));
        Assert.Contains("zu eng für die Rohre", nein.Fehler);
    }
}
