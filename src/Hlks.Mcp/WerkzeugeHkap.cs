// MCP-Werkzeuge der HKAP-Universalschnittstelle (Modul 1d). Dünne Hülle über dem Kern wie in Werkzeuge.cs; Name,
// Beschreibung und Parameter stehen genau so im Kern (Austausch/HkapBefehle.cs, HkapTexte, Abfragen.Liste).

using System.ComponentModel;
using System.Text.Json;
using Hlks.Kern;
using ModelContextProtocol.Server;

namespace Hlks.Mcp;

public sealed partial class Werkzeuge
{
    /// <summary>Das Projekt dieser Werkzeuge (der Parameter «projekt» der HKAP-Werkzeuge verdeckt den Konstruktorparameter).</summary>
    Projekt DiesesProjekt => projekt;

    [McpServerTool(Name = "hkap_verbindung"), Description(HkapTexte.Verbindung)]
    public string HkapVerbindung(
        [Description(HkapTexte.ParUrl)] string? url = null,
        [Description(HkapTexte.ParProjektListe)] string? projekt = null) =>
        Lauf(() => Antwort(HkapAbfragen.Verbindung(DiesesProjekt, url, projekt)));

    [McpServerTool(Name = "hkap_abgleich"), Description(HkapTexte.Abgleich)]
    public string HkapAbgleich(
        [Description(HkapTexte.ParDatenarten)] JsonElement? datenarten = null,
        [Description(HkapTexte.ParBuendeln)] string? buendeln = null) =>
        Lauf(() => Antwort(HkapAbfragen.Abgleich(DiesesProjekt, HkapBefehle.Liste(Parameter(("datenarten", datenarten)), "datenarten"), buendeln)));

    [McpServerTool(Name = "hkap_verknuepfen"), Description(HkapTexte.Verknuepfen)]
    public string HkapVerknuepfen(
        [Description(HkapTexte.ParProjekt)] string projekt,
        [Description(HkapTexte.ParProjektierung)] string? projektierung = null,
        [Description(HkapTexte.ParUrlVerknuepfen)] string? url = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("hkap_verknuepfen", Parameter(("projekt", projekt), ("projektierung", projektierung), ("url", url)), trockenlauf);

    [McpServerTool(Name = "hkap_uebernehmen"), Description(HkapTexte.Uebernehmen)]
    public string HkapUebernehmen(
        [Description(HkapTexte.ParDatenartenUebernehmen)] JsonElement? datenarten = null,
        [Description(HkapTexte.ParKonflikte)] JsonElement? konflikte = null,
        [Description(HkapTexte.ParSystemWahl)] JsonElement? system = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("hkap_uebernehmen", Parameter(("datenarten", datenarten), ("konflikte", konflikte), ("system", system)), trockenlauf);

    [McpServerTool(Name = "hkap_raum_zuordnen"), Description(HkapTexte.RaumZuordnen)]
    public string HkapRaumZuordnen(
        [Description(HkapTexte.ParRaum)] string raum,
        [Description(HkapTexte.ParHkapRaum)] string hkapRaum,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("hkap_raum_zuordnen", Parameter(("raum", raum), ("hkapRaum", hkapRaum)), trockenlauf);

    [McpServerTool(Name = "hkap_strang_zuordnen"), Description(HkapTexte.StrangZuordnen)]   // Modul 3, B5
    public string HkapStrangZuordnen(
        [Description(HkapTexte.ParStrang)] string strang,
        [Description(HkapTexte.ParHkapStrang)] string hkap,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("hkap_strang_zuordnen", Parameter(("strang", strang), ("hkap", hkap)), trockenlauf);

    [McpServerTool(Name = "hkap_senden"), Description(HkapTexte.Senden)]
    public string HkapSenden(
        [Description(HkapTexte.ParDatenartenSenden)] JsonElement? datenarten = null,
        [Description(HkapTexte.ParBuendeln)] string? buendeln = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("hkap_senden", Parameter(("datenarten", datenarten), ("buendeln", buendeln)), trockenlauf);

    [McpServerTool(Name = "hkap_befunde_senden"), Description(HkapTexte.BefundeSenden)]
    public string HkapBefundeSenden(
        [Description(HkapTexte.ParSchwere)] string? schwere = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("hkap_befunde_senden", Parameter(("schwere", schwere)), trockenlauf);
}
