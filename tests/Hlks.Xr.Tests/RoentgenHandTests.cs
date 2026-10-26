// Quest App P5b: Röntgen-Hand beim Begehen – Geste (offen, Handfläche nach vorn), Ein- und Ausschalten mit Haltezeit, Kegel (Winkel, Reichweite), welche Wände und
// Decken er trifft (Kette von Kugeln gegen die Dreiecke), der Boden unter den Füssen bleibt, neu gerechnet nur bei Bewegung; Bereiche zusammen mit dem Durchgang.

using Hlks.Xr.Modell;
using static Hlks.Xr.Tests.HandHilfen;
using static Hlks.Xr.Tests.ModellHilfen;
using static Hlks.Xr.Tests.T;

namespace Hlks.Xr.Tests;

public class RoentgenHandTests
{
    static readonly Vec3 Kopf = V(0, 1.6, 0);
    static readonly Vec3 Hoch = V(0, 1, 0), Vor = V(0, 0, -1);

    /// <summary>«Halt»-Hand rechts auf Brusthöhe: Finger nach oben, Handfläche nach vorn (−z).</summary>
    static Dictionary<string, Vec3> Halt(bool offen = true) => Hand("right", V(0.2, 1.3, -0.4), Hoch, Vor, offen);

    [Fact]
    public void Geste_offen_Handflaeche_nach_vorn()
    {
        Assert.True(RoentgenHand.Geste(Halt(), "right", Kopf, false));
        Assert.False(RoentgenHand.Geste(Halt(offen: false), "right", Kopf, false));                       // Faust
        Assert.False(RoentgenHand.Geste(Hand("right", V(0.2, 1.3, -0.4), Hoch, V(0, 0, 1)), "right", Kopf, false));   // Handfläche zum Gesicht
        Assert.False(RoentgenHand.Geste(Hand("right", V(0.25, 0.9, 0), V(0, -1, 0), V(-1, 0, 0)), "right", Kopf, false));   // hängende Hand
        Assert.False(RoentgenHand.Geste(Hand("right", V(0.2, 1.1, -0.4), Vor, Hoch), "right", Kopf, false));   // Handfläche nach oben vor der Brust
        // die erhobene Hand, Handfläche nach oben: durch die Decke
        Assert.True(RoentgenHand.Geste(Hand("right", V(0.1, 2.0, -0.2), Vor, Hoch), "right", Kopf, false));
        var pinch = Halt();
        pinch["thumb-tip"] = pinch["index-finger-tip"] + V(0.005, 0, 0);
        Assert.False(RoentgenHand.Geste(pinch, "right", Kopf, false));
        Assert.False(RoentgenHand.Geste(null, "right", Kopf, false));
        // 60° neben vorn: nicht neu, aber gehalten
        var n60 = V(0, -Math.Sin(60 * Grad), -Math.Cos(60 * Grad));
        var f60 = V(0, Math.Cos(60 * Grad), -Math.Sin(60 * Grad));
        var schraeg = Hand("right", V(0, 1.6, -0.5), f60, n60);
        Assert.False(RoentgenHand.Geste(schraeg, "right", Kopf, false));
        Assert.True(RoentgenHand.Geste(schraeg, "right", Kopf, true));
    }

    [Fact]
    public void Ein_nach_kurzem_Halten_aus_nach_kurzer_Pause_Kegel_aus_den_Stellschrauben()
    {
        var z = new RoentgenZustand();
        Assert.Null(RoentgenHand.Schritt(z, 0, Halt(), "right", Kopf));
        Assert.Null(RoentgenHand.Schritt(z, 0.1, Halt(), "right", Kopf));
        var k = RoentgenHand.Schritt(z, 0.2, Halt(), "right", Kopf)!;
        Assert.True(z.Aktiv);
        Assert.True(k.Spitze.Abstand(Mitte(V(0.2, 1.3, -0.4), Hoch)) < 1e-9, "Spitze in der Mitte der Handfläche");
        Assert.True(k.Achse.Abstand(Vor) < 1e-9, "Achse = Normale der Handfläche");
        Nah(k.HalbwinkelRad, 20 * Grad, 1e-12);
        Nah(k.ReichweiteM, 6);
        // kurz keine Geste: bleibt (der letzte Kegel), nach 0.25 s aus
        Assert.Same(k, RoentgenHand.Schritt(z, 0.3, Halt(offen: false), "right", Kopf));
        Assert.NotNull(RoentgenHand.Schritt(z, 0.5, null, "right", Kopf));
        Assert.Null(RoentgenHand.Schritt(z, 0.56, null, "right", Kopf));
        Assert.False(z.Aktiv);
        // Stellschrauben: Winkel 60° ganz, Reichweite 10 m
        var z2 = new RoentgenZustand();
        RoentgenHand.Schritt(z2, 0, Halt(), "right", Kopf, new RoentgenEinstellungen { WinkelGrad = 60, ReichweiteM = 10 });
        var k2 = RoentgenHand.Schritt(z2, 0.25, Halt(), "right", Kopf, new RoentgenEinstellungen { WinkelGrad = 60, ReichweiteM = 10 })!;
        Nah(k2.HalbwinkelRad, 30 * Grad, 1e-12);
        Nah(k2.ReichweiteM, 10);
    }

    [Fact]
    public void Kegel_Geometrie()
    {
        var k = new RoentgenKegel(V(0, 1.4, 0), Vor, 20 * Grad, 6);
        Assert.True(k.Enthaelt(V(0, 1.4, -3)));
        Assert.True(k.Enthaelt(V(1, 1.4, -3)));                                  // tan 20° · 3 = 1.09
        Assert.False(k.Enthaelt(V(1.2, 1.4, -3)));
        Assert.False(k.Enthaelt(V(0, 1.4, -6.5)));                               // jenseits der Reichweite
        Assert.False(k.Enthaelt(V(0, 1.4, 1)));                                  // hinter der Hand
        Assert.True(k.AbstandMantel(V(0, 1.4, -3)) < 0);
        Nah(k.AbstandMantel(V(0, 1.4, 2)), 2, 1e-12);                             // hinter der Spitze: Abstand zur Spitze
        Assert.True(RoentgenHand.KugelRadius(k, 0.125) >= RoentgenHand.SchrittM / 2);
    }

    /// <summary>EG: W1 3.8 m vor der Hand, W2 4 m rechts, W3 20 m weit, W4 hinter einem, ein Deckenfeld (2 × 2 m) über einem, Boden unter den Füssen; 1.OG: eine Wand
    /// vor einem.</summary>
    static ModellSzene Szene() => new(
    [
        (Datei(null, Quadernetz("architektur",
            ("W1", "wand", V(-2, 0, -4.2), V(2, 2.6, -4)),
            ("W2", "wand", V(4, 0, -1.5), V(4.2, 2.6, 0.5)),
            ("W3", "wand", V(-2, 0, -20.2), V(2, 2.6, -20)),
            ("W4", "wand", V(-2, 0, 3), V(2, 2.6, 3.2)),
            ("D", "decke", V(-1, 2.6, -1), V(1, 2.8, 1)),
            ("B", "decke", V(-10, -0.3, -10), V(10, 0, 10)),
            ("L", "leitung", V(0, 0, -6), V(0.1, 0.1, -5)))), "eg", "modell"),
        (Datei(null, Quadernetz("architektur", ("O1", "wand", V(-2, 2.8, -4.2), V(2, 5.4, -4)))), "og", "modell"),
    ]);

    [Fact]
    public void Trifft_Waende_und_Decken_im_Kegel_nicht_daneben_nicht_weiter_nicht_dahinter()
    {
        var s = Szene();
        var form = (string id) => (DreieckForm.Aus([.. s.Ecken(s.Elemente["modell:" + id])])!, s.Elemente["modell:" + id].Box);
        var k = new RoentgenKegel(V(0, 1.4, -0.2), Vor, 20 * Grad, 6);
        foreach (var (id, soll) in new[] { ("W1", true), ("W2", false), ("W3", false), ("W4", false) })
        {
            var (f, b) = form(id);
            Assert.True(RoentgenHand.Trifft(k, f, b) == soll, id);
        }
        // nach oben gerichtet: die Decke
        var oben = new RoentgenKegel(V(0, 1.9, -0.2), Hoch, 20 * Grad, 6);
        var (fd, bd) = form("D");
        Assert.True(RoentgenHand.Trifft(oben, fd, bd));
        Assert.False(RoentgenHand.Moeglich(k, s.Elemente["modell:W3"].Box));
    }

    [Fact]
    public void Verfolger_rechnet_nur_bei_Bewegung_nach_Geschoss_ohne_den_Boden()
    {
        var s = Szene();
        var v = new RoentgenVerfolger(s);
        var k = new RoentgenKegel(V(0, 1.4, -0.2), Vor, 20 * Grad, 6);
        Assert.True(v.Pruefen(k, "eg", 0));
        Assert.Equal(new HashSet<string> { "modell:W1" }, v.Nah);                  // die Leitung L ist kein Bauteil des Durchgangs; der Boden B bleibt
        Assert.False(v.Pruefen(k with { Spitze = V(0.01, 1.4, -0.2) }, "eg", 0));  // 1 cm: nicht neu gerechnet
        Assert.True(v.Pruefen(k with { Achse = V(1, 0, 0), Spitze = V(0, 1.4, -0.5) }, "eg", 0));
        Assert.Equal(new HashSet<string> { "modell:W2" }, v.Nah);
        // flach nach vorn unten: ohne Boden-Grenze käme der Boden dazu
        var unten = new RoentgenKegel(V(0, 1.2, -0.2), V(0, -0.5, -1).Einheit(), 20 * Grad, 6);
        v.Pruefen(unten, "eg", null);
        Assert.Contains("modell:B", v.Nah);
        v.Pruefen(unten, "eg", 0);
        Assert.DoesNotContain("modell:B", v.Nah);
        // anderes Geschoss: nur dessen Bauteile
        Assert.True(v.Pruefen(k with { Spitze = V(0, 4.2, -0.2) }, "og", 2.8));
        Assert.Equal(new HashSet<string> { "modell:O1" }, v.Nah);
        Assert.True(v.Gemessen >= 1);
        // keine Röntgen-Hand: leer
        Assert.True(v.Pruefen(null));
        Assert.Empty(v.Nah);
        Assert.False(v.Pruefen(null));
        // Bereiche je Netz für Durchgang und Röntgen-Hand zusammen: jedes Element einmal
        var b = DurchgangVerfolger.BereicheJeNetz(s, ["modell:W1", "modell:W2", "modell:W1"]);
        Assert.Equal(2, b.Values.Sum(l => l.Count));
    }
}
