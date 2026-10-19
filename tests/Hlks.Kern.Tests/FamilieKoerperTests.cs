using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.FamilieTestHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 2, B1: Grundkörper als Netz (kegel, kugel, extrusion mit Wölbung und Löchern, rotation, sweep), geschlossen
/// und aussen orientiert, Triangulierung, Flächen und Volumen mit Handrechnung. Facettierung: Kreise als 48-Eck, Bögen
/// höchstens 7.5° je Strecke – die Toleranzen nennen die Abweichung durch die Facetten. Alle Masse frei gewählt.</summary>
public class FamilieKoerperTests
{
    static void Geschlossen(Netz3 n)
    {
        Assert.Null(n.GeschlossenFehler());
        Assert.True(n.Volumen() > 0, "Normalen zeigen nach innen");
    }

    /// <summary>Euler-Charakteristik V − E + F (2 = kugelartig, 0 = ein Loch).</summary>
    static int Euler(Netz3 n)
    {
        var kanten = new HashSet<(int, int)>();
        foreach (var t in n.Dreiecke) for (int k = 0; k < 3; k++) { int a = t[k], b = t[(k + 1) % 3]; kanten.Add(a < b ? (a, b) : (b, a)); }
        var benutzt = n.Dreiecke.SelectMany(t => t).Distinct().Count();
        return benutzt - kanten.Count + n.Dreiecke.Count;
    }

    /// <summary>Fläche der Dreiecke, deren Normale in Richtung (0, 0, 1) zeigt (Deckfläche einer Extrusion in xy).</summary>
    static double Deckflaeche(Netz3 n)
    {
        double s = 0;
        foreach (var t in n.Dreiecke)
        {
            var a = V3.Aus(n.Ecken[t[0]]); var b = V3.Aus(n.Ecken[t[1]]); var c = V3.Aus(n.Ecken[t[2]]);
            var nn = (b - a).Kreuz(c - a);
            if (nn.Z > 0 && Math.Abs(nn.X) < 1e-9 && Math.Abs(nn.Y) < 1e-9) s += nn.Z / 2;
        }
        return s;
    }

    // ------------------------------------------------------------------ Konturen und Triangulierung

    [Fact]
    public void Facetten_HalbkreiseAusWoelbungErgebenEin48Eck()
    {
        // zwei Punkte mit Wölbung 1 (= tan(180°/4)): zwei Halbkreise, Kreis Ø 100 um (50, 0)
        var k = NetzBau.Facetten([(0, 0, 1), (100, 0, 1)]);
        Assert.Equal(48, k.Count);   // je Halbkreis 24 Strecken (180° / 7.5°)
        Assert.All(k, p => Etwa(50, Math.Sqrt((p.U - 50) * (p.U - 50) + p.V * p.V), 1e-9));
        Etwa(NEck(50), NetzBau.Flaeche(k), 1e-6);   // Gegenuhrzeigersinn (positive Wölbung)
        var viertel = NetzBau.Facetten([(100, 0, Math.Tan(Math.PI / 8)), (0, 100, 0), (0, 0, 0)]);   // Viertelkreis r 100 um (0, 0)
        Assert.Equal(12 + 2, viertel.Count);
        Etwa(0.5 * 12 * 100 * 100 * Math.Sin(Math.PI / 24), NetzBau.Flaeche(viertel), 1e-6);
    }

    [Fact]
    public void Triangulierung_QuadratMitLochUndEinspringendeEcken()
    {
        // Quadrat 100 × 100 mit Loch 50 × 50 in der Mitte; L-Form als zweiter Fall
        var p = new List<(double X, double Y)> { (0, 0), (100, 0), (100, 100), (0, 100), (25, 25), (25, 75), (75, 75), (75, 25) };
        var d = Triangulierung.Dreiecke(p, [0, 1, 2, 3], [[4, 5, 6, 7]]);
        double Flaeche(int[] t) => ((p[t[1]].X - p[t[0]].X) * (p[t[2]].Y - p[t[0]].Y) - (p[t[1]].Y - p[t[0]].Y) * (p[t[2]].X - p[t[0]].X)) / 2;
        Assert.All(d, t => Assert.True(Flaeche(t) > 0, "Dreieck nicht im Gegenuhrzeigersinn"));
        Etwa(10000 - 2500, d.Sum(Flaeche), 1e-9);
        Assert.Equal(8, d.Count);   // n + 2·Löcher − 2 = 8 + 2 − 2
        var l = new List<(double X, double Y)> { (0, 0), (200, 0), (200, 100), (100, 100), (100, 200), (0, 200) };
        var dl = Triangulierung.Dreiecke(l, [5, 4, 3, 2, 1, 0], []);   // im Uhrzeigersinn übergeben
        double Fl(int[] t) => ((l[t[1]].X - l[t[0]].X) * (l[t[2]].Y - l[t[0]].Y) - (l[t[1]].Y - l[t[0]].Y) * (l[t[2]].X - l[t[0]].X)) / 2;
        Etwa(30000, dl.Sum(Fl), 1e-9);
        Assert.All(dl, t => Assert.True(Fl(t) > 0));
    }

    [Fact]
    public void Triangulierung_GestreckteEckeBleibtImNetz()
    {
        // Rechteck mit einem Zwischenpunkt auf der Unterkante: das Netz der Extrusion muss trotzdem geschlossen sein
        var n = NetzBau.Extrusion("xy", [(0, 0), (100, 0), (200, 0), (200, 100), (0, 100)], [], 0, 50);
        Geschlossen(n);
        Etwa(200 * 100 * 50, n.Volumen(), 1e-4);
    }

    // ------------------------------------------------------------------ Extrusion

    [Fact]
    public void Extrusion_MitRundemLoch_FlaecheUndVolumenWieHandrechnung()
    {
        // Quadrat 200 × 200, Loch Ø 100 in der Mitte (Halbkreis und zwei Viertelkreise per Wölbung, tan(180°/4) = 1 bzw.
        // tan(90°/4) = 0.4142), von z 0 bis 300.
        // Handrechnung (Facetten): Fläche = 200² − 48-Eck(r 50) = 40 000 − ½·48·50²·sin(7.5°) = 40 000 − 7 831.5 = 32 168.5 mm²
        // (exakt mit Kreis: 40 000 − π·50² = 32 146.0 mm²; Abweichung durch Facetten 0.07 %), Volumen = Fläche · 300.
        var f = Def(Mini(koerper: """
            [{ "id": "platte", "art": "extrusion", "ebene": "xy", "kontur": [[0, 0], [200, 0], [200, 200], [0, 200]],
               "loecher": [[[50, 100, 1], [150, 100, 0.41421356237309503], [100, 150, 0.41421356237309503]]], "von": 0, "bis": 300 }]
            """));
        Assert.True(f.Gueltig, string.Join(" | ", f.Fehler));
        var t = FamilienAuswertung.Aufloesen(f, "A", null).Teile("mittel").Single();
        Assert.Equal("koerper", t.Art);
        var n = FamilienKoerper.AlsNetz(t);
        Geschlossen(n);
        Assert.Equal(0, Euler(n));   // ein Loch: Torus-artig
        double flaeche = 40000 - NEck(50);
        Etwa(flaeche, Deckflaeche(n), 1e-6, "Deckfläche");
        Etwa(flaeche * 300, n.Volumen(), 1e-6 * flaeche * 300, "Volumen");
        Assert.True(Math.Abs(Deckflaeche(n) - (40000 - Math.PI * 2500)) / (40000 - Math.PI * 2500) < 0.001);
        Etwa([0, 0, 0, 200, 200, 300], n.Ausdehnung(), 1e-9);
    }

    [Theory]
    [InlineData("xz")]
    [InlineData("yz")]
    public void Extrusion_InAnderenEbenenGeschlossenUndAussenOrientiert(string ebene)
    {
        // Dreieck (0,0)-(300,0)-(0,400) in der Ebene, Dicke 50 → Volumen ½·300·400·50 = 3 000 000 mm³
        var n = NetzBau.Extrusion(ebene, [(0, 0), (300, 0), (0, 400)], [], -25, 25);
        Geschlossen(n);
        Etwa(3_000_000, n.Volumen(), 1e-3);
        var a = n.Ausdehnung();
        if (ebene == "xz") Etwa([0, -25, 0, 300, 25, 400], a, 1e-9);
        else Etwa([-25, 0, 0, 25, 300, 400], a, 1e-9);
    }

    [Fact]
    public void Extrusion_MitFormelnAusDenParametern()
    {
        var f = Def(Mini("""[{ "name": "b", "art": "typ", "datentyp": "laenge", "wert": 300 }, { "name": "t", "art": "typ", "datentyp": "laenge", "wert": 40 }]""",
            """[{ "name": "A", "werte": {} }, { "name": "B", "werte": { "b": 500 } }]""",
            """[{ "id": "k", "art": "extrusion", "ebene": "xz", "kontur": [[0, 0], ["b", 0], ["b", "b / 2"], [0, "b / 2"]], "von": 0, "bis": "t" }]"""));
        Assert.True(f.Gueltig, string.Join(" | ", f.Fehler));
        Etwa(300 * 150 * 40, FamilienKoerper.AlsNetz(FamilienAuswertung.Aufloesen(f, "A", null).Teile("fein")[0]).Volumen(), 1e-3);
        Etwa(500 * 250 * 40, FamilienKoerper.AlsNetz(FamilienAuswertung.Aufloesen(f, "B", null).Teile("fein")[0]).Volumen(), 1e-3);
    }

    // ------------------------------------------------------------------ Rotation

    [Fact]
    public void Rotation_Vollzylinder_VolumenMitFacettierung()
    {
        // Rechteck r 0…100, a 0…200 um die v-Achse durch (0, 0) in xz → Zylinder um z, r 100, h 200.
        // Handrechnung: 48-Eck-Prisma V = ½·48·100²·sin(7.5°)·200 = 6 265 247 mm³; Kreis π·100²·200 = 6 283 185 mm³ (−0.29 %).
        var n = NetzBau.Rotation("xz", [(0, 0), (100, 0), (100, 200), (0, 200)], (0, 0), "v", 360);
        Geschlossen(n);
        Assert.Equal(2, Euler(n));
        Etwa(NEck(100) * 200, n.Volumen(), 1e-2);
        Assert.True(Math.Abs(n.Volumen() / (Math.PI * 100 * 100 * 200) - 1) < 0.003, "Facettierung über 0.3 %");
        Etwa([-100, -100, 0, 100, 100, 200], n.Ausdehnung(), 1e-6);
    }

    [Fact]
    public void Rotation_Torus_VolumenInnerhalbDerFacettierung()
    {
        // Kreis r 50 (zwei Halbkreise) um (200, 0) in xy, gedreht um die v-Achse (= y) → Torus R 200, r 50.
        // Pappus: V = 2π·R·π·r² = 2π²·200·2500 = 9 869 604 mm³; Facetten in Profil und Umlauf (je 48) zusammen unter 1 %.
        var profil = NetzBau.Facetten([(150, 0, 1), (250, 0, 1)]);
        var n = NetzBau.Rotation("xy", profil, (0, 0), "v", 360);
        Geschlossen(n);
        Assert.Equal(0, Euler(n));
        var exakt = 2 * Math.PI * Math.PI * 200 * 2500;
        Assert.True(Math.Abs(n.Volumen() / exakt - 1) < 0.01, $"Torus {n.Volumen()} statt {exakt}");
    }

    [Fact]
    public void Rotation_Teilwinkel_MitDeckeln()
    {
        // Ring r 50…100, Höhe 100, 90° um die v-Achse (xz → um z): 12 Segmente zu 7.5°.
        // Handrechnung: V = 12 · ½·sin(7.5°)·(100² − 50²) · 100 = 5 874 sowie exakt (π/4)·(100² − 50²)·100 = 589 049 mm³.
        var n = NetzBau.Rotation("xz", [(50, 0), (100, 0), (100, 100), (50, 100)], (0, 0), "v", 90);
        Geschlossen(n);
        Etwa(12 * 0.5 * Math.Sin(Math.PI / 24) * (10000 - 2500) * 100, n.Volumen(), 1e-2);
        Assert.True(Math.Abs(n.Volumen() / (Math.PI / 4 * 7500 * 100) - 1) < 0.003);
        // Drehsinn rechtshändig um +z: von +x nach +y
        var a = n.Ausdehnung();
        Etwa(0, a[0], 1e-6); Etwa(0, a[1], 1e-6); Etwa(100, a[3], 1e-6); Etwa(100, a[4], 1e-6);
    }

    [Fact]
    public void Rotation_KonturAufBeidenSeitenDerAchseIstFehler()
    {
        var f = Assert.Throws<HlksFehler>(() => NetzBau.Rotation("xz", [(-10, 0), (100, 0), (100, 100)], (0, 0), "v", 360));
        Assert.Contains("beiden Seiten", f.Message);
        Assert.NotNull(f.Vorschlag);
    }

    // ------------------------------------------------------------------ Sweep

    [Fact]
    public void Sweep_UmEineEcke_GehrungErhaeltDasVolumen()
    {
        // Kreisprofil r 50 entlang (0,0,0) → (1000,0,0) → (1000,1000,0), ohne Biegeradius: Gehrung in der Winkelhalbierenden.
        // Handrechnung: Bei Gehrung gleichen sich die Keile aus, V = 48-Eck(r 50) · Mittellinie 2000 = 7 831.5 · 2000 mm³.
        var n = NetzBau.Sweep([[0, 0, 0], [1000, 0, 0], [1000, 1000, 0]], 50, null, null);
        Geschlossen(n);
        Assert.Equal(2, Euler(n));
        Etwa(NEck(50) * 2000, n.Volumen(), 1e-1);
        Etwa([0, -50, -50, 1050, 1000, 50], n.Ausdehnung(), 1e-6);   // äussere Ecke der Gehrung bei (1050, −50)
    }

    [Fact]
    public void Sweep_MitBiegeradius_BogenStattEcke()
    {
        // wie oben, Biegeradius 200: gerade Stücke je 1000 − 200 = 800, Bogen ¼·2π·200 = 314.16 (als 12 Sehnen zu 7.5°:
        // 12·2·200·sin(3.75°) = 313.94). V ≈ 48-Eck · (1600 + 313.94); Gehrungen an den 12 kleinen Knicken gleichen sich aus.
        var n = NetzBau.Sweep([[0, 0, 0], [1000, 0, 0], [1000, 1000, 0]], 50, null, 200);
        Geschlossen(n);
        Etwa(NEck(50) * (1600 + 12 * 2 * 200 * Math.Sin(Math.PI / 48)), n.Volumen(), 1);
        var a = n.Ausdehnung();
        Etwa(1050, a[3], 1e-6);   // Bogen läuft nicht über die Ecke hinaus
        Assert.True(a[0] > -1e-6 && a[1] > -50 - 1e-6);
        // die Ecke (1000, 0) selbst liegt ausserhalb des gebogenen Rohrs: Mittelpunkt des Bogens (800, 200), Abstand 200·√2 − 200 = 82.8 > 50
    }

    [Fact]
    public void Sweep_Rechteckprofil_HochkantUndQuer()
    {
        // Rechteck b 100 × h 50 entlang x: h senkrecht (z), b quer (y) – Annahme im Dateikopf von FamilienNetz.cs
        var n = NetzBau.Sweep([[0, 0, 0], [500, 0, 0]], null, (100, 50), null);
        Geschlossen(n);
        Etwa([0, -50, -25, 500, 50, 25], n.Ausdehnung(), 1e-9);
        Etwa(100 * 50 * 500, n.Volumen(), 1e-4);
        var senkrecht = NetzBau.Sweep([[0, 0, 0], [0, 0, 500]], null, (100, 50), null);
        Etwa([-50, -25, 0, 50, 25, 500], senkrecht.Ausdehnung(), 1e-9);   // senkrechter Pfad: h in Richtung y
    }

    [Fact]
    public void Sweep_BiegeradiusZuGrossIstFehler() =>
        Assert.Contains("zu gross", Assert.Throws<HlksFehler>(() => NetzBau.Sweep([[0, 0, 0], [100, 0, 0], [100, 100, 0]], 10, null, 500)).Message);

    // ------------------------------------------------------------------ Kegel, Kugel, Orientierung

    [Fact]
    public void Kegel_SpitzeUndStumpf()
    {
        // Spitzkegel r 100, h 300: 48-Eck-Pyramide V = ⅓ · 48-Eck(100) · 300
        var spitz = NetzBau.Kegel([0, 0, 0], [0, 0, 300], 100, 0);
        Geschlossen(spitz);
        Etwa(NEck(100) * 300 / 3, spitz.Volumen(), 1e-2);
        // Stumpf r 100 → 50, h 300, schräge Achse: V = h/3 · (A1 + A2 + √(A1·A2)) mit den 48-Eck-Flächen
        var stumpf = NetzBau.Kegel([0, 0, 0], [300, 0, 0], 100, 50);
        Geschlossen(stumpf);
        double a1 = NEck(100), a2 = NEck(50);
        Etwa(300.0 / 3 * (a1 + a2 + Math.Sqrt(a1 * a2)), stumpf.Volumen(), 1e-2);
    }

    [Fact]
    public void Kugel_VolumenInnerhalbDerFacettierung()
    {
        var n = NetzBau.Kugel([10, 20, 30], 100);
        Geschlossen(n);
        Assert.Equal(2, Euler(n));
        Assert.True(Math.Abs(n.Volumen() / (4.0 / 3 * Math.PI * 1e6) - 1) < 0.02, "Kugel: Facettierung über 2 %");
        Etwa([-90, -80, -70, 110, 120, 130], n.Ausdehnung(), 1e-6);
    }

    [Fact]
    public void Orientieren_RichtetVerdrehteDreieckeAus()
    {
        var n = NetzBau.Quader([0, 0, 0], [100, 200, 300]);
        Geschlossen(n);
        foreach (var i in new[] { 0, 3, 7 }) (n.Dreiecke[i][1], n.Dreiecke[i][2]) = (n.Dreiecke[i][2], n.Dreiecke[i][1]);
        Assert.NotNull(n.GeschlossenFehler());
        n.Orientieren();
        Geschlossen(n);
        Etwa(100 * 200 * 300, n.Volumen(), 1e-4);
        n.Umkehren();
        n.Orientieren();
        Etwa(100 * 200 * 300, n.Volumen(), 1e-4);
    }

    // ------------------------------------------------------------------ Testfamilie: Körper je Detailgrad

    [Fact]
    public void Testfamilie_DetailgradUndSichtbarkeit()
    {
        var f = Def(Testheizkoerper());
        var a = FamilienAuswertung.Aufloesen(f, "22-600-1000", null);
        Assert.True(a.Gueltig, string.Join(" | ", a.Fehler));
        // grob: nur die Heizplatten (Abdeckung mittel/fein, Stutzen fein)
        Assert.Equal(["platten"], a.Sichtbare("grob").Select(k => k.Def.Id));
        Assert.Equal(["platten", "abdeckung"], a.Sichtbare("mittel").Select(k => k.Def.Id));
        Assert.Equal(["platten", "abdeckung", "stutzen_vl", "stutzen_rl"], a.Sichtbare("fein").Select(k => k.Def.Id));
        // 22-900-800: mit_abdeckung false → Abdeckung in keinem Detailgrad
        var b = FamilienAuswertung.Aufloesen(f, "22-900-800", null);
        Assert.Equal(["platten"], b.Sichtbare("mittel").Select(k => k.Def.Id));
        Assert.Equal(["platten", "stutzen_vl", "stutzen_rl"], b.Sichtbare("fein").Select(k => k.Def.Id));
        // Platten 22-600-1000: von [0, 0, 150] bis [1000, 100, 750] → Mitte (500, 50, 450), Masse 1000 × 100 × 600
        var platten = a.Teile("grob").Single();
        Assert.Equal("quader", platten.Art);
        Etwa([500, 50, 450], platten.Mitte!);
        Etwa([1000, 100, 600], platten.Masse!);
        // Stutzen vl: Zylinder von (950, 50, 150) nach (950, 50, 120), r 10
        var stutzen = a.Teile("fein")[2];
        Assert.Equal("zylinder", stutzen.Art);
        Etwa([950, 50, 150], stutzen.Von!);
        Etwa([950, 50, 120], stutzen.Nach!);
        Assert.Equal(10, stutzen.Radius);
    }

    [Fact]
    public void Platzieren_QuaderZylinderUndNetzWerdenGedrehtUndVerschoben()
    {
        // Drehung 90° um z: (x, y) → (−y, x); Einfügepunkt (5000, 3000, 100)
        var q = FamilienKoerper.Platzieren(new Koerperteil { Art = "quader", Mitte = [500, 50, 450], Masse = [1000, 100, 600], Drehung = 0 }, [5000, 3000, 100], 90, null);
        Etwa([4950, 3500, 550], q.Mitte!, 1e-9);
        Assert.Equal(90, q.Drehung);
        var z = FamilienKoerper.Platzieren(new Koerperteil { Art = "zylinder", Von = [950, 50, 150], Nach = [950, 50, 120], Radius = 10 }, [5000, 3000, 0], 90, "#d0312d");
        Etwa([4950, 3950, 150], z.Von!, 1e-9);
        Assert.Equal("#d0312d", z.Farbe);
        var n = NetzBau.Quader([0, 0, 0], [100, 10, 10]);
        var k = FamilienKoerper.Platzieren(new Koerperteil { Art = "koerper", Ecken = n.Ecken, Dreiecke = n.Dreiecke }, [0, 0, 0], 90, null);
        var box = new Netz3(); box.Ecken.AddRange(k.Ecken!); box.Dreiecke.AddRange(k.Dreiecke!);
        Etwa([-10, 0, 0, 0, 100, 10], box.Ausdehnung(), 1e-9);
        Assert.True(box.Volumen() > 0);   // Drehung erhält die Orientierung
    }
}
