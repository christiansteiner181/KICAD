// Familien (Nachtrag 21.10.2026, hlks-kern): Quellen von Familien bleiben über einen Wechsel der Leserversion gültig.
//
// Befund-Ids lauten «<Prüfsumme 12>#<laufende Nummer>» und sind nur stabil, solange Datei UND Leserversion gleich sind. Der PDF-Leser 2
// (c20e863) nummeriert anders als 1 (Schmidlin-Preisbuch: ab Seite 19 jede Nummer + 21); die Familien nennen noch Ids nach Leser 1, die
// Prüfung löst aber gegen Version 2 auf. Entscheid Hauptlauf 21.10.2026:
//  1. unterlagen[].leser = «<lesername>-<version>» (z. B. «pdf-2»): der Kern setzt es bei jedem Speichern für jede Unterlage, deren Befunde
//     die Quellen verwenden (FamilienQuellenAnalyse.Rechnen, Schritt 4). Optional – Familien ohne leser sind Altbestand.
//  2. Umschlüsseln beim Speichern und Prüfen: Ids einer anderen Version als der des aktuellen Lesers werden auf die aktuelle umgeschlüsselt –
//     über gleiche Seite bzw. gleiches Layout, gleiche Art, gleichen Wert (Text oder Zahl; Text ohne Leerraum verglichen, eine Tabelle ohne
//     ihre Zellnummern) und Box ±0.5 (Einheit der Unterlage), bei mehreren Kandidaten zuerst der genau gleiche Text, dann Lage, Zusatz (ohne
//     Verweise auf andere Ids), Layer und Einheit. Die
//     Zuordnung muss eindeutig sein; sonst lehnt das Speichern mit jeder Id und Grund ab, familie_pruefen meldet einen Prüfbefund.
//     Die alte Version kommt aus der Ablagedatei unterlagen/befunde/<prüfsumme>.<leser>.json, die neue aus der Ablage bzw. dem Leser.
//  3. Altbestand ohne leser: Version je Unterlage aus den Kandidaten (Ablagedateien dieser Prüfsumme für diesen Leser und die aktuelle
//     Version): die, unter der alle Quellen dieser Unterlage die Wertprüfung bestehen; sonst die mit den meisten bestandenen (der Rest wird
//     wie bisher gemeldet). Deterministisch, ohne Zufall.
//  4. Alte Version fehlt (anderes Projekt, gelöschte Ablage, alter Leser nicht mehr im Code): Warnung «Quellen nach Leser pdf-1, Befunde
//     dieser Version fehlen im Projekt – Quellen neu zuordnen»; die Wertprüfung dieser Quellen entfällt (BefundQuelle.Ueberspringen), statt
//     gegen die falsche Version zu laufen; Speichern bleibt möglich, Ids und leser bleiben unverändert.
//     Geprüft: Die Familienbibliothek führt zu einer Familie ihre Unterlagen mit (Dateien in _unterlagen\<prüfsumme>.<ext>, beim Laden ins
//     Projekt kopiert), aber keine Befunde. Aus einer Datei liest nur der aktuelle Leser – die Bibliothek ist also keine Quelle einer alten
//     Version.
//  5. Sammelbefehl familien_quellen_nachfuehren (unten) und Abfrage familie_pruefen (unten; gab es vorher nicht).
//
// ANNAHMEN (Prüfliste):
//  - Welche Ids «alt» sind: bei familie_aendern nur die, die schon in der gespeicherten Definition stehen – neu genannte Ids kommen aus der
//    aktuellen Abfrage befunde (Befunde-Browser des Editors); ihre Version ist die aktuelle. Sonst (familie_entwurf, familie_laden,
//    konflikt_entscheiden, wert_bestaetigen, Freigabe, Prüfung, Sammelbefehl) alle Ids der Definition, Version aus ihrem leser bzw. Altbestand.
//  - «Wertprüfung bestehen» bei der Versionswahl: passt (geprüft) und ungeprüft (Bild, Linie) bestehen, weicht/Einheit/keine Quelle/fehlt
//    nicht; Belege ohne Wertvergleich (Körper, Anschlüsse, Masse, Formelparameter, Quellen ohne Wert, Block leitung) bestehen, wenn es den
//    Befund gibt. Bestehen mehrere Versionen vollständig, gilt die mit den meisten geprüften Werten (passt), bei Gleichstand die aktuelle
//    (sonst die höhere) – damit ungeprüfte Zufallstreffer der falschen Version (Linien, Bilder) nicht die aktuelle wählen lassen.
//  - Text ohne Leerraum (Echtdaten 21.10.2026): PDF-Leser 2 setzt die Glyphe « .» als «.» – «Nr art . | DN» (1) und «Nr art. | DN» (2) sind
//    derselbe Text. Die Art bleibt streng: eine Zelle, die Leser 2 nur noch als Wort liest (Tabelle nicht mehr erkannt), ist nicht zuordenbar.
//  - Ein gespeicherter leser mit anderem Namen oder höherer Version als der aktuelle Leser wird ebenso umgeschlüsselt, wenn es seine Ablage gibt.
//  - Konflikte und Abdeckung: Ids in konflikte[].werte[].quellen und abdeckung[].quellen werden mit umgeschlüsselt, die Konflikt-Id neu
//    gebildet (FamilienQuellenAnalyse.KonfliktId) – Entscheide bleiben so erhalten. Texte (bericht, beschreibung, stellen) bleiben, wie sie sind.
//  - Der Sammelbefehl speichert nur Familien, deren Ids von einer anderen Version umgeschlüsselt werden; Familien, deren Quellen schon nach dem
//    aktuellen Leser gelten, bleiben ohne Änderung (leser setzt das nächste Speichern). Gespeichert wird wie bei familie_aendern: Status
//    entwurf (eine Freigabe fällt weg), Version + 1.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Hlks.Kern;

/// <summary>Zuordnung einer Befund-Id der alten Leserversion: neue Id oder Grund, warum keine.</summary>
public sealed record BefundZuordnung(string Alt, string? Neu, string? Grund);

/// <summary>Ordnet die Befunde einer Leserversion denen einer anderen zu: gleiche Seite bzw. Layout, gleiche Art, gleicher Wert, Box ±0.5;
/// bei mehreren Kandidaten zusätzlich Lage, Zusatz (ohne Verweise auf Ids), Layer und Einheit. Eindeutig oder gar nicht; deckungsgleiche
/// Doppel (auch die Box gleich) gelten als ein Befund, der erste in Lesereihenfolge.</summary>
public sealed class BefundZuordner
{
    /// <summary>Toleranz für Box und Lage in Einheiten der Unterlage (PDF pt, DWG Zeichnungseinheiten, 3D mm).</summary>
    public const double Toleranz = 0.5;

    readonly record struct Schluessel(int Seite, string Layout, string Art, string Wert);

    readonly Dictionary<string, UnterlagenBefund> alt = new(StringComparer.Ordinal);
    readonly Dictionary<Schluessel, List<UnterlagenBefund>> neu = [];
    readonly int altAnzahl;
    public string AltLeser { get; }
    public string NeuLeser { get; }

    public BefundZuordner(IReadOnlyList<UnterlagenBefund> altBefunde, IReadOnlyList<UnterlagenBefund> neuBefunde, string altLeser, string neuLeser)
    {
        AltLeser = altLeser; NeuLeser = neuLeser;
        foreach (var b in altBefunde) alt.TryAdd(b.Id, b);
        altAnzahl = altBefunde.Count;
        foreach (var b in neuBefunde)
        {
            var k = SchluesselVon(b);
            if (!neu.TryGetValue(k, out var l)) neu[k] = l = [];
            l.Add(b);
        }
    }

    static Schluessel SchluesselVon(UnterlagenBefund b) => new(b.Seite ?? 0, b.Layout ?? "", b.Art, WertText(b.Wert));

    /// <summary>Text ohne Leerraum: «Nr art .» (Leser 1, Glyphe « .») und «Nr art.» (Leser 2) sind derselbe Text.</summary>
    static string OhneLeerraum(string s) => s.Any(char.IsWhiteSpace) ? new string(s.Where(c => !char.IsWhiteSpace(c)).ToArray()) : s;

    /// <summary>Wert als Schlüssel: Text (ohne Leerraum) und Zahl genau; Objekte und Listen kommen in einen Topf und werden einzeln verglichen.</summary>
    static string WertText(JsonNode? w) => w switch
    {
        null => "-",
        JsonValue v => v.GetValueKind() switch
        {
            JsonValueKind.String => "s:" + OhneLeerraum(v.GetValue<string>()),
            JsonValueKind.Number => "z:" + (Json.Zahl(v) ?? double.NaN).ToString("R", CultureInfo.InvariantCulture),
            var k => "w:" + k + ":" + v.ToJsonString(),
        },
        _ => "o",
    };

    /// <summary>Wert zum Vergleich: ohne die Nummern anderer Befunde (Tabelle: Matrix «zellen» der Zellnummern), Texte ohne Leerraum.</summary>
    static JsonNode? WertNormal(UnterlagenBefund b)
    {
        if (b.Wert is not (JsonObject or JsonArray)) return b.Wert;
        var k = b.Wert.DeepClone();
        if (b.Art == "tabelle" && k is JsonObject o) o.Remove("zellen");
        return TexteOhneLeerraum(k);
    }

    static JsonNode? TexteOhneLeerraum(JsonNode? n)
    {
        switch (n)
        {
            case JsonValue v when v.GetValueKind() == JsonValueKind.String:
                return JsonValue.Create(OhneLeerraum(v.GetValue<string>()));
            case JsonObject o:
                foreach (var key in o.Select(x => x.Key).ToList()) o[key] = TexteOhneLeerraum(o[key]?.DeepClone());
                return o;
            case JsonArray a:
                for (int i = 0; i < a.Count; i++) a[i] = TexteOhneLeerraum(a[i]?.DeepClone());
                return a;
            default:
                return n;
        }
    }

    static readonly string[] Verweise = ["tabelle", "zellenAb", "typBefund", "nummer"];

    /// <summary>Zusatz ohne Verweise auf andere Befund-Ids und ohne laufende Tabellennummer.</summary>
    static JsonNode? ZusatzNormal(UnterlagenBefund b)
    {
        if (b.Zusatz is null || !Verweise.Any(b.Zusatz.ContainsKey)) return b.Zusatz;
        var k = (JsonObject)b.Zusatz.DeepClone();
        foreach (var v in Verweise) k.Remove(v);
        return k;
    }

    static bool Nah(double[]? a, double[]? b) =>
        (a is null && b is null) || (a is not null && b is not null && a.Length == b.Length && a.Zip(b).All(x => Math.Abs(x.First - x.Second) <= Toleranz + 1e-9));

    static bool NahLage(List<double[]>? a, List<double[]>? b) =>
        (a is null && b is null) || (a is not null && b is not null && a.Count == b.Count && a.Zip(b).All(x => Nah(x.First, x.Second)));

    static List<UnterlagenBefund> Enger(List<UnterlagenBefund> l, Func<UnterlagenBefund, bool> f)
    {
        if (l.Count < 2) return l;
        var x = l.Where(f).ToList();
        return x.Count > 0 ? x : l;
    }

    /// <summary>Kurzbeschreibung eines Befunds für Meldungen: Stelle, Art, Wert, Box.</summary>
    public static string Beschreiben(UnterlagenBefund b)
    {
        var w = b.Wert switch { null => "", JsonValue v when v.GetValueKind() == JsonValueKind.String => v.GetValue<string>(), var n => n.ToJsonString() };
        if (w.Length > 50) w = w[..50] + "…";
        var stelle = b.Seite is int s ? $"Seite {s}" : b.Layout is { } l ? $"Layout {l}" : "";
        var box = b.Box is { Length: 4 } x ? $", Box {string.Join(" ", x.Select(z => Math.Round(z, 1).ToString(CultureInfo.InvariantCulture)))}" : "";
        return $"{stelle}{(stelle.Length > 0 ? ", " : "")}{b.Art}{(w.Length > 0 ? $" «{w}»" : "")}{box}";
    }

    /// <summary>Für die Meldung: eine Zelle, die die neue Version an derselben Stelle nur noch als Wort liest (Tabelle nicht mehr erkannt) –
    /// nicht zugeordnet (andere Art), aber als Hinweis für die neue Quelle genannt.</summary>
    string AlsWort(UnterlagenBefund b)
    {
        if (b.Art != "zelle") return "";
        var text = b.Zusatz?["text"] is JsonValue tv && tv.TryGetValue<string>(out var t) ? t : b.Wert?.ToString();
        if (string.IsNullOrWhiteSpace(text) || !neu.TryGetValue(new Schluessel(b.Seite ?? 0, b.Layout ?? "", "wort", "s:" + OhneLeerraum(text)), out var w)) return "";
        var da = w.Where(n => Nah(b.Box, n.Box)).ToList();
        return da.Count == 1 ? $" – an derselben Stelle liest Leser {NeuLeser} nur das Wort «{text}» ({da[0].Id}), keine Tabelle mehr" : "";
    }

    static bool GleicheBox(double[]? a, double[]? b) =>
        (a is null && b is null) || (a is not null && b is not null && a.Length == b.Length && a.Zip(b).All(x => Math.Abs(x.First - x.Second) <= 1e-6));

    /// <summary>Laufnummer einer Befund-Id «präfix#nummer» (Lesereihenfolge); ohne Nummer ans Ende.</summary>
    static long Nummer(string id) => long.TryParse(id[(id.LastIndexOf('#') + 1)..], out var n) ? n : long.MaxValue;

    /// <summary>Befund der alten Version zur Id (null, wenn es ihn nicht gibt).</summary>
    public UnterlagenBefund? Alt(string id) => alt.GetValueOrDefault(id);

    public BefundZuordnung Zuordnen(string id)
    {
        if (!alt.TryGetValue(id, out var b))
            return new(id, null, $"den Befund gibt es in der Ablage von Leser {AltLeser} nicht ({altAnzahl} Befunde)");
        var k = SchluesselVon(b);
        var kand = neu.TryGetValue(k, out var l) ? l : [];
        if (k.Wert == "o")
        {
            var wn = WertNormal(b);
            kand = kand.Where(n => JsonNode.DeepEquals(wn, WertNormal(n))).ToList();
        }
        var r = kand.Where(n => Nah(b.Box, n.Box)).ToList();
        if (r.Count == 0)
            return new(id, null, $"kein Befund mit gleicher Seite, Art, Wert und Lage in Leser {NeuLeser} ({Beschreiben(b)})" +
                (kand.Count > 0 ? $" – {kand.Count} mit gleichem Wert an anderer Stelle" : "") + AlsWort(b));
        r = Enger(r, n => JsonNode.DeepEquals(b.Wert, n.Wert) || b.Art == "tabelle");   // genau gleicher Text vor «gleich ohne Leerraum»
        r = Enger(r, n => NahLage(b.Lage, n.Lage));
        r = Enger(r, n => JsonNode.DeepEquals(ZusatzNormal(b), ZusatzNormal(n)));
        r = Enger(r, n => n.Layer == b.Layer && n.Einheit == b.Einheit);
        if (r.Count == 1) return new(id, r[0].Id, null);
        // Doppelt gezeichnet (30.10.2026, FAM-24 Gewinderohr: dieselbe Linie zweimal auf Seite 20): Kandidaten, die sich auch in der Box
        // nicht unterscheiden, sind gleichwertig – der erste in Lesereihenfolge gilt, statt die ganze Familie liegen zu lassen.
        if (r.All(n => GleicheBox(n.Box, r[0].Box)))
            return new(id, r.OrderBy(n => Nummer(n.Id)).First().Id, null);
        return new(id, null, $"nicht eindeutig: {r.Count} Befunde mit gleicher Seite, Art, Wert und Lage in Leser {NeuLeser} ({Beschreiben(b)}: " +
            $"{string.Join(", ", r.Take(5).Select(x => x.Id))}{(r.Count > 5 ? " …" : "")})");
    }
}

/// <summary>Leserversion der Quellen einer Unterlage in einer Familie.</summary>
public sealed class UnterlagenVersion
{
    public required string Praefix { get; init; }
    public string? Datei { get; set; }
    public string? Art { get; set; }
    /// <summary>unterlagen[].leser der Definition (null = Altbestand ohne leser).</summary>
    public string? Gespeichert { get; set; }
    /// <summary>Aktueller Leser der Art, z. B. «pdf-2».</summary>
    public string? Aktuell { get; set; }
    /// <summary>Version, nach der die Ids gelten (gespeichert, beim Altbestand gewählt).</summary>
    public string? Quelle { get; set; }
    /// <summary>Altbestand: wie die Version gewählt wurde.</summary>
    public string? Wahl { get; set; }
    /// <summary>Die Befunde der Version Quelle fehlen im Projekt: nicht umschlüsselbar, Wertprüfung entfällt.</summary>
    public bool AltFehlt { get; set; }
    /// <summary>Ids dieser Unterlage, die nach Quelle gelten.</summary>
    public int Ids { get; set; }
    /// <summary>Davon auf die aktuelle Version zugeordnet (nur wenn Quelle ≠ Aktuell).</summary>
    public int Umgeschluesselt { get; set; }
    /// <summary>Davon mit anderer Nummer.</summary>
    public int Geaendert { get; set; }
    public List<BefundZuordnung> NichtZuordenbar { get; } = [];
    public List<(string Alt, string Neu)> Beispiele { get; } = [];
    public bool Umzuschluesseln => Quelle is not null && Aktuell is not null && Quelle != Aktuell;

    public JsonObject AlsJson() => new()
    {
        ["datei"] = Datei, ["pruefsumme"] = Praefix, ["art"] = Art, ["leser"] = Gespeichert, ["quelle"] = Quelle, ["aktuell"] = Aktuell,
        ["wahl"] = Wahl, ["altFehlt"] = AltFehlt, ["ids"] = Ids, ["umgeschluesselt"] = Umgeschluesselt, ["geaendert"] = Geaendert,
        ["nichtZuordenbar"] = new JsonArray(NichtZuordenbar.Select(z => (JsonNode)new JsonObject { ["id"] = z.Alt, ["grund"] = z.Grund }).ToArray()),
    };
}

/// <summary>Ergebnis von <see cref="FamilienQuellenVersion.Nachfuehren"/> für eine Familie.</summary>
public sealed class QuellenNachfuehrung
{
    public List<UnterlagenVersion> Unterlagen { get; } = [];
    /// <summary>Alte Id → neue Id (nur Ids einer anderen Leserversion, die eindeutig zugeordnet wurden).</summary>
    public Dictionary<string, string> Zuordnung { get; } = new(StringComparer.Ordinal);
    /// <summary>Nicht zuordenbare Ids mit Stelle, Grund und den beiden Versionen.</summary>
    public List<(string Stelle, string Id, string Grund, string Von, string Nach)> NichtZuordenbar { get; } = [];
    public List<string> Warnungen { get; } = [];
    public List<string> Hinweise { get; } = [];
    public int Umgeschluesselt => Unterlagen.Sum(u => u.Umgeschluesselt);
    public int Geaendert => Unterlagen.Sum(u => u.Geaendert);
    /// <summary>Ersetzte Vorkommen in den Quellenlisten der Definition.</summary>
    public int Ersetzt { get; set; }
    /// <summary>Es gibt Ids einer anderen Version, die sich umschlüsseln lassen (bzw. es versucht wurde).</summary>
    public bool Noetig => Unterlagen.Any(u => u.Umzuschluesseln && !u.AltFehlt);
}

public static class FamilienQuellenVersion
{
    /// <summary>«&lt;name&gt;-&lt;version&gt;» eines Lesers, z. B. «pdf-2» – wie im Namen der Ablagedatei.</summary>
    public static string Text(IUnterlagenLeser l) => $"{l.Name}-{l.Version}";

    static string? T(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    static IEnumerable<string> Ids(JsonArray a) => a.Select(T).Where(x => x is not null).Select(x => x!);

    /// <summary>Alle Quellenlisten der Definition mit ihrer Stelle: parameter, typen, koerper, anschluesse, masse und der Block leitung.</summary>
    public static IEnumerable<(string Stelle, JsonArray Liste)> Listen(JsonObject def)
    {
        foreach (var o in (def["parameter"] as JsonArray ?? []).OfType<JsonObject>())
            if (o["quellen"] is JsonArray a) yield return ($"parameter «{T(o["name"])}».quellen", a);
        foreach (var o in (def["typen"] as JsonArray ?? []).OfType<JsonObject>())
            if (o["quellen"] is JsonObject q)
                foreach (var (pn, l) in q) if (l is JsonArray a) yield return ($"typ «{T(o["name"])}».quellen.{pn}", a);
        foreach (var o in (def["koerper"] as JsonArray ?? []).OfType<JsonObject>())
            if (o["quellen"] is JsonArray a) yield return ($"koerper «{T(o["id"])}».quellen", a);
        foreach (var o in (def["anschluesse"] as JsonArray ?? []).OfType<JsonObject>())
            if (o["quellen"] is JsonArray a) yield return ($"anschluss «{T(o["name"])}».quellen", a);
        foreach (var o in (def["masse"] as JsonArray ?? []).OfType<JsonObject>())
            if (o["quellen"] is JsonArray a) yield return ($"mass «{T(o["name"])}».quellen", a);
        if (def["leitung"] is JsonObject lo)
        {
            if (lo["quellen"] is JsonArray a) yield return ("leitung.quellen", a);
            if (lo["frei"] is JsonObject fo && fo["quellen"] is JsonArray fa) yield return ("leitung.frei.quellen", fa);
        }
    }

    /// <summary>Ids der Quellen des Blocks leitung (Leitungsserie, Modul 3).</summary>
    public static IEnumerable<string> LeitungQuellen(JsonObject def) =>
        Listen(def).Where(x => x.Stelle.StartsWith("leitung", StringComparison.Ordinal)).SelectMany(x => Ids(x.Liste));

    /// <summary>unterlagen[].leser je Prüfsumme (12 Zeichen).</summary>
    static Dictionary<string, string> Leser(JsonObject def)
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var u in (def["unterlagen"] as JsonArray ?? []).OfType<JsonObject>())
            if (T(u["pruefsumme"]) is { Length: >= 12 } s && T(u["leser"]) is { Length: > 0 } l) d[s[..12].ToLowerInvariant()] = l.Trim();
        return d;
    }

    /// <summary>Ablagedatei der Befunde einer Unterlage für eine Leserversion («pdf-1»).</summary>
    public static string AblagePfad(Projekt p, string praefix, string leser) => Path.Combine(UnterlagenService.BefundOrdner(p), $"{praefix}.{leser}.json");

    /// <summary>Andere Versionen desselben Lesers in der Ablage (ohne die aktuelle), absteigend nach Version.</summary>
    static List<(string Leser, int Nr, string Pfad)> AndereVersionen(Projekt p, string praefix, IUnterlagenLeser l)
    {
        var ordner = UnterlagenService.BefundOrdner(p);
        var l2 = new List<(string, int, string)>();
        if (!Directory.Exists(ordner)) return l2;
        var muster = new Regex("^" + Regex.Escape(praefix) + @"\." + Regex.Escape(l.Name) + @"-(\d+)\.json$", RegexOptions.IgnoreCase);
        foreach (var f in Directory.EnumerateFiles(ordner, $"{praefix}.{l.Name}-*.json"))
            if (muster.Match(Path.GetFileName(f)) is { Success: true } m && int.TryParse(m.Groups[1].Value, out var nr) && nr != l.Version)
                l2.Add(($"{l.Name}-{nr}", nr, f));
        return [.. l2.OrderByDescending(x => x.Item2)];
    }

    // ---------- Zwischenspeicher (Ablagen alter Versionen und Zuordner; der Sammelbefehl braucht sie für viele Familien) ----------

    sealed record AltEintrag(string Pfad, long Laenge, long Zeit, UnterlagenBefunde Befunde);
    sealed record ZuordnerEintrag(string AltPfad, long Laenge, long Zeit, UnterlagenBefunde Neu, BefundZuordner Zuordner);
    static readonly List<AltEintrag> altSpeicher = [];
    static readonly List<ZuordnerEintrag> zuordnerSpeicher = [];
    static readonly object sperre = new();
    const int MaxSpeicher = 2;

    /// <summary>Befunde aus der Ablagedatei einer älteren Version (null, wenn sie fehlt oder unlesbar ist).</summary>
    static UnterlagenBefunde? AltLaden(string pfad)
    {
        if (!File.Exists(pfad)) return null;
        var fi = new FileInfo(pfad);
        lock (sperre)
        {
            var e = altSpeicher.FirstOrDefault(x => x.Pfad == pfad && x.Laenge == fi.Length && x.Zeit == fi.LastWriteTimeUtc.Ticks);
            if (e is not null) return e.Befunde;
        }
        UnterlagenBefunde? b;
        try { b = UnterlagenService.Laden(pfad); }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException) { return null; }
        if (b is null) return null;
        lock (sperre)
        {
            altSpeicher.RemoveAll(x => x.Pfad == pfad);
            altSpeicher.Insert(0, new AltEintrag(pfad, fi.Length, fi.LastWriteTimeUtc.Ticks, b));
            while (altSpeicher.Count > MaxSpeicher) altSpeicher.RemoveAt(altSpeicher.Count - 1);
        }
        return b;
    }

    static BefundZuordner Zuordner(string altPfad, UnterlagenBefunde alt, UnterlagenBefunde neu, string altLeser, string neuLeser)
    {
        var fi = new FileInfo(altPfad);
        long laenge = fi.Exists ? fi.Length : -1, zeit = fi.Exists ? fi.LastWriteTimeUtc.Ticks : -1;
        lock (sperre)
        {
            var e = zuordnerSpeicher.FirstOrDefault(x => x.AltPfad == altPfad && x.Laenge == laenge && x.Zeit == zeit && ReferenceEquals(x.Neu, neu));
            if (e is not null) return e.Zuordner;
        }
        var z = new BefundZuordner(alt.Befunde, neu.Befunde, altLeser, neuLeser);
        lock (sperre)
        {
            zuordnerSpeicher.Insert(0, new ZuordnerEintrag(altPfad, laenge, zeit, neu, z));
            while (zuordnerSpeicher.Count > MaxSpeicher) zuordnerSpeicher.RemoveAt(zuordnerSpeicher.Count - 1);
        }
        return z;
    }

    // ---------- Versionswahl beim Altbestand ----------

    /// <summary>Ergebnis der Wertprüfung der Quellen einer Unterlage unter einer Version: Gesamt, bestanden, davon geprüft (passt).</summary>
    public readonly record struct Bewertung(int Gesamt, int Bestanden, int Geprueft)
    {
        public bool Vollstaendig => Bestanden == Gesamt;
        public override string ToString() => $"{Bestanden} von {Gesamt} Quellen bestehen ({Geprueft} geprüft)";
    }

    /// <summary>Werte mit Quellen dieser Unterlage (wie FamilienQuellenAnalyse.Rechnen, Schritt 2): Parameter, Wert, Id.</summary>
    static List<(FamParameter P, FWert W, string Id)> Werte(FamilieDef f, JsonObject def, string praefix)
    {
        var l = new List<(FamParameter, FWert, string)>();
        var parameterJson = (def["parameter"] as JsonArray ?? []).OfType<JsonObject>().GroupBy(o => T(o["name"])?.Trim() ?? "").ToDictionary(g => g.Key, g => g.First());
        var typJson = (def["typen"] as JsonArray ?? []).OfType<JsonObject>().GroupBy(o => T(o["name"])?.Trim() ?? "").ToDictionary(g => g.Key, g => g.First());
        bool Von(string id) => BefundQuelle.Praefix(id) == praefix;
        void Dazu(FamParameter p, JsonNode? wert, IEnumerable<string> ids)
        {
            if (wert is null) return;
            var fw = p.Umwandeln(wert, out var fehler);
            if (fw is null || fehler is not null) return;
            foreach (var id in ids.Where(Von)) l.Add((p, fw.Value, id));
        }
        foreach (var p in f.Parameter)
        {
            if (p.HatFormel) continue;   // Belege, kein Wert
            if (parameterJson.TryGetValue(p.Name, out var po) && po["quellen"] is JsonArray pq) Dazu(p, p.Wert, Ids(pq));
            foreach (var t in f.Typen)
                if (typJson.TryGetValue(t.Name, out var to) && (to["quellen"] as JsonObject)?[p.Name] is JsonArray tq)
                    Dazu(p, t.Werte.TryGetValue(p.Name, out var tw) && tw is not null ? tw : p.Wert, Ids(tq));
        }
        return l;
    }

    /// <summary>Wertprüfung der Quellen einer Unterlage gegen die Befunde der Quelle <paramref name="q"/> (aktuell oder vorgegeben).</summary>
    public static Bewertung Bewerten(FamilieDef? f, JsonObject def, string praefix, BefundQuelle q)
    {
        int gesamt = 0, bestanden = 0, geprueft = 0;
        var werte = f is { Gueltig: true } ? Werte(f, def, praefix) : [];
        foreach (var (p, w, id) in werte)
        {
            gesamt++;
            var r = QuellVergleiche.Vergleichen(f!, p, w, id, q);
            if (r.Status == "passt") { bestanden++; geprueft++; }
            else if (r.Status == "ungeprueft") bestanden++;
        }
        var mitWert = werte.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var id in Listen(def).SelectMany(x => Ids(x.Liste)).Where(id => BefundQuelle.Praefix(id) == praefix && !mitWert.Contains(id)).Distinct())
        {
            gesamt++;
            if (q.Hole(id, out _) is not null) bestanden++;
        }
        return new Bewertung(gesamt, bestanden, geprueft);
    }

    // ---------- Umschlüsseln ----------

    /// <summary>Führt die Quellen einer Definition auf die aktuelle Leserversion nach: bestimmt je Unterlage die Version der Ids
    /// (unterlagen[].leser bzw. Altbestand), schlüsselt Ids einer anderen Version eindeutig um und ersetzt sie in <paramref name="def"/>
    /// (Quellenlisten, Konflikte, Abdeckung). Nicht zuordenbare Ids und Ids einer fehlenden Version bleiben stehen; ihre Wertprüfung entfällt
    /// (<see cref="BefundQuelle.Ueberspringen"/> auf <paramref name="q"/>). <paramref name="referenz"/>: Definition, deren Ids als alt gelten
    /// (bei familie_aendern die gespeicherte); null = alle Ids von <paramref name="def"/>.</summary>
    public static QuellenNachfuehrung Nachfuehren(Projekt p, JsonObject def, JsonObject? referenz, BefundQuelle q)
    {
        var n = new QuellenNachfuehrung();
        var refDef = referenz ?? def;
        var stellen = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (stelle, liste) in Listen(def)) foreach (var id in Ids(liste)) stellen.TryAdd(id, stelle);
        if (stellen.Count == 0) return n;
        var alt = ReferenceEquals(refDef, def) ? stellen.Keys.ToHashSet(StringComparer.Ordinal)
            : Listen(refDef).SelectMany(x => Ids(x.Liste)).ToHashSet(StringComparer.Ordinal);
        var leser = Leser(refDef);
        FamilieDef? fRef = null;
        bool fGelesen = false;

        foreach (var g in stellen.Keys.Where(alt.Contains).GroupBy(id => BefundQuelle.Praefix(id) ?? "").Where(g => g.Key.Length > 0).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var px = g.Key;
            var ids = g.OrderBy(Nummer).ToList();
            var u = q.Unterlage(px);
            if (u is null) continue;   // nicht im Projekt bzw. nicht lesbar: wie bisher (Verweis bzw. Fehler der Quellenprüfung)
            var l = UnterlagenLeser.Fuer(u.Art);
            if (l is null) continue;
            var st = new UnterlagenVersion { Praefix = px, Datei = u.Original, Art = u.Art, Gespeichert = leser.GetValueOrDefault(px), Aktuell = Text(l), Ids = ids.Count };
            n.Unterlagen.Add(st);
            UnterlagenBefunde? altBefunde;
            string altPfad;
            if (st.Gespeichert is { } gs)
            {
                st.Quelle = gs;
                if (string.Equals(gs, st.Aktuell, StringComparison.OrdinalIgnoreCase)) { st.Quelle = st.Aktuell; continue; }
                altPfad = AblagePfad(p, px, gs);
                altBefunde = AltLaden(altPfad);
                if (altBefunde is null)
                {
                    st.AltFehlt = true;
                    var grund = $"Quellen nach Leser {gs}, Befunde dieser Version fehlen im Projekt – Quellen neu zuordnen";
                    n.Warnungen.Add($"{u.Original} ({px}): {grund} ({ids.Count} Id(s); ihre Wertprüfung entfällt, Ids und leser bleiben).");
                    foreach (var id in ids) q.Ueberspringen(id, grund);
                    continue;
                }
            }
            else
            {
                // Altbestand ohne leser: Version nach der Wertprüfung wählen
                var andere = AndereVersionen(p, px, l);
                if (andere.Count == 0) { st.Quelle = st.Aktuell; continue; }
                if (!fGelesen) { fRef = FamilieDef.Lesen(refDef, p.Katalog); fGelesen = true; }
                var kandidaten = new List<(string Leser, int Nr, bool Aktuell, string? Pfad, UnterlagenBefunde? B, Bewertung W)>
                {
                    (st.Aktuell, l.Version, true, null, null, Bewerten(fRef, refDef, px, q)),
                };
                foreach (var (al, nr, pfad) in andere)
                    if (AltLaden(pfad) is { } b)
                        kandidaten.Add((al, nr, false, pfad, b, Bewerten(fRef, refDef, px, new BefundQuelle(p).Vorgeben(px, b))));
                var vollstaendig = kandidaten.Where(x => x.W.Vollstaendig).ToList();
                var wahl = vollstaendig.Count > 0
                    ? vollstaendig.OrderByDescending(x => x.W.Geprueft).ThenByDescending(x => x.Aktuell).ThenByDescending(x => x.Nr).First()
                    : kandidaten.OrderByDescending(x => x.W.Bestanden).ThenByDescending(x => x.W.Geprueft).ThenByDescending(x => x.Aktuell).ThenByDescending(x => x.Nr).First();
                st.Quelle = wahl.Leser;
                st.Wahl = $"ohne leser: {wahl.Leser} gewählt – " + string.Join("; ", kandidaten.Select(x => $"{x.Leser}: {x.W}"));
                if (wahl.Aktuell) continue;
                altPfad = wahl.Pfad!;
                altBefunde = wahl.B!;
            }
            var neu = q.BefundeVon(px);
            if (neu is null) continue;
            var z = Zuordner(altPfad, altBefunde, neu, st.Quelle!, st.Aktuell!);
            foreach (var id in ids)
            {
                var r = z.Zuordnen(id);
                if (r.Neu is null)
                {
                    st.NichtZuordenbar.Add(r);
                    n.NichtZuordenbar.Add((stellen[id], id, r.Grund!, st.Quelle!, st.Aktuell!));
                    q.Ueberspringen(id, $"Quelle nach Leser {st.Quelle}, nicht eindeutig auf {st.Aktuell} umschlüsselbar ({r.Grund})");
                    continue;
                }
                n.Zuordnung[id] = r.Neu;
                st.Umgeschluesselt++;
                if (r.Neu != id) { st.Geaendert++; if (st.Beispiele.Count < 3) st.Beispiele.Add((id, r.Neu)); }
            }
            n.Hinweise.Add($"{u.Original}: {st.Umgeschluesselt} Quelle(n) von Leser {st.Quelle} auf {st.Aktuell} umgeschlüsselt ({st.Geaendert} mit neuer Nummer" +
                (st.Beispiele.Count > 0 ? ", z. B. " + string.Join(", ", st.Beispiele.Select(x => $"#{Nummer(x.Alt)} → #{Nummer(x.Neu)}")) : "") + ")" +
                (st.NichtZuordenbar.Count > 0 ? $"; {st.NichtZuordenbar.Count} nicht eindeutig zuordenbar" : "") + (st.Wahl is null ? "." : $" – {st.Wahl}."));
        }
        n.Ersetzt = Anwenden(def, n.Zuordnung);
        return n;
    }

    static long Nummer(string id) => id.LastIndexOf('#') is int i and >= 0 && long.TryParse(id[(i + 1)..], out var nr) ? nr : long.MaxValue;

    /// <summary>Ersetzt Ids nach der Zuordnung (alt → neu) in den Quellenlisten, in konflikte[].werte[].quellen (Konflikt-Id neu gebildet)
    /// und in abdeckung (quellen, konflikt). Gibt die Zahl der ersetzten Vorkommen in den Quellenlisten zurück.</summary>
    public static int Anwenden(JsonObject def, IReadOnlyDictionary<string, string> zuordnung)
    {
        if (zuordnung.Count == 0) return 0;
        int n = 0;
        foreach (var (_, liste) in Listen(def)) n += Ersetzen(liste, zuordnung);
        var konfliktIds = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var k in (def["konflikte"] as JsonArray ?? []).OfType<JsonObject>())
        {
            int ersetzt = 0;
            foreach (var w in (k["werte"] as JsonArray ?? []).OfType<JsonObject>())
                if (w["quellen"] is JsonArray ql) ersetzt += Ersetzen(ql, zuordnung);
            if (ersetzt == 0 || T(k["id"]) is not { } altId || T(k["parameter"]) is not { } par) continue;
            var quellen = (k["werte"] as JsonArray ?? []).OfType<JsonObject>().SelectMany(w => w["quellen"] is JsonArray ql ? Ids(ql) : []);
            var neuId = FamilienQuellenAnalyse.KonfliktId(par, T(k["typ"]), quellen);
            k["id"] = neuId;
            konfliktIds[altId] = neuId;
        }
        foreach (var (_, v) in def["abdeckung"] as JsonObject ?? [])
            if (v is JsonObject a)
            {
                if (a["quellen"] is JsonArray ql) Ersetzen(ql, zuordnung);
                if (T(a["konflikt"]) is { } ki && konfliktIds.TryGetValue(ki, out var nk)) a["konflikt"] = nk;
            }
        return n;
    }

    static int Ersetzen(JsonArray a, IReadOnlyDictionary<string, string> zuordnung)
    {
        int n = 0;
        for (int i = 0; i < a.Count; i++)
            if (T(a[i]) is { } id && zuordnung.TryGetValue(id, out var neu) && neu != id) { a[i] = neu; n++; }
        return n;
    }
}

/// <summary>Abfrage familie_pruefen (Nachtrag 21.10.2026): Prüfbefunde einer gespeicherten Familie, ohne etwas zu ändern.</summary>
public static class FamiliePruefen
{
    public const string Name = "familie_pruefen";
    public const string Text = "Prüft eine gespeicherte Familie, ohne etwas zu ändern: Definition und Typen, Quellen gegen die Befunde der Unterlagen (Wert gegen Befund), " +
        "offene Konflikte, Pflichtwerte ohne Quelle, Massvergleich und die Leserversion der Quellen (unterlagen[].leser). Quellen nach einer älteren Leserversion " +
        "werden dafür im Speicher auf die aktuelle umgeschlüsselt (gleiche Seite, Art, Wert und Lage). befunde: je Punkt schwere (fehler | warnung | hinweis), art, " +
        "stelle, text und vorschlag – fehler sperren das Speichern, warnungen die Freigabe; unterlagen: je Unterlage gespeicherte, gewählte und aktuelle Leserversion, " +
        "umzuschlüsselnde und nicht zuordenbare Ids. Gespeichert wird die Umschlüsselung mit dem nächsten familie_aendern bzw. mit familien_quellen_nachfuehren.";
    public static readonly IReadOnlyList<ParameterDef> Parameter = [new("familie", "text", true, "Familie (Kennzeichen, Name oder id)")];

    static JsonObject Punkt(string schwere, string art, string stelle, string text, string? vorschlag, string? code = null) => new()
    {
        ["code"] = code, ["schwere"] = schwere, ["art"] = art, ["stelle"] = stelle, ["text"] = text, ["vorschlag"] = vorschlag,
    };

    public static JsonObject Abfrage(Projekt p, string familie)
    {
        var fam = Familien.Hole(p.Modell, familie);
        var s = FamilienFreigabe.Pruefen(p, fam, p.Katalog, p.AkteurArt);
        var befunde = new JsonArray();
        var n = s.Nachfuehrung;
        foreach (var x in s.DefinitionFehler)
            befunde.Add(Punkt("fehler", "definition", x.Stelle, x.Meldung, x.Vorschlag));
        foreach (var x in n?.NichtZuordenbar ?? [])
            befunde.Add(Punkt("fehler", "leserversion", x.Stelle, $"Quelle «{x.Id}» (Befund-Id nach Leser {x.Von}) lässt sich nicht eindeutig auf Leser {x.Nach} umschlüsseln: {x.Grund}",
                "Den Befund in der aktuellen Fassung suchen (Abfrage befunde der Unterlage) und die Quelle mit seiner Id neu nennen (familie_aendern).", "P32"));
        foreach (var x in s.Quellen?.Fehler ?? [])
            befunde.Add(Punkt("fehler", "quelle", x.Stelle, x.Meldung, x.Vorschlag));
        foreach (var w in n?.Warnungen ?? [])
            befunde.Add(Punkt("warnung", "leserversion", "unterlagen", w, "Quellen neu zuordnen: Befunde der Unterlage mit der Abfrage befunde (aktueller Leser) ansehen und die Ids in der Familie ersetzen.", "P31"));
        foreach (var x in s.Offen)
            befunde.Add(Punkt("warnung", x.Stelle.StartsWith("Konflikt", StringComparison.Ordinal) ? "konflikt" : x.Stelle.StartsWith("mass", StringComparison.Ordinal) ? "mass" : "abdeckung",
                x.Stelle, x.Meldung + " – sperrt die Freigabe", x.Vorschlag));
        foreach (var u in (n?.Unterlagen ?? []).Where(u => u.Umzuschluesseln && !u.AltFehlt && u.Umgeschluesselt > 0))
            befunde.Add(Punkt("hinweis", "leserversion", $"unterlagen «{u.Datei}»",
                $"{u.Umgeschluesselt} Quelle(n) nach Leser {u.Quelle} – für die Prüfung auf {u.Aktuell} umgeschlüsselt ({u.Geaendert} mit neuer Nummer){(u.Wahl is null ? "" : $"; {u.Wahl}")}.",
                "Speichern mit der neuen Version: familien_quellen_nachfuehren (bzw. das nächste familie_aendern)."));
        int Zahl(string schwere) => befunde.Count(b => b?["schwere"]?.GetValue<string>() == schwere);
        return new JsonObject
        {
            ["familie"] = fam.Kennzeichen, ["name"] = fam.Name, ["id"] = Familien.DefinitionsId(fam), ["version"] = fam.Zahl("version"), ["status"] = fam.Text("status"),
            ["gueltig"] = Zahl("fehler") == 0, ["freigebbar"] = s.Sperren.Count == 0 && (n?.Warnungen.Count ?? 0) == 0,
            ["anzahl"] = new JsonObject { ["fehler"] = Zahl("fehler"), ["warnungen"] = Zahl("warnung"), ["hinweise"] = Zahl("hinweis") },
            ["befunde"] = befunde,
            ["unterlagen"] = new JsonArray((n?.Unterlagen ?? []).Select(u => (JsonNode)u.AlsJson()).ToArray()),
            ["quellen"] = s.Quellen is null ? null : FamilienQuellenAnalyse.Zusammenfassung(s.Quellen),
        };
    }
}

/// <summary>Sammelbefehl familien_quellen_nachfuehren (Nachtrag 21.10.2026).</summary>
public sealed class FamilienQuellenNachfuehren : IBefehl
{
    public const string BefehlName = "familien_quellen_nachfuehren";
    public string Name => BefehlName;
    public const string Text = "Führt die Quellen von Familien auf die aktuelle Leserversion nach (Sammelbefehl, für alle Familien des Projekts oder eine Liste): Befund-Ids einer " +
        "anderen Leserversion – nach unterlagen[].leser bzw. beim Altbestand ohne leser die Version, unter der die Quellen die Wertprüfung bestehen – werden über gleiche " +
        "Seite, Art, Wert und Lage eindeutig auf die aktuelle umgeschlüsselt und je Familie als neue Version gespeichert, auf demselben Weg wie familie_aendern (Prüfung, " +
        "Version + 1, Status entwurf, leser nachgeführt). Eine Familie mit nicht eindeutig zuordenbaren Ids bleibt unverändert und wird mit jeder Id und Grund gemeldet; " +
        "Familien, deren Quellen schon nach dem aktuellen Leser gelten, bleiben ohne Änderung. Bericht in hinweise: erste Zeile die Summe, dann je Familie umgeschlüsselte " +
        "Ids, nicht zuordenbare Ids und die Version je Unterlage. Der Trockenlauf ist die Vorschau.";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
        [new("familien", "liste", false, "Familien (Kennzeichen, Name oder id); leer = alle Familien des Projekts")];

    static string? T(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        List<Element> liste = p["familien"] switch
        {
            null => [.. k.Arbeit.DerKlasse(Familien.Klasse).OrderBy(f => f.Kennzeichen, StringComparer.OrdinalIgnoreCase)],
            JsonArray a when a.Count > 0 => [.. a.Select(x => Familien.Hole(k.Arbeit, T(x) ?? x?.ToJsonString())).DistinctBy(f => f.Id)],
            JsonValue v when T(v) is { Length: > 0 } eine => [Familien.Hole(k.Arbeit, eine)],
            _ => throw new HlksFehler("Parameter «familien» muss eine Liste von Familien sein.", "z. B. [\"FAM-1\", \"FAM-2\"]; weglassen = alle Familien des Projekts."),
        };
        if (liste.Count == 0) throw new HlksFehler("Das Projekt hat keine Familien.", "Familie mit familie_entwurf anlegen oder mit familie_laden laden.");
        var bericht = new List<string>();
        int umgeschluesselt = 0, ids = 0, geaendert = 0, nichtZuordenbar = 0, ohne = 0, abgelehnt = 0, gespeichert = 0, warnungen = 0;
        foreach (var fam0 in liste)
        {
            var fam = k.Arbeit.Finde(fam0.Id) ?? fam0;
            var name = $"{fam.Kennzeichen} «{fam.Name}» ({Familien.DefinitionsId(fam)})";
            if (fam.Eigenschaft("definition")?.Inhalt is not JsonObject gespeicherteDef || !FamilienQuellenAnalyse.AusUnterlagen(gespeicherteDef))
            {
                ohne++; bericht.Add($"{name}: ohne Quellen – ohne Änderung."); continue;
            }
            var def = (JsonObject)gespeicherteDef.DeepClone();
            var n = FamilienQuellenVersion.Nachfuehren(k.Projekt, def, null, new BefundQuelle(k.Projekt));
            var versionen = string.Join("; ", n.Unterlagen.Where(u => u.Umzuschluesseln || u.AltFehlt)
                .Select(u => $"{u.Datei} {u.Quelle} → {u.Aktuell}{(u.AltFehlt ? " (Befunde fehlen)" : "")}{(u.Wahl is null ? "" : $" [{u.Wahl}]")}"));
            warnungen += n.Warnungen.Count;
            if (n.NichtZuordenbar.Count > 0)
            {
                nichtZuordenbar += n.NichtZuordenbar.Count;
                abgelehnt++;
                bericht.Add($"{name}: nicht gespeichert – {n.NichtZuordenbar.Count} Id(s) nicht eindeutig zuordenbar: " +
                    string.Join("; ", n.NichtZuordenbar.Select(x => $"{x.Id} ({x.Stelle}): {x.Grund}")) + (versionen.Length > 0 ? $" – Version: {versionen}" : "") + ".");
                continue;
            }
            if (!n.Noetig)
            {
                ohne++;
                bericht.Add($"{name}: ohne Änderung – Quellen nach dem aktuellen Leser{(n.Warnungen.Count > 0 ? "; " + string.Join(" ", n.Warnungen) : "")}{(versionen.Length > 0 ? $" – Version: {versionen}" : "")}.");
                continue;
            }
            int vorher = k.Hinweise.Count;
            int version = FamilienBefehle.Version(fam) + 1;
            try { FamilienBefehle.Speichern(k, fam, (JsonObject)gespeicherteDef.DeepClone(), Name, "entwurf", version, null); }
            catch (HlksFehler f)
            {
                k.Hinweise.RemoveRange(vorher, k.Hinweise.Count - vorher);
                abgelehnt++;
                bericht.Add($"{name}: nicht gespeichert – die Prüfung lehnt ab: {f.Message}{(f.Vorschlag is null ? "" : $" ({f.Vorschlag})")}");
                continue;
            }
            k.Hinweise.RemoveRange(vorher, k.Hinweise.Count - vorher);   // Prüfhinweise je Familie: der Bericht fasst zusammen
            gespeichert++;
            umgeschluesselt++;
            ids += n.Umgeschluesselt;
            geaendert += n.Geaendert;
            bericht.Add($"{name}: {n.Umgeschluesselt} Id(s) umgeschlüsselt ({n.Geaendert} mit neuer Nummer), 0 nicht zuordenbar – Version: {versionen}" +
                $"{(n.Warnungen.Count > 0 ? "; " + string.Join(" ", n.Warnungen) : "")} – {(k.Trockenlauf ? "würde gespeichert" : "gespeichert")} als Version {version} (Status entwurf).");
        }
        k.Hinweise.Insert(0, $"{BefehlName}: {liste.Count} Familie(n) – {umgeschluesselt} umgeschlüsselt ({ids} Ids, davon {geaendert} mit neuer Nummer), " +
            $"{nichtZuordenbar} Id(s) nicht zuordenbar, {ohne} ohne Änderung, {abgelehnt} nicht gespeichert, {warnungen} Warnung(en) zu fehlenden Leserversionen.");
        k.Hinweise.InsertRange(1, bericht);
    }
}
