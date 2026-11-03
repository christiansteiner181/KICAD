namespace Hlks.Kern.Tests;

/// <summary>Leitung ab Rolle (FBH, Christian 03.11.2026: «minimaler biegeradius 10cm»): Knicke als Kreisbogen mit dem Biegeradius der Serie.</summary>
public class LeitungAbRolleTests
{
    [Fact]
    public void Knick_90Grad_wird_Kreisbogen_mit_dem_Biegeradius_bei_kurzen_Strecken_kleiner()
    {
        var g = HlksKoerper.Gebogen([[0, 0, 0], [1000, 0, 0], [1000, 1000, 0]], 100);
        Assert.Equal([0.0, 0, 0], g[0]);
        Assert.Equal([1000.0, 1000, 0], g[^1]);
        var bogen = g.Skip(1).SkipLast(1).ToList();
        Assert.Equal(900, bogen[0][0], 6); Assert.Equal(0, bogen[0][1], 6);
        Assert.Equal(1000, bogen[^1][0], 6); Assert.Equal(100, bogen[^1][1], 6);
        Assert.All(bogen, q => Assert.Equal(100, Math.Sqrt((q[0] - 900) * (q[0] - 900) + (q[1] - 100) * (q[1] - 100)), 6));

        // Strecken 100 mm: der Bogen nimmt höchstens die halbe Strecke, Radius 50
        var k = HlksKoerper.Gebogen([[0, 0, 0], [100, 0, 0], [100, 100, 0]], 100).Skip(1).SkipLast(1).ToList();
        Assert.All(k, q => Assert.Equal(50, Math.Sqrt((q[0] - 50) * (q[0] - 50) + (q[1] - 50) * (q[1] - 50)), 6));
    }
}
