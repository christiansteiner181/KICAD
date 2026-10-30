using System.Text.Json.Nodes;

namespace Hlks.Kern.Tests;

/// <summary>Bögen mit Radius (30.10.2026, Christian: «alle ansichten von oben zeichnen keine bogenradien»; «rohrbogen sind meist ein
/// rotierter kreis mit einsteckmuffen»; «ausnahme geberit-pe dort sind es segmente»): Bogen-Familien aus zwei Schenkelzylindern werden
/// bei der Auswertung ein Sweep mit Radius; Geberit PE und Silent-db20 bleiben Segment.</summary>
public class BogenRadiusTests
{
    static JsonObject ZweiZylinder(string name)
    {
        var d = (JsonObject)JsonNode.Parse(File.ReadAllText(Modul3Hilfe.Datei("test_bogen")))!;
        d["name"] = name;
        d["koerper"] = JsonNode.Parse("""
            [{"id":"schenkel_a","art":"zylinder","material":"rohr","von":["-la",0,0],"nach":[0,0,0],"r":"da / 2"},
             {"id":"schenkel_b","art":"zylinder","material":"rohr","von":[0,0,0],"nach":["lb * cos(winkel)","lb * sin(winkel)",0],"r":"da / 2"}]
            """);
        return d;
    }

    [Fact]
    public void Zwei_Schenkelzylinder_werden_ein_Bogen_mit_Radius()
    {
        var f = FamilieTestHilfe.Def(ZweiZylinder("Testbogen Zylinder (fiktiv)"));
        Assert.True(f.Gueltig, string.Join("; ", f.Fehler.Select(x => x.Meldung)));
        var a = FamilienAuswertung.Aufloesen(f, f.Typen[0].Name, null);
        Assert.True(a.Gueltig, string.Join("; ", a.Fehler.Select(x => x.Meldung)));
        var t = Assert.Single(a.Koerper.Where(k => k.Teil is not null)).Teil!;
        Assert.Equal("koerper", t.Art);
        // die äussere Ecke des Knicks ist weg: kein Eckpunkt weiter als der Rohrradius vom Achsschnittpunkt entfernt in der «Ecke» (x > 0, y < 0)
        var da = a.Werte["da"]!.Value.Zahl;
        Assert.DoesNotContain(t.Ecken!, p => p[0] > da * 0.3 && p[1] < -da * 0.3);
    }

    [Theory]
    [InlineData("Geberit PE Bogen")]
    [InlineData("Geberit Silent-db20 Bogen")]
    public void Geberit_PE_und_Silent_bleiben_Segment(string name)
    {
        var f = FamilieTestHilfe.Def(ZweiZylinder(name));
        var a = FamilienAuswertung.Aufloesen(f, f.Typen[0].Name, null);
        Assert.Equal(2, a.Koerper.Count(k => k.Teil is { Art: "zylinder" }));
    }
}
