// MCP-Werkzeuge der Unterlagen (Modul 1b, B3): DWG/DXF, PDF und Bild einfügen und kalibrieren. Dünne Hülle über dem
// Kern wie in Werkzeuge.cs; Name, Beschreibung und Parameter stehen genau so im Kern (UnterlageEinfuegen.cs,
// UnterlageKalibrieren.cs).

using System.ComponentModel;
using System.Text.Json;
using Hlks.Kern;
using ModelContextProtocol.Server;

namespace Hlks.Mcp;

public sealed partial class Werkzeuge
{
    [McpServerTool(Name = "unterlage_einfuegen"), Description(Hlks.Kern.UnterlageEinfuegen.BeschreibungText)]
    public string UnterlageEinfuegen(
        [Description("Pfad der Datei (lokal oder W:) oder Name einer Unterlage im Ordner unterlagen")] string datei,
        [Description("Geschoss (Kennzeichen oder Id), in dessen Grundriss die Unterlage erscheint; entfällt bei zweck «familie»")] string? geschoss = null,
        [Description("dwg | dxf | pdf | bild (Vorgabe aus der Dateiendung); bei zweck «familie» auch step | iges | stl | obj | xlsx | csv | typkatalog | ifc")] string? art = null,
        [Description("PDF: Seite (ab 1, Vorgabe 1)")] double? seite = null,
        [Description("PDF/Bild: Massstabsnenner der Vorlage (100 = 1:100), bestimmt die erste Lage; ohne Angabe wird die Papiergrösse (1:1) eingesetzt")] double? massstab = null,
        [Description("PDF/Bild: Auflösung in Bildpunkten je Zoll (PDF: Vorgabe 150 beim Rendern; Bild: aus der Datei, sonst 150)")] double? dpi = null,
        [Description("plan (Vorgabe: Grundlage im Grundriss eines Geschosses) | familie (Herstellerunterlage für eine Familie, ohne Geschoss und Lage)")] string? zweck = null,
        [Description("Nur bei zweck «familie»: { familie: Familie (Kennzeichen oder Id) } – die Familie, zu der die Unterlage gehört (kann später gesetzt werden)")] JsonElement? bezuege = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("unterlage_einfuegen", Parameter(("datei", datei), ("geschoss", geschoss), ("art", art), ("seite", seite), ("massstab", massstab), ("dpi", dpi), ("zweck", zweck), ("bezuege", bezuege)), trockenlauf);

    [McpServerTool(Name = "unterlage_kalibrieren"), Description(Hlks.Kern.UnterlageKalibrieren.BeschreibungText)]
    public string UnterlageKalibrieren(
        [Description("Unterlage (Kennzeichen oder Id)")] string unterlage,
        [Description("Zwei Punkte der Unterlage [[u1, v1], [u2, v2]] in Unterlagen-Koordinaten: DWG/DXF in Zeichnungseinheiten; PDF/Bild in Bildpunkten (x nach rechts, y von oben nach unten)")] JsonElement von,
        [Description("Die zugehörigen Sollpunkte im Modell [[x1, y1], [x2, y2]] in mm")] JsonElement nach,
        [Description("true = nur verschieben (Faktor und Drehung bleiben); dann genügt ein Punktpaar")] bool nurVerschieben = false,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false)
    {
        var par = Parameter(("unterlage", unterlage), ("von", von), ("nach", nach));
        par["nurVerschieben"] = nurVerschieben;
        return Befehl("unterlage_kalibrieren", par, trockenlauf);
    }
}
