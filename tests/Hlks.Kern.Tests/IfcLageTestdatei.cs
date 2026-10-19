using Xbim.Common;
using Xbim.Common.Step21;
using Xbim.Ifc;
using Xbim.IO;

namespace Hlks.Kern.Tests;

/// <summary>Platzierung in mm (Welt oder relativ zum Gelände bzw. Gebäude) mit Drehung um z in Grad (gegen den Uhrzeigersinn).</summary>
public sealed record Pl(double X, double Y, double Z, double Grad = 0);

/// <summary>Georeferenz: Landeskoordinaten (m) des Weltursprungs der Datei und Drehung der x-Achse gegen Ost.</summary>
public sealed record KartenSpec(double OstM, double NordM, double HoeheM, double Grad = 0, double Massstab = 1, string Crs = "EPSG:2056");

/// <summary>Was eine Testdatei für die Ausrichtungstests enthält. Alle Längen in mm (Projekteinheit der Datei).</summary>
public sealed record LageSpec
{
    /// <summary>IfcSite-Platzierung in Weltkoordinaten (Gelände auf Landeskoordinaten: grosse Werte).</summary>
    public Pl Gelaende { get; init; } = new(0, 0, 0);
    /// <summary>IfcBuilding relativ zum Gelände.</summary>
    public Pl Gebaeude { get; init; } = new(0, 0, 0);
    /// <summary>Wand relativ zum Erdgeschoss (Achse 5000 mm entlang lokalem x, 200 dick, 2600 hoch).</summary>
    public Pl Wand { get; init; } = new(0, 0, 0);
    /// <summary>IfcMapConversion + IfcProjectedCRS (IFC4) bzw. ePSet_MapConversion (IFC2x3).</summary>
    public KartenSpec? Karte { get; init; }
    /// <summary>IFC2x3: Property Set am IfcProject (false) oder am IfcSite (true).</summary>
    public bool KarteAmGelaende { get; init; }
    /// <summary>Koordinationskörper (IfcBuildingElementProxy), Platzierung relativ zum Gelände; null = keiner.</summary>
    public Pl? Koerper { get; init; }
    public string KoerperName { get; init; } = "Koordinationskörper";
    /// <summary>Breite/Länge des Geländes in Grad (IfcCompoundPlaneAngleMeasure); null = nicht gesetzt.</summary>
    public (int Grad, int Minuten, int Sekunden)? Breite { get; init; }
    public (int Grad, int Minuten, int Sekunden)? Laenge { get; init; }
    public string GebaeudeName { get; init; } = "Testhaus";
    /// <summary>Damit zwei Dateien mit gleicher Lage verschiedene Bytes (und damit andere Prüfsumme) haben.</summary>
    public string Marke { get; init; } = "";
}

/// <summary>Erzeugt kleine IFC4- und IFC2x3-Dateien mit xBIM für die Ausrichtungstests: Projekt in mm, Gelände, Gebäude mit
/// frei platzierbarem Ursprung, zwei Geschosse (EG Kote 0, 1.OG Kote 3000), eine Wand, optional Georeferenz und Koordinationskörper.</summary>
public static class IfcLageTestdatei
{
    static XbimEditorCredentials Wer => new()
    {
        ApplicationDevelopersName = "H+K Planungs AG", ApplicationFullName = "HLKS-CAD Lagetest", ApplicationIdentifier = "hlks-lagetest",
        ApplicationVersion = "1.0", EditorsFamilyName = "Test", EditorsGivenName = "HLKS", EditorsOrganisationName = "H+K Planungs AG",
    };

    static (double C, double S) CS(double grad) => (Math.Cos(grad * Math.PI / 180), Math.Sin(grad * Math.PI / 180));

    public static string Schreiben(string schema, string datei, LageSpec s)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(datei)!);
        if (schema == "IFC4") Ifc4(datei, s); else Ifc2x3(datei, s);
        return datei;
    }

    // ------------------------------------------------------------------ IFC4

    static void Ifc4(string datei, LageSpec s)
    {
        using var m = IfcStore.Create(Wer, XbimSchemaVersion.Ifc4, XbimStoreType.InMemoryModel);
        using (var t = m.BeginTransaction("Lagetest"))
        {
            var i = m.Instances;
            var projekt = i.New<Xbim.Ifc4.Kernel.IfcProject>(p => p.Name = "Testprojekt" + s.Marke);
            projekt.Initialize(ProjectUnits.SIUnitsUK);   // Länge in mm
            var ctx = i.OfType<Xbim.Ifc4.RepresentationResource.IfcGeometricRepresentationContext>().First();

            Xbim.Ifc4.GeometryResource.IfcCartesianPoint P(double x, double y, double z) => i.New<Xbim.Ifc4.GeometryResource.IfcCartesianPoint>(p => p.SetXYZ(x, y, z));
            Xbim.Ifc4.GeometryResource.IfcCartesianPoint P2(double x, double y) => i.New<Xbim.Ifc4.GeometryResource.IfcCartesianPoint>(p => p.SetXY(x, y));
            Xbim.Ifc4.GeometricConstraintResource.IfcLocalPlacement L(Xbim.Ifc4.GeometricConstraintResource.IfcLocalPlacement? rel, Pl pl)
            {
                var (c, sn) = CS(pl.Grad);
                return i.New<Xbim.Ifc4.GeometricConstraintResource.IfcLocalPlacement>(l =>
                {
                    l.PlacementRelTo = rel;
                    l.RelativePlacement = i.New<Xbim.Ifc4.GeometryResource.IfcAxis2Placement3D>(a =>
                    {
                        a.Location = P(pl.X, pl.Y, pl.Z);
                        a.Axis = i.New<Xbim.Ifc4.GeometryResource.IfcDirection>(d => d.SetXYZ(0, 0, 1));
                        a.RefDirection = i.New<Xbim.Ifc4.GeometryResource.IfcDirection>(d => d.SetXYZ(c, sn, 0));
                    });
                });
            }

            var lSite = L(null, s.Gelaende);
            var site = i.New<Xbim.Ifc4.ProductExtension.IfcSite>(x =>
            {
                x.Name = "Gelaende"; x.ObjectPlacement = lSite;
                if (s.Breite is { } b) x.RefLatitude = new Xbim.Ifc4.MeasureResource.IfcCompoundPlaneAngleMeasure(new List<long> { b.Grad, b.Minuten, b.Sekunden, 0 });
                if (s.Laenge is { } l) x.RefLongitude = new Xbim.Ifc4.MeasureResource.IfcCompoundPlaneAngleMeasure(new List<long> { l.Grad, l.Minuten, l.Sekunden, 0 });
            });
            var lGeb = L(lSite, s.Gebaeude);
            var geb = i.New<Xbim.Ifc4.ProductExtension.IfcBuilding>(b => { b.Name = s.GebaeudeName; b.ObjectPlacement = lGeb; });
            var lEg = L(lGeb, new(0, 0, 0));
            var eg = i.New<Xbim.Ifc4.ProductExtension.IfcBuildingStorey>(x => { x.Name = "Erdgeschoss"; x.Elevation = 0; x.ObjectPlacement = lEg; });
            var lOg = L(lGeb, new(0, 0, 3000));
            var og = i.New<Xbim.Ifc4.ProductExtension.IfcBuildingStorey>(x => { x.Name = "1. Obergeschoss"; x.Elevation = 3000; x.ObjectPlacement = lOg; });
            i.New<Xbim.Ifc4.Kernel.IfcRelAggregates>(r => { r.RelatingObject = projekt; r.RelatedObjects.Add(site); });
            i.New<Xbim.Ifc4.Kernel.IfcRelAggregates>(r => { r.RelatingObject = site; r.RelatedObjects.Add(geb); });
            i.New<Xbim.Ifc4.Kernel.IfcRelAggregates>(r => { r.RelatingObject = geb; r.RelatedObjects.Add(eg); r.RelatedObjects.Add(og); });

            // Wand 5 m lang, 200 mm dick, 2.6 m hoch, Achse (0,0) → (5000,0) im lokalen System des Erdgeschosses
            var achse = i.New<Xbim.Ifc4.GeometryResource.IfcPolyline>(pl => { pl.Points.Add(P2(0, 0)); pl.Points.Add(P2(5000, 0)); });
            var profil = i.New<Xbim.Ifc4.ProfileResource.IfcRectangleProfileDef>(r => { r.ProfileType = Xbim.Ifc4.Interfaces.IfcProfileTypeEnum.AREA; r.XDim = 5000; r.YDim = 200;
                r.Position = i.New<Xbim.Ifc4.GeometryResource.IfcAxis2Placement2D>(a => a.Location = P2(2500, 0)); });
            var koerper = i.New<Xbim.Ifc4.GeometricModelResource.IfcExtrudedAreaSolid>(x => { x.SweptArea = profil; x.Depth = 2600;
                x.ExtrudedDirection = i.New<Xbim.Ifc4.GeometryResource.IfcDirection>(d => d.SetXYZ(0, 0, 1));
                x.Position = i.New<Xbim.Ifc4.GeometryResource.IfcAxis2Placement3D>(a => a.Location = P(0, 0, 0)); });
            var wand = i.New<Xbim.Ifc4.SharedBldgElements.IfcWall>(w =>
            {
                w.Name = "Wand Sued"; w.PredefinedType = Xbim.Ifc4.Interfaces.IfcWallTypeEnum.STANDARD; w.ObjectPlacement = L(lEg, s.Wand);
                w.Representation = i.New<Xbim.Ifc4.RepresentationResource.IfcProductDefinitionShape>(ps =>
                {
                    ps.Representations.Add(i.New<Xbim.Ifc4.RepresentationResource.IfcShapeRepresentation>(r => { r.ContextOfItems = ctx; r.RepresentationIdentifier = "Axis"; r.RepresentationType = "Curve2D"; r.Items.Add(achse); }));
                    ps.Representations.Add(i.New<Xbim.Ifc4.RepresentationResource.IfcShapeRepresentation>(r => { r.ContextOfItems = ctx; r.RepresentationIdentifier = "Body"; r.RepresentationType = "SweptSolid"; r.Items.Add(koerper); }));
                });
            });
            var enthalten = new List<Xbim.Ifc4.Interfaces.IIfcProduct> { wand };
            if (s.Koerper is { } kk)
            {
                var proxy = i.New<Xbim.Ifc4.SharedBldgElements.IfcBuildingElementProxy>(x => { x.Name = s.KoerperName; x.ObjectPlacement = L(lSite, kk); });
                enthalten.Add(proxy);
            }
            i.New<Xbim.Ifc4.ProductExtension.IfcRelContainedInSpatialStructure>(r => { r.RelatingStructure = eg; foreach (var e in enthalten) r.RelatedElements.Add((Xbim.Ifc4.Kernel.IfcProduct)e); });

            if (s.Karte is { } k)
            {
                var (c, sn) = CS(k.Grad);
                var ziel = i.New<Xbim.Ifc4.RepresentationResource.IfcProjectedCRS>(x => { x.Name = k.Crs; });
                i.New<Xbim.Ifc4.RepresentationResource.IfcMapConversion>(x =>
                {
                    x.SourceCRS = ctx; x.TargetCRS = ziel;
                    x.Eastings = k.OstM * 1000; x.Northings = k.NordM * 1000; x.OrthogonalHeight = k.HoeheM * 1000;
                    x.XAxisAbscissa = c; x.XAxisOrdinate = sn; x.Scale = k.Massstab;
                });
            }
            t.Commit();
        }
        m.SaveAs(datei, StorageType.Ifc);
    }

    // ------------------------------------------------------------------ IFC2x3

    static void Ifc2x3(string datei, LageSpec s)
    {
        using var m = IfcStore.Create(Wer, XbimSchemaVersion.Ifc2X3, XbimStoreType.InMemoryModel);
        using (var t = m.BeginTransaction("Lagetest"))
        {
            var i = m.Instances;
            var projekt = i.New<Xbim.Ifc2x3.Kernel.IfcProject>(p => p.Name = "Testprojekt" + s.Marke);
            projekt.Initialize(ProjectUnits.SIUnitsUK);
            var ctx = i.OfType<Xbim.Ifc2x3.RepresentationResource.IfcGeometricRepresentationContext>().First();

            Xbim.Ifc2x3.GeometryResource.IfcCartesianPoint P(double x, double y, double z) => i.New<Xbim.Ifc2x3.GeometryResource.IfcCartesianPoint>(p => p.SetXYZ(x, y, z));
            Xbim.Ifc2x3.GeometryResource.IfcCartesianPoint P2(double x, double y) => i.New<Xbim.Ifc2x3.GeometryResource.IfcCartesianPoint>(p => p.SetXY(x, y));
            Xbim.Ifc2x3.GeometricConstraintResource.IfcLocalPlacement L(Xbim.Ifc2x3.GeometricConstraintResource.IfcLocalPlacement? rel, Pl pl)
            {
                var (c, sn) = CS(pl.Grad);
                return i.New<Xbim.Ifc2x3.GeometricConstraintResource.IfcLocalPlacement>(l =>
                {
                    l.PlacementRelTo = rel;
                    l.RelativePlacement = i.New<Xbim.Ifc2x3.GeometryResource.IfcAxis2Placement3D>(a =>
                    {
                        a.Location = P(pl.X, pl.Y, pl.Z);
                        a.Axis = i.New<Xbim.Ifc2x3.GeometryResource.IfcDirection>(d => d.SetXYZ(0, 0, 1));
                        a.RefDirection = i.New<Xbim.Ifc2x3.GeometryResource.IfcDirection>(d => d.SetXYZ(c, sn, 0));
                    });
                });
            }
            var element = Xbim.Ifc2x3.ProductExtension.IfcElementCompositionEnum.ELEMENT;

            var lSite = L(null, s.Gelaende);
            var site = i.New<Xbim.Ifc2x3.ProductExtension.IfcSite>(x =>
            {
                x.Name = "Gelaende"; x.ObjectPlacement = lSite; x.CompositionType = element;
                if (s.Breite is { } b) x.RefLatitude = new Xbim.Ifc2x3.MeasureResource.IfcCompoundPlaneAngleMeasure(new List<long> { b.Grad, b.Minuten, b.Sekunden, 0 });
                if (s.Laenge is { } l) x.RefLongitude = new Xbim.Ifc2x3.MeasureResource.IfcCompoundPlaneAngleMeasure(new List<long> { l.Grad, l.Minuten, l.Sekunden, 0 });
            });
            var lGeb = L(lSite, s.Gebaeude);
            var geb = i.New<Xbim.Ifc2x3.ProductExtension.IfcBuilding>(b => { b.Name = s.GebaeudeName; b.ObjectPlacement = lGeb; b.CompositionType = element; });
            var lEg = L(lGeb, new(0, 0, 0));
            var eg = i.New<Xbim.Ifc2x3.ProductExtension.IfcBuildingStorey>(x => { x.Name = "Erdgeschoss"; x.Elevation = 0; x.ObjectPlacement = lEg; x.CompositionType = element; });
            var lOg = L(lGeb, new(0, 0, 3000));
            var og = i.New<Xbim.Ifc2x3.ProductExtension.IfcBuildingStorey>(x => { x.Name = "1. Obergeschoss"; x.Elevation = 3000; x.ObjectPlacement = lOg; x.CompositionType = element; });
            i.New<Xbim.Ifc2x3.Kernel.IfcRelAggregates>(r => { r.RelatingObject = projekt; r.RelatedObjects.Add(site); });
            i.New<Xbim.Ifc2x3.Kernel.IfcRelAggregates>(r => { r.RelatingObject = site; r.RelatedObjects.Add(geb); });
            i.New<Xbim.Ifc2x3.Kernel.IfcRelAggregates>(r => { r.RelatingObject = geb; r.RelatedObjects.Add(eg); r.RelatedObjects.Add(og); });

            var achse = i.New<Xbim.Ifc2x3.GeometryResource.IfcPolyline>(pl => { pl.Points.Add(P2(0, 0)); pl.Points.Add(P2(5000, 0)); });
            var profil = i.New<Xbim.Ifc2x3.ProfileResource.IfcRectangleProfileDef>(r => { r.ProfileType = Xbim.Ifc2x3.ProfileResource.IfcProfileTypeEnum.AREA; r.XDim = 5000; r.YDim = 200;
                r.Position = i.New<Xbim.Ifc2x3.GeometryResource.IfcAxis2Placement2D>(a => a.Location = P2(2500, 0)); });
            var koerper = i.New<Xbim.Ifc2x3.GeometricModelResource.IfcExtrudedAreaSolid>(x => { x.SweptArea = profil; x.Depth = 2600;
                x.ExtrudedDirection = i.New<Xbim.Ifc2x3.GeometryResource.IfcDirection>(d => d.SetXYZ(0, 0, 1));
                x.Position = i.New<Xbim.Ifc2x3.GeometryResource.IfcAxis2Placement3D>(a => a.Location = P(0, 0, 0)); });
            var wand = i.New<Xbim.Ifc2x3.SharedBldgElements.IfcWallStandardCase>(w =>
            {
                w.Name = "Wand Sued"; w.ObjectPlacement = L(lEg, s.Wand);
                w.Representation = i.New<Xbim.Ifc2x3.RepresentationResource.IfcProductDefinitionShape>(ps =>
                {
                    ps.Representations.Add(i.New<Xbim.Ifc2x3.RepresentationResource.IfcShapeRepresentation>(r => { r.ContextOfItems = ctx; r.RepresentationIdentifier = "Axis"; r.RepresentationType = "Curve2D"; r.Items.Add(achse); }));
                    ps.Representations.Add(i.New<Xbim.Ifc2x3.RepresentationResource.IfcShapeRepresentation>(r => { r.ContextOfItems = ctx; r.RepresentationIdentifier = "Body"; r.RepresentationType = "SweptSolid"; r.Items.Add(koerper); }));
                });
            });
            var enthalten = new List<Xbim.Ifc2x3.Kernel.IfcProduct> { wand };
            if (s.Koerper is { } kk)
                enthalten.Add(i.New<Xbim.Ifc2x3.ProductExtension.IfcBuildingElementProxy>(x => { x.Name = s.KoerperName; x.ObjectPlacement = L(lSite, kk); }));
            i.New<Xbim.Ifc2x3.ProductExtension.IfcRelContainedInSpatialStructure>(r => { r.RelatingStructure = eg; foreach (var e in enthalten) r.RelatedElements.Add(e); });

            if (s.Karte is { } k)
            {
                // Revit-Ersatz für IfcMapConversion in IFC2x3: Property Sets ePSet_MapConversion und ePSet_ProjectedCRS
                var (c, sn) = CS(k.Grad);
                Xbim.Ifc2x3.PropertyResource.IfcPropertySingleValue Zahl(string n, double w) =>
                    i.New<Xbim.Ifc2x3.PropertyResource.IfcPropertySingleValue>(v => { v.Name = n; v.NominalValue = new Xbim.Ifc2x3.MeasureResource.IfcReal(w); });
                var conv = i.New<Xbim.Ifc2x3.Kernel.IfcPropertySet>(ps =>
                {
                    ps.Name = "ePSet_MapConversion";
                    ps.HasProperties.Add(Zahl("Eastings", k.OstM * 1000)); ps.HasProperties.Add(Zahl("Northings", k.NordM * 1000));
                    ps.HasProperties.Add(Zahl("OrthogonalHeight", k.HoeheM * 1000));
                    ps.HasProperties.Add(Zahl("XAxisAbscissa", c)); ps.HasProperties.Add(Zahl("XAxisOrdinate", sn)); ps.HasProperties.Add(Zahl("Scale", k.Massstab));
                });
                var crs = i.New<Xbim.Ifc2x3.Kernel.IfcPropertySet>(ps =>
                {
                    ps.Name = "ePSet_ProjectedCRS";
                    ps.HasProperties.Add(i.New<Xbim.Ifc2x3.PropertyResource.IfcPropertySingleValue>(v => { v.Name = "Name"; v.NominalValue = new Xbim.Ifc2x3.MeasureResource.IfcLabel(k.Crs); }));
                });
                foreach (var def in new[] { conv, crs })
                    i.New<Xbim.Ifc2x3.Kernel.IfcRelDefinesByProperties>(r =>
                    {
                        if (s.KarteAmGelaende) r.RelatedObjects.Add(site); else r.RelatedObjects.Add(projekt);
                        r.RelatingPropertyDefinition = def;
                    });
            }
            t.Commit();
        }
        m.SaveAs(datei, StorageType.Ifc);
    }
}
