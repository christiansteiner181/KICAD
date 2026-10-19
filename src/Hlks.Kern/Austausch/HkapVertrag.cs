// HKAP-Universalschnittstelle (Modul 1d): Datenmodell des Austauschvertrags (entwurf/modul-1d.md, «Austauschvertrag»)
// und Hilfen zum Vergleichen von Feldwerten. Den Vertrag führt HKAP; KICAD liest ihn tolerant von Hand aus JSON
// (unbekannte Felder schaden nicht, Zahl statt Text bei Id oder Version auch nicht).
//
//   GET  /api/austausch/katalog                                → { fassung, datenarten: [{ name, felder: [{ name, typ, einheit, richtung, fuehrt }] }] }
//   GET  /api/austausch/{projekt}/{datenart}?projektierung=…   → { datensaetze: [{ id, version, felder }] }
//   POST /api/austausch/{projekt}/{datenart}?projektierung=…&trockenlauf=true|false
//        { quelle: "KICAD", datensaetze: [{ id?, version?, felder, loeschen? }] } → { ergebnisse: [{ id, status, version?, aktuell?, grund? }] }

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <summary>Austauschkatalog von HKAP: Datenarten mit ihren Feldern, Richtung und führender Seite.</summary>
public sealed class HkapKatalog
{
    public string Fassung { get; set; } = "?";
    public List<HkapDatenartDef> Datenarten { get; set; } = [];

    public HkapDatenartDef? Datenart(string name) =>
        Datenarten.FirstOrDefault(d => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public static HkapKatalog AusJson(JsonNode? n)
    {
        var k = new HkapKatalog { Fassung = HkapWerte.Text(n?["fassung"]) ?? "?" };
        foreach (var d in (n?["datenarten"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
        {
            var def = new HkapDatenartDef { Name = HkapWerte.Text(d["name"]) ?? "" };
            foreach (var f in (d["felder"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
                def.Felder.Add(new HkapFeldDef
                {
                    Name = HkapWerte.Text(f["name"]) ?? "", Typ = HkapWerte.Text(f["typ"]), Einheit = HkapWerte.Text(f["einheit"]),
                    Richtung = (HkapWerte.Text(f["richtung"]) ?? "").Trim().ToLowerInvariant(),
                    Fuehrt = (HkapWerte.Text(f["fuehrt"]) ?? "").Trim().ToLowerInvariant(),
                });
            if (def.Name.Length > 0) k.Datenarten.Add(def);
        }
        return k;
    }
}

public sealed class HkapDatenartDef
{
    public string Name { get; set; } = "";
    public List<HkapFeldDef> Felder { get; set; } = [];

    /// <summary>Feld zu einem Pfad («kicad.flaecheM2»): genau so im Katalog oder als übergeordnetes Objekt («kicad»).</summary>
    public HkapFeldDef? Feld(string pfad)
    {
        var f = Felder.FirstOrDefault(x => x.Name.Equals(pfad, StringComparison.OrdinalIgnoreCase));
        if (f is not null) return f;
        var i = pfad.LastIndexOf('.');
        return i > 0 ? Feld(pfad[..i]) : null;
    }
}

public sealed class HkapFeldDef
{
    public string Name { get; set; } = "";
    public string? Typ { get; set; }
    public string? Einheit { get; set; }
    /// <summary>hkap&gt;kicad | kicad&gt;hkap | beide</summary>
    public string Richtung { get; set; } = "";
    /// <summary>hkap | kicad | zuerst</summary>
    public string Fuehrt { get; set; } = "";
    public bool NachKicad => Richtung is "hkap>kicad" or "beide";
    public bool NachHkap => Richtung is "kicad>hkap" or "beide";
}

/// <summary>Ein Datensatz von HKAP. Version = Prüfsumme des Datensatzes (16 Hex), erwartete Version beim Schreiben.</summary>
public sealed class HkapDatensatz
{
    public string Id { get; set; } = "";
    public string? Version { get; set; }
    public JsonObject Felder { get; set; } = new();

    public static HkapDatensatz AusJson(JsonNode? n)
    {
        var felder = n?["felder"] as JsonObject;
        var s = new HkapDatensatz
        {
            Id = HkapWerte.Text(n?["id"]) ?? HkapWerte.Text(felder?["id"]) ?? "",
            Version = HkapWerte.Text(n?["version"]),
            Felder = felder?.DeepClone() as JsonObject ?? new JsonObject(),
        };
        return s;
    }

    public static List<HkapDatensatz> ListeAusJson(JsonNode? n) =>
        (n?["datensaetze"] as JsonArray ?? (n as JsonArray) ?? new JsonArray()).OfType<JsonObject>().Select(AusJson).Where(s => s.Id.Length > 0).ToList();
}

/// <summary>Ein zu schreibender Datensatz: anlegen (ohne Id), ändern (mit erwarteter Version), löschen.</summary>
public sealed class HkapAenderung
{
    public string? Id { get; set; }
    public string? Version { get; set; }
    public JsonObject Felder { get; set; } = new();
    public bool Loeschen { get; set; }

    public JsonObject AlsJson()
    {
        var o = new JsonObject();
        if (Id is not null) o["id"] = Id;
        if (Version is not null) o["version"] = Version;
        o["felder"] = Felder.DeepClone();
        if (Loeschen) o["loeschen"] = true;
        return o;
    }
}

/// <summary>Antwort von HKAP je geschriebenem Datensatz.</summary>
/// <param name="Status">neu | geaendert | geloescht | konflikt | abgelehnt | unveraendert</param>
public sealed record HkapErgebnis(string Id, string Status, string? Version, HkapDatensatz? Aktuell, string? Grund)
{
    public static List<HkapErgebnis> ListeAusJson(JsonNode? n) =>
        (n?["ergebnisse"] as JsonArray ?? new JsonArray()).OfType<JsonObject>().Select(e =>
        {
            HkapDatensatz? aktuell = null;
            if (e["aktuell"] is JsonObject a)
                aktuell = a.ContainsKey("felder") ? HkapDatensatz.AusJson(a)
                    : new HkapDatensatz { Id = HkapWerte.Text(a["id"]) ?? HkapWerte.Text(e["id"]) ?? "", Felder = (JsonObject)a.DeepClone() };
            return new HkapErgebnis(HkapWerte.Text(e["id"]) ?? "", (HkapWerte.Text(e["status"]) ?? "").Trim().ToLowerInvariant(),
                HkapWerte.Text(e["version"]), aktuell, HkapWerte.Text(e["grund"]));
        }).ToList();
}

/// <summary>Werte vergleichen, flach machen und wieder verschachteln. Felder in Objekten (Raum «kicad») heissen im
/// Abgleich mit Punkt: «kicad.flaecheM2».</summary>
public static class HkapWerte
{
    public static string? Text(JsonNode? n) => n switch
    {
        null => null,
        JsonValue v when v.TryGetValue<string>(out var s) => s,
        JsonValue v => v.ToJsonString(),
        _ => n.ToJsonString(),
    };

    /// <summary>Text oder null, wenn leer.</summary>
    public static string? Leer(JsonNode? n) => Text(n) is { } s && !string.IsNullOrWhiteSpace(s) ? s.Trim() : null;

    public static JsonObject Flach(JsonObject? felder)
    {
        var aus = new JsonObject();
        void Gehe(JsonObject o, string praefix)
        {
            foreach (var (k, v) in o)
            {
                if (v is JsonObject u) Gehe(u, praefix + k + ".");
                else aus[praefix + k] = v?.DeepClone();
            }
        }
        if (felder is not null) Gehe(felder, "");
        return aus;
    }

    public static JsonObject Verschachtelt(IEnumerable<KeyValuePair<string, JsonNode?>> werte)
    {
        var aus = new JsonObject();
        foreach (var (pfad, wert) in werte)
        {
            var teile = pfad.Split('.');
            var o = aus;
            for (int i = 0; i < teile.Length - 1; i++)
            {
                if (o[teile[i]] is not JsonObject u) { u = new JsonObject(); o[teile[i]] = u; }
                o = u;
            }
            o[teile[^1]] = wert?.DeepClone();
        }
        return aus;
    }

    /// <summary>Leerer Text und leere Liste gelten wie «kein Wert».</summary>
    public static JsonNode? Normal(JsonNode? n) => n switch
    {
        JsonValue v when v.TryGetValue<string>(out var s) && string.IsNullOrWhiteSpace(s) => null,
        JsonArray { Count: 0 } => null,
        _ => n,
    };

    static double? Zahl(JsonNode n) =>
        n is JsonValue v && v.GetValueKind() == JsonValueKind.Number ? Json.Zahl(v) : null;

    static double? ZahlAusText(JsonNode n) =>
        n is JsonValue v && v.TryGetValue<string>(out var s)
        && double.TryParse(s.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;

    /// <summary>Gleicher Wert: Zahlen auf 1e-6 genau (auch Zahl gegen Zahlentext), sonst gleicher JSON-Text.</summary>
    public static bool Gleich(JsonNode? a, JsonNode? b)
    {
        a = Normal(a); b = Normal(b);
        if (a is null || b is null) return a is null && b is null;
        var za = Zahl(a); var zb = Zahl(b);
        if (za is not null || zb is not null)
        {
            za ??= ZahlAusText(a); zb ??= ZahlAusText(b);
            if (za is double x && zb is double y) return Math.Abs(x - y) <= 1e-6 * Math.Max(1, Math.Max(Math.Abs(x), Math.Abs(y)));
        }
        return a.ToJsonString() == b.ToJsonString();
    }

    public static JsonNode? Kopie(JsonNode? n) => n?.DeepClone();
}
