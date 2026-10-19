// Bedienungsanleitung in KICAD selbst (Entscheid Christian Steiner 19.10.2026, Fragefenster): «In KICAD selbst – KICAD liefert die Seite
// selbst aus, ohne Anmeldung und ohne Internet; Häkchen und Notizen speichert KICAD je Benutzer über OneDrive; Bilder bleiben lokal auf
// diesem Rechner.» Zwei Teile:
//
//   Auslieferung   GET /hilfe/…  liefert index.html und bilder/* aus dem Anleitungsordner (werkzeuge/anleitung_bauen.mjs erzeugt ihn).
//                  Ordner: Einstellungen.AnleitungOrdner › HLKS_ANLEITUNG › kataloge/buero.json › anleitung.ordner (%…%-Variablen werden
//                  aufgelöst). Der Ordner liegt lokal (nie OneDrive: die Bilder zeigen echte Projekte).
//                  Schutz: nur Dateien INNERHALB des Ordners (kein «..», keine absoluten Pfade, Laufwerke, Datenströme, keine Junctions/
//                  Symlinks), nur bekannte Dateitypen, nur GET. Im Testlauf (HLKS_TEST oder Testrunner) wird ein Ordner ausserhalb %TEMP% nicht gelesen.
//   Prüfstand      GET /api/hilfe/pruefung · PUT /api/hilfe/pruefung/{id}: die Häkchen (✓/✗) und Notizen der Anleitungsseite, je Funktion ein
//                  Eintrag { titel, punkte, notiz, stand, geaendert, texte }. Ablage wie die Benutzereinstellungen (RechnerAblage):
//                  %OneDrive%\HLKS-CAD\anleitung\<Rechner>.json – JE RECHNER EINE DATEI, ein Rechner schreibt nur seine eigene; gelesen werden alle,
//                  je Funktion gewinnt der jüngste «geaendert» (bei Gleichstand der grössere Rechnername). Ohne OneDrive: %APPDATA%\HLKS-CAD\anleitung.json.
//                  Umleitung: Einstellungen.AnleitungPruefung bzw. HLKS_ANLEITUNG_PRUEFUNG (Ordner mit den Rechnerdateien). Im Testlauf ohne
//                  Umleitung nach %TEMP% wird weder gelesen noch geschrieben (gleiche Regel wie BenutzerEinstellungen).
//                  «geaendert» setzt der Server (seine Uhr), nicht der Browser: so zählt auf einem Rechner nur eine Uhr.

using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Hlks.Kern;

namespace Hlks.Web;

/// <summary>Wo die Anleitung liegt: Ordner (null = nicht eingestellt oder gesperrt), woher der Wert kommt, ggf. Grund der Sperre.</summary>
public sealed record AnleitungOrt(string? Ordner, string Quelle, string? Sperre, string? Hinweis);

/// <summary>Ergebnis der Pfadprüfung: Datei (null = abgelehnt oder nicht da) und Grund; Abgelehnt = verbotener Pfad (400), sonst Fehlen (404).</summary>
public readonly record struct AnleitungDatei(string? Pfad, bool Abgelehnt, string? Grund);

public sealed class AnleitungDienst
{
    /// <summary>Adresse der Anleitung auf dem Server der Oberfläche.</summary>
    public const string Url = "/hilfe/";

    /// <summary>Dateitypen, die ausgeliefert werden (alles andere, z. B. stand.txt, bleibt unsichtbar).</summary>
    public static readonly IReadOnlyDictionary<string, string> Typen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html; charset=utf-8",
        [".webp"] = "image/webp",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".svg"] = "image/svg+xml",
        [".css"] = "text/css; charset=utf-8",
        [".js"] = "text/javascript; charset=utf-8",
        [".json"] = "application/json; charset=utf-8",
    };

    readonly Einstellungen e;
    public Pruefstand Pruefstand { get; }

    public AnleitungDienst(Einstellungen e) { this.e = e; Pruefstand = Pruefstand.Aus(e); }

    // ---------------------------------------------------------------- Ordner

    static string? Text(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s) ? s.Trim() : null;

    public AnleitungOrt Ort()
    {
        string? roh = null, hinweis = null, quelle = "nicht eingestellt";
        try
        {
            var datei = Path.Combine(e.KatalogOrdner, "buero.json");
            if (File.Exists(datei) && JsonNode.Parse(File.ReadAllText(datei))?["anleitung"] is JsonObject o)
            {
                hinweis = Text(o["hinweis"]);
                roh = Text(o["ordner"]);
                if (roh is not null) quelle = "kataloge/buero.json";
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or HlksFehler) { /* ohne buero.json: nur Umgebung */ }
        var umgebung = e.AnleitungOrdner ?? Environment.GetEnvironmentVariable("HLKS_ANLEITUNG");
        if (!string.IsNullOrWhiteSpace(umgebung)) { roh = umgebung.Trim(); quelle = "Umleitung"; }
        if (roh is null) return new AnleitungOrt(null, quelle, null, hinweis);

        string voll;
        try { voll = Path.GetFullPath(Environment.ExpandEnvironmentVariables(roh)); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        { return new AnleitungOrt(null, quelle, $"Der Anleitungsordner «{roh}» ist kein gültiger Pfad.", hinweis); }
        if (voll.Contains('%'))
            return new AnleitungOrt(null, quelle, $"Im Anleitungsordner «{roh}» ist eine Umgebungsvariable nicht gesetzt.", hinweis);
        try
        {
            if (FamilienBibliothekDienst.Testlauf() && !FamilienBibliothekDienst.UnterTemp(voll))
                return new AnleitungOrt(null, quelle, $"Testlauf: der Anleitungsordner {voll} liegt nicht unter %TEMP% und wird nicht gelesen (HLKS_ANLEITUNG setzen).", hinweis);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return new AnleitungOrt(null, quelle, "Ungültiger Pfad.", hinweis); }
        return new AnleitungOrt(Path.TrimEndingDirectorySeparator(voll), quelle, null, hinweis);
    }

    /// <summary>Gibt es eine gebaute Anleitung (index.html im Ordner)?</summary>
    public bool Vorhanden(AnleitungOrt? ort = null)
    {
        ort ??= Ort();
        if (ort.Ordner is null) return false;
        try { return File.Exists(Path.Combine(ort.Ordner, "index.html")); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    // ---------------------------------------------------------------- Auslieferung

    /// <summary>Löst einen relativen Pfad (aus der Adresse) gegen den Anleitungsordner auf. Abgelehnt werden: absolute Pfade, «..»/«.», Laufwerke
    /// und Datenströme (Doppelpunkt), ungültige Zeichen, leere Teile mitten im Pfad und alles, was über eine Junction oder einen Symlink aus dem
    /// Ordner herausführen könnte (Abgelehnt = true, 400). Ordneradressen, unbekannte Dateitypen und fehlende Dateien sind harmlos: Pfad null und
    /// Abgelehnt false (404).</summary>
    public static AnleitungDatei Aufloesen(string ordner, string? relativ)
    {
        static AnleitungDatei Nein(string grund) => new(null, true, grund);
        if (string.IsNullOrEmpty(relativ)) return Nein("Es wurde keine Datei genannt.");
        if (relativ.Length > 200) return Nein("Der Pfad ist zu lang.");
        if (relativ.Any(char.IsControl)) return Nein("Der Pfad enthält Steuerzeichen.");
        var einheitlich = relativ.Replace('\\', '/');
        if (einheitlich.StartsWith('/')) return Nein("Absolute Pfade sind nicht erlaubt.");
        var ordnerAdresse = einheitlich.EndsWith('/');   // «bilder/»: eine Ordneradresse ist harmlos, aber keine Datei (404)
        var teile = (ordnerAdresse ? einheitlich[..^1] : einheitlich).Split('/');
        var ungueltig = Path.GetInvalidFileNameChars();
        foreach (var t in teile)
        {
            if (t.Length == 0) return Nein("Leere Pfadteile sind nicht erlaubt.");
            if (t is "." or "..") return Nein("«.» und «..» sind nicht erlaubt.");
            if (t.Contains(':')) return Nein("Laufwerke und Datenströme sind nicht erlaubt.");
            if (t.IndexOfAny(ungueltig) >= 0) return Nein("Der Pfad enthält ungültige Zeichen.");
            if (t.EndsWith('.') || t.EndsWith(' ')) return Nein("Pfadteile dürfen nicht mit Punkt oder Leerzeichen enden.");
        }
        if (ordnerAdresse) return new AnleitungDatei(null, false, "Das ist ein Ordner, keine Datei.");
        if (!Typen.ContainsKey(Path.GetExtension(teile[^1]))) return new AnleitungDatei(null, false, "Dieser Dateityp wird nicht ausgeliefert.");

        string wurzel, voll;
        try
        {
            wurzel = Path.TrimEndingDirectorySeparator(Path.GetFullPath(ordner));
            voll = Path.GetFullPath(Path.Combine(wurzel, string.Join(Path.DirectorySeparatorChar, teile)));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return Nein("Ungültiger Pfad."); }
        if (!voll.StartsWith(wurzel + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return Nein("Der Pfad führt aus dem Anleitungsordner heraus.");

        // Junctions und Symlinks innerhalb des Ordners führen eventuell hinaus: jedes vorhandene Teilstück prüfen
        var aktuell = wurzel;
        foreach (var t in teile)
        {
            aktuell = Path.Combine(aktuell, t);
            try
            {
                var a = File.GetAttributes(aktuell);
                if ((a & FileAttributes.ReparsePoint) != 0) return Nein("Verknüpfungen (Junctions, Symlinks) werden nicht gefolgt.");
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { return new AnleitungDatei(null, false, "Die Datei gibt es nicht."); }
        }
        return File.Exists(voll) ? new AnleitungDatei(voll, false, null) : new AnleitungDatei(null, false, "Die Datei gibt es nicht.");
    }

    /// <summary>Kleine Seite, wenn die Anleitung auf diesem Rechner noch nicht gebaut ist: sagt es und nennt den Befehl zum Erzeugen.</summary>
    public string NichtErstelltSeite(AnleitungOrt ort)
    {
        var profil = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var kopie = Path.Combine(profil, "HLKS_CAD_desktop");
        var befehl = $"node \"{Path.Combine(kopie, "werkzeuge", "anleitung_bauen.mjs")}\" --repo \"{kopie}\"";
        var wo = ort.Ordner ?? "(nicht eingestellt: kataloge/buero.json › anleitung.ordner)";
        string H(string s) => WebUtility.HtmlEncode(s);
        return $$"""
            <!doctype html>
            <html lang="de"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
            <title>KICAD Bedienungsanleitung</title>
            <style>
              body { font: 16px/1.5 "Segoe UI", system-ui, sans-serif; margin: 0; padding: 48px 24px; background: #f4f6f9; color: #1b2330; }
              main { max-width: 720px; margin: 0 auto; background: #fff; border: 1px solid #d6dce5; border-radius: 10px; padding: 28px 32px; }
              h1 { font-size: 1.4rem; margin: 0 0 12px; }
              code, pre { font: .9rem Consolas, monospace; background: #eef2f7; border-radius: 4px; }
              code { padding: 1px 5px; } pre { padding: 10px 12px; overflow-x: auto; }
              @media (prefers-color-scheme: dark) { body { background: #11151b; color: #e4e9f0; } main { background: #19202a; border-color: #2e3846; } code, pre { background: #212a36; } }
            </style></head><body><main>
            <h1>Die Bedienungsanleitung ist auf diesem Rechner noch nicht erstellt</h1>
            <p>KICAD liefert die Anleitung selbst aus, sobald sie hier gebaut ist. Sie liegt lokal im Ordner <code>{{H(wo)}}</code>.</p>
            <p>Erzeugen (PowerShell oder Eingabeaufforderung):</p>
            <pre>{{H(befehl)}}</pre>
            <p>Oder KICAD über das Desktop-Symbol neu starten: der Start baut die Anleitung mit.</p>
            </main></body></html>
            """;
    }
}

/// <summary>Häkchen und Notizen der Anleitungsseite, je Benutzer über OneDrive (siehe Kopf dieser Datei).</summary>
public sealed class Pruefstand
{
    public const int IdMax = 120, PunkteMax = 64, NotizMax = 4000, KoerperMax = 64 * 1024;
    public const int TitelMax = 300, StandMax = 64, TextMax = 1000, EintraegeMax = 3000;
    static readonly Regex IdMuster = new(@"^[A-Za-z0-9_.~:@+\-]+$", RegexOptions.CultureInvariant);
    static readonly Regex PunktMuster = new(@"^(0|[1-9][0-9]?)$", RegexOptions.CultureInvariant);

    readonly RechnerAblage ablage;
    readonly object sperre = new();

    public string Verzeichnis => ablage.Verzeichnis;
    public string Rechner => ablage.Rechner;
    /// <summary>OneDrive | Umleitung | lokal – woher der Prüfstand kommt.</summary>
    public string Quelle => ablage.Quelle;
    /// <summary>Uhr für «geaendert» (Tests setzen eine feste).</summary>
    public Func<DateTimeOffset> Uhr { get; set; } = () => DateTimeOffset.UtcNow;

    public Pruefstand(RechnerAblage ablage) { this.ablage = ablage; }

    public static Pruefstand Aus(Einstellungen e) =>
        new(RechnerAblage.Waehlen("anleitung", "anleitung.json", e.AnleitungPruefung, "HLKS_ANLEITUNG_PRUEFUNG", e.Geraet));

    /// <summary>Im Testlauf nur unter %TEMP% (wie BenutzerEinstellungen).</summary>
    public bool Erlaubt => ablage.SchreibenErlaubt;

    // ---------------------------------------------------------------- Prüfen (rein)

    static void PruefeId(string? id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > IdMax || !IdMuster.IsMatch(id))
            throw new HlksFehler($"Die Funktionskennung «{id}» ist ungültig.", $"Erlaubt sind bis {IdMax} Zeichen aus A–Z, a–z, 0–9 und _ . ~ : @ + -.");
    }

    static string? Zeichenkette(JsonObject o, string name, int max, string was)
    {
        if (!o.TryGetPropertyValue(name, out var n) || n is null) return null;
        if (n is not JsonValue v || !v.TryGetValue<string>(out var s)) throw new HlksFehler($"«{name}» muss ein Text sein.", $"Für {was} einen Text senden.");
        if (s.Length > max) throw new HlksFehler($"«{name}» ist zu lang (höchstens {max} Zeichen).", $"{was} kürzen.");
        return s;
    }

    /// <summary>Prüft den Körper eines PUT und liefert den bereinigten Eintrag (ohne «geaendert»; das setzt Setzen). Fehler: HlksFehler mit Vorschlag.</summary>
    public static JsonObject Pruefen(string? id, JsonObject? koerper)
    {
        PruefeId(id);
        koerper ??= new JsonObject();
        var punkte = new JsonObject();
        if (koerper.TryGetPropertyValue("punkte", out var pn) && pn is not null)
        {
            if (pn is not JsonObject po) throw new HlksFehler("«punkte» muss ein Objekt sein ({ \"0\": \"ok\", \"1\": \"mangel\" }).", "Je Prüfpunkt die Nummer und «ok» oder «mangel» senden.");
            if (po.Count > PunkteMax) throw new HlksFehler($"Zu viele Prüfpunkte ({po.Count}, höchstens {PunkteMax}).", "Weniger Prüfpunkte je Funktion senden.");
            foreach (var (k, w) in po)
            {
                if (!PunktMuster.IsMatch(k) || int.Parse(k, CultureInfo.InvariantCulture) >= PunkteMax)
                    throw new HlksFehler($"Die Punktnummer «{k}» ist ungültig.", $"Nummern von 0 bis {PunkteMax - 1}.");
                var s = w is JsonValue v && v.TryGetValue<string>(out var t) ? t : null;
                if (s is not ("ok" or "mangel")) throw new HlksFehler($"Der Wert von Punkt {k} muss «ok» oder «mangel» sein.", "Einen Punkt ohne Befund einfach weglassen.");
                punkte[k] = s;
            }
        }
        var texte = new JsonObject();
        if (koerper.TryGetPropertyValue("texte", out var tn) && tn is not null)
        {
            if (tn is not JsonObject to) throw new HlksFehler("«texte» muss ein Objekt sein.", "Je Punktnummer den Text des Prüfpunkts senden.");
            if (to.Count > PunkteMax) throw new HlksFehler($"Zu viele Texte ({to.Count}, höchstens {PunkteMax}).", "Weniger Prüfpunkte je Funktion senden.");
            foreach (var (k, w) in to)
            {
                if (!PunktMuster.IsMatch(k) || int.Parse(k, CultureInfo.InvariantCulture) >= PunkteMax) throw new HlksFehler($"Die Textnummer «{k}» ist ungültig.", $"Nummern von 0 bis {PunkteMax - 1}.");
                if (w is not JsonValue v || !v.TryGetValue<string>(out var s)) throw new HlksFehler($"Der Text zu Punkt {k} muss ein Text sein.", "Den Wortlaut des Prüfpunkts senden.");
                if (s.Length > TextMax) throw new HlksFehler($"Der Text zu Punkt {k} ist zu lang (höchstens {TextMax} Zeichen).", "Kürzen.");
                texte[k] = s;
            }
        }
        return new JsonObject
        {
            ["titel"] = Zeichenkette(koerper, "titel", TitelMax, "den Titel der Funktion") ?? "",
            ["punkte"] = punkte,
            ["notiz"] = Zeichenkette(koerper, "notiz", NotizMax, "die Notiz") ?? "",
            ["stand"] = Zeichenkette(koerper, "stand", StandMax, "den geprüften Stand (Commit)") ?? "",
            ["texte"] = texte,
        };
    }

    // ---------------------------------------------------------------- Vereinigen (rein)

    static DateTimeOffset Zeit(JsonNode? n) =>
        n is JsonValue v && v.TryGetValue<string>(out var s) && DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var z) ? z : DateTimeOffset.MinValue;

    /// <summary>Vereinigt die Dateien aller Rechner (Rechnername, Inhalt { eintraege }): je Funktion der Eintrag mit dem jüngsten «geaendert», bei
    /// Gleichstand der grössere Rechnername (alle Rechner sehen dasselbe). Das Ergebnis trägt zusätzlich «rechner».</summary>
    public static JsonObject Vereinigen(IEnumerable<(string Rechner, JsonObject? Datei)> dateien)
    {
        var bester = new Dictionary<string, (JsonObject Eintrag, string Rechner, DateTimeOffset Zeit)>(StringComparer.Ordinal);
        foreach (var (rechner, datei) in dateien)
        {
            if (datei?["eintraege"] is not JsonObject eintraege) continue;
            foreach (var (id, n) in eintraege)
            {
                if (n is not JsonObject e || string.IsNullOrEmpty(id) || id.Length > IdMax) continue;
                var z = Zeit(e["geaendert"]);
                if (bester.TryGetValue(id, out var alt) && (alt.Zeit > z || (alt.Zeit == z && string.CompareOrdinal(alt.Rechner, rechner) >= 0))) continue;
                bester[id] = (e, rechner, z);
            }
        }
        var erg = new JsonObject();
        foreach (var (id, b) in bester.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            var kopie = (JsonObject)b.Eintrag.DeepClone();
            kopie["rechner"] = b.Rechner;
            erg[id] = kopie;
        }
        return erg;
    }

    // ---------------------------------------------------------------- Lesen und Schreiben

    static JsonObject? DateiLesen(string pfad, out bool ioFehler)
    {
        ioFehler = false;
        try { return JsonNode.Parse(File.ReadAllText(pfad)) as JsonObject; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { ioFehler = true; return null; }
        catch (JsonException) { return null; }
    }

    JsonObject EigeneLesen()
    {
        JsonObject Leer() => new() { ["format"] = 1, ["rechner"] = Rechner, ["eintraege"] = new JsonObject() };
        if (!File.Exists(ablage.EigenerPfad)) return Leer();
        for (int versuch = 0; versuch < 4; versuch++)
        {
            var d = DateiLesen(ablage.EigenerPfad, out var ioFehler);
            if (d is not null) { if (d["eintraege"] is not JsonObject) d["eintraege"] = new JsonObject(); return d; }
            if (!ioFehler) return Leer();   // kaputter Inhalt: nicht zu retten, die Datei wird neu geschrieben
            Thread.Sleep(50);
        }
        throw new IOException($"Die Datei {ablage.EigenerPfad} ist gerade nicht lesbar.");   // nicht überschreiben
    }

    /// <summary>Alle Einträge, vereinigt über die Rechner des Benutzers. Im Testlauf ohne Umleitung nach %TEMP%: leer.</summary>
    public JsonObject Alle()
    {
        if (!Erlaubt) return new JsonObject();
        try
        {
            lock (sperre)
            {
                var dateien = new List<(string, JsonObject?)>();
                foreach (var f in ablage.AndereDateien())
                {
                    var d = DateiLesen(f, out _);
                    var name = d?["rechner"] is JsonValue v && v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s) ? s : Path.GetFileNameWithoutExtension(f);
                    dateien.Add((name, d));
                }
                if (File.Exists(ablage.EigenerPfad)) dateien.Add((Rechner, DateiLesen(ablage.EigenerPfad, out _)));
                return Vereinigen(dateien);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return new JsonObject(); }
    }

    /// <summary>Setzt den Eintrag einer Funktion in der Datei dieses Rechners («geaendert» = jetzt). Liefert den gespeicherten Eintrag.</summary>
    public JsonObject Setzen(string? id, JsonObject? koerper)
    {
        var eintrag = Pruefen(id, koerper);
        if (!Erlaubt)
            throw new HlksFehler($"Der Prüfstand der Anleitung ({Verzeichnis}) wird im Testlauf nicht beschrieben.", "Im Test AnleitungPruefung bzw. HLKS_ANLEITUNG_PRUEFUNG auf einen Ordner unter %TEMP% setzen.");
        eintrag["geaendert"] = Uhr().ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
        try
        {
            lock (sperre)
            {
                var d = EigeneLesen();
                var eintraege = (JsonObject)d["eintraege"]!;
                if (!eintraege.ContainsKey(id!) && eintraege.Count >= EintraegeMax)
                    throw new HlksFehler($"Zu viele Einträge im Prüfstand (höchstens {EintraegeMax}).", "Alte Einträge entfernen.");
                eintraege[id!] = eintrag.DeepClone();
                d["format"] = 1;
                d["rechner"] = Rechner;
                ablage.Schreiben(d.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
                return eintrag;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new HlksFehler($"Der Prüfstand konnte nicht gespeichert werden ({Verzeichnis}): {ex.Message}", "OneDrive und Schreibrechte prüfen und erneut versuchen.");
        }
    }
}
