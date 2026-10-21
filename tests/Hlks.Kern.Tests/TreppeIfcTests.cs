using System.Reflection;
using System.Resources;
using System.Xml.Linq;
using Xbim.Common;
using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Nachbearbeitung N17: Entitäten, Attribute und Property-Set-Namen der Treppe am Standard geprüft – nicht aus dem Gedächtnis:
/// Entitäten und Attribute an den Schemaklassen von xBIM (IFC4 und IFC2X3), Property Sets an den PSD von buildingSMART (Xbim.Properties 6.0.11).</summary>
public class TreppeIfcTests
{
    static readonly Assembly Ifc4 = typeof(Xbim.Ifc4.Kernel.IfcProduct).Assembly;
    static readonly Assembly Ifc2x3 = typeof(Xbim.Ifc2x3.Kernel.IfcProduct).Assembly;
    static readonly Assembly Psd = Assembly.Load("Xbim.Properties");

    static Type? Klasse(Assembly a, string name) =>
        a.GetTypes().FirstOrDefault(t => t.IsClass && t.Name == name && typeof(IPersistEntity).IsAssignableFrom(t));

    static Dictionary<string, HashSet<string>> Lesen(string ressource)
    {
        var aus = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        using var s = Psd.GetManifestResourceStream($"Xbim.Properties.Definitions.{ressource}.resources") ?? throw new Xunit.Sdk.XunitException($"Ressource {ressource} fehlt");
        using var r = new ResourceReader(s);
        var e = r.GetEnumerator();
        while (e.MoveNext())
        {
            var text = e.Value switch { byte[] b => System.Text.Encoding.UTF8.GetString(b), string t => t, _ => "" };
            if (text.Length == 0) continue;
            var x = XDocument.Parse(text.TrimStart('﻿'));
            aus[(string)e.Key] = x.Descendants().Where(d => d.Name.LocalName == "PropertyDef").Select(d => d.Elements().FirstOrDefault(n => n.Name.LocalName == "Name")?.Value).OfType<string>().ToHashSet(StringComparer.Ordinal);
        }
        return aus;
    }

    [Fact]
    public void Entitaeten_IfcStairUndIfcStairFlight_GibtEsInBeidenFassungen_MitDenAttributenDesLaufs()
    {
        foreach (var a in new[] { Ifc4, Ifc2x3 })
        {
            Assert.NotNull(Klasse(a, "IfcStair"));
            Assert.NotNull(Klasse(a, "IfcStairFlight"));
            Assert.NotNull(Klasse(a, "IfcSlab"));
        }
        // IFC2X3 nennt das Attribut «NumberOfRiser» (xBIM bildet es auf die IFC4-Schnittstelle als NumberOfRisers ab), IFC4 «NumberOfRisers»
        Assert.NotNull(typeof(Xbim.Ifc2x3.SharedBldgElements.IfcStairFlight).GetProperty("NumberOfRiser"));
        Assert.NotNull(typeof(Xbim.Ifc4.SharedBldgElements.IfcStairFlight).GetProperty("NumberOfRisers"));
        var schnittstelle = typeof(Xbim.Ifc4.Interfaces.IIfcStairFlight);
        foreach (var n in new[] { "NumberOfRisers", "NumberOfTreads", "RiserHeight", "TreadLength" })
            Assert.NotNull(schnittstelle.GetProperty(n));
        Assert.Contains(Enum.GetNames(typeof(Xbim.Ifc4.Interfaces.IfcSlabTypeEnum)), n => n == "LANDING");
    }

    [Fact]
    public void Abbildung_TreppeAufIfcStair_ImportAuchIfcStairFlight_BeideFassungen()
    {
        var k = Kat.Ifc!.Klasse("Treppe")!;
        Assert.Equal("IfcStair", k.Ifc4!.Entitaet);
        Assert.Equal("IfcStair", k.Ifc2x3!.Entitaet);
        Assert.Contains("IfcStairFlight", k.ImportAuch!);
        foreach (var schema in new[] { "IFC4", "IFC2X3" })
        {
            Assert.Equal("Treppe", Kat.Ifc.KlasseFuer(schema, "IfcStair", null, null, null)!.Klasse);
            Assert.Equal("Treppe", Kat.Ifc.KlasseFuer(schema, "IfcStairFlight", null, "STRAIGHT", null)!.Klasse);   // Typwert egal
        }
        Assert.Equal("IfcStair", Kat.Klassen["Treppe"].Ifc);
        Assert.Equal("TR", Kat.Klassen["Treppe"].Kuerzel);
    }

    [Fact]
    public void PropertySets_StairCommonUndStairFlightCommon_HabenDieGelesenenEigenschaften()
    {
        var ifc4 = Lesen("IFC4_Definition_files");
        var ifc2x3 = Lesen("IFC2x3_Defitinion_files");
        // Pset_StairCommon: in beiden Fassungen, die Eigenschaften, die der Übertrag liest
        foreach (var psd in new[] { ifc4, ifc2x3 })
            foreach (var n in new[] { "NumberOfRiser", "NumberOfTreads", "RiserHeight", "TreadLength" })
                Assert.Contains(n, psd["Pset_StairCommon"]);
        // Pset_StairFlightCommon: IFC4 (in IFC2X3 gibt es das Pset der Läufe unter diesem Namen nicht)
        foreach (var n in new[] { "NumberOfRiser", "NumberOfTreads", "RiserHeight", "TreadLength" })
            Assert.Contains(n, ifc4["Pset_StairFlightCommon"]);
        Assert.DoesNotContain("NumberOfRisers", ifc4["Pset_StairCommon"]);
    }
}
