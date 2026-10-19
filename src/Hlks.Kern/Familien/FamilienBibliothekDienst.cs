// Familien (Modul 2, Nachtrag B7, entwurf/modul-2-bibliothek.md): Dienst der projektübergreifenden Familienbibliothek –
// Ort, Ablage, Index, Versionen, Abzweige, Log, Unterlagen, Sperren, Warteschlange. Reine Logik (Planen, Prüfsumme):
// FamilienBibliothekDaten.cs; Befehle: FamilienBibliothekBefehle.cs; Abfrage: FamilienBibliothek.cs.
//
// Ort (Entscheid Christian 19.10.2026): Umgebungsvariable HLKS_FAMILIENBIBLIOTHEK › kataloge/buero.json
//   (familienbibliothek.pfad, W:\03_CAD Bibliotheken\d_KICAD Familien) › Rückfall kataloge/familien.
//   SCHUTZ: Läuft der Kern unter einem Testrunner (testhost, xUnit geladen) oder ist HLKS_TEST gesetzt, gilt die Bibliothek ohne
//   HLKS_FAMILIENBIBLIOTHEK als gesperrt (weder lesen noch schreiben, Hinweis statt Schreiben); mit der Variable nur unter %TEMP%.
//   So schreibt kein Test auf W: und keiner in kataloge/familien.
//
// Ablage:
//   <Bibliothek>\index.json                              Kopfdaten aller Familien (Zwischenspeicher, aus den Ordnern neu aufbaubar)
//   <Bibliothek>\<Kategorie>\<Name>__<id8>\familie.json  aktuelle Version (Format schema/hlks-familie.schema.json, version = Bibliotheksversion)
//                                     versionen\v0001.familie.json …  alle Versionen, nie überschrieben
//                                     abzweige\v0004-<projekt>-<zeit>.familie.json  parallele Änderungen
//                                     log.json  je Version/Abzweig/Entscheid: Projekt, Akteur, Art, Zeit, Befehl, Status, Prüfsumme, Basis
//                                     .sperre   {rechner, prozess, zeit}, verfällt nach 2 min
//   <Bibliothek>\_unterlagen\<pruefsumme>.<ext>          Herstellerunterlagen, einmal je Prüfsumme
//   Schreiben atomar (temporäre Datei + Umbenennen); Versionen, Abzweige und Unterlagen ohne Überschreiben.
//
// Warteschlange (NAS nicht erreichbar, Familienordner zu lange gesperrt): %LOCALAPPDATA%\HLKS_CAD\bibliothek_ausstehend\<hash des
//   Bibliothekspfads>\ (im Testlauf unter %TEMP%, überschreibbar mit HLKS_BIBLIOTHEK_AUSSTEHEND) – auftraege\*.json in Reihenfolge,
//   _unterlagen\ (Kopien), index.json (letzter gelesener Index für das Lesen ohne NAS). Nachgeholt beim nächsten Zugriff
//   (Lesen oder Schreiben), immer vor einem neuen Schreibvorgang; ein Auftrag je Rechner, Doppel verhindert die Planung.
// ANNAHMEN (Prüfliste): Ordnername bleibt beim Umbenennen der Familie (der Index trägt den aktuellen Namen); der Bibliotheksordner
//   wird angelegt, wenn sein übergeordneter Ordner erreichbar ist (sonst «nicht erreichbar»); dauert eine erfolglose Prüfung der
//   Erreichbarkeit über 2 s (Netzlaufwerk getrennt), wird 60 s lang nicht erneut geprüft.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <summary>Ergebnis des Lesens: Index (live oder letzter Stand ohne NAS), Warteschlange, Hinweise.</summary>
public sealed class BibLesen
{
    public required BibliotheksOrt Ort { get; init; }
    public BibIndex? Index { get; set; }
    /// <summary>Gelesen aus dem lokalen Zwischenspeicher, weil die Bibliothek nicht erreichbar ist.</summary>
    public bool Offline { get; set; }
    public bool Erreichbar { get; set; }
    public int Warteschlange { get; set; }
    public int Nachgeholt { get; set; }
    public string? Hinweis { get; set; }
    public List<string> Fehler { get; } = [];
}

public static class FamilienBibliothekDienst
{
    public const string Umgebungsvariable = "HLKS_FAMILIENBIBLIOTHEK";
    public const string WarteschlangeVariable = "HLKS_BIBLIOTHEK_AUSSTEHEND";
    public const string TestVariable = "HLKS_TEST";

    /// <summary>Eine Sperre verfällt nach 2 Minuten (Spezifikation, Annahme 3).</summary>
    public static readonly TimeSpan SperreVerfall = TimeSpan.FromMinutes(2);
    /// <summary>So lange wartet ein Schreiber auf eine fremde Sperre, dann geht der Auftrag in die Warteschlange.</summary>
    public static TimeSpan SperreWarten { get; set; } = TimeSpan.FromSeconds(10);

    static readonly JsonSerializerOptions Datei = new(Json.Eingerueckt);

    // =====================================================================================================  Ort

    /// <summary>Ort der Bürobibliothek für einen Katalog (Umgebungsvariable › buero.json › kataloge/familien; Schutz im Testlauf).</summary>
    public static BibliotheksOrt Ort(Katalog k) => Ermitteln(k.Ordner, Environment.GetEnvironmentVariable(Umgebungsvariable), Testlauf());

    /// <summary>Ermittelt den Ort (öffentlich für Tests): umgebung = Wert von HLKS_FAMILIENBIBLIOTHEK, testlauf = Schutzregel aktiv.</summary>
    public static BibliotheksOrt Ermitteln(string katalogOrdner, string? umgebung, bool testlauf)
    {
        if (!string.IsNullOrWhiteSpace(umgebung))
        {
            string pfad;
            try { pfad = Path.TrimEndingDirectorySeparator(Path.GetFullPath(umgebung.Trim())); }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            { return new BibliotheksOrt(null, "Umgebungsvariable " + Umgebungsvariable, $"{Umgebungsvariable} = «{umgebung}» ist kein gültiger Pfad."); }
            if (testlauf && (AufNetz(pfad) || !UnterTemp(pfad)))
                return new BibliotheksOrt(pfad, "Umgebungsvariable " + Umgebungsvariable,
                    $"Testlauf: {Umgebungsvariable} zeigt nicht unter %TEMP% ({pfad}) – die Bibliothek wird weder gelesen noch beschrieben.");
            return new BibliotheksOrt(pfad, "Umgebungsvariable " + Umgebungsvariable, null);
        }
        var buero = BueroPfad(katalogOrdner);
        var rueckfall = Path.Combine(katalogOrdner, "familien");
        if (testlauf)
            return new BibliotheksOrt(buero ?? rueckfall, buero is null ? "Rückfall kataloge/familien" : "kataloge/buero.json",
                $"Testlauf ohne {Umgebungsvariable}: die Familienbibliothek ({buero ?? rueckfall}) wird weder gelesen noch beschrieben (Schutz vor Schreiben auf W:).");
        return buero is not null ? new BibliotheksOrt(buero, "kataloge/buero.json", null) : new BibliotheksOrt(rueckfall, "Rückfall kataloge/familien", null);
    }

    static readonly ConcurrentDictionary<string, (long Zeit, string? Pfad)> bueroCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>familienbibliothek.pfad aus kataloge/buero.json (null, wenn die Datei oder der Eintrag fehlt).</summary>
    public static string? BueroPfad(string katalogOrdner)
    {
        var datei = Path.Combine(katalogOrdner, "buero.json");
        try
        {
            if (!File.Exists(datei)) return null;
            var zeit = File.GetLastWriteTimeUtc(datei).Ticks;
            if (bueroCache.TryGetValue(datei, out var c) && c.Zeit == zeit) return c.Pfad;
            var pfad = JsonNode.Parse(File.ReadAllText(datei, Encoding.UTF8))?["familienbibliothek"]?["pfad"] is JsonValue v && v.TryGetValue<string>(out var s)
                && !string.IsNullOrWhiteSpace(s) ? s.Trim() : null;
            bueroCache[datei] = (zeit, pfad);
            return pfad;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }

    static readonly Lazy<bool> testrunner = new(() =>
    {
        try
        {
            var name = Process.GetCurrentProcess().ProcessName;
            if (name.StartsWith("testhost", StringComparison.OrdinalIgnoreCase) || name.Contains("vstest", StringComparison.OrdinalIgnoreCase)
                || name.Contains("TestRunner", StringComparison.OrdinalIgnoreCase)) return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or PlatformNotSupportedException) { }
        return AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name is { } n
            && (n.StartsWith("xunit", StringComparison.OrdinalIgnoreCase) || n.StartsWith("Microsoft.TestPlatform", StringComparison.OrdinalIgnoreCase)
                || n.StartsWith("Microsoft.VisualStudio.TestPlatform", StringComparison.OrdinalIgnoreCase) || n.StartsWith("nunit", StringComparison.OrdinalIgnoreCase)));
    });

    /// <summary>Läuft der Kern in einem Test (HLKS_TEST gesetzt oder Testrunner)?</summary>
    public static bool Testlauf() => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(TestVariable)) || testrunner.Value;

    /// <summary>Beim Laden des Kerns in einem Testrunner HLKS_TEST=1 für den Prozess setzen: Kindprozesse (MCP-Server der KI-Attrappe,
    /// Web-Server) erben die Variable und schreiben so ebenfalls nie in die Bürobibliothek auf dem NAS.</summary>
#pragma warning disable CA2255   // bewusst: Schutz muss vor dem ersten Kindprozess wirken
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void TestlaufWeitergeben()
    {
        try { if (testrunner.Value && string.IsNullOrEmpty(Environment.GetEnvironmentVariable(TestVariable))) Environment.SetEnvironmentVariable(TestVariable, "1"); }
        catch (Exception ex) when (ex is System.Security.SecurityException or InvalidOperationException) { }
    }
#pragma warning restore CA2255

    /// <summary>Grund, warum ein Projekt NICHT automatisch in die Bürobibliothek schreibt (null = es schreibt): Bibliothek gesperrt, oder
    /// die Bibliothek stammt aus kataloge/buero.json bzw. dem Rückfall und das Projekt hat keine Ablage bzw. liegt unter %TEMP% (Test,
    /// Versuch, Browser-Test) – Schutz vor Testfamilien in der Bürobibliothek. Mit HLKS_FAMILIENBIBLIOTHEK gilt das nicht; von Hand
    /// (familie_in_bibliothek) geht es immer.</summary>
    public static string? AutomatischGesperrt(BibliotheksOrt ort, Projekt p)
    {
        if (!ort.Aktiv) return ort.Sperre ?? "Familienbibliothek gesperrt.";
        if (ort.Quelle.StartsWith("Umgebungsvariable", StringComparison.Ordinal)) return null;
        var ablage = p.Speicher?.Ort;
        if (ablage is null) return "Projekt ohne Ablage – in die Bürobibliothek schreiben nur Projekte mit Ablage (von Hand: familie_in_bibliothek).";
        try { if (UnterTemp(ablage)) return $"Projekt unter %TEMP% ({ablage}: Test oder Versuch) – die Bürobibliothek bekommt nur Familien aus Projektablagen (von Hand: familie_in_bibliothek; für Tests {Umgebungsvariable} setzen)."; }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { }
        return null;
    }

    /// <summary>Liegt der Pfad unter %TEMP%?</summary>
    public static bool UnterTemp(string pfad)
    {
        var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        var p = Path.GetFullPath(pfad);
        return p.StartsWith(temp + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || string.Equals(p, temp, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>NAS bzw. Netz: Laufwerk W: oder UNC-Pfad.</summary>
    public static bool AufNetz(string pfad) =>
        pfad.StartsWith(@"\\", StringComparison.Ordinal) || pfad.StartsWith("//", StringComparison.Ordinal) || pfad.StartsWith("W:", StringComparison.OrdinalIgnoreCase);

    // =====================================================================================================  Erreichbarkeit

    static readonly ConcurrentDictionary<string, DateTime> unerreichbarBis = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Ist die Bibliothek erreichbar? Fehlt nur der Bibliotheksordner selbst, ist sie erreichbar (anlegen = beim Schreiben anlegen).</summary>
    public static bool Erreichbar(string root, bool anlegen)
    {
        if (unerreichbarBis.TryGetValue(root, out var bis) && DateTime.UtcNow < bis) return false;
        var sw = Stopwatch.StartNew();
        try
        {
            if (Directory.Exists(root)) return true;
            var eltern = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(root));
            if (eltern is not null && Directory.Exists(eltern))
            {
                if (anlegen) Directory.CreateDirectory(root);
                return true;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        if (sw.Elapsed > TimeSpan.FromSeconds(2)) unerreichbarBis[root] = DateTime.UtcNow.AddSeconds(60);
        return false;
    }

    // =====================================================================================================  Dateien

    /// <summary>Atomar schreiben: temporäre Datei im selben Ordner, dann umbenennen (ueberschreiben = false: vorhandene bleibt).</summary>
    static bool Schreiben(string pfad, byte[] daten, bool ueberschreiben)
    {
        var ordner = Path.GetDirectoryName(pfad)!;
        Directory.CreateDirectory(ordner);
        var temp = Path.Combine(ordner, $".{Path.GetFileName(pfad)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllBytes(temp, daten);
            if (ueberschreiben) { File.Move(temp, pfad, overwrite: true); return true; }
            try { File.Move(temp, pfad, overwrite: false); return true; }
            catch (IOException) when (File.Exists(pfad)) { return false; }
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    static byte[] Bytes<T>(T wert) => new UTF8Encoding(false).GetBytes(JsonSerializer.Serialize(wert, Datei));

    static T? Lesen<T>(string pfad) where T : class
    {
        if (!File.Exists(pfad)) return null;
        return JsonSerializer.Deserialize<T>(File.ReadAllBytes(pfad), Json.Optionen);
    }

    /// <summary>log.json eines Familienordners (null, wenn es keines gibt).</summary>
    public static BibLog? LogLesen(string ordner) => Lesen<BibLog>(Path.Combine(ordner, "log.json"));

    static string Zeit(DateTimeOffset z) => z.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    static string Relativ(string root, string pfad) => Path.GetRelativePath(root, pfad).Replace('\\', '/');

    static string Absolut(string root, string relativ) => Path.Combine(root, relativ.Replace('/', Path.DirectorySeparatorChar));

    // =====================================================================================================  Sperren

    /// <summary>Sperre eines Ordners gegen zwei Rechner gleichzeitig: Datei .sperre {rechner, prozess, zeit}, verfällt nach 2 min.</summary>
    sealed class DateiSperre : IDisposable
    {
        readonly string pfad;
        DateiSperre(string pfad) { this.pfad = pfad; }

        public static DateiSperre Holen(string ordner, TimeSpan warten)
        {
            Directory.CreateDirectory(ordner);
            var pfad = Path.Combine(ordner, ".sperre");
            var sw = Stopwatch.StartNew();
            var inhalt = new UTF8Encoding(false).GetBytes(new JsonObject
            {
                ["rechner"] = Environment.MachineName, ["prozess"] = Environment.ProcessId, ["zeit"] = Zeit(DateTimeOffset.UtcNow),
            }.ToJsonString());
            while (true)
            {
                try
                {
                    using (var fs = new FileStream(pfad, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) fs.Write(inhalt);
                    return new DateiSperre(pfad);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    if (!Directory.Exists(ordner)) throw new IOException($"Ordner {ordner} nicht erreichbar.", ex);
                    var (alter, wer) = Alter(pfad);
                    if (alter > SperreVerfall)
                    {
                        try { File.Delete(pfad); } catch (Exception e2) when (e2 is IOException or UnauthorizedAccessException) { }
                        continue;
                    }
                    if (sw.Elapsed > warten) throw new IOException($"Familienordner {ordner} ist gesperrt ({wer}, seit {alter.TotalSeconds:0} s).", ex);
                    Thread.Sleep(100);
                }
            }
        }

        static (TimeSpan Alter, string Wer) Alter(string pfad)
        {
            try
            {
                var text = File.ReadAllText(pfad);
                var o = JsonNode.Parse(text) as JsonObject;
                var wer = $"{o?["rechner"]}, Prozess {o?["prozess"]}";
                if (o?["zeit"] is JsonValue v && v.TryGetValue<string>(out var s) && DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var z))
                    return (DateTimeOffset.UtcNow - z, wer);
                return (DateTime.UtcNow - File.GetLastWriteTimeUtc(pfad), wer);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                try { return (DateTime.UtcNow - File.GetLastWriteTimeUtc(pfad), "unbekannt"); }
                catch (Exception e2) when (e2 is IOException or UnauthorizedAccessException) { return (TimeSpan.Zero, "unbekannt"); }
            }
        }

        public void Dispose()
        {
            try { File.Delete(pfad); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    static readonly ConcurrentDictionary<string, object> prozessSperren = new(StringComparer.OrdinalIgnoreCase);
    static object ProzessSperre(string schluessel) => prozessSperren.GetOrAdd(Path.GetFullPath(schluessel), _ => new object());

    // =====================================================================================================  Index

    sealed record Zwischen(long Zeit, long Laenge, BibIndex Index);
    static readonly ConcurrentDictionary<string, Zwischen> indexCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Index lesen (zwischengespeichert nach Änderungszeit und Länge). Fehlt er oder ist er unlesbar bzw. als veraltet
    /// markiert, wird er aus den Familienordnern neu aufgebaut (und, wenn schreiben, abgelegt).</summary>
    static BibIndex IndexLesen(string root, bool schreiben)
    {
        if (!Directory.Exists(root)) return new BibIndex();
        var pfad = Path.Combine(root, "index.json");
        if (File.Exists(Path.Combine(root, "index.veraltet")) || !File.Exists(pfad)) return Neuaufbau(root, schreiben);
        var fi = new FileInfo(pfad);
        if (indexCache.TryGetValue(root, out var c) && c.Zeit == fi.LastWriteTimeUtc.Ticks && c.Laenge == fi.Length) return c.Index;
        BibIndex? idx;
        try { idx = Lesen<BibIndex>(pfad); }
        catch (JsonException) { idx = null; }
        if (idx is null) return Neuaufbau(root, schreiben);
        indexCache[root] = new Zwischen(fi.LastWriteTimeUtc.Ticks, fi.Length, idx);
        return idx;
    }

    /// <summary>Alle Familienordner (relativ) mit log.json.</summary>
    static IEnumerable<string> Familienordner(string root)
    {
        if (!Directory.Exists(root)) yield break;
        foreach (var kat in Directory.EnumerateDirectories(root).Order(StringComparer.OrdinalIgnoreCase))
        {
            var n = Path.GetFileName(kat);
            if (n.StartsWith('_') || n.StartsWith('.')) continue;
            foreach (var f in Directory.EnumerateDirectories(kat).Order(StringComparer.OrdinalIgnoreCase))
                if (File.Exists(Path.Combine(f, "log.json"))) yield return Relativ(root, f);
        }
    }

    static BibIndex Neuaufbau(string root, bool schreiben)
    {
        var idx = new BibIndex { Stand = Zeit(DateTimeOffset.UtcNow) };
        foreach (var rel in Familienordner(root))
        {
            try
            {
                var log = LogLesen(Absolut(root, rel));
                if (log is null || string.IsNullOrEmpty(log.Id)) continue;
                var e = FamilienBibliothekLogik.Eintrag(log, rel);
                if (e.Versionen.Count > 0 && idx.Familie(e.Id) is null) idx.Familien.Add(e);
            }
            catch (JsonException) { }
        }
        idx.Familien.Sort((a, b) => string.Compare(a.Ordner, b.Ordner, StringComparison.OrdinalIgnoreCase));
        if (schreiben)
        {
            try
            {
                lock (ProzessSperre(root))
                using (DateiSperre.Holen(root, SperreWarten))
                {
                    Schreiben(Path.Combine(root, "index.json"), Bytes(idx), true);
                    File.Delete(Path.Combine(root, "index.veraltet"));
                }
                var fi = new FileInfo(Path.Combine(root, "index.json"));
                indexCache[root] = new Zwischen(fi.LastWriteTimeUtc.Ticks, fi.Length, idx);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return idx;
    }

    /// <summary>Den Eintrag einer Familie im Index ersetzen (unter der Sperre des Bibliotheksordners). Gelingt das nicht, wird der
    /// Index als veraltet markiert und beim nächsten Lesen neu aufgebaut.</summary>
    static void IndexAktualisieren(string root, BibIndexEintrag e)
    {
        try
        {
            using (DateiSperre.Holen(root, SperreWarten))
            {
                var pfad = Path.Combine(root, "index.json");
                var veraltet = Path.Combine(root, "index.veraltet");
                BibIndex? idx = null;
                try { if (File.Exists(pfad) && !File.Exists(veraltet)) idx = Lesen<BibIndex>(pfad); }
                catch (JsonException) { idx = null; }
                idx ??= Neuaufbau(root, false);   // vollständig aus den Familienordnern
                idx.Familien.RemoveAll(x => x.Id == e.Id);
                idx.Familien.Add(e);
                idx.Familien.Sort((a, b) => string.Compare(a.Ordner, b.Ordner, StringComparison.OrdinalIgnoreCase));
                idx.Stand = Zeit(DateTimeOffset.UtcNow);
                Schreiben(pfad, Bytes(idx), true);
                if (File.Exists(veraltet)) File.Delete(veraltet);
                var fi = new FileInfo(pfad);
                indexCache[root] = new Zwischen(fi.LastWriteTimeUtc.Ticks, fi.Length, idx);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            try { File.WriteAllText(Path.Combine(root, "index.veraltet"), Zeit(DateTimeOffset.UtcNow)); }
            catch (Exception e2) when (e2 is IOException or UnauthorizedAccessException) { }
        }
    }

    // =====================================================================================================  Lesen

    /// <summary>Index der Bibliothek lesen: zuerst ausstehende Schreibvorgänge nachholen (nachholen), dann den Index – ohne NAS den
    /// letzten lokal gemerkten Stand (Offline).</summary>
    public static BibLesen Lesen(BibliotheksOrt ort, bool nachholen = true)
    {
        var r = new BibLesen { Ort = ort };
        if (!ort.Aktiv) { r.Hinweis = ort.Sperre; return r; }
        var root = ort.Pfad!;
        if (nachholen) r.Nachgeholt = Nachholen(ort, r.Fehler);
        if (Erreichbar(root, false))
        {
            try
            {
                r.Index = IndexLesen(root, nachholen);   // im Trockenlauf (nachholen = false) auch keinen Index ablegen
                r.Erreichbar = true;
                LokalMerken(root, r.Index);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { r.Fehler.Add(ex.Message); }
        }
        if (r.Index is null)
        {
            r.Offline = true;
            r.Index = LokalLesen(root);
            r.Hinweis = $"Familienbibliothek nicht erreichbar ({root}): " + (r.Index is null ? "kein gemerkter Stand – Liste leer." : $"letzter Stand vom {r.Index.Stand}.") +
                " Änderungen gehen in die Warteschlange und werden beim nächsten Zugriff nachgeschrieben.";
            r.Index ??= new BibIndex();
        }
        r.Warteschlange = Ausstehend(ort).Count;
        if (r.Warteschlange > 0 && !r.Offline) r.Hinweis = $"{r.Warteschlange} Schreibvorgang/-vorgänge warten noch (Familienordner gesperrt?).";
        return r;
    }

    /// <summary>Index ohne Nachholen und ohne Schreiben (Vorhersage im Befehl, auch im Trockenlauf).</summary>
    public static BibIndex? IndexOhneSchreiben(BibliotheksOrt ort)
    {
        if (!ort.Aktiv) return null;
        try { if (Erreichbar(ort.Pfad!, false)) return IndexLesen(ort.Pfad!, false); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return LokalLesen(ort.Pfad!);
    }

    /// <summary>Eine Fassung (Version bzw. Abzweig) einer Familie lesen. Wirft HlksFehler mit Vorschlag.</summary>
    public static JsonObject FassungLesen(BibliotheksOrt ort, BibIndexEintrag e, int? version, string? abzweig)
    {
        var root = ort.Pfad!;
        var datei = abzweig is not null ? Path.Combine(Absolut(root, e.Ordner), "abzweige", abzweig + ".familie.json")
            : Path.Combine(Absolut(root, e.Ordner), "versionen", $"v{version ?? e.Version:D4}.familie.json");
        try
        {
            if (!File.Exists(datei))
                throw new HlksFehler($"Die Fassung {(abzweig ?? $"Version {version ?? e.Version}")} von «{e.Name}» fehlt in der Bibliothek ({datei}).",
                    $"Versionen: {string.Join(", ", e.Versionen.Select(v => v.Version))}{(e.Abzweige.Count > 0 ? "; Abzweige: " + string.Join(", ", e.Abzweige.Select(a => a.Abzweig)) : "")}.");
            return JsonNode.Parse(File.ReadAllBytes(datei)) as JsonObject
                ?? throw new HlksFehler($"«{datei}» enthält kein JSON-Objekt.", "Die Datei in der Bibliothek prüfen (schema/hlks-familie.schema.json).");
        }
        catch (JsonException ex) { throw new HlksFehler($"«{datei}» ist kein gültiges JSON: {ex.Message}", "Die Datei in der Bibliothek prüfen (schema/hlks-familie.schema.json)."); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { throw new HlksFehler($"Familienbibliothek nicht erreichbar ({root}): {ex.Message}", "Netzwerk/VPN prüfen und erneut versuchen."); }
    }

    /// <summary>log.json einer Familie der Bibliothek (für die Abfrage), null wenn nicht lesbar.</summary>
    public static BibLog? Log(BibliotheksOrt ort, BibIndexEintrag e)
    {
        if (!ort.Aktiv) return null;
        try { return LogLesen(Absolut(ort.Pfad!, e.Ordner)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }

    /// <summary>Pfad einer Unterlage in der Bibliothek (null, wenn sie fehlt).</summary>
    public static string? UnterlageInBibliothek(BibliotheksOrt ort, BibUnterlage u)
    {
        if (!ort.Aktiv) return null;
        var p = Path.Combine(ort.Pfad!, "_unterlagen", UnterlageName(u));
        return File.Exists(p) ? p : null;
    }

    static string UnterlageName(BibUnterlage u)
    {
        var ext = Path.GetExtension(u.Datei ?? "").ToLowerInvariant();
        if (ext.Length < 2 || ext.Length > 8 || !ext[1..].All(char.IsAsciiLetterOrDigit))
            ext = u.Art switch { "pdf" => ".pdf", "dxf" => ".dxf", "dwg" => ".dwg", "bild" => ".png", "step" => ".step", "iges" => ".iges", "stl" => ".stl",
                "obj" => ".obj", "xlsx" => ".xlsx", "csv" => ".csv", "typkatalog" => ".txt", "ifc" => ".ifc", "html" => ".html", _ => ".bin" };
        return FamilienBibliothekLogik.Sauber(u.Pruefsumme, 64) + ext;
    }

    static string Sha256(string pfad)
    {
        using var s = File.OpenRead(pfad);
        return Convert.ToHexString(SHA256.HashData(s)).ToLowerInvariant();
    }

    static bool PasstZu(string pfad, string pruefsumme)
    {
        var s = Sha256(pfad);
        return pruefsumme.Length >= 64 ? s == pruefsumme.ToLowerInvariant() : s.StartsWith(pruefsumme.ToLowerInvariant(), StringComparison.Ordinal);
    }

    /// <summary>Unterlagen der Fassung nach _unterlagen kopieren (einmal je Prüfsumme). Rückgabe: Unterlagen, deren Datei fehlt.</summary>
    static List<BibUnterlage> UnterlagenAblegen(string root, IEnumerable<BibUnterlage>? liste)
    {
        var fehlt = new List<BibUnterlage>();
        foreach (var u in liste ?? [])
        {
            if (string.IsNullOrWhiteSpace(u.Pruefsumme)) continue;
            var ziel = Path.Combine(root, "_unterlagen", UnterlageName(u));
            if (File.Exists(ziel)) continue;
            if (u.Quelle is null || !File.Exists(u.Quelle) || !PasstZu(u.Quelle, u.Pruefsumme)) { fehlt.Add(u); continue; }
            Schreiben(ziel, File.ReadAllBytes(u.Quelle), false);
        }
        return fehlt;
    }

    // =====================================================================================================  Familienordner

    static string? OrdnerFinden(string root, string id, BibIndex? idx)
    {
        if (idx?.Familie(id) is { } e)
        {
            var o = Absolut(root, e.Ordner);
            try { if (LogLesen(o)?.Id == id) return e.Ordner; } catch (JsonException) { }
        }
        var id8 = FamilienBibliothekLogik.Sauber(id.Length > 8 ? id[..8] : id, 8);
        if (!Directory.Exists(root)) return null;
        foreach (var kat in Directory.EnumerateDirectories(root))
        {
            var n = Path.GetFileName(kat);
            if (n.StartsWith('_') || n.StartsWith('.')) continue;
            foreach (var f in Directory.EnumerateDirectories(kat, "*__" + id8 + "*"))
            {
                try { if (LogLesen(f)?.Id == id) return Relativ(root, f); } catch (JsonException) { }
            }
        }
        return null;
    }

    static string OrdnerNeu(string root, BibAuftrag a)
    {
        string? T(string f) => a.Definition?[f] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
        var kat = FamilienBibliothekLogik.Sauber(T("kategorie") ?? "Familie", 40);
        var name = FamilienBibliothekLogik.Sauber(T("name") ?? a.Id, 40);
        var id8 = FamilienBibliothekLogik.Sauber(a.Id.Length > 8 ? a.Id[..8] : a.Id, 8);
        var basis = $"{kat}/{name}__{id8}";
        var rel = basis;
        for (int i = 2; Directory.Exists(Absolut(root, rel)) && File.Exists(Path.Combine(Absolut(root, rel), "log.json")); i++) rel = $"{basis}_{i}";
        return rel;
    }

    /// <summary>Versionen und Abzweige, die als Datei da sind, aber im Log fehlen (Abbruch zwischen zwei Schritten), nachtragen.</summary>
    static bool Reparieren(BibLog log, string ordner)
    {
        bool geaendert = false;
        foreach (var (unter, art) in new[] { ("versionen", "version"), ("abzweige", "abzweig") })
        {
            var o = Path.Combine(ordner, unter);
            if (!Directory.Exists(o)) continue;
            foreach (var f in Directory.EnumerateFiles(o, "*.familie.json").Order(StringComparer.Ordinal))
            {
                var name = Path.GetFileName(f)[..^".familie.json".Length];
                int? nr = art == "version" && name.Length == 5 && name[0] == 'v' && int.TryParse(name[1..], out var n) ? n : null;
                if (art == "version" && nr is null) continue;
                if (art == "version" ? log.Eintraege.Any(x => x.Art == "version" && x.Version == nr) : log.Eintraege.Any(x => x.Art == "abzweig" && x.Abzweig == name)) continue;
                try
                {
                    if (JsonNode.Parse(File.ReadAllBytes(f)) is not JsonObject d) continue;
                    log.Eintraege.Add(Eintrag(art, d, nr, art == "abzweig" ? name : null, $"{unter}/{Path.GetFileName(f)}", null, null,
                        new BibAuftrag { Befehl = "wiederhergestellt", Zeit = Zeit(File.GetLastWriteTimeUtc(f)) }));
                    geaendert = true;
                }
                catch (JsonException) { }
            }
        }
        return geaendert;
    }

    static BibLogEintrag Eintrag(string art, JsonObject def, int? version, string? abzweig, string datei, int? basis, string? basisAbzweig, BibAuftrag a)
    {
        string? T(string f) => def[f] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
        return new BibLogEintrag
        {
            Art = art, Version = version, Abzweig = abzweig, Datei = datei, Pruefsumme = FamilienBibliothekLogik.Inhaltspruefsumme(def),
            Basis = basis, BasisAbzweig = basisAbzweig, Status = T("status") ?? "entwurf", Name = T("name"), Kategorie = T("kategorie"), Hersteller = T("hersteller"),
            Produkt = T("produkt"), Typen = (def["typen"] as JsonArray)?.Count ?? 0,
            Unterlagen = (def["unterlagen"] as JsonArray ?? []).OfType<JsonObject>().Select(u => new BibUnterlage
            {
                Pruefsumme = u["pruefsumme"] is JsonValue pv && pv.TryGetValue<string>(out var ps) ? ps : "",
                Datei = u["datei"] is JsonValue dv && dv.TryGetValue<string>(out var ds) ? ds : null,
                Art = u["art"] is JsonValue av && av.TryGetValue<string>(out var aa) ? aa : null,
            }).Where(u => u.Pruefsumme.Length > 0).ToList() is { Count: > 0 } ul ? ul : null,
            Projekt = a.Projekt, Akteur = a.Akteur, AkteurArt = a.AkteurArt, Geraet = a.Geraet, Zeit = a.Zeit, Befehl = a.Befehl,
            Freigabe = a.Freigabe?.DeepClone() as JsonObject,
        };
    }

    // =====================================================================================================  Schreiben

    /// <summary>Was ein Auftrag bewirken würde (nur lesen, auch im Trockenlauf): ohne erreichbare Bibliothek «ausstehend».</summary>
    public static BibPlan Vorhersage(BibliotheksOrt ort, BibAuftrag a, out string ordner)
    {
        ordner = "";
        if (!ort.Aktiv) return new BibPlan("gesperrt", null, null, null, null, null, []);
        BibIndex? idx = null;
        bool erreichbar = Erreichbar(ort.Pfad!, false);
        if (erreichbar)
            try { idx = IndexLesen(ort.Pfad!, false); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { erreichbar = false; }
        if (!erreichbar)
        {
            // ohne NAS: Basis aus dem letzten gemerkten Index, geschrieben wird später (Version erst dann bekannt)
            var lokal = LokalLesen(ort.Pfad!)?.Familie(a.Id);
            ordner = lokal?.Ordner ?? "";
            var (b, ba) = lokal is null ? ((int?)null, (string?)null) : FamilienBibliothekLogik.Basis(lokal, a.Vorher, a.MarkerVersion, a.MarkerAbzweig, a.MarkerInhalt);
            return BibPlan.Ausstehend with { Basis = b, BasisAbzweig = ba, Aktuell = lokal?.Version };
        }
        var e = idx!.Familie(a.Id);
        ordner = e?.Ordner ?? OrdnerNeu(ort.Pfad!, a);
        return FamilienBibliothekLogik.Planen(e, a.Pruefsumme!, a.Vorher, a.MarkerVersion, a.MarkerAbzweig, a.MarkerInhalt, a.AlsAktuell,
            FamilienBibliothekLogik.ProjektKurz(a.Projekt) + "-" + AbzweigZeit(a));
    }

    static string AbzweigZeit(BibAuftrag a) =>
        (DateTimeOffset.TryParse(a.Zeit, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var z) ? z : DateTimeOffset.UtcNow)
        .ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    /// <summary>Schreibt einen Auftrag; ist die Bibliothek nicht erreichbar oder der Familienordner zu lange gesperrt, kommt er in die
    /// Warteschlange. Ausstehende Aufträge werden vorher nachgeholt (Reihenfolge). Wirft nie wegen des NAS.</summary>
    public static BibErgebnis SchreibenOderEinreihen(BibliotheksOrt ort, BibAuftrag a)
    {
        if (!ort.Aktiv) return new BibErgebnis(new BibPlan("gesperrt", null, null, null, null, null, []), "", a.Pruefsumme ?? "", false, ort.Sperre ?? "Familienbibliothek gesperrt.");
        Nachholen(ort, null);
        if (Ausstehend(ort).Count == 0)
        {
            try { return Schreiben(ort, a); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return Einreihen(ort, a, ex.Message); }
        }
        return Einreihen(ort, a, "ältere Schreibvorgänge warten noch");
    }

    /// <summary>Schreibt einen Auftrag in die Bibliothek (wirft IOException, wenn sie nicht erreichbar oder gesperrt ist).</summary>
    public static BibErgebnis Schreiben(BibliotheksOrt ort, BibAuftrag a)
    {
        var root = ort.Pfad!;
        if (!Erreichbar(root, true)) throw new IOException($"Familienbibliothek {root} nicht erreichbar.");
        lock (ProzessSperre(root))
        {
            BibIndex? idx = null;
            try { idx = IndexLesen(root, false); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            var fehlend = a.Art == "schreiben" ? UnterlagenAblegen(root, a.Unterlagen) : [];
            var rel = OrdnerFinden(root, a.Id, idx);
            if (rel is null)
            {
                if (a.Art != "schreiben") throw new HlksFehler($"Die Familie «{a.Id}» gibt es in der Bibliothek nicht.", "Zuerst familie_in_bibliothek.");
                rel = OrdnerNeu(root, a);
            }
            var ordner = Absolut(root, rel);
            BibIndexEintrag eintrag;
            BibPlan plan;
            using (DateiSperre.Holen(ordner, SperreWarten))
            {
                var log = LogLesen(ordner) ?? new BibLog { Id = a.Id };
                if (string.IsNullOrEmpty(log.Id)) log.Id = a.Id;
                bool logNeu = Reparieren(log, ordner);
                var vorher = FamilienBibliothekLogik.Eintrag(log, rel);
                if (a.Art == "entscheid")
                {
                    var offen = vorher.OffeneAbzweige.Select(x => x.Abzweig).ToHashSet(StringComparer.Ordinal);
                    var liste = (a.Abzweige ?? []).Where(offen.Contains).ToList();
                    plan = new BibPlan("gleich", vorher.Version, null, null, null, vorher.Version, liste);
                    if (liste.Count > 0) { log.Eintraege.Add(Entscheid(liste, a.Ergebnis ?? "verworfen", null, a)); logNeu = true; }
                }
                else
                {
                    var def = a.Definition ?? throw new HlksFehler("Auftrag ohne Definition.", "familie_in_bibliothek erneut ausführen.");
                    plan = FamilienBibliothekLogik.Planen(vorher.Versionen.Count == 0 ? null : vorher, a.Pruefsumme!, a.Vorher, a.MarkerVersion, a.MarkerAbzweig,
                        a.MarkerInhalt, a.AlsAktuell, FamilienBibliothekLogik.ProjektKurz(a.Projekt) + "-" + AbzweigZeit(a));
                    switch (plan.Art)
                    {
                        case "neu":
                        {
                            var n = plan.Version!.Value;
                            var datei = Path.Combine(ordner, "versionen", $"v{n:D4}.familie.json");
                            var inhalt = FamilienFreigabe.Bytes(FamilienBibliothekLogik.FuerBibliothek(def, n));
                            if (!Schreiben(datei, inhalt, false)) throw new IOException($"Version {n} gibt es schon ({datei}).");
                            log.Eintraege.Add(Eintrag("version", FamilienBibliothekLogik.FuerBibliothek(def, n), n, null, $"versionen/v{n:D4}.familie.json", plan.Basis, plan.BasisAbzweig, a));
                            if (plan.Erledigt.Count > 0) log.Eintraege.Add(Entscheid(plan.Erledigt.ToList(), "ersetzt", n, a));
                            logNeu = true;
                            break;
                        }
                        case "abzweig":
                        {
                            var name = plan.Abzweig!;
                            for (int i = 2; File.Exists(Path.Combine(ordner, "abzweige", name + ".familie.json")); i++) name = $"{plan.Abzweig}-{i}";
                            plan = plan with { Abzweig = name };
                            var stamm = int.Parse(name[1..5], CultureInfo.InvariantCulture);
                            var d = FamilienBibliothekLogik.FuerBibliothek(def, stamm);
                            Schreiben(Path.Combine(ordner, "abzweige", name + ".familie.json"), FamilienFreigabe.Bytes(d), false);
                            log.Eintraege.Add(Eintrag("abzweig", d, null, name, $"abzweige/{name}.familie.json", plan.Basis, plan.BasisAbzweig, a));
                            logNeu = true;
                            break;
                        }
                        default:   // gleich
                            if (plan.Erledigt.Count > 0) { log.Eintraege.Add(Entscheid(plan.Erledigt.ToList(), "verworfen", null, a)); logNeu = true; }
                            break;
                    }
                }
                if (logNeu) Schreiben(Path.Combine(ordner, "log.json"), Bytes(log), true);
                eintrag = FamilienBibliothekLogik.Eintrag(log, rel);
                // familie.json = aktuelle Version
                var aktuell = Path.Combine(ordner, "familie.json");
                var quelle = Path.Combine(ordner, "versionen", $"v{eintrag.Version:D4}.familie.json");
                if (eintrag.Version > 0 && File.Exists(quelle))
                {
                    var b = File.ReadAllBytes(quelle);
                    if (!File.Exists(aktuell) || !File.ReadAllBytes(aktuell).AsSpan().SequenceEqual(b)) Schreiben(aktuell, b, true);
                }
            }
            IndexAktualisieren(root, eintrag);
            var parallel = eintrag.OffeneAbzweige.Any();
            return new BibErgebnis(plan, rel, a.Pruefsumme ?? eintrag.Pruefsumme, parallel, Text(plan, rel, eintrag, a, fehlend));
        }
    }

    static BibLogEintrag Entscheid(List<string> abzweige, string ergebnis, int? version, BibAuftrag a) => new()
    {
        Art = "entscheid", Abzweige = abzweige, Ergebnis = ergebnis, Version = version, Projekt = a.Projekt, Akteur = a.Akteur, AkteurArt = a.AkteurArt,
        Geraet = a.Geraet, Zeit = a.Zeit, Befehl = a.Befehl,
    };

    static string Text(BibPlan plan, string rel, BibIndexEintrag e, BibAuftrag a, List<BibUnterlage> fehlend)
    {
        var name = a.Definition?["name"]?.ToString() ?? e.Name;
        var t = plan.Art switch
        {
            "neu" => $"Familienbibliothek: «{name}» als Version {plan.Version} geschrieben ({rel})." +
                     (plan.Erledigt.Count > 0 ? $" Parallele Fassung(en) {string.Join(", ", plan.Erledigt)} damit entschieden." : ""),
            "abzweig" => $"Familienbibliothek: «{name}» wurde parallel geändert – Version {e.Version} ({e.Projekt ?? "anderes Projekt"}) ist neuer als die Grundlage " +
                         $"dieser Änderung; die Fassung ist als Abzweig {plan.Abzweig} erhalten (nicht aktuell). Entscheiden (nur ein Mensch): familie_aktualisieren " +
                         "(Bibliothek übernehmen) oder familie_in_bibliothek mit als_aktuell (diese Fassung gilt).",
            _ when a.Art == "entscheid" => plan.Erledigt.Count > 0 ? $"Familienbibliothek: parallele Fassung(en) {string.Join(", ", plan.Erledigt)} von «{name}» als {a.Ergebnis ?? "verworfen"} vermerkt." : "",
            _ => $"Familienbibliothek: «{name}» ist dort schon {(plan.Abzweig is not null ? "als Abzweig " + plan.Abzweig : "als Version " + plan.Version)} – keine neue Version." +
                 (plan.Erledigt.Count > 0 ? $" Parallele Fassung(en) {string.Join(", ", plan.Erledigt)} als verworfen vermerkt (diese Fassung gilt)." : ""),
        };
        if (plan.Art != "abzweig" && e.OffeneAbzweige.Any() && plan.Erledigt.Count == 0)
            t += $" Achtung: parallel geändert – offene Abzweige {string.Join(", ", e.OffeneAbzweige.Select(x => x.Abzweig))}.";
        if (fehlend.Count > 0)
            t += $" {fehlend.Count} Unterlage(n) nicht abgelegt (Datei im Projekt nicht gefunden): {string.Join(", ", fehlend.Select(u => u.Datei ?? u.Pruefsumme))}.";
        return t;
    }

    // =====================================================================================================  Warteschlange

    /// <summary>Wurzel der Warteschlangen (HLKS_BIBLIOTHEK_AUSSTEHEND › im Testlauf %TEMP% › %LOCALAPPDATA%).</summary>
    public static string WarteschlangeWurzel()
    {
        var env = Environment.GetEnvironmentVariable(WarteschlangeVariable);
        if (!string.IsNullOrWhiteSpace(env)) return Path.GetFullPath(env);
        if (Testlauf()) return Path.Combine(Path.GetTempPath(), "HLKS_CAD", "bibliothek_ausstehend");
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HLKS_CAD", "bibliothek_ausstehend");
    }

    /// <summary>Warteschlange einer Bibliothek (je Bibliothekspfad ein Ordner, damit nichts in eine andere Bibliothek nachgeschrieben wird).</summary>
    public static string WarteschlangeOrdner(string bibliothek)
    {
        var schluessel = Path.TrimEndingDirectorySeparator(Path.GetFullPath(bibliothek)).ToLowerInvariant();
        return Path.Combine(WarteschlangeWurzel(), Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(schluessel)))[..16].ToLowerInvariant());
    }

    static BibErgebnis Einreihen(BibliotheksOrt ort, BibAuftrag a, string grund)
    {
        var root = ort.Pfad!;
        var q = WarteschlangeOrdner(root);
        var auftraege = Path.Combine(q, "auftraege");
        Directory.CreateDirectory(auftraege);
        File.WriteAllText(Path.Combine(q, "ziel.txt"), root);
        var kopie = JsonSerializer.Deserialize<BibAuftrag>(JsonSerializer.Serialize(a, Json.Optionen), Json.Optionen)!;
        foreach (var u in kopie.Unterlagen ?? [])
        {
            if (u.Quelle is null || !File.Exists(u.Quelle)) continue;
            var ziel = Path.Combine(q, "_unterlagen", UnterlageName(u));
            try
            {
                if (!File.Exists(ziel)) Schreiben(ziel, File.ReadAllBytes(u.Quelle), false);
                u.Quelle = ziel;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        var name = $"{DateTime.UtcNow:yyyyMMdd'T'HHmmssfffffff'Z'}_{Guid.NewGuid().ToString("N")[..8]}.json";
        Schreiben(Path.Combine(auftraege, name), Bytes(kopie), false);
        ausstehendCache.TryRemove(root, out _);
        var plan = BibPlan.Ausstehend;
        var famName = a.Definition?["name"]?.ToString() ?? a.Id;
        return new BibErgebnis(plan, "", a.Pruefsumme ?? "", false,
            a.Art == "entscheid" ? $"Familienbibliothek: nicht geschrieben ({grund}) – der Entscheid zu «{famName}» wartet in der Warteschlange und wird beim nächsten Zugriff nachgeschrieben."
                                 : $"Familienbibliothek: nicht geschrieben ({grund}) – «{famName}» wartet in der Warteschlange ({q}) und wird beim nächsten Zugriff nachgeschrieben; im Projekt geht alles weiter.");
    }

    sealed record AusstehendStand(string Schluessel, List<BibAuftrag> Liste);
    static readonly ConcurrentDictionary<string, AusstehendStand> ausstehendCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Ausstehende Aufträge dieser Bibliothek (älteste zuerst).</summary>
    public static IReadOnlyList<BibAuftrag> Ausstehend(BibliotheksOrt ort)
    {
        if (!ort.Aktiv) return [];
        var auftraege = Path.Combine(WarteschlangeOrdner(ort.Pfad!), "auftraege");
        if (!Directory.Exists(auftraege)) return [];
        var dateien = Directory.GetFiles(auftraege).Where(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".inarbeit", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal).ToList();
        var schluessel = string.Join("|", dateien.Select(Path.GetFileName));
        if (ausstehendCache.TryGetValue(ort.Pfad!, out var c) && c.Schluessel == schluessel) return c.Liste;
        var liste = new List<BibAuftrag>();
        foreach (var f in dateien)
        {
            try { if (Lesen<BibAuftrag>(f) is { } a) liste.Add(a); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
        }
        ausstehendCache[ort.Pfad!] = new AusstehendStand(schluessel, liste);
        return liste;
    }

    /// <summary>Holt ausstehende Aufträge nach, wenn die Bibliothek erreichbar ist. Rückgabe: Anzahl geschrieben.</summary>
    public static int Nachholen(BibliotheksOrt ort, List<string>? fehler)
    {
        if (!ort.Aktiv) return 0;
        var root = ort.Pfad!;
        var q = WarteschlangeOrdner(root);
        var auftraege = Path.Combine(q, "auftraege");
        if (!Directory.Exists(auftraege)) return 0;
        // liegengebliebene Arbeitsdateien (Absturz) zurücknehmen
        foreach (var f in Directory.GetFiles(auftraege, "*.inarbeit"))
            try { if (DateTime.UtcNow - File.GetLastWriteTimeUtc(f) > SperreVerfall) File.Move(f, f[..^".inarbeit".Length], overwrite: false); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        var dateien = Directory.GetFiles(auftraege, "*.json").Order(StringComparer.Ordinal).ToList();
        if (dateien.Count == 0) return 0;
        if (!Erreichbar(root, true)) return 0;
        int n = 0;
        lock (ProzessSperre(q))
        {
            foreach (var f in dateien)
            {
                var arbeit = f[..^".json".Length] + ".inarbeit";
                try { File.Move(f, arbeit); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }
                BibAuftrag? a;
                try { a = Lesen<BibAuftrag>(arbeit); }
                catch (JsonException ex) { Beiseite(q, arbeit); fehler?.Add($"Warteschlange: {Path.GetFileName(f)} unlesbar ({ex.Message})."); continue; }
                if (a is null) { Beiseite(q, arbeit); continue; }
                try
                {
                    Schreiben(ort, a);
                    File.Delete(arbeit);
                    n++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    try { File.Move(arbeit, f); } catch (Exception e2) when (e2 is IOException or UnauthorizedAccessException) { }
                    fehler?.Add($"Warteschlange: {ex.Message}");
                    break;
                }
                catch (HlksFehler ex) { Beiseite(q, arbeit); fehler?.Add($"Warteschlange: {Path.GetFileName(f)}: {ex.Message}"); }
            }
            ausstehendCache.TryRemove(root, out _);
            if (Directory.GetFiles(auftraege).Length == 0)
                try { if (Directory.Exists(Path.Combine(q, "_unterlagen"))) Directory.Delete(Path.Combine(q, "_unterlagen"), true); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return n;
    }

    static void Beiseite(string q, string datei)
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(q, "fehler"));
            File.Move(datei, Path.Combine(q, "fehler", Path.GetFileName(datei)), overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    // =====================================================================================================  Lokaler Stand des Index

    static readonly ConcurrentDictionary<string, string?> lokalGemerkt = new(StringComparer.OrdinalIgnoreCase);

    static void LokalMerken(string root, BibIndex idx)
    {
        if (lokalGemerkt.TryGetValue(root, out var s) && s == idx.Stand && idx.Stand is not null) return;
        try
        {
            var q = WarteschlangeOrdner(root);
            Schreiben(Path.Combine(q, "index.json"), Bytes(idx), true);
            File.WriteAllText(Path.Combine(q, "ziel.txt"), root);
            lokalGemerkt[root] = idx.Stand;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    static BibIndex? LokalLesen(string root)
    {
        try { return Lesen<BibIndex>(Path.Combine(WarteschlangeOrdner(root), "index.json")); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }
}
