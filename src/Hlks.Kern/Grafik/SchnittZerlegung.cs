// Schnitt und Ansicht (Modul 1a, C1) – Körperteile zerlegen in Schnittflächen (Lage in der Ebene w = 0) und Sichtteile
// (was in Blickrichtung zwischen w = 0 und der Sichttiefe liegt).
//
// Die Blickrichtung ist immer waagrecht; Quader drehen nur um z, Prismen sind senkrecht extrudiert. Darum sind Quader und
// Prisma beide «senkrechte Prismen»: ihre Schnittfläche besteht aus Rechtecken (Abschnitte der Grundrisslinie w = 0 mal
// unten…oben), ihre Deck- und Bodenflächen sieht man streifend (nur als Linie), sichtbar sind die Seitenflächen, deren
// Normale zum Betrachter zeigt. Zylinder: regelmässiges 48-Eck-Prisma längs der Achse für Schnitt und Umriss, Tiefe exakt.
// Kugel: Schnittkreis, Umrisskreis.
//
// Regeln:
//   - Teile mit NichtSchneiden (Räume) erscheinen weder im Schnitt noch in der Ansicht (Annahme, Auftrag C1).
//   - Schnitt (art schnitt): was die Ebene w = 0 trifft, wird Schnittfläche (je Element gesammelt, später vereinigt).
//   - Ansicht (art ansicht): keine Schnittflächen; wo ein Körper die Ebene w = 0 durchstösst, wird sein Umriss in der Ebene
//     als Sichtkante gezeichnet (Annahme: die Ansicht schneidet den Körper ab, ohne ihn als geschnitten darzustellen).
//   - Wo die Sichttiefe einen Körper abschneidet, wird die Grenze als Kante gezeichnet (Annahme, wie Revit «mit Linie»).
//   - Rohrknicke: Stirnflächen eines Zylinders, an deren Mitte eine Kugel desselben Elements sitzt (Knick, HlksKoerper),
//     sind keine Kanten – der Knick ist rund.

using Clipper2Lib;

namespace Hlks.Kern;

internal sealed partial class Schnittzerlegung(Schnittraum raum, bool schnitt)
{
    /// <summary>Sichtteile aller Körper in der Reihenfolge der Elemente und ihrer Teile.</summary>
    public List<Sichtteil> Teile { get; } = [];
    /// <summary>Rohe Schnittflächen je Element (Index), noch nicht vereinigt.</summary>
    public Dictionary<int, PathsD> Schnitte { get; } = [];
    /// <summary>Dieselben Schnittflächen als konvexe Stücke (Verdecker für die Sichtkanten).</summary>
    public List<List<P2>> Schnittstuecke { get; } = [];
    /// <summary>Für den Teil in SchnittNetz.cs (Parameter des Hauptkonstruktors sind nur in dieser Datei sichtbar).</summary>
    Schnittraum Raum => raum;
    bool IstSchnitt => schnitt;

    public void Koerper(int element, Koerper k)
    {
        var kugeln = k.Teile.Where(t => t.Art == "kugel" && t.Mitte is { Length: >= 3 } && t.Radius > 0).ToList();
        foreach (var t in k.Teile)
        {
            if (t.NichtSchneiden) continue;
            switch (t.Art)
            {
                case "quader": Quader(element, k.Ebene, t); break;
                case "prisma":
                    if (t.Grundriss is { Count: >= 3 } g)
                        Prisma(element, k.Ebene, t.Farbe, g, t.Loecher, t.Unten ?? 0, t.Oben ?? t.Unten ?? 0);
                    break;
                case "zylinder": Zylinder(element, t, kugeln); break;
                case "kugel": Kugel(element, t); break;
                case "koerper": Netz(element, t); break;   // Modul 2: allgemeines Netz (SchnittNetz.cs)
            }
        }
    }

    /// <summary>Konvexe Schnittfläche (Gegenuhrzeigersinn) eines Elements merken.</summary>
    void Schnitt(int element, List<P2> flaeche)
    {
        if (!Schnitte.TryGetValue(element, out var l)) Schnitte[element] = l = [];
        l.Add(SchnittGeometrie.Pfad(flaeche));
        Schnittstuecke.Add(flaeche);
    }

    // ------------------------------------------------------------------ Quader und Prisma

    void Quader(int element, string ebene, Koerperteil t)
    {
        if (t.Mitte is not { Length: >= 3 } m || t.Masse is not { Length: >= 3 } s || s[0] <= 0 || s[1] <= 0 || s[2] <= 0) return;
        double a = (t.Drehung ?? 0) * Math.PI / 180, c = Math.Cos(a), sn = Math.Sin(a), hx = s[0] / 2, hy = s[1] / 2;
        var ring = new List<double[]>(4);
        foreach (var (px, py) in new[] { (-hx, -hy), (hx, -hy), (hx, hy), (-hx, hy) })
            ring.Add([m[0] + px * c - py * sn, m[1] + px * sn + py * c]);
        Prisma(element, ebene, t.Farbe, ring, null, m[2] - s[2] / 2, m[2] + s[2] / 2);
    }

    /// <summary>Senkrechtes Prisma: Grundriss (Aussenring, Löcher) von unten bis oben.</summary>
    void Prisma(int element, string ebene, string? farbe, List<double[]> aussen, List<List<double[]>>? loecher, double unten, double oben)
    {
        if (oben - unten < 1e-6) return;
        var ringe = new List<List<double[]>> { Orientiert(aussen, true) };
        foreach (var l in loecher ?? []) if (l.Count >= 3) ringe.Add(Orientiert(l, false));
        var uw = ringe.Select(r => r.Select(p => (U: raum.U(p[0], p[1]), W: raum.W(p[0], p[1]))).ToList()).ToList();
        double wmin = uw.SelectMany(r => r).Min(p => p.W), wmax = uw.SelectMany(r => r).Max(p => p.W);
        if (wmax < 0 || wmin > raum.Tiefe) return;

        // Lage in der Schnittebene w = 0: Abschnitte der Grundrisslinie (gerade/ungerade Regel)
        if (wmin < 0)
        {
            var us = new List<double>();
            foreach (var r in uw)
                for (int i = 0; i < r.Count; i++)
                {
                    var a = r[i]; var b = r[(i + 1) % r.Count];
                    if ((a.W < 0) != (b.W < 0)) us.Add(a.U + (0 - a.W) / (b.W - a.W) * (b.U - a.U));
                }
            us.Sort();
            for (int i = 0; i + 1 < us.Count; i += 2)
            {
                double u0 = us[i], u1 = us[i + 1];
                if (u1 - u0 < 1e-3) continue;
                var rechteck = SchnittGeometrie.Rechteck(u0, u1, unten, oben);
                if (schnitt) { Schnitt(element, rechteck); continue; }
                var f = new Flaechenteil { Element = element, Umriss = rechteck, Farbe = farbe, Ebene = ebene, U0 = u0, W0 = 0, Steigung = 0, Nu = 0, Nw = -1, Abstand = 0 };
                RechteckKanten(f, u0, 0, u1, 0, unten, oben, true, true);
                Teile.Add(f);
            }
        }

        // Seitenflächen, deren Normale zum Betrachter zeigt (w-Anteil < 0)
        var senkrechte = new HashSet<long>();   // senkrechte Kanten je Körper nur einmal (gemeinsame Ecke zweier Flächen)
        for (int k = 0; k < ringe.Count; k++)
        {
            var xy = ringe[k]; var r = uw[k];
            for (int i = 0; i < xy.Count; i++)
            {
                var a = xy[i]; var b = xy[(i + 1) % xy.Count];
                double ex = b[0] - a[0], ey = b[1] - a[1], el = Math.Sqrt(ex * ex + ey * ey);
                if (el < 1e-9) continue;
                double nx = ey / el, ny = -ex / el;                 // rechts der Kante = aussen (Aussenring Gegenuhrzeiger, Löcher Uhrzeiger)
                double nw = nx * raum.Nx + ny * raum.Ny, nu = nx * raum.Dx + ny * raum.Dy;
                if (nw >= -1e-9) continue;                          // abgewandt oder streifend
                var pa = r[i]; var pb = r[(i + 1) % r.Count];
                double dw = pb.W - pa.W, t0 = 0, t1 = 1;
                if (Math.Abs(dw) < 1e-12) { if (pa.W < 0 || pa.W > raum.Tiefe) continue; }
                else
                {
                    double tn = (0 - pa.W) / dw, tf = (raum.Tiefe - pa.W) / dw;
                    t0 = Math.Max(0, Math.Min(tn, tf)); t1 = Math.Min(1, Math.Max(tn, tf));
                    if (t1 - t0 < 1e-12) continue;
                }
                double ua = pa.U + t0 * (pb.U - pa.U), wa = pa.W + t0 * dw, ub = pa.U + t1 * (pb.U - pa.U), wb = pa.W + t1 * dw;
                if (Math.Abs(ub - ua) < 1e-3) continue;
                var f = new Flaechenteil
                {
                    Element = element, Umriss = SchnittGeometrie.Rechteck(ua, ub, unten, oben), Farbe = farbe, Ebene = ebene,
                    U0 = ua, W0 = wa, Steigung = (wb - wa) / (ub - ua), Nu = nu, Nw = nw, Abstand = nu * pa.U + nw * pa.W,
                };
                // Senkrechte Kanten an echten Ecken und an der Sichttiefe; wo die Ebene w = 0 die Fläche abschneidet, liegt die
                // Kante auf der Schnittfläche (Schnitt) bzw. auf deren Umriss (Ansicht) und entfällt hier.
                bool kanteA = !(t0 > 0 && dw > 0), kanteB = !(t1 < 1 && dw < 0);
                kanteA = kanteA && senkrechte.Add((long)Math.Round(ua * 1000));
                kanteB = kanteB && senkrechte.Add((long)Math.Round(ub * 1000));
                RechteckKanten(f, ua, wa, ub, wb, unten, oben, kanteA, kanteB);
                Teile.Add(f);
            }
        }
    }

    /// <summary>Kanten einer senkrechten Fläche von (ua, wa) bis (ub, wb), unten…oben, mit Normalen aus der Fläche hinaus.</summary>
    static void RechteckKanten(Sichtteil f, double ua, double wa, double ub, double wb, double unten, double oben, bool senkrechtA, bool senkrechtB)
    {
        double s = Math.Sign(ub - ua);
        f.Strecken.Add(new(new(ua, unten, wa), new(ub, unten, wb), true, new(0, -1)));
        f.Strecken.Add(new(new(ua, oben, wa), new(ub, oben, wb), true, new(0, 1)));
        if (senkrechtA) f.Strecken.Add(new(new(ua, unten, wa), new(ua, oben, wa), true, new(-s, 0)));
        if (senkrechtB) f.Strecken.Add(new(new(ub, unten, wb), new(ub, oben, wb), true, new(s, 0)));
    }

    /// <summary>Ring mit gewünschter Drehrichtung im Grundriss (true = Gegenuhrzeigersinn).</summary>
    static List<double[]> Orientiert(List<double[]> ring, bool ccw)
    {
        var r = ring.Where(p => p.Length >= 2).ToList();
        if ((Polygone.Flaecheninhalt(r) > 0) != ccw) r.Reverse();
        return r;
    }

    // ------------------------------------------------------------------ Zylinder

    void Zylinder(int element, Koerperteil t, List<Koerperteil> kugeln)
    {
        if (t.Von is not { Length: >= 3 } von || t.Nach is not { Length: >= 3 } nach || t.Radius is not double rad || rad <= 0) return;
        var p0 = raum.Punkt(von); var p1 = raum.Punkt(nach);
        var achse = p1 - p0;
        double laenge = achse.Laenge;
        if (laenge < 1e-6) return;
        if (Math.Max(p0.W, p1.W) + rad < 0 || Math.Min(p0.W, p1.W) - rad > raum.Tiefe) return;
        var e = achse * (1 / laenge);
        var e1 = e.Kreuz(Math.Abs(e.W) < 0.9 ? new P3(0, 0, 1) : new P3(1, 0, 0));
        e1 *= 1 / e1.Laenge;
        var e2 = e.Kreuz(e1);
        int n = SchnittGeometrie.Teilung;
        var ring0 = new P3[n]; var ring1 = new P3[n];
        for (int k = 0; k < n; k++)
        {
            double a = 2 * Math.PI * k / n;
            var q = e1 * (rad * Math.Cos(a)) + e2 * (rad * Math.Sin(a));
            ring0[k] = p0 + q; ring1[k] = p1 + q;
        }
        // alle Kanten des Vielflachs: Ring 0, Ring 1, Mantellinien
        var kanten = new List<(P3 A, P3 B)>(3 * n);
        for (int k = 0; k < n; k++)
        {
            kanten.Add((ring0[k], ring0[(k + 1) % n]));
            kanten.Add((ring1[k], ring1[(k + 1) % n]));
            kanten.Add((ring0[k], ring1[k]));
        }
        List<P3> Durchstoss(double w)
        {
            var l = new List<P3>();
            foreach (var (a, b) in kanten)
                if ((a.W < w) != (b.W < w)) l.Add(a + (b - a) * ((w - a.W) / (b.W - a.W)));
            return l;
        }

        // Lage in der Ebene w = 0: Schnittfläche bzw. (Ansicht) Umrisskante
        List<P3>? inEbene = null;
        var d0 = Durchstoss(0);
        if (d0.Count >= 3)
        {
            var h = SchnittGeometrie.Huelle(d0.Select(p => p.Projektion).ToList()).Select(i => d0[i]).ToList();
            if (h.Count >= 3 && SchnittGeometrie.Flaeche(h.Select(p => p.Projektion).ToList()) > Polygone.Splitter) inEbene = h;
        }
        if (schnitt && inEbene is not null) Schnitt(element, inEbene.Select(p => p.Projektion).ToList());

        // Sichtbarer Teil: Ecken in der Sichtschicht und Durchstosspunkte der Kanten durch w = 0 und w = Tiefe
        var pts = new List<P3>(); var marke = new List<int>();   // 0 = Ring 0, 1 = Ring 1, 2 = Ebene w = 0, 3 = Sichttiefe
        void Dazu(IEnumerable<P3> l, int m) { foreach (var p in l) { pts.Add(p); marke.Add(m); } }
        Dazu(ring0.Where(p => p.W >= 0 && p.W <= raum.Tiefe), 0);
        Dazu(ring1.Where(p => p.W >= 0 && p.W <= raum.Tiefe), 1);
        Dazu(d0, 2);
        if (raum.Begrenzt) Dazu(Durchstoss(raum.Tiefe), 3);
        if (pts.Count < 3) return;
        var huelle = SchnittGeometrie.Huelle(pts.Select(p => p.Projektion).ToList());
        if (huelle.Count < 3) return;
        var umriss = huelle.Select(i => pts[i].Projektion).ToList();
        if (SchnittGeometrie.Flaeche(umriss) < 1e-6) return;

        bool Gelenk(double[] ende) => kugeln.Any(k =>
            Math.Abs(k.Mitte![0] - ende[0]) < 1 && Math.Abs(k.Mitte[1] - ende[1]) < 1 && Math.Abs(k.Mitte[2] - ende[2]) < 1 && k.Radius >= 0.99 * rad);
        bool gelenk0 = Gelenk(von), gelenk1 = Gelenk(nach);

        var z = new Zylinderteil { Element = element, Umriss = umriss, Farbe = t.Farbe, Anfang = p0, Richtung = e, Laenge = laenge, Radius = rad, Max = raum.Tiefe };
        var gesehen = new HashSet<(long, long, long, long)>();
        void Strecke(P3 a, P3 b, bool gerade)
        {
            var ka = Schluessel(a.Projektion); var kb = Schluessel(b.Projektion);
            if (ka == kb) return;
            if (gesehen.Add(ka.CompareTo(kb) < 0 ? (ka.Item1, ka.Item2, kb.Item1, kb.Item2) : (kb.Item1, kb.Item2, ka.Item1, ka.Item2)))
                z.Strecken.Add(new(a, b, gerade));
        }
        // Umriss ohne die Stirnkanten an Knicken; Mantellinien (Ring 0 ↔ Ring 1) sind gerade
        for (int i = 0; i < huelle.Count; i++)
        {
            int a = huelle[i], b = huelle[(i + 1) % huelle.Count];
            int ma = marke[a], mb = marke[b];
            if (ma == mb && ((ma == 0 && gelenk0) || (ma == 1 && gelenk1))) continue;
            Strecke(pts[a], pts[b], (ma == 0 && mb == 1) || (ma == 1 && mb == 0));
        }
        // Stirnflächen, die zum Betrachter zeigen (Normale −e bzw. +e mit w-Anteil < 0): ganze Ellipse
        void Ring(IReadOnlyList<P3> r) { for (int i = 0; i < r.Count; i++) Strecke(r[i], r[(i + 1) % r.Count], false); }
        if (!gelenk0 && e.W > 1e-9) Ring(SchnittGeometrie.InSchicht(ring0, raum.Tiefe));
        if (!gelenk1 && e.W < -1e-9) Ring(SchnittGeometrie.InSchicht(ring1, raum.Tiefe));
        // Ansicht: Umriss in der Ebene w = 0
        if (!schnitt && inEbene is not null) Ring(inEbene);
        Teile.Add(z);
    }

    static (long, long) Schluessel(P2 p) => ((long)Math.Round(p.U * 1000), (long)Math.Round(p.V * 1000));

    // ------------------------------------------------------------------ Kugel

    void Kugel(int element, Koerperteil t)
    {
        if (t.Mitte is not { Length: >= 3 } m || t.Radius is not double rad || rad <= 0) return;
        var c = raum.Punkt(m);
        if (c.W + rad < 0 || c.W - rad > raum.Tiefe) return;
        var mitte = c.Projektion;
        double? inEbene = Math.Abs(c.W) < rad ? Math.Sqrt(rad * rad - c.W * c.W) : null;
        if (schnitt && inEbene is double rs && rs > 0.5) Schnitt(element, SchnittGeometrie.Kreis(mitte, rs));
        // Umriss: Grosskreis in der Tiefe der Mitte, oder – wo die Sichtschicht die Kugel abschneidet – der Schnittkreis dort
        var (rho, wu) = c.W < 0 ? (Math.Sqrt(rad * rad - c.W * c.W), 0.0)
            : c.W > raum.Tiefe ? (Math.Sqrt(rad * rad - (c.W - raum.Tiefe) * (c.W - raum.Tiefe)), raum.Tiefe) : (rad, c.W);
        if (rho < 0.01) return;
        var umriss = SchnittGeometrie.Kreis(mitte, rho);
        var k = new Kugelteil { Element = element, Umriss = umriss, Farbe = t.Farbe, Mitte = c, Radius = rad, Max = raum.Tiefe };
        Kreiskanten(k, umriss, wu);
        if (!schnitt && c.W >= 0 && inEbene is double ri && ri < rho - 0.01) Kreiskanten(k, SchnittGeometrie.Kreis(mitte, ri), 0);
        Teile.Add(k);
    }

    static void Kreiskanten(Sichtteil k, List<P2> kreis, double w)
    {
        for (int i = 0; i < kreis.Count; i++)
        {
            var a = kreis[i]; var b = kreis[(i + 1) % kreis.Count];
            k.Strecken.Add(new(new(a.U, a.V, w), new(b.U, b.V, w), false));
        }
    }
}
