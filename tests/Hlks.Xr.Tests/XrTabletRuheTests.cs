// Tests von Modul XR, Teil XR-C: das Tablet beruhigen (Rückmeldung nach dem ersten Brillentest, 21.10.2026: «tablet ist etwas nervös, sollte etwas stabilisiert werden»),
// übertragen aus xr-tablet-ruhe.test.mjs: Glättung der Handflächen-Normale, One-Euro-Filter, Totzone, Nachführen.

using static Hlks.Xr.Tests.T;
using G = Hlks.Xr.XrTabletLogik;
using R = Hlks.Xr.XrTabletRuhe;

namespace Hlks.Xr.Tests;

public class XrTabletRuheTests
{
    /// <summary>Deterministischer Zufall (mulberry32), derselbe Zahlenstrom wie in der JavaScript-Vorlage.</summary>
    static Func<double> Zufall(uint seed)
    {
        var a = seed;
        return () =>
        {
            unchecked
            {
                a += 0x6D2B79F5u;
                var t = a;
                t = (t ^ (t >> 15)) * (t | 1);
                t ^= t + (t ^ (t >> 7)) * (t | 61);
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        };
    }

    /// <summary>Lässt die Kette Palm-Glättung → TabletLage → TabletNachfuehren mit 72 Hz laufen; liefert die gezeigten Lagen je Bild.</summary>
    static List<TabletPose> RuheLauf(double sekunden, Func<double, (Vec3 Ort, Vec3 Normale)> eingabe, int hz = 72)
    {
        var kopf = new Vec3(0, 1.6, 0.8);
        var dt = 1.0 / hz;
        var z = R.NeuerTabletZustand();
        var aus = new List<TabletPose>();
        TabletPose? gezeigt = null;
        for (var i = 0; i <= (int)Math.Round(sekunden * hz); i++)
        {
            var e = eingabe(i * dt);
            var n = R.TabletPalmGlaetten(z, e.Normale, dt);
            gezeigt = R.TabletNachfuehren(gezeigt, G.TabletLage(e.Ort, n, kopf), dt, z);
            aus.Add(gezeigt);
        }
        return aus;
    }

    /// <summary>Normale (0, 1, 0), um ax/az (Grad) gekippt.</summary>
    static Vec3 Kipp(double ax, double az) => new Vec3(Math.Tan(az * Grad), 1, Math.Tan(ax * Grad)).Einheit();

    [Fact]
    public void Zittern_5_mm_2_Grad_2_s_bei_72_Hz_bewegt_das_Tablet_hoechstens_2_mm_bzw_1_Grad()
    {
        foreach (var seed in new uint[] { 1, 2, 3, 4, 5 })
        {
            var r = Zufall(seed);
            double S(double a) => (r() * 2 - 1) * a;
            var lagen = RuheLauf(2, _ => (new Vec3(0.1 + S(0.005), 1.1 + S(0.005), -0.3 + S(0.005)), Kipp(S(2), S(2))));
            var l0 = lagen[0];
            double dm = 0, dn = 0;
            foreach (var l in lagen)
            {
                dm = Math.Max(dm, l.Mitte.Abstand(l0.Mitte));
                dn = Math.Max(dn, R.WinkelGrad(l.Normale, l0.Normale));
            }
            Assert.True(dm <= 0.002, $"Seed {seed}: Mitte bewegt sich um {dm * 1000:F2} mm");
            Assert.True(dn <= 1, $"Seed {seed}: Normale bewegt sich um {dn:F2}°");
        }
    }

    [Fact]
    public void Ein_Sprung_um_10_cm_ist_nach_0_3_s_zu_mindestens_90_Prozent_eingeholt_und_kommt_zur_Ruhe()
    {
        var lagen = RuheLauf(2, t => (new Vec3(t < 0.5 ? 0 : 0.1, 1.1, -0.3), new Vec3(0, 1, 0)));
        var vorher = lagen[(int)Math.Round(0.4 * 72)].Mitte;
        var ende = lagen[^1].Mitte;
        var nach = lagen[(int)Math.Round(0.5 * 72) + (int)Math.Round(0.3 * 72)].Mitte;
        var weg = vorher.Abstand(ende);
        Assert.True(weg > 0.09 && weg < 0.11, $"Sprungweite {weg}");
        Assert.True(nach.Abstand(ende) <= 0.1 * weg, $"nach 0,3 s fehlen noch {nach.Abstand(ende) * 1000:F1} mm");
        foreach (var l in lagen.TakeLast(20)) Assert.True(l.Mitte.Abstand(ende) < 1e-9, "steht am Ende still");
    }

    [Fact]
    public void Eine_Drehung_der_Hand_um_20_Grad_wird_gefolgt()
    {
        var lagen = RuheLauf(2, t => (new Vec3(0, 1.1, -0.3), Kipp(t < 0.5 ? 0 : 20, 0)));
        var ziel = G.TabletLage(new Vec3(0, 1.1, -0.3), Kipp(20, 0), new Vec3(0, 1.6, 0.8));
        var ende = lagen[^1];
        Assert.True(ende.Mitte.Abstand(ziel.Mitte) < 0.015, $"Mitte weicht {ende.Mitte.Abstand(ziel.Mitte) * 1000:F1} mm ab");
        // Hebel TABLET_ABSTAND 0.025 m: 20° ≈ 0.9 cm, unter der Totzone – das Tablet bleibt bei der Handdrehung stehen (es folgt der Lage der Hand, nicht ihrer Neigung)
        Assert.True(ende.Mitte.Abstand(lagen[0].Mitte) <= R.TotzoneM, "kleiner Hebel: die Neigung der Hand bewegt das Tablet kaum");
    }

    [Fact]
    public void Oeffnen_uebernimmt_die_erste_Lage_sofort_langsames_Wandern_wird_nachgefuehrt()
    {
        var kopf = new Vec3(0, 1.6, 0.8);
        var z = R.NeuerTabletZustand();
        var soll = G.TabletLage(new Vec3(0.3, 1.2, -0.2), new Vec3(0, 1, 0), kopf);
        var erste = R.TabletNachfuehren(null, soll, 1.0 / 72, z);
        Assert.Equal(soll, erste);   // erste Lage unverändert
        var lagen = RuheLauf(4, t => (new Vec3(0.05 * t, 1.1, -0.3), new Vec3(0, 1, 0)));
        var ende = lagen[^1];
        Assert.True(Math.Abs(ende.Mitte.X - lagen[0].Mitte.X - 0.2) < R.TotzoneM + 0.01, $"Rückstand: Weg {ende.Mitte.X - lagen[0].Mitte.X}");
    }

    [Fact]
    public void Controller_Fall_feste_Normale_wackelnde_Hand_bleibt_in_der_Totzone_ruhig()
    {
        var r = Zufall(9);
        double S(double a) => (r() * 2 - 1) * a;
        var lagen = RuheLauf(2, _ => (new Vec3(S(0.004), 1.0 + S(0.004), -0.2 + S(0.004)), new Vec3(0, 1, 0)));
        double dm = 0;
        foreach (var l in lagen) dm = Math.Max(dm, l.Mitte.Abstand(lagen[0].Mitte));
        Assert.True(dm <= 0.002, $"{dm}");
    }

    [Fact]
    public void Totzone_und_Filterwerte_sind_benannte_Konstanten()
    {
        Assert.True(R.TotzoneM > 0 && R.TotzoneGrad > 0 && R.RuheAnteil < 1);
        Assert.True(R.FilterMitte.MinCut > 0 && R.FilterPalm.MinCut > 0);
        Assert.Equal(0.015, R.TotzoneM);
        Assert.Equal(4, R.TotzoneGrad);
        Assert.Equal(0.3, R.RuheAnteil);
        Assert.Equal(0.04, R.FolgeZeit);
        Assert.Equal(new EinEuroParameter(1.2, 40, 3), R.FilterMitte);
        Assert.Equal(new EinEuroParameter(1.2, 8, 3), R.FilterNormale);
        Assert.Equal(new EinEuroParameter(0.8, 8, 3), R.FilterPalm);
    }

    [Fact]
    public void EinEuroVektor_der_erste_Wert_geht_durch_ein_Sprung_wird_gedaempft_dt_wird_begrenzt()
    {
        var z = new EinEuroZustand();
        var p = new EinEuroParameter(1.2, 0, 3);
        Assert.Equal(new Vec3(1, 2, 3), R.EinEuroVektor(z, new Vec3(1, 2, 3), 0.014, p));
        var zweiter = R.EinEuroVektor(z, new Vec3(2, 2, 3), 0.014, p);
        Assert.True(zweiter.X > 1 && zweiter.X < 2, $"{zweiter.X}");
        // gleicher Wert bleibt stehen
        var z2 = new EinEuroZustand();
        R.EinEuroVektor(z2, new Vec3(5, 5, 5), 0.014, p);
        Assert.Equal(new Vec3(5, 5, 5), R.EinEuroVektor(z2, new Vec3(5, 5, 5), 0.014, p));
        // dt = 0 oder riesig: begrenzt auf 1 ms … 100 ms, keine NaN
        var z3 = new EinEuroZustand();
        R.EinEuroVektor(z3, Vec3.Null, 0, p);
        Assert.True(R.EinEuroVektor(z3, new Vec3(1, 0, 0), 0, p).IstEndlich);
        Assert.True(R.EinEuroVektor(z3, new Vec3(1, 0, 0), 100, p).IstEndlich);
        // WinkelGrad
        Nah(R.WinkelGrad(new Vec3(1, 0, 0), new Vec3(0, 1, 0)), 90, 1e-9);
        Nah(R.WinkelGrad(new Vec3(2, 0, 0), new Vec3(1, 0, 0)), 0, 1e-6);
    }
}
