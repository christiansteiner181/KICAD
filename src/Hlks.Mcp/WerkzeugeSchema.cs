// MCP-Werkzeuge des Schemas (Modul 3, B3 und Nachtrag B8): Abfrage schema, Befehle schema_lage, schema_anordnen und
// schema_anlegen. Dünne Hülle über dem Kern wie in Werkzeuge.cs; Name, Beschreibung und Parameter stehen genau so im Kern
// (Schema/SchemaBefehle.cs: SchemaAbfragen.Definitionen, SchemaLage.Text, SchemaAnordnen.Text, SchemaAnlegen.Text, SchemaBefehle.Par*).

using System.ComponentModel;
using Hlks.Kern;
using ModelContextProtocol.Server;

namespace Hlks.Mcp;

public sealed partial class Werkzeuge
{
    // ---------- Abfragen ----------

    [McpServerTool(Name = "schema"), Description(SchemaAbfragen.SchemaText)]
    public string SchemaLesen([Description(SchemaBefehle.ParAnsicht)] string ansicht) =>
        Lauf(() => Antwort(SchemaAbfragen.Schema(projekt, ansicht)));

    // ---------- Befehle ----------

    [McpServerTool(Name = "schema_lage"), Description(SchemaLage.Text)]
    public string SchemaLageSetzen(
        [Description(SchemaBefehle.ParAnsicht)] string ansicht,
        [Description(SchemaBefehle.ParElement)] string element,
        [Description(SchemaBefehle.ParX)] double? x = null,
        [Description(SchemaBefehle.ParY)] double? y = null,
        [Description(SchemaBefehle.ParZuruecksetzen)] bool zuruecksetzen = false,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false)
    {
        var par = Parameter(("ansicht", ansicht), ("element", element), ("x", x), ("y", y));
        if (zuruecksetzen) par["zuruecksetzen"] = true;
        return Befehl("schema_lage", par, trockenlauf);
    }

    [McpServerTool(Name = "schema_anordnen"), Description(SchemaAnordnen.Text)]
    public string SchemaAnordnenWerkzeug(
        [Description(SchemaBefehle.ParAnsicht)] string ansicht,
        [Description(SchemaBefehle.ParAlle)] bool alle = false,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false)
    {
        var par = Parameter(("ansicht", ansicht));
        if (alle) par["alle"] = true;
        return Befehl("schema_anordnen", par, trockenlauf);
    }

    [McpServerTool(Name = "schema_anlegen"), Description(SchemaAnlegen.Text)]
    public string SchemaAnlegenWerkzeug(
        [Description(SchemaBefehle.ParVorlage)] string vorlage,
        [Description(SchemaBefehle.ParName)] string? name = null,
        [Description(SchemaBefehle.ParKennzeichen)] string? kennzeichen = null,
        [Description(SchemaBefehle.ParBlickrichtung)] string? blickrichtung = null,
        [Description(SchemaBefehle.ParWohnungsbloecke)] bool wohnungsbloecke = false,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false)
    {
        var par = Parameter(("vorlage", vorlage), ("name", name), ("kennzeichen", kennzeichen), ("blickrichtung", blickrichtung));
        if (wohnungsbloecke) par["wohnungsbloecke"] = true;
        return Befehl("schema_anlegen", par, trockenlauf);
    }
}
