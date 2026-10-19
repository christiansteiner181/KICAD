// Kollisionsprüfung (Modul 3b, B1): räumliche Grundfunktionen der Schmalphase in mm mit doppelter Genauigkeit –
// Hüllquader, nächste Punkte (Punkt–Segment, Punkt–Dreieck, Segment–Segment, Segment–Dreieck), Dreieck–Dreieck-Schnitt
// (Verfahren nach Möller 1997: Abstände zur Ebene des anderen Dreiecks, Schnittintervalle auf der Schnittgeraden) und
// -Abstand, Strahl–Dreieck (Möller–Trumbore), Quader–Dreieck (Trennachsen nach Akenine-Möller: 3 Quaderachsen, Normale,
// 9 Kantenkreuzprodukte) und endlicher Zylinder–Dreieck (Dreieck auf die Zylinderschicht beschnitten, dann Abstand zur
// Achse in der Querschnittsebene).
//
// Toleranz: eine benannte Konstante (Geometrie3.Toleranz, 0.1 mm). ANNAHME (technisch, keine Norm): die Netzdateien der
// IFC-Unterlagen speichern float32; bei Koordinaten bis 100 m beträgt die Auflösung rund 0.008 mm, nach Lagen und
// Abbildungen einige Hundertstel. Was näher als 0.1 mm liegt, gilt als Berührung, nicht als Durchdringung.

namespace Hlks.Kern;

/// <summary>Achsparalleler Hüllquader in mm.</summary>
public readonly record struct Huelle3(V3 Min, V3 Max)
{
    /// <summary>Leerer Hüllquader (neutral für <see cref="Mit(V3)"/>).</summary>
    public static readonly Huelle3 Leer = new(new V3(double.MaxValue, double.MaxValue, double.MaxValue), new V3(double.MinValue, double.MinValue, double.MinValue));

    public bool IstLeer => Min.X > Max.X || Min.Y > Max.Y || Min.Z > Max.Z;
    public V3 Mitte => (Min + Max) * 0.5;
    public V3 Groesse => Max - Min;

    public Huelle3 Mit(V3 p) => new(
        new V3(Math.Min(Min.X, p.X), Math.Min(Min.Y, p.Y), Math.Min(Min.Z, p.Z)),
        new V3(Math.Max(Max.X, p.X), Math.Max(Max.Y, p.Y), Math.Max(Max.Z, p.Z)));

    public Huelle3 Mit(Huelle3 b) => b.IstLeer ? this : IstLeer ? b : new(
        new V3(Math.Min(Min.X, b.Min.X), Math.Min(Min.Y, b.Min.Y), Math.Min(Min.Z, b.Min.Z)),
        new V3(Math.Max(Max.X, b.Max.X), Math.Max(Max.Y, b.Max.Y), Math.Max(Max.Z, b.Max.Z)));

    /// <summary>Um <paramref name="d"/> nach allen Seiten vergrössert.</summary>
    public Huelle3 Erweitert(double d) => IstLeer ? this : new(Min - new V3(d, d, d), Max + new V3(d, d, d));

    /// <summary>Überlappen sich die Quader, wenn jeder um <paramref name="abstand"/> wächst (achsweise, also eher zu viel als
    /// zu wenig – richtig für die Breitphase).</summary>
    public bool Schneidet(Huelle3 b, double abstand = 0) =>
        Min.X <= b.Max.X + abstand && b.Min.X <= Max.X + abstand &&
        Min.Y <= b.Max.Y + abstand && b.Min.Y <= Max.Y + abstand &&
        Min.Z <= b.Max.Z + abstand && b.Min.Z <= Max.Z + abstand;

    public bool Enthaelt(V3 p, double rand = 0) =>
        p.X >= Min.X - rand && p.X <= Max.X + rand && p.Y >= Min.Y - rand && p.Y <= Max.Y + rand && p.Z >= Min.Z - rand && p.Z <= Max.Z + rand;

    /// <summary>Liegt b ganz in diesem Quader?</summary>
    public bool Umfasst(Huelle3 b, double rand = 0) => Enthaelt(b.Min, rand) && Enthaelt(b.Max, rand);

    /// <summary>Euklidischer Abstand zweier Quader (0, wenn sie sich überlappen).</summary>
    public double Abstand(Huelle3 b)
    {
        double dx = Math.Max(0, Math.Max(b.Min.X - Max.X, Min.X - b.Max.X));
        double dy = Math.Max(0, Math.Max(b.Min.Y - Max.Y, Min.Y - b.Max.Y));
        double dz = Math.Max(0, Math.Max(b.Min.Z - Max.Z, Min.Z - b.Max.Z));
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>Abstand eines Punkts zum Quader (0 innen).</summary>
    public double Abstand(V3 p)
    {
        double dx = Math.Max(0, Math.Max(Min.X - p.X, p.X - Max.X));
        double dy = Math.Max(0, Math.Max(Min.Y - p.Y, p.Y - Max.Y));
        double dz = Math.Max(0, Math.Max(Min.Z - p.Z, p.Z - Max.Z));
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    public static Huelle3 Aus(IEnumerable<V3> punkte)
    {
        var h = Leer;
        foreach (var p in punkte) h = h.Mit(p);
        return h;
    }

    public static Huelle3 Aus(V3 a, V3 b) => Leer.Mit(a).Mit(b);
    public static Huelle3 Aus(V3 a, V3 b, V3 c) => Leer.Mit(a).Mit(b).Mit(c);

    /// <summary>[minx, miny, minz, maxx, maxy, maxz]</summary>
    public double[] Feld() => [Min.X, Min.Y, Min.Z, Max.X, Max.Y, Max.Z];
}

/// <summary>Lage zweier Dreiecke zueinander.</summary>
public enum DreieckLage
{
    /// <summary>Weiter als die Toleranz voneinander entfernt.</summary>
    Getrennt,
    /// <summary>Berührung innerhalb der Toleranz (Ecke oder Kante auf der Fläche, koplanar aufliegend) – keine Durchdringung.</summary>
    Beruehrt,
    /// <summary>Echte Durchdringung: jedes Dreieck durchstösst die Ebene des anderen, die Schnittstrecke ist länger als die Toleranz.</summary>
    Schneidet,
}

public static class Geometrie3
{
    /// <summary>Toleranz in mm: näher gilt als Berührung (siehe Dateikopf). Technischer Wert, keine Norm.</summary>
    public const double Toleranz = 0.1;

    /// <summary>Quadrat der kleinsten Länge, die noch als Strecke gilt (mm²).</summary>
    const double Winzig = 1e-18;

    // ------------------------------------------------------------------ Punkt

    /// <summary>Nächster Punkt der Strecke a–b zu p; t = Parameter (0 = a, 1 = b).</summary>
    public static V3 NaechsterPunktSegment(V3 p, V3 a, V3 b, out double t)
    {
        var ab = b - a;
        double l2 = ab.Mal(ab);
        t = l2 <= Winzig ? 0 : Math.Clamp((p - a).Mal(ab) / l2, 0, 1);
        return a + ab * t;
    }

    public static double AbstandPunktSegment(V3 p, V3 a, V3 b) => (p - NaechsterPunktSegment(p, a, b, out _)).Laenge;

    /// <summary>Nächster Punkt des Dreiecks abc zu p (Ericson, «Real-Time Collision Detection», Abschnitt 5.1.5).</summary>
    public static V3 NaechsterPunktDreieck(V3 p, V3 a, V3 b, V3 c)
    {
        var ab = b - a; var ac = c - a; var ap = p - a;
        double d1 = ab.Mal(ap), d2 = ac.Mal(ap);
        if (d1 <= 0 && d2 <= 0) return a;
        var bp = p - b;
        double d3 = ab.Mal(bp), d4 = ac.Mal(bp);
        if (d3 >= 0 && d4 <= d3) return b;
        double vc = d1 * d4 - d3 * d2;
        if (vc <= 0 && d1 >= 0 && d3 <= 0) return a + ab * (d1 / (d1 - d3));
        var cp = p - c;
        double d5 = ab.Mal(cp), d6 = ac.Mal(cp);
        if (d6 >= 0 && d5 <= d6) return c;
        double vb = d5 * d2 - d1 * d6;
        if (vb <= 0 && d2 >= 0 && d6 <= 0) return a + ac * (d2 / (d2 - d6));
        double va = d3 * d6 - d5 * d4;
        if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0) return b + (c - b) * ((d4 - d3) / (d4 - d3 + (d5 - d6)));
        double summe = va + vb + vc;
        if (Math.Abs(summe) <= 1e-300)   // entartetes Dreieck: nächster Punkt auf den Kanten
        {
            var k1 = NaechsterPunktSegment(p, a, b, out _); var k2 = NaechsterPunktSegment(p, b, c, out _); var k3 = NaechsterPunktSegment(p, c, a, out _);
            double l1 = (p - k1).Mal(p - k1), l2 = (p - k2).Mal(p - k2), l3 = (p - k3).Mal(p - k3);
            return l1 <= l2 && l1 <= l3 ? k1 : l2 <= l3 ? k2 : k3;
        }
        double v = vb / summe, w = vc / summe;
        return a + ab * v + ac * w;
    }

    // ------------------------------------------------------------------ Segment–Segment

    /// <summary>Nächste Punkte zweier Strecken p1–q1 und p2–q2 (Ericson 5.1.9). Rückgabe: Abstand; s, t = Parameter auf den
    /// Strecken, c1, c2 = die Punkte.</summary>
    public static double SegmentSegment(V3 p1, V3 q1, V3 p2, V3 q2, out double s, out double t, out V3 c1, out V3 c2)
    {
        var d1 = q1 - p1; var d2 = q2 - p2; var r = p1 - p2;
        double a = d1.Mal(d1), e = d2.Mal(d2), f = d2.Mal(r);
        if (a <= Winzig && e <= Winzig) { s = t = 0; c1 = p1; c2 = p2; return (c1 - c2).Laenge; }
        if (a <= Winzig) { s = 0; t = Math.Clamp(f / e, 0, 1); }
        else
        {
            double c = d1.Mal(r);
            if (e <= Winzig) { t = 0; s = Math.Clamp(-c / a, 0, 1); }
            else
            {
                double b = d1.Mal(d2), nenner = a * e - b * b;
                s = nenner > 1e-12 * a * e ? Math.Clamp((b * f - c * e) / nenner, 0, 1) : 0;
                t = (b * s + f) / e;
                if (t < 0) { t = 0; s = Math.Clamp(-c / a, 0, 1); }
                else if (t > 1) { t = 1; s = Math.Clamp((b - c) / a, 0, 1); }
            }
        }
        c1 = p1 + d1 * s; c2 = p2 + d2 * t;
        return (c1 - c2).Laenge;
    }

    // ------------------------------------------------------------------ Strahl / Segment – Dreieck

    /// <summary>Gerade o + t·r gegen Dreieck abc (Möller–Trumbore, ohne Rückseitenausschluss). false, wenn die Gerade
    /// (fast) parallel zur Dreiecksebene liegt; sonst t sowie die baryzentrischen Koordinaten u (zu b), v (zu c) – auch
    /// ausserhalb des Dreiecks, damit der Aufrufer Kantentreffer erkennen kann.</summary>
    public static bool GeradeDreieck(V3 o, V3 r, V3 a, V3 b, V3 c, out double t, out double u, out double v)
    {
        var e1 = b - a; var e2 = c - a;
        var h = r.Kreuz(e2);
        double det = e1.Mal(h);
        double mass = e1.Laenge * e2.Laenge * r.Laenge;
        t = u = v = 0;
        if (Math.Abs(det) <= 1e-12 * mass || mass == 0) return false;
        double inv = 1 / det;
        var s = o - a;
        u = inv * s.Mal(h);
        var q = s.Kreuz(e1);
        v = inv * r.Mal(q);
        t = inv * e2.Mal(q);
        return true;
    }

    /// <summary>Durchstösst die Strecke p–q das Dreieck abc (Rand eingeschlossen)? Parallele Lage zählt nicht (dafür
    /// <see cref="SegmentDreieck"/>). t = Parameter auf der Strecke.</summary>
    public static bool SegmentDreieckSchnitt(V3 p, V3 q, V3 a, V3 b, V3 c, out double t)
    {
        if (!GeradeDreieck(p, q - p, a, b, c, out t, out var u, out var v)) return false;
        const double e = 1e-12;
        return u >= -e && v >= -e && u + v <= 1 + e && t >= -e && t <= 1 + e;
    }

    /// <summary>Kleinster Abstand der Strecke p–q zum Dreieck abc mit den nächsten Punkten; t = Parameter auf der Strecke.
    /// Schneidet die Strecke das Dreieck, ist der Abstand 0. Sonst liegt das Minimum an einem Streckenende (gegen das
    /// Dreieck) oder zwischen der Strecke und einer Dreieckskante.</summary>
    public static double SegmentDreieck(V3 p, V3 q, V3 a, V3 b, V3 c, out V3 aufSegment, out V3 aufDreieck, out double t)
    {
        if (SegmentDreieckSchnitt(p, q, a, b, c, out t))
        {
            t = Math.Clamp(t, 0, 1);
            aufSegment = aufDreieck = p + (q - p) * t;
            return 0;
        }
        var kp = NaechsterPunktDreieck(p, a, b, c);
        double best = (p - kp).Laenge; V3 ps = p, pd = kp; double ts = 0;
        var kq = NaechsterPunktDreieck(q, a, b, c);
        double dq = (q - kq).Laenge;
        if (dq < best) { best = dq; ps = q; pd = kq; ts = 1; }
        Kante(a, b); Kante(b, c); Kante(c, a);
        aufSegment = ps; aufDreieck = pd; t = ts;
        return best;

        void Kante(V3 x, V3 y)
        {
            double d = SegmentSegment(p, q, x, y, out var s, out _, out var c1, out var c2);
            if (d < best) { best = d; ps = c1; pd = c2; ts = s; }
        }
    }

    // ------------------------------------------------------------------ Dreieck – Dreieck

    /// <summary>Kleinster Abstand zweier Dreiecke mit den nächsten Punkten: das Minimum der sechs Abstände Kante–Dreieck
    /// (für sich schneidende Dreiecke trifft mindestens eine Kante das andere Dreieck: Abstand 0).</summary>
    public static double DreieckDreieckAbstand(V3 a0, V3 a1, V3 a2, V3 b0, V3 b1, V3 b2, out V3 pa, out V3 pb)
    {
        double best = double.PositiveInfinity; V3 xa = a0, xb = b0;
        void KanteA(V3 x, V3 y)
        {
            double d = SegmentDreieck(x, y, b0, b1, b2, out var s, out var r, out _);
            if (d < best) { best = d; xa = s; xb = r; }
        }
        void KanteB(V3 x, V3 y)
        {
            double d = SegmentDreieck(x, y, a0, a1, a2, out var s, out var r, out _);
            if (d < best) { best = d; xa = r; xb = s; }
        }
        KanteA(a0, a1); KanteA(a1, a2); KanteA(a2, a0);
        if (best > 0) { KanteB(b0, b1); KanteB(b1, b2); KanteB(b2, b0); }
        pa = xa; pb = xb;
        return best;
    }

    /// <summary>Dreieck–Dreieck-Schnitt nach Möller (1997) mit Toleranz: Abstände der Ecken zur Ebene des anderen Dreiecks
    /// (näher als <paramref name="tol"/> = in der Ebene), Schnittstrecke jedes Dreiecks mit der anderen Ebene, Vergleich der
    /// beiden Strecken auf der Schnittgeraden. Koplanar (innerhalb der Toleranz) ist höchstens Berührung.</summary>
    public static DreieckLage DreieckDreieck(V3 a0, V3 a1, V3 a2, V3 b0, V3 b1, V3 b2, double tol = Toleranz) =>
        DreieckDreieck(a0, a1, a2, b0, b1, b2, tol, out _, out _);

    /// <summary>Wie <see cref="DreieckDreieck(V3, V3, V3, V3, V3, V3, double)"/>; bei Schnitt bzw. Berührung zusätzlich die
    /// gemeinsame Strecke (s0, s1) auf der Schnittgeraden (bei koplanarer Lage zwei nächste Punkte).</summary>
    public static DreieckLage DreieckDreieck(V3 a0, V3 a1, V3 a2, V3 b0, V3 b1, V3 b2, double tol, out V3 s0, out V3 s1)
    {
        s0 = s1 = a0;
        var na = (a1 - a0).Kreuz(a2 - a0);
        var nb = (b1 - b0).Kreuz(b2 - b0);
        double la = na.Laenge, lb = nb.Laenge;
        if (la <= 1e-300 || lb <= 1e-300) return NachAbstand(a0, a1, a2, b0, b1, b2, tol, out s0, out s1);   // entartet
        na *= 1 / la; nb *= 1 / lb;

        // Abstände der Ecken von b zur Ebene von a
        double db0 = Null(na.Mal(b0 - a0)), db1 = Null(na.Mal(b1 - a0)), db2 = Null(na.Mal(b2 - a0));
        if ((db0 > 0 && db1 > 0 && db2 > 0) || (db0 < 0 && db1 < 0 && db2 < 0)) return DreieckLage.Getrennt;
        double da0 = Null(nb.Mal(a0 - b0)), da1 = Null(nb.Mal(a1 - b0)), da2 = Null(nb.Mal(a2 - b0));
        if ((da0 > 0 && da1 > 0 && da2 > 0) || (da0 < 0 && da1 < 0 && da2 < 0)) return DreieckLage.Getrennt;

        if (db0 == 0 && db1 == 0 && db2 == 0)   // koplanar: höchstens Berührung
            return NachAbstand(a0, a1, a2, b0, b1, b2, tol, out s0, out s1);

        var richtung = na.Kreuz(nb);
        double lr = richtung.Laenge;
        if (lr <= 1e-9) return NachAbstand(a0, a1, a2, b0, b1, b2, tol, out s0, out s1);   // fast parallel
        richtung *= 1 / lr;

        if (!Strecke(a0, a1, a2, da0, da1, da2, out var pa0, out var pa1)) return DreieckLage.Getrennt;
        if (!Strecke(b0, b1, b2, db0, db1, db2, out var pb0, out var pb1)) return DreieckLage.Getrennt;
        double ta0 = richtung.Mal(pa0), ta1 = richtung.Mal(pa1), tb0 = richtung.Mal(pb0), tb1 = richtung.Mal(pb1);
        if (ta0 > ta1) { (ta0, ta1) = (ta1, ta0); (pa0, pa1) = (pa1, pa0); }
        if (tb0 > tb1) { (tb0, tb1) = (tb1, tb0); (pb0, pb1) = (pb1, pb0); }
        double von = Math.Max(ta0, tb0), bis = Math.Min(ta1, tb1);
        if (bis - von < -tol) return DreieckLage.Getrennt;
        s0 = ta0 >= tb0 ? pa0 : pb0;
        s1 = ta1 <= tb1 ? pa1 : pb1;
        bool durchA = (da0 > 0 || da1 > 0 || da2 > 0) && (da0 < 0 || da1 < 0 || da2 < 0);
        bool durchB = (db0 > 0 || db1 > 0 || db2 > 0) && (db0 < 0 || db1 < 0 || db2 < 0);
        if (durchA && durchB && bis - von > tol) return DreieckLage.Schneidet;
        // Berührung nachprüfen: eine Ecke «in der Ebene» (näher als tol) liegt bei fast parallelen Ebenen nicht zwingend nahe
        // am anderen Dreieck – der Vergleich auf der Schnittgeraden allein sagt das nicht
        return NachAbstand(a0, a1, a2, b0, b1, b2, tol, out s0, out s1);

        double Null(double d) => Math.Abs(d) < tol ? 0 : d;
    }

    /// <summary>Strecke, in der das Dreieck p0p1p2 (Ebenenabstände d0..d2, mindestens ein Vorzeichenwechsel oder eine Null)
    /// die andere Ebene trifft: Ecken in der Ebene und Durchstosspunkte der Kanten.</summary>
    static bool Strecke(V3 p0, V3 p1, V3 p2, double d0, double d1, double d2, out V3 s0, out V3 s1)
    {
        V3 x0 = p0, x1 = p0;
        int n = 0;
        void Dazu(V3 x) { if (n == 0) x0 = x; else if (n == 1) x1 = x; n++; }
        if (d0 == 0) Dazu(p0);
        if (d1 == 0) Dazu(p1);
        if (d2 == 0) Dazu(p2);
        if (d0 * d1 < 0) Dazu(p0 + (p1 - p0) * (d0 / (d0 - d1)));
        if (d1 * d2 < 0) Dazu(p1 + (p2 - p1) * (d1 / (d1 - d2)));
        if (d2 * d0 < 0) Dazu(p2 + (p0 - p2) * (d2 / (d2 - d0)));
        s0 = x0; s1 = n > 1 ? x1 : x0;
        return n > 0;
    }

    static DreieckLage NachAbstand(V3 a0, V3 a1, V3 a2, V3 b0, V3 b1, V3 b2, double tol, out V3 s0, out V3 s1) =>
        DreieckDreieckAbstand(a0, a1, a2, b0, b1, b2, out s0, out s1) <= tol ? DreieckLage.Beruehrt : DreieckLage.Getrennt;

    // ------------------------------------------------------------------ Quader – Dreieck

    /// <summary>Überlappen sich ein gedrehter Quader (Mitte, orthonormale Achsen ax, ay, az, halbe Kantenlängen hx, hy, hz)
    /// und das Dreieck abc? Trennachsen nach Akenine-Möller (2001): drei Quaderachsen, Dreiecksnormale, neun Kreuzprodukte.
    /// Berührung zählt als Überlappung; für «echte» Durchdringung den Quader um die Toleranz verkleinern.</summary>
    public static bool QuaderDreieck(V3 mitte, V3 ax, V3 ay, V3 az, double hx, double hy, double hz, V3 a, V3 b, V3 c)
    {
        if (hx < 0 || hy < 0 || hz < 0) return false;
        var pa = a - mitte; var pb = b - mitte; var pc = c - mitte;
        var v0 = new V3(pa.Mal(ax), pa.Mal(ay), pa.Mal(az));
        var v1 = new V3(pb.Mal(ax), pb.Mal(ay), pb.Mal(az));
        var v2 = new V3(pc.Mal(ax), pc.Mal(ay), pc.Mal(az));
        // Quaderachsen
        if (Math.Max(v0.X, Math.Max(v1.X, v2.X)) < -hx || Math.Min(v0.X, Math.Min(v1.X, v2.X)) > hx) return false;
        if (Math.Max(v0.Y, Math.Max(v1.Y, v2.Y)) < -hy || Math.Min(v0.Y, Math.Min(v1.Y, v2.Y)) > hy) return false;
        if (Math.Max(v0.Z, Math.Max(v1.Z, v2.Z)) < -hz || Math.Min(v0.Z, Math.Min(v1.Z, v2.Z)) > hz) return false;
        var e0 = v1 - v0; var e1 = v2 - v1; var e2 = v0 - v2;
        // Normale
        var n = e0.Kreuz(e1);
        double rn = hx * Math.Abs(n.X) + hy * Math.Abs(n.Y) + hz * Math.Abs(n.Z);
        if (Math.Abs(n.Mal(v0)) > rn) return false;
        // Kantenkreuzprodukte mit den Achsen (1, 0, 0), (0, 1, 0), (0, 0, 1)
        Span<V3> kanten = [e0, e1, e2];
        foreach (var e in kanten)
        {
            if (!Achse(new V3(0, -e.Z, e.Y))) return false;   // x × e
            if (!Achse(new V3(e.Z, 0, -e.X))) return false;   // y × e
            if (!Achse(new V3(-e.Y, e.X, 0))) return false;   // z × e
        }
        return true;

        bool Achse(V3 l)
        {
            double p0 = l.Mal(v0), p1 = l.Mal(v1), p2 = l.Mal(v2);
            double r = hx * Math.Abs(l.X) + hy * Math.Abs(l.Y) + hz * Math.Abs(l.Z);
            return !(Math.Min(p0, Math.Min(p1, p2)) > r || Math.Max(p0, Math.Max(p1, p2)) < -r);
        }
    }

    // ------------------------------------------------------------------ Zylinder – Dreieck

    /// <summary>Überlappen sich der endliche Zylinder (Achse p–q, Radius r, ebene Enden) und das Dreieck abc? Das Dreieck wird
    /// auf die Schicht zwischen den Endflächen beschnitten; das Reststück wird in die Querschnittsebene projiziert und mit
    /// dem Kreis um die Achse verglichen (exakt).</summary>
    public static bool ZylinderDreieck(V3 p, V3 q, double r, V3 a, V3 b, V3 c)
    {
        var achse = q - p;
        double l = achse.Laenge;
        if (l <= 1e-9 || r <= 0) return false;
        var d = achse * (1 / l);
        Span<V3> poly = stackalloc V3[8];
        Span<V3> tmp = stackalloc V3[8];
        poly[0] = a; poly[1] = b; poly[2] = c;
        int n = Beschneiden(poly, 3, tmp, x => d.Mal(x - p));            // ≥ 0: vor der Endfläche bei p
        n = Beschneiden(tmp, n, poly, x => l - d.Mal(x - p));            // ≥ 0: vor der Endfläche bei q
        if (n == 0) return false;
        var (e1, e2) = d.Senkrechte();
        Span<double> u = stackalloc double[8];
        Span<double> v = stackalloc double[8];
        for (int i = 0; i < n; i++) { var w = poly[i] - p; u[i] = w.Mal(e1); v[i] = w.Mal(e2); }
        // Ursprung (Achse) im projizierten konvexen Vieleck?
        if (n >= 3)
        {
            int vorzeichen = 0; bool innen = true;
            for (int i = 0; i < n && innen; i++)
            {
                int j = (i + 1) % n;
                double k = u[i] * v[j] - v[i] * u[j];   // Kreuzprodukt (P_i − 0) × (P_j − 0) in 2D
                if (Math.Abs(k) < 1e-12) continue;
                int s = Math.Sign(k);
                if (vorzeichen == 0) vorzeichen = s; else if (s != vorzeichen) innen = false;
            }
            if (innen && vorzeichen != 0) return true;
        }
        double best = double.PositiveInfinity;
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            best = Math.Min(best, Abstand2(u[i], v[i], u[j], v[j]));
        }
        return best <= r;

        static double Abstand2(double x0, double y0, double x1, double y1)
        {
            double dx = x1 - x0, dy = y1 - y0, l2 = dx * dx + dy * dy;
            double t = l2 <= 1e-24 ? 0 : Math.Clamp(-(x0 * dx + y0 * dy) / l2, 0, 1);
            double x = x0 + t * dx, y = y0 + t * dy;
            return Math.Sqrt(x * x + y * y);
        }
    }

    /// <summary>Sutherland–Hodgman gegen eine Halbebene (seite ≥ 0 bleibt).</summary>
    static int Beschneiden(Span<V3> ein, int n, Span<V3> aus, Func<V3, double> seite)
    {
        int m = 0;
        for (int i = 0; i < n; i++)
        {
            var x = ein[i]; var y = ein[(i + 1) % n];
            double sx = seite(x), sy = seite(y);
            if (sx >= 0 && m < aus.Length) aus[m++] = x;
            if ((sx >= 0) != (sy >= 0) && m < aus.Length) aus[m++] = x + (y - x) * (sx / (sx - sy));
        }
        return m;
    }
}
