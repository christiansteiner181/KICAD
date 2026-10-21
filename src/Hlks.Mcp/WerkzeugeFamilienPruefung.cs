// MCP-Werkzeuge der Familienprüfung (Modul 2, B4): familie_vergleichen (Kontrollbild als Bildinhalt wie unterlage_bild),
// familie_masse (Abweichungsliste je Typ) und die Befehle konflikt_entscheiden, wert_bestaetigen, familie_freigeben, familie_speichern (im Kern nur für
// einen Menschen; der KI-Lauf hat sie nicht in seiner Werkzeugliste). Dünne Hülle über dem Kern wie in Werkzeuge.cs; Name,
// Beschreibung und Parameter stehen genau so im Kern (FamilienAbfragen.Definitionen, FamilienFreigabe.cs).

using System.ComponentModel;
using System.Text.Json;
using Hlks.Kern;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Hlks.Mcp;

public sealed partial class Werkzeuge
{
    // ---------- Abfragen ----------

    [McpServerTool(Name = "familie_masse"), Description(FamilienMasse.AbfrageText)]
    public string FamilieMasse(
        [Description("Familie (Kennzeichen, Name oder id)")] string familie,
        [Description("Nur dieser Typ; leer = alle Typen")] string? typ = null) =>
        Lauf(() => Antwort(FamilienMasse.Abfrage(projekt, familie, typ)));

    [McpServerTool(Name = FamiliePruefen.Name), Description(FamiliePruefen.Text)]   // Nachtrag 21.10.2026
    public string FamiliePruefenWerkzeug([Description("Familie (Kennzeichen, Name oder id)")] string familie) =>
        Lauf(() => Antwort(FamiliePruefen.Abfrage(projekt, familie)));

    [McpServerTool(Name = "familie_vergleichen"), Description(FamilienKontrollbild.AbfrageText)]
    public CallToolResult FamilieVergleichen(
        [Description("Familie (Kennzeichen, Name oder id)")] string familie,
        [Description("Typ aus der Typenliste der Familie")] string typ,
        [Description("Unterlage (Kennzeichen, Id oder Dateiname)")] string unterlage,
        [Description("vorne | hinten | links | rechts | oben | unten")] string ansicht,
        [Description("PDF: Seite (ab 1, Vorgabe 1)")] double? seite = null,
        [Description("Zwei Passpunkte [{ \"familie\": [u, v], \"unterlage\": [x, y] }, …]; ohne: über einen Massbezug mit Mass dieser Unterlage")] JsonElement? passpunkte = null,
        [Description("grob | mittel | fein (Vorgabe mittel)")] string? detailgrad = null,
        [Description("Ausschnitt [x0, y0, x1, y1] der Unterlage (Unterlagen-Koordinaten); Vorgabe: ganze Seite bzw. Zeichnung")] JsonElement? ausschnitt = null,
        [Description("Breite des Bildes in Bildpunkten (Vorgabe 1200, 100–3000)")] double? breite = null)
    {
        lock (projekt)
        {
            try
            {
                projekt.Synchronisieren();
                var par = Parameter(("familie", familie), ("typ", typ), ("unterlage", unterlage), ("ansicht", ansicht), ("seite", seite), ("passpunkte", passpunkte),
                    ("detailgrad", detailgrad), ("ausschnitt", ausschnitt), ("breite", breite));
                var info = FamilienKontrollbild.Erzeugen(projekt, par, out var png);
                return new CallToolResult
                {
                    Content = [new TextContentBlock { Text = Json.Text(info) }, ImageContentBlock.FromBytes(png, "image/png")],
                };
            }
            catch (Exception ex)
            {
                return new CallToolResult { IsError = true, Content = [new TextContentBlock { Text = FehlerText(ex) }] };
            }
        }
    }

    // ---------- Befehle (nur Mensch) ----------

    [McpServerTool(Name = "konflikt_entscheiden"), Description(KonfliktEntscheiden.Text)]
    public string KonfliktEntscheidenWerkzeug(
        [Description("Familie (Kennzeichen, Name oder id)")] string familie,
        [Description("Id des Konflikts (k-…) aus definition.konflikte")] string konflikt,
        [Description("Gewählter Wert (Zahl in der Einheit des Parameters bzw. Text), meist einer der Werte des Konflikts")] string wert,
        [Description("Begründung des Entscheids (z. B. «Massbild DXF ist massgebend, Rücksprache Hersteller»)")] string? begruendung = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("konflikt_entscheiden", Parameter(("familie", familie), ("konflikt", konflikt), ("wert", wert), ("begruendung", begruendung)), trockenlauf);

    [McpServerTool(Name = "wert_bestaetigen"), Description(WertBestaetigen.Text)]
    public string WertBestaetigenWerkzeug(
        [Description("Familie (Kennzeichen, Name oder id)")] string familie,
        [Description("Parameter, dessen Wert bestätigt wird")] string parameter,
        [Description("Typ, dessen Wert bestätigt wird; leer = Vorgabewert des Parameters")] string? typ = null,
        [Description("Begründung (z. B. «aus dem Massbild gelesen und am Muster nachgemessen»)")] string? begruendung = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("wert_bestaetigen", Parameter(("familie", familie), ("parameter", parameter), ("typ", typ), ("begruendung", begruendung)), trockenlauf);

    [McpServerTool(Name = "familie_freigeben"), Description(FamilieFreigeben.Text)]
    public string FamilieFreigebenWerkzeug(
        [Description("Familie (Kennzeichen, Name oder id)")] string familie,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("familie_freigeben", Parameter(("familie", familie)), trockenlauf);

    [McpServerTool(Name = "familie_speichern"), Description(FamilieSpeichern.Text)]
    public string FamilieSpeichernWerkzeug(
        [Description("Familie (Kennzeichen, Name oder id)")] string familie,
        [Description("Nur für die Ausfuhr als Datei: voller Pfad (ein relativer Name wird abgelehnt – kataloge/familien ist nur lesbar); leer = in die Familienbibliothek (wie familie_in_bibliothek)")] string? datei = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("familie_speichern", Parameter(("familie", familie), ("datei", datei)), trockenlauf);
}
