using System.Text.Json.Nodes;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using CSMath;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Raumbezeichnungen aus einer DWG/DXF-Unterlage (Christian 05.11.2026). Fiktive DXF mit einem Raumstempel wie im Archicad-Plan
/// (Name gross, Nummer, Fläche, Belag) im Raum und einem Stempel ausserhalb.</summary>
public class RaumbezeichnungenDwgTests
{
    static string Dxf()
    {
        var doc = new CadDocument();
        doc.Header.InsUnits = ACadSharp.Types.Units.UnitsType.Meters;
        var stempel = new Layer("080 Raum Innen Text");
        var mass = new Layer("110 Bemassungen");
        doc.Layers.Add(stempel); doc.Layers.Add(mass);
        void T(Layer l, string text, double x, double y, double h) => doc.Entities.Add(new TextEntity { Value = text, InsertPoint = new XYZ(x, y, 0), Height = h, Layer = l });
        T(stempel, "Bad", 3.0, 2.4, 0.25); T(stempel, "E22", 3.0, 2.1, 0.1); T(stempel, "6.12 m²", 3.0, 1.9, 0.1); T(stempel, "B: Platten", 3.0, 1.7, 0.1); T(stempel, "0.21", 3.0, 1.5, 0.1);
        T(mass, "2.40", 1.0, 1.0, 0.1);
        T(stempel, "Loggia", 10.0, 10.0, 0.25); T(stempel, "E27", 10.0, 9.7, 0.1);
        var pfad = Path.Combine(Path.GetTempPath(), "hlks_raumtexte_" + Guid.NewGuid().ToString("N")[..8] + ".dxf");
        using (var w = new DxfWriter(pfad, doc, false)) w.Write();
        return pfad;
    }

    [Fact]
    public void Raumstempel_NameUndNummerImRaum_StempelAusserhalbGemeldet()
    {
        var p = Rechteck();
        Ok(p.Ausfuehren("raum_erkennen", new JsonObject { ["geschoss"] = "EG", ["punkt"] = Pt(3000, 2000) }));
        var dxf = Dxf();
        try
        {
            Ok(p.Ausfuehren("unterlage_einfuegen", new JsonObject { ["datei"] = dxf, ["geschoss"] = "EG" }));
            var u = p.Modell.DerKlasse("Unterlage").Single().Kennzeichen;
            var a = (JsonObject)Tuer.Abfrage(p, "raumbezeichnungen_dwg", new JsonObject { ["unterlage"] = u });
            Assert.Equal("080 Raum Innen Text", (string)a["layer"]!);
            var r = a["raeume"]!.AsArray().Single()!;
            Assert.Equal("Bad", (string)r["name"]!);   // grösster Text ohne Kennzeichnung
            Assert.Equal("E22", (string)r["nummer"]!);
            var o = a["ohneRaum"]!.AsArray().Single()!;
            Assert.Equal("Loggia", (string)o["name"]!);
            Assert.Equal("E27", (string)o["nummer"]!);
            Ok(p.Ausfuehren("raumbezeichnungen_uebernehmen", new JsonObject { ["unterlage"] = u }));
            var raum = p.Modell.DerKlasse("Raum").Single();
            Assert.Equal("Bad", raum.Name);
            Assert.Equal("E22", raum.Text("nummer"));
        }
        finally { File.Delete(dxf); }
    }
}
