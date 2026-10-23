// Tests von Modul XR, Teile XR-B (Masse zu Decke und Wand) und XR-C (Messwerkzeug mit Fang), übertragen aus xr-b.test.mjs: xr-mess-logik.js.
// Nicht übertragen: die Tests der Greif-Teile (Reichweite, Querverschiebung, Befehlsmasse) – sie kommen mit XR-B.

using static Hlks.Xr.Tests.T;
using M = Hlks.Xr.XrMessLogik;

namespace Hlks.Xr.Tests;

public class XrMessLogikTests
{
    // Raum 5 × 4 m: Decke 2.8 … 3.0 m (Prisma), Wand im Westen (x = 0) und im Osten (x = 5), Modell-mm.
    static readonly Koerper Decke = new("D", "decke", [new KoerperTeil
    {
        Art = "prisma", Grundriss = [[0, 0], [5000, 0], [5000, 4000], [0, 4000]], Loecher = [[[4000, 1000], [4500, 1000], [4500, 1500], [4000, 1500]]],
        Unten = 2800, Oben = 3000, Material = "decke",
    }]);
    static readonly Koerper WandW = new("WW", "wand", [new KoerperTeil { Art = "quader", Mitte = [-100, 2000, 1400], Masse = [200, 4000, 2800], Drehung = 0, Material = "wand" }]);
    static readonly Koerper WandO = new("WO", "wand", [new KoerperTeil { Art = "quader", Mitte = [5100, 2000, 1400], Masse = [200, 4000, 2800], Drehung = 0, Material = "wand" }]);
    static readonly Koerper Rohr = new("R", "leitung", [new KoerperTeil { Art = "zylinder", Von = [500, 2000, 2500], Nach = [4500, 2000, 2500], Radius = 25, Material = "rohr" }]);
    static readonly Koerper Raum = new("RAUM", "raum", [new KoerperTeil { Art = "quader", Mitte = [2500, 2000, 1400], Masse = [5000, 4000, 2800], Material = "raum" }]);

    static Fangdaten Daten(params Koerper[] ks)
    {
        var d = new Fangdaten();
        foreach (var k in ks) d.Setzen(k);
        return d;
    }

    // ------------------------------------------------------------------------------------------------ Masse zu Decke und Wand

    [Fact]
    public void Masse_zu_Decke_und_Wand_lotrecht_nach_oben_waagrecht_quer_zur_Leitung_gegen_Handrechnung()
    {
        var d = Daten(Decke, WandW, WandO, Rohr, Raum);
        var p = M.AusModell([2500, 2000, 2500]);   // Achse der Leitung
        // Querachse «seitlich» einer Leitung nach Osten (Achse x) ist (0, 0, 1); einer Leitung nach Norden (Achse −z) ist (1, 0, 0)
        // (xr-greifen-logik.js querAchsen, noch nicht übertragen: Teil von XR-B)
        var s = new Vec3(0, 0, 1);
        var opt = new FangOptionen(Radius: 0.025, Ausser: new HashSet<string> { "R" });
        var e = M.DeckeUndWaende(d, p, [s, -s], opt);
        Nah(e.Decke!.AbstandMm, 300, 1e-6, "Decke: 2800 − 2500");
        Nah(e.Decke.AbstandRandMm, 275, 1e-6, "abzüglich Rohrradius");
        Assert.Equal("D", e.Decke.Id);
        // zwei Wände links und rechts der Leitung (quer zur Achse x → ± z): die Wände stehen bei x = 0 und 5, also NICHT quer; die Seitenrichtung ist ± z → keine Wand
        Assert.Null(e.WandA);
        Assert.Null(e.WandB);
        // Leitung nach Norden (Achse z): quer sind ± x, die Wände bei x = 0 und x = 5 liegen links und rechts
        var q2 = new Vec3(1, 0, 0);
        var e2 = M.DeckeUndWaende(d, p, [q2, -q2], opt);
        Gleich([2500.0, 2500], new[] { e2.WandA!, e2.WandB! }.Select(w => Math.Round(w.AbstandMm)).OrderBy(x => x).ToArray());   // Wandinnenseiten bei x = 0 und x = 5000, Leitung in der Mitte
        var verschoben = M.DeckeUndWaende(d, M.AusModell([1000, 2000, 2650]), [q2, -q2], new FangOptionen(Ausser: new HashSet<string> { "R" }));
        Gleich([1000.0, 4000], new[] { verschoben.WandA!, verschoben.WandB! }.Select(w => Math.Round(w.AbstandMm)).OrderBy(x => x).ToArray());
        Nah(verschoben.Decke!.AbstandMm, 150, 1e-6);
        // Der Raum (durchscheinendes Volumen) zählt nicht als Decke oder Wand
        Assert.NotEqual("RAUM", e.Decke.Id);
        // unter einem Loch in der Decke gibt es keine Decke
        Assert.Null(M.DeckeUndWaende(d, M.AusModell([4250, 1250, 2500]), []).Decke);   // Öffnung in der Decke
        // ohne Decke darüber
        Assert.Null(M.DeckeUndWaende(Daten(WandW), p, []).Decke);
        // nur eine Seite angegeben: die zweite Wand entfällt
        var eins = M.DeckeUndWaende(d, M.AusModell([1000, 2000, 2650]), [q2]);
        Assert.NotNull(eins.WandA);
        Assert.Null(eins.WandB);
    }

    // ------------------------------------------------------------------------------------------------ Fang und Abstand

    [Fact]
    public void Fang_Anschluss_vor_Ecke_vor_Kante_vor_Flaeche_mit_Fangradius_ohne_eigenes_Element()
    {
        var d = Daten(Decke, WandW, Rohr);
        // Anschluss am Rohrende (Achsenende, Ebene leitung)
        var a = M.FangenPunkt(d, M.AusModell([4510, 2005, 2500]));
        Assert.Equal("anschluss", a.Art);
        Assert.Equal("R", a.Id);
        Nah(a.P!.Value.X, 4.5, 1e-9);
        // Ecke der Decke
        var e = M.FangenPunkt(d, M.AusModell([5010, -10, 3000]));
        Assert.Equal("ecke", e.Art);
        Assert.Equal("D", e.Id);
        Nah(e.P!.Value.X, 5, 1e-9);
        // Achse der Leitung (mitten, aber nicht auf der Mitte)
        var k = M.FangenPunkt(d, M.AusModell([1500, 2010, 2500]));
        Assert.Equal("achse", k.Art);
        Nah(k.P!.Value.X, 1.5, 1e-9, "Fusspunkt auf der Achse");
        // Mitte der Achse
        Assert.Equal("mitte", M.FangenPunkt(d, M.AusModell([2500, 2010, 2500])).Art);
        // Kante der Decke (Unterkante entlang y bei x = 5000)
        var kante = M.FangenPunkt(d, M.AusModell([5000, 1200, 2790]));
        Assert.Equal("kante", kante.Art);
        Nah(kante.P!.Value.Z, -1.2, 1e-9);
        // Fläche: Deckenunterseite, mit Normale nach unten
        var f = M.FangenPunkt(d, M.AusModell([2500, 2000, 2780]), new FangOptionen(Radius: 0.03));
        Assert.Equal("flaeche", f.Art);
        Nah(f.N!.Value.Y, -1, 1e-9);
        Nah(f.P!.Value.Y, 2.8, 1e-9);
        // ausserhalb des Radius: frei
        var fr = M.FangenPunkt(d, M.AusModell([2500, 2000, 1000]));
        Assert.Equal("frei", fr.Art);
        Nah(fr.P!.Value.Y, 1, 1e-9);
        Assert.Null(fr.Id);
        // das gegriffene Element wird nie gefangen
        Assert.NotEqual("anschluss", M.FangenPunkt(d, M.AusModell([4510, 2005, 2500]), new FangOptionen(Ausser: new HashSet<string> { "R" })).Art);
        // Namen für die Anzeige
        Assert.Equal("Leitungsachse", k.Name);
        Assert.Equal("Fläche", f.Name);
    }

    [Fact]
    public void Fang_mit_dem_Strahl_Anschluss_nahe_am_Strahl_sonst_Treffer_auf_der_Flaeche()
    {
        var d = Daten(Decke, WandW, WandO, Rohr);
        var u = M.AusModell([2500, 2000, 1500]);
        // Strahl zum Rohrende bei x = 4500 (Anschluss)
        var ziel = M.AusModell([4500, 2000, 2500]);
        var r = (ziel - u).Einheit();
        var s = M.FangenStrahl(d, u, r);
        Assert.Equal("anschluss", s.Art);
        Assert.Equal("R", s.Id);
        // Strahl senkrecht nach oben, abseits der Leitung: Deckenunterseite
        var oben = M.FangenStrahl(d, M.AusModell([1000, 3000, 1500]), new Vec3(0, 1, 0));
        Assert.Equal("flaeche", oben.Art);
        Nah(oben.P!.Value.Y, 2.8, 1e-9);
        Nah(oben.T!.Value, 1.3, 1e-9);
        // Strahl nach Westen trifft die Innenseite der Wand
        var w = M.FangenStrahl(d, M.AusModell([1000, 3500, 1000]), new Vec3(-1, 0, 0));
        Assert.Equal("flaeche", w.Art);
        Nah(w.P!.Value.X, 0, 1e-9);
        // ins Leere
        var leer = M.FangenStrahl(d, u, new Vec3(0, -1, 0));
        Assert.Equal("frei", leer.Art);
        Assert.Null(leer.P);
        Assert.Null(leer.T);
    }

    [Fact]
    public void Abstand_Betrag_und_dx_dy_dz_in_Modell_Richtungen_Anzeige_in_mm_bzw_cm()
    {
        var a = M.AusModell([1000, 2000, 500]);
        var b = M.AusModell([1300, 1600, 1700]);
        var m = M.Masse(a, b);
        Nah(m.Dx, 300, 1e-9);
        Nah(m.Dy, -400, 1e-9);
        Nah(m.Dz, 1200, 1e-9);
        Nah(m.AbstandMm, Math.Sqrt(300 * 300 + 400 * 400 + 1200 * 1200), 1e-9);
        Assert.Equal("1234 mm · 123.4 cm", M.MassText(1234.4));
        Assert.Equal("37.3 mm · 3.7 cm", M.MassText(37.26));
        Assert.Equal("-1200 mm", M.MassKurz(-1200));   // dx/dy/dz behalten ihr Vorzeichen
        Assert.Equal("0.0 mm · 0.0 cm", M.MassText(-0.04));   // minus null wird zu null
        Assert.Equal("0.0 mm", M.MassKurz(-0.04));
        Assert.Equal("50.0 mm · 5.0 cm", M.MassText(50));
        Assert.Equal("-1200 mm · -120.0 cm", M.MassText(-1200));
        Assert.Equal("37.3 mm", M.MassKurz(37.26));
    }

    [Fact]
    public void Lot_zur_Flaeche_Abstand_zur_Ebene_Fusspunkt_unabhaengig_von_der_Lage_auf_der_Flaeche()
    {
        var d = Daten(Decke);
        var fl = M.FangenPunkt(d, M.AusModell([3000, 1000, 2790])).Flaeche;
        Assert.NotNull(fl);
        Assert.True(Math.Abs(fl!.N.Y + 1) < 1e-9);
        var p = M.AusModell([100, 3900, 1000]);   // weit seitlich, 1.8 m unter der Decke
        var l = M.LotMass(fl, p);
        Nah(l.AbstandMm, 1800, 1e-6);
        Nah(l.Fuss.X, p.X, 1e-12);
        Nah(l.Fuss.Y, 2.8, 1e-9);
        Nah(l.Fuss.Z, p.Z, 1e-12);
        Assert.Equal(1, l.Vorzeichen);   // die Normale der Deckenunterseite zeigt nach unten, der Punkt liegt auf ihrer Seite
        Assert.Equal(-1, M.LotMass(new Ebene3(new Vec3(0, 1, 0), new Vec3(0, 2.8, 0)), p).Vorzeichen);   // umgekehrte Normale: Rückseite
    }

    // ------------------------------------------------------------------------------------------------ Geometrie und Fangdaten

    [Fact]
    public void Fangdaten_Sammlung_Setzen_Ersetzen_Entfernen_Leeren_und_Reihenfolge()
    {
        var d = new Fangdaten();
        d.Setzen(Decke);
        d.Setzen(Rohr);
        d.Setzen(WandW);
        Assert.Equal(3, d.Anzahl);
        Gleich(["D", "R", "WW"], d.Alle().Select(z => z.Id).ToArray());
        d.Setzen(Rohr);   // Ersetzen behält die Stelle
        Gleich(["D", "R", "WW"], d.Alle().Select(z => z.Id).ToArray());
        Assert.NotNull(d.Get("R"));
        d.Entfernen("R");
        Assert.Null(d.Get("R"));
        Gleich(["D", "WW"], d.Alle().Select(z => z.Id).ToArray());
        d.Setzen(new Koerper("LEER", "sonst", []));   // ohne Geometrie nichts gespeichert
        Assert.Equal(2, d.Anzahl);
        d.Leeren();
        Assert.Equal(0, d.Anzahl);
    }

    [Fact]
    public void Fangdaten_aus_Koerper_Quader_hat_8_Ecken_12_Kanten_6_Flaechen_Zylinder_zwei_Anschluesse_eine_Achse()
    {
        var q = M.FangdatenAusKoerper(WandW);
        Assert.Equal(8, q.Punkte.Count);
        Assert.Equal(12, q.Strecken.Count);
        Assert.Equal(6, q.Flaechen.Count);
        Assert.True(q.Flaechen.All(f => Math.Abs(f.N.Laenge - 1) < 1e-9 && f.Ring.Length == 4));
        Nah(q.Box!.Value.Min.X, -0.2);
        Nah(q.Box.Value.Max.X, 0);
        var z = M.FangdatenAusKoerper(Rohr);
        Assert.Equal(2, z.Punkte.Count);
        Assert.True(z.Punkte.All(p => p.Art == "anschluss"));
        Assert.Single(z.Strecken);
        Assert.Equal("achse", z.Strecken[0].Art);
        Nah(z.Radius, 0.025);
        // anderes Element mit Zylinder: Enden sind Ecken
        Assert.True(M.FangdatenAusKoerper(new Koerper("S", "wand", Rohr.Teile)).Punkte.All(p => p.Art == "ecke"));
        // Prisma mit 4 Kanten: 2 + 4 Flächen, 12 Kanten, 8 Ecken
        var p4 = M.FangdatenAusKoerper(Decke);
        Assert.Equal(6, p4.Flaechen.Count);
        Assert.Equal(12, p4.Strecken.Count);
        Assert.Equal(8, p4.Punkte.Count);
        // Netzkörper: nur Ecken (höchstens 300)
        var netz = M.FangdatenAusKoerper(new Koerper("N", "apparat", [new KoerperTeil { Art = "koerper", Ecken = Enumerable.Range(0, 400).Select(i => new double[] { i, 0, 0 }).ToList() }]));
        Assert.Equal(300, netz.Punkte.Count);
        Assert.Equal("sonst", M.FangdatenAusKoerper(new Koerper("X", null, [])).Ebene);
    }

    [Fact]
    public void Geometrie_NaechsterAufStrecke_StrahlStrecke_ImFlaeche_StrahlFlaeche_FusspunktFlaeche()
    {
        var n = M.NaechsterAufStrecke3(new Vec3(0.5, 1, 0), Vec3.Null, new Vec3(2, 0, 0));
        Nah(n.T, 0.25);
        Nah(n.P.X, 0.5);
        Assert.Equal(0, M.NaechsterAufStrecke3(new Vec3(-5, 0, 0), Vec3.Null, new Vec3(2, 0, 0)).T);
        Assert.Equal(1, M.NaechsterAufStrecke3(new Vec3(9, 0, 0), Vec3.Null, new Vec3(2, 0, 0)).T);
        Assert.Equal(0, M.NaechsterAufStrecke3(new Vec3(9, 0, 0), Vec3.Null, Vec3.Null).T);
        // Strahl von (0,1,0) nach +x gegen die Strecke von (2,0,-1) bis (2,0,1): Abstand 1
        var h = M.StrahlStrecke(new Vec3(0, 1, 0), new Vec3(1, 0, 0), new Vec3(2, 0, -1), new Vec3(2, 0, 1));
        Nah(h.Abstand, 1);
        Nah(h.TStrahl, 2);
        Nah(h.PunktStrecke.Z, 0);
        // parallel
        var par = M.StrahlStrecke(new Vec3(0, 1, 0), new Vec3(1, 0, 0), new Vec3(0, 0, 0), new Vec3(4, 0, 0));
        Nah(par.Abstand, 1);
        // Fläche: Quadrat 2 × 2 in der Ebene y = 0 mit Loch, Normale nach oben
        var f = new FangFlaeche([new(0, 0, 0), new(2, 0, 0), new(2, 0, 2), new(0, 0, 2)], new Vec3(0, 1, 0), new Vec3(0, 0, 0),
            [[new(0.5, 0, 0.5), new(1, 0, 0.5), new(1, 0, 1), new(0.5, 0, 1)]]);
        Assert.True(M.ImFlaeche(new Vec3(1.5, 0, 1.5), f));
        Assert.False(M.ImFlaeche(new Vec3(0.7, 0, 0.7), f), "im Loch");
        Assert.False(M.ImFlaeche(new Vec3(3, 0, 1), f));
        Nah(M.StrahlFlaeche(new Vec3(1.5, 3, 1.5), new Vec3(0, -1, 0), f)!.Value, 3);
        Assert.Null(M.StrahlFlaeche(new Vec3(1.5, -3, 1.5), new Vec3(0, 1, 0), f));   // von hinten
        Nah(M.StrahlFlaeche(new Vec3(1.5, -3, 1.5), new Vec3(0, 1, 0), f, true)!.Value, 3);   // beidseitig
        Assert.Null(M.StrahlFlaeche(new Vec3(1.5, 3, 1.5), new Vec3(1, 0, 0), f));   // parallel
        Assert.Null(M.StrahlFlaeche(new Vec3(1.5, 3, 1.5), new Vec3(0, 1, 0), f));   // weg von der Fläche
        var fuss = M.FusspunktFlaeche(new Vec3(1.5, 2, 1.5), f)!.Value;
        Nah(fuss.Abstand, 2);
        Nah(fuss.P.Y, 0);
        Assert.Null(M.FusspunktFlaeche(new Vec3(5, 2, 5), f));
        // senkrechte Fläche: dominante Achse x
        var wand = new FangFlaeche([new(0, 0, 0), new(0, 0, 2), new(0, 2, 2), new(0, 2, 0)], new Vec3(1, 0, 0), new Vec3(0, 0, 0), []);
        Assert.True(M.ImFlaeche(new Vec3(0, 1, 1), wand));
        Assert.False(M.ImFlaeche(new Vec3(0, 3, 1), wand));
    }

    [Fact]
    public void Konstanten_Fangradien_und_Fangarten_in_der_Rangfolge()
    {
        Assert.Equal(0.03, M.FangRadiusFinger);
        Assert.Equal(0.03, M.FangRadiusStrahl);
        Assert.Equal(0.03, M.FangRadiusJeMeter);
        Gleich(["anschluss", "ecke", "mitte", "achse", "kante", "flaeche", "frei"], Fangart.Alle);
        Assert.True(Fangart.Alle.All(a => Fangart.Namen.ContainsKey(a)));
        Nah(M.AusModell([1000, 2000, 3000]).X, 1);
        Nah(M.AusModell([1000, 2000, 3000]).Y, 3);
        Nah(M.AusModell([1000, 2000, 3000]).Z, -2);
        Nah(M.AusModell([1000, 2000]).Y, 0);   // ohne Höhe
    }
}
