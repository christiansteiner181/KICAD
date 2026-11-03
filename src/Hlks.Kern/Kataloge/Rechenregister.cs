// Register der technischen Auslegungen (Christian 03.11.2026: «wir sollten ein zentrales register (im Klartext) über die technischen
// auslegungen im KICAD abrufbar machen. "Wie wird was gerechnet" … so dass der nutzer immer nachvollziehen kann wie was gerechnet wird.»).
// Quelle: kataloge/rechenregister.json – je Rechen- bzw. Auslegungsverfahren Zweck, Eingaben, Rechenweg im Klartext, Ergebnis, Quellen
// (Norm, Ausgabe, Ziffer bzw. Entscheid mit Datum), Annahmen, Grenzen und Code-Stelle. Abfrage rechenweg (KI und Oberfläche) und die Seite
// /rechenweg/ (RechenwegSeite, Hlks.Web) lesen nur diese Datei; ein neues Verfahren im Kern bekommt dort seinen Eintrag.

using System.Collections.Concurrent;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class Rechenregister
{
    public const string Datei = "rechenregister.json";

    public const string Text = "Register der technischen Auslegungen – wie KICAD was rechnet, im Klartext: je Verfahren Zweck, Eingaben, Rechenweg mit Formeln, " +
        "Ergebnis, Quellen (Norm, Ausgabe, Ziffer bzw. Entscheid), Annahmen, Grenzen und Code-Stelle. Ohne id die Übersicht (id, Titel, Gebiet, Zweck), mit id der " +
        "ganze Eintrag; suche bzw. gebiet grenzen ein. Dieselben Einträge zeigt die Seite /rechenweg/.";
    public const string ParId = "Kennung des Verfahrens (z. B. sanitaer.spitzendurchfluss); leer = Übersicht";
    public const string ParSuche = "Nur Verfahren, deren Text dieses Wort enthält (Gross/klein egal)";
    public const string ParGebiet = "Nur dieses Gebiet (z. B. Sanitär, Heizung, Lüftung)";
    public static IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("id", "text", false, ParId),
        new("suche", "text", false, ParSuche),
        new("gebiet", "text", false, ParGebiet),
    ];

    static readonly ConcurrentDictionary<string, (DateTime Zeit, JsonObject Inhalt)> geladen = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Inhalt von kataloge/rechenregister.json (neu gelesen, wenn die Datei geändert wurde); ohne Datei ein leeres Register.</summary>
    public static JsonObject Laden(Katalog k)
    {
        var pfad = Path.Combine(k.Ordner, Datei);
        if (!File.Exists(pfad)) return new JsonObject { ["verfahren"] = new JsonArray() };
        var zeit = File.GetLastWriteTimeUtc(pfad);
        if (geladen.TryGetValue(pfad, out var da) && da.Zeit == zeit) return da.Inhalt;
        var inhalt = JsonNode.Parse(File.ReadAllText(pfad)) as JsonObject ?? throw new HlksFehler($"{Datei} ist kein JSON-Objekt.");
        geladen[pfad] = (zeit, inhalt);
        return inhalt;
    }

    /// <summary>Verfahren nach suche und gebiet gefiltert (Reihenfolge der Datei).</summary>
    public static List<JsonObject> Verfahren(Katalog k, string? suche = null, string? gebiet = null) =>
        (Laden(k)["verfahren"] as JsonArray ?? []).OfType<JsonObject>()
            .Where(v => gebiet is not { Length: > 0 } || string.Equals(Wert(v["gebiet"]), gebiet, StringComparison.OrdinalIgnoreCase))
            .Where(v => suche is not { Length: > 0 } || v.ToJsonString(Klartext).Contains(suche, StringComparison.OrdinalIgnoreCase))
            .ToList();

    /// <summary>Abfrage rechenweg: ohne id die Übersicht, mit id der ganze Eintrag.</summary>
    public static JsonObject Abfrage(Katalog k, JsonObject? par)
    {
        par ??= [];
        var reg = Laden(k);
        if (Param.Text(par, "id") is { Length: > 0 } id)
        {
            var v = Verfahren(k).FirstOrDefault(x => string.Equals(Wert(x["id"]), id, StringComparison.OrdinalIgnoreCase))
                    ?? throw new HlksFehler($"Verfahren «{id}» steht nicht im Register.", "Übersicht: Abfrage rechenweg ohne id, oder mit suche.");
            return new JsonObject { ["stand"] = reg["stand"]?.DeepClone(), ["verfahren"] = v.DeepClone() };
        }
        var liste = Verfahren(k, Param.Text(par, "suche"), Param.Text(par, "gebiet"));
        return new JsonObject
        {
            ["stand"] = reg["stand"]?.DeepClone(), ["anzahl"] = liste.Count,
            ["verfahren"] = new JsonArray(liste.Select(v => (JsonNode)new JsonObject
            {
                ["id"] = v["id"]?.DeepClone(), ["titel"] = v["titel"]?.DeepClone(), ["gebiet"] = v["gebiet"]?.DeepClone(), ["zweck"] = v["zweck"]?.DeepClone(),
            }).ToArray()),
        };
    }

    static readonly System.Text.Json.JsonSerializerOptions Klartext = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    static string? Wert(JsonNode? n) => n is JsonValue j && j.TryGetValue<string>(out var s) ? s : null;
}
