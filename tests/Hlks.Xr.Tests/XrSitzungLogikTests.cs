// Tests von Modul XR, Teil XR-H (Sitzungsraum mit Modell auf dem Tisch), übertragen aus xr-sitzung.test.mjs: Einpassen 1:50, Zoomgrenzen, Griffe, Drehen mit einer Hand,
// Zoom mit zwei Händen um den Mittelpunkt, Gestenablauf, Schnitthöhe unter und über der Decke, «Alle Etagen», Zurücksetzen, Tablet-Seite, Ausschnitt.

using static Hlks.Xr.Tests.T;
using S = Hlks.Xr.XrSitzungLogik;
using Tab = Hlks.Xr.XrTabletLogik;

namespace Hlks.Xr.Tests;

public class XrSitzungLogikTests
{
    // ------------------------------------------------------------------------------------------------ Einpassen und Grenzen

    [Fact]
    public void Einpassen_zu_Beginn_1_50_in_der_Mitte_der_Platte_Drehung_0()
    {
        var e = S.Einpassen(new ModellGroesse(12.8, 8.8, 9));
        var l = e.Lage;
        Assert.Equal(50, l.Massstab);
        Assert.Equal(0, l.Drehung);
        Assert.Equal(0, l.X);
        Assert.Equal(0, l.Z);
        Assert.True(e.Passt);
        // 12.8 m / 50 = 0.256 m: passt auf die nutzbare Platte (1.4 × 0.8 m)
        var p = S.Platte();
        Nah(p.B, 1.4);
        Nah(p.T, 0.8);
        Assert.True(12.8 / l.Massstab <= p.B && 8.8 / l.Massstab <= p.T);
    }

    [Fact]
    public void Einpassen_ein_grosses_Modell_bekommt_einen_kleineren_Massstab_hoechstens_1_200()
    {
        var gross = S.Einpassen(new ModellGroesse(100, 30, 20));   // 100 m / 50 = 2 m: zu breit für 1.4 m
        Assert.Equal(Math.Ceiling(100 / 1.4), gross.Lage.Massstab);
        Assert.True(100 / gross.Lage.Massstab <= 1.4 + 1e-9);
        Assert.True(gross.Passt);
        var riesig = S.Einpassen(new ModellGroesse(600, 40, 10));
        Assert.Equal(S.MassstabMax, riesig.Lage.Massstab);
        Assert.False(riesig.Passt);
        var tief = S.Einpassen(new ModellGroesse(10, 60, 10));   // Tiefe 60 m / 0.8 m
        Assert.Equal(75, tief.Lage.Massstab);
        Assert.True(60 / tief.Lage.Massstab <= 0.8 + 1e-9);
        foreach (var u in new ModellGroesse?[] { null, new(0, 0), new(double.NaN, 3) })
            Assert.Equal(50, S.Einpassen(u).Lage.Massstab);   // ohne gültige Grösse 1:50
    }

    [Fact]
    public void Zoomgrenzen_1_20_bis_1_200_ungueltige_Werte_fallen_auf_1_50()
    {
        Assert.Equal(20, S.MassstabBegrenzen(5));
        Assert.Equal(200, S.MassstabBegrenzen(1000));
        Assert.Equal(80, S.MassstabBegrenzen(80));
        Assert.Equal(50, S.MassstabBegrenzen(double.NaN));
        Assert.Equal("1:50", S.MassstabText(50));
        Assert.Equal("1:50", S.MassstabText(49.6));
        Assert.Equal("1:20", S.MassstabText(3));
        // Die Mitte des Modells bleibt auf der Platte
        var l = S.LageBegrenzen(new SitzungLage(50, 0, 5, -5));
        Nah(l.X, S.TischBreite / 2 - S.TischHalt);
        Nah(l.Z, -(S.TischTiefe / 2 - S.TischHalt));
    }

    [Fact]
    public void Tischmitte_liegt_0_9_m_vor_dem_Kopf_auf_Plattenhoehe()
    {
        Assert.Equal(new Vec3(0, 0.75, -0.9), S.TischMitte);
        Nah(S.ModellMitte(new SitzungLage(50, 0, 0.1, -0.05)).Z, -0.95);
    }

    // ------------------------------------------------------------------------------------------------ Griffe

    [Fact]
    public void Griffe_an_den_vorderen_Gebaeudeecken_GRIFF_ABSTAND_schraeg_ausserhalb_folgen_Lage_Drehung_und_Massstab()
    {
        // Brillentest Christian 23.10.2026: «die beiden greiffpunkte sollten an einer gebäudeecke sein.»
        var G = new ModellGroesse(12, 8, 9);
        var l = S.Einpassen(G).Lage;   // 1:50, Mitte der Platte
        var m = S.ModellMitte(l);
        var p = S.GriffPunkte(l, G);
        Assert.Equal(2, p.Count);
        Assert.Equal("links", p[0].Seite);
        Assert.Equal("rechts", p[1].Seite);
        var a = S.GriffAbstandM / Math.Sqrt(2);
        var halb = 12.0 / 50 / 2 + a;
        var vorn = 8.0 / 50 / 2 + a;
        Nah(p[0].X, m.X - halb);
        Nah(p[1].X, m.X + halb);
        Nah(p[0].Z, m.Z + vorn);   // vordere Ecke (zum Benutzer, +z)
        Nah(p[1].Z, m.Z + vorn);
        Nah(p[0].Y, S.TischHoehe + S.GriffHoeheM);
        Nah(p[1].Y, S.TischHoehe + S.GriffHoeheM);
        Assert.Equal(0.06, S.GriffAbstandM);
        Assert.Equal(0.05, S.GriffHoeheM);
        Assert.Equal(0.05, S.GriffFangM);
        Assert.Equal(0.05, S.GriffHoverM);
        Assert.Equal(0.025, S.GriffDurchmesserM);
        // Zoom auf 1:25: die Modellbox verdoppelt sich, die Griffe rücken mit (der Abstand schräg nach aussen bleibt)
        p = S.GriffPunkte(l with { Massstab = 25 }, G);
        Nah(p[1].X - S.ModellMitte(l).X, 12.0 / 25 / 2 + a);
        Nah(p[1].Z - S.ModellMitte(l).Z, 8.0 / 25 / 2 + a);
        // Verschiebung: die Griffe folgen der Modellmitte
        p = S.GriffPunkte(l with { X = 0.1, Z = -0.05 }, G);
        Nah((p[0].X + p[1].X) / 2, m.X + 0.1);
        Nah((p[0].Z + p[1].Z) / 2, m.Z - 0.05 + vorn);
        // Drehung um 90° (gegen den Uhrzeigersinn von oben): die Ecken drehen mit (DreheXZ); die Höhe bleibt
        p = S.GriffPunkte(l with { Drehung = 90 * Grad }, G);
        var r = S.DreheXZ(new Xz(halb, vorn), 90 * Grad);
        Nah(p[1].X, m.X + r.X, 1e-9);
        Nah(p[1].Z, m.Z + r.Z, 1e-9);
        var li = S.DreheXZ(new Xz(-halb, vorn), 90 * Grad);
        Nah(p[0].X, m.X + li.X, 1e-9);
        Nah(p[0].Z, m.Z + li.Z, 1e-9);
        Nah(p[1].Y, S.TischHoehe + S.GriffHoeheM);
        // winziges oder unbekanntes Modell: Mindestabstand, keine NaN
        foreach (var u in new ModellGroesse?[] { null, new(0, 0), new(double.NaN, 0) })
        {
            var q = S.GriffPunkte(l, u);
            Assert.True(q.All(x => double.IsFinite(x.X) && double.IsFinite(x.Z)), "endlich");
            Nah(q[1].X - m.X, S.GriffMinHalbM + a);
            Nah(q[1].Z - m.Z, a);
        }
    }

    [Fact]
    public void Zwei_Haende_verschieben_das_Modell_auch_in_der_Hoehe_begrenzt_Griffe_folgen()
    {
        // Brillentest Christian 23.10.2026: «durch greiffen der punkte soll das modell auch gedreht, in der höhe und tiefe verschoben werden können.»
        var G = new ModellGroesse(12, 8, 9);
        var l0 = S.Einpassen(G).Lage;
        Assert.Equal(0, l0.Hoehe);
        Xz a = new(-0.2, -0.8), b = new(0.2, -0.8);
        var griff = S.ZweihandStart(l0, a, b, 0.80, 0.80);
        // beide Hände 0.2 m höher und 0.1 m nach hinten (Tiefe): Modell 0.2 m höher, Z −0.1
        var l1 = S.ZweihandSchritt(griff, new Xz(a.X, a.Z - 0.1), new Xz(b.X, b.Z - 0.1), 1.00, 1.00);
        Nah(l1.Hoehe, 0.2, 1e-9);
        Nah(l1.Z, l0.Z - 0.1, 1e-9);
        Nah(l1.Massstab, l0.Massstab, 1e-9);
        // Griffe und Modell folgen der Höhe
        Nah(S.GriffPunkte(l1, G)[0].Y, S.TischHoehe + 0.2 + S.GriffHoeheM, 1e-9);
        var bezug = new Hlks.Xr.Sitzung.SitzungBezug(new Xz(0, 0), 0, G);
        Nah(Hlks.Xr.Sitzung.SitzungRaum.ModellZuRaum(V(0, 0, 0), l1, bezug).Y, S.TischHoehe + 0.2, 1e-9);
        Nah(System.Numerics.Vector3.Transform(System.Numerics.Vector3.Zero, Hlks.Xr.Sitzung.SitzungRaum.ModellMatrix(l1, bezug)).Y, S.TischHoehe + 0.2, 1e-6);
        // Begrenzung: nicht über HoeheMaxM, nicht unter HoeheMinM
        Nah(S.ZweihandSchritt(griff, a, b, 3, 3).Hoehe, S.HoeheMaxM, 1e-9);
        Nah(S.ZweihandSchritt(griff, a, b, -2, -2).Hoehe, S.HoeheMinM, 1e-9);
        // Gestenerkennung reicht die Höhe der Hände durch
        var gp = S.GriffPunkte(l0, G);
        var g = S.GestenNeu(l0);
        g = S.GestenSchritt(g, new GestenHaende(new GestenHand(true, 0, gp[0].X, gp[0].Z, 0.8), new GestenHand(true, 1, gp[1].X, gp[1].Z, 0.8)));
        g = S.GestenSchritt(g, new GestenHaende(new GestenHand(true, 0, gp[0].X, gp[0].Z, 0.9), new GestenHand(true, 1, gp[1].X, gp[1].Z, 0.9)));
        Nah(g.Lage.Hoehe, 0.1, 1e-9);
    }

    [Fact]
    public void GriffTreffer_ein_Pinch_in_hoechstens_5_cm_fasst_den_naechsten_Griff_weiter_weg_nichts_Controller_zielen_mit_dem_Strahl()
    {
        var G = new ModellGroesse(12, 8, 9);
        var gp = S.GriffPunkte(S.Einpassen(G).Lage, G);
        Vec3 Bei(GriffPunkt g, double dx, double dy = 0, double dz = 0) => new(g.X + dx, g.Y + dy, g.Z + dz);
        Assert.Equal(0, S.GriffTreffer(Bei(gp[0], 0.04), null, gp));
        Assert.Equal(1, S.GriffTreffer(Bei(gp[1], 0, 0.03, 0.03), null, gp));   // 3D-Abstand √(0.03²+0.03²) = 4.2 cm
        Assert.Null(S.GriffTreffer(Bei(gp[0], 0.051), null, gp));   // knapp ausserhalb
        Assert.Null(S.GriffTreffer(Bei(gp[0], 0, 0.06), null, gp));   // zu hoch
        Assert.Null(S.GriffTreffer(Bei(gp[0], -0.3), null, gp));   // weit weg
        Assert.Null(S.GriffTreffer(null, null, gp));
        Assert.Null(S.GriffTreffer(new Vec3((gp[0].X + gp[1].X) / 2, gp[0].Y, gp[0].Z), null, gp));   // in der Mitte des Modells: weder noch
        // Controller: Strahl von 0.5 m entfernt, auf den rechten Griff gerichtet
        var u = Bei(gp[1], 0, 0.3, 0.5);
        var r = new Vec3(gp[1].X - u.X, gp[1].Y - u.Y, gp[1].Z - u.Z);
        Assert.Equal(1, S.GriffTreffer(u, new Strahl3(u, r), gp));   // Zielstrahl trifft
        Assert.Null(S.GriffTreffer(u, null, gp));   // ohne Strahl ist die Hand zu weit weg
        Assert.Null(S.GriffTreffer(u, new Strahl3(u, -r), gp));   // Strahl zeigt weg
        Nah(S.StrahlAbstand(Vec3.Null, new Vec3(0, 0, -1), new Vec3(0.03, 0, -2)), 0.03);
        Nah(S.StrahlAbstand(Vec3.Null, new Vec3(0, 0, -1), new Vec3(0, 0, 1)), 1, 1e-9, "hinter dem Ursprung zählt der Abstand zum Ursprung");
        Nah(S.StrahlAbstand(new Vec3(1, 1, 1), Vec3.Null, new Vec3(1, 1, 3)), 2, 1e-9, "ohne Richtung der Abstand zum Ursprung");
    }

    // ------------------------------------------------------------------------------------------------ Einhand: starr an der Hand

    [Fact]
    public void Eine_Hand_das_Modell_folgt_der_Hand_Verschieben_und_dreht_sich_mit_ihr_um_die_Senkrechte()
    {
        var l0 = new SitzungLage(50, 0, 0, 0);
        var mitte = S.ModellMitte(l0);
        var start = S.EinhandStart(l0, new HandXz(mitte.X + 0.05, mitte.Z, 0));
        // reine Verschiebung: 10 cm nach rechts, 5 cm nach vorn
        var l = S.EinhandSchritt(start, new HandXz(mitte.X + 0.15, mitte.Z - 0.05, 0));
        Nah(l.X, 0.10);
        Nah(l.Z, -0.05);
        Nah(l.Drehung, 0);
        Assert.Equal(50, l.Massstab);
        // Handdrehung um 90° (gegen den Uhrzeigersinn): das Modell dreht um 90°, die Hand bleibt am selben Punkt des Modells
        var h = new HandXz(mitte.X + 0.05, mitte.Z, 90 * Grad);
        l = S.EinhandSchritt(start, h);
        Nah(l.Drehung, 90 * Grad);
        Nah(S.ModellMitte(l).X - h.X, 0, 1e-9);
        Nah(S.ModellMitte(l).Z - h.Z, 0.05, 1e-9);
        // ohne Richtung der Hand bleibt die Drehung, es wird nur verschoben
        l = S.EinhandSchritt(S.EinhandStart(l0, new HandXz(0, -0.9, null)), new HandXz(0.1, -0.9, 0.5));
        Nah(l.Drehung, 0);
        Nah(l.X, 0.1);
        // das Modell kann nicht vom Tisch geschoben werden
        l = S.EinhandSchritt(start, new HandXz(5, 5, 0));
        Nah(l.X, S.TischBreite / 2 - S.TischHalt);
        Nah(l.Z, S.TischTiefe / 2 - S.TischHalt);
    }

    [Fact]
    public void Winkel_Drehung_ueber_plus_minus_180_Grad_ist_der_kuerzeste_Weg()
    {
        Nah(S.WinkelDiff(-170 * Grad, 170 * Grad), 20 * Grad, 1e-9);
        Nah(S.WinkelDiff(170 * Grad, -170 * Grad), -20 * Grad, 1e-9);
        var l0 = new SitzungLage(50, 0, 0, 0);
        var m = S.ModellMitte(l0);
        var g = S.EinhandStart(l0, new HandXz(m.X, m.Z, 170 * Grad));
        Nah(S.EinhandSchritt(g, new HandXz(m.X, m.Z, -170 * Grad)).Drehung, 20 * Grad, 1e-9);
    }

    // ------------------------------------------------------------------------------------------------ Zwei Hände: Zoom um den Mittelpunkt

    [Fact]
    public void Zwei_Haende_der_Abstand_zoomt_um_den_Mittelpunkt_die_Stelle_unter_dem_Mittelpunkt_bleibt_dort()
    {
        var l0 = new SitzungLage(50, 0, 0.1, 0.05);
        Xz A = new(-0.3, -0.9), B = new(0.1, -0.9);   // Abstand 0.4 m, Mitte (−0.1, −0.9)
        var g = S.ZweihandStart(l0, A, B);
        Xz Q(SitzungLage l, Xz p)
        {
            var m = S.ModellMitte(l);
            var d = S.DreheXZ(new Xz(p.X - m.X, p.Z - m.Z), -l.Drehung);
            return new Xz(d.X * l.Massstab, d.Z * l.Massstab);
        }
        var q0 = Q(l0, new Xz(-0.1, -0.9));
        // Hände doppelt so weit auseinander → Massstab halbiert (1:25), Mittelpunkt zeigt auf dieselbe Stelle des Modells
        var l = S.ZweihandSchritt(g, new Xz(-0.5, -0.9), new Xz(0.3, -0.9));
        Nah(l.Massstab, 25, 1e-9);
        var q1 = Q(l, new Xz(-0.1, -0.9));
        Nah(q1.X, q0.X, 1e-6);
        Nah(q1.Z, q0.Z, 1e-6);
        // zusammenschieben → kleiner (1:100)
        Nah(S.ZweihandSchritt(g, new Xz(-0.2, -0.9), new Xz(0, -0.9)).Massstab, 100, 1e-9);
        // Grenzen
        Assert.Equal(20, S.ZweihandSchritt(g, new Xz(-2, -0.9), new Xz(2, -0.9)).Massstab);   // mindestens 1:20
        Assert.Equal(200, S.ZweihandSchritt(g, new Xz(-0.101, -0.9), new Xz(-0.099, -0.9)).Massstab);   // höchstens 1:200
        // Mittelpunkt wandert → das Modell wandert mit (ohne Zoom, ohne Drehung)
        var v = S.ZweihandSchritt(g, new Xz(-0.2, -0.8), new Xz(0.2, -0.8));
        Nah(v.Massstab, 50, 1e-9);
        Nah(v.X, l0.X + 0.1, 1e-9, "x");
        Nah(v.Z, l0.Z + 0.1, 1e-9, "z");
    }

    [Fact]
    public void Zwei_Haende_die_Drehung_der_Verbindungslinie_dreht_das_Modell_um_den_Mittelpunkt()
    {
        var l0 = new SitzungLage(50, 0, 0, 0);
        var mitte = new Xz(0, -0.9);
        Xz A = new(mitte.X - 0.2, mitte.Z), B = new(mitte.X + 0.2, mitte.Z);
        var g = S.ZweihandStart(l0, A, B);
        // Linie um 90° gegen den Uhrzeigersinn gedreht (von +x nach −z): B wandert nach (0, −1.1)
        var l = S.ZweihandSchritt(g, new Xz(mitte.X, mitte.Z + 0.2), new Xz(mitte.X, mitte.Z - 0.2));
        Nah(l.Drehung, 90 * Grad, 1e-9);
        Nah(l.Massstab, 50, 1e-9);
        // Die Mitte des Modells lag unter dem Mittelpunkt der Hände und bleibt dort
        Nah(l.X, 0, 1e-9);
        Nah(l.Z, 0, 1e-9);
        // Drehen und Zoomen zugleich
        var l2 = S.ZweihandSchritt(g, new Xz(mitte.X, mitte.Z + 0.4), new Xz(mitte.X, mitte.Z - 0.4));
        Nah(l2.Drehung, 90 * Grad, 1e-9);
        Nah(l2.Massstab, 25, 1e-9);
    }

    // ------------------------------------------------------------------------------------------------ Gestenablauf

    [Fact]
    public void Gesten_ein_Pinch_am_Griff_haelt_ihn_ein_Pinch_weit_weg_nicht_bewegt_wird_nur_mit_beiden_Griffen_der_Wechsel_springt_nicht()
    {
        var G = new ModellGroesse(12, 8, 9);
        var l0 = S.Einpassen(G).Lage;
        var gp = S.GriffPunkte(l0, G);
        var g = S.GestenNeu(l0);
        GestenHand A(double dx = 0) => new(true, 0, gp[0].X + dx, gp[0].Z);
        GestenHand B(double dx = 0) => new(true, 1, gp[1].X + dx, gp[1].Z);
        // kein Pinch: Ruhe, auch nah am Griff
        g = S.GestenSchritt(g, new GestenHaende(A() with { Griff = false }, null));
        Assert.Null(g.Modus);
        Assert.Equal(new GriffBesitz(null, null), g.Besitzt);
        // Pinch am linken Griff: gehalten, aber das Modell ruht (ein Griff allein)
        g = S.GestenSchritt(g, new GestenHaende(A(), null));
        Assert.Equal("wartet", g.Modus);
        Assert.Equal(0, g.Besitzt.Links);
        var vor = g.Lage;
        g = S.GestenSchritt(g, new GestenHaende(A(0.1), null));
        Assert.Equal(vor, g.Lage);   // ein Griff allein bewegt nichts
        // die andere Hand pincht weit weg vom Griff (nah = null): zählt nicht
        g = S.GestenSchritt(g, new GestenHaende(A(0.1), new GestenHand(true, null, gp[1].X + 1, gp[1].Z)));
        Assert.Equal("wartet", g.Modus);
        Assert.Null(g.Besitzt.Rechts);
        Assert.Equal(vor, g.Lage);
        // sie pinchte weit weg schon: ein späteres Hineinwandern in die Fangreichweite fasst nicht (nur die steigende Flanke zählt)
        g = S.GestenSchritt(g, new GestenHaende(A(0.1), B()));
        Assert.Equal("wartet", g.Modus);   // Pinch war schon gesetzt, bevor die Hand am Griff war
        // loslassen und am Griff neu pinchen: nun halten beide
        g = S.GestenSchritt(g, new GestenHaende(A(0.1), B() with { Griff = false }));
        g = S.GestenSchritt(g, new GestenHaende(A(0.1), B()));
        Assert.Equal("zwei", g.Modus);
        Assert.Equal(new GriffBesitz(0, 1), g.Besitzt);
        var eins = g.Lage;
        g = S.GestenSchritt(g, new GestenHaende(A(0.1), B()));
        Nah(g.Lage.Massstab, eins.Massstab, 1e-9, "beim Wechsel springt massstab nicht");
        Nah(g.Lage.Drehung, eins.Drehung, 1e-9, "beim Wechsel springt drehung nicht");
        Nah(g.Lage.X, eins.X, 1e-9, "beim Wechsel springt x nicht");
        Nah(g.Lage.Z, eins.Z, 1e-9, "beim Wechsel springt z nicht");
        // Hände auseinander: doppelter Abstand → 1:25
        var d0 = gp[1].X - (gp[0].X + 0.1);
        g = S.GestenSchritt(g, new GestenHaende(A() with { X = gp[0].X + 0.1 - d0 / 2 }, B() with { X = gp[1].X + d0 / 2 }));
        Nah(g.Lage.Massstab, l0.Massstab / 2, 1e-6, "Abstand verdoppelt → Massstab halbiert");
        // eine Hand lässt los: das Modell bleibt stehen, die andere bewegt sich ohne Wirkung
        var zoomLage = g.Lage;
        g = S.GestenSchritt(g, new GestenHaende(A() with { Griff = false }, B(0.6)));
        Assert.Equal("wartet", g.Modus);
        Assert.Equal(zoomLage, g.Lage);
        g = S.GestenSchritt(g, new GestenHaende(null, null));
        Assert.Null(g.Modus);
        Assert.Equal(zoomLage, g.Lage);
        Assert.Equal(new GriffBesitz(null, null), g.Besitzt);
    }

    [Fact]
    public void Gesten_verschieben_und_drehen_mit_beiden_Griffen_die_Haende_duerfen_die_Griffe_vertauschen_rechte_Hand_am_linken_Griff()
    {
        var G = new ModellGroesse(12, 8, 9);
        var l0 = S.Einpassen(G).Lage;
        var gp = S.GriffPunkte(l0, G);
        var g = S.GestenNeu(l0);
        // rechte Hand am linken Griff (0), linke Hand am rechten Griff (1)
        GestenHand R(double dx = 0, double dz = 0) => new(true, 0, gp[0].X + dx, gp[0].Z + dz);
        GestenHand L(double dx = 0, double dz = 0) => new(true, 1, gp[1].X + dx, gp[1].Z + dz);
        g = S.GestenSchritt(g, new GestenHaende(L() with { Griff = false }, R() with { Griff = false }));
        g = S.GestenSchritt(g, new GestenHaende(L(), R()));
        Assert.Equal("zwei", g.Modus);
        Assert.Equal(new GriffBesitz(1, 0), g.Besitzt);
        // beide 10 cm nach vorn (−z) und 5 cm nach rechts: das Modell wandert mit
        g = S.GestenSchritt(g, new GestenHaende(L(0.05, -0.1), R(0.05, -0.1)));
        Nah(g.Lage.X, 0.05, 1e-9);
        Nah(g.Lage.Z, -0.1, 1e-9);
        Nah(g.Lage.Drehung, 0, 1e-9);
        Nah(g.Lage.Massstab, l0.Massstab, 1e-9);
        // Drehung: die Verbindungslinie (Griff 0 → Griff 1) um 90° gegen den Uhrzeigersinn gedreht → das Modell dreht um 90°
        Xz m = new((gp[0].X + gp[1].X) / 2, (gp[0].Z + gp[1].Z) / 2);
        var halb = Math.Abs(gp[1].X - gp[0].X) / 2;
        g = S.GestenSchritt(g, new GestenHaende(L() with { X = m.X, Z = m.Z - halb }, R() with { X = m.X, Z = m.Z + halb }));
        Nah(g.Lage.Drehung, 90 * Grad, 1e-9);
    }

    [Fact]
    public void Gesten_derselbe_Griff_wird_nicht_von_zwei_Haenden_gehalten()
    {
        var l0 = S.Einpassen(new ModellGroesse(12, 8, 9)).Lage;
        var g = S.GestenNeu(l0);
        var beide = new GestenHaende(new GestenHand(true, 0, 0, -1), new GestenHand(true, 0, 0.01, -1));
        g = S.GestenSchritt(g, beide);
        Assert.Equal(new GriffBesitz(0, null), g.Besitzt);   // die linke Hand hält ihn zuerst
        Assert.Equal("wartet", g.Modus);
    }

    [Fact]
    public void Richtung_der_Hand_aus_Handgelenk_und_Mittelfinger_ersatzweise_aus_dem_Zielstrahl()
    {
        var g = new Dictionary<string, Vec3> { ["wrist"] = new(0, 1, 0), ["middle-finger-phalanx-proximal"] = new(0, 1, -0.09) };
        Nah(S.HandGieren(new HandInfo(g))!.Value, 90 * Grad, 1e-9);   // nach −z = Gieren 90°
        Nah(S.HandGieren(new HandInfo(new Dictionary<string, Vec3> { ["wrist"] = g["wrist"], ["middle-finger-phalanx-proximal"] = new(0.09, 1, 0) }))!.Value, 0, 1e-9);
        Nah(S.WinkelDiff(S.HandGieren(new HandInfo(null, new Vec3(-1, 0, 0)))!.Value, Math.PI), 0, 1e-9);   // ±180° sind dieselbe Richtung
        Assert.Null(S.HandGieren(new HandInfo(null, new Vec3(0, -1, 0.01))));   // senkrecht: keine Richtung
        Assert.Null(S.HandGieren(null));
    }

    // ------------------------------------------------------------------------------------------------ Geschosse und Schnitthöhe

    static readonly SitzungGeschossRoh[] Geschosse =
    [
        new("2.OG", "2. Obergeschoss", 5800, 2900),
        new("EG", "Erdgeschoss", 0, 2900),
        new("1.OG", "1. Obergeschoss", 2900, 2900),
        new("UG", "Untergeschoss", -2900, 2900),
        new("XX", "ohne Höhe", 9000, null),
    ];

    [Fact]
    public void Geschosse_sortiert_nach_Kote_ohne_Hoehe_fehlen_Oberkante_der_Decke_ist_Kote_des_Geschosses_darueber_sonst_Kote_plus_Hoehe()
    {
        var g = S.GeschosseAufbereiten(Geschosse, []);
        Gleich(["UG", "EG", "1.OG", "2.OG"], g.Select(x => x.Kennzeichen).ToArray());
        Gleich([0.0, 2900, 5800, 8700], g.Select(x => x.DeckeOK).ToArray());
        Assert.True(g.All(x => x.OhneDecke && x.DeckeUK == x.DeckeOK), "ohne Decken im Modell gilt die Oberkante");
        // liegt das Geschoss darüber höher als Kote + Höhe (Decke dazwischen), gilt weiter die Kote darüber
        var g2 = S.GeschosseAufbereiten([new("A", null, 0, 2700), new("B", null, 3000, 2700)], []);
        Assert.Equal(3000, g2[0].DeckeOK);
        Assert.Equal(5700, g2[1].DeckeOK);
        // zwei Gebäude: das Geschoss darüber gehört zum selben Gebäude
        var g3 = S.GeschosseAufbereiten([new("A-EG", null, 0, 2800, "A"), new("B-1", null, 1000, 2800, "B")], []);
        Assert.Equal(2800, g3[0].DeckeOK);
        Assert.Empty(S.GeschosseAufbereiten(null, null));
    }

    [Fact]
    public void Schnitthoehe_unter_der_Decke_gleich_Unterkante_minus_SCHNITT_TIEFER_mit_Decke_zeigen_Oberkante_ohne_Decke_Oberkante_minus_SCHNITT_TIEFER()
    {
        var decken = new DeckeRoh[] { new(2900, 200), new(2901, 250), new(5800, 180), new(8700, 0), new(4000, 300) };
        var g = S.GeschosseAufbereiten(Geschosse, decken);
        SitzungGeschoss Nach(string kz) => g.First(x => x.Kennzeichen == kz);
        SitzungGeschoss eg = Nach("EG"), og1 = Nach("1.OG"), og2 = Nach("2.OG"), ug = Nach("UG");
        Assert.Equal(250, eg.Dicke);   // die dickere der beiden Platten auf der Kote (±2 mm) zählt
        Assert.Equal(50, S.SchnittTieferMm);
        Assert.Equal(2600, S.SchnittHoeheMm(eg, false));
        Assert.Equal(2900, S.SchnittHoeheMm(eg, true));
        Assert.Equal(5570, S.SchnittHoeheMm(og1, false));
        Assert.Equal(5800, S.SchnittHoeheMm(og1, true));
        Assert.True(og2.OhneDecke, "Dicke 0 zählt nicht");
        Assert.Equal(8650, S.SchnittHoeheMm(og2, false));
        Assert.True(ug.OhneDecke);
        Assert.Equal(-50, S.SchnittHoeheMm(ug, false));
    }

    [Fact]
    public void Clipping_Ebene_im_Raum_Plattenoberkante_plus_Schnitt_minus_Unterkante_des_Modells_durch_Massstab_Spiel_nach_der_Seite_der_Flaeche()
    {
        const double unten = -2.9;   // Unterkante des Modells (m), UG-Boden
        Nah(S.SchnittEbeneY(2650, false, unten, 50), S.TischHoehe + ((2650 - S.SpielMm) / 1000 + 2.9) / 50, 1e-12);
        Nah(S.SchnittEbeneY(2900, true, unten, 50), S.TischHoehe + ((2900 + S.SpielMm) / 1000 + 2.9) / 50, 1e-12);
        // beim Zoomen wandert die Ebene mit dem Modell: bei 1:25 doppelt so hoch über der Platte
        var a = S.SchnittEbeneY(2650, false, unten, 50) - S.TischHoehe;
        var b = S.SchnittEbeneY(2650, false, unten, 25) - S.TischHoehe;
        Nah(b / a, 2, 1e-9);
        // Unterkante des Modells auf der Platte: Schnitt in Höhe der Unterkante liegt auf der Platte
        Nah(S.SchnittEbeneY(0, false, 0, 50) - S.TischHoehe, -S.SpielMm / 1000 / 50, 1e-12);
    }

    [Fact]
    public void Alle_Etagen_und_Zuruecksetzen_Kopfzeilen_Umschalter_Startlage()
    {
        var g = S.GeschosseAufbereiten(Geschosse, [new(2900, 200)]);
        var lage = new SitzungLage(50, 0, 0, 0);
        Gleich(["Sitzungsraum · Massstab 1:50", "Alle Etagen"], S.KopfZeilen(lage, new SitzungWahl(null, false), g).ToArray());
        var z = S.KopfZeilen(lage with { Massstab = 25 }, new SitzungWahl("EG", false), g);
        Assert.Equal("Sitzungsraum · Massstab 1:25", z[0]);
        Assert.StartsWith("EG · Schnitt unter der Decke (2.65 m)", z[1]);
        Assert.Contains("über der Decke (2.90 m)", S.KopfZeilen(lage, new SitzungWahl("EG", true), g)[1]);
        Assert.Contains(S.KopfZeilen(lage, new SitzungWahl("1.OG", false), g), t => t.StartsWith("Keine Decke im Modell"));
        Assert.Equal(new Dictionary<string, bool> { ["alle"] = true, ["decke"] = false, ["ifc"] = false }, S.SitzungsTasten(new SitzungWahl(null, false), false));
        Assert.Equal(new Dictionary<string, bool> { ["alle"] = false, ["decke"] = true, ["ifc"] = true, ["g:EG"] = true }, S.SitzungsTasten(new SitzungWahl("EG", true), true));
        // Zurücksetzen: Startlage neu eingepasst, auch nach Zoom und Drehung
        var gross = new ModellGroesse(100, 30, 20);
        Assert.Equal(S.Einpassen(gross), S.Zuruecksetzen(gross));
        Assert.Equal(50, S.Zuruecksetzen(new ModellGroesse(12, 8, 9)).Lage.Massstab);
    }

    // ------------------------------------------------------------------------------------------------ Tablet-Seite

    [Fact]
    public void Tablet_Seite_Alle_Etagen_und_die_Geschosse_oberstes_zuerst_feste_Tasten_jede_Taste_trifft_sich_selbst_nichts_ueberlappt()
    {
        var g = S.GeschosseAufbereiten(Geschosse, []);
        var layout = S.SitzungsLayout(g);
        Gleich(["alle", "g:2.OG", "g:1.OG", "g:EG", "g:UG"], layout.Take(5).Select(t => t.Id).ToArray());
        foreach (var id in new[] { "decke", "zuruecksetzen", "ifc", "raum", "ende" }) Assert.Contains(layout, t => t.Id == id);
        Assert.Equal("Begehen", layout.First(t => t.Id == "raum").Text);
        foreach (var t in layout)
        {
            Assert.Equal(t.Id, Tab.TasteBei((t.X + t.W / 2) / Tab.TabletPxB, (t.Y + t.H / 2) / Tab.TabletPxH, layout)?.Id);
            Assert.True(t.X >= 0 && t.Y >= Tab.TabletKopfH && t.X + t.W <= Tab.TabletPxB && t.Y + t.H <= Tab.TabletPxH, $"{t.Id} im Bild");
            Assert.True(t.H >= 30, $"{t.Id} hoch genug zum Antippen ({t.H} px)");
        }
        for (var i = 0; i < layout.Count; i++)
            for (var j = i + 1; j < layout.Count; j++)
            {
                Taste a = layout[i], b = layout[j];
                Assert.True(a.X + a.W <= b.X + 1e-9 || b.X + b.W <= a.X + 1e-9 || a.Y + a.H <= b.Y + 1e-9 || b.Y + b.H <= a.Y + 1e-9, $"{a.Id} überlappt {b.Id}");
            }
        // viele Geschosse: höchstens MAX_ETAGEN, das Layout bleibt im Bild
        var viele = S.GeschosseAufbereiten(Enumerable.Range(0, 14).Select(i => new SitzungGeschossRoh($"G{i}", null, i * 3000, 3000)), []);
        var l2 = S.SitzungsLayout(viele);
        Assert.Equal(S.MaxEtagen, l2.Count(t => t.Id.StartsWith("g:")));
        Assert.True(l2.All(t => t.Y + t.H <= Tab.TabletPxH && t.H >= 30), "alle Tasten im Bild und gross genug");
        // ohne Geschosse nur «Alle Etagen»
        Gleich(["alle"], S.SitzungsLayout([]).Take(1).Select(t => t.Id).ToArray());
        // Beschriftung kurz
        Assert.Equal("EG", S.GeschossText("EG"));
        Assert.True(S.GeschossText("GESCHOSS-WEIT-LANG", "Dachgeschoss mit Galerie").Length <= S.GeschossTextMax);
        Assert.Equal("Dachgeschoss mit Galerie", S.GeschossText("GESCHOSS-WEIT-LANG", "Dachgeschoss mit Galerie"));
        Assert.Equal(S.GeschossTextMax, S.GeschossText("GESCHOSS-LANG", new string('n', 40)).Length);
        Assert.EndsWith("…", S.GeschossText("GESCHOSS-LANG", new string('n', 40)));
    }

    [Fact]
    public void Tablet_Seite_im_Hochformat_Etagen_untereinander_in_voller_Breite_darunter_zwei_Spalten_VR_beenden_ganz_unten_Texte_ungekuerzt()
    {
        var g = S.GeschosseAufbereiten(Geschosse, []);
        var layout = S.SitzungsLayout(g);
        var etagen = layout.Where(t => t.Id == "alle" || t.Id.StartsWith("g:")).ToList();
        Assert.Equal(5, etagen.Count);
        Assert.True(etagen.All(t => t.X == etagen[0].X && t.W == etagen[0].W && t.W > Tab.TabletPxB * 0.9), "volle Breite, untereinander");
        Assert.True(etagen.Select((t, i) => i == 0 || t.Y > etagen[i - 1].Y).All(x => x), "eine Zeile je Etage");
        var unten = new[] { "decke", "zuruecksetzen", "ifc", "raum" }.Select(id => layout.First(t => t.Id == id)).ToArray();
        Assert.Equal(unten[0].Y, unten[1].Y);
        Assert.Equal(unten[2].Y, unten[3].Y);
        Assert.True(unten[2].Y > unten[0].Y);
        Assert.True(unten[0].X < unten[1].X);
        Assert.True(unten[0].Y > etagen[^1].Y, "die Tasten stehen unter der Etagenliste");
        Assert.True(layout.First(t => t.Id == "ende").Y > unten[2].Y);
        // 15 Zoll hochkant
        Assert.True(Tab.TabletH > Tab.TabletB && Tab.TabletPxH > Tab.TabletPxB, "Hochformat");
        // Begehen-Seite: zwei Spalten, alle zwölf Tasten, keine Beschriftung gekürzt
        var b = Tab.TabletLayout();
        Assert.Equal(13, b.Count);
        Assert.Equal(2, Tab.TabletSpalten);
        Assert.True(b.Where(t => t.Id != "ende").All(t => t.X < Tab.TabletPxB / 2 ? t.W < Tab.TabletPxB / 2 : t.X > Tab.TabletPxB / 2 - 1), "zwei Spalten");
        Assert.True(b.All(t => t.H >= 40 && t.Y + t.H <= Tab.TabletPxH && t.X >= 0 && t.X + t.W <= Tab.TabletPxB), "gross genug und im Bild");
        foreach (var t in b) Assert.Equal(t.Id, Tab.TasteBei((t.X + t.W / 2) / Tab.TabletPxB, (t.Y + t.H / 2) / Tab.TabletPxH, b)?.Id);
        foreach (var t in Tab.TabletTasten) Assert.True(t.Text.Length > 0 && t.Text.Length <= 20, t.Text);
    }

    // ------------------------------------------------------------------------------------------------ Ausschnitt («Raum zeigen (VR)», 3D-Ansicht in VR)

    static readonly SchnittbereichRoh Box = new(true, [0, 0, 0], [10000, 8000, 3000], 0);

    [Fact]
    public void Ausschnitt_Groesse_Mitte_und_Unterkante_aus_dem_Quader_Koordinaten_der_3D_Ansicht_ungueltig_oder_ausgeschaltet_gibt_null()
    {
        var b = S.AusschnittBezugAus(Box)!;
        Nah(b.Groesse.B, 10);
        Nah(b.Groesse.T, 8);
        Nah(b.Groesse.H, 3);
        Nah(b.Anker.X, 5);
        Nah(b.Anker.Z, -4);
        Nah(b.Unten, 0);
        var hoch = S.AusschnittBezugAus(Box with { Min = [1000, 2000, 2900], Max = [3000, 6000, 5800] })!;
        Nah(hoch.Anker.X, 2);
        Nah(hoch.Anker.Z, -4);
        Nah(hoch.Unten, 2.9);
        Nah(hoch.Groesse.H, 2.9);
        Assert.Null(S.AusschnittBezugAus(Box with { Aktiv = false }));
        Assert.Null(S.AusschnittBezugAus(null));
        Assert.Null(S.AusschnittBezugAus(new SchnittbereichRoh(null, [0, 0, 0], [0.5, 1, 1])));   // kleiner als 1 mm
        Assert.Null(S.AusschnittBezugAus(new SchnittbereichRoh(true, [0, 0], [1, 1])));
        Assert.NotNull(S.AusschnittBezugAus(new SchnittbereichRoh(null, [0, 0, 0], [10, 10, 10])));   // «aktiv» fehlt: gilt
        // gedrehter Quader: die Achsenbox ist grösser als die Kantenlängen
        var gedreht = S.AusschnittBezugAus(Box with { Winkel = 30 })!;
        Assert.True(gedreht.Groesse.B > 10 && gedreht.Groesse.T > 8);
    }

    [Fact]
    public void Ausschnitt_der_Massstab_passt_den_Ausschnitt_auf_die_Platte_so_fein_wie_moeglich_1_20_groeber_nur_wenn_noetig_hoechstens_1_200()
    {
        Assert.Equal(20, S.AusschnittLage(new ModellGroesse(10, 8, 3)).Lage.Massstab);   // Raum plus 1.5 m: 1:20 genügt
        Assert.Equal(38, S.AusschnittLage(new ModellGroesse(40, 30, 9)).Lage.Massstab);   // gross: nächster ganzer Massstab, der noch auf die Platte (1.4 × 0.8 m) passt
        Assert.True(40.0 / 38 <= 1.4 + 1e-9 && 30.0 / 38 <= 0.8 + 1e-9);
        Assert.Equal(200, S.AusschnittLage(new ModellGroesse(400, 300, 9)).Lage.Massstab);
        Assert.False(S.AusschnittLage(new ModellGroesse(400, 300, 9)).Passt);
        Assert.Equal(20, S.AusschnittLage(new ModellGroesse(1, 1, 1)).Lage.Massstab);   // ein kleiner Ausschnitt wird nicht über 1:20 hinaus vergrössert
        Assert.Equal(50, S.Einpassen(new ModellGroesse(10, 8, 3)).Lage.Massstab);   // zum Vergleich: das ganze Modell beginnt bei 1:50
        var z = S.AusschnittLage(new ModellGroesse(10, 8, 3)).Lage;
        Assert.True(z.X == 0 && z.Z == 0 && z.Drehung == 0, "Mitte der Platte, nicht gedreht");
    }

    [Fact]
    public void Ausschnitt_sechs_Ebenen_die_genau_den_Quader_sichtbar_lassen()
    {
        var e = S.AusschnittEbenen(Box);
        Assert.Equal(6, e.Count);
        double[] Abstand(Vec3 p) => e.Select(x => x.Normale[0] * p.X + x.Normale[1] * p.Y + x.Normale[2] * p.Z + x.Konstante).ToArray();
        Assert.True(Abstand(new Vec3(5, 1.5, -4)).All(d => d > 0), "Mitte: innen");
        Assert.True(Abstand(new Vec3(0, 0, 0)).All(d => d > -1e-9), "Ecke liegt auf den Ebenen");
        foreach (var aus in new[] { new Vec3(-0.01, 1, -4), new Vec3(10.01, 1, -4), new Vec3(5, -0.01, -4), new Vec3(5, 3.01, -4), new Vec3(5, 1, 0.01), new Vec3(5, 1, -8.01) })
            Assert.True(Abstand(aus).Any(d => d < 0), $"ausserhalb: {aus}");
        // gedrehter Quader (30°): die Ebenen drehen mit
        var r = S.AusschnittEbenen(Box with { Winkel = 30 });
        Assert.Equal(6, r.Count);
        Assert.NotEqual(e[0].Normale, r[0].Normale);
        Assert.Empty(S.AusschnittEbenen(null));
    }

    [Fact]
    public void Ausschnitt_Kopfzeilen_und_Tasten_Alle_Etagen_ist_aus_solange_der_Ausschnitt_gilt()
    {
        var lage = new SitzungLage(20, 0, 0, 0);
        var z = S.KopfZeilen(lage, new SitzungWahl(null, false, "Raum E.01 + 1.5 m"), []);
        Assert.Equal("Sitzungsraum · Massstab 1:20", z[0]);
        Assert.Equal("Ausschnitt: Raum E.01 + 1.5 m", z[1]);
        Assert.Contains("Alle Etagen", z[2]);
        Assert.Equal(3, z.Count);
        Assert.Equal(new Dictionary<string, bool> { ["alle"] = false, ["decke"] = false, ["ifc"] = false }, S.SitzungsTasten(new SitzungWahl(null, false, "x"), false));
        Assert.True(S.SitzungsTasten(new SitzungWahl(null, false, null), false)["alle"]);
    }

    // ------------------------------------------------------------------------------------------------ Schnittbereich (Teil von schnittbereich-logik.js)

    [Fact]
    public void Schnittbereich_Lesen_Achsen_WeltPunkt_und_Ecken()
    {
        var sb = SchnittbereichLogik.Lesen(Box with { Winkel = 90 })!;
        Assert.True(sb.Aktiv);
        var a = SchnittbereichLogik.Achsen(90);
        Nah(a.U[0], 0, 1e-12);
        Nah(a.U[2], -1, 1e-12);   // u zeigt nach −z (Nord)
        Nah(a.V[0], -1, 1e-12);
        var p = SchnittbereichLogik.WeltPunkt(sb, 1000, 0, 500);
        Nah(p.X, 0, 1e-9);
        Nah(p.Y, 0.5, 1e-12);
        Nah(p.Z, -1, 1e-9);
        Assert.Equal(8, SchnittbereichLogik.Ecken(sb).Count);
        var kopie = SchnittbereichLogik.Lesen(Box)!;
        kopie.Min[0] = 99;
        Assert.Equal(0, Box.Min![0]);   // Lesen liefert eine Kopie
        Assert.Null(SchnittbereichLogik.Lesen(new SchnittbereichRoh(true, [0, 0, 0], [double.NaN, 1, 1])));
    }
}
