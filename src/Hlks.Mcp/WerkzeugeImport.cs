// MCP-Werkzeuge des IFC-Übertrags (Modul 1b): IFC prüfen und übernehmen. Dünne Hülle über dem Kern wie in
// Werkzeuge.cs; Name, Beschreibung und Parameter stehen genau so im Kern (IfcUebernahme.cs, Abfragen.Liste).

using System.ComponentModel;
using System.Text.Json;
using Hlks.Kern;
using ModelContextProtocol.Server;

namespace Hlks.Mcp;

public sealed partial class Werkzeuge
{
    [McpServerTool(Name = "ifc_pruefen"), Description("Liest ein IFC (IFC4 oder IFC2x3) ohne etwas zu ändern: Fassung, Programm, Einheit, Geschosse, Räume, welche Katalogklassen übernommen würden, was ohne Klasse bliebe, Koordinatenbezug. Vor ifc_uebernehmen aufrufen.")]
    public string IfcPruefen(
        [Description("Pfad der IFC-Datei oder Name einer Unterlage im Ordner unterlagen")] string datei) =>
        Lauf(() => Antwort(IfcImport.Pruefen(projekt, datei)));

    [McpServerTool(Name = "ifc_uebernehmen"), Description("Übernimmt ein IFC (IFC4 oder IFC2x3) als eigene, bearbeitbare Elemente: Geschosse, Räume, Wände, Decken, Türen, Fenster, Leitungen, Formteile, Armaturen, Apparate und Systeme mit Anschlüssen und Verbindungen. Jedes Element behält die GlobalId; dieselbe Datei erneut übernehmen gleicht ab (neu, geändert, gelöscht). Was sich nicht als Element fassen lässt, bleibt als Hintergrund (Unterlage). Ein Paket je Übernahme – zuerst als Trockenlauf ansehen.")]
    public string IfcUebernehmen(
        [Description("Pfad der IFC-Datei (lokal oder W:) oder Name einer Unterlage im Ordner unterlagen")] string datei,
        [Description("architektur | raeume | hlks (Vorgabe alle)")] JsonElement? bereiche = null,
        [Description("Nur diese Geschosse (Kennzeichen oder Name des Geschosses im Projekt bzw. im IFC, oder Kote in m wie \"3.06\"); Vorgabe alle")] JsonElement? geschosse = null,
        [Description("Nicht übernommene Objekte als Hintergrund zeigen (Vorgabe true)")] bool grundlage = true,
        [Description("IFC-Weltpunkt { x, y, z } in mm und drehung in Grad, der im Projekt Ursprung und ±0.00 ist; Vorgabe: Bezug des Projekts, beim ersten IFC aus dem Gebäude der Datei")] JsonElement? bezug = null,
        [Description("Nur nötig, wenn die Datei nicht zum Projektbezug passt (ifc_pruefen zeigt die Abweichung): projekt = Weltkoordinaten der Datei wie sie sind | gebaeude = Gebäudeursprung der Datei auf den Projektursprung | koordinationskoerper = Koordinationskörper aufeinander")] string? ausrichtung = null,
        [Description("Name des Koordinationskörpers, falls er nicht «Koordinationskörper» heisst")] string? koordinationskoerper = null,
        [Description("kicad (Vorgabe): seit dem letzten Übertrag in KICAD geänderte Elemente behalten | ifc: neue IFC-Fassung übernehmen")] string? konflikte = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false)
    {
        var par = Parameter(("datei", datei), ("bereiche", bereiche), ("geschosse", geschosse), ("bezug", bezug), ("ausrichtung", ausrichtung),
            ("koordinationskoerper", koordinationskoerper), ("konflikte", konflikte));
        par["grundlage"] = grundlage;
        return Befehl("ifc_uebernehmen", par, trockenlauf);
    }
}
