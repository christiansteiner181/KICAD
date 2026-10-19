using System.Diagnostics;
using Xunit.Abstractions;

using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.GrundlagenHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Rauchtests mit den echten Dateien von Bernstrasse 42 (DWG Architekt in Metern, DWG Sanitär in mm, DXF Schema):
/// überspringen sich, wenn %USERPROFILE%\HLKS_CAD_testdaten fehlt. Die Dateien werden nie ins Repo kopiert.</summary>
public class GrundlagenEchtTests(ITestOutputHelper aus)
{
    [Fact]
    public void ArchitektDwg_wirdGelesenGezeichnetUndKalibriert()
    {
        var datei = Echt(@"bernstrasse42\architekt_eg.dwg");
        if (datei is null) return;   // Datei fehlt auf diesem Rechner: Test überspringt sich
        var p = Projekt();
        var zeit = Stopwatch.StartNew();
        var e = Ok(Einfuegen(p, datei!));
        var einfuegen = zeit.ElapsedMilliseconds;
        foreach (var h in e.Hinweise) aus.WriteLine("Hinweis: " + h);
        var ul = Unterlage(p);
        Assert.Equal("dwg", ul.Text("art"));
        Assert.Equal(1000, UnterlagenLage.Lesen(ul).Faktor);   // Meter
        Assert.Equal(1000, ul.Eigenschaft("transformation")!.Inhalt!["erwartung"]!.GetValue<double>());

        var z = CadZeichnung.Laden(p, ul);
        var nachArt = z.Primitive.GroupBy(x => x.Art).ToDictionary(x => x.Key, x => x.Count());
        aus.WriteLine($"einfügen {einfuegen} ms; Primitive {z.Primitive.Count} ({string.Join(", ", nachArt.Select(x => $"{x.Key} {x.Value}"))}); Layer {z.Layer.Count}; nicht dargestellt {string.Join(", ", z.NichtDargestellt.Select(x => $"{x.Key} {x.Value}"))}");
        aus.WriteLine($"Umriss {string.Join(", ", z.Umriss!.Select(v => v.ToString("0.0")))}; Ausdehnung {string.Join(", ", z.Ausdehnung!.Select(v => v.ToString("0.0")))}");
        Assert.True(z.Primitive.Count(x => x.Art == 'l') > 2000, "Linien");
        Assert.True(z.Primitive.Count(x => x.Art == 't') > 300, "Texte (MText und Bemassungstexte)");
        Assert.True(z.Primitive.Count(x => x.Art == 'b') > 60, "Bögen");
        Assert.True(z.Umriss![2] - z.Umriss[0] < 200, "Hauptteil in Metern, ohne die weit abgelegene Legende");

        // Plangrafik: Zeichnen dauert nicht lange, Texte in Papier-mm
        zeit.Restart();
        var g = PlanGrundlage(p);
        aus.WriteLine($"Plangrafik {zeit.ElapsedMilliseconds} ms, {Grundlage(g).Count} Primitive");
        var prim = Grundlage(g);
        Assert.Equal(z.Primitive.Count, prim.Count);
        Assert.All(prim, x => Assert.Equal(GrundlagenCad.Grau, x.Stil.Farbe));
        var text = prim.First(x => x.Art == "text" && x.Text!.Contains("Gang"));
        Assert.InRange(text.Groesse!.Value, 1.5, 3);   // 0.1 m hoch bei 1:50 = 2 mm auf dem Papier

        // Layer ausblenden
        var layer = z.Layer.First(l => l.Contains("Aussenw"));
        Ok(p.Ausfuehren("element_aendern", new System.Text.Json.Nodes.JsonObject
        {
            ["element"] = "UL-1", ["eigenschaften"] = new System.Text.Json.Nodes.JsonObject { ["ebenenAus"] = new System.Text.Json.Nodes.JsonArray(layer) },
        }));
        var weniger = Grundlage(PlanGrundlage(p)).Count;
        Assert.True(weniger < prim.Count, $"{layer} ausgeblendet: {weniger} < {prim.Count}");

        // Plan-PDF mit der Unterlage (Vektoren, grau): Hauptteil im Ausschnitt, ohne die weit abgelegene Legende
        Ok(p.Ausfuehren("element_aendern", new System.Text.Json.Nodes.JsonObject
        {
            ["element"] = "UL-1", ["eigenschaften"] = new System.Text.Json.Nodes.JsonObject { ["ebenenAus"] = new System.Text.Json.Nodes.JsonArray("805 Plankopf Legenden", "255 Plangrafiken") },
        }));
        Ok(p.Ausfuehren("planblatt_anlegen", new System.Text.Json.Nodes.JsonObject { ["nummer"] = "T-1", ["titel"] = "Unterlage", ["format"] = "A1" }));
        Ok(p.Ausfuehren("element_aendern", new System.Text.Json.Nodes.JsonObject
        {
            ["element"] = "AN-EG", ["eigenschaften"] = new System.Text.Json.Nodes.JsonObject
            {
                ["massstab"] = 100,
                ["ausschnitt"] = new System.Text.Json.Nodes.JsonObject { ["minx"] = -6000, ["miny"] = -64000, ["maxx"] = 19000, ["maxy"] = -20000 },
            },
        }));
        Ok(p.Ausfuehren("ansicht_platzieren", new System.Text.Json.Nodes.JsonObject { ["planblatt"] = "PB-1", ["ansicht"] = "AN-EG", ["lage"] = new System.Text.Json.Nodes.JsonArray(420, 420) }));
        zeit.Restart();
        var bytes = Planausgabe.Pdf(p, "PB-1", grafik: (pr, a) => PlanGrundlage(pr, a));
        aus.WriteLine($"Plan-PDF {zeit.ElapsedMilliseconds} ms, {bytes.Length / 1024} KB");
        Assert.True(bytes.Length > 50_000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public void SanitaerDwg_Millimeter_BloeckeUndEllipsen()
    {
        var datei = Echt(@"bernstrasse42\sanitaer_eg.dwg");
        if (datei is null) return;   // Datei fehlt auf diesem Rechner: Test überspringt sich
        var p = Projekt();
        var zeit = Stopwatch.StartNew();
        var e = Ok(Einfuegen(p, datei!));
        foreach (var h in e.Hinweise) aus.WriteLine("Hinweis: " + h);
        var ul = Unterlage(p);
        Assert.Equal(1, UnterlagenLage.Lesen(ul).Faktor);   // Millimeter
        var z = CadZeichnung.Laden(p, ul);
        aus.WriteLine($"einfügen {zeit.ElapsedMilliseconds} ms; Primitive {z.Primitive.Count}; Layer {string.Join(" | ", z.Layer)}; nicht dargestellt {string.Join(", ", z.NichtDargestellt.Select(x => $"{x.Key} {x.Value}"))}");
        // 40 Inserts lösen sich in viele Primitive auf (mehr als der Modellbereich Entitäten hat)
        Assert.True(z.Primitive.Count > 1000, $"{z.Primitive.Count}");
        Assert.True(z.Primitive.Count(x => x.Art == 'g' || x.Art == 'p') > 64, "Ellipsen als Polylinien");
        Assert.Contains(z.Primitive, x => x.Art == 't' && x.Text == "WAS-H");
    }

    [Fact]
    public void SchemaDxf_Millimeter_TextGeschossbezeichnungen()
    {
        var datei = Echt(@"bernstrasse42\schema_wasser.dxf");
        if (datei is null) return;   // Datei fehlt auf diesem Rechner: Test überspringt sich
        var p = Projekt();
        var e = Ok(Einfuegen(p, datei!));
        foreach (var h in e.Hinweise) aus.WriteLine("Hinweis: " + h);
        var ul = Unterlage(p);
        Assert.Equal("dxf", ul.Text("art"));
        var z = CadZeichnung.Laden(p, ul);
        aus.WriteLine($"Primitive {z.Primitive.Count}; Layer {string.Join(" | ", z.Layer)}");
        Assert.Equal(139, z.Primitive.Count(x => x.Art == 't'));
        Assert.Equal(113, z.Primitive.Count(x => x.Art is 'l' or 'p'));   // 140 Linien, davon 27 ohne Länge (Strangsymbole)
        Assert.Contains(z.Primitive, x => x.Art == 't' && x.Text == "2.UG");
    }
}
