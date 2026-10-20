// Befund-Ablage und Auflösen von Unterlagen (Modul 2, Auftrag B2).
// Befunde sind abgeleitet (jederzeit neu lesbar) und liegen deshalb nicht im Modell, sondern als Datei
//   <Projektordner>/unterlagen/befunde/<prüfsumme>.<leser>-<version>.json
// Wiederholtes Lesen kommt aus der Ablage; eine neue Leserversion liest neu (die alte Datei bleibt liegen). Geschrieben wird
// über eine Temp-Datei und ohne Überschreiben: zwei Arbeitsplätze, die gleichzeitig lesen, erzeugen denselben Inhalt.
// Im Speicher bleiben die letzten Ergebnisse, damit seitenweises Abfragen nicht jedes Mal die Datei neu liest.

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hlks.Kern;

/// <summary>Eine Unterlage im Ordner unterlagen: Datei, Art, Prüfsumme, Pfad; Kennzeichen und Id, wenn sie als Element eingefügt ist.</summary>
public sealed record UnterlageRef(string Datei, string Original, string Art, string Pruefsumme, string Pfad, string? Kennzeichen, string? Id)
{
    /// <summary>Kurzer Name für Meldungen: Kennzeichen, sonst Dateiname.</summary>
    public string Anzeige => Kennzeichen ?? Original;
}

/// <summary>Ergebnis von <see cref="UnterlagenService.Lesen"/>.</summary>
public sealed record LeseErgebnis(UnterlagenBefunde Befunde, IUnterlagenLeser Leser, bool AusAblage, double Sekunden, string AblageDatei);

/// <summary>Dateiarten der Unterlagen (Katalogklasse Unterlage, Eigenschaft art).</summary>
public static class UnterlagenArten
{
    /// <summary>Arten für unterlage_einfuegen (auch Modul 1b: ifc nur über ifc_uebernehmen).</summary>
    public static readonly string[] Alle = ["ifc", "dwg", "dxf", "pdf", "bild", "step", "iges", "stl", "obj", "xlsx", "csv", "typkatalog", "html"];
    /// <summary>Arten, die zu einer Familie (ohne Geschoss und Lage) eingefügt werden können.</summary>
    public static readonly string[] FuerFamilie = ["pdf", "dwg", "dxf", "bild", "step", "iges", "stl", "obj", "xlsx", "csv", "typkatalog", "ifc", "html"];
    /// <summary>Arten, die nur als Referenz gelten (kein Leser): TIFF, RFA und Unbekanntes (Annahme 8, Prüfliste).</summary>
    public static string? AusEndung(string datei, string? pfadFuerInhalt = null)
    {
        var ext = Path.GetExtension(datei).ToLowerInvariant();
        switch (ext)
        {
            case ".pdf": return "pdf";
            case ".dwg": return "dwg";
            case ".dxf": return "dxf";
            case ".png" or ".jpg" or ".jpeg": return "bild";
            case ".ifc": return "ifc";
            case ".html" or ".htm": return "html";
            case ".step" or ".stp": return "step";
            case ".iges" or ".igs": return "iges";
            case ".stl": return "stl";
            case ".obj": return "obj";
            case ".xlsx" or ".xlsm": return "xlsx";
            case ".csv": return "csv";
            case ".txt":
                return pfadFuerInhalt is not null && IstTypkatalog(pfadFuerInhalt) ? "typkatalog" : null;
            default: return null;
        }
    }

    /// <summary>Revit-Typkatalog: erste Zeile mit Kopfspalten der Form «Name##Art##Einheit», durch Komma getrennt.</summary>
    public static bool IstTypkatalog(string pfad)
    {
        try
        {
            using var r = new StreamReader(pfad, detectEncodingFromByteOrderMarks: true);
            for (int i = 0; i < 4 && r.ReadLine() is { } z; i++)
                if (z.Contains("##") && z.Contains(',')) return true;
        }
        catch (IOException) { }
        return false;
    }
}
public static class UnterlagenService
{
    static readonly JsonSerializerOptions Optionen = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new BefundeKonverter() },
    };

    /// <summary>Befunde aus einer Datei der Ablage (null, wenn leer).</summary>
    public static UnterlagenBefunde? Laden(string ablageDatei)
    {
        var bytes = File.ReadAllBytes(ablageDatei);
        if (bytes.Length == 0) return null;
        var r = new Utf8JsonReader(bytes);
        r.Read();
        return new BefundeKonverter().Read(ref r, typeof(UnterlagenBefunde), Optionen);
    }

    // ---------- Auflösen ----------

    /// <summary>Unterlage über Kennzeichen oder Id eines Unterlage-Elements, über den Dateinamen im Ordner unterlagen (mit oder
    /// ohne Prüfsummen-Präfix) oder über einen Dateipfad (dann wird die Datei abgelegt wie bei unterlage_einfuegen).</summary>
    public static UnterlageRef Aufloesen(Projekt p, string? bezug)
    {
        if (string.IsNullOrWhiteSpace(bezug)) throw new HlksFehler("Es fehlt die Unterlage.", Hilfe(p));
        bezug = bezug.Trim();
        var e = p.Modell.Finde(bezug);
        if (e is not null)
        {
            if (e.Klasse != "Unterlage") throw new HlksFehler($"{e.Kennzeichen} ist keine Unterlage, sondern {e.Klasse}.", Hilfe(p));
            var datei = e.Text("datei") ?? throw new HlksFehler($"Unterlage {e.Kennzeichen} hat keine Datei.");
            var pfad = Unterlagen.Pfad(p, datei);
            var art = e.Text("art") ?? UnterlagenArten.AusEndung(datei, pfad)
                ?? throw new HlksFehler($"Die Art von {e.Kennzeichen} ist nicht bekannt.");
            return new UnterlageRef(datei, Unterlagen.OriginalName(datei), art, Pruefsumme(datei, pfad), pfad, e.Kennzeichen, e.Id);
        }
        var ordner = Unterlagen.Ordner(p);
        string? gefunden = null;
        var direkt = Path.Combine(ordner, Path.GetFileName(bezug));
        if (File.Exists(direkt)) gefunden = direkt;
        else if (Directory.Exists(ordner))
        {
            gefunden = Directory.EnumerateFiles(ordner)
                .Where(f => !Path.GetFileName(f).StartsWith('.') && string.Equals(Unterlagen.OriginalName(f), Path.GetFileName(bezug), StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        }
        if (gefunden is null && DateiPfad(p, bezug) is { } quelle)
        {
            var (name, _) = Unterlagen.Ablegen(p, quelle);
            gefunden = Path.Combine(ordner, name);
        }
        if (gefunden is null) throw new HlksFehler($"Unterlage «{bezug}» gibt es nicht.", Hilfe(p));
        var dateiName = Path.GetFileName(gefunden);
        var a = UnterlagenArten.AusEndung(dateiName, gefunden);
        if (a is null)
            throw new HlksFehler($"Die Art der Datei «{dateiName}» ist nicht bekannt.",
                "Gelesen werden: pdf, dwg, dxf, png/jpg, ifc, step/stp, iges/igs, stl, obj, xlsx, csv und Revit-Typkatalog (.txt mit Kopf wie Laenge##LENGTH##MILLIMETERS). TIFF und RFA werden nicht gelesen.");
        // Unterlage-Element der Datei (falls eingefügt), damit Kennzeichen erscheinen
        var el = p.Modell.DerKlasse("Unterlage").FirstOrDefault(u => string.Equals(u.Text("datei"), dateiName, StringComparison.OrdinalIgnoreCase));
        return new UnterlageRef(dateiName, Unterlagen.OriginalName(dateiName), a, Pruefsumme(dateiName, gefunden), gefunden, el?.Kennzeichen, el?.Id);
    }

    /// <summary>Unterlage zum Lesen ihres Inhalts (unterlagen_lesen, befunde, unterlage_bild, familie_vergleichen): wie <see cref="Aufloesen"/>,
    /// dazu die Lesegrenze eines Laufs mit Internet (Sicherheitsprüfung 3c, B3b; KiUnterlagen).</summary>
    public static UnterlageRef AufloesenZumLesen(Projekt p, string? bezug, string werkzeug)
    {
        var u = Aufloesen(p, bezug);
        KiUnterlagen.Pruefen(p, u, werkzeug);
        return u;
    }

    /// <summary>Dateipfad als Bezug (die Datei wird dann abgelegt), sonst null. Für die KI (Sicherheitsprüfung 3c, B3a/B4) nur Pfade unter dem
    /// Projektordner und den Wurzeln aus kataloge/buero.json – geprüft vor jedem Dateizugriff; ein blosser Name meint nur den Ordner unterlagen
    /// (oben schon gesucht); mit Internet (Lesegrenze) gar keine Pfade.</summary>
    static string? DateiPfad(Projekt p, string bezug)
    {
        if (!KiPfade.IstKi(p)) return File.Exists(bezug) ? bezug : null;
        if (KiPfade.IstName(bezug)) return null;
        if (KiUnterlagen.Grenze(p) is not null)
            throw new HlksFehler($"Mit Internet nimmt die KI keine Dateipfade («{bezug}») – nur Unterlagen des Projekts.",
                "Kennzeichen oder Dateinamen einer Unterlage angeben; andere Dateien legt ein Mensch über «Unterlagen» ins Projekt.");
        var voll = KiPfade.Pruefen(p, bezug, "Unterlage");
        return File.Exists(voll) ? voll : null;
    }

    static string Hilfe(Projekt p)
    {
        var liste = p.Modell.DerKlasse("Unterlage").OrderBy(u => u.Kennzeichen, StringComparer.OrdinalIgnoreCase)
            .Take(12).Select(u => $"{u.Kennzeichen} ({Unterlagen.OriginalName(u.Text("datei") ?? "")})").ToList();
        return liste.Count > 0 ? "Kennzeichen, Id oder Dateiname einer Unterlage angeben, z. B. " + string.Join(", ", liste) + "."
                               : "Kennzeichen, Id oder Dateiname einer Unterlage angeben; Dateien zuerst mit unterlage_einfuegen (zweck «familie») oder per Upload ablegen.";
    }

    static string Pruefsumme(string datei, string pfad)
    {
        if (datei.Length > 13 && datei[12] == '_' && datei[..12].All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f')) return datei[..12];
        using var s = File.OpenRead(pfad);
        return Convert.ToHexString(SHA256.HashData(s))[..12].ToLowerInvariant();
    }

    // ---------- Ablage ----------

    public static string BefundOrdner(Projekt p) => Path.Combine(Unterlagen.Ordner(p), "befunde");

    public static string AblagePfad(Projekt p, string pruefsumme, IUnterlagenLeser leser) =>
        Path.Combine(BefundOrdner(p), $"{pruefsumme}.{leser.Name}-{leser.Version}.json");

    sealed record Eintrag(string Pfad, long Laenge, long Zeit, UnterlagenBefunde Befunde);
    static readonly List<Eintrag> speicher = [];
    static readonly object sperre = new();
    const int MaxSpeicher = 3;

    /// <summary>Liest die Befunde einer Unterlage: aus der Ablage, sonst mit dem Leser der Art (und legt sie dann ab).</summary>
    public static LeseErgebnis Lesen(Projekt p, UnterlageRef u)
    {
        var leser = UnterlagenLeser.Fuer(u.Art)
            ?? throw new HlksFehler($"Für Dateien der Art «{u.Art}» gibt es keinen Leser.",
                "Lesbar: pdf, dwg, dxf, bild (png, jpg), ifc, stl, obj, xlsx, csv, typkatalog; step und iges mit dem STEP-Leser, wenn er eingebaut ist. Die Datei bleibt als Referenz abgelegt.");
        var sw = Stopwatch.StartNew();
        var ablage = AblagePfad(p, u.Pruefsumme, leser);
        var cached = Aus(ablage);
        if (cached is not null) return new LeseErgebnis(cached, leser, true, sw.Elapsed.TotalSeconds, ablage);

        if (File.Exists(ablage))
        {
            try
            {

                var b = Laden(ablage);
                if (b is not null)
                {
                    Merken(ablage, b!);
                    return new LeseErgebnis(b!, leser, true, sw.Elapsed.TotalSeconds, ablage);
                }
            }
            catch (Exception ex) when (ex is JsonException or IOException) { /* beschädigt: neu lesen */ }
        }

        UnterlagenBefunde erg;
        try { erg = leser.Lesen(u.Pfad, u.Original, u.Pruefsumme, u.Art); }
        catch (Exception ex) when (ex is not HlksFehler and not OutOfMemoryException)
        {
            throw new HlksFehler($"Die Datei «{u.Original}» lässt sich nicht lesen ({leser.Name} {leser.Version}): {ex.Message}",
                "Datei prüfen oder in einem anderen Format ablegen (z. B. PDF statt Bild-Scan, DXF statt DWG).");
        }
        erg.Datei = u.Original;
        erg.Pruefsumme = u.Pruefsumme;
        erg.Art = u.Art;
        Schreiben(p, ablage, erg);
        // Ergebnis aus der Ablage zurücklesen: so verhält sich das erste Lesen wie jedes spätere (gleiche Knotentypen)
        if (File.Exists(ablage)) { try { erg = Laden(ablage) ?? erg; } catch (Exception ex) when (ex is JsonException or IOException) { } }
        Merken(ablage, erg);
        return new LeseErgebnis(erg, leser, false, sw.Elapsed.TotalSeconds, ablage);
    }

    static UnterlagenBefunde? Aus(string pfad)
    {
        if (!File.Exists(pfad)) return null;
        var fi = new FileInfo(pfad);
        lock (sperre)
        {
            var e = speicher.FirstOrDefault(x => x.Pfad == pfad && x.Laenge == fi.Length && x.Zeit == fi.LastWriteTimeUtc.Ticks);
            if (e is null) return null;
            speicher.Remove(e); speicher.Insert(0, e);
            return e.Befunde;
        }
    }

    static void Merken(string pfad, UnterlagenBefunde b)
    {
        if (!File.Exists(pfad)) return;
        var fi = new FileInfo(pfad);
        lock (sperre)
        {
            speicher.RemoveAll(x => x.Pfad == pfad);
            speicher.Insert(0, new Eintrag(pfad, fi.Length, fi.LastWriteTimeUtc.Ticks, b));
            while (speicher.Count > MaxSpeicher) speicher.RemoveAt(speicher.Count - 1);
        }
    }

    static void Schreiben(Projekt p, string ablage, UnterlagenBefunde b)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ablage)!);
            var temp = Path.Combine(Path.GetDirectoryName(ablage)!, $".{Path.GetFileName(ablage)}.{Guid.NewGuid():N}.tmp");
            try
            {
                using (var fs = File.Create(temp)) JsonSerializer.Serialize(fs, b, Optionen);
                try { File.Move(temp, ablage, overwrite: false); }
                catch (IOException) when (File.Exists(ablage)) { }   // gleichzeitig von einem anderen Gerät abgelegt
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            b.Hinweise.Add($"Die Befunde konnten nicht abgelegt werden ({ex.Message}); sie werden bei der nächsten Abfrage neu gelesen.");
        }
    }
}
