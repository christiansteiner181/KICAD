using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;
using static Hlks.Kern.Tests.RoutingHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Bearbeitungswerkzeuge B1 (entwurf/bearbeitungswerkzeuge.md): ausrichten und versetzen. Werte FIKTIV.</summary>
public class AusrichtenVersatzTests
{
    static Projekt WandUndRohr()
    {
        var p = MitSerie();
        Ok(p.Ausfuehren("wand_zeichnen", new JsonObject { ["geschoss"] = "EG", ["punkte"] = Pts([0, 0], [5000, 0]), ["dicke"] = 200 }));
        Ok(Zeichnen(p, "DN 20", [[0, 500, 0], [3000, 500, 0]]));   // RO-1 parallel zur Wand, Enden frei
        return p;
    }

    [Fact]
    public void Ausrichten_LeitungAufWandflaecheNachKlickseite()
    {
        var p = WandUndRohr();
        var e = Ok(p.Ausfuehren("ausrichten", new JsonObject { ["element"] = "RO-1", ["bezug"] = "W-1", ["bezug_punkt"] = new JsonArray(100, 300) }));
        Gerade(p, "RO-1", [0, 100, 0], [3000, 100, 0]);   // Wandfläche y = 100 (Dicke 200, Achse y = 0)
        Assert.Contains(e.Hinweise, h => h.StartsWith("RO-1 auf die Fläche von W-1 ausgerichtet (400 mm verschoben)"));
    }

    [Fact]
    public void Ausrichten_NichtParallel_Abgelehnt()
    {
        var p = WandUndRohr();
        Ok(Zeichnen(p, "DN 20", [[4000, 300, 0], [4000, 2000, 0]]));   // RO-2 quer
        var e = p.Ausfuehren("ausrichten", new JsonObject { ["element"] = "RO-2", ["bezug"] = "W-1" });
        Assert.False(e.Angenommen);
        Assert.Contains("nicht parallel", e.Fehler);
    }

    [Fact]
    public void Versetzen_Kopieren_ParallelZurKlickseite_OriginalBleibt()
    {
        var p = WandUndRohr();
        var e = Ok(p.Ausfuehren("versetzen", new JsonObject { ["element"] = "RO-1", ["abstand"] = 150, ["seite"] = new JsonArray(0, 1000), ["kopieren"] = true }));
        Gerade(p, "RO-1", [0, 500, 0], [3000, 500, 0]);
        Gerade(p, "RO-2", [0, 650, 0], [3000, 650, 0]);
        Assert.Contains(e.Hinweise, h => h.StartsWith("RO-1 um 150 mm parallel kopiert"));
    }

    [Fact]
    public void Versetzen_GeradeZwischenBoegen_FormteileFolgen()
    {
        var p = MitSerie();
        Ok(Zeichnen(p, "DN 20", [[0, 0, 0], [1000, 0, 0], [1000, 1000, 0], [2000, 1000, 0]]));   // RO-1, BO-1, RO-2, BO-2, RO-3
        Ok(p.Ausfuehren("versetzen", new JsonObject { ["element"] = "RO-2", ["abstand"] = 100, ["seite"] = new JsonArray(2000, 500) }));
        Gerade(p, "RO-2", [1100, 50, 0], [1100, 950, 0]);
        Gerade(p, "RO-1", [0, 0, 0], [1050, 0, 0]);
        Sauber(p);
    }
}
