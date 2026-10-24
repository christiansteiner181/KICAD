// Quest App V1d (Befund Brillentest V1 24.10.2026: Fang beim Messen bzw. Zeigen 4 057 Abfragen, im Mittel 12 ms, einmal 1 013 ms auf der Quest): räumlicher Index
// für den Fang – eine Hüllkörper-Hierarchie (BVH) über achsenparallele Boxen, einmal je Modell im Hintergrund gebaut (SzenenFang.Aufbauen). Rein, ohne StereoKit.
//   - Bau: Teilung am Median der Mittelpunkte entlang der längsten Achse, bis höchstens BlattMax Einträge je Blatt (O(n log n)).
//   - Abfragen ohne Zuteilung von Speicher je Abfrage (eigener Stapel), nur im Bild-Thread: Strahl (Strecke [0, tMax], Boxen um «dehnung» vergrössert; die Knoten
//     in der Reihenfolge des Eintritts, so dass ein näherer Treffer die weiteren abschneidet) und Umkreis (Abstand Punkt–Box).
// Die Rangfolge des Fangs (Gleichstände) regelt SzenenFang über eine feste Nummer je Eintrag, nicht die Reihenfolge des Baums.

namespace Hlks.Xr.Messen;

/// <summary>Hüllkörper-Hierarchie über Boxen (Einträge 0 … n − 1). Unveränderlich nach dem Bau; Abfragen nur aus einem Thread.</summary>
public sealed class BoxBaum
{
    /// <summary>Höchstens so viele Einträge je Blatt.</summary>
    public const int BlattMax = 4;

    readonly double[] min, max;          // je Knoten 3 Werte
    readonly int[] links, rechts;        // Kinder (−1 = Blatt)
    readonly int[] ab, anzahl;           // Blatt: Einträge [ab, ab + anzahl) in reihe
    readonly int[] reihe;                // Einträge in der Reihenfolge der Blätter
    int knoten;
    int[] stapel = new int[64];
    double[] stapelT = new double[64];

    /// <summary>Zahl der Einträge bzw. der Knoten.</summary>
    public int Eintraege => reihe.Length;
    public int Knoten => knoten;

    /// <summary>Baut den Baum über die Boxen (Box3.Leer zählt nicht mit und wird nie gefunden).</summary>
    public BoxBaum(IReadOnlyList<Box3> boxen)
    {
        var gueltig = new List<int>(boxen.Count);
        for (int i = 0; i < boxen.Count; i++) if (!boxen[i].IstLeer) gueltig.Add(i);
        var n = gueltig.Count;
        var cap = Math.Max(1, 2 * n);
        min = new double[3 * cap]; max = new double[3 * cap];
        links = new int[cap]; rechts = new int[cap]; ab = new int[cap]; anzahl = new int[cap];
        reihe = [.. gueltig];
        var mitte = new double[3 * boxen.Count];
        foreach (var i in gueltig)
            for (int k = 0; k < 3; k++) mitte[3 * i + k] = (boxen[i].Min.Komp(k) + boxen[i].Max.Komp(k)) / 2;
        if (n == 0) { knoten = 0; return; }
        Bauen(boxen, mitte, 0, n);
    }

    int Bauen(IReadOnlyList<Box3> boxen, double[] mitte, int von, int bis)
    {
        var k = knoten++;
        double x0 = double.PositiveInfinity, y0 = x0, z0 = x0, x1 = double.NegativeInfinity, y1 = x1, z1 = x1;
        double c0x = x0, c0y = x0, c0z = x0, c1x = x1, c1y = x1, c1z = x1;
        for (int i = von; i < bis; i++)
        {
            var b = boxen[reihe[i]];
            x0 = Math.Min(x0, b.Min.X); y0 = Math.Min(y0, b.Min.Y); z0 = Math.Min(z0, b.Min.Z);
            x1 = Math.Max(x1, b.Max.X); y1 = Math.Max(y1, b.Max.Y); z1 = Math.Max(z1, b.Max.Z);
            var m = 3 * reihe[i];
            c0x = Math.Min(c0x, mitte[m]); c0y = Math.Min(c0y, mitte[m + 1]); c0z = Math.Min(c0z, mitte[m + 2]);
            c1x = Math.Max(c1x, mitte[m]); c1y = Math.Max(c1y, mitte[m + 1]); c1z = Math.Max(c1z, mitte[m + 2]);
        }
        min[3 * k] = x0; min[3 * k + 1] = y0; min[3 * k + 2] = z0;
        max[3 * k] = x1; max[3 * k + 1] = y1; max[3 * k + 2] = z1;
        if (bis - von <= BlattMax)
        {
            links[k] = rechts[k] = -1;
            ab[k] = von; anzahl[k] = bis - von;
            return k;
        }
        // längste Achse der Mittelpunkte, Median
        double ex = c1x - c0x, ey = c1y - c0y, ez = c1z - c0z;
        var achse = ex >= ey && ex >= ez ? 0 : ey >= ez ? 1 : 2;
        var mid = (von + bis) / 2;
        NthElement(mitte, achse, von, bis - 1, mid);
        links[k] = Bauen(boxen, mitte, von, mid);
        rechts[k] = Bauen(boxen, mitte, mid, bis);
        ab[k] = anzahl[k] = 0;
        return k;
    }

    /// <summary>Teilsortieren (Quickselect, Hoare): danach liegt an Stelle n der Eintrag, der dort sortiert stünde; links kleiner gleich, rechts grösser gleich.</summary>
    void NthElement(double[] mitte, int achse, int lo, int hi, int n)
    {
        while (lo < hi)
        {
            var pivot = mitte[3 * reihe[(lo + hi) >> 1] + achse];
            int i = lo, j = hi;
            while (i <= j)
            {
                while (mitte[3 * reihe[i] + achse] < pivot) i++;
                while (mitte[3 * reihe[j] + achse] > pivot) j--;
                if (i <= j) { (reihe[i], reihe[j]) = (reihe[j], reihe[i]); i++; j--; }
            }
            if (n <= j) hi = j;
            else if (n >= i) lo = i;
            else return;
        }
    }

    /// <summary>Eintritt des Strahls (u, Richtung r) in die Box des Knotens k, um dehnung vergrössert, auf [0, tMax]; NaN ohne Schnitt.</summary>
    double Eintritt(int k, in Vec3 u, in Vec3 invR, double dehnung, double tMax)
    {
        double t0 = 0, t1 = tMax;
        for (int a = 0; a < 3; a++)
        {
            double o = a == 0 ? u.X : a == 1 ? u.Y : u.Z, inv = a == 0 ? invR.X : a == 1 ? invR.Y : invR.Z;
            double lo = min[3 * k + a] - dehnung, hi = max[3 * k + a] + dehnung;
            if (double.IsInfinity(inv))
            {
                if (o < lo || o > hi) return double.NaN;
                continue;
            }
            double ta = (lo - o) * inv, tb = (hi - o) * inv;
            if (ta > tb) (ta, tb) = (tb, ta);
            if (ta > t0) t0 = ta;
            if (tb < t1) t1 = tb;
            if (t0 > t1) return double.NaN;
        }
        return t0;
    }

    void Platz(int n)
    {
        if (n < stapel.Length) return;
        Array.Resize(ref stapel, stapel.Length * 2);
        Array.Resize(ref stapelT, stapelT.Length * 2);
    }

    /// <summary>
    /// Strahl u + t·r (r Einheit) auf [0, tMax]: ruft besuch(eintrag, tEintrittKnoten) für jeden Eintrag eines Blatts, dessen Box (um dehnung vergrössert) der Strahl
    /// trifft, nahe Knoten zuerst. besuch gibt das neue tMax zurück (kleiner = ein Treffer schneidet die weiteren ab; double.PositiveInfinity = weiter wie bisher).
    /// </summary>
    public void Strahl(Vec3 u, Vec3 r, double tMax, double dehnung, Func<int, double, double> besuch)
    {
        if (knoten == 0) return;
        var invR = new Vec3(Math.Abs(r.X) < 1e-12 ? double.PositiveInfinity : 1 / r.X, Math.Abs(r.Y) < 1e-12 ? double.PositiveInfinity : 1 / r.Y,
            Math.Abs(r.Z) < 1e-12 ? double.PositiveInfinity : 1 / r.Z);
        var t0 = Eintritt(0, u, invR, dehnung, tMax);
        if (double.IsNaN(t0)) return;
        int sp = 0;
        stapel[sp] = 0; stapelT[sp++] = t0;
        while (sp > 0)
        {
            sp--;
            var k = stapel[sp];
            if (stapelT[sp] > tMax) continue;
            if (links[k] < 0)
            {
                for (int i = ab[k]; i < ab[k] + anzahl[k]; i++)
                {
                    var neu = besuch(reihe[i], stapelT[sp]);
                    if (neu < tMax) tMax = neu;
                }
                continue;
            }
            double ta = Eintritt(links[k], u, invR, dehnung, tMax), tb = Eintritt(rechts[k], u, invR, dehnung, tMax);
            Platz(sp + 2);
            // der fernere zuerst auf den Stapel: der nähere wird zuerst besucht
            if (!double.IsNaN(ta) && !double.IsNaN(tb))
            {
                if (ta <= tb) { stapel[sp] = rechts[k]; stapelT[sp++] = tb; stapel[sp] = links[k]; stapelT[sp++] = ta; }
                else { stapel[sp] = links[k]; stapelT[sp++] = ta; stapel[sp] = rechts[k]; stapelT[sp++] = tb; }
            }
            else if (!double.IsNaN(ta)) { stapel[sp] = links[k]; stapelT[sp++] = ta; }
            else if (!double.IsNaN(tb)) { stapel[sp] = rechts[k]; stapelT[sp++] = tb; }
        }
    }

    /// <summary>
    /// Strahl u + t·r (r Einheit) auf [0, tMax] mit schrumpfender Dehnung: ruft besuch(eintrag) für jeden Eintrag eines Blatts, dessen Box (um dehnung vergrössert) der
    /// Strahl trifft. besuch gibt die neue Dehnung zurück (kleiner = nur noch Einträge, die so nah am Strahl liegen können; double.PositiveInfinity = wie bisher). Die
    /// Knoten werden beim Besuch mit der Dehnung von dann geprüft, nahe Knoten zuerst.
    /// </summary>
    public void StrahlNahe(Vec3 u, Vec3 r, double tMax, double dehnung, Func<int, double> besuch, double kegel0 = double.PositiveInfinity, double kegelJe = 0)
    {
        if (knoten == 0) return;
        var invR = new Vec3(Math.Abs(r.X) < 1e-12 ? double.PositiveInfinity : 1 / r.X, Math.Abs(r.Y) < 1e-12 ? double.PositiveInfinity : 1 / r.Y,
            Math.Abs(r.Z) < 1e-12 ? double.PositiveInfinity : 1 / r.Z);
        // Kegel (optional): ein Eintrag zählt nur bis kegel0 + kegelJe × t vom Strahl – für einen Knoten genügt die Dehnung bei seinem fernsten t
        double D(int k)
        {
            if (double.IsPositiveInfinity(kegel0)) return dehnung;
            double c = 0, h = 0;
            for (int a = 0; a < 3; a++)
            {
                var ra = a == 0 ? r.X : a == 1 ? r.Y : r.Z;
                var ua = a == 0 ? u.X : a == 1 ? u.Y : u.Z;
                c += ((min[3 * k + a] + max[3 * k + a]) / 2 - ua) * ra;
                h += (max[3 * k + a] - min[3 * k + a]) / 2 * Math.Abs(ra);
            }
            return Math.Min(dehnung, kegel0 + kegelJe * Math.Max(0, Math.Min(tMax, c + h)));
        }
        if (double.IsNaN(Eintritt(0, u, invR, D(0), tMax))) return;
        int sp = 0;
        stapel[sp++] = 0;
        while (sp > 0)
        {
            var k = stapel[--sp];
            if (double.IsNaN(Eintritt(k, u, invR, D(k), tMax))) continue;   // mit der Dehnung von jetzt
            if (links[k] < 0)
            {
                for (int i = ab[k]; i < ab[k] + anzahl[k]; i++)
                {
                    var neu = besuch(reihe[i]);
                    if (neu < dehnung) dehnung = neu;
                }
                continue;
            }
            double ta = Eintritt(links[k], u, invR, D(links[k]), tMax), tb = Eintritt(rechts[k], u, invR, D(rechts[k]), tMax);
            Platz(sp + 2);
            if (!double.IsNaN(ta) && !double.IsNaN(tb))
            {
                if (ta <= tb) { stapel[sp++] = rechts[k]; stapel[sp++] = links[k]; }
                else { stapel[sp++] = links[k]; stapel[sp++] = rechts[k]; }
            }
            else if (!double.IsNaN(ta)) stapel[sp++] = links[k];
            else if (!double.IsNaN(tb)) stapel[sp++] = rechts[k];
        }
    }

    double Abstand(int k, in Vec3 p)
    {
        double dx = Math.Max(0, Math.Max(min[3 * k] - p.X, p.X - max[3 * k]));
        double dy = Math.Max(0, Math.Max(min[3 * k + 1] - p.Y, p.Y - max[3 * k + 1]));
        double dz = Math.Max(0, Math.Max(min[3 * k + 2] - p.Z, p.Z - max[3 * k + 2]));
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>
    /// Umkreis: ruft besuch(eintrag) für jeden Eintrag eines Blatts, dessen Box höchstens radius von p entfernt ist. besuch gibt den neuen Radius zurück (kleiner =
    /// enger suchen; double.PositiveInfinity = wie bisher).
    /// </summary>
    public void Nahe(Vec3 p, double radius, Func<int, double> besuch)
    {
        if (knoten == 0 || Abstand(0, p) > radius) return;
        int sp = 0;
        stapel[sp++] = 0;
        while (sp > 0)
        {
            var k = stapel[--sp];
            if (Abstand(k, p) > radius) continue;
            if (links[k] < 0)
            {
                for (int i = ab[k]; i < ab[k] + anzahl[k]; i++)
                {
                    var neu = besuch(reihe[i]);
                    if (neu < radius) radius = neu;
                }
                continue;
            }
            // der nähere zuerst (er wird zuletzt auf den Stapel gelegt): ein guter Treffer schrumpft den Umkreis früh
            Platz(sp + 2);
            double dl = Abstand(links[k], p), dr = Abstand(rechts[k], p);
            if (dl <= dr) { if (dr <= radius) stapel[sp++] = rechts[k]; if (dl <= radius) stapel[sp++] = links[k]; }
            else { if (dl <= radius) stapel[sp++] = links[k]; if (dr <= radius) stapel[sp++] = rechts[k]; }
        }
    }
}
