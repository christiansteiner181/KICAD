// Tests von Modul XR, Teil XR-C (Tablet an der linken Hand), übertragen aus xr-b.test.mjs: Greif- und Pinch-Erkennung aus Gelenk-Attrappen, Tablet-Geste, Lage und
// Treffer auf dem Tablet, Tablet-Inhalt. Nicht übertragen: Reichweite, Querverschiebung und Befehlsmasse (XR-B, Greifen selbst).

using static Hlks.Xr.Tests.T;
using G = Hlks.Xr.XrTabletLogik;

namespace Hlks.Xr.Tests;

public class XrTabletLogikTests
{
    // ------------------------------------------------------------------------------------------------ Gelenk-Attrappen

    /// <summary>
    /// Linke bzw. rechte Hand mit der Handfläche nach oben (+y), Finger nach vorn (−z), Handgelenk bei ort. offen: Finger gestreckt, sonst Faust.
    /// spiegeln: Handfläche nach unten.
    /// </summary>
    static Dictionary<string, Vec3> Hand(string seite = "left", bool offen = true, Vec3? ort = null, bool spiegeln = false, bool pinch = false)
    {
        var o = ort ?? new Vec3(0, 1, 0);
        double s = seite == "left" ? -1 : 1;   // Daumen- bzw. Zeigefingerseite: linke Hand mit Handfläche oben: Zeigefinger bei −x
        double sp = spiegeln ? -1 : 1;
        Vec3 P(double x, double y, double z) => new(o.X + sp * x * 1, o.Y + sp * y, o.Z + z);
        var g = new Dictionary<string, Vec3>
        {
            ["wrist"] = P(0, 0, 0),
            ["middle-finger-phalanx-proximal"] = P(0, 0, -0.09),
            ["index-finger-metacarpal"] = P(0.02 * s, 0, -0.03),
            ["pinky-finger-metacarpal"] = P(-0.02 * s, 0, -0.03),
            ["thumb-tip"] = P(0.07 * s, 0, -0.06),
        };
        var tipX = new Dictionary<string, double>
        {
            ["index-finger-tip"] = 0.03 * s, ["middle-finger-tip"] = 0.005 * s, ["ring-finger-tip"] = -0.02 * s, ["pinky-finger-tip"] = -0.04 * s,
        };
        foreach (var (n, x) in tipX) g[n] = offen ? P(x, 0, -0.18) : P(x * 0.5, 0.045, -0.06);   // Faust: Spitzen zur Handfläche gekrümmt
        if (pinch) g["thumb-tip"] = P(tipX["index-finger-tip"] + 0.01 * s, 0.0, offen ? -0.18 : -0.06);
        return g;
    }

    [Fact]
    public void Greif_Erkennung_offene_Hand_1_9_Handgroessen_Faust_1_0_mit_Hysterese()
    {
        var o = Hand(offen: true);
        var f = Hand(offen: false);
        var vo = G.FaustVerhaeltnis(o)!.Value;
        var vf = G.FaustVerhaeltnis(f)!.Value;
        Assert.True(vo > 1.8 && vo < 2.3, $"offen {vo}");
        Assert.True(vf > 0.7 && vf < 1.2, $"Faust {vf}");
        Assert.False(G.IstFaust(o));
        Assert.True(G.IstFaust(f));
        // Hysterese: zwischen FAUST_AN und FAUST_LOS bleibt der Zustand
        var mitte = Hand(offen: false);
        foreach (var n in new[] { "index-finger-tip", "middle-finger-tip", "ring-finger-tip", "pinky-finger-tip" })
            mitte[n] = new Vec3(mitte[n].X, mitte[n].Y, mitte[n].Z - 0.05);
        var vm = G.FaustVerhaeltnis(mitte)!.Value;
        Assert.True(vm > G.FaustAn && vm < G.FaustLos, $"Mitte {vm}");
        Assert.False(G.IstFaust(mitte, false), "noch offen: greift nicht");
        Assert.True(G.IstFaust(mitte, true), "war gegriffen: hält");
        Assert.False(G.IstFaust(new Dictionary<string, Vec3>(), false), "unvollständige Gelenke: kein Griff");
        Assert.False(G.IstFaust(null));
        Assert.Null(G.FaustVerhaeltnis(new Dictionary<string, Vec3> { ["wrist"] = Vec3.Null }));
        Assert.True(G.IstOffen(o));
        Assert.False(G.IstOffen(f));
        Assert.False(G.IstOffen(null));
        Assert.True(G.HandGroesse(o) > 0.05);
        // nicht endliche Gelenke zählen als fehlend
        var kaputt = Hand();
        kaputt["wrist"] = new Vec3(double.NaN, 0, 0);
        Assert.False(G.GelenkeVollstaendig(kaputt));
        Assert.True(G.GelenkeVollstaendig(o));
    }

    [Fact]
    public void Pinch_Daumen_und_Zeigefingerspitze_unter_2_5_cm_los_ab_4_cm()
    {
        Assert.False(G.IstPinch(Hand(offen: true), false));
        Assert.True(G.IstPinch(Hand(offen: true, pinch: true), false));
        var g = Hand(offen: true);
        g["thumb-tip"] = new Vec3(g["index-finger-tip"].X + 0.03, g["index-finger-tip"].Y, g["index-finger-tip"].Z);
        Assert.False(G.IstPinch(g, false));
        Assert.True(G.IstPinch(g, true), "zwischen 2.5 und 4 cm: Hysterese");
        Assert.False(G.IstPinch(new Dictionary<string, Vec3>(), true));
        Assert.False(G.IstPinch(null));
    }

    // ------------------------------------------------------------------------------------------------ Tablet

    [Fact]
    public void Tablet_Geste_offene_linke_Hand_Handflaeche_zum_Kopf_Wegdrehen_und_Faust_schliessen()
    {
        var kopf = new Vec3(0, 1.7, 0);
        var hoch = Hand("left", true, new Vec3(0, 1.1, -0.2));
        Nah(G.HandflaechenNormale(hoch, "left")!.Value.Y, 1, 1e-9, "Normale der linken Hand (Handfläche oben) zeigt nach oben");
        Assert.True(G.PalmWinkelZumKopf(hoch, "left", kopf) < 25);
        Assert.True(G.TabletGeste(hoch, "left", kopf, false), "Handfläche zum Kopf: öffnet");
        var weg = Hand("left", true, new Vec3(0, 1.1, -0.2), spiegeln: true);
        Assert.True(G.PalmWinkelZumKopf(weg, "left", kopf) > 150);
        Assert.False(G.TabletGeste(weg, "left", kopf, false), "weggedreht: öffnet nicht");
        Assert.False(G.TabletGeste(weg, "left", kopf, true), "weggedreht: schliesst");
        Assert.False(G.TabletGeste(Hand("left", false, new Vec3(0, 1.1, -0.2)), "left", kopf, true), "Faust schliesst");
        Assert.False(G.TabletGeste(hoch, "left", null, false), "ohne Kopf keine Geste");
        // rechte Hand spiegelbildlich: gleiche Aussage
        var r = Hand("right", true, new Vec3(0.3, 1.1, -0.2));
        Nah(G.HandflaechenNormale(r, "right")!.Value.Y, 1, 1e-9);
        // Hysterese beim Wegdrehen: 70° neben dem Kopf – offen noch gehalten, aber nicht neu geöffnet
        var schief = Hand("left", true, new Vec3(0, 1.1, -0.2));
        var k70 = new Vec3(Math.Sin(70 * Grad) * 0.6, 1.1 + Math.Cos(70 * Grad) * 0.6, -0.2);
        Assert.False(G.TabletGeste(schief, "left", k70, false));
        Assert.True(G.TabletGeste(schief, "left", k70, true));
        Assert.NotNull(G.HandflaechenMitte(hoch));
        Assert.Null(G.HandflaechenMitte(null));
        Assert.Null(G.HandflaechenNormale(new Dictionary<string, Vec3>(), "left"));
    }

    [Fact]
    public void Tablet_Treffer_Lage_ueber_der_Hand_Fingerspitze_und_Strahl_liefern_u_v_und_die_Taste()
    {
        var kopf = new Vec3(0, 1.7, 1);
        var lage = G.TabletLage(new Vec3(0, 1.1, 0), new Vec3(0, 1, 0), kopf);
        var erwartet = new Vec3(0, 1.1, 0) + new Vec3(0, 1, 0) * G.TabletAbstand + lage.Oben * G.TabletVersatzOben;
        Nah(lage.Mitte.Abstand(erwartet), 0, 1e-9, "TABLET_ABSTAND über der Handfläche, TABLET_VERSATZ_OBEN nach oben");
        Assert.True(G.TabletAbstand >= 0.02 && G.TabletAbstand <= 0.03, "auf Handdistanz: 2–3 cm");
        Nah(Math.Sqrt(G.TabletB * G.TabletB + G.TabletH * G.TabletH), 0.381, 1e-3, "15 Zoll Diagonale");
        Nah(G.TabletB / G.TabletH, 0.75, 1e-3, "3:4 hochkant");
        Assert.True(G.TabletH > G.TabletB, "Hochformat");
        Nah(G.TabletPxB / G.TabletPxH, G.TabletB / G.TabletH, 1e-9, "Textur im Seitenverhältnis");
        Assert.Equal(0.2286, G.TabletB);
        Assert.Equal(0.3048, G.TabletH);
        Nah(lage.Normale.Laenge, 1, 1e-9);
        Assert.True(lage.Normale.Z > 0.5, "dem Kopf zugewandt");
        Nah(lage.Rechts.Skalar(lage.Oben), 0, 1e-9, "Rechts ⟂ Oben");
        // Die Mitte der Fläche liegt bei (0.5, 0.5)
        var k = G.TabletKoordinaten(lage, lage.Mitte);
        Nah(k.U, 0.5, 1e-9);
        Nah(k.V, 0.5, 1e-9);
        Assert.True(k.Innen);
        // Ein Punkt rechts oben (vom Kopf aus gesehen)
        var ro = lage.Mitte + (lage.Rechts * (G.TabletB * 0.25) + lage.Oben * (G.TabletH * 0.25));
        var kr = G.TabletKoordinaten(lage, ro);
        Nah(kr.U, 0.75, 1e-9);
        Nah(kr.V, 0.25, 1e-9);
        // Jede Taste trifft sich selbst in ihrer Mitte
        var layout = G.TabletLayout();
        Assert.Equal(13, layout.Count);
        foreach (var t in layout) Assert.Equal(t.Id, G.TasteBei((t.X + t.W / 2) / G.TabletPxB, (t.Y + t.H / 2) / G.TabletPxH, layout)?.Id);
        Assert.Null(G.TasteBei(0.5, 0.05, layout));   // im Kopf liegt keine Taste
        Assert.Equal("rueckgaengig", G.TasteBei(0.15, 0.25)?.Id);   // ohne Layout das des Begehens
        // Fingerspitze: 1 cm vor der Fläche berührt, 10 cm davor nicht, ausserhalb nicht
        var taste = layout.First(t => t.Id == "rueckgaengig");
        var u = (taste.X + taste.W / 2) / G.TabletPxB;
        var vv = (taste.Y + taste.H / 2) / G.TabletPxH;
        Vec3 Stelle(double tiefe) => lage.Mitte + ((lage.Rechts * ((u - 0.5) * G.TabletB) + lage.Oben * ((0.5 - vv) * G.TabletH)) + lage.Normale * tiefe);
        var b = G.TabletBeruehrung(lage, Stelle(0.01));
        Assert.NotNull(b);
        Assert.Equal("rueckgaengig", G.TasteBei(b!.Value.U, b.Value.V)?.Id);
        Assert.Null(G.TabletBeruehrung(lage, Stelle(0.10)));   // 10 cm davor: keine Berührung
        Assert.Null(G.TabletBeruehrung(lage, Stelle(-0.08)));   // weit dahinter: keine Berührung
        Assert.Null(G.TabletBeruehrung(lage, Stelle(0.01) + lage.Rechts * 0.4));   // ausserhalb des Rechtecks
        // Strahl vom Kopf auf die Taste
        var ziel = Stelle(0);
        var r = (ziel - kopf).Einheit();
        var h = G.TabletStrahl(lage, kopf, r);
        Assert.NotNull(h);
        Nah(h!.Value.U, u, 1e-9);
        Nah(h.Value.V, vv, 1e-9);
        Assert.Equal("rueckgaengig", G.TasteBei(h.Value.U, h.Value.V)?.Id);
        Assert.Null(G.TabletStrahl(lage, kopf, r * -1));   // Strahl nach hinten trifft nicht
        Assert.Null(G.TabletStrahl(lage, kopf, lage.Rechts));   // parallel zur Fläche
    }

    [Fact]
    public void Tablet_Lage_aus_Normale_ohne_Querrichtung_senkrecht_nach_oben_bleibt_gueltig()
    {
        var l = G.TabletLageAus(Vec3.Null, new Vec3(0, 1, 0));   // Normale parallel zur Senkrechten: Ersatz «rechts» = x
        Nah(l.Rechts.X, 1, 1e-12);
        Nah(l.Oben.Laenge, 1, 1e-9);
    }

    [Fact]
    public void Tablet_Inhalt_Platzhalter_folgt_Umschalter_Eigenschaften_lesend_Aufbau_und_Aufleuchten()
    {
        var t = G.TabletLayout();
        foreach (var id in new[] { "familie", "problem" }) Assert.True(t.First(x => x.Id == id).Platzhalter, id);
        foreach (var id in new[] { "rueckgaengig", "wiederholen", "hoeher", "tiefer", "schnitt", "ifc", "messen", "messungenLoeschen", "lot", "ende", "raum" })
            Assert.False(t.First(x => x.Id == id).Platzhalter, id);
        foreach (var id in new[] { "messen", "lot", "schnitt", "ifc" }) Assert.True(t.First(x => x.Id == id).Umschalter, id);
        Gleich(["Kein Element gewählt"], G.EigenschaftenZeilen(null));
        var z = G.EigenschaftenZeilen(new ElementInfo("RO-1", "Rohr", "KW-1", "EG", null,
            [new("dn", 20), new("material", "Stahl"), new("geometrie", "x"), new("leer", ""), new("ok", true)]));
        Assert.Equal("RO-1 · Rohr", z[0]);
        Assert.Contains("System KW-1", z[1]);
        Assert.Contains("dn 20", z[2]);
        Assert.DoesNotContain("geometrie", z[2]);
        Assert.Equal("dn 20 · material Stahl · ok true", z[2]);
        Gleich(["RO-2 · Rohr"], G.EigenschaftenZeilen(new ElementInfo("RO-2", "Rohr", null, null, null), 3));
        Assert.Equal("Länge 1.57", G.EigenschaftenZeilen(new ElementInfo("A", "B", null, null, null, [new("Länge", 1.5678)]))[1]);
        Assert.Equal(2, G.EigenschaftenZeilen(new ElementInfo("RO-1", "Rohr", "KW-1", "EG", "R-1", [new("dn", 20)]), 2).Count);
        Assert.Equal(0, G.AufbauAnteil(0));
        Assert.Equal(1, G.AufbauAnteil(G.AufbauDauer));
        Assert.True(G.AufbauAnteil(0.1) > 0.2 && G.AufbauAnteil(0.1) < 1.1);
        Assert.Equal(1, G.Aufleuchten(0));
        Assert.Equal(0, G.Aufleuchten(G.AufleuchtDauer));
        Assert.True(G.Aufleuchten(0.1) > 0 && G.Aufleuchten(0.1) < 1);
    }
}
