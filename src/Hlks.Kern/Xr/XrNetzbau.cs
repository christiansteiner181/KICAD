// Quest App (Modul XR, P2): Körperteile des Kerns (Grafiktypen.cs: quader, prisma, zylinder, kugel, koerper) und Netze der IFC-Unterlage
// als Dreiecke mit Normalen, direkt in den Puffer eines Netzes (ein Material) in glTF-Koordinaten (XrKoordinaten).
//
// Gleiche Körper wie die 3D-Ansicht (koerper3d.js); Unterschiede nur in der Zerlegung runder Teile (technische Annahmen P2, keine Normwerte):
//   - Zylinder und Kugeln mit Rund = 24 Strecken je Vollkreis (3D-Ansicht 32): Sehnenfehler 1 − cos(7.5°) ≈ 0.86 % des Radius,
//     bei DN 100 (r ≈ 57 mm) rund 0.5 mm. Kugeln mit 24 Längen- und 12 Breitenstreifen (528 Dreiecke statt 960) – an jedem Knick einer
//     Rohrleitung sitzt eine Kugel, sie bestimmen die Dreieckszahl der Haustechnik.
//   - Quader und Prismen mit Flächennormalen (wie ExtrudeGeometry/BoxGeometry), Zylinder und Kugeln mit glatten Normalen am Mantel,
//     Deckel des Zylinders flach (Fächer ohne Mittelpunkt).
//   - Allgemeine Netze (Familienkörper «koerper», IFC-Unterlage) mit Knick-Normalen: über Kanten mit weniger als 20° Knick gemittelt
//     (nach Winkel gewichtet), sonst getrennt – wie koerper3d.js (KNICK) und Schnittzerlegung.Knickwinkel. Ecken mit gleicher Lage und
//     gleicher Normale werden geteilt (indiziert).
//   - IFC-Unterlage: die Netzdatei ist nicht orientiert (netzdatei.js). Je zusammenhängendem Stück werden die Dreiecke über gemeinsame
//     Kanten gleich gerichtet und so gewendet, dass das umschlossene Volumen positiv ist (Normalen nach aussen); so stimmen Knick-Normalen
//     und Licht auch in Darstellern, die Rückseiten nicht umkehren.
// Entartete Dreiecke (Fläche 0, doppelte oder ungültige Ecken) entfallen.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <summary>Bereich eines Netzes, der zu einem Element gehört: Dreiecke [Ab, Ab + Anzahl). Zusatz: Ebene (Modell) bzw. Entität (Unterlage).</summary>
public sealed record XrBereich(string Id, int Ab, int Anzahl, string? Zusatz);

/// <summary>Sammelt die Dreiecke eines Netzes (ein Material) in glTF-Koordinaten und die Zuordnung Dreiecksbereich → Element.</summary>
public sealed class XrPuffer(double[] bezug)
{
    float[] pos = new float[3 * 64], nor = new float[3 * 64];
    uint[] idx = new uint[3 * 64];
    int ecken, indizes;
    readonly List<XrBereich> bereiche = [];

    public double[] Bezug { get; } = bezug;
    public int Ecken => ecken;
    public int Dreiecke => indizes / 3;
    public IReadOnlyList<XrBereich> Bereiche => bereiche;

    /// <summary>Ecke aus KICAD-Lage (mm) und KICAD-Normale; gibt ihren Index zurück.</summary>
    public int Ecke(double x, double y, double z, double nx, double ny, double nz)
    {
        if (ecken * 3 + 3 > pos.Length) { Array.Resize(ref pos, pos.Length * 2); Array.Resize(ref nor, nor.Length * 2); }
        var (gx, gy, gz) = XrKoordinaten.Punkt(x, y, z, Bezug);
        var (hx, hy, hz) = XrKoordinaten.Richtung(nx, ny, nz);
        int o = ecken * 3;
        pos[o] = gx; pos[o + 1] = gy; pos[o + 2] = gz;
        nor[o] = hx; nor[o + 1] = hy; nor[o + 2] = hz;
        return ecken++;
    }

    public void Dreieck(int a, int b, int c)
    {
        if (indizes + 3 > idx.Length) Array.Resize(ref idx, idx.Length * 2);
        idx[indizes++] = (uint)a; idx[indizes++] = (uint)b; idx[indizes++] = (uint)c;
    }

    public void Viereck(int a, int b, int c, int d) { Dreieck(a, b, c); Dreieck(a, c, d); }

    /// <summary>Hält fest, dass die Dreiecke ab <paramref name="ab"/> bis zum Ende zu diesem Element gehören (schliesst an den letzten
    /// Bereich an, wenn er demselben Element gehört).</summary>
    public void Zuordnen(string id, string? zusatz, int ab)
    {
        int anzahl = Dreiecke - ab;
        if (anzahl <= 0) return;
        if (bereiche.Count > 0 && bereiche[^1] is { } l && l.Id == id && l.Ab + l.Anzahl == ab && l.Zusatz == zusatz)
            bereiche[^1] = l with { Anzahl = l.Anzahl + anzahl };
        else bereiche.Add(new XrBereich(id, ab, anzahl, zusatz));
    }

    /// <summary>Das fertige Netz für den GLB-Schreiber.</summary>
    public GlbNetz Netz(string name, GlbMaterial material, JsonObject? extras) => new()
    {
        Name = name, Material = material, Extras = extras,
        Positionen = pos.AsSpan(0, ecken * 3).ToArray(), Normalen = nor.AsSpan(0, ecken * 3).ToArray(), Indizes = idx.AsSpan(0, indizes).ToArray(),
    };
}

public static class XrNetzbau
{
    /// <summary>Strecken je Vollkreis für Zylinder und die Längen der Kugeln (siehe Dateikopf).</summary>
    public const int Rund = 24;
    /// <summary>Breitenstreifen der Kugeln (Pol zu Pol).</summary>
    public const int KugelBreiten = 12;
    /// <summary>Knickwinkel (Grad) der allgemeinen Netze, wie koerper3d.js KNICK und Schnittzerlegung.Knickwinkel.</summary>
    public const double KnickGrad = 20;
    /// <summary>Ab so vielen Dreiecken an einem Punkt werden die Normalen dort in Gruppen gemittelt (siehe Netz; technisch, gegen quadratischen Aufwand).</summary>
    public const int ValenzGrenze = 64;

    /// <summary>Ein Körperteil in den Puffer; false, wenn es nichts zu zeichnen gibt (fehlende oder ungültige Angaben, Art netz).</summary>
    public static bool Teil(Koerperteil t, XrPuffer p) => t.Art switch
    {
        "quader" => Quader(t, p),
        "prisma" => Prisma(t, p),
        "zylinder" => Zylinder(t, p),
        "kugel" => Kugel(t, p),
        "koerper" => Koerper(t, p),
        _ => false,
    };

    static bool Endlich(double[]? a, int n) => a is not null && a.Length >= n && a.Take(n).All(double.IsFinite);

    // ------------------------------------------------------------------ Quader

    /// <summary>Quader: Mitte, Masse [lx, ly, lz], Drehung um z (Grad, Gegenuhrzeigersinn). 24 Ecken, 12 Dreiecke, Flächennormalen.</summary>
    static bool Quader(Koerperteil t, XrPuffer p)
    {
        if (!Endlich(t.Mitte, 3) || !Endlich(t.Masse, 3) || t.Masse![0] <= 0 || t.Masse[1] <= 0 || t.Masse[2] <= 0) return false;
        var m = t.Mitte!; var s = t.Masse;
        double w = (t.Drehung is double d && double.IsFinite(d) ? d : 0) * Math.PI / 180, c = Math.Cos(w), sn = Math.Sin(w);
        double hx = s[0] / 2, hy = s[1] / 2, hz = s[2] / 2;
        (double X, double Y, double Z) E(int ix, int iy, int iz)
        {
            double px = ix * hx, py = iy * hy;
            return (m[0] + px * c - py * sn, m[1] + px * sn + py * c, m[2] + iz * hz);
        }
        void Flaeche((int, int, int) a, (int, int, int) b, (int, int, int) cc, (int, int, int) dd, double nx, double ny, double nz)
        {
            double gx = nx * c - ny * sn, gy = nx * sn + ny * c;   // Normale mitdrehen
            int Ecke((int X, int Y, int Z) q) { var e = E(q.X, q.Y, q.Z); return p.Ecke(e.X, e.Y, e.Z, gx, gy, nz); }
            p.Viereck(Ecke(a), Ecke(b), Ecke(cc), Ecke(dd));
        }
        // wie NetzBau.Quader: jede Fläche im Gegenuhrzeigersinn von aussen
        Flaeche((-1, -1, -1), (-1, 1, -1), (1, 1, -1), (1, -1, -1), 0, 0, -1);   // unten
        Flaeche((-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1), 0, 0, 1);       // oben
        Flaeche((-1, -1, -1), (1, -1, -1), (1, -1, 1), (-1, -1, 1), 0, -1, 0);  // vorne (−y)
        Flaeche((1, -1, -1), (1, 1, -1), (1, 1, 1), (1, -1, 1), 1, 0, 0);       // rechts (+x)
        Flaeche((1, 1, -1), (-1, 1, -1), (-1, 1, 1), (1, 1, 1), 0, 1, 0);       // hinten (+y)
        Flaeche((-1, 1, -1), (-1, -1, -1), (-1, -1, 1), (-1, 1, 1), -1, 0, 0);  // links (−x)
        return true;
    }

    // ------------------------------------------------------------------ Prisma

    /// <summary>Ring ohne doppelte Punkte (auch Schluss = Anfang) im gewünschten Drehsinn; null bei weniger als 3 Punkten oder Fläche 0.</summary>
    static List<(double X, double Y)>? Ring(List<double[]>? roh, bool ccw)
    {
        if (roh is null) return null;
        var r = new List<(double X, double Y)>();
        foreach (var q in roh)
        {
            if (q is not { Length: >= 2 } || !double.IsFinite(q[0]) || !double.IsFinite(q[1])) return null;
            if (r.Count == 0 || Math.Abs(q[0] - r[^1].X) > 1e-6 || Math.Abs(q[1] - r[^1].Y) > 1e-6) r.Add((q[0], q[1]));
        }
        while (r.Count > 1 && Math.Abs(r[0].X - r[^1].X) <= 1e-6 && Math.Abs(r[0].Y - r[^1].Y) <= 1e-6) r.RemoveAt(r.Count - 1);
        if (r.Count < 3) return null;
        var f = NetzBau.Flaeche(r);
        if (Math.Abs(f) < 1e-6) return null;
        if ((f > 0) != ccw) r.Reverse();
        return r;
    }

    /// <summary>Prisma: Grundriss (Aussenring) mit Löchern, senkrecht von unten bis oben. Deckel trianguliert (Kern: Triangulierung),
    /// Seitenflächen je Kante; alles mit Flächennormalen.</summary>
    static bool Prisma(Koerperteil t, XrPuffer p)
    {
        if (t.Unten is not double u || t.Oben is not double o || !double.IsFinite(u) || !double.IsFinite(o) || o - u < 1e-6) return false;
        var aussen = Ring(t.Grundriss, true);
        if (aussen is null) return false;
        var ringe = new List<List<(double X, double Y)>> { aussen };
        foreach (var l in t.Loecher ?? []) if (Ring(l, false) is { } r) ringe.Add(r);

        // Deckel: Punkte aller Ringe in einer Liste
        var punkte = new List<(double X, double Y)>();
        var indizes = new List<IReadOnlyList<int>>();
        foreach (var r in ringe) { indizes.Add(Enumerable.Range(punkte.Count, r.Count).ToList()); punkte.AddRange(r); }
        List<int[]> deckel;
        try { deckel = Triangulierung.Dreiecke(punkte, indizes[0], indizes.Skip(1).ToList()); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IndexOutOfRangeException) { deckel = []; }
        int start = p.Dreiecke;
        var obenIdx = new int[punkte.Count]; var untenIdx = new int[punkte.Count];
        for (int i = 0; i < punkte.Count; i++)
        {
            obenIdx[i] = p.Ecke(punkte[i].X, punkte[i].Y, o, 0, 0, 1);
            untenIdx[i] = p.Ecke(punkte[i].X, punkte[i].Y, u, 0, 0, -1);
        }
        foreach (var d in deckel)
        {
            p.Dreieck(obenIdx[d[0]], obenIdx[d[1]], obenIdx[d[2]]);      // Gegenuhrzeigersinn von oben
            p.Dreieck(untenIdx[d[0]], untenIdx[d[2]], untenIdx[d[1]]);   // von unten gesehen ebenso
        }
        // Seiten: rechts der Kante ist aussen (Aussenring Gegenuhrzeiger, Löcher Uhrzeiger)
        foreach (var r in ringe)
            for (int i = 0; i < r.Count; i++)
            {
                var a = r[i]; var b = r[(i + 1) % r.Count];
                double ex = b.X - a.X, ey = b.Y - a.Y, l = Math.Sqrt(ex * ex + ey * ey);
                if (l < 1e-9) continue;
                double nx = ey / l, ny = -ex / l;
                p.Viereck(p.Ecke(a.X, a.Y, u, nx, ny, 0), p.Ecke(b.X, b.Y, u, nx, ny, 0), p.Ecke(b.X, b.Y, o, nx, ny, 0), p.Ecke(a.X, a.Y, o, nx, ny, 0));
            }
        return p.Dreiecke > start;
    }

    // ------------------------------------------------------------------ Zylinder und Kugel

    /// <summary>Zylinder von → nach mit Radius: Mantel mit Rund Strecken und glatten Normalen, Deckel flach.</summary>
    static bool Zylinder(Koerperteil t, XrPuffer p)
    {
        if (!Endlich(t.Von, 3) || !Endlich(t.Nach, 3) || t.Radius is not double r || !double.IsFinite(r) || r <= 0) return false;
        var a = V3.Aus(t.Von!); var b = V3.Aus(t.Nach!);
        var d = b - a;
        if (d.Laenge < 1e-9) return false;
        var e = d.Einheit();
        var (e1, e2) = e.Senkrechte();   // e1 × e2 = e: Winkel wachsen im Gegenuhrzeigersinn um e
        int n = Rund;
        var radial = new V3[n];
        for (int k = 0; k < n; k++) { double w = 2 * Math.PI * k / n; radial[k] = e1 * Math.Cos(w) + e2 * Math.Sin(w); }
        var mu = new int[n]; var mo = new int[n];
        for (int k = 0; k < n; k++)
        {
            var q = radial[k];
            var pu = a + q * r; var po = b + q * r;
            mu[k] = p.Ecke(pu.X, pu.Y, pu.Z, q.X, q.Y, q.Z);
            mo[k] = p.Ecke(po.X, po.Y, po.Z, q.X, q.Y, q.Z);
        }
        for (int k = 0; k < n; k++) { int k1 = (k + 1) % n; p.Viereck(mu[k], mu[k1], mo[k1], mo[k]); }
        var du = new int[n]; var dob = new int[n];
        for (int k = 0; k < n; k++)
        {
            var pu = a + radial[k] * r; var po = b + radial[k] * r;
            du[k] = p.Ecke(pu.X, pu.Y, pu.Z, -e.X, -e.Y, -e.Z);
            dob[k] = p.Ecke(po.X, po.Y, po.Z, e.X, e.Y, e.Z);
        }
        for (int k = 1; k < n - 1; k++)
        {
            p.Dreieck(dob[0], dob[k], dob[k + 1]);   // Gegenuhrzeigersinn von +e
            p.Dreieck(du[0], du[k + 1], du[k]);      // von −e
        }
        return true;
    }

    /// <summary>Kugel um die Mitte: Rund Längen, KugelBreiten Breiten, glatte Normalen, Pole als je eine Ecke.</summary>
    static bool Kugel(Koerperteil t, XrPuffer p)
    {
        if (!Endlich(t.Mitte, 3) || t.Radius is not double r || !double.IsFinite(r) || r <= 0) return false;
        var m = t.Mitte!;
        int nl = Rund, nb = KugelBreiten;
        int sued = p.Ecke(m[0], m[1], m[2] - r, 0, 0, -1);
        var ringe = new int[nb - 1][];
        for (int j = 1; j < nb; j++)
        {
            double phi = -Math.PI / 2 + Math.PI * j / nb, cp = Math.Cos(phi), sp = Math.Sin(phi);
            ringe[j - 1] = new int[nl];
            for (int k = 0; k < nl; k++)
            {
                double l = 2 * Math.PI * k / nl, nx = cp * Math.Cos(l), ny = cp * Math.Sin(l);
                ringe[j - 1][k] = p.Ecke(m[0] + r * nx, m[1] + r * ny, m[2] + r * sp, nx, ny, sp);
            }
        }
        int nord = p.Ecke(m[0], m[1], m[2] + r, 0, 0, 1);
        for (int k = 0; k < nl; k++)
        {
            int k1 = (k + 1) % nl;
            p.Dreieck(sued, ringe[0][k1], ringe[0][k]);
            for (int j = 0; j < nb - 2; j++) p.Viereck(ringe[j][k], ringe[j][k1], ringe[j + 1][k1], ringe[j + 1][k]);
            p.Dreieck(nord, ringe[^1][k], ringe[^1][k1]);
        }
        return true;
    }

    // ------------------------------------------------------------------ allgemeine Netze

    /// <summary>Familienkörper (Art koerper): Ecken und Dreiecke (Gegenuhrzeigersinn von aussen) mit Knick-Normalen.</summary>
    static bool Koerper(Koerperteil t, XrPuffer p)
    {
        if (t.Ecken is not { Count: >= 3 } ecken || t.Dreiecke is not { Count: >= 1 } dr) return false;
        var punkte = new double[ecken.Count * 3];
        for (int i = 0; i < ecken.Count; i++)
        {
            var q = ecken[i];
            if (!Endlich(q, 3)) return false;   // wie koerper3d.js: eine Ecke ohne z macht das Netz ungültig
            punkte[i * 3] = q[0]; punkte[i * 3 + 1] = q[1]; punkte[i * 3 + 2] = q[2];
        }
        var tri = new int[dr.Count * 3];
        int n = 0;
        foreach (var x in dr)
        {
            if (x is not { Length: 3 }) continue;
            tri[n++] = x[0]; tri[n++] = x[1]; tri[n++] = x[2];
        }
        return Netz(punkte, tri.AsSpan(0, n).ToArray(), p, false) > 0;
    }

    /// <summary>Allgemeines Dreiecksnetz (Punkte x, y, z in KICAD-mm; Dreiecke als Punktindizes) mit Knick-Normalen in den Puffer.
    /// orientieren: Dreiecke je zusammenhängendem Stück gleich richten, Volumen positiv (IFC-Unterlage). Gibt die Zahl der
    /// geschriebenen Dreiecke zurück; ungültige und entartete Dreiecke entfallen.</summary>
    public static int Netz(double[] punkte, int[] dreiecke, XrPuffer p, bool orientieren, double knickGrad = KnickGrad)
    {
        int n = punkte.Length / 3;
        // gültige Dreiecke: Indizes im Bereich, drei verschiedene Ecken, Fläche > 0
        var t = new List<int>(dreiecke.Length);
        for (int f = 0; f + 2 < dreiecke.Length; f += 3)
        {
            int a = dreiecke[f], b = dreiecke[f + 1], c = dreiecke[f + 2];
            if ((uint)a >= (uint)n || (uint)b >= (uint)n || (uint)c >= (uint)n || a == b || b == c || a == c) continue;
            var (_, _, _, l) = Normale(punkte, a, b, c);
            if (!(l > 1e-12) || !double.IsFinite(l)) continue;
            t.Add(a); t.Add(b); t.Add(c);
        }
        if (t.Count == 0) return 0;
        var tri = t.ToArray();
        if (orientieren) Orientieren(punkte, tri);
        int m = tri.Length / 3;
        var fnA = new double[m * 3];
        var winkel = new double[m * 3];
        for (int f = 0; f < m; f++)
        {
            int a = tri[f * 3], b = tri[f * 3 + 1], c = tri[f * 3 + 2];
            var (nx, ny, nz, l) = Normale(punkte, a, b, c);
            fnA[f * 3] = nx / l; fnA[f * 3 + 1] = ny / l; fnA[f * 3 + 2] = nz / l;
            for (int k = 0; k < 3; k++)
            {
                int i = tri[f * 3 + k], j = tri[f * 3 + (k + 1) % 3], h = tri[f * 3 + (k + 2) % 3];
                winkel[f * 3 + k] = Winkel(punkte, i, j, h);
            }
        }
        // Dreiecke je Punkt (CSR)
        var start = new int[n + 1];
        foreach (var i in tri) start[i + 1]++;
        for (int i = 0; i < n; i++) start[i + 1] += start[i];
        var liste = new int[tri.Length];
        var fuell = (int[])start.Clone();
        for (int c = 0; c < tri.Length; c++) liste[fuell[tri[c]]++] = c;   // c = f * 3 + k

        double grenze = Math.Cos(knickGrad * Math.PI / 180);
        // Normale je Ecke eines Dreiecks (c = f * 3 + k): Summe der Flächennormalen am selben Punkt, die weniger als den Knickwinkel von
        // der eigenen abweichen, nach dem Winkel an der Ecke gewichtet (wie koerper3d.js). An Punkten mit mehr als ValenzGrenze Dreiecken
        // (Fächer, Spitzen fein zerlegter Möbel: sonst quadratisch, Befund Test_praxis EG 30 s) werden zuerst Gruppen gebildet – jedes Dreieck
        // in die erste Gruppe, deren erste Normale weniger als den Knickwinkel abweicht – und je Gruppe gemittelt.
        var cn = new double[tri.Length * 3];
        var gruppen = new List<(double X, double Y, double Z, double Sx, double Sy, double Sz)>();
        var gruppeVon = Array.Empty<int>();
        for (int i = 0; i < n; i++)
        {
            int s0 = start[i], s1 = start[i + 1], d = s1 - s0;
            if (d == 0) continue;
            if (d <= ValenzGrenze)
            {
                for (int q = s0; q < s1; q++)
                {
                    int c = liste[q], f = c / 3;
                    double fx = fnA[f * 3], fy = fnA[f * 3 + 1], fz = fnA[f * 3 + 2], sx = 0, sy = 0, sz = 0;
                    for (int q2 = s0; q2 < s1; q2++)
                    {
                        int c2 = liste[q2], g = c2 / 3;
                        double gx = fnA[g * 3], gy = fnA[g * 3 + 1], gz = fnA[g * 3 + 2];
                        if (gx * fx + gy * fy + gz * fz < grenze) continue;
                        double w = winkel[c2];
                        sx += gx * w; sy += gy * w; sz += gz * w;
                    }
                    Setzen(cn, c, sx, sy, sz, fx, fy, fz);
                }
                continue;
            }
            gruppen.Clear();
            if (gruppeVon.Length < d) gruppeVon = new int[d];
            for (int q = s0; q < s1; q++)
            {
                int c = liste[q], g = c / 3;
                double gx = fnA[g * 3], gy = fnA[g * 3 + 1], gz = fnA[g * 3 + 2], w = winkel[c];
                int k = -1;
                for (int j = 0; j < gruppen.Count; j++)
                    if (gruppen[j].X * gx + gruppen[j].Y * gy + gruppen[j].Z * gz >= grenze) { k = j; break; }
                if (k < 0) { gruppen.Add((gx, gy, gz, 0, 0, 0)); k = gruppen.Count - 1; }
                var x = gruppen[k];
                gruppen[k] = (x.X, x.Y, x.Z, x.Sx + gx * w, x.Sy + gy * w, x.Sz + gz * w);
                gruppeVon[q - s0] = k;
            }
            for (int q = s0; q < s1; q++)
            {
                int c = liste[q], f = c / 3;
                var x = gruppen[gruppeVon[q - s0]];
                Setzen(cn, c, x.Sx, x.Sy, x.Sz, fnA[f * 3], fnA[f * 3 + 1], fnA[f * 3 + 2]);
            }
        }

        var erste = new int[n];
        Array.Fill(erste, -1);
        var naechste = new List<int>();          // je erzeugter Ecke: nächste Ecke am selben Punkt
        var normalen = new List<double>();       // je erzeugter Ecke: Normale (KICAD)
        var eckeVon = new List<int>();           // je erzeugter Ecke: Index im Puffer
        int geschrieben = 0;
        var v = new int[3];
        for (int f = 0; f < m; f++)
        {
            for (int k = 0; k < 3; k++)
            {
                int c = f * 3 + k, i = tri[c];
                double sx = cn[c * 3], sy = cn[c * 3 + 1], sz = cn[c * 3 + 2];
                // vorhandene Ecke am selben Punkt mit gleicher Normale?
                int gefunden = -1;
                for (int e = erste[i]; e >= 0; e = naechste[e])
                    if (normalen[e * 3] * sx + normalen[e * 3 + 1] * sy + normalen[e * 3 + 2] * sz >= 1 - 1e-9) { gefunden = e; break; }
                if (gefunden < 0)
                {
                    gefunden = eckeVon.Count;
                    eckeVon.Add(p.Ecke(punkte[i * 3], punkte[i * 3 + 1], punkte[i * 3 + 2], sx, sy, sz));
                    normalen.Add(sx); normalen.Add(sy); normalen.Add(sz);
                    naechste.Add(erste[i]);
                    erste[i] = gefunden;
                }
                v[k] = eckeVon[gefunden];
            }
            p.Dreieck(v[0], v[1], v[2]);
            geschrieben++;
        }
        return geschrieben;
    }

    /// <summary>Gleichheit und gut verteilter Streuwert für Kantenschlüssel (zwei int in einem long).</summary>
    sealed class KantenVergleich : IEqualityComparer<long>
    {
        public static readonly KantenVergleich Instanz = new();
        public bool Equals(long a, long b) => a == b;
        public int GetHashCode(long x) => (int)(((ulong)x * 0x9E3779B97F4A7C15UL) >> 32);
    }

    /// <summary>Normale der Ecke c: Summe normiert, bei Summe 0 die Flächennormale.</summary>
    static void Setzen(double[] cn, int c, double sx, double sy, double sz, double fx, double fy, double fz)
    {
        double sl = Math.Sqrt(sx * sx + sy * sy + sz * sz);
        if (sl < 1e-12 || !double.IsFinite(sl)) { sx = fx; sy = fy; sz = fz; } else { sx /= sl; sy /= sl; sz /= sl; }
        cn[c * 3] = sx; cn[c * 3 + 1] = sy; cn[c * 3 + 2] = sz;
    }

    static (double X, double Y, double Z, double L) Normale(double[] p, int a, int b, int c)
    {
        double ux = p[b * 3] - p[a * 3], uy = p[b * 3 + 1] - p[a * 3 + 1], uz = p[b * 3 + 2] - p[a * 3 + 2];
        double vx = p[c * 3] - p[a * 3], vy = p[c * 3 + 1] - p[a * 3 + 1], vz = p[c * 3 + 2] - p[a * 3 + 2];
        double x = uy * vz - uz * vy, y = uz * vx - ux * vz, z = ux * vy - uy * vx;
        return (x, y, z, Math.Sqrt(x * x + y * y + z * z));
    }

    /// <summary>Winkel an der Ecke i zwischen den Kanten zu j und h (Bogenmass).</summary>
    static double Winkel(double[] p, int i, int j, int h)
    {
        double ax = p[j * 3] - p[i * 3], ay = p[j * 3 + 1] - p[i * 3 + 1], az = p[j * 3 + 2] - p[i * 3 + 2];
        double bx = p[h * 3] - p[i * 3], by = p[h * 3 + 1] - p[i * 3 + 1], bz = p[h * 3 + 2] - p[i * 3 + 2];
        double la = Math.Sqrt(ax * ax + ay * ay + az * az), lb = Math.Sqrt(bx * bx + by * by + bz * bz);
        if (la < 1e-12 || lb < 1e-12) return 0;
        return Math.Acos(Math.Clamp((ax * bx + ay * by + az * bz) / (la * lb), -1, 1));
    }

    /// <summary>Dreiecke je zusammenhängendem Stück (über Kanten mit genau zwei Dreiecken) gleich richten und so wenden, dass das
    /// Volumen des Stücks positiv ist (Normalen nach aussen bei geschlossenen Stücken). Ändert <paramref name="tri"/>.</summary>
    public static void Orientieren(double[] punkte, int[] tri)
    {
        int m = tri.Length / 3;
        if (m == 0) return;
        // Kante (klein, gross) → erstes und zweites Dreieck, Anzahl. Eigener Streuwert: long.GetHashCode ist a ^ b, für benachbarte
        // Punktnummern fast immer gleich (Befund Test_praxis: Geländer mit 185 000 Dreiecken 30 s statt Millisekunden).
        var kanten = new Dictionary<long, (int A, int B, int N)>(m * 2, KantenVergleich.Instanz);
        static long Schluessel(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        for (int f = 0; f < m; f++)
            for (int k = 0; k < 3; k++)
            {
                var key = Schluessel(tri[f * 3 + k], tri[f * 3 + (k + 1) % 3]);
                kanten[key] = kanten.TryGetValue(key, out var e) ? (e.A, e.N == 1 ? f : e.B, e.N + 1) : (f, -1, 1);
            }
        bool Gerichtet(int f, int a, int b)   // läuft Dreieck f die Kante a → b?
        {
            for (int k = 0; k < 3; k++) if (tri[f * 3 + k] == a && tri[f * 3 + (k + 1) % 3] == b) return true;
            return false;
        }
        void Wenden(int f) => (tri[f * 3 + 1], tri[f * 3 + 2]) = (tri[f * 3 + 2], tri[f * 3 + 1]);

        var besucht = new bool[m];
        var stueck = new List<int>();
        var schlange = new Queue<int>();
        for (int s = 0; s < m; s++)
        {
            if (besucht[s]) continue;
            besucht[s] = true;
            stueck.Clear();
            schlange.Enqueue(s);
            while (schlange.Count > 0)
            {
                int f = schlange.Dequeue();
                stueck.Add(f);
                for (int k = 0; k < 3; k++)
                {
                    int a = tri[f * 3 + k], b = tri[f * 3 + (k + 1) % 3];
                    var e = kanten[Schluessel(a, b)];
                    if (e.N != 2) continue;   // Rand oder mehr als zwei Dreiecke an der Kante: nicht über sie hinweg
                    int g = e.A == f ? e.B : e.A;
                    if (g < 0 || besucht[g]) continue;
                    if (Gerichtet(g, a, b)) Wenden(g);   // gleich durchlaufen heisst andersherum orientiert
                    besucht[g] = true;
                    schlange.Enqueue(g);
                }
            }
            // Volumen des Stücks (relativ zu seinem ersten Punkt, gegen Auslöschung bei grossen Koordinaten)
            int o = tri[stueck[0] * 3];
            double ox = punkte[o * 3], oy = punkte[o * 3 + 1], oz = punkte[o * 3 + 2], vol = 0;
            foreach (var f in stueck)
            {
                int a = tri[f * 3], b = tri[f * 3 + 1], c = tri[f * 3 + 2];
                double ax = punkte[a * 3] - ox, ay = punkte[a * 3 + 1] - oy, az = punkte[a * 3 + 2] - oz;
                double bx = punkte[b * 3] - ox, by = punkte[b * 3 + 1] - oy, bz = punkte[b * 3 + 2] - oz;
                double cx = punkte[c * 3] - ox, cy = punkte[c * 3 + 1] - oy, cz = punkte[c * 3 + 2] - oz;
                vol += ax * (by * cz - bz * cy) + ay * (bz * cx - bx * cz) + az * (bx * cy - by * cx);
            }
            if (vol < 0) foreach (var f in stueck) Wenden(f);
        }
    }
}
