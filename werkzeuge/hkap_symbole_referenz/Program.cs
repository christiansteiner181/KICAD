// Referenz der Heizungs- und Kältesymbole aus dem HKAP-Quelltext (SchemaSymbole.cs, nur gelesen, einkompiliert).
// Ausgabe: JSON mit der Geometrie in HKAP-Millimetern (y nach unten, Ursprung = Symbolmitte), unverändert aus SchemaSymbole.Eingebaut.
// Die Tests (SchemaSymboleHeizungTests) rechnen sie unabhängig vom Kern ins Zeichenmodell um und vergleichen.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AufgabenProtokolle.Web.Services;

if (args.Length < 1) { Console.Error.WriteLine("Aufruf: hkap_symbole_referenz <ausgabe.json>"); return 1; }

// Heizung und Kälte: die Gruppen der HKAP-Palette ohne Sanitär (Sparte «sanitaer») und ohne die Zapfstelle (Sanitär-Piktogramm);
// «Diverses» (Knoten, Verweis, Kompensator, Kamin, Gaszähler) gehört nicht zum Schema Heizung/Kälte.
var gruppen = new[] { "Wärmeerzeugung", "Speicher", "Verteilung", "Wärmeübertrager", "Pumpen", "Armaturen", "Regelarmaturen", "Verbraucher", "Messen / Regeln" };
var ohne = new[] { "zapfstelle" };
var symbole = SchemaSymbole.Eingebaut
    .Where(s => gruppen.Contains(s.Gruppe) && s.Sparte != "sanitaer" && !ohne.Contains(s.Key))
    .Select(s => new
    {
        key = s.Key, gruppe = s.Gruppe, label = s.Label, w = s.W, h = s.H, labelPos = s.LabelPos, standardFarbe = s.StandardFarbe, geo = s.Geo, anschluesse = s.Anschluesse,
    }).ToList();

// Quelldatei für die Herkunftsangabe (liegt neben dem einkompilierten Pfad)
var quelle = Environment.GetEnvironmentVariable("HKAP_QUELLE") ?? Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive", "Claude_Code", "AufgabenProtokolle.Web", "Services", "SchemaSymbole.cs");
var bytes = File.Exists(quelle) ? File.ReadAllBytes(quelle) : [];
var kopf = new
{
    quelle = "AufgabenProtokolle.Web/Services/SchemaSymbole.cs (SchemaSymbole.Eingebaut)",
    sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
    zeilen = bytes.Length == 0 ? 0 : Encoding.UTF8.GetString(bytes).Split('\n').Length,
    erzeugt = DateTime.Now.ToString("yyyy-MM-dd"),
    anzahl = symbole.Count,
    symbole,
};
var opt = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
File.WriteAllText(args[0], JsonSerializer.Serialize(kopf, opt), new UTF8Encoding(false));
Console.WriteLine($"{symbole.Count} Symbole -> {args[0]}");
return 0;
