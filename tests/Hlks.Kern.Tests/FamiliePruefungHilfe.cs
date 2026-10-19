using System.Text.Json.Nodes;
using ACadSharp.Entities;
using ACadSharp.Types.Units;
using CSMath;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.GrundlagenHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Hilfen der Tests zu B4 (Quellenprüfung, Konflikte, Abdeckung, Massvergleich, Kontrollbild, Freigabe). Alle Unterlagen
/// werden im Test erzeugt und haben bekannte, FIKTIVE Werte (keine Herstellerdaten):
///   PDF (UnterlagenHilfe.TestPdf): Seite 1 Tabelle Typ | DN [mm] | L [mm] | H [mm] mit V-15 | 15 | 67 | 35, V-20 | 20 | 78 | 46,
///       V-25 | 25 | 87 | 46 und eine Masslinie «L»;
///   DXF «widerspruch.dxf»: Rechteck 70 × 35 und eine Bemassung von genau 70 mm (absichtlicher Widerspruch zu L = 67);
///   DXF «ansicht.dxf»: Ansicht vorne von V-15 1:1 in mm (Rechteck 67 × 35), Bemassung 67 mm auf Layer MASSE, wahlweise eine
///       falsche Kante bei x = 72;
///   CSV: Typ;L [m];Kvs [m³/h] mit 0,067 / 6,3 und 0,078 / 8,5 (Einheit m, Dezimalkomma);
///   PNG: ein leeres Bild (Befund bild).</summary>
static class FamiliePruefungHilfe
{
    /// <summary>Projekt ohne Ablage; Akteur Christian (mensch) bzw. KI (ki).</summary>
    public static Projekt ProjektB4(string akteurArt = "mensch") =>
        Projekt.Anlegen(Kat, null, "Test B4", akteurArt == "ki" ? "KI" : "Christian", "pc", akteurArt);

    /// <summary>Unterlage mit zweck «familie» einfügen und ihre Befunde lesen.</summary>
    public static UnterlagenBefunde Ablegen(Projekt p, string pfad)
    {
        Ok(p.Ausfuehren("unterlage_einfuegen", new JsonObject { ["datei"] = pfad, ["zweck"] = "familie" }));
        var u = UnterlagenService.Aufloesen(p, Path.GetFileName(pfad));
        return UnterlagenService.Lesen(p, u).Befunde;
    }

    /// <summary>Id der Zelle (Zeile, Spalte ab 1) der Tabelle auf Seite 1 des Test-PDF.</summary>
    public static string Zelle(UnterlagenBefunde pdf, int zeile, int spalte)
    {
        var t = pdf.Befunde.Single(x => x.Art == "tabelle" && x.Seite == 1);
        return UnterlagenHilfe.Zelle(pdf, t.Id, zeile, spalte).Id;
    }

    public static string Mass(UnterlagenBefunde b, double wert) => b.Befunde.First(x => x.Art == "mass" && Json.Zahl(x.Wert) is double d && Math.Abs(d - wert) < 1e-6).Id;

    public static string DxfWiderspruch(Ordner o) => Dxf(o, "widerspruch.dxf", UnitsType.Millimeters, doc =>
    {
        doc.Entities.Add(Linie(doc, 0, 0, 70, 0, "GEHAEUSE")); doc.Entities.Add(Linie(doc, 70, 0, 70, 35, "GEHAEUSE"));
        doc.Entities.Add(Linie(doc, 70, 35, 0, 35, "GEHAEUSE")); doc.Entities.Add(Linie(doc, 0, 35, 0, 0, "GEHAEUSE"));
        doc.Entities.Add(new DimensionLinear { FirstPoint = new XYZ(0, 0, 0), SecondPoint = new XYZ(70, 0, 0), DefinitionPoint = new XYZ(70, -12, 0), Rotation = 0, Layer = LayerHolen(doc, "MASSE") });
    });

    public static string DxfAnsicht(Ordner o, bool fehlkante, string name = "ansicht.dxf") => Dxf(o, name, UnitsType.Millimeters, doc =>
    {
        doc.Entities.Add(Linie(doc, 0, 0, 67, 0, "GEHAEUSE")); doc.Entities.Add(Linie(doc, 67, 0, 67, 35, "GEHAEUSE"));
        doc.Entities.Add(Linie(doc, 67, 35, 0, 35, "GEHAEUSE")); doc.Entities.Add(Linie(doc, 0, 35, 0, 0, "GEHAEUSE"));
        if (fehlkante) doc.Entities.Add(Linie(doc, 72, 0, 72, 35, "GEHAEUSE"));
        doc.Entities.Add(new DimensionLinear { FirstPoint = new XYZ(0, 0, 0), SecondPoint = new XYZ(67, 0, 0), DefinitionPoint = new XYZ(67, -12, 0), Rotation = 0, Layer = LayerHolen(doc, "MASSE") });
    });

    public static string Csv(Ordner o) => UnterlagenHilfe.Datei(o, "kennwerte.csv", "Typ;L [m];Kvs [m³/h]\r\nV-15;0,067;6,3\r\nV-20;0,078;8,5\r\n");

    public static string Bild(Ordner o) => Png(o, "massbild.png", 120, 80, (255, 255, 255));

    public static string Id(UnterlagenBefunde b, string art) => b.Befunde.First(x => x.Art == art).Id;

    /// <summary>CSV-Zelle (Zeile, Spalte ab 1).</summary>
    public static string CsvZelle(UnterlagenBefunde csv, int zeile, int spalte)
    {
        var t = csv.Befunde.Single(x => x.Art == "tabelle");
        return UnterlagenHilfe.Zelle(csv, t.Id, zeile, spalte).Id;
    }

    static JsonArray A(params string[] ids) => new(ids.Select(i => (JsonNode)JsonValue.Create(i)).ToArray());

    /// <summary>Testventil (FIKTIV, Kategorie Ventil): dn, L, H je Typ aus der PDF-Tabelle (Zeile 1–3), B = 30 ohne Quelle (Vorgabe),
    /// Körper Quader L × B × H, Anschlüsse ein/aus auf halber Höhe, Massbezüge L (von/bis entlang x, Quelle: Spalte L) und H (Parameter).</summary>
    public static JsonObject Ventil(UnterlagenBefunde pdf, string name = "Testventil B4 (fiktiv)")
    {
        JsonObject Typ(string n, int zeile, double dn, double l, double h) => new()
        {
            ["name"] = n,
            ["werte"] = new JsonObject { ["dn"] = dn, ["L"] = l, ["H"] = h },
            ["quellen"] = new JsonObject { ["dn"] = A(Zelle(pdf, zeile, 2)), ["L"] = A(Zelle(pdf, zeile, 3)), ["H"] = A(Zelle(pdf, zeile, 4)) },
        };
        return new JsonObject
        {
            ["format"] = 1, ["name"] = name, ["kategorie"] = "Ventil", ["hersteller"] = "keiner – Testfamilie",
            ["parameter"] = new JsonArray(
                new JsonObject { ["name"] = "dn", ["art"] = "typ", ["datentyp"] = "dn", ["gruppe"] = "anschluss" },
                new JsonObject { ["name"] = "L", ["art"] = "typ", ["datentyp"] = "laenge", ["gruppe"] = "abmessung" },
                new JsonObject { ["name"] = "H", ["art"] = "typ", ["datentyp"] = "laenge", ["gruppe"] = "abmessung" },
                new JsonObject { ["name"] = "B", ["art"] = "typ", ["datentyp"] = "laenge", ["gruppe"] = "abmessung", ["wert"] = 30 }),
            ["typen"] = new JsonArray(Typ("V-15", 1, 15, 67, 35), Typ("V-20", 2, 20, 78, 46), Typ("V-25", 3, 25, 87, 46)),
            ["koerper"] = new JsonArray(new JsonObject { ["id"] = "gehaeuse", ["art"] = "quader", ["material"] = "armatur", ["von"] = new JsonArray(0, "-B / 2", 0), ["bis"] = new JsonArray("L", "B / 2", "H") }),
            ["anschluesse"] = new JsonArray(
                new JsonObject { ["name"] = "ein", ["medium"] = "*", ["fluss"] = "rein", ["art"] = "rohr", ["form"] = "rund", ["dn"] = "dn", ["lage"] = new JsonArray(0, 0, "H / 2"), ["richtung"] = new JsonArray(-1, 0, 0) },
                new JsonObject { ["name"] = "aus", ["medium"] = "*", ["fluss"] = "raus", ["art"] = "rohr", ["form"] = "rund", ["dn"] = "dn", ["lage"] = new JsonArray("L", 0, "H / 2"), ["richtung"] = new JsonArray(1, 0, 0) }),
            ["masse"] = new JsonArray(
                new JsonObject { ["name"] = "L", ["ansicht"] = "vorne", ["von"] = new JsonArray(0, 0, 0), ["bis"] = new JsonArray("L", 0, 0), ["richtung"] = "x", ["quellen"] = A(Zelle(pdf, 1, 3)) },
                new JsonObject { ["name"] = "H", ["ansicht"] = "vorne", ["parameter"] = "H", ["quellen"] = A(Zelle(pdf, 1, 4)) }),
        };
    }

    /// <summary>Typwert und Quellen eines Typs setzen (Quellen leer = entfernen).</summary>
    public static void TypWert(JsonObject def, string typ, string parameter, JsonNode? wert, params string[] quellen)
    {
        var t = (JsonObject)((JsonArray)def["typen"]!).Single(x => x!["name"]!.GetValue<string>() == typ)!;
        if (wert is null) ((JsonObject)t["werte"]!).Remove(parameter); else t["werte"]![parameter] = wert;
        if (t["quellen"] is not JsonObject q) t["quellen"] = q = [];
        if (quellen.Length == 0) q.Remove(parameter); else q[parameter] = A(quellen);
    }

    public static JsonObject Definition(Projekt p, string familie = "FAM-1") => (JsonObject)Hole(p, familie).Eigenschaft("definition")!.Inhalt!;

    public static JsonObject Abdeckung(Projekt p, string schluessel, string familie = "FAM-1") => (JsonObject)Definition(p, familie)["abdeckung"]![schluessel]!;

    public static Kontext Als(Projekt p, string art) =>
        new(p, p.Modell.Kopie(), new Herkunft { Art = art, Akteur = art == "ki" ? "KI" : "Christian", Zeit = DateTimeOffset.Now });

    public static JsonObject Abfrage(Projekt p, string name, JsonObject par) => (JsonObject)Tuer.Abfrage(p, name, par);
}
