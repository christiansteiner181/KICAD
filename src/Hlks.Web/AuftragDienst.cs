// KI-Aufträge im Arbeitsplatz-Server (Modul 2, Teil B5): der Lauf «Familie aus Unterlagen».
//
// Ein Auftrag startet Claude Code auf diesem Rechner – derselbe Weg wie HKAP (ClaudeCode.cs): Christians Abo, ohne
// API-Schlüssel (ANTHROPIC_API_KEY wird aus der Umgebung des Kindprozesses entfernt). Claude bekommt nur den MCP-Server
// «hlks» desselben Projektordners (Akteur «KI», Art ki), keine eingebauten Werkzeuge, keine Konnektoren, einen leeren
// Arbeitsordner. Der MCP-Server ist ein zweiter Prozess auf demselben Projektordner: seine Änderungen gehen als Pakete in
// die Ablage, dieser Server sieht sie über den Abgleich (AbgleichDienst) wie Änderungen eines anderen Arbeitsplatzes.
//
// Ablauf eines Auftrags: wartet -> laeuft -> fertig | fehler | abgebrochen. Höchstens ein Lauf zugleich je Projekt.
// Protokoll: <Projektordner>\auftraege\<id>.log (Zeilen von Claude: stream-json; eigene Zeilen als {"type":"hlks",…},
// stderr als {"type":"stderr",…}) und <id>.prompt.md (der Auftragstext). Fortschritt und Ende kommen als Ereignis
// {art:"auftrag", auftrag:{…}} über den Ereignisstrom (/api/ereignisse).
//
// ANNAHMEN (Prüfliste):
// - Erlaubt sind nur die Werkzeuge in AuftragEinstellungen.Werkzeuge (Liste statt «mcp__hlks»): Familien, Unterlagen und
//   lesende Abfragen; ausdrücklich nicht familie_freigeben, konflikt_entscheiden, familie_speichern, element_*, hkap_*, und seit der
//   Familienbibliothek (Modul 2, C3) nicht familie_aktualisieren, familie_in_bibliothek, familienauswahl_setzen (lesen darf die KI
//   die Bibliothek mit familien_bibliothek; Begründung am Feld ErlaubteWerkzeuge).
//   Neue, für den Lauf nötige Werkzeuge (Teil B4) müssen in die Liste.
// - Bei internet = true gilt die engere Liste InternetWerkzeuge (nur Familien und Unterlagen, keine Projektabfragen) und von den
//   eingebauten Werkzeugen nur WebSearch; Seiten und Dateien holt allein unterlage_aus_url (Entscheid Hauptlauf 18.10.2026).
//   Websuchen und Abrufe stehen als eigene Zeilen im Protokoll und im Ergebnis (abrufe).
// - Die CLI hat keine Begrenzung der Runden (kein --max-turns in --help); begrenzt wird nur durch das Zeitlimit.
// - Der Arbeitsordner liegt unter %TEMP% (nie unter OneDrive: sonst läse Claude fremde CLAUDE.md), die MCP-Konfiguration
//   daneben; beides wird nach dem Lauf gelöscht (die Konfiguration steht in der Protokolldatei).
// - Die Liste der Aufträge gilt seit dem Start dieses Servers (nur im Speicher); die Protokolle bleiben im Projektordner.
// - Eine Familie gilt als «von der KI angelegt/geändert», wenn ein neues Paket des Akteurs «KI» (AkteurArt ki) sie berührt.
// - Sicherheitsprüfung 3c (berichte/modul-3c-sicherheitspruefung.md): der MCP-Server registriert mit --werkzeuge nur die Liste des Laufs (B3c);
//   mit Internet liest er nur Herstellerunterlagen (zweck «familie»), aus dem Internet abgerufene und ausdrücklich die Unterlagen des Auftrags
//   (--nur-familienunterlagen, --unterlagen-erlaubt <Datei in der Ablage>…; B3b); Dateipfade der KI nur unter dem Projektordner und den Wurzeln
//   aus kataloge/buero.json (Kern, B3a/B4); Sperrdatei über Prozessgrenzen (KiLaeufe, B8); Umgebung bereinigt und CLAUDE.md-Kette geprüft
//   (KiUmgebung, B12/B13; Warnung im Protokoll und in ergebnis.warnung); Werkzeugergebnisse ohne Kopfzeile ausgewertet (B15).

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Hlks.Kern;

namespace Hlks.Web;

public static class AuftragStatus
{
    public const string Wartet = "wartet", Laeuft = "laeuft", Fertig = "fertig", Fehler = "fehler", Abgebrochen = "abgebrochen";
}

/// <summary>Fehler mit HTTP-Code (404 unbekannt, 409 belegt/schon beendet) und Hinweis.</summary>
public sealed class AuftragHttpFehler(int code, string meldung, string? vorschlag = null) : Exception(meldung)
{
    public int Code { get; } = code;
    public string? Vorschlag { get; } = vorschlag;
}

/// <summary>Einstellungen der KI-Aufträge. Vorgaben für den Betrieb; Tests registrieren eine eigene Fassung im Dienstcontainer
/// (ConfigureTestServices), z. B. mit einer Attrappe statt claude.exe.</summary>
public sealed record AuftragEinstellungen
{
    /// <summary>Zeitlimit eines Laufs (Vorgabe 20 Minuten, Spezifikation).</summary>
    public TimeSpan Zeitlimit { get; init; } = TimeSpan.FromMinutes(20);
    /// <summary>Claude Code. null = suchen (neueste claude.exe der Claude-App); Umgebung HLKS_CLAUDE_EXE.</summary>
    public string? ClaudeExe { get; init; }
    /// <summary>Argumente vor den Schaltern (Attrappe als «dotnet Attrappe.dll»).</summary>
    public IReadOnlyList<string> ClaudeVorArgumente { get; init; } = [];
    /// <summary>Hlks.Mcp.dll des MCP-Servers. null = suchen (Umgebung HLKS_MCP_DLL, neben dem Programm, Ausgabeordner des Baus).</summary>
    public string? McpDll { get; init; }
    /// <summary>dotnet.exe, mit der der MCP-Server startet. null = dieselbe wie dieser Server.</summary>
    public string? DotnetPfad { get; init; }
    /// <summary>Ordner mit familie_aus_unterlagen.md. null = suchen (Umgebung HLKS_KI, Quellordner, aufwärts vom Programm).</summary>
    public string? VorlagenOrdner { get; init; }
    /// <summary>Schema der Familie für die Vorlage. null = suchen.</summary>
    public string? SchemaDatei { get; init; }
    /// <summary>Wurzel der Arbeitsordner je Auftrag. null = %TEMP%\HLKS_CAD\auftraege.</summary>
    public string? ArbeitsWurzel { get; init; }
    /// <summary>Modell (--model); null = Vorgabe der Claude-Installation.</summary>
    public string? Modell { get; init; }
    /// <summary>Denktiefe (--effort); null = Vorgabe.</summary>
    public string? Aufwand { get; init; }
    /// <summary>Akteur der KI im Projekt (MCP-Server --akteur).</summary>
    public string Akteur { get; init; } = "KI";
    /// <summary>Erlaubte MCP-Werkzeuge (Namen ohne Präfix).</summary>
    public IReadOnlyList<string> Werkzeuge { get; init; } = AuftragDienst.ErlaubteWerkzeuge;
    /// <summary>Erlaubte MCP-Werkzeuge bei internet = true (eingegrenzt, siehe AuftragDienst.InternetWerkzeuge).</summary>
    public IReadOnlyList<string> WerkzeugeInternet { get; init; } = AuftragDienst.InternetWerkzeuge;
    /// <summary>So viele letzte Schritte stehen im Status.</summary>
    public int SchritteImStatus { get; init; } = 15;
    /// <summary>Nach dem Beenden so lange auf die Ausgabe von Claude warten.</summary>
    public TimeSpan AusgabeNachlauf { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Vorgabe für den Betrieb, mit den Umgebungsvariablen HLKS_CLAUDE_EXE, HLKS_MCP_DLL, HLKS_KI, HLKS_KI_MODELL, HLKS_KI_AUFWAND und
    /// HLKS_KI_ZEITLIMIT_MIN.</summary>
    public static AuftragEinstellungen Vorgabe()
    {
        static string? U(string n) => Environment.GetEnvironmentVariable(n) is { Length: > 0 } s ? s : null;
        var e = new AuftragEinstellungen
        {
            ClaudeExe = U("HLKS_CLAUDE_EXE"), McpDll = U("HLKS_MCP_DLL"), VorlagenOrdner = U("HLKS_KI"),
            Modell = U("HLKS_KI_MODELL"), Aufwand = U("HLKS_KI_AUFWAND"),
        };
        if (double.TryParse(U("HLKS_KI_ZEITLIMIT_MIN"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var min) && min > 0)
            e = e with { Zeitlimit = TimeSpan.FromMinutes(min) };
        return e;
    }
}

public sealed class AuftragDienst
{
    public const string ArtFamilie = "familie_aus_unterlagen";
    const int Behalten = 50;
    static readonly TimeSpan SchrittEreignisAbstand = TimeSpan.FromMilliseconds(400);

    /// <summary>Werkzeuge, die der Lauf «Familie aus Unterlagen» benutzen darf. Bewusst eine Liste: Freigeben, Konflikte
    /// entscheiden, in die Bibliothek speichern, Elemente ändern und HKAP sind nicht dabei (der Kern lehnt den Akteur ki
    /// bei Freigabe und Entscheid zusätzlich ab).
    /// Familienbibliothek (Modul 2, Nachtrag B7, C3): die KI darf die Bibliothek nur LESEN (familien_bibliothek, z. B. um zu sehen, ob es die
    /// Familie schon gibt). Bewusst NICHT in der Liste: familie_aktualisieren, familie_in_bibliothek und familienauswahl_setzen – Versionen
    /// aus der Bibliothek in ein Projekt zu übernehmen (sie stammen oft aus fremden Projekten), eine Fassung als aktuell zu setzen und die
    /// Auswahl der sichtbaren Familien je Projekt entscheidet ein Mensch (Entscheid Christian 19.10.2026: Übernahme nie automatisch).
    /// Das Schreiben ihrer Entwürfe in die Bibliothek besorgt der Kern selbst nach jedem Befehl.</summary>
    public static readonly IReadOnlyList<string> ErlaubteWerkzeuge =
    [
        // Orientierung
        "faehigkeiten", "projekt_ueberblick", "katalog", "warum", "aenderungen",
        // Familien
        "familien", "familie", "anschlusslagen", "familie_ansicht", "familie_entwurf", "familie_aendern", "familie_laden",
        "familie_vergleichen", "familie_masse", "rueckgaengig", "wiederholen",
        "familien_bibliothek",   // Modul 2, C3: Bibliothek nur lesend (Index, Stand, Versionen, Vorschau)
        "leitungsserie",   // Modul 3: Leitungsserie mit Formteilen je Grösse prüfen (nur lesen)
        // Unterlagen
        "unterlagen_lesen", "befunde", "unterlage_bild",
    ];

    /// <summary>Werkzeuge bei «internet» (Entscheid Hauptlauf 18.10.2026): nur Familien und Unterlagen, keine allgemeinen
    /// Projektabfragen – der Weg für Daten nach aussen (Suchtext, Abruf-URL) bleibt klein. Jeder Abruf von Seiten und Dateien
    /// geht über unterlage_aus_url (Sperre interner Adressen, Grössen- und Artgrenzen, Protokoll); dazu die eingebaute
    /// Websuche. Bewusst ohne rueckgaengig, familie_laden, anschlusslagen und faehigkeiten.</summary>
    public static readonly IReadOnlyList<string> InternetWerkzeuge =
    [
        "katalog", "familien", "familie", "familie_ansicht", "familie_masse", "familie_vergleichen", "familie_entwurf", "familie_aendern",
        "familien_bibliothek", "leitungsserie",
        "unterlagen_lesen", "befunde", "unterlage_bild", "unterlage_aus_url",
    ];
    /// <summary>Eingebautes Werkzeug von Claude Code bei «internet»: nur die Websuche. WebFetch ist nicht erlaubt (es lässt sich
    /// nicht auf öffentliche Adressen beschränken); Seiten und Dateien holt unterlage_aus_url.</summary>
    public static readonly IReadOnlyList<string> InternetEingebaut = ["WebSearch"];

    /// <summary>Eine Websuche oder ein Abruf (unterlage_aus_url) im Lauf – aus dem stream-json-Strom. Erfolg bleibt null, bis das Ergebnis da ist.</summary>
    sealed class Abruf(DateTimeOffset zeit, string art, string text) { public DateTimeOffset Zeit { get; } = zeit; public string Art { get; } = art; public string Text { get; } = text; public bool? Erfolg; }

    /// <summary>Aufruf von familie_entwurf oder familie_aendern im Lauf und was daraus wurde (aus stream-json): abgelehnt (angenommen false, fehler, is_error)
    /// oder unverändert («Keine Änderung»). Trockenläufe sind erkennbar und zählen nicht als Änderung.</summary>
    sealed class FamilienAufruf(DateTimeOffset zeit, string werkzeug, string? familie, bool trockenlauf)
    {
        public DateTimeOffset Zeit { get; } = zeit; public string Werkzeug { get; } = werkzeug; public string? Familie = familie; public bool Trockenlauf = trockenlauf;
        public bool Abgelehnt, Unveraendert; public string? Meldung, Vorschlag; public List<string> Reserviert = [];
    }

    sealed class Auftrag(string id, string art, string ort, DateTimeOffset start, TimeSpan zeitlimit)
    {
        public readonly object Sperre = new();
        public string Id { get; } = id;
        public string Art { get; } = art;
        public string Ort { get; } = ort;                       // Projektordner, für den der Auftrag läuft
        public DateTimeOffset Start { get; } = start;
        public DateTimeOffset? Ende;
        public TimeSpan Zeitlimit { get; } = zeitlimit;
        public string Status = AuftragStatus.Wartet;
        public string? Fehler, FehlerArt;
        public JsonObject Eingabe = new();
        public JsonObject? Ergebnis;
        public string LogPfad = "", PromptPfad = "", ArbeitsOrdner = "", McpDatei = "", Prompt = "";
        public string Exe = "";
        public bool Internet = true;
        public List<string> Argumente = [];
        public int? ProzessId;
        public readonly List<(DateTimeOffset Zeit, string Text)> Schritte = [];
        public int SchrittZahl;
        public DateTimeOffset LetzterSchrittEreignis;
        public readonly CancellationTokenSource Abbruch = new();
        public readonly TaskCompletionSource Fertig = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public StreamWriter? Log;
        public readonly object LogSperre = new();
        public JsonObject? ResultEreignis;
        public string? LetzterText;
        public readonly List<Abruf> Abrufe = [];
        public readonly Dictionary<string, Abruf> AbrufNachId = new(StringComparer.Ordinal);
        public readonly List<FamilienAufruf> FamilienAufrufe = [];
        public readonly Dictionary<string, FamilienAufruf> FamilienAufrufNachId = new(StringComparer.Ordinal);
        public readonly StringBuilder StderrEnde = new();
        public string[] Vorher = [];                            // Pakete vor dem Lauf
        public bool McpFehlgeschlagen;
        public IDisposable? Beleg;                              // Modul 3c: Projekt für diesen KI-Lauf belegt (KiLaeufe)
        public string? Warnung;                                 // Sicherheitsprüfung 3c, B13: CLAUDE.md in der Kette des Arbeitsordners
    }

    readonly ProjektDienst projekt;
    readonly AuftragEinstellungen einstellungen;
    readonly KiLaeufe? laeufe;
    readonly object sperre = new();
    readonly List<Auftrag> auftraege = [];

    /// <param name="laeufe">Modul 3c: gemeinsame Belegung «ein KI-Lauf je Projekt» mit den Gesprächen der KI-Palette (null = nur Aufträge unter sich).</param>
    public AuftragDienst(ProjektDienst projekt, AuftragEinstellungen einstellungen, KiLaeufe? laeufe = null)
    {
        this.projekt = projekt;
        this.einstellungen = einstellungen;
        this.laeufe = laeufe;
    }

    // ---------------------------------------------------------------- Zugriff

    public async Task<JsonObject> Starten(JsonObject par)
    {
        var art = Param.Text(par, "art") ?? throw new HlksFehler("Parameter «art» fehlt.", $"Erlaubt: {ArtFamilie}.");
        if (art != ArtFamilie) throw new HlksFehler($"Die Auftragsart «{art}» gibt es nicht.", $"Erlaubt: {ArtFamilie}.");
        if (par["unterlagen"] is not JsonArray lu || lu.Count == 0)
            throw new HlksFehler("Parameter «unterlagen» fehlt oder ist leer.", "Liste aus Kennzeichen (UL-n), Id oder Dateiname im Ordner unterlagen des Projekts angeben.");
        var unterlagen = new List<string>();
        foreach (var x in lu)
            unterlagen.Add(x is JsonValue v && v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s) ? s.Trim()
                : throw new HlksFehler("«unterlagen» muss eine Liste von Texten sein (Kennzeichen, Id oder Dateiname).", "Beispiel: [\"UL-1\", \"Datenblatt.pdf\"]"));
        var kategorie = Leer(Param.Text(par, "kategorie"));
        var hinweis = Leer(Param.Text(par, "hinweis"));
        var familie = Leer(Param.Text(par, "familie"));
        if (hinweis is { Length: > 4000 }) throw new HlksFehler("Der Hinweis ist zu lang (höchstens 4000 Zeichen).");
        var internet = true;                                   // Vorgabe: Recherche im Internet erlaubt
        if (par["internet"] is { } iw)
            internet = iw is JsonValue iv && iv.TryGetValue<bool>(out var ib) ? ib
                : throw new HlksFehler("«internet» muss true oder false sein.", "true: Websuche und Seitenabruf sind erlaubt (Vorgabe); false: der Lauf arbeitet nur mit den Unterlagen.");
        var zeitlimit = einstellungen.Zeitlimit;
        if (par["zeitlimitMinuten"] is { } zl)
        {
            var min = Json.Zahl(zl) ?? throw new HlksFehler("«zeitlimitMinuten» muss eine Zahl sein.");
            if (min < 0.05 || min > 240) throw new HlksFehler("«zeitlimitMinuten» muss zwischen 0.05 und 240 liegen.");
            zeitlimit = TimeSpan.FromMinutes(min);
        }

        var ort = projekt.Ort ?? throw new HlksFehler("Es ist kein Projekt offen.", "Projekt öffnen oder anlegen.");
        var (exe, vorArgs) = ClaudeStarter();
        var (mcpBefehl, mcpVorArgs) = McpStarter();
        var vorlage = VorlageLesen();

        var id = $"{DateTimeOffset.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..4]}";
        var a = new Auftrag(id, art, ort, DateTimeOffset.Now, zeitlimit) { Internet = internet };
        lock (sperre)
        {
            var laeuft = auftraege.FirstOrDefault(x => string.Equals(x.Ort, ort, StringComparison.OrdinalIgnoreCase) && x.Status is AuftragStatus.Wartet or AuftragStatus.Laeuft);
            if (laeuft is not null)
                throw new AuftragHttpFehler(409, $"Für dieses Projekt läuft schon ein KI-Auftrag ({laeuft.Id}).",
                    "Warten, bis er fertig ist, oder ihn abbrechen (POST /api/auftraege/" + laeuft.Id + "/abbrechen).");
            // Modul 3c: auch kein Auftrag, solange im Projekt eine Frage der KI-Palette läuft (409 «Claude arbeitet gerade an …»)
            // Sicherheitsprüfung 3c, B8: auch über Prozessgrenzen (Sperrdatei, gültig bis Zeitlimit + Nachlauf)
            a.Beleg = laeufe?.Belegen(ort, "auftrag", id, $"dem KI-Auftrag {id} (Familie aus Unterlagen)", zeitlimit);
            auftraege.Add(a);
        }
        try
        {
            // Unterlagen und Familie über die Tür des Kerns auflösen (Fehler kommen verständlich zurück)
            var (zeilen, familieText) = await projekt.Mit(p =>
            {
                var l = unterlagen.Select(u => UnterlagenService.Aufloesen(p, u)).ToList();
                string? ft = null;
                if (familie is not null)
                {
                    var f = Familien.Hole(p.Modell, familie);
                    ft = $"{f.Kennzeichen} «{f.Name}» – ihre Definition wird ersetzt (Werkzeug familie liest sie)";
                }
                return (l, ft);
            });
            a.Eingabe = new JsonObject
            {
                ["unterlagen"] = new JsonArray(zeilen.Select(u => (JsonNode)JsonValue.Create(u.Anzeige)).ToArray()),
                ["kategorie"] = kategorie,
                ["hinweis"] = hinweis,
                ["familie"] = familie,
                ["internet"] = internet,
            };
            var unterlagenText = string.Join("\n", zeilen.Select(u => $"  - {u.Anzeige} (Datei «{u.Original}», {u.Art})"));
            a.Prompt = Ausfuellen(Bereich(vorlage, "internet", internet), new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["internet"] = internet ? "erlaubt" : "nicht erlaubt",
                ["unterlagen"] = "\n" + unterlagenText,
                ["kategorie"] = kategorie ?? "nicht vorgegeben – wähle die passende familienfähige Klasse aus dem Katalog (Werkzeug katalog)",
                ["hinweis"] = hinweis ?? "keiner",
                ["familie"] = familieText ?? "keine – lege eine neue Familie an",
                ["schema"] = SchemaLesen(),
            });

            // Protokoll im Projektordner, Arbeitsordner und MCP-Konfiguration je Auftrag
            var logOrdner = Path.Combine(ort, "auftraege");
            Directory.CreateDirectory(logOrdner);
            a.LogPfad = Path.Combine(logOrdner, id + ".log");
            a.PromptPfad = Path.Combine(logOrdner, id + ".prompt.md");
            await File.WriteAllTextAsync(a.PromptPfad, a.Prompt, new UTF8Encoding(false));
            a.Log = new StreamWriter(new FileStream(a.LogPfad, FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true };

            var wurzel = einstellungen.ArbeitsWurzel ?? Path.Combine(Path.GetTempPath(), "HLKS_CAD", "auftraege");
            var jobOrdner = Path.Combine(wurzel, id);
            a.ArbeitsOrdner = Path.Combine(jobOrdner, "arbeit");
            Directory.CreateDirectory(a.ArbeitsOrdner);
            a.McpDatei = Path.Combine(jobOrdner, "mcp.json");
            var mcpArgs = new List<string>(mcpVorArgs)
            {
                "--projekt", ort, "--akteur", einstellungen.Akteur, "--art", "ki",
                "--geraet", projekt.Einstellungen.Geraet, "--katalog", projekt.Einstellungen.KatalogOrdner,
                // Sicherheitsprüfung 3c, B3c: der MCP-Server registriert nur die erlaubten Werkzeuge (mit Internet die Internetliste)
                "--werkzeuge", string.Join(",", (internet ? einstellungen.WerkzeugeInternet : einstellungen.Werkzeuge).Distinct()),
            };
            if (internet)
            {
                // B3b: mit Internet nur Herstellerunterlagen (zweck «familie»), aus dem Internet abgerufene und ausdrücklich die des Auftrags
                // (auch hochgeladene Dateien ohne Element bzw. ohne zweck) – Dateiname in der Ablage, eindeutig je Inhalt
                mcpArgs.Add("--nur-familienunterlagen");
                mcpArgs.Add("--unterlagen-erlaubt");
                mcpArgs.AddRange(zeilen.Select(u => u.Datei).Distinct(StringComparer.OrdinalIgnoreCase));
            }
            var konfig = new JsonObject
            {
                ["mcpServers"] = new JsonObject
                {
                    ["hlks"] = new JsonObject
                    {
                        ["command"] = mcpBefehl,
                        ["args"] = new JsonArray(mcpArgs.Select(x => (JsonNode)JsonValue.Create(x)).ToArray()),
                    },
                },
            };
            await File.WriteAllTextAsync(a.McpDatei, konfig.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = Json.Optionen.Encoder }), new UTF8Encoding(false));

            a.Exe = exe;
            a.Argumente = [.. vorArgs, .. ClaudeArgumente(einstellungen, a.McpDatei, internet)];
            a.Vorher = await Task.Run(() => Speicher(ort).PaketListe().ToArray());
            a.Log.WriteLine(Json.Text(new { type = "hlks", zeit = Jetzt(), text = "Auftrag gestartet", id, art, projekt = ort,
                claude = exe, argumente = a.Argumente, zeitlimitMinuten = zeitlimit.TotalMinutes, eingabe = a.Eingabe, mcpKonfiguration = konfig }));
            // B13: Anweisungsdateien (CLAUDE.md) in der Kette des Arbeitsordners – Warnung im Protokoll und im Ergebnis
            a.Warnung = KiUmgebung.ClaudeMdWarnung(KiUmgebung.ClaudeMdFunde(a.ArbeitsOrdner));
            if (a.Warnung is not null) a.Log.WriteLine(Json.Text(new { type = "hlks", zeit = Jetzt(), text = a.Warnung }));
        }
        catch
        {
            lock (sperre) auftraege.Remove(a);
            a.Beleg?.Dispose();
            a.Log?.Dispose();
            AufraeumenOrdner(a);
            throw;
        }

        _ = Task.Run(() => Laufen(a));
        Melden(a, true);
        return Zustand(a, true);
    }

    public IReadOnlyList<JsonObject> Liste()
    {
        var ort = projekt.Ort;
        List<Auftrag> l;
        lock (sperre) l = auftraege.Where(a => ort is null || string.Equals(a.Ort, ort, StringComparison.OrdinalIgnoreCase)).OrderByDescending(a => a.Start).ToList();
        return l.Select(a => Zustand(a, false)).ToList();
    }

    public JsonObject? Status(string id) => Finde(id) is { } a ? Zustand(a, true) : null;

    /// <summary>Beendet den Auftrag samt Kindprozessen (Claude und MCP-Server) und wartet, bis er beendet ist.</summary>
    public async Task<JsonObject> Abbrechen(string id)
    {
        var a = Finde(id) ?? throw new AuftragHttpFehler(404, $"Einen Auftrag «{id}» gibt es nicht.", "GET /api/auftraege listet die Aufträge.");
        lock (a.Sperre)
            if (a.Status is not (AuftragStatus.Wartet or AuftragStatus.Laeuft))
                throw new AuftragHttpFehler(409, $"Der Auftrag {id} ist schon beendet ({a.Status}).");
        a.Abbruch.Cancel();
        try { await a.Fertig.Task.WaitAsync(TimeSpan.FromSeconds(20)); } catch (TimeoutException) { /* Zustand zeigt, wie weit er ist */ }
        return Zustand(a, true);
    }

    /// <summary>Beim Beenden des Servers: alle laufenden Aufträge abbrechen (keine Waisen: Claude und MCP-Server enden mit).</summary>
    public void BeendenAlle()
    {
        List<Auftrag> laufende;
        lock (sperre) laufende = auftraege.Where(a => a.Status is AuftragStatus.Wartet or AuftragStatus.Laeuft).ToList();
        foreach (var a in laufende) a.Abbruch.Cancel();
        try { Task.WaitAll(laufende.Select(a => a.Fertig.Task).ToArray(), TimeSpan.FromSeconds(15)); } catch { /* beim Beenden egal */ }
    }

    Auftrag? Finde(string id)
    {
        lock (sperre) return auftraege.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    static string? Leer(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    static string Jetzt() => DateTimeOffset.Now.ToString("o");

    // ---------------------------------------------------------------- Zustand und Ereignisse

    JsonObject Zustand(Auftrag a, bool voll)
    {
        lock (a.Sperre)
        {
            var ende = a.Ende ?? DateTimeOffset.Now;
            var letzte = a.Schritte.Skip(Math.Max(0, a.Schritte.Count - einstellungen.SchritteImStatus)).ToList();
            var o = new JsonObject
            {
                ["id"] = a.Id,
                ["art"] = a.Art,
                ["status"] = a.Status,
                ["start"] = a.Start.ToString("o"),
                ["ende"] = a.Ende?.ToString("o"),
                ["dauerSekunden"] = Math.Round((ende - a.Start).TotalSeconds, 1),
                ["zeitlimitMinuten"] = Math.Round(a.Zeitlimit.TotalMinutes, 2),
                ["eingabe"] = a.Eingabe.DeepClone(),
                ["internet"] = a.Internet,
                ["protokoll"] = a.LogPfad,
                ["prozessId"] = a.ProzessId,
                ["schrittAnzahl"] = a.SchrittZahl,
                ["schritte"] = new JsonArray(letzte.Select(s => (JsonNode)new JsonObject { ["zeit"] = s.Zeit.ToString("o"), ["text"] = s.Text }).ToArray()),
                ["fehler"] = a.Fehler,
                ["fehlerart"] = a.FehlerArt,
            };
            if (a.Ergebnis is { } erg)
            {
                var kopie = (JsonObject)erg.DeepClone();
                if (!voll) kopie.Remove("bericht");
                o["ergebnis"] = kopie;
            }
            return o;
        }
    }

    /// <summary>Ereignis «auftrag» an alle Browserfenster. Schritte höchstens alle 400 ms, Beginn und Ende immer.</summary>
    void Melden(Auftrag a, bool immer)
    {
        if (!immer)
        {
            lock (a.Sperre)
            {
                var jetzt = DateTimeOffset.Now;
                if (jetzt - a.LetzterSchrittEreignis < SchrittEreignisAbstand) return;
                a.LetzterSchrittEreignis = jetzt;
            }
        }
        try { projekt.Senden(new JsonObject { ["art"] = "auftrag", ["auftrag"] = Zustand(a, false) }); }
        catch (Exception) { /* Ereignisse sind Komfort, der Lauf darf daran nie scheitern */ }
    }

    void Schritt(Auftrag a, string text)
    {
        lock (a.Sperre)
        {
            a.Schritte.Add((DateTimeOffset.Now, text));
            a.SchrittZahl++;
            if (a.Schritte.Count > 200) a.Schritte.RemoveRange(0, a.Schritte.Count - 200);
        }
        Melden(a, false);
    }

    // ---------------------------------------------------------------- Lauf

    async Task Laufen(Auftrag a)
    {
        string? endeStatus = null, endeFehler = null, fehlerArt = null;
        var beendetNormal = false;
        int exitCode = -1;
        try
        {
            if (a.Abbruch.IsCancellationRequested) { endeStatus = AuftragStatus.Abgebrochen; return; }
            lock (a.Sperre) a.Status = AuftragStatus.Laeuft;
            Melden(a, true);

            var psi = new ProcessStartInfo(a.Exe)
            {
                WorkingDirectory = a.ArbeitsOrdner,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardInputEncoding = new UTF8Encoding(false),
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = new UTF8Encoding(false),
            };
            // Über das Abo wie HKAP: ein gesetzter API-Schlüssel ginge sonst vor. CLAUDECODE (Server aus einer Claude-Sitzung
            // gestartet) und MAX_MCP_OUTPUT_TOKENS wie im KI-Verständnistest (ki-test\ki_test.ps1); kein umgeleitetes Ziel (Sicherheitsprüfung 3c, B12).
            KiUmgebung.Bereinigen(psi.Environment);
            foreach (var arg in a.Argumente) psi.ArgumentList.Add(arg);

            Process? p;
            try { p = Process.Start(psi); }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
            { endeStatus = AuftragStatus.Fehler; endeFehler = "Claude Code liess sich nicht starten: " + ex.Message; fehlerArt = "start"; return; }
            if (p is null) { endeStatus = AuftragStatus.Fehler; endeFehler = "Claude Code liess sich nicht starten."; fehlerArt = "start"; return; }
            using (p)
            {
                lock (a.Sperre) a.ProzessId = p.Id;
                Eigene(a, $"Prozess {p.Id} gestartet");
                Melden(a, true);

                var aus = LeseSchleife(p.StandardOutput, z => Ausgabe(a, z));
                var err = LeseSchleife(p.StandardError, z => Fehlerzeile(a, z));
                try
                {
                    // Der Auftrag geht über die Standardeingabe (als Argument zerfiele ein mehrzeiliger Text).
                    await p.StandardInput.WriteAsync(a.Prompt);
                    p.StandardInput.Close();
                }
                catch (IOException) { /* Claude ist schon beendet: der Ausgang zeigt, warum */ }

                using var frist = new CancellationTokenSource(a.Zeitlimit);
                using var verbund = CancellationTokenSource.CreateLinkedTokenSource(a.Abbruch.Token, frist.Token);
                try
                {
                    await p.WaitForExitAsync(verbund.Token);
                    beendetNormal = true;
                }
                catch (OperationCanceledException)
                {
                    if (a.Abbruch.IsCancellationRequested) { endeStatus = AuftragStatus.Abgebrochen; endeFehler = "Vom Benutzer abgebrochen."; fehlerArt = "abgebrochen"; }
                    else { endeStatus = AuftragStatus.Fehler; endeFehler = $"Zeitlimit von {a.Zeitlimit.TotalMinutes:0.##} Minuten erreicht – der Lauf wurde beendet."; fehlerArt = "zeitlimit"; }
                    Beenden(p);
                }
                try { await Task.WhenAll(aus, err).WaitAsync(einstellungen.AusgabeNachlauf); } catch (TimeoutException) { }
                if (p.HasExited) exitCode = p.ExitCode;
                Eigene(a, $"Prozess beendet, Code {exitCode}");
            }

            if (beendetNormal && endeStatus is null)
            {
                (endeStatus, endeFehler, fehlerArt) = Auswerten(a, exitCode);
            }
        }
        catch (Exception ex)
        {
            endeStatus = AuftragStatus.Fehler; endeFehler = "Der Lauf scheiterte: " + ex.Message; fehlerArt = "intern";
            Eigene(a, endeFehler);
        }
        finally
        {
            await Abschliessen(a, endeStatus ?? AuftragStatus.Fehler, endeFehler, fehlerArt);
        }
    }

    /// <summary>Ausgang eines Laufs, der von selbst endete: fertig, wenn Claude mit Code 0 endete und einen Bericht lieferte.</summary>
    (string Status, string? Fehler, string? Art) Auswerten(Auftrag a, int exitCode)
    {
        var r = a.ResultEreignis;
        var istFehler = r?["is_error"] is JsonValue v && v.TryGetValue<bool>(out var b) && b;
        var text = T(r?["result"]);
        if (exitCode == 0 && r is not null && !istFehler) return (AuftragStatus.Fertig, null, null);
        if (exitCode == 0 && r is null && !string.IsNullOrWhiteSpace(a.LetzterText)) return (AuftragStatus.Fertig, null, null);   // ohne Abschlusszeile: letzte Antwort gilt als Bericht
        string roh;
        lock (a.Sperre) roh = istFehler && !string.IsNullOrWhiteSpace(text) ? text! : a.StderrEnde.Length > 0 ? a.StderrEnde.ToString().Trim() : text ?? "";
        if (roh.Contains("login", StringComparison.OrdinalIgnoreCase) || roh.Contains("authenticat", StringComparison.OrdinalIgnoreCase))
            return (AuftragStatus.Fehler, "Claude ist nicht angemeldet – unter «Heute» mit «Claude anmelden» neu anmelden.", "anmeldung");
        if (a.McpFehlgeschlagen) return (AuftragStatus.Fehler, "Der MCP-Server «hlks» liess sich nicht starten. " + Kurz(roh, 300), "mcp");
        if (roh.Length == 0) roh = exitCode == 0 ? "Claude lieferte keinen Bericht." : $"Claude Code endete mit Code {exitCode}.";
        return (AuftragStatus.Fehler, Kurz(roh, 600), "claude");
    }

    async Task Abschliessen(Auftrag a, string status, string? fehler, string? fehlerArt)
    {
        JsonObject ergebnis;
        try { ergebnis = await ErgebnisErmitteln(a); }
        catch (Exception ex) { ergebnis = new JsonObject { ["warnung"] = "Das Ergebnis konnte nicht ermittelt werden: " + ex.Message }; }
        lock (a.Sperre)
        {
            a.Ergebnis = ergebnis;
            a.Status = status;
            a.Fehler = fehler;
            a.FehlerArt = fehlerArt;
            a.Ende = DateTimeOffset.Now;
        }
        Eigene(a, $"Auftrag beendet: {status}" + (fehler is null ? "" : " – " + fehler));
        lock (a.LogSperre) { try { a.Log?.Dispose(); } catch (IOException) { } a.Log = null; }
        a.Beleg?.Dispose();   // Modul 3c: Projekt wieder frei für Gespräche der KI-Palette
        AufraeumenOrdner(a);
        Melden(a, true);
        a.Fertig.TrySetResult();
        // alte Aufträge vergessen
        lock (sperre)
            while (auftraege.Count > Behalten && auftraege.FirstOrDefault(x => x.Status is not (AuftragStatus.Wartet or AuftragStatus.Laeuft)) is { } alt)
                auftraege.Remove(alt);
    }

    static void AufraeumenOrdner(Auftrag a)
    {
        if (a.ArbeitsOrdner.Length == 0) return;
        var ordner = Path.GetDirectoryName(a.ArbeitsOrdner)!;
        for (int i = 0; i < 5; i++)
        {
            try { if (Directory.Exists(ordner)) Directory.Delete(ordner, true); return; }
            catch (IOException) { Thread.Sleep(200); }
            catch (UnauthorizedAccessException) { Thread.Sleep(200); }
        }
    }

    /// <summary>Claude samt allen Kindprozessen (MCP-Server) beenden.</summary>
    static void Beenden(Process p)
    {
        try { p.Kill(true); } catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { }
        try { p.WaitForExit(5000); } catch (Exception) { }
    }

    static Task LeseSchleife(StreamReader r, Action<string> zeile) => Task.Run(async () =>
    {
        try { while (await r.ReadLineAsync() is { } z) if (z.Length > 0) zeile(z); }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException) { }
    });

    // ---------------------------------------------------------------- Ausgabe von Claude

    void LogZeile(Auftrag a, string zeile)
    {
        lock (a.LogSperre)
        {
            try { a.Log?.WriteLine(zeile); } catch (Exception ex) when (ex is IOException or ObjectDisposedException) { }
        }
    }

    void Eigene(Auftrag a, string text) => LogZeile(a, Json.Text(new { type = "hlks", zeit = Jetzt(), text }));

    void Fehlerzeile(Auftrag a, string zeile)
    {
        LogZeile(a, Json.Text(new { type = "stderr", zeit = Jetzt(), text = zeile }));
        lock (a.Sperre)
        {
            a.StderrEnde.AppendLine(zeile);
            if (a.StderrEnde.Length > 4000) a.StderrEnde.Remove(0, a.StderrEnde.Length - 4000);
        }
    }

    static bool IstWeb(string? s) => s is not null && (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
    static string? T(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
    static string Kurz(string s, int max) => s.Length <= max ? s : s[..max] + "…";
    static string Einzeilig(string s) => Regex.Replace(s, @"\s+", " ").Trim();
    static string Kurzname(string? werkzeug) => (werkzeug ?? "?").Replace("mcp__hlks__", "");

    /// <summary>Eine Zeile der Ausgabe (stream-json): ins Protokoll, Schritte und Bericht herausziehen.</summary>
    void Ausgabe(Auftrag a, string zeile)
    {
        LogZeile(a, zeile);
        JsonObject? o;
        try { o = JsonNode.Parse(zeile) as JsonObject; } catch (JsonException) { return; }   // Textausgabe: nur ins Protokoll
        if (o is null) return;
        switch (T(o["type"]))
        {
            case "system" when T(o["subtype"]) == "init":
                if (o["mcp_servers"] is JsonArray ms)
                    foreach (var s in ms.OfType<JsonObject>().Where(s => T(s["name"]) == "hlks"))
                    {
                        var st = T(s["status"]);
                        Schritt(a, $"MCP-Server hlks: {st ?? "?"}");
                        if (st is "failed" or "error") { a.McpFehlgeschlagen = true; }
                    }
                break;
            case "assistant":
                foreach (var b in (o["message"]?["content"] as JsonArray ?? []).OfType<JsonObject>())
                {
                    switch (T(b["type"]))
                    {
                        case "text" when T(b["text"]) is { Length: > 0 } t:
                            lock (a.Sperre) a.LetzterText = t;
                            Schritt(a, "KI: " + Kurz(Einzeilig(t), 200));
                            break;
                        case "tool_use":
                            Schritt(a, Kurzname(T(b["name"])) + " " + Kurz(b["input"]?.ToJsonString(Json.Optionen) ?? "", 140));
                            AbrufMerken(a, T(b["id"]), T(b["name"]), b["input"] as JsonObject);
                            FamilienAufrufMerken(a, T(b["id"]), T(b["name"]), b["input"] as JsonObject);
                            break;
                    }
                }
                break;
            case "user":
                foreach (var b in (o["message"]?["content"] as JsonArray ?? []).OfType<JsonObject>().Where(b => T(b["type"]) == "tool_result"))
                {
                    var inhalt = KiUmgebung.Werkzeuginhalt(b["content"]);   // Sicherheitsprüfung 3c, B15: ohne Kopfzeile auswerten
                    var istFehler = b["is_error"] is JsonValue v && v.TryGetValue<bool>(out var fehler) && fehler;
                    if (istFehler) Schritt(a, "Werkzeug meldet Fehler: " + Kurz(Einzeilig(inhalt), 200));
                    AbrufErgebnis(a, T(b["tool_use_id"]), !istFehler && !MeldetFehler(inhalt), inhalt);
                    FamilienAufrufErgebnis(a, T(b["tool_use_id"]), istFehler, inhalt);
                }
                break;
            case "result":
                lock (a.Sperre) a.ResultEreignis = (JsonObject)o.DeepClone();
                break;
        }
    }

    /// <summary>Websuche (WebSearch) und Abruf (unterlage_aus_url) als eigene Protokollzeile merken: wer fragt was, wann.</summary>
    void AbrufMerken(Auftrag a, string? toolId, string? werkzeug, JsonObject? eingabe)
    {
        var name = Kurzname(werkzeug);
        string art, text;
        if (name == "WebSearch") { art = "suche"; text = T(eingabe?["query"]) ?? eingabe?.ToJsonString(Json.Optionen) ?? ""; }
        else if (name == "unterlage_aus_url") { art = "abruf"; text = T(eingabe?["url"]) ?? eingabe?.ToJsonString(Json.Optionen) ?? ""; }
        else return;
        var ab = new Abruf(DateTimeOffset.Now, art, text);
        lock (a.Sperre)
        {
            a.Abrufe.Add(ab);
            if (toolId is not null) a.AbrufNachId[toolId] = ab;
        }
        LogZeile(a, Json.Text(new { type = "hlks", zeit = ab.Zeit.ToString("o"), text = (art == "suche" ? "Websuche: " : "Abruf: ") + text, abruf = new { art, text } }));
    }

    void AbrufErgebnis(Auftrag a, string? toolId, bool erfolg, string inhalt)
    {
        Abruf? ab;
        lock (a.Sperre)
        {
            if (toolId is null || !a.AbrufNachId.TryGetValue(toolId, out ab)) return;
            ab.Erfolg = erfolg;
        }
        LogZeile(a, Json.Text(new { type = "hlks", zeit = Jetzt(), text = (ab.Art == "suche" ? "Websuche " : "Abruf ") + (erfolg ? "erfolgreich: " : "fehlgeschlagen: ") + ab.Text,
            abruf = new { art = ab.Art, text = ab.Text, erfolg, meldung = erfolg ? null : Kurz(Einzeilig(inhalt), 300) } }));
    }

    /// <summary>Antwort eines Kern-Werkzeugs, die einen Fehler meldet: JSON mit «fehler» oder angenommen = false.</summary>
    static bool MeldetFehler(string inhalt)
    {
        try { return JsonNode.Parse(inhalt) is JsonObject o && (o["fehler"] is not null || (o["angenommen"] is JsonValue v && v.TryGetValue<bool>(out var b) && !b)); }
        catch (JsonException) { return false; }
    }

    /// <summary>Aufrufe von familie_entwurf und familie_aendern merken, um ihr Ergebnis auszuwerten.</summary>
    void FamilienAufrufMerken(Auftrag a, string? toolId, string? werkzeug, JsonObject? eingabe)
    {
        var name = Kurzname(werkzeug);
        if (toolId is null || name is not ("familie_entwurf" or "familie_aendern")) return;
        var trocken = eingabe?["trockenlauf"] is JsonValue v && v.TryGetValue<bool>(out var b) && b;
        var f = new FamilienAufruf(DateTimeOffset.Now, name, Leer(T(eingabe?["familie"])), trocken);
        lock (a.Sperre) { a.FamilienAufrufe.Add(f); a.FamilienAufrufNachId[toolId] = f; }
    }

    /// <summary>Ergebnis eines Familienaufrufs: abgelehnt (is_error, «fehler», angenommen false – z. B. Familie im Editor reserviert)
    /// oder «Keine Änderung» (gleiche Definition).</summary>
    void FamilienAufrufErgebnis(Auftrag a, string? toolId, bool istFehler, string inhalt)
    {
        FamilienAufruf? f;
        lock (a.Sperre) { if (toolId is null || !a.FamilienAufrufNachId.TryGetValue(toolId, out f)) return; }
        JsonObject? o = null;
        try { o = JsonNode.Parse(inhalt) as JsonObject; } catch (JsonException) { }
        var angenommen = o?["angenommen"] is JsonValue av && av.TryGetValue<bool>(out var ab) ? ab : (bool?)null;
        var abgelehnt = istFehler || (o is not null && (o["fehler"] is not null || angenommen == false));
        if (o?["trockenlauf"] is JsonValue tv && tv.TryGetValue<bool>(out var tb)) f.Trockenlauf = tb;
        if (abgelehnt)
        {
            var reserviert = (o?["gesperrt"] as JsonArray ?? []).OfType<JsonObject>()
                .Select(g => $"{T(g["akteur"])}@{T(g["geraet"])}" + (DateTimeOffset.TryParse(T(g["bis"]), out var bis) ? $" bis {bis.ToLocalTime():HH:mm}" : "")).ToList();
            var kern = T(o?["meldung"]) is { Length: > 0 } m ? (m.StartsWith("Abgelehnt: ") ? m["Abgelehnt: ".Length..] : m) : null;
            var meldung = T(o?["fehler"]) is { Length: > 0 } fe && reserviert.Count == 0 ? fe : kern ?? Kurz(Einzeilig(inhalt), 300);
            if (reserviert.Count > 0) meldung = $"Familie reserviert von {string.Join(", ", reserviert)} ({kern ?? T(o?["fehler"])})";
            lock (a.Sperre) { f.Abgelehnt = true; f.Meldung = meldung; f.Vorschlag = T(o?["vorschlag"]); f.Reserviert = reserviert; }
            Eigene(a, $"Änderung abgelehnt ({f.Werkzeug}{(f.Familie is null ? "" : ", " + f.Familie)}): {meldung}");
        }
        else if (angenommen == true && !f.Trockenlauf && (o?["hinweise"] as JsonArray ?? []).Any(h => T(h)?.StartsWith("Keine Änderung", StringComparison.Ordinal) == true))
        {
            lock (a.Sperre) f.Unveraendert = true;
            Eigene(a, $"Keine Änderung ({f.Werkzeug}{(f.Familie is null ? "" : ", " + f.Familie)}): die Definition ist gleich.");
        }
    }

    // ---------------------------------------------------------------- Ergebnis

    IProjektSpeicher Speicher(string ort) => projekt.Einstellungen.SpeicherFabrik?.Invoke(ort) ?? new OrdnerSpeicher(ort);

    /// <summary>Bericht (letzte Antwort von Claude) und die Familien, die neue Pakete des Akteurs KI angelegt oder geändert haben.</summary>
    async Task<JsonObject> ErgebnisErmitteln(Auftrag a)
    {
        var erg = new JsonObject();
        string? bericht;
        JsonObject? r;
        lock (a.Sperre) { r = a.ResultEreignis; bericht = a.LetzterText; }
        if (r is not null && T(r["result"]) is { Length: > 0 } rt) bericht = rt;
        erg["bericht"] = bericht;
        if (r is not null)
        {
            erg["runden"] = r["num_turns"]?.DeepClone();
            erg["kostenUsd"] = r["total_cost_usd"]?.DeepClone();
            erg["dauerClaudeMs"] = r["duration_ms"]?.DeepClone();
        }

        var neue = await Task.Run(() =>
        {
            var sp = Speicher(a.Ort);
            var vorher = a.Vorher.ToHashSet(StringComparer.Ordinal);
            var liste = new List<Paket>();
            foreach (var id in sp.PaketListe().Where(id => !vorher.Contains(id)))
            {
                try
                {
                    var paket = sp.PaketLesen(id);
                    if (paket.AkteurArt == "ki" && string.Equals(paket.Akteur, einstellungen.Akteur, StringComparison.OrdinalIgnoreCase)) liste.Add(paket);
                }
                catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { }
            }
            return liste.OrderBy(x => x.Id, StringComparer.Ordinal).ToList();
        });
        erg["pakete"] = neue.Count;
        erg["befehle"] = new JsonObject(neue.GroupBy(x => x.Befehl).Select(g => KeyValuePair.Create(g.Key, (JsonNode?)JsonValue.Create(g.Count()))));

        // Familien: Endzustand je Familie aus der Reihenfolge der Pakete
        var familien = new Dictionary<string, (string? Aktion, Element? Element, HashSet<string>? Pakete)>(StringComparer.Ordinal);
        var andere = new HashSet<string>(StringComparer.Ordinal);
        var abgerufen = new Dictionary<string, (Element Unterlage, DateTimeOffset Zeit)>(StringComparer.Ordinal);
        foreach (var p in neue)
            foreach (var op in p.Operationen)
            {
                var el = op.Element ?? op.ElementVorher;
                if (!op.Art.StartsWith("element", StringComparison.Ordinal)) { andere.Add(op.Id); continue; }
                if (el?.Klasse == "Unterlage" && op.Art == "element.setzen" && op.Element is { } ul && IstWeb(ul.Text("original")))
                {
                    abgerufen[op.Id] = (ul, p.Zeit);        // aus dem Internet abgerufene Herstellerunterlage (unterlage_aus_url)
                    continue;
                }
                if (el?.Klasse != Familien.Klasse) { andere.Add(op.Id); continue; }
                familien.TryGetValue(op.Id, out var bisher);
                var aktion = op.Art == "element.loeschen" ? "geloescht"
                    : bisher.Aktion == "angelegt" || (bisher.Aktion is null && op.Basis == 0 && op.ElementVorher is null) ? "angelegt" : "geaendert";
                (bisher.Pakete ??= new HashSet<string>(StringComparer.Ordinal)).Add(p.Id);
                familien[op.Id] = (aktion, op.Art == "element.loeschen" ? bisher.Element ?? op.ElementVorher : op.Element, bisher.Pakete);
            }

        andere.ExceptWith(abgerufen.Keys);   // das Anlegen der Unterlage selbst ist erwartet

        // Familienaufrufe aus dem Strom: Namen auflösen; «Keine Änderung» -> Familie mit Aktion unveraendert (sofern keine Pakete sie berührten)
        FamilienAufruf[] aufrufe;
        lock (a.Sperre) aufrufe = a.FamilienAufrufe.ToArray();
        var aufgeloest = new Dictionary<string, Element>(StringComparer.OrdinalIgnoreCase);
        if (aufrufe.Any(x => x.Familie is not null && (x.Unveraendert || x.Abgelehnt)))
            try
            {
                await projekt.Mit(pr =>
                {
                    foreach (var name in aufrufe.Where(x => x.Familie is not null).Select(x => x.Familie!).Distinct(StringComparer.OrdinalIgnoreCase))
                        try { aufgeloest[name] = Familien.Hole(pr.Modell, name).Kopie(); } catch (HlksFehler) { }
                    return 0;
                });
            }
            catch (HlksFehler) { }   // Projekt inzwischen geschlossen
        foreach (var u in aufrufe.Where(x => x.Unveraendert && x.Familie is not null))
            if (aufgeloest.TryGetValue(u.Familie!, out var uf) && !familien.ContainsKey(uf.Id))
                familien[uf.Id] = ("unveraendert", uf, new HashSet<string>(StringComparer.Ordinal));

        // Der Server sieht die Pakete über den Abgleich: kurz nachhelfen, bis die Familien im Modell sind
        var imModell = new Dictionary<string, Element?>(StringComparer.Ordinal);
        for (int versuch = 0; versuch < 6; versuch++)
        {
            try { imModell = await projekt.Mit(pr => familien.Keys.ToDictionary(id => id, id => pr.Modell.Finde(id)?.Kopie(), StringComparer.Ordinal)); }
            catch (Exception ex) when (ex is HlksFehler) { break; }   // Projekt inzwischen geschlossen
            if (familien.All(f => f.Value.Aktion == "geloescht" || imModell.GetValueOrDefault(f.Key) is not null)) break;
            try { await projekt.Abgleichen(); } catch (Exception) { }
            await Task.Delay(300);
        }

        var fl = new JsonArray();
        foreach (var (id, f) in familien)
        {
            var el = imModell.GetValueOrDefault(id) ?? f.Element;
            var o = new JsonObject { ["id"] = id, ["kennzeichen"] = el?.Kennzeichen, ["aktion"] = f.Aktion, ["pakete"] = f.Pakete?.Count ?? 0 };
            if (el is not null)
            {
                foreach (var (k, v) in Familien.Zusammenfassung(el)) if (k != "zusammenfassung") o[k] = v?.DeepClone();
                var konflikte = (el.Eigenschaft("definition")?.Inhalt as JsonObject)?["konflikte"] as JsonArray;
                o["konflikte"] = konflikte?.Count ?? 0;
                o["offeneKonflikte"] = konflikte?.OfType<JsonObject>().Count(k => k["entscheid"] is null) ?? 0;
            }
            o["imModell"] = f.Aktion == "geloescht" ? imModell.GetValueOrDefault(id) is null : imModell.GetValueOrDefault(id) is not null;
            fl.Add(o);
        }
        erg["familien"] = fl;

        // Aus dem Internet abgerufene Unterlagen: Kennzeichen, URL, Art, Grösse (Datei im Ordner unterlagen), Zeitpunkt, Prüfsumme
        var ul2 = new JsonArray();
        foreach (var (id, u) in abgerufen)
        {
            long? groesse = null;
            try { if (u.Unterlage.Text("datei") is { Length: > 0 } d && Path.Combine(a.Ort, "unterlagen", Path.GetFileName(d)) is var f && File.Exists(f)) groesse = new FileInfo(f).Length; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            ul2.Add(new JsonObject
            {
                ["id"] = id, ["kennzeichen"] = u.Unterlage.Kennzeichen, ["url"] = u.Unterlage.Text("original"), ["art"] = u.Unterlage.Text("art"),
                ["groesse"] = groesse, ["datei"] = u.Unterlage.Text("datei"), ["pruefsumme"] = u.Unterlage.Text("stand"),
                ["abgerufen"] = u.Unterlage.Text("abgerufen") ?? u.Zeit.ToString("o"),
            });
        }
        erg["internet"] = a.Internet;
        lock (a.Sperre)
            erg["abrufe"] = new JsonArray(a.Abrufe.Select(x => (JsonNode)new JsonObject
            {
                ["zeit"] = x.Zeit.ToString("o"), ["art"] = x.Art, [x.Art == "abruf" ? "url" : "text"] = x.Text, ["erfolg"] = x.Erfolg,
            }).ToArray());
        erg["abgerufeneUnterlagen"] = ul2;

        // Abgelehnte Änderungen an Familien (Werkzeug, Familie, Meldung); Trockenläufe stehen mit drin, sind aber gekennzeichnet
        string? Bezeichnung(FamilienAufruf x) => x.Familie is { } n ? (aufgeloest.TryGetValue(n, out var fe) ? fe.Kennzeichen : n)
            : Regex.Match(x.Meldung ?? "", @"FAM-\d+") is { Success: true } mm ? mm.Value : null;
        var abgelehnt = aufrufe.Where(x => x.Abgelehnt).ToList();
        erg["abgelehnt"] = new JsonArray(abgelehnt.Select(x => (JsonNode)new JsonObject
        {
            ["zeit"] = x.Zeit.ToString("o"), ["werkzeug"] = x.Werkzeug, ["familie"] = Bezeichnung(x), ["meldung"] = x.Meldung,
            ["vorschlag"] = x.Vorschlag, ["trockenlauf"] = x.Trockenlauf,
        }).ToArray());

        var warnungen = new List<string>();
        if (a.Warnung is not null) warnungen.Add(a.Warnung);   // Sicherheitsprüfung 3c, B13
        if (abgelehnt.Count > 0 && !familien.Values.Any(f => f.Aktion is "angelegt" or "geaendert"))
        {
            var x = abgelehnt.LastOrDefault(y => !y.Trockenlauf) ?? abgelehnt[^1];
            warnungen.Add($"Änderung an {Bezeichnung(x) ?? "einer Familie"} abgelehnt: {x.Meldung}");
        }
        if (andere.Count > 0)
            warnungen.Add($"Die KI hat ausser Familien {andere.Count} weitere Element(e) oder Verbindungen geändert – bitte prüfen (Abfrage aenderungen, Akteur {einstellungen.Akteur}).");
        if (warnungen.Count > 0) erg["warnung"] = string.Join(" ", warnungen);
        return erg;
    }

    // ---------------------------------------------------------------- Aufruf von Claude

    /// <summary>Schalter von Claude Code (alle in «claude --help» der Fassung 2.1.299): -p (Auftrag über Standardeingabe),
    /// --output-format stream-json (mit --verbose), --mcp-config + --strict-mcp-config (nur der Server hlks), --tools ""
    /// (keine eingebauten Werkzeuge), --allowedTools (die MCP-Werkzeuge der Liste), --permission-mode dontAsk (alles andere
    /// wird abgelehnt statt gefragt), --no-session-persistence, --disable-slash-commands, --setting-sources local (keine Benutzereinstellungen: Plugins, Hooks,
    /// Berechtigungsregeln; die Anmeldung bleibt), optional --model und --effort.</summary>
    public static List<string> ClaudeArgumente(AuftragEinstellungen e, string mcpDatei, bool internet = true)
    {
        // Mit Internet: WebSearch (eingebaut) und die engere MCP-Liste mit unterlage_aus_url; sonst kein eingebautes Werkzeug
        var eingebaut = internet ? InternetEingebaut : [];
        var mcp = (internet ? e.WerkzeugeInternet : e.Werkzeuge).Distinct().Select(w => "mcp__hlks__" + w);
        var a = new List<string>
        {
            "-p", "--output-format", "stream-json", "--verbose",
            "--mcp-config", mcpDatei, "--strict-mcp-config",
            "--tools", string.Join(",", eingebaut),
            "--allowedTools", string.Join(",", eingebaut.Concat(mcp)),
            "--permission-mode", "dontAsk",
            "--no-session-persistence", "--disable-slash-commands",
            "--setting-sources", "local",
        };
        if (!string.IsNullOrWhiteSpace(e.Modell)) { a.Add("--model"); a.Add(e.Modell); }
        if (!string.IsNullOrWhiteSpace(e.Aufwand)) { a.Add("--effort"); a.Add(e.Aufwand); }
        return a;
    }

    (string Exe, IReadOnlyList<string> Vor) ClaudeStarter()
    {
        var exe = einstellungen.ClaudeExe ?? FindeExe();
        if (exe is null || !File.Exists(exe))
            throw new HlksFehler("Claude Code wurde nicht gefunden – ist die Claude-App installiert?", "Die Claude-App installieren oder den Pfad der claude.exe in HLKS_CLAUDE_EXE setzen.");
        return (exe, einstellungen.ClaudeVorArgumente);
    }

    /// <summary>Kopie von ClaudeCode.FindeExe (HKAP): die neueste claude.exe aus dem Ordner der Claude-App. Seit dem
    /// App-Update vom 02.10.2026 liegt sie eine Ebene tiefer (claude-code\&lt;Version&gt;\&lt;Kennung&gt;\claude.exe statt
    /// claude-code\&lt;Version&gt;\claude.exe); beide Formen. Die Version steht im ersten Ordner unter der Wurzel, bei
    /// gleicher Version gewinnt die jüngere Datei.</summary>
    public static string? FindeExe()
    {
        var wurzeln = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Claude", "claude-code"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "Packages", "Claude_pzs8sxrjxfjjc", "LocalCache", "Roaming", "Claude", "claude-code"),
        };
        static string[] Ordner(string pfad) { try { return Directory.GetDirectories(pfad); } catch { return []; } }
        return wurzeln.Where(Directory.Exists)
            .SelectMany(Ordner)
            .SelectMany(v => new[] { Path.Combine(v, "claude.exe") }
                .Concat(Ordner(v).Select(u => Path.Combine(u, "claude.exe")))
                .Where(File.Exists)
                .Select(exe => (exe, version: Version.TryParse(Path.GetFileName(v), out var ver) ? ver : new Version(0, 0))))
            .OrderByDescending(x => x.version)
            .ThenByDescending(x => File.GetLastWriteTimeUtc(x.exe))
            .Select(x => x.exe)
            .FirstOrDefault();
    }

    /// <summary>Start des MCP-Servers: dotnet + Hlks.Mcp.dll (wie im KI-Verständnistest).</summary>
    (string Befehl, IReadOnlyList<string> VorArgs) McpStarter()
    {
        var dll = FindeMcpDll() ?? throw new HlksFehler("Hlks.Mcp.dll (der MCP-Server) wurde nicht gefunden.",
            "Hlks.Mcp bauen (dotnet build src\\Hlks.Mcp) oder den Pfad der Hlks.Mcp.dll in HLKS_MCP_DLL setzen.");
        return (DotnetPfad(), [dll]);
    }

    string DotnetPfad()
    {
        if (einstellungen.DotnetPfad is { Length: > 0 } s) return s;
        if (Environment.ProcessPath is { } pp && Path.GetFileNameWithoutExtension(pp).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) return pp;
        if (Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } h && File.Exists(h)) return h;
        var standard = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe");
        return File.Exists(standard) ? standard : "dotnet";
    }

    string? FindeMcpDll()
    {
        static bool Ok(string? d) => d is not null && File.Exists(d);
        if (Ok(einstellungen.McpDll)) return einstellungen.McpDll;
        if (Environment.GetEnvironmentVariable("HLKS_MCP_DLL") is { Length: > 0 } env && Ok(env)) return env;
        var neben = Path.Combine(AppContext.BaseDirectory, "Hlks.Mcp.dll");
        if (File.Exists(neben) && File.Exists(Path.ChangeExtension(neben, ".runtimeconfig.json"))) return neben;
        // Ausgabe des Baus: …\<Projekt>\bin\<Konfiguration>\<net10.0>\ -> …\Hlks.Mcp\bin\<Konfiguration>\<net10.0>\Hlks.Mcp.dll
        try
        {
            var tfm = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd('\\', '/'));
            var konfig = tfm.Parent!;
            var wurzel = konfig.Parent!.Parent!.Parent!;
            foreach (var k in new[] { konfig.Name, "Debug", "Release" })
            {
                var kandidat = Path.Combine(wurzel.FullName, "Hlks.Mcp", "bin", k, tfm.Name, "Hlks.Mcp.dll");
                if (File.Exists(kandidat)) return kandidat;
            }
        }
        catch (NullReferenceException) { }
        return null;
    }

    // ---------------------------------------------------------------- Vorlage

    string VorlageLesen()
    {
        var datei = FindeDatei("ki", "familie_aus_unterlagen.md", einstellungen.VorlagenOrdner)
            ?? throw new HlksFehler("Die Vorlage ki/familie_aus_unterlagen.md wurde nicht gefunden.", "Umgebungsvariable HLKS_KI auf den Ordner HLKS_CAD/ki setzen.");
        return File.ReadAllText(datei, Encoding.UTF8);
    }

    string SchemaLesen()
    {
        var datei = einstellungen.SchemaDatei is { } s && File.Exists(s) ? s : FindeDatei("schema", "hlks-familie.schema.json", null);
        if (datei is null) return "(Die Schemadatei wurde nicht gefunden. Das Format steht in der Beschreibung des Werkzeugs familie_entwurf; faehigkeiten nennt die Parameter.)";
        // einzeilig statt eingerückt: spart Platz im Auftrag
        try { return JsonNode.Parse(File.ReadAllText(datei, Encoding.UTF8))!.ToJsonString(Json.Optionen); }
        catch (JsonException) { return File.ReadAllText(datei, Encoding.UTF8); }
    }

    /// <summary>Datei in einem der Ordner: ausdrücklich genannt, Umgebung HLKS_KI (nur ki), Quellordner (über die Metadaten des
    /// Baus), aufwärts vom Programm und vom Arbeitsverzeichnis, bekannter OneDrive-Pfad.</summary>
    static string? FindeDatei(string unterordner, string name, string? ausdruecklich)
    {
        var kandidaten = new List<string>();
        if (ausdruecklich is not null) kandidaten.Add(Path.Combine(ausdruecklich, name));
        if (unterordner == "ki" && Environment.GetEnvironmentVariable("HLKS_KI") is { Length: > 0 } env) kandidaten.Add(Path.Combine(env, name));
        var quelle = typeof(AuftragDienst).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
            .Cast<System.Reflection.AssemblyMetadataAttribute>().FirstOrDefault(m => m.Key == "QuellWebRoot")?.Value;
        if (quelle is not null)
            try { kandidaten.Add(Path.Combine(Path.GetFullPath(Path.Combine(quelle, "..", "..", "..")), unterordner, name)); } catch (ArgumentException) { }
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
            for (var d = new DirectoryInfo(start); d is not null; d = d.Parent)
            {
                kandidaten.Add(Path.Combine(d.FullName, unterordner, name));
                kandidaten.Add(Path.Combine(d.FullName, "HLKS_CAD", unterordner, name));
            }
        kandidaten.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive", "Claude_Code", "HLKS_CAD", unterordner, name));
        return kandidaten.FirstOrDefault(File.Exists);
    }

    static readonly Regex Platzhalter = new(@"\{\{\s*(\w+)\s*\}\}", RegexOptions.Compiled);

    /// <summary>Platzhalter {{name}} in einem Durchgang ersetzen (eingesetzter Text wird nicht noch einmal durchsucht).</summary>
    public static string Ausfuellen(string vorlage, IReadOnlyDictionary<string, string> werte) =>
        Platzhalter.Replace(vorlage, m => werte.TryGetValue(m.Groups[1].Value, out var w) ? w : m.Value);

    /// <summary>Bereich der Vorlage zwischen &lt;!--name--&gt; und &lt;!--/name--&gt;: bleibt (ohne die Markierungen), wenn
    /// <paramref name="behalten"/>, sonst entfällt er samt Markierungen. Vor dem Einsetzen der Platzhalter anwenden.</summary>
    public static string Bereich(string vorlage, string name, bool behalten) =>
        Regex.Replace(vorlage, $@"<!--{Regex.Escape(name)}-->\r?\n?(.*?)<!--/{Regex.Escape(name)}-->\r?\n?", m => behalten ? m.Groups[1].Value : "", RegexOptions.Singleline);
}
