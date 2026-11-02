using Xbim.Ifc4.Interfaces;

namespace Hlks.Kern.Tests;

/// <summary>IFC 1:1 (Christian 02.11.2026: «ein fenster muss 1:1 so aussehen wie im IFC. mit allen details»): Farben der Teilkörper aus
/// IfcStyledItem in der Geometrie und in der Netzdatei Fassung 4. Testdatei: minimal_ifc4.ifc, Wand mit zwei Körpern – einer rot,
/// halb durchsichtig (IfcSurfaceStyleRendering), einer ohne Stil.</summary>
public class IfcFarbenTests
{
    const string Wand = "0lZ_jWpr1Fmuq60stvFG2B";
    static readonly uint Rot = IfcGeometrie.Rgba(1, 0, 0, 0.5);

    static string Datei()
    {
        var ordner = Directory.CreateTempSubdirectory("hlks_ifcfarben_").FullName;
        var text = File.ReadAllText(IfcHilfe.Testdaten("minimal_ifc4.ifc")).Replace("\r\n", "\n")
            .Replace("#66=IFCSHAPEREPRESENTATION(#27,'Body','SweptSolid',(#56));", "#66=IFCSHAPEREPRESENTATION(#27,'Body','SweptSolid',(#56,#94));")
            .Replace("ENDSEC;\nEND-ISO", """
                #90=IFCCOLOURRGB($,1.,0.,0.);
                #91=IFCSURFACESTYLERENDERING(#90,0.5,$,$,$,$,$,$,.NOTDEFINED.);
                #92=IFCSURFACESTYLE('Rot',.BOTH.,(#91));
                #93=IFCSTYLEDITEM(#56,(#92),$);
                #94=IFCEXTRUDEDAREASOLID(#53,#95,#57,100.);
                #95=IFCAXIS2PLACEMENT3D(#96,$,$);
                #96=IFCCARTESIANPOINT((0.,0.,2600.));
                ENDSEC;
                END-ISO
                """.Replace("\r\n", "\n"));
        var pfad = Path.Combine(ordner, "farben.ifc");
        File.WriteAllText(pfad, text.Replace("\r\n", "\n"));
        return pfad;
    }

    [Fact]
    public void Geometrie_StilfarbeJeTeilkoerper_OhneStilKeineFarbe()
    {
        using var d = IfcDatei.Oeffnen(Datei());
        var wand = d.Modell.Instances.OfType<IIfcProduct>().Single(p => p.GlobalId == Wand);
        var n = IfcGeometrie.Von(d).Koerper(wand, merken: false)!;
        Assert.Equal((0, Rot), n.Farben[0]);
        var (start, farbe) = Assert.Single(n.Farben.Skip(1));
        Assert.Equal(0u, farbe);
        Assert.InRange(start, 1, n.Dreiecke - 1);   // der zweite Körper ohne Stil: keine Farbe
    }

    [Fact]
    public void Netzdatei_Fassung4_TraegtDieFarbabschnitte()
    {
        var pfad = Datei();
        using var d = IfcDatei.Oeffnen(pfad);
        var produkte = d.Modell.Instances.OfType<IIfcProduct>().Where(p => p is IIfcWall).ToList();
        var ziel = Path.Combine(Path.GetDirectoryName(pfad)!, "farben.netz");
        new IfcNetzErzeuger().Erzeugen(d, produkte, new HashSet<int>(), ziel);
        var netz = IfcNetzdatei.Oeffnen(ziel);
        Assert.Equal(4, netz.Fassung);
        var w = netz.Produkt(Wand)!;
        Assert.Equal(Rot, w.Farben[0].Farbe);
        Assert.Equal(0u, w.Farben[^1].Farbe);
    }
}
