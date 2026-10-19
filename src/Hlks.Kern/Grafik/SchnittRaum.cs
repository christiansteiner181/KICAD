// Schnitt und Ansicht (Modul 1a, C1) – Lage der Schnittebene und Koordinaten der Ansicht.
//   u = waagrechter Abstand von links nach rechts, wie der Betrachter sieht, v = z (Höhe über ±0.00), w = Abstand vor
//   der Schnittebene in Blickrichtung. Alles in Modell-mm.
// Die Schnittebene steht senkrecht durch die Linie (entwurf/modul-1a.md «Schnitt und Ansicht»). Blick «links» = Normale
// links von A → B (wie das Schnittsymbol in BeschriftungSymbole): u läuft ab A; Blick «rechts»: u läuft ab B Richtung A,
// sonst wäre das Bild seitenverkehrt (Befund C1, korrigiert 17.10.2026); fehlt blick: links.
// Sichtbar ist 0 ≤ w ≤ Tiefe; hinter der Schnittebene (w < 0) liegt nichts Sichtbares. Fehlt `tiefe`, reicht die Sicht
// bis ans Ende des Modells.
// ANNAHMEN: Linienzüge mit mehr als zwei Punkten (abgesetzte Schnitte) gelten als gerade Linie vom ersten zum letzten
// Punkt; `tiefe` ≤ 0 gilt wie «fehlt»; die Ebene ist entlang u unbegrenzt (Zuschnitt über `ausschnitt` beim Planblatt).

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
        var a = pts[0]; var b = pts[^1];
        bool rechts = string.Equals(ansicht.Text("blick"), "rechts", StringComparison.OrdinalIgnoreCase);
        if (rechts) (a, b) = (b, a);   // Blick nach rechts = Blick nach links auf der Gegenrichtung B → A (nicht gespiegelt)
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

    public double U(double x, double y) => (x - Ax) * Dx + (y - Ay) * Dy;
    public double W(double x, double y) => (x - Ax) * Nx + (y - Ay) * Ny;
    public P3 Punkt(double[] p) => new(U(p[0], p[1]), p.Length > 2 ? p[2] : 0, W(p[0], p[1]));
}
