// Tests der Quest App V1g (Brillentest, Christian 24.10.2026): Kippen am roten Punkt um eine waagrechte Achse durch das Zentrum, Grenze, Drehung bleibt, Anheben über dem
// Tisch, Doppeltipp setzt die Neigung zurück; Symbole blenden nach der Wahl aus und sind nach dem Schliessen einer Tafel kurz gesperrt; keine Bodenplatte (Stellschraube
// «sitzung.umriss»).

using System.Numerics;
using System.Text.Json;
using Hlks.Xr.Bedienung;
using Hlks.Xr.Sitzung;
using static Hlks.Xr.Tests.T;

namespace Hlks.Xr.Tests;

public class V1gTests
{
    static JsonElement W(string j) => JsonDocument.Parse(j).RootElement.Clone();
    static readonly SitzungBezug Bezug = new(new Xz(0, 0), 0, new ModellGroesse(20, 10, 6));
    static readonly Vec3 Kopf = V(0, 1.2, 0);   // Raum-lokal: der Mensch sitzt am Ursprung, der Tisch liegt vor ihm (−z)
    static EinGriffHand Hd(Vec3 p, bool fasst = true) => new(fasst, p, 0, 0);

    /// <summary>Fasst den roten Punkt mit der rechten Hand und führt ihn nach ziel (eine Bewegung in Schritten); gibt den Zustand zurück.</summary>
    static ZweiPunkteZustand Fuehren(SitzungLage start, Func<ZweiPunkteLage, Vec3> ziel, ZweiPunkteEinstellungen? e = null, Vec3? kopf = null, bool loslassen = false)
    {
        var z = ZweiPunkte.Neu(start);
        var p = ZweiPunkte.Punkte(z.Lage, Bezug, null);
        ZweiPunkte.Schritt(z, null, null, p, e, null, kopf ?? Kopf, Bezug, null, 0);
        Assert.Equal(ZweiPunkteEreignis.Gefasst, ZweiPunkte.Schritt(z, null, Hd(p.Drehen), p, e, null, kopf ?? Kopf, Bezug, null, 0.1));
        var h0 = p.Drehen;
        var h1 = ziel(p);
        for (int i = 1; i <= 10; i++) ZweiPunkte.Schritt(z, null, Hd(h0 + (h1 - h0) * (i / 10.0)), p, e, null, kopf ?? Kopf, Bezug, null, 0.1 + i * 0.05);
        if (loslassen) ZweiPunkte.Schritt(z, null, null, p, e, null, kopf ?? Kopf, Bezug, null, 0.7);
        return z;
    }

    /// <summary>Hand am roten Punkt so weit gehoben, dass der Winkel über der Unterkante um grad zunimmt (gleicher waagrechter Abstand zum Zentrum).</summary>
    static Vec3 Gehoben(ZweiPunkteLage p, double grad, SitzungLage lage)
    {
        var yb = XrSitzungLogik.TischHoehe + lage.Hoehe;
        var r = Math.Sqrt((p.Drehen.X - p.Zentrum.X) * (p.Drehen.X - p.Zentrum.X) + (p.Drehen.Z - p.Zentrum.Z) * (p.Drehen.Z - p.Zentrum.Z));
        var a0 = Math.Atan2(p.Drehen.Y - yb, r);
        return p.Drehen with { Y = yb + r * Math.Tan(a0 + grad * Grad) };
    }

    [Fact]
    public void Kippen_um_eine_waagrechte_Achse_durch_das_Zentrum_quer_zum_Blick_die_Seite_zum_Betrachter_hebt_sich()
    {
        var start = new SitzungLage(50, 0, 0, 0);
        var p0 = ZweiPunkte.Punkte(start, Bezug, null);
        var z = Fuehren(start, p => Gehoben(p, 33, start));
        var k = z.Lage.Kippung!;
        Nah(k.Winkel, 30 * Grad, 1e-6);                       // 33° minus 3° Totzone (wie beim Drehen)
        Nah(Math.Abs(k.Achse.X), 1, 1e-9);                    // quer zum Blick (Kopf → Zentrum geht nach −z): Achse ±x
        Nah(k.Achse.Z, 0, 1e-9);
        // Drehung und Massstab bleiben (die Hand bleibt im selben waagrechten Abstand und Winkel)
        Nah(z.Lage.Drehung, 0, 1e-9);
        Nah(z.Lage.Massstab, 50, 1e-9);
        // das Zentrum bleibt an seinem Platz (waagrecht), die Seite zum Betrachter (+z) hebt sich, die hintere senkt sich relativ dazu
        var p1 = ZweiPunkte.Punkte(z.Lage, Bezug, null);
        Nah(p1.Zentrum.X, p0.Zentrum.X, 1e-9);
        Nah(p1.Zentrum.Z, p0.Zentrum.Z, 1e-9);
        var vorn = SitzungRaum.ModellZuRaum(V(0, 0, 5), z.Lage, Bezug);      // vordere Kante (Szene +z → Raum vorn)
        var hinten = SitzungRaum.ModellZuRaum(V(0, 0, -5), z.Lage, Bezug);
        Assert.True(vorn.Y > hinten.Y + 0.05, $"{vorn.Y} {hinten.Y}");
        Nah(vorn.Y - hinten.Y, 0.2 * Math.Sin(30 * Grad), 1e-9);            // 10 m tief bei 1:50 = 0.2 m, um 30° gekippt
        Assert.True(p1.Drehen.Y > p0.Drehen.Y);                              // der rote Punkt geht mit der Hand hoch
        // die Matrix kippt gleich wie die Punkte
        foreach (var q in new[] { V(0, 0, 0), V(10, 0, 5), V(-10, 6, -5), V(3, 2, 1) })
        {
            var a = SitzungRaum.ModellZuRaum(q, z.Lage, Bezug);
            var b = (Vec3)Vector3.Transform((Vector3)q, SitzungRaum.ModellMatrix(z.Lage, Bezug));
            Nah(a.Abstand(b), 0, 1e-5);
        }
        // nach unten gekippt: die Seite zum Betrachter senkt sich
        var zu = Fuehren(start, p => Gehoben(p, -23, start));
        Nah(zu.Lage.Kippung!.Winkel, -20 * Grad, 1e-6);
        Assert.True(SitzungRaum.ModellZuRaum(V(0, 0, 5), zu.Lage, Bezug).Y < SitzungRaum.ModellZuRaum(V(0, 0, -5), zu.Lage, Bezug).Y);
        // sitzt der Mensch seitlich (Kopf rechts vom Modell), liegt die Achse quer zu seiner Blickrichtung
        var seitlich = Fuehren(start, p => Gehoben(p, 33, start), kopf: V(1.2, 1.2, -0.9));
        Nah(Math.Abs(seitlich.Lage.Kippung!.Achse.Z), 1, 1e-6);
    }

    [Fact]
    public void Grenze_Totzone_und_Stellschraube_kipp_max_grad()
    {
        var start = new SitzungLage(50, 0, 0, 0);
        Nah(Fuehren(start, p => Gehoben(p, 70, start)).Lage.Kippung!.Winkel, 60 * Grad, 1e-9);   // höchstens 60°
        Nah(Fuehren(start, p => Gehoben(p, -80, start)).Lage.Kippung!.Winkel, -60 * Grad, 1e-9);
        Assert.Null(Fuehren(start, p => Gehoben(p, 2.5, start)).Lage.Kippung);                    // in der Totzone: nichts
        var e = XrBedienEinstellungen.Vorgabe;
        Assert.Equal(60, e.Punkte.KippMaxGrad);
        var dreissig = e.MitStellschraube("sitzung.kipp_max_grad", W("30"))!;
        Nah(Fuehren(start, p => Gehoben(p, 70, start), dreissig.Punkte).Lage.Kippung!.Winkel, 30 * Grad, 1e-9);
        Assert.Null(Fuehren(start, p => Gehoben(p, 70, start), e.MitStellschraube("sitzung.kipp_max_grad", W("0"))!.Punkte).Lage.Kippung);   // 0 = nicht kippen
        Assert.Null(e.MitStellschraube("sitzung.kipp_max_grad", W("90")));
    }

    [Fact]
    public void Drehen_und_Vergroessern_bleiben_die_Neigung_bleibt_beim_Drehen_und_Verschieben()
    {
        var start = new SitzungLage(50, 0, 0, 0);
        // links/rechts um das Zentrum: dreht, kippt nicht
        var z = Fuehren(start, p =>
        {
            var c = p.Zentrum;
            var d = XrSitzungLogik.DreheXZ(new Xz(p.Drehen.X - c.X, p.Drehen.Z - c.Z), 30 * Grad);
            return new Vec3(c.X + d.X, p.Drehen.Y, c.Z + d.Z);
        });
        Nah(z.Lage.Drehung, 27 * Grad, 1e-9);
        Assert.Null(z.Lage.Kippung);
        // erst kippen, loslassen; dann drehen: die Neigung bleibt, das Zentrum bleibt
        var g = Fuehren(start, p => Gehoben(p, 33, start), loslassen: true);
        var gekippt = g.Lage;
        var p1 = ZweiPunkte.Punkte(gekippt, Bezug, null);
        ZweiPunkte.Schritt(g, null, Hd(p1.Drehen), p1, null, null, Kopf, Bezug, null, 2.0);
        var c1 = p1.Zentrum;
        var d1 = XrSitzungLogik.DreheXZ(new Xz(p1.Drehen.X - c1.X, p1.Drehen.Z - c1.Z), -40 * Grad);
        ZweiPunkte.Schritt(g, null, Hd(new Vec3(c1.X + d1.X, p1.Drehen.Y, c1.Z + d1.Z)), p1, null, null, Kopf, Bezug, null, 2.3);
        Nah(g.Lage.Kippung!.Winkel, 30 * Grad, 1e-9);
        Nah(g.Lage.Drehung, -37 * Grad, 1e-9);
        var p2 = ZweiPunkte.Punkte(g.Lage, Bezug, null);
        Nah(p2.Zentrum.X, c1.X, 1e-9);
        Nah(p2.Zentrum.Z, c1.Z, 1e-9);
        // weiter weg: vergrössert weiter, die Neigung bleibt
        ZweiPunkte.Schritt(g, null, null, p2, null, null, Kopf, Bezug, null, 2.6);
        var p3 = ZweiPunkte.Punkte(g.Lage, Bezug, null);
        ZweiPunkte.Schritt(g, null, Hd(p3.Drehen), p3, null, null, Kopf, Bezug, null, 3.0);
        var c3 = p3.Zentrum;
        ZweiPunkte.Schritt(g, null, Hd(new Vec3(c3.X + (p3.Drehen.X - c3.X) * 1.5, p3.Drehen.Y, c3.Z + (p3.Drehen.Z - c3.Z) * 1.5)), p3, null, null, Kopf, Bezug, null, 3.3);
        Nah(g.Lage.Massstab, 50 / (1.5 / 1.05), 1e-6);
        Nah(g.Lage.Kippung!.Winkel, 30 * Grad, 1e-9);   // nach aussen zoomt nur (die Höhe der Hand zählt im Abstand beim Fassen)
        // verschieben (blauer Punkt): die Neigung kommt mit
        ZweiPunkte.Schritt(g, null, null, p3, null, null, Kopf, Bezug, null, 3.6);
        var p4 = ZweiPunkte.Punkte(g.Lage, Bezug, null);
        var w4 = g.Lage.Kippung!.Winkel;
        ZweiPunkte.Schritt(g, null, Hd(p4.Verschieben), p4, null, null, Kopf, Bezug, null, 4.0);
        ZweiPunkte.Schritt(g, null, Hd(p4.Verschieben + V(0.1, 0.05, 0)), p4, null, null, Kopf, Bezug, null, 4.2);
        Nah(g.Lage.Kippung!.Winkel, w4, 1e-12);
        Nah(ZweiPunkte.Punkte(g.Lage, Bezug, null).Zentrum.X, p4.Zentrum.X + 0.1, 1e-9);
    }

    [Fact]
    public void Das_gekippte_Modell_taucht_nie_unter_seine_Unterkante_es_hebt_sich()
    {
        foreach (var dreh in new[] { 0.0, 35, 90 })
            foreach (var grad in new[] { 10.0, 30, 60, -45 })
            {
                var start = new SitzungLage(50, dreh * Grad, 0.05, -0.02, 0.03);
                var z = Fuehren(start, p => Gehoben(p, grad + 3 * Math.Sign(grad), start));
                var k = z.Lage.Kippung!;
                Assert.True(k.Anheben > 0, $"{dreh} {grad}");
                var unten = XrSitzungLogik.TischHoehe + z.Lage.Hoehe;
                var tiefst = Kippen.Grundflaeche(Bezug, null).Min(e => SitzungRaum.ModellZuRaum(e, z.Lage, Bezug).Y);
                Nah(tiefst, unten, 1e-9);                   // die tiefste Ecke genau auf der Unterkante (angehoben, nicht mehr)
            }
        Assert.Equal(0, Kippen.Anheben(new SitzungLage(50, 0, 0, 0), Bezug, null));   // ohne Kippung
        // ohne Kippung: ModellZuRaum wie bis V1f
        var l = new SitzungLage(40, 0.3, 0.1, -0.05, 0.02);
        Nah(SitzungRaum.ModellZuRaum(V(3, 2, 1), l, Bezug).Abstand(SitzungRaum.ModellZuRaumOhneKippung(V(3, 2, 1), l, Bezug)), 0, 1e-12);
    }

    [Fact]
    public void Doppeltipp_am_roten_Punkt_stellt_die_Neigung_auf_null_Zuruecksetzen_ebenso()
    {
        var start = new SitzungLage(50, 0, 0, 0);
        var z = Fuehren(start, p => Gehoben(p, 33, start), loslassen: true);   // losgelassen bei t = 0.7
        Assert.NotNull(z.Lage.Kippung);
        var p = ZweiPunkte.Punkte(z.Lage, Bezug, null);
        // erster Tipp: kurz fassen (0.2 s), loslassen
        Assert.Equal(ZweiPunkteEreignis.Gefasst, ZweiPunkte.Schritt(z, null, Hd(p.Drehen), p, null, null, Kopf, Bezug, null, 1.0));
        ZweiPunkte.Schritt(z, null, Hd(p.Drehen), p, null, null, Kopf, Bezug, null, 1.1);
        Assert.Equal(ZweiPunkteEreignis.Losgelassen, ZweiPunkte.Schritt(z, null, Hd(p.Drehen, false), p, null, null, Kopf, Bezug, null, 1.2));
        Assert.NotNull(z.Lage.Kippung);
        // zweiter Tipp gleich danach: Neigung 0
        Assert.Equal(ZweiPunkteEreignis.NeigungZurueck, ZweiPunkte.Schritt(z, null, Hd(p.Drehen), p, null, null, Kopf, Bezug, null, 1.45));
        Assert.Null(z.Lage.Kippung);
        Nah(z.Neigung, 0, 1e-12);
        // zu langsam (Pause 0.8 s) bzw. zu lang gehalten: kein Doppeltipp
        var z2 = Fuehren(start, p0 => Gehoben(p0, 33, start), loslassen: true);
        var q = ZweiPunkte.Punkte(z2.Lage, Bezug, null);
        ZweiPunkte.Schritt(z2, null, Hd(q.Drehen), q, null, null, Kopf, Bezug, null, 1.0);
        ZweiPunkte.Schritt(z2, null, Hd(q.Drehen, false), q, null, null, Kopf, Bezug, null, 1.2);
        Assert.Equal(ZweiPunkteEreignis.Gefasst, ZweiPunkte.Schritt(z2, null, Hd(q.Drehen), q, null, null, Kopf, Bezug, null, 2.0));
        Assert.NotNull(z2.Lage.Kippung);
        ZweiPunkte.Schritt(z2, null, Hd(q.Drehen, false), q, null, null, Kopf, Bezug, null, 3.0);   // 1 s gehalten
        Assert.Equal(ZweiPunkteEreignis.Gefasst, ZweiPunkte.Schritt(z2, null, Hd(q.Drehen), q, null, null, Kopf, Bezug, null, 3.2));
        Assert.NotNull(z2.Lage.Kippung);
        // ohne Zeit (alte Aufrufer) nie
        var z3 = Fuehren(start, p0 => Gehoben(p0, 33, start), loslassen: true);
        var r = ZweiPunkte.Punkte(z3.Lage, Bezug, null);
        ZweiPunkte.Schritt(z3, null, Hd(r.Drehen), r, null, null, Kopf, Bezug, null);
        ZweiPunkte.Schritt(z3, null, Hd(r.Drehen, false), r, null, null, Kopf, Bezug, null);
        Assert.Equal(ZweiPunkteEreignis.Gefasst, ZweiPunkte.Schritt(z3, null, Hd(r.Drehen), r, null, null, Kopf, Bezug, null));
        // Zurücksetzen (neue Startlage) hat keine Neigung
        Assert.Null(ZweiPunkte.Neu(new SitzungLage(50, 0, 0, 0)).Lage.Kippung);
    }

    [Fact]
    public void Symbole_blenden_nach_der_Wahl_aus_nach_dem_Schliessen_kurz_gesperrt_keine_Bodenplatte()
    {
        var e = XrBedienEinstellungen.Vorgabe;
        Assert.Equal(Varianten.SymbolwahlAusblenden, e.Symbole.NachWahl);
        Assert.Equal(Varianten.SymbolwahlAusblenden, Varianten.Bekannt["symbolwahl"][0]);
        Assert.Equal(0.5, e.Symbole.SperreSchliessenS);
        Assert.Equal(1.0, e.MitStellschraube("symbole.sperre_schliessen_s", W("1"))!.Symbole.SperreSchliessenS);
        Assert.Null(e.MitStellschraube("symbole.sperre_schliessen_s", W("5")));
        Assert.Equal(Varianten.SymbolwahlStehen, e.MitStellschraube("labor.variante.symbolwahl", W("\"stehen\""))!.Symbole.NachWahl);
        // die Sperre: eine Hand, die beim Schliessen in ein Symbol gerät, wählt nichts; danach wieder
        var kugel = new BeruehrKugel("etagen", V(0, 1.2, -0.6), 0.04);
        var hand = Geisterhand.VorlageFlach(V(0, 1.2, -0.57), V(0, 0, -1), V(0, 1, 0), links: false);
        var z = new HandBeruehrZustand();
        z.Sperren(0.5);
        Assert.True(z.IstGesperrt(0.4));
        Assert.Null(HandBeruehrung.Schritt(z, 0.4, [null, hand], [kugel], false, e.Symbole).Gewaehlt);
        Assert.Null(HandBeruehrung.Schritt(z, 0.6, [null, null], [kugel], false, e.Symbole).Gewaehlt);
        Assert.False(z.IstGesperrt(0.6));
        Assert.Equal("etagen", HandBeruehrung.Schritt(z, 0.7, [null, hand], [kugel], false, e.Symbole).Gewaehlt);
        // keine Bodenplatte: höchstens ein Umriss, Vorgabe aus
        Assert.False(e.Umriss);
        Assert.True(e.MitStellschraube("sitzung.umriss", W("true"))!.Umriss);
        foreach (var k in new[] { "sitzung.kipp_max_grad", "symbole.sperre_schliessen_s", "sitzung.umriss" }) Assert.True(XrBedienEinstellungen.Kennt(k), k);
    }
}
