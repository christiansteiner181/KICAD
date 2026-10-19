using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.Tables;
using ACadSharp.Blocks;
using ACadSharp.Types.Units;
using CSMath;
using static Hlks.Kern.Tests.GrundlagenHilfe;

namespace Hlks.Kern.Tests;

/// <summary>DWG/DXF lesen und in Primitive umsetzen (Modul 1b, B3): Entitäten, Blöcke, Layer, Einheiten, Text.</summary>
public class GrundlagenCadTests
{
    static CadZeichnung Lesen(Action<CadDocument> fuellen, UnitsType einheit = UnitsType.Millimeters)
    {
        var doc = Dokument(einheit);
        fuellen(doc);
        return CadLeser.Lesen(doc);
    }

    static List<CadPrimitiv> Von(CadZeichnung z, char art) => z.Primitive.Where(p => p.Art == art).ToList();

    [Fact]
    public void Linie_Kreis_Bogen_Ellipse_Spline_Polylinie()
    {
        var z = Lesen(doc =>
        {
            doc.Entities.Add(Linie(doc, 0, 0, 100, 0));
            doc.Entities.Add(new Circle { Center = new XYZ(50, 50, 0), Radius = 20 });
            doc.Entities.Add(new Arc { Center = new XYZ(0, 0, 0), Radius = 10, StartAngle = 0, EndAngle = Math.PI / 2 });
            doc.Entities.Add(new Ellipse { Center = new XYZ(0, 0, 0), MajorAxisEndPoint = new XYZ(10, 0, 0), RadiusRatio = 0.5, StartParameter = 0, EndParameter = 2 * Math.PI });
            var lw = new LwPolyline();
            lw.Vertices.Add(new LwPolyline.Vertex(new XY(0, 0)));
            lw.Vertices.Add(new LwPolyline.Vertex(new XY(10, 0)));
            lw.Vertices.Add(new LwPolyline.Vertex(new XY(10, 10)));
            doc.Entities.Add(lw);
            var sp = new Spline { Degree = 3 };   // mit Kontrollpunkten und Knoten (so liegt sie in DWG-Dateien)
            sp.ControlPoints.AddRange([new XYZ(0, 0, 0), new XYZ(10, 20, 0), new XYZ(20, -20, 0), new XYZ(30, 0, 0)]);
            sp.Knots.AddRange([0, 0, 0, 0, 1, 1, 1, 1]);
            doc.Entities.Add(sp);
            var fit = new Spline { Degree = 3 };   // nur Anpassungspunkte: Kurve durch diese Punkte
            fit.FitPoints.AddRange([new XYZ(0, 100, 0), new XYZ(10, 105, 0), new XYZ(20, 100, 0), new XYZ(30, 105, 0)]);
            doc.Entities.Add(fit);
        });
        var l = Assert.Single(Von(z, 'l'));
        Assert.Equal([0, 0, 100, 0], l.W);
        var k = Assert.Single(Von(z, 'k'));
        Assert.Equal([50, 50, 20], k.W);
        var b = Assert.Single(Von(z, 'b'));
        Assert.Equal(10, b.W[2], 6);
        Assert.Equal(0, b.W[3], 6);
        Assert.Equal(90, b.W[4], 6);   // Bogenmass → Grad
        var p = Von(z, 'p').ToList();
        Assert.Contains(p, x => x.W.Length == 6);                       // Polylinie offen: 3 Punkte
        var ellipse = Von(z, 'g').Single(x => x.W.Length >= 100);        // Ellipse als geschlossene Polylinie
        for (int i = 0; i < ellipse.W.Length; i += 2)
            Assert.Equal(1.0, Math.Pow(ellipse.W[i] / 10, 2) + Math.Pow(ellipse.W[i + 1] / 5, 2), 4);   // liegt auf der Ellipse 10 × 5
        Assert.Equal(2, p.Count(x => x.W.Length >= 40));   // beide Splines als Polylinien
        Assert.Contains(p, x => x.W.Length == 2 * 25 && x.W[1] == 100);   // durch die Anpassungspunkte (3 Abschnitte à 8 + Endpunkt)
        Assert.Equal(0, z.NichtDargestellt.Count);
    }

    [Fact]
    public void Polylinie_MitAusbuchtung_ergibtBogen()
    {
        // Halbkreis von (0,0) nach (10,0), Bulge 1 (Gegenuhrzeigersinn): Mitte (5,0), Radius 5, unterhalb der Sehne
        var z = Lesen(doc =>
        {
            var lw = new LwPolyline();
            lw.Vertices.Add(new LwPolyline.Vertex(new XY(0, 0)) { Bulge = 1 });
            lw.Vertices.Add(new LwPolyline.Vertex(new XY(10, 0)));
            lw.Vertices.Add(new LwPolyline.Vertex(new XY(10, 10)));
            doc.Entities.Add(lw);
        });
        var b = Assert.Single(Von(z, 'b'));
        Assert.Equal(5, b.W[0], 6); Assert.Equal(0, b.W[1], 6); Assert.Equal(5, b.W[2], 6);
        Assert.Equal(180, b.W[3], 6); Assert.Equal(0, b.W[4], 6);   // Gegenuhrzeigersinn von 180° über 270° bis 360°: unten herum
        Assert.Single(Von(z, 'l'));   // Rest (10,0) → (10,10) als Linie
    }

    [Fact]
    public void Polylinie_NegativeAusbuchtung_LaeuftImUhrzeigersinn()
    {
        // von (0,0) nach (10,0) mit Bulge −1: Halbkreis oberhalb der Sehne (Uhrzeigersinn), Mitte (5,0)
        var z = Lesen(doc =>
        {
            var lw = new LwPolyline();
            lw.Vertices.Add(new LwPolyline.Vertex(new XY(0, 0)) { Bulge = -1 });
            lw.Vertices.Add(new LwPolyline.Vertex(new XY(10, 0)));
            doc.Entities.Add(lw);
        });
        var b = Assert.Single(Von(z, 'b'));
        // Gegenuhrzeigersinn von 0° bis 180° ist der obere Halbkreis
        Assert.Equal(0, b.W[3], 6);
        Assert.Equal(180, b.W[4], 6);
        Assert.Equal(0, b.W[1], 6);   // Mitte y = 0
    }

    [Fact]
    public void Text_MText_Hoehe_Ausrichtung_Zeilen()
    {
        var z = Lesen(doc =>
        {
            doc.Entities.Add(new TextEntity { Value = "Raum %%c25 %%d", InsertPoint = new XYZ(100, 200, 0), Height = 50, Rotation = Math.PI / 2 });
            doc.Entities.Add(new MText { Value = "\\A1;{\\fArial|b1|i0;\\W1.0;Zeile 1}\\PZeile 2", InsertPoint = new XYZ(0, 1000, 0), Height = 100, AttachmentPoint = AttachmentPointType.TopLeft });
        });
        var t = Von(z, 't');
        Assert.Equal(3, t.Count);
        var einfach = t.Single(x => x.Text!.StartsWith("Raum"));
        Assert.Equal("Raum Ø25 °", einfach.Text);
        Assert.Equal([100, 200, 50], einfach.W[..3]); Assert.Equal(90, einfach.W[3], 6); Assert.Equal(0, einfach.W[4]);
        // MText oben links: erste Grundlinie eine Texthöhe unter dem Einfügepunkt, Zeilenabstand 5/3 · h
        var m1 = t.Single(x => x.Text == "Zeile 1"); var m2 = t.Single(x => x.Text == "Zeile 2");
        Assert.Equal(900, m1.W[1], 6);
        Assert.Equal(900 - 100 * 5.0 / 3, m2.W[1], 6);
        Assert.Equal(0, m1.W[4]);
    }

    [Theory]
    [InlineData("\\A1;{\\pqc;\\fArial Narrow|b1|i0|c0|p0;\\W1.000000;E23}", "E23")]
    [InlineData("\\A1;{\\pql;\\pt0,0.05;\\fArial|b0;\\W1.0;8.98\\fArial|b0;\\A1{\\H.66x;\\S2^ ;}\\fArial|b0; m\\A1{\\H.66x;\\S2^ ;}}", "8.98² m²")]
    [InlineData("a\\Pb\\\\c\\{d\\}", "a|b\\c{d}")]
    [InlineData("\\S1/2;", "1/2")]
    [InlineData("\\U+00B0C und %%p5", "°C und ±5")]
    [InlineData("\\L unterstrichen\\l", " unterstrichen")]
    public void MText_Formatcodes_werdenEntfernt(string roh, string erwartet) =>
        Assert.Equal(erwartet, string.Join("|", CadText.MText(roh)));

    [Fact]
    public void Insert_Block_wirdMitTransformationAufgeloest()
    {
        var z = Lesen(doc =>
        {
            var block = new BlockRecord("B1");
            block.Entities.Add(Linie(doc, 10, 0, 20, 0));     // Blockbasis (10, 0)
            block.Entities.Add(new Circle { Center = new XYZ(10, 5, 0), Radius = 2 });
            doc.BlockRecords.Add(block);
            block.BlockEntity.BasePoint = new XYZ(10, 0, 0);
            doc.Entities.Add(new Insert(block) { InsertPoint = new XYZ(100, 100, 0), Rotation = Math.PI / 2, XScale = 2, YScale = 2 });
        });
        var l = Assert.Single(Von(z, 'l'));
        // (10,0) → Basis → (0,0) → ×2 → (0,0) → 90° → (0,0) → +100 → (100,100); (20,0) → (10,0)→(20,0)→(0,20)→(100,120)
        Assert.Equal(100, l.W[0], 6); Assert.Equal(100, l.W[1], 6);
        Assert.Equal(100, l.W[2], 6); Assert.Equal(120, l.W[3], 6);
        var k = Assert.Single(Von(z, 'k'));
        Assert.Equal(4, k.W[2], 6);                                  // Radius ×2
        Assert.Equal(100 - 10, k.W[0], 6); Assert.Equal(100, k.W[1], 6);   // (0,5) → ×2 (0,10) → 90°: (−10, 0) → (90, 100)
    }

    [Fact]
    public void Insert_Verschachtelt_Layer0ErbtVomInsert_LayerAusWirdUebersprungen()
    {
        var z = Lesen(doc =>
        {
            var innen = new BlockRecord("INNEN");
            innen.Entities.Add(Linie(doc, 0, 0, 1, 0, "0"));
            doc.BlockRecords.Add(innen);
            var aussen = new BlockRecord("AUSSEN");
            aussen.Entities.Add(new Insert(innen) { InsertPoint = new XYZ(5, 0, 0), Layer = LayerHolen(doc, "0") });
            doc.BlockRecords.Add(aussen);
            doc.Entities.Add(new Insert(aussen) { InsertPoint = new XYZ(0, 0, 0), Layer = LayerHolen(doc, "SICHTBAR") });
            doc.Entities.Add(new Insert(aussen) { InsertPoint = new XYZ(0, 50, 0), Layer = LayerHolen(doc, "AUS") });
            LayerHolen(doc, "AUS").IsOn = false;
        });
        var l = Assert.Single(Von(z, 'l'));
        Assert.Equal([5, 0, 6, 0], l.W);
        Assert.Equal(["SICHTBAR"], z.Layer);
        Assert.Equal(["AUS"], z.LayerAus);
    }

    [Fact]
    public void Bogen_NormaleMinusZ_SpiegeltX()
    {
        var z = Lesen(doc =>
        {
            doc.Entities.Add(new Arc { Center = new XYZ(10, 0, 0), Radius = 5, StartAngle = 0, EndAngle = Math.PI / 2, Normal = new XYZ(0, 0, -1) });
        });
        var b = Assert.Single(Von(z, 'b'));
        Assert.Equal(-10, b.W[0], 6);                 // x gespiegelt
        Assert.Equal(90, b.W[3], 6); Assert.Equal(180, b.W[4], 6);   // 0°–90° wird 90°–180°
    }

    [Fact]
    public void Hatch_wirdAlsUmrissGezeichnet()
    {
        var z = Lesen(doc =>
        {
            var h = new Hatch();
            var pfad = new Hatch.BoundaryPath();
            var poly = new Hatch.BoundaryPath.Polyline { IsClosed = true };
            poly.Vertices.Add(new XYZ(0, 0, 0)); poly.Vertices.Add(new XYZ(10, 0, 0)); poly.Vertices.Add(new XYZ(10, 10, 0)); poly.Vertices.Add(new XYZ(0, 10, 0));
            pfad.Edges.Add(poly);
            h.Paths.Add(pfad);
            doc.Entities.Add(h);
        });
        var g = Assert.Single(Von(z, 'g'));
        Assert.Equal(4 * 2, g.W.Length);
        Assert.Equal([0, 0, 10, 10], z.Ausdehnung);
    }

    [Fact]
    public void NichtDarstellbares_wirdGezaehlt()
    {
        var z = Lesen(doc =>
        {
            doc.Entities.Add(new Point { Location = new XYZ(1, 1, 0) });
            doc.Entities.Add(Linie(doc, 0, 0, 1, 1));
        });
        Assert.Equal(1, z.NichtDargestellt["Point"]);
        Assert.Single(z.Primitive);
    }

    [Theory]
    [InlineData(UnitsType.Millimeters, 1, true)]
    [InlineData(UnitsType.Centimeters, 10, true)]
    [InlineData(UnitsType.Meters, 1000, true)]
    [InlineData(UnitsType.Inches, 25.4, true)]
    [InlineData(UnitsType.Feet, 304.8, true)]
    [InlineData(UnitsType.Unitless, 1, false)]
    public void Einheit_nachInsunits(UnitsType einheit, double mm, bool bekannt)
    {
        var z = Lesen(doc => doc.Entities.Add(Linie(doc, 0, 0, 1, 0)), einheit);
        Assert.Equal(mm, z.MmJeEinheit, 9);
        Assert.Equal(bekannt, z.EinheitBekannt);
    }

    [Fact]
    public void Umriss_ignoriertWeitAbgelegeneEinzelteile_Ausdehnung_nicht()
    {
        var z = Lesen(doc =>
        {
            for (int i = 0; i < 100; i++) doc.Entities.Add(Linie(doc, i, 0, i + 1, 5));
            doc.Entities.Add(Linie(doc, 100_000, 100_000, 100_001, 100_001));   // Legende weit weg
        });
        Assert.Equal(100_001, z.Ausdehnung![2], 6);
        Assert.True(z.Umriss![2] < 1000, $"Umriss reicht bis {z.Umriss[2]}");
        Assert.Equal(0, z.Umriss[0], 6);
    }

    [Fact]
    public void Zeichnung_Ableitung_JsonRundlauf()
    {
        using var o = new Ordner();
        var pfad = StandardDxf(o, einheit: UnitsType.Meters);
        var z = CadLeser.Lesen(pfad);
        var ziel = o.Datei("abl.dwg.json");
        z.Schreiben(ziel);
        var z2 = CadZeichnung.Lesen(ziel);
        Assert.Equal(z.Primitive.Count, z2.Primitive.Count);
        Assert.Equal(z.Layer, z2.Layer);
        Assert.Equal(z.Umriss, z2.Umriss);
        Assert.Equal(1000, z2.MmJeEinheit);
        for (int i = 0; i < z.Primitive.Count; i++)
        {
            Assert.Equal(z.Primitive[i].Art, z2.Primitive[i].Art);
            Assert.Equal(z.Primitive[i].W, z2.Primitive[i].W);
        }
    }

    [Fact]
    public void Dxf_Datei_wirdGelesen_KaputteDateiGibtVerstaendlichenFehler()
    {
        using var o = new Ordner();
        var z = CadLeser.Lesen(StandardDxf(o));
        Assert.Equal(4, Von(z, 'l').Count);
        Assert.Single(Von(z, 'k'));
        Assert.Equal(["MOEBEL", "WAND"], z.Layer);

        var kaputt = o.Datei("kaputt.dwg");
        File.WriteAllText(kaputt, "das ist keine DWG-Datei");
        var f = Assert.Throws<HlksFehler>(() => CadLeser.Lesen(kaputt));
        Assert.Contains("lässt sich nicht lesen", f.Message);
        Assert.False(string.IsNullOrWhiteSpace(f.Vorschlag));
    }
}
