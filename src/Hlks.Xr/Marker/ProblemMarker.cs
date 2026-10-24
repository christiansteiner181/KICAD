// Quest App V1a (Modul XR, XR-E «Problem-Marker mit Nachverfolgung»): «Problem markieren» in der App und auf die Stelle zeigen bzw. tippen. Gespeichert werden Ort
// (KICAD-mm), Blickpunkt (Kopfpose), das betroffene Element (falls getroffen), Zeit und Benutzer; die Notiz ist freiwillig, ein Marker geht auch ohne Beschreibung.
// Vertrag mit KICAD (src/Hlks.Web/XrMarkerEndpunkte.cs; im Kern ein Element der Klasse «ProblemMarker», kataloge/klassen.json):
//   POST /api/xr/marker  { kennung, ort: [x, y, z] (mm, absolut), blick: { kopf: [x, y, z] (mm), richtung: [x, y, z] (Einheit, KICAD-Achsen), modus: begehen |
//                          sitzungsraum, massstab }, element: { id, teil: modell | unterlage, art } | null, geschoss: Id | null, zeit: ISO 8601, notiz: Text | null }
//                        → 201 { marker } bzw. 200 { marker, schon: true }, wenn die Kennung schon angelegt ist (wiederholt gesendet: kein zweiter Marker)
//   GET  /api/xr/marker  → { projekt, marker: [{ id, kennzeichen, kennung, ort, blick, element, geschoss, zeit, benutzer, brille, notiz, status, zustaendig, frist }] }
// Die Kennung erzeugt die App (Guid); ohne Verbindung bleibt der Marker im privaten Speicher (MarkerAblage, höchstens WartendMax) und geht mit der nächsten Verbindung
// (Ergänzung «Brille ohne PC»: «Marker und Notizen von der Baustelle gleicht die App ab, sobald sie wieder mit dem PC verbunden ist»). Die zuletzt geholte Liste
// liegt ebenfalls dort, damit die App die Marker auch ohne PC als Symbole zeigt.
// Am Sitzungstisch gelten Modellkoordinaten: der Blickpunkt ist der Punkt des Modells, über dem der Kopf steht (im Massstab vergrössert, wie die Posen im Kanal).

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hlks.Xr.Netz;

namespace Hlks.Xr.Marker;

/// <summary>Status eines Markers (Klasse ProblemMarker im Kern).</summary>
public static class MarkerStatus
{
    public const string Offen = "offen", Erledigt = "erledigt";
    public static readonly IReadOnlyList<string> Alle = [Offen, Erledigt];
}

/// <summary>Blickpunkt beim Setzen: Kopf (KICAD-mm), Blickrichtung (Einheit, KICAD-Achsen), Modus und Massstab (Nenner; 1 beim Begehen).</summary>
public sealed record MarkerBlick(double[] KopfMm, double[] Richtung, string Modus, double Massstab);

/// <summary>Das betroffene Element: Id (Modell) bzw. GlobalId (IFC-Unterlage), Teil der GLB (modell | unterlage), Ebene bzw. IFC-Entität.</summary>
public sealed record MarkerElement(string Id, string Teil, string? Art);

/// <summary>Ein neuer Marker aus der App (Inhalt von POST /api/xr/marker).</summary>
public sealed record MarkerNeu(string Kennung, double[] OrtMm, MarkerBlick Blick, MarkerElement? Element, string? Geschoss, string Zeit, string? Notiz)
{
    public JsonObject ZuJson() => new()
    {
        ["kennung"] = Kennung,
        ["ort"] = ProblemMarker.Zahlen(OrtMm),
        ["blick"] = new JsonObject
        {
            ["kopf"] = ProblemMarker.Zahlen(Blick.KopfMm), ["richtung"] = ProblemMarker.Zahlen(Blick.Richtung, 4), ["modus"] = Blick.Modus,
            ["massstab"] = Math.Round(Blick.Massstab, 3),
        },
        ["element"] = Element is null ? null : new JsonObject { ["id"] = Element.Id, ["teil"] = Element.Teil, ["art"] = Element.Art },
        ["geschoss"] = Geschoss,
        ["zeit"] = Zeit,
        ["notiz"] = Notiz,
    };
}

/// <summary>Ein Marker, wie ihn KICAD liefert (GET /api/xr/marker).</summary>
public sealed record MarkerEintrag(string Id, string Kennzeichen, string? Kennung, double[] OrtMm, string Status, string? Notiz = null, string? Zustaendig = null,
    string? Frist = null, string? Benutzer = null, string? Zeit = null, string? GeschossId = null, MarkerElement? Element = null, MarkerBlick? Blick = null)
{
    public bool Offen => Status != MarkerStatus.Erledigt;
    /// <summary>Kurzer Text für die App: «PM-3 · Kollision» bzw. «PM-3».</summary>
    public string Titel => string.IsNullOrWhiteSpace(Notiz) ? Kennzeichen : $"{Kennzeichen} · {ProblemMarker.Kuerzen(Notiz, 28)}";
}

public static class ProblemMarker
{
    /// <summary>Längste Notiz (Zeichen), wie der Server sie annimmt.</summary>
    public const int NotizMax = 500;
    /// <summary>Kurznotizen zum Antippen nach dem Setzen (kein Tippen in der Brille; Annahme V1a, Christian legt sie fest).</summary>
    public static readonly IReadOnlyList<string> Kurznotizen = ["Kollision", "Fehlt", "Prüfen", "Zu eng"];
    public const string ModusBegehen = "begehen", ModusSitzung = "sitzungsraum";

    internal static JsonArray Zahlen(double[] w, int stellen = 1) => new(w.Take(3).Select(x => (JsonNode)JsonValue.Create(Math.Round(x, stellen))!).ToArray());

    public static string Kuerzen(string? s, int max)
    {
        var t = (s ?? "").Replace('\n', ' ').Trim();
        return t.Length <= max ? t : t[..(max - 1)] + "…";
    }

    /// <summary>
    /// Neuer Marker aus der Szene: ort und kopf in der Szene (m, relativ zum Bezugspunkt), blick = Blickrichtung in der Szene; elementSchluessel = Schlüssel der Szene
    /// («modell:&lt;Id&gt;», «unterlage:&lt;GlobalId&gt;») bzw. null; art = Ebene bzw. Entität.
    /// </summary>
    public static MarkerNeu Neu(string kennung, Vec3 ort, Vec3 kopf, Vec3 blick, double[]? bezug, string modus, double massstab, string? elementSchluessel, string? art,
        string? geschoss, DateTimeOffset zeit, string? notiz = null)
    {
        var r = blick.Einheit();
        MarkerElement? element = null;
        if (elementSchluessel is { } s && s.IndexOf(':') is var i and > 0 && i < s.Length - 1) element = new MarkerElement(s[(i + 1)..], s[..i], art);
        return new MarkerNeu(kennung, KicadUmrechnung.PunktZuKicad(ort, bezug),
            new MarkerBlick(KicadUmrechnung.PunktZuKicad(kopf, bezug), [r.X, -r.Z, r.Y], modus, massstab),
            element, geschoss, zeit.ToString("yyyy-MM-dd'T'HH:mm:ssK", CultureInfo.InvariantCulture), NotizSauber(notiz));
    }

    /// <summary>Notiz ohne Steuerzeichen (ausser Zeilenwechsel), gekürzt auf <see cref="NotizMax"/>; leer = null.</summary>
    public static string? NotizSauber(string? notiz)
    {
        if (notiz is null) return null;
        var t = new string([.. notiz.Where(c => c == '\n' || !char.IsControl(c))]).Trim();
        return t.Length == 0 ? null : t.Length <= NotizMax ? t : t[..NotizMax];
    }

    /// <summary>Ort eines Markers in der Szene (m, relativ zum Bezugspunkt).</summary>
    public static Vec3 OrtSzene(MarkerEintrag m, double[]? bezug) => KicadUmrechnung.PunktAusKicad(m.OrtMm[0], m.OrtMm[1], m.OrtMm[2], bezug);

    /// <summary>Liest die Antwort von GET /api/xr/marker (tolerant: unlesbare Einträge fallen weg). Wirft JsonException, wenn der Text kein JSON ist.</summary>
    public static IReadOnlyList<MarkerEintrag> ListeLesen(string json)
    {
        using var d = JsonDocument.Parse(json);
        var aus = new List<MarkerEintrag>();
        if (!d.RootElement.TryGetProperty("marker", out var liste) || liste.ValueKind != JsonValueKind.Array) return aus;
        foreach (var m in liste.EnumerateArray()) if (EintragLesen(m) is { } e) aus.Add(e);
        return aus;
    }

    static string? T(JsonElement o, string n) => o.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    static double[]? P(JsonElement o, string n)
    {
        if (!o.TryGetProperty(n, out var v) || v.ValueKind != JsonValueKind.Array || v.GetArrayLength() != 3) return null;
        var w = v.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.Number && x.TryGetDouble(out var z) && double.IsFinite(z) ? z : double.NaN).ToArray();
        return w.All(double.IsFinite) ? w : null;
    }

    static MarkerElement? ElementLesen(JsonElement m) =>
        m.TryGetProperty("element", out var el) && el.ValueKind == JsonValueKind.Object && T(el, "id") is { } eid ? new MarkerElement(eid, T(el, "teil") ?? "modell", T(el, "art")) : null;

    static MarkerBlick? BlickLesen(JsonElement m) =>
        m.TryGetProperty("blick", out var b) && b.ValueKind == JsonValueKind.Object && P(b, "kopf") is { } k && P(b, "richtung") is { } r
            ? new MarkerBlick(k, r, T(b, "modus") ?? ModusBegehen, b.TryGetProperty("massstab", out var ms) && ms.ValueKind == JsonValueKind.Number && ms.TryGetDouble(out var x) ? x : 1)
            : null;

    /// <summary>Ein Marker aus JSON (auch «marker» der Antwort auf POST); null, wenn Id, Kennzeichen oder Ort fehlen.</summary>
    public static MarkerEintrag? EintragLesen(JsonElement m)
    {
        if (m.ValueKind != JsonValueKind.Object) return null;
        var id = T(m, "id");
        var kz = T(m, "kennzeichen");
        var ort = P(m, "ort");
        if (id is null || kz is null || ort is null) return null;
        var status = T(m, "status") is { } s && MarkerStatus.Alle.Contains(s) ? s : MarkerStatus.Offen;
        return new MarkerEintrag(id, kz, T(m, "kennung"), ort, status, T(m, "notiz"), T(m, "zustaendig"), T(m, "frist"), T(m, "benutzer"), T(m, "zeit"),
            T(m, "geschoss"), ElementLesen(m), BlickLesen(m));
    }

    /// <summary>Ein neuer Marker aus JSON (Format von <see cref="MarkerNeu.ZuJson"/>); null, wenn Kennung, Ort oder Blick fehlen.</summary>
    public static MarkerNeu? NeuLesen(JsonElement o)
    {
        if (o.ValueKind != JsonValueKind.Object || T(o, "kennung") is not { Length: > 0 } kennung || P(o, "ort") is not { } ort || BlickLesen(o) is not { } blick) return null;
        return new MarkerNeu(kennung, ort, blick, ElementLesen(o), T(o, "geschoss"), T(o, "zeit") ?? "", NotizSauber(T(o, "notiz")));
    }

    /// <summary>
    /// Die Marker, die die App zeigt: die Liste von KICAD und dazu die noch nicht gesendeten (als «offen», Kennzeichen «neu»), ohne Doppel (gleiche Kennung).
    /// </summary>
    public static IReadOnlyList<MarkerEintrag> Zusammen(IReadOnlyList<MarkerEintrag> vomPc, IReadOnlyList<MarkerNeu> wartend)
    {
        var kennungen = vomPc.Select(m => m.Kennung).Where(k => k is not null).ToHashSet(StringComparer.Ordinal);
        return [.. vomPc, .. wartend.Where(w => !kennungen.Contains(w.Kennung))
            .Select(w => new MarkerEintrag("wartend:" + w.Kennung, "neu", w.Kennung, w.OrtMm, MarkerStatus.Offen, w.Notiz, Zeit: w.Zeit, GeschossId: w.Geschoss, Element: w.Element, Blick: w.Blick))];
    }
}

/// <summary>
/// Ablage der App für Marker (privater Speicher): wartende (ohne Verbindung gesetzt; <c>marker/wartend.jsonl</c>, höchstens <see cref="WartendMax"/>, die ältesten
/// fallen weg) und die zuletzt von KICAD geholte Liste (<c>marker/liste.json</c>, je Projekt eine).
/// </summary>
public sealed class MarkerAblage(string ordner)
{
    public const int WartendMax = 100;
    string Ordner => Path.Combine(ordner, "marker");
    public string DateiWartend => Path.Combine(Ordner, "wartend.jsonl");
    string DateiListe(string projekt) => Path.Combine(Ordner, $"liste_{Sicher(projekt)}.json");

    static string Sicher(string s) => new([.. s.Where(c => char.IsAsciiLetterOrDigit(c) || c == '-').Take(64)]);

    /// <summary>Wartende Marker (älteste zuerst); unlesbare Zeilen fallen weg.</summary>
    public List<MarkerNeu> Wartend()
    {
        try
        {
            if (!File.Exists(DateiWartend)) return [];
            var aus = new List<MarkerNeu>();
            foreach (var z in File.ReadAllLines(DateiWartend, Encoding.UTF8))
            {
                try { if (Lesen(z) is { } m) aus.Add(m); }
                catch (JsonException) { }
            }
            return [.. aus.TakeLast(WartendMax)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return []; }
    }

    static MarkerNeu? Lesen(string zeile)
    {
        using var d = JsonDocument.Parse(zeile);
        return ProblemMarker.NeuLesen(d.RootElement);
    }

    /// <summary>Legt die wartenden Marker ab (die neuesten <see cref="WartendMax"/>); leer löscht die Datei.</summary>
    public void WartendSpeichern(IEnumerable<MarkerNeu> marker)
    {
        var liste = marker.TakeLast(WartendMax).Select(m => m.ZuJson().ToJsonString()).ToList();
        if (liste.Count == 0) { if (File.Exists(DateiWartend)) File.Delete(DateiWartend); return; }
        Directory.CreateDirectory(Ordner);
        var tmp = DateiWartend + ".tmp";
        File.WriteAllLines(tmp, liste, new UTF8Encoding(false));
        File.Move(tmp, DateiWartend, overwrite: true);
    }

    /// <summary>Zuletzt geholte Liste eines Projekts (Text der Antwort), sonst null.</summary>
    public string? Liste(string projekt)
    {
        try { return File.Exists(DateiListe(projekt)) ? File.ReadAllText(DateiListe(projekt), Encoding.UTF8) : null; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    public void ListeSpeichern(string projekt, string json)
    {
        Directory.CreateDirectory(Ordner);
        var tmp = DateiListe(projekt) + ".tmp";
        File.WriteAllText(tmp, json, new UTF8Encoding(false));
        File.Move(tmp, DateiListe(projekt), overwrite: true);
    }
}
