using System.Buffers.Binary;
using System.Text;
using System.Text.Json.Nodes;

namespace Hlks.Kern.Tests;

/// <summary>Quest App (Modul XR, P2): GLB-Schreiber, Umrechnung der Koordinaten und Zerlegung der Körperteile (XrNetzbau). Geprüft wird jede
/// Datei mit dem unabhängigen Leser GlbPruefer (Kopf, Chunks, Ausrichtung, min/max, Indizes, Normalen).</summary>
public class XrGlbTests
{
    static readonly double[] Null = [0, 0, 0];

    static GlbNetz Dreieck(GlbMaterial m) => new()
    {
        Name = "t", Material = m, Extras = new JsonObject { ["a"] = 1 },
        Positionen = [0, 0, 0, 1, 0, 0, 0, 1, 0], Normalen = [0, 0, 1, 0, 0, 1, 0, 0, 1], Indizes = [0, 1, 2],
    };

    // ------------------------------------------------------------------ Schreiber

    [Fact]
    public void Glb_EinDreieck_KopfChunksAusrichtungUndExtras()
    {
        var glb = GlbSchreiber.Schreiben([Dreieck(new GlbMaterial("m", "#808080", 1))], "Szene", new JsonObject { ["x"] = 1 }, new JsonObject { ["y"] = 2 });
        var p = GlbPruefer.Pruefen(glb);
        Assert.Equal("glTF", Encoding.ASCII.GetString(glb, 0, 4));
        Assert.Equal(2u, BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(4)));
        Assert.Equal((uint)glb.Length, BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(8)));
        Assert.Equal("JSON", Encoding.ASCII.GetString(glb, 16, 4));
        Assert.Equal(0, p.JsonLaenge % 4);
        Assert.Equal("BIN\0", Encoding.ASCII.GetString(glb, 20 + p.JsonLaenge + 4, 4));
        // 36 Byte Positionen, 36 Byte Normalen, 6 Byte Indizes (uint16) → 78, aufgefüllt auf 80
        Assert.Equal(80, p.BinLaenge);
        Assert.Equal(80, (int)p.Gltf["buffers"]![0]!["byteLength"]!);
        var views = (JsonArray)p.Gltf["bufferViews"]!;
        Assert.Equal([0, 36, 72], views.Select(v => (int)v!["byteOffset"]!));
        Assert.Equal([36, 36, 6], views.Select(v => (int)v!["byteLength"]!));
        Assert.Equal([0, 0], glb.Skip(glb.Length - 2));   // BIN mit Nullbytes aufgefüllt
        Assert.Equal(5123, p.IndexTyp(0));
        Assert.Equal([0.0, 0, 0, 1, 1, 0], ((JsonArray)p.Gltf["accessors"]![0]!["min"]!).Concat((JsonArray)p.Gltf["accessors"]![0]!["max"]!).Select(x => (double)x!));
        var mat = p.Material(0);
        Assert.Equal("OPAQUE", (string?)mat["alphaMode"]);
        Assert.True((bool)mat["doubleSided"]!);
        Assert.Equal(0, (double)mat["pbrMetallicRoughness"]!["metallicFactor"]!);
        Assert.Equal(0.21586, (double)mat["pbrMetallicRoughness"]!["baseColorFactor"]![0]!, 5);   // #80 sRGB → linear
        Assert.Equal(1, (int)p.Meshes[0]!["extras"]!["a"]!);
        Assert.Equal(1, (int)p.Gltf["scenes"]![0]!["extras"]!["x"]!);
        Assert.Equal(2, (int)p.Gltf["asset"]!["extras"]!["y"]!);
        Assert.Equal("2.0", (string?)p.Gltf["asset"]!["version"]);
        Assert.Single((JsonArray)p.Gltf["scenes"]![0]!["nodes"]!);
    }

    [Fact]
    public void Glb_Durchscheinend_Blend_MitDeckkraftImAlpha()
    {
        var p = GlbPruefer.Pruefen(GlbSchreiber.Schreiben([Dreieck(new GlbMaterial("glas", "#a9d4ee", 0.35))], "s"));
        var mat = p.Material(0);
        Assert.Equal("BLEND", (string?)mat["alphaMode"]);
        Assert.Equal(0.35, (double)mat["pbrMetallicRoughness"]!["baseColorFactor"]![3]!, 6);
    }

    [Fact]
    public void Glb_MehrAls65535Ecken_Uint32Indizes()
    {
        int n = 70_002;
        var pos = new float[n * 3]; var nor = new float[n * 3]; var idx = new uint[n];
        for (int i = 0; i < n; i++) { pos[i * 3] = i % 3; pos[i * 3 + 1] = (i % 3 == 2) ? 1 : 0; pos[i * 3 + 2] = i / 3 * 0.001f; nor[i * 3 + 2] = 1; idx[i] = (uint)i; }
        var netz = new GlbNetz { Name = "gross", Positionen = pos, Normalen = nor, Indizes = idx, Material = new GlbMaterial("m", "#777777", 1) };
        var p = GlbPruefer.Pruefen(GlbSchreiber.Schreiben([netz], "s"));
        Assert.Equal(5125, p.IndexTyp(0));
        Assert.Equal(n / 3, p.Dreiecke(0));
        Assert.Equal(n - 1, p.Indizes(0).Max());
    }

    [Fact]
    public void Glb_OhneNetz_NurJsonChunk_SzeneOhneKnoten()
    {
        var leer = new GlbNetz { Name = "leer", Positionen = [], Normalen = [], Indizes = [], Material = new GlbMaterial("m", "#777777", 1) };
        var glb = GlbSchreiber.Schreiben([leer], "leer", new JsonObject { ["kicad"] = new JsonObject() });
        var p = GlbPruefer.Pruefen(glb);
        Assert.Equal(0, p.BinLaenge);
        Assert.Equal(20 + p.JsonLaenge, glb.Length);
        Assert.Null(p.Gltf["buffers"]);
        Assert.Null(p.Gltf["scenes"]![0]!["nodes"]);   // glTF: nodes mindestens ein Eintrag oder weglassen
    }

    [Fact]
    public void Glb_IndexAusserhalb_Wirft()
    {
        var n = new GlbNetz { Name = "x", Positionen = [0, 0, 0, 1, 0, 0, 0, 1, 0], Normalen = [0, 0, 1, 0, 0, 1, 0, 0, 1], Indizes = [0, 1, 3], Material = new GlbMaterial("m", "#777777", 1) };
        Assert.Throws<ArgumentException>(() => GlbSchreiber.Schreiben([n], "s"));
    }

    [Fact]
    public void Linear_SrgbWieThree_UngueltigGrau()
    {
        Assert.Equal([0.0, 0, 0], GlbSchreiber.Linear("#000000"));
        Assert.Equal([1.0, 1, 1], GlbSchreiber.Linear("#FFFFFF"));
        Assert.Equal(0.215861, GlbSchreiber.Linear("#808080")[0], 6);
        Assert.Equal(10 / 255.0 / 12.92, GlbSchreiber.Linear("#0a0a0a")[0], 12);   // unter 0.04045 linear
        Assert.Equal(GlbSchreiber.Linear("#808080"), GlbSchreiber.Linear("kein"));
        Assert.Equal(GlbSchreiber.Linear("#ffaa00"), GlbSchreiber.Linear("#fa0"));
    }

    // ------------------------------------------------------------------ Koordinaten

    [Fact]
    public void Koordinaten_BekanntePunkte_UndUmkehrung()
    {
        Assert.Equal((1f, 3f, -2f), XrKoordinaten.Punkt(1000, 2000, 3000, Null));
        Assert.Equal((0.5f, 3f, -1.5f), XrKoordinaten.Punkt(1000, 2000, 3000, [500, 500, 0]));
        Assert.Equal((0f, -2.7f, 0f), XrKoordinaten.Punkt(0, 0, -2700, Null));
        Assert.Equal((-4.5f, 0f, 2.5f), XrKoordinaten.Punkt(2_600_000_500, 1_200_000_000, 0, [2_600_005_000, 1_200_002_500, 0]));
        Assert.Equal([1000.0, 2000, 3000], XrKoordinaten.Zurueck(1, 3, -2, Null));
        Assert.Equal([2_600_000_500.0, 1_200_000_000, 0], XrKoordinaten.Zurueck(-4.5, 0, 2.5, [2_600_005_000, 1_200_002_500, 0]));
        // Richtungen: Ost bleibt x, Nord wird −z, oben wird y; normiert
        Assert.Equal((1f, 0f, 0f), XrKoordinaten.Richtung(5, 0, 0));
        Assert.Equal((0f, 0f, -1f), XrKoordinaten.Richtung(0, 3, 0));
        Assert.Equal((0f, 1f, 0f), XrKoordinaten.Richtung(0, 0, 0.2));
        Assert.Equal((0f, 1f, 0f), XrKoordinaten.Richtung(0, 0, 0));   // Nullvektor
    }

    [Fact]
    public void Koordinaten_SindEineDrehung_UmlaufsinnBleibt()
    {
        // Bild von x × Bild von y = Bild von z (Determinante +1): Dreiecke im Gegenuhrzeigersinn bleiben es
        var (ax, ay, az) = XrKoordinaten.Richtung(1, 0, 0);
        var (bx, by, bz) = XrKoordinaten.Richtung(0, 1, 0);
        var (cx, cy, cz) = XrKoordinaten.Richtung(0, 0, 1);
        Assert.Equal(cx, ay * bz - az * by);
        Assert.Equal(cy, az * bx - ax * bz);
        Assert.Equal(cz, ax * by - ay * bx);
    }

    [Fact]
    public void Bezugspunkt_NahNull_FernMitteAufMeter()
    {
        Assert.Equal([0.0, 0, 0], XrKoordinaten.Bezugspunkt([[-4791, -78363, -7130, 25063, 500, 12995]]));   // Test_praxis: nah
        Assert.Equal([0.0, 0, 0], XrKoordinaten.Bezugspunkt([]));
        Assert.Equal([0.0, 0, 0], XrKoordinaten.Bezugspunkt([[-4_999_000, 0, 0, 4_999_000, 1, 1]]));
        // Landeskoordinaten in mm (LV95-ähnlich): Mitte auf ganze Meter, Höhe bleibt 0
        var b = XrKoordinaten.Bezugspunkt([[2_600_000_123, 1_200_000_000, 0, 2_600_040_000, 1_200_030_400, 9000], [2_600_001_000, 1_200_001_000, -3000, 2_600_002_000, 1_200_002_000, 0]]);
        Assert.Equal([2_600_020_000.0, 1_200_015_000, 0], b);
        Assert.Equal(0, b[0] % 1000);
    }

    // ------------------------------------------------------------------ Zerlegung

    /// <summary>Puffer als GLB schreiben und prüfen (Netz 0).</summary>
    static GlbPruefer Pruefen(XrPuffer p) =>
        GlbPruefer.Pruefen(GlbSchreiber.Schreiben([p.Netz("n", new GlbMaterial("m", "#777777", 1), null)], "s"));

    /// <summary>Jede Dreiecksnormale (aus den Ecken) zeigt in Richtung ihrer Eckennormalen.</summary>
    static void NormalenPassen(GlbPruefer g, double mindestens = 0.5)
    {
        var pos = g.Positionen(0); var nor = g.Normalen(0); var idx = g.Indizes(0);
        for (int t = 0; t < idx.Length; t += 3)
        {
            int a = idx[t] * 3, b = idx[t + 1] * 3, c = idx[t + 2] * 3;
            double ux = pos[b] - pos[a], uy = pos[b + 1] - pos[a + 1], uz = pos[b + 2] - pos[a + 2], vx = pos[c] - pos[a], vy = pos[c + 1] - pos[a + 1], vz = pos[c + 2] - pos[a + 2];
            double nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx, l = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            foreach (var e in new[] { a, b, c })
                Assert.True((nx * nor[e] + ny * nor[e + 1] + nz * nor[e + 2]) / l >= mindestens, $"Dreieck {t / 3}: Normale passt nicht zum Umlaufsinn");
        }
    }

    [Fact]
    public void Quader_24Ecken12Dreiecke_Gedreht_NachAussen()
    {
        var p = new XrPuffer(Null);
        Assert.True(XrNetzbau.Teil(new Koerperteil { Art = "quader", Mitte = [1000, 2000, 1500], Masse = [2000, 400, 3000], Drehung = 30 }, p));
        Assert.Equal(24, p.Ecken);
        Assert.Equal(12, p.Dreiecke);
        var g = Pruefen(p);
        NormalenPassen(g, 0.999);
        Assert.Equal(2.0 * 0.4 * 3.0, g.Volumen(0, 0, 12), 5);   // positiv: Normalen nach aussen
        // Ecke (+1000, +200) lokal gedreht um 30°: x = 1000 + 1000·cos30 − 200·sin30, y = 2000 + 1000·sin30 + 200·cos30
        double ex = 1000 + 1000 * Math.Cos(Math.PI / 6) - 200 * Math.Sin(Math.PI / 6), ey = 2000 + 1000 * Math.Sin(Math.PI / 6) + 200 * Math.Cos(Math.PI / 6);
        var pos = g.Positionen(0);
        Assert.Contains(Enumerable.Range(0, pos.Length / 3), i => Math.Abs(pos[i * 3] - ex / 1000) < 1e-6 && Math.Abs(pos[i * 3 + 2] + ey / 1000) < 1e-6 && Math.Abs(pos[i * 3 + 1] - 3.0) < 1e-6);
        // Höhe: z 0…3000 mm → glTF y 0…3 m
        Assert.Equal(0, ((JsonArray)g.Gltf["accessors"]![0]!["min"]!)[1]!.GetValue<double>(), 6);
        Assert.Equal(3, ((JsonArray)g.Gltf["accessors"]![0]!["max"]!)[1]!.GetValue<double>(), 6);
    }

    [Fact]
    public void Prisma_MitLoch_Volumen_NachAussen_LochKeinDeckel()
    {
        // Quadrat 4 × 4 m mit Loch 1 × 1 m (Löcher in beliebigem Drehsinn, Ring mit Schlusspunkt), 250 mm hoch
        var t = new Koerperteil
        {
            Art = "prisma", Unten = 2750, Oben = 3000,
            Grundriss = [[0, 0], [0, 4000], [4000, 4000], [4000, 0], [0, 0]],
            Loecher = [[[1000, 1000], [2000, 1000], [2000, 2000], [1000, 2000]]],
        };
        var p = new XrPuffer(Null);
        Assert.True(XrNetzbau.Teil(t, p));
        var g = Pruefen(p);
        NormalenPassen(g, 0.999);
        Assert.Equal(15 * 0.25, g.Volumen(0, 0, p.Dreiecke), 6);
        // Deckel: 8 Ecken, Ring mit Loch → 8 Dreiecke je Deckel; Seiten: 8 Kanten × 2
        Assert.Equal(8 + 8 + 16, p.Dreiecke);
        // kein Punkt eines Deckels liegt im Loch (1.5, −1.5)
        var pos = g.Positionen(0); var idx = g.Indizes(0);
        for (int i = 0; i < idx.Length; i += 3)
        {
            double cx = (pos[idx[i] * 3] + pos[idx[i + 1] * 3] + pos[idx[i + 2] * 3]) / 3, cz = (pos[idx[i] * 3 + 2] + pos[idx[i + 1] * 3 + 2] + pos[idx[i + 2] * 3 + 2]) / 3;
            Assert.False(cx > 1.001 && cx < 1.999 && cz < -1.001 && cz > -1.999, "Dreieck im Loch");
        }
    }

    [Fact]
    public void Zylinder_Kugel_Dreieckszahl_Volumen_GlatteNormalen()
    {
        var p = new XrPuffer(Null);
        Assert.True(XrNetzbau.Teil(new Koerperteil { Art = "zylinder", Von = [0, 0, 0], Nach = [0, 1000, 500], Radius = 50 }, p));
        int n = XrNetzbau.Rund;
        Assert.Equal(2 * n + 2 * (n - 2), p.Dreiecke);
        var g = Pruefen(p);
        NormalenPassen(g);
        double laenge = Math.Sqrt(1000 * 1000 + 500 * 500) / 1000, r = 0.05;
        double vieleck = n / 2.0 * r * r * Math.Sin(2 * Math.PI / n);   // einbeschriebenes Vieleck
        Assert.Equal(vieleck * laenge, g.Volumen(0, 0, p.Dreiecke), 6);

        var k = new XrPuffer(Null);
        Assert.True(XrNetzbau.Teil(new Koerperteil { Art = "kugel", Mitte = [100, 200, 300], Radius = 1000 }, k));
        Assert.Equal(2 * n + 2 * n * (XrNetzbau.KugelBreiten - 2), k.Dreiecke);
        Assert.Equal(2 + (XrNetzbau.KugelBreiten - 1) * n, k.Ecken);
        var gk = Pruefen(k);
        NormalenPassen(gk);
        double v = gk.Volumen(0, 0, k.Dreiecke);
        Assert.InRange(v, 0.95 * 4 / 3.0 * Math.PI, 4 / 3.0 * Math.PI);   // einbeschrieben: etwas kleiner als die Kugel
        // glatte Normalen: Normale = Richtung von der Mitte
        var pos = gk.Positionen(0); var nor = gk.Normalen(0);
        for (int i = 0; i < pos.Length; i += 3)
        {
            double dx = pos[i] - 0.1, dy = pos[i + 1] - 0.3, dz = pos[i + 2] + 0.2;
            Assert.Equal(1, dx * nor[i] + dy * nor[i + 1] + dz * nor[i + 2], 5);
        }
    }

    static Koerperteil Wuerfel(double s = 1000) => new()
    {
        Art = "koerper",
        Ecken = [[0, 0, 0], [s, 0, 0], [s, s, 0], [0, s, 0], [0, 0, s], [s, 0, s], [s, s, s], [0, s, s]],
        Dreiecke = [[0, 3, 2], [0, 2, 1], [4, 5, 6], [4, 6, 7], [0, 1, 5], [0, 5, 4], [1, 2, 6], [1, 6, 5], [2, 3, 7], [2, 7, 6], [3, 0, 4], [3, 4, 7]],
    };

    [Fact]
    public void Koerper_Knick_WuerfelGetrennt_FlacherKnickGeteilt()
    {
        var p = new XrPuffer(Null);
        Assert.True(XrNetzbau.Teil(Wuerfel(), p));
        Assert.Equal(12, p.Dreiecke);
        Assert.Equal(24, p.Ecken);   // 90° > 20°: je Ecke drei Normalen
        var g = Pruefen(p);
        NormalenPassen(g, 0.999);
        Assert.Equal(1.0, g.Volumen(0, 0, 12), 9);

        Assert.Equal(0, XrNetzbau.Netz(new double[] { 0, 0, 0, 1, 0, 0, 0, 1, 0 }, [], new XrPuffer(Null), false));
        // flache Pyramide über einem Quadrat 1 × 1 m, Flächen um «grad» geneigt: Nachbarflächen knicken um arccos(cos² grad),
        // gegenüberliegende um 2 · grad. 8°: 11.3° bzw. 16° (unter 20°) → je Punkt eine Ecke (5); 30°: 41° bzw. 60° → je Dreieck eigene (12)
        var punkte = new double[] { 0, 0, 0, 1000, 0, 0, 1000, 1000, 0, 0, 1000, 0, 500, 500, 0 };
        foreach (var (grad, ecken) in new[] { (8.0, 5), (30.0, 12) })
        {
            punkte[14] = 500 * Math.Tan(grad * Math.PI / 180);
            var r = new XrPuffer(Null);
            Assert.Equal(4, XrNetzbau.Netz(punkte, [0, 1, 4, 1, 2, 4, 2, 3, 4, 3, 0, 4], r, false));
            Assert.Equal(ecken, r.Ecken);
            NormalenPassen(Pruefen(r));
        }
    }

    [Fact]
    public void Orientieren_RichtetUndWendet_VolumenPositiv()
    {
        var w = Wuerfel();
        var punkte = w.Ecken!.SelectMany(x => x).ToArray();
        // zwei Dreiecke falsch herum, dann der ganze Würfel innen nach aussen
        var tri = w.Dreiecke!.SelectMany(x => x).ToArray();
        (tri[1], tri[2]) = (tri[2], tri[1]);
        (tri[16], tri[17]) = (tri[17], tri[16]);
        var p = new XrPuffer(Null);
        Assert.Equal(12, XrNetzbau.Netz(punkte, tri, p, true));
        Assert.Equal(1.0, Pruefen(p).Volumen(0, 0, 12), 9);
        var innen = w.Dreiecke!.SelectMany(x => new[] { x[0], x[2], x[1] }).ToArray();
        var q = new XrPuffer(Null);
        XrNetzbau.Netz(punkte, innen, q, true);
        Assert.Equal(1.0, Pruefen(q).Volumen(0, 0, 12), 9);
        // ohne Orientieren bleibt er innen nach aussen
        var r = new XrPuffer(Null);
        XrNetzbau.Netz(punkte, innen, r, false);
        Assert.Equal(-1.0, Pruefen(r).Volumen(0, 0, 12), 9);
    }

    [Fact]
    public void Netz_EntarteteUndUngueltigeDreieckeEntfallen()
    {
        var w = Wuerfel();
        var punkte = w.Ecken!.SelectMany(x => x).ToArray();
        var tri = w.Dreiecke!.SelectMany(x => x).Concat([0, 0, 1, 0, 1, 99, 0, 1, -1, 0, 1, 1]).ToArray();   // doppelte Ecke, ausserhalb, negativ
        var mitLinie = punkte.Concat(new double[] { 2000, 0, 0 }).ToArray();
        tri = tri.Concat([0, 1, 8]).ToArray();   // drei Punkte auf einer Linie: Fläche 0
        var p = new XrPuffer(Null);
        Assert.Equal(12, XrNetzbau.Netz(mitLinie, tri, p, false));
    }

    [Fact]
    public void Teil_OhneGueltigeAngaben_False_NetzNichtImModell()
    {
        var p = new XrPuffer(Null);
        Assert.False(XrNetzbau.Teil(new Koerperteil { Art = "quader", Mitte = [0, 0, 0], Masse = [0, 1, 1] }, p));
        Assert.False(XrNetzbau.Teil(new Koerperteil { Art = "quader", Mitte = [0, 0, double.NaN], Masse = [1, 1, 1] }, p));
        Assert.False(XrNetzbau.Teil(new Koerperteil { Art = "prisma", Grundriss = [[0, 0], [1, 0], [2, 0]], Unten = 0, Oben = 1 }, p));
        Assert.False(XrNetzbau.Teil(new Koerperteil { Art = "prisma", Grundriss = [[0, 0], [1, 0], [1, 1]], Unten = 1, Oben = 1 }, p));
        Assert.False(XrNetzbau.Teil(new Koerperteil { Art = "zylinder", Von = [0, 0, 0], Nach = [0, 0, 0], Radius = 5 }, p));
        Assert.False(XrNetzbau.Teil(new Koerperteil { Art = "kugel", Mitte = [0, 0, 0], Radius = 0 }, p));
        Assert.False(XrNetzbau.Teil(new Koerperteil { Art = "koerper", Ecken = [[0, 0], [1, 0, 0], [0, 1, 0]], Dreiecke = [[0, 1, 2]] }, p));
        Assert.False(XrNetzbau.Teil(new Koerperteil { Art = "netz", Quelle = "u" }, p));
        Assert.Equal(0, p.Dreiecke);
    }

    [Fact]
    public void Puffer_Zuordnen_SchliesstAnGleichesElementAn()
    {
        var p = new XrPuffer(Null);
        XrNetzbau.Teil(new Koerperteil { Art = "quader", Mitte = [0, 0, 0], Masse = [1, 1, 1] }, p);
        p.Zuordnen("A", "wand", 0);
        XrNetzbau.Teil(new Koerperteil { Art = "quader", Mitte = [5, 0, 0], Masse = [1, 1, 1] }, p);
        p.Zuordnen("A", "wand", 12);
        XrNetzbau.Teil(new Koerperteil { Art = "quader", Mitte = [9, 0, 0], Masse = [1, 1, 1] }, p);
        p.Zuordnen("B", "decke", 24);
        p.Zuordnen("C", "decke", 36);   // nichts dazugekommen
        Assert.Equal([new XrBereich("A", 0, 24, "wand"), new XrBereich("B", 24, 12, "decke")], p.Bereiche);
    }

    [Fact]
    public void Farben_WieKoerper3d()
    {
        Assert.Equal("#cfd3d8", XrFarben.Farbe(new Koerperteil { Material = "wand" }));
        Assert.Equal("#777777", XrFarben.Farbe(new Koerperteil { Material = "formteil" }));   // Alias rohr
        Assert.Equal("#888888", XrFarben.Farbe(new Koerperteil { Material = "unbekannt" }));
        Assert.Equal("#aabbcc", XrFarben.Farbe(new Koerperteil { Material = "wand", Farbe = "#ABC" }));
        Assert.Equal("#cfd3d8", XrFarben.Farbe(new Koerperteil { Material = "wand", Farbe = "#aabbccdd" }));   // three r160 kennt #rrggbbaa nicht
        Assert.Equal(0.28, XrFarben.Deckkraft3d(new Koerperteil { Material = "wand" }));
        Assert.Equal(1, XrFarben.Deckkraft3d(new Koerperteil { Material = "decke" }));
        Assert.Equal(0.15, XrFarben.Deckkraft3d(new Koerperteil { Material = "raum", Deckkraft = 0.15 }));
        Assert.Equal(1, XrFarben.Deckkraft3d(new Koerperteil { Material = "glas", Deckkraft = 7 }));
        Assert.Equal(1, XrFarben.DeckkraftVr("wand", 0.28));
        Assert.Equal(1, XrFarben.DeckkraftVr("decke", 0.6));
        Assert.Equal(0.35, XrFarben.DeckkraftVr("oeffnung", 0.35));
        Assert.Equal(XrFarben.Architektur, XrFarben.Gruppe("oeffnung"));
        Assert.Equal(XrFarben.Hlks, XrFarben.Gruppe("armatur"));
        Assert.Equal(XrFarben.Raum, XrFarben.Gruppe("raum"));
        Assert.Equal(XrFarben.Sonst, XrFarben.Gruppe("grundlage"));
    }
}
