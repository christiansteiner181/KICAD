// Modul 4 Teil A (entwurf/modul-4.md): Belastungswerte der Sanitärapparate. Einzige fachliche Quelle ist der Katalog
// kataloge/sanitaer_belastungswerte.json, vom Hauptlauf aus Christians Excel «LU_DU Zusammenstellung_.xlsx» (Objekt Walkringen,
// 14.03.2024, Tabelle1 A7:D40) übertragen: Apparatearten mit LU KW, LU WW, DU und Excel-Zeile, Rechenformeln mit Zellbezug,
// Referenzergebnis, Vorgabe je Klasse. Werte und Formeln kommen NUR von dort – nichts ergänzen, nichts herleiten (CLAUDE.md:
// Normwerte nie aus dem Gedächtnis). Fehlt etwas oder ist es unlesbar, lehnt das Laden ab (verständlicher Fehler), statt zu raten.

using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Hlks.Kern;

/// <summary>Eine Apparateart. null = der Apparat hat diesen Anschluss nicht (Excel-Zelle leer).</summary>
/// <param name="Reihenfolge">Index im Katalog (0 …) = Reihenfolge der Excel-Zeilen; die Zusammenstellung liest wie das Excel.</param>
public sealed record SanitaerArt(string Schluessel, string Name, double? LuKw, double? LuWw, double? Du, int Zeile, int Reihenfolge)
{
    /// <summary>Wert der Spalte, die zum Medium eines Apparateanschlusses gehört: KW → LU KW, WW → LU WW, SW → DU; sonst null.</summary>
    public double? WertFuer(string medium) => SanitaerKatalog.SpalteFuer(medium) switch
    {
        "luKw" => LuKw, "luWw" => LuWw, "du" => Du, _ => null,
    };

    /// <summary>Grösster Einzel-LU der Art (LU KW oder LU WW, wie HKAP SanitaerKatalog.Rechne); 0 ohne LU.</summary>
    public double GroessterLu => Math.Max(LuKw ?? 0, LuWw ?? 0);
}

/// <summary>Kurve des Spitzendurchflusses: Spitze = Volumenstrom^Exponent × Faktor (Excel-Zelle im Katalog).</summary>
public sealed record SanitaerKurve(int Nummer, double Faktor, double Exponent, string Zelle, string Text)
{
    public double Spitze(double volumenstrom) => volumenstrom > 0 ? Math.Pow(volumenstrom, Exponent) * Faktor : 0;
}

public sealed class SanitaerKatalog
{
    public const string Datei = "sanitaer_belastungswerte.json";

    /// <summary>Spalten des Excel je Medium eines Apparateanschlusses (klassen.json: Anschlüsse kw/ww/sw der Apparate).
    /// Trinkwasser kalt/warm und Schmutzwasser; Zirkulation (ZIR) und Regenwasser (RW) haben keine Spalte und sind ausgenommen.</summary>
    public static readonly IReadOnlyDictionary<string, string> Spalten = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["KW"] = "luKw", ["WW"] = "luWw", ["SW"] = "du",
    };

    /// <summary>Medien des Trinkwassernetzes (Summen stromab) und des Abwassernetzes (Summen stromauf).</summary>
    public static readonly IReadOnlySet<string> Trinkwasser = new HashSet<string>(["KW", "WW"], StringComparer.OrdinalIgnoreCase);
    public static readonly IReadOnlySet<string> Abwasser = new HashSet<string>(["SW"], StringComparer.OrdinalIgnoreCase);

    public static string? SpalteFuer(string medium) => Spalten.TryGetValue(medium, out var s) ? s : null;

    /// <summary>Lesbarer Name einer Spalte: LU KW, LU WW, DU.</summary>
    public static string SpaltenName(string spalte) => spalte switch { "luKw" => "LU KW", "luWw" => "LU WW", "du" => "DU", _ => spalte };

    public string Dokument { get; private init; } = "";
    public string Pfad { get; private init; } = "";
    public string Objekt { get; private init; } = "";
    public string Stand { get; private init; } = "";
    public string Verfasser { get; private init; } = "";
    /// <summary>Bereich im Excel, z. B. «Tabelle1 A7:D40».</summary>
    public string Bereich { get; private init; } = "";
    /// <summary>Tabellenblatt (erstes Wort des Bereichs), z. B. «Tabelle1».</summary>
    public string Blatt { get; private init; } = "";

    public IReadOnlyList<SanitaerArt> Arten { get; private init; } = [];
    public IReadOnlyDictionary<string, SanitaerArt> NachSchluessel { get; private init; } = new Dictionary<string, SanitaerArt>();
    /// <summary>Vorbelegte Art je Klasse (vorgabeJeKlasse, Entscheid Hauptlauf 20.10.2026 wie HKAP; von Christian zu bestätigen).</summary>
    public IReadOnlyDictionary<string, string> VorgabeJeKlasse { get; private init; } = new Dictionary<string, string>();

    // ---- Rechnung (Formeln mit Zellbezug, Texte wörtlich aus dem Katalog)
    public string TotalLuText { get; private init; } = "";
    public string VolumenstromText { get; private init; } = "";
    /// <summary>Faktor Total LU → Volumenstrom in l/s, gelesen aus der Zellformel des Katalogs («B56 = B55*0.1»).</summary>
    public double VolumenstromFaktor { get; private init; }
    public SanitaerKurve Kurve3 { get; private init; } = null!;
    public SanitaerKurve Kurve5 { get; private init; } = null!;
    public string SpitzeGesamtText { get; private init; } = "";
    public string SchmutzwasserZelle { get; private init; } = "";
    public string SchmutzwasserText { get; private init; } = "";
    public string GesamtabflussText { get; private init; } = "";
    /// <summary>Abflusskennzahl im Objekt Walkringen (nur dort gültig; weitere Werte stehen in SN 592000, die fehlt).</summary>
    public double? AbflusskennzahlBeispiel { get; private init; }
    public string AbflusskennzahlBeispielText { get; private init; } = "";
    public string AbflusskennzahlBeispielZelle { get; private init; } = "";
    /// <summary>Referenzfall Walkringen (Excel-Ergebnis als Testfall), wie im Katalog.</summary>
    public JsonObject Referenz { get; private init; } = [];

    /// <summary>Kurze Quellenangabe: Katalog, Dokument, Objekt, Stand, Bereich.</summary>
    public string QuelleKurz => $"Katalog {Datei}: {Dokument} ({Objekt}, Stand {Stand}), {Bereich}";

    /// <summary>Quelle eines Werts einer Excel-Zeile: Katalog und Excel-Zeile.</summary>
    public string Quelle(int zeile) => $"Katalog {Datei}: {Dokument} ({Objekt}, Stand {Stand}), {Blatt} Zeile {zeile}";

    /// <summary>Quelle einer Formel: Katalog und Excel-Zelle bzw. Formeltext.</summary>
    public string QuelleFormel(string formel) => $"Katalog {Datei}: {Dokument} ({Objekt}, Stand {Stand}), {Blatt} {formel}";

    public SanitaerArt? Art(string? schluessel) => schluessel is not null && NachSchluessel.TryGetValue(schluessel.Trim(), out var a) ? a : null;

    static readonly Regex FaktorMuster = new(@"=\s*[A-Z]+[0-9]+\s*\*\s*([0-9]+(?:\.[0-9]+)?)", RegexOptions.CultureInvariant);

    /// <summary>Liest den Katalog. Fehlt ein Pflichtteil oder ist er unlesbar: HlksFehler mit Stelle.</summary>
    public static SanitaerKatalog Laden(string datei)
    {
        JsonObject w;
        try { w = JsonNode.Parse(File.ReadAllText(datei, System.Text.Encoding.UTF8)) as JsonObject ?? throw new HlksFehler($"Katalog {datei} ist kein Objekt."); }
        catch (System.Text.Json.JsonException ex) { throw new HlksFehler($"Katalog {datei} ist kein gültiges JSON: {ex.Message}"); }
        return Lesen(w, datei);
    }

    /// <summary>Liest den Katalog aus einem JSON-Objekt (für Tests mit abgewandelten Katalogen).</summary>
    public static SanitaerKatalog Lesen(JsonObject w, string ort = Datei)
    {
        HlksFehler Fehl(string stelle, string text) => new($"Katalog {Path.GetFileName(ort)}, {stelle}: {text}",
            "Den Katalog gegen Christians Excel «LU_DU Zusammenstellung_.xlsx» nachführen – Werte nie ergänzen oder herleiten.");
        static string? T(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
        string Pflicht(JsonNode? n, string stelle) => T(n) is { Length: > 0 } s ? s : throw Fehl(stelle, "Text fehlt");
        double ZahlPflicht(JsonNode? n, string stelle) => Json.Zahl(n) ?? throw Fehl(stelle, "Zahl fehlt");
        double? ZahlOderNull(JsonNode? n, string stelle) => n is null ? null : Json.Zahl(n) ?? throw Fehl(stelle, "muss Zahl oder null sein");

        var q = w["quelle"] as JsonObject ?? throw Fehl("quelle", "fehlt");
        var bereich = Pflicht(q["bereich"], "quelle.bereich");

        var arten = new List<SanitaerArt>();
        var nach = new Dictionary<string, SanitaerArt>(StringComparer.OrdinalIgnoreCase);
        if (w["arten"] is not JsonArray liste || liste.Count == 0) throw Fehl("arten", "Liste fehlt oder ist leer");
        foreach (var (n, i) in liste.Select((n, i) => (n, i)))
        {
            var o = n as JsonObject ?? throw Fehl($"arten[{i}]", "kein Objekt");
            var key = Pflicht(o["key"], $"arten[{i}].key");
            var zeile = ZahlPflicht(o["zeile"], $"arten[{i}].zeile");
            if (zeile <= 0 || zeile != Math.Floor(zeile)) throw Fehl($"arten[{i}].zeile", "muss eine Excel-Zeile (ganze Zahl > 0) sein");
            var a = new SanitaerArt(key, Pflicht(o["name"], $"arten[{i}].name"), ZahlOderNull(o["luKw"], $"arten[{i}].luKw"),
                ZahlOderNull(o["luWw"], $"arten[{i}].luWw"), ZahlOderNull(o["du"], $"arten[{i}].du"), (int)zeile, i);
            if (!nach.TryAdd(key, a)) throw Fehl($"arten[{i}].key", $"Schlüssel «{key}» doppelt");
            arten.Add(a);
        }

        var vorgabe = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (w["vorgabeJeKlasse"] is JsonObject vj)
            foreach (var (klasse, wert) in vj)
            {
                if (klasse.StartsWith('$')) continue;   // $kommentar
                var key = Pflicht(wert, $"vorgabeJeKlasse.{klasse}");
                if (!nach.ContainsKey(key)) throw Fehl($"vorgabeJeKlasse.{klasse}", $"Art «{key}» gibt es in arten nicht");
                vorgabe[klasse] = nach[key].Schluessel;
            }

        var r = w["rechnung"] as JsonObject ?? throw Fehl("rechnung", "fehlt");
        var volumenstrom = Pflicht(r["volumenstrom"], "rechnung.volumenstrom");
        var fm = FaktorMuster.Match(volumenstrom);
        if (!fm.Success) throw Fehl("rechnung.volumenstrom", $"Faktor der Zellformel nicht lesbar («{volumenstrom}»)");
        SanitaerKurve Kurve(string name, int nummer)
        {
            var o = r[name] as JsonObject ?? throw Fehl($"rechnung.{name}", "fehlt");
            return new SanitaerKurve(nummer, ZahlPflicht(o["faktor"], $"rechnung.{name}.faktor"), ZahlPflicht(o["exponent"], $"rechnung.{name}.exponent"),
                Pflicht(o["zelle"], $"rechnung.{name}.zelle"), Pflicht(o["text"], $"rechnung.{name}.text"));
        }
        var sw = r["schmutzwasser"] as JsonObject ?? throw Fehl("rechnung.schmutzwasser", "fehlt");
        var kb = r["abflusskennzahlBeispiel"] as JsonObject;

        return new SanitaerKatalog
        {
            Dokument = Pflicht(q["dokument"], "quelle.dokument"),
            Pfad = T(q["pfad"]) ?? "",
            Objekt = Pflicht(q["objekt"], "quelle.objekt"),
            Stand = Pflicht(q["stand"], "quelle.stand"),
            Verfasser = T(q["verfasser"]) ?? "",
            Bereich = bereich,
            Blatt = bereich.Split(' ', 2)[0],
            Arten = arten,
            NachSchluessel = nach,
            VorgabeJeKlasse = vorgabe,
            TotalLuText = Pflicht(r["totalLu"], "rechnung.totalLu"),
            VolumenstromText = volumenstrom,
            VolumenstromFaktor = double.Parse(fm.Groups[1].Value, CultureInfo.InvariantCulture),
            Kurve3 = Kurve("spitzeKurve3", 3),
            Kurve5 = Kurve("spitzeKurve5", 5),
            SpitzeGesamtText = Pflicht(r["spitzeGesamt"], "rechnung.spitzeGesamt"),
            SchmutzwasserZelle = Pflicht(sw["zelle"], "rechnung.schmutzwasser.zelle"),
            SchmutzwasserText = Pflicht(sw["text"], "rechnung.schmutzwasser.text"),
            GesamtabflussText = Pflicht(r["gesamtabfluss"], "rechnung.gesamtabfluss"),
            AbflusskennzahlBeispiel = kb is null ? null : Json.Zahl(kb["wert"]),
            AbflusskennzahlBeispielText = T(kb?["text"]) ?? "",
            AbflusskennzahlBeispielZelle = T(kb?["zelle"]) ?? "",
            Referenz = w["referenz"]?.DeepClone() as JsonObject ?? [],
        };
    }

    /// <summary>Vorgabe-Art einer Klasse (eigene Klasse, sonst nächste Basisklasse mit Vorgabe); null ohne Vorgabe.</summary>
    public SanitaerArt? Vorgabe(Katalog k, string klasse, out string? vorgabeKlasse)
    {
        vorgabeKlasse = null;
        if (VorgabeJeKlasse.TryGetValue(klasse, out var key)) { vorgabeKlasse = klasse; return Art(key); }
        if (k.Klassen.TryGetValue(klasse, out var d))
            foreach (var b in d.Linie)
                if (VorgabeJeKlasse.TryGetValue(b, out key)) { vorgabeKlasse = b; return Art(key); }
        return null;
    }
}
