using Xunit.Abstractions;

namespace Hlks.Kern.Tests;

/// <summary>IFC einpassen (entwurf/ifc-einpassen.md), reine Rechnung auf 2D-Strecken (IfcEinpassen.Rechnen) mit synthetischen Grundrissen
/// (fiktiv): Datei = Grundriss des Projekts, um 37.5° gedreht und um (12.3 m, −45.6 m) verschoben (Projekt = R(θ)·Datei + t). Erwartung
/// wie in der Spezifikation: Drehung innert 0.01°, Verschiebung innert 5 mm; Teilmengen, zusätzliche Wände, Rauschen, Raumkanten auf
/// Wandflächen, symmetrisch → unsicher, leer → keine Merkmale, schiefwinklig. Dazu die Umrechnung θ/t → Bezug mit eigener Prüfung.</summary>
public class IfcEinpassenTests(ITestOutputHelper aus)
{
    const double Theta = 37.5, Tx = 12300, Ty = -45600;

    // ------------------------------------------------------------------ Grundrisse (fiktiv)

    /// <summary>Achsraster eines L-förmigen Grundrisses (mm): Räume als Rechtecke zwischen Wandachsen; Wanddicke 200 mm. Unregelmässige
    /// Achsabstände wie in echten Grundrissen (ein Raster aus lauter 4-m-Feldern passt auch um ein Feld verschoben zur Hälfte).</summary>
    static readonly (double X0, double Y0, double X1, double Y1)[] RaeumeAchsen =
    [
        (0, 0, 5600, 4700), (5600, 0, 9300, 4700), (9300, 0, 17400, 4700),
        (0, 4700, 3900, 11900), (3900, 4700, 9300, 8600), (9300, 4700, 13700, 8600), (3900, 8600, 13700, 11900),
    ];

    /// <summary>Wandachsen (Strecken) des L-Grundrisses: Aussenwände und Innenwände je einmal.</summary>
    static readonly (double Ax, double Ay, double Bx, double By)[] WaendeAchsen =
    [
        // aussen
        (0, 0, 17400, 0), (17400, 0, 17400, 4700), (17400, 4700, 13700, 4700), (13700, 4700, 13700, 11900), (13700, 11900, 0, 11900), (0, 11900, 0, 0),
        // innen
        (5600, 0, 5600, 4700), (9300, 0, 9300, 8600), (0, 4700, 13700, 4700), (3900, 4700, 3900, 11900), (3900, 8600, 13700, 8600),
    ];

    /// <summary>Grundriss in zwei Geschossen (Index 0 und 1). Räume: um die halbe Wanddicke eingerückt (Raumkanten auf den Wandflächen).</summary>
    static EinpassMerkmale Grundriss(bool waende, bool raeume, Func<double, double, (double, double)>? abb = null, Func<int, bool>? raumWahl = null, int geschosse = 2,
        double dicke = 200)
    {
        double Dicke = dicke;
        abb ??= (x, y) => (x, y);
        var m = new EinpassMerkmale();
        for (int g = 0; g < geschosse; g++)
        {
            if (waende)
                foreach (var (ax, ay, bx, by) in WaendeAchsen)
                {
                    var (a1, a2) = abb(ax, ay); var (b1, b2) = abb(bx, by);
                    m.Wand(a1, a2, b1, b2, Dicke, g);
                }
            if (raeume)
                for (int i = 0; i < RaeumeAchsen.Length; i++)
                {
                    if (raumWahl is not null && !raumWahl(i)) continue;
                    var (x0, y0, x1, y1) = RaeumeAchsen[i];
                    double e = Dicke / 2;
                    m.Raum(new[] { (x0 + e, y0 + e), (x1 - e, y0 + e), (x1 - e, y1 - e), (x0 + e, y1 - e) }.Select(p => { var (u, v) = abb(p.Item1, p.Item2); return new[] { u, v }; }).ToList(), g);
                }
        }
        return m;
    }

    /// <summary>Projekt → Datei: L = R(−θ)·(P − t), damit Projekt = R(θ)·L + t.</summary>
    static (double, double) ZurDatei(double x, double y) => Drehen(x - Tx, y - Ty, -Theta);

    static (double, double) Drehen(double x, double y, double grad)
    {
        double w = grad * Math.PI / 180;
        return (Math.Cos(w) * x - Math.Sin(w) * y, Math.Sin(w) * x + Math.Cos(w) * y);
    }

    void Zeigen(EinpassErgebnis e) =>
        aus.WriteLine($"{e.Ergebnis} θ={e.Drehung:0.0000}° t=({e.Tx:0.0}, {e.Ty:0.0}) a150={e.Anteil150:0.000} median={e.MedianMm:0.0} proben={e.Proben} " +
                      $"zweit={(e.Zweitbester is { } z ? $"{z.Anteil150:0.000} θ={z.Drehung:0.00}" : "–")} kandidaten={e.Kandidaten} {e.Vergleich} {e.RechenMs} ms {e.Grund}");

    static void LageIst(EinpassErgebnis e, double tolMm = 5, double tolGrad = 0.01)
    {
        Assert.Equal(IfcEinpassen.Eindeutig, e.Ergebnis);
        Assert.True(Math.Abs(IfcEinpassen.Winkel(e.Drehung - Theta)) <= tolGrad, $"Drehung {e.Drehung} statt {Theta}");
        Assert.True(Math.Abs(e.Tx - Tx) <= tolMm && Math.Abs(e.Ty - Ty) <= tolMm, $"Verschiebung ({e.Tx}, {e.Ty}) statt ({Tx}, {Ty})");
    }

    // ------------------------------------------------------------------ Grundfall und Paarungen

    [Theory]
    [InlineData(true, true, true, true)]     // Wände und Räume auf beiden Seiten
    [InlineData(true, false, true, false)]   // Wände ↔ Wände
    [InlineData(false, true, false, true)]   // Raumkanten ↔ Raumkanten
    [InlineData(true, false, false, true)]   // Raumkanten der Datei auf Wandflächen des Projekts (Bernstrasse: Datei ohne Wände)
    [InlineData(false, true, true, false)]   // Wände der Datei auf Raumkanten des Projekts
    public void Gedreht375_Verschoben_FindetDieLage(bool projektWaende, bool projektRaeume, bool dateiWaende, bool dateiRaeume)
    {
        var p = Grundriss(projektWaende, projektRaeume);
        var d = Grundriss(dateiWaende, dateiRaeume, ZurDatei);
        var e = IfcEinpassen.Rechnen(p, d, 2);
        Zeigen(e);
        LageIst(e);
        Assert.Equal("je Geschoss", e.Vergleich);
        // Wände der Datei auf Raumkanten allein: die Aussenseiten der Aussenwände haben kein Gegenstück (58.6 m von 205.2 m Wandflächen → 71 %)
        double erwartet = dateiWaende && !projektWaende ? 0.70 : 0.95;
        Assert.True(e.Anteil150 >= erwartet, $"Anteil {e.Anteil150}");
        Assert.True(e.MedianMm < 1, $"Median {e.MedianMm}");
        Assert.Equal(2, e.JeGeschoss.Count);
        Assert.True(e.Zweitbester is null || e.Zweitbester.Anteil150 <= IfcEinpassen.HoechstensZweiter * e.Anteil150);
    }

    [Fact]
    public void NurSechzigProzentDerRaeume_DateiOhneWaende()
    {
        var p = Grundriss(true, true);
        var d = Grundriss(false, true, ZurDatei, i => i % 5 is 0 or 2 or 4);   // 4 von 7 Räumen je Geschoss (57 %; 60 % der Fläche gibt das Raster nicht genauer her)
        Assert.Equal(8, d.Raeume);
        var e = IfcEinpassen.Rechnen(p, d, 2);
        Zeigen(e);
        LageIst(e);
    }

    [Fact]
    public void ZusaetzlicheWaendeInDerDatei_StoerenNicht()
    {
        var p = Grundriss(true, true);
        var d = Grundriss(true, true, ZurDatei);
        // fünf Wände, die das Projekt nicht hat (je Geschoss), u. a. ausserhalb des Gebäudes
        foreach (var (ax, ay, bx, by) in new[] { (1500.0, 1000.0, 1500.0, 4000.0), (20000.0, 0.0, 26000.0, 0.0), (20000.0, 0.0, 20000.0, 8000.0), (6500.0, 6000.0, 8500.0, 6000.0), (-5000.0, -3000.0, 3000.0, -3000.0) })
            for (int g = 0; g < 2; g++)
            {
                var (a1, a2) = ZurDatei(ax, ay); var (b1, b2) = ZurDatei(bx, by);
                d.Wand(a1, a2, b1, b2, 150, g);
            }
        var e = IfcEinpassen.Rechnen(p, d, 2);
        Zeigen(e);
        LageIst(e);
        Assert.True(e.Anteil150 < 1, "die fremden Wände treffen nichts");
    }

    [Fact]
    public void Rauschen20mm_LageInnertDerToleranz()
    {
        var p = Grundriss(true, true);
        var zufall = new Random(4711);   // feste Folge
        double R() => (zufall.NextDouble() * 2 - 1) * 20;
        var d = Grundriss(true, true, (x, y) => { var (u, v) = ZurDatei(x, y); return (u + R(), v + R()); });
        var e = IfcEinpassen.Rechnen(p, d, 2);
        Zeigen(e);
        // Rauschen ±20 mm je Eckpunkt: gemittelt über alle Kanten bleibt die Lage innert 5 mm / 0.01°
        LageIst(e);
        Assert.True(e.Anteil150 >= 0.95);
        Assert.True(e.MedianMm is > 1 and < 20, $"Median {e.MedianMm}");
    }

    [Theory]
    [InlineData(120.0)]
    [InlineData(200.0)]
    [InlineData(350.0)]   // halbe Dicke 175 mm > Grenze 150 mm: die Achse allein träfe die Raumkanten nicht
    public void RaumkantenAufWandflaechen_VersatzHalbeWanddicke(double dicke)
    {
        // Projekt nur Wände (Achse + Dicke → Flächen ± halbe Dicke), Datei nur Räume, deren Kanten auf den Wandflächen liegen. Die Stimmen
        // der Eckpunkte (Raumecken gegen Enden der Wandflächen) liegen dabei eine halbe Wanddicke neben der Lage – Feinsuche und ICP
        // dürfen nicht auf die andere Fläche derselben Wand springen.
        var p = Grundriss(true, false, dicke: dicke);
        var d = Grundriss(false, true, ZurDatei, dicke: dicke);
        var e = IfcEinpassen.Rechnen(p, d, 2);
        Zeigen(e);
        LageIst(e);
        Assert.True(e.Anteil150 >= 0.95);
    }

    // ------------------------------------------------------------------ unsicher, keine Merkmale

    [Fact]
    public void SymmetrischerGrundriss_Unsicher()
    {
        // Rechteck 12 × 8 m mit vier gleichen Räumen (2 × 2) – 180° gedreht passt alles genauso
        var p = new EinpassMerkmale();
        var d = new EinpassMerkmale();
        foreach (var (m, abb) in new[] { (p, (Func<double, double, (double, double)>)((x, y) => (x, y))), (d, ZurDatei) })
        {
            foreach (var (ax, ay, bx, by) in new[] { (0.0, 0.0, 12000.0, 0.0), (12000.0, 0.0, 12000.0, 8000.0), (12000.0, 8000.0, 0.0, 8000.0), (0.0, 8000.0, 0.0, 0.0), (6000.0, 0.0, 6000.0, 8000.0), (0.0, 4000.0, 12000.0, 4000.0) })
            {
                var (a1, a2) = abb(ax, ay); var (b1, b2) = abb(bx, by);
                m.Wand(a1, a2, b1, b2, 200, 0);
            }
            foreach (var (x0, y0) in new[] { (0.0, 0.0), (6000.0, 0.0), (0.0, 4000.0), (6000.0, 4000.0) })
                m.Raum(new[] { (x0 + 100, y0 + 100), (x0 + 5900, y0 + 100), (x0 + 5900, y0 + 3900), (x0 + 100, y0 + 3900) }.Select(q => { var (u, v) = abb(q.Item1, q.Item2); return new[] { u, v }; }).ToList(), 0);
        }
        var e = IfcEinpassen.Rechnen(p, d, 1);
        Zeigen(e);
        Assert.Equal(IfcEinpassen.Unsicher, e.Ergebnis);
        Assert.NotNull(e.Zweitbester);
        Assert.True(e.Zweitbester!.Anteil150 > IfcEinpassen.HoechstensZweiter * e.Anteil150);
        Assert.Contains("nächstbeste Lage", e.Grund);
        // die beste Lage ist eine der beiden gleichwertigen (θ oder θ + 180°)
        double rest = Math.Abs(IfcEinpassen.Winkel(e.Drehung - Theta));
        Assert.True(rest < 0.05 || Math.Abs(rest - 180) < 0.05, $"Drehung {e.Drehung}");
    }

    [Fact]
    public void LeeresProjekt_KeineMerkmale_UndLeereDatei_KeineMerkmale()
    {
        var e = IfcEinpassen.Rechnen(new EinpassMerkmale(), Grundriss(true, true, ZurDatei), 2);
        Assert.Equal(IfcEinpassen.KeineMerkmale, e.Ergebnis);
        Assert.Contains("Projekt", e.Grund);
        var e2 = IfcEinpassen.Rechnen(Grundriss(true, true), new EinpassMerkmale(), 2);
        Assert.Equal(IfcEinpassen.KeineMerkmale, e2.Ergebnis);
        Assert.Contains("Datei", e2.Grund);
    }

    [Fact]
    public void ZuWenigTreffer_Unsicher()
    {
        // eine einzige kurze Wand in der Datei: < 30 Treffer
        var p = Grundriss(true, true);
        var d = new EinpassMerkmale();
        var (a1, a2) = ZurDatei(5600, 0); var (b1, b2) = ZurDatei(5600, 2000);
        d.Wand(a1, a2, b1, b2, 200, 0);
        var e = IfcEinpassen.Rechnen(p, d, 2);
        Zeigen(e);
        Assert.Equal(IfcEinpassen.Unsicher, e.Ergebnis);
        Assert.Contains("Treffer", e.Grund);
    }

    // ------------------------------------------------------------------ schiefwinklig

    /// <summary>Raster aus Parallelogrammen: Kanten unter 0° und unter winkel (mm, je Feld 5 m × 4 m), dazu ein Flügel unter 0°/90°, damit
    /// die Lage nicht symmetrisch ist.</summary>
    static EinpassMerkmale Schief(double winkel, Func<double, double, (double, double)> abb)
    {
        var m = new EinpassMerkmale();
        double ux = Math.Cos(winkel * Math.PI / 180), uy = Math.Sin(winkel * Math.PI / 180);
        (double, double) P(double i, double j) => (i * 5000 + j * 4000 * ux, j * 4000 * uy);
        double[] A((double, double) q) { var (u, v) = abb(q.Item1, q.Item2); return [u, v]; }
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 2; j++)
            {
                if (i == 2 && j == 1) continue;   // Ecke fehlt: nicht symmetrisch
                m.Raum([A(P(i, j)), A(P(i + 1, j)), A(P(i + 1, j + 1)), A(P(i, j + 1))], 0);
            }
        // rechtwinkliger Anbau unten links
        m.Raum([A((-6000, -5000)), A((0, -5000)), A((0, 0)), A((-6000, 0))], 0);
        m.Raum([A((-6000, 0)), A((-3000, 0)), A((-3000, 3000)), A((-6000, 3000))], 0);
        return m;
    }

    [Theory]
    [InlineData(60.0)]
    [InlineData(75.0)]
    public void Schiefwinklig_FindetDieLage(double winkel)
    {
        var p = Schief(winkel, (x, y) => (x, y));
        var d = Schief(winkel, ZurDatei);
        var e = IfcEinpassen.Rechnen(p, d, 1);
        Zeigen(e);
        LageIst(e);
    }

    [Fact]
    public void OhneKlareHauptrichtung_DreiSpitzenJeSeite()
    {
        // vier etwa gleich starke Richtungen (0°, 20°, 40°, 65°; Umfänge 42, 38, 42, 46 m): keine Spitze erreicht 30 %
        var p = new EinpassMerkmale(); var d = new EinpassMerkmale();
        var rechtecke = new[] { (0.0, 0.0, 0.0), (9000.0, 2000.0, 20.0), (2000.0, 11000.0, 40.0), (14000.0, 12000.0, 65.0), (-8000.0, 6000.0, 20.0),
                                (-3000.0, -7000.0, 40.0), (20000.0, -5000.0, 65.0), (6000.0, 20000.0, 0.0) };
        foreach (var (m, abb) in new[] { (p, (Func<double, double, (double, double)>)((x, y) => (x, y))), (d, ZurDatei) })
            for (int r = 0; r < rechtecke.Length; r++)
            {
                var (ox, oy, w) = rechtecke[r];
                double b = 4000 + 700 * r, h = 3000 + 300 * r;   // verschieden gross
                m.Raum(new[] { (0.0, 0.0), (b, 0.0), (b, h), (0.0, h) }.Select(q => { var (u, v) = Drehen(q.Item1, q.Item2, w); var (s, t) = abb(u + ox, v + oy); return new[] { s, t }; }).ToList(), 0);
            }
        var sp = IfcEinpassen.Spitzen(p.Kanten);
        Assert.True(sp[0].Anteil < IfcEinpassen.KlareSpitze, $"Spitze {sp[0].Anteil}");
        var e = IfcEinpassen.Rechnen(p, d, 1);
        Zeigen(e);
        LageIst(e);
    }

    // ------------------------------------------------------------------ Geschosse

    [Fact]
    public void Geschosse_ZuordnungNachUnterkante()
    {
        var g = new List<EinpassGeschoss> { new("UG", -2700), new("EG", 0), new("1.OG", 2900) };
        Assert.Equal(0, IfcEinpassen.GeschossIndex(g, -2700));
        Assert.Equal(0, IfcEinpassen.GeschossIndex(g, -1001));    // −1001 + 1000 < 0 → UG
        Assert.Equal(1, IfcEinpassen.GeschossIndex(g, -1000));    // −1000 + 1000 = 0 ≤ 0 → EG (Wand unter der Decke)
        Assert.Equal(1, IfcEinpassen.GeschossIndex(g, 1899));
        Assert.Equal(2, IfcEinpassen.GeschossIndex(g, 1900));
        Assert.Equal(0, IfcEinpassen.GeschossIndex(g, -9000));    // darunter: das unterste
        Assert.Equal(2, IfcEinpassen.GeschossIndex(g, 50000));
        Assert.Equal(-1, IfcEinpassen.GeschossIndex([], 0));
    }

    [Fact]
    public void OhneGeschosse_AllesZusammen_MitHinweis()
    {
        var p = Grundriss(true, true, geschosse: 1);
        var d = Grundriss(false, true, ZurDatei, geschosse: 1);
        static EinpassMerkmale OhneG(EinpassMerkmale m) { var o = new EinpassMerkmale(); foreach (var k in m.Kanten) o.Kanten.Add(k with { Geschoss = -1 }); return o; }
        var e = IfcEinpassen.Rechnen(OhneG(p), OhneG(d), 0);
        Zeigen(e);
        LageIst(e);
        Assert.True(e.Gemeinsam);
        Assert.Contains("keine Geschosse", e.Vergleich);
    }

    [Fact]
    public void ZuWenigMerkmaleJeGeschoss_AllesZusammen()
    {
        // Datei-Merkmale stehen im Geschoss 1, das Projekt hat dort nichts (Geschosskoten der Datei anders): alles zusammen
        var p = Grundriss(true, true, geschosse: 1);
        var d = new EinpassMerkmale();
        foreach (var k in Grundriss(false, true, ZurDatei, geschosse: 1).Kanten) d.Kanten.Add(k with { Geschoss = 1 });
        var e = IfcEinpassen.Rechnen(p, d, 2);
        Zeigen(e);
        LageIst(e);
        Assert.True(e.Gemeinsam);
        Assert.Contains("zu wenig Merkmale je Geschoss", e.Vergleich);
    }

    [Fact]
    public void JeGeschoss_VerwechseltDieGeschosseNicht()
    {
        // EG und OG verschieden: die Datei hat nur das OG. Je Geschoss verglichen findet sie die Lage im OG.
        var p = Grundriss(true, true, geschosse: 1);   // EG (0) = L-Grundriss
        foreach (var k in Grundriss(true, false, (x, y) => (x + 30000, y), geschosse: 1).Kanten) p.Kanten.Add(k with { Geschoss = 1 });   // OG (1) woanders
        var d = new EinpassMerkmale();
        foreach (var k in Grundriss(true, false, (x, y) => ZurDatei(x + 30000, y), geschosse: 1).Kanten) d.Kanten.Add(k with { Geschoss = 1 });
        var e = IfcEinpassen.Rechnen(p, d, 2);
        Zeigen(e);
        Assert.Equal(IfcEinpassen.Eindeutig, e.Ergebnis);
        Assert.Equal("je Geschoss", e.Vergleich);
        Assert.True(Math.Abs(IfcEinpassen.Winkel(e.Drehung - Theta)) <= 0.01);
        Assert.True(Math.Abs(e.Tx - Tx) <= 5 && Math.Abs(e.Ty - Ty) <= 5);
        Assert.Single(e.JeGeschoss);
        Assert.Equal(1, e.JeGeschoss[0].Geschoss);
    }

    // ------------------------------------------------------------------ Bezug

    [Fact]
    public void Bezug_AusDrehungUndVerschiebung_PunktDerDateiLandetAmErwartetenOrt()
    {
        // Gebäudeursprung der Datei B0 = (1000, 2000, 500), d0 = 10°; gefunden: Projekt = R(37.5°)·L + (12300, −45600)
        var b0 = new IfcBezug(1000, 2000, 500, 10);
        var b = IfcEinpassen.Bezug(b0, Theta, Tx, Ty);
        Assert.Equal(10 - Theta, b.Drehung, 9);   // d = d0 − θ
        Assert.Equal(500, b.Z, 9);                // z = B0.z
        foreach (var (lx, ly, lz) in new[] { (0.0, 0.0, 0.0), (5000.0, -3000.0, 2900.0), (-12000.0, 800.0, -2700.0) })
        {
            // Lage «Gebäudeursprung» L → Welt der Datei: W = B0 + R(d0)·L
            var w = Matrix4.Verschiebung(b0.X, b0.Y, b0.Z) * Matrix4.DrehungZ(b0.Drehung);
            var welt = w.Punkt([lx, ly, lz]);
            // mit dem neuen Bezug zurück ins Projekt (Konvention IfcDatei.ZuModell)
            var modell = (Matrix4.DrehungZ(-b.Drehung) * Matrix4.Verschiebung(-b.X, -b.Y, -b.Z)).Punkt(welt);
            var (ex, ey) = Drehen(lx, ly, Theta);
            Assert.Equal(ex + Tx, modell[0], 6);
            Assert.Equal(ey + Ty, modell[1], 6);
            Assert.Equal(lz, modell[2], 6);
        }
    }

    [Fact]
    public void Bezug_BeispielDerSpezifikation()
    {
        // Lagebericht-Beispiel in entwurf/ifc-einpassen.md: drehung −42.7182, verschiebungMm [30.2, 16.1], Gebäudeursprung (0, 0, 583690, 0)
        // → bezug { x: −11.3, y: −32.3, z: 583690, drehung: 42.7182 }
        var b = IfcEinpassen.Bezug(new IfcBezug(0, 0, 583690, 0), -42.7182, 30.2, 16.1);
        Assert.Equal(-11.3, b.X, 1);
        Assert.Equal(-32.3, b.Y, 1);
        Assert.Equal(583690, b.Z, 6);
        Assert.Equal(42.7182, b.Drehung, 6);
    }

    [Fact]
    public void Spitzen_HauptrichtungAufEinHundertstelGrad()
    {
        var d = Grundriss(true, true, ZurDatei);
        var s = IfcEinpassen.Spitzen(d.Kanten);
        // Datei-Kanten liegen unter −37.5° bzw. 52.5° → modulo 90: 52.5°
        Assert.Equal(52.5, s[0].Winkel, 2);
        Assert.True(s[0].Anteil > 0.99);
    }

    [Fact]
    public void Laufzeit_GrossesModell_InWenigenSekunden()
    {
        // 30 × 20 Raster aus Räumen in drei Geschossen (≈ 7200 Raumkanten, 3700 Wände je Seite): Stichproben begrenzen die Rechnung
        var p = new EinpassMerkmale(); var d = new EinpassMerkmale();
        foreach (var (m, abb) in new[] { (p, (Func<double, double, (double, double)>)((x, y) => (x, y))), (d, ZurDatei) })
            for (int g = 0; g < 3; g++)
                for (int i = 0; i < 30; i++)
                    for (int j = 0; j < 20; j++)
                    {
                        if ((i * 7 + j * 3 + g) % 11 == 0) continue;   // Lücken: nicht periodisch
                        double x0 = i * 4000 + (j % 3) * 300, y0 = j * 3500;
                        m.Raum(new[] { (x0, y0), (x0 + 3800, y0), (x0 + 3800, y0 + 3300), (x0, y0 + 3300) }.Select(q => { var (u, v) = abb(q.Item1, q.Item2); return new[] { u, v }; }).ToList(), g);
                        var (a1, a2) = abb(x0 - 100, y0 - 100); var (b1, b2) = abb(x0 + 3900, y0 - 100);
                        m.Wand(a1, a2, b1, b2, 200, g);
                    }
        var uhr = System.Diagnostics.Stopwatch.StartNew();
        var e = IfcEinpassen.Rechnen(p, d, 3);
        Zeigen(e);
        aus.WriteLine($"{p.Kanten.Count} Projektkanten, {d.Kanten.Count} Dateikanten, {uhr.ElapsedMilliseconds} ms");
        Assert.Equal(IfcEinpassen.MaxProben, e.Proben);
        Assert.True(Math.Abs(IfcEinpassen.Winkel(e.Drehung - Theta)) <= 0.01);
        Assert.True(uhr.Elapsed.TotalSeconds < 20, $"{uhr.Elapsed.TotalSeconds:0.0} s");   // Zeitgrenze unter Last grosszügig
    }
}
