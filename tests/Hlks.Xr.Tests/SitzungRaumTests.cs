// Tests Quest App P4b (Modul XR, XR-H): Lage des Sitzungsraums im Raum der Brille (virtuell vor dem Kopf, echter Tisch am angetippten Punkt), Modell auf dem Tisch
// (Regel und Matrix, passend zu den Zweihand-Gesten aus XrSitzungLogik) und der Schnittquader (Etagenschnitt, Ausschnitt) gegen SchnittbereichLogik.

using System.Numerics;
using Hlks.Xr.Sitzung;
using static Hlks.Xr.Tests.T;
using S = Hlks.Xr.XrSitzungLogik;

namespace Hlks.Xr.Tests;

public class SitzungRaumTests
{
    static void NahV(Vec3 a, Vec3 b, double eps = 1e-9, string text = "")
    {
        Nah(a.X, b.X, eps, text + " x");
        Nah(a.Y, b.Y, eps, text + " y");
        Nah(a.Z, b.Z, eps, text + " z");
    }

    static Vec3 Mal(Matrix4x4 m, Vec3 p) => Vector3.Transform((Vector3)p, m);

    // ------------------------------------------------------------------------------------------------ Raum vor dem Kopf

    [Fact]
    public void VorKopf_Blick_nach_minus_z_Tisch_steht_0_9_m_vor_dem_Kopf_auf_dem_Boden()
    {
        var r = SitzungRaum.VorKopf(V(1, 1.6, 2), 90 * Grad);   // Gieren 90° = −z
        Nah(r.Drehung, 0);
        NahV(r.ZuRef(S.TischMitte), V(1, 0.75, 2 - 0.9));
        NahV(r.ZuRef(V(0, 0, 0)), V(1, 0, 2), text: "Ursprung auf dem Boden unter dem Kopf");
    }

    [Fact]
    public void VorKopf_Blick_nach_Osten_das_Raum_lokale_minus_z_zeigt_nach_plus_x()
    {
        var r = SitzungRaum.VorKopf(V(0, 1.7, 0), 0);
        NahV(r.ZuRef(V(0, 0, -1)), V(1, 0, 0));
        NahV(r.ZuRef(V(1, 0, 0)), V(0, 0, 1), text: "rechts vom Blick nach Osten ist Süden (+z)");
    }

    [Fact]
    public void ZuRef_und_AusRef_kehren_sich_um_und_die_Matrix_rechnet_dasselbe()
    {
        var r = new SitzungRaumLage(V(2.5, -0.1, -3), 1.234);
        foreach (var p in new[] { V(0, 0, 0), V(1, 2, 3), V(-0.4, 0.75, -0.9), V(7, -1, 0.5) })
        {
            NahV(r.AusRef(r.ZuRef(p)), p);
            NahV(Mal(r.Matrix, p), r.ZuRef(p), 1e-5, "Matrix");
        }
    }

    // ------------------------------------------------------------------------------------------------ echter Tisch

    [Fact]
    public void AufTisch_Mitte_der_Platte_liegt_auf_dem_angetippten_Punkt_und_minus_z_zeigt_vom_Kopf_dorthin()
    {
        var kopf = V(0, 1.2, 0);
        var tipp = V(0.3, 0.72, -0.6);
        var r = SitzungRaum.AufTisch(tipp, kopf, 0);
        NahV(r.ZuRef(S.TischMitte), tipp);
        var vor = r.ZuRef(S.TischMitte + V(0, 0, -1)) - tipp;   // Raum-lokal −z
        var soll = new Vec3(0.3, 0, -0.6).Einheit();
        NahV(vor, soll, 1e-9, "Richtung");
        Nah(r.ZuRef(V(0, 0, 0)).Y, 0.72 - S.TischHoehe, 1e-9, "gedachter Boden");
    }

    [Fact]
    public void AufTisch_zu_nah_am_Kopf_gilt_die_Blickrichtung()
    {
        var r = SitzungRaum.AufTisch(V(0.05, 0.8, -0.05), V(0, 1.2, 0), 0);   // Blick nach +x
        Nah(r.Drehung, -Math.PI / 2);
        NahV(r.ZuRef(S.TischMitte), V(0.05, 0.8, -0.05));
    }

    // ------------------------------------------------------------------------------------------------ Modell auf dem Tisch

    [Fact]
    public void ModellZuRaum_Anker_auf_der_Tischmitte_Unterkante_auf_der_Platte_1_50()
    {
        var bezug = new SitzungBezug(new Xz(10, -4), -2.7, new ModellGroesse(12, 9, 9));
        var lage = new SitzungLage(50, 0, 0, 0);
        NahV(SitzungRaum.ModellZuRaum(V(10, -2.7, -4), lage, bezug), S.TischMitte);
        // 5 m nach Osten und 3 m hoch: 0.1 m nach rechts und 0.06 m über der Platte
        NahV(SitzungRaum.ModellZuRaum(V(15, 0.3, -4), lage, bezug), S.TischMitte + V(0.1, 0.06, 0));
    }

    [Fact]
    public void ModellZuRaum_Drehung_90_Grad_gegen_den_Uhrzeigersinn_Osten_wird_vom_Menschen_weg()
    {
        var bezug = new SitzungBezug(new Xz(0, 0), 0, new ModellGroesse(10, 10, 3));
        var lage = new SitzungLage(50, Math.PI / 2, 0.1, -0.05);
        var p = SitzungRaum.ModellZuRaum(V(5, 0, 0), lage, bezug);
        NahV(p, S.TischMitte + V(0.1, 0, -0.05) + V(0, 0, -0.1));
    }

    [Fact]
    public void ModellMatrix_rechnet_wie_ModellZuRaum_auch_mit_Raumlage()
    {
        var bezug = new SitzungBezug(new Xz(3.2, -7.5), 0.25, new ModellGroesse(20, 14, 12));
        var raum = new SitzungRaumLage(V(-1, 0, 2), 0.7);
        foreach (var lage in new[] { new SitzungLage(50, 0, 0, 0), new SitzungLage(120, -2.1, 0.3, -0.2), new SitzungLage(20, 3, -0.6, 0.4) })
            foreach (var p in new[] { V(3.2, 0.25, -7.5), V(0, 0, 0), V(12, 9, -20), V(-5, 3, 4) })
            {
                NahV(Mal(SitzungRaum.ModellMatrix(lage, bezug), p), SitzungRaum.ModellZuRaum(p, lage, bezug), 1e-5, "ohne Raum");
                NahV(Mal(SitzungRaum.ModellMatrix(lage, bezug, raum), p), raum.ZuRef(SitzungRaum.ModellZuRaum(p, lage, bezug)), 1e-4, "mit Raum");
            }
    }

    [Fact]
    public void Zweihandgeste_der_Punkt_des_Modells_unter_der_Mitte_der_Haende_bleibt_unter_ihr()
    {
        // Die Darstellung (ModellZuRaum) muss zur Regel der Gesten (ZweihandStart/ZweihandSchritt) passen: zoomen und drehen um die Mitte zwischen den Händen.
        var bezug = new SitzungBezug(new Xz(4, -6), 0, new ModellGroesse(14, 10, 6));
        var lage0 = new SitzungLage(50, 0.3, 0.05, -0.02);
        Xz a0 = new(-0.2, -0.85), b0 = new(0.15, -0.95);
        var g = S.ZweihandStart(lage0, a0, b0);
        Xz a1 = new(-0.25, -0.8), b1 = new(0.3, -1.0);
        var lage1 = S.ZweihandSchritt(g, a1, b1);
        // Punkt des Modells unter der Mitte am Anfang (Umkehrung von ModellZuRaum in x, z)
        var m0 = new Xz((a0.X + b0.X) / 2, (a0.Z + b0.Z) / 2);
        var mm0 = S.ModellMitte(lage0);
        var q = S.DreheXZ(new Xz(m0.X - mm0.X, m0.Z - mm0.Z), -lage0.Drehung);
        var punkt = V(bezug.Anker.X + q.X * lage0.Massstab, 0, bezug.Anker.Z + q.Z * lage0.Massstab);
        var nachher = SitzungRaum.ModellZuRaum(punkt, lage1, bezug);
        var m1 = new Xz((a1.X + b1.X) / 2, (a1.Z + b1.Z) / 2);
        Nah(nachher.X, m1.X, 1e-9);
        Nah(nachher.Z, m1.Z, 1e-9);
        Assert.True(lage1.Massstab < lage0.Massstab, "Hände auseinander: grösser");
    }

    [Fact]
    public void BezugAusBox_Mitte_der_Grundflaeche_tiefster_Punkt_und_Groesse()
    {
        var b = SitzungRaum.BezugAusBox(new Box3(V(-2, -3, 1), V(10, 9, 5)))!;
        Assert.Equal(new Xz(4, 3), b.Anker);
        Nah(b.Unten, -3);
        Assert.Equal(new ModellGroesse(12, 4, 12), b.Groesse);
        Assert.Null(SitzungRaum.BezugAusBox(Box3.Leer));
    }

    // ------------------------------------------------------------------------------------------------ Schnittquader

    [Fact]
    public void Oben_alles_ueber_der_Hoehe_entfaellt()
    {
        var q = SchnittQuader.Oben(2.5);
        Assert.True(q.Enthaelt(V(100, 2.5, -50)));
        Assert.False(q.Enthaelt(V(0, 2.51, 0)));
        Assert.Equal(SchnittLage.Innen, q.Lage(new Box3(V(0, 0, 0), V(5, 2.4, 5))));
        Assert.Equal(SchnittLage.Schneidet, q.Lage(new Box3(V(0, 2, 0), V(5, 3, 5))));
        Assert.Equal(SchnittLage.Aussen, q.Lage(new Box3(V(0, 2.6, 0), V(5, 3, 5))));
        Assert.Equal(SchnittLage.Aussen, q.Lage(Box3.Leer));
    }

    [Fact]
    public void AusSchnittbereich_ohne_Winkel_u_ist_x_v_ist_Nord_Grenzen_in_Metern()
    {
        var q = SchnittQuader.AusSchnittbereich(new SchnittbereichRoh(true, [1000, 2000, -500], [4000, 6000, 3000], 0))!;
        Nah(q.Ux, 1); Nah(q.Uz, 0); Nah(q.Vx, 0); Nah(q.Vz, -1);
        Nah(q.UMin, 1); Nah(q.UMax, 4); Nah(q.VMin, 2); Nah(q.VMax, 6); Nah(q.YMin, -0.5); Nah(q.YMax, 3);
        Assert.True(q.Enthaelt(V(2, 0, -3)));    // Szene z = −Nord: Nord 3 m
        Assert.False(q.Enthaelt(V(2, 0, 3)));
        Assert.Null(SchnittQuader.AusSchnittbereich(new SchnittbereichRoh(false, [0, 0, 0], [1000, 1000, 1000])));
        Assert.Null(SchnittQuader.AusSchnittbereich(new SchnittbereichRoh(true, [0, 0, 0], [0.5, 1000, 1000])));
    }

    [Fact]
    public void AusSchnittbereich_gedreht_stimmt_mit_den_Ebenen_von_SchnittbereichLogik_ueberein()
    {
        var roh = new SchnittbereichRoh(true, [-3000, 500, 0], [2500, 4200, 2800], 30);
        var q = SchnittQuader.AusSchnittbereich(roh)!;
        var ebenen = SchnittbereichLogik.Ebenen(SchnittbereichLogik.Lesen(roh)!);
        var zufall = new Random(7);
        for (int i = 0; i < 500; i++)
        {
            var p = V(zufall.NextDouble() * 12 - 6, zufall.NextDouble() * 4 - 0.5, zufall.NextDouble() * 12 - 9);
            var innen = ebenen.All(e => e.Normale[0] * p.X + e.Normale[1] * p.Y + e.Normale[2] * p.Z + e.Konstante >= 0);
            Assert.Equal(innen, q.Enthaelt(p));
        }
    }

    [Fact]
    public void Lage_einer_Box_gegen_den_gedrehten_Quader()
    {
        var q = SchnittQuader.AusSchnittbereich(new SchnittbereichRoh(true, [-2000, -2000, 0], [2000, 2000, 3000], 45))!;
        Assert.Equal(SchnittLage.Innen, q.Lage(new Box3(V(-0.5, 0.5, -0.5), V(0.5, 2, 0.5))));
        Assert.Equal(SchnittLage.Schneidet, q.Lage(new Box3(V(1.5, 0.5, -0.2), V(3, 2, 0.2))));
        Assert.Equal(SchnittLage.Aussen, q.Lage(new Box3(V(10, 0, 10), V(11, 1, 11))));
        Assert.Equal(SchnittLage.Aussen, q.Lage(new Box3(V(-0.5, 3.5, -0.5), V(0.5, 4, 0.5))));   // über dem Quader
    }

    [Fact]
    public void ShaderWerte_in_der_Reihenfolge_des_Shaders()
    {
        var (u, v, y) = new SchnittQuader(0.6, -0.8, -1, 2, -0.8, -0.6, -3, 4, -5, 6).ShaderWerte();
        Assert.Equal(new Vector4(0.6f, -0.8f, -1, 2), u);
        Assert.Equal(new Vector4(-0.8f, -0.6f, -3, 4), v);
        Assert.Equal(new Vector4(-5, 6, 0, 0), y);
    }
}
