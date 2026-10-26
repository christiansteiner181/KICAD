// Quest App P5b: Variante «teleport» = «stick» – Zielen mit dem Stick nach vorn, Springen beim Loslassen auf das letzte gültige Ziel, Abbruch ohne (bzw. mit zu altem)
// Ziel, Snap-Turn seitlich nur mit wirksamem «Drehen», Controller verloren.

using static Hlks.Xr.Tests.T;

namespace Hlks.Xr.Tests;

public class StickTeleportTests
{
    static readonly (Vec3, Vec3) Strahl = (V(0, 1.2, 0), V(0, -0.5, -1));       // trifft den Boden 2.4 m vor einem
    static readonly (Vec3, Vec3) Hoch = (V(0, 1.2, 0), V(0, 0.5, -1));         // zeigt über den Horizont: kein Ziel

    static List<BegehenEreignis> Lauf(StickZustand z, IEnumerable<(double T, (double, double)? Stick, (Vec3, Vec3)? Strahl)> bilder, bool drehen = true) =>
        [.. bilder.SelectMany(b => StickTeleport.Schritt(z, b.T, "right", b.Stick, b.Strahl, 0, drehen))];

    [Fact]
    public void Nach_vorn_zielen_loslassen_springt()
    {
        var z = new StickZustand();
        var e = Lauf(z, [(0, (0, 0.8), Strahl), (0.05, (0, 0.9), Strahl)]);
        Assert.Equal([BegehenArt.PinchAn, BegehenArt.Ziel, BegehenArt.Ziel], e.Select(x => x.Art));
        Assert.True(z.Zielt);
        Nah(z.Ziel!.Value.Punkt.Z, -2.4, 1e-9);
        var los = Lauf(z, [(0.1, (0, 0.1), Strahl)]);
        Assert.Equal([BegehenArt.Teleport, BegehenArt.PinchAus], los.Select(x => x.Art));
        Nah(los[0].Punkt!.Value.Z, -2.4, 1e-9);
        Assert.False(z.Zielt);
    }

    [Fact]
    public void Ohne_bzw_mit_altem_Ziel_Abbruch_Controller_weg_Abbruch()
    {
        var z = new StickZustand();
        var e = Lauf(z, [(0, (0, 0.8), Hoch), (0.1, (0, 0), Hoch)]);
        Assert.Contains(e, x => x.Art == BegehenArt.Abbruch);
        Assert.DoesNotContain(e, x => x.Art == BegehenArt.Teleport);
        // gültig bei 0 s, dann 0.2 s über den Horizont: beim Loslassen zu alt (Verzögerung 0.1 s)
        var z2 = new StickZustand();
        var e2 = Lauf(z2, [(0, (0, 0.8), Strahl), (0.1, (0, 0.8), Hoch), (0.2, (0, 0), Hoch)]);
        Assert.Contains(e2, x => x.Art == BegehenArt.Abbruch);
        // innerhalb der Verzögerung zählt das letzte gültige Ziel (der Strahl darf beim Loslassen kurz verreissen)
        var z3 = new StickZustand();
        var e3 = Lauf(z3, [(0, (0, 0.8), Strahl), (0.05, (0, 0), Hoch)]);
        Assert.Contains(e3, x => x.Art == BegehenArt.Teleport);
        // Controller weg während des Zielens
        var z4 = new StickZustand();
        Lauf(z4, [(0, (0, 0.8), Strahl)]);
        Assert.Equal([BegehenArt.Abbruch], Lauf(z4, [(0.05, null, null)]).Select(x => x.Art));
        Assert.False(z4.Zielt);
        Assert.Null(z4.Strahl);
        // ein schräger Ausschlag (mehr seitlich als vorn) zielt nicht
        Assert.DoesNotContain(Lauf(new StickZustand(), [(0, (0.8, 0.75), Strahl)]), x => x.Art == BegehenArt.PinchAn);
    }

    [Fact]
    public void Snap_Turn_seitlich_einmal_je_Ausschlag_nur_wenn_Drehen_wirkt()
    {
        var z = new StickZustand();
        var e = Lauf(z, [(0, (0.9, 0), null), (0.1, (0.95, 0), null), (0.2, (0, 0), null), (0.3, (-0.9, 0), null)]);
        Assert.Equal([(BegehenArt.Drehen, -1), (BegehenArt.Drehen, 1)], e.Select(x => (x.Art, x.Schritt)));   // rechts = nach rechts drehen (wie der Prototyp)
        Assert.Empty(Lauf(new StickZustand(), [(0, (0.9, 0), null)], drehen: false));
        // während des Zielens dreht der Stick nicht
        var z2 = new StickZustand();
        Assert.DoesNotContain(Lauf(z2, [(0, (0, 0.8), Strahl), (0.05, (0.5, 0.7), Strahl)]), x => x.Art == BegehenArt.Drehen);
    }
}
