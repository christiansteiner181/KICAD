// Modul 5 A-S 2 (zweiter Anlauf): Katalog der Schemasymbole aus Christians HKAP-Symbolbibliothek.
//
// Die wirksame Bibliothek entsteht genau wie in HKAP (SchemaSymbole.Alle): eingebaute Symbole aus dem Code, durch gleichnamige Einträge
// der Datei data/schema_symbole.json ersetzt («angepasst»), plus die eigenen Symbole der Datei («eigen»). Ausgeblendete Symbole
// (versteckt) bleiben mit dem Kennzeichen im Katalog, damit Schemas, die sie verwenden, ihre Bauteile behalten; die Oberfläche
// bietet sie nicht an. HKAP-Code und -Daten werden nur gelesen, nie verändert; nie die Live-Daten, nie Port 5013.
//
// Aufruf: hkap_symbole_uebernehmen [--daten <schema_symbole.json>] [--ausgabe <schemasymbole.json>] [--zuordnung <zuordnung.json>]
// Der Katalog wird nie von Hand bearbeitet; die Prüfsumme im Kopf (inhaltspruefsumme) lässt den Kern jede Handänderung erkennen.
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using AufgabenProtokolle.Web.Services;

string? Arg(string name) { var i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
string Gesucht(string unter) // aufwärts vom aktuellen Ordner suchen
{
    for (var d = new DirectoryInfo(Directory.GetCurrentDirectory()); d != null; d = d.Parent)
    {
        var p = Path.Combine(d.FullName, unter);
        if (File.Exists(p)) return p;
    }
    throw new FileNotFoundException(unter);
}

var temp = Path.GetTempPath();
var daten = Path.GetFullPath(Arg("--daten") ?? Path.Combine(temp, "hkap_testkopie", "data", "schema_symbole.json"));
var zuordnungPfad = Path.GetFullPath(Arg("--zuordnung") ?? Gesucht(Path.Combine("werkzeuge", "hkap_symbole_uebernehmen", "zuordnung.json")));
var ausgabe = Path.GetFullPath(Arg("--ausgabe") ?? Path.Combine(Directory.GetParent(Directory.GetParent(Path.GetDirectoryName(zuordnungPfad)!)!.FullName)!.FullName, "kataloge", "schemasymbole.json"));

// Schutz: nie Live-Daten von HKAP, nie W: oder das NAS
string Norm(string p) => p.Replace('/', '\\').ToLowerInvariant();
if (Norm(daten).Contains("aufgabenprotokolle.web\\data") || Norm(daten).StartsWith("w:") || Norm(daten).StartsWith("\\\\hk-nas"))
{ Console.Error.WriteLine("Abbruch: Die Live-Daten von HKAP bzw. W:/NAS werden nicht gelesen. Bitte die Testkopie angeben."); return 2; }
if (Norm(ausgabe).StartsWith("w:") || Norm(ausgabe).StartsWith("\\\\hk-nas")) { Console.Error.WriteLine("Abbruch: Schreiben auf W:/NAS ist nicht erlaubt."); return 2; }
if (!File.Exists(daten)) { Console.Error.WriteLine($"Datendatei nicht gefunden: {daten}"); return 2; }

var quelleCode = Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>().First(a => a.Key == "HkapQuelle").Value!;
var datenBytes = File.ReadAllBytes(daten);
var codeBytes = File.Exists(quelleCode) ? File.ReadAllBytes(quelleCode) : [];

// wirksame Bibliothek wie HKAP
var eigeneDatei = JsonSerializer.Deserialize<List<SchemaSymbol>>(Encoding.UTF8.GetString(datenBytes)) ?? [];
SchemaSymbole.SetzeEigene(eigeneDatei);
var eingebauteKeys = SchemaSymbole.Eingebaut.Select(s => s.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
var dateiKeys = eigeneDatei.Select(s => s.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
var alle = SchemaSymbole.Alle;
var codeGeo = SchemaSymbole.Eingebaut.ToDictionary(c => c.Key, c => JsonSerializer.Serialize(new object[] { c.Geo, c.Anschluesse }), StringComparer.OrdinalIgnoreCase);
// angepasste Symbole, deren Geometrie und Anschluesse noch die des HKAP-Codes sind (z. B. nur ausgeblendet oder umbenannt)
bool GeoWieCode(SchemaSymbol s) => codeGeo.TryGetValue(s.Key, out var g) && g == JsonSerializer.Serialize(new object[] { s.Geo, s.Anschluesse });

string Herkunft(SchemaSymbol s) =>
    !eingebauteKeys.Contains(s.Key) ? "HKAP data/schema_symbole.json (eigen)"
    : dateiKeys.Contains(s.Key) ? "HKAP data/schema_symbole.json (angepasst)"
    : "HKAP-Code";

// Zuordnungsquelle
var zq = JsonNode.Parse(File.ReadAllText(zuordnungPfad, Encoding.UTF8))!.AsObject();
double massstab = zq["massstab"]!.GetValue<double>();
var einbauQuelle = zq["einbau"]!.AsObject();
var einbauMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
foreach (var art in new[] { "senkrecht", "waagrecht", "fest" })
    foreach (var k in einbauQuelle[art]!.AsArray()) einbauMap[k!.GetValue<string>()] = art;

// Einbaulage: Armaturen, Regelarmaturen, Pumpen mit Anschlüssen N/S (senkrecht) bzw. W/E (waagrecht) auf der Achse
string Einbau(SchemaSymbol s)
{
    if (einbauMap.TryGetValue(s.Key, out var e)) return e;
    if (s.Gruppe is not ("Armaturen" or "Regelarmaturen" or "Pumpen")) return "fest";
    bool Hat(string dir, Func<SchemaAnschluss, bool> achse) => s.Anschluesse.Any(a => a.Dir == dir && achse(a));
    bool ns = Hat("N", a => Math.Abs(a.X) < 1e-9) && Hat("S", a => Math.Abs(a.X) < 1e-9);
    bool we = Hat("W", a => Math.Abs(a.Y) < 1e-9) && Hat("E", a => Math.Abs(a.Y) < 1e-9);
    return ns ? "senkrecht" : we ? "waagrecht" : "fest";
}

JsonNode Geo(SchemaGeo g)
{
    var o = new JsonObject { ["t"] = g.T, ["p"] = new JsonArray(g.P.Select(v => (JsonNode?)JsonValue.Create(v)).ToArray()) };
    if (g.Fill) o["fill"] = true;
    if (!string.IsNullOrWhiteSpace(g.Fc)) o["fc"] = g.Fc;
    if (g.Close) o["close"] = true;
    if (!string.IsNullOrEmpty(g.Align)) o["align"] = g.Align;
    if (g.Aufrecht) o["aufrecht"] = true;
    if (g.W != 0) o["w"] = g.W;
    if (g.Dash) o["dash"] = true;
    if (g.T == "text") { o["s"] = g.S; o["size"] = g.Size; if (g.Bold) o["bold"] = true; }
    return o;
}

var symbole = new JsonArray();
foreach (var s in alle)
{
    var o = new JsonObject
    {
        ["key"] = s.Key, ["gruppe"] = s.Gruppe, ["sparte"] = s.Sparte, ["bereich"] = s.Bereich, ["label"] = s.Label,
        ["herkunft"] = Herkunft(s), ["geometrieWieCode"] = codeGeo.ContainsKey(s.Key) ? GeoWieCode(s) : null, ["versteckt"] = s.Versteckt, ["einbau"] = Einbau(s),
        ["w"] = s.W, ["h"] = s.H, ["bx0"] = s.Bx0, ["by0"] = s.By0, ["bx1"] = s.Bx1, ["by1"] = s.By1,
        ["labelPos"] = s.LabelPos, ["suchbegriffe"] = s.Suchbegriffe, ["standardFarbe"] = s.StandardFarbe,
        ["kennbuchstabe"] = s.Kennbuchstabe, ["rolle"] = s.Rolle,
        ["geo"] = new JsonArray(s.Geo.Select(Geo).ToArray()),
        ["anschluesse"] = new JsonArray(s.Anschluesse.Select(a => (JsonNode?)new JsonObject { ["id"] = a.Id, ["x"] = a.X, ["y"] = a.Y, ["dir"] = a.Dir }).ToArray()),
        ["textFelder"] = new JsonArray(s.TextFelder.Select(t => (JsonNode?)new JsonObject { ["key"] = t.Key, ["label"] = t.Label }).ToArray()),
    };
    symbole.Add(o);
}

// Zuordnung prüfen und übernehmen
var sichtbar = alle.Where(s => !s.Versteckt).Select(s => s.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
var zeilen = new JsonArray();
var fehler = new List<string>();
foreach (var z in zq["zeilen"]!.AsArray())
{
    var key = z!["symbol"]?.GetValue<string>();
    if (key is not null && !sichtbar.Contains(key)) fehler.Add($"Zuordnung {z["klasse"]}/{z["art"]}: Symbol «{key}» fehlt oder ist ausgeblendet");
    var o = new JsonObject { ["klasse"] = z["klasse"]!.GetValue<string>(), ["art"] = z["art"]?.GetValue<string>(), ["kaelte"] = z["kaelte"]?.GetValue<bool>(),
        ["gewerk"] = z["gewerk"]?.GetValue<string>(), ["symbol"] = key, ["quelle"] = z["quelle"]?.GetValue<string>() ?? "", ["annahme"] = z["annahme"]?.GetValue<string>() ?? "" };
    zeilen.Add(o);
}
if (fehler.Count > 0) { foreach (var f in fehler) Console.Error.WriteLine(f); return 3; }

var darstellung = new JsonObject { ["massstab"] = massstab, ["massstabAnnahme"] = zq["massstabAnnahme"]!.GetValue<string>() };

// Prüfsumme: SHA-256 über die kompakte JSON-Form von {darstellung, zuordnung, symbole} (der Kern rechnet dieselbe Form nach)
var opt = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
var kompakt = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
string Pruefsumme(JsonNode d, JsonNode z, JsonNode s)
{
    // über die Textform neu eingelesen, damit Zahlen und Zeichen genau so lauten wie in der Datei
    var obj = new JsonObject { ["darstellung"] = JsonNode.Parse(d.ToJsonString(kompakt)), ["zuordnung"] = JsonNode.Parse(z.ToJsonString(kompakt)), ["symbole"] = JsonNode.Parse(s.ToJsonString(kompakt)) };
    return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(obj.ToJsonString(kompakt)))).ToLowerInvariant();
}
var summe = Pruefsumme(darstellung, zeilen, symbole);

int Anz(Func<SchemaSymbol, bool> f) => alle.Count(f);
var kopf = new JsonObject
{
    ["hinweis"] = "Erzeugt mit werkzeuge/hkap_symbole_uebernehmen aus Christians HKAP-Symbolbibliothek. NICHT von Hand aendern: Aenderungen in HKAP vornehmen und das Werkzeug neu laufen lassen. Der Kern lehnt einen Katalog ab, dessen Pruefsumme nicht stimmt.",
    ["erzeugt"] = DateTime.Now.ToString("yyyy-MM-dd"),
    ["quelleDaten"] = new JsonObject
    {
        ["datei"] = "data/schema_symbole.json (HKAP, Testkopie)", ["sha256"] = Convert.ToHexString(SHA256.HashData(datenBytes)).ToLowerInvariant(),
        ["bytes"] = datenBytes.Length, ["geaendert"] = File.GetLastWriteTime(daten).ToString("yyyy-MM-dd"), ["eintraege"] = eigeneDatei.Count,
    },
    ["quelleCode"] = new JsonObject
    {
        ["datei"] = "AufgabenProtokolle.Web/Services/SchemaSymbole.cs", ["sha256"] = Convert.ToHexString(SHA256.HashData(codeBytes)).ToLowerInvariant(),
        ["bytes"] = codeBytes.Length, ["geaendert"] = File.Exists(quelleCode) ? File.GetLastWriteTime(quelleCode).ToString("yyyy-MM-dd") : "",
    },
    ["anzahl"] = new JsonObject
    {
        ["symbole"] = alle.Count, ["sichtbar"] = Anz(s => !s.Versteckt), ["ausgeblendet"] = Anz(s => s.Versteckt),
        ["hkapCode"] = Anz(s => Herkunft(s) == "HKAP-Code"), ["angepasst"] = Anz(s => Herkunft(s).EndsWith("(angepasst)")), ["eigen"] = Anz(s => Herkunft(s).EndsWith("(eigen)")),
        ["angepasstMitAndererGeometrie"] = Anz(s => codeGeo.ContainsKey(s.Key) && !GeoWieCode(s)),
    },
    ["inhaltspruefsumme"] = summe,
};
var wurzel = new JsonObject { ["kopf"] = kopf, ["darstellung"] = darstellung, ["zuordnung"] = zeilen, ["symbole"] = symbole };
Directory.CreateDirectory(Path.GetDirectoryName(ausgabe)!);
File.WriteAllText(ausgabe, wurzel.ToJsonString(opt), new UTF8Encoding(false));

// Gegenprobe: die geschriebene Datei neu einlesen und die Prüfsumme nachrechnen
var gelesen = JsonNode.Parse(File.ReadAllText(ausgabe, Encoding.UTF8))!;
var nach = Pruefsumme(gelesen["darstellung"]!, gelesen["zuordnung"]!, gelesen["symbole"]!);
if (nach != summe) { Console.Error.WriteLine("Pruefsumme der geschriebenen Datei weicht ab."); return 4; }

Console.WriteLine($"{alle.Count} Symbole ({Anz(s => !s.Versteckt)} sichtbar, {Anz(s => s.Versteckt)} ausgeblendet; Code {Anz(s => Herkunft(s) == "HKAP-Code")}, angepasst {Anz(s => Herkunft(s).EndsWith("(angepasst)"))}, eigen {Anz(s => Herkunft(s).EndsWith("(eigen)"))}) -> {ausgabe}");
Console.WriteLine($"Daten: {daten} sha256 {Convert.ToHexString(SHA256.HashData(datenBytes)).ToLowerInvariant()[..12]}…  Pruefsumme {summe[..12]}…");
return 0;
