// Tests der Quest App V1b-1 (Modul XR, Gestenlabor): Merkmale je Bild und Merkmalsfolge (Körperbezug, Spiegelung, Normierung, beidhändig), Datenmodell der Hand,
// Einstellungen.

using Hlks.Xr.Gesten;
using G = Hlks.Xr.Tests.GestenGenerator;

namespace Hlks.Xr.Tests;

public class GestenMerkmaleTests
{
    static readonly GestenEinstellungen E = GestenEinstellungen.Vorgabe;

    static Aufnahme Ohne(GestenBewegung b, double dauer = 0.5, double gieren = 0, bool gespiegelt = false, double groesse = 1) =>
        G.Erzeugen(b, new Vorfuehrung { DauerS = dauer, GierenGrad = gieren, Gespiegelt = gespiegelt, Groesse = groesse, RauschenM = 0, RuheVorS = 0, RuheNachS = 0 });

    static void GleicheWerte(double[] a, double[] b, double eps = 1e-9)
    {
        Assert.Equal(a.Length, b.Length);
        for (var i = 0; i < a.Length; i++) Assert.True(Math.Abs(a[i] - b[i]) < eps, $"Wert {i}: {a[i]} ≠ {b[i]}");
    }

    [Fact]
    public void Koerperdrehung_aendert_die_Merkmale_nicht()
    {
        foreach (var (b, art) in new[] { (G.WischLinks, Merkmalart.Rechts), (G.Kreis, Merkmalart.Rechts), (G.Hebe, Merkmalart.Beide), (G.Auseinander, Merkmalart.Beide) })
        {
            var a = GestenMerkmale.Folge(Ohne(b), art)!;
            foreach (var gieren in new[] { 37.0, 90, 180, -137 })
                GleicheWerte(a.Werte, GestenMerkmale.Folge(Ohne(b, gieren: gieren), art)!.Werte);
        }
    }

    [Fact]
    public void Hebegeste_ist_nach_oben_egal_wohin_man_schaut_mit_Abstand_und_Richtung_der_Haende()
    {
        foreach (var gieren in new[] { 0.0, 120, -75 })
        {
            var f = GestenMerkmale.Folge(Ohne(G.Hebe, 0.7, gieren), Merkmalart.Beide)!;
            Assert.Equal(GestenMerkmale.DimBeide, f.Dimension);
            var n = f.Stuetzpunkte - 1;
            foreach (var block in new[] { 0, 12 })
            {
                Assert.True(f.Wert(n, block + 1) > 0.99 * E.GewichtLage, "nach oben");
                Assert.True(Math.Abs(f.Wert(n, block)) < 0.01 && Math.Abs(f.Wert(n, block + 2)) < 0.01, "weder seitlich noch vor");
                Assert.True(f.Wert(n, block + GestenMerkmale.SpalteNormale + 1) > 0.99 * E.GewichtNormale, "Handfläche nach oben");
                Assert.True(Math.Abs(f.Wert(0, block + 1)) < 1e-12, "Start bei null");
            }
            T.Nah(f.Wert(n, 25), 0.4 * E.GewichtHandabstand, 1e-6, "Abstand der Hände 40 cm");
            T.Nah(f.Wert(n, 26), E.GewichtHandrichtung, 1e-6, "Richtung links → rechts");
            T.Nah(f.Wert(n, 24), E.GewichtGroesse * Math.Log(0.3 / E.BezugAusdehnungM), 1e-6, "Grösse 30 cm");
        }
    }

    [Fact]
    public void Linke_Hand_gespiegelt_gleicht_der_rechten()
    {
        foreach (var b in new[] { G.WischLinks, G.Kreis, G.Faust, G.Stoss })
        {
            var rechts = GestenMerkmale.Folge(Ohne(b), Merkmalart.Rechts)!;
            var links = Ohne(b, gespiegelt: true);
            Assert.Null(GestenMerkmale.Folge(links, Merkmalart.Rechts));   // die rechte Hand fehlt
            GleicheWerte(rechts.Werte, GestenMerkmale.Folge(links, Merkmalart.LinksGespiegelt)!.Werte, 1e-9);
            // ungespiegelt geht die linke Hand zur anderen Seite: andere Lage in x (Wisch, Kreis)
            var ungespiegelt = GestenMerkmale.Folge(links, Merkmalart.Links)!;
            var dx = Enumerable.Range(0, rechts.Stuetzpunkte).Max(s => Math.Abs(ungespiegelt.Wert(s, 0) - rechts.Wert(s, 0)));
            if (b == G.WischLinks || b == G.Kreis) Assert.True(dx > 0.5, $"ungespiegelt nur {dx}");
            else Assert.True(dx < 0.05, "Stoss und Faust gehen nicht zur Seite");
        }
    }

    [Fact]
    public void Groessere_Bewegung_hat_dieselbe_Bahn_und_nur_einen_anderen_Groessenwert()
    {
        var a = GestenMerkmale.Folge(Ohne(G.WischLinks), Merkmalart.Rechts)!;
        var b = GestenMerkmale.Folge(Ohne(G.WischLinks, groesse: 1.25), Merkmalart.Rechts)!;
        for (var s = 0; s < a.Stuetzpunkte; s++)
        {
            for (var k = 0; k < 12; k++) T.Nah(a.Wert(s, k), b.Wert(s, k), 1e-9, $"{s}/{k}");
            T.Nah(b.Wert(s, 12) - a.Wert(s, 12), E.GewichtGroesse * Math.Log(1.25), 1e-6, "Grösse");
        }
    }

    [Fact]
    public void Ruhende_Hand_bleibt_klein_trotz_Rauschen()
    {
        var a = G.Erzeugen(G.Ruhe, new Vorfuehrung { DauerS = 1, Saat = 5 });
        var f = GestenMerkmale.Folge(a, Merkmalart.Rechts)!;
        for (var s = 0; s < f.Stuetzpunkte; s++)
            for (var k = 0; k < 3; k++) Assert.True(Math.Abs(f.Wert(s, k)) < 0.4 * E.GewichtLage, $"Lage {s}/{k} = {f.Wert(s, k)}");
        T.Nah(f.Wert(0, 12), E.GewichtGroesse * Math.Log(E.MindestAusdehnungM / E.BezugAusdehnungM), 1e-9, "Grösse = Mindestausdehnung");
    }

    [Fact]
    public void Kruemmung_ist_mit_der_Handgroesse_normiert_und_Pinch_gekappt()
    {
        var g = G.Hand(HandSeite.Rechts, new Vec3(0, 1.2, -0.4), new Vec3(0, 0, -1), new Vec3(0, -1, 0), [0.4, 0.6, 0.6, 0.7, 0.8], 0);
        var gross = g.ToDictionary(x => x.Key, x => g["wrist"] + (x.Value - g["wrist"]) * 1.2);
        HandBild a = HandBild.AusGelenken(HandSeite.Rechts, g), b = HandBild.AusGelenken(HandSeite.Rechts, gross);
        T.Nah(GestenMerkmale.HandGroesse(b), 1.2 * GestenMerkmale.HandGroesse(a), 1e-12);
        var ka = GestenMerkmale.Kruemmung(a);
        var kb = GestenMerkmale.Kruemmung(b);
        for (var f = 0; f < 5; f++) T.Nah(ka[f], kb[f], 1e-9, $"Finger {f}");
        Assert.True(ka[4] > ka[1], "kleiner Finger stärker gekrümmt als der Zeigefinger");
        T.Nah(GestenMerkmale.PinchNormiert(a), Math.Min(1, a.PinchAbstand / GestenMerkmale.HandGroesse(a)), 1e-12);
        T.Nah(GestenMerkmale.PinchNormiert(b), GestenMerkmale.PinchNormiert(a), 1e-9, "Pinch normiert");
        Assert.Equal(E.PinchKappe, GestenMerkmale.PinchNormiert(a with { PinchAbstand = double.NaN }));
        Assert.Equal(0.5, GestenMerkmale.PinchNormiert(a, E with { PinchKappe = 0.5 }));
        // fehlende Gelenke: Handgrösse nach Vorgabe, Finger ohne Gelenke gestreckt
        var ohne = a with { Gelenke = [] };
        Assert.Equal(E.HandGroesseVorgabeM, GestenMerkmale.HandGroesse(ohne));
        Assert.All(GestenMerkmale.Kruemmung(ohne), k => Assert.Equal(0, k));
    }

    [Fact]
    public void Folge_ohne_noetige_Hand_ist_null_beidhaendig_braucht_beide()
    {
        var a = Ohne(G.WischLinks);
        Assert.NotNull(GestenMerkmale.Folge(a, Merkmalart.Rechts));
        Assert.Null(GestenMerkmale.Folge(a, Merkmalart.Links));
        Assert.Null(GestenMerkmale.Folge(a, Merkmalart.Beide));
        // eine Hand verloren in einem Bild
        var bilder = a.Bilder.ToList();
        bilder[10] = bilder[10] with { Rechts = HandBild.Fehlt };
        Assert.Null(GestenMerkmale.Folge(new Aufnahme(bilder), Merkmalart.Rechts));
        Assert.Null(GestenMerkmale.Folge(new Aufnahme(bilder.Take(1).ToList()), Merkmalart.Rechts));
    }

    [Fact]
    public void Arten_je_Haendigkeit()
    {
        Assert.Equal([Merkmalart.Rechts], GestenMerkmale.Arten(Haendigkeit.Rechts));
        Assert.Equal([Merkmalart.Links], GestenMerkmale.Arten(Haendigkeit.Links));
        Assert.Equal([Merkmalart.Rechts, Merkmalart.LinksGespiegelt], GestenMerkmale.Arten(Haendigkeit.Beliebig));
        Assert.Equal([Merkmalart.Beide], GestenMerkmale.Arten(Haendigkeit.Beidhaendig));
        Assert.Equal(HandSeite.Links, GestenMerkmale.Seite(Merkmalart.LinksGespiegelt));
        Assert.Null(GestenMerkmale.Seite(Merkmalart.Beide));
    }

    [Fact]
    public void Koerperbezug_rechts_oben_vorn()
    {
        var gieren = Math.PI / 2;   // Blick nach Norden (−z)
        var v = GestenMerkmale.Koerperbezug(new Vec3(1, 2, -3), gieren);
        T.Nah(v.X, 1, 1e-12, "Osten = rechts");
        T.Nah(v.Y, 2, 1e-12);
        T.Nah(v.Z, 3, 1e-12, "Norden = vorn");
    }

    [Fact]
    public void HandBild_aus_Gelenken_mit_Namen_wie_HandGelenke()
    {
        var g = G.Hand(HandSeite.Links, new Vec3(0, 1, 0), new Vec3(1, 0, 0), new Vec3(0, 1, 0), G.Kr(0), 0);
        var h = HandBild.AusGelenken(HandSeite.Links, g);
        Assert.True(h.Verfolgt);
        Assert.Equal(25, h.Gelenke.Count);
        Assert.Equal(0, HandBild.Index("wrist"));
        Assert.Equal(-1, HandBild.Index("ellbogen"));
        Assert.Equal(g["index-finger-tip"], h.Gelenk("index-finger-tip"));
        Assert.Equal(g.Count, h.GelenkeMitNamen().Count);
        T.Nah(h.PinchAbstand, g["thumb-tip"].Abstand(g["index-finger-tip"]), 1e-12);
        Assert.True(h.Normale.Skalar(new Vec3(0, 1, 0)) > 0.99);
        // unvollständig bzw. null: nicht verfolgt
        g.Remove("pinky-finger-metacarpal");
        Assert.False(HandBild.AusGelenken(HandSeite.Links, g).Verfolgt);
        Assert.False(HandBild.AusGelenken(HandSeite.Links, null).Verfolgt);
        Assert.False(HandBild.Fehlt.Brauchbar);
    }

    [Fact]
    public void Einstellungen_werden_geprueft()
    {
        Assert.Null(E.Pruefen());
        Assert.Equal(6, E.Band);
        Assert.NotNull((E with { Stuetzpunkte = 4 }).Pruefen());
        Assert.NotNull((E with { BandAnteil = 0 }).Pruefen());
        Assert.NotNull((E with { GewichtLage = -1 }).Pruefen());
        Assert.NotNull((E with { GewichtPinch = double.NaN }).Pruefen());
        Assert.NotNull((E with { SchwelleMin = 1, SchwelleMax = 0.5 }).Pruefen());
        Assert.NotNull((E with { MaxDauerS = 3 }).Pruefen());   // länger als der Puffer
        Assert.NotNull((E with { LaengenSchritt = 1 }).Pruefen());
        Assert.NotNull((E with { KonfliktWarnFaktor = 0.5 }).Pruefen());
        Assert.NotNull((E with { TorLageM = 0 }).Pruefen());
        Assert.Throws<ArgumentException>(() => new GestenErkenner([], E with { Stuetzpunkte = 200 }));
        Assert.Throws<ArgumentException>(() => GestenMerkmale.Folge(Ohne(G.Stoss), Merkmalart.Rechts, E with { MindestBeispiele = 1 }));
    }
}
