using System.Text.Json.Nodes;
namespace Hlks.Kern.Tests;

/// <summary>N38 (Christian 13.11.2026: «Annehmen und umwandeln»): Ja/Nein-Parameter als Text («true», «ja», «1») werden umgewandelt; ein unlesbarer
/// Wert wird abgelehnt statt still als Vorgabe zu gelten (vorher räumte schema_anordnen { alle: "true" } nur die verwaisten Lagen auf).</summary>
public class JaNeinTests
{
    [Fact]
    public void JaNein_TextUndZahl_Umgewandelt_UnlesbaresAbgelehnt()
    {
        var p = JsonNode.Parse("""{ "a": "true", "b": "Ja", "c": " 0 ", "d": 1, "e": false, "f": "vielleicht", "g": null }""")!.AsObject();
        Assert.True(Param.Bool(p, "a"));
        Assert.True(Param.Bool(p, "b"));
        Assert.False(Param.Bool(p, "c", true));
        Assert.True(Param.Bool(p, "d"));
        Assert.False(Param.Bool(p, "e", true));
        Assert.True(Param.Bool(p, "g", true));        // null → Vorgabe
        Assert.True(Param.Bool(p, "fehlt", true));
        var f = Assert.Throws<HlksFehler>(() => Param.Bool(p, "f"));
        Assert.Contains("«f» muss true oder false sein", f.Message);

        bool t = false;
        var ohne = Projekt.TrockenlaufAusParametern(JsonNode.Parse("""{ "trockenlauf": "ja", "x": 1 }""")!.AsObject(), ref t);
        Assert.True(t);
        Assert.False(ohne!.ContainsKey("trockenlauf"));
    }
}
