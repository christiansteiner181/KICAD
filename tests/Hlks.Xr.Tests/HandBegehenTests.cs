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

    static List<BegehenEreignis> Lauf(HandBegehenZustand z, IEnumerable<(double T, Dictionary<string, Vec3>? G)> bilder, double boden = 0,
        Func<double, (Vec3, Vec3)?>? zeigestrahl = null, HandBegehenEinstellungen? einstellungen = null)
    {
        var alle = new List<BegehenEreignis>();
        foreach (var (t, g) in bilder) alle.AddRange(HandBegehen.Schritt(z, t, "right", g, Kopf, Gieren, boden, zeigestrahl?.Invoke(t), einstellungen));
        return alle;
    }

    /// <summary>Pinch über «bilder» Bilder (90 Hz) an derselben Stelle, dann Loslassen.</summary>
    static IEnumerable<(double, Dictionary<string, Vec3>?)> Halten(int bilder, Vec3? hand = null) =>
        Enumerable.Range(0, bilder).Select(i => (i / 90.0, (Dictionary<string, Vec3>?)Hand(hand ?? Zeigen, 0.01)))
            .Append((bilder / 90.0, Hand(hand ?? Zeigen, 0.06)));

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
        var bilder = Halten(45).ToList();   // 0.5 s: Ruhezeit 0.25 s vor «Loslassen − 0.1 s» ist abgedeckt
        var ev = Lauf(z, bilder);
        Assert.Equal(BegehenArt.PinchAn, ev[0].Art);
        var ziele = ev.Where(e => e.Art == BegehenArt.Ziel).ToList();
        Assert.Equal(45, ziele.Count);
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
        foreach (var (richtung, schritt) in new[] { (1.0, 1), (-1.0, -1) })
        {
            var z = new HandBegehenZustand();
            var bilder = new List<(double, Dictionary<string, Vec3>?)>();
            for (int i = 0; i < 10; i++) bilder.Add((i / 90.0, Hand(Zeigen, 0.01)));
            for (int i = 0; i <= 18; i++) bilder.Add(((10 + i) / 90.0, Hand(Zeigen + V(richtung * 0.25 * i / 18, 0, 0), 0.01)));   // 25 cm in 0.2 s
            bilder.Add((40 / 90.0, Hand(Zeigen + V(richtung * 0.25, 0, 0), 0.06)));
            var ev = Lauf(z, bilder);
            var d = Assert.Single(ev, e => e.Art == BegehenArt.Drehen);
            Assert.Equal(schritt, d.Schritt);   // umgekehrt (Christian 23.10.2026): nach rechts gewischt = nach links drehen (+1), die Welt folgt der Hand
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

    /// <summary>Ein Wisch über 0.2 s ab Zeit t0 (Pinch gehalten), 25 cm in Richtung richtung (+1 = nach rechts), Hand am Ende bei ende.</summary>
    static IEnumerable<(double, Dictionary<string, Vec3>?)> Wisch(double t0, Vec3 start, double richtung) =>
        Enumerable.Range(0, 19).Select(i => (t0 + i / 90.0, (Dictionary<string, Vec3>?)Hand(start + V(richtung * 0.25 * i / 18, 0, 0), 0.01)));

    /// <summary>Pinch gehalten, Hand ruhig bei p, Bilder (90 Hz) von t0 bis vor t1.</summary>
    static IEnumerable<(double, Dictionary<string, Vec3>?)> Ruhig(double t0, double t1, Vec3 p) =>
        Enumerable.Range(0, (int)Math.Round((t1 - t0) * 90)).Select(i => (t0 + i / 90.0, (Dictionary<string, Vec3>?)Hand(p, 0.01)));

    /// <summary>Wisch nach rechts, dann nach Pause «pause» (s) zurück nach links; Bilder lückenlos wie in der Brille.</summary>
    static List<(double, Dictionary<string, Vec3>?)> HinUndZurueck(double pause)
    {
        var b = new List<(double, Dictionary<string, Vec3>?)>();
        b.AddRange(Ruhig(0, 10 / 90.0, Zeigen));
        b.AddRange(Wisch(10 / 90.0, Zeigen, 1));                              // nach rechts (endet bei ~0.31 s)
        var zurueck = 10 / 90.0 + 0.2 + pause;
        b.AddRange(Ruhig(29 / 90.0, zurueck, Zeigen + V(0.25, 0, 0)));
        b.AddRange(Wisch(zurueck, Zeigen + V(0.25, 0, 0), -1));               // zurück nach links
        b.AddRange(Ruhig(zurueck + 0.2, zurueck + 0.4, Zeigen));
        b.Add((zurueck + 0.4, Hand(Zeigen, 0.06)));
        return b;
    }

    [Fact]
    public void Schnell_hintereinander_dreht_nur_eine_Richtung()
    {
        // Christian 23.10.2026: «schnellaufeinanderfolgende drehungen nur in eine richtung zulassen» – das Zurückholen der Hand dreht nicht zurück.
        // Zurück nach 0.6 s Pause: nach der Wischpause von XrWischLogik (0.5 s), aber innerhalb der Richtungssperre (1.0 s ab der Drehung)
        var ev = Lauf(new HandBegehenZustand(), HinUndZurueck(0.6));
        Assert.Equal([1], ev.Where(e => e.Art == BegehenArt.Drehen).Select(e => e.Schritt));
        Assert.DoesNotContain(ev, e => e.Art == BegehenArt.Teleport);

        // Stellschraube «begehen.drehen_sperre_s» = 0: die Rückbewegung dreht zurück (der Wisch wird also erkannt, nur die Sperre hält ihn)
        var ohne = new HandBegehenEinstellungen().MitStellschraube("begehen.drehen_sperre_s", System.Text.Json.JsonDocument.Parse("0").RootElement)!;
        Assert.Equal(0, ohne.DrehenSperreS);
        Assert.Equal([1, -1], Lauf(new HandBegehenZustand(), HinUndZurueck(0.6), einstellungen: ohne).Where(e => e.Art == BegehenArt.Drehen).Select(e => e.Schritt));

        // nach Ablauf der Sperre geht die Gegenrichtung wieder
        Assert.Equal([1, -1], Lauf(new HandBegehenZustand(), HinUndZurueck(1.2)).Where(e => e.Art == BegehenArt.Drehen).Select(e => e.Schritt));

        // gleiche Richtung zweimal schnell hintereinander dreht beide Male
        var z = new HandBegehenZustand();
        var b = new List<(double, Dictionary<string, Vec3>?)>();
        b.AddRange(Ruhig(0, 10 / 90.0, Zeigen));
        b.AddRange(Wisch(10 / 90.0, Zeigen, 1));
        b.AddRange(Ruhig(29 / 90.0, 0.9, Zeigen + V(0.25, 0, 0)));
        b.AddRange(Wisch(0.9, Zeigen + V(0.25, 0, 0), 1));
        b.Add((1.2, Hand(Zeigen + V(0.5, 0, 0), 0.06)));
        Assert.Equal([1, 1], Lauf(z, b).Where(e => e.Art == BegehenArt.Drehen).Select(e => e.Schritt));
    }

    // ---------------------------------------------------------------------------------------------- Nachtrag Brillentest 23.10.2026: «Springt ungewollt»

    [Fact]
    public void Nach_dem_Drehen_springt_auch_ruhiges_Halten_nicht()
    {
        var z = new HandBegehenZustand();
        var bilder = new List<(double, Dictionary<string, Vec3>?)>();
        for (int i = 0; i < 10; i++) bilder.Add((i / 90.0, Hand(Zeigen, 0.01)));
        for (int i = 0; i <= 18; i++) bilder.Add(((10 + i) / 90.0, Hand(Zeigen + V(0.25 * i / 18, 0, 0), 0.01)));
        for (int i = 29; i < 90; i++) bilder.Add((i / 90.0, Hand(Zeigen + V(0.25, 0, 0), 0.01)));   // danach 0.7 s ruhig auf den Boden gezeigt
        bilder.Add((1.0, Hand(Zeigen + V(0.25, 0, 0), 0.06)));
        var ev = Lauf(z, bilder);
        Assert.Single(ev, e => e.Art == BegehenArt.Drehen);
        Assert.DoesNotContain(ev, e => e.Art is BegehenArt.Teleport or BegehenArt.Abbruch);
    }

    [Fact]
    public void Gesprungen_wird_nur_nach_der_Ruhezeit_auf_gueltigem_Boden()
    {
        var oben = V(0.2, 1.9, -0.4);
        IEnumerable<(double, Dictionary<string, Vec3>?)> Erst_oben_dann_unten(int obenBilder, int untenBilder) =>
            Enumerable.Range(0, obenBilder + untenBilder).Select(i => (i / 90.0, (Dictionary<string, Vec3>?)Hand(i < obenBilder ? oben : Zeigen, 0.01)))
                .Append(((obenBilder + untenBilder) / 90.0, Hand(Zeigen, 0.06)));
        // 0.2 s auf dem Boden (Auswertung 0.1 s vor dem Loslassen: nur 0.1 s gültig) – kein Sprung
        var kurz = Lauf(new HandBegehenZustand(), Erst_oben_dann_unten(27, 18));
        Assert.DoesNotContain(kurz, e => e.Art == BegehenArt.Teleport);
        Assert.Equal(BegehenArt.Abbruch, kurz[^2].Art);
        // 0.5 s auf dem Boden – Sprung
        var lang = Lauf(new HandBegehenZustand(), Erst_oben_dann_unten(18, 45));
        Assert.Single(lang, e => e.Art == BegehenArt.Teleport);
        // die Anzeige weiss, ab wann Loslassen springt
        var z = new HandBegehenZustand();
        var bereit = new List<(double T, bool Bereit)>();
        foreach (var (t, g) in Halten(60).SkipLast(1)) { HandBegehen.Schritt(z, t, "right", g, Kopf, Gieren, 0); bereit.Add((t, z.Bereit)); }
        Assert.False(bereit.First(b => b.T < 0.2).Bereit);
        Assert.True(bereit.Where(b => b.T >= 0.26).All(b => b.Bereit));
    }

    [Fact]
    public void Unruhiges_Ziel_springt_nicht()
    {
        // die Hand wippt schnell auf und ab (kein Wisch, der ist waagrecht): das Ziel springt über mehr als den Umkreis
        var bilder = Enumerable.Range(0, 60).Select(i => (i / 90.0, (Dictionary<string, Vec3>?)Hand(Zeigen + V(0, 0.08 * Math.Sin(2 * Math.PI * 3 * i / 90.0), 0), 0.01)))
            .Append((60 / 90.0, Hand(Zeigen, 0.06)));
        var ev = Lauf(new HandBegehenZustand(), bilder);
        Assert.DoesNotContain(ev, e => e.Art is BegehenArt.Teleport or BegehenArt.Drehen);
        Assert.Contains(ev, e => e.Art == BegehenArt.Abbruch);
    }

    [Fact]
    public void Verlaesst_der_Strahl_den_Boden_ist_der_Pinch_abgebrochen()
    {
        var z = new HandBegehenZustand();
        var oben = V(0.2, 1.9, -0.4);
        var bilder = new List<(double, Dictionary<string, Vec3>?)>();
        for (int i = 0; i < 45; i++) bilder.Add((i / 90.0, Hand(Zeigen, 0.01)));       // 0.5 s auf dem Boden
        for (int i = 45; i < 63; i++) bilder.Add((i / 90.0, Hand(oben, 0.01)));         // 0.2 s nach oben (länger als 0.1 s)
        for (int i = 63; i < 120; i++) bilder.Add((i / 90.0, Hand(Zeigen, 0.01)));      // wieder ruhig auf den Boden
        bilder.Add((120 / 90.0, Hand(Zeigen, 0.06)));
        var ev = Lauf(z, bilder);
        var abbruch = ev.FindIndex(e => e.Art == BegehenArt.Abbruch);
        Assert.True(abbruch > 0);
        Assert.Single(ev, e => e.Art == BegehenArt.Abbruch);                                // sofort gemeldet, nicht noch einmal beim Loslassen
        Assert.DoesNotContain(ev.Skip(abbruch), e => e.Art is BegehenArt.Ziel or BegehenArt.Teleport);
        Assert.Equal(BegehenArt.PinchAus, ev[^1].Art);
        // kürzer als die Verzögerung (Öffnen der Finger) bricht nicht ab: siehe Beim_Loslassen_gilt_das_Ziel_von_0_1_s_vorher
    }

    [Fact]
    public void Der_Zeigestrahl_des_Systems_bestimmt_das_Ziel()
    {
        var z = new HandBegehenZustand();
        (Vec3, Vec3)? Aim(double _) => (V(1, 1.2, -1), V(0, -2, 0));   // senkrecht nach unten, nicht normiert
        var ev = Lauf(z, Halten(45).ToList(), zeigestrahl: Aim);
        var tp = Assert.Single(ev, e => e.Art == BegehenArt.Teleport).Punkt!.Value;
        Nah(tp.X, 1); Nah(tp.Y, 0); Nah(tp.Z, -1);   // unabhängig vom Grundgelenk des Zeigefingers
        HandBegehen.Schritt(z, 2, "right", Hand(Zeigen, 0.06), Kopf, Gieren, 0, Aim(2));
        Assert.True(z.StrahlVomSystem);
        Nah(z.Strahl!.Value.Richtung.Y, -1);
        HandBegehen.Schritt(z, 2.1, "right", Hand(Zeigen, 0.06), Kopf, Gieren, 0);
        Assert.False(z.StrahlVomSystem);   // ohne Zeigestrahl: Rückfall über die Schulter
        HandBegehen.Schritt(z, 2.2, "right", Hand(Zeigen, 0.06), Kopf, Gieren, 0, (V(0, 1, 0), V(0, 0, 0)));
        Assert.False(z.StrahlVomSystem);   // Richtung null: Rückfall
    }

    [Fact]
    public void Schwellen_als_Stellschrauben()
    {
        var e = HandBegehenEinstellungen.Vorgabe;
        Assert.Equal(0.25, e.ZielRuheS);
        Assert.Equal(HandBegehen.ZielRuheS, e.ZielRuheS);
        Assert.Equal(HandBegehen.ZielVerzoegerungS, e.ZielVerzoegerungS);
        System.Text.Json.JsonElement W(string json) => System.Text.Json.JsonDocument.Parse(json).RootElement;
        Assert.Equal(0.4, e.MitStellschraube("begehen.ziel_ruhe_s", W("0.4"))!.ZielRuheS);
        Assert.Equal(1.0, e.MitStellschraube("begehen.ziel_ruhe_radius_m", W("1"))!.ZielRuheRadiusM);
        Assert.Equal(0.05, e.MitStellschraube("begehen.ziel_verzoegerung_s", W("0.05"))!.ZielVerzoegerungS);
        Assert.Equal(0.25, (e with { ZielRuheS = 1 }).MitStellschraube("begehen.ziel_ruhe_s", W("null"))!.ZielRuheS);   // null: zurück auf die Vorgabe
        Assert.Null(e.MitStellschraube("begehen.ziel_ruhe_s", W("5")));     // ausserhalb 0 … 2
        Assert.Null(e.MitStellschraube("begehen.ziel_ruhe_s", W("\"x\"")));
        Assert.Null(e.MitStellschraube("pinch.schwelle", W("0.02")));       // gehört nicht hierher
        // ohne Ruhezeit springt schon ein Pinch von 0.2 s
        var ev = Lauf(new HandBegehenZustand(), Halten(18), einstellungen: e with { ZielRuheS = 0 });
        Assert.Single(ev, x => x.Art == BegehenArt.Teleport);
        Assert.DoesNotContain(Lauf(new HandBegehenZustand(), Halten(18)), x => x.Art == BegehenArt.Teleport);
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
