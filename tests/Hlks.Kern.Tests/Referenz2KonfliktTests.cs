using System.Text.Json.Nodes;
using ACadSharp.Entities;
using ACadSharp.Types.Units;
using CSMath;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.FamiliePruefungHilfe;
using static Hlks.Kern.Tests.GrundlagenHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 2, Auftrag B6: unabhängige Referenzfälle von Hand – Konflikte. Drei Quellen für denselben Wert (L von V-15):
/// PDF-Tabellenzelle 67 mm (UnterlagenHilfe.TestPdf), CSV-Zelle 0,067 m = 67 mm, DXF-Bemassung. Toleranz für Längen max(1 mm; 0.5 %).
/// Vorrang für den Vorschlag (Spezifikation): 3D-Modell › DWG/DXF-Bemassung › PDF-Zelle/Tabelle › Text › Bild. Alles fiktiv.</summary>
public class Referenz2KonfliktTests
{
    static string DxfMass(Ordner o, string name, double mm) => Dxf(o, name, UnitsType.Millimeters, doc =>
        doc.Entities.Add(new DimensionLinear { FirstPoint = new XYZ(0, 0, 0), SecondPoint = new XYZ(mm, 0, 0), DefinitionPoint = new XYZ(mm, -10, 0), Rotation = 0, Layer = LayerHolen(doc, "MASSE") }));

    /// <summary>Testventil mit L von V-15 aus drei Quellen; B ist «sonstiges».</summary>
    static JsonObject Ventil3(UnterlagenBefunde pdf, UnterlagenBefunde csv, UnterlagenBefunde dxf, double dxfWert)
    {
        var d = Ventil(pdf);
        ((JsonObject)((JsonArray)d["parameter"]!)[3]!)["gruppe"] = "sonstiges";
        TypWert(d, "V-15", "L", 67, Zelle(pdf, 1, 3), CsvZelle(csv, 1, 2), Mass(dxf, dxfWert));
        return d;
    }

    static JsonArray KonfliktListe(Projekt p) => Definition(p)["konflikte"] as JsonArray ?? [];

    static double TypL(Projekt p, string typ = "V-15") =>
        ((JsonArray)Definition(p)["typen"]!).Single(t => t!["name"]!.GetValue<string>() == typ)!["werte"]!["L"]!.GetValue<double>();

    [Fact]
    public void Drei_Quellen_zwei_gleich_eine_abweichend_ein_Konflikt_mit_allen_drei()
    {
        using var o = new Ordner();
        var p = ProjektB4();
        var pdf = Ablegen(p, UnterlagenHilfe.TestPdf(o));
        var csv = Ablegen(p, Csv(o));                       // L [m]: 0,067 → 67 mm
        var dxf = Ablegen(p, DxfMass(o, "m70.dxf", 70));    // Bemassung 70 mm
        var z = Zelle(pdf, 1, 3); var c = CsvZelle(csv, 1, 2); var m = Mass(dxf, 70);

        // 67 (PDF), 67 (CSV, 0,067 m · 1000), 70 (DXF): Abstand 3 mm > Toleranz max(1; 0.5 % von 70 = 0.35) = 1 mm → zwei Gruppen
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Ventil3(pdf, csv, dxf, 70) }));
        var k = Assert.Single(KonfliktListe(p))!.AsObject();
        Assert.Equal("L", k["parameter"]!.GetValue<string>());
        Assert.Equal("V-15", k["typ"]!.GetValue<string>());
        var werte = k["werte"]!.AsArray();
        Assert.Equal(2, werte.Count);
        // alle drei Quellen stehen im Konflikt
        var alle = werte.SelectMany(w => w!["quellen"]!.AsArray().Select(q => q!.GetValue<string>())).ToList();
        Assert.Equal(3, alle.Count);
        Assert.Equal(new HashSet<string> { z, c, m }, alle.ToHashSet());
        // Gruppe 1 (stärkster Vorrang zuerst): 70 mit der DXF-Bemassung; Gruppe 2: 67 mit PDF-Zelle und CSV-Zelle
        Assert.Equal(70, werte[0]!["wert"]!.GetValue<double>());
        Assert.Equal([m], werte[0]!["quellen"]!.AsArray().Select(q => q!.GetValue<string>()));
        Assert.Equal(67, werte[1]!["wert"]!.GetValue<double>());
        Assert.Equal(new HashSet<string> { z, c }, werte[1]!["quellen"]!.AsArray().Select(q => q!.GetValue<string>()).ToHashSet());
        // Vorschlag nach Vorrang: DXF-Bemassung (4) vor PDF-Zelle (3)
        Assert.Equal(70, k["vorschlag"]!.GetValue<double>());
        Assert.Contains("DWG/DXF-Bemassung vor PDF-Zelle", k["vorrang"]!.GetValue<string>());
        Assert.Contains("±1 mm", k["toleranz"]!.GetValue<string>());
        // der Vorschlag steht im Entwurf, die Abdeckung meldet den Konflikt
        Assert.Equal(70, TypL(p));
        Assert.Equal("konflikt", Abdeckung(p, "V-15/L")["status"]!.GetValue<string>());
        Assert.Null(k["entscheid"]);
    }

    [Theory]
    [InlineData(67.3, false)]   // |67.3 − 67| = 0.3 ≤ 1 mm: kein Konflikt
    [InlineData(68.0, false)]   // genau auf der Toleranz 1 mm (max(1; 0.5 % von 68 = 0.34)): kein Konflikt
    [InlineData(68.1, true)]    // 1.1 mm > 1 mm: Konflikt
    [InlineData(66.0, false)]   // genau 1 mm nach unten
    [InlineData(65.9, true)]    // 1.1 mm nach unten
    public void Gleiche_Werte_innerhalb_der_Toleranz_sind_kein_Konflikt(double dxfWert, bool konflikt)
    {
        using var o = new Ordner();
        var p = ProjektB4();
        var pdf = Ablegen(p, UnterlagenHilfe.TestPdf(o));
        var csv = Ablegen(p, Csv(o));
        var dxf = Ablegen(p, DxfMass(o, "m.dxf", dxfWert));
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Ventil3(pdf, csv, dxf, dxfWert) }));
        if (konflikt)
        {
            Assert.Single(KonfliktListe(p));
            Assert.Equal("konflikt", Abdeckung(p, "V-15/L")["status"]!.GetValue<string>());
        }
        else
        {
            Assert.Empty(KonfliktListe(p));
            var a = Abdeckung(p, "V-15/L");
            Assert.Equal("geprüft", a["status"]!.GetValue<string>());
            Assert.Equal(3, a["quellen"]!.AsArray().Count);
            Assert.Equal(67, TypL(p));   // der Familienwert bleibt, wie er ist
        }
    }

    [Theory]
    [InlineData(1004, false)]   // 4 mm ≤ 0.5 % von 1004 = 5.02 mm
    [InlineData(1005, false)]   // 5 mm ≤ 5.025 mm
    [InlineData(1006, true)]    // 6 mm > 5.03 mm
    public void Toleranz_waechst_mit_dem_Wert_ein_halbes_Prozent(double dxfWert, bool konflikt)
    {
        using var o = new Ordner();
        var p = ProjektB4();
        var csv = Ablegen(p, UnterlagenHilfe.Datei(o, "gross.csv", "Typ;L [mm]\r\nG1;1000\r\n"));
        var dxf = Ablegen(p, DxfMass(o, "gross.dxf", dxfWert));
        var def = new JsonObject
        {
            ["format"] = 1, ["name"] = "Gross (fiktiv)", ["kategorie"] = "Heizkoerper",
            ["parameter"] = new JsonArray(new JsonObject { ["name"] = "L", ["art"] = "typ", ["datentyp"] = "laenge", ["gruppe"] = "abmessung" }),
            ["typen"] = new JsonArray(new JsonObject
            {
                ["name"] = "G1", ["werte"] = new JsonObject { ["L"] = 1000 },
                ["quellen"] = new JsonObject { ["L"] = new JsonArray((JsonNode)JsonValue.Create(CsvZelle(csv, 1, 2)), (JsonNode)JsonValue.Create(Mass(dxf, dxfWert))) },
            }),
            ["koerper"] = new JsonArray(), ["anschluesse"] = new JsonArray(),
        };
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = def }));
        Assert.Equal(konflikt ? 1 : 0, KonfliktListe(p).Count);
        if (konflikt) Assert.Equal(1006, KonfliktListe(p)[0]!["vorschlag"]!.GetValue<double>());   // DXF-Bemassung geht vor der Tabelle
    }

    [Fact]
    public void Vorrang_der_Quellenarten_von_oben_nach_unten()
    {
        // Spezifikation: Geometrie STEP/3D › DWG/DXF-Bemassung › PDF-Vektor/Zelle › Text › Bild
        using var o = new Ordner();
        var p = ProjektB4();
        var pdf = Ablegen(p, UnterlagenHilfe.TestPdf(o));
        var dxf = Ablegen(p, DxfMass(o, "v.dxf", 50));
        var bild = Ablegen(p, Bild(o));
        var ifc = Ablegen(p, UnterlagenHilfe.TestIfc(o));
        int V(UnterlagenBefund b, string art) => QuellVergleiche.Vorrang(b, art, geometrie: true);
        var bildV = V(bild.Befunde.First(b => b.Art == "bild"), "bild");
        var textV = V(pdf.Befunde.First(b => b.Art is "wort" or "zeile" or "text"), "pdf");
        var zelleV = V(pdf.Befunde.First(b => b.Art == "zelle"), "pdf");
        var dxfV = V(dxf.Befunde.First(b => b.Art == "mass"), "dxf");
        var modellV = V(ifc.Befunde.First(b => b.Art is "port" or "form3d"), "ifc");
        Assert.True(bildV < textV, $"Bild {bildV} < Text {textV}");
        Assert.True(textV < zelleV, $"Text {textV} < PDF-Zelle {zelleV}");
        Assert.True(zelleV < dxfV, $"PDF-Zelle {zelleV} < DXF-Bemassung {dxfV}");
        Assert.True(dxfV < modellV, $"DXF-Bemassung {dxfV} < 3D-Modell {modellV}");
    }
}
