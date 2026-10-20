// MCP-Werkzeug des IFC-Exports (Modul 7 Teil B2): Abfrage ifc_export – schreibt die IFC-Datei samt Bericht, ändert das Modell nicht.
// Dünne Hülle über dem Kern wie in Werkzeuge.cs; Name, Beschreibung und Parameter stehen genau so im Kern (IfcExport/IfcExport.cs:
// IfcExportTexte).

using System.ComponentModel;
using System.Text.Json;
using Hlks.Kern;
using ModelContextProtocol.Server;

namespace Hlks.Mcp;

public sealed partial class Werkzeuge
{
    [McpServerTool(Name = "ifc_export"), Description(IfcExportTexte.Export)]
    public string IfcExportieren(
        [Description(IfcExportTexte.ParDatei)] string? datei = null,
        [Description(IfcExportTexte.ParFassung)] string? fassung = null,
        [Description(IfcExportTexte.ParUmfang)] JsonElement? umfang = null,
        [Description(IfcExportTexte.ParRaeume)] bool raeume = false,
        [Description(IfcExportTexte.ParIds)] JsonElement? ids = null)
    {
        var p = Parameter(("datei", datei), ("fassung", fassung), ("umfang", umfang), ("ids", ids));
        if (raeume) p["raeume"] = true;
        return Lauf(() => Antwort(IfcExport.Schreiben(projekt, p)));
    }
}
