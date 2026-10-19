using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;
using static Hlks.Kern.Tests.RoutingHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3, B6, Referenzfall 3: Gefälle – von Hand gerechnet (FIKTIVE Testfamilien; System SW = Schmutzwasser, Schwerkraft).
/// Regel (Spezifikation B2): waagrecht gezeichnete Stücke fallen ab dem ersten Punkt um g % der waagrechten Länge, senkrechte behalten
/// ihre Länge; Bögen sitzen im Knotenpunkt, die Geraden enden um La entlang ihrer eigenen Richtung davor/danach.
/// DN 50: da 55, r = 110, 90° → La = Lb = 110 mm.
/// Winkeltoleranz ±0.5° (dokumentierte Annahme B2): senkrecht ↔ Gefälle g knickt um 90° − arctan(g); arctan(g) ≤ 0.5° ⇔
/// g ≤ tan 0.5° = 0.0087269 = 0.8727 %.</summary>
public class Referenz3GefaelleTests
{
    const double Tol = 1e-3;

    static Projekt Sw()
    {
        var p = MitSerie();
        MitSystem(p, "SW", "Schmutzwasser");
        return p;
    }

    static BefehlsErgebnis ZeichnenSw(Projekt p, double gefaelle, params double[][] punkte) =>
        Zeichnen(p, "DN 50", punkte, new JsonObject { ["gefaelle"] = gefaelle }, system: "SW");

    // ------------------------------------------------------------------ einzelne Gerade

    /// <summary>1 % über 4000 mm: Δz = 0.01·4000 = 40; 2 % über 2500: 50; 3 % über 1500: 45; Ende bei z0 − Δz.
    /// Länge der geneigten Achse = √(L² + Δz²).</summary>
    [Theory]
    [InlineData(1.0, 4000.0, 3000.0, 40.0)]
    [InlineData(2.0, 2500.0, 3000.0, 50.0)]
    [InlineData(3.0, 1500.0, 3000.0, 45.0)]
    [InlineData(1.0, 12000.0, 2500.0, 120.0)]   // 0.01·12000 = 120
    [InlineData(2.0, 800.0, 2500.0, 16.0)]      // 0.02·800 = 16
    public void EinzelneGerade_DeltaZ_UndEndhoehe(double g, double l, double z0, double dz)
    {
        var p = Sw();
        Ok(ZeichnenSw(p, g, [0, 0, z0], [l, 0, z0]));
        Gerade(p, "RO-1", [0, 0, z0], [l, 0, z0 - dz], Tol);
        Etwa(Math.Sqrt(l * l + dz * dz), Laenge(p, "RO-1"), Tol);
        Assert.Equal(g, Hole(p, "RO-1").Zahl("gefaelle"));
        Assert.Empty(Befunde(p, "P16"));
    }

    // ------------------------------------------------------------------ Knick in der Waagrechten

    /// <summary>2 %, L-Zug (0,0,2000) → (3000,0,2000) → (3000,2000,2000): Knoten z1 = 2000 − 0.02·3000 = 1940, Ende z2 = 1940 − 0.02·2000 = 1900.
    /// Richtungen d1 = (3000, 0, −60)/3000.59995, d2 = (0, 2000, −40)/2000.39998; Bogen DN 50 (La = 110) im Knoten (3000, 0, 1940).
    /// RO-1 endet bei P1 − 110·d1 = (3000 − 109.978, 0, 1940 + 2.19956) = (2890.0220, 0, 1942.19956) → Δz 57.80044 auf 2890.022 waagrecht = 2.000 %.
    /// Der Bogen ist ein starrer 90°-Bogen (Winkel aus der Serienliste, der Knick von 89.977° liegt innerhalb ±0.5°): sein Anschluss b liegt im
    /// Bogen genau 90° zu d1, also bei P1 + 110·y mit y = (d2 − (d1·d2)·d1)/|…| (y ⟂ d1 in der Ebene d1/d2) – nicht bei P1 + 110·d2.
    /// d1·d2 = (−60)(−40)/(3000.59995·2000.39998) = 0.00039988; y = (−0.00039972, 0.99980, −0.019988) → b = (3000 − 0.04397, 109.978,
    /// 1940 − 2.1987 = 1937.8013). Das Ende von RO-2 bleibt P2 = (3000, 2000, 1900); RO-2 weicht damit um etwa 0.00005 %-Punkte vom Sollgefälle ab.</summary>
    [Fact]
    public void LZug_2Prozent_KnotenhoehenUndDeltaZJeStueck()
    {
        var p = Sw();
        var e = Ok(ZeichnenSw(p, 2, [0, 0, 2000], [3000, 0, 2000], [3000, 2000, 2000]));
        Etwa([3000, 0, 1940], Familien.Einfuegepunkt(Hole(p, "BO-1")), 1e-6);
        Etwa(1900, Pkt(p, "RO-2")[1][2], 1e-6);
        double l1 = Math.Sqrt(3000.0 * 3000 + 60 * 60), l2 = Math.Sqrt(2000.0 * 2000 + 40 * 40);
        double[] d1 = [3000 / l1, 0, -60 / l1], d2 = [0, 2000 / l2, -40 / l2];
        double dp = d1[0] * d2[0] + d1[1] * d2[1] + d1[2] * d2[2];
        double[] y = [d2[0] - dp * d1[0], d2[1] - dp * d1[1], d2[2] - dp * d1[2]];
        double yl = Math.Sqrt(y[0] * y[0] + y[1] * y[1] + y[2] * y[2]);
        Gerade(p, "RO-1", [0, 0, 2000], [3000 - 110 * d1[0], 0, 1940 + 110 * 60 / l1], Tol);
        Gerade(p, "RO-2", [3000 + 110 * y[0] / yl, 110 * y[1] / yl, 1940 + 110 * y[2] / yl], [3000, 2000, 1900], Tol);
        Etwa(2890.0220, Pkt(p, "RO-1")[1][0], 1e-3);
        Etwa(1942.19956, Pkt(p, "RO-1")[1][2], 1e-3);
        Etwa(1937.8013, Pkt(p, "RO-2")[0][2], 2e-3);
        Etwa(2000 - 1942.19956, 2000 - Pkt(p, "RO-1")[1][2], 1e-3);   // Δz RO-1 = 57.80044
        // Gefälle je Stück: Δz / waagrechte Länge = 2.000 %
        Etwa(0.02, (2000 - Pkt(p, "RO-1")[1][2]) / Pkt(p, "RO-1")[1][0], 1e-7, "Gefälle RO-1");
        Etwa(0.02, (Pkt(p, "RO-2")[0][2] - Pkt(p, "RO-2")[1][2]) / (2000 - Pkt(p, "RO-2")[0][1]), 1e-5, "Gefälle RO-2");
        Assert.Equal(2, Hole(p, "RO-1").Zahl("gefaelle")); Assert.Equal(2, Hole(p, "RO-2").Zahl("gefaelle"));
        // Knick: cos = (0.02·0.02)/(1.0004) → 89.977° (innerhalb ±0.5°): Bogen 90°
        Assert.Equal(90, Leitungspruefungen.Bogenwinkel(p.Modell, p.Katalog, Hole(p, "BO-1"))!.Value, 4);
        Assert.Empty(Befunde(p, "P16"));
        Sauber(p);
    }

    /// <summary>1 %, Z-Zug (0,0,3000) → (2000,0,3000) → (2000,1500,3000) → (4500,1500,3000): Knoten z1 = 3000 − 0.01·2000 = 2980,
    /// z2 = 2980 − 0.01·1500 = 2965, Ende z3 = 2965 − 0.01·2500 = 2940 (gesamt 0.01·6000 = 60 mm).</summary>
    [Fact]
    public void ZZug_1Prozent_ZweiKnickeKnotenhoehen()
    {
        var p = Sw();
        Ok(ZeichnenSw(p, 1, [0, 0, 3000], [2000, 0, 3000], [2000, 1500, 3000], [4500, 1500, 3000]));
        Etwa([2000, 0, 2980], Familien.Einfuegepunkt(Hole(p, "BO-1")), 1e-6);
        Etwa([2000, 1500, 2965], Familien.Einfuegepunkt(Hole(p, "BO-2")), 1e-6);
        Etwa(2940, Pkt(p, "RO-3")[1][2], 1e-6);
        Etwa(4500, Pkt(p, "RO-3")[1][0], 1e-6);
        Assert.Equal(60, 3000 - Pkt(p, "RO-3")[1][2], 6);
        foreach (var kz in new[] { "RO-1", "RO-2", "RO-3" }) Assert.Equal(1, Hole(p, kz).Zahl("gefaelle"));
        Assert.Empty(Befunde(p, "P16"));
        Sauber(p);
    }

    /// <summary>3 %, L-Zug (0,0,3000) → (1500,0,3000) → (1500,1000,3000): z1 = 3000 − 45 = 2955, Ende z2 = 2955 − 30 = 2925.
    /// Knick der beiden geneigten Richtungen: cos = 0.03²/(1 + 0.03²) = 0.000899 → 89.948° (innerhalb ±0.5°).</summary>
    [Fact]
    public void LZug_3Prozent()
    {
        var p = Sw();
        Ok(ZeichnenSw(p, 3, [0, 0, 3000], [1500, 0, 3000], [1500, 1000, 3000]));
        Etwa([1500, 0, 2955], Familien.Einfuegepunkt(Hole(p, "BO-1")), 1e-6);
        Etwa(2925, Pkt(p, "RO-2")[1][2], 1e-6);
        Assert.Empty(Befunde(p, "P16"));
        Sauber(p);
    }

    // ------------------------------------------------------------------ Winkeltoleranz ±1° (Entscheid Hauptlauf 19.10.2026; B2 hatte ±0.5°)

    /// <summary>Senkrecht (0,0,3000) → (0,0,1000), dann waagrecht 3000 mm mit g %. Knick = 90° − arctan(g):
    /// 0.5 % → 0.2865°, 0.87 % → 0.4985°, 1 % → 0.5729°, 1.5 % → 0.8593° (angenommen, Grenze tan 1° = 1.7455 %);
    /// 1.75 % → 1.0026°, 2 % → 1.1458°, 3 % → 1.7184° (abgelehnt).
    /// Das senkrechte Stück behält seine Länge (Knoten bei z = 1000), das waagrechte fällt: Ende z = 1000 − g·3000.</summary>
    [Theory]
    [InlineData(0.5, 985.0)]    // 1000 − 0.005·3000 = 985
    [InlineData(0.87, 973.9)]   // 1000 − 0.0087·3000 = 973.9
    [InlineData(1.0, 970.0)]    // 1000 − 0.01·3000 = 970
    [InlineData(1.5, 955.0)]    // 1000 − 0.015·3000 = 955 (arctan 0.015 = 0.859°; direkt an der Grenze 1° lehnt verbinden wegen Rundung ab)
    public void SenkrechtZuGefaelle_BisZurToleranzAngenommen(double g, double zEnde)
    {
        var p = Sw();
        Ok(ZeichnenSw(p, g, [0, 0, 3000], [0, 0, 1000], [3000, 0, 1000]));
        Etwa([0, 0, 1000], Familien.Einfuegepunkt(Hole(p, "BO-1")), 1e-6);
        Etwa(zEnde, Pkt(p, "RO-2")[1][2], 1e-6);
        Assert.Equal(90, Leitungspruefungen.Bogenwinkel(p.Modell, p.Katalog, Hole(p, "BO-1"))!.Value, 4);
        Assert.Null(Hole(p, "RO-1").Zahl("gefaelle"));
        Assert.Empty(Befunde(p, "P16"));
    }

    [Theory]
    [InlineData(1.75)]   // arctan 0.0175 = 1.0026° > 1°
    [InlineData(2.0)]    // 1.1458°
    [InlineData(3.0)]    // 1.7184°
    public void SenkrechtZuGefaelle_UeberDerToleranzAbgelehnt(double g)
    {
        var p = Sw();
        var vorher = Inhalt(p);
        var e = Nein(ZeichnenSw(p, g, [0, 0, 3000], [0, 0, 1000], [3000, 0, 1000]));
        Assert.Contains("Knick", e.Fehler);
        Assert.Equal(vorher, Inhalt(p));
    }

    /// <summary>Gleiches mit der Reihenfolge waagrecht (fallend) → senkrecht abwärts: Knick 90° − arctan(g) am Ende des geneigten Stücks.</summary>
    [Theory]
    [InlineData(0.87, true)]
    [InlineData(1.75, false)]   // arctan 0.0175 = 1.0026° > 1°
    public void GefaelleZuSenkrecht_Toleranz(double g, bool angenommen)
    {
        var p = Sw();
        var r = ZeichnenSw(p, g, [0, 0, 3000], [2000, 0, 3000], [2000, 0, 1000]);
        if (angenommen)
        {
            Ok(r);
            Etwa([2000, 0, 2982.6], Familien.Einfuegepunkt(Hole(p, "BO-1")), 1e-6);   // 0.0087·2000 = 17.4 → 3000 − 17.4 = 2982.6
            Etwa(982.6, Pkt(p, "RO-2")[1][2], 1e-6);   // senkrechtes Stück behält seine Länge 2000: 2982.6 − 2000
        }
        else Assert.Contains("Knick", Nein(r).Fehler);
    }

    // ------------------------------------------------------------------ P16

    static BefehlsErgebnis SwRohr(Projekt p, string kz, double? gefaelle, double zAnfang, double zEnde, double laenge, string system = "SW")
    {
        var eig = new JsonObject { ["familientyp"] = "DN 50" };
        if (gefaelle is double g) eig["gefaelle"] = g;
        return p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Rohr", ["kennzeichen"] = kz, ["system"] = system, ["geschoss"] = "EG",
            ["eigenschaften"] = eig, ["bezuege"] = new JsonObject { ["familie"] = "FAM-1" },
            ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Pts([0, 0, zAnfang], [laenge, 0, zEnde]) },
        });
    }

    /// <summary>Soll 2 % über 2000 mm waagrecht = 40 mm. 40 mm → 2.00 % (kein P16). 39 mm → 1.95 %, Abweichung 0.05 ≤ 0.1 (kein P16).
    /// 37 mm → 1.85 %, Abweichung 0.15 > 0.1 (P16). 10 mm → 0.5 %, Abweichung 1.5 (P16). 0 mm waagrecht: kein Stück geneigt (kein P16).</summary>
    [Theory]
    [InlineData(40.0, false)]
    [InlineData(39.0, false)]
    [InlineData(37.0, true)]
    [InlineData(10.0, true)]
    [InlineData(0.0, true)]   // flach statt 2 %
    public void P16_MeldetAbweichungUeberZehntelProzentpunkt(double dz, bool meldet)
    {
        var p = Sw();
        Ok(SwRohr(p, "RO-9", 2, 1000, 1000 - dz, 2000));
        var b = Befunde(p, "P16");
        if (meldet)
        {
            var x = Assert.Single(b);
            Assert.Equal("RO-9", x.Element);
            Assert.Equal("fehler", x.Schwere);
        }
        else Assert.Empty(b);
    }

    [Fact]
    public void P16_SteigendeLeitung_MeldetFehler_FehlendesGefaelle_Warnung()
    {
        var p = Sw();
        Ok(SwRohr(p, "RO-S", 2, 1000, 1040, 2000));        // steigt um 2 % in Fliessrichtung
        var s = Assert.Single(Befunde(p, "P16"));
        Assert.Equal("RO-S", s.Element); Assert.Equal("fehler", s.Schwere);
        Ok(SwRohr(p, "RO-F", null, 1000, 960, 2000));      // fällt 2 %, aber gefaelle fehlt
        var f = Befunde(p, "P16").Single(x => x.Element == "RO-F");
        Assert.Equal("warnung", f.Schwere);
        // waagrecht ohne Gefälle: in einem Schwerkraftsystem fehlt das Gefälle (Spezifikation P16 «Gefälle fehlt oder falsch») → Warnung
        Ok(SwRohr(p, "RO-W", null, 1000, 1000, 2000));
        Assert.Equal("warnung", Befunde(p, "P16").Single(x => x.Element == "RO-W").Schwere);
        // senkrecht (waagrechte Länge 0): nichts zu melden
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Rohr", ["kennzeichen"] = "RO-V", ["system"] = "SW", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["familientyp"] = "DN 50" }, ["bezuege"] = new JsonObject { ["familie"] = "FAM-1" },
            ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Pts([0, 5000, 3000], [0, 5000, 1000]) },
        }));
        Assert.DoesNotContain(Befunde(p, "P16"), x => x.Element == "RO-V");
    }

    [Fact]
    public void P16_NurSchwerkraftsysteme_DruckleitungMitFalschemGefaelleSchweigt()
    {
        var p = Sw();
        Ok(SwRohr(p, "RO-K", 2, 1000, 1010, 2000, system: "KW"));
        Assert.Empty(Befunde(p, "P16"));
    }

    /// <summary>Mit leitung_zeichnen richtig gezeichnetes Gefälle (3 %, 2000 mm: 60 mm) schweigt; absichtlich verstellt (Ende 30 mm höher) meldet.</summary>
    [Fact]
    public void P16_GezeichnetesGefaelleSchweigt_VerstellteHoeheMeldet()
    {
        var p = Sw();
        Ok(ZeichnenSw(p, 3, [0, 0, 2000], [2000, 0, 2000]));
        Gerade(p, "RO-1", [0, 0, 2000], [2000, 0, 1940], Tol);
        Assert.Empty(Befunde(p, "P16"));
        Ok(p.Ausfuehren("element_aendern", new JsonObject
        {
            ["element"] = "RO-1", ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Pts([0, 0, 2000], [2000, 0, 1970]) },
        }));
        var b = Assert.Single(Befunde(p, "P16"));   // 30 mm auf 2000 = 1.5 % statt 3 %
        Assert.Equal("RO-1", b.Element);
    }
}
