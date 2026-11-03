using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;

namespace Hlks.Kern.Tests;

/// <summary>Bearbeitungswerkzeuge A2 (entwurf/bearbeitungswerkzeuge.md): element_aendern mit «verbinden» – ein Leitungsende wird mit dem
/// Griff auf einen Anschluss gezogen, Geometrie und Verbindung im selben Paket. Werte FIKTIV.</summary>
public class LeitungsendeAnschliessenTests
{
    static JsonObject Ende(Projekt p, string kz, double[] neu, string nach)
    {
        var pts = Pts(Hole(p, kz).Geometrie!.Punkte.ToArray());
        pts[pts.Count - 1] = new JsonArray(neu.Select(x => (JsonNode?)x).ToArray());
        return new JsonObject
        {
            ["element"] = kz, ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = pts },
            ["verbinden"] = new JsonObject { ["anschluss"] = "ende", ["nach"] = nach },
        };
    }

    static Anschlusspunkt? Gegen(Projekt p, string kz, string an) => p.Modell.VerbindungAn(Hole(p, kz).Id, an)?.Gegenueber(Hole(p, kz).Id, an);

    [Fact]
    public void EndeAufAnschluss_VerbindetUndTrenntDieAlteVerbindung_EinRueckgaengig()
    {
        var p = MitSerie(false);
        Ok(RohrOhneSerie(p, "RO-1", 20, [0, 0, 500], [1000, 0, 500]));
        Ok(RohrOhneSerie(p, "RO-2", 20, [2000, 0, 500], [3000, 0, 500]));
        Ok(RohrOhneSerie(p, "RO-3", 20, [1000, 0, 500], [1500, 0, 500]));
        Ok(p.Ausfuehren("verbinden", new JsonObject { ["von"] = "RO-1.ende", ["nach"] = "RO-3.anfang" }));

        var r = Ok(p.Ausfuehren("element_aendern", Ende(p, "RO-1", [2000, 0, 500], "RO-2.anfang")));

        Assert.Equal(Hole(p, "RO-2").Id, Gegen(p, "RO-1", "ende")?.Element);
        Assert.Null(p.Modell.VerbindungAn(Hole(p, "RO-3").Id, "anfang"));
        Assert.Contains(r.Hinweise, h => h.Contains("RO-3.anfang getrennt"));
        Assert.Equal(2000, Hole(p, "RO-1").Geometrie!.Punkte[^1][0]);

        p.Rueckgaengig();
        Assert.Equal(Hole(p, "RO-3").Id, Gegen(p, "RO-1", "ende")?.Element);
        Assert.Equal(1000, Hole(p, "RO-1").Geometrie!.Punkte[^1][0]);
    }

    [Fact]
    public void AnschlussPasstNicht_GeometrieBleibt()
    {
        var p = MitSerie(false);
        Ok(RohrOhneSerie(p, "RO-1", 20, [0, 0, 500], [1000, 0, 500]));
        Ok(RohrOhneSerie(p, "RO-2", 25, [2000, 0, 500], [3000, 0, 500]));

        var r = p.Ausfuehren("element_aendern", Ende(p, "RO-1", [2000, 0, 500], "RO-2.anfang"));

        Assert.False(r.Angenommen);
        Assert.Contains("Grössen passen nicht", r.Fehler);
        Assert.Equal(1000, Hole(p, "RO-1").Geometrie!.Punkte[^1][0]);
        Assert.Null(p.Modell.VerbindungAn(Hole(p, "RO-1").Id, "ende"));
    }
}
