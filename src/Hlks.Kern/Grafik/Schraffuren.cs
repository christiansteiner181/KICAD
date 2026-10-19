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
}
