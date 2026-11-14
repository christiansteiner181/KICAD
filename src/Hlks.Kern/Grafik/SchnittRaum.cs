// Schnitt und Ansicht (Modul 1a, C1) – Lage der Schnittebene und Koordinaten der Ansicht.
//   u = waagrechter Abstand von links nach rechts, wie der Betrachter sieht, v = z (Höhe über ±0.00), w = Abstand vor
//   der Schnittebene in Blickrichtung. Alles in Modell-mm.
// Die Schnittebene steht senkrecht durch die Linie (entwurf/modul-1a.md «Schnitt und Ansicht»). Blick «links» = Normale
// links von A → B (wie das Schnittsymbol in BeschriftungSymbole): u läuft ab A; Blick «rechts»: u läuft ab B Richtung A,
// sonst wäre das Bild seitenverkehrt (Befund C1, korrigiert 17.10.2026); fehlt blick: links.
// Sichtbar ist 0 ≤ w ≤ Tiefe; hinter der Schnittebene (w < 0) liegt nichts Sichtbares. Fehlt `tiefe`, reicht die Sicht
// bis ans Ende des Modells.
// Abgesetzter Schnitt (Gesamtprüfung N31, Christian 13.11.2026: «Echter abgesetzter Schnitt», wie Revit): Linienzug mit mehr als zwei Punkten. Richtung
// und u kommen vom ersten Stück; Stücke parallel dazu (höchstens 1° Abweichung) sind Schnittabschnitte, alle anderen Versätze. Jeder Abschnitt schneidet
// in seiner eigenen Ebene (Lage quer zur Linie = sein Versatz) und gilt für seinen u-Bereich; die Bereiche schliessen lückenlos aneinander (Grenze in der
// Mitte zwischen zwei Abschnitten), der erste und der letzte reichen nach aussen unbegrenzt. Die Sichttiefe gilt je Abschnitt ab seiner Ebene.
// ANNAHMEN: `tiefe` ≤ 0 gilt wie «fehlt»; die Ebene ist entlang u unbegrenzt (Zuschnitt über `ausschnitt` beim Planblatt); an der Grenze zweier
// Abschnitte wird scharf getrennt (Schnittflächen enden dort mit ihrer Kontur, Versatzlinie wird nicht eigens gezeichnet).

namespace Hlks.Kern;

/// <summary>Punkt der Ansicht (u, v) in Modell-mm.</summary>
internal readonly record struct P2(double U, double V);

/// <summary>Punkt im Raum der Ansicht: u entlang der Linie, v = z, w = Tiefe in Blickrichtung (Modell-mm).</summary>
internal readonly record struct P3(double U, double V, double W)
{
    public static P3 operator +(P3 a, P3 b) => new(a.U + b.U, a.V + b.V, a.W + b.W);
    public static P3 operator -(P3 a, P3 b) => new(a.U - b.U, a.V - b.V, a.W - b.W);
    public static P3 operator *(P3 a, double s) => new(a.U * s, a.V * s, a.W * s);
    public double Mal(P3 b) => U * b.U + V * b.V + W * b.W;
    public P3 Kreuz(P3 b) => new(V * b.W - W * b.V, W * b.U - U * b.W, U * b.V - V * b.U);
    public double Laenge => Math.Sqrt(Mal(this));
    public P2 Projektion => new(U, V);
}

internal sealed class Schnittraum
{
    /// <summary>Anfang A der Schnittlinie (xy).</summary>
    public required double Ax { get; init; }
    public required double Ay { get; init; }
    /// <summary>Einheitsvektor A → B (xy).</summary>
    public required double Dx { get; init; }
    public required double Dy { get; init; }
    /// <summary>Blickrichtung (waagrechter Einheitsvektor, xy).</summary>
    public required double Nx { get; init; }
    public required double Ny { get; init; }
    /// <summary>Länge der Schnittlinie A → B.</summary>
    public required double Laenge { get; init; }
    /// <summary>Sichttiefe ab der Schnittebene; ohne Angabe unendlich.</summary>
    public required double Tiefe { get; init; }

    public bool Begrenzt => !double.IsPositiveInfinity(Tiefe);

    public static Schnittraum Aus(Element ansicht)
    {
        var pts = ansicht.Geometrie?.Punkte ?? [];
        if (pts.Count < 2 || pts[0].Length < 2 || pts[^1].Length < 2)
            throw new HlksFehler($"{ansicht.Kennzeichen} hat keine Schnittlinie.", "Geometrie { art: linie, punkte: [[x1,y1],[x2,y2]] } setzen.");
        bool rechts = string.Equals(ansicht.Text("blick"), "rechts", StringComparison.OrdinalIgnoreCase);
        var zug = rechts ? Enumerable.Reverse(pts).ToList() : pts.ToList();   // Blick nach rechts = Blick nach links auf der Gegenrichtung (nicht gespiegelt)
        var a = zug[0]; var b = zug[1];   // Richtung aus dem ersten Stück (abgesetzter Schnitt, N31)
        double dx = b[0] - a[0], dy = b[1] - a[1], l = Math.Sqrt(dx * dx + dy * dy);
        if (l < 1e-6)
            throw new HlksFehler($"Die Schnittlinie von {ansicht.Kennzeichen} hat die Länge 0.",
                "Zwei verschiedene Punkte setzen: geometrie { art: linie, punkte: [[x1,y1],[x2,y2]] }.");
        dx /= l; dy /= l;
        return new Schnittraum
        {
            Ax = a[0], Ay = a[1], Dx = dx, Dy = dy, Laenge = l,
            Nx = -dy, Ny = dx,   // Blick nach links der (ggf. umgedrehten) Linie
            Tiefe = ansicht.Zahl("tiefe") is double t && t > 0 ? t : double.PositiveInfinity,
        };
    }

    /// <summary>Schnittabschnitte (N31): je Abschnitt ein Schnittraum (gleiche Richtung und u, Ebene um den Versatz quer verschoben) und sein u-Bereich.
    /// Gerade Schnittlinie: ein Abschnitt, u unbegrenzt.</summary>
    public static List<(Schnittraum Raum, double U0, double U1)> Abschnitte(Element ansicht)
    {
        var basis = Aus(ansicht);
        var pts = ansicht.Geometrie!.Punkte;
        if (pts.Count <= 2) return [(basis, double.NegativeInfinity, double.PositiveInfinity)];
        bool rechts = string.Equals(ansicht.Text("blick"), "rechts", StringComparison.OrdinalIgnoreCase);
        var zug = rechts ? Enumerable.Reverse(pts).ToList() : pts.ToList();
        var stuecke = new List<(double U0, double U1, double Versatz)>();
        for (int i = 0; i + 1 < zug.Count; i++)
        {
            double ex = zug[i + 1][0] - zug[i][0], ey = zug[i + 1][1] - zug[i][1], l = Math.Sqrt(ex * ex + ey * ey);
            if (l < 1e-6) continue;
            if (Math.Abs((ex * basis.Dy - ey * basis.Dx) / l) > Math.Sin(Math.PI / 180)) continue;   // Versatz
            double u0 = basis.U(zug[i][0], zug[i][1]), u1 = basis.U(zug[i + 1][0], zug[i + 1][1]);
            stuecke.Add((Math.Min(u0, u1), Math.Max(u0, u1), basis.W(zug[i][0], zug[i][1])));
        }
        stuecke = stuecke.OrderBy(s => s.U0).ToList();
        for (int i = 1; i < stuecke.Count; i++)
            if (stuecke[i].U0 < stuecke[i - 1].U1 - 1e-6)
                throw new HlksFehler($"Die abgesetzte Schnittlinie von {ansicht.Kennzeichen} überlappt sich (Abschnitte {i} und {i + 1} decken denselben Bereich).",
                    "Die Schnittabschnitte müssen in Richtung des ersten Stücks hintereinander liegen; die Versätze quer dazu zeichnen.");
        if (stuecke.Count <= 1) return [(basis, double.NegativeInfinity, double.PositiveInfinity)];
        var aus = new List<(Schnittraum, double, double)>();
        for (int i = 0; i < stuecke.Count; i++)
        {
            double von = i == 0 ? double.NegativeInfinity : (stuecke[i - 1].U1 + stuecke[i].U0) / 2;
            double bis = i == stuecke.Count - 1 ? double.PositiveInfinity : (stuecke[i].U1 + stuecke[i + 1].U0) / 2;
            double o = stuecke[i].Versatz;
            aus.Add((new Schnittraum { Ax = basis.Ax + basis.Nx * o, Ay = basis.Ay + basis.Ny * o, Dx = basis.Dx, Dy = basis.Dy, Nx = basis.Nx, Ny = basis.Ny, Laenge = basis.Laenge, Tiefe = basis.Tiefe }, von, bis));
        }
        return aus;
    }

    public double U(double x, double y) => (x - Ax) * Dx + (y - Ay) * Dy;
    public double W(double x, double y) => (x - Ax) * Nx + (y - Ay) * Ny;
    public P3 Punkt(double[] p) => new(U(p[0], p[1]), p.Length > 2 ? p[2] : 0, W(p[0], p[1]));
}

/// <summary>Zuschnitt der Grafik eines Schnittabschnitts auf seinen u-Bereich (abgesetzter Schnitt, N31).</summary>
internal static class SchnittAbschnitt
{
    const double Gross = 1e9;

    public static List<Grafikteil> Zuschneiden(IEnumerable<Grafikteil> teile, double u0, double u1)
    {
        double a = double.IsNegativeInfinity(u0) ? -Gross : u0, b = double.IsPositiveInfinity(u1) ? Gross : u1;
        bool Drin(double u) => u >= a - 1e-9 && u < b;
        var aus = new List<Grafikteil>();
        foreach (var t in teile)
        {
            var neu = new List<Primitiv>();
            foreach (var pr in t.Primitive)
            {
                switch (pr.Art)
                {
                    case "linie" or "polylinie" when pr.Punkte.Count >= 2:
                        foreach (var stueck in Linie(pr.Punkte, a, b))
                            neu.Add(Kopie(pr, stueck, stueck.Count == 2 ? "linie" : "polylinie"));
                        break;
                    case "polygon" when pr.Punkte.Count >= 3:
                    {
                        var pfade = new Clipper2Lib.PathsD { Pfad(pr.Punkte) };
                        foreach (var l in pr.Loecher ?? []) pfade.Add(Pfad(l));
                        var z = Clipper2Lib.Clipper.RectClip(new Clipper2Lib.RectD(a, -Gross, b, Gross), pfade, Polygone.Praezision);
                        foreach (var fl in Polygone.Flaechen(z))
                        {
                            var k = Kopie(pr, fl.Aussen, "polygon");
                            k.Loecher = fl.Loecher.Count > 0 ? fl.Loecher : null;
                            neu.Add(k);
                        }
                        break;
                    }
                    default:
                        if (pr.Punkte.Count == 0 || Drin(pr.Punkte[0][0])) neu.Add(pr);
                        break;
                }
            }
            if (neu.Count == 0) continue;
            aus.Add(new Grafikteil
            {
                Id = t.Id, Ebene = t.Ebene, Teil = t.Teil, Primitive = neu,
                Fangpunkte = t.Fangpunkte?.Where(p => Drin(p[0])).ToList(),
                Fangkanten = t.Fangkanten?.SelectMany(k => Linie([k[0], k[1]], a, b)).Where(s => s.Count == 2).Select(s => s.ToArray()).ToList(),
            });
        }
        return aus;
    }

    static Clipper2Lib.PathD Pfad(List<double[]> pts) { var p = new Clipper2Lib.PathD(); foreach (var q in pts) p.Add(new Clipper2Lib.PointD(q[0], q[1])); return p; }

    static Primitiv Kopie(Primitiv pr, List<double[]> punkte, string art) => new()
    {
        Art = art, Punkte = punkte, Radius = pr.Radius, Von = pr.Von, Bis = pr.Bis, Text = pr.Text, Groesse = pr.Groesse, Ausrichtung = pr.Ausrichtung,
        Winkel = pr.Winkel, Fett = pr.Fett, Stil = pr.Stil, Mitdrehen = pr.Mitdrehen, Anker = pr.Anker,
    };

    /// <summary>Linienzug auf den Streifen a ≤ u ≤ b schneiden; liefert die Stücke im Streifen.</summary>
    static IEnumerable<List<double[]>> Linie(List<double[]> pts, double a, double b)
    {
        var akt = new List<double[]>();
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            var p = pts[i]; var q = pts[i + 1];
            double t0 = 0, t1 = 1, du = q[0] - p[0];
            if (Math.Abs(du) < 1e-12) { if (p[0] < a || p[0] > b) t1 = -1; }
            else
            {
                double ta = (a - p[0]) / du, tb = (b - p[0]) / du;
                t0 = Math.Max(0, Math.Min(ta, tb)); t1 = Math.Min(1, Math.Max(ta, tb));
            }
            if (t1 < t0 + 1e-12) { if (akt.Count >= 2) yield return akt; akt = []; continue; }
            double[] P(double t) => [Math.Round(p[0] + t * du, Polygone.Praezision), Math.Round(p[1] + t * (q[1] - p[1]), Polygone.Praezision)];
            var s = P(t0); var e = P(t1);
            if (akt.Count == 0 || t0 > 1e-12) { if (akt.Count >= 2) yield return akt; akt = [s]; }
            akt.Add(e);
            if (t1 < 1 - 1e-12) { yield return akt; akt = []; }
        }
        if (akt.Count >= 2) yield return akt;
    }
}
