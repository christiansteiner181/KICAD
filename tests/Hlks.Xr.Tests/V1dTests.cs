// Tests der Quest App V1d (Befunde des ersten Brillentests, Christian 24.10.2026): Symbolfeld höher (sitzend noch höher, am Tisch tiefer), Sperre nach der Wahl einer
// App, Tafel mittig mit trägem Nachführen, ein Griff am Sitzungstisch (Bewegen, Drehen, Zoomen mit zweiter Hand bzw. Drehknopf), Wegwischen und Hineinziehen mit
// beiden Händen, Blättern auf der Tafel, vereinfachte Unterlage am Tisch (TischStufe), räumlicher Index des Fangs (gleiche Ergebnisse wie bis V1a), Stellschrauben.

using System.Text.Json;
using Hlks.Xr.Bedienung;
using Hlks.Xr.Desktop;
using Hlks.Xr.Messen;
using Hlks.Xr.Modell;
using Hlks.Xr.Sitzung;
using Xunit.Abstractions;
using static Hlks.Xr.Tests.T;

namespace Hlks.Xr.Tests;

public class V1dTests(ITestOutputHelper aus)
{
    static readonly GlasMasse M = GlasMasse.Fuer(new AppEinstellungen());
    static readonly string[] Ids = ["status", "etagen", "messen", "marker", "ort", "ebenen"];
    const double Blick = Math.PI / 2;   // Blick nach −z
    static JsonElement W(string j) => JsonDocument.Parse(j).RootElement.Clone();

    // ================================================================================================ 1. Symbolfeld höher, sitzend höher, am Tisch tiefer

    [Fact]
    public void Symbolfeld_beim_Begehen_10_cm_hoeher_sitzend_weitere_8_cm_am_Tisch_20_cm_tiefer_als_V1a()
    {
        var e = new SymbolEinstellungen();
        Assert.Equal(0.22, e.HoeheM);
        Assert.Equal(0.08, e.HoeheSitzendM);
        Assert.Equal(0.52, e.HoeheTischM);
        Assert.Equal(0.06, e.TischAbstandM);
        var v1a = e with { HoeheM = 0.32, HoeheSitzendM = 0, HoeheTischM = 0.32 };   // Stand d5a8df6: eine Mitte überall
        var steh = V(0, 1.65, 0);
        var sitz = V(0, 1.2, 0);
        double Y(List<SymbolPlatz> p, string id) => p.Single(x => x.Id == id).Lage.Mitte.Y;
        // Begehen stehend: jedes Symbol genau 0.10 m höher; Begehen sitzend: 0.18 m höher (0.10 + 0.08)
        var alt = AppSymbole.Plaetze(Ids, steh, Blick, v1a, M);
        var neu = AppSymbole.Plaetze(Ids, steh, Blick, e, M);
        foreach (var id in Ids) Nah(Y(neu, id) - Y(alt, id), 0.10, 1e-9, id);
        var altS = AppSymbole.Plaetze(Ids, sitz, Blick, v1a, M);
        var neuS = AppSymbole.Plaetze(Ids, sitz, Blick, e, M, sitzend: true);
        foreach (var id in Ids) Nah(Y(neuS, id) - Y(altS, id), 0.18, 1e-9, id);
        Assert.True(neu.Max(x => x.Lage.Mitte.Y) <= steh.Y + 0.08 + 1e-9);      // oberste Reihe höchstens 8 cm über den Augen
        Assert.True(neuS.Max(x => x.Lage.Mitte.Y) <= sitz.Y + 0.16 + 1e-9);     // sitzend höchstens 16 cm darüber
        // am echten Tisch (Platte 0.639 m wie im Brillentest, sitzend): die oberste Reihe rund 0.20 m tiefer als in V1a; die Haltung zählt am Tisch nicht
        var ug = AppSymbole.Untergrenze(0.639, e, M);
        var altT = AppSymbole.Plaetze(Ids, sitz, Blick, v1a, M, AppSymbole.Untergrenze(0.639, v1a, M));
        var neuT = AppSymbole.Plaetze(Ids, sitz, Blick, e, M, ug, sitzend: true);
        Assert.InRange(altT.Max(x => x.Lage.Mitte.Y) - neuT.Max(x => x.Lage.Mitte.Y), 0.19, 0.2 + 1e-9);   // rund 0.20 m (das Feld wird bis FeldMinM gestaucht)
        Assert.All(neuT, s => Assert.True(s.Lage.Mitte.Y - AppSymbole.Durchmesser(M) / 2 >= 0.639 + 0.06 - 1e-9, s.Id));   // unterste Reihe über der Platte
        var neuTs = AppSymbole.Plaetze(Ids, sitz, Blick, e, M, ug, sitzend: false);
        foreach (var id in Ids) Nah(Y(neuTs, id), Y(neuT, id), 1e-12, id);
        // am virtuellen Tisch (0.75 m) bleibt weniger Platz: das Feld wird bis FeldMinM (0.24 m) gestaucht, die oberste Reihe liegt nur gut 8 cm tiefer als in V1a
        var altV = AppSymbole.Plaetze(Ids, sitz, Blick, v1a, M, AppSymbole.Untergrenze(0.75, v1a, M));
        var neuV = AppSymbole.Plaetze(Ids, sitz, Blick, e, M, AppSymbole.Untergrenze(0.75, e, M));
        Assert.True(altV.Max(x => x.Lage.Mitte.Y) - neuV.Max(x => x.Lage.Mitte.Y) >= 0.08);
        Nah(neuV.Max(x => x.Lage.Mitte.Y) - neuV.Min(x => x.Lage.Mitte.Y), AppSymbole.FeldMinM, 0.031);
        Assert.All(neuV, s => Assert.True(s.Lage.Mitte.Y - AppSymbole.Durchmesser(M) / 2 >= 0.75 + 0.06 - 1e-9, s.Id));
        // Tabelle für den Bericht: oberste und unterste Reihe (Mitte) gegen das Auge
        foreach (var (fall, p, auge) in new[] { ("Begehen stehend (Auge 1.65 m)", neu, steh.Y), ("Begehen sitzend (Auge 1.20 m)", neuS, sitz.Y),
            ("Tisch virtuell 0.75 m (Auge 1.20 m)", neuV, sitz.Y), ("Tisch echt 0.639 m (Auge 1.20 m)", neuT, sitz.Y) })
            aus.WriteLine($"{fall}: oberste Reihe {p.Max(x => x.Lage.Mitte.Y) - auge:+0.000;-0.000} m, unterste {p.Min(x => x.Lage.Mitte.Y) - auge:+0.000;-0.000} m, Mitte {(p.Max(x => x.Lage.Mitte.Y) + p.Min(x => x.Lage.Mitte.Y)) / 2 - auge:+0.000;-0.000} m");
        // bogen: dieselbe Mitte
        var bogen = AppSymbole.Plaetze(Ids, sitz, Blick, e with { Verteilung = Varianten.SymboleBogen }, M, sitzend: true);
        Assert.All(bogen, s => Nah(s.Lage.Mitte.Y, sitz.Y - 0.14, 1e-9));
        Nah(AppSymbole.MitteUnterAuge(e, amTisch: false, sitzend: false), 0.22);
        Nah(AppSymbole.MitteUnterAuge(e, amTisch: false, sitzend: true), 0.14);
        Nah(AppSymbole.MitteUnterAuge(e, amTisch: true, sitzend: true), 0.52);
    }

    // ================================================================================================ 2. Sperre nach der Wahl einer App

    static List<BeruehrKugel> Kugeln(double versatz = 0) =>
        [new("a", V(-0.2 + versatz, 1.2, -0.6), 0.043), new("b", V(0 + versatz, 1.2, -0.6), 0.043), new("c", V(0.2 + versatz, 1.2, -0.6), 0.043)];

    static Dictionary<string, Vec3> FlachAn(Vec3 p) => Geisterhand.VorlageFlach(p + V(0, 0, 0.05), V(0, 0, -1), V(0, 1, 0), links: false);

    [Fact]
    public void Nach_der_Wahl_zaehlt_keine_Beruehrung_bis_das_Feld_ruht_und_die_Hand_0_3_s_frei_ist()
    {
        var e = new SymbolEinstellungen();
        var z = new HandBeruehrZustand();
        var k = Kugeln();
        string? Schritt(double t, Dictionary<string, Vec3>? hand, bool bewegt = false, List<BeruehrKugel>? kugeln = null) =>
            HandBeruehrung.Schritt(z, t, [null, hand], kugeln ?? k, gesteLaeuft: false, e, bewegt).Gewaehlt;
        Assert.Null(Schritt(0, null));
        Assert.Equal("a", Schritt(0.1, FlachAn(k[0].Mitte)));
        Assert.True(z.WahlGesperrt);
        // gleich weiter zum nächsten: gesperrt (auch nach dem Loslassen von «a», solange die Hand nicht 0.3 s frei war)
        Assert.Null(Schritt(0.2, FlachAn(k[1].Mitte)));
        Assert.Null(Schritt(0.3, null));
        Assert.Null(Schritt(0.45, FlachAn(k[2].Mitte)));   // nur 0.15 s frei
        Assert.Null(Schritt(0.5, null));
        Assert.Null(Schritt(0.7, null));
        Assert.True(z.WahlGesperrt);
        Assert.Null(Schritt(0.81, null));                  // 0.31 s frei: die Sperre fällt
        Assert.False(z.WahlGesperrt);
        Assert.Equal("c", Schritt(0.9, FlachAn(k[2].Mitte)));
    }

    [Fact]
    public void Feld_gleitet_unter_der_ruhenden_Hand_durch_ohne_Wahl()
    {
        var e = new SymbolEinstellungen();
        var z = new HandBeruehrZustand();
        var hand = FlachAn(V(-0.2, 1.2, -0.6));   // bleibt über dem Platz von «a»
        Assert.Equal("a", HandBeruehrung.Schritt(z, 0, [null, hand], Kugeln(), false, e).Gewaehlt);
        // das Feld weicht aus: es gleitet in 1 s um 0.5 m nach links – «b» und «c» laufen unter der Hand durch
        var vorher = Kugeln().Select(x => x.Mitte).ToList();
        var gewaehlt = new List<string>();
        int beruehrtB = 0, beruehrtC = 0;
        for (int i = 1; i <= 72; i++)
        {
            var t = i / 72.0;
            var kugeln = Kugeln(-0.5 * GlasStil.Weich(t));
            var jetzt = kugeln.Select(x => x.Mitte).ToList();
            var bewegt = AppSymbole.Bewegt(vorher, jetzt, 1 / 72.0);
            vorher = jetzt;
            var r = HandBeruehrung.Schritt(z, t, [null, hand], kugeln, false, e, bewegt);
            if (r.Gewaehlt is { } g) gewaehlt.Add(g);
            if (r.Beruehrt == "b") beruehrtB++;
            if (r.Beruehrt == "c") beruehrtC++;
        }
        Assert.Empty(gewaehlt);
        Assert.True(beruehrtB > 0 && beruehrtC > 0, $"b {beruehrtB}, c {beruehrtC}");   // sie liefen wirklich unter der Hand durch
        // das Feld ruht, die Hand liegt in «c»: weiter gesperrt (sie hat die Symbole nie 0.3 s verlassen, solange das Feld glitt); dann weg, 0.3 s frei, wieder hinein: gewählt
        var ruhig = Kugeln(-0.5);
        var aufC = FlachAn(ruhig[2].Mitte);
        for (int i = 0; i < 20; i++) Assert.Null(HandBeruehrung.Schritt(z, 1.1 + i / 72.0, [null, aufC], ruhig, false, e, false).Gewaehlt);
        for (int i = 0; i <= 30; i++) Assert.Null(HandBeruehrung.Schritt(z, 1.5 + i / 72.0, [null, null], ruhig, false, e, false).Gewaehlt);
        Assert.Equal("c", HandBeruehrung.Schritt(z, 2.0, [null, aufC], ruhig, false, e, false).Gewaehlt);
    }

    [Fact]
    public void Solange_das_Feld_sich_bewegt_bleibt_die_Wahl_gesperrt_auch_mit_freier_Hand()
    {
        var e = new SymbolEinstellungen();
        var z = new HandBeruehrZustand();
        var k = Kugeln();
        Assert.Equal("a", HandBeruehrung.Schritt(z, 0, [null, FlachAn(k[0].Mitte)], k, false, e).Gewaehlt);
        for (int i = 1; i <= 60; i++) HandBeruehrung.Schritt(z, i / 72.0, [null, null], k, false, e, feldBewegt: true);   // 0.8 s frei, aber das Feld gleitet
        Assert.True(z.WahlGesperrt);
        Assert.Null(HandBeruehrung.Schritt(z, 0.85, [null, FlachAn(k[1].Mitte)], k, false, e, feldBewegt: false).Gewaehlt);
        // mit «symbole.wahl_frei_s» = 0 genügt es, dass das Feld ruht und nichts berührt ist (wie bis V1d)
        var z0 = new HandBeruehrZustand();
        var e0 = e with { WahlFreiS = 0 };
        Assert.Equal("a", HandBeruehrung.Schritt(z0, 0, [null, FlachAn(k[0].Mitte)], k, false, e0).Gewaehlt);
        HandBeruehrung.Schritt(z0, 0.02, [null, null], k, false, e0);
        Assert.Equal("b", HandBeruehrung.Schritt(z0, 0.04, [null, FlachAn(k[1].Mitte)], k, false, e0).Gewaehlt);
        // Bewegt: schneller als 3 cm/s bzw. andere Zahl der Symbole
        Assert.True(AppSymbole.Bewegt([V(0, 0, 0)], [V(0.01, 0, 0)], 1 / 72.0));
        Assert.False(AppSymbole.Bewegt([V(0, 0, 0)], [V(0.0003, 0, 0)], 1 / 72.0));
        Assert.True(AppSymbole.Bewegt([V(0, 0, 0)], [V(0, 0, 0), V(1, 0, 0)], 1 / 72.0));
    }

    // ================================================================================================ 3. Tafel mittig, träges Nachführen

    static double GierenZu(Vec3 p, Vec3 auge) => VrLogik.GierenVonRichtung(new Xz(p.X - auge.X, p.Z - auge.Z));

    [Fact]
    public void Tafel_oeffnet_mittig_in_der_aktuellen_Blickrichtung_auf_bequemer_Hoehe()
    {
        var a = new AppEinstellungen();
        var e = TafelEinstellungen.Vorgabe;
        Assert.Equal(0.15, e.HoeheM);
        Assert.Equal(25, e.NachfuehrenGrad);
        var auge = V(1, 1.6, 2);
        foreach (var grad in new[] { 90.0, 30, -120, 175 })
        {
            var p = Tafel.Platz(auge, grad * Grad, a, e);
            Nah(Wd(GierenZu(p, auge), grad * Grad), 0, 1e-9);
            Nah(Math.Sqrt((p.X - auge.X) * (p.X - auge.X) + (p.Z - auge.Z) * (p.Z - auge.Z)), a.AbstandM, 1e-9);
            Nah(p.Y, 1.45, 1e-9);
        }
        // am Tisch: Unterkante von Fenster und Leiste um den Abstand über der Platte (sitzend, Augen 1.0 m über einer hohen Platte 0.8 m)
        var t = Tafel.Platz(V(0, 1.0, 0), Blick, a, e, tischOben: 0.8, unterhalbM: 0.2, abstandM: 0.06);
        Nah(t.Y, 1.06, 1e-9);
        Nah(Tafel.Platz(V(0, 1.6, 0), Blick, a, e, tischOben: 0.75, unterhalbM: 0.2).Y, 1.45, 1e-9);   // genug Platz: bequeme Höhe
    }

    [Fact]
    public void Tafel_rueckt_traege_nach_ab_der_Schwelle_sonst_bleibt_sie_stehen()
    {
        var a = new AppEinstellungen();
        var e = TafelEinstellungen.Vorgabe;
        var auge = V(0, 1.6, 0);
        var mitte = Tafel.Platz(auge, Blick, a, e);
        var z = new TafelFolgen();
        // 20° weggedreht: unter der Schwelle (25°) – sie bleibt im Raum stehen
        for (int i = 0; i < 72; i++) mitte = Tafel.Nachfuehren(z, mitte, auge, Blick + 20 * Grad, 1 / 72.0, Tafel.Platz(auge, Blick + 20 * Grad, a, e), e, false, false);
        Nah(Wd(GierenZu(mitte, auge), Blick), 0, 1e-9);
        Assert.False(z.Folgt);
        // 40° weggedreht: sie rückt nach – ruhig (nicht im ersten Bild), am Ende mittig
        var blick = Blick + 40 * Grad;
        var p1 = Tafel.Nachfuehren(z, mitte, auge, blick, 1 / 72.0, Tafel.Platz(auge, blick, a, e), e, false, false);
        Assert.True(z.Folgt);
        var schritt1 = Math.Abs(Wd(GierenZu(p1, auge), GierenZu(mitte, auge)));
        Assert.InRange(schritt1, 0.5 * Grad, 3 * Grad);   // träge: rund 4 % des Winkels je Bild
        mitte = p1;
        var winkel = new List<double>();
        for (int i = 0; i < 3 * 72; i++)
        {
            mitte = Tafel.Nachfuehren(z, mitte, auge, blick, 1 / 72.0, Tafel.Platz(auge, blick, a, e), e, false, false);
            winkel.Add(Math.Abs(Wd(GierenZu(mitte, auge), blick)));
        }
        Assert.True(winkel[^1] <= Tafel.RestGrad * Grad + 1e-9);
        Assert.False(z.Folgt);
        Assert.Equal(1, z.Nachgerueckt);
        for (int i = 1; i < winkel.Count; i++) Assert.True(winkel[i] <= winkel[i - 1] + 1e-12);   // ruhig, ohne Überschwingen
        Nah(Math.Sqrt(mitte.X * mitte.X + mitte.Z * mitte.Z), a.AbstandM, 0.01);
        // Hand an der Tafel bzw. Variante «fest»: kein Nachrücken
        var z2 = new TafelFolgen();
        var m2 = Tafel.Platz(auge, Blick, a, e);
        Assert.Equal(m2, Tafel.Nachfuehren(z2, m2, auge, blick, 1 / 72.0, Tafel.Platz(auge, blick, a, e), e, handAn: true, gezogen: false));
        var fest = e with { Nachfuehren = Varianten.TafelFest };
        Assert.Equal(m2, Tafel.Nachfuehren(z2, m2, auge, blick, 1 / 72.0, Tafel.Platz(auge, blick, a, e), fest, false, false));
        // am Griff verschoben: bleibt, wo man sie hinstellt, bis zum nächsten Öffnen
        Tafel.Nachfuehren(z2, m2, auge, Blick, 1 / 72.0, m2, e, false, gezogen: true);
        Assert.True(z2.Gestellt);
        Assert.Equal(m2, Tafel.Nachfuehren(z2, m2, auge, blick, 1 / 72.0, Tafel.Platz(auge, blick, a, e), e, false, false));
        z2.Geoeffnet();
        Assert.NotEqual(m2, Tafel.Nachfuehren(z2, m2, auge, blick, 1 / 72.0, Tafel.Platz(auge, blick, a, e), e, false, false));
    }

    [Fact]
    public void Der_PC_Bildschirm_oeffnet_frontal_und_rueckt_ebenso_traege_nach()
    {
        var e = DesktopEinstellungen.Vorgabe;
        var a = new DesktopAnsicht();
        var auge = V(0, 1.6, 0);
        a.Einblenden(0, auge, Blick, e);
        for (int i = 1; i <= 60; i++) a.Schritt(i / 72.0, 1 / 72.0, auge, Blick, SeitenWischStand.Nichts, e);
        Assert.Equal(DesktopPhase.Da, a.Phase);
        Nah(Wd(GierenZu(a.Lage!.Mitte, auge), Blick), 0, 1e-9);
        // 20° gedreht: bleibt; 60° gedreht: rückt träge nach und steht am Ende wieder frontal (zum Kopf gedreht)
        for (int i = 0; i < 72; i++) a.Nachfuehren(auge, Blick + 20 * Grad, 1 / 72.0, e, TafelEinstellungen.Vorgabe, false);
        Nah(Wd(GierenZu(a.Lage!.Mitte, auge), Blick), 0, 1e-9);
        var blick = Blick - 60 * Grad;
        a.Nachfuehren(auge, blick, 1 / 72.0, e, TafelEinstellungen.Vorgabe, false);
        Assert.True(a.Nachfuehrung.Folgt);
        Assert.True(Math.Abs(Wd(GierenZu(a.Lage!.Mitte, auge), blick)) > 50 * Grad);   // nicht sprunghaft
        for (int i = 0; i < 4 * 72; i++) a.Nachfuehren(auge, blick, 1 / 72.0, e, TafelEinstellungen.Vorgabe, false);
        Assert.True(Math.Abs(Wd(GierenZu(a.Lage!.Mitte, auge), blick)) <= Tafel.RestGrad * Grad + 1e-9);
        Nah(Wd(a.Lage!.Gieren, blick), 0, Tafel.RestGrad * Grad + 1e-9);   // die Fläche schaut wieder zum Kopf
        Nah(a.Lage!.Mitte.Abstand(auge with { Y = a.Lage.Mitte.Y }), e.AbstandM, 0.01);
        // Variante «fest»: kein Nachrücken
        var f = new DesktopAnsicht();
        f.Einblenden(0, auge, Blick, e);
        for (int i = 1; i <= 60; i++) f.Schritt(i / 72.0, 1 / 72.0, auge, Blick, SeitenWischStand.Nichts, e);
        var vor = f.Lage!.Mitte;
        for (int i = 0; i < 72; i++) f.Nachfuehren(auge, blick, 1 / 72.0, e, TafelEinstellungen.Vorgabe with { Nachfuehren = Varianten.TafelFest }, false);
        Assert.Equal(vor, f.Lage!.Mitte);
    }

    // ================================================================================================ 4. Ein Griff am Sitzungstisch

    static readonly SitzungBezug Bezug = new(new Xz(0, 0), 0, new ModellGroesse(20, 10, 6));

    static EinGriffHand H(Vec3 p, bool pinch = true, double? gieren = 0, double? rollen = 0) => new(pinch, p, gieren, rollen);

    [Fact]
    public void Griff_liegt_mitten_an_der_vorderen_Gebaeudekante_auch_nach_dem_Drehen()
    {
        var lage = new SitzungLage(50, 0, 0, 0);
        var g = EinGriff.GriffPunkt(lage, Bezug, null);
        Nah(g.X, 0, 1e-9);
        Nah(g.Z, -0.9 + 0.1 + XrSitzungLogik.GriffAbstandM, 1e-9);   // Modellmitte −0.9, halbe Tiefe 10/50/2, 6 cm nach aussen
        Nah(g.Y, XrSitzungLogik.TischHoehe + XrSitzungLogik.GriffHoeheM, 1e-9);
        // um 90° gedreht: die Seite −x (Länge 10 m) zeigt zum Menschen; der Griff kommt wieder nach vorn
        var gd = EinGriff.GriffPunkt(lage with { Drehung = Math.PI / 2 }, Bezug, null);
        Nah(gd.X, 0, 1e-9);
        Nah(gd.Z, -0.9 + 0.2 + XrSitzungLogik.GriffAbstandM, 1e-9);
        // mit dem Rechteck des sichtbaren Gebäudes (gedreht im Plan): Mitte der vorderen Seite
        var rahmen = new SichtbarRahmen(Math.Cos(0.75), Math.Sin(0.75), -6, 6, -3, 3);
        var gr = EinGriff.GriffPunkt(lage, Bezug, rahmen);
        Assert.True(gr.Z > -0.9, "vor der Modellmitte");
        Assert.InRange(gr.X, -0.15, 0.15);
    }

    [Fact]
    public void Ein_Griff_bewegt_in_allen_Richtungen_und_dreht_mit_dem_Gierwinkel_der_Hand()
    {
        var z = EinGriff.Neu(new SitzungLage(50, 0, 0, 0));
        var g = EinGriff.GriffPunkt(z.Lage, Bezug, null);
        // ein Pinch weit weg fasst nicht; am Griff fasst er
        Assert.Equal(EinGriffEreignis.Nichts, EinGriff.Schritt(z, null, H(g + V(0.2, 0, 0)), g));
        EinGriff.Schritt(z, null, null, g);
        Assert.Equal(EinGriffEreignis.Gefasst, EinGriff.Schritt(z, null, H(g + V(0.01, 0, 0.01)), g));
        Assert.Equal("rechts", z.Hand);
        var mitte0 = XrSitzungLogik.ModellMitte(z.Lage);
        var h0 = g + V(0.01, 0, 0.01);
        // verschieben in Breite, Tiefe und Höhe
        EinGriff.Schritt(z, null, H(h0 + V(0.1, 0.05, -0.05)), g);
        Nah(z.Lage.X, 0.1, 1e-9);
        Nah(z.Lage.Z, -0.05, 1e-9);
        Nah(z.Lage.Hoehe, 0.05, 1e-9);
        Nah(z.Lage.Massstab, 50, 1e-9);
        Nah(z.Lage.Drehung, 0, 1e-12);
        // drehen mit dem Gierwinkel der Hand (30°, die Totzone von 2° zieht ab), um die Hand: ihr Abstand zur Modellmitte bleibt
        var h1 = h0 + V(0.1, 0.05, -0.05);
        EinGriff.Schritt(z, null, H(h1, gieren: 30 * Grad), g);
        Nah(z.Lage.Drehung, 28 * Grad, 1e-9);
        var m = XrSitzungLogik.ModellMitte(z.Lage);
        Nah(Math.Sqrt((m.X - h1.X) * (m.X - h1.X) + (m.Z - h1.Z) * (m.Z - h1.Z)), Math.Sqrt((mitte0.X - h0.X) * (mitte0.X - h0.X) + (mitte0.Z - h0.Z) * (mitte0.Z - h0.Z)), 1e-9);
        // kleines Zittern dreht nicht
        EinGriff.Schritt(z, null, H(h1, gieren: 1.5 * Grad), g);
        Nah(z.Lage.Drehung, 0, 1e-12);
        // loslassen: die Lage bleibt
        var lage = z.Lage;
        Assert.Equal(EinGriffEreignis.Losgelassen, EinGriff.Schritt(z, null, H(h1, pinch: false), g));
        Assert.False(z.Gehalten);
        Assert.Equal(lage, z.Lage);
        // die zweite Hand allein bewegt nichts
        EinGriff.Schritt(z, H(V(0.3, 0.9, -0.6)), null, g);
        Assert.Equal(lage, z.Lage);
    }

    [Fact]
    public void Zoomen_mit_der_zweiten_Hand_um_die_Griffhand_in_den_Grenzen_ohne_Sprung()
    {
        var z = EinGriff.Neu(new SitzungLage(50, 0, 0, 0));
        var g = EinGriff.GriffPunkt(z.Lage, Bezug, null);
        EinGriff.Schritt(z, null, H(g), g);
        Assert.True(z.Gehalten);
        // die linke Hand pincht 10 cm neben der Griffhand, dann 20 cm: doppelt so gross (1:25); der Punkt unter der Griffhand bleibt
        Assert.Equal(EinGriffEreignis.ZoomBeginnt, EinGriff.Schritt(z, H(g + V(-0.1, 0, 0)), H(g), g));
        Assert.True(z.ZweiteHand);
        Assert.True(EinGriff.HandAmGriff(z, "links") && EinGriff.HandAmGriff(z, "rechts"));
        EinGriff.Schritt(z, H(g + V(-0.2, 0, 0)), H(g), g);
        Nah(z.Lage.Massstab, 25, 1e-9);
        var m = XrSitzungLogik.ModellMitte(z.Lage);
        Nah(m.Z - g.Z, (-0.9 - g.Z) * 2, 1e-9);   // um die Griffhand gezoomt: doppelte Grösse, doppelter Abstand Griff – Modellmitte
        // Grenzen 1:5 … 1:200
        EinGriff.Schritt(z, H(g + V(-2.0, 0, 0)), H(g), g);
        Nah(z.Lage.Massstab, XrSitzungLogik.MassstabMin, 1e-9);
        EinGriff.Schritt(z, H(g + V(-0.001, 0, 0)), H(g), g);
        Assert.InRange(z.Lage.Massstab, 5, 200);
        EinGriff.Schritt(z, H(g + V(-0.2, 0, 0)), H(g), g);
        Nah(z.Lage.Massstab, 25, 1e-9);
        // die zweite Hand lässt los: der Massstab bleibt, das Bewegen geht ohne Sprung weiter
        Assert.Equal(EinGriffEreignis.ZoomEndet, EinGriff.Schritt(z, H(g + V(-0.2, 0, 0), pinch: false), H(g), g));
        var l0 = z.Lage;
        EinGriff.Schritt(z, null, H(g + V(0.001, 0, 0)), g);
        Nah(z.Lage.Massstab, 25, 1e-9);
        Nah(z.Lage.X - l0.X, 0.001, 1e-9);
        // mit eigenen Grenzen
        var z2 = EinGriff.Neu(new SitzungLage(50, 0, 0, 0));
        var grenzen = new ZoomGrenzen(20, 100);
        EinGriff.Schritt(z2, null, H(g), g, null, grenzen);
        EinGriff.Schritt(z2, H(g + V(-0.1, 0, 0)), H(g), g, null, grenzen);
        EinGriff.Schritt(z2, H(g + V(-1, 0, 0)), H(g), g, null, grenzen);
        Nah(z2.Lage.Massstab, 20, 1e-9);
    }

    [Fact]
    public void Zoomen_als_Drehknopf_Rollen_im_Uhrzeigersinn_vergroessert()
    {
        var e = EinGriffEinstellungen.Vorgabe with { Zoom = Varianten.ZoomDrehknopf };
        Assert.Equal(60, e.DrehknopfGrad);
        var z = EinGriff.Neu(new SitzungLage(50, 0, 0, 0));
        var g = EinGriff.GriffPunkt(z.Lage, Bezug, null);
        EinGriff.Schritt(z, null, H(g, rollen: 0.3), g, e);
        // +63° (Totzone 3°): halb so grosser Nenner (doppelt so gross)
        EinGriff.Schritt(z, null, H(g, rollen: 0.3 + 63 * Grad), g, e);
        Nah(z.Lage.Massstab, 25, 1e-9);
        EinGriff.Schritt(z, null, H(g, rollen: 0.3 - 63 * Grad), g, e);
        Nah(z.Lage.Massstab, 100, 1e-9);
        EinGriff.Schritt(z, null, H(g, rollen: 0.3 + 2 * Grad), g, e);   // Zittern
        Nah(z.Lage.Massstab, 50, 1e-9);
        // im Drehknopf zoomt die zweite Hand nicht
        EinGriff.Schritt(z, H(g + V(-0.1, 0, 0)), H(g, rollen: 0.3), g, e);
        EinGriff.Schritt(z, H(g + V(-0.3, 0, 0)), H(g, rollen: 0.3), g, e);
        Assert.False(z.ZweiteHand);
        Nah(z.Lage.Massstab, 50, 1e-9);
        // Rollen aus den Gelenken: die Handfläche um die Achse der Hand im Uhrzeigersinn (aus Sicht des Menschen) gedreht = positiv
        foreach (var seite in new[] { "right", "left" })
        {
            var f = V(0, 0, -1);
            var n0 = V(0, -1, 0);
            var r0 = EinGriff.Rollen(HandHilfen.Hand(seite, V(0.1, 1.0, -0.3), f, n0), seite)!.Value;
            var w = 30 * Grad;
            var n1 = n0 * Math.Cos(w) + f.Kreuz(n0) * Math.Sin(w);   // rechtsdrehend um die Achse vom Menschen weg
            var r1 = EinGriff.Rollen(HandHilfen.Hand(seite, V(0.1, 1.0, -0.3), f, n1), seite)!.Value;
            Nah(Wd(r1, r0), 30 * Grad, 1e-6, seite);
        }
        Assert.Null(EinGriff.Rollen(HandHilfen.Hand("right", V(0, 1, 0), V(0, 1, 0), V(0, 0, 1)), "right"));   // Achse senkrecht
    }

    // ================================================================================================ 5. Wegwischen und Hineinziehen mit beiden Händen

    static HandBeobachtung? Hb(double x, Vec3 n, bool pinch = false) => new HandBeobachtung(V(x, 1.3, -0.4), n, true, pinch);

    /// <summary>Spielt eine Bewegung der Handfläche ab (x von → bis in dauer s, 72 Bilder/s, davor und danach 0.3 s Ruhe) und zählt die Ereignisse des Wegwischens.</summary>
    static int Wegwischen_(Func<double, (HandBeobachtung? R, HandBeobachtung? L)> haende, double dauer = 0.4)
    {
        var z = new WegwischZustand();
        var n = 0;
        for (int i = 0; i <= (int)((dauer + 0.6) * 72); i++)
        {
            var t = i / 72.0;
            var u = Math.Clamp((t - 0.3) / dauer, 0, 1);
            var (r, l) = haende(GlasStil.Weich(u));
            if (Wegwischen.Schritt(z, t, r, l, Blick).Ereignis) n++;
        }
        return n;
    }

    [Fact]
    public void Wegwischen_mit_der_linken_Hand_Handflaeche_seitlich_in_beide_Richtungen()
    {
        var links = V(-1, 0, 0);
        var rechts = V(1, 0, 0);
        // linke Hand von rechts nach links (30 cm in 0.4 s), Handfläche nach links bzw. nach rechts: Wegwischen
        Assert.Equal(1, Wegwischen_(u => (null, Hb(0.15 - 0.3 * u, links))));
        Assert.Equal(1, Wegwischen_(u => (null, Hb(0.15 - 0.3 * u, rechts))));
        // rechte Hand wie bisher nur mit der Handfläche nach links
        Assert.Equal(1, Wegwischen_(u => (Hb(0.15 - 0.3 * u, links), null)));
        Assert.Equal(0, Wegwischen_(u => (Hb(0.15 - 0.3 * u, rechts), null)));
        // Handfläche oben (Hebegeste), Pinch, Gegenrichtung: nichts
        Assert.Equal(0, Wegwischen_(u => (null, Hb(0.15 - 0.3 * u, V(0, 1, 0)))));
        Assert.Equal(0, Wegwischen_(u => (null, Hb(0.15 - 0.3 * u, links, pinch: true))));
        Assert.Equal(0, Wegwischen_(u => (null, Hb(-0.15 + 0.3 * u, links))));
        // die eine Hand wischt, die andere ruht in der Haltung: einmal
        Assert.Equal(1, Wegwischen_(u => (Hb(0.3, links), Hb(0.15 - 0.3 * u, rechts))));
        Assert.Equal(1, Wegwischen_(u => (Hb(0.15 - 0.3 * u, links), Hb(-0.3, links))));
        // beide Hände wischen zugleich: einmal (eine Sperre für beide)
        Assert.Equal(1, Wegwischen_(u => (Hb(0.25 - 0.3 * u, links), Hb(0.05 - 0.3 * u, rechts))));
        // die Hand, die ausgelöst hat
        var z = new WegwischZustand();
        var s = new WegwischStand(false, 0, false, []);
        for (int i = 0; i <= 50 && !s.Ereignis; i++) s = Wegwischen.Schritt(z, i / 72.0, null, Hb(0.15 - 0.3 * Math.Min(1, i / 28.0), rechts), Blick);
        Assert.True(s.Ereignis);
        Assert.Equal("left", z.Hand);
    }

    [Fact]
    public void Gestentabelle_nennt_den_einen_Griff_die_linke_Hand_und_das_Blaettern_auf_der_Tafel()
    {
        var text = string.Join(" ", Gestenbedeutung.Tabelle.Select(x => x.Geste + " " + x.Bedeutung));
        foreach (var s in new[] { "vorderen Gebäudekante", "zweiten Hand", "Drehknopf", "linke Hand", "Wisch auf der Tafel", "nächste Seite" }) Assert.Contains(s, text);
        Assert.DoesNotContain("Zwei Hände an den Griffen", text);
    }

    [Fact]
    public void Gegenlaeufige_zweite_Hand_ist_kein_Wegwischen_Wegschieben_mit_zwei_Haenden()
    {
        var links = V(-1, 0, 0);
        var rechts = V(1, 0, 0);
        // auseinander: die linke Hand nach links (Handfläche nach aussen bzw. innen), die rechte zugleich nach rechts – kein Wegwischen
        Assert.Equal(0, Wegwischen_(u => (Hb(0.05 + 0.25 * u, rechts), Hb(-0.05 - 0.3 * u, links))));
        Assert.Equal(0, Wegwischen_(u => (Hb(0.05 + 0.25 * u, links), Hb(-0.05 - 0.3 * u, rechts))));
        // zusammen: die rechte Hand nach links, die linke zugleich nach rechts – kein Wegwischen
        Assert.Equal(0, Wegwischen_(u => (Hb(0.3 - 0.3 * u, links), Hb(-0.3 + 0.25 * u, rechts))));
        // die andere Hand zittert nur bzw. geht ein paar Zentimeter mit (unter GegenM): Wegwischen wie bisher
        Assert.Equal(1, Wegwischen_(u => (Hb(0.05 + 0.04 * u, rechts), Hb(-0.05 - 0.3 * u, links))));
        Assert.Equal(1, Wegwischen_(u => (Hb(0.15 - 0.3 * u, links), Hb(-0.3 + 0.03 * Math.Sin(u * Math.PI * 4), rechts))));
        Assert.Equal(0.08, Wegwischen.GegenM);
        // nur die rechte Hand (Schritt ohne die linke, wie bis V1d): unverändert
        var z = new WegwischZustand();
        var n = 0;
        for (int i = 0; i <= 72; i++) if (Wegwischen.Schritt(z, i / 72.0, Hb(0.15 - 0.3 * GlasStil.Weich(Math.Clamp((i / 72.0 - 0.3) / 0.4, 0, 1)), links), Blick).Ereignis) n++;
        Assert.Equal(1, n);
    }

    static Dictionary<string, Vec3> Offen(string seite, double x, Vec3 n) => HandHilfen.Hand(seite, V(x, 1.25, -0.25), V(0, 0.2, -1), n);

    static (int Rechts, int Links) Hineinziehen_(Func<double, (Dictionary<string, Vec3>? R, Dictionary<string, Vec3>? L)> haende, double dauer = 0.45)
    {
        var g = new DesktopGesten(() => DesktopEinstellungen.Vorgabe, null, () => WegwischEinstellungen.Vorgabe);
        int r = 0, l = 0;
        for (int i = 0; i <= (int)((dauer + 0.8) * 72); i++)
        {
            var t = i / 72.0;
            var u = Math.Clamp((t - 0.4) / dauer, 0, 1);
            var (hr, hl) = haende(GlasStil.Weich(u));
            var s = g.Schritt(new DesktopHaende(t, hr, hl, Blick), true);
            if (s.Ausgeloest && g.Hand == "right") r++;
            if (s.Ausgeloest && g.Hand == "left") l++;
        }
        return (r, l);
    }

    [Fact]
    public void Hineinziehen_mit_der_linken_Hand_von_links_nach_rechts()
    {
        var links = V(-1, 0, 0);   // waagrecht nach links, senkrecht zur Fingerrichtung (0, 0.2, −1)
        var rechts = V(1, 0, 0);
        // linke Hand, Handfläche nach rechts bzw. links, 35 cm von links nach rechts, die rechte ruht
        Assert.Equal((0, 1), Hineinziehen_(u => (Offen("right", 0.35, links), Offen("left", -0.2 + 0.35 * u, rechts))));
        Assert.Equal((0, 1), Hineinziehen_(u => (null, Offen("left", -0.2 + 0.35 * u, links))));
        // rechte Hand wie bisher (Handfläche nach links), die linke ruht
        Assert.Equal((1, 0), Hineinziehen_(u => (Offen("right", -0.1 + 0.35 * u, links), Offen("left", -0.35, rechts))));
        // beide Hände ziehen zugleich: nichts (die andere Hand muss ruhen) – sicher nicht doppelt
        var beide = Hineinziehen_(u => (Offen("right", 0.0 + 0.35 * u, links), Offen("left", -0.3 + 0.35 * u, rechts)));
        Assert.True(beide.Rechts + beide.Links <= 1);
        // linke Hand mit der Handfläche oben: nichts
        Assert.Equal((0, 0), Hineinziehen_(u => (null, Offen("left", -0.2 + 0.35 * u, V(0, 1, 0.2).Einheit()))));
    }

    // ================================================================================================ 6. Blättern auf der Tafel

    static readonly TabletPose Tafel0 = new(V(0, 1.45, -0.45), V(0, 0, 1), V(1, 0, 0), V(0, 1, 0), 0.32, 0.31);

    /// <summary>Rechte bzw. linke Hand, Finger nach vorn, Handfläche nach links (bzw. rechts): die Zeigefingerspitze bei spitze.</summary>
    static Dictionary<string, Vec3> Spitze(string seite, Vec3 spitze, Vec3 n)
    {
        var h0 = HandHilfen.Hand(seite, V(0, 0, 0), V(0, 0, -1), n);
        var d = spitze - h0["index-finger-tip"];
        return h0.ToDictionary(x => x.Key, x => x.Value + d);
    }

    /// <summary>
    /// Ein Wisch wie in der App verdrahtet: Blättern je Hand, die Hand auf der Tafel zählt nicht für Wegwischen und Hineinziehen. bahn(u) = Lage der Zeigefingerspitze
    /// (0 … 1 in dauer s, davor und danach 0.3 s Ruhe). Rückgabe: Ereignisse des Blätterns, des Wegwischens, des Hineinziehens.
    /// </summary>
    static (List<WischRichtung> Blaettern, int Weg, int Hinein) Wisch(Func<double, Vec3> bahn, string seite = "right", Vec3? normale = null, double dauer = 0.35, TabletPose? tafel = null)
    {
        var n = normale ?? V(-1, 0, 0);
        var zb = new TafelBlaetternZustand();
        var wz = new WegwischZustand();
        var dg = new DesktopGesten(() => DesktopEinstellungen.Vorgabe, null, () => WegwischEinstellungen.Vorgabe);
        var bl = new List<WischRichtung>();
        int weg = 0, hinein = 0;
        for (int i = 0; i <= (int)((dauer + 0.6) * 72); i++)
        {
            var t = i / 72.0;
            var u = GlasStil.Weich(Math.Clamp((t - 0.3) / dauer, 0, 1));
            var hand = Spitze(seite, bahn(u), n);
            var b = TafelBlaettern.Schritt(zb, t, hand, false, tafel ?? Tafel0);
            if (b.Ereignis is { } r) bl.Add(r);
            var frei = b.AnTafel ? null : hand;
            var hb = HandBeobachtung.Aus(frei, seite);
            if (Wegwischen.Schritt(wz, t, seite == "right" ? hb : null, seite == "left" ? hb : null, Blick).Ereignis) weg++;
            if (dg.Schritt(new DesktopHaende(t, seite == "right" ? frei : null, seite == "left" ? frei : null, Blick), true).Ausgeloest) hinein++;
        }
        return (bl, weg, hinein);
    }

    [Fact]
    public void Wisch_auf_der_Tafel_blaettert_und_loest_weder_Wegwischen_noch_Hineinziehen_aus()
    {
        // 2 cm vor der Fläche von rechts nach links (24 cm): nächste Seite; kein Wegwischen
        var r1 = Wisch(u => V(0.12 - 0.24 * u, 1.45, -0.43));
        Assert.Equal([WischRichtung.Links], r1.Blaettern);
        Assert.Equal(0, r1.Weg);
        Assert.Equal(1, TafelBlaettern.SeitenSchritt(WischRichtung.Links));
        // von links nach rechts: vorige Seite; kein Hineinziehen
        var r2 = Wisch(u => V(-0.12 + 0.24 * u, 1.45, -0.43));
        Assert.Equal([WischRichtung.Rechts], r2.Blaettern);
        Assert.Equal(0, r2.Hinein);
        Assert.Equal(-1, TafelBlaettern.SeitenSchritt(WischRichtung.Rechts));
        // hoch/runter rollt die Liste
        Assert.Equal([WischRichtung.Hoch], Wisch(u => V(0, 1.36 + 0.18 * u, -0.43)).Blaettern);
        Assert.Equal([WischRichtung.Runter], Wisch(u => V(0, 1.54 - 0.18 * u, -0.43)).Blaettern);
        // dieselben Wische im freien Raum (30 cm vor der Tafel bzw. neben ihr): kein Blättern, das Wegwischen bzw. Hineinziehen gilt
        var frei = Wisch(u => V(0.15 - 0.3 * u, 1.45, -0.15));
        Assert.Empty(frei.Blaettern);
        Assert.Equal(1, frei.Weg);
        var daneben = Wisch(u => V(0.75 - 0.3 * u, 1.45, -0.43));
        Assert.Empty(daneben.Blaettern);
        Assert.Equal(1, daneben.Weg);
        var hineinFrei = Wisch(u => V(-0.1 + 0.35 * u, 1.45, -0.15), dauer: 0.45);
        Assert.Empty(hineinFrei.Blaettern);
        Assert.Equal(1, hineinFrei.Hinein);
    }

    [Fact]
    public void Blaettern_mit_beiden_Haenden_Fang_bis_zum_Ende_und_Antippen_ist_kein_Wisch()
    {
        // linke Hand (Handfläche nach rechts): blättert ebenso
        var l = Wisch(u => V(0.12 - 0.24 * u, 1.45, -0.43), "left", V(1, 0, 0));
        Assert.Equal([WischRichtung.Links], l.Blaettern);
        Assert.Equal(0, l.Weg);
        // Fang: beginnt auf der Tafel und entfernt sich dabei auf 10 cm – bleibt Blättern
        var fang = Wisch(u => V(0.12 - 0.24 * u, 1.45, -0.43 + 0.08 * u));
        Assert.Equal([WischRichtung.Links], fang.Blaettern);
        Assert.Equal(0, fang.Weg);
        // beginnt im freien Raum (10 cm davor) und streicht dann über die Tafel: kein Blättern
        var vorbei = Wisch(u => V(0.15 - 0.3 * u, 1.45, -0.35 - 0.08 * u));
        Assert.Empty(vorbei.Blaettern);
        // Antippen: die Spitze stösst von 4 cm vor in die Fläche (kaum seitlich): kein Wisch
        var tipp = Wisch(u => V(0.02 + 0.005 * u, 1.45, -0.41 - 0.045 * u), dauer: 0.2);
        Assert.Empty(tipp.Blaettern);
        // kürzer als der Mindestweg (6 cm < 8 cm): kein Wisch
        Assert.Empty(Wisch(u => V(0.03 - 0.06 * u, 1.45, -0.43)).Blaettern);
        Assert.Equal(0.08, TafelEinstellungen.Vorgabe.BlaetternWegM);
        Assert.Equal(0.05, TafelEinstellungen.Vorgabe.BlaetternAbstandM);
        // keine Tafel offen (PC-Bildschirm bzw. nichts): kein Blättern
        var zb = new TafelBlaetternZustand();
        Assert.Equal(BlaetternStand.Nichts, TafelBlaettern.Schritt(zb, 0, Spitze("right", V(0, 1.45, -0.43), V(-1, 0, 0)), false, null));
        // Pinch: kein Blättern (Pinch + Wisch gehört P5a)
        Assert.Equal(BlaetternStand.Nichts, TafelBlaettern.Schritt(zb, 0, Spitze("right", V(0, 1.45, -0.43), V(-1, 0, 0)), true, Tafel0));
    }

    [Fact]
    public void Blaettern_Seite_gleitet_mit_und_die_Gegenrichtung_ist_kurz_gesperrt()
    {
        var zb = new TafelBlaetternZustand();
        BlaetternStand s = BlaetternStand.Nichts;
        double kleinster = 0;
        WischRichtung? erst = null;
        for (int i = 0; i <= 25; i++)
        {
            s = TafelBlaettern.Schritt(zb, i / 72.0, Spitze("right", V(0.1 - 0.2 * i / 25.0, 1.45, -0.43), V(-1, 0, 0)), false, Tafel0);
            erst ??= s.Ereignis;
            kleinster = Math.Min(kleinster, s.VersatzX);
        }
        Assert.Equal(WischRichtung.Links, erst);
        Assert.True(s.Laeuft && s.AnTafel);
        Nah(kleinster, -0.16, 0.001);   // gleitet mit der Hand (höchstens die halbe Tafel)
        Nah(s.VersatzY, 0, 1e-12);
        // die Hand gleich zurück (kurz weg, dann von links nach rechts): die Gegenrichtung zählt in den ersten 0.8 s nicht
        for (int i = 0; i < 5; i++) TafelBlaettern.Schritt(zb, 0.36 + i / 72.0, null, false, Tafel0);   // Hand weg (Strich zu Ende)
        WischRichtung? zurueck = null;
        for (int i = 0; i <= 25; i++)
            zurueck ??= TafelBlaettern.Schritt(zb, 0.43 + i / 72.0, Spitze("right", V(-0.1 + 0.2 * i / 25.0, 1.45, -0.43), V(-1, 0, 0)), false, Tafel0).Ereignis;
        Assert.Null(zurueck);
        // nach der Sperre gilt die Gegenrichtung wieder
        for (int i = 0; i < 5; i++) TafelBlaettern.Schritt(zb, 1.2 + i / 72.0, null, false, Tafel0);
        WischRichtung? spaeter = null;
        for (int i = 0; i <= 25; i++)
            spaeter ??= TafelBlaettern.Schritt(zb, 1.3 + i / 72.0, Spitze("right", V(-0.1 + 0.2 * i / 25.0, 1.45, -0.43), V(-1, 0, 0)), false, Tafel0).Ereignis;
        Assert.Equal(WischRichtung.Rechts, spaeter);
    }

    // ================================================================================================ 7. Vereinfachte Unterlage am Tisch

    /// <summary>Dichtes Netz: ein Zylinder (Radius r, Länge l, n Strecken je Kreis, k Ringe) mit Normalen nach aussen.</summary>
    static (float[] Pos, float[] Nor, uint[] Ind) Zylinder(double r, double l, int n, int k)
    {
        var pos = new List<float>(); var nor = new List<float>(); var ind = new List<uint>();
        for (int j = 0; j <= k; j++)
            for (int i = 0; i < n; i++)
            {
                var phi = 2 * Math.PI * i / n;
                pos.AddRange([(float)(r * Math.Cos(phi)), (float)(l * j / k), (float)(r * Math.Sin(phi))]);
                nor.AddRange([(float)Math.Cos(phi), 0f, (float)Math.Sin(phi)]);
            }
        for (int j = 0; j < k; j++)
            for (int i = 0; i < n; i++)
            {
                uint a = (uint)(j * n + i), b = (uint)(j * n + (i + 1) % n), c = (uint)((j + 1) * n + i), d = (uint)((j + 1) * n + (i + 1) % n);
                ind.AddRange([a, c, b, b, c, d]);
            }
        return ([.. pos], [.. nor], [.. ind]);
    }

    [Fact]
    public void Tischstufe_fasst_feine_Netze_zusammen_und_behaelt_grosse_Flaechen()
    {
        // feines Rohr (2 cm Radius, 48 Strecken, 50 Ringe auf 1 m): mit 4 cm Zellen bleibt fast nichts, mit 1 mm fast alles
        var (p, n, i) = Zylinder(0.02, 1, 48, 50);
        var vor = i.Length / 3;
        var grob = TischStufe.Vereinfachen(p, n, i, 0.04);
        var fein = TischStufe.Vereinfachen(p, n, i, 0.001);
        aus.WriteLine($"Rohr {vor} Dreiecke: 4 cm → {grob.Dreiecke}, 1 mm → {fein.Dreiecke}");
        Assert.True(grob.Dreiecke <= vor / 5);
        Assert.True(fein.Dreiecke > vor * 0.9);
        Assert.All(grob.Indizes, x => Assert.True(x < grob.Ecken));
        // Zelle 0: unverändert
        Assert.Same(i, TischStufe.Vereinfachen(p, n, i, 0).Indizes);
        // ein grosser Quader (Wand 4 × 2.5 × 0.2 m) mit 4 cm Zellen: alle 12 Dreiecke bleiben, in derselben Umlaufrichtung
        var wand = ModellHilfen.Quadernetz("architektur", ("w", "wand", V(0, 0, -0.2), V(4, 2.5, 0)));
        var v = TischStufe.Vereinfachen(wand.Positionen, wand.Normalen, wand.Indizes, 0.04);
        Assert.Equal(12, v.Dreiecke);
        Vec3 Fl(float[] pp, uint[] ii, int t)
        {
            Vec3 E(uint k) => V(pp[3 * k], pp[3 * k + 1], pp[3 * k + 2]);
            return (E(ii[3 * t + 1]) - E(ii[3 * t])).Kreuz(E(ii[3 * t + 2]) - E(ii[3 * t])).Einheit();
        }
        for (int t = 0; t < 12; t++) Nah(Fl(v.Positionen, v.Indizes, t).Skalar(Fl(wand.Positionen, wand.Indizes, t)), 1, 1e-4);
        // nur die IFC-Unterlage wird vereinfacht
        Assert.True(TischStufe.Vereinfacht("unterlage"));
        Assert.False(TischStufe.Vereinfacht("modell"));
        Assert.Equal(27, Enumerable.Range(0, 27).Select(k => TischStufe.Richtung((k / 9 - 1) * 0.9f, (k / 3 % 3 - 1) * 0.9f, (k % 3 - 1) * 0.9f)).Distinct().Count());
    }

    [Fact]
    public void Tischstufe_nach_Massstab_und_Budget_mit_Hysterese()
    {
        long[] d = [861_291, 340_888, 240_655, 151_973];   // Test_praxis, alle Etagen mit Unterlage (Echt-Test XrV1dEchtTests)
        var e = TischStufeEinstellungen.Vorgabe;
        Assert.Equal(1, e.VereinfachenMm);
        Assert.Equal(350_000, e.DreieckeMax);
        Assert.Equal([0, 0.01, 0.02, 0.04], TischStufe.ZellenM);
        Assert.Equal(1, TischStufe.StufeWaehlen(d, 19, e));     // 1 cm = 0.53 mm, 2 cm = 1.05 mm > 1 mm
        Assert.Equal(3, TischStufe.StufeWaehlen(d, 50, e));     // 4 cm = 0.8 mm
        Assert.Equal(3, TischStufe.StufeWaehlen(d, 200, e));
        Assert.Equal(1, TischStufe.StufeWaehlen(d, 10, e));     // 1 cm = 1 mm genau
        Assert.Equal(1, TischStufe.StufeWaehlen(d, 5, e));      // fein wäre 0, aber 861 291 > 350 000: die nächste, die das Budget hält
        Assert.Equal(3, TischStufe.StufeWaehlen(d, 5, e with { DreieckeMax = 200_000 }));
        Assert.Equal(0, TischStufe.StufeWaehlen([500, 300, 200, 100], 5, e));   // kleines Modell: fein, wo es sichtbar wäre
        Assert.Equal(0, TischStufe.StufeWaehlen([1_000_000], 50, e));          // ohne Stufen
        // Hysterese: bei 1:38 bleibt die Stufe 4 cm (1.05 mm < 1.15 mm), ohne bisherige Stufe gilt 2 cm; bei 1:34 (1.18 mm) wird es feiner
        Assert.Equal(2, TischStufe.StufeWaehlen(d, 38, e));
        Assert.Equal(3, TischStufe.StufeWaehlen(d, 38, e, bisher: 3));
        Assert.Equal(2, TischStufe.StufeWaehlen(d, 34, e, bisher: 3));
        Assert.Equal(2, TischStufe.StufeWaehlen(d, 42, e, bisher: 2));   // 4 cm = 0.95 mm, aber gröber erst unter 0.85 mm
        Assert.Equal(3, TischStufe.StufeWaehlen(d, 48, e, bisher: 2));
    }

    // ================================================================================================ 8. Fang mit räumlichem Index = Fang bis V1a

    static ModellSzene FangSzene()
    {
        var zufall = new Random(7);
        var boxen = new List<(string, string?, Vec3, Vec3)>
        {
            ("w1", "wand", V(0, 0, -0.2), V(4, 2.5, 0)), ("w2", "wand", V(0, 0, -4), V(0.2, 2.5, 0)), ("d1", "decke", V(0, 2.5, -4), V(4, 2.7, 0)),
        };
        for (int i = 0; i < 40; i++)
        {
            var p = V(zufall.NextDouble() * 4, zufall.NextDouble() * 2.3, -zufall.NextDouble() * 4);
            var s = V(0.05 + zufall.NextDouble() * 0.4, 0.05 + zufall.NextDouble() * 0.4, 0.05 + zufall.NextDouble() * 0.4);
            boxen.Add(($"b{i}", "moebel", p, p + s));
        }
        return V1aMessenTests.Szene(ModellHilfen.Quadernetz("architektur", [.. boxen]), V1aMessenTests.Rohr("r1", V(0.5, 2.2, -1), V(3.5, 2.2, -1), 0.05),
            V1aMessenTests.Rohr("r2", V(1, 0.3, -3), V(1, 2.0, -3), 0.03), V1aMessenTests.Rohr("r3", V(0.4, 1.0, -0.5), V(0.4, 1.0, -3.5), 0.1, 12));
    }

    static void Gleich(FangErgebnis a, FangErgebnis b, string text)
    {
        Assert.True(a.Art == b.Art, $"{text}: {a.Art} ≠ {b.Art}");
        Assert.True(a.Id == b.Id, $"{text}: {a.Id} ≠ {b.Id}");
        Assert.True(a.P.HasValue == b.P.HasValue, text);
        if (a.P is { } pa && b.P is { } pb) Assert.True(pa.Abstand(pb) < 1e-9, $"{text}: {pa} ≠ {pb}");
        Assert.True(a.T.HasValue == b.T.HasValue && (a.T is null || Math.Abs(a.T.Value - b.T!.Value) < 1e-9), text);
    }

    [Fact]
    public void Fang_mit_Index_findet_dasselbe_wie_der_Fang_bis_V1a()
    {
        var sz = FangSzene();
        var f = new SzenenFang(sz);
        var r = new FangReferenz(sz);
        Assert.False(f.Bereit);
        f.Aufbauen();
        Assert.True(f.Bereit);
        var (st, pu, sr) = f.Umfang;
        aus.WriteLine($"Index: {st} Stücke, {pu} Punkte, {sr} Strecken in {f.AufbauMs:0.0} ms");
        Assert.True(st > 0 && pu > 0 && sr > 0);
        f.Aufbauen();   // einmal
        var zufall = new Random(11);
        Vec3 Z(double x0, double x1, double y0, double y1, double z0, double z1) =>
            V(x0 + zufall.NextDouble() * (x1 - x0), y0 + zufall.NextDouble() * (y1 - y0), z0 + zufall.NextDouble() * (z1 - z0));
        foreach (var (name, filter, sichtbar) in new (string, Func<SzenenElement, bool>?, Func<Vec3, bool>?)[]
            { ("ohne", null, null), ("Filter", e => e.Id != "w1" && e.Id != "r1", null), ("Schnitt", null, p => p.Y < 2.0) })
        {
            f.Filter = r.Filter = filter;
            f.Sichtbar = r.Sichtbar = sichtbar;
            for (int i = 0; i < 300; i++)
            {
                var p = Z(-0.3, 4.3, -0.2, 2.9, -4.3, 0.3);
                Gleich(f.Punkt(p), r.Punkt(p), $"{name} Punkt {i}");
                Gleich(f.Punkt(p, 0.3), r.Punkt(p, 0.3), $"{name} Punkt 0.3 {i}");
            }
            for (int i = 0; i < 300; i++)
            {
                var u = Z(-1, 5, 0.2, 2.6, -5, 1);
                var richtung = Z(-1, 1, -1, 1, -1, 1);
                Gleich(f.Strahl(u, richtung), r.Strahl(u, richtung), $"{name} Strahl {i}");
                // wie am Tisch im Massstab 1:50: Radius 1.5 m + 3 % des Wegs, Reichweite 200 m
                Gleich(f.Strahl(u, richtung, 1.5, 0.03, 200), r.Strahl(u, richtung, 1.5, 0.03, 200), $"{name} Strahl Tisch {i}");
            }
        }
        Assert.Equal(sz.Elemente.Count, f.Aufbereitet);
    }

    [Fact]
    public void Fang_im_Hintergrund_rechnet_die_neueste_Anfrage_je_Hand_und_das_Bild_wartet_nie()
    {
        var sz = FangSzene();
        var f = new SzenenFang(sz);
        f.Aufbauen();
        var schlange = new Queue<Action>();
        var h = new FangImHintergrund(f, schlange.Enqueue);
        var a1 = new FangAnfrage(V(1, 1, -1), 0.03, V(-0.5, 1.2, 0.5), V(1, 0, -1));
        var a2 = new FangAnfrage(V(2, 1, -2), 0.03, V(-0.5, 1.2, 0.5), V(1, 0.1, -1));
        var b = new FangAnfrage(V(0.4, 1.0, -2), 0.3);
        h.Anfragen(1, a1);
        Assert.Null(h.Letztes(1));   // noch nicht gerechnet: das Bild bekommt nichts, wartet aber nicht
        Assert.True(h.Wartet(1) && h.Laeuft);
        h.Anfragen(1, a2);   // ersetzt a1 (nur die neueste zählt)
        h.Anfragen(0, b);
        Assert.Single(schlange);   // eine Arbeit zur Zeit
        schlange.Dequeue()();
        Assert.Empty(schlange);
        Assert.Equal(2, h.Abfragen);
        Assert.Equal(1, h.Ersetzt);
        Assert.False(h.Laeuft || h.Wartet(0) || h.Wartet(1));
        Assert.Same(a2, h.Letztes(1)!.Value.Anfrage);
        Gleich(a2.Rechnen(f), h.Letztes(1)!.Value.Ergebnis, "rechts");
        Assert.Same(b, h.Letztes(0)!.Value.Anfrage);
        Gleich(b.Rechnen(f), h.Letztes(0)!.Value.Ergebnis, "links");
        // die Hand geht weg, während ihre Abfrage läuft (der Filter ruft hier Vergessen): das Ergebnis von davor zählt nicht
        var einmal = true;
        f.Filter = e => { if (einmal) { einmal = false; h.Vergessen(0); } return true; };
        h.Anfragen(0, b);
        schlange.Dequeue()();
        Assert.Null(h.Letztes(0));
        Assert.Same(a2, h.Letztes(1)!.Value.Anfrage);
        // ohne Strahl und ohne Treffer: frei ohne Punkt (kein Punkt in der Luft)
        var leer = new FangAnfrage(V(20, 20, 20), 0.03);
        Assert.Equal(Fangart.Frei, leer.Rechnen(f).Art);
        Assert.Null(leer.Rechnen(f).P);
    }

    [Fact]
    public void Fang_Anfrage_gilt_als_unveraendert_bis_2_mm_mal_Massstab_und_0_3_Grad_bei_gleichem_Filter()
    {
        var a = new FangAnfrage(V(1, 1, 1), 0.03, V(0, 1, 0), V(0, 0, -1), Filter: "b|eg|False");
        Assert.True(FangAnfrage.Unveraendert(a, a with { Punkt = V(1.0015, 1, 1) }));
        Assert.False(FangAnfrage.Unveraendert(a, a with { Punkt = V(1.003, 1, 1) }));
        Assert.False(FangAnfrage.Unveraendert(a, a with { StrahlU = V(0, 1.003, 0) }));
        var w = 0.2 * Math.PI / 180;
        Assert.True(FangAnfrage.Unveraendert(a, a with { StrahlR = V(Math.Sin(w), 0, -Math.Cos(w)) }));
        w = 0.5 * Math.PI / 180;
        Assert.False(FangAnfrage.Unveraendert(a, a with { StrahlR = V(Math.Sin(w), 0, -Math.Cos(w)) }));
        Assert.True(FangAnfrage.Unveraendert(a, a with { StrahlR = V(0, 0, -3) }));   // Länge der Richtung zählt nicht
        Assert.False(FangAnfrage.Unveraendert(a, a with { Filter = "b|og|False" }));
        Assert.False(FangAnfrage.Unveraendert(a, a with { StrahlU = null, StrahlR = null }));
        // am Tisch 1:50: 2 mm im Raum der Brille = 0.1 m in der Szene
        var t = a with { Massstab = 50, RadiusFinger = 1.5 };
        Assert.True(FangAnfrage.Unveraendert(t, t with { Punkt = V(1.09, 1, 1) }));
        Assert.False(FangAnfrage.Unveraendert(t, t with { Punkt = V(1.11, 1, 1) }));
        Assert.False(FangAnfrage.Unveraendert(a, t));
    }

    [Fact]
    public void Fang_im_Hintergrund_auf_dem_Pool_gleich_wie_im_Bild_auch_mit_Abfragen_daneben()
    {
        var sz = FangSzene();
        var f = new SzenenFang(sz);
        f.Aufbauen();
        var h = new FangImHintergrund(f);
        var zufall = new Random(23);
        for (int i = 0; i < 200; i++)
        {
            var a = new FangAnfrage(V(zufall.NextDouble() * 4, zufall.NextDouble() * 2.5, -zufall.NextDouble() * 4), 0.05, V(2, 1.2, 1),
                V(zufall.NextDouble() - 0.5, zufall.NextDouble() - 0.5, -1));
            var hand = i % 2;
            h.Anfragen(hand, a);
            var direkt = a.Rechnen(f);   // daneben im «Bild» (so fragt die Prüfung)
            var uhr = System.Diagnostics.Stopwatch.StartNew();
            while ((h.Wartet(hand) || h.Laeuft) && uhr.ElapsedMilliseconds < 5000) Thread.Yield();
            var l = h.Letztes(hand);
            Assert.NotNull(l);
            Assert.Same(a, l.Value.Anfrage);
            Gleich(direkt, l.Value.Ergebnis, $"Abfrage {i}");
        }
        Assert.Equal(200, h.Abfragen);
        Assert.Null(h.Fehler);
        aus.WriteLine($"im Hintergrund ⌀ {h.SummeMs / h.Abfragen:0.000} ms, höchstens {h.MaxMs:0.000} ms");
    }

    [Fact]
    public void BoxBaum_findet_alle_Boxen_im_Umkreis_und_am_Strahl()
    {
        var zufall = new Random(3);
        var boxen = new List<Box3>();
        for (int i = 0; i < 500; i++)
        {
            var p = V(zufall.NextDouble() * 10, zufall.NextDouble() * 10, zufall.NextDouble() * 10);
            boxen.Add(new Box3(p, p + V(zufall.NextDouble(), zufall.NextDouble() * 0.2, zufall.NextDouble() * 0.5)));
        }
        boxen.Add(Box3.Leer);
        var b = new BoxBaum(boxen);
        Assert.Equal(500, b.Eintraege);
        for (int k = 0; k < 50; k++)
        {
            var p = V(zufall.NextDouble() * 10, zufall.NextDouble() * 10, zufall.NextDouble() * 10);
            var r = zufall.NextDouble() * 2;
            var gefunden = new HashSet<int>();
            b.Nahe(p, r, i => { gefunden.Add(i); return double.PositiveInfinity; });
            var soll = Enumerable.Range(0, 500).Where(i => boxen[i].Abstand(p) <= r).ToHashSet();
            Assert.True(soll.IsSubsetOf(gefunden), $"Umkreis {k}");
            var u = V(-1, zufall.NextDouble() * 10, zufall.NextDouble() * 10);
            var richtung = V(1, zufall.NextDouble() - 0.5, zufall.NextDouble() - 0.5).Einheit();
            var amStrahl = new HashSet<int>();
            b.Strahl(u, richtung, 8, 0.1, (i, _) => { amStrahl.Add(i); return double.PositiveInfinity; });
            var sollS = Enumerable.Range(0, 500).Where(i => SzenenFang.BoxStrahl(boxen[i], u, richtung, 0.1) is { } t && t <= 8).ToHashSet();
            Assert.True(sollS.IsSubsetOf(amStrahl), $"Strahl {k}");
        }
        Assert.Equal(0, new BoxBaum([]).Eintraege);
    }

    // ================================================================================================ 9. Stellschrauben und Geisterhände

    [Fact]
    public void Neue_Stellschrauben_V1d_mit_Vorgaben_und_Grenzen()
    {
        var e = XrBedienEinstellungen.Vorgabe;
        foreach (var (k, vorgabe, gut, schlecht) in new (string, double, double, double)[]
        {
            ("symbole.hoehe_sitzend_m", 0.08, 0.12, 0.5), ("symbole.hoehe_tisch_m", 0.52, 0.4, 1.2), ("symbole.wahl_frei_s", 0.3, 0.5, 3), ("tafel.hoehe_m", 0.15, 0.2, 0.7),
            ("tafel.nachfuehren_grad", 25, 40, 100), ("tafel.blaettern_abstand_m", 0.05, 0.08, 0.3), ("tafel.blaettern_weg_m", 0.08, 0.1, 0.01),
            ("sitzung.drehknopf_grad", 60, 90, 10), ("sitzung.vereinfachen_mm", 1, 2, 6), ("sitzung.dreiecke_max", 350_000, 500_000, 10_000),
        })
        {
            Assert.Contains(k, XrBedienEinstellungen.Schluessel);
            var neu = e.MitStellschraube(k, W(gut.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            Assert.NotNull(neu);
            Assert.Null(e.MitStellschraube(k, W(schlecht.ToString(System.Globalization.CultureInfo.InvariantCulture))));
            Assert.Equal(e, neu!.MitStellschraube(k, W("null")));
            Assert.True(XrBedienEinstellungen.Grenzen[k].Min <= vorgabe && vorgabe <= XrBedienEinstellungen.Grenzen[k].Max, k);
        }
        Assert.Equal(0.12, e.MitStellschraube("symbole.hoehe_sitzend_m", W("0.12"))!.Symbole.HoeheSitzendM);
        Assert.Equal(40, e.MitStellschraube("tafel.nachfuehren_grad", W("40"))!.Tafel.NachfuehrenGrad);
        Assert.Equal(500_000, e.MitStellschraube("sitzung.dreiecke_max", W("500000"))!.Tisch.DreieckeMax);
        Assert.Equal(90, e.MitStellschraube("sitzung.drehknopf_grad", W("90"))!.Griff.DrehknopfGrad);
        Assert.Equal(Varianten.TafelFest, e.MitStellschraube("labor.variante.tafel_nachfuehren", W("\"fest\""))!.Variante("tafel_nachfuehren"));
        Assert.Equal(Varianten.ZoomDrehknopf, e.MitStellschraube("labor.variante.zoom_ein_griff", W("\"drehknopf\""))!.Griff.Zoom);
        Assert.Null(e.MitStellschraube("labor.variante.zoom_ein_griff", W("\"zwei_griffe\"")));
        Assert.Equal("traege", e.Variante("tafel_nachfuehren"));
        Assert.Equal("zweite_hand", e.Variante("zoom_ein_griff"));
    }

    [Fact]
    public void Geisterhand_zeigt_Zoomen_mit_der_zweiten_Hand_und_den_Drehknopf()
    {
        var hand = HandHilfen.Hand("left", V(-0.2, 1.0, -0.4), V(0, 0, -1), V(0, -1, 0));
        var ziel = new TabletPose(V(0, 0.8, -0.7), V(0, 1, 0), V(-1, 0, 0), V(0, 0, -1), 0.1, 0.1);
        var z0 = Geisterhand.Bild(GeisterGeste.Zoomen, 0.01, hand, ziel)!;
        var z1 = Geisterhand.Bild(GeisterGeste.Zoomen, Geisterhand.DauerS / 2 * 0.62, hand, ziel)!;
        Assert.True(z0.Gelenke["thumb-tip"].Abstand(z0.Gelenke["index-finger-tip"]) < 0.02);   // Pinch
        Assert.True((z1.Gelenke["index-finger-tip"] - z0.Gelenke["index-finger-tip"]).Skalar(ziel.Rechts) > 0.12);   // auseinander
        var rechts = HandHilfen.Hand("right", V(0.1, 1.0, -0.4), V(0, 0, -1), V(0, -1, 0));
        var d0 = Geisterhand.Bild(GeisterGeste.Drehknopf, 0.01, rechts, ziel)!;
        var d1 = Geisterhand.Bild(GeisterGeste.Drehknopf, Geisterhand.DauerS / 4, rechts, ziel)!;
        var r0 = EinGriff.Rollen(d0.Gelenke, "right")!.Value;
        var r1 = EinGriff.Rollen(d1.Gelenke, "right")!.Value;
        Nah(Wd(r1, r0), 60 * Grad, 2 * Grad);   // im Uhrzeigersinn = grösser
    }
}
