using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;
using static Hlks.Kern.Tests.RoutingHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3, B6, Referenzfall 4: Mengen – Materialliste und Leitungslängen eines kleinen Netzes, von Hand gerechnet
/// (FIKTIVE Testfamilien: da = DN + 5, Bogen r = 2·da, T-Stück l = lc = da).
///
/// Netz (Systeme KW = Trinkwasser kalt, WW = Trinkwasser warm; Stränge KW-S1, KW-S2 in KW, WW-S1 in WW):
///   KW-S1  S1A  DN 20 L-Zug (0,0,1000) → (3000,0,1000) → (3000,2000,1000): La 50 → 2950 + 1950 = 4900 mm, 1 Bogen 90°
///          S1B  DN 25 gerade 2000 mm
///   KW-S2  S2A  DN 25 45°-Versatz (0,8000) → (1000,8000) → (2000,9000) → (3000,9000): La = 60·tan 22.5° = 24.85281372;
///               1000 − La = 975.147186; 1000·√2 − 2·La = 1414.213562 − 49.705627 = 1364.507935; 975.147186 → 3314.802306 mm, 2 Bögen 45°
///          S2D  DN 20 gerade 3000 mm bei y = 12000, Abzweig DN 20 bei x = 1500 nach +y, 763 mm ab Achse: T-Stück l = lc = 25:
///               1475 + 1475 + (763 − 25 = 738) = 3688 mm, 1 T-Stück
///   WW-S1  W1   DN 25 gerade 1000 mm
/// Summen (m, auf 2 Stellen): KW-S1 DN 20 4.90, DN 25 2.00 · KW-S2 DN 20 3.688 → 3.69, DN 25 3.3148 → 3.31 ·
/// KW gesamt DN 20 8.588 → 8.59 (5 Geraden), DN 25 5.3148 → 5.31 (4 Geraden), total 13.9028 → 13.90 · WW DN 25 1.00.</summary>
public class Referenz3MengenTests
{
    const string SerieName = "Testrohrserie (fiktiv)";

    static Projekt Netz3()
    {
        var p = MitSerie();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "WW", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Trinkwasser warm" } }));
        foreach (var (kz, sys) in new[] { ("KW-S1", "KW"), ("KW-S2", "KW"), ("WW-S1", "WW") })
            Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Strang", ["kennzeichen"] = kz, ["teilVon"] = sys }));
        JsonObject Ex(string strang, string praefix) => new() { ["strang"] = strang, ["praefix"] = praefix };
        Ok(Zeichnen(p, "DN 20", [[0, 0, 1000], [3000, 0, 1000], [3000, 2000, 1000]], Ex("KW-S1", "S1A"), system: null));
        Ok(Zeichnen(p, "DN 25", [[0, 5000, 1000], [2000, 5000, 1000]], Ex("KW-S1", "S1B"), system: null));
        Ok(Zeichnen(p, "DN 25", [[0, 8000, 1000], [1000, 8000, 1000], [2000, 9000, 1000], [3000, 9000, 1000]], Ex("KW-S2", "S2A"), system: null));
        Ok(Zeichnen(p, "DN 20", [[0, 12000, 1000], [3000, 12000, 1000]], Ex("KW-S2", "S2D"), system: null));
        Ok(p.Ausfuehren("leitung_abzweigen", new JsonObject
        {
            ["leitung"] = "S2D-1", ["punkt"] = new JsonArray(1500, 12000, 1000), ["richtung"] = new JsonArray(0, 1, 0), ["laenge"] = 763, ["groesse"] = "DN 20", ["praefix"] = "S2E",
        }));
        Ok(Zeichnen(p, "DN 25", [[0, 15000, 1000], [1000, 15000, 1000]], Ex("WW-S1", "W1"), system: null));
        return p;
    }

    static List<(string Serie, string Groesse, double M)> Laengen(Projekt p, string? strang = null, string? system = null) =>
        Ableitungen.Leitungslaengen(p, strang, system).Select(x => (x.Serie, x.Groesse, x.LaengeM)).ToList();

    static double Zahl(JsonNode? n) => Json.Zahl(n) ?? throw new Xunit.Sdk.XunitException("keine Zahl: " + n?.ToJsonString());

    [Fact]
    public void Netz_GeradenStimmenMitDerHandrechnung()
    {
        var p = Netz3();
        Etwa(2950, Laenge(p, "S1A-1"), 1e-3); Etwa(1950, Laenge(p, "S1A-2"), 1e-3);
        Etwa(2000, Laenge(p, "S1B-1"), 1e-3);
        Etwa(975.147186, Laenge(p, "S2A-1"), 2e-3); Etwa(1364.507935, Laenge(p, "S2A-2"), 2e-3); Etwa(975.147186, Laenge(p, "S2A-3"), 2e-3);
        Etwa(1475, Laenge(p, "S2D-1"), 1e-3); Etwa(1475, Laenge(p, "S2D-1-2"), 1e-3); Etwa(738, Laenge(p, "S2E-1"), 1e-3);
        Etwa(1000, Laenge(p, "W1-1"), 1e-3);
    }

    [Fact]
    public void Leitungslaengen_JeSerieUndGroesse_AufZweiStellen()
    {
        var p = Netz3();
        // alles: DN 20 = 4900 + 3688 = 8588 → 8.59; DN 25 = 2000 + 3314.802306 + 1000 = 6314.802 → 6.31
        // B9b (Entscheid Christian 19.10.2026): Rohre ausser Abwasser werden mit NW (Vorgabe, nicht Typname) bezeichnet, in Listen und Exporten ebenso
        Assert.Equal([(SerieName, "NW 20", 8.59), (SerieName, "NW 25", 6.31)], Laengen(p));
        // KW: DN 25 = 2000 + 3314.802306 = 5314.802 → 5.31
        Assert.Equal([(SerieName, "NW 20", 8.59), (SerieName, "NW 25", 5.31)], Laengen(p, system: "KW"));
        Assert.Equal([(SerieName, "NW 25", 1.00)], Laengen(p, system: "WW"));
    }

    [Fact]
    public void Leitungslaengen_FilterJeStrang()
    {
        var p = Netz3();
        Assert.Equal([(SerieName, "NW 20", 4.90), (SerieName, "NW 25", 2.00)], Laengen(p, strang: "KW-S1"));
        Assert.Equal([(SerieName, "NW 20", 3.69), (SerieName, "NW 25", 3.31)], Laengen(p, strang: "KW-S2"));   // 3.688 / 3.3148
        Assert.Equal([(SerieName, "NW 25", 1.00)], Laengen(p, strang: "WW-S1"));
        // Strang und System zusammen
        Assert.Equal([(SerieName, "NW 20", 4.90), (SerieName, "NW 25", 2.00)], Laengen(p, strang: "KW-S1", system: "KW"));
        Assert.Empty(Laengen(p, strang: "KW-S1", system: "WW"));
    }

    [Fact]
    public void Materialliste_Gesamt_LeitungenStueckUndFormteile()
    {
        var p = Netz3();
        var m = Ableitungen.Materialliste(p, system: "KW");
        var l = m["leitungen"]!.AsArray();
        Assert.Equal(2, l.Count);
        var dn20 = l.Single(x => x!["groesse"]!.GetValue<string>() == "NW 20")!;
        var dn25 = l.Single(x => x!["groesse"]!.GetValue<string>() == "NW 25")!;
        Assert.Equal(SerieName, dn20["serie"]!.GetValue<string>());
        Etwa(8.59, Zahl(dn20["laenge_m"]), 1e-9); Assert.Equal(5, (int)Zahl(dn20["stueck"]));    // S1A-1, S1A-2, S2D-1, S2D-1-2, S2E-1
        Etwa(5.31, Zahl(dn25["laenge_m"]), 1e-9); Assert.Equal(4, (int)Zahl(dn25["stueck"]));    // S1B-1, S2A-1, S2A-2, S2A-3
        Etwa(13.90, Zahl(m["leitungslaengeTotal_m"]), 1e-9);   // 4900 + 2000 + 3314.802 + 3688 = 13902.8 mm
        // Formteile: 1 × Bogen DN 20 90°, 2 × Bogen DN 25 45°, 1 × T-Stück DN 20
        var f = m["formteile"]!.AsArray();
        Assert.Equal(4, (int)Zahl(m["formteileTotal"]));
        var b20 = f.Single(x => x!["klasse"]!.GetValue<string>() == "Bogen" && x["typ"]!.GetValue<string>() == "DN 20")!;
        Etwa(90, Zahl(b20["winkel"]), 1e-6); Assert.Equal(1, (int)Zahl(b20["stueck"]));
        var b25 = f.Single(x => x!["klasse"]!.GetValue<string>() == "Bogen" && x["typ"]!.GetValue<string>() == "DN 25")!;
        Etwa(45, Zahl(b25["winkel"]), 1e-6); Assert.Equal(2, (int)Zahl(b25["stueck"]));
        var t = f.Single(x => x!["klasse"]!.GetValue<string>() == "TStueck")!;
        Assert.Equal("DN 20", t["typ"]!.GetValue<string>()); Assert.Equal(1, (int)Zahl(t["stueck"]));
        Assert.Equal(3, f.Count);
    }

    [Fact]
    public void Materialliste_FilterJeStrang()
    {
        var p = Netz3();
        // KW-S1: DN 20 4.90 (2 Stück), DN 25 2.00 (1); 1 Bogen DN 20 90°
        var s1 = Ableitungen.Materialliste(p, strang: "KW-S1");
        var l1 = s1["leitungen"]!.AsArray();
        Assert.Equal(2, l1.Count);
        Etwa(4.90, Zahl(l1[0]!["laenge_m"]), 1e-9); Assert.Equal(2, (int)Zahl(l1[0]!["stueck"]));
        Etwa(2.00, Zahl(l1[1]!["laenge_m"]), 1e-9); Assert.Equal(1, (int)Zahl(l1[1]!["stueck"]));
        Etwa(6.90, Zahl(s1["leitungslaengeTotal_m"]), 1e-9);
        Assert.Equal(1, (int)Zahl(s1["formteileTotal"]));
        var b = Assert.Single(s1["formteile"]!.AsArray())!;
        Assert.Equal("Bogen", b["klasse"]!.GetValue<string>()); Assert.Equal("DN 20", b["typ"]!.GetValue<string>()); Etwa(90, Zahl(b["winkel"]), 1e-6);
        // KW-S2: DN 20 3.69 (3 Stück), DN 25 3.31 (3); 2 Bögen 45° DN 25 + 1 T-Stück DN 20
        var s2 = Ableitungen.Materialliste(p, strang: "KW-S2");
        var l2 = s2["leitungen"]!.AsArray();
        Etwa(3.69, Zahl(l2.Single(x => x!["groesse"]!.GetValue<string>() == "NW 20")!["laenge_m"]), 1e-9);
        Assert.Equal(3, (int)Zahl(l2.Single(x => x!["groesse"]!.GetValue<string>() == "NW 20")!["stueck"]));
        Etwa(3.31, Zahl(l2.Single(x => x!["groesse"]!.GetValue<string>() == "NW 25")!["laenge_m"]), 1e-9);
        Assert.Equal(3, (int)Zahl(l2.Single(x => x!["groesse"]!.GetValue<string>() == "NW 25")!["stueck"]));
        Assert.Equal(3, (int)Zahl(s2["formteileTotal"]));
        Assert.Equal(2, (int)Zahl(s2["formteile"]!.AsArray().Single(x => x!["klasse"]!.GetValue<string>() == "Bogen")!["stueck"]));
        // WW-S1: nur 1.00 m DN 25, keine Formteile
        var w = Ableitungen.Materialliste(p, strang: "WW-S1");
        Etwa(1.00, Zahl(w["leitungen"]![0]!["laenge_m"]), 1e-9);
        Assert.Equal(0, (int)Zahl(w["formteileTotal"]));
        // falsches Filterkennzeichen: abgelehnt
        Assert.Throws<HlksFehler>(() => Ableitungen.Materialliste(p, strang: "KW"));
    }

    [Fact]
    public void Strangliste_LaengenJeStrang_StimmenMitLeitungslaengen()
    {
        var p = Netz3();
        var zeilen = Ableitungen.Strangliste(p)["zeilen"]!.AsArray();
        var s1 = zeilen.Single(z => z!["strang"]!.GetValue<string>() == "KW-S1")!;
        var laengen = s1["laengen"]!.AsArray();
        Assert.Equal(2, laengen.Count);
        Assert.Equal("NW 20", laengen[0]!["groesse"]!.GetValue<string>()); Etwa(4.90, Zahl(laengen[0]!["laenge_m"]), 1e-9);
        Assert.Equal("NW 25", laengen[1]!["groesse"]!.GetValue<string>()); Etwa(2.00, Zahl(laengen[1]!["laenge_m"]), 1e-9);
        Etwa(6.90, Zahl(s1["leitungslaenge_m"]), 1e-9);
        var s2 = zeilen.Single(z => z!["strang"]!.GetValue<string>() == "KW-S2")!;
        Etwa(7.00, Zahl(s2["leitungslaenge_m"]), 1e-9);   // 3688 + 3314.802 = 7002.8 mm
    }
}
