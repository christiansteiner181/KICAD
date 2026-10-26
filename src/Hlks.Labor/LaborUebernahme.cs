// Übernahme der bisherigen Labor-Daten (XR-Labor L1; Entscheid Christian 26.10.2026: «Übernehmen»): kopiert – nie verschiebt, nie löscht – was KICAD bisher je PC
// gesammelt hat, in die Ablage des Labors (LaborAblage.cs):
//   %LOCALAPPDATA%\HLKS_CAD\xr\messprotokolle\*.jsonl  →  labor\messungen\   (Rechnername in den Dateinamen: JJJJMMTT-hhmmss_<rechner>_<teilnehmer>.jsonl)
//   %LOCALAPPDATA%\HLKS_CAD\xr\fragebogen\*.json       →  labor\fragebogen\  (JJJJMMTT-hhmmss_<rechner>_<name>.json)
//   Benutzereinstellungen labor.profil.<kennung>        →  labor\profile\<kennung>.json    (nur gelesen über BenutzerEinstellungen)
//   Benutzereinstellung  labor.profil_aktiv             →  labor\aktiv.json                (nur, wenn im Labor noch kein aktives Profil gilt)
// Wann: einmal beim ersten Start des Labors (kein Merker uebernahme_<rechner>.json) und auf «Erneut übernehmen» (POST /api/labor/uebernahme).
// Doppelte: Eine Datei mit gleichem Inhalt (SHA-256) wie eine schon vorhandene im Ziel wird übersprungen. Wuchs eine Messdatei seit der ersten Übernahme (die Quelle beginnt mit dem
// Inhalt der Kopie), wird die Kopie durch die längere ersetzt (zählt als «aktualisiert»); sonst entsteht eine zweite Datei mit Nummer. Ein Profil, das es im Labor schon mit anderem
// Inhalt gibt, bleibt unverändert («abweichend»): im Labor Geändertes geht nie verloren.
// Merker uebernahme_<rechner>.json: { fassung, rechner, zeit, lesenErlaubt, quellen, messungen{…}, fragebogen{…}, profile{…} } mit den Zahlen des letzten Laufs.
// Schutz: Im Testlauf nur mit Quellen und Ablagen unter %TEMP% (sonst übersprungen, ohne Merker). Die Quelle der Dateien lässt sich mit HLKS_LABOR_ALTDATEN umleiten.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Hlks.Kern;
using Hlks.Web;
using static Hlks.Web.EndpunktHilfe;

namespace Hlks.Labor;

/// <summary>Wo die bisherigen Dateien liegen: der XR-Ordner je PC (darin messprotokolle und fragebogen).</summary>
public sealed record LaborAltdaten(string XrOrdner)
{
    /// <summary>Umgebungsvariable mit einem anderen XR-Ordner (Probestart, Tests).</summary>
    public const string Variable = "HLKS_LABOR_ALTDATEN";

    public static LaborAltdaten Vorgabe() =>
        new(Environment.GetEnvironmentVariable(Variable) is { Length: > 0 } v && !string.IsNullOrWhiteSpace(v) ? Path.GetFullPath(v.Trim()) : XrEinstellungen.StandardOrdner());

    public string Messprotokolle => Path.Combine(XrOrdner, "messprotokolle");
    public string Fragebogen => Path.Combine(XrOrdner, "fragebogen");
}

public sealed class LaborUebernahme(LaborAblage ablage, LaborProfile profile, BenutzerEinstellungen benutzer, LaborAltdaten alt, Func<DateTimeOffset> jetzt)
{
    static readonly Regex Zeitstempel = new(@"^(\d{8}-\d{6})_(.+)$", RegexOptions.CultureInvariant);
    /// <summary>Grösste Datei, die übernommen wird (die Messprotokolle sind höchstens 5 MB je Sitzung).</summary>
    public const long DateiMax = 16L * 1024 * 1024;
    readonly object sperre = new();

    /// <summary>Dürfen die alten Dateien gelesen werden? Ausser im Testlauf immer; im Testlauf nur unter %TEMP%.</summary>
    public bool LesenErlaubt => !ablage.Testlauf || RechnerAblage.Temporaer(alt.XrOrdner);

    public string MerkerPfad => ablage.UebernahmeDatei;

    /// <summary>Der Merker des letzten Laufs dieses Rechners oder null.</summary>
    public JsonObject? Merker()
    {
        try { return File.Exists(MerkerPfad) ? JsonNode.Parse(File.ReadAllText(MerkerPfad, Encoding.UTF8)) as JsonObject : null; }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or IOException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>Beim Start des Labors: einmal übernehmen, wenn es noch keinen Merker gibt und beides erlaubt ist. Gibt zurück, ob es lief. Wirft nie.</summary>
    public bool ErsterStart()
    {
        try
        {
            if (!ablage.Erlaubt || !LesenErlaubt || Merker() is not null) return false;
            Ausfuehren();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HlksFehler) { return false; }
    }

    /// <summary>Was an alten Dateien da ist (ohne zu kopieren): für GET /api/labor/daten.</summary>
    public JsonObject Vorhanden()
    {
        var erlaubt = LesenErlaubt;
        var (mn, mb) = erlaubt ? LaborAblage.Zaehlen(alt.Messprotokolle, "*.jsonl") : (0, 0);
        var (fn, fb) = erlaubt ? LaborAblage.Zaehlen(alt.Fragebogen, "*.json") : (0, 0);
        int profileN = 0; string? aktiv = null;
        try
        {
            var alle = benutzer.Alle();
            profileN = alle.Keys.Count(k => k.StartsWith(XrLaborProfil.Vorsatz, StringComparison.Ordinal));
            if (alle.TryGetValue(XrLaborProfil.AktivSchluessel, out var a) && a.Wert is JsonValue v && v.TryGetValue<string>(out var s)) aktiv = s;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* Einstellungen nicht lesbar: keine Profile */ }
        return new JsonObject
        {
            ["lesenErlaubt"] = erlaubt, ["xrOrdner"] = alt.XrOrdner,
            ["messprotokolle"] = new JsonObject { ["ordner"] = alt.Messprotokolle, ["dateien"] = mn, ["bytes"] = mb },
            ["fragebogen"] = new JsonObject { ["ordner"] = alt.Fragebogen, ["dateien"] = fn, ["bytes"] = fb },
            ["profile"] = new JsonObject { ["anzahl"] = profileN, ["aktiv"] = aktiv, ["quelle"] = benutzer.Quelle },
        };
    }

    /// <summary>Kopiert alles, schreibt den Merker und gibt ihn zurück. Wirft HlksFehler, wenn die Ablage gesperrt ist.</summary>
    public JsonObject Ausfuehren()
    {
        if (ablage.SchreibSperre() is { } s) throw new HlksFehler(s, "Im Test " + LaborAblage.UmleitungVariable + " auf einen Ordner unter %TEMP% setzen.");
        lock (sperre)
        {
            var lesen = LesenErlaubt;
            var merker = new JsonObject
            {
                ["fassung"] = 1, ["rechner"] = ablage.Rechner, ["zeit"] = jetzt().ToString("o"), ["lesenErlaubt"] = lesen,
                ["quellen"] = new JsonObject { ["messprotokolle"] = alt.Messprotokolle, ["fragebogen"] = alt.Fragebogen, ["profile"] = $"Benutzereinstellungen ({benutzer.Quelle})" },
                ["messungen"] = lesen ? Dateien(alt.Messprotokolle, "*.jsonl", ablage.Messungen) : Leer(),
                ["fragebogen"] = lesen ? Dateien(alt.Fragebogen, "*.json", ablage.Fragebogen) : Leer(),
                ["profile"] = ProfileUebernehmen(),
            };
            ablage.Schreiben(MerkerPfad, Json.Text(merker, true));
            return merker;
        }
    }

    static JsonObject Leer() => new() { ["gefunden"] = 0, ["kopiert"] = 0, ["aktualisiert"] = 0, ["uebersprungen"] = 0, ["fehler"] = 0 };

    /// <summary>Dateiname im Labor: der Rechnername nach dem Zeitstempel («20261023-101010_t3.jsonl» → «20261023-101010_PC_t3.jsonl»); ohne Zeitstempel davor.</summary>
    public string ZielName(string quellName)
    {
        var m = Zeitstempel.Match(quellName);
        return m.Success ? $"{m.Groups[1].Value}_{ablage.Rechner}_{m.Groups[2].Value}" : $"00000000-000000_{ablage.Rechner}_{quellName}";
    }

    static string Hash(byte[] b) => Convert.ToHexString(SHA256.HashData(b));

    static byte[]? Lesen(string datei)
    {
        try
        {
            using var fs = new FileStream(datei, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (fs.Length > DateiMax) return null;
            using var ms = new MemoryStream((int)fs.Length);
            fs.CopyTo(ms);
            return ms.ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    JsonObject Dateien(string quelle, string muster, string ziel)
    {
        int gefunden = 0, kopiert = 0, aktualisiert = 0, uebersprungen = 0, fehler = 0;
        if (Directory.Exists(quelle))
        {
            // Inhalte (Hash) der Dateien, die es im Ziel schon gibt: gleicher Inhalt unter anderem Namen zählt als doppelt
            var vorhanden = new Dictionary<string, string>(StringComparer.Ordinal);
            if (Directory.Exists(ziel))
                foreach (var z in Directory.EnumerateFiles(ziel, muster))
                    if (Lesen(z) is { } zb) vorhanden[Hash(zb)] = z;
            IEnumerable<string> dateien;
            try { dateien = [.. Directory.EnumerateFiles(quelle, muster).OrderBy(Path.GetFileName, StringComparer.Ordinal)]; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { dateien = []; }
            foreach (var datei in dateien)
            {
                gefunden++;
                var inhalt = Lesen(datei);
                if (inhalt is null) { fehler++; continue; }
                var hash = Hash(inhalt);
                if (vorhanden.ContainsKey(hash)) { uebersprungen++; continue; }
                var name = ZielName(Path.GetFileName(datei));
                var pfad = Path.Combine(ziel, name);
                try
                {
                    if (File.Exists(pfad))
                    {
                        var bisher = Lesen(pfad);
                        if (bisher is not null && inhalt.Length > bisher.Length && inhalt.AsSpan(0, bisher.Length).SequenceEqual(bisher))
                        {
                            ablage.Schreiben(pfad, inhalt);   // die Quelle ist gewachsen: die Kopie wird länger
                            vorhanden.Remove(Hash(bisher));
                            vorhanden[hash] = pfad;
                            aktualisiert++;
                            continue;
                        }
                        var stamm = Path.GetFileNameWithoutExtension(name);
                        var endung = Path.GetExtension(name);
                        for (int i = 2; File.Exists(pfad); i++) pfad = Path.Combine(ziel, $"{stamm}-{i}{endung}");
                    }
                    ablage.Schreiben(pfad, inhalt);
                    vorhanden[hash] = pfad;
                    kopiert++;
                }
                catch (HlksFehler) { fehler++; }
            }
        }
        return new JsonObject { ["gefunden"] = gefunden, ["kopiert"] = kopiert, ["aktualisiert"] = aktualisiert, ["uebersprungen"] = uebersprungen, ["fehler"] = fehler };
    }

    JsonObject ProfileUebernehmen()
    {
        int gefunden = 0, kopiert = 0, uebersprungen = 0, abweichend = 0, ungueltig = 0;
        var abweichende = new JsonArray();
        string? aktivUebernommen = null;
        Dictionary<string, EinstellungsStand> alle;
        try { alle = benutzer.Alle(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { alle = []; }
        foreach (var (schluessel, stand) in alle.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (!schluessel.StartsWith(XrLaborProfil.Vorsatz, StringComparison.Ordinal) || stand.Wert is null) continue;
            gefunden++;
            var kennung = schluessel[XrLaborProfil.Vorsatz.Length..];
            if (XrLaborProfil.Pruefen(schluessel, stand.Wert) is not null) { ungueltig++; continue; }
            var bisher = profile.Holen(kennung);
            if (bisher is null)
            {
                try { profile.Speichern(kennung, stand.Wert); kopiert++; }
                catch (HlksFehler) { ungueltig++; }   // z. B. schon die Höchstzahl an Profilen
            }
            else if (JsonNode.DeepEquals(bisher, SauberesProfil(stand.Wert))) uebersprungen++;
            else { abweichend++; abweichende.Add(kennung); }
        }
        // das Profil, das für die App gilt: nur übernehmen, wenn im Labor noch keines gilt
        if (alle.TryGetValue(XrLaborProfil.AktivSchluessel, out var a) && a.Wert is JsonValue av && av.TryGetValue<string>(out var ak)
            && XrLaborProfil.KennungGueltig(ak) && profile.Aktiv() is null && profile.Holen(ak) is not null)
        {
            profile.AktivSetzen(ak);
            aktivUebernommen = ak;
        }
        return new JsonObject
        {
            ["gefunden"] = gefunden, ["kopiert"] = kopiert, ["uebersprungen"] = uebersprungen, ["abweichend"] = abweichend, ["ungueltig"] = ungueltig,
            ["abweichende"] = abweichende, ["aktivUebernommen"] = aktivUebernommen,
        };
    }

    /// <summary>Das Profil so, wie LaborProfile es speichert (Name getrimmt, nur name und werte).</summary>
    static JsonObject SauberesProfil(JsonNode p) => new() { ["name"] = ((string)p["name"]!).Trim(), ["werte"] = p["werte"]!.DeepClone() };
}

/// <summary>Startet die Übernahme beim ersten Start des Labors (kein Merker).</summary>
public sealed class LaborUebernahmeStart(LaborUebernahme uebernahme) : IHostedService
{
    public Task StartAsync(CancellationToken ct) { uebernahme.ErsterStart(); return Task.CompletedTask; }
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}

public static class LaborDatenEndpunkte
{
    /// <summary>
    ///   GET  /api/labor/daten        { ablage{ verzeichnis, quelle, rechner, erlaubt, testlauf, messungen{ordner,dateien,bytes}, fragebogen{…}, profile{ordner,anzahl,aktiv} },
    ///                                  alt{ lesenErlaubt, xrOrdner, messprotokolle{…}, fragebogen{…}, profile{anzahl,aktiv,quelle} }, uebernahme: Merker | null }
    ///   POST /api/labor/uebernahme   kopiert die bisherigen Daten erneut (nie verschieben, nie löschen) → der neue Merker
    /// </summary>
    public static WebApplication MapLaborDaten(this WebApplication app)
    {
        var ablage = app.Services.GetRequiredService<LaborAblage>();
        var profile = app.Services.GetRequiredService<LaborProfile>();
        var uebernahme = app.Services.GetRequiredService<LaborUebernahme>();

        app.MapGet("/api/labor/daten", () => Gesichert(() =>
        {
            var (mn, mb) = LaborAblage.Zaehlen(ablage.Messungen, "*.jsonl");
            var (fn, fb) = LaborAblage.Zaehlen(ablage.Fragebogen, "*.json");
            var antwort = new JsonObject
            {
                ["ablage"] = new JsonObject
                {
                    ["verzeichnis"] = ablage.Verzeichnis, ["quelle"] = ablage.Quelle, ["rechner"] = ablage.Rechner, ["erlaubt"] = ablage.Erlaubt, ["testlauf"] = ablage.Testlauf,
                    ["messungen"] = new JsonObject { ["ordner"] = Path.Combine(ablage.Verzeichnis, "messungen"), ["dateien"] = mn, ["bytes"] = mb },
                    ["fragebogen"] = new JsonObject { ["ordner"] = Path.Combine(ablage.Verzeichnis, "fragebogen"), ["dateien"] = fn, ["bytes"] = fb },
                    ["profile"] = new JsonObject { ["ordner"] = Path.Combine(ablage.Verzeichnis, "profile"), ["anzahl"] = profile.Liste().Count, ["aktiv"] = profile.Aktiv() },
                },
                ["alt"] = uebernahme.Vorhanden(),
                ["uebernahme"] = uebernahme.Merker(),
            };
            return Task.FromResult(Antwort(antwort));
        }));

        app.MapPost("/api/labor/uebernahme", () => Gesichert(() => Task.FromResult(Antwort(uebernahme.Ausfuehren()))));
        return app;
    }
}
