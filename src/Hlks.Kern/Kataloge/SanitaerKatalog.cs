// Modul 4 Teil A (entwurf/modul-4.md): Belastungswerte der Sanitärapparate. Einzige fachliche Quelle ist der Katalog
// kataloge/sanitaer_belastungswerte.json, vom Hauptlauf aus Christians Excel «LU_DU Zusammenstellung_.xlsx» (Objekt Walkringen,
// 14.03.2024; Tabellenblatt und Bereich stehen im Katalog unter quelle.bereich) übertragen: Apparatearten mit LU KW, LU WW, DU und
// Excel-Zeile, Rechenformeln mit Zellbezug, Kurven mit «grösster LU» und Gültigkeitsbereich, Regel der Kurvenwahl mit Herkunft,
// Referenzergebnis, Vorgabe je Klasse. Werte und Formeln kommen NUR von dort – nichts ergänzen, nichts herleiten (CLAUDE.md: Normwerte nie
// aus dem Gedächtnis). Fehlt etwas oder ist es unlesbar, lehnt das Laden ab (verständlicher Fehler), statt zu raten.
// Prüfbericht 4A (berichte/modul-4a-pruefbericht.md), Auflagen B1/B3/B5: kein Blattname, keine Kurvennummer und keine Schwelle der Kurvenwahl
// im Code – alles aus dem Katalog.

using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Hlks.Kern;

/// <summary>Eine Apparateart. null = der Apparat hat diesen Anschluss nicht (Excel-Zelle leer).</summary>
/// <param name="Reihenfolge">Index im Katalog (0 …) = Reihenfolge der Excel-Zeilen; die Zusammenstellung liest wie das Excel.</param>
public sealed record SanitaerArt(string Schluessel, string Name, double? LuKw, double? LuWw, double? Du, int Zeile, int Reihenfolge)
{
    /// <summary>Name, wie er im Excel steht, wenn der Katalog ihn typografisch bereinigt hat (Katalog nameExcel, Prüfbericht 4A B4); sonst null.</summary>
    public string? NameExcel { get; init; }

    /// <summary>Wert der Spalte, die zum Medium eines Apparateanschlusses gehört: KW → LU KW, WW → LU WW, SW → DU; sonst null.</summary>
    public double? WertFuer(string medium) => SanitaerKatalog.SpalteFuer(medium) switch
    {
        "luKw" => LuKw, "luWw" => LuWw, "du" => Du, _ => null,
    };

    /// <summary>Grösster Einzel-LU der Art (LU KW oder LU WW, wie HKAP SanitaerKatalog.Rechne); 0 ohne LU.</summary>
    public double GroessterLu => Math.Max(LuKw ?? 0, LuWw ?? 0);
}

/// <summary>Kurve des Spitzendurchflusses: Spitze = Volumenstrom^Exponent × Faktor (Excel-Zelle im Katalog).</summary>
/// <param name="Schluessel">Schlüssel im Katalog (rechnung.spitzeKurve3 bzw. rechnung.spitzeKurve5).</param>
/// <param name="Nummer">«Grösster LU» der Kurve aus dem Katalog (grosserLu; Excel C56 «Grösster LU: 3», I56 «Grösster LU: 5»).</param>
/// <param name="BereichVon">Gültigkeitsbereich des Volumenstroms in l/s aus dem Katalog (bereichLs; Excel C55 bzw. I55).</param>
public sealed record SanitaerKurve(string Schluessel, int Nummer, double Faktor, double Exponent, string Zelle, string Text,
    double BereichVon, double BereichBis, string BereichZelle)
{
    public double Spitze(double volumenstrom) => volumenstrom > 0 ? Math.Pow(volumenstrom, Exponent) * Faktor : 0;

    /// <summary>Anzeigename wie in der Zusammenstellung: Kurve «grösster LU n».</summary>
    public string Name => $"Kurve «grösster LU {Nummer}»";

    public bool ImBereich(double volumenstrom) => volumenstrom >= BereichVon && volumenstrom <= BereichBis;
}

/// <summary>Regel, nach der die massgebende Kurve gewählt wird, mit Herkunft (Katalog rechnung.kurvenwahl). Bestaetigt = von Christian bestätigt.</summary>
public sealed record SanitaerKurvenwahl(string Regel, string Quelle, bool Bestaetigt);

public sealed class SanitaerKatalog
{
    public const string Datei = "sanitaer_belastungswerte.json";

    /// <summary>Spalten des Excel je Medium eines Apparateanschlusses (klassen.json: Anschlüsse kw/ww/sw der Apparate). TECHNISCHE Zuordnung,
    /// kein Fachwert: die Spaltenköpfe des Excel (B4 «LU KW», C4 «LU WW», D4 «DU») zu den Medienkennungen aus medien.json (KW Trinkwasser kalt,
    /// WW Trinkwasser warm, SW Schmutzwasser). Zirkulation (ZIR) und Regenwasser (RW) haben keine Spalte und sind ausgenommen. Beim Laden der
    /// Kataloge prüft Katalog.cs, dass es die Medien in medien.json gibt und ihre Systemtypen die passende Verbraucherrichtung haben.</summary>
    public static readonly IReadOnlyDictionary<string, string> Spalten = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["KW"] = "luKw", ["WW"] = "luWw", ["SW"] = "du",
    };

    /// <summary>Medien des Trinkwassernetzes (Spalten LU: Belastungswerte der Versorgung, Summen stromab) und des Abwassernetzes (Spalte DU:
    /// Anschlusswerte der Entwässerung, Summen stromauf) – abgeleitet aus <see cref="Spalten"/>, nicht eigens festgelegt.</summary>
    public static readonly IReadOnlySet<string> Trinkwasser = new HashSet<string>(Spalten.Where(x => x.Value != "du").Select(x => x.Key), StringComparer.OrdinalIgnoreCase);
    public static readonly IReadOnlySet<string> Abwasser = new HashSet<string>(Spalten.Where(x => x.Value == "du").Select(x => x.Key), StringComparer.OrdinalIgnoreCase);

    public static string? SpalteFuer(string medium) => Spalten.TryGetValue(medium, out var s) ? s : null;

    /// <summary>Lesbarer Name einer Spalte: LU KW, LU WW, DU.</summary>
    public static string SpaltenName(string spalte) => spalte switch { "luKw" => "LU KW", "luWw" => "LU WW", "du" => "DU", _ => spalte };

    public string Dokument { get; private init; } = "";
    public string Pfad { get; private init; } = "";
    public string Objekt { get; private init; } = "";
    public string Stand { get; private init; } = "";
    public string Verfasser { get; private init; } = "";
    /// <summary>Bereich im Excel wie im Katalog (quelle.bereich), z. B. «Blatt «MFHA» A7:D40 (Formeln B55–O67)».</summary>
    public string Bereich { get; private init; } = "";
    /// <summary>Tabellenblatt aus dem Katalog: quelle.blatt, sonst «Blatt «…»» in quelle.bereich.</summary>
    public string Blatt { get; private init; } = "";

    public IReadOnlyList<SanitaerArt> Arten { get; private init; } = [];
    public IReadOnlyDictionary<string, SanitaerArt> NachSchluessel { get; private init; } = new Dictionary<string, SanitaerArt>();
    /// <summary>Vorbelegte Art je Klasse (vorgabeJeKlasse, Entscheid Hauptlauf 20.10.2026 wie HKAP; von Christian zu bestätigen).</summary>
    public IReadOnlyDictionary<string, string> VorgabeJeKlasse { get; private init; } = new Dictionary<string, string>();

    // ---- Rechnung (Formeln mit Zellbezug, Texte wörtlich aus dem Katalog)
    public string TotalLuText { get; private init; } = "";
    public string VolumenstromText { get; private init; } = "";
    /// <summary>Faktor Total LU → Volumenstrom in l/s aus dem Katalogfeld rechnung.volumenstromFaktor.wert (Prüfbericht 4A B5.4; nicht mehr
    /// aus dem Formeltext gelesen).</summary>
    public double VolumenstromFaktor { get; private init; }
    /// <summary>Excel-Zelle des Faktors wie im Katalog (rechnung.volumenstromFaktor.zelle, z. B. die Zellformel von B56).</summary>
    public string VolumenstromFaktorZelle { get; private init; } = "";
    /// <summary>Kurve rechnung.spitzeKurve3 des Katalogs (kleinerer «grösster LU»; Excel C57).</summary>
    public SanitaerKurve Kurve3 { get; private init; } = null!;
    /// <summary>Kurve rechnung.spitzeKurve5 des Katalogs (grösserer «grösster LU»; Excel I57).</summary>
    public SanitaerKurve Kurve5 { get; private init; } = null!;
    /// <summary>Regel der Kurvenwahl mit Herkunft (rechnung.kurvenwahl; nicht im Excel, aus HKAP).</summary>
    public SanitaerKurvenwahl Kurvenwahl { get; private init; } = null!;
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

    /// <summary>Quelle eines Werts einer Excel-Zeile: «Katalog …: LU_DU Zusammenstellung_.xlsx, Blatt MFHA, Zeile n (Objekt, Stand)»; mit
    /// nameExcel: «…, Zeile n, im Excel «Name» (Objekt, Stand)».</summary>
    public string Quelle(int zeile, string? nameExcel = null) =>
        $"Katalog {Datei}: {Dokument}, Blatt {Blatt}, Zeile {zeile}{(nameExcel is { Length: > 0 } n ? $", im Excel «{n}»" : "")} ({Objekt}, Stand {Stand})";

    /// <summary>Quelle der Werte einer Apparateart (Excel-Zeile; bei bereinigtem Namen auch der Name im Excel).</summary>
    public string Quelle(SanitaerArt a) => Quelle(a.Zeile, a.NameExcel);

    /// <summary>Quelle einer Formel: Katalog und Excel-Zelle bzw. Formeltext.</summary>
    public string QuelleFormel(string formel) => $"Katalog {Datei}: {Dokument}, Blatt {Blatt}, {formel} ({Objekt}, Stand {Stand})";

    public SanitaerArt? Art(string? schluessel) => schluessel is not null && NachSchluessel.TryGetValue(schluessel.Trim(), out var a) ? a : null;

    /// <summary>TECHNIK: Tabellenblatt in quelle.bereich in der Form «Blatt «Name» …».</summary>
    static readonly Regex BlattMuster = new(@"Blatt\s*«([^»]+)»", RegexOptions.CultureInvariant);

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
        var blatt = T(q["blatt"]) is { Length: > 0 } b ? b.Trim()
            : BlattMuster.Match(bereich) is { Success: true } bm ? bm.Groups[1].Value.Trim()
            : throw Fehl("quelle.bereich", $"Tabellenblatt nicht lesbar («{bereich}») – «Blatt «Name» …» schreiben oder quelle.blatt angeben");

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
                ZahlOderNull(o["luWw"], $"arten[{i}].luWw"), ZahlOderNull(o["du"], $"arten[{i}].du"), (int)zeile, i)
            { NameExcel = T(o["nameExcel"]) is { Length: > 0 } ne ? ne : null };
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
        // Prüfbericht 4A B5.4: Faktor Total LU → Volumenstrom als eigenes Feld { wert, zelle } (wie faktor/zelle der Kurven), nicht aus dem Formeltext
        var vf = r["volumenstromFaktor"] as JsonObject
            ?? throw Fehl("rechnung.volumenstromFaktor", "fehlt – Faktor Total LU → Volumenstrom als { wert, zelle } aus der Excel-Zelle des Volumenstroms eintragen");
        var faktor = ZahlPflicht(vf["wert"], "rechnung.volumenstromFaktor.wert");
        if (!(faktor > 0) || !double.IsFinite(faktor)) throw Fehl("rechnung.volumenstromFaktor.wert", $"muss eine Zahl > 0 sein (ist {faktor.ToString(CultureInfo.InvariantCulture)})");
        var faktorZelle = Pflicht(vf["zelle"], "rechnung.volumenstromFaktor.zelle");
        if (!volumenstrom.Contains(faktorZelle, StringComparison.Ordinal))
            throw Fehl("rechnung.volumenstromFaktor.zelle", $"«{faktorZelle}» steht nicht in rechnung.volumenstrom («{volumenstrom}») – Formeltext und Faktor müssen dieselbe Excel-Zelle nennen");
        SanitaerKurve Kurve(string name)
        {
            var o = r[name] as JsonObject ?? throw Fehl($"rechnung.{name}", "fehlt");
            var lu = ZahlPflicht(o["grosserLu"], $"rechnung.{name}.grosserLu");
            if (lu <= 0 || lu != Math.Floor(lu)) throw Fehl($"rechnung.{name}.grosserLu", "muss eine ganze Zahl > 0 sein (Excel «Grösster LU: n»)");
            if (o["bereichLs"] is not JsonArray br || br.Count != 2) throw Fehl($"rechnung.{name}.bereichLs", "fehlt oder ist kein Paar [von, bis] in l/s");
            var von = ZahlPflicht(br[0], $"rechnung.{name}.bereichLs[0]");
            var bis = ZahlPflicht(br[1], $"rechnung.{name}.bereichLs[1]");
            if (!(von >= 0 && bis > von)) throw Fehl($"rechnung.{name}.bereichLs", "von muss ≥ 0 und kleiner als bis sein");
            return new SanitaerKurve(name, (int)lu, ZahlPflicht(o["faktor"], $"rechnung.{name}.faktor"), ZahlPflicht(o["exponent"], $"rechnung.{name}.exponent"),
                Pflicht(o["zelle"], $"rechnung.{name}.zelle"), Pflicht(o["text"], $"rechnung.{name}.text"), von, bis, Pflicht(o["bereichZelle"], $"rechnung.{name}.bereichZelle"));
        }
        var k3 = Kurve("spitzeKurve3");
        var k5 = Kurve("spitzeKurve5");
        if (k3.Nummer >= k5.Nummer)
            throw Fehl("rechnung.spitzeKurve3.grosserLu", $"muss kleiner sein als rechnung.spitzeKurve5.grosserLu ({k3.Nummer} ≥ {k5.Nummer}) – die Kurvenwahl vergleicht mit dem kleineren Wert");
        var kw = r["kurvenwahl"] as JsonObject ?? throw Fehl("rechnung.kurvenwahl", "fehlt (Regel der massgebenden Kurve mit Quelle)");
        var bestaetigt = kw["bestaetigt"] switch
        {
            null => false,
            JsonValue v when v.GetValueKind() is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False => v.GetValue<bool>(),
            _ => throw Fehl("rechnung.kurvenwahl.bestaetigt", "muss true oder false sein"),
        };
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
            Blatt = blatt,
            Arten = arten,
            NachSchluessel = nach,
            VorgabeJeKlasse = vorgabe,
            TotalLuText = Pflicht(r["totalLu"], "rechnung.totalLu"),
            VolumenstromText = volumenstrom,
            VolumenstromFaktor = faktor,
            VolumenstromFaktorZelle = faktorZelle,
            Kurve3 = k3,
            Kurve5 = k5,
            Kurvenwahl = new SanitaerKurvenwahl(Pflicht(kw["regel"], "rechnung.kurvenwahl.regel"), Pflicht(kw["quelle"], "rechnung.kurvenwahl.quelle"), bestaetigt),
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

    /// <summary>Massgebende Kurve nach der Regel des Katalogs (rechnung.kurvenwahl): grösster Einzel-LU über dem «grössten LU» der kleineren
    /// Kurve → die grössere Kurve, sonst die kleinere. Schwelle und Kurvennummern stehen im Katalog (grosserLu), nicht im Code.</summary>
    public SanitaerKurve MassgebendeKurve(double groessterLu) => groessterLu > Kurve3.Nummer ? Kurve5 : Kurve3;

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
