using System.Text.Json.Nodes;
using Xunit.Abstractions;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.SchemaansichtHilfe;
using static Hlks.Kern.Tests.SchemaStrangschemaHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3, Nachtrag B8d: Apparate auf dem Boden, Abläufe im Deckenband, Schächte im Schnitt und Grundleitungen unter dem
/// untersten Band (SchemaAnordnung.cs «Apparate auf dem Boden» … «Grundleitungen», SchemaGrafik.cs, SchemaSymbole.cs). Testhaus «MFH
/// Testweg» (SchemaStrangschemaHilfe, FIKTIV): Zeilen UG 0, EG 50, 1.OG 100, 2.OG 150; Bänder 2.4 mm mit Mitte auf y0 (Band 0 −1.2 … 1.2).
///
/// Testvariante MfhMitGrundleitung (FIKTIV): Bodenplatte D-BP unter dem UG (0…12000 × 0…10000, Oberkante −2700, Dicke 300, «Beton 300»),
/// Grundleitung SW-G im Erdreich (z −3300 … −3500) von zwei Bodenabläufen BA-U1, BA-U2 im UG über die T-Stücke TS-G-2 und TS-G-3 (ohne
/// Geschoss) zum Kontrollschacht KS-1 (12500, 5200, −3500) ausserhalb des Gebäudes; die Sammelleitung SW-S fällt bei TS-SS-2 senkrecht in
/// die Grundleitung (TS-G-3 liegt im Grundriss genau darunter). Die Regenwasserleitung RE-R1-U → KS-2 bleibt frei verlegt (im UG).</summary>
public class SchemaUntergrundTests(ITestOutputHelper aus)
{
    static void A(Projekt p, string befehl, JsonObject par) => Ok(p.Ausfuehren(befehl, par));

    static void Punkt(Projekt p, string klasse, string kz, double[] punkt, string? teilVon = null, string? geschoss = null)
    {
        var o = new JsonObject { ["klasse"] = klasse, ["kennzeichen"] = kz, ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts(punkt) } };
        if (teilVon is not null) o["teilVon"] = teilVon;
        if (geschoss is not null) o["geschoss"] = geschoss;
        A(p, "element_anlegen", o);
    }

    static void Rohr(Projekt p, string kz, string strang, double dn, string von, string nach, params double[][] punkte) =>
        A(p, "leitung_ziehen", new JsonObject
        {
            ["kennzeichen"] = kz, ["strang"] = strang, ["dn"] = dn, ["material"] = "Testrohr (fiktiv)", ["von"] = von, ["nach"] = nach, ["punkte"] = Pts(punkte),
        });

    static void Geometrie(Projekt p, string kz, string art, params double[][] punkte) =>
        A(p, "element_aendern", new JsonObject { ["element"] = kz, ["geometrie"] = new JsonObject { ["art"] = art, ["punkte"] = Pts(punkte) } });

    /// <summary>MFH mit Bodenplatte, zwei Bodenabläufen im UG und Grundleitung im Erdreich zum Kontrollschacht KS-1 (siehe Klassenkopf).
    /// Verlegung (Handrechnung wie Verlegung.cs): SW-G-1 7750 mm, davon 250 im Beton der Platte und 7500 darunter → erdreich;
    /// SW-G-BA2 3800 / 3550 → erdreich; SW-G-2, SW-G-3 ganz darunter → erdreich; SW-S-3 2600 mm senkrecht, 400 darunter (15 %) → frei;
    /// TS-G-2 wie seine Leitungen → erdreich; TS-G-3 (Leitungen uneinig) nach dem Einfügepunkt unter der Platte → erdreich.</summary>
    public static Projekt MfhMitGrundleitung()
    {
        var p = Mfh();
        A(p, "element_anlegen", new JsonObject
        {
            ["klasse"] = "Decke", ["kennzeichen"] = "D-BP", ["geschoss"] = "UG", ["eigenschaften"] = new JsonObject { ["aufbau"] = "Beton 300", ["art"] = "boden" },
            ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = Pts([0, 0, -2700], [12000, 0, -2700], [12000, 10000, -2700], [0, 10000, -2700]), ["dicke"] = 300 },
        });
        A(p, "element_anlegen", new JsonObject
        {
            ["klasse"] = "Strang", ["kennzeichen"] = "SW-G", ["name"] = "Grundleitung SW", ["teilVon"] = "SW", ["eigenschaften"] = new JsonObject { ["art"] = "grundleitung" },
        });
        Punkt(p, "Bodenablauf", "BA-U1", [4000, 2000, -2700], geschoss: "UG");
        Punkt(p, "Bodenablauf", "BA-U2", [8000, 2000, -2700], geschoss: "UG");
        Punkt(p, "TStueck", "TS-G-2", [8000, 5200, -3350], teilVon: "SW-G");
        Punkt(p, "TStueck", "TS-G-3", [9300, 5200, -3400], teilVon: "SW-G");
        A(p, "element_loeschen", new JsonObject { ["element"] = "SW-S-3" });
        Geometrie(p, "KS-1", "punkt", [12500, 5200, -3500]);
        Rohr(p, "SW-G-1", "SW-G", 100, "BA-U1.sw", "TS-G-2.a", [4000, 2000, -2750], [4000, 2000, -3300], [4000, 5200, -3300], [8000, 5200, -3350]);
        Rohr(p, "SW-G-BA2", "SW-G", 75, "BA-U2.sw", "TS-G-2.c", [8000, 2000, -2750], [8000, 2000, -3350], [8000, 5200, -3350]);
        Rohr(p, "SW-G-2", "SW-G", 100, "TS-G-2.b", "TS-G-3.a", [8000, 5200, -3350], [9300, 5200, -3400]);
        Rohr(p, "SW-S-3", "SW-S", 125, "TS-SS-2.b", "TS-G-3.c", [9300, 5200, -800], [9300, 5200, -3400]);
        Rohr(p, "SW-G-3", "SW-G", 125, "TS-G-3.b", "KS-1.ein", [9300, 5200, -3400], [12500, 5200, -3500]);
        return p;
    }

    static string P(double x, double y) => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"({x:0.##},{y:0.##})");

    static SchemaZeile Zeile(Schemaplan s, string titel) => s.Zeilen.Single(z => z.Titel == titel);

    /// <summary>Waagrechte Stücke aller Linien auf der Höhe y (x von, x bis, Linie).</summary>
    static List<(double X0, double X1, SchemaLinie L)> Waagrecht(Schemaplan s, double y) =>
        s.Graph.Linien.SelectMany(l => l.Punkte.Zip(l.Punkte.Skip(1)).Where(x => Math.Abs(x.First[1] - y) < 1e-6 && Math.Abs(x.Second[1] - y) < 1e-6)
            .Select(x => (Math.Min(x.First[0], x.Second[0]), Math.Max(x.First[0], x.Second[0]), l))).ToList();

    // ------------------------------------------------------------------ 1. Apparate auf dem Boden

    /// <summary>Schema Abwasser, EG (y0 50): Boden = Etagenleitung des Steig-T TS-S1-E (y0 + 20 = 70), Unterkante 73; Knoten = Unterkante +
    /// Abfluss unter dem Knoten (WC 4.375, WT 2.625, DU 2.5, SP 1.575, siehe SchemaSinnbilderTests). Der Anschluss läuft kurz senkrecht
    /// nach unten auf die Etagenleitung; die Fallleitungs-Abgänge bleiben auf y0 + 20 unter dem Endgerät.</summary>
    [Fact]
    public void Apparate_StehenAufDemBoden_AnschlussKurzSenkrechtNachUnten()
    {
        var s = Plan(Mfh(), Abwasser);
        foreach (var (g, y0) in new[] { ("E", 50.0), ("1", 100.0), ("2", 150.0) })
        {
            foreach (var (a, ab) in new[] { ("WC", 4.375), ("WT", 2.625), ("DU", 2.5), ("SP", 1.575) })
            {
                var k = Knoten(s, $"{a}-{g}1");
                Assert.Equal(y0 + 23, k.Unterkante!.Value, 6);
                Assert.Equal(y0 + 23 + ab, k.Y, 6);
                Assert.True(k.Y < y0 + SchemaRaster.Endgeraetehoehe);                     // tiefer als bis B8c
            }
            // Fallleitungs-Abgänge (Steig-T und Etagen-T) bleiben auf der Etagenleitung
            Assert.Equal((40.0, y0 + 20), Lage(s, $"TS-S1-{g}"));
            Assert.Equal((55.0, y0 + 20), Lage(s, $"TS-S{g}1-1"));
        }
        Assert.Equal("(55,77.38) (55,70)", Zug(LinieMit(s, "SW-E1-WC")));                  // WC → T: 7.375 mm senkrecht
        Assert.Equal("(68,75.63) (68,70)", Zug(LinieMit(s, "SW-E1-WT")));
        Assert.Equal("(94,74.58) (94,70) (81,70)", Zug(LinieMit(s, "SW-E1-SP")));          // Spültisch am 3. T: senkrecht, dann der Etagenleitung entlang
        // UG: die Waschmaschine hängt an der Sammelleitung (Spur y0 + 4): Anschlusslinie 4 + 3 = 7, Unterkante 7 + 3 = 10, Knoten 13
        var wm = Knoten(s, "WM-U");
        Assert.Equal((10.0, 13.0), (wm.Unterkante!.Value, wm.Y));
        var lwm = LinieMit(s, "SW-S-WM");
        Assert.Equal(7, lwm.AnschlussY);
        Assert.Equal($"{P(wm.X, 13)} {P(wm.X, 7)} (145,7) (145,4)", Zug(lwm));
        Assert.Equal((145.0, 4.0), Lage(s, "TS-SS-WM"));                                     // Spur unverändert
        // Kennzeichen weiter über dem Sinnbild
        var p = Mfh();
        var g2 = Grafik.Plan(p, Abwasser);
        var wc = Knoten(Plan(p, Abwasser), "WC-E1");
        var t = g2.Teile.Single(x => x.Id == wc.Element.Id);
        var kz = t.Primitive.Single(x => x.Art == "text" && x.Text == "WC-E1");
        double oben = Grafik.Begrenzung([new Grafikteil { Primitive = t.Primitive.Where(x => x.Art != "text").ToList() }])![3];
        Assert.True(kz.Punkte[0][1] > oben, $"Kennzeichen {kz.Punkte[0][1]} nicht über dem Sinnbild {oben}");
    }

    /// <summary>Schema Wasser: Boden der Zeile = oberste Etagen- bzw. Anschlusslinie. EG: Wurzeln TS-K1-E 70, TS-W1-E 73.5, TS-K1-EA 77
    /// (Ausgussbecken), dazu TS-K2-E, TS-W2-E → 77 + 3 = 80, AB-E1 (Waschtrog 3) bei 83. UG: Spuren KW-V 4, WW-V 8, ZIR-V 12; Anschlusslinien
    /// der Verteilleitung KW-V zu HA-1 (x0 + 10 → x0 + 25), WE-1 (x0 + 20 → x0 + 38), WM-U (x0 + 30 → x0 + 51): HA-1 und WM-U überdecken sich
    /// nicht (25 + 3 ≤ 30) → Stufe 0 = 12 + 3 = 15, WE-1 überdeckt HA-1 → Stufe 1 = 18; Unterkante 18 + 3 = 21, Kästchen Gerät bzw.
    /// Waschmaschine 3 → 24. Die Wohnungsblöcke bleiben, wie sie sind, und nichts überdeckt sich.</summary>
    [Fact]
    public void Apparate_Wasser_UeberDerOberstenAnschlussspur_BloeckeUnveraendert()
    {
        var s = Plan(Mfh(), Wasser);
        var ab = Knoten(s, "AB-E1");
        Assert.Equal((80.0, 83.0), (ab.Unterkante!.Value, ab.Y));
        var ha = Knoten(s, "HA-1"); var we = Knoten(s, "WE-1"); var wm = Knoten(s, "WM-U");
        var x0 = s.Gruppen.Single(gr => gr.Bezeichnung == "KW-V").X0;
        Assert.Equal([x0 + 25, x0 + 38, x0 + 51], new[] { ha.X, we.X, wm.X });
        Assert.All(new[] { ha, we, wm }, k => Assert.Equal((21.0, 24.0), (k.Unterkante!.Value, k.Y)));
        Assert.Equal((15.0, 18.0, 15.0), (LinieMit(s, "KW-V-1").AnschlussY!.Value, LinieMit(s, "KW-V-WE").AnschlussY!.Value, LinieMit(s, "KW-V-WM").AnschlussY!.Value));
        var kwvWe = LinieMit(s, "KW-V-WE");
        Assert.Equal($"{P(x0 + 20, 4)} {P(x0 + 20, 18)} {P(x0 + 38, 18)} {P(x0 + 38, 24)}", Zug(kwvWe));
        // Wohnungsblöcke unverändert (B8: x 92, y 67, 38 × 29 im EG) und frei von Endgeräten
        var b = s.Bloecke.Single(x => x.Zone.Kennzeichen == "W-E1");
        Assert.Equal((92.0, 67.0, 38.0, 29.0), (b.X, b.Y, b.Breite, b.Hoehe));
        var p = Mfh();
        var g = Grafik.Plan(p, Wasser);
        var plan = Plan(p, Wasser);
        foreach (var k in plan.Graph.Knoten.Where(k => k.Unterkante is not null))
        {
            var r = Grafik.Begrenzung([g.Teile.Single(x => x.Id == k.Id)])!;
            foreach (var bl in plan.Bloecke.Where(x => x.Zeile == k.Zeile))
                Assert.False(r[0] < bl.X + bl.Breite && bl.X < r[2] && r[1] < bl.Y + bl.Hoehe && bl.Y < r[3], $"{k.Kennzeichen} überdeckt den Block {bl.Titel}");
        }
    }

    /// <summary>Knoten auf dem Abgang (nur ein Endgerät dahinter): Unterkante mindestens Etagenleitung + (k + 1) · Abgangschritt, der Knoten
    /// gleichmässig dazwischen. Strang KW-1 (EG): Steig-T TS-1 (y 70) → Ventil V-A → WT-1; V-A hat nur WT-1 dahinter → Abgang; Unterkante
    /// = max(70 + 3, 70 + 2 · 5) = 80, V-A auf 70 + (80 − 70) / 2 = 75 senkrecht unter dem Waschtisch (x 55), WT-1 auf 80 + 2.625.</summary>
    [Fact]
    public void Abgang_MitArmatur_GibtPlatzUnterDemEndgeraet()
    {
        var p = Projekt.Anlegen(Kat, null, "Abgang (fiktiv)", "T", "pc");
        A(p, "element_anlegen", Par(("klasse", "Gebaeude"), ("kennzeichen", "GEB")));
        A(p, "element_anlegen", Par(("klasse", "Geschoss"), ("kennzeichen", "UG"), ("teilVon", "GEB"), ("eigenschaften", new JsonObject { ["kote"] = -2700, ["hoehe"] = 2700 })));
        A(p, "element_anlegen", Par(("klasse", "Geschoss"), ("kennzeichen", "EG"), ("teilVon", "GEB"), ("eigenschaften", new JsonObject { ["kote"] = 0, ["hoehe"] = 2900 })));
        A(p, "element_anlegen", Par(("klasse", "System"), ("kennzeichen", "KW"), ("eigenschaften", new JsonObject { ["systemtyp"] = "Trinkwasser kalt" })));
        A(p, "element_anlegen", Par(("klasse", "Strang"), ("kennzeichen", "KW-1"), ("teilVon", "KW"), ("eigenschaften", new JsonObject { ["art"] = "steigstrang" })));
        Punkt(p, "TStueck", "TS-0", [1000, 0, -2400], teilVon: "KW-1");
        Punkt(p, "TStueck", "TS-1", [1000, 0, 300], teilVon: "KW-1");
        A(p, "element_anlegen", new JsonObject
        {
            ["klasse"] = "Ventil", ["kennzeichen"] = "V-A", ["teilVon"] = "KW-1", ["eigenschaften"] = new JsonObject { ["art"] = "absperrventil" },
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([1300, 0, 300]) },
        });
        Punkt(p, "Waschtisch", "WT-1", [1500, 500, 0], geschoss: "EG");
        Rohr(p, "R-0", "KW-1", 20, "TS-0.b", "TS-1.a", [1000, 0, -2400], [1000, 0, 300]);
        Rohr(p, "R-1", "KW-1", 15, "TS-1.c", "V-A.ein", [1000, 0, 300], [1300, 0, 300]);
        Rohr(p, "R-2", "KW-1", 15, "V-A.aus", "WT-1.kw", [1300, 0, 300], [1500, 0, 300], [1500, 500, 300]);
        A(p, "element_anlegen", Par(("klasse", "Ansicht"), ("kennzeichen", "AN-AB"), ("eigenschaften", new JsonObject { ["art"] = "schema" })));
        var s = Plan(p, "AN-AB");
        var wt = Knoten(s, "WT-1");
        Assert.Equal((55.0, 80.0, 82.625), (wt.X, wt.Unterkante!.Value, wt.Y));
        Assert.Equal((55.0, 75.0, "abgang"), (Lage(s, "V-A").X, Lage(s, "V-A").Y, Knoten(s, "V-A").Ausrichtung));
        Assert.Equal("(40,70) (55,70) (55,75)", Zug(LinieMit(s, "R-1")));
        Assert.Equal("(55,75) (55,82.63)", Zug(LinieMit(s, "R-2")));
    }

    /// <summary>Geht die Linie eines Geräts auf dem Boden senkrecht nach oben ab, steht sein Kennzeichen rechts neben dem Symbol (sonst
    /// liefe die Linie durch den Text): MFH mit Einlagen (SchemaEinlagenTests), WM-U hängt an TS-SS-WM in der Decke über dem UG.</summary>
    [Fact]
    public void Kennzeichen_RechtsDaneben_WennDieLinieNachObenAbgeht()
    {
        var p = SchemaEinlagenTests.MfhMitEinlagen();
        var s = Plan(p, Abwasser);
        var wm = Knoten(s, "WM-U");
        var l = LinieMit(s, "SW-S-WM");
        Assert.Same(wm, l.Von.Knoten);
        Assert.Equal(wm.X, l.Punkte[1][0], 6);
        Assert.True(l.Punkte[1][1] > wm.Y);                                                   // vom Gerät senkrecht nach oben
        var kz = SchemaGrafik.Plan(p, s).Teile.Single(x => x.Id == wm.Id).Primitive.Single(x => x.Art == "text" && x.Text == "WM-U");
        Assert.Equal("links", kz.Ausrichtung);
        Assert.Equal(wm.X + 3 + 1, kz.Punkte[0][0], 6);                                       // Waschmaschine halb 3 breit, 1 Abstand
        Assert.Equal(wm.Y - 0.6, kz.Punkte[0][1], 6);
        // im Testhaus ohne Einlagen geht die Linie nach unten ab: Kennzeichen mittig über dem Sinnbild
        var p2 = Mfh();
        var wm2 = Knoten(Plan(p2, Abwasser), "WM-U");
        var kz2 = Grafik.Plan(p2, Abwasser).Teile.Single(x => x.Id == wm2.Id).Primitive.Single(x => x.Art == "text" && x.Text == "WM-U");
        Assert.Equal(("mitte", wm2.X), (kz2.Ausrichtung, kz2.Punkte[0][0]));
    }

    // ------------------------------------------------------------------ 2. Abläufe im Band

    /// <summary>Dachablauf DAB-1 (2.OG, z 8700 = Kote 5800 + Höhe 2900 → Dach über dem 2.OG = oberstes Band 4, Mitte 200, Oberkante 201.2):
    /// Quadrat 2.4 bündig unter der Bandoberkante, Knoten y 200; x wie ein Endgerät von WAR-R 01 (Stranglinie 110 + 15 = 125). Die Linie
    /// verlässt den Ablauf senkrecht bis 2 mm unter das Band (198.8 − 2 = 196.8), dann zum Fallstrang und hinunter zur Reinigungsöffnung.
    /// Liegt der Dachablauf näher am Boden seines Geschosses, sitzt er im Band darunter.</summary>
    [Fact]
    public void Dachablauf_ImOberstenBand_OberkanteBuendig()
    {
        var p = Mfh();
        var s = Plan(p, Abwasser);
        var dab = Knoten(s, "DAB-1");
        var band = s.Baender[^1];
        Assert.Same(band, dab.Ablaufband);
        Assert.Equal((125.0, 200.0), (dab.X, dab.Y));
        Assert.Equal(band.Oben - SchemaRaster.Ablaufgroesse / 2, dab.Y, 6);
        Assert.Null(dab.Unterkante);
        Assert.Equal("(125,200) (125,196.8) (110,196.8) (110,70)", Zug(LinieMit(s, "RW-1-2")));
        // Grafik: Quadrat 2.4 × 2.4, Oberkante = Bandoberkante, in der Medienfarbe bzw. wie die Sinnbilder; Kennzeichen über dem Band
        var t = Grafik.Plan(p, Abwasser).Teile.Single(x => x.Id == dab.Element.Id);
        var q = t.Primitive.Single(x => x.Art == "polygon");
        Assert.Equal((123.8, 126.2, 198.8, 201.2), (q.Punkte.Min(x => x[0]), q.Punkte.Max(x => x[0]), q.Punkte.Min(x => x[1]), q.Punkte.Max(x => x[1])));
        Assert.Equal("papier", q.Stil.Fuellung);
        // ohne eigenes System in der Medienfarbe seiner Linie (Regenwasser)
        var farbe = HlksEinordnung.Farbe(p.Katalog.Medien[LinieMit(s, "RW-1-2").Medium!]);
        Assert.NotNull(farbe);
        Assert.Equal(farbe, q.Stil.Farbe);
        Assert.Equal(band.Oben + 0.8, t.Primitive.Single(x => x.Art == "text" && x.Text == "DAB-1").Punkte[0][1], 6);
        // näher an der Kote des 2.OG (5800) als an 8700: Band unter dem 2.OG (Band 3, Mitte 150)
        Geometrie(p, "DAB-1", "punkt", [6000, 9800, 5900]);
        var s2 = Plan(p, Abwasser);
        Assert.Same(s2.Baender[3], Knoten(s2, "DAB-1").Ablaufband);
        Assert.Equal(150, Knoten(s2, "DAB-1").Y, 6);
    }

    /// <summary>Bodenablauf im Band unter seinem Geschoss: Testvariante, BA-U1 und BA-U2 im UG → Band 0 (Mitte 0, Oberkante 1.2), Knoten
    /// y 0. Sinnbild der HKAP auf 2.4 mm verkleinert (Quadrat, zwei Diagonalen, Punkt).</summary>
    [Fact]
    public void Bodenablauf_ImBandUnterSeinemGeschoss_SinnbildInBandhoehe()
    {
        var p = MfhMitGrundleitung();
        var s = Plan(p, Abwasser);
        foreach (var kz in new[] { "BA-U1", "BA-U2" })
        {
            var k = Knoten(s, kz);
            Assert.Same(s.Baender[0], k.Ablaufband);
            Assert.Equal(0, k.Y, 6);
        }
        var t = Grafik.Plan(p, Abwasser).Teile.Single(x => x.Id == Hole(p, "BA-U1").Id);
        var q = t.Primitive.Single(x => x.Art == "polygon");
        Assert.Equal(SchemaRaster.Ablaufgroesse, q.Punkte.Max(x => x[1]) - q.Punkte.Min(x => x[1]), 6);
        Assert.Equal(s.Baender[0].Oben, q.Punkte.Max(x => x[1]), 6);
        Assert.Equal(2, t.Primitive.Count(x => x.Art == "linie"));
        Assert.Single(t.Primitive, x => x.Art == "kreis");
    }

    /// <summary>Ein Einlagezug läuft auf Spur 0 nicht durch einen fremden Ablauf: Decke über UG mit der Sammelleitung als Einlage (TS-S1-E →
    /// BO-SS-1 → TS-SS-WM, Zug x 40 … 145 auf Spur 0, wie SchemaEinlagenTests) und ein Bodenablauf BA-E1 im EG, frei an einem neuen T-Stück
    /// TS-SE1-4 der Etagenleitung von SW-1 (Platz x 94 zwischen 40 und 145). Der Zug weicht auf Spur 1 aus; Spur 0 bleibt dem Ablauf, das
    /// Band wächst auf 2 · 1.2 + 1.5 = 3.9 mm.</summary>
    [Fact]
    public void Ablauf_ImBand_EinlageWeichtAufDieNaechsteSpurAus()
    {
        var p = SchemaEinlagenTests.MfhMitEinlagen(einlagen: false);
        const double z = -150;
        Geometrie(p, "BO-SS-1", "punkt", [2300, 5200, z]);
        Geometrie(p, "TS-SS-WM", "punkt", [6300, 5200, z]);
        Geometrie(p, "SW-1-E", "polylinie", [2300, 5200, 150], [2300, 5200, z]);
        Geometrie(p, "SW-S-1", "polylinie", [2300, 5200, z], [6300, 5200, z]);
        A(p, "element_loeschen", new JsonObject { ["element"] = "SW-E1-DU" });
        Punkt(p, "TStueck", "TS-SE1-4", [4500, 5200, 150], teilVon: "SW-1");
        Punkt(p, "Bodenablauf", "BA-E1", [4500, 6000, 0], geschoss: "EG");
        Rohr(p, "SW-E1-DU", "SW-1", 50, "DU-E1.sw", "TS-SE1-4.a", [4800, 6500, 150], [4800, 5200, 150], [4500, 5200, 150]);
        Rohr(p, "SW-E1-4", "SW-1", 75, "TS-SE1-4.b", "TS-SE1-3.a", [4500, 5200, 150], [4200, 5200, 150]);
        Rohr(p, "SW-E1-BA", "SW-1", 50, "BA-E1.sw", "TS-SE1-4.c", [4500, 6000, 0], [4500, 6000, 150], [4500, 5200, 150]);
        var s = Plan(p, Abwasser);
        var eg = Zeile(s, "EG");
        var band = s.Baender[eg.Index];
        var ba = Knoten(s, "BA-E1");
        Assert.Same(band, ba.Ablaufband);
        var zug = LinieMit(s, "SW-S-1");
        Assert.Same(band, zug.Band);
        Assert.InRange(ba.X, Knoten(s, "TS-S1-E").X + 5, Knoten(s, "TS-SS-WM").X - 5);
        Assert.Equal(["", "SW-1"], band.Spuren);
        Assert.Equal(3.9, band.Hoehe, 6);
        Assert.Equal((band.SpurY(0), band.SpurY(1)), (ba.Y, zug.BandY));
        Assert.Equal(band.Oben - SchemaRaster.Ablaufgroesse / 2, ba.Y, 6);
        // ohne Ablauf (gleiches Modell ohne BA-E1): der Zug liegt auf Spur 0, das Band bleibt 2.4
        A(p, "element_loeschen", new JsonObject { ["element"] = "SW-E1-BA" });
        A(p, "element_loeschen", new JsonObject { ["element"] = "BA-E1" });
        var s2 = Plan(p, Abwasser);
        Assert.Equal(["SW-1"], s2.Baender[eg.Index].Spuren);
        Assert.Equal(SchemaRaster.Deckenband, s2.Baender[eg.Index].Hoehe, 6);
    }

    // ------------------------------------------------------------------ 3. Schächte im Schnitt

    /// <summary>Testhaus: KS-1 (12500, 5200) und KS-2 (12500, 9800) liegen östlich aller Gruppen → unter der letzten Gruppe WAS-H 02
    /// (x0 = 30 + 70 + 35 + 35 + 35 = 205, Stranglinie 215), nach Lage quer gleich, dann nach Tiefe (nahe zuerst): KS-1 x 215 + 15 = 230,
    /// KS-2 243. Rechteck 8 breit ab der
    /// Oberkante von Band 0 (1.2); KS-1: Unterkante Band 0 (−1.2) − 12 = −13.2 (Einlauf SW-S-3 auf Spur 0 = −1.2 − 3 = −4.2, Sohle −4.2 − 2
    /// liegt höher). Die Regenwasserleitung zu KS-2 läuft unter KS-1 durch: Spur ≥ (12 + 1.5 − 3) / 4 → 3, y = −4.2 − 12 = −16.2; KS-2
    /// Unterkante −16.2 − 2 = −18.2. Fläche unter dem Band: max(3 + 3 · 4, 17) + 3 = 20 → Rahmen unten −21.2.</summary>
    [Fact]
    public void Schaechte_ImSchnittUnterDemUnterstenBand_NachLage()
    {
        var p = Mfh();
        var s = Plan(p, Abwasser);
        var ks1 = Knoten(s, "KS-1").Schacht!; var ks2 = Knoten(s, "KS-2").Schacht!;
        Assert.Equal("WAS-H 02", ks1.Gruppe.Bezeichnung);
        Assert.Same(ks1.Gruppe, ks2.Gruppe);
        Assert.Equal(215, s.Spalten.Single(x => x.Titel == "SW-2").LinieX);
        Assert.Equal((230.0, 243.0), (ks1.X, ks2.X));
        Assert.Equal((1.2, -13.2), (ks1.Oben, ks1.Unten));
        Assert.Equal((1.2, -18.2), (ks2.Oben, ks2.Unten));
        var u = s.Untergrund!;
        Assert.Equal((-1.2, 20.0), (u.Oben, u.Hoehe));
        Assert.Equal(["KS-1", "KS-2"], u.Schaechte.Select(x => x.Knoten.Kennzeichen));
        Assert.Equal(-21.2, SchemaGrafik.Rahmen(s).Y0, 6);
        Assert.DoesNotContain(s.Zeilen, z => z.IstZone);
        // Ein- und Ausläufe seitlich an der Wand: SW-S-3 von TS-SS-2 (215, 4) senkrecht hinunter auf Spur 0, dann zur linken Wand 226
        Assert.Equal("(215,4) (215,-4.2) (226,-4.2)", Zug(LinieMit(s, "SW-S-3")));
        var rw = LinieMit(s, "RW-G-1");
        Assert.True(rw.Untergrund);
        Assert.Equal("(110,20) (110,-16.2) (239,-16.2)", Zug(rw));
        Assert.True(rw.UntergrundY < ks1.Unten - SchemaRaster.Querabstand + 1e-9);   // unter KS-1 durch
        // Grafik: Rechteck (Schachtwand) papierweiss, Deckelstrich über die Wand hinaus, Kennzeichen senkrecht; kein Doppelkreis mehr
        var t = Grafik.Plan(p, Abwasser).Teile.Single(x => x.Id == Hole(p, "KS-1").Id);
        var r = t.Primitive.Single(x => x.Art == "polygon");
        Assert.Equal((226.0, 234.0, -13.2, 1.2), (r.Punkte.Min(x => x[0]), r.Punkte.Max(x => x[0]), r.Punkte.Min(x => x[1]), r.Punkte.Max(x => x[1])));
        Assert.Equal("papier", r.Stil.Fuellung);
        var deckel = t.Primitive.Single(x => x.Art == "linie");
        Assert.Equal(Stifte.Mittel, deckel.Stil.Stift);
        Assert.Equal((225.2, 234.8, 1.2), (deckel.Punkte.Min(x => x[0]), deckel.Punkte.Max(x => x[0]), deckel.Punkte[0][1]));
        var text = t.Primitive.Single(x => x.Art == "text");
        Assert.Equal(("KS-1", 90.0), (text.Text, text.Winkel!.Value));
        Assert.Equal(((-1.2 - 13.2) / 2), text.Punkte[0][1], 6);                       // Mitte des Teils unter dem Band
        Assert.DoesNotContain(t.Primitive, x => x.Art == "kreis");
        Assert.Equal("schacht", SchemaSymbole.Schluessel(p, Hole(p, "KS-1")));
        Assert.DoesNotContain(SchemaSymbole.Zuordnung, z => z.Klasse == "Kontrollschacht");
        // Hinweis
        Assert.Contains(s.Hinweise, h => h.StartsWith("Unter dem untersten Band: 2 Linie(n)") && h.Contains("Schächte im Schnitt: KS-1, KS-2"));
    }

    [Fact]
    public void Schacht_Klassen_NurDieDerKatalogFuehrt()
    {
        // im Katalog gibt es heute nur Kontrollschacht; Pumpenschacht, Abscheider, Hebeanlage greifen, sobald der Katalog sie führt
        Assert.Equal(["Kontrollschacht"], Schemaplan.SchachtKlassen.Where(Kat.Klassen.ContainsKey));
        Assert.Equal(["Bodenablauf", "Dachablauf"], Schemaplan.AblaufKlassen.Where(Kat.Klassen.ContainsKey));
        Assert.False(Kat.Klassen.ContainsKey("Terrasseneinlauf"));
    }

    /// <summary>Von Hand gesetzt gilt die Hand-Lage: der Schacht steht frei im Schnitt um seinen Knoten, nicht unter dem Band; ohne
    /// Schacht und Leitung im Erdreich gibt es keine Fläche unter dem Band.</summary>
    [Fact]
    public void Schacht_MitHandLage_StehtFrei()
    {
        var p = Mfh();
        Ok(p.Ausfuehren("schema_lage", Par(("ansicht", Abwasser), ("element", "KS-1"), ("x", 200.0), ("y", 30.0))));
        var s = Plan(p, Abwasser);
        var k = Knoten(s, "KS-1");
        Assert.True(k.Manuell);
        Assert.Null(k.Schacht);
        Assert.Equal((200.0, 30.0), (k.X, k.Y));
        Assert.Equal(["KS-2"], s.Untergrund!.Schaechte.Select(x => x.Knoten.Kennzeichen));
        var t = Grafik.Plan(p, Abwasser).Teile.Single(x => x.Id == k.Element.Id);
        var r = t.Primitive.Single(x => x.Art == "polygon" && x.Stil.Fuellung == "papier");
        Assert.Equal((196.0, 204.0, 24.0, 36.0), (r.Punkte.Min(x => x[0]), r.Punkte.Max(x => x[0]), r.Punkte.Min(x => x[1]), r.Punkte.Max(x => x[1])));
    }

    // ------------------------------------------------------------------ 4. Grundleitungen unter dem Band

    /// <summary>Testvariante: Verlegung wie im Kopf von MfhMitGrundleitung. Knoten im Erdreich: TS-G-3 unter TS-SS-2 (direkt verbunden, 0 mm
    /// daneben → x 285), TS-G-2 unter dem Platz stromauf, der dem Abfluss (KS-1, x 300) am nächsten liegt: BA-U1 (die Plätze stehen nach
    /// Lage im Netz, BA-U2 mit dem kürzeren Weg zuerst: BA-U2 x0 + 25, BA-U1 x0 + 38). Alle Grundleitungen auf
    /// Spur 0 (−1.2 − 3 = −4.2): sie gehören zu einem Zug und stossen nur aneinander. Die T-Stücke ohne Geschoss gehören zum UG (keine Zone
    /// «ohne Geschoss»); die Grundleitung belegt im UG keine Spur (die Sammelleitung bleibt auf 4).</summary>
    [Fact]
    public void Grundleitungen_UnterDemUnterstenBand_ZumSchacht()
    {
        var p = MfhMitGrundleitung();
        var vs = Verlegung.Von(p);
        foreach (var kz in new[] { "SW-G-1", "SW-G-BA2", "SW-G-2", "SW-G-3", "TS-G-2", "TS-G-3" })
            Assert.Equal("erdreich", vs.Befund(Hole(p, kz).Id)!.Verlegung);
        Assert.Equal("frei", vs.Befund(Hole(p, "SW-S-3").Id)!.Verlegung);
        var s = Plan(p, Abwasser);
        var ug = Zeile(s, "UG");
        Assert.DoesNotContain(s.Zeilen, z => z.IstZone);
        Assert.DoesNotContain(s.Hinweise, h => h.StartsWith("Ohne Geschoss"));
        var ts2 = Knoten(s, "TS-G-2"); var ts3 = Knoten(s, "TS-G-3");
        Assert.All(new[] { ts2, ts3 }, k => { Assert.True(k.Untergrund); Assert.Same(ug, k.Zeile); Assert.Equal(-4.2, k.Y, 6); });
        Assert.Equal(Knoten(s, "TS-SS-2").X, ts3.X);
        Assert.Equal(285, ts3.X);
        Assert.Equal(Knoten(s, "BA-U1").X, ts2.X);
        Assert.True(Knoten(s, "BA-U2").X < Knoten(s, "BA-U1").X);
        Assert.Equal((145.0, 4.0), Lage(s, "TS-SS-WM"));
        double x1 = Knoten(s, "BA-U1").X, x2 = Knoten(s, "BA-U2").X;
        Assert.Equal($"{P(x1, 0)} {P(x1, -4.2)}", Zug(LinieMit(s, "SW-G-1")));
        Assert.Equal($"{P(x2, 0)} {P(x2, -4.2)} {P(x1, -4.2)}", Zug(LinieMit(s, "SW-G-BA2")));
        Assert.Equal($"{P(x1, -4.2)} (285,-4.2)", Zug(LinieMit(s, "SW-G-2")));
        Assert.Equal("(285,4) (285,-4.2)", Zug(LinieMit(s, "SW-S-3")));
        Assert.Equal("(285,-4.2) (296,-4.2)", Zug(LinieMit(s, "SW-G-3")));
        Assert.All(new[] { "SW-G-1", "SW-G-BA2", "SW-G-2", "SW-S-3", "SW-G-3" }, kz => Assert.True(LinieMit(s, kz).Untergrund, kz));
        Assert.Equal(1.0, LinieMit(s, "SW-G-3").Erdreichanteil, 6);
        Assert.Equal("SW-G, SW-S", s.Untergrund!.Spuren[0]);
        // auf derselben Spur überdecken sich keine waagrechten Stücke
        foreach (var y in new[] { -4.2, -16.2 })
        {
            var w = Waagrecht(s, y);
            for (int i = 0; i < w.Count; i++)
                for (int j = i + 1; j < w.Count; j++)
                    Assert.False(w[i].X0 < w[j].X1 - 1e-6 && w[j].X0 < w[i].X1 - 1e-6, $"Überdeckung auf {y}: {Zug(w[i].L)} und {Zug(w[j].L)}");
        }
        // KS-1 tiefer als bis zur Sohle seines Einlaufs: Unterkante −1.2 − 12 = −13.2; RW unter KS-1 durch auf −16.2
        Assert.Equal(-13.2, Knoten(s, "KS-1").Schacht!.Unten, 6);
        Assert.Equal(-16.2, LinieMit(s, "RW-G-1").UntergrundY, 6);
        Assert.Contains(s.Hinweise, h => h.StartsWith("Unter dem untersten Band: 6 Linie(n) auf 2 Grundleitungsspuren, Schächte im Schnitt: KS-1, KS-2"));
        // Grössentexte freigestellt: keine Überdeckung der Texte unter dem Band
        var g = SchemaGrafik.Plan(p, s);
        var texte = g.Teile.SelectMany(t => t.Primitive).Where(x => x.Art == "text" && x.Groesse == 1.6 && x.Punkte[0][1] < s.Baender[0].Unten).ToList();
        Assert.NotEmpty(texte);
        var rahmen = texte.Select(t => (t, r: t.Winkel is null
            ? new[] { t.Punkte[0][0] - SchemaRaster.TextBreite(t.Text!, 1.6) / 2, t.Punkte[0][1], t.Punkte[0][0] + SchemaRaster.TextBreite(t.Text!, 1.6) / 2, t.Punkte[0][1] + 1.2 }
            : new[] { t.Punkte[0][0] - 1.2, t.Punkte[0][1] - SchemaRaster.TextBreite(t.Text!, 1.6) / 2, t.Punkte[0][0], t.Punkte[0][1] + SchemaRaster.TextBreite(t.Text!, 1.6) / 2 })).ToList();
        for (int i = 0; i < rahmen.Count; i++)
            for (int j = i + 1; j < rahmen.Count; j++)
                Assert.False(GroessenTexte.Schneidet(rahmen[i].r, rahmen[j].r), $"{rahmen[i].t.Text} / {rahmen[j].t.Text}");
    }

    /// <summary>Ohne Endgeräte, Abläufe, Schächte und Leitungen im Erdreich bleibt das Layout wie in B8c: zwei Steigknoten auf y0 + 20,
    /// keine Fläche unter dem Band, Rahmen unten an der Unterkante von Band 0 (−1.2), keine Anschlusslinien.</summary>
    [Fact]
    public void OhneApparateUndSchaechte_LayoutUnveraendert()
    {
        var p = Projekt.Anlegen(Kat, null, "Ohne Apparate (fiktiv)", "T", "pc");
        A(p, "element_anlegen", Par(("klasse", "Gebaeude"), ("kennzeichen", "GEB")));
        A(p, "element_anlegen", Par(("klasse", "Geschoss"), ("kennzeichen", "UG"), ("teilVon", "GEB"), ("eigenschaften", new JsonObject { ["kote"] = -2700, ["hoehe"] = 2700 })));
        A(p, "element_anlegen", Par(("klasse", "Geschoss"), ("kennzeichen", "EG"), ("teilVon", "GEB"), ("eigenschaften", new JsonObject { ["kote"] = 0, ["hoehe"] = 2900 })));
        A(p, "element_anlegen", Par(("klasse", "System"), ("kennzeichen", "KW"), ("eigenschaften", new JsonObject { ["systemtyp"] = "Trinkwasser kalt" })));
        A(p, "element_anlegen", Par(("klasse", "Strang"), ("kennzeichen", "KW-1"), ("teilVon", "KW"), ("eigenschaften", new JsonObject { ["art"] = "steigstrang" })));
        Punkt(p, "TStueck", "T-U", [1000, 0, -2400], teilVon: "KW-1");
        Punkt(p, "TStueck", "T-E", [1000, 0, 300], teilVon: "KW-1");
        Rohr(p, "R-1", "KW-1", 20, "T-U.b", "T-E.a", [1000, 0, -2400], [1000, 0, 300]);
        A(p, "element_anlegen", Par(("klasse", "Ansicht"), ("kennzeichen", "AN-O"), ("eigenschaften", new JsonObject { ["art"] = "schema" })));
        var s = Plan(p, "AN-O");
        Assert.Equal([(40.0, 20.0), (40.0, 70.0)], new[] { Lage(s, "T-U"), Lage(s, "T-E") });
        Assert.Null(s.Untergrund);
        Assert.All(s.Graph.Knoten, k => Assert.True(k.Unterkante is null && k.Schacht is null && k.Ablaufband is null && !k.Untergrund));
        Assert.All(s.Graph.Linien, l => Assert.True(l.AnschlussY is null && !l.Untergrund));
        Assert.Equal(-1.2, SchemaGrafik.Rahmen(s).Y0, 6);
        Assert.Null(s.AlsJson()["untergrund"]);
    }

    // ------------------------------------------------------------------ JSON

    [Fact]
    public void Json_SchachtAblaufUntergrundUnterkante()
    {
        var p = MfhMitGrundleitung();
        var o = Plan(p, Abwasser).AlsJson();
        JsonNode K(string kz) => o["knoten"]!.AsArray().First(x => x!["kennzeichen"]!.GetValue<string>() == kz)!;
        var ks = K("KS-1")["schacht"]!;
        Assert.Equal((300.0, 1.2, -13.2, 8.0, -11.2), (ks["x"]!.GetValue<double>(), ks["oben"]!.GetValue<double>(), ks["unten"]!.GetValue<double>(), ks["breite"]!.GetValue<double>(), ks["sohle"]!.GetValue<double>()));
        Assert.Equal("schacht", K("KS-1")["symbol"]!.GetValue<string>());
        Assert.Equal(0, K("BA-U1")["ablauf"]!.GetValue<int>());
        Assert.True(K("TS-G-2")["untergrund"]!.GetValue<bool>());
        Assert.Equal(73.0, K("WC-E1")["unterkante"]!.GetValue<double>());
        var l = o["linien"]!.AsArray().First(x => x!["kennzeichen"]!.AsArray().Any(k => k!.GetValue<string>() == "SW-G-3"))!;
        Assert.Equal((-4.2, 100.0), (l["untergrund"]!["y"]!.GetValue<double>(), l["untergrund"]!["erdreichanteil"]!.GetValue<double>()));
        var ug = o["untergrund"]!;
        Assert.Equal((-1.2, 20.0), (ug["y"]!.GetValue<double>(), ug["hoehe"]!.GetValue<double>()));
        Assert.Equal(["KS-1", "KS-2"], ug["schaechte"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray());
        Assert.Equal(3.0, o["raster"]!["bodenabstand"]!.GetValue<double>());
    }

    // ------------------------------------------------------------------ Bilder (Sichtprüfung)

    [Fact]
    public void Bild_MfhAbwasserUndWasser_AlsPlanPdf()
    {
        var p = MfhMitGrundleitung();
        Ok(p.Ausfuehren("planblatt_anlegen", Par(("nummer", "B8d-1"), ("titel", "Schema Abwasser mit Grundleitungen (fiktiv)"), ("format", "A2"))));
        Ok(p.Ausfuehren("ansicht_platzieren", Par(("planblatt", "PB-1"), ("ansicht", Abwasser))));
        Ok(p.Ausfuehren("planblatt_anlegen", Par(("nummer", "B8d-2"), ("titel", "Schema Wasser (fiktiv)"), ("format", "A2"))));
        Ok(p.Ausfuehren("ansicht_platzieren", Par(("planblatt", "PB-2"), ("ansicht", Wasser))));
        foreach (var (blatt, name) in new[] { ("PB-1", "b8d_schema_abwasser"), ("PB-2", "b8d_schema_wasser") })
        {
            var pdf = Planausgabe.Pdf(p, blatt);
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
            if (Environment.GetEnvironmentVariable("HLKS_SCHEMA_BILD") is { Length: > 0 } ordner)   // Sichtprüfung von Hand
                File.WriteAllBytes(Path.Combine(ordner, name + ".pdf"), pdf);
        }
        foreach (var h in Plan(p, Abwasser).Hinweise) aus.WriteLine("Abwasser: " + h);
        foreach (var h in Plan(p, Wasser).Hinweise) aus.WriteLine("Wasser: " + h);
    }
}
