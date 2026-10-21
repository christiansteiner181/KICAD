// Datenausgabe für die Prüfseite der Schemasymbole: Primitive der KICAD-Darstellung (SchemaSymbolZeichnung) je Symbol und je Zuordnungszeile.
// Aufruf: schemasymbole_uebersicht [<kataloge-Ordner> [<daten.json>]]
using System.Text.Json;
using Hlks.Kern;

var kataloge = args.Length > 0 ? args[0] : Katalog.StandardOrdner();
var ausgabe = args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "HLKS_CAD", "symbole_hk", "daten.json");
Directory.CreateDirectory(Path.GetDirectoryName(ausgabe)!);
var kat = Katalog.Laden(kataloge);
var sk = kat.Schemasymbole ?? throw new InvalidOperationException("kataloge/schemasymbole.json fehlt");
double m = sk.Massstab;

object Teil(KatalogSymbol s, bool gedreht) => new
{
    primitive = SchemaSymbolZeichnung.Primitive(s, m, gedreht),
    anschluesse = SchemaSymbolZeichnung.Anschluesse(s, m, gedreht),
};

var symbole = sk.Symbole.Select(s => new
{
    s.Key, s.Gruppe, s.Sparte, s.Label, s.Herkunft, s.Versteckt, s.Einbau, s.GeometrieWieCode,
    aufrecht = Teil(s, false),
    gedreht = s.Einbau == "fest" ? null : Teil(s, true),
}).ToList();

var zeilen = sk.Zuordnung.Select(z => new
{
    z.Klasse, z.Art, z.Kaelte, z.Gewerk, z.Symbol, z.Quelle, z.Annahme,
    label = z.Symbol is null ? null : sk.Symbol(z.Symbol)?.Label,
    einbau = z.Symbol is null ? null : sk.Symbol(z.Symbol)?.Einbau,
    teil = z.Symbol is null ? null : Teil(sk.Symbol(z.Symbol)!, false),
}).ToList();

File.WriteAllText(ausgabe, JsonSerializer.Serialize(new { kopf = sk.Kopf, massstab = m, symbole, zuordnung = zeilen }, Json.Optionen));
Console.WriteLine($"{symbole.Count} Symbole, {zeilen.Count} Zeilen -> {ausgabe}");
