// Auswertung der Messprotokolle des Interface-Labors (Modul XR, Quest App P6): liest die JSON-Zeilen, die XrKanal je Prüfaufgabe schreibt
// (<XrEinstellungen.Ordner>\messprotokolle\*.jsonl), und fasst sie zusammen: je Aufgabe und Variante (Läufe, mittlere Zeit, Fehlgriffe, Abbrüche, Erfolge) und je Fassung der App
// (Trendlinie, dazu die Fragebogen). Rein lesend; nichts davon verlässt den PC.
//
// Zeile eines Messprotokolls (Felder, die die Auswertung braucht; weitere werden ignoriert): zeit (ISO), app (Fassung der App aus «hallo», z. B. «0.1+70e9bae»), aufgabe (Id),
//   variante (Text, alt) oder varianten ({ name: option }), zeitMs, fehlgriffe, abbrueche, erfolg (true | false | fehlt).
// Variantenschlüssel einer Zeile: «name=option, name=option» (sortiert), sonst der Text «variante», sonst «ohne Angabe».
// Grenzen (damit die Auswertung nie hängt): die neuesten 300 Dateien, zusammen höchstens 64 MB; mehr wird weggelassen und gemeldet (abgeschnitten).

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hlks.Kern;

namespace Hlks.Web;

public static class XrLaborAuswertung
{
    public const int DateienMax = 300;
    public const long BytesMax = 64L * 1024 * 1024;
    public const string OhneAngabe = "ohne Angabe";
    public const string UnbekannteApp = "unbekannt";

    sealed class Summe
    {
        public int N, Erfolge, MitErfolg;
        public double Zeit, Fehlgriffe, Abbrueche;
        public DateTimeOffset? Erste, Letzte;

        public void Dazu(double zeitMs, int fehl, int abbr, bool? erfolg, DateTimeOffset? zeit)
        {
            N++;
            Zeit += zeitMs; Fehlgriffe += fehl; Abbrueche += abbr;
            if (erfolg is { } e) { MitErfolg++; if (e) Erfolge++; }
            if (zeit is { } z)
            {
                if (Erste is null || z < Erste) Erste = z;
                if (Letzte is null || z > Letzte) Letzte = z;
            }
        }

        public JsonObject Zeile(string schluesselName, string schluessel)
        {
            var o = new JsonObject
            {
                [schluesselName] = schluessel, ["laeufe"] = N, ["erfolge"] = Erfolge, ["mitErfolgsangabe"] = MitErfolg,
                ["erfolgsquote"] = MitErfolg == 0 ? null : Math.Round(Erfolge / (double)MitErfolg, 3),
                ["mittelZeitMs"] = N == 0 ? null : Math.Round(Zeit / N, 0),
                ["fehlgriffe"] = (int)Fehlgriffe, ["fehlgriffeMittel"] = N == 0 ? null : Math.Round(Fehlgriffe / N, 2),
                ["abbrueche"] = (int)Abbrueche, ["abbruecheMittel"] = N == 0 ? null : Math.Round(Abbrueche / N, 2),
                ["erste"] = Erste?.ToString("o"), ["letzte"] = Letzte?.ToString("o"),
            };
            return o;
        }
    }

    /// <summary>Variantenschlüssel einer Zeile (siehe Dateikopf).</summary>
    public static string VarianteSchluessel(JsonObject zeile)
    {
        if (zeile["varianten"] is JsonObject v && v.Count > 0)
        {
            var teile = v.Where(p => p.Value is JsonValue pv && pv.TryGetValue<string>(out _)).OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}={(string)p.Value!}").ToList();
            if (teile.Count > 0) return string.Join(", ", teile);
        }
        if (zeile["variante"] is JsonValue t && t.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s)) return s.Trim();
        return OhneAngabe;
    }

    static string? TextVon(JsonObject o, string name) => o[name] is JsonValue v && v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s) ? s.Trim() : null;

    /// <summary>Liest die Messprotokolle im Ordner und fasst sie mit dem Katalog und den Fragebogen zusammen (Antwort von GET /api/xr/labor/auswertung).</summary>
    public static JsonObject Lesen(string ordner, XrLaborKatalog katalog, JsonArray fragebogen)
    {
        var proAufgabe = new Dictionary<string, (Summe Alle, Dictionary<string, Summe> Varianten, Dictionary<string, Summe> Apps)>(StringComparer.Ordinal);
        var proApp = new Dictionary<string, (Summe Summe, HashSet<string> Aufgaben)>(StringComparer.Ordinal);
        int dateien = 0, laeufe = 0, ungueltig = 0;
        bool abgeschnitten = false;
        long bytes = 0;

        if (Directory.Exists(ordner))
        {
            List<string> namen;
            try { namen = [.. Directory.EnumerateFiles(ordner, "*.jsonl").OrderByDescending(Path.GetFileName, StringComparer.Ordinal)]; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { namen = []; }
            foreach (var datei in namen)
            {
                if (dateien >= DateienMax) { abgeschnitten = true; break; }
                long laenge;
                try { laenge = new FileInfo(datei).Length; } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }
                if (bytes + laenge > BytesMax) { abgeschnitten = true; break; }
                bytes += laenge;
                dateien++;
                IEnumerable<string> zeilen;
                try { zeilen = File.ReadAllLines(datei, Encoding.UTF8); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }
                foreach (var text in zeilen)
                {
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    JsonObject? z;
                    try { z = JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { MaxDepth = 16 }) as JsonObject; }
                    catch (JsonException) { z = null; }
                    var aufgabe = z is null ? null : TextVon(z, "aufgabe");
                    if (z is null || aufgabe is null || z["zeitMs"] is not JsonValue zv || Json.Zahl(zv) is not { } zeitMs || !double.IsFinite(zeitMs)) { ungueltig++; continue; }
                    int fehl = z["fehlgriffe"] is JsonValue fv && fv.TryGetValue<int>(out var fi) ? fi : 0;
                    int abbr = z["abbrueche"] is JsonValue av && av.TryGetValue<int>(out var ai) ? ai : 0;
                    bool? erfolg = z["erfolg"] is JsonValue ev && ev.TryGetValue<bool>(out var eb) ? eb : null;
                    DateTimeOffset? zeit = TextVon(z, "zeit") is { } zt && DateTimeOffset.TryParse(zt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var zp) ? zp : null;
                    var app = TextVon(z, "app") ?? UnbekannteApp;
                    var variante = VarianteSchluessel(z);

                    if (!proAufgabe.TryGetValue(aufgabe, out var a)) proAufgabe[aufgabe] = a = (new Summe(), new Dictionary<string, Summe>(StringComparer.Ordinal), new Dictionary<string, Summe>(StringComparer.Ordinal));
                    a.Alle.Dazu(zeitMs, fehl, abbr, erfolg, zeit);
                    if (!a.Varianten.TryGetValue(variante, out var vs)) a.Varianten[variante] = vs = new Summe();
                    vs.Dazu(zeitMs, fehl, abbr, erfolg, zeit);
                    if (!a.Apps.TryGetValue(app, out var asum)) a.Apps[app] = asum = new Summe();
                    asum.Dazu(zeitMs, fehl, abbr, erfolg, zeit);
                    if (!proApp.TryGetValue(app, out var pa)) proApp[app] = pa = (new Summe(), new HashSet<string>(StringComparer.Ordinal));
                    pa.Summe.Dazu(zeitMs, fehl, abbr, erfolg, zeit);
                    pa.Aufgaben.Add(aufgabe);
                    laeufe++;
                }
            }
        }

        // Aufgaben: erst die des Katalogs in dessen Reihenfolge (auch ohne Läufe), dann unbekannte Ids
        var ids = katalog.AufgabenIds.ToList();
        ids.AddRange(proAufgabe.Keys.Where(k => !katalog.AufgabeBekannt(k)).OrderBy(k => k, StringComparer.Ordinal));
        var aufgabenArray = new JsonArray();
        foreach (var id in ids)
        {
            var hat = proAufgabe.TryGetValue(id, out var a);
            var o = new JsonObject
            {
                ["aufgabe"] = id, ["titel"] = katalog.AufgabeTitel(id) ?? id, ["bekannt"] = katalog.AufgabeBekannt(id), ["status"] = katalog.AufgabeStatusVon(id),
            };
            var gesamt = hat ? a.Alle : new Summe();
            foreach (var (k, w) in gesamt.Zeile("aufgabe", id)) if (k != "aufgabe") o[k] = w?.DeepClone();
            o["varianten"] = new JsonArray([.. (hat ? a.Varianten : new Dictionary<string, Summe>()).OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => (JsonNode)p.Value.Zeile("variante", p.Key))]);
            o["fassungen"] = new JsonArray([.. (hat ? a.Apps : new Dictionary<string, Summe>()).OrderBy(p => p.Value.Erste ?? DateTimeOffset.MaxValue).ThenBy(p => p.Key, StringComparer.Ordinal).Select(p => (JsonNode)p.Value.Zeile("app", p.Key))]);
            aufgabenArray.Add(o);
        }

        // Fassungen der App insgesamt, mit den Fragebogen
        var fb = new Dictionary<string, List<JsonObject>>(StringComparer.Ordinal);
        foreach (var f in fragebogen.OfType<JsonObject>())
        {
            var app = TextVon(f, "app") ?? UnbekannteApp;
            if (!fb.TryGetValue(app, out var l)) fb[app] = l = [];
            l.Add(f);
        }
        var apps = proApp.Keys.Union(fb.Keys, StringComparer.Ordinal).ToList();
        var fassungen = new JsonArray([.. apps
            .OrderBy(k => proApp.TryGetValue(k, out var p) ? p.Summe.Erste ?? DateTimeOffset.MaxValue : DateTimeOffset.MaxValue).ThenBy(k => k, StringComparer.Ordinal)
            .Select(k =>
            {
                var o = proApp.TryGetValue(k, out var p) ? p.Summe.Zeile("app", k) : new Summe().Zeile("app", k);
                o["aufgaben"] = proApp.TryGetValue(k, out var q) ? q.Aufgaben.Count : 0;
                o["fragebogen"] = fb.TryGetValue(k, out var l) ? FragebogenMittel(l) : null;
                return (JsonNode)o;
            })]);

        return new JsonObject
        {
            ["fassung"] = 1, ["ordner"] = ordner, ["dateien"] = dateien, ["laeufe"] = laeufe, ["ungueltigeZeilen"] = ungueltig, ["abgeschnitten"] = abgeschnitten,
            ["leer"] = laeufe == 0 && fragebogen.Count == 0,
            ["aufgaben"] = aufgabenArray, ["fassungen"] = fassungen, ["fragebogen"] = fragebogen.DeepClone(),
        };
    }

    static JsonObject FragebogenMittel(List<JsonObject> liste)
    {
        static double? Mittel(IEnumerable<double?> w) { var l = w.Where(x => x is not null).Select(x => x!.Value).ToList(); return l.Count == 0 ? null : Math.Round(l.Average(), 2); }
        static double? Z(JsonNode? n) => n is JsonValue v && v.GetValueKind() == JsonValueKind.Number ? Json.Zahl(v) : null;
        return new JsonObject
        {
            ["anzahl"] = liste.Count,
            ["sus"] = Mittel(liste.Select(f => Z(f["sus"]?["punkte"]))),
            ["tlx"] = Mittel(liste.Select(f => Z(f["tlx"]?["raw"]))),
            ["uebelkeit"] = Mittel(liste.Select(f => Z(f["komfort"]?["uebelkeit"]))),
            ["augen"] = Mittel(liste.Select(f => Z(f["komfort"]?["augen"]))),
            ["natuerlich"] = Mittel(liste.Select(f => Z(f["komfort"]?["natuerlich"]))),
        };
    }
}
