// Tests der Quest App V1e (Brillentest, Christian 24.10.2026): Anstoss statt ganzer Bewegung für Hebegeste, Hineinziehen und Wegwischen – mit den Fehlauslösungen
// vorher/nachher über Zufallsbewegungen, ruhige Hände und die Testgesten des Gestenlabors (synthetischer Generator, GestenGenerator).

using Hlks.Xr.Bedienung;
using Hlks.Xr.Desktop;
using Hlks.Xr.Gesten;
using Xunit.Abstractions;
using G = Hlks.Xr.Tests.GestenGenerator;
using static Hlks.Xr.Tests.T;

namespace Hlks.Xr.Tests;

public class V1eTests(ITestOutputHelper aus)
{
    const double Blick = Math.PI / 2;   // Blick nach −z

    // ================================================================================================ 1. Anstoss

    /// <summary>
    /// Alle drei Gesten über eine Aufnahme, verdrahtet wie in der App (QuestApp.AppsBild: Hebegeste mit beiden Händen; WegwischenBild: beide Hände, ruht kurz nach dem
    /// Hineinziehen; DesktopBildschirm: Hineinziehen mit beiden Händen, ruht nach dem Wegwischen). anstoss = Satz für alle drei (Aus = wie bis V1d).
    /// </summary>
    internal static (int Hebe, int Weg, int Hinein, int Anstoss) Abspielen(Aufnahme a, AnstossEinstellungen anstoss)
    {
        var w = WegwischEinstellungen.Vorgabe with { Anstoss = anstoss };
        var he = HebeEinstellungen.Vorgabe with { Anstoss = anstoss };
        var g = new DesktopGesten(() => DesktopEinstellungen.Vorgabe, null, () => w);
        var hz = new HebeZustand();
        var wz = new WegwischZustand();
        int hebe = 0, weg = 0, hinein = 0, durch = 0;
        foreach (var b in a.Bilder)
        {
            var r = b.Rechts.Verfolgt ? b.Rechts.GelenkeMitNamen() : null;
            var l = b.Links.Verfolgt ? b.Links.GelenkeMitNamen() : null;
            var hr = HandBeobachtung.Aus(r, "right");
            var hl = HandBeobachtung.Aus(l, "left");
            var hs = HebeGeste.Schritt(hz, b.T, hl, hr, b.Gieren, he);
            if (hs.Ereignis is not null) { hebe++; if (hs.DurchAnstoss) durch++; }
            var ruht = g.WegwischenRuht(b.T);
            var ws = Wegwischen.Schritt(wz, b.T, ruht ? null : hr, ruht ? null : hl, b.Gieren, w);
            if (ws.Ereignis) { weg++; g.NachWegwischen(b.T); if (ws.DurchAnstoss) durch++; }
            var s = g.Schritt(new DesktopHaende(b.T, r, l, b.Gieren), true);
            if (s.Ausgeloest) { hinein++; if (s.DurchAnstoss) durch++; }
        }
        return (hebe, weg, hinein, durch);
    }

    /// <summary>Fehlauslösungen über viele Bewegungen, die keine der Gesten sind: Zufall (rechte bzw. beide Hände), ruhige Hände, die übrigen Testgesten des Labors.</summary>
    internal static (int Hebe, int Weg, int Hinein, int Bewegungen, double Sekunden) Fehlauslösungen(AnstossEinstellungen anstoss, int zufall = 300, List<string>? wo = null)
    {
        int hebe = 0, weg = 0, hinein = 0, n = 0;
        double sek = 0;
        void Zaehlen(Aufnahme a, string name)
        {
            var (h, w, i, _) = Abspielen(a, anstoss);
            hebe += h; weg += w; hinein += i; n++;
            if (h + w + i > 0) wo?.Add($"{name}: H{h} W{w} I{i}");
            sek += a.Bilder.Count > 0 ? a.Bilder[^1].T - a.Bilder[0].T : 0;
        }
        for (int s = 0; s < zufall; s++)
            Zaehlen(G.Erzeugen(G.Zufall(4000 + s, mitLinks: s % 2 == 0), new Vorfuehrung { DauerS = 1.0 + (s % 5) * 0.25, Saat = 5000 + s, GierenGrad = (s % 7) * 50 }), $"Zufall {s}");
        for (int s = 0; s < 10; s++) Zaehlen(G.Erzeugen(G.Ruhe, new Vorfuehrung { DauerS = 3, Saat = 900 + s, RauschenM = 0.004 + 0.002 * (s % 3) }), $"Ruhe {s}");
        // Testgesten des Labors, die keine der drei Gesten sind (Kreis, Stoss, Faust, Wisch mit Pinch, Auseinander, Wisch runter, Heranziehen) – je in Abwandlungen und
        // gespiegelt
        foreach (var t in new[] { G.TKreis, G.TStoss, G.TFaust, G.TWischHochPinch, G.TAuseinander, G.TWischRunter, G.THeranziehen })
            foreach (var gesp in new[] { false, true })
                foreach (var (text, v) in G.Abwandlungen(t, gesp).Take(6))
                    Zaehlen(G.Erzeugen(t.Bewegung, v), $"{t.Name} {text}{(gesp ? " gespiegelt" : "")}");
        return (hebe, weg, hinein, n, sek);
    }

    static readonly Vec3 VornHoch = G.N(0, 0.3, 1);

    /// <summary>Kurze, deutliche Anstösse aller drei Gesten (rechte und linke Hand), in Abwandlungen: Weg, Dauer, Ruhe davor, Körperrichtung, Bogen.</summary>
    internal static IEnumerable<(string Art, Aufnahme A)> Anstoesse()
    {
        var faelle = new (double Weg, double Dauer)[] { (0.07, 0.12), (0.08, 0.15), (0.10, 0.2), (0.12, 0.2), (0.065, 0.1), (0.09, 0.25) };
        int saat = 1;
        foreach (var (weg, dauer) in faelle)
            foreach (var ruhe in new[] { 0.4, 0.15 })
                foreach (var gieren in new[] { 0.0, 120 })
                {
                    var v = new Vorfuehrung { DauerS = dauer, RuheVorS = ruhe, RuheNachS = 0.5, GierenGrad = gieren, BogenM = 0.01, Saat = saat++ };
                    GestenBewegung weg_ = u => (null, new Haltung(G.V(0.15 - weg * u, -0.25, 0.4), VornHoch, G.V(-1, 0, 0), G.Kr(0.1)));
                    GestenBewegung wegL = u => (new Haltung(G.V(-0.05 - weg * u, -0.25, 0.4), VornHoch, G.V(1, 0, 0), G.Kr(0.1)), null);
                    GestenBewegung hin = u => (null, new Haltung(G.V(-0.05 + weg * u, -0.25, 0.4), VornHoch, G.V(-1, 0, 0), G.Kr(0.1)));
                    GestenBewegung hinL = u => (new Haltung(G.V(-0.25 + weg * u, -0.25, 0.4), VornHoch, G.V(1, 0, 0), G.Kr(0.1)), null);
                    GestenBewegung hebe = u => (new Haltung(G.V(-0.2, -0.5 + weg * u, 0.35), G.V(0, 0, 1), G.V(0, 1, 0), G.Kr(0.1)),
                        new Haltung(G.V(0.2, -0.5 + weg * u, 0.35), G.V(0, 0, 1), G.V(0, 1, 0), G.Kr(0.1)));
                    yield return ("weg", G.Erzeugen(weg_, v));
                    yield return ("weg", G.Erzeugen(wegL, v));
                    yield return ("hinein", G.Erzeugen(hin, v));
                    yield return ("hinein", G.Erzeugen(hinL, v));
                    yield return ("hebe", G.Erzeugen(hebe, v));
                }
    }

    [Fact]
    public void Anstoss_Fehlausloesungen_nehmen_nicht_zu_Zufall_Ruhe_und_Testgesten()
    {
        // 900 Zufallsbewegungen (je 1–2 s, rechte bzw. beide Hände, verschiedene Körperrichtungen), 10 × 3 s ruhige Hände, 84 Testgesten des Labors
        var vorher = Fehlauslösungen(AnstossEinstellungen.Aus, 900);
        var nachher = Fehlauslösungen(AnstossEinstellungen.Vorgabe, 900);
        aus.WriteLine($"Fehlauslösungen vorher (ganze Bewegung, bis V1d): Hebegeste {vorher.Hebe}, Wegwischen {vorher.Weg}, Hineinziehen {vorher.Hinein} " +
            $"in {vorher.Bewegungen} Bewegungen ({vorher.Sekunden / 60:0.0} min)");
        aus.WriteLine($"Fehlauslösungen nachher (mit Anstoss V1e): Hebegeste {nachher.Hebe}, Wegwischen {nachher.Weg}, Hineinziehen {nachher.Hinein}");
        Assert.True(nachher.Hebe <= vorher.Hebe, "Hebegeste");
        Assert.True(nachher.Weg <= vorher.Weg, "Wegwischen");
        Assert.True(nachher.Hinein <= vorher.Hinein, "Hineinziehen");
        // ruhige Hände allein: nie
        var ruhe = Enumerable.Range(0, 6).Select(s => Abspielen(G.Erzeugen(G.Ruhe, new Vorfuehrung { DauerS = 4, Saat = 70 + s, RauschenM = 0.008 }), AnstossEinstellungen.Vorgabe)).ToList();
        Assert.All(ruhe, r => Assert.Equal(0, r.Hebe + r.Weg + r.Hinein));
    }

    [Fact]
    public void Ein_kurzer_deutlicher_Anstoss_loest_jede_der_drei_Gesten_aus_die_ganze_Bewegung_bis_V1d_nicht()
    {
        int ok = 0, n = 0, okVorher = 0;
        foreach (var (art, a) in Anstoesse())
        {
            n++;
            var r = Abspielen(a, AnstossEinstellungen.Vorgabe);
            if ((art == "weg" ? r.Weg : art == "hinein" ? r.Hinein : r.Hebe) == 1 && r.Hebe + r.Weg + r.Hinein == 1) ok++;
            var v = Abspielen(a, AnstossEinstellungen.Aus);
            if (v.Hebe + v.Weg + v.Hinein > 0) okVorher++;
        }
        aus.WriteLine($"kurze Anstösse (6.5–12 cm in 0.1–0.25 s, rechte und linke Hand, Hebegeste): mit Anstoss {ok}/{n} erkannt, mit der ganzen Bewegung bis V1d {okVorher}/{n}");
        Assert.True(ok >= n * 9 / 10, $"{ok}/{n}");
        Assert.True(okVorher < n / 4);
        // je Geste eine typische: 8 cm in 0.15 s
        var v0 = new Vorfuehrung { DauerS = 0.15, RuheVorS = 0.3, RuheNachS = 0.5 };
        var weg = Abspielen(G.Erzeugen(u => (null, new Haltung(G.V(0.15 - 0.08 * u, -0.25, 0.4), VornHoch, G.V(-1, 0, 0), G.Kr(0.1))), v0), AnstossEinstellungen.Vorgabe);
        Assert.Equal((0, 1, 0, 1), weg);
        var hin = Abspielen(G.Erzeugen(u => (new Haltung(G.V(-0.25 + 0.08 * u, -0.25, 0.4), VornHoch, G.V(1, 0, 0), G.Kr(0.1)), null), v0), AnstossEinstellungen.Vorgabe);
        Assert.Equal((0, 0, 1, 1), hin);
        var hebe = Abspielen(G.Erzeugen(u => (new Haltung(G.V(-0.2, -0.5 + 0.08 * u, 0.35), G.V(0, 0, 1), G.V(0, 1, 0), G.Kr(0.1)),
            new Haltung(G.V(0.2, -0.5 + 0.08 * u, 0.35), G.V(0, 0, 1), G.V(0, 1, 0), G.Kr(0.1))), v0), AnstossEinstellungen.Vorgabe);
        Assert.Equal((1, 0, 0, 1), hebe);
    }

    [Fact]
    public void Kein_Anstoss_langsam_zu_kurz_Hand_erst_waehrend_des_Schubs_gestellt_geschwenkt_Pinch_Faust_einhaendig_gehoben()
    {
        int Ereignisse(GestenBewegung b, double dauer, double ruhe = 0.3) =>
            Abspielen(G.Erzeugen(b, new Vorfuehrung { DauerS = dauer, RuheVorS = ruhe, RuheNachS = 0.5 }), AnstossEinstellungen.Vorgabe) is var r ? r.Hebe + r.Weg + r.Hinein : -1;
        // 8 cm in 0.4 s (0.2 m/s): zu langsam; 4 cm in 0.08 s: zu kurz
        Assert.Equal(0, Ereignisse(u => (null, new Haltung(G.V(0.15 - 0.08 * u, -0.25, 0.4), VornHoch, G.V(-1, 0, 0), G.Kr(0.1))), 0.4));
        Assert.Equal(0, Ereignisse(u => (null, new Haltung(G.V(0.15 - 0.04 * u, -0.25, 0.4), VornHoch, G.V(-1, 0, 0), G.Kr(0.1))), 0.08));
        // die Handfläche dreht erst während des Schubs nach links (Schwenk um die Fingerachse, 46° → 0°): kein Anstoss
        Assert.Equal(0, Ereignisse(u => (null, new Haltung(G.V(0.15 - 0.09 * u, -0.25, 0.4), VornHoch, G.N(-Math.Cos((1 - u) * 0.8), -Math.Sin((1 - u) * 0.8), 0), G.Kr(0.1))), 0.15));
        // dieselbe Bewegung ohne Schwenk löst aus (Gegenprobe)
        Assert.Equal(1, Ereignisse(u => (null, new Haltung(G.V(0.15 - 0.09 * u, -0.25, 0.4), VornHoch, G.V(-1, 0, 0), G.Kr(0.1))), 0.15));
        // Pinch bzw. Faust: nichts
        Assert.Equal(0, Ereignisse(u => (null, new Haltung(G.V(0.15 - 0.09 * u, -0.25, 0.4), VornHoch, G.V(-1, 0, 0), [0.3, 0.35, 0.7, 0.8, 0.8], 1)), 0.15));
        Assert.Equal(0, Ereignisse(u => (null, new Haltung(G.V(0.15 - 0.09 * u, -0.25, 0.4), VornHoch, G.V(-1, 0, 0), G.Kr(0.9))), 0.15));
        // nur halb offen (gekrümmte Finger, Verhältnis unter OffenAb): beim Anstoss nicht, die ganze Bewegung wie bisher schon
        Assert.Equal(0, Ereignisse(u => (null, new Haltung(G.V(0.15 - 0.09 * u, -0.25, 0.4), VornHoch, G.V(-1, 0, 0), G.Kr(0.42))), 0.15));
        // Hebegeste: nur eine Hand hebt kurz an
        Assert.Equal(0, Ereignisse(u => (new Haltung(G.V(-0.2, -0.5, 0.35), G.V(0, 0, 1), G.V(0, 1, 0), G.Kr(0.1)),
            new Haltung(G.V(0.2, -0.5 + 0.09 * u, 0.35), G.V(0, 0, 1), G.V(0, 1, 0), G.Kr(0.1))), 0.15));
        // ohne Ruhe in der Haltung davor (die Hand wird erst beim Schub gestellt; Bewegung ab dem ersten Bild): nichts
        var z = new WegwischZustand();
        var n = 0;
        for (int i = 0; i <= 40; i++)
        {
            var t = i / 72.0;
            var x = 0.15 - 0.08 * GlasStil.Weich(Math.Clamp(t / 0.15, 0, 1));
            if (Wegwischen.Schritt(z, t, new HandBeobachtung(V(x, 1.2, -0.35), V(-1, 0, 0), true, false), Blick).Ereignis) n++;
        }
        Assert.Equal(0, n);
    }

    [Fact]
    public void Ausholen_Zurueckholen_und_gegenlaeufige_Hand_loesen_mit_Anstoss_nichts_Falsches_aus()
    {
        // gemächliches Ausholen nach rechts (12 cm in 0.45 s) mit der Handfläche nach links, dann ein Anstoss nach links: nur das Wegwischen
        GestenBewegung ausholen = u => (null, new Haltung(G.V(0.02 + 0.12 * u, -0.25, 0.4), VornHoch, G.V(-1, 0, 0), G.Kr(0.1)));
        GestenBewegung anstoss = u => (null, new Haltung(G.V(0.14 - 0.09 * u, -0.25, 0.4), VornHoch, G.V(-1, 0, 0), G.Kr(0.1)));
        var (a1, _) = G.Strom([(ausholen, new Vorfuehrung { DauerS = 0.45, RuheVorS = 0.4, RuheNachS = 0.12 }), (anstoss, new Vorfuehrung { DauerS = 0.15, RuheVorS = 0.0, RuheNachS = 0.5 })],
            uebergangS: 0.05);
        Assert.Equal((0, 1, 0), Ohne(Abspielen(a1, AnstossEinstellungen.Vorgabe)));
        // Anstoss nach rechts (herein), dann die Hand zügig zurück nach links: der Bildschirm kommt, das Zurückholen wischt nichts weg (Gegensperre)
        GestenBewegung herein = u => (null, new Haltung(G.V(-0.05 + 0.09 * u, -0.25, 0.4), VornHoch, G.V(-1, 0, 0), G.Kr(0.1)));
        GestenBewegung zurueck = u => (null, new Haltung(G.V(0.04 - 0.2 * u, -0.25, 0.4), VornHoch, G.V(-1, 0, 0), G.Kr(0.1)));
        var (a2, _) = G.Strom([(herein, new Vorfuehrung { DauerS = 0.15, RuheVorS = 0.4, RuheNachS = 0.05 }), (zurueck, new Vorfuehrung { DauerS = 0.35, RuheVorS = 0.0, RuheNachS = 0.6 })],
            uebergangS: 0.05);
        Assert.Equal((0, 0, 1), Ohne(Abspielen(a2, AnstossEinstellungen.Vorgabe)));
        // zwei Hände gegenläufig (auseinander) mit kurzem Schub: weder Wegwischen noch Hineinziehen
        GestenBewegung auseinander = u => (new Haltung(G.V(-0.1 - 0.08 * u, -0.25, 0.4), VornHoch, G.V(1, 0, 0), G.Kr(0.1)),
            new Haltung(G.V(0.1 + 0.08 * u, -0.25, 0.4), VornHoch, G.V(-1, 0, 0), G.Kr(0.1)));
        Assert.Equal((0, 0, 0), Ohne(Abspielen(G.Erzeugen(auseinander, new Vorfuehrung { DauerS = 0.15, RuheVorS = 0.4 }), AnstossEinstellungen.Vorgabe)));
        // zwei Hände zugleich nach links: einmal
        GestenBewegung beide = u => (new Haltung(G.V(-0.1 - 0.08 * u, -0.25, 0.4), VornHoch, G.V(1, 0, 0), G.Kr(0.1)),
            new Haltung(G.V(0.15 - 0.08 * u, -0.25, 0.4), VornHoch, G.V(-1, 0, 0), G.Kr(0.1)));
        Assert.Equal((0, 1, 0), Ohne(Abspielen(G.Erzeugen(beide, new Vorfuehrung { DauerS = 0.15, RuheVorS = 0.4 }), AnstossEinstellungen.Vorgabe)));
    }

    static (int, int, int) Ohne((int Hebe, int Weg, int Hinein, int Anstoss) r) => (r.Hebe, r.Weg, r.Hinein);

    [Fact]
    public void Anstoss_weder_Pinch_Wisch_noch_Sprung_noch_Blaettern_auf_der_Tafel()
    {
        // der kurze Anstoss mit offener Hand ist kein Pinch + Wisch und kein Sprung (die brauchen den Pinch)
        var wisch = new PinchWischZustand();
        var begehen = new HandBegehenZustand();
        var gesten = new List<BegehenEreignis>();
        var a = G.Erzeugen(u => (null, new Haltung(G.V(0.15 - 0.09 * u, -0.25, 0.4), VornHoch, G.V(-1, 0, 0), G.Kr(0.1))), new Vorfuehrung { DauerS = 0.15, RuheVorS = 0.3 });
        foreach (var b in a.Bilder)
        {
            var g = b.Rechts.GelenkeMitNamen();
            Assert.Null(XrGesten.PinchWischSchritt(wisch, b.T, g["index-finger-tip"], XrTabletLogik.IstPinch(g), b.Gieren, null, 1));
            gesten.AddRange(HandBegehen.Schritt(begehen, b.T, "right", g, G.KopfLage, b.Gieren, 0));
        }
        Assert.DoesNotContain(gesten, e => e.Art is BegehenArt.Teleport or BegehenArt.Wisch or BegehenArt.Drehen or BegehenArt.PinchAn);
        // auf der Tafel (die Hand liegt dort, die App gibt sie nicht weiter): ein kurzer Wisch blättert nicht (Mindestweg 8 cm) und wischt nichts weg
        var tafel = new TabletPose(V(0, 1.45, -0.45), V(0, 0, 1), V(1, 0, 0), V(0, 1, 0), 0.32, 0.31);
        var zb = new TafelBlaetternZustand();
        var wz = new WegwischZustand();
        int bl = 0, weg = 0;
        for (int i = 0; i <= 60; i++)
        {
            var t = i / 72.0;
            var x = 0.08 - 0.07 * GlasStil.Weich(Math.Clamp((t - 0.3) / 0.12, 0, 1));
            var h = HandHilfen.Hand("right", V(x, 1.45, -0.43), V(0, 0, -1), V(-1, 0, 0));
            var s = TafelBlaettern.Schritt(zb, t, h, false, tafel);
            if (s.Ereignis is not null) bl++;
            if (Wegwischen.Schritt(wz, t, s.AnTafel ? null : HandBeobachtung.Aus(h, "right"), null, Blick).Ereignis) weg++;
        }
        Assert.Equal(0, bl);
        Assert.Equal(0, weg);
    }

    [Fact]
    public void Stellschrauben_des_Anstosses_gelten_fuer_alle_drei_Gesten()
    {
        static System.Text.Json.JsonElement W(string j) => System.Text.Json.JsonDocument.Parse(j).RootElement.Clone();
        var e = XrBedienEinstellungen.Vorgabe;
        Assert.Equal(0.06, e.Anstoss.WegM);
        Assert.Equal(0.5, e.Anstoss.TempoMs);
        var n = e.MitStellschraube("gesten.anstoss_weg_m", W("0.09"))!;
        Assert.Equal(0.09, n.Hebe.Anstoss.WegM);
        Assert.Equal(0.09, n.Wegwischen.Anstoss.WegM);
        Assert.Equal(0.09, DesktopEinstellungen.Vorgabe.HineinMit(n.Wegwischen).Anstoss.WegM);
        Assert.Equal(0.8, n.MitStellschraube("gesten.anstoss_tempo_ms", W("0.8"))!.Anstoss.TempoMs);
        Assert.Null(e.MitStellschraube("gesten.anstoss_weg_m", W("0.5")));
        Assert.Null(e.MitStellschraube("gesten.anstoss_tempo_ms", W("0.1")));
        Assert.Equal(0.06, n.MitStellschraube("gesten.anstoss_weg_m", W("null"))!.Anstoss.WegM);
        // mit grösserem Weg löst der kurze Schub (8 cm) nicht mehr aus
        var a = G.Erzeugen(u => (null, new Haltung(G.V(0.15 - 0.08 * u, -0.25, 0.4), VornHoch, G.V(-1, 0, 0), G.Kr(0.1))), new Vorfuehrung { DauerS = 0.15, RuheVorS = 0.3 });
        Assert.Equal(1, Abspielen(a, n.Anstoss with { WegM = 0.06 }).Weg);
        Assert.Equal(0, Abspielen(a, n.Anstoss with { WegM = 0.12 }).Weg);
    }
}
