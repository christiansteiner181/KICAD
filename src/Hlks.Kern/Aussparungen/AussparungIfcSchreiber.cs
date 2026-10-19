// IFC-Ausgabe der Aussparungen (Modul 3b, B4b): der Schreiber. Erzeugt die Datei mit Xbim.Essentials 6.1.605 aus den Daten von
// AussparungIfcExport – IFC4 und IFC2X3 mit derselben Beschreibung (EntityCreator der IFC4-Schnittstellen, wie die Testdateien von
// Modul 1b); was es nur in einer Fassung gibt (PredefinedType des Proxys), steht in einer Verzweigung. Aufbau und Schemaprüfung:
// Kopf von AussparungIfcExport.cs.

using Xbim.Common;
using Xbim.Common.Step21;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.IO;

namespace Hlks.Kern;

public static class AussparungIfcSchreiber
{
    static double R(double x) => Math.Round(x, 4) + 0.0;   // + 0.0: aus -0 wird 0

    /// <summary>Schreibt die Datei (STEP, .ifc). Der Pfad der Hilfsdatei darf jede Endung haben.</summary>
    public static void Schreiben(IfcAusgabe aus, string datei)
    {
        bool ifc4 = aus.Fassung == AussparungIfcExport.Fassung4;
        var wer = new XbimEditorCredentials
        {
            ApplicationDevelopersName = "H+K Planungs AG", ApplicationFullName = "KICAD (HLKS-CAD)", ApplicationIdentifier = "kicad",
            ApplicationVersion = typeof(AussparungIfcSchreiber).Assembly.GetName().Version?.ToString() ?? "1.0",
            EditorsFamilyName = string.IsNullOrWhiteSpace(aus.Akteur) ? "KICAD" : aus.Akteur, EditorsGivenName = "-", EditorsOrganisationName = "H+K Planungs AG",
        };
        using var m = IfcStore.Create(wer, ifc4 ? XbimSchemaVersion.Ifc4 : XbimSchemaVersion.Ifc2X3, XbimStoreType.InMemoryModel);
        m.Header.FileName.Name = Path.GetFileName(aus.Pfad);
        m.Header.FileName.AuthorName.Clear(); m.Header.FileName.AuthorName.Add(wer.EditorsFamilyName);
        m.Header.FileName.Organization.Clear(); m.Header.FileName.Organization.Add(wer.EditorsOrganisationName);
        m.Header.FileDescription.Description.Clear(); m.Header.FileDescription.Description.Add("Aussparungen und Kernbohrungen HLKS (KICAD) für Architekt und Bauingenieur");
        using (var tr = m.BeginTransaction("Aussparungen"))
        {
            var i = m.Instances;
            var c = new EntityCreator(m);
            string G(string schluessel) => AussparungIfcExport.GlobalId(aus.ProjektId, schluessel);

            IIfcProject projekt;
            var beschr = $"Aussparungen und Kernbohrungen HLKS (KICAD), Stand {aus.Zeit.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture)}, Koordinaten: {aus.Koordinaten}";
            if (ifc4)
            {
                var p4 = i.New<Xbim.Ifc4.Kernel.IfcProject>(p => { p.GlobalId = G("ifc/projekt"); p.Name = aus.ProjektName; p.Description = beschr; });
                p4.Initialize(ProjectUnits.SIUnitsUK);
                projekt = p4;
            }
            else
            {
                var p2 = i.New<Xbim.Ifc2x3.Kernel.IfcProject>(p => { p.GlobalId = G("ifc/projekt"); p.Name = aus.ProjektName; p.Description = beschr; });
                p2.Initialize(ProjectUnits.SIUnitsUK);
                projekt = p2;
            }
            var ctx = i.OfType<IIfcGeometricRepresentationContext>().First();

            IIfcCartesianPoint P(double x, double y, double z) => c.CartesianPoint(p => { p.Coordinates.Add(R(x)); p.Coordinates.Add(R(y)); p.Coordinates.Add(R(z)); });
            IIfcCartesianPoint P2(double x, double y) => c.CartesianPoint(p => { p.Coordinates.Add(R(x)); p.Coordinates.Add(R(y)); });
            IIfcDirection D(V3 v) => c.Direction(d => { d.DirectionRatios.Add(Math.Round(v.X, 12) + 0.0); d.DirectionRatios.Add(Math.Round(v.Y, 12) + 0.0); d.DirectionRatios.Add(Math.Round(v.Z, 12) + 0.0); });
            IIfcAxis2Placement3D A3(double x, double y, double z, V3? achse = null, V3? richtung = null) => c.Axis2Placement3D(a =>
            {
                a.Location = P(x, y, z);
                if (achse is { } az) a.Axis = D(az);
                if (richtung is { } ax) a.RefDirection = D(ax);
            });
            IIfcLocalPlacement L(IIfcObjectPlacement? rel, IIfcAxis2Placement3D lage) => c.LocalPlacement(l => { l.PlacementRelTo = rel; l.RelativePlacement = lage; });

            // Gliederung: Gelände im Ursprung, Gebäude im Projektbezug (koordinaten ifc) bzw. im Ursprung
            var site = c.Site(s => { s.GlobalId = G("ifc/gelaende"); s.Name = "Gelaende"; s.ObjectPlacement = L(null, A3(0, 0, 0)); s.CompositionType = IfcElementCompositionEnum.ELEMENT; });
            var lSite = site.ObjectPlacement;
            // IfcAxis2Placement3D.AxisAndRefDirProvision: Axis und RefDirection nur zusammen (Fund der Schemaprüfung)
            IIfcAxis2Placement3D gebLage = aus.Bezug is { } b
                ? A3(b.X, b.Y, b.Z, b.Drehung == 0 ? null : new V3(0, 0, 1),
                    b.Drehung == 0 ? null : new V3(Math.Cos(b.Drehung * Math.PI / 180), Math.Sin(b.Drehung * Math.PI / 180), 0))
                : A3(0, 0, 0);
            var lGeb = L(lSite, gebLage);
            var geb = c.Building(x =>
            {
                x.GlobalId = G("ifc/gebaeude"); x.Name = aus.ProjektName; x.ObjectPlacement = lGeb; x.CompositionType = IfcElementCompositionEnum.ELEMENT;
            });
            c.RelAggregates(r => { r.GlobalId = G("ifc/rel/projekt-gelaende"); r.RelatingObject = projekt; r.RelatedObjects.Add(site); });
            c.RelAggregates(r => { r.GlobalId = G("ifc/rel/gelaende-gebaeude"); r.RelatingObject = site; r.RelatedObjects.Add(geb); });

            // Geschosse
            var lGeschoss = new Dictionary<string, IIfcLocalPlacement>(StringComparer.Ordinal);
            var geschoss = new Dictionary<string, IIfcBuildingStorey>(StringComparer.Ordinal);
            foreach (var g in aus.Geschosse)
            {
                var lg = L(lGeb, A3(0, 0, g.Kote));
                var s = c.BuildingStorey(x =>
                {
                    x.GlobalId = g.GlobalId; x.Name = g.Kennzeichen;
                    if (!string.IsNullOrWhiteSpace(g.Name) && g.Name != g.Kennzeichen) x.LongName = g.Name;
                    x.Elevation = R(g.Kote); x.ObjectPlacement = lg; x.CompositionType = IfcElementCompositionEnum.ELEMENT;
                });
                lGeschoss[g.Id] = lg; geschoss[g.Id] = s;
                var eig = new List<IfcEigenschaft> { new("Kote", "laenge", Laenge: R(g.Kote)) };
                if (g.Hoehe is double h) eig.Add(new("Hoehe", "laenge", Laenge: R(h)));
                Pset(c, s, AussparungIfcExport.PsetGeschoss, eig, G("ifc/pset/geschoss/" + g.Id), G);
            }
            if (geschoss.Count > 0)
                c.RelAggregates(r =>
                {
                    r.GlobalId = G("ifc/rel/gebaeude-geschosse"); r.RelatingObject = geb;
                    foreach (var s in geschoss.Values) r.RelatedObjects.Add(s);
                });

            // Aussparungen
            var imGeschoss = new Dictionary<string, List<IIfcProduct>>(StringComparer.Ordinal);
            var imGebaeude = new List<IIfcProduct>();
            foreach (var d in aus.Aussparungen)
            {
                var lage = d.Lage;
                var kote = d.GeschossId is { } gid ? aus.Geschosse.First(x => x.Id == gid).Kote : 0;
                var rel = d.GeschossId is { } g2 ? lGeschoss[g2] : lGeb;
                IIfcProfileDef profil = lage.Rund
                    ? c.CircleProfileDef(x => { x.ProfileType = IfcProfileTypeEnum.AREA; x.Radius = R(lage.D / 2); x.Position = c.Axis2Placement2D(a => a.Location = P2(0, 0)); })
                    : c.RectangleProfileDef(x => { x.ProfileType = IfcProfileTypeEnum.AREA; x.XDim = R(lage.B); x.YDim = R(lage.H); x.Position = c.Axis2Placement2D(a => a.Location = P2(0, 0)); });
                var koerper = c.ExtrudedAreaSolid(x =>
                {
                    x.SweptArea = profil; x.Depth = R(lage.Tiefe); x.ExtrudedDirection = D(new V3(0, 0, 1)); x.Position = A3(0, 0, -lage.Tiefe / 2);
                });
                var form = c.ProductDefinitionShape(sh => sh.Representations.Add(c.ShapeRepresentation(r =>
                {
                    r.ContextOfItems = ctx; r.RepresentationIdentifier = "Body"; r.RepresentationType = "SweptSolid"; r.Items.Add(koerper);
                })));
                var proxy = c.BuildingElementProxy(x =>
                {
                    x.GlobalId = d.GlobalId; x.Name = d.Name; x.Description = d.Beschreibung; x.Tag = d.Kennzeichen;
                    x.ObjectPlacement = L(rel, A3(lage.Mitte.X, lage.Mitte.Y, lage.Mitte.Z - kote, lage.Achse, lage.U));
                    x.Representation = form;
                    if (ifc4) x.PredefinedType = IfcBuildingElementProxyTypeEnum.PROVISIONFORVOID;   // IFC4
                    else x.ObjectType = AussparungIfcExport.ObjectType2x3;                           // IFC2X3: kein PredefinedType am Proxy
                });
                Pset(c, proxy, AussparungIfcExport.PsetAussparung, d.Eigenschaften, G("ifc/pset/aussparung/" + d.Id), G);
                if (d.GeschossId is { } g3)
                {
                    if (!imGeschoss.TryGetValue(g3, out var l)) imGeschoss[g3] = l = [];
                    l.Add(proxy);
                }
                else imGebaeude.Add(proxy);
            }
            foreach (var g in aus.Geschosse)
                if (imGeschoss.TryGetValue(g.Id, out var l))
                    c.RelContainedInSpatialStructure(r =>
                    {
                        r.GlobalId = G("ifc/rel/enthalten/geschoss/" + g.Id); r.RelatingStructure = geschoss[g.Id];
                        foreach (var x in l) r.RelatedElements.Add(x);
                    });
            if (imGebaeude.Count > 0)
                c.RelContainedInSpatialStructure(r =>
                {
                    r.GlobalId = G("ifc/rel/enthalten/gebaeude"); r.RelatingStructure = geb;
                    foreach (var x in imGebaeude) r.RelatedElements.Add(x);
                });
            tr.Commit();
        }
        m.SaveAs(datei, StorageType.Ifc);
    }

    static void Pset(EntityCreator c, IIfcObject objekt, string name, IEnumerable<IfcEigenschaft> eigenschaften, string psetGid, Func<string, string> g)
    {
        c.RelDefinesByProperties(r =>
        {
            r.GlobalId = g("ifc/rel/pset/" + psetGid);
            r.RelatingPropertyDefinition = c.PropertySet(ps =>
            {
                ps.GlobalId = psetGid; ps.Name = name;
                foreach (var e in eigenschaften)
                {
                    if (Wert(e) is not { } w) continue;
                    ps.HasProperties.Add(c.PropertySingleValue(v => { v.Name = e.Name; v.NominalValue = w; }));
                }
            });
            r.RelatedObjects.Add(objekt);
        });
    }

    /// <summary>Wert als IFC-Messart (IFC4-Klassen; xBIM übersetzt sie beim Schreiben von IFC2X3 in die Klassen dieser Fassung). Leere Texte entfallen.</summary>
    static IIfcValue? Wert(IfcEigenschaft e) => e.Art switch
    {
        "laenge" when e.Laenge is double l => new Xbim.Ifc4.MeasureResource.IfcLengthMeasure(R(l)),
        "text" when !string.IsNullOrEmpty(e.Text) => new Xbim.Ifc4.MeasureResource.IfcText(e.Text),
        "kennung" when !string.IsNullOrEmpty(e.Text) => new Xbim.Ifc4.MeasureResource.IfcIdentifier(e.Text),
        "label" when !string.IsNullOrEmpty(e.Text) => new Xbim.Ifc4.MeasureResource.IfcLabel(e.Text),
        _ => null,
    };
}
