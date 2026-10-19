using Xbim.Common;
using Xbim.Common.Step21;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.IO;

namespace Hlks.Kern.Tests;

/// <summary>Synthetische Architekten-IFC (IFC4, mit xBIM wie IfcNetzFassung2Testdatei) in drei Fassungen unter demselben Dateinamen
/// «b3_architekt.ifc» (gleicher Name = neue Fassung derselben Unterlage) – für den Abgleich der Aussparungen (Modul 3b, B3).
/// Projekt in mm, EG Kote 0, 1.OG Kote 3000; GlobalIds fest (<see cref="Gid"/>):
///   W-X  Wand 6000 × 200 × 2600 bei y 0 … 200 im EG (Körper nicht ausgeschnitten, wie Archicad).
///   D-X  Decke im 1.OG, Oberkante 3000, Dicke 250 (z 2750 … 3000), 6000 × 4000.
/// Fassungen:
///   ohne        nur Wand und Decke.
///   mit         Öffnung O-X1 in W-X x 1350 … 1650, z 1050 … 1350 (deckt eine Aussparung Ø 95 bei x 1500, z 1200 ab);
///               Öffnung O-X2 x 4460 … 4540, z 1160 … 1240 (zu klein für Ø 95 bei x 4500, z 1200);
///               Decke mit Loch x 2700 … 3300, y 1800 … 2200 im Profil (Netz ausgeschnitten, wie Revit).
///   nachgeholt  wie «mit», aber O-X2 x 4350 … 4650, z 1050 … 1350.
///   ohnewand    nur die Decke (wie «ohne») – die Wand fehlt in dieser Fassung.</summary>
public static class AussparungIfcTestdatei
{
    static readonly object sperre = new();
    static readonly Dictionary<string, string> dateien = [];

    /// <summary>Feste GlobalId aus dem Namen («W-X» → «4WX000…», 22 Zeichen).</summary>
    public static string Gid(string name) => ("4" + new string(name.Where(char.IsAsciiLetterOrDigit).ToArray())).PadRight(22, '0')[..22];

    public static string Datei(string fassung)
    {
        lock (sperre)
        {
            if (dateien.TryGetValue(fassung, out var da)) return da;
            var ordner = Path.Combine(Path.GetTempPath(), "HLKS_CAD", $"testdateien_b3_{Environment.ProcessId}", fassung);
            Directory.CreateDirectory(ordner);
            var pfad = Path.Combine(ordner, "b3_architekt.ifc");
            Erzeugen(pfad, fassung);
            return dateien[fassung] = pfad;
        }
    }

    static XbimEditorCredentials Wer => new()
    {
        ApplicationDevelopersName = "H+K Planungs AG", ApplicationFullName = "HLKS-CAD Aussparungs-Test", ApplicationIdentifier = "hlks-b3",
        ApplicationVersion = "1.0", EditorsFamilyName = "Test", EditorsGivenName = "HLKS", EditorsOrganisationName = "H+K Planungs AG",
    };

    static void Erzeugen(string pfad, string fassung)
    {
        using var m = IfcStore.Create(Wer, XbimSchemaVersion.Ifc4, XbimStoreType.InMemoryModel);
        using (var tr = m.BeginTransaction("B3"))
        {
            var i = m.Instances;
            var c = new EntityCreator(m);
            var projekt = i.New<Xbim.Ifc4.Kernel.IfcProject>(p => p.Name = "B3 Test");
            projekt.Initialize(ProjectUnits.SIUnitsUK);
            var ctx = i.OfType<IIfcGeometricRepresentationContext>().First();

            IIfcCartesianPoint P(double x, double y, double z) => c.CartesianPoint(p => { p.Coordinates.Add(x); p.Coordinates.Add(y); p.Coordinates.Add(z); });
            IIfcCartesianPoint P2(double x, double y) => c.CartesianPoint(p => { p.Coordinates.Add(x); p.Coordinates.Add(y); });
            IIfcDirection D(double x, double y, double z) => c.Direction(d => { d.DirectionRatios.Add(x); d.DirectionRatios.Add(y); d.DirectionRatios.Add(z); });
            IIfcAxis2Placement3D A3(double x, double y, double z) => c.Axis2Placement3D(a => a.Location = P(x, y, z));
            IIfcLocalPlacement L(IIfcObjectPlacement? rel, double x, double y, double z) =>
                c.LocalPlacement(l => { l.PlacementRelTo = rel; l.RelativePlacement = A3(x, y, z); });
            IIfcShapeRepresentation Rep(string id, string typ, IIfcRepresentationItem item) => c.ShapeRepresentation(r =>
            {
                r.ContextOfItems = ctx; r.RepresentationIdentifier = id; r.RepresentationType = typ; r.Items.Add(item);
            });
            IIfcProductDefinitionShape Form(params IIfcShapeRepresentation[] reps) => c.ProductDefinitionShape(s => { foreach (var r in reps) s.Representations.Add(r); });
            IIfcPolyline Linie(params (double X, double Y)[] p) => c.Polyline(pl => { foreach (var q in p) pl.Points.Add(P2(q.X, q.Y)); });
            IIfcRectangleProfileDef Rechteck(double dx, double dy, double mx, double my) => c.RectangleProfileDef(r =>
            {
                r.ProfileType = IfcProfileTypeEnum.AREA; r.XDim = dx; r.YDim = dy; r.Position = c.Axis2Placement2D(a => a.Location = P2(mx, my));
            });
            IIfcExtrudedAreaSolid Extrusion(IIfcProfileDef profil, double tiefe) => c.ExtrudedAreaSolid(x =>
            {
                x.SweptArea = profil; x.Depth = tiefe; x.ExtrudedDirection = D(0, 0, 1); x.Position = A3(0, 0, 0);
            });
            void MatZu(string name, IIfcObjectDefinition o) => c.RelAssociatesMaterial(r => { r.RelatingMaterial = c.Material(x => x.Name = name); r.RelatedObjects.Add(o); });

            var lSite = L(null, 0, 0, 0);
            var site = c.Site(s => { s.Name = "Gelaende"; s.ObjectPlacement = lSite; });
            var lGeb = L(lSite, 0, 0, 0);
            var geb = c.Building(b => { b.Name = "Testhaus B3"; b.ObjectPlacement = lGeb; });
            var lEg = L(lGeb, 0, 0, 0);
            var eg = c.BuildingStorey(s => { s.Name = "Erdgeschoss"; s.Elevation = 0; s.ObjectPlacement = lEg; });
            var lOg = L(lGeb, 0, 0, 3000);
            var og = c.BuildingStorey(s => { s.Name = "1. Obergeschoss"; s.Elevation = 3000; s.ObjectPlacement = lOg; });
            c.RelAggregates(r => { r.RelatingObject = projekt; r.RelatedObjects.Add(site); });
            c.RelAggregates(r => { r.RelatingObject = site; r.RelatedObjects.Add(geb); });
            c.RelAggregates(r => { r.RelatingObject = geb; r.RelatedObjects.Add(eg); r.RelatedObjects.Add(og); });

            // W-X: Achse und Körper, Breite/Höhe als Mengen (fehlt in der Fassung «ohnewand»)
            IIfcWall? wand = null;
            if (fassung != "ohnewand")
            {
                wand = c.Wall(x =>
                {
                    x.Name = "W-X"; x.GlobalId = Gid("W-X"); x.ObjectPlacement = L(lEg, 0, 0, 0);
                    x.Representation = Form(Rep("Axis", "Curve2D", Linie((0, 0), (6000, 0))), Rep("Body", "SweptSolid", Extrusion(Rechteck(6000, 200, 3000, 100), 2600)));
                });
                MatZu("Beton", wand);
                var w = wand;
                c.RelDefinesByProperties(r =>
                {
                    r.RelatingPropertyDefinition = c.ElementQuantity(q =>
                    {
                        q.Name = "Qto_WallBaseQuantities";
                        q.Quantities.Add(c.QuantityLength(x => { x.Name = "Width"; x.LengthValue = 200; }));
                        q.Quantities.Add(c.QuantityLength(x => { x.Name = "Height"; x.LengthValue = 2600; }));
                    });
                    r.RelatedObjects.Add(w);
                });
            }
            void Oeffnung(string name, double x0, double z0, double b, double h)
            {
                var o = c.OpeningElement(op =>
                {
                    op.Name = name; op.GlobalId = Gid(name); op.ObjectPlacement = L(wand!.ObjectPlacement, x0, 0, z0);
                    op.Representation = Form(Rep("Body", "SweptSolid", Extrusion(Rechteck(b, 400, b / 2, 100), h)));
                });
                c.RelVoidsElement(r => { r.RelatingBuildingElement = wand; r.RelatedOpeningElement = o; });
            }
            if (fassung is "mit" or "nachgeholt")
            {
                Oeffnung("O-X1", 1350, 1050, 300, 300);
                if (fassung == "nachgeholt") Oeffnung("O-X2", 4350, 1050, 300, 300);
                else Oeffnung("O-X2", 4460, 1160, 80, 80);
            }

            // D-X: Decke im 1.OG, Oberkante 3000 (Platzierung −250, Extrusion 250); mit Loch im Profil
            IIfcProfileDef profil = fassung is "ohne" or "ohnewand"
                ? Rechteck(6000, 4000, 3000, 2000)
                : c.ArbitraryProfileDefWithVoids(a =>
                {
                    a.ProfileType = IfcProfileTypeEnum.AREA;
                    a.OuterCurve = Linie((0, 0), (6000, 0), (6000, 4000), (0, 4000), (0, 0));
                    a.InnerCurves.Add(Linie((2700, 1800), (2700, 2200), (3300, 2200), (3300, 1800), (2700, 1800)));
                });
            var decke = c.Slab(s =>
            {
                s.Name = "D-X"; s.GlobalId = Gid("D-X"); s.PredefinedType = IfcSlabTypeEnum.FLOOR; s.ObjectPlacement = L(lOg, 0, 0, -250);
                s.Representation = Form(Rep("Body", "SweptSolid", Extrusion(profil, 250)));
            });
            MatZu("Beton", decke);

            if (wand is not null) c.RelContainedInSpatialStructure(r => { r.RelatingStructure = eg; r.RelatedElements.Add(wand); });
            c.RelContainedInSpatialStructure(r => { r.RelatingStructure = og; r.RelatedElements.Add(decke); });
            tr.Commit();
        }
        m.SaveAs(pfad, StorageType.Ifc);
    }
}
