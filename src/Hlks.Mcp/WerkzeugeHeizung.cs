// MCP-Werkzeuge der Heizung (Modul 5 Teil A-D): Abfragen heizung_lesen, heizung_leistung; Befehle system_paar_anlegen, heizgruppe_anlegen,
// heizgruppe_aendern, heizgruppen_vorschlagen. Dünne Hülle über dem Kern wie in Werkzeuge.cs; Name, Beschreibung und Parameter stehen genau so im
// Kern (Heizung/HeizungAbfragen.cs, Heizung/HeizungBefehle.cs).

using System.ComponentModel;
using System.Text.Json.Nodes;
using Hlks.Kern;
using ModelContextProtocol.Server;

namespace Hlks.Mcp;

public sealed partial class Werkzeuge
{
    // ---------- Abfragen ----------

    [McpServerTool(Name = "heizung_lesen"), Description(HeizungAbfragen.LesenText)]
    public string HeizungLesen([Description(HeizungAbfragen.ParAnlage)] string? anlage = null) =>
        Lauf(() => HeizungAbfragen.Lesen(projekt, anlage));

    [McpServerTool(Name = "heizlast"), Description(Heizlast.Text)]   // Modul 5 Teil B-H, Christian 03.11.2026
    public string HeizlastRechnen(
        [Description(Heizlast.ParRaum)] string? raum = null,
        [Description(Heizlast.ParGebaeude)] string? gebaeude = null) =>
        Lauf(() => Antwort((JsonNode)Tuer.Abfrage(projekt, "heizlast", Parameter(("raum", raum), ("gebaeude", gebaeude)))), synchronisieren: false);

    [McpServerTool(Name = "heizung_leistung"), Description(HeizungAbfragen.LeistungText)]
    public string HeizungLeistungLesen(
        [Description(HeizungAbfragen.ParAnlage)] string? anlage = null,
        [Description(HeizungAbfragen.ParHeizgruppe)] string? heizgruppe = null,
        [Description(HeizungAbfragen.ParRaum)] string? raum = null,
        [Description(HeizungAbfragen.ParLeitung)] string? leitung = null) =>
        Lauf(() => Antwort(HeizungAbfragen.Leistung(projekt, anlage, heizgruppe, raum, leitung)));

    // ---------- Befehle ----------

    [McpServerTool(Name = "system_paar_anlegen"), Description(SystemPaarAnlegen.Text)]
    public string SystemPaarAnlegenWerkzeug(
        [Description(HeizungBefehle.ParVorlaufPaar)] string? vorlauf = null,
        [Description(HeizungBefehle.ParRuecklaufPaar)] string? ruecklauf = null,
        [Description(HeizungBefehle.ParSystemtyp)] string? systemtyp = null,
        [Description(HeizungBefehle.ParAnlagePaar)] string? anlage = null,
        [Description(HeizungBefehle.ParNamePaar)] string? name = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("system_paar_anlegen", Parameter(("vorlauf", vorlauf), ("ruecklauf", ruecklauf), ("systemtyp", systemtyp), ("anlage", anlage), ("name", name)), trockenlauf);

    [McpServerTool(Name = "heizgruppe_anlegen"), Description(HeizgruppeAnlegen.Text)]
    public string HeizgruppeAnlegenWerkzeug(
        [Description(HeizungBefehle.ParVerteiler)] string? verteiler = null,
        [Description(HeizungBefehle.ParAbgangVl)] string? abgangVl = null,
        [Description(HeizungBefehle.ParAbgangRl)] string? abgangRl = null,
        [Description(HeizungBefehle.ParPumpe)] string? pumpe = null,
        [Description(HeizungBefehle.ParAnlage)] string? anlage = null,
        [Description(HeizungBefehle.ParVorlauf)] string? vorlauf = null,
        [Description(HeizungBefehle.ParRuecklauf)] string? ruecklauf = null,
        [Description(HeizungBefehle.ParKennzeichen)] string? kennzeichen = null,
        [Description(HeizungBefehle.ParName)] string? name = null,
        [Description(HeizungBefehle.ParArt)] string? art = null,
        [Description(HeizungBefehle.ParVerbraucherart)] string? verbraucherart = null,
        [Description(HeizungBefehle.ParVlTemp)] double? vlTemp = null,
        [Description(HeizungBefehle.ParRlTemp)] double? rlTemp = null,
        [Description(HeizungBefehle.ParQuelle)] string? quelle = null,
        [Description(HeizungBefehle.ParStrangschema)] string? strangschema = null,
        [Description(HeizungBefehle.ParBeschreibung)] string? beschreibung = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("heizgruppe_anlegen", Parameter(("verteiler", verteiler), ("abgangVl", abgangVl), ("abgangRl", abgangRl), ("pumpe", pumpe), ("anlage", anlage),
            ("vorlauf", vorlauf), ("ruecklauf", ruecklauf), ("kennzeichen", kennzeichen), ("name", name), ("art", art), ("verbraucherart", verbraucherart),
            ("vlTemp", vlTemp), ("rlTemp", rlTemp), ("quelle", quelle), ("strangschema", strangschema), ("beschreibung", beschreibung)), trockenlauf);

    [McpServerTool(Name = "heizgruppe_aendern"), Description(HeizgruppeAendern.Text)]
    public string HeizgruppeAendernWerkzeug(
        [Description(HeizungBefehle.ParHeizgruppe)] string heizgruppe,
        [Description(HeizungBefehle.ParVerteiler)] string? verteiler = null,
        [Description(HeizungBefehle.ParAbgangVl)] string? abgangVl = null,
        [Description(HeizungBefehle.ParAbgangRl)] string? abgangRl = null,
        [Description(HeizungBefehle.ParPumpe)] string? pumpe = null,
        [Description(HeizungBefehle.ParAnlage)] string? anlage = null,
        [Description(HeizungBefehle.ParVorlauf)] string? vorlauf = null,
        [Description(HeizungBefehle.ParRuecklauf)] string? ruecklauf = null,
        [Description("Neues Kennzeichen")] string? kennzeichen = null,
        [Description("Neue Bezeichnung")] string? name = null,
        [Description(HeizungBefehle.ParArt)] string? art = null,
        [Description(HeizungBefehle.ParVerbraucherart)] string? verbraucherart = null,
        [Description(HeizungBefehle.ParVlTemp)] double? vlTemp = null,
        [Description(HeizungBefehle.ParRlTemp)] double? rlTemp = null,
        [Description(HeizungBefehle.ParQuelle)] string? quelle = null,
        [Description(HeizungBefehle.ParStrangschema)] string? strangschema = null,
        [Description(HeizungBefehle.ParBeschreibung)] string? beschreibung = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("heizgruppe_aendern", Parameter(("heizgruppe", heizgruppe), ("verteiler", verteiler), ("abgangVl", abgangVl), ("abgangRl", abgangRl), ("pumpe", pumpe),
            ("anlage", anlage), ("vorlauf", vorlauf), ("ruecklauf", ruecklauf), ("kennzeichen", kennzeichen), ("name", name), ("art", art),
            ("verbraucherart", verbraucherart), ("vlTemp", vlTemp), ("rlTemp", rlTemp), ("quelle", quelle), ("strangschema", strangschema),
            ("beschreibung", beschreibung)), trockenlauf);

    [McpServerTool(Name = "heizgruppen_vorschlagen"), Description(HeizgruppenVorschlagen.Text)]
    public string HeizgruppenVorschlagenWerkzeug(
        [Description(HeizungBefehle.ParAnlageVorschlag)] string? anlage = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("heizgruppen_vorschlagen", Parameter(("anlage", anlage)), trockenlauf);

    [McpServerTool(Name = "heizlast_uebernehmen"), Description(HeizlastUebernehmen.Text)]   // Modul 5 Teil B-H
    public string HeizlastUebernehmenWerkzeug(
        [Description(Heizlast.ParRaum)] string? raum = null,
        [Description(Heizlast.ParGebaeude)] string? gebaeude = null,
        [Description("Auch eine Heizleistung ersetzen, die nicht aus der Berechnung stammt (von Hand, HKAP, IFC)")] bool ueberschreiben = false,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("heizlast_uebernehmen", Parameter(("raum", raum), ("gebaeude", gebaeude), ("ueberschreiben", ueberschreiben ? true : null)), trockenlauf);
}
