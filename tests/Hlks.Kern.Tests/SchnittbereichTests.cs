using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.RaumgrenzenHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Nachbearbeitung N13/N14: Ansichtseigenschaften schnittbereich und startansicht (Form, Speichern, Rückgängig) und der Befehl
/// ansicht_raum_zeigen (Schnittbereich aus Räumen mit Rand, Planwinkel, mehrere Räume, 3D-Ansicht anlegen, Trockenlauf). Testgebäude frei gewählt
/// (keine Normwerte): Rechteck 4 × 3 m, Wände 200 mm; gedreht: 10 × 6 m um 37°.</summary>
public class SchnittbereichTests
{
    static JsonObject Bereich(bool aktiv = true, double[]? min = null, double[]? max = null, double winkel = 0) => new()
    {
        ["aktiv"] = aktiv,
        ["min"] = new JsonArray((min ?? [0, 0, 0]).Select(x => (JsonNode)JsonValue.Create(x)!).ToArray()),
        ["max"] = new JsonArray((max ?? [1000, 2000, 3000]).Select(x => (JsonNode)JsonValue.Create(x)!).ToArray()),
        ["winkel"] = winkel,
    };

    static Projekt MitAnsicht()
    {
        var p = Leer();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Ansicht", ["kennzeichen"] = "AN-3D", ["eigenschaften"] = new JsonObject { ["art"] = "3d" } }));
        return p;
    }

    /// <summary>Gebäude mit einem Raum 4 × 3 m (Achsen 0 … 4000 × 0 … 3000, Wände 200 → Innenmass 3800 × 2800 von (100, 100)), Geschoss EG kote 0, Höhe 3000.</summary>
    static (Projekt P, string Raum) Einfach(double? lichteHoehe = 2500)
    {
        var p = Leer();
        Geschoss(p, "EG", 0, 3000);
        Waende(p, "EG", 0, 0, 4000, 3000, 200);
        return (p, Raum(p, "EG", 2000, 1500, lichteHoehe));
    }

    static JsonObject Zeige(Projekt p, object raum, string? ansicht = null, double? rand = null, bool trocken = false, bool ok = true)
    {
        var par = new JsonObject { ["raum"] = raum is string s ? s : new JsonArray(((string[])raum).Select(x => (JsonNode)x).ToArray()) };
        if (ansicht is not null) par["ansicht"] = ansicht;
        if (rand is double r) par["rand"] = r;
        var e = p.Ausfuehren("ansicht_raum_zeigen", par, trockenlauf: trocken);
        if (ok) Ok(e);
        return p.Modell.Finde("AN-3D")?.Eigenschaft("schnittbereich")?.Inhalt as JsonObject ?? new JsonObject();
    }

    static double[] Zahlen(JsonNode? n) => ((JsonArray)n!).Select(x => x!.GetValue<double>()).ToArray();

    // ------------------------------------------------------------------ Eigenschaft schnittbereich

    [Fact]
    public void Schnittbereich_Speichern_Normalisiert_Und_RueckgaengigNimmtEsZurueck()
    {
        var p = MitAnsicht();
        Eigenschaften(p, "AN-3D", new JsonObject { ["schnittbereich"] = Bereich(true, [100.04, -200, 0], [5000, 4000, 3200.06], 397) });
        var w = p.Modell.Finde("AN-3D")!.Eigenschaft("schnittbereich")!;
        var o = (JsonObject)w.Inhalt!;
        Assert.True(o["aktiv"]!.GetValue<bool>());
        Assert.Equal([100, -200, 0], Zahlen(o["min"]));
        Assert.Equal([5000, 4000, 3200.1], Zahlen(o["max"]));
        Assert.Equal(37, o["winkel"]!.GetValue<double>(), 6);   // 397° = 37° (−180 … 180)
        Ok(p.Rueckgaengig());
        Assert.Null(p.Modell.Finde("AN-3D")!.Eigenschaft("schnittbereich"));
    }

    [Fact]
    public void Schnittbereich_AusschaltenBehaeltDenQuader_NullEntfernt()
    {
        var p = MitAnsicht();
        Eigenschaften(p, "AN-3D", new JsonObject { ["schnittbereich"] = Bereich(true) });
        Eigenschaften(p, "AN-3D", new JsonObject { ["schnittbereich"] = Bereich(false) });
        var o = (JsonObject)p.Modell.Finde("AN-3D")!.Eigenschaft("schnittbereich")!.Inhalt!;
        Assert.False(o["aktiv"]!.GetValue<bool>());
        Assert.Equal([1000, 2000, 3000], Zahlen(o["max"]));
        Eigenschaften(p, "AN-3D", new JsonObject { ["schnittbereich"] = null });
        Assert.Null(p.Modell.Finde("AN-3D")!.Eigenschaft("schnittbereich"));
    }

    [Theory]
    [InlineData("vertauscht")]
    [InlineData("keineZahl")]
    [InlineData("zweiWerte")]
    [InlineData("winkelText")]
    [InlineData("aktivText")]
    public void Schnittbereich_UngueltigeFormWirdAbgelehntMitVorschlag(string fall)
    {
        var p = MitAnsicht();
        var b = Bereich();
        switch (fall)
        {
            case "vertauscht": b["min"] = new JsonArray(2000, 0, 0); break;
            case "keineZahl": b["max"] = new JsonArray("a", 1, 2); break;
            case "zweiWerte": b["max"] = new JsonArray(1000, 2000); break;
            case "winkelText": b["winkel"] = "schräg"; break;
            case "aktivText": b["aktiv"] = "ja"; break;
        }
        var e = p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "AN-3D", ["eigenschaften"] = new JsonObject { ["schnittbereich"] = b } });
        Assert.False(e.Angenommen);
        Assert.Contains("schnittbereich", e.Fehler);
        Assert.False(string.IsNullOrWhiteSpace(e.Vorschlag) && fall is "vertauscht" or "keineZahl" or "zweiWerte");
        Assert.Null(p.Modell.Finde("AN-3D")!.Eigenschaft("schnittbereich"));
    }

    // ------------------------------------------------------------------ Eigenschaft startansicht

    [Fact]
    public void Startansicht_SpeichernUndLesen_MitPositionZielUndProjektion()
    {
        var p = MitAnsicht();
        Eigenschaften(p, "AN-3D", new JsonObject
        {
            ["startansicht"] = new JsonObject { ["azimutGrad"] = 30.12345, ["polarGrad"] = 62, ["position"] = new JsonArray(1000, -9000, 6000), ["ziel"] = new JsonArray(2000, 1500, 1200) },
        });
        var o = (JsonObject)p.Modell.Finde("AN-3D")!.Eigenschaft("startansicht")!.Inhalt!;
        Assert.Equal(30.123, o["azimutGrad"]!.GetValue<double>(), 3);
        Assert.Equal(62, o["polarGrad"]!.GetValue<double>());
        Assert.Equal([1000, -9000, 6000], Zahlen(o["position"]));
        Assert.Equal("perspektive", o["projektion"]!.GetValue<string>());
        // zurücksetzen: null entfernt (Vorgabe Isometrie)
        Eigenschaften(p, "AN-3D", new JsonObject { ["startansicht"] = null });
        Assert.Null(p.Modell.Finde("AN-3D")!.Eigenschaft("startansicht"));
    }

    [Theory]
    [InlineData("""{ "polarGrad": 50 }""", "azimutGrad")]
    [InlineData("""{ "azimutGrad": 10 }""", "polarGrad")]
    [InlineData("""{ "azimutGrad": 10, "polarGrad": 200 }""", "polarGrad")]
    [InlineData("""{ "azimutGrad": 10, "polarGrad": 50, "projektion": "orthogonal" }""", "projektion")]
    [InlineData("""{ "azimutGrad": 10, "polarGrad": 50, "ziel": [1, 2] }""", "ziel")]
    public void Startansicht_UngueltigeFormWirdAbgelehnt(string json, string stichwort)
    {
        var p = MitAnsicht();
        var e = p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "AN-3D", ["eigenschaften"] = new JsonObject { ["startansicht"] = JsonNode.Parse(json) } });
        Assert.False(e.Angenommen);
        Assert.Contains(stichwort, e.Fehler);
    }

    // ------------------------------------------------------------------ ansicht_raum_zeigen

    [Fact]
    public void RaumZeigen_EinRaum_RandHoeheUndLegtDie3DAnsichtAn()
    {
        var (p, raum) = Einfach(2500);
        Assert.Empty(p.Modell.DerKlasse("Ansicht"));
        var e = Ok(p.Ausfuehren("ansicht_raum_zeigen", new JsonObject { ["raum"] = raum }));
        Assert.Contains(e.Aenderungen, a => a.Kennzeichen == "AN-3D" && a.Klasse == "Ansicht");
        Assert.Contains(e.Hinweise, h => h.Contains("angelegt") && h.Contains("AN-3D"));
        Assert.Contains(e.Hinweise, h => h.Contains("Rand 1500"));
        var a3 = p.Modell.Finde("AN-3D")!;
        Assert.Equal("3d", a3.Text("art"));
        var o = (JsonObject)a3.Eigenschaft("schnittbereich")!.Inhalt!;
        Assert.True(o["aktiv"]!.GetValue<bool>());
        Assert.Equal(0, o["winkel"]!.GetValue<double>());
        // Innenmass 100 … 3900 × 100 … 2900, Raum von z 0, 2500 hoch; Rand 1500 in alle Richtungen
        Assert.Equal([100 - 1500, 100 - 1500, 0 - 1500], Zahlen(o["min"]));
        Assert.Equal([3900 + 1500, 2900 + 1500, 2500 + 1500], Zahlen(o["max"]));
        // ein Rückgängig nimmt Ansicht und Bereich zurück
        Ok(p.Rueckgaengig());
        Assert.Empty(p.Modell.DerKlasse("Ansicht"));
    }

    [Fact]
    public void RaumZeigen_OhneLichteHoehe_GeschosshoeheUndEigenerRand()
    {
        var (p, raum) = Einfach(null);
        var o = Zeige(p, raum, rand: 300);
        Assert.Equal([100 - 300, 100 - 300, -300], Zahlen(o["min"]));
        Assert.Equal([3900 + 300, 2900 + 300, 3000 + 300], Zahlen(o["max"]));   // Raumhöhe = Geschosshöhe 3000
    }

    [Fact]
    public void RaumZeigen_RandNull_UndUngueltigerRand()
    {
        var (p, raum) = Einfach(2500);
        var o = Zeige(p, raum, rand: 0);
        Assert.Equal([100, 100, 0], Zahlen(o["min"]));
        Assert.Equal([3900, 2900, 2500], Zahlen(o["max"]));
        var e = p.Ausfuehren("ansicht_raum_zeigen", new JsonObject { ["raum"] = raum, ["rand"] = -5 });
        Assert.False(e.Angenommen);
        Assert.Contains("rand", e.Fehler);
        Assert.False(string.IsNullOrWhiteSpace(e.Vorschlag));
    }

    [Fact]
    public void RaumZeigen_GedrehtesGebaeude_QuaderAmPlanwinkelAusgerichtet()
    {
        // Rechteck 10 × 6 m (Achsen) um 37° gedreht → Planwinkel −37, Quader um +37° gedreht, Masse in den Achsen des Gebäudes
        var p = Leer();
        Geschoss(p, "EG", 0, 3000);
        double[] D(double x, double y) => Ansichtsausrichtung.Drehen([x, y], 37);
        Ok(p.Ausfuehren("wand_zeichnen", new JsonObject
        {
            ["geschoss"] = "EG", ["punkte"] = Pts(D(0, 0), D(10000, 0), D(10000, 6000), D(0, 6000)), ["dicke"] = 200, ["geschlossen"] = true,
        }));
        var c = D(5000, 3000);
        var raum = Raum(p, "EG", c[0], c[1], 2600);
        Assert.Equal(-37, p.Modell.Finde("GEB")!.Zahl("planwinkel")!.Value, 6);
        var o = Zeige(p, raum);
        Assert.Equal(37, o["winkel"]!.GetValue<double>(), 4);
        var min = Zahlen(o["min"]); var max = Zahlen(o["max"]);
        // Innenmass 9800 × 5800 (Wände 200), Rand 1500: Kantenlängen in den Achsen des Gebäudes
        Assert.Equal(9800 + 3000, max[0] - min[0], 1);
        Assert.Equal(5800 + 3000, max[1] - min[1], 1);
        Assert.Equal(2600 + 3000, max[2] - min[2], 1);
        // die Mitte des Quaders (in seinen Achsen) liegt, ins Modell zurückgedreht, auf der Raummitte
        double u = (min[0] + max[0]) / 2, v = (min[1] + max[1]) / 2, th = 37 * Math.PI / 180;
        Assert.Equal(c[0], u * Math.Cos(th) - v * Math.Sin(th), 0);
        Assert.Equal(c[1], u * Math.Sin(th) + v * Math.Cos(th), 0);
        // ohne Drehung (Modellachsen) wäre der Quader deutlich grösser: Hüllrechteck eines um 37° gedrehten 12.8 × 8.8 m grossen Rechtecks
        Assert.True(max[0] - min[0] < 13000);
    }

    [Fact]
    public void RaumZeigen_MehrereRaeume_EinGemeinsamerBereich_AuchAlsEinzelnerText()
    {
        var p = Leer();
        Geschoss(p, "EG", 0, 3000);
        Geschoss(p, "OG", 3000, 3000);
        Waende(p, "EG", 0, 0, 4000, 3000, 200);
        Waende(p, "OG", 6000, 0, 10000, 3000, 200);
        var r1 = Raum(p, "EG", 2000, 1500, 2500);
        var r2 = Raum(p, "OG", 8000, 1500, 2700);
        var o = Zeige(p, new[] { r1, r2 });
        Assert.Equal([100 - 1500, 100 - 1500, 0 - 1500], Zahlen(o["min"]));
        Assert.Equal([9900 + 1500, 2900 + 1500, 3000 + 2700 + 1500], Zahlen(o["max"]));   // r2 liegt im OG: Unterkante 3000
        Assert.Equal(0, o["winkel"]!.GetValue<double>());
        // doppelte Nennung zählt einmal, ein einzelner Text geht auch
        var einzeln = Zeige(p, r1);
        Assert.Equal([3900 + 1500, 2900 + 1500, 2500 + 1500], Zahlen(einzeln["max"]));
        Assert.Equal(einzeln.ToJsonString(), Zeige(p, new[] { r1, r1 }).ToJsonString());
    }

    [Fact]
    public void RaumZeigen_VorhandeneAnsichtWirdBenutzt_FalscheArtUndKeinRaumWerdenAbgelehnt()
    {
        var (p, raum) = Einfach();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Ansicht", ["kennzeichen"] = "AN-3D", ["eigenschaften"] = new JsonObject { ["art"] = "3d" } }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Ansicht", ["kennzeichen"] = "AN-EG", ["geschoss"] = "EG", ["eigenschaften"] = new JsonObject { ["art"] = "grundriss" } }));
        var e = Ok(p.Ausfuehren("ansicht_raum_zeigen", new JsonObject { ["raum"] = raum }));
        Assert.DoesNotContain(e.Aenderungen, a => a.Art == "angelegt");
        Assert.NotNull(p.Modell.Finde("AN-3D")!.Eigenschaft("schnittbereich"));
        Assert.Null(p.Modell.Finde("AN-EG")!.Eigenschaft("schnittbereich"));

        var falsch = p.Ausfuehren("ansicht_raum_zeigen", new JsonObject { ["raum"] = raum, ["ansicht"] = "AN-EG" });
        Assert.False(falsch.Angenommen);
        Assert.Contains("keine 3D-Ansicht", falsch.Fehler);

        var keinRaum = p.Ausfuehren("ansicht_raum_zeigen", new JsonObject { ["raum"] = "EG" });
        Assert.False(keinRaum.Angenommen);
        Assert.Contains("erwartet wird ein Raum", keinRaum.Fehler);
        var leer = p.Ausfuehren("ansicht_raum_zeigen", new JsonObject { ["raum"] = new JsonArray() });
        Assert.False(leer.Angenommen);
    }

    [Fact]
    public void RaumZeigen_Trockenlauf_AendertNichts_BefehlIstImKatalogDerKI()
    {
        var (p, raum) = Einfach();
        var t = Ok(p.Ausfuehren("ansicht_raum_zeigen", new JsonObject { ["raum"] = raum }, trockenlauf: true));
        Assert.True(t.Trockenlauf);
        Assert.NotEmpty(t.Aenderungen);                          // zeigt die Auswirkung: Ansicht angelegt, Bereich gesetzt
        Assert.Empty(p.Modell.DerKlasse("Ansicht"));             // …ändert aber nichts
        Assert.Contains(p.Befehle.Alle, b => b.Name == "ansicht_raum_zeigen" && b.Parameter.Any(x => x.Name == "rand" && x.Typ == "zahl") && b.Parameter.Any(x => x.Name == "raum" && x.Pflicht));
    }
}
