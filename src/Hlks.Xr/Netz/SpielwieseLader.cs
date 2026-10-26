// XR-Labor Spielwiese S1: den Katalog der Spielwiese vom Labor-PC holen (GET /api/xr/labor/spielwiese, ETag/304 wie der Katalog des Labors, LaborKatalogLader) und
// für den Start ohne PC ablegen. Format: Hlks.Xr.Labor.SpielwieseKatalog; Server: src/Hlks.Labor/SpielwieseEndpunkt.cs.
//
// Ablage (im privaten Speicher der App, je Werkzeug): <ordner>/labor/spielwiese.json  { etag, zeit, text }.
// Reihenfolge, was gilt (Auftrag S1: «ohne Verbindung die zuletzt geholte Fassung, sonst eine eingebaute Vorgabe»):
//   - 200 mit gültigem Katalog: dieser (abgelegt) – «pc»;
//   - 304 und gültige Ablage: die Ablage – «pc, unverändert»;
//   - sonst (Netzfehler, 404 älterer Labor-PC, 409 ungültig am PC, andere Antworten, unbrauchbarer Text): die Ablage – «zwischenspeicher», mit Hinweis;
//   - ohne brauchbare Ablage: die eingebaute Vorgabe der App (kataloge/labor_spielwiese.json beim Bau) – «vorgabe»; ist auch sie unbrauchbar: keiner.

using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hlks.Xr.Labor;

namespace Hlks.Xr.Netz;

/// <summary>Der zuletzt geladene Katalog der Spielwiese (Start ohne PC).</summary>
public sealed class SpielwieseAblage(string ordner)
{
    public string Datei => Path.Combine(ordner, "labor", "spielwiese.json");

    public void Speichern(string text, string? etag)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Datei)!);
        var tmp = Datei + ".tmp";
        File.WriteAllText(tmp, new JsonObject { ["etag"] = etag, ["zeit"] = DateTimeOffset.UtcNow.ToString("o"), ["text"] = text }.ToJsonString(), new UTF8Encoding(false));
        File.Move(tmp, Datei, overwrite: true);
    }

    /// <summary>Gespeicherter Katalog (Text und ETag) oder null.</summary>
    public (string Text, string? ETag)? Lesen()
    {
        try
        {
            if (!File.Exists(Datei)) return null;
            var o = JsonNode.Parse(File.ReadAllText(Datei)) as JsonObject;
            return o?["text"] is JsonValue t && t.TryGetValue<string>(out var text) ? (text, o["etag"] is JsonValue ev && ev.TryGetValue<string>(out var etag) ? etag : null) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }
}

/// <summary>Geltender Katalog der Spielwiese (null = keiner), woher und Hinweise.</summary>
public sealed record SpielwieseStand(SpielwieseKatalog? Katalog, string Quelle, IReadOnlyList<string> Hinweise)
{
    public const string VomPc = "pc", Unveraendert = "pc, unverändert", AusAblage = "zwischenspeicher", AusVorgabe = "vorgabe", Keiner = "keiner";
}

public static class SpielwieseLader
{
    public const string Pfad = "/api/xr/labor/spielwiese";

    /// <summary>Ablage, sonst die eingebaute Vorgabe, sonst keiner (siehe Dateikopf).</summary>
    static SpielwieseStand Ersatz(SpielwieseAblage? ablage, string? vorgabe, IReadOnlyCollection<string>? farben, List<string> hinweise)
    {
        if (ablage?.Lesen() is { } g)
        {
            try { return new SpielwieseStand(SpielwieseKatalog.Lesen(g.Text, farben), SpielwieseStand.AusAblage, hinweise); }
            catch (XrNetzFehler f) { hinweise.Add("Abgelegter Katalog der Spielwiese unbrauchbar: " + f.Message); }
        }
        if (vorgabe is not null)
        {
            try { return new SpielwieseStand(SpielwieseKatalog.Lesen(vorgabe, farben), SpielwieseStand.AusVorgabe, hinweise); }
            catch (XrNetzFehler f) { hinweise.Add("Eingebaute Spielwiese unbrauchbar: " + f.Message); }
        }
        return new SpielwieseStand(null, SpielwieseStand.Keiner, hinweise);
    }

    /// <summary>Katalog vom Labor-PC (siehe Dateikopf). Wirft nicht: Netzfehler und ungültige Antworten ergeben Ablage bzw. Vorgabe mit Hinweis.</summary>
    public static async Task<SpielwieseStand> VomPc(IEbenenQuelle q, SpielwieseAblage? ablage, string? vorgabe, IReadOnlyCollection<string>? farben = null,
        CancellationToken abbruch = default)
    {
        var hinweise = new List<string>();
        var gespeichert = ablage?.Lesen();
        TextAntwort a;
        try { a = await q.Text(Pfad, gespeichert?.ETag, abbruch).ConfigureAwait(false); }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException && !abbruch.IsCancellationRequested)
        {
            hinweise.Add("Spielwiese nicht vom PC geladen: " + ex.Message);
            return Ersatz(ablage, vorgabe, farben, hinweise);
        }
        if (a.Status == (int)HttpStatusCode.NotModified && gespeichert is { } g)
        {
            try { return new SpielwieseStand(SpielwieseKatalog.Lesen(g.Text, farben), SpielwieseStand.Unveraendert, hinweise); }
            catch (XrNetzFehler f) { hinweise.Add("Abgelegter Katalog der Spielwiese unbrauchbar: " + f.Message); return Ersatz(null, vorgabe, farben, hinweise); }
        }
        if (a.Status == (int)HttpStatusCode.NotFound)
        {
            hinweise.Add("Der Labor-PC kennt die Spielwiese noch nicht (älterer Stand).");
            return Ersatz(ablage, vorgabe, farben, hinweise);
        }
        if (a.Status != (int)HttpStatusCode.OK || a.Text is null)
        {
            hinweise.Add($"Spielwiese: Antwort {a.Status}{(a.Text is { Length: > 0 } t ? " – " + Kurz(t) : "")}.");
            return Ersatz(ablage, vorgabe, farben, hinweise);
        }
        SpielwieseKatalog k;
        try { k = SpielwieseKatalog.Lesen(a.Text, farben); }
        catch (XrNetzFehler f)
        {
            hinweise.Add(f.Message);
            return Ersatz(ablage, vorgabe, farben, hinweise);
        }
        hinweise.AddRange(k.Hinweise);
        try { ablage?.Speichern(a.Text, a.ETag); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { hinweise.Add("Spielwiese nicht abgelegt: " + ex.Message); }
        return new SpielwieseStand(k, SpielwieseStand.VomPc, hinweise);
    }

    /// <summary>Start ohne PC bzw. beim Betreten des Werkzeugs: die Ablage, sonst die Vorgabe.</summary>
    public static SpielwieseStand OhnePc(SpielwieseAblage? ablage, string? vorgabe, IReadOnlyCollection<string>? farben = null) => Ersatz(ablage, vorgabe, farben, []);

    /// <summary>Fehlermeldung des PCs (JSON { fehler }) bzw. der Anfang des Texts.</summary>
    static string Kurz(string text)
    {
        try { if (JsonNode.Parse(text) is JsonObject o && o["fehler"] is JsonValue f && f.TryGetValue<string>(out var s)) return s; }
        catch (JsonException) { }
        return text.Length > 200 ? text[..200] + " …" : text;
    }
}
