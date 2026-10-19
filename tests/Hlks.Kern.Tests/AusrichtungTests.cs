using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3, B1: volle Ausrichtung von Familien (ausrichtung { x, z }, y = z × x) – Prüfung, Anschlusslagen, 3D-Körper,
/// Grundriss (Umriss von oben bzw. Sinnbild nur bei senkrechter lokaler z-Achse), Fangpunkte. Handrechnungen mit dem FIKTIVEN
/// Testbogen (DN 20, 90°: La = Lb = 50 mm, Rohr-Ø 25 → Radius 12.5).</summary>
public class AusrichtungTests
{
    static readonly double[] P = [1000, 2000, 500];
    /// <summary>Der Bogen ist als Netz facettiert (Bögen ≤ 7.5° je Strecke, Endflächen senkrecht zur letzten Sehne): Ausdehnungen weichen
    /// bis 12.5 · sin 3.75° = 0.82 mm von der Handrechnung ab.</summary>
    const double Facette = 1.0;

    // ------------------------------------------------------------------ Lesen und prüfen

    [Fact]
    public void AusrichtungLesen_NormiertUndRechtwinklig()
    {
        var (x, z) = Familien.AusrichtungLesen(Ausrichtung([2, 0, 0], [0, 0, 5]))!.Value;
        Assert.Equal(new V3(1, 0, 0), x);
        Assert.Equal(new V3(0, 0, 1), z);
        Assert.Null(Familien.AusrichtungLesen(null));
        // kleine Abweichung (≤ 1e-6) wird gerade gerichtet
        var (x2, z2) = Familien.AusrichtungLesen(Ausrichtung([1, 0, 0], [5e-7, 0, 1]))!.Value;
        Assert.True(Math.Abs(x2.Mal(z2)) < 1e-12);
    }

    [Theory]
    [InlineData("""{ "x": [0, 0, 0], "z": [0, 0, 1] }""", "keine Richtung")]
    [InlineData("""{ "x": [1, 0, 0], "z": [1, 0, 1] }""", "nicht rechtwinklig")]
    [InlineData("""{ "x": [1, 0, 0] }""", "z fehlt")]
    [InlineData("""{ "x": [1, 0], "z": [0, 0, 1] }""", "x fehlt oder ist keine Liste")]
    [InlineData("""{ "x": [1, 0, "a"], "z": [0, 0, 1] }""", "keine Zahl")]
    [InlineData("""{ "x": [1, 0, 0], "z": [0, 0, 1], "y": [0, 1, 0] }""", "Feld «y»")]
    [InlineData("""[1, 0, 0]""", "muss ein Objekt")]
    public void AusrichtungLesen_UngueltigMitVorschlag(string json, string text)
    {
        var f = Assert.Throws<HlksFehler>(() => Familien.AusrichtungLesen(JsonNode.Parse(json)));
        Assert.Contains(text, f.Message);
        Assert.Contains("x = [1, 0, 0], z = [0, -1, 0]", f.Vorschlag);
    }

    [Fact]
    public void AusrichtungLesen_SchiefNenntDenWinkel()
    {
        // x = (1, 0, 0), z = (cos 80°, 0, sin 80°): Winkel 80° statt 90°
        var f = Assert.Throws<HlksFehler>(() => Familien.AusrichtungLesen(Ausrichtung([1, 0, 0], [Math.Cos(80 * Math.PI / 180), 0, Math.Sin(80 * Math.PI / 180)])));
        Assert.Contains("Winkel 80°", f.Message);
    }

    // ------------------------------------------------------------------ Anschlusslagen (Handrechnung)

    [Fact]
    public void Steigbogen_AnschlussBZeigtNachOben()
    {
        var p = MitSerie();
        // Steigbogen: x = (1, 0, 0), z = (0, −1, 0) → y = z × x = (0, 0, 1)
        Ok(Formteil(p, "fam-test-bogen", "DN 20", "BO-1", P, winkel: 90, x: [1, 0, 0], z: [0, -1, 0]));
        var a = Stelle(p, "BO-1", "a"); var b = Stelle(p, "BO-1", "b");
        // a lokal (−50, 0, 0)/(−1, 0, 0) → Welt P + (−50, 0, 0)
        Etwa([950, 2000, 500], a.Lage); Etwa([-1, 0, 0], a.Richtung);
        // b lokal (0, 50, 0)/(0, 1, 0) → Welt P + (0, 0, 50), Richtung nach oben
        Etwa([1000, 2000, 550], b.Lage, 1e-3); Etwa([0, 0, 1], b.Richtung, 1e-9);
        Assert.Equal("familie", b.Quelle);
        Assert.Equal(20, b.Groesse.Dn);
        // die bisherige Funktion der Familien rechnet gleich
        var alt = Familien.Anschlusslagen(p.Modell, p.Katalog, Hole(p, "BO-1")).Single(x => x.Name == "b");
        Etwa([1000, 2000, 550], alt.Lage, 1e-3);
    }

    [Fact]
    public void Fallbogen_UndGedrehterSteigbogen()
    {
        var p = MitSerie();
        // Fallbogen: z = (0, 1, 0) → y = (0, 1, 0) × (1, 0, 0) = (0, 0, −1): b zeigt nach unten
        Ok(Formteil(p, "fam-test-bogen", "DN 20", "BO-1", P, winkel: 90, x: [1, 0, 0], z: [0, 1, 0]));
        Etwa([0, 0, -1], Stelle(p, "BO-1", "b").Richtung, 1e-9);
        Etwa([1000, 2000, 450], Stelle(p, "BO-1", "b").Lage, 1e-3);
        // Steigbogen, von Norden her angeschlossen: x = (0, −1, 0), z = (−1, 0, 0) → y = z × x = (0, 0, 1);
        // a lokal (−50, 0, 0) → Welt −50·x = (0, 50, 0), Richtung −x = (0, 1, 0); b lokal (0, 50, 0) → (0, 0, 50), nach oben
        Ok(Formteil(p, "fam-test-bogen", "DN 20", "BO-2", P, winkel: 90, x: [0, -1, 0], z: [-1, 0, 0]));
        Etwa([0, 1, 0], Stelle(p, "BO-2", "a").Richtung, 1e-9);
        Etwa([1000, 2050, 500], Stelle(p, "BO-2", "a").Lage, 1e-3);
        Etwa([0, 0, 1], Stelle(p, "BO-2", "b").Richtung, 1e-9);
    }

    [Fact]
    public void Ausrichtung_GehtDrehungVor()
    {
        var p = MitSerie();
        Ok(Formteil(p, "fam-test-bogen", "DN 20", "BO-1", P, winkel: 90, x: [1, 0, 0], z: [0, -1, 0], drehung: 90));
        Etwa([0, 0, 1], Stelle(p, "BO-1", "b").Richtung, 1e-9);
        // ohne ausrichtung: Drehung 90° um z → a zeigt nach (0, −1, 0), b nach (−1, 0, 0)
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "BO-1", ["eigenschaften"] = new JsonObject { ["ausrichtung"] = null } }));
        Etwa([0, -1, 0], Stelle(p, "BO-1", "a").Richtung, 1e-9);
        Etwa([-1, 0, 0], Stelle(p, "BO-1", "b").Richtung, 1e-9);
        Etwa([950, 2000, 500], Stelle(p, "BO-1", "b").Lage, 1e-3);
    }

    [Fact]
    public void ElementAendern_AusrichtungWirdGeprueft()
    {
        var p = MitSerie();
        Ok(Formteil(p, "fam-test-bogen", "DN 20", "BO-1", P, winkel: 90));
        var n = Nein(p.Ausfuehren("element_aendern", new JsonObject
        {
            ["element"] = "BO-1", ["eigenschaften"] = new JsonObject { ["ausrichtung"] = Ausrichtung([1, 0, 0], [1, 1, 0]) },
        }));
        Assert.Contains("nicht rechtwinklig", n.Fehler);
        Ok(p.Ausfuehren("element_aendern", new JsonObject
        {
            ["element"] = "BO-1", ["eigenschaften"] = new JsonObject { ["ausrichtung"] = Ausrichtung([1, 0, 0], [0, -1, 0]) },
        }));
        Etwa([0, 0, 1], Stelle(p, "BO-1", "b").Richtung, 1e-9);
        Ok(p.Rueckgaengig());
        Etwa([0, 1, 0], Stelle(p, "BO-1", "b").Richtung, 1e-9);
    }

    [Fact]
    public void FamiliePlatzieren_UngueltigeAusrichtungAbgelehnt_TrockenlaufOhneAenderung()
    {
        var p = MitSerie();
        var n = Nein(Formteil(p, "fam-test-bogen", "DN 20", "BO-1", P, x: [0, 0, 0], z: [0, 0, 1]));
        Assert.Contains("keine Richtung", n.Fehler);
        var vorher = p.Modell.Elemente.Count;
        var t = Ok(Formteil(p, "fam-test-bogen", "DN 20", "BO-1", P, winkel: 90, x: [1, 0, 0], z: [0, -1, 0], trocken: true));
        Assert.Contains(t.Hinweise, h => h.Contains("Ausrichtung x = [1, 0, 0], z = [0, -1, 0]"));
        Assert.Equal(vorher, p.Modell.Elemente.Count);
    }

    [Fact]
    public void Ausrichtung_OhneFamilieNurHinweis()
    {
        var p = MitSerie();
        var e = Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Bogen", ["kennzeichen"] = "BO-X", ["system"] = "KW", ["geschoss"] = "EG",
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([0, 0, 0]) },
        }));
        e = Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "BO-X", ["eigenschaften"] = new JsonObject { ["ausrichtung"] = Ausrichtung([1, 0, 0], [0, 0, 1]) } }));
        Assert.Contains(e.Hinweise, h => h.Contains("wirken nur mit einer Familie"));
        Nein(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "BO-X", ["eigenschaften"] = new JsonObject { ["ausrichtung"] = Ausrichtung([1, 0, 0], [1, 0, 0]) } }));
    }

    // ------------------------------------------------------------------ Grafik: 3D, Grundriss, Fang

    [Fact]
    public void Koerper3D_SteigbogenReichtNachOben()
    {
        var p = MitSerie();
        Ok(Formteil(p, "fam-test-bogen", "DN 20", "BO-1", P, winkel: 90, x: [1, 0, 0], z: [0, -1, 0]));
        var q = Grafik.QuelleFuer(p.Katalog, Hole(p, "BO-1"));
        Assert.IsType<FamilienGrafik>(q);
        var k = q!.Koerper(p, Hole(p, "BO-1"))!;
        var ecken = k.Teile.SelectMany(t => t.Ecken!).ToList();
        // Pfad (−50, 0, 0) → (0, 0, 0) → (0, 0, 50) in Welt relativ P, Profil-Radius 12.5:
        // x von 950 bis 1012.5, y 2000 ± 12.5, z von 487.5 bis 550
        Etwa(950, ecken.Min(e => e[0]), Facette); Etwa(1012.5, ecken.Max(e => e[0]), Facette);
        Etwa(1987.5, ecken.Min(e => e[1]), Facette); Etwa(2012.5, ecken.Max(e => e[1]), Facette);
        Etwa(487.5, ecken.Min(e => e[2]), Facette); Etwa(550, ecken.Max(e => e[2]), Facette);
        // derselbe Bogen liegend (ohne Ausrichtung): z bleibt 500 ± 12.5, y reicht bis 2050
        Ok(Formteil(p, "fam-test-bogen", "DN 20", "BO-2", P, winkel: 90));
        var e2 = q.Koerper(p, Hole(p, "BO-2"))!.Teile.SelectMany(t => t.Ecken!).ToList();
        Etwa(512.5, e2.Max(e => e[2]), Facette); Etwa(2050, e2.Max(e => e[1]), Facette);
    }

    [Fact]
    public void Koerper3D_QuaderWirdBeiVollerAusrichtungZumNetz()
    {
        // FIKTIVE Mini-Familie (Kategorie Ventil): Quader 100 × 40 × 20 ab dem Ursprung
        var p = MitSerie(false);
        var d = FamilieTestHilfe.JsonAus("""
            { "format": 1, "name": "Quader (fiktiv)", "kategorie": "Ventil", "parameter": [], "typen": [ { "name": "A", "werte": {} } ],
              "koerper": [ { "id": "q", "art": "quader", "von": [0, 0, 0], "bis": [100, 40, 20] } ], "anschluesse": [] }
            """);
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d.DeepClone() }));
        Ok(p.Ausfuehren("familie_platzieren", new JsonObject
        {
            ["familie"] = "Quader (fiktiv)", ["typ"] = "A", ["punkt"] = new JsonArray(0, 0, 0), ["kennzeichen"] = "VE-1", ["system"] = "KW",
            ["ausrichtung"] = Ausrichtung([0, 0, 1], [1, 0, 0]),   // x → oben, z → Ost, y = z × x = (1,0,0) × (0,0,1) = (0, −1, 0)
        }));
        var t = Assert.Single(new FamilienGrafik().Koerper(p, Hole(p, "VE-1"))!.Teile);
        Assert.Equal("koerper", t.Art);
        // lokal x 0..100 → Welt z 0..100; lokal y 0..40 → Welt y 0..−40; lokal z 0..20 → Welt x 0..20
        Etwa(100, t.Ecken!.Max(e => e[2]), 1e-9); Etwa(-40, t.Ecken!.Min(e => e[1]), 1e-9); Etwa(20, t.Ecken!.Max(e => e[0]), 1e-9);
        var n = new Netz3(); foreach (var e in t.Ecken!) n.Ecken.Add(e); foreach (var dr in t.Dreiecke!) n.Dreiecke.Add(dr);
        Etwa(100 * 40 * 20, n.Volumen(), 1e-6);   // Orientierung bleibt (rechtshändig)
    }

    static Grafikteil Grundriss(Projekt p, string kz)
    {
        var g = Grafik.Plan(p, "AN-EG");
        return g.Teile.Single(t => t.Id == Hole(p, kz).Id);
    }

    [Fact]
    public void Grundriss_SteigbogenAlsUmrissDerGedrehtenKoerper()
    {
        var p = MitSerie();
        Ok(Formteil(p, "fam-test-bogen", "DN 20", "BO-1", P, winkel: 90, x: [1, 0, 0], z: [0, -1, 0]));
        var t = Grundriss(p, "BO-1");
        var poly = t.Primitive.Where(x => x.Art == "polygon").ToList();
        Assert.NotEmpty(poly);
        var pts = poly.SelectMany(x => x.Punkte).ToList();
        // von oben: x von 950 bis 1012.5, y 2000 ± 12.5 (die Steigung liegt über dem Achsschnittpunkt)
        Etwa(950, pts.Min(q => q[0]), Facette); Etwa(1012.5, pts.Max(q => q[0]), Facette);
        Etwa(1987.5, pts.Min(q => q[1]), Facette); Etwa(2012.5, pts.Max(q => q[1]), Facette);
        // Fang: Einfügepunkt, Anschlüsse a (950, 2000) und b (1000, 2000) von oben
        Assert.Contains(t.Fangpunkte!, f => Math.Abs(f[0] - 950) < 1e-6 && Math.Abs(f[1] - 2000) < 1e-6);
        Assert.Contains(t.Fangpunkte!, f => Math.Abs(f[0] - 1000) < 1e-6 && Math.Abs(f[1] - 2000) < 1e-6);
    }

    [Fact]
    public void Grundriss_SinnbildNurBeiSenkrechterZAchse()
    {
        // FIKTIVE Mini-Familie mit Sinnbild (Kreis Radius 30 um den Ursprung) und einem Quader
        var p = MitSerie(false);
        var d = FamilieTestHilfe.JsonAus("""
            { "format": 1, "name": "Sinnbild (fiktiv)", "kategorie": "Ventil", "parameter": [], "typen": [ { "name": "A", "werte": {} } ],
              "koerper": [ { "id": "q", "art": "quader", "von": [0, 0, 0], "bis": [100, 40, 20] } ], "anschluesse": [],
              "sinnbild": [ { "art": "kreis", "punkte": [[0, 0]], "radius": 30 } ] }
            """);
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d.DeepClone() }));
        void Setzen(string kz, double x, JsonObject? ausrichtung)
        {
            var par = new JsonObject { ["familie"] = "Sinnbild (fiktiv)", ["typ"] = "A", ["punkt"] = new JsonArray(x, 0, 500), ["kennzeichen"] = kz, ["system"] = "KW" };
            if (ausrichtung is not null) par["ausrichtung"] = ausrichtung;
            Ok(p.Ausfuehren("familie_platzieren", par));
        }
        Setzen("VE-1", 0, Ausrichtung([0, 1, 0], [0, 0, 1]));     // senkrecht, gedreht um 90°: Sinnbild
        Setzen("VE-2", 5000, Ausrichtung([1, 0, 0], [0, -1, 0]));  // gekippt: Umriss
        var t1 = Grundriss(p, "VE-1");
        Assert.Contains(t1.Primitive, x => x.Art == "kreis" && x.Radius == 30 && !x.Stil.NurBildschirm);
        Assert.DoesNotContain(t1.Primitive, x => x.Art == "polygon");
        var t2 = Grundriss(p, "VE-2");
        Assert.DoesNotContain(t2.Primitive, x => x.Art == "kreis" && x.Radius == 30);
        var poly = Assert.Single(t2.Primitive, x => x.Art == "polygon");
        // x = (1,0,0), z = (0,−1,0), y = (0,0,1): Quader lokal y 0..40 → Welt z; lokal z 0..20 → Welt y 0..−20 → von oben 100 × 20
        Etwa(5000, poly.Punkte.Min(q => q[0]), 1e-6); Etwa(5100, poly.Punkte.Max(q => q[0]), 1e-6);
        Etwa(-20, poly.Punkte.Min(q => q[1]), 1e-6); Etwa(0, poly.Punkte.Max(q => q[1]), 1e-6);
    }

    [Fact]
    public void Formteilausrichtung_AusRichtungen_FuerDasRouting()
    {
        // Steigbogen nach Spezifikation: durch (1, 0, 0), seitlich (0, 0, 1) → x = (1, 0, 0), z = (0, −1, 0)
        var (x, z) = Formteilgeometrie.Ausrichtung(new V3(2, 0, 0), new V3(0, 0, 5));
        Etwa([1, 0, 0], x.Feld(), 1e-12); Etwa([0, -1, 0], z.Feld(), 1e-12);
        // liegender Bogen nach +y: z = (0, 0, 1) wie ohne Ausrichtung
        Etwa([0, 0, 1], Formteilgeometrie.Ausrichtung(new V3(1, 0, 0), new V3(0, 1, 0)).Z.Feld(), 1e-12);
        // gerades Formteil: waagrechte Senkrechte, z nach oben
        Etwa([0, 0, 1], Formteilgeometrie.Ausrichtung(new V3(0, 1, 0)).Z.Feld(), 1e-12);
        Assert.Throws<HlksFehler>(() => Formteilgeometrie.Ausrichtung(new V3(1, 0, 0), new V3(-3, 0, 0)));

        // Handrechnung: Knoten K = (1000, 2000, 500); Rohr A waagrecht nach +x bis K − 50·x, Steigbogen DN 20 in K, Rohr B ab K + 50·z nach oben
        var p = MitSerie();
        var w = Serie(p).Formteil(p.Modell, p.Katalog, "bogen", Leitungsgroesse.Rohr(20), winkel: 90);
        var la = w.Geometrie!.Einbaulaenge("a"); var lb = w.Geometrie.Einbaulaenge("b");
        Ok(Rohr(p, "RO-A", "DN 20", [500, 2000, 500], [1000 - la, 2000, 500]));
        Ok(Rohr(p, "RO-B", "DN 20", [1000, 2000, 500 + lb], [1000, 2000, 1500]));
        var (ax, az) = Formteilgeometrie.Ausrichtung(new V3(1, 0, 0), new V3(0, 0, 1));
        Ok(p.Ausfuehren("familie_platzieren", new JsonObject
        {
            ["familie"] = w.Familie!.Kennzeichen, ["typ"] = w.Typ, ["parameter"] = w.Exemplar!.DeepClone(), ["kennzeichen"] = "BO-1", ["system"] = "KW",
            ["punkt"] = new JsonArray(1000, 2000, 500), ["ausrichtung"] = Familien.AusrichtungJson(ax, az),
        }));
        Ok(p.Ausfuehren("verbinden", new JsonObject { ["von"] = "RO-A.ende", ["nach"] = "BO-1.a" }));
        Ok(p.Ausfuehren("verbinden", new JsonObject { ["von"] = "BO-1.b", ["nach"] = "RO-B.anfang" }));
        Assert.DoesNotContain(Pruefungen.Alle(p.Modell, p.Katalog), b => b.Code is "P13" or "P14" or "P15");
    }

    [Fact]
    public void Schnitt_NimmtDieAusgerichtetenKoerper()
    {
        var p = MitSerie();
        Ok(Formteil(p, "fam-test-bogen", "DN 20", "BO-1", P, winkel: 90, x: [1, 0, 0], z: [0, -1, 0]));
        var k = FamilienGrafik.KoerperFuer(Grafik.QuelleFuer(p.Katalog, Hole(p, "BO-1"))!, p, Hole(p, "BO-1"), "mittel")!;
        Etwa(550, k.Teile.SelectMany(t => t.Ecken!).Max(e => e[2]), Facette);
    }
}
