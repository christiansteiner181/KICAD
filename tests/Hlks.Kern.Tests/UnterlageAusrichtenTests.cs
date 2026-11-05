using System.Text.Json.Nodes;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using CSMath;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>DWG/DXF automatisch an den Wänden ausrichten (Christian 05.11.2026). Fiktive DXF: Wandumriss des Testrechtecks (6 × 4 m, Wand 200 mm),
/// mit einer Innenwand, um 90° gedreht und um (20 m, 7 m) verschoben – nach dem Einfügen liegt sie wieder auf den Wänden.</summary>
public class UnterlageAusrichtenTests
{
    static (double X, double Y) T(double x, double y) => (-y + 20, x + 7);   // Drehung 90°, Verschiebung (20, 7) m

    [Fact]
    public void Einfuegen_DrehtUndVerschiebtAufDieWaende()
    {
        var p = Rechteck();
        // Innenwand bei x = 1500 macht die Lage eindeutig (das Rechteck allein ist um 180° symmetrisch)
        Ok(p.Ausfuehren("wand_zeichnen", new JsonObject { ["geschoss"] = "EG", ["punkte"] = Pts([1500, 100], [1500, 3900]), ["dicke"] = 100 }));
        var doc = new CadDocument();
        doc.Header.InsUnits = ACadSharp.Types.Units.UnitsType.Meters;
        var wand = new Layer("010 Wände"); doc.Layers.Add(wand);
        void Ring(double x0, double y0, double x1, double y1)
        {
            (double, double)[] e = [(x0, y0), (x1, y0), (x1, y1), (x0, y1)];
            for (int i = 0; i < 4; i++)
            {
                var (ax, ay) = T(e[i].Item1, e[i].Item2); var (bx, by) = T(e[(i + 1) % 4].Item1, e[(i + 1) % 4].Item2);
                doc.Entities.Add(new Line { StartPoint = new XYZ(ax, ay, 0), EndPoint = new XYZ(bx, by, 0), Layer = wand });
            }
        }
        Ring(-0.1, -0.1, 6.1, 4.1); Ring(0.1, 0.1, 5.9, 3.9); Ring(1.45, 0.1, 1.55, 3.9);
        var pfad = Path.Combine(Path.GetTempPath(), "hlks_ausrichten_" + Guid.NewGuid().ToString("N")[..8] + ".dxf");
        using (var w = new DxfWriter(pfad, doc, false)) w.Write();
        try
        {
            var r = Ok(p.Ausfuehren("unterlage_einfuegen", new JsonObject { ["datei"] = pfad, ["geschoss"] = "EG" }));
            Assert.Contains(r.Hinweise, h => h.Contains("automatisch ausgerichtet"));
            var lage = UnterlagenLage.Lesen(p.Modell.DerKlasse("Unterlage").Single());
            foreach (var (x, y) in new[] { (-0.1, -0.1), (6.1, 4.1) })
            {
                var (u, v) = T(x, y);
                var (mx, my) = lage.Auf(u, v);
                Assert.Equal(x * 1000, mx, 0.5 * 20);   // auf 10 mm genau
                Assert.Equal(y * 1000, my, 0.5 * 20);
            }
            // schon gut: erneutes Ausrichten ändert nichts
            Assert.Contains(Ok(p.Ausfuehren("unterlage_ausrichten", new JsonObject { ["unterlage"] = "UL-1" })).Hinweise, h => h.Contains("liegt schon gut"));
        }
        finally { File.Delete(pfad); }
    }
}
