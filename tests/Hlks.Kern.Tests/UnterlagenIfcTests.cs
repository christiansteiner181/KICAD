using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.GrundlagenHilfe;
using static Hlks.Kern.Tests.UnterlagenHilfe;

namespace Hlks.Kern.Tests;

/// <summary>IFC-Leser (Modul 2, B2): Hersteller-IFC mit Typ, Ports, Property Sets und Körper.</summary>
public class UnterlagenIfcTests
{
    static UnterlagenBefunde Lesen1(out Ordner o)
    {
        o = new Ordner();
        var pfad = TestIfc(o);
        return new IfcBefundLeser().Lesen(pfad, "ventil.ifc", "aabbccddeeff0011", "ifc");
    }

    [Fact]
    public void Typobjekt_mit_Klasse_Predefined_Type_und_Pset()
    {
        var b = Lesen1(out var o);
        using (o)
        {
            var t = Art(b, "typ").Single();
            var w = (JsonObject)t.Wert!;
            Assert.Equal("V-15 Kugelhahn", w["name"]!.GetValue<string>());
            Assert.Equal("IfcValveType", w["ifcKlasse"]!.GetValue<string>());
            Assert.Equal("ISOLATING", w["predefinedType"]!.GetValue<string>());
            Assert.Equal(1, t.Zusatz!["exemplare"]!.GetValue<int>());
            var ps = Art(b, "pset").Single(x => x.Zusatz?["quelle"]?.GetValue<string>() == "typ");
            Assert.Equal("Pset_ValveTypeCommon", ps.Wert!["pset"]!.GetValue<string>());
            Assert.Equal(15, Zahl(ps.Wert["eigenschaften"]!["NominalDiameter"]), 1e-9);
            Assert.Equal("mm", ps.Wert["einheiten"]!["NominalDiameter"]!.GetValue<string>());
            Assert.Equal("R2015-S1", ps.Wert["eigenschaften"]!["Reference"]!.GetValue<string>());
            Assert.Equal(t.Id, ps.Zusatz!["typBefund"]!.GetValue<string>());
        }
    }

    [Fact]
    public void Ports_mit_Lage_Richtung_Fluss_und_Eigenschaften()
    {
        var b = Lesen1(out var o);
        using (o)
        {
            var ports = Art(b, "port").ToList();
            Assert.Equal(2, ports.Count);
            var a = ports.Single(p => p.Wert!["name"]!.GetValue<string>() == "A");
            var bb = ports.Single(p => p.Wert!["name"]!.GetValue<string>() == "B");
            Assert.Equal([-50, 0, 20], ((JsonArray)a.Wert!["lage"]!).Select(Zahl).ToArray());
            Assert.Equal([50, 0, 20], ((JsonArray)bb.Wert!["lage"]!).Select(Zahl).ToArray());
            Assert.Equal([-1, 0, 0], ((JsonArray)a.Wert["richtung"]!).Select(x => Math.Round(Zahl(x), 3) + 0.0).ToArray());
            Assert.Equal([1, 0, 0], ((JsonArray)bb.Wert["richtung"]!).Select(x => Math.Round(Zahl(x), 3) + 0.0).ToArray());
            Assert.Equal("rein", a.Wert["fluss"]!.GetValue<string>());      // SINK
            Assert.Equal("raus", bb.Wert["fluss"]!.GetValue<string>());     // SOURCE
            Assert.Equal("SINK", a.Wert["flussDirection"]!.GetValue<string>());
            Assert.Equal(15, Zahl(a.Wert["eigenschaften"]!["Pset_DistributionPortTypeCommon.NominalDiameter"]));
            Assert.Equal("V-15 Kugelhahn", a.Zusatz!["typ"]!.GetValue<string>());
            Assert.Contains(b.Hinweise, h => h.Contains("Port-Richtung"));
        }
    }

    [Fact]
    public void Form3d_Huellquader_aus_der_IFC_Geometrie_lokal_zum_Produkt()
    {
        var b = Lesen1(out var o);
        using (o)
        {
            var f = Art(b, "form3d").Single();
            var w = (JsonObject)f.Wert!;
            Assert.Equal([-50, -30, 0], ((JsonArray)w["huelle"]!["min"]!).Select(Zahl).ToArray());
            Assert.Equal([50, 30, 40], ((JsonArray)w["huelle"]!["max"]!).Select(Zahl).ToArray());
            Assert.Equal(12, w["anzahlDreiecke"]!.GetValue<int>());
            Assert.Equal("mm", f.Einheit);
            Assert.Equal("Kugelhahn R2015-S1", f.Zusatz!["produkt"]!.GetValue<string>());
            Assert.Equal("IfcValve", f.Zusatz["ifcKlasse"]!.GetValue<string>());
        }
    }

    [Fact]
    public void Ids_laufend()
    {
        var b = Lesen1(out var o);
        using (o) Assert.All(b.Befunde.Select((x, i) => (x, i)), t => Assert.Equal($"aabbccddeeff#{t.i + 1}", t.x.Id));
    }

    [Fact]
    public void Beschaedigte_Datei_gibt_Fehler_mit_Vorschlag()
    {
        using var o = new Ordner();
        var pfad = Datei(o, "kaputt.ifc", "kein IFC");
        var ex = Assert.Throws<HlksFehler>(() => new IfcBefundLeser().Lesen(pfad, "kaputt.ifc", "000000000000", "ifc"));
        Assert.False(string.IsNullOrWhiteSpace(ex.Vorschlag));
    }
}
