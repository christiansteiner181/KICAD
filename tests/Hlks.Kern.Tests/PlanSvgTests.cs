namespace Hlks.Kern.Tests;

/// <summary>PlanSvg (BIM to field) zeichnet wie PDF und Bildschirm: Strichmuster, genaue Strichstärke, gedrehte Ansicht (Gesamtprüfung 13.11.2026).</summary>
public class PlanSvgTests
{
    static Plangrafik Plan(params Primitiv[] primitive) => new()
    {
        Ansicht = "T", Massstab = 50, Begrenzung = [0, 0, 20000, 5000],
        Teile = [new Grafikteil { Id = "e1", Ebene = "leitung", Primitive = [.. primitive] }],
    };

    [Fact]
    public void Strichmuster_UndGenaueStaerke_WiePdf()
    {
        // Handrechnung 1:50: «strich» 3.0/1.0 mm Papier = 150/50 Modell-mm; «achse» 3.0/0.6/0.3/0.6 = 150/30/15/30; Stift fein 0.10 mm = 5; Stil.Mm 0.25 = 12.5
        var svg = PlanSvg.Svg(Plan(
            new Primitiv { Art = "linie", Punkte = [[0, 0], [1000, 0]], Stil = new Stil { Stift = Stifte.Hilf, Linie = "strich" } },
            new Primitiv { Art = "linie", Punkte = [[0, 100], [1000, 100]], Stil = new Stil { Stift = Stifte.Fein, Linie = "achse" } },
            new Primitiv { Art = "linie", Punkte = [[0, 200], [1000, 200]], Stil = new Stil { Stift = Stifte.Duenn, Mm = 0.25 } }));
        Assert.Contains("stroke-dasharray=\"150 50\"", svg);
        Assert.Contains("stroke-width=\"5\" stroke-dasharray=\"150 30 15 30\"", svg);
        Assert.Contains("stroke-width=\"12.5\"", svg);
    }

    [Fact]
    public void GedrehteAnsicht_AusschnittInDenAchsenDerAnsicht()
    {
        // Plan 20000 × 5000 um 90° gedreht (Gegenuhrzeigersinn um den Ursprung): x' = −y, y' = x → Begrenzung [−5000, 0, 0, 20000];
        // Rand 2 % von 20000 = 400 → viewBox x −5400, y −(20000 + 400), Breite 5800, Höhe 20800
        var g = Plan(new Primitiv { Art = "linie", Punkte = [[0, 0], [20000, 5000]], Stil = new Stil() });
        g.Drehung = 90; g.BegrenzungAnsicht = Grafik.Begrenzung(g.Teile, 90);
        Assert.Equal([-5000.0, 0, 0, 20000], g.BegrenzungAnsicht!.Select(v => Math.Round(v, 6)));
        var svg = PlanSvg.Svg(g);
        Assert.Contains("viewBox=\"-5400 -20400 5800 20800\"", svg);
        Assert.Contains("<g transform=\"rotate(-90)\">", svg);
    }
}
