// MCP-Werkzeuge der Kollisionsprüfung (Modul 3b, B2): Befehle vorgaben_setzen, kollision_pruefen, kollision_status; Abfragen
// vorgaben, kollisionen; Nachtrag B3d: typmaterial_setzen und typmaterial (Kollision/Typenliste.cs: TypmaterialTexte).
// Dünne Hülle über dem Kern wie in Werkzeuge.cs; Name, Beschreibung und Parameter stehen genau so im Kern
// (Kollision/Vorgaben.cs: VorgabenTexte, VorgabenSetzen; Kollision/Kollisionspruefung.cs: KollisionTexte, KollisionPruefen;
// Kollision/Kollisionsliste.cs: KollisionStatusSetzen, Kollisionsliste.Definitionen).

using System.ComponentModel;
using System.Text.Json;
using Hlks.Kern;
using ModelContextProtocol.Server;

namespace Hlks.Mcp;

public sealed partial class Werkzeuge
{
    // ---------- Abfragen ----------

    [McpServerTool(Name = "vorgaben"), Description(VorgabenTexte.Abfrage)]
    public string VorgabenLesen() => Lauf(() => Antwort(VorgabenAbfrage.Lesen(projekt)));

    [McpServerTool(Name = "kollisionen"), Description(KollisionTexte.Liste)]
    public string KollisionenLesen(
        [Description(KollisionTexte.ParListeStatus)] string? status = null,
        [Description(KollisionTexte.ParListeArt)] string? art = null,
        [Description(KollisionTexte.ParListeTyp)] string? typ = null,
        [Description(KollisionTexte.ParListeGeschoss)] string? geschoss = null,
        [Description(KollisionTexte.ParListeElement)] string? element = null,
        [Description(KollisionTexte.ParListePaar)] string? paar = null) =>
        Lauf(() => Antwort(Kollisionsliste.Lesen(projekt, status, art, typ, geschoss, element, paar)));

    // ---------- Befehle ----------

    [McpServerTool(Name = "vorgaben_setzen"), Description(VorgabenTexte.Setzen)]
    public string VorgabenSetzenWerkzeug(
        [Description(VorgabenTexte.ParWerte)] JsonElement werte,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("vorgaben_setzen", Parameter(("werte", werte)), trockenlauf);

    [McpServerTool(Name = "kollision_pruefen"), Description(KollisionTexte.Pruefen)]
    public string KollisionPruefenWerkzeug(
        [Description(KollisionTexte.ParPaare)] JsonElement? paare = null,
        [Description(KollisionTexte.ParUnterlagen)] JsonElement? unterlagen = null,
        [Description(KollisionTexte.ParGeschoss)] string? geschoss = null,
        [Description(KollisionTexte.ParWeich)] bool weich = true,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false)
    {
        var par = Parameter(("paare", paare), ("unterlagen", unterlagen), ("geschoss", geschoss));
        if (!weich) par["weich"] = false;
        return Befehl("kollision_pruefen", par, trockenlauf);
    }

    // ---------- Typenliste (Nachtrag B3d: Kollision/Typenliste.cs) ----------

    [McpServerTool(Name = "typmaterial"), Description(TypmaterialTexte.Abfrage)]
    public string TypmaterialLesen([Description(TypmaterialTexte.ParBetroffen)] bool betroffen = false) =>
        Lauf(() => Antwort(TypmaterialAbfrage.Lesen(projekt, betroffen)));

    [McpServerTool(Name = "typmaterial_setzen"), Description(TypmaterialTexte.Setzen)]
    public string TypmaterialSetzenWerkzeug(
        [Description(TypmaterialTexte.ParTypen)] JsonElement typen,
        [Description(TypmaterialTexte.ParQuelle)] string? quelle = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("typmaterial_setzen", Parameter(("typen", typen), ("quelle", quelle)), trockenlauf);

    [McpServerTool(Name = "kollision_status"), Description(KollisionTexte.Status)]
    public string KollisionStatusWerkzeug(
        [Description(KollisionTexte.ParKollisionen)] JsonElement kollisionen,
        [Description(KollisionTexte.ParStatus)] string status,
        [Description(KollisionTexte.ParKommentar)] string? kommentar = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("kollision_status", Parameter(("kollisionen", kollisionen), ("status", status), ("kommentar", kommentar)), trockenlauf);
}
