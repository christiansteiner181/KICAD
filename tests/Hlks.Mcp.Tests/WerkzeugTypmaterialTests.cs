using System.Text.Json;
using System.Text.Json.Nodes;
using static Hlks.Mcp.Tests.Hilfe;

namespace Hlks.Mcp.Tests;

/// <summary>MCP-Werkzeuge der Typenliste (Modul 3b, Nachtrag B3d): typmaterial_setzen (Trockenlauf, Quelle, Fehler), typmaterial,
/// Rückgängig – derselbe Weg, den die KI geht. Beispielprojekt ohne IFC-Unterlage; Typname und Quelle FIKTIV.</summary>
public class WerkzeugTypmaterialTests
{
    static JsonElement Elem(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public void TypmaterialSetzenUndLesen_UeberDieWerkzeuge()
    {
        var w = new Werkzeuge(Beispiel());
        // ohne Quelle abgelehnt, mit Fehler und Vorschlag
        var nein = Json(w.TypmaterialSetzenWerkzeug(Elem("""[{ "typname": "FIKTIV Wandtyp 1", "kategorie": "leichtbau" }]""")));
        Assert.False(nein["angenommen"]!.GetValue<bool>());
        Assert.Contains("Quelle fehlt", nein["fehler"]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(nein["vorschlag"]?.GetValue<string>()));
        // Trockenlauf: nichts gespeichert
        var tl = Json(w.TypmaterialSetzenWerkzeug(Elem("""[{ "typname": "FIKTIV Wandtyp 1", "kategorie": "leichtbau" }]"""), quelle: "FIKTIV, MCP-Test", trockenlauf: true));
        Assert.True(tl["trockenlauf"]!.GetValue<bool>(), tl.ToJsonString());
        Assert.Empty(Json(w.TypmaterialLesen())["typen"]!.AsArray());
        // setzen, lesen (kein Bauteil dieses Typs im Beispielprojekt: Hinweis auf die Schreibweise)
        var ja = Json(w.TypmaterialSetzenWerkzeug(Elem("""[{ "typname": "FIKTIV Wandtyp 1", "kategorie": "leichtbau" }]"""), quelle: "FIKTIV, MCP-Test"));
        Assert.True(ja["angenommen"]!.GetValue<bool>(), ja.ToJsonString());
        var o = Json(w.TypmaterialLesen());
        var t = Assert.Single(o["typen"]!.AsArray())!;
        Assert.Equal(("TM-1", "leichtbau", "FIKTIV, MCP-Test", 0), (t["element"]!.GetValue<string>(), t["kategorie"]!.GetValue<string>(),
            t["quelle"]!.GetValue<string>(), t["bauteile"]!.GetValue<int>()));
        Assert.Contains(o["hinweise"]!.AsArray(), h => h!.GetValue<string>().Contains("passt auf kein Bauteil"));
        Assert.NotNull(Json(w.TypmaterialLesen(betroffen: true))["unbekannt"]);
        // rückgängig
        Assert.True(Json(w.Rueckgaengig())["angenommen"]!.GetValue<bool>());
        Assert.Empty(Json(w.TypmaterialLesen())["typen"]!.AsArray());
        // unbekannte Kategorie
        var falsch = Json(w.TypmaterialSetzenWerkzeug(Elem("""[{ "typname": "X", "kategorie": "gips" }]"""), quelle: "FIKTIV"));
        Assert.False(falsch["angenommen"]!.GetValue<bool>());
        Assert.Contains("installationsvorwand", falsch["fehler"]!.GetValue<string>());
        // Kollisionsprüfung kennt das Paar hlks-vorwand
        Assert.True(Json(w.KollisionPruefenWerkzeug(paare: Elem("""["hlks-vorwand"]"""), trockenlauf: true))["angenommen"]!.GetValue<bool>());
    }
}
