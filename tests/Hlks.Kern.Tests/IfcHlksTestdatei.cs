// Synthetische HLKS-IFC-Dateien für die Tests des HLKS-Übertrags (Modul 1b, B2), erzeugt mit xBIM in IFC4 und IFC2x3.
// Grundnetz im EG (Projekt in mm, Kote 0): WC → Rohr 2 → Bogen → Rohr 1, Schmutzwasser fliesst vom WC weg.
//   WC (Lage 2000/2500/0): Port sw (SOURCE) bei (2000, 2000, 500), Port kw (SINK) bei (2100, 2300, 800), offen
//   Rohr 2: (2000, 2000, 500) → (2000, 100, 500), Kreisprofil r 55 (Aussendurchmesser 110), ohne Nennweite in Psets
//   Bogen (Lage 2000/0/500): Ports b bei (2000, 100, 500) Achse y und a bei (1900, 0, 500) Achse x – 90°
//   Rohr 1: (1900, 0, 500) → (x1, 0, 500), Pset_PipeSegmentTypeCommon.NominalDiameter 100 (Längenmass, mm)
// Wahlweise: Kanal 400 × 200 im System Zuluft, Revit-Formteile ohne PredefinedType (T-Stück, gerades, geknicktes),
// Proxy «Heizkreisverteiler» mit 8 Ports, Rohr im System «Abgas» (kein Systemtyp), Ventil, Nova-Stil (Ports über
// IfcRelConnectsPortToElement, ohne FlowDirection, alle Produkte auf dem Geschossursprung mit absolutem Körper).
// GlobalIds sind aus dem Namen abgeleitet: zwei Fassungen derselben Datei gleichen ab.

using System.Security.Cryptography;
using System.Text;
using Xbim.Common;
using Xbim.Common.Step21;
using Xbim.Ifc;
using Xbim.IO;

namespace Hlks.Kern.Tests;

public sealed record IfcHlksSpec
{
    /// <summary>IFC4: Ports über IfcRelNests (true) oder wie Nova über IfcRelConnectsPortToElement (false).</summary>
    public bool PortsUeberNests { get; init; } = true;
    /// <summary>FlowDirection SOURCE/SINK an den Ports; false = NOTDEFINED (Nova).</summary>
    public bool MitRichtung { get; init; } = true;
    public bool MitDn { get; init; } = true;
    /// <summary>Aussendurchmesser (mm) als Pset «Abmessungen.Aussendurchmesser» an Rohr 2 (Revit-Stil, deutsch); weicht absichtlich vom Kreisprofil (110) ab.</summary>
    public double? DaPset { get; init; }
    /// <summary>Systeme der Schmutzwasserleitung; zwei Namen = Revit-Teile (Rohr 1 und Bogen im ersten, Rohr 2 und WC im zweiten).</summary>
    public string[] SwSysteme { get; init; } = ["SW Schmutzwasser"];
    /// <summary>Alle Produkte auf dem Geschossursprung, Körper in absoluten Koordinaten (Trimble Nova).</summary>
    public bool NovaLagen { get; init; }
    public bool MitKanal { get; init; } = true;
    public bool MitFormteilen { get; init; }
    public bool MitVerteiler { get; init; }
    public bool MitAbgas { get; init; }
    public bool MitVentil { get; init; }
    /// <summary>x des freien Endes von Rohr 1.</summary>
    public double Rohr1Ende { get; init; }
    /// <summary>WC ohne Ports und ohne PredefinedType (Revit-Architekturobjekt) und mit neutralem Namen.</summary>
    public bool WcOhnePorts { get; init; }
    /// <summary>Nur IFC4: Strang (IfcDistributionCircuit) «Strang SW 1» im ersten Schmutzwassersystem mit Rohr 1 und Bogen.</summary>
    public bool MitStrang { get; init; }
}

public static class IfcHlksTestdatei
{
    /// <summary>Schreibt die Testdatei; schema = IFC4 | IFC2X3.</summary>
    public static string Schreiben(string schema, string datei, IfcHlksSpec s)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(datei)!);
        using IBau b = schema == "IFC4" ? new Bau4(s.PortsUeberNests) : new Bau2x3();
        string? fluss(string f) => s.MitRichtung ? f : null;

        // WC
        var wcLage = new[] { 2000.0, 2500, 0 };
        var wc = b.Element("wc", s.WcOhnePorts ? "Objekt-109" : "WC Vorwand", s.NovaLagen ? null : wcLage, new Koerper("quader", [2000, 2500, 0], null, 400, 700, 400), s.WcOhnePorts ? "NOTDEFINED" : null);
        var wcSw = s.WcOhnePorts ? null : b.Port(wc, "CP1", [2000, 2000, 500], [0, -1, 0], fluss("SOURCE"));
        if (!s.WcOhnePorts) b.Port(wc, "CP2", [2100, 2300, 800], [0, 0, 1], fluss("SINK"));

        // Rohr 2 (ohne Nennweite), Bogen, Rohr 1 (mit Nennweite)
        var r2 = b.Element("rohr", "Rohr 2", null, new Koerper("kreis", [2000, 2000, 500], [2000, 100, 500], 55, 0, 0), "RIGIDSEGMENT");
        var r2a = b.Port(r2, "CP1", [2000, 2000, 500], [0, -1, 0], fluss("SINK"));
        var r2e = b.Port(r2, "CP2", [2000, 100, 500], [0, -1, 0], fluss("SOURCE"));
        var bo = b.Element("formteil", "Rohrbogen 90", s.NovaLagen ? null : [2000, 0, 500], new Koerper("quader", [1950, -50, 450], null, 100, 100, 100), "BEND");
        var bob = b.Port(bo, "CP1", [2000, 100, 500], [0, -1, 0], fluss("SINK"));
        var boa = b.Port(bo, "CP2", [1900, 0, 500], [-1, 0, 0], fluss("SOURCE"));
        var r1 = b.Element("rohr", "Rohr 1", null, new Koerper("kreis", [1900, 0, 500], [s.Rohr1Ende, 0, 500], 55, 0, 0), "RIGIDSEGMENT");
        var r1a = b.Port(r1, "CP1", [1900, 0, 500], [-1, 0, 0], fluss("SINK"));
        b.Port(r1, "CP2", [s.Rohr1Ende, 0, 500], [-1, 0, 0], fluss("SOURCE"));
        if (s.DaPset is double dap) b.Pset(r2, "Abmessungen", "Aussendurchmesser", dap, true);
        if (s.MitDn) b.Pset(r1, "Pset_PipeSegmentTypeCommon", "NominalDiameter", 100, true);
        if (wcSw is not null) b.Verbinden(wcSw, r2a);
        b.Verbinden(r2e, bob);
        b.Verbinden(boa, r1a);

        var sw = new List<object> { r1, bo, r2, wc };
        if (s.SwSysteme.Length >= 2)
        {
            b.System(s.SwSysteme[0], "WASTEWATER", [r1, bo]);
            b.System(s.SwSysteme[1], "WASTEWATER", [r2, wc]);
        }
        else b.System(s.SwSysteme[0], "WASTEWATER", sw);
        if (s.MitStrang) b.Strang("Strang SW 1", s.SwSysteme[0], [r1, bo]);

        if (s.MitKanal)
        {
            // Kanal 400 breit (waagrecht) × 200 hoch, entlang x
            var ka = b.Element("kanal", "Kanal Zuluft", null, new Koerper("rechteck", [0, 4000, 2500], [3000, 4000, 2500], 400, 200, 0), "RIGIDSEGMENT");
            b.Port(ka, "CP1", [0, 4000, 2500], [1, 0, 0], fluss("SINK"));
            b.Port(ka, "CP2", [3000, 4000, 2500], [1, 0, 0], fluss("SOURCE"));
            b.System("ZUL Zuluft", "VENTILATION", [ka]);
        }
        if (s.MitFormteilen)
        {
            // Revit: PredefinedType NOTDEFINED – die Lage der Ports entscheidet
            var t = b.Element("formteil", "Fitting A", s.NovaLagen ? null : [5000, 0, 500], new Koerper("quader", [4900, -50, 450], null, 200, 150, 100), "NOTDEFINED");
            b.Port(t, "P0", [4900, 0, 500], [1, 0, 0], null);
            var tc = b.Port(t, "P1", [5000, 100, 500], [0, 1, 0], null);
            b.Port(t, "P2", [5100, 0, 500], [1, 0, 0], null);
            // Abzweigrohr am Port P1: muss am Anschluss «c» des T-Stücks hängen
            var az = b.Element("rohr", "Abzweigrohr", null, new Koerper("kreis", [5000, 100, 500], [5000, 600, 500], 20, 0, 0), "RIGIDSEGMENT");
            b.Verbinden(b.Port(az, "CP1", [5000, 100, 500], [0, 1, 0], null), tc);
            b.Port(az, "CP2", [5000, 600, 500], [0, 1, 0], null);
            b.System("SW Formteile", "WASTEWATER", [az]);
            var g = b.Element("formteil", "Fitting B", s.NovaLagen ? null : [6000, 0, 500], new Koerper("quader", [5950, -50, 450], null, 100, 100, 100), "NOTDEFINED");
            b.Port(g, "P0", [5950, 0, 500], [1, 0, 0], null);
            b.Port(g, "P1", [6050, 0, 500], [1, 0, 0], null);
            var k = b.Element("formteil", "Fitting C", s.NovaLagen ? null : [7000, 0, 500], new Koerper("quader", [6950, -50, 450], null, 100, 100, 100), "NOTDEFINED");
            b.Port(k, "P0", [6950, 0, 500], [1, 0, 0], null);
            b.Port(k, "P1", [7000, 50, 500], [0, 1, 0], null);
            b.System("SW Formteile", "WASTEWATER", [t, g, k]);
        }
        if (s.MitVerteiler)
        {
            var v = b.Element("proxy", "Heizkreisverteiler 8", s.NovaLagen ? null : [8000, 0, 800], new Koerper("quader", [8000, 0, 600], null, 600, 150, 400), null);
            for (int i = 0; i < 8; i++) b.Port(v, $"CP{i + 1}", [7800 + i * 60, 0, 600], [0, 0, -1], null);
        }
        if (s.MitAbgas)
        {
            var ab = b.Element("rohr", "Abgasrohr", null, new Koerper("kreis", [0, 6000, 2000], [0, 6000, 4000], 50, 0, 0), "RIGIDSEGMENT");
            b.Port(ab, "CP1", [0, 6000, 2000], [0, 0, 1], fluss("SINK"));
            b.Port(ab, "CP2", [0, 6000, 4000], [0, 0, 1], fluss("SOURCE"));
            b.System("Abgas", null, [ab]);
        }
        if (s.MitVentil)
        {
            var ve = b.Element("ventil", "Absperrventil", s.NovaLagen ? null : [3000, 0, 500], new Koerper("quader", [3000, 0, 450], null, 100, 100, 100), "ISOLATING");
            b.Port(ve, "CP1", [2950, 0, 500], [1, 0, 0], fluss("SINK"));
            b.Port(ve, "CP2", [3050, 0, 500], [1, 0, 0], fluss("SOURCE"));
            b.System(s.SwSysteme[0], "WASTEWATER", [ve]);
        }
        b.Speichern(datei);
        return datei;
    }

    /// <summary>Temp-Ordner je Test (die Dateien sind klein; die Tests räumen auf).</summary>
    public static string Ordner() => Path.Combine(Path.GetTempPath(), "hlks_b2_tests", Guid.NewGuid().ToString("N"));

    /// <summary>GlobalId aus einem Namen (gleicher Name = gleiche GlobalId in jeder Fassung).</summary>
    internal static Guid GuidAus(string name) => new(MD5.HashData(Encoding.UTF8.GetBytes("hlks-b2:" + name)));

    /// <summary>Körper: kreis/rechteck = Extrusion von «Von» nach «Nach» (Radius A bzw. Breite A × Höhe B); quader = A × B × H
    /// mit Ecke bzw. Mitte unten in «Von» (x, y Mitte; z unten).</summary>
    public sealed record Koerper(string Art, double[] Von, double[]? Nach, double A, double B, double H);

    interface IBau : IDisposable
    {
        object Element(string art, string name, double[]? lage, Koerper k, string? predefined);
        object Port(object element, string name, double[] lage, double[] achse, string? fluss);
        void Verbinden(object a, object b);
        void System(string name, string? ifc4Typ, IEnumerable<object> mitglieder);
        void Strang(string name, string system, IEnumerable<object> mitglieder);
        void Pset(object element, string pset, string name, double wert, bool laenge);
        void Speichern(string datei);
    }

    static XbimEditorCredentials Wer => new()
    {
        ApplicationDevelopersName = "H+K Planungs AG", ApplicationFullName = "HLKS-CAD Testdatei B2", ApplicationIdentifier = "hlks-test-b2",
        ApplicationVersion = "1.0", EditorsFamilyName = "Test", EditorsGivenName = "HLKS", EditorsOrganisationName = "H+K Planungs AG",
    };

    static double[] Senkrecht(double[] z) => Math.Abs(z[2]) < 0.9 ? Norm([-z[1], z[0], 0]) : [1, 0, 0];
    static double[] Norm(double[] v) { var l = Math.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]); return [v[0] / l, v[1] / l, v[2] / l]; }
    static double[] Minus(double[] a, double[]? b) => b is null ? a : [a[0] - b[0], a[1] - b[1], a[2] - b[2]];

    // ------------------------------------------------------------------ IFC4

    sealed class Bau4 : IBau
    {
        readonly IfcStore m;
        readonly ITransaction t;
        readonly bool nests;
        readonly Xbim.Ifc4.RepresentationResource.IfcGeometricRepresentationContext ctx;
        readonly Xbim.Ifc4.ProductExtension.IfcBuildingStorey eg;
        readonly Xbim.Ifc4.GeometricConstraintResource.IfcLocalPlacement lEg;
        readonly List<Xbim.Ifc4.Kernel.IfcProduct> inEg = [];
        readonly Dictionary<Xbim.Ifc4.Kernel.IfcProduct, double[]?> lagen = [];
        readonly Dictionary<string, Xbim.Ifc4.SharedBldgServiceElements.IfcDistributionSystem> systeme = [];
        IEntityCollection I => m.Instances;

        public Bau4(bool nests)
        {
            this.nests = nests;
            m = IfcStore.Create(Wer, XbimSchemaVersion.Ifc4, XbimStoreType.InMemoryModel);
            t = m.BeginTransaction("Testdatei B2");
            var projekt = I.New<Xbim.Ifc4.Kernel.IfcProject>(p => p.Name = "Testprojekt HLKS");
            projekt.Initialize(ProjectUnits.SIUnitsUK);   // Länge in mm
            ctx = I.OfType<Xbim.Ifc4.RepresentationResource.IfcGeometricRepresentationContext>().First();
            var lSite = L(null, [0, 0, 0]);
            var site = I.New<Xbim.Ifc4.ProductExtension.IfcSite>(x => { x.Name = "Gelaende"; x.ObjectPlacement = lSite; });
            var lGeb = L(lSite, [0, 0, 0]);
            var geb = I.New<Xbim.Ifc4.ProductExtension.IfcBuilding>(x => { x.Name = "Testhaus"; x.ObjectPlacement = lGeb; });
            lEg = L(lGeb, [0, 0, 0]);
            eg = I.New<Xbim.Ifc4.ProductExtension.IfcBuildingStorey>(x => { x.Name = "Erdgeschoss"; x.Elevation = 0; x.ObjectPlacement = lEg; });
            var og = I.New<Xbim.Ifc4.ProductExtension.IfcBuildingStorey>(x => { x.Name = "1. Obergeschoss"; x.Elevation = 3000; x.ObjectPlacement = L(lGeb, [0, 0, 3000]); });
            I.New<Xbim.Ifc4.Kernel.IfcRelAggregates>(r => { r.RelatingObject = projekt; r.RelatedObjects.Add(site); });
            I.New<Xbim.Ifc4.Kernel.IfcRelAggregates>(r => { r.RelatingObject = site; r.RelatedObjects.Add(geb); });
            I.New<Xbim.Ifc4.Kernel.IfcRelAggregates>(r => { r.RelatingObject = geb; r.RelatedObjects.Add(eg); r.RelatedObjects.Add(og); });
        }

        Xbim.Ifc4.GeometryResource.IfcCartesianPoint P(double[] c) => I.New<Xbim.Ifc4.GeometryResource.IfcCartesianPoint>(p => p.SetXYZ(c[0], c[1], c[2]));
        Xbim.Ifc4.GeometryResource.IfcDirection D(double[] c) => I.New<Xbim.Ifc4.GeometryResource.IfcDirection>(p => p.SetXYZ(c[0], c[1], c[2]));
        Xbim.Ifc4.GeometryResource.IfcAxis2Placement3D A(double[] o, double[]? z = null) => I.New<Xbim.Ifc4.GeometryResource.IfcAxis2Placement3D>(a =>
        {
            a.Location = P(o);
            if (z is not null) { a.Axis = D(Norm(z)); a.RefDirection = D(Senkrecht(Norm(z))); }
        });
        Xbim.Ifc4.GeometricConstraintResource.IfcLocalPlacement L(Xbim.Ifc4.GeometricConstraintResource.IfcLocalPlacement? rel, double[] o, double[]? z = null) =>
            I.New<Xbim.Ifc4.GeometricConstraintResource.IfcLocalPlacement>(l => { l.PlacementRelTo = rel; l.RelativePlacement = A(o, z); });

        public object Element(string art, string name, double[]? lage, Koerper k, string? pt)
        {
            Xbim.Ifc4.Kernel.IfcProduct e = art switch
            {
                "rohr" => I.New<Xbim.Ifc4.HvacDomain.IfcPipeSegment>(x => x.PredefinedType = Enum.Parse<Xbim.Ifc4.Interfaces.IfcPipeSegmentTypeEnum>(pt!)),
                "kanal" => I.New<Xbim.Ifc4.HvacDomain.IfcDuctSegment>(x => x.PredefinedType = Enum.Parse<Xbim.Ifc4.Interfaces.IfcDuctSegmentTypeEnum>(pt!)),
                "formteil" => I.New<Xbim.Ifc4.HvacDomain.IfcPipeFitting>(x => x.PredefinedType = Enum.Parse<Xbim.Ifc4.Interfaces.IfcPipeFittingTypeEnum>(pt!)),
                "wc" => I.New<Xbim.Ifc4.PlumbingFireProtectionDomain.IfcSanitaryTerminal>(x => x.PredefinedType = Enum.Parse<Xbim.Ifc4.Interfaces.IfcSanitaryTerminalTypeEnum>(pt ?? "TOILETPAN")),
                "ventil" => I.New<Xbim.Ifc4.HvacDomain.IfcValve>(x => x.PredefinedType = Enum.Parse<Xbim.Ifc4.Interfaces.IfcValveTypeEnum>(pt!)),
                "proxy" => I.New<Xbim.Ifc4.SharedBldgElements.IfcBuildingElementProxy>(),
                _ => throw new ArgumentException(art),
            };
            e.Name = name;
            e.GlobalId = Xbim.Ifc4.UtilityResource.IfcGloballyUniqueId.ConvertToBase64(GuidAus(name));
            e.ObjectPlacement = L(lEg, lage ?? [0, 0, 0]);
            e.Representation = Form(k, lage);
            lagen[e] = lage;
            inEg.Add(e);
            return e;
        }

        Xbim.Ifc4.RepresentationResource.IfcProductDefinitionShape Form(Koerper k, double[]? ursprung)
        {
            Xbim.Ifc4.ProfileResource.IfcProfileDef profil;
            Xbim.Ifc4.GeometryResource.IfcAxis2Placement3D pos;
            double tiefe;
            var pos2 = I.New<Xbim.Ifc4.GeometryResource.IfcAxis2Placement2D>(a => a.Location = I.New<Xbim.Ifc4.GeometryResource.IfcCartesianPoint>(p => p.SetXY(0, 0)));
            if (k.Art == "quader")
            {
                profil = I.New<Xbim.Ifc4.ProfileResource.IfcRectangleProfileDef>(r => { r.ProfileType = Xbim.Ifc4.Interfaces.IfcProfileTypeEnum.AREA; r.XDim = k.A; r.YDim = k.B; r.Position = pos2; });
                pos = A(Minus(k.Von, ursprung));
                tiefe = k.H;
            }
            else
            {
                var r = Minus(k.Nach!, k.Von);
                tiefe = Math.Sqrt(r[0] * r[0] + r[1] * r[1] + r[2] * r[2]);
                profil = k.Art == "kreis"
                    ? I.New<Xbim.Ifc4.ProfileResource.IfcCircleProfileDef>(c => { c.ProfileType = Xbim.Ifc4.Interfaces.IfcProfileTypeEnum.AREA; c.Radius = k.A; c.Position = pos2; })
                    : I.New<Xbim.Ifc4.ProfileResource.IfcRectangleProfileDef>(c => { c.ProfileType = Xbim.Ifc4.Interfaces.IfcProfileTypeEnum.AREA; c.XDim = k.A; c.YDim = k.B; c.Position = pos2; });
                pos = A(Minus(k.Von, ursprung), r);
            }
            var koerper = I.New<Xbim.Ifc4.GeometricModelResource.IfcExtrudedAreaSolid>(x => { x.SweptArea = profil; x.Depth = tiefe; x.ExtrudedDirection = D([0, 0, 1]); x.Position = pos; });
            return I.New<Xbim.Ifc4.RepresentationResource.IfcProductDefinitionShape>(s =>
                s.Representations.Add(I.New<Xbim.Ifc4.RepresentationResource.IfcShapeRepresentation>(r =>
                {
                    r.ContextOfItems = ctx; r.RepresentationIdentifier = "Body"; r.RepresentationType = "SweptSolid"; r.Items.Add(koerper);
                })));
        }

        public object Port(object element, string name, double[] lage, double[] achse, string? fluss)
        {
            var e = (Xbim.Ifc4.Kernel.IfcProduct)element;
            var p = I.New<Xbim.Ifc4.SharedBldgServiceElements.IfcDistributionPort>(x =>
            {
                x.Name = name;
                x.FlowDirection = fluss is null ? Xbim.Ifc4.Interfaces.IfcFlowDirectionEnum.NOTDEFINED : Enum.Parse<Xbim.Ifc4.Interfaces.IfcFlowDirectionEnum>(fluss);
                x.ObjectPlacement = L((Xbim.Ifc4.GeometricConstraintResource.IfcLocalPlacement)e.ObjectPlacement, Minus(lage, lagen[e]), achse);
            });
            if (nests || e is not Xbim.Ifc4.ProductExtension.IfcDistributionElement) I.New<Xbim.Ifc4.Kernel.IfcRelNests>(r => { r.RelatingObject = e; r.RelatedObjects.Add(p); });
            else I.New<Xbim.Ifc4.ProductExtension.IfcRelConnectsPortToElement>(r => { r.RelatingPort = p; r.RelatedElement = (Xbim.Ifc4.ProductExtension.IfcDistributionElement)e; });
            return p;
        }

        public void Verbinden(object a, object b) =>
            I.New<Xbim.Ifc4.ProductExtension.IfcRelConnectsPorts>(r => { r.RelatingPort = (Xbim.Ifc4.ProductExtension.IfcPort)a; r.RelatedPort = (Xbim.Ifc4.ProductExtension.IfcPort)b; });

        public void System(string name, string? ifc4Typ, IEnumerable<object> mitglieder)
        {
            if (!systeme.TryGetValue(name, out var s))
                systeme[name] = s = I.New<Xbim.Ifc4.SharedBldgServiceElements.IfcDistributionSystem>(x =>
                {
                    x.Name = name;
                    x.GlobalId = Xbim.Ifc4.UtilityResource.IfcGloballyUniqueId.ConvertToBase64(GuidAus("system " + name));
                    if (ifc4Typ is not null) x.PredefinedType = Enum.Parse<Xbim.Ifc4.Interfaces.IfcDistributionSystemEnum>(ifc4Typ);
                });
            I.New<Xbim.Ifc4.Kernel.IfcRelAssignsToGroup>(r => { r.RelatingGroup = s; foreach (var o in mitglieder) r.RelatedObjects.Add((Xbim.Ifc4.Kernel.IfcObjectDefinition)o); });
        }

        public void Strang(string name, string system, IEnumerable<object> mitglieder)
        {
            var k = I.New<Xbim.Ifc4.SharedBldgServiceElements.IfcDistributionCircuit>(x =>
            {
                x.Name = name;
                x.GlobalId = Xbim.Ifc4.UtilityResource.IfcGloballyUniqueId.ConvertToBase64(GuidAus("strang " + name));
            });
            I.New<Xbim.Ifc4.Kernel.IfcRelAssignsToGroup>(r => { r.RelatingGroup = systeme[system]; r.RelatedObjects.Add(k); });
            I.New<Xbim.Ifc4.Kernel.IfcRelAssignsToGroup>(r => { r.RelatingGroup = k; foreach (var o in mitglieder) r.RelatedObjects.Add((Xbim.Ifc4.Kernel.IfcObjectDefinition)o); });
        }

        public void Pset(object element, string pset, string name, double wert, bool laenge)
        {
            var ps = I.New<Xbim.Ifc4.Kernel.IfcPropertySet>(x =>
            {
                x.Name = pset;
                x.HasProperties.Add(I.New<Xbim.Ifc4.PropertyResource.IfcPropertySingleValue>(v =>
                {
                    v.Name = name;
                    v.NominalValue = laenge ? new Xbim.Ifc4.MeasureResource.IfcPositiveLengthMeasure(wert) : new Xbim.Ifc4.MeasureResource.IfcReal(wert);
                }));
            });
            I.New<Xbim.Ifc4.Kernel.IfcRelDefinesByProperties>(r => { r.RelatingPropertyDefinition = ps; r.RelatedObjects.Add((Xbim.Ifc4.Kernel.IfcObject)element); });
        }

        public void Speichern(string datei)
        {
            I.New<Xbim.Ifc4.ProductExtension.IfcRelContainedInSpatialStructure>(r => { r.RelatingStructure = eg; foreach (var e in inEg) r.RelatedElements.Add(e); });
            t.Commit();
            m.SaveAs(datei, StorageType.Ifc);
        }

        public void Dispose() { t.Dispose(); m.Dispose(); }
    }

    // ------------------------------------------------------------------ IFC2x3

    sealed class Bau2x3 : IBau
    {
        readonly IfcStore m;
        readonly ITransaction t;
        readonly Xbim.Ifc2x3.RepresentationResource.IfcGeometricRepresentationContext ctx;
        readonly Xbim.Ifc2x3.ProductExtension.IfcBuildingStorey eg;
        readonly Xbim.Ifc2x3.GeometricConstraintResource.IfcLocalPlacement lEg;
        readonly List<Xbim.Ifc2x3.Kernel.IfcProduct> inEg = [];
        readonly Dictionary<Xbim.Ifc2x3.Kernel.IfcProduct, double[]?> lagen = [];
        readonly Dictionary<string, Xbim.Ifc2x3.Kernel.IfcTypeObject> typen = [];
        readonly Dictionary<string, Xbim.Ifc2x3.ProductExtension.IfcSystem> systeme = [];
        IEntityCollection I => m.Instances;

        public Bau2x3()
        {
            m = IfcStore.Create(Wer, XbimSchemaVersion.Ifc2X3, XbimStoreType.InMemoryModel);
            t = m.BeginTransaction("Testdatei B2");
            var projekt = I.New<Xbim.Ifc2x3.Kernel.IfcProject>(p => p.Name = "Testprojekt HLKS");
            projekt.Initialize(ProjectUnits.SIUnitsUK);
            ctx = I.OfType<Xbim.Ifc2x3.RepresentationResource.IfcGeometricRepresentationContext>().First();
            var element = Xbim.Ifc2x3.ProductExtension.IfcElementCompositionEnum.ELEMENT;
            var lSite = L(null, [0, 0, 0]);
            var site = I.New<Xbim.Ifc2x3.ProductExtension.IfcSite>(x => { x.Name = "Gelaende"; x.ObjectPlacement = lSite; x.CompositionType = element; });
            var lGeb = L(lSite, [0, 0, 0]);
            var geb = I.New<Xbim.Ifc2x3.ProductExtension.IfcBuilding>(x => { x.Name = "Testhaus"; x.ObjectPlacement = lGeb; x.CompositionType = element; });
            lEg = L(lGeb, [0, 0, 0]);
            eg = I.New<Xbim.Ifc2x3.ProductExtension.IfcBuildingStorey>(x => { x.Name = "Erdgeschoss"; x.Elevation = 0; x.ObjectPlacement = lEg; x.CompositionType = element; });
            var og = I.New<Xbim.Ifc2x3.ProductExtension.IfcBuildingStorey>(x => { x.Name = "1. Obergeschoss"; x.Elevation = 3000; x.ObjectPlacement = L(lGeb, [0, 0, 3000]); x.CompositionType = element; });
            I.New<Xbim.Ifc2x3.Kernel.IfcRelAggregates>(r => { r.RelatingObject = projekt; r.RelatedObjects.Add(site); });
            I.New<Xbim.Ifc2x3.Kernel.IfcRelAggregates>(r => { r.RelatingObject = site; r.RelatedObjects.Add(geb); });
            I.New<Xbim.Ifc2x3.Kernel.IfcRelAggregates>(r => { r.RelatingObject = geb; r.RelatedObjects.Add(eg); r.RelatedObjects.Add(og); });
        }

        Xbim.Ifc2x3.GeometryResource.IfcCartesianPoint P(double[] c) => I.New<Xbim.Ifc2x3.GeometryResource.IfcCartesianPoint>(p => p.SetXYZ(c[0], c[1], c[2]));
        Xbim.Ifc2x3.GeometryResource.IfcDirection D(double[] c) => I.New<Xbim.Ifc2x3.GeometryResource.IfcDirection>(p => p.SetXYZ(c[0], c[1], c[2]));
        Xbim.Ifc2x3.GeometryResource.IfcAxis2Placement3D A(double[] o, double[]? z = null) => I.New<Xbim.Ifc2x3.GeometryResource.IfcAxis2Placement3D>(a =>
        {
            a.Location = P(o);
            if (z is not null) { a.Axis = D(Norm(z)); a.RefDirection = D(Senkrecht(Norm(z))); }
        });
        Xbim.Ifc2x3.GeometricConstraintResource.IfcLocalPlacement L(Xbim.Ifc2x3.GeometricConstraintResource.IfcLocalPlacement? rel, double[] o, double[]? z = null) =>
            I.New<Xbim.Ifc2x3.GeometricConstraintResource.IfcLocalPlacement>(l => { l.PlacementRelTo = rel; l.RelativePlacement = A(o, z); });

        Xbim.Ifc2x3.Kernel.IfcTypeObject Typ(string art, string? pt) =>
            typen.TryGetValue($"{art}|{pt}", out var x) ? x : typen[$"{art}|{pt}"] = art switch
            {
                "rohr" => I.New<Xbim.Ifc2x3.HVACDomain.IfcPipeSegmentType>(y => { y.Name = "Rohrtyp"; y.PredefinedType = Enum.Parse<Xbim.Ifc2x3.HVACDomain.IfcPipeSegmentTypeEnum>(pt!); }),
                "kanal" => I.New<Xbim.Ifc2x3.HVACDomain.IfcDuctSegmentType>(y => { y.Name = "Kanaltyp"; y.PredefinedType = Enum.Parse<Xbim.Ifc2x3.HVACDomain.IfcDuctSegmentTypeEnum>(pt!); }),
                "formteil" => I.New<Xbim.Ifc2x3.HVACDomain.IfcPipeFittingType>(y => { y.Name = "Formteiltyp " + pt; y.PredefinedType = Enum.Parse<Xbim.Ifc2x3.HVACDomain.IfcPipeFittingTypeEnum>(pt!); }),
                "wc" => I.New<Xbim.Ifc2x3.PlumbingFireProtectionDomain.IfcSanitaryTerminalType>(y => { y.Name = "Apparattyp"; y.PredefinedType = Enum.Parse<Xbim.Ifc2x3.PlumbingFireProtectionDomain.IfcSanitaryTerminalTypeEnum>(pt!); }),
                "ventil" => I.New<Xbim.Ifc2x3.HVACDomain.IfcValveType>(y => { y.Name = "Ventiltyp"; y.PredefinedType = Enum.Parse<Xbim.Ifc2x3.HVACDomain.IfcValveTypeEnum>(pt!); }),
                _ => throw new ArgumentException(art),
            };

        public object Element(string art, string name, double[]? lage, Koerper k, string? pt)
        {
            Xbim.Ifc2x3.Kernel.IfcProduct e = art switch
            {
                "rohr" or "kanal" => I.New<Xbim.Ifc2x3.SharedBldgServiceElements.IfcFlowSegment>(),
                "formteil" => I.New<Xbim.Ifc2x3.SharedBldgServiceElements.IfcFlowFitting>(),
                "wc" => I.New<Xbim.Ifc2x3.SharedBldgServiceElements.IfcFlowTerminal>(),
                "ventil" => I.New<Xbim.Ifc2x3.SharedBldgServiceElements.IfcFlowController>(),
                "proxy" => I.New<Xbim.Ifc2x3.ProductExtension.IfcBuildingElementProxy>(),
                _ => throw new ArgumentException(art),
            };
            e.Name = name;
            e.GlobalId = Xbim.Ifc2x3.UtilityResource.IfcGloballyUniqueId.ConvertToBase64(GuidAus(name));
            e.ObjectPlacement = L(lEg, lage ?? [0, 0, 0]);
            e.Representation = Form(k, lage);
            if (art != "proxy")
            {
                var typ = Typ(art, art == "wc" ? pt ?? "TOILETPAN" : pt);
                I.New<Xbim.Ifc2x3.Kernel.IfcRelDefinesByType>(r => { r.RelatingType = typ; r.RelatedObjects.Add((Xbim.Ifc2x3.Kernel.IfcObject)e); });
            }
            lagen[e] = lage;
            inEg.Add(e);
            return e;
        }

        Xbim.Ifc2x3.RepresentationResource.IfcProductDefinitionShape Form(Koerper k, double[]? ursprung)
        {
            Xbim.Ifc2x3.ProfileResource.IfcProfileDef profil;
            Xbim.Ifc2x3.GeometryResource.IfcAxis2Placement3D pos;
            double tiefe;
            var pos2 = I.New<Xbim.Ifc2x3.GeometryResource.IfcAxis2Placement2D>(a => a.Location = I.New<Xbim.Ifc2x3.GeometryResource.IfcCartesianPoint>(p => p.SetXY(0, 0)));
            if (k.Art == "quader")
            {
                profil = I.New<Xbim.Ifc2x3.ProfileResource.IfcRectangleProfileDef>(r => { r.ProfileType = Xbim.Ifc2x3.ProfileResource.IfcProfileTypeEnum.AREA; r.XDim = k.A; r.YDim = k.B; r.Position = pos2; });
                pos = A(Minus(k.Von, ursprung));
                tiefe = k.H;
            }
            else
            {
                var r = Minus(k.Nach!, k.Von);
                tiefe = Math.Sqrt(r[0] * r[0] + r[1] * r[1] + r[2] * r[2]);
                profil = k.Art == "kreis"
                    ? I.New<Xbim.Ifc2x3.ProfileResource.IfcCircleProfileDef>(c => { c.ProfileType = Xbim.Ifc2x3.ProfileResource.IfcProfileTypeEnum.AREA; c.Radius = k.A; c.Position = pos2; })
                    : I.New<Xbim.Ifc2x3.ProfileResource.IfcRectangleProfileDef>(c => { c.ProfileType = Xbim.Ifc2x3.ProfileResource.IfcProfileTypeEnum.AREA; c.XDim = k.A; c.YDim = k.B; c.Position = pos2; });
                pos = A(Minus(k.Von, ursprung), r);
            }
            var koerper = I.New<Xbim.Ifc2x3.GeometricModelResource.IfcExtrudedAreaSolid>(x => { x.SweptArea = profil; x.Depth = tiefe; x.ExtrudedDirection = D([0, 0, 1]); x.Position = pos; });
            return I.New<Xbim.Ifc2x3.RepresentationResource.IfcProductDefinitionShape>(s =>
                s.Representations.Add(I.New<Xbim.Ifc2x3.RepresentationResource.IfcShapeRepresentation>(r =>
                {
                    r.ContextOfItems = ctx; r.RepresentationIdentifier = "Body"; r.RepresentationType = "SweptSolid"; r.Items.Add(koerper);
                })));
        }

        public object Port(object element, string name, double[] lage, double[] achse, string? fluss)
        {
            var e = (Xbim.Ifc2x3.Kernel.IfcProduct)element;
            var p = I.New<Xbim.Ifc2x3.SharedBldgServiceElements.IfcDistributionPort>(x =>
            {
                x.Name = name;
                x.FlowDirection = fluss is null ? Xbim.Ifc2x3.SharedBldgServiceElements.IfcFlowDirectionEnum.NOTDEFINED : Enum.Parse<Xbim.Ifc2x3.SharedBldgServiceElements.IfcFlowDirectionEnum>(fluss);
                x.ObjectPlacement = L((Xbim.Ifc2x3.GeometricConstraintResource.IfcLocalPlacement)e.ObjectPlacement, Minus(lage, lagen[e]), achse);
            });
            I.New<Xbim.Ifc2x3.ProductExtension.IfcRelConnectsPortToElement>(r => { r.RelatingPort = p; r.RelatedElement = (Xbim.Ifc2x3.ProductExtension.IfcElement)e; });
            return p;
        }

        public void Verbinden(object a, object b) =>
            I.New<Xbim.Ifc2x3.ProductExtension.IfcRelConnectsPorts>(r => { r.RelatingPort = (Xbim.Ifc2x3.ProductExtension.IfcPort)a; r.RelatedPort = (Xbim.Ifc2x3.ProductExtension.IfcPort)b; });

        public void System(string name, string? ifc4Typ, IEnumerable<object> mitglieder)
        {
            if (!systeme.TryGetValue(name, out var s))
                systeme[name] = s = I.New<Xbim.Ifc2x3.ProductExtension.IfcSystem>(x =>
                {
                    x.Name = name;
                    x.GlobalId = Xbim.Ifc2x3.UtilityResource.IfcGloballyUniqueId.ConvertToBase64(GuidAus("system " + name));
                });
            I.New<Xbim.Ifc2x3.Kernel.IfcRelAssignsToGroup>(r => { r.RelatingGroup = s; foreach (var o in mitglieder) r.RelatedObjects.Add((Xbim.Ifc2x3.Kernel.IfcObjectDefinition)o); });
        }

        public void Strang(string name, string system, IEnumerable<object> mitglieder) =>
            throw new NotSupportedException("IfcDistributionCircuit gibt es erst in IFC4.");

        public void Pset(object element, string pset, string name, double wert, bool laenge)
        {
            var ps = I.New<Xbim.Ifc2x3.Kernel.IfcPropertySet>(x =>
            {
                x.Name = pset;
                x.HasProperties.Add(I.New<Xbim.Ifc2x3.PropertyResource.IfcPropertySingleValue>(v =>
                {
                    v.Name = name;
                    v.NominalValue = laenge ? new Xbim.Ifc2x3.MeasureResource.IfcPositiveLengthMeasure(wert) : new Xbim.Ifc2x3.MeasureResource.IfcReal(wert);
                }));
            });
            I.New<Xbim.Ifc2x3.Kernel.IfcRelDefinesByProperties>(r => { r.RelatingPropertyDefinition = ps; r.RelatedObjects.Add((Xbim.Ifc2x3.Kernel.IfcObject)element); });
        }

        public void Speichern(string datei)
        {
            I.New<Xbim.Ifc2x3.ProductExtension.IfcRelContainedInSpatialStructure>(r => { r.RelatingStructure = eg; foreach (var e in inEg) r.RelatedElements.Add(e); });
            t.Commit();
            m.SaveAs(datei, StorageType.Ifc);
        }

        public void Dispose() { t.Dispose(); m.Dispose(); }
    }
}
