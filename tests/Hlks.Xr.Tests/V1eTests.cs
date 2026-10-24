// Tests der Quest App V1e (Brillentest, Christian 24.10.2026): Anstoss statt ganzer Bewegung für Hebegeste, Hineinziehen und Wegwischen – mit den Fehlauslösungen
// vorher/nachher über Zufallsbewegungen, ruhige Hände und die Testgesten des Gestenlabors (synthetischer Generator, GestenGenerator).

using Hlks.Xr.Bedienung;
using Hlks.Xr.Desktop;
using Hlks.Xr.Gesten;
using Hlks.Xr.Sitzung;
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

    // ================================================================================================ 2. Etagenliste auf einer Seite

    [Fact]
    public void Etagenliste_auf_einer_Seite_die_Tafel_wird_hoeher_erst_ueber_12_rollt_sie_das_Aktuelle_beim_Oeffnen_sichtbar()
    {
        // das gewöhnliche Fenster fasst unter dem Titel 4 Zeilen (Test_praxis: 7 Geschosse beim Begehen, 8 + «Alle Etagen» am Tisch)
        var m = GlasMasse.Fuer(new AppEinstellungen());
        var c = new GlasInhalt(m, m.FensterB - 2 * m.M(GlasStil.RandPt), m.FensterH - 2 * m.M(GlasStil.RandPt) - m.M(GlasStil.FussPt));
        c.Titel("Etagen");
        var frei = c.FreieZeilen;
        Assert.Equal(4, frei);
        Assert.Equal(new EtagenAnordnung(0, 0, 3, false), EtagenListe.Anordnen(3, frei, 12, 1, null));
        var a9 = EtagenListe.Anordnen(9, frei, 12, 5, null);
        Assert.Equal(new EtagenAnordnung(5, 0, 9, false), a9);
        // die höhere Tafel fasst genau so viele Zeilen mehr, gleich gross und gleich weit auseinander
        var m9 = m with { MehrZeilen = a9.MehrZeilen };
        Nah(m9.FensterH - m.FensterH, 5 * m.M(GlasStil.ZeilenMittePt), 1e-12);
        var c9 = new GlasInhalt(m9, m9.FensterB - 2 * m9.M(GlasStil.RandPt), m9.FensterH - 2 * m9.M(GlasStil.RandPt) - m9.M(GlasStil.FussPt));
        c9.Titel("Etagen");
        Assert.Equal(9, c9.FreieZeilen);
        for (int i = 0; i < 9; i++) c9.Zeile($"g{i}", $"G{i}");
        var zeilen = c9.Elemente.Where(e => e.Art == GlasArt.Zeile).ToList();
        Assert.All(zeilen, z => Nah(z.H, m.M(GlasStil.ZeilePt), 1e-12));
        for (int i = 1; i < zeilen.Count; i++) Nah(zeilen[i - 1].Y - zeilen[i].Y, m.M(GlasStil.ZeilenMittePt), 1e-12);
        Assert.All(zeilen, z => Assert.True(z.Y - z.H / 2 >= -c9.Hoehe / 2 - 1e-9));   // alle im Fenster
        // genau 12: noch eine Seite; 20: es rollt, 12 sichtbar, das aktuelle (Index 10) in der Mitte
        Assert.Equal(new EtagenAnordnung(8, 0, 12, false), EtagenListe.Anordnen(12, frei, 12, 11, null));
        var a20 = EtagenListe.Anordnen(20, frei, 12, 10, null);
        Assert.True(a20.Rollt);
        Assert.Equal(12, a20.Sichtbar);
        Assert.InRange(10, a20.Ab, a20.Ab + a20.Sichtbar - 1);
        Assert.Equal(10 - 6, a20.Ab);
        // ganz unten bzw. oben begrenzt
        Assert.Equal(8, EtagenListe.Anordnen(20, frei, 12, 19, null).Ab);
        Assert.Equal(0, EtagenListe.Anordnen(20, frei, 12, 0, null).Ab);
        // rollen mit dem Wisch: hoch = weiter (eine Zeile bleibt stehen), runter = zurück, links/rechts nichts
        var a = EtagenListe.Anordnen(20, frei, 12, 0, null);
        Assert.Equal(8, EtagenListe.Rollen(a, 20, WischRichtung.Hoch));   // 0 + 11, begrenzt auf 8
        var b = EtagenListe.Anordnen(20, frei, 12, -1, 8);
        Assert.Equal(0, EtagenListe.Rollen(b, 20, WischRichtung.Runter));
        Assert.Equal(8, EtagenListe.Rollen(b, 20, WischRichtung.Links));
        // ohne Rollen (alles auf einer Seite) bleibt es stehen
        Assert.Equal(0, EtagenListe.Rollen(a9, 9, WischRichtung.Hoch));
        // Stellschraube
        Assert.Equal(12, XrBedienEinstellungen.Vorgabe.Apps.EtagenZeilenMax);
        Assert.Equal(8, XrBedienEinstellungen.Vorgabe.MitStellschraube("etagen.zeilen_max", System.Text.Json.JsonDocument.Parse("8").RootElement)!.Apps.EtagenZeilenMax);
        Assert.Null(XrBedienEinstellungen.Vorgabe.MitStellschraube("etagen.zeilen_max", System.Text.Json.JsonDocument.Parse("8.5").RootElement));
    }

    [Fact]
    public void Geisterhand_zeigt_den_kurzen_Anstoss_und_das_Kreisen_am_Punkt_Drehen_Symbole_und_Glanz()
    {
        // Wegwischen und Hebegeste: erst die Hand stellen (AnstossVor), dann der Weg (AnstossM) in AnstossAnteil des Durchgangs, dann steht sie
        var hand = Geisterhand.VorlageOffen(V(0, 1.2, -0.4), V(0, 0, -1), false);
        var ziel = new TabletPose(V(0, 1.2, -0.4), V(0, 0, 1), V(-1, 0, 0), V(0, 1, 0), 0.1, 0.1);
        var mitte = XrTabletLogik.HandflaechenMitte(hand)!.Value;
        Vec3 M(GeisterGeste g, double t) => XrTabletLogik.HandflaechenMitte(Geisterhand.Bild(g, t, hand, ziel)!.Gelenke)!.Value;
        var z = Geisterhand.DauerS / 2;
        var fertig = z * (Geisterhand.AnstossVor + Geisterhand.AnstossAnteil);
        Nah(M(GeisterGeste.Wegwischen, z * Geisterhand.AnstossVor * 0.9).Abstand(M(GeisterGeste.Wegwischen, 0.001)), 0, 1e-9);   // steht zuerst
        Nah(M(GeisterGeste.Wegwischen, fertig).Abstand(ziel.Mitte + ziel.Rechts * Geisterhand.AnstossM), 0, 1e-9);
        Nah(M(GeisterGeste.Wegwischen, z * 0.8).Abstand(M(GeisterGeste.Wegwischen, fertig + 0.01)), 0, 1e-9);
        Assert.True(Geisterhand.AnstossM > AnstossEinstellungen.Vorgabe.WegM && Geisterhand.AnstossM < 0.15);
        Assert.True(Geisterhand.AnstossM / (z * Geisterhand.AnstossAnteil) >= AnstossEinstellungen.Vorgabe.TempoMs);   // zügig genug für den Anstoss
        Assert.True(z * Geisterhand.AnstossVor >= AnstossEinstellungen.Vorgabe.HaltungVorS);                           // und vorher gestellt
        Nah(M(GeisterGeste.Heben, fertig).Abstand(ziel.Mitte), 0, 1e-9);
        // Kreisen: Pinch am Punkt, auf dem Bogen (Rechts) und nach aussen (Oben)
        var k0 = Geisterhand.Bild(GeisterGeste.Kreisen, 0.01, hand, ziel)!.Gelenke;
        var k1 = Geisterhand.Bild(GeisterGeste.Kreisen, z * 0.7, hand, ziel)!.Gelenke;
        Assert.True(XrTabletLogik.IstPinch(k1));
        var weg = Vec3.Mitte(k1["thumb-tip"], k1["index-finger-tip"]) - Vec3.Mitte(k0["thumb-tip"], k0["index-finger-tip"]);
        Assert.True(weg.Skalar(ziel.Rechts) > 0.08 && weg.Skalar(ziel.Oben) > 0.02);
        // die Symbole der Punkte sind die Werkzeuge von KICAD
        Assert.Contains(GlasSymbole.Verschieben, GlasSymbole.Alle);
        Assert.Contains(GlasSymbole.Drehen, GlasSymbole.Alle);
        Assert.Equal("verschieben", GlasSymbole.Verschieben);
        Assert.Equal("drehen", GlasSymbole.Drehen);
        // Glanz als Stellschraube (0 = wie bis V1d)
        Assert.Equal(0.6, XrBedienEinstellungen.Vorgabe.Apps.Glanz);
        Assert.Equal(0.0, XrBedienEinstellungen.Vorgabe.MitStellschraube("anzeige.glanz", System.Text.Json.JsonDocument.Parse("0").RootElement)!.Apps.Glanz);
        Assert.Null(XrBedienEinstellungen.Vorgabe.MitStellschraube("anzeige.glanz", System.Text.Json.JsonDocument.Parse("1.5").RootElement));
        Assert.True(mitte.Abstand(ziel.Mitte) < 0.2);
    }

    // ================================================================================================ 5. Symbolfeld neben der Tafel: mehr Abstand, kleinerer Bogen

    [Fact]
    public void Neben_der_Tafel_stehen_die_Symbole_naeher_auf_kleinerem_Bogen_mit_10_cm_Abstand_in_Griffweite()
    {
        var auge = V(0, 1.6, 0);
        var m = GlasMasse.Fuer(new AppEinstellungen());
        var e = new SymbolEinstellungen();
        Assert.Equal(0.10, e.AbstandTafelM);
        Assert.Equal(0.50, e.RadiusSeitlichM);
        var mitten = AppSymbole.Plaetze(["status", "etagen", "messen", "marker", "ort", "ebenen", "bildschirm"], auge, Blick, e, m).Select(x => x.Lage.Mitte).ToList();
        // die Tafel wie in der App (V1d: mittig, Tafel.Platz) mit Werkzeugleiste, Griff, Schliessen und der Karte einer Aufgabe darüber
        var platz = Tafel.Platz(auge, Blick, new AppEinstellungen(), TafelEinstellungen.Vorgabe, null, Glas.UnterhalbM(m, 2));
        var fenster = Glas.Fenster(platz, auge, m, 0);
        var rahmen = Glas.Rahmen(fenster, m, 0, 0, 2);
        var karte = fenster with { Mitte = fenster.Mitte + fenster.Oben * (fenster.H / 2 + 0.06), H = 0.09 };
        var flaechen = new List<TabletPose> { rahmen.Fenster, rahmen.Griff, rahmen.Schliessen, rahmen.Ornament!, karte };
        var r = AppSymbole.Durchmesser(m) / 2;
        var neben = AppSymbole.Seitlich(mitten, auge, Blick, flaechen, r, 1, e.AbstandTafelM, e.RadiusSeitlichM);
        var bisher = AppSymbole.Seitlich(mitten, auge, Blick, flaechen, r, 1);
        double Raum(IEnumerable<Vec3> ps) => ps.Min(p => flaechen.Min(f => AppSymbole.AbstandZuFlaeche(p, f))) - r;
        double Seite(Vec3 p) => Math.Abs(Math.Atan2(p.X, -p.Z)) / Grad;
        // Luft im Blick: seitlicher Abstand (m, in der Entfernung des Symbols) zwischen dem Rand des Symbols und der äussersten Kante von Tafel und Ornamenten
        double W(Vec3 p) => Math.Atan2(p.X, -p.Z);
        var kl = flaechen.SelectMany(Glas.Ecken).Min(W);
        var kr = flaechen.SelectMany(Glas.Ecken).Max(W);
        double Blick_(IEnumerable<Vec3> ps) => ps.Min(p =>
        {
            var waag = Math.Sqrt(p.X * p.X + p.Z * p.Z);
            return waag * Math.Sin(W(p) < 0 ? kl - W(p) : W(p) - kr) - r;
        });
        aus.WriteLine($"Luft im Blick Symbol ↔ Tafel: bis V1d {Blick_(bisher):0.000} m, V1e {Blick_(neben):0.000} m; kleinster Raumabstand bis V1d {Raum(bisher):0.000} m, V1e {Raum(neben):0.000} m; " +
            $"weitestes Symbol seitlich bis V1d {bisher.Max(Seite):0}°, V1e {neben.Max(Seite):0}°; Abstand zum Auge höchstens bis V1d {bisher.Max(p => p.Abstand(auge)):0.00} m, V1e {neben.Max(p => p.Abstand(auge)):0.00} m");
        Assert.True(Blick_(neben) >= 0.10 - 0.005, $"{Blick_(neben):0.000}");   // im Blick mindestens 10 cm (bis V1d 3 cm)
        Assert.True(Blick_(bisher) < 0.05);
        // auf dem kleineren Bogen (waagrecht 0.50 m), Höhe bleibt; mindestens 10 cm Luft im Raum; im Blick überdeckt keines; in Griffweite
        for (int i = 0; i < mitten.Count; i++)
        {
            Nah(Math.Sqrt((neben[i].X - auge.X) * (neben[i].X - auge.X) + (neben[i].Z - auge.Z) * (neben[i].Z - auge.Z)), 0.50, 1e-9);
            Nah(neben[i].Y, mitten[i].Y, 1e-12);
        }
        Assert.True(Raum(neben) >= 0.10 - 1e-6, $"{Raum(neben):0.000}");   // und im Raum mindestens 10 cm
        Assert.DoesNotContain(neben, p => flaechen.Any(f => Glas.HinterBedienung(p, auge, [f], 0) || Glas.UeberdecktImBlick(f, Glas.ImBlick(p, auge, 2 * r, 2 * r), auge)));
        // in Griffweite: näher am Auge als das frontale Feld bzw. bis V1d, nicht weiter als 75° zur Seite
        Assert.True(neben.Max(p => p.Abstand(auge)) < bisher.Max(p => p.Abstand(auge)));
        Assert.All(neben, p => Assert.True(Seite(p) <= 75, $"{Seite(p):0}°"));
        // als Stellschrauben: mehr Abstand → weiter zur Seite
        Assert.Equal(0.2, XrBedienEinstellungen.Vorgabe.MitStellschraube("symbole.abstand_tafel_m", System.Text.Json.JsonDocument.Parse("0.2").RootElement)!.Symbole.AbstandTafelM);
        Assert.Equal(0.45, XrBedienEinstellungen.Vorgabe.MitStellschraube("symbole.radius_seitlich_m", System.Text.Json.JsonDocument.Parse("0.45").RootElement)!.Symbole.RadiusSeitlichM);
        var weiter = AppSymbole.Seitlich(mitten, auge, Blick, flaechen, r, 1, 0.2, 0.5);
        Assert.True(Raum(weiter) >= 0.2 - 1e-6 && Blick_(weiter) >= 0.2 - 0.005);
        // ohne offene Tafel (k = 0) bleibt das frontale Feld wie bisher
        Assert.All(AppSymbole.Seitlich(mitten, auge, Blick, flaechen, r, 0, 0.1, 0.5).Zip(mitten), x => Nah(x.First.Abstand(x.Second), 0, 1e-12));
    }

    // ================================================================================================ 3. Zwei Punkte am Sitzungstisch

    static readonly SitzungBezug Bezug = new(new Xz(0, 0), 0, new ModellGroesse(20, 10, 6));
    static EinGriffHand Hd(Vec3 p, bool pinch = true) => new(pinch, p, 0, 0);
    static double Waagrecht(Vec3 a, Vec3 b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));

    [Fact]
    public void Zwei_Punkte_Verschieben_mitten_vorn_Drehen_an_der_rechten_vorderen_Ecke_Zentrum_in_der_Mitte()
    {
        var lage = new SitzungLage(50, 0, 0, 0);
        var p = ZweiPunkte.Punkte(lage, Bezug, null);
        // Modell 20 × 10 m bei 1:50: 0.4 × 0.2 m, Mitte (0, −0.9); vorn ist +z
        Nah(p.Verschieben.X, 0, 1e-9);
        Nah(p.Verschieben.Z, -0.9 + 0.1 + XrSitzungLogik.GriffAbstandM, 1e-9);
        Nah(p.Verschieben.Y, XrSitzungLogik.TischHoehe + XrSitzungLogik.GriffHoeheM, 1e-9);
        Nah(p.Drehen.X, 0.2 + XrSitzungLogik.GriffAbstandM / Math.Sqrt(2), 1e-9);   // rechte Ecke, schräg nach aussen
        Nah(p.Drehen.Z, -0.8 + XrSitzungLogik.GriffAbstandM / Math.Sqrt(2), 1e-9);
        Nah(p.Zentrum.X, 0, 1e-9);
        Nah(p.Zentrum.Z, -0.9, 1e-9);
        Assert.True(p.Verschieben.Abstand(p.Drehen) > 2 * XrSitzungLogik.GriffFangM);   // nie beide mit einem Pinch
        // gedreht und verschoben: wieder vorn bzw. vorn rechts, das Zentrum bleibt die Mitte des Rechtecks
        var l2 = new SitzungLage(50, 2.0, 0.1, 0.05);
        var p2 = ZweiPunkte.Punkte(l2, Bezug, null);
        Assert.True(p2.Verschieben.Z > p2.Zentrum.Z && p2.Drehen.Z > p2.Zentrum.Z);
        Assert.True(p2.Drehen.X > p2.Verschieben.X);
        Nah(p2.Zentrum.X, 0.1, 1e-9);
        Nah(p2.Zentrum.Z, -0.85, 1e-9);
    }

    [Fact]
    public void Verschieben_in_allen_Richtungen_ohne_Drehen_und_ohne_Massstab()
    {
        var z = ZweiPunkte.Neu(new SitzungLage(50, 0.3, 0, 0));
        var p = ZweiPunkte.Punkte(z.Lage, Bezug, null);
        Assert.Equal(ZweiPunkteEreignis.Nichts, ZweiPunkte.Schritt(z, null, Hd(p.Verschieben + V(0.2, 0, 0)), p));   // zu weit weg
        ZweiPunkte.Schritt(z, null, null, p);
        Assert.Equal(ZweiPunkteEreignis.Gefasst, ZweiPunkte.Schritt(z, null, Hd(p.Verschieben + V(0.01, 0, 0)), p));
        Assert.Equal(TischPunkt.Verschieben, z.Punkt);
        var h0 = p.Verschieben + V(0.01, 0, 0);
        // die Hand bewegt sich und dreht (ein Kreis um das Zentrum): das Modell folgt nur der Lage
        ZweiPunkte.Schritt(z, null, Hd(h0 + V(0.12, 0.04, -0.06)), p);
        Nah(z.Lage.X, 0.12, 1e-9);
        Nah(z.Lage.Z, -0.06, 1e-9);
        Nah(z.Lage.Hoehe, 0.04, 1e-9);
        Nah(z.Lage.Drehung, 0.3, 1e-12);
        Nah(z.Lage.Massstab, 50, 1e-12);
        Assert.Equal(ZweiPunkteEreignis.Losgelassen, ZweiPunkte.Schritt(z, null, Hd(h0 + V(0.12, 0.04, -0.06), pinch: false), p));
        Assert.False(z.Gehalten);
    }

    [Fact]
    public void Drehen_immer_um_das_Zentrum_mit_dem_Winkel_der_Hand_und_Totzone()
    {
        var z = ZweiPunkte.Neu(new SitzungLage(50, 0, 0, 0));
        var p = ZweiPunkte.Punkte(z.Lage, Bezug, null);
        ZweiPunkte.Schritt(z, null, null, p);
        Assert.Equal(ZweiPunkteEreignis.Gefasst, ZweiPunkte.Schritt(z, null, Hd(p.Drehen), p));
        Assert.Equal(TischPunkt.Drehen, z.Punkt);
        var c = p.Zentrum;
        var h0 = new Xz(p.Drehen.X - c.X, p.Drehen.Z - c.Z);
        Vec3 Hand(double winkel, double faktor = 1)
        {
            var d = XrSitzungLogik.DreheXZ(new Xz(h0.X * faktor, h0.Z * faktor), winkel);
            return new Vec3(c.X + d.X, p.Drehen.Y, c.Z + d.Z);
        }
        // 30° um das Zentrum (gleicher Abstand): das Modell dreht 27° (Totzone 3°), der Massstab bleibt; das Zentrum des Modells bleibt am selben Ort
        ZweiPunkte.Schritt(z, null, Hd(Hand(30 * Grad)), p);
        Nah(z.Lage.Drehung, 27 * Grad, 1e-9);
        Nah(z.Lage.Massstab, 50, 1e-9);
        var c1 = ZweiPunkte.Punkte(z.Lage, Bezug, null).Zentrum;
        Nah(c1.X, c.X, 1e-9);
        Nah(c1.Z, c.Z, 1e-9);
        // die andere Richtung
        ZweiPunkte.Schritt(z, null, Hd(Hand(-45 * Grad)), p);
        Nah(z.Lage.Drehung, -42 * Grad, 1e-9);
        // kleine Ausschläge (2°, 3 % Abstand): nichts
        ZweiPunkte.Schritt(z, null, Hd(Hand(2 * Grad, 1.03)), p);
        Nah(z.Lage.Drehung, 0, 1e-12);
        Nah(z.Lage.Massstab, 50, 1e-9);
        // die Höhe der Hand ändert nichts
        ZweiPunkte.Schritt(z, null, Hd(Hand(0) + V(0, 0.1, 0)), p);
        Nah(z.Lage.Hoehe, 0, 1e-12);
    }

    [Fact]
    public void Vergroessern_aus_dem_Abstand_zum_Zentrum_in_den_Grenzen()
    {
        var z = ZweiPunkte.Neu(new SitzungLage(50, 0, 0, 0));
        var p = ZweiPunkte.Punkte(z.Lage, Bezug, null);
        ZweiPunkte.Schritt(z, null, null, p);
        ZweiPunkte.Schritt(z, null, Hd(p.Drehen), p);
        var c = p.Zentrum;
        Vec3 Hand(double faktor) => new(c.X + (p.Drehen.X - c.X) * faktor, p.Drehen.Y, c.Z + (p.Drehen.Z - c.Z) * faktor);
        // anderthalbmal so weit weg: grösser (Faktor 1.5 / 1.05 nach der Totzone von 5 %), ohne Drehung; das Zentrum bleibt
        ZweiPunkte.Schritt(z, null, Hd(Hand(1.5)), p);
        Nah(z.Lage.Massstab, 50 / (1.5 / 1.05), 1e-9);
        Nah(z.Lage.Drehung, 0, 1e-9);
        var c1 = ZweiPunkte.Punkte(z.Lage, Bezug, null).Zentrum;
        Nah(c1.X, c.X, 1e-9);
        Nah(c1.Z, c.Z, 1e-9);
        // näher: kleiner
        ZweiPunkte.Schritt(z, null, Hd(Hand(0.6)), p);
        Nah(z.Lage.Massstab, 50 / (0.6 * 1.05), 1e-9);
        // Grenzen 1:5 … 1:200
        ZweiPunkte.Schritt(z, null, Hd(Hand(30)), p);
        Nah(z.Lage.Massstab, XrSitzungLogik.MassstabMin, 1e-9);
        ZweiPunkte.Schritt(z, null, Hd(Hand(0.05)), p);
        Nah(z.Lage.Massstab, XrSitzungLogik.MassstabMax, 1e-9);
        // Totzonen als Stellschrauben
        var z2 = ZweiPunkte.Neu(new SitzungLage(50, 0, 0, 0));
        var e = ZweiPunkteEinstellungen.Vorgabe with { ZoomTotzone = 0, DrehTotzoneGrad = 0 };
        ZweiPunkte.Schritt(z2, null, null, p, e);
        ZweiPunkte.Schritt(z2, null, Hd(p.Drehen), p, e);
        ZweiPunkte.Schritt(z2, null, Hd(Hand(1.5)), p, e);
        Nah(z2.Lage.Massstab, 50 / 1.5, 1e-9);
    }

    [Fact]
    public void Immer_nur_ein_Punkt_nie_beide_mit_derselben_Hand_nach_dem_Loslassen_wieder_an_seinem_Platz()
    {
        var z = ZweiPunkte.Neu(new SitzungLage(50, 0, 0, 0));
        var p = ZweiPunkte.Punkte(z.Lage, Bezug, null);
        ZweiPunkte.Schritt(z, null, null, p);
        ZweiPunkte.Schritt(z, null, Hd(p.Drehen), p);
        Assert.Equal(("rechts", TischPunkt.Drehen), (z.Hand, z.Punkt));
        // die linke Hand pincht am Punkt «Verschieben»: nichts, der Punkt «Drehen» bleibt bei der rechten
        Assert.Equal(ZweiPunkteEreignis.Nichts, ZweiPunkte.Schritt(z, Hd(p.Verschieben), Hd(p.Drehen), p));
        Assert.Equal(("rechts", TischPunkt.Drehen), (z.Hand, z.Punkt));
        Assert.True(ZweiPunkte.HandAmPunkt(z, "rechts"));
        Assert.False(ZweiPunkte.HandAmPunkt(z, "links"));
        // drehen, loslassen: die Punkte sitzen wieder an der Lage des gedrehten Modells (vorn bzw. vorn rechts)
        var c = p.Zentrum;
        var h = XrSitzungLogik.DreheXZ(new Xz(p.Drehen.X - c.X, p.Drehen.Z - c.Z), 40 * Grad);
        ZweiPunkte.Schritt(z, Hd(p.Verschieben), Hd(new Vec3(c.X + h.X, p.Drehen.Y, c.Z + h.Z)), p);
        Assert.Equal(ZweiPunkteEreignis.Losgelassen, ZweiPunkte.Schritt(z, Hd(p.Verschieben), null, p));
        var p2 = ZweiPunkte.Punkte(z.Lage, Bezug, null);
        Assert.True(p2.Verschieben.Z > p2.Zentrum.Z && p2.Drehen.X > p2.Zentrum.X);
        // die linke Hand hält noch den Pinch von vorher: erst ein neuer Pinch fasst
        Assert.Equal(ZweiPunkteEreignis.Nichts, ZweiPunkte.Schritt(z, Hd(p2.Verschieben), null, p2));
        ZweiPunkte.Schritt(z, null, null, p2);
        Assert.Equal(ZweiPunkteEreignis.Gefasst, ZweiPunkte.Schritt(z, Hd(p2.Verschieben), null, p2));
        Assert.Equal(("links", TischPunkt.Verschieben), (z.Hand, z.Punkt));
        // eine Hand an den Apps (nicht frei) fasst nicht
        var z3 = ZweiPunkte.Neu(new SitzungLage(50, 0, 0, 0));
        ZweiPunkte.Schritt(z3, null, null, p);
        Assert.Equal(ZweiPunkteEreignis.Nichts, ZweiPunkte.Schritt(z3, null, new EinGriffHand(true, p.Drehen, 0, 0, Frei: false), p));
    }

    [Fact]
    public void Variante_griffe_tisch_und_Totzonen_als_Stellschrauben_Farben_der_Punkte()
    {
        static System.Text.Json.JsonElement W(string j) => System.Text.Json.JsonDocument.Parse(j).RootElement.Clone();
        var e = XrBedienEinstellungen.Vorgabe;
        Assert.Equal(Varianten.GriffeZweiPunkte, e.Variante("griffe_tisch"));
        Assert.True(e.Punkte.MitZweiPunkten);
        var ein = e.MitStellschraube("labor.variante.griffe_tisch", W("\"ein_griff\""))!;
        Assert.False(ein.Punkte.MitZweiPunkten);
        Assert.Null(e.MitStellschraube("labor.variante.griffe_tisch", W("\"drei\"")));
        Assert.Equal(5, e.MitStellschraube("sitzung.dreh_totzone_grad", W("5"))!.Punkte.DrehTotzoneGrad);
        Assert.Equal(0.1, e.MitStellschraube("sitzung.zoom_totzone", W("0.1"))!.Punkte.ZoomTotzone);
        Assert.Null(e.MitStellschraube("sitzung.zoom_totzone", W("0.5")));
        // zwei eigene, deutlich verschiedene Farben im App-Farbkatalog
        var k = AppFarben.Lesen(File.ReadAllText(Path.Combine(Repo(), "kataloge", "xr_farben.json")));
        var v = k.Fuer("verschieben").Hell.Akzent;
        var d = k.Fuer("drehen").Hell.Akzent;
        Assert.True(Math.Abs(v.R - d.R) + Math.Abs(v.G - d.G) + Math.Abs(v.B - d.B) > 0.4);
        Assert.Contains("verschieben", AppFarben.Pflicht);
        Assert.Contains("drehen", AppFarben.Pflicht);
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
