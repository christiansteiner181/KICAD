using Xbim.Common;
using Xbim.Common.Step21;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.IO;

namespace Hlks.Kern.Tests;

/// <summary>Synthetische Datei (IFC4 und IFC2x3) für die Netzdatei Fassung 2 (Modul 3b, B1b), mit xBIM erzeugt (EntityCreator wie
/// IfcArchitekturTestdatei). Projekt in mm, EG Kote 0, 1.OG Kote 3000; alle GlobalIds fest (<see cref="Gid"/>):
///   W-A  Wand mit Körper 6000 × 200 × 2600, Achse (0,0)→(6000,0); zwei Öffnungen:
///          O-Leer    x 1000…1600, y −100…300, z 1000…1600, ohne Füllung (Durchbruch);
///          O-Fenster x 3000…4200, z 900…2100, gefüllt mit Fenster F-1 (IfcRelFillsElement).
///   W-B  Wand mit eigenem Körper 6000 × 200 × 2600 bei y = 2000 und drei Teilen (IfcBuildingElementPart über IfcRelAggregates):
///          Beton y 0…150, Dämmung 150…200 (im Körper) und Putz 200…215 (ausserhalb des Körpers – die Lage-Heuristik aus B1 hielte
///          ihn für ein eigenes Bauteil); Öffnung O-Teil im Teil Beton (Wirt = Teil) x 500…900, z 500…900.
///   W-C  Wand ohne Körper (nur Achse) bei y = 4000 aus zwei Teilen Beton 150 / Dämmung 50; Öffnung O-C x 2000…2800, z 0…2100.
///   D-T  Decke (IfcSlab FLOOR) im 1.OG ganz ohne Repräsentation (wie die Archicad-Decken der Bernstrasse) aus zwei Teilen
///          0…6000 × 0…4500, z 2750…2950 und 2950…3000; Öffnung O-Decke x 1000…1500, y 1000…1500, z 2700…3050.
///   Wie im Revit-Export der verknüpften Architektur (Bernstrasse): O-Frei x 5000…5400, y −100…300, z 1000…1400 (in W-A) und
///   O-Frei-Leer x 3000…3400, y 900…1300, z 1000…1400 (im Leeren) ohne IfcRelVoidsElement; Teil «Putz frei» x 100…5900,
///   y 50…150, z 0…2500 ohne Zerlegung, im EG, ganz im Körper von W-A.
/// Teile und Öffnungen stehen in keinem Geschoss (wie bei Archicad); Wände, Fenster und Decke im EG bzw. 1.OG.</summary>
public static class IfcNetzFassung2Testdatei
{
    static readonly object sperre = new();
    static readonly Dictionary<string, string> dateien = [];

    /// <summary>Feste GlobalId aus dem Namen («W-A» → «3WA0000…», 22 Zeichen aus dem IFC-Zeichenvorrat).</summary>
    public static string Gid(string name) => ("3" + new string(name.Where(char.IsAsciiLetterOrDigit).ToArray())).PadRight(22, '0')[..22];

    /// <summary>Pfad der Testdatei (einmal je Fassung erzeugt, im Temp-Ordner).</summary>
    public static string Datei(string schema)
    {
        var name = $"b1b_netz2_{schema.ToLowerInvariant()}.ifc";
        lock (sperre)
        {
            if (dateien.TryGetValue(name, out var da)) return da;
            var ordner = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "testdateien_b1b_" + Environment.ProcessId);
            Directory.CreateDirectory(ordner);
            var pfad = Path.Combine(ordner, name);
            Erzeugen(pfad, schema == "IFC4");
            return dateien[name] = pfad;
        }
    }

    static XbimEditorCredentials Wer => new()
    {
        ApplicationDevelopersName = "H+K Planungs AG", ApplicationFullName = "HLKS-CAD Netzdatei-Test", ApplicationIdentifier = "hlks-b1b",
        ApplicationVersion = "1.0", EditorsFamilyName = "Test", EditorsGivenName = "HLKS", EditorsOrganisationName = "H+K Planungs AG",
    };

    static void Erzeugen(string pfad, bool ifc4)
    {
        using var m = IfcStore.Create(Wer, ifc4 ? XbimSchemaVersion.Ifc4 : XbimSchemaVersion.Ifc2X3, XbimStoreType.InMemoryModel);
        using (var tr = m.BeginTransaction("B1b"))
        {
            var i = m.Instances;
            var c = new EntityCreator(m);
            IIfcProject projekt;
            if (ifc4) { var p4 = i.New<Xbim.Ifc4.Kernel.IfcProject>(p => p.Name = "B1b Test"); p4.Initialize(ProjectUnits.SIUnitsUK); projekt = p4; }
            else { var p2 = i.New<Xbim.Ifc2x3.Kernel.IfcProject>(p => p.Name = "B1b Test"); p2.Initialize(ProjectUnits.SIUnitsUK); projekt = p2; }
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
            IIfcExtrudedAreaSolid Extrusion(IIfcProfileDef profil, double tiefe, double z0 = 0) => c.ExtrudedAreaSolid(x =>
            {
                x.SweptArea = profil; x.Depth = tiefe; x.ExtrudedDirection = D(0, 0, 1); x.Position = A3(0, 0, z0);
            });
            IIfcProductDefinitionShape Koerper(IIfcProfileDef profil, double tiefe, double z0 = 0) => Form(Rep("Body", "SweptSolid", Extrusion(profil, tiefe, z0)));
            void MatZu(string name, IIfcObjectDefinition o) => c.RelAssociatesMaterial(r => { r.RelatingMaterial = c.Material(x => x.Name = name); r.RelatedObjects.Add(o); });
            void Menge(IIfcObject o, double breite, double hoehe) => c.RelDefinesByProperties(r =>
            {
                r.RelatingPropertyDefinition = c.ElementQuantity(q =>
                {
                    q.Name = "Qto_WallBaseQuantities";
                    q.Quantities.Add(c.QuantityLength(x => { x.Name = "Width"; x.LengthValue = breite; }));
                    q.Quantities.Add(c.QuantityLength(x => { x.Name = "Height"; x.LengthValue = hoehe; }));
                });
                r.RelatedObjects.Add(o);
            });
            void Teile(IIfcElement ganz, params IIfcProduct[] teile) => c.RelAggregates(r => { r.RelatingObject = ganz; foreach (var t in teile) r.RelatedObjects.Add(t); });
            IIfcBuildingElementPart Teil(string name, IIfcObjectPlacement ganz, IIfcProductDefinitionShape form)
            {
                var t = c.BuildingElementPart(x => { x.Name = name; x.GlobalId = Gid(name); x.ObjectPlacement = L(ganz, 0, 0, 0); x.Representation = form; });
                MatZu(name.Split(' ')[0], t);
                return t;
            }
            IIfcOpeningElement Oeffnung(string name, IIfcElement wirt, double x, double y, double z, IIfcProfileDef profil, double tiefe)
            {
                var o = c.OpeningElement(op => { op.Name = name; op.GlobalId = Gid(name); op.ObjectPlacement = L(wirt.ObjectPlacement, x, y, z); op.Representation = Koerper(profil, tiefe); });
                c.RelVoidsElement(r => { r.RelatingBuildingElement = wirt; r.RelatedOpeningElement = o; });
                return o;
            }

            // Gliederung
            var lSite = L(null, 0, 0, 0);
            var site = c.Site(s => { s.Name = "Gelaende"; s.ObjectPlacement = lSite; });
            var lGeb = L(lSite, 0, 0, 0);
            var geb = c.Building(b => { b.Name = "Testhaus B1b"; b.ObjectPlacement = lGeb; });
            var lEg = L(lGeb, 0, 0, 0);
            var eg = c.BuildingStorey(s => { s.Name = "Erdgeschoss"; s.Elevation = 0; s.ObjectPlacement = lEg; });
            var lOg = L(lGeb, 0, 0, 3000);
            var og = c.BuildingStorey(s => { s.Name = "1. Obergeschoss"; s.Elevation = 3000; s.ObjectPlacement = lOg; });
            c.RelAggregates(r => { r.RelatingObject = projekt; r.RelatedObjects.Add(site); });
            c.RelAggregates(r => { r.RelatingObject = site; r.RelatedObjects.Add(geb); });
            c.RelAggregates(r => { r.RelatingObject = geb; r.RelatedObjects.Add(eg); r.RelatedObjects.Add(og); });
            var imEg = new List<IIfcProduct>();
            var imOg = new List<IIfcProduct>();

            IIfcWall Wand(string name, double y, bool mitKoerper)
            {
                var w = c.Wall(x =>
                {
                    x.Name = name; x.GlobalId = Gid(name); x.ObjectPlacement = L(lEg, 0, y, 0);
                    x.Representation = mitKoerper
                        ? Form(Rep("Axis", "Curve2D", Linie((0, 0), (6000, 0))), Rep("Body", "SweptSolid", Extrusion(Rechteck(6000, 200, 3000, 100), 2600)))
                        : Form(Rep("Axis", "Curve2D", Linie((0, 0), (6000, 0))));
                });
                MatZu("Beton", w);
                Menge(w, 200, 2600);
                imEg.Add(w);
                return w;
            }

            // W-A mit zwei Öffnungen: Durchbruch ohne Füllung, Fensteröffnung mit Fenster
            var wa = Wand("W-A", 0, true);
            Oeffnung("O-Leer", wa, 1000, 0, 1000, Rechteck(600, 400, 300, 100), 600);
            var of = Oeffnung("O-Fenster", wa, 3000, 0, 900, Rechteck(1200, 400, 600, 100), 1200);
            var fenster = c.Window(f =>
            {
                f.Name = "F-1"; f.GlobalId = Gid("F-1"); f.OverallWidth = 1200; f.OverallHeight = 1200; f.ObjectPlacement = L(lEg, 3000, 0, 900);
                f.Representation = Koerper(Rechteck(1200, 60, 600, 100), 1200);
            });
            c.RelFillsElement(r => { r.RelatingOpeningElement = of; r.RelatedBuildingElement = fenster; });
            imEg.Add(fenster);
            // wie der Revit-Export der verknüpften Architektur: Öffnungen ohne IfcRelVoidsElement (eine in W-A, eine im Leeren) und ein
            // Teil ohne Zerlegung, das ganz im Körper von W-A liegt
            c.OpeningElement(op => { op.Name = "O-Frei"; op.GlobalId = Gid("O-Frei"); op.ObjectPlacement = L(lEg, 5000, 0, 1000); op.Representation = Koerper(Rechteck(400, 400, 200, 100), 400); });
            c.OpeningElement(op => { op.Name = "O-Frei-Leer"; op.GlobalId = Gid("O-Frei-Leer"); op.ObjectPlacement = L(lEg, 3000, 900, 1000); op.Representation = Koerper(Rechteck(400, 400, 200, 200), 400); });
            var putz = c.BuildingElementPart(x => { x.Name = "Putz frei"; x.GlobalId = Gid("Putz frei"); x.ObjectPlacement = L(lEg, 0, 0, 0); x.Representation = Koerper(Rechteck(5800, 100, 3000, 100), 2500); });
            MatZu("Putz", putz);
            imEg.Add(putz);

            // W-B: eigener Körper und drei Teile (Putz ausserhalb des Körpers); Öffnung im Teil Beton
            var wb = Wand("W-B", 2000, true);
            var beton = Teil("Beton WB", wb.ObjectPlacement, Koerper(Rechteck(6000, 150, 3000, 75), 2600));
            Teile(wb, beton, Teil("Daemmung WB", wb.ObjectPlacement, Koerper(Rechteck(6000, 50, 3000, 175), 2600)),
                Teil("Putz WB", wb.ObjectPlacement, Koerper(Rechteck(6000, 15, 3000, 207.5), 2600)));
            Oeffnung("O-Teil", beton, 500, 0, 500, Rechteck(400, 400, 200, 100), 400);

            // W-C: ohne Körper, aus zwei Teilen; Öffnung in der Wand
            var wc = Wand("W-C", 4000, false);
            Teile(wc, Teil("Beton WC", wc.ObjectPlacement, Koerper(Rechteck(6000, 150, 3000, 75), 2600)),
                Teil("Daemmung WC", wc.ObjectPlacement, Koerper(Rechteck(6000, 50, 3000, 175), 2600)));
            Oeffnung("O-C", wc, 2000, 0, 0, Rechteck(800, 400, 400, 100), 2100);

            // D-T: Decke ohne Repräsentation aus zwei Teilen, mit Deckendurchbruch
            var decke = c.Slab(s => { s.Name = "D-T"; s.GlobalId = Gid("D-T"); s.PredefinedType = IfcSlabTypeEnum.FLOOR; s.ObjectPlacement = L(lOg, 0, 0, -250); });
            MatZu("Beton", decke);
            Teile(decke, Teil("Beton DT", decke.ObjectPlacement, Koerper(Rechteck(6000, 4500, 3000, 2250), 200)),
                Teil("Belag DT", decke.ObjectPlacement, Koerper(Rechteck(6000, 4500, 3000, 2250), 50, 200)));
            Oeffnung("O-Decke", decke, 1000, 1000, -50, Rechteck(500, 500, 250, 250), 350);
            imOg.Add(decke);

            c.RelContainedInSpatialStructure(r => { r.RelatingStructure = eg; foreach (var x in imEg) r.RelatedElements.Add(x); });
            c.RelContainedInSpatialStructure(r => { r.RelatingStructure = og; foreach (var x in imOg) r.RelatedElements.Add(x); });
            tr.Commit();
        }
        m.SaveAs(pfad, StorageType.Ifc);
    }
}
