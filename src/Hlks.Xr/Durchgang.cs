// Körper des Kerns und die Regel «Wände nur unter 500 mm durchscheinend» (Nachbearbeitung N22).
// Quelle: wwwroot/js/ansichten/begehen-logik.js, Abschnitt «Durchgang» (formAusTeil, huellboxVon, abstandZurBox, abstandZurFlaeche, abstandZurForm,
// abstandZumKoerper, imDurchgang, durchgangDeckkraft). Die Anwendung auf Materialien (durchgang3d.js) gehört zur App.
//
// Konventionen: Körper kommen vom Kern in Modell-Millimetern (x Ost, y Nord, z oben) und werden hier in Szenenmeter umgerechnet
// (x Ost, y oben, z = -Nord). Gemessen wird gegen den Körper selbst (Grundriss-Umriss und Höhe), nicht gegen die Hüllbox.

namespace Hlks.Xr;

/// <summary>
/// Ein Körperteil, wie ihn /api/koerper liefert (Modell-mm). Je nach <see cref="Art"/> gelten andere Felder:
/// prisma (Grundriss, Loecher, Unten, Oben), quader (Mitte, Masse, Drehung in Grad), zylinder (Von, Nach, Radius), kugel (Mitte, Radius),
/// koerper (Ecken, ein Netz). Punkte sind [x, y] oder [x, y, z].
/// </summary>
public sealed record KoerperTeil
{
    public string Art { get; init; } = "";
    public IReadOnlyList<double[]>? Grundriss { get; init; }
    public IReadOnlyList<IReadOnlyList<double[]>>? Loecher { get; init; }
    public double? Unten { get; init; }
    public double? Oben { get; init; }
    public double[]? Mitte { get; init; }
    public double[]? Masse { get; init; }
    public double? Drehung { get; init; }
    public double[]? Von { get; init; }
    public double[]? Nach { get; init; }
    public double? Radius { get; init; }
    public IReadOnlyList<double[]>? Ecken { get; init; }
    public string? Material { get; init; }
}

/// <summary>Ein Körper des Kerns (/api/koerper): Id, Ebene (wand, decke, leitung, formteil, armatur, apparat, raum …) und Teile.</summary>
public sealed record Koerper(string Id, string? Ebene, IReadOnlyList<KoerperTeil> Teile);

/// <summary>Form eines Körperteils für die Abstandsrechnung, in Szenenmetern; <see cref="Box"/> ist ihre Hüllbox.</summary>
public abstract record Form(Box3 Box);
/// <summary>Prisma und Quader: Umriss (x, z) mit Löchern, Höhe von Y0 bis Y1.</summary>
public sealed record PolyForm(Xz[] Ring, Xz[][] Loecher, double Y0, double Y1, Box3 Box) : Form(Box);
/// <summary>Gerader Kreiszylinder mit ebenen Stirnflächen (Rohre, Kanäle).</summary>
public sealed record ZylForm(Vec3 A, Vec3 B, double R, Box3 Box) : Form(Box);
public sealed record KugelForm(Vec3 C, double R, Box3 Box) : Form(Box);
/// <summary>Netzkörper: nur die Hüllbox der Ecken.</summary>
public sealed record BoxForm(Box3 Box) : Form(Box);

public static class Durchgang
{
    /// <summary>begehen-logik.js DURCHGANG_MM: Abstand zum Bauteil, unter dem es durchscheinend wird (Christian 21.10.2026, ±50 cm). Streng kleiner zählt.</summary>
    public const double DurchgangMm = 500;
    /// <summary>begehen-logik.js DURCHGANG_DECKKRAFT: Deckkraft im Durchgang, wenn das Bauteil sonst undurchsichtig ist.</summary>
    public const double DurchgangDeckkraftStandard = 0.3;
    /// <summary>begehen-logik.js DURCHGANG_EBENEN: Ebenen, die beim Begehen undurchsichtig werden: Wand (auch Stütze, Unterzug), Decke (auch Dach, Boden).</summary>
    public static readonly IReadOnlyList<string> DurchgangEbenen = ["wand", "decke"];

    const double MM = 0.001;

    /// <summary>begehen-logik.js durchgangDeckkraft: die bisherige Deckkraft, wenn sie durchscheinend ist, sonst 0.3.</summary>
    public static double DurchgangDeckkraft(double? original) => Num.Ok(original) && original!.Value < 1 ? original.Value : DurchgangDeckkraftStandard;

    static bool PunktOk(double[]? p) => p is { Length: >= 2 } && Num.Ok(p[0]) && Num.Ok(p[1]);
    static Xz AusModellXz(double[] p) => new(p[0] * MM, -p[1] * MM);
    static double Dritte(double[] p) => p.Length > 2 ? p[2] : 0;

    /// <summary>
    /// begehen-logik.js formAusTeil. Null bei ungültigen Angaben und bei Teilen ohne Mass (Netz-Halter).
    /// </summary>
    public static Form? FormAusTeil(KoerperTeil? t)
    {
        if (t is null) return null;
        switch (t.Art)
        {
            case "prisma":
            {
                var ring = (t.Grundriss ?? []).Where(PunktOk).Select(AusModellXz).ToArray();
                if (ring.Length < 3 || !Num.Ok(t.Unten) || !Num.Ok(t.Oben) || t.Oben!.Value <= t.Unten!.Value) return null;
                var loecher = (t.Loecher ?? []).Select(l => (l ?? []).Where(PunktOk).Select(AusModellXz).ToArray()).Where(l => l.Length >= 3).ToArray();
                double y0 = t.Unten.Value * MM, y1 = t.Oben.Value * MM;
                return new PolyForm(ring, loecher, y0, y1, RingBox(ring, y0, y1));
            }
            case "quader":
            {
                var m = t.Mitte;
                if (m is null || m.Length < 2 || !Num.Ok(m[0]) || !Num.Ok(m[1]) || t.Masse is null || t.Masse.Length < 3 || !t.Masse.Take(3).All(x => Num.Ok(x) && x > 0)) return null;
                double lx = t.Masse[0], ly = t.Masse[1], lz = t.Masse[2];
                var mz = m.Length > 2 && Num.Ok(m[2]) ? m[2] : 0;
                var w = Num.Ok(t.Drehung) ? t.Drehung!.Value * Num.Grad : 0;
                double c = Math.Cos(w), s = Math.Sin(w);
                var ring = new (int I, int J)[] { (-1, -1), (1, -1), (1, 1), (-1, 1) }.Select(ij =>
                {
                    var dx = ij.I * lx / 2;
                    var dy = ij.J * ly / 2;
                    return AusModellXz([m[0] + dx * c - dy * s, m[1] + dx * s + dy * c]);
                }).ToArray();
                double y0 = (mz - lz / 2) * MM, y1 = (mz + lz / 2) * MM;
                return new PolyForm(ring, [], y0, y1, RingBox(ring, y0, y1));
            }
            case "zylinder":
            {
                if (!PunktOk(t.Von) || !PunktOk(t.Nach) || !(t.Radius > 0)) return null;
                var a = new Vec3(t.Von![0] * MM, Dritte(t.Von) * MM, -t.Von[1] * MM);
                var b = new Vec3(t.Nach![0] * MM, Dritte(t.Nach) * MM, -t.Nach[1] * MM);
                var r = t.Radius!.Value * MM;
                var box = Box3.Von([a, b]);
                return new ZylForm(a, b, r, new Box3(box.Min - new Vec3(r, r, r), box.Max + new Vec3(r, r, r)));
            }
            case "kugel":
            {
                if (!PunktOk(t.Mitte) || !(t.Radius > 0)) return null;
                var c = new Vec3(t.Mitte![0] * MM, Dritte(t.Mitte) * MM, -t.Mitte[1] * MM);
                var r = t.Radius!.Value * MM;
                return new KugelForm(c, r, new Box3(c - new Vec3(r, r, r), c + new Vec3(r, r, r)));
            }
            case "koerper":
            {
                var e = (t.Ecken ?? []).Where(p => p is { Length: >= 3 } && p.All(Num.Ok)).ToList();
                if (e.Count < 3) return null;
                return new BoxForm(Box3.Von(e.Select(p => new Vec3(p[0] * MM, p[2] * MM, -p[1] * MM))));
            }
            default:
                return null;
        }
    }

    static Box3 RingBox(Xz[] ring, double y0, double y1) => Box3.Von(ring.SelectMany(p => new[] { new Vec3(p.X, y0, p.Z), new Vec3(p.X, y1, p.Z) }));

    /// <summary>begehen-logik.js huellboxVon: Hüllbox eines Körpers aus seinen Formen (null, wenn keine).</summary>
    public static Box3? HuellboxVon(IEnumerable<Form>? formen)
    {
        Box3? b = null;
        foreach (var f in formen ?? [])
            b = b is null ? f.Box : b.Value.Vereinigt(f.Box);
        return b;
    }

    /// <summary>begehen-logik.js abstandZurBox: Abstand eines Punktes zu einer achsparallelen Box (0 innen).</summary>
    public static double AbstandZurBox(Vec3 p, Box3 b) => b.Abstand(p);

    static bool InnenRing(double x, double z, Xz[] ring)
    {
        // gerade-ungerade-Regel
        var innen = false;
        for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
        {
            double xi = ring[i].X, zi = ring[i].Z, xj = ring[j].X, zj = ring[j].Z;
            if ((zi > z) != (zj > z) && x < ((xj - xi) * (z - zi)) / (zj - zi) + xi) innen = !innen;
        }
        return innen;
    }

    static double AbstandStrecke2(double px, double pz, double ax, double az, double bx, double bz)
    {
        double dx = bx - ax, dz = bz - az, l2 = dx * dx + dz * dz;
        var t = l2 > 0 ? Math.Min(1, Math.Max(0, ((px - ax) * dx + (pz - az) * dz) / l2)) : 0;
        return Num.Hypot(px - (ax + t * dx), pz - (az + t * dz));
    }

    static double AbstandRand(double x, double z, Xz[] ring)
    {
        var d = double.PositiveInfinity;
        for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
            d = Math.Min(d, AbstandStrecke2(x, z, ring[j].X, ring[j].Z, ring[i].X, ring[i].Z));
        return d;
    }

    /// <summary>begehen-logik.js abstandZurFlaeche: Abstand eines 2D-Punktes zur Fläche (Aussenring mit Löchern); 0 auf der Fläche, sonst zum nächsten Rand.</summary>
    public static double AbstandZurFlaeche(double x, double z, Xz[] ring, Xz[][]? loecher = null)
    {
        loecher ??= [];
        if (InnenRing(x, z, ring) && !loecher.Any(l => InnenRing(x, z, l))) return 0;
        return Math.Min(AbstandRand(x, z, ring), loecher.Select(l => AbstandRand(x, z, l)).DefaultIfEmpty(double.PositiveInfinity).Min());
    }

    /// <summary>begehen-logik.js abstandZurForm: Abstand eines Punktes (Szenenmeter) zu einer Form, in m (0 innen).</summary>
    public static double AbstandZurForm(Vec3 p, Form f)
    {
        switch (f)
        {
            case PolyForm poly:
                return Num.Hypot(AbstandZurFlaeche(p.X, p.Z, poly.Ring, poly.Loecher), Math.Max(Math.Max(poly.Y0 - p.Y, 0), p.Y - poly.Y1));
            case ZylForm zyl:
            {
                // gerader Kreiszylinder mit ebenen Stirnflächen: radialer Abstand zur Achse (minus Radius) und Überstand über die Stirnflächen
                double dx = zyl.B.X - zyl.A.X, dy = zyl.B.Y - zyl.A.Y, dz = zyl.B.Z - zyl.A.Z;
                var l2 = dx * dx + dy * dy + dz * dz;
                var l = Math.Sqrt(l2);
                var t = l2 > 0 ? ((p.X - zyl.A.X) * dx + (p.Y - zyl.A.Y) * dy + (p.Z - zyl.A.Z) * dz) / l2 : 0;
                var ueber = Math.Max(Math.Max(-t * l, (t - 1) * l), 0);
                var radial = Num.Hypot(p.X - (zyl.A.X + t * dx), p.Y - (zyl.A.Y + t * dy), p.Z - (zyl.A.Z + t * dz));
                return Num.Hypot(Math.Max(radial - zyl.R, 0), ueber);
            }
            case KugelForm k:
                return Math.Max(Num.Hypot(p.X - k.C.X, p.Y - k.C.Y, p.Z - k.C.Z) - k.R, 0);
            default:
                return AbstandZurBox(p, f.Box);
        }
    }

    /// <summary>
    /// begehen-logik.js abstandZumKoerper: kürzester Abstand eines Punktes zu den Formen eines Körpers (m); unendlich ohne Formen oder, wenn schon die
    /// Hüllbox weiter als <paramref name="grenze"/> entfernt ist (Vorauswahl: dann ist der genaue Wert nicht nötig).
    /// </summary>
    public static double AbstandZumKoerper(Vec3 p, IReadOnlyList<Form>? formen, Box3? huellbox = null, double grenze = double.PositiveInfinity)
    {
        if (formen is null || formen.Count == 0) return double.PositiveInfinity;
        if (huellbox is { } hb && AbstandZurBox(p, hb) >= grenze) return double.PositiveInfinity;
        var d = double.PositiveInfinity;
        foreach (var f in formen)
        {
            if (AbstandZurBox(p, f.Box) >= Math.Min(d, grenze)) continue;
            d = Math.Min(d, AbstandZurForm(p, f));
        }
        return d;
    }

    /// <summary>
    /// begehen-logik.js imDurchgang: ist ein Körper näher als die Durchgangsgrenze (Vorgabe <see cref="DurchgangMm"/>) an p?
    /// Streng kleiner: genau 500 mm gelten noch als «entfernt». p ist beim Begehen in VR die Kopfposition (nicht die Rig-Mitte).
    /// </summary>
    public static bool ImDurchgang(Vec3 p, IReadOnlyList<Form>? formen, Box3? huellbox = null, double grenzeMm = DurchgangMm)
        => AbstandZumKoerper(p, formen, huellbox, grenzeMm * MM) < grenzeMm * MM;
}
