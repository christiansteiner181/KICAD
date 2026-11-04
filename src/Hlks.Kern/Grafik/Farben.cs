// Farben der Heizkörper (Christian 04.11.2026: «Heizkörper in Grundrissen Grün, 3d in realer Farbe. diese muss einstellbar sein. mit standard RAL
// und NCF auswahl, (mit Farbmster bei der Auswahl)»).
// - Grundriss: Heizkörper in der Planfarbe aus kataloge/farben.json › plan (grün), unabhängig vom Medium.
// - 3D, Schnitt, Ansicht: Oberflächenfarbe aus der Eigenschaft farbe des Heizkörpers (RAL-, NCS-Code oder #RRGGBB), sonst die Standardfarbe
//   des Herstellers (farben.json › herstellerstandard, Hersteller = Anfang des Familiennamens), sonst wie bisher die Farbe des Mediums.
// Farbwerte nur aus farben.json (Quelle je Tabelle); RAL und NCS geben Bildschirmwerte als Annäherung an.

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Hlks.Kern;

public static class Farben
{
    /// <summary>Planfarbe, wenn farben.json keine nennt (Grün; Farbton Annahme Hauptlauf, Entscheid Christian «Grün»).</summary>
    const string HeizkoerperPlanVorgabe = "#1f9a3c";

    sealed record Tabelle(Dictionary<string, (string Hex, string Name)> Codes, Dictionary<string, string> Plan, List<(string Hersteller, string Farbe)> Standard);

    static readonly Dictionary<string, (DateTime, Tabelle)> Speicher = [];
    static readonly Lock Sperre = new();

    static Tabelle Laden(Katalog k)
    {
        var pfad = Path.Combine(k.Ordner, "farben.json");
        if (!File.Exists(pfad)) return new([], [], []);
        var stand = File.GetLastWriteTimeUtc(pfad);
        lock (Sperre)
        {
            if (Speicher.TryGetValue(pfad, out var z) && z.Item1 == stand) return z.Item2;
            var j = JsonNode.Parse(File.ReadAllText(pfad))!.AsObject();
            var codes = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);
            foreach (var reihe in new[] { "ral", "ncs" })
                foreach (var f in (j[reihe]?["farben"] as JsonArray ?? []).OfType<JsonObject>())
                    if ((string?)f["code"] is { } c && (string?)f["hex"] is { } h) codes[Schluessel(c)] = (h, (string?)f["name"] ?? "");
            var plan = (j["plan"] as JsonObject ?? []).Where(x => x.Value is JsonObject).ToDictionary(x => x.Key, x => (string?)x.Value!["farbe"] ?? "", StringComparer.OrdinalIgnoreCase);
            var std = (j["herstellerstandard"] as JsonArray ?? []).OfType<JsonObject>()
                .Where(x => (string?)x["hersteller"] is not null && (string?)x["farbe"] is not null).Select(x => ((string)x["hersteller"]!, (string)x["farbe"]!)).ToList();
            var t = new Tabelle(codes, plan, std);
            Speicher[pfad] = (stand, t);
            return t;
        }
    }

    /// <summary>Code vergleichbar machen: «ral9016», «RAL  9016» → «RAL 9016»; «S 0502-Y» → «NCS S 0502-Y».</summary>
    public static string Schluessel(string code)
    {
        var c = Regex.Replace(code.Trim().ToUpperInvariant(), @"\s+", " ");
        c = Regex.Replace(c, @"^RAL\s*", "RAL ");
        if (c.StartsWith("S ") || Regex.IsMatch(c, @"^\d{4}-")) c = "NCS " + c;
        return c;
    }

    /// <summary>Hex-Wert zu einem Farbcode (RAL, NCS) oder direkt #RRGGBB; null, wenn unbekannt.</summary>
    public static string? Hex(Katalog k, string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        if (Regex.IsMatch(code.Trim(), "^#[0-9a-fA-F]{6}$")) return code.Trim().ToLowerInvariant();
        return Laden(k).Codes.TryGetValue(Schluessel(code), out var f) ? f.Hex.ToLowerInvariant() : null;
    }

    public const string Text = "Farbtabellen für die Oberflächenfarbe der Heizkörper (RAL Classic, NCS) mit Code, Name und Hex-Wert sowie Quelle und Hinweis je Tabelle (kataloge/farben.json); dazu die Standardfarben der Hersteller und die Planfarbe im Grundriss.";
    public static IReadOnlyList<ParameterDef> Parameter { get; } = [];

    /// <summary>Abfrage farben: Inhalt von kataloge/farben.json (leer, wenn die Datei fehlt).</summary>
    public static JsonObject Abfrage(Katalog k)
    {
        var pfad = Path.Combine(k.Ordner, "farben.json");
        return File.Exists(pfad) ? JsonNode.Parse(File.ReadAllText(pfad))!.AsObject() : new JsonObject { ["ral"] = new JsonObject { ["farben"] = new JsonArray() }, ["ncs"] = new JsonObject { ["farben"] = new JsonArray() } };
    }

    static bool IstHeizkoerper(Katalog k, Element e) => k.IstArt(e, "Heizkoerper");

    /// <summary>Planfarbe im Grundriss: Heizkörper grün (farben.json › plan.Heizkoerper), sonst null (Farbe des Mediums).</summary>
    public static string? Plan(Katalog k, Element e) =>
        IstHeizkoerper(k, e) ? (Laden(k).Plan.TryGetValue("Heizkoerper", out var f) && f.Length > 0 ? f : HeizkoerperPlanVorgabe) : null;

    /// <summary>Oberflächenfarbe im 3D: Eigenschaft farbe, sonst Standard des Herstellers; null = Farbe des Mediums.</summary>
    public static string? Oberflaeche(Projekt p, Element e)
    {
        if (!IstHeizkoerper(p.Katalog, e)) return null;
        if (Hex(p.Katalog, e.Text("farbe")) is { } eigen) return eigen;
        var fam = e.Bezuege.TryGetValue("familie", out var fid) ? p.Modell.Finde(fid)?.Name : null;
        if (fam is not null)
            foreach (var (h, f) in Laden(p.Katalog).Standard)
                if (fam.StartsWith(h, StringComparison.OrdinalIgnoreCase) && Hex(p.Katalog, f) is { } std) return std;
        return null;
    }
}
