using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using ACadSharp.Types.Units;
using CSMath;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Xbim.Common;
using Xbim.Common.Step21;
using Xbim.Ifc;
using Xbim.IO;
using static Hlks.Kern.Tests.GrundlagenHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Hilfen der Tests zu den Herstellerunterlagen (Modul 2, B2): Testdateien mit bekannten Werten selbst erzeugen
/// (PDF mit Text, Tabelle, Linien und Masslinie; DXF mit einer Bemassung von genau 67 mm; CSV, Typkatalog, XLSX, STL, OBJ,
/// Hersteller-IFC), Projekt mit Ordner unterlagen, Befunde lesen.</summary>
static class UnterlagenHilfe
{
    public static readonly Katalog Kat = Katalog.Laden(Katalog.StandardOrdner());

    /// <summary>Projekt ohne Ablage (Unterlagen im Temp-Ordner des Projekts).</summary>
    public static Projekt Leer() => Projekt.Anlegen(Kat, null, "Test Unterlagen", "T", "pc");

    /// <summary>Projekt mit Geschoss EG und Grundriss (für Pläne mit Unterlagen).</summary>
    public static Projekt MitGeschoss() => GrundlagenHilfe.Projekt();

    public static UnterlagenBefunde Lesen(Projekt p, string pfad, string? art = null)
    {
        var u = UnterlagenService.Aufloesen(p, pfad);
        if (art is not null) u = u with { Art = art };
        return UnterlagenService.Lesen(p, u).Befunde;
    }

    public static IEnumerable<UnterlagenBefund> Art(UnterlagenBefunde b, string art) => b.Befunde.Where(x => x.Art == art);

    public static double Zahl(JsonNode? n) => Json.Zahl(n) ?? throw new Xunit.Sdk.XunitException("keine Zahl: " + n?.ToJsonString());

    /// <summary>Zelle einer Tabelle (Zeile und Spalte ab 1).</summary>
    public static UnterlagenBefund Zelle(UnterlagenBefunde b, string tabelleId, int zeile, int spalte) =>
        b.Befunde.Single(x => x.Art == "zelle" && x.Zusatz?["tabelle"]?.GetValue<string>() == tabelleId
            && x.Zusatz["zeile"]!.GetValue<int>() == zeile && x.Zusatz["spalte"]!.GetValue<int>() == spalte);

    // ---------- PDF ----------

    /// <summary>PDF mit zwei Seiten (A4, pt, y von oben gezeichnet):
    /// Seite 1: Titel «Datenblatt Testventil»; Tabelle (Kopf Typ | DN | L | H, Einheiten [mm]; Zeilen V-15 | 15 | 67 | 35, V-20 | 20 | 78 | 46,
    /// V-25 | 25 | 87 | 46); eine Masslinie mit Pfeilspitzen und Buchstabe «L» (Linie bei y = 500 von oben, x 100–300); ein Rechteck,
    /// ein Kreis (Mitte 400/650, r 20) und ein Viertelkreis (Mitte 400/700, r 30).
    /// Seite 2: Tabelle B | H | V mit sechs Zeilen; H-Werte 100 und 150 gelten für je drei Zeilen (Trennlinien nur an den Gruppengrenzen).</summary>
    public static string TestPdf(Ordner o, string name = "datenblatt.pdf")
    {
        var doc = new PdfDocument();
        var s1 = doc.AddPage(); s1.Width = XUnit.FromPoint(595); s1.Height = XUnit.FromPoint(842);
        using (var g = XGraphics.FromPdfPage(s1))
        {
            _ = new PdfZeichner(g, 297);   // richtet die Schriften ein
            var gross = new XFont("Arial", 14); var f = new XFont("Arial", 9);
            g.DrawString("Datenblatt Testventil", gross, XBrushes.Black, 50, 60);
            void T(string t, double x, double y) => g.DrawString(t, f, XBrushes.Black, x, y);
            string[] kopf = ["Typ", "DN", "L", "H"]; double[] xs = [50, 150, 220, 290];
            for (int i = 0; i < 4; i++) T(kopf[i], xs[i], 200);
            for (int i = 1; i < 4; i++) T("[mm]", xs[i], 212);
            string[][] z = [["V-15", "15", "67", "35"], ["V-20", "20", "78", "46"], ["V-25", "25", "87", "46"]];
            for (int r = 0; r < 3; r++)
                for (int i = 0; i < 4; i++) T(z[r][i], xs[i], 230 + 14 * r);
            var pen = new XPen(XColors.Black, 0.5);
            // Trennlinien unter den Zeilen
            foreach (var y in new[] { 197.0, 227.0, 241.0, 255.0, 269.0 }) g.DrawLine(pen, 45, y, 340, y);
            // Masslinie mit Pfeilspitzen und Hilfslinien
            g.DrawLine(pen, 100, 500, 300, 500);
            g.DrawLine(pen, 100, 480, 100, 505); g.DrawLine(pen, 300, 480, 300, 505);
            g.DrawPolygon(XBrushes.Black, [new XPoint(100, 500), new XPoint(106, 497.5), new XPoint(106, 502.5)], XFillMode.Winding);
            g.DrawPolygon(XBrushes.Black, [new XPoint(300, 500), new XPoint(294, 497.5), new XPoint(294, 502.5)], XFillMode.Winding);
            T("L", 196, 489);
            g.DrawRectangle(pen, 100, 520, 200, 80);
            g.DrawEllipse(pen, 380, 630, 40, 40);                   // Mitte 400/650, r 20
            g.DrawArc(pen, 370, 670, 60, 60, 0, 90);                 // Mitte 400/700, r 30
        }
        var s2 = doc.AddPage(); s2.Width = XUnit.FromPoint(595); s2.Height = XUnit.FromPoint(842);
        using (var g = XGraphics.FromPdfPage(s2))
        {
            _ = new PdfZeichner(g, 297);
            var f = new XFont("Arial", 9);
            void T(string t, double x, double y) => g.DrawString(t, f, XBrushes.Black, x, y);
            T("B", 50, 100); T("H", 150, 100); T("V", 250, 100);
            string[] b = ["200", "300", "400", "200", "300", "400"], v = ["130", "190", "255", "193", "285", "382"];
            for (int r = 0; r < 6; r++) { T(b[r], 50, 120 + 14 * r); T(v[r], 250, 120 + 14 * r); }
            T("100", 150, 120 + 14 * 1); T("150", 150, 120 + 14 * 4);   // in der mittleren Zeile ihrer Gruppe
            var pen = new XPen(XColors.Black, 0.5);
            // Linie unter jeder Zeile in den Spalten B und V; in H nur an den Gruppengrenzen
            for (int r = -1; r < 6; r++)
            {
                double y = 117 + 14 * (r + 1);
                if (r == -1) y = 117;
                g.DrawLine(pen, 45, y, 85, y); g.DrawLine(pen, 245, y, 285, y);
                if (r is -1 or 2 or 5) g.DrawLine(pen, 145, y, 185, y);
            }
        }
        var pfad = o.Datei(name);
        doc.Save(pfad);
        return pfad;
    }

    // ---------- DXF ----------

    /// <summary>DXF in Millimetern: Layer GEHAEUSE (Linie 0/0–120/0, Kreis Mitte 60/40 r 15, Bogen, Polylinie, Text «Ventil DN 15»), Block
    /// VENTIL mit Attribut ARTNR, eine lineare Bemassung von genau 67 mm (Punkte 0/0 und 67/0) und eine mit überschriebenem Text.</summary>
    public static string TestDxf(Ordner o, string name = "masse.dxf")
    {
        var doc = Dokument(UnitsType.Millimeters);
        doc.Entities.Add(Linie(doc, 0, 0, 120, 0, "GEHAEUSE"));
        doc.Entities.Add(new Circle { Center = new XYZ(60, 40, 0), Radius = 15, Layer = LayerHolen(doc, "GEHAEUSE") });
        doc.Entities.Add(new Arc { Center = new XYZ(0, 0, 0), Radius = 30, StartAngle = 0, EndAngle = Math.PI / 2, Layer = LayerHolen(doc, "GEHAEUSE") });
        var pl = new LwPolyline { Layer = LayerHolen(doc, "KONTUR") };
        pl.Vertices.Add(new LwPolyline.Vertex(new XY(0, 10))); pl.Vertices.Add(new LwPolyline.Vertex(new XY(50, 10))); pl.Vertices.Add(new LwPolyline.Vertex(new XY(50, 30)));
        doc.Entities.Add(pl);
        doc.Entities.Add(new TextEntity { Value = "Ventil DN 15", InsertPoint = new XYZ(5, 60, 0), Height = 5, Layer = LayerHolen(doc, "TEXT") });
        // Block
        var block = new BlockRecord("VENTIL");
        doc.BlockRecords.Add(block);
        block.Entities.Add(Linie(doc, 0, 0, 10, 0));
        var ins = new Insert(block) { InsertPoint = new XYZ(100, 50, 0), Layer = LayerHolen(doc, "BLOECKE") };
        doc.Entities.Add(ins);
        // Bemassungen
        doc.Entities.Add(new DimensionLinear
        {
            FirstPoint = new XYZ(0, 0, 0), SecondPoint = new XYZ(67, 0, 0), DefinitionPoint = new XYZ(67, -12, 0), Rotation = 0, Layer = LayerHolen(doc, "MASSE"),
        });
        doc.Entities.Add(new DimensionAligned
        {
            FirstPoint = new XYZ(0, 20, 0), SecondPoint = new XYZ(0, 55, 0), DefinitionPoint = new XYZ(-10, 55, 0), Text = "H = 35", Layer = LayerHolen(doc, "MASSE"),
        });
        var pfad = o.Datei(name);
        using (var w = new DxfWriter(pfad, doc, false)) w.Write();
        return pfad;
    }

    // ---------- Tabellen ----------

    public static string Datei(Ordner o, string name, string inhalt, Encoding? e = null)
    {
        var pfad = o.Datei(name);
        File.WriteAllText(pfad, inhalt, e ?? new UTF8Encoding(false));
        return pfad;
    }

    public const string CsvText = "Typ;DN;Rp [Zoll];L [mm];Gewicht [kg]\r\nR2015-S1;15;1/2;67;0,30\r\nR2020-S2;20;3/4;78;0,43\r\n\"R2025;S2\";25;1;87;0,51\r\n";

    public const string TypkatalogText = ",Laenge##LENGTH##MILLIMETERS,Breite##LENGTH##MILLIMETERS,DN##NUMBER##,Material##OTHER##\r\n" +
        "V-15,67,35,15,Messing\r\nV-20,78,46,20,Messing\r\nV-25,87,46,25,\"Rotguss, vernickelt\"\r\n";

    /// <summary>XLSX von Hand als OpenXML-Zip: Blatt «Typen» mit Kopf Typ | DN | L [mm] und drei Zeilen, Zahlen als Zahlen, B5:C5 verbunden;
    /// zweites Blatt «Leer».</summary>
    public static string TestXlsx(Ordner o, string name = "typen.xlsx")
    {
        var pfad = o.Datei(name);
        var strings = new List<string>();
        int S(string t) { var i = strings.IndexOf(t); if (i < 0) { strings.Add(t); i = strings.Count - 1; } return i; }
        string Z(string r, string t) => $"<c r=\"{r}\" t=\"s\"><v>{S(t)}</v></c>";
        string N(string r, double v) => $"<c r=\"{r}\"><v>{v.ToString(System.Globalization.CultureInfo.InvariantCulture)}</v></c>";
        var sheet = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>" +
            $"<row r=\"1\">{Z("A1", "Typ")}{Z("B1", "DN")}{Z("C1", "L [mm]")}</row>" +
            $"<row r=\"2\">{Z("A2", "R2015-S1")}{N("B2", 15)}{N("C2", 67)}</row>" +
            $"<row r=\"3\">{Z("A3", "R2020-S2")}{N("B3", 20)}{N("C3", 78.5)}</row>" +
            $"<row r=\"4\">{Z("A4", "R2025-S2")}{N("B4", 25)}{N("C4", 87)}</row>" +
            $"<row r=\"5\">{Z("A5", "alle gleich")}{N("B5", 99)}</row>" +
            "</sheetData><mergeCells count=\"1\"><mergeCell ref=\"B5:C5\"/></mergeCells></worksheet>";
        var leer = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData/></worksheet>";
        var sst = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" count=\"" + strings.Count + "\" uniqueCount=\"" + strings.Count + "\">" +
            string.Concat(strings.Select(t => $"<si><t>{System.Security.SecurityElement.Escape(t)}</t></si>")) + "</sst>";
        using var fs = File.Create(pfad);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);
        void E(string n, string inhalt) { var e = zip.CreateEntry(n); using var w = new StreamWriter(e.Open(), new UTF8Encoding(false)); w.Write(inhalt); }
        E("[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
            "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
            "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
            "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
            "<Override PartName=\"/xl/worksheets/sheet2.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
            "<Override PartName=\"/xl/sharedStrings.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml\"/></Types>");
        E("_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
        E("xl/workbook.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
            "<sheets><sheet name=\"Typen\" sheetId=\"1\" r:id=\"rId1\"/><sheet name=\"Leer\" sheetId=\"2\" r:id=\"rId2\"/></sheets></workbook>");
        E("xl/_rels/workbook.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
            "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet2.xml\"/>" +
            "<Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings\" Target=\"sharedStrings.xml\"/></Relationships>");
        E("xl/worksheets/sheet1.xml", sheet);
        E("xl/worksheets/sheet2.xml", leer);
        E("xl/sharedStrings.xml", sst);
        return pfad;
    }

    // ---------- Netze ----------

    /// <summary>Quader 10 × 20 × 30 als 12 Dreiecke (Ecken 0/0/0 bis 10/20/30).</summary>
    static readonly double[][] QuaderEcken = [[0, 0, 0], [10, 0, 0], [10, 20, 0], [0, 20, 0], [0, 0, 30], [10, 0, 30], [10, 20, 30], [0, 20, 30]];
    static readonly int[][] QuaderDreiecke = [[0, 2, 1], [0, 3, 2], [4, 5, 6], [4, 6, 7], [0, 1, 5], [0, 5, 4], [1, 2, 6], [1, 6, 5], [2, 3, 7], [2, 7, 6], [3, 0, 4], [3, 4, 7]];

    public static string StlBinaer(Ordner o, string name = "quader.stl")
    {
        var pfad = o.Datei(name);
        using var w = new BinaryWriter(File.Create(pfad));
        w.Write(new byte[80]);
        w.Write((uint)QuaderDreiecke.Length);
        foreach (var d in QuaderDreiecke)
        {
            w.Write(0f); w.Write(0f); w.Write(0f);
            foreach (var i in d) { var e = QuaderEcken[i]; w.Write((float)e[0]); w.Write((float)e[1]); w.Write((float)e[2]); }
            w.Write((ushort)0);
        }
        return pfad;
    }

    public static string StlAscii(Ordner o, string name = "quader_ascii.stl")
    {
        var sb = new StringBuilder("solid quader\n");
        foreach (var d in QuaderDreiecke)
        {
            sb.Append("facet normal 0 0 0\n outer loop\n");
            foreach (var i in d) sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"  vertex {QuaderEcken[i][0]} {QuaderEcken[i][1]} {QuaderEcken[i][2]}\n");
            sb.Append(" endloop\nendfacet\n");
        }
        sb.Append("endsolid quader\n");
        return Datei(o, name, sb.ToString());
    }

    /// <summary>OBJ: Quader aus sechs Vierecken in zwei Gruppen («unten», «oben»), Ecken mit /vt/vn.</summary>
    public static string Obj(Ordner o, string name = "quader.obj")
    {
        var sb = new StringBuilder("# Quader\n");
        foreach (var e in QuaderEcken) sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"v {e[0]} {e[1]} {e[2]}\n");
        sb.Append("g unten\nf 1//1 4//1 3//1 2//1\nf 1/1/2 2/2/2 6/3/2 5/4/2\ng oben\nf 5 6 7 8\nf 2 3 7 6\nf 3 4 8 7\nf 4 1 5 8\n");
        return Datei(o, name, sb.ToString());
    }

    // ---------- Hersteller-IFC ----------

    /// <summary>Hersteller-IFC (IFC4, mm) mit einem Ventil-Typ (IfcValveType «V-15 Kugelhahn», Pset_ValveTypeCommon mit NominalDiameter 15 mm),
    /// einem Exemplar auf dem Ursprung (Quader 100 × 60 × 40, x/y Mitte, z von 0) und zwei Ports: «A» bei (−50, 0, 20) Achse −x,
    /// FlowDirection SINK, und «B» bei (50, 0, 20) Achse +x, SOURCE; Port A mit Pset_DistributionPortTypeCommon.NominalDiameter 15.</summary>
    public static string TestIfc(Ordner o, string name = "ventil.ifc")
    {
        var pfad = o.Datei(name);
        var cred = new XbimEditorCredentials
        {
            ApplicationDevelopersName = "H+K Planungs AG", ApplicationFullName = "HLKS-CAD Testdatei B2", ApplicationIdentifier = "hlks-test-b2",
            ApplicationVersion = "1.0", EditorsFamilyName = "Test", EditorsGivenName = "HLKS", EditorsOrganisationName = "Hersteller AG",
        };
        using var m = IfcStore.Create(cred, XbimSchemaVersion.Ifc4, XbimStoreType.InMemoryModel);
        using var t = m.BeginTransaction("Hersteller-IFC");
        var I = m.Instances;
        var projekt = I.New<Xbim.Ifc4.Kernel.IfcProject>(p => p.Name = "Hersteller");
        projekt.Initialize(ProjectUnits.SIUnitsUK);   // Länge in mm
        var ctx = I.OfType<Xbim.Ifc4.RepresentationResource.IfcGeometricRepresentationContext>().First();
        Xbim.Ifc4.GeometryResource.IfcCartesianPoint P(double x, double y, double z) => I.New<Xbim.Ifc4.GeometryResource.IfcCartesianPoint>(p => p.SetXYZ(x, y, z));
        Xbim.Ifc4.GeometryResource.IfcDirection D(double x, double y, double z) => I.New<Xbim.Ifc4.GeometryResource.IfcDirection>(p => p.SetXYZ(x, y, z));
        Xbim.Ifc4.GeometricConstraintResource.IfcLocalPlacement L(Xbim.Ifc4.GeometricConstraintResource.IfcLocalPlacement? rel, double x, double y, double z, double[]? achse = null) =>
            I.New<Xbim.Ifc4.GeometricConstraintResource.IfcLocalPlacement>(l =>
            {
                l.PlacementRelTo = rel;
                l.RelativePlacement = I.New<Xbim.Ifc4.GeometryResource.IfcAxis2Placement3D>(a =>
                {
                    a.Location = P(x, y, z);
                    if (achse is not null) { a.Axis = D(achse[0], achse[1], achse[2]); a.RefDirection = D(0, 1, 0); }
                });
            });
        var typ = I.New<Xbim.Ifc4.HvacDomain.IfcValveType>(x =>
        {
            x.Name = "V-15 Kugelhahn"; x.PredefinedType = Xbim.Ifc4.Interfaces.IfcValveTypeEnum.ISOLATING;
            x.GlobalId = Xbim.Ifc4.UtilityResource.IfcGloballyUniqueId.ConvertToBase64(new Guid("11111111-1111-1111-1111-111111111111"));
        });
        var tps = I.New<Xbim.Ifc4.Kernel.IfcPropertySet>(x =>
        {
            x.Name = "Pset_ValveTypeCommon";
            x.HasProperties.Add(I.New<Xbim.Ifc4.PropertyResource.IfcPropertySingleValue>(v => { v.Name = "NominalDiameter"; v.NominalValue = new Xbim.Ifc4.MeasureResource.IfcPositiveLengthMeasure(15); }));
            x.HasProperties.Add(I.New<Xbim.Ifc4.PropertyResource.IfcPropertySingleValue>(v => { v.Name = "Reference"; v.NominalValue = new Xbim.Ifc4.MeasureResource.IfcIdentifier("R2015-S1"); }));
        });
        typ.HasPropertySets.Add(tps);
        var pos2 = I.New<Xbim.Ifc4.GeometryResource.IfcAxis2Placement2D>(a => a.Location = I.New<Xbim.Ifc4.GeometryResource.IfcCartesianPoint>(p => p.SetXY(0, 0)));
        var profil = I.New<Xbim.Ifc4.ProfileResource.IfcRectangleProfileDef>(r => { r.ProfileType = Xbim.Ifc4.Interfaces.IfcProfileTypeEnum.AREA; r.XDim = 100; r.YDim = 60; r.Position = pos2; });
        var koerper = I.New<Xbim.Ifc4.GeometricModelResource.IfcExtrudedAreaSolid>(x => { x.SweptArea = profil; x.Depth = 40; x.ExtrudedDirection = D(0, 0, 1); x.Position = I.New<Xbim.Ifc4.GeometryResource.IfcAxis2Placement3D>(a => a.Location = P(0, 0, 0)); });
        var lVentil = L(null, 0, 0, 0);
        var ventil = I.New<Xbim.Ifc4.HvacDomain.IfcValve>(x =>
        {
            x.Name = "Kugelhahn R2015-S1"; x.PredefinedType = Xbim.Ifc4.Interfaces.IfcValveTypeEnum.ISOLATING; x.ObjectPlacement = lVentil;
            x.GlobalId = Xbim.Ifc4.UtilityResource.IfcGloballyUniqueId.ConvertToBase64(new Guid("22222222-2222-2222-2222-222222222222"));
            x.Representation = I.New<Xbim.Ifc4.RepresentationResource.IfcProductDefinitionShape>(s =>
                s.Representations.Add(I.New<Xbim.Ifc4.RepresentationResource.IfcShapeRepresentation>(r =>
                { r.ContextOfItems = ctx; r.RepresentationIdentifier = "Body"; r.RepresentationType = "SweptSolid"; r.Items.Add(koerper); })));
        });
        I.New<Xbim.Ifc4.Kernel.IfcRelDefinesByType>(r => { r.RelatingType = typ; r.RelatedObjects.Add(ventil); });
        Xbim.Ifc4.SharedBldgServiceElements.IfcDistributionPort Port(string n, double x, double[] achse, Xbim.Ifc4.Interfaces.IfcFlowDirectionEnum fluss)
        {
            var p = I.New<Xbim.Ifc4.SharedBldgServiceElements.IfcDistributionPort>(q => { q.Name = n; q.FlowDirection = fluss; q.ObjectPlacement = L(lVentil, x, 0, 20, achse); });
            I.New<Xbim.Ifc4.Kernel.IfcRelNests>(r => { r.RelatingObject = ventil; r.RelatedObjects.Add(p); });
            return p;
        }
        var a = Port("A", -50, [-1, 0, 0], Xbim.Ifc4.Interfaces.IfcFlowDirectionEnum.SINK);
        Port("B", 50, [1, 0, 0], Xbim.Ifc4.Interfaces.IfcFlowDirectionEnum.SOURCE);
        var pps = I.New<Xbim.Ifc4.Kernel.IfcPropertySet>(x =>
        {
            x.Name = "Pset_DistributionPortTypeCommon";
            x.HasProperties.Add(I.New<Xbim.Ifc4.PropertyResource.IfcPropertySingleValue>(v => { v.Name = "NominalDiameter"; v.NominalValue = new Xbim.Ifc4.MeasureResource.IfcPositiveLengthMeasure(15); }));
        });
        I.New<Xbim.Ifc4.Kernel.IfcRelDefinesByProperties>(r => { r.RelatingPropertyDefinition = pps; r.RelatedObjects.Add(a); });
        t.Commit();
        m.SaveAs(pfad, StorageType.Ifc);
        return pfad;
    }

    // ---------- echte Dateien ----------

    public static string? Echt(string relativ) => IfcHilfe.Echt(relativ);

    public static string? EchtOrdner(string relativ)
    {
        var p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "HLKS_CAD_testdaten", relativ);
        return Directory.Exists(p) ? p : null;
    }
}
