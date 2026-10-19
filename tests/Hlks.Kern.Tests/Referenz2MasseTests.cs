using System.Text.Json.Nodes;
using ACadSharp.Types.Units;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.FamiliePruefungHilfe;
using static Hlks.Kern.Tests.GrundlagenHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 2, Auftrag B6: unabhängige Referenzfälle von Hand – Massvergleich (Masse entlang x, y, z, als Abstand in der Ansicht
/// und räumlich, gegen die Typwerte einer Tabelle) und die Passpunkte des Kontrollbilds (Massstab, Drehung, Verschiebung aus zwei
/// Punktpaaren). Alle Werte fiktiv.</summary>
public class Referenz2MasseTests
{
    // ------------------------------------------------------------------ Massvergleich

    /// <summary>Tabelle der Unterlage (CSV, mm): pro Typ die Masse Lx, Ly, Lz, D (Abstand in der Draufsicht) und R (räumlicher Abstand).
    /// Pythagoräische Zahlen, damit alles ohne Rundung geht: T1 (120, 160, 480): D = √(120² + 160²) = √40 000 = 200, R = √(200² + 480²) = √270 400 = 520;
    /// T2 (180, 240, 720): D = 300 (3-4-5 mal 60), R = √(300² + 720²) = √608 400 = 780;
    /// T3 (60, 80, 240): D = 100, R = √(100² + 240²) = √67 600 = 260.</summary>
    const string Tabelle =
        "Typ;Lx [mm];Ly [mm];Lz [mm];D [mm];R [mm]\r\n" +
        "T1;120;160;480;200;520\r\n" +
        "T2;180;240;720;300;780\r\n" +
        "T3;60;80;240;100;260\r\n";

    static JsonObject Massprobe(UnterlagenBefunde csv, double c3)
    {
        JsonObject P(string n) => new() { ["name"] = n, ["art"] = "typ", ["datentyp"] = "laenge", ["gruppe"] = "abmessung" };
        JsonObject T(string n, double a, double b, double c) => new() { ["name"] = n, ["werte"] = new JsonObject { ["a"] = a, ["b"] = b, ["c"] = c } };
        JsonObject M(string name, string? ansicht, JsonArray bis, string? richtung, int spalte)
        {
            var o = new JsonObject { ["name"] = name, ["von"] = new JsonArray(0, 0, 0), ["bis"] = bis, ["quellen"] = new JsonArray((JsonNode)JsonValue.Create(CsvZelle(csv, 1, spalte))) };
            if (ansicht is not null) o["ansicht"] = ansicht;
            if (richtung is not null) o["richtung"] = richtung;
            return o;
        }
        return new JsonObject
        {
            ["format"] = 1, ["name"] = "Massprobe (fiktiv)", ["kategorie"] = "Heizkoerper",
            ["parameter"] = new JsonArray(P("a"), P("b"), P("c")),
            // T3 ist absichtlich falsch modelliert: Höhe c = 245 statt 240 aus der Tabelle
            ["typen"] = new JsonArray(T("T1", 120, 160, 480), T("T2", 180, 240, 720), T("T3", 60, 80, c3)),
            ["koerper"] = new JsonArray(), ["anschluesse"] = new JsonArray(),
            ["masse"] = new JsonArray(
                M("Lx", "vorne", new JsonArray("a", 0, 0), "x", 2),
                M("Ly", "links", new JsonArray(0, "b", 0), "y", 3),
                M("Lz", "vorne", new JsonArray(0, 0, "c"), "z", 4),
                M("D", "oben", new JsonArray("a", "b", "c"), null, 5),        // Abstand in der Draufsicht: z zählt nicht
                M("R", null, new JsonArray("a", "b", "c"), null, 6)),         // räumlicher Abstand
        };
    }

    static JsonObject Zeile(JsonObject abf, string typ, string mass) =>
        (JsonObject)((JsonArray)abf["typen"]!).Single(t => t!["typ"]!.GetValue<string>() == typ)!["abweichungen"]!.AsArray().Single(z => z!["mass"]!.GetValue<string>() == mass)!;

    [Fact]
    public void Massvergleich_entlang_x_y_z_Abstand_und_raeumlich_von_Hand()
    {
        using var o = new Ordner();
        var p = ProjektB4();
        var csv = Ablegen(p, UnterlagenHilfe.Datei(o, "massbild.csv", Tabelle));
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Massprobe(csv, 245) }));
        var abf = Abfrage(p, "familie_masse", new JsonObject { ["familie"] = "FAM-1" });

        // (Typ, Mass, Soll aus der Tabelle, Ist an der Familie, Toleranz = max(1 mm; 0.5 % des Solls), bestanden)
        // T1 und T2 stimmen überall; T3 hat c = 245: Lz Ist 245 / Soll 240 (Abweichung 5 > 1.2), R Ist √(100² + 245²) = √70 025 = 264.622 / Soll 260 (4.622 > 1.3);
        // Lx, Ly und D hängen nicht von c ab und stimmen (D in der Draufsicht = √(60² + 80²) = 100).
        (string Typ, string Mass, double Soll, double Ist, double Tol, bool Ok)[] erwartet =
        [
            ("T1", "Lx", 120, 120, 1, true), ("T1", "Ly", 160, 160, 1, true), ("T1", "Lz", 480, 480, 2.4, true), ("T1", "D", 200, 200, 1, true), ("T1", "R", 520, 520, 2.6, true),
            ("T2", "Lx", 180, 180, 1, true), ("T2", "Ly", 240, 240, 1.2, true), ("T2", "Lz", 720, 720, 3.6, true), ("T2", "D", 300, 300, 1.5, true), ("T2", "R", 780, 780, 3.9, true),
            ("T3", "Lx", 60, 60, 1, true), ("T3", "Ly", 80, 80, 1, true), ("T3", "Lz", 240, 245, 1.2, false), ("T3", "D", 100, 100, 1, true), ("T3", "R", 260, 264.622372, 1.3, false),
        ];
        foreach (var (typ, mass, soll, ist, tol, ok) in erwartet)
        {
            var z = Zeile(abf, typ, mass);
            Assert.True(Math.Abs(z["soll"]!.GetValue<double>() - soll) < 5e-4, $"{typ} {mass}: Soll erwartet {soll}, erhalten {z["soll"]}");
            Assert.True(Math.Abs(z["ist"]!.GetValue<double>() - ist) < 5e-4, $"{typ} {mass}: Ist erwartet {ist}, erhalten {z["ist"]}");
            Assert.True(Math.Abs(z["toleranz"]!.GetValue<double>() - tol) < 5e-4, $"{typ} {mass}: Toleranz erwartet {tol}, erhalten {z["toleranz"]}");
            Assert.True(z["bestanden"]!.GetValue<bool>() == ok, $"{typ} {mass}: bestanden erwartet {ok}");
        }
        // genau diese zwei Abweichungen (eine Ursache: c von T3), sonst nichts
        Assert.Equal(2, abf["ausserhalb"]!.GetValue<int>());
        Assert.Equal(13, abf["bestanden"]!.GetValue<int>());
        Assert.Equal(0, abf["ohneVergleich"]!.GetValue<int>());
        Assert.Equal(["T3"], abf["typen"]!.AsArray().Where(t => !t!["bestanden"]!.GetValue<bool>()).Select(t => t!["typ"]!.GetValue<string>()));
        // die Abweichung Lz von T3: Ist − Soll = 245 − 240 = 5
        Assert.True(Math.Abs(Zeile(abf, "T3", "Lz")["abweichung"]!.GetValue<double>() - 5) < 5e-4);
    }

    [Fact]
    public void Massvergleich_richtig_modelliert_alles_bestanden()
    {
        using var o = new Ordner();
        var p = ProjektB4();
        var csv = Ablegen(p, UnterlagenHilfe.Datei(o, "massbild.csv", Tabelle));
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Massprobe(csv, 240) }));
        var abf = Abfrage(p, "familie_masse", new JsonObject { ["familie"] = "FAM-1" });
        Assert.Equal(0, abf["ausserhalb"]!.GetValue<int>());
        Assert.Equal(15, abf["bestanden"]!.GetValue<int>());
    }

    [Theory]
    [InlineData(240.0, true)]    // gleich
    [InlineData(241.2, true)]    // genau 1.2 = 0.5 % von 240 darüber
    [InlineData(241.21, false)]
    [InlineData(238.8, true)]    // genau 1.2 darunter
    [InlineData(238.79, false)]
    public void Massvergleich_Toleranz_max_1_mm_oder_halbes_Prozent_des_Solls(double c, bool bestanden)
    {
        using var o = new Ordner();
        var p = ProjektB4();
        var csv = Ablegen(p, UnterlagenHilfe.Datei(o, "massbild.csv", Tabelle));
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Massprobe(csv, c) }));
        var z = Zeile(Abfrage(p, "familie_masse", new JsonObject { ["familie"] = "FAM-1", ["typ"] = "T3" }), "T3", "Lz");
        Assert.Equal(1.2, z["toleranz"]!.GetValue<double>(), 6);
        Assert.Equal(bestanden, z["bestanden"]!.GetValue<bool>());
    }

    // ------------------------------------------------------------------ Passpunkte des Kontrollbilds

    /// <summary>Familie «Kiste» (FIKTIV): Quader 200 × 100 × 100; die Vorderansicht ist das Rechteck u 0…200, v (= z) 0…100.</summary>
    static JsonObject Kiste() => FamilieTestHilfe.Mini(koerper: """[{ "id": "k", "art": "quader", "von": [0, 0, 0], "bis": [200, 100, 100] }]""");

    static string Rahmen(Ordner o, string name, double[][] ecken) => Dxf(o, name, UnitsType.Millimeters, doc =>
    {
        for (int i = 0; i < 4; i++)
        {
            var a = ecken[i]; var b = ecken[(i + 1) % 4];
            doc.Entities.Add(Linie(doc, a[0], a[1], b[0], b[1], "GEHAEUSE"));
        }
    });

    static JsonObject Par(string unterlage, double[] f1, double[] g1, double[] f2, double[] g2, int breite = 500) => new()
    {
        ["familie"] = "FAM-1", ["typ"] = "A", ["unterlage"] = unterlage, ["ansicht"] = "vorne", ["breite"] = breite,
        ["passpunkte"] = new JsonArray(
            new JsonObject { ["familie"] = new JsonArray(f1[0], f1[1]), ["unterlage"] = new JsonArray(g1[0], g1[1]) },
            new JsonObject { ["familie"] = new JsonArray(f2[0], f2[1]), ["unterlage"] = new JsonArray(g2[0], g2[1]) }),
    };

    static void Bildgroesse(JsonObject r, byte[] png)
    {
        Assert.True(png.Length > 1000, "PNG zu klein");
        Assert.Equal(0x89, png[0]); Assert.Equal((byte)'P', png[1]);
        Assert.True(r["breite"]!.GetValue<int>() > 0);
        Assert.True(r["hoehe"]!.GetValue<int>() > 0);
        Assert.Equal(png.Length, r["bytes"]!.GetValue<int>());
    }

    [Fact]
    public void Passpunkte_Massstab_halb_Drehung_90_Verschiebung_ueber_die_Kanten()
    {
        // Abbildung (u, v) → (x, y) = (A·u − B·v + Tx, B·u + A·v + Ty) mit A + iB = s · e^(iθ). Gewählt: s = 0.5, θ = 90° → A = 0, B = 0.5, Tx = 1000, Ty = 500.
        // Ecken der Familienansicht: (0,0) → (1000, 500); (200,0) → (0.5·0 … : x = −0.5·0 + 1000 = 1000, y = 0.5·200 + 500 = 600);
        //   (200,100) → (x = −0.5·100 + 1000 = 950, y = 600); (0,100) → (x = 950, y = 500).
        // Passpunkte: (0,0) ↔ (1000, 500) und (200,100) ↔ (950, 600); daraus: Δu 200, Δv 100, Δx −50, Δy 100, n = 200² + 100² = 50 000,
        //   A = (Δx·Δu + Δy·Δv)/n = (−10 000 + 10 000)/50 000 = 0;  B = (Δy·Δu − Δx·Δv)/n = (20 000 + 5 000)/50 000 = 0.5.
        using var o = new Ordner();
        var p = ProjektB4();
        Ablegen(p, Rahmen(o, "rahmen.dxf", [[1000, 500], [1000, 600], [950, 600], [950, 500]]));
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Kiste() }));

        var r = FamilienKontrollbild.Erzeugen(p, Par("rahmen.dxf", [0, 0], [1000, 500], [200, 100], [950, 600]), out var png);
        Bildgroesse(r, png);
        Assert.Equal("passpunkte", r["einpassung"]!["quelle"]!.GetValue<string>());
        Assert.Equal(0.5, r["einpassung"]!["massstab"]!.GetValue<double>(), 6);
        Assert.Equal(90, r["einpassung"]!["drehung"]!.GetValue<double>(), 4);
        Assert.True(r["massstaeblich"]!.GetValue<bool>());
        // Verschiebung: liegt Tx/Ty richtig, fallen die vier Zeichnungskanten auf die vier Familienkanten (Abstand 0 in mm der Familie)
        var kanten = r["geometrisch"]!["kanten"]!;
        Assert.Equal(4, kanten["anzahl"]!.GetValue<int>());
        Assert.Equal(4, kanten["bestanden"]!.GetValue<int>());
        Assert.Equal(0, kanten["ausserhalb"]!.GetValue<int>());
        Assert.True(kanten["groessterAbstand"]!.GetValue<double>() < 0.001, $"grösster Abstand {kanten["groessterAbstand"]}");

        // Gegenprobe: beide Passpunkte der Unterlage um 5 Einheiten in x verschoben → Tx 1005, also die Zeichnung um 5 / 0.5 = 10 mm der Familie
        // verrückt: die Zeichnungskanten liegen 10 mm neben den Familienkanten
        var r2 = FamilienKontrollbild.Erzeugen(p, Par("rahmen.dxf", [0, 0], [1005, 500], [200, 100], [955, 600]), out _);
        Assert.Equal(0.5, r2["einpassung"]!["massstab"]!.GetValue<double>(), 6);   // Massstab und Drehung bleiben
        Assert.Equal(90, r2["einpassung"]!["drehung"]!.GetValue<double>(), 4);
        var k2 = r2["geometrisch"]!["kanten"]!;
        Assert.Equal(4, k2["ausserhalb"]!.GetValue<int>());
        Assert.Equal(10, k2["groessterAbstand"]!.GetValue<double>(), 3);
    }

    [Fact]
    public void Passpunkte_Massstab_2_Drehung_30_Verschiebung_ueber_die_Kanten()
    {
        // s = 2, θ = 30°: A = 2·cos 30° = √3 = 1.7320508, B = 2·sin 30° = 1. Verschiebung so, dass (10, 20) auf (300, 400) fällt:
        //   Tx = 300 − (A·10 − B·20) = 300 − (17.320508 − 20) = 302.679492; Ty = 400 − (B·10 + A·20) = 400 − (10 + 34.641016) = 355.358984.
        // Ecken: (0,0) → (302.679492, 355.358984); (200,0) → (A·200 + Tx, B·200 + Ty) = (649.089654, 555.358984);
        //        (200,100) → (A·200 − 100 + Tx, 200 + A·100 + Ty) = (549.089654, 728.564065); (0,100) → (−100 + Tx, A·100 + Ty) = (202.679492, 528.564065).
        using var o = new Ordner();
        var p = ProjektB4();
        Ablegen(p, Rahmen(o, "gedreht.dxf", [[302.679492, 355.358984], [649.089654, 555.358984], [549.089654, 728.564065], [202.679492, 528.564065]]));
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Kiste() }));
        var r = FamilienKontrollbild.Erzeugen(p, Par("gedreht.dxf", [0, 0], [302.679492, 355.358984], [200, 100], [549.089654, 728.564065]), out var png);
        Bildgroesse(r, png);
        Assert.Equal(2, r["einpassung"]!["massstab"]!.GetValue<double>(), 5);
        Assert.Equal(30, r["einpassung"]!["drehung"]!.GetValue<double>(), 3);
        var kanten = r["geometrisch"]!["kanten"]!;
        Assert.Equal(4, kanten["anzahl"]!.GetValue<int>());
        Assert.Equal(0, kanten["ausserhalb"]!.GetValue<int>());
        Assert.True(kanten["groessterAbstand"]!.GetValue<double>() < 0.01, $"grösster Abstand {kanten["groessterAbstand"]}");
    }

    [Fact]
    public void Passpunkte_negative_Drehung_und_Massstab_unter_1()
    {
        // s = 0.5, θ = −45°: A = 0.5·cos(−45°) = 0.353553, B = −0.353553; (0,0) ↔ (10, 20); (200,100) ↔
        //   x = A·200 − B·100 + 10 = 70.710678 + 35.355339 + 10 = 116.066017, y = B·200 + A·100 + 20 = −70.710678 + 35.355339 + 20 = −15.355339
        using var o = new Ordner();
        var p = ProjektB4();
        Ablegen(p, UnterlagenHilfe.TestPdf(o));
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Kiste() }));
        var r = FamilienKontrollbild.Erzeugen(p, Par("datenblatt.pdf", [0, 0], [10, 20], [200, 100], [116.066017, -15.355339], 300), out var png);
        Bildgroesse(r, png);
        Assert.Equal(0.5, r["einpassung"]!["massstab"]!.GetValue<double>(), 5);
        Assert.Equal(-45, r["einpassung"]!["drehung"]!.GetValue<double>(), 3);
        Assert.False(r["massstaeblich"]!.GetValue<bool>());   // PDF-Massbild: schematisch, nur Anschauung
    }

    [Fact]
    public void Passpunkte_Massstab_nur_Verschiebung()
    {
        // reine Verschiebung: (10, 20) ↔ (110, 220) und (110, 20) ↔ (210, 220): A = (100·100 + 0)/10 000 = 1, B = (0 − 100·0)/10 000 = 0
        using var o = new Ordner();
        var p = ProjektB4();
        Ablegen(p, UnterlagenHilfe.TestPdf(o));
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Kiste() }));
        var r = FamilienKontrollbild.Erzeugen(p, Par("datenblatt.pdf", [10, 20], [110, 220], [110, 20], [210, 220], 300), out var png);
        Bildgroesse(r, png);
        Assert.Equal(1, r["einpassung"]!["massstab"]!.GetValue<double>(), 6);
        Assert.Equal(0, r["einpassung"]!["drehung"]!.GetValue<double>(), 6);
    }

    [Fact]
    public void Passpunkte_Fehler_zusammenfallend_oder_falsche_Anzahl()
    {
        using var o = new Ordner();
        var p = ProjektB4();
        Ablegen(p, UnterlagenHilfe.TestPdf(o));
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Kiste() }));
        // beide Familienpunkte gleich: die Strecke hat keine Länge, Massstab und Drehung wären unbestimmt
        var z = Assert.Throws<HlksFehler>(() => FamilienKontrollbild.Erzeugen(p, Par("datenblatt.pdf", [5, 5], [100, 100], [5, 5], [200, 200], 300), out _));
        Assert.Contains("fallen zusammen", z.Message);
        // beide Unterlagenpunkte gleich: ebenso
        Assert.Contains("fallen zusammen", Assert.Throws<HlksFehler>(() => FamilienKontrollbild.Erzeugen(p, Par("datenblatt.pdf", [0, 0], [100, 100], [200, 100], [100, 100], 300), out _)).Message);
        // ein Passpunkt allein genügt nicht
        var par = Par("datenblatt.pdf", [0, 0], [10, 10], [1, 1], [20, 20], 300);
        par["passpunkte"] = new JsonArray(((JsonArray)par["passpunkte"]!)[0]!.DeepClone());
        Assert.Contains("genau zwei", Assert.Throws<HlksFehler>(() => FamilienKontrollbild.Erzeugen(p, par, out _)).Message);
    }
}
