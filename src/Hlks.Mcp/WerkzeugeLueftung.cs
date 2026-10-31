// MCP-Werkzeug der Lüftungskette (Modul 6 Teil A): Abfrage lueftung_varianten.
// Dünne Hülle über dem Kern wie in Werkzeuge.cs; Name, Beschreibung und Parameter stehen genau so im Kern (LueftungKette.Definitionen).

using System.ComponentModel;
using Hlks.Kern;
using ModelContextProtocol.Server;

namespace Hlks.Mcp;

public sealed partial class Werkzeuge
{
    [McpServerTool(Name = "lueftung_varianten"), Description(LueftungKette.AbfrageText)]
    public string LueftungVariantenLesen(
        [Description(LueftungKette.ParGeschoss)] string? geschoss = null,
        [Description(LueftungKette.ParGleichzeitigkeit)] double? gleichzeitigkeit = null,
        [Description(LueftungKette.ParVariante)] string? variante = null) =>
        Lauf(() => Antwort(LueftungKette.Rechnen(projekt, geschoss, gleichzeitigkeit, variante)));
}
