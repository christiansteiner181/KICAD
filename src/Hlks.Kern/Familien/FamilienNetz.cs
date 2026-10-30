// Familien (Modul 2, B1): allgemeine Netze für die Grundkörper kegel, kugel, extrusion, rotation und sweep (Koerperteil
// «koerper»: Ecken und Dreiecke, geschlossen und aussen orientiert) und die Triangulierung von Konturen mit Löchern
// (Ohrenschneiden mit Brücken nach Eberly, eigene Umsetzung).
//
// Facettierung (technisch, keine Normwerte): ganze Kreise als 48-Eck wie SchnittGeometrie.Teilung (Sehnenfehler 0.2 % des
// Radius), Bögen mit höchstens 7.5° je Strecke, Kugeln mit 48 Längen- und 24 Breitenstreifen.
// ANNAHMEN (Prüfliste): Rotation dreht rechtshändig um die Achsrichtung (+u bzw. +v), Winkel 0 = gezeichnete Kontur;
//   Sweep ohne Biegeradius mit Gehrung an den Ecken (Profil in der Winkelhalbierenden), mit Biegeradius als Kreisbogen
//   (Torusstück in Facetten); das Rechteckprofil [b, h] steht mit h senkrecht (bei waagrechtem Pfadanfang; bei
//   senkrechtem Pfadanfang h in Richtung +y) und wird ohne Verdrehung entlang des Pfads mitgeführt; ein geschlossener Pfad
//   (Anfang = Ende) wird wie ein offener mit Deckeln behandelt.

namespace Hlks.Kern;

/// <summary>Dreiecksnetz in mm. Dreiecke im Gegenuhrzeigersinn von aussen gesehen (Normale nach aussen).</summary>
public sealed class Netz3
{
    public List<double[]> Ecken { get; } = [];
    public List<int[]> Dreiecke { get; } = [];

    public int Ecke(double x, double y, double z) { Ecken.Add([x, y, z]); return Ecken.Count - 1; }
    public int Ecke(V3 p) => Ecke(p.X, p.Y, p.Z);

    /// <summary>Dreieck; entartete (gleiche Ecken) entfallen.</summary>
    public void Dreieck(int a, int b, int c)
    {
        if (a != b && b != c && a != c) Dreiecke.Add([a, b, c]);
    }

    public void Viereck(int a, int b, int c, int d) { Dreieck(a, b, c); Dreieck(a, c, d); }

    V3 P(int i) => new(Ecken[i][0], Ecken[i][1], Ecken[i][2]);

    /// <summary>Eingeschlossenes Volumen (mm³), positiv bei Normalen nach aussen.</summary>
    public double Volumen()
    {
        double v = 0;
        foreach (var t in Dreiecke) v += P(t[0]).Mal(P(t[1]).Kreuz(P(t[2])));
        return v / 6;
    }

    /// <summary>Oberfläche (mm²).</summary>
    public double Oberflaeche() => Dreiecke.Sum(t => (P(t[1]) - P(t[0])).Kreuz(P(t[2]) - P(t[0])).Laenge / 2);

    public void Umkehren() { foreach (var t in Dreiecke) (t[1], t[2]) = (t[2], t[1]); }

    /// <summary>[minx, miny, minz, maxx, maxy, maxz]</summary>
    public double[] Ausdehnung()
    {
        var a = new[] { double.MaxValue, double.MaxValue, double.MaxValue, double.MinValue, double.MinValue, double.MinValue };
        foreach (var p in Ecken)
            for (int k = 0; k < 3; k++) { a[k] = Math.Min(a[k], p[k]); a[k + 3] = Math.Max(a[k + 3], p[k]); }
        return a;
    }

    static long Kante(int a, int b) => ((long)a << 32) | (uint)b;

    /// <summary>Geschlossen: jede Kante gehört genau zwei Dreiecken, die sie gegenläufig durchlaufen. null = in Ordnung, sonst
    /// die erste Abweichung.</summary>
    public string? GeschlossenFehler()
    {
        var gerichtet = new Dictionary<long, int>();
        foreach (var t in Dreiecke)
            for (int k = 0; k < 3; k++)
            {
                var key = Kante(t[k], t[(k + 1) % 3]);
                gerichtet[key] = gerichtet.GetValueOrDefault(key) + 1;
            }
        foreach (var (key, n) in gerichtet)
        {
            int a = (int)(key >> 32), b = (int)(key & 0xffffffff);
            if (n != 1) return $"Kante {a}–{b} wird {n}-mal gleich durchlaufen";
            if (gerichtet.GetValueOrDefault(Kante(b, a)) != 1) return $"Kante {a}–{b} hat kein Gegenstück (Loch im Netz)";
        }
        return null;
    }

    /// <summary>Dreiecke einheitlich orientieren (über gemeinsame Kanten) und so, dass das Volumen positiv ist.</summary>
    public void Orientieren()
    {
        if (Dreiecke.Count == 0) return;
        var anKante = new Dictionary<(int, int), List<int>>();
        for (int i = 0; i < Dreiecke.Count; i++)
        {
            var t = Dreiecke[i];
            for (int k = 0; k < 3; k++)
            {
                int a = t[k], b = t[(k + 1) % 3];
                var key = a < b ? (a, b) : (b, a);
                if (!anKante.TryGetValue(key, out var l)) anKante[key] = l = [];
                l.Add(i);
            }
        }
        var besucht = new bool[Dreiecke.Count];
        var schlange = new Queue<int>();
        for (int start = 0; start < Dreiecke.Count; start++)
        {
            if (besucht[start]) continue;
            besucht[start] = true;
            schlange.Enqueue(start);
            while (schlange.Count > 0)
            {
                int i = schlange.Dequeue();
                var t = Dreiecke[i];
                for (int k = 0; k < 3; k++)
                {
                    int a = t[k], b = t[(k + 1) % 3];
                    foreach (var j in anKante[a < b ? (a, b) : (b, a)])
                    {
                        if (j == i || besucht[j]) continue;
                        var u = Dreiecke[j];
                        // gleich durchlaufen (a → b auch in j) heisst: j ist andersherum orientiert
                        bool gleich = (u[0] == a && u[1] == b) || (u[1] == a && u[2] == b) || (u[2] == a && u[0] == b);
                        if (gleich) (u[1], u[2]) = (u[2], u[1]);
                        besucht[j] = true;
                        schlange.Enqueue(j);
                    }
                }
            }
        }
        if (Volumen() < 0) Umkehren();
    }

    public Netz3 Transformiert(Func<double[], double[]> f)
    {
        var n = new Netz3();
        foreach (var p in Ecken) n.Ecken.Add(f(p));
        foreach (var t in Dreiecke) n.Dreiecke.Add([t[0], t[1], t[2]]);
        return n;
    }
}

/// <summary>Kleiner 3D-Vektor (mm).</summary>
public readonly record struct V3(double X, double Y, double Z)
{
    public static V3 operator +(V3 a, V3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static V3 operator -(V3 a, V3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static V3 operator *(V3 a, double s) => new(a.X * s, a.Y * s, a.Z * s);
    public static V3 operator -(V3 a) => new(-a.X, -a.Y, -a.Z);
    public double Mal(V3 b) => X * b.X + Y * b.Y + Z * b.Z;
    public V3 Kreuz(V3 b) => new(Y * b.Z - Z * b.Y, Z * b.X - X * b.Z, X * b.Y - Y * b.X);
    public double Laenge => Math.Sqrt(Mal(this));
    public V3 Einheit() { var l = Laenge; return l < 1e-12 ? this : this * (1 / l); }
    public static V3 Aus(double[] p) => new(p[0], p[1], p.Length > 2 ? p[2] : 0);
    public double[] Feld() => [X, Y, Z];

    /// <summary>Drehung um die Achse (Einheitsvektor) um den Winkel (Bogenmass), rechtshändig (Rodrigues).</summary>
    public V3 Gedreht(V3 achse, double winkel)
    {
        double c = Math.Cos(winkel), s = Math.Sin(winkel);
        return this * c + achse.Kreuz(this) * s + achse * (achse.Mal(this) * (1 - c));
    }

    /// <summary>Zwei Einheitsvektoren senkrecht zu this (Einheitsvektor), rechtshändig: e1 × e2 = this.</summary>
    public (V3 E1, V3 E2) Senkrechte()
    {
        var hilf = Math.Abs(Z) < 0.9 ? new V3(0, 0, 1) : new V3(1, 0, 0);
        var e1 = hilf.Kreuz(this).Einheit();
        var e2 = Kreuz(e1);
        return (e1, e2);
    }
}

public static class NetzBau
{
    /// <summary>Strecken je Vollkreis (wie SchnittGeometrie.Teilung).</summary>
    public const int Teilung = 48;
    /// <summary>Grösster Bogenwinkel je Strecke (Grad).</summary>
    public const double BogenSchritt = 360.0 / Teilung;
    const double Eps = 1e-9;

    // ------------------------------------------------------------------ Konturen

    /// <summary>Kontur mit Wölbungen (DXF-Bulge = tan(Öffnungswinkel/4), positiv im Gegenuhrzeigersinn) in Strecken; die Wölbung
    /// eines Punkts gilt für den Bogen zum nächsten Punkt (beim letzten Punkt zum ersten). Doppelte Punkte entfallen.</summary>
    public static List<(double U, double V)> Facetten(IReadOnlyList<(double U, double V, double W)> punkte)
    {
        var aus = new List<(double U, double V)>();
        int n = punkte.Count;
        for (int i = 0; i < n; i++)
        {
            var (u0, v0, w) = punkte[i];
            var (u1, v1, _) = punkte[(i + 1) % n];
            aus.Add((u0, v0));
            if (Math.Abs(w) < 1e-12) continue;
            double du = u1 - u0, dv = v1 - v0, l = Math.Sqrt(du * du + dv * dv);
            if (l < Eps) continue;
            double theta = 4 * Math.Atan(w);                     // Öffnungswinkel mit Vorzeichen
            double h = l / 2 / Math.Tan(theta / 2);              // Abstand Sehnenmitte → Mittelpunkt (links der Sehne bei w > 0)
            double mu = (u0 + u1) / 2 - dv / l * h, mv = (v0 + v1) / 2 + du / l * h;
            double r = Math.Sqrt((u0 - mu) * (u0 - mu) + (v0 - mv) * (v0 - mv));
            double a0 = Math.Atan2(v0 - mv, u0 - mu);
            int k = Math.Max(1, (int)Math.Ceiling(Math.Abs(theta) / (BogenSchritt * Math.PI / 180) - 1e-9));
            for (int j = 1; j < k; j++)
            {
                double a = a0 + theta * j / k;
                aus.Add((mu + r * Math.Cos(a), mv + r * Math.Sin(a)));
            }
        }
        // doppelte Punkte (auch Schluss = Anfang) entfernen
        var sauber = new List<(double U, double V)>();
        foreach (var p in aus)
            if (sauber.Count == 0 || Math.Abs(p.U - sauber[^1].U) > 1e-7 || Math.Abs(p.V - sauber[^1].V) > 1e-7) sauber.Add(p);
        while (sauber.Count > 1 && Math.Abs(sauber[0].U - sauber[^1].U) <= 1e-7 && Math.Abs(sauber[0].V - sauber[^1].V) <= 1e-7) sauber.RemoveAt(sauber.Count - 1);
        return sauber;
    }

    public static double Flaeche(IReadOnlyList<(double U, double V)> r)
    {
        double s = 0;
        for (int i = 0; i < r.Count; i++) { var a = r[i]; var b = r[(i + 1) % r.Count]; s += a.U * b.V - b.U * a.V; }
        return s / 2;
    }

    static List<(double U, double V)> Drehsinn(List<(double U, double V)> r, bool ccw)
    {
        if ((Flaeche(r) > 0) != ccw) { var k = new List<(double, double)>(r); k.Reverse(); return k; }
        return r;
    }

    // ------------------------------------------------------------------ einfache Körper

    /// <summary>Achsparalleler Quader von a nach b (für Ansichten, die alles als Netz brauchen).</summary>
    public static Netz3 Quader(double[] a, double[] b)
    {
        var n = new Netz3();
        double x0 = Math.Min(a[0], b[0]), x1 = Math.Max(a[0], b[0]), y0 = Math.Min(a[1], b[1]), y1 = Math.Max(a[1], b[1]), z0 = Math.Min(a[2], b[2]), z1 = Math.Max(a[2], b[2]);
        int[] e = [n.Ecke(x0, y0, z0), n.Ecke(x1, y0, z0), n.Ecke(x1, y1, z0), n.Ecke(x0, y1, z0), n.Ecke(x0, y0, z1), n.Ecke(x1, y0, z1), n.Ecke(x1, y1, z1), n.Ecke(x0, y1, z1)];
        n.Viereck(e[0], e[3], e[2], e[1]);   // unten
        n.Viereck(e[4], e[5], e[6], e[7]);   // oben
        n.Viereck(e[0], e[1], e[5], e[4]);   // vorne (−y)
        n.Viereck(e[1], e[2], e[6], e[5]);   // rechts (+x)
        n.Viereck(e[2], e[3], e[7], e[6]);   // hinten (+y)
        n.Viereck(e[3], e[0], e[4], e[7]);   // links (−x)
        return n;
    }

    /// <summary>Übergang zwischen zwei Profilen (30.10.2026, Christian: «es müssen alle geometrien abgebildet werden können»): Kreis
    /// (Radius) oder Rechteck (b, h) bei «von» und bei «nach», z. B. Übergang rund–eckig oder exzentrische Reduktion (Versatz über von/nach).
    /// Profile stehen senkrecht zur Achse von→nach; oben gibt die Lage der Rechteckhöhe h (Vorgabe z, bei senkrechter Achse x). Beide Ringe
    /// mit gleich vielen Punkten (48), Rechteck ab seiner Ecke gleichmässig über den Umfang, Kreis im selben Winkelbezug; Deckel als Fächer.</summary>
    public static Netz3 Uebergang(double[] von, double[] nach, double? r1, (double B, double H)? q1, double? r2, (double B, double H)? q2, double[]? oben = null)
    {
        const int N = 48;
        var p0 = V3.Aus(von); var p1 = V3.Aus(nach);
        var w = (p1 - p0).Einheit();
        var o = oben is null ? (Math.Abs(w.Z) > 0.9 ? new V3(0, 1, 0) : new V3(0, 0, 1)) : V3.Aus(oben);
        var u = (o - w * o.Mal(w)).Einheit();   // Richtung der Höhe h
        var v = w.Kreuz(u).Einheit();             // Richtung der Breite b
        List<V3> Ring(V3 m, double? r, (double B, double H)? q)
        {
            var ring = new List<V3>(N);
            for (int i = 0; i < N; i++)
            {
                double t = (double)i / N;
                if (r is double rr) { double a = Math.PI * 2 * t + Math.PI * 0.75; ring.Add(m + v * (rr * Math.Cos(a)) + u * (rr * Math.Sin(a))); continue; }
                var (b, h) = q!.Value;
                double s = t * 2 * (b + h), x, y;   // Umfang ab der Ecke (−b/2, +h/2) im Gegenuhrzeigersinn
                if (s < h) { x = -b / 2; y = h / 2 - s; }
                else if (s < h + b) { x = -b / 2 + (s - h); y = -h / 2; }
                else if (s < 2 * h + b) { x = b / 2; y = -h / 2 + (s - h - b); }
                else { x = b / 2 - (s - 2 * h - b); y = h / 2; }
                ring.Add(m + v * x + u * y);
            }
            return ring;
        }
        var n = new Netz3();
        var a0 = Ring(p0, r1, q1).Select(n.Ecke).ToArray();
        var a1 = Ring(p1, r2, q2).Select(n.Ecke).ToArray();
        for (int i = 0; i < N; i++) { int j = (i + 1) % N; n.Viereck(a0[i], a0[j], a1[j], a1[i]); }
        int c0 = n.Ecke(p0), c1 = n.Ecke(p1);
        for (int i = 0; i < N; i++) { int j = (i + 1) % N; n.Dreieck(c0, a0[j], a0[i]); n.Dreieck(c1, a1[i], a1[j]); }
        return n;
    }

    /// <summary>Kegelstumpf (auch Zylinder r1 = r2, Kegel mit Spitze r = 0) von «von» nach «nach».</summary>
    public static Netz3 Kegel(double[] von, double[] nach, double r1, double r2)
    {
        var a = V3.Aus(von); var b = V3.Aus(nach);
        var d = b - a;
        if (d.Laenge < Eps) throw new HlksFehler("Kegel: von und nach liegen aufeinander.", "Zwei verschiedene Punkte angeben.");
        if (r1 < 0 || r2 < 0 || (r1 < Eps && r2 < Eps)) throw new HlksFehler($"Kegel: Radien {r1} und {r2} ungültig.", "Radien ≥ 0, mindestens einer > 0.");
        var (e1, e2) = d.Einheit().Senkrechte();
        var n = new Netz3();
        int[] Ring(V3 m, double r)
        {
            if (r < Eps) return [n.Ecke(m)];
            var ring = new int[Teilung];
            for (int k = 0; k < Teilung; k++)
            {
                double w = 2 * Math.PI * k / Teilung;
                ring[k] = n.Ecke(m + e1 * (r * Math.Cos(w)) + e2 * (r * Math.Sin(w)));
            }
            return ring;
        }
        var unten = Ring(a, r1); var oben = Ring(b, r2);
        for (int k = 0; k < Teilung; k++)
        {
            int k1 = (k + 1) % Teilung;
            n.Viereck(unten[k % unten.Length], unten[k1 % unten.Length], oben[k1 % oben.Length], oben[k % oben.Length]);
        }
        if (unten.Length > 1) { int m = n.Ecke(a); for (int k = 0; k < Teilung; k++) n.Dreieck(m, unten[(k + 1) % Teilung], unten[k]); }
        if (oben.Length > 1) { int m = n.Ecke(b); for (int k = 0; k < Teilung; k++) n.Dreieck(m, oben[k], oben[(k + 1) % Teilung]); }
        n.Orientieren();
        return n;
    }

    public static Netz3 Kugel(double[] mitte, double r)
    {
        if (r <= 0) throw new HlksFehler($"Kugel: Radius {r} ungültig.", "Radius > 0 angeben.");
        var m = V3.Aus(mitte);
        var n = new Netz3();
        const int breiten = Teilung / 2;
        int sued = n.Ecke(m + new V3(0, 0, -r));
        var ringe = new int[breiten - 1][];
        for (int j = 1; j < breiten; j++)
        {
            double phi = -Math.PI / 2 + Math.PI * j / breiten;
            ringe[j - 1] = new int[Teilung];
            for (int k = 0; k < Teilung; k++)
            {
                double l = 2 * Math.PI * k / Teilung;
                ringe[j - 1][k] = n.Ecke(m + new V3(r * Math.Cos(phi) * Math.Cos(l), r * Math.Cos(phi) * Math.Sin(l), r * Math.Sin(phi)));
            }
        }
        int nord = n.Ecke(m + new V3(0, 0, r));
        for (int k = 0; k < Teilung; k++)
        {
            int k1 = (k + 1) % Teilung;
            n.Dreieck(sued, ringe[0][k1], ringe[0][k]);
            for (int j = 0; j < breiten - 2; j++) n.Viereck(ringe[j][k], ringe[j][k1], ringe[j + 1][k1], ringe[j + 1][k]);
            n.Dreieck(nord, ringe[^1][k], ringe[^1][k1]);
        }
        n.Orientieren();
        return n;
    }

    // ------------------------------------------------------------------ Extrusion und Rotation

    /// <summary>Basis einer Ebene: (e_u, e_v, Achse der Extrusion).</summary>
    static (V3 U, V3 V, V3 N) Basis(string ebene) => ebene switch
    {
        "xy" => (new V3(1, 0, 0), new V3(0, 1, 0), new V3(0, 0, 1)),
        "xz" => (new V3(1, 0, 0), new V3(0, 0, 1), new V3(0, 1, 0)),
        "yz" => (new V3(0, 1, 0), new V3(0, 0, 1), new V3(1, 0, 0)),
        _ => throw new HlksFehler($"Ebene «{ebene}» gibt es nicht.", "xy, xz oder yz"),
    };

    /// <summary>Kontur (mit Löchern) in der Ebene, gezogen entlang der dritten Achse von «von» bis «bis».</summary>
    public static Netz3 Extrusion(string ebene, List<(double U, double V)> kontur, List<List<(double U, double V)>> loecher, double von, double bis)
    {
        if (bis < von) (von, bis) = (bis, von);
        if (bis - von < 1e-6) throw new HlksFehler("Extrusion: von und bis sind gleich (Dicke 0).", "Verschiedene Werte für von und bis angeben.");
        if (kontur.Count < 3 || Math.Abs(Flaeche(kontur)) < 1e-6) throw new HlksFehler("Extrusion: Kontur hat keine Fläche.", "Mindestens drei Punkte, die eine Fläche umschliessen.");
        var ringe = new List<List<(double U, double V)>> { Drehsinn(kontur, true) };
        foreach (var l in loecher)
        {
            if (l.Count < 3 || Math.Abs(Flaeche(l)) < 1e-6) throw new HlksFehler("Extrusion: ein Loch hat keine Fläche.", "Loch mit mindestens drei Punkten angeben.");
            ringe.Add(Drehsinn(l, false));
        }
        var (eu, ev, en) = Basis(ebene);
        var n = new Netz3();
        var punkte2 = new List<(double X, double Y)>();
        var unten = new List<int[]>(); var oben = new List<int[]>();
        var indizes2 = new List<IReadOnlyList<int>>();
        foreach (var r in ringe)
        {
            var u = new int[r.Count]; var o = new int[r.Count]; var i2 = new int[r.Count];
            for (int i = 0; i < r.Count; i++)
            {
                var p = eu * r[i].U + ev * r[i].V;
                u[i] = n.Ecke(p + en * von);
                o[i] = n.Ecke(p + en * bis);
                i2[i] = punkte2.Count;
                punkte2.Add((r[i].U, r[i].V));
            }
            unten.Add(u); oben.Add(o); indizes2.Add(i2);
        }
        // Zuordnung 2D-Index → Ecke unten/oben
        var zuUnten = new int[punkte2.Count]; var zuOben = new int[punkte2.Count];
        for (int k = 0; k < ringe.Count; k++)
            for (int i = 0; i < ringe[k].Count; i++) { zuUnten[indizes2[k][i]] = unten[k][i]; zuOben[indizes2[k][i]] = oben[k][i]; }
        var deckel = Triangulierung.Dreiecke(punkte2, indizes2[0], indizes2.Skip(1).ToList());
        foreach (var t in deckel)
        {
            n.Dreieck(zuOben[t[0]], zuOben[t[1]], zuOben[t[2]]);
            n.Dreieck(zuUnten[t[0]], zuUnten[t[2]], zuUnten[t[1]]);
        }
        for (int k = 0; k < ringe.Count; k++)
        {
            var u = unten[k]; var o = oben[k];
            for (int i = 0; i < u.Length; i++)
            {
                int j = (i + 1) % u.Length;
                n.Viereck(u[i], u[j], o[j], o[i]);
            }
        }
        if (eu.Kreuz(ev).Mal(en) < 0) n.Umkehren();   // linkshändige Basis (xz): alles andersherum
        n.Orientieren();
        return n;
    }

    /// <summary>Kontur in der Ebene, gedreht um eine Achse in dieser Ebene (durch achsPunkt, parallel zu u oder v).</summary>
    public static Netz3 Rotation(string ebene, List<(double U, double V)> kontur, (double U, double V) achsPunkt, string richtung, double winkelGrad)
    {
        if (kontur.Count < 3 || Math.Abs(Flaeche(kontur)) < 1e-6) throw new HlksFehler("Rotation: Kontur hat keine Fläche.", "Mindestens drei Punkte, die eine Fläche umschliessen.");
        if (!(winkelGrad > 1e-6) || winkelGrad > 360 + 1e-9) throw new HlksFehler($"Rotation: Winkel {winkelGrad}° ungültig.", "Winkel grösser als 0 und höchstens 360 Grad.");
        var (eu, ev, _) = Basis(ebene);
        bool umV = richtung == "v";
        // Profil in (r, a): r = Abstand von der Achse (mit Vorzeichen), a = Lage entlang der Achse
        var prof = kontur.Select(p => umV ? (R: p.U - achsPunkt.U, A: p.V - achsPunkt.V) : (R: p.V - achsPunkt.V, A: p.U - achsPunkt.U)).ToList();
        bool plus = prof.Any(p => p.R > 1e-6), minus = prof.Any(p => p.R < -1e-6);
        if (plus && minus) throw new HlksFehler("Rotation: die Kontur liegt auf beiden Seiten der Achse.", "Kontur nur auf einer Seite der Achse zeichnen (Punkte auf der Achse sind erlaubt).");
        if (!plus && !minus) throw new HlksFehler("Rotation: die Kontur liegt auf der Achse.", "Kontur neben die Achse legen.");
        double s = plus ? 1 : -1;
        prof = prof.Select(p => (Math.Abs(p.R) < 1e-6 ? 0 : p.R * s, p.A)).ToList();
        var achse = umV ? ev : eu;
        var radial = (umV ? eu : ev) * s;
        var tangential = achse.Kreuz(radial);
        var p0 = eu * achsPunkt.U + ev * achsPunkt.V;
        bool voll = winkelGrad >= 360 - 1e-9;
        int segmente = Math.Max(1, (int)Math.Ceiling(Teilung * winkelGrad / 360 - 1e-9));
        int ringe = voll ? segmente : segmente + 1;
        double schritt = winkelGrad * Math.PI / 180 / segmente;
        var n = new Netz3();
        var ecken = new int[prof.Count][];
        for (int i = 0; i < prof.Count; i++)
        {
            var (r, a) = prof[i];
            if (r == 0) { int e = n.Ecke(p0 + achse * a); ecken[i] = Enumerable.Repeat(e, ringe).ToArray(); continue; }
            ecken[i] = new int[ringe];
            for (int k = 0; k < ringe; k++)
            {
                double w = k * schritt;
                ecken[i][k] = n.Ecke(p0 + achse * a + radial * (r * Math.Cos(w)) + tangential * (r * Math.Sin(w)));
            }
        }
        for (int i = 0; i < prof.Count; i++)
        {
            int j = (i + 1) % prof.Count;
            if (prof[i].R == 0 && prof[j].R == 0) continue;
            for (int k = 0; k < segmente; k++)
            {
                int k1 = voll ? (k + 1) % segmente : k + 1;
                n.Viereck(ecken[i][k], ecken[j][k], ecken[j][k1], ecken[i][k1]);
            }
        }
        if (!voll)
        {
            var punkte2 = prof.Select(p => (p.R, p.A)).ToList();
            var ring = Enumerable.Range(0, prof.Count).ToList();
            if (Flaeche(prof.Select(p => (p.R, p.A)).ToList()) < 0) ring.Reverse();
            foreach (var t in Triangulierung.Dreiecke(punkte2, ring, []))
            {
                n.Dreieck(ecken[t[0]][0], ecken[t[2]][0], ecken[t[1]][0]);
                n.Dreieck(ecken[t[0]][ringe - 1], ecken[t[1]][ringe - 1], ecken[t[2]][ringe - 1]);
            }
        }
        n.Orientieren();
        return n;
    }

    // ------------------------------------------------------------------ Sweep

    /// <summary>Kreis- (Radius) oder Rechteckprofil (b, h) entlang eines Pfads, optional mit Biegeradius an den Ecken.</summary>
    public static Netz3 Sweep(IReadOnlyList<double[]> pfad, double? kreis, (double B, double H)? rechteck, double? biegeradius)
    {
        var pts = new List<V3>();
        foreach (var p in pfad.Select(V3.Aus))
            if (pts.Count == 0 || (p - pts[^1]).Laenge > 1e-6) pts.Add(p);
        if (pts.Count < 2) throw new HlksFehler("Sweep: der Pfad hat keine Länge.", "Mindestens zwei verschiedene Punkte angeben.");
        // Profil im eigenen System (s quer = b, t hoch = h), Gegenuhrzeigersinn
        var profil = new List<(double S, double T)>();
        if (kreis is double r)
        {
            if (r <= 0) throw new HlksFehler($"Sweep: Kreisradius {r} ungültig.", "Radius > 0 angeben.");
            for (int k = 0; k < Teilung; k++) profil.Add((r * Math.Cos(2 * Math.PI * k / Teilung), r * Math.Sin(2 * Math.PI * k / Teilung)));
        }
        else if (rechteck is { } q)
        {
            if (q.B <= 0 || q.H <= 0) throw new HlksFehler($"Sweep: Rechteck {q.B} × {q.H} ungültig.", "b und h > 0 angeben.");
            profil.AddRange([(-q.B / 2, -q.H / 2), (q.B / 2, -q.H / 2), (q.B / 2, q.H / 2), (-q.B / 2, q.H / 2)]);
        }
        else throw new HlksFehler("Sweep: Profil fehlt.", "kreis oder rechteck angeben.");
        if (biegeradius is double rb && rb > 0) pts = Abrunden(pts, rb);

        var d0 = (pts[1] - pts[0]).Einheit();
        var hoch = Math.Abs(d0.Z) < 0.999 ? new V3(0, 0, 1) : new V3(0, 1, 0);
        var eh = (hoch - d0 * hoch.Mal(d0)).Einheit();
        var eb = eh.Kreuz(d0);
        var n = new Netz3();
        var ringe = new List<int[]>();
        int[] Ring(V3 mitte, V3 b, V3 h, V3 richtung, V3 ebene)
        {
            var ring = new int[profil.Count];
            for (int k = 0; k < profil.Count; k++)
            {
                var q = b * profil[k].S + h * profil[k].T;
                double s = -q.Mal(ebene) / richtung.Mal(ebene);   // entlang der Richtung in die (Gehrungs-)Ebene
                ring[k] = n.Ecke(mitte + q + richtung * s);
            }
            return ring;
        }
        ringe.Add(Ring(pts[0], eb, eh, d0, d0));
        var dIn = d0;
        for (int i = 1; i < pts.Count - 1; i++)
        {
            var dOut = (pts[i + 1] - pts[i]).Einheit();
            var m = dIn + dOut;
            if (m.Laenge < 1e-6) throw new HlksFehler("Sweep: der Pfad kehrt um (Winkel 180°).", "Pfadpunkte prüfen.");
            ringe.Add(Ring(pts[i], eb, eh, dIn, m.Einheit()));
            // Bezugssystem ohne Verdrehung mitführen (Drehung dIn → dOut)
            var achse = dIn.Kreuz(dOut);
            if (achse.Laenge > 1e-12)
            {
                var w = Math.Atan2(achse.Laenge, dIn.Mal(dOut));
                achse = achse.Einheit();
                eb = eb.Gedreht(achse, w); eh = eh.Gedreht(achse, w);
            }
            dIn = dOut;
        }
        ringe.Add(Ring(pts[^1], eb, eh, dIn, dIn));
        for (int i = 0; i + 1 < ringe.Count; i++)
            for (int k = 0; k < profil.Count; k++)
            {
                int k1 = (k + 1) % profil.Count;
                n.Viereck(ringe[i][k], ringe[i][k1], ringe[i + 1][k1], ringe[i + 1][k]);
            }
        var punkte2 = profil.Select(p => (p.S, p.T)).ToList();
        foreach (var t in Triangulierung.Dreiecke(punkte2, Enumerable.Range(0, profil.Count).ToList(), []))
        {
            n.Dreieck(ringe[0][t[0]], ringe[0][t[2]], ringe[0][t[1]]);
            n.Dreieck(ringe[^1][t[0]], ringe[^1][t[1]], ringe[^1][t[2]]);
        }
        n.Orientieren();
        return n;
    }

    /// <summary>Ecken eines Pfads durch Kreisbögen mit Radius r ersetzen (Bogen in Strecken ≤ 7.5°).</summary>
    static List<V3> Abrunden(List<V3> pts, double r)
    {
        if (pts.Count < 3) return pts;
        var tangente = new double[pts.Count];
        for (int i = 1; i < pts.Count - 1; i++)
        {
            var dIn = (pts[i] - pts[i - 1]).Einheit(); var dOut = (pts[i + 1] - pts[i]).Einheit();
            double alpha = Math.Acos(Math.Clamp(dIn.Mal(dOut), -1, 1));   // Umlenkwinkel
            if (alpha > Math.PI - 1e-6) throw new HlksFehler("Sweep: der Pfad kehrt um (Winkel 180°).", "Pfadpunkte prüfen.");
            tangente[i] = r * Math.Tan(alpha / 2);
        }
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            double l = (pts[i + 1] - pts[i]).Laenge;
            if (tangente[i] + tangente[i + 1] > l + 1e-6)
                throw new HlksFehler($"Sweep: Biegeradius {r} mm ist zu gross für das Pfadstück {i + 1}–{i + 2} ({l:0.#} mm).", "Kleineren Biegeradius wählen oder die Pfadstücke verlängern.");
        }
        var aus = new List<V3> { pts[0] };
        for (int i = 1; i < pts.Count - 1; i++)
        {
            if (tangente[i] < 1e-9) { aus.Add(pts[i]); continue; }
            var dIn = (pts[i] - pts[i - 1]).Einheit(); var dOut = (pts[i + 1] - pts[i]).Einheit();
            double alpha = Math.Acos(Math.Clamp(dIn.Mal(dOut), -1, 1));
            var t1 = pts[i] - dIn * tangente[i];
            var mitte = t1 + (dOut - dIn * dIn.Mal(dOut)).Einheit() * r;   // senkrecht zu dIn, zur Innenseite
            var achse = dIn.Kreuz(dOut).Einheit();
            int k = Math.Max(1, (int)Math.Ceiling(alpha * 180 / Math.PI / BogenSchritt - 1e-9));
            for (int j = 0; j <= k; j++) aus.Add(mitte + (t1 - mitte).Gedreht(achse, alpha * j / k));
        }
        aus.Add(pts[^1]);
        var sauber = new List<V3>();
        foreach (var p in aus) if (sauber.Count == 0 || (p - sauber[^1]).Laenge > 1e-6) sauber.Add(p);
        return sauber;
    }
}

/// <summary>Triangulierung einfacher Polygone mit Löchern: Löcher über Brücken an den Aussenring hängen (Eberly), dann
/// Ohren abschneiden. Ergebnis im Gegenuhrzeigersinn, Indizes in die Punktliste.</summary>
public static class Triangulierung
{
    const double Eps = 1e-12;

    public static List<int[]> Dreiecke(IReadOnlyList<(double X, double Y)> p, IReadOnlyList<int> aussen, IReadOnlyList<IReadOnlyList<int>> loecher)
    {
        double Flaeche(IReadOnlyList<int> r)
        {
            double s = 0;
            for (int i = 0; i < r.Count; i++) { var a = p[r[i]]; var b = p[r[(i + 1) % r.Count]]; s += a.X * b.Y - b.X * a.Y; }
            return s / 2;
        }
        var ring = aussen.ToList();
        if (Flaeche(ring) < 0) ring.Reverse();
        var loch = loecher.Select(l => { var r = l.ToList(); if (Flaeche(r) > 0) r.Reverse(); return r; }).Where(r => r.Count >= 3).ToList();
        foreach (var l in loch.OrderByDescending(l => l.Max(i => p[i].X)))
            ring = Bruecke(p, ring, l);
        return Ohren(p, ring);
    }

    static double Kreuz((double X, double Y) a, (double X, double Y) b, (double X, double Y) c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

    /// <summary>Loch über die Brücke vom rechtesten Lochpunkt zu einem sichtbaren Punkt des Rings einhängen.</summary>
    static List<int> Bruecke(IReadOnlyList<(double X, double Y)> p, List<int> ring, List<int> loch)
    {
        int mi = 0;
        for (int i = 1; i < loch.Count; i++) if (p[loch[i]].X > p[loch[mi]].X || (p[loch[i]].X == p[loch[mi]].X && p[loch[i]].Y < p[loch[mi]].Y)) mi = i;
        var m = p[loch[mi]];
        // nächster Schnitt des Strahls nach +x mit dem Ring
        double bestX = double.MaxValue; int kante = -1;
        for (int i = 0; i < ring.Count; i++)
        {
            var a = p[ring[i]]; var b = p[ring[(i + 1) % ring.Count]];
            if ((a.Y > m.Y) == (b.Y > m.Y) && !(a.Y == m.Y && b.Y == m.Y)) continue;
            if (Math.Abs(b.Y - a.Y) < Eps) continue;
            double x = a.X + (m.Y - a.Y) / (b.Y - a.Y) * (b.X - a.X);
            if (x < m.X - 1e-9 || x >= bestX) continue;
            bestX = x; kante = i;
        }
        int sicht;
        if (kante < 0) sicht = Enumerable.Range(0, ring.Count).OrderBy(i => Math.Pow(p[ring[i]].X - m.X, 2) + Math.Pow(p[ring[i]].Y - m.Y, 2)).First();
        else
        {
            int ia = kante, ib = (kante + 1) % ring.Count;
            sicht = p[ring[ia]].X > p[ring[ib]].X ? ia : ib;
            var punktI = (X: bestX, Y: m.Y);
            var ps = p[ring[sicht]];
            if (!(Math.Abs(ps.X - bestX) < 1e-9 && Math.Abs(ps.Y - m.Y) < 1e-9))
            {
                // einspringende Ecken im Dreieck (M, I, P): die mit dem kleinsten Winkel zur Strahlrichtung nehmen
                double bestW = double.MaxValue, bestD = double.MaxValue;
                for (int i = 0; i < ring.Count; i++)
                {
                    if (i == sicht) continue;
                    var q = p[ring[i]];
                    var vor = p[ring[(i - 1 + ring.Count) % ring.Count]]; var nach = p[ring[(i + 1) % ring.Count]];
                    if (Kreuz(vor, q, nach) > 0) continue;   // konvex
                    if (!ImDreieck(m, punktI, ps, q)) continue;
                    double w = Math.Abs(Math.Atan2(q.Y - m.Y, q.X - m.X)), d = Math.Pow(q.X - m.X, 2) + Math.Pow(q.Y - m.Y, 2);
                    if (w < bestW - 1e-12 || (Math.Abs(w - bestW) <= 1e-12 && d < bestD)) { bestW = w; bestD = d; sicht = i; }
                }
            }
        }
        var aus = new List<int>(ring.Count + loch.Count + 2);
        for (int i = 0; i <= sicht; i++) aus.Add(ring[i]);
        for (int k = 0; k <= loch.Count; k++) aus.Add(loch[(mi + k) % loch.Count]);
        aus.Add(ring[sicht]);
        for (int i = sicht + 1; i < ring.Count; i++) aus.Add(ring[i]);
        return aus;
    }

    static bool ImDreieck((double X, double Y) a, (double X, double Y) b, (double X, double Y) c, (double X, double Y) q)
    {
        double d1 = Kreuz(a, b, q), d2 = Kreuz(b, c, q), d3 = Kreuz(c, a, q);
        bool neg = d1 < -1e-12 || d2 < -1e-12 || d3 < -1e-12, pos = d1 > 1e-12 || d2 > 1e-12 || d3 > 1e-12;
        return !(neg && pos);
    }

    static List<int[]> Ohren(IReadOnlyList<(double X, double Y)> p, List<int> ring)
    {
        var aus = new List<int[]>();
        var r = new List<int>(ring);
        int schutz = r.Count * r.Count + 10;
        while (r.Count > 3 && schutz-- > 0)
        {
            int n = r.Count;
            int ohr = -1;
            for (int i = 0; i < n; i++)
            {
                int ia = r[(i - 1 + n) % n], ib = r[i], ic = r[(i + 1) % n];
                var a = p[ia]; var b = p[ib]; var c = p[ic];
                double k = Kreuz(a, b, c);
                if (k <= Eps) continue;   // einspringend oder gestreckt
                bool frei = true;
                for (int j = 0; j < n && frei; j++)
                {
                    int iq = r[j];
                    if (iq == ia || iq == ib || iq == ic) continue;
                    var q = p[iq];
                    if ((q.X == a.X && q.Y == a.Y) || (q.X == b.X && q.Y == b.Y) || (q.X == c.X && q.Y == c.Y)) continue;
                    if (StriktImDreieck(a, b, c, q)) frei = false;
                }
                if (frei) { ohr = i; break; }
            }
            if (ohr < 0)
            {
                // Rundungsfälle: gestreckte Ecke als flaches Dreieck abschneiden (das Netz bleibt geschlossen), sonst die
                // konvexeste Ecke nehmen (Schutz vor Endlosschleife)
                ohr = Enumerable.Range(0, n).FirstOrDefault(i => Math.Abs(Kreuz(p[r[(i - 1 + n) % n]], p[r[i]], p[r[(i + 1) % n]])) <= 1e-9, -1);
                if (ohr < 0) ohr = Enumerable.Range(0, n).OrderByDescending(i => Kreuz(p[r[(i - 1 + n) % n]], p[r[i]], p[r[(i + 1) % n]])).First();
            }
            aus.Add([r[(ohr - 1 + n) % n], r[ohr], r[(ohr + 1) % n]]);
            r.RemoveAt(ohr);
        }
        if (r.Count == 3) aus.Add([r[0], r[1], r[2]]);
        return aus;
    }

    /// <summary>Im Dreieck oder auf seinem Rand (ohne die Ecken) – Punkte auf dem Rand verhindern ein Ohr ebenfalls.</summary>
    static bool StriktImDreieck((double X, double Y) a, (double X, double Y) b, (double X, double Y) c, (double X, double Y) q)
    {
        double d1 = Kreuz(a, b, q), d2 = Kreuz(b, c, q), d3 = Kreuz(c, a, q);
        return d1 >= -1e-12 && d2 >= -1e-12 && d3 >= -1e-12;
    }
}
