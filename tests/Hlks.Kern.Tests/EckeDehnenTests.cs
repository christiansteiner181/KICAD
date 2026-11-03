using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;
using static Hlks.Kern.Tests.RoutingHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Bearbeitungswerkzeuge A4 (entwurf/bearbeitungswerkzeuge.md): ecke_bilden und dehnen_bis. Werte FIKTIV (Testfamilien).</summary>
public class EckeDehnenTests
{
    [Fact]
    public void Ecke_ZweiLeitungen_BogenDerSerie_KennzeichenBleiben()
    {
        var p = MitSerie();
        Ok(Zeichnen(p, "DN 20", [[0, 0, 0], [800, 0, 0]]));          // RO-1 nach Osten, endet vor der Ecke
        Ok(Zeichnen(p, "DN 20", [[1000, 200, 0], [1000, 1000, 0]]));  // RO-2 nach Norden, beginnt nach der Ecke
        var e = Ok(p.Ausfuehren("ecke_bilden", new JsonObject { ["a"] = "RO-1", ["b"] = "RO-2" }));
        // Ecke (1000, 0); Bogen 90° DN 20: Einbaulänge 50
        Gerade(p, "RO-1", [0, 0, 0], [950, 0, 0]);
        Gerade(p, "RO-2", [1000, 50, 0], [1000, 1000, 0]);
        var bo = Assert.Single(Angelegt(e, "Bogen"));
        IstVerbunden(p, "RO-1", "ende", bo, "a");
        IstVerbunden(p, bo, "b", "RO-2", "anfang");
        Sauber(p);
    }

    [Fact]
    public void Ecke_BeideLaufenZurEcke_Abgelehnt()
    {
        var p = MitSerie();
        Ok(Zeichnen(p, "DN 20", [[0, 0, 0], [800, 0, 0]]));
        Ok(Zeichnen(p, "DN 20", [[1000, 1000, 0], [1000, 200, 0]]));  // RO-2 nach Süden, läuft auch zur Ecke
        var e = p.Ausfuehren("ecke_bilden", new JsonObject { ["a"] = "RO-1", ["b"] = "RO-2" });
        Assert.False(e.Angenommen);
        Assert.Contains("laufen beide zur Ecke hin", e.Fehler);
    }

    [Fact]
    public void Ecke_Waende_GeometrischImGrundriss_BleibtNachKlick()
    {
        var p = Neu();
        Ok(p.Ausfuehren("wand_zeichnen", new JsonObject { ["geschoss"] = "EG", ["punkte"] = Pts([0, 0], [3000, 0]), ["dicke"] = 200 }));
        Ok(p.Ausfuehren("wand_zeichnen", new JsonObject { ["geschoss"] = "EG", ["punkte"] = Pts([2000, -1000], [2000, 2000]), ["dicke"] = 200 }));
        // W-1 bleibt links der Kreuzung, W-2 oberhalb
        Ok(p.Ausfuehren("ecke_bilden", new JsonObject { ["a"] = "W-1", ["b"] = "W-2", ["bleibt_a"] = new JsonArray(500, 0), ["bleibt_b"] = new JsonArray(2000, 1500) }));
        Etwa([2000, 0], Hole(p, "W-1").Geometrie!.Punkte[1][..2]);
        Etwa([2000, 0], Hole(p, "W-2").Geometrie!.Punkte[0][..2]);
    }

    [Fact]
    public void DehnenBis_LeitungAnHauptleitung_TStueckUndAngeschlossen()
    {
        var p = MitSerie();
        Ok(Zeichnen(p, "DN 20", [[0, 0, 0], [2000, 0, 0]]));            // RO-1 Hauptleitung
        Ok(Zeichnen(p, "DN 20", [[1000, 300, 0], [1000, 1500, 0]]));     // RO-2 geht von der Hauptleitung weg (Trinkwasser: zuführend)
        var e = Ok(p.Ausfuehren("dehnen_bis", new JsonObject { ["element"] = "RO-2", ["bis"] = "RO-1" }));
        var ts = Assert.Single(Angelegt(e, "TStueck"));
        var an = p.Modell.VerbindungAn(Hole(p, "RO-2").Id, "anfang")?.Gegenueber(Hole(p, "RO-2").Id, "anfang");
        Assert.NotNull(an);
        Assert.Equal(Hole(p, ts).Id, an!.Element);
        Assert.Equal(1500, Pkt(p, "RO-2")[1][1], 3);
        Assert.Contains(e.Hinweise, h => h.StartsWith("RO-2 bis RO-1 gedehnt bzw. gestutzt, angeschlossen an"));
        Sauber(p);
    }
}
