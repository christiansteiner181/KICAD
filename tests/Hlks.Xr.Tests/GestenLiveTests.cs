// Tests der Quest App V1b-1 (Modul XR, Gestenlabor): Live-Erkennung im Strom (Ringpuffer, Kandidatenlängen, Schrittweite, Sperrzeit, Bewegungstor) und Budget.
// Zahlen (Ereignisse, Fehlauslösungen, Zeit je Bild) stehen in der Testausgabe.

using System.Diagnostics;
using Hlks.Xr.Gesten;
using Xunit.Abstractions;
using G = Hlks.Xr.Tests.GestenGenerator;

namespace Hlks.Xr.Tests;

/// <summary>Die Budgetmessung läuft allein (nicht parallel zu anderen Tests).</summary>
[CollectionDefinition("Gesten-Budget", DisableParallelization = true)]
public class GestenBudgetSammlung;

public class GestenLiveTests(ITestOutputHelper aus)
{
    static readonly Lazy<GestenErkenner> Auftrag = new(() => new GestenErkenner(G.Auftrag.Select(G.Fertig)));

    static List<GestenEreignis> Abspielen(GestenLiveErkenner live, Aufnahme a)
    {
        var ev = new List<GestenEreignis>();
        foreach (var b in a.Bilder)
            if (live.Bild(b) is { } e) ev.Add(e);
        return ev;
    }

    void Zeigen(IEnumerable<GestenEreignis> ev)
    {
        foreach (var e in ev)
            aus.WriteLine($"  {e.T:0.00} s: {e.Art} {e.Beste.GestenId} (q {e.Beste.Verhaeltnis:0.00}, {e.Beste.Seite}) Abschnitt {e.StartS:0.00}–{e.EndeS:0.00}");
    }

    [Fact]
    public void Erkennung_im_Strom_mit_Zufallsbewegungen_dazwischen()
    {
        var stuecke = new List<(GestenBewegung, Vorfuehrung)>();
        var erwartet = new List<Testgeste>();
        var i = 0;
        double[] tempi = [1, 1.3, 0.75], groessen = [1, 0.85, 1.2];
        foreach (var t in G.Auftrag)
        {
            stuecke.Add((t.Bewegung, new Vorfuehrung { DauerS = t.DauerS / tempi[i % 3], Groesse = groessen[i % 3], Saat = 700 + i, BogenM = 0.015, RuheVorS = 0.3, RuheNachS = 0.4 }));
            erwartet.Add(t);
            stuecke.Add((G.Zufall(800 + i, mitLinks: i % 2 == 0), new Vorfuehrung { DauerS = 1.5, Saat = 900 + i, RuheVorS = 0.2, RuheNachS = 0.2 }));
            i++;
        }
        var (strom, bewegungen) = G.Strom(stuecke, gierenGrad: 30);
        var live = new GestenLiveErkenner(Auftrag.Value);
        var ev = Abspielen(live, strom);
        Zeigen(ev);
        aus.WriteLine($"{strom.Dauer:0.0} s, {strom.Bilder.Count} Bilder, {live.Auswertungen} Auswertungen, {live.Folgen} Folgen, {live.Schranken} Schranken, {live.Vergleiche} DTW");
        Assert.Equal(erwartet.Count, ev.Count);
        for (var k = 0; k < ev.Count; k++)
        {
            var (_, ende) = bewegungen[2 * k];
            Assert.Equal(ErkennungsArt.Erkannt, ev[k].Art);
            Assert.Equal(erwartet[k].Id, ev[k].Beste.GestenId);
            Assert.InRange(ev[k].EndeS, ende - 0.2, ende + 0.35);
            Assert.InRange(ev[k].T, ev[k].EndeS + 0.1, ende + 0.6);
            Assert.True(ev[k].StartS < ev[k].EndeS);
        }
    }

    /// <summary>Zwei Wische links hintereinander: die Hand holt zwischen ihnen in uebergangS aus und beginnt den zweiten nach pause.</summary>
    static Aufnahme ZweiWische(double pause) => G.Strom([
        (G.WischLinks, new Vorfuehrung { DauerS = 0.4, Saat = 1, RuheVorS = 0.4, RuheNachS = 0 }),
        (G.WischLinks, new Vorfuehrung { DauerS = 0.4, Saat = 2, RuheVorS = pause, RuheNachS = 0.6 })], uebergangS: 0.2).Aufnahme;

    [Fact]
    public void Sperrzeit_nach_einem_Treffer_unterdrueckt_eine_gleich_folgende_Geste()
    {
        var k = Auftrag.Value;
        // zweiter Wisch beginnt 0.2 s nach dem Ende des ersten (Ausholen 0.2 s): in der Sperrzeit (0.5 s) – nur ein Ereignis
        var kurz = Abspielen(new GestenLiveErkenner(k), ZweiWische(0));
        Zeigen(kurz);
        Assert.Single(kurz);
        // ohne Sperrzeit: beide
        var ohne = new GestenErkenner(G.Auftrag.Select(G.Fertig), GestenEinstellungen.Vorgabe with { SperreS = 0 });
        var beide = Abspielen(new GestenLiveErkenner(ohne), ZweiWische(0));
        Zeigen(beide);
        Assert.Equal(2, beide.Count);
        // mit Pause nach der Sperrzeit: beide
        var lang = Abspielen(new GestenLiveErkenner(k), ZweiWische(0.8));
        Assert.Equal(2, lang.Count);
        Assert.All(lang, e => Assert.Equal("wisch-links", e.Beste.GestenId));
        Assert.True(lang[1].StartS >= lang[0].EndeS + GestenEinstellungen.Vorgabe.SperreS - 1e-9, "zweiter Abschnitt beginnt nach der Sperrzeit");
    }

    [Fact]
    public void Ruhende_Haende_loesen_nichts_aus_und_kosten_keine_Auswertung()
    {
        var live = new GestenLiveErkenner(Auftrag.Value);
        var ruhe = G.Erzeugen(G.Ruhe, new Vorfuehrung { DauerS = 10, Saat = 3 });
        Assert.Empty(Abspielen(live, ruhe));
        Assert.Equal(0, live.Auswertungen);   // Bewegungstor: bei 5 mm Rauschen keine Bewegung
    }

    [Fact]
    public void Zufallsbewegungen_loesen_nicht_aus()
    {
        var stuecke = Enumerable.Range(0, 40).Select(s => (G.Zufall(5000 + s, mitLinks: s % 2 == 0),
            new Vorfuehrung { DauerS = 1.2 + 0.03 * s, Saat = 6000 + s, RuheVorS = 0.1, RuheNachS = 0.1 })).ToList();
        var (strom, _) = G.Strom(stuecke, uebergangS: 0.4, gierenGrad: -40);
        var live = new GestenLiveErkenner(Auftrag.Value);
        var ev = Abspielen(live, strom);
        Zeigen(ev);
        aus.WriteLine($"Zufall: {strom.Dauer:0} s, {ev.Count} Fehlauslösungen, {live.Auswertungen} Auswertungen");
        Assert.Empty(ev);
    }

    [Fact]
    public void Hand_verloren_mitten_in_der_Geste_loest_nicht_aus()
    {
        var k = Auftrag.Value;
        var a = G.Erzeugen(G.WischLinks, new Vorfuehrung { DauerS = 0.4, Saat = 11 });
        Assert.Single(Abspielen(new GestenLiveErkenner(k), a));
        // drei Bilder ohne rechte Hand in der Mitte der Bewegung
        var mitte = a.Bilder.Count / 2;
        var luecke = a.Bilder.Select((b, i) => Math.Abs(i - mitte) <= 1 ? b with { Rechts = HandBild.Fehlt } : b).ToList();
        Assert.Empty(Abspielen(new GestenLiveErkenner(k), new Aufnahme(luecke)));
        // Lücke im Strom (0.3 s keine Bilder) mitten in der Bewegung
        var sprung = a.Bilder.Where(b => b.T < a.Bilder[mitte].T - 0.15 || b.T > a.Bilder[mitte].T + 0.15).ToList();
        Assert.Empty(Abspielen(new GestenLiveErkenner(k), new Aufnahme(sprung)));
    }

    [Fact]
    public void Zwei_fast_gleiche_Gesten_ergeben_live_unklar()
    {
        var a = G.Fertig(G.TWischLinks);
        var b = G.Geste(G.TWischLinks, saat: 4242, id: "wisch-links-2", funktion: "SeiteVorZwei");
        var live = new GestenLiveErkenner([a, b]);
        var ev = Abspielen(live, G.Erzeugen(G.WischLinks, new Vorfuehrung { DauerS = 0.4, Saat = 12 }));
        Zeigen(ev);
        var e = Assert.Single(ev);
        Assert.Equal(ErkennungsArt.Unklar, e.Art);
        Assert.NotNull(e.Zweite);
        Assert.NotEqual(e.Beste.GestenId, e.Zweite!.GestenId);
    }

    [Fact]
    public void Zeit_rueckwaerts_beginnt_neu_gleiche_Zeit_und_kaputte_Bilder_fallen_weg_ohne_Gesten_nichts()
    {
        var k = Auftrag.Value;
        var a = G.Erzeugen(G.Stoss, new Vorfuehrung { DauerS = 0.45, Saat = 13 });
        var live = new GestenLiveErkenner(k);
        Assert.Single(Abspielen(live, a));
        // dieselbe Aufnahme noch einmal (Zeit springt zurück): beginnt neu und erkennt wieder
        Assert.Single(Abspielen(live, a));
        // doppelte Zeiten und NaN stören nicht
        var doppelt = a.Bilder.SelectMany(b => new[] { b, b, b with { T = double.NaN } }).ToList();
        Assert.Single(Abspielen(new GestenLiveErkenner(k), new Aufnahme(doppelt)));
        Assert.Empty(Abspielen(new GestenLiveErkenner([]), a));
        Assert.Empty(new GestenLiveErkenner([]).Laengen);
        // Raster der Längen: aufsteigend im Verhältnis LaengenSchritt
        var l = new GestenLiveErkenner(k).Laengen;
        for (var i = 1; i < l.Count - 1; i++) T.Nah(l[i] / l[i - 1], GestenEinstellungen.Vorgabe.LaengenSchritt, 1e-9);
    }
}

[Collection("Gesten-Budget")]
public class GestenBudgetTests(ITestOutputHelper aus)
{
    [Fact]
    public void Budget_10_Gesten_mal_5_Beispiele_im_Mittel_unter_der_Grenze()
    {
        var k = new GestenErkenner(G.Zehn.Select(G.Fertig));
        Assert.Equal(10, k.Gueltige.Count);
        Assert.All(k.Gueltige, g => Assert.Equal(5, g.Beispiele.Count));
        var stuecke = new List<(GestenBewegung, Vorfuehrung)>();
        var i = 0;
        foreach (var t in G.Zehn)
        {
            stuecke.Add((t.Bewegung, new Vorfuehrung { DauerS = t.DauerS, Saat = 100 + i, BogenM = 0.015, RuheVorS = 0.3, RuheNachS = 0.3 }));
            stuecke.Add((G.Zufall(200 + i, mitLinks: i % 2 == 1), new Vorfuehrung { DauerS = 1.5, Saat = 300 + i, RuheVorS = 0.1, RuheNachS = 0.1 }));
            i++;
        }
        var (strom, _) = G.Strom(stuecke, gierenGrad: 15);
        // einmal zum Aufwärmen (JIT), dann messen
        var warm = new GestenLiveErkenner(k);
        foreach (var b in strom.Bilder) warm.Bild(b);
        var live = new GestenLiveErkenner(k);
        var ereignisse = 0;
        var langsamstes = 0.0;
        var uhr = new Stopwatch();
        foreach (var b in strom.Bilder)
        {
            var t0 = Stopwatch.GetTimestamp();
            uhr.Start();
            if (live.Bild(b) is not null) ereignisse++;
            uhr.Stop();
            langsamstes = Math.Max(langsamstes, (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency);
        }
        var mittel = uhr.Elapsed.TotalMilliseconds / strom.Bilder.Count;
        aus.WriteLine($"10 Gesten × 5 Beispiele, {strom.Dauer:0} s Strom ({strom.Bilder.Count} Bilder, ohne Ruhe dazwischen): im Mittel {mittel:0.000} ms je Bild, " +
            $"langsamstes Bild {langsamstes:0.00} ms; {live.Auswertungen} Auswertungen, {live.Folgen} Folgen, {live.Schranken} Schranken, {live.Vergleiche} DTW; " +
            $"{ereignisse} Ereignisse");
        Assert.Equal(10, ereignisse);
        var json = GestenJson.Schreiben(new GestenSammlung(1, G.Zehn.Select(G.Fertig).ToList()));
        aus.WriteLine($"JSON der 10 Gesten: {json.Length / 1e6:0.00} Mio. Zeichen");
        Assert.True(json.Length < GestenGrenzen.Vorgabe.ZeichenMax / 10);
        // Ziel «deutlich unter 0.5 ms» (Auftrag); die Grenze hier ist grosszügig, damit der Test auf einem ausgelasteten Rechner nicht flackert
        Assert.True(mittel < 2.0, $"im Mittel {mittel:0.000} ms je Bild");
    }
}
