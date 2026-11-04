// BIM to field Stufe 1 (entwurf/bim-to-field.md): die erzeugte Baustellen-Webseite im Browser öffnen – dünne Hülle, der Export selbst ist die
// Abfrage baustelle_export (POST /api/abfrage/baustelle_export). In Program.cs hängt nur «app.MapBaustelle(dienst);» sie ein.
//
//   GET /api/baustelle/datei?pfad=…   nur .html aus <Projekt>\ausgabe\baustelle des offenen Projekts (sonst 403; fehlt sie 404; ohne pfad 400).

using System.Text.Json.Nodes;
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
        // Rückmeldungen, die über den Testserver eingegangen sind (BaustelleZugang.cs): Liste und Inhalt für den Dialog «Rückmeldung Baustelle»
        app.MapGet("/api/baustelle/eingang", () => Gesichert(async () => Antwort(await dienst.Mit(p =>
        {
            var d = new DirectoryInfo(BaustelleZugang.Eingang(p));
            var liste = new JsonArray();
            if (d.Exists)
                foreach (var f in d.GetFiles("*.json").OrderByDescending(f => f.LastWriteTimeUtc))
                {
                    string? person = null; int n = 0;
                    try { var j = JsonNode.Parse(File.ReadAllText(f.FullName)); person = (string?)j?["person"]; n = (j?["meldungen"] as JsonArray)?.Count ?? 0; }
                    catch (System.Text.Json.JsonException) { }
                    liste.Add(new JsonObject { ["datei"] = f.Name, ["person"] = person, ["meldungen"] = n, ["zeit"] = f.LastWriteTime.ToString("o") });
                }
            return new JsonObject { ["dateien"] = liste };
        }))));
        app.MapGet("/api/baustelle/eingang/{datei}", (string datei) => Gesichert(async () =>
        {
            var pfad = await dienst.Mit(p => Path.Combine(BaustelleZugang.Eingang(p), Path.GetFileName(datei)));
            if (!pfad.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || !File.Exists(pfad)) return Antwort(new { fehler = $"Die Rückmeldung «{datei}» gibt es nicht." }, 404);
            return Results.File(pfad, "application/json; charset=utf-8");
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
