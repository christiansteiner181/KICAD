using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Nachbearbeitung N18 (Christian 21.10.2026: «stelle holz als wand ohne schraffur dar. verwende eine beige/transparente
/// farbe»): Holz wird im Grundriss und im Schnitt ohne Schraffur mit beiger, durchscheinender Füllung gezeichnet.</summary>
public class HolzOhneSchraffurTests
{
    [Fact]
    public void Schnittstil_Holz_BeigeDurchscheinendOhneMuster()
    {
        var s = ArchitekturGrafik.Schnittstil("holz");
        Assert.Null(s.Muster);
        Assert.Equal(ArchitekturGrafik.HolzFarbe, s.Fuellung);
        Assert.Equal(ArchitekturGrafik.HolzDeckkraft, s.Deckkraft);
        Assert.InRange(s.Deckkraft!.Value, 0.1, 0.9);       // durchscheinend
        Assert.Equal("beton", ArchitekturGrafik.Schnittstil("beton").Muster);   // andere Materialien unverändert
    }

    [Fact]
    public void Holzwand_ImGrundriss_OhneSchraffurMitBeigerFuellung()
    {
        var p = Neu();
        Ok(p.Ausfuehren("wand_zeichnen", new JsonObject
        {
            ["geschoss"] = "EG", ["punkte"] = Pts([0, 0], [4000, 0]), ["dicke"] = 160, ["hoehe"] = 2600, ["material"] = "holz",
        }));
        var flaechen = Grafik.Plan(p, "AN-EG").Teile.Where(t => t.Id == Hole(p, "W-1").Id)
            .SelectMany(t => t.Primitive).Where(x => x.Art == "polygon").ToList();
        Assert.NotEmpty(flaechen);
        Assert.All(flaechen, f => { Assert.Null(f.Stil.Muster); Assert.Equal(ArchitekturGrafik.HolzFarbe, f.Stil.Fuellung); });
    }
}
