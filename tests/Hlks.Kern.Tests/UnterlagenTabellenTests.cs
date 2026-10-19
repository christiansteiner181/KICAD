using System.Text;
using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.GrundlagenHilfe;
using static Hlks.Kern.Tests.UnterlagenHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Tabellen-Leser (Modul 2, B2): CSV, Revit-Typkatalog, XLSX.</summary>
public class UnterlagenTabellenTests
{
    static UnterlagenBefunde Lesen(string pfad, string art) => new TabellenBefundLeser().Lesen(pfad, Path.GetFileName(pfad), "abcdefabcdef1234", art);

    [Fact]
    public void Csv_Trennzeichen_Dezimalkomma_Anfuehrungszeichen_und_Kopf()
    {
        using var o = new Ordner();
        var b = Lesen(Datei(o, "typen.csv", CsvText), "csv");
        var t = Art(b, "tabelle").Single();
        var w = (JsonObject)t.Wert!;
        Assert.Equal(5, w["spalten"]!.GetValue<int>());
        Assert.Equal(3, w["zeilen"]!.GetValue<int>());
        Assert.Equal(1, w["kopfZeilen"]!.GetValue<int>());
        Assert.Equal(["Typ", "DN", "Rp [Zoll]", "L [mm]", "Gewicht [kg]"], ((JsonArray)w["kopf"]!).Select(x => x!.GetValue<string>()).ToArray());
        var gewicht = Zelle(b, t.Id, 1, 5);
        Assert.Equal(0.3, Zahl(gewicht.Wert), 1e-12);          // 0,30 → 0.3
        Assert.Equal("0,30", gewicht.Zusatz!["text"]!.GetValue<string>());   // Schreibweise der Unterlage bleibt erhalten
        Assert.Equal("kg", gewicht.Einheit);
        Assert.Equal("1/2", Zelle(b, t.Id, 1, 3).Text());       // Bruch bleibt Text
        Assert.Equal("R2025;S2", Zelle(b, t.Id, 3, 1).Text());  // Trennzeichen im Anführungszeichen
        Assert.Equal(67, Zahl(Zelle(b, t.Id, 1, 4).Wert));
        Assert.Equal("mm", Zelle(b, t.Id, 1, 4).Einheit);
        Assert.Equal("tabelle 1", b.Leser);
    }

    [Fact]
    public void Csv_Windows1252_wird_erkannt()
    {
        using var o = new Ordner();
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        var b = Lesen(Datei(o, "ansi.csv", "Bezeichnung;Länge [mm]\r\nGehäuse;350\r\n", Encoding.GetEncoding(1252)), "csv");
        var t = Art(b, "tabelle").Single();
        Assert.Equal("Länge [mm]", ((JsonArray)((JsonObject)t.Wert!)["kopf"]!)[1]!.GetValue<string>());
        Assert.Equal("Gehäuse", Zelle(b, t.Id, 1, 1).Text());
        Assert.Contains(b.Hinweise, h => h.Contains("Windows-1252"));
    }

    [Fact]
    public void Typkatalog_je_Zeile_ein_Typ_mit_Werten_und_Einheiten()
    {
        using var o = new Ordner();
        var b = Lesen(Datei(o, "ventil.txt", TypkatalogText), "typkatalog");
        var typen = Art(b, "typ").ToList();
        Assert.Equal(3, typen.Count);
        var v15 = (JsonObject)typen[0].Wert!;
        Assert.Equal("V-15", v15["name"]!.GetValue<string>());
        Assert.Equal(67, Zahl(v15["werte"]!["Laenge"]));
        Assert.Equal(35, Zahl(v15["werte"]!["Breite"]));
        Assert.Equal(15, Zahl(v15["werte"]!["DN"]));
        Assert.Equal("Messing", v15["werte"]!["Material"]!.GetValue<string>());
        Assert.Equal("mm", v15["einheiten"]!["Laenge"]!.GetValue<string>());
        Assert.Null(v15["einheiten"]!["DN"]);
        Assert.Equal("LENGTH", typen[0].Zusatz!["parameterarten"]!["Laenge"]!.GetValue<string>());
        Assert.Equal("Rotguss, vernickelt", ((JsonObject)typen[2].Wert!)["werte"]!["Material"]!.GetValue<string>());
        // dazu die Tabelle mit Zellen
        var t = Art(b, "tabelle").Single();
        Assert.Equal(["Typ", "Laenge [mm]", "Breite [mm]", "DN", "Material"], ((JsonArray)((JsonObject)t.Wert!)["kopf"]!).Select(x => x!.GetValue<string>()).ToArray());
        Assert.Equal(78, Zahl(Zelle(b, t.Id, 2, 2).Wert));
        Assert.Equal(t.Id, typen[0].Zusatz!["tabelle"]!.GetValue<string>());
    }

    [Fact]
    public void Textdatei_ohne_Typkatalog_Kopf_wird_abgelehnt()
    {
        using var o = new Ordner();
        var pfad = Datei(o, "frei.txt", "Das ist ein Brief.\r\nKeine Tabelle.\r\n");
        var ex = Assert.Throws<HlksFehler>(() => Lesen(pfad, "typkatalog"));
        Assert.False(string.IsNullOrWhiteSpace(ex.Vorschlag));
    }

    [Fact]
    public void Xlsx_Blaetter_Zahlen_als_Zahlen_und_zusammengefasste_Zellen()
    {
        using var o = new Ordner();
        var b = Lesen(TestXlsx(o), "xlsx");
        Assert.Equal(2, b.Seiten);
        var t = Art(b, "tabelle").Single(x => x.Layout == "Typen");
        var w = (JsonObject)t.Wert!;
        Assert.Equal(3, w["spalten"]!.GetValue<int>());
        Assert.Equal(4, w["zeilen"]!.GetValue<int>());
        Assert.Equal(1, w["kopfZeilen"]!.GetValue<int>());
        var c = Zelle(b, t.Id, 2, 3);
        Assert.Equal(78.5, Zahl(c.Wert), 1e-12);
        Assert.Equal("mm", c.Einheit);
        Assert.Equal("C3", c.Zusatz!["zelle"]!.GetValue<string>());
        Assert.Equal("R2025-S2", Zelle(b, t.Id, 3, 1).Text());
        // B5:C5 verbunden: Wert nur oben links, mit Hinweis
        var zus = (JsonArray)t.Zusatz!["zusammengefasst"]!;
        Assert.Equal("B5", zus[0]!["von"]!.GetValue<string>());
        Assert.Equal("C5", zus[0]!["bis"]!.GetValue<string>());
        Assert.Contains(b.Hinweise, h => h.Contains("zusammengefasste") && h.Contains("Typen"));
        Assert.Contains(b.Hinweise, h => h.Contains("Leer") && h.Contains("leer"));
    }

    [Fact]
    public void Xlsx_beschaedigt_gibt_Fehler_mit_Vorschlag()
    {
        using var o = new Ordner();
        var pfad = Datei(o, "kaputt.xlsx", "PK nein");
        var ex = Assert.Throws<HlksFehler>(() => Lesen(pfad, "xlsx"));
        Assert.False(string.IsNullOrWhiteSpace(ex.Vorschlag));
    }
}
