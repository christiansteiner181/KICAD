// Projektablage in einem Ordner (auf dem NAS: <Projekt>\c_Pläne und Modelle\e_KICAD\). Mehrere Bearbeiter auf
// mehreren Geräten greifen gleichzeitig darauf zu, ohne Server und ohne Datenbank. Darum gilt:
//
//   projekt.json              Kopf – atomar ersetzt (Temp-Datei + Umbenennen)
//   stand\stand-<zeit>-<z>.json Standabbilder – abgeleitete Daten, die drei neuesten bleiben liegen
//   aenderungen\<paketId>.json  Änderungspakete – nur neu angelegt, nie überschrieben (Umbenennen ohne Ersetzen)
//   sperren\<elementId>.json    Reservierungen – atomar neu angelegt (CreateNew), eigene/abgelaufene ersetzt
//   unterlagen\                 Unterlagen (Inhalt ab Modul 1b)
//   austausch\hkap.json         Basisstand des HKAP-Abgleichs (Modul 1d) – atomar ersetzt, Ordner bei Bedarf angelegt
//
// Dateien beginnen nie mit einem Punkt; Namen mit Punkt am Anfang sind Temp-Dateien eines laufenden (oder
// abgebrochenen) Schreibvorgangs und werden beim Lesen übergangen. Alle Dateien UTF-8 ohne BOM.
// Zugriffsfehler (Ordner weg, kein Netz, keine Rechte) gehen als IOException bzw. UnauthorizedAccessException an den
// Aufrufer; Ordner und Unterordner werden dabei nie stillschweigend neu angelegt – nur Anlegen() (aus Projekt.Anlegen)
// legt sie an. Ausnahme: «stand» (abgeleitete Daten) wird beim Schreiben eines Standabbilds bei Bedarf neu angelegt.

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hlks.Kern;

public sealed class OrdnerSpeicher : IProjektSpeicher
{
    const string KopfDateiName = "projekt.json";
    const string StandPraefix = "stand-";
    const string ZeitFormat = "yyyyMMdd'T'HHmmssfff'Z'";
    const int ZeitLaenge = 19;                 // Länge von ZeitFormat im Dateinamen
    const int StaendeBehalten = 3;
    const int Wiederholungen = 5;              // nach dem ersten Versuch, bei vorübergehenden Fehlern
    const int PauseMs = 50;                    // wächst linear: 50, 100, 150, 200, 250 ms
    const int SperrNameMax = 150;
    const string Zeichen = "abcdefghijklmnopqrstuvwxyz0123456789";
    const string VerboteneZeichen = "\\/:*?\"<>|";

    static readonly byte[] Bom = [0xEF, 0xBB, 0xBF];
    static readonly HashSet<string> ReservierteNamen = new(
        ["con", "prn", "aux", "nul", .. Enumerable.Range(0, 10).SelectMany(i => new[] { $"com{i}", $"lpt{i}" })],
        StringComparer.OrdinalIgnoreCase);

    readonly string kopfPfad, standOrdner, paketOrdner, sperrOrdner, unterlagenOrdner;
    readonly TimeSpan dauer;

    /// <param name="ordner">Ordner e_KICAD des Projekts. Der Konstruktor legt nichts an (siehe Anlegen).</param>
    /// <param name="reservierungsDauer">Gültigkeit einer Reservierung ab der letzten Änderung; Vorgabe 2 Stunden.</param>
    public OrdnerSpeicher(string ordner, TimeSpan? reservierungsDauer = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ordner);
        dauer = reservierungsDauer ?? TimeSpan.FromHours(2);
        if (dauer <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(reservierungsDauer), "Reservierungsdauer muss positiv sein.");
        Ort = ordner;
        kopfPfad = Path.Combine(ordner, KopfDateiName);
        standOrdner = Path.Combine(ordner, "stand");
        paketOrdner = Path.Combine(ordner, "aenderungen");
        sperrOrdner = Path.Combine(ordner, "sperren");
        unterlagenOrdner = Path.Combine(ordner, "unterlagen");
    }

    public string Ort { get; }

    public string? UnterlagenOrdner => unterlagenOrdner;

    public void Anlegen()
    {
        foreach (var o in new[] { Ort, standOrdner, paketOrdner, sperrOrdner, unterlagenOrdner }) Directory.CreateDirectory(o);
    }

    // ---------------------------------------------------------------- Kopf

    public ProjektKopf? KopfLesen()
    {
        OrtPruefen();
        try { return DateiLesen<ProjektKopf>(kopfPfad, inhaltWiederholen: true); }
        catch (FileNotFoundException) { return null; }
    }

    public void KopfSchreiben(ProjektKopf kopf)
    {
        ArgumentNullException.ThrowIfNull(kopf);
        ErsetzenAtomar(kopfPfad, JsonSerializer.SerializeToUtf8Bytes(kopf, Json.Eingerueckt));
    }

    // ---------------------------------------------------------------- Standabbilder

    public StandAbbild? StandLesen()
    {
        OrtPruefen();
        if (!Directory.Exists(standOrdner)) return null;          // abgeleitete Daten: fehlt der Ordner, gibt es keinen Stand
        foreach (var datei in StandDateien())
        {
            try
            {
                var s = DateiLesen<StandAbbild>(datei, inhaltWiederholen: false);
                if (s.Elemente is null || s.Verbindungen is null || s.EnthaltenePakete is null
                    || s.Elemente.Any(e => e is null) || s.Verbindungen.Any(v => v is null)) continue;
                return s;
            }
            catch (FileNotFoundException) { }                       // inzwischen von einem anderen Gerät aufgeräumt
            catch (InhaltUnlesbar) { }                              // kaputt → nächstältere Datei
        }
        return null;
    }

    public void StandSchreiben(StandAbbild stand)
    {
        ArgumentNullException.ThrowIfNull(stand);
        OrtPruefen();
        Directory.CreateDirectory(standOrdner);
        var vorhandene = StandDateien();
        // Name muss nach allen vorhandenen sortieren, sonst gälte ein Stand derselben Millisekunde als älter.
        var zeit = DateTime.UtcNow;
        if (vorhandene.Count > 0 && StandZeit(vorhandene[0]) is { } letzte && zeit <= letzte) zeit = letzte.AddMilliseconds(1);
        var inhalt = JsonSerializer.SerializeToUtf8Bytes(stand, Json.Optionen);
        for (int versuch = 0; ; versuch++)
        {
            var name = $"{StandPraefix}{zeit.ToString(ZeitFormat, CultureInfo.InvariantCulture)}-{Zufall(4)}.json";
            if (NeuAtomar(Path.Combine(standOrdner, name), inhalt)) break;
            if (versuch >= Wiederholungen) throw new IOException($"Standabbild konnte in {standOrdner} nicht angelegt werden (Name besetzt).");
        }
        foreach (var alt in StandDateien().Skip(StaendeBehalten)) Versuchen(() => File.Delete(alt));
    }

    /// <summary>Standdateien, neueste zuerst (Name enthält die Zeit UTC, ordinal absteigend).</summary>
    List<string> StandDateien() =>
        JsonDateien(standOrdner).Where(d => Path.GetFileName(d).StartsWith(StandPraefix, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(d => Path.GetFileName(d), StringComparer.Ordinal).ToList();

    static DateTime? StandZeit(string datei)
    {
        var name = Path.GetFileName(datei);
        if (name.Length < StandPraefix.Length + ZeitLaenge) return null;
        return DateTime.TryParseExact(name.AsSpan(StandPraefix.Length, ZeitLaenge), ZeitFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t) ? t : null;
    }

    // ---------------------------------------------------------------- Pakete

    public IReadOnlyList<string> PaketListe() =>
        JsonDateien(paketOrdner).Select(d => Path.GetFileNameWithoutExtension(d)).Order(StringComparer.Ordinal).ToList();

    public Paket PaketLesen(string id) => DateiLesen<Paket>(PaketPfad(id), inhaltWiederholen: true);

    public void PaketSchreiben(Paket paket)
    {
        ArgumentNullException.ThrowIfNull(paket);
        var ziel = PaketPfad(paket.Id);
        if (!NeuAtomar(ziel, JsonSerializer.SerializeToUtf8Bytes(paket, Json.Eingerueckt)))
            throw new IOException($"Paket {paket.Id} gibt es schon in {paketOrdner}; Pakete werden nie überschrieben.");
    }

    string PaketPfad(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id[0] == '.' || id.Length > 200 || id[^1] is ' ' or '.'
            || id.Any(c => c < ' ' || VerboteneZeichen.Contains(c)))
            throw new ArgumentException($"Paket-Id «{id}» ist als Dateiname nicht zulässig.", nameof(id));
        return Path.Combine(paketOrdner, id + ".json");
    }

    // ---------------------------------------------------------------- Reservierungen

    public IReadOnlyList<Reservierung> Reservieren(IEnumerable<string> elementIds, string akteur, string geraet)
    {
        ArgumentNullException.ThrowIfNull(elementIds);
        ArgumentNullException.ThrowIfNull(akteur);
        ArgumentNullException.ThrowIfNull(geraet);
        // Feste Reihenfolge: Zwei Geräte mit überlappenden Mengen stossen beim ersten gemeinsamen Element aufeinander,
        // statt sich gegenseitig je eine Hälfte wegzunehmen und beide zu scheitern.
        var ids = elementIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        if (ids.Count == 0) return [];
        var pfade = ids.Select(SperrPfad).ToList();
        var jetzt = DateTimeOffset.Now;

        // 1. Alle prüfen. Fremde, noch gültige Reservierungen → nichts reservieren.
        var bestehend = pfade.Select(SperreLesen).ToList();
        var konflikte = bestehend.Where(r => r is not null && Fremd(r, akteur, geraet) && r.Bis > jetzt).Select(r => r!).ToList();
        if (konflikte.Count > 0) return konflikte;

        // 2. Anlegen bzw. übernehmen. «uebernommen» = Dateien, die vor diesem Aufruf nicht (gültig) uns gehörten.
        var bis = jetzt + dauer;
        var uebernommen = new List<string>();
        for (int i = 0; i < ids.Count; i++)
        {
            var alt = bestehend[i];
            if (alt is null)
            {
                var neu = new Reservierung(ids[i], akteur, geraet, jetzt, bis);
                if (NeuAnlegen(pfade[i], neu)) { uebernommen.Add(pfade[i]); continue; }
                // Inzwischen hat jemand die Datei angelegt.
                alt = SperreLesen(pfade[i]);
                if (alt is not null && Fremd(alt, akteur, geraet) && alt.Bis > DateTimeOffset.Now)
                {
                    Zuruecknehmen(uebernommen, akteur, geraet);
                    var liste = new List<Reservierung> { alt };
                    for (int j = i + 1; j < ids.Count; j++)
                        if (SperreLesen(pfade[j]) is { } r && Fremd(r, akteur, geraet) && r.Bis > DateTimeOffset.Now) liste.Add(r);
                    return liste;
                }
            }
            bool eigenGueltig = alt is not null && !Fremd(alt, akteur, geraet) && alt.Bis > jetzt;
            var ersatz = new Reservierung(ids[i], akteur, geraet, eigenGueltig ? alt!.Seit : jetzt, bis);
            ErsetzenAtomar(pfade[i], JsonSerializer.SerializeToUtf8Bytes(ersatz, Json.Eingerueckt));
            if (!eigenGueltig) uebernommen.Add(pfade[i]);
        }
        return [];
    }

    public void Freigeben(string akteur, string geraet, IEnumerable<string>? elementIds = null)
    {
        ArgumentNullException.ThrowIfNull(akteur);
        ArgumentNullException.ThrowIfNull(geraet);
        var pfade = elementIds is null ? JsonDateien(sperrOrdner) : elementIds.Distinct(StringComparer.Ordinal).Select(SperrPfad).ToList();
        foreach (var pfad in pfade)
            if (SperreLesen(pfad) is { } r && !Fremd(r, akteur, geraet))
                Wiederholt(() => File.Delete(pfad));
    }

    public IReadOnlyList<Reservierung> Reservierungen()
    {
        var jetzt = DateTimeOffset.Now;
        return JsonDateien(sperrOrdner).Select(SperreLesen).Where(r => r is not null && r.Bis > jetzt).Select(r => r!)
            .OrderBy(r => r.Element, StringComparer.Ordinal).ToList();
    }

    static bool Fremd(Reservierung r, string akteur, string geraet) =>
        !string.Equals(r.Akteur, akteur, StringComparison.OrdinalIgnoreCase) || !string.Equals(r.Geraet, geraet, StringComparison.OrdinalIgnoreCase);

    /// <summary>Reservierung aus der Datei; null, wenn es keine gibt oder sie unlesbar ist (verwaist, darf ersetzt werden).</summary>
    static Reservierung? SperreLesen(string pfad)
    {
        try
        {
            var r = DateiLesen<Reservierung>(pfad, inhaltWiederholen: true);
            return string.IsNullOrEmpty(r.Element) || r.Akteur is null || r.Geraet is null ? null : r;
        }
        catch (FileNotFoundException) { return null; }
        catch (InhaltUnlesbar) { return null; }
    }

    /// <summary>Legt die Sperrdatei atomar neu an. false = gibt es schon.</summary>
    static bool NeuAnlegen(string pfad, Reservierung r)
    {
        var inhalt = JsonSerializer.SerializeToUtf8Bytes(r, Json.Eingerueckt);
        FileStream fs;
        try { fs = new FileStream(pfad, FileMode.CreateNew, FileAccess.Write, FileShare.None); }
        catch (IOException) when (File.Exists(pfad)) { return false; }
        try
        {
            using (fs) { fs.Write(inhalt); fs.Flush(flushToDisk: true); }
        }
        catch
        {
            Versuchen(() => File.Delete(pfad));
            throw;
        }
        return true;
    }

    /// <summary>Löscht die in einem gescheiterten Aufruf übernommenen Sperren wieder, sofern sie noch uns gehören.</summary>
    static void Zuruecknehmen(List<string> pfade, string akteur, string geraet)
    {
        foreach (var pfad in pfade)
            Versuchen(() => { if (SperreLesen(pfad) is { } r && !Fremd(r, akteur, geraet)) File.Delete(pfad); });
    }

    /// <summary>Dateiname der Sperre. Erlaubt bleiben a–z, 0–9, «-», «_»; alles andere (auch Grossbuchstaben, weil
    /// Windows-Dateinamen Gross/klein nicht unterscheiden) wird als ~xxxx kodiert – eindeutig umkehrbar.</summary>
    string SperrPfad(string elementId)
    {
        if (string.IsNullOrEmpty(elementId)) throw new ArgumentException("Element-Id fehlt.", nameof(elementId));
        var sb = new StringBuilder(elementId.Length);
        foreach (var c in elementId)
            if (char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c is '-' or '_') sb.Append(c);
            else sb.Append('~').Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
        var name = sb.ToString();
        if (name.Length > SperrNameMax)
            name = "~h" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(elementId)));
        else if (ReservierteNamen.Contains(name))
            name = "~" + ((int)name[0]).ToString("x4", CultureInfo.InvariantCulture) + name[1..];
        return Path.Combine(sperrOrdner, name + ".json");
    }

    // ---------------------------------------------------------------- Austausch (Modul 1d)

    /// <summary>Datei im Ordner austausch. Nur einfache Namen (kein Pfad, kein Punkt am Anfang, Endung .json).</summary>
    string AustauschPfad(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name[0] == '.' || name.Length > 100 || !name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            || name.Any(c => c < ' ' || VerboteneZeichen.Contains(c)))
            throw new ArgumentException($"Austauschdatei «{name}» ist als Dateiname nicht zulässig.", nameof(name));
        return Path.Combine(Ort, "austausch", name);
    }

    public byte[]? AustauschLesen(string name)
    {
        var pfad = AustauschPfad(name);
        OrtPruefen();
        byte[]? daten = null;
        try { Wiederholt(() => daten = Bytes(pfad)); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }     // Ordner austausch gibt es noch nicht
        return daten;
    }

    public void AustauschSchreiben(string name, byte[] inhalt)
    {
        ArgumentNullException.ThrowIfNull(inhalt);
        var pfad = AustauschPfad(name);
        OrtPruefen();
        Directory.CreateDirectory(Path.GetDirectoryName(pfad)!);
        ErsetzenAtomar(pfad, inhalt);
    }

    // ---------------------------------------------------------------- Dateizugriff

    void OrtPruefen()
    {
        if (!Directory.Exists(Ort)) throw new DirectoryNotFoundException($"Projektablage {Ort} nicht erreichbar.");
    }

    /// <summary>JSON-Dateien eines Ordners ohne Temp-Dateien (Punkt am Anfang).</summary>
    static List<string> JsonDateien(string ordner) =>
        Directory.EnumerateFiles(ordner).Where(d =>
        {
            var n = Path.GetFileName(d);
            return n.Length > 5 && n[0] != '.' && n.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
        }).ToList();

    /// <summary>Liest eine JSON-Datei. Vorübergehende Fehler (Datei gerade gesperrt, SMB-Aussetzer) werden bis zu
    /// fünfmal wiederholt, unvollständiger Inhalt ebenso, wenn inhaltWiederholen. Fehlende Datei/Ordner sofort.</summary>
    static T DateiLesen<T>(string pfad, bool inhaltWiederholen) where T : class
    {
        for (int versuch = 0; ; versuch++)
        {
            try
            {
                var daten = Bytes(pfad);
                ReadOnlySpan<byte> json = daten.AsSpan().StartsWith(Bom) ? daten.AsSpan(Bom.Length) : daten;
                var t = JsonSerializer.Deserialize<T>(json, Json.Optionen);
                if (t is not null) return t;
                if (!inhaltWiederholen || versuch >= Wiederholungen) throw new InhaltUnlesbar($"{pfad} ist unlesbar (Inhalt null).", null);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or InhaltUnlesbar) { throw; }
            catch (IOException) when (versuch < Wiederholungen) { }
            catch (JsonException ex)
            {
                if (!inhaltWiederholen || versuch >= Wiederholungen) throw new InhaltUnlesbar($"{pfad} ist unlesbar (kein gültiges JSON): {ex.Message}", ex);
            }
            Thread.Sleep(PauseMs * (versuch + 1));
        }
    }

    static byte[] Bytes(string pfad)
    {
        // Andere dürfen währenddessen lesen, ersetzen und löschen (FileShare.Delete), damit Lesende niemanden blockieren.
        using var fs = new FileStream(pfad, new FileStreamOptions
        {
            Mode = FileMode.Open, Access = FileAccess.Read, Share = FileShare.ReadWrite | FileShare.Delete, Options = FileOptions.SequentialScan,
        });
        using var ms = new MemoryStream();
        fs.CopyTo(ms);
        return ms.ToArray();
    }

    static void DateiSchreiben(string pfad, byte[] inhalt)
    {
        using var fs = new FileStream(pfad, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        fs.Write(inhalt);
        fs.Flush(flushToDisk: true);
    }

    static string TempPfad(string ziel) =>
        Path.Combine(Path.GetDirectoryName(ziel)!, "." + Path.GetFileNameWithoutExtension(ziel) + "." + Zufall(8) + ".tmp");

    /// <summary>Legt ziel mit Inhalt an, ohne je zu überschreiben: Temp-Datei, dann Umbenennen ohne Ersetzen.
    /// false = ziel gibt es schon.</summary>
    static bool NeuAtomar(string ziel, byte[] inhalt)
    {
        if (File.Exists(ziel)) return false;
        var tmp = TempPfad(ziel);
        try
        {
            DateiSchreiben(tmp, inhalt);
            try { File.Move(tmp, ziel, overwrite: false); return true; }
            catch (IOException) when (File.Exists(ziel)) { return false; }
        }
        finally { if (File.Exists(tmp)) Versuchen(() => File.Delete(tmp)); }
    }

    /// <summary>Ersetzt ziel atomar (Temp-Datei, dann Umbenennen mit Ersetzen). Lesende sehen alt oder neu, nie halb.</summary>
    static void ErsetzenAtomar(string ziel, byte[] inhalt)
    {
        var tmp = TempPfad(ziel);
        try
        {
            DateiSchreiben(tmp, inhalt);
            Wiederholt(() => File.Move(tmp, ziel, overwrite: true));
        }
        finally { if (File.Exists(tmp)) Versuchen(() => File.Delete(tmp)); }
    }

    /// <summary>Führt eine Dateioperation aus und wiederholt sie bei vorübergehenden Fehlern (gesperrt, Löschung hängig).</summary>
    static void Wiederholt(Action aktion)
    {
        for (int versuch = 0; ; versuch++)
        {
            try { aktion(); return; }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { throw; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && versuch < Wiederholungen) { }
            Thread.Sleep(PauseMs * (versuch + 1));
        }
    }

    /// <summary>Aufräumen nach bestem Bemühen: Fehler werden verschluckt (ein anderes Gerät räumt evtl. gleichzeitig auf).</summary>
    static void Versuchen(Action aktion)
    {
        try { aktion(); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    static string Zufall(int laenge) => RandomNumberGenerator.GetString(Zeichen, laenge);

    /// <summary>Datei vorhanden, Inhalt aber kein gültiges JSON (abgebrochen geschrieben oder von Hand verändert).</summary>
    sealed class InhaltUnlesbar(string meldung, Exception? inner) : IOException(meldung, inner);
}
