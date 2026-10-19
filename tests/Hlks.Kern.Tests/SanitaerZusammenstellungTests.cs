using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.SanitaerHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 4 Teil A-K, Punkt 4 und 7: Sanitär-Zusammenstellung je Anlage wie Christians Excel. Referenzfall Walkringen aus dem Katalog
/// (referenz: Stückzahlen und Excel-Ergebnis) auf 1e-9 – einmal rein aus den Stückzahlen, einmal als Projekt mit Netz über die Befehle.
/// Zusätzlich die Zahlen aus dem Auftrag (ΣKW 80, ΣWW 52, ΣDU 38.8, Total LU 132, 13.2 l/s …) als feste Werte, damit eine Änderung am Katalog
/// auffällt. Abflusskennzahlen in Tests: Katalogwert 0.5 (nur Walkringen) bzw. «Testwert».</summary>
public class SanitaerZusammenstellungTests
{
    static JsonObject Referenz => SKat.Referenz;

    static List<(SanitaerArt Art, int Anzahl)> ReferenzStueckzahlen() =>
        ((JsonObject)Referenz["anzahl"]!).Select(x => (SKat.Art(x.Key) ?? throw new Xunit.Sdk.XunitException($"Art {x.Key} fehlt"), x.Value!.GetValue<int>())).ToList();

    static void ReferenzPruefen(double summeLuKw, double summeLuWw, double summeDu, double totalLu, double volumenstrom, double spitze3, double spitze5,
        double groessterLu, int kurve, double spitzendurchfluss, double? schmutzwasser)
    {
        // gegen den Katalog (referenz)
        Etwa(Zahl(Referenz["summeLuKw"]), summeLuKw, "ΣLU KW");
        Etwa(Zahl(Referenz["summeLuWw"]), summeLuWw, "ΣLU WW");
        Etwa(Zahl(Referenz["summeDu"]), summeDu, "ΣDU");
        Etwa(Zahl(Referenz["totalLu"]), totalLu, "Total LU");
        Etwa(Zahl(Referenz["volumenstrom"]), volumenstrom, "Volumenstrom");
        Etwa(Zahl(Referenz["spitzeKurve3"]), spitze3, "Spitze Kurve 3");
        Etwa(Zahl(Referenz["spitzeKurve5"]), spitze5, "Spitze Kurve 5");
        Etwa(Zahl(Referenz["schmutzwasser"]), schmutzwasser ?? double.NaN, "Q_WW");
        // gegen die Zahlen des Auftrags (Excel Walkringen 14.03.2024)
        Etwa(80, summeLuKw, "ΣLU KW (Auftrag)");
        Etwa(52, summeLuWw, "ΣLU WW (Auftrag)");
        Etwa(38.8, summeDu, "ΣDU (Auftrag)");
        Etwa(132, totalLu, "Total LU (Auftrag)");
        Etwa(13.2, volumenstrom, "Volumenstrom (Auftrag)");
        Etwa(1.1412355278976094, spitze3, "Spitze Kurve 3 (Auftrag)");
        Etwa(1.160616349950851, spitze5, "Spitze Kurve 5 (Auftrag)");
        Etwa(3.1144823004794873, schmutzwasser ?? double.NaN, "Q_WW mit K 0.5 (Auftrag)");
        // grösster Einzel-LU 5 (Entnahmearmatur Garten/Garage) → Kurve «grösster LU 5»
        Etwa(5, groessterLu, "grösster Einzel-LU");
        Assert.Equal(5, kurve);
        Etwa(spitze5, spitzendurchfluss, "massgebend = Kurve 5");
    }

    [Fact]
    public void Referenz_Walkringen_AusDenStueckzahlen()
    {
        var zeilen = ReferenzStueckzahlen().Select(x => new SanitaerZeile { Art = x.Art, JeGeschoss = [x.Anzahl], Anzahl = x.Anzahl }).ToList();
        var a = SanitaerZusammenstellung.Auswerten(SKat, zeilen, [], SKat.AbflusskennzahlBeispiel, "Katalog M65 (Walkringen)");
        ReferenzPruefen(a.SummeLuKw, a.SummeLuWw, a.SummeDu, a.TotalLu, a.Volumenstrom, a.SpitzeKurve3, a.SpitzeKurve5, a.GroessterLu, a.MassgebendeKurve, a.Spitzendurchfluss, a.Schmutzwasser);
        Assert.Equal(49, a.Apparate);
        // ohne Spezialinstallationen: Spitze gesamt = Spitze, Q_tot = Q_WW
        Etwa(a.SpitzeKurve5, a.SpitzeGesamt, "Spitze gesamt");
        Etwa(a.Schmutzwasser!.Value, a.Gesamtabfluss!.Value, "Q_tot");
        // Zeilen in Katalogreihenfolge (Excel-Zeilen aufsteigend)
        Assert.Equal(a.Zeilen.Select(z => z.Art.Zeile).Order(), a.Zeilen.Select(z => z.Art.Zeile));
        var wc = a.Zeilen.Single(z => z.Art.Schluessel == "wc_75");
        Assert.Equal(10, wc.SummeLuKw);
        Assert.Null(wc.SummeLuWw);   // Excel-Zelle leer: das WC hat keinen WW-Anschluss
        Assert.Equal(20, wc.SummeDu);
    }

    /// <summary>Walkringen als Projekt: die Stückzahlen des Excel als Apparate (Klassen passend, Art ausdrücklich), verteilt auf EG und OG,
    /// je Medium ein Verteiler: KW vom Hausanschluss (auch zum Wassererwärmer), WW vom Wassererwärmer, SW zum Kontrollschacht.
    /// Klassen: Haushalt-GWA über Spültisch als Waschmaschine, Entnahmearmatur Garten als Ausgussbecken (nur KW angeschlossen) – FIKTIV.</summary>
    internal static Projekt WalkringenProjekt(double? k = 0.5)
    {
        var p = Netzprojekt(k);
        var klasse = new Dictionary<string, string>
        {
            ["waschtisch"] = "Waschtisch", ["dusche"] = "Dusche", ["badewanne"] = "Badewanne", ["spuelbecken"] = "Spueltisch",
            ["gwa_spueltisch"] = "Waschmaschine", ["wc_75"] = "WC", ["entnahme_garten"] = "Ausgussbecken",
        };
        var apparate = new List<(string Kz, SanitaerArt Art)>();
        foreach (var (art, anzahl) in ReferenzStueckzahlen())
            for (int i = 1; i <= anzahl; i++)
            {
                var kz = $"{art.Schluessel}-{i}";
                Apparat(p, klasse[art.Schluessel], kz, art.Schluessel, i % 2 == 0 ? "OG" : "EG");
                apparate.Add((kz, art));
            }
        Hausanschluss(p); Wassererwaermer(p); Kontrollschacht(p);
        var kw = apparate.Where(a => a.Art.LuKw is not null).Select(a => a.Kz + ".kw").Append("WE-1.kw").ToList();
        var ww = apparate.Where(a => a.Art.LuWw is not null).Select(a => a.Kz + ".ww").ToList();
        var sw = apparate.Where(a => a.Art.Du is not null).Select(a => a.Kz + ".sw").ToList();
        Verteiler(p, "VT-KW", "KW-1", kw.Count); Rohr(p, "LK-0", "KW-1", "HA-1.aus", "VT-KW.a");
        for (int i = 0; i < kw.Count; i++) Rohr(p, $"LK-{i + 1}", "KW-1", $"VT-KW.v{i + 1}", kw[i]);
        Verteiler(p, "VT-WW", "WW-1", ww.Count); Rohr(p, "LW-0", "WW-1", "WE-1.ww", "VT-WW.a");
        for (int i = 0; i < ww.Count; i++) Rohr(p, $"LW-{i + 1}", "WW-1", $"VT-WW.v{i + 1}", ww[i]);
        Verteiler(p, "VT-SW", "SW-1", sw.Count); Rohr(p, "LS-0", "SW-1", "VT-SW.a", "KS-1.ein");
        for (int i = 0; i < sw.Count; i++) Rohr(p, $"LS-{i + 1}", "SW-1", sw[i], $"VT-SW.v{i + 1}");
        return p;
    }

    [Fact]
    public void Referenz_Walkringen_AlsProjektMitNetz()
    {
        var p = WalkringenProjekt();
        var z = Abfrage(p, "sanitaer_zusammenstellung", new JsonObject { ["anlage"] = "A-1" });
        ReferenzPruefen(Zahl(z["summeLuKw"]), Zahl(z["summeLuWw"]), Zahl(z["summeDu"]), Zahl(z["totalLu"]), Zahl(z["volumenstrom"]), Zahl(z["spitzeKurve3"]),
            Zahl(z["spitzeKurve5"]), Zahl(z["groessterLu"]), z["massgebendeKurve"]!.GetValue<int>(), Zahl(z["spitzendurchfluss"]), Zahl(z["schmutzwasser"]));
        Assert.Equal(49, z["apparate"]!.GetValue<int>());
        Assert.Equal(new[] { "EG", "OG" }, z["geschosse"]!.AsArray().Select(g => g!["kennzeichen"]!.GetValue<string>()));
        var wt = z["zeilen"]!.AsArray().Single(r => r!["art"]!.GetValue<string>() == "waschtisch")!;
        Assert.Equal(new[] { 5, 5 }, wt["jeGeschoss"]!.AsArray().Select(x => x!.GetValue<int>()));
        Assert.Equal(8, wt["zeile"]!.GetValue<int>());
        Assert.Contains("Tabelle1 Zeile 8", wt["quelle"]!.GetValue<string>());
        Assert.Contains("Testwert", z["abflusskennzahlHerkunft"]!.GetValue<string>());
        Assert.Empty(z["hinweise"]!.AsArray());

        // Netz: die Anlage trägt dieselben Summen; die Leitung vom Hausanschluss alles (KW direkt, WW über den Wassererwärmer)
        var an = Abfrage(p, "sanitaer_belastung", new JsonObject { ["anlage"] = "A-1" })["anlagen"]![0]!;
        Etwa(80, Zahl(an["trinkwasser"]!["summeLuKw"]), "Anlage ΣLU KW");
        Etwa(52, Zahl(an["trinkwasser"]!["summeLuWw"]), "Anlage ΣLU WW");
        Etwa(5, Zahl(an["trinkwasser"]!["groessterLu"]), "Anlage grösster LU");
        Etwa(38.8, Zahl(an["abwasser"]!["summeDu"]), "Anlage ΣDU");
        var ha = Belastung(p, "LK-0");
        Etwa(80, Zahl(ha["summeLuKw"]), "LK-0 ΣLU KW");
        Etwa(52, Zahl(ha["summeLuWw"]), "LK-0 ΣLU WW (über WE-1)");
        Assert.Equal(49, ha["apparate"]!.GetValue<int>());
        Assert.False(ha["masche"]!.GetValue<bool>());
        var we = Belastung(p, $"LK-{p.Modell.Finde("VT-KW")!.Anschluesse.Count - 1}");   // letzte Abgangsleitung: zum Wassererwärmer
        Etwa(0, Zahl(we["summeLuKw"]), "Zuleitung WE ΣLU KW");
        Etwa(52, Zahl(we["summeLuWw"]), "Zuleitung WE ΣLU WW");
        Etwa(38.8, Zahl(Belastung(p, "LS-0")["summeDu"]), "LS-0 ΣDU");
        Assert.True(Sanitaerbefunde(p).Count == 0, Liste(Sanitaerbefunde(p)));
    }

    [Fact]
    public void Spezialinstallationen_AddiertWieExcel_C59_I59_M67()
    {
        var p = WalkringenProjekt();
        var vorher = SanitaerZusammenstellung.Rechnen(p.Modell, p.Katalog, p.Modell.Hole("A-1"));
        Ok(p.Ausfuehren("element_aendern", new JsonObject
        {
            ["element"] = "A-1",
            ["eigenschaften"] = new JsonObject
            {
                ["spezialinstallationen"] = new JsonObject
                {
                    ["wert"] = JsonNode.Parse("""[{ "bezeichnung": "Getränkeautomat (fiktiv)", "versorgung": 0.3, "entsorgung": 0.2 }, { "bezeichnung": "Spülstation (fiktiv)", "versorgung": 0.5 }, { "bezeichnung": "Abscheider (fiktiv)", "entsorgung": 0.4 }]"""),
                    ["quelle"] = "Testwert (fiktiv)",
                },
            },
        }));
        var a = SanitaerZusammenstellung.Rechnen(p.Modell, p.Katalog, p.Modell.Hole("A-1"));
        Assert.Equal(3, a.Spezialinstallationen.Count);
        Etwa(0.8, a.SpezialVersorgung, "Σ Versorgung (I51)");
        Etwa(0.6, a.SpezialEntsorgung, "Σ Entsorgung (N51)");
        Etwa(vorher.SpitzeKurve3 + 0.8, a.SpitzeGesamtKurve3, "C59 = C57 + I51");
        Etwa(vorher.SpitzeKurve5 + 0.8, a.SpitzeGesamtKurve5, "I59 = I57 + I51");
        Etwa(a.SpitzeGesamtKurve5, a.SpitzeGesamt, "massgebend Kurve 5");
        Etwa(3.1144823004794873, a.Schmutzwasser!.Value, "Q_WW unverändert");
        Etwa(3.1144823004794873 + 0.6, a.Gesamtabfluss!.Value, "M67 = M66 + N51");
        Etwa(vorher.SpitzeKurve5, a.SpitzeKurve5, "Spitze ohne Spezial unverändert");
        var z = Abfrage(p, "sanitaer_zusammenstellung", new JsonObject { ["anlage"] = "A-1" });
        Assert.Equal("Getränkeautomat (fiktiv)", z["spezialinstallationen"]![0]!["bezeichnung"]!.GetValue<string>());
        Assert.Contains("C59", z["formeln"]!["spitzeGesamt"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("""[{ "versorgung": 0.3 }]""", "bezeichnung")]
    [InlineData("""[{ "bezeichnung": "X" }]""", "versorgung oder entsorgung")]
    [InlineData("""[{ "bezeichnung": "X", "versorgung": -1 }]""", "≥ 0")]
    [InlineData("""[{ "bezeichnung": "X", "versorgung": "0.3" }]""", "Zahl")]
    [InlineData("""[{ "bezeichnung": "X", "versorgung": 0.3, "menge": 2 }]""", "menge")]
    [InlineData("""[ 3 ]""", "Objekt")]
    public void Spezialinstallationen_Ungueltig_WirdAbgelehnt(string liste, string text)
    {
        var p = Netzprojekt(0.5, "SW");
        var e = Nein(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "A-1", ["eigenschaften"] = new JsonObject { ["spezialinstallationen"] = JsonNode.Parse(liste) } }));
        Assert.Contains(text, e.Fehler);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.5)]
    public void Abflusskennzahl_NullOderNegativ_WirdAbgelehnt(double k)
    {
        var p = Netzprojekt(null, "SW");
        Nein(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "A-1", ["eigenschaften"] = new JsonObject { ["abflusskennzahl"] = k } }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Projektinformation" }));
        Nein(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "PI-1", ["eigenschaften"] = new JsonObject { ["abflusskennzahl"] = k } }));
    }

    [Fact]
    public void Abflusskennzahl_Fehlt_P25_KeinQww_VorgabeDerProjektinformation_AnlageGehtVor()
    {
        var p = WalkringenProjekt(k: null);
        var b = Assert.Single(Sanitaerbefunde(p));
        Assert.Equal(("P25", "warnung", "A-1"), (b.Code, b.Schwere, b.Element));
        Assert.Contains("SN 592000", b.Vorschlag);
        var z = Abfrage(p, "sanitaer_zusammenstellung");   // ohne anlage: die einzige Anlage mit Apparaten
        Assert.Equal("A-1", z["anlage"]!.GetValue<string>());
        Assert.Null(z["abflusskennzahl"]);
        Assert.Null(z["schmutzwasser"]);
        Assert.Null(z["gesamtabfluss"]);
        Assert.Contains(z["hinweise"]!.AsArray(), h => h!.GetValue<string>().Contains("P25"));
        Etwa(38.8, Zahl(z["summeDu"]), "ΣDU auch ohne K");

        // Vorgabe der Projektinformation (Testwert)
        var e = Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Projektinformation", ["eigenschaften"] = new JsonObject { ["abflusskennzahl"] = new JsonObject { ["wert"] = 0.7, ["quelle"] = "Testwert PI" } } }));
        Assert.Contains(e.BehobeneBefunde, x => x.Code == "P25");
        var a = SanitaerZusammenstellung.Rechnen(p.Modell, p.Katalog, p.Modell.Hole("A-1"));
        Etwa(0.7 * Math.Sqrt(38.8), a.Schmutzwasser!.Value, "Q_WW mit K der Projektinformation");
        Assert.Contains("Projektinformation", a.AbflusskennzahlHerkunft);
        // die Anlage geht vor
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "A-1", ["eigenschaften"] = new JsonObject { ["abflusskennzahl"] = new JsonObject { ["wert"] = 0.5, ["quelle"] = "Testwert Anlage" } } }));
        a = SanitaerZusammenstellung.Rechnen(p.Modell, p.Katalog, p.Modell.Hole("A-1"));
        Etwa(3.1144823004794873, a.Schmutzwasser!.Value, "Q_WW mit K der Anlage");
        Assert.Contains("Testwert Anlage", a.AbflusskennzahlHerkunft);
    }

    [Fact]
    public void MassgebendeKurve_GroessterEinzelLuBis3_Kurve3_Ueber3_Kurve5()
    {
        SanitaerZeile Z(string art, int n) => new() { Art = SKat.Art(art)!, JeGeschoss = [n], Anzahl = n };
        var bis3 = SanitaerZusammenstellung.Auswerten(SKat, [Z("badewanne", 2), Z("waschtisch", 3)], [], null, null);
        Assert.Equal(3, bis3.GroessterLu);
        Assert.Equal(3, bis3.MassgebendeKurve);
        Assert.Equal(bis3.SpitzeKurve3, bis3.Spitzendurchfluss);
        Assert.Null(bis3.Schmutzwasser);
        // 2 Badewannen + 3 Waschtische: Total LU = 2·(3+3) + 3·(1+1) = 18 → V = 1.8 l/s, Spitzen nach den Katalogformeln
        Etwa(18, bis3.TotalLu, "Total LU");
        Etwa(Math.Pow(18 * 0.1, 0.353) * 0.459, bis3.SpitzeKurve3, "C57");
        Etwa(Math.Pow(18 * 0.1, 0.257) * 0.598, bis3.SpitzeKurve5, "I57");
        var mit5 = SanitaerZusammenstellung.Auswerten(SKat, [Z("waschtisch", 1), Z("entnahme_garten", 1)], [], null, null);
        Assert.Equal(5, mit5.GroessterLu);
        Assert.Equal(5, mit5.MassgebendeKurve);
        Assert.Equal(mit5.SpitzeKurve5, mit5.Spitzendurchfluss);
        var leer = SanitaerZusammenstellung.Auswerten(SKat, [], [], 0.5, "Testwert");
        Assert.Equal(0, leer.Volumenstrom);
        Assert.Equal(0, leer.SpitzeKurve3);
        Assert.Equal(0, leer.Schmutzwasser);
    }

    [Fact]
    public void Zusammenstellung_GeschosseNachKote_OhneGeschoss_OhneArt_Fehler()
    {
        var p = Netzprojekt(0.5, "KW", "SW");
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Geschoss", ["kennzeichen"] = "UG", ["teilVon"] = "GEB", ["eigenschaften"] = new JsonObject { ["kote"] = -3000, ["hoehe"] = 3000 },
        }));
        Apparat(p, "WC", "WC-OG", geschoss: "OG");
        Apparat(p, "WC", "WC-UG", geschoss: "UG");
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Waschtisch", ["kennzeichen"] = "WT-X" }));   // ohne Geschoss
        Apparat(p, "Bodenablauf", "BA-1");                                                                         // ohne Art
        Kontrollschacht(p);
        Formteil(p, "VT", "SW-1", "TStueck", new JsonArray(new[] { "a", "v1", "v2", "v3", "v4" }.Select(x => (JsonNode)new JsonObject { ["name"] = x, ["medium"] = "*", ["fluss"] = "beide" }).ToArray()));
        Rohr(p, "SW-0", "SW-1", "VT.a", "KS-1.ein");
        Rohr(p, "SW-a", "SW-1", "WC-OG.sw", "VT.v1");
        Rohr(p, "SW-b", "SW-1", "WC-UG.sw", "VT.v2");
        Rohr(p, "SW-c", "SW-1", "WT-X.sw", "VT.v3");
        Rohr(p, "SW-d", "SW-1", "BA-1.sw", "VT.v4");
        var z = Abfrage(p, "sanitaer_zusammenstellung", new JsonObject { ["anlage"] = "A-1" });
        Assert.Equal(new string?[] { "UG", "EG", "OG", null }, z["geschosse"]!.AsArray().Select(g => g!["kennzeichen"]?.GetValue<string>()));
        var wc = z["zeilen"]!.AsArray().Single(r => r!["art"]!.GetValue<string>() == "wc_75")!;
        Assert.Equal(new[] { 1, 0, 1, 0 }, wc["jeGeschoss"]!.AsArray().Select(x => x!.GetValue<int>()));
        var wt = z["zeilen"]!.AsArray().Single(r => r!["art"]!.GetValue<string>() == "waschtisch")!;
        Assert.Equal(new[] { 0, 0, 0, 1 }, wt["jeGeschoss"]!.AsArray().Select(x => x!.GetValue<int>()));
        Assert.Equal(new[] { "BA-1" }, z["ohneArt"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Contains(z["hinweise"]!.AsArray(), h => h!.GetValue<string>().Contains("BA-1"));
        Assert.Equal(3, z["apparate"]!.GetValue<int>());
        // Fehler: keine Anlage bzw. kein Element der Klasse Anlage
        Assert.Throws<HlksFehler>(() => Abfrage(p, "sanitaer_zusammenstellung", new JsonObject { ["anlage"] = "WC-OG" }));
        Assert.Throws<HlksFehler>(() => Abfrage(p, "sanitaer_zusammenstellung", new JsonObject { ["anlage"] = "A-99" }));
        var leer = Netzprojekt(null, "SW");
        Assert.Contains("Keine Anlage", Assert.Throws<HlksFehler>(() => Abfrage(leer, "sanitaer_zusammenstellung")).Message);
    }
}
