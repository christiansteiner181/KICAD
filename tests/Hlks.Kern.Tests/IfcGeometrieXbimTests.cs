using Xbim.Ifc4.Interfaces;

namespace Hlks.Kern.Tests;

/// <summary>xBIM-Geometrie (OpenCascade, Entscheid Christian 02.11.2026): Booleans werden gerechnet statt nur des ersten Operanden. Testdatei:
/// minimal_ifc4.ifc, der Wandkörper (Extrusion 2600 mm) minus ein Quader ab 2000 mm – die Wand endet bei 2000 mm, ohne Hinweis «vereinfacht».</summary>
public class IfcGeometrieXbimTests
{
    const string Wand = "0lZ_jWpr1Fmuq60stvFG2B";

    static string Datei()
    {
        var ordner = Directory.CreateTempSubdirectory("hlks_xbim_").FullName;
        var text = File.ReadAllText(IfcHilfe.Testdaten("minimal_ifc4.ifc")).Replace("\r\n", "\n")
            .Replace("#66=IFCSHAPEREPRESENTATION(#27,'Body','SweptSolid',(#56));", "#66=IFCSHAPEREPRESENTATION(#27,'Body','CSG',(#90));")
            .Replace("ENDSEC;\nEND-ISO", """
                #90=IFCBOOLEANRESULT(.DIFFERENCE.,#56,#91);
                #91=IFCBLOCK(#92,6000.,2000.,2000.);
                #92=IFCAXIS2PLACEMENT3D(#93,$,$);
                #93=IFCCARTESIANPOINT((-500.,-1000.,2000.));
                ENDSEC;
                END-ISO
                """.Replace("\r\n", "\n"));
        var pfad = Path.Combine(ordner, "boolean.ifc");
        File.WriteAllText(pfad, text);
        return pfad;
    }

    [Fact]
    public void Boolean_Differenz_WirdGerechnet_WandEndetAmZuschnitt()
    {
        if (!OperatingSystem.IsWindows()) return;   // xBIM-Geometrie nur unter Windows; sonst rechnet KICAD selbst (vereinfacht)
        using var d = IfcDatei.Oeffnen(Datei());
        var g = IfcGeometrie.Von(d);
        var wand = d.Modell.Instances.OfType<IIfcProduct>().Single(p => p.GlobalId == Wand);
        var n = g.Koerper(wand, merken: false)!;
        Assert.True(g.MitXbim, IfcGeometrieXbim.Grund);
        var b = n.Begrenzung()!;
        Assert.Equal(2000, b[5], 1);   // eigene Auswertung: 2600 (nur der erste Operand)
        Assert.Equal(0, b[2], 1);
        Assert.False(g.IstVereinfacht(wand));
    }
}
