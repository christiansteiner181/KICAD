using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.SanitaerHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 4 Teil B, Abwasser: Tabellenwahl je Leitungsart am Beispielprojekt mit FIKTIVEN Normtabellen (die echten Werte liegen nie im
/// Repository, SanitaerNormtabellen). Geprüft wird der Rechenweg: Tabelle 9/11 erste passende Zeile, Tabelle 22 mit Q = max(K·√ΣDU, grösster
/// Einzel-DU) in der Gefällespalte, Mindest-DN, Vergleich Innendurchmesser gegen Tabelle 5.</summary>
[Collection("Normtabellen")]
public class SanitaerDimensionierungTests
{
    static readonly Projekt B = Beispielprojekt.Erzeugen(Kat, null);

    const string Fiktiv = """
    { "anschlussleitungen": { "tabelle9_unbeluefteteAnschlussleitungen": { "tabelle": "T9 fiktiv", "werte": [
        { "hoechstDU": 1, "groesstEinzelDU": 0.5, "DN": 50 }, { "hoechstDU": 3, "groesstEinzelDU": 1.5, "DN": 70, "fussnoten": ["1"] },
        { "hoechstDU": 8, "groesstEinzelDU": 2, "DN": 100, "fussnoten": ["2"] }, { "hoechstDU": 20, "groesstEinzelDU": 2, "DN": 125 } ] } },
      "fallleitungen": { "tabelle11_hauptlueftung_winkelAbzweig": { "tabelle": "T11 fiktiv", "werte": [
        { "hoechstDU_K05": 2, "groesstEinzelDU": 0.5, "Qmax_ls": 1, "DN": 70 }, { "hoechstDU_K05": 20, "groesstEinzelDU": 1.5, "Qmax_ls": 3, "DN": 90 },
        { "hoechstDU_K05": 100, "groesstEinzelDU": 2, "Qmax_ls": 6, "DN": 110 }, { "hoechstDU_K05": 500, "groesstEinzelDU": 2, "Qmax_ls": 12, "DN": 160 } ] } },
      "sammelUndGrundleitungen": { "tabelle22": { "tabelle": "T22 fiktiv", "gefaelleSpalten_prozent": [1, 2, 3], "werte": [
        { "DN": 80, "Qmax_ls": [1, 2, 3], "fuerGrundleitung": false }, { "DN": 100, "Qmax_ls": [3, 4, 5], "fuerGrundleitung": true },
        { "DN": 125, "Qmax_ls": [6, 8, 10], "fuerGrundleitung": true }, { "DN": 150, "Qmax_ls": [12, 15, 18], "fuerGrundleitung": true } ] } },
      "mindestInnendurchmesser": { "tabelle": "T5 fiktiv", "werte": [ { "DN": 50, "IDmin_mm": 40 }, { "DN": 70, "IDmin_mm": 60 }, { "DN": 80, "IDmin_mm": 70 },
        { "DN": 90, "IDmin_mm": 80 }, { "DN": 100, "IDmin_mm": 90 }, { "DN": 110, "IDmin_mm": 100 }, { "DN": 125, "IDmin_mm": 110 }, { "DN": 150, "IDmin_mm": 140 },
        { "DN": 160, "IDmin_mm": 150 } ] } }
    """;

    static JsonObject Mit(Func<JsonObject> f, string? inhalt, string? trinkwasser = null)
    {
        var ordner = Path.Combine(Path.GetTempPath(), "hlks-normtabellen-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(ordner, "sanitaer"));
        if (inhalt is not null) File.WriteAllText(Path.Combine(ordner, "sanitaer", "abwasser.json"), inhalt);
        if (trinkwasser is not null) File.WriteAllText(Path.Combine(ordner, "sanitaer", "trinkwasser.json"), trinkwasser);
        var alt = Environment.GetEnvironmentVariable(SanitaerNormtabellen.Umgebungsvariable);
        Environment.SetEnvironmentVariable(SanitaerNormtabellen.Umgebungsvariable, ordner);
        try { return f(); }
        finally { Environment.SetEnvironmentVariable(SanitaerNormtabellen.Umgebungsvariable, alt); Directory.Delete(ordner, true); }
    }

    [Fact]
    public void Abwasser_waehlt_je_Leitungsart_die_Tabelle_und_die_erste_passende_Zeile()
    {
        var r = Mit(() => Abfrage(B, "sanitaer_dimensionierung", new JsonObject { ["anlage"] = "A-SAN" }), Fiktiv);
        var l = r["leitungen"]!.AsArray().OfType<JsonObject>().ToList();
        Assert.Contains(l, x => (string?)x["art"] == "fall");
        Assert.Contains(l, x => (string?)x["art"] is "sammel" or "grund");
        foreach (var x in l)
        {
            double du = Zahl(x["summeDu"]), duMax = Zahl(x["groessterDu"]);
            int? soll = (int?)(x["dnTabelle"] ?? x["dnSoll"]);   // Tabellenwert vor «nicht kleiner als zufliessend»
            switch ((string?)x["art"])
            {
                case "fall":
                    // K fiktiv 0.5 im Beispiel: Spalte DU
                    int erwartet = du <= 2 && duMax <= 0.5 ? 70 : du <= 20 && duMax <= 1.5 ? 90 : du <= 100 ? 110 : 160;
                    Assert.Equal(erwartet, soll);
                    break;
                case "sammel" or "grund" when x["gefaelle"] is not null:
                    double q = Math.Max(0.5 * Math.Sqrt(du), duMax);
                    Etwa(q, Zahl(x["q"]), "Q = max(K·√ΣDU, grösster Einzel-DU), auf 3 Stellen", 0.0006);
                    int sp = new[] { 1.0, 2, 3 }.ToList().FindLastIndex(g => g <= Zahl(x["gefaelle"]) + 1e-9);
                    var zeilen = new (int Dn, double[] Q, bool Grund)[] { (80, [1, 2, 3], false), (100, [3, 4, 5], true), (125, [6, 8, 10], true), (150, [12, 15, 18], true) };
                    int dn = zeilen.First(z => ((string?)x["art"] != "grund" || z.Grund) && z.Q[sp] >= q - 1e-9).Dn;
                    if ((string?)x["art"] == "sammel") dn = Math.Max(dn, (int)x["wc"]! > 0 ? 100 : 80);
                    Assert.Equal(dn, soll);
                    break;
            }
            if ((int?)x["dnSoll"] is int s && x["innenIst"] is JsonNode ii)
            {
                var idMin = new Dictionary<int, double> { [50] = 40, [70] = 60, [80] = 70, [90] = 80, [100] = 90, [110] = 100, [125] = 110, [150] = 140, [160] = 150 }[s];
                Assert.Equal(ii.GetValue<double>() + 1e-6 >= idMin, (bool?)x["ok"]);
            }
        }
    }

    [Fact]
    public void Spitzendurchfluss_nach_Diagramm_1_mit_Kurvenwahl_und_Startpunkt()
    {
        // Konstanten wie kataloge/sanitaer_belastungswerte.json (Büro-Excel «Diagramm 1»), Bereiche fiktiv gleich
        var tw = JsonNode.Parse("""{ "diagramm1": { "einheitLU_ls": 0.1, "gleichungen": [ { "a": 0.459, "b": 0.353, "geltungsbereichQT_ls": [0.3, 300] }, { "a": 0.598, "b": 0.257, "geltungsbereichQT_ls": [0.5, 15] } ] } }""")!.AsObject();
        Etwa(0.459 * Math.Pow(13.2, 0.353), SanitaerDimensionierung.Spitze(tw, 13.2, 3).Qd, "grösster LU 3");
        Etwa(0.598 * Math.Pow(13.2, 0.257), SanitaerDimensionierung.Spitze(tw, 13.2, 5).Qd, "grösster LU 5");
        Etwa(0.2, SanitaerDimensionierung.Spitze(tw, 0.2, 3).Qd, "unter dem Startpunkt QD = QT");
        Assert.NotNull(SanitaerDimensionierung.Spitze(tw, 20, 5).Hinweis);   // über dem Geltungsbereich
    }

    [Fact]
    public void Abwasser_nie_kleiner_als_eine_zufliessende_Leitung()
    {
        var r = Mit(() => Abfrage(B, "sanitaer_dimensionierung", new JsonObject()), Fiktiv);
        var soll = r["leitungen"]!.AsArray().OfType<JsonObject>().Where(x => x["dnSoll"] is not null).ToDictionary(x => (string)x["leitung"]!, x => (int)x["dnSoll"]!);
        Assert.NotEmpty(soll);
        foreach (var (kz, dn) in soll)
        {
            var e = B.Modell.Finde(kz)!;
            foreach (var x in Netz.Folgen(B.Modell, Kat, e, "stromauf", Netz.ElementMedium(B.Modell, e)))
                if (soll.TryGetValue(x.Element.Kennzeichen, out var auf)) Assert.True(dn >= auf, $"{kz} DN {dn} kleiner als zufliessend {x.Element.Kennzeichen} DN {auf}");
        }
    }

    [Fact]
    public void Fallstrang_trennt_Fallleitung_Anschlussleitung_mit_Fallstrecke_und_Fuss()
    {
        // Ein Strang «fallstrang» wie aus dem Import: WC-1.sw → A1 (steil, Fallstrecke) → T0 → A2 (waagrecht) → T1.c; F2 (steil) T1.b → T2.a; F3 (waagrecht, Fuss) T2.b → KS-1
        var p = Netzprojekt(0.5, "SW");
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "SW-1", ["eigenschaften"] = new JsonObject { ["art"] = "fallstrang" } }));
        Apparat(p, "WC", "WC-1"); Kontrollschacht(p);
        foreach (var t in new[] { "T0", "T1", "T2" }) Formteil(p, t, "SW-1");
        void L(string kz, string von, string nach, double[] a, double[] b) =>
            Ok(p.Ausfuehren("leitung_ziehen", new JsonObject { ["kennzeichen"] = kz, ["strang"] = "SW-1", ["dn"] = 100, ["von"] = von, ["nach"] = nach, ["punkte"] = Pts(a, b) }));
        L("A1", "WC-1.sw", "T0.a", [0, 0, 3000], [0, 0, 2600]);
        L("A2", "T0.b", "T1.c", [0, 0, 2600], [1000, 0, 2500]);
        L("F2", "T1.b", "T2.a", [1000, 0, 2500], [1000, 0, 0]);
        L("F3", "T2.b", "KS-1.ein", [1000, 0, 0], [3000, 0, -50]);
        Assert.Equal(("anschluss", false), SanitaerDimensionierung.Leitungsart(p.Modell, p.Katalog, p.Modell.Finde("A1")!));
        Assert.Equal(("anschluss", false), SanitaerDimensionierung.Leitungsart(p.Modell, p.Katalog, p.Modell.Finde("A2")!));
        Assert.Equal(("fall", false), SanitaerDimensionierung.Leitungsart(p.Modell, p.Katalog, p.Modell.Finde("F2")!));
        Assert.Equal(("sammel", false), SanitaerDimensionierung.Leitungsart(p.Modell, p.Katalog, p.Modell.Finde("F3")!));
    }

    [Fact]
    public void Abwasser_ohne_Anlage_nimmt_K_der_Projektinformation()
    {
        // SW-System ohne Anlage: WC-1.sw → S1 → T1.a, WT-1.sw → S2 → T1.c, T1.b → S3 → KS-1 (S3 ohne Strangart: Sammelleitung geschätzt)
        var p = Netzprojekt(null, "KW");
        SystemMitStrang(p, "SW", null);
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Projektinformation", ["eigenschaften"] = new JsonObject { ["abflusskennzahl"] = new JsonObject { ["wert"] = 0.7, ["quelle"] = "Testwert PI" } } }));
        Apparat(p, "WC", "WC-1"); Apparat(p, "Waschtisch", "WT-1"); Kontrollschacht(p);
        Formteil(p, "T1", "SW-1");
        Rohr(p, "S1", "SW-1", "WC-1.sw", "T1.a");
        Rohr(p, "S2", "SW-1", "WT-1.sw", "T1.c");
        Rohr(p, "S3", "SW-1", "T1.b", "KS-1.ein");
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "S3", ["eigenschaften"] = new JsonObject { ["gefaelle"] = 2 } }));
        var s3 = Mit(() => Abfrage(p, "sanitaer_dimensionierung", new JsonObject { ["leitung"] = "S3" }), Fiktiv)["leitungen"]![0]!;
        Assert.Equal("sammel", (string?)s3["art"]);
        Assert.Equal(0.7, Zahl(s3["k"]));
        Assert.Contains("Projektinformation", (string?)s3["kHerkunft"]);
        Assert.NotNull(s3["dnSoll"]);
    }

    [Fact]
    public void Trinkwasser_Kaltwasser_vor_dem_Wassererwaermer_traegt_Warmwasser_und_Leitungsart_ab_Verteiler()
    {
        // KW: HA-1 → R1 → T1; T1.b → R2 → WE-1.kw; T1.c → R3 → VT-1.a; VT-1.v1 → R7 → WT-1.kw. WW: WE-1.ww → R4 → T2; T2.b → R5 → WT-1.ww; T2.c → R6 → BW-1.ww
        var p = Netzprojekt(null, "KW", "WW");
        Apparat(p, "Waschtisch", "WT-1"); Apparat(p, "Badewanne", "BW-1");
        Hausanschluss(p); Wassererwaermer(p);
        Formteil(p, "T1", "KW-1"); Formteil(p, "T2", "WW-1");
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Verteiler", ["kennzeichen"] = "VT-1", ["teilVon"] = "KW-1", ["geschoss"] = "EG",
            ["anschluesse"] = new JsonArray(new[] { "a", "v1", "v2" }.Select(x => (JsonNode)new JsonObject { ["name"] = x, ["medium"] = "*", ["fluss"] = "beide" }).ToArray()),
        }));
        Rohr(p, "R1", "KW-1", "HA-1.aus", "T1.a");
        Rohr(p, "R2", "KW-1", "T1.b", "WE-1.kw");
        Rohr(p, "R3", "KW-1", "T1.c", "VT-1.a");
        Rohr(p, "R7", "KW-1", "VT-1.v1", "WT-1.kw");
        Rohr(p, "R4", "WW-1", "WE-1.ww", "T2.a");
        Rohr(p, "R5", "WW-1", "T2.b", "WT-1.ww");
        Rohr(p, "R6", "WW-1", "T2.c", "BW-1.ww");
        var netz = SanitaerBelastung.Ergebnis(p.Modell, p.Katalog);
        const string tw = """{ "diagramm1": { "einheitLU_ls": 0.1, "gleichungen": [ { "a": 0.459, "b": 0.353, "geltungsbereichQT_ls": [0.3, 300] }, { "a": 0.598, "b": 0.257, "geltungsbereichQT_ls": [0.5, 15] } ] } }""";
        var l = Mit(() => Abfrage(p, "sanitaer_dimensionierung", new JsonObject()), null, tw)["leitungen"]!.AsArray().OfType<JsonObject>().ToDictionary(x => (string)x["leitung"]!);
        var r1 = netz.Leitung(p.Modell.Finde("R1")!.Id)!;
        Assert.True(r1.SummeLuWw > 0);
        Etwa(r1.SummeLuKw + r1.SummeLuWw, Zahl(l["R1"]["summeLu"]), "KW vor dem Wassererwärmer: ΣLU KW + ΣLU WW");
        Etwa(netz.Leitung(p.Modell.Finde("R4")!.Id)!.SummeLuWw, Zahl(l["R4"]["summeLu"]), "WW-Leitung: nur ΣLU WW");
        Assert.Equal("A", (string?)l["R7"]["art"]);   // ein Apparat, gespeist aus dem Verteiler
        Assert.Equal("T", (string?)l["R3"]["art"]);   // ein Apparat, aber nicht direkt ab Verteiler
        Assert.Equal("T", (string?)l["R1"]["art"]);
    }

    [Fact]
    public void Trinkwasser_Tabelle_nach_System_und_Feld_nach_Laenge_und_LU()
    {
        var tw = JsonNode.Parse("""
        { "belastungswerttabellen": { "tabellen": [
          { "tabelle": "X1", "system": "GF/JRG Sanipex MT", "leitungsart": "T", "spaltenKopf": { "abgewickelteLaenge_m": [10, 20, 10, 20], "wasserzaehler": ["ohne", "ohne", "mit", "mit"] },
            "zeilen": [ { "lu": 5, "werte": ["16x2.2", "20x2.8", "20x2.8", "26x3"] }, { "lu": 10, "werte": ["20x2.8", "26x3", "26x3", "32x3"] } ] },
          { "tabelle": "X2", "system": "Nussbaum Optiflex-profix", "leitungsart": "T" }, { "tabelle": "X3", "system": "Nussbaum Optiflex-Flowpress", "leitungsart": "T" } ] } }
        """)!.AsObject();
        var t = Assert.Single(SanitaerDimensionierung.Tabellen(tw, "JRG Sanipex MT 20x2.8", "T"));
        Assert.Empty(SanitaerDimensionierung.Tabellen(tw, "JRG Sanipex MT 20x2.8", "V"));
        Assert.Equal(2, SanitaerDimensionierung.Tabellen(tw, "Nussbaum Optiflex", "T").Count);   // mehrdeutig → Hinweis
        Assert.Equal("26x3", SanitaerDimensionierung.Feld(t, 12, 6).Rohr);   // Spalte 20 m ohne Zähler, Zeile 10 LU
        Assert.Equal("16x2.2", SanitaerDimensionierung.Feld(t, 10, 5).Rohr);   // genau auf der Stufe
        Assert.Equal(-1, SanitaerDimensionierung.Feld(t, 25, 1).Spalte);   // länger als «ohne Zähler» – nie in die Spalten «mit»
        Assert.Null(SanitaerDimensionierung.Feld(t, 5, 11).Zeile);
    }

    [Fact]
    public void Ohne_Normtabellen_sagt_die_Antwort_es_und_rechnet_nicht()
    {
        var r = Mit(() => Abfrage(B, "sanitaer_dimensionierung", new JsonObject()), null);
        Assert.Empty(r["leitungen"]!.AsArray());
        Assert.Contains(r["hinweise"]!.AsArray(), h => h!.GetValue<string>().Contains("Normtabellen Abwasser fehlen"));
    }
}

[CollectionDefinition("Normtabellen", DisableParallelization = true)]
public sealed class NormtabellenSammlung;
