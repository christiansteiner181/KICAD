// MCP-Werkzeuge der Mengen (Modul 3, B4): Abfrage materialliste. Dünne Hülle über dem Kern wie in Werkzeuge.cs; Name, Beschreibung
// und Parameter stehen genau so im Kern (LeitungenAbfragen.Definitionen, Ableitungen.MateriallisteText).

using System.ComponentModel;
using Hlks.Kern;
using ModelContextProtocol.Server;

namespace Hlks.Mcp;

public sealed partial class Werkzeuge
{
    // ---------- Abfragen ----------

    [McpServerTool(Name = "materialliste"), Description(Ableitungen.MateriallisteText)]
    public string MateriallisteLesen(
        [Description("Nur dieses System (Kennzeichen)")] string? system = null,
        [Description("Nur dieser Strang (Kennzeichen)")] string? strang = null,
        [Description("Nur dieses Geschoss (Kennzeichen)")] string? geschoss = null) =>
        Lauf(() => Antwort(Ableitungen.Materialliste(projekt, system, strang, geschoss)));
}
