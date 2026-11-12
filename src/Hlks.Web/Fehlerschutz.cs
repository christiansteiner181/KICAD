// Unerwartete Ausnahmen unter /api (Gesamtprüfung 13.11.2026): Die Endpunkte fangen fachliche Fehler (HlksFehler → 400/409) und Ablagefehler
// (503) selbst ab. Alles andere (Programmfehler im Kern, unlesbare Datei, …) kam bisher als leere Antwort 500 zurück – die Oberfläche zeigte
// dann nur «Fehler 500». Diese Middleware macht daraus die übliche Antwort { fehler, vorschlag } mit 500 und schreibt die Ausnahme ins Protokoll.
// Abgebrochene Anfragen (Browser hat aufgelegt) und schon begonnene Antworten (Ereignisstrom, Dateien) bleiben unberührt.

namespace Hlks.Web;

public static class Fehlerschutz
{
    public const string Vorschlag = "Den Vorgang wiederholen. Bleibt es so: KICAD neu starten und den Fehler mit dem letzten Arbeitsschritt melden.";

    public static string Meldung(Exception ex) => $"Unerwarteter Fehler im KICAD-Server: {ex.Message} ({ex.GetType().Name})";

    public static WebApplication UseFehlerschutz(this WebApplication app)
    {
        var log = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Hlks.Web.Fehlerschutz");
        app.Use(async (ctx, weiter) =>
        {
            if (!ctx.Request.Path.StartsWithSegments("/api")) { await weiter(); return; }
            try { await weiter(); }
            catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested) { }
            catch (Exception ex) when (ex is not OperationCanceledException && !ctx.Response.HasStarted)
            {
                log.LogError(ex, "Unerwarteter Fehler bei {methode} {pfad}", ctx.Request.Method, ctx.Request.Path);
                ctx.Response.Clear();
                ctx.Response.StatusCode = 500;
                ctx.Response.ContentType = "application/json; charset=utf-8";
                await ctx.Response.WriteAsync(Hlks.Kern.Json.Text(new { fehler = Meldung(ex), vorschlag = Vorschlag }));
            }
        });
        return app;
    }
}
