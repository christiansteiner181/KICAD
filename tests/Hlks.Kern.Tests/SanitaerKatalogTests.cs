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
        // Excel Blatt «MFHA» A7:D40: Zeilen 7 bis 40 lückenlos in Katalogreihenfolge
        Assert.Equal(Enumerable.Range(7, 34), SKat.Arten.Select(a => a.Zeile));
        // Blattname aus dem Katalog (quelle.bereich «Blatt «MFHA» …»), nicht aus dem Code (Prüfbericht 4A B1); fest «MFHA», damit eine Änderung auffällt
        Assert.Contains($"Blatt «{SKat.Blatt}»", Roh()["quelle"]!["bereich"]!.GetValue<string>());
        Assert.Equal("MFHA", SKat.Blatt);
        Assert.Equal("LU_DU Zusammenstellung_.xlsx", SKat.Dokument);
        // typografisch bereinigte Namen tragen den Namen im Excel (Prüfbericht 4A B4)
        foreach (var (r, a) in roh.Zip(SKat.Arten))
            Assert.Equal(r!["nameExcel"]?.GetValue<string>(), a.NameExcel);
        Assert.Equal(4, SKat.Arten.Count(a => a.NameExcel is not null));
        Assert.Equal("Waschrog KW", SKat.Art("waschtrog_kw")!.NameExcel);
    }

    [Fact]
    public void Katalog_FormelnAusDerDatei_FaktorAusDemKatalogfeld()
    {
        var r = (JsonObject)Roh()["rechnung"]!;
        // Prüfbericht 4A B5.4: Faktor aus rechnung.volumenstromFaktor { wert, zelle }, nicht mehr per regulärem Ausdruck aus dem Formeltext
        Assert.Equal(N(r["volumenstromFaktor"]!["wert"]), SKat.VolumenstromFaktor);
        Assert.Equal(0.1, SKat.VolumenstromFaktor);   // fest, damit eine Änderung des Katalogs auffällt (Excel B56 = B55*0.1)
        Assert.Equal(r["volumenstromFaktor"]!["zelle"]!.GetValue<string>(), SKat.VolumenstromFaktorZelle);
        Assert.Equal("B56 = B55*0.1", SKat.VolumenstromFaktorZelle);
        Assert.Contains(SKat.VolumenstromFaktorZelle, SKat.VolumenstromText);   // Formeltext und Feld nennen dieselbe Excel-Zelle
        Assert.Contains("B55*0.1", SKat.VolumenstromText);
        Assert.Equal(N(r["spitzeKurve3"]!["faktor"]), SKat.Kurve3.Faktor);
        Assert.Equal(N(r["spitzeKurve3"]!["exponent"]), SKat.Kurve3.Exponent);
        Assert.Equal(N(r["spitzeKurve5"]!["faktor"]), SKat.Kurve5.Faktor);
        Assert.Equal(N(r["spitzeKurve5"]!["exponent"]), SKat.Kurve5.Exponent);
        Assert.Equal("C57 = B56^0.353*0.459", SKat.Kurve3.Zelle);
        Assert.Equal("I57 = B56^0.257*0.598", SKat.Kurve5.Zelle);
        // Kurvennummern, Gültigkeitsbereiche und Regel der Kurvenwahl aus dem Katalog (Prüfbericht 4A B3)
        Assert.Equal(r["spitzeKurve3"]!["grosserLu"]!.GetValue<int>(), SKat.Kurve3.Nummer);
        Assert.Equal(r["spitzeKurve5"]!["grosserLu"]!.GetValue<int>(), SKat.Kurve5.Nummer);
        Assert.Equal((3, 5), (SKat.Kurve3.Nummer, SKat.Kurve5.Nummer));
        Assert.Equal(N(r["spitzeKurve3"]!["bereichLs"]![0]), SKat.Kurve3.BereichVon);
        Assert.Equal(N(r["spitzeKurve3"]!["bereichLs"]![1]), SKat.Kurve3.BereichBis);
        Assert.Equal(N(r["spitzeKurve5"]!["bereichLs"]![0]), SKat.Kurve5.BereichVon);
        Assert.Equal(N(r["spitzeKurve5"]!["bereichLs"]![1]), SKat.Kurve5.BereichBis);
        Assert.Equal((0.3, 300.0, 0.5, 15.0), (SKat.Kurve3.BereichVon, SKat.Kurve3.BereichBis, SKat.Kurve5.BereichVon, SKat.Kurve5.BereichBis));
        Assert.Contains("C55", SKat.Kurve3.BereichZelle);
        Assert.Contains("I56", SKat.Kurve5.BereichZelle);
        Assert.Equal(r["kurvenwahl"]!["regel"]!.GetValue<string>(), SKat.Kurvenwahl.Regel);
        Assert.Equal(r["kurvenwahl"]!["quelle"]!.GetValue<string>(), SKat.Kurvenwahl.Quelle);
        Assert.False(SKat.Kurvenwahl.Bestaetigt);
        Assert.Contains("HKAP", SKat.Kurvenwahl.Quelle);
        Assert.Equal(0.5, SKat.AbflusskennzahlBeispiel);
        Assert.Equal("M65", SKat.AbflusskennzahlBeispielZelle);
        Assert.Contains("M66", SKat.SchmutzwasserZelle);
    }

    [Fact]
    public void Quelle_NenntKatalogDokumentBlattUndExcelZeile()
    {
        var q = SKat.Quelle(31);
        Assert.Contains("sanitaer_belastungswerte.json", q);
        Assert.Contains("LU_DU Zusammenstellung_.xlsx, Blatt MFHA, Zeile 31", q);
        Assert.Contains("14.03.2024", q);
        Assert.DoesNotContain("Tabelle1", q);
        Assert.Contains("Blatt MFHA, C57", SKat.QuelleFormel(SKat.Kurve3.Zelle));
        Assert.Contains("Blatt «MFHA»", SKat.QuelleKurz);
        // bereinigter Name: die Quelle nennt den Namen im Excel
        Assert.Contains("LU_DU Zusammenstellung_.xlsx, Blatt MFHA, Zeile 21, im Excel «Waschrog KW» (", SKat.Quelle(SKat.Art("waschtrog_kw")!));
        Assert.Equal(SKat.Quelle(31), SKat.Quelle(SKat.Art("wc_75")!));
    }

    [Fact]
    public void Blatt_AusFeldBlattOderBereich_SonstAbgelehnt()
    {
        var feld = Roh(); feld["quelle"]!["blatt"] = "Testblatt (fiktiv)";
        Assert.Equal("Testblatt (fiktiv)", SanitaerKatalog.Lesen(feld).Blatt);                 // eigenes Feld geht vor
        var bereich = Roh(); bereich["quelle"]!["bereich"] = "Blatt «Zweites Blatt» A1:D9";
        Assert.Equal("Zweites Blatt", SanitaerKatalog.Lesen(bereich).Blatt);
        Assert.Contains("Blatt Zweites Blatt, Zeile 8", SanitaerKatalog.Lesen(bereich).Quelle(8));
        var ohne = Roh(); ohne["quelle"]!["bereich"] = "Tabelle1 A7:D40";                      // kein «Blatt «…»» und kein Feld blatt: nicht raten
        var f = Assert.Throws<HlksFehler>(() => SanitaerKatalog.Lesen(ohne));
        Assert.Contains("Tabellenblatt", f.Message);
    }

    [Fact]
    public void VolumenstromFaktor_AusDemFeld_FehltOderUngueltig_LadenAbgelehnt()
    {
        // fehlt das Feld: Laden abgelehnt mit Stelle und Anleitung – kein Rückgriff auf den Formeltext
        var ohne = Roh(); ((JsonObject)ohne["rechnung"]!).Remove("volumenstromFaktor");
        var f = Assert.Throws<HlksFehler>(() => SanitaerKatalog.Lesen(ohne));
        Assert.Contains("rechnung.volumenstromFaktor: fehlt", f.Message);
        Assert.Contains("{ wert, zelle }", f.Message);
        Assert.Contains("LU_DU Zusammenstellung_.xlsx", f.Vorschlag ?? "");
        Assert.Contains("rechnung.volumenstromFaktor: fehlt", Assert.Throws<HlksFehler>(() => SanitaerKatalog.Lesen(Mit(o => o["volumenstromFaktor"] = 0.1))).Message);   // Zahl statt { wert, zelle }
        // wert: Pflicht, Zahl > 0
        Assert.Contains("volumenstromFaktor.wert: Zahl fehlt", Assert.Throws<HlksFehler>(() => SanitaerKatalog.Lesen(Mit(o => ((JsonObject)o["volumenstromFaktor"]!).Remove("wert")))).Message);
        Assert.Contains("volumenstromFaktor.wert: Zahl fehlt", Assert.Throws<HlksFehler>(() => SanitaerKatalog.Lesen(Mit(o => o["volumenstromFaktor"]!["wert"] = "Faktor"))).Message);
        foreach (var w in new[] { 0.0, -0.1 })
            Assert.Contains("volumenstromFaktor.wert: muss eine Zahl > 0 sein", Assert.Throws<HlksFehler>(() => SanitaerKatalog.Lesen(Mit(o => o["volumenstromFaktor"]!["wert"] = w))).Message);
        // zelle: Pflicht und dieselbe Excel-Zelle wie im Formeltext rechnung.volumenstrom
        Assert.Contains("volumenstromFaktor.zelle: Text fehlt", Assert.Throws<HlksFehler>(() => SanitaerKatalog.Lesen(Mit(o => ((JsonObject)o["volumenstromFaktor"]!).Remove("zelle")))).Message);
        Assert.Contains("steht nicht in rechnung.volumenstrom", Assert.Throws<HlksFehler>(() => SanitaerKatalog.Lesen(Mit(o => o["volumenstromFaktor"]!["zelle"] = "B57 = B55*0.1"))).Message);
    }

    [Fact]
    public void VolumenstromFaktor_RechnungNimmtDenWertDesFelds_NichtDieZahlImFormeltext()
    {
        // abgewandelter Katalog (FIKTIV, nur für den Test): wert 0.2, Formeltext und Zelle unverändert mit «0.1» – gerechnet wird mit dem Feld
        var kat = SanitaerKatalog.Lesen(Mit(o => o["volumenstromFaktor"]!["wert"] = 0.2));
        Assert.Equal(0.2, kat.VolumenstromFaktor);
        Assert.Contains("B55*0.1", kat.VolumenstromText);
        var wc = kat.Art("wc_75")!;
        var a = SanitaerZusammenstellung.Auswerten(kat, [new SanitaerZeile { Art = wc, JeGeschoss = [10], Anzahl = 10 }], [], null, null);
        Etwa(10 * wc.LuKw!.Value, a.TotalLu, "Total LU");
        Etwa(a.TotalLu * 0.2, a.Volumenstrom, "Volumenstrom = Total LU × wert");
        // mit dem echten Katalog: Faktor des Felds
        var echt = SanitaerZusammenstellung.Auswerten(SKat, [new SanitaerZeile { Art = SKat.Art("wc_75")!, JeGeschoss = [10], Anzahl = 10 }], [], null, null);
        Etwa(echt.TotalLu * SKat.VolumenstromFaktor, echt.Volumenstrom, "Volumenstrom mit dem Katalogfeld");
    }

    /// <summary>Katalog aus der Datei mit einer Änderung unter rechnung (für Ablehnungstests).</summary>
    static JsonObject Mit(Action<JsonObject> aendern)
    {
        var roh = Roh();
        aendern((JsonObject)roh["rechnung"]!);
        return roh;
    }

    [Fact]
    public void Kurven_FehlerhafteAngaben_WerdenAbgelehnt()
    {
        var ohneLu = Roh(); ((JsonObject)ohneLu["rechnung"]!["spitzeKurve5"]!).Remove("grosserLu");
        Assert.Contains("grosserLu", Assert.Throws<HlksFehler>(() => SanitaerKatalog.Lesen(ohneLu)).Message);
        var vertauscht = Roh(); vertauscht["rechnung"]!["spitzeKurve3"]!["grosserLu"] = 6;
        Assert.Contains("kleiner", Assert.Throws<HlksFehler>(() => SanitaerKatalog.Lesen(vertauscht)).Message);
        var bereich = Roh(); bereich["rechnung"]!["spitzeKurve3"]!["bereichLs"] = new JsonArray(300, 0.3);
        Assert.Contains("bereichLs", Assert.Throws<HlksFehler>(() => SanitaerKatalog.Lesen(bereich)).Message);
        var ohneWahl = Roh(); ((JsonObject)ohneWahl["rechnung"]!).Remove("kurvenwahl");
        Assert.Contains("kurvenwahl", Assert.Throws<HlksFehler>(() => SanitaerKatalog.Lesen(ohneWahl)).Message);
        var wahlText = Roh(); wahlText["rechnung"]!["kurvenwahl"]!["bestaetigt"] = "ja";
        Assert.Contains("bestaetigt", Assert.Throws<HlksFehler>(() => SanitaerKatalog.Lesen(wahlText)).Message);
        var bestaetigt = Roh(); bestaetigt["rechnung"]!["kurvenwahl"]!["bestaetigt"] = true;
        Assert.True(SanitaerKatalog.Lesen(bestaetigt).Kurvenwahl.Bestaetigt);
    }

    [Fact]
    public void Medien_AusDenSpaltenAbgeleitet_UndGegenMedienUndSystemtypenGeprueft()
    {
        // Trinkwasser = Medien der LU-Spalten, Abwasser = Medium der DU-Spalte (eine Zuordnung, keine eigenen Listen; Prüfbericht 4A B5)
        Assert.Equal(new[] { "KW", "WW" }, SanitaerKatalog.Trinkwasser.Order());
        Assert.Equal(new[] { "SW" }, SanitaerKatalog.Abwasser.Order());
        Assert.All(SanitaerKatalog.Spalten.Keys, m => Assert.True(Kat.Medien.ContainsKey(m), $"{m} fehlt in medien.json"));
        // ein Systemtyp mit verkehrter Verbraucherrichtung (Schmutzwasser stromab) macht den Katalog ungültig
        var ordner = Path.Combine(Path.GetTempPath(), "hlks_m4_medien_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(ordner);
        try
        {
            foreach (var datei in Directory.GetFiles(Katalog.StandardOrdner(), "*.json")) File.Copy(datei, Path.Combine(ordner, Path.GetFileName(datei)));
            var st = Path.Combine(ordner, "systemtypen.json");
            var typen = (JsonArray)JsonNode.Parse(File.ReadAllText(st))!;
            typen.Single(t => t!["medium"]!.GetValue<string>() == "SW")!["verbraucherRichtung"] = "stromab";
            File.WriteAllText(st, typen.ToJsonString());
            var f = Assert.Throws<HlksFehler>(() => Katalog.Laden(ordner));
            Assert.Contains("verbraucherRichtung", f.Message);
            Assert.Contains("SW", f.Message);
        }
        finally { Directory.Delete(ordner, true); }
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
        var faktor = Roh(); faktor["rechnung"]!["volumenstrom"] = "Total LU mal etwas";   // Formeltext nennt die Zelle des Faktors nicht mehr
        Assert.Contains("volumenstromFaktor.zelle", Assert.Throws<HlksFehler>(() => SanitaerKatalog.Lesen(faktor)).Message);
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
            Assert.DoesNotContain(Pruefungen.Alle(p.Modell, k), b => b.Code is "P21" or "P22" or "P23" or "P24" or "P25" or "P26");   // ohne Katalog keine Sanitärbefunde
        }
        finally { Directory.Delete(ordner, true); }
    }
}
