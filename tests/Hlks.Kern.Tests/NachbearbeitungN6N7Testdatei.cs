using Xbim.Common;
using Xbim.Common.Step21;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.IO;

namespace Hlks.Kern.Tests;

/// <summary>Synthetische Datei der Nachbearbeitung N6/N7 (IFC4 und IFC2x3, mit xBIM erzeugt; Projekt in mm, EG Kote 0), gebaut
/// nach den Befunden im Archicad-27-IFC Bernstrasse 42 (Zahlen dort abgelesen, Herkunft je Fall):
///   W-Sued (0,0)→(8000,0), W-Nord (8000,4000)→(0,4000): 200 dick, 2600 hoch, Achse an der Aussenfläche.
///   T-Koerper (wie IfcDoor #39395 «A81»): OverallHeight 5190, OverallWidth 940; Öffnung 940 breit von z −3040 bis 2150 (reicht
///     ein Geschoss unter die Tür); Körper MappedItem → IfcFacetedBrep 940 × 60 × 2150 (z 0 … 2150). Erwartet: hoehe 2150.
///   T-OhneKoerper: dieselben Werte, aber ohne Körper (keine Repräsentation) → hoehe bleibt OverallHeight 5190.
///   T-Rahmen: OverallHeight 2100, Körper 2250 hoch (Rahmen 150 über der Öffnung, wie Lift-Türen Bernstrasse bis 170 mm)
///     → hoehe bleibt 2100.
///   F-Koerper (Fenster, gleicher Fall): in W-Nord, OverallHeight 2700, Öffnung z −600 … 2100, Körper z 900 … 2100
///     → hoehe 1200, bruestung 900.
///   UZ-Stahl (wie IfcBeam #28246 «Unterzug-002», UZ-14): Lage (7725.122850360668, −47733.83425578748, 2864.000599711209) mm,
///     x-Achse (−0.6779209389291859, −0.735134817949314, 0); Körper IfcBooleanResult DIFFERENCE aus einer Extrusion des H-Profils
///     (IfcArbitraryClosedProfileDef, 220 × 220) über 9435 mm (Position (0, 9435, 0), Achse (0, −1, 0)) und einer Brep-Kerbe
///     220 mm breit bei 7.355 … 7.575 m. Im IFC liegt der Träger von (14660.97, −54130.13) nach (7725.12, −47733.83).</summary>
public static class NachbearbeitungN6N7Testdatei
{
    static readonly object sperre = new();
    static readonly Dictionary<string, string> dateien = [];

    public const double UzX = 7725.122850360668, UzY = -47733.83425578748, UzZ = 2864.000599711209, UzLaenge = 9435.000036960005;
    public static readonly double[] UzRichtungX = [-0.6779209389291859, -0.735134817949314, 0];

    /// <summary>Anfang und Ende der Trägerachse im IFC (Grundriss, mm): Anfang = Lage + 9435 · y-Achse der Lage, Ende = Lage.</summary>
    public static (double[] Anfang, double[] Ende) UzErwartet()
    {
        double l = Math.Sqrt(UzRichtungX[0] * UzRichtungX[0] + UzRichtungX[1] * UzRichtungX[1]);
        double xx = UzRichtungX[0] / l, xy = UzRichtungX[1] / l;
        double yx = -xy, yy = xx;   // y = z × x
        return ([UzX + UzLaenge * yx, UzY + UzLaenge * yy], [UzX, UzY]);
    }

    public static string Datei(string schema)
    {
        var name = $"n6n7_{schema.ToLowerInvariant()}.ifc";
        lock (sperre)
        {
            if (dateien.TryGetValue(name, out var da)) return da;
            var ordner = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "testdateien_n6n7_" + Environment.ProcessId);
            Directory.CreateDirectory(ordner);
            var pfad = Path.Combine(ordner, name);
            Erzeugen(pfad, schema == "IFC4");
            return dateien[name] = pfad;
        }
    }

    static XbimEditorCredentials Wer => new()
    {
        ApplicationDevelopersName = "H+K Planungs AG", ApplicationFullName = "HLKS-CAD Test N6 N7", ApplicationIdentifier = "hlks-n6n7",
        ApplicationVersion = "1.0", EditorsFamilyName = "Test", EditorsGivenName = "HLKS", EditorsOrganisationName = "H+K Planungs AG",
    };

    static void Erzeugen(string pfad, bool ifc4)
    {
        using var m = IfcStore.Create(Wer, ifc4 ? XbimSchemaVersion.Ifc4 : XbimSchemaVersion.Ifc2X3, XbimStoreType.InMemoryModel);
        using (var tr = m.BeginTransaction("N6N7"))
        {
            var i = m.Instances;
            var c = new EntityCreator(m);
            IIfcProject projekt;
            if (ifc4) { var p4 = i.New<Xbim.Ifc4.Kernel.IfcProject>(p => p.Name = "N6 N7 Test"); p4.Initialize(ProjectUnits.SIUnitsUK); projekt = p4; }
            else { var p2 = i.New<Xbim.Ifc2x3.Kernel.IfcProject>(p => p.Name = "N6 N7 Test"); p2.Initialize(ProjectUnits.SIUnitsUK); projekt = p2; }
            var ctx = i.OfType<IIfcGeometricRepresentationContext>().First();

            IIfcCartesianPoint P(double x, double y, double z) => c.CartesianPoint(p => { p.Coordinates.Add(x); p.Coordinates.Add(y); p.Coordinates.Add(z); });
            IIfcCartesianPoint P2(double x, double y) => c.CartesianPoint(p => { p.Coordinates.Add(x); p.Coordinates.Add(y); });
            IIfcDirection D(double x, double y, double z) => c.Direction(d => { d.DirectionRatios.Add(x); d.DirectionRatios.Add(y); d.DirectionRatios.Add(z); });
            IIfcAxis2Placement3D A3(double x, double y, double z, double[]? rx = null, double[]? ax = null) => c.Axis2Placement3D(a =>
            {
                a.Location = P(x, y, z);
                if (rx is not null || ax is not null) { a.Axis = ax is null ? D(0, 0, 1) : D(ax[0], ax[1], ax[2]); a.RefDirection = rx is null ? D(1, 0, 0) : D(rx[0], rx[1], rx[2]); }
            });
            IIfcLocalPlacement L(IIfcLocalPlacement? rel, double x, double y, double z, double[]? rx = null) =>
                c.LocalPlacement(l => { l.PlacementRelTo = rel; l.RelativePlacement = A3(x, y, z, rx); });
            IIfcShapeRepresentation Rep(string id, string typ, params IIfcRepresentationItem[] items) => c.ShapeRepresentation(r =>
            {
                r.ContextOfItems = ctx; r.RepresentationIdentifier = id; r.RepresentationType = typ;
                foreach (var it in items) r.Items.Add(it);
            });
            IIfcProductDefinitionShape Form(params IIfcShapeRepresentation[] reps) => c.ProductDefinitionShape(s => { foreach (var r in reps) s.Representations.Add(r); });
            IIfcPolyline Linie(params (double X, double Y)[] p) => c.Polyline(pl => { foreach (var q in p) pl.Points.Add(P2(q.X, q.Y)); });
            IIfcExtrudedAreaSolid Extrusion(IIfcProfileDef profil, double tiefe, IIfcAxis2Placement3D? lage = null) => c.ExtrudedAreaSolid(x =>
            {
                x.SweptArea = profil; x.Depth = tiefe; x.ExtrudedDirection = D(0, 0, 1); x.Position = lage ?? A3(0, 0, 0);
            });
            IIfcRectangleProfileDef Rechteck(double dx, double dy, double mx, double my) => c.RectangleProfileDef(r =>
            {
                r.ProfileType = IfcProfileTypeEnum.AREA; r.XDim = dx; r.YDim = dy; r.Position = c.Axis2Placement2D(a => a.Location = P2(mx, my));
            });
            // Quader x0 … x1, y0 … y1, z0 … z1 als IfcFacetedBrep
            IIfcFacetedBrep Quader(double x0, double y0, double z0, double x1, double y1, double z1)
            {
                var e = new IIfcCartesianPoint[8];
                for (int k = 0; k < 8; k++) e[k] = P((k & 1) != 0 ? x1 : x0, (k & 2) != 0 ? y1 : y0, (k & 4) != 0 ? z1 : z0);
                int[][] seiten = [[0, 2, 3, 1], [4, 5, 7, 6], [0, 1, 5, 4], [2, 6, 7, 3], [0, 4, 6, 2], [1, 3, 7, 5]];
                return c.FacetedBrep(b => b.Outer = c.ClosedShell(s =>
                {
                    foreach (var f in seiten)
                        s.CfsFaces.Add(c.Face(fa => fa.Bounds.Add(c.FaceOuterBound(fb => { fb.Orientation = true; fb.Bound = c.PolyLoop(pl => { foreach (var k in f) pl.Polygon.Add(e[k]); }); }))));
                }));
            }
            // Körper als MappedItem (wie Archicad: Typ-Geometrie in einer IfcRepresentationMap)
            IIfcProductDefinitionShape Abgebildet(IIfcRepresentationItem it)
            {
                var karte = c.RepresentationMap(k => { k.MappingOrigin = A3(0, 0, 0); k.MappedRepresentation = Rep("Body", "Brep", it); });
                return Form(Rep("Body", "MappedRepresentation", c.MappedItem(mi => { mi.MappingSource = karte; mi.MappingTarget = c.CartesianTransformationOperator3D(o => o.LocalOrigin = P(0, 0, 0)); })));
            }

            var lSite = L(null, 0, 0, 0);
            var site = c.Site(s => { s.Name = "Gelaende"; s.ObjectPlacement = lSite; });
            var lGeb = L(lSite, 0, 0, 0);
            var geb = c.Building(b => { b.Name = "Testhaus N6"; b.ObjectPlacement = lGeb; });
            var lEg = L(lGeb, 0, 0, 0);
            var eg = c.BuildingStorey(s => { s.Name = "Erdgeschoss"; s.Elevation = 0; s.ObjectPlacement = lEg; });
            c.RelAggregates(r => { r.RelatingObject = projekt; r.RelatedObjects.Add(site); });
            c.RelAggregates(r => { r.RelatingObject = site; r.RelatedObjects.Add(geb); });
            c.RelAggregates(r => { r.RelatingObject = geb; r.RelatedObjects.Add(eg); });
            var imEg = new List<IIfcProduct>();

            IIfcWall Wand(string name, double x, double y, double[] rx, double laenge)
            {
                var w = c.Wall(w =>
                {
                    w.Name = name; w.ObjectPlacement = L(lEg, x, y, 0, rx);
                    w.Representation = Form(Rep("Axis", "Curve2D", Linie((0, 0), (laenge, 0))), Rep("Body", "SweptSolid", Extrusion(Rechteck(laenge, 200, laenge / 2, 100), 2600)));
                });
                c.RelAssociatesMaterial(r => { r.RelatingMaterial = c.Material(mt => mt.Name = "Beton"); r.RelatedObjects.Add(w); });
                imEg.Add(w);
                return w;
            }
            var sued = Wand("W-Sued", 0, 0, [1, 0, 0], 8000);
            var nord = Wand("W-Nord", 8000, 4000, [-1, 0, 0], 8000);

            // Öffnung in der Wand (Lage relativ zur Wand): x0 … x0 + breite längs, z0 … z0 + hoehe
            IIfcOpeningElement Oeffnung(IIfcWall wand, double x0, double breite, double z0, double hoehe)
            {
                var o = c.OpeningElement(o =>
                {
                    o.Name = "Oeffnung"; o.ObjectPlacement = L(wand.ObjectPlacement as IIfcLocalPlacement, x0, 0, z0);
                    o.Representation = Form(Rep("Body", "SweptSolid", Extrusion(Rechteck(breite, 400, breite / 2, 100), hoehe)));
                });
                c.RelVoidsElement(r => { r.RelatingBuildingElement = wand; r.RelatedOpeningElement = o; });
                return o;
            }
            void Tuer(string name, double x0, double overallHeight, double oeffUnten, double oeffHoehe, double? koerperHoehe)
            {
                var oe = Oeffnung(sued, x0, 940, oeffUnten, oeffHoehe);
                var t = c.Door(t =>
                {
                    t.Name = name; t.OverallWidth = 940; t.OverallHeight = overallHeight; t.ObjectPlacement = L(lEg, x0, 0, 0);
                    if (koerperHoehe is double kh) t.Representation = Abgebildet(Quader(0, 70, 0, 940, 130, kh));
                });
                if (ifc4) t.OperationType = IfcDoorTypeOperationEnum.SINGLE_SWING_LEFT;
                c.RelFillsElement(r => { r.RelatingOpeningElement = oe; r.RelatedBuildingElement = t; });
                imEg.Add(t);
            }
            Tuer("T-Koerper", 1000, 5190, -3040, 5190, 2150);
            Tuer("T-OhneKoerper", 3000, 5190, -3040, 5190, null);
            Tuer("T-Rahmen", 5000, 2100, 0, 2100, 2250);

            // Fenster in W-Nord (lokal x = 2000 … 3200, also Welt x 6000 … 4800), Körper z 900 … 2100, Öffnung z −600 … 2100
            var oeF = Oeffnung(nord, 2000, 1200, -600, 2700);
            var fenster = c.Window(f =>
            {
                f.Name = "F-Koerper"; f.OverallWidth = 1200; f.OverallHeight = 2700; f.ObjectPlacement = L(lEg, 6000, 4000, 0, [-1, 0, 0]);
                f.Representation = Abgebildet(Quader(0, 70, 900, 1200, 130, 2100));
            });
            c.RelFillsElement(r => { r.RelatingOpeningElement = oeF; r.RelatedBuildingElement = fenster; });
            imEg.Add(fenster);

            // Stahlträger wie UZ-14 (Zahlen aus #28246, #28247, #27729 in m → mm)
            (double, double)[] h =
            [
                (0, 0), (-110.0002712680377, 0), (-110.000135633996, -15.99981341316272), (-4.999999999999695, -15.99981341316272),
                (-5.000000000001399, -204.001611467336), (-110.000135633996, -204.001611467336), (-110.0002712680377, -220.000542536159),
                (110.0002712680377, -220.000542536159), (110.000135633996, -204.001611467336), (4.999999999998605, -204.001611467336),
                (5.00000000000031, -15.99981341316272), (110.000135633996, -15.99981341316272), (110.0002712680377, 0), (0, 0),
            ];
            var profil = c.ArbitraryClosedProfileDef(a => { a.ProfileType = IfcProfileTypeEnum.AREA; a.ProfileName = "Universal H-Träger"; a.OuterCurve = Linie(h); });
            var extrusion = Extrusion(profil, UzLaenge, A3(0, UzLaenge, 0, [1, 0, 0], [0, -1, 0]));
            var kerbe = Quader(-120, 2000, -230, 120, 2220, 10);   // Kerbe längs 7.215 … 7.435 m ab Anfang, quer über den Träger
            var unterzug = c.Beam(b =>
            {
                b.Name = "UZ-Stahl"; b.ObjectPlacement = L(lEg, UzX, UzY, UzZ, UzRichtungX);
                b.Representation = Form(Rep("Body", "CSG", c.BooleanResult(r => { r.Operator = IfcBooleanOperator.DIFFERENCE; r.FirstOperand = extrusion; r.SecondOperand = kerbe; })));
            });
            c.RelAssociatesMaterial(r => { r.RelatingMaterial = c.Material(mt => mt.Name = "Baustahl S235"); r.RelatedObjects.Add(unterzug); });
            imEg.Add(unterzug);

            c.RelContainedInSpatialStructure(r => { r.RelatingStructure = eg; foreach (var x in imEg) r.RelatedElements.Add(x); });
            tr.Commit();
        }
        m.SaveAs(pfad, StorageType.Ifc);
    }
}
