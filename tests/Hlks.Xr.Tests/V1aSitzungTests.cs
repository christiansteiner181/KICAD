// Tests Quest App V1a, Befund Prüfbild 24.10.2026: «Raum in AR» zeigte oben eine dunkle Platte, obwohl «unter der Decke» geschnitten wurde. Ursache: die Regel des
// Geschosses findet eine Decke nur, wenn ihre Oberkante auf ±2 mm auf der Kote des Geschosses darüber liegt; liegt die Rohdecke tiefer (Bodenaufbau), schnitt sie 50 mm
// unter der Kote – über der Platte. Jetzt zählt zusätzlich die Unterkante der Decke über dem Raum aus der Geometrie.

using Hlks.Xr.Modell;
using Hlks.Xr.Netz;
using Hlks.Xr.Sitzung;
using static Hlks.Xr.Tests.T;

namespace Hlks.Xr.Tests;

public class V1aSitzungTests
{
    static ListenTeil Teil(string id) => new("abcdef0123456789", $"/api/xr/geschoss/{id}.glb?teil=modell", null);

    static (ModellListe, ModellSzene) Haus(bool mitPlatte, string art = "IfcSlab")
    {
        var liste = new ModellListe("P", [0, 0, 0],
        [
            new ListenGeschoss("g-eg", "EG", "Erdgeschoss", 0, 3000, Teil("g-eg"), null),
            new ListenGeschoss("g-og", "OG", "Obergeschoss", 3000, 3000, Teil("g-og"), null),
        ], "{}");
        var raum = ModellHilfen.RaumNetz("r", [new(1, -1), new(5, -1), new(5, -4), new(1, -4)], 0, 2.5);
        var dateien = new List<(GlbDatei, string, string)> { (ModellHilfen.Datei([0, 0, 0], raum), "g-eg", GlbZwischenspeicher.TeilModell) };
        if (mitPlatte)   // Rohdecke 2.70 … 2.90 m, der fertige Boden des OG (Kote 3.00) 100 mm höher
            dateien.Add((ModellHilfen.Datei([0, 0, 0], ModellHilfen.Quadernetz("unterlage", ("slab", art, V(0, 2.7, -6), V(8, 2.9, 0)))), "g-og", GlbZwischenspeicher.TeilUnterlage));
        return (liste, new ModellSzene(dateien));
    }

    [Fact]
    public void Raum_in_AR_schneidet_unter_der_Rohdecke_aus_der_Geometrie()
    {
        var (liste, szene) = Haus(true);
        Nah(SitzungAuswahl.DeckeUeberRaumM(szene, szene.Finde("r")!)!.Value, 2.7, 1e-6);
        var a = SitzungAuswahl.Ausschnitt(szene, "r", liste)!;
        Assert.Contains("unter der Decke", a.Text);
        // Schnitt 50 mm unter der Unterkante der Platte (und dem Spiel), nicht 50 mm unter der Kote des OG (2.95 m)
        Nah(a.Schnitt!.YMax, 2.65 - XrSitzungLogik.SpielMm / 1000, 1e-6);
        // die Platte ist damit ganz weg: ihre Hüllbox liegt über dem Schnitt
        Assert.Equal(SchnittLage.Aussen, a.Schnitt.Lage(szene.Finde("slab")!.Box));
    }

    [Fact]
    public void Ohne_Platte_gilt_die_Regel_des_Geschosses_der_eigene_Boden_zählt_nicht()
    {
        var (liste, szene) = Haus(false);
        Assert.Null(SitzungAuswahl.DeckeUeberRaumM(szene, szene.Finde("r")!));
        var a = SitzungAuswahl.Ausschnitt(szene, "r", liste)!;
        Nah(a.Schnitt!.YMax, 2.95 - XrSitzungLogik.SpielMm / 1000, 1e-6);   // Kote OG − 50 mm
        // eine Platte unter dem Raum (eigener Boden) bzw. unter 1.5 m zählt nicht
        var boden = new ModellSzene([(ModellHilfen.Datei([0, 0, 0], ModellHilfen.RaumNetz("r", [new(1, -1), new(5, -1), new(5, -4), new(1, -4)], 0, 2.5),
            ModellHilfen.Quadernetz("architektur", ("b", "decke", V(0, -0.3, -6), V(8, 0, 0)), ("tief", "decke", V(0, 1.0, -6), V(8, 1.2, 0)))), "g-eg", GlbZwischenspeicher.TeilModell)]);
        Assert.Null(SitzungAuswahl.DeckeUeberRaumM(boden, boden.Finde("r")!));
        // ein Dach (IfcRoof) über dem Raum zählt wie eine Decke
        var (_, dach) = Haus(true, "IfcRoof");
        Nah(SitzungAuswahl.DeckeUeberRaumM(dach, dach.Finde("r")!)!.Value, 2.7, 1e-6);
    }
}
