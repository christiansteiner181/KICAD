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
            ["punkt"] = new JsonArray(500, 500), ["serie"] = "fam-test-abwasserserie", ["groesse"] = "DN 100", ["system"] = "SW",
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
    }
}
