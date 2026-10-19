// Familien (Modul 2, B4): Werte einer Familie gegen die Befunde ihrer Unterlagen (entwurf/modul-2.md, «Zusammenführen,
// Konflikte, Abdeckung»). Hier: Zahlen und Einheiten aus Unterlagen lesen, Befunde zu Befund-Ids finden, die Zahlen bzw. Texte
// eines Befunds (Kandidaten), Vergleich mit Toleranz und Vorrang je Wertart. Konflikte, Abdeckung und die Prüfung beim Speichern
// stehen in FamilienKonflikte.cs, der Massvergleich in FamilienMasse.cs.
//
// ANNAHMEN (Prüfliste; die Spezifikation lässt sie offen):
//  - Zahlen: Dezimalkomma oder -punkt («1,5» = «1.5»; «1.234» ist 1.234, nicht tausend), Tausender mit ' («1'250»); Vorsatz
//    DN, Rp, G, R, Ø vor der Zahl zählt nicht. Brüche «1 1/4», «1-1/4», «3/4» (Gewinde in Zoll) sind nur für Zahlparameter eine
//    Zahl – ein Textparameter vergleicht den Text («Rp 1/2» passt zu «1/2» in der Spalte «Rp»). Zoll ist keine Länge.
//  - Einheit: aus dem Befund, sonst aus dem Tabellenkopf («L [mm]», «Kvs [m³/h]»), sonst direkt hinter der Zahl («67 mm»).
//    Ohne Einheit gilt die Einheit des Parameters (Länge mm, Winkel Grad, Zahl: einheit des Parameters). DN ist einheitenlos
//    (ein «[mm]» über der DN-Spalte zählt nicht). Umgerechnet werden Länge, Fläche, Volumen, Volumenstrom, Masse, Druck,
//    Leistung und Winkel; °C und K nicht.
//  - Toleranzen (Spezifikation, Annahme 4): Länge max(1 mm; 0.5 % des Quellwerts); Durchmesser 1 mm (Parameter, die als d eines
//    Anschlusses dienen oder «durchmesser» im Namen tragen; Massbezüge mit Ø); Anschlusslage 2 mm (Kontrollbild); DN genau;
//    Kennwerte, Winkel und übrige Zahlen auf die Stellen der Unterlage (± eine halbe Einheit der letzten Stelle).
//  - Vorrang nur für den Vorschlag eines Konflikts (Annahme 5): Länge/Winkel: 3D-Modell (STEP, IFC, STL, OBJ) 5 › DWG/DXF-
//    Bemassung 4 › PDF-Zelle, PDF-Masszahl, Tabelle 3 › Wort, Zeile, Text 2 › Bild 1; übrige Werte (Kennwerte, DN, Text):
//    Tabelle (Zelle, Typzeile, Pset, Port) 3 › Fliesstext und Masszahl 2 › Bild 1.
//  - Welche Zahl eines Befunds gilt: Zelle, Wort, Masszahl je genau eine; Typzeile (Typkatalog), Pset und Port die Spalte bzw.
//    Eigenschaft mit dem Namen des Parameters oder seines Ziels («Kvs [m³/h]» passt zu kvs), sonst jede Zahl der Zeile; Zeile und
//    Text (Fliesstext) jede Zahl darin; Stutzen, Hülle, Umriss ihre Masse in mm. Passt keine, zählt die nächstliegende für die Meldung.
//  - Texte (Nachtrag Hauptlauf 18.10.2026 nach Abnahmelauf 1): verglichen nach Normalisierung (klein, Satz- und Aufzählungszeichen
//    • | – weg, Leerraum zusammengefasst); passt, wenn gleich, als Teilstück aus ganzen Wörtern in der Quelle, oder wenn alle Wörter
//    des Werts (ab 2 Zeichen, Zahlen immer) in der Quelle stehen («ULSA ohne Isolierung» ↔ «• | ULSA, Gehäuse ohne Isolierung»).
//    Zahlen bleiben streng (Toleranzen unten).
//  - Nicht nachrechenbar (Wert gilt als «aus Bild gelesen, ungeprüft», kein Fehler): bild, linie, polylinie, bogen, kreis, eine
//    Masslinie mit Buchstabe ohne Zahl, ja/nein-Werte. Eine ganze Tabelle, eine Seite oder ein Block ist keine Quelle eines
//    Werts (Fehler mit Vorschlag «Zelle nennen»); als Massbezug zeigt eine Tabelle die Spalte.

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Hlks.Kern;

/// <summary>Eine Zahl, wie sie in einer Unterlage steht: Wert, Einheit (vereinheitlicht oder null), Nachkommastellen, Text.</summary>
public sealed record QuellZahl(double Wert, string? Einheit, int Stellen, string Text, bool Bruch = false);

/// <summary>Einheiten der Unterlagen: vereinheitlichen und umrechnen.</summary>
public static class QuellEinheiten
{
    sealed record E(string Groesse, double Faktor, string Kanon);

    static readonly Dictionary<string, E> Tabelle = Bauen();

    static Dictionary<string, E> Bauen()
    {
        var t = new Dictionary<string, E>(StringComparer.OrdinalIgnoreCase);
        void A(string groesse, double faktor, params string[] namen) { foreach (var n in namen) t[n] = new E(groesse, faktor, namen[0]); }
        A("laenge", 1, "mm"); A("laenge", 10, "cm"); A("laenge", 100, "dm"); A("laenge", 1000, "m"); A("laenge", 1e6, "km");
        A("laenge", 25.4, "in"); A("laenge", 304.8, "ft");
        A("flaeche", 1, "mm²"); A("flaeche", 100, "cm²"); A("flaeche", 1e4, "dm²"); A("flaeche", 1e6, "m²");
        A("volumen", 1, "l", "dm³", "Liter"); A("volumen", 1000, "m³"); A("volumen", 0.001, "cm³", "ml");
        A("volumenstrom", 1, "m³/h"); A("volumenstrom", 3600, "m³/s"); A("volumenstrom", 3.6, "l/s", "dm³/s"); A("volumenstrom", 0.001, "l/h");
        A("volumenstrom", 0.06, "l/min");
        A("masse", 1, "kg"); A("masse", 0.001, "g"); A("masse", 1000, "t");
        A("druck", 1, "Pa"); A("druck", 100, "hPa", "mbar"); A("druck", 1000, "kPa"); A("druck", 1e6, "MPa"); A("druck", 1e5, "bar");
        A("leistung", 1, "W"); A("leistung", 1000, "kW"); A("leistung", 1e6, "MW");
        A("winkel", 1, "°", "Grad", "grad"); A("winkel", 180 / Math.PI, "rad");
        A("geschwindigkeit", 1, "m/s");
        A("zoll", 1, "Zoll", "\"", "″");   // Gewindegrösse, keine Länge
        A("temperatur", 1, "°C"); A("kelvin", 1, "K");
        A("anteil", 1, "%"); A("schall", 1, "dB"); A("schalla", 1, "dB(A)"); A("spannung", 1, "V"); A("strom", 1, "A");
        A("frequenz", 1, "Hz"); A("moment", 1, "Nm"); A("zeit", 1, "s"); A("zeit", 60, "min"); A("zeit", 3600, "h");
        A("stueck", 1, "px");
        return t;
    }

    /// <summary>Einheit vereinheitlicht («m3/h» → «m³/h», «[mm]» → «mm», «KW» → «kW»); unbekannte bleiben, wie sie sind; leer → null.</summary>
    public static string? Normal(string? e)
    {
        if (string.IsNullOrWhiteSpace(e)) return null;
        var t = e.Trim();
        if (t.Length > 2 && ((t[0] == '[' && t[^1] == ']') || (t[0] == '(' && t[^1] == ')'))) t = t[1..^1].Trim();
        t = t.Replace("^2", "²").Replace("^3", "³");
        t = Regex.Replace(t, @"(?<=[A-Za-z])3(?=$|/)", "³");
        t = Regex.Replace(t, @"(?<=[A-Za-z])2(?=$|/)", "²");
        if (t.Length == 0) return null;
        return Tabelle.TryGetValue(t, out var x) ? x.Kanon : t;
    }

    public static bool Bekannt(string? e) => e is not null && Tabelle.ContainsKey(Normal(e) ?? "");

    /// <summary>Faktor, mit dem ein Wert in <paramref name="von"/> in <paramref name="nach"/> umgerechnet wird; null = nicht umrechenbar.</summary>
    public static double? Faktor(string von, string nach)
    {
        var a = Normal(von); var b = Normal(nach);
        if (a is null || b is null) return null;
        if (string.Equals(a, b, StringComparison.Ordinal)) return 1;
        if (Tabelle.TryGetValue(a, out var x) && Tabelle.TryGetValue(b, out var y) && x.Groesse == y.Groesse) return x.Faktor / y.Faktor;
        return null;
    }
}

/// <summary>Zahlen aus Texten der Unterlagen (Zellen, Wörter, Zeilen), mit Einheit und Nachkommastellen.</summary>
public static class QuellZahlen
{
    const string Zahl = @"[+\-−–]?(?:\d{1,3}(?:['’]\d{3})+|\d+)(?:[.,]\d+)?";
    const string Einheit = @"m³/h|m3/h|m³/s|m3/s|l/s|l/h|l/min|m/s|mm²|cm²|dm²|m²|mm2|cm2|m2|m³|m3|dm³|cm³|mm|cm|dm|km|m|kg|g|t|kPa|hPa|MPa|Pa|mbar|bar|kW|KW|MW|W|°C|°|%|dB\(A\)|dB|Nm|V|Hz|Zoll|""|″";
    static readonly Regex Ganz = new($@"^\s*(?:(?:DN|Rp|G|R|Ø|⌀|ø)\s*)?(?<z>{Zahl})\s*(?<e>\S.{{0,14}})?$", RegexOptions.Compiled);
    static readonly Regex BruchGanz = new(@"^\s*(?:(?:Rp|G|R)\s*)?(?:(?<g>\d+)[\s\-]+)?(?<a>\d+)\s*/\s*(?<b>\d+)\s*(?<e>""|″|Zoll)?\s*$", RegexOptions.Compiled);
    static readonly Regex InText = new($@"(?<![\w.,'’/])(?:(?<g>\d+)[\s\-](?<a>\d+)/(?<b>\d+)|(?<a>\d+)/(?<b>\d+)|(?<z>{Zahl}))(?![\d/]|[.,]\d)(?:\s*(?<e>{Einheit})(?![A-Za-z³²]))?", RegexOptions.Compiled);

    /// <summary>Der ganze Text ist eine Zahl (mit Einheit dahinter oder Vorsatz DN, Rp, G, Ø davor); mit <paramref name="bruch"/>
    /// auch ein Bruch («1 1/4»). Sonst null.</summary>
    public static QuellZahl? Lesen(string? text, bool bruch = true)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var m = Ganz.Match(text);
        if (m.Success)
        {
            var e = m.Groups["e"].Success ? m.Groups["e"].Value.Trim() : null;
            // «15 Stück», «200 x 100» … sind keine einzelne Zahl; «1-1/2» kann ein Bruch sein (unten)
            if (e is null || QuellEinheiten.Bekannt(e)) return Wert(m.Groups["z"].Value, QuellEinheiten.Normal(e), text.Trim());
        }
        if (!bruch) return null;
        var b = BruchGanz.Match(text);
        if (!b.Success) return null;
        return Bruchwert(b, text.Trim());
    }

    /// <summary>Alle Zahlen in einem Fliesstext (Zeile, Text), je mit der Einheit direkt dahinter.</summary>
    public static List<QuellZahl> AlleLesen(string? text)
    {
        var l = new List<QuellZahl>();
        if (string.IsNullOrWhiteSpace(text)) return l;
        foreach (Match m in InText.Matches(text))
        {
            var e = m.Groups["e"].Success ? QuellEinheiten.Normal(m.Groups["e"].Value) : null;
            if (m.Groups["b"].Success) { var q = Bruchwert(m, m.Value.Trim()); if (q is not null) l.Add(q with { Einheit = e ?? q.Einheit }); }
            else if (Wert(m.Groups["z"].Value, e, m.Value.Trim()) is { } z) l.Add(z);
        }
        return l;
    }

    static QuellZahl? Bruchwert(Match m, string text)
    {
        if (!double.TryParse(m.Groups["a"].Value, CultureInfo.InvariantCulture, out var a) || !double.TryParse(m.Groups["b"].Value, CultureInfo.InvariantCulture, out var b) || b == 0) return null;
        double g = m.Groups["g"].Success && double.TryParse(m.Groups["g"].Value, CultureInfo.InvariantCulture, out var gg) ? gg : 0;
        var e = m.Groups["e"].Success && m.Groups["e"].Value.Length > 0 ? "Zoll" : null;
        return new QuellZahl(g + a / b, e, 4, text, true);
    }

    static QuellZahl? Wert(string z, string? einheit, string text)
    {
        var t = z.Replace("−", "-").Replace("–", "-").Replace("'", "").Replace("’", "");
        int stellen = 0;
        int k = t.IndexOfAny(['.', ',']);
        if (k >= 0) stellen = t.Length - k - 1;
        t = t.Replace(',', '.');
        return double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && double.IsFinite(d) ? new QuellZahl(d, einheit, stellen, text) : null;
    }

    /// <summary>Nachkommastellen einer Zahl in ihrer kürzesten Schreibweise (für Zahlen aus XLSX, DXF).</summary>
    public static int Stellen(double d)
    {
        var s = Math.Round(d, 9).ToString("R", CultureInfo.InvariantCulture);
        if (s.Contains('E')) return 6;
        int k = s.IndexOf('.');
        return k < 0 ? 0 : s.Length - k - 1;
    }

    /// <summary>Text zum Vergleich: klein, Leerzeichen einfach, «×» als «x», ohne Punkt am Ende.</summary>
    public static string TextNormal(string? s) =>
        Regex.Replace((s ?? "").Trim().TrimEnd('.').Replace('×', 'x').Replace(' ', ' ').ToLowerInvariant(), @"\s+", " ");

    /// <summary>Name einer Spalte bzw. Eigenschaft zum Vergleich mit einem Parameternamen: ohne Einheit in Klammern, nur Buchstaben
    /// und Ziffern, klein, Umlaute als ae/oe/ue.</summary>
    public static string NameNormal(string? s)
    {
        var t = Regex.Replace(s ?? "", @"[\[\(][^\]\)]*[\]\)]", " ").Trim().ToLowerInvariant()
            .Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss");
        return new string(t.Where(char.IsLetterOrDigit).ToArray());
    }
}

/// <summary>Eine Zahl bzw. ein Text eines Befunds, mit dem Namen der Spalte oder Eigenschaft (falls bekannt).</summary>
public sealed record QuellKandidat(string? Name, QuellZahl? Zahl, string? Text);

/// <summary>Ergebnis des Vergleichs eines Werts mit einem Befund.</summary>
public sealed class QuellVergleich
{
    public required string Id { get; init; }
    public UnterlagenBefund? Befund { get; init; }
    /// <summary>passt | weicht | einheit | ungeprueft | fehlt (Befund gibt es nicht) | keine (Tabelle, Seite: keine Quelle eines Werts)
    /// | abwesend (Unterlage nicht im Projekt, nur Verweis)</summary>
    public required string Status { get; init; }
    /// <summary>Zahl der Quelle in der Einheit des Parameters (passend bzw. nächste).</summary>
    public double? Quellwert { get; init; }
    /// <summary>Text der Quelle (Zahl wie in der Unterlage mit Einheit bzw. Text).</summary>
    public string? QuellText { get; init; }
    public double? Toleranz { get; init; }
    public int Vorrang { get; init; }
    public string? Meldung { get; init; }
    /// <summary>Datei, Seite bzw. Layout, Stelle.</summary>
    public string Stelle { get; init; } = "";
    public bool Bild => Befund?.Art == "bild";
}

/// <summary>Zugriff auf die Befunde der Unterlagen eines Projekts über Befund-Ids («&lt;Prüfsumme 12&gt;#&lt;n&gt;»): sucht die Unterlage
/// zur Prüfsumme (Unterlage-Element, sonst Datei im Ordner unterlagen), liest ihre Befunde einmal (Ablage) und hält ein
/// Verzeichnis der Ids und Tabellenzellen.</summary>
public sealed class BefundQuelle(Projekt projekt)
{
    sealed class Satz
    {
        public UnterlageRef? Ref;
        public UnterlagenBefunde? Befunde;
        public Dictionary<string, UnterlagenBefund> NachId = new(StringComparer.Ordinal);
        public Dictionary<(string Tabelle, int Zeile, int Spalte), UnterlagenBefund> Zellen = [];
        public string? Fehler;
    }

    readonly Dictionary<string, Satz> saetze = new(StringComparer.Ordinal);
    public Projekt Projekt { get; } = projekt;

    public static string? Praefix(string id) => id.Length > 13 && id[12] == '#' ? id[..12] : null;

    Satz Laden(string praefix)
    {
        if (saetze.TryGetValue(praefix, out var s)) return s;
        s = new Satz();
        saetze[praefix] = s;
        try
        {
            s.Ref = Finden(praefix);
            if (s.Ref is null) { s.Fehler = $"Zur Prüfsumme «{praefix}» gibt es im Projekt keine Unterlage"; return s; }
            var r = UnterlagenService.Lesen(Projekt, s.Ref);
            s.Befunde = r.Befunde;
            foreach (var b in r.Befunde.Befunde)
            {
                s.NachId[b.Id] = b;
                if (b.Art == "zelle" && b.Zusatz?["tabelle"] is JsonValue tv && tv.TryGetValue<string>(out var t)
                    && Json.Zahl(b.Zusatz["zeile"]) is double z && Json.Zahl(b.Zusatz["spalte"]) is double sp)
                    s.Zellen[(t, (int)z, (int)sp)] = b;
            }
        }
        catch (HlksFehler f) { s.Fehler = $"Die Unterlage zur Prüfsumme «{praefix}» lässt sich nicht lesen: {f.Message}"; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        { s.Fehler = $"Die Unterlage zur Prüfsumme «{praefix}» lässt sich nicht lesen: {ex.Message}"; }
        return s;
    }

    /// <summary>Unterlage zur Prüfsumme (12 Zeichen): Unterlage-Element mit dieser Datei, sonst Datei «&lt;prüfsumme&gt;_…» im Ordner.</summary>
    public UnterlageRef? Finden(string praefix)
    {
        foreach (var u in Projekt.Modell.DerKlasse("Unterlage").OrderBy(u => u.Kennzeichen, StringComparer.OrdinalIgnoreCase))
            if (u.Text("datei") is { } d && d.StartsWith(praefix + "_", StringComparison.OrdinalIgnoreCase))
            {
                try { return UnterlagenService.Aufloesen(Projekt, u.Kennzeichen); } catch (HlksFehler) { }
            }
        var ordner = Unterlagen.Ordner(Projekt);
        if (!Directory.Exists(ordner)) return null;
        foreach (var f in Directory.EnumerateFiles(ordner, praefix + "_*").OrderBy(f => f.Length))
        {
            var name = Path.GetFileName(f);
            if (name.StartsWith('.') || UnterlagenArten.AusEndung(name, f) is null) continue;
            try { return UnterlagenService.Aufloesen(Projekt, name); } catch (HlksFehler) { }
        }
        return null;
    }

    /// <summary>Unterlage zur Prüfsumme, wenn sie im Projekt liegt und lesbar ist.</summary>
    public UnterlageRef? Unterlage(string praefix) => Laden(praefix) is { Fehler: null } s ? s.Ref : null;

    /// <summary>Alle Befunde der Unterlage zur Prüfsumme (leer, wenn es sie nicht gibt).</summary>
    public IReadOnlyList<UnterlagenBefund> Alle(string praefix) => Laden(praefix).Befunde?.Befunde ?? [];

    /// <summary>Befund zur Id oder null mit Grund.</summary>
    public UnterlagenBefund? Hole(string id, out string? fehler)
    {
        fehler = null;
        var p = Praefix(id);
        if (p is null) { fehler = $"«{id}» ist keine Befund-Id"; return null; }
        var s = Laden(p);
        if (s.Fehler is not null) { fehler = s.Fehler; return null; }
        if (s.NachId.TryGetValue(id, out var b)) return b;
        fehler = $"Den Befund «{id}» gibt es nicht – «{s.Ref!.Original}» hat {s.NachId.Count} Befunde (#1 bis #{s.NachId.Count})";
        return null;
    }

    /// <summary>Gibt es eine Unterlage zur Prüfsumme der Id im Projekt?</summary>
    public bool UnterlageDa(string id) => Praefix(id) is { } p && Laden(p).Fehler is null;

    /// <summary>Zelle einer Tabelle (Zeile und Spalte ab 1) oder null.</summary>
    public UnterlagenBefund? Zelle(string tabelle, int zeile, int spalte) =>
        Praefix(tabelle) is { } p && Laden(p).Zellen.TryGetValue((tabelle, zeile, spalte), out var b) ? b : null;

    /// <summary>Alle Zellen einer Tabelle.</summary>
    public IEnumerable<UnterlagenBefund> Zellen(string tabelle) =>
        Praefix(tabelle) is { } p ? Laden(p).Zellen.Where(kv => kv.Key.Tabelle == tabelle).Select(kv => kv.Value) : [];

    public UnterlageRef? RefVon(string id) => Praefix(id) is { } p ? Laden(p).Ref : null;

    /// <summary>Art der Unterlage (pdf, dxf, step …) zur Befund-Id.</summary>
    public string? ArtVon(string id) => RefVon(id)?.Art;

    /// <summary>Stelle eines Befunds für Meldungen: Datei, Seite bzw. Layout/Layer, Tabellenstelle.</summary>
    public string Stelle(UnterlagenBefund b)
    {
        var sb = new StringBuilder(b.Datei);
        if (b.Seite is int s && RefVon(b.Id)?.Art is "pdf" or "bild") sb.Append($", Seite {s}");
        if (b.Layout is { } l) sb.Append(RefVon(b.Id)?.Art is "xlsx" ? $", Blatt «{l}»" : $", Layout {l}");
        if (b.Layer is { } ly) sb.Append($", Layer {ly}");
        if (b.Art == "zelle" && b.Zusatz is { } z)
            sb.Append($", Tabelle Zeile {z["zeile"]}, Spalte {z["spalte"]}{(z["kopf"]?.ToString() is { Length: > 0 } k ? $" «{k}»" : "")}");
        sb.Append($" ({b.Id}, {b.Art})");
        return sb.ToString();
    }

    static readonly Dictionary<string, (long Laenge, long Zeit, string Summe)> summen = [];

    /// <summary>SHA-256 der Unterlagendatei (64 Zeichen), zwischengespeichert je Pfad, Länge und Zeit.</summary>
    public static string Pruefsumme(string pfad)
    {
        var fi = new FileInfo(pfad);
        lock (summen)
            if (summen.TryGetValue(pfad, out var x) && x.Laenge == fi.Length && x.Zeit == fi.LastWriteTimeUtc.Ticks) return x.Summe;
        string s;
        using (var f = File.OpenRead(pfad)) s = Convert.ToHexString(SHA256.HashData(f)).ToLowerInvariant();
        lock (summen) summen[pfad] = (fi.Length, fi.LastWriteTimeUtc.Ticks, s);
        return s;
    }
}

/// <summary>Kandidaten eines Befunds und Vergleich eines Familienwerts mit einem Befund.</summary>
public static class QuellVergleiche
{
    public const string AusBild = "aus Bild gelesen, ungeprüft";

    static string? T(JsonNode? n) => n is JsonValue v ? (v.TryGetValue<string>(out var s) ? s : v.ToJsonString()) : null;

    /// <summary>Zahlen und Texte eines Befunds. nichtPruefbar: Grund, wenn der Befund keine Zahl und keinen Text trägt
    /// (Bild, Linie …); keineQuelle: Grund, wenn er kein Wert sein kann (Tabelle, Seite, Block).</summary>
    public static List<QuellKandidat> Kandidaten(UnterlagenBefund b, out string? nichtPruefbar, out string? keineQuelle)
    {
        nichtPruefbar = null; keineQuelle = null;
        var l = new List<QuellKandidat>();
        switch (b.Art)
        {
            case "zelle":
            {
                var text = T(b.Zusatz?["text"]) ?? T(b.Wert) ?? "";
                var kopf = T(b.Zusatz?["kopf"]);
                var einheit = QuellEinheiten.Normal(b.Einheit ?? Bau.EinheitAusKopf(kopf));
                QuellZahl? z = null;
                if (b.Wert is JsonValue v && v.GetValueKind() == JsonValueKind.Number)
                {
                    var d = Json.Zahl(v)!.Value;
                    var ausText = QuellZahlen.Lesen(text);
                    z = new QuellZahl(d, einheit ?? ausText?.Einheit, ausText is not null && Math.Abs(ausText.Wert - d) < 1e-9 ? ausText.Stellen : QuellZahlen.Stellen(d), text);
                }
                else if (QuellZahlen.Lesen(text) is { } q) z = q with { Einheit = q.Einheit ?? einheit };
                l.Add(new QuellKandidat(kopf, z, text));
                break;
            }
            case "wort":
            {
                var text = T(b.Wert) ?? "";
                l.Add(new QuellKandidat(null, QuellZahlen.Lesen(text), text));
                break;
            }
            case "zeile" or "text":
            {
                var text = T(b.Wert) ?? "";
                foreach (var z in QuellZahlen.AlleLesen(text)) l.Add(new QuellKandidat(null, z, null));
                l.Add(new QuellKandidat(null, null, text));
                break;
            }
            case "mass":
                if (b.Wert is JsonValue mv && mv.GetValueKind() == JsonValueKind.Number)
                {
                    var d = Json.Zahl(mv)!.Value;
                    l.Add(new QuellKandidat(T(b.Zusatz?["angezeigt"]), new QuellZahl(d, QuellEinheiten.Normal(b.Einheit), QuellZahlen.Stellen(d), T(b.Zusatz?["angezeigt"]) ?? d.ToString(CultureInfo.InvariantCulture)), null));
                }
                else if (Json.Zahl(b.Zusatz?["zahl"]) is double pz)
                    l.Add(new QuellKandidat(null, QuellZahlen.Lesen(T(b.Wert)) ?? new QuellZahl(pz, null, QuellZahlen.Stellen(pz), T(b.Wert) ?? ""), T(b.Wert)));
                else nichtPruefbar = $"Masslinie mit Buchstabe «{T(b.Wert)}» ohne Zahl (die Zahl steht in der Tabelle) – ungeprüft";
                break;
            case "typ" or "pset" or "port":
            {
                var w = b.Wert as JsonObject;
                var werte = (b.Art == "typ" ? w?["werte"] : w?["eigenschaften"]) as JsonObject;
                var einheiten = w?["einheiten"] as JsonObject;
                foreach (var (k, v) in werte ?? [])
                {
                    var e = QuellEinheiten.Normal(T(einheiten?[k]));
                    if (v is JsonValue jv && jv.GetValueKind() == JsonValueKind.Number)
                    {
                        var d = Json.Zahl(jv)!.Value;
                        l.Add(new QuellKandidat(k, new QuellZahl(d, e, QuellZahlen.Stellen(d), d.ToString(CultureInfo.InvariantCulture)), null));
                    }
                    else if (T(v) is { } s)
                    {
                        var z = QuellZahlen.Lesen(s);
                        l.Add(new QuellKandidat(k, z is null ? null : z with { Einheit = z.Einheit ?? e }, s));
                    }
                }
                if (T(w?["name"]) is { } name) l.Add(new QuellKandidat("name", null, name));
                if (b.Art == "port" && w?["fluss"] is { } fl && T(fl) is { } fs) l.Add(new QuellKandidat("fluss", null, fs));
                break;
            }
            case "stutzen":
                foreach (var k in new[] { "aussendurchmesser", "innendurchmesser", "laenge", "bohrungstiefe" }) Mm(l, b.Wert?[k], k);
                break;
            case "form3d":
                if (b.Wert?["huelle"]?["groesse"] is JsonArray g && g.Count == 3)
                    for (int i = 0; i < 3; i++) Mm(l, g[i], "groesse " + "xyz"[i]);
                break;
            case "flaeche":
                foreach (var k in new[] { "durchmesser", "radius", "laenge" }) Mm(l, b.Wert?[k], k);
                break;
            case "ansicht":
                foreach (var k in new[] { "breite", "hoehe" }) Mm(l, b.Wert?[k], k);
                break;
            case "bild":
                nichtPruefbar = AusBild;
                break;
            case "linie" or "polylinie" or "bogen" or "kreis":
                nichtPruefbar = $"{b.Art} ohne Zahl (aus der Zeichnung gelesen) – ungeprüft";
                break;
            case "tabelle" or "seite" or "block":
                keineQuelle = $"ein Befund der Art «{b.Art}» ist keine Quelle eines einzelnen Werts";
                break;
            default:
                nichtPruefbar = $"Befundart «{b.Art}» ohne nachrechenbaren Wert – ungeprüft";
                break;
        }
        return l;
    }

    static void Mm(List<QuellKandidat> l, JsonNode? n, string name)
    {
        if (Json.Zahl(n) is double d) l.Add(new QuellKandidat(name, new QuellZahl(d, "mm", QuellZahlen.Stellen(d), d.ToString(CultureInfo.InvariantCulture)), null));
    }

    /// <summary>Einheit, in der ein Parameter seine Werte führt (Länge mm, Winkel Grad, DN keine).</summary>
    public static string? ParameterEinheit(FamParameter p) => p.Datentyp switch
    {
        "laenge" => "mm", "winkel" => "°", "dn" => null, _ => QuellEinheiten.Normal(p.Einheit),
    };

    /// <summary>Ist der Parameter ein Durchmesser (Toleranz 1 mm)? Annahme: Name oder Beschreibung enthält «durchmesser», oder
    /// der Parameter ist genau das d eines Anschlusses.</summary>
    public static bool IstDurchmesser(FamilieDef f, FamParameter p) =>
        p.Datentyp == "laenge" && (p.Name.Contains("durchmesser", StringComparison.OrdinalIgnoreCase)
            || (p.Beschreibung?.Contains("durchmesser", StringComparison.OrdinalIgnoreCase) ?? false)
            || f.Anschluesse.Any(a => a.D?.Text?.Trim() == p.Name));

    /// <summary>Toleranz in der Einheit des Parameters für einen Quellwert <paramref name="q"/> (umgerechnet).</summary>
    public static double Toleranz(string datentyp, bool durchmesser, double q, int stellen, double faktor) => datentyp switch
    {
        "laenge" => durchmesser ? 1.0 : Math.Max(1.0, 0.005 * Math.Abs(q)),
        "dn" => 1e-6,
        _ => 0.5 * Math.Pow(10, -Math.Min(stellen, 9)) * Math.Abs(faktor),
    };

    /// <summary>Toleranz als Text für Meldungen und Konflikte.</summary>
    public static string ToleranzText(string datentyp, bool durchmesser, double tol, string? einheit) => datentyp switch
    {
        "laenge" => durchmesser ? "Durchmesser ±1 mm" : $"Länge max(1 mm; 0.5 %) = ±{F(tol)} mm",
        "dn" => "DN genau",
        _ => $"auf die Stellen der Unterlage = ±{F(tol)}{(einheit is null ? "" : " " + einheit)}",
    };

    public static string F(double d) => Math.Round(d, 6).ToString("0.######", CultureInfo.InvariantCulture);

    /// <summary>Vorrang eines Befunds für den Vorschlag eines Konflikts (höher = stärker), siehe Dateikopf.</summary>
    public static int Vorrang(UnterlagenBefund b, string? unterlagenArt, bool geometrie)
    {
        bool modell3d = unterlagenArt is "step" or "iges" or "stl" or "obj" or "ifc";
        if (b.Art == "bild") return 1;
        if (geometrie)
        {
            if (modell3d && b.Art is "form3d" or "stutzen" or "flaeche" or "ansicht" or "port") return 5;
            if (unterlagenArt is "dwg" or "dxf" && b.Art == "mass") return 4;
            if (b.Art is "zelle" or "typ" or "pset" or "mass" or "port") return 3;
            return 2;
        }
        if (b.Art is "zelle" or "typ" or "pset" or "port") return 3;
        return 2;
    }

    public static string VorrangText(int v, bool geometrie) => v switch
    {
        5 => "3D-Modell (STEP/IFC/STL/OBJ)",
        4 => "DWG/DXF-Bemassung",
        3 => geometrie ? "PDF-Zelle bzw. PDF-Vektor / Tabelle" : "Tabelle",
        2 => "Fliesstext",
        _ => "Bild",
    };

    /// <summary>Passt der Name einer Spalte/Eigenschaft zum Parameter (Name oder Ziel; «L [mm]» passt zu «L»)?</summary>
    public static bool NamePasst(string? spalte, string name, string? ziel = null)
    {
        if (string.IsNullOrWhiteSpace(spalte)) return false;
        var s = QuellZahlen.NameNormal(spalte);
        if (s.Length == 0) return false;
        if (s == QuellZahlen.NameNormal(name) || (ziel is not null && s == QuellZahlen.NameNormal(ziel))) return true;
        var erstes = QuellZahlen.NameNormal(Regex.Split(spalte.Trim(), @"[\s\[\(]")[0]);
        return erstes.Length > 0 && (erstes == QuellZahlen.NameNormal(name) || (ziel is not null && erstes == QuellZahlen.NameNormal(ziel)));
    }

    /// <summary>Text zum Vergleich (Nachtrag Hauptlauf 18.10.2026): klein, «×» als «x», Satzzeichen und Aufzählungszeichen (• | – …)
    /// als Leerraum, Leerraum zusammengefasst. Erhalten bleiben Buchstaben, Ziffern, «/» (Gewinde 1/2) und der Dezimalpunkt zwischen Ziffern.</summary>
    public static string TextVergleich(string? s)
    {
        var t = (s ?? "").ToLowerInvariant().Replace('×', 'x');
        var sb = new StringBuilder(t.Length);
        for (int i = 0; i < t.Length; i++)
        {
            char c = t[i];
            if (char.IsLetterOrDigit(c) || c == '/') sb.Append(c);
            else if (c is '.' or ',' && i > 0 && i + 1 < t.Length && char.IsDigit(t[i - 1]) && char.IsDigit(t[i + 1])) sb.Append('.');
            else sb.Append(' ');
        }
        var n = Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
        // gemischter Bruch (Gewinde «1 1/4», «1-1/4») ist ein Wort: «Rp 1/4» steht nicht in «Rp 1 1/4»
        return Regex.Replace(n, @"(?<![\d.])(\d+) (\d+/\d+)", "$1+$2");
    }

    /// <summary>Passt ein Textwert der Familie zu einem Text der Unterlage (nach <see cref="TextVergleich"/>)? Ja, wenn er gleich ist, als
    /// Teilstück aus ganzen Wörtern in der Quelle steht, alle seine Wörter (ab 2 Zeichen; Zahlen immer) in der Quelle vorkommen,
    /// oder die Quelle am Ende hinter einem Vorsatz ohne Ziffern steht («Rp 1/2» ↔ «1/2»). Sonst nicht («ULDA» ↔ «ULSA»).</summary>
    public static bool TextPasst(string familie, string quelle)
    {
        var a = TextVergleich(familie); var b = TextVergleich(quelle);
        if (a.Length == 0 || b.Length == 0) return false;
        if (a == b) return true;
        if (Regex.IsMatch(b, @"(?<![\w/])" + Regex.Escape(a) + @"(?![\w/])")) return true;
        if (a.EndsWith(" " + b, StringComparison.Ordinal) && !a[..^(b.Length + 1)].Any(char.IsDigit)) return true;
        var woerter = a.Split(' ').Where(w => w.Length >= 2 || w.Any(char.IsDigit)).ToList();
        var inQuelle = b.Split(' ').ToHashSet(StringComparer.Ordinal);
        return woerter.Count > 0 && woerter.All(inQuelle.Contains);
    }

    /// <summary>Vergleicht den Wert eines Parameters mit einem Befund. typName/kopfSpalte nur für Meldungen.</summary>
    public static QuellVergleich Vergleichen(FamilieDef f, FamParameter p, FWert wert, string id, BefundQuelle q, Func<bool>? unterlageFehltErlaubt = null)
    {
        var b = q.Hole(id, out var fehler);
        if (b is null)
        {
            var abwesend = !q.UnterlageDa(id) && unterlageFehltErlaubt?.Invoke() == true;
            return new QuellVergleich { Id = id, Status = abwesend ? "abwesend" : "fehlt", Meldung = fehler, Stelle = id };
        }
        var stelle = q.Stelle(b);
        var art = q.ArtVon(id);
        bool geometrie = p.Datentyp is "laenge" or "winkel";
        int vorrang = Vorrang(b, art, geometrie);
        var kand = Kandidaten(b, out var nichtPruefbar, out var keineQuelle);
        if (keineQuelle is not null)
            return new QuellVergleich { Id = id, Befund = b, Status = "keine", Meldung = keineQuelle, Stelle = stelle, Vorrang = vorrang };
        if (nichtPruefbar is not null || p.Datentyp == "ja_nein")
            return new QuellVergleich { Id = id, Befund = b, Status = "ungeprueft", Meldung = nichtPruefbar ?? "ja/nein-Werte werden nicht nachgerechnet – ungeprüft", Stelle = stelle, Vorrang = vorrang };

        if (wert.IstText)
        {
            var texte = kand.Where(k => k.Text is not null).ToList();
            var bevorzugt = texte.Where(k => NamePasst(k.Name, p.Name, p.Ziel)).ToList();
            if (bevorzugt.Count == 0) bevorzugt = texte;
            foreach (var k in bevorzugt)
            {
                if (TextPasst(wert.Text!, k.Text!)) return new QuellVergleich { Id = id, Befund = b, Status = "passt", QuellText = k.Text, Stelle = stelle, Vorrang = vorrang };
                // Kopf als Vorsatz: Spalte «Rp [Zoll]» mit Zelle «1/2» ↔ «Rp 1/2»
                if (k.Name is { } kopf && b.Art == "zelle" && TextPasst(wert.Text!, Regex.Split(kopf.Trim(), @"[\s\[\(]")[0] + " " + k.Text))
                    return new QuellVergleich { Id = id, Befund = b, Status = "passt", QuellText = k.Text, Stelle = stelle, Vorrang = vorrang };
            }
            var zeigen = bevorzugt.FirstOrDefault()?.Text;
            if (zeigen is null)
                return new QuellVergleich { Id = id, Befund = b, Status = "weicht", Meldung = $"der Befund nennt keinen Text für «{p.Name}»", Stelle = stelle, Vorrang = vorrang };
            return new QuellVergleich { Id = id, Befund = b, Status = "weicht", QuellText = zeigen, Stelle = stelle, Vorrang = vorrang,
                Meldung = $"Familie «{wert.Text}», Quelle «{Kurz(zeigen)}»" };
        }

        var pe = ParameterEinheit(p);
        bool dm = IstDurchmesser(f, p);
        var zahlen = kand.Where(k => k.Zahl is not null).ToList();
        var benannt = zahlen.Where(k => NamePasst(k.Name, p.Name, p.Ziel)).ToList();
        if (b.Art is "typ" or "pset" or "port" && benannt.Count > 0) zahlen = benannt;
        if (zahlen.Count == 0)
            return new QuellVergleich { Id = id, Befund = b, Status = "weicht", Stelle = stelle, Vorrang = vorrang,
                Meldung = $"der Befund trägt keine Zahl ({Kurz(kand.FirstOrDefault()?.Text ?? T(b.Wert) ?? "")})" };
        double v = wert.Zahl;
        QuellVergleich? naechste = null; double naechsteAbstand = double.PositiveInfinity;
        string? einheitFehler = null;
        foreach (var k in zahlen)
        {
            var z = k.Zahl!;
            double faktor = 1;
            if (p.Datentyp != "dn" && z.Einheit is not null && pe is not null)
            {
                if (QuellEinheiten.Faktor(z.Einheit, pe) is double fk) faktor = fk;
                else { einheitFehler = $"Einheit «{z.Einheit}» der Quelle lässt sich nicht in «{pe}» umrechnen"; continue; }
            }
            if (p.Datentyp != "dn" && z.Bruch && pe is not null && z.Einheit is null && p.Datentyp == "laenge") continue;   // Zoll-Bruch ist keine Länge
            double qw = z.Wert * faktor;
            double tol = Toleranz(p.Datentyp, dm, qw, z.Stellen, faktor);
            double ab = Math.Abs(v - qw);
            var text = z.Text + (z.Einheit is not null && !z.Text.Contains(z.Einheit, StringComparison.Ordinal) ? " " + z.Einheit : "");
            var erg = new QuellVergleich
            {
                Id = id, Befund = b, Status = ab <= tol + 1e-9 * Math.Max(1, Math.Abs(qw)) ? "passt" : "weicht", Quellwert = qw, QuellText = text,
                Toleranz = tol, Stelle = stelle, Vorrang = vorrang,
                Meldung = $"Familie {F(v)}{(pe is null ? "" : " " + pe)}, Quelle {text}{(faktor != 1 ? $" = {F(qw)} {pe}" : "")} (Toleranz {ToleranzText(p.Datentyp, dm, tol, pe)})",
            };
            if (erg.Status == "passt") return erg;
            if (ab < naechsteAbstand) { naechsteAbstand = ab; naechste = erg; }
        }
        if (naechste is not null) return naechste;
        return new QuellVergleich { Id = id, Befund = b, Status = "einheit", Stelle = stelle, Vorrang = vorrang, Meldung = einheitFehler ?? "keine vergleichbare Zahl" };
    }

    static string Kurz(string s) => s.Length > 60 ? s[..60] + "…" : s;
}
