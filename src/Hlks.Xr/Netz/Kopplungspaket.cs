// Quest App P4a (Modul XR): das Kopplungspaket der App lesen, prüfen und im privaten App-Speicher ablegen. Ohne StereoKit, am PC testbar.
//
// Format wie KICAD es ausgibt (POST /api/xr/kopplung/paket, src/Hlks.Web/XrKopplung.cs; OBERFLAECHE.md «Modul XR: Zugang im Büronetz»):
//   { art: "hlks-xr-kopplung", fassung: 1, pc, ips[], port, suchPort, fingerabdruck, kopplung, schluessel, brille, name, angelegt }
// Angenommen wird auch die ganze Antwort des Endpunkts { paket: { … } }.
//
// Ablage (KopplungAblage): Die App liest das Paket nur aus ihrem privaten Speicher (Android: Context.FilesDir, nur für die App lesbar).
// P7 bringt es bei der Installation per adb in den Eingang der App (Android: /sdcard/Android/data/ch.hkplanung.kicad.quest/files/kopplung.json –
// diesen Ordner kann adb ohne run-as beschreiben); die App übernimmt es beim Start in den privaten Speicher und löscht es im Eingang
// (auch ein ungültiges, damit kein Schlüssel im geteilten Speicher liegen bleibt). Am PC: --kopplung <datei> liest die Datei direkt.
//
// Der Schlüssel erscheint nie in Texten der App (ToString ist überschrieben); er geht nur als Bearer-Kopfzeile über TLS an den gepinnten PC.

using System.Buffers.Text;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Hlks.Xr.Netz;

/// <summary>Fehler im Netz-Teil der App, mit Meldung für den Menschen und optionalem Vorschlag.</summary>
public sealed class XrNetzFehler(string meldung, string? vorschlag = null) : Exception(meldung)
{
    public string? Vorschlag { get; } = vorschlag;
}

/// <summary>Das geprüfte Kopplungspaket. Enthält den Schlüssel im Klartext: nur im privaten App-Speicher ablegen, nie ausgeben.</summary>
public sealed record Kopplungspaket(string Pc, IReadOnlyList<string> Ips, int Port, int SuchPort, string Fingerabdruck, string Kopplung,
    string Schluessel, string Brille, string Name, DateTimeOffset? Angelegt)
{
    public const string Art = "hlks-xr-kopplung";
    public const int Fassung = 1;
    /// <summary>Höchstens so viele IP-Adressen werden aus dem Paket übernommen.</summary>
    public const int IpsMax = 16;

    static readonly Regex HexMuster = new("^[0-9a-f]{64}$", RegexOptions.CultureInvariant);
    static readonly Regex IdMuster = new("^[0-9a-f]{16}$", RegexOptions.CultureInvariant);
    static readonly Regex SchluesselMuster = new("^[A-Za-z0-9_-]{43}$", RegexOptions.CultureInvariant);

    /// <summary>«&lt;kopplung&gt;.&lt;schluessel&gt;» für die Kopfzeile Authorization: Bearer.</summary>
    public string Token => $"{Kopplung}.{Schluessel}";

    /// <summary>Die 32 Byte des Schlüssels.</summary>
    public byte[] SchluesselBytes() => SchluesselLesen(Schluessel) ?? throw new XrNetzFehler("Der Schlüssel im Kopplungspaket ist ungültig.");

    /// <summary>Suchschlüssel = HMAC-SHA256(Schlüssel, "hlks-xr-suche") – wie XrSchluessel.Suchschluessel am PC.</summary>
    public byte[] Suchschluessel()
    {
        var s = SchluesselBytes();
        try { return HMACSHA256.HashData(s, "hlks-xr-suche"u8); }
        finally { CryptographicOperations.ZeroMemory(s); }
    }

    /// <summary>Kurzform des Fingerabdrucks für den Vergleich durch den Menschen (wie XrZertifikat.Kurz am PC): «AB12 CD34 EF56 7890».</summary>
    public string FingerabdruckKurz => $"{Fingerabdruck[..4]} {Fingerabdruck[4..8]} {Fingerabdruck[8..12]} {Fingerabdruck[12..16]}".ToUpperInvariant();

    /// <summary>Gleich, wenn alle Felder gleich sind (die Liste der IP-Adressen nach Inhalt).</summary>
    public bool Equals(Kopplungspaket? o) => o is not null && Pc == o.Pc && Ips.SequenceEqual(o.Ips) && Port == o.Port && SuchPort == o.SuchPort
        && Fingerabdruck == o.Fingerabdruck && Kopplung == o.Kopplung && Schluessel == o.Schluessel && Brille == o.Brille && Name == o.Name && Angelegt == o.Angelegt;

    public override int GetHashCode() => HashCode.Combine(Pc, Port, Kopplung, Fingerabdruck);

    /// <summary>Ohne Schlüssel (Records geben sonst alle Felder aus).</summary>
    public override string ToString() => $"Kopplung {Kopplung} mit {Pc} ({string.Join(", ", Ips)}:{Port}, Suche {SuchPort})";

    static byte[]? SchluesselLesen(string? text)
    {
        if (text is null || !SchluesselMuster.IsMatch(text)) return null;
        var b = new byte[32];
        var status = Base64Url.DecodeFromChars(text, b, out var gelesen, out var geschrieben);
        return status == System.Buffers.OperationStatus.Done && gelesen == text.Length && geschrieben == 32 ? b : null;
    }

    /// <summary>Liest und prüft ein Paket (JSON). Wirft <see cref="XrNetzFehler"/> mit einer Meldung, die sagt, was nicht stimmt.</summary>
    public static Kopplungspaket Lesen(string json)
    {
        JsonNode? n;
        try { n = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { MaxDepth = 8 }); }
        catch (JsonException) { throw Fehler("Die Kopplungsdatei ist kein gültiges JSON."); }
        if (n is JsonObject h && h["paket"] is JsonObject innen) n = innen;
        if (n is not JsonObject o) throw Fehler("Die Kopplungsdatei muss ein JSON-Objekt sein.");

        if (Text(o, "art") != Art) throw Fehler($"Die Datei ist kein Kopplungspaket von KICAD («art» muss «{Art}» sein).");
        if (Ganz(o, "fassung") != Fassung) throw Fehler($"Die Fassung des Kopplungspakets wird nicht unterstützt (erwartet {Fassung}).", "App und KICAD auf denselben Stand bringen.");
        var pc = Text(o, "pc");
        if (string.IsNullOrWhiteSpace(pc) || pc.Length > 100 || pc.Any(char.IsControl)) throw Fehler("«pc» fehlt oder ist ungültig.");
        var ips = new List<string>();
        if (o["ips"] is JsonArray a)
        {
            foreach (var x in a)
            {
                var t = x is JsonValue v && v.TryGetValue<string>(out var s) ? s.Trim() : null;
                if (t is null || !IPAddress.TryParse(t, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork)
                    throw Fehler($"«ips» enthält keine gültige IPv4-Adresse: «{Kuerzen(x?.ToJsonString())}».");
                if (!ips.Contains(t) && ips.Count < IpsMax) ips.Add(t);
            }
        }
        else if (o["ips"] is not null) throw Fehler("«ips» muss eine Liste sein.");
        var port = Ganz(o, "port");
        var suchPort = Ganz(o, "suchPort");
        if (port is not (>= 1 and <= 65535)) throw Fehler("«port» muss von 1 bis 65535 sein.");
        if (suchPort is not (>= 1 and <= 65535)) throw Fehler("«suchPort» muss von 1 bis 65535 sein.");
        var fp = Text(o, "fingerabdruck")?.Trim().ToLowerInvariant();
        if (fp is null || !HexMuster.IsMatch(fp)) throw Fehler("«fingerabdruck» muss der SHA-256-Fingerabdruck (64 Zeichen hex) sein.");
        var kopplung = Text(o, "kopplung");
        if (kopplung is null || !IdMuster.IsMatch(kopplung)) throw Fehler("«kopplung» muss eine Kennung aus 16 Zeichen hex sein.");
        var schluessel = Text(o, "schluessel");
        var roh = SchluesselLesen(schluessel) ?? throw Fehler("«schluessel» muss 32 Byte in base64url (43 Zeichen) sein.");
        CryptographicOperations.ZeroMemory(roh);
        var brille = Text(o, "brille")?.Trim();
        if (string.IsNullOrEmpty(brille) || brille.Length > 64 || brille.Any(char.IsControl)) throw Fehler("«brille» fehlt oder ist ungültig.");
        var name = Text(o, "name")?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 60 || name.Any(char.IsControl)) name = brille;
        DateTimeOffset? angelegt = DateTimeOffset.TryParse(Text(o, "angelegt"), System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal, out var z) ? z : null;
        return new Kopplungspaket(pc.Trim(), ips, port.Value, suchPort.Value, fp, kopplung, schluessel!, brille, name, angelegt);
    }

    /// <summary>Das Paket als JSON (Format wie von KICAD), zum Ablegen im privaten Speicher.</summary>
    public string AlsJson()
    {
        var o = new JsonObject
        {
            ["art"] = Art, ["fassung"] = Fassung, ["pc"] = Pc, ["ips"] = new JsonArray([.. Ips.Select(i => (JsonNode)JsonValue.Create(i))]),
            ["port"] = Port, ["suchPort"] = SuchPort, ["fingerabdruck"] = Fingerabdruck, ["kopplung"] = Kopplung, ["schluessel"] = Schluessel,
            ["brille"] = Brille, ["name"] = Name,
        };
        if (Angelegt is { } a) o["angelegt"] = a.ToString("o", System.Globalization.CultureInfo.InvariantCulture);
        return o.ToJsonString();
    }

    static XrNetzFehler Fehler(string meldung, string? vorschlag = null) =>
        new(meldung, vorschlag ?? "Die Brille in KICAD neu koppeln («Quest verbinden» › «App installieren/aktualisieren»).");

    static string? Text(JsonObject o, string name) => o[name] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    static int? Ganz(JsonObject o, string name) => o[name] is JsonValue v && v.TryGetValue<int>(out var i) ? i : null;

    static string Kuerzen(string? s) => s is null ? "" : s.Length <= 30 ? s : s[..30] + "…";
}

/// <summary>Ablage des Kopplungspakets im App-Speicher (siehe Dateikopf).</summary>
public static class KopplungAblage
{
    public const string Dateiname = "kopplung.json";

    /// <summary>
    /// Übernimmt eine per adb abgelegte Datei aus dem Eingang in den privaten Ordner und löscht sie im Eingang. Rückgabe: null, wenn nichts zu tun war
    /// oder die Übernahme gelang, sonst die Fehlermeldung (die Datei im Eingang ist dann trotzdem gelöscht).
    /// </summary>
    public static string? Importieren(string? eingang, string privat)
    {
        if (string.IsNullOrEmpty(eingang)) return null;
        var quelle = Path.Combine(eingang, Dateiname);
        if (!File.Exists(quelle)) return null;
        try
        {
            var p = Kopplungspaket.Lesen(File.ReadAllText(quelle, Encoding.UTF8));
            Speichern(p, privat);
            return null;
        }
        catch (XrNetzFehler f) { return "Die neue Kopplungsdatei ist ungültig: " + f.Message; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return "Die neue Kopplungsdatei liess sich nicht übernehmen: " + ex.Message; }
        finally
        {
            try { File.Delete(quelle); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* bleibt liegen; nächster Start versucht es wieder */ }
        }
    }

    /// <summary>Legt das Paket atomar im privaten Ordner ab (unter Linux/Android nur für den Benutzer lesbar).</summary>
    public static void Speichern(Kopplungspaket p, string privat)
    {
        Directory.CreateDirectory(privat);
        var ziel = Path.Combine(privat, Dateiname);
        var temp = ziel + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
        File.WriteAllText(temp, p.AlsJson(), new UTF8Encoding(false));
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(temp, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.Move(temp, ziel, true);
    }

    /// <summary>Liest das Paket aus einer Datei; null, wenn sie fehlt. Wirft <see cref="XrNetzFehler"/>, wenn sie ungültig ist.</summary>
    public static Kopplungspaket? LadenDatei(string datei)
    {
        if (!File.Exists(datei)) return null;
        try { return Kopplungspaket.Lesen(File.ReadAllText(datei, Encoding.UTF8)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { throw new XrNetzFehler("Die Kopplungsdatei ist nicht lesbar: " + ex.Message); }
    }

    /// <summary>Liest das Paket aus dem privaten Ordner; null, wenn es keines gibt.</summary>
    public static Kopplungspaket? Laden(string privat) => LadenDatei(Path.Combine(privat, Dateiname));
}
