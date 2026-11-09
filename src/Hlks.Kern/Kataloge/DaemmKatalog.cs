// Dämmregeln für Leitungen (kataloge/daemmung.json; Christian 09.11.2026: «dämmdicken gem. energiegesetzt»): minimale Dämmstärke nach
// MuKEn 2014 Anhang 4 je Rohrnennweite DN, eine Regel je λ-Spalte. Werte und Quelle stehen nur im Katalog.

using System.Text.Json.Serialization;

namespace Hlks.Kern;

public sealed class DaemmStufe
{
    public double DnVon { get; set; }
    public double DnBis { get; set; }
    public double Dicke { get; set; }
}

public sealed class DaemmRegel
{
    public string Name { get; set; } = "";
    public string Spalte { get; set; } = "";
    public List<DaemmStufe> Stufen { get; set; } = [];

    /// <summary>Minimale Dämmstärke in mm für eine Nennweite: Stufe mit DN im Bereich, sonst die nächstgrössere Stufe (ANNAHME im Katalog);
    /// unter der kleinsten Stufe deren Wert; über der grössten Stufe null (die MuKEn nennt keinen Wert).</summary>
    public double? Dicke(double dn)
    {
        if (Stufen.Count == 0 || dn <= 0) return null;
        var geordnet = Stufen.OrderBy(s => s.DnVon).ToList();
        if (dn > geordnet[^1].DnBis) return null;
        return geordnet.FirstOrDefault(s => dn <= s.DnBis)?.Dicke;
    }
}

public sealed class DaemmKatalog
{
    public const string Datei = "daemmung.json";
    public string Beschreibung { get; set; } = "";
    public string Quelle { get; set; } = "";
    public string Gilt { get; set; } = "";
    public string Hinweis { get; set; } = "";
    public List<DaemmRegel> Regeln { get; set; } = [];
    public List<string> Annahmen { get; set; } = [];

    [JsonIgnore] public IReadOnlyList<string> Namen => Regeln.Select(r => r.Name).ToList();

    public DaemmRegel? Regel(string? name) => name is null ? null : Regeln.FirstOrDefault(r => r.Name == name);

    public static DaemmKatalog Laden(string datei) =>
        System.Text.Json.JsonSerializer.Deserialize<DaemmKatalog>(File.ReadAllText(datei), Json.Optionen)
        ?? throw new HlksFehler($"Katalog {Datei} ist leer.", "kataloge/daemmung.json prüfen (Hauptlauf).");
}
