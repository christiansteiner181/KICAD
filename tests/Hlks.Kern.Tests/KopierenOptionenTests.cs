using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;
using static Hlks.Kern.Tests.RoutingHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Bearbeitungswerkzeuge B6 (entwurf/bearbeitungswerkzeuge.md): Drehen/Spiegeln mit kopieren, Kopieren auf ein anderes Geschoss. Werte FIKTIV.</summary>
public class KopierenOptionenTests
{
    [Fact]
    public void Drehen_Kopieren_OriginalBleibt_KopieGedreht()
    {
        var p = Neu();
        Ok(p.Ausfuehren("wand_zeichnen", new JsonObject { ["geschoss"] = "EG", ["punkte"] = Pts([0, 0], [3000, 0]), ["dicke"] = 200 }));
        var vorher = Hole(p, "W-1").Geometrie!.Punkte.Select(q => q[..2]).ToList();
        var e = Ok(p.Ausfuehren("elemente_drehen", new JsonObject { ["elemente"] = new JsonArray("W-1"), ["mitte"] = new JsonArray(0, 0), ["winkel"] = 90, ["kopieren"] = true }));
        Assert.Equal(vorher, Hole(p, "W-1").Geometrie!.Punkte.Select(q => q[..2]).ToList());
        Etwa([0, 3000], Hole(p, "W-2").Geometrie!.Punkte[1][..2]);
        Assert.Contains(e.Hinweise, h => h.StartsWith("Kopiert: W-2"));
    }

    [Fact]
    public void Spiegeln_Kopieren_LeitungAufDerAnderenSeite()
    {
        var p = MitSerie();
        Ok(Zeichnen(p, "DN 20", [[0, 500, 0], [1000, 500, 0]]));
        Ok(p.Ausfuehren("elemente_spiegeln", new JsonObject { ["elemente"] = new JsonArray("RO-1"), ["achse"] = Pts([0, 0], [1000, 0]), ["kopieren"] = true }));
        Gerade(p, "RO-1", [0, 500, 0], [1000, 500, 0]);
        Gerade(p, "RO-2", [0, -500, 0], [1000, -500, 0]);
    }

    [Fact]
    public void Kopieren_AufAnderesGeschoss_HoeheUndGeschossNeu()
    {
        var p = MitSerie();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Geschoss", ["kennzeichen"] = "OG", ["teilVon"] = "GEB", ["eigenschaften"] = new JsonObject { ["kote"] = 3000, ["hoehe"] = 3000 },
        }));
        Ok(Zeichnen(p, "DN 20", [[0, 500, 500], [1000, 500, 500]]));
        Ok(p.Ausfuehren("elemente_kopieren", new JsonObject { ["elemente"] = new JsonArray("RO-1"), ["dz"] = 3000, ["geschoss"] = "OG" }));
        Gerade(p, "RO-2", [0, 500, 3500], [1000, 500, 3500]);
        Assert.Equal(Hole(p, "OG").Id, Hole(p, "RO-2").Geschoss);
        Assert.Equal(Hole(p, "EG").Id, Hole(p, "RO-1").Geschoss);
    }
}
