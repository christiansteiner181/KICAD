// Quest App P5b (Modul XR, Interface-Labor): den Katalog des Labors vom PC holen (GET /api/xr/labor/katalog, ETag/304 wie die GLB) und für den Start ohne PC
// ablegen. Format: Hlks.Xr.Labor.LaborKatalog; Server: src/Hlks.Web/XrLaborEndpunkte.cs.
//
// Ablage (im privaten Speicher der App): <ordner>/labor/katalog.json  { etag, zeit, text }.
//   - Mit Ablage fragt die App mit If-None-Match; 304 = die Ablage gilt («pc, unverändert»).
//   - 200: lesen; ist der Text unbrauchbar, gilt die Ablage (mit Hinweis); sonst ablegen.
//   - 404 (älteres KICAD ohne Labor): die Ablage, sonst kein Katalog – mit Hinweis, ohne Fehler.
//   - andere Antworten und Netzfehler: die Ablage gilt (mit Hinweis); ohne Ablage kein Katalog (die App nimmt die Vorgaben des Codes).

using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hlks.Xr.Labor;

namespace Hlks.Xr.Netz;

/// <summary>Der zuletzt geladene Katalog des Labors (Start ohne PC).</summary>
public sealed class LaborKatalogAblage(string ordner)
{
    public string Datei => Path.Combine(ordner, "labor", "katalog.json");

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

/// <summary>Geladener Katalog (null = keiner), woher («pc», «pc, unverändert», «zwischenspeicher», «keiner») und Hinweise.</summary>
public sealed record LaborKatalogStand(LaborKatalog? Katalog, string Quelle, IReadOnlyList<string> Hinweise)
{
    public const string VomPc = "pc", Unveraendert = "pc, unverändert", AusAblage = "zwischenspeicher", Keiner = "keiner";
}

public static class LaborKatalogLader
{
    public const string Pfad = "/api/xr/labor/katalog";

    static LaborKatalogStand Abgelegt(LaborKatalogAblage? ablage, List<string> hinweise)
    {
        if (ablage?.Lesen() is { } g)
        {
            try { return new LaborKatalogStand(LaborKatalog.Lesen(g.Text), LaborKatalogStand.AusAblage, hinweise); }
            catch (XrNetzFehler f) { hinweise.Add("Abgelegter Katalog unbrauchbar: " + f.Message); }
        }
        return new LaborKatalogStand(null, LaborKatalogStand.Keiner, hinweise);
    }

    /// <summary>Katalog vom PC (siehe Dateikopf). Wirft nicht: Netzfehler ergeben die Ablage mit Hinweis.</summary>
    public static async Task<LaborKatalogStand> VomPc(IEbenenQuelle q, LaborKatalogAblage? ablage, CancellationToken abbruch = default)
    {
        var hinweise = new List<string>();
        var gespeichert = ablage?.Lesen();
        TextAntwort a;
        try { a = await q.Text(Pfad, gespeichert?.ETag, abbruch).ConfigureAwait(false); }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException && !abbruch.IsCancellationRequested)
        {
            hinweise.Add("Katalog des Labors nicht geladen: " + ex.Message);
            return Abgelegt(ablage, hinweise);
        }
        if (a.Status == (int)HttpStatusCode.NotModified && gespeichert is { } g)
        {
            try { return new LaborKatalogStand(LaborKatalog.Lesen(g.Text), LaborKatalogStand.Unveraendert, hinweise); }
            catch (XrNetzFehler f) { hinweise.Add("Abgelegter Katalog unbrauchbar: " + f.Message); return new LaborKatalogStand(null, LaborKatalogStand.Keiner, hinweise); }
        }
        if (a.Status == (int)HttpStatusCode.NotFound)
        {
            hinweise.Add("KICAD am PC kennt den Katalog des Labors noch nicht (älterer Stand).");
            return Abgelegt(ablage, hinweise);
        }
        if (a.Status != (int)HttpStatusCode.OK || a.Text is null)
        {
            hinweise.Add($"Katalog des Labors: Antwort {a.Status}.");
            return Abgelegt(ablage, hinweise);
        }
        LaborKatalog k;
        try { k = LaborKatalog.Lesen(a.Text); }
        catch (XrNetzFehler f)
        {
            hinweise.Add(f.Message);
            return Abgelegt(ablage, hinweise);
        }
        hinweise.AddRange(k.Hinweise);
        try { ablage?.Speichern(a.Text, a.ETag); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { hinweise.Add("Katalog nicht abgelegt: " + ex.Message); }
        return new LaborKatalogStand(k, LaborKatalogStand.VomPc, hinweise);
    }

    /// <summary>Start ohne PC: der abgelegte Katalog, sonst keiner.</summary>
    public static LaborKatalogStand AusAblage(LaborKatalogAblage ablage) => Abgelegt(ablage, []);
}
