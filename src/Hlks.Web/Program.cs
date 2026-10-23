// HLKS-CAD Arbeitsplatz: lokaler Web-Server (nur localhost) mit Oberfläche im Browser.
// Aufruf: Hlks.Web [--port 5130] [--projekt <e_KICAD-Ordner>] [--akteur <name>] [--geraet <name>] [--katalog <ordner>]
// REST ist eine dünne Hülle über der Kern-Tür (Tuer.Abfrage, Tuer.Befehl); keine Fachlogik hier.

using System.Text;
using System.Text.Json.Nodes;
using Hlks.Kern;
using Hlks.Web;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.ResponseCompression;

string? Arg(string name) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }

int port = int.TryParse(Arg("--port"), out var pt) ? pt : 5130;
var einstellungen = new Einstellungen(
    Akteur: Arg("--akteur") ?? Environment.UserName,
    Geraet: Arg("--geraet") ?? Environment.MachineName,
    KatalogOrdner: Arg("--katalog") ?? Katalog.StandardOrdner(),
    StartProjekt: Arg("--projekt"));

// Keine Dateiwächter auf Quell- bzw. OneDrive-Ordnern (Befund R 19.10.2026: PhysicalFilesWatcher.OnRenamed beendete den
// Server, wenn dort Dateien umbenannt wurden – Agenten und OneDrive tun das ständig): Konfiguration ohne Neuladen bei
// Änderung, Inhaltswurzel im Programmordner statt im Arbeitsverzeichnis. Die Oberfläche (WebRootPath) wird nur gelesen.
Environment.SetEnvironmentVariable("DOTNET_hostBuilder__reloadConfigOnChange", "false");
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, WebRootPath = WebRoot(), ContentRootPath = AppContext.BaseDirectory });
builder.WebHost.UseUrls($"http://localhost:{port}");
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.Services.AddSingleton(einstellungen);
builder.Services.AddSingleton<ProjektDienst>();
builder.Services.AddSingleton<KiLaeufe>();   // Modul 3c: höchstens ein KI-Lauf je Projekt (Gespräch der KI-Palette oder Auftrag)
builder.Services.AddHostedService<AbgleichDienst>();
// Modul XR (Quest App P3): zweiter Zugang im Büronetz – eigener HTTPS-Server neben diesem, nur /api/xr, nur gekoppelte Brillen; aus, solange der
// Schalter dieses PCs aus ist (%LOCALAPPDATA%\HLKS_CAD\xr\zugang.json, je PC), und nur im KICAD auf Port 5130 (XrZugang.cs). Dieser Server bleibt
// unverändert nur auf localhost.
builder.Services.AddXrZugang(port);
// Antwortkomprimierung nur für rohe Binärdateien (application/octet-stream = die Netzdatei einer IFC-Unterlage, bis ~35 MB;
// model/gltf-binary = das Modell als GLB je Geschoss, XrModellEndpunkte.cs): gzip, wenn der Browser es annimmt und nicht auf demselben
// Rechner läuft (siehe UseWhen unten). Alles andere (JSON, Oberfläche, Bilder, SSE) bleibt unkomprimiert. Der Zugang im Büronetz
// (XrZugang.cs) hat eine eigene Komprimierung (Brotli und gzip, nur GLB).
builder.Services.AddResponseCompression(o =>
{
    o.MimeTypes = ["application/octet-stream", XrModellEndpunkte.Medientyp];
    o.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = System.IO.Compression.CompressionLevel.Fastest);
var app = builder.Build();
// Schutz gegen Fremdaufrufe (Befund B1 der Sicherheitsprüfung 3c): vor allen Endpunkten unter /api und /hilfe.
app.UseHerkunftsschutz();
// Nur für Anfragen von einem anderen Rechner: lokal kostet gzip mehr Zeit, als es spart (Befund C2 vom 17.10.2026: 0.5–3 s
// Server-Rechenzeit je Netzdatei statt 0.12 s roh); der KICAD-Server läuft im Normalfall auf demselben PC.
app.UseWhen(c => c.Connection.RemoteIpAddress is not { } ip || !System.Net.IPAddress.IsLoopback(ip), a => a.UseResponseCompression());
var dienst = app.Services.GetRequiredService<ProjektDienst>();

if (einstellungen.StartProjekt is { } start)
{
    try { await dienst.Oeffnen(start); }
    catch (HlksFehler f) { Console.Error.WriteLine($"Projekt {start} nicht geöffnet: {f.Message}"); }
}

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions { OnPrepareResponse = c => c.Context.Response.Headers.CacheControl = "no-cache" });

/// Dateiname einer hochgeladenen Unterlage: nur der Name (kein Pfad), ohne ungültige Zeichen, höchstens 150 Zeichen mit Endung.
static string UnterlageName(string? roh)
{
    var n = Path.GetFileName((roh ?? "").Replace('\\', '/'));
    n = new string(n.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray()).Trim().TrimStart('.');
    if (n.Length == 0) throw new HlksFehler("Der Dateiname fehlt.", "Beim Hochladen den Namen mitgeben (?name=…) oder als Formulardatei senden.");
    if (n.Length > 150) { var ext = Path.GetExtension(n); n = n[..(150 - Math.Min(ext.Length, 20))] + (ext.Length <= 20 ? ext : ""); }
    return n;
}

static IResult JsonAntwort(object o) => Results.Text(Hlks.Kern.Json.Text(o), "application/json; charset=utf-8");
static IResult Fehler(HlksFehler f, int code = 400) => Results.Text(Hlks.Kern.Json.Text(new { fehler = f.Message, vorschlag = f.Vorschlag }), "application/json; charset=utf-8", statusCode: code);

async Task<JsonObject> Koerper(HttpRequest r)
{
    using var leser = new StreamReader(r.Body, Encoding.UTF8);
    var text = await leser.ReadToEndAsync();
    if (string.IsNullOrWhiteSpace(text)) return new JsonObject();
    try { return JsonNode.Parse(text) as JsonObject ?? throw new HlksFehler("Parameter müssen ein JSON-Objekt sein."); }
    catch (System.Text.Json.JsonException ex) { throw new HlksFehler("Parameter sind kein gültiges JSON: " + ex.Message); }
}

async Task<IResult> Sicher(Func<Task<IResult>> arbeit)
{
    try { return await arbeit(); }
    catch (HlksFehler f) { return Fehler(f, f.Message.StartsWith("Es ist kein Projekt offen") ? 409 : 400); }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return Fehler(new HlksFehler("Projektablage nicht erreichbar: " + ex.Message, "Netzwerk/VPN prüfen."), 503); }
}

var api = app.MapGroup("/api");

api.MapGet("/status", async () => JsonAntwort(await dienst.Status()));
// /api/zuletzt und die übrigen Endpunkte des Startbildschirms: StartEndpunkte.cs (app.MapStart)
api.MapPost("/projekt/oeffnen", (HttpRequest r) => Sicher(async () =>
{
    var p = await Koerper(r);
    return JsonAntwort(await dienst.Oeffnen(Param.Text(p, "ordner", true)!));
}));
api.MapPost("/projekt/anlegen", (HttpRequest r) => Sicher(async () =>
{
    var p = await Koerper(r);
    return JsonAntwort(await dienst.Anlegen(Param.Text(p, "ordner", true)!, Param.Text(p, "name") ?? "Neues Projekt", Param.Bool(p, "beispiel")));
}));
api.MapPost("/projekt/schliessen", () => Sicher(async () => { await dienst.Schliessen(); return JsonAntwort(await dienst.Status()); }));

api.MapGet("/modell", () => Sicher(async () => JsonAntwort(await dienst.Mit(p => Darstellung.Modell(p)))));
api.MapPost("/elemente", (HttpRequest r) => Sicher(async () =>
{
    var p = await Koerper(r);
    var ids = new List<string>();
    foreach (var x in p["ids"] as JsonArray ?? [])
        ids.Add(x is JsonValue v && v.TryGetValue<string>(out var id) ? id : throw new HlksFehler("«ids» muss eine Liste von Element-Ids (Text) sein."));
    return JsonAntwort(await dienst.Mit(pr => Darstellung.Elemente(pr, ids)));
}));

// Grafik aus dem Kern (Modul 1a): Plangrafik einer Ansicht, Körper für 3D, Planblatt als PDF
api.MapGet("/plangrafik/{ansicht}", (string ansicht) => Sicher(async () => JsonAntwort(await dienst.Mit(p => Grafik.Plan(p, ansicht)))));
api.MapGet("/koerper", (string? ids) => Sicher(async () =>
    JsonAntwort(await dienst.Mit(p => Grafik.Koerper(p, string.IsNullOrWhiteSpace(ids) ? null : ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))))));
api.MapGet("/schraffuren", () => JsonAntwort(Schraffuren.Alle));
api.MapGet("/plan/{planblatt}.pdf", (string planblatt) => Sicher(async () =>
    Results.File(await dienst.Mit(p => Planausgabe.Pdf(p, planblatt)), "application/pdf", $"{planblatt}.pdf")));

// Abfragen in zwei Schritten (Nachtrag 21.10.2026): unter der Sperre nur vorbereiten (leitungsserie: Lesestand), gerechnet wird danach ohne
// Sperre – andere Anfragen warten nicht; bricht der Browser ab (AbortController), endet die Rechnung im Kern (RequestAborted).
api.MapPost("/abfrage/{name}", (string name, HttpRequest r) => Sicher(async () =>
{
    var par = await Koerper(r);
    var abbruch = r.HttpContext.RequestAborted;
    object ergebnis;
    try
    {
        var rechnen = await dienst.Mit(p => Tuer.AbfrageVorbereiten(p, name, par));
        ergebnis = await Task.Run(() => rechnen(abbruch), abbruch);
    }
    catch (OperationCanceledException) when (abbruch.IsCancellationRequested)
    {
        return Fehler(new HlksFehler("Abfrage abgebrochen.", "Neu anfragen, wenn das Ergebnis gebraucht wird."), 499);
    }
    return ergebnis is string text ? Results.Text(text, "text/plain; charset=utf-8") : JsonAntwort(ergebnis);
}));

api.MapPost("/befehl/{name}", (string name, bool? trockenlauf, HttpRequest r) => Sicher(async () =>
{
    var par = await Koerper(r);
    return JsonAntwort(await dienst.Mit(p => Tuer.Befehl(p, name, par, trockenlauf ?? false)));
}));

api.MapPost("/freigeben", () => Sicher(async () => JsonAntwort(new { freigegeben = await dienst.Mit(p => p.Freigeben()) })));

// Unterlagen (Modul 1b, C1): Datei hochladen (roh mit ?name=… oder multipart) in den Ordner unterlagen des offenen Projekts;
// Netz einer IFC-Unterlage roh holen (3D). Die Ablage selbst macht der Kern (Unterlagen.Ablegen: Prüfsumme + Name, nie überschreiben).
const long UnterlageMaxBytes = 500L * 1024 * 1024;

api.MapPost("/unterlagen", (HttpRequest r, string? name) => Sicher(async () =>
{
    var projekt = dienst.Projekt ?? throw new HlksFehler("Es ist kein Projekt offen.", "Projekt öffnen oder anlegen.");
    // Grosse Dateien: Grenze nur für diese Anfrage anheben (Vorgabe von Kestrel: 30 MB)
    if (r.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } grenze) grenze.MaxRequestBodySize = UnterlageMaxBytes;
    var ordner = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "hochladen", Guid.NewGuid().ToString("N"));
    try
    {
        string? roh = name;
        Stream? quelle = null;
        if (r.HasFormContentType)
        {
            r.HttpContext.Features.Set<IFormFeature>(new FormFeature(r, new FormOptions { MultipartBodyLengthLimit = UnterlageMaxBytes }));
            var form = await r.ReadFormAsync();
            var datei = form.Files.FirstOrDefault() ?? throw new HlksFehler("Im Formular fehlt die Datei.", "Eine Datei als Formularfeld senden (multipart/form-data).");
            roh ??= datei.FileName;
            quelle = datei.OpenReadStream();
        }
        else
        {
            roh ??= r.Headers["X-Dateiname"].FirstOrDefault() is { Length: > 0 } kopf ? Uri.UnescapeDataString(kopf) : null;
            quelle = r.Body;
        }
        var dateiname = UnterlageName(roh);
        Directory.CreateDirectory(ordner);
        var temp = Path.Combine(ordner, dateiname);
        long bytes;
        await using (var ziel = File.Create(temp)) { await quelle.CopyToAsync(ziel); bytes = ziel.Length; }
        if (bytes == 0) throw new HlksFehler($"Die Datei «{dateiname}» ist leer.", "Die Datei prüfen und erneut hochladen.");
        var (ablage, stand) = Unterlagen.Ablegen(projekt, temp);
        return JsonAntwort(new { name = ablage, stand, bytes });
    }
    catch (BadHttpRequestException ex) { throw new HlksFehler("Die Datei ist zu gross oder die Übertragung brach ab: " + ex.Message, "Grösse bis 500 MB; Verbindung prüfen."); }
    catch (InvalidDataException ex) { throw new HlksFehler("Das Formular ist nicht lesbar: " + ex.Message, "Datei roh (?name=…) oder als multipart/form-data senden."); }
    catch (Exception ex) when (ex is not (HlksFehler or IOException or UnauthorizedAccessException or OperationCanceledException))
    { throw new HlksFehler("Die Datei konnte nicht gespeichert werden: " + ex.Message, "Erneut versuchen; bleibt es so, Dateiname und Grösse prüfen."); }
    finally { try { if (Directory.Exists(ordner)) Directory.Delete(ordner, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
}));

api.MapGet("/unterlagen/{id}/netz", (string id) => Sicher(async () =>
{
    var (pfad, grund) = await dienst.Mit<(string?, string?)>(p =>
    {
        var e = p.Modell.Finde(id);
        if (e is null || e.Klasse != "Unterlage") return (null, $"Unterlage «{id}» gibt es nicht.");
        var netz = e.Text("netz");
        if (string.IsNullOrWhiteSpace(netz)) return (null, $"Die Unterlage {e.Kennzeichen} hat kein Netz (nur IFC-Unterlagen mit Hintergrund haben eins).");
        var datei = Path.Combine(Unterlagen.Ordner(p), Path.GetFileName(netz));
        return File.Exists(datei) ? (datei, null) : (null, $"Die Netzdatei {netz} der Unterlage {e.Kennzeichen} fehlt im Ordner unterlagen.");
    });
    return pfad is null ? Results.Text(grund!, "text/plain; charset=utf-8", statusCode: 404) : Results.File(pfad, "application/octet-stream", enableRangeProcessing: true);
}));

// Bild einer PDF- oder Bild-Unterlage (Plangrafik, Primitiv «bild»): die Seite als PNG bzw. das Bild selbst, vom Kern (UnterlagenBild.Pfad).
// Der Browser fragt bei jedem Zeichnen nach (no-cache); mit Änderungsdatum antwortet der Server dann mit 304.
api.MapGet("/unterlagen/{id}/bild", (string id, HttpContext ctx) => Sicher(async () =>
{
    var (pfad, grund) = await dienst.Mit<(string?, string?)>(p =>
    {
        var e = p.Modell.Finde(id);
        if (e is null || e.Klasse != "Unterlage") return (null, $"Unterlage «{id}» gibt es nicht.");
        if (e.Text("art") is not ("pdf" or "bild")) return (null, $"Die Unterlage {e.Kennzeichen} hat kein Bild (nur PDF- und Bild-Unterlagen).");
        try
        {
            var datei = UnterlagenBild.Pfad(p, e);
            return File.Exists(datei) ? (datei, null) : (null, $"Die Bilddatei der Unterlage {e.Kennzeichen} fehlt im Ordner unterlagen.");
        }
        catch (Exception ex) when (ex is HlksFehler or IOException or UnauthorizedAccessException) { return (null, $"Das Bild der Unterlage {e.Kennzeichen} ist nicht lesbar: {ex.Message}"); }
    });
    if (pfad is null) return Results.Text(grund!, "text/plain; charset=utf-8", statusCode: 404);
    string typ;
    try { typ = UnterlagenBild.Medientyp(pfad); }
    catch (Exception ex) when (ex is HlksFehler or IOException or UnauthorizedAccessException) { return Results.Text($"Die Bilddatei ist nicht lesbar: {ex.Message}", "text/plain; charset=utf-8", statusCode: 404); }
    ctx.Response.Headers.CacheControl = "no-cache";
    return Results.File(pfad, typ, lastModified: new DateTimeOffset(File.GetLastWriteTimeUtc(pfad)));
}));

api.MapGet("/ereignisse", async (HttpContext ctx) =>
{
    ctx.Response.Headers.ContentType = "text/event-stream";
    ctx.Response.Headers.CacheControl = "no-cache";
    var leser = dienst.Abonnieren(ctx.RequestAborted);
    await ctx.Response.WriteAsync("event: hallo\ndata: {}\n\n", ctx.RequestAborted);
    await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
    try
    {
        Task<bool>? warten = null;   // über die 15-s-Runden hinweg dasselbe Warten, sonst stauen sich hängende Wartende
        while (!ctx.RequestAborted.IsCancellationRequested)
        {
            warten ??= leser.WaitToReadAsync(ctx.RequestAborted).AsTask();
            var fertig = await Task.WhenAny(warten, Task.Delay(TimeSpan.FromSeconds(15), ctx.RequestAborted));
            if (fertig != warten) { await ctx.Response.WriteAsync(": lebt\n\n", ctx.RequestAborted); await ctx.Response.Body.FlushAsync(ctx.RequestAborted); continue; }
            var mehr = await warten;
            warten = null;
            if (!mehr) break;
            while (leser.TryRead(out var text))
                await ctx.Response.WriteAsync($"data: {text}\n\n", ctx.RequestAborted);
            await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
        }
    }
    catch (OperationCanceledException) { }
});

app.Lifetime.ApplicationStopping.Register(() => dienst.Schliessen().GetAwaiter().GetResult());
app.MapStart(dienst);   // Startbildschirm (S1): StartEndpunkte.cs
app.MapBenutzer(dienst);   // Benutzereinstellungen (G1): BenutzerEndpunkte.cs (z. B. primäre Gliederung des Projektbrowsers)
app.MapVrSeite();   // VR-Startseite /vr (VrSeite.cs; Modul XR)
var questStarter = QuestEndpunkte.StarterErzeugen(app, dienst);   // gemeinsame Sperre für Quest verbinden und Autostart (QuestStarter.cs)
app.MapVrAuftrag(dienst, questStarter);   // VR-Auftrag vom PC an die Brille (VrAuftrag.cs; Modul XR)
app.MapAnleitung(dienst);   // Bedienungsanleitung (AnleitungEndpunkte.cs): /hilfe/ und Prüfstand /api/hilfe/pruefung
app.MapAuftraege(dienst);   // KI-Aufträge (Modul 2, B5): AuftragEndpunkte.cs; hinter dem Schliessen angemeldet, damit beim Beenden zuerst die Läufe enden
app.MapGespraeche(dienst);   // KI-Palette «Claude» (Modul 3c, Teil K): GespraechEndpunkte.cs – Gespräche, Zurücknehmen, Normensuche
app.MapQuest(dienst, questStarter);   // Quest verbinden (Modul XR): QuestEndpunkte.cs – adb reverse für den Browser der Quest; reine Rechner-Funktion, nicht für die KI
app.MapIfcExport(dienst);   // IFC-Export mit IDS und Bericht (Modul 7 Teil B2): IfcExportEndpunkte.cs – POST /api/ifc/export, Download aus ausgabe\ifc
app.MapXr();   // Quest App P3: /api/xr – Zugang, Kopplung, Live-Kanal (XrEndpunkte.cs); im Büronetz nur Endpunkte mit .XrImNetz()
app.MapXrModell(dienst);   // Quest App (Modul XR, P2): XrModellEndpunkte.cs – GLB je Geschoss (/api/xr/geschosse, /api/xr/geschoss/{id}.glb); reine Rechner-Funktion, nicht für die KI

Console.WriteLine($"HLKS-CAD läuft auf http://localhost:{port}  (Akteur {einstellungen.Akteur}@{einstellungen.Geraet})");
await app.RunAsync();

// Oberfläche: HLKS_WEBROOT, sonst der Quellordner (Entwicklung, Änderungen ohne Neubau), sonst die Kopie neben dem Programm
static string WebRoot()
{
    var env = Environment.GetEnvironmentVariable("HLKS_WEBROOT");
    if (!string.IsNullOrEmpty(env) && File.Exists(Path.Combine(env, "index.html"))) return env;
    var quelle = typeof(Program).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
        .Cast<System.Reflection.AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "QuellWebRoot")?.Value;
    if (quelle is not null && File.Exists(Path.Combine(quelle, "index.html"))) return quelle;
    return Path.Combine(AppContext.BaseDirectory, "wwwroot");
}

public partial class Program { }
