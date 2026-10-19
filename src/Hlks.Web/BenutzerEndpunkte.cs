// REST-Schnittstelle der Benutzereinstellungen (G1, entwurf/projektbrowser-gliederung.md). Dünne Hülle über BenutzerEinstellungen;
// in Program.cs hängt nur «app.MapBenutzer(dienst);» sie ein. Fehler: 400 mit { fehler, vorschlag }, Ablage/Netz weg: 503 (EndpunktHilfe).
//
//   GET  /api/benutzer/einstellungen          { werte: { "<schluessel>": <wert> }, zeiten: { "<schluessel>": { zeit, rechner } }, rechner, quelle, ort }
//                                             die Einstellungen des Benutzers, vereinigt über alle seine Rechner (der neueste Wert je Schlüssel gilt)
//   POST /api/benutzer/einstellungen          { schluessel, wert }  → { schluessel, wert, zeit, rechner }; wert null setzt den Schlüssel zurück
//
// Schlüssel heute: projektbrowser.gliederung (Liste der Kennungen aus gebaeude, sanitaer, heizung, lueftung, koordination, energie; ein alter Einzeltext gilt auch). Weitere Schlüssel
// brauchen keinen neuen Code (siehe BenutzerEinstellungen.cs).

using System.Text.Json.Nodes;
using static Hlks.Web.EndpunktHilfe;

namespace Hlks.Web;

public static class BenutzerEndpunkte
{
    public static WebApplication MapBenutzer(this WebApplication app, ProjektDienst dienst)
    {
        var g = app.MapGroup("/api/benutzer");

        g.MapGet("/einstellungen", () => Gesichert(() =>
        {
            var alle = dienst.Benutzer.Alle();
            var werte = new JsonObject();
            var zeiten = new JsonObject();
            foreach (var (k, s) in alle.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                werte[k] = s.Wert?.DeepClone();
                zeiten[k] = new JsonObject { ["zeit"] = s.Zeit.ToString("o"), ["rechner"] = s.Rechner };
            }
            return Task.FromResult(Antwort(new { werte, zeiten, rechner = dienst.Benutzer.Rechner, quelle = dienst.Benutzer.Quelle, ort = dienst.Benutzer.Verzeichnis }));
        }));

        g.MapPost("/einstellungen", (HttpRequest r) => Gesichert(async () =>
        {
            var p = await KoerperLesen(r);
            var schluessel = p["schluessel"] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
            var wert = p["wert"];
            var stand = dienst.Benutzer.Setzen(schluessel, wert);
            return Antwort(new { schluessel = stand.Schluessel, wert = stand.Wert, zeit = stand.Zeit.ToString("o"), rechner = stand.Rechner });
        }));

        return app;
    }
}
