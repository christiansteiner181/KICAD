// IFC-Export (Modul 7 Teil B2): der Schreiber. Erzeugt IFC4 oder IFC2X3 mit Xbim.Essentials 6.1.605 aus der neutralen Beschreibung
// (IfcExportDaten.cs) – wie AussparungIfcSchreiber über die IFC4-Schnittstellen und den EntityCreator, wo beide Fassungen dieselbe
// Entität kennen; Entitäten aus ifc.json (IfcPipeSegment, IfcFlowSegment …) über ihren Namen aus den Schemaklassen der Fassung.
// Jede Entität, jedes Attribut und jeder Aufzählungswert wird dabei an den Xbim-Klassen der Fassung geprüft: unbekannte Entität,
// fehlende Eigenschaft oder Wert ausserhalb der Aufzählung = HlksFehler (nie still ersetzt). Am 20.10.2026 an Xbim 6.1.605 geprüft
// (Test IfcExportSchemaTests):
//   IfcDistributionPort      IFC4: FlowDirection, PredefinedType (IfcDistributionPortTypeEnum: PIPE, DUCT …), SystemType
//                            (IfcDistributionSystemEnum); IFC2X3: nur FlowDirection. Am Element: IFC4 IfcRelNests (Element → Ports),
//                            IFC2X3 IfcRelConnectsPortToElement (RelatedElement: IfcElement). Verbindung: IfcRelConnectsPorts (beide).
//   IfcCovering (Dämmung)    beide Fassungen, PredefinedType INSULATION (Wert in IfcCoveringTypeEnum IFC4 und IFC2X3). Zuordnung zum
//                            Element über IfcRelCoversBldgElements: RelatingBuildingElement ist in beiden Fassungen IfcElement – Rohre
//                            (IfcPipeSegment bzw. IfcFlowSegment) sind IfcElement, die Beziehung ist damit am Schema zulässig. Die
//                            Dämmung steht zusätzlich im Geschoss (IfcRelContainedInSpatialStructure) wie jedes Bauteil.
//   Systeme                  IFC4 IfcDistributionSystem (PredefinedType aus ifc.json systemtypen, LongName), Stränge IfcDistributionCircuit
//                            (Untertyp, als Teil des Systems über IfcRelAggregates – eine Zerlegung, kein zweites System); IFC2X3 kennt
//                            beide nicht: IfcSystem mit ObjectType = Systemtyp, keine Stränge.
//   Anlagen                  ANLAGE ALS GRUPPE DER SYSTEME: IfcSystem (ifc.json, beide Fassungen) mit IfcRelAssignsToGroup → Systeme.
//                            Begründung: IfcSystem ist eine IfcGroup; Mitgliedschaft in einer Gruppe ist in IFC IfcRelAssignsToGroup
//                            (RelatedObjects: IfcObjectDefinition – ein IfcSystem ist eines, geprüft in beiden Fassungen). IfcRelAggregates
//                            würde eine Zerlegung (Ganzes – Teil) behaupten, die IFC4 für System → Strang braucht; eine Anlage ist eine
//                            Zusammenstellung, kein Ganzes aus Teilen, und ein System dürfte so nur einer Anlage angehören. IfcBuildingSystem
//                            (IFC4) ist für Bauteile der Hülle bestimmt, IfcZone für Räume – beide passen nicht. ObjectType «Anlage» trennt
//                            Anlagen in IFC2X3 von den Systemen.
//   Klassifikation           IfcClassification (IFC2X3: Source und Edition Pflicht – die Ausgabe des BKP steht nicht in ifc.json: «ohne
//                            Angabe»), je Kode eine IfcClassificationReference, IfcRelAssociatesClassification.
//   Kartenbezug              nur IFC4: IfcMapConversion (SourceCRS = Modellkontext) → IfcProjectedCRS (Name = Bezugssystem, MapUnit mm).

using System.Collections.Concurrent;
using System.Reflection;
using Xbim.Common;
using Xbim.Common.Step21;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.IO;

namespace Hlks.Kern;

public static class IfcExportSchreiber
{
    static readonly Assembly Ifc4Asm = typeof(Xbim.Ifc4.Kernel.IfcProduct).Assembly;
    static readonly Assembly Ifc2x3Asm = typeof(Xbim.Ifc2x3.Kernel.IfcProduct).Assembly;
    static readonly Lazy<Dictionary<string, Type>> klassen4 = new(() => Klassen(Ifc4Asm));
    static readonly Lazy<Dictionary<string, Type>> klassen2x3 = new(() => Klassen(Ifc2x3Asm));

    static Dictionary<string, Type> Klassen(Assembly a)
    {
        Type[] alle;
        try { alle = a.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { alle = ex.Types.Where(t => t is not null).ToArray()!; }
        var d = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in alle.Where(t => t.IsClass && !t.IsAbstract && typeof(IPersistEntity).IsAssignableFrom(t))) d.TryAdd(t.Name, t);
        return d;
    }

    /// <summary>Entitätsklasse der Fassung (Xbim-Schemaklasse) oder null, wenn es die Entität in dieser Fassung nicht gibt.</summary>
    public static Type? Klasse(string fassung, string entitaet) =>
        (fassung == AussparungIfcExport.Fassung4 ? klassen4.Value : klassen2x3.Value).GetValueOrDefault(entitaet);

    /// <summary>Schreibt die Datei (STEP). Rückgabe: Anzahl Entitäten der Datei.</summary>
    public static int Schreiben(IfcExAusgabe aus, string datei, string zielname)
    {
        bool ifc4 = aus.IstIfc4;
        var wer = new XbimEditorCredentials
        {
            ApplicationDevelopersName = "H+K Planungs AG", ApplicationFullName = "KICAD (HLKS-CAD)", ApplicationIdentifier = "kicad",
            ApplicationVersion = typeof(IfcExportSchreiber).Assembly.GetName().Version?.ToString() ?? "1.0",
            EditorsFamilyName = string.IsNullOrWhiteSpace(aus.Akteur) ? "KICAD" : aus.Akteur, EditorsGivenName = "-", EditorsOrganisationName = "H+K Planungs AG",
        };
        using var m = IfcStore.Create(wer, ifc4 ? XbimSchemaVersion.Ifc4 : XbimSchemaVersion.Ifc2X3, XbimStoreType.InMemoryModel);
        m.Header.FileName.Name = zielname;
        m.Header.FileName.AuthorName.Clear(); m.Header.FileName.AuthorName.Add(wer.EditorsFamilyName);
        m.Header.FileName.Organization.Clear(); m.Header.FileName.Organization.Add(wer.EditorsOrganisationName);
        m.Header.FileDescription.Description.Clear(); m.Header.FileDescription.Description.Add("HLKS-Modell KICAD (H+K Planungs AG)");
        using (var tr = m.BeginTransaction("IFC-Export"))
        {
            new Bau(m, aus).Alles();
            tr.Commit();
        }
        m.SaveAs(datei, StorageType.Ifc);
        return (int)m.Instances.Count;
    }

    /// <summary>Ein Schreiblauf.</summary>
    sealed class Bau
    {
        readonly IfcStore m;
        readonly EntityCreator c;
        readonly IfcExAusgabe aus;
        readonly bool ifc4;
        IIfcGeometricRepresentationContext ctx = null!;
        IIfcAxis2Placement2D? null2;
        readonly Dictionary<(long, long, long), IIfcDirection> richtungen = [];
        readonly Dictionary<string, IIfcObjectDefinition> objekte = new(StringComparer.Ordinal);
        readonly Dictionary<string, IIfcLocalPlacement> geschossLage = new(StringComparer.Ordinal);
        readonly Dictionary<string, IIfcBuildingStorey> geschosse = new(StringComparer.Ordinal);
        readonly Dictionary<string, IIfcBuilding> gebaeude = new(StringComparer.Ordinal);
        readonly Dictionary<string, IIfcLocalPlacement> gebaeudeLage = new(StringComparer.Ordinal);

        public Bau(IfcStore m, IfcExAusgabe aus) { this.m = m; this.aus = aus; ifc4 = aus.IstIfc4; c = new EntityCreator(m); }

        string G(string schluessel) => AussparungIfcExport.GlobalId(aus.ProjektId, schluessel);
        static double R(double x) => Math.Round(x, 4) + 0.0;

        // ---------------------------------------------------------- Schema (Xbim-Klassen der Fassung)

        IPersistEntity Neu(string entitaet)
        {
            var t = Klasse(aus.Fassung, entitaet)
                ?? throw new HlksFehler($"Die Entität {entitaet} gibt es in {aus.Fassung} nicht (Xbim-Schemaklassen 6.1.605).", "Abbildung in kataloge/ifc.json für diese Fassung prüfen.");
            return m.Instances.New(t);
        }

        static PropertyInfo? Eigenschaft(object o, string name) =>
            o.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.Name == name && p.CanWrite)
                .OrderByDescending(p => Tiefe(p.DeclaringType)).FirstOrDefault();

        static int Tiefe(Type? t) { int n = 0; for (; t is not null; t = t.BaseType) n++; return n; }

        /// <summary>Setzt einen Aufzählungswert (Name) – nur wenn die Eigenschaft am Schema existiert und der Wert in der Aufzählung steht.</summary>
        void Aufzaehlung(object o, string eigenschaft, string? wert, bool pflicht = false)
        {
            if (wert is null) return;
            var p = Eigenschaft(o, eigenschaft);
            var name = ((IPersistEntity)o).ExpressType.Name;
            if (p is null)
            {
                if (pflicht) throw new HlksFehler($"{name} hat in {aus.Fassung} kein Attribut {eigenschaft} (Xbim-Schemaklasse).");
                return;
            }
            var t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
            if (!t.IsEnum || !Enum.GetNames(t).Contains(wert, StringComparer.Ordinal))
                throw new HlksFehler($"{name}.{eigenschaft} = {wert} gibt es in {aus.Fassung} nicht ({t.Name}: {(t.IsEnum ? string.Join(", ", Enum.GetNames(t)) : "keine Aufzählung")}).",
                    "Abbildung in kataloge/ifc.json prüfen.");
            p.SetValue(o, Enum.Parse(t, wert));
        }

        /// <summary>Pflicht-Aufzählungen von Typobjekten (OperationType, ConstructionType, PartitioningType) auf NOTDEFINED, statt den
        /// ersten Wert der Aufzählung stillschweigend zu behaupten.</summary>
        void Pflichtaufzaehlungen(object o)
        {
            foreach (var n in new[] { "OperationType", "ConstructionType", "PartitioningType" })
                if (Eigenschaft(o, n) is { } p && p.PropertyType.IsEnum && Enum.GetNames(p.PropertyType).Contains("NOTDEFINED"))
                    p.SetValue(o, Enum.Parse(p.PropertyType, "NOTDEFINED"));
        }

        // ---------------------------------------------------------- Geometrie-Grundlagen

        IIfcCartesianPoint P(double[] p) => c.CartesianPoint(x => { x.Coordinates.Add(R(p[0])); x.Coordinates.Add(R(p[1])); x.Coordinates.Add(R(p.Length > 2 ? p[2] : 0)); });
        IIfcCartesianPoint P2(double x0, double y0) => c.CartesianPoint(x => { x.Coordinates.Add(R(x0)); x.Coordinates.Add(R(y0)); });

        IIfcDirection D(double[] v)
        {
            var e = IfcExportGeometrie.Einheit(v);
            var k = ((long)Math.Round(e[0] * 1e9), (long)Math.Round(e[1] * 1e9), (long)Math.Round(e[2] * 1e9));
            if (richtungen.TryGetValue(k, out var d)) return d;
            d = c.Direction(x => { x.DirectionRatios.Add(Math.Round(e[0], 9) + 0.0); x.DirectionRatios.Add(Math.Round(e[1], 9) + 0.0); x.DirectionRatios.Add(Math.Round(e[2], 9) + 0.0); });
            richtungen[k] = d;
            return d;
        }

        /// <summary>Achsensystem; Axis und RefDirection immer zusammen (IfcAxis2Placement3D.AxisAndRefDirProvision), nie parallel.</summary>
        IIfcAxis2Placement3D A3(double[] lage, double[]? achse = null, double[]? refx = null)
        {
            if (achse is not null)
            {
                var z = IfcExportGeometrie.Einheit(achse);
                var x = refx is null ? IfcExportGeometrie.Senkrecht(z) : IfcExportGeometrie.Einheit(refx);
                if (Math.Abs(IfcExportGeometrie.Skalar(z, x)) > 1 - 1e-9) x = IfcExportGeometrie.Senkrecht(z);
                bool standard = Math.Abs(z[2] - 1) < 1e-12 && Math.Abs(x[0] - 1) < 1e-12;
                if (!standard) return c.Axis2Placement3D(a => { a.Location = P(lage); a.Axis = D(z); a.RefDirection = D(x); });
            }
            return c.Axis2Placement3D(a => a.Location = P(lage));
        }

        IIfcLocalPlacement L(IIfcObjectPlacement? rel, IIfcAxis2Placement3D lage) => c.LocalPlacement(l => { l.PlacementRelTo = rel; l.RelativePlacement = lage; });

        IIfcAxis2Placement2D Null2() => null2 ??= c.Axis2Placement2D(a => a.Location = P2(0, 0));

        IIfcProfileDef Rechteck(double b, double h, double? wand) => wand is double w
            ? c.RectangleHollowProfileDef(x => { x.ProfileType = IfcProfileTypeEnum.AREA; x.XDim = R(b); x.YDim = R(h); x.WallThickness = R(w); x.Position = Null2(); })
            : c.RectangleProfileDef(x => { x.ProfileType = IfcProfileTypeEnum.AREA; x.XDim = R(b); x.YDim = R(h); x.Position = Null2(); });

        IIfcProfileDef Kreis(double r, double? wand) => wand is double w
            ? c.CircleHollowProfileDef(x => { x.ProfileType = IfcProfileTypeEnum.AREA; x.Radius = R(r); x.WallThickness = R(w); x.Position = Null2(); })
            : c.CircleProfileDef(x => { x.ProfileType = IfcProfileTypeEnum.AREA; x.Radius = R(r); x.Position = Null2(); });

        IIfcPolyline? Polylinie(List<double[]> ring)
        {
            var pts = new List<double[]>();
            foreach (var q in ring)
                if (pts.Count == 0 || Math.Abs(pts[^1][0] - q[0]) > 1e-4 || Math.Abs(pts[^1][1] - q[1]) > 1e-4) pts.Add(q);
            if (pts.Count > 1 && Math.Abs(pts[0][0] - pts[^1][0]) <= 1e-4 && Math.Abs(pts[0][1] - pts[^1][1]) <= 1e-4) pts.RemoveAt(pts.Count - 1);
            if (pts.Count < 3) return null;
            return c.Polyline(pl =>
            {
                var erster = P2(pts[0][0], pts[0][1]);
                pl.Points.Add(erster);
                foreach (var q in pts.Skip(1)) pl.Points.Add(P2(q[0], q[1]));
                pl.Points.Add(erster);   // geschlossen: erster Punkt am Ende (IfcArbitraryClosedProfileDef)
            });
        }

        IIfcRepresentationItem? Item(IfcExKoerper k)
        {
            switch (k)
            {
                case IfcExQuader q:
                    return c.ExtrudedAreaSolid(x => { x.SweptArea = Rechteck(q.B, q.H, q.Wand); x.Position = A3(q.Lage, q.Achse, q.RefX); x.ExtrudedDirection = D([0, 0, 1]); x.Depth = R(q.Tiefe); });
                case IfcExZylinder z:
                    return c.ExtrudedAreaSolid(x => { x.SweptArea = Kreis(z.R, z.Wand); x.Position = A3(z.Lage, z.Achse); x.ExtrudedDirection = D([0, 0, 1]); x.Depth = R(z.Tiefe); });
                case IfcExPrisma pr:
                {
                    var aussen = Polylinie(pr.Aussen);
                    if (aussen is null) return null;
                    var innen = pr.Loecher.Select(Polylinie).OfType<IIfcPolyline>().ToList();
                    IIfcProfileDef profil = innen.Count == 0
                        ? c.ArbitraryClosedProfileDef(x => { x.ProfileType = IfcProfileTypeEnum.AREA; x.OuterCurve = aussen; })
                        : c.ArbitraryProfileDefWithVoids(x => { x.ProfileType = IfcProfileTypeEnum.AREA; x.OuterCurve = aussen; foreach (var i in innen) x.InnerCurves.Add(i); });
                    return c.ExtrudedAreaSolid(x => { x.SweptArea = profil; x.Position = A3([0, 0, pr.Unten]); x.ExtrudedDirection = D([0, 0, 1]); x.Depth = R(pr.Hoehe); });
                }
                case IfcExNetz n:
                {
                    var punkte = n.Ecken.Select(P).ToList();
                    var schale = c.ClosedShell(s =>
                    {
                        foreach (var t in n.Dreiecke)
                        {
                            if (t.Length < 3 || t[0] == t[1] || t[1] == t[2] || t[0] == t[2]) continue;
                            var f = IfcExportGeometrie.Kreuz(IfcExportGeometrie.Minus(n.Ecken[t[1]], n.Ecken[t[0]]), IfcExportGeometrie.Minus(n.Ecken[t[2]], n.Ecken[t[0]]));
                            if (IfcExportGeometrie.Laenge(f) < 1e-6) continue;   // ausgeartetes Dreieck
                            s.CfsFaces.Add(c.Face(fa => fa.Bounds.Add(c.FaceOuterBound(b =>
                            {
                                b.Orientation = true;
                                b.Bound = c.PolyLoop(pl => { pl.Polygon.Add(punkte[t[0]]); pl.Polygon.Add(punkte[t[1]]); pl.Polygon.Add(punkte[t[2]]); });
                            }))));
                        }
                    });
                    return schale.CfsFaces.Count == 0 ? null : c.FacetedBrep(b => b.Outer = schale);
                }
                default: return null;
            }
        }

        IIfcProductDefinitionShape? Form(List<IfcExKoerper> koerper)
        {
            var items = koerper.Select(Item).OfType<IIfcRepresentationItem>().ToList();
            if (items.Count == 0) return null;
            var art = IfcExportGeometrie.Darstellungsart(koerper);
            return c.ProductDefinitionShape(sh => sh.Representations.Add(c.ShapeRepresentation(r =>
            {
                r.ContextOfItems = ctx; r.RepresentationIdentifier = "Body"; r.RepresentationType = art;
                foreach (var i in items) r.Items.Add(i);
            })));
        }

        // ---------------------------------------------------------- Werte und Psets

        static IIfcValue? Wert(IfcExWert w) => w.Leer ? null : w.Art switch
        {
            "laenge" => new Xbim.Ifc4.MeasureResource.IfcLengthMeasure(R(w.Zahl!.Value)),
            "positivlaenge" => new Xbim.Ifc4.MeasureResource.IfcPositiveLengthMeasure(R(w.Zahl!.Value)),
            "real" => new Xbim.Ifc4.MeasureResource.IfcReal(w.Zahl!.Value),
            "bool" => new Xbim.Ifc4.MeasureResource.IfcBoolean(w.Wahr!.Value),
            "text" => new Xbim.Ifc4.MeasureResource.IfcText(w.Text!),
            "kennung" => new Xbim.Ifc4.MeasureResource.IfcIdentifier(w.Text!),
            _ => new Xbim.Ifc4.MeasureResource.IfcLabel(w.Text!),
        };

        IIfcPropertySetDefinition? PsetBauen(IfcExPset ps, string gid)
        {
            var werte = ps.Werte.Where(w => !w.Leer).ToList();
            if (werte.Count == 0) return null;
            if (ps.Mengen)
                return c.ElementQuantity(q =>
                {
                    q.GlobalId = gid; q.Name = ps.Name;
                    foreach (var w in werte) q.Quantities.Add(c.QuantityLength(l => { l.Name = w.Name; l.LengthValue = R(w.Zahl!.Value); }));
                });
            return c.PropertySet(s =>
            {
                s.GlobalId = gid; s.Name = ps.Name;
                foreach (var w in werte) s.HasProperties.Add(c.PropertySingleValue(v => { v.Name = w.Name; v.NominalValue = Wert(w); }));
            });
        }

        void Psets(IIfcObjectDefinition o, string eigentuemer, IEnumerable<IfcExPset> psets)
        {
            foreach (var ps in psets)
            {
                var gid = G($"pset/{eigentuemer}/{ps.Name}");
                if (PsetBauen(ps, gid) is not { } def) continue;
                c.RelDefinesByProperties(r => { r.GlobalId = G($"rel/pset/{eigentuemer}/{ps.Name}"); r.RelatingPropertyDefinition = def; r.RelatedObjects.Add(o); });
            }
        }

        // ---------------------------------------------------------- Ablauf

        public void Alles()
        {
            var i = m.Instances;
            var beschr = $"HLKS-Modell KICAD, Stand {aus.Zeit.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture)}";
            IIfcProject projekt;
            if (ifc4)
            {
                var p4 = i.New<Xbim.Ifc4.Kernel.IfcProject>(p => { p.GlobalId = G("export/projekt"); p.Name = aus.ProjektName; p.Description = beschr; });
                p4.Initialize(ProjectUnits.SIUnitsUK);
                projekt = p4;
            }
            else
            {
                var p2 = i.New<Xbim.Ifc2x3.Kernel.IfcProject>(p => { p.GlobalId = G("export/projekt"); p.Name = aus.ProjektName; p.Description = beschr; });
                p2.Initialize(ProjectUnits.SIUnitsUK);
                projekt = p2;
            }
            ctx = i.OfType<IIfcGeometricRepresentationContext>().First();
            Gliederung(projekt);
            Kartenbezug();
            Typen();
            Produkte();
            Gruppen();
            Verbindungen();
            Klassifikation();
            Material();
        }

        void Gliederung(IIfcProject projekt)
        {
            var site = c.Site(s => { s.GlobalId = G("export/gelaende"); s.Name = "Gelaende"; s.ObjectPlacement = L(null, A3([0, 0, 0])); s.CompositionType = IfcElementCompositionEnum.ELEMENT; });
            c.RelAggregates(r => { r.GlobalId = G("rel/aggregat/projekt"); r.RelatingObject = projekt; r.RelatedObjects.Add(site); });
            var b = aus.Bezug;
            double w = (b?.Drehung ?? 0) * Math.PI / 180;
            var lageGeb = b is null ? A3([0, 0, 0]) : A3([b.X, b.Y, b.Z], b.Drehung == 0 ? null : [0, 0, 1], b.Drehung == 0 ? null : [Math.Cos(w), Math.Sin(w), 0]);
            var gebs = new List<IIfcBuilding>();
            foreach (var g in aus.Gebaeude)
            {
                var lg = L(site.ObjectPlacement, lageGeb);
                var geb = c.Building(x =>
                {
                    x.GlobalId = g.GlobalId; x.Name = g.Name; x.Description = g.Beschreibung; x.ObjectPlacement = lg; x.CompositionType = IfcElementCompositionEnum.ELEMENT;
                });
                gebaeude[g.GlobalId] = geb; gebaeudeLage[g.GlobalId] = lg; objekte[g.GlobalId] = geb; gebs.Add(geb);
                if (g.Element is { } ge)
                    Psets(geb, g.GlobalId, [new IfcExPset(IfcExport.PsetKicad, [new("Kennzeichen", "kennung", ge.Kennzeichen), new("Klasse", "label", ge.Klasse), new("ElementId", "kennung", ge.Id)])]);
            }
            c.RelAggregates(r => { r.GlobalId = G("rel/aggregat/gelaende"); r.RelatingObject = site; foreach (var x in gebs) r.RelatedObjects.Add(x); });
            foreach (var gruppe in aus.Geschosse.GroupBy(g => g.GebaeudeGid))
            {
                var liste = new List<IIfcBuildingStorey>();
                foreach (var g in gruppe)
                {
                    var lg = L(gebaeudeLage[g.GebaeudeGid], A3([0, 0, g.Kote]));
                    var s = c.BuildingStorey(x =>
                    {
                        x.GlobalId = g.GlobalId; x.Name = g.Kennzeichen;
                        if (!string.IsNullOrWhiteSpace(g.Name) && g.Name != g.Kennzeichen) x.LongName = g.Name;
                        x.Elevation = R(g.Kote); x.ObjectPlacement = lg; x.CompositionType = IfcElementCompositionEnum.ELEMENT;
                    });
                    geschossLage[g.Element.Id] = lg; geschosse[g.Element.Id] = s; objekte[g.GlobalId] = s; liste.Add(s);
                    var eig = new List<IfcExWert> { new("Kennzeichen", "kennung", g.Kennzeichen), new("Klasse", "label", "Geschoss"), new("Kote", "laenge", Zahl: g.Kote), new("ElementId", "kennung", g.Element.Id) };
                    if (g.Hoehe is double h) eig.Add(new("Hoehe", "laenge", Zahl: h));
                    Psets(s, g.GlobalId, [new IfcExPset(IfcExport.PsetKicad, eig)]);
                }
                c.RelAggregates(r => { r.GlobalId = G("rel/aggregat/" + gruppe.Key); r.RelatingObject = gebaeude[gruppe.Key]; foreach (var s in liste) r.RelatedObjects.Add(s); });
            }
        }

        /// <summary>IFC4: IfcMapConversion vom Modellkontext zu den Landeskoordinaten (IfcProjectedCRS, MapUnit mm).</summary>
        void Kartenbezug()
        {
            if (!ifc4 || aus.Karte is not { } k) return;
            var mm = c.SIUnit(u => { u.UnitType = IfcUnitEnum.LENGTHUNIT; u.Prefix = IfcSIPrefix.MILLI; u.Name = IfcSIUnitName.METRE; });
            var crs = (IIfcProjectedCRS)Neu("IfcProjectedCRS");
            crs.Name = k.Bezugssystem; crs.MapUnit = mm;
            var mc = (IIfcMapConversion)Neu("IfcMapConversion");
            double w = k.Drehung * Math.PI / 180;
            mc.SourceCRS = (IIfcCoordinateReferenceSystemSelect)ctx; mc.TargetCRS = crs;
            mc.Eastings = R(k.OstMm); mc.Northings = R(k.NordMm); mc.OrthogonalHeight = R(k.HoeheMm);
            mc.XAxisAbscissa = Math.Round(Math.Cos(w), 12) + 0.0; mc.XAxisOrdinate = Math.Round(Math.Sin(w), 12) + 0.0; mc.Scale = 1.0;
        }

        readonly Dictionary<string, IIfcTypeObject> typObjekte = new(StringComparer.Ordinal);

        void Typen()
        {
            foreach (var t in aus.Typen)
            {
                var o = (IIfcTypeObject)Neu(t.Entitaet);
                o.GlobalId = t.GlobalId; o.Name = t.Name;
                Aufzaehlung(o, "PredefinedType", t.PredefinedType);
                if (t.ElementType is not null && o is IIfcElementType et) et.ElementType = t.ElementType;
                Pflichtaufzaehlungen(o);
                foreach (var ps in t.Psets)
                    if (PsetBauen(ps, G($"pset/{t.GlobalId}/{ps.Name}")) is IIfcPropertySetDefinition def) o.HasPropertySets.Add(def);
                typObjekte[t.Schluessel] = o;
            }
        }

        void Produkte()
        {
            var enthalten = new Dictionary<string, List<IIfcProduct>>(StringComparer.Ordinal);   // Geschoss-Id bzw. "" (Gebäude)
            var raeume = new Dictionary<string, List<IIfcSpace>>(StringComparer.Ordinal);
            var jeTyp = new Dictionary<string, List<IIfcObject>>(StringComparer.Ordinal);
            void Enthalten(string? geschoss, IIfcProduct p)
            {
                var k = geschoss is not null && geschosse.ContainsKey(geschoss) ? geschoss : "";
                if (!enthalten.TryGetValue(k, out var l)) enthalten[k] = l = [];
                l.Add(p);
            }
            foreach (var x in aus.Produkte)
            {
                var kote = x.GeschossId is { } gid && geschosse.ContainsKey(gid) ? aus.Geschosse.First(g => g.Element.Id == gid).Kote : 0;
                var rel = x.GeschossId is { } g2 && geschossLage.TryGetValue(g2, out var lg) ? (IIfcObjectPlacement)lg : gebaeudeLage[aus.Gebaeude[0].GlobalId];
                var lage = L(rel, A3([x.Ursprung[0], x.Ursprung[1], x.Ursprung[2] - kote]));
                var p = (IIfcProduct)Neu(x.Entitaet);
                p.GlobalId = x.GlobalId; p.Name = x.Name; p.Description = x.Description;
                if (x.ObjectType is not null) p.ObjectType = x.ObjectType;
                if (p is IIfcElement el && x.Tag is not null) el.Tag = x.Tag;
                Aufzaehlung(p, "PredefinedType", x.PredefinedType);
                p.ObjectPlacement = lage;
                if (Form(x.Koerper) is { } form) p.Representation = form;
                objekte[x.GlobalId] = p;
                if (x.IstRaum && p is IIfcSpace sp)
                {
                    if (!string.IsNullOrWhiteSpace(x.Description)) sp.LongName = x.Description;
                    sp.CompositionType = IfcElementCompositionEnum.ELEMENT;
                    Aufzaehlung(sp, "InteriorOrExteriorSpace", ifc4 ? null : "INTERNAL");   // IFC2X3: Pflichtattribut
                    var k = x.GeschossId is { } rg && geschosse.ContainsKey(rg) ? rg : null;
                    if (k is null) Enthalten(null, sp);
                    else { if (!raeume.TryGetValue(k, out var rl)) raeume[k] = rl = []; rl.Add(sp); }
                }
                else Enthalten(x.GeschossId, p);
                Psets(p, x.GlobalId, x.Psets);
                if (x.Typ is { } t && typObjekte.ContainsKey(t.Schluessel))
                {
                    if (!jeTyp.TryGetValue(t.Schluessel, out var tl)) jeTyp[t.Schluessel] = tl = [];
                    tl.Add(p);
                }
                if (x.Ports.Count > 0 && p is IIfcElement trager) Ports(x, trager, lage);
                if (x.Daemmung is { } d && p is IIfcElement belegt)
                {
                    var cov = c.Covering(cv =>
                    {
                        cv.GlobalId = d.GlobalId; cv.Name = "Daemmung " + x.Element.Kennzeichen;
                        cv.Description = $"{(string.IsNullOrWhiteSpace(d.Art) ? "Dämmung" : d.Art)} {IfcExportAufbau.Zahl(d.Dicke)} mm";
                        cv.PredefinedType = IfcCoveringTypeEnum.INSULATION;
                        cv.ObjectPlacement = L(rel, A3([x.Ursprung[0], x.Ursprung[1], x.Ursprung[2] - kote]));
                        if (Form(d.Koerper) is { } f) cv.Representation = f;
                    });
                    c.RelCoversBldgElements(r => { r.GlobalId = G("rel/daemmung/" + x.Element.Id); r.RelatingBuildingElement = belegt; r.RelatedCoverings.Add(cov); });
                    Enthalten(x.GeschossId, cov);
                    Psets(cov, d.GlobalId, [new IfcExPset(IfcExport.PsetKicad,
                    [
                        new("Kennzeichen", "kennung", x.Element.Kennzeichen), new("Klasse", "label", "Daemmung"), new("Daemmdicke", "positivlaenge", Zahl: d.Dicke),
                        new("Daemmung", "label", d.Art), new("ElementId", "kennung", x.Element.Id),
                    ])]);
                }
            }
            foreach (var (k, l) in enthalten)
                c.RelContainedInSpatialStructure(r =>
                {
                    r.GlobalId = G("rel/enthalten/" + (k.Length == 0 ? "gebaeude" : k));
                    r.RelatingStructure = k.Length == 0 ? gebaeude[aus.Gebaeude[0].GlobalId] : geschosse[k];
                    foreach (var x in l) r.RelatedElements.Add(x);
                });
            foreach (var (k, l) in raeume)
                c.RelAggregates(r => { r.GlobalId = G("rel/raeume/" + k); r.RelatingObject = geschosse[k]; foreach (var s in l) r.RelatedObjects.Add(s); });
            foreach (var (schluessel, l) in jeTyp)
                c.RelDefinesByType(r => { r.GlobalId = G("rel/typ/" + schluessel); r.RelatingType = typObjekte[schluessel]; foreach (var o in l) r.RelatedObjects.Add(o); });
        }

        readonly Dictionary<string, IIfcDistributionPort> ports = new(StringComparer.Ordinal);

        void Ports(IfcExProdukt x, IIfcElement element, IIfcLocalPlacement lage)
        {
            var liste = new List<IIfcDistributionPort>();
            foreach (var pt in x.Ports)
            {
                var port = c.DistributionPort(d =>
                {
                    d.GlobalId = pt.GlobalId; d.Name = pt.Name; d.Description = pt.Medium;
                    d.ObjectPlacement = L(lage, A3(IfcExportGeometrie.Minus(pt.Lage, x.Ursprung), pt.Richtung));
                });
                Aufzaehlung(port, "FlowDirection", pt.Fluss, pflicht: true);
                if (ifc4)
                {
                    Aufzaehlung(port, "PredefinedType", pt.PortTyp ?? "NOTDEFINED", pflicht: true);
                    Aufzaehlung(port, "SystemType", pt.SystemTyp ?? "NOTDEFINED", pflicht: true);
                }
                Psets(port, pt.GlobalId, pt.Psets);
                ports[pt.GlobalId] = port;
                liste.Add(port);
            }
            if (ifc4)
                c.RelNests(r => { r.GlobalId = G("rel/ports/" + x.Element.Id); r.RelatingObject = element; foreach (var p in liste) r.RelatedObjects.Add(p); });
            else if (element is IIfcDistributionElement de)
                foreach (var p in liste)
                    c.RelConnectsPortToElement(r => { r.GlobalId = G("rel/port/" + p.GlobalId); r.RelatingPort = p; r.RelatedElement = de; });
        }

        void Verbindungen()
        {
            foreach (var v in aus.Verbindungen)
                if (ports.TryGetValue(v.VonPort, out var a) && ports.TryGetValue(v.NachPort, out var b))
                    c.RelConnectsPorts(r => { r.GlobalId = v.GlobalId; r.RelatingPort = a; r.RelatedPort = b; });
        }

        void Gruppen()
        {
            var gruppen = new List<(IfcExGruppe Daten, IIfcGroup Gruppe)>();
            foreach (var gr in aus.Gruppen)
            {
                var g = (IIfcGroup)Neu(gr.Entitaet);
                g.GlobalId = gr.GlobalId; g.Name = gr.Name; g.Description = gr.Description;
                if (gr.ObjectType is not null) g.ObjectType = gr.ObjectType;
                Aufzaehlung(g, "PredefinedType", gr.PredefinedType);
                if (gr.LongName is not null && Eigenschaft(g, "LongName") is { } ln) ln.SetValue(g, new Xbim.Ifc4.MeasureResource.IfcLabel(gr.LongName));
                objekte[gr.GlobalId] = g;
                gruppen.Add((gr, g));
                Psets(g, gr.GlobalId, gr.Psets);
            }
            foreach (var (gr, g) in gruppen)
            {
                var mitglieder = gr.Mitglieder.Select(x => objekte.GetValueOrDefault(x)).OfType<IIfcObjectDefinition>().ToList();
                if (mitglieder.Count == 0) continue;
                c.RelAssignsToGroup(r => { r.GlobalId = G("rel/gruppe/" + gr.GlobalId); r.RelatingGroup = g; foreach (var x in mitglieder) r.RelatedObjects.Add(x); });
            }
            foreach (var teil in gruppen.Where(x => x.Daten.Teil is not null).GroupBy(x => x.Daten.Teil!))
                if (objekte.GetValueOrDefault(teil.Key) is { } sys)
                    c.RelAggregates(r => { r.GlobalId = G("rel/straenge/" + teil.Key); r.RelatingObject = sys; foreach (var (_, g) in teil) r.RelatedObjects.Add(g); });
        }

        void Klassifikation()
        {
            var jeKode = aus.Produkte.Where(x => x.Bkp is not null).GroupBy(x => x.Bkp!).OrderBy(g => g.Key, StringComparer.Ordinal).ToList();
            if (jeKode.Count == 0) return;
            var k = c.Classification(x =>
            {
                x.Name = aus.KlassifikationName;
                if (aus.KlassifikationQuelle is { } q) x.Source = q;
                if (!ifc4)
                {
                    // IFC2X3: Source und Edition sind Pflicht; die Ausgabe des BKP nennt ifc.json nicht – nicht erfinden
                    if (aus.KlassifikationQuelle is null) x.Source = aus.KlassifikationName;
                    x.Edition = IfcExport.KlassifikationOhneAusgabe;
                }
            });
            foreach (var g in jeKode)
            {
                var r = c.ClassificationReference(x => { x.Identification = g.Key; x.Name = g.Key; x.ReferencedSource = k; });
                c.RelAssociatesClassification(rel =>
                {
                    rel.GlobalId = G($"rel/klassifikation/{aus.KlassifikationName}/{g.Key}"); rel.Name = aus.KlassifikationName; rel.RelatingClassification = r;
                    foreach (var x in g) if (objekte.GetValueOrDefault(x.GlobalId) is IIfcDefinitionSelect o) rel.RelatedObjects.Add(o);
                });
            }
        }

        readonly Dictionary<string, IIfcMaterial> materialien = new(StringComparer.Ordinal);

        IIfcMaterial MaterialVon(string name)
        {
            if (!materialien.TryGetValue(name, out var mt)) materialien[name] = mt = c.Material(x => x.Name = name);
            return mt;
        }

        void Material()
        {
            // Wände: eine Schicht in Wanddicke, Bezugslinie = Wandachse (Versatz −Dicke/2, Richtung AXIS2 positiv); IFC2X3 IfcWallStandardCase WR1
            foreach (var g in aus.Produkte.Where(x => x.Schicht is not null).GroupBy(x => x.Schicht!.Value).OrderBy(g => g.Key.Material, StringComparer.Ordinal).ThenBy(g => g.Key.Dicke))
            {
                var (name, dicke) = g.Key;
                var satz = c.MaterialLayerSet(s =>
                {
                    s.LayerSetName = $"{name} {IfcExportAufbau.Zahl(dicke)} mm";
                    s.MaterialLayers.Add(c.MaterialLayer(l => { l.Material = MaterialVon(name); l.LayerThickness = R(dicke); }));
                });
                var nutzung = c.MaterialLayerSetUsage(u =>
                {
                    u.ForLayerSet = satz; u.LayerSetDirection = IfcLayerSetDirectionEnum.AXIS2; u.DirectionSense = IfcDirectionSenseEnum.POSITIVE;
                    u.OffsetFromReferenceLine = R(-dicke / 2);
                });
                c.RelAssociatesMaterial(r =>
                {
                    r.GlobalId = G($"rel/schicht/{name}/{IfcExportAufbau.Zahl(dicke)}"); r.RelatingMaterial = nutzung;
                    foreach (var x in g) if (objekte.GetValueOrDefault(x.GlobalId) is IIfcDefinitionSelect o) r.RelatedObjects.Add(o);
                });
            }
            foreach (var g in aus.Produkte.Where(x => x.Material is not null).GroupBy(x => x.Material!).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var mat = MaterialVon(g.Key);
                c.RelAssociatesMaterial(r =>
                {
                    r.GlobalId = G("rel/material/" + g.Key); r.RelatingMaterial = mat;
                    foreach (var x in g) if (objekte.GetValueOrDefault(x.GlobalId) is IIfcDefinitionSelect o) r.RelatedObjects.Add(o);
                });
            }
        }
    }
}
