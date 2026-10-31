// MCP-Werkzeuge der Lüftungskette (Modul 6 Teil A): Abfragen lueftung_varianten, raumforderung; Befehle raumforderung_anlegen, lueftung_variante_uebernehmen.
// Dünne Hülle über dem Kern wie in Werkzeuge.cs; Name, Beschreibung und Parameter stehen genau so im Kern (LueftungKette.Definitionen).

using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hlks.Kern;
using ModelContextProtocol.Server;

namespace Hlks.Mcp;

public sealed partial class Werkzeuge
{
    [McpServerTool(Name = "lueftung_varianten"), Description(LueftungKette.AbfrageText)]
    public string LueftungVariantenLesen(
        [Description(LueftungKette.ParGeschoss)] string? geschoss = null,
        [Description(LueftungKette.ParGleichzeitigkeit)] double? gleichzeitigkeit = null,
        [Description(LueftungKette.ParVariante)] string? variante = null,
        [Description(LueftungKette.ParGewichte)] JsonElement? gewichte = null) =>
        Lauf(() => Antwort(LueftungKette.Rechnen(projekt, geschoss, gleichzeitigkeit, variante, Knoten(gewichte) as JsonObject)));

    [McpServerTool(Name = "lueftung_schema"), Description(LueftungSchema.AbfrageText)]
    public string LueftungSchemaLesen(
        [Description(LueftungTexte.ParVariante)] string? variante = null,
        [Description(LueftungKette.ParGleichzeitigkeit)] double? gleichzeitigkeit = null) =>
        Lauf(() => Antwort(LueftungSchema.AlsJson(LueftungSchema.Aufbauen(projekt, variante, gleichzeitigkeit))));

    [McpServerTool(Name = "raumforderung"), Description(LueftungTexte.Liste)]
    public string RaumforderungLesen() => Lauf(() => Antwort(LueftungBefehle.Liste(projekt)));

    [McpServerTool(Name = "raumforderung_anlegen"), Description(LueftungTexte.Anlegen)]
    public string RaumforderungAnlegenWerkzeug(
        [Description(LueftungTexte.ParVariante)] string variante,
        [Description(LueftungKette.ParGeschoss)] string? geschoss = null,
        [Description(LueftungKette.ParGleichzeitigkeit)] double? gleichzeitigkeit = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("raumforderung_anlegen", Parameter(("variante", variante), ("geschoss", geschoss), ("gleichzeitigkeit", gleichzeitigkeit)), trockenlauf);

    [McpServerTool(Name = "lueftung_variante_uebernehmen"), Description(LueftungTexte.Uebernehmen)]
    public string LueftungVarianteUebernehmenWerkzeug(
        [Description(LueftungTexte.ParVariante)] string variante,
        [Description(LueftungKette.ParGeschoss)] string? geschoss = null,
        [Description(LueftungKette.ParGleichzeitigkeit)] double? gleichzeitigkeit = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("lueftung_variante_uebernehmen", Parameter(("variante", variante), ("geschoss", geschoss), ("gleichzeitigkeit", gleichzeitigkeit)), trockenlauf);
}
