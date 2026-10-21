// Schraffurmuster der Schnittflächen und Füllbereiche – eine Tabelle für Bildschirm, PDF und später DWG.
// Quelle: SIA 400:2000 Ziff. B.8.3.1, Fig. 34 (S. 26); als Kopie übernommen aus KI_Planungsbuero/src/Kern/Bauteile.cs
// (Schraffuren(), 16.10.2026), deckt sich mit der Vorgabe von Christian Steiner (Backstein 45°, Beton Kreuzschraffur,
// Dämmung 90°). Winkel auf dem Blatt (0° waagrecht, Gegenuhrzeigersinn), Abstand in mm auf dem Papier – gilt in allen
// Massstäben. Das Büro schraffiert auch bis 1:100 (SIA erlaubt dort schwarz).

namespace Hlks.Kern;

/// <param name="Art">linien | punkte | voll</param>
/// <param name="Winkel">Linien: Winkel in Grad (zwei Werte = Kreuzschraffur)</param>
/// <param name="AbstandMm">Linien-/Punktabstand auf dem Papier in mm</param>
/// <param name="Annahme">true, wenn das Muster nicht aus der Quelle stammt (Prüfliste Christian)</param>
public sealed record Schraffur(string Name, string Art, double[] Winkel, double AbstandMm, bool Annahme = false);

public static class Schraffuren
{
    public const string Quelle = "SIA 400:2000 Ziff. B.8.3.1, Fig. 34 (S. 26); Muster aus KI-Planungsbüro Bauteile.Schraffuren() vom 16.10.2026";

    static readonly Dictionary<string, Schraffur> Tabelle = new(StringComparer.OrdinalIgnoreCase)
    {
        ["beton"] = new("beton", "linien", [45, 135], 1.2),
        ["backstein"] = new("backstein", "linien", [45], 1.2),            // Quelle: «mauerwerk»
        ["kalksandstein"] = new("kalksandstein", "linien", [135], 1.2),
        ["daemmung"] = new("daemmung", "linien", [90], 1.2),
        ["holz"] = new("holz", "linien", [45], 0.7),
        ["glas"] = new("glas", "linien", [0], 0.8),
        ["putz"] = new("putz", "punkte", [], 0.8),
        ["voll"] = new("voll", "voll", [], 0),
        // Nicht in der Quelle – Annahmen bis zum Entscheid von Christian:
        ["porenbeton"] = new("porenbeton", "linien", [45], 2.4, Annahme: true),   // wie Mauerwerk, doppelter Abstand
        ["erdreich"] = new("erdreich", "linien", [135], 2.4, Annahme: true),
        ["kies"] = new("kies", "punkte", [], 1.6, Annahme: true),
    };

    /// <summary>Muster zum Namen (Material oder Füllbereich-Muster); null bei unbekannt oder «unbestimmt».</summary>
    public static Schraffur? Definition(string? name) => name is not null && Tabelle.TryGetValue(name, out var s) ? s : null;

    public static IReadOnlyCollection<Schraffur> Alle => Tabelle.Values;

    // ------------------------------------------------------------------ Muster folgt der Achse (Nachbearbeitung N8)
    // Befund Christian 21.10.2026: «schraffuren sollten mit wand rotieren». Verhalten wie Revit «an Element ausrichten»: Das Muster
    // liegt nicht achsparallel zum Blatt, sondern gegen die Achse des Bauteils fest. Die Winkel der Tabelle bleiben die Winkel
    // gegen die Achse (Backstein 45° heisst: 45° zur Wandachse); Stil.MusterWinkel dreht sie auf das Blatt.

    // Der Winkel wird auf 0.001° gerundet, damit Wände auf derselben Achse mit Rechenrauschen (IFC, 1e-9°) denselben Winkel und
    // Anker erhalten (Annahme; 0.001° sind auf 10 m Wandlänge 0.17 mm).
    /// <summary>Genauigkeit des Ankers in mm Modell (Annahme).</summary>
    const double AnkerRaster = 0.01;

    /// <summary>Richtung einer Achse A → B in Grad, Gegenuhrzeigersinn, auf [0, 180) gebracht: Schraffurlinien sind ungerichtet,
    /// A → B und B → A geben dasselbe Muster. Gerundet auf 0.001°; 180° ergibt 0°.</summary>
    public static double Achswinkel(double ax, double ay, double bx, double by)
    {
        double w = Math.Round(Math.Atan2(by - ay, bx - ax) * 180 / Math.PI, 3);   // 0.001°
        w %= 180;
        if (w < 0) w += 180;
        return w >= 180 ? 0 : w + 0.0;   // + 0.0 macht aus -0 eine 0
    }

    /// <summary>Muster an die Achse A → B binden: Stil.MusterWinkel = Achswinkel, Stil.MusterUrsprung = Fusspunkt des Lots vom Ursprung
    /// der Ansicht auf die (unendlich verlängerte) Achse. Zwei Bauteile auf derselben Achse erhalten so denselben Winkel und denselben
    /// Anker – das Muster läuft durch. Ohne Muster im Stil oder bei Achse der Länge 0 geschieht nichts.</summary>
    public static void AnAchse(Stil stil, double ax, double ay, double bx, double by)
    {
        if (string.IsNullOrEmpty(stil.Muster) || Definition(stil.Muster) is null) return;
        if (Math.Abs(bx - ax) < 1e-9 && Math.Abs(by - ay) < 1e-9) return;
        double w = Achswinkel(ax, ay, bx, by), r = w * Math.PI / 180;
        double nx = -Math.Sin(r), ny = Math.Cos(r);
        double h = Math.Round((ax * nx + ay * ny) / AnkerRaster) * AnkerRaster;   // Abstand der Achse vom Ursprung, mit Vorzeichen
        stil.MusterWinkel = w;
        stil.MusterUrsprung = [Math.Round(h * nx + 0.0, 2), Math.Round(h * ny + 0.0, 2)];
    }
}
