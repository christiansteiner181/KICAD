using System.Reflection;
using System.Resources;
using System.Xml.Linq;
using Xbim.Common;

namespace Hlks.Kern.Tests;

/// <summary>Modul 7 Teil B2: jede Entität, jedes Attribut und jeder Aufzählungswert, die der IFC-Export schreibt, an den Schemaklassen von
/// Xbim.Essentials 6.1.605 (IFC4 ADD2 TC1 und IFC2X3 TC1, aus den EXPRESS-Schemas von buildingSMART) – nicht aus dem Gedächtnis.</summary>
public class IfcExportSchemaTests
{
    static readonly Assembly Ifc4 = typeof(Xbim.Ifc4.Kernel.IfcProduct).Assembly;
    static readonly Assembly Ifc2x3 = typeof(Xbim.Ifc2x3.Kernel.IfcProduct).Assembly;

    static Type K(Assembly a, string n) => a.GetTypes().Single(t => t.IsClass && t.Name == n && typeof(IPersistEntity).IsAssignableFrom(t));
    static Type? KOderNull(Assembly a, string n) => a.GetTypes().SingleOrDefault(t => t.IsClass && t.Name == n && typeof(IPersistEntity).IsAssignableFrom(t));
    static PropertyInfo E(Type t, string n) => t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.Name == n).OrderByDescending(p => p.DeclaringType == t).First();
    static Type U(PropertyInfo p) => Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
    static string[] Werte(Type t, string n) => Enum.GetNames(U(E(t, n)));
    static bool Erbt(Type t, string basis) { for (var x = t; x is not null; x = x.BaseType) if (x.Name == basis) return true; return false; }

    [Fact]
    public void Port_Ifc4MitPredefinedTypeUndSystemType_Ifc2x3NurFlowDirection()
    {
        var p4 = K(Ifc4, "IfcDistributionPort");
        Assert.Contains("PIPE", Werte(p4, "PredefinedType"));
        Assert.Contains("DUCT", Werte(p4, "PredefinedType"));
        Assert.Contains("NOTDEFINED", Werte(p4, "PredefinedType"));
        Assert.Equal("IfcDistributionSystemEnum", U(E(p4, "SystemType")).Name);
        Assert.Equal(["NOTDEFINED", "SINK", "SOURCE", "SOURCEANDSINK"], Werte(p4, "FlowDirection").Order());
        var p2 = K(Ifc2x3, "IfcDistributionPort");
        Assert.Null(p2.GetProperty("PredefinedType", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.Null(p2.GetProperty("SystemType", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.Equal(["NOTDEFINED", "SINK", "SOURCE", "SOURCEANDSINK"], Werte(p2, "FlowDirection").Order());
        // am Element: IFC4 IfcRelNests (beide Seiten IfcObjectDefinition), IFC2X3 IfcRelConnectsPortToElement (IfcElement)
        Assert.Equal("IfcObjectDefinition", E(K(Ifc4, "IfcRelNests"), "RelatingObject").PropertyType.Name);
        Assert.Equal("IfcElement", E(K(Ifc2x3, "IfcRelConnectsPortToElement"), "RelatedElement").PropertyType.Name);
        Assert.Equal("IfcPort", E(K(Ifc2x3, "IfcRelConnectsPorts"), "RelatingPort").PropertyType.Name);
        Assert.Equal("IfcPort", E(K(Ifc4, "IfcRelConnectsPorts"), "RelatedPort").PropertyType.Name);
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Daemmung_IfcCoveringInsulation_UeberIfcRelCoversBldgElementsAnIfcElement(string fassung)
    {
        var a = fassung == "IFC4" ? Ifc4 : Ifc2x3;
        Assert.Contains("INSULATION", Werte(K(a, "IfcCovering"), "PredefinedType"));
        var rel = K(a, "IfcRelCoversBldgElements");
        Assert.Equal("IfcElement", E(rel, "RelatingBuildingElement").PropertyType.Name);
        // Rohre und Kanäle sind IfcElement: IFC4 IfcPipeSegment/IfcDuctSegment, IFC2X3 IfcFlowSegment
        foreach (var n in fassung == "IFC4" ? new[] { "IfcPipeSegment", "IfcDuctSegment" } : ["IfcFlowSegment"])
            Assert.True(Erbt(K(a, n), "IfcElement"), n);
        Assert.Equal("IfcPositiveLengthMeasure", E(K(a, "IfcCircleHollowProfileDef"), "WallThickness").PropertyType.Name);
        Assert.Equal("IfcPositiveLengthMeasure", E(K(a, "IfcRectangleHollowProfileDef"), "WallThickness").PropertyType.Name);
    }

    [Fact]
    public void Systeme_Ifc4DistributionSystemUndCircuit_Ifc2x3NurIfcSystem_AnlageAlsGruppe()
    {
        var ds = K(Ifc4, "IfcDistributionSystem");
        var werte = Werte(ds, "PredefinedType");
        foreach (var s in IfcHilfe.Kat.Ifc!.Systemtypen) Assert.Contains(s.Ifc4, werte);
        Assert.Contains("CONTROL", werte);
        Assert.Equal("IfcLabel", U(E(ds, "LongName")).Name);
        Assert.True(Erbt(K(Ifc4, "IfcDistributionCircuit"), "IfcDistributionSystem"));
        Assert.Null(KOderNull(Ifc2x3, "IfcDistributionSystem"));
        Assert.Null(KOderNull(Ifc2x3, "IfcDistributionCircuit"));
        // Anlage: IfcSystem ist eine IfcGroup; Mitglieder über IfcRelAssignsToGroup.RelatedObjects (IfcObjectDefinition) – in beiden Fassungen
        foreach (var a in new[] { Ifc4, Ifc2x3 })
        {
            Assert.True(Erbt(K(a, "IfcSystem"), "IfcGroup"));
            Assert.True(Erbt(K(a, "IfcSystem"), "IfcObjectDefinition"));
            Assert.Contains("IfcObjectDefinition", E(K(a, "IfcRelAssigns"), "RelatedObjects").PropertyType.GenericTypeArguments.Select(t => t.Name));
            Assert.Equal("IfcGroup", E(K(a, "IfcRelAssignsToGroup"), "RelatingGroup").PropertyType.Name);
        }
    }

    [Fact]
    public void Klassifikation_Ifc2x3SourceUndEditionPflicht_Ifc4Optional()
    {
        var k2 = K(Ifc2x3, "IfcClassification");
        Assert.Null(Nullable.GetUnderlyingType(E(k2, "Edition").PropertyType));   // Pflicht: kein Nullable
        Assert.Null(Nullable.GetUnderlyingType(E(k2, "Source").PropertyType));
        var k4 = K(Ifc4, "IfcClassification");
        Assert.NotNull(Nullable.GetUnderlyingType(E(k4, "Edition").PropertyType));
        Assert.NotNull(Nullable.GetUnderlyingType(E(k4, "Source").PropertyType));
    }

    [Fact]
    public void Kartenbezug_NurIfc4()
    {
        var mc = K(Ifc4, "IfcMapConversion");
        foreach (var n in new[] { "Eastings", "Northings", "OrthogonalHeight", "XAxisAbscissa", "XAxisOrdinate", "Scale", "SourceCRS", "TargetCRS" }) Assert.NotNull(E(mc, n));
        Assert.Equal("IfcNamedUnit", E(K(Ifc4, "IfcProjectedCRS"), "MapUnit").PropertyType.Name);
        Assert.Null(KOderNull(Ifc2x3, "IfcMapConversion"));
    }

    [Fact]
    public void Raum_Wand_Typobjekte_Pflichtwerte()
    {
        Assert.Contains("INTERNAL", Werte(K(Ifc2x3, "IfcSpace"), "InteriorOrExteriorSpace"));
        Assert.Contains("SPACE", Werte(K(Ifc4, "IfcSpace"), "PredefinedType"));
        foreach (var a in new[] { Ifc4, Ifc2x3 })
        {
            Assert.Contains("ELEMENT", Werte(K(a, "IfcBuildingStorey"), "CompositionType"));
            Assert.Equal(["AXIS1", "AXIS2", "AXIS3"], Werte(K(a, "IfcMaterialLayerSetUsage"), "LayerSetDirection"));
            Assert.Contains("POSITIVE", Werte(K(a, "IfcMaterialLayerSetUsage"), "DirectionSense"));
        }
        // Pflicht-Aufzählungen der Typobjekte, die der Schreiber auf NOTDEFINED setzt
        foreach (var (a, t, n) in new[] { (Ifc4, "IfcDoorType", "OperationType"), (Ifc4, "IfcWindowType", "PartitioningType"),
                     (Ifc2x3, "IfcDoorStyle", "OperationType"), (Ifc2x3, "IfcDoorStyle", "ConstructionType"), (Ifc2x3, "IfcWindowStyle", "OperationType"), (Ifc2x3, "IfcWindowStyle", "ConstructionType") })
        {
            Assert.Null(Nullable.GetUnderlyingType(E(K(a, t), n).PropertyType));
            Assert.Contains("NOTDEFINED", Werte(K(a, t), n));
        }
    }

    /// <summary>Jede Entität und jedes Typobjekt der Abbildung (ifc.json) findet der Schreiber in den Schemaklassen der Fassung.</summary>
    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Schreiber_FindetAlleZieleDerAbbildung(string fassung)
    {
        foreach (var k in IfcHilfe.Kat.Ifc!.Klassen)
            foreach (var (z, _, _) in k.Ziele(fassung))
            {
                Assert.True(IfcExportSchreiber.Klasse(fassung, z.Entitaet) is not null, $"{k.Klasse}: {z.Entitaet} ({fassung})");
                if (z.Typ is not null) Assert.True(IfcExportSchreiber.Klasse(fassung, z.Typ) is not null, $"{k.Klasse}: {z.Typ} ({fassung})");
            }
        foreach (var n in new[] { "IfcDistributionPort", "IfcCovering", "IfcRelCoversBldgElements", "IfcSystem", "IfcClassificationReference", "IfcElementQuantity", "IfcFacetedBrep" })
            Assert.NotNull(IfcExportSchreiber.Klasse(fassung, n));
        Assert.Equal(fassung == "IFC4", IfcExportSchreiber.Klasse(fassung, "IfcDistributionCircuit") is not null);
    }
}

/// <summary>Modul 7 Teil B2: Property-Set- und Mengennamen des Exports an den Property-Set-Definitionen (PSD) von buildingSMART, wie sie
/// Xbim.Properties 6.0.11 mitliefert (eingebettete PSD-XML für IFC4 und IFC2x3; nur im Test).</summary>
public class IfcExportPsdTests
{
    static readonly Assembly Psd = Assembly.Load("Xbim.Properties");

    /// <summary>Property Set → Eigenschaft → Datentyp (bzw. QtoType) aus einer Ressource von Xbim.Properties.</summary>
    static Dictionary<string, Dictionary<string, string>> Lesen(string ressource)
    {
        var aus = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        using var s = Psd.GetManifestResourceStream($"Xbim.Properties.Definitions.{ressource}.resources") ?? throw new Xunit.Sdk.XunitException($"Ressource {ressource} fehlt");
        using var r = new ResourceReader(s);
        var e = r.GetEnumerator();
        while (e.MoveNext())
        {
            var text = e.Value switch { byte[] b => System.Text.Encoding.UTF8.GetString(b), string t => t, _ => "" };
            if (text.Length == 0) continue;
            var x = XDocument.Parse(text.TrimStart('﻿'));
            var eig = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var d in x.Descendants().Where(d => d.Name.LocalName is "PropertyDef" or "QtoDef"))
            {
                var name = d.Elements().FirstOrDefault(n => n.Name.LocalName == "Name")?.Value;
                if (name is null) continue;
                var typ = d.Descendants().FirstOrDefault(n => n.Name.LocalName == "DataType")?.Attribute("type")?.Value
                          ?? d.Elements().FirstOrDefault(n => n.Name.LocalName == "QtoType")?.Value ?? "";
                eig[name] = typ;
            }
            aus[(string)e.Key] = eig;
        }
        return aus;
    }

    static readonly Lazy<Dictionary<string, Dictionary<string, string>>> ifc4 = new(() => Lesen("IFC4_Definition_files"));
    static readonly Lazy<Dictionary<string, Dictionary<string, string>>> ifc4Qto = new(() => Lesen("IFC4_QTO_Definition_files"));
    static readonly Lazy<Dictionary<string, Dictionary<string, string>>> ifc2x3 = new(() => Lesen("IFC2x3_Defitinion_files"));

    [Fact]
    public void ErsteEintraegeDerAbbildung_SindPsetsDerPsdIfc4_MitLaengenmass()
    {
        int n = 0;
        foreach (var k in IfcHilfe.Kat.Ifc!.Klassen)
            foreach (var (eig, kandidaten) in k.Psets ?? [])
            {
                var erster = kandidaten[0];
                if (!erster.StartsWith("Pset_", StringComparison.Ordinal)) continue;   // der Export schreibt nur buildingSMART-Psets
                var (set, name) = (erster[..erster.IndexOf('.')], erster[(erster.IndexOf('.') + 1)..]);
                Assert.True(ifc4.Value.TryGetValue(set, out var props), $"{k.Klasse}.{eig}: {set} fehlt in der PSD IFC4");
                Assert.True(props!.TryGetValue(name, out var typ), $"{k.Klasse}.{eig}: {set}.{name} fehlt in der PSD IFC4");
                Assert.Equal("IfcPositiveLengthMeasure", typ);
                n++;
            }
        Assert.True(n >= 5);
    }

    [Fact]
    public void PortNennweite_Ifc4Pset_DistributionPortTypePipe_Ifc2x3KeineNennweite()
    {
        var ziel = IfcHilfe.Kat.Ifc!.Ports!.Ifc4!["dn"][0];
        Assert.Equal("Pset_DistributionPortTypePipe.NominalDiameter", ziel);
        Assert.Equal("IfcPositiveLengthMeasure", ifc4.Value["Pset_DistributionPortTypePipe"]["NominalDiameter"]);
        Assert.False(ifc2x3.Value.ContainsKey("Pset_DistributionPortTypePipe"));
        Assert.Equal(["ConnectionType", "PortNumber"], ifc2x3.Value["Pset_DistributionPortPipe"].Keys.Order());
        Assert.Equal("HK_KICAD.Nennweite", IfcHilfe.Kat.Ifc!.Ports!.Ifc2x3!["dn"][0]);
    }

    [Fact]
    public void Mengen_QtoBaseQuantitiesLength_Ifc4()
    {
        foreach (var e in new[] { "IfcPipeSegment", "IfcDuctSegment" })
        {
            var name = IfcExport.QtoName(e);
            Assert.True(ifc4Qto.Value.TryGetValue(name, out var q), $"{name} fehlt in der PSD IFC4 (Qto)");
            Assert.Equal("Q_LENGTH", q!["Length"]);
        }
    }

    [Fact]
    public void Ventil_Nennweite_Pset_ValveTypeCommon_Size_in_IFC4_und_IFC2x3()
    {
        // Nachbearbeitung 20.10.2026: Exportziel der Ventil-Nennweite; die Lesekandidaten des Imports bleiben dahinter
        var dn = IfcHilfe.Kat.Ifc!.Klassen.Single(k => k.Klasse == "Ventil").Psets!["dn"];
        Assert.Equal(["Pset_ValveTypeCommon.Size", "Pset MEP.Geom-DN", "Pset MEP.Tech-DN"], dn);
        Assert.Equal("IfcPositiveLengthMeasure", ifc4.Value["Pset_ValveTypeCommon"]["Size"]);
        Assert.Equal("IfcPositiveLengthMeasure", ifc2x3.Value["Pset_ValveTypeCommon"]["Size"]);
    }

    [Fact]
    public void Ifc2x3_RohrUndKanalPsetsGibtEsAuch()
    {
        Assert.Equal("IfcPositiveLengthMeasure", ifc2x3.Value["Pset_PipeSegmentTypeCommon"]["NominalDiameter"]);
        Assert.Equal("IfcPositiveLengthMeasure", ifc2x3.Value["Pset_PipeSegmentTypeCommon"]["OuterDiameter"]);
        Assert.Equal("IfcPositiveLengthMeasure", ifc2x3.Value["Pset_DuctSegmentTypeCommon"]["NominalDiameterOrWidth"]);
        Assert.Equal("IfcPositiveLengthMeasure", ifc2x3.Value["Pset_DuctSegmentTypeCommon"]["NominalHeight"]);
    }
}
