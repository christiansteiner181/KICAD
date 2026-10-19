using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.AussparungHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3b (Hauptlauf 19.10.2026, Befund Abnahme Bernstrasse 42): der Aussparungsplan (darstellung «aussparungen») zeigt keine
/// Kollisionsmarken, der gewöhnliche Grundriss weiter. Werte FIKTIV.</summary>
public class KollisionMarkeAussparungsplanTests
{
    [Fact]
    public void Aussparungsplan_OhneKollisionsmarken_GrundrissMit()
    {
        var p = Wandprojekt();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Kollision", ["kennzeichen"] = "KOL-1", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["art"] = "hart", ["typ"] = "kontakt", ["status"] = "neu", ["mass"] = 12 },
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([3000, 0, 1000]) },
        }));
        var k = Hole(p, "KOL-1");
        Assert.Contains(Grafik.Plan(p, "AN-EG").Teile, t => t.Id == k.Id);
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "AN-EG", ["eigenschaften"] = new JsonObject { ["darstellung"] = "aussparungen" } }));
        Assert.DoesNotContain(Grafik.Plan(p, "AN-EG").Teile, t => t.Id == k.Id);
    }
}
