// Tests der Regel «Wände nur unter 500 mm durchscheinend» (N22), übertragen aus durchgang.test.mjs (Abstandsrechnung), vr.test.mjs (N22 mit der Kopfposition)
// und begehen.test.mjs (Zustand aus dem Standort, Einblendung). Das Umschalten der three-Materialien (durchgang3d.js) ist Sache der App.

using static Hlks.Xr.Tests.T;

namespace Hlks.Xr.Tests;

public class DurchgangTests
{
    /// <summary>Wand 4 m lang, 200 mm dick, 0 … 2.5 m hoch, entlang x bei y = 0 … 200 (Projekt-mm) → Szene: z von 0 bis −0.2.</summary>
    static readonly KoerperTeil Wand = new() { Art = "prisma", Grundriss = [[0, 0], [4000, 0], [4000, 200], [0, 200]], Unten = 0, Oben = 2500, Material = "wand" };
    /// <summary>Diagonale Wand von (0,0) nach (4000,4000), 200 mm dick.</summary>
    static readonly KoerperTeil Schraeg = new() { Art = "prisma", Grundriss = [[0, 0], [4000, 4000], [3858.6, 4141.4], [-141.4, 141.4]], Unten = 0, Oben = 2500, Material = "wand" };

    static Form F(KoerperTeil t) => Durchgang.FormAusTeil(t)!;

    // ---------------------------------------------------------------------------------------------- Konstanten

    [Fact]
    public void Konstante_500_mm_Christian_21_10_2026_Deckkraft_im_Durchgang()
    {
        Assert.Equal(500, Durchgang.DurchgangMm);
        Assert.Equal(500, VrLogik.DurchgangMm);
        Assert.Equal(0.28, Durchgang.DurchgangDeckkraft(0.28));   // bisherige durchscheinende Deckkraft bleibt
        Assert.Equal(Durchgang.DurchgangDeckkraftStandard, Durchgang.DurchgangDeckkraft(1));   // undurchsichtige Decke: Vorgabe
        Assert.Equal(Durchgang.DurchgangDeckkraftStandard, Durchgang.DurchgangDeckkraft(null));
        Assert.Equal(0.3, Durchgang.DurchgangDeckkraftStandard);
        Gleich(["wand", "decke"], Durchgang.DurchgangEbenen.ToArray());
    }

    // ---------------------------------------------------------------------------------------------- Abstand

    [Fact]
    public void Abstand_Punkt_Prisma_innen_0_seitlich_ueber_unter_Ecke()
    {
        var f = F(Wand);
        Nah(Durchgang.AbstandZurForm(V(2, 1.2, -0.1), f), 0, 1e-9, "innen");
        Nah(Durchgang.AbstandZurForm(V(2, 1.2, 0.3), f), 0.3, 1e-9, "seitlich vorn");
        Nah(Durchgang.AbstandZurForm(V(2, 1.2, -0.5), f), 0.3, 1e-9, "seitlich hinten (z = −0.2 Rückseite)");
        Nah(Durchgang.AbstandZurForm(V(2, 3.0, -0.1), f), 0.5, 1e-9, "über OK");
        Nah(Durchgang.AbstandZurForm(V(2, -0.4, -0.1), f), 0.4, 1e-9, "unter UK");
        Nah(Durchgang.AbstandZurForm(V(-0.3, 1.2, 0.4), f), 0.5, 1e-9, "Grundrissecke (Pythagoras 0.3/0.4)");
        Nah(Durchgang.AbstandZurForm(V(-0.3, 3.5, 0.4), f), Math.Sqrt(0.3 * 0.3 + 0.4 * 0.4 + 1.0), 1e-9, "Ecke dreidimensional");
    }

    [Fact]
    public void Abstand_schraege_Wand_wird_gegen_den_Koerper_gemessen_nicht_gegen_die_Huellbox()
    {
        var f = F(Schraeg);
        // Punkt (3000, 1000) mm: im achsparallelen Hüllquader (0 … 4000 × −141 … 4141), aber 1.41 m von der Diagonalen entfernt
        var p = V(3, 1.2, -1);
        Assert.Equal(0, Durchgang.AbstandZurBox(p, f.Box));   // die Hüllbox sagt «innen»
        var d = Durchgang.AbstandZurForm(p, f);
        Assert.True(d > 1.2 && d < 1.5, $"Abstand {d}");
        Nah(d, 2 / Math.Sqrt(2), 0.01, "Abstand zur Wandfläche (Diagonale), Wand liegt auf der anderen Seite");
        Assert.False(Durchgang.ImDurchgang(p, [f], Durchgang.HuellboxVon([f])));
        // auf der Wand selbst: Mitte bei (2000, 2000)
        Assert.True(Durchgang.ImDurchgang(V(2, 1.2, -2), [f], Durchgang.HuellboxVon([f])));
        Nah(Durchgang.AbstandZurForm(V(2, 1.2, -2), f), 0);
    }

    [Fact]
    public void Wand_mit_Loch_Fenster_als_Aussparung_im_Grundriss_im_Loch_Abstand_zum_Lochrand()
    {
        var f = (PolyForm)F(new KoerperTeil
        {
            Art = "prisma", Grundriss = [[0, 0], [4000, 0], [4000, 4000], [0, 4000]],
            Loecher = [[[1000, 1000], [3000, 1000], [3000, 3000], [1000, 3000]]], Unten = 0, Oben = 2500,
        });
        Nah(Durchgang.AbstandZurFlaeche(0.5, -0.5, f.Ring, f.Loecher), 0);   // im Wandstreifen
        Nah(Durchgang.AbstandZurFlaeche(2, -2, f.Ring, f.Loecher), 1);       // Mitte des Lochs: 1 m bis zum Rand
        Nah(Durchgang.AbstandZurFlaeche(5, -2, f.Ring, f.Loecher), 1);       // ausserhalb: 1 m
    }

    [Fact]
    public void Quader_mit_Drehung_Zylinder_Kugel_Netzkoerper()
    {
        // Quader 4000 × 200 × 2500 um (0,0) gedreht 90°: liegt dann entlang y (Nord), Szene-z
        var q = F(new KoerperTeil { Art = "quader", Mitte = [0, 0, 1250], Masse = [4000, 200, 2500], Drehung = 90 });
        Nah(Durchgang.AbstandZurForm(V(0, 1.25, -1.9), q), 0, 1e-9, "innen (entlang Nord)");
        Nah(Durchgang.AbstandZurForm(V(1.1, 1.25, 0), q), 1.0, 1e-9, "seitlich 1.1 − 0.1");
        Nah(Durchgang.AbstandZurForm(V(0, 1.25, -2.4), q), 0.4, 1e-9, "Stirnseite");
        var z = F(new KoerperTeil { Art = "zylinder", Von = [0, 0, 0], Nach = [0, 0, 3000], Radius = 150 });
        Nah(Durchgang.AbstandZurForm(V(0.65, 1.5, 0), z), 0.5, 1e-9, "Stütze rund");
        Nah(Durchgang.AbstandZurForm(V(0, 3.5, 0), z), 0.5, 1e-9, "über der Stütze");
        Nah(Durchgang.AbstandZurForm(V(0, 1.5, 0), z), 0, 1e-9, "in der Stütze");
        var k = F(new KoerperTeil { Art = "kugel", Mitte = [0, 0, 0], Radius = 500 });
        Nah(Durchgang.AbstandZurForm(V(1, 0, 0), k), 0.5);
        var n = F(new KoerperTeil { Art = "koerper", Ecken = [[0, 0, 0], [1000, 0, 0], [0, 1000, 0], [0, 0, 1000]] });
        Nah(Durchgang.AbstandZurForm(V(2, 0.5, 0), n), 1);
        Assert.Null(Durchgang.FormAusTeil(new KoerperTeil { Art = "netz" }));
        Assert.Null(Durchgang.FormAusTeil(new KoerperTeil { Art = "prisma", Grundriss = [[0, 0], [1, 1]], Unten = 0, Oben = 1 }));
        Assert.Null(Durchgang.FormAusTeil(null));
    }

    [Fact]
    public void FormAusTeil_ungueltige_Angaben_geben_null()
    {
        Assert.Null(Durchgang.FormAusTeil(new KoerperTeil { Art = "prisma", Grundriss = [[0, 0], [1000, 0], [0, 1000]], Unten = 500, Oben = 500 }));   // keine Höhe
        Assert.Null(Durchgang.FormAusTeil(new KoerperTeil { Art = "quader", Mitte = [0, 0, 0], Masse = [100, 0, 100] }));
        Assert.Null(Durchgang.FormAusTeil(new KoerperTeil { Art = "quader", Masse = [100, 100, 100] }));
        Assert.Null(Durchgang.FormAusTeil(new KoerperTeil { Art = "zylinder", Von = [0, 0, 0], Nach = [0, 0, 1000], Radius = 0 }));
        Assert.Null(Durchgang.FormAusTeil(new KoerperTeil { Art = "zylinder", Von = [0], Nach = [0, 0, 1000], Radius = 10 }));
        Assert.Null(Durchgang.FormAusTeil(new KoerperTeil { Art = "kugel", Mitte = [0, 0], Radius = -1 }));
        Assert.Null(Durchgang.FormAusTeil(new KoerperTeil { Art = "koerper", Ecken = [[0, 0, 0], [1, 1, 1]] }));
    }

    [Fact]
    public void Schwelle_500_mm_darunter_durchscheinend_ab_500_mm_nicht()
    {
        Form[] f = [F(Wand)];
        var box = Durchgang.HuellboxVon(f);
        Assert.True(Durchgang.ImDurchgang(V(2, 1.7, 0.4999), f, box));
        Assert.False(Durchgang.ImDurchgang(V(2, 1.7, 0.5), f, box));
        Assert.False(Durchgang.ImDurchgang(V(2, 1.7, 0.5001), f, box));
        Assert.True(Durchgang.ImDurchgang(V(2, 1.7, -0.1), f, box));   // mitten in der Wand
        Assert.True(Durchgang.ImDurchgang(V(2, 1.7, -0.6999), f, box));   // hinten 0.4999 vom Rand
        Assert.False(Durchgang.ImDurchgang(V(2, 1.7, -0.7001), f, box));
        Assert.False(Durchgang.ImDurchgang(V(2, 3.2, 0), f, box), "über der Wand: 0.7 m");
        Assert.False(Durchgang.ImDurchgang(V(2, 1.7, 0.4), [], null), "ohne Formen nie");
    }

    [Fact]
    public void Vorauswahl_ueber_die_Huellbox_spart_die_genaue_Rechnung_aendert_das_Ergebnis_aber_nicht()
    {
        Form[] f = [F(Schraeg)];
        var box = Durchgang.HuellboxVon(f);
        foreach (var (x, z) in new (double, double)[] { (3, -1), (2, -2), (-2, 0), (5, -5), (1, -1.2), (1.35, -1.5) })
        {
            var p = V(x, 1.2, z);
            Assert.True(Durchgang.ImDurchgang(p, f, box) == Durchgang.ImDurchgang(p, f, null), $"Punkt {x}/{z}");
        }
        Assert.Equal(double.PositiveInfinity, Durchgang.AbstandZumKoerper(V(100, 1, 100), f, box, 0.5));
    }

    [Fact]
    public void HuellboxVon_vereinigt_die_Formen_und_ist_ohne_Formen_null()
    {
        Assert.Null(Durchgang.HuellboxVon(null));
        Assert.Null(Durchgang.HuellboxVon([]));
        var box = Durchgang.HuellboxVon([F(Wand), F(new KoerperTeil { Art = "kugel", Mitte = [5000, 0, 0], Radius = 500 })])!.Value;
        Nah(box.Min.X, 0);
        Nah(box.Max.X, 5.5);
        Nah(box.Max.Y, 2.5);
        Nah(box.Min.Z, -0.5);
    }

    // ---------------------------------------------------------------------------------------------- N22 mit der Kopfposition (vr.test.mjs)

    [Fact]
    public void N22_in_VR_Waende_undurchsichtig_durchscheinend_unter_500_mm_zur_KOPFposition_nicht_zur_Rig_Mitte()
    {
        Form[] w = [F(Wand)];   // Wand entlang x bei z = 0 … −0.2, bis 2.5 m hoch
        var box = Durchgang.HuellboxVon(w);
        // Rig 2 m vor der Wand; der Kopf steht im Referenzraum 1.8 m seitlich/zurück versetzt (physisch gegangen) und 1.7 m hoch
        var rg = new Rig(2, 0, 2, 0);
        var lokal = new Vec3(0, 1.7, 0);
        bool Nah_() => Durchgang.ImDurchgang(VrLogik.KopfWelt(rg, lokal), w, box);
        Assert.False(Nah_(), "beim Start undurchsichtig");
        // der Nutzer geht (Rig versetzt) bis 0.3 m vor die Wand (Kopf bei z = 0.3): durchscheinend
        rg = VrLogik.RigVersetzen(rg, 0, -1.7);
        Assert.True(Nah_());
        // genau 500 mm gelten noch als entfernt (streng kleiner)
        rg = rg with { Z = 0.5 };
        Assert.False(Nah_());
        rg = rg with { Z = 0.4999 };
        Assert.True(Nah_());
        // Kopf nach hinten (Teleport 3 m weg): wieder undurchsichtig
        rg = VrLogik.RigAufZiel(rg, lokal, new Xz(2, 3), 0);
        Assert.False(Nah_());
    }

    [Fact]
    public void N22_in_VR_auch_die_Hoehe_des_Kopfes_zaehlt_Kopf_ueber_der_Wand_3D_Abstand()
    {
        Form[] w = [F(Wand)];
        var box = Durchgang.HuellboxVon(w);
        var rg = new Rig(2, 0, 0.1, 0);
        bool Nah_(double hoehe) => Durchgang.ImDurchgang(VrLogik.KopfWelt(rg, new Vec3(0, hoehe, 0)), w, box);
        Assert.False(Nah_(3.2), "0.7 m über der Wand");   // Wand endet bei 2.5 m
        Assert.True(Nah_(2.9), "0.4 m über der Wand");
    }

    // ---------------------------------------------------------------------------------------------- Zustand und Einblendung (begehen.test.mjs)

    [Fact]
    public void Zustand_aus_dem_Standort_Projekt_mm_nach_Szenenmeter_x_Ost_z_minus_Nord_Augen_1_7_m_ueber_Boden()
    {
        var z = Begehen.ZustandAusStandort(StandortMit(90, 0))!;
        Nah(z.X, 2);
        Nah(z.Z, -1.5);
        Nah(z.Boden, 0);
        Nah(z.Augen, 1.7);
        Nah(z.Gieren, Math.PI / 2);
        Nah(z.Nicken, 0);
    }

    [Fact]
    public void Obergeschoss_Augenhoehe_zaehlt_ab_OK_Boden_des_Raums()
    {
        var z = Begehen.ZustandAusStandort(StandortMit(90, 3000))!;
        Nah(z.Boden + z.Augen, 4.7);
    }

    [Fact]
    public void Augenhoehe_der_Benutzereinstellung_uebersteuert_die_der_Abfrage_ungueltige_Werte_werden_begrenzt()
    {
        Nah(Begehen.ZustandAusStandort(StandortMit(), 1200)!.Augen, 1.2);
        Nah(Begehen.ZustandAusStandort(StandortMit(), 99999)!.Augen, Begehen.AugenhoeheMax / 1000);
        Nah(Begehen.ZustandAusStandort(StandortMit(), 10)!.Augen, Begehen.AugenhoeheMin / 1000);
        Nah(Begehen.ZustandAusStandort(StandortMit() with { Augenhoehe = null })!.Augen, 1.7);   // Vorgabe
        Assert.Equal(1700, Begehen.AugenhoeheVorgabe);
        Assert.Equal(300, Begehen.AugenhoeheMin);
    }

    [Fact]
    public void Unvollstaendiger_Standort_gibt_null()
    {
        Assert.Null(Begehen.ZustandAusStandort(null));
        Assert.Null(Begehen.ZustandAusStandort(new Standort { Standpunkt = new Standpunkt(1, 2) }));
        Assert.Null(Begehen.ZustandAusStandort(StandortMit() with { Blick = null }));
        Assert.Null(Begehen.ZustandAusStandort(StandortMit() with { Blick = new Blick(double.NaN) }));
        Assert.Null(Begehen.ZustandAusStandort(StandortMit() with { BodenOk = null }));
        Assert.Null(Begehen.ZustandAusStandort(StandortMit() with { Standpunkt = new Standpunkt(double.NaN, 1) }));
    }

    [Fact]
    public void Nicken_wird_auf_80_Grad_begrenzt()
    {
        Nah(Begehen.ZustandAusStandort(StandortMit() with { Blick = new Blick(90, 120) })!.Nicken, 80 * Grad);
        Nah(Begehen.ZustandAusStandort(StandortMit() with { Blick = new Blick(90, -120) })!.Nicken, -80 * Grad);
    }

    [Fact]
    public void Einblendung_Raumkennzeichen_Nummer_und_Name_Geschoss_Augenhoehe_in_m()
    {
        var z = Begehen.ZustandAusStandort(StandortMit(90, 0))!;
        var e = Begehen.Einblendung(StandortMit(90, 0), z);
        Assert.Equal("R-1 · 0.01 · Wohnen", e.Titel);
        Assert.Equal("Geschoss EG – Erdgeschoss", e.Geschoss);
        Assert.Equal("Augenhöhe 1.70 m über OK Boden", e.Augenhoehe);
        var o = Begehen.Einblendung(new Standort { Raum = "R-2", Name = null, Geschoss = "OG" }, z with { Augen = z.Augen + 0.1 });
        Assert.Equal("R-2", o.Titel);
        Assert.Equal("Geschoss OG", o.Geschoss);
        Assert.Equal("Augenhöhe 1.80 m über OK Boden", o.Augenhoehe);
        Assert.Equal("", Begehen.Einblendung(new Standort(), z).Geschoss);
    }
}
