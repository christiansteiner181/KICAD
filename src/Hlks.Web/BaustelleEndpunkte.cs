// BIM to field Stufe 1 (entwurf/bim-to-field.md): die erzeugte Baustellen-Webseite im Browser öffnen – dünne Hülle, der Export selbst ist die
// Abfrage baustelle_export (POST /api/abfrage/baustelle_export). In Program.cs hängt nur «app.MapBaustelle(dienst);» sie ein.
//
//   GET /api/baustelle/datei?pfad=…   nur .html aus <Projekt>\ausgabe\baustelle des offenen Projekts (sonst 403; fehlt sie 404; ohne pfad 400).

using Hlks.Kern;
using static Hlks.Web.EndpunktHilfe;

namespace Hlks.Web;

public static class BaustelleEndpunkte
{
    public static WebApplication MapBaustelle(this WebApplication app, ProjektDienst dienst)
    {
        app.MapGet("/api/baustelle/datei", (string? pfad) => Gesichert(async () =>
        {
            if (string.IsNullOrWhiteSpace(pfad)) return Antwort(new { fehler = "Parameter «pfad» fehlt.", vorschlag = "pfad = datei aus der Antwort von baustelle_export" }, 400);
            var (datei, fehler, code) = await dienst.Mit(p => Erlaubt(p, pfad));
            if (datei is null) return Antwort(new { fehler, vorschlag = "Nur Webseiten aus dem Ordner ausgabe\\baustelle des offenen Projekts." }, code);
            return Results.File(datei, "text/html; charset=utf-8");
        }));
        return app;
    }

    /// <summary>Vollständiger Pfad, wenn die Datei direkt in ausgabe\baustelle des Projekts liegt, .html heisst und existiert; sonst Fehler und Code.</summary>
    public static (string? Datei, string? Fehler, int Code) Erlaubt(Projekt p, string pfad)
    {
        if (p.Speicher is null) return (null, "Projekt ohne Ablage.", 404);
        var ordner = Path.GetFullPath(Path.Combine(p.Speicher.Ort, "ausgabe", "baustelle")).TrimEnd('\\', '/');
        string voll;
        try { voll = Path.GetFullPath(Path.IsPathRooted(pfad) ? pfad : Path.Combine(ordner, pfad)); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return (null, $"Der Pfad «{pfad}» ist ungültig.", 400); }
        if (!string.Equals(Path.GetDirectoryName(voll)?.TrimEnd('\\', '/'), ordner, StringComparison.OrdinalIgnoreCase))
            return (null, $"«{pfad}» liegt nicht im Ordner ausgabe\\baustelle des offenen Projekts.", 403);
        if (!voll.EndsWith(".html", StringComparison.OrdinalIgnoreCase)) return (null, "Nur .html.", 403);
        if (!File.Exists(voll)) return (null, $"Die Datei «{Path.GetFileName(voll)}» gibt es nicht.", 404);
        return (voll, null, 200);
    }
}
