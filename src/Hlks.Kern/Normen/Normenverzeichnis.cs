// Normenverzeichnis (Modul 3c, Teil K): Volltextsuche in den Normen und Merkblättern des Büros – für die KI (MCP-Werkzeuge normen_suchen,
// norm_lesen) und das Suchfeld der KI-Palette (GET /api/normen/suche). Nur lesen: in den Normenordner wird nie geschrieben.
//
// HERKUNFT: Kopie aus KI_Planungsbuero/src/Kern/Normen.cs (Christian, 16.10.2026: «wir brauchen ein Normenverzeichnis»; Stand der Kopie
// 19.10.2026), angepasst für HLKS-CAD:
//   - Wurzel: Umgebungsvariable HLKS_NORMEN, Vorgabe W:\05_Normen und Merkblätter (dort KI_NORMEN).
//   - Zwischenspeicher: %LOCALAPPDATA%\HLKS_CAD\normen\<Kennung der Wurzel> (dort %LOCALAPPDATA%\KI_Planungsbuero\normen). Bewusst NIE in
//     OneDrive, im Repository oder in einem Projektordner: SIA-Normtexte sind lizenziert und werden nicht synchronisiert. Je Wurzel ein
//     eigener Ordner; die Textdatei trägt Grösse und Änderungszeit der PDF im Namen (eine geänderte PDF wird neu gelesen).
//   - Weggelassen: die Reihen «Gesetz» und «Grundlagen» (gebunden an Ordner des KI-Planungsbüros in OneDrive).
//   - Instanz je Wurzel statt statischer Klasse (Tests mit eigener Wurzel); Treffer mit Ausgabe, Datei, Ziffer (Überschrift vor der Fundstelle)
//     und Auszug von höchstens 600 Zeichen; Suche mit Rückfall «ein Begriff darf fehlen»; norm_lesen höchstens 3 Seiten je Aufruf.
//   - Schutz im Testlauf (HLKS_TEST oder Testrunner): eine Wurzel ausserhalb %TEMP% (z. B. W:) wird weder gelesen noch durchsucht; der
//     Zwischenspeicher liegt dann unter %TEMP%\HLKS_CAD\normen_test.
// Nur deutsche Fassungen (SIA «_d», «_df», «_di» oder ohne Sprachkürzel). Der Volltext jeder Norm wird einmal mit PdfPig gelesen und
// zwischengespeichert; ohne Volltext liest die Suche Normen, deren Kürzel, Titel oder Datei einen Begriff enthält (Rückgriff auf die PDF).

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Hlks.Kern;

public sealed class Normenverzeichnis
{
    public const string Umgebungsvariable = "HLKS_NORMEN";
    public const string VorgabeWurzel = @"W:\05_Normen und Merkblätter";
    /// <summary>Höchstlänge eines Auszugs je Treffer (lizenzierte Texte: nur kurze Auszüge).</summary>
    public const int AuszugMax = 600;
    /// <summary>norm_lesen liefert höchstens so viele Seiten je Aufruf.</summary>
    public const int SeitenJeAufruf = 3;
    /// <summary>Das Verzeichnis (Dateiliste) wird nach dieser Zeit neu eingelesen (neue Normen im Ordner).</summary>
    public static readonly TimeSpan VerzeichnisAlter = TimeSpan.FromHours(1);

    public sealed record Eintrag(string Id, string Reihe, string Kuerzel, string Titel, string Datei, long Groesse, DateTime Geaendert)
    {
        public int Seiten { get; set; }
        public bool Gelesen { get; set; }
    }

    /// <summary>Ein Treffer der Suche. Datei = Pfad relativ zur Wurzel (für norm_lesen), Seite ab 1 (0 = nur der Titel passt).</summary>
    public sealed record Treffer(string Id, string Reihe, string Norm, string Titel, string? Ausgabe, string Datei, int Seite, string? Ziffer, string Auszug, int Punkte);

    // Reihen im Normenordner (wie im KI-Planungsbüro; ohne Gesetze und Fachgrundlagen)
    static readonly (string Ordner, string Reihe)[] Reihen = [("SIA_Normen", "SIA"), ("SWKI", "SWKI"), ("VKF_Brandschutzrichtlinien", "VKF"), ("suissetec", "suissetec"), ("Vorschriften Sprinkler", "Sprinkler")];

    static readonly ConcurrentDictionary<string, Normenverzeichnis> instanzen = new(StringComparer.OrdinalIgnoreCase);

    public string Wurzel { get; }
    /// <summary>Ordner des Zwischenspeichers (Volltexte, verzeichnis.json).</summary>
    public string Ablage { get; }
    /// <summary>Grund, warum das Verzeichnis nicht gelesen wird (Testlauf mit Wurzel ausserhalb %TEMP%); null = lesbar.</summary>
    public string? Sperre { get; }
    public string Zustand { get; private set; } = "nicht aufgebaut";

    readonly object sperre = new();
    List<Eintrag>? verzeichnis;
    DateTimeOffset verzeichnisZeit;
    Task? aufbau;
    int aufbauFertig, aufbauGesamt;

    Normenverzeichnis(string wurzel, string ablage, string? gesperrt)
    {
        Wurzel = wurzel; Ablage = ablage; Sperre = gesperrt;
        if (gesperrt is not null) Zustand = gesperrt;
    }

    /// <summary>Wurzel aus der Umgebung: HLKS_NORMEN, sonst W:\05_Normen und Merkblätter.</summary>
    public static string WurzelAusUmgebung() =>
        Environment.GetEnvironmentVariable(Umgebungsvariable) is { Length: > 0 } w ? w.Trim() : VorgabeWurzel;

    /// <summary>Das Verzeichnis der Wurzel aus der Umgebung (HLKS_NORMEN bzw. Vorgabe).</summary>
    public static Normenverzeichnis Standard() => Fuer(WurzelAusUmgebung());

    /// <summary>Das Verzeichnis einer Wurzel (je Wurzel und Ablage eine Instanz). ablage = null: %LOCALAPPDATA%\HLKS_CAD\normen\&lt;Kennung&gt;
    /// bzw. im Testlauf %TEMP%\HLKS_CAD\normen_test\&lt;Kennung&gt;.</summary>
    public static Normenverzeichnis Fuer(string wurzel, string? ablage = null)
    {
        string voll;
        try { voll = Path.TrimEndingDirectorySeparator(Path.GetFullPath(wurzel)); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { voll = wurzel; }
        var testlauf = FamilienBibliothekDienst.Testlauf();
        var kennung = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(voll.ToLowerInvariant())))[..10].ToLowerInvariant();
        ablage ??= testlauf
            ? Path.Combine(Path.GetTempPath(), "HLKS_CAD", "normen_test", kennung)
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HLKS_CAD", "normen", kennung);
        string? gesperrt = null;
        if (testlauf)
        {
            bool unterTemp;
            try { unterTemp = FamilienBibliothekDienst.UnterTemp(voll); }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { unterTemp = false; }
            if (!unterTemp) gesperrt = $"Testlauf: der Normenordner {voll} liegt nicht unter %TEMP% und wird nicht gelesen ({Umgebungsvariable} auf einen Testordner setzen).";
        }
        return instanzen.GetOrAdd(voll + "|" + ablage, _ => new Normenverzeichnis(voll, ablage, gesperrt));
    }

    // ------------------------------------------------------------------------------------------------ Verzeichnis

    // Nur deutsche Fassungen: SIA «_d», «_df», «_di» oder ohne Sprachkürzel; französische/italienische/englische weg
    static bool Deutsch(string name) => !Regex.IsMatch(Path.GetFileNameWithoutExtension(name), @"(?:_|\d)(f|i|e|fi|ie)$", RegexOptions.IgnoreCase);

    // Reste der alten SIA-Normen-CD (Masken, Register, Inhaltsverzeichnisse) gehören nicht ins Verzeichnis
    static bool Rest(string f) => Regex.IsMatch(Path.GetFileNameWithoutExtension(f), @"^(_|siamask|reserve$|revision$|pub\d|i\d)", RegexOptions.IgnoreCase);

    static string IdVon(string rel) => "N-" + Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(rel.Replace('/', '\\').ToLowerInvariant())))[..6];

    // SIA-Dateien heissen «2024_2021_d.pdf», «118-198_2004_d.pdf», «180_4_d.pdf» → «SIA 2024:2021», «SIA 118/198:2004», «SIA 180/4»
    static string Kuerzel(string reihe, string datei)
    {
        var b = Path.GetFileNameWithoutExtension(datei);
        if (reihe == "SIA") b = Regex.Replace(b, @"^SIA[\s_-]*", "", RegexOptions.IgnoreCase);
        if (reihe == "SIA")
        {
            var m = Regex.Match(b, @"^(\d{2,4})(?:-([0-9A-Z]{1,3}))?(?:_(\d{1,3})(?!\d))?(?:_(\d{4}))?");
            if (m.Success)
            {
                var teil = m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Success ? m.Groups[3].Value : null;
                return "SIA " + m.Groups[1].Value + (teil is null ? "" : "/" + teil) + (m.Groups[4].Success ? ":" + m.Groups[4].Value : "");
            }
        }
        var t = Regex.Replace(b, @"^(VKF_\s*|Richtlinie\s+)", "", RegexOptions.IgnoreCase).Replace('_', ' ').Trim();
        var sw = Regex.Match(t, @"(SWKI\s*(?:VA\s*)?\d{2,4}(?:-\d{1,2})?)", RegexOptions.IgnoreCase);
        if (sw.Success) return Regex.Replace(sw.Value, @"\s+", " ").ToUpperInvariant().Trim();
        if (reihe == "SWKI") { var r = Regex.Match(t, @"^(\d{2,4}-\d{1,2}[A-Z]?)"); if (r.Success) return "SWKI " + r.Value; }
        return reihe == "Merkblatt" ? "" : reihe;
    }

    static string TitelAusDatei(string datei) => Regex.Replace(Path.GetFileNameWithoutExtension(datei).Replace('_', ' '), @"\s+", " ").Trim();

    /// <summary>Alle Normen der Wurzel (zwischengespeichert; neu = Ordner neu einlesen). Wurzel nicht erreichbar: Verzeichnis aus dem
    /// Zwischenspeicher. Gesperrt (Testlauf): leer.</summary>
    public List<Eintrag> Verzeichnis(bool neu = false)
    {
        lock (sperre)
        {
            if (Sperre is not null) return [];
            if (verzeichnis is not null && !neu && DateTimeOffset.Now - verzeichnisZeit < VerzeichnisAlter) return [.. verzeichnis];   // Kopie: der Aufbau im Hintergrund ersetzt Einträge
            var gespeichert = LadenGespeichert();
            bool erreichbar;
            try { erreichbar = Directory.Exists(Wurzel); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { erreichbar = false; }
            if (!erreichbar)
            {
                verzeichnis = gespeichert;
                verzeichnisZeit = DateTimeOffset.Now;
                Zustand = $"{Wurzel} nicht erreichbar – Verzeichnis aus dem Zwischenspeicher ({gespeichert.Count} Normen)";
                return [.. verzeichnis];   // Kopie: der Aufbau im Hintergrund ersetzt Einträge
            }
            var liste = new List<Eintrag>();
            void Aufnehmen(string pfad, string reihe)
            {
                var fi = new FileInfo(pfad);
                var rel = Path.GetRelativePath(Wurzel, pfad);
                var id = IdVon(rel);
                var alt = gespeichert.FirstOrDefault(e => e.Datei == rel && e.Groesse == fi.Length && e.Geaendert == fi.LastWriteTimeUtc);
                var textDatei = TextDatei(id, fi.Length, fi.LastWriteTimeUtc);
                string? titel = alt?.Titel, kuerzel = alt?.Kuerzel;
                int seitenZahl = alt?.Seiten ?? 0;
                var gelesen = File.Exists(textDatei);
                if ((titel is null || Regex.IsMatch(titel, "^(ohne titel|untitled)", RegexOptions.IgnoreCase)) && gelesen)
                {   // Volltext schon da: Titel und Kürzel aus der Titelseite statt aus dem Dateinamen
                    var probe = new Eintrag(id, reihe, Kuerzel(reihe, pfad), TitelAusDatei(pfad), rel, fi.Length, fi.LastWriteTimeUtc);
                    var alleSeiten = File.ReadAllText(textDatei, Encoding.UTF8).Split('\f');
                    seitenZahl = alleSeiten.Length;
                    titel = TitelAusText(probe, alleSeiten.Take(2).ToArray(), null);
                    kuerzel = KuerzelAusText(probe, alleSeiten.Take(2).ToArray());
                }
                liste.Add(new Eintrag(id, reihe, kuerzel ?? Kuerzel(reihe, pfad), titel ?? TitelAusDatei(pfad), rel, fi.Length, fi.LastWriteTimeUtc)
                    { Seiten = seitenZahl, Gelesen = gelesen });
            }
            try
            {
                foreach (var (ordner, reihe) in Reihen)
                {
                    var d = Path.Combine(Wurzel, ordner);
                    if (!Directory.Exists(d)) continue;
                    // SIA: nur die Normen im Ordner selbst (index5/index7 sind Reste einer alten Normen-CD)
                    var opt = reihe == "SIA" ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories;
                    foreach (var f in Directory.EnumerateFiles(d, "*.pdf", opt).Where(Deutsch).Where(f => !Rest(f)))
                        Aufnehmen(f, reihe == "VKF" && !Path.GetFileName(f).StartsWith("VKF", StringComparison.OrdinalIgnoreCase) ? "Vorlage" : reihe);
                }
                foreach (var f in Directory.EnumerateFiles(Wurzel, "*.pdf", SearchOption.TopDirectoryOnly).Where(Deutsch)) Aufnehmen(f, "Merkblatt");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                verzeichnis = gespeichert;
                verzeichnisZeit = DateTimeOffset.Now;
                Zustand = $"{Wurzel} nicht lesbar ({ex.Message}) – Verzeichnis aus dem Zwischenspeicher ({gespeichert.Count} Normen)";
                return [.. verzeichnis];   // Kopie: der Aufbau im Hintergrund ersetzt Einträge
            }
            verzeichnis = liste.OrderBy(e => e.Reihe).ThenBy(e => e.Kuerzel, StringComparer.OrdinalIgnoreCase).ThenBy(e => e.Titel).ToList();
            verzeichnisZeit = DateTimeOffset.Now;
            Speichern();
            Zustand = $"{verzeichnis.Count} Normen, {verzeichnis.Count(e => e.Gelesen)} mit Volltext";
            return [.. verzeichnis];   // Kopie: der Aufbau im Hintergrund ersetzt Einträge
        }
    }

    string TextDatei(string id, long groesse, DateTime geaendert) =>
        Path.Combine(Ablage, $"{id}-{Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes($"{groesse}|{geaendert.Ticks}")))[..8].ToLowerInvariant()}.txt");
    string TextDatei(Eintrag e) => TextDatei(e.Id, e.Groesse, e.Geaendert);
    string VerzeichnisDatei => Path.Combine(Ablage, "verzeichnis.json");

    List<Eintrag> LadenGespeichert()
    {
        try { return File.Exists(VerzeichnisDatei) ? JsonSerializer.Deserialize<List<Eintrag>>(File.ReadAllText(VerzeichnisDatei, Encoding.UTF8)) ?? [] : []; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException) { return []; }
    }

    void Speichern()
    {
        if (verzeichnis is null) return;
        AtomarSchreiben(VerzeichnisDatei, JsonSerializer.Serialize(verzeichnis, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>Zwischenspeicher atomar schreiben (MCP-Server und Arbeitsplatz-Server können gleichzeitig lesen); Fehler übergehen –
    /// dann liest der nächste Zugriff die PDF eben noch einmal.</summary>
    void AtomarSchreiben(string ziel, string inhalt)
    {
        try
        {
            Directory.CreateDirectory(Ablage);
            var temp = ziel + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
            File.WriteAllText(temp, inhalt, new UTF8Encoding(false));
            try { File.Move(temp, ziel, overwrite: true); }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Seiten einer Norm (aus dem Zwischenspeicher, sonst aus der PDF lesen und ablegen).</summary>
    string[] SeitenVon(Eintrag e)
    {
        var td = TextDatei(e);
        try { if (File.Exists(td)) return File.ReadAllText(td, Encoding.UTF8).Split('\f'); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        var pfad = Path.Combine(Wurzel, e.Datei);
        if (!File.Exists(pfad)) return [];
        string[] seiten;
        string? metaTitel = null;
        try
        {
            using var pdf = PdfDocument.Open(pfad, new ParsingOptions { UseLenientParsing = true, SkipMissingFonts = true });
            metaTitel = pdf.Information.Title;
            seiten = pdf.GetPages().Select(p => { try { return ContentOrderTextExtractor.GetText(p); } catch (Exception) { return ""; } }).ToArray();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { seiten = []; }
        if (seiten.Length > 0) AtomarSchreiben(td, string.Join('\f', seiten));
        lock (sperre)
        {
            e.Seiten = seiten.Length; e.Gelesen = seiten.Length > 0;
            var t = TitelAusText(e, seiten, metaTitel) ?? e.Titel;
            var k = KuerzelAusText(e, seiten) ?? e.Kuerzel;
            if (verzeichnis is not null)
            {
                var i = verzeichnis.IndexOf(e);
                if (i >= 0) verzeichnis[i] = e with { Titel = t, Kuerzel = k, Seiten = e.Seiten, Gelesen = e.Gelesen };
                Speichern();
            }
        }
        return seiten;
    }

    static string? KuerzelAusText(Eintrag e, string[] seiten)
    {
        var kopf = string.Join("\n", seiten.Take(2));
        if (e.Reihe == "VKF") { var m = Regex.Match(kopf, @"\b(\d{1,2}-\d{2})\s?de\b"); if (m.Success) return "VKF BSR " + m.Groups[1].Value; }
        if (e.Reihe == "SIA")
        {   // nur die Nummer aus dem Dateinamen übernehmen (Merkblätter nennen auf der Titelseite auch andere Normen)
            var nr = Regex.Match(e.Kuerzel, @"^SIA (\d{2,4})").Groups[1].Value;
            var m = Regex.Matches(kopf, @"\bSIA\s+(\d{2,4}(?:/\d{1,3})?)(?::\s?(\d{4}))?").FirstOrDefault(x => nr.Length > 0 && x.Groups[1].Value.StartsWith(nr, StringComparison.Ordinal));
            if (m is not null) return "SIA " + m.Groups[1].Value + (m.Groups[2].Success ? ":" + m.Groups[2].Value : Regex.Match(e.Kuerzel, @":\d{4}$").Value);
        }
        return null;
    }

    // Titel: bei SIA aus der Titelseite (erste sinnvolle Zeilen), sonst Metadaten, sonst Dateiname
    static string? TitelAusText(Eintrag e, string[] seiten, string? meta)
    {
        if (e.Reihe == "SIA" && seiten.Length > 0)
        {
            // Titelseiten bestehen grösstenteils aus Verlag (Verein, Adresse, SNV-Vermerk, Ersetzt-Hinweis) und den
            // fremdsprachigen Titeln; der deutsche Titel steht oft über zwei Zeilen («Allgemeine Bedingungen | für …»)
            var weg = new Regex(@"verein|société|societ|swiss|selnau|zürich|zurich|www\.|postfach|copyright|preisgruppe|herausgeber|schweizer norm|norme suisse|norma svizzera|ersetzt|eingetragene|snv|telefon|ingenieur|architekt|ingénieur|ingegner|engineers|ausgabe|bauwesen|schweizerisch|^sia\b|^norm$|^merkblatt\b|^ordnung|^[^äöü]*[éèàêç]|^\d|^©|^ics|referenznummer|druck|verkauf|\b(pour|des|générales|et|per|condizioni|della|of|the)\b", RegexOptions.IgnoreCase);
            var zeilen = seiten[0].Split('\n').Select(z => z.Trim()).Where(z => z.Length >= 4 && Regex.IsMatch(z, "[a-zäöü]{3}") && !weg.IsMatch(z)).ToList();
            if (zeilen.Count > 0)
            {
                var titel = zeilen[0];
                for (int i = 1; i < zeilen.Count && i < 4 && (titel.EndsWith('–') || Regex.IsMatch(zeilen[i], @"^(für|der|des|und|zur|zum|im|in|von|mit|bei|–|-|[a-zäöü])")); i++) titel += " " + zeilen[i];
                return titel.Length > 140 ? titel[..140] : titel;
            }
        }
        if (!string.IsNullOrWhiteSpace(meta) && meta.Length > 6 && !Regex.IsMatch(meta, @"^(microsoft|untitled|ohne titel|sans titre|dokument|document|\d+$)", RegexOptions.IgnoreCase)) return meta.Trim();
        return null;
    }

    // ------------------------------------------------------------------------------------------------ Volltext aufbauen

    /// <summary>Stand des Aufbaus im Hintergrund.</summary>
    public (bool Laeuft, int Fertig, int Gesamt) AufbauStand
    {
        get { lock (sperre) return (aufbau is { IsCompleted: false }, aufbauFertig, aufbauGesamt); }
    }

    /// <summary>Liest den Volltext aller Normen ohne Zwischenspeicher einmal aus – im Hintergrund, höchstens ein Aufbau zugleich
    /// (~350 PDF über VPN dauern einige Minuten). Gibt den laufenden bzw. neuen Aufbau zurück.</summary>
    public Task AufbauenImHintergrund()
    {
        lock (sperre)
        {
            if (Sperre is not null) return Task.CompletedTask;
            if (aufbau is { IsCompleted: false }) return aufbau;
            aufbau = Task.Factory.StartNew(Aufbauen, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            return aufbau;
        }
    }

    void Aufbauen()
    {
        try
        {
            var liste = Verzeichnis(neu: true).ToList();
            var offen = liste.Where(e => !File.Exists(TextDatei(e))).ToList();
            lock (sperre) { aufbauFertig = 0; aufbauGesamt = offen.Count; }
            foreach (var e in offen)
            {
                SeitenVon(e);
                lock (sperre) { aufbauFertig++; Zustand = $"Volltext wird aufgebaut: {aufbauFertig}/{aufbauGesamt}"; }
            }
            Verzeichnis(neu: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { lock (sperre) Zustand = "Aufbau abgebrochen: " + ex.Message; }
    }

    // ------------------------------------------------------------------------------------------------ Suchen und Lesen

    // Füllwörter einer Frage («Wie gross ist der Abstand …») – gesucht wird nach den Fachwörtern
    static readonly HashSet<string> Fuellwoerter = new(StringComparer.OrdinalIgnoreCase)
    {
        "der", "die", "das", "den", "dem", "des", "ein", "eine", "einer", "eines", "einem", "einen", "und", "oder", "für", "fuer", "von", "vom", "zu", "zum",
        "zur", "im", "in", "an", "am", "auf", "aus", "bei", "mit", "nach", "über", "ueber", "unter", "ist", "sind", "wie", "was", "wer", "wo", "welche",
        "welcher", "welches", "gross", "muss", "müssen", "darf", "soll", "kann", "nicht", "es", "sich", "auch", "nur", "noch", "laut", "gemäss", "gemaess",
        "norm", "steht", "gilt", "the", "of",
    };

    /// <summary>Fachbegriffe einer Suche (ab 2 Zeichen, ohne Satzzeichen und Füllwörter; bleibt nichts übrig, alle Wörter).</summary>
    public static string[] Begriffe(string suche)
    {
        var alle = Regex.Split(suche ?? "", @"[\s,;:!?«»""()\[\]]+").Select(b => b.Trim('.', '\'', '’')).Where(b => b.Length >= 2).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var fach = alle.Where(b => !Fuellwoerter.Contains(b)).ToArray();
        return fach.Length > 0 ? fach : alle;
    }

    /// <summary>Volltextsuche: Seiten, die alle Begriffe enthalten (sonst alle bis auf einen); Treffer nach Häufigkeit, je Norm höchstens drei
    /// Seiten. Ohne Volltext werden Normen gelesen, deren Kürzel, Titel oder Datei einen Begriff enthält.</summary>
    public (List<Treffer> Treffer, bool Vollstaendig) Suchen(string suche, string? reihe = null, int max = 15)
    {
        var begriffe = Begriffe(suche);
        if (begriffe.Length == 0) return ([], true);
        var liste = Verzeichnis().Where(e => reihe is null || e.Reihe.Equals(reihe, StringComparison.OrdinalIgnoreCase)).ToList();
        var treffer = new List<Treffer>();
        var teilweise = new List<Treffer>();
        int mindestens = begriffe.Length >= 2 ? begriffe.Length - 1 : 1;
        // Kleine Verzeichnisse (wenige PDF ohne Volltext) gleich ganz lesen; das grosse Büroverzeichnis baut den Volltext im Hintergrund auf
        bool alleLesen = liste.Count(e => !HatText(e)) <= SofortLesenMax;
        foreach (var e in liste)
        {
            bool imNamen = begriffe.Any(b => $"{e.Kuerzel} {e.Titel} {e.Datei}".Contains(b, StringComparison.OrdinalIgnoreCase));
            if (!HatText(e) && !imNamen && !alleLesen) continue;
            var seiten = SeitenVon(e);
            var voll = new List<Treffer>();
            var teil = new List<Treffer>();
            for (int i = 0; i < seiten.Length; i++)
            {
                var t = Entbinden(seiten[i]);
                var gefunden = begriffe.Where(b => t.Contains(b, StringComparison.OrdinalIgnoreCase)).ToList();
                if (gefunden.Count < mindestens) continue;
                int punkte = gefunden.Count * 10 + gefunden.Sum(b => Regex.Matches(t, Regex.Escape(b), RegexOptions.IgnoreCase).Count) + (imNamen ? 5 : 0);
                var pos = t.IndexOf(gefunden[0], StringComparison.OrdinalIgnoreCase);
                var tr = new Treffer(e.Id, e.Reihe, NormName(e), e.Titel, Ausgabe(e), e.Datei, i + 1, Ziffer(t, pos), Auszug(t, pos), punkte);
                (gefunden.Count == begriffe.Length ? voll : teil).Add(tr);
            }
            treffer.AddRange(voll.OrderByDescending(x => x.Punkte).Take(3));
            teilweise.AddRange(teil.OrderByDescending(x => x.Punkte).Take(3));
            if (voll.Count == 0 && teil.Count == 0 && imNamen)
                teilweise.Add(new Treffer(e.Id, e.Reihe, NormName(e), e.Titel, Ausgabe(e), e.Datei, 0, null,
                    "Titel passt – Inhaltsverzeichnis mit norm_lesen ab Seite 1 ansehen.", 3));
        }
        if (treffer.Count > 0) return (treffer.OrderByDescending(x => x.Punkte).Take(max).ToList(), true);
        return (teilweise.OrderByDescending(x => x.Punkte).Take(max).ToList(), false);
    }

    static string NormName(Eintrag e) => e.Kuerzel.Length > 0 ? e.Kuerzel : e.Titel;

    /// <summary>Höchstens so viele PDF ohne Volltext liest die Suche selbst; sonst nur die, deren Kürzel, Titel oder Datei passt.</summary>
    public const int SofortLesenMax = 25;

    bool HatText(Eintrag e) => e.Gelesen || File.Exists(TextDatei(e));

    /// <summary>Normen ohne Volltext (Suche dort nur über Kürzel, Titel und Datei).</summary>
    public int OhneVolltext() => Verzeichnis().Count(e => !HatText(e));

    /// <summary>Ausgabe (Jahr): aus dem Kürzel («SIA 2024:2021»), sonst einer Jahreszahl im Dateinamen.</summary>
    static string? Ausgabe(Eintrag e)
    {
        var k = Regex.Match(e.Kuerzel, @":(\d{4})$");
        if (k.Success) return k.Groups[1].Value;
        var d = Regex.Match(Path.GetFileNameWithoutExtension(e.Datei), @"(?<!\d)(19[5-9]\d|20\d\d)(?!\d)");
        return d.Success ? d.Groups[1].Value : null;
    }

    /// <summary>Getrennte Wörter am Zeilenende zusammenfügen («Trink-\nwasser» → «Trinkwasser»), damit die Suche sie findet.</summary>
    static string Entbinden(string text) => Regex.Replace(text, @"(?<=\p{Ll})-[ \t]*\r?\n[ \t]*(?=\p{Ll})", "");

    /// <summary>Ziffer: die letzte Überschrift mit Nummer («4.2.1 Bemessung») vor der Fundstelle auf derselben Seite.</summary>
    static string? Ziffer(string seite, int pos)
    {
        if (pos < 0) return null;
        var davor = seite[..Math.Min(seite.Length, pos + 1)];
        var m = Regex.Matches(davor, @"(?m)^[ \t]*(\d{1,2}(?:\.\d{1,3}){0,4})\.?[ \t]+\p{Lu}");
        return m.Count > 0 ? m[^1].Groups[1].Value : null;
    }

    /// <summary>Auszug um die Fundstelle, höchstens 600 Zeichen (lizenzierte Texte: nur kurze Auszüge).</summary>
    static string Auszug(string text, int pos)
    {
        var flach = Regex.Replace(text, @"\s+", " ").Trim();
        if (flach.Length == 0) return "";
        // Position im flachen Text ungefähr: Anteil der Zeichen vor der Fundstelle
        var vor = pos <= 0 ? 0 : Regex.Replace(text[..Math.Min(text.Length, pos)], @"\s+", " ").TrimStart().Length;
        int a = Math.Max(0, vor - 200), b = Math.Min(flach.Length, a + AuszugMax - 2);
        if (a > 0) { var leer = flach.IndexOf(' ', a); if (leer > 0 && leer < a + 30) a = leer + 1; }
        if (b < flach.Length) { var leer = flach.LastIndexOf(' ', b); if (leer > a + 100) b = leer; }
        var s = (a > 0 ? "…" : "") + flach[a..b] + (b < flach.Length ? "…" : "");
        return s.Length <= AuszugMax ? s : s[..AuszugMax];
    }

    /// <summary>Eintrag über Datei (relativ zur Wurzel, / oder \), Id oder Kürzel. Nie ein beliebiger Pfad.</summary>
    public Eintrag? Finden(string bezug)
    {
        if (string.IsNullOrWhiteSpace(bezug)) return null;
        var b = bezug.Trim().Replace('/', '\\');
        var l = Verzeichnis();
        static string Normiert(string s) => Regex.Replace(s.ToLowerInvariant(), @"[\s:._-]", "");
        return l.FirstOrDefault(e => e.Datei.Equals(b, StringComparison.OrdinalIgnoreCase))
            ?? l.FirstOrDefault(e => e.Id.Equals(b, StringComparison.OrdinalIgnoreCase))
            ?? l.FirstOrDefault(e => e.Kuerzel.Length > 0 && e.Kuerzel.Equals(bezug.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? l.FirstOrDefault(e => e.Kuerzel.Length > 0 && Normiert(e.Kuerzel) == Normiert(bezug));
    }

    /// <summary>Text der Seiten von bis bis (höchstens 3 Seiten je Aufruf) als Textansicht für die KI.</summary>
    public string Lesen(string datei, int seite, int? bis = null)
    {
        if (Sperre is not null) throw new HlksFehler(Sperre, $"{Umgebungsvariable} auf einen Ordner unter %TEMP% setzen.");
        var e = Finden(datei) ?? throw new HlksFehler($"«{datei}» ist nicht im Normenverzeichnis ({Wurzel}).", "Zuerst normen_suchen verwenden und die Datei aus dem Treffer angeben.");
        var seiten = SeitenVon(e);
        if (seiten.Length == 0)
            throw new HlksFehler($"{NormName(e)} ({e.Datei}) lässt sich nicht lesen – {Wurzel} nicht erreichbar oder PDF ohne Textebene (Scan).", "Netzwerk/VPN prüfen; bei einem Scan die PDF selbst ansehen.");
        if (seite < 1 || seite > seiten.Length)
            throw new HlksFehler($"Seite {seite} gibt es in {NormName(e)} nicht (Seiten 1 bis {seiten.Length}).", "Eine Seite aus dem Treffer von normen_suchen angeben.");
        var ende = Math.Clamp(bis ?? seite, seite, seiten.Length);
        var gekuerzt = ende - seite + 1 > SeitenJeAufruf;
        if (gekuerzt) ende = seite + SeitenJeAufruf - 1;
        var sb = new StringBuilder();
        sb.Append($"{NormName(e)} – {e.Titel}");
        if (Ausgabe(e) is { } a) sb.Append($" · Ausgabe {a}");
        sb.Append($" · {seiten.Length} Seiten · Datei {e.Datei}\n");
        sb.Append("Zitieren: Norm, Ausgabe, Ziffer und Seite. Der Text ist lizenziert – in Antworten nur kurze Auszüge wiedergeben.\n");
        for (int i = seite; i <= ende; i++) sb.Append($"\n=== Seite {i} ===\n{seiten[i - 1].Trim()}\n");
        if (gekuerzt) sb.Append($"\n[höchstens {SeitenJeAufruf} Seiten je Aufruf – weiter mit seite {ende + 1}]\n");
        return sb.ToString();
    }

    /// <summary>Suche als JSON (normen_suchen, /api/normen/suche).</summary>
    public JsonObject SuchenJson(string suche, string? reihe = null, int max = 15)
    {
        if (string.IsNullOrWhiteSpace(suche)) throw new HlksFehler("Die Suche ist leer.", "Fachbegriffe angeben, z. B. «Mindestgefälle Grundleitung».");
        if (suche.Length > 500) throw new HlksFehler("Die Suche ist zu lang (höchstens 500 Zeichen).", "Wenige Fachbegriffe angeben.");
        var (treffer, vollstaendig) = Sperre is null ? Suchen(suche, reihe, max) : ([], true);
        var arr = new JsonArray(treffer.Select(t => (JsonNode)new JsonObject
        {
            ["id"] = t.Id, ["reihe"] = t.Reihe, ["norm"] = t.Norm, ["titel"] = t.Titel, ["ausgabe"] = t.Ausgabe, ["datei"] = t.Datei,
            ["seite"] = t.Seite, ["ziffer"] = t.Ziffer, ["auszug"] = t.Auszug,
        }).ToArray());
        string hinweis = Sperre is not null ? Sperre
            : arr.Count == 0 ? (Verzeichnis().Count == 0 ? $"Keine Normen gefunden – {Zustand}." : "Keine Seite enthält die Begriffe – weniger oder andere Begriffe (Fachwörter der Norm) versuchen.")
            : vollstaendig ? "Mit norm_lesen (datei, seite) die Seite ganz lesen; Norm, Ausgabe, Ziffer und Seite zitieren. Die Ziffer stammt aus der Überschrift vor der Fundstelle – beim Lesen prüfen."
            : "Keine Seite enthält alle Begriffe; gezeigt sind Seiten mit allen bis auf einen. Mit norm_lesen prüfen.";
        var ohne = Sperre is null ? OhneVolltext() : 0;
        if (ohne > 0) hinweis += $" Volltext fehlt noch für {ohne} Norm(en) – dort wurde nur in Kürzel, Titel und Dateiname gesucht ({Zustand}).";
        return new JsonObject
        {
            ["suche"] = suche, ["begriffe"] = new JsonArray(Begriffe(suche).Select(b => (JsonNode)JsonValue.Create(b)).ToArray()),
            ["quelle"] = Wurzel, ["stand"] = Zustand, ["vollstaendig"] = vollstaendig, ["ohneVolltext"] = ohne, ["treffer"] = arr, ["hinweis"] = hinweis,
        };
    }

    /// <summary>Zustand für die Oberfläche: Quelle, Anzahl, Volltext, Aufbau.</summary>
    public JsonObject ZustandJson()
    {
        var l = Verzeichnis();
        var (laeuft, fertig, gesamt) = AufbauStand;
        return new JsonObject
        {
            ["quelle"] = Wurzel, ["verfuegbar"] = Sperre is null && l.Count > 0, ["gesperrt"] = Sperre, ["zustand"] = Zustand,
            ["normen"] = l.Count, ["mitVolltext"] = l.Count(e => e.Gelesen || File.Exists(TextDatei(e))),
            ["aufbau"] = new JsonObject { ["laeuft"] = laeuft, ["fertig"] = fertig, ["gesamt"] = gesamt },
        };
    }
}
