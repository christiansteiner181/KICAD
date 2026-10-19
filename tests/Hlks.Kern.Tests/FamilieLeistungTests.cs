using System.Diagnostics;
using System.Text.Json.Nodes;
using Xunit.Abstractions;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.FamilieTestHilfe;

namespace Hlks.Kern.Tests;

[CollectionDefinition("FamilieZeit", DisableParallelization = true)]
public class FamilieZeitSammlung;

/// <summary>Modul 2, B1: Leistung der Familiengrafik – 500 platzierte Familienelemente, Grafik.Koerper und Grundriss je unter
/// 1 s. Läuft allein (keine parallelen Tests), damit die Messung nicht von anderen Tests abhängt.</summary>
[Collection("FamilieZeit")]
public class FamilieLeistungTests(ITestOutputHelper aus)
{
    [Fact]
    public void Leistung_500Familienelemente_KoerperUndGrundrissJeUnterEinerSekunde()
    {
        var p = MitHeizkoerper();
        // Aufwärmen (JIT) an einem kleinen Stand
        Grafik.Koerper(p); Grafik.Plan(p, "AN-EG");
        // 500 Elemente als Kopien von HK-1 (direkt über Operationen, damit der Aufbau den Test nicht dominiert)
        var vorlage = Hole(p, "HK-1");
        var ops = new List<Operation>();
        for (int i = 0; i < 500; i++)
        {
            var e = vorlage.Kopie();
            e.Id = Guid.NewGuid().ToString(); e.Kennzeichen = $"HK-{i + 2}"; e.Version = 0;
            e.Geometrie!.Punkte[0] = [(i % 25) * 1500.0, (i / 25) * 1500.0, 0];
            e.Eigenschaften["familientyp"] = new Wert { Inhalt = JsonValue.Create(i % 3 == 0 ? "22-600-1000" : i % 3 == 1 ? "22-600-1400" : "22-900-800") };
            if (i % 7 == 0) e.Eigenschaften["parameter"] = new Wert { Inhalt = new JsonObject { ["bodenabstand"] = 100 + i % 4 * 50 } };
            ops.Add(new Operation { Art = "element.setzen", Id = e.Id, Basis = 0, Element = e });
        }
        p.Modell.Anwenden(ops);
        Assert.Equal(501, p.Modell.DerKlasse("Heizkoerper").Count());

        // drei Läufe, der erste mit leeren Zwischenspeichern der neuen Typ-/Exemplarwerte; geprüft wird der Median (wie
        // SchnittTests: parallele Bauläufe belasten die Maschine). Grafik.Plan enthält Modell.Pruefsumme() (gut 2/3 der Zeit).
        var tKoerper = new double[3]; var tPlan = new double[3];
        List<Koerper> koerper = []; Plangrafik plan = new();
        for (int i = 0; i < 3; i++)
        {
            var uhr = Stopwatch.StartNew();
            koerper = Grafik.Koerper(p);
            tKoerper[i] = uhr.Elapsed.TotalMilliseconds;
            uhr.Restart();
            plan = Grafik.Plan(p, "AN-EG");
            tPlan[i] = uhr.Elapsed.TotalMilliseconds;
        }
        aus.WriteLine($"Grafik.Koerper 501 Familienelemente: {string.Join(" / ", tKoerper.Select(t => t.ToString("0")))} ms; " +
                      $"Grundriss: {string.Join(" / ", tPlan.Select(t => t.ToString("0")))} ms (erster Lauf / Wiederholungen)");
        Assert.Equal(501, koerper.Count(k => k.Ebene == "apparat"));
        Assert.Equal(501, plan.Teile.Count(t => t.Ebene == "apparat"));
        double Median(double[] x) => x.Order().ElementAt(1);
        Assert.True(Median(tKoerper) < 1000, $"Grafik.Koerper {Median(tKoerper):0} ms");
        Assert.True(Median(tPlan) < 1000, $"Grundriss {Median(tPlan):0} ms");
    }
}
