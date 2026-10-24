// Tests Quest App V1a, Vorgabe Christian 24.10.2026 (nach den Bildern 01): Hebegeste (beide Handflächen nach oben, deutlich nach oben streichen) holt frei schwebende
// App-Symbole, verteilt in Griffweite, Anwahl durch Berührung, Zeiger mit Tooltip; Wegschieben (beide Arme aus der Mitte nach aussen) blendet aus; Hinweise frei schwebend. Auch Fehlauslösungen: eine Hand,
// Pinch, Handflächen nicht oben, zu langsam, nicht gleichzeitig, seitlich; und die Geste löst weder Pinch + Wisch noch den Sprung aus.

using System.Text.Json;
using Hlks.Xr.Bedienung;
using static Hlks.Xr.Tests.T;

namespace Hlks.Xr.Tests;

public class V1aSymboleTests
{
    static readonly GlasMasse M = GlasMasse.Fuer(new AppEinstellungen());
    static readonly Vec3 Auge = V(0, 1.6, 0);
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

    [Fact]
    public void Hebegeste_beide_Handflächen_oben_deutlich_nach_oben_holt_die_Symbole_einmal()
    {
        var z = new HebeZustand();
        var s = Geste(z, 10, 0.5, 0.22);
        Assert.Equal(1, s.Count(x => x.Ereignis == HebeArt.Heben));
        Assert.DoesNotContain(s, x => x.Ereignis == HebeArt.Wegschieben);   // die Hebegeste schiebt nichts weg
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
    public void Die_Hebegeste_löst_weder_Pinch_Wisch_noch_den_Sprung_aus()
    {
        // dieselben Hände (offen, Handfläche oben, ohne Pinch) durch die Regeln des Begehens und des Wischs
        var wisch = new PinchWischZustand();
        var links = new HandBegehenZustand();
        var rechts = new HandBegehenZustand();
        var gesten = new List<BegehenEreignis>();
        for (double t = 0; t <= 1.2; t += 1 / 72.0)
        {
            var y = 1.0 + 0.25 * GlasStil.Weich(Math.Clamp(t / 0.4, 0, 1));
            var gl = Geisterhand.VorlageOffen(V(-0.2, y, -0.35), V(0, 0, -1), true);
            var gr = Geisterhand.VorlageOffen(V(0.2, y, -0.35), V(0, 0, -1), false);
            Assert.True(HandBeobachtung.Aus(gl, "left")!.Value.Normale.Y > 0.9);   // die Vorlage zeigt die Handfläche nach oben
            Assert.True(HandBeobachtung.Aus(gr, "right") is { Offen: true, Pinch: false });
            Assert.Null(XrGesten.PinchWischSchritt(wisch, t, XVec(gr["index-finger-tip"]), XrTabletLogik.IstPinch(gr), Blick, null, 1));
            gesten.AddRange(HandBegehen.Schritt(links, t, "left", gl, Auge, Blick, 0));
            gesten.AddRange(HandBegehen.Schritt(rechts, t, "right", gr, Auge, Blick, 0));
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

    static Vec3 XVec(Vec3 v) => v;

    /// <summary>
    /// Wegschieben (Blick −z: aussen = links −x bzw. rechts +x): beide Hände beginnen ±start/2 um die Mitte, Handflächen nach aussen (nAussen = Anteil waagrecht nach
    /// aussen, der Rest nach vorn), beide wischen um weg in dauer s nach aussen (rechts versatz s später), hoch = Weg nach oben dabei.
    /// </summary>
    static List<HebeStand> Schieben(HebeZustand z, double dauer, double weg, double start = 0.12, double versatz = 0, double nAussen = 1, bool offen = true,
        bool pinch = false, double hoch = 0, bool nurRechts = false, double t1 = 1.2)
    {
        var aus = new List<HebeStand>();
        Vec3 N(int s) => V(s * nAussen, 0, -Math.Sqrt(Math.Max(0, 1 - nAussen * nAussen)));
        for (double t = 0; t <= t1 + 1e-9; t += 1 / 72.0)
        {
            var fl = nurRechts ? 0 : GlasStil.Weich(Math.Clamp(t / dauer, 0, 1));
            var fr = GlasStil.Weich(Math.Clamp((t - versatz) / dauer, 0, 1));
            var l = new HandBeobachtung(V(-start / 2 - weg * fl, 1.2 + hoch * fl, -0.35), N(-1), offen, pinch);
            var r = new HandBeobachtung(V(start / 2 + weg * fr, 1.2 + hoch * fr, -0.35), N(1), offen, pinch);
            aus.Add(HebeGeste.Schritt(z, t, l, r, Blick));
        }
        return aus;
    }

    [Fact]
    public void Wegschieben_aus_der_Mitte_beide_Arme_nach_aussen_Handflächen_aussen_einmal_mit_mitlaufendem_Weg()
    {
        var s = Schieben(new HebeZustand(), 0.4, 0.25);
        Assert.Equal(1, s.Count(x => x.Ereignis == HebeArt.Wegschieben));
        Assert.DoesNotContain(s, x => x.Ereignis == HebeArt.Heben);
        // vor dem Ereignis: der Weg jeder Hand nach aussen wächst (die Symbole laufen mit), die App wertet keinen Wisch über Messlinien aus
        var vor = s.TakeWhile(x => x.Ereignis is null).ToList();
        Assert.True(vor.Count > 5);
        Assert.All(vor, x => Assert.True(x.SchiebenLaeuft));
        Assert.True(vor[^1].SchiebenLinks > 0.1 && vor[^1].SchiebenRechts > 0.1);
        for (int i = 1; i < vor.Count; i++) Assert.True(vor[i].SchiebenLinks >= vor[i - 1].SchiebenLinks - 1e-9);
        Assert.All(s, x => Assert.Equal(0, x.Fortschritt, 9));   // kein Hineinheben
    }

    [Fact]
    public void Wegschieben_Fehlauslösungen_eine_Hand_aussen_begonnen_Handflächen_vorn_Pinch_Faust_nacheinander_zu_kurz_schräg()
    {
        static int W(List<HebeStand> s) => s.Count(x => x.Ereignis == HebeArt.Wegschieben);
        Assert.Equal(0, W(Schieben(new HebeZustand(), 0.4, 0.25, nurRechts: true)));             // nur ein Arm (einhändiger Wisch)
        Assert.Equal(0, W(Schieben(new HebeZustand(), 0.4, 0.25, start: 0.7)));                  // nicht aus der Mitte (0.3 m)
        Assert.Equal(0, W(Schieben(new HebeZustand(), 0.4, 0.25, nAussen: 0.3)));                // Handflächen nach vorn (73° neben aussen)
        Assert.Equal(0, W(Schieben(new HebeZustand(), 0.4, 0.25, pinch: true)));                 // Pinch (Pinch + Wisch)
        Assert.Equal(0, W(Schieben(new HebeZustand(), 0.4, 0.25, offen: false)));                // Faust
        Assert.Equal(0, W(Schieben(new HebeZustand(), 0.3, 0.25, versatz: 0.6)));                // nacheinander (0.35 s)
        Assert.Equal(0, W(Schieben(new HebeZustand(), 0.4, 0.08)));                              // zu kurz (0.15 m)
        Assert.Equal(0, W(Schieben(new HebeZustand(), 0.4, 0.15, hoch: 0.3)));                   // eher nach oben als zur Seite
        Assert.Equal(0, W(Schieben(new HebeZustand(), 2.5, 0.25, t1: 3)));                       // zu langsam (0.8 s)
        Assert.Equal(1, W(Schieben(new HebeZustand(), 0.4, 0.25, versatz: 0.15)));               // leicht versetzt zählt
        // die Hebegeste (Handflächen oben) schiebt nichts weg, das Wegschieben hebt nichts
        Assert.DoesNotContain(Geste(new HebeZustand(), 0, 0.5, 0.22), x => x.Ereignis == HebeArt.Wegschieben);
        // Stellschrauben: ein grösserer Startabstand erlaubt den Beginn weiter aussen
        var weit = new HebeZustand();
        var n = 0;
        for (double t = 0; t <= 1.2; t += 1 / 72.0)
        {
            var f = GlasStil.Weich(Math.Clamp(t / 0.4, 0, 1));
            var l = new HandBeobachtung(V(-0.25 - 0.25 * f, 1.2, -0.35), V(-1, 0, 0), true, false);
            var r = new HandBeobachtung(V(0.25 + 0.25 * f, 1.2, -0.35), V(1, 0, 0), true, false);
            if (HebeGeste.Schritt(weit, t, l, r, Blick, null, SchiebeEinstellungen.Vorgabe with { StartabstandM = 0.55 }).Ereignis == HebeArt.Wegschieben) n++;
        }
        Assert.Equal(1, n);
    }

    [Fact]
    public void Wegschieben_löst_weder_Wisch_links_rechts_noch_Pinch_Wisch_noch_den_Sprung_aus()
    {
        var wischL = new PinchWischZustand();
        var wischR = new PinchWischZustand();
        var links = new HandBegehenZustand();
        var rechts = new HandBegehenZustand();
        var gesten = new List<BegehenEreignis>();
        var z = new HebeZustand();
        var weg = 0;
        for (double t = 0; t <= 1.2; t += 1 / 72.0)
        {
            var f = GlasStil.Weich(Math.Clamp(t / 0.35, 0, 1));
            var gl = Geisterhand.VorlageSeitlich(V(-0.06 - 0.3 * f, 1.2, -0.35), V(0, 0, -1));
            var gr = Geisterhand.VorlageSeitlich(V(0.06 + 0.3 * f, 1.2, -0.35), V(0, 0, -1));
            var bl = HandBeobachtung.Aus(gl, "left")!.Value;
            var br = HandBeobachtung.Aus(gr, "right")!.Value;
            Assert.True(bl.Normale.X < -0.9 && br.Normale.X > 0.9);   // die Vorlage zeigt die Handflächen nach aussen
            Assert.True(bl is { Offen: true, Pinch: false });
            Assert.Null(XrGesten.PinchWischSchritt(wischL, t, gl["index-finger-tip"], XrTabletLogik.IstPinch(gl), Blick, null, 1));
            Assert.Null(XrGesten.PinchWischSchritt(wischR, t, gr["index-finger-tip"], XrTabletLogik.IstPinch(gr), Blick, null, 1));
            gesten.AddRange(HandBegehen.Schritt(links, t, "left", gl, Auge, Blick, 0));
            gesten.AddRange(HandBegehen.Schritt(rechts, t, "right", gr, Auge, Blick, 0));
            if (HebeGeste.Schritt(z, t, bl, br, Blick).Ereignis == HebeArt.Wegschieben) weg++;
        }
        Assert.DoesNotContain(gesten, e => e.Art is BegehenArt.Teleport or BegehenArt.Wisch or BegehenArt.Drehen or BegehenArt.PinchAn);
        Assert.Equal(1, weg);
        // die Geisterhand zeigt das Wegschieben: von der Mitte nach aussen (Rechts des Ziels = aussen)
        var ziel = XrTabletLogik.TabletLageAus(V(0.06, 1.2, -0.35), V(0, 0, 1));
        var hand = Geisterhand.VorlageSeitlich(V(0.06, 1.2, -0.35), V(0, 0, -1));
        var b0 = Geisterhand.Bild(GeisterGeste.Wegschieben, 0, hand, ziel)!;
        var b1 = Geisterhand.Bild(GeisterGeste.Wegschieben, Geisterhand.DauerS / 2 * 0.6, hand, ziel)!;
        Assert.True((b1.Gelenke["wrist"] - b0.Gelenke["wrist"]).Skalar(ziel.Rechts) > 0.15);
    }

    static readonly string[] Ids = ["status", "etagen", "messen", "marker", "ort", "ebenen"];

    [Fact]
    public void Symbole_verteilt_in_Griffweite_ohne_Grundlinie_mit_Abstand_jedes_Mal_gleich()
    {
        var e = new SymbolEinstellungen();
        var p = AppSymbole.Plaetze(Ids, Auge, Blick, e, M);
        Assert.Equal(Ids, p.Select(x => x.Id));
        // jedes Mal gleich (Muskelgedächtnis), auch in anderer Reihenfolge der Apps
        var q = AppSymbole.Plaetze([.. Ids.Reverse()], Auge, Blick, e, M);
        foreach (var s in p) Nah(q.Single(x => x.Id == s.Id).Lage.Mitte.Abstand(s.Lage.Mitte), 0, 1e-12);
        var d = AppSymbole.Durchmesser(M);
        Nah(p[0].Lage.B, d, 1e-12);
        foreach (var s in p)
        {
            var w = Math.Sqrt(Math.Pow(s.Lage.Mitte.X - Auge.X, 2) + Math.Pow(s.Lage.Mitte.Z - Auge.Z, 2));
            Assert.InRange(w, 0.3, 0.6);                                             // Griffweite
            Assert.InRange(Auge.Y - s.Lage.Mitte.Y, 0.12, 0.5);                      // Brust bis Schulter
            Nah(s.Lage.Normale.Skalar((Auge - s.Lage.Mitte).Einheit()), 1, 1e-9);    // zum Auge gedreht
        }
        // keine gemeinsame Grundlinie: mindestens vier verschiedene Höhen, gestaffelte Tiefe
        Assert.True(p.Select(x => Math.Round(x.Lage.Mitte.Y, 3)).Distinct().Count() >= 4);
        Assert.True(p.Select(x => Math.Round(x.Lage.Mitte.Z, 3)).Distinct().Count() >= 3);
        // Mitten mindestens 0.13 m auseinander (Finger eindeutig; weit über 60 pt) – und kein Tooltip überdeckt ein Symbol oder einen anderen Tooltip
        var namen = new Dictionary<string, string> { ["status"] = "KICAD", ["etagen"] = "Etagen", ["messen"] = "Messen", ["marker"] = "Marker", ["ort"] = "Ort", ["ebenen"] = "Ebenen" };
        for (int i = 0; i < p.Count; i++)
            for (int j = i + 1; j < p.Count; j++)
            {
                Assert.True(p[i].Lage.Mitte.Abstand(p[j].Lage.Mitte) >= 0.13, $"{p[i].Id}/{p[j].Id}");
                Assert.True(p[i].Lage.Mitte.Abstand(p[j].Lage.Mitte) >= M.M(60));
            }
        foreach (var a in p)
        {
            var ta = AppSymbole.TooltipLage(a.Lage, namen[a.Id], M);
            Assert.True((ta.Mitte - a.Lage.Mitte).Skalar(a.Lage.Oben) < -a.Lage.H / 2);   // darunter
            foreach (var b in p.Where(x => x != a))
            {
                Assert.False(Glas.Ueberdeckt(ta, b.Lage), $"Tooltip {a.Id} über Symbol {b.Id}");
                Assert.False(Glas.Ueberdeckt(ta, AppSymbole.TooltipLage(b.Lage, namen[b.Id], M)), $"Tooltip {a.Id} über Tooltip {b.Id}");
            }
        }
        // Streuung vergrössert die Abstände; die Variante «bogen» steht auf einer Höhe
        var weit = AppSymbole.Plaetze(Ids, Auge, Blick, e with { Streuung = 1.5 }, M);
        Assert.True(weit[0].Lage.Mitte.Abstand(weit[1].Lage.Mitte) > p[0].Lage.Mitte.Abstand(p[1].Lage.Mitte));
        var bogen = AppSymbole.Plaetze(Ids, Auge, Blick, e with { Verteilung = Varianten.SymboleBogen }, M);
        Assert.Single(bogen.Select(x => Math.Round(x.Lage.Mitte.Y, 6)).Distinct());
        // eine unbekannte App bekommt einen Reserveplatz, frei von den anderen
        var mit = AppSymbole.Plaetze([.. Ids, "neu"], Auge, Blick, e, M);
        Assert.All(mit.Take(Ids.Length), s => Assert.True(s.Lage.Mitte.Abstand(mit[^1].Lage.Mitte) >= 0.1));
    }

    [Fact]
    public void Hineinheben_von_unten_jedes_Symbol_an_seinen_Platz()
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
        // das erste steigt vor dem letzten
        Assert.True(AppSymbole.Hineinheben(0.4, 0, 6).Unten < AppSymbole.Hineinheben(0.4, 5, 6).Unten);
        // Wegschieben: links der Mitte mit der linken Hand nach links, rechts mit der rechten nach rechts, mitlaufend (mehr als die Hand), danach ganz aus dem Bild
        Assert.Equal(0, AppSymbole.Seitlich(-0.1, 0, 0, 0));
        Nah(AppSymbole.Seitlich(-0.1, 0.1, 0.05, 0), -0.1 * AppSymbole.SchiebenFaktor, 1e-12);
        Nah(AppSymbole.Seitlich(0.1, 0.1, 0.05, 0), 0.05 * AppSymbole.SchiebenFaktor, 1e-12);
        Assert.True(AppSymbole.Seitlich(0.1, 0.1, 0.1, 1) >= AppSymbole.WegschiebenM);
        Assert.Equal(1, AppSymbole.SeitlichDeckkraft(0));
        Assert.Equal(0, AppSymbole.SeitlichDeckkraft(AppSymbole.Seitlich(-0.2, 0.1, 0.1, 1)));
        Assert.True(AppSymbole.SeitlichDeckkraft(0.2) is > 0 and < 1);
    }

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
        // ein anderes Ziel beginnt von vorn
        var anderes = ziele[0].Mitte + ziele[0].Normale * 0.05;
        Assert.Equal(0, Zeiger.Schritt(z, 1, anderes, true, ziele, 0.6)!.Tooltip);
        // Finger nicht gestreckt, zu weit weg bzw. daneben: kein Zeiger
        Assert.Null(Zeiger.Schritt(new ZeigerZustand(), 0, spitze, false, ziele, 0.6));
        Assert.Null(Zeiger.Schritt(new ZeigerZustand(), 0, ziel.Mitte + ziel.Normale * 0.2, true, ziele, 0.6));
        Assert.Null(Zeiger.Schritt(new ZeigerZustand(), 0, ziel.Mitte + ziel.Normale * 0.05 + ziel.Rechts * 0.08, true, ziele, 0.6));
        // aus der Ferne mit dem Strahl
        var u = Auge;
        var s = Zeiger.Schritt(new ZeigerZustand(), 0, null, false, ziele, 0.6, (u, ziel.Mitte - u))!;
        Assert.Equal(2, s.Index);
        Assert.True(s.Punkt.Abstand(ziel.Mitte) < 0.01);
        // gestreckt: ja bei der Vorlage (Zeigefinger gestreckt), nein bei eingerolltem Zeigefinger
        var hand = Geisterhand.Vorlage(V(0, 1.2, -0.4), ziel);
        Assert.True(Zeiger.ZeigefingerGestreckt(hand));
        hand["index-finger-tip"] = hand["index-finger-phalanx-distal"] + (hand["index-finger-phalanx-intermediate"] - hand["index-finger-phalanx-proximal"]).Einheit() * -0.02 + V(0, -0.02, 0);
        Assert.False(Zeiger.ZeigefingerGestreckt(hand));
    }

    [Fact]
    public void Hinweise_frei_schwebend_kurz_weich_ein_und_aus_nie_über_den_Apps()
    {
        // höchstens zwei Zeilen, an einer Wortgrenze gekürzt
        var lang = string.Join(' ', Enumerable.Repeat("Messung", 30));
        var k = SchwebeHinweise.Kurz(lang);
        Assert.True(k.Length <= SchwebeHinweise.ZeichenMax + 2 && k.EndsWith(" …", StringComparison.Ordinal));
        Assert.Equal("a b", SchwebeHinweise.Kurz("a\nb"));
        // weich ein, steht, weich aus, dann weg
        var l = new HinweisListe();
        var h = l.Neu("Problem-Marker gesetzt («Prüfen»).", 10, 4, HinweisArt.Objekt, V(1, 1, 1));
        Assert.Equal(0, SchwebeHinweise.Deckkraft(h, 10), 9);
        Assert.Equal(1, SchwebeHinweise.Deckkraft(h, 11), 9);
        Assert.True(SchwebeHinweise.Deckkraft(h, 14.2) is > 0 and < 1);
        Assert.Single(l.Sichtbar(12));
        Assert.Empty(l.Sichtbar(14.5));
        // allgemein steht immer nur einer; derselbe Text ersetzt; höchstens drei
        l.Neu("Etage 2.OG", 0, 4);
        l.Neu("Verbunden", 0.5, 4);
        Assert.Single(l.Alle, x => x.Art == HinweisArt.Allgemein);
        l.Neu("A", 1, 4, HinweisArt.Objekt, V(0, 0, 0)); l.Neu("B", 1, 4, HinweisArt.Hand, V(0, 0, 0)); l.Neu("C", 1, 4, HinweisArt.Objekt, V(0, 0, 0));
        Assert.Equal(SchwebeHinweise.HoechstensGleichzeitig, l.Alle.Count);
        // Platz: unter der Blickmitte in Griffweite; bei Hand bzw. Objekt darüber
        var p = SchwebeHinweise.Allgemein(Auge, Blick);
        Nah(p.Y, Auge.Y - SchwebeHinweise.UnterM, 1e-12);
        Nah(-p.Z, SchwebeHinweise.VorM, 1e-12);
        Nah(SchwebeHinweise.Darueber(V(0, 1, 0), HinweisArt.Hand).Y, 1 + SchwebeHinweise.UeberHandM, 1e-12);
        // nie über einem Fenster: rückt darunter
        var fenster = XrTabletLogik.TabletLageAus(V(0, 1.36, -0.55), V(0, 0.24, 0.55)) with { B = 0.32, H = 0.31 };
        var (b, hh, _) = SchwebeHinweise.Groesse("Problem-Marker gesetzt", 0.012);
        var frei = SchwebeHinweise.Ausweichen(p, Auge, b, hh, [fenster]);
        Assert.NotEqual(p, frei);
        Assert.True(frei.Y <= Auge.Y - SchwebeHinweise.UnterAugeMinM);   // unter der Blickmitte
        Assert.False(Glas.Ueberdeckt(XrTabletLogik.TabletLageAus(frei, Auge - frei) with { B = b, H = hh }, fenster));
        Assert.Equal(p, SchwebeHinweise.Ausweichen(p, Auge, b, hh, []));
        // nachgeführt: bleibt bei kleiner Kopfdrehung, gleitet bei grosser (nie starr am Kopf)
        Assert.Equal(p, SchwebeHinweise.Nachfuehren(p, Auge, Blick + 10 * Math.PI / 180, 1 / 72.0));
        var neu = SchwebeHinweise.Nachfuehren(p, Auge, Blick + 60 * Math.PI / 180, 1 / 72.0);
        Assert.True(neu.Abstand(p) > 0 && neu.Abstand(SchwebeHinweise.Allgemein(Auge, Blick + 60 * Math.PI / 180)) > 0.01);
    }

    [Fact]
    public void Stellschrauben_der_Hebegeste_der_Symbole_und_der_Hinweise()
    {
        static JsonElement W(string j) => JsonDocument.Parse(j).RootElement.Clone();
        var e = XrBedienEinstellungen.Vorgabe;
        Assert.Equal(HebeEinstellungen.Vorgabe, e.Hebe);
        Assert.Equal(0.15, e.Hebe.WegM);
        Assert.Equal(0.42, e.Symbole.AbstandM);
        Assert.Equal(4, e.Apps.HinweisDauerS);
        Assert.Equal(Varianten.SymboleVerteilt, e.Variante("symbole"));
        Assert.Equal(0.2, e.MitStellschraube("hebe.weg_m", W("0.2"))!.Hebe.WegM);
        Assert.Null(e.MitStellschraube("hebe.weg_m", W("0.9")));
        Assert.Equal(1.2, e.MitStellschraube("hebe.zeit_s", W("1.2"))!.Hebe.ZeitS);
        Assert.Equal(30, e.MitStellschraube("hebe.handflaeche_grad", W("30"))!.Hebe.HandflaecheGrad);
        Assert.Equal(0.5, e.MitStellschraube("hebe.gleichzeitig_s", W("0.5"))!.Hebe.GleichzeitigS);
        Assert.Equal(0.5, e.MitStellschraube("symbole.abstand_m", W("0.5"))!.Symbole.AbstandM);
        Assert.Null(e.MitStellschraube("symbole.abstand_m", W("1.5")));
        Assert.Equal(0.25, e.MitStellschraube("symbole.hoehe_m", W("0.25"))!.Symbole.HoeheM);
        Assert.Equal(1.3, e.MitStellschraube("symbole.streuung", W("1.3"))!.Symbole.Streuung);
        Assert.Equal(20, e.MitStellschraube("symbole.wartezeit_s", W("20"))!.Symbole.WartezeitS);
        Assert.Equal(1, e.MitStellschraube("zeiger.tooltip_s", W("1"))!.Symbole.TooltipS);
        Assert.Equal(6, e.MitStellschraube("hinweis.dauer_s", W("6"))!.Apps.HinweisDauerS);
        var bogen = e.MitStellschraube("labor.variante.symbole", W("\"bogen\""))!;
        Assert.Equal(Varianten.SymboleBogen, bogen.Symbole.Verteilung);
        Assert.Null(e.MitStellschraube("labor.variante.symbole", W("\"kreis\"")));
        Assert.Equal(e, bogen.MitStellschraube("labor.variante.symbole", W("null")));
        Assert.All(new[] { "hebe.weg_m", "symbole.streuung", "zeiger.tooltip_s", "hinweis.dauer_s", "labor.variante.symbole" }, k => Assert.Contains(k, XrBedienEinstellungen.Schluessel));
        // Wegschieben
        Assert.Equal(SchiebeEinstellungen.Vorgabe, e.Schieben);
        Assert.Equal(0.4, e.MitStellschraube("schieben.startabstand_m", W("0.4"))!.Schieben.StartabstandM);
        Assert.Null(e.MitStellschraube("schieben.startabstand_m", W("0.9")));
        Assert.Equal(0.2, e.MitStellschraube("schieben.weg_m", W("0.2"))!.Schieben.WegM);
        Assert.Equal(1.1, e.MitStellschraube("schieben.zeit_s", W("1.1"))!.Schieben.ZeitS);
        Assert.Equal(35, e.MitStellschraube("schieben.handflaeche_grad", W("35"))!.Schieben.HandflaecheGrad);
        Assert.Equal(0.6, e.MitStellschraube("schieben.gleichzeitig_s", W("0.6"))!.Schieben.GleichzeitigS);
        Assert.Equal(e, e.MitStellschraube("schieben.weg_m", W("0.2"))!.MitStellschraube("schieben.weg_m", W("null")));
    }
}
