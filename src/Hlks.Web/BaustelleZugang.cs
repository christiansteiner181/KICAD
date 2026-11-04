// BIM to field: Testserver für die Baustellen-Webseite im Büronetz (Christian 04.11.2026: «ich habe noch kein testserver mit icon»).
// Die Webseite als Einzeldatei (file://) lässt sich am Tablet nicht mit Icon auf den Startbildschirm legen und kann nichts zurücksenden; dafür liefert
// KICAD sie über HTTP aus. Eingeschaltet nur mit --baustelle-port <n> bzw. HLKS_BAUSTELLE_PORT (werkzeuge\baustelle_testserver.ps1):
// - zweiter Zugang http://<alle IPv4>:<n>, auf dem NUR /baustelle/… antwortet (alles andere 404) – die Oberfläche und /api bleiben auf localhost.
// - GET  /baustelle/                     Übersicht der Webseiten aus <Projekt>\ausgabe\baustelle (neueste zuerst), Manifest und Icon
// - GET  /baustelle/seite/<datei>.html   die Webseite, ergänzt um den Verweis aufs Manifest (Startbildschirm mit Icon)
// - GET  /baustelle/manifest.webmanifest, /baustelle/icon.png, /baustelle/icon.svg
// - POST /baustelle/rueckmeldung         Rückmeldung (Format kicad-baustelle-rueckmeldung, wie die Datei) → <Projekt>\eingang\baustelle\*.json;
//                                         nur über den Baustellen-Zugang. Übernehmen tut weiter der Mensch in KICAD (Rückmeldung Baustelle).
// Grenzen (Testserver): keine Anmeldung – wer im Büronetz die Adresse kennt, sieht die Webseiten des offenen Projekts; kein HTTPS, darum kein
// Offline-Speicher (Service Worker) und am Android-Tablet nur «Zum Startbildschirm hinzufügen», keine Installation als App.

using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using Hlks.Kern;
using Microsoft.AspNetCore.Http.Features;
using static Hlks.Web.EndpunktHilfe;

namespace Hlks.Web;

public static class BaustelleZugang
{
    /// <summary>Höchstgrösse einer Rückmeldung (Fotos als JPEG in der JSON, je ~0.3 MB). Annahme Hauptlauf.</summary>
    const long MaxRueckmeldung = 60L * 1024 * 1024;

    /// <summary>Port des Baustellen-Zugangs aus --baustelle-port oder HLKS_BAUSTELLE_PORT; null = aus.</summary>
    public static int? Port(string[] args)
    {
        int i = Array.IndexOf(args, "--baustelle-port");
        var roh = i >= 0 && i + 1 < args.Length ? args[i + 1] : Environment.GetEnvironmentVariable("HLKS_BAUSTELLE_PORT");
        return int.TryParse(roh, out var p) && p is > 0 and < 65536 ? p : null;
    }

    /// <summary>Auf dem Baustellen-Port nur /baustelle; steht vor allen anderen Middlewares.</summary>
    public static WebApplication UseBaustelleZugang(this WebApplication app, int? port)
    {
        if (port is not { } bp) return app;
        app.Use(async (ctx, weiter) =>
        {
            if (ctx.Connection.LocalPort == bp && !ctx.Request.Path.StartsWithSegments("/baustelle"))
            {
                ctx.Response.StatusCode = 404;
                return;
            }
            await weiter();
        });
        return app;
    }

    /// <summary>IPv4-Adressen dieses Rechners im Netz (für die Meldung des Startskripts).</summary>
    public static List<string> Adressen() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses)
            .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a.Address) && !a.Address.ToString().StartsWith("169.254."))
            .Select(a => a.Address.ToString()).Distinct().ToList();

    static byte[]? Ressource(string name)
    {
        using var s = typeof(BaustelleExport).Assembly.GetManifestResourceStream("Hlks.Kern.Baustelle." + name);
        if (s is null) return null;
        using var m = new MemoryStream(); s.CopyTo(m); return m.ToArray();
    }

    static string Kopf(string titel) => $"""
        <meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>{WebUtility.HtmlEncode(titel)}</title>
        <link rel="manifest" href="/baustelle/manifest.webmanifest"><link rel="icon" type="image/svg+xml" href="/baustelle/icon.svg">
        <link rel="apple-touch-icon" href="/baustelle/icon.png"><meta name="theme-color" content="#16202e"><meta name="apple-mobile-web-app-title" content="KICAD Baustelle">
        """;

    static IResult Html(string html, int code = 200) => Results.Text(html, "text/html; charset=utf-8", Encoding.UTF8, code);

    /// <summary>Webseiten in ausgabe\baustelle, neueste zuerst.</summary>
    static List<FileInfo> Webseiten(Projekt p) =>
        p.Speicher is null ? [] : new DirectoryInfo(Path.Combine(p.Speicher.Ort, "ausgabe", "baustelle")) is { Exists: true } d
            ? d.GetFiles("*.html").OrderByDescending(f => f.LastWriteTimeUtc).ToList() : [];

    public static string Eingang(Projekt p) => Path.Combine(p.Speicher?.Ort ?? throw new HlksFehler("Projekt ohne Ablage."), "eingang", "baustelle");

    public static WebApplication MapBaustelleZugang(this WebApplication app, ProjektDienst dienst, int? port)
    {
        app.MapGet("/baustelle/", async () =>
        {
            string titel, inhalt;
            try
            {
                (titel, inhalt) = await dienst.Mit(p =>
                {
                    var liste = Webseiten(p);
                    var zeilen = liste.Select(f => $"<li><a href=\"/baustelle/seite/{Uri.EscapeDataString(f.Name)}\">{WebUtility.HtmlEncode(Path.GetFileNameWithoutExtension(f.Name))}</a>" +
                        $"<span>{f.LastWriteTime:dd.MM.yyyy HH:mm} · {f.Length / 1024.0 / 1024.0:0.0} MB</span></li>");
                    return (p.Kopf.Name, liste.Count == 0
                        ? "<p>Noch keine Webseite erzeugt. In KICAD: Datei › Ausgabe › «Baustelle (Webseite)».</p>"
                        : $"<ul>{string.Concat(zeilen)}</ul>");
                });
            }
            catch (HlksFehler f) { (titel, inhalt) = ("KICAD", $"<p>{WebUtility.HtmlEncode(f.Message)}</p>"); }
            return Html($$"""
                <!doctype html><html lang="de-CH"><head>{{Kopf("KICAD Baustelle")}}
                <style>body{font:16px system-ui,sans-serif;margin:0;background:#f4f5f7;color:#16202e}header{background:#16202e;color:#fff;padding:14px 18px;display:flex;gap:12px;align-items:center}
                header img{width:40px;height:40px}h1{font-size:19px;margin:0}main{padding:14px 18px;max-width:760px}ul{list-style:none;padding:0;margin:0}
                li{background:#fff;border:1px solid #d9dde3;border-radius:6px;margin:0 0 8px;padding:12px 14px;display:flex;flex-wrap:wrap;justify-content:space-between;gap:6px}
                a{color:#1d5fa8;font-weight:600;text-decoration:none;word-break:break-all}span,.leise{color:#667;font-size:14px}</style></head>
                <body><header><img src="/baustelle/icon.svg" alt=""><h1>KICAD Baustelle · {{WebUtility.HtmlEncode(titel)}}</h1></header><main>
                <p class="leise">Webseiten für die Baustelle (neueste zuerst). Zum Startbildschirm: im Browsermenü «Zum Startbildschirm hinzufügen» bzw. «Teilen › Zum Home-Bildschirm».</p>
                {{inhalt}}</main></body></html>
                """);
        });
        app.MapGet("/baustelle/seite/{datei}", (string datei) => Gesichert(async () =>
        {
            var (pfad, fehler, code) = await dienst.Mit(p => BaustelleEndpunkte.Erlaubt(p, Path.GetFileName(datei)));
            if (pfad is null) return Html($"<!doctype html><meta charset=utf-8><p>{WebUtility.HtmlEncode(fehler)}</p><p><a href=\"/baustelle/\">Zur Übersicht</a></p>", code);
            var html = await File.ReadAllTextAsync(pfad);
            int k = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
            if (k > 0) html = html.Insert(k, "<link rel=\"manifest\" href=\"/baustelle/manifest.webmanifest\"><meta name=\"kicad-baustelle-server\" content=\"1\">\n");
            return Html(html);
        }));
        app.MapGet("/baustelle/manifest.webmanifest", () => Results.Text(new JsonObject
        {
            ["name"] = "KICAD Baustelle", ["short_name"] = "Baustelle", ["start_url"] = "/baustelle/", ["scope"] = "/baustelle/", ["display"] = "standalone",
            ["background_color"] = "#16202e", ["theme_color"] = "#16202e", ["lang"] = "de-CH",
            ["icons"] = new JsonArray(
                new JsonObject { ["src"] = "/baustelle/icon.png", ["sizes"] = "180x180", ["type"] = "image/png" },
                new JsonObject { ["src"] = "/baustelle/icon.svg", ["sizes"] = "any", ["type"] = "image/svg+xml" }),
        }.ToJsonString(), "application/manifest+json; charset=utf-8"));
        app.MapGet("/baustelle/icon.png", () => Ressource("icon.png") is { } b ? Results.File(b, "image/png") : Results.NotFound());
        app.MapGet("/baustelle/icon.svg", () => Ressource("icon.svg") is { } b ? Results.File(b, "image/svg+xml") : Results.NotFound());

        // Parameter HttpRequest statt HttpContext: ein Lambda (HttpContext) → Task gilt als RequestDelegate, das IResult ginge verloren (leere 200)
        app.MapPost("/baustelle/rueckmeldung", (HttpRequest anfrage) => Gesichert(async () =>
        {
            // nur über den Baustellen-Zugang: auf localhost gilt der Herkunftsschutz von /api nicht, eine fremde Seite im Browser des PCs soll nichts ablegen
            var ctx = anfrage.HttpContext;
            if (port is not { } bp || ctx.Connection.LocalPort != bp)
                return Antwort(new { fehler = "Rückmeldungen nur über den Baustellen-Zugang.", vorschlag = "Webseite über den Testserver öffnen (werkzeuge\\baustelle_testserver.ps1)." }, 403);
            if (ctx.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } f) f.MaxRequestBodySize = MaxRueckmeldung;
            JsonObject r;
            try { r = await JsonNode.ParseAsync(ctx.Request.Body) as JsonObject ?? throw new HlksFehler("Keine Rückmeldung."); }
            catch (System.Text.Json.JsonException) { return Antwort(new { fehler = "Die Rückmeldung ist kein gültiges JSON." }, 400); }
            if ((string?)r["format"] != BaustelleRueckmeldung.Format || r["meldungen"] is not JsonArray m)
                return Antwort(new { fehler = "Das ist keine Rückmeldung der Baustellen-Webseite." }, 400);
            var person = ((string?)r["person"] ?? "").Trim();
            if (person.Length == 0) return Antwort(new { fehler = "Name fehlt.", vorschlag = "Im Reiter Meldungen den Namen eintragen." }, 400);
            var datei = await dienst.Mit(p =>
            {
                BaustelleRueckmeldung.Lesen(p, r);   // wirft, wenn die Meldungen nicht lesbar sind
                var ordner = Eingang(p);
                Directory.CreateDirectory(ordner);
                var name = $"Rueckmeldung_{DateTime.Now:yyyyMMdd-HHmmss}_{string.Concat(person.Select(c => char.IsLetterOrDigit(c) ? c : '_'))}.json";
                var pfad = Path.Combine(ordner, name);
                File.WriteAllText(pfad, r.ToJsonString(), new UTF8Encoding(false));
                return name;
            });
            return Antwort(new { ok = true, datei, meldungen = m.Count });
        }));
        return app;
    }
}
