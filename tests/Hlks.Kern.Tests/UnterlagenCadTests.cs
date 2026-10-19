using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.GrundlagenHilfe;
using static Hlks.Kern.Tests.UnterlagenHilfe;

namespace Hlks.Kern.Tests;

/// <summary>DWG/DXF-Leser «cad» (Modul 2, B2): Entitäten je Layout und Layer, Bemassung mit exaktem Messwert, Blöcke mit Namen.</summary>
public class UnterlagenCadTests
{
    static UnterlagenBefunde Lesen1(out Ordner o)
    {
        o = new Ordner();
        var pfad = TestDxf(o);
        return new CadBefundLeser().Lesen(pfad, "masse.dxf", "fedcba987654aaaa", "dxf");
    }

    [Fact]
    public void Bemassung_67_mm_exakt_mit_Definitionspunkten()
    {
        var b = Lesen1(out var o);
        using (o)
        {
            var m = Art(b, "mass").Single(x => x.Zusatz?["bemassung"]?.GetValue<string>() == "linear");
            Assert.Equal(67, Zahl(m.Wert), 1e-9);
            Assert.Equal("mm", m.Einheit);
            Assert.Equal(1.0, m.Sicherheit);
            Assert.Equal("MASSE", m.Layer);
            Assert.Equal("Model", m.Layout);
            Assert.Equal(3, m.Lage!.Count);
            Assert.Equal([0, 0], m.Lage[0]);
            Assert.Equal([67, 0], m.Lage[1]);
            Assert.Equal(67, m.Box![2] - m.Box[0], 1e-9);
        }
    }

    [Fact]
    public void Bemassung_mit_ueberschriebenem_Text_nennt_Messwert_und_Text()
    {
        var b = Lesen1(out var o);
        using (o)
        {
            var m = Art(b, "mass").Single(x => x.Zusatz?["bemassung"]?.GetValue<string>() == "ausgerichtet");
            Assert.Equal(35, Zahl(m.Wert), 1e-9);
            Assert.Equal("H = 35", m.Zusatz!["text"]!.GetValue<string>());
            Assert.Equal("H = 35", m.Zusatz["angezeigt"]!.GetValue<string>());
        }
    }

    [Fact]
    public void Entitaeten_je_Layer_Linie_Kreis_Bogen_Polylinie_Text()
    {
        var b = Lesen1(out var o);
        using (o)
        {
            Assert.Contains(Art(b, "linie"), l => l.Layer == "GEHAEUSE" && l.Layout == "Model" && l.Lage![0][0] == 0 && l.Lage[1][0] == 120);
            var k = Art(b, "kreis").Single(x => x.Layer == "GEHAEUSE");
            Assert.Equal(15, Zahl(((JsonObject)k.Wert!)["radius"]), 1e-9);
            Assert.Equal(30, Zahl(((JsonObject)k.Wert!)["durchmesser"]), 1e-9);
            var bo = Art(b, "bogen").Single();
            Assert.Equal(30, Zahl(((JsonObject)bo.Wert!)["radius"]), 1e-9);
            Assert.Equal(90, Zahl(((JsonObject)bo.Wert!)["bis"]), 1e-6);
            var pl = Art(b, "polylinie").Single(x => x.Layer == "KONTUR");
            Assert.Equal(3, pl.Lage!.Count);
            var t = Art(b, "text").Single(x => x.Layer == "TEXT");
            Assert.Equal("Ventil DN 15", t.Text());
            Assert.Equal(5, Zahl(t.Zusatz!["hoehe"]), 1e-9);
        }
    }

    [Fact]
    public void Block_mit_Name_Einfuegepunkt_und_Layer()
    {
        var b = Lesen1(out var o);
        using (o)
        {
            var bl = Art(b, "block").Single();
            Assert.Equal("VENTIL", bl.Text());
            Assert.Equal([100, 50], bl.Lage![0]);
            Assert.Equal("BLOECKE", bl.Layer);
        }
    }

    [Fact]
    public void Ids_laufend_und_Leser_cad()
    {
        var b = Lesen1(out var o);
        using (o)
        {
            Assert.Equal("cad 1", b.Leser);
            Assert.All(b.Befunde.Select((x, i) => (x, i)), t => Assert.Equal($"fedcba987654#{t.i + 1}", t.x.Id));
            Assert.Contains(b.Hinweise, h => h.Contains("Millimeters") || h.Contains("mm"));
        }
    }

    [Fact]
    public void Ohne_Einheit_gilt_die_Annahme_Millimeter_mit_Hinweis()
    {
        using var o = new Ordner();
        var pfad = Dxf(o, "ohne.dxf", ACadSharp.Types.Units.UnitsType.Unitless, doc => doc.Entities.Add(Linie(doc, 0, 0, 10, 0, "L")));
        var b = new CadBefundLeser().Lesen(pfad, "ohne.dxf", "000000000000", "dxf");
        Assert.Contains(b.Hinweise, h => h.Contains("unbekannt") && h.Contains("Millimeter"));
    }

    [Fact]
    public void Kaputte_Datei_gibt_Fehler_mit_Vorschlag()
    {
        using var o = new Ordner();
        var pfad = Datei(o, "kaputt.dxf", "kein DXF\nnur Text\n");
        var ex = Assert.Throws<HlksFehler>(() => new CadBefundLeser().Lesen(pfad, "kaputt.dxf", "000000000000", "dxf"));
        Assert.False(string.IsNullOrWhiteSpace(ex.Vorschlag));
    }
}
