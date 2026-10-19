// Rechenteil der Netzanalyse (Modul 2, Auftrag B3): Zusammenführen, Verschweissen, Segmentieren, Einpassen von Ebene und
// Zylinder, Zylindergruppen, Stutzen, Ansichtsumrisse (Clipper2), Vereinfachen. Beschreibung: NetzAnalyse.cs.

using Clipper2Lib;

namespace Hlks.Kern;

public static partial class NetzAnalyse
{
    // ---------- Grenzwerte (Annahmen, Prüfliste) ----------

    /// <summary>Ebene: zweitgrösster Eigenwert der Normalenstreuung darunter (≈ 1.8° Streuung).</summary>
    const double EbeneStreuung = 1e-3;
    /// <summary>Zylinder: kleinster Eigenwert der Normalenstreuung darunter (Normalen senkrecht zu einer Achse).</summary>
    const double ZylinderStreuung = 2e-3;
    /// <summary>Zylinder: Einpassfehler höchstens 1 % des Radius, sonst «sonst».</summary>
    const double ZylinderFehlerMax = 0.01;
    /// <summary>Bezugsfehler für die Sicherheit: Zylinder 0.2 % des Radius → Sicherheit 0.5.</summary>
    const double ZylinderFehlerBezug = 0.002;
    /// <summary>Stutzen: Zylinder muss mindestens so weit umlaufen (Grad).</summary>
    const double StutzenBogenMin = 350;
    /// <summary>Umriss: Rechengenauigkeit 0.001 mm.</summary>
    const double Skala = 1000;

    // ---------- Grunddaten ----------

    sealed class Roh
    {
        public double[] P = [], N = [], TN = [], TA = [];
        public int[] T = [];
        public int NV, NT;
        public bool[] NetzHatNormalen = [];
    }

    sealed class Kontext(Roh r)
    {
        public readonly Roh R = r;
        public double Diag, TolL;
        public readonly double[] Min = new double[3], Max = new double[3];
        /// <summary>Geschweisste Id je Ecke, Anzahl geschweisster Ecken.</summary>
        public int[] Wid = [];
        public int NW;
        /// <summary>Kompaktes geschweisstes Netz (nur benutzte Ecken).</summary>
        public double[] EP = [];
        public int[] ET = [];
        public readonly int[] Marke = new int[r.NV];
        public int MarkeZaehler;
        public readonly double[] Gewicht = new double[r.NV];

        public void Verschweissen()
        {
            double q = Math.Max(1e-4, Diag * 1e-9);
            var karte = new Dictionary<(long, long, long), int>(R.NV);
            Wid = new int[R.NV];
            for (int v = 0; v < R.NV; v++)
            {
                var key = ((long)Math.Round(R.P[3 * v] / q), (long)Math.Round(R.P[3 * v + 1] / q), (long)Math.Round(R.P[3 * v + 2] / q));
                if (!karte.TryGetValue(key, out var id)) { id = NW++; karte[key] = id; }
                Wid[v] = id;
            }
            var kompakt = new int[NW];
            Array.Fill(kompakt, -1);
            var ep = new List<double>(NW * 3);
            ET = new int[R.T.Length];
            int n = 0;
            for (int i = 0; i < R.T.Length; i++)
            {
                int v = R.T[i], w = Wid[v];
                if (kompakt[w] < 0) { kompakt[w] = n++; ep.Add(R.P[3 * v]); ep.Add(R.P[3 * v + 1]); ep.Add(R.P[3 * v + 2]); }
                ET[i] = kompakt[w];
            }
            EP = ep.ToArray();
        }

        public (double[], int[]) GeschweisstesNetz() => (EP, ET);

        public double[] Punkt(int v) => [R.P[3 * v], R.P[3 * v + 1], R.P[3 * v + 2]];

        readonly Dictionary<(long, long, long), double> stuetzWerte = [];

        /// <summary>Grösste Projektion der Modellecken auf die Richtung d (je Richtung einmal gerechnet).</summary>
        public double Stuetz(double[] d)
        {
            var key = ((long)Math.Round(d[0] * 1e9), (long)Math.Round(d[1] * 1e9), (long)Math.Round(d[2] * 1e9));
            if (stuetzWerte.TryGetValue(key, out var w)) return w;
            double dx = d[0], dy = d[1], dz = d[2], m = double.NegativeInfinity;
            var ep = EP;
            for (int i = 0; i + 2 < ep.Length; i += 3)
            {
                double s = ep[i] * dx + ep[i + 1] * dy + ep[i + 2] * dz;
                if (s > m) m = s;
            }
            return stuetzWerte[key] = m;
        }
    }

    sealed class ZylGruppe
    {
        public List<int> Glieder = [];
        public double[] A = [], P0 = [], B1 = [], B2 = [];
        public double R, Rms, TMin, TMax, Bogen, SFit;
        public bool Konvex, Segmentiert;
        public EndInfo[] Enden = [];
    }

    sealed class EndInfo
    {
        public double T;
        public double[] Mitte = [], Richtung = [];
        /// <summary>frei (Deckel/Ring nach aussen bzw. Bohrung öffnet in eine Fläche) | ansatz | anderes | offen | unklar</summary>
        public string Status = "offen";
        public List<int> Deckel = [], Ansatz = [], Anderes = [];
        public bool Benutzt;
    }

    // ---------- Zusammenführen ----------

    static Roh Zusammenfuehren(IReadOnlyList<AnalyseNetz> netze, NetzAnalyseErgebnis e)
    {
        int nv = 0, nt = 0;
        foreach (var n in netze)
        {
            if (n.Ecken.Length % 3 != 0)
                throw new HlksFehler($"Netz «{n.Name}»: {n.Ecken.Length} Eckwerte sind kein Vielfaches von 3.", "Ecken als x, y, z je Ecke übergeben.");
            if (n.Dreiecke.Length % 3 != 0)
                throw new HlksFehler($"Netz «{n.Name}»: {n.Dreiecke.Length} Dreieckswerte sind kein Vielfaches von 3.", "Dreiecke als drei Eckindizes übergeben.");
            nv += n.Ecken.Length / 3;
            nt += n.Dreiecke.Length / 3;
        }
        var r = new Roh
        {
            NV = nv, NT = nt, P = new double[3 * nv], N = new double[3 * nv], T = new int[3 * nt],
            TN = new double[3 * nt], TA = new double[nt], NetzHatNormalen = new bool[netze.Count],
        };
        int ov = 0, ot = 0;
        for (int i = 0; i < netze.Count; i++)
        {
            var n = netze[i];
            int nvi = n.Ecken.Length / 3, nti = n.Dreiecke.Length / 3;
            for (int j = 0; j < n.Ecken.Length; j++)
            {
                if (!double.IsFinite(n.Ecken[j]))
                    throw new HlksFehler($"Netz «{n.Name}»: Ecke {j / 3} hat keine gültige Koordinate.", "Datei neu exportieren.");
                r.P[3 * ov + j] = n.Ecken[j];
            }
            bool normalen = n.Normalen is not null && n.Normalen.Length == n.Ecken.Length && n.Normalen.All(double.IsFinite);
            if (n.Normalen is not null && !normalen) e.Hinweise.Add($"Netz «{n.Name}»: Normalen unvollständig – aus den Dreiecken gerechnet.");
            r.NetzHatNormalen[i] = normalen;
            for (int j = 0; j < n.Ecken.Length; j++) r.N[3 * ov + j] = normalen ? n.Normalen![j] : double.NaN;
            for (int j = 0; j < n.Dreiecke.Length; j++)
            {
                int idx = n.Dreiecke[j];
                if ((uint)idx >= (uint)nvi)
                    throw new HlksFehler($"Netz «{n.Name}»: Dreieck {j / 3} verweist auf Ecke {idx}, das Netz hat nur {nvi} Ecken.", "Datei neu exportieren.");
                r.T[3 * ot + j] = idx + ov;
            }
            e.Teile.Add(new NetzTeilInfo { Name = n.Name, Farbe = n.Farbe, Dreiecke = [ot, ot + nti - 1] });
            ov += nvi;
            ot += nti;
        }
        for (int t = 0; t < nt; t++)
        {
            int a = r.T[3 * t], b = r.T[3 * t + 1], c = r.T[3 * t + 2];
            double ux = r.P[3 * b] - r.P[3 * a], uy = r.P[3 * b + 1] - r.P[3 * a + 1], uz = r.P[3 * b + 2] - r.P[3 * a + 2];
            double vx = r.P[3 * c] - r.P[3 * a], vy = r.P[3 * c + 1] - r.P[3 * a + 1], vz = r.P[3 * c + 2] - r.P[3 * a + 2];
            double nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
            double l = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            r.TA[t] = l / 2;
            if (l > 1e-300) { r.TN[3 * t] = nx / l; r.TN[3 * t + 1] = ny / l; r.TN[3 * t + 2] = nz / l; }
        }
        return r;
    }

    static void HuelleAusEcken(Kontext k, NetzAnalyseErgebnis e)
    {
        var r = k.R;
        for (int i = 0; i < 3; i++) { k.Min[i] = double.PositiveInfinity; k.Max[i] = double.NegativeInfinity; }
        foreach (var v in r.T)
            for (int i = 0; i < 3; i++)
            {
                k.Min[i] = Math.Min(k.Min[i], r.P[3 * v + i]);
                k.Max[i] = Math.Max(k.Max[i], r.P[3 * v + i]);
            }
        k.Diag = Math.Sqrt(Sq(k.Max[0] - k.Min[0]) + Sq(k.Max[1] - k.Min[1]) + Sq(k.Max[2] - k.Min[2]));
        // Längentoleranz für «liegt auf»: 0.01 mm bzw. 1e-5 der Diagonale (Annahme)
        k.TolL = Math.Max(0.01, 1e-5 * k.Diag);
        e.Min = (double[])k.Min.Clone();
        e.Max = (double[])k.Max.Clone();
    }

    // ---------- Flächen ----------

    static List<NetzFlaecheInfo> FlaechenBilden(IReadOnlyList<AnalyseNetz> netze, Kontext k, NetzAnalyseOptionen o, NetzAnalyseErgebnis e)
    {
        var liste = new List<NetzFlaecheInfo>();
        double cosGrenze = Math.Cos(o.KantenwinkelGrad * Math.PI / 180);
        int ot = 0, ungueltig = 0, segmentiert = 0;
        for (int i = 0; i < netze.Count; i++)
        {
            var n = netze[i];
            int nti = n.Dreiecke.Length / 3;
            var belegt = new bool[nti];
            bool hatBrep = n.Flaechen is { Count: > 0 };
            if (hatBrep)
                for (int gi = 0; gi < n.Flaechen!.Count; gi++)
                {
                    var g = n.Flaechen[gi];
                    if (g.Erstes < 0 || g.Letztes < g.Erstes || g.Letztes >= nti) { ungueltig++; continue; }
                    var tris = new List<int>();
                    for (int t = g.Erstes; t <= g.Letztes; t++)
                        if (!belegt[t]) { belegt[t] = true; tris.Add(ot + t); }
                    if (tris.Count == 0) continue;
                    liste.Add(new NetzFlaecheInfo
                    {
                        Netz = i, BrepFlaeche = gi, Quelle = "brep", Dreiecksbereich = [g.Erstes, g.Letztes],
                        Farbe = g.Farbe ?? n.Farbe, Tris = tris.ToArray(),
                    });
                }
            var rest = new List<int>();
            for (int t = 0; t < nti; t++) if (!belegt[t]) rest.Add(ot + t);
            if (rest.Count > 0)
            {
                foreach (var region in Segmentieren(k, rest, cosGrenze))
                    liste.Add(new NetzFlaecheInfo { Netz = i, Quelle = "segmentiert", Farbe = n.Farbe, Tris = region });
                segmentiert += rest.Count;
                if (hatBrep) e.Hinweise.Add($"Netz «{n.Name}»: {rest.Count} Dreiecke ohne B-Rep-Fläche – nach Kantenwinkel segmentiert.");
            }
            ot += nti;
        }
        if (ungueltig > 0) e.Hinweise.Add($"{ungueltig} B-Rep-Flächen mit ungültigem Dreiecksbereich übergangen.");
        if (segmentiert > 0 && netze.All(n => n.Flaechen is not { Count: > 0 }))
            e.Hinweise.Add($"Keine B-Rep-Flächen: Flächen nach Kantenwinkel ≤ {o.KantenwinkelGrad}° segmentiert (Sicherheit × 0.9).");
        for (int i = 0; i < liste.Count; i++) liste[i].Nummer = i;
        return liste;
    }

    /// <summary>Teilt Dreiecke in Flächen: Nachbarn über eine gemeinsame Kante mit Knick ≤ Kantenwinkel.</summary>
    static List<int[]> Segmentieren(Kontext k, List<int> tris, double cosGrenze)
    {
        var r = k.R;
        int m = tris.Count;
        var nb = new int[3 * m];
        Array.Fill(nb, -1);
        var kanten = new Dictionary<long, int>(m * 2);
        for (int i = 0; i < m; i++)
        {
            int t = tris[i];
            for (int j = 0; j < 3; j++)
            {
                int a = k.Wid[r.T[3 * t + j]], b = k.Wid[r.T[3 * t + (j + 1) % 3]];
                if (a == b) continue;
                long key = (long)Math.Min(a, b) * k.NW + Math.Max(a, b);
                if (!kanten.TryGetValue(key, out var anderer)) kanten[key] = 3 * i + j;
                else if (anderer >= 0) { nb[3 * i + j] = anderer / 3; nb[anderer] = i; kanten[key] = -1; }
            }
        }
        var region = new int[m];
        Array.Fill(region, -1);
        var aus = new List<int[]>();
        var stapel = new Stack<int>();
        for (int s = 0; s < m; s++)
        {
            if (region[s] >= 0) continue;
            var glieder = new List<int>();
            region[s] = aus.Count;
            stapel.Push(s);
            while (stapel.Count > 0)
            {
                int i = stapel.Pop();
                glieder.Add(tris[i]);
                int ti = tris[i];
                bool entartetI = r.TA[ti] < 1e-12;
                for (int j = 0; j < 3; j++)
                {
                    int x = nb[3 * i + j];
                    if (x < 0 || region[x] >= 0) continue;
                    int tx = tris[x];
                    double c = r.TN[3 * ti] * r.TN[3 * tx] + r.TN[3 * ti + 1] * r.TN[3 * tx + 1] + r.TN[3 * ti + 2] * r.TN[3 * tx + 2];
                    if (c >= cosGrenze || entartetI || r.TA[tx] < 1e-12) { region[x] = aus.Count; stapel.Push(x); }
                }
            }
            glieder.Sort();
            aus.Add(glieder.ToArray());
        }
        return aus;
    }

    /// <summary>Passt Ebene bzw. Zylinder ein und setzt Form, Werte und Sicherheit.</summary>
    static void Klassifizieren(Kontext k, NetzFlaecheInfo f)
    {
        var r = k.R;
        int marke = ++k.MarkeZaehler;
        var verts = new List<int>();
        double inhalt = 0, cx = 0, cy = 0, cz = 0, sx = 0, sy = 0, sz = 0;
        foreach (var t in f.Tris)
        {
            double a = r.TA[t];
            inhalt += a;
            for (int j = 0; j < 3; j++)
            {
                int v = r.T[3 * t + j];
                if (k.Marke[v] != marke) { k.Marke[v] = marke; verts.Add(v); k.Gewicht[v] = 0; }
                k.Gewicht[v] += a / 3;
                cx += a / 3 * r.P[3 * v]; cy += a / 3 * r.P[3 * v + 1]; cz += a / 3 * r.P[3 * v + 2];
            }
            sx += a * r.TN[3 * t]; sy += a * r.TN[3 * t + 1]; sz += a * r.TN[3 * t + 2];
        }
        f.Verts = verts.ToArray();
        f.AnzahlDreiecke = f.Tris.Length;
        f.Inhalt = inhalt;
        var min = new[] { double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity };
        var max = new[] { double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity };
        double mx = 0, my = 0, mz = 0;
        foreach (var v in verts)
            for (int i = 0; i < 3; i++)
            {
                min[i] = Math.Min(min[i], r.P[3 * v + i]);
                max[i] = Math.Max(max[i], r.P[3 * v + i]);
            }
        foreach (var v in verts) { mx += r.P[3 * v]; my += r.P[3 * v + 1]; mz += r.P[3 * v + 2]; }
        f.Min = min;
        f.Max = max;
        f.Schwerpunkt = inhalt > 0 ? [cx / inhalt, cy / inhalt, cz / inhalt] : [mx / verts.Count, my / verts.Count, mz / verts.Count];
        double quelle = f.Quelle == "brep" ? 1 : 0.9;
        double groesse = Math.Sqrt(Sq(max[0] - min[0]) + Sq(max[1] - min[1]) + Sq(max[2] - min[2]));
        double nsum = Math.Sqrt(sx * sx + sy * sy + sz * sz);

        // Streuung der Normalen: Eckennormalen (exakt aus STEP), sonst Dreiecksnormalen
        var m = new double[3, 3];
        double w = 0;
        if (r.NetzHatNormalen[f.Netz])
            foreach (var v in verts)
            {
                double nx = r.N[3 * v], ny = r.N[3 * v + 1], nz = r.N[3 * v + 2], l = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (l < 1e-12) continue;
                Aufaddieren(m, nx / l, ny / l, nz / l, k.Gewicht[v]);
                w += k.Gewicht[v];
            }
        else
            foreach (var t in f.Tris)
            {
                Aufaddieren(m, r.TN[3 * t], r.TN[3 * t + 1], r.TN[3 * t + 2], r.TA[t]);
                w += r.TA[t];
            }
        if (w <= 0 || verts.Count < 3)
        {
            f.Form = "sonst";
            f.Sicherheit = 0.3;
            return;
        }
        for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) m[i, j] /= w;
        var (lam, vec) = Eigen(m);

        if (lam[1] < EbeneStreuung)
        {
            // Ebene: Normale aus den Dreiecken (exakt für ebene Dreiecke), sonst Hauptachse der Ecken
            double[] n = nsum / Math.Max(inhalt, 1e-300) > 0.5 ? [sx / nsum, sy / nsum, sz / nsum] : EbenenNormale(k, verts, sx, sy, sz);
            double qs = 0;
            foreach (var v in verts)
            {
                double d = (r.P[3 * v] - f.Schwerpunkt[0]) * n[0] + (r.P[3 * v + 1] - f.Schwerpunkt[1]) * n[1] + (r.P[3 * v + 2] - f.Schwerpunkt[2]) * n[2];
                qs += d * d;
            }
            double rms = Math.Sqrt(qs / verts.Count);
            double bezug = Math.Max(1e-3, 1e-4 * groesse);
            double s = 1 / (1 + Sq(rms / bezug)) * quelle;
            if (nsum / Math.Max(inhalt, 1e-300) < 0.5) s *= 0.5; // Dreiecke uneinheitlich ausgerichtet
            f.Form = "eben";
            f.Normale = n;
            f.Abstand = Dot(n, f.Schwerpunkt);
            f.Fehler = rms;
            f.Sicherheit = s;
            return;
        }
        if (lam[2] < ZylinderStreuung && ZylinderEinpassen(k, f, Spalte(vec, 2), quelle)) return;

        f.Form = "sonst";
        if (nsum / Math.Max(inhalt, 1e-300) > 0.95) f.Normale = [sx / nsum, sy / nsum, sz / nsum];
        // Nur Inhalt und Schwerpunkt sind gemessen; die Form ist unbekannt (Annahme 0.5).
        f.Sicherheit = 0.5 * quelle;
    }

    static double[] EbenenNormale(Kontext k, List<int> verts, double sx, double sy, double sz)
    {
        var r = k.R;
        double mx = 0, my = 0, mz = 0;
        foreach (var v in verts) { mx += r.P[3 * v]; my += r.P[3 * v + 1]; mz += r.P[3 * v + 2]; }
        mx /= verts.Count; my /= verts.Count; mz /= verts.Count;
        var c = new double[3, 3];
        foreach (var v in verts) Aufaddieren(c, r.P[3 * v] - mx, r.P[3 * v + 1] - my, r.P[3 * v + 2] - mz, 1);
        var (_, vec) = Eigen(c);
        var n = Spalte(vec, 2);
        if (n[0] * sx + n[1] * sy + n[2] * sz < 0) n = [-n[0], -n[1], -n[2]];
        return n;
    }

    static bool ZylinderEinpassen(Kontext k, NetzFlaecheInfo f, double[] a, double quelle)
    {
        var r = k.R;
        a = Kanonisch(a);
        var (b1, b2) = Basis(a);
        var c0 = f.Schwerpunkt;
        var kreis = KreisAus(k, f.Verts, c0, b1, b2);
        if (kreis is null) return false;
        var (ccx, ccy, rad, rms) = kreis.Value;
        if (rad <= 0 || rad > 1000 * k.Diag || rms / rad > ZylinderFehlerMax) return false;
        double[] p0 = [c0[0] + ccx * b1[0] + ccy * b2[0], c0[1] + ccx * b1[1] + ccy * b2[1], c0[2] + ccx * b1[2] + ccy * b2[2]];
        double tmin = double.PositiveInfinity, tmax = double.NegativeInfinity;
        foreach (var v in f.Verts)
        {
            double t = (r.P[3 * v] - p0[0]) * a[0] + (r.P[3 * v + 1] - p0[1]) * a[1] + (r.P[3 * v + 2] - p0[2]) * a[2];
            tmin = Math.Min(tmin, t);
            tmax = Math.Max(tmax, t);
        }
        double konvex = 0, inhalt = 0;
        foreach (var t in f.Tris)
        {
            double gx = 0, gy = 0, gz = 0;
            for (int j = 0; j < 3; j++) { int v = r.T[3 * t + j]; gx += r.P[3 * v] / 3; gy += r.P[3 * v + 1] / 3; gz += r.P[3 * v + 2] / 3; }
            double dx = gx - p0[0], dy = gy - p0[1], dz = gz - p0[2];
            double ta = dx * a[0] + dy * a[1] + dz * a[2];
            dx -= ta * a[0]; dy -= ta * a[1]; dz -= ta * a[2];
            double s = dx * r.TN[3 * t] + dy * r.TN[3 * t + 1] + dz * r.TN[3 * t + 2];
            konvex += r.TA[t] * Math.Sign(s);
            inhalt += r.TA[t];
        }
        double rel = rms / rad;
        double sicher = 1 / (1 + Sq(rel / ZylinderFehlerBezug)) * quelle;
        if (Math.Abs(konvex) < 0.9 * inhalt) sicher *= 0.5; // Dreiecke uneinheitlich ausgerichtet
        f.Form = "zylindrisch";
        f.Achse = a;
        f.AchsPunkt = [p0[0] + tmin * a[0], p0[1] + tmin * a[1], p0[2] + tmin * a[2]];
        f.Radius = rad;
        f.Laenge = tmax - tmin;
        f.Bogen = Abdeckung(k, f.Tris, p0, b1, b2);
        f.Konvex = konvex > 0;
        f.Fehler = rms;
        f.Sicherheit = sicher;
        return true;
    }

    /// <summary>Kreis durch die auf die Ebene (b1, b2) projizierten Ecken, Ursprung c0: Mitte (u, v), Radius, Fehler.</summary>
    static (double cx, double cy, double r, double rms)? KreisAus(Kontext k, IReadOnlyList<int> verts, double[] c0, double[] b1, double[] b2)
    {
        var p = k.R.P;
        var u = new double[verts.Count];
        var w = new double[verts.Count];
        for (int i = 0; i < verts.Count; i++)
        {
            int v = verts[i];
            double dx = p[3 * v] - c0[0], dy = p[3 * v + 1] - c0[1], dz = p[3 * v + 2] - c0[2];
            u[i] = dx * b1[0] + dy * b1[1] + dz * b1[2];
            w[i] = dx * b2[0] + dy * b2[1] + dz * b2[2];
        }
        return KreisEinpassen(u, w);
    }

    /// <summary>Kreis einpassen: algebraisch (Kasa) als Start, dann geometrisch (Gauss-Newton).</summary>
    internal static (double cx, double cy, double r, double rms)? KreisEinpassen(double[] u, double[] w)
    {
        int n = u.Length;
        if (n < 3) return null;
        var m = new double[3, 3];
        var b = new double[3];
        for (int i = 0; i < n; i++)
        {
            double z = u[i] * u[i] + w[i] * w[i];
            double[] zeile = [u[i], w[i], 1];
            for (int a = 0; a < 3; a++)
            {
                for (int c = 0; c < 3; c++) m[a, c] += zeile[a] * zeile[c];
                b[a] -= zeile[a] * z;
            }
        }
        var x = Loesen(m, b);
        if (x is null) return null;
        double cx = -x[0] / 2, cy = -x[1] / 2, r2 = cx * cx + cy * cy - x[2];
        if (!(r2 > 0)) return null;
        double r = Math.Sqrt(r2);
        for (int it = 0; it < 30; it++)
        {
            var jtj = new double[3, 3];
            var jtr = new double[3];
            for (int i = 0; i < n; i++)
            {
                double dx = u[i] - cx, dy = w[i] - cy, rho = Math.Sqrt(dx * dx + dy * dy);
                if (rho < 1e-15) continue;
                double res = rho - r;
                double[] j = [-dx / rho, -dy / rho, -1];
                for (int a = 0; a < 3; a++)
                {
                    for (int c = 0; c < 3; c++) jtj[a, c] += j[a] * j[c];
                    jtr[a] -= j[a] * res;
                }
            }
            var d = Loesen(jtj, jtr);
            if (d is null) break;
            cx += d[0]; cy += d[1]; r += d[2];
            if (Math.Abs(d[0]) + Math.Abs(d[1]) + Math.Abs(d[2]) < 1e-12 * (1 + Math.Abs(r))) break;
        }
        if (!(r > 0)) return null;
        double qs = 0;
        for (int i = 0; i < n; i++) qs += Sq(Math.Sqrt(Sq(u[i] - cx) + Sq(w[i] - cy)) - r);
        return (cx, cy, r, Math.Sqrt(qs / n));
    }

    /// <summary>Abgedeckter Winkel um die Achse (Grad, Raster 0.5°): je Dreieck der kleinste Bogen über seine Ecken.</summary>
    static double Abdeckung(Kontext k, IEnumerable<int> tris, double[] p0, double[] b1, double[] b2)
    {
        var r = k.R;
        var fach = new bool[720];
        Span<double> w = stackalloc double[3];
        foreach (var t in tris)
        {
            for (int j = 0; j < 3; j++)
            {
                int v = r.T[3 * t + j];
                double dx = r.P[3 * v] - p0[0], dy = r.P[3 * v + 1] - p0[1], dz = r.P[3 * v + 2] - p0[2];
                double g = Math.Atan2(dx * b2[0] + dy * b2[1] + dz * b2[2], dx * b1[0] + dy * b1[1] + dz * b1[2]) * 180 / Math.PI;
                w[j] = g < 0 ? g + 360 : g;
            }
            w.Sort();
            double g0 = w[1] - w[0], g1 = w[2] - w[1], g2 = 360 - (w[2] - w[0]);
            double von, bis;
            if (g2 >= g0 && g2 >= g1) { von = w[0]; bis = w[2]; }
            else if (g0 >= g1) { von = w[1]; bis = w[0] + 360; }
            else { von = w[2]; bis = w[1] + 360; }
            if (bis - von < 1e-9) continue;
            for (int i = (int)Math.Floor(von * 2); i < (int)Math.Ceiling(bis * 2); i++) fach[i % 720] = true;
        }
        return fach.Count(x => x) / 2.0;
    }

    // ---------- Zylindergruppen und Enden ----------

    static List<ZylGruppe> Zylindergruppen(Kontext k, List<NetzFlaecheInfo> flaechen)
    {
        var zyl = flaechen.Where(f => f.Form == "zylindrisch").ToList();
        var eltern = Enumerable.Range(0, zyl.Count).ToArray();
        int Wurzel(int i) { while (eltern[i] != i) i = eltern[i] = eltern[eltern[i]]; return i; }
        double cos1 = Math.Cos(Math.PI / 180);
        for (int i = 0; i < zyl.Count; i++)
            for (int j = i + 1; j < zyl.Count; j++)
            {
                NetzFlaecheInfo f = zyl[i], g = zyl[j];
                if (f.Konvex != g.Konvex || Math.Abs(Dot(f.Achse!, g.Achse!)) < cos1) continue;
                double rad = Math.Max(f.Radius!.Value, g.Radius!.Value);
                double tolR = Math.Max(k.TolL, Math.Max(3 * Math.Max(f.Fehler ?? 0, g.Fehler ?? 0), 2e-3 * rad));
                if (Math.Abs(f.Radius.Value - g.Radius.Value) > tolR) continue;
                var d = Sub(g.AchsPunkt!, f.AchsPunkt!);
                double t0 = Dot(d, f.Achse!);
                if (Norm(Sub(d, Mul(f.Achse!, t0))) > Math.Max(k.TolL, 0.01 * rad)) continue;
                double t1 = t0 + g.Laenge!.Value * Math.Sign(Dot(g.Achse!, f.Achse!));
                double gmin = Math.Min(t0, t1), gmax = Math.Max(t0, t1);
                double luecke = Math.Max(0, Math.Max(gmin - f.Laenge!.Value, -gmax));
                if (luecke > Math.Max(k.TolL, 1e-3 * rad)) continue;
                eltern[Wurzel(j)] = Wurzel(i);
            }
        var gruppen = new List<ZylGruppe>();
        foreach (var glieder in Enumerable.Range(0, zyl.Count).GroupBy(Wurzel).OrderBy(g => g.Min()))
        {
            var fl = glieder.Select(i => zyl[i]).ToList();
            var haupt = fl.OrderByDescending(f => f.Inhalt).First();
            var a = haupt.Achse!;
            var (b1, b2) = Basis(a);
            var verts = fl.SelectMany(f => f.Verts).Distinct().ToList();
            var kreis = KreisAus(k, verts, haupt.AchsPunkt!, b1, b2);
            double cx = 0, cy = 0, rad = haupt.Radius!.Value, rms = haupt.Fehler ?? 0;
            if (kreis is not null) (cx, cy, rad, rms) = kreis.Value;
            var p0 = Add(haupt.AchsPunkt!, Add(Mul(b1, cx), Mul(b2, cy)));
            double tmin = double.PositiveInfinity, tmax = double.NegativeInfinity;
            foreach (var v in verts)
            {
                double t = Dot(Sub(k.Punkt(v), p0), a);
                tmin = Math.Min(tmin, t);
                tmax = Math.Max(tmax, t);
            }
            var g = new ZylGruppe
            {
                Glieder = fl.Select(f => f.Nummer).ToList(), A = a, P0 = p0, B1 = b1, B2 = b2, R = rad, Rms = rms,
                TMin = tmin, TMax = tmax, Bogen = Abdeckung(k, fl.SelectMany(f => f.Tris), p0, b1, b2),
                SFit = 1 / (1 + Sq(rms / rad / ZylinderFehlerBezug)), Konvex = haupt.Konvex == true,
                Segmentiert = fl.Any(f => f.Quelle != "brep"),
            };
            g.Enden = [EndeUntersuchen(k, flaechen, g, 0), EndeUntersuchen(k, flaechen, g, 1)];
            gruppen.Add(g);
        }
        return gruppen;
    }

    /// <summary>Welche Flächen berühren den Endkreis (≥ 3 Ecken auf dem Kreis) und wie liegen sie?</summary>
    static EndInfo EndeUntersuchen(Kontext k, List<NetzFlaecheInfo> flaechen, ZylGruppe g, int ende)
    {
        var r = k.R;
        double t = ende == 0 ? g.TMin : g.TMax;
        var richtung = ende == 0 ? Mul(g.A, -1) : (double[])g.A.Clone();
        var e = new EndInfo { T = t, Richtung = richtung, Mitte = Add(g.P0, Mul(g.A, t)) };
        double tolR = Math.Max(k.TolL, Math.Max(3 * g.Rms, 2e-3 * g.R));
        double tolT = Math.Max(k.TolL, Math.Max(3 * g.Rms, 1e-3 * g.R));
        double cos2 = Math.Cos(2 * Math.PI / 180);
        var eigene = new HashSet<int>(g.Glieder);
        foreach (var f in flaechen)
        {
            if (eigene.Contains(f.Nummer)) continue;
            bool weg = false;
            for (int i = 0; i < 3 && !weg; i++)
                weg = f.Max[i] < e.Mitte[i] - g.R - tolR || f.Min[i] > e.Mitte[i] + g.R + tolR;
            if (weg) continue;
            int anzahl = 0;
            foreach (var v in f.Verts)
            {
                double dx = r.P[3 * v] - g.P0[0], dy = r.P[3 * v + 1] - g.P0[1], dz = r.P[3 * v + 2] - g.P0[2];
                double tv = dx * g.A[0] + dy * g.A[1] + dz * g.A[2];
                if (Math.Abs(tv - t) > tolT) continue;
                dx -= tv * g.A[0]; dy -= tv * g.A[1]; dz -= tv * g.A[2];
                if (Math.Abs(Math.Sqrt(dx * dx + dy * dy + dz * dz) - g.R) <= tolR && ++anzahl >= 3) break;
            }
            if (anzahl < 3) continue;
            if (f.Form == "eben" && Math.Abs(Dot(f.Normale!, g.A)) >= cos2)
                (Dot(f.Normale!, richtung) > 0 ? e.Deckel : e.Ansatz).Add(f.Nummer);
            else e.Anderes.Add(f.Nummer);
        }
        e.Status = e.Deckel.Count > 0 && e.Ansatz.Count > 0 ? "unklar"
            : e.Deckel.Count > 0 ? "frei"
            : e.Ansatz.Count > 0 ? "ansatz"
            : e.Anderes.Count > 0 ? "anderes"
            : "offen";
        return e;
    }

    /// <summary>Exakte Kreise voller Zylinder in die Hülle nehmen (die Vernetzung trifft deren Extrempunkte nicht immer).</summary>
    static void HuelleKorrigieren(Kontext k, List<ZylGruppe> gruppen, NetzAnalyseErgebnis e)
    {
        var exakt = new HashSet<int>();
        foreach (var g in gruppen)
        {
            if (g.Bogen < 359.5 || g.Rms / g.R > 1e-3) continue;
            foreach (var en in g.Enden)
                for (int i = 0; i < 3; i++)
                {
                    double aus = g.R * Math.Sqrt(Math.Max(0, 1 - g.A[i] * g.A[i]));
                    if (en.Mitte[i] - aus < e.Min[i] - 1e-9) { e.Min[i] = en.Mitte[i] - aus; e.HuelleKorrigiert = true; }
                    if (en.Mitte[i] + aus > e.Max[i] + 1e-9) { e.Max[i] = en.Mitte[i] + aus; e.HuelleKorrigiert = true; }
                }
            exakt.UnionWith(g.Glieder);
        }
        e.HuelleExakt = e.Flaechen.All(f => f.Form == "eben" || exakt.Contains(f.Nummer));
    }

    // ---------- Stutzen ----------

    static List<NetzStutzenInfo> StutzenSuchen(Kontext k, List<NetzFlaecheInfo> flaechen, List<ZylGruppe> gruppen,
        NetzAnalyseOptionen o, NetzAnalyseErgebnis ergebnis)
    {
        var kandidaten = new List<(NetzStutzenInfo s, double basis, double fit, bool segmentiert, string anderes)>();
        double cos1 = Math.Cos(Math.PI / 180);

        // Aussenzylinder mit freiem Ende: rohr (mit Innenzylinder), zapfen (voll, Deckel), rohrende (ohne Abschluss)
        foreach (var g in gruppen.Where(g => g.Konvex && g.Bogen >= StutzenBogenMin))
            for (int ei = 0; ei < 2; ei++)
            {
                var en = g.Enden[ei];
                if (en.Status is not ("frei" or "offen")) continue;
                double tolT = Math.Max(k.TolL, Math.Max(3 * g.Rms, 1e-3 * g.R));
                ZylGruppe? h = null;
                int hEnde = -1;
                foreach (var x in gruppen)
                {
                    if (x.Konvex || x.Bogen < StutzenBogenMin || x.R >= g.R - k.TolL || Math.Abs(Dot(x.A, g.A)) < cos1) continue;
                    var d = Sub(x.P0, g.P0);
                    if (Norm(Sub(d, Mul(g.A, Dot(d, g.A)))) > Math.Max(k.TolL, 0.01 * g.R)) continue;
                    for (int j = 0; j < 2; j++)
                    {
                        var xe = x.Enden[j];
                        if (xe.Benutzt || xe.Status is not ("frei" or "offen")) continue;
                        if (Math.Abs(Dot(Sub(xe.Mitte, en.Mitte), g.A)) > tolT || Dot(xe.Richtung, en.Richtung) < 0) continue;
                        if (h is null || x.R > h.R) { h = x; hEnde = j; }
                    }
                }
                if (h is not null) h.Enden[hEnde].Benutzt = true;
                string art = h is not null ? "rohr" : en.Status == "offen" ? "rohrende" : "zapfen";
                var s = new NetzStutzenInfo
                {
                    Art = art, Mitte = en.Mitte, Normale = en.Richtung, Aussendurchmesser = 2 * g.R,
                    Innendurchmesser = h is null ? null : 2 * h.R, Laenge = g.TMax - g.TMin,
                    Bohrungstiefe = h is null ? null : h.TMax - h.TMin, Bogen = g.Bogen, Fehler = g.Rms,
                    Flaechen = [.. g.Glieder, .. en.Deckel, .. (h?.Glieder ?? [])],
                };
                s.Flaechen = s.Flaechen.Distinct().Order().ToList();
                // Grundsicherheit je Art (Annahme): Rohr mit Ring 0.95, voller Zapfen 0.85, offenes Rohrende 0.8
                double basis = art switch { "rohr" => 0.95, "zapfen" => 0.85, _ => 0.8 };
                // Zylinder so gross wie das Gerät (Behälter, runde Klappe ohne Bohrung): wohl der Gerätekörper
                if (art != "rohr")
                {
                    double quer = Math.Min(k.Stuetz(g.B1) + k.Stuetz(Mul(g.B1, -1)), k.Stuetz(g.B2) + k.Stuetz(Mul(g.B2, -1)));
                    s.Geraetekoerper = 2 * g.R >= 0.8 * quer;
                }
                kandidaten.Add((s, basis, h is null ? g.SFit : Math.Min(g.SFit, h.SFit), g.Segmentiert || h?.Segmentiert == true, g.Enden[1 - ei].Status));
            }

        // Bohrungen, die in eine ebene Fläche nach aussen öffnen: muffe
        foreach (var h in gruppen.Where(g => !g.Konvex && g.Bogen >= StutzenBogenMin))
            for (int ei = 0; ei < 2; ei++)
            {
                var en = h.Enden[ei];
                if (en.Status != "frei" || en.Benutzt) continue;
                en.Benutzt = true;
                var s = new NetzStutzenInfo
                {
                    Art = "muffe", Mitte = en.Mitte, Normale = en.Richtung, Aussendurchmesser = Ringdurchmesser(k, flaechen, h, en),
                    Innendurchmesser = 2 * h.R, Laenge = h.TMax - h.TMin, Bogen = h.Bogen, Fehler = h.Rms,
                    Flaechen = [.. h.Glieder, .. en.Deckel],
                };
                s.Flaechen = s.Flaechen.Distinct().Order().ToList();
                // Annahme 0.75: Bohrungen können auch Schrauben- oder Befestigungslöcher sein
                kandidaten.Add((s, 0.75, h.SFit, h.Segmentiert, h.Enden[1 - ei].Status));
            }

        // Rand der Hülle, Verdeckung, Sicherheit
        var aus = new List<NetzStutzenInfo>();
        int zuWeitInnen = 0;
        foreach (var (s, basis, fit, segmentiert, anderes) in kandidaten)
        {
            var d = s.Normale;
            double stuetz = k.Stuetz(d), gegen = k.Stuetz(Mul(d, -1));
            double ausdehnung = stuetz + gegen;
            s.AbstandRand = Math.Max(0, stuetz - Dot(s.Mitte, d));
            double grenze = Math.Max(1, o.RandAnteil * ausdehnung);
            if (s.AbstandRand > grenze) { zuWeitInnen++; continue; }
            double r = (s.Aussendurchmesser ?? s.Innendurchmesser ?? 0) / 2;
            s.Verdeckt = Verdeckt(k, s.Mitte, d, Math.Max(10 * k.TolL, 1e-3 * r));
            s.AnderesEnde = anderes switch { "ansatz" => "flaeche", "frei" => "frei", "offen" => "offen", _ => "anderes" };
            double fAnderes = s.AnderesEnde switch { "flaeche" => 1, "anderes" => 0.95, _ => 0.9 };
            double sicher = basis * fit * Math.Min(1, s.Bogen / 360) * (1 - 0.5 * Math.Min(1, s.AbstandRand / grenze))
                            * (s.Verdeckt ? 0.5 : 1) * (segmentiert ? 0.9 : 1) * (s.Geraetekoerper ? 0.4 : 1) * fAnderes;
            s.Sicherheit = Math.Clamp(sicher, 0.01, 0.95);
            aus.Add(s);
        }

        // Nebenbohrungen (z. B. Schraubenlöcher eines Flansches): Bohrung in der Anschlussebene eines grösseren Stutzens
        var cosN = Math.Cos(Math.PI / 180);
        bool Nebenbohrung(NetzStutzenInfo m, NetzStutzenInfo c)
        {
            double dc = c.Aussendurchmesser ?? c.Innendurchmesser ?? 0;
            if (ReferenceEquals(c, m) || dc <= m.Innendurchmesser || Dot(c.Normale, m.Normale) < cosN) return false;
            var d = Sub(m.Mitte, c.Mitte);
            if (Math.Abs(Dot(d, c.Normale)) > Math.Max(k.TolL, 1e-3 * dc)) return false;
            double seitlich = Norm(Sub(d, Mul(c.Normale, Dot(d, c.Normale))));
            return seitlich > k.TolL && seitlich < dc / 2;
        }
        var nebenbohrungen = aus.Where(m => m.Art == "muffe" && aus.Any(c => Nebenbohrung(m, c))).ToHashSet();
        int neben = aus.RemoveAll(nebenbohrungen.Contains);

        if (zuWeitInnen > 0)
            ergebnis.Hinweise.Add($"{zuWeitInnen} zylindrische Enden liegen weiter als {o.RandAnteil:0.##} der Ausdehnung vom Rand der Hülle – nicht als Stutzen gemeldet.");
        if (neben > 0)
            ergebnis.Hinweise.Add($"{neben} Bohrungen in der Anschlussebene eines grösseren Stutzens (z. B. Flanschlöcher) nicht als Stutzen gemeldet.");
        return aus.OrderByDescending(s => s.Sicherheit).ToList();
    }

    /// <summary>Muffe: Aussendurchmesser, wenn die ebene Fläche um die Bohrung aussen ein Kreis ist (Ringfläche), sonst null.</summary>
    static double? Ringdurchmesser(Kontext k, List<NetzFlaecheInfo> flaechen, ZylGruppe h, EndInfo en)
    {
        double tolR = Math.Max(k.TolL, Math.Max(3 * h.Rms, 2e-3 * h.R));
        double rmin = double.PositiveInfinity, rmax = double.NegativeInfinity;
        int n = 0;
        foreach (var fi in en.Deckel)
            foreach (var v in flaechen[fi].Verts)
            {
                var d = Sub(k.Punkt(v), h.P0);
                double rho = Norm(Sub(d, Mul(h.A, Dot(d, h.A))));
                if (rho <= h.R + tolR) continue;
                rmin = Math.Min(rmin, rho);
                rmax = Math.Max(rmax, rho);
                n++;
            }
        if (n < 3 || rmax - rmin > Math.Max(tolR, 2e-3 * rmax)) return null;
        return rmin + rmax;
    }

    /// <summary>Trifft der Strahl ab o in Richtung d (nach eps) ein Dreieck des Modells?</summary>
    static bool Verdeckt(Kontext k, double[] o, double[] d, double eps)
    {
        var p = k.EP;
        var t = k.ET;
        // Vorprüfung: Ecken in der Ebene senkrecht zum Strahl; liegt ein Dreieck ganz auf einer Seite des Strahls, trifft er nicht
        var (b1, b2) = Basis(d);
        int nv = p.Length / 3;
        var pu = new double[nv];
        var pw = new double[nv];
        for (int v = 0; v < nv; v++)
        {
            double x = p[3 * v] - o[0], y = p[3 * v + 1] - o[1], z = p[3 * v + 2] - o[2];
            pu[v] = x * b1[0] + y * b1[1] + z * b1[2];
            pw[v] = x * b2[0] + y * b2[1] + z * b2[2];
        }
        for (int i = 0; i < t.Length; i += 3)
        {
            int ia = t[i], ib = t[i + 1], ic = t[i + 2];
            if ((pu[ia] > 0 && pu[ib] > 0 && pu[ic] > 0) || (pu[ia] < 0 && pu[ib] < 0 && pu[ic] < 0)
                || (pw[ia] > 0 && pw[ib] > 0 && pw[ic] > 0) || (pw[ia] < 0 && pw[ib] < 0 && pw[ic] < 0)) continue;
            int a = 3 * ia, b = 3 * ib, c = 3 * ic;
            double e1x = p[b] - p[a], e1y = p[b + 1] - p[a + 1], e1z = p[b + 2] - p[a + 2];
            double e2x = p[c] - p[a], e2y = p[c + 1] - p[a + 1], e2z = p[c + 2] - p[a + 2];
            double px = d[1] * e2z - d[2] * e2y, py = d[2] * e2x - d[0] * e2z, pz = d[0] * e2y - d[1] * e2x;
            double det = e1x * px + e1y * py + e1z * pz;
            if (Math.Abs(det) < 1e-12) continue;
            double inv = 1 / det;
            double sx = o[0] - p[a], sy = o[1] - p[a + 1], sz = o[2] - p[a + 2];
            double u = (sx * px + sy * py + sz * pz) * inv;
            if (u < 0 || u > 1) continue;
            double qx = sy * e1z - sz * e1y, qy = sz * e1x - sx * e1z, qz = sx * e1y - sy * e1x;
            double v = (d[0] * qx + d[1] * qy + d[2] * qz) * inv;
            if (v < 0 || u + v > 1) continue;
            if ((e2x * qx + e2y * qy + e2z * qz) * inv > eps) return true;
        }
        return false;
    }

    // ---------- Ansichten ----------

    static readonly (string Name, double[] Blick, int Achse, bool SpiegelU, bool SpiegelV, string U, string V)[] AnsichtDefs =
    [
        ("vorne", [0, 1, 0], 1, false, false, "x", "z"),
        ("hinten", [0, -1, 0], 1, true, false, "-x", "z"),
        ("links", [1, 0, 0], 0, true, false, "-y", "z"),
        ("rechts", [-1, 0, 0], 0, false, false, "y", "z"),
        ("oben", [0, 0, -1], 2, false, false, "x", "y"),
        ("unten", [0, 0, 1], 2, false, true, "x", "-y"),
    ];

    static List<NetzAnsichtInfo> AnsichtenRechnen(Kontext k)
    {
        var basis = new List<(Path64 aussen, List<Path64> loecher)>[3];
        Parallel.For(0, 3, achse => basis[achse] = Projektion(k.EP, k.ET, achse));
        var aus = new List<NetzAnsichtInfo>();
        foreach (var def in AnsichtDefs)
        {
            var a = new NetzAnsichtInfo { Name = def.Name, Blick = def.Blick, U = def.U, V = def.V };
            double minu = double.PositiveInfinity, minv = double.PositiveInfinity, maxu = double.NegativeInfinity, maxv = double.NegativeInfinity;
            List<double[]> Wandeln(Path64 p)
            {
                var ring = p.Select(q => new[] { (def.SpiegelU ? -q.X : q.X) / Skala, (def.SpiegelV ? -q.Y : q.Y) / Skala }).ToList();
                if (def.SpiegelU ^ def.SpiegelV) ring.Reverse();
                return ring;
            }
            foreach (var (aussen, loecher) in basis[def.Achse])
            {
                var kontur = new NetzKontur { Aussen = Wandeln(aussen), Loecher = loecher.Select(Wandeln).ToList() };
                foreach (var q in kontur.Aussen)
                {
                    minu = Math.Min(minu, q[0]); maxu = Math.Max(maxu, q[0]);
                    minv = Math.Min(minv, q[1]); maxv = Math.Max(maxv, q[1]);
                }
                a.Inhalt += (Math.Abs(Clipper.Area(aussen)) - loecher.Sum(l => Math.Abs(Clipper.Area(l)))) / (Skala * Skala);
                a.Konturen.Add(kontur);
            }
            a.Box = a.Konturen.Count == 0 ? [0, 0, 0, 0] : [minu, minv, maxu, maxv];
            aus.Add(a);
        }
        return aus;
    }

    /// <summary>Umriss in Richtung einer Achse (0: entlang x → (y, z); 1: entlang y → (x, z); 2: entlang z → (x, y)):
    /// Vereinigung aller projizierten Dreiecke.
    /// Rechenweg: Jedes Dreieck wird in der Projektion gegen den Uhrzeigersinn gedreht; die Summe ihrer Ränder hat an jedem
    /// Punkt die Umlaufzahl «Anzahl überdeckender Dreiecke». Gegenläufige gemeinsame Kanten heben sich in dieser Summe
    /// auf, ohne die Umlaufzahl zu ändern. Clipper (NonZero) vereinigt deshalb nur die verbleibenden Randschleifen statt
    /// aller Dreiecke – gleiches Ergebnis, bei 200 000 Dreiecken ein Bruchteil der Zeit.</summary>
    static List<(Path64 aussen, List<Path64> loecher)> Projektion(double[] p, int[] t, int achse)
    {
        int iu = achse == 0 ? 1 : 0, iv = achse == 2 ? 1 : 2;
        int nv = p.Length / 3;
        var q = new Point64[nv];
        for (int v = 0; v < nv; v++) q[v] = new Point64(Math.Round(p[3 * v + iu] * Skala), Math.Round(p[3 * v + iv] * Skala));
        // Netto-Anzahl je Kante (a < b): + für a→b, − für b→a
        var netto = new Dictionary<long, int>(t.Length);
        void Kante(int a, int b)
        {
            long key = a < b ? (long)a * nv + b : (long)b * nv + a;
            ref int n = ref System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(netto, key, out _);
            n += a < b ? 1 : -1;
        }
        for (int i = 0; i < t.Length; i += 3)
        {
            int a = t[i], b = t[i + 1], c = t[i + 2];
            long kreuz = (q[b].X - q[a].X) * (q[c].Y - q[a].Y) - (q[b].Y - q[a].Y) * (q[c].X - q[a].X);
            if (kreuz == 0) continue; // senkrecht zur Blickrichtung
            if (kreuz < 0) (b, c) = (c, b);
            Kante(a, b); Kante(b, c); Kante(c, a);
        }
        // verbleibende gerichtete Kanten zu geschlossenen Schleifen verbinden (Eingang = Ausgang an jeder Ecke)
        var weiter = new Dictionary<int, Stack<int>>();
        foreach (var (key, n) in netto)
        {
            if (n == 0) continue;
            int a = (int)(key / nv), b = (int)(key % nv);
            if (n < 0) (a, b) = (b, a);
            if (!weiter.TryGetValue(a, out var s)) weiter[a] = s = new Stack<int>();
            for (int j = 0; j < Math.Abs(n); j++) s.Push(b);
        }
        var schleifen = new Paths64();
        foreach (var start in weiter.Keys.Order().ToList())
            while (weiter[start].Count > 0)
            {
                var pfad = new Path64 { q[start] };
                int jetzt = start;
                while (weiter.TryGetValue(jetzt, out var raus) && raus.Count > 0) // immer erfüllt; Schutz gegen Endlosschleife
                {
                    int naechste = raus.Pop();
                    if (naechste == start) break;
                    pfad.Add(q[naechste]);
                    jetzt = naechste;
                }
                if (pfad.Count >= 3) schleifen.Add(pfad);
            }
        if (schleifen.Count == 0) return [];
        var baum = new PolyTree64();
        var clip = new Clipper64 { PreserveCollinear = false };
        clip.AddSubject(schleifen);
        clip.Execute(ClipType.Union, FillRule.NonZero, baum);
        var aus = new List<(Path64, List<Path64>)>();
        const double winzig = 0.01 * Skala * Skala; // Ringe unter 0.01 mm² sind Rechenreste
        void Sammeln(PolyPath64 knoten)
        {
            for (int i = 0; i < knoten.Count; i++)
            {
                var aussen = knoten[i];
                if (aussen.Polygon is null || Math.Abs(Clipper.Area(aussen.Polygon)) < winzig) continue;
                var loecher = new List<Path64>();
                for (int j = 0; j < aussen.Count; j++)
                {
                    var loch = aussen[j];
                    if (loch.Polygon is not null && Math.Abs(Clipper.Area(loch.Polygon)) >= winzig)
                        loecher.Add(Clipper.SimplifyPath(loch.Polygon, 2));
                    Sammeln(loch); // Inseln im Loch
                }
                aus.Add((Clipper.SimplifyPath(aussen.Polygon, 2), loecher));
            }
        }
        Sammeln(baum);
        return aus;
    }

    // ---------- Vereinfachen ----------

    /// <summary>Vereinfachtes Netz für die KI: Ecken im Raster zusammenlegen, bis höchstens max Dreiecke bleiben.</summary>
    static (double[] ecken, int[] dreiecke, double raster) Vereinfachen(Kontext k, NetzAnalyseErgebnis e, int max)
    {
        var p = k.EP;
        var t = k.ET;
        if (t.Length / 3 <= max) return (p, t, 0);
        double inhalt = 0;
        for (int i = 0; i < t.Length; i += 3)
        {
            var u = new[] { p[3 * t[i + 1]] - p[3 * t[i]], p[3 * t[i + 1] + 1] - p[3 * t[i] + 1], p[3 * t[i + 1] + 2] - p[3 * t[i] + 2] };
            var v = new[] { p[3 * t[i + 2]] - p[3 * t[i]], p[3 * t[i + 2] + 1] - p[3 * t[i] + 1], p[3 * t[i + 2] + 2] - p[3 * t[i] + 2] };
            inhalt += Norm(Kreuz(u, v)) / 2;
        }
        double h = Math.Max(Math.Sqrt(2 * inhalt / Math.Max(1, max)) * 0.8, k.Diag * 1e-4);
        for (int versuch = 0; versuch < 40; versuch++, h *= 1.25)
        {
            var (ep, et) = Raster(p, t, k.Min, h);
            if (et.Length / 3 <= max) return (ep, et, h);
        }
        return (p, t, 0);
    }

    static (double[] ecken, int[] dreiecke) Raster(double[] p, int[] t, double[] min, double h)
    {
        var zellen = new Dictionary<(long, long, long), int>();
        var summe = new List<double>();
        var anzahl = new List<int>();
        var id = new int[p.Length / 3];
        for (int v = 0; v < id.Length; v++)
        {
            var key = ((long)Math.Floor((p[3 * v] - min[0]) / h), (long)Math.Floor((p[3 * v + 1] - min[1]) / h), (long)Math.Floor((p[3 * v + 2] - min[2]) / h));
            if (!zellen.TryGetValue(key, out var z)) { z = anzahl.Count; zellen[key] = z; summe.Add(0); summe.Add(0); summe.Add(0); anzahl.Add(0); }
            id[v] = z;
            summe[3 * z] += p[3 * v]; summe[3 * z + 1] += p[3 * v + 1]; summe[3 * z + 2] += p[3 * v + 2];
            anzahl[z]++;
        }
        var gesehen = new HashSet<(int, int, int)>();
        var dreiecke = new List<int>();
        var neu = new int[anzahl.Count];
        Array.Fill(neu, -1);
        var ecken = new List<double>();
        for (int i = 0; i < t.Length; i += 3)
        {
            int a = id[t[i]], b = id[t[i + 1]], c = id[t[i + 2]];
            if (a == b || b == c || a == c) continue;
            int lo = Math.Min(a, Math.Min(b, c)), hi = Math.Max(a, Math.Max(b, c)), mi = a + b + c - lo - hi;
            if (!gesehen.Add((lo, mi, hi))) continue;
            foreach (var z in new[] { a, b, c })
            {
                if (neu[z] >= 0) continue;
                neu[z] = ecken.Count / 3;
                ecken.Add(summe[3 * z] / anzahl[z]); ecken.Add(summe[3 * z + 1] / anzahl[z]); ecken.Add(summe[3 * z + 2] / anzahl[z]);
            }
            dreiecke.Add(neu[a]); dreiecke.Add(neu[b]); dreiecke.Add(neu[c]);
        }
        return (ecken.ToArray(), dreiecke.ToArray());
    }

    // ---------- Lineare Algebra ----------

    static double Sq(double x) => x * x;
    static double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
    static double[] Sub(double[] a, double[] b) => [a[0] - b[0], a[1] - b[1], a[2] - b[2]];
    static double[] Add(double[] a, double[] b) => [a[0] + b[0], a[1] + b[1], a[2] + b[2]];
    static double[] Mul(double[] a, double s) => [a[0] * s, a[1] * s, a[2] * s];
    static double Norm(double[] a) => Math.Sqrt(Dot(a, a));
    static double[] Kreuz(double[] a, double[] b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
    static double[] Einheit(double[] a) { double l = Norm(a); return [a[0] / l, a[1] / l, a[2] / l]; }

    /// <summary>Richtung mit positiver grösster Komponente (eindeutige Achsrichtung).</summary>
    static double[] Kanonisch(double[] a)
    {
        int i = Math.Abs(a[0]) >= Math.Abs(a[1]) && Math.Abs(a[0]) >= Math.Abs(a[2]) ? 0 : Math.Abs(a[1]) >= Math.Abs(a[2]) ? 1 : 2;
        return Einheit(a[i] < 0 ? Mul(a, -1) : a);
    }

    /// <summary>Zwei Einheitsvektoren senkrecht zu a (rechtshändig: b1 × b2 = a).</summary>
    static (double[] b1, double[] b2) Basis(double[] a)
    {
        double[] hilfe = Math.Abs(a[0]) < 0.9 ? [1, 0, 0] : [0, 1, 0];
        var b1 = Einheit(Kreuz(a, hilfe));
        return (b1, Kreuz(a, b1));
    }

    static void Aufaddieren(double[,] m, double x, double y, double z, double w)
    {
        m[0, 0] += w * x * x; m[0, 1] += w * x * y; m[0, 2] += w * x * z;
        m[1, 0] += w * y * x; m[1, 1] += w * y * y; m[1, 2] += w * y * z;
        m[2, 0] += w * z * x; m[2, 1] += w * z * y; m[2, 2] += w * z * z;
    }

    static double[] Spalte(double[,] v, int j) => [v[0, j], v[1, j], v[2, j]];

    /// <summary>Eigenwerte (absteigend) und Eigenvektoren (Spalten) einer symmetrischen 3×3-Matrix (Jacobi).</summary>
    internal static (double[] werte, double[,] vektoren) Eigen(double[,] matrix)
    {
        var a = (double[,])matrix.Clone();
        var v = new double[,] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };
        for (int runde = 0; runde < 60; runde++)
        {
            double aus = Sq(a[0, 1]) + Sq(a[0, 2]) + Sq(a[1, 2]);
            double spur = Sq(a[0, 0]) + Sq(a[1, 1]) + Sq(a[2, 2]);
            if (aus <= 1e-30 * Math.Max(spur, 1e-300)) break;
            foreach (var (p, q) in new[] { (0, 1), (0, 2), (1, 2) })
            {
                if (Math.Abs(a[p, q]) < 1e-300) continue;
                double theta = 0.5 * Math.Atan2(2 * a[p, q], a[q, q] - a[p, p]);
                double c = Math.Cos(theta), s = Math.Sin(theta);
                var j = new double[,] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };
                j[p, p] = c; j[q, q] = c; j[p, q] = s; j[q, p] = -s;
                a = Mal(Mal(Transponiert(j), a), j);
                v = Mal(v, j);
            }
        }
        var ordnung = new[] { 0, 1, 2 }.OrderByDescending(i => a[i, i]).ToArray();
        var werte = ordnung.Select(i => a[i, i]).ToArray();
        var vek = new double[3, 3];
        for (int s = 0; s < 3; s++) for (int z = 0; z < 3; z++) vek[z, s] = v[z, ordnung[s]];
        return (werte, vek);
    }

    static double[,] Mal(double[,] x, double[,] y)
    {
        var m = new double[3, 3];
        for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) for (int l = 0; l < 3; l++) m[i, j] += x[i, l] * y[l, j];
        return m;
    }

    static double[,] Transponiert(double[,] x)
    {
        var m = new double[3, 3];
        for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) m[i, j] = x[j, i];
        return m;
    }

    /// <summary>Löst m · x = b (3×3, Gauss mit Spaltenpivot); null, wenn singulär.</summary>
    static double[]? Loesen(double[,] m, double[] b)
    {
        var a = (double[,])m.Clone();
        var x = (double[])b.Clone();
        double skala = 0;
        foreach (var w in a) skala = Math.Max(skala, Math.Abs(w));
        if (skala == 0) return null;
        for (int s = 0; s < 3; s++)
        {
            int piv = s;
            for (int z = s + 1; z < 3; z++) if (Math.Abs(a[z, s]) > Math.Abs(a[piv, s])) piv = z;
            if (Math.Abs(a[piv, s]) < 1e-14 * skala) return null;
            if (piv != s)
            {
                for (int j = 0; j < 3; j++) (a[s, j], a[piv, j]) = (a[piv, j], a[s, j]);
                (x[s], x[piv]) = (x[piv], x[s]);
            }
            for (int z = s + 1; z < 3; z++)
            {
                double f = a[z, s] / a[s, s];
                for (int j = s; j < 3; j++) a[z, j] -= f * a[s, j];
                x[z] -= f * x[s];
            }
        }
        for (int s = 2; s >= 0; s--)
        {
            for (int j = s + 1; j < 3; j++) x[s] -= a[s, j] * x[j];
            x[s] /= a[s, s];
        }
        return x;
    }
}
