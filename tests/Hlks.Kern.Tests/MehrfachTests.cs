using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;
using static Hlks.Kern.Tests.RoutingHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Bearbeitungswerkzeuge B4 (entwurf/bearbeitungswerkzeuge.md): elemente_loeschen und elemente_aendern. Werte FIKTIV.</summary>
public class MehrfachTests
{
    [Fact]
    public void Loeschen_MitVerweisenInnerhalb_EinPaket_EinRueckgaengig()
    {
        var p = MitSerie();
        Ok(Zeichnen(p, "DN 20", [[0, 0, 0], [1000, 0, 0]]));   // RO-1 im System KW
        var vorher = Inhalt(p);
        var e = Ok(p.Ausfuehren("elemente_loeschen", new JsonObject { ["elemente"] = new JsonArray("KW", "RO-1") }));   // KW zuerst genannt
        Assert.Null(p.Modell.Finde("KW")); Assert.Null(p.Modell.Finde("RO-1"));
        Assert.Contains(e.Hinweise, h => h.StartsWith("2 Element(e) gelöscht"));
        Ok(p.Rueckgaengig());
        Assert.Equal(vorher, Inhalt(p));
    }

    [Fact]
    public void Loeschen_VerweisVonAussen_GanzerBefehlAbgelehnt()
    {
        var p = MitSerie();
        Ok(Zeichnen(p, "DN 20", [[0, 0, 0], [1000, 0, 0]]));
        Ok(Zeichnen(p, "DN 20", [[0, 500, 0], [1000, 500, 0]]));
        var vorher = Inhalt(p);
        var e = p.Ausfuehren("elemente_loeschen", new JsonObject { ["elemente"] = new JsonArray("RO-1", "KW") });   // RO-2 braucht KW noch
        Assert.False(e.Angenommen);
        Assert.Equal(vorher, Inhalt(p));
    }

    [Fact]
    public void Aendern_EigenschaftFuerAlle()
    {
        var p = MitSerie();
        Ok(Zeichnen(p, "DN 20", [[0, 0, 0], [1000, 0, 0]]));
        Ok(Zeichnen(p, "DN 20", [[0, 500, 0], [1000, 500, 0]]));
        Ok(p.Ausfuehren("elemente_aendern", new JsonObject { ["elemente"] = new JsonArray("RO-1", "RO-2"), ["eigenschaften"] = new JsonObject { ["daemmdicke"] = 20 } }));
        Assert.Equal(20, Hole(p, "RO-1").Zahl("daemmdicke"));
        Assert.Equal(20, Hole(p, "RO-2").Zahl("daemmdicke"));
    }
}
