// MCP-Werkzeuge der automatischen Aussparungen (Modul 3b, B3): Befehle aussparungen_vorschlagen, aussparungen_nummerieren,
// aussparung_status, aussparung_form (Nachtrag B3b), aussparungen_abgleichen; Abfragen aussparungsliste und (B4b) aussparungen_ifc. Dünne Hülle über dem Kern wie in Werkzeuge.cs; Name,
// Beschreibung und Parameter stehen genau so im Kern (Aussparungen/AussparungBefehle.cs: AussparungTexte).

using System.ComponentModel;
using System.Text.Json;
using Hlks.Kern;
using ModelContextProtocol.Server;

namespace Hlks.Mcp;

public sealed partial class Werkzeuge
{
    // ---------- Abfragen ----------

    [McpServerTool(Name = "aussparungsliste"), Description(AussparungTexte.Liste)]
    public string AussparungslisteLesen([Description(AussparungTexte.ParGeschossListe)] string? geschoss = null) =>
        Lauf(() => Antwort(Aussparungsliste.Abfrage(projekt, geschoss)));

    // B4b: IFC-Ausgabe für Architekt und Bauingenieur – Abfrage wie bcf_export (schreibt die Datei, ändert das Modell nicht);
    // den Status «versandt» setzt anschliessend aussparung_status
    [McpServerTool(Name = "aussparungen_ifc"), Description(AussparungIfcTexte.Export)]
    public string AussparungenIfcSchreiben(
        [Description(AussparungIfcTexte.ParDatei)] string datei,
        [Description(AussparungIfcTexte.ParFassung)] string? fassung = null,
        [Description(AussparungIfcTexte.ParGeschoss)] string? geschoss = null,
        [Description(AussparungIfcTexte.ParStatus)] JsonElement? status = null,
        [Description(AussparungIfcTexte.ParKoordinaten)] string? koordinaten = null) =>
        Lauf(() => Antwort(AussparungIfcExport.Schreiben(projekt, Parameter(("datei", datei), ("fassung", fassung), ("geschoss", geschoss),
            ("status", status), ("koordinaten", koordinaten)))));

    // ---------- Befehle ----------

    [McpServerTool(Name = "aussparungen_vorschlagen"), Description(AussparungTexte.Vorschlagen)]
    public string AussparungenVorschlagenWerkzeug(
        [Description(AussparungTexte.ParGeschossVorschlagen)] string? geschoss = null,
        [Description(AussparungTexte.ParBauteile)] JsonElement? bauteile = null,
        [Description(AussparungTexte.ParLeitungen)] JsonElement? leitungen = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("aussparungen_vorschlagen", Parameter(("geschoss", geschoss), ("bauteile", bauteile), ("leitungen", leitungen)), trockenlauf);

    [McpServerTool(Name = "aussparungen_nummerieren"), Description(AussparungTexte.Nummerieren)]
    public string AussparungenNummerierenWerkzeug(
        [Description(AussparungTexte.ParGeschossNummerieren)] string? geschoss = null,
        [Description(AussparungTexte.ParAlle)] bool alle = false,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false)
    {
        var p = Parameter(("geschoss", geschoss));
        if (alle) p["alle"] = true;
        return Befehl("aussparungen_nummerieren", p, trockenlauf);
    }

    [McpServerTool(Name = "aussparung_status"), Description(AussparungTexte.Status)]
    public string AussparungStatusWerkzeug(
        [Description(AussparungTexte.ParAussparungen)] JsonElement aussparungen,
        [Description(AussparungTexte.ParStatus)] string status,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("aussparung_status", Parameter(("aussparungen", aussparungen), ("status", status)), trockenlauf);

    // Nachtrag B3b: Kernbohrungen nur von Hand (Entscheid Christian 19.10.2026)
    [McpServerTool(Name = "aussparung_form"), Description(AussparungTexte.Form)]
    public string AussparungFormWerkzeug(
        [Description(AussparungTexte.ParAussparungen)] JsonElement aussparungen,
        [Description(AussparungTexte.ParForm)] string form,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("aussparung_form", Parameter(("aussparungen", aussparungen), ("form", form)), trockenlauf);

    [McpServerTool(Name = "aussparungen_abgleichen"), Description(AussparungTexte.Abgleichen)]
    public string AussparungenAbgleichenWerkzeug(
        [Description(AussparungTexte.ParUnterlage)] string unterlage,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("aussparungen_abgleichen", Parameter(("unterlage", unterlage)), trockenlauf);
}
