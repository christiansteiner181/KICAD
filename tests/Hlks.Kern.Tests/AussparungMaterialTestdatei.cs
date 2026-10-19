using Xbim.Common;
using Xbim.Common.Step21;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.IO;

namespace Hlks.Kern.Tests;

/// <summary>Synthetische Datei (IFC4 und IFC2x3) für die Netzdatei Fassung 3 (Nachtrag B3c: Material und Typname), mit xBIM erzeugt
/// (EntityCreator wie IfcNetzFassung2Testdatei). Projekt in mm, EG Kote 0. Alle Namen FIKTIV, je Wand 1000 × 200 × 2600 nebeneinander
/// entlang x (Wand i bei x = 2000·i), GlobalIds fest (<see cref="Gid"/>):
///   W-MAT   IfcMaterial «Stahlbeton» (IFC4: Category «Concrete»)
///   W-LIST  IfcMaterialList «Kalksandstein», «Kalkputz»
///   W-LSU   IfcMaterialLayerSetUsage: «Gipskarton» 12.5, «Mineralwolle» 50
///   W-TYP   ohne eigenes Material; Typobjekt IfcWallType «Typ Backstein» mit IfcMaterialLayerSet «Backstein» 150
///   W-OBJ   ObjectType «Beton, Stahlbeton 250», IfcMaterial «&lt;Unnamed&gt;» (wie der Revit-Export der Bernstrasse)
///   W-KONST nur IFC4: IfcMaterialConstituentSet «Brettschichtholz», «Holzfaserdämmung»
///   W-OHNE  ohne Materialzuordnung
///   B-BEKL  IfcCovering CLADDING mit IfcMaterial «Gipsputz»</summary>
public static class AussparungMaterialTestdatei
{
    static readonly object sperre = new();
    static readonly Dictionary<string, string> dateien = [];

    public static string Gid(string name) => ("3" + new string(name.Where(char.IsAsciiLetterOrDigit).ToArray())).PadRight(22, '0')[..22];

    public static string Datei(string schema)
    {
        var name = $"b3c_material_{schema.ToLowerInvariant()}.ifc";
        lock (sperre)
        {
            if (dateien.TryGetValue(name, out var da)) return da;
            var ordner = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "testdateien_b3c_" + Environment.ProcessId);
            Directory.CreateDirectory(ordner);
            var pfad = Path.Combine(ordner, name);
            Erzeugen(pfad, schema == "IFC4");
            return dateien[name] = pfad;
        }
    }

    static XbimEditorCredentials Wer => new()
    {
        ApplicationDevelopersName = "H+K Planungs AG", ApplicationFullName = "HLKS-CAD Material-Test", ApplicationIdentifier = "hlks-b3c",
        ApplicationVersion = "1.0", EditorsFamilyName = "Test", EditorsGivenName = "HLKS", EditorsOrganisationName = "H+K Planungs AG",
    };

    static void Erzeugen(string pfad, bool ifc4)
    {
        using var m = IfcStore.Create(Wer, ifc4 ? XbimSchemaVersion.Ifc4 : XbimSchemaVersion.Ifc2X3, XbimStoreType.InMemoryModel);
        using (var tr = m.BeginTransaction("B3c"))
        {
            var i = m.Instances;
            var c = new EntityCreator(m);
            IIfcProject projekt;
            if (ifc4) { var p4 = i.New<Xbim.Ifc4.Kernel.IfcProject>(p => p.Name = "B3c Test"); p4.Initialize(ProjectUnits.SIUnitsUK); projekt = p4; }
            else { var p2 = i.New<Xbim.Ifc2x3.Kernel.IfcProject>(p => p.Name = "B3c Test"); p2.Initialize(ProjectUnits.SIUnitsUK); projekt = p2; }
            var ctx = i.OfType<IIfcGeometricRepresentationContext>().First();

            IIfcCartesianPoint P(double x, double y, double z) => c.CartesianPoint(p => { p.Coordinates.Add(x); p.Coordinates.Add(y); p.Coordinates.Add(z); });
            IIfcCartesianPoint P2(double x, double y) => c.CartesianPoint(p => { p.Coordinates.Add(x); p.Coordinates.Add(y); });
            IIfcDirection D(double x, double y, double z) => c.Direction(d => { d.DirectionRatios.Add(x); d.DirectionRatios.Add(y); d.DirectionRatios.Add(z); });
            IIfcAxis2Placement3D A3(double x, double y, double z) => c.Axis2Placement3D(a => a.Location = P(x, y, z));
            IIfcLocalPlacement L(IIfcObjectPlacement? rel, double x, double y, double z) =>
                c.LocalPlacement(l => { l.PlacementRelTo = rel; l.RelativePlacement = A3(x, y, z); });
            IIfcProductDefinitionShape Koerper(double dx, double dy, double dz) => c.ProductDefinitionShape(s => s.Representations.Add(c.ShapeRepresentation(r =>
            {
                r.ContextOfItems = ctx; r.RepresentationIdentifier = "Body"; r.RepresentationType = "SweptSolid";
                r.Items.Add(c.ExtrudedAreaSolid(x =>
                {
                    x.SweptArea = c.RectangleProfileDef(rp => { rp.ProfileType = IfcProfileTypeEnum.AREA; rp.XDim = dx; rp.YDim = dy; rp.Position = c.Axis2Placement2D(a => a.Location = P2(dx / 2, dy / 2)); });
                    x.Depth = dz; x.ExtrudedDirection = D(0, 0, 1); x.Position = A3(0, 0, 0);
                }));
            })));
            IIfcMaterial Mat(string name, string? kategorie = null)
            {
                var mat = c.Material(x => x.Name = name);
                if (ifc4 && kategorie is not null) ((Xbim.Ifc4.MaterialResource.IfcMaterial)mat).Category = kategorie;
                return mat;
            }
            void MatZu(IIfcMaterialSelect mat, IIfcObjectDefinition o) => c.RelAssociatesMaterial(r => { r.RelatingMaterial = mat; r.RelatedObjects.Add(o); });
            IIfcMaterialLayerSet Schichten(params (string Name, double Dicke)[] s) => c.MaterialLayerSet(x =>
            {
                x.LayerSetName = "Aufbau";
                foreach (var (n, d) in s) x.MaterialLayers.Add(c.MaterialLayer(l => { l.Material = Mat(n); l.LayerThickness = d; }));
            });

            var lSite = L(null, 0, 0, 0);
            var site = c.Site(s => { s.Name = "Gelaende"; s.ObjectPlacement = lSite; });
            var lGeb = L(lSite, 0, 0, 0);
            var geb = c.Building(b => { b.Name = "Testhaus B3c"; b.ObjectPlacement = lGeb; });
            var lEg = L(lGeb, 0, 0, 0);
            var eg = c.BuildingStorey(s => { s.Name = "Erdgeschoss"; s.Elevation = 0; s.ObjectPlacement = lEg; });
            c.RelAggregates(r => { r.RelatingObject = projekt; r.RelatedObjects.Add(site); });
            c.RelAggregates(r => { r.RelatingObject = site; r.RelatedObjects.Add(geb); });
            c.RelAggregates(r => { r.RelatingObject = geb; r.RelatedObjects.Add(eg); });
            var imEg = new List<IIfcProduct>();
            int nr = 0;
            IIfcWall Wand(string name, string? objectType = null)
            {
                double x0 = 2000 * nr++;
                var w = c.Wall(x =>
                {
                    x.Name = name; x.GlobalId = Gid(name); x.ObjectPlacement = L(lEg, x0, 0, 0); x.Representation = Koerper(1000, 200, 2600);
                    if (objectType is not null) x.ObjectType = objectType;
                });
                imEg.Add(w);
                return w;
            }

            MatZu(Mat("Stahlbeton", "Concrete"), Wand("W-MAT"));
            MatZu(c.MaterialList(l => { l.Materials.Add(Mat("Kalksandstein")); l.Materials.Add(Mat("Kalkputz")); }), Wand("W-LIST"));
            MatZu(c.MaterialLayerSetUsage(u =>
            {
                u.ForLayerSet = Schichten(("Gipskarton", 12.5), ("Mineralwolle", 50));
                u.LayerSetDirection = IfcLayerSetDirectionEnum.AXIS2; u.DirectionSense = IfcDirectionSenseEnum.POSITIVE; u.OffsetFromReferenceLine = 0;
            }), Wand("W-LSU"));
            var typ = c.WallType(t => { t.Name = "Typ Backstein"; t.PredefinedType = IfcWallTypeEnum.STANDARD; });
            MatZu(Schichten(("Backstein", 150)), typ);
            var wTyp = Wand("W-TYP");
            c.RelDefinesByType(r => { r.RelatingType = typ; r.RelatedObjects.Add(wTyp); });
            MatZu(Mat("<Unnamed>"), Wand("W-OBJ", "Beton, Stahlbeton 250"));
            if (ifc4)
            {
                var ks = i.New<Xbim.Ifc4.MaterialResource.IfcMaterialConstituentSet>(s =>
                {
                    s.Name = "Holzwand";
                    s.MaterialConstituents.Add(i.New<Xbim.Ifc4.MaterialResource.IfcMaterialConstituent>(k => { k.Name = "Tragwerk"; k.Material = (Xbim.Ifc4.MaterialResource.IfcMaterial)Mat("Brettschichtholz"); }));
                    s.MaterialConstituents.Add(i.New<Xbim.Ifc4.MaterialResource.IfcMaterialConstituent>(k => { k.Name = "Gefach"; k.Material = (Xbim.Ifc4.MaterialResource.IfcMaterial)Mat("Holzfaserdämmung"); }));
                });
                MatZu(ks, Wand("W-KONST"));
            }
            Wand("W-OHNE");
            var bek = c.Covering(x =>
            {
                x.Name = "B-BEKL"; x.GlobalId = Gid("B-BEKL"); x.PredefinedType = IfcCoveringTypeEnum.CLADDING;
                x.ObjectPlacement = L(lEg, 0, -20, 0); x.Representation = Koerper(1000, 20, 2600);
            });
            MatZu(Mat("Gipsputz"), bek);
            imEg.Add(bek);

            c.RelContainedInSpatialStructure(r => { r.RelatingStructure = eg; foreach (var x in imEg) r.RelatedElements.Add(x); });
            tr.Commit();
        }
        m.SaveAs(pfad, StorageType.Ifc);
    }
}
