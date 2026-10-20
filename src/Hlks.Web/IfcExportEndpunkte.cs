// IFC-Export (Modul 7 Teil B2) über REST – dünne Hülle über dem Kern (IfcExport.Schreiben); in Program.cs hängt nur
// «app.MapIfcExport(dienst);» sie ein. Der Herkunftsschutz gilt: POST nur mit application/json (sonst 415), nur von localhost.
//
//   POST /api/ifc/export          Körper wie die Abfrage ifc_export { datei?, fassung?, umfang?, raeume?, ids? } → { datei, bericht }
//                                 (ohne datei: <Projektordner>\ausgabe\ifc\<Projekt>_<Fassung>_<JJJJMMTT-hhmm>.ifc, Bericht daneben)
//   GET  /api/ifc/export/datei?pfad=…   die Datei herunterladen – nur .ifc, .bericht.json, .bericht.md aus ausgabe\ifc des offenen
//                                 Projekts (sonst 403; fehlt sie 404; ohne pfad 400). Ein blosser Dateiname gilt in diesem Ordner.

using System.Text.Json.Nodes;
using Hlks.Kern;
using static Hlks.Web.EndpunktHilfe;

namespace Hlks.Web;

public static class IfcExportEndpunkte
{
    static readonly Dictionary<string, string> Typen = new(StringComparer.OrdinalIgnoreCase)
    {
        [".ifc"] = "application/x-step", [".json"] = "application/json; charset=utf-8", [".md"] = "text/markdown; charset=utf-8",
    };

    public static WebApplication MapIfcExport(this WebApplication app, ProjektDienst dienst)
    {
        var g = app.MapGroup("/api/ifc/export");

        g.MapPost("", (HttpRequest r) => Gesichert(async () =>
        {
            var par = await KoerperLesen(r);
            var bericht = await dienst.Mit(p => IfcExport.Schreiben(p, par));
            return Antwort(new JsonObject { ["datei"] = bericht["datei"]?.DeepClone(), ["bericht"] = bericht });
        }));

        g.MapGet("/datei", (string? pfad) => Gesichert(async () =>
        {
            if (string.IsNullOrWhiteSpace(pfad)) return Antwort(new { fehler = "Parameter «pfad» fehlt.", vorschlag = "pfad = Datei aus der Antwort von POST /api/ifc/export" }, 400);
            var (datei, fehler, code) = await dienst.Mit(p => Erlaubt(p, pfad));
            if (datei is null) return Antwort(new { fehler, vorschlag = "Nur Dateien aus dem Ordner ausgabe\\ifc des offenen Projekts (IFC und Bericht)." }, code);
            return Results.File(datei, Typen[Path.GetExtension(datei)], Path.GetFileName(datei));
        }));

        return app;
    }

    /// <summary>Vollständiger Pfad, wenn die Datei in ausgabe\ifc des Projekts liegt, eine erlaubte Endung hat und existiert; sonst Fehler und
    /// Statuscode (403 ausserhalb bzw. andere Endung, 404 fehlt).</summary>
    public static (string? Datei, string? Fehler, int Code) Erlaubt(Projekt p, string pfad)
    {
        var ordner = Path.GetFullPath(IfcExport.Ausgabeordner(p)).TrimEnd('\\', '/');
        string voll;
        try { voll = Path.GetFullPath(Path.IsPathRooted(pfad) ? pfad : Path.Combine(ordner, pfad)); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return (null, $"Der Pfad «{pfad}» ist ungültig.", 400); }
        if (!voll.StartsWith(ordner + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || Path.GetDirectoryName(voll)?.TrimEnd('\\', '/').Equals(ordner, StringComparison.OrdinalIgnoreCase) != true)
            return (null, $"«{pfad}» liegt nicht im Ordner ausgabe\\ifc des offenen Projekts.", 403);
        var name = Path.GetFileName(voll);
        bool endung = name.EndsWith(".ifc", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".bericht.json", StringComparison.OrdinalIgnoreCase)
                      || name.EndsWith(".bericht.md", StringComparison.OrdinalIgnoreCase);
        if (!endung) return (null, $"«{name}» ist keine IFC-Ausgabe (erlaubt: .ifc, .bericht.json, .bericht.md).", 403);
        if (!File.Exists(voll)) return (null, $"Die Datei «{name}» gibt es in ausgabe\\ifc nicht.", 404);
        return (voll, null, 200);
    }
}
