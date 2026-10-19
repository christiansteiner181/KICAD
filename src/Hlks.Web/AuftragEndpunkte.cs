// REST-Schnittstelle der KI-Aufträge (Modul 2, B5). Dünne Hülle über dem AuftragDienst; in Program.cs hängt nur
// «app.MapAuftraege(dienst);» sie ein.
//
//   POST /api/auftraege                 {art: "familie_aus_unterlagen", unterlagen[], kategorie?, hinweis?, familie?, zeitlimitMinuten?}
//                                       -> 202 mit dem Auftrag (id, status wartet/laeuft); 409, wenn schon ein Lauf im Projekt läuft
//   GET  /api/auftraege                 Liste der Aufträge des offenen Projekts (neueste zuerst, ohne Bericht)
//   GET  /api/auftraege/{id}            Status mit letzten Schritten und Ergebnis (bericht, familien)
//   POST /api/auftraege/{id}/abbrechen  beendet Claude samt MCP-Server; antwortet, wenn der Auftrag beendet ist
//
// Einstellungen: AuftragEinstellungen aus dem Dienstcontainer (Tests), sonst die Vorgabe aus der Umgebung.

using System.Text;
using System.Text.Json.Nodes;
using Hlks.Kern;

namespace Hlks.Web;

public static class AuftragEndpunkte
{
    public static WebApplication MapAuftraege(this WebApplication app, ProjektDienst dienst)
    {
        var einstellungen = app.Services.GetService<AuftragEinstellungen>() ?? AuftragEinstellungen.Vorgabe();
        var auftraege = new AuftragDienst(dienst, einstellungen, app.Services.GetService<KiLaeufe>());   // Modul 3c: ein KI-Lauf je Projekt, gemeinsam mit den Gesprächen
        app.Lifetime.ApplicationStopping.Register(auftraege.BeendenAlle);

        static IResult Antwort(object o, int code = 200) =>
            Results.Text(Json.Text(o), "application/json; charset=utf-8", statusCode: code);

        static async Task<JsonObject> Koerper(HttpRequest r)
        {
            using var leser = new StreamReader(r.Body, Encoding.UTF8);
            var text = await leser.ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(text)) return new JsonObject();
            try { return JsonNode.Parse(text) as JsonObject ?? throw new HlksFehler("Parameter müssen ein JSON-Objekt sein."); }
            catch (System.Text.Json.JsonException ex) { throw new HlksFehler("Parameter sind kein gültiges JSON: " + ex.Message); }
        }

        static async Task<IResult> Sicher(Func<Task<IResult>> arbeit)
        {
            try { return await arbeit(); }
            catch (AuftragHttpFehler f) { return Antwort(new { fehler = f.Message, vorschlag = f.Vorschlag }, f.Code); }
            catch (HlksFehler f) { return Antwort(new { fehler = f.Message, vorschlag = f.Vorschlag }, f.Message.StartsWith("Es ist kein Projekt offen") ? 409 : 400); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { return Antwort(new { fehler = "Projektablage nicht erreichbar: " + ex.Message, vorschlag = "Netzwerk/VPN prüfen." }, 503); }
        }

        var g = app.MapGroup("/api/auftraege");
        g.MapPost("", (HttpRequest r) => Sicher(async () => Antwort(await auftraege.Starten(await Koerper(r)), 202)));
        g.MapGet("", () => Sicher(() => Task.FromResult(Antwort(new { auftraege = auftraege.Liste() }))));
        g.MapGet("/{id}", (string id) => Sicher(() => Task.FromResult(
            auftraege.Status(id) is { } s ? Antwort(s)
            : Antwort(new { fehler = $"Einen Auftrag «{id}» gibt es nicht.", vorschlag = "GET /api/auftraege listet die Aufträge." }, 404))));
        g.MapPost("/{id}/abbrechen", (string id) => Sicher(async () => Antwort(await auftraege.Abbrechen(id))));
        return app;
    }
}
