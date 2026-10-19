namespace Hlks.Kern.Tests;

/// <summary>Modul 3b, B1: räumliche Grundfunktionen der Schmalphase (Geometrie3) mit Handrechnung – nächste Punkte,
/// Segment–Dreieck, Dreieckspaare (schneidend, berührend, parallel, koplanar, Toleranz), Quader–Dreieck (Trennachsen),
/// Zylinder–Dreieck. Alle Masse in mm.</summary>
public class KollisionGeometrieDreieckTests
{
    static readonly V3 O = new(0, 0, 0);
    // Grunddreieck in der Ebene z = 0
    static readonly V3 A = new(0, 0, 0), B = new(10, 0, 0), C = new(0, 10, 0);

    static void Nahe(V3 erwartet, V3 ist, double tol = 1e-9) =>
        Assert.True((erwartet - ist).Laenge <= tol, $"erwartet {erwartet}, ist {ist}");

    // ------------------------------------------------------------------ nächste Punkte

    [Fact]
    public void PunktDreieck_Innen_Kante_Ecke()
    {
        // über dem Inneren: Lotfusspunkt
        Nahe(new V3(2, 3, 0), Geometrie3.NaechsterPunktDreieck(new V3(2, 3, 7), A, B, C));
        // vor der Kante AB (y < 0): Punkt auf der Kante
        Nahe(new V3(4, 0, 0), Geometrie3.NaechsterPunktDreieck(new V3(4, -5, 1), A, B, C));
        // jenseits der Ecke B
        Nahe(B, Geometrie3.NaechsterPunktDreieck(new V3(15, -3, 0), A, B, C));
        // jenseits der Hypotenuse BC (x + y > 10): Lot auf BC, (6, 6) → (5, 5)
        Nahe(new V3(5, 5, 0), Geometrie3.NaechsterPunktDreieck(new V3(6, 6, 0), A, B, C));
    }

    [Fact]
    public void SegmentSegment_Windschief_Parallel_Endpunkte()
    {
        // windschief: x-Achse 0…10 und Gerade x = 5, z = 3 in y-Richtung → Abstand 3, beide Mitten
        double d = Geometrie3.SegmentSegment(O, new V3(10, 0, 0), new V3(5, -5, 3), new V3(5, 5, 3), out var s, out var t, out var c1, out var c2);
        Assert.Equal(3, d, 12);
        Assert.Equal(0.5, s, 12); Assert.Equal(0.5, t, 12);
        Nahe(new V3(5, 0, 0), c1); Nahe(new V3(5, 0, 3), c2);
        // parallel im Abstand 4 (y), überlappend
        Assert.Equal(4, Geometrie3.SegmentSegment(O, new V3(10, 0, 0), new V3(3, 4, 0), new V3(20, 4, 0), out _, out _, out _, out _), 12);
        // in Verlängerung: Abstand der Endpunkte 2
        Assert.Equal(2, Geometrie3.SegmentSegment(O, new V3(10, 0, 0), new V3(12, 0, 0), new V3(20, 0, 0), out _, out _, out _, out _), 12);
    }

    [Fact]
    public void SegmentDreieck_Durchstoss_Ueber_Daneben_Parallel()
    {
        // senkrecht durch (2, 2): Abstand 0, t = 0.5
        Assert.Equal(0, Geometrie3.SegmentDreieck(new V3(2, 2, -1), new V3(2, 2, 1), A, B, C, out var ps, out var pd, out var t));
        Assert.Equal(0.5, t, 12);
        Nahe(new V3(2, 2, 0), ps); Nahe(new V3(2, 2, 0), pd);
        Assert.True(Geometrie3.SegmentDreieckSchnitt(new V3(2, 2, -1), new V3(2, 2, 1), A, B, C, out _));
        // endet 3 über dem Dreieck
        Assert.Equal(3, Geometrie3.SegmentDreieck(new V3(2, 2, 3), new V3(2, 2, 9), A, B, C, out _, out pd, out _), 12);
        Nahe(new V3(2, 2, 0), pd);
        // parallel 5 über der Ebene (über dem Dreieck)
        Assert.Equal(5, Geometrie3.SegmentDreieck(new V3(1, 1, 5), new V3(3, 2, 5), A, B, C, out _, out _, out _), 12);
        Assert.False(Geometrie3.SegmentDreieckSchnitt(new V3(1, 1, 5), new V3(3, 2, 5), A, B, C, out _));
        // daneben in der Ebene: Strecke x = −4 parallel zu AC → Abstand 4 zur Kante AC
        Assert.Equal(4, Geometrie3.SegmentDreieck(new V3(-4, 2, 0), new V3(-4, 8, 0), A, B, C, out _, out _, out _), 12);
        // parallel neben der Kante AB: y = −2, z = 1 entlang x → Abstand √(2² + 1²)
        Assert.Equal(Math.Sqrt(5), Geometrie3.SegmentDreieck(new V3(3, -2, 1), new V3(7, -2, 1), A, B, C, out _, out _, out _), 12);
    }

    [Fact]
    public void GeradeDreieck_BaryzentrischUndParallel()
    {
        Assert.True(Geometrie3.GeradeDreieck(new V3(2, 3, 5), new V3(0, 0, -1), A, B, C, out var t, out var u, out var v));
        Assert.Equal(5, t, 12); Assert.Equal(0.2, u, 12); Assert.Equal(0.3, v, 12);
        Assert.False(Geometrie3.GeradeDreieck(new V3(2, 3, 5), new V3(1, 0, 0), A, B, C, out _, out _, out _));
    }

    // ------------------------------------------------------------------ Dreieckspaare

    [Fact]
    public void Dreieckspaar_Schneidend_MitSchnittstrecke()
    {
        // Dreieck in der Ebene x = 2: (y, z) = (1, −5), (1, 5), (6, 0) – durchstösst z = 0 auf y ∈ [1, 6];
        // das Grunddreieck schneidet x = 2 auf y ∈ [0, 8] → gemeinsame Strecke (2, 1, 0)…(2, 6, 0), Länge 5
        var lage = Geometrie3.DreieckDreieck(A, B, C, new V3(2, 1, -5), new V3(2, 1, 5), new V3(2, 6, 0), Geometrie3.Toleranz, out var s0, out var s1);
        Assert.Equal(DreieckLage.Schneidet, lage);
        var (lo, hi) = s0.Y < s1.Y ? (s0, s1) : (s1, s0);
        Nahe(new V3(2, 1, 0), lo, 1e-9); Nahe(new V3(2, 6, 0), hi, 1e-9);
        Assert.Equal(0, Geometrie3.DreieckDreieckAbstand(A, B, C, new V3(2, 1, -5), new V3(2, 1, 5), new V3(2, 6, 0), out _, out _), 12);
        // Reihenfolge der Dreiecke egal
        Assert.Equal(DreieckLage.Schneidet, Geometrie3.DreieckDreieck(new V3(2, 1, -5), new V3(2, 1, 5), new V3(2, 6, 0), A, B, C));
    }

    [Fact]
    public void Dreieckspaar_Beruehrend_EckeAufFlaeche_KanteAufFlaeche()
    {
        // Ecke (2, 2, 0) liegt auf dem Grunddreieck, die anderen 5 darüber → Berührung, keine Durchdringung
        Assert.Equal(DreieckLage.Beruehrt, Geometrie3.DreieckDreieck(A, B, C, new V3(2, 2, 0), new V3(2, 2, 5), new V3(4, 2, 5)));
        // Kante (1, 1, 0)–(4, 1, 0) liegt in der Fläche, dritte Ecke darüber
        Assert.Equal(DreieckLage.Beruehrt, Geometrie3.DreieckDreieck(A, B, C, new V3(1, 1, 0), new V3(4, 1, 0), new V3(2, 1, 6)));
        // Toleranz: nur 0.05 mm unter die Ebene (< 0.1) → Berührung, nicht Durchdringung
        Assert.Equal(DreieckLage.Beruehrt, Geometrie3.DreieckDreieck(A, B, C, new V3(2, 2, -0.05), new V3(2, 2, 5), new V3(4, 2, 5)));
        // 0.5 mm darunter → echte Durchdringung
        Assert.Equal(DreieckLage.Schneidet, Geometrie3.DreieckDreieck(A, B, C, new V3(2, 2, -0.5), new V3(2, 2, 5), new V3(4, 2, 5)));
    }

    [Fact]
    public void Dreieckspaar_Parallel_Getrennt_MitAbstand()
    {
        var a2 = A + new V3(0, 0, 3); var b2 = B + new V3(0, 0, 3); var c2 = C + new V3(0, 0, 3);
        Assert.Equal(DreieckLage.Getrennt, Geometrie3.DreieckDreieck(A, B, C, a2, b2, c2));
        Assert.Equal(3, Geometrie3.DreieckDreieckAbstand(A, B, C, a2, b2, c2, out var pa, out var pb), 12);
        Assert.Equal(3, (pb - pa).Laenge, 12);
        // Ebenen schneiden sich, die Dreiecke nicht (Schnittstrecken auf der Geraden getrennt)
        Assert.Equal(DreieckLage.Getrennt, Geometrie3.DreieckDreieck(A, B, C, new V3(20, 1, -5), new V3(20, 1, 5), new V3(20, 6, 0)));
    }

    [Fact]
    public void Dreieckspaar_Koplanar_UeberlappendIstBeruehrung_GetrenntIstGetrennt()
    {
        Assert.Equal(DreieckLage.Beruehrt, Geometrie3.DreieckDreieck(A, B, C, new V3(1, 1, 0), new V3(5, 1, 0), new V3(1, 5, 0)));
        Assert.Equal(DreieckLage.Beruehrt, Geometrie3.DreieckDreieck(A, B, C, new V3(-2, -2, 0), new V3(20, -2, 0), new V3(-2, 20, 0)));   // enthält das Grunddreieck
        Assert.Equal(DreieckLage.Getrennt, Geometrie3.DreieckDreieck(A, B, C, new V3(20, 0, 0), new V3(30, 0, 0), new V3(20, 10, 0)));
        Assert.Equal(10, Geometrie3.DreieckDreieckAbstand(A, B, C, new V3(20, 0, 0), new V3(30, 0, 0), new V3(20, 10, 0), out _, out _), 12);
    }

    // ------------------------------------------------------------------ Quader und Zylinder

    static readonly V3 X = new(1, 0, 0), Y = new(0, 1, 0), Z = new(0, 0, 1);

    [Fact]
    public void QuaderDreieck_Trennachsen()
    {
        // Würfel [−1, 1]³
        Assert.True(Geometrie3.QuaderDreieck(O, X, Y, Z, 1, 1, 1, new V3(-5, -5, 0), new V3(5, -5, 0), new V3(0, 5, 0)));    // schneidet mittig
        Assert.False(Geometrie3.QuaderDreieck(O, X, Y, Z, 1, 1, 1, new V3(-5, -5, 2), new V3(5, -5, 2), new V3(0, 5, 2)));   // darüber
        Assert.True(Geometrie3.QuaderDreieck(O, X, Y, Z, 1, 1, 1, new V3(-0.5, -0.5, 0), new V3(0.5, -0.5, 0), new V3(0, 0.5, 0)));   // ganz innen
        // nur über eine Kantenachse getrennt: Dreieck mit Kante auf x + y = 2.4 (z = 0) und Spitze (5, 5, 10); seine
        // Ebene schneidet den Würfel, die Hüllquader überlappen, aber x + y ≥ 2.4 > 2 = Würfelausdehnung
        var a = new V3(4.2, -1.8, 0); var b = new V3(-1.8, 4.2, 0); var c = new V3(5, 5, 10);
        Assert.False(Geometrie3.QuaderDreieck(O, X, Y, Z, 1, 1, 1, a, b, c));
        // gleicher Fall um 30° gedreht (Quaderachsen und Dreieck mitgedreht)
        double w = Math.PI / 6;
        V3 D(V3 p) => new(p.X * Math.Cos(w) - p.Y * Math.Sin(w), p.X * Math.Sin(w) + p.Y * Math.Cos(w), p.Z);
        Assert.False(Geometrie3.QuaderDreieck(O, D(X), D(Y), Z, 1, 1, 1, D(a), D(b), D(c)));
        Assert.True(Geometrie3.QuaderDreieck(O, D(X), D(Y), Z, 1.3, 1.3, 1, D(a), D(b), D(c)));   // grösser: 2.6 > 2.4
    }

    [Fact]
    public void ZylinderDreieck_Exakt()
    {
        var p = new V3(0, 0, 0); var q = new V3(0, 0, 10);
        Assert.True(Geometrie3.ZylinderDreieck(p, q, 1, new V3(-5, -5, 5), new V3(5, -5, 5), new V3(0, 5, 5)));      // quer durch die Mitte
        Assert.False(Geometrie3.ZylinderDreieck(p, q, 1, new V3(1.5, -5, 0), new V3(1.5, 5, 0), new V3(1.5, 0, 10)));  // Ebene x = 1.5, parallel
        Assert.True(Geometrie3.ZylinderDreieck(p, q, 1, new V3(0.9, -5, 0), new V3(0.9, 5, 0), new V3(0.9, 0, 10)));   // x = 0.9 < r
        Assert.False(Geometrie3.ZylinderDreieck(p, q, 1, new V3(-5, -5, 11), new V3(5, -5, 11), new V3(0, 5, 12)));    // hinter dem Ende
        // Dreieck nahe der Achse, aber nur jenseits der Endfläche – vorn abgeschnitten bleibt nichts
        Assert.False(Geometrie3.ZylinderDreieck(p, q, 1, new V3(0.2, 0, -3), new V3(0.4, 0, -1), new V3(0.3, 0.2, -2)));
    }

    [Fact]
    public void Huelle_Abstand_Schneidet_Umfasst()
    {
        var h = Huelle3.Aus(new V3(0, 0, 0), new V3(10, 10, 10));
        var g = Huelle3.Aus(new V3(13, 14, 0), new V3(20, 20, 5));
        Assert.Equal(5, h.Abstand(g), 12);                         // √(3² + 4²)
        Assert.False(h.Schneidet(g)); Assert.True(h.Schneidet(g, 4)); Assert.False(h.Schneidet(g, 3.9));
        Assert.True(h.Umfasst(Huelle3.Aus(new V3(1, 1, 1), new V3(2, 2, 2))));
        Assert.True(Huelle3.Leer.IstLeer);
        Assert.Equal(h, Huelle3.Leer.Mit(h));
    }
}
