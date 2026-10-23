// Quest App P4a, Nachtrag Brillentest 23.10.2026: Geschoss am Standboden (GeschossWahl; die App zeichnet nur dieses Geschoss mit IFC-Unterlage) und die
// Teilung grosser Netze in Blöcke an Elementgrenzen (NetzBloecke; der Durchgang lädt nur betroffene Blöcke neu).

using Hlks.Xr.Modell;
using Hlks.Xr.Netz;

namespace Hlks.Xr.Tests;

public class GeschossBlockTests
{
    static ListenGeschoss G(string kz, double kote) => new($"id-{kz}", kz, kz, kote, 2800, new ListenTeil("aaaaaaaa", $"/api/xr/geschoss/{kz}.glb?teil=modell", null), null);

    // Test_praxis-artig: 2.UG −5600, 1.UG −2800, EG 0, 1.OG 2900 (Reihenfolge durcheinander)
    static readonly ListenGeschoss[] Liste = [G("EG", 0), G("1.OG", 2900), G("2.UG", -5600), G("1.UG", -2800)];

    [Fact]
    public void Geschoss_am_Standboden_mit_Toleranz_0_3_m()
    {
        Assert.Equal(300, GeschossWahl.ToleranzMm);
        string? Kz(double mm) => GeschossWahl.AmStand(Liste, mm)?.Kennzeichen;
        Assert.Equal("EG", Kz(0));
        Assert.Equal("EG", Kz(2599));        // knapp unter der Grenze zum 1.OG (2900 − 300)
        Assert.Equal("1.OG", Kz(2600));      // genau 0.3 m unter der Kote: schon 1.OG
        Assert.Equal("1.OG", Kz(2900));
        Assert.Equal("1.OG", Kz(50_000));    // über dem obersten: das oberste
        Assert.Equal("EG", Kz(-300));        // Boden etwas unter der Kote (Bodenaufbau) bleibt EG
        Assert.Equal("1.UG", Kz(-301));
        Assert.Equal("2.UG", Kz(-5600));
        Assert.Equal("2.UG", Kz(-9000));     // unter dem untersten: das unterste
        Assert.Null(GeschossWahl.AmStand([], 0));
        Assert.Equal("EG", GeschossWahl.AmStand(Liste, 100, toleranzMm: 0)!.Kennzeichen);
        Assert.Equal("2.UG", GeschossWahl.AmStand(Liste, double.NaN)!.Kennzeichen);
    }

    [Fact]
    public void Standboden_aus_Rig_und_Bezugspunkt()
    {
        Assert.Equal(2900, GeschossWahl.StandbodenMm(2.9, null), 9);
        Assert.Equal(3420, GeschossWahl.StandbodenMm(2.9, [2_600_000, 1_200_000, 520]), 9);
        // ein Rig auf 2.38 m in der Szene mit Bezug 520 mm steht auf 2900 mm: 1.OG
        Assert.Equal("1.OG", GeschossWahl.AmStand(Liste, GeschossWahl.StandbodenMm(2.38, [0, 0, 520]))!.Kennzeichen);
    }

    static List<ElementBereich> Bereiche(params (int Ab, int Anzahl)[] b) => [.. b.Select((x, i) => new ElementBereich($"e{i}", x.Ab, x.Anzahl, "wand"))];

    [Fact]
    public void Kleine_Netze_bleiben_ein_Block()
    {
        Assert.Equal([(0, 500)], NetzBloecke.Teilen(500, Bereiche((0, 500))));
        Assert.Equal([(0, 20_000)], NetzBloecke.Teilen(20_000, []));
        Assert.Empty(NetzBloecke.Teilen(0, []));
    }

    [Fact]
    public void Grosse_Netze_werden_an_Elementgrenzen_geteilt()
    {
        // 10 Elemente zu 7 Dreiecken: Blöcke höchstens 20 Dreiecke → je 2 Elemente (14), nie mitten in einem Element
        var b = Bereiche([.. Enumerable.Range(0, 10).Select(i => (i * 7, 7))]);
        var bloecke = NetzBloecke.Teilen(70, b, max: 20);
        Assert.Equal([(0, 14), (14, 14), (28, 14), (42, 14), (56, 14)], bloecke);
        Assert.All(bloecke, x => Assert.Equal(0, x.Ab % 7));
        // ein Element grösser als ein Block wird in Blöcke zu max geschnitten (Test_praxis EG: ein IFC-Produkt mit 185 480 Dreiecken); sonst an
        // Elementgrenzen; Dreiecke ohne Element (Lücken) dürfen überall geschnitten werden
        var c = NetzBloecke.Teilen(100, Bereiche((5, 50), (60, 10)), max: 20);
        Assert.Equal([(0, 5), (5, 20), (25, 20), (45, 15), (60, 20), (80, 20)], c);
        Assert.All(c, x => Assert.True(x.Anzahl <= 20));
        // lückenlos und der Reihe nach
        foreach (var x in new[] { bloecke, c })
        {
            Assert.Equal(0, x[0].Ab);
            for (int i = 1; i < x.Count; i++) Assert.Equal(x[i - 1].Ab + x[i - 1].Anzahl, x[i].Ab);
        }
        Assert.Equal(100, c[^1].Ab + c[^1].Anzahl);
    }

    [Fact]
    public void Test_praxis_EG_Unterlage_378000_Dreiecke_in_etwa_19_Bloecken()
    {
        // 378 000 Dreiecke in Elementen zu 1 bis 400 Dreiecken
        var zufall = new Random(1);
        var b = new List<ElementBereich>();
        int ab = 0;
        while (ab < 378_000) { var n = Math.Min(378_000 - ab, zufall.Next(1, 400)); b.Add(new ElementBereich($"e{b.Count}", ab, n, "IFCWALL")); ab += n; }
        var bloecke = NetzBloecke.Teilen(378_000, b);
        Assert.InRange(bloecke.Count, 19, 21);
        Assert.All(bloecke, x => Assert.True(x.Anzahl <= NetzBloecke.BlockDreiecke));
        var grenzen = b.Select(x => x.Ab).Append(378_000).ToHashSet();
        Assert.All(bloecke, x => Assert.Contains(x.Ab, grenzen));
    }

    [Fact]
    public void Bereiche_im_Block_und_Indizes_des_Blocks()
    {
        Assert.Equal([(0, 3), (8, 2)], NetzBloecke.Lokal([(8, 5), (18, 4), (30, 2)], 10, 10));   // beschnitten, verschoben; ausserhalb weg
        Assert.Empty(NetzBloecke.Lokal([(0, 10)], 10, 5));
        uint[] ind = [0, 1, 2, 3, 4, 5, 6, 7, 8];
        Assert.Equal([3u, 4, 5, 6, 7, 8], NetzBloecke.Indizes(ind, 1, 2));
    }

    [Fact]
    public void Durchgang_im_Block_wie_im_ganzen_Netz()
    {
        // dasselbe Element im ganzen Netz und im Block: gleiche Dreiecke im Durchgang
        var netz = ModellHilfen.Quadernetz("architektur", ("a", "wand", new Vec3(0, 0, 0), new Vec3(1, 1, 1)), ("b", "wand", new Vec3(2, 0, 0), new Vec3(3, 1, 1)),
            ("c", "wand", new Vec3(4, 0, 0), new Vec3(5, 1, 1)));
        var bloecke = NetzBloecke.Teilen(netz.Dreiecke, netz.Bereiche, max: 12);
        Assert.Equal(3, bloecke.Count);
        var b = netz.Bereiche[1];
        var (_, durchGanz) = NetzDurchgang.Aufteilen(netz.Indizes, [(b.Ab, b.Anzahl)]);
        var blk = bloecke[1];
        var (pos, _, ind) = NetzDurchgang.Ausschnitt(netz.Positionen, netz.Normalen, NetzBloecke.Indizes(netz.Indizes, blk.Ab, blk.Anzahl));
        var (uebrig, durchBlock) = NetzDurchgang.Aufteilen(ind, NetzBloecke.Lokal([(b.Ab, b.Anzahl)], blk.Ab, blk.Anzahl));
        Assert.Empty(uebrig);
        Assert.Equal(durchGanz.Length, durchBlock.Length);
        for (int i = 0; i < durchGanz.Length; i++)
            for (int k = 0; k < 3; k++) Assert.Equal(netz.Positionen[3 * durchGanz[i] + k], pos[3 * durchBlock[i] + k]);

        // ein Element über zwei Blöcke (Blöcke zu 8 Dreiecken): die Teile in beiden Blöcken ergeben zusammen dieselben Dreiecke
        var klein = NetzBloecke.Teilen(netz.Dreiecke, netz.Bereiche, max: 8);
        var e = netz.Bereiche[1];   // Dreiecke 12 … 23
        var teile = new List<float>();
        foreach (var (ab, anzahl) in klein)
        {
            var lokal = NetzBloecke.Lokal([(e.Ab, e.Anzahl)], ab, anzahl);
            if (lokal.Count == 0) continue;
            var (p2, _, i2) = NetzDurchgang.Ausschnitt(netz.Positionen, netz.Normalen, NetzBloecke.Indizes(netz.Indizes, ab, anzahl));
            var (_, d2) = NetzDurchgang.Aufteilen(i2, lokal);
            foreach (var x in d2) teile.AddRange(p2.Skip(3 * (int)x).Take(3));
        }
        Assert.Equal(durchGanz.SelectMany(x => netz.Positionen.Skip(3 * (int)x).Take(3)), teile);
        Assert.Equal(2, klein.Count(x => NetzBloecke.Lokal([(e.Ab, e.Anzahl)], x.Ab, x.Anzahl).Count > 0));
    }
}
