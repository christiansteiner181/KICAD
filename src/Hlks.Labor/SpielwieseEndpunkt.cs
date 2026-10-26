// XR-Labor Spielwiese S1 (entwurf/xr-empfaenger.md, Neuausrichtung 26.10.2026; Auftrag S1 Punkt 2): der Katalog der Platzhalter-Apps für die Brille.
//
//   GET /api/xr/labor/spielwiese      der Text von kataloge/labor_spielwiese.json (Ordner der Kataloge, Einstellungen.KatalogOrdner) unverändert,
//                                     application/json; ETag = "<Schlüssel>" (SpielwieseKatalog.SchluesselVon), Cache-Control no-cache; If-None-Match → 304.
//                                     Fehlt die Datei oder ist sie ungültig (Prüfung wie in der App: Hlks.Xr.Labor.SpielwieseKatalog, Farben aus xr_farben.json):
//                                     409 { fehler, vorschlag } – die Brille behält dann ihre zuletzt geholte Fassung.
//
// Die Datei wird bei jedem Abruf gelesen (klein; die Brille fragt alle 30 s mit ETag): Claude ändert Apps und Inhalte in der Datei, ohne Neustart des Labors und ohne
// neue APK. Mit .XrImNetz() auch über den Netzzugang der Labor-Brille (5340, HTTPS mit Schlüssel), wie der Katalog des Labors (/api/xr/labor/katalog); auf localhost
// gilt der Herkunftsschutz des Labors (nur GET). Eingebunden mit einer Zeile in LaborDienste.MapLabor: LaborSpielwiese.Abbilden(app).

using System.Text;
using Hlks.Web;
using Hlks.Xr.Bedienung;
using Hlks.Xr.Labor;
using Hlks.Xr.Netz;
using Microsoft.Net.Http.Headers;
using static Hlks.Web.EndpunktHilfe;

namespace Hlks.Labor;

public static class LaborSpielwiese
{
    public const string Pfad = SpielwieseLader.Pfad;

    /// <summary>Text und Schlüssel des gültigen Katalogs im Ordner; wirft <see cref="XrNetzFehler"/>, wenn er fehlt oder ungültig ist.</summary>
    public static (string Text, string Schluessel) Lesen(string katalogOrdner)
    {
        var datei = Path.Combine(katalogOrdner, SpielwieseKatalog.Datei);
        string text;
        try { text = File.ReadAllText(datei, Encoding.UTF8); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new XrNetzFehler($"Der Katalog der Spielwiese fehlt: {datei} ({ex.Message}).", $"kataloge/{SpielwieseKatalog.Datei} anlegen bzw. den Ordner der Kataloge (--katalog, HLKS_KATALOG) prüfen.");
        }
        var k = SpielwieseKatalog.Lesen(text, Farben(katalogOrdner));
        return (text, k.Schluessel);
    }

    /// <summary>Die Einträge von xr_farben.json im selben Ordner; null, wenn die Datei fehlt oder ungültig ist (dann gilt nur das Muster der Farbe).</summary>
    static IReadOnlyCollection<string>? Farben(string ordner)
    {
        try { return [.. AppFarben.Lesen(File.ReadAllText(Path.Combine(ordner, "xr_farben.json"), Encoding.UTF8)).Apps.Keys]; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or System.Text.Json.JsonException) { return null; }
    }

    /// <summary>GET /api/xr/labor/spielwiese (siehe Dateikopf).</summary>
    public static WebApplication Abbilden(WebApplication app)
    {
        var ordner = app.Services.GetRequiredService<Einstellungen>().KatalogOrdner;
        app.MapGet(Pfad, (HttpRequest r) => Gesichert(() =>
        {
            string text, schluessel;
            try { (text, schluessel) = Lesen(ordner); }
            catch (XrNetzFehler f) { return Task.FromResult(Antwort(new { fehler = f.Message, vorschlag = f.Vorschlag }, 409)); }
            var ctx = r.HttpContext;   // (HttpContext) als Parameter wäre ein RequestDelegate: das zurückgegebene IResult würde nie geschrieben
            var etag = new EntityTagHeaderValue($"\"{schluessel}\"");
            ctx.Response.Headers.ETag = etag.ToString();
            ctx.Response.Headers.CacheControl = "no-cache";
            if (Passt(r, etag)) return Task.FromResult(Results.StatusCode(StatusCodes.Status304NotModified));
            return Task.FromResult(Results.Text(text, "application/json; charset=utf-8"));
        })).XrImNetz();
        return app;
    }

    /// <summary>If-None-Match enthält diesen ETag (schwacher Vergleich) oder «*» – wie LaborTestraum.</summary>
    static bool Passt(HttpRequest r, EntityTagHeaderValue etag)
    {
        var kopf = r.Headers.IfNoneMatch;
        if (kopf.Count == 0) return false;
        if (!EntityTagHeaderValue.TryParseList(kopf, out var liste)) return false;
        return liste.Any(x => x.Equals(EntityTagHeaderValue.Any) || x.Compare(etag, useStrongComparison: false));
    }
}
