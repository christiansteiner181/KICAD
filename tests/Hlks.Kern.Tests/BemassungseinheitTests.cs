using System.Text.Json.Nodes;

namespace Hlks.Kern.Tests;

/// <summary>Einheit der Masszahlen: Vorgabe je Gewerk (Christian Steiner 17.10.2026: Grundriss m, Lüftung mm,
/// Heizung und Sanitär cm), übersteuerbar je Projekt, Ansicht und Masskette.</summary>
public class BemassungseinheitTests
{
    static readonly Katalog Kat = Katalog.Laden(Katalog.StandardOrdner());

    static Element Anlegen(Projekt p, JsonObject o)
    {
        var r = p.Ausfuehren("element_anlegen", o);
        Assert.True(r.Angenommen, $"{o["klasse"]} abgelehnt: {r.Fehler} {r.Vorschlag}");
        return p.Modell.Finde(o["kennzeichen"]!.GetValue<string>())!;
    }

    static Projekt Neu()
    {
        var p = Projekt.Anlegen(Kat, null, "Einheitentest", "T", "pc");
        Anlegen(p, new JsonObject { ["klasse"] = "Gebaeude", ["kennzeichen"] = "GEB-1" });
        Anlegen(p, new JsonObject { ["klasse"] = "Geschoss", ["kennzeichen"] = "EG", ["teilVon"] = "GEB-1", ["eigenschaften"] = new JsonObject { ["kote"] = 0, ["hoehe"] = 2900 } });
        return p;
    }

    static Element Grundriss(Projekt p, string kz, string[] gewerke, string? einheit = null)
    {
        var eig = new JsonObject { ["art"] = "grundriss", ["massstab"] = 50, ["gewerke"] = new JsonArray(gewerke.Select(g => (JsonNode)g).ToArray()) };
        if (einheit is not null) eig["bemassungEinheit"] = einheit;
        return Anlegen(p, new JsonObject { ["klasse"] = "Ansicht", ["kennzeichen"] = kz, ["geschoss"] = "EG", ["eigenschaften"] = eig });
    }

    /// <summary>Masskette 0/0 → 3450/0 in der Ansicht; liefert die Masszahl aus der Plangrafik.</summary>
    static string Masszahl(Projekt p, string ansicht, string kz, string? einheit = null)
    {
        var eig = new JsonObject { ["versatz"] = 1000 };
        if (einheit is not null) eig["einheit"] = einheit;
        var b = Anlegen(p, new JsonObject
        {
            ["klasse"] = "Bemassung", ["kennzeichen"] = kz, ["eigenschaften"] = eig,
            ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = new JsonArray(new JsonArray(0, 0), new JsonArray(3450, 0)) },
            ["bezuege"] = new JsonObject { ["ansicht"] = ansicht },
        });
        return Grafik.Plan(p, ansicht).Teile.Single(t => t.Id == b.Id).Primitive.Single(x => x.Art == "text").Text!;
    }

    [Theory]
    [InlineData(new string[0], "3.45")]                                   // ohne Filter: Grundriss → m
    [InlineData(new[] { "architektur" }, "3.45")]
    [InlineData(new[] { "lueftung", "architektur" }, "3450")]
    [InlineData(new[] { "heizung", "architektur" }, "345")]
    [InlineData(new[] { "sanitaer", "architektur" }, "345")]
    [InlineData(new[] { "lueftung", "sanitaer" }, "3450")]                // erstes HLKS-Gewerk in der Reihenfolge L, H, S
    public void Vorgabe_JeGewerk(string[] gewerke, string erwartet)
    {
        var p = Neu();
        Grundriss(p, "AN-1", gewerke);
        Assert.Equal(erwartet, Masszahl(p, "AN-1", "BM-1"));
    }

    [Fact]
    public void Ansicht_UebersteuertDieVorgabe()
    {
        var p = Neu();
        Grundriss(p, "AN-L", ["lueftung"], einheit: "cm");
        Assert.Equal("345", Masszahl(p, "AN-L", "BM-1"));
        Assert.Equal(("cm", "ansicht"), Bemassungseinheit.Fuer(p.Modell, p.Modell.Finde("AN-L")!));
    }

    [Fact]
    public void Projekt_UebersteuertDieVorgabe_AnsichtUndMasskettenGehenVor()
    {
        var p = Neu();
        Anlegen(p, new JsonObject
        {
            ["klasse"] = "Projektinformation", ["kennzeichen"] = "PI-1",
            ["eigenschaften"] = new JsonObject { ["bemassungEinheiten"] = new JsonObject { ["sanitaer"] = "mm" } },
        });
        Grundriss(p, "AN-S", ["sanitaer"]);
        Grundriss(p, "AN-S2", ["sanitaer"], einheit: "m");
        Assert.Equal("3450", Masszahl(p, "AN-S", "BM-1"));
        Assert.Equal(("mm", "projekt"), Bemassungseinheit.Fuer(p.Modell, p.Modell.Finde("AN-S")!));
        Assert.Equal("3.45", Masszahl(p, "AN-S2", "BM-2"));                // Ansicht vor Projekt
        Assert.Equal("345", Masszahl(p, "AN-S2", "BM-3", einheit: "cm"));  // Masskette vor allem
    }

    [Fact]
    public void Vorgabetabelle_EntsprichtDemEntscheid()
    {
        Assert.Equal("m", Bemassungseinheit.Vorgabe["architektur"]);
        Assert.Equal("mm", Bemassungseinheit.Vorgabe["lueftung"]);
        Assert.Equal("cm", Bemassungseinheit.Vorgabe["heizung"]);
        Assert.Equal("cm", Bemassungseinheit.Vorgabe["sanitaer"]);
    }
}
