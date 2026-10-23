// Kopplung der Quest App mit diesem PC (Modul XR, Quest App P3). Je Brille eine Kopplung; mehrere Brillen möglich (höchstens 8).
//
// SCHLÜSSEL (XrSchluessel, rein):
//   Je Kopplung eine Kennung (16 hex, zufällig) und ein Schlüssel (32 zufällige Byte, als base64url ohne Auffüllung, 43 Zeichen).
//   Die App sendet bei jeder Anfrage  Authorization: Bearer <kopplung>.<schluessel>
//   Gespeichert wird NIE der Schlüssel, nur zwei daraus abgeleitete Werte:
//     Prüfwert       = HMAC-SHA256(Schlüssel, "hlks-xr-zugang")   – zum Prüfen des Bearer-Schlüssels (Vergleich in fester Zeit)
//     Suchschlüssel  = HMAC-SHA256(Schlüssel, "hlks-xr-suche")    – zum Prüfen der Signatur einer Suchanfrage (UDP, XrSuche.cs); mit DPAPI geschützt
//   Wer die Ablage liest, kann damit keine Anfrage an /api/xr stellen (dafür braucht es den Schlüssel selbst).
//
// EINE STELLE für die Prüfung des Schlüssels: XrKopplungen.Pruefen(Authorization-Kopfzeile). Der Netzzugang (XrZugang.cs, XrPforte) und die
// Brillen-Endpunkte auf localhost rufen nur diese Methode.
//
// WEG A (bei der Installation, Knopf folgt in P7): PaketErzeugen erzeugt das Kopplungspaket (JSON: PC-Name, IP-Adressen, Port, Suchport,
//   Fingerabdruck des Zertifikats, Kopplung, Schlüssel, Brille). KICAD gibt es nur einmal heraus (POST /api/xr/kopplung/paket, nur localhost) und
//   bringt es per adb in die App; es wird nirgends gespeichert.
// WEG B (Ersatz ohne Kabel): Die Brille fragt an (POST /api/xr/kopplung/anfrage, ohne Schlüssel, streng begrenzt). KICAD zeigt am PC einen
//   6-stelligen Code. Der Mensch gibt den Code in der Brille ein UND bestätigt am PC (POST /api/xr/kopplung/bestaetigen, nur localhost). Erst dann
//   gibt POST /api/xr/kopplung/abholen { anfrage, code } das Paket heraus. Frist 5 min, höchstens 5 falsche Codes je Anfrage, höchstens 3 offene und
//   20 neue Anfragen je Stunde; abholen nur von der IP-Adresse, die angefragt hat. Anfragen leben nur im Speicher.
// WIDERRUF: Widerrufen(kopplung) löscht die Prüfwerte; laufende Kanal-Verbindungen dieser Kopplung trennt der Kanal (Ereignis KopplungEntfernt).
//   Eine neue Kopplung für dieselbe Brille ersetzt die alte.
//
// ABLAGE: <XrEinstellungen.Ordner>\kopplungen.json (Vorgabe %LOCALAPPDATA%\HLKS_CAD\xr) – je PC, weil das Zertifikat (und damit der Fingerabdruck,
// den die App pinnt) je PC ist; nicht in den Benutzereinstellungen (OneDrive). Zwei KICAD-Prozesse auf demselben PC: Schreiben unter einer Sperrdatei,
// Lesen merkt Änderungen am Zeitstempel der Datei.

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Hlks.Kern;

namespace Hlks.Web;

/// <summary>Fehler mit HTTP-Status (Kopplung und Netzzugang).</summary>
public sealed class XrFehler(int status, string meldung, string? vorschlag = null) : Exception(meldung)
{
    public int Status { get; } = status;
    public string? Vorschlag { get; } = vorschlag;
}

/// <summary>Schlüssel und abgeleitete Werte (rein).</summary>
public static class XrSchluessel
{
    public const int Laenge = 32;
    static readonly Regex IdMuster = new("^[0-9a-f]{16}$", RegexOptions.CultureInvariant);
    static readonly Regex TextMuster = new("^[A-Za-z0-9_-]{43}$", RegexOptions.CultureInvariant);

    public static (string Id, byte[] Schluessel) Neu() => (Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8)), RandomNumberGenerator.GetBytes(Laenge));

    public static bool IdGueltig(string? id) => id is not null && IdMuster.IsMatch(id);

    /// <summary>Der Schlüssel als Text (base64url, ohne Auffüllung).</summary>
    public static string Text(byte[] schluessel) => Base64Url.EncodeToString(schluessel);

    /// <summary>Schlüssel aus Text; null, wenn es nicht genau 32 Byte in base64url sind.</summary>
    public static byte[]? AusText(string? text) => text is not null && TextMuster.IsMatch(text) ? Base64UrlLesen(text, Laenge) : null;

    /// <summary>Liest genau «laenge» Byte aus base64url; null bei jeder Abweichung (wirft nie – auch nicht bei nicht kanonischen Endzeichen).</summary>
    public static byte[]? Base64UrlLesen(string text, int laenge)
    {
        var b = new byte[laenge];
        var status = Base64Url.DecodeFromChars(text, b, out var gelesen, out var geschrieben);
        return status == System.Buffers.OperationStatus.Done && gelesen == text.Length && geschrieben == laenge ? b : null;
    }

    public static byte[] Pruefwert(ReadOnlySpan<byte> schluessel) => HMACSHA256.HashData(schluessel, "hlks-xr-zugang"u8);
    public static byte[] Suchschluessel(ReadOnlySpan<byte> schluessel) => HMACSHA256.HashData(schluessel, "hlks-xr-suche"u8);

    /// <summary>Liest «Bearer &lt;kopplung&gt;.&lt;schluessel&gt;»; null bei jeder Abweichung.</summary>
    public static (string Id, byte[] Schluessel)? AusKopfzeile(string? authorization)
    {
        if (string.IsNullOrEmpty(authorization) || authorization.Length > 120) return null;
        const string Art = "Bearer ";
        if (!authorization.StartsWith(Art, StringComparison.OrdinalIgnoreCase)) return null;
        var wert = authorization[Art.Length..].Trim();
        var punkt = wert.IndexOf('.');
        if (punkt <= 0) return null;
        var id = wert[..punkt];
        if (!IdGueltig(id)) return null;
        var s = AusText(wert[(punkt + 1)..]);
        return s is null ? null : (id, s);
    }
}

/// <summary>Eine gekoppelte Brille (ohne Geheimnisse). Weg: «paket» (Weg A) oder «code» (Weg B).</summary>
public sealed record XrKopplung(string Id, string Brille, string Name, string Weg, DateTimeOffset Angelegt);

/// <summary>Was die App über den Zugang wissen muss (für Paket und Suche).</summary>
public sealed record XrZugangInfo(string Pc, IReadOnlyList<string> Ips, int Port, int SuchPort, string Fingerabdruck);

/// <summary>Das Kopplungspaket (Weg A und B). Enthält den Schlüssel im Klartext: nur einmal ausgeben, nie speichern.</summary>
public sealed record XrPaket(string Art, int Fassung, string Pc, IReadOnlyList<string> Ips, int Port, int SuchPort, string Fingerabdruck,
    string Kopplung, string Schluessel, string Brille, string Name, DateTimeOffset Angelegt)
{
    public const string ArtName = "hlks-xr-kopplung";
}

/// <summary>Eine offene Kopplungsanfrage (Weg B), wie der PC sie sieht (mit Code).</summary>
public sealed record XrAnfrageStand(string Anfrage, string Brille, string Name, string? Ip, string Code, DateTimeOffset Bis, bool Bestaetigt, bool Abgelehnt, int Versuche);

/// <summary>Antwort auf «abholen»: Status 200 (gekoppelt, mit Paket), 202 (wartet auf den PC), 400, 403 (falsch, abgelehnt), 404 (unbekannt), 410 (abgelaufen, gesperrt).</summary>
public sealed record XrAbholErgebnis(int Status, string Zustand, XrPaket? Paket, string? Meldung, int? VersucheUebrig);

public sealed class XrKopplungen
{
    static readonly Regex BrilleMuster = new(@"^[A-Za-z0-9][A-Za-z0-9._:\-]{0,63}$", RegexOptions.CultureInvariant);
    static readonly Regex CodeMuster = new(@"^[0-9]{6}$", RegexOptions.CultureInvariant);
    static readonly Regex AnfrageMuster = new(@"^[A-Za-z0-9_-]{22}$", RegexOptions.CultureInvariant);
    static readonly byte[] Leer = new byte[32];
    const int NameMax = 60;

    sealed record Eintrag(XrKopplung Kopplung, byte[] Pruefwert, byte[]? Suchschluessel);

    sealed class Anfrage
    {
        public required string Id;
        public required string Brille;
        public required string Name;
        public string? Ip;
        public required string Code;
        public DateTimeOffset Bis;
        public int Versuche;
        public bool Bestaetigt;
        public bool Abgelehnt;
        public XrAnfrageStand Stand() => new(Id, Brille, Name, Ip, Code, Bis, Bestaetigt, Abgelehnt, Versuche);
    }

    // Dateiformat
    sealed class Ablage { public int Format { get; set; } = 1; public List<AblageEintrag> Kopplungen { get; set; } = []; }
    sealed class AblageEintrag
    {
        public string Id { get; set; } = "";
        public string Brille { get; set; } = "";
        public string Name { get; set; } = "";
        public string Weg { get; set; } = "";
        public DateTimeOffset Angelegt { get; set; }
        public string Pruefwert { get; set; } = "";
        public string? Suche { get; set; }
    }

    readonly XrEinstellungen einstellungen;
    readonly object sperre = new();
    List<Eintrag> eintraege = [];
    bool geladen;
    DateTime geladenStand;
    long letzterBlick;
    readonly List<Anfrage> anfragen = [];
    readonly List<DateTimeOffset> neueAnfragen = [];
    readonly Dictionary<string, DateTimeOffset> zuletzt = new(StringComparer.Ordinal);

    public XrKopplungen(XrEinstellungen einstellungen) { this.einstellungen = einstellungen; }

    /// <summary>Eine Kopplung wurde widerrufen oder durch eine neue derselben Brille ersetzt (Kennung der alten).</summary>
    public event Action<string>? KopplungEntfernt;

    public string DateiPfad => Path.Combine(einstellungen.OrdnerPfad(), "kopplungen.json");

    // ---------------------------------------------------------------- Prüfen (EINE Stelle)

    /// <summary>Prüft die Authorization-Kopfzeile «Bearer &lt;kopplung&gt;.&lt;schluessel&gt;»; die Kopplung oder null. Vergleich in fester Zeit.</summary>
    public XrKopplung? Pruefen(string? authorization)
    {
        var t = XrSchluessel.AusKopfzeile(authorization);
        if (t is null) return null;
        var (id, schluessel) = t.Value;
        try
        {
            Eintrag? e;
            lock (sperre) { Laden(false); e = eintraege.FirstOrDefault(x => x.Kopplung.Id == id); }
            var ist = XrSchluessel.Pruefwert(schluessel);
            var gleich = CryptographicOperations.FixedTimeEquals(ist, e?.Pruefwert ?? Leer);
            if (e is null || !gleich) return null;
            lock (sperre) zuletzt[id] = einstellungen.Jetzt();
            return e.Kopplung;
        }
        finally { CryptographicOperations.ZeroMemory(schluessel); }
    }

    /// <summary>Der Suchschlüssel einer Kopplung (Kopie) oder null.</summary>
    public byte[]? Suchschluessel(string id)
    {
        lock (sperre)
        {
            Laden(false);
            return eintraege.FirstOrDefault(x => x.Kopplung.Id == id)?.Suchschluessel?.ToArray();
        }
    }

    public IReadOnlyList<XrKopplung> Liste() { lock (sperre) { Laden(false); return [.. eintraege.Select(e => e.Kopplung)]; } }

    /// <summary>Zuletzt mit gültigem Schlüssel gesehen (nur dieser Lauf).</summary>
    public DateTimeOffset? Zuletzt(string id) { lock (sperre) return zuletzt.TryGetValue(id, out var z) ? z : null; }

    // ---------------------------------------------------------------- Weg A: Paket

    /// <summary>Koppelt eine Brille und gibt das Paket zurück (der Schlüssel steht nur darin). Eine alte Kopplung derselben Brille wird ersetzt.</summary>
    public XrPaket PaketErzeugen(string? brille, string? name, XrZugangInfo info, string weg = "paket")
    {
        var b = BrillePruefen(brille);
        var n = NamePruefen(name, b);
        var (id, schluessel) = XrSchluessel.Neu();
        try
        {
            var jetzt = einstellungen.Jetzt();
            var kopplung = new XrKopplung(id, b, n, weg, jetzt);
            string? ersetzt = null;
            Aendern(liste =>
            {
                var alt = liste.FirstOrDefault(x => string.Equals(x.Kopplung.Brille, b, StringComparison.OrdinalIgnoreCase));
                if (alt is not null) { liste.Remove(alt); ersetzt = alt.Kopplung.Id; }
                if (liste.Count >= einstellungen.KopplungenMax)
                    throw new XrFehler(409, $"Es lassen sich höchstens {einstellungen.KopplungenMax} Brillen koppeln.", "Zuerst die Kopplung einer anderen Brille widerrufen.");
                liste.Add(new Eintrag(kopplung, XrSchluessel.Pruefwert(schluessel), XrSchluessel.Suchschluessel(schluessel)));
            });
            if (ersetzt is not null) Melden(ersetzt);
            return new XrPaket(XrPaket.ArtName, 1, info.Pc, info.Ips, info.Port, info.SuchPort, info.Fingerabdruck, id, XrSchluessel.Text(schluessel), b, n, jetzt);
        }
        finally { CryptographicOperations.ZeroMemory(schluessel); }
    }

    // ---------------------------------------------------------------- Widerruf

    /// <summary>Widerruft eine Kopplung; false, wenn es sie nicht gibt.</summary>
    public bool Widerrufen(string? id)
    {
        if (!XrSchluessel.IdGueltig(id)) return false;
        bool weg = false;
        Aendern(liste => { weg = liste.RemoveAll(x => x.Kopplung.Id == id) > 0; });
        if (weg) Melden(id!);
        return weg;
    }

    void Melden(string id)
    {
        lock (sperre) zuletzt.Remove(id);
        try { KopplungEntfernt?.Invoke(id); }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or ObjectDisposedException) { /* Trennen ist eine Zugabe; die Kopplung ist weg */ }
    }

    // ---------------------------------------------------------------- Weg B: Code

    /// <summary>Neue Anfrage einer Brille (ohne Schlüssel). Wirft XrFehler 429, wenn zu viele offen oder in der letzten Stunde.</summary>
    public XrAnfrageStand Anfragen(string? brille, string? name, string? ip)
    {
        var b = BrillePruefen(brille);
        var n = NamePruefen(name, b);
        lock (sperre)
        {
            var jetzt = einstellungen.Jetzt();
            Aufraeumen(jetzt);
            neueAnfragen.RemoveAll(z => jetzt - z >= TimeSpan.FromHours(1));
            if (neueAnfragen.Count >= einstellungen.AnfragenJeStunde)
                throw new XrFehler(429, "Zu viele Kopplungsanfragen in der letzten Stunde.", "Später erneut versuchen oder die Brille per Kabel koppeln (KICAD «App installieren»).");
            if (ip is not null) anfragen.RemoveAll(a => a.Ip == ip);   // eine offene Anfrage je Absender: die neue ersetzt die alte
            if (anfragen.Count >= einstellungen.AnfragenOffenMax)
                throw new XrFehler(429, "Es sind schon zu viele Kopplungsanfragen offen.", "Am PC offene Anfragen ablehnen oder ablaufen lassen.");
            string code;
            do code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6"); while (anfragen.Any(a => a.Code == code));
            var neu = new Anfrage { Id = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16)), Brille = b, Name = n, Ip = ip, Code = code, Bis = jetzt + einstellungen.KopplungFrist };
            anfragen.Add(neu);
            neueAnfragen.Add(jetzt);
            return neu.Stand();
        }
    }

    /// <summary>Die offenen Anfragen (mit Code) für die Anzeige am PC.</summary>
    public IReadOnlyList<XrAnfrageStand> OffeneAnfragen()
    {
        lock (sperre) { Aufraeumen(einstellungen.Jetzt()); return [.. anfragen.Select(a => a.Stand())]; }
    }

    /// <summary>Am PC bestätigen (nur localhost). Wirft XrFehler 404, wenn es die Anfrage nicht (mehr) gibt.</summary>
    public XrAnfrageStand Bestaetigen(string? anfrage)
    {
        lock (sperre)
        {
            var a = Offen(anfrage) ?? throw new XrFehler(404, "Diese Kopplungsanfrage gibt es nicht (mehr).", "In der Brille neu anfragen.");
            if (a.Abgelehnt) throw new XrFehler(409, "Die Anfrage wurde schon abgelehnt.", "In der Brille neu anfragen.");
            Laden(false);
            var ersetzt = eintraege.Any(x => string.Equals(x.Kopplung.Brille, a.Brille, StringComparison.OrdinalIgnoreCase));
            if (!ersetzt && eintraege.Count >= einstellungen.KopplungenMax)
                throw new XrFehler(409, $"Es lassen sich höchstens {einstellungen.KopplungenMax} Brillen koppeln.", "Zuerst die Kopplung einer anderen Brille widerrufen.");
            a.Bestaetigt = true;
            return a.Stand();
        }
    }

    /// <summary>Am PC ablehnen (nur localhost). Die Brille erfährt es beim nächsten «abholen».</summary>
    public XrAnfrageStand Ablehnen(string? anfrage)
    {
        lock (sperre)
        {
            var a = Offen(anfrage) ?? throw new XrFehler(404, "Diese Kopplungsanfrage gibt es nicht (mehr).");
            a.Abgelehnt = true;
            a.Bestaetigt = false;
            return a.Stand();
        }
    }

    /// <summary>Die Brille holt den Schlüssel ab (mit dem Code vom PC). Siehe XrAbholErgebnis für die Status.</summary>
    public XrAbholErgebnis Abholen(string? anfrage, string? code, string? ip, Func<XrZugangInfo> info)
    {
        Anfrage a;
        lock (sperre)
        {
            var jetzt = einstellungen.Jetzt();
            var gefunden = anfrage is not null && AnfrageMuster.IsMatch(anfrage) ? anfragen.FirstOrDefault(x => x.Id == anfrage) : null;
            if (gefunden is null || (gefunden.Ip is not null && ip is not null && gefunden.Ip != ip))
                return new(404, "unbekannt", null, "Diese Kopplungsanfrage gibt es nicht (mehr).", null);
            if (jetzt > gefunden.Bis)
            {
                anfragen.Remove(gefunden);
                return new(410, "abgelaufen", null, "Die Kopplungsanfrage ist abgelaufen.", null);
            }
            if (gefunden.Abgelehnt)
            {
                anfragen.Remove(gefunden);
                return new(403, "abgelehnt", null, "Die Kopplung wurde am PC abgelehnt.", null);
            }
            if (code is null || !CodeMuster.IsMatch(code))
                return new(400, "ohne_code", null, "Den 6-stelligen Code vom PC mitsenden.", einstellungen.KopplungVersuche - gefunden.Versuche);
            if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(code), Encoding.ASCII.GetBytes(gefunden.Code)))
            {
                gefunden.Versuche++;
                var uebrig = einstellungen.KopplungVersuche - gefunden.Versuche;
                if (uebrig <= 0)
                {
                    anfragen.Remove(gefunden);
                    return new(410, "gesperrt", null, "Zu viele falsche Codes – die Anfrage ist gesperrt.", 0);
                }
                return new(403, "falsch", null, "Der Code stimmt nicht.", uebrig);
            }
            if (!gefunden.Bestaetigt) return new(202, "wartet", null, "Der Code stimmt. Jetzt am PC die Kopplung bestätigen.", null);
            anfragen.Remove(gefunden);
            a = gefunden;
        }
        try { return new(200, "gekoppelt", PaketErzeugen(a.Brille, a.Name, info(), "code"), null, null); }
        catch (XrFehler f) { return new(f.Status, "fehler", null, f.Message, null); }
    }

    Anfrage? Offen(string? anfrage)
    {
        Aufraeumen(einstellungen.Jetzt());
        return anfrage is null ? null : anfragen.FirstOrDefault(x => x.Id == anfrage);
    }

    void Aufraeumen(DateTimeOffset jetzt) => anfragen.RemoveAll(a => jetzt > a.Bis);

    // ---------------------------------------------------------------- Prüfung der Eingaben

    public static string BrillePruefen(string? brille)
    {
        var b = brille?.Trim();
        if (string.IsNullOrEmpty(b) || !BrilleMuster.IsMatch(b))
            throw new HlksFehler("Die Kennung der Brille fehlt oder ist ungültig.", "Seriennummer bzw. Kennung der Brille angeben: Buchstaben, Ziffern, . _ : - (höchstens 64 Zeichen, nicht mit - beginnend).");
        return b;
    }

    public static string NamePruefen(string? name, string vorgabe)
    {
        var n = string.IsNullOrWhiteSpace(name) ? vorgabe : name.Trim();
        if (n.Length > NameMax || n.Any(char.IsControl))
            throw new HlksFehler($"Der Name der Brille ist ungültig (höchstens {NameMax} Zeichen, keine Steuerzeichen).", "Einen kurzen Namen wählen, z. B. «Quest Christian».");
        return n;
    }

    // ---------------------------------------------------------------- Ablage

    /// <summary>Ändert die Liste unter der Sperre (auch gegen andere KICAD-Prozesse) und schreibt sie atomar. Wirft die Änderung, bleibt alles wie es war.</summary>
    void Aendern(Action<List<Eintrag>> aenderung)
    {
        lock (sperre)
        {
            var ordner = einstellungen.OrdnerPfad();
            XrSchutz.OrdnerAnlegen(ordner);
            using var dateisperre = Sperren(Path.Combine(ordner, "kopplungen.sperre"));
            Laden(true);
            var kopie = eintraege.ToList();
            aenderung(kopie);
            var ablage = new Ablage
            {
                Kopplungen = [.. kopie.Select(e => new AblageEintrag
                {
                    Id = e.Kopplung.Id, Brille = e.Kopplung.Brille, Name = e.Kopplung.Name, Weg = e.Kopplung.Weg, Angelegt = e.Kopplung.Angelegt,
                    Pruefwert = Convert.ToBase64String(e.Pruefwert), Suche = e.Suchschluessel is null ? null : XrSchutz.TextSchuetzen(e.Suchschluessel),
                })],
            };
            var ziel = DateiPfad;
            var temp = ziel + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
            File.WriteAllText(temp, Json.Text(ablage, true));
            XrSchutz.NurBenutzer(temp);
            File.Move(temp, ziel, true);
            eintraege = kopie;
            geladenStand = File.GetLastWriteTimeUtc(ziel);
            geladen = true;
        }
    }

    static FileStream Sperren(string pfad)
    {
        for (int versuch = 0; ; versuch++)
        {
            try { return new FileStream(pfad, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (versuch < 40) { Thread.Sleep(50); }
        }
    }

    /// <summary>Liest die Datei, wenn sie sich geändert hat (höchstens einmal je Sekunde nachsehen, ausser erzwingen). Nur unter sperre.</summary>
    void Laden(bool erzwingen)
    {
        if (geladen && !erzwingen && Environment.TickCount64 - letzterBlick < 1000) return;
        letzterBlick = Environment.TickCount64;
        var pfad = DateiPfad;
        DateTime stand;
        try { stand = File.Exists(pfad) ? File.GetLastWriteTimeUtc(pfad) : DateTime.MinValue; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return; }
        if (geladen && !erzwingen && stand == geladenStand) return;
        var liste = new List<Eintrag>();
        if (stand != DateTime.MinValue)
        {
            try
            {
                var ablage = JsonSerializer.Deserialize<Ablage>(File.ReadAllText(pfad), Json.Optionen);
                foreach (var x in ablage?.Kopplungen ?? [])
                    if (EintragAus(x) is { } e && liste.All(y => y.Kopplung.Id != e.Kopplung.Id)) liste.Add(e);
            }
            catch (JsonException) { /* unlesbar: keine Kopplung gilt (lieber zu streng) */ }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (erzwingen) throw;
                return;   // Zugriff klemmt kurz: den bisherigen Stand behalten
            }
        }
        eintraege = liste;
        geladenStand = stand;
        geladen = true;
    }

    static Eintrag? EintragAus(AblageEintrag x)
    {
        if (!XrSchluessel.IdGueltig(x.Id) || !BrilleMuster.IsMatch(x.Brille ?? "")) return null;
        byte[] pruefwert;
        try { pruefwert = Convert.FromBase64String(x.Pruefwert ?? ""); }
        catch (FormatException) { return null; }
        if (pruefwert.Length != 32) return null;
        var suche = XrSchutz.TextOeffnen(x.Suche);
        if (suche is { Length: not 32 }) suche = null;
        var name = string.IsNullOrWhiteSpace(x.Name) || x.Name.Length > NameMax ? x.Brille : x.Name;
        return new Eintrag(new XrKopplung(x.Id, x.Brille!, name!, x.Weg is "code" ? "code" : "paket", x.Angelegt), pruefwert, suche);
    }
}
