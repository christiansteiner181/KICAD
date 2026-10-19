// MCP-Werkzeuge der projektübergreifenden Familienbibliothek (Modul 2, Nachtrag B7): familie_in_bibliothek (als_aktuell nur ein
// Mensch – der Kern lehnt die KI ab), familie_aktualisieren (Trockenlauf = Vorschau), familienauswahl_setzen. Die Abfrage
// familien_bibliothek und familie_laden stehen in WerkzeugeFamilien.cs. Dünne Hülle über dem Kern; Name, Beschreibung und Parameter
// stehen genau so im Kern (FamilienBibliothekBefehle.cs).

using System.ComponentModel;
using System.Text.Json;
using Hlks.Kern;
using ModelContextProtocol.Server;

namespace Hlks.Mcp;

public sealed partial class Werkzeuge
{
    [McpServerTool(Name = "familie_in_bibliothek"), Description(FamilieInBibliothek.Text)]
    public string FamilieInBibliothekWerkzeug(
        [Description("Familien (Kennzeichen, Name oder id); leer = alle Familien des Projekts")] JsonElement? familien = null,
        [Description("Die Fassung des Projekts als neue aktuelle Version setzen, auch wenn die Familie parallel geändert wurde (nur ein Mensch)")] bool als_aktuell = false,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false)
    {
        var par = Parameter(("familien", familien));
        if (als_aktuell) par["als_aktuell"] = true;
        return Befehl("familie_in_bibliothek", par, trockenlauf);
    }

    [McpServerTool(Name = "familie_aktualisieren"), Description(FamilieAktualisieren.Text)]
    public string FamilieAktualisierenWerkzeug(
        [Description("Familie des Projekts (Kennzeichen, Name oder id)")] string familie,
        [Description("Version der Bibliothek (Nummer, z. B. 5) oder Name eines Abzweigs; leer = aktuelle Version")] string? version = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("familie_aktualisieren", Parameter(("familie", familie), ("version", version)), trockenlauf);

    [McpServerTool(Name = "familienauswahl_setzen"), Description(FamilienauswahlSetzen.Text)]
    public string FamilienauswahlSetzenWerkzeug(
        [Description("{ gewerke, kategorien, hersteller, status, ein, aus } – je eine Liste von Texten; null bzw. weglassen = nur die Familien des Projekts")] JsonElement? auswahl = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("familienauswahl_setzen", Parameter(("auswahl", auswahl)), trockenlauf);
}
