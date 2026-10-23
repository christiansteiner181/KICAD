// Tests der Wischgeste (Modul XR, Teil XR-C): Masslinien wegwischen, übertragen aus xr-wisch.test.mjs. Nur die Rechenregeln, ohne Brille.

using static Hlks.Xr.Tests.T;
using W = Hlks.Xr.XrWischLogik;

namespace Hlks.Xr.Tests;

public class XrWischTests
{
    const double BILD = 1.0 / 72;

    /// <summary>Verlauf einer Hand, die in <paramref name="dauer"/> s geradlinig von a nach b geht (Bilder zu 1/72 s ab t0).</summary>
    static List<WischProbe> Strich(Vec3 a, Vec3 b, double dauer, double t0 = 0)
    {
        var n = Math.Max(2, (int)Math.Round(dauer / BILD, MidpointRounding.AwayFromZero));
        return Enumerable.Range(0, n + 1).Select(i => new WischProbe(t0 + (double)i / n * dauer,
            new Vec3(a.X + (b.X - a.X) * i / n, a.Y + (b.Y - a.Y) * i / n, a.Z + (b.Z - a.Z) * i / n))).ToList();
    }

    /// <summary>Spielt einen Verlauf durch WischSchritt; Rückgabe alle Ergebnisse (null oder Wisch).</summary>
    static List<WischErgebnis?> Abspielen(Wischer z, IEnumerable<WischProbe> verlauf, Func<WischProbe, bool>? erlaubt = null)
        => verlauf.Select(s => W.WischSchritt(z, s.T, s.P, erlaubt?.Invoke(s) ?? true)).ToList();

    [Fact]
    public void Wisch_erkannt_30_cm_waagrecht_in_0_25_s_1_2_m_s()
    {
        var w = W.WischErkennen(Strich(V(0, 1.2, -0.5), V(0.3, 1.2, -0.5), 0.25))!;
        Assert.NotNull(w);
        Assert.True(w.Strecke >= W.WischWegM && w.V >= W.WischVMs && w.Dauer <= W.WischZeitS);
        Assert.True(w.Richtung.X > 0.99, "Richtung +x");
        Assert.True(w.Weg.Count > 5);
    }

    [Fact]
    public void Wisch_mit_schwach_geneigter_Bahn_20_Grad_gegen_die_Waagrechte_zaehlt_60_Grad_nicht()
    {
        var a = V(0, 1.2, -0.5);
        Assert.NotNull(W.WischErkennen(Strich(a, V(0.3, 1.2 + 0.3 * Math.Tan(20 * Grad), -0.5), 0.25)));
        Assert.Null(W.WischErkennen(Strich(a, V(0.3, 1.2 + 0.3 * Math.Tan(60 * Grad), -0.5), 0.25)));
    }

    [Fact]
    public void Nicht_erkannt_langsam_kurz_senkrecht_Ausreisser_in_einem_Bild()
    {
        var a = V(0, 1.2, -0.5);
        Assert.Null(W.WischErkennen(Strich(a, V(0.3, 1.2, -0.5), 0.8)));   // langsam (0.4 m/s)
        Assert.Null(W.WischErkennen(Strich(a, V(0.3, 1.2, -0.5), 0.45)));   // zu lange (0.67 m/s)
        Assert.Null(W.WischErkennen(Strich(a, V(0.1, 1.2, -0.5), 0.1)));   // zu kurz (10 cm)
        Assert.Null(W.WischErkennen(Strich(a, V(0, 1.5, -0.5), 0.2)));   // senkrecht
        Assert.Null(W.WischErkennen([new WischProbe(0, a), new WischProbe(BILD, V(0.4, 1.2, -0.5))]));   // Sprung in einem Bild
        Assert.Null(W.WischErkennen([]));
        Assert.Null(W.WischErkennen([new WischProbe(0, a)]));
        Assert.Null(W.WischErkennen(null));
    }

    [Fact]
    public void WischSchritt_Wisch_erkannt_nicht_erkannt_bei_Faust_oder_Pinch_erlaubt_gleich_false()
    {
        var verlauf = Strich(V(0, 1.2, -0.5), V(0.4, 1.2, -0.5), 0.3);
        var z = W.NeuerWischer();
        var erg = Abspielen(z, verlauf);
        Assert.Contains(erg, e => e?.Neu == true);   // Wisch erkannt
        Assert.Equal(1, erg.Count(e => e?.Neu == true));   // genau ein erstes Bild
        // Faust / Pinch: die Hand ist nicht «erlaubt»
        var z2 = W.NeuerWischer();
        Assert.True(Abspielen(z2, verlauf, _ => false).All(e => e is null), "Faust/Pinch/Tablet: nichts");
    }

    [Fact]
    public void Sperre_nach_einem_Wisch_0_5_s_lang_kein_zweiter()
    {
        var z = W.NeuerWischer();
        var v1 = Strich(V(0, 1.2, -0.5), V(0.4, 1.2, -0.5), 0.3, 0);
        Abspielen(z, v1);
        // Hand steht (Wisch endet) und wischt sofort zurück: gesperrt
        var halt = Enumerable.Range(0, 36).Select(i => new WischProbe(0.3 + (i + 1) * BILD, V(0.4, 1.2, -0.5))).ToList();   // der Strich verlässt das Fenster nach 0.4 s
        Abspielen(z, halt);
        Assert.True(z.SperreBis > 0.3, $"Sperre bis {z.SperreBis}");
        var t1 = halt[^1].T;
        var zurueck = Strich(V(0.4, 1.2, -0.5), V(0, 1.2, -0.5), 0.1, t1 + BILD);   // 4 m/s, ganz innerhalb der Sperre
        Assert.True(t1 + BILD + 0.1 < z.SperreBis, $"Sperre bis {z.SperreBis}");
        Assert.True(Abspielen(z, zurueck).All(e => e is null), "innerhalb der Sperre nichts");
        // nach der Sperre geht es wieder
        var spaeter = Strich(V(0, 1.2, -0.5), V(0.4, 1.2, -0.5), 0.3, z.SperreBis + 0.1);
        Assert.Contains(Abspielen(z, spaeter), e => e?.Neu == true);   // nach der Sperre erkannt
    }

    [Fact]
    public void Anlauf_aus_der_Ruhe_der_Verlauf_wird_gesammelt_bis_die_Schwellen_erfuellt_sind()
    {
        var z = W.NeuerWischer();
        var ruhe = Enumerable.Range(0, 10).Select(i => new WischProbe(i * BILD, V(0, 1.2, -0.5))).ToList();
        Abspielen(z, ruhe);
        Assert.Contains(Abspielen(z, Strich(V(0, 1.2, -0.5), V(0.4, 1.2, -0.5), 0.3, 10 * BILD)), e => e?.Neu == true);
    }

    [Fact]
    public void Strecken_Abstand_gekreuzt_parallel_windschief_Punkt()
    {
        Nah(W.StreckenAbstand(V(-1, 0, 0), V(1, 0, 0), V(0, 0.3, -1), V(0, 0.3, 1)), 0.3);   // windschief, 30 cm darüber
        Nah(W.StreckenAbstand(V(0, 0, 0), V(1, 0, 0), V(0, 0.2, 0), V(1, 0.2, 0)), 0.2);   // parallel
        Nah(W.StreckenAbstand(V(0, 0, 0), V(1, 0, 0), V(2, 0, 0), V(3, 0, 0)), 1);   // in einer Linie, getrennt
        Nah(W.StreckenAbstand(V(0, 0, 0), V(0, 0, 0), V(1, 0, 0), V(1, 1, 0)), 1);   // Punkt gegen Strecke
        Nah(W.StreckenAbstand(V(-1, 0, 0), V(1, 0, 0), V(0, 0, -1), V(0, 0, 1)), 0);   // schneiden sich
        Nah(W.StreckenAbstand(V(0, 0, 0), V(1, 0, 0), V(5, 0, 0), V(5, 0, 0)), 4);   // Strecke gegen Punkt
        Nah(W.StreckenAbstand(V(1, 1, 1), V(1, 1, 1), V(1, 1, 1), V(1, 1, 1)), 0);   // zwei Punkte
    }

    [Fact]
    public void Treffer_Linie_im_Abstand_unter_10_cm_getroffen_darueber_nicht_mehrere_zugleich_ins_Leere_nichts()
    {
        // Handweg: 40 cm in x-Richtung auf Höhe 1.2 m, z = -0.5
        var w = W.WischErkennen(Strich(V(0, 1.2, -0.5), V(0.4, 1.2, -0.5), 0.3))!;
        Assert.NotNull(w);
        Masslinie Quer(double x, double dz, double y = 1.2) => new(V(x, y - 0.03, -0.5 + dz), V(x, y + 0.03, -0.5 + dz));   // kurze senkrechte Masslinie bei x, dz vor/hinter dem Weg, auf Höhe y
        var linien = new[]
        {
            Quer(0.2, 0.0),         // 0: mitten im Weg → getroffen
            Quer(0.2, 0.07),        // 1: 7 cm daneben (in z) → getroffen
            Quer(0.2, 0.0, 1.4),    // 2: 17 cm darüber → nicht
            Quer(0.2, 0.0, 1.0),    // 3: 17 cm darunter → nicht
            Quer(0.8, 0.0),         // 4: jenseits des Wegs (40 cm hinter dem Ende) → nicht
        };
        Gleich([0, 1], W.WischTreffer(w.Weg, linien));
        Assert.Empty(W.WischTreffer(w.Weg, [linien[2], linien[3], linien[4]]));   // Wisch ins Leere: nichts
        Assert.Empty(W.WischTreffer([], linien));
        Assert.Empty(W.WischTreffer(null, linien));
        // Grenzwert: knapp unter bzw. über WISCH_TREFFER_M
        Gleich([0], W.WischTreffer(w.Weg, [Quer(0.2, W.WischTrefferM - 0.01)]));
        Assert.Empty(W.WischTreffer(w.Weg, [Quer(0.2, W.WischTrefferM + 0.01)]));
    }

    [Fact]
    public void Durchlauf_Wisch_von_weit_vor_der_Linie_bis_hinter_sie_loescht_die_Linie_Treffer_waehrend_des_Strichs()
    {
        var z = W.NeuerWischer();
        var linie = new Masslinie(V(0.5, 1.1, -0.5), V(0.5, 1.3, -0.5));
        var geloescht = false;
        foreach (var s in Strich(V(0, 1.2, -0.5), V(0.8, 1.2, -0.5), 0.5))
        {
            var w = W.WischSchritt(z, s.T, s.P, true);
            if (w is not null && W.WischTreffer(w.Weg, [linie]).Count > 0) geloescht = true;
        }
        Assert.True(geloescht, "die Linie liegt am Weg und wird getroffen");
        // Ein Wisch weit weg (1 m) trifft nicht
        var z2 = W.NeuerWischer();
        var fern = new Masslinie(V(0.5, 1.1, -1.6), V(0.5, 1.3, -1.6));
        var treffer = false;
        foreach (var s in Strich(V(0, 1.2, -0.5), V(0.8, 1.2, -0.5), 0.5))
        {
            var w = W.WischSchritt(z2, s.T, s.P, true);
            if (w is not null && W.WischTreffer(w.Weg, [fern]).Count > 0) treffer = true;
        }
        Assert.False(treffer);
    }

    [Fact]
    public void Sprung_des_Trackings_Hand_springt_1_m_in_einem_Bild_ist_kein_Wisch()
    {
        var z = W.NeuerWischer();
        var ruhe = Enumerable.Range(0, 12).Select(i => new WischProbe(i * BILD, V(0, 1.2, -0.5))).ToList();
        var danach = Enumerable.Range(0, 12).Select(i => new WischProbe((12 + i) * BILD, V(1.0, 1.2, -0.5))).ToList();
        Assert.True(Abspielen(z, ruhe).Concat(Abspielen(z, danach)).All(e => e is null));
    }

    [Fact]
    public void WischSchritt_ohne_Ort_beendet_den_Wisch_und_setzt_die_Sperre()
    {
        var z = W.NeuerWischer();
        Abspielen(z, Strich(V(0, 1.2, -0.5), V(0.4, 1.2, -0.5), 0.3));
        Assert.True(z.Aktiv);
        Assert.Null(W.WischSchritt(z, 0.4, null, true));   // Hand nicht verfolgt
        Assert.False(z.Aktiv);
        Assert.Equal(0.4 + W.WischSperreS, z.SperreBis);
        Assert.Empty(z.Verlauf);
    }

    [Fact]
    public void Konstanten_der_Wischgeste()
    {
        Assert.Equal(0.15, W.WischWegM);
        Assert.Equal(0.4, W.WischZeitS);
        Assert.Equal(0.08, W.WischMinZeitS);
        Assert.Equal(0.8, W.WischVMs);
        Assert.Equal(0.8, W.WischWaagrecht);
        Assert.Equal(0.5, W.WischVerlaufS);
        Assert.Equal(0.5, W.WischSperreS);
        Assert.Equal(0.10, W.WischTrefferM);
        Assert.Equal(0.30, W.WischTabletNaeheM);
        Assert.Equal(0.2, W.WischAusblendenS);
        Assert.Equal(8, W.WischSprungMs);
    }
}
