// REST-Schnittstelle der KI-Palette «Claude» (Modul 3c, Teil K). Dünne Hülle über dem GespraechDienst; in Program.cs hängt nur
// «app.MapGespraeche(dienst);» sie ein. Beschreibung für die Oberfläche: OBERFLAECHE.md, Abschnitt «KI-Palette: Schnittstelle (3c, Teil K)».
//
//   GET    /api/ki/einstellungen                      Modelle, Vorgaben, verfuegbar/grund, Grenzen
//   GET    /api/ki/gespraeche                         Gespräche des offenen Projekts (neueste zuerst)
//   POST   /api/ki/gespraeche              {titel?}   neues Gespräch -> 201
//   GET    /api/ki/gespraeche/{id}                    Gespräch mit Nachrichten (während einer Frage: laeuft, Antwort im Entstehen)
//   POST   /api/ki/gespraeche/{id}/frage   {text, kontext:{ansicht?, geschoss?, auswahl?, bild?}, modell?, internet?} -> 202
//   POST   /api/ki/gespraeche/{id}/abbrechen          beendet die laufende Frage, antwortet mit dem Gespräch
//   DELETE /api/ki/gespraeche/{id}                    verschiebt das Gespräch nach ki\geloescht
//   POST   /api/ki/aenderungen/{paket}/zuruecknehmen  nimmt ein Paket der KI zurück (409, wenn später dieselben Elemente geändert wurden)
//   GET    /api/normen/suche?q=…&reihe=…              Normensuche (gleiche Treffer wie normen_suchen)
//   GET    /api/normen                                Zustand des Normenverzeichnisses; POST /api/normen/aufbauen startet den Volltext-Aufbau
//
// Fehler: 400 { fehler, vorschlag } (Eingabe), 404 (unbekannt), 409 (kein Projekt offen, Lauf belegt, schon beendet, spätere Änderungen),
// 503 (Ablage nicht erreichbar). Einstellungen: GespraechEinstellungen aus dem Dienstcontainer (Tests), sonst die Vorgabe aus der Umgebung.

using System.Text.Json.Nodes;
using Hlks.Kern;

namespace Hlks.Web;

public static class GespraechEndpunkte
{
    public static WebApplication MapGespraeche(this WebApplication app, ProjektDienst dienst)
    {
        var einstellungen = app.Services.GetService<GespraechEinstellungen>() ?? GespraechEinstellungen.Vorgabe();
        var laeufe = app.Services.GetService<KiLaeufe>() ?? new KiLaeufe();
        var ki = new GespraechDienst(dienst, einstellungen, laeufe);
        app.Lifetime.ApplicationStopping.Register(ki.BeendenAlle);

        static IResult Antwort(object o, int code = 200) => EndpunktHilfe.Antwort(o, code);

        static async Task<IResult> Sicher(Func<Task<IResult>> arbeit)
        {
            try { return await arbeit(); }
            catch (AuftragHttpFehler f) { return Antwort(new { fehler = f.Message, vorschlag = f.Vorschlag }, f.Code); }
            catch (ZuruecknehmenFehler f)
            {
                return Antwort(new JsonObject
                {
                    ["fehler"] = f.Ergebnis.Fehler ?? f.Message, ["vorschlag"] = f.Ergebnis.Vorschlag, ["ablehnung"] = f.Ergebnis.Ablehnung,
                    ["spaeter"] = f.Ergebnis.SpaeterePakete is { } s ? Json.Knoten(s) : null,
                    ["gesperrt"] = f.Ergebnis.Gesperrt.Count > 0 ? Json.Knoten(f.Ergebnis.Gesperrt) : null,
                }, f.Code);
            }
            catch (HlksFehler f) { return Antwort(new { fehler = f.Message, vorschlag = f.Vorschlag }, f.Message.StartsWith("Es ist kein Projekt offen") ? 409 : 400); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { return Antwort(new { fehler = "Projektablage nicht erreichbar: " + ex.Message, vorschlag = "Netzwerk/VPN prüfen." }, 503); }
        }

        var g = app.MapGroup("/api/ki");
        g.MapGet("/einstellungen", () => Sicher(() => Task.FromResult(Antwort(ki.EinstellungenJson()))));
        g.MapGet("/gespraeche", () => Sicher(() => Task.FromResult(Antwort(new { gespraeche = ki.Liste() }))));
        g.MapPost("/gespraeche", (HttpRequest r) => Sicher(async () => Antwort(ki.Anlegen(await EndpunktHilfe.KoerperLesen(r)), 201)));
        g.MapGet("/gespraeche/{id}", (string id) => Sicher(async () => Antwort(await ki.Lesen(id))));
        g.MapPost("/gespraeche/{id}/frage", (string id, HttpRequest r) => Sicher(async () => Antwort(await ki.Fragen(id, await EndpunktHilfe.KoerperLesen(r)), 202)));
        g.MapPost("/gespraeche/{id}/abbrechen", (string id) => Sicher(async () => Antwort(await ki.Abbrechen(id))));
        g.MapDelete("/gespraeche/{id}", (string id) => Sicher(() => Task.FromResult(Antwort(ki.Loeschen(id)))));
        g.MapPost("/aenderungen/{paket}/zuruecknehmen", (string paket) => Sicher(async () => Antwort(await ki.Zuruecknehmen(paket))));

        // Normensuche (für das Suchfeld der Palette; die Normen hängen nicht am Projekt)
        var n = app.MapGroup("/api/normen");
        n.MapGet("/suche", (string? q, string? reihe) => Sicher(async () => Antwort(await Task.Run(() => ki.NormenSuchen(q, reihe)))));
        n.MapGet("", () => Sicher(async () => Antwort(await Task.Run(() => ki.Normen().ZustandJson()))));
        n.MapPost("/aufbauen", () => Sicher(async () =>
        {
            var v = ki.Normen();
            if (v.Sperre is not null) throw new HlksFehler(v.Sperre);
            _ = v.AufbauenImHintergrund();
            return Antwort(await Task.Run(v.ZustandJson), 202);
        }));
        return app;
    }
}
