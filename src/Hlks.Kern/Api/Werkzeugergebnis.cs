// Nachbearbeitung N5 (20.10.2026): EINE Regel, wann das Ergebnis eines Kern-Werkzeugs eine Ablehnung meldet. Benutzt vom MCP-Server (setzt
// isError, damit Claude Code den Aufruf als Fehler bekommt), von der KI-Palette und dem Auftragsprotokoll (zeigen «abgelehnt») und von der
// Kommandozeile (Exit-Code 1). Vorher hatte jeder Leser seine eigene Regel; unterlagen_lesen meldete Fehler nur je Unterlage und galt als «ok».
//
// Ablehnung = das Ergebnis ist ein JSON-Objekt mit «fehler» als nicht leerem Text bzw. nicht leerer Liste, oder mit «angenommen»: false. So
// antworten die Werkzeuge, wenn der Kern einen HlksFehler wirft (Hülle Werkzeuge.Lauf: {fehler, vorschlag}), wenn ein Befehl abgelehnt wird
// (BefehlsErgebnis angenommen false) und wenn unterlagen_lesen mindestens eine Unterlage nicht liest (dann steht «fehler» zusätzlich oben).
// Nicht: eine leere Liste (synchronisieren meldet «fehler»: [] bei Erfolg – die frühere Regel der Palette zeigte das als abgelehnt), eine Zahl
// und verschachtelte Felder (projekt_ueberblick.befunde.fehler ist eine Anzahl). Die Kopfzeile B15 vor inhaltstragenden Ergebnissen wird übergangen.

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class Werkzeugergebnis
{
    /// <summary>Meldet der Text eines Werkzeugergebnisses eine Ablehnung (JSON-Objekt mit «fehler» als Text bzw. nicht leere Liste, oder
    /// angenommen = false)? Textansichten (kein JSON) melden nie eine Ablehnung.</summary>
    public static bool MeldetFehler(string? text)
    {
        var t = KiInhalt.OhneKopf(text).Trim();
        if (t.Length == 0 || t[0] != '{') return false;
        try
        {
            if (JsonNode.Parse(t) is not JsonObject o) return false;
            if (o["angenommen"] is JsonValue a && a.TryGetValue<bool>(out var angenommen) && !angenommen) return true;
            return o["fehler"] switch
            {
                JsonValue v => v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s),
                JsonArray l => l.Count > 0,
                _ => false,
            };
        }
        catch (JsonException) { return false; }
    }
}
