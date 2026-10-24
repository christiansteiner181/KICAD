// Tests Quest App V1a, Vorgaben Christian 24.10.2026: Hebegeste (beide Handflächen nach oben, deutlich nach oben streichen) holt frei schwebende App-Symbole in ein
// Feld frontal etwa 0.6 m vor den Augen über etwa 0.6 m Höhe in höchstens drei Reihen; Anwahl durch Berührung mit der ganzen Hand; Zeiger mit Tooltip; nach der Wahl
// bleiben die Symbole stehen (Feld weicht hinter die App zurück); EINE Geste «Wegwischen» (rechte Hand offen, Handfläche nach links, von rechts nach links) für alles,
// mit der Regel für ihr Ziel; Hinweise frei schwebend, im Blick nie über Apps, Symbolen, Geisterhänden oder dem Modell. Auch Fehlauslösungen: eine Hand, Pinch,
// Handflächen falsch, zu langsam, nicht gleichzeitig, seitlich, Ausholen; und die Gesten lösen weder Pinch + Wisch noch Drehen noch den Sprung aus.

using System.Text.Json;
using Hlks.Xr.Bedienung;
using static Hlks.Xr.Tests.T;

namespace Hlks.Xr.Tests;

public class V1aSymboleTests
{
    static readonly GlasMasse M = GlasMasse.Fuer(new AppEinstellungen());
    static readonly Vec3 Auge = V(0, 1.6, 0);
    static JsonElement Wert(string j) => JsonDocument.Parse(j).RootElement.Clone();
    const double Blick = Math.PI / 2;   // nach −z

    /// <summary>Eine Hand (Handflächenmitte, Normale) als Beobachtung.</summary>
    static HandBeobachtung H(double x, double y, double ny = 1, bool offen = true, bool pinch = false, double z = -0.35) =>
        new(V(x, y, z), V(0, ny, Math.Sqrt(Math.Max(0, 1 - ny * ny))), offen, pinch);

    /// <summary>Bilder mit 72 Hz: beide Hände steigen von y0 um weg in dauer s (rechts mit Versatz s später); gibt die Ereignisse und den Stand jedes Bildes.</summary>
    static List<HebeStand> Geste(HebeZustand z, double t0, double dauer, double weg, double versatz = 0, double ny = 1, bool offen = true, bool pinch = false,
        double seitlich = 0, bool nurLinks = false, double t1 = 1.4)
    {
        var aus = new List<HebeStand>();
        for (double t = 0; t <= t1 + 1e-9; t += 1 / 72.0)
        {
            double Y(double v) => 1.0 + weg * GlasStil.Weich(Math.Clamp((t - v) / dauer, 0, 1));
            double X(double v) => seitlich * GlasStil.Weich(Math.Clamp((t - v) / dauer, 0, 1));
            var l = H(-0.2 - X(0), Y(0), ny, offen, pinch);
            var r = nurLinks ? H(0.2, 1.0, ny, offen, pinch) : H(0.2 + X(versatz), Y(versatz), ny, offen, pinch);
            aus.Add(HebeGeste.Schritt(z, t0 + t, l, r, Blick));
        }
        return aus;
    }

    // ================================================================================================ Hebegeste

    [Fact]
    public void Hebegeste_beide_Handflächen_oben_deutlich_nach_oben_holt_die_Symbole_einmal()
    {
        var z = new HebeZustand();
        var s = Geste(z, 10, 0.5, 0.22);
        Assert.Equal(1, s.Count(x => x.Ereignis == HebeArt.Heben));
        // der Fortschritt steigt mit den Händen (für das Hineinheben der Symbole), vor dem Ereignis
        var vor = s.TakeWhile(x => x.Ereignis is null).ToList();
        Assert.True(vor.Count > 5);
        Assert.Equal(0, vor[0].Fortschritt, 6);
        Assert.True(vor[^1].Fortschritt > 0.8);
        for (int i = 1; i < vor.Count; i++) Assert.True(vor[i].Fortschritt >= vor[i - 1].Fortschritt - 1e-9);
        Assert.True(vor[^1].Hoehe > 1.1);
        // Stellschrauben: ein längerer Weg verlangt mehr
        var lang = new HebeZustand();
        var e = HebeEinstellungen.Vorgabe with { WegM = 0.3 };
        var ereignisse = 0;
        for (double t = 0; t <= 1.4; t += 1 / 72.0)
        {
            var y = 1.0 + 0.22 * GlasStil.Weich(Math.Clamp(t / 0.5, 0, 1));
            if (HebeGeste.Schritt(lang, 20 + t, H(-0.2, y), H(0.2, y), Blick, e).Ereignis is not null) ereignisse++;
        }
        Assert.Equal(0, ereignisse);
    }

    [Fact]
    public void Fehlauslösungen_eine_Hand_Pinch_Faust_Handflächen_nicht_oben_zu_langsam_nicht_gleichzeitig_seitlich()
    {
        static int Heben(List<HebeStand> s) => s.Count(x => x.Ereignis == HebeArt.Heben);
        Assert.Equal(0, Heben(Geste(new HebeZustand(), 0, 0.5, 0.22, nurLinks: true)));                 // nur eine Hand
        Assert.Equal(0, Heben(Geste(new HebeZustand(), 0, 0.5, 0.22, pinch: true)));                    // Pinch
        Assert.Equal(0, Heben(Geste(new HebeZustand(), 0, 0.5, 0.22, offen: false)));                   // Faust
        Assert.Equal(0, Heben(Geste(new HebeZustand(), 0, 0.5, 0.22, ny: 0.2)));                        // Handflächen zueinander bzw. vorn (78° neben oben)
        Assert.Equal(0, Heben(Geste(new HebeZustand(), 0, 0.5, 0.22, ny: -1)));                         // Handflächen nach unten
        Assert.Equal(0, Heben(Geste(new HebeZustand(), 0, 2.5, 0.22, t1: 3)));                          // zu langsam (0.9 s)
        Assert.Equal(0, Heben(Geste(new HebeZustand(), 0, 0.3, 0.22, versatz: 0.7)));                  // nacheinander (0.35 s)
        Assert.Equal(0, Heben(Geste(new HebeZustand(), 0, 0.5, 0.16, seitlich: 0.3)));                  // eher zur Seite als nach oben
        Assert.Equal(0, Heben(Geste(new HebeZustand(), 0, 0.5, 0.08)));                                 // zu kurz (0.15 m)
        Assert.Equal(1, Heben(Geste(new HebeZustand(), 0, 0.4, 0.22, versatz: 0.2)));                  // leicht versetzt zählt
    }

    [Fact]
    public void Gezögert_beide_Handflächen_oben_ruhig_einmal_dann_Sperre_nach_dem_Ereignis()
    {
        var z = new HebeZustand();
        var gezoegert = 0;
        for (double t = 0; t <= 3; t += 1 / 72.0)
            if (HebeGeste.Schritt(z, t, H(-0.2, 1.0), H(0.2, 1.0), Blick).Zoegert) gezoegert++;
        Assert.Equal(1, gezoegert);
        // nach einem Heben zählt die Geste SperreS lang nicht
        var z2 = new HebeZustand();
        var s = Geste(z2, 0, 0.3, 0.22, t1: 0.6);
        Assert.Equal(1, s.Count(x => x.Ereignis == HebeArt.Heben));
        Assert.DoesNotContain(Geste(z2, 0.62, 0.3, 0.22, t1: 0.5), x => x.Ereignis is not null);
    }

    [Fact]
    public void Die_Hebegeste_löst_weder_Pinch_Wisch_noch_den_Sprung_noch_das_Wegwischen_aus()
    {
        // dieselben Hände (offen, Handfläche oben, ohne Pinch) durch die Regeln des Begehens, des Wischs und des Wegwischens
        var wisch = new PinchWischZustand();
        var links = new HandBegehenZustand();
        var rechts = new HandBegehenZustand();
        var weg = new WegwischZustand();
        var gesten = new List<BegehenEreignis>();
        for (double t = 0; t <= 1.2; t += 1 / 72.0)
        {
            var y = 1.0 + 0.25 * GlasStil.Weich(Math.Clamp(t / 0.4, 0, 1));
            var gl = Geisterhand.VorlageOffen(V(-0.2, y, -0.35), V(0, 0, -1), true);
            var gr = Geisterhand.VorlageOffen(V(0.2, y, -0.35), V(0, 0, -1), false);
            Assert.True(HandBeobachtung.Aus(gl, "left")!.Value.Normale.Y > 0.9);   // die Vorlage zeigt die Handfläche nach oben
            Assert.True(HandBeobachtung.Aus(gr, "right") is { Offen: true, Pinch: false });
            Assert.Null(XrGesten.PinchWischSchritt(wisch, t, gr["index-finger-tip"], XrTabletLogik.IstPinch(gr), Blick, null, 1));
            gesten.AddRange(HandBegehen.Schritt(links, t, "left", gl, Auge, Blick, 0));
            gesten.AddRange(HandBegehen.Schritt(rechts, t, "right", gr, Auge, Blick, 0));
            Assert.False(Wegwischen.Schritt(weg, t, HandBeobachtung.Aus(gr, "right"), Blick).Ereignis);
        }
        Assert.DoesNotContain(gesten, e => e.Art is BegehenArt.Teleport or BegehenArt.Wisch or BegehenArt.Drehen or BegehenArt.PinchAn);
        // und die Hebegeste selbst erkennt diese Hände
        var z = new HebeZustand();
        var heben = 0;
        for (double t = 0; t <= 1.2; t += 1 / 72.0)
        {
            var y = 1.0 + 0.25 * GlasStil.Weich(Math.Clamp(t / 0.4, 0, 1));
            var l = HandBeobachtung.Aus(Geisterhand.VorlageOffen(V(-0.2, y, -0.35), V(0, 0, -1), true), "left");
            var r = HandBeobachtung.Aus(Geisterhand.VorlageOffen(V(0.2, y, -0.35), V(0, 0, -1), false), "right");
            if (HebeGeste.Schritt(z, t, l, r, Blick).Ereignis == HebeArt.Heben) heben++;
        }
        Assert.Equal(1, heben);
    }

    // ================================================================================================ Wegwischen

    /// <summary>
    /// Die rechte Hand (Blick −z: links = −x) wischt um weg (m, positiv = nach links) in dauer s; vorher holt sie ggf. um ausholen (m) nach rechts aus. Handfläche:
    /// nx = Anteil nach links (Rest nach vorn), hoch = Weg nach oben dabei. Gibt die Stände je Bild.
    /// </summary>
    static List<WegwischStand> Wischen(WegwischZustand z, double dauer, double weg, double nx = 1, bool offen = true, bool pinch = false, double hoch = 0,
        double ausholen = 0, double t1 = 1.2, WegwischEinstellungen? e = null, double t0 = 0)
    {
        var aus = new List<WegwischStand>();
        Vec3 N() => V(-nx, 0, -Math.Sqrt(Math.Max(0, 1 - nx * nx)));
        for (double t = 0; t <= t1 + 1e-9; t += 1 / 72.0)
        {
            var a = ausholen > 0 ? GlasStil.Weich(Math.Clamp(t / 0.25, 0, 1)) * ausholen : 0;
            var tw = ausholen > 0 ? t - 0.3 : t;
            var f = GlasStil.Weich(Math.Clamp(tw / dauer, 0, 1));
            var h = new HandBeobachtung(V(0.15 + a - weg * f, 1.2 + hoch * f, -0.35), N(), offen, pinch);
            aus.Add(Wegwischen.Schritt(z, t0 + t, h, Blick, e));
        }
        return aus;
    }

    [Fact]
    public void Wegwischen_rechte_Hand_offen_Handfläche_links_von_rechts_nach_links_einmal_mit_mitlaufendem_Weg()
    {
        var s = Wischen(new WegwischZustand(), 0.3, 0.3);
        Assert.Equal(1, s.Count(x => x.Ereignis));
        var vor = s.TakeWhile(x => !x.Ereignis).ToList();
        Assert.Contains(vor, x => x.Laeuft);
        for (int i = 1; i < vor.Count; i++) Assert.True(vor[i].Weg >= vor[i - 1].Weg - 1e-9);   // der Weg nach links wächst – das Ziel läuft mit
        Assert.True(vor[^1].Weg is > 0.1 and < 0.18 + 1e-9);
        var ereignis = s.First(x => x.Ereignis);
        Assert.True(ereignis.Pfad.Count >= 2 && ereignis.Pfad[^1].X < ereignis.Pfad[0].X);   // der Weg der Hand für die Wahl des Ziels
        Assert.Equal(V(-1, 0, 0), Wegwischen.Links(Blick).Einheit() is var l ? V(Math.Round(l.X, 9), Math.Round(l.Y, 9), Math.Round(l.Z, 9)) : default);
    }

    [Fact]
    public void Wegwischen_Fehlauslösungen_Ausholen_nach_rechts_Pinch_Faust_Handfläche_falsch_zu_kurz_zu_langsam_schräg_linke_Hand()
    {
        static int W(List<WegwischStand> s) => s.Count(x => x.Ereignis);
        Assert.Equal(0, W(Wischen(new WegwischZustand(), 0.3, -0.3)));                 // von links nach rechts (Ausholen) löst nichts aus
        Assert.Equal(0, W(Wischen(new WegwischZustand(), 0.3, 0.3, pinch: true)));      // Pinch (Pinch + Wisch)
        Assert.Equal(0, W(Wischen(new WegwischZustand(), 0.3, 0.3, offen: false)));     // Faust
        Assert.Equal(0, W(Wischen(new WegwischZustand(), 0.3, 0.3, nx: -1)));          // Handfläche nach rechts (Handrücken voran)
        Assert.Equal(0, W(Wischen(new WegwischZustand(), 0.3, 0.3, nx: 0.3)));         // Handfläche nach vorn (73° neben links)
        Assert.Equal(0, W(Wischen(new WegwischZustand(), 0.3, 0.1)));                  // zu kurz
        Assert.Equal(0, W(Wischen(new WegwischZustand(), 1.5, 0.3, t1: 2.5)));         // zu langsam (0.6 s)
        Assert.Equal(0, W(Wischen(new WegwischZustand(), 0.3, 0.2, hoch: 0.3)));       // eher nach oben als nach links
        // mit Ausholen nach rechts zählt nur der Weg nach links
        Assert.Equal(1, W(Wischen(new WegwischZustand(), 0.3, 0.3, ausholen: 0.1)));
        // die linke Hand wischt nicht weg (die Geste gehört der rechten Hand)
        var z = new WegwischZustand();
        Assert.Equal(0, Enumerable.Range(0, 80).Count(i => Wegwischen.Schritt(z, i / 72.0, null, Blick).Ereignis));
        // Sperre: eine zweite Bewegung gleich danach zählt nicht
        var zs = new WegwischZustand();
        Assert.Equal(1, W(Wischen(zs, 0.25, 0.3, t1: 0.4)));
        Assert.Equal(0, W(Wischen(zs, 0.25, 0.3, t1: 0.3, t0: 0.42)));
        // Stellschrauben: ein längerer Weg verlangt mehr
        Assert.Equal(0, W(Wischen(new WegwischZustand(), 0.3, 0.3, e: WegwischEinstellungen.Vorgabe with { WegM = 0.4 })));
    }

    [Fact]
    public void Wegwischen_löst_weder_Pinch_Wisch_noch_Drehen_Teleport_noch_die_Hebegeste_aus()
    {
        var wisch = new PinchWischZustand();
        var begehen = new HandBegehenZustand();
        var hebe = new HebeZustand();
        var weg = new WegwischZustand();
        var gesten = new List<BegehenEreignis>();
        var n = 0;
        for (double t = 0; t <= 1.0; t += 1 / 72.0)
        {
            var f = GlasStil.Weich(Math.Clamp(t / 0.3, 0, 1));
            var gr = Geisterhand.VorlageFlach(V(0.15 - 0.3 * f, 1.2, -0.35), V(-1, 0, 0), V(0, 1, 0), links: false);
            var b = HandBeobachtung.Aus(gr, "right")!.Value;
            Assert.True(b.Normale.X < -0.9 && b is { Offen: true, Pinch: false });   // die Vorlage: Handfläche nach links, offen
            Assert.Null(XrGesten.PinchWischSchritt(wisch, t, gr["index-finger-tip"], XrTabletLogik.IstPinch(gr), Blick, null, 1));
            gesten.AddRange(HandBegehen.Schritt(begehen, t, "right", gr, Auge, Blick, 0));
            Assert.Null(HebeGeste.Schritt(hebe, t, null, b, Blick).Ereignis);
            if (Wegwischen.Schritt(weg, t, b, Blick).Ereignis) n++;
        }
        Assert.DoesNotContain(gesten, e => e.Art is BegehenArt.Teleport or BegehenArt.Wisch or BegehenArt.Drehen or BegehenArt.PinchAn);
        Assert.Equal(1, n);
        // die Geisterhand zeigt das Wegwischen: in Richtung «Rechts» des Ziels (die App setzt es nach links)
        var ziel = new TabletPose(V(0.12, 1.2, -0.4), V(0, 0, 1), V(-1, 0, 0), V(0, 1, 0), 0.1, 0.1);
        var hand = Geisterhand.VorlageFlach(V(0.12, 1.2, -0.4), V(-1, 0, 0), V(0, 1, 0), links: false);
        var b0 = Geisterhand.Bild(GeisterGeste.Wegwischen, 0, hand, ziel)!;
        var b1 = Geisterhand.Bild(GeisterGeste.Wegwischen, Geisterhand.DauerS / 2 * 0.6, hand, ziel)!;
        Assert.True(b1.Gelenke["wrist"].X < b0.Gelenke["wrist"].X - 0.15);
    }

    [Fact]
    public void Geisterhand_des_Wegwischens_schräg_zum_Blick_und_eine_gültige_Geste()
    {
        // Befund Prüfbilder 03: genau «Handfläche nach links» stand die Geisterhand auf der Kante (nur ein Strich). Jetzt schräg: man sieht auf den Handrücken.
        var links = Wegwischen.Links(Blick);
        var vor = V(0, 0, -1);
        var hand = Geisterhand.VorlageWisch(V(0.12, 1.28, -0.45), links, vor);
        var b = HandBeobachtung.Aus(hand, "right")!.Value;
        Assert.True(b is { Offen: true, Pinch: false });
        var nachLinks = Math.Acos(Math.Clamp(b.Normale.Skalar(links.Einheit()), -1, 1)) * 180 / Math.PI;
        Nah(nachLinks, Geisterhand.WischSchraegGrad, 3);                              // um 35° aus «nach links» gedreht …
        Assert.True(nachLinks < WegwischEinstellungen.Vorgabe.HandflaecheGrad);       // … innerhalb der Toleranz der Geste
        Assert.True(b.Normale.Skalar(vor) > Math.Sin(25 * Math.PI / 180));            // vom Auge weg: man sieht schräg auf den Handrücken, nicht auf die Kante
        // die Vorführung (Geisterhand.Bild) ist eine Geste, die das Wegwischen erkennt
        var ziel = new TabletPose(V(0.12, 1.28, -0.45), vor * -1, links, V(0, 1, 0), 0.1, 0.1);
        var z = new WegwischZustand();
        var n = 0;
        for (double t = 0; t <= Geisterhand.DauerS / 2; t += 1 / 72.0)
            if (Geisterhand.Bild(GeisterGeste.Wegwischen, t, hand, ziel) is { } g && Wegwischen.Schritt(z, t, HandBeobachtung.Aus(g.Gelenke, "right"), Blick).Ereignis) n++;
        Assert.Equal(1, n);
    }

    static TabletPose Flaeche(double x, double y, double z, double b, double h) => Glas.ImBlick(V(x, y, z), Auge, b, h);

    [Fact]
    public void Ziel_des_Wegwischens_was_die_Hand_überstreicht_sonst_das_zuletzt_Eingeblendete()
    {
        var app = new WegwischKandidat("app", Flaeche(0, 1.4, -0.45, 0.32, 0.25), 20);
        var symbole = new WegwischKandidat("symbole", Flaeche(0, 1.28, -0.6, 0.4, 0.7), 10);
        var pc = new WegwischKandidat("pc", Flaeche(0.8, 1.4, -1.2, 0.8, 0.5), 15);
        // ohne Überstreichen: das zuletzt Eingeblendete (die App, dann die Symbole, dann der PC-Bildschirm nach seinem Einblendzeitpunkt)
        Assert.Equal("app", Wegwischen.Ziel([symbole, pc, app], [], Auge));
        Assert.Equal("pc", Wegwischen.Ziel([symbole, pc], [], Auge));
        Assert.Equal("symbole", Wegwischen.Ziel([symbole], [], Auge));
        Assert.Null(Wegwischen.Ziel([], [V(0, 1.3, -0.35)], Auge));
        // die Hand wischt vor dem PC-Bildschirm (rechts aussen): der Bildschirm, obwohl die App jünger ist
        var vorPc = new[] { V(0.5, 1.4, -0.7), V(0.35, 1.4, -0.7), V(0.2, 1.4, -0.7) };
        Assert.Equal("pc", Wegwischen.Ziel([symbole, pc, app], vorPc, Auge));
        // vor App und Symbolen zugleich (die App steht vor dem Feld): gleich viele Punkte → die jüngere, die App
        var mitte = new[] { V(0.08, 1.45, -0.35), V(0.0, 1.45, -0.35), V(-0.08, 1.45, -0.35) };
        Assert.Equal("app", Wegwischen.Ziel([symbole, app], mitte, Auge));
        // unten am Feld, unter der App (im Blick 48° unter der Waagrechten; die App reicht bis etwa 42°): die Symbole
        var unten = new[] { V(0.06, 1.045, -0.5), V(0.0, 1.045, -0.5), V(-0.06, 1.045, -0.5) };
        Assert.Equal("symbole", Wegwischen.Ziel([symbole, app], unten, Auge));
        // über die ganze Geste bleibt das Ziel (es läuft mit der Hand mit und verliesse sonst ihren Weg – Befund Prüflauf 03), danach wird neu gewählt
        var zz = new WegwischZielZustand();
        Assert.Null(Wegwischen.ZielFuehren(zz, new WegwischStand(false, 0, false, []), [symbole, app], Auge));
        Assert.Equal("app", Wegwischen.ZielFuehren(zz, new WegwischStand(false, 0.05, true, mitte), [symbole, app], Auge));
        var weggerueckt = app with { Flaeche = Flaeche(-0.3, 1.4, -0.45, 0.32, 0.25) };   // die App ist mit der Hand nach links gelaufen
        Assert.Equal("app", Wegwischen.ZielFuehren(zz, new WegwischStand(false, 0.15, true, unten), [symbole, weggerueckt], Auge));
        Assert.Equal("app", Wegwischen.ZielFuehren(zz, new WegwischStand(true, 0.2, false, unten), [symbole, weggerueckt], Auge));   // Ereignis: die App
        Assert.Equal("symbole", Wegwischen.ZielFuehren(zz, new WegwischStand(false, 0.05, true, unten), [symbole], Auge));          // nächste Geste: neu
        Assert.Equal("symbole", Wegwischen.ZielFuehren(zz, new WegwischStand(false, 0.1, true, mitte), [symbole, app], Auge));     // bleibt
        Assert.Null(Wegwischen.ZielFuehren(zz, new WegwischStand(false, 0, false, []), [symbole, app], Auge));                    // abgebrochen
    }

    // ================================================================================================ Symbolfeld

    static readonly string[] Ids = ["status", "etagen", "messen", "marker", "ort", "ebenen"];

    [Fact]
    public void Symbolfeld_frontal_60_cm_vor_den_Augen_über_60_cm_Höhe_in_höchstens_drei_versetzten_Reihen_jedes_Mal_gleich()
    {
        var e = new SymbolEinstellungen();
        Assert.Equal(0.6, e.AbstandM);
        Assert.Equal(0.6, e.FeldHoeheM);
        Assert.Equal(3, e.Reihen);
        var p = AppSymbole.Plaetze(Ids, Auge, Blick, e, M);
        Assert.Equal(Ids, p.Select(x => x.Id));
        // jedes Mal gleich (Muskelgedächtnis), auch in anderer Reihenfolge und mit weniger Apps
        var q = AppSymbole.Plaetze([.. Ids.Reverse()], Auge, Blick, e, M);
        foreach (var s in p) Nah(q.Single(x => x.Id == s.Id).Lage.Mitte.Abstand(s.Lage.Mitte), 0, 1e-12);
        var nurStatus = AppSymbole.Plaetze(["status"], Auge, Blick, e, M);
        Nah(nurStatus[0].Lage.Mitte.Abstand(p.Single(x => x.Id == "status").Lage.Mitte), 0, 1e-12);
        Nah(AppSymbole.Plaetze(["tisch"], Auge, Blick, e, M)[0].Lage.Mitte.Abstand(p.Single(x => x.Id == "ort").Lage.Mitte), 0, 1e-12);   // Tisch = Platz von Ort
        foreach (var s in p)
        {
            Nah(-s.Lage.Mitte.Z, 0.6, 0.031);                                          // 0.6 m vor den Augen (Bezug: Mitte der Augen, waagrecht in Körperrichtung)
            Assert.InRange(s.Lage.Mitte.X, -0.25, 0.25);                               // frontal
            Assert.True(s.Lage.Mitte.Y <= Auge.Y - 0.019);                             // die oberste Reihe nicht über Augenhöhe
            Nah(s.Lage.Normale.Skalar((Auge - s.Lage.Mitte).Einheit()), 1, 1e-9);     // zum Kopf gewandt
        }
        Nah(p.Average(x => x.Lage.Mitte.X), 0, 0.03);                                  // zentriert
        var hoehen = p.Select(x => x.Lage.Mitte.Y).ToList();
        Assert.InRange(hoehen.Max() - hoehen.Min(), 0.55, 0.65);                        // über etwa 0.6 m verteilt
        Nah((hoehen.Max() + hoehen.Min()) / 2, Auge.Y - 0.32, 0.03);                   // senkrechte Mitte (Stellschraube)
        // höchstens drei Reihen, locker: keine gemeinsame Grundlinie, Reihen gegeneinander versetzt, Tiefe gestaffelt
        var reihen = p.GroupBy(x => Math.Round((x.Lage.Mitte.Y - (Auge.Y - 0.62)) / 0.3)).ToList();
        Assert.True(reihen.Count <= 3);
        Assert.True(hoehen.Select(h => Math.Round(h, 3)).Distinct().Count() >= 4);
        Assert.True(p.Select(x => Math.Round(x.Lage.Mitte.Z, 3)).Distinct().Count() >= 3);
        Assert.True(reihen.Select(r => Math.Round(r.Min(x => x.Lage.Mitte.X), 2)).Distinct().Count() >= 2);   // die Reihen beginnen nicht alle an derselben Stelle
        // Mindestabstand: Mitten mindestens 0.18 m auseinander, kein Tooltip (über dem Symbol) über einem Symbol oder Tooltip
        var namen = new Dictionary<string, string> { ["status"] = "KICAD", ["etagen"] = "Etagen", ["messen"] = "Messen", ["marker"] = "Marker", ["ort"] = "Ort", ["ebenen"] = "Ebenen" };
        for (int i = 0; i < p.Count; i++)
            for (int j = i + 1; j < p.Count; j++)
                Assert.True(p[i].Lage.Mitte.Abstand(p[j].Lage.Mitte) >= 0.18, $"{p[i].Id}/{p[j].Id}");
        foreach (var a in p)
        {
            var ta = AppSymbole.TooltipLage(a.Lage, namen[a.Id], M);
            Assert.True((ta.Mitte - a.Lage.Mitte).Skalar(a.Lage.Oben) > a.Lage.H / 2);   // darüber (Befund Bilder 02: darunter verdeckte ihn die Hand)
            foreach (var b in p.Where(x => x != a))
            {
                Assert.False(Glas.UeberdecktImBlick(ta, b.Lage, Auge), $"Tooltip {a.Id} über Symbol {b.Id}");
                Assert.False(Glas.UeberdecktImBlick(ta, AppSymbole.TooltipLage(b.Lage, namen[b.Id], M), Auge), $"Tooltip {a.Id} über Tooltip {b.Id}");
            }
        }
        // frontal heisst Gieren beim Holen: gedreht schaut das Feld mit
        var gedreht = AppSymbole.Plaetze(Ids, Auge, 0, e, M);   // Blick nach +x
        Assert.All(gedreht, s => Nah(s.Lage.Mitte.X, 0.6, 0.031));
        // Stellschrauben: Reihen, Höhe, Abstand
        var zwei = AppSymbole.Plaetze(Ids, Auge, Blick, e with { Reihen = 2 }, M);
        Assert.True(zwei.Select(x => Math.Round(x.Lage.Mitte.Y, 1)).Distinct().Count() <= 2 + 1);
        var weit = AppSymbole.Plaetze(Ids, Auge, Blick, e with { AbstandM = 0.8 }, M);
        Assert.All(weit, s => Nah(-s.Lage.Mitte.Z, 0.8, 0.031));
        var flach = AppSymbole.Plaetze(Ids, Auge, Blick, e with { FeldHoeheM = 0.3 }, M);
        Assert.InRange(flach.Max(x => x.Lage.Mitte.Y) - flach.Min(x => x.Lage.Mitte.Y), 0.25, 0.35);
        // Variante «bogen»: eine Reihe
        var bogen = AppSymbole.Plaetze(Ids, Auge, Blick, e with { Verteilung = Varianten.SymboleBogen }, M);
        Assert.Single(bogen.Select(x => Math.Round(x.Lage.Mitte.Y, 6)).Distinct());
        // eine unbekannte App bekommt einen Reserveplatz, frei von den anderen
        var mit = AppSymbole.Plaetze([.. Ids, "neu"], Auge, Blick, e, M);
        Assert.All(mit.Take(Ids.Length), s => Assert.True(s.Lage.Mitte.Abstand(mit[^1].Lage.Mitte) >= 0.15));
    }

    [Fact]
    public void Am_Sitzungstisch_bleibt_das_Feld_über_der_Tischplatte()
    {
        // sitzend: Augen 1.2 m; die unterste Reihe läge bei 0.58 m – unter der Tischplatte (0.75 m). Mit der Untergrenze rückt sie darüber, das Feld wird niedriger.
        var sitz = V(0, 1.2, 0);
        var e = new SymbolEinstellungen();
        var ohne = AppSymbole.Plaetze(Ids, sitz, Blick, e, M);
        Assert.True(ohne.Min(x => x.Lage.Mitte.Y) < 0.75);
        var mit = AppSymbole.Plaetze(Ids, sitz, Blick, e, M, untergrenze: 0.83);
        Assert.True(mit.Min(x => x.Lage.Mitte.Y) >= 0.83 - 0.031);
        Assert.True(mit.Max(x => x.Lage.Mitte.Y) <= sitz.Y - 0.019);
        Assert.True(mit.Max(x => x.Lage.Mitte.Y) - mit.Min(x => x.Lage.Mitte.Y) >= AppSymbole.FeldMinM - 0.06);
        for (int i = 0; i < mit.Count; i++)
            for (int j = i + 1; j < mit.Count; j++)
                Assert.True(mit[i].Lage.Mitte.Abstand(mit[j].Lage.Mitte) >= 0.12, $"{mit[i].Id}/{mit[j].Id}");
        // stehend am Tisch genügt der Platz: keine Änderung
        var stehend = AppSymbole.Plaetze(Ids, Auge, Blick, e, M, untergrenze: 0.83);
        Assert.All(stehend, s => Nah(s.Lage.Mitte.Abstand(AppSymbole.Plaetze(Ids, Auge, Blick, e, M).Single(x => x.Id == s.Id).Lage.Mitte), 0, 1e-12));
    }

    [Fact]
    public void Am_Sitzungstisch_liegt_die_unterste_Reihe_einige_Zentimeter_über_der_Tischplatte_Stellschraube()
    {
        // Befund Prüfbilder 03 (Hauptlauf 24.10.2026): «Die unterste Symbolreihe sitzt mindestens einige Zentimeter über der Tischplatte» – auch mit der leichten
        // Verschiebung je Platz (vorher bis 3 cm darunter)
        var sitz = V(0, 1.2, 0);
        const double tisch = 0.75;
        var e = new SymbolEinstellungen();
        Assert.Equal(0.06, e.TischAbstandM);
        var r = AppSymbole.Durchmesser(M) / 2;
        Nah(AppSymbole.Untergrenze(tisch, e, M), tisch + 0.06 + r, 1e-12);
        foreach (var abstand in new[] { 0.0, 0.06, 0.12 })
        {
            var es = e with { TischAbstandM = abstand };
            var p = AppSymbole.Plaetze(Ids, sitz, Blick, es, M, AppSymbole.Untergrenze(tisch, es, M));
            Assert.All(p, s => Assert.True(s.Lage.Mitte.Y - r >= tisch + abstand - 1e-9, $"{s.Id}: Unterkante {s.Lage.Mitte.Y - r:0.000} m"));
            Assert.True(p.Max(x => x.Lage.Mitte.Y) <= sitz.Y - 0.019);   // die oberste Reihe bleibt unter Augenhöhe – das Feld wird niedriger
        }
        // als Stellschraube vom PC (Grenzen 0 … 0.3 m)
        var b = XrBedienEinstellungen.Vorgabe;
        Assert.Equal(0.1, b.MitStellschraube("symbole.tisch_abstand_m", Wert("0.1"))!.Symbole.TischAbstandM);
        Assert.Null(b.MitStellschraube("symbole.tisch_abstand_m", Wert("0.5")));
        Assert.Equal(0.06, b.MitStellschraube("symbole.tisch_abstand_m", Wert("0.1"))!.MitStellschraube("symbole.tisch_abstand_m", Wert("null"))!.Symbole.TischAbstandM);
    }

    [Fact]
    public void Bei_offener_App_weicht_das_Feld_seitlich_neben_Fenster_und_Ornamente_aus_in_Griffweite()
    {
        // Christian 24.10.2026: «Das Symbolfeld weicht bei offener App seitlich neben das Fenster aus.» – kein Symbol überlappt Fenster oder Ornamente, alles bleibt in
        // Griffweite; «dahinter» bleibt als Vergleich
        var e = new SymbolEinstellungen();
        Assert.Equal(Varianten.FeldSeitlich, e.FeldBeiApp);
        var b = XrBedienEinstellungen.Vorgabe;
        Assert.Equal(Varianten.FeldSeitlich, b.Variante("feld_bei_app"));
        Assert.Equal(Varianten.FeldDahinter, b.MitStellschraube("labor.variante.feld_bei_app", Wert("\"dahinter\""))!.Symbole.FeldBeiApp);
        Assert.Null(b.MitStellschraube("labor.variante.feld_bei_app", Wert("\"oben\"")));
        Assert.Equal([Varianten.FeldSeitlich, Varianten.FeldDahinter], Varianten.Bekannt["feld_bei_app"]);
        // das Fenster frontal vor dem Feld wie in der App (Rahmen mit Werkzeugleiste, Griff, Schliessen), dazu die Karte darüber
        var p = AppSymbole.Plaetze(Ids, Auge, Blick, e, M);
        var mitten = p.Select(x => x.Lage.Mitte).ToList();
        var fenster = Glas.Fenster(AppSymbole.FensterVorFeld(Auge, Blick, e), Auge, M, 0);
        var rahmen = Glas.Rahmen(fenster, M, 0, 0, 2);
        var karte = fenster with { Mitte = fenster.Mitte + fenster.Oben * (fenster.H / 2 + 0.06), H = 0.09 };
        var flaechen = new List<TabletPose> { rahmen.Fenster, rahmen.Griff, rahmen.Schliessen, rahmen.Ornament!, karte };
        var r = AppSymbole.Durchmesser(M) / 2;
        bool Ueberdeckt(Vec3 m) => flaechen.Any(f => Glas.HinterBedienung(m, Auge, [f], 0) || Glas.UeberdecktImBlick(f, Glas.ImBlick(m, Auge, 2 * r, 2 * r), Auge));
        Assert.Contains(mitten, Ueberdeckt);                                                  // ohne Ausweichen überdeckt das Fenster die Mitte des Felds
        var neben = AppSymbole.Seitlich(mitten, Auge, Blick, flaechen, r, 1);
        Assert.DoesNotContain(neben, Ueberdeckt);                                              // danach keines mehr
        // in Griffweite: der Abstand zum Auge und die Höhe bleiben (gedreht um die senkrechte Achse durch das Auge); nicht weiter als 50° zur Seite
        for (int i = 0; i < mitten.Count; i++)
        {
            Nah(neben[i].Abstand(Auge), mitten[i].Abstand(Auge), 1e-9);
            Nah(neben[i].Y, mitten[i].Y, 1e-12);
            Assert.Equal(Math.Sign(mitten[i].X), Math.Sign(neben[i].X));                       // die linken nach links, die rechten nach rechts
            Assert.True(Math.Abs(Math.Atan2(neben[i].X, -neben[i].Z)) < 50 * Math.PI / 180, $"{p[i].Id}");
        }
        // je Seite um denselben Winkel (Anordnung bleibt), nicht weiter als nötig: das innerste steht am Rand
        double W(Vec3 m) => Math.Atan2(m.X, -m.Z);
        Assert.Single(mitten.Zip(neben).Where(x => x.First.X < 0).Select(x => Math.Round(W(x.Second) - W(x.First), 9)).Distinct());
        Assert.Single(mitten.Zip(neben).Where(x => x.First.X >= 0).Select(x => Math.Round(W(x.Second) - W(x.First), 9)).Distinct());
        var kante = flaechen.SelectMany(Glas.Ecken).Min(W);
        Assert.Contains(neben.Where(m => m.X < 0), m => Math.Abs(W(m) - (kante - Math.Asin((r + AppSymbole.SeitlichRandM) / Math.Sqrt(m.X * m.X + m.Z * m.Z)))) < 1e-9);
        // k = 0: am Platz; halb: halber Winkel; ohne Flächen: am Platz
        Assert.All(AppSymbole.Seitlich(mitten, Auge, Blick, flaechen, r, 0).Zip(mitten), x => Nah(x.First.Abstand(x.Second), 0, 1e-12));
        var halb = AppSymbole.Seitlich(mitten, Auge, Blick, flaechen, r, 0.5);
        for (int i = 0; i < mitten.Count; i++) Nah(W(halb[i]) - W(mitten[i]), (W(neben[i]) - W(mitten[i])) / 2, 1e-9);
        Assert.All(AppSymbole.Seitlich(mitten, Auge, Blick, [], r, 1).Zip(mitten), x => Nah(x.First.Abstand(x.Second), 0, 1e-12));
        // ein verschobenes Fenster (rechts): die Seiten richten sich nach seiner Mitte, auch dann überdeckt keines
        var rechts = Glas.ImBlick(V(0.3, 1.28, -0.45), Auge, 0.4, 0.3);
        Assert.DoesNotContain(AppSymbole.Seitlich(mitten, Auge, Blick, [rechts], r, 1), m => Glas.HinterBedienung(m, Auge, [rechts], r));
        // gedreht (Blick nach +x): dieselbe Regel
        var pg = AppSymbole.Plaetze(Ids, Auge, 0, e, M).Select(x => x.Lage.Mitte).ToList();
        var fg = Glas.Fenster(AppSymbole.FensterVorFeld(Auge, 0, e), Auge, M, 0);
        Assert.DoesNotContain(AppSymbole.Seitlich(pg, Auge, 0, [fg], r, 1), m => Glas.HinterBedienung(m, Auge, [fg], r));
    }

    [Fact]
    public void Nach_der_Wahl_bleiben_die_Symbole_die_App_davor_das_Feld_weicht_zurück()
    {
        var e = new SymbolEinstellungen();
        Assert.Equal(Varianten.SymbolwahlStehen, e.NachWahl);
        Assert.Equal(0, e.WartezeitS);   // kein selbsttätiges Ausblenden
        var f = AppSymbole.FensterVorFeld(Auge, Blick, e);
        Nah(-f.Z, 0.6 - AppSymbole.AppVorM, 1e-9);   // frontal, etwas näher als das Feld
        Nah(f.X, 0, 1e-9);
        Nah(f.Y, Auge.Y - e.HoeheM, 1e-9);
        var p = V(0.1, 1.3, -0.6);
        var z = AppSymbole.Zurueck(p, Auge, 1);
        Nah(z.Abstand(Auge), p.Abstand(Auge) + AppSymbole.ZurueckM, 1e-9);   // in die Tiefe zurück …
        Nah((z - Auge).Einheit().Skalar((p - Auge).Einheit()), 1, 1e-9);    // … im Blick an derselben Stelle
        Assert.Equal(p, AppSymbole.Zurueck(p, Auge, 0));
        Nah(AppSymbole.Gedaempft(1), AppSymbole.GedaempftDeckkraft, 1e-12);
        Nah(AppSymbole.Gedaempft(0), 1, 1e-12);
    }

    [Fact]
    public void Hineinheben_von_unten_und_Wegwischen_nach_links()
    {
        for (int i = 0; i < 6; i++)
        {
            var (u0, d0) = AppSymbole.Hineinheben(0, i, 6);
            Nah(u0, AppSymbole.HubM, 1e-12);
            Nah(d0, 0, 1e-12);
            var (u1, d1) = AppSymbole.Hineinheben(1, i, 6);
            Nah(u1, 0, 1e-12);
            Nah(d1, 1, 1e-12);
            double vorher = AppSymbole.HubM + 1;
            for (double f = 0; f <= 1; f += 0.05)
            {
                var (u, _) = AppSymbole.Hineinheben(f, i, 6);
                Assert.True(u <= vorher + 1e-12);
                vorher = u;
            }
        }
        Assert.True(AppSymbole.Hineinheben(0.4, 0, 6).Unten < AppSymbole.Hineinheben(0.4, 5, 6).Unten);
        // Wegwischen: alle Symbole laufen mit der Hand nach links (mehr als die Hand), danach ganz aus dem Bild
        Assert.Equal(0, AppSymbole.NachLinks(0, 0));
        Nah(AppSymbole.NachLinks(0.1, 0), 0.1 * AppSymbole.WischFaktor, 1e-12);
        Assert.Equal(0, AppSymbole.NachLinks(-0.1, 0));
        Assert.True(AppSymbole.NachLinks(0.1, 1) >= AppSymbole.NachWischM);
        Assert.Equal(1, AppSymbole.SeitlichDeckkraft(0));
        Assert.Equal(0, AppSymbole.SeitlichDeckkraft(AppSymbole.NachLinks(0.2, 1)));
        Assert.True(AppSymbole.SeitlichDeckkraft(0.2) is > 0 and < 1);
    }

    // ================================================================================================ Berührung mit der ganzen Hand

    static List<BeruehrKugel> Kugeln() => [.. AppSymbole.Plaetze(Ids, Auge, Blick, new SymbolEinstellungen(), M).Select(p => new BeruehrKugel(p.Id, p.Lage.Mitte, p.Lage.H / 2))];

    /// <summary>Eine flache rechte Hand, die Handfläche zum Symbol, so weit (m, positiv = davor) vor seiner Mitte.</summary>
    static Dictionary<string, Vec3> FlachVor(BeruehrKugel k, double vor) =>
        Geisterhand.VorlageFlach(k.Mitte + (Auge - k.Mitte).Einheit() * vor, (k.Mitte - Auge).Einheit(), V(0, 1, 0), links: false);

    [Fact]
    public void Ganze_Hand_berührt_Handfläche_Handrücken_oder_Finger_wählt()
    {
        var kugeln = Kugeln();
        var messen = kugeln.Single(k => k.Id == "messen");
        // Handfläche: nähert sich, wählt beim ersten Kontakt einmal
        var z = new HandBeruehrZustand();
        string? gewaehlt = null;
        var bei = -1.0;
        for (int i = 0; i <= 20 && gewaehlt is null; i++)
        {
            var vor = 0.15 - 0.01 * i;
            gewaehlt = HandBeruehrung.Schritt(z, 10 + i / 72.0, [null, FlachVor(messen, vor)], kugeln, false).Gewaehlt;
            if (gewaehlt is not null) bei = vor;
        }
        Assert.Equal("messen", gewaehlt);
        Assert.InRange(bei, messen.Radius - 0.02, messen.Radius + new SymbolEinstellungen().BeruehrRandM + HandBeruehrung.MitteRadiusM + 0.02);
        // Handrücken: die Hand von hinten (Handfläche vom Symbol weg)
        var z2 = new HandBeruehrZustand();
        var ruecken = Geisterhand.VorlageFlach(messen.Mitte + (Auge - messen.Mitte).Einheit() * 0.03, (Auge - messen.Mitte).Einheit(), V(0, 1, 0), links: false);
        HandBeruehrung.Schritt(z2, 0, [null, Geisterhand.VorlageFlach(messen.Mitte + (Auge - messen.Mitte).Einheit() * 0.3, (Auge - messen.Mitte).Einheit(), V(0, 1, 0), false)], kugeln, false);
        Assert.Equal("messen", HandBeruehrung.Schritt(z2, 0.02, [null, ruecken], kugeln, false).Gewaehlt);
        // ein Finger: nur die Zeigefingerspitze im Symbol (die Hand sonst weit weg)
        Assert.True(HandBeruehrung.Eindringen(new Dictionary<string, Vec3> { ["index-finger-phalanx-distal"] = messen.Mitte + V(0, -0.05, 0.02), ["index-finger-tip"] = messen.Mitte + V(0, -0.02, 0.01) },
            messen.Mitte, messen.Radius) > 0);
        // weit weg: nichts, aber die Nähe meldet sich vorher (Leuchten)
        var z3 = new HandBeruehrZustand();
        var fern = HandBeruehrung.Schritt(z3, 0, [null, FlachVor(messen, 0.3)], kugeln, false);
        Assert.Null(fern.Gewaehlt);
        Assert.Null(fern.Beruehrt);
        var nah = HandBeruehrung.Schritt(z3, 0.02, [null, FlachVor(messen, 0.12)], kugeln, false);
        Assert.True(nah.Naehe["messen"] > 0 && nah.Naehe["messen"] < 1);
    }

    [Fact]
    public void Schutz_vor_Fehlwahl_Sperre_bei_Gesten_erst_loslassen_tiefste_Eindringung_gewinnt()
    {
        var kugeln = Kugeln();
        var messen = kugeln.Single(k => k.Id == "messen");
        var e = new SymbolEinstellungen();
        // während einer Geste (Hebegeste, Wegwischen) und BeruehrSperreS danach zählt keine Berührung – und was dabei berührt wurde, erst nach dem Loslassen
        var z = new HandBeruehrZustand();
        Assert.Null(HandBeruehrung.Schritt(z, 0, [null, FlachVor(messen, 0.02)], kugeln, gesteLaeuft: true, e).Gewaehlt);
        Assert.True(HandBeruehrung.Schritt(z, 0.3, [null, FlachVor(messen, 0.02)], kugeln, false, e).Gesperrt);
        Assert.Null(HandBeruehrung.Schritt(z, e.BeruehrSperreS + 0.1, [null, FlachVor(messen, 0.0)], kugeln, false, e).Gewaehlt);   // noch drin: gesperrt
        HandBeruehrung.Schritt(z, e.BeruehrSperreS + 0.2, [null, FlachVor(messen, 0.3)], kugeln, false, e);                          // losgelassen
        Assert.Equal("messen", HandBeruehrung.Schritt(z, e.BeruehrSperreS + 0.3, [null, FlachVor(messen, 0.02)], kugeln, false, e).Gewaehlt);
        // nach einer Wahl: nichts mehr, bis alle Berührungen gelöst sind
        Assert.Null(HandBeruehrung.Schritt(z, e.BeruehrSperreS + 0.32, [null, FlachVor(messen, 0.0)], kugeln, false, e).Gewaehlt);
        // streift die Hand zwei Symbole zugleich, gilt das tiefer berührte
        var marker = kugeln.Single(k => k.Id == "marker");
        var zwischen = messen.Mitte * 0.35 + marker.Mitte * 0.65;   // näher am Marker
        var z2 = new HandBeruehrZustand();
        var h = Geisterhand.VorlageFlach(zwischen + (Auge - zwischen).Einheit() * 0.01, (zwischen - Auge).Einheit(), (marker.Mitte - messen.Mitte).Einheit(), links: false);
        var r = HandBeruehrung.Schritt(z2, 1, [null, h], [new BeruehrKugel("messen", messen.Mitte, 0.08), new BeruehrKugel("marker", marker.Mitte, 0.08)], false, e);
        Assert.Equal("marker", r.Gewaehlt);
        // beide Hände: die tiefere zählt; die linke Hand allein wählt auch
        var z3 = new HandBeruehrZustand();
        Assert.Equal("messen", HandBeruehrung.Schritt(z3, 1, [FlachVor(messen, 0.02), null], kugeln, false, e).Gewaehlt);
        // Stellschraube Rand: mit grösserem Rand berührt die Hand früher
        Assert.True(HandBeruehrung.Eindringen(FlachVor(messen, 0.1), messen.Mitte, messen.Radius + 0.05) > HandBeruehrung.Eindringen(FlachVor(messen, 0.1), messen.Mitte, messen.Radius));
    }

    // ================================================================================================ Zeiger

    [Fact]
    public void Zeiger_mit_gestrecktem_Finger_Punkt_Linie_und_Tooltip_nach_der_Verzögerung()
    {
        var p = AppSymbole.Plaetze(Ids, Auge, Blick, new SymbolEinstellungen(), M);
        var ziele = p.Select(x => x.Lage).ToList();
        var ziel = ziele[2];
        var z = new ZeigerZustand();
        var spitze = ziel.Mitte + ziel.Normale * 0.06 + ziel.Rechts * 0.01;
        var b0 = Zeiger.Schritt(z, 0, spitze, true, ziele, 0.6)!;
        Assert.Equal(2, b0.Index);
        Nah((b0.Punkt - ziel.Mitte).Skalar(ziel.Rechts), 0.01, 1e-9);   // der Punkt liegt unter der Fingerspitze auf dem Symbol
        Assert.Equal(spitze, b0.Von);
        Assert.InRange(b0.Naehe, 0.4, 0.6);
        Assert.Equal(0, b0.Tooltip);
        Assert.Equal(0, Zeiger.Schritt(z, 0.5, spitze, true, ziele, 0.6)!.Tooltip);
        Assert.Equal(1, Zeiger.Schritt(z, 0.6 + Zeiger.EinblendenS, spitze, true, ziele, 0.6)!.Tooltip, 9);
        var anderes = ziele[0].Mitte + ziele[0].Normale * 0.05;
        Assert.Equal(0, Zeiger.Schritt(z, 1, anderes, true, ziele, 0.6)!.Tooltip);
        Assert.Null(Zeiger.Schritt(new ZeigerZustand(), 0, spitze, false, ziele, 0.6));
        Assert.Null(Zeiger.Schritt(new ZeigerZustand(), 0, ziel.Mitte + ziel.Normale * 0.2, true, ziele, 0.6));
        Assert.Null(Zeiger.Schritt(new ZeigerZustand(), 0, ziel.Mitte + ziel.Normale * 0.05 + ziel.Rechts * 0.08, true, ziele, 0.6));
        var u = Auge;
        var s = Zeiger.Schritt(new ZeigerZustand(), 0, null, false, ziele, 0.6, (u, ziel.Mitte - u))!;
        Assert.Equal(2, s.Index);
        Assert.True(s.Punkt.Abstand(ziel.Mitte) < 0.01);
        var hand = Geisterhand.Vorlage(V(0, 1.2, -0.4), ziel);
        Assert.True(Zeiger.ZeigefingerGestreckt(hand));
        hand["index-finger-tip"] = hand["index-finger-phalanx-distal"] + (hand["index-finger-phalanx-intermediate"] - hand["index-finger-phalanx-proximal"]).Einheit() * -0.02 + V(0, -0.02, 0);
        Assert.False(Zeiger.ZeigefingerGestreckt(hand));
    }

    // ================================================================================================ Hinweise und Überdecken im Blick

    [Fact]
    public void Überdecken_im_Blick_zählt_der_Winkel_nicht_die_Tiefe()
    {
        var nah = Glas.ImBlick(V(0, 1.4, -0.4), Auge, 0.2, 0.05);
        var fern = Glas.ImBlick(V(0, 1.0, -1.2), Auge, 0.4, 0.1);   // dreimal so weit auf derselben Sichtlinie, im Blick an derselben Stelle
        Assert.False(Glas.Ueberdeckt(nah, fern));                    // in gleicher Tiefe gemessen: nein
        Assert.True(Glas.UeberdecktImBlick(nah, fern, Auge));        // im Blick: ja
        Assert.False(Glas.UeberdecktImBlick(nah, Glas.ImBlick(V(0.6, 1.4, -0.4), Auge, 0.2, 0.05), Auge));
        Assert.False(Glas.UeberdecktImBlick(nah, Glas.ImBlick(V(0, 1.4, 0.5), Auge, 0.2, 0.05), Auge));   // hinter dem Auge
        // Hülle im Blick (Modell, Hand): umfasst alle Punkte
        var punkte = new[] { V(-0.2, 0.75, -0.7), V(0.2, 0.75, -0.7), V(-0.2, 0.95, -1.1), V(0.2, 0.95, -1.1) };
        var h = Glas.HuelleImBlick(punkte, Auge)!;
        Assert.All(punkte, p => Assert.True(Glas.UeberdecktImBlick(Glas.ImBlick(p, Auge, 0.001, 0.001), h, Auge)));
        Assert.Null(Glas.HuelleImBlick(Array.Empty<Vec3>(), Auge));
    }

    [Fact]
    public void Hinweise_frei_schwebend_kurz_weich_im_Blick_nie_über_Apps_Händen_Modell()
    {
        // höchstens zwei Zeilen, an einer Wortgrenze gekürzt; schmal bei einer Geste
        var lang = string.Join(' ', Enumerable.Repeat("Messung", 30));
        var k = SchwebeHinweise.Kurz(lang);
        Assert.True(k.Length <= SchwebeHinweise.ZeichenMax + 2 && k.EndsWith(" …", StringComparison.Ordinal));
        Assert.Equal("a b", SchwebeHinweise.Kurz("a\nb"));
        Assert.True(SchwebeHinweise.Kurz(lang, SchwebeHinweise.ZeichenJeZeileGeste).Length <= 2 * SchwebeHinweise.ZeichenJeZeileGeste + 2);
        Assert.Equal(2, SchwebeHinweise.Groesse("Nach links wischen: schliessen (rechte Hand offen)", 0.01, SchwebeHinweise.ZeichenJeZeileGeste).Zeilen);
        // weich ein, steht, weich aus, dann weg
        var l = new HinweisListe();
        var h = l.Neu("Problem-Marker gesetzt («Prüfen»).", 10, 4, HinweisArt.Objekt, V(1, 1, 1));
        Assert.Equal(0, SchwebeHinweise.Deckkraft(h, 10), 9);
        Assert.Equal(1, SchwebeHinweise.Deckkraft(h, 11), 9);
        Assert.True(SchwebeHinweise.Deckkraft(h, 14.2) is > 0 and < 1);
        Assert.Single(l.Sichtbar(12));
        Assert.Empty(l.Sichtbar(14.5));
        l.Neu("Etage 2.OG", 0, 4);
        l.Neu("Verbunden", 0.5, 4);
        Assert.Single(l.Alle, x => x.Art == HinweisArt.Allgemein);
        l.Neu("A", 1, 4, HinweisArt.Objekt, V(0, 0, 0)); l.Neu("B", 1, 4, HinweisArt.Hand, V(0, 0, 0)); l.Neu("C", 1, 4, HinweisArt.Geste, V(0, 0, 0), 27);
        Assert.Equal(SchwebeHinweise.HoechstensGleichzeitig, l.Alle.Count);
        Assert.Equal(27, l.Alle[^1].ZeichenJeZeile);
        l.Leeren();
        Assert.Empty(l.Alle);
        // Platz: unter der Blickmitte in Griffweite; bei Hand bzw. Objekt darüber; bei einer Geste dort, wo die App ihn hinstellt (neben bzw. unter den Händen)
        var p = SchwebeHinweise.Allgemein(Auge, Blick);
        Nah(p.Y, Auge.Y - SchwebeHinweise.UnterM, 1e-12);
        Nah(-p.Z, SchwebeHinweise.VorM, 1e-12);
        Nah(SchwebeHinweise.Darueber(V(0, 1, 0), HinweisArt.Hand).Y, 1 + SchwebeHinweise.UeberHandM, 1e-12);
        Assert.Equal(V(0, 1, 0), SchwebeHinweise.Darueber(V(0, 1, 0), HinweisArt.Geste));
        // nie dicht vor dem Auge (Befund Bilder 02: «Messung 3» riesig auf der Treppe), im Blick gleich gross
        Nah(SchwebeHinweise.Abruecken(V(0, 1.5, -0.1), Auge).Abstand(Auge), SchwebeHinweise.MindestAbstandM, 1e-9);
        Assert.Equal(p, SchwebeHinweise.Abruecken(p, Auge));
        Nah(SchwebeHinweise.Schrifthoehe(0.01, 1.1), 0.02, 1e-12);
        Nah(SchwebeHinweise.Schrifthoehe(0.01, SchwebeHinweise.VorM), 0.01, 1e-12);
        // nie über einem Fenster: rückt weg, auch im Blick
        var fenster = Glas.ImBlick(V(0, 1.36, -0.55), Auge, 0.32, 0.31);
        var (b, hh, _) = SchwebeHinweise.Groesse("Problem-Marker gesetzt", 0.012);
        var frei = SchwebeHinweise.Ausweichen(p, Auge, b, hh, [fenster]);
        Assert.NotEqual(p, frei);
        Assert.True(frei.Y <= Auge.Y - SchwebeHinweise.UnterAugeMinM);
        Assert.False(Glas.UeberdecktImBlick(Glas.ImBlick(frei, Auge, b, hh), fenster, Auge));
        Assert.Equal(p, SchwebeHinweise.Ausweichen(p, Auge, b, hh, []));
        // Befund Bilder 02: ein Hinweis in anderer Tiefe deckte einen anderen – im Blick weicht er aus
        var erster = Glas.ImBlick(V(0, 1.3, -0.5), Auge, b, hh);
        var zweitOrt = V(0, 1.06, -0.9);   // doppelt so weit, im Blick auf dem ersten
        Assert.True(Glas.UeberdecktImBlick(Glas.ImBlick(zweitOrt, Auge, b * 1.8, hh * 1.8), erster, Auge));
        var zweit = SchwebeHinweise.Ausweichen(zweitOrt, Auge, b * 1.8, hh * 1.8, [erster]);
        Assert.False(Glas.UeberdecktImBlick(Glas.ImBlick(zweit, Auge, b * 1.8, hh * 1.8), erster, Auge));
        // am Tisch nie über dem Modell (Hülle im Blick): ein allgemeiner Hinweis vor dem Modell weicht aus
        var modell = Glas.HuelleImBlick([V(-0.3, 0.75, -0.5), V(0.3, 0.75, -0.5), V(-0.3, 1.0, -1.2), V(0.3, 1.0, -1.2)], V(0, 1.2, 0), 0.02)!;
        var vorModell = SchwebeHinweise.Allgemein(V(0, 1.2, 0), Blick);
        Assert.True(Glas.UeberdecktImBlick(Glas.ImBlick(vorModell, V(0, 1.2, 0), b, hh), modell, V(0, 1.2, 0)));
        var weg = SchwebeHinweise.Ausweichen(vorModell, V(0, 1.2, 0), b, hh, [modell]);
        Assert.False(Glas.UeberdecktImBlick(Glas.ImBlick(weg, V(0, 1.2, 0), b, hh), modell, V(0, 1.2, 0)));
        // nachgeführt: bleibt bei kleiner Kopfdrehung, gleitet bei grosser (nie starr am Kopf)
        Assert.Equal(p, SchwebeHinweise.Nachfuehren(p, Auge, Blick + 10 * Math.PI / 180, 1 / 72.0));
        var neu = SchwebeHinweise.Nachfuehren(p, Auge, Blick + 60 * Math.PI / 180, 1 / 72.0);
        Assert.True(neu.Abstand(p) > 0 && neu.Abstand(SchwebeHinweise.Allgemein(Auge, Blick + 60 * Math.PI / 180)) > 0.01);
    }

    // ================================================================================================ Stellschrauben

    [Fact]
    public void Stellschrauben_der_Hebegeste_der_Symbole_des_Wegwischens_und_der_Hinweise()
    {
        static JsonElement W(string j) => JsonDocument.Parse(j).RootElement.Clone();
        var e = XrBedienEinstellungen.Vorgabe;
        Assert.Equal(HebeEinstellungen.Vorgabe, e.Hebe);
        Assert.Equal(0.15, e.Hebe.WegM);
        Assert.Equal(0.6, e.Symbole.AbstandM);
        Assert.Equal(4, e.Apps.HinweisDauerS);
        Assert.Equal(Varianten.SymboleVerteilt, e.Variante("symbole"));
        Assert.Equal(Varianten.SymbolwahlStehen, e.Variante("symbolwahl"));
        Assert.Equal(0.2, e.MitStellschraube("hebe.weg_m", W("0.2"))!.Hebe.WegM);
        Assert.Null(e.MitStellschraube("hebe.weg_m", W("0.9")));
        Assert.Equal(1.2, e.MitStellschraube("hebe.zeit_s", W("1.2"))!.Hebe.ZeitS);
        Assert.Equal(30, e.MitStellschraube("hebe.handflaeche_grad", W("30"))!.Hebe.HandflaecheGrad);
        Assert.Equal(0.5, e.MitStellschraube("hebe.gleichzeitig_s", W("0.5"))!.Hebe.GleichzeitigS);
        Assert.Equal(0.7, e.MitStellschraube("symbole.abstand_m", W("0.7"))!.Symbole.AbstandM);
        Assert.Null(e.MitStellschraube("symbole.abstand_m", W("1.5")));
        Assert.Equal(0.25, e.MitStellschraube("symbole.hoehe_m", W("0.25"))!.Symbole.HoeheM);
        Assert.Equal(0.5, e.MitStellschraube("symbole.feldhoehe_m", W("0.5"))!.Symbole.FeldHoeheM);
        Assert.Equal(2, e.MitStellschraube("symbole.reihen", W("2"))!.Symbole.Reihen);
        Assert.Null(e.MitStellschraube("symbole.reihen", W("4")));
        Assert.Null(e.MitStellschraube("symbole.reihen", W("2.5")));
        Assert.Equal(1.3, e.MitStellschraube("symbole.streuung", W("1.3"))!.Symbole.Streuung);
        Assert.Equal(20, e.MitStellschraube("symbole.wartezeit_s", W("20"))!.Symbole.WartezeitS);
        Assert.Equal(0, e.MitStellschraube("symbole.wartezeit_s", W("0"))!.Symbole.WartezeitS);   // 0 = nie (Vorgabe)
        Assert.Equal(0.02, e.MitStellschraube("symbole.beruehr_rand_m", W("0.02"))!.Symbole.BeruehrRandM);
        Assert.Equal(1, e.MitStellschraube("symbole.beruehr_sperre_s", W("1"))!.Symbole.BeruehrSperreS);
        Assert.Equal(1, e.MitStellschraube("zeiger.tooltip_s", W("1"))!.Symbole.TooltipS);
        Assert.Equal(6, e.MitStellschraube("hinweis.dauer_s", W("6"))!.Apps.HinweisDauerS);
        var bogen = e.MitStellschraube("labor.variante.symbole", W("\"bogen\""))!;
        Assert.Equal(Varianten.SymboleBogen, bogen.Symbole.Verteilung);
        Assert.Null(e.MitStellschraube("labor.variante.symbole", W("\"kreis\"")));
        Assert.Equal(e, bogen.MitStellschraube("labor.variante.symbole", W("null")));
        Assert.Equal(Varianten.SymbolwahlAusblenden, e.MitStellschraube("labor.variante.symbolwahl", W("\"ausblenden\""))!.Symbole.NachWahl);
        Assert.Null(e.MitStellschraube("labor.variante.symbolwahl", W("\"weg\"")));
        // Wegwischen
        Assert.Equal(WegwischEinstellungen.Vorgabe, e.Wegwischen);
        Assert.Equal(0.25, e.MitStellschraube("wegwischen.weg_m", W("0.25"))!.Wegwischen.WegM);
        Assert.Null(e.MitStellschraube("wegwischen.weg_m", W("0.9")));
        Assert.Equal(0.8, e.MitStellschraube("wegwischen.zeit_s", W("0.8"))!.Wegwischen.ZeitS);
        Assert.Equal(35, e.MitStellschraube("wegwischen.handflaeche_grad", W("35"))!.Wegwischen.HandflaecheGrad);
        Assert.Equal(20, e.MitStellschraube("wegwischen.richtung_grad", W("20"))!.Wegwischen.RichtungGrad);
        Assert.Equal(e, e.MitStellschraube("wegwischen.weg_m", W("0.25"))!.MitStellschraube("wegwischen.weg_m", W("null")));
        Assert.All(new[] { "hebe.weg_m", "symbole.streuung", "symbole.feldhoehe_m", "symbole.reihen", "symbole.beruehr_rand_m", "symbole.beruehr_sperre_s", "zeiger.tooltip_s",
            "hinweis.dauer_s", "labor.variante.symbole", "labor.variante.symbolwahl", "wegwischen.weg_m", "wegwischen.zeit_s", "wegwischen.handflaeche_grad",
            "wegwischen.richtung_grad" }, k => Assert.Contains(k, XrBedienEinstellungen.Schluessel));
        Assert.DoesNotContain(XrBedienEinstellungen.Schluessel, k => k.StartsWith("schieben.", StringComparison.Ordinal));
    }
}
