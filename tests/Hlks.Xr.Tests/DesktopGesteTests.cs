// Tests der Quest App V1c (Modul XR, XR-I «Desktop in der Brille»): Hineinziehen (rechte Hand offen, Handfläche nach links, links → rechts) zusammen mit dem
// gemeinsamen Wegwischen der App (V1a, Hlks.Xr.Bedienung.Wegwischen; dieselbe Haltung, rechts → links) so verdrahtet wie in der App (QuestApp: erst das Wegwischen,
// dann das Hineinziehen; Gegensperren über DesktopGesten.WegwischenRuht und NachWegwischen), mit dem synthetischen Generator des Gestenlabors (GestenGenerator,
// erfundene Hände; seit V1d mit beiden Händen für beide Gesten verdrahtet wie in der App, die linke Hand im Einzelnen in V1dTests).
// Geprüft: Auslösen, Folgen der Hand, Fehlauslösungen (Ausholen, Zurückholen in beide Richtungen, langsam, kurz, zwei Hände, linke Hand, Pinch,
// Hebegeste, Wegschieben mit zwei Händen, Röntgen-Hand, Zufall) und umgekehrt, dass die beiden Gesten weder Pinch + Wisch noch die Hebegeste bzw. das Wegschieben
// (Gestenlabor) auslösen.

using Hlks.Xr.Bedienung;
using Hlks.Xr.Desktop;
using Hlks.Xr.Gesten;
using Xunit.Abstractions;
using G = Hlks.Xr.Tests.GestenGenerator;

namespace Hlks.Xr.Tests;

public class DesktopGesteTests(ITestOutputHelper aus)
{
    static readonly Vec3 VornHoch = G.N(0, 0.3, 1);

    /// <summary>Hineinziehen: rechte Hand offen, Handfläche nach links, 35 cm von links nach rechts (Körperbezug x rechts, y oben, z vorn).</summary>
    static readonly GestenBewegung Hinein = u => (null, new Haltung(G.V(-0.10 + 0.35 * u, -0.25 + 0.03 * Math.Sin(Math.PI * u), 0.4), VornHoch, G.V(-1, 0, 0), G.Kr(0.1)));
    /// <summary>Wegwischen: wie GestenGenerator.WischLinks (Handfläche nach links, 35 cm nach links).</summary>
    static readonly GestenBewegung Weg = G.WischLinks;

    static GestenBewegung MitLinkerHand(GestenBewegung b, Haltung links) => u => (links, b(u).Rechts);

    static Aufnahme Vorzeigen(GestenBewegung b, double dauer = 0.45, double gieren = 0, int saat = 1, double ruhe = 0.4) =>
        G.Erzeugen(b, new Vorfuehrung { DauerS = dauer, GierenGrad = gieren, Saat = saat, BogenM = 0.015, RuheVorS = ruhe, RuheNachS = ruhe });

    static DesktopHaende Haende(GestenBild b) =>
        new(b.T, b.Rechts.Verfolgt ? b.Rechts.GelenkeMitNamen() : null, b.Links.Verfolgt ? b.Links.GelenkeMitNamen() : null, b.Gieren);

    /// <summary>
    /// Beide Gesten über eine Aufnahme wie in der App (ohne Sperre durch den Zustand des Bildschirms): je Bild erst das gemeinsame Wegwischen (die rechte Hand zählt
    /// nicht, solange es nach dem Hineinziehen ruht; nach einem Ereignis ruht das Hineinziehen), dann das Hineinziehen. Zeiten der Auslösungen, grösster Fortschritt
    /// des Hineinziehens, grösster Weg des Wegwischens.
    /// </summary>
    static (List<double> Hinein, List<double> Weg, double MaxHinein, double MaxWeg) Abspielen(Aufnahme a, DesktopEinstellungen? e = null, WegwischEinstellungen? w = null)
    {
        var g = new DesktopGesten(() => e ?? DesktopEinstellungen.Vorgabe, null, () => w ?? WegwischEinstellungen.Vorgabe);
        var wz = new WegwischZustand();
        var hi = new List<double>();
        var we = new List<double>();
        double mh = 0, mw = 0;
        foreach (var b in a.Bilder)
        {
            var h = Haende(b);
            // V1d: beide Hände wischen weg (QuestApp.WegwischenBild), beide ziehen herein (DesktopGesten)
            var ruht = g.WegwischenRuht(b.T);
            var rechts = ruht ? null : HandBeobachtung.Aus(h.Rechts, "right");
            var links = ruht ? null : HandBeobachtung.Aus(h.Links, "left");
            var ws = Wegwischen.Schritt(wz, b.T, rechts, links, b.Gieren, w);
            if (ws.Ereignis) { we.Add(b.T); g.NachWegwischen(b.T); }
            mw = Math.Max(mw, ws.Weg);
            var s = g.Schritt(h, erlaubt: true);
            if (s.Ausgeloest) hi.Add(b.T);
            mh = Math.Max(mh, s.Fortschritt);
        }
        return (hi, we, mh, mw);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(-135)]
    [InlineData(180)]
    public void Hineinziehen_loest_einmal_aus_in_jeder_Koerperrichtung(double gieren)
    {
        var (hi, we, _, _) = Abspielen(Vorzeigen(Hinein, gieren: gieren));
        Assert.Single(hi);
        Assert.Empty(we);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(180)]
    public void Wegwischen_loest_einmal_aus_in_jeder_Koerperrichtung(double gieren)
    {
        var (hi, we, _, _) = Abspielen(Vorzeigen(Weg, gieren: gieren));
        Assert.Single(we);
        Assert.Empty(hi);
    }

    [Fact]
    public void Mit_ruhender_linker_Hand_loest_es_aus()
    {
        Assert.Single(Abspielen(Vorzeigen(MitLinkerHand(Hinein, G.RuheLinks))).Hinein);
        Assert.Single(Abspielen(Vorzeigen(MitLinkerHand(Weg, G.RuheLinks))).Weg);
    }

    [Fact]
    public void Der_Fortschritt_folgt_der_Hand_und_faellt_beim_Abbruch_zurueck()
    {
        // halber Wisch (12 cm gemächlich – V1e: zügig wäre es ein Anstoss –, dann stehen bleiben): der Bildschirm folgt bis etwa zur Hälfte, ausgelöst wird nicht; nach
        // dem Zeitfenster fällt der Fortschritt auf 0
        GestenBewegung halb = u => (null, new Haltung(G.V(-0.10 + 0.12 * u, -0.25, 0.4), VornHoch, G.V(-1, 0, 0), G.Kr(0.1)));
        var a = Vorzeigen(halb, dauer: 0.6, ruhe: 1.0);
        var g = new DesktopGesten(() => DesktopEinstellungen.Vorgabe);
        var verlauf = a.Bilder.Select(b => (b.T, S: g.Schritt(Haende(b), true))).ToList();
        Assert.DoesNotContain(verlauf, x => x.S.Ausgeloest);
        var max = verlauf.Max(x => x.S.Fortschritt);
        aus.WriteLine($"halber Wisch: grösster Fortschritt {max:0.00}");
        Assert.InRange(max, 0.35, 0.65);   // (0.12 − 0.04) / 0.16 = 0.5
        Assert.True(verlauf.First(x => x.S.Fortschritt == max).S.Laeuft);
        Assert.Equal(0, verlauf[^1].S.Fortschritt, 6);
        Assert.False(verlauf[^1].S.Laeuft);
        // Fortschritt steigt mit dem Weg (monoton bis zum Höchstwert; gemächlich ist der Schritt je Bild kleiner als das Rauschen der Hand von 5 mm ≈ 0.03)
        var bisMax = verlauf.TakeWhile(x => x.S.Fortschritt < max).Select(x => x.S.Fortschritt).ToList();
        for (int i = 1; i < bisMax.Count; i++) Assert.True(bisMax[i] >= bisMax[i - 1] - 0.07, $"Bild {i}");
    }

    [Fact]
    public void Langsam_oder_kurz_loest_nicht_aus()
    {
        Assert.Empty(Abspielen(Vorzeigen(Hinein, dauer: 1.6)).Hinein);   // 35 cm in 1.6 s: 0.22 m/s
        GestenBewegung kurz = u => (null, new Haltung(G.V(-0.10 + 0.15 * u, -0.25, 0.4), VornHoch, G.V(-1, 0, 0), G.Kr(0.1)));
        GestenBewegung kurzWeg = u => (null, new Haltung(G.V(0.25 - 0.15 * u, -0.25, 0.4), VornHoch, G.V(-1, 0, 0), G.Kr(0.1)));
        // die ganze Bewegung (V1c): 15 cm zügig reichen nicht
        var ohne = WegwischEinstellungen.Vorgabe with { Anstoss = AnstossEinstellungen.Aus };
        Assert.Empty(Abspielen(Vorzeigen(kurz, dauer: 0.25), null, ohne).Hinein);
        Assert.Empty(Abspielen(Vorzeigen(kurzWeg, dauer: 0.25), null, ohne).Weg);
        // V1e (Christian 24.10.2026: «nur einen anstoss»): derselbe zügige kurze Schub löst aus; ein langsamer kurzer nicht
        Assert.Single(Abspielen(Vorzeigen(kurz, dauer: 0.25)).Hinein);
        Assert.Single(Abspielen(Vorzeigen(kurzWeg, dauer: 0.25)).Weg);
        Assert.Empty(Abspielen(Vorzeigen(kurz, dauer: 0.8)).Hinein);
        Assert.Empty(Abspielen(Vorzeigen(kurzWeg, dauer: 0.8)).Weg);
    }

    [Fact]
    public void Ausholen_vor_dem_Wisch_loest_die_andere_Geste_nicht_aus()
    {
        // Ausholen nach links (12 cm, gemächlich), dann Hineinziehen
        GestenBewegung ausholenLinks = u => (null, new Haltung(G.V(0.02 - 0.12 * u, -0.25, 0.4), VornHoch, G.V(-1, 0, 0), G.Kr(0.1)));
        var (a1, _) = G.Strom([(ausholenLinks, new Vorfuehrung { DauerS = 0.45, RuheVorS = 0.4, RuheNachS = 0.08 }), (Hinein, new Vorfuehrung { DauerS = 0.45, RuheVorS = 0.05, RuheNachS = 0.5 })],
            uebergangS: 0.05);
        var r1 = Abspielen(a1);
        Assert.Single(r1.Hinein);
        Assert.Empty(r1.Weg);
        // Ausholen nach rechts, dann Wegwischen
        GestenBewegung ausholenRechts = u => (null, new Haltung(G.V(0.13 + 0.12 * u, -0.25, 0.4), VornHoch, G.V(-1, 0, 0), G.Kr(0.1)));
        var (a2, _) = G.Strom([(ausholenRechts, new Vorfuehrung { DauerS = 0.45, RuheVorS = 0.4, RuheNachS = 0.08 }), (Weg, new Vorfuehrung { DauerS = 0.45, RuheVorS = 0.05, RuheNachS = 0.5 })],
            uebergangS: 0.05);
        var r2 = Abspielen(a2);
        Assert.Single(r2.Weg);
        Assert.Empty(r2.Hinein);
    }

    [Fact]
    public void Zurueckholen_der_Hand_nach_dem_Wisch_schiebt_nicht_gleich_wieder_hinaus()
    {
        // nach dem Hineinziehen die Hand sofort und zügig zurück nach links (30 cm in 0.5 s): fällt in die Sperre
        GestenBewegung zurueck = u => (null, new Haltung(G.V(0.25 - 0.30 * u, -0.25, 0.4), VornHoch, G.V(-1, 0, 0), G.Kr(0.1)));
        var (a, _) = G.Strom([(Hinein, new Vorfuehrung { DauerS = 0.45, RuheVorS = 0.4, RuheNachS = 0.05 }), (zurueck, new Vorfuehrung { DauerS = 0.5, RuheVorS = 0.05, RuheNachS = 0.6 })],
            uebergangS: 0.05);
        var r = Abspielen(a);
        Assert.Single(r.Hinein);
        Assert.Empty(r.Weg);
        // nach der Sperre ist ein Wegwischen wieder eine Geste
        var (b, _) = G.Strom([(Hinein, new Vorfuehrung { DauerS = 0.45, RuheVorS = 0.4, RuheNachS = 1.2 }), (Weg, new Vorfuehrung { DauerS = 0.45, RuheVorS = 0.3, RuheNachS = 0.5 })],
            uebergangS: 0.3);
        var r2 = Abspielen(b);
        Assert.Single(r2.Hinein);
        Assert.Single(r2.Weg);
        Assert.True(r2.Weg[0] > r2.Hinein[0] + 1.0);
    }

    [Fact]
    public void Zurueckholen_der_Hand_nach_dem_Wegwischen_zieht_nicht_gleich_wieder_herein()
    {
        // nach dem Wegwischen die Hand sofort und zügig zurück nach rechts (30 cm in 0.5 s, schneller als das Mindesttempo): fällt in die Gegensperre
        GestenBewegung zurueck = u => (null, new Haltung(G.V(-0.10 + 0.30 * u, -0.25, 0.4), VornHoch, G.V(-1, 0, 0), G.Kr(0.1)));
        var (a, _) = G.Strom([(Weg, new Vorfuehrung { DauerS = 0.45, RuheVorS = 0.4, RuheNachS = 0.05 }), (zurueck, new Vorfuehrung { DauerS = 0.5, RuheVorS = 0.05, RuheNachS = 0.6 })],
            uebergangS: 0.05);
        var r = Abspielen(a);
        Assert.Single(r.Weg);
        Assert.Empty(r.Hinein);
        // ohne die Gegensperre hätte dieselbe Bewegung hereingezogen (die Sperre wirkt, nicht die Schwellen)
        var g = new DesktopGesten(() => DesktopEinstellungen.Vorgabe);
        Assert.Contains(a.Bilder, b => g.Schritt(Haende(b), true).Ausgeloest);
    }

    [Fact]
    public void Zwei_Haende_linke_Hand_Pinch_und_andere_Handlagen_loesen_nicht_aus()
    {
        // beide Hände wischen gemeinsam nach rechts (die linke bewegt sich mit)
        GestenBewegung beide = u => (new Haltung(G.V(-0.35 + 0.35 * u, -0.25, 0.4), VornHoch, G.V(-1, 0, 0), G.Kr(0.1)), Hinein(u).Rechts);
        Assert.Empty(Abspielen(Vorzeigen(beide)).Hinein);
        // dieselbe Geste mit der linken Hand gespiegelt (also von rechts nach links, Handfläche nach rechts): seit V1d (Christian 24.10.2026: «wisch mit der linken
        // hand von rechts nach links soll in wieder raus schieben») ist das das Wegwischen der linken Hand – herein zieht sie nicht
        var links = G.Erzeugen(Hinein, new Vorfuehrung { DauerS = 0.45, Gespiegelt = true });
        var rl = Abspielen(links);
        Assert.Empty(rl.Hinein);
        Assert.Single(rl.Weg);
        // Pinch gehalten (das ist Pinch + Wisch: Drehen bzw. Blättern)
        GestenBewegung pinch = u => (null, new Haltung(G.V(-0.10 + 0.35 * u, -0.25, 0.4), VornHoch, G.V(-1, 0, 0), [0.3, 0.35, 0.7, 0.8, 0.8], 1));
        Assert.Empty(Abspielen(Vorzeigen(pinch)).Hinein);
        // Handfläche nach vorn (Röntgen-Hand) bzw. nach unten
        GestenBewegung vorn = u => (null, new Haltung(G.V(-0.10 + 0.35 * u, -0.2, 0.35), G.V(0, 1, 0), G.V(0, 0, 1), G.Kr(0.05)));
        GestenBewegung unten = u => (null, new Haltung(G.V(-0.10 + 0.35 * u, -0.2, 0.35), G.V(0, 0, 1), G.V(0, -1, 0), G.Kr(0.1)));
        Assert.Empty(Abspielen(Vorzeigen(vorn)).Hinein);
        Assert.Empty(Abspielen(Vorzeigen(unten)).Hinein);
        // Faust
        GestenBewegung faust = u => (null, new Haltung(G.V(-0.10 + 0.35 * u, -0.25, 0.4), VornHoch, G.V(-1, 0, 0), G.Kr(0.95)));
        Assert.Empty(Abspielen(Vorzeigen(faust)).Hinein);
    }

    [Fact]
    public void Hebegeste_Wegschieben_mit_zwei_Haenden_und_die_Testgesten_des_Labors_loesen_nicht_aus()
    {
        foreach (var t in G.Zehn)
            foreach (var (text, v) in G.Abwandlungen(t, gespiegelt: false).Take(5))
            {
                var r = Abspielen(G.Erzeugen(t.Bewegung, v));
                // Ausnahme: «Wisch links» des Labors ist genau die Haltung des Wegwischens (rechte Hand, Handfläche nach links)
                if (t.Id == "wisch-links") { Assert.Empty(r.Hinein); continue; }
                Assert.True(r.Hinein.Count == 0 && r.Weg.Count == 0, $"{t.Name}, {text}: Hinein {r.Hinein.Count}, Weg {r.Weg.Count}");
            }
    }

    [Fact]
    public void Zufallsbewegungen_loesen_kaum_aus()
    {
        int hinein = 0, weg = 0;
        for (int s = 0; s < 80; s++)
        {
            var r = Abspielen(G.Erzeugen(G.Zufall(4000 + s, mitLinks: s % 2 == 0), new Vorfuehrung { DauerS = 1.5, Saat = 5000 + s }));
            hinein += r.Hinein.Count;
            weg += r.Weg.Count;
        }
        aus.WriteLine($"Zufallsbewegungen (80 × 1.5 s): Hineinziehen {hinein}, Wegwischen {weg}");
        Assert.Equal(0, hinein + weg);   // feste Saat: am 24.10.2026 keine Fehlauslösung
    }

    [Fact]
    public void Umgekehrt_loesen_Hineinziehen_und_Wegwischen_weder_Pinch_und_Wisch_noch_Hebegeste_und_Wegschieben_aus()
    {
        var aufnahmen = new[] { Vorzeigen(Hinein), Vorzeigen(Weg), Vorzeigen(MitLinkerHand(Hinein, G.RuheLinks)), Vorzeigen(Hinein, gieren: 90) };
        // feste Pinch-Gesten (Drehen, Blättern, Etage, Wegwischen der Apps mit Pinch, P5a): der Wisch braucht den Pinch
        var pinchWisch = new WischRegel();
        foreach (var a in aufnahmen) Assert.Empty(pinchWisch.Abspielen(a));
        // Hebegeste und Wegschieben mit zwei Händen (Labor, vorgezeigte Beispiele): der Erkenner lehnt beide Gesten ab
        var live = new GestenLiveErkenner([G.Fertig(G.THebe), G.Fertig(G.TAuseinander)]);
        foreach (var a in aufnahmen)
        {
            live.Zuruecksetzen();
            foreach (var b in a.Bilder)
                if (live.Bild(b) is { Art: ErkennungsArt.Erkannt } ev) Assert.Fail($"{ev.Beste.GestenId} bei {ev.T:0.00} s");
        }
        // Konfliktprüfung des Labors mit dem Hineinziehen als «eigener» Geste: keine feste Pinch-Geste löst aus, Trennschärfe gegen Hebegeste und Auseinander > 1
        var beispiele = Enumerable.Range(0, 5).Select(i => AufnahmeHilfe.Zuschneiden(Vorzeigen(Hinein, dauer: 0.45 * (0.9 + 0.05 * i), gieren: -20 + 10 * i, saat: 60 + i), Haendigkeit.Rechts))
            .Where(z => z.Ok).Select(z => z.Aufnahme!).ToList();
        Assert.True(beispiele.Count >= 3);
        var bericht = GestenKonflikte.Pruefen(new EigeneGeste("hinein", "Hineinziehen", "DesktopHinein", Haendigkeit.Rechts, beispiele),
            [G.Fertig(G.THebe), G.Fertig(G.TAuseinander)], [pinchWisch]);
        aus.WriteLine($"Konfliktprüfung Hineinziehen: Trennschärfe {bericht.Trennschaerfe:0.00}, feste Auslösungen {bericht.Feste.Count}");
        Assert.Empty(bericht.Feste);
        Assert.True(bericht.Trennschaerfe > 1, $"Trennschärfe {bericht.Trennschaerfe}");
    }

    [Fact]
    public void Gedachte_Hand_des_Pruefjaufs_zieht_halb_dann_ganz_herein_und_wischt_weg()
    {
        // wie QuestApp.Desktop.cs: 12 cm in 0.25 s, 0.2 s halten (Prüfbild «Geste halb»), dann 23 cm in 0.3 s; später zurück – verdrahtet wie in der App
        // (WegwischenBild mit dem Bildschirm als einzigem Ziel, dann DesktopBildschirm.Bild)
        static double W(double u) { u = Math.Clamp(u, 0, 1); return u * u * u * (10 - 15 * u + 6 * u * u); }
        var kopf = new Vec3(0.2, 1.55, 0.4);
        var gieren = Math.PI / 2 + 0.3;
        var g = new DesktopGesten(() => DesktopEinstellungen.Vorgabe, null, () => WegwischEinstellungen.Vorgabe);
        var a = new DesktopAnsicht();
        var e = DesktopEinstellungen.Vorgabe;
        var m = DesktopMasse.Aus(e);
        var wz = new WegwischZustand();
        var zz = new WegwischZielZustand();
        double t = 0, halb = -1, wegHalb = -1;
        int hinein = 0, weg = 0;
        void Bild(double x, bool hand = true)
        {
            var h = new DesktopHaende(t, hand ? PruefHand.RechtsHandflaecheLinks(kopf, gieren, x) : null, null, gieren);
            // QuestApp.WegwischenBild
            var rechts = g.WegwischenRuht(t) ? null : HandBeobachtung.Aus(h.Rechts, "right");
            var ws = Wegwischen.Schritt(wz, t, rechts, gieren);
            if (ws.Ereignis) g.NachWegwischen(t);
            var k = a.Kandidat(m);
            var ziel = Wegwischen.ZielFuehren(zz, ws, k is null ? [] : [k], kopf);
            if (ziel == DesktopAnsicht.ZielId)
            {
                a.Mitlaufen(ws.Laeuft ? ws.Weg : 0);
                if (ws.Ereignis) { weg++; a.Ausblenden(t, kopf); }
            }
            // DesktopBildschirm.Bild
            var s = g.Schritt(h, a.HineinMoeglich);
            if (s.Ausgeloest) hinein++;
            a.Schritt(t, 1 / 72.0, kopf, gieren, s, e);
            t += 1 / 72.0;
        }
        for (int i = 0; i < 30; i++) Bild(-0.10);
        var t0 = t;
        // V1e: der halbe Weg gemächlich (12 cm in 0.7 s; zügig wäre es schon ein Anstoss), dann der Rest
        while (t - t0 < 0.9) Bild(-0.10 + 0.12 * W((t - t0) / 0.7));
        halb = a.Anteil;
        t0 = t;
        while (t - t0 < 0.35) Bild(0.02 + 0.23 * W((t - t0) / 0.3));
        for (int i = 0; i < 60; i++) Bild(0.25, hand: false);
        aus.WriteLine($"gedachte Hand: halb {halb:0.00}, eingeblendet {hinein}×");
        Assert.Equal(1, hinein);
        Assert.Equal(DesktopPhase.Da, a.Phase);
        Assert.InRange(halb, 0.3, 0.75);
        // Wegwischen nach der Sperre: halb (gemächlich), dann ganz
        t0 = t;
        while (t - t0 < 1.75)
        {
            var tt = t - t0;
            Bild(tt < 0.9 ? 0.25 - 0.12 * W(tt / 0.7) : 0.13 - 0.23 * W((tt - 0.9) / 0.3), hand: tt <= 1.25);
            if (wegHalb < 0 && t - t0 >= 0.9) wegHalb = a.Anteil;
        }
        aus.WriteLine($"gedachte Hand: Wegwischen halb {wegHalb:0.00}");
        Assert.Equal(1, weg);
        Assert.Equal(1, hinein);
        Assert.InRange(wegHalb, 0.6, 0.95);   // läuft mit der Hand nach links mit (gleicher Winkel wie das Symbolfeld)
        Assert.Equal(DesktopPhase.Aus, a.Phase);
        // V1e, zweiter Durchgang des Prüflaufs: nur ein Anstoss (0.2 s stellen, 8 cm in 0.15 s), dann steht die Hand – herein bzw. hinaus mit Schwung
        for (int i = 0; i < 100; i++) Bild(-0.10, hand: false);
        t0 = t;
        while (t - t0 < 0.8) { var ta = t - t0 - 0.2; Bild(-0.10 + 0.08 * W(ta / 0.15), hand: ta < 0.4); }
        Assert.Equal(2, hinein);
        for (int i = 0; i < 150; i++) Bild(0.25, hand: false);
        Assert.Equal(DesktopPhase.Da, a.Phase);
        t0 = t;
        while (t - t0 < 1.5) { var ta = t - t0 - 0.2; Bild(0.25 - 0.08 * W(ta / 0.15), hand: ta < 0.45); }
        Assert.Equal(2, weg);
        Assert.Equal(DesktopPhase.Aus, a.Phase);
        Assert.True(XrTabletLogik.FaustVerhaeltnis(PruefHand.RechtsHandflaecheLinks(kopf, gieren, 0)) >= AnstossEinstellungen.Vorgabe.OffenAb);
        // die Hand ist offen, ohne Pinch, Handfläche nach links
        var hand = PruefHand.RechtsHandflaecheLinks(kopf, gieren, 0);
        Assert.True(XrTabletLogik.IstOffen(hand));
        Assert.False(XrTabletLogik.IstPinch(hand));
        Assert.True(XrTabletLogik.HandflaechenNormale(hand, "right")!.Value.Skalar(XrGesten.Rechts(gieren) * -1) > 0.99);
    }

    [Fact]
    public void Sperre_von_aussen_und_erlaubt_false_zaehlen_keine_Bewegung()
    {
        var a = Vorzeigen(Hinein);
        var z = new SeitenWischZustand();
        // nicht erlaubt (Hand an den Apps bzw. am Griff): nichts
        Assert.DoesNotContain(a.Bilder, b => SeitenWisch.Schritt(z, b.T, b.Rechts.GelenkeMitNamen(), null, b.Gieren, erlaubt: false).Ausgeloest);
        // gesperrt bis nach der Bewegung: nichts, auch nicht verspätet
        var z2 = new SeitenWischZustand();
        z2.Sperren(a.Bilder[^1].T - 0.3);
        Assert.DoesNotContain(a.Bilder, b => SeitenWisch.Schritt(z2, b.T, b.Rechts.GelenkeMitNamen(), null, b.Gieren).Ausgeloest);
        // NachWegwischen sperrt das Hineinziehen; WegwischenRuht nur nach dem Hineinziehen
        var g = new DesktopGesten(() => DesktopEinstellungen.Vorgabe);
        g.NachWegwischen(a.Bilder[^1].T - 0.3);
        Assert.DoesNotContain(a.Bilder, b => g.Schritt(Haende(b), true).Ausgeloest);
        Assert.False(g.WegwischenRuht(a.Bilder[^1].T));
        var g2 = new DesktopGesten(() => DesktopEinstellungen.Vorgabe);
        var bei = a.Bilder.First(b => g2.Schritt(Haende(b), true).Ausgeloest).T;
        Assert.True(g2.WegwischenRuht(bei + 0.9));
        Assert.False(g2.WegwischenRuht(bei + 1.01));
    }

    [Fact]
    public void Stellschrauben_aendern_die_Schwellen()
    {
        // ein kurzer Wisch (15 cm) löst mit Weg 0.12 aus, mit der Vorgabe 0.2 nicht
        GestenBewegung kurz = u => (null, new Haltung(G.V(-0.10 + 0.15 * u, -0.25, 0.4), VornHoch, G.V(-1, 0, 0), G.Kr(0.1)));
        var e = DesktopEinstellungen.Vorgabe with { Hinein = SeitenWischEinstellungen.Vorgabe with { WegM = 0.12 } };
        Assert.Single(Abspielen(Vorzeigen(kurz, dauer: 0.25), e).Hinein);
        // Handfläche 60° schräg nach vorn-links (Finger nach oben): mit der Vorgabe 50° (wegwischen.handflaeche_grad) nicht, mit 70° schon – dieselbe Haltung für
        // beide Richtungen
        GestenBewegung schraeg = u => (null, new Haltung(G.V(-0.10 + 0.35 * u, -0.25, 0.4), G.V(0, 1, 0), G.N(-0.5, 0, 0.866), G.Kr(0.1)));
        Assert.Equal(50, WegwischEinstellungen.Vorgabe.HandflaecheGrad);
        Assert.Equal(WegwischEinstellungen.Vorgabe.HandflaecheGrad, DesktopEinstellungen.Vorgabe.Hinein.HandflaecheGrad);
        Assert.Empty(Abspielen(Vorzeigen(schraeg)).Hinein);
        Assert.Single(Abspielen(Vorzeigen(schraeg), null, WegwischEinstellungen.Vorgabe with { HandflaecheGrad = 70 }).Hinein);
        Assert.Equal(70, DesktopEinstellungen.Vorgabe.HineinMit(WegwischEinstellungen.Vorgabe with { HandflaecheGrad = 70 }).HandflaecheGrad);
    }

    [Fact]
    public void Einstellungen_Schluessel_Grenzen_und_Zuruecksetzen()
    {
        static System.Text.Json.JsonElement W(string j) => System.Text.Json.JsonDocument.Parse(j).RootElement.Clone();
        var e = DesktopEinstellungen.Vorgabe;
        Assert.Equal(6, DesktopEinstellungen.Schluessel.Count);
        Assert.Equal(["hineinziehen.weg_m", "hineinziehen.tempo_ms", "desktop.breite_m", "desktop.abstand_m", "desktop.bildrate", "desktop.ruhe_bildrate"], DesktopEinstellungen.Schluessel);
        // das Wegwischen gehört V1a (wegwischen.*), nicht hierher; keine doppelten Schlüssel
        Assert.DoesNotContain(DesktopEinstellungen.Schluessel, s => s.StartsWith("wegwischen.", StringComparison.Ordinal) || s.StartsWith("gesten.", StringComparison.Ordinal));
        Assert.Equal(Labor.LaborWerte.Schluessel.Count, Labor.LaborWerte.Schluessel.Distinct().Count());
        Assert.All(DesktopEinstellungen.Schluessel, s => Assert.True(DesktopEinstellungen.Kennt(s)));
        Assert.All(DesktopEinstellungen.Schluessel, s => Assert.True(Labor.LaborWerte.Kennt(s), s));
        var n = e.MitStellschraube("desktop.breite_m", W("2.0"))!;
        Assert.Equal(2.0, n.BreiteM);
        Assert.Null(e.MitStellschraube("desktop.breite_m", W("9")));        // ausserhalb
        Assert.Null(e.MitStellschraube("desktop.breite_m", W("\"x\"")));    // kein Zahl
        Assert.Null(e.MitStellschraube("anzeige.skala", W("1")));           // gehört nicht hierher
        Assert.Equal(1.4, n.MitStellschraube("desktop.breite_m", W("null"))!.BreiteM);
        Assert.Equal(0.3, e.MitStellschraube("hineinziehen.weg_m", W("0.3"))!.Hinein.WegM);
        Assert.Equal(0.6, e.MitStellschraube("hineinziehen.tempo_ms", W("0.6"))!.Hinein.TempoMs);
        Assert.Null(e.MitStellschraube("wegwischen.weg_m", W("0.3")));          // gehört dem gemeinsamen Wegwischen (V1a)
        Assert.Null(e.MitStellschraube("gesten.wegwischen.weg_m", W("0.3")));   // alte Namen gibt es nicht mehr
        Assert.Equal(2.5, e.RuheBildrate);
        Assert.Equal(1.0, e.MitStellschraube("desktop.ruhe_bildrate", W("1"))!.RuheBildrate);
        Assert.Null(e.MitStellschraube("desktop.ruhe_bildrate", W("0.2")));
        // Grenzen aus dem Katalog gehen vor
        Assert.Null(e.MitStellschraube("desktop.bildrate", W("20"), new Dictionary<string, (double, double)> { ["desktop.bildrate"] = (1, 15) }));
        // über LaborWerte (live)
        var lw = new Labor.LaborWerte();
        Assert.Equal("ok", lw.Setzen("desktop.abstand_m", W("1.5")));
        Assert.Equal(1.5, lw.Desktop.AbstandM);
        Assert.Equal("ungültig", lw.Setzen("desktop.abstand_m", W("0.1")));
        Assert.Equal(1.5, lw.Desktop.AbstandM);
        Assert.Equal("ok", lw.Setzen("desktop.abstand_m", W("null")));
        Assert.Equal(1.1, lw.Desktop.AbstandM);
    }
}
