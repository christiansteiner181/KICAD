// Schnitt und Ansicht (Modul 1a, C1) – Verdeckung: welche Teile der Kanten man wirklich sieht.
//
// Verfahren (gleiches Ergebnis wie das Malerverfahren «von hinten nach vorn deckend füllen», aber im Kern gerechnet und
// exakt auch dort, wo Körper einander durchdringen, z. B. ein Rohr schräg durch eine Wand):
//   Ein Punkt X = (u, v, w) einer Kante ist verdeckt, wenn er
//   a) in einer Schnittfläche liegt (die liegt in w = 0, also vor allem), oder
//   b) im Umriss eines anderen Sichtteils liegt und hinter dessen vorderer Oberfläche: bei ebenen Flächen exakt über
//      ihre Ebene (die Grenze ist linear entlang der Kante), bei Zylinder und Kugel über die exakte Tiefe (die vordere
//      Oberfläche eines konvexen Körpers ist konvex, der verdeckte Teil einer Strecke also ein Abschnitt: abtasten und
//      die Grenzen einschachteln), oder
//   c) als Naht zwischen Flächen in derselben Ebene liegt (gleiche Zeichenebene, z. B. Wandstücke neben einer Öffnung,
//      Wandecken, Geschosswände übereinander): auch die Gegenseite der Kante ist von einer Fläche der Gruppe belegt; oder
//      wenn eine frühere Fläche der Gruppe dieselbe Kante schon zeichnet.
//   d) (Modul 2) bei Netzen (Familienkörper, nicht unbedingt konvex) zusätzlich hinter der eigenen vorderen Oberfläche liegt
//      (exakte Tiefe aus dem Strahlschnitt, SchnittNetz.cs).
//   Gerechnet wird je Kante mit Parameterabschnitten t ∈ [0, 1] (Cyrus–Beck gegen die konvexen Umrisse).
// Warum nicht deckend füllen: Bildschirm und PDF zeichnen die Grafikteile nach Zeichenebenen geordnet (Ebenen.Reihenfolge),
// nicht in der Tiefe; Füllungen von hinten nach vorn würden dort durcheinandergeraten. Reine Kanten sind ausserdem für
// DWG sauberer.
// Technische Toleranzen (keine Normwerte): Rand 0.1 mm (Kanten auf dem Umriss eines vorderen Teils gelten als verdeckt),
// Tiefenunterschied unter 0.5 mm gilt als gleichauf (nicht verdeckt), Kantenreste unter 0.3 mm fallen weg.

namespace Hlks.Kern;

internal static class Schnittverdeckung
{
    public const double Rand = 0.1;
    const double Gleichauf = 0.5;
    const double MinRest = 3 * Rand;
    const int Proben = 16;          // Abtastungen je gekrümmtem Verdecker und Strecke
    const int Schachtelung = 24;    // Halbierungen für die Grenze

    /// <summary>Sichtbares Stück einer Kante in (u, v).</summary>
    public readonly record struct Stueck(P2 A, P2 B, bool Gerade);

    /// <summary>Sichtbare Kantenstücke je Sichtteil (gleiche Reihenfolge wie <paramref name="teile"/>).</summary>
    public static List<List<Stueck>> Rechnen(List<Sichtteil> teile, IReadOnlyList<List<P2>> schnitte)
    {
        for (int i = 0; i < teile.Count; i++) { teile[i].Nummer = i; teile[i].Begrenzen(); }
        var gruppen = Gruppen(teile);
        var schnittBox = schnitte.Select(s => SchnittGeometrie.Box(s)).ToList();
        var folge = teile.OrderBy(t => t.Box[0]).ToList();
        var aus = new List<List<Stueck>>(teile.Count);
        var flaeche = teile.Select(t => Math.Abs(SchnittGeometrie.Flaeche(t.Umriss))).ToArray();
        foreach (var t in teile)
        {
            // Kandidaten: alles, dessen Umriss den eigenen (mit Rand) berührt; grosse ebene Flächen zuerst, weil sie am
            // meisten verdecken und eine ganz verdeckte Kante nicht weiter geprüft wird
            var nachbarn = new List<Sichtteil>();
            foreach (var o in folge)
            {
                if (o.Box[0] > t.Box[2] + 2 * Rand) break;
                if (!ReferenceEquals(o, t) && SchnittGeometrie.Ueberlappt(o.Box, t.Box, 2 * Rand)) nachbarn.Add(o);
            }
            nachbarn.Sort((x, y) => (y is Flaechenteil).CompareTo(x is Flaechenteil) is var r && r != 0 ? r : flaeche[y.Nummer].CompareTo(flaeche[x.Nummer]));
            var schnittNah = Enumerable.Range(0, schnitte.Count).Where(k => SchnittGeometrie.Ueberlappt(schnittBox[k], t.Box, 2 * Rand)).Select(k => schnitte[k]).ToList();
            var gruppe = t.Gruppe >= 0 && gruppen[t.Gruppe].Count >= 2 ? gruppen[t.Gruppe] : null;
            var stuecke = new List<Stueck>();
            foreach (var s in t.Strecken) Sichtbar(t, s, nachbarn, schnittNah, gruppe, stuecke);
            aus.Add(stuecke);
        }
        return aus;
    }

    // ------------------------------------------------------------------ Gruppen gleich liegender Flächen

    static List<List<Sichtteil>> Gruppen(List<Sichtteil> teile)
    {
        var liste = new List<List<Sichtteil>>();
        var nachLage = new Dictionary<(string, long, long), List<(double Abstand, int Nummer)>>();
        foreach (var t in teile)
        {
            if (t is not Flaechenteil f) continue;
            var schluessel = (f.Ebene, (long)Math.Round(f.Nu * 1e6), (long)Math.Round(f.Nw * 1e6));
            if (!nachLage.TryGetValue(schluessel, out var l)) nachLage[schluessel] = l = [];
            int nummer = -1;
            foreach (var (abstand, n) in l)
                if (Math.Abs(abstand - f.Abstand) <= Gleichauf) { nummer = n; break; }
            if (nummer < 0)
            {
                nummer = liste.Count;
                liste.Add([]);
                l.Add((f.Abstand, nummer));
            }
            f.Gruppe = nummer;
            liste[nummer].Add(f);
        }
        return liste;
    }

    // ------------------------------------------------------------------ eine Kante

    static void Sichtbar(Sichtteil t, Strecke s, List<Sichtteil> nachbarn, List<List<P2>> schnitte, List<Sichtteil>? gruppe, List<Stueck> aus)
    {
        P2 a = s.A.Projektion, b = s.B.Projektion;
        double laenge = Math.Sqrt((b.U - a.U) * (b.U - a.U) + (b.V - a.V) * (b.V - a.V));
        if (laenge < MinRest) return;
        var box = new[] { Math.Min(a.U, b.U), Math.Min(a.V, b.V), Math.Max(a.U, b.U), Math.Max(a.V, b.V) };
        var weg = new List<(double, double)>();

        // a) Schnittflächen
        foreach (var f in schnitte)
            if (Innen(f, a, b, Rand) is { } r) weg.Add(r);

        // c) Nähte und doppelte Kanten in der Gruppe gleich liegender Flächen
        if (gruppe is not null)
        {
            bool aussen = s.Aussen.U != 0 || s.Aussen.V != 0;
            P2 a2 = new(a.U + 2 * Rand * s.Aussen.U, a.V + 2 * Rand * s.Aussen.V), b2 = new(b.U + 2 * Rand * s.Aussen.U, b.V + 2 * Rand * s.Aussen.V);
            foreach (var g in gruppe)
            {
                if (ReferenceEquals(g, t) || !SchnittGeometrie.Ueberlappt(g.Box, box, 3 * Rand)) continue;
                if (aussen && Innen(g.Umriss, a2, b2, 1e-9) is { } naht) weg.Add(naht);                     // Naht
                if (g.Nummer < t.Nummer && Innen(g.Umriss, a, b, Rand) is { } doppelt) weg.Add(doppelt);    // schon gezeichnet
            }
        }

        // b) andere Sichtteile davor
        if (Bedeckt(weg)) return;
        foreach (var o in nachbarn)
        {
            if (gruppe is not null && o.Gruppe == t.Gruppe) continue;   // gleiche Ebene: nur c)
            if (!SchnittGeometrie.Ueberlappt(o.Box, box, 2 * Rand)) continue;
            if (Innen(o.Umriss, a, b, Rand) is not { } innen) continue;
            int vorher = weg.Count;
            if (o is Flaechenteil f) Hinter(s, f, innen.Item1, innen.Item2, weg);
            else Hinter(s, o, innen.Item1, innen.Item2, weg);
            if (weg.Count > vorher && Bedeckt(weg)) return;
        }

        // d) Netze (Modul 2) müssen nicht konvex sein: sie verdecken auch eigene Kanten (exakte Tiefe des Netzes)
        if (t is Netzteil && Innen(t.Umriss, a, b, Rand) is { } selbst)
        {
            Hinter(s, t, selbst.Item1, selbst.Item2, weg);
            if (Bedeckt(weg)) return;
        }

        // sichtbar = [0, 1] ohne die verdeckten Abschnitte
        weg.Sort();
        double von = 0;
        void Dazu(double x0, double x1)
        {
            if ((x1 - x0) * laenge < MinRest) return;
            aus.Add(new(Punkt(a, b, x0), Punkt(a, b, x1), s.Gerade));
        }
        foreach (var (w0, w1) in weg)
        {
            if (w0 > von) Dazu(von, Math.Min(w0, 1));
            von = Math.Max(von, w1);
            if (von >= 1) break;
        }
        if (von < 1) Dazu(von, 1);
    }

    /// <summary>Decken die verdeckten Abschnitte die ganze Strecke [0, 1]?</summary>
    static bool Bedeckt(List<(double, double)> weg)
    {
        if (weg.Count == 0) return false;
        weg.Sort();
        double bis = 0;
        foreach (var (w0, w1) in weg)
        {
            if (w0 > bis + 1e-9) return false;
            bis = Math.Max(bis, w1);
            if (bis >= 1 - 1e-9) return true;
        }
        return false;
    }

    static P2 Punkt(P2 a, P2 b, double t) => t <= 0 ? a : t >= 1 ? b : new(a.U + t * (b.U - a.U), a.V + t * (b.V - a.V));

    /// <summary>Abschnitt [t0, t1] der Strecke a → b, der im konvexen Polygon (Gegenuhrzeigersinn) liegt, jede Seite um
    /// <paramref name="rand"/> nach aussen geschoben (Cyrus–Beck); null, wenn keiner.</summary>
    static (double, double)? Innen(IReadOnlyList<P2> poly, P2 a, P2 b, double rand)
    {
        double t0 = 0, t1 = 1;
        for (int i = 0; i < poly.Count; i++)
        {
            var c1 = poly[i]; var c2 = poly[(i + 1) % poly.Count];
            double eu = c2.U - c1.U, ev = c2.V - c1.V, el = Math.Sqrt(eu * eu + ev * ev);
            if (el < 1e-12) continue;
            // Abstand nach innen (links der Seite): d(t) = da + t · (db − da) ≥ −rand
            double da = (eu * (a.V - c1.V) - ev * (a.U - c1.U)) / el + rand, db = (eu * (b.V - c1.V) - ev * (b.U - c1.U)) / el + rand;
            if (da < 0 && db < 0) return null;
            if (da >= 0 && db >= 0) continue;
            double tx = da / (da - db);
            if (da < 0) t0 = Math.Max(t0, tx); else t1 = Math.Min(t1, tx);
            if (t0 >= t1) return null;
        }
        return (t0, t1);
    }

    /// <summary>Ebene Fläche als Verdecker: verdeckt ist, was hinter ihrer Ebene liegt (w der Kante − Tiefe der Fläche linear in t).</summary>
    static void Hinter(Strecke s, Flaechenteil f, double t0, double t1, List<(double, double)> weg)
    {
        double ga = s.A.W - f.Tiefe(s.A.U, s.A.V) - Gleichauf, gb = s.B.W - f.Tiefe(s.B.U, s.B.V) - Gleichauf;   // > 0: dahinter
        double h0 = 0, h1 = 1;
        if (ga <= 0 && gb <= 0) return;
        if (ga < 0) h0 = ga / (ga - gb);
        else if (gb < 0) h1 = ga / (ga - gb);
        double x0 = Math.Max(t0, h0), x1 = Math.Min(t1, h1);
        if (x1 > x0) weg.Add((x0, x1));
    }

    /// <summary>Gekrümmter Verdecker (Zylinder, Kugel): verdeckt ist, was hinter seiner vorderen Oberfläche liegt.</summary>
    static void Hinter(Strecke s, Sichtteil o, double t0, double t1, List<(double, double)> weg)
    {
        bool Verdeckt(double t)
        {
            var p = s.A + (s.B - s.A) * t;
            double d = o.Tiefe(p.U, p.V);
            return !double.IsNaN(d) && p.W > d + Gleichauf;
        }
        double Grenze(double sichtbar, double verdeckt)
        {
            for (int k = 0; k < Schachtelung; k++)
            {
                double m = (sichtbar + verdeckt) / 2;
                if (Verdeckt(m)) verdeckt = m; else sichtbar = m;
            }
            return verdeckt;
        }
        double vorher = t0;
        bool warVerdeckt = Verdeckt(t0);
        double anfang = t0;
        for (int k = 1; k <= Proben; k++)
        {
            double t = t0 + (t1 - t0) * k / Proben;
            bool jetzt = Verdeckt(t);
            if (jetzt && !warVerdeckt) anfang = Grenze(vorher, t);
            else if (!jetzt && warVerdeckt) weg.Add((anfang, Grenze(t, vorher)));
            warVerdeckt = jetzt;
            vorher = t;
        }
        if (warVerdeckt) weg.Add((anfang, t1));
    }
}
