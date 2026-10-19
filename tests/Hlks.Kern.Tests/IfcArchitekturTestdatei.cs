using Xbim.Common;
using Xbim.Common.Step21;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.IO;

namespace Hlks.Kern.Tests;

/// <summary>Synthetische Architektur-Datei (IFC4 und IFC2x3) für die Tests von B1, mit xBIM erzeugt (EntityCreator: dieselbe
/// Beschreibung für beide Fassungen; was es nur in einer Fassung gibt, über deren Klassen). Projekt in mm, EG Kote 0,
/// 1.OG Kote 3000. Inhalt (EG, alle Wände 200 dick, 2600 hoch, Achse an der Aussenfläche, Schichten in +y der Wand):
///   W-Sued  (0,0)→(6000,0)      Schichtaufbau Beton 150 / Wärmedämmung 50, tragend; Tür 900 × 2100 bei x = 3000
///   W-Ost   (6000,0)→(6000,4000) Material «MW, Kalksandstein»
///   W-Nord  (6000,4000)→(0,4000) Material Beton; Fenster 1200 × 1200, Brüstung 900, Mitte x = 2000
///   W-West  (0,4000)→(0,0)       Material Gipsplatte
///   Rundwand (Achse Kreisbogen, Hintergrund), Stütze 300 × 300 bei (3000, 2000) als MappedItem, Unterzug 200 × 200 von
///   (1000, 3000) bis (5000, 3000), Unterkante 2400 (ohne Achse, Extrusion längs), Objekt (IfcBuildingElementProxy) als
///   FacetedBrep 1000 × 1000 × 2000 bei (8000..9000, 0..1000) im Hintergrund.
/// 1.OG: Decke 6000 × 4000 mit Loch 1000 × 1000, Oberkante 3000, Dicke 250 (Profil mit Loch); Dach (IfcSlab ROOF) Oberkante
///   6000, Dicke 250; Wand aus Teilen (nur Achse, Beton 150 und Dämmung 50 als IfcBuildingElementPart) (0,−3000)→(4000,−3000);
///   IFC4 zusätzlich eine Wand mit IndexedPolyCurve-Achse und PolygonalFaceSet-Körper (0,6000)→(3000,6000) und eine Rundwand
///   mit IfcArcIndex.
/// Raster mit Achsen A (x = 0), B (x = 6000), 1 (y = 0), 2 (y = 4000). Mit Raum: IfcSpace 1.01 «Wohnen» 5600 × 3600, 2500 hoch.</summary>
public static class IfcArchitekturTestdatei
{
    static readonly object sperre = new();
    static readonly Dictionary<string, string> dateien = [];

    /// <summary>Pfad der Testdatei (einmal je Fassung und Variante erzeugt, im Temp-Ordner).</summary>
    public static string Datei(string schema, bool mitRaum)
    {
        var name = $"b1_architektur_{schema.ToLowerInvariant()}{(mitRaum ? "_raum" : "")}.ifc";
        lock (sperre)
        {
            if (dateien.TryGetValue(name, out var da)) return da;
            var ordner = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "testdateien_b1_" + Environment.ProcessId);
            Directory.CreateDirectory(ordner);
            var pfad = Path.Combine(ordner, name);
            Erzeugen(pfad, schema == "IFC4", mitRaum);
            return dateien[name] = pfad;
        }
    }

    static XbimEditorCredentials Wer => new()
    {
        ApplicationDevelopersName = "H+K Planungs AG", ApplicationFullName = "HLKS-CAD Architekturtest", ApplicationIdentifier = "hlks-b1",
        ApplicationVersion = "1.0", EditorsFamilyName = "Test", EditorsGivenName = "HLKS", EditorsOrganisationName = "H+K Planungs AG",
    };

    static void Erzeugen(string pfad, bool ifc4, bool mitRaum)
    {
        using var m = IfcStore.Create(Wer, ifc4 ? XbimSchemaVersion.Ifc4 : XbimSchemaVersion.Ifc2X3, XbimStoreType.InMemoryModel);
        using (var tr = m.BeginTransaction("B1"))
        {
            var i = m.Instances;
            var c = new EntityCreator(m);
            IIfcProject projekt;
            if (ifc4) { var p4 = i.New<Xbim.Ifc4.Kernel.IfcProject>(p => p.Name = "B1 Test"); p4.Initialize(ProjectUnits.SIUnitsUK); projekt = p4; }
            else { var p2 = i.New<Xbim.Ifc2x3.Kernel.IfcProject>(p => p.Name = "B1 Test"); p2.Initialize(ProjectUnits.SIUnitsUK); projekt = p2; }
            var ctx = i.OfType<IIfcGeometricRepresentationContext>().First();

            IIfcCartesianPoint P(double x, double y, double z) => c.CartesianPoint(p => { p.Coordinates.Add(x); p.Coordinates.Add(y); p.Coordinates.Add(z); });
            IIfcCartesianPoint P2(double x, double y) => c.CartesianPoint(p => { p.Coordinates.Add(x); p.Coordinates.Add(y); });
            IIfcDirection D(double x, double y, double z) => c.Direction(d => { d.DirectionRatios.Add(x); d.DirectionRatios.Add(y); d.DirectionRatios.Add(z); });
            IIfcAxis2Placement3D A3(double x, double y, double z, double grad = 0) => c.Axis2Placement3D(a =>
            {
                a.Location = P(x, y, z);
                if (grad != 0) { a.Axis = D(0, 0, 1); a.RefDirection = D(Math.Round(Math.Cos(grad * Math.PI / 180), 12), Math.Round(Math.Sin(grad * Math.PI / 180), 12), 0); }
            });
            IIfcAxis2Placement2D A2(double x, double y) => c.Axis2Placement2D(a => a.Location = P2(x, y));
            IIfcLocalPlacement L(IIfcLocalPlacement? rel, double x, double y, double z, double grad = 0) =>
                c.LocalPlacement(l => { l.PlacementRelTo = rel; l.RelativePlacement = A3(x, y, z, grad); });
            IIfcShapeRepresentation Rep(string id, string typ, params IIfcRepresentationItem[] items) => c.ShapeRepresentation(r =>
            {
                r.ContextOfItems = ctx; r.RepresentationIdentifier = id; r.RepresentationType = typ;
                foreach (var it in items) r.Items.Add(it);
            });
            IIfcProductDefinitionShape Form(params IIfcShapeRepresentation[] reps) => c.ProductDefinitionShape(s => { foreach (var r in reps) s.Representations.Add(r); });
            IIfcPolyline Linie(params (double X, double Y)[] p) => c.Polyline(pl => { foreach (var q in p) pl.Points.Add(P2(q.X, q.Y)); });
            IIfcExtrudedAreaSolid Extrusion(IIfcProfileDef profil, double tiefe, IIfcAxis2Placement3D? lage = null, IIfcDirection? richtung = null) => c.ExtrudedAreaSolid(x =>
            {
                x.SweptArea = profil; x.Depth = tiefe; x.ExtrudedDirection = richtung ?? D(0, 0, 1); x.Position = lage ?? A3(0, 0, 0);
            });
            IIfcRectangleProfileDef Rechteck(double dx, double dy, double mx, double my) => c.RectangleProfileDef(r =>
            {
                r.ProfileType = IfcProfileTypeEnum.AREA; r.XDim = dx; r.YDim = dy; r.Position = A2(mx, my);
            });
            IIfcMaterial Mat(string name) => c.Material(x => x.Name = name);
            void MatZu(IIfcMaterialSelect mat, params IIfcObjectDefinition[] o) => c.RelAssociatesMaterial(r => { r.RelatingMaterial = mat; foreach (var x in o) r.RelatedObjects.Add(x); });
            void Pset(IIfcObject o, string name, string eig, IIfcValue wert) => c.RelDefinesByProperties(r =>
            {
                r.RelatingPropertyDefinition = c.PropertySet(ps => { ps.Name = name; ps.HasProperties.Add(c.PropertySingleValue(v => { v.Name = eig; v.NominalValue = wert; })); });
                r.RelatedObjects.Add(o);
            });
            void Menge(IIfcObject o, string name, params (string Name, double Wert)[] l) => c.RelDefinesByProperties(r =>
            {
                r.RelatingPropertyDefinition = c.ElementQuantity(q => { q.Name = name; foreach (var (n, w) in l) q.Quantities.Add(c.QuantityLength(x => { x.Name = n; x.LengthValue = w; })); });
                r.RelatedObjects.Add(o);
            });

            // Gliederung
            var lSite = L(null, 0, 0, 0);
            var site = c.Site(s => { s.Name = "Gelaende"; s.ObjectPlacement = lSite; });
            var lGeb = L(lSite, 0, 0, 0);
            var geb = c.Building(b => { b.Name = "Testhaus"; b.ObjectPlacement = lGeb; });
            var lEg = L(lGeb, 0, 0, 0);
            var eg = c.BuildingStorey(s => { s.Name = "Erdgeschoss"; s.Elevation = 0; s.ObjectPlacement = lEg; });
            var lOg = L(lGeb, 0, 0, 3000);
            var og = c.BuildingStorey(s => { s.Name = "1. Obergeschoss"; s.Elevation = 3000; s.ObjectPlacement = lOg; });
            c.RelAggregates(r => { r.RelatingObject = projekt; r.RelatedObjects.Add(site); });
            c.RelAggregates(r => { r.RelatingObject = site; r.RelatedObjects.Add(geb); });
            c.RelAggregates(r => { r.RelatingObject = geb; r.RelatedObjects.Add(eg); r.RelatedObjects.Add(og); });
            var imEg = new List<IIfcProduct>();
            var imOg = new List<IIfcProduct>();

            // Wände: Achse an der Aussenfläche (lokal y = 0), Körper in +y (0 … 200), über die ganze Achslänge
            IIfcWall Wand(string name, double x, double y, double grad, double laenge, IIfcMaterialSelect mat, bool schichten)
            {
                var w = c.Wall(w =>
                {
                    w.Name = name; w.ObjectPlacement = L(lEg, x, y, 0, grad);
                    w.Representation = Form(Rep("Axis", "Curve2D", Linie((0, 0), (laenge, 0))), Rep("Body", "SweptSolid", Extrusion(Rechteck(laenge, 200, laenge / 2, 100), 2600)));
                });
                MatZu(mat, w);
                if (!schichten) Menge(w, "Qto_WallBaseQuantities", ("Width", 200), ("Height", 2600));
                imEg.Add(w);
                return w;
            }
            var lsu = c.MaterialLayerSetUsage(u =>
            {
                u.ForLayerSet = c.MaterialLayerSet(s =>
                {
                    s.LayerSetName = "Aussenwand";
                    s.MaterialLayers.Add(c.MaterialLayer(l => { l.Material = Mat("Beton"); l.LayerThickness = 150; }));
                    s.MaterialLayers.Add(c.MaterialLayer(l => { l.Material = Mat("Wärmedämmung"); l.LayerThickness = 50; }));
                });
                u.LayerSetDirection = IfcLayerSetDirectionEnum.AXIS2; u.DirectionSense = IfcDirectionSenseEnum.POSITIVE; u.OffsetFromReferenceLine = 0;
            });
            var sued = Wand("W-Sued", 0, 0, 0, 6000, lsu, true);
            Pset(sued, "Pset_WallCommon", "LoadBearing", new Xbim.Ifc4.MeasureResource.IfcBoolean(true));
            Wand("W-Ost", 6000, 0, 90, 4000, Mat("MW, Kalksandstein"), false);
            var nord = Wand("W-Nord", 6000, 4000, 180, 6000, Mat("Beton, Stahlbeton"), false);
            Wand("W-West", 0, 4000, 270, 4000, Mat("Gipsplatte"), false);

            // Tür in W-Sued: Öffnung 900 breit, 2100 hoch bei x = 2550 … 3450; Tür öffnet in +y (ins Rechteck), Band links (−x)
            var oeffT = c.OpeningElement(o =>
            {
                o.Name = "Oeffnung Tuer"; o.ObjectPlacement = L(sued.ObjectPlacement as IIfcLocalPlacement, 2550, 0, 0);
                o.Representation = Form(Rep("Body", "SweptSolid", Extrusion(Rechteck(900, 400, 450, 100), 2100)));
            });
            c.RelVoidsElement(r => { r.RelatingBuildingElement = sued; r.RelatedOpeningElement = oeffT; });
            var tuer = c.Door(t =>
            {
                t.Name = "T 0.01"; t.OverallWidth = 900; t.OverallHeight = 2100; t.ObjectPlacement = L(lEg, 2550, 0, 0);
                t.Representation = Form(Rep("Body", "SweptSolid", Extrusion(Rechteck(900, 40, 450, 100), 2100)));
            });
            if (ifc4) tuer.OperationType = IfcDoorTypeOperationEnum.SINGLE_SWING_LEFT;
            else
            {
                var stil = i.New<Xbim.Ifc2x3.SharedBldgElements.IfcDoorStyle>(s =>
                {
                    s.Name = "Drehtuer"; s.OperationType = Xbim.Ifc2x3.SharedBldgElements.IfcDoorStyleOperationEnum.SINGLE_SWING_LEFT;
                    s.ConstructionType = Xbim.Ifc2x3.SharedBldgElements.IfcDoorStyleConstructionEnum.WOOD;
                });
                c.RelDefinesByType(r => { r.RelatingType = stil; r.RelatedObjects.Add(tuer); });
            }
            c.RelFillsElement(r => { r.RelatingOpeningElement = oeffT; r.RelatedBuildingElement = tuer; });
            imEg.Add(tuer);

            // Fenster in W-Nord: Mitte x = 2000, Breite 1200, Höhe 1200, Brüstung 900 (in W-Nord lokal: x = 3400 … 4600)
            var oeffF = c.OpeningElement(o =>
            {
                o.Name = "Oeffnung Fenster"; o.ObjectPlacement = L(nord.ObjectPlacement as IIfcLocalPlacement, 3400, 0, 900);
                o.Representation = Form(Rep("Body", "SweptSolid", Extrusion(Rechteck(1200, 400, 600, 100), 1200)));
            });
            c.RelVoidsElement(r => { r.RelatingBuildingElement = nord; r.RelatedOpeningElement = oeffF; });
            var fenster = c.Window(f =>
            {
                f.Name = "F 0.01"; f.OverallWidth = 1200; f.OverallHeight = 1200; f.ObjectPlacement = L(lEg, 2600, 4000, 900, 180);
                f.Representation = Form(Rep("Body", "SweptSolid", Extrusion(Rechteck(1200, 60, 600, 100), 1200)));
            });
            c.RelFillsElement(r => { r.RelatingOpeningElement = oeffF; r.RelatedBuildingElement = fenster; });
            imEg.Add(fenster);

            // Rundwand: Achse Viertelkreis (Hintergrund)
            var kreis = c.Circle(k => { k.Radius = 2000; k.Position = c.Axis2Placement2D(a => a.Location = P2(0, 0)); });
            var bogen = c.TrimmedCurve(t =>
            {
                t.BasisCurve = kreis; t.Trim1.Add(P2(2000, 0)); t.Trim2.Add(P2(0, 2000)); t.SenseAgreement = true; t.MasterRepresentation = IfcTrimmingPreference.CARTESIAN;
            });
            imEg.Add(c.Wall(w =>
            {
                w.Name = "Rundwand"; w.ObjectPlacement = L(lEg, 12000, 0, 0);
                w.Representation = Form(Rep("Axis", "Curve2D", bogen), Rep("Body", "SweptSolid", Extrusion(Rechteck(2000, 200, 1000, 100), 2600)));
            }));

            // Stütze als MappedItem (Typ-Geometrie 300 × 300 × 2600, Ursprung in der Mitte)
            var karte = c.RepresentationMap(k => { k.MappingOrigin = A3(0, 0, 0); k.MappedRepresentation = Rep("Body", "SweptSolid", Extrusion(Rechteck(300, 300, 0, 0), 2600)); });
            var stuetze = c.Column(s =>
            {
                s.Name = "Stuetze 1"; s.ObjectPlacement = L(lEg, 3000, 2000, 0);
                s.Representation = Form(Rep("Body", "MappedRepresentation", c.MappedItem(mi => { mi.MappingSource = karte; mi.MappingTarget = c.CartesianTransformationOperator3D(o => o.LocalOrigin = P(0, 0, 0)); })));
            });
            MatZu(Mat("Stahlbeton"), stuetze);
            imEg.Add(stuetze);

            // Unterzug: Profil 200 × 200 in der Ebene y-z, Extrusion längs +x über 4000
            var unterzug = c.Beam(b =>
            {
                b.Name = "UZ 1"; b.ObjectPlacement = L(lEg, 1000, 3000, 2500);
                b.Representation = Form(Rep("Body", "SweptSolid", Extrusion(Rechteck(200, 200, 0, 0), 4000,
                    c.Axis2Placement3D(a => { a.Location = P(0, 0, 0); a.Axis = D(1, 0, 0); a.RefDirection = D(0, 1, 0); }))));
            });
            MatZu(Mat("Baustahl S235"), unterzug);
            imEg.Add(unterzug);

            // Objekt (Hintergrund): Quader als FacetedBrep
            imEg.Add(c.BuildingElementProxy(x =>
            {
                x.Name = "Objekt Quader"; x.ObjectPlacement = L(lEg, 8000, 0, 0);
                x.Representation = Form(Rep("Body", "Brep", Brep(c, P, 1000, 1000, 2000)));
            }));

            // 1.OG: Decke mit Loch (Profil mit Aussparung), Oberkante 3000 → im 1.OG z = −250 … 0
            var decke = c.Slab(s =>
            {
                s.Name = "Decke 1.OG"; s.PredefinedType = IfcSlabTypeEnum.FLOOR; s.ObjectPlacement = L(lOg, 0, 0, -250);
                var profil = c.ArbitraryProfileDefWithVoids(a =>
                {
                    a.ProfileType = IfcProfileTypeEnum.AREA;
                    a.OuterCurve = Linie((0, 0), (6000, 0), (6000, 4000), (0, 4000), (0, 0));
                    a.InnerCurves.Add(Linie((1000, 1000), (1000, 2000), (2000, 2000), (2000, 1000), (1000, 1000)));
                });
                s.Representation = Form(Rep("Body", "SweptSolid", Extrusion(profil, 250)));
            });
            MatZu(Mat("Beton"), decke);
            imOg.Add(decke);
            imOg.Add(c.Slab(s =>
            {
                s.Name = "Dach"; s.PredefinedType = IfcSlabTypeEnum.ROOF; s.ObjectPlacement = L(lOg, 0, 0, 2750);
                s.Representation = Form(Rep("Body", "SweptSolid", Extrusion(Rechteck(6000, 4000, 3000, 2000), 250)));
            }));

            // Wand aus Teilen (wie Archicad 27, IFC2x3): nur Achse, Körper in zwei IfcBuildingElementPart (Beton 150, Dämmung 50)
            var wt = c.Wall(w => { w.Name = "W-Teile"; w.ObjectPlacement = L(lOg, 0, -3000, 0); w.Representation = Form(Rep("Axis", "Curve2D", Linie((0, 0), (4000, 0)))); });
            Menge(wt, "BaseQuantities", ("Width", 200), ("Height", 2600));
            foreach (var (name, y0, dy) in new[] { ("Beton", 0.0, 150.0), ("Dämmung", 150.0, 50.0) })
            {
                var teil = c.BuildingElementPart(x =>
                {
                    x.Name = name; x.ObjectPlacement = L(wt.ObjectPlacement as IIfcLocalPlacement, 0, 0, 0);
                    x.Representation = Form(Rep("Body", "SweptSolid", Extrusion(Rechteck(4000, dy, 2000, y0 + dy / 2), 2600)));
                });
                MatZu(Mat(name), teil);
                c.RelAggregates(r => { r.RelatingObject = wt; r.RelatedObjects.Add(teil); });
            }
            imOg.Add(wt);

            if (ifc4)
            {
                // Wand wie Archicad 28: Achse IndexedPolyCurve, Körper PolygonalFaceSet (Quader 3000 × 150 × 2800, Achse in der Mitte)
                Xbim.Ifc4.GeometricModelResource.IfcCartesianPointList2D Liste2(params double[][] q) => i.New<Xbim.Ifc4.GeometricModelResource.IfcCartesianPointList2D>(l =>
                {
                    for (int k = 0; k < q.Length; k++) l.CoordList.GetAt(k).AddRange(q[k].Select(v => (Xbim.Ifc4.MeasureResource.IfcLengthMeasure)v));
                });
                var achse = i.New<Xbim.Ifc4.GeometryResource.IfcIndexedPolyCurve>(k => k.Points = Liste2([0, 0], [3000, 0]));
                // Rundwand wie Archicad 28: IndexedPolyCurve mit Bogen (IfcArcIndex) → Hintergrund
                var bogenIfc4 = i.New<Xbim.Ifc4.GeometryResource.IfcIndexedPolyCurve>(k =>
                {
                    k.Points = Liste2([0, 0], [1000, 267.949], [1732.051, 1000]);
                    k.Segments.Add(new Xbim.Ifc4.GeometryResource.IfcArcIndex([1, 2, 3]));
                });
                imOg.Add(c.Wall(w =>
                {
                    w.Name = "Rundwand IFC4"; w.ObjectPlacement = L(lOg, 20000, 0, 0);
                    w.Representation = Form(Rep("Axis", "Curve2D", bogenIfc4), Rep("Body", "SweptSolid", Extrusion(Rechteck(2000, 200, 1000, 100), 2600)));
                }));
                var fs = i.New<Xbim.Ifc4.GeometricModelResource.IfcPolygonalFaceSet>(f =>
                {
                    f.Closed = true;
                    f.Coordinates = i.New<Xbim.Ifc4.GeometricModelResource.IfcCartesianPointList3D>(l =>
                    {
                        double[][] q = [[0, -75, 0], [3000, -75, 0], [3000, 75, 0], [0, 75, 0], [0, -75, 2800], [3000, -75, 2800], [3000, 75, 2800], [0, 75, 2800]];
                        for (int k = 0; k < q.Length; k++) l.CoordList.GetAt(k).AddRange(q[k].Select(v => (Xbim.Ifc4.MeasureResource.IfcLengthMeasure)v));
                    });
                    int[][] seiten = [[1, 4, 3, 2], [5, 6, 7, 8], [1, 2, 6, 5], [2, 3, 7, 6], [3, 4, 8, 7], [4, 1, 5, 8]];
                    foreach (var s in seiten)
                        f.Faces.Add(i.New<Xbim.Ifc4.GeometricModelResource.IfcIndexedPolygonalFace>(x => x.CoordIndex.AddRange(s.Select(v => (Xbim.Ifc4.MeasureResource.IfcPositiveInteger)v))));
                });
                var w5 = c.Wall(w =>
                {
                    w.Name = "W-OG"; w.ObjectPlacement = L(lOg, 0, 6000, 0);
                    w.Representation = Form(Rep("Axis", "Curve2D", achse), Rep("Body", "Tessellation", fs));
                });
                MatZu(Mat("Beton, Stahlbeton"), w5);
                Menge(w5, "Qto_WallBaseQuantities", ("Width", 150), ("Height", 2800));
                imOg.Add(w5);
            }

            // Raster
            imEg.Add(c.Grid(g =>
            {
                g.Name = "Raster"; g.ObjectPlacement = L(lEg, 0, 0, 0);
                g.UAxes.Add(c.GridAxis(a => { a.AxisTag = "A"; a.AxisCurve = Linie((0, -1000), (0, 5000)); a.SameSense = true; }));
                g.UAxes.Add(c.GridAxis(a => { a.AxisTag = "B"; a.AxisCurve = Linie((6000, -1000), (6000, 5000)); a.SameSense = true; }));
                g.VAxes.Add(c.GridAxis(a => { a.AxisTag = "1"; a.AxisCurve = Linie((-1000, 0), (7000, 0)); a.SameSense = true; }));
                g.VAxes.Add(c.GridAxis(a => { a.AxisTag = "2"; a.AxisCurve = Linie((-1000, 4000), (7000, 4000)); a.SameSense = true; }));
            }));

            if (mitRaum)
            {
                var raum = c.Space(s =>
                {
                    s.Name = "1.01"; s.LongName = "Wohnen"; s.ObjectType = "Wohnen"; s.ObjectPlacement = L(lEg, 0, 0, 0);
                    s.Representation = Form(Rep("Body", "SweptSolid", Extrusion(Rechteck(5600, 3600, 3000, 2000), 2500)));
                });
                c.RelAggregates(r => { r.RelatingObject = eg; r.RelatedObjects.Add(raum); });
            }

            c.RelContainedInSpatialStructure(r => { r.RelatingStructure = eg; foreach (var x in imEg) r.RelatedElements.Add(x); });
            c.RelContainedInSpatialStructure(r => { r.RelatingStructure = og; foreach (var x in imOg) r.RelatedElements.Add(x); });
            tr.Commit();
        }
        m.SaveAs(pfad, StorageType.Ifc);
    }

    /// <summary>Quader 0 … lx × 0 … ly × 0 … lz als IfcFacetedBrep (sechs Vierecke, gemeinsame Punkte).</summary>
    static IIfcFacetedBrep Brep(EntityCreator c, Func<double, double, double, IIfcCartesianPoint> P, double lx, double ly, double lz)
    {
        var e = new IIfcCartesianPoint[8];
        for (int k = 0; k < 8; k++) e[k] = P((k & 1) != 0 ? lx : 0, (k & 2) != 0 ? ly : 0, (k & 4) != 0 ? lz : 0);
        int[][] seiten = [[0, 2, 3, 1], [4, 5, 7, 6], [0, 1, 5, 4], [2, 6, 7, 3], [0, 4, 6, 2], [1, 3, 7, 5]];
        return c.FacetedBrep(b => b.Outer = c.ClosedShell(s =>
        {
            foreach (var f in seiten)
                s.CfsFaces.Add(c.Face(fa => fa.Bounds.Add(c.FaceOuterBound(fb =>
                {
                    fb.Orientation = true;
                    fb.Bound = c.PolyLoop(pl => { foreach (var k in f) pl.Polygon.Add(e[k]); });
                }))));
        }));
    }
}
