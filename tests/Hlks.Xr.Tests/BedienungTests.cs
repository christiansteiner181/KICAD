// Quest App P5a: Regeln der Bedienung – Wisch in allen Richtungen und seine Bedeutung, Stellschrauben, schwebende Apps (Lage, Nachführen, Einblenden,
// Blättern), Berühren (Poke, Fang, Pinch wählt), Zögern und Geisterhand, Tunnelblick, Grenzen des Zoomens.

using System.Text.Json;
using Hlks.Xr.Bedienung;
using static Hlks.Xr.Tests.T;

namespace Hlks.Xr.Tests;

public class BedienungTests
{
    const double Gieren = Math.PI / 2;   // Blick nach −z; rechts = +x
    static JsonElement W(string json) => JsonDocument.Parse(json).RootElement;

    /// <summary>Verlauf einer Bewegung um d in dauer s (90 Hz), ab p0.</summary>
    static List<WischProbe> Bewegung(Vec3 p0, Vec3 d, double dauer)
    {
        int n = (int)Math.Round(dauer * 90);
        return [.. Enumerable.Range(0, n + 1).Select(i => new WischProbe(i / 90.0, p0 + d * ((double)i / n)))];
    }

    // ------------------------------------------------------------------------------------------------ Wisch

    [Fact]
    public void Wisch_in_allen_Richtungen_und_eingeordnet_zum_Blick()
    {
        var p0 = V(0, 1.2, -0.4);
        foreach (var (d, erwartet) in new[]
        {
            (V(0.25, 0, 0), WischRichtung.Rechts), (V(-0.25, 0, 0), WischRichtung.Links), (V(0, 0.25, 0), WischRichtung.Hoch),
            (V(0, -0.25, 0), WischRichtung.Runter), (V(0, 0, -0.25), WischRichtung.Vor), (V(0, 0, 0.25), WischRichtung.Zurueck),
        })
        {
            var w = XrGesten.WischErkennen(Bewegung(p0, d, 0.2));
            Assert.NotNull(w);
            Assert.Equal(erwartet, XrGesten.Einordnen(w!.Richtung, Gieren, WischEinstellungen.ToleranzVorgabe));
        }
        // Blick nach Osten (+x, Gieren 0): ein Wisch nach +x ist «vor»
        Assert.Equal(WischRichtung.Vor, XrGesten.Einordnen(V(1, 0, 0), 0, 37));
        Assert.Equal(WischRichtung.Rechts, XrGesten.Einordnen(V(0, 0, 1), 0, 37));
        // diagonal 45° zwischen waagrecht und senkrecht: weder noch (Toleranz 37°)
        Assert.Null(XrGesten.WischErkennen(Bewegung(p0, V(0.2, 0.2, 0), 0.2)));
        // langsam (10 cm/s) oder kurz (10 cm) ist kein Wisch
        Assert.Null(XrGesten.WischErkennen(Bewegung(p0, V(0.1, 0, 0), 1.0)));
        Assert.Null(XrGesten.WischErkennen(Bewegung(p0, V(0, 0.1, 0), 0.1)));
        Assert.Null(XrGesten.Einordnen(V(0, 0, 0), Gieren, 37));
    }

    [Fact]
    public void Waagrecht_genau_wie_der_Prototyp()
    {
        // die Vorgabe der Toleranz ist «mindestens 80 % waagrecht» (XrWischLogik.WischWaagrecht)
        Nah(WischEinstellungen.ToleranzVorgabe, Math.Acos(0.8) / Grad, 0.005);   // P5b: auf 0.01° gerundet (Katalog), die Regel bleibt genau 0.8
        Assert.Equal(0.8, WischEinstellungen.Vorgabe.CosToleranz);
        foreach (var d in new[] { V(0.25, 0.1, 0), V(0.25, 0.2, 0), V(0.2, 0, 0.1), V(0.12, 0, 0) })
        {
            var proto = XrWischLogik.WischErkennen(Bewegung(V(0, 1, 0), d, 0.2));
            var neu = XrGesten.WischErkennen(Bewegung(V(0, 1, 0), d, 0.2));
            Assert.Equal(proto is null, neu is null || neu.Richtung.Y != 0);   // was der Prototyp waagrecht erkennt, erkennt P5a waagrecht
            if (proto is not null) { Nah(proto.Richtung.X, neu!.Richtung.X); Nah(proto.Richtung.Z, neu.Richtung.Z); }
        }
        // XrWischLogik.WischSchritt ohne Einstellungen bleibt waagrecht (Masslinien, Prototyp)
        var z = XrWischLogik.NeuerWischer();
        WischErgebnis? r = null;
        foreach (var p in Bewegung(V(0, 1, 0), V(0, 0.25, 0), 0.2)) r ??= XrWischLogik.WischSchritt(z, p.T, p.P, true);
        Assert.Null(r);
    }

    [Fact]
    public void Richtungstoleranz_als_Stellschraube()
    {
        var w = new WischEinstellungen { RichtungToleranzGrad = 50 };
        // 40° aus der Waagrechten: mit 37° kein Wisch, mit 50° waagrecht
        var d = V(Math.Cos(40 * Grad), Math.Sin(40 * Grad), 0) * 0.3;
        Assert.Null(XrGesten.WischErkennen(Bewegung(V(0, 1, 0), d, 0.2)));
        var e = XrGesten.WischErkennen(Bewegung(V(0, 1, 0), d, 0.2), w);
        Assert.Equal(WischRichtung.Rechts, XrGesten.Einordnen(e!.Richtung, Gieren, w.RichtungToleranzGrad));
        // die Toleranz gilt auch für vor/zurück: 30° aus der Blickrichtung ist mit 37° «vor», mit 20° «seitlich»
        var vorn30 = V(Math.Sin(30 * Grad), 0, -Math.Cos(30 * Grad));
        Assert.Equal(WischRichtung.Vor, XrGesten.Einordnen(vorn30, Gieren, 37));
        Assert.Equal(WischRichtung.Rechts, XrGesten.Einordnen(vorn30, Gieren, 20));
    }

    [Fact]
    public void Pinch_Wisch_mit_Sperre_der_Gegenrichtung()
    {
        // eine lückenlose Spur wie in der Brille (90 Hz): je Abschnitt Dauer und Weg (Weg null = ruhig)
        static List<WischProbe> Spur(params (double Dauer, Vec3 Weg)[] abschnitte)
        {
            var proben = new List<WischProbe> { new(0, V(0, 1, 0)) };
            foreach (var (dauer, weg) in abschnitte)
            {
                var (t0, p0) = (proben[^1].T, proben[^1].P);
                int n = (int)Math.Round(dauer * 90);
                for (int i = 1; i <= n; i++) proben.Add(new WischProbe(t0 + i / 90.0, p0 + weg * ((double)i / n)));
            }
            return proben;
        }
        var spur = Spur((0.1, V(0, 0, 0)), (0.2, V(-0.25, 0, 0)),   // links
            (0.6, V(0, 0, 0)), (0.2, V(0.25, 0, 0)),                   // zurück nach rechts (nach 0.6 s, innerhalb der Sperre von 1 s): gesperrt
            (0.6, V(0, 0, 0)), (0.2, V(-0.25, 0, 0)),                  // wieder links: zählt
            (0.6, V(0, 0, 0)), (0.2, V(0, 0.25, 0)),                   // hoch: andere Achse, zählt
            (1.2, V(0, 0, 0)), (0.2, V(0.25, 0, 0)));                  // rechts nach der Sperre: zählt
        var z = new PinchWischZustand();
        var erkannt = new List<(double T, WischRichtung R)>();
        foreach (var p in spur) if (XrGesten.PinchWischSchritt(z, p.T, p.P, true, Gieren, null, 1.0) is { } r) erkannt.Add((p.T, r));
        Assert.True(erkannt.Select(x => x.R).SequenceEqual([WischRichtung.Links, WischRichtung.Links, WischRichtung.Hoch, WischRichtung.Rechts]),
            string.Join(", ", erkannt.Select(x => $"{x.T:0.00} {x.R}")));
        // ohne Pinch kein Wisch
        var ohne = new PinchWischZustand();
        foreach (var p in Bewegung(V(0, 1, 0), V(0.25, 0, 0), 0.2)) Assert.Null(XrGesten.PinchWischSchritt(ohne, p.T, p.P, false, Gieren, null, 1.0));
        Assert.Equal(WischRichtung.Rechts, XrGesten.Gegenteil(WischRichtung.Links));
        Assert.Equal(WischRichtung.Runter, XrGesten.Gegenteil(WischRichtung.Hoch));
        Assert.Equal(WischRichtung.Zurueck, XrGesten.Gegenteil(WischRichtung.Vor));
    }

    [Fact]
    public void Feste_Gestenbedeutungen()
    {
        var begehen = new GestenKontext(false, false, true);
        var sitzung = new GestenKontext(true, false, true);
        var app = new GestenKontext(false, true, true);
        Assert.Equal(GestenAktion.Drehen, Gestenbedeutung.Wisch(WischRichtung.Links, begehen));
        Assert.Equal(GestenAktion.Nichts, Gestenbedeutung.Wisch(WischRichtung.Links, sitzung));
        Assert.Equal(GestenAktion.SeiteVor, Gestenbedeutung.Wisch(WischRichtung.Links, app));
        Assert.Equal(GestenAktion.SeiteZurueck, Gestenbedeutung.Wisch(WischRichtung.Rechts, app with { Sitzungsraum = true }));
        foreach (var k in new[] { begehen, sitzung, app })
        {
            Assert.Equal(GestenAktion.EtageHoch, Gestenbedeutung.Wisch(WischRichtung.Hoch, k));
            Assert.Equal(GestenAktion.EtageRunter, Gestenbedeutung.Wisch(WischRichtung.Runter, k));
            Assert.Equal(GestenAktion.Nichts, Gestenbedeutung.Wisch(WischRichtung.Hoch, k with { EtageMitWisch = false }));   // Variante «etage» = «app»
            Assert.Equal(GestenAktion.Nichts, Gestenbedeutung.Wisch(WischRichtung.Zurueck, k));
        }
        // V1a: Pinch + Wisch nach vorn löst nichts mehr aus (Schliessen geht mit dem Wegwischen, Christian 24.10.2026)
        Assert.Equal(GestenAktion.Nichts, Gestenbedeutung.Wisch(WischRichtung.Vor, app));
        Assert.Equal(GestenAktion.Nichts, Gestenbedeutung.Wisch(WischRichtung.Vor, begehen));
        Assert.Equal(GestenAktion.Nichts, Gestenbedeutung.Wisch(WischRichtung.Vor, begehen with { AppsSichtbar = false }));
        // die Tabelle nennt alle festen Gesten
        var text = string.Join(" ", Gestenbedeutung.Tabelle.Select(x => x.Geste));
        foreach (var g in new[] { "Antippen", "Pinch", "links/rechts", "hoch/runter", "Wegwischen", "Handfläche", "Faust", "Griff", "Zögern" }) Assert.Contains(g, text);   // V1d: ein Griff statt zwei
    }

    // ------------------------------------------------------------------------------------------------ Stellschrauben

    [Fact]
    public void Stellschrauben_mit_Grenzen_und_Zuruecksetzen()
    {
        var e = XrBedienEinstellungen.Vorgabe;
        Assert.Equal(1.35, e.Apps.Skala);
        Assert.Equal(0.18, e.Apps.GroesseM);
        Assert.Equal(Anordnungen.Bogen, e.Apps.Anordnung);
        Assert.Equal(0.025, e.Pinch.AnM);
        Assert.Equal(0.04, e.Pinch.AusM);
        Assert.Equal(0.03, e.Pinch.FangRadiusM);
        Assert.True(e.Komfort.Tunnelblick);
        Assert.Equal(new ZoomGrenzen(5, 200), e.Zoom);
        Assert.True(e.MitApps);
        Assert.False(e.EtageMitWisch);                       // seit dem Brillentest 24.10.2026: Etage nur in der App

        Assert.Equal(1.6, e.MitStellschraube("anzeige.skala", W("1.6"))!.Apps.Skala);
        Assert.Null(e.MitStellschraube("anzeige.skala", W("4")));                      // über 3
        Assert.Null(e.MitStellschraube("anzeige.skala", W("\"gross\"")));              // falscher Typ
        Assert.Equal(1.35, (e with { Apps = e.Apps with { Skala = 2 } }).MitStellschraube("anzeige.skala", W("null"))!.Apps.Skala);
        Assert.Equal(Anordnungen.Reihe, e.MitStellschraube("apps.anordnung", W("\"Reihe\""))!.Apps.Anordnung);
        Assert.Null(e.MitStellschraube("apps.anordnung", W("\"kreis\"")));
        Assert.Equal(0.12, e.MitStellschraube("wisch.mindestweg_m", W("0.12"))!.Wisch.MindestwegM);
        Assert.Equal(1.5, e.MitStellschraube("wisch.mindesttempo_ms", W("1.5"))!.Wisch.MindesttempoMs);
        Assert.Equal(45, e.MitStellschraube("wisch.richtung_toleranz_grad", W("45"))!.Wisch.RichtungToleranzGrad);
        Assert.Equal(0.05, e.MitStellschraube("fang.radius_m", W("0.05"))!.Pinch.FangRadiusM);
        Assert.False(e.MitStellschraube("komfort.tunnelblick", W("false"))!.Komfort.Tunnelblick);
        Assert.Null(e.MitStellschraube("komfort.tunnelblick", W("0")));               // ja/nein, keine Zahl
        Assert.Equal(0.8, e.MitStellschraube("komfort.tunnelblick_staerke", W("0.8"))!.Komfort.Staerke);
        Assert.False(e.MitStellschraube("komfort.ton", W("false"))!.Komfort.Ton);
        Assert.Equal(45, e.MitStellschraube("begehen.drehwinkel_grad", W("45"))!.DrehwinkelGrad);
        Assert.False(e.MitStellschraube("labor.variante.bedienung", W("\"tablet\""))!.MitApps);
        Assert.Null(e.MitStellschraube("labor.variante.bedienung", W("\"box\"")));
        Assert.False(e.MitStellschraube("labor.variante.etage", W("\"app\""))!.EtageMitWisch);
        Assert.Null(e.MitStellschraube("begehen.ziel_ruhe_s", W("0.3")));             // gehört zu HandBegehenEinstellungen
        Assert.True(XrBedienEinstellungen.Kennt("sitzung.zoom_max"));
        Assert.False(XrBedienEinstellungen.Kennt("haltung.schwelle_m"));
        // jeder Schlüssel mit Zahl hat Grenzen, jeder Schlüssel wird verstanden
        foreach (var k in XrBedienEinstellungen.Schluessel) Assert.NotNull(e.MitStellschraube(k, W("null")));
    }

    [Fact]
    public void Paare_Pinch_an_unter_aus_und_Zoom_min_unter_max()
    {
        var e = XrBedienEinstellungen.Vorgabe;
        Assert.Equal(0.03, e.MitStellschraube("pinch.schwelle_an_m", W("0.03"))!.Pinch.AnM);
        Assert.Null(e.MitStellschraube("pinch.schwelle_an_m", W("0.045")));            // nicht unter «aus» (0.04)
        Assert.Null(e.MitStellschraube("pinch.schwelle_aus_m", W("0.02")));            // nicht über «an» (0.025)
        Assert.Equal(new ZoomGrenzen(20, 200), e.MitStellschraube("sitzung.zoom_min", W("20"))!.Zoom);
        Assert.Equal(new ZoomGrenzen(5, 500), e.MitStellschraube("sitzung.zoom_max", W("500"))!.Zoom);
        Assert.Null(e.MitStellschraube("sitzung.zoom_max", W("40")));                  // unter 50 (Katalog)
        // mehrere auf einmal (gemerkt, «willkommen»): in jeder Reihenfolge
        var (satz, ab) = e.MitStellschrauben([new("pinch.schwelle_an_m", W("0.05")), new("pinch.schwelle_aus_m", W("0.07")), new("haltung.schwelle_m", W("1.2")),
            new("sitzung.zoom_min", W("100")), new("sitzung.zoom_max", W("60"))]);
        Assert.Equal(0.05, satz.Pinch.AnM);                                         // «an» kam im zweiten Durchgang, nach «aus»
        Assert.Equal(0.07, satz.Pinch.AusM);
        Assert.Equal(new ZoomGrenzen(100, 200), satz.Zoom);                           // widersprüchlich (100 ≥ 60): der erste gilt,
        Assert.Equal(["sitzung.zoom_max"], ab);                                       // der zweite ist abgewiesen; fremde Schlüssel (haltung.*) übergangen
    }

    [Fact]
    public void Pinch_Schwellen_an_einer_Stelle()
    {
        var g = new Dictionary<string, Vec3> { ["thumb-tip"] = V(0, 0, 0), ["index-finger-tip"] = V(0.03, 0, 0) };
        Assert.False(XrTabletLogik.IstPinch(g));
        Assert.True(XrTabletLogik.IstPinch(g, true));
        var p = new PinchEinstellungen { AnM = 0.035, AusM = 0.05 };
        Assert.True(XrTabletLogik.IstPinch(g, false, p));
        Assert.False(XrTabletLogik.IstPinch(g, true, new PinchEinstellungen { AnM = 0.01, AusM = 0.02 }));
    }

    [Fact]
    public void Zoom_in_den_Grenzen_der_Stellschrauben()
    {
        Assert.Equal(5, XrSitzungLogik.MassstabBegrenzen(1));
        Assert.Equal(20, XrSitzungLogik.MassstabBegrenzen(1, new ZoomGrenzen(20, 200)));
        Assert.Equal(500, XrSitzungLogik.MassstabBegrenzen(800, new ZoomGrenzen(5, 500)));
        Assert.Equal(50, XrSitzungLogik.MassstabBegrenzen(double.NaN));
        Assert.Equal("1:500", XrSitzungLogik.MassstabText(500, new ZoomGrenzen(5, 500)));
        // Einpassen: ein 400 m langes Gelände passt erst mit 1:500 als Grenze (Platte 1.4 m → 1:286)
        var gross = new ModellGroesse(400, 100, 10);
        Assert.False(XrSitzungLogik.Einpassen(gross).Passt);
        var e = XrSitzungLogik.Einpassen(gross, XrSitzungLogik.MassstabStart, new ZoomGrenzen(5, 500));
        Assert.True(e.Passt);
        Assert.True(e.Lage.Massstab is > 200 and <= 500);
        Assert.Equal(20, XrSitzungLogik.AusschnittLage(new ModellGroesse(3, 3, 3), new ZoomGrenzen(20, 200)).Lage.Massstab);
        // zwei Hände: auseinander zoomt nur bis zur Grenze
        var lage = new SitzungLage(50, 0, 0, 0);
        var g = XrSitzungLogik.ZweihandStart(lage, new Xz(-0.1, -0.9), new Xz(0.1, -0.9));
        Assert.Equal(25, XrSitzungLogik.ZweihandSchritt(g, new Xz(-0.2, -0.9), new Xz(0.2, -0.9)).Massstab, 6);
        Assert.Equal(40, XrSitzungLogik.ZweihandSchritt(g, new Xz(-0.2, -0.9), new Xz(0.2, -0.9), 0, 0, new ZoomGrenzen(40, 200)).Massstab, 6);
    }

    // ------------------------------------------------------------------------------------------------ Schwebende Apps: Lage

    static readonly Vec3 Auge = V(0, 1.6, 0);
    static readonly AppAnker Anker = new(Auge, Gieren);

    [Fact]
    public void Bogen_auf_Brusthoehe_im_Griffbereich()
    {
        var e = new AppEinstellungen();
        Assert.Equal((2, 4), AppLayout.Raster(8));
        Assert.Equal((2, 3), AppLayout.Raster(5));
        Assert.Equal((1, 4), AppLayout.Raster(4));
        var k = AppLayout.Kacheln(5, Anker, e);
        Assert.Equal(5, k.Count);
        var brust = Auge - V(0, AppLayout.BrustUnterAugeM, 0);
        foreach (var p in k)
        {
            Nah(Math.Sqrt(Math.Pow(p.Mitte.X - brust.X, 2) + Math.Pow(p.Mitte.Z - brust.Z, 2)), e.AbstandM, 1e-9);   // auf dem Bogen
            Assert.True(p.Mitte.Z < 0);                                                    // vor einem
            Assert.Equal(e.GroesseM, p.B);
            Assert.True((Auge - p.Mitte).Einheit().Skalar(p.Normale) > 0.999);             // zum Auge gedreht
            Nah(p.Rechts.Y, 0, 1e-9);                                                      // aufrecht
        }
        // erste Reihe oben (3 Kacheln) von links nach rechts, zweite darunter (2)
        Assert.True(k[0].Mitte.X < k[1].Mitte.X && k[1].Mitte.X < k[2].Mitte.X);
        Nah(k[1].Mitte.X, 0, 1e-9);
        Assert.True(k[3].Mitte.Y < k[0].Mitte.Y);
        Nah((k[0].Mitte.Y + k[3].Mitte.Y) / 2, brust.Y, 1e-9);                             // Reihen um die Brusthöhe
        Nah(k[0].Mitte.Y - k[3].Mitte.Y, e.GroesseM * (1 + AppLayout.ReihenLueckeAnteil), 1e-9);
        // vom Auge aus liegt der obere Rand der unteren Reihe unter dem unteren Rand der oberen (keine Verdeckung, Prüfbild P5a)
        double Abwaerts(Vec3 p) => Math.Atan2(Auge.Y - p.Y, Math.Sqrt(Math.Pow(p.X - Auge.X, 2) + Math.Pow(p.Z - Auge.Z, 2)));
        Assert.True(Abwaerts(k[4].Mitte + k[4].Oben * (k[4].H / 2)) > Abwaerts(k[1].Mitte - k[1].Oben * (k[1].H / 2)));
        // Nachbarn überlappen nicht: Abstand der Mitten mindestens Kachel + Lücke (als Sehne knapp darunter)
        Assert.True(k[0].Mitte.Abstand(k[1].Mitte) > e.GroesseM);
        // Reihe: alle in einer Ebene, gleich ausgerichtet
        var r = AppLayout.Kacheln(4, Anker, e with { Anordnung = Anordnungen.Reihe });
        Assert.All(r, p => Nah(p.Mitte.Z, r[0].Mitte.Z, 1e-9));
        Assert.All(r, p => Nah(p.Normale.Skalar(r[0].Normale), 1, 1e-9));
        Nah(r[1].Mitte.X - r[0].Mitte.X, e.GroesseM * (1 + AppLayout.LueckeAnteil), 1e-9);
        // an der Hand: ohne Hand nichts, mit Hand halb so gross in der Ebene der Hand
        Assert.Empty(AppLayout.Kacheln(4, Anker, e with { Anordnung = Anordnungen.AnHand }));
        var hand = XrTabletLogik.TabletLageAus(V(-0.2, 1.2, -0.35), V(0.3, 0.4, 0.35));
        var h = AppLayout.Kacheln(4, Anker, e with { Anordnung = Anordnungen.AnHand }, hand);
        Assert.Equal(4, h.Count);
        Assert.All(h, p => { Nah(p.B, e.GroesseM * AppLayout.AnHandAnteil); Nah((p.Mitte - hand.Mitte).Skalar(hand.Normale), 0, 1e-9); });
    }

    [Fact]
    public void Offene_App_mit_Eintraegen_und_Schliessen()
    {
        var e = new AppEinstellungen();
        var panel = AppLayout.Panel(Anker, e)!;
        var (b, h) = AppLayout.PanelGroesse(e);
        Assert.Equal(b, panel.B);
        Assert.Equal(h, panel.H);
        Assert.True(panel.Mitte.Y + h / 2 < Auge.Y);                       // oberer Rand unter dem Blick
        var ein = AppLayout.Eintraege(panel, 10, e);
        Assert.Equal(8, ein.Count);                                          // höchstens 2 × 4 je Seite
        foreach (var p in ein)
        {
            var (aussen, tiefe) = AppBeruehrung.Lage(panel, p.Mitte);
            Assert.Equal(0, aussen);                                         // in der App
            Nah(tiefe, 0.002, 1e-9);                                         // knapp davor
            Assert.True(Math.Abs((p.Mitte - panel.Mitte).Skalar(panel.Oben)) + p.H / 2 <= h / 2 + 1e-9);
        }
        Assert.True(ein[0].Mitte.X < ein[1].Mitte.X);                        // zeilenweise
        Assert.True(ein[2].Mitte.Y < ein[0].Mitte.Y);
        var x = AppLayout.Schliessen(panel, e);
        Assert.True((x.Mitte - panel.Mitte).Skalar(panel.Rechts) > 0 && (x.Mitte - panel.Mitte).Skalar(panel.Oben) > (ein[0].Mitte - panel.Mitte).Skalar(panel.Oben));
        Assert.Null(AppLayout.Panel(Anker, e with { Anordnung = Anordnungen.AnHand }));
        // so viele Zeilen, wie die Einträge brauchen; mit mehreren Seiten alle 4; der obere Rand bleibt etwa am selben Ort
        Assert.Equal([1, 1, 2, 4, 4, 4], new[] { 0, 2, 3, 8, 9, 30 }.Select(AppLayout.Zeilen));
        var klein = AppLayout.Panel(Anker, e, null, 2)!;
        Assert.True(klein.H < panel.H);
        Nah(klein.Mitte.Y + klein.H / 2, panel.Mitte.Y + panel.H / 2, 0.02);
        Assert.Equal(4, AppLayout.Eintraege(klein, 4, e).Count);
        Assert.All(AppLayout.Eintraege(klein, 4, e), p => Assert.Equal(0, AppBeruehrung.Lage(klein, p.Mitte).Aussen));
    }

    [Fact]
    public void Blaettern_in_Listen()
    {
        Assert.Equal(8, AppListe.JeSeite);
        Assert.Equal(1, AppListe.Seiten(0));
        Assert.Equal(1, AppListe.Seiten(8));
        Assert.Equal(2, AppListe.Seiten(9));
        Assert.Equal(1, AppListe.Blaettern(0, 12, WischRichtung.Links));     // nach links = nächste Seite
        Assert.Equal(1, AppListe.Blaettern(1, 12, WischRichtung.Links));     // am Ende bleibt sie
        Assert.Equal(0, AppListe.Blaettern(1, 12, WischRichtung.Rechts));
        Assert.Equal(0, AppListe.Blaettern(0, 12, WischRichtung.Hoch));
        Assert.Equal(1, AppListe.SeiteVon(9));
        Assert.Equal([8, 9, 10, 11], AppListe.AufSeite(Enumerable.Range(0, 12).ToList(), 1));
        Assert.Equal([8, 9, 10, 11], AppListe.AufSeite(Enumerable.Range(0, 12).ToList(), 5));   // begrenzt
    }

    [Fact]
    public void Apps_folgen_dem_Blick_traege()
    {
        var z = new AnkerZustand();
        var a = AppLayout.Nachfuehren(z, Auge, Gieren, 1 / 72.0, false);
        Assert.Equal(Anker, a);
        // 20° zur Seite geschaut: bleibt (Totzone 30°)
        for (int i = 0; i < 100; i++) a = AppLayout.Nachfuehren(z, Auge, Gieren + 20 * Grad, 1 / 72.0, false);
        Assert.Equal(Gieren, a.Gieren);
        // 45°: folgt weich bis fast hin, nicht in einem Bild
        a = AppLayout.Nachfuehren(z, Auge, Gieren + 45 * Grad, 1 / 72.0, false);
        Assert.True(a.Gieren > Gieren && a.Gieren < Gieren + 5 * Grad);
        for (int i = 0; i < 200; i++) a = AppLayout.Nachfuehren(z, Auge, Gieren + 45 * Grad, 1 / 72.0, false);
        Assert.True(Math.Abs(a.Gieren - (Gieren + 45 * Grad)) < 10 * Grad);
        // mit einer Hand an den Apps bleibt es stehen
        var vorher = a;
        for (int i = 0; i < 50; i++) a = AppLayout.Nachfuehren(z, Auge, Gieren - 60 * Grad, 1 / 72.0, true);
        Assert.Equal(vorher, a);
        // grosse Verschiebung (Sitzungsraum betreten, Kopf weit weg): sofort
        a = AppLayout.Nachfuehren(z, Auge + V(1, 0, 0), Gieren, 1 / 72.0, true);
        Assert.Equal(new AppAnker(Auge + V(1, 0, 0), Gieren), a);
        Assert.Equal(new AppAnker(Auge, 0), AppLayout.Holen(z, Auge, 0));
    }

    /// <summary>Linke Hand, Handfläche zum Kopf (wie in XrTabletLogikTests): offen vor dem Gesicht.</summary>
    static Dictionary<string, Vec3> LinkeHandOffen(bool zumKopf)
    {
        // Handfläche in der Ebene z = −0.35, Finger nach oben; Normale der linken Hand = −(a × b) → zum Kopf (+z) bzw. weg
        var w = V(-0.1, 1.3, -0.35);
        double s = zumKopf ? 1 : -1;
        Vec3 P(double x, double y) => w + V(s * x, y, 0);
        return new()
        {
            ["wrist"] = w,
            ["index-finger-metacarpal"] = P(0.03, 0.03), ["pinky-finger-metacarpal"] = P(-0.03, 0.03),
            ["middle-finger-phalanx-proximal"] = P(0, 0.09),
            ["index-finger-tip"] = P(0.03, 0.18), ["middle-finger-tip"] = P(0, 0.19), ["ring-finger-tip"] = P(-0.02, 0.18), ["pinky-finger-tip"] = P(-0.04, 0.16),
            ["thumb-tip"] = P(0.08, 0.06),
        };
    }

    [Fact]
    public void Linke_Handflaeche_holt_die_Apps()
    {
        var kopf = V(-0.1, 1.6, 0);
        var offen = LinkeHandOffen(true);
        var weg = LinkeHandOffen(false);
        // welche Seite zum Kopf zeigt, hängt von der Händigkeit ab: die Probe nimmt die, die TabletGeste erkennt
        if (!XrTabletLogik.TabletGeste(offen, "left", kopf)) (offen, weg) = (weg, offen);
        Assert.True(XrTabletLogik.TabletGeste(offen, "left", kopf));
        var z = new EinblendZustand();
        var ausgeloest = new List<double>();
        for (int i = 0; i < 72; i++) if (AppLayout.Einblenden(z, i / 72.0, offen, kopf)) ausgeloest.Add(i / 72.0);
        Assert.Single(ausgeloest);                                     // einmal je Geste
        Assert.InRange(ausgeloest[0], AppLayout.EinblendenHaltenS - 1e-9, AppLayout.EinblendenHaltenS + 1 / 72.0);
        Assert.True(z.Geste);
        Assert.False(AppLayout.Einblenden(z, 1.1, weg, kopf));          // weggedreht: aus
        Assert.False(z.Geste);
        var wieder = false;
        for (int i = 0; i < 40; i++) wieder |= AppLayout.Einblenden(z, 1.2 + i / 72.0, offen, kopf);
        Assert.True(wieder);                                           // die nächste Geste zählt wieder
        Assert.False(AppLayout.Einblenden(new EinblendZustand(), 0, null, kopf));
    }

    // ------------------------------------------------------------------------------------------------ Berühren

    static readonly TabletPose Kachel = XrTabletLogik.TabletLageAus(V(0, 1.3, -0.45), V(0, 0, 1)) with { B = 0.18, H = 0.18 };
    static readonly TabletPose Nachbar = XrTabletLogik.TabletLageAus(V(0.21, 1.3, -0.45), V(0, 0, 1)) with { B = 0.18, H = 0.18 };

    /// <summary>Fingerspitze fährt von z0 nach z1 (vor der Kachel, Normale +z) bei (x, y), n Bilder.</summary>
    static List<BeruehrErgebnis> Stoss(BeruehrHand z, double x, double y, double z0, double z1, int n = 10, bool pinch = false, IReadOnlyList<TabletPose>? ziele = null)
        => [.. Enumerable.Range(0, n + 1).Select(i => AppBeruehrung.Schritt(z, V(x, y, -0.45 + z0 + (z1 - z0) * i / n), pinch, ziele ?? [Kachel, Nachbar]))];

    [Fact]
    public void Antippen_loest_einmal_aus_und_leuchtet_beim_Annaehern()
    {
        var z = new BeruehrHand();
        var lauf = Stoss(z, 0.02, 1.32, 0.12, -0.01);
        Assert.Equal(0, lauf[0].Naehe);                                           // 12 cm davor: noch nichts
        Assert.Null(lauf[0].Hover);
        var erste = lauf.First(r => r.Hover is not null);
        Assert.True(erste.HoverNeu);                                              // Fang: Ton
        Assert.True(lauf.Where(r => r.Hover is not null).Select(r => r.Naehe).SequenceEqual(lauf.Where(r => r.Hover is not null).Select(r => r.Naehe).Order()));   // heller beim Annähern
        Assert.Equal([0], lauf.Where(r => r.Ausgeloest is not null).Select(r => r.Ausgeloest!.Value));
        // weiter drücken oder darin bleiben löst nicht noch einmal aus; zurückziehen und wieder hinein schon
        Assert.DoesNotContain(Stoss(z, 0.02, 1.32, -0.01, -0.03), r => r.Ausgeloest is not null);
        Assert.DoesNotContain(Stoss(z, 0.02, 1.32, -0.03, 0.015), r => r.Ausgeloest is not null);   // noch nicht weit genug zurück
        Assert.DoesNotContain(Stoss(z, 0.02, 1.32, 0.015, 0.05), r => r.Ausgeloest is not null);
        Assert.Single(Stoss(z, 0.02, 1.32, 0.05, -0.01), r => r.Ausgeloest == 0);
    }

    [Fact]
    public void Fang_zum_naechsten_Ziel_und_kein_Ausloesen_von_der_Seite()
    {
        // in der Lücke zwischen den Kacheln (1 cm neben der linken, 2 cm neben der rechten): Fang 3 cm → die nächste, die linke
        var z = new BeruehrHand();
        Assert.Single(Stoss(z, 0.10, 1.3, 0.05, -0.01), r => r.Ausgeloest == 0);
        Assert.Single(Stoss(new BeruehrHand(), 0.11, 1.3, 0.05, -0.01), r => r.Ausgeloest == 1);
        // ausserhalb des Fangs (6 cm über der Kachel) nichts
        Assert.DoesNotContain(Stoss(new BeruehrHand(), 0, 1.3 + 0.09 + 0.06, 0.05, -0.01), r => r.Hover is not null);
        // mit grösserem Fangradius (Stellschraube) doch
        var gross = new PinchEinstellungen { FangRadiusM = 0.08 };
        var zg = new BeruehrHand();
        var lauf = Enumerable.Range(0, 11).Select(i => AppBeruehrung.Schritt(zg, V(0, 1.3 + 0.15, -0.45 + 0.05 - 0.06 * i / 10), false, [Kachel], gross)).ToList();
        Assert.Contains(lauf, r => r.Ausgeloest == 0);
        // von der Seite hinter der Fläche hineinfahren löst nicht aus
        var seite = new BeruehrHand();
        var s = Enumerable.Range(0, 11).Select(i => AppBeruehrung.Schritt(seite, V(-0.3 + 0.3 * i / 10, 1.3, -0.45 - 0.01), false, [Kachel])).ToList();
        Assert.Contains(s, r => r.Hover == 0);
        Assert.DoesNotContain(s, r => r.Ausgeloest is not null);
        // Sprung des Trackings (20 cm in einem Bild) löst nicht aus
        var sprung = new BeruehrHand();
        AppBeruehrung.Schritt(sprung, V(0, 1.3, -0.45 + 0.19), false, [Kachel]);
        Assert.Null(AppBeruehrung.Schritt(sprung, V(0, 1.3, -0.45 - 0.01), false, [Kachel]).Ausgeloest);
        // Hand verloren: zurückgesetzt
        Assert.Equal(default, AppBeruehrung.Schritt(sprung, null, false, [Kachel]));
        Assert.Null(sprung.Hover);
    }

    [Fact]
    public void Pinch_waehlt_beim_Loslassen_wenn_die_Hand_ruhig_bleibt()
    {
        var p = V(0.01, 1.3, -0.45 + 0.04);
        var z = new BeruehrHand();
        Assert.Null(AppBeruehrung.Schritt(z, p, false, [Kachel]).Ausgeloest);
        Assert.Null(AppBeruehrung.Schritt(z, p, true, [Kachel]).Ausgeloest);         // Pinch beginnt
        Assert.Null(AppBeruehrung.Schritt(z, p + V(0.01, 0, 0), true, [Kachel]).Ausgeloest);
        var los = AppBeruehrung.Schritt(z, p + V(0.01, 0, 0), false, [Kachel]);      // losgelassen
        Assert.Equal(0, los.Ausgeloest);
        Assert.True(los.PerPinch);
        // Pinch und Wisch (die Hand wandert 20 cm) wählt nicht
        var w = new BeruehrHand();
        AppBeruehrung.Schritt(w, p, false, [Kachel]);
        AppBeruehrung.Schritt(w, p, true, [Kachel]);
        for (int i = 1; i <= 10; i++) AppBeruehrung.Schritt(w, p + V(-0.02 * i, 0, 0), true, [Kachel]);
        Assert.Null(AppBeruehrung.Schritt(w, p + V(-0.2, 0, 0), false, [Kachel]).Ausgeloest);
        // während des Pinch löst auch ein Stoss nicht aus
        var st = new BeruehrHand();
        Assert.DoesNotContain(Stoss(st, 0, 1.3, 0.05, -0.01, pinch: true), r => r.Ausgeloest is not null && !r.PerPinch);
    }

    [Fact]
    public void Eine_App_die_sich_unter_dem_Finger_oeffnet_loest_nichts_aus()
    {
        var z = new BeruehrHand();
        Assert.Single(Stoss(z, 0, 1.3, 0.05, -0.005), r => r.Ausgeloest == 0);
        // jetzt liegen andere Ziele an derselben Stelle (die offene App), der Finger ist noch drin
        var eintrag = XrTabletLogik.TabletLageAus(V(0, 1.3, -0.45 + 0.002), V(0, 0, 1)) with { B = 0.18, H = 0.07 };
        Assert.Null(AppBeruehrung.Schritt(z, V(0, 1.3, -0.45 - 0.005), false, [eintrag]).Ausgeloest);
        Assert.Null(AppBeruehrung.Schritt(z, V(0, 1.3, -0.45 + 0.001), false, [eintrag]).Ausgeloest);
        // erst nach dem Zurückziehen (mehr als 2.5 cm vor die Fläche) und erneutem Antippen
        Assert.Null(AppBeruehrung.Schritt(z, V(0, 1.3, -0.45 + 0.04), false, [eintrag]).Ausgeloest);
        Assert.Null(AppBeruehrung.Schritt(z, V(0, 1.3, -0.45 + 0.03), false, [eintrag]).Ausgeloest);
        Assert.Equal(0, AppBeruehrung.Schritt(z, V(0, 1.3, -0.45 - 0.002), false, [eintrag]).Ausgeloest);
    }

    // ------------------------------------------------------------------------------------------------ Zögern und Geisterhand

    [Fact]
    public void Zoegern_meldet_einmal_und_nicht_beim_Handeln()
    {
        var z = new ZoegernZustand();
        var p = V(0, 1.3, -0.4);
        var meldungen = new List<(double T, string Ziel)>();
        for (int i = 0; i <= 72 * 4; i++)
        {
            var t = i / 72.0;
            if (Geisterhand.Zoegern(z, t, p + V(0.005 * Math.Sin(t * 7), 0, 0), "kachel:etagen", false) is { } m) meldungen.Add((t, m));
        }
        Assert.Single(meldungen);                                              // nach 1.5 s, dann frühestens nach 6 s wieder
        Assert.InRange(meldungen[0].T, Geisterhand.ZoegernS - 1e-9, Geisterhand.ZoegernS + 0.02);
        Assert.Equal("kachel:etagen", meldungen[0].Ziel);
        // anderes Ziel: zählt neu
        string? anders = null;
        for (int i = 0; i <= 120; i++) anders ??= Geisterhand.Zoegern(z, 5 + i / 72.0, p, "kachel:status", false);
        Assert.Equal("kachel:status", anders);
        // wer handelt (antippt oder pincht), zögert nicht
        var h = new ZoegernZustand();
        string? x = null;
        for (int i = 0; i <= 150; i++) x ??= Geisterhand.Zoegern(h, i / 72.0, p, "kachel:etagen", i % 50 == 49);
        Assert.Null(x);
        // die Hand bewegt sich (mehr als 3 cm): beginnt neu
        var b = new ZoegernZustand();
        string? y = null;
        for (int i = 0; i <= 150; i++) y ??= Geisterhand.Zoegern(b, i / 72.0, p + V(0.1 * i / 150, 0, 0), "panel", false);
        Assert.Null(y);
        Assert.Null(Geisterhand.Zoegern(b, 9, null, "panel", false));
    }

    [Fact]
    public void Geisterhand_fuehrt_die_Geste_vor()
    {
        var hand = new Dictionary<string, Vec3>
        {
            ["wrist"] = V(0.1, 1.2, -0.25), ["index-finger-tip"] = V(0.1, 1.3, -0.3), ["thumb-tip"] = V(0.13, 1.27, -0.29), ["thumb-phalanx-distal"] = V(0.14, 1.25, -0.28),
        };
        // Antippen: Start AnlaufM vor der Kachel, nach einem Viertel der Dauer knapp darin, am Ende ausgeblendet
        var a0 = Geisterhand.Bild(GeisterGeste.Antippen, 0, hand, Kachel)!;
        Nah(AppBeruehrung.Lage(Kachel, a0.Gelenke["index-finger-tip"]).Tiefe, Geisterhand.AnlaufM, 1e-9);
        Assert.Equal(0, a0.Deckkraft);
        var a1 = Geisterhand.Bild(GeisterGeste.Antippen, Geisterhand.DauerS / 4, hand, Kachel)!;
        Nah(AppBeruehrung.Lage(Kachel, a1.Gelenke["index-finger-tip"]).Tiefe, -0.004, 1e-9);
        Assert.True(a1.Deckkraft is > 0 and <= Geisterhand.DeckkraftMax);
        // die Form bleibt: das Handgelenk wandert mit der Spitze
        Nah((a1.Gelenke["wrist"] - a1.Gelenke["index-finger-tip"]).Abstand(hand["wrist"] - hand["index-finger-tip"]), 0, 1e-9);
        Assert.Null(Geisterhand.Bild(GeisterGeste.Antippen, Geisterhand.DauerS + 0.01, hand, Kachel));
        // Blättern (V1d: Wisch auf der Tafel ohne Pinch): die Spitze knapp vor der Fläche, von rechts nach links
        var b0 = Geisterhand.Bild(GeisterGeste.Blaettern, 0.01, hand, Kachel)!;
        var b1 = Geisterhand.Bild(GeisterGeste.Blaettern, Geisterhand.DauerS / 2 * 0.6, hand, Kachel)!;
        Assert.True(b0.Gelenke["thumb-tip"].Abstand(b0.Gelenke["index-finger-tip"]) > 0.02);
        Nah((b0.Gelenke["index-finger-tip"] - Kachel.Mitte).Skalar(Kachel.Normale), Geisterhand.BlaetternVorM, 1e-9);
        Assert.True(b1.Gelenke["index-finger-tip"].X < b0.Gelenke["index-finger-tip"].X - 0.15);
        // Wegwischen (V1a): die offene Hand wandert in Richtung «Rechts» des Ziels (die App setzt es nach links), kein Pinch
        var w0 = Geisterhand.Bild(GeisterGeste.Wegwischen, 0.01, hand, Kachel)!;
        var w1 = Geisterhand.Bild(GeisterGeste.Wegwischen, Geisterhand.DauerS / 2 * 0.6, hand, Kachel)!;
        Assert.True((w1.Gelenke["wrist"] - w0.Gelenke["wrist"]).Skalar(Kachel.Rechts) > 0.15);
        Assert.True(w1.Gelenke["thumb-tip"].Abstand(w1.Gelenke["index-finger-tip"]) > 0.02);
        // Greifen: Daumen schliesst sich zur Hälfte des Durchgangs
        var g0 = Geisterhand.Bild(GeisterGeste.Greifen, 0.01, hand, Kachel)!;
        var g1 = Geisterhand.Bild(GeisterGeste.Greifen, Geisterhand.DauerS / 4, hand, Kachel)!;
        Assert.True(g1.Gelenke["thumb-tip"].Abstand(g1.Gelenke["index-finger-tip"]) < g0.Gelenke["thumb-tip"].Abstand(g0.Gelenke["index-finger-tip"]) / 3);
        Assert.Null(Geisterhand.Bild(GeisterGeste.Greifen, 0.5, new Dictionary<string, Vec3> { ["wrist"] = V(0, 0, 0) }, Kachel));
        // Knochen: nur Gelenke der WebXR-Hand, alle Finger
        Assert.Equal(24, Geisterhand.Knochen.Count);
        Assert.All(Geisterhand.Knochen, k => { Assert.Contains(k.A, HandGelenke.WebXr); Assert.Contains(k.B, HandGelenke.WebXr); });
    }

    // ------------------------------------------------------------------------------------------------ Tunnelblick

    [Fact]
    public void Tunnelblick_kurz_und_weich()
    {
        var z = new TunnelZustand();
        var k = new KomfortEinstellungen();
        Assert.Null(Tunnelblick.Form(z, 0));
        Tunnelblick.Ausloesen(z, 10, k);
        Assert.Null(Tunnelblick.Form(z, 9.99));
        var anfang = Tunnelblick.Form(z, 10.01)!.Value;
        var mitte = Tunnelblick.Form(z, 10 + Tunnelblick.AnS + 0.05)!.Value;
        var spaet = Tunnelblick.Form(z, 10 + Tunnelblick.Dauer - 0.05)!.Value;
        Assert.True(anfang.Deckkraft < mitte.Deckkraft && spaet.Deckkraft < mitte.Deckkraft);   // weich ein und aus
        Nah(mitte.Deckkraft, Tunnelblick.DeckkraftMax);
        Nah(mitte.InnenGrad, Tunnelblick.OffenGrad - (Tunnelblick.OffenGrad - Tunnelblick.EngGrad) * k.Staerke);
        Nah(mitte.AussenGrad - mitte.InnenGrad, Tunnelblick.RandGrad);
        Assert.True(anfang.InnenGrad > mitte.InnenGrad);
        Assert.Null(Tunnelblick.Form(z, 10 + Tunnelblick.Dauer + 0.01));                          // kurz: nach 0.55 s vorbei
        Assert.True(Tunnelblick.Dauer < 0.6);
        // stärker = enger
        var stark = new TunnelZustand();
        Tunnelblick.Ausloesen(stark, 0, k with { Staerke = 1 });
        Nah(Tunnelblick.Form(stark, Tunnelblick.AnS + 0.05)!.Value.InnenGrad, Tunnelblick.EngGrad);
        // aus bzw. Stärke 0: kein Tunnel
        var aus = new TunnelZustand();
        Tunnelblick.Ausloesen(aus, 0, k with { Tunnelblick = false });
        Tunnelblick.Ausloesen(aus, 0, k with { Staerke = 0 });
        Assert.Null(Tunnelblick.Form(aus, 0.1));
    }
}
