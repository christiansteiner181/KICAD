// Bedienungsanleitung in KICAD (Entscheid Christian Steiner 19.10.2026): Auslieferung unter /hilfe/ und Prüfstand (Häkchen, Notizen).
// Dünne Hülle über AnleitungDienst; in Program.cs hängt nur «app.MapAnleitung(dienst);» sie ein. Fehler: 400 mit { fehler, vorschlag },
// Ablage/Netz weg: 503 (EndpunktHilfe).
//
//   GET  /hilfe            302 → /hilfe/
//   GET  /hilfe/           index.html der Anleitung (404 mit kleiner Seite «noch nicht erstellt», wenn es sie nicht gibt)
//   GET  /hilfe/<datei>    Datei im Anleitungsordner (bilder/….webp usw.); nur bekannte Typen, nur innerhalb des Ordners
//                          (verbotener Pfad → 400, fehlende Datei → 404); Cache-Control: no-cache; nur GET
//   GET  /api/hilfe/pruefung        { quelle, ort, rechner, hinweis?, eintraege: { <funktionId>: { titel, punkte, notiz, stand, geaendert, texte, rechner } } }
//   PUT  /api/hilfe/pruefung/{id}   Körper { titel?, punkte, notiz, stand?, texte? } → { gespeichert, id, geaendert, rechner, quelle }
//                                   Grenzen: id ≤ 120 Zeichen aus [A-Za-z0-9_.~:@+-], ≤ 64 Punkte, Notiz ≤ 4000 Zeichen, Körper ≤ 64 KB; sonst 400

using System.Text;
using System.Text.Json.Nodes;
using Hlks.Kern;
using static Hlks.Web.EndpunktHilfe;

namespace Hlks.Web;

public static class AnleitungEndpunkte
{
    public static WebApplication MapAnleitung(this WebApplication app, ProjektDienst dienst)
    {
        var a = dienst.Anleitung;

        // Eine Route für «/hilfe» und «/hilfe/…»: die Vorlage ignoriert den Schrägstrich am Ende, eine eigene Route für «/hilfe» würde auch «/hilfe/»
        // treffen und endlos auf sich selbst weiterleiten.
        app.MapGet("/hilfe/{**pfad}", (string? pfad, HttpContext ctx) =>
        {
            if (string.IsNullOrEmpty(pfad) && ctx.Request.Path.Value is { } p && !p.EndsWith('/')) return Results.Redirect("/hilfe/", permanent: false);
            try
            {
                var ort = a.Ort();
                var anfang = string.IsNullOrEmpty(pfad) || pfad.Equals("index.html", StringComparison.OrdinalIgnoreCase);
                var datei = ort.Ordner is null ? default : AnleitungDienst.Aufloesen(ort.Ordner, anfang ? "index.html" : pfad);
                if (datei.Pfad is null)
                {
                    if (anfang && !datei.Abgelehnt)
                        return Results.Text(a.NichtErstelltSeite(ort), "text/html; charset=utf-8", statusCode: 404);
                    var abgelehnt = datei.Abgelehnt;
                    return Results.Text(abgelehnt ? $"Dieser Pfad ist nicht erlaubt: {datei.Grund}" : "Diese Datei gibt es in der Bedienungsanleitung nicht.",
                        "text/plain; charset=utf-8", statusCode: abgelehnt ? 400 : 404);
                }
                var info = new FileInfo(datei.Pfad);
                var zeit = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero);
                var etag = new Microsoft.Net.Http.Headers.EntityTagHeaderValue($"\"{info.LastWriteTimeUtc.Ticks:x}-{info.Length:x}\"");
                ctx.Response.Headers.CacheControl = "no-cache";
                ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
                return Results.File(datei.Pfad, AnleitungDienst.Typen[Path.GetExtension(datei.Pfad)], lastModified: zeit, entityTag: etag);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { return Results.Text("Die Bedienungsanleitung ist nicht lesbar: " + ex.Message, "text/plain; charset=utf-8", statusCode: 503); }
        });

        var g = app.MapGroup("/api/hilfe");

        g.MapGet("/pruefung", () => Gesichert(() =>
        {
            var p = a.Pruefstand;
            return Task.FromResult(Antwort(new
            {
                quelle = p.Quelle, ort = p.Verzeichnis, rechner = p.Rechner,
                hinweis = p.Erlaubt ? null : "Testlauf: ohne Umleitung nach %TEMP% (HLKS_ANLEITUNG_PRUEFUNG) wird der Prüfstand weder gelesen noch geschrieben.",
                eintraege = p.Alle(),
            }));
        }));

        g.MapPut("/pruefung/{id}", (string id, HttpRequest r) => Gesichert(async () =>
        {
            var p = a.Pruefstand;
            var koerper = await Begrenzt(r, Pruefstand.KoerperMax);
            var e = p.Setzen(id, koerper);
            return Antwort(new { gespeichert = true, id, geaendert = (string?)e["geaendert"], rechner = p.Rechner, quelle = p.Quelle });
        }));

        return app;
    }

    /// <summary>Liest den Körper als JSON-Objekt, höchstens max Bytes (sonst 400).</summary>
    static async Task<JsonObject> Begrenzt(HttpRequest r, int max)
    {
        var zu = new HlksFehler($"Der Körper ist zu gross (höchstens {max / 1024} KB).", "Weniger senden (Notiz kürzen).");
        if (r.ContentLength > max) throw zu;
        using var ms = new MemoryStream();
        var puffer = new byte[8 * 1024];
        int n;
        while ((n = await r.Body.ReadAsync(puffer)) > 0)
        {
            ms.Write(puffer, 0, n);
            if (ms.Length > max) throw zu;
        }
        var text = Encoding.UTF8.GetString(ms.ToArray());
        if (string.IsNullOrWhiteSpace(text)) return new JsonObject();
        try { return JsonNode.Parse(text) as JsonObject ?? throw new HlksFehler("Der Körper muss ein JSON-Objekt sein.", "Ein Objekt { punkte, notiz } senden."); }
        catch (System.Text.Json.JsonException ex) { throw new HlksFehler("Der Körper ist kein gültiges JSON: " + ex.Message, "Gültiges JSON senden."); }
    }
}
