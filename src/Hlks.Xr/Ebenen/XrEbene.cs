// Quest App P4b (Modul XR, «Ergänzungen»: Schnittstelle für Fachmodule – «Fachmodule legen eigene Ebenen in die Szene, z. B. animierte Fliessrichtung, Luftströmung als
// Partikel (10a/10b) oder Temperaturfelder. Der Inhalt kommt vom Server; die App zeigt Linien, Pfeile, Partikel und Farbflächen. Im Prototyp genügt eine Probe-Ebene.»).
// Das Format einer Ebene, wie der Server es liefert (src/Hlks.Web/XrEbenenEndpunkte.cs), und das Lesen in der App. Rein, ohne StereoKit.
//
//   GET /api/xr/ebenen          { fassung: 1, projekt, ebenen: [{ name, titel, beschreibung, url, schluessel, elemente }] }
//   GET /api/xr/ebene/{name}    { art: "hlks-xr-ebene", fassung: 1, name, titel, quelle, projekt, schluessel, einheit: "mm",
//                                 elemente: [{ typ, geschoss?, element?, farbe?, deckkraft?, breite?, groesse?, tempo?, abstand?, punkte: [[x, y, z], …] }], hinweise: [] }
//
// Koordinaten in KICAD-mm, absolut (x Ost, y Nord, z oben) – wie die Posen im Live-Kanal; die App rechnet mit dem Bezugspunkt ihrer Szene um (KicadUmrechnung).
// Typen:  linie    Polylinie (≥ 2 Punkte), Breite in mm.
//         pfeil    Polylinie (≥ 2 Punkte) mit Spitze am letzten Punkt, Breite in mm.
//         punkte   einzelne Punkte (≥ 1), Durchmesser «groesse» in mm.
//         partikel Punkte (Durchmesser «groesse») laufen mit «tempo» mm/s im Abstand «abstand» mm die Polylinie (≥ 2 Punkte) entlang.
//         flaeche  ebenes Vieleck (≥ 3 Punkte, ohne Selbstüberschneidung), «farbe» mit «deckkraft».
// Bezug: «geschoss» (Id aus GET /api/xr/geschosse; null = in jedem Geschoss sichtbar) und «element» (Element-Id des Modells, null = keines).
// Unbekannte Typen überspringt die App mit Hinweis (spätere Fassungen dürfen Typen ergänzen); eine andere «fassung» weist sie ab.

using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Hlks.Xr.Netz;

namespace Hlks.Xr.Ebenen;

public static class XrEbeneFormat
{
    public const string Art = "hlks-xr-ebene";
    public const int Fassung = 1;
    public const string Linie = "linie", Pfeil = "pfeil", Punkte = "punkte", Partikel = "partikel", Flaeche = "flaeche";
    public static readonly IReadOnlySet<string> Typen = new HashSet<string>(StringComparer.Ordinal) { Linie, Pfeil, Punkte, Partikel, Flaeche };

    /// <summary>Grenzen gegen übergrosse Ebenen (die App muss sie zeichnen können).</summary>
    public const int ElementeMax = 50_000;
    public const int PunkteMax = 500_000;
    /// <summary>Koordinaten bis 100 km (mm).</summary>
    public const double KoordinateMax = 1e8;

    /// <summary>Vorgaben, wenn ein Wert fehlt (mm, mm/s).</summary>
    public const double BreiteVorgabe = 20, GroesseVorgabe = 50, TempoVorgabe = 500, AbstandVorgabe = 1000;

    /// <summary>Name einer Ebene: Kleinbuchstaben, Ziffern, «-» und «_», 1–40 Zeichen.</summary>
    public static readonly Regex NameMuster = new("^[a-z0-9][a-z0-9_-]{0,39}$", RegexOptions.CultureInvariant);

    /// <summary>Mindestpunkte je Typ.</summary>
    public static int MindestPunkte(string typ) => typ switch { Punkte => 1, Flaeche => 3, _ => 2 };
}

/// <summary>Eintrag der Liste GET /api/xr/ebenen.</summary>
public sealed record EbenenEintrag(string Name, string Titel, string? Beschreibung, string Url, string Schluessel, int Elemente);

/// <summary>Liste der Ebenen eines Projekts.</summary>
public sealed record EbenenListe(string? Projekt, IReadOnlyList<EbenenEintrag> Ebenen)
{
    static readonly Regex UrlMuster = new(@"^/api/xr/ebene/[a-z0-9][a-z0-9_-]{0,39}$", RegexOptions.CultureInvariant);

    /// <summary>Liest und prüft die Liste. Wirft <see cref="XrNetzFehler"/>. Adressen nur unter /api/xr/ebene/.</summary>
    public static EbenenListe Lesen(string text)
    {
        using var d = XrEbene.Parsen(text, "Die Liste der Ebenen");
        var o = d.RootElement;
        if (o.ValueKind != JsonValueKind.Object || !o.TryGetProperty("fassung", out var f) || f.ValueKind != JsonValueKind.Number || !f.TryGetInt32(out var fv)
            || fv != XrEbeneFormat.Fassung)
            throw new XrNetzFehler("Die Liste der Ebenen hat eine unbekannte Fassung.", "Quest App bzw. KICAD aktualisieren.");
        var liste = new List<EbenenEintrag>();
        if (o.TryGetProperty("ebenen", out var a) && a.ValueKind == JsonValueKind.Array)
            foreach (var e in a.EnumerateArray())
            {
                var name = XrEbene.Text(e, "name");
                var url = XrEbene.Text(e, "url");
                if (name is null || !XrEbeneFormat.NameMuster.IsMatch(name) || url is null || !UrlMuster.IsMatch(url)) continue;
                liste.Add(new EbenenEintrag(name, XrEbene.Text(e, "titel") ?? name, XrEbene.Text(e, "beschreibung"), url, XrEbene.Text(e, "schluessel") ?? "",
                    e.TryGetProperty("elemente", out var n) && n.ValueKind == JsonValueKind.Number && n.TryGetInt32(out var k) ? k : 0));
            }
        return new EbenenListe(XrEbene.Text(o, "projekt"), liste);
    }
}

/// <summary>Ein Element einer Ebene (Koordinaten in KICAD-mm, absolut). Farbe RGBA 0…1.</summary>
public sealed record EbenenElement(string Typ, string? Geschoss, string? Element, float[] Farbe, double BreiteMm, double GroesseMm, double TempoMmS, double AbstandMm,
    IReadOnlyList<double[]> PunkteMm);

/// <summary>Eine gelesene Ebene.</summary>
public sealed record XrEbene(string Name, string Titel, string? Quelle, string? Projekt, string Schluessel, IReadOnlyList<EbenenElement> Elemente, IReadOnlyList<string> Hinweise)
{
    internal static JsonDocument Parsen(string text, string was)
    {
        try { return JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 16 }); }
        catch (JsonException) { throw new XrNetzFehler($"{was} ist kein gültiges JSON."); }
    }

    internal static string? Text(JsonElement o, string name) =>
        o.ValueKind == JsonValueKind.Object && o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    static double Zahl(JsonElement o, string name, double vorgabe, double min, double max) =>
        o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d) && double.IsFinite(d) ? Math.Clamp(d, min, max) : vorgabe;

    /// <summary>#rgb oder #rrggbb (sonst weiss) mit Deckkraft 0…1 → RGBA 0…1.</summary>
    public static float[] Farbe(string? hex, double deckkraft = 1)
    {
        float[] f = [1, 1, 1, (float)Math.Clamp(double.IsFinite(deckkraft) ? deckkraft : 1, 0, 1)];
        var h = hex?.Trim();
        if (h is null || h.Length is not (4 or 7) || h[0] != '#' || !h.AsSpan(1).ToString().All(Uri.IsHexDigit)) return f;
        if (h.Length == 4) h = $"#{h[1]}{h[1]}{h[2]}{h[2]}{h[3]}{h[3]}";
        for (int i = 0; i < 3; i++) f[i] = int.Parse(h.AsSpan(1 + 2 * i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255f;
        return f;
    }

    /// <summary>
    /// Liest und prüft eine Ebene (Format siehe Dateikopf). Wirft <see cref="XrNetzFehler"/> bei falscher Art oder Fassung, ungültigem JSON und zu vielen Elementen bzw. Punkten.
    /// Ungültige Elemente (zu wenige Punkte, Punkte nicht endlich oder ausserhalb ±100 km, unbekannter Typ) fallen mit Hinweis weg.
    /// </summary>
    public static XrEbene Lesen(string text)
    {
        using var d = Parsen(text, "Die Ebene");
        var o = d.RootElement;
        if (o.ValueKind != JsonValueKind.Object || Text(o, "art") != XrEbeneFormat.Art)
            throw new XrNetzFehler("Das ist keine Ebene für die Quest App (art fehlt).");
        if (!o.TryGetProperty("fassung", out var fa) || fa.ValueKind != JsonValueKind.Number || !fa.TryGetInt32(out var fassung) || fassung != XrEbeneFormat.Fassung)
            throw new XrNetzFehler("Die Ebene hat eine unbekannte Fassung.", "Quest App bzw. KICAD aktualisieren.");
        var name = Text(o, "name");
        if (name is null || !XrEbeneFormat.NameMuster.IsMatch(name)) throw new XrNetzFehler("Der Name der Ebene ist ungültig.");
        if (Text(o, "einheit") is { } einheit && einheit != "mm") throw new XrNetzFehler($"Einheit «{einheit}» kennt die App nicht (nur mm).");
        var hinweise = new List<string>();
        var elemente = new List<EbenenElement>();
        long punkte = 0;
        var unbekannt = new SortedDictionary<string, int>(StringComparer.Ordinal);
        int ungueltig = 0;
        if (o.TryGetProperty("elemente", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            if (arr.GetArrayLength() > XrEbeneFormat.ElementeMax)
                throw new XrNetzFehler($"Die Ebene {name} hat mehr als {XrEbeneFormat.ElementeMax} Elemente.", "Die Ebene am Server zusammenfassen bzw. aufteilen.");
            foreach (var e in arr.EnumerateArray())
            {
                var typ = Text(e, "typ") ?? "";
                if (!XrEbeneFormat.Typen.Contains(typ)) { unbekannt[typ] = unbekannt.GetValueOrDefault(typ) + 1; continue; }
                var pl = new List<double[]>();
                var ok = e.TryGetProperty("punkte", out var pa) && pa.ValueKind == JsonValueKind.Array;
                if (ok)
                    foreach (var p in pa.EnumerateArray())
                    {
                        if (p.ValueKind != JsonValueKind.Array || p.GetArrayLength() != 3) { ok = false; break; }
                        var xyz = new double[3];
                        int i = 0;
                        foreach (var c in p.EnumerateArray())
                        {
                            if (c.ValueKind != JsonValueKind.Number || !c.TryGetDouble(out var v) || !double.IsFinite(v) || Math.Abs(v) > XrEbeneFormat.KoordinateMax) { ok = false; break; }
                            xyz[i++] = v;
                        }
                        if (!ok) break;
                        pl.Add(xyz);
                    }
                if (!ok || pl.Count < XrEbeneFormat.MindestPunkte(typ)) { ungueltig++; continue; }
                punkte += pl.Count;
                if (punkte > XrEbeneFormat.PunkteMax)
                    throw new XrNetzFehler($"Die Ebene {name} hat mehr als {XrEbeneFormat.PunkteMax} Punkte.", "Die Ebene am Server zusammenfassen bzw. aufteilen.");
                elemente.Add(new EbenenElement(typ, Text(e, "geschoss"), Text(e, "element"), Farbe(Text(e, "farbe"), Zahl(e, "deckkraft", 1, 0, 1)),
                    Zahl(e, "breite", XrEbeneFormat.BreiteVorgabe, 1, 1000), Zahl(e, "groesse", XrEbeneFormat.GroesseVorgabe, 1, 5000),
                    Zahl(e, "tempo", typ == XrEbeneFormat.Partikel ? XrEbeneFormat.TempoVorgabe : 0, 0, 100_000),
                    Zahl(e, "abstand", XrEbeneFormat.AbstandVorgabe, 10, 1_000_000), pl));
            }
        }
        foreach (var (typ, n) in unbekannt) hinweise.Add($"Ebene {name}: {n} Elemente vom Typ «{typ}» kennt diese App nicht (übergangen).");
        if (ungueltig > 0) hinweise.Add($"Ebene {name}: {ungueltig} Elemente ohne gültige Punkte (übergangen).");
        if (o.TryGetProperty("hinweise", out var h) && h.ValueKind == JsonValueKind.Array)
            foreach (var x in h.EnumerateArray().Take(10)) if (x.ValueKind == JsonValueKind.String) hinweise.Add($"Ebene {name}: {x.GetString()}");
        return new XrEbene(name, Text(o, "titel") ?? name, Text(o, "quelle"), Text(o, "projekt"), Text(o, "schluessel") ?? "", elemente, hinweise);
    }
}
