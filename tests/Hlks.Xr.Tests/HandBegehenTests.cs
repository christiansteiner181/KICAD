// Quest App P4a: Begehen mit den Händen (HandBegehen) – Teleport mit Zeigen und Pinch, Drehen mit Pinch und Wisch, Abbruch – und die Abbildung der
// WebXR-Gelenknamen auf StereoKit (HandGelenke).

using static Hlks.Xr.Tests.T;

namespace Hlks.Xr.Tests;

public class HandBegehenTests
{
    const double Gieren = Math.PI / 2;      // Blick nach −z (Norden); rechts = +x
    static readonly Vec3 Kopf = V(0, 1.7, 0);

    /// <summary>Rechte Hand: Grundgelenk des Zeigefingers bei p, Daumen- und Zeigefingerspitze im Abstand «spalt» (m).</summary>
    static Dictionary<string, Vec3> Hand(Vec3 p, double spalt) => new()
    {
        ["wrist"] = p + V(0, 0, 0.08),
        ["index-finger-phalanx-proximal"] = p,
        ["index-finger-tip"] = p + V(0, -0.02, -0.05),
        ["thumb-tip"] = p + V(spalt, -0.02, -0.05),
    };

    static List<BegehenEreignis> Lauf(HandBegehenZustand z, IEnumerable<(double T, Dictionary<string, Vec3>? G)> bilder, double boden = 0)
    {
        var alle = new List<BegehenEreignis>();
        foreach (var (t, g) in bilder) alle.AddRange(HandBegehen.Schritt(z, t, "right", g, Kopf, Gieren, boden));
        return alle;
    }

    static readonly Vec3 Zeigen = V(0.2, 1.2, -0.4);   // Hand vor dem Körper, nach unten vorn

    [Fact]
    public void Strahl_von_der_Schulter_durch_die_Hand()
    {
        var (u, r) = HandBegehen.Strahl(Kopf, Gieren, Zeigen, "right")!.Value;
        Assert.Equal(Zeigen, u);
        Nah(r.Laenge, 1, 1e-12);
        var erwartet = (Zeigen - V(HandBegehen.SchulterSeitlich, 1.7 - HandBegehen.SchulterUnterKopf, 0)).Einheit();
        Nah(r.X, erwartet.X); Nah(r.Y, erwartet.Y); Nah(r.Z, erwartet.Z);
        var (_, l) = HandBegehen.Strahl(Kopf, Gieren, V(-0.2, 1.2, -0.4), "left")!.Value;
        Nah(l.X, -r.X); Nah(l.Y, r.Y);   // gespiegelt
        Assert.Null(HandBegehen.Strahl(Kopf, Gieren, V(double.NaN, 0, 0), "right"));
        var rechts = HandBegehen.Rechts(Gieren);
        Nah(rechts.X, 1); Nah(rechts.Z, 0, 1e-12);
    }

    [Fact]
    public void Teleport_Pinch_halten_zeigt_das_Ziel_Loslassen_springt()
    {
        var z = new HandBegehenZustand();
        var bilder = Enumerable.Range(0, 30).Select(i => (i / 90.0, (Dictionary<string, Vec3>?)Hand(Zeigen, 0.01)))
            .Append((30 / 90.0, Hand(Zeigen, 0.06)));
        var ev = Lauf(z, bilder);
        Assert.Equal(BegehenArt.PinchAn, ev[0].Art);
        var ziele = ev.Where(e => e.Art == BegehenArt.Ziel).ToList();
        Assert.Equal(30, ziele.Count);
        var ziel = ziele[^1].Punkt!.Value;
        Assert.Equal(0, ziel.Y);
        Assert.True(ziel.Z < -1.5 && ziel.Z > -2.2, $"{ziel}");   // vorn auf dem Boden
        var tp = Assert.Single(ev, e => e.Art == BegehenArt.Teleport);
        Assert.Equal(ziel, tp.Punkt);
        Assert.Equal(BegehenArt.PinchAus, ev[^1].Art);
        Assert.False(z.Pinch);
        Assert.Null(z.Ziel);
        // Boden höher (anderes Geschoss): Ziel auf seiner Höhe
        var ev2 = Lauf(new HandBegehenZustand(), bilder.Select(b => (b.Item1, b.Item2)), boden: 0.5);
        Assert.Equal(0.5, Assert.Single(ev2, e => e.Art == BegehenArt.Teleport).Punkt!.Value.Y);
    }

    [Fact]
    public void Beim_Loslassen_gilt_das_Ziel_von_0_1_s_vorher()
    {
        var z = new HandBegehenZustand();
        var bilder = new List<(double, Dictionary<string, Vec3>?)>();
        for (int i = 0; i < 30; i++) bilder.Add((i / 90.0, Hand(Zeigen, 0.01)));
        // in den letzten 0.05 s wandert der Strahl nach oben (Öffnen der Finger): kein Ziel mehr
        for (int i = 30; i < 35; i++) bilder.Add((i / 90.0, Hand(V(0.2, 1.75, -0.4), 0.03)));
        bilder.Add((35 / 90.0, Hand(V(0.2, 1.75, -0.4), 0.06)));
        var ev = Lauf(z, bilder);
        Assert.Contains(ev, e => e.Art == BegehenArt.Ziel && e.Punkt is null);
        Assert.Single(ev, e => e.Art == BegehenArt.Teleport);
    }

    [Fact]
    public void Kurzer_Pinch_und_Zeigen_ins_Leere_springen_nicht()
    {
        var kurz = Lauf(new HandBegehenZustand(), [(0, Hand(Zeigen, 0.01)), (0.05, Hand(Zeigen, 0.01)), (0.1, Hand(Zeigen, 0.06))]);
        Assert.DoesNotContain(kurz, e => e.Art == BegehenArt.Teleport);
        Assert.Contains(kurz, e => e.Art == BegehenArt.Abbruch);
        var oben = V(0.2, 1.9, -0.4);   // nach oben gezeigt
        var leer = Lauf(new HandBegehenZustand(), Enumerable.Range(0, 30).Select(i => (i / 90.0, (Dictionary<string, Vec3>?)Hand(oben, 0.01))).Append((0.4, Hand(oben, 0.06))));
        Assert.All(leer.Where(e => e.Art == BegehenArt.Ziel), e => Assert.Null(e.Punkt));
        Assert.DoesNotContain(leer, e => e.Art == BegehenArt.Teleport);
        Assert.Contains(leer, e => e.Art == BegehenArt.Abbruch);
        // Hysterese: 3 cm Spalt bleibt Pinch (los erst über 4 cm), beginnt aber keinen
        Assert.DoesNotContain(Lauf(new HandBegehenZustand(), [(0, Hand(Zeigen, 0.03))]), e => e.Art == BegehenArt.PinchAn);
    }

    [Fact]
    public void Pinch_und_schneller_Wisch_dreht_in_30_Grad_Schritten()
    {
        foreach (var (richtung, schritt) in new[] { (1.0, -1), (-1.0, 1) })
        {
            var z = new HandBegehenZustand();
            var bilder = new List<(double, Dictionary<string, Vec3>?)>();
            for (int i = 0; i < 10; i++) bilder.Add((i / 90.0, Hand(Zeigen, 0.01)));
            for (int i = 0; i <= 18; i++) bilder.Add(((10 + i) / 90.0, Hand(Zeigen + V(richtung * 0.25 * i / 18, 0, 0), 0.01)));   // 25 cm in 0.2 s
            bilder.Add((40 / 90.0, Hand(Zeigen + V(richtung * 0.25, 0, 0), 0.06)));
            var ev = Lauf(z, bilder);
            var d = Assert.Single(ev, e => e.Art == BegehenArt.Drehen);
            Assert.Equal(schritt, d.Schritt);   // nach rechts gewischt = nach rechts drehen (−1, wie der rechte Stick)
            Assert.DoesNotContain(ev, e => e.Art is BegehenArt.Teleport or BegehenArt.Abbruch);   // nach dem Drehen springt dieser Pinch nicht
            Assert.Equal(BegehenArt.PinchAus, ev[^1].Art);
        }
        // langsames Zielen (10 cm in 1 s) dreht nicht
        var langsam = new List<(double, Dictionary<string, Vec3>?)>();
        for (int i = 0; i <= 90; i++) langsam.Add((i / 90.0, Hand(Zeigen + V(0.1 * i / 90, 0, 0), 0.01)));
        langsam.Add((1.1, Hand(Zeigen + V(0.1, 0, 0), 0.06)));
        var ev2 = Lauf(new HandBegehenZustand(), langsam);
        Assert.DoesNotContain(ev2, e => e.Art == BegehenArt.Drehen);
        Assert.Single(ev2, e => e.Art == BegehenArt.Teleport);
    }

    [Fact]
    public void Hand_verloren_waehrend_des_Pinch_bricht_ab()
    {
        var z = new HandBegehenZustand();
        var ev = Lauf(z, [(0, Hand(Zeigen, 0.01)), (0.3, Hand(Zeigen, 0.01)), (0.31, null)]);
        Assert.Equal(BegehenArt.Abbruch, ev[^1].Art);
        Assert.DoesNotContain(ev, e => e.Art == BegehenArt.Teleport);
        Assert.Null(z.Strahl);
        Assert.Empty(HandBegehen.Schritt(z, 0.4, "right", new Dictionary<string, Vec3> { ["wrist"] = V(0, 1, 0) }, Kopf, Gieren, 0));   // unvollständig
    }

    // ---------------------------------------------------------------------------------------------- Gelenke

    [Fact]
    public void Gelenke_WebXR_auf_StereoKit()
    {
        Assert.Equal(25, HandGelenke.WebXr.Count);
        Assert.Equal(25, HandGelenke.WebXr.Distinct().Count());
        Assert.Equal("wrist", HandGelenke.WebXr[0]);
        Assert.Equal("pinky-finger-tip", HandGelenke.WebXr[^1]);
        Assert.Equal(24, HandGelenke.StereoKit.Count);
        Assert.Equal(HandGelenke.WebXr.Skip(1).Order(), HandGelenke.StereoKit.Keys.Order());
        Assert.Equal(24, HandGelenke.StereoKit.Values.Distinct().Count());
        Assert.Equal((0, 4), HandGelenke.StereoKit["thumb-tip"]);
        Assert.Equal((0, 1), HandGelenke.StereoKit["thumb-metacarpal"]);       // beim Daumen sind Root und KnuckleMajor gleich
        Assert.Equal((1, 0), HandGelenke.StereoKit["index-finger-metacarpal"]);
        Assert.Equal((1, 1), HandGelenke.StereoKit["index-finger-phalanx-proximal"]);
        Assert.Equal((2, 1), HandGelenke.StereoKit["middle-finger-phalanx-proximal"]);   // Handgrösse (XrTabletLogik.HandGroesse)
        Assert.Equal((4, 4), HandGelenke.StereoKit["pinky-finger-tip"]);
        Assert.All(HandGelenke.StereoKit.Values, v => Assert.InRange(v.Finger, 0, 4));

        var g = HandGelenke.AusStereoKit((f, j) => f == 3 && j == 2 ? V(double.NaN, 0, 0) : V(f, j, 0), V(9, 9, 9));
        Assert.Equal(24, g.Count);   // ein Gelenk ohne gültige Lage fehlt
        Assert.Equal(V(9, 9, 9), g["wrist"]);
        Assert.Equal(V(0, 4, 0), g["thumb-tip"]);
        Assert.False(g.ContainsKey("ring-finger-phalanx-intermediate"));
        // die Regeln des Prototyps finden ihre Gelenke
        Assert.True(XrTabletLogik.GelenkeVollstaendig(HandGelenke.AusStereoKit((f, j) => V(f * 0.02, j * 0.03, 0), V(0, -0.05, 0))));
    }
}
