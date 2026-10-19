// MCP-Werkzeuge der Raumgrenzen (Modul 1c, Auftrag B1): raumgrenzen, huellbauteile, waermebruecken_vorschlagen.
// Dünne Hülle über dem Kern wie in Werkzeuge.cs; Name, Beschreibung und Parameter stehen genau so im Kern
// (RaumgrenzenAbfragen.Definitionen, WaermebrueckenVorschlagen).

using System.ComponentModel;
using Hlks.Kern;
using ModelContextProtocol.Server;

namespace Hlks.Mcp;

public sealed partial class Werkzeuge
{
    [McpServerTool(Name = "raumgrenzen"), Description(RaumgrenzenAbfragen.BeschreibungRaumgrenzen)]
    public string RaumgrenzenLesen(
        [Description("Nur dieser Raum (Kennzeichen oder Id)")] string? raum = null,
        [Description("Nur Räume dieses Geschosses")] string? geschoss = null,
        [Description(RaumgrenzenAbfragen.GeometrieBeschreibung)] bool geometrie = false) =>
        Lauf(() => Antwort(RaumgrenzenAbfragen.Raumgrenzen(projekt, raum, geschoss, geometrie)));

    [McpServerTool(Name = "huellbauteile"), Description(RaumgrenzenAbfragen.BeschreibungHuellbauteile)]
    public string Huellbauteile([Description("Nur Bauteile dieses Geschosses")] string? geschoss = null) =>
        Lauf(() => Antwort(RaumgrenzenAbfragen.Huellbauteile(projekt, geschoss)));

    [McpServerTool(Name = "waermebruecken_vorschlagen"), Description(Hlks.Kern.WaermebrueckenVorschlagen.BeschreibungText)]
    public string WaermebrueckenVorschlagen(
        [Description("Nur dieses Geschoss (Kennzeichen oder Id); Vorgabe alle")] string? geschoss = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("waermebruecken_vorschlagen", Parameter(("geschoss", geschoss)), trockenlauf);
}
