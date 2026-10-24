// Tests Quest App V1a: Erscheinungsbild im Stil von visionOS (Glas): Massstab in pt, Abstände der Ziele (HIG «at least 60 pts apart»), Teile des Fensters (Register
// links, Griff und Schliessen darunter, Werkzeugleiste an der Unterkante), Verankerung im Raum (folgt dem Kopf nicht), Fenster neben dem Tisch (Befund Prüfbild),
// Hinweise auf dem Glas statt frei im Raum (Befund Prüfbild), Strahl auf die Ziele, Bewegungen, Symbole, neue Stellschrauben.

using System.Text.Json;
using Hlks.Xr.Bedienung;
using Hlks.Xr.Sitzung;
using static Hlks.Xr.Tests.T;

namespace Hlks.Xr.Tests;

public class V1aGlasTests
{
    static readonly AppEinstellungen Vorgabe = new();
    static GlasMasse M => GlasMasse.Fuer(Vorgabe);
    static double Hyp(double a, double b) => Math.Sqrt(a * a + b * b);

    [Fact]
    public void Massstab_in_pt_und_Grösse_nach_den_Stellschrauben()
    {
        Nah(GlasStil.PtM, 0.0009, 1e-12);          // 1.2 mm/m × 0.5 m × 1.5
        Nah(GlasStil.SchriftPtM, 0.0006, 1e-12);
        Nah(M.FensterB, 0.324, 1e-9);              // 1.8 × apps.groesse_m
        Nah(M.Schrift(GlasStil.TextPt), 0.0102, 1e-9);
        var gross = GlasMasse.Fuer(Vorgabe with { GroesseM = 0.36, Skala = 2.7 });
        Nah(gross.FensterB, 0.648, 1e-9);
        Nah(gross.Schrift(GlasStil.TextPt), 0.0102 * 4, 1e-9);   // Grösse × Skala
        Nah(GlasMasse.Fuer(Vorgabe with { Anordnung = Anordnungen.AnHand }).FensterB, 0.162, 1e-9);
        // Ziele mindestens 60 pt = 54 mm auseinander, die Knöpfe 52 pt hoch (Fingerziel gut 4.6 cm)
        Assert.True(GlasStil.ZeilenMittePt >= 60 && GlasStil.RegisterMittePt >= 60);
        Nah(M.M(GlasStil.ZeilePt), 0.0468, 1e-9);
    }

    [Fact]
    public void Inhalt_stapelt_Ziele_mit_60_pt_Abstand_ohne_Überlappung()
    {
        var f = Glas.Fenster(V(0, 1.2, -0.45), V(0, 1.6, 0), M);
        var r = Glas.Rahmen(f, M, 6);
        var inhalt = new GlasInhalt(M, r.Inhalt.B, r.Inhalt.H);
        inhalt.Titel("Messen");
        inhalt.Schalter("an", "Messen", true);
        inhalt.Reihe([("messband", "Messband", true), ("zwei", "Zwei Punkte", false)]);
        inhalt.Knopf("loeschen", "Messungen löschen", GlasSymbole.Loeschen);
        var frei = inhalt.FreieZeilen;
        for (int i = 0; i < frei; i++) inhalt.Zeile($"m{i}", $"{i} · 1234 mm", "dx …");
        Assert.True(frei >= 1, $"frei {frei}");
        Assert.Equal(0, inhalt.FreieZeilen);
        var ziele = inhalt.Elemente.Where(e => e.Ziel).ToList();
        // alle im Inhaltsbereich, Mitten senkrecht mindestens 60 pt auseinander (Zeilen), Reihe nebeneinander
        foreach (var e in ziele)
        {
            Assert.True(Math.Abs(e.X) + e.B / 2 <= r.Inhalt.B / 2 + 1e-9, e.Id);
            Assert.True(e.Y - e.H / 2 >= -r.Inhalt.H / 2 - 1e-9 && e.Y + e.H / 2 <= r.Inhalt.H / 2 + 1e-9, e.Id);
        }
        var reihen = ziele.GroupBy(e => Math.Round(e.Y, 6)).Select(g => g.Key).OrderByDescending(y => y).ToList();
        for (int i = 1; i < reihen.Count; i++) Assert.True(reihen[i - 1] - reihen[i] >= M.M(GlasStil.ZeilenMittePt) - 1e-9);
        var seg = ziele.Where(e => e.Art == GlasArt.Segment).ToList();
        Assert.Equal(2, seg.Count);
        Assert.True(seg[1].X - seg[0].X >= seg[0].B);
        Assert.Equal(GlasForm.Kapsel, seg[0].Form);
        Assert.True(seg[0].Ein);
        Assert.Equal(GlasForm.Rechteck, ziele.First(e => e.Art == GlasArt.Zeile).Form);   // abgerundetes Rechteck nur im senkrechten Stapel
        var k = ziele.Single(e => e.Id == "loeschen");
        Assert.Equal(GlasForm.Kapsel, k.Form);
        Assert.True(k.B < r.Inhalt.B);
        // Lage im Raum: 2 mm vor dem Glas, angehoben weiter vorn
        var lage = Glas.Lage(r.Inhalt, k);
        var hoch = Glas.Lage(r.Inhalt, k, GlasStil.AnhebenM);
        Nah((hoch.Mitte - lage.Mitte).Skalar(lage.Normale), GlasStil.AnhebenM, 1e-9);
    }

    [Fact]
    public void Fensterteile_Register_links_Griff_und_Schliessen_unten_Ornament_davor()
    {
        var auge = V(0, 1.6, 0);
        var f = Glas.Fenster(V(0, 1.32, -0.45), auge, M);
        Nah(f.Normale.Skalar((auge - f.Mitte).Einheit()), 1, 1e-9);   // zum Auge gedreht
        var r = Glas.Rahmen(f, M, 5, 0, 2);
        Assert.Equal(5, r.Register.Count);
        Assert.All(r.Register, t => Assert.True((t.Mitte - f.Mitte).Skalar(f.Rechts) < -f.B / 2));   // links vom Fenster
        for (int i = 1; i < 5; i++) Nah(r.Register[i - 1].Mitte.Abstand(r.Register[i].Mitte), M.M(GlasStil.RegisterMittePt), 1e-9);
        // aufgeklappt breiter, Kreise gleich hoch
        var auf = Glas.Rahmen(f, M, 5, 1, 2);
        Assert.True(auf.RegisterLeiste.B > r.RegisterLeiste.B + 0.05);
        Nah(auf.Register[0].H, r.Register[0].H, 1e-12);
        // Griff unter dem Fenster, Schliessen links daneben (rund), auf gleicher Höhe
        Assert.True((r.Griff.Mitte - f.Mitte).Skalar(f.Oben) < -f.H / 2);
        Nah((r.Schliessen.Mitte - r.Griff.Mitte).Skalar(f.Oben), 0, 1e-9);
        Assert.True((r.Schliessen.Mitte - r.Griff.Mitte).Skalar(f.Rechts) < 0);
        Nah(r.Schliessen.B, r.Schliessen.H, 1e-12);
        // Ornament an der Unterkante (ragt 10 pt hinauf, deckt die Fusszeile nicht), vor dem Glas; Griff und Schliessen darunter
        Assert.NotNull(r.Ornament);
        Nah((r.Ornament!.Mitte - f.Mitte).Skalar(f.Oben) + r.Ornament.H / 2, -f.H / 2 + M.M(GlasStil.OrnamentUeberlappPt), 1e-9);
        Assert.True((r.Ornament.Mitte - f.Mitte).Skalar(f.Oben) + r.Ornament.H / 2 < (r.Fuss.Mitte - f.Mitte).Skalar(f.Oben) - r.Fuss.H / 2);
        Assert.True((r.Griff.Mitte - f.Mitte).Skalar(f.Oben) + r.Griff.H / 2 < (r.Ornament.Mitte - f.Mitte).Skalar(f.Oben) - r.Ornament.H / 2);
        Assert.True((r.Ornament.Mitte - f.Mitte).Skalar(f.Normale) > 0.01);
        Assert.Equal(2, r.OrnamentKnoepfe.Count);
        Assert.Equal(5 + 2 + 2, r.Rand().Count());
        // Fusszeile innerhalb des Fensters, unten
        Assert.True((r.Fuss.Mitte - f.Mitte).Skalar(f.Oben) < 0 && (r.Fuss.Mitte - f.Mitte).Skalar(f.Oben) - r.Fuss.H / 2 > -f.H / 2);
        // zurückweichen: weiter vom Auge
        var zurueck = Glas.Fenster(V(0, 1.32, -0.45), auge, M, GlasStil.ZurueckM);
        Nah(zurueck.Mitte.Abstand(auge) - f.Mitte.Abstand(auge), GlasStil.ZurueckM, 1e-9);
    }

    [Fact]
    public void Verankert_im_Raum_folgt_dem_Kopf_nicht_nur_beim_Weggehen_und_Holen()
    {
        var a = new GlasAnker();
        var auge = V(0, 1.6, 0);
        var platz = Glas.Platz(auge, Math.PI / 2, Vorgabe);
        // 30° links neben dem Blick (−z), auf Brusthöhe, 0.45 m
        Nah(platz.Y, 1.6 - GlasStil.UnterAugeM, 1e-12);
        Nah(Hyp(platz.X, platz.Z), 0.45, 1e-9);
        Nah(Math.Atan2(-platz.Z, platz.X) * 180 / Math.PI, 120, 1e-9);
        Assert.True(Glas.Pruefen(a, auge, platz, 0));    // noch keins: setzt
        Nah(GlasStil.Aufbau(0).Deckkraft, 0);
        Nah(GlasStil.Aufbau(GlasStil.AufbauS).Deckkraft, 1);
        Nah(GlasStil.Aufbau(GlasStil.AufbauS).Groesse, 1);
        // Kopf drehen und ein Stück gehen: es bleibt stehen
        Assert.False(Glas.Pruefen(a, V(0.8, 1.6, 0.5), Glas.Platz(V(0.8, 1.6, 0.5), 0, Vorgabe), 1));
        Assert.Equal(platz, a.Mitte);
        // weit weg: kommt nach
        Assert.True(Glas.Pruefen(a, V(3, 1.6, 0), Glas.Platz(V(3, 1.6, 0), 0, Vorgabe), 2));
        Nah(a.Seit, 2);
        // Griff: die Hand führt, loslassen behält
        Glas.Fassen(a, V(3.1, 1.0, -0.4));
        var vorher = a.Mitte!.Value;
        Glas.Ziehen(a, V(3.3, 1.1, -0.4));
        Nah(a.Mitte!.Value.Abstand(vorher + V(0.2, 0.1, 0)), 0, 1e-12);
        Assert.False(Glas.Pruefen(a, V(10, 1.6, 0), V(10, 1, 0), 3));   // gefasst: nichts nachholen
        Glas.Ziehen(a, null);
        Assert.False(a.Gezogen);
        Glas.Holen(a, V(1, 1, 1), 5);
        Assert.Equal(V(1, 1, 1), a.Mitte);
    }

    [Fact]
    public void Am_Sitzungstisch_steht_das_Fenster_neben_dem_Tisch()
    {
        // virtueller Raum vor dem Kopf: Kopf über dem Ursprung, Tisch 1.6 × 1.0 m, Vorderkante 0.4 m vor dem Kopf (Raum-lokal = Raum der Brille)
        var rl = SitzungRaum.VorKopf(V(0, 1.2, 0), Math.PI / 2);
        double hb = XrSitzungLogik.TischBreite / 2, ht = XrSitzungLogik.TischTiefe / 2;
        var tm = XrSitzungLogik.TischMitte;
        var tisch = new[] { V(tm.X - hb, 0.75, tm.Z - ht), V(tm.X + hb, 0.75, tm.Z - ht), V(tm.X + hb, 0.75, tm.Z + ht), V(tm.X - hb, 0.75, tm.Z + ht) }
            .Select(rl.ZuRef).ToList();
        var auge = V(0, 1.2, 0);
        var blick = Math.PI / 2;   // zum Tisch (−z)
        // nach der Stellschraube allein (30° links) stünde es über dem Tisch
        var poly = tisch.Select(p => new Xz(p.X, p.Z)).ToList();
        Assert.True(Glas.UeberTisch(Glas.Fenster(Glas.Platz(auge, blick, Vorgabe), auge, M), poly, 0.05));
        var mitte = Glas.NebenTisch(auge, blick, Vorgabe, M, tisch);
        var f = Glas.Fenster(mitte, auge, M);
        Assert.False(Glas.UeberTisch(f, poly, 0.05));
        Assert.True(mitte.X < 0);   // links
        Assert.True(Hyp(mitte.X, mitte.Z) <= Vorgabe.AbstandM + 1e-9);   // im Griffbereich
        Assert.True(Hyp(mitte.X, mitte.Z) >= 0.25 - 1e-9);
    }

    [Fact]
    public void Hinweise_auf_dem_Glas_nie_frei_über_anderen_Flächen()
    {
        var auge = V(0, 1.6, 0);
        var f = Glas.Fenster(V(0, 1.32, -0.45), auge, M);
        var r = Glas.Rahmen(f, M, 5);
        // Fenster sichtbar: in der Fusszeile
        var (lage, fuss) = Glas.HinweisLage(r, auge, Math.PI / 2, M, [], 0.3);
        Assert.True(fuss);
        Assert.Equal(r.Fuss, lage);
        // ohne Fenster: eigene Kapsel unten vor dem Blick, rückt unter eine Karte, die dort steht
        var (frei, f2) = Glas.HinweisLage(null, auge, Math.PI / 2, M, [], 0.3);
        Assert.False(f2);
        Nah(frei.Mitte.Y, 1.6 - 0.22, 1e-9);
        var karte = frei with { B = 0.3, H = 0.17 };
        var (unter, _) = Glas.HinweisLage(null, auge, Math.PI / 2, M, [karte], 0.3);
        Assert.False(Glas.Ueberdeckt(unter, karte));
        Assert.True(unter.Mitte.Y < frei.Mitte.Y);
        Assert.True(Glas.Ueberdeckt(frei, karte));
        Assert.False(Glas.Ueberdeckt(frei, karte with { Mitte = karte.Mitte + V(0, 0, -1) }));   // weit dahinter
    }

    [Fact]
    public void Strahl_wählt_beim_Loslassen_auf_demselben_Ziel()
    {
        var auge = V(0, 1.6, 0);
        var f = Glas.Fenster(V(0, 1.32, -0.45), auge, M);
        var r = Glas.Rahmen(f, M, 3);
        var ziele = r.Register;
        var ziel = ziele[1].Mitte;
        var s = (V(0.1, 1.2, 0), (ziel - V(0.1, 1.2, 0)).Einheit());
        Assert.Equal(1, Glas.Strahl(ziele, s.Item1, s.Item2)!.Value.Index);
        Assert.Null(Glas.Strahl(ziele, s.Item1, -s.Item2));   // von hinten bzw. weg: nichts
        var z = new GlasStrahlHand();
        Assert.Null(GlasStrahl.Schritt(z, ziele, s, false));
        Assert.Equal(1, z.Hover);
        Assert.Null(GlasStrahl.Schritt(z, ziele, s, true));
        Assert.Equal(1, GlasStrahl.Schritt(z, ziele, s, false));
        // gedrückt auf 1, losgelassen daneben: nichts
        GlasStrahl.Schritt(z, ziele, s, true);
        var daneben = (V(0.1, 1.2, 0), (ziele[0].Mitte - V(0.1, 1.2, 0)).Einheit());
        Assert.Null(GlasStrahl.Schritt(z, ziele, daneben, false));
        Assert.Null(GlasStrahl.Schritt(z, ziele, null, false));
        Assert.Null(z.Hover);
    }

    [Fact]
    public void Glas_tönt_sich_nach_der_Umgebung_im_hellen_Raum_dunkler()
    {
        // Befund Prüfbild 24.10.2026: im hellen Sitzungsraum war das Glas zu hell für weisse Schrift
        var ziel = 0.42;
        var hell = GlasStil.Toenung(GlasStil.HintergrundHell, 0.75, ziel);
        var modell = GlasStil.Toenung(GlasStil.HintergrundModell, 0.75, ziel);
        var dunkel = GlasStil.Toenung(GlasStil.HintergrundDunkel, 0.75, ziel);
        Assert.True(hell.G < modell.G && modell.G < dunkel.G);   // im hellen Raum dunkleres Glas
        foreach (var (f, bg) in new[] { (hell, GlasStil.HintergrundHell), (modell, GlasStil.HintergrundModell), (dunkel, GlasStil.HintergrundDunkel) })
        {
            Nah(GlasStil.Wirkt(f, bg), ziel, 1e-9);   // wirkt überall gleich hell
            Assert.True(f.G >= GlasStil.GlasMin && f.G <= GlasStil.GlasMax);
            Assert.True(f.B > f.R);   // leicht kühl, neutral
        }
        Nah(hell.A, 0.75, 1e-12);
        // sehr durchscheinend im hellen Raum: das dunkelste Glas reicht nicht – es deckt mehr (höchstens 0.95)
        var duenn = GlasStil.Toenung(GlasStil.HintergrundHell, 0.3, ziel);
        Assert.True(duenn.A > 0.3 && duenn.A <= 0.95);
        Nah(GlasStil.Wirkt(duenn, GlasStil.HintergrundHell), ziel, 1e-6);
        // sehr dunkler Hintergrund mit hellem Ziel: nie heller als GlasMax
        Nah(GlasStil.Toenung(0, 0.75, 0.6).G, GlasStil.GlasMax, 1e-12);
        // Umgebung der App: Passthrough geht vor, dann Begehen (Modell), Sitzungsraum hell bzw. dunkel
        Assert.Equal(GlasStil.HintergrundEcht, GlasStil.HintergrundFuer(true, true, true));
        Assert.Equal(GlasStil.HintergrundModell, GlasStil.HintergrundFuer(false, false, true));
        Assert.Equal(GlasStil.HintergrundHell, GlasStil.HintergrundFuer(true, false, true));
        Assert.Equal(GlasStil.HintergrundDunkel, GlasStil.HintergrundFuer(true, false, false));
    }

    [Fact]
    public void Geisterhand_führt_neben_der_Registerleiste_vor_nie_über_ihr()
    {
        // Befund Prüfbild 24.10.2026: die Geisterhand lag über der Registerleiste
        var auge = V(0, 1.6, 0);
        var f = Glas.Fenster(V(0, 1.32, -0.45), auge, M);
        var r = Glas.Rahmen(f, M, 6, 0, 2);
        var ziel = Geisterhand.Neben(r.Register[1], r.RegisterLeiste);
        var hand = Geisterhand.Vorlage(ziel.Mitte + ziel.Normale * Geisterhand.AnlaufM, ziel);
        Assert.Equal(25, hand.Count);
        Assert.True(HandGelenke.WebXr.All(hand.ContainsKey));
        var linkerRand = (r.RegisterLeiste.Mitte - f.Mitte).Skalar(f.Rechts) - r.RegisterLeiste.B / 2;
        Nah((ziel.Mitte - f.Mitte).Skalar(f.Oben), (r.Register[1].Mitte - f.Mitte).Skalar(f.Oben), 1e-9);   // gleiche Höhe wie das Register
        foreach (var t in new[] { 0.0, Geisterhand.DauerS / 4, Geisterhand.DauerS / 2 - 0.01 })
        {
            var b = Geisterhand.Bild(GeisterGeste.Antippen, t, hand, ziel)!;
            Assert.All(b.Gelenke.Values, p => Assert.True((p - f.Mitte).Skalar(f.Rechts) < linkerRand, "ein Gelenk liegt über der Leiste"));
        }
    }

    [Fact]
    public void Folgen_Farben_Symbole_und_Stellschrauben()
    {
        Nah(GlasStil.Folgen(0, 1, 1e9, 0.25), 1, 1e-9);
        Nah(GlasStil.Folgen(0, 1, 0, 0.25), 0);
        Assert.InRange(GlasStil.Folgen(0, 1, 0.25, 0.25), 0.6, 0.65);
        Assert.Equal(1, GlasStil.TextPrimaer.A);
        Assert.True(GlasStil.TextSekundaer.A < GlasStil.TextPrimaer.A && GlasStil.TextTertiaer.A < GlasStil.TextSekundaer.A);
        Assert.True(Math.Abs(GlasStil.Glas.R - GlasStil.Glas.B) < 0.1);   // neutral, nicht farbig
        Assert.Equal(new GlasFarbe(0.5, 0.5, 0.5, 1).Heller(1), new GlasFarbe(1, 1, 1, 1));
        foreach (var s in GlasSymbole.Alle)
        {
            var l = GlasSymbole.Linien(s);
            Assert.NotEmpty(l);
            Assert.All(l.SelectMany(x => x), p => Assert.True(Math.Abs(p.X) <= 0.5 + 1e-9 && Math.Abs(p.Y) <= 0.5 + 1e-9, s));
        }
        Assert.Empty(GlasSymbole.Linien("gibtsnicht"));
        // neue Stellschrauben V1a
        static JsonElement W(string j) => JsonDocument.Parse(j).RootElement.Clone();
        var e = XrBedienEinstellungen.Vorgabe;
        Assert.Equal(30, e.Apps.SeitlichGrad);
        Assert.Equal(0.75, e.Apps.GlasDeckkraft);
        Assert.Equal(0.42, e.Apps.GlasHelligkeit);
        Assert.Equal(0.3, e.MitStellschraube("anzeige.glas_helligkeit", W("0.3"))!.Apps.GlasHelligkeit);
        Assert.Null(e.MitStellschraube("anzeige.glas_helligkeit", W("0.9")));
        Assert.Equal(e, e.MitStellschraube("anzeige.glas_helligkeit", W("0.3"))!.MitStellschraube("anzeige.glas_helligkeit", W("null")));
        Assert.Equal(50, e.MitStellschraube("apps.seitlich_grad", W("50"))!.Apps.SeitlichGrad);
        Assert.Null(e.MitStellschraube("apps.seitlich_grad", W("120")));
        Assert.Equal(0.8, e.MitStellschraube("anzeige.glas_deckkraft", W("0.8"))!.Apps.GlasDeckkraft);
        Assert.Null(e.MitStellschraube("anzeige.glas_deckkraft", W("0.1")));
        Assert.True(e.UmgebungHell);
        var tron = e.MitStellschraube("labor.variante.umgebung", W("\"tron\""))!;
        Assert.False(tron.UmgebungHell);
        Assert.Equal("tron", tron.Variante("umgebung"));
        Assert.Null(e.MitStellschraube("labor.variante.umgebung", W("\"wald\"")));
        Assert.Equal(e, tron.MitStellschraube("labor.variante.umgebung", W("null")));
    }
}
