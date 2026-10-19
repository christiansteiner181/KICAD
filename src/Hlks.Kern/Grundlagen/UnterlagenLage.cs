// Lage einer Unterlage im Modell (Modul 1b, B3): Ähnlichkeitstransformation von Unterlagen-Koordinaten in Modell-mm.
//   Modell = Drehung(faktor · u) + [dx, dy]
// DWG/DXF: u = Zeichnungskoordinaten (Zeichnungseinheiten). PDF/Bild: u = Pixel (x nach rechts, y nach unten); y wird
// zuerst gespiegelt, damit das Bild im Modell aufrecht steht – die Pixelspalte y zählt also als −y.
// Die Eigenschaft «transformation» der Unterlage ist { faktor, drehung (Grad, Gegenuhrzeigersinn), dx, dy }; dazu legt
// der Kern Erwartungswerte ab (erwartung, grundlage, dpi), mit denen das Kalibrieren seine Plausibilität prüft.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <summary>Ähnlichkeitstransformation (Massstab, Drehung, Verschiebung), ohne Spiegelung.</summary>
public readonly record struct UnterlagenLage(double Faktor, double Drehung, double Dx, double Dy)
{
    public static readonly UnterlagenLage Identitaet = new(1, 0, 0, 0);

    /// <summary>Unterlagen-Koordinate (u, v) → Modell-mm. Bei PDF/Bild ist v bereits gespiegelt (−Pixelzeile).</summary>
    public (double X, double Y) Auf(double u, double v)
    {
        double w = Drehung * Math.PI / 180, c = Faktor * Math.Cos(w), s = Faktor * Math.Sin(w);
        return (c * u - s * v + Dx, s * u + c * v + Dy);
    }

    /// <summary>Modell-mm → Unterlagen-Koordinate (Umkehrung von <see cref="Auf"/>).</summary>
    public (double U, double V) Zurueck(double x, double y)
    {
        double w = Drehung * Math.PI / 180, c = Math.Cos(w) / Faktor, s = Math.Sin(w) / Faktor;
        double px = x - Dx, py = y - Dy;
        return (c * px + s * py, -s * px + c * py);
    }

    /// <summary>Pixel (x, Zeile von oben) einer Rasterunterlage → Modell-mm.</summary>
    public (double X, double Y) AufPixel(double px, double zeile) => Auf(px, -zeile);

    /// <summary>Ist die Unterlage ein Raster (PDF/Bild, y nach unten)?</summary>
    public static bool IstRaster(string? art) => art is "pdf" or "bild";

    /// <summary>Lage der Unterlage; ohne Eigenschaft die Identität.</summary>
    public static UnterlagenLage Lesen(Element unterlage)
    {
        if (unterlage.Eigenschaft("transformation")?.Inhalt is not JsonObject o) return Identitaet;
        return new UnterlagenLage(Json.Zahl(o["faktor"]) ?? 1, Json.Zahl(o["drehung"]) ?? 0, Json.Zahl(o["dx"]) ?? 0, Json.Zahl(o["dy"]) ?? 0);
    }

    /// <summary>JSON für die Eigenschaft «transformation»; weitere Schlüssel (erwartung, grundlage, dpi) aus <paramref name="bisher"/> bleiben.</summary>
    public JsonObject Zu(JsonObject? bisher = null)
    {
        var o = bisher?.DeepClone() as JsonObject ?? new JsonObject();
        o["faktor"] = Math.Round(Faktor, 9);
        o["drehung"] = Math.Round(Drehung, 6);
        o["dx"] = Math.Round(Dx, 3);
        o["dy"] = Math.Round(Dy, 3);
        return o;
    }

    /// <summary>Drehung in (−180, 180].</summary>
    public static double Normieren(double grad)
    {
        grad %= 360;
        if (grad > 180) grad -= 360;
        if (grad <= -180) grad += 360;
        return grad;
    }

    /// <summary>Ähnlichkeitstransformation, die u1 → n1 und u2 → n2 abbildet (Faktor, Drehung, Verschiebung).</summary>
    public static UnterlagenLage ZweiPunkte((double U, double V) u1, (double U, double V) u2, (double X, double Y) n1, (double X, double Y) n2)
    {
        double au = u2.U - u1.U, av = u2.V - u1.V, bx = n2.X - n1.X, by = n2.Y - n1.Y;
        double la = Math.Sqrt(au * au + av * av), lb = Math.Sqrt(bx * bx + by * by);
        if (la < 1e-12) throw new HlksFehler("Die beiden Punkte in «von» sind gleich.", "Zwei verschiedene Punkte der Unterlage angeben.");
        if (lb < 1e-9) throw new HlksFehler("Die beiden Punkte in «nach» sind gleich.", "Zwei verschiedene Zielpunkte im Modell (mm) angeben.");
        double grad = Normieren((Math.Atan2(by, bx) - Math.Atan2(av, au)) * 180 / Math.PI);
        var ohneVersatz = new UnterlagenLage(lb / la, grad, 0, 0);
        var (px, py) = ohneVersatz.Auf(u1.U, u1.V);
        return ohneVersatz with { Dx = n1.X - px, Dy = n1.Y - py };
    }
}
