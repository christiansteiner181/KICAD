// Quest App P4a (Modul XR): den PC im Büronetz finden. Gegenstück zu src/Hlks.Web/XrSuche.cs (Protokoll dort und in OBERFLAECHE.md,
// «Modul XR: Zugang im Büronetz», Abschnitt Suche).
//
// Reihenfolge (Finden):
//   1. die gespeicherten Adressen (zuletzt erfolgreich, AppAblage) und die IP-Adressen aus dem Kopplungspaket – je eine Probe GET /api/xr/verbindung
//      über HTTPS mit Pinning und Schlüssel, alle gleichzeitig, die erste gültige Antwort gilt;
//   2. sonst die Suche per UDP-Broadcast an den Suchport: Anfrage mit HMAC-Signatur (Suchschlüssel = HMAC-SHA256(Schlüssel, "hlks-xr-suche")), KICAD
//      antwortet nur auf gültig signierte Anfragen und nur an den Absender. Die App prüft die Signatur der Antwort, dass «zufall» ihr eigener ist und
//      dass der Fingerabdruck dem gepinnten entspricht; dann wie 1. mit dem Absender und den IP-Adressen der Antwort.
// Eine Antwort 401 der Probe heisst: der Schlüssel gilt nicht mehr (widerrufen bzw. ersetzt) – dann nicht weiter versuchen (der PC sperrt nach
// 10 Fehlversuchen die Adresse), sondern «neu koppeln» melden. Ein anderer Fingerabdruck in einer gültig signierten Antwort heisst: KICAD hat ein
// neues Zertifikat – ebenfalls neu koppeln (die App nimmt nur das gepinnte an).
//
//   Anfrage  { typ: "suche", fassung: 1, kopplung, zeit (Unix-ms), zufall (base64url, 16 Byte), signatur }
//            signatur = base64url(HMAC-SHA256(Suchschlüssel, "hlks-xr-suche|1|<kopplung>|<zeit>|<zufall>"))
//   Antwort  { typ: "gefunden", fassung: 1, pc, ips[], port, fingerabdruck, zufall, signatur }
//            signatur = base64url(HMAC-SHA256(Suchschlüssel, "hlks-xr-gefunden|1|<zufall>|<pc>|<port>|<fingerabdruck>|<ips mit Komma>"))

using System.Buffers.Text;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hlks.Xr.Netz;

/// <summary>Gültig signierte Antwort der Suche. FingerabdruckPasst = gleich dem gepinnten aus dem Paket.</summary>
public sealed record SucheAntwort(string Pc, IReadOnlyList<string> Ips, int Port, string Fingerabdruck, string? Absender, bool FingerabdruckPasst);

/// <summary>Ergebnis von <see cref="PcSuche.Finden"/>: Adresse (bei «gefunden»), Zustand (<see cref="PcFundZustand"/>) und Meldung für den Menschen.</summary>
public sealed record PcFund(PcAdresse? Adresse, string Zustand, string Meldung);

public static class PcFundZustand
{
    public const string Gefunden = "gefunden";
    public const string NichtGefunden = "nicht_gefunden";
    public const string SchluesselUngueltig = "schluessel_ungueltig";
    public const string ZertifikatAnders = "zertifikat_anders";
}

/// <summary>Fristen und Ziele der Suche; Tests setzen eigene Ziele (z. B. 127.0.0.1:Suchport) und kürzere Fristen.</summary>
public sealed record SucheOptionen
{
    /// <summary>Frist je Probe einer Adresse (Netz im Büro: Antwort in Millisekunden; eine fremde Adresse antwortet nie).</summary>
    public TimeSpan ProbeFrist { get; init; } = TimeSpan.FromSeconds(2.5);
    /// <summary>So lange wartet die Suche auf eine Antwort (zwei Anfragen im Abstand der halben Dauer, UDP kann verloren gehen).</summary>
    public TimeSpan SucheDauer { get; init; } = TimeSpan.FromSeconds(3);
    /// <summary>false: nur die gespeicherten Adressen und die des Pakets probieren.</summary>
    public bool Suchen { get; init; } = true;
    /// <summary>Ziele der Suche; null = Broadcast (255.255.255.255 und die Broadcast-Adressen der eigenen Netze) an den Suchport des Pakets.</summary>
    public IReadOnlyList<IPEndPoint>? Ziele { get; init; }
    /// <summary>Nur für Tests: erzeugt den Zugang für eine Probe (sonst mit Pinning-Handler).</summary>
    public Func<PcAdresse, PcZugang>? ZugangFabrik { get; init; }
    /// <summary>Uhr für den Zeitstempel der Anfrage (Tests).</summary>
    public Func<DateTimeOffset>? Uhr { get; init; }
}

public static class PcSuche
{
    /// <summary>Grösste Antwort, die gelesen wird (Bytes).</summary>
    public const int AntwortMax = 4096;

    public static string AnfrageText(string kopplung, long zeit, string zufall) => $"hlks-xr-suche|1|{kopplung}|{zeit}|{zufall}";

    public static string AntwortText(string zufall, string pc, int port, string fingerabdruck, IEnumerable<string> ips) =>
        $"hlks-xr-gefunden|1|{zufall}|{pc}|{port}|{fingerabdruck}|{string.Join(",", ips)}";

    public static string NeuerZufall() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));

    /// <summary>Die Anfrage als UDP-Datagramm (UTF-8 JSON).</summary>
    public static byte[] Anfrage(Kopplungspaket p, DateTimeOffset zeit, string zufall)
    {
        var ms = zeit.ToUnixTimeMilliseconds();
        var schluessel = p.Suchschluessel();
        try
        {
            var o = new JsonObject
            {
                ["typ"] = "suche", ["fassung"] = 1, ["kopplung"] = p.Kopplung, ["zeit"] = ms, ["zufall"] = zufall,
                ["signatur"] = Base64Url.EncodeToString(HMACSHA256.HashData(schluessel, Encoding.UTF8.GetBytes(AnfrageText(p.Kopplung, ms, zufall)))),
            };
            return Encoding.UTF8.GetBytes(o.ToJsonString());
        }
        finally { CryptographicOperations.ZeroMemory(schluessel); }
    }

    /// <summary>Prüft eine Antwort: Form, eigener Zufall, Signatur mit dem Suchschlüssel. null = ungültig (wird verworfen).</summary>
    public static SucheAntwort? AntwortPruefen(ReadOnlySpan<byte> datagramm, Kopplungspaket p, IReadOnlyCollection<string> zufaelle, string? absender = null)
    {
        if (datagramm.Length is 0 or > AntwortMax) return null;
        JsonObject? o;
        try { o = JsonNode.Parse(datagramm, documentOptions: new JsonDocumentOptions { MaxDepth = 4 }) as JsonObject; }
        catch (JsonException) { return null; }
        if (o is null) return null;
        string? Text(string n) => o[n] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
        if (Text("typ") != "gefunden" || o["fassung"] is not JsonValue fv || !fv.TryGetValue<int>(out var f) || f != 1) return null;
        var zufall = Text("zufall");
        var pc = Text("pc");
        var fp = Text("fingerabdruck");
        var signatur = Text("signatur");
        if (zufall is null || !zufaelle.Contains(zufall) || pc is null || fp is null || signatur is null) return null;
        if (o["port"] is not JsonValue pv || !pv.TryGetValue<int>(out var port) || port is < 1 or > 65535) return null;
        if (o["ips"] is not JsonArray a) return null;
        var ips = new List<string>();
        foreach (var x in a)
        {
            if (x is not JsonValue iv || !iv.TryGetValue<string>(out var ip)) return null;
            ips.Add(ip);
        }
        var schluessel = p.Suchschluessel();
        try
        {
            var soll = HMACSHA256.HashData(schluessel, Encoding.UTF8.GetBytes(AntwortText(zufall, pc, port, fp, ips)));
            var ist = new byte[32];
            var status = Base64Url.DecodeFromChars(signatur, ist, out var gelesen, out var geschrieben);
            if (status != System.Buffers.OperationStatus.Done || gelesen != signatur.Length || geschrieben != 32) return null;
            if (!CryptographicOperations.FixedTimeEquals(soll, ist)) return null;
        }
        finally { CryptographicOperations.ZeroMemory(schluessel); }
        // nur IPv4-Adressen übernehmen (die Signatur deckt die Liste, wie sie kam)
        var gueltig = ips.Where(i => IPAddress.TryParse(i, out var ip) && ip.AddressFamily == AddressFamily.InterNetwork).Distinct().ToList();
        return new SucheAntwort(pc, gueltig, port, fp, absender, string.Equals(fp, p.Fingerabdruck, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>255.255.255.255 und die Broadcast-Adressen der eigenen IPv4-Netze (ohne Loopback), je am Suchport.</summary>
    public static IReadOnlyList<IPEndPoint> BroadcastZiele(int port)
    {
        var ziele = new List<IPEndPoint> { new(IPAddress.Broadcast, port) };
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback || ni.OperationalStatus is not (OperationalStatus.Up or OperationalStatus.Unknown)) continue;
                IPInterfaceProperties eig;
                try { eig = ni.GetIPProperties(); }
                catch (Exception ex) when (ex is NetworkInformationException or PlatformNotSupportedException) { continue; }
                foreach (var ua in eig.UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(ua.Address)) continue;
                    try
                    {
                        if (Broadcast(ua.Address, ua.IPv4Mask) is { } b && !ziele.Any(z => z.Address.Equals(b))) ziele.Add(new IPEndPoint(b, port));
                    }
                    catch (Exception ex) when (ex is PlatformNotSupportedException or NotImplementedException or ArgumentException) { /* ohne Maske nur 255.255.255.255 */ }
                }
            }
        }
        catch (Exception ex) when (ex is NetworkInformationException or PlatformNotSupportedException) { /* nur 255.255.255.255 */ }
        return ziele;
    }

    /// <summary>Broadcast-Adresse eines IPv4-Netzes (Adresse ODER NICHT Maske); null bei fehlender oder ungültiger Maske (auch /32 und /31).</summary>
    public static IPAddress? Broadcast(IPAddress adresse, IPAddress? maske)
    {
        if (maske is null || adresse.AddressFamily != AddressFamily.InterNetwork || maske.AddressFamily != AddressFamily.InterNetwork) return null;
        var a = adresse.GetAddressBytes();
        var m = maske.GetAddressBytes();
        var wert = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(m);
        if (wert is 0 or 0xFFFFFFFF or 0xFFFFFFFE) return null;
        var b = new byte[4];
        for (int i = 0; i < 4; i++) b[i] = (byte)(a[i] | ~m[i]);
        return new IPAddress(b);
    }

    /// <summary>
    /// Ruft per UDP an die Ziele und wartet höchstens <see cref="SucheOptionen.SucheDauer"/> auf eine gültig signierte Antwort. Zwei Anfragen (je mit eigenem
    /// Zufall, der PC beantwortet jeden nur einmal), die zweite nach der halben Dauer. null = keine gültige Antwort.
    /// </summary>
    public static async Task<SucheAntwort?> Rufen(Kopplungspaket p, IEnumerable<IPEndPoint> ziele, SucheOptionen? o = null, CancellationToken abbruch = default)
    {
        o ??= new SucheOptionen();
        var liste = ziele.ToList();
        using var udp = new UdpClient(AddressFamily.InterNetwork);
        if (OperatingSystem.IsWindows())
        {
            const int SioUdpConnreset = -1744830452;   // ICMP «Port unerreichbar» soll das Empfangen nicht abbrechen
            udp.Client.IOControl(SioUdpConnreset, [0, 0, 0, 0], null);
        }
        udp.EnableBroadcast = true;
        udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
        var zufaelle = new List<string>();
        var uhr = o.Uhr ?? (() => DateTimeOffset.UtcNow);

        async Task Senden()
        {
            var z = NeuerZufall();
            lock (zufaelle) zufaelle.Add(z);
            var d = Anfrage(p, uhr(), z);
            foreach (var ziel in liste)
            {
                try { await udp.SendAsync(d, ziel, abbruch).ConfigureAwait(false); }
                catch (SocketException) { /* Netz nicht erreichbar bzw. Broadcast verboten: nächstes Ziel */ }
            }
        }

        using var frist = CancellationTokenSource.CreateLinkedTokenSource(abbruch);
        frist.CancelAfter(o.SucheDauer);
        await Senden().ConfigureAwait(false);
        var zweite = Task.Delay(o.SucheDauer / 2, frist.Token).ContinueWith(t => t.IsCompletedSuccessfully ? Senden() : Task.CompletedTask, TaskScheduler.Default).Unwrap();
        try
        {
            while (true)
            {
                UdpReceiveResult r;
                try { r = await udp.ReceiveAsync(frist.Token).ConfigureAwait(false); }
                catch (SocketException) { continue; }
                string[] kopie;
                lock (zufaelle) kopie = [.. zufaelle];
                if (AntwortPruefen(r.Buffer, p, kopie, r.RemoteEndPoint.Address.ToString()) is { } a) return a;
            }
        }
        catch (OperationCanceledException) when (!abbruch.IsCancellationRequested) { return null; }
        finally
        {
            frist.Cancel();
            try { await zweite.ConfigureAwait(false); } catch (OperationCanceledException) { } catch (SocketException) { } catch (ObjectDisposedException) { }
        }
    }

    /// <summary>Findet den PC (siehe Dateikopf). gespeichert: zuletzt erfolgreiche Adressen (IP, Port), zuerst probiert.</summary>
    public static async Task<PcFund> Finden(Kopplungspaket p, IEnumerable<(string Ip, int Port)>? gespeichert, SucheOptionen? o = null, CancellationToken abbruch = default)
    {
        o ??= new SucheOptionen();
        var kandidaten = (gespeichert ?? []).Select(x => new PcAdresse(x.Ip, x.Port, p.Pc, "gespeichert"))
            .Concat(p.Ips.Select(ip => new PcAdresse(ip, p.Port, p.Pc, "paket")))
            .DistinctBy(a => (a.Ip, a.Port)).ToList();
        var erste = await Proben(kandidaten, p, o, abbruch).ConfigureAwait(false);
        if (erste.Zustand != PcFundZustand.NichtGefunden || !o.Suchen) return erste;

        var antwort = await Rufen(p, o.Ziele ?? BroadcastZiele(p.SuchPort), o, abbruch).ConfigureAwait(false);
        if (antwort is null)
            return new PcFund(null, PcFundZustand.NichtGefunden, $"KICAD auf {p.Pc} antwortet nicht (Adressen {string.Join(", ", kandidaten.Select(k => $"{k.Ip}:{k.Port}"))}, Suche ohne Antwort).");
        if (!antwort.FingerabdruckPasst)
            return new PcFund(null, PcFundZustand.ZertifikatAnders, $"KICAD auf {antwort.Pc} hat ein neues Zertifikat. Die Brille in KICAD neu koppeln.");
        var neu = new[] { antwort.Absender }.Concat(antwort.Ips).Where(i => i is not null).Distinct()
            .Select(ip => new PcAdresse(ip!, antwort.Port, antwort.Pc, "suche")).ToList();
        var zweite = await Proben(neu, p, o, abbruch).ConfigureAwait(false);
        return zweite.Zustand == PcFundZustand.NichtGefunden
            ? new PcFund(null, PcFundZustand.NichtGefunden, $"KICAD auf {antwort.Pc} antwortet auf die Suche, aber nicht über HTTPS (Port {antwort.Port}; Firewall?).")
            : zweite;
    }

    /// <summary>Probiert alle Adressen gleichzeitig; die erste gültige Antwort gilt. 401 irgendwo = Schlüssel ungültig.</summary>
    static async Task<PcFund> Proben(IReadOnlyList<PcAdresse> kandidaten, Kopplungspaket p, SucheOptionen o, CancellationToken abbruch)
    {
        if (kandidaten.Count == 0) return new PcFund(null, PcFundZustand.NichtGefunden, "Keine Adresse zum Probieren.");
        using var alle = CancellationTokenSource.CreateLinkedTokenSource(abbruch);
        var laufend = kandidaten.Select(async k =>
        {
            using var z = o.ZugangFabrik?.Invoke(k) ?? new PcZugang(k, p, frist: o.ProbeFrist);
            using var frist = CancellationTokenSource.CreateLinkedTokenSource(alle.Token);
            frist.CancelAfter(o.ProbeFrist);
            return (Adresse: k, Ergebnis: await z.Probe(frist.Token).ConfigureAwait(false));
        }).ToList();
        bool ungueltig = false;
        while (laufend.Count > 0)
        {
            var fertig = await Task.WhenAny(laufend).ConfigureAwait(false);
            laufend.Remove(fertig);
            var (adresse, e) = await fertig.ConfigureAwait(false);
            if (e.Ok)
            {
                alle.Cancel();
                try { await Task.WhenAll(laufend).ConfigureAwait(false); } catch (OperationCanceledException) { }
                return new PcFund(adresse with { Pc = e.Pc ?? adresse.Pc }, PcFundZustand.Gefunden, $"Verbunden mit {e.Pc ?? adresse.Pc} ({adresse.Ip}:{adresse.Port}).");
            }
            if (e.SchluesselUngueltig) ungueltig = true;
        }
        return ungueltig
            ? new PcFund(null, PcFundZustand.SchluesselUngueltig, $"KICAD auf {p.Pc} kennt diese Brille nicht mehr (Kopplung widerrufen oder ersetzt). Die Brille in KICAD neu koppeln.")
            : new PcFund(null, PcFundZustand.NichtGefunden, $"KICAD auf {p.Pc} antwortet nicht.");
    }
}
