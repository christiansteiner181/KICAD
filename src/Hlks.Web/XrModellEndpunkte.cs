// Quest App (Modul XR, P2): das Modell als GLB je Geschoss für die native App – dünne Hülle über Hlks.Kern.XrModell; in Program.cs hängt nur
// «app.MapXrModell(dienst);» sie ein.
//
//   GET /api/xr/geschosse                 { fassung, projekt, bezugspunktMm: [x, y, z], achsen, geschosse: [{ id, kennzeichen, name, koteMm, hoeheMm,
//                                           modell: { schluessel, elemente, url }, unterlage: { schluessel, produkte, dreiecke, url } | null }], hinweise }
//   GET /api/xr/geschoss/{id}.glb?teil=modell|unterlage
//                                         GLB (model/gltf-binary) mit ETag = "<schluessel>"; If-None-Match mit diesem ETag → 304 ohne Inhalt.
//                                         teil fehlt = modell. Fehler: 400 (Teil unbekannt), 404 (Geschoss unbekannt, keine Unterlage), 409 (kein Projekt).
//
// Erfasst wird unter der Sperre des Projekts (Grafik.Koerper, wie /api/koerper); Köpfe der Netzdateien lesen, Zerlegen und Schreiben laufen danach ohne
// Sperre, damit andere Anfragen nicht warten. Die fertigen GLB bleiben je Schlüssel im Speicher (höchstens 256 MB, die zuletzt benutzten), gleichzeitige
// Anfragen für denselben Schlüssel rechnen nur einmal.
//
// Einordnung: reine Rechner-Funktion – Datenlieferung an ein Gerät des Menschen, nur lesend, kein Kern-Befehl, keine Kern-Abfrage, kein Paket; für die KI nicht
// nötig und nicht freigegeben (KiEinordnung.RechnerFunktionen «xr_modell», kein MCP-Werkzeug). Herkunftsschutz wie alle /api (nur localhost, nur GET).
// Zugang im Büronetz (P3-Nachtrag, 23.10.2026): beide Endpunkte tragen die Marke .XrImNetz() – die Brille holt sie über HTTPS mit ihrem Schlüssel (XrZugang.cs);
// dort werden die GLB mit Brotli bzw. gzip komprimiert, wenn die App es anbietet (Accept-Encoding). Auf dem localhost-Server gilt die Komprimierung aus
// Program.cs (gzip, nur für Anfragen von einem anderen Rechner).

using Hlks.Kern;
using Microsoft.Net.Http.Headers;
using static Hlks.Web.EndpunktHilfe;

namespace Hlks.Web;

/// <summary>Zwischenspeicher der fertigen GLB je Schlüssel (Projekt, Geschoss, Teil, Versionsschlüssel), begrenzt auf eine Gesamtgrösse.</summary>
public sealed class XrGlbSpeicher(long grenzeBytes)
{
    sealed class Eintrag { public required Lazy<Task<byte[]>> Daten; public long Bytes; public long Benutzt; }
    readonly Dictionary<string, Eintrag> eintraege = new(StringComparer.Ordinal);
    readonly object sperre = new();
    long zaehler;

    public long GrenzeBytes { get; } = grenzeBytes;
    public int Anzahl { get { lock (sperre) return eintraege.Count; } }
    /// <summary>Wie oft wirklich erzeugt wurde (Tests).</summary>
    public int Erzeugt => erzeugt;
    int erzeugt;

    public async Task<byte[]> Holen(string schluessel, Func<byte[]> erzeugen)
    {
        Eintrag e;
        lock (sperre)
        {
            if (!eintraege.TryGetValue(schluessel, out e!))
            {
                e = new Eintrag { Daten = new Lazy<Task<byte[]>>(() => Task.Run(() => { Interlocked.Increment(ref erzeugt); return erzeugen(); })) };
                eintraege[schluessel] = e;
            }
            e.Benutzt = ++zaehler;
        }
        byte[] daten;
        try { daten = await e.Daten.Value; }
        catch
        {
            lock (sperre) if (eintraege.TryGetValue(schluessel, out var x) && x == e) eintraege.Remove(schluessel);   // Fehler nicht merken
            throw;
        }
        lock (sperre)
        {
            e.Bytes = daten.LongLength;
            long summe = eintraege.Values.Sum(x => x.Bytes);
            foreach (var (k, alt) in eintraege.Where(x => x.Value != e && x.Value.Bytes > 0).OrderBy(x => x.Value.Benutzt).ToList())
            {
                if (summe <= GrenzeBytes) break;
                eintraege.Remove(k);
                summe -= alt.Bytes;
            }
        }
        return daten;
    }
}

public static class XrModellEndpunkte
{
    public const string Medientyp = "model/gltf-binary";
    /// <summary>Grenze des GLB-Zwischenspeichers (Annahme P2: ein grosses Projekt mit IFC-Unterlage hat einige zehn MB je Geschoss).</summary>
    public const long SpeicherGrenze = 256L * 1024 * 1024;

    public static string Url(string geschossId, string teil) => $"/api/xr/geschoss/{Uri.EscapeDataString(geschossId)}.glb?teil={teil}";

    public static WebApplication MapXrModell(this WebApplication app, ProjektDienst dienst)
    {
        var speicher = new XrGlbSpeicher(SpeicherGrenze);
        var g = app.MapGroup("/api/xr");

        g.MapGet("/geschosse", () => Gesichert(async () =>
        {
            var erfassung = await dienst.Mit(XrModell.Erfassen);
            var stand = await Task.Run(() => XrModell.Stand(erfassung));
            return Antwort(XrModell.Liste(stand, Url));
        })).XrImNetz();   // auch im Büronetz (mit Schlüssel)

        g.MapGet("/geschoss/{id}.glb", (string id, string? teil, HttpContext ctx) => Gesichert(async () =>
        {
            teil = string.IsNullOrWhiteSpace(teil) ? XrModell.TeilModell : teil.Trim().ToLowerInvariant();
            if (!XrModell.Teile.Contains(teil))
                return Antwort(new { fehler = $"Teil «{teil}» gibt es nicht.", vorschlag = "teil=modell oder teil=unterlage." }, 400);
            var erfassung = await dienst.Mit(XrModell.Erfassen);
            var stand = await Task.Run(() => XrModell.Stand(erfassung));
            var gs = stand.Geschoss(id);
            if (gs is null)
                return Antwort(new { fehler = $"Geschoss «{id}» gibt es nicht.", vorschlag = "Id aus GET /api/xr/geschosse verwenden." }, 404);
            var schluessel = teil == XrModell.TeilModell ? gs.SchluesselModell : gs.SchluesselUnterlage;
            if (schluessel is null)
                return Antwort(new { fehler = $"Im Geschoss {gs.Geschoss.Kennzeichen} gibt es keine IFC-Unterlage.", vorschlag = "Nur Geschosse mit unterlage ≠ null in GET /api/xr/geschosse haben eine." }, 404);

            var etag = new EntityTagHeaderValue($"\"{schluessel}\"");
            ctx.Response.Headers.ETag = etag.ToString();
            ctx.Response.Headers.CacheControl = "no-cache";
            if (Passt(ctx.Request, etag)) return Results.StatusCode(StatusCodes.Status304NotModified);
            var daten = await speicher.Holen($"{stand.ProjektId}|{gs.Geschoss.Id}|{teil}|{schluessel}", () => XrModell.Glb(stand, gs.Geschoss.Id, teil));
            return Results.Bytes(daten, Medientyp);
        })).XrImNetz();   // auch im Büronetz (mit Schlüssel)
        return app;
    }

    /// <summary>If-None-Match enthält diesen ETag (schwacher Vergleich, wie HTTP es für If-None-Match vorsieht) oder «*».</summary>
    static bool Passt(HttpRequest r, EntityTagHeaderValue etag)
    {
        var kopf = r.Headers.IfNoneMatch;
        if (kopf.Count == 0) return false;
        if (!EntityTagHeaderValue.TryParseList(kopf, out var liste)) return false;
        return liste.Any(x => x.Equals(EntityTagHeaderValue.Any) || x.Compare(etag, useStrongComparison: false));
    }
}
