// Tests Quest App V1a: Prüfaufgabe «messen-wand-wand» – Wahl des Wandpaars (Raum am Stand, Kanten gegenüber, Blickrichtung), Sollmass aus den Wandflächen, Prüfung
// einer Messung (von Wand zu Wand, ±2 cm), Lauf mit Fehlgriffen und Erfolg, Messprotokoll mit der Variante «messen».

using Hlks.Xr.Labor;
using Hlks.Xr.Messen;
using Hlks.Xr.Modell;
using Hlks.Xr.Netz;
using static Hlks.Xr.Tests.T;
using P = Hlks.Xr.Labor.PruefAufgaben;

namespace Hlks.Xr.Tests;

public class V1aLaborTests
{
    /// <summary>
    /// Raum 4 × 3 m (Wandflächen x = 0 und 4, z = 0 und −3), Boden 0; der Grundriss des Raums liegt 1 cm innerhalb der Wände (wie eine Erkennung mit Toleranz),
    /// eine Seite in zwei Stücke geteilt. Wände als Quader 20 cm dick (Ebene «wand»).
    /// </summary>
    static ModellSzene Raum(bool mitWaenden = true)
    {
        var raum = ModellHilfen.RaumNetz("r", [new(0.01, -0.01), new(2, -0.01), new(3.99, -0.01), new(3.99, -2.99), new(0.01, -2.99)], 0, 2.6);
        var netze = new List<GlbNetzDaten> { raum };
        if (mitWaenden)
            netze.Add(ModellHilfen.Quadernetz("architektur",
                ("wl", "wand", V(-0.2, 0, -3.2), V(0, 2.6, 0.2)), ("wr", "wand", V(4, 0, -3.2), V(4.2, 2.6, 0.2)),
                ("wv", "wand", V(0, 0, 0), V(4, 2.6, 0.2)), ("wh", "wand", V(0, 0, -3.2), V(4, 2.6, -3))));
        return V1aMessenTests.Szene([.. netze]);
    }

    static Messung M(Vec3 a, Vec3 b) => new(1, MessTyp.Abstand, new MessPunkt(a, Fangart.Flaeche), new MessPunkt(b, Fangart.Flaeche), XrMessLogik.Masse(a, b), "begehen", 0);

    [Fact]
    public void Wandpaar_quer_zum_Blick_Sollmass_aus_den_Wandflächen()
    {
        var szene = Raum();
        // Blick nach +x (Gieren 0): die Wände links und rechts (x = 0 und 4) liegen gegenüber
        var paar = WandPaare.Waehlen(szene, "g", 0, V(2, 1.6, -1.5), 0)!;
        Assert.Equal("r", paar.Raum);
        Nah(paar.SollMm, 4000, 0.05);   // die Wandflächen, nicht der Grundriss (3980 mm)
        var xs = new[] { paar.A.Punkt.X, paar.B.Punkt.X }.Order().ToArray();
        Nah(xs[0], 0, 1e-6);
        Nah(xs[1], 4, 1e-6);
        Nah(Math.Abs(paar.A.Normale.X), 1, 1e-9);
        Nah(paar.A.Normale.Skalar(paar.B.Normale), -1, 1e-9);   // gegeneinander, je in den Raum
        Assert.True(paar.A.Abstand(V(2, 1, -1.5)) > 0 && paar.B.Abstand(V(2, 1, -1.5)) > 0);
        // Blick nach −z (Gieren 90°): vorne und hinten (z = 0 und −3)
        var quer = WandPaare.Waehlen(szene, "g", 0, V(2, 1.6, -1.5), Math.PI / 2)!;
        Nah(quer.SollMm, 3000, 0.05);
        // ohne Wände gilt der Grundriss
        Nah(WandPaare.Waehlen(Raum(false), "g", 0, V(2, 1.6, -1.5), 0)!.SollMm, 3980, 0.05);
        // ausserhalb eines Raums: der Raum 2 m vor dem Blick bzw. der nächste; kein Raum im Geschoss: null
        Assert.NotNull(WandPaare.Waehlen(szene, "g", 0, V(-1, 1.6, -1.5), 0));
        Assert.Null(WandPaare.Waehlen(szene, "anderes", 0, V(2, 1.6, -1.5), 0));
    }

    [Fact]
    public void Sollmass_von_der_sichtbaren_Fläche_auch_wenn_eine_Wand_im_Grundriss_liegt()
    {
        // Befund Prüflauf Test_praxis 24.10.2026: der Grundriss liegt auf der Aussenseite einer 25 cm dicken Wand im Raum (x = 0 … 0.25); man sieht und misst ab x = 0.25
        var raum = ModellHilfen.RaumNetz("r", [new(0, 0), new(4, 0), new(4, -3), new(0, -3)], 0, 2.6);
        var waende = ModellHilfen.Quadernetz("architektur",
            ("wi", "wand", V(0, 0, -3), V(0.25, 2.6, 0)), ("wr", "wand", V(4, 0, -3.2), V(4.2, 2.6, 0.2)), ("wl", "wand", V(-0.2, 0, -3.2), V(0, 2.6, 0.2)));
        var paar = WandPaare.Waehlen(V1aMessenTests.Szene(raum, waende), "g", 0, V(2, 1.6, -1.5), 0)!;
        Nah(paar.SollMm, 3750, 0.05);
        var xs = new[] { paar.A.Punkt.X, paar.B.Punkt.X }.Order().ToArray();
        Nah(xs[0], 0.25, 1e-6);
        Nah(xs[1], 4, 1e-6);
        // eine Messung zwischen den sichtbaren Flächen gelingt, eine bis zur Kante des Grundrisses nicht (25 cm daneben)
        Assert.Null(WandPaare.Pruefen(paar, M(V(0.25, 1.2, -1.5), V(4, 1.2, -1.5))));
        Assert.NotNull(WandPaare.Pruefen(paar, M(V(0, 1.2, -1.5), V(4, 1.2, -1.5))));
    }

    [Fact]
    public void Kanten_werden_unabhängig_von_der_Richtung_verbunden()
    {
        var k = WandPaare.Kanten([(new Xz(0, 0), new Xz(1, 0)), (new Xz(2, 0), new Xz(1, 0)), (new Xz(2, 0), new Xz(2, 1))]);
        Assert.Equal(2, k.Count);
        Assert.Contains(k, x => Math.Abs(Math.Abs(x.B.X - x.A.X) - 2) < 1e-9);
    }

    [Fact]
    public void Messung_prüfen_von_Wand_zu_Wand_und_Sollmass()
    {
        var paar = WandPaare.Waehlen(Raum(), "g", 0, V(2, 1.6, -1.5), 0)!;
        Assert.Null(WandPaare.Pruefen(paar, M(V(0, 1.2, -1.5), V(4, 1.3, -1.6))));      // 4002.5 mm: 2.5 mm daneben
        Assert.Null(WandPaare.Pruefen(paar, M(V(4, 1.2, -1.5), V(0.015, 1.2, -1.5))));   // umgekehrt, 1.5 cm vor der Wand: 15 mm daneben
        Assert.Contains("30 mm", WandPaare.Pruefen(paar, M(V(4, 1.2, -1.5), V(0.03, 1.2, -1.5))));   // 3 cm vor der Wand: 30 mm daneben
        Assert.Contains("neben dem Sollmass", WandPaare.Pruefen(paar, M(V(0, 1.2, -1.5), V(3.95, 1.2, -1.5))));
        Assert.Equal("Messung nicht von Wand zu Wand", WandPaare.Pruefen(paar, M(V(2, 1.2, 0), V(2, 1.2, -3))));
        Assert.Equal("Messung nicht von Wand zu Wand", WandPaare.Pruefen(paar, M(V(0, 1.2, -1.5), V(0, 1.5, -2))));   // beide an derselben Wand
    }

    [Fact]
    public void Lauf_messen_wand_wand_mit_Fehlgriffen_und_Erfolg()
    {
        var l = P.Starten(P.MessenWandWand, null);
        Assert.Equal(AufgabeOrt.Begehen, P.Ort(P.MessenWandWand));
        Assert.False(P.MessenVorbereiten(l, null));
        Assert.Contains("Kein Raum", l.Grund);
        Assert.True(P.MessenVorbereiten(l, WandPaare.Waehlen(Raum(), "g", 0, V(2, 1.6, -1.5), 0)));
        P.Beginnen(l, 10);
        Assert.Equal("Jetzt: von der einen markierten Wand zur anderen messen · 0.0 s · 0 Fehlgriffe", P.Zeile(l, 10));
        P.MessenDaneben(l, "Messband zu kurz");
        P.Gemessen(l, 12, M(V(2, 1.2, 0), V(2, 1.2, -3)));
        Assert.Equal(2, l.Fehlgriffe);
        Assert.True(l.Laeuft);
        P.Gemessen(l, 15, M(V(0, 1.2, -1.5), V(4, 1.2, -1.5)));
        Assert.Equal(LaufZustand.Erfolg, l.Zustand);
        var o = P.Messprotokoll(l, 15, new Dictionary<string, string> { ["messen"] = "messband" });
        Assert.Equal(5000, (double)o["zeitMs"]!);
        Assert.Equal(2, (int)o["fehlgriffe"]!);
        Assert.True((bool)o["erfolg"]!);
        Assert.Equal("messband", (string)o["varianten"]!["messen"]!);
        Assert.Contains("Messung nicht von Wand zu Wand", (string)o["notiz"]!);
        // andere Aufgaben zählen Messungen nicht
        var d = P.Starten(P.Drehen90, null);
        P.Beginnen(d, 0);
        P.MessenDaneben(d, "x");
        Assert.Equal(0, d.Fehlgriffe);
    }
}
