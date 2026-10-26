// Benutzereinstellungen – gleich auf allen Rechnern des Benutzers, in allen Projekten (G1, entwurf/projektbrowser-gliederung.md,
// Entscheid Christian 19.10.2026: «je Benutzer, überall (alle Projekte, alle Rechner des Benutzers, wie «zuletzt geöffnet»)»).
//
// Ablage (gemeinsamer Unterbau mit der Liste «zuletzt»: RechnerAblage):
//   %OneDrive%\HLKS-CAD\einstellungen\<Rechner>.json   JE RECHNER EINE DATEI; ein Rechner schreibt nur seine eigene.
//   Ohne OneDrive: %APPDATA%\HLKS-CAD\einstellungen.json
//   Umleitung für Tests: Einstellungen.BenutzerOrdner bzw. Umgebungsvariable HLKS_BENUTZER (Ordner mit den Rechnerdateien).
// Dateiformat: { format: 1, rechner, werte: { "<schluessel>": { wert, zeit } } }. Ein Wert ohne «wert» heisst: zurückgesetzt (gilt ab «zeit»).
//
// Vereinigung (Vereinigen, reine Funktion): je Schlüssel gilt der Eintrag mit der neuesten Zeit über alle Rechner; bei gleicher Zeit
//   entscheidet der Rechnername (grösser gewinnt) – damit alle Rechner dasselbe Ergebnis sehen. Zeiten sind UTC der jeweiligen Rechner;
//   gehen Uhren nach, gewinnt der Rechner mit der späteren Uhr (gleiche Annahme wie bei «zuletzt»).
// Schutz: Im Testlauf (HLKS_TEST oder Testrunner) wird nur unter %TEMP% gelesen und geschrieben. Ist keine Umleitung nach %TEMP% gesetzt,
//   sieht der Test leere Einstellungen (Vorgabe «Gebäude» im Projektbrowser) und das Schreiben wird abgelehnt – die echten Dateien des
//   Benutzers werden nie berührt (auch nicht lesend: ein Test hängt nicht von der Wahl des Entwicklers ab).
//
// Schlüssel: Kleinbuchstaben, Ziffern, Punkt, Unterstrich, Bindestrich. Bekannt (mit Prüfung des Werts): projektbrowser.gliederung (seit G2 eine Liste von Kennungen; Einzeltext bleibt gültig).
// Weitere Schlüssel (z. B. Design hell/dunkel, Menüband minimiert) sind ohne Änderung hier möglich: jeder Schlüssel nimmt Text, Zahl,
// Ja/Nein sowie kleine Objekte und Listen (bis 4000 Zeichen) auf.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Hlks.Kern;

namespace Hlks.Web;

/// <summary>Ein Wert mit Zeitpunkt der letzten Änderung; ein fehlender Wert heisst «zurückgesetzt».</summary>
public sealed class EinstellungsWert
{
    public JsonNode? Wert { get; set; }
    public DateTimeOffset Zeit { get; set; }
}

/// <summary>Die Datei eines Rechners.</summary>
public sealed class EinstellungsDatei
{
    public int Format { get; set; } = 1;
    public string Rechner { get; set; } = "";
    public Dictionary<string, EinstellungsWert> Werte { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>Ein Wert der vereinigten Einstellungen mit dem Rechner, der ihn zuletzt gesetzt hat.</summary>
public sealed record EinstellungsStand(string Schluessel, JsonNode? Wert, DateTimeOffset Zeit, string Rechner);

public sealed class BenutzerEinstellungen
{
    static readonly Regex SchluesselMuster = new(@"^[a-z][a-z0-9_.\-]{0,63}$", RegexOptions.CultureInvariant);
    static readonly Regex KennungMuster = new(@"^[a-z][a-z0-9_]{0,31}$", RegexOptions.CultureInvariant);
    const int WertMax = 4000;

    /// <summary>Schlüssel mit eigener Prüfung des Werts (Meldung bei Fehler, sonst null). Weitere Schlüssel gelten ohne Eintrag hier.</summary>
    public static readonly IReadOnlyDictionary<string, Func<JsonNode, string?>> Bekannt = new Dictionary<string, Func<JsonNode, string?>>(StringComparer.Ordinal)
    {
        // Die gewählten Gliederungen (G2): eine Liste von Kennungen (gebaeude, sanitaer, …), z. B. ["sanitaer","heizung"]; ein alter Einzelwert (Text) gilt als Liste mit einem Eintrag.
        // Welche Kennungen es gibt und welche sich ausschliessen («Gebäude»), steht in der Tabelle der Oberfläche (projektbrowser-gliederung.js);
        // hier nur die Form – eine unbekannte Kennung ignoriert die Oberfläche.
        ["projektbrowser.gliederung"] = GliederungPruefen,
        // Symbolsatz der Oberfläche (Teil S, entwurf/menueband-revit.md): «strich» (Vorgabe, einfarbige Strichsymbole) oder «revit» (mehrfarbiger Satz im Stil von Revit, Probe).
        ["oberflaeche.symbole"] = SymbolsatzPruefen,
        // Rand um den Raum bei «Raum im 3D zeigen» in mm (N14, Vorgabe 1500 steht in der Oberfläche): eine Zahl von 0 bis 100000.
        ["ansicht3d.raumrand"] = RaumrandPruefen,
        // Augenhöhe bei «Raum begehen» in mm über OK Boden (N15, Vorgabe 1700 steht in der Oberfläche und im Kern): eine Zahl von 300 bis 5000.
        ["ansicht3d.augenhoehe"] = AugenhoeheDaten,
        // Pfad zur adb.exe der Android Platform Tools für «Quest verbinden» (Modul XR, BrillenDienst.cs): leer/zurückgesetzt = suchen; sonst ein
        // vollständiger lokaler Pfad, der auf adb.exe endet (kein Netzpfad – es wird ein Programm gestartet).
        ["quest.adb"] = BrillenDienst.AdbPfadPruefen,
        // «Brille automatisch verbinden» (Modul XR): Ja/Nein, Vorgabe nein; und die gemerkten Brillen (Liste { seriennummer, mac, letzteIp, zuletzt }, gültige MAC).
        ["quest.automatisch"] = BrillenDienst.AutomatikPruefen,
        ["quest.brillen"] = BrillenDienst.BrillenPruefen,
        // «Begehung beim Aufsetzen automatisch starten» (Modul XR, QuestStarter.cs): Ja/Nein, Vorgabe ja.
        ["quest.autostart"] = BrillenDienst.AutomatikPruefen,
        // Quest App (Quest App P7): Ordner mit der abgelegten APK und version.json (Vorgabe %USERPROFILE%\HLKS_CAD_werkzeuge\hk_xr, HK_XR werkzeuge/app_bereitstellen.ps1; H+K XR E1: gilt für KICAD und XR-Labor); ein vollständiger lokaler Ordnerpfad.
        ["quest.app_ablage"] = BrillenDienst.AppAblagePruefen,
        // Brillen der Quest App in der 3D-Ansicht am PC zeigen (Quest App P7, Register Ansicht): Ja/Nein, Vorgabe ja.
        ["ansicht3d.brillen_zeigen"] = BrillenDienst.AutomatikPruefen,
        // Gesten am Arbeitsplatz (Modul G, entwurf/modul-gesten.md, GestenEinstellung.cs): { an (Vorgabe nein), hand (links), kameraId, skelett (ja), stellschrauben { Name: Zahl }, belegung { G2 { links, rechts }, G6 } };
        // jedes Feld wahlweise, unbekannte Felder werden abgelehnt (nie Bilddaten). Das Kamerabild bleibt im Browser.
        ["gesten"] = GestenEinstellung.Pruefen,
        // Der Zugang der Quest App im Büronetz (Modul XR, Quest App P3) ist bewusst KEINE Benutzereinstellung: er gilt je PC (Christian 23.10.2026),
        // Schalter und Ports stehen in %LOCALAPPDATA%\HLKS_CAD\xr\zugang.json (XrPcEinstellung in XrEinstellungen.cs).
    };

    static string? AugenhoeheDaten(JsonNode w) =>
        w is JsonValue v && v.TryGetValue<double>(out var d) && d >= RaumBegehen.AugenhoeheMin && d <= RaumBegehen.AugenhoeheMax ? null
            : $"Die Augenhöhe muss eine Zahl von {RaumBegehen.AugenhoeheMin} bis {RaumBegehen.AugenhoeheMax} sein (mm).";

    static string? RaumrandPruefen(JsonNode w) =>
        w is JsonValue v && v.TryGetValue<double>(out var d) && d >= 0 && d <= 100000 ? null : "Der Rand muss eine Zahl von 0 bis 100000 sein (mm).";

    static string? SymbolsatzPruefen(JsonNode w) =>
        w is JsonValue v && v.TryGetValue<string>(out var s) && (s == "strich" || s == "revit") ? null : "Erlaubt sind «strich» (Vorgabe) und «revit».";

    const int GliederungenMax = 16;
    const string GliederungHinweis = "Der Wert muss eine Liste von Kennungen der Gliederungen sein (Kleinbuchstaben, z. B. [\"sanitaer\",\"heizung\"]); ein einzelner Text wie «gebaeude» gilt als Liste mit einem Eintrag.";

    static string? GliederungPruefen(JsonNode w)
    {
        static bool Kennung(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) && KennungMuster.IsMatch(s);
        if (w is JsonValue) return Kennung(w) ? null : GliederungHinweis;
        if (w is not JsonArray liste) return GliederungHinweis;
        if (liste.Count == 0) return "Die Liste darf nicht leer sein: mindestens eine Gliederung bleibt gewählt.";
        if (liste.Count > GliederungenMax) return $"Die Liste darf höchstens {GliederungenMax} Gliederungen enthalten.";
        if (!liste.All(Kennung)) return GliederungHinweis;
        if (liste.Select(n => n!.GetValue<string>()).Distinct(StringComparer.Ordinal).Count() != liste.Count) return "Jede Gliederung darf nur einmal in der Liste stehen.";
        return null;
    }

    readonly RechnerAblage ablage;
    readonly object sperre = new();

    public string Verzeichnis => ablage.Verzeichnis;
    public string Rechner => ablage.Rechner;
    /// <summary>OneDrive | Umleitung | lokal – woher die Einstellungen kommen.</summary>
    public string Quelle => ablage.Quelle;
    /// <summary>Uhr für die Zeitstempel (Tests setzen eine feste).</summary>
    public Func<DateTimeOffset> Uhr { get; set; } = () => DateTimeOffset.UtcNow;

    public BenutzerEinstellungen(RechnerAblage ablage) { this.ablage = ablage; }

    /// <summary>Wählt die Ablage: Einstellung/Umgebung HLKS_BENUTZER › OneDrive des Benutzers › %APPDATA%.</summary>
    public static BenutzerEinstellungen Aus(Einstellungen e) =>
        new(RechnerAblage.Waehlen("einstellungen", "einstellungen.json", e.BenutzerOrdner, "HLKS_BENUTZER", e.Geraet));

    /// <summary>Im Testlauf nur unter %TEMP% (siehe Kopf).</summary>
    bool LesenErlaubt => ablage.SchreibenErlaubt;

    // ---------------------------------------------------------------- Prüfen (rein)

    /// <summary>Prüft Schlüssel und Wert; wirft HlksFehler mit Vorschlag. Ein Wert null setzt den Schlüssel zurück.</summary>
    public static void Pruefen(string? schluessel, JsonNode? wert)
    {
        if (string.IsNullOrWhiteSpace(schluessel))
            throw new HlksFehler("Parameter «schluessel» fehlt.", "Den Namen der Einstellung angeben, z. B. «projektbrowser.gliederung».");
        if (!SchluesselMuster.IsMatch(schluessel))
            throw new HlksFehler($"Der Schlüssel «{schluessel}» ist ungültig.", "Erlaubt sind Kleinbuchstaben, Ziffern, Punkt, Unterstrich und Bindestrich (bis 64 Zeichen, mit einem Buchstaben am Anfang).");
        if (wert is null) return;
        if (wert.ToJsonString().Length > WertMax)
            throw new HlksFehler($"Der Wert für «{schluessel}» ist zu gross (höchstens {WertMax} Zeichen).", "Einen kürzeren Wert wählen.");
        if (Bekannt.TryGetValue(schluessel, out var pruefung) && pruefung(wert) is { } meldung)
            throw new HlksFehler($"Der Wert für «{schluessel}» ist ungültig. {meldung}", "Einen gültigen Wert senden oder null, um die Einstellung zurückzusetzen.");
    }

    // ---------------------------------------------------------------- Lesen

    /// <summary>Liest eine Datei; null, wenn sie fehlt oder unlesbar ist (ioFehler zeigt, ob es am Zugriff lag).</summary>
    public static EinstellungsDatei? DateiLesen(string pfad, string rechnerVorgabe, out bool ioFehler)
    {
        ioFehler = false;
        try
        {
            var d = JsonSerializer.Deserialize<EinstellungsDatei>(File.ReadAllText(pfad), Json.Optionen);
            if (d is null) return null;
            if (string.IsNullOrWhiteSpace(d.Rechner)) d.Rechner = rechnerVorgabe;
            d.Werte ??= new(StringComparer.Ordinal);
            return d;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { ioFehler = true; return null; }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException) { return null; }
    }

    /// <summary>Vereinigt die Dateien aller Rechner: je Schlüssel der Eintrag mit der neuesten Zeit (bei Gleichstand der grössere Rechnername).
    /// Zurückgesetzte Schlüssel (ohne Wert) erscheinen mit Wert null, damit ein neuerer Reset einen älteren Wert eines anderen Rechners aufhebt.</summary>
    public static Dictionary<string, EinstellungsStand> Vereinigen(IEnumerable<EinstellungsDatei> dateien)
    {
        var e = new Dictionary<string, EinstellungsStand>(StringComparer.Ordinal);
        foreach (var d in dateien)
            foreach (var (k, w) in d.Werte)
            {
                if (string.IsNullOrWhiteSpace(k) || w is null) continue;
                if (e.TryGetValue(k, out var alt) && (alt.Zeit > w.Zeit || (alt.Zeit == w.Zeit && string.CompareOrdinal(alt.Rechner, d.Rechner) >= 0))) continue;
                e[k] = new EinstellungsStand(k, w.Wert, w.Zeit, d.Rechner);
            }
        return e;
    }

    EinstellungsDatei EigeneLesen()
    {
        if (!File.Exists(ablage.EigenerPfad)) return new EinstellungsDatei { Rechner = Rechner };
        for (int versuch = 0; versuch < 4; versuch++)
        {
            var d = DateiLesen(ablage.EigenerPfad, Rechner, out var ioFehler);
            if (d is not null) { d.Rechner = Rechner; return d; }
            if (!ioFehler) return new EinstellungsDatei { Rechner = Rechner };   // kaputter Inhalt: nicht zu retten, die Datei wird neu geschrieben
            Thread.Sleep(50);
        }
        // Zugriff klemmt dauernd: nicht überschreiben (sonst gingen die anderen Einstellungen dieses Rechners verloren)
        throw new IOException($"Die Datei {ablage.EigenerPfad} ist gerade nicht lesbar.");
    }

    /// <summary>Alle Einstellungen, vereinigt über die Rechner des Benutzers (ohne zurückgesetzte). Im Testlauf ohne Umleitung nach %TEMP%: leer.</summary>
    public Dictionary<string, EinstellungsStand> Alle()
    {
        var leer = new Dictionary<string, EinstellungsStand>(StringComparer.Ordinal);
        if (!LesenErlaubt) return leer;
        try
        {
            lock (sperre)
            {
                var dateien = new List<EinstellungsDatei>();
                foreach (var f in ablage.AndereDateien())
                    if (DateiLesen(f, Path.GetFileNameWithoutExtension(f), out _) is { } d) dateien.Add(d);
                if (File.Exists(ablage.EigenerPfad) && DateiLesen(ablage.EigenerPfad, Rechner, out _) is { } eigene) { eigene.Rechner = Rechner; dateien.Add(eigene); }
                return Vereinigen(dateien).Where(p => p.Value.Wert is not null).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return leer; }
    }

    /// <summary>Der Wert eines Schlüssels oder null.</summary>
    public JsonNode? Hole(string schluessel) => Alle().TryGetValue(schluessel, out var s) ? s.Wert : null;

    // ---------------------------------------------------------------- Schreiben

    /// <summary>Setzt eine Einstellung (wert null = zurücksetzen) in der Datei dieses Rechners. Fehler beim Speichern werden gemeldet.</summary>
    public EinstellungsStand Setzen(string? schluessel, JsonNode? wert)
    {
        Pruefen(schluessel, wert);
        if (!ablage.SchreibenErlaubt)
            throw new HlksFehler($"Die Benutzereinstellungen ({Verzeichnis}) werden im Testlauf nicht beschrieben.", "Im Test BenutzerOrdner bzw. HLKS_BENUTZER auf einen Ordner unter %TEMP% setzen.");
        try
        {
            lock (sperre)
            {
                var d = EigeneLesen();
                var zeit = Uhr();
                d.Werte[schluessel!] = new EinstellungsWert { Wert = wert?.DeepClone(), Zeit = zeit };
                d.Rechner = Rechner;
                d.Format = 1;
                ablage.Schreiben(Json.Text(d, true));
                return new EinstellungsStand(schluessel!, wert, zeit, Rechner);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new HlksFehler($"Die Einstellungen konnten nicht gespeichert werden ({Verzeichnis}): {ex.Message}", "OneDrive und Schreibrechte prüfen und erneut versuchen.");
        }
    }
}
