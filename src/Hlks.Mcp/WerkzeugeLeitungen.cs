// MCP-Werkzeuge der Leitungen (Modul 3): Abfragen leitungsserie (B1; seit 21.10.2026 ohne Sperre gerechnet und abbrechbar), abzweigwinkel
// (Nachtrag 21.10.2026), leitungszug (B2) und verlegung (B8c), Befehle des Routings (B2:
// leitung_zeichnen, leitung_abzweigen, leitung_aufteilen, leitung_groesse, leitung_serie). Dünne Hülle über dem Kern wie in
// Werkzeuge.cs; Name, Beschreibung und Parameter stehen genau so im Kern (LeitungenAbfragen.Definitionen, Leitungsserie.AbfrageText,
// Leitungszug.AbfrageText, RoutingBefehle.Par*, LeitungZeichnen.Text usw.).

using System.ComponentModel;
using System.Text.Json;
using Hlks.Kern;
using ModelContextProtocol.Server;

namespace Hlks.Mcp;

public sealed partial class Werkzeuge
{
    // ---------- Abfragen ----------

    // Nachtrag 21.10.2026: leitungsserie rechnet ohne Sperre auf einem Lesestand und bricht ab, wenn der Client den Aufruf abbricht
    [McpServerTool(Name = "leitungsserie"), Description(Leitungsserie.AbfrageText)]
    public string LeitungsserieLesen([Description("Leitungsserie: Familie der Kategorie Rohr bzw. Kanal (Kennzeichen, Name oder id)")] string familie,
        CancellationToken abbruch = default) =>
        LaufAbgesetzt(() => { var r = Leitungsserie.AbfrageVorbereiten(projekt, familie); return a => r(a); }, abbruch);

    [McpServerTool(Name = "abzweigwinkel"), Description(Leitungsserie.AbzweigwinkelAbfrageText)]
    public string AbzweigwinkelLesen(
        [Description(Leitungsserie.ParAbzweigLeitung)] string? leitung = null,
        [Description(Leitungsserie.ParAbzweigFamilie)] string? familie = null,
        [Description(Leitungsserie.ParAbzweigGroesse)] JsonElement? groesse = null) =>
        Lauf(() => Antwort(Leitungsserie.AbzweigwinkelAbfrage(projekt, leitung, familie, Knoten(groesse))));

    [McpServerTool(Name = "leitungszug"), Description(Leitungszug.AbfrageText)]
    public string LeitungszugLesen([Description(Leitungszug.ParLeitung)] string leitung) =>
        Lauf(() => Antwort(Leitungszug.Abfrage(projekt, leitung)));

    [McpServerTool(Name = "verlegung"), Description(Verlegung.AbfrageText)]
    public string VerlegungLesen(
        [Description(Verlegung.ParGeschoss)] string? geschoss = null,
        [Description(Verlegung.ParArt)] string? art = null) =>
        Lauf(() => Antwort(Verlegung.Abfrage(projekt, geschoss, art)));

    // ---------- Befehle (B2: Routing) ----------

    [McpServerTool(Name = "leitung_zeichnen"), Description(LeitungZeichnen.Text)]
    public string LeitungZeichnenWerkzeug(
        [Description(RoutingBefehle.ParSerie)] string serie,
        [Description(RoutingBefehle.ParGroesse)] JsonElement groesse,
        [Description(RoutingBefehle.ParPunkte)] JsonElement punkte,
        [Description(RoutingBefehle.ParVon)] JsonElement? von = null,
        [Description(RoutingBefehle.ParNach)] JsonElement? nach = null,
        [Description(RoutingBefehle.ParGefaelle)] double? gefaelle = null,
        [Description(RoutingBefehle.ParSystem)] string? system = null,
        [Description(RoutingBefehle.ParStrang)] string? strang = null,
        [Description(RoutingBefehle.ParGeschoss)] string? geschoss = null,
        [Description(RoutingBefehle.ParPraefix)] string? praefix = null,
        [Description(RoutingBefehle.ParEigenschaften)] JsonElement? eigenschaften = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("leitung_zeichnen", Parameter(("serie", serie), ("groesse", groesse), ("punkte", punkte), ("von", von), ("nach", nach), ("gefaelle", gefaelle),
            ("system", system), ("strang", strang), ("geschoss", geschoss), ("praefix", praefix), ("eigenschaften", eigenschaften)), trockenlauf);

    [McpServerTool(Name = "leitung_abzweigen"), Description(LeitungAbzweigen.Text)]
    public string LeitungAbzweigenWerkzeug(
        [Description(LeitungAbzweigen.ParHaupt)] string leitung,
        [Description(LeitungAbzweigen.ParPunkt)] JsonElement punkt,
        [Description(LeitungAbzweigen.ParWinkel)] double? winkel = null,
        [Description(LeitungAbzweigen.ParSeite)] JsonElement? seite = null,
        [Description(LeitungAbzweigen.ParRichtung)] JsonElement? richtung = null,
        [Description(LeitungAbzweigen.ParLaenge)] double? laenge = null,
        [Description(LeitungAbzweigen.ParAbzweigPunkte)] JsonElement? punkte = null,
        [Description(LeitungAbzweigen.ParAbzweigGroesse)] JsonElement? groesse = null,
        [Description(LeitungAbzweigen.ParAbzweigSerie)] string? serie = null,
        [Description(LeitungAbzweigen.ParEnde)] JsonElement? ende = null,
        [Description(RoutingBefehle.ParGefaelle)] double? gefaelle = null,
        [Description(LeitungAbzweigen.ParSystemAbzweig)] string? system = null,
        [Description(LeitungAbzweigen.ParStrangAbzweig)] string? strang = null,
        [Description(RoutingBefehle.ParGeschoss)] string? geschoss = null,
        [Description(RoutingBefehle.ParPraefix)] string? praefix = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("leitung_abzweigen", Parameter(("leitung", leitung), ("punkt", punkt), ("winkel", winkel), ("seite", seite), ("richtung", richtung), ("laenge", laenge), ("punkte", punkte),
            ("groesse", groesse), ("serie", serie), ("ende", ende), ("gefaelle", gefaelle), ("system", system), ("strang", strang), ("geschoss", geschoss),
            ("praefix", praefix)), trockenlauf);

    [McpServerTool(Name = "leitung_aufteilen"), Description(LeitungAufteilen.Text)]
    public string LeitungAufteilenWerkzeug(
        [Description(RoutingBefehle.ParLeitung)] string? leitung = null,
        [Description(RoutingBefehle.ParLeitungen)] JsonElement? leitungen = null,
        [Description(LeitungAufteilen.ParSerie)] string? serie = null,
        [Description(LeitungAufteilen.ParGroesse)] JsonElement? groesse = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("leitung_aufteilen", Parameter(("leitung", leitung), ("leitungen", leitungen), ("serie", serie), ("groesse", groesse)), trockenlauf);

    [McpServerTool(Name = "leitung_groesse"), Description(LeitungGroesseAendern.Text)]
    public string LeitungGroesseWerkzeug(
        [Description(LeitungGroesseAendern.ParGroesse)] JsonElement groesse,
        [Description(RoutingBefehle.ParLeitung)] string? leitung = null,
        [Description(RoutingBefehle.ParLeitungen)] JsonElement? leitungen = null,
        [Description(RoutingBefehle.ParZug)] bool zug = false,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false)
    {
        var par = Parameter(("leitung", leitung), ("leitungen", leitungen), ("groesse", groesse));
        if (zug) par["zug"] = true;
        return Befehl("leitung_groesse", par, trockenlauf);
    }

    [McpServerTool(Name = "leitung_serie"), Description(LeitungSerieAendern.Text)]
    public string LeitungSerieWerkzeug(
        [Description(LeitungSerieAendern.ParSerie)] string serie,
        [Description(RoutingBefehle.ParLeitung)] string? leitung = null,
        [Description(RoutingBefehle.ParLeitungen)] JsonElement? leitungen = null,
        [Description(RoutingBefehle.ParZug)] bool zug = false,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false)
    {
        var par = Parameter(("leitung", leitung), ("leitungen", leitungen), ("serie", serie));
        if (zug) par["zug"] = true;
        return Befehl("leitung_serie", par, trockenlauf);
    }
}
