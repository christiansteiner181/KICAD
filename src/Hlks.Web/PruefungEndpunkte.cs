// Prüfansicht (PDF prüfen und markieren, Christian 02.11.2026) – dünne Hülle über dem Kern (PruefDokument); in Program.cs hängt nur
// «app.MapPruefung(dienst);» sie ein. Die Markierungen sind Elemente (Pruefmarke) und laufen über die Befehle element_anlegen/element_aendern.
//
//   GET /api/pruefung/info?dokument=…                 { seiten, masse: [{ breite, hoehe }] (pt), titel }
//   GET /api/pruefung/seite?dokument=…&seite=n&dpi=…  die Seite als PNG (dpi 36–300, Vorgabe 110)

using System.Text.Json.Nodes;
using Hlks.Kern;
using static Hlks.Web.EndpunktHilfe;

namespace Hlks.Web;

public static class PruefungEndpunkte
{
    public static WebApplication MapPruefung(this WebApplication app, ProjektDienst dienst)
    {
        app.MapGet("/api/pruefung/info", (string? dokument) => Gesichert(async () =>
        {
            if (string.IsNullOrWhiteSpace(dokument)) return Antwort(new { fehler = "Parameter «dokument» fehlt.", vorschlag = "Kennzeichen eines Planblatts oder einer PDF-Unterlage" }, 400);
            var (seiten, masse, titel) = await dienst.Mit(p => PruefDokument.Info(p, dokument));
            return Antwort(new JsonObject
            {
                ["seiten"] = seiten, ["titel"] = titel,
                ["masse"] = new JsonArray(masse.Select(m => (JsonNode)new JsonObject { ["breite"] = Math.Round(m.Breite, 2), ["hoehe"] = Math.Round(m.Hoehe, 2) }).ToArray()),
            });
        }));
        app.MapGet("/api/pruefung/seite", (string? dokument, int? seite, double? dpi) => Gesichert(async () =>
        {
            if (string.IsNullOrWhiteSpace(dokument)) return Antwort(new { fehler = "Parameter «dokument» fehlt.", vorschlag = "Kennzeichen eines Planblatts oder einer PDF-Unterlage" }, 400);
            var png = await dienst.Mit(p => PruefDokument.SeitePng(p, dokument, seite ?? 1, dpi));
            return Results.File(png, "image/png");
        }));
        return app;
    }
}
