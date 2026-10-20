// Bürokatalog kataloge/buero.json: Absender (Standorte) der Planköpfe. Die Werte stehen mit Quelle in der Datei; sie sind aus dem Textauszug
// einer echten H+K-Plan-PDF (Bernstrasse, Revisionspläne 21.01.2026) gelesen, nicht erfunden. Gelesen wird je Katalogordner einmal
// (neu, wenn sich die Datei ändert).

using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <summary>Ein Standort des Büros im Plankopf (Firma mit Ort, Strasse, PLZ Ort, Telefon, Mail, Web).</summary>
public sealed record Standort(string Schluessel, string Firma, string Strasse, string PlzOrt, string Telefon, string? Mail, string? Web, string? Quelle);

public static class Buero
{
    static readonly ConcurrentDictionary<string, (long Zeit, IReadOnlyList<Standort> Liste)> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Standorte aus buero.json › standorte in der Reihenfolge der Datei (der erste ist der Hauptsitz). Fehlt die Datei oder der
    /// Eintrag, ist die Liste leer – der Plankopf zeigt dann keinen Absender (nichts wird geraten).</summary>
    public static IReadOnlyList<Standort> Standorte(string katalogOrdner)
    {
        var datei = Path.Combine(katalogOrdner, "buero.json");
        try
        {
            if (!File.Exists(datei)) return [];
            var zeit = File.GetLastWriteTimeUtc(datei).Ticks;
            if (Cache.TryGetValue(datei, out var c) && c.Zeit == zeit) return c.Liste;
            var liste = new List<Standort>();
            if (JsonNode.Parse(File.ReadAllText(datei, Encoding.UTF8))?["standorte"] is JsonArray a)
                foreach (var n in a.OfType<JsonObject>())
                {
                    string? T(string k) => n[k] is JsonValue v && v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s) ? s.Trim() : null;
                    if (T("firma") is not { } firma) continue;
                    liste.Add(new Standort(T("schluessel") ?? firma, firma, T("strasse") ?? "", T("plzOrt") ?? "", T("telefon") ?? "", T("mail"), T("web"), T("quelle")));
                }
            Cache[datei] = (zeit, liste);
            return liste;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return []; }
    }
}
