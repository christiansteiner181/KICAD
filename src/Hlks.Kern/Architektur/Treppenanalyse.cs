// Treppe aus einem Körper (Dreiecksnetz) erkennen (Nachbearbeitung N17, Christian 21.10.2026: «auch treppen sind im Grundriss noch
// nicht korrekt angezeigt»). Der IFC-Übertrag (IfcTreppe.cs) nimmt Stufenzahl, Steigung und Auftritt aus den Attributen der Datei;
// fehlen sie – und immer für Stufenkanten, Lauflinie und Podeste – kommen sie von hier.
//
// Verfahren:
//   1. Trittflächen = waagrechte, nach oben zeigende Dreiecke (Neigung unter 3.6°), nach Höhe gruppiert (Toleranz 3 mm) und je Höhe
//      im Grundriss vereinigt (Clipper2). Jede zusammenhängende Fläche ist eine Region mit Höhe z.
//   2. Rauschen fällt weg: Regionen unter 0.02 m², unter 15 % der mittleren Fläche, und kleine Regionen (unter 70 %), die nicht
//      auf dem Raster der Steigung liegen (Seitenwangen, Kanten, Schwellen).
//   3. Steigung = Median der Höhenunterschiede aufeinanderfolgender Regionen (über 20 mm).
//   4. Podest = Region mit mehr als der 2.2fachen mittleren Fläche (Annahme).
//   5. Gehrichtung u je Trittfläche: Achse des umschliessenden Rechtecks, die der Richtung zu den Nachbarregionen am nächsten liegt;
//      ein Podest übernimmt die Richtung des Laufs, der in es mündet (sonst des folgenden).
//   6. Stufenkante = Rand der Region, der gegen die Gehrichtung zeigt (Aussennormale · u ≤ −0.8, längster); rechts → links orientiert.
//      Dazu eine Kante am Austritt (Rand mit Normale · u ≥ 0.8 der letzten Trittfläche, z = Oberkante der letzten Steigung), ausser der
//      Lauf endet auf einem Podest oder auf einer Trittfläche in Höhe einer Geschosskote.
//   7. Lauflinie = Mitte der ersten Kante (leicht nach innen), Schwerpunkte der Regionen, Mitte der Austrittskante (leicht nach
//      innen); Punkte auf einer Geraden (unter 15 mm Abstand) entfallen.
//   8. Stufenzahl (Steigungen) = (Oberkante − Unterkante) / Steigung. Die Unterkante liegt eine Steigung unter der ersten Region,
//      ausser diese liegt in Höhe eines Bodens (dann ist sie selbst der Boden). Die Oberkante liegt eine Steigung über der
//      letzten Region, ausser diese ist ein Podest oder liegt in Höhe eines Bodens. Böden = Geschosskoten und die halben
//      Geschosshöhen (Zwischenpodest zweiläufiger Treppen; Annahme).
//      Nennt die Datei die Stufenzahl (<c>stufenVorgabe</c>), entscheidet sie, wie viele der zwei Steigungen ausserhalb der Flächen
//      liegen: Sprünge = (letzte − erste Höhe) / Steigung; Stufen = Sprünge → beide Enden sind Böden, Sprünge + 2 → keines, Sprünge + 1 →
//      eines (welches, sagen Geschosskoten und Podest).
// Grenze (Annahme): Mündet ein Lauf in ein Podest oder einen Boden, den der Körper nicht enthält und der auf keiner Geschosskote und
// keiner halben Geschosshöhe liegt, zählt die Geometrie ohne Stufenzahl eine Steigung zu viel (Bernstrasse: 2 von 10); die Attribute der
// Datei gehen deshalb vor (IfcTreppe).

using Clipper2Lib;

namespace Hlks.Kern;

public sealed class Treppenform
{
    public double Unterkante { get; init; }
    public double Oberkante { get; init; }
    public double Steigung { get; init; }
    public double Auftritt { get; init; }
    public double Laufbreite { get; init; }
    public double Laufdicke { get; init; }
    /// <summary>Anzahl Steigungen (nach Verfahren Punkt 8).</summary>
    public int Stufen { get; init; }
    /// <summary>Höhenunterschied von der ersten zur letzten waagrechten Fläche in Steigungen (Zahl der Höhensprünge zwischen den Flächen).</summary>
    public int Spruenge { get; init; }
    /// <summary>Anzahl Trittflächen (Regionen) ohne Podeste.</summary>
    public int Trittflaechen { get; init; }
    public List<double[]> Lauflinie { get; init; } = [];
    public List<double[]> Kanten { get; init; } = [];
    public List<TreppePodest> Podeste { get; init; } = [];
    /// <summary>Kurztext: wie Stufenzahl und Höhen entstanden (für die Herkunft).</summary>
    public string Herleitung { get; init; } = "";
}

/// <summary>Waagrechte, nach oben zeigende Fläche eines Netzes: Höhe, Umriss (Gegenuhrzeigersinn), Fläche in mm².</summary>
public sealed record WaagrechteFlaeche(double Z, List<double[]> Ring, double Inhalt);

public static class Treppenanalyse
{
    /// <summary>Höhen innerhalb dieser Toleranz (mm) sind dieselbe Trittfläche.</summary>
    public const double HoehenToleranz = 3;
    const double MinFlaeche = 20_000;          // mm², 0.02 m²
    const double RauschAnteil = 0.15;
    const double PodestAnteil = 2.2;
    const double KoteToleranz = 15;
    const double GeradeToleranz = 15;

    /// <summary>Alle waagrechten, nach oben zeigenden Flächen eines Netzes (Dreiecke je Höhe vereinigt).</summary>
    public static List<WaagrechteFlaeche> Waagrechte(Dreiecksnetz n)
    {
        var dreiecke = new List<(double Z, PathD P)>();
        for (int i = 0; i + 2 < n.D.Count; i += 3)
        {
            var (ax, ay, az) = n.Lage(n.D[i]); var (bx, by, bz) = n.Lage(n.D[i + 1]); var (cx, cy, cz) = n.Lage(n.D[i + 2]);
            double nx = (by - ay) * (cz - az) - (bz - az) * (cy - ay);
            double ny = (bz - az) * (cx - ax) - (bx - ax) * (cz - az);
            double nz = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
            double l = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (l < 1e-6 || nz / l < 0.998) continue;
            if (Math.Max(az, Math.Max(bz, cz)) - Math.Min(az, Math.Min(bz, cz)) > 2) continue;
            dreiecke.Add(((az + bz + cz) / 3, new PathD { new PointD(ax, ay), new PointD(bx, by), new PointD(cx, cy) }));
        }
        var aus = new List<WaagrechteFlaeche>();
        dreiecke.Sort((a, b) => a.Z.CompareTo(b.Z));
        for (int i = 0; i < dreiecke.Count;)
        {
            int j = i + 1;
            while (j < dreiecke.Count && dreiecke[j].Z - dreiecke[j - 1].Z <= HoehenToleranz && dreiecke[j].Z - dreiecke[i].Z <= 2 * HoehenToleranz) j++;
            var pfade = new PathsD(dreiecke.Skip(i).Take(j - i).Select(d => d.P));
            double z = dreiecke.Skip(i).Take(j - i).Average(d => d.Z);
            foreach (var f in Polygone.Flaechen(Polygone.Vereinigen(pfade)))
                aus.Add(new WaagrechteFlaeche(z, f.Aussen, f.Inhalt));
            i = j;
        }
        return aus;
    }

    sealed class Region
    {
        public double Z, Inhalt;
        public List<double[]> Ring = [];
        public double[] C = [0, 0];
        public bool Podest;
        public double[] U = [1, 0];
        public double Tiefe, Breite;
        public double[]? Vorn;   // Stufenkante [x1, y1, x2, y2]
    }

    static double Median(IEnumerable<double> werte)
    {
        var l = werte.Order().ToList();
        if (l.Count == 0) return 0;
        return l.Count % 2 == 1 ? l[l.Count / 2] : (l[l.Count / 2 - 1] + l[l.Count / 2]) / 2;
    }

    static double[] Minus(double[] a, double[] b) => [a[0] - b[0], a[1] - b[1]];
    static double Skalar(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1];
    static double Betrag(double[] a) => Math.Sqrt(a[0] * a[0] + a[1] * a[1]);
    static double[] Links(double[] u) => [-u[1], u[0]];

    /// <summary>Erkennt die Treppe im Netz; null, wenn weniger als zwei Trittflächen oder keine glaubhafte Steigung (60 … 300 mm).
    /// <paramref name="geschosskoten"/>: Höhen der Geschossböden in Projekt-mm (für Antritt und Austritt).</summary>
    public static Treppenform? Aus(Dreiecksnetz netz, IReadOnlyList<double> geschosskoten, int? stufenVorgabe = null)
    {
        var roh = Waagrechte(netz).Where(r => r.Inhalt >= MinFlaeche).ToList();
        if (roh.Count < 2) return null;
        double mittel = Median(roh.Select(r => r.Inhalt));
        roh = roh.Where(r => r.Inhalt >= RauschAnteil * mittel).ToList();
        if (roh.Count < 2) return null;

        // Steigung aus den Höhenunterschieden
        static double Steigung(IEnumerable<WaagrechteFlaeche> flaechen)
        {
            var z = flaechen.Select(r => r.Z).Order().ToList();
            var d = new List<double>();
            for (int i = 1; i < z.Count; i++) if (z[i] - z[i - 1] > 20) d.Add(z[i] - z[i - 1]);
            return d.Count == 0 ? 0 : Median(d);
        }
        double h = Steigung(roh);
        if (h <= 0) return null;
        // kleine Regionen abseits des Rasters (Seitenwangen, Kanten) entfernen
        mittel = Median(roh.Select(r => r.Inhalt));
        var anker = roh.MinBy(r => Math.Abs(r.Inhalt - mittel))!.Z;
        roh = roh.Where(r =>
        {
            if (r.Inhalt >= 0.7 * mittel) return true;
            double f = (r.Z - anker) / h;
            return Math.Abs(f - Math.Round(f)) <= 0.2;
        }).ToList();
        if (roh.Count < 2) return null;
        h = Steigung(roh);
        if (h < 60 || h > 300) return null;
        mittel = Median(roh.Select(r => r.Inhalt));

        var rs = roh.OrderBy(r => r.Z).Select(r => new Region
        {
            Z = r.Z, Inhalt = r.Inhalt, Ring = r.Ring, C = Polygone.Schwerpunkt(r.Ring), Podest = r.Inhalt > PodestAnteil * mittel,
        }).ToList();
        int n = rs.Count;

        // Gehrichtung: erst die Trittflächen, dann die Podeste
        for (int i = 0; i < n; i++)
        {
            var r = rs[i];
            if (r.Podest) continue;
            var vor = i > 0 ? rs[i - 1] : null; var nach = i + 1 < n ? rs[i + 1] : null;
            double[]? v = vor is { Podest: false } ? Minus(r.C, vor.C)
                : nach is { Podest: false } ? Minus(nach.C, r.C)
                : vor is not null ? Minus(r.C, vor.C) : nach is not null ? Minus(nach.C, r.C) : null;
            var rechteck = Netzflaechen.Rechteck(r.Ring);
            double[] lang = rechteck?.Richtung ?? [1, 0], quer = Links(lang);
            double[] achse = v is null || Betrag(v) < 1 ? quer : Math.Abs(Skalar(v, lang)) > Math.Abs(Skalar(v, quer)) ? lang : quer;
            if (v is not null && Skalar(v, achse) < 0) achse = [-achse[0], -achse[1]];
            r.U = achse;
        }
        for (int i = 0; i < n; i++)
        {
            var r = rs[i];
            if (!r.Podest) continue;
            Region? vor = null, nach = null;
            for (int j = i - 1; j >= 0 && vor is null; j--) if (!rs[j].Podest) vor = rs[j];
            for (int j = i + 1; j < n && nach is null; j++) if (!rs[j].Podest) nach = rs[j];
            r.U = i > 0 && !rs[i - 1].Podest ? rs[i - 1].U : nach?.U ?? vor?.U ?? [1, 0];
        }
        foreach (var r in rs)
        {
            var l = Links(r.U);
            double u0 = r.Ring.Min(p => Skalar(p, r.U)), u1 = r.Ring.Max(p => Skalar(p, r.U));
            double q0 = r.Ring.Min(p => Skalar(p, l)), q1 = r.Ring.Max(p => Skalar(p, l));
            r.Tiefe = u1 - u0; r.Breite = q1 - q0;
        }

        // Stufenkanten (vorn)
        for (int i = 0; i < n; i++)
        {
            var r = rs[i];
            r.Vorn = Kante(r.Ring, r.U, false) ?? NaechsteKante(r, i > 0 ? rs[i - 1].C : i + 1 < n ? [2 * r.C[0] - rs[i + 1].C[0], 2 * r.C[1] - rs[i + 1].C[1]] : r.C);
            if (r.Vorn is not null) r.Vorn = Ausrichten(r.Vorn, r.U);
        }
        var letzte = rs[^1];
        // Böden: Geschosskoten und die halbe Geschosshöhe (Zwischenpodest einer zweiläufigen Treppe, Annahme)
        var koten = geschosskoten.Order().Distinct().ToList();
        var boeden = koten.Concat(koten.Zip(koten.Skip(1), (a, b) => (a + b) / 2)).ToList();
        bool amBoden(double z) => boeden.Any(k => Math.Abs(k - z) <= KoteToleranz);
        // Boden unten und oben: Optionen «Fläche ist selbst der Boden» oder «eine Steigung davor bzw. dahinter». Mit bekannter Stufenzahl
        // (Attribut der Datei) entscheidet sie, wie viele der zwei Steigungen ausserhalb der Flächen liegen; bleibt eine Wahl offen,
        // entscheiden Geschosskoten und Podest. Ohne Stufenzahl gilt: Fläche auf einer Geschosskote bzw. Podest ist der Boden.
        int m = (int)Math.Round((letzte.Z - rs[0].Z) / h);
        bool endetAufBoden, beginntAufBoden;
        if (stufenVorgabe is int vorgabe && vorgabe > 0)
        {
            int diff = vorgabe - m;
            if (diff <= 0) { beginntAufBoden = true; endetAufBoden = true; }
            else if (diff >= 2) { beginntAufBoden = false; endetAufBoden = false; }
            else
            {
                // eine Steigung ausserhalb: unten (erste Fläche = Boden, oben eine Steigung) oder oben (letzte Fläche = Boden)
                int a = (amBoden(rs[0].Z) ? 1 : 0) + (amBoden(letzte.Z + h) ? 1 : 0);
                int b = (amBoden(rs[0].Z - h) ? 1 : 0) + (amBoden(letzte.Z) || letzte.Podest ? 1 : 0);
                beginntAufBoden = a > b; endetAufBoden = !(a > b);
            }
        }
        else
        {
            endetAufBoden = letzte.Podest || amBoden(letzte.Z);
            beginntAufBoden = amBoden(rs[0].Z);
        }
        double unten = beginntAufBoden ? rs[0].Z : rs[0].Z - h;
        double oben = endetAufBoden ? letzte.Z : letzte.Z + h;
        double[]? ende = endetAufBoden ? null : Kante(letzte.Ring, letzte.U, true);
        if (ende is not null) ende = Ausrichten(ende, letzte.U);
        int stufen = stufenVorgabe is int sv && sv > 0 ? sv : Math.Max(1, (int)Math.Round((oben - unten) / h));

        var kanten = new List<double[]>();
        foreach (var r in rs) if (r.Vorn is { } v) kanten.Add([v[0], v[1], v[2], v[3], r.Z]);
        if (ende is not null) kanten.Add([ende[0], ende[1], ende[2], ende[3], oben]);

        // Lauflinie
        var tritte = rs.Where(r => !r.Podest).ToList();
        double auftritt = tritte.Count > 0 ? Median(tritte.Select(r => r.Tiefe)) : rs.Median(r => r.Tiefe);
        double breite = tritte.Count > 0 ? Median(tritte.Select(r => r.Breite)) : rs.Median(r => r.Breite);
        double einzug = Math.Min(0.3 * auftritt, 100);
        var punkte = new List<double[]>();
        if (rs[0].Vorn is { } v0)
            punkte.Add([(v0[0] + v0[2]) / 2 + rs[0].U[0] * einzug, (v0[1] + v0[3]) / 2 + rs[0].U[1] * einzug, rs[0].Z]);
        foreach (var r in rs) punkte.Add([r.C[0], r.C[1], r.Z]);
        if (ende is not null)
            punkte.Add([(ende[0] + ende[2]) / 2 - letzte.U[0] * einzug, (ende[1] + ende[3]) / 2 - letzte.U[1] * einzug, oben]);
        punkte = Vereinfachen(punkte);

        double dicke = h;
        if (netz.Begrenzung() is { } bb) dicke = Math.Clamp(Math.Round((rs[0].Z - bb[2]) / 5) * 5, h, 3 * h);

        string herleitung = $"{n} waagrechte Flächen (davon {rs.Count(r => r.Podest)} Podest), Steigung {Math.Round(h, 1)} mm (Median der Höhenunterschiede), " +
            $"Boden unten {Math.Round(unten, 1)} mm ({(beginntAufBoden ? "erste Fläche auf einem Boden (Geschosskote oder halbe Geschosshöhe)" : "eine Steigung unter der ersten Fläche")}), " +
            $"oben {Math.Round(oben, 1)} mm ({(letzte.Podest ? "letzte Fläche ist ein Podest" : endetAufBoden ? "letzte Fläche auf einem Boden (Geschosskote oder halbe Geschosshöhe)" : "eine Steigung über der letzten Fläche")})";
        return new Treppenform
        {
            Unterkante = Math.Round(unten, 1), Oberkante = Math.Round(unten + stufen * h, 1), Steigung = Math.Round(h, 1),
            Auftritt = Math.Round(auftritt, 1), Laufbreite = Math.Round(breite, 1), Laufdicke = dicke,
            Stufen = stufen, Spruenge = m, Trittflaechen = tritte.Count,
            Lauflinie = punkte, Kanten = kanten,
            Podeste = rs.Where(r => r.Podest).Select(r => new TreppePodest(Math.Round(r.Z, 1), r.Ring)).ToList(),
            Herleitung = herleitung,
        };
    }

    static double Median<T>(this IEnumerable<T> quelle, Func<T, double> f) => Median(quelle.Select(f));

    /// <summary>Rand des Rings (Gegenuhrzeigersinn), dessen Aussennormale gegen (vorn) bzw. mit (hinten) der Gehrichtung zeigt;
    /// der längste. null, wenn keiner (Keilstufe).</summary>
    static double[]? Kante(List<double[]> ring, double[] u, bool hinten)
    {
        double[]? best = null; double bestL = 80;
        for (int i = 0; i < ring.Count; i++)
        {
            var a = ring[i]; var b = ring[(i + 1) % ring.Count];
            double dx = b[0] - a[0], dy = b[1] - a[1], l = Math.Sqrt(dx * dx + dy * dy);
            if (l < bestL) continue;
            double nx = dy / l, ny = -dx / l;   // Aussennormale eines Rings im Gegenuhrzeigersinn
            double s = nx * u[0] + ny * u[1];
            if (hinten ? s >= 0.8 : s <= -0.8) { best = [a[0], a[1], b[0], b[1]]; bestL = l; }
        }
        return best;
    }

    /// <summary>Rückfall: der Rand mit der Mitte, die dem Punkt am nächsten liegt.</summary>
    static double[]? NaechsteKante(Region r, double[] punkt)
    {
        double[]? best = null; double bestD = double.MaxValue;
        for (int i = 0; i < r.Ring.Count; i++)
        {
            var a = r.Ring[i]; var b = r.Ring[(i + 1) % r.Ring.Count];
            if (Math.Sqrt((b[0] - a[0]) * (b[0] - a[0]) + (b[1] - a[1]) * (b[1] - a[1])) < 80) continue;
            double d = Math.Sqrt(Math.Pow((a[0] + b[0]) / 2 - punkt[0], 2) + Math.Pow((a[1] + b[1]) / 2 - punkt[1], 2));
            if (d < bestD) { bestD = d; best = [a[0], a[1], b[0], b[1]]; }
        }
        return best;
    }

    /// <summary>Kante von rechts nach links in Gehrichtung u.</summary>
    static double[] Ausrichten(double[] k, double[] u)
    {
        var l = Links(u);
        return (k[2] - k[0]) * l[0] + (k[3] - k[1]) * l[1] >= 0 ? k : [k[2], k[3], k[0], k[1]];
    }

    /// <summary>Innere Punkte, die weniger als die Toleranz von der Verbindung ihrer Nachbarn abweichen, entfallen.</summary>
    static List<double[]> Vereinfachen(List<double[]> p)
    {
        var aus = new List<double[]>();
        foreach (var q in p) if (aus.Count == 0 || Math.Abs(q[0] - aus[^1][0]) + Math.Abs(q[1] - aus[^1][1]) > 1) aus.Add(q);
        bool geaendert = true;
        while (geaendert && aus.Count > 2)
        {
            geaendert = false;
            for (int i = 1; i + 1 < aus.Count; i++)
            {
                var a = aus[i - 1]; var b = aus[i]; var c = aus[i + 1];
                double dx = c[0] - a[0], dy = c[1] - a[1], l = Math.Sqrt(dx * dx + dy * dy);
                double abstand = l < 1e-9 ? Math.Sqrt(Math.Pow(b[0] - a[0], 2) + Math.Pow(b[1] - a[1], 2)) : Math.Abs(dx * (b[1] - a[1]) - dy * (b[0] - a[0])) / l;
                double s = l < 1e-9 ? 0 : ((b[0] - a[0]) * dx + (b[1] - a[1]) * dy) / (l * l);
                if (abstand < GeradeToleranz && s > 0 && s < 1) { aus.RemoveAt(i); geaendert = true; break; }
            }
        }
        return aus.Select(q => new[] { Math.Round(q[0], 1), Math.Round(q[1], 1), Math.Round(q[2], 1) }).ToList();
    }
}
