using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.SanitaerHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 4 Teil A-K, Punkt 7: Beispielprojekt «MFH Musterweg» (fiktiv) – Summen plausibel (Handrechnung mit den Katalogwerten) und
/// stabil (zwei Erzeugungen, gleiche Antworten); dazu das Abnahmeszenario aus Modul 3 mit Trinkwasser warm über den Wassererwärmer.
/// Handrechnung (Katalog): WC wc_75 1/–/2, Waschtisch 1/1/0.5, Badewanne 3/3/0.8, Spültisch spuelbecken 2/2/0.8, Dusche 2/2/0.6,
/// Waschmaschine waschmaschine_6 2/–/0.8, Bodenablauf bodenablauf_70 (im Beispiel gesetzt) –/–/1.5.</summary>
public class SanitaerBeispielTests
{
    static readonly Projekt B = Beispielprojekt.Erzeugen(Kat, null);

    [Fact]
    public void Zusammenstellung_HandrechnungDerSechzehnApparate()
    {
        var z = Abfrage(B, "sanitaer_zusammenstellung", new JsonObject { ["anlage"] = "A-SAN" });
        Assert.Equal(16, z["apparate"]!.GetValue<int>());
        Etwa(4 * 1 + 2 * 2 + 2 * 3 + 2 * 2 + 2 + 4 * 1, Zahl(z["summeLuKw"]), "ΣLU KW = 24");
        Etwa(4 * 1 + 2 * 2 + 2 * 3 + 2 * 2, Zahl(z["summeLuWw"]), "ΣLU WW = 18");
        Etwa(4 * 0.5 + 2 * 0.6 + 2 * 0.8 + 2 * 0.8 + 0.8 + 1.5 + 4 * 2, Zahl(z["summeDu"]), "ΣDU = 16.7");
        Etwa(42, Zahl(z["totalLu"]), "Total LU");
        Etwa(4.2, Zahl(z["volumenstrom"]), "Volumenstrom");
        Etwa(3, Zahl(z["groessterLu"]), "grösster Einzel-LU (Badewanne)");
        Assert.Equal(3, z["massgebendeKurve"]!.GetValue<int>());
        Etwa(Math.Pow(4.2, 0.353) * 0.459, Zahl(z["spitzendurchfluss"]), "Spitze Kurve 3");
        Etwa(0.5 * Math.Sqrt(16.7), Zahl(z["schmutzwasser"]), "Q_WW (K fiktiv wie Walkringen)");
        Assert.Contains("fiktiv", z["abflusskennzahlHerkunft"]!.GetValue<string>());
        Assert.Equal(new[] { "UG", "EG", "1.OG" }, z["geschosse"]!.AsArray().Select(g => g!["kennzeichen"]!.GetValue<string>()));
        Assert.Equal(new[] { "waschtisch", "dusche", "badewanne", "spuelbecken", "waschmaschine_6", "bodenablauf_70", "wc_75" },
            z["zeilen"]!.AsArray().Select(r => r!["art"]!.GetValue<string>()));
        var wc = z["zeilen"]!.AsArray().Single(r => r!["art"]!.GetValue<string>() == "wc_75")!;
        Assert.Equal(new[] { 0, 2, 2 }, wc["jeGeschoss"]!.AsArray().Select(x => x!.GetValue<int>()));
        // Hinweise: ΣLU WW zählt die Warmwasser-Apparate (Stückzahl × Wert wie das Excel), das Beispiel hat aber kein Warmwassernetz (Prüfbericht 4A B7);
        // die Kurvenwahl ist nicht im Excel und nicht bestätigt (B3). 4.2 l/s liegt in beiden Kurvenbereichen.
        var h = z["hinweise"]!.AsArray().Select(x => x!.GetValue<string>()).ToList();
        Assert.Equal(2, h.Count);
        Assert.StartsWith("ΣLU WW enthält 10 Apparate ohne WW-Verbindung: ", h[0]);
        Assert.StartsWith("Kurvenwahl nicht im Excel, aus HKAP, nicht bestätigt", h[1]);
        Assert.Empty(z["ohneArt"]!.AsArray());
    }

    [Theory]
    [InlineData("KW-V1-0", 24, 15)]   // Hausanschluss: alle Kaltwasser-Apparate (ohne Bodenablauf)
    [InlineData("KW-V1-3", 22, 14)]   // nach dem Abzweig zur Waschmaschine
    [InlineData("KW-U.02-WM", 2, 1)]
    [InlineData("KW-V1-4", 8, 6)]     // zum Steigstrang Dusche/WC: 2 × (WC 1 + WT 1 + DU 2)
    [InlineData("KW-S1-1", 14, 8)]    // Steigstrang Bad/Küche: 2 × (BW 3 + WT 1 + WC 1 + SP 2)
    [InlineData("KW-S1-3", 7, 4)]     // über Bad EG: Bad 1.OG 5 + Küche 1.OG 2
    [InlineData("KW-1.02-1", 5, 3)]
    public void Netz_Kaltwasser_Handrechnung(string leitung, double kw, int apparate)
    {
        var b = Belastung(B, leitung);
        Etwa(kw, Zahl(b["summeLuKw"]), leitung);
        Etwa(0, Zahl(b["summeLuWw"]), leitung + " (kein Warmwasser im Beispiel)");
        Assert.Equal(apparate, b["apparate"]!.GetValue<int>());
        Assert.False(b["masche"]!.GetValue<bool>());
    }

    [Theory]
    [InlineData("SW-G1-1", 16.7, 16)]   // Grundleitung zum Kontrollschacht: alles
    [InlineData("SW-G1-2", 2.3, 2)]     // Waschküche: WM 0.8 + BA 1.5
    [InlineData("SW-S1-2", 14.4, 14)]   // Sammelleitung nach F2: F1 8.2 + F2 6.2
    [InlineData("SW-F1-4", 8.2, 8)]     // 2 × (BW 0.8 + WT 0.5 + WC 2 + SP 0.8)
    [InlineData("SW-F2-2", 6.2, 6)]     // 2 × (DU 0.6 + WT 0.5 + WC 2)
    [InlineData("SW-1.02-2", 3.3, 3)]
    public void Netz_Schmutzwasser_Handrechnung(string leitung, double du, int apparate)
    {
        var b = Belastung(B, leitung);
        Etwa(du, Zahl(b["summeDu"]), leitung);
        Assert.Equal(apparate, b["apparate"]!.GetValue<int>());
    }

    [Fact]
    public void Straenge_Anlage_PassenZurZusammenstellung_OhneSanitaerbefund()
    {
        var a = Abfrage(B, "sanitaer_belastung", new JsonObject { ["anlage"] = "A-SAN" })["anlagen"]![0]!;
        var z = Abfrage(B, "sanitaer_zusammenstellung", new JsonObject { ["anlage"] = "A-SAN" });
        Etwa(Zahl(z["summeLuKw"]), Zahl(a["trinkwasser"]!["summeLuKw"]), "Anlage ΣLU KW = Zusammenstellung");
        Etwa(Zahl(z["summeDu"]), Zahl(a["abwasser"]!["summeDu"]), "Anlage ΣDU = Zusammenstellung");
        var strang = a["systeme"]!.AsArray().SelectMany(s => s!["straenge"]!.AsArray()).ToDictionary(s => s!["strang"]!.GetValue<string>(), s => s!);
        foreach (var (kz, wert) in new[] { ("KW-V1", 24.0), ("KW-S1", 14.0), ("KW-S2", 8.0) }) Etwa(wert, Zahl(strang[kz]["summeLuKw"]), kz);
        foreach (var (kz, wert) in new[] { ("SW-F1", 8.2), ("SW-F2", 6.2), ("SW-S1", 14.4), ("SW-G1", 16.7) }) Etwa(wert, Zahl(strang[kz]["summeDu"]), kz);
        Assert.True(Sanitaerbefunde(B).Count == 0, Liste(Sanitaerbefunde(B)));
        // jede Leitung des Beispiels hat Summen (Kalt- und Schmutzwasser)
        Assert.Equal(B.Modell.DerKlasse("Rohr").Count(r => Ableitungen.Gewerk(B.Modell, B.Katalog, r) == "sanitaer"), a["systeme"]!.AsArray().Sum(s => s!["leitungen"]!.AsArray().Count));   // (der Kältekreis des Beispiels gehört zur Heizung)
    }

    [Fact]
    public void Stabil_ZweiErzeugungenGleicheAntworten()
    {
        var b2 = Beispielprojekt.Erzeugen(Kat, null);
        static string Ohne(JsonObject o) { o.Remove("abflusskennzahlHerkunft"); return o.ToJsonString(); }   // enthält die Zeit der Erzeugung
        Assert.Equal(Abfrage(B, "sanitaer_belastung").ToJsonString(), Abfrage(b2, "sanitaer_belastung").ToJsonString());
        Assert.Equal(Ohne(Abfrage(B, "sanitaer_zusammenstellung")), Ohne(Abfrage(b2, "sanitaer_zusammenstellung")));
        // wiederholte Abfrage aus dem Zwischenspeicher: gleich
        Assert.Equal(Abfrage(B, "sanitaer_belastung").ToJsonString(), Abfrage(B, "sanitaer_belastung").ToJsonString());
    }

    [Fact]
    public void ElementLesen_ApparatUndLeitungDesBeispiels()
    {
        var wt = (JsonObject)Tuer.Abfrage(B, "element_lesen", new JsonObject { ["element"] = "WT-E.02" });
        Assert.Equal("waschtisch", wt["sanitaer"]!["art"]!.GetValue<string>());
        Assert.Equal(new[] { "A-SAN" }, wt["sanitaer"]!["anlagen"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Equal(0.5, Zahl(wt["abgeleitet"]!["du"]));
        var ba = (JsonObject)Tuer.Abfrage(B, "element_lesen", new JsonObject { ["element"] = "BA-U.02" });
        Assert.Equal("eigenschaft", ba["sanitaer"]!["woher"]!.GetValue<string>());
        Assert.Equal(1.5, Zahl(ba["abgeleitet"]!["du"]));
        var r = (JsonObject)Tuer.Abfrage(B, "element_lesen", new JsonObject { ["element"] = "KW-V1-0" });
        Assert.Equal(24, Zahl(r["abgeleitet"]!["summeLuKw"]));
    }

    [Fact]
    public void Abnahmeszenario3_WarmwasserUeberDenWassererwaermer()
    {
        // Modul 3: Strang Dusche/WC neu mit Serien gezeichnet, dazu Trinkwasser warm WW-S2 ab WE-1 (gespeist aus KW-S2)
        var s = new Abnahme3Szenario();
        var p = s.P;
        var a = Abfrage(p, "sanitaer_belastung", new JsonObject { ["anlage"] = "A-SAN" })["anlagen"]![0]!;
        var ww = a["systeme"]!.AsArray().Single(x => x!["system"]!.GetValue<string>() == "WW")!;
        // am Warmwasser hängen die Waschtische und Duschen *.03 (EG, 1.OG): 2 × (WT 1 + DU 2)
        Etwa(6, Zahl(ww["summeLuWw"]), "System WW ΣLU WW");
        Etwa(0, Zahl(ww["summeLuKw"]), "System WW ΣLU KW");
        // der Hausanschluss trägt Kalt- und Warmwasser (über WE-1)
        var ha = Belastung(p, "KW-V1-0");
        Etwa(24, Zahl(ha["summeLuKw"]), "KW-V1-0 ΣLU KW");
        Etwa(6, Zahl(ha["summeLuWw"]), "KW-V1-0 ΣLU WW");
        Assert.False(ha["masche"]!.GetValue<bool>());
        Etwa(16.7, Zahl(Belastung(p, "SW-G1-1")["summeDu"]), "Schmutzwasser unverändert");
        var andere = Sanitaerbefunde(p).Where(b => b.Code is "P21" or "P23" or "P24" or "P25" or "P26").ToList();
        Assert.True(andere.Count == 0, Liste(andere));
        // Das Projekt hat jetzt Warmwasser: Waschtische, Badewannen, Spültische *.02/*.01 haben LU WW, ihr WW-Anschluss ist offen → P22
        var p22 = Sanitaerbefunde(p).Where(b => b.Code == "P22").Select(b => b.Element).Order().ToList();
        Assert.Equal(new[] { "BW-1.02", "BW-E.02", "SP-1.01", "SP-E.01", "WT-1.02", "WT-E.02" }, p22);
    }
}
