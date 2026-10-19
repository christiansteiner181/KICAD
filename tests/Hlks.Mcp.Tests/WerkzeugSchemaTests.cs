using static Hlks.Mcp.Tests.Hilfe;

namespace Hlks.Mcp.Tests;

/// <summary>Modul 3, B3 und Nachtrag B8: MCP-Werkzeuge schema, schema_lage, schema_anordnen, schema_anlegen über dem Beispielprojekt.</summary>
public class WerkzeugSchemaTests
{
    static double Lage(Werkzeuge w, string kz, string achse) =>
        Json(w.SchemaLesen("AN-SCHEMA-SAN"))["knoten"]!.AsArray().Single(k => k!["kennzeichen"]!.GetValue<string>() == kz)![achse]!.GetValue<double>();

    [Fact]
    public void SchemaLage_TrockenlaufSetzenAnordnenRueckgaengig()
    {
        var p = Beispiel();
        var w = new Werkzeuge(p);
        double x0 = Lage(w, "WC-E.02", "x");

        var t = Json(w.SchemaLageSetzen("AN-SCHEMA-SAN", "WC-E.02", 500, 300, trockenlauf: true));
        Assert.True(t["angenommen"]!.GetValue<bool>(), t.ToJsonString());
        Assert.True(t["trockenlauf"]!.GetValue<bool>());
        Assert.Equal(x0, Lage(w, "WC-E.02", "x"));

        var s = Json(w.SchemaLageSetzen("AN-SCHEMA-SAN", "WC-E.02", 500, 300));
        Assert.True(s["angenommen"]!.GetValue<bool>(), s.ToJsonString());
        Assert.Equal(500, Lage(w, "WC-E.02", "x"));
        Assert.Equal(300, Lage(w, "WC-E.02", "y"));

        var a = Json(w.SchemaAnordnenWerkzeug("AN-SCHEMA-SAN", alle: true));
        Assert.True(a["angenommen"]!.GetValue<bool>(), a.ToJsonString());
        Assert.Equal(x0, Lage(w, "WC-E.02", "x"));

        Json(w.Rueckgaengig());
        Assert.Equal(500, Lage(w, "WC-E.02", "x"));
        var z = Json(w.SchemaLageSetzen("AN-SCHEMA-SAN", "WC-E.02", zuruecksetzen: true));
        Assert.True(z["angenommen"]!.GetValue<bool>(), z.ToJsonString());
        Assert.Equal(x0, Lage(w, "WC-E.02", "x"));
    }

    [Fact]
    public void SchemaAnlegen_VorlageWasser_TrockenlaufUndAnlegen()
    {
        var p = Beispiel();
        var w = new Werkzeuge(p);
        var t = Json(w.SchemaAnlegenWerkzeug("wasser", trockenlauf: true));
        Assert.True(t["angenommen"]!.GetValue<bool>(), t.ToJsonString());
        Assert.Null(p.Modell.Finde("AN-SCHEMA-WASSER"));
        var e = Json(w.SchemaAnlegenWerkzeug("wasser", blickrichtung: "ost", wohnungsbloecke: true));
        Assert.True(e["angenommen"]!.GetValue<bool>(), e.ToJsonString());
        // das Beispielprojekt hat nur Trinkwasser kalt: Warmwasser und Zirkulation nennt der Hinweis
        Assert.Contains("Trinkwasser warm", e.ToJsonString());
        var sch = Json(w.SchemaLesen("AN-SCHEMA-WASSER"));
        Assert.Equal("ost", sch["blickrichtung"]!.GetValue<string>());
        Assert.True(sch["wohnungsbloecke"]!.GetValue<bool>());
        Assert.All(sch["linien"]!.AsArray(), l => Assert.Equal("KW", l!["medium"]?.GetValue<string>()));
        var f = Json(w.SchemaAnlegenWerkzeug("lueftung"));
        Assert.False(f["angenommen"]!.GetValue<bool>());
        Assert.Contains("abwasser", f["vorschlag"]!.GetValue<string>());
    }

    [Fact]
    public void Schema_FehlerMitVorschlag()
    {
        var w = new Werkzeuge(Beispiel());
        var f = Json(w.SchemaLesen("AN-GR-EG"));
        Assert.Contains("kein Schema", f["fehler"]!.GetValue<string>());
        Assert.Contains("AN-SCHEMA-SAN", f["vorschlag"]!.GetValue<string>());
        var b = Json(w.SchemaLageSetzen("AN-SCHEMA-SAN", "KW-S1-1", 1, 1));   // Leitung: folgt ihren Endknoten
        Assert.False(b["angenommen"]!.GetValue<bool>());
        Assert.Contains("Endknoten", b["vorschlag"]!.GetValue<string>());
    }
}
