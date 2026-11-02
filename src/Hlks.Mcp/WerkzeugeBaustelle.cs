// MCP-Werkzeuge zu BIM to field (entwurf/bim-to-field.md): Abfragen montageabschnitte, rueckmeldung_lesen; Befehle montagestatus_setzen,
// rueckmeldung_uebernehmen (beide für die KI-Palette gesperrt, GespraechDienst.Verbote). Dünne Hülle über dem Kern wie in Werkzeuge.cs.

using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hlks.Kern;
using ModelContextProtocol.Server;

namespace Hlks.Mcp;

public sealed partial class Werkzeuge
{
    [McpServerTool(Name = "montageabschnitte"), Description(BaustelleDaten.AbschnitteText)]
    public string MontageabschnitteLesen([Description("Kennzeichen eines Montageabschnitts (MA-n) für die Elementliste")] string? abschnitt = null) =>
        Lauf(() => Antwort(BaustelleDaten.Abschnitte(projekt, abschnitt)));

    [McpServerTool(Name = "rueckmeldung_lesen"), Description(BaustelleRueckmeldung.LesenText)]
    public string RueckmeldungLesen(
        [Description(BaustelleRueckmeldung.ParDatei)] string? datei = null,
        [Description(BaustelleRueckmeldung.ParInhalt)] JsonElement? inhalt = null) =>
        Lauf(() =>
        {
            var par = Parameter(("datei", datei));
            if (Knoten(inhalt) is JsonObject o) par["inhalt"] = o;
            return Antwort(BaustelleRueckmeldung.Lesen(projekt, BaustelleRueckmeldung.Laden(par)));
        });

    [McpServerTool(Name = "montagestatus_setzen"), Description(BaustelleDaten.StatusText)]
    public string MontagestatusSetzenWerkzeug(
        [Description("geplant | freigegeben | montiert | geprueft | abgenommen")] string status,
        [Description("Kennzeichen der Elemente")] JsonElement? elemente = null,
        [Description("Montageabschnitt (MA-n): alle seine Elemente")] string? abschnitt = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false)
    {
        var par = Parameter(("status", status), ("abschnitt", abschnitt));
        if (Knoten(elemente) is JsonArray l) par["elemente"] = l;
        return Befehl("montagestatus_setzen", par, trockenlauf);
    }

    [McpServerTool(Name = "rueckmeldung_uebernehmen"), Description(BaustelleRueckmeldung.UebernehmenText)]
    public string RueckmeldungUebernehmenWerkzeug(
        [Description(BaustelleRueckmeldung.ParDatei)] string? datei = null,
        [Description(BaustelleRueckmeldung.ParInhalt)] JsonElement? inhalt = null,
        [Description("Nur diese Meldungen (Ids); ohne: alle noch nicht übernommenen")] JsonElement? meldungen = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false)
    {
        var par = Parameter(("datei", datei));
        if (Knoten(inhalt) is JsonObject o) par["inhalt"] = o;
        if (Knoten(meldungen) is JsonArray l) par["meldungen"] = l;
        return Befehl("rueckmeldung_uebernehmen", par, trockenlauf);
    }

    [McpServerTool(Name = "baustelle_export"), Description(BaustelleExport.AbfrageText)]
    public string BaustelleExportWerkzeug(
        [Description(BaustelleExport.ParDatei)] string? datei = null,
        [Description(BaustelleExport.ParRolle)] string? rolle = null,
        [Description(BaustelleExport.ParStand)] string? stand = null) =>
        Lauf(() => Antwort(BaustelleExport.Schreiben(projekt, Parameter(("datei", datei), ("rolle", rolle), ("stand", stand)))));
}
