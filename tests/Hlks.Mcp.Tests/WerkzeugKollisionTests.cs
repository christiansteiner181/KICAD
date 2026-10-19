using System.Text.Json;
using System.Text.Json.Nodes;
using static Hlks.Mcp.Tests.Hilfe;

namespace Hlks.Mcp.Tests;

/// <summary>MCP-Werkzeuge der Kollisionsprüfung (Modul 3b, B2): vorgaben_setzen, vorgaben, kollision_pruefen (Trockenlauf, Paare,
/// weich), kollisionen, kollision_status – derselbe Weg, den die KI geht. Zwei sich kreuzende Testrohre ausserhalb des
/// Beispielgebäudes; Vorgaben FIKTIV.</summary>
public class WerkzeugKollisionTests
{
    static JsonElement Elem(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public void VorgabenPruefenListeStatus_UeberDieWerkzeuge()
    {
        var w = new Werkzeuge(Beispiel());
        // Vorgaben: ohne Quelle abgelehnt, mit Quelle gesetzt
        var nein = Json(w.VorgabenSetzenWerkzeug(Elem("""{ "abstand_leitungen": { "wert": 50 } }""")));
        Assert.False(nein["angenommen"]!.GetValue<bool>());
        Assert.False(string.IsNullOrWhiteSpace(nein["vorschlag"]?.GetValue<string>()));
        Assert.True(Json(w.VorgabenSetzenWerkzeug(Elem("""{ "abstand_leitungen": { "wert": 50, "quelle": "FIKTIV, MCP-Test" } }""")))["angenommen"]!.GetValue<bool>());
        Assert.Equal("FIKTIV, MCP-Test", Json(w.VorgabenLesen())["werte"]!["abstand_leitungen"]!["quelle"]!.GetValue<string>());

        // zwei kreuzende Rohre DN 20 weit ausserhalb des Gebäudes
        foreach (var (kz, punkte) in new[] { ("RO-T1", "[[50000, 0, 1000], [51000, 0, 1000]]"), ("RO-T2", "[[50500, -500, 1000], [50500, 500, 1000]]") })
            Assert.True(Json(w.ElementAnlegen("Rohr", kennzeichen: kz, system: "KW", eigenschaften: Elem("""{ "dn": 20 }"""),
                geometrie: Elem($$"""{ "art": "polylinie", "punkte": {{punkte}} }""")))["angenommen"]!.GetValue<bool>());
        var tl = Json(w.KollisionPruefenWerkzeug(paare: Elem("""["hlks-hlks"]"""), weich: false, trockenlauf: true));
        Assert.True(tl["trockenlauf"]!.GetValue<bool>(), tl.ToJsonString());
        var hinweise = string.Join(" | ", tl["hinweise"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Contains("Prüflauf 1 (hlks-hlks)", hinweise);
        Assert.Contains("weich = false", hinweise);
        Assert.Equal(0, Json(w.KollisionenLesen())["anzahl"]!.GetValue<int>());

        Assert.True(Json(w.KollisionPruefenWerkzeug(paare: Elem("""["hlks-hlks"]""")))["angenommen"]!.GetValue<bool>());
        var liste = Json(w.KollisionenLesen(element: "RO-T1"));
        Assert.Equal(1, liste["anzahl"]!.GetValue<int>());
        var zeile = liste["zeilen"]![0]!;
        Assert.Equal("hart", zeile["art"]!.GetValue<string>());
        Assert.Contains("RO-T2", zeile["beteiligte"]!.GetValue<string>());
        var kz2 = zeile["kollision"]!.GetValue<string>();

        var st = Json(w.KollisionStatusWerkzeug(Elem($"[\"{kz2}\"]"), "akzeptiert", kommentar: "bewusst so (Test)"));
        Assert.True(st["angenommen"]!.GetValue<bool>(), st.ToJsonString());
        Assert.Equal("akzeptiert", Json(w.KollisionenLesen(status: "akzeptiert"))["zeilen"]![0]!["status"]!.GetValue<string>());
        Assert.True(Json(w.Rueckgaengig())["angenommen"]!.GetValue<bool>());
        Assert.Equal(0, Json(w.KollisionenLesen(status: "akzeptiert"))["anzahl"]!.GetValue<int>());
        // Fehler als JSON
        Assert.Contains("fehler", w.KollisionenLesen(status: "kaputt"));
        Assert.False(Json(w.KollisionPruefenWerkzeug(paare: Elem("""["hlks-mond"]""")))["angenommen"]!.GetValue<bool>());
    }
}
