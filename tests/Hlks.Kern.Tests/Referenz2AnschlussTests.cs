using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 2, Auftrag B6: unabhängige Referenzfälle von Hand – Anschlusslagen der Testfamilie (FIKTIVE Werte aus
/// tests/daten/familien/test_heizkoerper.familie.json) bei Einfügepunkt (2000, 1000, 500) und Drehung 0, 90, 180, 270, 45°.
///
/// Handrechnung. Typ 22-600-1000: laenge 1000, tiefe 100, randabstand 50, anschlussabstand 50, stutzen 30, bodenabstand 150.
///   Familienkoordinaten: vl = (laenge − randabstand, tiefe / 2, bodenabstand − stutzen) = (950, 50, 120),
///                        rl = (laenge − randabstand − anschlussabstand, tiefe / 2, …) = (900, 50, 120), beide Richtung (0, 0, −1).
///   Drehung θ im Gegenuhrzeigersinn um z, dann Verschiebung: x' = 2000 + x·cosθ − y·sinθ, y' = 1000 + x·sinθ + y·cosθ, z' = 500 + z.</summary>
public class Referenz2AnschlussTests
{
    static Projekt Platziert(double drehung, string typ = "22-600-1000", JsonObject? parameter = null)
    {
        var p = FamilieTestHilfe.MitFamilie();
        var par = new JsonObject
        {
            ["familie"] = "FAM-1", ["typ"] = typ, ["punkt"] = new JsonArray(2000, 1000, 500), ["drehung"] = drehung, ["kennzeichen"] = "HK-1",
        };
        if (parameter is not null) par["parameter"] = parameter;
        Ok(p.Ausfuehren("familie_platzieren", par));
        return p;
    }

    static JsonObject Lage(Projekt p, string anschluss, string element = "HK-1") =>
        ((JsonObject)Tuer.Abfrage(p, "anschlusslagen", new JsonObject { ["element"] = element }))["anschluesse"]!.AsArray()
            .Select(x => x!.AsObject()).Single(x => x["name"]!.GetValue<string>() == anschluss);

    static double[] Vek(JsonNode? n) => n!.AsArray().Select(x => x!.GetValue<double>()).ToArray();

    static void Nahe(double[] erwartet, double[] ist, double tol, string was)
    {
        Assert.Equal(erwartet.Length, ist.Length);
        for (int i = 0; i < erwartet.Length; i++)
            Assert.True(Math.Abs(erwartet[i] - ist[i]) <= tol, $"{was}[{i}]: erwartet {erwartet[i]}, erhalten {ist[i]}");
    }

    // ------------------------------------------------------------------ Drehung 0 … 270 und 45°

    [Theory]
    // θ = 0:   vl (2000 + 950, 1000 + 50) = (2950, 1050); rl (2000 + 900, 1050) = (2900, 1050)
    [InlineData(0, 2950, 1050, 2900, 1050)]
    // θ = 90:  cos 0, sin 1: x' = 2000 − y, y' = 1000 + x → vl (2000 − 50, 1000 + 950) = (1950, 1950); rl (1950, 1000 + 900) = (1950, 1900)
    [InlineData(90, 1950, 1950, 1950, 1900)]
    // θ = 180: cos −1, sin 0: x' = 2000 − x, y' = 1000 − y → vl (2000 − 950, 1000 − 50) = (1050, 950); rl (2000 − 900, 950) = (1100, 950)
    [InlineData(180, 1050, 950, 1100, 950)]
    // θ = 270: cos 0, sin −1: x' = 2000 + y, y' = 1000 − x → vl (2050, 1000 − 950) = (2050, 50); rl (2050, 1000 − 900) = (2050, 100)
    [InlineData(270, 2050, 50, 2050, 100)]
    // θ = 45:  cos = sin = 0.70710678
    //   vl: x' = 2000 + (950 − 50)·0.70710678 = 2000 + 636.396 = 2636.396; y' = 1000 + (950 + 50)·0.70710678 = 1000 + 707.107 = 1707.107
    //   rl: x' = 2000 + (900 − 50)·0.70710678 = 2000 + 601.041 = 2601.041; y' = 1000 + (900 + 50)·0.70710678 = 1000 + 671.751 = 1671.751
    [InlineData(45, 2636.396, 1707.107, 2601.041, 1671.751)]
    public void Lage_nach_Drehung_um_z(double drehung, double vlx, double vly, double rlx, double rly)
    {
        var p = Platziert(drehung);
        // z' = 500 + 120 = 620 in jedem Fall (Drehung um z ändert z nicht); Richtung (0, 0, −1) ebenso; die Lage ist auf 0.001 mm gerundet
        var vl = Lage(p, "vl"); var rl = Lage(p, "rl");
        Nahe([vlx, vly, 620], Vek(vl["lage"]), 6e-4, "vl.lage");
        Nahe([rlx, rly, 620], Vek(rl["lage"]), 6e-4, "rl.lage");
        Nahe([0, 0, -1], Vek(vl["richtung"]), 1e-9, "vl.richtung");
        Nahe([0, 0, -1], Vek(rl["richtung"]), 1e-9, "rl.richtung");
        // Kopf: Medium, Fluss, DN stehen in der Familie (dn 15)
        Assert.Equal("HZ-VL", vl["medium"]!.GetValue<string>());
        Assert.Equal("rein", vl["fluss"]!.GetValue<string>());
        Assert.Equal(15, vl["dn"]!.GetValue<double>());
        Assert.Equal("HZ-RL", rl["medium"]!.GetValue<string>());
        Assert.Equal("raus", rl["fluss"]!.GetValue<string>());
        Assert.Equal(15, rl["dn"]!.GetValue<double>());
    }

    // ------------------------------------------------------------------ Exemplarwert und Typwechsel

    [Fact]
    public void Exemplarwert_bodenabstand_300_hebt_die_Anschluesse()
    {
        // bodenabstand 300 statt 150: z_Familie = 300 − 30 = 270 → z' = 500 + 270 = 770; x, y unverändert
        // θ = 90: vl (1950, 1950, 770); rl (1950, 1900, 770)
        var p = Platziert(90, parameter: new JsonObject { ["bodenabstand"] = 300 });
        Nahe([1950, 1950, 770], Vek(Lage(p, "vl")["lage"]), 1e-9, "vl");
        Nahe([1950, 1900, 770], Vek(Lage(p, "rl")["lage"]), 1e-9, "rl");
        // θ = 0 mit demselben Exemplarwert: vl (2950, 1050, 770)
        var q = Platziert(0, parameter: new JsonObject { ["bodenabstand"] = 300 });
        Nahe([2950, 1050, 770], Vek(Lage(q, "vl")["lage"]), 1e-9, "vl 0°");
    }

    [Fact]
    public void Typwechsel_verschiebt_die_Anschluesse_mit_der_Laenge()
    {
        var p = Platziert(90);
        // Typ 22-900-800: laenge 800 → vl (800 − 50, 50, 120) = (750, 50, 120), rl (700, 50, 120)
        // θ = 90: x' = 2000 − 50 = 1950; y' = 1000 + 750 = 1750 bzw. 1000 + 700 = 1700 → vl (1950, 1750, 620); rl (1950, 1700, 620)
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "HK-1", ["eigenschaften"] = new JsonObject { ["familientyp"] = "22-900-800" } }));
        Nahe([1950, 1750, 620], Vek(Lage(p, "vl")["lage"]), 1e-9, "vl 22-900-800");
        Nahe([1950, 1700, 620], Vek(Lage(p, "rl")["lage"]), 1e-9, "rl 22-900-800");
        // Typ 22-600-1400: laenge 1400 → vl (1350, 50, 120): θ = 90 → (1950, 1000 + 1350, 620) = (1950, 2350, 620); rl (1300, 50) → (1950, 2300, 620)
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "HK-1", ["eigenschaften"] = new JsonObject { ["familientyp"] = "22-600-1400" } }));
        Nahe([1950, 2350, 620], Vek(Lage(p, "vl")["lage"]), 1e-9, "vl 22-600-1400");
        Nahe([1950, 2300, 620], Vek(Lage(p, "rl")["lage"]), 1e-9, "rl 22-600-1400");
    }

    [Fact]
    public void Exemplarwert_und_Typwechsel_zusammen()
    {
        // 22-900-800, bodenabstand 300, θ = 90: vl (1950, 1750, 300 − 30 + 500 = 770)
        var p = Platziert(90, "22-900-800", new JsonObject { ["bodenabstand"] = 300 });
        Nahe([1950, 1750, 770], Vek(Lage(p, "vl")["lage"]), 1e-9, "vl");
        Nahe([1950, 1700, 770], Vek(Lage(p, "rl")["lage"]), 1e-9, "rl");
    }

    // ------------------------------------------------------------------ Richtungen in der Ebene (eigene Familie mit waagrechten Anschlüssen)

    /// <summary>Familie «Richtungsprobe» (FIKTIV): Anschluss a bei (100, 0, 0) Richtung +x, Anschluss b bei (0, −50, 0) Richtung −y.</summary>
    static JsonObject Richtungsprobe() => FamilieTestHilfe.Mini(
        anschluesse: """
            [{ "name": "a", "medium": "*", "fluss": "rein", "art": "rohr", "form": "rund", "dn": 15, "lage": [100, 0, 0], "richtung": [1, 0, 0] },
             { "name": "b", "medium": "*", "fluss": "raus", "art": "rohr", "form": "rund", "dn": 15, "lage": [0, -50, 0], "richtung": [0, -1, 0] }]
            """);

    // Drehung θ: Lage a = (2000 + 100·cosθ, 1000 + 100·sinθ), b = (0, −50) → (2000 + 50·sinθ, 1000 − 50·cosθ); z = 500.
    // Richtung a = (cosθ, sinθ, 0); Richtung b = (0, −1) → (sinθ, −cosθ, 0).
    //   0°:   a (2100, 1000)        r (1, 0)       b (2000, 950)           r (0, −1)
    //   90°:  a (2000, 1100)        r (0, 1)       b (2050, 1000)          r (1, 0)
    //   180°: a (1900, 1000)        r (−1, 0)      b (2000, 1050)          r (0, 1)
    //   270°: a (2000, 900)         r (0, −1)      b (1950, 1000)          r (−1, 0)
    //   45°:  a (2070.711, 1070.711) r (0.7071, 0.7071)   b (2000 + 35.355, 1000 − 35.355) = (2035.355, 964.645)   r (0.7071, −0.7071)
    [Theory]
    [InlineData(0, 2100, 1000, 1, 0, 2000, 950, 0, -1)]
    [InlineData(90, 2000, 1100, 0, 1, 2050, 1000, 1, 0)]
    [InlineData(180, 1900, 1000, -1, 0, 2000, 1050, 0, 1)]
    [InlineData(270, 2000, 900, 0, -1, 1950, 1000, -1, 0)]
    [InlineData(45, 2070.711, 1070.711, 0.707106781, 0.707106781, 2035.355, 964.645, 0.707106781, -0.707106781)]
    public void Richtung_und_Lage_waagrechter_Anschluesse(double drehung, double ax, double ay, double adx, double ady, double bx, double by, double bdx, double bdy)
    {
        var p = Neu();
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Richtungsprobe() }));
        Ok(p.Ausfuehren("familie_platzieren", new JsonObject
        {
            ["familie"] = "FAM-1", ["typ"] = "A", ["punkt"] = new JsonArray(2000, 1000, 500), ["drehung"] = drehung, ["kennzeichen"] = "HK-2",
        }));
        var a = Lage(p, "a", "HK-2"); var b = Lage(p, "b", "HK-2");
        Nahe([ax, ay, 500], Vek(a["lage"]), 6e-4, "a.lage");
        Nahe([adx, ady, 0], Vek(a["richtung"]), 1e-8, "a.richtung");
        Nahe([bx, by, 500], Vek(b["lage"]), 6e-4, "b.lage");
        Nahe([bdx, bdy, 0], Vek(b["richtung"]), 1e-8, "b.richtung");
    }
}
