// Quest App V1g (Brillentest, Christian 24.10.2026: «durch oben unten des roten punkte soll das modell auch rotieren.» – auf Rückfrage «Kippen»). Rein, ohne StereoKit.
// Am Punkt «Drehen und Vergrössern» kippt eine Auf/Ab-Bewegung der Hand das Modell um eine WAAGRECHTE ACHSE DURCH DAS ZENTRUM (Mitte des sichtbaren Rechtecks auf der
// Unterkante), quer zur Blickrichtung des Betrachters beim Greifen (Regel in ZweiPunkte). Die Kippung liegt im Raum-lokalen System des Tisches (XrSitzungLogik), nach der
// Drehung um die Hochachse: dreht man das gekippte Modell, bleibt die Neigung zum Betrachter. Damit das Modell nie in den (echten bzw. angenommenen) Tisch taucht, hebt es
// sich so weit an, dass die tiefste Ecke seiner Grundfläche auf der Höhe der Unterkante ohne Kippung bleibt (Anheben). Grenze und Totzone: ZweiPunkteEinstellungen.
//   Raum-lokal ohne Kippung q (SitzungRaum.ModellZuRaum ohne Kippung) → mit Kippung: q' = Drehen(q − P, Achse, Winkel) + P + (0, Anheben, 0); P = Zentrum ohne Kippung.
// Drehsinn: positiver Winkel um die Achse nach der Rechte-Hand-Regel; die Achse zeigt beim Greifen nach links vom Betrachter, so hebt ein positiver Winkel die Seite zum
// Betrachter (die Hand geht hoch, der vordere Rand mit dem roten Punkt geht mit). Alle Zahlen sind Annahmen V1g.

using System.Numerics;

namespace Hlks.Xr.Sitzung;

/// <summary>Kippung des Modells: Winkel (rad), waagrechte Achse (Einheitsvektor in x, z, Raum-lokal), Drehpunkt (Szene, x und z; auf der Unterkante des Bezugs), Anheben (m).</summary>
public sealed record SitzungKippung(double Winkel, Xz Achse, Xz Punkt, double Anheben = 0);

public static class Kippen
{
    /// <summary>Vektor v um die waagrechte Achse k (Einheit, y = 0) um den Winkel w drehen (Rechte-Hand-Regel, Rodrigues).</summary>
    public static Vec3 Drehen(Vec3 v, Xz k, double w)
    {
        var a = new Vec3(k.X, 0, k.Z);
        double c = Math.Cos(w), s = Math.Sin(w);
        return v * c + a.Kreuz(v) * s + a * (a.Skalar(v) * (1 - c));
    }

    /// <summary>Der Drehpunkt im Raum-lokalen System (ohne Kippung) für diese Lage und diesen Bezug.</summary>
    public static Vec3 Drehpunkt(SitzungKippung k, SitzungLage lage, SitzungBezug bezug) =>
        SitzungRaum.ModellZuRaumOhneKippung(new Vec3(k.Punkt.X, bezug.Unten, k.Punkt.Z), lage, bezug);

    /// <summary>Ein Punkt ohne Kippung (Raum-lokal) → mit Kippung (siehe Dateikopf); ohne Kippung unverändert.</summary>
    public static Vec3 Anwenden(Vec3 q, SitzungLage lage, SitzungBezug bezug)
    {
        if (lage.Kippung is not { } k) return q;
        var p = Drehpunkt(k, lage, bezug);
        return Drehen(q - p, k.Achse, k.Winkel) + p + new Vec3(0, k.Anheben, 0);
    }

    /// <summary>Matrix Raum-lokal ohne Kippung → mit Kippung (Zeilenvektoren wie System.Numerics); Identität ohne Kippung.</summary>
    public static Matrix4x4 Matrix(SitzungLage lage, SitzungBezug bezug)
    {
        if (lage.Kippung is not { } k) return Matrix4x4.Identity;
        var p = Drehpunkt(k, lage, bezug);
        double c = Math.Cos(k.Winkel), s = Math.Sin(k.Winkel), x = k.Achse.X, z = k.Achse.Z;
        // Rodrigues mit ky = 0, als Spaltenmatrix R; für Zeilenvektoren die Transponierte
        double r11 = c + (1 - c) * x * x, r12 = -s * z, r13 = (1 - c) * x * z;
        double r21 = s * z, r22 = c, r23 = -s * x;
        double r31 = (1 - c) * x * z, r32 = s * x, r33 = c + (1 - c) * z * z;
        var dreh = new Matrix4x4((float)r11, (float)r21, (float)r31, 0, (float)r12, (float)r22, (float)r32, 0, (float)r13, (float)r23, (float)r33, 0, 0, 0, 0, 1);
        return Matrix4x4.CreateTranslation((float)-p.X, (float)-p.Y, (float)-p.Z) * dreh
            * Matrix4x4.CreateTranslation((float)p.X, (float)(p.Y + k.Anheben), (float)p.Z);
    }

    /// <summary>Die vier Ecken der Grundfläche (Szene, auf der Unterkante): das sichtbare Rechteck, sonst die Box des Bezugs.</summary>
    public static IReadOnlyList<Vec3> Grundflaeche(SitzungBezug bezug, SichtbarRahmen? sichtbar)
    {
        var r = sichtbar is { Gueltig: true } s ? s
            : new SichtbarRahmen(1, 0, bezug.Anker.X - bezug.Groesse.B / 2, bezug.Anker.X + bezug.Groesse.B / 2, bezug.Anker.Z - bezug.Groesse.T / 2, bezug.Anker.Z + bezug.Groesse.T / 2);
        return [.. r.Ecken().Select(e => new Vec3(e.X, bezug.Unten, e.Z))];
    }

    /// <summary>
    /// So weit (m) muss sich das gekippte Modell heben, damit die tiefste Ecke der Grundfläche nicht unter der Unterkante ohne Kippung liegt (0, wenn sie nicht sinkt).
    /// Das Anheben der Kippung in lage zählt dabei nicht.
    /// </summary>
    public static double Anheben(SitzungLage lage, SitzungBezug bezug, SichtbarRahmen? sichtbar)
    {
        if (lage.Kippung is not { } k) return 0;
        var ohne = lage with { Kippung = k with { Anheben = 0 } };
        var unten = XrSitzungLogik.TischHoehe + lage.Hoehe;
        var tiefst = Grundflaeche(bezug, sichtbar).Min(e => Anwenden(SitzungRaum.ModellZuRaumOhneKippung(e, lage, bezug), ohne, bezug).Y);
        return Math.Max(0, unten - tiefst);
    }
}
