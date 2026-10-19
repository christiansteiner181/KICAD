using Xbim.Common;
using Xbim.Common.Step21;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.IO;

namespace Hlks.Kern.Tests;

/// <summary>Abweichungen der Hüllen-Testdatei vom Grundfall.</summary>
/// <param name="Konflikt">Die Nordwand nennt einen anderen U-Wert als die Südwand (gleicher Aufbau).</param>
/// <param name="Gedreht">Das Gebäude ist in der Datei um 20° gedreht (Ausrichtung der Datei 20°).</param>
/// <param name="Einheit">Die spezifische Wärmekapazität steht in kJ/(kg·K) (abgeleitete Einheit der Datei).</param>
/// <param name="OhneNord">Die Datei nennt keine TrueNorth.</param>
/// <param name="OhneSchichten">Wände und Bodenplatte haben nur ein IfcMaterial, keinen Schichtaufbau (U steht trotzdem im Pset).</param>
public sealed record HuelleVariante(bool Konflikt = false, bool Gedreht = false, bool Einheit = false, bool OhneNord = false, bool OhneSchichten = false)
{
    public string Kuerzel => (Konflikt ? "k" : "") + (Gedreht ? "g" : "") + (Einheit ? "e" : "") + (OhneNord ? "o" : "") + (OhneSchichten ? "s" : "");
}

/// <summary>Synthetische Hüllen-Datei (IFC4 und IFC2x3) für die Tests von B4 (Modul 1c), mit xBIM erzeugt. Projekt in mm, EG Kote 0.
/// Alle Kennwerte sind TESTWERTE (frei gewählt, keine Baustoff- oder Normwerte).
/// Wände 200 dick, 2600 hoch, Achse an der Aussenfläche, lokal +y der Wand = Innenseite des Hauses (6000 × 4000 aussen):
///   W-Sued  (0,0)→(6000,0)      Schichten der Datei [Wärmedämmung 50, Beton 150], Usage AXIS2 POSITIVE → Wärmedämmung aussen;
///                               Pset_WallCommon: ThermalTransmittance 0.25, IsExternal; Tür 900 × 2100 (Pset_DoorCommon U 1.8)
///   W-Ost   (6000,0)→(6000,4000) dieselben Schichten, Usage AXIS2 NEGATIVE ab Offset 200 → Wärmedämmung innen; U 0.35
///   W-Nord  (6000,4000)→(0,4000) dieselbe Schichtfolge wie W-Sued, eigene Instanzen (auch ein zweites IfcMaterial «Beton»),
///                               U 0.25 (Variante Konflikt 0.28); Fenster 1200 × 1200 (U 1.3, GlazingAreaFraction 0.7)
///   W-West  (0,4000)→(0,0)      nur ein IfcMaterial «Gipsplatte», kein Schichtaufbau
///   Bodenplatte (IfcSlab BASESLAB, Oberkante 0, 360 dick): Schichten [Estrich 60, Wärmedämmung 100, Beton 200], Usage AXIS3
///                               NEGATIVE ab Offset 360 (erste Schicht oben); Pset_SlabCommon U 0.45
/// Materialkennwerte: Beton λ 0.123, c 456, ρ 789; Wärmedämmung λ 0.321; Estrich und Gipsplatte ohne Kennwerte.
/// TrueNorth 30° im Uhrzeigersinn von +y (Richtung (0.5, 0.866…)).</summary>
public static class IfcHuelleTestdatei
{
    static readonly object sperre = new();
    static readonly Dictionary<string, string> dateien = [];

    public static string Datei(string schema, HuelleVariante? v = null)
    {
        v ??= new HuelleVariante();
        var name = $"b4_huelle_{schema.ToLowerInvariant()}_{v.Kuerzel}.ifc";
        lock (sperre)
        {
            if (dateien.TryGetValue(name, out var da)) return da;
            var ordner = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "testdateien_b4_" + Environment.ProcessId);
            Directory.CreateDirectory(ordner);
            var pfad = Path.Combine(ordner, name);
            Erzeugen(pfad, schema == "IFC4", v);
            return dateien[name] = pfad;
        }
    }

    static XbimEditorCredentials Wer => new()
    {
        ApplicationDevelopersName = "H+K Planungs AG", ApplicationFullName = "HLKS-CAD Hüllentest", ApplicationIdentifier = "hlks-b4",
        ApplicationVersion = "1.0", EditorsFamilyName = "Test", EditorsGivenName = "HLKS", EditorsOrganisationName = "H+K Planungs AG",
    };

    static void Erzeugen(string pfad, bool ifc4, HuelleVariante v)
    {
        using var m = IfcStore.Create(Wer, ifc4 ? XbimSchemaVersion.Ifc4 : XbimSchemaVersion.Ifc2X3, XbimStoreType.InMemoryModel);
        using (var tr = m.BeginTransaction("B4"))
        {
            var i = m.Instances;
            var c = new EntityCreator(m);
            IIfcProject projekt;
            if (ifc4) { var p4 = i.New<Xbim.Ifc4.Kernel.IfcProject>(p => p.Name = "B4 Test"); p4.Initialize(ProjectUnits.SIUnitsUK); projekt = p4; }
            else { var p2 = i.New<Xbim.Ifc2x3.Kernel.IfcProject>(p => p.Name = "B4 Test"); p2.Initialize(ProjectUnits.SIUnitsUK); projekt = p2; }
            var ctx = i.OfType<IIfcGeometricRepresentationContext>().First();

            IIfcCartesianPoint P(double x, double y, double z) => c.CartesianPoint(p => { p.Coordinates.Add(x); p.Coordinates.Add(y); p.Coordinates.Add(z); });
            IIfcCartesianPoint P2(double x, double y) => c.CartesianPoint(p => { p.Coordinates.Add(x); p.Coordinates.Add(y); });
            IIfcDirection D(double x, double y, double z) => c.Direction(d => { d.DirectionRatios.Add(x); d.DirectionRatios.Add(y); d.DirectionRatios.Add(z); });

            // TrueNorth 30° im Uhrzeigersinn von +y
            if (v.OhneNord) ctx.TrueNorth = null;
            else ctx.TrueNorth = c.Direction(d => { d.DirectionRatios.Add(Math.Sin(30 * Math.PI / 180)); d.DirectionRatios.Add(Math.Cos(30 * Math.PI / 180)); });

            if (v.Einheit)
            {
                // spezifische Wärmekapazität in kJ/(kg·K)
                IIfcSIUnit Si(IfcUnitEnum art, IfcSIPrefix? prefix, IfcSIUnitName name) => c.SIUnit(s => { s.UnitType = art; s.Prefix = prefix; s.Name = name; });
                var du = c.DerivedUnit(u =>
                {
                    u.UnitType = IfcDerivedUnitEnum.SPECIFICHEATCAPACITYUNIT;
                    u.Elements.Add(c.DerivedUnitElement(e => { e.Unit = Si(IfcUnitEnum.ENERGYUNIT, IfcSIPrefix.KILO, IfcSIUnitName.JOULE); e.Exponent = 1; }));
                    u.Elements.Add(c.DerivedUnitElement(e => { e.Unit = Si(IfcUnitEnum.MASSUNIT, IfcSIPrefix.KILO, IfcSIUnitName.GRAM); e.Exponent = -1; }));
                    u.Elements.Add(c.DerivedUnitElement(e => { e.Unit = Si(IfcUnitEnum.THERMODYNAMICTEMPERATUREUNIT, null, IfcSIUnitName.KELVIN); e.Exponent = -1; }));
                });
                projekt.UnitsInContext.Units.Add(du);
            }

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
            IIfcExtrudedAreaSolid Extrusion(IIfcProfileDef profil, double tiefe) => c.ExtrudedAreaSolid(x =>
            {
                x.SweptArea = profil; x.Depth = tiefe; x.ExtrudedDirection = D(0, 0, 1); x.Position = A3(0, 0, 0);
            });
            IIfcRectangleProfileDef Rechteck(double dx, double dy, double mx, double my) => c.RectangleProfileDef(r =>
            {
                r.ProfileType = IfcProfileTypeEnum.AREA; r.XDim = dx; r.YDim = dy; r.Position = A2(mx, my);
            });
            void MatZu(IIfcMaterialSelect mat, params IIfcObjectDefinition[] o) => c.RelAssociatesMaterial(r => { r.RelatingMaterial = mat; foreach (var x in o) r.RelatedObjects.Add(x); });
            void Pset(IIfcObject o, string name, string eig, IIfcValue wert) => c.RelDefinesByProperties(r =>
            {
                r.RelatingPropertyDefinition = c.PropertySet(ps => { ps.Name = name; ps.HasProperties.Add(c.PropertySingleValue(p => { p.Name = eig; p.NominalValue = wert; })); });
                r.RelatedObjects.Add(o);
            });
            static IIfcValue Zahl(string art, double x) => art switch
            {
                "U" => new Xbim.Ifc4.MeasureResource.IfcThermalTransmittanceMeasure(x),
                "Anteil" => new Xbim.Ifc4.MeasureResource.IfcPositiveRatioMeasure(x),
                _ => throw new ArgumentException(art),
            };

            // ---- Materialien mit Testwerten
            IIfcMaterial Mat(string name) => c.Material(x => x.Name = name);
            Xbim.Ifc4.PropertyResource.IfcPropertySingleValue Einzel(string name, IIfcValue wert) =>
                i.New<Xbim.Ifc4.PropertyResource.IfcPropertySingleValue>(p => { p.Name = name; p.NominalValue = (Xbim.Ifc4.MeasureResource.IfcValue)wert; });
            void Kennwerte(IIfcMaterial mat, double? lambda, double? waerme, double? dichte)
            {
                double wk = v.Einheit ? 0.456 : waerme ?? 0;     // Testwert: 456 J/(kg·K), in der Variante Einheit als 0.456 kJ/(kg·K)
                if (ifc4)
                {
                    var mm = (Xbim.Ifc4.MaterialResource.IfcMaterial)mat;
                    if (lambda is not null || waerme is not null)
                        i.New<Xbim.Ifc4.MaterialResource.IfcMaterialProperties>(mp =>
                        {
                            mp.Name = "Pset_MaterialThermal"; mp.Material = mm;
                            if (lambda is { } l) mp.Properties.Add(Einzel("ThermalConductivity", new Xbim.Ifc4.MeasureResource.IfcThermalConductivityMeasure(l)));
                            if (waerme is not null) mp.Properties.Add(Einzel("SpecificHeatCapacity", new Xbim.Ifc4.MeasureResource.IfcSpecificHeatCapacityMeasure(wk)));
                        });
                    if (dichte is { } r)
                        i.New<Xbim.Ifc4.MaterialResource.IfcMaterialProperties>(mp =>
                        {
                            mp.Name = "Pset_MaterialCommon"; mp.Material = mm;
                            mp.Properties.Add(Einzel("MassDensity", new Xbim.Ifc4.MeasureResource.IfcMassDensityMeasure(r)));
                        });
                }
                else
                {
                    var mm = (Xbim.Ifc2x3.MaterialResource.IfcMaterial)mat;
                    if (waerme is not null)
                        // beide Wege der Fassung 2x3: eigene Entität (Beton) …
                        i.New<Xbim.Ifc2x3.MaterialPropertyResource.IfcThermalMaterialProperties>(mp =>
                        {
                            mp.Material = mm;
                            if (lambda is { } l) mp.ThermalConductivity = new Xbim.Ifc2x3.MeasureResource.IfcThermalConductivityMeasure(l);
                            mp.SpecificHeatCapacity = new Xbim.Ifc2x3.MeasureResource.IfcSpecificHeatCapacityMeasure(wk);
                        });
                    else if (lambda is { } l2)
                        // … und IfcExtendedMaterialProperties mit dem Pset-Namen (Revit-Art, Wärmedämmung)
                        i.New<Xbim.Ifc2x3.MaterialPropertyResource.IfcExtendedMaterialProperties>(mp =>
                        {
                            mp.Name = "Pset_MaterialThermal"; mp.Material = mm;
                            mp.ExtendedProperties.Add(i.New<Xbim.Ifc2x3.PropertyResource.IfcPropertySingleValue>(p =>
                            {
                                p.Name = "ThermalConductivity"; p.NominalValue = new Xbim.Ifc2x3.MeasureResource.IfcThermalConductivityMeasure(l2);
                            }));
                        });
                    if (dichte is { } r)
                        i.New<Xbim.Ifc2x3.MaterialPropertyResource.IfcGeneralMaterialProperties>(mp =>
                        {
                            mp.Material = mm; mp.MassDensity = new Xbim.Ifc2x3.MeasureResource.IfcMassDensityMeasure(r);
                        });
                }
            }
            var beton = Mat("Beton"); Kennwerte(beton, 0.123, 456, 789);
            var daemmung = Mat("Wärmedämmung"); Kennwerte(daemmung, 0.321, null, null);
            var estrich = Mat("Estrich");
            var gips = Mat("Gipsplatte");

            IIfcMaterialLayerSetUsage Usage(string name, (IIfcMaterial Mat, double Dicke)[] schichten, IfcLayerSetDirectionEnum richtung, IfcDirectionSenseEnum sinn, double versatz) =>
                c.MaterialLayerSetUsage(u =>
                {
                    u.ForLayerSet = c.MaterialLayerSet(s =>
                    {
                        s.LayerSetName = name;
                        foreach (var (mat, dicke) in schichten) s.MaterialLayers.Add(c.MaterialLayer(l => { l.Material = mat; l.LayerThickness = dicke; }));
                    });
                    u.LayerSetDirection = richtung; u.DirectionSense = sinn; u.OffsetFromReferenceLine = versatz;
                });

            // ---- Gliederung
            var lSite = L(null, 0, 0, 0);
            var site = c.Site(s => { s.Name = "Gelaende"; s.ObjectPlacement = lSite; });
            var lGeb = L(lSite, 0, 0, 0, v.Gedreht ? 20 : 0);
            var geb = c.Building(b => { b.Name = "Testhaus"; b.ObjectPlacement = lGeb; });
            var lEg = L(lGeb, 0, 0, 0);
            var eg = c.BuildingStorey(s => { s.Name = "Erdgeschoss"; s.Elevation = 0; s.ObjectPlacement = lEg; });
            c.RelAggregates(r => { r.RelatingObject = projekt; r.RelatedObjects.Add(site); });
            c.RelAggregates(r => { r.RelatingObject = site; r.RelatedObjects.Add(geb); });
            c.RelAggregates(r => { r.RelatingObject = geb; r.RelatedObjects.Add(eg); });
            var imEg = new List<IIfcProduct>();

            IIfcWall Wand(string name, double x, double y, double grad, double laenge, IIfcMaterialSelect mat, bool schichten)
            {
                if (v.OhneSchichten) { mat = gips; schichten = false; }
                var w = c.Wall(w =>
                {
                    w.Name = name; w.ObjectPlacement = L(lEg, x, y, 0, grad);
                    w.Representation = Form(Rep("Axis", "Curve2D", Linie((0, 0), (laenge, 0))), Rep("Body", "SweptSolid", Extrusion(Rechteck(laenge, 200, laenge / 2, 100), 2600)));
                });
                MatZu(mat, w);
                if (!schichten)
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
                imEg.Add(w);
                return w;
            }
            // Testwerte der Wand-Psets
            void WandPsets(IIfcWall w, double u)
            {
                Pset(w, "Pset_WallCommon", "ThermalTransmittance", Zahl("U", u));
                Pset(w, "Pset_WallCommon", "IsExternal", new Xbim.Ifc4.MeasureResource.IfcBoolean(true));
            }

            (IIfcMaterial, double)[] Aussen(IIfcMaterial wd, IIfcMaterial bt) => [(wd, 50), (bt, 150)];
            var sued = Wand("W-Sued", 0, 0, 0, 6000, Usage("Aussenwand", Aussen(daemmung, beton), IfcLayerSetDirectionEnum.AXIS2, IfcDirectionSenseEnum.POSITIVE, 0), true);
            WandPsets(sued, 0.25);
            var ost = Wand("W-Ost", 6000, 0, 90, 4000, Usage("Aussenwand innen gedaemmt", Aussen(daemmung, beton), IfcLayerSetDirectionEnum.AXIS2, IfcDirectionSenseEnum.NEGATIVE, 200), true);
            WandPsets(ost, 0.35);
            var nord = Wand("W-Nord", 6000, 4000, 180, 6000, Usage("Aussenwand", Aussen(Mat("Wärmedämmung"), Mat("Beton")), IfcLayerSetDirectionEnum.AXIS2, IfcDirectionSenseEnum.POSITIVE, 0), true);
            WandPsets(nord, v.Konflikt ? 0.28 : 0.25);
            Wand("W-West", 0, 4000, 270, 4000, gips, false);

            // Tür in W-Sued
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
            c.RelFillsElement(r => { r.RelatingOpeningElement = oeffT; r.RelatedBuildingElement = tuer; });
            Pset(tuer, "Pset_DoorCommon", "ThermalTransmittance", Zahl("U", 1.8));   // Testwert
            imEg.Add(tuer);

            // Fenster in W-Nord
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
            Pset(fenster, "Pset_WindowCommon", "ThermalTransmittance", Zahl("U", 1.3));      // Testwert
            Pset(fenster, "Pset_WindowCommon", "GlazingAreaFraction", Zahl("Anteil", 0.7));   // Testwert
            imEg.Add(fenster);

            // Bodenplatte: Oberkante 0, 360 dick
            var boden = c.Slab(s =>
            {
                s.Name = "Bodenplatte"; s.PredefinedType = IfcSlabTypeEnum.BASESLAB; s.ObjectPlacement = L(lEg, 0, 0, -360);
                s.Representation = Form(Rep("Body", "SweptSolid", Extrusion(Rechteck(6000, 4000, 3000, 2000), 360)));
            });
            MatZu(v.OhneSchichten ? (IIfcMaterialSelect)estrich : Usage("Bodenplatte", [(estrich, 60), (daemmung, 100), (beton, 200)], IfcLayerSetDirectionEnum.AXIS3, IfcDirectionSenseEnum.NEGATIVE, 360), boden);
            Pset(boden, "Pset_SlabCommon", "ThermalTransmittance", Zahl("U", 0.45));   // Testwert
            imEg.Add(boden);

            c.RelContainedInSpatialStructure(r => { r.RelatingStructure = eg; foreach (var x in imEg) r.RelatedElements.Add(x); });
            tr.Commit();
        }
        m.SaveAs(pfad, StorageType.Ifc);
    }
}
