// REST-Schnittstelle des Startbildschirms (S1, entwurf/startbildschirm.md). Dünne Hülle über StartDienst und ZuletztListe;
// in Program.cs hängt nur «app.MapStart(dienst);» sie ein. Fachliche Fehler: 400 mit { fehler, vorschlag }, Ablage/Netz weg: 503.
//
//   GET  /api/zuletzt?max=10&pruefen=0     zuletzt geöffnete Projekte, angeheftete zuerst, dann nach Zeit:
//                                          [{ ordner, name, zeit, zeitIso, rechner, angeheftet, objekt, vorhanden?, vorschau?, vorschauV? }]
//                                          (vorhanden/vorschau/vorschauV nur mit pruefen=1: mit Zeitlimit; vorhanden null = unbekannt)
//   POST /api/zuletzt/anheften             { ordner, an?: true, name? }  → { angeheftet }
//   POST /api/zuletzt/entfernen            { ordner }                    → { entfernt: true }
//   GET  /api/projekte/ablage?neu=1        { pfad, kicadOrdner, erreichbar, hinweis, gesucht, dauerMs, zwischengespeichert, laeuft,
//                                            projekte: [{ name, objekt, ordner, nummer, geaendert, vorschau, vorschauV }] }
//   GET  /api/ordner?pfad=                 { pfad, eltern, ordner: [{ name, pfad, projekt, enthaeltProjekt }], istProjekt, projektUnter,
//                                            ablage, kicadOrdner }  – ohne pfad die Laufwerke: { laufwerke: [{ name, pfad, art, bezeichnung }] }
//   POST /api/ordner/zeigen                { pfad }  Ordner im Explorer zeigen (nur lokal)
//   GET  /api/familien/zuletzt?n=12        { bibliothek, erreichbar, offline, hinweis, familien: [{ id, name, kategorie, hersteller, status,
//                                            version, projekt, projektNummer, projektOrdner, zeit, zeitIso }] }
//   POST /api/projekt/vorschau?stand=&ort= Body: PNG (image/png) → speichert vorschau.png im Ordner des offenen Projekts
//   GET  /api/projekt/vorschau?ordner=     das Vorschaubild eines Projektordners (404 ohne Bild; nur Ordner mit projekt.json)
//   GET  /api/start/info                   Programmstand (Version, Build-Datum), Rechner, Ablage der Liste «zuletzt»,
//                                          anleitung { url: "/hilfe/", vorhanden, hinweis, ordner } (Bedienungsanleitung, von KICAD selbst ausgeliefert:
//                                          AnleitungEndpunkte.cs; vorhanden = auf diesem Rechner gebaut) für das Hilfe-Symbol

using Hlks.Kern;
using static Hlks.Web.EndpunktHilfe;   // Antwort, KoerperLesen, Gesichert (gemeinsam mit BenutzerEndpunkte)

namespace Hlks.Web;

public static class StartEndpunkte
{
    public static WebApplication MapStart(this WebApplication app, ProjektDienst dienst)
    {
        var start = new StartDienst(dienst);
        app.Lifetime.ApplicationStarted.Register(start.Aufwaermen);

        var g = app.MapGroup("/api");

        g.MapGet("/zuletzt", (int? max, string? pruefen) => Gesichert(async () =>
        {
            var liste = dienst.Zuletzt.Alle().Take(Math.Clamp(max ?? 10, 1, 200)).ToList();
            var pruef = Flag(pruefen) ? await start.Pruefen(liste.Select(z => z.Ordner)) : null;
            return Antwort(liste.Select(z =>
            {
                (bool? Vorhanden, bool Vorschau, long V) p = default;
                var hat = pruef is not null && pruef.TryGetValue(z.Ordner, out p);
                return new
                {
                    ordner = z.Ordner, name = z.Name, zeit = z.ZeitText, zeitIso = z.Zeit.ToString("o"), rechner = z.Rechner, angeheftet = z.Angeheftet,
                    objekt = ObjektName(z.Ordner),
                    vorhanden = hat ? p.Vorhanden : null, vorschau = hat ? p.Vorschau : (bool?)null, vorschauV = hat ? p.V : (long?)null,
                };
            }).ToList());
        }));

        g.MapPost("/zuletzt/anheften", (HttpRequest r) => Gesichert(async () =>
        {
            var p = await KoerperLesen(r);
            var an = p["an"] is null || Param.Bool(p, "an");
            dienst.Zuletzt.Anheften(Param.Text(p, "ordner", true)!, an, Param.Text(p, "name"));
            return Antwort(new { angeheftet = an });
        }));

        g.MapPost("/zuletzt/entfernen", (HttpRequest r) => Gesichert(async () =>
        {
            var p = await KoerperLesen(r);
            dienst.Zuletzt.Entfernen(Param.Text(p, "ordner", true)!);
            return Antwort(new { entfernt = true });
        }));

        g.MapGet("/projekte/ablage", (string? neu) => Gesichert(async () =>
        {
            var a = await start.Ablage(Flag(neu));
            return Antwort(new
            {
                pfad = a.Pfad, kicadOrdner = a.KicadOrdner, erreichbar = a.Erreichbar, hinweis = a.Hinweis, gesucht = a.Gesucht.ToString("o"),
                dauerMs = a.DauerMs, zwischengespeichert = a.Zwischengespeichert, laeuft = a.Laeuft,
                projekte = a.Projekte.Select(x => new
                {
                    name = x.Name, objekt = x.Objekt, ordner = x.Ordner, nummer = x.Nummer, geaendert = x.Geaendert.ToString("o"), vorschau = x.Vorschau, vorschauV = x.VorschauV,
                }).ToList(),
            });
        }));

        g.MapGet("/ordner", (string? pfad) => Gesichert(async () => Antwort(await start.Ordner(pfad))));
        g.MapPost("/ordner/zeigen", (HttpRequest r) => Gesichert(async () => Antwort(start.ImExplorerZeigen(Param.Text(await KoerperLesen(r), "pfad", true)))));

        g.MapGet("/familien/zuletzt", (int? n) => Gesichert(async () => Antwort(await start.FamilienZuletzt(n ?? 12))));

        g.MapPost("/projekt/vorschau", (HttpRequest r, string? stand, string? ort) => Gesichert(async () =>
        {
            using var ms = new MemoryStream();
            var puffer = new byte[16 * 1024];
            int n;
            while ((n = await r.Body.ReadAsync(puffer)) > 0)
            {
                ms.Write(puffer, 0, n);
                if (ms.Length > StartDienst.VorschauMaxBytes)
                    throw new HlksFehler($"Das Vorschaubild ist zu gross (erlaubt {StartDienst.VorschauMaxBytes / 1024} KB).", "Das Bild kleiner rendern (z. B. 640 × 400).");
            }
            return Antwort(await start.VorschauSpeichern(ms.ToArray(), stand, ort));
        }));

        g.MapGet("/projekt/vorschau", (string? ordner, HttpContext ctx) =>
        {
            var pfad = start.VorschauPfad(ordner);
            if (pfad is null) return Results.Text("Für dieses Projekt gibt es noch kein Vorschaubild.", "text/plain; charset=utf-8", statusCode: 404);
            ctx.Response.Headers.CacheControl = "no-cache";
            return Results.File(pfad, "image/png", lastModified: new DateTimeOffset(File.GetLastWriteTimeUtc(pfad)));
        });

        g.MapGet("/start/info", () => Antwort(start.Info()));
        return app;
    }

    /// <summary>Schalter in der Adresse: 1, true oder ja.</summary>
    static bool Flag(string? s) => s is not null && (s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase) || s.Equals("ja", StringComparison.OrdinalIgnoreCase));

    /// <summary>Name des Objektordners aus …\&lt;Objekt&gt;\c_Pläne und Modelle\e_KICAD (drittletzter Teil), sonst leer.</summary>
    public static string ObjektName(string? ordner)
    {
        var teile = (ordner ?? "").Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        return teile.Length >= 3 ? teile[^3] : "";
    }
}
