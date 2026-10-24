// Tests der Quest App V1c (Modul XR, XR-I «Desktop in der Brille»): Gesten Hineinziehen (rechte Hand offen, Handfläche nach links, links → rechts) und Wegwischen
// (dieselbe Haltung, rechts → links; gemeinsame Geste der App, vorläufig DesktopWegwischen) mit dem synthetischen Generator des Gestenlabors (GestenGenerator,
// erfundene Hände). Geprüft: Auslösen, Folgen der Hand, Fehlauslösungen (Ausholen, Zurückholen, langsam, kurz, zwei Hände, linke Hand, Pinch, Hebegeste, Wegschieben
// mit zwei Händen, Röntgen-Hand, Zufall) und umgekehrt, dass die beiden Gesten weder Pinch + Wisch noch die Hebegeste bzw. das Wegschieben (Gestenlabor) auslösen.

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

    /// <summary>Beide Gesten über eine Aufnahme (ohne Sperre durch den Zustand des Bildschirms): Zeiten der Auslösungen, grösster Fortschritt.</summary>
    static (List<double> Hinein, List<double> Weg, double MaxHinein, double MaxWeg) Abspielen(Aufnahme a, DesktopEinstellungen? e = null)
    {
        var g = new DesktopGesten(() => e ?? DesktopEinstellungen.Vorgabe);
        var hi = new List<double>();
        var we = new List<double>();
        double mh = 0, mw = 0;
        foreach (var b in a.Bilder)
        {
            var s = g.Schritt(Haende(b), hineinErlaubt: true, wegErlaubt: true);
            if (s.Hinein.Ausgeloest) hi.Add(b.T);
            if (s.Weg.Ausgeloest) we.Add(b.T);
            mh = Math.Max(mh, s.Hinein.Fortschritt);
            mw = Math.Max(mw, s.Weg.Fortschritt);
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
        // halber Wisch (12 cm, dann stehen bleiben): der Bildschirm folgt bis etwa zur Hälfte, ausgelöst wird nicht; nach dem Zeitfenster fällt der Fortschritt auf 0
        GestenBewegung halb = u => (null, new Haltung(G.V(-0.10 + 0.12 * u, -0.25, 0.4), VornHoch, G.V(-1, 0, 0), G.Kr(0.1)));
        var a = Vorzeigen(halb, dauer: 0.25, ruhe: 1.0);
        var g = new DesktopGesten(() => DesktopEinstellungen.Vorgabe);
        var verlauf = a.Bilder.Select(b => (b.T, S: g.Schritt(Haende(b), true, true).Hinein)).ToList();
        Assert.DoesNotContain(verlauf, x => x.S.Ausgeloest);
        var max = verlauf.Max(x => x.S.Fortschritt);
        aus.WriteLine($"halber Wisch: grösster Fortschritt {max:0.00}");
        Assert.InRange(max, 0.35, 0.65);   // (0.12 − 0.04) / 0.16 = 0.5
        Assert.True(verlauf.First(x => x.S.Fortschritt == max).S.Laeuft);
        Assert.Equal(0, verlauf[^1].S.Fortschritt, 6);
        Assert.False(verlauf[^1].S.Laeuft);
        // Fortschritt steigt mit dem Weg (monoton bis zum Höchstwert)
        var bisMax = verlauf.TakeWhile(x => x.S.Fortschritt < max).Select(x => x.S.Fortschritt).ToList();
        for (int i = 1; i < bisMax.Count; i++) Assert.True(bisMax[i] >= bisMax[i - 1] - 0.02, $"Bild {i}");
    }

    [Fact]
    public void Langsam_oder_kurz_loest_nicht_aus()
    {
        Assert.Empty(Abspielen(Vorzeigen(Hinein, dauer: 1.6)).Hinein);   // 35 cm in 1.6 s: 0.22 m/s
        GestenBewegung kurz = u => (null, new Haltung(G.V(-0.10 + 0.15 * u, -0.25, 0.4), VornHoch, G.V(-1, 0, 0), G.Kr(0.1)));
        Assert.Empty(Abspielen(Vorzeigen(kurz, dauer: 0.25)).Hinein);   // 15 cm
        Assert.Empty(Abspielen(Vorzeigen(u => (null, new Haltung(G.V(0.25 - 0.15 * u, -0.25, 0.4), VornHoch, G.V(-1, 0, 0), G.Kr(0.1))), dauer: 0.25)).Weg);
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
    public void Zwei_Haende_linke_Hand_Pinch_und_andere_Handlagen_loesen_nicht_aus()
    {
        // beide Hände wischen gemeinsam nach rechts (die linke bewegt sich mit)
        GestenBewegung beide = u => (new Haltung(G.V(-0.35 + 0.35 * u, -0.25, 0.4), VornHoch, G.V(-1, 0, 0), G.Kr(0.1)), Hinein(u).Rechts);
        Assert.Empty(Abspielen(Vorzeigen(beide)).Hinein);
        // dieselbe Geste mit der linken Hand (gespiegelt): nur die rechte Hand zählt
        var links = G.Erzeugen(Hinein, new Vorfuehrung { DauerS = 0.45, Gespiegelt = true });
        var rl = Abspielen(links);
        Assert.Empty(rl.Hinein);
        Assert.Empty(rl.Weg);
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
        // wie QuestApp.Desktop.cs: 12 cm in 0.25 s, 0.2 s halten (Prüfbild «Geste halb»), dann 23 cm in 0.3 s; später zurück
        static double W(double u) { u = Math.Clamp(u, 0, 1); return u * u * u * (10 - 15 * u + 6 * u * u); }
        var kopf = new Vec3(0.2, 1.55, 0.4);
        var gieren = Math.PI / 2 + 0.3;
        var g = new DesktopGesten(() => DesktopEinstellungen.Vorgabe);
        var a = new DesktopAnsicht();
        var e = DesktopEinstellungen.Vorgabe;
        double t = 0, halb = -1;
        int hinein = 0, weg = 0;
        void Bild(double x, bool hand = true)
        {
            var h = new DesktopHaende(t, hand ? PruefHand.RechtsHandflaecheLinks(kopf, gieren, x) : null, null, gieren);
            var s = g.Schritt(h, a.HineinMoeglich, a.Da);
            if (s.Hinein.Ausgeloest) hinein++;
            if (s.Weg.Ausgeloest) weg++;
            a.Schritt(t, 1 / 72.0, kopf, gieren, s, e);
            t += 1 / 72.0;
        }
        for (int i = 0; i < 30; i++) Bild(-0.10);
        var t0 = t;
        while (t - t0 < 0.45) Bild(-0.10 + 0.12 * W((t - t0) / 0.25));
        halb = a.Anteil;
        t0 = t;
        while (t - t0 < 0.35) Bild(0.02 + 0.23 * W((t - t0) / 0.3));
        for (int i = 0; i < 60; i++) Bild(0.25, hand: false);
        aus.WriteLine($"gedachte Hand: halb {halb:0.00}, eingeblendet {hinein}×");
        Assert.Equal(1, hinein);
        Assert.Equal(DesktopPhase.Da, a.Phase);
        Assert.InRange(halb, 0.3, 0.75);
        // Wegwischen nach der Sperre: halb, dann ganz
        t0 = t;
        while (t - t0 < 1.3) { var tt = t - t0; Bild(tt < 0.45 ? 0.25 - 0.12 * W(tt / 0.25) : 0.13 - 0.23 * W((tt - 0.45) / 0.3), hand: tt <= 0.8); }
        Assert.Equal(1, weg);
        Assert.Equal(DesktopPhase.Aus, a.Phase);
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
        Assert.DoesNotContain(a.Bilder, b => SeitenWisch.Schritt(z, b.T, b.Rechts.GelenkeMitNamen(), null, b.Gieren, true, erlaubt: false).Ausgeloest);
        // gesperrt bis nach der Bewegung: nichts, auch nicht verspätet
        var z2 = new SeitenWischZustand();
        z2.Sperren(a.Bilder[^1].T - 0.3);
        Assert.DoesNotContain(a.Bilder, b => SeitenWisch.Schritt(z2, b.T, b.Rechts.GelenkeMitNamen(), null, b.Gieren, true).Ausgeloest);
    }

    [Fact]
    public void Stellschrauben_aendern_die_Schwellen()
    {
        // ein kurzer Wisch (15 cm) löst mit Weg 0.12 aus, mit der Vorgabe 0.2 nicht
        GestenBewegung kurz = u => (null, new Haltung(G.V(-0.10 + 0.15 * u, -0.25, 0.4), VornHoch, G.V(-1, 0, 0), G.Kr(0.1)));
        var e = DesktopEinstellungen.Vorgabe with { Hinein = SeitenWischEinstellungen.Vorgabe with { WegM = 0.12 } };
        Assert.Single(Abspielen(Vorzeigen(kurz, dauer: 0.25), e).Hinein);
        // Handfläche 60° schräg nach vorn-links (Finger nach oben): mit 45° nicht, mit 70° schon
        GestenBewegung schraeg = u => (null, new Haltung(G.V(-0.10 + 0.35 * u, -0.25, 0.4), G.V(0, 1, 0), G.N(-0.5, 0, 0.866), G.Kr(0.1)));
        Assert.Empty(Abspielen(Vorzeigen(schraeg)).Hinein);
        Assert.Single(Abspielen(Vorzeigen(schraeg), DesktopEinstellungen.Vorgabe with { Hinein = SeitenWischEinstellungen.Vorgabe with { HandflaecheGrad = 70 } }).Hinein);
    }

    [Fact]
    public void Einstellungen_Schluessel_Grenzen_und_Zuruecksetzen()
    {
        static System.Text.Json.JsonElement W(string j) => System.Text.Json.JsonDocument.Parse(j).RootElement.Clone();
        var e = DesktopEinstellungen.Vorgabe;
        Assert.Equal(10, DesktopEinstellungen.Schluessel.Count);
        Assert.All(DesktopEinstellungen.Schluessel, s => Assert.True(DesktopEinstellungen.Kennt(s)));
        Assert.All(DesktopEinstellungen.Schluessel, s => Assert.True(Labor.LaborWerte.Kennt(s), s));
        var n = e.MitStellschraube("desktop.breite_m", W("2.0"))!;
        Assert.Equal(2.0, n.BreiteM);
        Assert.Null(e.MitStellschraube("desktop.breite_m", W("9")));        // ausserhalb
        Assert.Null(e.MitStellschraube("desktop.breite_m", W("\"x\"")));    // kein Zahl
        Assert.Null(e.MitStellschraube("anzeige.skala", W("1")));           // gehört nicht hierher
        Assert.Equal(1.4, n.MitStellschraube("desktop.breite_m", W("null"))!.BreiteM);
        Assert.Equal(0.3, e.MitStellschraube("gesten.wegwischen.weg_m", W("0.3"))!.Weg.WegM);
        Assert.Equal(0.2, e.MitStellschraube("gesten.wegwischen.weg_m", W("0.3"))!.Hinein.WegM);
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
