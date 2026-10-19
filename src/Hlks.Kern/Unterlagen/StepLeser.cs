// STEP- und IGES-Leser (Modul 2, Auftrag B3; entwurf/modul-2.md, «Unterlagen lesen → Befunde»).
// Liest Herstellermodelle über occt-import-js 0.0.23 (LGPL-2.1, OpenCascade als WebAssembly). Das Paket läuft unverändert
// in einem eigenen Node-Prozess (werkzeuge/step/step_lesen.mjs), damit der Kern nicht mit LGPL-Code gelinkt wird;
// installiert wird es mit werkzeuge/step/einrichten.ps1 nach %USERPROFILE%\HLKS_CAD_build\step (nie in OneDrive).
// Das Skript liefert Netze je Teil mit B-Rep-Flächen (brep_faces), Normalen und Farben in mm; die Befunde (form3d,
// flaeche, stutzen, ansicht) macht NetzAnalyse.
//
// Einheit: occt-import-js rechnet mit linearUnit «millimeter» jede Datei nach ihrer eigenen Einheit (STEP: SI- bzw.
// Umrechnungseinheit; IGES: Einheitenschalter im Global-Abschnitt) auf mm um – geprüft mit
// tests/daten/step/quader_200x100x50_meter.stp (Datei in m, Ergebnis 200 mm). Die Einheit der Datei steht zur Kontrolle
// im form3d-Befund (Zusatz einheitDatei).

using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Hlks.Kern;

/// <summary>Rohdaten aus step_lesen.mjs: Netze in mm und Angaben zum Lesen.</summary>
public sealed class StepRohdaten
{
    public List<AnalyseNetz> Netze { get; set; } = [];
    /// <summary>Einheit der gelieferten Koordinaten (immer «mm»).</summary>
    public string Einheit { get; set; } = "mm";
    public string Occt { get; set; } = "";
    public JsonNode? Vernetzung { get; set; }
    /// <summary>Baugruppenbaum der Datei (Name, Netze, Kinder).</summary>
    public JsonNode? Baum { get; set; }
    /// <summary>Meldungen von OpenCascade (Warnungen, Fehler beim Lesen).</summary>
    public List<string> Meldungen { get; set; } = [];
}

/// <summary>Liest STEP (AP203/AP214/AP242) und IGES über occt-import-js in einem Node-Prozess.</summary>
public sealed partial class StepLeser : IUnterlagenLeser
{
    public string Name => "step";
    public int Version => 1;
    public IReadOnlyCollection<string> Arten { get; } = ["step", "iges"];

    /// <summary>Pfad zu node.exe; null = HLKS_NODE, sonst PATH, sonst C:\Program Files\nodejs\node.exe.</summary>
    public string? NodePfad { get; init; }
    /// <summary>Ordner mit node_modules\occt-import-js; null = HLKS_STEP_PAKET, sonst %USERPROFILE%\HLKS_CAD_build\step.</summary>
    public string? PaketOrdner { get; init; }
    /// <summary>Pfad zu step_lesen.mjs; null = HLKS_STEP_SKRIPT, sonst werkzeuge\step\ im Projektordner HLKS_CAD.</summary>
    public string? SkriptPfad { get; init; }
    /// <summary>Zeitlimit für den Node-Prozess. Annahme 3 min: grosse Herstellermodelle brauchen in WebAssembly Zeit.</summary>
    public TimeSpan Zeitlimit { get; init; } = TimeSpan.FromMinutes(3);
    public NetzAnalyseOptionen Optionen { get; init; } = new();

    const string Einrichten = "werkzeuge/step/einrichten.ps1 ausführen (installiert occt-import-js 0.0.23 mit npm ci nach %USERPROFILE%\\HLKS_CAD_build\\step).";

    public UnterlagenBefunde Lesen(string pfad, string datei, string pruefsumme, string art)
    {
        var artKlein = ArtPruefen(art);
        var roh = Rohdaten(pfad, artKlein);
        var einheitDatei = EinheitDerDatei(pfad, artKlein);
        var zusatz = new JsonObject
        {
            ["einheitDatei"] = einheitDatei,
            ["einheitAusgabe"] = roh.Einheit,
            ["occtImportJs"] = roh.Occt,
            ["vernetzung"] = roh.Vernetzung?.DeepClone(),
            ["baum"] = roh.Baum?.DeepClone(),
        };
        var leser = $"{Name} {Version}";
        var ergebnis = NetzAnalyse.Analysieren(roh.Netze, Optionen);
        var hinweise = new List<string>();
        if (einheitDatei is null) hinweise.Add("Längeneinheit der Datei nicht erkannt; occt-import-js rechnet nach den Angaben der Datei auf mm um.");
        else if (einheitDatei != "mm") hinweise.Add($"Datei in {einheitDatei}; von occt-import-js auf mm umgerechnet.");
        var meldungen = roh.Meldungen.Where(m => !string.IsNullOrWhiteSpace(m) && !m.StartsWith("Total number of loaded entities", StringComparison.Ordinal)).ToList();
        hinweise.AddRange(meldungen.Take(20).Select(m => $"OpenCascade: {m.Trim()}"));
        if (meldungen.Count > 20) hinweise.Add($"OpenCascade: {meldungen.Count - 20} weitere Meldungen ausgelassen.");
        hinweise.AddRange(ergebnis.Hinweise);
        return new UnterlagenBefunde
        {
            Datei = datei, Pruefsumme = pruefsumme, Art = artKlein, Leser = leser, Seiten = 1,
            Befunde = NetzAnalyse.Befunde(ergebnis, datei, pruefsumme, leser, 1, zusatz, Optionen),
            Hinweise = hinweise,
        };
    }

    /// <summary>Startet step_lesen.mjs und liest dessen JSON (Netze in mm). Fehler als HlksFehler mit Vorschlag.</summary>
    public StepRohdaten Rohdaten(string pfad, string art)
    {
        var artKlein = ArtPruefen(art);
        if (!File.Exists(pfad)) throw new HlksFehler($"Datei nicht gefunden: {pfad}", "Pfad prüfen.");
        var node = NodeFinden() ?? throw new HlksFehler(
            NodePfad is null ? "Node.js nicht gefunden (weder HLKS_NODE noch PATH noch C:\\Program Files\\nodejs)." : $"Node.js nicht gefunden: {NodePfad}",
            "Node.js (v18 oder neuer) installieren, dann " + Einrichten);
        var skript = SkriptFinden() ?? throw new HlksFehler("Skript werkzeuge/step/step_lesen.mjs nicht gefunden.",
            "HLKS_STEP_SKRIPT auf HLKS_CAD/werkzeuge/step/step_lesen.mjs setzen.");
        var paket = PaketFinden();
        if (!File.Exists(Path.Combine(paket, "node_modules", "occt-import-js", "dist", "occt-import-js.wasm")))
            throw new HlksFehler($"occt-import-js fehlt im Ordner {paket}.", Einrichten);

        var psi = new ProcessStartInfo(node)
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
            StandardOutputEncoding = new UTF8Encoding(false), StandardErrorEncoding = new UTF8Encoding(false),
        };
        foreach (var a in new[] { skript, "--datei", Path.GetFullPath(pfad), "--art", artKlein, "--paket", paket }) psi.ArgumentList.Add(a);
        Process? p;
        try { p = Process.Start(psi); }
        catch (Win32Exception ex) { throw new HlksFehler($"Node.js liess sich nicht starten ({node}): {ex.Message}", "Node.js installieren, dann " + Einrichten); }
        if (p is null) throw new HlksFehler($"Node.js liess sich nicht starten ({node}).", "Node.js installieren, dann " + Einrichten);
        string stdout, stderr;
        using (p)
        {
            var aus = p.StandardOutput.ReadToEndAsync();
            var fehler = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(Zeitlimit))
            {
                try { p.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } catch (Win32Exception) { }
                p.WaitForExit(5000);
                throw new HlksFehler($"Zeitlimit von {Zeitlimit.TotalSeconds:0.###} s beim Lesen von {Path.GetFileName(pfad)} überschritten – Node-Prozess beendet.",
                    "Zeitlimit erhöhen (StepLeser.Zeitlimit) oder ein schlankeres Modell exportieren (ohne Gewinde, Schrauben und Innenleben).");
            }
            p.WaitForExit();
            stdout = aus.GetAwaiter().GetResult();
            stderr = fehler.GetAwaiter().GetResult();
            return Auswerten(stdout, stderr, p.ExitCode, pfad, artKlein);
        }
    }

    static StepRohdaten Auswerten(string stdout, string stderr, int code, string pfad, string art)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(stdout); }
        catch (JsonException)
        {
            var rest = (stderr + " " + stdout).Trim();
            throw new HlksFehler($"Der STEP-Leser hat keine gültige Antwort geliefert (Exit-Code {code}): {Kurz(rest, 400)}", Einrichten);
        }
        using (doc)
        {
            var w = doc.RootElement;
            var meldungen = w.TryGetProperty("meldungen", out var m) && m.ValueKind == JsonValueKind.Array
                ? m.EnumerateArray().Select(x => x.GetString() ?? "").ToList() : [];
            if (!w.TryGetProperty("ok", out var ok) || !ok.GetBoolean())
            {
                var text = w.TryGetProperty("fehler", out var f) ? f.GetString() ?? "" : "unbekannter Fehler";
                var vorschlag = w.TryGetProperty("vorschlag", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                if (code == 3) throw new HlksFehler(text, Einrichten);
                var occt = meldungen.Where(x => !string.IsNullOrWhiteSpace(x)).Take(3).Select(x => x.Trim()).ToList();
                throw new HlksFehler($"{Path.GetFileName(pfad)}: {text}" + (occt.Count > 0 ? $" OpenCascade: {string.Join(" | ", occt)}" : ""),
                    vorschlag ?? "Datei im Herstellerprogramm neu exportieren (STEP AP214/AP242 bzw. IGES 5.3).");
            }
            var roh = new StepRohdaten
            {
                Einheit = w.TryGetProperty("einheit", out var e) ? e.GetString() ?? "mm" : "mm",
                Occt = w.TryGetProperty("occt", out var o) ? o.GetString() ?? "" : "",
                Vernetzung = w.TryGetProperty("vernetzung", out var vn) ? JsonNode.Parse(vn.GetRawText()) : null,
                Baum = w.TryGetProperty("baum", out var b) ? JsonNode.Parse(b.GetRawText()) : null,
                Meldungen = meldungen,
            };
            if (roh.Einheit != "mm")
                throw new HlksFehler($"step_lesen.mjs liefert «{roh.Einheit}» statt mm.", "werkzeuge/step/step_lesen.mjs aus dem Projekt verwenden.");
            foreach (var n in w.GetProperty("netze").EnumerateArray())
            {
                var netz = new AnalyseNetz
                {
                    Name = n.TryGetProperty("name", out var nm) ? nm.GetString() ?? "" : "",
                    Farbe = Zahlen(n, "farbe"),
                    Ecken = Zahlen(n, "ecken") ?? [],
                    Normalen = Zahlen(n, "normalen"),
                    Dreiecke = n.GetProperty("dreiecke").EnumerateArray().Select(x => x.GetInt32()).ToArray(),
                };
                if (n.TryGetProperty("flaechen", out var fl) && fl.ValueKind == JsonValueKind.Array)
                    netz.Flaechen = fl.EnumerateArray()
                        .Select(x => new NetzFlaechengruppe(x.GetProperty("erstes").GetInt32(), x.GetProperty("letztes").GetInt32(), Zahlen(x, "farbe")))
                        .ToList();
                roh.Netze.Add(netz);
            }
            return roh;
        }
    }

    static double[]? Zahlen(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var a) || a.ValueKind != JsonValueKind.Array) return null;
        var z = new double[a.GetArrayLength()];
        int i = 0;
        foreach (var x in a.EnumerateArray()) z[i++] = x.GetDouble();
        return z;
    }

    static string Kurz(string s, int n) => s.Length <= n ? s : s[..n] + " …";

    static string ArtPruefen(string art) => art.Trim().ToLowerInvariant() switch
    {
        "step" or "stp" => "step",
        "iges" or "igs" => "iges",
        _ => throw new HlksFehler($"Der STEP-Leser liest step und iges, nicht «{art}».", "Leser über UnterlagenLeser.Fuer(art) wählen."),
    };

    // ---------- Pfade ----------

    string? NodeFinden()
    {
        if (NodePfad is not null) return File.Exists(NodePfad) ? NodePfad : null;
        var env = Environment.GetEnvironmentVariable("HLKS_NODE");
        if (!string.IsNullOrEmpty(env) && File.Exists(env)) return env;
        var name = OperatingSystem.IsWindows() ? "node.exe" : "node";
        foreach (var d in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var k = Path.Combine(d.Trim().Trim('"'), name);
                if (File.Exists(k)) return k;
            }
            catch (ArgumentException) { }
        }
        var standard = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "node.exe");
        return File.Exists(standard) ? standard : null;
    }

    string? SkriptFinden()
    {
        if (SkriptPfad is not null) return File.Exists(SkriptPfad) ? Path.GetFullPath(SkriptPfad) : null;
        var env = Environment.GetEnvironmentVariable("HLKS_STEP_SKRIPT");
        if (!string.IsNullOrEmpty(env) && File.Exists(env)) return Path.GetFullPath(env);
        var teil = Path.Combine("werkzeuge", "step", "step_lesen.mjs");
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
            for (var d = new DirectoryInfo(start); d is not null; d = d.Parent)
            {
                foreach (var k in new[] { Path.Combine(d.FullName, teil), Path.Combine(d.FullName, "HLKS_CAD", teil) })
                    if (File.Exists(k)) return k;
            }
        var bekannt = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive", "Claude_Code", "HLKS_CAD", teil);
        return File.Exists(bekannt) ? bekannt : null;
    }

    string PaketFinden()
    {
        if (PaketOrdner is not null) return Path.GetFullPath(PaketOrdner);
        var env = Environment.GetEnvironmentVariable("HLKS_STEP_PAKET");
        if (!string.IsNullOrEmpty(env)) return Path.GetFullPath(env);
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "HLKS_CAD_build", "step");
    }

    // ---------- Einheit der Datei (nur zur Kontrolle; umgerechnet hat occt-import-js) ----------

    /// <summary>Längeneinheit, die die Datei selbst festlegt (mm, m, inch …), oder null, wenn nicht erkannt.</summary>
    public static string? EinheitDerDatei(string pfad, string art)
    {
        try
        {
            if (new FileInfo(pfad).Length > 200_000_000) return null;
            return ArtPruefen(art) == "iges" ? IgesEinheit(File.ReadLines(pfad, Encoding.Latin1)) : StepEinheit(File.ReadAllText(pfad, Encoding.Latin1));
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    [GeneratedRegex(@"CONVERSION_BASED_UNIT\s*\(\s*'([^']*)'", RegexOptions.IgnoreCase)]
    private static partial Regex Umrechnungseinheit();

    [GeneratedRegex(@"SI_UNIT\s*\(\s*(?:\.(\w+)\.|\$)\s*,\s*\.METRE\.\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex SiMeter();

    [GeneratedRegex(@"\G\s*(\d+)H")]
    private static partial Regex Hollerith();

    internal static string? StepEinheit(string text)
    {
        var gefunden = new SortedSet<string>(StringComparer.Ordinal);
        int i = 0;
        while ((i = text.IndexOf("LENGTH_UNIT", i, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            int von = text.LastIndexOf(';', i) + 1, bis = text.IndexOf(';', i);
            if (bis < 0) bis = text.Length;
            var satz = text[von..bis];
            var m = Umrechnungseinheit().Match(satz);
            if (m.Success)
                gefunden.Add(m.Groups[1].Value.Trim().ToLowerInvariant() switch
                {
                    "inch" or "in" => "inch",
                    "foot" or "ft" => "ft",
                    var x => x,
                });
            else if ((m = SiMeter().Match(satz)).Success)
                gefunden.Add(!m.Groups[1].Success ? "m" : m.Groups[1].Value.ToUpperInvariant() switch
                {
                    "MILLI" => "mm",
                    "CENTI" => "cm",
                    "DECI" => "dm",
                    "KILO" => "km",
                    "MICRO" => "µm",
                    var x => x.ToLowerInvariant() + "m",
                });
            i = bis;
        }
        return gefunden.Count == 0 ? null : string.Join(", ", gefunden);
    }

    internal static string? IgesEinheit(IEnumerable<string> zeilen)
    {
        var g = new StringBuilder();
        foreach (var z in zeilen)
        {
            if (z.Length < 73) continue;
            if (z[72] == 'G') g.Append(z.AsSpan(0, 72));
            else if (z[72] is 'D' or 'P' or 'T') break;
        }
        var s = g.ToString();
        var felder = new List<string>();
        char trenn = ',', ende = ';';
        int i = 0;
        while (i < s.Length && felder.Count < 16)
        {
            string wert;
            var m = Hollerith().Match(s, i);
            if (m.Success)
            {
                int n = int.Parse(m.Groups[1].Value), von = i + m.Length;
                wert = s.Substring(von, Math.Min(n, s.Length - von));
                i = von + n;
            }
            else
            {
                int j = i;
                while (j < s.Length && s[j] != trenn && s[j] != ende) j++;
                wert = s[i..j].Trim();
                i = j;
            }
            felder.Add(wert);
            if (felder.Count == 1 && wert.Length == 1) trenn = wert[0];
            if (felder.Count == 2 && wert.Length == 1) ende = wert[0];
            while (i < s.Length && s[i] == ' ') i++;
            if (i >= s.Length || s[i] == ende) break;
            if (s[i] == trenn) i++;
        }
        if (felder.Count < 14) return null;
        // Feld 14: Einheitenschalter, Feld 15: Name der Einheit (IGES 5.3, Global-Abschnitt)
        return felder[13].Trim() switch
        {
            "1" => "inch",
            "2" => "mm",
            "4" => "ft",
            "5" => "mile",
            "6" => "m",
            "7" => "km",
            "8" => "mil",
            "9" => "µm",
            "10" => "cm",
            "11" => "µin",
            _ => felder.Count > 14 && felder[14].Length > 0 ? felder[14].ToLowerInvariant() : null,
        };
    }
}
