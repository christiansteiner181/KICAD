// Quest App P4b (Modul XR, Schnittstelle für Fachmodule): eine gelesene Ebene (XrEbene, KICAD-mm) als Geometrie in der Szene der App (m, y oben, relativ zum
// Bezugspunkt; KicadUmrechnung). Rein, ohne StereoKit – die App zeichnet daraus Linien (Lines.Add), Kugeln und ein Netz für die Flächen.
//
//   linie    → Striche entlang der Polylinie
//   pfeil    → Striche der Polylinie und eine Spitze aus zwei Strichen am letzten Punkt (Länge PfeilSpitze, waagrecht aufgespannt; senkrechte Pfeile in x)
//   punkte   → Kugeln
//   partikel → Bahn mit kumulierten Längen; Partikel(t) liefert die Lagen der Kugeln zur Zeit t (laufen mit tempo, im Abstand abstand)
//   flaeche  → Dreiecke (Ohrenschneiden in der Ebene des Vielecks; bei entarteten Vielecken ein Fächer)
// ANNAHMEN P4b (Aussehen, im Labor P5 nachzustellen): Spitze 40 % des letzten Abschnitts, höchstens 6 × Breite, mindestens 8 cm; halb so breit wie lang.

using Hlks.Xr.Netz;

namespace Hlks.Xr.Ebenen;

/// <summary>Ein Strich (Szene, m).</summary>
public readonly record struct EbenenStrich(Vec3 A, Vec3 B);

/// <summary>Ein Element einer Ebene in der Szene: Art (Typ des Formats), Bezug, Farbe RGBA, Breite bzw. Durchmesser (m), Geometrie, Hüllbox.</summary>
public sealed class EbenenTeil
{
    public required string Typ { get; init; }
    public string? Geschoss { get; init; }
    public string? Element { get; init; }
    public required float[] Farbe { get; init; }
    /// <summary>Strichbreite (linie, pfeil) bzw. Durchmesser (punkte, partikel), m.</summary>
    public double Breite { get; init; }
    public IReadOnlyList<EbenenStrich> Striche { get; init; } = [];
    public IReadOnlyList<Vec3> Punkte { get; init; } = [];
    /// <summary>Partikel: Bahn, kumulierte Längen (m), Tempo (m/s), Abstand (m).</summary>
    public IReadOnlyList<Vec3> Bahn { get; init; } = [];
    public IReadOnlyList<double> Laengen { get; init; } = [];
    public double Tempo { get; init; }
    public double Abstand { get; init; }
    /// <summary>Fläche: Ecken und Dreiecke (je drei Indizes).</summary>
    public IReadOnlyList<int> Dreiecke { get; init; } = [];
    public required Box3 Box { get; init; }

    public double BahnLaenge => Laengen.Count > 0 ? Laengen[^1] : 0;

    /// <summary>Lagen der Partikel zur Zeit t (s): Start bei (t · Tempo) modulo Abstand, dann alle Abstand bis ans Ende; höchstens max.</summary>
    public IEnumerable<Vec3> Partikel(double t, int max = EbenenSzene.PartikelJeBahnMax)
    {
        var l = BahnLaenge;
        if (Bahn.Count < 2 || !(l > 0) || !(Abstand > 0)) yield break;
        var s = Tempo > 0 && double.IsFinite(t) ? (t * Tempo) % Abstand : 0;
        if (s < 0) s += Abstand;
        int k = 0, seg = 1;
        for (; s <= l && k < max; s += Abstand, k++)
        {
            while (seg < Laengen.Count - 1 && Laengen[seg] < s) seg++;
            double a = Laengen[seg - 1], b = Laengen[seg];
            var f = b > a ? (s - a) / (b - a) : 0;
            yield return Bahn[seg - 1] + (Bahn[seg] - Bahn[seg - 1]) * f;
        }
    }
}

/// <summary>Eine Ebene in der Szene.</summary>
public sealed class EbenenSzene
{
    public const double SpitzeAnteil = 0.4, SpitzeMinM = 0.08, SpitzeBreiten = 6;
    public const int PartikelJeBahnMax = 200;

    public required string Name { get; init; }
    public required string Titel { get; init; }
    public string? Quelle { get; init; }
    public required string Schluessel { get; init; }
    public required IReadOnlyList<EbenenTeil> Teile { get; init; }

    public int Striche => Teile.Sum(t => t.Striche.Count);

    /// <summary>Baut die Ebene in der Szene (Bezugspunkt der Szene, KICAD-mm).</summary>
    public static EbenenSzene Bauen(XrEbene e, double[]? bezug)
    {
        var teile = new List<EbenenTeil>();
        foreach (var el in e.Elemente)
        {
            var p = el.PunkteMm.Select(x => KicadUmrechnung.PunktAusKicad(x[0], x[1], x[2], bezug)).ToList();
            var box = Box3.Von(p);
            var breite = el.BreiteMm / 1000;
            switch (el.Typ)
            {
                case XrEbeneFormat.Linie:
                    teile.Add(new EbenenTeil { Typ = el.Typ, Geschoss = el.Geschoss, Element = el.Element, Farbe = el.Farbe, Breite = breite, Striche = Polylinie(p), Box = box });
                    break;
                case XrEbeneFormat.Pfeil:
                    var striche = Polylinie(p);
                    striche.AddRange(Spitze(p[^2], p[^1], breite));
                    teile.Add(new EbenenTeil { Typ = el.Typ, Geschoss = el.Geschoss, Element = el.Element, Farbe = el.Farbe, Breite = breite, Striche = striche, Box = box });
                    break;
                case XrEbeneFormat.Punkte:
                    teile.Add(new EbenenTeil { Typ = el.Typ, Geschoss = el.Geschoss, Element = el.Element, Farbe = el.Farbe, Breite = el.GroesseMm / 1000, Punkte = p, Box = box });
                    break;
                case XrEbeneFormat.Partikel:
                    var laengen = new List<double> { 0 };
                    for (int i = 1; i < p.Count; i++) laengen.Add(laengen[^1] + p[i].Abstand(p[i - 1]));
                    teile.Add(new EbenenTeil
                    {
                        Typ = el.Typ, Geschoss = el.Geschoss, Element = el.Element, Farbe = el.Farbe, Breite = el.GroesseMm / 1000, Bahn = p, Laengen = laengen,
                        Tempo = el.TempoMmS / 1000, Abstand = el.AbstandMm / 1000, Box = box,
                    });
                    break;
                case XrEbeneFormat.Flaeche:
                    teile.Add(new EbenenTeil { Typ = el.Typ, Geschoss = el.Geschoss, Element = el.Element, Farbe = el.Farbe, Punkte = p, Dreiecke = Dreieckszerlegung(p), Box = box });
                    break;
            }
        }
        return new EbenenSzene { Name = e.Name, Titel = e.Titel, Quelle = e.Quelle, Schluessel = e.Schluessel, Teile = teile };
    }

    static List<EbenenStrich> Polylinie(IReadOnlyList<Vec3> p)
    {
        var s = new List<EbenenStrich>();
        for (int i = 1; i < p.Count; i++) if (p[i].Abstand(p[i - 1]) > 1e-9) s.Add(new EbenenStrich(p[i - 1], p[i]));
        return s;
    }

    /// <summary>Die zwei Striche der Pfeilspitze am Punkt b (Richtung a → b), siehe Dateikopf.</summary>
    public static IReadOnlyList<EbenenStrich> Spitze(Vec3 a, Vec3 b, double breite)
    {
        var d = b - a;
        var l = d.Laenge;
        if (!(l > 1e-9)) return [];
        var r = d * (1 / l);
        var h = Math.Min(l * SpitzeAnteil, Math.Max(SpitzeMinM, SpitzeBreiten * breite));
        var seite = new Vec3(-r.Z, 0, r.X);
        seite = seite.Laenge > 1e-6 ? seite.Einheit() : new Vec3(1, 0, 0);
        var basis = b - r * h;
        return [new EbenenStrich(b, basis + seite * (h / 2)), new EbenenStrich(b, basis - seite * (h / 2))];
    }

    /// <summary>Dreiecke eines ebenen Vielecks (Indizes in die Ecken): Ohrenschneiden in der Projektion auf die Ebene mit der grössten Fläche (Newell), sonst Fächer.</summary>
    public static int[] Dreieckszerlegung(IReadOnlyList<Vec3> p)
    {
        int n = p.Count;
        if (n < 3) return [];
        double nx = 0, ny = 0, nz = 0;
        for (int i = 0; i < n; i++)
        {
            var a = p[i]; var b = p[(i + 1) % n];
            nx += (a.Y - b.Y) * (a.Z + b.Z); ny += (a.Z - b.Z) * (a.X + b.X); nz += (a.X - b.X) * (a.Y + b.Y);
        }
        // Projektion: die Achse mit der grössten Normalenkomponente weglassen
        Func<Vec3, (double, double)> pr = Math.Abs(nx) >= Math.Abs(ny) && Math.Abs(nx) >= Math.Abs(nz) ? v => (v.Y, v.Z)
            : Math.Abs(ny) >= Math.Abs(nz) ? v => (v.Z, v.X) : v => (v.X, v.Y);
        var q = p.Select(pr).ToArray();
        double flaeche = 0;
        for (int i = 0; i < n; i++) flaeche += q[i].Item1 * q[(i + 1) % n].Item2 - q[(i + 1) % n].Item1 * q[i].Item2;
        if (Math.Abs(flaeche) < 1e-12) return Faecher(n);
        var ccw = flaeche > 0;
        var rest = Enumerable.Range(0, n).ToList();
        var aus = new List<int>();
        int schutz = n * n;
        while (rest.Count > 3 && schutz-- > 0)
        {
            bool geschnitten = false;
            for (int i = 0; i < rest.Count; i++)
            {
                int ia = rest[(i + rest.Count - 1) % rest.Count], ib = rest[i], ic = rest[(i + 1) % rest.Count];
                var (a, b, c) = (q[ia], q[ib], q[ic]);
                var kreuz = (b.Item1 - a.Item1) * (c.Item2 - a.Item2) - (b.Item2 - a.Item2) * (c.Item1 - a.Item1);
                if (ccw ? kreuz <= 1e-15 : kreuz >= -1e-15) continue;   // keine konvexe Ecke
                bool drin = false;
                foreach (var j in rest)
                {
                    if (j == ia || j == ib || j == ic) continue;
                    if (InDreieck(q[j], a, b, c)) { drin = true; break; }
                }
                if (drin) continue;
                aus.AddRange([ia, ib, ic]);
                rest.RemoveAt(i);
                geschnitten = true;
                break;
            }
            if (!geschnitten) return Faecher(n);
        }
        if (rest.Count == 3) aus.AddRange(rest);
        return [.. aus];
    }

    static int[] Faecher(int n)
    {
        var aus = new List<int>();
        for (int i = 1; i + 1 < n; i++) aus.AddRange([0, i, i + 1]);
        return [.. aus];
    }

    static bool InDreieck((double, double) p, (double, double) a, (double, double) b, (double, double) c)
    {
        double d1 = (p.Item1 - b.Item1) * (a.Item2 - b.Item2) - (a.Item1 - b.Item1) * (p.Item2 - b.Item2);
        double d2 = (p.Item1 - c.Item1) * (b.Item2 - c.Item2) - (b.Item1 - c.Item1) * (p.Item2 - c.Item2);
        double d3 = (p.Item1 - a.Item1) * (c.Item2 - a.Item2) - (c.Item1 - a.Item1) * (p.Item2 - a.Item2);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(neg && pos);
    }
}
