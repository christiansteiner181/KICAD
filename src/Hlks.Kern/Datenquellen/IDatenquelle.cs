// Datenquellen für Mess- und Zählerwerte (GA-Anbindung). Die Werte bleiben auf der Anlage und werden bei jedem
// Zugriff abgerufen; das Modell speichert nur die Adresse am Datenpunkt. Bis zur echten Anbindung (Modul 9)
// liefert der Simulator nachvollziehbare Testwerte.

namespace Hlks.Kern;

/// <param name="Qualitaet">gut | ersatz | gestoert</param>
public sealed record Messwert(DateTimeOffset Zeit, double Wert, string Qualitaet = "gut");

public interface IDatenquelle
{
    string Name { get; }
    /// <summary>Zeitreihe eines Datenpunkts zwischen von und bis im Raster aufloesung (verdichtet: Mittelwert,
    /// bei Zählern der Stand am Rasterende). Nur lesend.</summary>
    IReadOnlyList<Messwert> Zeitreihe(Element datenpunkt, DateTimeOffset von, DateTimeOffset bis, TimeSpan aufloesung);
}
