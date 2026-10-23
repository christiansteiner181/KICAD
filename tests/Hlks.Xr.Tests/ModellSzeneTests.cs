// Quest App P4a: Szene aus den GLB (ModellSzene), Durchgang auf den Netzen (NetzDurchgang, DreieckForm), Räume als Startpunkt (RaumStandort) und die Wahl
// des Starts (AppStart nach VrStartLogik).

using Hlks.Xr.Modell;
using Hlks.Xr.Netz;
using static Hlks.Xr.Tests.ModellHilfen;
using static Hlks.Xr.Tests.T;

namespace Hlks.Xr.Tests;

public class ModellSzeneTests
{
    /// <summary>EG: Raum R1 4 × 3 m (x 0 … 4, z 0 … −3), Wand W1 an der Südseite (z 0 … 0.2), Wand W2 im Westen, Decke D1 auf 2.5 m, ein Rohr; OG: Raum R2.
    /// Unterlage EG: eine IFC-Wand und ein IFC-Möbel.</summary>
    static ModellSzene Szene(double[]? bezug = null)
    {
        var eg = Datei(bezug,
            Quadernetz("architektur", ("W1", "wand", V(0, 0, 0), V(4, 2.5, 0.2)), ("W2", "wand", V(-0.2, 0, -3), V(0, 2.5, 0)), ("D1", "decke", V(0, 2.5, -3), V(4, 2.75, 0)),
                ("T1", "oeffnung", V(1, 0, 0), V(2, 2, 0.2))),
            Quadernetz("hlks", ("L1", "leitung", V(1, 2.2, -2), V(3, 2.3, -1.9))),
            RaumNetz("R1", [new Xz(0, 0), new Xz(4, 0), new Xz(4, -3), new Xz(0, -3)], 0, 2.5));
        var og = Datei(bezug, RaumNetz("R2", [new Xz(0, 0), new Xz(6, 0), new Xz(6, -5), new Xz(0, -5)], 2.75, 2.5));
        var ul = Datei(bezug, Quadernetz("unterlage", ("ifc-w", "IFCWALLSTANDARDCASE", V(8, 0, -3), V(8.2, 2.5, 0)), ("ifc-m", "IFCFURNISHINGELEMENT", V(5, 0, -1), V(6, 1, 0))));
        return new ModellSzene([(eg, "g-eg", "modell"), (og, "g-og", "modell"), (ul, "g-eg", "unterlage")]);
    }

    static ModellListe Liste(string projekt = "p1", double[]? bezug = null) => new(projekt, bezug ?? [0, 0, 0],
    [
        new ListenGeschoss("g-ug", "UG", "Untergeschoss", -2700, 2700, new ListenTeil("aaaaaaaa", "/api/xr/geschoss/g-ug.glb?teil=modell", null), null),
        new ListenGeschoss("g-eg", "EG", "Erdgeschoss", 0, 2750, new ListenTeil("bbbbbbbb", "/api/xr/geschoss/g-eg.glb?teil=modell", null), null),
        new ListenGeschoss("g-og", "1.OG", "Obergeschoss", 2750, 2750, new ListenTeil("cccccccc", "/api/xr/geschoss/g-og.glb?teil=modell", null), null),
    ], "{}");

    // ---------------------------------------------------------------------------------------------- Szene

    [Fact]
    public void Szene_Elemente_Raeume_Durchgangsbauteile_Box_und_Sichtbar()
    {
        var s = Szene();
        Assert.Equal(5, s.Netze.Count);
        Assert.Equal(9, s.Elemente.Count);
        Assert.Equal(["modell:R1", "modell:R2"], s.Raeume.Select(r => r.Schluessel));
        // Wände und Decken; die IFC-Wand ja, das IFC-Möbel nein, die Öffnung nein (Annahme P4a: IFC-Wände, -Decken usw. gehören dazu)
        Assert.Equal(["modell:D1", "modell:W1", "modell:W2", "unterlage:ifc-w"], s.DurchgangsBauteile.Select(e => e.Schluessel));
        Nah(s.Box.Min.X, -0.2, 1e-6); Nah(s.Box.Min.Y, 0); Nah(s.Box.Min.Z, -3);   // ohne Räume: R2 (6 × 5 m im OG) zählt nicht (float in der GLB)
        Assert.Equal(8.2, s.Box.Max.X, 6);
        var w1 = s.Finde("W1")!;
        Assert.Equal(V(0, 0, 0), w1.Box.Min);
        Assert.Equal(12, w1.Dreiecke);
        Assert.Equal("g-eg", w1.GeschossId);
        Assert.Same(s.Elemente["unterlage:ifc-w"], s.Finde("ifc-w"));
        var (netze, dreiecke) = s.Sichtbar(new HashSet<string> { "raum" });
        Assert.Equal(3, netze);
        Assert.Equal(48 + 12 + 24, dreiecke);
        Assert.Empty(s.Hinweise);
    }

    [Fact]
    public void Szene_meldet_abweichenden_Bezugspunkt_und_unlesbare_GLB()
    {
        var s = new ModellSzene([(Datei([0, 0, 0], Quadernetz("hlks", ("a", "leitung", V(0, 0, 0), V(1, 1, 1)))), "g", "modell"),
            (Datei([1000, 0, 0], Quadernetz("hlks", ("b", "leitung", V(0, 0, 0), V(1, 1, 1)))), "h", "modell")]);
        Assert.Contains("anderer Bezugspunkt", Assert.Single(s.Hinweise));
        var t = ModellSzene.AusGlb([(new byte[] { 1, 2, 3 }, "g", "modell", "EG modell")]);
        Assert.Contains("EG modell", Assert.Single(t.Hinweise));
        Assert.Empty(t.Netze);
    }

    // ---------------------------------------------------------------------------------------------- Durchgang

    [Fact]
    public void Naechster_Punkt_im_Dreieck_innen_Kante_Ecke()
    {
        Vec3 a = V(0, 0, 0), b = V(2, 0, 0), c = V(0, 0, -2);
        Assert.Equal(V(0.5, 0, -0.5), Durchgang.NaechsterPunktImDreieck(V(0.5, 3, -0.5), a, b, c));   // über der Fläche
        Assert.Equal(a, Durchgang.NaechsterPunktImDreieck(V(-1, 0, 1), a, b, c));
        Assert.Equal(V(1, 0, 0), Durchgang.NaechsterPunktImDreieck(V(1, 0, 1), a, b, c));           // vor der Kante a–b
        var p = Durchgang.NaechsterPunktImDreieck(V(2, 0, -2), a, b, c);                              // gegenüber der Kante b–c
        Nah(p.X, 1); Nah(p.Z, -1);
        Assert.Equal(a, Durchgang.NaechsterPunktImDreieck(V(1, 1, 1), a, a, a));                      // entartet
    }

    [Fact]
    public void DreieckForm_misst_wie_der_Quader_des_Kerns()
    {
        // dieselbe Wand als Prisma (Kern) und als Netz (GLB): ausserhalb gleicher Abstand
        var prisma = Durchgang.FormAusTeil(new KoerperTeil { Art = "prisma", Grundriss = [[0, 0], [4000, 0], [4000, 200], [0, 200]], Unten = 0, Oben = 2500 })!;
        var (pos, ind) = Quader(V(0, 0, -0.2), V(4, 2.5, 0));
        var ecken = ind.Select(i => V(pos[(int)i * 3], pos[(int)i * 3 + 1], pos[(int)i * 3 + 2])).ToList();
        var netz = DreieckForm.Aus(ecken)!;
        foreach (var p in new[] { V(2, 1.2, 0.3), V(2, 1.2, -0.5), V(2, 3, -0.1), V(-0.3, 1.2, 0.4), V(-0.3, 3.5, 0.4) })
            Nah(Durchgang.AbstandZurForm(p, netz), Durchgang.AbstandZurForm(p, prisma), 1e-6, $"{p}");
        // innen: Abstand zur nächsten Fläche (Wand 200 mm dick → höchstens 100 mm), also im Durchgang
        Nah(Durchgang.AbstandZurForm(V(2, 1.2, -0.1), netz), 0.1, 1e-6);
        Assert.True(Durchgang.ImDurchgang(V(2, 1.2, -0.1), [netz], netz.Box));
        Assert.Null(DreieckForm.Aus([V(0, 0, 0), V(1, 0, 0)]));
    }

    [Fact]
    public void Durchgang_nur_unter_500_mm_und_nur_bei_Bewegung_neu()
    {
        var s = Szene();
        var d = new DurchgangVerfolger(s);
        Assert.False(d.Pruefen(V(2, 1.7, -1.5)));   // Raummitte: alles weiter als 0.5 m (Decke 0.8 m über dem Kopf)
        Assert.Empty(d.Nah);
        Assert.True(d.Pruefen(V(2, 1.7, -0.25)));   // 0.25 m vor W1 (Südwand, z 0 … 0.2 → Innenseite bei z = 0)
        Assert.Equal(["modell:W1"], d.Nah.Order());
        Assert.False(d.Pruefen(V(2, 1.7, -0.252)));   // kleiner als 5 mm: nicht neu gerechnet
        Assert.False(d.Pruefen(V(2, 1.7, -0.3)));     // neu gerechnet, gleiche Menge
        Assert.True(d.Pruefen(V(-0.1, 1.7, -0.1)));   // in der Ecke: W1 und W2
        Assert.Equal(["modell:W1", "modell:W2"], d.Nah.Order());
        Assert.True(d.Pruefen(V(8.1, 1.7, -1)));       // in der IFC-Wand der Unterlage
        Assert.Equal(["unterlage:ifc-w"], d.Nah.Order());
        Assert.True(d.Pruefen(V(2, 1.7, -0.5)));       // genau 500 mm: noch entfernt (streng kleiner zählt)
        Assert.Empty(d.Nah);
        d.Pruefen(V(2, 2.3, -1.5));                    // 0.2 m unter der Decke
        Assert.Equal(["modell:D1"], d.Nah.Order());
        var je = d.BereicheJeNetz();
        Assert.Equal([(24, 12)], Assert.Single(je).Value);   // D1 ist das dritte Element im Netz der Architektur
    }

    [Fact]
    public void Aufteilen_und_Ausschnitt_der_Netze()
    {
        uint[] ind = [0, 1, 2, 2, 1, 3, 4, 5, 6, 6, 5, 7];
        var (uebrig, durch) = NetzDurchgang.Aufteilen(ind, [(1, 1), (1, 1), (3, 5)]);   // doppelt und über das Ende hinaus
        Assert.Equal([0u, 1, 2, 4, 5, 6], uebrig);
        Assert.Equal([2u, 1, 3, 6, 5, 7], durch);
        var pos = Enumerable.Range(0, 24).Select(i => (float)i).ToArray();
        var nor = Enumerable.Range(0, 24).Select(i => (float)-i).ToArray();
        var (p, n, i2) = NetzDurchgang.Ausschnitt(pos, nor, durch);
        Assert.Equal([0u, 1, 2, 3, 4, 5], i2);
        Assert.Equal([6f, 7, 8, 3, 4, 5, 9, 10, 11, 18, 19, 20, 15, 16, 17, 21, 22, 23], p);
        Assert.Equal(-6f, n[0]);
        var (p2, _, i3) = NetzDurchgang.Ausschnitt(pos, nor, [5, 1, 5]);   // gemeinsame Ecken nur einmal
        Assert.Equal([0u, 1, 0], i3);
        Assert.Equal([15f, 16, 17, 3, 4, 5], p2);
        var (alles, nichts) = NetzDurchgang.Aufteilen(ind, []);
        Assert.Equal(ind, alles);
        Assert.Empty(nichts);
    }

    [Fact]
    public void Bauteile_des_Durchgangs_nach_Gruppe_und_Art()
    {
        Assert.True(NetzDurchgang.IstBauteil("architektur", "wand"));
        Assert.True(NetzDurchgang.IstBauteil("architektur", "decke"));
        Assert.False(NetzDurchgang.IstBauteil("architektur", "oeffnung"));
        Assert.False(NetzDurchgang.IstBauteil("hlks", "wand"));
        foreach (var e in new[] { "IFCWALL", "IfcWallStandardCase", "IFCSLAB", "IFCCOLUMN", "IFCBEAM", "IFCROOF", "IFCCURTAINWALL" })
            Assert.True(NetzDurchgang.IstBauteil("unterlage", e), e);
        foreach (var e in new[] { "IFCDOOR", "IFCWINDOW", "IFCFURNISHINGELEMENT", "IFCSPACE" })
            Assert.False(NetzDurchgang.IstBauteil("unterlage", e), e);
        Assert.False(NetzDurchgang.IstBauteil("unterlage", null));
    }

    // ---------------------------------------------------------------------------------------------- Räume

    [Fact]
    public void Raum_Rechteck_Schwerpunkt_Boden_Blick_nach_Norden()
    {
        var s = Szene();
        var st = AppStart.RaumStandortVon(s, Liste(), "R1")!;
        Assert.Equal("schwerpunkt", st.Standpunkt!.Art);
        Nah(st.Standpunkt.X, 2000, 1e-6);
        Nah(st.Standpunkt.Y, 1500, 1e-6);       // Szene z = −1.5 → Projekt-Nord 1500 mm
        Nah(st.BodenOk!.Value, 0, 1e-6);
        Nah(st.Standpunkt.Z!.Value, 1700, 1e-6);
        Assert.Equal(90, st.Blick!.AzimutGrad);
        Assert.Equal("EG", st.Geschoss);
        Assert.Equal("Erdgeschoss", st.GeschossName);
        Assert.Null(AppStart.RaumStandortVon(s, Liste(), "W1"));     // kein Raum
        Assert.Null(AppStart.RaumStandortVon(s, Liste(), "R-1.04")); // Kennzeichen kennt die GLB nicht
        var og = AppStart.RaumStandortVon(s, Liste(), "R2")!;
        Nah(og.BodenOk!.Value, 2750, 1e-3);
    }

    [Fact]
    public void Raum_L_Form_innerster_Punkt_liegt_innen()
    {
        // schmales L (Schenkel 1 m breit, 6 m lang), Schwerpunkt (1.86, −4.14) liegt aussen; Ring ab dem einspringenden Eck (Fächer der Hilfe)
        Xz[] ring = [new(1, -5), new(1, 0), new(0, 0), new(0, -6), new(6, -6), new(6, -5)];
        var raum = RaumNetz("L", ring, 0, 2.5);
        var ecken = new List<Vec3>();
        for (int t = 0; t < raum.Indizes.Length; t++) ecken.Add(raum.Ecke(raum.Indizes[t]));
        var g = RaumStandort.Grundriss(ecken)!;
        Nah(g.Flaeche, 6 + 5, 1e-6);
        Assert.Equal(6, g.Rand.Count);
        var (p, art) = RaumStandort.Standpunkt(g);
        Assert.Equal("innerster Punkt", art);
        Assert.True(RaumStandort.Innen(g, p));
        Assert.True(RaumStandort.Abstand(g, p) > 0.45, $"Abstand {RaumStandort.Abstand(g, p)}");   // Schenkel 1 m breit: Mitte 0.5 m vom Rand
    }

    [Fact]
    public void Raum_gedreht_Blick_nach_der_laengsten_Kante()
    {
        double w = 30 * Grad;
        Xz R(double x, double z) => new(x * Math.Cos(w) + z * Math.Sin(w), -x * Math.Sin(w) + z * Math.Cos(w));
        var raum = RaumNetz("G", [R(0, 0), R(6, 0), R(6, -3), R(0, -3)], 0, 2.5);
        var ecken = raum.Indizes.Select(i => raum.Ecke(i)).ToList();
        var g = RaumStandort.Grundriss(ecken)!;
        Nah(RaumStandort.AzimutGrad(g), 120, 1e-6);   // längste Kante um 30° gedreht → 90° + 30°
        Assert.Null(RaumStandort.Grundriss([V(0, 0, 0), V(0, 1, 0), V(0, 1, -1)]));   // senkrecht: keine Unterseite
    }

    [Fact]
    public void Raum_mit_Bezugspunkt_Standort_absolut_Zustand_wieder_in_der_Szene()
    {
        double[] bezug = [2_600_000, 1_200_000, 520];
        var s = Szene(bezug);
        var st = AppStart.RaumStandortVon(s, Liste(bezug: bezug), "R1")!;
        Nah(st.Standpunkt!.X, 2_602_000, 1e-3);
        Nah(st.Standpunkt.Y, 1_201_500, 1e-3);
        Nah(st.BodenOk!.Value, 520, 1e-3);
        var z = KicadUmrechnung.ZustandAusStandort(st, bezug)!;
        Nah(z.X, 2, 1e-6); Nah(z.Z, -1.5, 1e-6); Nah(z.Boden, 0, 1e-6); Nah(z.Augen, 1.7, 1e-9);
    }

    // ---------------------------------------------------------------------------------------------- Start

    [Fact]
    public async Task Start_Auftrag_vor_gemerkt_vor_EG_vor_Gesamt()
    {
        var s = Szene();
        var liste = Liste();
        var auftrag = VrStartLogik.AuftragPruefen(new VrAuftragRoh { Modus = "begehen", Raum = "R2", Nr = 3 });
        var a = await AppStart.Waehlen(auftrag, null, s, liste);
        Assert.Equal(Startquellen.Auftrag, a.Punkt.Quelle);
        Nah(a.Zustand.X, 3, 1e-6); Nah(a.Zustand.Z, -2.5, 1e-6); Nah(a.Zustand.Boden, 2.75, 1e-6);
        Assert.Equal("Auftrag vom PC", a.Anzeige.Quelle);

        var gemerkt = new GemerktStandpunkt { Ort = "p1", X = 1000, Y = 500, BodenOk = 0, AzimutGrad = 180, Name = "Letzter Standpunkt" };
        var b = await AppStart.Waehlen(null, gemerkt, s, liste);
        Assert.Equal(Startquellen.Gemerkt, b.Punkt.Quelle);
        Nah(b.Zustand.X, 1); Nah(b.Zustand.Z, -0.5); Nah(b.Zustand.Gieren, Math.PI);

        // Auftrag mit unbekanntem Raum (Kennzeichen): weiter mit dem gemerkten
        var c = await AppStart.Waehlen(VrStartLogik.AuftragPruefen(new VrAuftragRoh { Modus = "begehen", Raum = "R-9", Nr = 4 }), gemerkt, s, liste);
        Assert.Equal(Startquellen.Gemerkt, c.Punkt.Quelle);

        // gemerkt in einem anderen Projekt zählt nicht: EG (unterstes Geschoss mit Kote ≥ 0 und Raum) – R1
        var d = await AppStart.Waehlen(null, gemerkt with { Ort = "anderes" }, s, liste);
        Assert.Equal(Startquellen.Eg, d.Punkt.Quelle);
        Nah(d.Zustand.X, 2); Nah(d.Zustand.Z, -1.5);
        Assert.Equal("Erster Raum im Erdgeschoss", d.Anzeige.Quelle);

        // Sitzungsraum-Aufträge setzen keinen Raum zum Begehen
        var e = await AppStart.Waehlen(VrStartLogik.AuftragPruefen(new VrAuftragRoh { Modus = "sitzungsraum", Raum = "R2", Nr = 5 }), null, s, liste);
        Assert.Equal(Startquellen.Eg, e.Punkt.Quelle);
    }

    [Fact]
    public async Task Start_ohne_Raeume_vor_dem_Gebaeude_Blick_nach_Norden()
    {
        var s = new ModellSzene([(Datei(null, Quadernetz("architektur", ("W1", "wand", V(0, 0, -10), V(20, 3, 0)))), "g-eg", "modell")]);
        var a = await AppStart.Waehlen(null, null, s, Liste());
        Assert.Equal(Startquellen.Gesamt, a.Punkt.Quelle);
        Nah(a.Zustand.X, 10, 1e-3);
        Nah(a.Zustand.Z, AppStart.GesamtAbstandM, 1e-3);   // 5 m südlich der Südseite (z = 0)
        Nah(a.Zustand.Boden, 0, 1e-3);                     // EG, Kote 0 (das UG zählt nicht)
        Nah(a.Zustand.Gieren, Math.PI / 2, 1e-6);          // nach Norden
        Assert.Equal("Vor dem Gebäude", a.Anzeige.Raum);
    }

    [Fact]
    public void EG_Raeume_groesster_zuerst()
    {
        var s = new ModellSzene([(Datei(null,
            RaumNetz("a-klein", [new Xz(0, 0), new Xz(2, 0), new Xz(2, -2), new Xz(0, -2)], 0, 2.5),
            RaumNetz("b-gross", [new Xz(3, 0), new Xz(9, 0), new Xz(9, -5), new Xz(3, -5)], 0, 2.5)), "g-eg", "modell")]);
        Assert.Equal(["b-gross", "a-klein"], AppStart.EgRaeume(s, Liste()));   // nach Fläche, nicht nach Name (die GLB kennt kein Kennzeichen)
        Assert.Empty(AppStart.EgRaeume(Szene(), Liste() with { Geschosse = [] }));
    }
}
