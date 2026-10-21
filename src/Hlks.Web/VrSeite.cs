// VR-Startseite (Modul XR, 21.10.2026): GET /vr liefert wwwroot/vr.html aus (das Gegenstück zu «/» für die Meta Quest: ein Tipp, und man steht im Modell).
// /vr/ führt auf /vr zurück. Das Web-App-Manifest vr.webmanifest und die Symbole liefert die statische Auslieferung wie die übrigen Dateien.
// Wie /api und /hilfe liegen /vr, /vr.html und /vr.webmanifest hinter der Ursprungsprüfung (Herkunftsschutz.cs: Host-Kopf nur localhost).
// Keine Logik hier; alles andere steht in js/vr-start.js und über die Kern-Tür.

namespace Hlks.Web;

public static class VrSeite
{
    public static WebApplication MapVrSeite(this WebApplication app)
    {
        app.MapGet("/vr", (IWebHostEnvironment env, HttpContext ctx) =>
        {
            if (ctx.Request.Path.Value is { } p && p.EndsWith('/')) return Results.Redirect("/vr", permanent: false);
            var datei = Path.Combine(env.WebRootPath ?? "", "vr.html");
            if (!File.Exists(datei)) return Results.NotFound();
            ctx.Response.Headers.CacheControl = "no-cache";
            return Results.File(datei, "text/html; charset=utf-8");
        });
        return app;
    }
}
