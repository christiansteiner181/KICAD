using Xbim.Common;
using Xbim.Common.Step21;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.IO;

namespace Hlks.Kern.Tests;

/// <summary>Synthetische Datei der Nachbearbeitung N17 (IFC4 und IFC2x3, mit xBIM erzeugt; Projekt in mm). Geschosse: EG Kote 0, OG Kote 2800.
/// Fünf Treppen mit 16 Steigungen von 175 mm und Auftritt 280 mm, 1000 mm breit, von EG nach OG (Stufenblöcke 350 mm hoch, Lauf entlang +x),
/// im Abstand von 3 m in y:
///   T-Lauf   (y 0)      IfcStair → IfcStairFlight (RelAggregates) mit den Attributen NumberOfRisers 16, NumberOfTreads 15, RiserHeight 175, TreadLength 280.
///   T-Pset   (y 3000)   IfcStair mit Körper und Pset_StairCommon (NumberOfRiser 16, NumberOfTreads 15, RiserHeight 175, TreadLength 280), keine Läufe.
///   T-Geo    (y 6000)   IfcStair mit Körper, ohne jedes Attribut: Stufen, Steigung, Auftritt nur aus der Geometrie.
///   T-Podest (y 9000)   IfcStair mit Körper, ohne Attribute, mit Zwischenpodest 1000 × 1000 in 1400 mm Höhe (7 Stufen, Podest, 7 Stufen).
///   T-Zwei   (y 12000)  IfcStair mit zwei IfcStairFlight (je NumberOfRisers 8, NumberOfTreads 7, RiserHeight 175, TreadLength 280) und einer IfcSlab
///                       LANDING (1000 × 1000, Oberkante 1400) als Teile.</summary>
public static class TreppeTestdatei
{
    static readonly object sperre = new();
    static readonly Dictionary<string, string> dateien = [];

    public const double Steigung = 175, Auftritt = 280, Breite = 1000;

    public static string Datei(string schema)
    {
        var name = $"n17_{schema.ToLowerInvariant()}.ifc";
        lock (sperre)
        {
            if (dateien.TryGetValue(name, out var da)) return da;
            var ordner = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "testdateien_n17_" + Environment.ProcessId);
            Directory.CreateDirectory(ordner);
            var pfad = Path.Combine(ordner, name);
            Erzeugen(pfad, schema == "IFC4");
            return dateien[name] = pfad;
        }
    }

    static XbimEditorCredentials Wer => new()
    {
        ApplicationDevelopersName = "H+K Planungs AG", ApplicationFullName = "HLKS-CAD Test N17", ApplicationIdentifier = "hlks-n17",
        ApplicationVersion = "1.0", EditorsFamilyName = "Test", EditorsGivenName = "HLKS", EditorsOrganisationName = "H+K Planungs AG",
    };

    static void Erzeugen(string pfad, bool ifc4)
    {
        using var m = IfcStore.Create(Wer, ifc4 ? XbimSchemaVersion.Ifc4 : XbimSchemaVersion.Ifc2X3, XbimStoreType.InMemoryModel);
        using (var tr = m.BeginTransaction("N17"))
        {
            var i = m.Instances;
            var c = new EntityCreator(m);
            IIfcProject projekt;
            if (ifc4) { var p4 = i.New<Xbim.Ifc4.Kernel.IfcProject>(p => p.Name = "N17 Test"); p4.Initialize(ProjectUnits.SIUnitsUK); projekt = p4; }
            else { var p2 = i.New<Xbim.Ifc2x3.Kernel.IfcProject>(p => p.Name = "N17 Test"); p2.Initialize(ProjectUnits.SIUnitsUK); projekt = p2; }
            var ctx = i.OfType<IIfcGeometricRepresentationContext>().First();

            IIfcCartesianPoint P(double x, double y, double z) => c.CartesianPoint(p => { p.Coordinates.Add(x); p.Coordinates.Add(y); p.Coordinates.Add(z); });
            IIfcAxis2Placement3D A3(double x, double y, double z) => c.Axis2Placement3D(a => a.Location = P(x, y, z));
            IIfcLocalPlacement L(IIfcLocalPlacement? rel, double x, double y, double z) => c.LocalPlacement(l => { l.PlacementRelTo = rel; l.RelativePlacement = A3(x, y, z); });
            IIfcShapeRepresentation Rep(params IIfcRepresentationItem[] items) => c.ShapeRepresentation(r =>
            {
                r.ContextOfItems = ctx; r.RepresentationIdentifier = "Body"; r.RepresentationType = "Brep";
                foreach (var it in items) r.Items.Add(it);
            });
            IIfcProductDefinitionShape Form(params IIfcRepresentationItem[] items) => c.ProductDefinitionShape(s => s.Representations.Add(Rep(items)));
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
            void Pset(IIfcObject o, string name, params (string Name, IIfcValue Wert)[] l) => c.RelDefinesByProperties(r =>
            {
                r.RelatingPropertyDefinition = c.PropertySet(ps => { ps.Name = name; foreach (var (n, w) in l) ps.HasProperties.Add(c.PropertySingleValue(v => { v.Name = n; v.NominalValue = w; })); });
                r.RelatedObjects.Add(o);
            });
            IIfcValue Zaehl(double x) => new Xbim.Ifc4.MeasureResource.IfcCountMeasure(x);
            IIfcValue Laenge(double x) => new Xbim.Ifc4.MeasureResource.IfcPositiveLengthMeasure(x);

            var lSite = L(null, 0, 0, 0);
            var site = c.Site(s => { s.Name = "Gelaende"; s.ObjectPlacement = lSite; });
            var lGeb = L(lSite, 0, 0, 0);
            var geb = c.Building(b => { b.Name = "Testhaus N17"; b.ObjectPlacement = lGeb; });
            var lEg = L(lGeb, 0, 0, 0);
            var eg = c.BuildingStorey(s => { s.Name = "Erdgeschoss"; s.Elevation = 0; s.ObjectPlacement = lEg; });
            var lOg = L(lGeb, 0, 0, 2800);
            var og = c.BuildingStorey(s => { s.Name = "Obergeschoss"; s.Elevation = 2800; s.ObjectPlacement = lOg; });
            c.RelAggregates(r => { r.RelatingObject = projekt; r.RelatedObjects.Add(site); });
            c.RelAggregates(r => { r.RelatingObject = site; r.RelatedObjects.Add(geb); });
            c.RelAggregates(r => { r.RelatingObject = geb; r.RelatedObjects.Add(eg); r.RelatedObjects.Add(og); });
            var imEg = new List<IIfcProduct>();

            // Stufenblöcke einer Strecke: n Stufen ab x0, erste Trittfläche in z = z1; Block 350 mm hoch
            List<IIfcRepresentationItem> Stufen(double x0, double z1, int n)
            {
                var l = new List<IIfcRepresentationItem>();
                for (int k = 0; k < n; k++) l.Add(Quader(x0 + k * Auftritt, 0, z1 + k * Steigung - 350, x0 + (k + 1) * Auftritt, Breite, z1 + k * Steigung));
                return l;
            }
            IIfcStair Treppe(string name, double y)
            {
                var s = c.Stair(t => { t.Name = name; t.ObjectPlacement = L(lEg, 0, y, 0); });
                imEg.Add(s);
                return s;
            }

            // T-Lauf: IfcStair → IfcStairFlight mit Attributen
            var lauf = Treppe("T-Lauf", 0);
            var fl = c.StairFlight(f =>
            {
                f.Name = "T-Lauf Lauf"; f.ObjectPlacement = L(lEg, 0, 0, 0);
                f.NumberOfRisers = 16; f.NumberOfTreads = 15; f.RiserHeight = Steigung; f.TreadLength = Auftritt;
                f.Representation = Form([.. Stufen(0, Steigung, 15)]);
            });
            c.RelAggregates(r => { r.RelatingObject = lauf; r.RelatedObjects.Add(fl); });

            // T-Pset: IfcStair mit Körper und Pset_StairCommon
            var pset = Treppe("T-Pset", 3000);
            pset.Representation = Form([.. Stufen(0, Steigung, 15)]);
            Pset(pset, "Pset_StairCommon", ("NumberOfRiser", Zaehl(16)), ("NumberOfTreads", Zaehl(15)), ("RiserHeight", Laenge(Steigung)), ("TreadLength", Laenge(Auftritt)));

            // T-Geo: nur Geometrie
            var geo = Treppe("T-Geo", 6000);
            geo.Representation = Form([.. Stufen(0, Steigung, 15)]);

            // T-Podest: 7 Stufen, Podest 1000 × 1000 (Oberkante 1400), 7 Stufen; ohne Attribute
            var pod = Treppe("T-Podest", 9000);
            {
                var teile = Stufen(0, Steigung, 7);
                teile.Add(Quader(7 * Auftritt, 0, 1400 - 350, 7 * Auftritt + 1000, Breite, 1400));
                teile.AddRange(Stufen(7 * Auftritt + 1000, 1575, 7));
                pod.Representation = Form([.. teile]);
            }

            // T-Zwei: zwei Läufe und ein Podest (Platte LANDING) als Teile einer IfcStair
            var zwei = Treppe("T-Zwei", 12000);
            var l1 = c.StairFlight(f =>
            {
                f.Name = "T-Zwei Lauf 1"; f.ObjectPlacement = L(lEg, 0, 12000, 0);
                f.NumberOfRisers = 8; f.NumberOfTreads = 7; f.RiserHeight = Steigung; f.TreadLength = Auftritt;
                f.Representation = Form([.. Stufen(0, Steigung, 7)]);
            });
            var landung = c.Slab(s =>
            {
                s.Name = "T-Zwei Podest"; s.ObjectPlacement = L(lEg, 0, 12000, 0); s.PredefinedType = IfcSlabTypeEnum.LANDING;
                s.Representation = Form(Quader(7 * Auftritt, 0, 1400 - 200, 7 * Auftritt + 1000, Breite, 1400));
            });
            var l2 = c.StairFlight(f =>
            {
                f.Name = "T-Zwei Lauf 2"; f.ObjectPlacement = L(lEg, 0, 12000, 0);
                f.NumberOfRisers = 8; f.NumberOfTreads = 7; f.RiserHeight = Steigung; f.TreadLength = Auftritt;
                f.Representation = Form([.. Stufen(7 * Auftritt + 1000, 1575, 7)]);
            });
            c.RelAggregates(r => { r.RelatingObject = zwei; r.RelatedObjects.Add(l1); r.RelatedObjects.Add(landung); r.RelatedObjects.Add(l2); });

            c.RelContainedInSpatialStructure(r => { r.RelatingStructure = eg; foreach (var x in imEg) r.RelatedElements.Add(x); });
            tr.Commit();
        }
        m.SaveAs(pfad, StorageType.Ifc);
    }
}
