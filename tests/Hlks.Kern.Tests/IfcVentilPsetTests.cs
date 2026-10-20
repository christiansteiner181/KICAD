using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.IfcExportHilfe;
using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Nachbearbeitung 20.10.2026: Nennweite des Ventils im IFC-Export als Pset_ValveTypeCommon.Size (buildingSMART, PSD IFC4 und IFC2x3,
/// IfcPositiveLengthMeasure; geprüft in IfcExportPsdTests). Vorher stand «Pset MEP.Geom-DN» zuerst – kein buildingSMART-Pset, darum nicht
/// exportiert. Der Import liest weiter alle Einträge der Liste und nimmt den ersten, der eine Nennweite ist.</summary>
public sealed class IfcVentilPsetTests : IDisposable
{
    readonly IfcExportOrdner ordner = new();
    readonly List<string> testordner = [];
    public void Dispose()
    {
        ordner.Dispose();
        foreach (var o in testordner) try { Directory.Delete(o, true); } catch (IOException) { }
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Rundreise_Ventil_mit_Nennweite_ueber_Pset_ValveTypeCommon_Size(string fassung)
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        var mv = p.Modell.Hole("MV-1");
        Assert.Equal(32, mv.Zahl("dn"));
        var datei = ordner.Datei($"ventil_{fassung}.ifc");
        var bericht = Export(p, datei, fassung);
        Assert.DoesNotContain("Ventil.dn", bericht.ToJsonString());   // kein Hinweis «kein buildingSMART-Pset» mehr

        // in der Datei: Pset_ValveTypeCommon.Size = 32 mm als Längenmass am Ventil
        using (var d = IfcDatei.Oeffnen(datei))
        {
            var x = d.Modell.Instances.OfType<Xbim.Ifc4.Interfaces.IIfcObject>().Single(o => o.GlobalId == Gid(p, mv));
            Assert.Equal(fassung == "IFC4" ? "IfcValve" : "IfcFlowController", IfcDatei.Entitaet(x));
            var size = d.Eigenschaften(x)["Pset_ValveTypeCommon.Size"];
            Assert.Contains("PositiveLengthMeasure", size.Art);
            Assert.Equal(32, d.LaengeMm(size)!.Value, 6);
        }

        // Import: gleiche Nennweite, Fundstelle Pset_ValveTypeCommon.Size
        var ziel = Importieren(datei);
        var vi = Importiert(ziel, Gid(p, mv));
        Assert.NotNull(vi);
        Assert.Equal("Ventil", vi!.Klasse);
        Assert.Equal(32, vi.Zahl("dn"));
        Assert.Contains("Pset_ValveTypeCommon.Size", vi.Eigenschaft("dn")!.Herkunft.Quelle);
        // alle Ventile des Beispiels
        foreach (var v in p.Modell.DerKlasse("Ventil").Where(v => v.Zahl("dn") is not null))
            Assert.Equal(v.Zahl("dn"), Importiert(ziel, Gid(p, v))!.Zahl("dn"));
    }

    Projekt Laden(string schema, IfcHlksSpec spec)
    {
        var o = IfcHlksTestdatei.Ordner();
        testordner.Add(o);
        var p = Leer();
        Ok(Uebernehmen(p, IfcHlksTestdatei.Schreiben(schema, Path.Combine(o, "ventil.ifc"), spec), new JsonObject { ["bereiche"] = new JsonArray("hlks"), ["grundlage"] = false }));
        return p;
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Import_Size_zuerst_wenn_Nennweite_sonst_weiter_mit_Geom_DN(string schema)
    {
        // Nova-Stil: Size und Geom-DN als IfcReal, gleich → Size (erster Eintrag)
        var a = Laden(schema, new IfcHlksSpec { MitVentil = true, VentilPsets = [("Pset_ValveTypeCommon", "Size", 50, false), ("Pset MEP", "Geom-DN", 50, false)] });
        var va = Assert.Single(a.Modell.DerKlasse("Ventil"));
        Assert.Equal(50, va.Zahl("dn"));
        Assert.Contains("Pset_ValveTypeCommon.Size", va.Eigenschaft("dn")!.Herkunft.Quelle);
        // Size keine Nennweite (0) → nicht mehr blockiert: Geom-DN
        var b = Laden(schema, new IfcHlksSpec { MitVentil = true, VentilPsets = [("Pset_ValveTypeCommon", "Size", 0, false), ("Pset MEP", "Geom-DN", 25, false)] });
        var vb = Assert.Single(b.Modell.DerKlasse("Ventil"));
        Assert.Equal(25, vb.Zahl("dn"));
        Assert.Contains("Geom-DN", vb.Eigenschaft("dn")!.Herkunft.Quelle);
        // nur Geom-DN (Dateien vor der Änderung): wie bisher
        var c = Laden(schema, new IfcHlksSpec { MitVentil = true, VentilPsets = [("Pset MEP", "Geom-DN", 40, false)] });
        Assert.Equal(40, Assert.Single(c.Modell.DerKlasse("Ventil")).Zahl("dn"));
    }

    [Fact]
    public void Echt_BKW_Nova_Heizung_Size_ist_die_Nennweite_wie_Geom_DN()
    {
        // Import nicht verschlechtert: wo Nova Pset_ValveTypeCommon.Size schreibt, ist es dieselbe Zahl wie Geom-DN bzw. Tech-DN (nur lokal)
        var pfad = Echt("bkw_hauptsitz/BKW-HS_1_HK-GEBT_NOV.ifc");
        if (pfad is null) return;
        using var d = IfcDatei.Oeffnen(pfad);
        int ventile = 0, mitSize = 0;
        foreach (var o in d.Modell.Instances.OfType<Xbim.Ifc4.Interfaces.IIfcObject>())
        {
            if (!(IfcDatei.Entitaet(o) == "IfcValve" || d.Typ(o) is Xbim.Ifc4.Interfaces.IIfcValveType)) continue;
            ventile++;
            var e = d.Eigenschaften(o);
            if (!e.TryGetValue("Pset_ValveTypeCommon.Size", out var size)) continue;
            mitSize++;
            var alt = e.GetValueOrDefault("Pset MEP.Geom-DN") ?? e.GetValueOrDefault("Pset MEP.Tech-DN");
            Assert.NotNull(alt);
            Assert.Equal(Convert.ToDouble(alt!.Inhalt), Convert.ToDouble(size.Inhalt));
        }
        Assert.True(ventile >= 200 && mitSize >= 200, $"{ventile} Ventile, {mitSize} mit Size");   // Stand 20.10.2026: 238 bzw. 202
    }
}
