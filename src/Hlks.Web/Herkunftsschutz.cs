// Schutz gegen Fremdaufrufe (Sicherheitsprüfung 3c, Befund B1, 20.10.2026): Der Server bindet nur localhost, aber der Browser
// des Benutzers ist localhost. Ohne Prüfung könnte jede fremde Webseite «einfache» Anfragen (text/plain, kein CORS nötig) an
// /api schicken – z. B. eine KI-Frage starten – und per DNS-Rebinding Antworten lesen. Diese Middleware steht vor allen
// Endpunkten unter /api und /hilfe sowie vor der VR-Startseite (/vr, /vr.html, /vr.webmanifest; nur Punkt 1, es sind GET-Seiten):
//   1. Host-Kopf nur localhost / 127.0.0.1 / [::1] (beliebiger Port), sonst 403 (DNS-Rebinding).
//   2. Ändernde Methoden (POST, PUT, PATCH, DELETE): Sec-Fetch-Site, falls vorhanden, nur same-origin/none; Origin, falls
//      vorhanden, nur http://localhost|127.0.0.1|[::1]:<Port dieses Servers>, sonst 403. Werkzeuge (PowerShell, Node, MCP)
//      senden beides nicht und bleiben erlaubt.
//   3. Ändernde Methoden mit Körper: Content-Type application/json; Ausnahmen nur die Uploads
//      (POST /api/unterlagen: application/octet-stream oder multipart/form-data; POST /api/projekt/vorschau: image/png),
//      sonst 415. Ohne Körper (z. B. …/abbrechen) ist kein Content-Type nötig.
// Es werden keine CORS-Kopfzeilen gesetzt.

using Microsoft.Net.Http.Headers;

namespace Hlks.Web;

public static class Herkunftsschutz
{
    static readonly string[] LoopbackNamen = ["localhost", "127.0.0.1", "[::1]"];
    const string Vorschlag = "KICAD nur aus der eigenen Oberfläche (http://localhost:<Port>) oder mit Werkzeugen auf diesem Rechner aufrufen.";

    /// <summary>Uploads mit anderem Inhaltstyp als JSON: Pfad → erlaubte Medientypen (zusätzlich zu application/json).</summary>
    static readonly (string Pfad, string[] Typen)[] Uploads =
    [
        ("/api/unterlagen", ["application/octet-stream", "multipart/form-data"]),
        ("/api/projekt/vorschau", ["image/png"]),
    ];

    /// <summary>Einzelne Seiten ausserhalb von /api und /hilfe, die ebenfalls geprüft werden (VR-Startseite, Modul XR).</summary>
    static readonly string[] Seiten = ["/vr.html", "/vr.webmanifest"];

    public static bool Geschuetzt(PathString pfad) =>
        pfad.StartsWithSegments("/api") || pfad.StartsWithSegments("/hilfe") || pfad.StartsWithSegments("/vr")
        || Seiten.Any(s => pfad.Equals(s, StringComparison.OrdinalIgnoreCase));

    public static bool Aendernd(string methode) =>
        HttpMethods.IsPost(methode) || HttpMethods.IsPut(methode) || HttpMethods.IsPatch(methode) || HttpMethods.IsDelete(methode);

    /// <summary>Host-Kopf zeigt auf diesen Rechner (Name ohne Port).</summary>
    public static bool HostErlaubt(HostString host) =>
        host.HasValue && LoopbackNamen.Contains(host.Host, StringComparer.OrdinalIgnoreCase);

    /// <summary>Origin ist die eigene Oberfläche: http://localhost|127.0.0.1|[::1] mit dem Port aus dem (geprüften) Host-Kopf.</summary>
    public static bool OriginErlaubt(string origin, HostString host)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var u) || u.Scheme != Uri.UriSchemeHttp) return false;
        if (u.AbsolutePath != "/" || u.Query.Length > 0 || !string.IsNullOrEmpty(u.UserInfo)) return false;
        if (!LoopbackNamen.Contains(u.Host, StringComparer.OrdinalIgnoreCase)) return false;   // Uri.Host liefert IPv6 mit Klammern: [::1]
        return u.Port == (host.Port ?? 80);
    }

    static bool HatKoerper(HttpRequest r) =>
        r.ContentLength is > 0 || (r.ContentLength is null && r.Headers.TransferEncoding.Count > 0);

    public static bool InhaltstypErlaubt(HttpRequest r)
    {
        if (!HatKoerper(r)) return true;
        if (!MediaTypeHeaderValue.TryParse(r.ContentType, out var typ) || typ.MediaType.Value is not { } mt) return false;
        if (mt.Equals("application/json", StringComparison.OrdinalIgnoreCase)) return true;
        if (!HttpMethods.IsPost(r.Method)) return false;
        foreach (var (pfad, typen) in Uploads)
            if (r.Path.Equals(pfad, StringComparison.OrdinalIgnoreCase) && typen.Contains(mt, StringComparer.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>Prüft eine Anfrage; null = durchlassen, sonst (Statuscode, Fehlertext).</summary>
    public static (int Code, string Fehler)? Pruefen(HttpRequest r)
    {
        if (!Geschuetzt(r.Path)) return null;
        if (!HostErlaubt(r.Host))
            return (403, $"Anfrage an fremden Host «{r.Host.Value}» abgelehnt");
        if (!Aendernd(r.Method)) return null;
        var site = r.Headers["Sec-Fetch-Site"].ToString();
        if (site.Length > 0 && !site.Equals("same-origin", StringComparison.OrdinalIgnoreCase) && !site.Equals("none", StringComparison.OrdinalIgnoreCase))
            return (403, "Anfrage von fremder Seite abgelehnt");
        if (r.Headers.Origin.Count > 0 && !OriginErlaubt(r.Headers.Origin.ToString(), r.Host))
            return (403, "Anfrage von fremder Seite abgelehnt");
        if (!InhaltstypErlaubt(r))
            return (415, $"Inhaltstyp «{r.ContentType ?? "(keiner)"}» nicht erlaubt – Parameter als application/json senden");
        return null;
    }

    public static WebApplication UseHerkunftsschutz(this WebApplication app)
    {
        app.Use(async (ctx, weiter) =>
        {
            if (Pruefen(ctx.Request) is { } abgelehnt)
            {
                ctx.Response.StatusCode = abgelehnt.Code;
                ctx.Response.ContentType = "application/json; charset=utf-8";
                await ctx.Response.WriteAsync(Hlks.Kern.Json.Text(new { fehler = abgelehnt.Fehler, vorschlag = Vorschlag }));
                return;
            }
            await weiter();
        });
        return app;
    }
}
