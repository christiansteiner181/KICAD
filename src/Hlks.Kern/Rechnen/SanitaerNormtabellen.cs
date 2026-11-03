// Normtabellen der Sanitär-Dimensionierung (Modul 4 Teil B): Abwasser nach SN 592000:2024, Trinkwasser nach SVGW W3 – übertragen aus dem
// «Handbuch Gebäudetechnik Sanitär 2025» (Lehrmittel mit Normauszügen, W:\05_Normen und Merkblätter; Christian 02./03.11.2026), je Wert mit
// Befund-Id als Beleg. Die Werte stehen NICHT im Repository (Regel «Normtexte nie ins Repository», Weitergabe laut Impressum eingeschränkt),
// sondern im Büroordner: Umgebungsvariable HLKS_NORMTABELLEN, sonst kataloge/buero.json › normtabellen.pfad (Vorgabe
// %OneDrive%\HLKS-CAD\normtabellen), darin sanitaer\abwasser.json und sanitaer\trinkwasser.json. Fehlt eine Datei, rechnet Teil B diesen Teil
// nicht und sagt es (Hinweis). Tests legen eigene, fiktive Tabellen unter %TEMP% an.

using System.Collections.Concurrent;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class SanitaerNormtabellen
{
    public const string Umgebungsvariable = "HLKS_NORMTABELLEN";
    public const string Vorgabe = @"%OneDrive%\HLKS-CAD\normtabellen";

    static readonly ConcurrentDictionary<string, (DateTime Zeit, JsonObject Inhalt)> geladen = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Ordner der Normtabellen (Umgebungsvariable, buero.json, Vorgabe).</summary>
    public static string Ordner(Katalog k)
    {
        if (Environment.GetEnvironmentVariable(Umgebungsvariable) is { Length: > 0 } u) return Environment.ExpandEnvironmentVariables(u);
        var buero = Path.Combine(k.Ordner, "buero.json");
        if (File.Exists(buero) && JsonNode.Parse(File.ReadAllText(buero))?["normtabellen"]?["pfad"] is JsonValue v && v.TryGetValue<string>(out var p) && p.Length > 0)
            return Environment.ExpandEnvironmentVariables(p);
        return Environment.ExpandEnvironmentVariables(Vorgabe);
    }

    /// <summary>sanitaer\abwasser.json bzw. sanitaer\trinkwasser.json; null, wenn die Datei fehlt.</summary>
    public static JsonObject? Laden(Katalog k, string name)
    {
        var pfad = Path.Combine(Ordner(k), "sanitaer", name + ".json");
        if (!File.Exists(pfad)) return null;
        var zeit = File.GetLastWriteTimeUtc(pfad);
        if (geladen.TryGetValue(pfad, out var da) && da.Zeit == zeit) return da.Inhalt;
        var inhalt = JsonNode.Parse(File.ReadAllText(pfad)) as JsonObject ?? throw new HlksFehler($"Normtabelle {pfad} ist kein JSON-Objekt.");
        geladen[pfad] = (zeit, inhalt);
        return inhalt;
    }

    public static JsonObject? Abwasser(Katalog k) => Laden(k, "abwasser");
    public static JsonObject? Trinkwasser(Katalog k) => Laden(k, "trinkwasser");

    /// <summary>Kurzer Beleg einer Tabellenzeile: «Tabelle n, S. x (Befund-Ids)».</summary>
    public static string Beleg(JsonNode? tabelle, JsonNode? zeile)
    {
        var t = tabelle?["tabelle"]?.GetValue<string>() ?? "Tabelle";
        var s = (zeile?["seite"] ?? tabelle?["seite"])?.ToString();
        var q = (zeile?["quelle"] ?? tabelle?["quelle"]) as JsonArray;
        return $"{t}{(s is { Length: > 0 } ? ", S. " + s : "")}{(q is { Count: > 0 } ? " (" + string.Join(", ", q.Select(x => x?.ToString())) + ")" : "")}";
    }
}
