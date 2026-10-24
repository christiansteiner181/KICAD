// Quest App P5b: Schnitt mit der flachen Hand am Sitzungstisch – Lage der Hand (waagrecht, senkrecht, nicht flach), Ruhe, Folgen, Behalten, über dem Modell,
// der Schnitt in der Szene (Höhe bzw. Ebene zum Kopf hin, Fang an die Achsen) und der Quader für den Shader zusammen mit dem Schnitt der Ansicht.

using System.Numerics;
using Hlks.Xr.Sitzung;
using static Hlks.Xr.Tests.HandHilfen;
using static Hlks.Xr.Tests.T;

namespace Hlks.Xr.Tests;

public class FlacheHandTests
{
    const double W = SchnittQuader.Weit;
    // Modell 1:50 auf dem Tisch: Szene (−10 … 10, 0 … 6, −8 … 8) → Raum der Brille x −0.2 … 0.2, y 0.75 … 0.87, z −0.76 … −0.44
    static readonly Matrix4x4 ZuRaum = Matrix4x4.CreateScale(0.02f) * Matrix4x4.CreateTranslation(0, 0.75f, -0.6f);
    static readonly Box3 Modell = new(V(-10, 0, -8), V(10, 6, 8));
    static readonly Vec3 Kopf = V(0, 1.4, 0);
    static readonly Vec3 Vor = V(0, 0, -1), Unten = V(0, -1, 0), Oben = V(0, 1, 0), Links = V(-1, 0, 0);

    /// <summary>Rechte Hand mit der Mitte der Handfläche bei m (Raum der Brille), Finger f, Normale n.</summary>
    static Dictionary<string, Vec3> HandBei(Vec3 m, Vec3 f, Vec3 n, bool offen = true) => Hand("right", m - f.Einheit() * 0.045, f, n, offen);

    static List<(FlacheHandEreignis E, HandSchnitt? S)> Lauf(FlacheHandZustand z, IEnumerable<(double T, Dictionary<string, Vec3>? G)> bilder, SichtbarRahmen? grundriss = null,
        bool pflicht = false, FlacheHandEinstellungen? e = null) =>
        [.. bilder.Select(b => FlacheHand.Schritt(z, b.T, b.G, "right", Kopf, ZuRaum, Modell, grundriss, pflicht, e))];

    [Fact]
    public void Lage_waagrecht_senkrecht_oder_keine()
    {
        var m = V(0, 0.82, -0.6);
        Assert.Equal(HandSchnittArt.Waagrecht, FlacheHand.Lage(HandBei(m, Vor, Unten), "right", Kopf, 25, false));
        Assert.Equal(HandSchnittArt.Senkrecht, FlacheHand.Lage(HandBei(m, Vor, Links), "right", Kopf, 25, false));   // Handfläche zur Seite, Finger nach vorn
        Assert.Null(FlacheHand.Lage(HandBei(m, Vor, Oben), "right", Kopf, 25, false));                               // Handfläche nach oben
        Assert.Null(FlacheHand.Lage(HandBei(m, Vor, Unten, offen: false), "right", Kopf, 25, false));                // Faust
        var schraeg = V(0, -Math.Cos(45 * Grad), Math.Sin(45 * Grad));                                              // 45° gekippt
        Assert.Null(FlacheHand.Lage(HandBei(m, V(0, -Math.Sin(45 * Grad), -Math.Cos(45 * Grad)), schraeg), "right", Kopf, 25, false));
        // 30° gekippt: nicht neu erkannt, aber solange der Schnitt folgt (Hysterese 10°) noch waagrecht
        var n30 = V(0, -Math.Cos(30 * Grad), Math.Sin(30 * Grad));
        var f30 = V(0, -Math.Sin(30 * Grad), -Math.Cos(30 * Grad));
        Assert.Null(FlacheHand.Lage(HandBei(m, f30, n30), "right", Kopf, 25, false));
        Assert.Equal(HandSchnittArt.Waagrecht, FlacheHand.Lage(HandBei(m, f30, n30), "right", Kopf, 25, true));
        // Handfläche zum Gesicht (holt die Apps): kein Vertikalschnitt
        Assert.Null(FlacheHand.Lage(HandBei(V(0, 1.35, -0.6), Oben, V(0, 0, 1)), "right", Kopf, 25, false));
        Assert.Equal(HandSchnittArt.Senkrecht, FlacheHand.Lage(HandBei(V(0, 1.35, -0.6), Oben, V(0, 0, -1)), "right", Kopf, 25, false));   // Handrücken zum Gesicht
        // linke Hand genauso
        Assert.Equal(HandSchnittArt.Waagrecht, FlacheHand.Lage(Hand("left", m - Vor * 0.045, Vor, Unten), "left", Kopf, 25, false));
    }

    [Fact]
    public void Waagrecht_kurz_ruhig_dann_folgt_der_Schnitt_Hand_weg_behaelt_ihn()
    {
        var z = new FlacheHandZustand();
        var r = Lauf(z, [(0, HandBei(V(0, 0.81, -0.6), Vor, Unten)), (0.15, HandBei(V(0.005, 0.81, -0.6), Vor, Unten)), (0.31, HandBei(V(0.01, 0.81, -0.6), Vor, Unten))]);
        Assert.Equal([FlacheHandEreignis.Erkannt, FlacheHandEreignis.Erkannt, FlacheHandEreignis.Beginnt], r.Select(x => x.E));
        Nah(r[2].S!.Hoehe, 3, 1e-4);                                              // (0.81 − 0.75) / 0.02 = 3 m in der Szene
        Assert.Equal(HandSchnittArt.Waagrecht, r[2].S!.Art);
        // die Hand sinkt: der Schnitt folgt
        var f = Lauf(z, [(0.4, HandBei(V(0.01, 0.79, -0.6), Vor, Unten))]);
        Assert.Equal(FlacheHandEreignis.Folgt, f[0].E);
        Nah(f[0].S!.Hoehe, 2, 1e-4);
        // Faust: der Schnitt bleibt stehen; danach zählt die Faust nicht mehr
        var b = Lauf(z, [(0.5, HandBei(V(0.01, 0.79, -0.6), Vor, Unten, offen: false)), (0.6, HandBei(V(0.01, 0.79, -0.6), Vor, Unten, offen: false))]);
        Assert.Equal(FlacheHandEreignis.Behalten, b[0].E);
        Nah(b[0].S!.Hoehe, 2, 1e-4);
        Assert.Equal(FlacheHandEreignis.Nichts, b[1].E);
        Assert.False(z.Folgt);
        Assert.NotNull(z.Schnitt);
        // Hand weg (nicht erkannt) während des Folgens: ebenfalls Behalten
        var z2 = new FlacheHandZustand();
        Lauf(z2, [(0, HandBei(V(0, 0.81, -0.6), Vor, Unten)), (0.35, HandBei(V(0, 0.81, -0.6), Vor, Unten))]);
        Assert.Equal(FlacheHandEreignis.Behalten, Lauf(z2, [(0.4, null)])[0].E);
        z2.Zuruecksetzen();
        Assert.Null(z2.Schnitt);
    }

    [Fact]
    public void Unruhig_neben_dem_Modell_oder_zu_hoch_zaehlt_nicht()
    {
        // bewegt sich die Hand während der Ruhezeit mehr als 2 cm, beginnt die Ruhezeit neu
        var z = new FlacheHandZustand();
        var r = Lauf(z, [(0, HandBei(V(0, 0.81, -0.6), Vor, Unten)), (0.2, HandBei(V(0.05, 0.81, -0.6), Vor, Unten)), (0.35, HandBei(V(0.05, 0.81, -0.6), Vor, Unten)),
            (0.55, HandBei(V(0.05, 0.81, -0.6), Vor, Unten))]);
        Assert.Equal([FlacheHandEreignis.Erkannt, FlacheHandEreignis.Erkannt, FlacheHandEreignis.Erkannt, FlacheHandEreignis.Beginnt], r.Select(x => x.E));
        // neben dem Modell (0.3 m rechts; der Rand ist 5 cm) und weit darüber: nichts
        var z2 = new FlacheHandZustand();
        Assert.All(Lauf(z2, [(0, HandBei(V(0.3, 0.81, -0.6), Vor, Unten)), (0.5, HandBei(V(0.3, 0.81, -0.6), Vor, Unten))]), x => Assert.Equal(FlacheHandEreignis.Nichts, x.E));
        Assert.All(Lauf(z2, [(1, HandBei(V(0, 1.3, -0.6), Vor, Unten)), (1.5, HandBei(V(0, 1.3, -0.6), Vor, Unten))]), x => Assert.Equal(FlacheHandEreignis.Nichts, x.E));
        // die Stellschraube Ruhezeit
        var z3 = new FlacheHandZustand();
        var schnell = Lauf(z3, [(0, HandBei(V(0, 0.81, -0.6), Vor, Unten)), (0.1, HandBei(V(0, 0.81, -0.6), Vor, Unten))], e: new FlacheHandEinstellungen { RuheS = 0.1 });
        Assert.Equal(FlacheHandEreignis.Beginnt, schnell[1].E);
        Assert.True(FlacheHand.UeberDemModell(V(0, 3, 0), Modell, null, 50));
        Assert.False(FlacheHand.UeberDemModell(V(0, 3, 13), Modell, null, 50));   // 5 m neben der Box (Rand 2.5 m in der Szene)
    }

    [Fact]
    public void Senkrecht_Ebene_durch_die_Hand_die_Seite_zum_Kopf_faellt_weg()
    {
        var z = new FlacheHandZustand();
        var r = Lauf(z, [(0, HandBei(V(0.05, 0.82, -0.6), Vor, Links)), (0.35, HandBei(V(0.05, 0.82, -0.6), Vor, Links))]);
        var s = r[1].S!;
        Assert.Equal(HandSchnittArt.Senkrecht, s.Art);
        Nah(s.Punkt.X, 2.5, 1e-4);
        Nah(s.Normale.X, -1, 1e-6);                                               // zum Kopf hin (der Kopf steht bei x = 0 < 2.5)
        var q = s.Kombiniert(SchnittQuader.Oben(5));
        Assert.True(q.Enthaelt(V(4, 1, 0)));                                     // jenseits der Ebene: bleibt
        Assert.False(q.Enthaelt(V(1, 1, 0)));                                    // zum Kopf hin: weg
        Assert.False(q.Enthaelt(V(4, 6, 0)));                                    // über dem Etagenschnitt: weg
        // Hand vor dem Modell quer zum Blick, Handrücken zum Kopf: die vordere Hälfte fällt weg, man blickt auf die Schnittfläche
        var quer = FlacheHand.SchnittAus(HandSchnittArt.Senkrecht, V(0, 3, 2), V(0, 0, -1), V(0, 32, 30))!;
        Nah(quer.Normale.Z, 1, 1e-9);
        Assert.False(quer.Kombiniert(null).Enthaelt(V(0, 1, 5)));
        Assert.True(quer.Kombiniert(null).Enthaelt(V(0, 1, -5)));
    }

    [Fact]
    public void Fang_an_die_Achsen_des_Gebaeudes_und_Pflicht_im_Ausschnitt()
    {
        double c = Math.Cos(43 * Grad), s = Math.Sin(43 * Grad);
        var achsen = new SichtbarRahmen(c, s, -10, 10, -8, 8);                    // Gebäude 43° gedreht (wie Test_praxis)
        var n48 = V(Math.Cos(48 * Grad), 0, Math.Sin(48 * Grad));
        var kopf = V(50, 0, 50);
        var a = FlacheHand.SchnittAus(HandSchnittArt.Senkrecht, V(0, 0, 0), n48, kopf, achsen)!;
        Nah(a.Normale.X, c, 1e-9); Nah(a.Normale.Z, s, 1e-9);                    // 5° daneben: rastet ein
        var n70 = V(Math.Cos(70 * Grad), 0, Math.Sin(70 * Grad));
        var b = FlacheHand.SchnittAus(HandSchnittArt.Senkrecht, V(0, 0, 0), n70, kopf, achsen)!;
        Nah(b.Normale.X, Math.Cos(70 * Grad), 1e-9);                             // 27° daneben: frei
        var p = FlacheHand.SchnittAus(HandSchnittArt.Senkrecht, V(0, 0, 0), n70, kopf, achsen, achsenPflicht: true)!;
        Nah(p.Normale.X, c, 1e-9);                                                // Pflicht: die nächste Achse
        Assert.Null(FlacheHand.SchnittAus(HandSchnittArt.Senkrecht, V(0, 0, 0), V(0, 1, 0), kopf));   // keine waagrechte Richtung
    }

    [Fact]
    public void Kombiniert_mit_dem_Schnitt_der_Ansicht_und_Rahmen()
    {
        var w = new HandSchnitt(HandSchnittArt.Waagrecht, 3, new Xz(0, 0), new Xz(0, 0));
        Assert.Equal(3, w.Kombiniert(SchnittQuader.Oben(5)).YMax);
        Assert.Equal(2, w.Kombiniert(SchnittQuader.Oben(2)).YMax);               // die kleinere Oberkante
        Assert.Equal(3, w.Kombiniert(null).YMax);
        Assert.Equal(-W, w.Kombiniert(null).UMin);
        // Ausschnitt (Quader mit Achsen U = x, V = z): die senkrechte Ebene wird eine weitere Grenze in der nächsten Achse
        var aus = new SchnittQuader(1, 0, 0, 10, 0, 1, -5, 5, 0, 3);
        var nachX = new HandSchnitt(HandSchnittArt.Senkrecht, 0, new Xz(4, 0), new Xz(0.99, 0.14));
        Assert.Equal(aus with { UMax = 4 }, nachX.Kombiniert(aus));
        var nachMinusZ = new HandSchnitt(HandSchnittArt.Senkrecht, 0, new Xz(4, -2), new Xz(-0.2, -0.98));
        Assert.Equal(aus with { VMin = -2 }, nachMinusZ.Kombiniert(aus));
        var aussen = new HandSchnitt(HandSchnittArt.Senkrecht, 0, new Xz(20, 0), new Xz(1, 0));
        Assert.Equal(10, aussen.Kombiniert(aus).UMax);                            // jenseits des Ausschnitts: der Ausschnitt bleibt
        // Werte für den Shader
        var (u, v, y) = nachX.Kombiniert(aus).ShaderWerte();
        Assert.Equal(new Vector4(1, 0, 0, 4), u);
        Assert.Equal(new Vector4(0, 1, -5, 5), v);
        Assert.Equal(new Vector4(0, 3, 0, 0), y);
        // Rahmen der Schnittfläche in der Box
        Assert.Equal([V(-10, 3, -8), V(10, 3, -8), V(10, 3, 8), V(-10, 3, 8)], w.Rahmen(Modell));
        Assert.Empty((w with { Hoehe = 9 }).Rahmen(Modell));
        var senk = new HandSchnitt(HandSchnittArt.Senkrecht, 0, new Xz(2.5, 0), new Xz(-1, 0));
        var r = senk.Rahmen(Modell);
        Assert.Equal(4, r.Count);
        Assert.All(r, p => Nah(p.X, 2.5, 1e-9));
        Assert.Equal([-8.0, 8.0], r.Take(2).Select(p => p.Z).Order());
        Assert.Empty(new HandSchnitt(HandSchnittArt.Senkrecht, 0, new Xz(30, 0), new Xz(-1, 0)).Rahmen(Modell));
    }
}
