using System.Text.Json.Nodes;

namespace Hlks.Kern.Tests;

/// <summary>Leitungsbezeichnung (Christian Steiner 17.10.2026): Rohre in NW oder DN (einstellbar), Kanäle und
/// Lüftungsrohre in mm; Kanäle und Lüftungsrohre in echter Grösse in Grundriss und 3D.</summary>
public class LeitungsbezeichnungTests
{
    static readonly Katalog Kat = Katalog.Laden(Katalog.StandardOrdner());

    static Element Anlegen(Projekt p, JsonObject o)
    {
        var r = p.Ausfuehren("element_anlegen", o);
        Assert.True(r.Angenommen, $"{o["klasse"]} abgelehnt: {r.Fehler} {r.Vorschlag}");
        return p.Modell.Finde(o["kennzeichen"]!.GetValue<string>())!;
    }

    static JsonArray Punkte(params double[][] pts) => new(pts.Select(q => (JsonNode)new JsonArray(q.Select(v => (JsonNode)JsonValue.Create(v)).ToArray())).ToArray());

    /// <summary>Projekt mit EG, Zuluftsystem und Grundriss AN-L (Lüftung, fein) bzw. AN-S (Sanitär, fein).</summary>
    static Projekt Neu()
    {
        var p = Projekt.Anlegen(Kat, null, "Leitungstest", "T", "pc");
        Anlegen(p, new JsonObject { ["klasse"] = "Gebaeude", ["kennzeichen"] = "GEB-1" });
        Anlegen(p, new JsonObject { ["klasse"] = "Geschoss", ["kennzeichen"] = "EG", ["teilVon"] = "GEB-1", ["eigenschaften"] = new JsonObject { ["kote"] = 0, ["hoehe"] = 2900 } });
        Anlegen(p, new JsonObject { ["klasse"] = "Anlage", ["kennzeichen"] = "A-L", ["eigenschaften"] = new JsonObject() });
        Anlegen(p, new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "S-ZUL", ["teilVon"] = "A-L", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Zuluft" } });
        Anlegen(p, new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "S-KW", ["teilVon"] = "A-L", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Trinkwasser kalt" } });
        foreach (var (kz, gw) in new[] { ("AN-L", "lueftung"), ("AN-S", "sanitaer") })
            Anlegen(p, new JsonObject
            {
                ["klasse"] = "Ansicht", ["kennzeichen"] = kz, ["geschoss"] = "EG",
                ["eigenschaften"] = new JsonObject { ["art"] = "grundriss", ["massstab"] = 50, ["detailgrad"] = "fein", ["gewerke"] = new JsonArray(gw) },
            });
        return p;
    }

    static Element Leitung(Projekt p, string klasse, string kz, string system, JsonObject eig) => Anlegen(p, new JsonObject
    {
        ["klasse"] = klasse, ["kennzeichen"] = kz, ["system"] = system, ["geschoss"] = "EG", ["eigenschaften"] = eig,
        ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Punkte([0, 0, 2500], [4000, 0, 2500], [4000, 3000, 2500]) },
    });

    static string? Text(Projekt p, string ansicht, Element e) =>
        Grafik.Plan(p, ansicht).Teile.Single(t => t.Id == e.Id).Primitive.SingleOrDefault(x => x.Art == "text")?.Text;

    [Fact]
    public void Rohr_ZeigtNwAlsVorgabe_DnNurAufEinstellung()
    {
        var p = Neu();
        var r = Leitung(p, "Rohr", "KW-1", "S-KW", new JsonObject { ["dn"] = 32 });
        Assert.Equal("NW 32", Text(p, "AN-S", r));                          // Vorgabe NW (Christian 17.10.2026)
        Anlegen(p, new JsonObject { ["klasse"] = "Projektinformation", ["kennzeichen"] = "PI-1", ["eigenschaften"] = new JsonObject { ["rohrbezeichnung"] = "DN" } });
        Assert.Equal("DN 32", Text(p, "AN-S", r));                          // Projekt stellt auf DN
        Assert.True(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "AN-S", ["eigenschaften"] = new JsonObject { ["rohrbezeichnung"] = "NW" } }).Angenommen);
        Assert.Equal("NW 32", Text(p, "AN-S", r));                          // Ansicht vor Projekt
    }

    [Fact]
    public void Kanal_RechteckUndRund_InMm()
    {
        var p = Neu();
        var rechteck = Leitung(p, "Kanal", "KA-1", "S-ZUL", new JsonObject { ["breite"] = 400, ["hoehe"] = 200 });
        var rund = Leitung(p, "Kanal", "KA-2", "S-ZUL", new JsonObject { ["durchmesser"] = 250 });
        var rohr = Leitung(p, "Rohr", "ZUL-1", "S-ZUL", new JsonObject { ["dn"] = 160 });   // Lüftungsrohr (Wickelfalz)
        Assert.Equal("400×200", Text(p, "AN-L", rechteck));
        Assert.Equal("Ø 250", Text(p, "AN-L", rund));
        Assert.Equal("Ø 160", Text(p, "AN-L", rohr));
    }

    [Fact]
    public void Kanal_InEchterBreiteImGrundriss()
    {
        var p = Neu();
        var k = Leitung(p, "Kanal", "KA-1", "S-ZUL", new JsonObject { ["breite"] = 400, ["hoehe"] = 200 });
        var poly = Grafik.Plan(p, "AN-L").Teile.Single(t => t.Id == k.Id).Primitive.First(x => x.Art == "polygon");
        var breite = Math.Abs(poly.Punkte[0][1] - poly.Punkte[3][1]);    // erstes Segment liegt auf der x-Achse
        Assert.Equal(400, breite, 1);
        Assert.Equal("#d0312d", poly.Stil.Farbe);                           // Zuluft rot (SIA 410/1, Katalog)
    }

    [Fact]
    public void Kanal_Im3D_QuaderMitBreiteUndHoehe()
    {
        var p = Neu();
        var k = Leitung(p, "Kanal", "KA-1", "S-ZUL", new JsonObject { ["breite"] = 400, ["hoehe"] = 200 });
        var teile = Grafik.Koerper(p, [k.Id]).Single().Teile;
        var erstes = teile.First(t => t.Art == "quader");
        Assert.Equal([4000, 400, 200], erstes.Masse!);
        Assert.Equal(0, erstes.Drehung!.Value, 3);
        Assert.Contains(teile, t => t.Art == "quader" && Math.Abs(t.Drehung!.Value - 90) < 1e-6 && t.Masse![0] == 3000);
    }
}
