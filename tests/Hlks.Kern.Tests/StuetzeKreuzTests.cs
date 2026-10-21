using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Nachbearbeitung N16 (Christian 21.10.2026: «stützen werden im Grundriss mit einem Kreuz angezeigt. nur bei beton bleibt die
/// schraffur»): Geschnittene Stützen zeigen im Grundriss ihren Umriss mit beiden Diagonalen und ohne Schraffur; Beton (auch Stahlbeton)
/// behält die Schraffur. Nicht geschnittene Stützen bleiben wie bisher.</summary>
public class StuetzeKreuzTests
{
    static void Stuetze(Projekt p, string kz, string? material, double unten, params double[][] punkte)
    {
        var eig = new JsonObject();
        if (material is not null) eig["material"] = material;
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Stuetze", ["kennzeichen"] = kz, ["geschoss"] = "EG", ["eigenschaften"] = eig,
            ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = Pts(punkte.Select(q => new[] { q[0], q[1], unten }).ToArray()), ["hoehe"] = 2750 },
        }));
    }

    static List<Primitiv> Primitive(Projekt p, string kz) =>
        Grafik.Plan(p, "AN-EG").Teile.Where(t => t.Id == Hole(p, kz).Id).SelectMany(t => t.Primitive).ToList();

    static readonly double[][] Quadrat = [[0, 0], [300, 0], [300, 300], [0, 300]];

    [Theory]
    [InlineData("stahl")]
    [InlineData("holz")]
    [InlineData(null)]
    public void GeschnitteneStuetze_OhneBeton_ZeigtKreuzOhneSchraffur(string? material)
    {
        var p = Neu();
        Stuetze(p, "STZ-1", material, 0, Quadrat);
        var prim = Primitive(p, "STZ-1");
        var umriss = Assert.Single(prim, x => x.Art == "polygon");
        Assert.Null(umriss.Stil.Muster);
        Assert.Equal("papier", umriss.Stil.Fuellung);
        var linien = prim.Where(x => x.Art == "linie").ToList();
        Assert.Equal(2, linien.Count);
        // Ecke zu Ecke: (0|0)–(300|300) und (300|0)–(0|300)
        Assert.Contains(linien, l => l.Punkte[0][0] == 0 && l.Punkte[0][1] == 0 && l.Punkte[1][0] == 300 && l.Punkte[1][1] == 300);
        Assert.Contains(linien, l => l.Punkte[0][0] == 300 && l.Punkte[0][1] == 0 && l.Punkte[1][0] == 0 && l.Punkte[1][1] == 300);
    }

    [Fact]
    public void GeschnitteneStuetze_Beton_BehaeltSchraffurOhneKreuz()
    {
        var p = Neu();
        Stuetze(p, "STZ-1", "beton", 0, Quadrat);
        var prim = Primitive(p, "STZ-1");
        Assert.DoesNotContain(prim, x => x.Art == "linie");
        Assert.Equal("beton", Assert.Single(prim, x => x.Art == "polygon").Stil.Muster);
    }

    [Theory]
    [InlineData("beton", true)]
    [InlineData("Stahlbeton", true)]           // Materialliste kennt heute nur «beton»; IFC- bzw. spätere Namen gelten mit
    [InlineData("Porenbeton", false)]
    [InlineData("stahl", false)]
    [InlineData("unbestimmt", false)]
    public void IstBeton_ErkenntBetonNamen(string material, bool beton) => Assert.Equal(beton, ArchitekturGrafik.IstBeton(material));

    [Fact]
    public void RundeStuetze_KreuzLaeuftDurchDieMitteVonRandZuRand()
    {
        var p = Neu();
        var kreis = Enumerable.Range(0, 16).Select(i => new[] { 150 + 150 * Math.Cos(i * Math.PI / 8), 150 + 150 * Math.Sin(i * Math.PI / 8) }).ToArray();
        Stuetze(p, "STZ-R", "stahl", 0, kreis);
        var linien = Primitive(p, "STZ-R").Where(x => x.Art == "linie").ToList();
        Assert.Equal(2, linien.Count);
        foreach (var l in linien)
        {
            double mx = (l.Punkte[0][0] + l.Punkte[1][0]) / 2, my = (l.Punkte[0][1] + l.Punkte[1][1]) / 2;
            Assert.InRange(mx, 140, 160); Assert.InRange(my, 140, 160);       // durch die Mitte
            double laenge = Math.Sqrt(Math.Pow(l.Punkte[1][0] - l.Punkte[0][0], 2) + Math.Pow(l.Punkte[1][1] - l.Punkte[0][1], 2));
            Assert.InRange(laenge, 290, 300.001);                             // von Rand zu Rand (Durchmesser 300)
        }
    }

    [Fact]
    public void NichtGeschnitteneStuetze_BleibtWieBisher_OhneKreuz()
    {
        var p = Neu();
        Stuetze(p, "STZ-O", "stahl", 1500, Quadrat);       // Unterkante über der Schnitthöhe: Sichtkante gestrichelt
        var prim = Primitive(p, "STZ-O");
        Assert.DoesNotContain(prim, x => x.Art == "linie");
        Assert.Equal("strich", Assert.Single(prim).Stil.Linie);
    }
}
