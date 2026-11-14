using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Gesamtprüfung N31 (Christian 13.11.2026: «Echter abgesetzter Schnitt»): Schnittlinie als Linienzug, jeder zur ersten Richtung parallele Abschnitt
/// schneidet in seiner Ebene. Rechteck 6 × 4 m (W-1 Süd, W-2 Ost x = 6000, W-3 Nord, W-4 West x = 0), Blick nach Norden.</summary>
public class AbgesetzterSchnittTests
{
    static Plangrafik Schnitt(Projekt p, string kz, params double[][] punkte)
    {
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Ansicht", ["kennzeichen"] = kz, ["eigenschaften"] = new JsonObject { ["art"] = "schnitt", ["massstab"] = 50 },
            ["geometrie"] = new JsonObject { ["art"] = "linie", ["punkte"] = Pts(punkte) },
        }));
        return Grafik.Plan(p, kz);
    }

    static int Schnittflaechen(Plangrafik g, Projekt p, string wand) =>
        g.Teile.Where(t => t.Id == Hole(p, wand).Id).SelectMany(t => t.Primitive).Count(x => x.Art == "polygon");

    [Fact]
    public void Abschnitte_schneiden_je_in_ihrer_Ebene()
    {
        var p = Rechteck();
        // gerade bei y = 2000: beide Seitenwände geschnitten
        var gerade = Schnitt(p, "S-1", [-1000, 2000], [7000, 2000]);
        Assert.True(Schnittflaechen(gerade, p, "W-4") > 0 && Schnittflaechen(gerade, p, "W-2") > 0);
        // abgesetzt: links (u < 3000) bei y = 2000, rechts (u > 3000) bei y = −500 vor dem Gebäude → nur die Westwand geschnitten, die Ostwand in Ansicht
        var abgesetzt = Schnitt(p, "S-2", [-1000, 2000], [3000, 2000], [3000, -500], [7000, -500]);
        Assert.True(Schnittflaechen(abgesetzt, p, "W-4") > 0);
        Assert.Equal(0, Schnittflaechen(abgesetzt, p, "W-2"));
        // Südwand: im linken Abschnitt hinter der Ebene (unsichtbar), im rechten 500 mm davor → nur Sichtkanten, alle im u-Bereich ab 4000 (Zuschnitt)
        var sued = abgesetzt.Teile.Where(t => t.Id == Hole(p, "W-1").Id).SelectMany(t => t.Primitive).ToList();
        Assert.Contains(sued, x => x.Art is "linie" or "polylinie");
        Assert.DoesNotContain(sued, x => x.Art == "polygon");
        Assert.All(sued.SelectMany(x => x.Punkte), q => Assert.True(q[0] >= 4000 - 1e-6, $"u {q[0]} ausserhalb des Abschnitts"));
        // u-Bereiche: Grenze in der Mitte des Versatzes, aussen unbegrenzt
        var ab = Schnittraum.Abschnitte(Hole(p, "S-2"));
        Assert.Equal(2, ab.Count);
        Assert.Equal((double.NegativeInfinity, 4000.0, 4000.0, double.PositiveInfinity), (ab[0].U0, ab[0].U1, ab[1].U0, ab[1].U1));
    }
}
