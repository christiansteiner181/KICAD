using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;
using static Hlks.Kern.Tests.RoutingHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Bearbeitungswerkzeuge B2 (entwurf/bearbeitungswerkzeuge.md): parallele_leitungen. Werte FIKTIV (Testfamilien).</summary>
public class ParalleleLeitungenTests
{
    [Fact]
    public void Waagrecht_LZug_AussenParallel_KnickAufDerWinkelhalbierenden()
    {
        var p = MitSerie();
        Ok(Zeichnen(p, "DN 20", [[0, 0, 0], [1000, 0, 0], [1000, 1000, 0]]));   // RO-1, BO-1, RO-2
        var e = Ok(p.Ausfuehren("parallele_leitungen", new JsonObject { ["leitung"] = "RO-1", ["abstand"] = 200, ["seite"] = new JsonArray(500, -500) }));
        // Knoten (0, −200), (1200, −200), (1200, 1000); Bogen 90° DN 20 Einbaulänge 50
        var neu = Angelegt(e, "Rohr");
        Assert.Equal(2, neu.Count);
        Gerade(p, neu[0], [0, -200, 0], [1150, -200, 0]);
        Gerade(p, neu[1], [1200, -150, 0], [1200, 1000, 0]);
        Assert.Single(Angelegt(e, "Bogen"));
        Assert.Contains(e.Hinweise, h => h.StartsWith("1 paralleler Zug zu RO-1 (waagrecht, Abstand 200 mm)"));
        Sauber(p);
    }

    /// <summary>Gesamtprüfung 13.11.2026: die Seite gilt gegenüber der angeklickten Geraden. Klick aussen neben RO-2 (x = 1300) – gegenüber dem ersten
    /// Stück des Zugs (RO-1) läge der Punkt auf der Innenseite (y > 0), die Parallele entstand dort.</summary>
    [Fact]
    public void Waagrecht_LZug_KlickAmZweitenStueck_SeiteGegenueberDerAngeklicktenGeraden()
    {
        var p = MitSerie();
        Ok(Zeichnen(p, "DN 20", [[0, 0, 0], [1000, 0, 0], [1000, 1000, 0]]));   // RO-1, BO-1, RO-2
        var e = Ok(p.Ausfuehren("parallele_leitungen", new JsonObject { ["leitung"] = "RO-2", ["abstand"] = 200, ["seite"] = new JsonArray(1300, 500) }));
        var neu = Angelegt(e, "Rohr");
        Gerade(p, neu[0], [0, -200, 0], [1150, -200, 0]);
        Gerade(p, neu[1], [1200, -150, 0], [1200, 1000, 0]);
    }

    [Fact]
    public void Oben_ZweiZuege_UebereinanderImAbstand()
    {
        var p = MitSerie();
        Ok(Zeichnen(p, "DN 20", [[0, 0, 0], [1000, 0, 0], [1000, 1000, 0]]));
        var e = Ok(p.Ausfuehren("parallele_leitungen", new JsonObject { ["leitung"] = "RO-2", ["abstand"] = 300, ["anzahl"] = 2, ["lage"] = "oben" }));
        var neu = Angelegt(e, "Rohr");
        Assert.Equal(4, neu.Count);
        Assert.Equal([300.0, 300, 600, 600], neu.Select(kz => Pkt(p, kz)[0][2]).Order().ToArray());
        Sauber(p);
    }

    [Fact]
    public void ZugMitReduktion_Abgelehnt()
    {
        var p = MitSerie();
        Ok(Zeichnen(p, "DN 20", [[0, 0, 0], [1000, 0, 0]]));
        Ok(p.Ausfuehren("leitung_groesse", new JsonObject { ["leitung"] = "RO-1", ["groesse"] = "DN 25" }));
        Ok(Zeichnen(p, "DN 20", [[1000, 0, 0], [2000, 0, 0]], new JsonObject { ["von"] = "RO-1.ende" }));
        var e = p.Ausfuehren("parallele_leitungen", new JsonObject { ["leitung"] = "RO-1", ["abstand"] = 200, ["seite"] = new JsonArray(0, 500) });
        Assert.False(e.Angenommen);
        Assert.Contains("nur in einer Grösse", e.Fehler);
    }
}
