// Kollisionsprüfung (Modul 3b, B1): räumlicher Index – ein statisch gebauter Hüllquader-Baum (BVH) über Hüllquader.
// Gebaut von oben nach unten: je Knoten die längste Achse der Mittelpunkte, Teilung am Median (Auswahl in linearer Zeit),
// Blätter mit höchstens vier Einträgen. Abfragen: Kandidaten zu einem Quader (+ Abstand), Strahl/Strecke, nächster Eintrag
// (Verzweigen und Begrenzen), Paare zwischen zwei Bäumen und innerhalb eines Baums. Der Baum ändert sich nach dem Bau
// nicht und darf von mehreren Fäden gleichzeitig gelesen werden.
// Derselbe Baum dient den Prüfkörpern (Breitphase) und den Dreiecken eines Netzes (Schmalphase, Pruefnetz).

namespace Hlks.Kern;

/// <summary>Hüllquader-Baum über Einträge 0 … Anzahl − 1 (Indizes in die Liste der Hüllquader beim Bau).</summary>
public sealed class Raumindex
{
    /// <summary>Höchstzahl Einträge je Blatt.</summary>
    public const int Blatt = 4;

    readonly double[] emin, emax;      // Hüllquader der Einträge, je 3 Werte
    readonly int[] folge;              // Einträge in Baumreihenfolge
    double[] kmin = [], kmax = [];     // Hüllquader der Knoten
    int[] kind = [], erster = [], anzahl = [];   // innen: kind = linkes Kind (rechtes = kind + 1); Blatt: erster, anzahl > 0
    int knoten;

    public int Anzahl => folge.Length;
    public int Knoten => knoten;

    public Raumindex(IReadOnlyList<Huelle3> huellen)
    {
        int n = huellen.Count;
        emin = new double[3 * n]; emax = new double[3 * n];
        var mitte = new double[3 * n];
        folge = new int[n];
        for (int i = 0; i < n; i++)
        {
            var h = huellen[i];
            emin[3 * i] = h.Min.X; emin[3 * i + 1] = h.Min.Y; emin[3 * i + 2] = h.Min.Z;
            emax[3 * i] = h.Max.X; emax[3 * i + 1] = h.Max.Y; emax[3 * i + 2] = h.Max.Z;
            for (int k = 0; k < 3; k++) mitte[3 * i + k] = (emin[3 * i + k] + emax[3 * i + k]) / 2;
            folge[i] = i;
        }
        int platz = Math.Max(1, 2 * ((n + Blatt - 1) / Blatt) + 1);
        kmin = new double[3 * platz]; kmax = new double[3 * platz];
        kind = new int[platz]; erster = new int[platz]; anzahl = new int[platz];
        if (n == 0) { knoten = 0; return; }
        knoten = 1;
        Bauen(0, 0, n, mitte);
    }

    int Neu()
    {
        if (knoten == kind.Length)
        {
            int neu = kind.Length * 2;
            Array.Resize(ref kmin, 3 * neu); Array.Resize(ref kmax, 3 * neu);
            Array.Resize(ref kind, neu); Array.Resize(ref erster, neu); Array.Resize(ref anzahl, neu);
        }
        return knoten++;
    }

    void Bauen(int k, int von, int bis, double[] mitte)
    {
        double x0 = double.MaxValue, y0 = double.MaxValue, z0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue, z1 = double.MinValue;
        double cx0 = double.MaxValue, cy0 = double.MaxValue, cz0 = double.MaxValue, cx1 = double.MinValue, cy1 = double.MinValue, cz1 = double.MinValue;
        for (int i = von; i < bis; i++)
        {
            int e = folge[i];
            x0 = Math.Min(x0, emin[3 * e]); y0 = Math.Min(y0, emin[3 * e + 1]); z0 = Math.Min(z0, emin[3 * e + 2]);
            x1 = Math.Max(x1, emax[3 * e]); y1 = Math.Max(y1, emax[3 * e + 1]); z1 = Math.Max(z1, emax[3 * e + 2]);
            cx0 = Math.Min(cx0, mitte[3 * e]); cy0 = Math.Min(cy0, mitte[3 * e + 1]); cz0 = Math.Min(cz0, mitte[3 * e + 2]);
            cx1 = Math.Max(cx1, mitte[3 * e]); cy1 = Math.Max(cy1, mitte[3 * e + 1]); cz1 = Math.Max(cz1, mitte[3 * e + 2]);
        }
        kmin[3 * k] = x0; kmin[3 * k + 1] = y0; kmin[3 * k + 2] = z0;
        kmax[3 * k] = x1; kmax[3 * k + 1] = y1; kmax[3 * k + 2] = z1;
        int n = bis - von;
        if (n <= Blatt) { erster[k] = von; anzahl[k] = n; return; }
        double ex = cx1 - cx0, ey = cy1 - cy0, ez = cz1 - cz0;
        int achse = ex >= ey && ex >= ez ? 0 : ey >= ez ? 1 : 2;
        int mittel = von + n / 2;
        Auswaehlen(von, bis - 1, mittel, achse, mitte);   // gleiche Mittelpunkte: Teilung nach Reihenfolge, auch so endlich
        int links = Neu(); int rechts = Neu();
        kind[k] = links; anzahl[k] = 0;
        _ = rechts;
        Bauen(links, von, mittel, mitte);
        Bauen(links + 1, mittel, bis, mitte);
    }

    /// <summary>Quickselect: folge[von..bis] so umordnen, dass an Stelle m der m-kleinste Mittelpunkt (Achse) steht.</summary>
    void Auswaehlen(int von, int bis, int m, int achse, double[] mitte)
    {
        while (bis > von)
        {
            // Median aus drei als Pivot
            int mid = von + (bis - von) / 2;
            double a = mitte[3 * folge[von] + achse], b = mitte[3 * folge[mid] + achse], c = mitte[3 * folge[bis] + achse];
            double pivot = a < b ? (b < c ? b : a < c ? c : a) : (a < c ? a : b < c ? c : b);
            int i = von, j = bis;
            while (i <= j)
            {
                while (mitte[3 * folge[i] + achse] < pivot) i++;
                while (mitte[3 * folge[j] + achse] > pivot) j--;
                if (i <= j) { (folge[i], folge[j]) = (folge[j], folge[i]); i++; j--; }
            }
            if (m <= j) bis = j;
            else if (m >= i) von = i;
            else return;
        }
    }

    bool Blattknoten(int k) => anzahl[k] > 0;

    bool KnotenSchneidet(int k, Huelle3 q, double a) =>
        kmin[3 * k] <= q.Max.X + a && q.Min.X <= kmax[3 * k] + a &&
        kmin[3 * k + 1] <= q.Max.Y + a && q.Min.Y <= kmax[3 * k + 1] + a &&
        kmin[3 * k + 2] <= q.Max.Z + a && q.Min.Z <= kmax[3 * k + 2] + a;

    bool EintragSchneidet(int e, Huelle3 q, double a) =>
        emin[3 * e] <= q.Max.X + a && q.Min.X <= emax[3 * e] + a &&
        emin[3 * e + 1] <= q.Max.Y + a && q.Min.Y <= emax[3 * e + 1] + a &&
        emin[3 * e + 2] <= q.Max.Z + a && q.Min.Z <= emax[3 * e + 2] + a;

    /// <summary>Hüllquader eines Eintrags.</summary>
    public Huelle3 Huelle(int e) => new(new V3(emin[3 * e], emin[3 * e + 1], emin[3 * e + 2]), new V3(emax[3 * e], emax[3 * e + 1], emax[3 * e + 2]));

    Huelle3 KnotenHuelle(int k) => new(new V3(kmin[3 * k], kmin[3 * k + 1], kmin[3 * k + 2]), new V3(kmax[3 * k], kmax[3 * k + 1], kmax[3 * k + 2]));

    /// <summary>Hüllquader aller Einträge.</summary>
    public Huelle3 Gesamt => knoten == 0 ? Huelle3.Leer : KnotenHuelle(0);

    // ------------------------------------------------------------------ Abfragen

    /// <summary>Alle Einträge, deren Hüllquader achsweise höchstens <paramref name="abstand"/> vom Quader <paramref name="q"/>
    /// entfernt liegt (0 = sie schneiden oder berühren sich). Ergebnis an <paramref name="aus"/> angehängt, ohne Ordnung.</summary>
    public void Kandidaten(Huelle3 q, double abstand, List<int> aus)
    {
        if (knoten == 0 || q.IstLeer) return;
        Span<int> stapel = stackalloc int[128];
        int sp = 0;
        stapel[sp++] = 0;
        while (sp > 0)
        {
            int k = stapel[--sp];
            if (!KnotenSchneidet(k, q, abstand)) continue;
            if (Blattknoten(k))
            {
                for (int i = erster[k], ende = erster[k] + anzahl[k]; i < ende; i++)
                    if (EintragSchneidet(folge[i], q, abstand)) aus.Add(folge[i]);
                continue;
            }
            stapel[sp++] = kind[k]; stapel[sp++] = kind[k] + 1;
        }
    }

    public List<int> Kandidaten(Huelle3 q, double abstand = 0)
    {
        var aus = new List<int>();
        Kandidaten(q, abstand, aus);
        return aus;
    }

    /// <summary>Einträge, deren Hüllquader (um <paramref name="rand"/> vergrössert) die Gerade o + t·r für t ∈ [t0, t1] trifft
    /// (Schichtenverfahren). r muss nicht normiert sein.</summary>
    public void Strahl(V3 o, V3 r, double t0, double t1, double rand, List<int> aus)
    {
        if (knoten == 0) return;
        double ix = 1 / r.X, iy = 1 / r.Y, iz = 1 / r.Z;
        Span<int> stapel = stackalloc int[128];
        int sp = 0;
        stapel[sp++] = 0;
        while (sp > 0)
        {
            int k = stapel[--sp];
            if (!Trifft(kmin, kmax, k)) continue;
            if (Blattknoten(k))
            {
                for (int i = erster[k], ende = erster[k] + anzahl[k]; i < ende; i++)
                    if (Trifft(emin, emax, folge[i])) aus.Add(folge[i]);
                continue;
            }
            stapel[sp++] = kind[k]; stapel[sp++] = kind[k] + 1;
        }

        bool Trifft(double[] lo, double[] hi, int k)
        {
            double tmin = t0, tmax = t1;
            if (!Achse(o.X, ix, lo[3 * k] - rand, hi[3 * k] + rand, ref tmin, ref tmax)) return false;
            if (!Achse(o.Y, iy, lo[3 * k + 1] - rand, hi[3 * k + 1] + rand, ref tmin, ref tmax)) return false;
            return Achse(o.Z, iz, lo[3 * k + 2] - rand, hi[3 * k + 2] + rand, ref tmin, ref tmax);
        }

        static bool Achse(double o, double inv, double lo, double hi, ref double tmin, ref double tmax)
        {
            if (double.IsInfinity(inv)) return o >= lo && o <= hi;   // parallel zur Achse
            double a = (lo - o) * inv, b = (hi - o) * inv;
            if (a > b) (a, b) = (b, a);
            if (a > tmin) tmin = a;
            if (b < tmax) tmax = b;
            return tmin <= tmax;
        }
    }

    /// <summary>Eintrag mit dem kleinsten Wert von <paramref name="abstand"/> (z. B. Abstand Punkt–Dreieck), gesucht nur unter
    /// Einträgen, deren Hüllquader näher als <paramref name="max"/> an <paramref name="punkt"/> liegt (Verzweigen und
    /// Begrenzen mit dem Quaderabstand als unterer Schranke). (-1, ∞), wenn keiner näher liegt.</summary>
    public (int Eintrag, double Wert) Naechster(V3 punkt, Func<int, double> abstand, double max = double.PositiveInfinity)
    {
        int best = -1; double wert = max;
        if (knoten == 0) return (best, double.PositiveInfinity);
        var stapel = new Stack<(int K, double D)>();
        stapel.Push((0, KnotenHuelle(0).Abstand(punkt)));
        while (stapel.Count > 0)
        {
            var (k, d) = stapel.Pop();
            if (d > wert) continue;
            if (Blattknoten(k))
            {
                for (int i = erster[k], ende = erster[k] + anzahl[k]; i < ende; i++)
                {
                    int e = folge[i];
                    if (Huelle(e).Abstand(punkt) > wert) continue;
                    double w = abstand(e);
                    if (w < wert || (w == wert && best < 0)) { wert = w; best = e; }
                }
                continue;
            }
            int l = kind[k], r = l + 1;
            double dl = KnotenHuelle(l).Abstand(punkt), dr = KnotenHuelle(r).Abstand(punkt);
            // den näheren zuletzt auf den Stapel: er wird zuerst untersucht
            if (dl <= dr) { if (dr <= wert) stapel.Push((r, dr)); if (dl <= wert) stapel.Push((l, dl)); }
            else { if (dl <= wert) stapel.Push((l, dl)); if (dr <= wert) stapel.Push((r, dr)); }
        }
        return best < 0 ? (-1, double.PositiveInfinity) : (best, wert);
    }

    /// <summary>Paar (Eintrag hier, Eintrag in b) mit dem kleinsten Wert von <paramref name="abstand"/>, nur unter Paaren, deren
    /// Hüllquader näher als <paramref name="max"/> liegen. (-1, -1, ∞), wenn keines.</summary>
    public (int A, int B, double Wert) NaechstesPaar(Raumindex b, Func<int, int, double> abstand, double max = double.PositiveInfinity)
    {
        int ba = -1, bb = -1; double wert = max;
        if (knoten == 0 || b.knoten == 0) return (-1, -1, double.PositiveInfinity);
        var schlange = new PriorityQueue<(int I, int J), double>();
        schlange.Enqueue((0, 0), KnotenHuelle(0).Abstand(b.KnotenHuelle(0)));
        while (schlange.TryDequeue(out var paar, out var d))
        {
            if (d > wert) break;
            var (i, j) = paar;
            bool blattI = Blattknoten(i), blattJ = b.Blattknoten(j);
            if (blattI && blattJ)
            {
                for (int x = erster[i], ex = erster[i] + anzahl[i]; x < ex; x++)
                    for (int y = b.erster[j], ey = b.erster[j] + b.anzahl[j]; y < ey; y++)
                    {
                        int ea = folge[x], eb = b.folge[y];
                        if (Huelle(ea).Abstand(b.Huelle(eb)) > wert) continue;
                        double w = abstand(ea, eb);
                        if (w < wert || (w == wert && ba < 0)) { wert = w; ba = ea; bb = eb; }
                    }
                continue;
            }
            bool teileI = !blattI && (blattJ || Volumen(KnotenHuelle(i)) >= Volumen(b.KnotenHuelle(j)));
            if (teileI)
            {
                for (int c = kind[i]; c <= kind[i] + 1; c++)
                {
                    double dc = KnotenHuelle(c).Abstand(b.KnotenHuelle(j));
                    if (dc <= wert) schlange.Enqueue((c, j), dc);
                }
            }
            else
            {
                for (int c = b.kind[j]; c <= b.kind[j] + 1; c++)
                {
                    double dc = KnotenHuelle(i).Abstand(b.KnotenHuelle(c));
                    if (dc <= wert) schlange.Enqueue((i, c), dc);
                }
            }
        }
        return ba < 0 ? (-1, -1, double.PositiveInfinity) : (ba, bb, wert);
    }

    static double Volumen(Huelle3 h) { var g = h.Groesse; return (g.X + 1e-9) * (g.Y + 1e-9) * (g.Z + 1e-9); }

    /// <summary>Alle Paare (Eintrag hier, Eintrag in b), deren Hüllquader achsweise höchstens <paramref name="abstand"/>
    /// auseinander liegen (gleichzeitiger Abstieg in beiden Bäumen).</summary>
    public void Paare(Raumindex b, double abstand, List<(int A, int B)> aus)
    {
        if (knoten == 0 || b.knoten == 0) return;
        var stapel = new Stack<(int, int)>();
        stapel.Push((0, 0));
        double a2 = abstand;
        while (stapel.Count > 0)
        {
            var (i, j) = stapel.Pop();
            if (!KnotenSchneidet(i, b.KnotenHuelle(j), a2)) continue;
            bool blattI = Blattknoten(i), blattJ = b.Blattknoten(j);
            if (blattI && blattJ)
            {
                for (int x = erster[i], ex = erster[i] + anzahl[i]; x < ex; x++)
                {
                    var ha = Huelle(folge[x]);
                    for (int y = b.erster[j], ey = b.erster[j] + b.anzahl[j]; y < ey; y++)
                        if (b.EintragSchneidet(b.folge[y], ha, a2)) aus.Add((folge[x], b.folge[y]));
                }
                continue;
            }
            if (!blattI && (blattJ || Volumen(KnotenHuelle(i)) >= Volumen(b.KnotenHuelle(j))))
            {
                stapel.Push((kind[i], j)); stapel.Push((kind[i] + 1, j));
            }
            else
            {
                stapel.Push((i, b.kind[j])); stapel.Push((i, b.kind[j] + 1));
            }
        }
    }

    public List<(int A, int B)> Paare(Raumindex b, double abstand = 0)
    {
        var aus = new List<(int, int)>();
        Paare(b, abstand, aus);
        return aus;
    }

    /// <summary>Alle Paare verschiedener Einträge dieses Baums (A &lt; B), deren Hüllquader achsweise höchstens
    /// <paramref name="abstand"/> auseinander liegen.</summary>
    public void Paare(double abstand, List<(int A, int B)> aus)
    {
        if (knoten == 0) return;
        var stapel = new Stack<(int, int)>();
        stapel.Push((0, 0));
        double a2 = abstand;
        while (stapel.Count > 0)
        {
            var (i, j) = stapel.Pop();
            if (i == j)
            {
                if (Blattknoten(i))
                {
                    for (int x = erster[i], ende = erster[i] + anzahl[i]; x < ende; x++)
                        for (int y = x + 1; y < ende; y++)
                            if (EintragSchneidet(folge[y], Huelle(folge[x]), a2)) aus.Add(Geordnet(folge[x], folge[y]));
                    continue;
                }
                int l = kind[i];
                stapel.Push((l, l)); stapel.Push((l + 1, l + 1)); stapel.Push((l, l + 1));
                continue;
            }
            if (!KnotenSchneidet(i, KnotenHuelle(j), a2)) continue;
            bool blattI = Blattknoten(i), blattJ = Blattknoten(j);
            if (blattI && blattJ)
            {
                for (int x = erster[i], ex = erster[i] + anzahl[i]; x < ex; x++)
                {
                    var ha = Huelle(folge[x]);
                    for (int y = erster[j], ey = erster[j] + anzahl[j]; y < ey; y++)
                        if (EintragSchneidet(folge[y], ha, a2)) aus.Add(Geordnet(folge[x], folge[y]));
                }
                continue;
            }
            if (!blattI && (blattJ || Volumen(KnotenHuelle(i)) >= Volumen(KnotenHuelle(j))))
            {
                stapel.Push((kind[i], j)); stapel.Push((kind[i] + 1, j));
            }
            else
            {
                stapel.Push((i, kind[j])); stapel.Push((i, kind[j] + 1));
            }
        }

        static (int, int) Geordnet(int a, int b) => a < b ? (a, b) : (b, a);
    }

    public List<(int A, int B)> Paare(double abstand = 0)
    {
        var aus = new List<(int, int)>();
        Paare(abstand, aus);
        return aus;
    }
}

/// <summary>Hüllquader-Baum über Objekten (z. B. Prüfkörpern) mit ihrem Hüllquader.</summary>
public sealed class Raumindex<T>
{
    public IReadOnlyList<T> Elemente { get; }
    /// <summary>Der Baum über den Indizes von <see cref="Elemente"/>.</summary>
    public Raumindex Baum { get; }

    public Raumindex(IReadOnlyList<T> elemente, Func<T, Huelle3> huelle)
    {
        Elemente = elemente;
        Baum = new Raumindex(elemente.Select(huelle).ToList());
    }

    /// <summary>Objekte, deren Hüllquader achsweise höchstens abstand vom Quader entfernt liegt.</summary>
    public List<T> Kandidaten(Huelle3 q, double abstand = 0) => Baum.Kandidaten(q, abstand).Select(i => Elemente[i]).ToList();

    /// <summary>Paare (hier, andere), deren Hüllquader achsweise höchstens abstand auseinander liegen.</summary>
    public List<(T A, U B)> Paare<U>(Raumindex<U> andere, double abstand = 0) =>
        Baum.Paare(andere.Baum, abstand).Select(p => (Elemente[p.A], andere.Elemente[p.B])).ToList();

    /// <summary>Paare verschiedener Objekte dieses Index, deren Hüllquader achsweise höchstens abstand auseinander liegen.</summary>
    public List<(T A, T B)> Paare(double abstand = 0) => Baum.Paare(abstand).Select(p => (Elemente[p.A], Elemente[p.B])).ToList();
}
