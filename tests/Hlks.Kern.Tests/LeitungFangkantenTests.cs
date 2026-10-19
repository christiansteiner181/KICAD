using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3, C3: Fangkanten der Leitungen im Grundriss. Die Plangrafik liefert für eine Leitung die Achse als Fangkanten
/// (je Teilstück eine), mit und ohne Serie – so rasten Werkzeuge auf der Achse ein (Schnittpunkt, Mitte, Lot, nächster Punkt), nicht
/// nur auf den Enden. Werte FIKTIV (Testrohrserie).</summary>
public class LeitungFangkantenTests
{
    static Grafikteil Teil(Projekt p, string kz, string ansicht = "AN-EG") =>
        Grafik.Plan(p, ansicht).Teile.Single(t => t.Id == Hole(p, kz).Id);

    [Fact]
    public void RohrMitSerie_AchseIstFangkante_Fangpunkteunveraendert()
    {
        var p = MitSerie(false);
        Ok(Rohr(p, "RO-1", "DN 20", [0, 0, 500], [3000, 0, 500]));
        var t = Teil(p, "RO-1");
        var kante = Assert.Single(t.Fangkanten!);
        Etwa([0, 0], kante[0]); Etwa([3000, 0], kante[1]);
        Assert.Equal(2, t.Fangpunkte!.Count);   // wie bisher: Enden
    }

    [Fact]
    public void Polylinie_JedesTeilstueckEineKante_DoppeltePunkteEntfallen()
    {
        var p = MitSerie(false);
        // L-Zug mit einem doppelten Punkt und einem senkrechten Stück (im Grundriss ohne Länge)
        Ok(Rohr(p, "RO-1", "DN 20", [0, 0, 500], [2000, 0, 500], [2000, 0, 900], [2000, 1500, 900]));
        var t = Teil(p, "RO-1");
        Assert.Equal(2, t.Fangkanten!.Count);
        Etwa([0, 0], t.Fangkanten[0][0]); Etwa([2000, 0], t.Fangkanten[0][1]);
        Etwa([2000, 0], t.Fangkanten[1][0]); Etwa([2000, 1500], t.Fangkanten[1][1]);
    }

    [Fact]
    public void RohrOhneSerie_AchseIstFangkante()
    {
        var p = MitSerie(false);
        Ok(RohrOhneSerie(p, "RO-1", 20, [100, 200, 500], [3100, 2200, 500]));
        var kante = Assert.Single(Teil(p, "RO-1").Fangkanten!);
        Etwa([100, 200], kante[0]); Etwa([3100, 2200], kante[1]);
    }

    [Fact]
    public void SenkrechteLeitung_HatKeineKante_NurDenFangpunkt()
    {
        var p = MitSerie(false);
        Ok(Rohr(p, "RO-1", "DN 20", [1000, 1000, 0], [1000, 1000, 2000]));
        var t = Teil(p, "RO-1");
        Assert.True(t.Fangkanten is null || t.Fangkanten.Count == 0);
        Etwa([1000, 1000], Assert.Single(t.Fangpunkte!));
    }
}
