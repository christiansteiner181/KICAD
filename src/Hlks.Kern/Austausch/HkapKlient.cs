// HKAP-Klient (Modul 1d): ruft die Austauschstelle von HKAP auf (Server zu Server, HKAP hat kein CORS). HKAP läuft
// lokal und kann jederzeit fehlen – es beendet sich 12 s nach dem Schliessen seines Browserfensters. Darum: kurzes
// Zeitlimit (5 s), kein Proxy, und jeder Ausfall wird ein HlksFehler mit klarer Meldung; der Aufrufer ändert dann
// nichts (der Basisstand bleibt, der Abgleich kann jederzeit wiederholt werden).
//
// Adresse: ausdrücklicher Parameter › Projektinformation.hkap.url › Umgebung HLKS_HKAP_URL › http://localhost:5013.

using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

public sealed class HkapKlient
{
    public const string Vorgabe = "http://localhost:5013";
    public const string Umgebung = "HLKS_HKAP_URL";
    public static readonly TimeSpan VorgabeZeitlimit = TimeSpan.FromSeconds(5);

    static readonly HttpClient Http = new(new SocketsHttpHandler
    {
        UseProxy = false,
        ConnectTimeout = VorgabeZeitlimit,
        PooledConnectionLifetime = TimeSpan.FromMinutes(1),
        AutomaticDecompression = DecompressionMethods.All,
    }) { Timeout = Timeout.InfiniteTimeSpan };

    readonly Uri basis;

    public string Url { get; }
    public TimeSpan Zeitlimit { get; }
    /// <summary>false, wenn der letzte Aufruf HKAP nicht erreicht hat (keine Verbindung, Zeitlimit); null = noch kein Aufruf.</summary>
    public bool? Erreichbar { get; private set; }

    public HkapKlient(string url, TimeSpan? zeitlimit = null)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var u) || (u.Scheme != Uri.UriSchemeHttp && u.Scheme != Uri.UriSchemeHttps))
            throw new HlksFehler($"HKAP-Adresse «{url}» ist ungültig.", $"Adresse wie {Vorgabe} angeben (http oder https).");
        Url = u.GetLeftPart(UriPartial.Path).TrimEnd('/');
        basis = new Uri(Url + "/");
        Zeitlimit = zeitlimit ?? VorgabeZeitlimit;
    }

    /// <summary>Adresse: ausdrücklich › aus der Verknüpfung › HLKS_HKAP_URL › Vorgabe.</summary>
    public static string UrlFuer(string? ausdruecklich, string? ausVerknuepfung = null) =>
        Leer(ausdruecklich) ?? Leer(ausVerknuepfung) ?? Leer(Environment.GetEnvironmentVariable(Umgebung)) ?? Vorgabe;

    static string? Leer(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    // ---------------------------------------------------------------- Vertrag

    public HkapKatalog Katalog()
    {
        var n = Aufruf(HttpMethod.Get, "api/austausch/katalog", null, "Austauschkatalog",
            "HKAP hat keine Austauschstelle (/api/austausch fehlt): eine HKAP-Fassung mit Modul 1d starten.");
        return HkapKatalog.AusJson(n);
    }

    public List<HkapDatensatz> Lesen(string projekt, string datenart, string? projektierung) =>
        HkapDatensatz.ListeAusJson(Aufruf(HttpMethod.Get, Pfad(projekt, datenart, projektierung, null), null,
            $"{datenart} von {projekt}{(projektierung is null ? "" : "/" + projektierung)}",
            $"Projekt-Id (P-…){(projektierung is null ? "" : " und Projektierung (PJ-…)")} prüfen; hkap_verbindung listet die Projekte."));

    public List<HkapErgebnis> Schreiben(string projekt, string datenart, string? projektierung, bool trockenlauf, IReadOnlyList<HkapAenderung> saetze)
    {
        var body = new JsonObject
        {
            ["quelle"] = "KICAD",
            ["datensaetze"] = new JsonArray(saetze.Select(s => (JsonNode)s.AlsJson()).ToArray()),
        };
        var n = Aufruf(HttpMethod.Post, Pfad(projekt, datenart, projektierung, trockenlauf), body,
            $"{datenart} von {projekt}{(projektierung is null ? "" : "/" + projektierung)}", "Projekt-Id und Projektierung prüfen.");
        return HkapErgebnis.ListeAusJson(n);
    }

    // ---------------------------------------------------------------- Projektlisten

    JsonArray? austauschProjekte;
    bool ohneAustauschProjekte;

    /// <summary>GET /api/austausch/projekte → [{ id, name, typ, kuerzel, bezeichnung, projektierungen: [{ id, name, etagen, raeume }] }];
    /// null, wenn HKAP die Stelle noch nicht hat (404, ältere Fassung). Je Klient einmal gelesen.</summary>
    JsonArray? AustauschProjekte()
    {
        if (austauschProjekte is not null || ohneAustauschProjekte) return austauschProjekte;
        try
        {
            var n = Aufruf(HttpMethod.Get, "api/austausch/projekte", null, "Projektliste", null);
            return austauschProjekte = (JsonArray)((n?["projekte"] as JsonArray ?? n as JsonArray ?? new JsonArray()).DeepClone());
        }
        catch (HlksFehler) when (LetzterStatus == HttpStatusCode.NotFound) { ohneAustauschProjekte = true; return null; }
    }

    /// <summary>Projekte: zuerst aus /api/austausch/projekte (mit Projektierungen), sonst aus GET /api/projects (Rückfall auf die
    /// Stelle vor dem Vertrag): [{ id, name, … }]. Ausfall der Liste ist kein Ausfall von HKAP.</summary>
    public JsonArray Projekte()
    {
        if (AustauschProjekte() is { } liste) return (JsonArray)liste.DeepClone();
        var n = Aufruf(HttpMethod.Get, "api/projects", null, "Projektliste", null);
        return new JsonArray((n as JsonArray ?? new JsonArray()).OfType<JsonObject>().Select(p => (JsonNode)new JsonObject
        {
            ["id"] = HkapWerte.Text(p["id"]), ["name"] = HkapWerte.Text(p["name"]), ["phase"] = HkapWerte.Leer(p["aktuellePhase"]),
        }).ToArray());
    }

    /// <summary>Projektierungen eines Projekts: aus /api/austausch/projekte ([{ id, name, etagen, raeume }]), sonst aus
    /// GET /api/projects/{id}/projektierungen ([{ id, name }]).</summary>
    public JsonArray Projektierungen(string projekt)
    {
        if (AustauschProjekte() is { } liste)
        {
            var p = liste.OfType<JsonObject>().FirstOrDefault(x => string.Equals(HkapWerte.Text(x["id"]), projekt, StringComparison.OrdinalIgnoreCase))
                ?? throw new HlksFehler($"HKAP kennt das Projekt {projekt} nicht.", "Projekt-Id prüfen; hkap_verbindung listet die Projekte.");
            return (JsonArray)(p["projektierungen"] as JsonArray ?? new JsonArray()).DeepClone();
        }
        var n = Aufruf(HttpMethod.Get, $"api/projects/{Uri.EscapeDataString(projekt)}/projektierungen", null, $"Projektierungen von {projekt}", null);
        return new JsonArray((n as JsonArray ?? new JsonArray()).OfType<JsonObject>().Select(p => (JsonNode)new JsonObject
        {
            ["id"] = HkapWerte.Text(p["id"]), ["name"] = HkapWerte.Text(p["name"]),
        }).ToArray());
    }

    /// <summary>HTTP-Status der letzten Antwort (null ohne Antwort).</summary>
    public HttpStatusCode? LetzterStatus { get; private set; }

    // ---------------------------------------------------------------- Transport

    static string Pfad(string projekt, string datenart, string? projektierung, bool? trockenlauf)
    {
        var q = new List<string>();
        if (!string.IsNullOrEmpty(projektierung)) q.Add("projektierung=" + Uri.EscapeDataString(projektierung));
        if (trockenlauf is bool t) q.Add("trockenlauf=" + (t ? "true" : "false"));
        return $"api/austausch/{Uri.EscapeDataString(projekt)}/{Uri.EscapeDataString(datenart)}" + (q.Count > 0 ? "?" + string.Join("&", q) : "");
    }

    string Vorschlag => $"HKAP starten (es beendet sich 12 s nach dem Schliessen seines Browserfensters) und erneut versuchen; Adresse prüfen ({Url}). " +
                        "Nichts ist verloren: KICAD hat nichts geändert, der Basisstand bleibt.";

    JsonNode? Aufruf(HttpMethod methode, string pfad, JsonNode? body, string was, string? vorschlag404)
    {
        using var anfrage = new HttpRequestMessage(methode, new Uri(basis, pfad));
        anfrage.Headers.Accept.ParseAdd("application/json");
        if (body is not null) anfrage.Content = new StringContent(body.ToJsonString(Json.Optionen), Encoding.UTF8, "application/json");
        using var cts = new CancellationTokenSource(Zeitlimit);
        LetzterStatus = null;
        HttpResponseMessage antwort;
        string text;
        try
        {
            antwort = Http.Send(anfrage, HttpCompletionOption.ResponseContentRead, cts.Token);
            using var s = antwort.Content.ReadAsStream(cts.Token);
            using var r = new StreamReader(s, Encoding.UTF8);
            text = r.ReadToEnd();
        }
        catch (OperationCanceledException)
        {
            Erreichbar = false;
            throw new HlksFehler($"HKAP unter {Url} antwortet nicht (keine Antwort innert {Zeitlimit.TotalSeconds:0.#} s).", Vorschlag);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            Erreichbar = false;
            throw new HlksFehler($"HKAP ist unter {Url} nicht erreichbar ({ex.GetBaseException().Message}).", Vorschlag);
        }
        Erreichbar = true;
        LetzterStatus = antwort.StatusCode;
        using (antwort)
        {
            if (!antwort.IsSuccessStatusCode)
            {
                var grund = FehlerText(text);
                if (antwort.StatusCode == HttpStatusCode.NotFound)
                    throw new HlksFehler($"HKAP kennt {was} nicht (404{(grund is null ? "" : ": " + grund)}).", vorschlag404 ?? "Angaben prüfen.");
                throw new HlksFehler($"HKAP lehnt den Aufruf ab ({(int)antwort.StatusCode} {antwort.ReasonPhrase}{(grund is null ? "" : ": " + grund)}).",
                    "Meldung von HKAP prüfen; bei 409 hat jemand gleichzeitig geändert – Abgleich erneut ansehen.");
            }
            if (string.IsNullOrWhiteSpace(text)) return null;
            try { return JsonNode.Parse(text); }
            catch (JsonException ex) { throw new HlksFehler($"Antwort von HKAP ({was}) ist kein gültiges JSON: {ex.Message}", "HKAP-Fassung prüfen (Austauschstelle Modul 1d)."); }
        }
    }

    static string? FehlerText(string text)
    {
        try
        {
            var n = JsonNode.Parse(text);
            return HkapWerte.Leer(n?["fehler"]) ?? HkapWerte.Leer(n?["error"]) ?? HkapWerte.Leer(n?["grund"]) ?? HkapWerte.Leer(n?["title"]);
        }
        catch (JsonException) { return string.IsNullOrWhiteSpace(text) ? null : text.Trim()[..Math.Min(200, text.Trim().Length)]; }
    }
}
