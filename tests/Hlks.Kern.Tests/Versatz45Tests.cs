using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;
using static Hlks.Kern.Tests.RoutingHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Befund Christian 14.11.2026 («scheinbar wird nur 1. Lage gezeichnet»), Entscheid «Mit 45°-Versatz»: ein Höhenwechsel, der für T-Stück bzw. Bogen
/// und 90°-Bogen zu kurz ist, wird mit 2 × 45° ausgeführt. Handrechnung mit den FIKTIVEN Testfamilien DN 32: T-Stück c = 37 mm, Bogen r = 74 mm →
/// 90°: la = 74 mm, 45°: la = lb = 74·tan 22.5° = 30.65 mm. Abzweig 100 mm tiefer braucht mit 90° 37 + 74 = 111 mm.</summary>
public class Versatz45Tests
{
    static readonly double L45 = 74 * Math.Tan(Math.PI / 8);

    [Fact]
    public void Abzweig_100mm_tiefer_mit_zwei_45Grad_Boegen()
    {
        var p = MitSerie();
        Ok(Zeichnen(p, "DN 32", [[0, 0, 1000], [4000, 0, 1000]]));
        var e = Ok(p.Ausfuehren("leitung_abzweigen", new JsonObject
        {
            ["leitung"] = "RO-1", ["punkt"] = new JsonArray(2000, 0, 1000), ["punkte"] = Punkte([2000, 0, 900], [2000, -1500, 900]),
        }));
        Assert.Contains(e.Hinweise, h => h.Contains("als Versatz mit 2 × 45°"));
        // T-Stück mit waagrechtem Abgang nach −y, Bogen 45° ab A1 = 37 + 30.65 mm, schräg 100 mm hinunter, Bogen 45° zurück in die Waagrechte
        Etwa([2000, -37, 1000], Stelle(p, "TS-1", "c").Lage, 1e-3);
        Etwa(45, Hole(p, "BO-1").Zahl("winkel") ?? 0, 1e-6, "BO-1");
        Etwa(45, Hole(p, "BO-2").Zahl("winkel") ?? 0, 1e-6, "BO-2");
        double s1 = 37 + L45;
        Etwa([2000, -s1, 1000], Familien.Einfuegepunkt(Hole(p, "BO-1")), 1e-3);
        Etwa([2000, -(s1 + 100), 900], Familien.Einfuegepunkt(Hole(p, "BO-2")), 1e-3);
        Sauber(p);
    }

    [Fact]
    public void Abzweig_mit_genug_Hoehe_bleibt_90Grad()
    {
        var p = MitSerie();
        Ok(Zeichnen(p, "DN 32", [[0, 0, 1000], [4000, 0, 1000]]));
        var e = Ok(p.Ausfuehren("leitung_abzweigen", new JsonObject
        {
            ["leitung"] = "RO-1", ["punkt"] = new JsonArray(2000, 0, 1000), ["punkte"] = Punkte([2000, 0, 800], [2000, -1500, 800]),
        }));
        Assert.DoesNotContain(e.Hinweise, h => h.Contains("2 × 45°"));
        Etwa(90, Hole(p, "BO-1").Zahl("winkel") ?? 0, 1e-6);
    }
}
