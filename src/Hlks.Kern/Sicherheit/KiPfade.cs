// Sicherheitsprüfung 3c, Befunde B3(a) und B4 (berichte/modul-3c-sicherheitspruefung.md): EINE Pfadprüfung für Akteure der Art «ki».
//
// Dateipfade in Parametern darf die KI nur unter diesen Wurzeln angeben (alle Werkzeuge mit Pfad-Parametern, Stand 20.10.2026:
// unterlagen_lesen, befunde, unterlage_bild, familie_vergleichen, unterlage_einfuegen, ifc_pruefen, ifc_uebernehmen, familie_laden, bcf_lesen,
// bcf_import, bcf_export, aussparungen_ifc, plan_pdf, plansatz_pdf, plansatz_setzen ausgabe.ordner; norm_lesen prüft selbst gegen die Normenwurzel,
// familie_speichern lehnt die KI ganz ab):
//   - Projektordner: Ablage des offenen Projekts (…\e_KICAD); ohne Ablage dessen Unterlagenordner unter %TEMP%
//   - Projektablage:  HLKS_PROJEKTABLAGE › kataloge/buero.json projektablage.pfad
//   - Familienbibliothek: HLKS_FAMILIENBIBLIOTHEK › buero.json familienbibliothek.pfad (nur, wenn die Bibliothek aktiv ist) und die
//     mitgelieferten Familien kataloge/familien (Rückfall der Bibliothek, nur lesbar)
//   - NICHT die Normen (Entscheid Hauptlauf 20.10.2026): lizenzierte Normtexte dürfen nie als Unterlage ins Projekt kopiert werden; norm_lesen und
//     normen_suchen prüfen selbst gegen die Normenwurzel (Normenverzeichnis) und geben nur Auszüge
// UNC-Pfade nur zu den Servern aus buero.json netz.server (\\HK-NAS02); ein Laufwerk aus netz.laufwerke (W:) gilt gleich wie seine UNC-Form.
// Geprüft wird VOR jedem Dateizugriff: schon File.Exists auf \\fremder-server\… baut eine SMB- bzw. WebDAV-Verbindung nach aussen auf (B4).
// Gerätepfade (\\?\, \\.\) und nicht vollständige Pfade (relativ, «C:datei») sind für die KI nie erlaubt; ein blosser Dateiname meint – je nach
// Werkzeug – den Ordner unterlagen des Projekts bzw. kataloge/familien. Menschen (Akteur-Art mensch, beispiel, import) bleiben unverändert.
// ANNAHMEN (Prüfliste): Verknüpfungen (Junctions, symbolische Links) innerhalb einer Wurzel werden nicht aufgelöst; 8.3-Kurznamen werden nicht
// erweitert (ein solcher Pfad passt zu keiner Wurzel und wird abgelehnt); Gross-/Kleinschreibung zählt nicht (Windows).

using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <summary>Eine erlaubte Wurzel für Dateipfade der KI (Name für Meldungen, Pfad wie konfiguriert).</summary>
public sealed record KiWurzel(string Name, string Pfad);

/// <summary>Regeln der Pfadprüfung: Wurzeln, erlaubte UNC-Server und Laufwerke mit ihrer UNC-Form (aus kataloge/buero.json › netz).</summary>
public sealed class KiPfadRegeln
{
    public IReadOnlyList<KiWurzel> Wurzeln { get; init; } = [];
    /// <summary>Server, zu denen UNC-Pfade gehen dürfen (ohne «\\»).</summary>
    public IReadOnlyList<string> Server { get; init; } = [];
    /// <summary>Laufwerk («W:») → UNC-Form («\\HK-NAS02\WorkShare_NAS»).</summary>
    public IReadOnlyDictionary<string, string> Laufwerke { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public static class KiPfade
{
    /// <summary>Anfang jeder Ablehnung (Tests und Oberfläche erkennen sie daran).</summary>
    public const string Meldung = "Pfad für die KI nicht erlaubt";

    /// <summary>Ist der Akteur des Projekts eine KI (Akteur-Art «ki»)?</summary>
    public static bool IstKi(Projekt p) => string.Equals(p.AkteurArt, "ki", StringComparison.OrdinalIgnoreCase);

    /// <summary>Ein blosser Name ohne Ordner und Laufwerk (kein «\», «/», «:»)? Solche Angaben meinen eine Unterlage bzw. Familie, keinen Pfad.</summary>
    public static bool IstName(string? s)
    {
        var t = (s ?? "").Trim().Trim('"').Trim();
        return t.Length > 0 && t.IndexOfAny(['\\', '/', ':']) < 0 && t is not ("." or "..");
    }

    /// <summary>Prüft einen Dateipfad aus einem Parameter, bevor der Kern die Datei berührt. Für Menschen unverändert zurück; für die KI der volle
    /// Pfad, wenn er unter einer erlaubten Wurzel liegt – sonst HlksFehler «Pfad für die KI nicht erlaubt …».</summary>
    public static string Pruefen(Projekt p, string pfad, string werkzeug)
    {
        if (!IstKi(p)) return pfad;
        var r = Regeln(p);
        var grund = Grund(r, pfad, out var voll);
        if (grund is null) return voll!;
        throw new HlksFehler($"{Meldung}: «{Kurz(pfad)}» ({werkzeug}) – {grund}.", Vorschlag(r));
    }

    /// <summary>Grund der Ablehnung (null = erlaubt) und der volle Pfad. Ohne Dateizugriff: nur Text, damit nichts nach aussen geht.</summary>
    public static string? Grund(KiPfadRegeln r, string? pfad, out string? voll)
    {
        voll = null;
        var s = (pfad ?? "").Trim().Trim('"').Trim();
        if (s.Length == 0) return "leerer Pfad";
        if (s.Any(char.IsControl)) return "Steuerzeichen im Pfad";
        var n = s.Replace('/', '\\');
        if (n.StartsWith(@"\\?\", StringComparison.Ordinal) || n.StartsWith(@"\\.\", StringComparison.Ordinal) || n.StartsWith(@"\??\", StringComparison.Ordinal))
            return @"Gerätepfade (\\?\, \\.\) sind nicht erlaubt";
        if (!Path.IsPathFullyQualified(n)) return @"kein vollständiger Pfad (Laufwerk mit «\» oder \\Server\Freigabe)";
        string full;
        try { full = Path.GetFullPath(n); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return "ungültiger Pfad"; }
        voll = full;
        var kanon = Kanonisch(r, full);
        if (Server(kanon) is { } server && !r.Server.Contains(server, StringComparer.OrdinalIgnoreCase))
            return r.Server.Count == 0 ? "UNC-Pfade sind nicht erlaubt (kein Server in kataloge/buero.json › netz)"
                : $@"UNC-Pfade nur zum Server {string.Join(", ", r.Server.Select(x => @"\\" + x))} (nicht \\{server})";
        foreach (var w in r.Wurzeln)
        {
            string wurzel;
            try { wurzel = Kanonisch(r, Path.GetFullPath(w.Pfad.Trim().Replace('/', '\\'))); }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { continue; }
            if (Unter(kanon, wurzel)) return null;
        }
        return "nicht unter dem Projektordner oder einer Wurzel aus kataloge/buero.json (Projektablage, Familienbibliothek)";
    }

    /// <summary>Regeln für ein Projekt: Projektordner, Familienbibliothek (wenn aktiv), mitgelieferte Familien, dazu Projektablage und Netz
    /// aus Umgebung und kataloge/buero.json.</summary>
    public static KiPfadRegeln Regeln(Projekt p)
    {
        var w = new List<KiWurzel>();
        if (p.Speicher?.Ort is { Length: > 0 } ort) w.Add(new("Projektordner", ort));
        else w.Add(new("Unterlagen des Projekts", Path.Combine(Path.GetTempPath(), "HLKS_CAD", "unterlagen", p.Kopf.Id)));
        var bib = FamilienBibliothekDienst.Ort(p.Katalog);
        if (bib.Aktiv && bib.Pfad is { Length: > 0 } bp) w.Add(new("Familienbibliothek", bp));
        w.Add(new("Familien (kataloge/familien)", Path.Combine(p.Katalog.Ordner, "familien")));
        return AusBuero(p.Katalog.Ordner, w, Environment.GetEnvironmentVariable);
    }

    /// <summary>Regeln aus kataloge/buero.json und der Umgebung (öffentlich für Tests): weitere Wurzeln plus Projektablage (nie Normen); Server und Laufwerke
    /// aus netz.</summary>
    public static KiPfadRegeln AusBuero(string katalogOrdner, IEnumerable<KiWurzel> weitere, Func<string, string?> umgebung)
    {
        var b = BueroLesen(katalogOrdner);
        var w = new List<KiWurzel>(weitere);
        static string? Leer(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        static string? T(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s) ? s.Trim() : null;
        if ((Leer(umgebung("HLKS_PROJEKTABLAGE")) ?? T(b?["projektablage"]?["pfad"])) is { } ablage) w.Add(new("Projektablage", ablage));
        var server = (b?["netz"]?["server"] as JsonArray ?? []).Select(T).Where(x => x is not null).Select(x => x!.TrimStart('\\')).ToList();
        var laufwerke = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (b?["netz"]?["laufwerke"] is JsonObject lw)
            foreach (var (k, v) in lw)
                if (T(v) is { } unc && k.Trim().TrimEnd('\\') is { Length: 2 } lk && lk[1] == ':') laufwerke[lk] = unc.TrimEnd('\\');
        return new KiPfadRegeln { Wurzeln = w, Server = server, Laufwerke = laufwerke };
    }

    /// <summary>Vorschlag zur Ablehnung: die erlaubten Wurzeln und der Weg über den Menschen.</summary>
    static string Vorschlag(KiPfadRegeln r) =>
        "Erlaubt sind Dateien unter: " + string.Join("; ", r.Wurzeln.Select(w => $"{w.Name} ({w.Pfad})")) +
        (r.Server.Count > 0 ? $"; UNC nur zu {string.Join(", ", r.Server.Select(x => @"\\" + x))}" : "") +
        ". Andere Dateien legt ein Mensch über «Unterlagen» (Hochladen) ins Projekt; dann die Unterlage über Kennzeichen oder Dateinamen ansprechen.";

    static string Kurz(string s) { s = s.Trim(); return s.Length <= 160 ? s : s[..160] + "…"; }

    /// <summary>Laufwerk mit bekannter UNC-Form (W: → \\HK-NAS02\WorkShare_NAS) einheitlich als UNC; ohne abschliessendes «\».</summary>
    static string Kanonisch(KiPfadRegeln r, string voll)
    {
        var s = voll;
        if (s.Length >= 2 && s[1] == ':' && r.Laufwerke.TryGetValue(s[..2], out var unc)) s = unc + s[2..];
        return s.Length > 3 ? s.TrimEnd('\\') : s;
    }

    /// <summary>Server eines UNC-Pfads (\\server\…), sonst null.</summary>
    static string? Server(string pfad)
    {
        if (!pfad.StartsWith(@"\\", StringComparison.Ordinal)) return null;
        var rest = pfad[2..];
        var i = rest.IndexOf('\\');
        return i < 0 ? rest : rest[..i];
    }

    static bool Unter(string pfad, string wurzel)
    {
        if (string.Equals(pfad, wurzel, StringComparison.OrdinalIgnoreCase)) return true;
        var mit = wurzel.EndsWith('\\') ? wurzel : wurzel + "\\";
        return pfad.StartsWith(mit, StringComparison.OrdinalIgnoreCase);
    }

    // ---------- kataloge/buero.json (je Datei einmal, neu bei Änderung)

    static readonly ConcurrentDictionary<string, (long Zeit, JsonObject? Inhalt)> cache = new(StringComparer.OrdinalIgnoreCase);

    static JsonObject? BueroLesen(string katalogOrdner)
    {
        var datei = Path.Combine(katalogOrdner, "buero.json");
        try
        {
            if (!File.Exists(datei)) return null;
            var zeit = File.GetLastWriteTimeUtc(datei).Ticks;
            if (cache.TryGetValue(datei, out var c) && c.Zeit == zeit) return c.Inhalt;
            var o = JsonNode.Parse(File.ReadAllText(datei, Encoding.UTF8)) as JsonObject;
            cache[datei] = (zeit, o);
            return o;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }
}
