using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.SanitaerHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 4 Teil A-K, Punkt 1 und 2: Katalog sanitaer_belastungswerte.json laden (nur lesen, nichts ergänzen), Auswahl der Eigenschaft
/// sanitaerArt, Vorgabe je Klasse. Erwartete Werte werden aus der JSON-Datei selbst gelesen (Werte nur von dort).</summary>
public class SanitaerKatalogTests
{
    static JsonObject Roh() => (JsonObject)JsonNode.Parse(File.ReadAllText(Path.Combine(Katalog.StandardOrdner(), SanitaerKatalog.Datei)))!;

    static double? N(JsonNode? n) => n is null ? null : n.GetValue<double>();

    [Fact]
    public void Katalog_ArtenWieInDerDatei_ReihenfolgeUndExcelZeilen()
    {
        var roh = (JsonArray)Roh()["arten"]!;
        Assert.Equal(34, roh.Count);
        Assert.Equal(roh.Count, SKat.Arten.Count);
        for (int i = 0; i < roh.Count; i++)
        {
            var r = roh[i]!; var a = SKat.Arten[i];
            Assert.Equal(r["key"]!.GetValue<string>(), a.Schluessel);
            Assert.Equal(r["name"]!.GetValue<string>(), a.Name);
            Assert.Equal(N(r["luKw"]), a.LuKw);
            Assert.Equal(N(r["luWw"]), a.LuWw);
            Assert.Equal(N(r["du"]), a.Du);
            Assert.Equal(r["zeile"]!.GetValue<int>(), a.Zeile);
            Assert.Equal(i, a.Reihenfolge);
        }
        // Excel Tabelle1 A7:D40: Zeilen 7 bis 40 lückenlos in Katalogreihenfolge
        Assert.Equal(Enumerable.Range(7, 34), SKat.Arten.Select(a => a.Zeile));
        Assert.Equal("Tabelle1", SKat.Blatt);
        Assert.Equal("LU_DU Zusammenstellung_.xlsx", SKat.Dokument);
    }

    [Fact]
    public void Katalog_FormelnAusDerDatei_FaktorAusDerZellformel()
    {
        var r = (JsonObject)Roh()["rechnung"]!;
        Assert.Equal(0.1, SKat.VolumenstromFaktor);   // aus «B56 = B55*0.1»
        Assert.Contains("B55*0.1", SKat.VolumenstromText);
        Assert.Equal(N(r["spitzeKurve3"]!["faktor"]), SKat.Kurve3.Faktor);
        Assert.Equal(N(r["spitzeKurve3"]!["exponent"]), SKat.Kurve3.Exponent);
        Assert.Equal(N(r["spitzeKurve5"]!["faktor"]), SKat.Kurve5.Faktor);
        Assert.Equal(N(r["spitzeKurve5"]!["exponent"]), SKat.Kurve5.Exponent);
        Assert.Equal("C57 = B56^0.353*0.459", SKat.Kurve3.Zelle);
        Assert.Equal("I57 = B56^0.257*0.598", SKat.Kurve5.Zelle);
        Assert.Equal(3, SKat.Kurve3.Nummer);
        Assert.Equal(5, SKat.Kurve5.Nummer);
        Assert.Equal(0.5, SKat.AbflusskennzahlBeispiel);
        Assert.Equal("M65", SKat.AbflusskennzahlBeispielZelle);
        Assert.Contains("M66", SKat.SchmutzwasserZelle);
    }

    [Fact]
    public void Quelle_NenntKatalogDokumentUndExcelZeile()
    {
        var q = SKat.Quelle(31);
        Assert.Contains("sanitaer_belastungswerte.json", q);
        Assert.Contains("LU_DU Zusammenstellung_.xlsx", q);
        Assert.Contains("Tabelle1 Zeile 31", q);
        Assert.Contains("14.03.2024", q);
    }

    [Fact]
    public void Auswahl_SanitaerArt_SindDieSchluesselMitNamen_FuerAlleApparateklassen()
    {
        foreach (var klasse in new[] { "WC", "Waschtisch", "Dusche", "Badewanne", "Spueltisch", "Waschmaschine", "Ausgussbecken", "Bodenablauf" })
        {
            var d = Kat.Klasse(klasse).Eigenschaften.Single(e => e.Name == "sanitaerArt");
            Assert.Equal("auswahl", d.Typ);
            Assert.Equal(SKat.Arten.Select(a => a.Schluessel), d.Auswahl!);
            Assert.Equal("Klosettanlage bis 7.5 Liter Spülwassermenge", d.AuswahlNamen!["wc_75"]);
        }
        Assert.DoesNotContain(Kat.Klasse("Heizkoerper").Eigenschaften, e => e.Name == "sanitaerArt");
        // Die Abfrage katalog liefert Auswahl und Namen für die Oberfläche
        var p = Neu();
        var wc = (JsonObject)Tuer.Abfrage(p, "katalog", new JsonObject { ["klasse"] = "WC" });
        var def = wc["eigenschaften"]!.AsArray().Single(e => e!["name"]!.GetValue<string>() == "sanitaerArt")!;
        Assert.Equal(34, def["auswahl"]!.AsArray().Count);
        Assert.Equal("Bodenablauf DN 50", def["auswahlNamen"]!["bodenablauf_50"]!.GetValue<string>());
    }

    [Fact]
    public void VorgabeJeKlasse_WieSpezifikation_BodenablaufOhne()
    {
        var erwartet = new Dictionary<string, string>
        {
            ["WC"] = "wc_75", ["Waschtisch"] = "waschtisch", ["Dusche"] = "dusche", ["Badewanne"] = "badewanne", ["Spueltisch"] = "spuelbecken",
            ["Waschmaschine"] = "waschmaschine_6", ["Ausgussbecken"] = "waschtrog",
        };
        Assert.Equal(erwartet.OrderBy(x => x.Key), SKat.VorgabeJeKlasse.OrderBy(x => x.Key));
        Assert.Null(SKat.Vorgabe(Kat, "Bodenablauf", out _));
        var p = Neu();
        var k = Abfrage(p, "sanitaer_katalog");
        Assert.Equal("wc_75", k["vorgabeJeKlasse"]!["WC"]!.GetValue<string>());
        Assert.Contains("Bodenablauf", k["ohneVorgabe"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Equal(34, k["arten"]!.AsArray().Count);
        Assert.Equal(40, k["arten"]![33]!["zeile"]!.GetValue<int>());
        Assert.Contains("M67", k["formeln"]!["gesamtabfluss"]!.GetValue<string>());
    }

    [Fact]
    public void Katalog_Fehlerhaft_WirdAbgelehnt()
    {
        var doppelt = Roh(); ((JsonArray)doppelt["arten"]!)[1]!["key"] = "urinal_wasserlos";
        Assert.Contains("doppelt", Assert.Throws<HlksFehler>(() => SanitaerKatalog.Lesen(doppelt)).Message);
        var vorgabe = Roh(); vorgabe["vorgabeJeKlasse"]!["WC"] = "wc_99";
        Assert.Contains("wc_99", Assert.Throws<HlksFehler>(() => SanitaerKatalog.Lesen(vorgabe)).Message);
        var faktor = Roh(); faktor["rechnung"]!["volumenstrom"] = "Total LU mal etwas";
        Assert.Contains("Faktor", Assert.Throws<HlksFehler>(() => SanitaerKatalog.Lesen(faktor)).Message);
        var zeile = Roh(); ((JsonArray)zeile["arten"]!)[0]!["zeile"] = 0;
        Assert.Contains("Excel-Zeile", Assert.Throws<HlksFehler>(() => SanitaerKatalog.Lesen(zeile)).Message);
        var wert = Roh(); ((JsonArray)wert["arten"]!)[0]!["du"] = "viel";
        Assert.Throws<HlksFehler>(() => SanitaerKatalog.Lesen(wert));
    }

    [Fact]
    public void OhneKatalogdatei_AuswahlLeer_AbfragenMeldenDasFehlen()
    {
        var ordner = Path.Combine(Path.GetTempPath(), "hlks_m4_ohne_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(ordner);
        try
        {
            foreach (var datei in Directory.GetFiles(Katalog.StandardOrdner(), "*.json").Where(x => !x.EndsWith(SanitaerKatalog.Datei)))
                File.Copy(datei, Path.Combine(ordner, Path.GetFileName(datei)));
            var k = Katalog.Laden(ordner);
            Assert.Null(k.Sanitaer);
            Assert.Empty(k.Klasse("WC").Eigenschaften.Single(e => e.Name == "sanitaerArt").Auswahl!);
            var p = Projekt.Anlegen(k, null, "ohne", "T", "pc");
            Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "WC", ["kennzeichen"] = "WC-1" }));
            Assert.False(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "WC-1", ["eigenschaften"] = new JsonObject { ["sanitaerArt"] = "wc_75" } }).Angenommen);
            var f = Assert.Throws<HlksFehler>(() => Tuer.Abfrage(p, "sanitaer_katalog"));
            Assert.Contains(SanitaerKatalog.Datei, f.Message);
            Assert.DoesNotContain(Pruefungen.Alle(p.Modell, k), b => b.Code is "P21" or "P22" or "P23" or "P24" or "P25");   // ohne Katalog keine Sanitärbefunde
        }
        finally { Directory.Delete(ordner, true); }
    }
}
