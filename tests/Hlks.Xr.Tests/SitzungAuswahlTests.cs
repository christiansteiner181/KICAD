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
    static (ModellListe Liste, ModellSzene Szene) Haus(bool deckeEg = true)
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
        var szene = new ModellSzene(
        [
            (ModellHilfen.Datei(bezug, egNetz, raum), "g-eg", GlbZwischenspeicher.TeilModell),
            (ModellHilfen.Datei(bezug, ogNetz), "g-og", GlbZwischenspeicher.TeilModell),
            (ModellHilfen.Datei(bezug, ogUnterlage), "g-og", GlbZwischenspeicher.TeilUnterlage),
        ]);
        return (liste, szene);
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
        // Grösse 7 × 6 m: 1:20 wäre 0.35 × 0.3 m – passt auf die Platte
        Nah(a.Bezug.Groesse.B, 7, 1e-6); Nah(a.Bezug.Groesse.T, 6, 1e-6); Nah(a.Bezug.Groesse.H, 5.5, 1e-6);
        Assert.Equal(S.MassstabMin, SitzungAuswahl.Startlage(a).Lage.Massstab);
        Assert.Null(SitzungAuswahl.Ausschnitt(szene, "w-eg"));   // kein Raum
        Assert.Null(SitzungAuswahl.Ausschnitt(szene, "fehlt"));
    }

    [Fact]
    public void EtageZumAuftrag_Geschoss_sonst_das_Geschoss_des_Raums_ausser_bei_Ausschnitt()
    {
        var (_, szene) = Haus();
        VrAuftrag A(string? raum = null, string? geschoss = null, bool ausschnitt = false, string modus = Modi.Sitzungsraum) => new(modus, raum, geschoss, ausschnitt, null, 1, null);
        Assert.Equal("g-og", SitzungAuswahl.EtageZumAuftrag(A(raum: "r-1", geschoss: "g-og"), szene));
        Assert.Equal("g-eg", SitzungAuswahl.EtageZumAuftrag(A(raum: "r-1"), szene));   // «Im Sitzungsraum zeigen»
        Assert.Null(SitzungAuswahl.EtageZumAuftrag(A(raum: "r-1", ausschnitt: true), szene));   // «Raum zeigen (VR)»
        Assert.Null(SitzungAuswahl.EtageZumAuftrag(A(), szene));   // alle Etagen
        Assert.Null(SitzungAuswahl.EtageZumAuftrag(A(raum: "r-1", modus: Modi.Begehen), szene));
        Assert.Null(SitzungAuswahl.EtageZumAuftrag(A(raum: "unbekannt"), szene));
    }
}
