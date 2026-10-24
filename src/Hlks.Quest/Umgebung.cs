using Hlks.Xr;
using Hlks.Xr.Sitzung;
using StereoKit;
using Vec3 = StereoKit.Vec3;
using XVec3 = Hlks.Xr.Vec3;

namespace Hlks.Quest;

/// <summary>
/// Quest App V1a: die helle Umgebung des virtuellen Sitzungsraums im Stil von visionOS (Christian 24.10.2026: «auch der Sitzungsraum soll im Stil von visionOS erscheinen
/// (ruhige, helle Umgebung)»; Apple HIG «Immersive experiences», «Creating an environment», abgerufen 24.10.2026):
///   - weit statt eng (kein Kasten): eine Kuppel von 40 m mit sanftem Verlauf vom hellen Horizont zu einem leicht bläulichen Zenit, ohne Muster und ohne Bewegung;
///   - immer eine Bodenfläche («Always provide a ground plane mesh»): eine Scheibe von 40 m, in der Mitte etwas dunkler, zum Horizont in die Kuppel übergehend;
///   - ein heller Tisch als ruhiger Mittelpunkt, mit weichem Kontaktschatten am Boden;
///   - Licht in die Farben der Netze gebacken (Ecken-Farben, unbeleuchtetes Material), Formen statt eines flachen 360°-Bilds.
/// Kosten (gemessen bzw. gezählt): Kuppel 32 × 12 Felder (768 Dreiecke), Boden 48 × 6 Ringe (528 Dreiecke), Schatten 48 Dreiecke, Tisch (abgerundeter Quader und
/// Zylinder); 5 Zeichenaufrufe, zusammen gut 2 000 Dreiecke. Die frühere dunkle Fassung bleibt als Variante «umgebung» = «tron» zum Vergleich (Sitzungsraum.cs).
/// </summary>
sealed class HelleUmgebung
{
    /// <summary>Farben (sRGB): Horizont = Löschfarbe (Hintergrund.Hell), Zenit, Boden in der Mitte, Tisch, Fuss.</summary>
    public static readonly Color Horizont = new(Hintergrund.Hell.R, Hintergrund.Hell.G, Hintergrund.Hell.B, 1), Zenit = new(0.70f, 0.78f, 0.88f, 1),
        BodenMitte = new(0.74f, 0.75f, 0.77f, 1), TischPlatte = new(0.95f, 0.95f, 0.96f, 1), TischFuss = new(0.80f, 0.81f, 0.83f, 1);
    /// <summary>Radien (m): unter der fernen Schnittebene des Renderers (StereoKit: 50 m) – Befund Prüfbild 24.10.2026: mit 60 m schnitt sie die Kuppel ab.</summary>
    public const float KuppelRadius = 40, BodenRadius = 40;

    readonly Mesh kuppel, boden, schatten;
    readonly Material unlit, durch, platteM, fussM;
    readonly Mesh platte = Mesh.GenerateRoundedCube(new Vec3((float)XrSitzungLogik.TischBreite, 0.03f, (float)XrSitzungLogik.TischTiefe), 0.012f);
    readonly Mesh fuss = Mesh.GenerateCylinder(0.16f, (float)XrSitzungLogik.TischHoehe - 0.03f, Vec3.Up, 32);
    public int Dreiecke { get; }

    static Color32 C(Color c, float a = 1) { var l = c; return new Color32((byte)(l.r * 255), (byte)(l.g * 255), (byte)(l.b * 255), (byte)(a * 255)); }
    static Color Mix(Color a, Color b, float t) => new(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, 1);

    public HelleUmgebung()
    {
        unlit = Material.Unlit.Copy();
        unlit.FaceCull = Cull.None;
        unlit.QueueOffset = -20;
        durch = Material.Unlit.Copy();
        durch.Transparency = Transparency.Blend;
        durch.DepthWrite = false;
        durch.FaceCull = Cull.None;
        platteM = Material.Default.Copy();
        platteM[MatParamName.ColorTint] = TischPlatte;
        fussM = Material.Default.Copy();
        fussM[MatParamName.ColorTint] = TischFuss;
        // Kuppel: Breiten von −8° (unter dem Horizont, verdeckt den Rand des Bodens) bis zum Zenit
        const int seg = 32, ringe = 12;
        var v = new List<Vertex>();
        var i = new List<uint>();
        for (int r = 0; r <= ringe; r++)
        {
            var lat = (-8 + 98.0 * r / ringe) * Math.PI / 180;
            var t = (float)Math.Pow(Math.Max(0, Math.Sin(lat)), 0.7);
            var farbe = C(Mix(Horizont, Zenit, t));
            for (int s = 0; s <= seg; s++)
            {
                var lon = 2 * Math.PI * s / seg;
                var p = new Vec3((float)(Math.Cos(lat) * Math.Cos(lon)), (float)Math.Sin(lat), (float)(Math.Cos(lat) * Math.Sin(lon))) * KuppelRadius;
                v.Add(new Vertex(p, -p.Normalized, Vec2.Zero, farbe));
            }
        }
        for (int r = 0; r < ringe; r++)
            for (int s = 0; s < seg; s++)
            {
                uint a = (uint)(r * (seg + 1) + s), b = a + 1, c = a + seg + 1, d = c + 1;
                i.AddRange([a, c, b, b, c, d]);
            }
        kuppel = new Mesh();
        kuppel.SetData([.. v], [.. i]);
        var dreiecke = i.Count / 3;
        // Boden: Ringe von der Mitte zum Rand, Farbe zum Horizont
        float[] radien = [0, 1.5f, 4, 9, 18, 30, BodenRadius];
        v.Clear(); i.Clear();
        const int bseg = 48;
        for (int r = 0; r < radien.Length; r++)
        {
            var t = Math.Clamp(radien[r] / 30f, 0, 1);
            var farbe = C(Mix(BodenMitte, Horizont, (float)Math.Pow(t, 0.8)));
            for (int s = 0; s < bseg; s++)
            {
                var lon = 2 * Math.PI * s / bseg;
                v.Add(new Vertex(new Vec3((float)Math.Cos(lon) * radien[r], 0, (float)Math.Sin(lon) * radien[r]), Vec3.Up, Vec2.Zero, farbe));
            }
        }
        for (int r = 0; r + 1 < radien.Length; r++)
            for (int s = 0; s < bseg; s++)
            {
                uint a = (uint)(r * bseg + s), b = (uint)(r * bseg + (s + 1) % bseg), c = (uint)((r + 1) * bseg + s), d = (uint)((r + 1) * bseg + (s + 1) % bseg);
                i.AddRange([a, c, b, b, c, d]);
            }
        boden = new Mesh();
        boden.SetData([.. v], [.. i]);
        dreiecke += i.Count / 3;
        // Kontaktschatten unter dem Tisch: Mitte dunkel durchscheinend, Rand durchsichtig
        v.Clear(); i.Clear();
        v.Add(new Vertex(Vec3.Zero, Vec3.Up, Vec2.Zero, new Color32(40, 44, 52, 70)));
        for (int s = 0; s < bseg; s++)
        {
            var lon = 2 * Math.PI * s / bseg;
            v.Add(new Vertex(new Vec3((float)Math.Cos(lon) * 1.15f, 0, (float)Math.Sin(lon) * 0.8f), Vec3.Up, Vec2.Zero, new Color32(40, 44, 52, 0)));
            i.AddRange([0u, (uint)(1 + s), (uint)(1 + (s + 1) % bseg)]);
        }
        schatten = new Mesh();
        schatten.SetData([.. v], [.. i]);
        Dreiecke = dreiecke + i.Count / 3;
    }

    /// <summary>Zeichnet Kuppel, Boden, Schatten und Tisch im Raum-lokalen System der Lage rl (Tisch in der Mitte, Boden y = 0).</summary>
    public void Zeichnen(SitzungRaumLage rl)
    {
        System.Numerics.Matrix4x4 R(System.Numerics.Matrix4x4 lokal) => lokal * rl.Matrix;
        var tm = XrSitzungLogik.TischMitte;
        kuppel.Draw(unlit, R(System.Numerics.Matrix4x4.CreateTranslation((float)tm.X, 0, (float)tm.Z)));
        boden.Draw(unlit, R(System.Numerics.Matrix4x4.CreateTranslation((float)tm.X, -0.002f, (float)tm.Z)));
        schatten.Draw(durch, R(System.Numerics.Matrix4x4.CreateTranslation((float)tm.X, 0.001f, (float)tm.Z)));
        fuss.Draw(fussM, R(System.Numerics.Matrix4x4.CreateTranslation((float)tm.X, ((float)XrSitzungLogik.TischHoehe - 0.03f) / 2, (float)tm.Z)));
        platte.Draw(platteM, R(System.Numerics.Matrix4x4.CreateTranslation((float)tm.X, (float)XrSitzungLogik.TischHoehe - 0.015f, (float)tm.Z)));
    }
}
