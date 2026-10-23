// Quest App P4b (Modul XR, Schnittstelle für Fachmodule): die Ebenen vom PC laden (GET /api/xr/ebenen, dann je Ebene GET /api/xr/ebene/{name} mit If-None-Match)
// und für den Start ohne PC ablegen. Format: Hlks.Xr.Ebenen.XrEbene.
//
// Ablage (im privaten Speicher der App): <ordner>/ebenen/<projekt>/<name>.json  { etag, zeit, text }. Namen aus dem Netz gehen nur geprüft in Dateinamen
// (Projekt: Hash; Name: Muster der Ebene). Kennt KICAD die Ebenen nicht (404, ältere Fassung), bleibt die Liste leer – mit Hinweis, ohne Fehler.

using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hlks.Xr.Ebenen;

namespace Hlks.Xr.Netz;

/// <summary>Antwort auf einen GET mit Text: Status (200, 304, 404 …), Text (bei 200) und ETag.</summary>
public sealed record TextAntwort(int Status, string? Text, string? ETag);

/// <summary>Woher die App die Ebenen holt (Netzzugang mit Schlüssel; im Test auch localhost).</summary>
public interface IEbenenQuelle
{
    /// <summary>GET pfad (relativ, z. B. «/api/xr/ebenen»); etag = If-None-Match. Wirft bei Netzfehlern HttpRequestException bzw. OperationCanceledException.</summary>
    Task<TextAntwort> Text(string pfad, string? etag, CancellationToken abbruch);
}

/// <summary>Geladene Ebenen (in der Reihenfolge der Liste), Hinweise und woher sie kamen («pc», «zwischenspeicher»).</summary>
public sealed record EbenenStand(IReadOnlyList<XrEbene> Ebenen, IReadOnlyList<string> Hinweise, string Quelle);

/// <summary>Die zuletzt geladenen Ebenen je Projekt (Start ohne PC).</summary>
public sealed class EbenenAblage(string ordner)
{
    string Ordner(string projekt) => Path.Combine(ordner, "ebenen", Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(projekt)))[..24]);

    public void Speichern(string projekt, XrEbene e, string text, string? etag)
    {
        if (!XrEbeneFormat.NameMuster.IsMatch(e.Name)) return;
        var o = Ordner(projekt);
        Directory.CreateDirectory(o);
        var ziel = Path.Combine(o, e.Name + ".json");
        var tmp = ziel + ".tmp";
        File.WriteAllText(tmp, new JsonObject { ["etag"] = etag, ["zeit"] = DateTimeOffset.UtcNow.ToString("o"), ["text"] = text }.ToJsonString(), new UTF8Encoding(false));
        File.Move(tmp, ziel, overwrite: true);
    }

    /// <summary>Gespeicherte Ebene (Text und ETag) oder null.</summary>
    public (string Text, string? ETag)? Lesen(string projekt, string name)
    {
        if (!XrEbeneFormat.NameMuster.IsMatch(name)) return null;
        var datei = Path.Combine(Ordner(projekt), name + ".json");
        try
        {
            if (!File.Exists(datei)) return null;
            var o = JsonNode.Parse(File.ReadAllText(datei)) as JsonObject;
            return o?["text"] is JsonValue t && t.TryGetValue<string>(out var text) ? (text, o["etag"] is JsonValue ev && ev.TryGetValue<string>(out var etag) ? etag : null) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }

    /// <summary>Namen der gespeicherten Ebenen eines Projekts.</summary>
    public IReadOnlyList<string> Namen(string projekt)
    {
        var o = Ordner(projekt);
        if (!Directory.Exists(o)) return [];
        return [.. Directory.GetFiles(o, "*.json").Select(f => Path.GetFileNameWithoutExtension(f)).Where(n => XrEbeneFormat.NameMuster.IsMatch(n)).Order(StringComparer.Ordinal)];
    }

    /// <summary>Entfernt Ebenen, die der PC nicht mehr anbietet.</summary>
    public void Behalten(string projekt, IEnumerable<string> namen)
    {
        var bleiben = namen.ToHashSet(StringComparer.Ordinal);
        foreach (var n in Namen(projekt).Where(n => !bleiben.Contains(n)))
            try { File.Delete(Path.Combine(Ordner(projekt), n + ".json")); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}

public static class EbenenLader
{
    public const string ListePfad = "/api/xr/ebenen";

    /// <summary>
    /// Lädt die Liste und jede Ebene (bedingt mit dem gespeicherten ETag; 304 = die gespeicherte gilt). Fehlt der Endpunkt (404), ist die Liste leer (Hinweis).
    /// Fehler einzelner Ebenen stehen in den Hinweisen; dann gilt die gespeicherte Fassung, wenn es eine gibt. Netzfehler bei der Liste werfen.
    /// </summary>
    public static async Task<EbenenStand> VomPc(IEbenenQuelle q, string projekt, EbenenAblage? ablage, CancellationToken abbruch = default)
    {
        var hinweise = new List<string>();
        var a = await q.Text(ListePfad, null, abbruch).ConfigureAwait(false);
        if (a.Status == (int)HttpStatusCode.NotFound) return new EbenenStand([], ["KICAD am PC kennt noch keine Ebenen für die App (älterer Stand)."], ModellStand.VomPc);
        if (a.Status != (int)HttpStatusCode.OK || a.Text is null) throw new XrNetzFehler($"Die Liste der Ebenen kam nicht ({a.Status}).", ModellHttp.Vorschlag((HttpStatusCode)a.Status));
        var liste = EbenenListe.Lesen(a.Text);
        var ebenen = new List<XrEbene>();
        foreach (var e in liste.Ebenen)
        {
            var gespeichert = ablage?.Lesen(projekt, e.Name);
            try
            {
                var r = await q.Text(e.Url, gespeichert?.ETag, abbruch).ConfigureAwait(false);
                if (r.Status == (int)HttpStatusCode.NotModified && gespeichert is { } g) { ebenen.Add(XrEbene.Lesen(g.Text)); continue; }
                if (r.Status != (int)HttpStatusCode.OK || r.Text is null) throw new XrNetzFehler($"Ebene {e.Name}: Antwort {r.Status}.");
                var ebene = XrEbene.Lesen(r.Text);
                ebenen.Add(ebene);
                hinweise.AddRange(ebene.Hinweise);
                try { ablage?.Speichern(projekt, ebene, r.Text, r.ETag); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { hinweise.Add($"Ebene {e.Name} nicht abgelegt: {ex.Message}"); }
            }
            catch (XrNetzFehler f)
            {
                hinweise.Add(f.Message);
                if (gespeichert is { } g2) try { ebenen.Add(XrEbene.Lesen(g2.Text)); } catch (XrNetzFehler) { }
            }
        }
        ablage?.Behalten(projekt, liste.Ebenen.Select(x => x.Name));
        return new EbenenStand(ebenen, hinweise, ModellStand.VomPc);
    }

    /// <summary>Start ohne PC: die abgelegten Ebenen des Projekts.</summary>
    public static EbenenStand AusAblage(EbenenAblage ablage, string projekt)
    {
        var ebenen = new List<XrEbene>();
        var hinweise = new List<string>();
        foreach (var n in ablage.Namen(projekt))
        {
            if (ablage.Lesen(projekt, n) is not { } g) continue;
            try { ebenen.Add(XrEbene.Lesen(g.Text)); }
            catch (XrNetzFehler f) { hinweise.Add(f.Message); }
        }
        return new EbenenStand(ebenen, hinweise, ModellStand.AusZwischenspeicher);
    }
}
