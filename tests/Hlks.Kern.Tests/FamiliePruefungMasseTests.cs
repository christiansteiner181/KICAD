using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.FamiliePruefungHilfe;
using static Hlks.Kern.Tests.GrundlagenHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 2, B4: Massvergleich je Typ (Abweichungsliste, Abfrage familie_masse) und Kontrollbild familie_vergleichen –
/// PDF mit Passpunkten bzw. über die Masslinie eingepasst (schematisch), DXF massstäblich mit geometrischem Vergleich, 3D-Modell
/// (Hersteller-IFC) mit Anschlusslage gegen Port. Alle Unterlagen synthetisch mit bekannten, fiktiven Werten.</summary>
public class FamiliePruefungMasseTests
{
    static JsonObject Zeile(JsonObject abf, string typ, string mass) =>
        (JsonObject)((JsonArray)abf["typen"]!).Single(t => t!["typ"]!.GetValue<string>() == typ)!["abweichungen"]!.AsArray().Single(z => z!["mass"]!.GetValue<string>() == mass)!;

    // ------------------------------------------------------------------ Massvergleich

    [Fact]
    public void Massvergleich_je_Typ_ein_falsch_modellierter_Typ_gibt_Abweichung()
    {
        using var o = new Ordner();
        var p = ProjektB4();
        var pdf = Ablegen(p, UnterlagenHilfe.TestPdf(o));
        var d = Ventil(pdf);
        TypWert(d, "V-25", "L", 90);   // ohne Quelle eingetragen, die Tabelle sagt 87 – nur der Massvergleich über die Spalte L sieht es
        ((JsonArray)d["masse"]!).Add(new JsonObject { ["name"] = "B", ["ansicht"] = "oben", ["von"] = new JsonArray(0, "-B / 2", 0), ["bis"] = new JsonArray(0, "B / 2", 0), ["richtung"] = "y", ["parameter"] = "B" });
        var e = Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d.DeepClone() }));
        Assert.Contains(e.Hinweise, h => h.StartsWith("Massvergleich: 8 von 9") && h.Contains("L Typ V-25 (Soll 87, Ist 90)"));

        var abf = Abfrage(p, "familie_masse", new JsonObject { ["familie"] = "FAM-1" });
        Assert.Equal(1, abf["ausserhalb"]!.GetValue<int>());
        var z = Zeile(abf, "V-25", "L");
        Assert.Equal(87, z["soll"]!.GetValue<double>());
        Assert.Equal(90, z["ist"]!.GetValue<double>());
        Assert.Equal(3, z["abweichung"]!.GetValue<double>());
        Assert.Equal(1, z["toleranz"]!.GetValue<double>());
        Assert.False(z["bestanden"]!.GetValue<bool>());
        Assert.Equal(Zelle(pdf, 3, 3), z["befund"]!.GetValue<string>());   // Spalte L, Zeile von V-25
        Assert.Contains("datenblatt.pdf, Seite 1", z["quelle"]!.GetValue<string>());
        Assert.True(Zeile(abf, "V-15", "L")["bestanden"]!.GetValue<bool>());
        Assert.Equal(46, Zeile(abf, "V-20", "H")["soll"]!.GetValue<double>());   // Massbezug H nennt die Spalte H
        var b = Zeile(abf, "V-15", "B");
        Assert.Equal("Typwert «B»", b["quelle"]!.GetValue<string>());
        Assert.Equal(30, b["ist"]!.GetValue<double>());
        Assert.True(b["bestanden"]!.GetValue<bool>());
        // nur ein Typ
        var nur = Abfrage(p, "familie_masse", new JsonObject { ["familie"] = "FAM-1", ["typ"] = "V-20" });
        Assert.Equal("V-20", ((JsonArray)nur["typen"]!).Single()!["typ"]!.GetValue<string>());
        Assert.Throws<HlksFehler>(() => Abfrage(p, "familie_masse", new JsonObject { ["familie"] = "FAM-1", ["typ"] = "V-99" }));
        // die Freigabe sperrt
        Assert.Contains("mass «L» Typ «V-25»", Nein(p.Ausfuehren("familie_freigeben", new JsonObject { ["familie"] = "FAM-1" })).Fehler);
    }

    [Fact]
    public void Durchmesser_Toleranz_1_mm()
    {
        var f = FamilieDef.Lesen(FamilieTestHilfe.Mini(parameter: """[{ "name": "D", "art": "typ", "datentyp": "laenge", "wert": 630 }]""",
            mehr: """, "masse": [{ "name": "ØD", "parameter": "D" }, { "name": "L", "parameter": "D" }]"""), Kat);
        Assert.True(f.Gueltig, string.Join("; ", f.Fehler));
        Assert.True(FamilienMasse.IstDurchmesser(f.Masse[0], f));
        Assert.False(FamilienMasse.IstDurchmesser(f.Masse[1], f));
    }

    // ------------------------------------------------------------------ Kontrollbild

    static int RotePunkte(byte[] png)
    {
        var pfad = Path.Combine(Path.GetTempPath(), $"hlks_b4_{Guid.NewGuid():N}.png");
        try
        {
            File.WriteAllBytes(pfad, png);
            var (w, h, rgb) = PngLesen(pfad);
            int n = 0;
            for (int i = 0; i < w * h; i++) if (rgb[3 * i] > 170 && rgb[3 * i + 1] < 90 && rgb[3 * i + 2] < 90) n++;
            return n;
        }
        finally { File.Delete(pfad); }
    }

    [Fact]
    public void Kontrollbild_PDF_mit_Passpunkten_PNG_und_Abweichungsliste_schematisch()
    {
        using var o = new Ordner();
        var p = ProjektB4();
        var pdf = Ablegen(p, UnterlagenHilfe.TestPdf(o));
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Ventil(pdf) }));
        var par = new JsonObject
        {
            ["familie"] = "FAM-1", ["typ"] = "V-15", ["unterlage"] = "UL-1", ["seite"] = 1, ["ansicht"] = "vorne", ["breite"] = 600,
            ["passpunkte"] = JsonNode.Parse("""[{ "familie": [0, 0], "unterlage": [100, 342] }, { "familie": [67, 0], "unterlage": [300, 342] }]"""),
        };
        var r = FamilienKontrollbild.Erzeugen(p, par, out var png);
        Assert.True(png.Length > 1000);
        Assert.Equal(0x89, png[0]); Assert.Equal((byte)'P', png[1]);
        Assert.InRange(r["breite"]!.GetValue<int>(), 598, 602);
        Assert.Equal(png.Length, r["bytes"]!.GetValue<int>());
        Assert.True(RotePunkte(png) > 50, "die Familie ist rot gezeichnet");
        Assert.Equal("passpunkte", r["einpassung"]!["quelle"]!.GetValue<string>());
        Assert.Equal(200.0 / 67, r["einpassung"]!["massstab"]!.GetValue<double>(), 4);
        Assert.Equal(0, r["einpassung"]!["drehung"]!.GetValue<double>(), 6);
        Assert.False(r["massstaeblich"]!.GetValue<bool>());
        Assert.Contains("schematisch, Überlagerung nur Anschauung", r["hinweis"]!.GetValue<string>());
        Assert.Null(r["geometrisch"]);
        var ab = (JsonArray)r["abweichungen"]!;
        Assert.Equal(["L", "H"], ab.Select(z => z!["mass"]!.GetValue<string>()));
        Assert.All(ab, z => { Assert.Equal("V-15", z!["typ"]!.GetValue<string>()); Assert.True(z["bestanden"]!.GetValue<bool>()); });
        Assert.True(r["masseBestanden"]!.GetValue<bool>());
        // REST: dasselbe Bild als base64
        var rest = Abfrage(p, "familie_vergleichen", par);
        Assert.Equal(png.Length, Convert.FromBase64String(rest["base64"]!.GetValue<string>()).Length);
        // ohne Passpunkte und ohne passenden Massbezug: Fehler mit Vorschlag
        par.Remove("passpunkte");
        var ex = Assert.Throws<HlksFehler>(() => FamilienKontrollbild.Erzeugen(p, par, out _));
        Assert.Contains("passpunkte", ex.Vorschlag);
    }

    [Fact]
    public void Kontrollbild_PDF_ueber_die_Masslinie_eingepasst()
    {
        using var o = new Ordner();
        var p = ProjektB4();
        var pdf = Ablegen(p, UnterlagenHilfe.TestPdf(o));
        var d = Ventil(pdf);
        ((JsonArray)d["masse"]![0]!["quellen"]!).Add(Id(pdf, "mass"));   // Masslinie «L» (x 100–300 pt)
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d.DeepClone() }));
        foreach (var typ in new[] { "V-15", "V-25" })   // je Typ an demselben Mass eingepasst
        {
            var r = FamilienKontrollbild.Erzeugen(p, new JsonObject { ["familie"] = "FAM-1", ["typ"] = typ, ["unterlage"] = "UL-1", ["ansicht"] = "vorne", ["breite"] = 400 }, out var png);
            Assert.True(png.Length > 0);
            Assert.StartsWith("Massbezug «L»", r["einpassung"]!["quelle"]!.GetValue<string>());
            Assert.Equal(200.0 / (typ == "V-15" ? 67 : 87), r["einpassung"]!["massstab"]!.GetValue<double>(), 3);
            Assert.True(r["masseBestanden"]!.GetValue<bool>());
        }
    }

    [Fact]
    public void Kontrollbild_DXF_massstaeblich_mit_geometrischem_Vergleich()
    {
        using var o = new Ordner();
        var p = ProjektB4();
        var pdf = Ablegen(p, UnterlagenHilfe.TestPdf(o));
        var dxf = Ablegen(p, DxfAnsicht(o, fehlkante: true));
        var d = Ventil(pdf);
        ((JsonArray)d["masse"]![0]!["quellen"]!).Add(Mass(dxf, 67));   // DXF-Bemassung 67 mm: Einpassung und zweite Quelle
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d.DeepClone() }));
        var r = FamilienKontrollbild.Erzeugen(p, new JsonObject { ["familie"] = "FAM-1", ["typ"] = "V-15", ["unterlage"] = "ansicht.dxf", ["ansicht"] = "vorne", ["breite"] = 500 }, out var png);
        Assert.True(png.Length > 0);
        Assert.True(RotePunkte(png) > 20);
        Assert.True(r["massstaeblich"]!.GetValue<bool>());
        Assert.Null(r["hinweis"]);
        Assert.StartsWith("Massbezug «L»", r["einpassung"]!["quelle"]!.GetValue<string>());
        Assert.Equal(1, r["einpassung"]!["massstab"]!.GetValue<double>(), 9);
        Assert.Equal(0, r["einpassung"]!["drehung"]!.GetValue<double>(), 9);
        var g = (JsonObject)r["geometrisch"]!;
        var k = (JsonObject)g["kanten"]!;
        Assert.Equal(5, k["anzahl"]!.GetValue<int>());          // vier Kanten des Gehäuses und die falsche bei x = 72 (Bemassung zählt nicht)
        Assert.Equal(4, k["bestanden"]!.GetValue<int>());
        Assert.Equal(1, k["ausserhalb"]!.GetValue<int>());
        Assert.Equal(5, k["groessterAbstand"]!.GetValue<double>(), 6);
        var schlecht = (JsonObject)k["liste"]![0]!;
        Assert.False(schlecht["bestanden"]!.GetValue<bool>());
        Assert.Equal(5, schlecht["abstand"]!.GetValue<double>(), 6);
        Assert.Equal("GEHAEUSE", schlecht["layer"]!.GetValue<string>());
        var m = (JsonObject)g["masse"]!.AsArray().Single()!;
        Assert.Equal(Mass(dxf, 67), m["befund"]!.GetValue<string>());
        Assert.True(m["bestanden"]!.GetValue<bool>());
        Assert.All(r["abweichungen"]!.AsArray(), z => Assert.True(z!["bestanden"]!.GetValue<bool>()));
        // Kontrollbild ohne die falsche Kante: alles innerhalb
        var dxf2 = Ablegen(p, DxfAnsicht(o, fehlkante: false, name: "ansicht2.dxf"));
        var r2 = FamilienKontrollbild.Erzeugen(p, new JsonObject
        {
            ["familie"] = "FAM-1", ["typ"] = "V-15", ["unterlage"] = "ansicht2.dxf", ["ansicht"] = "vorne",
            ["passpunkte"] = JsonNode.Parse("""[[[0, 0], [0, 0]], [[67, 35], [67, 35]]]"""),
        }, out _);
        Assert.Equal(0, r2["geometrisch"]!["kanten"]!["ausserhalb"]!.GetValue<int>());
        Assert.Equal(4, r2["geometrisch"]!["kanten"]!["bestanden"]!.GetValue<int>());
    }

    [Fact]
    public void Kontrollbild_3D_Anschlusslage_gegen_Port()
    {
        using var o = new Ordner();
        var p = ProjektB4();
        Ablegen(p, UnterlagenHilfe.TestIfc(o));   // Quader 100 × 60 × 40, Ports A (−50, 0, 20) und B (50, 0, 20)
        JsonObject Familie(double xb) => FamilieTestHilfe.JsonAus($$"""
            { "format": 1, "name": "Ventil 3D (fiktiv)", "kategorie": "Ventil", "parameter": [],
              "typen": [{ "name": "V-15", "werte": {} }],
              "koerper": [{ "id": "k", "art": "quader", "von": [-50, -30, 0], "bis": [50, 30, 40] }],
              "anschluesse": [
                { "name": "A", "medium": "*", "fluss": "rein", "art": "rohr", "dn": 15, "lage": [-50, 0, 20], "richtung": [-1, 0, 0] },
                { "name": "B", "medium": "*", "fluss": "raus", "art": "rohr", "dn": 15, "lage": [{{xb}}, 0, 20], "richtung": [1, 0, 0] }] }
            """);
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Familie(50) }));
        var par = new JsonObject { ["familie"] = "FAM-1", ["typ"] = "V-15", ["unterlage"] = "ventil.ifc", ["ansicht"] = "vorne", ["breite"] = 400 };
        var r = FamilienKontrollbild.Erzeugen(p, par, out var png);
        Assert.True(png.Length > 0);
        Assert.True(r["massstaeblich"]!.GetValue<bool>());
        Assert.StartsWith("Hülle", r["einpassung"]!["quelle"]!.GetValue<string>());
        var g = (JsonObject)r["geometrisch"]!;
        var an = (JsonArray)g["anschluesse"]!;
        Assert.Equal(2, an.Count);
        Assert.All(an, a => { Assert.Equal("port", a!["art"]!.GetValue<string>()); Assert.True(a["bestanden"]!.GetValue<bool>()); Assert.Equal(0, a["abstand"]!.GetValue<double>(), 6); });
        Assert.Equal(0, g["kanten"]!["ausserhalb"]!.GetValue<int>());   // Hülle des Modells = Umriss der Familie
        // Anschluss B 5 mm daneben: ausserhalb der Anschlusstoleranz 2 mm
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Familie(45), ["familie"] = "FAM-1" }));
        var r2 = FamilienKontrollbild.Erzeugen(p, par, out _);
        var b = (JsonObject)r2["geometrisch"]!["anschluesse"]!.AsArray().Single(a => a!["anschluss"]!.GetValue<string>() == "B")!;
        Assert.False(b["bestanden"]!.GetValue<bool>());
        Assert.Equal(5, b["abstand"]!.GetValue<double>(), 6);
        Assert.Equal(2, b["toleranz"]!.GetValue<double>());
    }

    [Fact]
    public void Kontrollbild_3D_Umriss_aus_dem_Netzbefund_auch_von_unten()
    {
        using var o = new Ordner();
        var p = ProjektB4();
        Ablegen(p, UnterlagenHilfe.StlBinaer(o));   // Quader 10 × 20 × 30, Ecke 0/0/0
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = FamilieTestHilfe.JsonAus("""
            { "format": 1, "name": "Quader (fiktiv)", "kategorie": "Ventil", "parameter": [], "typen": [{ "name": "A", "werte": {} }],
              "koerper": [{ "id": "k", "art": "quader", "von": [0, 0, 0], "bis": [10, 20, 30] }], "anschluesse": [] }
            """) }));
        foreach (var ansicht in new[] { "vorne", "links", "oben", "unten" })
        {
            var r = FamilienKontrollbild.Erzeugen(p, new JsonObject { ["familie"] = "FAM-1", ["typ"] = "A", ["unterlage"] = "quader.stl", ["ansicht"] = ansicht, ["breite"] = 300 }, out var png);
            Assert.True(png.Length > 0);
            var k = (JsonObject)r["geometrisch"]!["kanten"]!;
            Assert.True(k["anzahl"]!.GetValue<int>() >= 4, ansicht);
            Assert.Equal(0, k["ausserhalb"]!.GetValue<int>());
            Assert.Equal(0, k["groessterAbstand"]!.GetValue<double>(), 6);
            Assert.Contains(r["hinweise"]!.AsArray(), h => h!.GetValue<string>().Contains("keine Stutzen bzw. Ports"));
        }
    }

    [Fact]
    public void Kontrollbild_auf_einem_Bild_mit_y_nach_unten()
    {
        using var o = new Ordner();
        var p = ProjektB4();
        var pdf = Ablegen(p, UnterlagenHilfe.TestPdf(o));
        Ablegen(p, Bild(o));   // 120 × 80 Bildpunkte, weiss
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Ventil(pdf) }));
        // Familie 0..67 mm auf die Bildpunkte 20..100 (Zeile 60, y nach unten): oben im Bild ist z gross
        var r = FamilienKontrollbild.Erzeugen(p, new JsonObject
        {
            ["familie"] = "FAM-1", ["typ"] = "V-15", ["unterlage"] = "massbild.png", ["ansicht"] = "vorne",
            ["passpunkte"] = JsonNode.Parse("""[{ "familie": [0, 0], "unterlage": [20, 60] }, { "familie": [67, 0], "unterlage": [100, 60] }]"""),
        }, out var png);
        Assert.True(png.Length > 0);
        Assert.Equal(80.0 / 67, r["einpassung"]!["massstab"]!.GetValue<double>(), 6);
        Assert.Equal(0, r["einpassung"]!["drehung"]!.GetValue<double>(), 6);
        Assert.Equal([0, 0, 120, 80], r["bereich"]!.AsArray().Select(x => x!.GetValue<double>()));
        Assert.True(RotePunkte(png) > 20);
        Assert.Contains("schematisch", r["hinweis"]!.GetValue<string>());
    }
}
