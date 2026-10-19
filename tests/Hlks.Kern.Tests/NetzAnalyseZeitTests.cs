using System.Diagnostics;

namespace Hlks.Kern.Tests;

[CollectionDefinition("NetzAnalyse Zeit", DisableParallelization = true)]
public class NetzAnalyseZeitSammlung;

/// <summary>Zeitbedarf der Netzanalyse (Modul 2, Auftrag B3): 200 000 Dreiecke unter 5 s. Läuft allein (keine parallelen
/// Tests), damit die Messung nicht von anderen Tests abhängt.</summary>
[Collection("NetzAnalyse Zeit")]
public class NetzAnalyseZeitTests(Xunit.Abstractions.ITestOutputHelper ausgabe)
{
    [Fact]
    public void Netz_mit_200000_Dreiecken_unter_5_Sekunden()
    {
        // Quader mit feinem Raster (6 × 2 × 100² = 120 000) und 16 Zapfen mit 256 Segmenten × 10 + Deckel (≈ 86 000)
        var b = new NetzAnalyseTests.Bauer();
        b.Raster([0, 0, 0], [0, 400, 0], [400, 0, 0], 100).Raster([0, 0, 200], [400, 0, 200], [0, 400, 200], 100)
         .Raster([0, 0, 0], [400, 0, 0], [0, 0, 200], 100).Raster([0, 400, 0], [0, 400, 200], [400, 400, 0], 100)
         .Raster([0, 0, 0], [0, 0, 200], [0, 400, 0], 100).Raster([400, 0, 0], [400, 400, 0], [400, 0, 200], 100);
        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 4; j++)
            {
                double[] m = [50 + 100 * i, 50 + 100 * j, 200];
                b.Zylinder(m, [0, 0, 1], 20, 40, 256, true, 10).Scheibe([m[0], m[1], 240], [0, 0, 1], 20, 256);
            }
        while (b.AnzahlDreiecke < 200_000) b.Viereck([0, 0, -1], [1, 0, -1], [1, 1, -1], [0, 1, -1]);
        var netz = b.Netz();
        Assert.True(netz.Dreiecke.Length / 3 >= 200_000);
        NetzAnalyse.Analysieren([new NetzAnalyseTests.Bauer().Quader(0, 0, 0, 1, 1, 1).Netz()]); // JIT aufwärmen
        var uhr = Stopwatch.StartNew();
        var e = NetzAnalyse.Analysieren([netz]);
        var bf = NetzAnalyse.Befunde(e, "gross.stp", "0123456789abcdef", "step 1");
        uhr.Stop();
        ausgabe.WriteLine($"{netz.Dreiecke.Length / 3} Dreiecke: Analyse und Befunde {uhr.Elapsed.TotalSeconds:0.00} s");
        Assert.True(uhr.Elapsed.TotalSeconds < 5, $"Analyse dauerte {uhr.Elapsed.TotalSeconds:0.00} s");
        Assert.Equal(16, e.Stutzen.Count(s => s.Art == "zapfen"));
        Assert.Contains(bf, x => x.Art == "ansicht");
    }
}
