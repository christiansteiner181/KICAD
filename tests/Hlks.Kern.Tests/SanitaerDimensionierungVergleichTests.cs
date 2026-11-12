using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.SanitaerHilfe;

namespace Hlks.Kern.Tests;

/// <summary>SanitaerDimensionierung (Gesamtprüfung 13.11.2026, Leistung): mit gemerkter Leitungsart je Leitung liefert die Abfrage Zeichen für
/// Zeichen dasselbe wie ohne. Netz FIKTIV: Fallstränge (Strang «fallstrang» wie aus dem Import, mit Fallstrecken der Anschlussleitungen und
/// Fuss) an einer gemeinsamen Sammelleitung; Normtabellen fiktiv wie in SanitaerDimensionierungTests.</summary>
[Collection("Normtabellen")]
public class SanitaerDimensionierungVergleichTests
{
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

    internal static T MitTabellen<T>(Func<T> f)
    {
        var ordner = Path.Combine(Path.GetTempPath(), "hlks-normtabellen-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(ordner, "sanitaer"));
        File.WriteAllText(Path.Combine(ordner, "sanitaer", "abwasser.json"), Fiktiv);
        var alt = Environment.GetEnvironmentVariable(SanitaerNormtabellen.Umgebungsvariable);
        Environment.SetEnvironmentVariable(SanitaerNormtabellen.Umgebungsvariable, ordner);
        try { return f(); }
        finally { Environment.SetEnvironmentVariable(SanitaerNormtabellen.Umgebungsvariable, alt); Directory.Delete(ordner, true); }
    }

    /// <summary>Ein Fallstrang über die Befehle (je Geschoss WC → Fallstrecke → waagrechte Anschlussleitung → Fallleitung; unten Fuss in die
    /// Sammelleitung zum Kontrollschacht), danach «straenge − 1» Kopien davon, deren Sammelleitung jeweils in das Sammel-T-Stück der Kopie davor
    /// mündet: ein zusammenhängendes Netz mit straenge · (3 · geschosse + 1) Leitungen.</summary>
    internal static Projekt Fallstraenge(int straenge, int geschosse)
    {
        var p = Netzprojekt(0.5, "SW");
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "SW-1", ["eigenschaften"] = new JsonObject { ["art"] = "fallstrang" } }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Strang", ["kennzeichen"] = "SW-S", ["teilVon"] = "SW", ["eigenschaften"] = new JsonObject { ["art"] = "sammelleitung" } }));
        Kontrollschacht(p);
        void L(string kz, string strang, string von, string nach, double[] a, double[] b) =>
            Ok(p.Ausfuehren("leitung_ziehen", new JsonObject { ["kennzeichen"] = kz, ["strang"] = strang, ["dn"] = 100, ["von"] = von, ["nach"] = nach, ["punkte"] = Pts(a, b) }));
        Formteil(p, "TS", "SW-S");
        for (int g = geschosse - 1; g >= 0; g--)
        {
            double z = g * 3000;
            Apparat(p, "WC", $"WC-{g}");
            Formteil(p, $"T0{g}", "SW-1"); Formteil(p, $"T1{g}", "SW-1");
            L($"A1{g}", "SW-1", $"WC-{g}.sw", $"T0{g}.a", [0, 0, z + 3000], [0, 0, z + 2600]);
            L($"A2{g}", "SW-1", $"T0{g}.b", $"T1{g}.c", [0, 0, z + 2600], [1000, 0, z + 2500]);
            if (g < geschosse - 1) L($"F{g}", "SW-1", $"T1{g + 1}.b", $"T1{g}.a", [1000, 0, z + 5500], [1000, 0, z + 2500]);
        }
        L("FU", "SW-1", "T10.b", "TS.c", [1000, 0, 0], [3000, 0, -50]);
        L("S", "SW-S", "TS.b", "KS-1.ein", [3000, 0, -50], [8000, 0, -100]);

        var m = p.Modell;
        var teile = m.Elemente.Where(e => e.TeilVon is not null && m.Finde(e.TeilVon)?.Klasse == "Strang" || e.Klasse == "WC").ToList();
        var ids = teile.Select(e => e.Id).ToHashSet();
        var innen = m.Verbindungen.Where(v => ids.Contains(v.Von.Element) && ids.Contains(v.Nach.Element)).ToList();
        string sId = m.Hole("S").Id, tsId = m.Hole("TS").Id;
        var zumSchacht = m.VerbindungenVon(sId).Single(v => !ids.Contains(v.Von.Element) || !ids.Contains(v.Nach.Element));
        string sEnde = zumSchacht.Von.Element == sId ? zumSchacht.Von.Anschluss : zumSchacht.Nach.Anschluss;
        var ops = new List<Operation>();
        for (int c = 1; c < straenge; c++)
        {
            foreach (var e in teile)
            {
                var k = e.Kopie();
                k.Id = $"{e.Id}#{c}"; k.Kennzeichen = $"{e.Kennzeichen}_{c}"; k.Version = 0;
                ops.Add(new Operation { Art = "element.setzen", Id = k.Id, Element = k });
            }
            foreach (var v in innen)
                ops.Add(new Operation { Art = "verbindung.setzen", Id = $"{v.Id}#{c}", Verbindung = new Verbindung { Id = $"{v.Id}#{c}", Von = new($"{v.Von.Element}#{c}", v.Von.Anschluss), Nach = new($"{v.Nach.Element}#{c}", v.Nach.Anschluss) } });
            string davor = c == 1 ? tsId : $"{tsId}#{c - 1}";
            ops.Add(new Operation { Art = "verbindung.setzen", Id = $"S#{c}", Verbindung = new Verbindung { Id = $"S#{c}", Von = new($"{sId}#{c}", sEnde), Nach = new(davor, "a") } });
        }
        Assert.Empty(m.Anwenden(ops));
        return p;
    }

    [Fact]
    public void Abfrage_MitGemerkterLeitungsart_WieOhne()
    {
        var p = Fallstraenge(6, 4);
        var (neu, alt) = MitTabellen(() => (SanitaerDimensionierung.Abfrage(p, null), SanitaerDimensionierung.Abfrage(p, null, merken: false)));
        var zeilen = neu["leitungen"]!.AsArray().OfType<JsonObject>().ToList();
        Assert.Equal(6 * (3 * 4 + 1), zeilen.Count);
        // nicht trivial: alle Leitungsarten kommen vor, und die abgewickelte Länge läuft stromab durch weitere Anschlussleitungen
        foreach (var art in new[] { "anschluss", "fall", "sammel" }) Assert.Contains(zeilen, x => (string?)x["art"] == art);
        Assert.Contains(zeilen, x => x["laenge_m"] is JsonNode l && l.GetValue<double>() > 1.2);   // Fallstrecke 0.4 m + waagrechte Anschlussleitung stromab
        Assert.Equal(alt.ToJsonString(), neu.ToJsonString());
    }
}
