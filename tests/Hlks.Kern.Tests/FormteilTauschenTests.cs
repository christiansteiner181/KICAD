using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;
using static Hlks.Kern.Tests.RoutingHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Bearbeitungswerkzeuge B3 (entwurf/bearbeitungswerkzeuge.md): bogen_zu_tstueck und tstueck_zu_bogen. Werte FIKTIV (Testfamilien).</summary>
public class FormteilTauschenTests
{
    static string? Klasse(Projekt p, string kz, string an) =>
        p.Modell.VerbindungAn(Hole(p, kz).Id, an)?.Gegenueber(Hole(p, kz).Id, an) is { } g ? p.Modell.Finde(g.Element)?.Klasse : null;

    [Fact]
    public void Plus_BogenWirdTStueck_DurchgangOffen_MinusMachtWiederEinenBogen()
    {
        var p = MitSerie();
        Ok(Zeichnen(p, "DN 20", [[0, 0, 0], [1000, 0, 0], [1000, 1000, 0]]));   // RO-1, BO-1, RO-2
        var e = Ok(p.Ausfuehren("bogen_zu_tstueck", new JsonObject { ["bogen"] = "BO-1" }));
        Assert.Null(p.Modell.Finde("BO-1"));
        var ts = Assert.Single(Angelegt(e, "TStueck"));
        Assert.Equal("TStueck", Klasse(p, "RO-2", "anfang"));
        Assert.Equal("TStueck", Klasse(p, "RO-1", "ende"));
        Assert.Empty(Angelegt(e, "Rohr").Where(kz => p.Modell.Finde(kz) is not null));   // das Hilfsstück ist wieder weg
        Assert.Null(p.Modell.VerbindungAn(Hole(p, ts).Id, "b"));                         // Durchgang offen zum Weiterzeichnen
        Assert.Contains(e.Hinweise, h => h.Contains($"{ts}.b ist offen"));
        Sauber(p);

        var r = Ok(p.Ausfuehren("tstueck_zu_bogen", new JsonObject { ["tstueck"] = ts }));
        Assert.Null(p.Modell.Finde(ts));
        Assert.Single(Angelegt(r, "Bogen"));
        Gerade(p, "RO-1", [0, 0, 0], [950, 0, 0]);
        Gerade(p, "RO-2", [1000, 50, 0], [1000, 1000, 0]);
        Sauber(p);
    }

    [Fact]
    public void Minus_AbzweigWeg_DurchgangDirektVerbunden()
    {
        var p = MitSerie();
        Ok(Zeichnen(p, "DN 20", [[0, 0, 0], [2000, 0, 0]]));
        Ok(Zeichnen(p, "DN 20", [[1000, 300, 0], [1000, 1500, 0]]));
        var d = Ok(p.Ausfuehren("dehnen_bis", new JsonObject { ["element"] = "RO-2", ["bis"] = "RO-1" }));
        var ts = Assert.Single(Angelegt(d, "TStueck"));
        var zweite = Angelegt(d, "Rohr").Single();   // zweites Stück der Hauptleitung
        Ok(p.Ausfuehren("tstueck_zu_bogen", new JsonObject { ["tstueck"] = ts, ["anschluss"] = "c" }));
        Assert.Null(p.Modell.Finde("RO-2"));          // der Abzweig hatte ein freies Ende und entfällt mit
        Gerade(p, "RO-1", [0, 0, 0], [1000, 0, 0]);
        Gerade(p, zweite, [1000, 0, 0], [2000, 0, 0]);
        IstVerbunden(p, "RO-1", "ende", zweite, "anfang");
        Sauber(p);
    }
}
