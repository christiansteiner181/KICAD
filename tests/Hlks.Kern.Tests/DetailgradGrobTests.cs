using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Nachbearbeitung N20 (Christian 21.10.2026: «detaillierungsgrad grob, alle wände schwarz solid»; SIA 400:2000 Ziff. B.8.3.1: bis 1:100
/// schwarz): Im Detailgrad «grob» zeichnen geschnittene Wände, Stützen und Unterzüge eine volle Fläche in Planfarbe ohne Muster, Kreuz
/// und Materialfarbe, im Grundriss und im Schnitt. «mittel» und «fein» bleiben unverändert.</summary>
public class DetailgradGrobTests
{
    static Projekt Haus(string detailgrad, string? material = null)
    {
        var p = Neu();
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "AN-EG", ["eigenschaften"] = new JsonObject { ["detailgrad"] = detailgrad } }));
        var par = new JsonObject { ["geschoss"] = "EG", ["punkte"] = Pts([0, 0], [6000, 0], [6000, 4000], [0, 4000]), ["dicke"] = 200, ["hoehe"] = 2600, ["geschlossen"] = true };
        if (material is not null) par["material"] = material;
        Ok(p.Ausfuehren("wand_zeichnen", par));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Stuetze", ["kennzeichen"] = "STZ-1", ["geschoss"] = "EG", ["eigenschaften"] = new JsonObject { ["material"] = "stahl" },
            ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = Pts([2000, 1000, 0], [2300, 1000, 0], [2300, 1300, 0], [2000, 1300, 0]), ["hoehe"] = 2600 },
        }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Traeger", ["kennzeichen"] = "UZ-1", ["geschoss"] = "EG", ["eigenschaften"] = new JsonObject { ["material"] = "beton" },
            ["geometrie"] = new JsonObject { ["art"] = "linie", ["punkte"] = Pts([500, 2000, 0], [3500, 2000, 0]), ["dicke"] = 300, ["hoehe"] = 3000 },
        }));
        return p;
    }

    static List<Primitiv> Flaechen(Projekt p, string kz, string ansicht = "AN-EG") =>
        Grafik.Plan(p, ansicht).Teile.Where(t => t.Id == Hole(p, kz).Id).SelectMany(t => t.Primitive).Where(x => x.Art == "polygon").ToList();

    static void AssertSchwarzVoll(IEnumerable<Primitiv> flaechen)
    {
        var liste = flaechen.ToList();
        Assert.NotEmpty(liste);
        Assert.All(liste, f =>
        {
            Assert.Equal("schnitt", f.Stil.Fuellung);       // Planfarbe: schwarz auf Papier, hell im dunklen Design
            Assert.Null(f.Stil.Farbe);
            Assert.Null(f.Stil.Muster);
            Assert.Equal(1.0, f.Stil.Deckkraft);
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("beton")]
    [InlineData("backstein")]
    [InlineData("holz")]
    [InlineData("kalksandstein")]
    public void Grob_Wand_SchwarzVollOhneMusterUndMaterialfarbe(string? material)
    {
        var p = Haus("grob", material);
        foreach (var w in new[] { "W-1", "W-2", "W-3", "W-4" }) AssertSchwarzVoll(Flaechen(p, w));
    }

    [Fact]
    public void Grob_WandUmriss_BleibtMitDickemStift()
    {
        var p = Haus("grob", "beton");
        var umriss = Grafik.Plan(p, "AN-EG").Teile.Where(t => t.Id is null && t.Ebene == "wand").SelectMany(t => t.Primitive).ToList();
        Assert.NotEmpty(umriss);
        Assert.All(umriss, x => Assert.Equal(Stifte.Dick, x.Stil.Stift));
    }

    [Fact]
    public void Grob_Stuetze_VolleFlaecheOhneKreuz()
    {
        var p = Haus("grob");
        var teil = Grafik.Plan(p, "AN-EG").Teile.Single(t => t.Id == Hole(p, "STZ-1").Id);
        var f = Assert.Single(teil.Primitive);                    // nur die Fläche, keine Diagonalen
        Assert.Equal("polygon", f.Art);
        Assert.Equal(Stifte.Dick, f.Stil.Stift);
        AssertSchwarzVoll([f]);
    }

    [Fact]
    public void Grob_StuetzeAusBeton_AuchOhneSchraffur()
    {
        var p = Haus("grob");
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "STZ-1", ["eigenschaften"] = new JsonObject { ["material"] = "beton" } }));
        AssertSchwarzVoll(Flaechen(p, "STZ-1"));
    }

    [Fact]
    public void Grob_Unterzug_VolleFlaecheOhneSchraffur()
    {
        var p = Haus("grob");
        var f = Assert.Single(Flaechen(p, "UZ-1"));
        Assert.Equal(Stifte.Dick, f.Stil.Stift);
        AssertSchwarzVoll([f]);
    }

    [Fact]
    public void Mittel_BleibtUnveraendert()
    {
        var p = Haus("mittel", "beton");
        Assert.All(Flaechen(p, "W-1"), f => { Assert.Equal("beton", f.Stil.Muster); Assert.Equal("papier", f.Stil.Fuellung); });
        var stuetze = Grafik.Plan(p, "AN-EG").Teile.Single(t => t.Id == Hole(p, "STZ-1").Id);
        Assert.Equal(2, stuetze.Primitive.Count(x => x.Art == "linie"));          // Kreuz (N16)
        Assert.Equal("beton", Assert.Single(Flaechen(p, "UZ-1")).Stil.Muster);
    }

    [Fact]
    public void Fein_Holz_BleibtBeige()
    {
        var p = Haus("fein", "holz");
        Assert.All(Flaechen(p, "W-1"), f => { Assert.Equal(ArchitekturGrafik.HolzFarbe, f.Stil.Fuellung); Assert.Null(f.Stil.Muster); });
    }

    [Fact]
    public void Grob_Schnitt_WandStuetzeUnterzugSchwarzVoll()
    {
        var p = Haus("grob", "beton");
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Ansicht", ["kennzeichen"] = "S-1",
            ["eigenschaften"] = new JsonObject { ["art"] = "schnitt", ["massstab"] = 50, ["detailgrad"] = "grob", ["blick"] = "links" },
            ["geometrie"] = new JsonObject { ["art"] = "linie", ["punkte"] = Pts([2150, -1000], [2150, 5000]) },
        }));
        var g = Grafik.Plan(p, "S-1");
        var schnittflaechen = g.Teile.Where(t => t.Ebene == "wand").SelectMany(t => t.Primitive).Where(x => x.Art == "polygon" && x.Stil.Stift == Stifte.Dick).ToList();
        Assert.True(schnittflaechen.Count >= 3);                   // zwei Wände, Stütze, Unterzug (Unterzug liegt in x = 500 … 3500, also geschnitten)
        AssertSchwarzVoll(schnittflaechen);

        // mittel: dieselbe Ansicht schraffiert (Beton)
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "S-1", ["eigenschaften"] = new JsonObject { ["detailgrad"] = "mittel" } }));
        var mittel = Grafik.Plan(p, "S-1").Teile.Where(t => t.Id == Hole(p, "W-1").Id).SelectMany(t => t.Primitive).Where(x => x.Art == "polygon").ToList();
        Assert.All(mittel, f => Assert.Equal("beton", f.Stil.Muster));
    }
}
