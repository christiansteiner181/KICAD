// Quest App V1h (Brillentest Christian 25.10.2026): die live gelockerten Werte der Wischgesten als Vorgabe («Ja, das Wegwischen geht jetzt gut, übernimm die Werte als
// Vorgabe.»), Christians Regeln für Striche nach aussen («rechts nach links gesten mit der linken hand am linken bildrand nicht beachten. ebenso rechte hand links nach
// rechts gesten am rechten rand nicht beachten.» – «oder im normbereich der jeweiligen hand.») und die Gegenregel «unter dem Blick» gegen die Fehlauslösungen, die mit den
// lockereren Werten zunehmen. Alle Daten erfunden; Zufall mit fester Saat.

using System.Collections.Concurrent;
using System.Text.Json;
using Hlks.Xr.Bedienung;
using Hlks.Xr.Desktop;
using Hlks.Xr.Gesten;
using Xunit.Abstractions;
using G = Hlks.Xr.Tests.GestenGenerator;
using static Hlks.Xr.Tests.T;

namespace Hlks.Xr.Tests;

public class V1hTests(ITestOutputHelper aus)
{
    static readonly double Inf = double.PositiveInfinity;
    static readonly Vec3 VornHoch = G.N(0, 0.3, 1);

    /// <summary>V1g: Werte und Regeln vor V1h (Anstoss 6 cm, 0.5 m/s; Handfläche 50°, Richtung 35°; ohne Rand, Körpermitte und «unter dem Blick»).</summary>
    internal static readonly AnstossEinstellungen AnstossV1g = AnstossEinstellungen.Vorgabe with { WegM = 0.06, TempoMs = 0.5, RandGrad = 90, MitteToleranzM = Inf, UntenGrad = 90 };
    internal static readonly V1fTests.Satz SatzV1g = new("V1g", AnstossV1g, HebeEinstellungen.Vorgabe with { Anstoss = AnstossV1g },
        WegwischEinstellungen.Vorgabe with { HandflaecheGrad = 50, RichtungGrad = 35 });
    /// <summary>V1h-Werte ohne die neuen Regeln (nur zum Vergleich).</summary>
    static readonly AnstossEinstellungen OhneRegeln = AnstossEinstellungen.Vorgabe with { RandGrad = 90, MitteToleranzM = Inf, UntenGrad = 90 };
    static readonly V1fTests.Satz SatzOhneRegeln = new("V1h ohne Regeln", OhneRegeln, HebeEinstellungen.Vorgabe with { Anstoss = OhneRegeln });
    static readonly V1fTests.Satz SatzV1h = new("V1h", AnstossEinstellungen.Vorgabe, HebeEinstellungen.Vorgabe);

    static JsonElement W(string j) => JsonDocument.Parse(j).RootElement.Clone();

    // ================================================================================================ Vorgaben

    [Fact]
    public void Vorgaben_des_Brillentests_in_Code_und_Katalog_fuer_alle_drei_Gesten()
    {
        var b = XrBedienEinstellungen.Vorgabe;
        Assert.Equal(0.045, b.Anstoss.WegM);
        Assert.Equal(0.35, b.Anstoss.TempoMs);
        Assert.Equal(60, b.Wegwischen.HandflaecheGrad);
        Assert.Equal(45, b.Wegwischen.RichtungGrad);
        Assert.Equal(25, b.Anstoss.RandGrad);
        Assert.Equal(0.05, b.Anstoss.MitteToleranzM);
        Assert.Equal(45, b.Anstoss.UntenGrad);
        // dasselbe Paar für Hebegeste, Wegwischen und Hineinziehen; die Handfläche des Hineinziehens wie beim Wegwischen
        Assert.Equal(b.Anstoss, b.Hebe.Anstoss);
        var hinein = DesktopEinstellungen.Vorgabe.HineinMit(b.Wegwischen);
        Assert.Equal(b.Anstoss, hinein.Anstoss);
        Assert.Equal(60, hinein.HandflaecheGrad);
        Assert.Equal(60, DesktopEinstellungen.Vorgabe.Hinein.HandflaecheGrad);
        // der Katalog nennt dieselben Vorgaben
        using var d = JsonDocument.Parse(File.ReadAllText(Path.Combine(Repo(), "kataloge", "xr_stellschrauben.json")));
        double Vorgabe(string k) => d.RootElement.GetProperty("stellschrauben").EnumerateArray().Single(s => s.GetProperty("schluessel").GetString() == k).GetProperty("vorgabe").GetDouble();
        foreach (var (k, v) in new (string, double)[] { ("gesten.anstoss_weg_m", 0.045), ("gesten.anstoss_tempo_ms", 0.35), ("wegwischen.handflaeche_grad", 60), ("wegwischen.richtung_grad", 45),
                     ("gesten.randwinkel_grad", 25), ("gesten.mitte_toleranz_m", 0.05), ("gesten.unten_grad", 45) })
            Assert.Equal(v, Vorgabe(k));
        // die neuen Stellschrauben: gelten für alle drei Gesten, Grenzen, null = Vorgabe
        var n = b.MitStellschraube("gesten.randwinkel_grad", W("30"))!;
        Assert.Equal(30, n.Hebe.Anstoss.RandGrad);
        Assert.Equal(30, n.Wegwischen.Anstoss.RandGrad);
        Assert.Equal(30, DesktopEinstellungen.Vorgabe.HineinMit(n.Wegwischen).Anstoss.RandGrad);
        Assert.Equal(25, n.MitStellschraube("gesten.randwinkel_grad", W("null"))!.Anstoss.RandGrad);
        Assert.Null(b.MitStellschraube("gesten.randwinkel_grad", W("95")));
        Assert.Equal(0.1, b.MitStellschraube("gesten.mitte_toleranz_m", W("0.1"))!.Anstoss.MitteToleranzM);
        Assert.Null(b.MitStellschraube("gesten.mitte_toleranz_m", W("-0.01")));
        Assert.Equal(60, b.MitStellschraube("gesten.unten_grad", W("60"))!.Wegwischen.Anstoss.UntenGrad);
        Assert.Null(b.MitStellschraube("gesten.unten_grad", W("5")));
        foreach (var k in new[] { "gesten.randwinkel_grad", "gesten.mitte_toleranz_m", "gesten.unten_grad" }) Assert.True(XrBedienEinstellungen.Kennt(k), k);
        // die Werte bis V1g bleiben als Satz für den Vergleich
        Assert.Equal((0.06, 0.5), (AnstossEinstellungen.V1e.WegM, AnstossEinstellungen.V1e.TempoMs));
    }

    // ================================================================================================ Rand des Blicks und Körpermitte (Regel)

    static readonly Vec3 Kopf0 = V(0, 1.6, 0);
    const double Blick0 = Math.PI / 2;   // Blick nach −z

    /// <summary>Punkt x m rechts und z m vor dem Kopf (im Blick Blick0), y m darunter (negativ).</summary>
    static Vec3 P(double x, double z, double y = -0.25) => Kopf0 + HebeGeste.Rechts(Blick0) * x + new Vec3(Math.Cos(Blick0), 0, -Math.Sin(Blick0)) * z + new Vec3(0, y, 0);

    [Fact]
    public void Rand_des_Blicks_und_Koerpermitte_gelten_nur_fuer_Striche_nach_aussen_beider_Haende()
    {
        var e = AnstossEinstellungen.Vorgabe;
        bool Links(Vec3 p, double? koerper = null) => Anstoss.AussenGesperrt(p, true, Kopf0, Blick0, e, koerper);
        bool Rechts(Vec3 p, double? koerper = null) => Anstoss.AussenGesperrt(p, false, Kopf0, Blick0, e, koerper);
        // Winkel gegen den Blick: rechts positiv
        Nah(Anstoss.SeitenWinkel(P(0.4 * Math.Tan(30 * Grad), 0.4), Kopf0, Blick0), 30, 1e-9);
        Nah(Anstoss.SeitenWinkel(P(-0.4 * Math.Tan(30 * Grad), 0.4), Kopf0, Blick0), -30, 1e-9);
        // Rand: der Körper ist 40° nach links gedreht (der Kopf schaut über die rechte Schulter) – die Hand 30° links vom Blick liegt rechts der Körpermitte; es sperrt
        // nur der Rand. 20° links vom Blick: frei
        var koerperLinks = Blick0 + 40 * Grad;
        var rand30L = P(-0.4 * Math.Tan(30 * Grad), 0.4);
        Assert.True((rand30L - Kopf0).Skalar(HebeGeste.Rechts(koerperLinks)) > e.MitteToleranzM);
        Assert.True(Links(rand30L, koerperLinks));
        Assert.False(Links(P(-0.4 * Math.Tan(20 * Grad), 0.4), koerperLinks));
        Assert.False(Anstoss.AussenGesperrt(rand30L, true, Kopf0, Blick0, e with { RandGrad = 90 }, koerperLinks));
        // gespiegelt für die rechte Hand (Körper 40° nach rechts)
        var koerperRechts = Blick0 - 40 * Grad;
        Assert.True(Rechts(P(0.4 * Math.Tan(30 * Grad), 0.4), koerperRechts));
        Assert.False(Rechts(P(0.4 * Math.Tan(20 * Grad), 0.4), koerperRechts));
        // Körpermitte (Körper = Blick): eigene Seite gesperrt (über 5 cm), Mitte und Gegenseite frei – beide Hände
        Assert.True(Links(P(-0.10, 0.4)));
        Assert.False(Links(P(-0.04, 0.4)));
        Assert.False(Links(P(0, 0.4)));
        Assert.False(Links(P(0.15, 0.4)));
        Assert.True(Rechts(P(0.10, 0.4)));
        Assert.False(Rechts(P(0.04, 0.4)));
        Assert.False(Rechts(P(0, 0.4)));
        Assert.False(Rechts(P(-0.15, 0.4)));
        // die Körpermitte folgt dem Körper, nicht dem Blick: Kopf 40° nach rechts gedreht, die linke Hand 10 cm links im Blick liegt rechts der Körpermitte
        Assert.False(Links(P(-0.10, 0.4), koerperLinks));
        // ohne Kopf bzw. ohne beide Regeln: nie
        Assert.False(Anstoss.AussenGesperrt(P(-0.3, 0.4), true, null, Blick0, e));
        Assert.False(Anstoss.AussenGesperrt(P(-0.3, 0.4), true, Kopf0, Blick0, e with { RandGrad = 90, MitteToleranzM = Inf }));
    }

    [Fact]
    public void Koerperrichtung_folgt_dem_Blick_traege()
    {
        var k = new KoerperRichtung();
        Assert.Null(k.Gieren);
        Nah(k.Schritt(0.1, Blick0), Blick0);
        // der Kopf dreht 40° nach links: nach 0.1 s ist der Körper erst gut 4° nach, nach 3 s fast ganz
        Nah(k.Schritt(0.1, Blick0 + 40 * Grad), Blick0 + 40 * Grad * (1 - Math.Exp(-0.1 / KoerperRichtung.KoerperS)), 1e-9);
        for (int i = 0; i < 29; i++) k.Schritt(0.1, Blick0 + 40 * Grad);
        Assert.InRange((k.Gieren!.Value - Blick0) / Grad, 37.5, 40);
        // über ±180° hinweg auf dem kürzeren Weg
        var w = new KoerperRichtung();
        w.Schritt(0, Math.PI - 5 * Grad);
        var g = w.Schritt(10, -Math.PI + 5 * Grad);
        Nah(Wd(g, -Math.PI + 5 * Grad), 0, 1e-3);
    }

    // ================================================================================================ Rand und Körpermitte (Gesten)

    /// <summary>
    /// Wegwischen und Hineinziehen über eine Aufnahme wie in der App (Kopf, Körperrichtung = Blick des Generators, nickenGrad = Neigung des Blicks), mit dem Satz e.
    /// </summary>
    static (int Weg, int Hinein) Wische(Aufnahme a, AnstossEinstellungen? e = null, double nickenGrad = 0)
    {
        var w = WegwischEinstellungen.Vorgabe with { Anstoss = e ?? AnstossEinstellungen.Vorgabe };
        var g = new DesktopGesten(() => DesktopEinstellungen.Vorgabe, null, () => w);
        var wz = new WegwischZustand();
        int weg = 0, hinein = 0;
        foreach (var b in a.Bilder)
        {
            var r = b.Rechts.Verfolgt ? b.Rechts.GelenkeMitNamen() : null;
            var l = b.Links.Verfolgt ? b.Links.GelenkeMitNamen() : null;
            var ruht = g.WegwischenRuht(b.T);
            var ws = Wegwischen.Schritt(wz, b.T, ruht ? null : HandBeobachtung.Aus(r, "right"), ruht ? null : HandBeobachtung.Aus(l, "left"), b.Gieren, w, b.Kopf, b.Gieren,
                nickenGrad * Grad);
            if (ws.Ereignis) { weg++; g.NachWegwischen(b.T); }
            if (g.Schritt(new DesktopHaende(b.T, r, l, b.Gieren, b.Kopf, b.Gieren, nickenGrad * Grad), true).Ausgeloest) hinein++;
        }
        return (weg, hinein);
    }

    /// <summary>Ein kurzer Anstoss (8 cm in 0.15 s) der Hand quer zum Körper: Beginn x (m rechts der Körpermitte), Richtung dx (−1 nach links, +1 nach rechts), Höhe y und Abstand z.</summary>
    static Aufnahme Strich(bool linkeHand, double x, double dx, double y = -0.25, double z = 0.4, double gieren = 0, int saat = 3)
    {
        // Handfläche in Wischrichtung (die linke Hand darf seitlich in beide Richtungen zeigen; das Hineinziehen will die Handfläche nach links bzw. – links – nach rechts)
        var normale = linkeHand ? G.V(1, 0, 0) : G.V(-1, 0, 0);
        Haltung H(double u) => new(G.V(x + dx * 0.08 * u, y, z), VornHoch, normale, G.Kr(0.1));
        GestenBewegung b = linkeHand ? u => (H(u), null) : u => (null, H(u));
        return G.Erzeugen(b, new Vorfuehrung { DauerS = 0.15, RuheVorS = 0.4, RuheNachS = 0.5, GierenGrad = gieren, Saat = saat });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(120)]
    public void Linke_Hand_nach_links_zaehlt_aus_der_Mitte_und_von_rechts_nicht_von_der_eigenen_Seite_und_nicht_am_Rand(double gieren)
    {
        // Wegwischen mit der linken Hand (rechts nach links)
        Assert.Equal((1, 0), Wische(Strich(true, 0.0, -1, gieren: gieren)));        // aus der Mitte
        Assert.Equal((1, 0), Wische(Strich(true, -0.04, -1, gieren: gieren)));      // knapp links der Mitte (Toleranz 5 cm)
        Assert.Equal((1, 0), Wische(Strich(true, 0.12, -1, gieren: gieren)));       // von der Gegenseite
        Assert.Equal((0, 0), Wische(Strich(true, -0.10, -1, gieren: gieren)));      // eigene Seite («im normbereich der jeweiligen hand»)
        Assert.Equal((1, 0), Wische(Strich(true, -0.10, -1, gieren: gieren), AnstossEinstellungen.Vorgabe with { MitteToleranzM = Inf }));   // Gegenprobe ohne die Regel
        // am linken Rand (37° links vom Blick): auch ohne Körpermitte nicht, ohne beide Regeln schon
        Assert.Equal((0, 0), Wische(Strich(true, -0.30, -1, gieren: gieren), AnstossEinstellungen.Vorgabe with { MitteToleranzM = Inf }));
        Assert.Equal((1, 0), Wische(Strich(true, -0.30, -1, gieren: gieren), AnstossEinstellungen.Vorgabe with { MitteToleranzM = Inf, RandGrad = 90 }));
        // nach innen (links nach rechts: Hineinziehen) gilt die Regel nicht – auch nicht vom linken Rand
        Assert.Equal((0, 1), Wische(Strich(true, -0.30, 1, gieren: gieren)));
        Assert.Equal((0, 1), Wische(Strich(true, -0.12, 1, gieren: gieren)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-90)]
    public void Rechte_Hand_nach_rechts_zaehlt_aus_der_Mitte_und_von_links_nicht_von_der_eigenen_Seite_und_nicht_am_Rand(double gieren)
    {
        // Hineinziehen mit der rechten Hand (links nach rechts)
        Assert.Equal((0, 1), Wische(Strich(false, 0.0, 1, gieren: gieren)));
        Assert.Equal((0, 1), Wische(Strich(false, 0.04, 1, gieren: gieren)));
        Assert.Equal((0, 1), Wische(Strich(false, -0.12, 1, gieren: gieren)));
        Assert.Equal((0, 0), Wische(Strich(false, 0.10, 1, gieren: gieren)));
        Assert.Equal((0, 1), Wische(Strich(false, 0.10, 1, gieren: gieren), AnstossEinstellungen.Vorgabe with { MitteToleranzM = Inf }));
        Assert.Equal((0, 0), Wische(Strich(false, 0.30, 1, gieren: gieren), AnstossEinstellungen.Vorgabe with { MitteToleranzM = Inf }));
        Assert.Equal((0, 1), Wische(Strich(false, 0.30, 1, gieren: gieren), AnstossEinstellungen.Vorgabe with { MitteToleranzM = Inf, RandGrad = 90 }));
        // nach innen (rechts nach links: Wegwischen) wie bisher – auch vom rechten Rand
        Assert.Equal((1, 0), Wische(Strich(false, 0.30, -1, gieren: gieren)));
        Assert.Equal((1, 0), Wische(Strich(false, 0.12, -1, gieren: gieren)));
    }

    [Fact]
    public void Die_ganze_Bewegung_nach_aussen_zaehlt_ebenso_nicht_von_der_eigenen_Seite()
    {
        // 30 cm gemächlich (ohne Anstoss, wie bis V1d): die linke Hand von der eigenen Seite nach links bzw. die rechte nach rechts zählt nicht, aus der Mitte schon
        var ohneAnstoss = AnstossEinstellungen.Aus with { RandGrad = 25, MitteToleranzM = 0.05, UntenGrad = 45 };
        Aufnahme Lang(bool linkeHand, double x, double dx)
        {
            var normale = linkeHand ? G.V(1, 0, 0) : G.V(-1, 0, 0);
            Haltung H(double u) => new(G.V(x + dx * 0.3 * u, -0.25, 0.4), VornHoch, normale, G.Kr(0.1));
            GestenBewegung b = linkeHand ? u => (H(u), null) : u => (null, H(u));
            return G.Erzeugen(b, new Vorfuehrung { DauerS = 0.45, RuheVorS = 0.4, RuheNachS = 0.4, Saat = 5 });
        }
        Assert.Equal((1, 0), Wische(Lang(true, 0.05, -1), ohneAnstoss));
        Assert.Equal((0, 0), Wische(Lang(true, -0.08, -1), ohneAnstoss));
        Assert.Equal((0, 1), Wische(Lang(false, -0.05, 1), ohneAnstoss));
        Assert.Equal((0, 0), Wische(Lang(false, 0.08, 1), ohneAnstoss));
    }

    // ================================================================================================ Unter dem Blick

    [Fact]
    public void Ein_Wisch_der_unter_dem_Blick_beginnt_zaehlt_nicht_die_Hebegeste_schon()
    {
        var e = AnstossEinstellungen.Vorgabe;
        // die ruhende Hand der Tests (20 cm rechts, 45 cm unter den Augen, 30 cm vor): rund 51° unter dem waagrechten Blick; ein Wisch auf Brusthöhe rund 30°
        Nah(Anstoss.UnterBlick(P(0.2, 0.3, -0.45), Kopf0, 0), Math.Atan2(0.45, Math.Sqrt(0.2 * 0.2 + 0.3 * 0.3)) / Grad, 1e-9);
        Assert.InRange(Anstoss.UnterBlick(P(0.2, 0.3, -0.45), Kopf0, 0), 51, 52);
        Assert.InRange(Anstoss.UnterBlick(P(0.15, 0.4, -0.25), Kopf0, 0), 30, 31);
        Assert.True(Anstoss.UntenGesperrt(P(0.2, 0.3, -0.45), Kopf0, 0, e));
        Assert.False(Anstoss.UntenGesperrt(P(0.15, 0.4, -0.25), Kopf0, 0, e));
        // der Blick ist 20° nach unten geneigt (die Hand im Bild): frei; ohne Kopf bzw. mit 90° nie
        Assert.False(Anstoss.UntenGesperrt(P(0.2, 0.3, -0.45), Kopf0, -20 * Grad, e));
        Assert.False(Anstoss.UntenGesperrt(P(0.2, 0.3, -0.45), null, 0, e));
        Assert.False(Anstoss.UntenGesperrt(P(0.2, 0.3, -0.45), Kopf0, 0, e with { UntenGrad = 90 }));
        // Gesten: aus der Ruhelage am Körper weder Wegwischen (rechte Hand) noch Hineinziehen (linke Hand); mit geneigtem Blick bzw. auf Brusthöhe schon
        Assert.Equal((0, 0), Wische(Strich(false, 0.2, -1, y: -0.45, z: 0.3)));
        Assert.Equal((1, 0), Wische(Strich(false, 0.2, -1, y: -0.45, z: 0.3), nickenGrad: -20));
        Assert.Equal((1, 0), Wische(Strich(false, 0.2, -1, y: -0.45, z: 0.3), AnstossEinstellungen.Vorgabe with { UntenGrad = 90 }));
        Assert.Equal((0, 0), Wische(Strich(true, -0.2, 1, y: -0.45, z: 0.3)));
        Assert.Equal((0, 1), Wische(Strich(true, -0.2, 1, y: -0.45, z: 0.3), nickenGrad: -20));
        Assert.Equal((1, 0), Wische(Strich(false, 0.15, -1)));
        // die Hebegeste beginnt unten (Hände 50 cm unter den Augen, rund 55°) und zählt weiter
        var hebe = G.Erzeugen(u => (new Haltung(G.V(-0.2, -0.5 + 0.08 * u, 0.35), G.V(0, 0, 1), G.V(0, 1, 0), G.Kr(0.1)),
            new Haltung(G.V(0.2, -0.5 + 0.08 * u, 0.35), G.V(0, 0, 1), G.V(0, 1, 0), G.Kr(0.1))), new Vorfuehrung { DauerS = 0.15, RuheVorS = 0.3, RuheNachS = 0.5 });
        Assert.Equal(1, V1fTests.Abspielen(hebe, SatzV1h).Hebe);
    }

    // ================================================================================================ Absichtliche Wische vor Symbolfeld und Tafel

    static readonly string[] SymbolIds = ["etagen", "ort", "messen", "marker", "status", "ebenen", "desktop"];

    /// <summary>Wie viele Startpunkte frei sind: rechte Hand nach links (Wegwischen), linke Hand nach links (Wegwischen), rechte Hand nach rechts (Hineinziehen).</summary>
    static (int RechtsWeg, int LinksWeg, int RechtsHinein) Frei(IEnumerable<Vec3> punkte, Vec3 kopf, double nickenGrad)
    {
        var e = AnstossEinstellungen.Vorgabe;
        var n = nickenGrad * Grad;
        int a = 0, b = 0, c = 0;
        foreach (var p in punkte)
        {
            var unten = Anstoss.UntenGesperrt(p, kopf, n, e);
            if (!unten) a++;
            if (!unten && !Anstoss.AussenGesperrt(p, true, kopf, Blick0, e)) b++;
            if (!unten && !Anstoss.AussenGesperrt(p, false, kopf, Blick0, e)) c++;
        }
        return (a, b, c);
    }

    [Fact]
    public void Absichtliche_Wische_vor_Symbolfeld_und_Tafel_bleiben_frei_ausser_der_linken_Hand_von_links()
    {
        var se = new SymbolEinstellungen();
        var m = GlasMasse.Fuer(new AppEinstellungen());
        // stehend (Begehen): Feld 0.5 m vor den Augen, Mitte 22 cm darunter, 60 cm hoch
        var stehend = AppSymbole.Plaetze(SymbolIds, Kopf0, Blick0, se, m).Select(x => x.Lage.Mitte).ToList();
        // am Sitzungstisch (Augen 1.2 m, Platte 0.75 m): Feld 52 cm unter den Augen, über der Platte gestaucht
        var kopfSitz = V(0, 1.2, 0);
        var tisch = AppSymbole.Plaetze(SymbolIds, kopfSitz, Blick0, se, m, AppSymbole.Untergrenze(0.75, se, m)).Select(x => x.Lage.Mitte).ToList();
        // die Tafel (Glas-App): Mitte, Ecken
        var tm = Tafel.Platz(Kopf0, Blick0, new AppEinstellungen(), TafelEinstellungen.Vorgabe);
        var re = HebeGeste.Rechts(Blick0);
        var tafel = new[] { tm, tm + re * (m.FensterB / 2), tm - re * (m.FensterB / 2), tm + new Vec3(0, -m.FensterH / 2, 0), tm + re * (m.FensterB / 2) + new Vec3(0, -m.FensterH / 2, 0),
            tm - re * (m.FensterB / 2) + new Vec3(0, -m.FensterH / 2, 0) };
        foreach (var (name, punkte, kopf) in new[] { ("Symbolfeld stehend", stehend, Kopf0), ("Symbolfeld am Tisch", tisch, kopfSitz), ("Tafel", tafel.ToList(), Kopf0) })
            foreach (var nicken in new[] { 0.0, -10 })
            {
                var f = Frei(punkte, kopf, nicken);
                var tief = punkte.Max(p => Anstoss.UnterBlick(p, kopf, nicken * Grad));
                aus.WriteLine($"{name}, Blick {nicken:0}° geneigt: {punkte.Count} Startpunkte, tiefster {tief:0.0}° unter dem Blick; frei rechte Hand nach links {f.RechtsWeg}, " +
                    $"linke Hand nach links {f.LinksWeg}, rechte Hand nach rechts {f.RechtsHinein}");
            }
        // rechte Hand nach links (die übliche Wischgeste): am Tisch und vor der Tafel überall; stehend bei waagrechtem Blick bis auf 2 Symbole der untersten Reihe (bis 47°),
        // mit 10° Neigung überall
        Assert.Equal(tisch.Count, Frei(tisch, kopfSitz, 0).RechtsWeg);
        Assert.Equal(tafel.Length, Frei(tafel, Kopf0, 0).RechtsWeg);
        Assert.Equal(stehend.Count, Frei(stehend, Kopf0, -10).RechtsWeg);
        Assert.InRange(Frei(stehend, Kopf0, 0).RechtsWeg, stehend.Count - 2, stehend.Count);
        // linke Hand nach links: nicht aus der linken Spalte des Felds bzw. von der linken Hälfte der Tafel (Christian: «im normbereich der jeweiligen hand»)
        Assert.True(Frei(stehend, Kopf0, -10).LinksWeg < stehend.Count);
        Assert.True(Frei(stehend, Kopf0, -10).LinksWeg >= stehend.Count(p => (p - Kopf0).Skalar(re) >= -0.05));
        Assert.Equal(tafel.Count(p => (p - Kopf0).Skalar(re) >= -0.05), Frei(tafel, Kopf0, 0).LinksWeg);
    }

    // ================================================================================================ Fehlauslösungen und leichtes Auslösen, vorher und nachher

    /// <summary>Leichte Anstösse (4.5–6.5 cm in 0.10–0.17 s, 0.38–0.5 m/s im Mittel) aller drei Gesten, beide Hände aus der Mitte bzw. von der Gegenseite.</summary>
    internal static IEnumerable<(string Art, Aufnahme A)> LeichteAnstoesse()
    {
        var faelle = new (double Weg, double Dauer)[] { (0.05, 0.13), (0.05, 0.11), (0.055, 0.14), (0.06, 0.15), (0.05, 0.1), (0.065, 0.17) };
        int saat = 61;
        foreach (var (weg, dauer) in faelle)
            foreach (var gieren in new[] { 0.0, 120 })
            {
                var v = new Vorfuehrung { DauerS = dauer, RuheVorS = 0.3, RuheNachS = 0.5, GierenGrad = gieren, BogenM = 0.01, Saat = saat++ };
                yield return ("weg", G.Erzeugen(u => (null, new Haltung(G.V(0.15 - weg * u, -0.25, 0.4), VornHoch, G.V(-1, 0, 0), G.Kr(0.1))), v));
                yield return ("weg", G.Erzeugen(u => (new Haltung(G.V(0.0 - weg * u, -0.25, 0.4), VornHoch, G.V(1, 0, 0), G.Kr(0.1)), null), v));
                yield return ("hinein", G.Erzeugen(u => (null, new Haltung(G.V(-0.05 + weg * u, -0.25, 0.4), VornHoch, G.V(-1, 0, 0), G.Kr(0.1))), v));
                yield return ("hinein", G.Erzeugen(u => (new Haltung(G.V(-0.25 + weg * u, -0.25, 0.4), VornHoch, G.V(1, 0, 0), G.Kr(0.1)), null), v));
                yield return ("hebe", G.Erzeugen(u => (new Haltung(G.V(-0.2, -0.5 + weg * u, 0.35), G.V(0, 0, 1), G.V(0, 1, 0), G.Kr(0.1)),
                    new Haltung(G.V(0.2, -0.5 + weg * u, 0.35), G.V(0, 0, 1), G.V(0, 1, 0), G.Kr(0.1))), v));
            }
    }

    static int Erkannt(IEnumerable<(string Art, Aufnahme A)> faelle, V1fTests.Satz s, out int n)
    {
        int ok = 0;
        n = 0;
        foreach (var (art, a) in faelle)
        {
            n++;
            var r = V1fTests.Abspielen(a, s);
            if ((art == "weg" ? r.Weg : art == "hinein" ? r.Hinein : r.Hebe) == 1 && r.Hebe + r.Weg + r.Hinein == 1) ok++;
        }
        return ok;
    }

    [Fact]
    public void Leichte_Anstoesse_loesen_jetzt_aus_die_deutlichen_weiterhin()
    {
        var leichtV1g = Erkannt(LeichteAnstoesse(), SatzV1g, out var n);
        var leichtV1h = Erkannt(LeichteAnstoesse(), SatzV1h, out _);
        var deutlichV1g = Erkannt(V1eTests.Anstoesse(), SatzV1g, out var nd);
        var deutlichV1h = Erkannt(V1eTests.Anstoesse(), SatzV1h, out _);
        aus.WriteLine($"leichte Anstösse (4.5–6.5 cm, 0.38–0.5 m/s): V1g {leichtV1g}/{n}, V1h {leichtV1h}/{n}; deutliche (V1e, 6.5–12 cm): V1g {deutlichV1g}/{nd}, V1h {deutlichV1h}/{nd}");
        Assert.True(leichtV1h >= n * 95 / 100, $"{leichtV1h}/{n}");
        Assert.True(leichtV1h > leichtV1g + n / 2);
        Assert.True(deutlichV1h >= nd * 95 / 100, $"{deutlichV1h}/{nd}");
    }

    [Fact]
    public void Fehlausloesungen_vorher_V1g_nachher_V1h_nehmen_mit_den_Regeln_nicht_zu()
    {
        // je 900 Zufallsbewegungen mit drei Saaten, 10 × 3 s ruhige Hände, 84 Testgesten des Labors (V1fTests.Fehlausloesungen, verdrahtet wie in der App mit Kopf und
        // Körperrichtung; Blick waagrecht). Gemessen am 25.10.2026 (Saaten 4000, 20000, 40000; Hebegeste/Wegwischen/Hineinziehen): V1g 0/5/3, V1h-Werte ohne Regeln
        // 0/34/22, nur mit Rand und Körpermitte 0/29/2, mit «unter dem Blick» 0/4/0. Der Vergleich ohne Regeln läuft hier nur mit der ersten Saat (Laufzeit).
        var saaten = new[] { 4000, 20000, 40000 };
        var laeufe = saaten.SelectMany(z => new[] { (S: SatzV1g, Z: z), (S: SatzV1h, Z: z) }).Append((S: SatzOhneRegeln, Z: saaten[0])).ToList();
        var erg = new ConcurrentDictionary<(string, int), (int Hebe, int Weg, int Hinein, int Bewegungen)>();
        Parallel.ForEach(laeufe, x => erg[(x.S.Name, x.Z)] = V1fTests.Fehlausloesungen(x.S, 900, null, x.Z));   // unabhängig, parallel (Laufzeit)
        (int H, int W, int I) Summe(V1fTests.Satz s, IEnumerable<int> zs) => (zs.Sum(z => erg[(s.Name, z)].Hebe), zs.Sum(z => erg[(s.Name, z)].Weg), zs.Sum(z => erg[(s.Name, z)].Hinein));
        foreach (var s in new[] { SatzV1g, SatzV1h })
        {
            var (h, w, i) = Summe(s, saaten);
            aus.WriteLine($"{s.Name}: Hebegeste {h}, Wegwischen {w}, Hineinziehen {i} in {saaten.Sum(z => erg[(s.Name, z)].Bewegungen)} Bewegungen " +
                $"({string.Join(", ", saaten.Select(z => $"Saat {z}: {erg[(s.Name, z)].Hebe}/{erg[(s.Name, z)].Weg}/{erg[(s.Name, z)].Hinein}"))})");
        }
        var ohne = Summe(SatzOhneRegeln, [saaten[0]]);
        var vorher0 = Summe(SatzV1g, [saaten[0]]);
        aus.WriteLine($"{SatzOhneRegeln.Name}, Saat {saaten[0]}: {ohne.H}/{ohne.W}/{ohne.I} (V1g {vorher0.H}/{vorher0.W}/{vorher0.I})");
        var vorher = Summe(SatzV1g, saaten);
        var nachher = Summe(SatzV1h, saaten);
        // die lockereren Werte allein liessen Wegwischen und Hineinziehen deutlich häufiger auslösen; mit den Regeln nicht mehr als vorher
        Assert.True(ohne.W + ohne.I >= 3 * Math.Max(1, vorher0.W + vorher0.I), $"{ohne} gegen {vorher0}");
        Assert.True(nachher.H <= vorher.H, "Hebegeste");
        Assert.True(nachher.W <= vorher.W, "Wegwischen");
        Assert.True(nachher.I <= vorher.I, "Hineinziehen");
        // ruhige Hände allein: nie
        var ruhe = Enumerable.Range(0, 6).Select(s => V1fTests.Abspielen(G.Erzeugen(G.Ruhe, new Vorfuehrung { DauerS = 4, Saat = 70 + s, RauschenM = 0.008 }), SatzV1h)).ToList();
        Assert.All(ruhe, r => Assert.Equal(0, r.Hebe + r.Weg + r.Hinein));
    }
}
