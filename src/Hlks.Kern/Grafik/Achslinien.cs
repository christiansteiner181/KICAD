// Achslinien runder Bauteile (Christian 05.11.2026: «für die Familiendarstellung: alle Runden Bauteile brauchen eine Achslinie», dann «überall»):
// jedes Körperteil mit Achse (Zylinder, Kegel, Rotation, runder Sweep bzw. Übergang, Bogen; Kugel nur Mittelpunkt – Koerperteil.Achse) bekommt eine
// dünne Strichpunktlinie über den Körper hinaus; zeigt die Achse zum Betrachter (Kreis in der Ansicht) bzw. bei der Kugel ein Achsenkreuz.
// Gilt in der Familiendarstellung (FamilienAnsicht), im Grundriss (FamilienGrafik) sowie in Schnitt und Ansicht (Schnittgrafik).
// ANNAHME (Gestaltung): Überstand r, mindestens 0.3 r, höchstens 3 mm Papier.

namespace Hlks.Kern;

public static class Achslinien
{
    public const string Ebene = "achse";

    /// <summary>Linien für die Achsen der Teile; nach2d bildet einen 3D-Punkt (Koordinaten der Teile) auf die Zeichenebene ab.</summary>
    public static List<Primitiv> Zeichnen(IEnumerable<Koerperteil> teile, Func<double[], double[]> nach2d, double massstab, string? farbe = null)
    {
        var aus = new List<Primitiv>();
        var stil = new Stil { Stift = Stifte.Duenn, Linie = "strichpunkt", Farbe = farbe };
        static double Ab(double[] p, double[] q) => Math.Sqrt((p[0] - q[0]) * (p[0] - q[0]) + (p[1] - q[1]) * (p[1] - q[1]));
        static double[] Weiter(double[] von, double[] bis, double um)
        {
            double l = Ab(von, bis);
            return l < 1e-9 ? bis : [bis[0] + (bis[0] - von[0]) / l * um, bis[1] + (bis[1] - von[1]) / l * um];
        }
        foreach (var t in teile)
        {
            if (t.Achse is not { Count: > 0 } achse || t.AchseRadius <= 0) continue;
            double r = t.AchseRadius, ue = Math.Max(0.3 * r, Math.Min(3 * massstab, r));
            var pts = achse.Select(nach2d).ToList();
            double laenge = 0;
            for (int i = 1; i < pts.Count; i++) laenge += Ab(pts[i - 1], pts[i]);
            if (pts.Count >= 2 && laenge > 0.2 * r)
            {
                var linie = pts.Where((q, i) => i == 0 || Ab(q, pts[i - 1]) > 1e-6).ToList();
                if (linie.Count < 2) continue;
                linie[0] = Weiter(linie[1], linie[0], ue);
                linie[^1] = Weiter(linie[^2], linie[^1], ue);
                aus.Add(new Primitiv { Art = "polylinie", Punkte = linie, Stil = stil });
            }
            else
            {
                double[] m = [pts.Average(q => q[0]), pts.Average(q => q[1])];
                double h = r + ue;
                aus.Add(new Primitiv { Art = "linie", Punkte = [[m[0] - h, m[1]], [m[0] + h, m[1]]], Stil = stil });
                aus.Add(new Primitiv { Art = "linie", Punkte = [[m[0], m[1] - h], [m[0], m[1] + h]], Stil = stil });
            }
        }
        return aus;
    }
}
