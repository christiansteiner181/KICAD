// Fragebogen nach einem Test im Interface-Labor (Modul XR, Quest App P6; Entscheid Christian 23.10.2026 «Prüfung: kurz gemischt»): Komfort (eigene Fragen),
// SUS und NASA-TLX (Bewertungsregeln hier, Wortlaute nur mit Quelle in kataloge/xr_fragebogen.json), Ablage der Antworten.
//
// Bewertungsregeln (Quellen: bitte durch Christian prüfen – «Quelle prüfen»):
//   SUS  Brooke, J. (1996): SUS – a «quick and dirty» usability scale. In: Jordan, P. W. et al. (Hrsg.): Usability Evaluation in Industry. London: Taylor & Francis, S. 189–194.
//        10 Aussagen, Antworten 1 … 5. Ungerade Aussagen (1, 3, 5, 7, 9): Wert − 1. Gerade (2, 4, 6, 8, 10): 5 − Wert. Summe × 2.5 = 0 … 100.
//   TLX  Hart, S. G.; Staveland, L. E. (1988): Development of NASA-TLX (Task Load Index): Results of empirical and theoretical research. In: Hancock, P. A.; Meshkati, N. (Hrsg.):
//        Human Mental Workload. Amsterdam: North-Holland, S. 139–183. Raw TLX: ungewichtetes Mittel der sechs Skalen (je 0 … 100), ohne die paarweisen Gewichtungen.
//   Beide Regeln sind mit bekannten Beispielwerten getestet (XrLaborTests). Die deutschen Wortlaute der Fragen fehlen noch (Entscheid Christian: «Deutsche Fassungen der
//   Fragebogen nur mit Quelle»): solange die Datei keine hat, nimmt KICAD keine SUS- und TLX-Antworten an; der Komfortteil funktioniert.
//
// Ablage: JSON je Fragebogen in <XrEinstellungen.Ordner>\fragebogen\<JJJJMMTT-hhmmss>_<name>.json (Vorgabe %LOCALAPPDATA%\HLKS_CAD\xr\fragebogen; je PC, ausserhalb von
// Repository und OneDrive; im Testlauf unter %TEMP%).

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Hlks.Kern;

namespace Hlks.Web;

/// <summary>Eine Frage des Fragebogens (Wortlaut null = fehlt noch, dann steht ein Platzhalter da).</summary>
public sealed record XrFrage(string Id, string Art, string? Wortlaut, string? Polaritaet, JsonObject Roh);

/// <summary>Ein Teil des Fragebogens (komfort, sus, nasa_tlx).</summary>
public sealed class XrFragebogenTeil
{
    public required string Id { get; init; }
    public required string Bewertung { get; init; }
    public required IReadOnlyList<XrFrage> Fragen { get; init; }
    public string? WortlautQuelle { get; init; }
    public required JsonObject Roh { get; init; }
    /// <summary>Alle Fragen haben einen Wortlaut.</summary>
    public bool WortlautVorhanden => Fragen.All(f => !string.IsNullOrWhiteSpace(f.Wortlaut));
}

public sealed class XrFragebogenKatalog
{
    public const string Komfort = "komfort", Sus = "sus", Tlx = "nasa_tlx";
    static readonly string[] KomfortIds = ["uebelkeit", "augen", "natuerlich", "text"];
    static readonly string[] TlxIds = ["mental", "physical", "temporal", "performance", "effort", "frustration"];
    const string Datei = XrLaborKatalog.DateiFragebogen;

    public IReadOnlyList<XrFragebogenTeil> Teile { get; private init; } = [];
    JsonObject kopf = [];

    public XrFragebogenTeil Teil(string id) => Teile.First(t => t.Id == id);

    public static XrFragebogenKatalog Laden(JsonObject o)
    {
        var teile = new List<XrFragebogenTeil>();
        if (o["teile"] is not JsonArray ta || ta.Count == 0) throw XrLaborKatalog.Fehler(Datei, "«teile» muss eine nicht leere Liste sein.");
        foreach (var tn in ta)
        {
            var t = tn as JsonObject ?? throw XrLaborKatalog.Fehler(Datei, "teile: muss ein Objekt sein.");
            var id = Text(t["id"], "teile.id", 20);
            var wo = $"teile.{id}";
            if (teile.Any(x => x.Id == id)) throw XrLaborKatalog.Fehler(Datei, $"{wo}: kommt doppelt vor.");
            Text(t["titel"], $"{wo}.titel", 80);
            Text(t["quelle"], $"{wo}.quelle", 800);
            var bewertung = Text(t["bewertung"], $"{wo}.bewertung", 20);
            var erwartet = id switch { Komfort => "einzeln", Sus => "sus", Tlx => "tlx", _ => throw XrLaborKatalog.Fehler(Datei, $"{wo}: erlaubt sind komfort, sus und nasa_tlx.") };
            if (bewertung != erwartet) throw XrLaborKatalog.Fehler(Datei, $"{wo}.bewertung muss «{erwartet}» sein.");
            var fragen = new List<XrFrage>();
            if (t["fragen"] is not JsonArray fa || fa.Count == 0) throw XrLaborKatalog.Fehler(Datei, $"{wo}.fragen fehlt.");
            foreach (var fn in fa)
            {
                var f = fn as JsonObject ?? throw XrLaborKatalog.Fehler(Datei, $"{wo}.fragen: muss ein Objekt sein.");
                var fid = Text(f["id"], $"{wo}.fragen.id", 20);
                if (fragen.Any(x => x.Id == fid)) throw XrLaborKatalog.Fehler(Datei, $"{wo}.fragen.{fid}: kommt doppelt vor.");
                var art = Text(f["art"], $"{wo}.fragen.{fid}.art", 10);
                if (art is not ("skala" or "text")) throw XrLaborKatalog.Fehler(Datei, $"{wo}.fragen.{fid}.art: erlaubt sind skala und text.");
                string? wortlaut = null;
                if (f["wortlaut"] is not null)
                {
                    wortlaut = f["wortlaut"] is JsonValue wv && wv.TryGetValue<string>(out var ws) && ws.Length is > 0 and <= 400 && !ws.Any(char.IsControl)
                        ? ws : throw XrLaborKatalog.Fehler(Datei, $"{wo}.fragen.{fid}.wortlaut: Text (1 bis 400 Zeichen) oder null.");
                }
                else if (id != Komfort) Text(f["platzhalter"], $"{wo}.fragen.{fid}.platzhalter", 120);
                if (id == Komfort && wortlaut is null) throw XrLaborKatalog.Fehler(Datei, $"{wo}.fragen.{fid}: Der Komfortteil braucht einen Wortlaut (eigene Fragen).");
                string? pol = null;
                if (id == Sus)
                {
                    pol = Text(f["polaritaet"], $"{wo}.fragen.{fid}.polaritaet", 10);
                    if (pol is not ("positiv" or "negativ")) throw XrLaborKatalog.Fehler(Datei, $"{wo}.fragen.{fid}.polaritaet: erlaubt sind positiv und negativ.");
                }
                fragen.Add(new XrFrage(fid, art, wortlaut, pol, (JsonObject)f.DeepClone()));
            }
            string? wq = null;
            if (id != Komfort)
            {
                if (t["wortlautQuelle"] is JsonValue qv && qv.TryGetValue<string>(out var qs) && !string.IsNullOrWhiteSpace(qs)) wq = qs;
                else if (t["wortlautQuelle"] is not null) throw XrLaborKatalog.Fehler(Datei, $"{wo}.wortlautQuelle: Text oder null.");
                if (fragen.Any(f => f.Wortlaut is not null) && wq is null)
                    throw XrLaborKatalog.Fehler(Datei, $"{wo}: Die Fragen haben einen Wortlaut, aber «wortlautQuelle» fehlt. Deutsche Fassungen der Fragebogen nur mit Quelle (Entscheid Christian 23.10.2026).");
                if (fragen.Any(f => f.Wortlaut is null) && fragen.Any(f => f.Wortlaut is not null))
                    throw XrLaborKatalog.Fehler(Datei, $"{wo}: Entweder haben alle Fragen einen Wortlaut oder keine.");
            }
            ReihenfolgePruefen(id, wo, fragen, t);
            teile.Add(new XrFragebogenTeil { Id = id, Bewertung = bewertung, Fragen = fragen, WortlautQuelle = wq, Roh = (JsonObject)t.DeepClone() });
        }
        foreach (var pflicht in new[] { Komfort, Sus, Tlx })
            if (teile.All(t => t.Id != pflicht)) throw XrLaborKatalog.Fehler(Datei, $"Der Teil «{pflicht}» fehlt.");
        return new XrFragebogenKatalog { Teile = teile, kopf = new JsonObject { ["art"] = o["art"]!.DeepClone(), ["fassung"] = o["fassung"]!.DeepClone(), ["beschreibung"] = o["beschreibung"]?.DeepClone() } };
    }

    static string Text(JsonNode? n, string wo, int max)
    {
        if (n is not JsonValue v || !v.TryGetValue<string>(out var s) || string.IsNullOrWhiteSpace(s) || s.Length > max || s.Any(char.IsControl))
            throw XrLaborKatalog.Fehler(Datei, $"{wo}: Text fehlt oder ist zu lang (höchstens {max} Zeichen).");
        return s;
    }

    static void ReihenfolgePruefen(string id, string wo, List<XrFrage> fragen, JsonObject teil)
    {
        string[] soll = id switch
        {
            Komfort => KomfortIds,
            Sus => [.. Enumerable.Range(1, 10).Select(i => "sus" + i)],
            _ => TlxIds,
        };
        if (!fragen.Select(f => f.Id).SequenceEqual(soll)) throw XrLaborKatalog.Fehler(Datei, $"{wo}.fragen: erwartet {string.Join(", ", soll)} in dieser Reihenfolge.");
        if (id == Sus)
            for (int i = 0; i < 10; i++)
                if (fragen[i].Polaritaet != (i % 2 == 0 ? "positiv" : "negativ"))
                    throw XrLaborKatalog.Fehler(Datei, $"{wo}.fragen.{fragen[i].Id}.polaritaet: bei SUS sind die ungeraden Aussagen positiv, die geraden negativ.");
        int lo, hi;
        if (id == Komfort) (lo, hi) = (0, 10); else if (id == Sus) (lo, hi) = (1, 5); else (lo, hi) = (0, 100);
        if (id != Komfort)
        {
            if (teil["skala"] is not JsonObject s || Json.Zahl(s["min"]) != lo || Json.Zahl(s["max"]) != hi) throw XrLaborKatalog.Fehler(Datei, $"{wo}.skala muss von {lo} bis {hi} gehen.");
        }
        else
            foreach (var f in fragen.Where(f => f.Art == "skala"))
                if (f.Roh["skala"] is not JsonObject s || Json.Zahl(s["min"]) != 0 || Json.Zahl(s["max"]) != 10) throw XrLaborKatalog.Fehler(Datei, $"{wo}.fragen.{f.Id}.skala muss von 0 bis 10 gehen.");
    }

    /// <summary>Der Fragebogen für den Dialog (GET /api/xr/labor/fragebogen): wie die Datei, je Teil mit «wortlautVorhanden».</summary>
    public JsonObject Antwort()
    {
        var o = (JsonObject)kopf.DeepClone();
        var teile = new JsonArray();
        foreach (var t in Teile)
        {
            var n = (JsonObject)t.Roh.DeepClone();
            n["wortlautVorhanden"] = t.WortlautVorhanden;
            teile.Add(n);
        }
        o["teile"] = teile;
        return o;
    }
}

/// <summary>Bewertungsregeln (siehe Dateikopf).</summary>
public static class XrFragebogenRegeln
{
    /// <summary>SUS-Wert 0 … 100 aus zehn Antworten 1 … 5. Ungerade Aussagen: Antwort − 1, gerade: 5 − Antwort; Summe × 2.5.</summary>
    public static double Sus(IReadOnlyList<int> antworten)
    {
        if (antworten.Count != 10) throw new HlksFehler("SUS braucht genau zehn Antworten.", "Zehn Zahlen von 1 bis 5 senden.");
        double summe = 0;
        for (int i = 0; i < 10; i++)
        {
            var a = antworten[i];
            if (a is < 1 or > 5) throw new HlksFehler($"SUS, Antwort {i + 1}: {a} liegt nicht zwischen 1 und 5.", "Zehn ganze Zahlen von 1 bis 5 senden.");
            summe += (i % 2 == 0) ? a - 1 : 5 - a;   // Aussage i + 1: ungerade (i gerade) positiv, gerade negativ
        }
        return summe * 2.5;
    }

    /// <summary>Raw TLX 0 … 100: ungewichtetes Mittel der sechs Skalen (je 0 … 100).</summary>
    public static double TlxRaw(IReadOnlyList<double> werte)
    {
        if (werte.Count != 6) throw new HlksFehler("NASA-TLX braucht genau sechs Werte.", "Sechs Zahlen von 0 bis 100 senden.");
        foreach (var w in werte)
            if (!double.IsFinite(w) || w < 0 || w > 100) throw new HlksFehler($"NASA-TLX: {w} liegt nicht zwischen 0 und 100.", "Sechs Zahlen von 0 bis 100 senden.");
        return werte.Sum() / 6.0;
    }
}

/// <summary>Speichert und liest die ausgefüllten Fragebogen (JSON je Fragebogen).</summary>
public sealed class XrFragebogenSpeicher(string ordner, Func<DateTimeOffset> jetzt)
{
    static readonly Regex SitzungMuster = new(@"^[A-Za-z0-9_\-]{1,80}$", RegexOptions.CultureInvariant);
    static readonly Regex NameMuster = new(@"^[a-z][a-z0-9_]{0,31}$", RegexOptions.CultureInvariant);
    static readonly Regex OptionMuster = new(@"^[a-z][a-z0-9_]{0,39}$", RegexOptions.CultureInvariant);
    public const int Max = 200;
    public string Ordner { get; } = ordner;

    public JsonObject Speichern(JsonObject p, XrLaborKatalog katalog)
    {
        var name = Zeile(p, "name", 40, true)!;
        var sitzung = Zeile(p, "sitzung", 80, false);
        if (sitzung is not null && !SitzungMuster.IsMatch(sitzung)) throw new HlksFehler("«sitzung» ist ungültig.", "Der Name der Protokolldatei ohne Endung (Buchstaben, Ziffern, _ und -).");
        var app = Zeile(p, "app", 40, false);
        var notiz = Zeile(p, "notiz", 2000, false, mehrzeilig: true);
        var rec = new JsonObject { ["fassung"] = 1, ["name"] = name };
        if (sitzung is not null) rec["sitzung"] = sitzung;
        if (app is not null) rec["app"] = app;
        if (p["varianten"] is { } vn)
        {
            if (vn is not JsonObject vo || vo.Count > 12) throw new HlksFehler("«varianten» muss ein Objekt { Name: Option } mit höchstens 12 Einträgen sein.");
            var v = new JsonObject();
            foreach (var (k, w) in vo)
                v[k] = NameMuster.IsMatch(k) && w is JsonValue wv && wv.TryGetValue<string>(out var os) && OptionMuster.IsMatch(os) ? os : throw new HlksFehler($"«varianten.{(k.Length > 20 ? k[..20] : k)}» ist ungültig.");
            rec["varianten"] = v;
        }

        var teile = 0;
        if (p["komfort"] is { } kn)
        {
            if (kn is not JsonObject ko) throw new HlksFehler("«komfort» muss ein Objekt sein.");
            var k = new JsonObject();
            foreach (var id in new[] { "uebelkeit", "augen", "natuerlich" })
                if (ko[id] is { } w)
                    k[id] = w is JsonValue v && v.TryGetValue<int>(out var z) && z is >= 0 and <= 10 ? z : throw new HlksFehler($"komfort.{id} muss eine ganze Zahl von 0 bis 10 sein.");
            if (Zeile(ko, "text", 2000, false, mehrzeilig: true) is { } t) k["text"] = t;
            if (k.Count == 0) throw new HlksFehler("«komfort» enthält keine Antwort.");
            rec["komfort"] = k;
            teile++;
        }
        if (p["sus"] is { } sn)
        {
            if (!katalog.Fragebogen.Teil(XrFragebogenKatalog.Sus).WortlautVorhanden)
                throw new HlksFehler("SUS hat noch keinen Wortlaut (Quelle durch Christian): die Antworten werden nicht gespeichert.", "Den Wortlaut mit Quelle in kataloge/xr_fragebogen.json eintragen.");
            if (sn is not JsonArray sa) throw new HlksFehler("«sus» muss eine Liste von zehn Antworten (1 bis 5) sein.");
            var antworten = sa.Select(x => x is JsonValue v && v.TryGetValue<int>(out var z) ? z : throw new HlksFehler("«sus»: jede Antwort muss eine ganze Zahl sein.")).ToList();
            rec["sus"] = new JsonObject { ["antworten"] = new JsonArray([.. antworten.Select(a => (JsonNode)JsonValue.Create(a))]), ["punkte"] = XrFragebogenRegeln.Sus(antworten) };
            teile++;
        }
        if (p["tlx"] is { } tn)
        {
            var teil = katalog.Fragebogen.Teil(XrFragebogenKatalog.Tlx);
            if (!teil.WortlautVorhanden)
                throw new HlksFehler("NASA-TLX hat noch keinen Wortlaut (Quelle durch Christian): die Antworten werden nicht gespeichert.", "Den Wortlaut mit Quelle in kataloge/xr_fragebogen.json eintragen.");
            if (tn is not JsonObject to) throw new HlksFehler("«tlx» muss ein Objekt { mental, physical, temporal, performance, effort, frustration } sein.");
            var werte = new List<double>();
            var o = new JsonObject();
            foreach (var f in teil.Fragen)
            {
                var w = Json.Zahl(to[f.Id]) ?? throw new HlksFehler($"tlx.{f.Id} muss eine Zahl von 0 bis 100 sein.");
                werte.Add(w);
                o[f.Id] = w;
            }
            rec["tlx"] = new JsonObject { ["werte"] = o, ["raw"] = Math.Round(XrFragebogenRegeln.TlxRaw(werte), 2) };
            teile++;
        }
        if (teile == 0) throw new HlksFehler("Der Fragebogen enthält keine Antworten.", "Mindestens einen der Teile komfort, sus oder tlx senden.");
        if (notiz is not null) rec["notiz"] = notiz;

        var jetztZeit = jetzt();
        rec["zeit"] = jetztZeit.ToString("o");
        Directory.CreateDirectory(Ordner);
        var slug = new string([.. name.ToLowerInvariant().Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Select(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' ? c : '_')]).Trim('_');
        if (slug.Length == 0) slug = "test";
        if (slug.Length > 24) slug = slug[..24];
        var stamm = $"{jetztZeit.UtcDateTime:yyyyMMdd-HHmmss}_{slug}";
        for (int i = 1; ; i++)
        {
            var datei = Path.Combine(Ordner, i == 1 ? $"{stamm}.json" : $"{stamm}-{i}.json");
            try
            {
                using var fs = new FileStream(datei, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                rec["datei"] = Path.GetFileName(datei);
                fs.Write(new UTF8Encoding(false).GetBytes(Json.Text(rec, true)));
                return rec;
            }
            catch (IOException) when (File.Exists(datei) && i < 100) { /* gleicher Name in derselben Sekunde: nächste Nummer */ }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new HlksFehler($"Der Fragebogen konnte nicht gespeichert werden ({Ordner}): {ex.Message}", "Schreibrechte im Ordner prüfen und erneut versuchen.");
            }
        }
    }

    /// <summary>Die gespeicherten Fragebogen, neueste zuerst (höchstens <see cref="Max"/>); unlesbare Dateien werden übersprungen.</summary>
    public JsonArray Liste()
    {
        var liste = new JsonArray();
        if (!Directory.Exists(Ordner)) return liste;
        try
        {
            foreach (var f in Directory.EnumerateFiles(Ordner, "*.json").OrderByDescending(Path.GetFileName, StringComparer.Ordinal).Take(Max))
            {
                try
                {
                    if (JsonNode.Parse(File.ReadAllText(f, Encoding.UTF8), documentOptions: new JsonDocumentOptions { MaxDepth = 16 }) is JsonObject o) { o["datei"] = Path.GetFileName(f); liste.Add(o); }
                }
                catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException) { /* nächste */ }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* Ordner nicht lesbar: leere Liste */ }
        return liste;
    }

    static string? Zeile(JsonObject o, string name, int max, bool pflicht, bool mehrzeilig = false)
    {
        var n = o[name];
        if (n is null) return pflicht ? throw new HlksFehler($"«{name}» fehlt.") : null;
        if (n is not JsonValue v || !v.TryGetValue<string>(out var s)) throw new HlksFehler($"«{name}» muss Text sein.");
        s = s.Trim();
        if (s.Length == 0) return pflicht ? throw new HlksFehler($"«{name}» ist leer.") : null;
        if (s.Length > max || s.Any(c => char.IsControl(c) && !(mehrzeilig && c is '\n' or '\r'))) throw new HlksFehler($"«{name}» ist zu lang (höchstens {max} Zeichen) oder enthält Steuerzeichen.");
        return s;
    }
}
