// Tests der Quest App V1f (Brillentest, Christian 24.10.2026): Pistolengeste statt Pinch, Faustgriff an den Punkten, Sitzungsraum ohne Tisch auf angenommener Höhe, Tafel
// fest, lockere Hand für die Wischgesten und lockerere Hebegeste – mit den Fehlauslösungen vorher (V1e) und nachher (V1f) über Zufallsbewegungen, ruhige Hände und die
// Testgesten des Gestenlabors (synthetischer Generator, GestenGenerator).

using System.Text.Json;
using Hlks.Xr.Bedienung;
using Hlks.Xr.Desktop;
using Hlks.Xr.Gesten;
using Hlks.Xr.Sitzung;
using Xunit.Abstractions;
using G = Hlks.Xr.Tests.GestenGenerator;
using static Hlks.Xr.Tests.T;

namespace Hlks.Xr.Tests;

public class V1fTests(ITestOutputHelper aus)
{
    static JsonElement W(string j) => JsonDocument.Parse(j).RootElement.Clone();

    // ================================================================================================ Pistolengeste

    /// <summary>Rechte Hand in der Pistolengeste (Zeigefinger nach vorn und etwas nach unten, Handfläche nach links, Daumen oben); abzug 0 … 1 legt den Daumen an.</summary>
    static Haltung Pist(Vec3 mitte, double abzug = 0, Vec3? finger = null, double daumen = 0) =>
        new(mitte, finger ?? G.N(0, -0.3, 1), G.V(-1, 0, 0), [daumen, 0, 0.9, 0.9, 0.9], 0, abzug);

    /// <summary>Spielt eine Aufnahme durch die Pistolengeste der rechten Hand: Anzahl Abzüge, die Stände.</summary>
    static (int Abzuege, List<PistoleStand> Staende) Pistole_(Aufnahme a, PistoleEinstellungen? e = null)
    {
        var z = new PistoleZustand();
        var l = new List<PistoleStand>();
        foreach (var b in a.Bilder) l.Add(Pistole.Schritt(z, b.T, b.Rechts.Verfolgt ? b.Rechts.GelenkeMitNamen() : null, e));
        return (l.Count(s => s.Abgedrueckt), l);
    }

    [Fact]
    public void Pistole_Haltung_Abzug_und_Strahl_aus_den_Gelenken()
    {
        var oben = G.Hand(HandSeite.Rechts, V(0, 1.3, -0.4), G.N(0, -0.3, -1), V(-1, 0, 0), [0, 0, 0.9, 0.9, 0.9], 0);
        var an = G.Hand(HandSeite.Rechts, V(0, 1.3, -0.4), G.N(0, -0.3, -1), V(-1, 0, 0), [0, 0, 0.9, 0.9, 0.9], 0, 1);
        Assert.True(Pistole.Haltung(oben));
        Assert.True(Pistole.Haltung(an));
        Assert.True(Pistole.Abzug(oben) > PistoleEinstellungen.Vorgabe.AbzugAusM, $"{Pistole.Abzug(oben)}");
        Assert.True(Pistole.Abzug(an) < PistoleEinstellungen.Vorgabe.AbzugAnM, $"{Pistole.Abzug(an)}");
        aus.WriteLine($"Daumen oben {Pistole.Abzug(oben):0.000} m, angelegt {Pistole.Abzug(an):0.000} m");
        // der Strahl vom Grundgelenk des Zeigefingers durch die Spitze
        var s = Pistole.Strahl(oben)!.Value;
        Nah(s.U.Abstand(oben["index-finger-tip"]), 0, 1e-12);
        Assert.True(s.R.Skalar(G.N(0, -0.3, -1)) > 0.99);
        // keine Pistole: offene Hand, lockere Hand, Faust, Pinch (Zeigefinger gestreckt, Daumen an der Spitze), ohne Gelenke
        foreach (var kr in new[] { G.Kr(0.1), G.Kr(0.45), G.Kr(0.95), new double[] { 0, 0, 0.5, 0.5, 0.5 } })
            Assert.False(Pistole.Haltung(G.Hand(HandSeite.Rechts, V(0, 1.3, -0.4), G.N(0, 0, -1), V(-1, 0, 0), kr, 0)), string.Join(",", kr));
        Assert.False(Pistole.Haltung(G.Hand(HandSeite.Rechts, V(0, 1.3, -0.4), G.N(0, 0, -1), V(-1, 0, 0), [0, 0, 0.9, 0.9, 0.9], 1)));
        Assert.False(Pistole.Haltung(HandHilfen.Hand("right", V(0, 1.3, -0.3), V(0, 0, -1), V(-1, 0, 0))));
        Assert.False(Pistole.Haltung(null));
        // die Geisterhand der Pistole ist eine gültige Pistole, oben und angelegt
        var vor = Geisterhand.VorlagePistole(V(0, 1.2, -0.5), V(0, -0.2, -1), V(0, 1, 0), links: false);
        Assert.True(Pistole.Haltung(vor));
        Assert.True(Pistole.Abzug(vor) > PistoleEinstellungen.Vorgabe.AbzugAusM, $"{Pistole.Abzug(vor)}");
        var ziel = new TabletPose(V(0, 1.2, -0.5), V(0, 0, 1), V(1, 0, 0), V(0, 1, 0), 0.05, 0.05);
        var gedrueckt = Geisterhand.Bild(GeisterGeste.Pistole, Geisterhand.DauerS / 2 * 0.65, vor, ziel)!.Gelenke;
        Assert.True(Pistole.Haltung(gedrueckt));
        Assert.True(Pistole.Abzug(gedrueckt) < PistoleEinstellungen.Vorgabe.AbzugAnM, $"{Pistole.Abzug(gedrueckt)}");
        Assert.True(Pistole.Abzug(Geisterhand.Bild(GeisterGeste.Pistole, Geisterhand.DauerS / 2 * 0.2, vor, ziel)!.Gelenke) > PistoleEinstellungen.Vorgabe.AbzugAusM);
    }

    [Fact]
    public void Pistole_zielen_und_abdruecken_loest_einmal_aus_mit_dem_Strahl_von_kurz_davor()
    {
        // zielen (Daumen oben, 0.4 s ruhig), Daumen in 0.08 s anlegen, 0.3 s halten, wieder hoch
        GestenBewegung abdruecken = u => (null, Pist(G.V(0.2, -0.3, 0.4), u));
        GestenBewegung hoch = u => (null, Pist(G.V(0.2, -0.3, 0.4), 1 - u));
        var (a, _) = G.Strom([(abdruecken, new Vorfuehrung { DauerS = 0.08, RuheVorS = 0.4, RuheNachS = 0.3 }), (hoch, new Vorfuehrung { DauerS = 0.1, RuheVorS = 0, RuheNachS = 0.5 })],
            uebergangS: 0.02);
        var (n, st) = Pistole_(a);
        Assert.Equal(1, n);
        var i = st.FindIndex(s => s.Abgedrueckt);
        Assert.True(st[i].Gedrueckt && st[i].Haltung);
        Assert.Contains(st.Skip(i + 1), s => s.Losgelassen);
        Assert.True(st.Take(i).Any(s => s.Zielt));   // vorher: zielt
        // der Strahl beim Abzug ist der von kurz davor (Daumen oben, ruhig): Richtung des Zeigefingers
        var r = st[i].StrahlBeimAbzug!.Value.R;
        Assert.True(r.Skalar(st[i - 10].Strahl!.Value.R) > 0.995);
        // in jeder Körperrichtung, mit beiden Varianten des Rauschens
        foreach (var gieren in new[] { -90.0, 0, 45, 160 })
        {
            var (b, _) = G.Strom([(abdruecken, new Vorfuehrung { DauerS = 0.1, RuheVorS = 0.35, RuheNachS = 0.2 }), (hoch, new Vorfuehrung { DauerS = 0.1, RuheVorS = 0, RuheNachS = 0.3 })],
                uebergangS: 0.02, gierenGrad: gieren, rauschenM: 0.007);
            Assert.Equal(1, Pistole_(b).Abzuege);
        }
        // zweimal hintereinander (mit Pause): zweimal
        var (c, _) = G.Strom([(abdruecken, new Vorfuehrung { DauerS = 0.08, RuheVorS = 0.4, RuheNachS = 0.2 }), (hoch, new Vorfuehrung { DauerS = 0.1, RuheVorS = 0, RuheNachS = 0.4 }),
            (abdruecken, new Vorfuehrung { DauerS = 0.08, RuheVorS = 0.0, RuheNachS = 0.2 }), (hoch, new Vorfuehrung { DauerS = 0.1, RuheVorS = 0, RuheNachS = 0.3 })], uebergangS: 0.02);
        Assert.Equal(2, Pistole_(c).Abzuege);
    }

    [Fact]
    public void Pistole_blosses_Zeigen_Handbewegung_und_zu_fruehes_Anlegen_loesen_nicht_aus()
    {
        // nur zielen und die Hand bewegen (Daumen oben): nie
        GestenBewegung schwenken = u => (null, Pist(G.V(0.2 + 0.25 * Math.Sin(2 * Math.PI * u), -0.3 + 0.1 * Math.Cos(2 * Math.PI * u), 0.4), 0,
            G.N(Math.Sin(3 * u), -0.3, 1)));
        foreach (var dauer in new[] { 0.4, 1.0, 2.0 })
            Assert.Equal(0, Pistole_(G.Erzeugen(schwenken, new Vorfuehrung { DauerS = dauer, RauschenM = 0.008 })).Abzuege);
        // zeigen mit angelegtem Daumen (wie man meist zeigt) und dabei bewegen: nie – der Daumen stand nie oben
        GestenBewegung zeigen = u => (null, Pist(G.V(0.2 + 0.2 * u, -0.3, 0.4), 1, G.N(Math.Sin(2 * u), -0.3, 1)));
        Assert.Equal(0, Pistole_(G.Erzeugen(zeigen, new Vorfuehrung { DauerS = 1.0, RauschenM = 0.008 })).Abzuege);
        // die Pistole erst formen, der Daumen geht nur kurz hoch (0.05 s) und wieder an: zu früh, nichts
        GestenBewegung kurz = u => (null, Pist(G.V(0.2, -0.3, 0.4), u < 0.5 ? 1 - u * 2 : (u - 0.5) * 2));
        Assert.Equal(0, Pistole_(G.Erzeugen(kurz, new Vorfuehrung { DauerS = 0.12, RuheVorS = 0.0, RuheNachS = 0.4 })).Abzuege);
        // zittert der Daumen nach dem Abzug zwischen den Schwellen (Hysterese): ein Abzug, kein Flackern, kein Lösen
        var z = new PistoleZustand();
        int n = 0, los = 0;
        var band = new List<double>();
        for (int i = 0; i < 200; i++)
        {
            var t = i / 72.0;
            var abzug = t < 0.4 ? 0 : t < 0.6 ? 1 : 0.71 + 0.05 * Math.Sin(i * 1.7);   // nach dem Abzug zitternd zwischen an und aus
            var h = G.Hand(HandSeite.Rechts, V(0, 1.3, -0.4), G.N(0, -0.3, -1), V(-1, 0, 0), [0, 0, 0.9, 0.9, 0.9], 0, abzug);
            var s = Pistole.Schritt(z, t, h);
            if (s.Abgedrueckt) n++;
            if (s.Losgelassen) los++;
            if (t >= 0.6) band.Add(Pistole.Abzug(h)!.Value);
        }
        Assert.Equal(1, n);
        Assert.Equal(0, los);
        Assert.True(band.Min() > PistoleEinstellungen.Vorgabe.AbzugAnM && band.Max() < PistoleEinstellungen.Vorgabe.AbzugAusM, $"{band.Min():0.000} … {band.Max():0.000}");
        // ein Schwenk um mehr als 20° während der Ruhezeit: kein Abzug (das Ziel war nicht ruhig)
        GestenBewegung schwenkDann = u => (null, Pist(G.V(0.2, -0.3, 0.4), u < 0.7 ? 0 : (u - 0.7) / 0.3, G.N(Math.Sin(1.2 * Math.Min(u, 0.7) / 0.7) * 1.2, -0.3, 1)));
        Assert.Equal(0, Pistole_(G.Erzeugen(schwenkDann, new Vorfuehrung { DauerS = 0.3, RuheVorS = 0.3, RuheNachS = 0.3 })).Abzuege);
    }

    [Fact]
    public void Pistole_Fehlausloesungen_ueber_Zufallsbewegungen_Ruhe_und_Testgesten_selten()
    {
        int n = 0, abzuege = 0;
        var wo = new List<string>();
        for (int s = 0; s < 900; s++)
        {
            var a = G.Erzeugen(G.Zufall(4000 + s, mitLinks: s % 2 == 0), new Vorfuehrung { DauerS = 1.0 + (s % 5) * 0.25, Saat = 5000 + s, GierenGrad = (s % 7) * 50 });
            var k = Pistole_(a).Abzuege;
            n++;
            abzuege += k;
            if (k > 0) wo.Add($"Zufall {s}: {k}");
        }
        foreach (var t in new[] { G.TKreis, G.TStoss, G.TFaust, G.TWischHochPinch, G.TAuseinander, G.TWischRunter, G.THeranziehen, G.THebe, G.TWischLinks })
            foreach (var gesp in new[] { false, true })
                foreach (var (text, v) in G.Abwandlungen(t, gesp).Take(6))
                {
                    var k = Pistole_(G.Erzeugen(t.Bewegung, v)).Abzuege;
                    n++;
                    abzuege += k;
                    if (k > 0) wo.Add($"{t.Name} {text}: {k}");
                }
        for (int s = 0; s < 10; s++) abzuege += Pistole_(G.Erzeugen(G.Ruhe, new Vorfuehrung { DauerS = 3, Saat = 900 + s, RauschenM = 0.008 })).Abzuege;
        aus.WriteLine($"Pistolengeste: {abzuege} Abzüge in {n + 10} Bewegungen ohne Pistole ({string.Join("; ", wo.Take(10))})");
        Assert.True(abzuege <= 5, $"{abzuege}: {string.Join("; ", wo)}");   // höchstens jede 200. Zufallsbewegung (sie enthalten zufällig Pistolen mit springendem Daumen)
    }

    [Fact]
    public void Pistole_springt_beim_Begehen_der_Pinch_zielt_nicht_mehr_Pinch_Wisch_dreht_weiter()
    {
        // ruhig auf den Boden zielen, abdrücken: ein Sprung auf das Ziel; vorher Ziel-Ereignisse und bereit
        GestenBewegung abdruecken = u => (null, Pist(G.V(0.2, -0.3, 0.4), u, G.N(0, -0.7, 1)));
        GestenBewegung hoch = u => (null, Pist(G.V(0.2, -0.3, 0.4), 1 - u, G.N(0, -0.7, 1)));
        var (a, _) = G.Strom([(abdruecken, new Vorfuehrung { DauerS = 0.08, RuheVorS = 0.5, RuheNachS = 0.3 }), (hoch, new Vorfuehrung { DauerS = 0.1, RuheVorS = 0, RuheNachS = 0.3 })], uebergangS: 0.02);
        var ev = Begehen(a);
        var sprung = ev.Where(e => e.Art == BegehenArt.Teleport).ToList();
        Assert.Single(sprung);
        Assert.DoesNotContain(ev, e => e.Art == BegehenArt.Abbruch);
        Assert.Contains(ev, e => e.Art == BegehenArt.Ziel && e.Punkt is not null);
        Assert.Equal(0, sprung[0].Punkt!.Value.Y, 6);   // auf dem Boden (Rig y = 0)
        // auf den Himmel gezielt: Abbruch, kein Sprung
        GestenBewegung himmel = u => (null, Pist(G.V(0.2, -0.3, 0.4), u, G.N(0, 0.6, 1)));
        var (b, _) = G.Strom([(himmel, new Vorfuehrung { DauerS = 0.08, RuheVorS = 0.5, RuheNachS = 0.3 })], uebergangS: 0.02);
        var evb = Begehen(b);
        Assert.DoesNotContain(evb, e => e.Art == BegehenArt.Teleport);
        Assert.Contains(evb, e => e.Art == BegehenArt.Abbruch);
        // Pinch halten und loslassen (wie bis V1e springen): kein Sprung, kein Ziel
        GestenBewegung pinch = u => (null, new Haltung(G.V(0.2, -0.3, 0.4), G.N(0, -0.7, 1), G.V(-1, 0, 0), G.Kr(0.35), u < 0.8 ? 1 : 0));
        var evp = Begehen(G.Erzeugen(pinch, new Vorfuehrung { DauerS = 1.0, RuheVorS = 0.2, RuheNachS = 0.3 }));
        Assert.DoesNotContain(evp, e => e.Art is BegehenArt.Teleport or BegehenArt.Ziel or BegehenArt.Abbruch);
        Assert.Contains(evp, e => e.Art == BegehenArt.PinchAn);
        // Pinch + Wisch nach rechts: dreht weiter
        GestenBewegung wisch = u => (null, new Haltung(G.V(-0.1 + 0.35 * u, -0.25, 0.4), G.N(0, 0.3, 1), G.V(-1, 0, 0), [0.3, 0.35, 0.7, 0.8, 0.8], 1));
        var evw = Begehen(G.Erzeugen(wisch, new Vorfuehrung { DauerS = 0.25, RuheVorS = 0.2, RuheNachS = 0.3 }));
        Assert.Contains(evw, e => e.Art == BegehenArt.Drehen);
        // mit springen = false (Hand an den Apps, Messen, Marker) zielt und springt die Pistole beim Begehen nicht
        Assert.DoesNotContain(Begehen(a, springen: false), e => e.Art is BegehenArt.Teleport or BegehenArt.Ziel);
        // Variante «pinch»: wie bis V1e (die App gibt dann keinen Stand der Pistole) – der Pinch springt
        GestenBewegung pinchBoden = u => (null, new Haltung(G.V(0.2, -0.3, 0.4), G.N(0, -0.7, 1), G.V(-1, 0, 0), G.Kr(0.35), u < 0.85 ? 1 : 0));
        var evv1e = Begehen(G.Erzeugen(pinchBoden, new Vorfuehrung { DauerS = 1.2, RuheVorS = 0.2, RuheNachS = 0.3 }), mitPistole: false);
        Assert.Contains(evv1e, e => e.Art == BegehenArt.Teleport);
    }

    /// <summary>Die rechte Hand einer Aufnahme durch HandBegehen (Szene = Raum der Aufnahme, Boden y = 0), mit der Pistolengeste wie in der App.</summary>
    static List<BegehenEreignis> Begehen(Aufnahme a, bool springen = true, bool mitPistole = true)
    {
        var z = new HandBegehenZustand();
        var pz = new PistoleZustand();
        var l = new List<BegehenEreignis>();
        foreach (var b in a.Bilder)
        {
            var g = b.Rechts.Verfolgt ? b.Rechts.GelenkeMitNamen() : null;
            var p = Pistole.Schritt(pz, b.T, g);
            l.AddRange(HandBegehen.Schritt(z, b.T, "right", g, G.KopfLage, b.Gieren, 0, null, null, true, springen, mitPistole ? p : null));
        }
        return l;
    }

    [Fact]
    public void Pistole_waehlt_auf_Distanz_den_Knopf_unter_dem_Strahl_von_kurz_vor_dem_Abzug()
    {
        var ziele = new List<TabletPose>
        {
            new(V(-0.1, 1.4, -1), V(0, 0, 1), V(1, 0, 0), V(0, 1, 0), 0.08, 0.08),
            new(V(0.1, 1.4, -1), V(0, 0, 1), V(1, 0, 0), V(0, 1, 0), 0.08, 0.08),
        };
        var z = new GlasStrahlHand();
        (Vec3, Vec3) Auf(double x) => (V(0, 1.4, 0), (V(x, 1.4, -1) - V(0, 1.4, 0)).Einheit());
        Assert.Null(GlasStrahl.Abzug(z, ziele, Auf(-0.1), false, null));
        Assert.Equal(0, z.Hover);
        // beim Abzug zeigt der Strahl schon auf den rechten Knopf (Zucken), der Strahl von kurz davor auf den linken: der linke
        Assert.Equal(0, GlasStrahl.Abzug(z, ziele, Auf(0.1), true, Auf(-0.1)));
        Assert.Equal(1, z.Hover);
        Assert.Null(GlasStrahl.Abzug(z, ziele, Auf(0.5), true, Auf(0.5)));   // daneben
    }

    [Fact]
    public void Variante_ausloeser_und_Stellschrauben_der_Pistole()
    {
        var e = XrBedienEinstellungen.Vorgabe;
        Assert.Equal(Varianten.AusloeserPistole, e.Variante("ausloeser"));
        Assert.True(e.MitPistole);
        Assert.Equal(new[] { Varianten.AusloeserPistole, Varianten.AusloeserPinch }, Varianten.Bekannt["ausloeser"].ToArray());
        var p = e.MitStellschraube("labor.variante.ausloeser", W("\"pinch\""))!;
        Assert.False(p.MitPistole);
        Assert.Null(e.MitStellschraube("labor.variante.ausloeser", W("\"faust\"")));
        Assert.True(p.MitStellschraube("labor.variante.ausloeser", W("null"))!.MitPistole);
        Assert.Equal((0.025, 0.04, 0.15), (e.Pistole.AbzugAnM, e.Pistole.AbzugAusM, e.Pistole.RuheS));
        Assert.Equal(0.03, e.MitStellschraube("pistole.abzug_an_m", W("0.03"))!.Pistole.AbzugAnM);
        Assert.Null(e.MitStellschraube("pistole.abzug_an_m", W("0.045")));   // an muss unter aus liegen
        Assert.Equal(0.06, e.MitStellschraube("pistole.abzug_aus_m", W("0.06"))!.Pistole.AbzugAusM);
        Assert.Equal(0.3, e.MitStellschraube("pistole.ruhe_s", W("0.3"))!.Pistole.RuheS);
        Assert.Null(e.MitStellschraube("pistole.ruhe_s", W("2")));
        foreach (var k in new[] { "labor.variante.ausloeser", "pistole.abzug_an_m", "pistole.abzug_aus_m", "pistole.ruhe_s", "sitzung.tisch_hoehe_m", "gesten.offen_ab", "hebe.vorhalte_s", "hebe.dreh_grad" })
            Assert.True(XrBedienEinstellungen.Kennt(k), k);
        // eine längere Ruhezeit verlangt länger gezielt
        GestenBewegung abdruecken = u => (null, Pist(G.V(0.2, -0.3, 0.4), u));
        var a = G.Erzeugen(abdruecken, new Vorfuehrung { DauerS = 0.08, RuheVorS = 0.25, RuheNachS = 0.3 });
        Assert.Equal(1, Pistole_(a).Abzuege);
        Assert.Equal(0, Pistole_(a, e.Pistole with { RuheS = 0.5 }).Abzuege);
        // die Gestentabelle nennt die Pistole
        var text = string.Join(" ", Gestenbedeutung.Tabelle.Select(x => x.Geste + " " + x.Bedeutung));
        foreach (var s in new[] { "Pistolengeste", "abdrücken", "Faust", "locker" }) Assert.Contains(s, text);
    }

    // ================================================================================================ Faustgriff an den Punkten

    [Fact]
    public void Faust_fasst_die_Punkte_am_Tisch_die_Pistole_und_die_offene_Hand_nicht()
    {
        var faust = G.Hand(HandSeite.Rechts, V(0.1, 0.9, -0.6), G.N(0, 0, -1), V(0, -1, 0), G.Kr(0.95), 0);
        var offen = G.Hand(HandSeite.Rechts, V(0.1, 0.9, -0.6), G.N(0, 0, -1), V(0, -1, 0), G.Kr(0.1), 0);
        var pistole = G.Hand(HandSeite.Rechts, V(0.1, 0.9, -0.6), G.N(0, 0, -1), V(-1, 0, 0), [0, 0, 0.9, 0.9, 0.9], 0);
        Assert.True(ZweiPunkte.Faust(faust, false));
        Assert.False(ZweiPunkte.Faust(offen, false));
        Assert.False(ZweiPunkte.Faust(pistole, false));   // zielt, greift nicht
        Assert.True(ZweiPunkte.Faust(G.Hand(HandSeite.Rechts, V(0.1, 0.9, -0.6), G.N(0, 0, -1), V(0, -1, 0), G.Kr(0.62), 0), true));   // gehalten: Hysterese
        Assert.NotNull(ZweiPunkte.FaustPunkt(faust));
        // Faust 7 cm neben dem Punkt fasst (Pinch: 5 cm), 9 cm nicht
        var bezug = new SitzungBezug(new Xz(0, 0), 0, new ModellGroesse(20, 10, 6));
        var lage = new SitzungLage(50, 0, 0, 0);
        var pk = ZweiPunkte.Punkte(lage, bezug, null);
        var ef = ZweiPunkteEinstellungen.Vorgabe with { Faust = true };
        var z = ZweiPunkte.Neu(lage);
        ZweiPunkte.Schritt(z, null, null, pk, ef);
        Assert.Equal(ZweiPunkteEreignis.Gefasst, ZweiPunkte.Schritt(z, null, new EinGriffHand(true, pk.Verschieben + V(0.07, 0, 0), 0, 0), pk, ef));
        var z2 = ZweiPunkte.Neu(lage);
        ZweiPunkte.Schritt(z2, null, null, pk, ef);
        Assert.Equal(ZweiPunkteEreignis.Nichts, ZweiPunkte.Schritt(z2, null, new EinGriffHand(true, pk.Verschieben + V(0.09, 0, 0), 0, 0), pk, ef));
        var z3 = ZweiPunkte.Neu(lage);
        ZweiPunkte.Schritt(z3, null, null, pk);
        Assert.Equal(ZweiPunkteEreignis.Nichts, ZweiPunkte.Schritt(z3, null, new EinGriffHand(true, pk.Verschieben + V(0.07, 0, 0), 0, 0), pk));   // Pinch: 5 cm
        // die Geisterhand «Faust» schliesst die eigene Hand zur Faust (die Regel erkennt sie) mit dem Punkt innen
        var ziel = XrTabletLogik.TabletLageAus(pk.Verschieben, V(0, 1, 0));
        var form = G.Hand(HandSeite.Rechts, pk.Verschieben + V(0, 0.08, 0.05), G.N(0, -0.2, -1), G.N(0, -1, 0.2), G.Kr(0.15), 0);
        var zu = Geisterhand.Bild(GeisterGeste.Faust, Geisterhand.DauerS / 2 * 0.45, form, ziel)!.Gelenke;
        Assert.True(XrTabletLogik.IstFaust(zu), $"{XrTabletLogik.FaustVerhaeltnis(zu)}");
        Assert.True(ZweiPunkte.FaustPunkt(zu)!.Value.Abstand(pk.Verschieben) < ZweiPunkte.FaustFangM, $"{ZweiPunkte.FaustPunkt(zu)!.Value.Abstand(pk.Verschieben)}");
        Assert.False(XrTabletLogik.IstFaust(Geisterhand.Bild(GeisterGeste.Faust, 0.01, form, ziel)!.Gelenke));   // erst offen
        Assert.True(XrTabletLogik.IstFaust(Geisterhand.Bild(GeisterGeste.FaustKreisen, Geisterhand.DauerS / 2 * 0.7, form, ziel)!.Gelenke));
    }

    // ================================================================================================ Sitzungsraum ohne Tisch, Tafel fest

    [Fact]
    public void Ohne_echten_Tisch_steht_das_Modell_auf_der_angenommenen_Hoehe_Hintergrund_durchsichtig_bzw_hell()
    {
        var kopf = V(1, 1.2, -2);
        var r = SitzungRaum.VorKopf(kopf, Math.PI / 2);
        Nah(r.ZuRef(XrSitzungLogik.TischMitte).Y, 0.75, 1e-12);   // Vorgabe wie bisher der virtuelle Tisch
        var e = XrBedienEinstellungen.Vorgabe;
        Assert.Equal(0.75, e.TischHoeheM);
        var tief = e.MitStellschraube("sitzung.tisch_hoehe_m", W("0.68"))!;
        var r2 = SitzungRaum.VorKopf(kopf, Math.PI / 2, tief.TischHoeheM);
        Nah(r2.ZuRef(XrSitzungLogik.TischMitte).Y, 0.68, 1e-12);
        // die Platte liegt vor dem Kopf wie bisher (waagrecht unverändert)
        Nah(r2.ZuRef(XrSitzungLogik.TischMitte).X, r.ZuRef(XrSitzungLogik.TischMitte).X, 1e-12);
        Nah(r2.ZuRef(XrSitzungLogik.TischMitte).Z, r.ZuRef(XrSitzungLogik.TischMitte).Z, 1e-12);
        Assert.Null(e.MitStellschraube("sitzung.tisch_hoehe_m", W("2")));
        // Hintergrund: Passthrough → durchsichtig; ohne Passthrough (Simulator) der helle Ersatzhintergrund – auch mit «umgebung» = «tron» (gilt am Tisch nicht mehr)
        var h = new HintergrundVerwalter();
        Assert.Equal(Hintergrund.Durchsichtig, h.Setzen(new Hintergrund(0.1f, 0.1f, 0.1f, 1, true), true, true, true));
        Assert.Equal(Hintergrund.Hell, h.Setzen(Hintergrund.Durchsichtig, true, false, true));
    }

    [Fact]
    public void Tafel_oeffnet_mittig_und_bleibt_stehen_traege_als_Variante()
    {
        Assert.Equal(Varianten.TafelFest, TafelEinstellungen.Vorgabe.Nachfuehren);
        Assert.Equal(Varianten.TafelFest, XrBedienEinstellungen.Vorgabe.Variante("tafel_nachfuehren"));
        Assert.Equal(Varianten.TafelFest, Varianten.Bekannt["tafel_nachfuehren"][0]);
        var auge = V(0, 1.6, 0);
        var platz = Tafel.Platz(auge, Math.PI / 2, new AppEinstellungen(), TafelEinstellungen.Vorgabe);
        Nah(Tafel.Abweichung(platz, auge, Math.PI / 2), 0, 1e-12);   // mittig in der Blickrichtung
        // man dreht sich 60° weg: sie bleibt stehen
        var f = new TafelFolgen();
        var m = platz;
        for (int i = 0; i < 200; i++) m = Tafel.Nachfuehren(f, m, auge, Math.PI / 2 + 60 * Grad, 1 / 72.0, platz, TafelEinstellungen.Vorgabe, false, false);
        Nah(m.Abstand(platz), 0, 1e-12);
        Assert.Equal(0, f.Nachgerueckt);
        // «traege» rückt nach (Vergleich)
        var t = TafelEinstellungen.Vorgabe with { Nachfuehren = Varianten.TafelTraege };
        var f2 = new TafelFolgen();
        var m2 = platz;
        for (int i = 0; i < 200; i++) m2 = Tafel.Nachfuehren(f2, m2, auge, Math.PI / 2 + 60 * Grad, 1 / 72.0, platz, t, false, false);
        Assert.True(m2.Abstand(platz) > 0.2);
        Assert.Equal(Varianten.TafelTraege, XrBedienEinstellungen.Vorgabe.MitStellschraube("labor.variante.tafel_nachfuehren", W("\"traege\""))!.Variante("tafel_nachfuehren"));
    }

    // ================================================================================================ Lockere Hand, Hebegeste

    /// <summary>Ein Satz für den Vergleich: V1e (deutlich offene Hand, strenge Hebegeste) bzw. V1f (Vorgabe).</summary>
    internal sealed record Satz(string Name, AnstossEinstellungen Anstoss, HebeEinstellungen Hebe, WegwischEinstellungen? Wegwischen = null)
    {
        public static readonly Satz V1e = new("V1e", AnstossEinstellungen.V1e, HebeEinstellungen.V1e);
        public static readonly Satz V1f = new("V1f", AnstossEinstellungen.Vorgabe, HebeEinstellungen.Vorgabe);
    }

    /// <summary>Alle drei Gesten über eine Aufnahme, verdrahtet wie in der App (wie V1eTests.Abspielen), mit dem Satz s; dazu die verworfenen Ansätze der Hebegeste.</summary>
    internal static (int Hebe, int Weg, int Hinein, int Anstoss, List<string> Verworfen) Abspielen(Aufnahme a, Satz s)
    {
        var w = (s.Wegwischen ?? WegwischEinstellungen.Vorgabe) with { Anstoss = s.Anstoss };
        var he = s.Hebe;
        var g = new DesktopGesten(() => DesktopEinstellungen.Vorgabe, null, () => w);
        var hz = new HebeZustand();
        var wz = new WegwischZustand();
        int hebe = 0, weg = 0, hinein = 0, durch = 0;
        var verworfen = new List<string>();
        foreach (var b in a.Bilder)
        {
            var r = b.Rechts.Verfolgt ? b.Rechts.GelenkeMitNamen() : null;
            var l = b.Links.Verfolgt ? b.Links.GelenkeMitNamen() : null;
            var hr = HandBeobachtung.Aus(r, "right");
            var hl = HandBeobachtung.Aus(l, "left");
            var hs = HebeGeste.Schritt(hz, b.T, hl, hr, b.Gieren, he);
            if (hs.Ereignis is not null) { hebe++; if (hs.DurchAnstoss) durch++; }
            if (hs.Verworfen is { } v) verworfen.Add(v);
            var ruht = g.WegwischenRuht(b.T);
            // V1h: mit Kopf und Körperrichtung (Randregel, Körpermitte)
            var ws = Wegwischen.Schritt(wz, b.T, ruht ? null : hr, ruht ? null : hl, b.Gieren, w, b.Kopf, b.Gieren);
            if (ws.Ereignis) { weg++; g.NachWegwischen(b.T); if (ws.DurchAnstoss) durch++; }
            var st = g.Schritt(new DesktopHaende(b.T, r, l, b.Gieren, b.Kopf, b.Gieren), true);
            if (st.Ausgeloest) { hinein++; if (st.DurchAnstoss) durch++; }
        }
        return (hebe, weg, hinein, durch, verworfen);
    }

    /// <summary>Fehlauslösungen wie V1eTests.Fehlauslösungen (Zufall, ruhige Hände, Testgesten des Labors), mit dem Satz s.</summary>
    internal static (int Hebe, int Weg, int Hinein, int Bewegungen) Fehlausloesungen(Satz s, int zufall, List<string>? wo = null, int saat = 4000)
    {
        int hebe = 0, weg = 0, hinein = 0, n = 0;
        void Zaehlen(Aufnahme a, string name)
        {
            var (h, w, i, _, _) = Abspielen(a, s);
            hebe += h; weg += w; hinein += i; n++;
            if (h + w + i > 0) wo?.Add($"{name}: H{h} W{w} I{i}");
        }
        for (int k = 0; k < zufall; k++)
            Zaehlen(G.Erzeugen(G.Zufall(saat + k, mitLinks: k % 2 == 0), new Vorfuehrung { DauerS = 1.0 + (k % 5) * 0.25, Saat = saat + 1000 + k, GierenGrad = (k % 7) * 50 }), $"Zufall {k}");
        for (int k = 0; k < 10; k++) Zaehlen(G.Erzeugen(G.Ruhe, new Vorfuehrung { DauerS = 3, Saat = 900 + k, RauschenM = 0.004 + 0.002 * (k % 3) }), $"Ruhe {k}");
        foreach (var t in new[] { G.TKreis, G.TStoss, G.TFaust, G.TWischHochPinch, G.TAuseinander, G.TWischRunter, G.THeranziehen })
            foreach (var gesp in new[] { false, true })
                foreach (var (text, v) in G.Abwandlungen(t, gesp).Take(6))
                    Zaehlen(G.Erzeugen(t.Bewegung, v), $"{t.Name} {text}{(gesp ? " gespiegelt" : "")}");
        return (hebe, weg, hinein, n);
    }

    [Fact]
    public void Lockere_Hand_und_Hebegeste_Fehlausloesungen_nehmen_nicht_zu()
    {
        var woV = new List<string>();
        var woN = new List<string>();
        var vorher = Fehlausloesungen(Satz.V1e, 900, woV);
        var nachher = Fehlausloesungen(Satz.V1f, 900, woN);
        aus.WriteLine($"Fehlauslösungen vorher (V1e: deutlich offene Hand, Hebegeste 45°/25°/0.1 s/0.35 s): Hebegeste {vorher.Hebe}, Wegwischen {vorher.Weg}, Hineinziehen {vorher.Hinein} in {vorher.Bewegungen} Bewegungen ({string.Join("; ", woV)})");
        aus.WriteLine($"Fehlauslösungen nachher (V1f: lockere Hand ab {AnstossEinstellungen.Vorgabe.OffenAb:0.00}, Hebegeste {HebeEinstellungen.Vorgabe.HandflaecheGrad}°/{HebeEinstellungen.Vorgabe.DrehGrad}°/{HebeEinstellungen.Vorgabe.VorhalteS} s/{HebeEinstellungen.Vorgabe.GleichzeitigS} s): Hebegeste {nachher.Hebe}, Wegwischen {nachher.Weg}, Hineinziehen {nachher.Hinein} ({string.Join("; ", woN)})");
        Assert.True(nachher.Hebe <= vorher.Hebe, "Hebegeste");
        Assert.True(nachher.Weg <= vorher.Weg, "Wegwischen");
        Assert.True(nachher.Hinein <= vorher.Hinein, "Hineinziehen");
    }

    /// <summary>Kurze Anstösse aller drei Gesten mit lockerer, halb gekrümmter Hand (Krümmung kr), wie V1eTests.Anstoesse.</summary>
    static IEnumerable<(string Art, Aufnahme A)> LockereAnstoesse(double kr)
    {
        var faelle = new (double Weg, double Dauer)[] { (0.07, 0.12), (0.08, 0.15), (0.10, 0.2), (0.12, 0.2), (0.09, 0.25) };
        var vornHoch = G.N(0, 0.3, 1);
        int saat = 31;
        foreach (var (weg, dauer) in faelle)
            foreach (var gieren in new[] { 0.0, 120 })
            {
                var v = new Vorfuehrung { DauerS = dauer, RuheVorS = 0.3, RuheNachS = 0.5, GierenGrad = gieren, BogenM = 0.01, Saat = saat++ };
                yield return ("weg", G.Erzeugen(u => (null, new Haltung(G.V(0.15 - weg * u, -0.25, 0.4), vornHoch, G.V(-1, 0, 0), G.Kr(kr))), v));
                yield return ("weg", G.Erzeugen(u => (new Haltung(G.V(-0.05 - weg * u, -0.25, 0.4), vornHoch, G.V(1, 0, 0), G.Kr(kr)), null), v));
                yield return ("hinein", G.Erzeugen(u => (null, new Haltung(G.V(-0.05 + weg * u, -0.25, 0.4), vornHoch, G.V(-1, 0, 0), G.Kr(kr))), v));
                yield return ("hinein", G.Erzeugen(u => (new Haltung(G.V(-0.25 + weg * u, -0.25, 0.4), vornHoch, G.V(1, 0, 0), G.Kr(kr)), null), v));
                yield return ("hebe", G.Erzeugen(u => (new Haltung(G.V(-0.2, -0.5 + weg * u, 0.35), G.V(0, 0, 1), G.V(0, 1, 0), G.Kr(kr)),
                    new Haltung(G.V(0.2, -0.5 + weg * u, 0.35), G.V(0, 0, 1), G.V(0, 1, 0), G.Kr(kr))), v));
            }
    }

    static int Treffer(IEnumerable<(string Art, Aufnahme A)> faelle, Satz s, out int n)
    {
        int ok = 0;
        n = 0;
        foreach (var (art, a) in faelle)
        {
            n++;
            var r = Abspielen(a, s);
            if ((art == "weg" ? r.Weg : art == "hinein" ? r.Hinein : r.Hebe) == 1 && r.Hebe + r.Weg + r.Hinein == 1) ok++;
        }
        return ok;
    }

    [Fact]
    public void Wischgesten_mit_lockerer_halb_gekruemmter_Hand_loesen_aus_Faust_Pistole_Pinch_nicht()
    {
        foreach (var kr in new[] { 0.4, 0.45, 0.5 })
        {
            var hand = G.Hand(HandSeite.Rechts, V(0, 1.3, -0.4), G.N(0, 0, -1), V(-1, 0, 0), G.Kr(kr), 0);
            var neu = Treffer(LockereAnstoesse(kr), Satz.V1f, out var n);
            var alt = Treffer(LockereAnstoesse(kr), Satz.V1e, out _);
            aus.WriteLine($"lockere Hand (Krümmung {kr}, FaustVerhältnis {XrTabletLogik.FaustVerhaeltnis(hand):0.00}): V1f {neu}/{n} erkannt, V1e {alt}/{n}");
            Assert.True(neu >= n * 9 / 10, $"{kr}: {neu}/{n}");
            Assert.True(alt < n / 2, $"{kr}: {alt}/{n}");
        }
        // Faust, Pistole, Pinch: nichts (gleicher kurzer Wisch nach links bzw. Hebe)
        var v0 = new Vorfuehrung { DauerS = 0.15, RuheVorS = 0.3, RuheNachS = 0.5 };
        var vornHoch = G.N(0, 0.3, 1);
        foreach (var (kr, pinch) in new (double[] Kr, double Pinch)[] { (G.Kr(0.9), 0), (new double[] { 0, 0, 0.9, 0.9, 0.9 }, 0), (new double[] { 0.3, 0.35, 0.7, 0.8, 0.8 }, 1), (G.Kr(0.65), 0) })
        {
            var r = Abspielen(G.Erzeugen(u => (null, new Haltung(G.V(0.15 - 0.09 * u, -0.25, 0.4), vornHoch, G.V(-1, 0, 0), kr, pinch)), v0), Satz.V1f);
            Assert.Equal(0, r.Hebe + r.Weg + r.Hinein);
            var h = Abspielen(G.Erzeugen(u => (new Haltung(G.V(-0.2, -0.5 + 0.09 * u, 0.35), G.V(0, 0, 1), G.V(0, 1, 0), kr, pinch),
                new Haltung(G.V(0.2, -0.5 + 0.09 * u, 0.35), G.V(0, 0, 1), G.V(0, 1, 0), kr, pinch)), v0), Satz.V1f);
            Assert.Equal(0, h.Hebe + h.Weg + h.Hinein);
        }
        // Stellschraube: strenger (1.75 wie V1e) – die lockere Hand löst nicht mehr aus
        var e = XrBedienEinstellungen.Vorgabe.MitStellschraube("gesten.offen_ab", W("1.75"))!;
        Assert.Equal((1.75, 1.75), (e.Anstoss.OffenAb, e.Anstoss.HaltungOffenAb));
        Assert.Equal(1.75, e.Hebe.Anstoss.OffenAb);
        Assert.Equal(1.75, DesktopEinstellungen.Vorgabe.HineinMit(e.Wegwischen).Anstoss.HaltungOffenAb);
        var streng = new Satz("streng", e.Anstoss, e.Hebe);
        Assert.True(Treffer(LockereAnstoesse(0.45), streng, out var ns) < ns / 2);
        Assert.Null(XrBedienEinstellungen.Vorgabe.MitStellschraube("gesten.offen_ab", W("1.0")));
        // auf der Tafel blättert auch die lockere Hand (Blättern verlangt keine offene Hand)
        var tafel = new TabletPose(V(0, 1.45, -0.45), V(0, 0, 1), V(1, 0, 0), V(0, 1, 0), 0.32, 0.31);
        var zb = new TafelBlaetternZustand();
        int bl = 0;
        for (int i = 0; i <= 90; i++)
        {
            var t = i / 72.0;
            var x = 0.1 - 0.16 * GlasStil.Weich(Math.Clamp((t - 0.3) / 0.3, 0, 1));
            var g = G.Hand(HandSeite.Rechts, V(x, 1.42, -0.47), G.N(0, 0.2, -1), V(0, 0, 1), G.Kr(0.45), 0);
            if (TafelBlaettern.Schritt(zb, t, g, XrTabletLogik.IstPinch(g), tafel).Ereignis is not null) bl++;
        }
        Assert.Equal(1, bl);
    }

    /// <summary>
    /// Natürliches Anheben (Befund Christian 24.10.2026: «die app hineinheben geste scheint im letzten test weniger gut zu klappen»): die Hände kommen aus der Ruhe (Handflächen
    /// zueinander), drehen sich beim Steigen nach oben (bis drehEnde neben oben), sind locker (Krümmung kr), die zweite Hand beginnt versatz s später, ohne Halten davor.
    /// </summary>
    static Aufnahme NatuerlichHeben(double weg, double dauer, double drehStart, double drehEnde, double kr, double versatz, double gieren, int saat)
    {
        Haltung Hand(double seite, double u)
        {
            var w = (drehStart + (drehEnde - drehStart) * u) * Grad;   // Winkel der Handfläche neben oben, zur Körpermitte gekippt
            var n = G.N(-seite * Math.Sin(w), Math.Cos(w), 0.1);
            return new Haltung(G.V(0.2 * seite, -0.5 + weg * u, 0.35), G.V(0, 0, 1), n, G.Kr(kr));
        }
        double U(double t, double ab) => G.MinimumJerk((t - 0.4 - ab) / dauer);
        return G.Rendern(t => (Hand(-1, U(t, 0)), Hand(1, U(t, versatz))), 0.4 + dauer + versatz + 0.5, gieren, 0.005, saat);
    }

    [Fact]
    public void Hebegeste_natuerlich_angehoben_locker_drehend_versetzt_wird_erkannt()
    {
        int ok = 0, okV1e = 0, n = 0, saat = 1;
        foreach (var (weg, dauer) in new[] { (0.10, 0.2), (0.14, 0.3), (0.2, 0.4) })
            foreach (var (d0, d1) in new[] { (75.0, 15.0), (55.0, 10.0), (20.0, 45.0) })
                foreach (var kr in new[] { 0.2, 0.45 })
                    foreach (var versatz in new[] { 0.0, 0.2, 0.35 })
                    {
                        n++;
                        var a = NatuerlichHeben(weg, dauer, d0, d1, kr, versatz, saat * 37 % 180, saat++);
                        var rr = Abspielen(a, Satz.V1f);
                        if (rr is { Hebe: 1, Weg: 0, Hinein: 0 }) ok++;
                        else aus.WriteLine($"nicht: Weg {weg} in {dauer} s, Handfläche {d0}° → {d1}°, Krümmung {kr}, versetzt {versatz} s: H{rr.Hebe} W{rr.Weg} I{rr.Hinein} {string.Join(" | ", rr.Verworfen)}");
                        if (Abspielen(a, Satz.V1e) is { Hebe: 1 }) okV1e++;
                    }
        aus.WriteLine($"natürliches Anheben (Hände drehen mit, locker, bis 0.35 s versetzt, ohne Halten davor): V1f {ok}/{n} erkannt, V1e {okV1e}/{n}");
        Assert.True(ok >= n * 9 / 10, $"{ok}/{n}");
        Assert.True(okV1e < ok);
        // die kurze Hebegeste von V1e bleibt (Gegenprobe)
        var v0 = new Vorfuehrung { DauerS = 0.15, RuheVorS = 0.3, RuheNachS = 0.5 };
        var hebe = Abspielen(G.Erzeugen(u => (new Haltung(G.V(-0.2, -0.5 + 0.08 * u, 0.35), G.V(0, 0, 1), G.V(0, 1, 0), G.Kr(0.1)),
            new Haltung(G.V(0.2, -0.5 + 0.08 * u, 0.35), G.V(0, 0, 1), G.V(0, 1, 0), G.Kr(0.1))), v0), Satz.V1f);
        Assert.Equal((1, 0, 0), (hebe.Hebe, hebe.Weg, hebe.Hinein));
        // nur eine Hand hebt, bzw. die Handflächen nach unten: nichts
        Assert.Equal(0, Abspielen(G.Erzeugen(u => (new Haltung(G.V(-0.2, -0.5, 0.35), G.V(0, 0, 1), G.V(0, 1, 0), G.Kr(0.1)),
            new Haltung(G.V(0.2, -0.5 + 0.1 * u, 0.35), G.V(0, 0, 1), G.V(0, 1, 0), G.Kr(0.1))), v0), Satz.V1f).Hebe);
        Assert.Equal(0, Abspielen(G.Erzeugen(u => (new Haltung(G.V(-0.2, -0.5 + 0.1 * u, 0.35), G.V(0, 0, 1), G.V(0, -1, 0), G.Kr(0.1)),
            new Haltung(G.V(0.2, -0.5 + 0.1 * u, 0.35), G.V(0, 0, 1), G.V(0, -1, 0), G.Kr(0.1))), v0), Satz.V1f).Hebe);
        // Stellschrauben der Hebegeste
        var e = XrBedienEinstellungen.Vorgabe;
        Assert.Equal((60.0, 45.0, 0.0, 0.5), (e.Hebe.HandflaecheGrad, e.Hebe.DrehGrad, e.Hebe.VorhalteS, e.Hebe.GleichzeitigS));
        Assert.Equal(0.1, e.MitStellschraube("hebe.vorhalte_s", W("0.1"))!.Hebe.VorhalteS);
        Assert.Equal(25, e.MitStellschraube("hebe.dreh_grad", W("25"))!.Hebe.DrehGrad);
        Assert.Null(e.MitStellschraube("hebe.dreh_grad", W("5")));
        Assert.Equal(0.1, e.MitStellschraube("hebe.vorhalte_s", W("0.1"))!.Hebe.AnstossHebe.HaltungVorS);
        Assert.Equal(0.1, e.Wegwischen.Anstoss.HaltungVorS);   // die übrigen Gesten behalten ihre Vorhaltezeit
    }

    [Fact]
    public void Hebe_verworfen_nennt_den_Grund_einmal_je_Ansatz_gedrosselt()
    {
        var v0 = new Vorfuehrung { DauerS = 0.2, RuheVorS = 0.3, RuheNachS = 0.6 };
        // Handflächen nach unten angehoben: verworfen wegen der Handfläche
        var r = Abspielen(G.Erzeugen(u => (new Haltung(G.V(-0.2, -0.5 + 0.12 * u, 0.35), G.V(0, 0, 1), G.V(0, -1, 0), G.Kr(0.1)),
            new Haltung(G.V(0.2, -0.5 + 0.12 * u, 0.35), G.V(0, 0, 1), G.V(0, -1, 0), G.Kr(0.1))), v0), Satz.V1f);
        Assert.Equal(0, r.Hebe);
        Assert.Single(r.Verworfen);
        Assert.Contains("Handfläche", r.Verworfen[0]);
        Assert.Contains("°", r.Verworfen[0]);
        aus.WriteLine($"Beispiel: «Hebe verworfen: {r.Verworfen[0]}»");
        // Fäuste angehoben: der Grund ist die Hand
        var f = Abspielen(G.Erzeugen(u => (new Haltung(G.V(-0.2, -0.5 + 0.12 * u, 0.35), G.V(0, 0, 1), G.V(0, 1, 0), G.Kr(0.95)),
            new Haltung(G.V(0.2, -0.5 + 0.12 * u, 0.35), G.V(0, 0, 1), G.V(0, 1, 0), G.Kr(0.95))), v0), Satz.V1f);
        Assert.Single(f.Verworfen);
        Assert.Contains("Faust", f.Verworfen[0]);
        // zu langsam (Handflächen oben, 8 cm in 0.8 s): Tempo bzw. Weg
        var l = Abspielen(G.Erzeugen(u => (new Haltung(G.V(-0.2, -0.5 + 0.1 * u, 0.35), G.V(0, 0, 1), G.V(0, 1, 0), G.Kr(0.1)),
            new Haltung(G.V(0.2, -0.5 + 0.1 * u, 0.35), G.V(0, 0, 1), G.V(0, 1, 0), G.Kr(0.1))), v0 with { DauerS = 0.6 }), Satz.V1f);
        Assert.Equal(0, l.Hebe);
        Assert.Single(l.Verworfen);
        Assert.Matches("Tempo|Weg", l.Verworfen[0]);
        // eine gelungene Hebegeste meldet nichts; ruhige Hände und eine Hand allein auch nicht
        var ok = Abspielen(G.Erzeugen(G.Hebe, new Vorfuehrung { DauerS = 0.5 }), Satz.V1f);
        Assert.Equal(1, ok.Hebe);
        Assert.Empty(ok.Verworfen);
        Assert.Empty(Abspielen(G.Erzeugen(G.Ruhe, new Vorfuehrung { DauerS = 3, RauschenM = 0.008 }), Satz.V1f).Verworfen);
        // gedrosselt: zwei verworfene Ansätze kurz hintereinander (in 1 s) – eine Zeile
        GestenBewegung unten = u => (new Haltung(G.V(-0.2, -0.5 + 0.12 * Math.Sin(Math.PI * u), 0.35), G.V(0, 0, 1), G.V(0, -1, 0), G.Kr(0.1)),
            new Haltung(G.V(0.2, -0.5 + 0.12 * Math.Sin(Math.PI * u), 0.35), G.V(0, 0, 1), G.V(0, -1, 0), G.Kr(0.1)));
        var (zwei, _) = G.Strom([(unten, new Vorfuehrung { DauerS = 0.5, RuheVorS = 0.2, RuheNachS = 0.05 }), (unten, new Vorfuehrung { DauerS = 0.5, RuheVorS = 0, RuheNachS = 1.2 })], uebergangS: 0.05);
        var z = Abspielen(zwei, Satz.V1f);
        Assert.Single(z.Verworfen);
    }
}
