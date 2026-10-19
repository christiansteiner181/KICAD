// MCP-Werkzeuge für BCF 3.0 (Modul 3b, B6): Abfragen bcf_export und bcf_lesen, Befehl bcf_import. Dünne Hülle über dem Kern wie in
// Werkzeuge.cs; Name, Beschreibung und Parameter stehen genau so im Kern (Kollision/BcfBefehle.cs: BcfAbfragen.Definitionen,
// BcfTexte, BcfImport).

using System.ComponentModel;
using System.Text.Json;
using Hlks.Kern;
using ModelContextProtocol.Server;

namespace Hlks.Mcp;

public sealed partial class Werkzeuge
{
    // ---------- Abfragen ----------

    [McpServerTool(Name = "bcf_export"), Description(BcfTexte.Export)]
    public string BcfExportieren(
        [Description(BcfTexte.ParDateiExport)] string datei,
        [Description(BcfTexte.ParKollisionen)] JsonElement? kollisionen = null,
        [Description(BcfTexte.ParStatus)] JsonElement? status = null,
        [Description(BcfTexte.ParArt)] string? art = null,
        [Description(BcfTexte.ParAutor)] string? autor = null,
        [Description(BcfTexte.ParKoordinaten)] string? koordinaten = null) =>
        Lauf(() => Antwort(BcfExport.Schreiben(projekt, Parameter(("datei", datei), ("kollisionen", kollisionen), ("status", status),
            ("art", art), ("autor", autor), ("koordinaten", koordinaten)))));

    [McpServerTool(Name = "bcf_lesen"), Description(BcfTexte.Lesen)]
    public string BcfLesenWerkzeug([Description(BcfTexte.ParDateiLesen)] string datei) =>
        Lauf(() => Antwort(BcfLesen.Abfrage(projekt, datei)));

    // ---------- Befehle ----------

    [McpServerTool(Name = "bcf_import"), Description(BcfTexte.Import)]
    public string BcfImportieren(
        [Description(BcfTexte.ParDateiLesen)] string datei,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("bcf_import", Parameter(("datei", datei)), trockenlauf);
}
