// Ausgabeprotokoll (Phase 1 Rang 4, 30.10.2026, Gesamtschau M5): Dateiausgaben an Dritte (Planblatt- und Plansatz-PDF, IFC-Export,
// Aussparungs-IFC) laufen als Abfragen und erzeugen kein Paket. Damit nachvollziehbar bleibt, was wann auf welchem Modellstand
// hinausging, schreibt jede Ausgabe eine Zeile in <Projekt>\ausgabe\protokoll\<Gerät>.jsonl (je Gerät eine Datei, nie zwei Schreiber
// auf derselben Datei über das NAS). Ohne Projektablage (Tests, Beispiel im Speicher) wird nichts geschrieben. Ein Fehler beim
// Protokollieren bricht die Ausgabe nicht ab – er kommt als Hinweis zurück.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class Ausgabeprotokoll
{
    public static string? Ordner(Projekt p) => p.Speicher is null ? null : Path.Combine(p.Speicher.Ort, "ausgabe", "protokoll");

    /// <summary>Vermerkt eine geschriebene Datei. Rückgabe: Hinweis bei einem Fehler, sonst null.</summary>
    public static string? Vermerken(Projekt p, string art, string datei)
    {
        var ordner = Ordner(p);
        if (ordner is null) return null;
        try
        {
            string? pruefsumme = null; long groesse = 0;
            if (File.Exists(datei))
            {
                using var s = File.OpenRead(datei);
                groesse = s.Length;
                pruefsumme = Convert.ToHexString(SHA256.HashData(s)).ToLowerInvariant()[..12];
            }
            var stand = p.Stand;
            var zeile = new JsonObject
            {
                ["zeit"] = DateTimeOffset.Now.ToString("o"), ["akteur"] = p.Akteur, ["akteurArt"] = p.AkteurArt, ["geraet"] = p.Geraet,
                ["art"] = art, ["datei"] = datei, ["groesseBytes"] = groesse, ["pruefsumme"] = pruefsumme,
                ["modellPruefsumme"] = stand.Pruefsumme, ["letztesPaket"] = stand.LetztesPaket,
            };
            Directory.CreateDirectory(ordner);
            File.AppendAllText(Path.Combine(ordner, Sauber(p.Geraet) + ".jsonl"), zeile.ToJsonString() + "\n", new UTF8Encoding(false));
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"Ausgabeprotokoll nicht geschrieben ({ordner}): {ex.Message}";
        }
    }

    /// <summary>Alle Einträge aller Geräte, neueste zuerst.</summary>
    public static JsonObject Lesen(Projekt p, int anzahl)
    {
        var liste = new List<JsonObject>();
        var ordner = Ordner(p);
        if (ordner is not null && Directory.Exists(ordner))
            foreach (var f in Directory.EnumerateFiles(ordner, "*.jsonl"))
                foreach (var z in File.ReadLines(f))
                    if (z.Length > 0 && JsonNode.Parse(z) is JsonObject o) liste.Add(o);
        return new JsonObject
        {
            ["hinweis"] = ordner is null ? "Projekt ohne Ablage: kein Ausgabeprotokoll." : $"Protokoll in {ordner} (je Gerät eine Datei).",
            ["anzahl"] = liste.Count,
            ["ausgaben"] = new JsonArray([.. liste.OrderByDescending(o => o["zeit"]?.ToString(), StringComparer.Ordinal).Take(Math.Clamp(anzahl, 1, 1000))]),
        };
    }

    static string Sauber(string s)
    {
        var t = new string(s.Select(c => char.IsAsciiLetterOrDigit(c) || c == '-' ? c : '-').ToArray()).Trim('-');
        return t.Length == 0 ? "geraet" : t;
    }
}
