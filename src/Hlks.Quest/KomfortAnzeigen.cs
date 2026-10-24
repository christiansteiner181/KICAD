// Quest App P5a: Geisterhand und Tunnelblick (Komfort). Die schwebenden Kacheln aus P5a (früher in dieser Datei, SchwebendeApps.cs) ersetzt seit V1a das Glas-Fenster (GlasApps.cs).
using System.Numerics;
using Hlks.Xr;
using Hlks.Xr.Bedienung;
using StereoKit;
using Vec3 = StereoKit.Vec3;
using XVec3 = Hlks.Xr.Vec3;

namespace Hlks.Quest;

/// <summary>
/// Geisterhand (P5a, Regeln in Hlks.Xr.Bedienung.Geisterhand): eine halbdurchsichtige Hand aus Kugeln (Gelenke) und Linien (Knochen) führt eine Geste vor, aus der
/// Form der eigenen Hand im Augenblick des Zögerns.
/// </summary>
sealed class GeisterHand
{
    readonly Material material;
    (GeisterGeste Geste, double Start, IReadOnlyDictionary<string, XVec3> Form, TabletPose Ziel)? aktuell;

    public int Gezeigt { get; private set; }
    public GeisterGeste? Letzte { get; private set; }

    public GeisterHand()
    {
        material = Material.Unlit.Copy();
        material.Transparency = Transparency.Blend;
        material.DepthWrite = false;
        material.QueueOffset = 20;
    }

    public void Starten(GeisterGeste geste, double t, IReadOnlyDictionary<string, XVec3> form, TabletPose ziel)
    {
        aktuell = (geste, t, new Dictionary<string, XVec3>(form, StringComparer.Ordinal), ziel);
        Gezeigt++;
        Letzte = geste;
    }

    public void Zeichnen(double t)
    {
        if (aktuell is not { } a) return;
        var bild = Geisterhand.Bild(a.Geste, t - a.Start, a.Form, a.Ziel);
        if (bild is null) { if (t - a.Start > Geisterhand.DauerS) aktuell = null; return; }
        var alpha = (float)bild.Deckkraft;
        static Vec3 S(XVec3 v) => new((float)v.X, (float)v.Y, (float)v.Z);
        foreach (var p in bild.Gelenke.Values) Mesh.Sphere.Draw(material, Matrix.TS(S(p), 0.011f), new Color(0.75f, 0.95f, 1f, alpha));
        var c = new Color32(190, 240, 255, (byte)(255 * alpha));
        foreach (var (x, y) in Geisterhand.Knochen)
            if (bild.Gelenke.TryGetValue(x, out var pa) && bild.Gelenke.TryGetValue(y, out var pb)) Lines.Add(S(pa), S(pb), c, 0.006f);
    }
}

/// <summary>
/// Tunnelblick (P5a, Regeln in Hlks.Xr.Bedienung.Tunnelblick): ein Ring um die Blickachse vor den Augen – innen frei, weicher Rand, aussen dunkel. Die Ecken liegen
/// im Raum des Kopfes auf einer Kugel von 0.3 m; Deckkraft in den Vertexfarben (schwarz).
/// </summary>
sealed class TunnelAnzeige
{
    const int Teile = 48;
    const float Radius = 0.3f, AussenRandGrad = 115;
    readonly Mesh netz = new();
    readonly Material material;
    readonly Vertex[] ecken = new Vertex[Teile * 3];
    public int Gezeichnet { get; private set; }

    public TunnelAnzeige()
    {
        material = Material.Unlit.Copy();
        material.Transparency = Transparency.Blend;
        material.DepthTest = DepthTest.Always;
        material.DepthWrite = false;
        material.FaceCull = Cull.None;
        material.QueueOffset = 120;
        var ind = new List<uint>();
        for (int i = 0; i < Teile; i++)
        {
            uint a = (uint)i, b = (uint)((i + 1) % Teile);
            for (int ring = 0; ring < 2; ring++)
            {
                uint o = (uint)(ring * Teile), u = (uint)((ring + 1) * Teile);
                ind.AddRange([o + a, u + a, u + b, o + a, u + b, o + b]);
            }
        }
        Ecken(60, 78, 0);
        netz.SetData(ecken, [.. ind]);
    }

    void Ecken(double innenGrad, double aussenGrad, double deckkraft)
    {
        var alpha = (byte)Math.Clamp(255 * deckkraft, 0, 255);
        var winkel = new[] { innenGrad, aussenGrad, AussenRandGrad };
        for (int ring = 0; ring < 3; ring++)
        {
            var th = winkel[ring] * Math.PI / 180;
            var c = new Color32(0, 0, 0, ring == 0 ? (byte)0 : alpha);
            for (int i = 0; i < Teile; i++)
            {
                var ph = 2 * Math.PI * i / Teile;
                var p = new Vec3((float)(Math.Sin(th) * Math.Cos(ph)), (float)(Math.Sin(th) * Math.Sin(ph)), -(float)Math.Cos(th)) * Radius;
                ecken[ring * Teile + i] = new Vertex(p, -p.Normalized, Vec2.Zero, c);
            }
        }
    }

    public void Zeichnen(Pose kopf, TunnelForm? form)
    {
        if (form is not { } f) return;
        Ecken(f.InnenGrad, f.AussenGrad, f.Deckkraft);
        netz.SetVerts(ecken);
        netz.Draw(material, Matrix.TR(kopf.position, kopf.orientation));
        Gezeichnet++;
    }
}
