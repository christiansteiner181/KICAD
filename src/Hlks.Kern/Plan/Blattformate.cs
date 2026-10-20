// Blattformate der Planblätter und Masse des Plankopfs.
// Blattgrössen: Reihe A nach ISO 216 (A4 210 × 297 mm … A0 841 × 1189 mm), Vorgabe quer.
// Alle übrigen Werte (Rand, Plankopfbreite, Höhe des Titelbereichs) sind Gestaltungswerte und als «Annahme» markiert.

namespace Hlks.Kern;

public static class Blattformate
{
    /// <summary>Format → (Breite, Höhe) im Hochformat in mm, ISO 216.</summary>
    static readonly Dictionary<string, (double B, double H)> Tabelle = new(StringComparer.OrdinalIgnoreCase)
    {
        ["A4"] = (210, 297),
        ["A3"] = (297, 420),
        ["A2"] = (420, 594),
        ["A1"] = (594, 841),
        ["A0"] = (841, 1189),
    };

    public static IReadOnlyCollection<string> Namen => Tabelle.Keys;

    /// <summary>Format, wenn bei planblatt_anlegen keines genannt wird (Annahme).</summary>
    public const string Vorgabe = "A3";

    /// <summary>Weisser Rand rund um das Blatt bis zum Zeichenrahmen in mm (Annahme).</summary>
    public const double Rand = 10;

    /// <summary>Breite und Höhe des Blatts in mm; Vorgabe quer.</summary>
    public static (double Breite, double Hoehe) Groesse(string? format, bool hochformat)
    {
        if (!Tabelle.TryGetValue(format ?? Vorgabe, out var g))
            throw new HlksFehler($"Blattformat «{format}» gibt es nicht.", "Erlaubt: " + string.Join(", ", Tabelle.Keys));
        return hochformat ? (g.B, g.H) : (g.H, g.B);
    }

    /// <summary>Breite des H+K-Plankopfs in mm: Grundriss-Kopf 180 mm (Vorlage), Schema-Kopf 150 mm (schmal); auf A4 verkleinert, damit die
    /// Zeichnung Platz behält (Annahme).</summary>
    public static double KopfBreite(string? format, HkPlankopf.Art art = HkPlankopf.Art.Grundriss)
    {
        bool a4 = string.Equals(format, "A4", StringComparison.OrdinalIgnoreCase);
        return art == HkPlankopf.Art.Schema ? (a4 ? 125 : 150) : (a4 ? 150 : 180);
    }

    /// <summary>Gesamthöhe des Plankopfs in mm: wächst mit der Zahl der Planänderungen (Zeilen) nach oben, mit Titelband +10 mm bei 180 mm Breite.</summary>
    public static double KopfHoehe(string? format, HkPlankopf.Art art = HkPlankopf.Art.Grundriss, int zeilen = 1, bool titelband = false) =>
        HkPlankopf.Hoehe(art, KopfBreite(format, art), zeilen, titelband);
    /// <summary>Unterhalb jedes Ansichtsfensters reservierte Höhe für Titel, grafischen Massstab und Nordpfeil in mm (Annahme).</summary>
    public const double TitelHoehe = 16;
}
