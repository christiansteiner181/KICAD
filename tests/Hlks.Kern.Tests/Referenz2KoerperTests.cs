using System.Text.Json.Nodes;

namespace Hlks.Kern.Tests;

/// <summary>Modul 2, Auftrag B6: unabhängige Referenzfälle von Hand – Körper (Quader, Zylinder, Kegelstumpf, Kugel, Extrusion mit
/// Loch, Kreis aus Wölbungspunkten, Rotation nach Pappus, Sweep). Volumen und Flächen sind aus den Formeln der Geometrie gerechnet,
/// nicht mit dem Netz. Die Prüfung des Netzes (Volumen, Oberfläche, geschlossen, aussen) rechnet der Test selbst aus Ecken und
/// Dreiecken, nicht mit Netz3.Volumen()/GeschlossenFehler().
///
/// TOLERANZ AUS DER FACETTIERUNG (selbst begründet). Ein Kreis wird als regelmässiges 48-Eck mit Ecken auf dem Kreis gezeichnet
/// (7.5° je Strecke). Seine Fläche ist ½ · 48 · r² · sin(7.5°) = 24 · sin(7.5°) · r² = 24 · 0.13052619 · r² = 3.1326286 · r²
/// statt π r² = 3.1415927 · r²: Verhältnis 0.997147, das 48-Eck liegt also 0.285 % UNTER dem Kreis. Alle Körper, deren Querschnitt
/// ein Kreis ist (Zylinder, Kegelstumpf, Rotation, Sweep), haben das Volumen des Kreiskörpers mal 0.997147 (Prismen und Stümpfe
/// mit dem 48-Eck als Querschnitt, Flächenverhältnis bleibt unter Skalierung gleich). Erlaubt wird deshalb: mindestens 0.3 %
/// UNTER dem exakten Wert, nie darüber (einbeschriebenes Vieleck). Kugel: zwei Facettierungen (24 Streifen über 180° und 48 in der
/// Länge) geben ≈ 0.285 % + 0.285 % ≈ 0.57 %, von Hand mit Rotationsstumpfsummen gerechnet 0.71 % → erlaubt 1 %. Sweep mit
/// Biegung: der Bogen wird mit 12 Sehnen zu 7.5° ersetzt, Länge 12 · 2 · 200 · sin(3.75°) = 313.935 statt 314.159 (−0.07 %),
/// zusammen mit 0.285 % also höchstens 0.4 %.</summary>
public class Referenz2KoerperTests
{
    // ------------------------------------------------------------------ eigene Netzrechnung (unabhängig von Netz3)

    static double[] Sub(double[] a, double[] b) => [a[0] - b[0], a[1] - b[1], a[2] - b[2]];
    static double[] Kreuz(double[] a, double[] b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
    static double Mal(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];

    /// <summary>Volumen aus der Divergenz: Summe der Tetraeder (Ursprung, a, b, c) = ⅙ · a · (b × c), positiv bei Normalen nach aussen.</summary>
    static double Vol(Netz3 n) =>
        n.Dreiecke.Sum(t => Mal(n.Ecken[t[0]], Kreuz(n.Ecken[t[1]], n.Ecken[t[2]]))) / 6;

    static double Oberflaeche(Netz3 n) =>
        n.Dreiecke.Sum(t => Math.Sqrt(Mal(Kreuz(Sub(n.Ecken[t[1]], n.Ecken[t[0]]), Sub(n.Ecken[t[2]], n.Ecken[t[0]])), Kreuz(Sub(n.Ecken[t[1]], n.Ecken[t[0]]), Sub(n.Ecken[t[2]], n.Ecken[t[0]])))) / 2);

    /// <summary>Jede Kante gehört genau zwei Dreiecken, in je einer Richtung durchlaufen (geschlossen und einheitlich orientiert).
    /// Liefert die Zahl der Kanten.</summary>
    static int Geschlossen(Netz3 n)
    {
        var gerichtet = new HashSet<(int, int)>();
        var proKante = new Dictionary<(int, int), int>();
        foreach (var t in n.Dreiecke)
            for (int k = 0; k < 3; k++)
            {
                int a = t[k], b = t[(k + 1) % 3];
                Assert.True(gerichtet.Add((a, b)), $"Kante {a}→{b} wird zweimal in derselben Richtung durchlaufen");
                var key = a < b ? (a, b) : (b, a);
                proKante[key] = proKante.GetValueOrDefault(key) + 1;
            }
        Assert.All(proKante, kv => Assert.True(kv.Value == 2, $"Kante {kv.Key} gehört {kv.Value} Dreiecken statt genau zwei"));
        Assert.All(gerichtet, g => Assert.True(gerichtet.Contains((g.Item2, g.Item1)), $"Kante {g} hat kein Gegenstück"));
        return proKante.Count;
    }

    /// <summary>Euler-Charakteristik V − E + F über die benutzten Ecken.</summary>
    static int Euler(Netz3 n)
    {
        int kanten = Geschlossen(n);
        int ecken = n.Dreiecke.SelectMany(t => t).Distinct().Count();
        return ecken - kanten + n.Dreiecke.Count;
    }

    static void Aussen(Netz3 n, double erwartetesVolumen, double rel, string was)
    {
        Geschlossen(n);
        var v = Vol(n);
        Assert.True(v > 0, $"{was}: Normalen zeigen nach innen (Volumen {v})");
        Assert.True(Math.Abs(v - erwartetesVolumen) <= rel * erwartetesVolumen, $"{was}: Volumen erwartet {erwartetesVolumen}, erhalten {v}");
    }

    /// <summary>Einbeschriebenes Vieleck: Volumen kleiner als der exakte Wert, aber höchstens <paramref name="rel"/> darunter.</summary>
    static void Eingeschrieben(Netz3 n, double exakt, double rel, string was)
    {
        Geschlossen(n);
        var v = Vol(n);
        Assert.True(v > 0, $"{was}: Normalen zeigen nach innen (Volumen {v})");
        Assert.True(v < exakt, $"{was}: ein einbeschriebenes Vieleck muss unter dem exakten Volumen {exakt} liegen, erhalten {v}");
        Assert.True(v > exakt * (1 - rel), $"{was}: Volumen {v} liegt mehr als {rel * 100} % unter dem exakten {exakt}");
    }

    static void Nahe(double[] erwartet, double[] ist, double tol, string was)
    {
        Assert.Equal(erwartet.Length, ist.Length);
        for (int i = 0; i < erwartet.Length; i++)
            Assert.True(Math.Abs(erwartet[i] - ist[i]) <= tol, $"{was}[{i}]: erwartet {erwartet[i]}, erhalten {ist[i]}");
    }

    /// <summary>Netz des einzigen Körpers einer Mini-Familie (Detailgrad fein).</summary>
    static Netz3 Bau(string koerperJson)
    {
        var f = FamilieTestHilfe.Def(FamilieTestHilfe.Mini(koerper: "[" + koerperJson + "]"));
        Assert.True(f.Gueltig, string.Join(" | ", f.Fehler));
        var a = FamilienAuswertung.Aufloesen(f, "A", null);
        Assert.True(a.Gueltig, string.Join(" | ", a.Fehler));
        return FamilienKoerper.AlsNetz(a.Teile("fein").Single());
    }

    const double Pi = 3.141592653589793;
    const double Facette = 0.003;   // 0.3 % > 0.285 % (siehe Kopf)

    // ------------------------------------------------------------------ Quader

    [Fact]
    public void Quader_100_200_300()
    {
        // Volumen 100 · 200 · 300 = 6 000 000 mm³; Oberfläche 2 · (100·200 + 100·300 + 200·300) = 2 · (20 000 + 30 000 + 60 000) = 220 000 mm²
        // Netz: 8 Ecken, 6 Flächen zu je 2 Dreiecken = 12, Kanten 12 + 6 Diagonalen = 18; Euler 8 − 18 + 12 = 2
        var n = Bau("""{ "id": "q", "art": "quader", "von": [0, 0, 0], "bis": [100, 200, 300] }""");
        Aussen(n, 6_000_000, 1e-9, "Quader");
        Assert.Equal(12, n.Dreiecke.Count);
        Assert.Equal(2, Euler(n));
        Assert.Equal(220_000, Oberflaeche(n), 6);
        Nahe([0, 0, 0, 100, 200, 300], n.Ausdehnung(), 1e-9, "Ausdehnung");
    }

    [Fact]
    public void Quader_Teil_Mitte_und_Masse_aus_von_bis()
    {
        // von (-20, 10, 5) bis (80, 70, 45): Mitte = (30, 40, 25), Masse = (100, 60, 40)
        var f = FamilieTestHilfe.Def(FamilieTestHilfe.Mini(koerper: """[{ "id": "q", "art": "quader", "von": [-20, 10, 5], "bis": [80, 70, 45] }]"""));
        var t = FamilienAuswertung.Aufloesen(f, "A", null).Teile("grob").Single();
        Assert.Equal("quader", t.Art);
        Nahe([30, 40, 25], t.Mitte!, 1e-9, "Mitte");
        Nahe([100, 60, 40], t.Masse!, 1e-9, "Masse");
    }

    // ------------------------------------------------------------------ Zylinder, Kegelstumpf, Kugel

    [Fact]
    public void Zylinder_r50_h400()
    {
        // exakt V = π r² h = π · 2500 · 400 = π · 1 000 000 = 3 141 592.65 mm³
        // Facetten: 3 132 628.6 mm³ (· 0.997147)
        // Oberfläche exakt: Mantel 2π r h = 2π · 50 · 400 = 125 663.7, Deckel 2 · π r² = 15 707.96, zusammen 141 371.67 mm²;
        // Facetten: Mantel 48 · (2 · 50 · sin 3.75°) · 400 = 125 574.0, Deckel 2 · 7 831.57 = 15 663.1, zusammen 141 237.2 (−0.095 %)
        var n = Bau("""{ "id": "z", "art": "zylinder", "von": [0, 0, 0], "nach": [0, 0, 400], "r": 50 }""");
        Eingeschrieben(n, Pi * 2500 * 400, Facette, "Zylinder");
        Assert.True(Math.Abs(Vol(n) - 3_132_628.6) < 1, $"Facettenvolumen {Vol(n)}");   // 24 · 2500 · sin 7.5° · 400
        Assert.True(Math.Abs(Oberflaeche(n) / 141_371.669 - 1) < 0.002, $"Oberfläche {Oberflaeche(n)}");
        Assert.Equal(2, Euler(n));
        Nahe([-50, -50, 0, 50, 50, 400], n.Ausdehnung(), 1e-9, "Ausdehnung");
    }

    [Fact]
    public void Kegelstumpf_r1_100_r2_50_h300_und_Spitzkegel()
    {
        // V = π h/3 · (r1² + r1 r2 + r2²) = π · 100 · (10 000 + 5 000 + 2 500) = π · 1 750 000 = 5 497 787.14 mm³
        var s = Bau("""{ "id": "k", "art": "kegel", "von": [0, 0, 0], "nach": [0, 0, 300], "r1": 100, "r2": 50 }""");
        Eingeschrieben(s, Pi * 100 * (10_000 + 5_000 + 2_500), Facette, "Kegelstumpf");
        Assert.Equal(2, Euler(s));
        Nahe([-100, -100, 0, 100, 100, 300], s.Ausdehnung(), 1e-9, "Ausdehnung");
        // Spitzkegel (r2 = 0): V = π r² h / 3 = π · 10 000 · 300 / 3 = π · 1 000 000 = 3 141 592.65 mm³
        var k = Bau("""{ "id": "k", "art": "kegel", "von": [0, 0, 0], "nach": [0, 0, 300], "r1": 100, "r2": 0 }""");
        Eingeschrieben(k, Pi * 1_000_000, Facette, "Spitzkegel");
    }

    [Fact]
    public void Kugel_r100()
    {
        // exakt V = 4/3 π r³ = 4.18879 · 10⁶ mm³; die Kugel hat 24 Streifen in der Breite und 48 in der Länge: von Hand mit den
        // 24 Rotationsstümpfen gerechnet 0.99288 · V (−0.71 %) → erlaubt 1 %
        var n = Bau("""{ "id": "k", "art": "kugel", "mitte": [10, 20, 30], "r": 100 }""");
        Eingeschrieben(n, 4.0 / 3 * Pi * 1_000_000, 0.01, "Kugel");
        Assert.Equal(2, Euler(n));
        // Pole und Äquatorpunkte sind Ecken: Ausdehnung = Mitte ± r
        Nahe([-90, -80, -70, 110, 120, 130], n.Ausdehnung(), 1e-9, "Ausdehnung");
    }

    // ------------------------------------------------------------------ Extrusion

    [Fact]
    public void Extrusion_Rechteck_300x200_mit_Rechteckloch_100x100_Dicke_40()
    {
        // Fläche = 300 · 200 − 100 · 100 = 60 000 − 10 000 = 50 000 mm²; Volumen = 50 000 · 40 = 2 000 000 mm³ (keine Rundung)
        // Oberfläche = 2 · 50 000 (Deckel und Boden) + Umfang aussen 1 000 · 40 + Umfang Loch 400 · 40 = 100 000 + 40 000 + 16 000 = 156 000 mm²
        // Netz: 8 Ecken je Ebene = 16; Deckel je n + 2h − 2 = 8 + 2 − 2 = 8 Dreiecke, Seiten 8 Vierecke = 16 → 32 Dreiecke, 48 Kanten
        // Euler 16 − 48 + 32 = 0 (ein Loch: wie ein Torus)
        var n = Bau("""
            { "id": "p", "art": "extrusion", "ebene": "xy", "kontur": [[0, 0], [300, 0], [300, 200], [0, 200]],
              "loecher": [[[100, 50], [200, 50], [200, 150], [100, 150]]], "von": 0, "bis": 40 }
            """);
        Aussen(n, 2_000_000, 1e-9, "Extrusion mit Loch");
        Assert.Equal(156_000, Oberflaeche(n), 6);
        Assert.Equal(16, n.Ecken.Count);
        Assert.Equal(32, n.Dreiecke.Count);
        Assert.Equal(0, Euler(n));
        Nahe([0, 0, 0, 300, 200, 40], n.Ausdehnung(), 1e-9, "Ausdehnung");
    }

    [Fact]
    public void Extrusion_in_der_Ebene_yz_wird_entlang_x_gezogen()
    {
        // dieselbe Kontur mit u = y, v = z, gezogen von x = 0 bis 40: gleiches Volumen 2 000 000, Ausdehnung x 0…40, y 0…300, z 0…200
        var n = Bau("""
            { "id": "p", "art": "extrusion", "ebene": "yz", "kontur": [[0, 0], [300, 0], [300, 200], [0, 200]],
              "loecher": [[[100, 50], [200, 50], [200, 150], [100, 150]]], "von": 0, "bis": 40 }
            """);
        Aussen(n, 2_000_000, 1e-9, "Extrusion yz");
        Nahe([0, 0, 0, 40, 300, 200], n.Ausdehnung(), 1e-9, "Ausdehnung");
    }

    [Fact]
    public void Extrusion_Kreis_aus_zwei_Woelbungspunkten()
    {
        // Punkte (0,0) und (100,0) mit Wölbung 1 = tan(180°/4): zwei Halbkreise → Kreis Ø 100 um (50, 0), r 50, gezogen 0…20
        // exakt V = π r² h = π · 2500 · 20 = 157 079.63 mm³; 48-Eck: 24 · 2500 · sin 7.5° = 7 831.57 mm² · 20 = 156 631.4 mm³
        // Ecken: je Halbkreis 180° / 7.5° = 24 Strecken → 48 Ecken je Ebene = 96; Deckel je 48 − 2 = 46 Dreiecke, Seiten 48 Vierecke = 96
        // → 2 · 46 + 96 = 188 Dreiecke; Euler 96 − 282 + 188 = 2
        var n = Bau("""{ "id": "c", "art": "extrusion", "ebene": "xy", "kontur": [[0, 0, 1], [100, 0, 1]], "von": 0, "bis": 20 }""");
        Eingeschrieben(n, Pi * 2500 * 20, Facette, "Kreisextrusion");
        Assert.Equal(96, n.Ecken.Count);
        Assert.Equal(188, n.Dreiecke.Count);
        Assert.Equal(2, Euler(n));
        Assert.True(Math.Abs(Vol(n) - 156_631.43) < 0.05, $"Facettenvolumen {Vol(n)}");   // 24 · 2500 · sin 7.5° · 20
        // alle Ecken liegen auf dem Kreis um (50, 0) mit r 50
        Assert.All(n.Ecken, p => Assert.True(Math.Abs(Math.Sqrt((p[0] - 50) * (p[0] - 50) + p[1] * p[1]) - 50) < 1e-9, $"Ecke {p[0]}/{p[1]} liegt nicht auf dem Kreis"));
        // Ecken bei 0°, 90°, 180°, 270° vorhanden (90 / 7.5 = 12): Ausdehnung x 0…100, y −50…50
        Nahe([0, -50, 0, 100, 50, 20], n.Ausdehnung(), 1e-9, "Ausdehnung");
    }

    // ------------------------------------------------------------------ Rotation (Pappus)

    [Fact]
    public void Rotation_Rechteck_um_die_senkrechte_Achse_Pappus()
    {
        // Rechteck u 100…160, v 0…200 in der Ebene xz, Achse parallel zur v-Achse (= z) durch u = 0 → Rohr, Innenradius 100, Aussenradius 160, Höhe 200
        // Pappus: V = Fläche · 2π · Schwerpunktabstand = (60 · 200) · 2π · 130 = 12 000 · 816.814 = 9 801 769.08 mm³
        // (Gegenprobe: π · (160² − 100²) · 200 = π · 15 600 · 200 = 9 801 769.08)
        var n = Bau("""
            { "id": "r", "art": "rotation", "ebene": "xz", "kontur": [[100, 0], [160, 0], [160, 200], [100, 200]],
              "achse": { "punkt": [0, 0], "richtung": "v" }, "winkel": 360 }
            """);
        Eingeschrieben(n, 12_000 * 2 * Pi * 130, Facette, "Rotation 360°");
        Assert.Equal(0, Euler(n));   // Ring: ein Loch
        Nahe([-160, -160, 0, 160, 160, 200], n.Ausdehnung(), 1e-9, "Ausdehnung");

        // halbe Drehung (180°): halbes Volumen = 4 900 884.54 mm³; die Drehung läuft rechtshändig um +z von +x nach +y: y ≥ 0
        var h = Bau("""
            { "id": "r", "art": "rotation", "ebene": "xz", "kontur": [[100, 0], [160, 0], [160, 200], [100, 200]],
              "achse": { "punkt": [0, 0], "richtung": "v" }, "winkel": 180 }
            """);
        Eingeschrieben(h, 12_000 * Pi * 130, Facette, "Rotation 180°");
        Nahe([-160, 0, 0, 160, 160, 200], h.Ausdehnung(), 1e-9, "Ausdehnung 180°");
    }

    [Fact]
    public void Rotation_um_die_waagrechte_Achse_u()
    {
        // Rechteck u 0…200, v 50…90 (Höhe 40) in xz, Achse parallel zur u-Achse (= x) durch v = 0 → Rohr entlang x, r 50…90, Länge 200
        // Pappus: V = (200 · 40) · 2π · 70 = 8 000 · 439.823 = 3 518 583.77 mm³ (π · (90² − 50²) · 200 = π · 5 600 · 200)
        var n = Bau("""
            { "id": "r", "art": "rotation", "ebene": "xz", "kontur": [[0, 50], [200, 50], [200, 90], [0, 90]],
              "achse": { "punkt": [0, 0], "richtung": "u" } }
            """);
        Eingeschrieben(n, 8_000 * 2 * Pi * 70, Facette, "Rotation um u");
        Nahe([0, -90, -90, 200, 90, 90], n.Ausdehnung(), 1e-9, "Ausdehnung");
    }

    // ------------------------------------------------------------------ Sweep

    [Fact]
    public void Sweep_gerades_Rohr_r25_L1000()
    {
        // V = π r² L = π · 625 · 1 000 = 1 963 495.4 mm³ (Facetten · 0.997147)
        var n = Bau("""{ "id": "s", "art": "sweep", "profil": { "kreis": 25 }, "pfad": [[0, 0, 0], [1000, 0, 0]] }""");
        Eingeschrieben(n, Pi * 625 * 1000, Facette, "gerades Rohr");
        Assert.Equal(2, Euler(n));
        Nahe([0, -25, -25, 1000, 25, 25], n.Ausdehnung(), 1e-9, "Ausdehnung");
    }

    [Fact]
    public void Sweep_Rohr_mit_90_Grad_Bogen_Biegeradius_200()
    {
        // Pfad (0,0,0) → (1000,0,0) → (1000,1000,0), Biegeradius 200: Tangentenlänge = R · tan(90°/2) = 200
        // Mittellinie = 2 · (1000 − 200) + ¼ · 2π · 200 = 1 600 + 314.159 = 1 914.159 mm
        // V = π r² · L(Mittellinie) = π · 625 · 1 914.159 = 3 758 442.9 mm³ (Pappus für den Bogen)
        // Facetten: Querschnitt −0.285 %, Bogen aus 12 Sehnen (313.935 statt 314.159, −0.07 %) → erlaubt 0.4 %
        var n = Bau("""{ "id": "s", "art": "sweep", "profil": { "kreis": 25 }, "pfad": [[0, 0, 0], [1000, 0, 0], [1000, 1000, 0]], "biegeradius": 200 }""");
        Eingeschrieben(n, Pi * 625 * (1600 + Pi / 2 * 200), 0.004, "Rohr mit Bogen");
        // Enden: der Anfangsdeckel liegt bei x = 0, der Enddeckel bei y = 1000; die Rohrwand geht bis 25 mm neben die Achsen
        Nahe([0, -25, -25, 1025, 1000, 25], n.Ausdehnung(), 1e-6, "Ausdehnung");
    }

    [Fact]
    public void Sweep_Rechteckprofil_gerade()
    {
        // b 100 × h 50, 500 lang: V = 100 · 50 · 500 = 2 500 000 mm³ (keine Rundung); h senkrecht (z), b quer (y): y ±50, z ±25
        var n = Bau("""{ "id": "s", "art": "sweep", "profil": { "rechteck": [100, 50] }, "pfad": [[0, 0, 0], [500, 0, 0]] }""");
        Aussen(n, 2_500_000, 1e-9, "Rechteckprofil");
        Nahe([0, -50, -25, 500, 50, 25], n.Ausdehnung(), 1e-9, "Ausdehnung");
    }
}
