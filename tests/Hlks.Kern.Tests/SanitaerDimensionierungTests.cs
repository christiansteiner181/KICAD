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

    static JsonObject Mit(Func<JsonObject> f, string? inhalt)
    {
        var ordner = Path.Combine(Path.GetTempPath(), "hlks-normtabellen-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(ordner, "sanitaer"));
        if (inhalt is not null) File.WriteAllText(Path.Combine(ordner, "sanitaer", "abwasser.json"), inhalt);
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
            int? soll = (int?)x["dnSoll"];
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
            if (soll is int s && x["innenIst"] is JsonNode ii)
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
    public void Ohne_Normtabellen_sagt_die_Antwort_es_und_rechnet_nicht()
    {
        var r = Mit(() => Abfrage(B, "sanitaer_dimensionierung", new JsonObject()), null);
        Assert.Empty(r["leitungen"]!.AsArray());
        Assert.Contains(r["hinweise"]!.AsArray(), h => h!.GetValue<string>().Contains("Normtabellen Abwasser fehlen"));
    }
}

[CollectionDefinition("Normtabellen", DisableParallelization = true)]
public sealed class NormtabellenSammlung;
