// Tests Quest App P4b (Modul XR, XR-H): was im Sitzungsraum auf dem Tisch steht – Geschosse mit Decken aus der Szene, Etagenschnitt (nur das gewählte Geschoss und die
// darunter, Schnitt 50 mm unter der Decke), «Alle Etagen», «Raum zeigen (VR)» (Raum ± 1.5 m wie «Raum im 3D zeigen»), Etage zu einem Auftrag.

using Hlks.Xr.Modell;
using Hlks.Xr.Netz;
using Hlks.Xr.Sitzung;
using static Hlks.Xr.Tests.T;
using S = Hlks.Xr.XrSitzungLogik;

namespace Hlks.Xr.Tests;

public class SitzungAuswahlTests
{
    static ListenTeil Teil(string id) => new("abcdef0123456789", $"/api/xr/geschoss/{id}.glb?teil=modell", null);

    /// <summary>Zwei Geschosse (EG Kote 0, OG Kote 3000) mit Bezugspunkt z = 500 mm: Wände, je eine Decke (KICAD im EG, IfcSlab in der Unterlage des OG), ein Raum im EG.</summary>
    static (ModellListe Liste, ModellSzene Szene) Haus(bool deckeEg = true, bool ausreisser = false)
    {
        double[] bezug = [1000, 2000, 500];
        var liste = new ModellListe("P-1", bezug,
        [
            new ListenGeschoss("g-eg", "EG", "Erdgeschoss", 0, 3000, Teil("g-eg"), null),
            new ListenGeschoss("g-og", "OG", "Obergeschoss", 3000, null, Teil("g-og"), null),
        ], "{}");
        // Szene relativ zum Bezugspunkt: EG-Boden bei y = −0.5, OG-Boden bei y = 2.5
        var eg = new List<(string, string?, Vec3, Vec3)> { ("w-eg", "wand", V(0, -0.5, -6), V(0.2, 2.5, 0)) };
        if (deckeEg) eg.Add(("d-eg", "decke", V(0, 2.25, -6), V(8, 2.5, 0)));   // Oberkante 3000 mm absolut, Dicke 250 mm
        var egNetz = ModellHilfen.Quadernetz("architektur", [.. eg]);
        var raum = ModellHilfen.RaumNetz("r-1", [new(1, -1), new(5, -1), new(5, -4), new(1, -4)], -0.5, 2.5);
        var ogNetz = ModellHilfen.Quadernetz("architektur", ("w-og", "wand", V(0, 2.5, -6), V(0.2, 5.5, 0)));
        var ogUnterlage = ModellHilfen.Quadernetz("unterlage", ("slab-og", "IfcSlab", V(0, 5.3, -6), V(8, 5.5, 0)));   // Oberkante 6000 mm
        var dateien = new List<(GlbDatei, string, string)>
        {
            (ModellHilfen.Datei(bezug, egNetz, raum), "g-eg", GlbZwischenspeicher.TeilModell),
            (ModellHilfen.Datei(bezug, ogNetz), "g-og", GlbZwischenspeicher.TeilModell),
            (ModellHilfen.Datei(bezug, ogUnterlage), "g-og", GlbZwischenspeicher.TeilUnterlage),
        };
        // Ausreisser wie in Test_praxis: ein Proxy im Projekt-Nullpunkt weit vor dem Gebäude und ein langes Geländer davor (beide IFC-Unterlage des EG)
        if (ausreisser)
            dateien.Add((ModellHilfen.Datei(bezug, ModellHilfen.Quadernetz("unterlage",
                ("proxy-null", "IfcBuildingElementProxy", V(-20, -0.5, 40), V(-19, 0.5, 41)),
                ("gelaender", "IfcRailing", V(2, -0.5, 0.2), V(6, 0.5, 15)))), "g-eg", GlbZwischenspeicher.TeilUnterlage));
        return (liste, new ModellSzene(dateien));
    }

    [Fact]
    public void Geschosse_mit_Decken_aus_dem_Modell_und_der_IFC_Unterlage()
    {
        var (liste, szene) = Haus();
        var g = SitzungAuswahl.Geschosse(liste, szene);
        Assert.Equal(["EG", "OG"], g.Select(x => x.Kennzeichen));
        // EG: Decke (KICAD) mit Oberkante 3000 und 250 mm Dicke
        Nah(g[0].DeckeOK, 3000, 1e-3);
        Nah(g[0].Dicke!.Value, 250, 1e-3);
        Nah(g[0].DeckeUK, 2750, 1e-3);
        Assert.False(g[0].OhneDecke);
        // OG: ohne Höhe in der Liste und ohne Geschoss darüber → Ersatzhöhe 3000; die IfcSlab-Decke liegt auf 6000 = Kote + Ersatzhöhe
        Nah(g[1].Hoehe, SitzungAuswahl.HoeheErsatzMm);
        Nah(g[1].DeckeOK, 6000, 1e-3);
        Nah(g[1].Dicke!.Value, 200, 1e-3);
    }

    [Fact]
    public void Etage_nur_das_Geschoss_und_die_darunter_Schnitt_50_mm_unter_der_Decke()
    {
        var (liste, szene) = Haus();
        var a = SitzungAuswahl.Etage(liste, szene, "EG")!;
        Assert.Equal(["g-eg"], a.Geschosse!.Order());
        Assert.Equal("g-eg", a.Geschoss);
        // Schnitt: Unterkante der Decke 2750 − 50 − Spiel 0.5 = 2699.5 mm absolut; Szene (Bezugspunkt z 500 mm): 2.1995 m
        Nah(a.Schnitt!.YMax, (2750 - S.SchnittTieferMm - S.SpielMm - 500) / 1000, 1e-9);
        Assert.Contains("EG", a.Text);
        Assert.Contains("unter der Decke", a.Text);
        // das OG (mit Id) zeigt beide Geschosse
        var og = SitzungAuswahl.Etage(liste, szene, "g-og")!;
        Assert.Equal(["g-eg", "g-og"], og.Geschosse!.Order());
        Assert.Null(SitzungAuswahl.Etage(liste, szene, "DG"));
    }

    [Fact]
    public void Etage_Lage_der_Bloecke_ueber_dem_Schnitt_weg_darunter_innen_dazwischen_mit_Schnitt()
    {
        var (liste, szene) = Haus();
        var a = SitzungAuswahl.Etage(liste, szene, "EG")!;
        Assert.Equal(SchnittLage.Aussen, a.Lage("g-og", new Box3(V(0, 2.5, -6), V(0.2, 5.5, 0))));   // anderes Geschoss
        Assert.Equal(SchnittLage.Innen, a.Lage("g-eg", new Box3(V(0, -0.5, -6), V(8, 1, 0))));
        Assert.Equal(SchnittLage.Schneidet, a.Lage("g-eg", new Box3(V(0, -0.5, -6), V(0.2, 2.5, 0))));   // Wand bis unter die Decke
        Assert.Equal(SchnittLage.Aussen, a.Lage("g-eg", new Box3(V(0, 2.25, -6), V(8, 2.5, 0))));   // die Decke selbst
    }

    [Fact]
    public void Etage_ohne_Decke_schneidet_unter_der_Kote_des_Geschosses_darueber()
    {
        var (liste, szene) = Haus(deckeEg: false);
        var a = SitzungAuswahl.Etage(liste, szene, "EG")!;
        Nah(a.Schnitt!.YMax, (3000 - S.SchnittTieferMm - S.SpielMm - 500) / 1000, 1e-9);
        Assert.Contains("Keine Decke", string.Join(" ", S.KopfZeilen(new SitzungLage(50, 0, 0, 0), new SitzungWahl("EG", false), SitzungAuswahl.Geschosse(liste, szene))));
    }

    [Fact]
    public void Alle_Etagen_kein_Schnitt_Bezug_aus_der_Huelle_ohne_Raeume_und_Etage_behaelt_den_Bezug()
    {
        var (liste, szene) = Haus();
        var alle = SitzungAuswahl.Alle(szene)!;
        Assert.Null(alle.Geschosse);
        Assert.Null(alle.Schnitt);
        Assert.Equal(new Xz(4, -3), alle.Bezug.Anker);
        Nah(alle.Bezug.Unten, -0.5);
        Assert.Equal(SchnittLage.Innen, alle.Lage("g-og", new Box3(V(0, 0, 0), V(1, 1, 1))));
        Assert.Equal(alle.Bezug, SitzungAuswahl.Etage(liste, szene, "EG")!.Bezug);   // gleicher Bezug: die Wahl einer Etage behält Lage und Massstab
        Assert.Equal(50, SitzungAuswahl.Startlage(alle).Lage.Massstab);
    }

    [Fact]
    public void QuaderAusRaum_Grundriss_plus_Rand_in_alle_Richtungen()
    {
        var ecken = new List<Vec3>();
        var netz = ModellHilfen.RaumNetz("r", [new(1, -1), new(5, -1), new(5, -4), new(1, -4)], -0.5, 2.5);
        for (int t = 0; t < netz.Dreiecke; t++) for (int k = 0; k < 3; k++) ecken.Add(netz.Ecke(netz.Indizes[3 * t + k]));
        var q = SitzungAuswahl.QuaderAusRaum(ecken)!;
        Assert.True(q.Aktiv);
        Assert.Equal(0, q.Winkel);
        // x 1…5 m, Nord (−z) 1…4 m, Höhe −0.5…2 m, je ± 1500 mm
        Assert.Equal(new double[] { 1000 - 1500, 1000 - 1500, -500 - 1500 }, q.Min!);
        Assert.Equal(new double[] { 5000 + 1500, 4000 + 1500, 2000 + 1500 }, q.Max!);
        Assert.Null(SitzungAuswahl.QuaderAusRaum([]));
    }

    [Fact]
    public void QuaderAusRaum_gedrehter_Raum_nimmt_die_Achsen_der_laengsten_Kante()
    {
        // Rechteck 6 × 2 m, um 20° gedreht (gegen den Uhrzeigersinn von oben in den Achsen des Modells)
        double w = 20 * Grad;
        Xz P(double u, double v) => new(u * Math.Cos(w) - v * Math.Sin(w), -(u * Math.Sin(w) + v * Math.Cos(w)));   // Modell (x, y=Nord) → Szene (x, −y)
        var netz = ModellHilfen.RaumNetz("r", [P(0, 0), P(6, 0), P(6, 2), P(0, 2)], 0, 2.5);
        var ecken = new List<Vec3>();
        for (int t = 0; t < netz.Dreiecke; t++) for (int k = 0; k < 3; k++) ecken.Add(netz.Ecke(netz.Indizes[3 * t + k]));
        var q = SitzungAuswahl.QuaderAusRaum(ecken, 0)!;
        Nah(q.Winkel!.Value, 20, 1e-3);
        Nah(q.Min![0], 0, 1); Nah(q.Max![0], 6000, 1);
        Nah(q.Min[1], 0, 1); Nah(q.Max[1], 2000, 1);
    }

    [Fact]
    public void Ausschnitt_Raum_plus_1_5_m_als_Quader_so_gross_wie_moeglich_eingepasst()
    {
        var (_, szene) = Haus();
        var a = SitzungAuswahl.Ausschnitt(szene, "r-1")!;
        Assert.True(a.Ausschnitt);
        Assert.Null(a.Geschosse);
        Assert.Equal("g-eg", a.Geschoss);
        Assert.Equal("Raum + 1.5 m", a.Text);
        Assert.True(a.Schnitt!.Enthaelt(V(3, 1, -2.5)));
        Assert.True(a.Schnitt.Enthaelt(V(-0.4, 1, -2.5)), "1.5 m Rand");
        Assert.False(a.Schnitt.Enthaelt(V(-0.6, 1, -2.5)));
        // Grösse 7 × 6 m: so gross wie möglich, passend auf die Platte 1.4 × 0.8 m → 6 m / 0.8 m = 7.5 → 1:8 (Grenze 1:5)
        Nah(a.Bezug.Groesse.B, 7, 1e-6); Nah(a.Bezug.Groesse.T, 6, 1e-6); Nah(a.Bezug.Groesse.H, 5.5, 1e-6);
        Assert.Equal(8, SitzungAuswahl.Startlage(a).Lage.Massstab);
        Assert.Null(SitzungAuswahl.Ausschnitt(szene, "w-eg"));   // kein Raum
        Assert.Null(SitzungAuswahl.Ausschnitt(szene, "fehlt"));
    }

    [Fact]
    public void Ausschnitt_mit_Liste_endet_oben_unter_der_Decke_wie_der_Etagenschnitt()
    {
        // Christian 23.10.2026: «bei der anzeige von räumen in VR soll nur bis zur unterkannte der Decke angezeigt werden. so dass in den Raum geschaut werden kann.»
        var (liste, szene) = Haus();
        var a = SitzungAuswahl.Ausschnitt(szene, "r-1", liste)!;
        var etage = SitzungAuswahl.Etage(liste, szene, "EG")!;
        Nah(a.Schnitt!.YMax, etage.Schnitt!.YMax, 1e-9);   // gleiche Höhe: UK Decke 2750 − 50 − Spiel, relativ zum Bezugspunkt
        Assert.False(a.Schnitt.Enthaelt(V(3, 2.3, -2.5)), "Decke (UK 2.25 m in der Szene) liegt über dem Schnitt");
        Assert.True(a.Schnitt.Enthaelt(V(3, 2.1, -2.5)), "der Raum darunter bleibt");
        Assert.True(a.Schnitt.Enthaelt(V(-0.4, 1, -2.5)), "Rand seitlich bleibt 1.5 m");
        Assert.Contains("unter der Decke", a.Text);
        Nah(a.Bezug.Groesse.H, (etage.Schnitt.YMax - (-0.5 - 1.5)), 1e-6);   // Höhe von UK Raum − Rand bis zum Schnitt
        // ohne Decke: Kote des Geschosses darüber − 50 mm
        var (l2, s2) = Haus(deckeEg: false);
        Nah(SitzungAuswahl.Ausschnitt(s2, "r-1", l2)!.Schnitt!.YMax, (3000 - S.SchnittTieferMm - S.SpielMm - 500) / 1000, 1e-9);
        // ohne Liste wie bisher: Raum + 1.5 m auch nach oben
        Assert.True(SitzungAuswahl.Ausschnitt(szene, "r-1")!.Schnitt!.Enthaelt(V(3, 2.3, -2.5)));
    }

    [Fact]
    public void EtageZumAuftrag_Geschoss_sonst_das_Geschoss_des_Raums_ausser_bei_Ausschnitt()
    {
        var (_, szene) = Haus();
        VrAuftrag A(string? raum = null, string? geschoss = null, bool ausschnitt = false, string modus = Modi.Sitzungsraum) => new(modus, raum, geschoss, ausschnitt, null, 1, null);
        Assert.Equal("g-og", SitzungAuswahl.EtageZumAuftrag(A(raum: "r-1", geschoss: "g-og"), szene));
        Assert.Equal("g-eg", SitzungAuswahl.EtageZumAuftrag(A(raum: "r-1"), szene));   // «Geschoss im Sitzungsraum zeigen»
        Assert.Null(SitzungAuswahl.EtageZumAuftrag(A(raum: "r-1", ausschnitt: true), szene));   // «Raum zeigen (VR)»
        Assert.Null(SitzungAuswahl.EtageZumAuftrag(A(), szene));   // alle Etagen
        Assert.Null(SitzungAuswahl.EtageZumAuftrag(A(raum: "r-1", modus: Modi.Begehen), szene));
        Assert.Null(SitzungAuswahl.EtageZumAuftrag(A(raum: "unbekannt"), szene));
    }

    // ------------------------------------------------------------------------------------------------ P4b-Nachtrag: Gebäudehülle, Griffe, Ausschnitt-Netze

    [Fact]
    public void Gebaeude_ohne_Ausreisser_Bezug_und_Griffe_am_Gebaeude_nicht_an_der_ganzen_Box()
    {
        // Brillentest Christian 23.10.2026: «griffe sind generell verschoben (weit vor dem Modell)» – die Box aller Netze reichte in Test_praxis 40 m vor das Gebäude
        var (liste, szene) = Haus(ausreisser: true);
        Assert.True(szene.Box.Max.Z > 40);   // die ganze Box mit Ausreissern
        var g = SitzungAuswahl.Gebaeude(szene, null);
        Nah(g.Min.X, 0); Nah(g.Max.X, 8); Nah(g.Min.Z, -6); Nah(g.Max.Z, 0);
        var alle = SitzungAuswahl.Alle(szene)!;
        Assert.Equal(new Xz(4, -3), alle.Bezug.Anker);
        var r = alle.Sichtbar!;
        Nah(r.Ux, 1); Nah(r.Uz, 0);   // Räume in den Achsen: keine Drehung
        Nah(r.UMin, 0); Nah(r.UMax, 8); Nah(r.VMin, -6); Nah(r.VMax, 0);
        Assert.Equal(alle.Bezug, SitzungAuswahl.Etage(liste, szene, "EG")!.Bezug);   // gleicher Bezug: die Wahl einer Etage behält die Lage
        // Etage EG: sichtbar sind die Bauteile des EG (Wand bei x 0 … 0.2 und Decke 0 … 8), nicht die des OG und nicht das Geländer
        var eg = SitzungAuswahl.Etage(liste, szene, "EG")!.Sichtbar!;
        Nah(eg.UMax, 8); Nah(eg.VMax, 0);
        // ohne Bauteile: die Hülle aller Netze ohne Räume
        var leer = new ModellSzene([(ModellHilfen.Datei([0, 0, 0], ModellHilfen.Quadernetz("hlks", ("rohr", "leitung", V(1, 0, 1), V(2, 0.125, 3)))), "g", GlbZwischenspeicher.TeilModell)]);
        Assert.Equal(new Box3(V(1, 0, 1), V(2, 0.125, 3)), SitzungAuswahl.Gebaeude(leer, null));
        Assert.Equal(new SichtbarRahmen(1, 0, 1, 2, 1, 3), SitzungAuswahl.GebaeudeRahmen(leer, null));
    }

    [Fact]
    public void Griffe_an_den_vorderen_Ecken_des_sichtbaren_Gebaeudes_und_sie_folgen_Lage_Massstab_Drehung_Hoehe()
    {
        var (_, szene) = Haus(ausreisser: true);
        var a = SitzungAuswahl.Alle(szene)!;
        var lage = SitzungAuswahl.Startlage(a).Lage;
        var g = SitzungRaum.GriffPunkte(lage, a.Bezug, a.Sichtbar);
        var abstand = S.GriffAbstandM / Math.Sqrt(2);
        // vorne links = (min x, max z), vorne rechts = (max x, max z) des Gebäudes, schräg nach aussen, 5 cm über der Platte
        var vl = SitzungRaum.ModellZuRaum(V(0, a.Bezug.Unten, 0), lage, a.Bezug);
        var vr = SitzungRaum.ModellZuRaum(V(8, a.Bezug.Unten, 0), lage, a.Bezug);
        Nah(g[0].X, vl.X - abstand, 1e-9); Nah(g[0].Z, vl.Z + abstand, 1e-9);
        Nah(g[1].X, vr.X + abstand, 1e-9); Nah(g[1].Z, vr.Z + abstand, 1e-9);
        Nah(g[0].Y, S.TischHoehe + S.GriffHoeheM, 1e-9);
        // die alte Regel (Box des Bezugs um die Modellmitte) gibt hier dieselben Ecken, weil das Gebäude in den Achsen steht und den Bezug bildet
        var alt = S.GriffPunkte(lage, a.Bezug.Groesse);
        Nah(alt[0].X, g[0].X, 1e-9); Nah(alt[1].Z, g[1].Z, 1e-9);
        // verschoben, gedreht, gezoomt und angehoben: die Griffe bleiben an den Ecken
        var l2 = new SitzungLage(25, 0.7, 0.1, -0.05, 0.2);
        var g2 = SitzungRaum.GriffPunkte(l2, a.Bezug, a.Sichtbar);
        var e0 = SitzungRaum.ModellZuRaum(V(0, a.Bezug.Unten, 0), l2, a.Bezug);
        var aussen = S.DreheXZ(new Xz(-abstand, abstand), 0.7);
        Nah(g2[0].X, e0.X + aussen.X, 1e-9); Nah(g2[0].Z, e0.Z + aussen.Z, 1e-9);
        Nah(g2[0].Y, S.TischHoehe + 0.2 + S.GriffHoeheM, 1e-9);
        // kleinerer sichtbarer Teil: Griffe an dessen Ecken, nicht an denen des Bezugs
        var g3 = SitzungRaum.GriffPunkte(lage, a.Bezug, new SichtbarRahmen(1, 0, 0, 0.2, -6, -1));
        var hinten = SitzungRaum.ModellZuRaum(V(0, a.Bezug.Unten, -1), lage, a.Bezug);
        Nah(g3[0].Z, hinten.Z + abstand, 1e-9);
        // winziges Gebäude: die Griffe rücken auf 2 × GriffMinHalbM (plus Abstand) auseinander; ohne Rechteck die alte Regel
        var w = SitzungRaum.GriffPunkte(lage, a.Bezug, new SichtbarRahmen(1, 0, 4, 4.1, -3, -2.9));
        Assert.True(Math.Abs(w[1].X - w[0].X) >= 2 * S.GriffMinHalbM);
        Assert.Equal(S.GriffPunkte(lage, a.Bezug.Groesse), SitzungRaum.GriffPunkte(lage, a.Bezug, null));
    }

    /// <summary>Netz eines Bauteils (Ebene «wand») als Prisma über einem Grundriss (Szene).</summary>
    static GlbNetzDaten Wand(string id, IReadOnlyList<Xz> ring, double boden, double hoehe, string art = "wand")
    {
        var p = ModellHilfen.RaumNetz(id, ring, boden, hoehe);
        return new GlbNetzDaten { Name = id, Positionen = p.Positionen, Normalen = p.Normalen, Indizes = p.Indizes, Gruppe = "architektur", Bereiche = [new ElementBereich(id, 0, p.Dreiecke, art)] };
    }

    [Fact]
    public void Gedrehtes_Gebaeude_Griffe_an_den_echten_Ecken_nicht_an_der_achsparallelen_Box()
    {
        // Test_praxis steht um etwa 43° gedreht im Plan: die vorderen Ecken einer achsparallelen Box lagen im Leeren vor dem Gebäude (Prüfbild senkrecht, 23.10.2026)
        double w = 30 * Grad;
        Xz P(double u, double v) => new(u * Math.Cos(w) - v * Math.Sin(w), u * Math.Sin(w) + v * Math.Cos(w));
        var aussenwand = Wand("aussen", [P(0, 0), P(10, 0), P(10, 10), P(0, 10)], 0, 3);
        var decke = Wand("decke", [P(0, 0), P(10, 0), P(10, 10), P(0, 10)], 2.75, 0.25, "decke");
        // eine freistehende Wand ragt 8 m aus dem Gebäude (wie in Test_praxis): sie zählt für die Griffe nicht, weil das Gebäude Platten hat
        var frei = Wand("frei", [P(-8, 5), P(0, 5), P(0, 5.2), P(-8, 5.2)], 0, 2);
        var raum = ModellHilfen.RaumNetz("r", [P(1, 1), P(9, 1), P(9, 9), P(1, 9)], 0, 2.6);
        var szene = new ModellSzene([(ModellHilfen.Datei([0, 0, 0], aussenwand, decke, frei, raum), "g", GlbZwischenspeicher.TeilModell)]);
        var ax = SitzungAuswahl.Ausrichtung(szene);
        Nah(Math.Atan2(ax.Z, ax.X), w, 1e-6);
        var a = SitzungAuswahl.Alle(szene)!;
        var r = a.Sichtbar!;
        Nah(r.UMin, 0, 1e-5); Nah(r.UMax, 10, 1e-5); Nah(r.VMin, 0, 1e-5); Nah(r.VMax, 10, 1e-5);
        // Vorderseite: v = 10 (Aussenrichtung (−sin 30°, cos 30°) zeigt am meisten nach +z); links die Ecke mit kleinerem x
        var (e0, e1, n) = r.Vorderseite();
        Nah(Math.Min(e0.X, e1.X), P(0, 10).X, 1e-5);
        Nah(n.X, -Math.Sin(w), 1e-6); Nah(n.Z, Math.Cos(w), 1e-6);   // Positionen der GLB sind float
        var lage = SitzungAuswahl.Startlage(a).Lage;
        var g = SitzungRaum.GriffPunkte(lage, a.Bezug, r);
        var ecke = SitzungRaum.ModellZuRaum(new Vec3(P(0, 10).X, a.Bezug.Unten, P(0, 10).Z), lage, a.Bezug);
        Nah(a.Bezug.Unten, 0, 1e-6);   // Unterkante aus allen Bauteilen, nicht nur aus der Decke
        var box = SitzungRaum.ModellZuRaum(new Vec3(szene.Box.Min.X, a.Bezug.Unten, szene.Box.Max.Z), lage, a.Bezug);   // Ecke der achsparallelen Box
        var d = Math.Sqrt((g[0].X - ecke.X) * (g[0].X - ecke.X) + (g[0].Z - ecke.Z) * (g[0].Z - ecke.Z));
        Nah(d, S.GriffAbstandM, 1e-6);   // genau GriffAbstandM neben der echten Ecke
        Assert.True(Math.Sqrt((g[0].X - box.X) * (g[0].X - box.X) + (g[0].Z - box.Z) * (g[0].Z - box.Z)) > 0.04, "nicht an der Ecke der achsparallelen Box");
        // die Griffe liegen ausserhalb des Gebäudes vor seiner Vorderseite
        var mitte = SitzungRaum.ModellZuRaum(new Vec3(P(5, 5).X, a.Bezug.Unten, P(5, 5).Z), lage, a.Bezug);
        Assert.All(g, x => Assert.True(x.Z > mitte.Z));
    }

    [Fact]
    public void Ausschnitt_sichtbar_ist_das_Rechteck_der_Elemente_im_Quader_begrenzt_auf_den_Quader()
    {
        var (liste, szene) = Haus(ausreisser: true);
        var a = SitzungAuswahl.Ausschnitt(szene, "r-1", liste)!;
        var s = a.Sichtbar!;
        Nah(s.Ux, 1); Nah(Math.Abs(s.Uz), 0);
        Nah(s.UMin, 0);       // Wand bei x = 0
        Nah(s.UMax, 6);       // Geländer bis x = 6
        Nah(s.VMin, -5.5);    // Wand bis z = −6, am Quader (Raum −4 − 1.5) begrenzt
        Nah(s.VMax, 0.5);     // Geländer bis z = 15, am Quader (−1 + 1.5) begrenzt
        // ohne Elemente im Quader: der Quader selbst
        var nur = new ModellSzene([(ModellHilfen.Datei([0, 0, 0], ModellHilfen.RaumNetz("r", [new(0, 0), new(2, 0), new(2, -1), new(0, -1)], 0, 2.5)), "g", GlbZwischenspeicher.TeilModell)]);
        var b = SitzungAuswahl.Ausschnitt(nur, "r")!.Sichtbar!;
        Nah(b.UMin, -1.5); Nah(b.UMax, 3.5); Nah(b.VMin, -2.5); Nah(b.VMax, 1.5);
    }
    static List<Vec3> Ecken(ModellSzene s, string id) => [.. s.Ecken(s.Finde(id)!)];

    [Fact]
    public void Ausschnitt_Netze_nur_Elemente_im_Quader_Ecken_und_Normalen_wie_im_Ausgangsnetz_endlich_und_normiert()
    {
        // P4b-Nachtrag «Raum in AR schwarz»: die zusammengestellten Netze müssen dieselben Ecken und Normalen tragen wie die GLB (die Ursache war der Shader, siehe
        // ShaderTests); die Normalen hier absichtlich verschieden je Ecke
        var netz = ModellHilfen.Quadernetz("unterlage", ("innen", "IfcWall", V(0, 0, -2), V(1, 2, -1)), ("aussen", "IfcWall", V(30, 0, 30), V(31, 2, 31)));
        var nor = new float[netz.Positionen.Length];
        for (int i = 0; i < nor.Length; i += 3)
        {
            var n = new Vec3(netz.Positionen[i] - 0.5, netz.Positionen[i + 1] - 1, netz.Positionen[i + 2] + 1.5).Einheit();
            nor[i] = (float)n.X; nor[i + 1] = (float)n.Y; nor[i + 2] = (float)n.Z;
        }
        var mit = new GlbNetzDaten { Name = netz.Name, Positionen = netz.Positionen, Normalen = nor, Indizes = netz.Indizes, Gruppe = netz.Gruppe, Bereiche = netz.Bereiche };
        var raum = ModellHilfen.RaumNetz("r", [new(0.2, -1.8), new(0.8, -1.8), new(0.8, -1.2), new(0.2, -1.2)], 0, 2);
        var szene = new ModellSzene([(ModellHilfen.Datei([0, 0, 0], mit, raum), "g", GlbZwischenspeicher.TeilUnterlage)]);
        var a = SitzungAuswahl.Ausschnitt(szene, "r")!;
        var je = SitzungAuswahl.AusschnittIndizes(szene, a.Schnitt!);
        var ind = Assert.Single(je).Value;   // nur das Netz der Wände – der Raum selbst wird nicht gezeichnet
        Assert.Equal(12 * 3, ind.Length);    // nur der Quader «innen»
        var (pos, nr, i2) = NetzDurchgang.Ausschnitt(mit.Positionen, mit.Normalen, ind);
        Assert.Equal(ind.Length, i2.Length);
        for (int k = 0; k < ind.Length; k++)
            for (int c = 0; c < 3; c++)
            {
                Assert.Equal(mit.Positionen[3 * ind[k] + c], pos[3 * i2[k] + c]);
                Assert.Equal(mit.Normalen[3 * ind[k] + c], nr[3 * i2[k] + c]);
            }
        for (int i = 0; i < nr.Length; i += 3)
        {
            Assert.True(float.IsFinite(nr[i]) && float.IsFinite(nr[i + 1]) && float.IsFinite(nr[i + 2]));
            Nah(Math.Sqrt(nr[i] * nr[i] + nr[i + 1] * nr[i + 1] + nr[i + 2] * nr[i + 2]), 1, 1e-6);
        }
    }
}