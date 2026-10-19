// Kollisionsprüfung (Modul 3b, B1): Prüfnetz – ein Dreiecksnetz in mm (doppelte Genauigkeit) mit eigenem Hüllquader-Baum über
// den Dreiecken, für die Schmalphase und die Durchgänge (Aussparungen).
//
// Geschlossenheit und «innen»: Die Ecken werden nach Lage verschweisst (auf 0.0001 mm gerundet – Netze aus der Netzdatei und
// aus Körperteilen teilen Ecken nicht immer über Indizes), das Netz zerfällt in zusammenhängende Teile (über gemeinsame
// Ecken). Ein Teil ist geschlossen, wenn jede seiner Kanten in einer geraden Anzahl Dreiecke vorkommt (wasserdicht; die
// Ausrichtung der Dreiecke spielt für den Strahltest keine Rolle). Ein Punkt liegt innen, wenn er in mindestens einem
// geschlossenen Teil liegt: Strahl in eine «schräge» Richtung, Treffer je Teil zählen, ungerade = innen. Teile, die sich
// überlappen (Familienkörper aus mehreren Grundkörpern), zählen so richtig; Teile mit gemeinsamer Fläche (Wandschichten)
// ebenfalls. Strahlen, die eine Kante, eine Ecke oder streifend eine Fläche treffen, werden verworfen; entschieden wird mit
// zwei übereinstimmenden von bis zu sechs Richtungen. Liegt der Punkt näher als die Toleranz an der Oberfläche (vorab über den
// nächsten Punkt geprüft), heisst das «Flaeche». Offene Teile (Flächenmodelle, Löcher im Netz) haben kein Innen.

namespace Hlks.Kern;

/// <summary>Lage eines Punkts zu einem Netz.</summary>
public enum Punktlage { Aussen, Innen, Flaeche }

/// <summary>Durchstosspunkt einer Strecke a–b durch ein Dreieck: Parameter t (0 = a, 1 = b), Dreieck, Punkt, Einheitsnormale
/// des Dreiecks (nach seiner Umlaufrichtung) und ob der Treffer auf einer Kante oder Ecke liegt.</summary>
public readonly record struct Treffer3(double T, int Dreieck, V3 Punkt, V3 Normale, bool AmRand);

public sealed class Pruefnetz
{
    /// <summary>Raster, auf das Ecken zum Verschweissen gerundet werden (mm).</summary>
    public const double Schweissraster = 1e-4;

    /// <summary>Baryzentrischer Rand, innerhalb dessen ein Strahltreffer als Kanten- bzw. Eckentreffer gilt.</summary>
    const double Randbary = 1e-7;

    readonly double[] p;
    readonly int[] d;
    readonly int[] nutzbar;   // Dreiecke mit Fläche (für Geometrie); die Topologie zählt alle
    readonly object sperre = new();
    Raumindex? index;
    Topologie? topo;

    /// <summary>Strahlrichtungen für den Innen-Test (schräg zu allen Achsen, damit Kanten achsparalleler Netze nicht exakt
    /// getroffen werden).</summary>
    static readonly V3[] Richtungen =
    [
        new V3(0.6247, 0.4321, 0.6505).Einheit(), new V3(-0.3712, 0.8167, 0.4417).Einheit(), new V3(0.2141, -0.5378, 0.8155).Einheit(),
        new V3(-0.7313, -0.2489, -0.6349).Einheit(), new V3(0.5016, -0.7919, -0.3480).Einheit(), new V3(0.0937, 0.2973, -0.9502).Einheit(),
    ];

    public int AnzahlEcken => p.Length / 3;
    public int AnzahlDreiecke => d.Length / 3;
    public Huelle3 Huelle { get; }

    /// <summary>Netz aus Punkten (x, y, z hintereinander, mm) und Dreiecken (je drei Eckindizes). Dreiecke mit ungültigen
    /// Indizes entfallen; Dreiecke ohne Fläche zählen nur für die Geschlossenheit.</summary>
    public Pruefnetz(double[] punkte, int[] dreiecke)
    {
        p = punkte;
        int n = punkte.Length / 3;
        var dl = new List<int>(dreiecke.Length);
        for (int i = 0; i + 2 < dreiecke.Length; i += 3)
        {
            int a = dreiecke[i], b = dreiecke[i + 1], c = dreiecke[i + 2];
            if (a < 0 || b < 0 || c < 0 || a >= n || b >= n || c >= n || a == b || b == c || a == c) continue;
            dl.Add(a); dl.Add(b); dl.Add(c);
        }
        d = [.. dl];
        var nz = new List<int>(d.Length / 3);
        var h = Huelle3.Leer;
        for (int t = 0; t < d.Length / 3; t++)
        {
            Dreieck(t, out var a, out var b, out var c);
            h = h.Mit(a).Mit(b).Mit(c);
            if ((b - a).Kreuz(c - a).Laenge > 1e-9) nz.Add(t);
        }
        nutzbar = [.. nz];
        Huelle = h;
    }

    /// <summary>Aus einer Netzdatei (float32-Punkte, Dreiecke je Produkt).</summary>
    public static Pruefnetz Aus(float[] punkte, int[] dreiecke)
    {
        var pd = new double[punkte.Length];
        for (int i = 0; i < punkte.Length; i++) pd[i] = punkte[i];
        return new Pruefnetz(pd, dreiecke);
    }

    public static Pruefnetz Aus(Netz3 n) => Aus([n]);

    /// <summary>Mehrere Netze zu einem Prüfnetz (jedes bleibt ein eigener Teil, sofern es keine Ecke mit einem anderen teilt).</summary>
    public static Pruefnetz Aus(IEnumerable<Netz3> netze)
    {
        var pu = new List<double>(); var dr = new List<int>();
        foreach (var n in netze)
        {
            int basis = pu.Count / 3;
            foreach (var e in n.Ecken) { pu.Add(e[0]); pu.Add(e.Length > 1 ? e[1] : 0); pu.Add(e.Length > 2 ? e[2] : 0); }
            foreach (var t in n.Dreiecke) if (t.Length == 3) { dr.Add(basis + t[0]); dr.Add(basis + t[1]); dr.Add(basis + t[2]); }
        }
        return new Pruefnetz([.. pu], [.. dr]);
    }

    /// <summary>Gedrehter Quader (Mitte, orthonormale Achsen, halbe Kantenlängen) als geschlossenes Netz, Normalen nach aussen.</summary>
    public static Pruefnetz Quader(V3 mitte, V3 ax, V3 ay, V3 az, double hx, double hy, double hz)
    {
        var pu = new double[24];
        int i = 0;
        for (int k = 0; k < 8; k++)
        {
            double sx = (k & 1) == 0 ? -hx : hx, sy = (k & 2) == 0 ? -hy : hy, sz = (k & 4) == 0 ? -hz : hz;
            var q = mitte + ax * sx + ay * sy + az * sz;
            pu[i++] = q.X; pu[i++] = q.Y; pu[i++] = q.Z;
        }
        // Ecke k: Bit 0 = +x, Bit 1 = +y, Bit 2 = +z (lokal); Flächen im Gegenuhrzeigersinn von aussen
        int[] dr =
        [
            0, 2, 3, 0, 3, 1,   // −z
            4, 5, 7, 4, 7, 6,   // +z
            0, 1, 5, 0, 5, 4,   // −y
            2, 6, 7, 2, 7, 3,   // +y
            0, 4, 6, 0, 6, 2,   // −x
            1, 3, 7, 1, 7, 5,   // +x
        ];
        var n = new Pruefnetz(pu, dr);
        if (ax.Kreuz(ay).Mal(az) < 0) n.UmkehrenIntern();   // linkshändige Achsen: Umlauf drehen
        return n;
    }

    void UmkehrenIntern() { for (int t = 0; t < d.Length; t += 3) (d[t + 1], d[t + 2]) = (d[t + 2], d[t + 1]); }

    public V3 Ecke(int i) => new(p[3 * i], p[3 * i + 1], p[3 * i + 2]);

    public void Dreieck(int t, out V3 a, out V3 b, out V3 c)
    {
        int i = d[3 * t], j = d[3 * t + 1], k = d[3 * t + 2];
        a = new V3(p[3 * i], p[3 * i + 1], p[3 * i + 2]);
        b = new V3(p[3 * j], p[3 * j + 1], p[3 * j + 2]);
        c = new V3(p[3 * k], p[3 * k + 1], p[3 * k + 2]);
    }

    /// <summary>Eckindizes eines Dreiecks.</summary>
    public (int A, int B, int C) Indizes(int t) => (d[3 * t], d[3 * t + 1], d[3 * t + 2]);

    /// <summary>Einheitsnormale nach der Umlaufrichtung (Gegenuhrzeigersinn von aussen = nach aussen, sofern das Netz so
    /// ausgerichtet ist; IFC-Netze sind es nicht immer).</summary>
    public V3 Normale(int t)
    {
        Dreieck(t, out var a, out var b, out var c);
        return (b - a).Kreuz(c - a).Einheit();
    }

    public Huelle3 DreieckHuelle(int t)
    {
        Dreieck(t, out var a, out var b, out var c);
        return Huelle3.Aus(a, b, c);
    }

    /// <summary>Hüllquader-Baum über den Dreiecken mit Fläche (Einträge = Dreiecksnummern), beim ersten Gebrauch gebaut.</summary>
    public Raumindex Index
    {
        get
        {
            if (index is not null) return index;
            lock (sperre)
            {
                if (index is null)
                {
                    var boxen = new Huelle3[nutzbar.Length];
                    for (int i = 0; i < nutzbar.Length; i++) boxen[i] = DreieckHuelle(nutzbar[i]);
                    index = new Raumindex(boxen);
                }
                return index;
            }
        }
    }

    /// <summary>Dreiecksnummer eines Eintrags im Baum.</summary>
    public int DreieckVon(int eintrag) => nutzbar[eintrag];

    /// <summary>Dreiecke (Nummern), deren Hüllquader höchstens <paramref name="abstand"/> vom Quader entfernt liegt.</summary>
    public void Dreiecke(Huelle3 q, double abstand, List<int> aus)
    {
        int vorher = aus.Count;
        Index.Kandidaten(q, abstand, aus);
        for (int i = vorher; i < aus.Count; i++) aus[i] = nutzbar[aus[i]];
    }

    // ------------------------------------------------------------------ Topologie

    sealed class Topologie
    {
        public required int[] Teil { get; init; }          // je Dreieck
        public required bool[] Geschlossen { get; init; }  // je Teil
        public int Teile => Geschlossen.Length;
    }

    Topologie Topo
    {
        get
        {
            if (topo is not null) return topo;
            lock (sperre) { return topo ??= TopologieBauen(); }
        }
    }

    /// <summary>Anzahl zusammenhängender Teile (über verschweisste Ecken).</summary>
    public int Teile => Topo.Teile;

    /// <summary>Alle Teile geschlossen (wasserdicht) – erst dann ist «innen» für das ganze Netz bestimmt.</summary>
    public bool Geschlossen => Topo.Teile > 0 && Topo.Geschlossen.All(g => g);

    /// <summary>Mindestens ein Teil geschlossen (Innen-Test möglich, zählt nur geschlossene Teile).</summary>
    public bool HatGeschlosseneTeile => Topo.Geschlossen.Any(g => g);

    Topologie TopologieBauen()
    {
        int n = AnzahlEcken;
        var schweiss = new int[n];
        var nachLage = new Dictionary<(long, long, long), int>();
        for (int i = 0; i < n; i++)
        {
            var key = ((long)Math.Round(p[3 * i] / Schweissraster), (long)Math.Round(p[3 * i + 1] / Schweissraster), (long)Math.Round(p[3 * i + 2] / Schweissraster));
            if (!nachLage.TryGetValue(key, out var w)) nachLage[key] = w = nachLage.Count;
            schweiss[i] = w;
        }
        int m = nachLage.Count;
        var eltern = new int[m];
        for (int i = 0; i < m; i++) eltern[i] = i;
        int Wurzel(int x) { while (eltern[x] != x) { eltern[x] = eltern[eltern[x]]; x = eltern[x]; } return x; }
        void Verbinden(int x, int y) { x = Wurzel(x); y = Wurzel(y); if (x != y) eltern[Math.Max(x, y)] = Math.Min(x, y); }
        int dn = d.Length / 3;
        var kanten = new Dictionary<long, int>(dn * 2);
        for (int t = 0; t < dn; t++)
        {
            int a = schweiss[d[3 * t]], b = schweiss[d[3 * t + 1]], c = schweiss[d[3 * t + 2]];
            Verbinden(a, b); Verbinden(b, c);
            Zaehlen(a, b); Zaehlen(b, c); Zaehlen(c, a);
        }
        void Zaehlen(int x, int y)
        {
            if (x == y) return;   // zusammengeschweisste Kante (entartet)
            long key = x < y ? ((long)x << 32) | (uint)y : ((long)y << 32) | (uint)x;
            kanten[key] = kanten.GetValueOrDefault(key) + 1;
        }
        var teilVonWurzel = new Dictionary<int, int>();
        var teil = new int[dn];
        for (int t = 0; t < dn; t++)
        {
            int w = Wurzel(schweiss[d[3 * t]]);
            if (!teilVonWurzel.TryGetValue(w, out var k)) teilVonWurzel[w] = k = teilVonWurzel.Count;
            teil[t] = k;
        }
        var geschlossen = Enumerable.Repeat(true, teilVonWurzel.Count).ToArray();
        foreach (var (key, anzahl) in kanten)
        {
            if (anzahl % 2 == 0) continue;
            int x = (int)(key >> 32);
            geschlossen[teilVonWurzel[Wurzel(x)]] = false;
        }
        return new Topologie { Teil = teil, Geschlossen = geschlossen };
    }

    // ------------------------------------------------------------------ Lage eines Punkts

    /// <summary>Liegt der Punkt in einem geschlossenen Teil des Netzes, auf seiner Fläche (näher als die Toleranz) oder
    /// ausserhalb? Ohne geschlossene Teile immer «Aussen».</summary>
    public Punktlage Lage(V3 q)
    {
        if (!Huelle.Enthaelt(q, Geometrie3.Toleranz)) return Punktlage.Aussen;
        var tp = Topo;
        if (!tp.Geschlossen.Any(g => g)) return Punktlage.Aussen;
        if (Naechster(q, Geometrie3.Toleranz).Dreieck >= 0) return Punktlage.Flaeche;   // näher als die Toleranz an der Oberfläche
        int innen = 0, aussen = 0, flaeche = 0;
        var kandidaten = new List<int>();
        var zaehler = new int[tp.Teile];
        foreach (var r in Richtungen)
        {
            var ergebnis = Strahl(q, r, tp, kandidaten, zaehler);
            if (ergebnis == StrahlErgebnis.Flaeche) { if (++flaeche >= 2) return Punktlage.Flaeche; continue; }
            if (ergebnis == StrahlErgebnis.Unklar) continue;
            if (ergebnis == StrahlErgebnis.Innen) innen++; else aussen++;
            if (innen >= 2) return Punktlage.Innen;
            if (aussen >= 2) return Punktlage.Aussen;
        }
        if (innen > aussen) return Punktlage.Innen;
        if (innen == 0 && aussen == 0 && flaeche > 0) return Punktlage.Flaeche;
        return Punktlage.Aussen;
    }

    enum StrahlErgebnis { Innen, Aussen, Flaeche, Unklar }

    StrahlErgebnis Strahl(V3 q, V3 r, Topologie tp, List<int> kandidaten, int[] zaehler)
    {
        kandidaten.Clear();
        Array.Clear(zaehler);
        Index.Strahl(q, r, -Geometrie3.Toleranz, double.PositiveInfinity, Geometrie3.Toleranz, kandidaten);
        foreach (var e in kandidaten)
        {
            int t = nutzbar[e];
            Dreieck(t, out var a, out var b, out var c);
            if (!Geometrie3.GeradeDreieck(q, r, a, b, c, out var s, out var u, out var v))
            {
                // Strahl parallel zur Ebene: liegt er in ihr, ist die Richtung unbrauchbar
                var n = (b - a).Kreuz(c - a).Einheit();
                if (Math.Abs(n.Mal(q - a)) < Geometrie3.Toleranz) return StrahlErgebnis.Unklar;
                continue;
            }
            if (u < -Randbary || v < -Randbary || u + v > 1 + Randbary) continue;
            if (Math.Abs(s) <= Geometrie3.Toleranz) return StrahlErgebnis.Flaeche;
            if (s < 0) continue;
            if (u < Randbary || v < Randbary || u + v > 1 - Randbary) return StrahlErgebnis.Unklar;
            zaehler[tp.Teil[t]]++;
        }
        for (int k = 0; k < zaehler.Length; k++)
            if (tp.Geschlossen[k] && (zaehler[k] & 1) == 1) return StrahlErgebnis.Innen;
        return StrahlErgebnis.Aussen;
    }

    // ------------------------------------------------------------------ nächster Punkt, Durchstosspunkte

    /// <summary>Nächster Punkt der Netzoberfläche zu q (nur gesucht bis <paramref name="max"/>). Dreieck −1 und Abstand ∞, wenn
    /// nichts näher liegt.</summary>
    public (double Abstand, V3 Punkt, int Dreieck) Naechster(V3 q, double max = double.PositiveInfinity)
    {
        var (e, w) = Index.Naechster(q, i =>
        {
            Dreieck(nutzbar[i], out var a, out var b, out var c);
            return (q - Geometrie3.NaechsterPunktDreieck(q, a, b, c)).Laenge;
        }, max);
        if (e < 0) return (double.PositiveInfinity, q, -1);
        int t = nutzbar[e];
        Dreieck(t, out var x, out var y, out var z);
        return (w, Geometrie3.NaechsterPunktDreieck(q, x, y, z), t);
    }

    /// <summary>Alle Durchstosspunkte der Strecke a–b durch Dreiecke des Netzes, nach t geordnet. Dreiecke, in deren Ebene die
    /// Strecke liegt, zählen nicht (nur Berührung).</summary>
    public List<Treffer3> Treffer(V3 a, V3 b)
    {
        var aus = new List<Treffer3>();
        var r = b - a;
        double l = r.Laenge;
        if (l <= 1e-9) return aus;
        var kandidaten = new List<int>();
        Index.Strahl(a, r, 0, 1, Geometrie3.Toleranz, kandidaten);
        double et = Geometrie3.Toleranz / l;
        foreach (var e in kandidaten)
        {
            int t = nutzbar[e];
            Dreieck(t, out var x, out var y, out var z);
            if (!Geometrie3.GeradeDreieck(a, r, x, y, z, out var s, out var u, out var v)) continue;
            if (u < -Randbary || v < -Randbary || u + v > 1 + Randbary || s < -et || s > 1 + et) continue;
            bool rand = u < Randbary || v < Randbary || u + v > 1 - Randbary;
            aus.Add(new Treffer3(Math.Clamp(s, 0, 1), t, a + r * Math.Clamp(s, 0, 1), (y - x).Kreuz(z - x).Einheit(), rand));
        }
        aus.Sort((m, n) => m.T.CompareTo(n.T));
        return aus;
    }

    /// <summary>Abschnitte [T0, T1] der Strecke a–b, die in geschlossenen Teilen des Netzes liegen (Parameter 0 = a, 1 = b).
    /// Verfahren: Durchstosspunkte (auf die Toleranz zusammengefasst) teilen die Strecke; die Mitte jedes Stücks wird mit dem
    /// Strahltest eingeordnet; benachbarte innere Stücke (innere Trennflächen, z. B. zwischen Wandschichten) werden vereinigt.</summary>
    public List<(double T0, double T1)> InnenAbschnitte(V3 a, V3 b) => InnenAbschnitte(a, b, out _);

    /// <summary>Wie <see cref="InnenAbschnitte(V3, V3)"/>; dazu die Durchstosspunkte je Grenze (Gruppen gleicher Lage).</summary>
    public List<(double T0, double T1)> InnenAbschnitte(V3 a, V3 b, out List<List<Treffer3>> gruppen)
    {
        gruppen = [];
        var aus = new List<(double, double)>();
        double l = (b - a).Laenge;
        if (l <= 1e-9 || !Huelle3.Aus(a, b).Schneidet(Huelle, Geometrie3.Toleranz) || !HatGeschlosseneTeile) return aus;
        var treffer = Treffer(a, b);
        double et = Geometrie3.Toleranz / l;
        foreach (var t in treffer)
        {
            if (gruppen.Count > 0 && t.T - gruppen[^1][^1].T <= et) gruppen[^1].Add(t);
            else gruppen.Add([t]);
        }
        var grenzen = new List<double> { 0 };
        foreach (var g in gruppen)
        {
            double tm = g.Average(x => x.T);
            if (tm - grenzen[^1] > et) grenzen.Add(tm);
        }
        if (1 - grenzen[^1] > et) grenzen.Add(1); else grenzen[^1] = 1;
        if (grenzen.Count < 2) grenzen = [0, 1];
        double? beginn = null;
        for (int i = 0; i + 1 < grenzen.Count; i++)
        {
            double t0 = grenzen[i], t1 = grenzen[i + 1];
            var m = a + (b - a) * ((t0 + t1) / 2);
            bool innen = Lage(m) == Punktlage.Innen;
            if (innen) beginn ??= t0;
            else if (beginn is double s) { aus.Add((s, t0)); beginn = null; }
        }
        if (beginn is double s1) aus.Add((s1, 1));
        return aus;
    }
}
