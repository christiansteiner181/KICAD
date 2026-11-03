// Zentrales Verzeichnis der Grundlagen (Christian 03.11.2026: «mache doch ein zentrales normenverzeichniss. dass die grundlagen klar sind.
// fehlt eine normelle regulierung gilt die phisik.»). Quelle: kataloge/grundlagen.json – Grundsatz (Rangfolge Norm › Herstellerunterlage ›
// Bürovorgabe › Physik) und je Grundlage Art, Kürzel, Ausgabe, Ablage und Status (original, Auszug, online, fehlt). Die Verfahren im
// Rechenregister verweisen mit «grundlagen» auf die Kennungen; «verwendet in» rechnet dieses Verzeichnis daraus. Nicht zu verwechseln mit
// der Volltextsuche im Normenordner (Normen/Normenverzeichnis.cs).

using System.Collections.Concurrent;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class Grundlagenverzeichnis
{
    public const string Datei = "grundlagen.json";
    /// <summary>Arten einer Grundlage in der Reihenfolge der Seite.</summary>
    public static readonly string[] Arten = ["norm", "richtlinie", "vorschrift", "standard", "lehrmittel", "hersteller", "stoffwerte", "buero", "entscheid", "physik", "annahme"];

    public const string Text = "Zentrales Verzeichnis der Grundlagen von KICAD: Grundsatz (Rangfolge Norm › Herstellerunterlage › Bürovorgabe › Physik – fehlt eine " +
        "normative Regelung, gilt die Physik) und je Grundlage Art, Kürzel, Ausgabe, Ablage, Status (original, Auszug, online, fehlt) und die Verfahren, die sie " +
        "verwenden. Ohne id alle Grundlagen, mit id eine samt Verfahren; art bzw. gebiet grenzen ein. Dieselben Angaben zeigt die Seite /rechenweg/ oben.";
    public const string ParId = "Kennung der Grundlage (z. B. norm.sn592000); leer = alle";
    public const string ParArt = "Nur diese Art (norm, richtlinie, vorschrift, standard, lehrmittel, hersteller, stoffwerte, buero, entscheid, physik, annahme)";
    public const string ParGebiet = "Nur Grundlagen dieses Gebiets (z. B. Sanitär, Heizung)";
    public static IReadOnlyList<ParameterDef> Parameter { get; } =
        [new("id", "text", false, ParId), new("art", "text", false, ParArt), new("gebiet", "text", false, ParGebiet)];

    static readonly ConcurrentDictionary<string, (DateTime Zeit, JsonObject Inhalt)> geladen = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Inhalt von kataloge/grundlagen.json (neu gelesen, wenn die Datei geändert wurde); ohne Datei leer.</summary>
    public static JsonObject Laden(Katalog k)
    {
        var pfad = Path.Combine(k.Ordner, Datei);
        if (!File.Exists(pfad)) return new JsonObject { ["grundlagen"] = new JsonArray() };
        var zeit = File.GetLastWriteTimeUtc(pfad);
        if (geladen.TryGetValue(pfad, out var da) && da.Zeit == zeit) return da.Inhalt;
        var inhalt = JsonNode.Parse(File.ReadAllText(pfad)) as JsonObject ?? throw new HlksFehler($"{Datei} ist kein JSON-Objekt.");
        geladen[pfad] = (zeit, inhalt);
        return inhalt;
    }

    public static List<JsonObject> Alle(Katalog k) => (Laden(k)["grundlagen"] as JsonArray ?? []).OfType<JsonObject>().ToList();

    /// <summary>Kennungen der Verfahren im Rechenregister, die eine Grundlage verwenden.</summary>
    public static List<string> VerwendetIn(Katalog k, string id) =>
        Rechenregister.Verfahren(k).Where(v => (v["grundlagen"] as JsonArray ?? []).Any(g => W(g) == id)).Select(v => W(v["id"])).OfType<string>().ToList();

    /// <summary>Abfrage grundlagen: ohne id Grundsatz und alle (gefiltert) mit «verwendetIn», mit id eine Grundlage samt Verfahren (id, Titel).</summary>
    public static JsonObject Abfrage(Katalog k, JsonObject? par)
    {
        par ??= [];
        var inhalt = Laden(k);
        if (Param.Text(par, "id") is { Length: > 0 } id)
        {
            var g = Alle(k).FirstOrDefault(x => string.Equals(W(x["id"]), id, StringComparison.OrdinalIgnoreCase))
                    ?? throw new HlksFehler($"Grundlage «{id}» steht nicht im Verzeichnis.", "Übersicht: Abfrage grundlagen ohne id.");
            var aus = g.DeepClone().AsObject();
            var reg = Rechenregister.Verfahren(k);
            aus["verwendetIn"] = new JsonArray(VerwendetIn(k, W(g["id"])!).Select(v => (JsonNode)new JsonObject
            {
                ["id"] = v, ["titel"] = reg.First(x => W(x["id"]) == v)["titel"]?.DeepClone(),
            }).ToArray());
            return new JsonObject { ["stand"] = inhalt["stand"]?.DeepClone(), ["grundsatz"] = inhalt["grundsatz"]?.DeepClone(), ["grundlage"] = aus };
        }
        string? art = Param.Text(par, "art"), gebiet = Param.Text(par, "gebiet");
        var liste = Alle(k)
            .Where(x => art is not { Length: > 0 } || string.Equals(W(x["art"]), art, StringComparison.OrdinalIgnoreCase))
            .Where(x => gebiet is not { Length: > 0 } || (x["gebiete"] as JsonArray ?? []).Any(y => string.Equals(W(y), gebiet, StringComparison.OrdinalIgnoreCase)))
            .Select(x => { var o = x.DeepClone().AsObject(); o["verwendetIn"] = new JsonArray(VerwendetIn(k, W(x["id"])!).Select(v => (JsonNode)v).ToArray()); return o; })
            .ToList();
        return new JsonObject
        {
            ["stand"] = inhalt["stand"]?.DeepClone(), ["grundsatz"] = inhalt["grundsatz"]?.DeepClone(), ["anzahl"] = liste.Count,
            ["grundlagen"] = new JsonArray(liste.Select(x => (JsonNode)x).ToArray()),
        };
    }

    static string? W(JsonNode? n) => n is JsonValue j && j.TryGetValue<string>(out var s) ? s : null;
}
