// Bürobibliothek der Materialien (kataloge/materialien.json, Modul 1c). Nur Vorlage: material_uebernehmen kopiert einen
// Eintrag samt Quelle ins Projekt. Jeder Kennwert braucht eine Quelle; ein Eintrag ohne Quelle macht die Bibliothek
// ungültig (Fehler mit Eintrag und Kennwert), statt still einen Wert ohne Beleg zu übernehmen.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public sealed record BibliotheksWert(double Wert, string Quelle);

public sealed class BibliotheksMaterial
{
    public string Name { get; init; } = "";
    public string? Kategorie { get; init; }
    public Dictionary<string, BibliotheksWert> Werte { get; init; } = [];
}

public static class MaterialBibliothek
{
    public const string Datei = "materialien.json";

    /// <summary>Kennwerte eines Materials mit erlaubtem Bereich: Name, Einheit, kleinster Wert, eingeschlossen?, grösster Wert.
    /// Die Bereiche folgen den Katalogbeschreibungen (Anteile 0 … 1, übrige Kennwerte grösser als 0).</summary>
    public static readonly (string Name, double Min, bool MinEin, double? Max)[] Kennwerte =
    [
        ("lambda", 0, false, null), ("rohdichte", 0, false, null), ("waermekapazitaet", 0, false, null),
        ("emission", 0, true, 1), ("absorption", 0, true, 1), ("mu", 0, false, null),
    ];

    public static string Pfad(Katalog kat) => Path.Combine(kat.Ordner, Datei);

    /// <summary>Lädt die Bibliothek. Fehlt die Datei, ist sie leer. Ungültige Einträge: HlksFehler.</summary>
    public static List<BibliotheksMaterial> Laden(Katalog kat)
    {
        var pfad = Pfad(kat);
        var liste = new List<BibliotheksMaterial>();
        if (!File.Exists(pfad)) return liste;
        JsonNode? wurzel;
        try { wurzel = JsonNode.Parse(File.ReadAllText(pfad, System.Text.Encoding.UTF8)); }
        catch (System.Text.Json.JsonException ex) { throw new HlksFehler($"Bürobibliothek {pfad} ist kein gültiges JSON: {ex.Message}", "Datei prüfen (Format steht in der Datei unter «format»)."); }
        if (wurzel?["materialien"] is not JsonArray a) return liste;
        foreach (var n in a)
        {
            if (n is not JsonObject o || Param.Text(o, "name") is not { } name || string.IsNullOrWhiteSpace(name))
                throw new HlksFehler("Bürobibliothek: ein Eintrag hat keinen Namen.", "Jeder Eintrag braucht «name» (Format steht in der Datei unter «format»).");
            var m = new BibliotheksMaterial { Name = name.Trim(), Kategorie = Param.Text(o, "kategorie") };
            if (o["werte"] is JsonObject werte)
                foreach (var (key, roh) in werte)
                {
                    var feld = Kennwerte.FirstOrDefault(x => x.Name.Equals(key, StringComparison.OrdinalIgnoreCase));
                    if (feld.Name is null)
                        throw new HlksFehler($"Bürobibliothek, «{name}»: Kennwert «{key}» gibt es nicht.", "Erlaubt: " + string.Join(", ", Kennwerte.Select(x => x.Name)));
                    var (wert, quelle) = HuelleHilfe.WertMitQuelle(roh, $"{name}.{feld.Name}");
                    HuelleHilfe.Pruefen($"{name}.{feld.Name}", wert, feld.Min, feld.MinEin, feld.Max, "");
                    m.Werte[feld.Name] = new BibliotheksWert(wert, quelle);
                }
            liste.Add(m);
        }
        return liste;
    }
}
