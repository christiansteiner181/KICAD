// MCP-Werkzeuge der Sanitär-Belastungswerte (Modul 4 Teil A-K): Abfragen sanitaer_belastung, sanitaer_zusammenstellung, sanitaer_katalog.
// Dünne Hülle über dem Kern wie in Werkzeuge.cs; Name, Beschreibung und Parameter stehen genau so im Kern (SanitaerAbfragen.Definitionen).
// Die Apparateart (Eigenschaft sanitaerArt) ändert element_aendern.

using System.ComponentModel;
using Hlks.Kern;
using ModelContextProtocol.Server;

namespace Hlks.Mcp;

public sealed partial class Werkzeuge
{
    [McpServerTool(Name = "sanitaer_belastung"), Description(SanitaerAbfragen.BelastungText)]
    public string SanitaerBelastungLesen(
        [Description(SanitaerAbfragen.ParAnlage)] string? anlage = null,
        [Description(SanitaerAbfragen.ParStrang)] string? strang = null,
        [Description(SanitaerAbfragen.ParLeitung)] string? leitung = null) =>
        Lauf(() => Antwort(SanitaerAbfragen.Belastung(projekt, anlage, strang, leitung)));

    [McpServerTool(Name = "sanitaer_zusammenstellung"), Description(SanitaerZusammenstellung.AbfrageText)]
    public string SanitaerZusammenstellungLesen([Description(SanitaerZusammenstellung.ParAnlage)] string? anlage = null) =>
        Lauf(() => Antwort(SanitaerAbfragen.Zusammenstellung(projekt, anlage)));

    [McpServerTool(Name = "sanitaer_katalog"), Description(SanitaerAbfragen.KatalogText)]
    public string SanitaerKatalogLesen() => Lauf(() => Antwort(SanitaerAbfragen.KatalogLesen(projekt)));
}
