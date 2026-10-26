// Quest App P4a (Modul XR): das Kopplungspaket der App lesen, prüfen und im privaten App-Speicher ablegen. Ohne StereoKit, am PC testbar.
//
// Format wie KICAD es ausgibt (POST /api/xr/kopplung/paket, src/Hlks.Web/XrKopplung.cs; OBERFLAECHE.md «Modul XR: Zugang im Büronetz»):
//   { art: "hlks-xr-kopplung", fassung: 1, pc, ips[], port, suchPort, fingerabdruck, kopplung, schluessel, brille, name, angelegt }
// Angenommen wird auch die ganze Antwort des Endpunkts { paket: { … } }.
// H+K XR E1 (entwurf/xr-empfaenger.md, «Schnittstellen im Bau E1»): Fassung 2 = Fassung 1 plus «werkzeug» (kicad | xrlabor | …, Muster [a-z][a-z0-9_-]{0,31}) und
// «werkzeugName» («KICAD» | «XR-Labor»). Eine Fassung 1 ohne «werkzeug» gilt als «kicad» (KICAD vor E1). Die App schreibt in ihre Ablage immer Fassung 2.
//
// Ablage (KopplungAblage): Die App liest die Pakete nur aus ihrem privaten Speicher (Android: Context.FilesDir, nur für die App lesbar), je Werkzeug eines:
// kopplungen/<werkzeug>.json. Das Werkzeug bringt sein Paket bei der Installation per adb in den Eingang der App (Android: /sdcard/Android/data/ch.hkplanung.xr/
// files/kopplung_<werkzeug>.json – diesen Ordner kann adb ohne run-as beschreiben); ein altes «kopplung.json» gilt als «kicad». Die App übernimmt jedes beim Start
// in den privaten Speicher und löscht es im Eingang (auch ein ungültiges, damit kein Schlüssel im geteilten Speicher liegen bleibt). Ein zweites Werkzeug fügt nur
// seine Kopplung hinzu; eine neue Kopplung desselben Werkzeugs ersetzt die alte. Am PC: --kopplung <datei> (mehrfach) liest die Dateien direkt.
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

/// <summary>
/// Das geprüfte Kopplungspaket. Enthält den Schlüssel im Klartext: nur im privaten App-Speicher ablegen, nie ausgeben. H+K XR E1: <see cref="Werkzeug"/> und
/// <see cref="WerkzeugName"/> (Fassung 2; aus Fassung 1 «kicad» / «KICAD»).
/// </summary>
public sealed record Kopplungspaket(string Pc, IReadOnlyList<string> Ips, int Port, int SuchPort, string Fingerabdruck, string Kopplung,
    string Schluessel, string Brille, string Name, DateTimeOffset? Angelegt)
{
    public const string Art = "hlks-xr-kopplung";
    /// <summary>Fassung, die die App schreibt (H+K XR E1: mit Werkzeug).</summary>
    public const int Fassung = 2;
    /// <summary>Älteste Fassung, die die App liest (ohne «werkzeug» = KICAD).</summary>
    public const int FassungAlt = 1;
    /// <summary>Werkzeug eines Pakets der Fassung 1 (KICAD vor H+K XR E1).</summary>
    public const string WerkzeugAlt = "kicad", WerkzeugNameAlt = "KICAD";
    /// <summary>Höchstens so viele Zeichen hat der Anzeigename des Werkzeugs (wie am PC, XrKopplungen.WerkzeugNamePruefen).</summary>
    public const int WerkzeugNameMax = 40;

    /// <summary>Kennung des Werkzeugs, mit dem diese Kopplung besteht (Fassung 2 «werkzeug»; Fassung 1: «kicad»).</summary>
    public string Werkzeug { get; init; } = WerkzeugAlt;
    /// <summary>Anzeigename des Werkzeugs (Fassung 2 «werkzeugName»; Fassung 1: «KICAD»).</summary>
    public string WerkzeugName { get; init; } = WerkzeugNameAlt;
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
        && Fingerabdruck == o.Fingerabdruck && Kopplung == o.Kopplung && Schluessel == o.Schluessel && Brille == o.Brille && Name == o.Name && Angelegt == o.Angelegt
        && Werkzeug == o.Werkzeug && WerkzeugName == o.WerkzeugName;

    public override int GetHashCode() => HashCode.Combine(Pc, Port, Kopplung, Fingerabdruck, Werkzeug);

    /// <summary>Ohne Schlüssel (Records geben sonst alle Felder aus).</summary>
    public override string ToString() => $"Kopplung {Kopplung} mit {WerkzeugName} auf {Pc} ({string.Join(", ", Ips)}:{Port}, Suche {SuchPort})";

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
        var fassung = Ganz(o, "fassung");
        if (fassung is not (>= FassungAlt and <= Fassung))
            throw Fehler($"Die Fassung des Kopplungspakets wird nicht unterstützt (erwartet {FassungAlt} bis {Fassung}).", "App und Werkzeug am PC auf denselben Stand bringen.");
        // H+K XR E1: Fassung 2 nennt das Werkzeug (Pflicht); Fassung 1 ist KICAD
        string werkzeug = WerkzeugAlt, werkzeugName = WerkzeugNameAlt;
        if (fassung >= 2)
        {
            var w = Text(o, "werkzeug")?.Trim();
            if (!Netz.Werkzeug.KennungGueltig(w))
                throw Fehler("«werkzeug» fehlt oder ist ungültig (Kleinbuchstaben, Ziffern, _ und -, höchstens 32 Zeichen, mit einem Buchstaben beginnend).");
            var wn = Text(o, "werkzeugName")?.Trim();
            if (string.IsNullOrEmpty(wn) || wn.Length > WerkzeugNameMax || wn.Any(char.IsControl)) throw Fehler("«werkzeugName» fehlt oder ist ungültig (1 bis 40 Zeichen).");
            (werkzeug, werkzeugName) = (w!, wn);
        }
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
        return new Kopplungspaket(pc.Trim(), ips, port.Value, suchPort.Value, fp, kopplung, schluessel!, brille, name, angelegt)
        {
            Werkzeug = werkzeug, WerkzeugName = werkzeugName,
        };
    }

    /// <summary>Das Paket als JSON (Format wie vom PC, Fassung 2 mit Werkzeug), zum Ablegen im privaten Speicher.</summary>
    public string AlsJson()
    {
        var o = new JsonObject
        {
            ["art"] = Art, ["fassung"] = Fassung, ["pc"] = Pc, ["ips"] = new JsonArray([.. Ips.Select(i => (JsonNode)JsonValue.Create(i))]),
            ["port"] = Port, ["suchPort"] = SuchPort, ["fingerabdruck"] = Fingerabdruck, ["kopplung"] = Kopplung, ["schluessel"] = Schluessel,
            ["brille"] = Brille, ["name"] = Name, ["werkzeug"] = Werkzeug, ["werkzeugName"] = WerkzeugName,
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

/// <summary>Eine Kopplung im privaten Speicher: Werkzeug (Name der Datei), das Paket bzw. warum es nicht lesbar ist.</summary>
public sealed record KopplungEintrag(string Werkzeug, Kopplungspaket? Paket, string? Fehler);

/// <summary>Ablage der Kopplungspakete im App-Speicher, je Werkzeug eines (siehe Dateikopf).</summary>
public static class KopplungAblage
{
    /// <summary>Altes Paket (bis H+K XR E1): im Eingang gilt es als «kicad»; im privaten Ordner (eine Kopplung je App) zieht es nach <see cref="Ordner"/> um.</summary>
    public const string Dateiname = "kopplung.json";
    /// <summary>Unterordner des privaten Speichers mit einer Datei je Werkzeug (&lt;werkzeug&gt;.json).</summary>
    public const string Ordner = "kopplungen";
    /// <summary>Höchstens so viele Kopplungen liest die App (Schutz gegen volle Ordner; mehr Werkzeuge gibt es nicht).</summary>
    public const int Max = 16;

    /// <summary>Name der Eingangsdatei eines Werkzeugs: kopplung_&lt;werkzeug&gt;.json (wie am PC, QuestAppMarke.KopplungDatei).</summary>
    public static string EingangDatei(string werkzeug) => "kopplung_" + werkzeug + ".json";

    /// <summary>Datei der Kopplung eines Werkzeugs im privaten Speicher.</summary>
    public static string PrivatDatei(string privat, string werkzeug) => Path.Combine(privat, Ordner, werkzeug + ".json");

    /// <summary>
    /// Werkzeug, für das eine Eingangsdatei gilt: kopplung_&lt;werkzeug&gt;.json → werkzeug, das alte kopplung.json → «kicad»; sonst null (keine Kopplung).
    /// </summary>
    public static string? WerkzeugDerDatei(string dateiname)
    {
        if (dateiname == Dateiname) return Kopplungspaket.WerkzeugAlt;
        if (!dateiname.StartsWith("kopplung_", StringComparison.Ordinal) || !dateiname.EndsWith(".json", StringComparison.Ordinal)) return null;
        var w = dateiname["kopplung_".Length..^".json".Length];
        return Werkzeug.KennungGueltig(w) ? w : null;
    }

    /// <summary>
    /// Übernimmt alle per adb abgelegten Pakete aus dem Eingang (kopplung_&lt;werkzeug&gt;.json, das alte kopplung.json = «kicad») in den privaten Ordner und löscht
    /// sie im Eingang – auch ungültige. Das Werkzeug im Paket muss zum Namen der Datei passen (Fassung 1 = «kicad»). Zieht zudem ein altes privates kopplung.json
    /// nach kopplungen/&lt;werkzeug&gt;.json um (eine Kopplung je Werkzeug; eine schon vorhandene bleibt). Rückgabe: null, wenn nichts zu melden war, sonst die
    /// Meldungen (je Datei eine, mit «·» getrennt).
    /// </summary>
    public static string? Importieren(string? eingang, string privat)
    {
        var meldungen = new List<string>();
        Umziehen(privat, meldungen);
        if (!string.IsNullOrEmpty(eingang) && Directory.Exists(eingang))
        {
            foreach (var quelle in Directory.EnumerateFiles(eingang, "kopplung*.json").Order(StringComparer.Ordinal).ToList())
            {
                var name = Path.GetFileName(quelle);
                var werkzeug = WerkzeugDerDatei(name);
                if (werkzeug is null) continue;   // fremde Datei: liegen lassen
                try
                {
                    var p = Kopplungspaket.Lesen(File.ReadAllText(quelle, Encoding.UTF8));
                    if (p.Werkzeug != werkzeug)
                        meldungen.Add($"Die neue Kopplungsdatei {name} passt nicht: sie gilt für «{p.WerkzeugName}» ({p.Werkzeug}), nicht für «{werkzeug}».");
                    else Speichern(p, privat);
                }
                catch (XrNetzFehler f) { meldungen.Add($"Die neue Kopplungsdatei{(name == Dateiname ? "" : " " + name)} ist ungültig: " + f.Message); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { meldungen.Add($"Die neue Kopplungsdatei {name} liess sich nicht übernehmen: " + ex.Message); }
                finally
                {
                    try { File.Delete(quelle); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* bleibt liegen; nächster Start versucht es wieder */ }
                }
            }
        }
        return meldungen.Count == 0 ? null : string.Join(" · ", meldungen);
    }

    /// <summary>Altes privates kopplung.json (eine Kopplung je App, bis E1) nach kopplungen/&lt;werkzeug&gt;.json; eine schon vorhandene neue bleibt.</summary>
    static void Umziehen(string privat, List<string> meldungen)
    {
        var alt = Path.Combine(privat, Dateiname);
        if (!File.Exists(alt)) return;
        try
        {
            var p = Kopplungspaket.Lesen(File.ReadAllText(alt, Encoding.UTF8));
            if (!File.Exists(PrivatDatei(privat, p.Werkzeug))) Speichern(p, privat);
            File.Delete(alt);
        }
        catch (XrNetzFehler f) { meldungen.Add("Die bisherige Kopplung ist ungültig: " + f.Message); TryDelete(alt); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { meldungen.Add("Die bisherige Kopplung liess sich nicht übernehmen: " + ex.Message); }
    }

    static void TryDelete(string datei)
    {
        try { File.Delete(datei); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Legt das Paket atomar im privaten Ordner ab (kopplungen/&lt;werkzeug&gt;.json; unter Linux/Android nur für den Benutzer lesbar).</summary>
    public static void Speichern(Kopplungspaket p, string privat)
    {
        var ziel = PrivatDatei(privat, p.Werkzeug);
        Directory.CreateDirectory(Path.GetDirectoryName(ziel)!);
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

    /// <summary>Liest das Paket eines Werkzeugs aus dem privaten Ordner (Vorgabe KICAD); null, wenn es keines gibt. Wirft, wenn es ungültig ist.</summary>
    public static Kopplungspaket? Laden(string privat, string werkzeug = Kopplungspaket.WerkzeugAlt) => LadenDatei(PrivatDatei(privat, werkzeug));

    /// <summary>
    /// Alle Kopplungen im privaten Ordner, in der Reihenfolge der Startseite (<see cref="Werkzeug.Ordnen"/>). Eine unlesbare bzw. eine, deren Werkzeug nicht zum
    /// Namen der Datei passt, kommt mit Fehler (ohne Paket). Höchstens <see cref="Max"/>.
    /// </summary>
    public static IReadOnlyList<KopplungEintrag> Alle(string privat)
    {
        var ordner = Path.Combine(privat, Ordner);
        if (!Directory.Exists(ordner)) return [];
        var werkzeuge = Directory.EnumerateFiles(ordner, "*.json").Select(d => Path.GetFileNameWithoutExtension(d)).Where(Werkzeug.KennungGueltig).ToList();
        var aus = new List<KopplungEintrag>();
        foreach (var w in Werkzeug.Ordnen(werkzeuge).Take(Max))
        {
            try
            {
                var p = Laden(privat, w);
                aus.Add(p is null ? new(w, null, "Die Kopplung fehlt.") : p.Werkzeug == w ? new(w, p, null)
                    : new(w, null, $"Die Kopplung von «{w}» gilt für ein anderes Werkzeug ({p.Werkzeug})."));
            }
            catch (XrNetzFehler f) { aus.Add(new(w, null, f.Message)); }
        }
        return aus;
    }
}
