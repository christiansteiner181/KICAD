// MCP-Werkzeuge der Ausrichtung der Grundrisse (Nachbearbeitung N9): planwinkel (Abfrage), planwinkel_bestimmen (Befehl); N14: ansicht_raum_zeigen (Befehl); N15: raum_begehen (Abfrage).
// Dünne Hülle über dem Kern wie in Werkzeuge.cs; Name, Beschreibung und Parameter stehen genau so im Kern (Architektur/Planwinkel.cs).
// Die Ausrichtung einer Ansicht selbst (geometrie | himmelsrichtung) setzt element_aendern (Eigenschaft ausrichtung).

using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hlks.Kern;
using ModelContextProtocol.Server;

namespace Hlks.Mcp;

public sealed partial class Werkzeuge
{
    [McpServerTool(Name = "planwinkel"), Description(PlanwinkelAbfragen.Text)]
    public string PlanwinkelLesen(
        [Description("Nur dieses Gebäude (Kennzeichen)")] string? gebaeude = null,
        [Description("Ansicht (Kennzeichen): ihre Ausrichtung und Drehung")] string? ansicht = null) =>
        Lauf(() => Antwort((JsonNode)Tuer.Abfrage(projekt, "planwinkel", Parameter(("gebaeude", gebaeude), ("ansicht", ansicht)))));

    [McpServerTool(Name = "schnitthoehe_sia"), Description(SchnitthoeheAbfragen.Text)]
    public string SchnitthoeheSiaLesen(
        [Description("Grundriss (Kennzeichen): Wert, Schalter und geltende Höhe")] string? ansicht = null,
        [Description("Geschoss (Kennzeichen), wenn keine Ansicht genannt ist")] string? geschoss = null) =>
        Lauf(() => Antwort((JsonNode)Tuer.Abfrage(projekt, "schnitthoehe_sia", Parameter(("ansicht", ansicht), ("geschoss", geschoss)))));

    [McpServerTool(Name = "planwinkel_bestimmen"), Description(PlanwinkelBestimmen.Text)]
    public string PlanwinkelBestimmenWerkzeug(
        [Description("Gebäude (Kennzeichen); fehlt es, alle Gebäude")] string? gebaeude = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("planwinkel_bestimmen", Parameter(("gebaeude", gebaeude)), trockenlauf);

    [McpServerTool(Name = "raum_begehen"), Description(RaumBegehenAbfragen.Text)]
    public string RaumBegehenLesen(
        [Description(RaumBegehenAbfragen.ParRaum)] string raum,
        [Description(RaumBegehenAbfragen.ParAugenhoehe)] double? augenhoehe = null) =>
        Lauf(() => Antwort((JsonNode)Tuer.Abfrage(projekt, "raum_begehen", Parameter(("raum", raum), ("augenhoehe", augenhoehe)))));

    [McpServerTool(Name = "ansicht_raum_zeigen"), Description(AnsichtRaumZeigen.Text)]
    public string AnsichtRaumZeigenWerkzeug(
        [Description(AnsichtRaumZeigen.ParRaum)] JsonElement raum,
        [Description(AnsichtRaumZeigen.ParAnsicht)] string? ansicht = null,
        [Description(AnsichtRaumZeigen.ParRand)] double? rand = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("ansicht_raum_zeigen", Parameter(("raum", raum), ("ansicht", ansicht), ("rand", rand)), trockenlauf);
}
