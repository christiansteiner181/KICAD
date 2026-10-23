// REST und Kanal des XR-Zugangs (Modul XR, Quest App P3); in Program.cs hängt nur «app.MapXr();» sie ein. Reine Rechner-Funktionen: kein Kern-Befehl,
// das Modell wird nicht berührt; für die KI verboten (KiEinordnung.RechnerFunktionen xr_zugang, xr_kopplung, xr_kanal; kein MCP-Werkzeug).
//
// Nur am PC (localhost, Herkunftsschutz; im Netzzugang 404, weil ohne Marke XrImNetz):
//   GET  /api/xr/zugang                 Zustand: { an, aktiv, adresse, port, suchPort, sucheAktiv, seit?, pc, ips[], fingerabdruck, fingerabdruckKurz, zertifikatBis,
//                                       zertifikatErsetzt, fehler?, hinweis?, kopplungen[{ id, brille, name, weg, angelegt, zuletzt?, verbunden }],
//                                       anfragen[{ anfrage, brille, name, ip?, code, bis, bestaetigt, abgelehnt, versuche }], teilnehmer }
//   POST /api/xr/zugang                 { an: bool }  Einstellung xr.zugang setzen und sofort anwenden → Zustand
//   POST /api/xr/kopplung/paket         { brille, name? }  Weg A: Kopplungspaket (mit Schlüssel; nur einmal, nie speichern) → { paket }
//   POST /api/xr/kopplung/bestaetigen   { anfrage }  Weg B am PC bestätigen → { anfrage }
//   POST /api/xr/kopplung/ablehnen      { anfrage }  Weg B ablehnen → { anfrage }
//   POST /api/xr/kopplung/widerrufen    { kopplung }  Kopplung widerrufen (trennt den Kanal) → { widerrufen: bool }
//   GET  /api/xr/teilnehmer             { teilnehmer[{ id, name, geraet, app?, kopplung, brille, brillenName, seit, pose?, poseZeit?, zeigen?, zeigenZeit?,
//                                       posen, weitergereicht, verworfen }], stellschrauben{} }
//   POST /api/xr/stellschraube          { schluessel, wert, teilnehmer? }  an die Brille(n) → { gesendet, stellschrauben }
// Für die Brille (Marke XrImNetz; im Netzzugang nur über HTTPS mit Schlüssel, ausser den zwei Kopplungsschritten):
//   GET  /api/xr/verbindung             Probe mit Schlüssel → { ok, fassung, pc, kopplung{ id, brille, name }, zeit, teilnehmer }
//   GET  /api/xr/kanal                  WebSocket (XrKanal.cs)
//   POST /api/xr/kopplung/anfrage       { brille, name? }  Weg B, ohne Schlüssel → { anfrage, bis, versucheMax }
//   POST /api/xr/kopplung/abholen       { anfrage, code }  Weg B, ohne Schlüssel → 200 { status: gekoppelt, paket } | 202 { status: wartet } | 400 | 403 | 404 | 410
// Fehler: { fehler, vorschlag } (400 Eingabe, 401 Schlüssel, 404, 409, 429 Bremse, 503 Ablage).

using System.Text.Json.Nodes;
using Hlks.Kern;
using static Hlks.Web.EndpunktHilfe;

namespace Hlks.Web;

public static class XrEndpunkte
{
    public static WebApplication MapXr(this WebApplication app)
    {
        var netz = app.Services.GetRequiredService<XrNetzDienst>();
        var kopplungen = app.Services.GetRequiredService<XrKopplungen>();
        var kanal = app.Services.GetRequiredService<XrKanal>();
        var einstellungen = app.Services.GetRequiredService<XrEinstellungen>();
        var g = app.MapGroup("/api/xr");

        // ---------------------------------------------------------------- nur PC (localhost)

        g.MapGet("/zugang", () => XrGesichert(() => Task.FromResult(Antwort(netz.Zustand()))));
        g.MapPost("/zugang", (HttpRequest r) => XrGesichert(async () =>
        {
            var p = await KoerperLesen(r);
            var an = p["an"] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : throw new HlksFehler("«an» muss true oder false sein.", "{ \"an\": true } öffnet den Zugang im Büronetz, false schliesst ihn.");
            await netz.Schalten(an);
            return Antwort(netz.Zustand());
        }));
        g.MapPost("/kopplung/paket", (HttpRequest r) => XrGesichert(async () =>
        {
            var p = await KoerperLesen(r);
            var paket = kopplungen.PaketErzeugen(Param.Text(p, "brille"), Param.Text(p, "name"), netz.Info());
            r.HttpContext.Response.Headers.CacheControl = "no-store";
            return Antwort(new { paket });
        }));
        g.MapPost("/kopplung/bestaetigen", (HttpRequest r) => XrGesichert(async () =>
        {
            var p = await KoerperLesen(r);
            return Antwort(new { anfrage = kopplungen.Bestaetigen(Param.Text(p, "anfrage", true)) });
        }));
        g.MapPost("/kopplung/ablehnen", (HttpRequest r) => XrGesichert(async () =>
        {
            var p = await KoerperLesen(r);
            return Antwort(new { anfrage = kopplungen.Ablehnen(Param.Text(p, "anfrage", true)) });
        }));
        g.MapPost("/kopplung/widerrufen", (HttpRequest r) => XrGesichert(async () =>
        {
            var p = await KoerperLesen(r);
            return Antwort(new { widerrufen = kopplungen.Widerrufen(Param.Text(p, "kopplung", true)) });
        }));
        g.MapGet("/teilnehmer", () => XrGesichert(() => Task.FromResult(Antwort(new JsonObject { ["teilnehmer"] = kanal.TeilnehmerListe(), ["stellschrauben"] = kanal.Stellschrauben() }))));
        g.MapPost("/stellschraube", (HttpRequest r) => XrGesichert(async () =>
        {
            var p = await KoerperLesen(r);
            if (!p.ContainsKey("wert")) throw new HlksFehler("Parameter «wert» fehlt.", "Eine Zahl, true/false oder einen kurzen Text senden; null setzt zurück.");
            var gesendet = kanal.StellschraubeSetzen(Param.Text(p, "schluessel", true), p["wert"], Param.Text(p, "teilnehmer"));
            return Antwort(new JsonObject { ["gesendet"] = gesendet, ["stellschrauben"] = kanal.Stellschrauben() });
        }));

        // ---------------------------------------------------------------- für die Brille (Netzzugang)

        g.MapGet("/verbindung", (HttpContext ctx) =>
        {
            var k = XrPforte.Anmeldung(ctx, kopplungen);
            if (k is null) return OhneSchluessel(ctx);
            return Antwort(new
            {
                ok = true, fassung = XrNachrichten.Fassung, pc = Environment.MachineName, kopplung = new { id = k.Id, brille = k.Brille, name = k.Name },
                zeit = einstellungen.Jetzt().ToString("o"), teilnehmer = kanal.Anzahl,
            });
        }).XrImNetz();

        g.MapGet("/kanal", async (HttpContext ctx) =>
        {
            var k = XrPforte.Anmeldung(ctx, kopplungen);
            if (k is null) { await OhneSchluessel(ctx).ExecuteAsync(ctx); return; }
            if (!ctx.WebSockets.IsWebSocketRequest)
            {
                await Antwort(new { fehler = "Hier gibt es nur den Live-Kanal (WebSocket) über den XR-Zugang.", vorschlag = "wss://<PC>:<Port>/api/xr/kanal mit dem Schlüssel öffnen." }, 400).ExecuteAsync(ctx);
                return;
            }
            await kanal.Bedienen(ctx, k);
        }).XrImNetz();

        g.MapPost("/kopplung/anfrage", (HttpRequest r) => XrGesichert(async () =>
        {
            var p = await KoerperLesen(r);
            var a = kopplungen.Anfragen(Param.Text(p, "brille"), Param.Text(p, "name"), r.HttpContext.Connection.RemoteIpAddress?.ToString());
            return Antwort(new { anfrage = a.Anfrage, bis = a.Bis.ToString("o"), versucheMax = einstellungen.KopplungVersuche });
        })).XrImNetz(ohneSchluessel: true);

        g.MapPost("/kopplung/abholen", (HttpRequest r) => XrGesichert(async () =>
        {
            var p = await KoerperLesen(r);
            var e = kopplungen.Abholen(Param.Text(p, "anfrage"), Param.Text(p, "code"), r.HttpContext.Connection.RemoteIpAddress?.ToString(), netz.Info);
            if (e.Paket is not null) r.HttpContext.Response.Headers.CacheControl = "no-store";
            return Antwort(new { status = e.Zustand, meldung = e.Meldung, versucheUebrig = e.VersucheUebrig, paket = e.Paket }, e.Status);
        })).XrImNetz(ohneSchluessel: true);

        return app;
    }

    static IResult OhneSchluessel(HttpContext ctx)
    {
        ctx.Response.Headers.WWWAuthenticate = "Bearer";
        return Antwort(new { fehler = "Schlüssel fehlt oder gilt nicht.", vorschlag = "Die Brille mit KICAD koppeln (Kopplungspaket oder Code am PC)." }, 401);
    }

    /// <summary>Wie EndpunktHilfe.Gesichert, dazu XrFehler mit eigenem Status und zu grosse Körper (413; BadHttpRequestException ist eine IOException und gälte sonst als 503).</summary>
    static async Task<IResult> XrGesichert(Func<Task<IResult>> arbeit)
    {
        async Task<IResult> OhneZuGross()
        {
            try { return await arbeit(); }
            catch (BadHttpRequestException ex) { return Antwort(new { fehler = "Die Anfrage ist zu gross oder unvollständig." }, ex.StatusCode); }
        }
        try { return await Gesichert(OhneZuGross); }
        catch (XrFehler f) { return Antwort(new { fehler = f.Message, vorschlag = f.Vorschlag }, f.Status); }
    }
}
