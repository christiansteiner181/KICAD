// Suche des PCs im Büronetz (Modul XR, Quest App P3): UDP, Vorgabe Port 5141, nur wenn der XR-Zugang an ist (XrNetzDienst startet und beendet sie).
//
// Die Brille ruft per Broadcast (z. B. 255.255.255.255:5141 oder die Broadcast-Adresse ihres Netzes); KICAD antwortet NUR auf eine Anfrage, die mit dem
// Suchschlüssel einer gültigen Kopplung signiert ist, und nur an den Absender (Unicast). KICAD sendet nie von selbst.
//
//   Anfrage  { "typ": "suche", "fassung": 1, "kopplung": "<16 hex>", "zeit": <Unix-ms>, "zufall": "<base64url, 16–48 Byte>", "signatur": "<base64url>" }
//            signatur = HMAC-SHA256(Suchschlüssel, "hlks-xr-suche|1|<kopplung>|<zeit>|<zufall>")
//   Antwort  { "typ": "gefunden", "fassung": 1, "pc", "ips": [..], "port", "fingerabdruck", "zufall": <wie Anfrage>, "signatur" }
//            signatur = HMAC-SHA256(Suchschlüssel, "hlks-xr-gefunden|1|<zufall>|<pc>|<port>|<fingerabdruck>|<ips mit Komma>")
//   Suchschlüssel = HMAC-SHA256(Schlüssel, "hlks-xr-suche") (XrSchluessel.Suchschluessel; die App leitet ihn aus dem Schlüssel des Pakets ab).
//
// Schutz: Datagramme bis 1024 Byte; Zeit höchstens ±60 s neben der Uhr des PCs; jedes «zufall» nur einmal (Wiederholung wird verworfen); je Absender
// höchstens 10 Datagramme, dann 1 je Sekunde; ungültige Anfragen bleiben ohne Antwort (kein Hinweis, ob es die Kopplung gibt). Die Antwort ist kaum
// grösser als die Anfrage und geht nur an gültig signierte Absender (keine Verstärkung).

using System.Buffers.Text;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Hlks.Web;

public sealed class XrSuche : IDisposable
{
    public const int DatagrammMax = 1024;
    static readonly Regex ZufallMuster = new("^[A-Za-z0-9_-]{22,64}$", RegexOptions.CultureInvariant);

    readonly XrKopplungen kopplungen;
    readonly Func<XrZugangInfo> info;
    readonly XrEinstellungen einstellungen;
    readonly XrBremse bremse;
    readonly Dictionary<string, DateTimeOffset> gesehen = new(StringComparer.Ordinal);
    readonly object sperre = new();
    UdpClient? udp;
    CancellationTokenSource? stopp;
    Task? schleife;

    public XrSuche(XrKopplungen kopplungen, Func<XrZugangInfo> info, XrEinstellungen einstellungen)
    {
        this.kopplungen = kopplungen;
        this.info = info;
        this.einstellungen = einstellungen;
        bremse = new XrBremse(1, 10, einstellungen.Jetzt);
    }

    /// <summary>Gebundener Port (nach Starten).</summary>
    public int Port { get; private set; }

    /// <summary>Anzahl beantworteter Anfragen (Diagnose).</summary>
    public long Beantwortet;

    // ---------------------------------------------------------------- Texte und Signaturen (rein; die App rechnet gleich)

    public static string AnfrageText(string kopplung, long zeit, string zufall) => $"hlks-xr-suche|1|{kopplung}|{zeit}|{zufall}";

    public static string AntwortText(string zufall, string pc, int port, string fingerabdruck, IEnumerable<string> ips) =>
        $"hlks-xr-gefunden|1|{zufall}|{pc}|{port}|{fingerabdruck}|{string.Join(",", ips)}";

    public static string Signieren(byte[] suchschluessel, string text) => Base64Url.EncodeToString(HMACSHA256.HashData(suchschluessel, Encoding.UTF8.GetBytes(text)));

    /// <summary>Baut eine Anfrage (Muster für die App und für Tests).</summary>
    public static byte[] AnfrageBauen(string kopplung, byte[] suchschluessel, DateTimeOffset zeit, string? zufall = null)
    {
        zufall ??= Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));
        var ms = zeit.ToUnixTimeMilliseconds();
        var o = new JsonObject
        {
            ["typ"] = "suche", ["fassung"] = 1, ["kopplung"] = kopplung, ["zeit"] = ms, ["zufall"] = zufall,
            ["signatur"] = Signieren(suchschluessel, AnfrageText(kopplung, ms, zufall)),
        };
        return Encoding.UTF8.GetBytes(o.ToJsonString());
    }

    /// <summary>Prüft eine Anfrage und baut die Antwort; null = nicht antworten. absender: Schlüssel der Bremse (IP-Adresse).</summary>
    public byte[]? Beantworten(ReadOnlySpan<byte> datagramm, string absender)
    {
        if (datagramm.Length is 0 or > DatagrammMax) return null;
        if (!bremse.Nehmen(absender)) return null;
        JsonObject? o;
        try { o = JsonNode.Parse(datagramm, documentOptions: new JsonDocumentOptions { MaxDepth = 4 }) as JsonObject; }
        catch (JsonException) { return null; }
        if (o is null) return null;
        string? Text(string n) => o[n] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
        if (Text("typ") != "suche" || o["fassung"] is not JsonValue fv || !fv.TryGetValue<int>(out var f) || f != 1) return null;
        var kopplung = Text("kopplung");
        var zufall = Text("zufall");
        var signatur = Text("signatur");
        if (!XrSchluessel.IdGueltig(kopplung) || zufall is null || !ZufallMuster.IsMatch(zufall) || signatur is null || signatur.Length > 64) return null;
        if (o["zeit"] is not JsonValue zv || !zv.TryGetValue<long>(out var zeit)) return null;
        var jetzt = einstellungen.Jetzt();
        if (Math.Abs(jetzt.ToUnixTimeMilliseconds() - zeit) > einstellungen.SucheZeitfenster.TotalMilliseconds) return null;
        var schluessel = kopplungen.Suchschluessel(kopplung!);
        if (schluessel is null) return null;
        try
        {
            var soll = HMACSHA256.HashData(schluessel, Encoding.UTF8.GetBytes(AnfrageText(kopplung!, zeit, zufall)));
            var ist = XrSchluessel.Base64UrlLesen(signatur, 32);
            if (ist is null || !CryptographicOperations.FixedTimeEquals(soll, ist)) return null;
            lock (sperre)
            {
                foreach (var alt in gesehen.Where(p => jetzt - p.Value > 2 * einstellungen.SucheZeitfenster).Select(p => p.Key).ToList()) gesehen.Remove(alt);
                if (gesehen.Count >= 10_000 || !gesehen.TryAdd(zufall, jetzt)) return null;   // Wiederholung
            }
            var i = info();
            var antwort = new JsonObject
            {
                ["typ"] = "gefunden", ["fassung"] = 1, ["pc"] = i.Pc, ["ips"] = new JsonArray([.. i.Ips.Select(x => (JsonNode)x)]), ["port"] = i.Port,
                ["fingerabdruck"] = i.Fingerabdruck, ["zufall"] = zufall,
                ["signatur"] = Signieren(schluessel, AntwortText(zufall, i.Pc, i.Port, i.Fingerabdruck, i.Ips)),
            };
            Interlocked.Increment(ref Beantwortet);
            return Encoding.UTF8.GetBytes(antwort.ToJsonString());
        }
        finally { CryptographicOperations.ZeroMemory(schluessel); }
    }

    // ---------------------------------------------------------------- Lauschen

    /// <summary>Bindet UDP auf adresse:port (0 = frei) und beantwortet Anfragen, bis Dispose. Wirft SocketException, wenn der Port belegt ist.</summary>
    public void Starten(IPAddress adresse, int port)
    {
        var u = new UdpClient(AddressFamily.InterNetwork);
        try
        {
            if (OperatingSystem.IsWindows())
            {
                u.Client.ExclusiveAddressUse = true;   // kein anderer Prozess darf denselben Port mitbenutzen
                const int SioUdpConnreset = -1744830452;   // ICMP «Port unerreichbar» soll das Empfangen nicht abbrechen
                u.Client.IOControl(SioUdpConnreset, [0, 0, 0, 0], null);
            }
            u.Client.Bind(new IPEndPoint(adresse, port));
        }
        catch { u.Dispose(); throw; }
        udp = u;
        Port = ((IPEndPoint)u.Client.LocalEndPoint!).Port;
        stopp = new CancellationTokenSource();
        schleife = Task.Run(() => Lauschen(u, stopp.Token));
    }

    async Task Lauschen(UdpClient u, CancellationToken abbruch)
    {
        while (!abbruch.IsCancellationRequested)
        {
            UdpReceiveResult r;
            try { r = await u.ReceiveAsync(abbruch); }
            catch (OperationCanceledException) { return; }
            catch (ObjectDisposedException) { return; }
            catch (SocketException) { continue; }   // z. B. ICMP-Meldungen; weiter lauschen
            byte[]? antwort;
            try { antwort = Beantworten(r.Buffer, r.RemoteEndPoint.Address.ToString()); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException or InvalidOperationException) { antwort = null; }
            if (antwort is null) continue;
            try { await u.SendAsync(antwort, r.RemoteEndPoint, abbruch); }
            catch (Exception ex) when (ex is SocketException or OperationCanceledException or ObjectDisposedException) { /* Absender weg */ }
        }
    }

    public void Dispose()
    {
        try { stopp?.Cancel(); } catch (ObjectDisposedException) { }
        udp?.Dispose();
        try { schleife?.Wait(TimeSpan.FromSeconds(2)); } catch (AggregateException) { }
        stopp?.Dispose();
        udp = null;
    }
}
