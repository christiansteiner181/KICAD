// Tests der Quest App V1c (Modul XR, XR-I): der virtuelle Bildschirm im Raum (Form, Glasnetz, Platz frontal, Kommen von links und der Hand folgen, Setzen,
// Mitlaufen beim gemeinsamen Wegwischen und Hinausgleiten, verankert, Griff, Schliessen, Monitor, wegwischbares Ziel, belegte Flächen) und das Format der Bilder
// in der App (Raster, Kopf, Puffer). Rein, ohne StereoKit.

using System.Buffers.Binary;
using System.IO.Compression;
using Hlks.Xr.Bedienung;
using Hlks.Xr.Desktop;

namespace Hlks.Xr.Tests;

public class DesktopAnsichtTests
{
    static readonly DesktopEinstellungen E = DesktopEinstellungen.Vorgabe;
    static readonly Vec3 Auge = new(0.5, 1.6, 2.0);
    const double Nord = Math.PI / 2;   // Blick nach −z

    static SeitenWischStand Stand(SeitenWischStand hinein = default) => hinein;
    static SeitenWischStand Laeuft(double f) => new(false, f, true, 0.1);
    static readonly SeitenWischStand Ausgeloest = new(true, 1, false, 0.25);

    [Fact]
    public void Form_leicht_gebogen_mit_Mitte_im_Ursprung_und_Raendern_zum_Betrachter()
    {
        var m = DesktopMasse.Aus(E, 1920, 1080);
        Assert.Equal(1.4, m.BreiteM, 9);
        Assert.Equal(1.4 * 1080 / 1920, m.HoeheM, 9);
        Assert.Equal(2.0, m.RadiusM, 9);
        Assert.Equal(new Vec3(0, 0.1, 0), DesktopFlaeche.Punkt(0, 0.1, m.RadiusM));
        var rand = DesktopFlaeche.Punkt(0.7, 0, m.RadiusM);
        Assert.True(rand.Z > 0.1 && rand.Z < 0.13, $"Rand {rand.Z:0.000} m zum Betrachter");   // R (1 − cos 0.35) = 0.121 m
        Assert.Equal(m.Tiefe, rand.Z, 9);
        Assert.InRange(m.Sehne, 1.37, 1.4);
        // Punkt auf der Fläche zurück: Bogen, Höhe, Tiefe 0
        var (s, v, t) = DesktopFlaeche.AufFlaeche(DesktopFlaeche.Punkt(-0.4, 0.2, m.RadiusM), m.RadiusM);
        Assert.Equal(-0.4, s, 9);
        Assert.Equal(0.2, v, 9);
        Assert.Equal(0, t, 9);
        // vor der Fläche (zum Betrachter) ist die Tiefe positiv
        Assert.True(DesktopFlaeche.AufFlaeche(new Vec3(0, 0, 0.05), m.RadiusM).Tiefe > 0.049);
        // Welt ↔ lokal
        var w = DesktopFlaeche.Welt(new Vec3(0.3, -0.1, 0.02), new Vec3(1, 1.5, -1), 0.7);
        Assert.True(DesktopFlaeche.Lokal(w, new Vec3(1, 1.5, -1), 0.7).Abstand(new Vec3(0.3, -0.1, 0.02)) < 1e-12);
        // radius kleiner als B/π würde mehr als einen Halbkreis ergeben: begrenzt
        Assert.True(DesktopMasse.Aus(E with { RadiusM = 0.1 }).Winkel <= Math.PI + 1e-9);
    }

    [Fact]
    public void Netze_der_Felder_decken_den_Bildschirm_ohne_Luecke_mit_Texturkoordinaten_von_oben_links()
    {
        var r = new DesktopRaster(1920, 1080, 64);
        Assert.Equal(30, r.Spalten);
        Assert.Equal(17, r.Zeilen);
        Assert.Equal(4, r.FeldSpalten);
        Assert.Equal(3, r.FeldZeilen);
        Assert.Equal((1536, 1024, 384, 56), r.FeldRechteck(11));
        Assert.Equal(11, r.FeldVonKachel(29 + 16 * 30));
        var m = DesktopMasse.Aus(E, 1920, 1080);
        var netze = Enumerable.Range(0, r.Felder).Select(f => DesktopFlaeche.Feld(r, f, m)).ToList();
        // links oben beginnt das erste Feld, rechts unten endet das letzte
        Assert.True(netze[0].Positionen[0].Abstand(DesktopFlaeche.Punkt(-0.7, m.HoeheM / 2, m.RadiusM)) < 1e-9);
        Assert.Equal((0.0, 0.0), netze[0].Uv[0]);
        var letzt = netze[^1];
        Assert.True(letzt.Positionen[^1].Abstand(DesktopFlaeche.Punkt(0.7, -m.HoeheM / 2, m.RadiusM)) < 1e-9);
        Assert.Equal((1.0, 1.0), letzt.Uv[^1]);
        // Nachbarn teilen den Rand (keine Lücke)
        var k0 = netze[0].Positionen.Length / 2;
        Assert.True(netze[0].Positionen[k0 - 1].Abstand(netze[1].Positionen[0]) < 1e-9);
        // alle Dreiecke zeigen zum Betrachter (Normale der Fläche, +z)
        foreach (var n in netze)
            for (int i = 0; i < n.Indizes.Length; i += 3)
            {
                var (a, b, c) = (n.Positionen[n.Indizes[i]], n.Positionen[n.Indizes[i + 1]], n.Positionen[n.Indizes[i + 2]]);
                Assert.True((b - a).Kreuz(c - a).Skalar(n.Normalen[n.Indizes[i]]) > 0);
            }
        // Rahmen mit gerundeten Ecken liegt hinter der Fläche und ist grösser
        var rahmen = DesktopFlaeche.Rahmen(m, 0.02, 0.03, 0.006);
        Assert.True(rahmen.Positionen.Max(p => p.Y) <= m.HoeheM / 2 + 0.02 + 1e-9);
        Assert.True(rahmen.Positionen.Max(p => p.Y) > m.HoeheM / 2 + 0.019);
        Assert.True(rahmen.Positionen[0].Y < m.HoeheM / 2 + 0.02 - 0.005, "Ecke gerundet");
    }

    [Fact]
    public void Platz_frontal_vor_den_Augen_und_Kommen_von_links_der_Hand_folgend()
    {
        var a = new DesktopAnsicht();
        Assert.False(a.Sichtbar);
        // die Hand beginnt: Bildschirm kommt, ganz links und durchsichtig
        a.Schritt(0, 1 / 72.0, Auge, Nord, Stand(Laeuft(0.0)), E);
        Assert.Equal(DesktopPhase.Kommt, a.Phase);
        Assert.True(a.Lage!.Mitte.Abstand(Auge + new Vec3(0, -0.10, -1.1)) < 1e-9);
        var (m0, g0) = a.Jetzt!.Value;
        Assert.True(m0.X < Auge.X - 1.0, "ganz links");                 // 95° links von Norden: fast im Westen
        Assert.Equal(Nord + DesktopAnsicht.AusGrad * Math.PI / 180, g0, 9);
        Assert.Equal(0, a.Deckkraft, 9);
        // die Hand ist auf halbem Weg: der Bildschirm folgt (nach kurzer Glättung)
        for (int i = 1; i <= 20; i++) a.Schritt(i / 72.0, 1 / 72.0, Auge, Nord, Stand(Laeuft(0.5)), E);
        Assert.InRange(a.Anteil, 0.49, 0.51);
        var (m1, _) = a.Jetzt!.Value;
        Assert.True(m1.X < Auge.X && m1.X > m0.X, "auf dem Weg von links");
        Assert.Equal(1.1, Math.Sqrt((m1.X - Auge.X) * (m1.X - Auge.X) + (m1.Z - Auge.Z) * (m1.Z - Auge.Z)), 9);  // auf dem Kreis um die Augen
        // ausgelöst: setzt sich weich, dann da und ruhig
        a.Schritt(0.3, 1 / 72.0, Auge, Nord, Stand(Ausgeloest), E);
        Assert.Equal(DesktopPhase.Setzt, a.Phase);
        Assert.Equal(0.3, a.EingeblendetT);
        double t = 0.3;
        for (int i = 0; i < 40; i++) { t += 1 / 72.0; a.Schritt(t, 1 / 72.0, Auge, Nord, Stand(), E); }
        Assert.Equal(DesktopPhase.Da, a.Phase);
        Assert.Equal(1, a.Anteil);
        var (m2, g2) = a.Jetzt!.Value;
        Assert.True(m2.Abstand(Auge + new Vec3(0, -0.10, -1.1)) < 1e-9);
        Assert.Equal(Nord, g2, 9);
        // verankert: der Kopf bewegt und dreht sich, der Bildschirm bleibt
        for (int i = 0; i < 30; i++) { t += 1 / 72.0; a.Schritt(t, 1 / 72.0, Auge + new Vec3(0.4, -0.3, 0.2), 0.3, Stand(), E); }
        Assert.True(a.Jetzt!.Value.Mitte.Abstand(m2) < 1e-9);
    }

    [Fact]
    public void Abgebrochenes_Hineinziehen_gleitet_zurueck_Wegwischen_folgt_der_Hand_hinaus_nach_links()
    {
        var a = new DesktopAnsicht();
        double t = 0;
        for (int i = 0; i < 20; i++) { a.Schritt(t, 1 / 72.0, Auge, Nord, Stand(Laeuft(0.4)), E); t += 1 / 72.0; }
        Assert.True(a.Anteil > 0.3);
        for (int i = 0; i < 60; i++) { a.Schritt(t, 1 / 72.0, Auge, Nord, Stand(), E); t += 1 / 72.0; }
        Assert.Equal(DesktopPhase.Aus, a.Phase);
        Assert.False(a.Sichtbar);
        // einblenden, dann wegwischen: folgt der Hand (Geht), ausgelöst gleitet er hinaus
        a.Einblenden(t, Auge, Nord, E);
        for (int i = 0; i < 40; i++) { t += 1 / 72.0; a.Schritt(t, 1 / 72.0, Auge, Nord, Stand(), E); }
        Assert.Equal(DesktopPhase.Da, a.Phase);
        // gemeinsames Wegwischen (V1a): die Geste meldet je Bild den Weg der Hand nach links (Mitlaufen), er dreht sich so weit nach links wie das Symbolfeld
        for (int i = 0; i < 20; i++) { t += 1 / 72.0; a.Mitlaufen(0.15); a.Schritt(t, 1 / 72.0, Auge, Nord, Stand(), E); }
        Assert.Equal(DesktopPhase.Geht, a.Phase);
        var soll = 1 - DesktopAnsicht.MitlaufAnteil(0.15);
        Assert.InRange(a.Anteil, soll - 0.01, soll + 0.01);
        Assert.InRange(soll, 0.7, 0.85);   // 0.15 m × 1.4 = 0.21 m in 0.6 m: 19° von 95°
        Assert.True(a.Jetzt!.Value.Mitte.X < Auge.X - 0.2, "nach links unterwegs");
        // Ereignis (DesktopBildschirm.Wegwischen): gleitet um den Kopf von jetzt hinaus
        t += 1 / 72.0;
        a.Ausblenden(t, Auge);
        a.Schritt(t, 1 / 72.0, Auge, Nord, Stand(), E);
        Assert.Equal(DesktopPhase.Raus, a.Phase);
        var vorher = a.Jetzt!.Value.Mitte;
        t += 1 / 72.0;
        a.Schritt(t, 1 / 72.0, Auge, Nord, Stand(), E);
        Assert.True(a.Jetzt!.Value.Mitte.Abstand(vorher) < 0.05, "kein Sprung beim Wechsel in «Raus»");
        for (int i = 0; i < 30; i++) { t += 1 / 72.0; a.Schritt(t, 1 / 72.0, Auge, Nord, Stand(), E); }
        Assert.Equal(DesktopPhase.Aus, a.Phase);
        // ein Wegwischen, das abbricht, lässt ihn zurückkommen
        a.Einblenden(t, Auge, Nord, E);
        for (int i = 0; i < 40; i++) { t += 1 / 72.0; a.Schritt(t, 1 / 72.0, Auge, Nord, Stand(), E); }
        for (int i = 0; i < 10; i++) { t += 1 / 72.0; a.Mitlaufen(0.08); a.Schritt(t, 1 / 72.0, Auge, Nord, Stand(), E); }
        Assert.Equal(DesktopPhase.Geht, a.Phase);
        for (int i = 0; i < 60; i++) { t += 1 / 72.0; a.Schritt(t, 1 / 72.0, Auge, Nord, Stand(), E); }   // ohne Mitlaufen: zurück
        Assert.Equal(DesktopPhase.Da, a.Phase);
        // Mitlaufen: mit dem Weg der Hand monoton, begrenzt; 0 ohne Weg
        Assert.Equal(0, DesktopAnsicht.MitlaufAnteil(0), 12);
        Assert.Equal(0, DesktopAnsicht.MitlaufAnteil(-0.1), 12);
        Assert.True(DesktopAnsicht.MitlaufAnteil(0.05) < DesktopAnsicht.MitlaufAnteil(0.1) && DesktopAnsicht.MitlaufAnteil(0.1) < DesktopAnsicht.MitlaufAnteil(0.18));
        Assert.True(DesktopAnsicht.MitlaufAnteil(100) <= 1);
    }

    [Fact]
    public void Griff_verschiebt_und_dreht_zum_Kopf_Schliessen_und_Monitor_loesen_einmal_aus()
    {
        var a = new DesktopAnsicht();
        a.Einblenden(0, Auge, Nord, E);
        double t = 0;
        for (int i = 0; i < 40; i++) { t += 1 / 72.0; a.Schritt(t, 1 / 72.0, Auge, Nord, Stand(), E); }
        var m = DesktopMasse.Aus(E);
        var (mitte, gieren) = a.Jetzt!.Value;
        var (griff, schliessen, monitor) = DesktopAnsicht.Teile(m);
        Vec3 W(Vec3 lokal) => DesktopFlaeche.Welt(lokal, mitte, gieren);
        // Pinch am Griff fassen und 0.5 m nach rechts ziehen
        var p0 = W(griff);
        var s0 = a.Bedienen(t, Auge, p0, true, null, m, false);
        Assert.True(s0.GriffGehalten);
        Assert.True(a.GriffGehalten);
        var p1 = p0 + new Vec3(0.5, 0, 0);
        a.Bedienen(t + 0.1, Auge, p1, true, null, m, false);
        var (neu, gNeu) = a.Jetzt!.Value;
        Assert.True(neu.Abstand(mitte + new Vec3(0.5, 0, 0)) < 1e-9);
        var blick = neu - Auge;
        Assert.Equal(VrLogik.GierenVonRichtung(new Xz(blick.X, blick.Z)), gNeu, 9);   // zum Kopf gedreht
        // loslassen: bleibt
        a.Bedienen(t + 0.2, Auge, p1, false, null, m, false);
        Assert.False(a.GriffGehalten);
        Assert.True(a.Jetzt!.Value.Mitte.Abstand(neu) < 1e-9);
        // Pinch neben dem Griff fasst nicht
        var b2 = new DesktopAnsicht();
        b2.Einblenden(0, Auge, Nord, E);
        for (int i = 0; i < 40; i++) b2.Schritt(i / 72.0 + 0.01, 1 / 72.0, Auge, Nord, Stand(), E);
        var j2 = b2.Jetzt!.Value;
        Assert.False(b2.Bedienen(1, Auge, DesktopFlaeche.Welt(griff + new Vec3(0.3, 0, 0), j2.Mitte, j2.Gieren), true, null, m, false).GriffGehalten);
        // Monitor-Knopf nur mit mehreren Monitoren; einmal, bis der Finger weg ist
        var fm = DesktopFlaeche.Welt(monitor, j2.Mitte, j2.Gieren);
        Assert.False(b2.Bedienen(1, Auge, null, false, fm, m, false).MonitorWechseln);
        Assert.True(b2.Bedienen(1.1, Auge, null, false, fm, m, true).MonitorWechseln);
        Assert.False(b2.Bedienen(1.2, Auge, null, false, fm, m, true).MonitorWechseln);
        b2.Bedienen(1.3, Auge, null, false, null, m, true);
        Assert.True(b2.Bedienen(1.4, Auge, null, false, fm, m, true).MonitorWechseln);
        // Schliessen-Knopf: blendet aus
        b2.Bedienen(1.5, Auge, null, false, null, m, true);
        var s = b2.Bedienen(1.6, Auge, null, false, DesktopFlaeche.Welt(schliessen, j2.Mitte, j2.Gieren), m, true);
        Assert.True(s.Schliessen);
        Assert.Equal(DesktopPhase.Raus, b2.Phase);
    }

    [Fact]
    public void Wegwischbares_Ziel_nur_wenn_da_mit_Flaeche_und_Zeit_des_Einblendens()
    {
        var a = new DesktopAnsicht();
        var m = DesktopMasse.Aus(E);
        Assert.Null(a.Kandidat(m));
        a.Einblenden(2.0, Auge, Nord, E);
        Assert.Null(a.Kandidat(m));   // setzt sich noch
        for (int i = 1; i <= 40; i++) a.Schritt(2.0 + i / 72.0, 1 / 72.0, Auge, Nord, Stand(), E);
        var z = a.Kandidat(m)!;
        Assert.Equal(DesktopAnsicht.ZielId, z.Id);
        Assert.Equal("desktop", z.Id);
        Assert.Equal(2.0, z.EingeblendetSeit);
        Assert.Equal(m.Sehne, z.Flaeche.B, 9);
        Assert.True(z.Flaeche.Normale.Skalar(new Vec3(0, 0, 1)) > 0.999, "Normale zum Betrachter (Süden)");
        Assert.True(z.Flaeche.Mitte.Z > a.Lage!.Mitte.Z, "Fläche zwischen Mitte und Rändern");
        // V1a wählt ihn: die Hand wischt vor ihm (im Blick) – auch neben einem älteren Ziel; ohne Treffer das zuletzt eingeblendete
        var pfad = new List<Vec3> { Auge + new Vec3(0.2, -0.2, -0.45), Auge + new Vec3(0.0, -0.2, -0.45) };
        var symbole = new WegwischKandidat("symbole", XrTabletLogik.TabletLageAus(Auge + new Vec3(1.5, 0, -0.6), new Vec3(-1, 0, 1)) with { B = 0.3, H = 0.3 }, 1.0);
        Assert.Equal("desktop", Wegwischen.Ziel([symbole, z], pfad, Auge));
        Assert.Equal("desktop", Wegwischen.Ziel([symbole, z], [Auge + new Vec3(0, 3, 0)], Auge));
        // am Griff gehalten: kein Ziel (das Verschieben wischt ihn nicht weg)
        var (griff, _, _) = DesktopAnsicht.Teile(m);
        var j = a.Jetzt!.Value;
        a.Bedienen(3.0, Auge, DesktopFlaeche.Welt(griff, j.Mitte, j.Gieren), true, null, m, false);
        Assert.True(a.GriffGehalten);
        Assert.Null(a.Kandidat(m));
        a.Bedienen(3.1, Auge, null, false, null, m, false);
        Assert.NotNull(a.Kandidat(m));
        a.Ausblenden(3.2);
        Assert.Null(a.Kandidat(m));
    }

    [Fact]
    public void Belegte_Flaechen_decken_den_Bildschirm_im_Blick_Teile_zum_Betrachter_Glasnetz_fuer_den_Shader()
    {
        var a = new DesktopAnsicht();
        var m = DesktopMasse.Aus(E);
        Assert.Empty(a.Belegt(m));
        a.Einblenden(0, Auge, Nord, E);
        for (int i = 1; i <= 40; i++) a.Schritt(i / 72.0, 1 / 72.0, Auge, Nord, Stand(), E);
        var b = a.Belegt(m);
        Assert.Equal(2, b.Count);
        var (mitte, gieren) = a.Jetzt!.Value;
        // jeder Punkt des Bildes (auch die Ränder, die zum Betrachter kommen) liegt im Blick innerhalb des ersten Rechtecks
        foreach (var s in new[] { -0.7, -0.35, 0, 0.35, 0.7 })
            foreach (var v in new[] { -m.HoeheM / 2, 0, m.HoeheM / 2 })
            {
                var p = DesktopFlaeche.Welt(DesktopFlaeche.Punkt(s, v, m.RadiusM), mitte, gieren);
                var q = Glas.AufEbene(p, Auge, b[0])!.Value - b[0].Mitte;
                Assert.True(Math.Abs(q.Skalar(b[0].Rechts)) <= b[0].B / 2 + 1e-9 && Math.Abs(q.Skalar(b[0].Oben)) <= b[0].H / 2 + 1e-9, $"{s}/{v}");
            }
        // ein Hinweis unter der Blickmitte in Griffweite läge vor dem Bildschirm, einer weiter unten nicht (Hinweise weichen aus, SchwebeHinweise.Ausweichen)
        var hinweis = SchwebeHinweise.Allgemein(Auge, Nord);
        Assert.Contains(b, f => Glas.UeberdecktImBlick(Glas.ImBlick(hinweis, Auge, 0.2, 0.03), f, Auge));
        var frei = SchwebeHinweise.Ausweichen(hinweis, Auge, 0.2, 0.03, b);
        Assert.DoesNotContain(b, f => Glas.UeberdecktImBlick(Glas.ImBlick(frei, Auge, 0.2, 0.03), f, Auge));
        // das Symbolfeld weicht seitlich aus (wie bei einer offenen App): kein Symbol im Blick über dem Bildschirm
        var se = new SymbolEinstellungen();
        var plaetze = AppSymbole.Plaetze(["etagen", "ort", "messen", "marker", "status", "ebenen", "desktop"], Auge, Nord, se, GlasMasse.Fuer(new AppEinstellungen()));
        var seitlich = AppSymbole.Seitlich([.. plaetze.Select(p => p.Lage.Mitte)], Auge, Nord, b, AppSymbole.Durchmesser(GlasMasse.Fuer(new AppEinstellungen())) / 2, 1);
        foreach (var p in seitlich)
        {
            var l = XrTabletLogik.TabletLageAus(p, Auge - p) with { B = 0.065, H = 0.065 };
            Assert.False(Glas.UeberdecktImBlick(l, b[0], Auge), $"Symbol bei {p}");
        }
        // Teile: Griff und Knöpfe zum Betrachter, auf der Fläche
        var (griff, schliessen, _) = DesktopAnsicht.Teile(m);
        var gl = DesktopAnsicht.TeilLage(griff, mitte, gieren, DesktopAnsicht.GriffB, DesktopAnsicht.GriffH, m.RadiusM);
        Assert.True(gl.Normale.Skalar(new Vec3(0, 0, 1)) > 0.999);
        Assert.True(gl.Rechts.Skalar(new Vec3(1, 0, 0)) > 0.999);
        var kl = DesktopAnsicht.TeilLage(schliessen, mitte, gieren, 0.044, 0.044, m.RadiusM);
        Assert.True(kl.Normale.Skalar(Auge - kl.Mitte) > 0, "der Knopf schaut zum Betrachter");
        // Glasnetz: gebogen wie das Bild, uv in m um die Mitte, die Masse (halbe Breite, halbe Höhe, Ecke) in jeder Ecke
        var n = DesktopFlaeche.Glasnetz(m, 0.018, 0.035, 0.004, 0.003);
        Assert.All(n.Normalen, x => Assert.Equal(new Vec3(m.BreiteM / 2 + 0.018, m.HoeheM / 2 + 0.018, 0.035), x));
        Assert.Equal(m.BreiteM / 2 + 0.021, n.Uv.Max(x => x.U), 9);
        Assert.Equal(-(m.HoeheM / 2 + 0.021), n.Uv.Min(x => x.V), 9);
        Assert.All(n.Positionen.Zip(n.Uv), x => Assert.Equal(-0.004, DesktopFlaeche.AufFlaeche(x.First, m.RadiusM).Tiefe, 9));   // 4 mm hinter dem Bild
        Assert.Equal(0, n.Indizes.Length % 3);
    }

    // ------------------------------------------------------------------------------------------------ Format in der App

    /// <summary>Ein Bild im Format HXD1 wie der PC es sendet (eigene kleine Fassung für die Tests der App; der PC hat seine eigene Umsetzung).</summary>
    static byte[] Bild(int b, int h, int kachel, uint nr, bool voll, IEnumerable<(int Index, Func<int, int, (byte R, byte G, byte B)> Farbe)> kacheln, Action<MemoryStream>? stoeren = null)
    {
        var r = new DesktopRaster(b, h, kachel);
        using var roh = new MemoryStream();
        int n = 0;
        foreach (var (i, f) in kacheln)
        {
            n++;
            var idx = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(idx, (uint)i);
            roh.Write(idx);
            var (x, y, kb, kh) = r.KachelRechteck(i);
            for (int zy = 0; zy < kh; zy++)
            {
                byte pb = 0, pg = 0, pr = 0;
                for (int zx = 0; zx < kb; zx++)
                {
                    var (cr, cg, cb) = f(x + zx, y + zy);
                    roh.WriteByte((byte)(cb - pb)); roh.WriteByte((byte)(cg - pg)); roh.WriteByte((byte)(cr - pr));
                    (pb, pg, pr) = (cb, cg, cr);
                }
            }
        }
        stoeren?.Invoke(roh);
        using var aus = new MemoryStream();
        aus.Write(new byte[32]);
        using (var ds = new DeflateStream(aus, CompressionLevel.Fastest, true)) ds.Write(roh.ToArray());
        var d = aus.ToArray();
        "HXD1"u8.CopyTo(d);
        BinaryPrimitives.WriteUInt32LittleEndian(d.AsSpan(4), nr);
        BinaryPrimitives.WriteUInt16LittleEndian(d.AsSpan(8), (ushort)b);
        BinaryPrimitives.WriteUInt16LittleEndian(d.AsSpan(10), (ushort)h);
        BinaryPrimitives.WriteUInt16LittleEndian(d.AsSpan(12), (ushort)kachel);
        d[14] = (byte)(voll ? 1 : 0);
        d[15] = 1;
        BinaryPrimitives.WriteUInt32LittleEndian(d.AsSpan(16), (uint)n);
        BinaryPrimitives.WriteUInt32LittleEndian(d.AsSpan(20), (uint)(d.Length - 32));
        BinaryPrimitives.WriteInt64LittleEndian(d.AsSpan(24), 1234567);
        return d;
    }

    static (byte, byte, byte) Muster(int x, int y) => ((byte)(x * 7), (byte)(y * 3), (byte)(x ^ y));

    [Fact]
    public void Puffer_nimmt_Vollbild_und_Teilbild_an_und_zaehlt_nur_geaenderte_Felder()
    {
        var r = new DesktopRaster(200, 130, 64, 2);   // 4 × 3 Kacheln, Felder 128 px: 2 × 2
        var voll = Bild(200, 130, 64, 1, true, Enumerable.Range(0, r.Kacheln).Select(i => (i, (Func<int, int, (byte, byte, byte)>)Muster)));
        var k = DesktopFormat.KopfLesen(voll);
        Assert.Equal((1u, 200, 130, 64, true, 12), (k.Nr, k.Breite, k.Hoehe, k.Kachel, k.Voll, k.Anzahl));
        Assert.Equal(1234567, k.ZeitMs);
        var p = new DesktopPuffer(r);
        p.Anwenden(voll, k);
        Assert.True(p.HatVollbild);
        Assert.All(Enumerable.Range(0, 4), f => Assert.Equal(1, p.Stand(f)));
        for (int y = 0; y < 130; y += 7) for (int x = 0; x < 200; x += 11) Assert.Equal(Muster(x, y), p.Punkt(x, y));
        Assert.Equal(255, p.Felder[3][3]);   // Alpha deckend
        Assert.Equal(72 * 2 * 4, p.Felder[3].Length);   // Feld rechts unten: 72 × 2 Punkte
        // Teilbild: nur Kachel 3 (rechts oben, Feld 1) rot
        var teil = Bild(200, 130, 64, 2, false, [(3, (_, _) => ((byte)255, (byte)0, (byte)0))]);
        p.Anwenden(teil, DesktopFormat.KopfLesen(teil));
        Assert.Equal(((byte)255, (byte)0, (byte)0), p.Punkt(199, 10));
        Assert.Equal(Muster(150, 10), p.Punkt(150, 10));   // Kachel 2 unverändert
        Assert.Equal([1, 2, 1, 1], Enumerable.Range(0, 4).Select(p.Stand).ToArray());
        Assert.Equal(2u, p.LetzteNr);
    }

    [Fact]
    public void Fehlerhafte_Bilder_werden_abgewiesen()
    {
        var gut = Bild(128, 64, 64, 1, true, [(0, Muster), (1, Muster)]);
        DesktopFormat.KopfLesen(gut);
        var magie = (byte[])gut.Clone(); magie[0] = (byte)'X';
        Assert.Throws<DesktopFormatFehler>(() => DesktopFormat.KopfLesen(magie));
        Assert.Throws<DesktopFormatFehler>(() => DesktopFormat.KopfLesen(gut.AsSpan(0, gut.Length - 1)));   // Länge
        var gross = (byte[])gut.Clone(); BinaryPrimitives.WriteUInt16LittleEndian(gross.AsSpan(8), 9000);
        Assert.Throws<DesktopFormatFehler>(() => DesktopFormat.KopfLesen(gross));
        var kodierung = (byte[])gut.Clone(); kodierung[15] = 7;
        Assert.Throws<DesktopFormatFehler>(() => DesktopFormat.KopfLesen(kodierung));
        var anzahl = (byte[])gut.Clone(); BinaryPrimitives.WriteUInt32LittleEndian(anzahl.AsSpan(16), 3);
        Assert.Throws<DesktopFormatFehler>(() => DesktopFormat.KopfLesen(anzahl));   // mehr Kacheln als das Raster
        // doppelte Kachel, Index ausserhalb, zu wenig und zu viel Inhalt
        var p = new DesktopPuffer(new DesktopRaster(128, 64, 64));
        var doppelt = Bild(128, 64, 64, 2, false, [(1, Muster), (1, Muster)]);
        Assert.Throws<DesktopFormatFehler>(() => p.Anwenden(doppelt, DesktopFormat.KopfLesen(doppelt)));
        var aussen = Bild(128, 64, 64, 2, false, [(0, Muster)], s => { s.Position = 0; s.Write(BitConverter.GetBytes(5u)); s.Position = s.Length; });
        Assert.Throws<DesktopFormatFehler>(() => p.Anwenden(aussen, DesktopFormat.KopfLesen(aussen)));
        var kurz = Bild(128, 64, 64, 2, false, [(0, Muster)], s => s.SetLength(s.Length - 10));
        Assert.Throws<DesktopFormatFehler>(() => p.Anwenden(kurz, DesktopFormat.KopfLesen(kurz)));
        var lang = Bild(128, 64, 64, 2, false, [(0, Muster)], s => s.WriteByte(1));
        Assert.Throws<DesktopFormatFehler>(() => p.Anwenden(lang, DesktopFormat.KopfLesen(lang)));
        // anderes Raster
        var fremd = Bild(64, 64, 64, 3, true, [(0, Muster)]);
        Assert.False(p.Passt(DesktopFormat.KopfLesen(fremd)));
        Assert.Throws<DesktopFormatFehler>(() => p.Anwenden(fremd, DesktopFormat.KopfLesen(fremd)));
    }
}
