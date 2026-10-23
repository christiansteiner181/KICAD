// Quest App P4a (Modul XR): Standort zum Begehen eines Raums aus seinem Netz in der GLB – Ersatz für die Kern-Abfrage raum_begehen, die die App über den
// Netzzugang nicht erreicht (nur /api/xr; siehe Bericht P4a, «am Server fehlt»).
//
// Dieselben Regeln wie src/Hlks.Kern/Plan/RaumBegehen.cs, soweit das Netz sie hergibt:
//   - Grundriss = die waagrechten Dreiecke an der Unterseite des Raumkörpers (Gruppe «raum», ein Prisma vom Boden bis zur Raumhöhe);
//   - Boden = tiefster Punkt (Unterseite des Körpers);
//   - Standpunkt = Flächenschwerpunkt; liegt er ausserhalb (L-, U-Form), der innerste Punkt (grösster Abstand zum Rand), hier über ein Raster mit zwei
//     Verfeinerungen auf etwa 1/400 der Ausdehnung genau (Kern: 10 mm);
//   - Blick waagrecht. ANNAHME P4a: Der Kern blickt in Richtung «oben» des ausgerichteten Grundrisses (Planwinkel des Gebäudes); die App kennt den
//     Planwinkel nicht und nimmt die Richtung der längsten Kante des Raums, gedreht auf −45° … 45° um Projekt-Nord. Bei Gebäuden in den Achsen ist das
//     wie im Kern Norden (Azimut 90°).
//   - Augenhöhe: Vorgabe des Begehens (1700 mm über OK Boden).

using Hlks.Xr.Netz;

namespace Hlks.Xr.Modell;

/// <summary>Grundriss eines Raums aus dem Netz: waagrechte Dreiecke der Unterseite (x, z in der Szene), Boden (y), Fläche (m²) und Randkanten.</summary>
public sealed record RaumGrundriss(IReadOnlyList<(Xz A, Xz B, Xz C)> Dreiecke, double Boden, double Flaeche, IReadOnlyList<(Xz A, Xz B)> Rand);

public static class RaumStandort
{
    /// <summary>Toleranz für «auf der Unterseite» (m).</summary>
    const double BodenToleranz = 0.002;

    /// <summary>Grundriss aus den Ecken (je drei ein Dreieck, Szene); null ohne waagrechte Unterseite.</summary>
    public static RaumGrundriss? Grundriss(IReadOnlyList<Vec3> ecken)
    {
        if (ecken.Count < 3) return null;
        var boden = double.PositiveInfinity;
        foreach (var p in ecken) if (p.IstEndlich) boden = Math.Min(boden, p.Y);
        if (!double.IsFinite(boden)) return null;
        var dreiecke = new List<(Xz, Xz, Xz)>();
        double flaeche = 0;
        var kanten = new Dictionary<(long, long, long, long), (Xz A, Xz B, int N)>();
        static long K(double v) => (long)Math.Round(v * 10000);   // 0.1 mm
        void Kante(Xz a, Xz b)
        {
            var ka = (K(a.X), K(a.Z));
            var kb = (K(b.X), K(b.Z));
            var key = ka.CompareTo(kb) <= 0 ? (ka.Item1, ka.Item2, kb.Item1, kb.Item2) : (kb.Item1, kb.Item2, ka.Item1, ka.Item2);
            kanten[key] = kanten.TryGetValue(key, out var x) ? (x.A, x.B, x.N + 1) : (a, b, 1);
        }
        for (int i = 0; i + 2 < ecken.Count; i += 3)
        {
            Vec3 a = ecken[i], b = ecken[i + 1], c = ecken[i + 2];
            if (Math.Abs(a.Y - boden) > BodenToleranz || Math.Abs(b.Y - boden) > BodenToleranz || Math.Abs(c.Y - boden) > BodenToleranz) continue;
            var f = Math.Abs((b.X - a.X) * (c.Z - a.Z) - (c.X - a.X) * (b.Z - a.Z)) / 2;
            if (f < 1e-10) continue;
            var t = (new Xz(a.X, a.Z), new Xz(b.X, b.Z), new Xz(c.X, c.Z));
            dreiecke.Add(t);
            flaeche += f;
            Kante(t.Item1, t.Item2); Kante(t.Item2, t.Item3); Kante(t.Item3, t.Item1);
        }
        if (dreiecke.Count == 0) return null;
        var rand = kanten.Values.Where(k => k.N == 1).Select(k => (k.A, k.B)).ToList();
        return new RaumGrundriss(dreiecke, boden, flaeche, rand);
    }

    static bool ImDreieck(Xz p, Xz a, Xz b, Xz c)
    {
        double d1 = (p.X - b.X) * (a.Z - b.Z) - (a.X - b.X) * (p.Z - b.Z);
        double d2 = (p.X - c.X) * (b.Z - c.Z) - (b.X - c.X) * (p.Z - c.Z);
        double d3 = (p.X - a.X) * (c.Z - a.Z) - (c.X - a.X) * (p.Z - a.Z);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(neg && pos);
    }

    /// <summary>Liegt der Punkt im Grundriss?</summary>
    public static bool Innen(RaumGrundriss g, Xz p) => g.Dreiecke.Any(t => ImDreieck(p, t.A, t.B, t.C));

    /// <summary>Abstand zum Rand (m), positiv innen, negativ aussen.</summary>
    public static double Abstand(RaumGrundriss g, Xz p)
    {
        var d = double.PositiveInfinity;
        foreach (var (a, b) in g.Rand)
        {
            double dx = b.X - a.X, dz = b.Z - a.Z, l2 = dx * dx + dz * dz;
            var t = l2 > 0 ? Math.Clamp(((p.X - a.X) * dx + (p.Z - a.Z) * dz) / l2, 0, 1) : 0;
            d = Math.Min(d, Num.Hypot(p.X - (a.X + t * dx), p.Z - (a.Z + t * dz)));
        }
        return Innen(g, p) ? d : -d;
    }

    /// <summary>Schwerpunkt (flächengewichtet) und, falls er ausserhalb liegt, der innerste Punkt; mit Art wie raum_begehen.</summary>
    public static (Xz Punkt, string Art) Standpunkt(RaumGrundriss g)
    {
        double sx = 0, sz = 0, sf = 0;
        foreach (var (a, b, c) in g.Dreiecke)
        {
            var f = Math.Abs((b.X - a.X) * (c.Z - a.Z) - (c.X - a.X) * (b.Z - a.Z)) / 2;
            sx += f * (a.X + b.X + c.X) / 3;
            sz += f * (a.Z + b.Z + c.Z) / 3;
            sf += f;
        }
        var s = new Xz(sx / sf, sz / sf);
        if (Abstand(g, s) > 0) return (s, "schwerpunkt");
        double x0 = double.PositiveInfinity, z0 = double.PositiveInfinity, x1 = double.NegativeInfinity, z1 = double.NegativeInfinity;
        foreach (var (a, b, c) in g.Dreiecke)
            foreach (var p in new[] { a, b, c }) { x0 = Math.Min(x0, p.X); x1 = Math.Max(x1, p.X); z0 = Math.Min(z0, p.Z); z1 = Math.Max(z1, p.Z); }
        var beste = s;
        var besteD = double.NegativeInfinity;
        double bx = (x1 - x0) / 20, bz = (z1 - z0) / 20;
        double mx = (x0 + x1) / 2, mz = (z0 + z1) / 2;
        for (int stufe = 0; stufe < 3; stufe++)
        {
            for (int i = -10; i <= 10; i++)
                for (int j = -10; j <= 10; j++)
                {
                    var p = new Xz(mx + i * bx, mz + j * bz);
                    var d = Abstand(g, p);
                    if (d > besteD) { besteD = d; beste = p; }
                }
            mx = beste.X; mz = beste.Z; bx /= 10; bz /= 10;
        }
        return (beste, "innerster Punkt");
    }

    /// <summary>Blick (Azimut in Grad, ab Projekt-x gegen den Uhrzeigersinn) nach der längsten Randkante, auf 90° ± 45° gebracht (siehe Dateikopf).</summary>
    public static double AzimutGrad(RaumGrundriss g)
    {
        var laengste = g.Rand.OrderByDescending(k => Num.Hypot(k.B.X - k.A.X, k.B.Z - k.A.Z)).FirstOrDefault();
        double dx = laengste.B.X - laengste.A.X, dy = -(laengste.B.Z - laengste.A.Z);   // Szene z = −Nord → Projekt y = −z
        if (Num.Hypot(dx, dy) < 1e-9) return 90;
        var w = Math.Atan2(dy, dx) / Num.Grad;   // −180 … 180
        w = ((w % 90) + 90) % 90;                 // 0 … 90
        if (w > 45) w -= 90;                      // −45 … 45
        return Math.Round((90 + w) * 1e4) / 1e4;
    }

    /// <summary>
    /// Standort im Format der Abfrage raum_begehen (Projekt-mm, absolut mit Bezugspunkt) aus den Ecken des Raumkörpers; null, wenn der Körper keine waagrechte
    /// Unterseite hat. raum = Anzeige (Id oder Kennzeichen), geschoss/geschossName für die Einblendung.
    /// </summary>
    public static Standort? AusEcken(IReadOnlyList<Vec3> ecken, double[]? bezug, string? raum = null, string? geschoss = null, string? geschossName = null)
    {
        if (Grundriss(ecken) is not { } g) return null;
        var (p, art) = Standpunkt(g);
        var mitte = new Vec3(p.X, g.Boden, p.Z);
        var mm = KicadUmrechnung.PunktZuKicad(mitte, bezug);
        static double R(double x) => Math.Round(x * 10) / 10;
        return new Standort
        {
            Raum = raum, Name = "", Nummer = "", Geschoss = geschoss ?? "", GeschossName = geschossName ?? "",
            Standpunkt = new Standpunkt(R(mm[0]), R(mm[1]), R(mm[2] + Begehen.AugenhoeheVorgabe), art),
            BodenOk = R(mm[2]),
            Blick = new Blick(AzimutGrad(g), 0),
        };
    }
}
