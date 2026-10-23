// Quest App P4a (Modul XR): HTTPS-Zugang der App zum KICAD am PC (Netzzugang, Port 5140, nur /api/xr; src/Hlks.Web/XrZugang.cs).
//
//   GET /api/xr/verbindung                       Probe mit Schlüssel → { ok, fassung, pc, kopplung{ id, brille, name }, zeit, teilnehmer }
//   GET /api/xr/geschosse                        Liste der Geschosse mit Versionsschlüsseln und Adressen der GLB (P2)
//   GET /api/xr/geschoss/{id}.glb?teil=…         GLB mit ETag; If-None-Match → 304 (P2)
//   GET /api/xr/ebenen, /api/xr/ebene/{name}      Ebenen der Fachmodule mit ETag (P4b, EbenenLader)
//
// Jede Anfrage trägt Authorization: Bearer <kopplung>.<schluessel> und geht nur an das gepinnte Zertifikat (XrPinning). Die Modellteile laufen über
// IModellQuelle, damit dieselbe Logik (Zwischenspeicher, Lader) im Test bzw. am PC auch über localhost laufen kann, solange der Netzzugang die
// GLB-Endpunkte noch nicht freigibt (paralleler Nachtrag P3; die App versucht immer zuerst den Netzzugang).

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Hlks.Xr.Netz;

/// <summary>Ein gefundener PC: IP-Adresse und Port des Netzzugangs, Name des PCs und woher die Adresse kam (gespeichert, paket, suche).</summary>
public sealed record PcAdresse(string Ip, int Port, string Pc, string Quelle)
{
    public Uri Basis => new($"https://{Ip}:{Port}/");
    public Uri Kanal => new($"wss://{Ip}:{Port}/api/xr/kanal");
    public override string ToString() => $"{Pc} ({Ip}:{Port}, {Quelle})";
}

/// <summary>Antwort auf die Probe: Ok, HTTP-Status (0 = keine Verbindung), Name des PCs, Meldung.</summary>
public sealed record ProbeErgebnis(bool Ok, int Status, string? Pc, string? Meldung)
{
    /// <summary>401: Schlüssel gilt nicht (Kopplung widerrufen oder ersetzt).</summary>
    public bool SchluesselUngueltig => Status == 401;
}

/// <summary>Antwort auf den Abruf einer GLB: 200 mit Daten oder 304 (unverändert), mit ETag.</summary>
public sealed record GlbAntwort(int Status, byte[]? Daten, string? ETag);

/// <summary>Woher die App Liste und GLB holt (Netzzugang mit Schlüssel; im Test bzw. am PC auch localhost).</summary>
public interface IModellQuelle
{
    /// <summary>Kurzname für Anzeige und Protokoll, z. B. «netz» oder «lokal».</summary>
    string Name { get; }
    /// <summary>GET /api/xr/geschosse als Text. Wirft <see cref="XrNetzFehler"/> bzw. HttpRequestException.</summary>
    Task<string> GeschosseText(CancellationToken abbruch);
    /// <summary>GET der GLB (Adresse aus der Liste, relativ); etag = If-None-Match. Status 200 oder 304, sonst <see cref="XrNetzFehler"/>.</summary>
    Task<GlbAntwort> Glb(string url, string? etag, CancellationToken abbruch);
}

/// <summary>Liste und GLB über einen HttpClient mit Basisadresse (Netzzugang oder localhost); seit P4b auch die Ebenen (IEbenenQuelle).</summary>
public sealed class ModellHttp(HttpClient http, string name) : IModellQuelle, IEbenenQuelle
{
    public string Name { get; } = name;

    /// <summary>P4b: GET mit Text (Ebenen), etag = If-None-Match. Jeder Status kommt zurück (200 mit Text, 304, 404 …).</summary>
    public async Task<TextAntwort> Text(string pfad, string? etag, CancellationToken abbruch)
    {
        using var r = new HttpRequestMessage(HttpMethod.Get, pfad.TrimStart('/'));
        if (!string.IsNullOrEmpty(etag)) r.Headers.TryAddWithoutValidation("If-None-Match", etag);
        using var a = await http.SendAsync(r, abbruch).ConfigureAwait(false);
        var neu = a.Headers.ETag?.ToString();
        if (a.StatusCode == HttpStatusCode.NotModified) return new TextAntwort(304, null, neu ?? etag);
        var t = await a.Content.ReadAsStringAsync(abbruch).ConfigureAwait(false);
        return new TextAntwort((int)a.StatusCode, a.StatusCode == HttpStatusCode.OK ? t : null, neu);
    }

    public async Task<string> GeschosseText(CancellationToken abbruch)
    {
        using var a = await http.GetAsync("api/xr/geschosse", abbruch).ConfigureAwait(false);
        var t = await a.Content.ReadAsStringAsync(abbruch).ConfigureAwait(false);
        if (a.StatusCode != HttpStatusCode.OK) throw new XrNetzFehler($"Die Liste der Geschosse kam nicht ({(int)a.StatusCode}): {Meldung(t)}", Vorschlag(a.StatusCode));
        return t;
    }

    public async Task<GlbAntwort> Glb(string url, string? etag, CancellationToken abbruch)
    {
        using var r = new HttpRequestMessage(HttpMethod.Get, url.TrimStart('/'));
        if (!string.IsNullOrEmpty(etag)) r.Headers.TryAddWithoutValidation("If-None-Match", etag);
        using var a = await http.SendAsync(r, HttpCompletionOption.ResponseHeadersRead, abbruch).ConfigureAwait(false);
        var neu = a.Headers.ETag?.ToString();
        if (a.StatusCode == HttpStatusCode.NotModified) return new GlbAntwort(304, null, neu ?? etag);
        if (a.StatusCode != HttpStatusCode.OK)
            throw new XrNetzFehler($"Das Modell kam nicht ({(int)a.StatusCode}): {Meldung(await a.Content.ReadAsStringAsync(abbruch).ConfigureAwait(false))}", Vorschlag(a.StatusCode));
        return new GlbAntwort(200, await a.Content.ReadAsByteArrayAsync(abbruch).ConfigureAwait(false), neu);
    }

    internal static string Meldung(string text)
    {
        try
        {
            using var d = JsonDocument.Parse(text);
            if (d.RootElement.ValueKind == JsonValueKind.Object && d.RootElement.TryGetProperty("fehler", out var f) && f.ValueKind == JsonValueKind.String) return f.GetString()!;
        }
        catch (JsonException) { }
        return text.Length > 120 ? text[..120] + "…" : text;
    }

    internal static string? Vorschlag(HttpStatusCode s) => s switch
    {
        HttpStatusCode.Unauthorized => "Die Brille in KICAD neu koppeln.",
        HttpStatusCode.NotFound => "KICAD aktualisieren (die Endpunkte für die App fehlen) bzw. ein Projekt öffnen.",
        HttpStatusCode.Conflict => "In KICAD am PC ein Projekt öffnen.",
        HttpStatusCode.TooManyRequests => "Kurz warten; zu viele Anfragen bzw. Fehlversuche.",
        _ => null,
    };
}

/// <summary>Verbindung zum Netzzugang eines PCs: HttpClient mit Pinning und Schlüssel.</summary>
public sealed class PcZugang : IDisposable
{
    readonly HttpClient http;

    public PcAdresse Adresse { get; }
    /// <summary>Liste und GLB über den Netzzugang.</summary>
    public IModellQuelle Modell { get; }
    /// <summary>Ebenen der Fachmodule über den Netzzugang (P4b).</summary>
    public IEbenenQuelle Ebenen => (ModellHttp)Modell;

    /// <param name="handler">Nur für Tests; sonst ein Handler mit Pinning (<see cref="XrPinning.Handler"/>).</param>
    public PcZugang(PcAdresse adresse, Kopplungspaket paket, HttpMessageHandler? handler = null, TimeSpan? frist = null)
    {
        Adresse = adresse;
        http = new HttpClient(handler ?? XrPinning.Handler(paket.Fingerabdruck), disposeHandler: true)
        {
            BaseAddress = adresse.Basis,
            Timeout = frist ?? TimeSpan.FromSeconds(120),   // grosse GLB über WLAN (Test_praxis EG: 11 MB)
        };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", paket.Token);
        Modell = new ModellHttp(http, "netz");
    }

    /// <summary>GET /api/xr/verbindung: erreichbar, Zertifikat passt, Schlüssel gilt? Wirft nicht (Status 0 = keine Verbindung oder falsches Zertifikat).</summary>
    public async Task<ProbeErgebnis> Probe(CancellationToken abbruch)
    {
        try
        {
            using var a = await http.GetAsync("api/xr/verbindung", abbruch).ConfigureAwait(false);
            var t = await a.Content.ReadAsStringAsync(abbruch).ConfigureAwait(false);
            if (a.StatusCode != HttpStatusCode.OK) return new ProbeErgebnis(false, (int)a.StatusCode, null, ModellHttp.Meldung(t));
            using var d = JsonDocument.Parse(t);
            var ok = d.RootElement.TryGetProperty("ok", out var o) && o.ValueKind == JsonValueKind.True;
            var pc = d.RootElement.TryGetProperty("pc", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
            return new ProbeErgebnis(ok, 200, pc, ok ? null : "Unerwartete Antwort.");
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or IOException)
        {
            return new ProbeErgebnis(false, 0, null, ex is OperationCanceledException ? "keine Antwort in der Frist" : ex.Message);
        }
    }

    public void Dispose() => http.Dispose();
}
