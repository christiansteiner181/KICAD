// Hilfen der Leser für Herstellerunterlagen (Modul 2, Auftrag B2): Befund-Sammler (laufende Ids), Zahlen aus Text, Runden.
// Die Schnittstelle selbst (UnterlagenBefund, IUnterlagenLeser) steht in UnterlagenBefund.cs und bleibt unverändert.

using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Hlks.Kern;

/// <summary>Sammelt die Befunde eines Lesers und vergibt die Ids «&lt;Prüfsumme[..12]&gt;#&lt;n&gt;» in der Reihenfolge des Anlegens.</summary>
sealed class BefundSammler
{
    readonly string praefix;
    public UnterlagenBefunde Ergebnis { get; }
    readonly string leserText;

    public BefundSammler(IUnterlagenLeser leser, string datei, string pruefsumme, string art)
    {
        praefix = pruefsumme.Length > 12 ? pruefsumme[..12] : pruefsumme;
        leserText = $"{leser.Name} {leser.Version}";
        Ergebnis = new UnterlagenBefunde { Datei = datei, Pruefsumme = pruefsumme, Art = art, Leser = leserText };
    }

    /// <summary>Nummer, die der nächste Befund bekommt (ab 1).</summary>
    public int Naechste => Ergebnis.Befunde.Count + 1;
    public int Anzahl => Ergebnis.Befunde.Count;
    public string Id(int n) => $"{praefix}#{n}";
    public List<string> Hinweise => Ergebnis.Hinweise;

    public UnterlagenBefund Neu(string art, JsonNode? wert = null, string? einheit = null, int? seite = null, string? layout = null, string? layer = null,
        List<double[]>? lage = null, double[]? box = null, double sicherheit = 1, JsonObject? zusatz = null)
    {
        var b = new UnterlagenBefund
        {
            Id = Id(Naechste), Datei = Ergebnis.Datei, Art = art, Wert = wert, Einheit = einheit, Seite = seite, Layout = layout, Layer = layer,
            Lage = lage, Box = box, Sicherheit = sicherheit, Leser = leserText, Zusatz = zusatz,
        };
        Ergebnis.Befunde.Add(b);
        return b;
    }

    /// <summary>Hinweis einmal aufnehmen (gleicher Text nicht doppelt).</summary>
    public void Hinweis(string text) { if (!Ergebnis.Hinweise.Contains(text)) Ergebnis.Hinweise.Add(text); }
}

static class Bau
{
    public static double R(double x) => Math.Round(x, 2);
    public static double R3(double x) => Math.Round(x, 3);
    public static double[] Box(double x0, double y0, double x1, double y1) => [R(Math.Min(x0, x1)), R(Math.Min(y0, y1)), R(Math.Max(x0, x1)), R(Math.Max(y0, y1))];
    public static double[] P(double x, double y) => [R(x), R(y)];
    public static double[] P(double x, double y, double z) => [R3(x), R3(y), R3(z)];

    static readonly Regex ZahlMuster = new(@"^[+\-−–]?\d{1,3}(?:['’ ]\d{3})*(?:[.,]\d+)?$|^[+\-−–]?\d+(?:[.,]\d+)?$", RegexOptions.Compiled);

    /// <summary>Text als Zahl, wenn er genau eine Zahl ist (Dezimalkomma oder -punkt, Tausendertrenner ' oder Leerzeichen); sonst null.
    /// Brüche («1/2»), Bereiche und Wörter bleiben Text.</summary>
    public static double? Zahl(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.Trim();
        if (!ZahlMuster.IsMatch(t)) return null;
        t = t.Replace("−", "-").Replace("–", "-").Replace("'", "").Replace("’", "").Replace(" ", "");
        // «1,234» ist hier eine Dezimalzahl (Schweizer und deutsche Datenblätter), kein Tausendertrenner
        t = t.Replace(',', '.');
        return double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && !double.IsNaN(d) && !double.IsInfinity(d) ? d : null;
    }

    /// <summary>Wert eines Zelltexts: Zahl, wenn eindeutig, sonst der Text.</summary>
    public static JsonNode? ZellWert(string text) => Zahl(text) is double d ? JsonValue.Create(d) : text.Length == 0 ? null : JsonValue.Create(text);

    public static JsonArray Feld(IEnumerable<double> werte) => new(werte.Select(w => (JsonNode)JsonValue.Create(w)).ToArray());
    public static JsonArray Texte(IEnumerable<string?> werte) => new(werte.Select(w => w is null ? null : (JsonNode)JsonValue.Create(w)).ToArray());

    /// <summary>Einheit aus einer Kopfzelle wie «L [mm]», «[kg]», «Kvs (m³/h)» oder «Länge (mm)»; sonst null.</summary>
    public static string? EinheitAusKopf(string? kopf)
    {
        if (string.IsNullOrWhiteSpace(kopf)) return null;
        var m = Regex.Match(kopf, @"[\[\(]\s*([^\]\)\[\(]{1,12}?)\s*[\]\)]\s*$");
        if (!m.Success) return null;
        var e = m.Groups[1].Value.Trim().Trim('"');
        return e.Length == 0 ? null : e;
    }
}
