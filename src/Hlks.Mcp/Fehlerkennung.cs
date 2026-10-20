// Nachbearbeitung N5 (20.10.2026): Lehnt der Kern einen Werkzeugaufruf ab, ist das MCP-Ergebnis als Fehler gekennzeichnet (isError) – für ALLE
// Werkzeuge an einer Stelle, als Filter für tools/call. Die Werkzeuge selbst liefern Ablehnungen als JSON-Text ({fehler, vorschlag} aus der Hülle
// Werkzeuge.Lauf, BefehlsErgebnis mit angenommen false, unterlagen_lesen mit «fehler» oben); ohne Kennzeichen bekam Claude Code sie als normales
// Ergebnis und die KI-Palette zeigte z. B. unterlagen_lesen als «ok» (Befund Abnahmelauf B6). Regel: Werkzeugergebnis.MeldetFehler im Kern –
// dieselbe, nach der Palette, Auftragsprotokoll und Kommandozeile entscheiden. Werkzeuge mit eigenem Ergebnis (unterlage_bild, kontext_bild)
// setzen isError selbst; ein gesetztes Kennzeichen bleibt. Der Inhalt bleibt unverändert (Claude liest Meldung und Vorschlag weiter).

using Hlks.Kern;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Hlks.Mcp;

public static class Fehlerkennung
{
    /// <summary>Filter für tools/call: kennzeichnet abgelehnte Aufrufe (isError).</summary>
    public static McpRequestHandler<CallToolRequestParams, CallToolResult> Filter(McpRequestHandler<CallToolRequestParams, CallToolResult> weiter) =>
        async (anfrage, abbruch) => Kennzeichnen(await weiter(anfrage, abbruch));

    /// <summary>Setzt isError, wenn ein Textblock des Ergebnisses eine Ablehnung meldet (die Kopfzeile B15 ist kein JSON und zählt nicht).</summary>
    public static CallToolResult Kennzeichnen(CallToolResult r)
    {
        if (r.IsError != true && r.Content.OfType<TextContentBlock>().Any(t => Werkzeugergebnis.MeldetFehler(t.Text))) r.IsError = true;
        return r;
    }
}
