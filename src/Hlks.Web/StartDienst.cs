// Daten für den Startbildschirm (entwurf/startbildschirm.md, Teil S1). Nur lesend gegenüber den Projekten und der
// Familienbibliothek; einzig das Vorschaubild wird in den Ordner des OFFENEN Projekts geschrieben.
//
//   Ablage(neu)        alle KICAD-Projekte unter der Projektablage: genau <pfad>\*\<kicadOrdner>\projekt.json (feste Tiefe, nie
//                      tiefer). Pfad: Einstellungen.ProjektAblage › HLKS_PROJEKTABLAGE › kataloge/buero.json (projektablage).
//                      Zwischenspeicher 10 min; Suche im Hintergrund (mehrere Aufrufe teilen eine Suche); nicht erreichbar →
//                      Ergebnis mit Hinweis statt Fehler (der Rest des Startbildschirms geht weiter).
//   Ordner(pfad)       Unterordner eines Ordners für den Ordner-Browser (leer = Laufwerke); Kennzeichen «KICAD-Projekt»
//                      (projekt.json im Ordner) und «enthält Projekt» (projekt.json unter <kicadOrdner>).
//   FamilienZuletzt(n) zuletzt bearbeitete Familien aus dem Bibliotheksindex (ohne offenes Projekt lesbar, nichts schreibend).
//   VorschauSpeichern  vorschau.png (+ vorschau.json mit Name, Projektnummer, Stand) im Ordner des offenen Projekts.
//   Pruefen(ordner)    Vorschau/Erreichbarkeit der Einträge von «zuletzt» (mit Zeitlimit; ein toter NAS hält nichts auf).
// Schutz im Testlauf (HLKS_TEST oder Testrunner): eine Projektablage ausserhalb von %TEMP% wird nicht gelesen, ein Projekt
// ausserhalb von %TEMP% bekommt kein Vorschaubild – nie ein Zugriff auf W: aus Tests heraus.

using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
using Hlks.Kern;

namespace Hlks.Web;

public sealed record ProjektZeile(string Name, string Objekt, string Ordner, string? Nummer, DateTimeOffset Geaendert, bool Vorschau, long VorschauV);

public sealed class AblageErgebnis
{
    public string? Pfad { get; init; }
    public string KicadOrdner { get; init; } = "";
    public bool Erreichbar { get; init; }
    public string? Hinweis { get; init; }
    public DateTimeOffset Gesucht { get; init; } = DateTimeOffset.Now;
    public long DauerMs { get; init; }
    public bool Zwischengespeichert { get; init; }
    public bool Laeuft { get; init; }
    public List<ProjektZeile> Projekte { get; init; } = [];
}

public sealed class StartDienst(ProjektDienst dienst)
{
    const string VorschauDatei = "vorschau.png";
    const string VorschauInfo = "vorschau.json";
    public const int VorschauMaxBytes = 4 * 1024 * 1024;
    static readonly TimeSpan KurzesLimit = TimeSpan.FromSeconds(4);

    readonly object sperre = new();
    AblageErgebnis? letzte;
    DateTimeOffset letzteGueltigBis;
    Task<AblageErgebnis>? suche;

    Einstellungen E => dienst.Einstellungen;

    // ---------------------------------------------------------------- Konfiguration

    /// <summary>Pfad und Unterordner der Projektablage; Pfad null, wenn nichts eingestellt ist.</summary>
    public (string? Pfad, string KicadOrdner) Konfiguration()
    {
        string kicad = @"c_Pläne und Modelle\e_KICAD";
        string? pfad = null;
        try
        {
            var datei = Path.Combine(dienst.Katalog.Ordner, "buero.json");
            if (File.Exists(datei) && JsonNode.Parse(File.ReadAllText(datei))?["projektablage"] is JsonObject o)
            {
                pfad = o["pfad"] is JsonValue p && p.TryGetValue<string>(out var ps) && !string.IsNullOrWhiteSpace(ps) ? ps.Trim() : null;
                if (o["kicadOrdner"] is JsonValue k && k.TryGetValue<string>(out var ks) && !string.IsNullOrWhiteSpace(ks)) kicad = ks.Trim();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or HlksFehler) { /* ohne buero.json: nur Umgebung */ }
        var umgebung = E.ProjektAblage ?? Environment.GetEnvironmentVariable("HLKS_PROJEKTABLAGE");
        if (!string.IsNullOrWhiteSpace(umgebung)) pfad = umgebung.Trim();
        return (pfad, kicad);
    }

    static string? AblageSperre(string pfad)
    {
        try
        {
            if (FamilienBibliothekDienst.Testlauf() && (FamilienBibliothekDienst.AufNetz(pfad) || !FamilienBibliothekDienst.UnterTemp(pfad)))
                return $"Testlauf: die Projektablage {pfad} liegt nicht unter %TEMP% und wird nicht gelesen (HLKS_PROJEKTABLAGE setzen).";
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        { return $"Die Projektablage «{pfad}» ist kein gültiger Pfad."; }
        return null;
    }

    // ---------------------------------------------------------------- Projektsuche

    public void Aufwaermen()
    {
        var (pfad, _) = Konfiguration();
        if (pfad is null) return;
        _ = Task.Run(async () => { try { await Ablage(false); } catch (Exception) { /* der Startbildschirm fragt später selbst */ } });
    }

    public async Task<AblageErgebnis> Ablage(bool neu)
    {
        Task<AblageErgebnis> lauf;
        lock (sperre)
        {
            if (!neu && letzte is not null && DateTimeOffset.Now < letzteGueltigBis) return Kopie(letzte, zwischen: true);
            lauf = suche ??= Task.Run(Suchen);
        }
        try { return await lauf.WaitAsync(E.StartZeitlimit); }
        catch (TimeoutException)
        {
            // Die Suche läuft weiter und füllt den Zwischenspeicher; hier gilt der letzte Stand (oder ein Hinweis)
            var (pfad, kicad) = Konfiguration();
            lock (sperre)
                return letzte is not null ? Kopie(letzte, zwischen: true, laeuft: true)
                    : new AblageErgebnis { Pfad = pfad, KicadOrdner = kicad, Erreichbar = false, Laeuft = true,
                        Hinweis = $"Die Suche in {pfad} dauert länger als {E.StartZeitlimit.TotalSeconds:0} s (Netzwerk/VPN?). Sie läuft weiter; «Neu suchen» zeigt das Ergebnis, sobald sie fertig ist." };
        }
    }

    static AblageErgebnis Kopie(AblageErgebnis a, bool zwischen, bool laeuft = false) => new()
    {
        Pfad = a.Pfad, KicadOrdner = a.KicadOrdner, Erreichbar = a.Erreichbar, Hinweis = a.Hinweis, Gesucht = a.Gesucht, DauerMs = a.DauerMs,
        Projekte = a.Projekte, Zwischengespeichert = zwischen, Laeuft = laeuft,
    };

    AblageErgebnis Suchen()
    {
        var uhr = Stopwatch.StartNew();
        var (pfad, kicad) = Konfiguration();
        AblageErgebnis erg;
        try
        {
            erg = SuchenOhneZwischenspeicher(pfad, kicad, uhr);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            erg = new AblageErgebnis { Pfad = pfad, KicadOrdner = kicad, Erreichbar = false, DauerMs = uhr.ElapsedMilliseconds,
                Hinweis = $"Die Projektablage {pfad} ist nicht erreichbar: {ex.Message} Netzwerk/VPN prüfen und «Neu suchen» wählen." };
        }
        lock (sperre)
        {
            letzte = erg;
            // Ein Fehlschlag wird nur kurz gemerkt, damit ein toter NAS nicht bei jedem Aufruf neu zehn Sekunden kostet
            letzteGueltigBis = DateTimeOffset.Now + (erg.Erreichbar ? E.AblageZwischenspeicher : TimeSpan.FromSeconds(Math.Min(30, E.AblageZwischenspeicher.TotalSeconds)));
            suche = null;
        }
        return erg;
    }

    AblageErgebnis SuchenOhneZwischenspeicher(string? pfad, string kicad, Stopwatch uhr)
    {
        if (string.IsNullOrWhiteSpace(pfad))
            return new AblageErgebnis { KicadOrdner = kicad, Erreichbar = false, Hinweis = "Keine Projektablage eingestellt (kataloge/buero.json › projektablage, oder HLKS_PROJEKTABLAGE).", DauerMs = uhr.ElapsedMilliseconds };
        if (AblageSperre(pfad) is { } gesperrt)
            return new AblageErgebnis { Pfad = pfad, KicadOrdner = kicad, Erreichbar = false, Hinweis = gesperrt, DauerMs = uhr.ElapsedMilliseconds };
        if (!Directory.Exists(pfad))
            return new AblageErgebnis { Pfad = pfad, KicadOrdner = kicad, Erreichbar = false, DauerMs = uhr.ElapsedMilliseconds,
                Hinweis = $"Die Projektablage {pfad} ist nicht erreichbar. Netzwerk/VPN prüfen und «Neu suchen» wählen; offene und zuletzt geöffnete Projekte gehen weiter." };

        var objekte = new DirectoryInfo(pfad).EnumerateDirectories()
            .Where(d => !d.Name.StartsWith('.') && !d.Name.StartsWith('$')).Select(d => d.FullName).ToList();
        var liste = new List<ProjektZeile>();
        var fehler = 0;
        var schloss = new object();
        Parallel.ForEach(objekte, new ParallelOptions { MaxDegreeOfParallelism = 12 }, objekt =>
        {
            try
            {
                var ordner = Path.Combine(objekt, kicad);
                var kopf = Path.Combine(ordner, "projekt.json");
                var fi = new FileInfo(kopf);
                if (!fi.Exists) return;
                var z = Zeile(objekt, ordner, fi);
                lock (schloss) liste.Add(z);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Interlocked.Increment(ref fehler); }
        });
        liste.Sort((a, b) => string.Compare(a.Objekt, b.Objekt, StringComparison.CurrentCultureIgnoreCase));
        return new AblageErgebnis
        {
            Pfad = pfad, KicadOrdner = kicad, Erreichbar = true, DauerMs = uhr.ElapsedMilliseconds, Projekte = liste,
            Hinweis = fehler > 0 ? $"{fehler} Ordner waren nicht lesbar und fehlen in der Liste." : null,
        };
    }

    static ProjektZeile Zeile(string objekt, string ordner, FileInfo kopf)
    {
        string name = Path.GetFileName(objekt);
        try
        {
            if (JsonNode.Parse(File.ReadAllText(kopf.FullName)) is JsonObject o)
                foreach (var (k, v) in o)
                    if (k.Equals("name", StringComparison.OrdinalIgnoreCase) && v is JsonValue jv && jv.TryGetValue<string>(out var n) && !string.IsNullOrWhiteSpace(n)) name = n;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException) { /* Ordnername */ }
        var geaendert = kopf.LastWriteTimeUtc;
        foreach (var unter in new[] { "aenderungen", "stand" })
        {
            var t = Directory.GetLastWriteTimeUtc(Path.Combine(ordner, unter));
            if (t > geaendert) geaendert = t;
        }
        string? nummer = null;
        try
        {
            var info = Path.Combine(ordner, VorschauInfo);
            if (File.Exists(info) && JsonNode.Parse(File.ReadAllText(info)) is JsonObject i
                && i["nummer"] is JsonValue nv && nv.TryGetValue<string>(out var ns) && !string.IsNullOrWhiteSpace(ns)) nummer = ns;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException) { /* ohne Nummer */ }
        var bild = new FileInfo(Path.Combine(ordner, VorschauDatei));
        return new ProjektZeile(name, Path.GetFileName(objekt), ordner, nummer, new DateTimeOffset(geaendert, TimeSpan.Zero), bild.Exists, bild.Exists ? bild.LastWriteTimeUtc.Ticks : 0);
    }

    // ---------------------------------------------------------------- zuletzt: Vorschau und Erreichbarkeit

    /// <summary>Für jeden Ordner: ist das Projekt da (projekt.json), gibt es ein Vorschaubild (und seine Version)? Mit Zeitlimit; was
    /// nicht rechtzeitig antwortet, bleibt «unbekannt» (vorhanden = null).</summary>
    public async Task<Dictionary<string, (bool? Vorhanden, bool Vorschau, long V)>> Pruefen(IEnumerable<string> ordner)
    {
        var ergebnis = new Dictionary<string, (bool?, bool, long)>(StringComparer.OrdinalIgnoreCase);
        var schloss = new object();
        var arbeit = Parallel.ForEachAsync(ordner.Distinct(StringComparer.OrdinalIgnoreCase), new ParallelOptions { MaxDegreeOfParallelism = 8 }, async (o, _) =>
        {
            var r = await Task.Run(() =>
            {
                try
                {
                    var vorhanden = File.Exists(Path.Combine(o, "projekt.json"));
                    var bild = new FileInfo(Path.Combine(o, VorschauDatei));
                    return ((bool?)vorhanden, bild.Exists, bild.Exists ? bild.LastWriteTimeUtc.Ticks : 0L);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { return ((bool?)false, false, 0L); }
            });
            lock (schloss) ergebnis[o] = r;
        });
        try { await arbeit.WaitAsync(KurzesLimit); } catch (TimeoutException) { /* der Rest bleibt unbekannt */ }
        lock (schloss) return new Dictionary<string, (bool?, bool, long)>(ergebnis, StringComparer.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------- Ordner-Browser

    static T MitLimit<T>(Func<T> arbeit, TimeSpan limit, T bei)
    {
        var t = Task.Run(arbeit);
        try { return t.WaitAsync(limit).GetAwaiter().GetResult(); }
        catch (TimeoutException) { return bei; }
    }

    public async Task<object> Ordner(string? pfad)
    {
        var (ablage, kicad) = Konfiguration();
        if (string.IsNullOrWhiteSpace(pfad))
        {
            var laufwerke = await Task.Run(() =>
            {
                var l = new List<object>();
                foreach (var d in DriveInfo.GetDrives())
                {
                    try
                    {
                        var art = d.DriveType switch { DriveType.Network => "Netzlaufwerk", DriveType.Removable => "Wechseldatenträger", DriveType.CDRom => "Laufwerk", _ => "Lokaler Datenträger" };
                        string? bez = null;
                        if (d.DriveType is DriveType.Fixed or DriveType.Removable && d.IsReady) bez = d.VolumeLabel;
                        l.Add(new { name = d.Name, pfad = d.Name, art, bezeichnung = string.IsNullOrWhiteSpace(bez) ? null : bez });
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
                }
                return l;
            }).WaitAsync(KurzesLimit);
            return new { pfad = "", eltern = (string?)null, ordner = Array.Empty<object>(), laufwerke, ablage, kicadOrdner = kicad, istProjekt = false };
        }

        string voll;
        try { voll = Path.GetFullPath(pfad.Trim()); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        { throw new HlksFehler($"Der Pfad «{pfad}» ist ungültig: {ex.Message}", "Einen vollständigen Ordnerpfad angeben, z. B. W:\\02_Projektdateien Objekte."); }
        var ohneSchluss = voll.Length > 3 ? voll.TrimEnd('\\', '/') : voll;

        var vorhanden = await Task.Run(() => Directory.Exists(ohneSchluss)).WaitAsync(E.StartZeitlimit).ContinueWith(t => t.IsCompletedSuccessfully && t.Result);
        if (!vorhanden)
            throw new HlksFehler($"Den Ordner {ohneSchluss} gibt es nicht oder er ist nicht erreichbar.", "Pfad prüfen; bei Netzlaufwerken Netzwerk/VPN prüfen. Mit «Höher» geht es zum übergeordneten Ordner.");

        var unterordner = new List<(string Name, string Pfad)>();
        var gekuerzt = false;
        string? lesefehler = null;
        try
        {
            foreach (var d in new DirectoryInfo(ohneSchluss).EnumerateDirectories())
            {
                if (d.Name.StartsWith('.') || d.Name.StartsWith('$')) continue;
                if ((d.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0) continue;
                unterordner.Add((d.Name, d.FullName));
                if (unterordner.Count >= 1500) { gekuerzt = true; break; }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { lesefehler = ex.Message; }
        unterordner.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));

        // KICAD-Projekte erkennen: nur lesen (File.Exists), parallel, mit Zeitlimit
        var marken = new (bool Projekt, bool Enthaelt)[unterordner.Count];
        var pruef = Parallel.ForEachAsync(Enumerable.Range(0, unterordner.Count), new ParallelOptions { MaxDegreeOfParallelism = 12 }, async (i, _) =>
        {
            marken[i] = await Task.Run(() =>
            {
                try
                {
                    var p = unterordner[i].Pfad;
                    return (File.Exists(Path.Combine(p, "projekt.json")), File.Exists(Path.Combine(p, kicad, "projekt.json")));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { return (false, false); }
            });
        });
        try { await pruef.WaitAsync(KurzesLimit); } catch (TimeoutException) { /* ohne Kennzeichen */ }

        var istProjekt = MitLimit(() => File.Exists(Path.Combine(ohneSchluss, "projekt.json")), KurzesLimit, false);
        var enthaelt = MitLimit(() => File.Exists(Path.Combine(ohneSchluss, kicad, "projekt.json")), KurzesLimit, false);
        var eltern = Path.GetDirectoryName(ohneSchluss);
        return new
        {
            pfad = ohneSchluss,
            eltern = eltern ?? "",     // "" = die Laufwerksliste
            ordner = unterordner.Select((u, i) => new { name = u.Name, pfad = u.Pfad, projekt = marken[i].Projekt, enthaeltProjekt = marken[i].Enthaelt }).ToList(),
            istProjekt,
            projektUnter = enthaelt ? Path.Combine(ohneSchluss, kicad) : null,
            ablage, kicadOrdner = kicad, gekuerzt, hinweis = lesefehler,
        };
    }

    /// <summary>Zeigt einen Ordner im Explorer (der Server läuft auf demselben Rechner wie das Fenster). Im Testlauf nicht.</summary>
    public object ImExplorerZeigen(string? pfad)
    {
        if (string.IsNullOrWhiteSpace(pfad)) throw new HlksFehler("Parameter «pfad» fehlt.", "Den Ordner angeben, der gezeigt werden soll.");
        var voll = Path.GetFullPath(pfad.Trim());
        if (!MitLimit(() => Directory.Exists(voll), KurzesLimit, false))
            throw new HlksFehler($"Den Ordner {voll} gibt es nicht oder er ist nicht erreichbar.", "Pfad und Netzwerk/VPN prüfen.");
        if (FamilienBibliothekDienst.Testlauf()) return new { gezeigt = false, testlauf = true, pfad = voll };
        if (!OperatingSystem.IsWindows()) return new { gezeigt = false, pfad = voll, hinweis = "Der Explorer gibt es nur unter Windows." };
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{voll}\"") { UseShellExecute = true });
        return new { gezeigt = true, pfad = voll };
    }

    // ---------------------------------------------------------------- Familien

    public async Task<object> FamilienZuletzt(int n)
    {
        n = Math.Clamp(n, 1, 100);
        BibliotheksOrt ort;
        try { ort = E.Familienbibliothek is { } fb ? FamilienBibliothekDienst.Ermitteln(dienst.Katalog.Ordner, fb, FamilienBibliothekDienst.Testlauf()) : FamilienBibliothekDienst.Ort(dienst.Katalog); }
        catch (HlksFehler f) { return new { erreichbar = false, hinweis = f.Message, familien = Array.Empty<object>() }; }
        if (!ort.Aktiv)
            return new { bibliothek = new { pfad = ort.Pfad, quelle = ort.Quelle }, erreichbar = false, hinweis = ort.Sperre ?? "Keine Familienbibliothek eingestellt.", familien = Array.Empty<object>() };

        var root = ort.Pfad!;
        var erreichbar = await Task.Run(() => FamilienBibliothekDienst.Erreichbar(root, false)).WaitAsync(E.StartZeitlimit).ContinueWith(t => t.IsCompletedSuccessfully && t.Result);
        BibIndex? idx = null;
        try { idx = await Task.Run(() => FamilienBibliothekDienst.IndexOhneSchreiben(ort)).WaitAsync(E.StartZeitlimit); }
        catch (TimeoutException) { /* unten: Hinweis */ }
        string? hinweis = null;
        if (idx is null) hinweis = $"Die Familienbibliothek {root} ist nicht erreichbar und es gibt keinen gemerkten Stand. Netzwerk/VPN prüfen.";
        else if (!erreichbar) hinweis = $"Die Familienbibliothek {root} ist nicht erreichbar – Stand vom {idx.Stand ?? "unbekannt"} (zuletzt gemerkt).";

        static DateTimeOffset Zeit(BibIndexEintrag e) =>
            DateTimeOffset.TryParse(e.Zeit, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var z) ? z : DateTimeOffset.MinValue;

        var oben = (idx?.Familien ?? []).OrderByDescending(Zeit).ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).Take(n).ToList();
        var logs = new BibLog?[oben.Count];
        if (erreichbar && oben.Count > 0)
        {
            var arbeit = Parallel.ForEachAsync(Enumerable.Range(0, oben.Count), new ParallelOptions { MaxDegreeOfParallelism = 6 },
                async (i, _) => logs[i] = await Task.Run(() => FamilienBibliothekDienst.Log(ort, oben[i])));
            try { await arbeit.WaitAsync(KurzesLimit); } catch (TimeoutException) { /* ohne Ordner des Projekts */ }
        }
        var familien = oben.Select((e, i) =>
        {
            var lg = logs[i];
            var eintrag = lg?.Eintraege.Where(x => x.Art == "version" && x.Version == e.Version).LastOrDefault()
                ?? lg?.Eintraege.Where(x => x.Art is "version" or "abzweig").LastOrDefault();
            var z = Zeit(e);
            return new
            {
                id = e.Id, name = e.Name, kategorie = e.Kategorie, hersteller = e.Hersteller, produkt = e.Produkt, status = e.Status, version = e.Version,
                projekt = e.Projekt ?? eintrag?.Projekt?.Name, projektNummer = eintrag?.Projekt?.Nummer, projektOrdner = eintrag?.Projekt?.Ordner,
                akteur = e.Akteur, zeit = e.Zeit, zeitIso = z == DateTimeOffset.MinValue ? null : z.ToString("o"),
            };
        }).ToList();
        return new { bibliothek = new { pfad = root, quelle = ort.Quelle }, erreichbar, offline = !erreichbar, hinweis, anzahl = idx?.Familien.Count ?? 0, familien };
    }

    // ---------------------------------------------------------------- Vorschaubild

    /// <summary>Prüft ein PNG (Signatur, Grösse) und liefert Breite und Höhe.</summary>
    public static (int Breite, int Hoehe) PngPruefen(byte[] png)
    {
        if (png.Length < 33 || png[0] != 0x89 || png[1] != 0x50 || png[2] != 0x4E || png[3] != 0x47 || png[4] != 0x0D || png[5] != 0x0A || png[6] != 0x1A || png[7] != 0x0A)
            throw new HlksFehler("Das Vorschaubild ist keine PNG-Datei.", "Das Bild als PNG (Content-Type image/png) senden.");
        if (png.Length > VorschauMaxBytes)
            throw new HlksFehler($"Das Vorschaubild ist zu gross ({png.Length / 1024} KB, erlaubt {VorschauMaxBytes / 1024} KB).", "Das Bild kleiner rendern (z. B. 640 × 400).");
        int b = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19], h = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
        if (b < 16 || h < 16 || b > 4000 || h > 4000)
            throw new HlksFehler($"Das Vorschaubild hat eine unbrauchbare Grösse ({b} × {h}).", "Zwischen 16 und 4000 Pixel je Seite, üblich 640 × 400.");
        return (b, h);
    }

    /// <summary>Speichert vorschau.png und vorschau.json im Ordner des offenen Projekts. ort (Ablageort, den der Browser kennt)
    /// muss zum offenen Projekt passen, sonst gehörte das Bild zu einem anderen Projekt.</summary>
    public async Task<object> VorschauSpeichern(byte[] png, string? stand, string? ort)
    {
        var (breite, hoehe) = PngPruefen(png);
        var (ordner, name, nummer) = await dienst.Mit(p => (p.Speicher?.Ort, p.Kopf.Name,
            FamilienBibliothekSchreiben.Projektinformation(p.Modell)?.Text("projektnummer")));
        if (string.IsNullOrWhiteSpace(ordner))
            throw new HlksFehler("Das offene Projekt hat keinen Ablageordner.", "Ein Projekt aus einem Ordner öffnen.");
        if (ort is not null && !string.Equals(ort.TrimEnd('\\', '/'), ordner.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
            throw new HlksFehler("Das offene Projekt hat inzwischen gewechselt; das Vorschaubild gehört zu einem anderen Projekt.", "Das Bild des jetzt offenen Projekts neu erzeugen.");
        if (FamilienBibliothekDienst.Testlauf() && !FamilienBibliothekDienst.UnterTemp(ordner))
            throw new HlksFehler($"Testlauf: das Projekt {ordner} liegt nicht unter %TEMP% – es wird kein Vorschaubild geschrieben.", "Im Test ein Projekt unter %TEMP% verwenden.");
        if (!MitLimit(() => Directory.Exists(ordner), KurzesLimit, false))
            throw new HlksFehler($"Der Projektordner {ordner} ist nicht erreichbar.", "Netzwerk/VPN prüfen.");

        await Task.Run(() =>
        {
            Atomar(Path.Combine(ordner, VorschauDatei), png);
            var info = new JsonObject
            {
                ["name"] = name, ["nummer"] = string.IsNullOrWhiteSpace(nummer) ? null : nummer, ["stand"] = stand,
                ["zeit"] = DateTimeOffset.UtcNow.ToString("o"), ["breite"] = breite, ["hoehe"] = hoehe,
            };
            Atomar(Path.Combine(ordner, VorschauInfo), System.Text.Encoding.UTF8.GetBytes(info.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping })));
        }).WaitAsync(E.StartZeitlimit);
        lock (sperre) { letzteGueltigBis = DateTimeOffset.MinValue; }   // die nächste Suche zeigt das neue Bild
        return new { gespeichert = true, datei = Path.Combine(ordner, VorschauDatei), bytes = png.Length, breite, hoehe };
    }

    static void Atomar(string ziel, byte[] daten)
    {
        var temp = Path.Combine(Path.GetDirectoryName(ziel)!, $".{Path.GetFileName(ziel)}.{Guid.NewGuid():N}.tmp");
        try { File.WriteAllBytes(temp, daten); File.Move(temp, ziel, true); }
        finally { try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }

    /// <summary>Pfad des Vorschaubilds eines Projektordners (nur wenn der Ordner ein KICAD-Projekt ist); sonst null.</summary>
    public string? VorschauPfad(string? ordner)
    {
        if (string.IsNullOrWhiteSpace(ordner)) return null;
        try
        {
            var o = Path.GetFullPath(ordner.Trim());
            return MitLimit(() => File.Exists(Path.Combine(o, "projekt.json")) && File.Exists(Path.Combine(o, VorschauDatei)) ? Path.Combine(o, VorschauDatei) : null, KurzesLimit, null);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return null; }
    }

    // ---------------------------------------------------------------- Programmstand

    public object Info()
    {
        var asm = typeof(StartDienst).Assembly;
        DateTime? stand = null;
        try { stand = File.GetLastWriteTime(asm.Location); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        var version = asm.GetName().Version?.ToString(3) ?? "0.1.0";
        var (ablage, kicad) = Konfiguration();
        var ort = dienst.Anleitung.Ort();
        return new
        {
            // Bedienungsanleitung: KICAD liefert sie selbst unter /hilfe/ aus; «vorhanden» = auf diesem Rechner gebaut (index.html da)
            anleitung = new { url = AnleitungDienst.Url, vorhanden = dienst.Anleitung.Vorhanden(ort), hinweis = ort.Hinweis, ordner = ort.Ordner },
            programm = "KICAD", name = "HLKS-CAD", version, stand = stand?.ToString("yyyy-MM-dd HH:mm"),
            rechner = E.Geraet, akteur = E.Akteur, zuletztQuelle = dienst.Zuletzt.Quelle, zuletztOrt = dienst.Zuletzt.Verzeichnis,
            projektablage = ablage, kicadOrdner = kicad,
        };
    }
}
