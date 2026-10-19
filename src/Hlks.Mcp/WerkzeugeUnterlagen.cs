// MCP-Werkzeuge der Herstellerunterlagen (Modul 2, Auftrag B2): unterlagen_lesen, befunde, unterlage_bild. Dünne Hülle über dem
// Kern wie in Werkzeuge.cs; Name, Beschreibung und Parameter stehen genau so im Kern (UnterlagenBefehle.cs, UnterlagenAbfragen).
// unterlage_bild liefert den Bildinhalt: Text mit den Angaben (Bereich, Grösse, Hinweise) und das PNG als ImageContentBlock
// (ModelContextProtocol 2.2.0: ImageContentBlock.FromBytes in einem CallToolResult).

using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hlks.Kern;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Hlks.Mcp;

public sealed partial class Werkzeuge
{
    [McpServerTool(Name = "unterlagen_lesen"), Description(UnterlagenAbfragen.LesenText)]
    public string UnterlagenLesen(
        [Description("Unterlagen: Liste aus Kennzeichen (UL-n), Id oder Dateiname im Ordner unterlagen (auch ein Dateipfad: die Datei wird dann abgelegt)")] JsonElement unterlagen) =>
        TuerAbfrage("unterlagen_lesen", Parameter(("unterlagen", unterlagen)));

    [McpServerTool(Name = "befunde"), Description(UnterlagenAbfragen.BefundeText)]
    public string Befunde(
        [Description("Unterlage (Kennzeichen, Id oder Dateiname)")] string unterlage,
        [Description("Nur Befunde dieser Dokumentseite (PDF, ab 1)")] double? seite = null,
        [Description("Befundart, mehrere mit Komma: wort, zeile, tabelle, zelle, linie, polylinie, bogen, kreis, mass, block, text, bild, form3d, port, pset, typ")] string? art = null,
        [Description("Nur Befunde, deren Wert diesen Text enthält (Gross-/Kleinschreibung egal)")] string? text = null,
        [Description("Nur Befunde, deren Box dieses Rechteck [x0, y0, x1, y1] berührt (Unterlagen-Koordinaten)")] JsonElement? box = null,
        [Description("DWG/DXF: Layout (Model oder Name des Layouts); XLSX: Blatt")] string? layout = null,
        [Description("DWG/DXF: Layer")] string? layer = null,
        [Description("Befund-Id oder Nummer, mehrere mit Komma (z. B. «a1b2c3d4e5f6#17» oder «17, 18»)")] string? id = null,
        [Description("3D-Befunde mit Netz (Ecken und Dreiecke); Vorgabe false: nur Hülle und Anzahl")] bool voll = false,
        [Description("Befunde je Antwort (Vorgabe 50, höchstens 500)")] double? proSeite = null,
        [Description("Seite der Trefferliste (ab 1, Vorgabe 1)")] double? trefferSeite = null)
    {
        var par = Parameter(("unterlage", unterlage), ("seite", seite), ("art", art), ("text", text), ("box", box), ("layout", layout), ("layer", layer), ("id", id),
            ("proSeite", proSeite), ("trefferSeite", trefferSeite));
        if (voll) par["voll"] = true;
        return TuerAbfrage("befunde", par);
    }

    [McpServerTool(Name = "unterlage_bild"), Description(UnterlagenAbfragen.BildText)]
    public CallToolResult UnterlageBild(
        [Description("Unterlage (Kennzeichen, Id oder Dateiname)")] string unterlage,
        [Description("PDF: Seite (ab 1, Vorgabe 1)")] double? seite = null,
        [Description("Ausschnitt [x0, y0, x1, y1] in Unterlagen-Koordinaten (PDF: pt, y nach oben; Bild: Bildpunkte; DWG/DXF: Zeichnungseinheiten); Vorgabe: ganze Seite bzw. Zeichnung")] JsonElement? ausschnitt = null,
        [Description("Breite des Bildes in Bildpunkten (Vorgabe 1000, 100–3000)")] double? breite = null)
    {
        lock (projekt)
        {
            try
            {
                projekt.Synchronisieren();
                var par = Parameter(("unterlage", unterlage), ("seite", seite), ("ausschnitt", ausschnitt), ("breite", breite));
                var info = UnterlagenAbfrage.BildErzeugen(projekt, par, out var bild, out var mime);
                return new CallToolResult
                {
                    Content = [new TextContentBlock { Text = Json.Text(info) }, ImageContentBlock.FromBytes(bild, mime)],
                };
            }
            catch (Exception ex)
            {
                return new CallToolResult { IsError = true, Content = [new TextContentBlock { Text = FehlerText(ex) }] };
            }
        }
    }

    [McpServerTool(Name = "unterlage_aus_url"), Description(Hlks.Kern.UnterlageAusUrl.BeschreibungText)]
    public string UnterlageAusUrl(
        [Description("Adresse der Datei oder Webseite (http oder https)")] string url,
        [Description("Erwartete Dateiart: pdf | dwg | dxf | step | iges | stl | obj | ifc | xlsx | csv | typkatalog | bild | html | zip; stimmt der Inhalt nicht damit überein, wird abgelehnt")] string? art = null,
        [Description("Familie (Kennzeichen oder Id), zu der die Unterlage(n) gehören (bezuege.familie)")] string? familie = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("unterlage_aus_url", Parameter(("url", url), ("art", art), ("familie", familie)), trockenlauf);

    /// <summary>Abfrage über die Tür des Kerns (Tuer.Abfrage) in der gemeinsamen Hülle: Text oder JSON.</summary>
    string TuerAbfrage(string name, JsonObject parameter) =>
        Lauf(() => Tuer.Abfrage(projekt, name, parameter) switch { string t => t, JsonNode n => Antwort(n), var o => Antwort(Json.Knoten(o)) });
}
