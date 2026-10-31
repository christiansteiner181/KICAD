// Modul 6 Teil A (entwurf/modul-6.md, A-1 5 und A-2 1): Katalog der Lüftungskette – Variantentypen, Luftgeschwindigkeiten nach MuKEn 2014
// Art. 1.19 Abs. 3 und Christians Planungswerte, je mit Quelle (kataloge/lueftung.json). Der Kern ergänzt keinen Wert.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <summary>Ein Variantentyp: wie die Räume den Geräten zugeordnet werden (gebaeude, zone, geschoss, nutzungseinheit).</summary>
public sealed record LueftungVariantentyp(string Schluessel, string Name, string Gruppierung);

/// <summary>Eine Geschwindigkeitsstufe der Kanäle: bis zu diesem Volumenstrom (null = darüber) gilt ms.</summary>
public sealed record LueftungStufe(double? BisM3h, double Ms);

/// <summary>Ein Planungswert mit Quelle (Christian).</summary>
public sealed record LueftungPlanungswert(double Wert, string Quelle, string? Hinweis);

public sealed class LueftungKatalog
{
    public const string Datei = "lueftung.json";
    public static readonly string[] Gruppierungen = ["gebaeude", "zone", "geschoss", "nutzungseinheit"];

    public string VariantenQuelle { get; private init; } = "";
    public IReadOnlyList<LueftungVariantentyp> Typen { get; private init; } = [];
    public JsonObject GeschwindigkeitQuelle { get; private init; } = new();
    public double ApparateMs { get; private init; }
    public IReadOnlyList<LueftungStufe> Kanal { get; private init; } = [];
    public string Ausnahmen { get; private init; } = "";
    public LueftungPlanungswert HohlraumZuschlagMm { get; private init; } = null!;
    public LueftungPlanungswert SchachtZuschlagMm { get; private init; } = null!;
    public LueftungPlanungswert LaengenZuschlagProzent { get; private init; } = null!;
    public string WartungsflaecheQuelle { get; private init; } = "";
    public IReadOnlyList<string> ZentraleNutzung { get; private init; } = [];
    public IReadOnlyList<string> SchachtNutzung { get; private init; } = [];

    /// <summary>Kanalgeschwindigkeit für einen Volumenstrom (Stufe nach MuKEn 2014 Art. 1.19 Abs. 3).</summary>
    public double KanalMs(double m3h) => Kanal.First(s => s.BisM3h is null || m3h <= s.BisM3h).Ms;

    public static LueftungKatalog Laden(string datei)
    {
        JsonObject w;
        try { w = JsonNode.Parse(File.ReadAllText(datei, System.Text.Encoding.UTF8)) as JsonObject ?? throw new HlksFehler($"Katalog {datei} ist kein Objekt."); }
        catch (System.Text.Json.JsonException ex) { throw new HlksFehler($"Katalog {datei} ist kein gültiges JSON: {ex.Message}"); }
        return Lesen(w, datei);
    }

    public static LueftungKatalog Lesen(JsonObject w, string ort = Datei)
    {
        HlksFehler Fehl(string stelle, string text) => new($"Katalog {Path.GetFileName(ort)}, {stelle}: {text}",
            "Den Katalog nur mit Quelle nachführen (MuKEn 2014 Art. 1.19 bzw. Angabe von Christian) – Werte nie ergänzen oder herleiten.");
        static string? T(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
        string Pflicht(JsonNode? n, string stelle) => T(n) is { Length: > 0 } s ? s : throw Fehl(stelle, "Text fehlt");
        double Zahl(JsonNode? n, string stelle) => Json.Zahl(n) ?? throw Fehl(stelle, "Zahl fehlt");
        LueftungPlanungswert Wert(string name)
        {
            var o = w["planungswerte"]?[name] as JsonObject ?? throw Fehl($"planungswerte.{name}", "fehlt");
            return new(Zahl(o["wert"], $"planungswerte.{name}.wert"), Pflicht(o["quelle"], $"planungswerte.{name}.quelle"), T(o["hinweis"]));
        }
        List<string> Liste(string name) => (w["kandidaten"]?[name] as JsonArray ?? throw Fehl($"kandidaten.{name}", "fehlt"))
            .Select((n, i) => Pflicht(n, $"kandidaten.{name}[{i}]")).ToList();

        var v = w["varianten"] as JsonObject ?? throw Fehl("varianten", "fehlt");
        var typen = (v["typen"] as JsonArray ?? throw Fehl("varianten.typen", "fehlt")).Select((n, i) =>
        {
            var g = Pflicht(n?["gruppierung"], $"varianten.typen[{i}].gruppierung");
            if (!Gruppierungen.Contains(g)) throw Fehl($"varianten.typen[{i}].gruppierung", $"«{g}» unbekannt (erlaubt: {string.Join(", ", Gruppierungen)})");
            return new LueftungVariantentyp(Pflicht(n?["schluessel"], $"varianten.typen[{i}].schluessel"), Pflicht(n?["name"], $"varianten.typen[{i}].name"), g);
        }).ToList();
        if (typen.Count == 0) throw Fehl("varianten.typen", "leer");

        var gw = w["geschwindigkeit"] as JsonObject ?? throw Fehl("geschwindigkeit", "fehlt");
        var quelle = gw["quelle"] as JsonObject ?? throw Fehl("geschwindigkeit.quelle", "fehlt");
        foreach (var k in new[] { "dokument", "stelle" }) Pflicht(quelle[k], $"geschwindigkeit.quelle.{k}");
        var stufen = (gw["kanal"] as JsonArray ?? throw Fehl("geschwindigkeit.kanal", "fehlt")).Select((n, i) =>
            new LueftungStufe(n?["bisM3h"] is null ? null : Zahl(n["bisM3h"], $"geschwindigkeit.kanal[{i}].bisM3h"), Zahl(n?["ms"], $"geschwindigkeit.kanal[{i}].ms"))).ToList();
        if (stufen.Count == 0 || stufen[^1].BisM3h is not null) throw Fehl("geschwindigkeit.kanal", "die letzte Stufe braucht bisM3h null (darüber)");
        for (var i = 1; i < stufen.Count - 1; i++)
            if (stufen[i].BisM3h <= stufen[i - 1].BisM3h) throw Fehl($"geschwindigkeit.kanal[{i}]", "bisM3h muss steigen");
        if (stufen.Any(s => s.Ms <= 0)) throw Fehl("geschwindigkeit.kanal", "ms muss grösser als 0 sein");
        var apparate = Zahl(gw["apparateMs"], "geschwindigkeit.apparateMs");
        if (apparate <= 0) throw Fehl("geschwindigkeit.apparateMs", "muss grösser als 0 sein");

        return new LueftungKatalog
        {
            VariantenQuelle = Pflicht(v["quelle"], "varianten.quelle"),
            Typen = typen,
            GeschwindigkeitQuelle = (JsonObject)quelle.DeepClone(),
            ApparateMs = apparate,
            Kanal = stufen,
            Ausnahmen = T(gw["ausnahmen"]) ?? "",
            HohlraumZuschlagMm = Wert("hohlraumZuschlagMm"),
            SchachtZuschlagMm = Wert("schachtZuschlagMm"),
            LaengenZuschlagProzent = Wert("laengenZuschlagProzent"),
            WartungsflaecheQuelle = Pflicht(w["planungswerte"]?["wartungsflaeche"]?["quelle"], "planungswerte.wartungsflaeche.quelle"),
            ZentraleNutzung = Liste("zentrale"),
            SchachtNutzung = Liste("schacht"),
        };
    }
}
