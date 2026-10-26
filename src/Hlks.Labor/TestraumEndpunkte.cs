// XR-Labor L1 (entwurf/labor-app.md, «Zuschnitt L1»: «Der Labor-Server liefert die Etagen des Testraums über dieselben Endpunkte wie KICAD»): die Etagen des
// Testraums für die Labor-Brille. Derselbe Vertrag wie src/Hlks.Web/XrModellEndpunkte.cs:
//
//   GET /api/xr/geschosse                 { fassung, projekt, bezugspunktMm, achsen, geschosse: [{ id, kennzeichen, name, koteMm, hoeheMm,
//                                           modell: { schluessel, elemente, url }, unterlage: null }], hinweise }
//   GET /api/xr/geschoss/{id}.glb?teil=modell|unterlage
//                                         GLB (model/gltf-binary) mit ETag = "<schluessel>", Cache-Control no-cache; If-None-Match mit diesem ETag → 304.
//                                         teil fehlt = modell. Fehler wie KICAD: 400 (Teil unbekannt), 404 (Geschoss unbekannt; Teil «unterlage»: der Testraum hat
//                                         keine IFC-Unterlage). Fehlt der Katalog des Testraums oder ist er ungültig: 409 { fehler, vorschlag } (KICAD: 409 ohne Projekt).
//
// Beide Endpunkte tragen die Marke .XrImNetz() – die Labor-Brille holt sie über den Netzzugang des Labors (5340, HTTPS mit Schlüssel); dort komprimiert der Zugang
// die GLB wie bei KICAD. Auf localhost gilt der Herkunftsschutz des Labors (nur GET).
// Der Katalog (kataloge/labor_testraum.json im Ordner der Kataloge, Einstellungen.KatalogOrdner) wird beim ersten Abruf gelesen und neu gebaut, sobald sich die Datei
// ändert (Zeit und Grösse wie XrLaborEndpunkte.KatalogHolen; kein Dateiwächter, Befund R 19.10.2026).
// Eingebunden mit genau einer Zeile in LaborDienste.MapLabor: LaborTestraum.Abbilden(app).
// Was es im Labor NICHT gibt (kein Projekt): /api/xr/marker, /api/xr/ebenen, /api/xr/ebene/{name} → 404; die App übergeht sie (src/Hlks.Labor.Quest/LIESMICH.md).

using Hlks.Kern;
using Hlks.Web;
using Microsoft.Net.Http.Headers;
using static Hlks.Web.EndpunktHilfe;

namespace Hlks.Labor;

/// <summary>Der gebaute Testraum, neu gebaut, wenn sich der Katalog ändert.</summary>
public sealed class TestraumQuelle(string katalogOrdner)
{
    readonly object sperre = new();
    (string Stand, TestraumStand Testraum)? gemerkt;

    public string KatalogOrdner { get; } = katalogOrdner;
    /// <summary>Wie oft gebaut wurde (Tests).</summary>
    public int Gebaut { get; private set; }

    /// <summary>Der Testraum zum aktuellen Katalog. Wirft HlksFehler, wenn der Katalog fehlt oder ungültig ist.</summary>
    public TestraumStand Holen()
    {
        var fi = new FileInfo(Path.Combine(KatalogOrdner, TestraumKatalog.Datei));
        var stand = fi.Exists ? $"{fi.LastWriteTimeUtc.Ticks}:{fi.Length}" : "fehlt";
        lock (sperre)
        {
            if (gemerkt is { } g && g.Stand == stand) return g.Testraum;
            var t = TestraumModell.Bauen(TestraumKatalog.Laden(KatalogOrdner));
            gemerkt = (stand, t);
            Gebaut++;
            return t;
        }
    }
}

public static class LaborTestraum
{
    public const string Medientyp = "model/gltf-binary";

    /// <summary>GET /api/xr/geschosse und GET /api/xr/geschoss/{id}.glb des Testraums (siehe Dateikopf). Der Katalog kommt aus Einstellungen.KatalogOrdner.</summary>
    public static WebApplication Abbilden(WebApplication app)
    {
        var quelle = new TestraumQuelle(app.Services.GetRequiredService<Einstellungen>().KatalogOrdner);
        var g = app.MapGroup("/api/xr");

        g.MapGet("/geschosse", () => Gesichert(() => Task.FromResult(Mit(quelle, t => Antwort(TestraumModell.Liste(t)))))).XrImNetz();

        g.MapGet("/geschoss/{id}.glb", (string id, string? teil, HttpRequest r) => Gesichert(() => Task.FromResult(Mit(quelle, t =>
        {
            teil = string.IsNullOrWhiteSpace(teil) ? TestraumModell.TeilModell : teil.Trim().ToLowerInvariant();
            if (teil is not (XrModell.TeilModell or XrModell.TeilUnterlage))
                return Antwort(new { fehler = $"Teil «{teil}» gibt es nicht.", vorschlag = "teil=modell oder teil=unterlage." }, 400);
            var e = t.Etage(id);
            if (e is null)
                return Antwort(new { fehler = $"Geschoss «{id}» gibt es nicht.", vorschlag = "Id aus GET /api/xr/geschosse verwenden." }, 404);
            if (teil == XrModell.TeilUnterlage)
                return Antwort(new { fehler = $"Im Geschoss {e.Geschoss.Kennzeichen} gibt es keine IFC-Unterlage.", vorschlag = "Der Testraum des Labors hat keine IFC-Unterlage (unterlage = null in GET /api/xr/geschosse)." }, 404);
            var ctx = r.HttpContext;   // (HttpContext) als Parameter wäre ein RequestDelegate: das zurückgegebene IResult würde nie geschrieben
            var etag = new EntityTagHeaderValue($"\"{e.Schluessel}\"");
            ctx.Response.Headers.ETag = etag.ToString();
            ctx.Response.Headers.CacheControl = "no-cache";
            if (Passt(r, etag)) return Results.StatusCode(StatusCodes.Status304NotModified);
            return Results.Bytes(e.Glb, Medientyp);
        })))).XrImNetz();
        return app;
    }

    /// <summary>Arbeit mit dem Testraum; ein fehlender oder ungültiger Katalog ergibt 409 mit Fehler und Vorschlag (wie KICAD ohne Projekt).</summary>
    static IResult Mit(TestraumQuelle q, Func<TestraumStand, IResult> arbeit)
    {
        TestraumStand t;
        try { t = q.Holen(); }
        catch (HlksFehler f) { return Antwort(new { fehler = f.Message, vorschlag = f.Vorschlag }, 409); }
        return arbeit(t);
    }

    /// <summary>If-None-Match enthält diesen ETag (schwacher Vergleich, wie HTTP es für If-None-Match vorsieht) oder «*» – wie XrModellEndpunkte.</summary>
    static bool Passt(HttpRequest r, EntityTagHeaderValue etag)
    {
        var kopf = r.Headers.IfNoneMatch;
        if (kopf.Count == 0) return false;
        if (!EntityTagHeaderValue.TryParseList(kopf, out var liste)) return false;
        return liste.Any(x => x.Equals(EntityTagHeaderValue.Any) || x.Compare(etag, useStrongComparison: false));
    }
}
