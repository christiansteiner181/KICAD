using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Deckung Nachbau ↔ IFC (Christian 02.11.2026: «nach dem import müssen die beiden geometrien deckungsgleich sein») an der fiktiven
/// Architektur-Testdatei: Wände, Decken, Stützen decken sich; eine in KICAD verschobene Wand weicht um die Verschiebung ab.</summary>
public class IfcDeckungTests
{
    [Fact]
    public void FiktiveArchitektur_WaendeDeckenStuetzen_Deckungsgleich_VerschiebungWirdGemessen()
    {
        var p = Leer();
        Ok(Uebernehmen(p, IfcArchitekturTestdatei.Datei("IFC4", false)));
        var u = IfcDeckung.Pruefen(p, new JsonObject { ["toleranz"] = 1 })["unterlagen"]!.AsArray().Single()!;
        var k = u["jeKlasse"]!.AsObject();
        foreach (var klasse in new[] { "Wand", "Decke", "Stuetze", "Tuer", "Fenster" }.Where(x => k.ContainsKey(x)))
            Assert.Equal(k[klasse]!["anzahl"]!.GetValue<int>(), k[klasse]!["deckungsgleich"]!.GetValue<int>());
        var w = IfcArchitekturTests.Namens(p, "Wand", "W-Sued");
        var g = w.Geometrie!.Punkte;
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = w.Kennzeichen, ["geometrie"] = new JsonObject
        {
            ["art"] = "linie", ["punkte"] = new JsonArray(g.Select(q => (JsonNode)new JsonArray(q[0], q[1] + 200, q.Length > 2 ? q[2] : 0)).ToArray()),
            ["dicke"] = w.Geometrie.Dicke, ["hoehe"] = w.Geometrie.Hoehe,
        } }));
        var a = IfcDeckung.Pruefen(p, new JsonObject { ["toleranz"] = 1 })["unterlagen"]![0]!["abweichend"]!.AsArray();
        var e = Assert.Single(a, x => x!["kennzeichen"]!.GetValue<string>() == w.Kennzeichen)!;
        Assert.Equal(200, e["abweichungMm"]!.GetValue<double>(), 1);
    }
}
