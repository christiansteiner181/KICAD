// Quest App P5b: die Prüfaufgaben des Labors in der App – Start, Warten, Zeit, Fehlgriffe, Abbrüche und Erfolg je Aufgabe, das Messprotokoll (Felder wie
// XrNachrichten.Messprotokoll am PC) und die Lage der Karte. Szenen fiktiv (Räume und Wände aus Quadern).

using System.Text.Json;
using Hlks.Xr.Labor;
using Hlks.Xr.Modell;
using Hlks.Xr.Netz;
using static Hlks.Xr.Tests.ModellHilfen;
using static Hlks.Xr.Tests.T;
using P = Hlks.Xr.Labor.PruefAufgaben;

namespace Hlks.Xr.Tests;

public class PruefAufgabenTests
{
    const double Blick = Math.PI / 2;   // Blick nach −z

    static ListenGeschoss G(string id, string kz, double kote) => new(id, kz, null, kote, null, new ListenTeil("aaaaaaaa", $"/api/xr/geschoss/{id}.glb?teil=modell", null), null);
    static readonly ListenGeschoss Ug = G("g-ug", "UG", -2700), Eg = G("g-eg", "EG", 0), Og = G("g-og", "1.OG", 2750);

    static LaborAufgabeDef Def(string id, string status = "p5") => new(id, id, "Anleitung", status, [], ["zeit", "fehlgriffe", "abbrueche"], "Kriterium", null, null);

    static PruefLauf Laufend(string id, double t = 10)
    {
        var l = P.Starten(id, Def(id));
        P.Beginnen(l, t);
        return l;
    }

    // ------------------------------------------------------------------------------------------------ Start, Warten, Abbruch

    [Fact]
    public void Start_gesperrt_wartet_laeuft_und_Abbruch()
    {
        Assert.Equal(LaufZustand.Gesperrt, P.Starten(P.MessenWandWand, Def(P.MessenWandWand, "folgt_v1")).Zustand);   // der Katalog sperrt noch
        Assert.Equal(LaufZustand.Wartet, P.Starten(P.MessenWandWand, Def(P.MessenWandWand)).Zustand);                // V1a: spielbar
        Assert.Equal(LaufZustand.Wartet, P.Starten(P.MessenWandWand, null).Zustand);
        Assert.Equal(LaufZustand.Gesperrt, P.Starten("neue-aufgabe", Def("neue-aufgabe")).Zustand);   // die App kennt sie nicht
        var ohneKatalog = P.Starten(P.Drehen90, null);
        Assert.Equal(LaufZustand.Wartet, ohneKatalog.Zustand);                    // ohne Katalog spielt die App sie mit ihren Texten
        var l = P.Starten(P.Drehen90, Def(P.Drehen90));
        Assert.Equal(0, l.ZeitMs(100));
        P.Warten(l, "Variante");
        Assert.Equal("Variante", l.Grund);
        P.Beginnen(l, 10);
        Assert.True(l.Laeuft);
        Assert.Null(l.Grund);
        Nah(l.ZeitMs(12.5), 2500);
        P.Neustart(l);                                                            // dieselbe Aufgabe nochmals: neu begonnen
        Assert.Equal(1, l.Abbrueche);
        P.Abbrechen(l, 14);
        Assert.Equal(LaufZustand.Abgebrochen, l.Zustand);
        Assert.Equal(2, l.Abbrueche);
        Nah(l.ZeitMs(99), 4000);                                                  // die Zeit steht beim Abbruch
        Assert.True(P.MitMessung(l));
        // ein wartender Lauf, der abgebrochen wird, misst nichts (kein Messprotokoll)
        var w = P.Starten(P.TabletWertAblesen, Def(P.TabletWertAblesen));
        P.Abbrechen(w, 5);
        Assert.False(P.MitMessung(w));
        Assert.Equal(0, w.Abbrueche);
        Assert.Equal(AufgabeOrt.Sitzungsraum, P.Ort(P.ModellGreifenZoomen));
        Assert.Equal(AufgabeOrt.Begehen, P.Ort(P.TeleportMarkierung));
    }

    // ------------------------------------------------------------------------------------------------ teleport-markierung

    static ModellSzene RaumSzene() => new([(Datei(null,
        RaumNetz("R-1", [new(-2, -6), new(2, -6), new(2, 0), new(-2, 0)], 0, 2.6),
        Quadernetz("architektur", ("W1", "wand", V(-2, 0, -6.2), V(2, 2.6, -6)), ("W2", "wand", V(5, 0, -3), V(5.2, 2.6, -1)))), Eg.Id, "modell")]);

    [Fact]
    public void Teleport_Markierung_im_Raum_Sprung_daneben_abgebrochen_und_Erfolg()
    {
        var s = RaumSzene();
        var (m, boden) = P.MarkierungWaehlen(s, Eg.Id, 0, V(0, 1.7, 0), Blick);
        Assert.Equal(new Xz(0, -3.5), new Xz(Math.Round(m.X, 6), Math.Round(m.Z, 6)));   // 3.5 m vor dem Blick, im Raum R-1
        Nah(boden, 0, 1e-6);
        // südlich vor dem Raum nach Osten blickend liegt geradeaus kein Raum: die Markierung kommt 50° links daneben in den Raum
        var (m2, _) = P.MarkierungWaehlen(s, Eg.Id, 0, V(-1, 1.7, 1.5), 0);
        Nah(m2.X, -1 + Math.Cos(50 * Grad) * 3.5, 1e-9);
        Nah(m2.Z, 1.5 - Math.Sin(50 * Grad) * 3.5, 1e-9);
        // ohne Räume: 3.5 m geradeaus
        var (m3, _) = P.MarkierungWaehlen(null, null, 0, V(0, 1.7, 0), 0);
        Nah(m3.X, 3.5); Nah(m3.Z, 0);

        var l = Laufend(P.TeleportMarkierung);
        P.TeleportVorbereiten(l, m, boden);
        P.SprungAbgebrochen(l);                                                   // Pinch losgelassen ohne Ziel
        P.Sprung(l, 12, new Xz(0, -2.8));                                         // 0.7 m daneben
        Assert.Equal(2, l.Fehlgriffe);
        Assert.True(l.Laeuft);
        P.Sprung(l, 15, new Xz(0.3, -3.2));                                       // 0.42 m: in der Markierung
        Assert.Equal(LaufZustand.Erfolg, l.Zustand);
        Nah(l.ZeitMs(99), 5000);
        P.Sprung(l, 16, new Xz(9, 9));                                            // nach dem Ende zählt nichts mehr
        Assert.Equal(2, l.Fehlgriffe);

        // Messprotokoll: Felder und Grenzen wie XrNachrichten.Messprotokoll
        var o = P.Messprotokoll(l, 99, new Dictionary<string, string> { ["teleport"] = "zeigen_pinch" }, new() { ["begehen.ziel_ruhe_s"] = 0.25 });
        Assert.Equal("teleport-markierung", (string)o["aufgabe"]!);
        Assert.Equal(5000, (double)o["zeitMs"]!);
        Assert.Equal(2, (int)o["fehlgriffe"]!);
        Assert.Equal(0, (int)o["abbrueche"]!);
        Assert.True((bool)o["erfolg"]!);
        Assert.Equal("zeigen_pinch", (string)o["varianten"]!["teleport"]!);
        Assert.Equal(0.25, (double)o["stellschrauben"]!["begehen.ziel_ruhe_s"]!);
        Assert.Equal("Fehlgriffe: Sprung abgebrochen; Sprung daneben", (string)o["notiz"]!);
        using var d = JsonDocument.Parse(KanalText.Messprotokoll(o));
        Assert.Equal("messprotokoll", d.RootElement.GetProperty("typ").GetString());
        Assert.Equal(1, d.RootElement.GetProperty("fassung").GetInt32());
        Assert.Equal(5000, d.RootElement.GetProperty("zeitMs").GetDouble());
    }

    // ------------------------------------------------------------------------------------------------ drehen-90

    [Fact]
    public void Drehen_90_links_halten_zurueck_halten_Fehlgriff_bei_falscher_Richtung()
    {
        var l = Laufend(P.Drehen90, 0);
        P.DrehenVorbereiten(l, Blick);
        // Drehschritt nach rechts in Phase 0 (Ziel links): falsch
        P.Drehschritt(l, Blick, Blick - 30 * Grad);
        Assert.Equal(1, l.Fehlgriffe);
        P.Drehschritt(l, Blick - 30 * Grad, Blick);                               // zurück zum Ziel: kein Fehlgriff
        Assert.Equal(1, l.Fehlgriffe);
        // 80° (innerhalb ±15°) – erst nach 0.5 s gilt Phase 0
        P.DrehenSchritt(l, 1.0, Blick + 80 * Grad);
        P.DrehenSchritt(l, 1.3, Blick + 85 * Grad);
        Assert.Equal(0, l.Phase);
        P.DrehenSchritt(l, 1.4, Blick + 110 * Grad);                              // zu weit: Halten beginnt neu
        P.DrehenSchritt(l, 1.5, Blick + 95 * Grad);
        P.DrehenSchritt(l, 2.0, Blick + 92 * Grad);
        Assert.Equal(1, l.Phase);
        // Phase 1: zurück; ein Drehschritt weiter nach links ist falsch
        P.Drehschritt(l, Blick + 90 * Grad, Blick + 120 * Grad);
        Assert.Equal(2, l.Fehlgriffe);
        P.Sprung(l, 2.2, new Xz(1, 1));                                           // ein Sprung hier ist ungewollt
        Assert.Equal(3, l.Fehlgriffe);
        P.DrehenSchritt(l, 3.0, Blick + 10 * Grad);
        P.DrehenSchritt(l, 3.6, Blick - 5 * Grad);
        Assert.Equal(LaufZustand.Erfolg, l.Zustand);
        Nah(P.DrehungGrad(l, Blick + 350 * Grad), -10, 1e-9);                     // auf −180 … 180
        Assert.Equal(["Drehschritt in die falsche Richtung", "Drehschritt in die falsche Richtung", "ungewollter Sprung"], l.FehlgriffGruende);
    }

    // ------------------------------------------------------------------------------------------------ etage-wechseln

    [Fact]
    public void Etage_wechseln_darueber_und_zurueck_falsches_Geschoss_und_Wisch_ohne_Wirkung()
    {
        var alle = new List<ListenGeschoss> { Og, Eg, Ug };
        var l = Laufend(P.EtageWechseln);
        Assert.True(P.EtageVorbereiten(l, alle, Eg));
        Assert.Equal(Og, l.ZielGeschoss);
        Assert.Contains("1.OG", P.Zeile(l, 11));
        P.WischOhneWirkung(l);                                                    // Variante «App»: der Wisch wechselt nicht
        P.EtageGewechselt(l, 11, Ug);                                             // falsches Geschoss
        Assert.Equal(2, l.Fehlgriffe);
        P.EtageGewechselt(l, 12, Og);
        Assert.Equal(1, l.Phase);
        Assert.Contains("zurück ins EG", P.Zeile(l, 12));
        P.EtageGewechselt(l, 13, Eg);
        Assert.Equal(LaufZustand.Erfolg, l.Zustand);
        // im obersten Geschoss: darunter und zurück; nur ein Geschoss: geht nicht
        var o = Laufend(P.EtageWechseln);
        Assert.True(P.EtageVorbereiten(o, alle, Og));
        Assert.Equal(Eg, o.ZielGeschoss);
        var e = P.Starten(P.EtageWechseln, Def(P.EtageWechseln));
        Assert.False(P.EtageVorbereiten(e, [Eg], Eg));
        Assert.Contains("nur ein Geschoss", e.Grund);
    }

    // ------------------------------------------------------------------------------------------------ modell-greifen-zoomen

    [Fact]
    public void Modell_greifen_zoomen_Zielmassstab_Erfolg_erst_nach_dem_Loslassen()
    {
        var zoom = ZoomGrenzen.Vorgabe;   // 1:5 … 1:200
        Assert.Equal(25, P.ZielMassstabZu(50, zoom));
        Assert.Equal(75, P.ZielMassstabZu(137, zoom));
        Assert.Equal(10, P.ZielMassstabZu(5, zoom));                              // schon am stärksten Zoom: doppelt so klein
        Assert.Equal(50, P.ZielMassstabZu(100, new ZoomGrenzen(50, 200)));
        var l = Laufend(P.ModellGreifenZoomen);
        P.ZoomVorbereiten(l, new SitzungLage(50, 0.2, 0, 0), zoom);
        Assert.Equal(25, l.ZielMassstab);
        Assert.Contains("1:25", P.Zeile(l, 11));
        P.PinchOhneGriff(l);
        Assert.Equal(1, l.Fehlgriffe);
        var richtig = new SitzungLage(26, 0.2 + 88 * Grad, 0, 0);                 // 4 % daneben, 88°
        P.ZoomPruefen(l, 12, richtig, gehalten: true);                           // noch gehalten: zählt nicht
        Assert.True(l.Laeuft);
        P.ZoomPruefen(l, 12.5, new SitzungLage(30, 0.2 + 90 * Grad, 0, 0), false);   // 20 % daneben
        P.ZoomPruefen(l, 12.6, new SitzungLage(25, 0.2 + 60 * Grad, 0, 0), false);   // 60°
        Assert.True(l.Laeuft);
        P.ZoomPruefen(l, 13, richtig, false);
        Assert.Equal(LaufZustand.Erfolg, l.Zustand);
        // auch nach rechts gedreht
        var r = Laufend(P.ModellGreifenZoomen);
        P.ZoomVorbereiten(r, new SitzungLage(50, 0, 0, 0), zoom);
        P.ZoomPruefen(r, 11, new SitzungLage(24, -95 * Grad, 0, 0), false);
        Assert.Equal(LaufZustand.Erfolg, r.Zustand);
    }

    // ------------------------------------------------------------------------------------------------ raum-ar-oeffnen

    [Fact]
    public void Raum_in_AR_Abschnitte_und_ohne_Passthrough()
    {
        var l = Laufend(P.RaumArOeffnen);
        Assert.True(P.RaumVorbereiten(l, "R-1"));
        P.RaumPruefen(l, 11, false, true, false, false);
        Assert.Equal(0, l.Phase);
        P.RaumPruefen(l, 12, true, true, false, false);
        Assert.Equal(1, l.Phase);
        P.RaumRueckschritt(l, "Echter Tisch aus");
        P.RaumPruefen(l, 13, true, true, true, false);
        Assert.Equal(2, l.Phase);
        Assert.Contains("Tischplatte", P.Zeile(l, 13));
        P.RaumPruefen(l, 14, true, true, true, true);
        Assert.Equal(LaufZustand.Erfolg, l.Zustand);
        Assert.Equal(1, l.Fehlgriffe);
        // Simulator: Passthrough nicht verfügbar – der Ausschnitt genügt
        var s = Laufend(P.RaumArOeffnen);
        P.RaumVorbereiten(s, "R-1");
        P.RaumPruefen(s, 11, true, false, false, false);
        Assert.Equal(LaufZustand.Erfolg, s.Zustand);
        var ohne = P.Starten(P.RaumArOeffnen, Def(P.RaumArOeffnen));
        Assert.False(P.RaumVorbereiten(ohne, null));
    }

    // ------------------------------------------------------------------------------------------------ tablet-wert-ablesen

    [Fact]
    public void Tablet_Wert_ablesen_Frage_zum_markierten_Bauteil_falsche_und_richtige_Antwort()
    {
        var s = RaumSzene();
        var b = P.BauteilWaehlen(s, Eg.Id, V(0, 1.7, -4), Blick)!;                // 2 m vor dem Blick liegt die Wand W1
        Assert.Equal("modell:W1", b.Schluessel);
        Assert.Null(P.BauteilWaehlen(s, Ug.Id, V(0, 1.7, -4), Blick));
        var f = P.FrageZu(b);
        Nah(f.Soll, 2.6, 1e-9);
        Assert.Equal([2.4, 2.6, 2.8, 3.0], f.Optionen);
        Assert.Equal("wand", f.Art);
        var l = Laufend(P.TabletWertAblesen);
        P.TabletVorbereiten(l, f);
        P.TabletAntwort(l, 11, 2.8);
        Assert.Equal(1, l.Fehlgriffe);
        P.Daneben(l);                                                             // Antippen neben eine Taste
        P.TabletAntwort(l, 12, 2.6);
        Assert.Equal(LaufZustand.Erfolg, l.Zustand);
        Assert.Equal(2, l.Fehlgriffe);
        var o = P.Messprotokoll(l, 20, new Dictionary<string, string> { ["bedienung"] = "tablet" });
        Assert.Equal("Fehlgriffe: falsche Antwort; Antippen daneben", (string)o["notiz"]!);
        Assert.False(o.ContainsKey("stellschrauben"));
        // niedriges Bauteil (0.1 m): die tiefste Antwort bleibt positiv
        var niedrig = new ModellSzene([(Datei(null, Quadernetz("architektur", ("K", "wand", V(0, 0, 0), V(1, 0.1, 1)))), Eg.Id, "modell")]);
        var klein = P.FrageZu(niedrig.Elemente["modell:K"]);
        Assert.Equal([0.1, 0.3, 0.5, 0.7], klein.Optionen);
        var ohne = P.Starten(P.TabletWertAblesen, Def(P.TabletWertAblesen));
        P.TabletVorbereiten(ohne, null);
        Assert.Contains("Kein Bauteil", ohne.Grund);
    }

    [Fact]
    public void Raum_am_Standort_und_groesster_Raum_fuer_Raum_zeigen()
    {
        var s = new ModellSzene([(Datei(null,
            RaumNetz("R-1", [new(-2, -6), new(2, -6), new(2, 0), new(-2, 0)], 0, 2.6),
            RaumNetz("R-2", [new(3, -3), new(5, -3), new(5, 0), new(3, 0)], 0, 2.6)), Eg.Id, "modell"),
            (Datei(null, RaumNetz("R-OG", [new(-2, -6), new(2, -6), new(2, 0), new(-2, 0)], 2.75, 2.6)), Og.Id, "modell")]);
        Assert.Equal("R-1", RaumStandort.RaumBei(s, Eg.Id, new Xz(0, -3), 0));
        Assert.Equal("R-2", RaumStandort.RaumBei(s, Eg.Id, new Xz(4, -1), 0));
        Assert.Null(RaumStandort.RaumBei(s, Eg.Id, new Xz(10, 10), 0));
        Assert.Equal("R-OG", RaumStandort.RaumBei(s, null, new Xz(0, -3), 2.75));   // ohne Geschoss: der mit dem nächsten Boden
        Assert.Equal("R-1", RaumStandort.GroessterRaum(s, Eg.Id));                   // 24 m² gegen 6 m²
        Assert.Null(RaumStandort.GroessterRaum(s, Ug.Id));
    }

    // ------------------------------------------------------------------------------------------------ Karte

    [Fact]
    public void Karte_rechts_neben_dem_Blick_zum_Auge_gedreht_Knopf_unten()
    {
        var auge = V(0, 1.6, 0);
        var k = P.KarteLage(auge, Blick);
        Assert.True(k.Mitte.X > 0.2, "rechts neben dem Blick (die Apps stehen links)");
        Assert.True(k.Mitte.Z < -0.4, "vor dem Auge");
        Nah(k.Mitte.Y, 1.6 - P.KarteUnterAugeM, 1e-9);
        Assert.True(k.Normale.Skalar((auge - k.Mitte).Einheit()) > 0.999, "zum Auge gedreht");
        Nah(k.B, P.KarteB); Nah(k.H, P.KarteH);
        var gross = P.KarteLage(auge, Blick, 1.5);
        Nah(gross.B, P.KarteB * 1.5);
        var kn = P.KnopfLage(k);
        Assert.True(kn.Mitte.Y < k.Mitte.Y - k.H / 4, "Knopf unten auf der Karte");
        var lage = Hlks.Xr.Bedienung.AppBeruehrung.Lage(k, kn.Mitte);
        Assert.Equal(0, lage.Aussen);
        Assert.True(lage.Tiefe > 0, "Knopf liegt vor der Karte");
    }
}
