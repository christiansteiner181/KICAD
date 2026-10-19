// MCP-Werkzeuge der Normensuche (Modul 3c, Teil K): normen_suchen und norm_lesen. Dünne Hülle über der Tür des Kerns (NormenAbfragen);
// Name, Beschreibung und Parameter stehen genau so im Kern. Die Wurzel kommt aus HLKS_NORMEN (bzw. --normen beim Start des Servers).

using System.ComponentModel;
using System.Text.Json.Nodes;
using Hlks.Kern;
using ModelContextProtocol.Server;

namespace Hlks.Mcp;

public sealed partial class Werkzeuge
{
    [McpServerTool(Name = "normen_suchen"), Description(NormenAbfragen.SuchenText)]
    public string NormenSuchen(
        [Description(NormenAbfragen.ParFrage)] string frage,
        [Description(NormenAbfragen.ParReihe)] string? reihe = null) =>
        Lauf(() => Antwort((JsonNode)Tuer.Abfrage(projekt, "normen_suchen", Parameter(("frage", frage), ("reihe", reihe)))), synchronisieren: false);

    [McpServerTool(Name = "norm_lesen"), Description(NormenAbfragen.LesenText)]
    public string NormLesen(
        [Description(NormenAbfragen.ParDatei)] string datei,
        [Description(NormenAbfragen.ParSeite)] double seite,
        [Description(NormenAbfragen.ParBis)] double? bis = null) =>
        Lauf(() => (string)Tuer.Abfrage(projekt, "norm_lesen", Parameter(("datei", datei), ("seite", seite), ("bis", bis))), synchronisieren: false);
}
