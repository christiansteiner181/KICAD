// Abbruchprotokoll der Gesten (Modul G, Abnahme Christian 26.10.2026: «speichere weshalb die gesten nach, 1-2 sek. abbrechen»).
// Die Gestenlogik im Browser (js/gesten/gesten-logik.js › faustAbbruch) meldet jedes Ende einer laufenden Faust mit Grund und einigen Massen; die Oberfläche
// (js/oberflaeche/gesten-protokoll.js) schickt es hierher, und der Server hängt eine Zeile an
//   %LOCALAPPDATA%\HLKS_CAD\gesten\abbrueche.jsonl   (Ordner überschreibbar mit HLKS_GESTEN_PROTOKOLL; nur lokal, nie OneDrive)
// DATENSCHUTZ: angenommen wird nur ein kleines Objekt { geste, dauerMs, grund, werte { Name: Zahl } } – keine Punkte, keine Bilder, keine langen Texte.
// Alles andere wird abgelehnt (400), damit über diesen Weg nie Bilddaten auf die Platte kommen.

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Hlks.Web;

public static class GestenProtokoll
{
    public const int KoerperMax = 2048;
    public const int WerteMax = 12;
    static readonly string[] Felder = ["geste", "dauerMs", "grund", "werte"];
    static readonly Regex NameMuster = new(@"^[A-Za-z][A-Za-z0-9_]{0,23}$", RegexOptions.CultureInvariant);
    static readonly Regex TextMuster = new(@"^[\p{L}0-9 _\-]{0,40}$", RegexOptions.CultureInvariant);
    static readonly object Sperre = new();

    /// <summary>Ordner des Protokolls (HLKS_GESTEN_PROTOKOLL oder %LOCALAPPDATA%\HLKS_CAD\gesten).</summary>
    public static string Ordner() =>
        Environment.GetEnvironmentVariable("HLKS_GESTEN_PROTOKOLL") is { Length: > 0 } o ? o
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HLKS_CAD", "gesten");

    /// <summary>Prüft einen Eintrag; Rückgabe (Zeile ohne Zeit, null) oder (null, Meldung).</summary>
    public static (JsonObject? Eintrag, string? Fehler) Pruefen(JsonNode? wert)
    {
        if (wert is not JsonObject o) return (null, "Ein Abbruch ist ein Objekt { geste, dauerMs, grund, werte }.");
        foreach (var (k, _) in o)
            if (!Felder.Contains(k, StringComparer.Ordinal)) return (null, $"Unbekanntes Feld «{(k.Length > 20 ? k[..20] + "…" : k)}».");
        var e = new JsonObject();
        foreach (var f in new[] { "geste", "grund" })
        {
            var n = o[f];
            if (n is null) { e[f] = null; continue; }
            if (n is not JsonValue v || !v.TryGetValue<string>(out var s) || !TextMuster.IsMatch(s)) return (null, $"«{f}» ist ein kurzer Text (höchstens 40 Zeichen).");
            e[f] = s;
        }
        if (o["dauerMs"] is { } d)
        {
            if (d is not JsonValue dv || !dv.TryGetValue<double>(out var ms) || !double.IsFinite(ms) || ms < 0 || ms > 3_600_000) return (null, "«dauerMs» ist eine Zahl von 0 bis 3 600 000.");
            e["dauerMs"] = Math.Round(ms);
        }
        var werte = new JsonObject();
        if (o["werte"] is { } w)
        {
            if (w is not JsonObject wo || wo.Count > WerteMax) return (null, $"«werte» ist ein Objekt mit höchstens {WerteMax} Zahlen.");
            foreach (var (k, n) in wo)
            {
                if (!NameMuster.IsMatch(k)) return (null, "Ungültiger Name in «werte».");
                if (n is null) { werte[k] = null; continue; }
                if (n is not JsonValue nv || !nv.TryGetValue<double>(out var x) || !double.IsFinite(x)) return (null, $"«werte.{k}» ist eine Zahl.");
                werte[k] = Math.Round(x, 4);
            }
        }
        e["werte"] = werte;
        return (e, null);
    }

    /// <summary>Hängt einen geprüften Eintrag mit Zeit an das Protokoll an.</summary>
    public static void Anhaengen(JsonObject eintrag, string? ordner = null, DateTimeOffset? jetzt = null)
    {
        var z = new JsonObject { ["zeit"] = (jetzt ?? DateTimeOffset.Now).ToString("yyyy-MM-ddTHH:mm:ss.fffzzz") };
        foreach (var (k, v) in eintrag) z[k] = v?.DeepClone();
        var dir = ordner ?? Ordner();
        Directory.CreateDirectory(dir);
        lock (Sperre) File.AppendAllText(Path.Combine(dir, "abbrueche.jsonl"), z.ToJsonString() + "\n");
    }

    /// <summary>POST /api/gesten/abbruch: nimmt einen Abbruch an (204) oder lehnt ab (400/413).</summary>
    public static void MapGestenProtokoll(this WebApplication app)
    {
        app.MapPost("/api/gesten/abbruch", async (HttpRequest r) =>
        {
            if (r.ContentLength is > KoerperMax) return Results.StatusCode(413);
            using var lies = new StreamReader(r.Body);
            var puffer = new char[KoerperMax + 1];
            var n = await lies.ReadBlockAsync(puffer, 0, puffer.Length);
            if (n > KoerperMax) return Results.StatusCode(413);
            JsonNode? wert;
            try { wert = JsonNode.Parse(new string(puffer, 0, n)); } catch { return Results.BadRequest(new { fehler = "Kein gültiges JSON." }); }
            var (e, fehler) = Pruefen(wert);
            if (e is null) return Results.BadRequest(new { fehler });
            try { Anhaengen(e); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return Results.StatusCode(503); }
            return Results.NoContent();
        });
    }
}
