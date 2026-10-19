// Kommandozeile über dem Kern: Beispielprojekt anlegen, KI-Verständnistest (Auftrag, Erwartung, Bewertung) und
// einzelne Abfragen bzw. Befehle ausführen. Alles läuft über dieselben Türen wie der MCP-Server (Werkzeuge-Klasse,
// Projekt.Ausfuehren, KiVerstaendnistest). Keine Fachlogik hier.
//
// Ausgabe der Einmal-Modi: UTF-8 ausschliesslich auf stdout (auch Fehlermeldungen). Exit-Codes: 0 ok,
// 1 Fehler/abgelehnt, 2 KI-Test nicht bestanden.

using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hlks.Kern;
using ModelContextProtocol.Server;

namespace Hlks.Mcp;

/// <summary>Optionen der Form «--name wert wert …»: Werte sind alle Argumente bis zur nächsten Option.</summary>
public sealed class Argumente
{
    readonly Dictionary<string, List<string>> optionen = new(StringComparer.OrdinalIgnoreCase);

    public Argumente(IEnumerable<string> args)
    {
        List<string>? aktuell = null;
        foreach (var a in args)
        {
            if (a.StartsWith("--", StringComparison.Ordinal)) optionen[a] = aktuell = [];
            else aktuell?.Add(a);
        }
    }

    public bool Hat(string name) => optionen.ContainsKey(name);
    public string? Wert(string name, int index = 0) => optionen.TryGetValue(name, out var l) && index < l.Count ? l[index] : null;
}

public static class Kommandozeile
{
    static readonly string[] Einmal = ["--beispiel", "--ki-auftrag", "--ki-erwartung", "--ki-bewerten", "--abfrage", "--befehl"];

    public static bool IstEinmalModus(Argumente a) => Einmal.Any(a.Hat);

    public static Katalog KatalogLaden(Argumente a) => Katalog.Laden(a.Wert("--katalog") ?? Katalog.StandardOrdner());

    public static Projekt Oeffnen(Argumente a)
    {
        var ordner = a.Wert("--projekt") ?? throw new HlksFehler("--projekt <e_KICAD-Ordner> fehlt.", "Beispiel: --projekt \"W:\\…\\c_Pläne und Modelle\\e_KICAD\"");
        var art = (a.Wert("--art") ?? "mensch").ToLowerInvariant();
        if (art is not ("mensch" or "ki")) throw new HlksFehler($"--art «{art}» ist ungültig.", "Erlaubt: mensch, ki");
        return Projekt.Oeffnen(KatalogLaden(a), new OrdnerSpeicher(ordner), a.Wert("--akteur") ?? "CLI", a.Wert("--geraet") ?? Environment.MachineName, art);
    }

    static string Fehlertext(Exception ex) => ex is HlksFehler f
        ? "Fehler: " + f.Message + (f.Vorschlag is null ? "" : "\nVorschlag: " + f.Vorschlag)
        : "Fehler: " + ex.Message;

    static JsonObject ParameterLesen(string? text, string was)
    {
        if (string.IsNullOrWhiteSpace(text)) return new JsonObject();
        if (text.StartsWith('@'))
        {
            var datei = text[1..];
            if (!File.Exists(datei)) throw new HlksFehler($"Parameterdatei {datei} gibt es nicht.");
            text = File.ReadAllText(datei, Encoding.UTF8);
        }
        try { return JsonNode.Parse(text) as JsonObject ?? throw new HlksFehler($"Die Parameter für {was} müssen ein JSON-Objekt sein."); }
        catch (JsonException ex)
        {
            // Windows PowerShell 5.1 entfernt beim Aufruf die Anführungszeichen: {strang:SW-F2} -> flache Form lesen
            if (FlacheForm(text) is { } flach) return flach;
            throw new HlksFehler($"Die Parameter für {was} sind kein gültiges JSON: {ex.Message}",
                "Form: '{\"element\": \"WC-1.02\"}' oder @datei.json; in Windows PowerShell 5.1 das JSON in eine Datei schreiben und mit @datei übergeben.");
        }
    }

    /// <summary>Tolerante Notform ohne Anführungszeichen, nur flach: {schluessel:wert,schluessel:wert}; Zahlen werden Zahlen.</summary>
    static JsonObject? FlacheForm(string text)
    {
        text = text.Trim();
        if (!text.StartsWith('{') || !text.EndsWith('}') || text.Contains('[') || text.LastIndexOf('{') > 0) return null;
        var o = new JsonObject();
        foreach (var teil in text[1..^1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var i = teil.IndexOf(':');
            if (i <= 0) return null;
            var wert = teil[(i + 1)..].Trim();
            // Werte bleiben Text (Kennzeichen wie «1.20» dürfen keine Zahl werden); Zahlen liest der Kern auch aus Text
            o[teil[..i].Trim()] = wert switch { "true" => JsonValue.Create(true), "false" => JsonValue.Create(false), "null" => null, _ => JsonValue.Create(wert) };
        }
        return o;
    }

    /// <summary>Führt einen Einmal-Modus aus und gibt den Exit-Code zurück.</summary>
    public static int Ausfuehren(Argumente a, TextWriter aus)
    {
        Projekt? projekt = null;
        try
        {
            if (a.Hat("--ki-auftrag")) { aus.WriteLine(KiVerstaendnistest.Auftrag()); return 0; }
            if (a.Hat("--beispiel")) return BeispielAnlegen(a, aus);

            projekt = Oeffnen(a);
            if (a.Hat("--ki-erwartung")) { aus.WriteLine(Json.Text(KiVerstaendnistest.Erwartung(projekt), true)); return 0; }
            if (a.Hat("--ki-bewerten")) return Bewerten(a, projekt, aus);
            if (a.Hat("--abfrage")) return Abfrage(a, projekt, aus);
            if (a.Hat("--befehl")) return Befehl(a, projekt, aus);
            throw new HlksFehler("Kein Modus angegeben.");
        }
        catch (Exception ex)
        {
            aus.WriteLine(Fehlertext(ex));
            return 1;
        }
        finally { projekt?.Freigeben(); }
    }

    static int BeispielAnlegen(Argumente a, TextWriter aus)
    {
        var ordner = a.Wert("--beispiel") ?? throw new HlksFehler("--beispiel <ordner> fehlt.", "Beispiel: --beispiel C:\\Temp\\beispiel\\e_KICAD");
        var speicher = new OrdnerSpeicher(ordner);
        if (Directory.Exists(ordner) && speicher.KopfLesen() is not null)
            throw new HlksFehler($"In {ordner} gibt es schon ein Projekt.", "Einen leeren Ordner wählen oder das Projekt mit --projekt öffnen.");
        var p = Beispielprojekt.Erzeugen(KatalogLaden(a), speicher, a.Wert("--geraet") ?? Environment.MachineName);
        p.Freigeben();
        var s = p.Stand;
        aus.WriteLine($"Beispielprojekt «{p.Kopf.Name}» angelegt in {ordner}");
        aus.WriteLine($"{s.Elemente} Elemente, {s.Verbindungen} Verbindungen, {s.Pakete} Pakete, Prüfsumme {s.Pruefsumme}");
        return 0;
    }

    static int Bewerten(Argumente a, Projekt projekt, TextWriter aus)
    {
        var datei = a.Wert("--ki-bewerten") ?? throw new HlksFehler("--ki-bewerten <antwort.json> fehlt.");
        if (!File.Exists(datei)) throw new HlksFehler($"Antwortdatei {datei} gibt es nicht.");
        JsonObject antwort;
        try { antwort = JsonNode.Parse(File.ReadAllText(datei, Encoding.UTF8)) as JsonObject ?? new JsonObject(); }
        catch (JsonException) { antwort = new JsonObject(); }
        var ergebnis = KiVerstaendnistest.Bewerten(projekt, antwort);
        aus.WriteLine(Json.Text(ergebnis, true));
        return ergebnis["bestanden"]?.GetValue<bool>() == true ? 0 : 2;
    }

    static int Abfrage(Argumente a, Projekt projekt, TextWriter aus)
    {
        var name = a.Wert("--abfrage") ?? throw new HlksFehler("--abfrage <name> fehlt.");
        if (Abfragen.Liste.All(x => x.Name != name))
            throw new HlksFehler($"Abfrage «{name}» gibt es nicht.", "Abfragen: " + string.Join(", ", Abfragen.Liste.Select(x => x.Name)));
        var parameter = ParameterLesen(a.Wert("--abfrage", 1), name);
        var werkzeuge = new Werkzeuge(projekt);
        var methode = typeof(Werkzeuge).GetMethods().Single(m => m.GetCustomAttribute<McpServerToolAttribute>()?.Name == name);
        var ps = methode.GetParameters();
        var unbekannt = parameter.Select(k => k.Key).Where(k => ps.All(p => !string.Equals(p.Name, k, StringComparison.OrdinalIgnoreCase))).ToList();
        if (unbekannt.Count > 0)
            throw new HlksFehler($"Parameter {string.Join(", ", unbekannt)} gibt es bei {name} nicht.", "Erlaubt: " + (ps.Length == 0 ? "keine" : string.Join(", ", ps.Select(p => p.Name))));
        var werte = new object?[ps.Length];
        for (int i = 0; i < ps.Length; i++)
        {
            var node = parameter.FirstOrDefault(k => string.Equals(k.Key, ps[i].Name, StringComparison.OrdinalIgnoreCase)).Value;
            if (node is null)
            {
                if (!ps[i].HasDefaultValue) throw new HlksFehler($"Parameter «{ps[i].Name}» fehlt.", $"{name}: " + string.Join(", ", ps.Select(p => p.Name + (p.HasDefaultValue ? "?" : ""))));
                werte[i] = ps[i].DefaultValue;
                continue;
            }
            var typ = Nullable.GetUnderlyingType(ps[i].ParameterType) ?? ps[i].ParameterType;
            werte[i] = typ == typeof(double) ? Json.Zahl(node) ?? throw new HlksFehler($"Parameter «{ps[i].Name}» muss eine Zahl sein.")
                     : typ == typeof(string) ? (node is JsonValue v && v.TryGetValue<string>(out var s) ? s : node.ToJsonString())
                     : typ == typeof(bool) ? (node is JsonValue bv && bv.TryGetValue<bool>(out var bb) ? bb : throw new HlksFehler($"Parameter «{ps[i].Name}» muss true oder false sein."))
                     : typ == typeof(JsonElement) ? JsonDocument.Parse(node.ToJsonString()).RootElement.Clone()
                     : throw new HlksFehler($"Parameter «{ps[i].Name}» wird auf der Kommandozeile nicht unterstützt.");
        }
        var text = ErgebnisText(methode.Invoke(werkzeuge, werte));
        aus.WriteLine(text);
        return FehlerInAntwort(text) ? 1 : 0;
    }

    /// <summary>Text einer Werkzeugantwort: String unverändert; ein Ergebnis mit Bildinhalt (unterlage_bild) als sein Text
    /// mit dem Vermerk, dass das Bild nur über MCP mitkommt.</summary>
    static string ErgebnisText(object? ergebnis) => ergebnis switch
    {
        string s => s,
        ModelContextProtocol.Protocol.CallToolResult r => string.Join("\n", r.Content.OfType<ModelContextProtocol.Protocol.TextContentBlock>().Select(t => t.Text))
            + (r.Content.OfType<ModelContextProtocol.Protocol.ImageContentBlock>().Any() ? "\n(Bildinhalt nur über den MCP-Server; über REST: /api/abfrage/unterlage_bild liefert base64.)" : ""),
        _ => ergebnis?.ToString() ?? "",
    };

    static bool FehlerInAntwort(string text)
    {
        try { return JsonNode.Parse(text) is JsonObject o && o.ContainsKey("fehler"); }
        catch (JsonException) { return false; }   // Textansicht
    }

    static int Befehl(Argumente a, Projekt projekt, TextWriter aus)
    {
        var name = a.Wert("--befehl") ?? throw new HlksFehler("--befehl <name> fehlt.");
        var parameter = ParameterLesen(a.Wert("--befehl", 1), name);
        var text = new Werkzeuge(projekt).Befehl(name, parameter, a.Hat("--trockenlauf"));
        aus.WriteLine(JsonNode.Parse(text)!.ToJsonString(Json.Eingerueckt));
        var o = JsonNode.Parse(text) as JsonObject;
        return o?.ContainsKey("fehler") == true || o?["angenommen"]?.GetValue<bool>() == false ? 1 : 0;
    }
}
