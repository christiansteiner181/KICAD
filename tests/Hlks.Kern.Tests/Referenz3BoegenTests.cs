using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;
using static Hlks.Kern.Tests.RoutingHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3, B6, Referenzfall 1: Einbaulängen und Bogenlagen – unabhängig von Hand gerechnet.
/// Grundlage sind allein die Werte der FIKTIVEN Testfamilien (tests/daten/familien): Testrohrserie da = DN + 5; Testbogen
/// r = 2·da, La = Lb = r·tan(w/2); a bei (−La, 0, 0) Richtung (−1, 0, 0), b bei Lb·(cos w, sin w, 0) Richtung (cos w, sin w, 0).
/// Die trigonometrischen Werte stehen als Zahlen (nicht aus dem Kern): tan 7.5° = √6 − √3 + √2 − 2 = 0.131652498, tan 15° = 2 − √3 =
/// 0.267949192, tan 22.5° = √2 − 1 = 0.414213562, tan 45° = 1; cos/sin 15° = 0.965925826 / 0.258819045, 30° = 0.866025404 / 0.5,
/// 45° = 0.707106781 / 0.707106781, 90° = 0 / 1.</summary>
public class Referenz3BoegenTests
{
    const double T7_5 = 0.131652498, T15 = 0.267949192, T22_5 = 0.414213562, T45 = 1.0;
    const double W2 = 0.707106781;
    const double Tol = 2e-3;   // mm – die Handwerte haben 9 Stellen, Längen bis 1000 mm

    // da: DN 15 → 20, DN 25 → 30, DN 50 → 55; r = 2·da: 40, 60, 110 mm
    public static IEnumerable<object[]> BogenFaelle()
    {
        foreach (var (typ, r) in new[] { ("DN 15", 40.0), ("DN 25", 60.0), ("DN 50", 110.0) })
            foreach (var (w, tanHalb, cos, sin) in new[]
            {
                (15.0, T7_5, 0.965925826, 0.258819045),
                (30.0, T15, 0.866025404, 0.5),
                (45.0, T22_5, W2, W2),
                (90.0, T45, 0.0, 1.0),
            })
                yield return [typ, r, w, tanHalb, cos, sin];
    }

    /// <summary>Platzierter Bogen im Punkt (1000, 2000, 500) ohne Drehung: a = P + (−La, 0, 0), b = P + Lb·(cos w, sin w, 0).</summary>
    [Theory]
    [MemberData(nameof(BogenFaelle))]
    public void Bogen_Einbaulaengen_UndLageVonB_DN15_25_50_Winkel15_30_45_90(string typ, double r, double w, double tanHalb, double cos, double sin)
    {
        var p = MitSerie();
        Ok(Formteil(p, "fam-test-bogen", typ, "BO-X", [1000, 2000, 500], winkel: w));
        double la = r * tanHalb;   // La = Lb = r·tan(w/2)
        var a = Stelle(p, "BO-X", "a"); var b = Stelle(p, "BO-X", "b");
        Etwa([1000 - la, 2000, 500], a.Lage, Tol, typ + " " + w + "° a");
        Etwa([-1, 0, 0], a.Richtung, 1e-9, "Richtung a");
        Etwa([1000 + la * cos, 2000 + la * sin, 500], b.Lage, Tol, typ + " " + w + "° b");
        Etwa([cos, sin, 0], b.Richtung, 1e-6, "Richtung b");
        Etwa(w, Leitungspruefungen.Bogenwinkel(p.Modell, p.Katalog, Hole(p, "BO-X"))!.Value, 1e-4, "Bogenwinkel");
        // die Einbaulänge, die das Routing verwendet, ist dieselbe
        var wahl = Serie(p).Formteil(p.Modell, p.Katalog, "bogen", new Leitungsgroesse(int.Parse(typ[3..]), null, null, null), winkel: w);
        Assert.True(wahl.Ok, wahl.Fehler);
        Etwa(la, wahl.Geometrie!.Einbaulaenge("a"), Tol, "Einbaulänge a");
        Etwa(la, wahl.Geometrie.Einbaulaenge("b"), Tol, "Einbaulänge b");
    }

    /// <summary>Zug mit einem Knick von w: beide Geraden um La gekürzt, Bogen im Knick, b = Knick + La·(cos w, sin w).</summary>
    [Theory]
    [MemberData(nameof(BogenFaelle))]
    public void Zug_MitKnick_GeradenUmDieEinbaulaengeGekuerzt(string typ, double r, double w, double tanHalb, double cos, double sin)
    {
        var p = MitSerie();
        // Knick bei (1000, 0, 0); zweiter Schenkel 1000 mm in Richtung (cos w, sin w)
        var e = Ok(Zeichnen(p, typ, [[0, 0, 0], [1000, 0, 0], [1000 + 1000 * cos, 1000 * sin, 0]]));
        double la = r * tanHalb;
        Assert.Equal(["RO-1", "RO-2"], Angelegt(e, "Rohr"));
        Gerade(p, "RO-1", [0, 0, 0], [1000 - la, 0, 0], Tol);                                                       // 1000 − La
        Gerade(p, "RO-2", [1000 + la * cos, la * sin, 0], [1000 + 1000 * cos, 1000 * sin, 0], Tol);                 // 1000 − La
        Etwa(1000 - la, Laenge(p, "RO-1"), Tol); Etwa(1000 - la, Laenge(p, "RO-2"), Tol);
        Etwa([1000, 0, 0], Familien.Einfuegepunkt(Hole(p, "BO-1")), 1e-6);
        Etwa(w, Leitungspruefungen.Bogenwinkel(p.Modell, p.Katalog, Hole(p, "BO-1"))!.Value, 1e-4);
        Assert.Equal(typ, Familien.Typ(Hole(p, "BO-1")));
        Sauber(p);
    }

    /// <summary>45°-Versatz DN 40: da 45, r 90, La = Lb = 90·tan 22.5° = 90·0.414213562 = 37.27922058 mm.
    /// Schräges Stück: 1000·√2 − 2·La = 1414.213562 − 74.55844116 = 1339.65512 mm.</summary>
    [Fact]
    public void Versatz45Grad_DN40_BeideBoegenUndDasSchraegeStueck()
    {
        var p = MitSerie();
        Ok(Zeichnen(p, "DN 40", [[0, 0, 0], [1000, 0, 0], [2000, 1000, 0], [3000, 1000, 0]]));
        const double la = 37.27922058;
        Gerade(p, "RO-1", [0, 0, 0], [1000 - la, 0, 0], Tol);                                  // 962.72077942
        Gerade(p, "RO-2", [1000 + la * W2, la * W2, 0], [2000 - la * W2, 1000 - la * W2, 0], Tol);   // (1026.3604, 26.3604) → (1973.6396, 973.6396)
        Gerade(p, "RO-3", [2000 + la, 1000, 0], [3000, 1000, 0], Tol);                         // 962.72077942
        Etwa(1339.65512, Laenge(p, "RO-2"), 2e-3);
        Etwa(962.72078, Laenge(p, "RO-1"), 2e-3);
        // Bogen 1 biegt links (+45°), Bogen 2 rechts (−45°): b von BO-2 zeigt wieder in +x
        Etwa([W2, W2, 0], Stelle(p, "BO-1", "b").Richtung, 1e-6);
        Etwa([1, 0, 0], Stelle(p, "BO-2", "b").Richtung, 1e-6);
        Etwa([2000 + la, 1000, 0], Stelle(p, "BO-2", "b").Lage, Tol);
        Assert.Equal(45, Leitungspruefungen.Bogenwinkel(p.Modell, p.Katalog, Hole(p, "BO-1"))!.Value, 4);
        Assert.Equal(45, Leitungspruefungen.Bogenwinkel(p.Modell, p.Katalog, Hole(p, "BO-2"))!.Value, 4);
        Sauber(p);
    }

    /// <summary>Steigbogen (waagrecht → senkrecht aufwärts), DN 25: La = Lb = 60·tan 45° = 60 mm.
    /// Knick (2000, 0, 1000): a = (1940, 0, 1000) Richtung (−1, 0, 0); b = (2000, 0, 1060) Richtung (0, 0, 1).</summary>
    [Fact]
    public void Steigbogen_WaagrechtNachSenkrecht_AlleAnschluesseInWeltkoordinaten()
    {
        var p = MitSerie();
        Ok(Zeichnen(p, "DN 25", [[0, 0, 1000], [2000, 0, 1000], [2000, 0, 3000]]));
        Etwa([2000, 0, 1000], Familien.Einfuegepunkt(Hole(p, "BO-1")), 1e-6);
        Etwa([1940, 0, 1000], Stelle(p, "BO-1", "a").Lage, Tol); Etwa([-1, 0, 0], Stelle(p, "BO-1", "a").Richtung, 1e-6);
        Etwa([2000, 0, 1060], Stelle(p, "BO-1", "b").Lage, Tol); Etwa([0, 0, 1], Stelle(p, "BO-1", "b").Richtung, 1e-6);
        Gerade(p, "RO-1", [0, 0, 1000], [1940, 0, 1000], Tol);        // 1940 mm
        Gerade(p, "RO-2", [2000, 0, 1060], [2000, 0, 3000], Tol);     // 3000 − 1060 = 1940 mm
        Etwa(1940, Laenge(p, "RO-2"), Tol);
        IstVerbunden(p, "RO-1", "ende", "BO-1", "a");
        IstVerbunden(p, "BO-1", "b", "RO-2", "anfang");
        // die Leitungsenden liegen auf den Anschlüssen
        Etwa(Stelle(p, "BO-1", "a").Lage!, Pkt(p, "RO-1")[1], 1e-6);
        Etwa(Stelle(p, "BO-1", "b").Lage!, Pkt(p, "RO-2")[0], 1e-6);
        Sauber(p);
    }

    /// <summary>Fallbogen (senkrecht abwärts → waagrecht), DN 25: Knick (0, 0, 1000); a liegt 60 mm darüber und zeigt nach oben,
    /// b liegt 60 mm in +x.</summary>
    [Fact]
    public void Fallbogen_SenkrechtNachWaagrecht_AlleAnschluesseInWeltkoordinaten()
    {
        var p = MitSerie();
        Ok(Zeichnen(p, "DN 25", [[0, 0, 3000], [0, 0, 1000], [2000, 0, 1000]]));
        Etwa([0, 0, 1060], Stelle(p, "BO-1", "a").Lage, Tol); Etwa([0, 0, 1], Stelle(p, "BO-1", "a").Richtung, 1e-6);
        Etwa([60, 0, 1000], Stelle(p, "BO-1", "b").Lage, Tol); Etwa([1, 0, 0], Stelle(p, "BO-1", "b").Richtung, 1e-6);
        Gerade(p, "RO-1", [0, 0, 3000], [0, 0, 1060], Tol);           // 1940 mm
        Gerade(p, "RO-2", [60, 0, 1000], [2000, 0, 1000], Tol);       // 1940 mm
        Sauber(p);
    }

    /// <summary>Neigungsbogen: waagrecht → 45° aufwärts, DN 25: La = 60·0.414213562 = 24.85281372 mm.
    /// a = (2000 − La, 0, 0); b = (2000, 0, 0) + La·(0.707106781, 0, 0.707106781) = (2017.5736, 0, 17.5736).
    /// Zweites Stück bis (3000, 0, 1000): 1000·√2 − La = 1414.213562 − 24.852814 = 1389.360748 mm.</summary>
    [Fact]
    public void Neigungsbogen_45GradAufwaerts()
    {
        var p = MitSerie();
        Ok(Zeichnen(p, "DN 25", [[0, 0, 0], [2000, 0, 0], [3000, 0, 1000]]));
        const double la = 24.85281372;
        Etwa([2000 - la, 0, 0], Stelle(p, "BO-1", "a").Lage, Tol);
        Etwa([2000 + la * W2, 0, la * W2], Stelle(p, "BO-1", "b").Lage, Tol);
        Etwa([W2, 0, W2], Stelle(p, "BO-1", "b").Richtung, 1e-6);
        Gerade(p, "RO-1", [0, 0, 0], [2000 - la, 0, 0], Tol);
        Gerade(p, "RO-2", [2000 + la * W2, 0, la * W2], [3000, 0, 1000], Tol);
        Etwa(1389.360748, Laenge(p, "RO-2"), 2e-3);
        Etwa(45, Leitungspruefungen.Bogenwinkel(p.Modell, p.Katalog, Hole(p, "BO-1"))!.Value, 1e-4);
        Sauber(p);
    }

    /// <summary>Platzierter Steigbogen mit voller Ausrichtung: x = (1, 0, 0), z = (0, −1, 0) → lokales y = z × x = (0, 0, 1).
    /// DN 25, 90°, Ursprung (500, 500, 500): a = (440, 500, 500), b = (500, 500, 560) Richtung (0, 0, 1).</summary>
    [Fact]
    public void PlatzierterBogen_MitAusrichtung_Steigbogen()
    {
        var p = MitSerie();
        Ok(Formteil(p, "fam-test-bogen", "DN 25", "BO-S", [500, 500, 500], x: [1, 0, 0], z: [0, -1, 0]));
        Etwa([440, 500, 500], Stelle(p, "BO-S", "a").Lage, Tol);
        Etwa([500, 500, 560], Stelle(p, "BO-S", "b").Lage, Tol);
        Etwa([0, 0, 1], Stelle(p, "BO-S", "b").Richtung, 1e-6);
    }

    /// <summary>Ausrichtung x = (0, 1, 0), z = (0, 0, 1) → lokales y = z × x = (0·0 − 1·1, 1·0 − 0·0, 0) = (−1, 0, 0): der Bogen
    /// zeigt nach Norden und biegt nach Westen. DN 25, 90°, Ursprung (500, 500, 500): a = (500, 440, 500), b = (440, 500, 500).</summary>
    [Fact]
    public void PlatzierterBogen_MitAusrichtung_NachNordenUndLinksNachWesten()
    {
        var p = MitSerie();
        Ok(Formteil(p, "fam-test-bogen", "DN 25", "BO-N", [500, 500, 500], x: [0, 1, 0], z: [0, 0, 1]));
        Etwa([500, 440, 500], Stelle(p, "BO-N", "a").Lage, Tol);
        Etwa([0, -1, 0], Stelle(p, "BO-N", "a").Richtung, 1e-6);
        Etwa([440, 500, 500], Stelle(p, "BO-N", "b").Lage, Tol);
        Etwa([-1, 0, 0], Stelle(p, "BO-N", "b").Richtung, 1e-6);
    }

    /// <summary>T-Stück DN 32 (da 37 → l = lc = 37): Anschlüsse a (−37, 0, 0), b (37, 0, 0), c (0, 37, 0); Reduktion DN 32/25 (l = 40):
    /// a im Ursprung, b bei (40, 0, 0). Platziert bei (1000, 1000, 1000) ohne Drehung.</summary>
    [Fact]
    public void TStueckUndReduktion_Anschlusslagen_AusDenFamilienwerten()
    {
        var p = MitSerie();
        Ok(Formteil(p, "fam-test-tstueck", "DN 32", "TS-X", [1000, 1000, 1000]));
        Etwa([963, 1000, 1000], Stelle(p, "TS-X", "a").Lage, Tol);
        Etwa([1037, 1000, 1000], Stelle(p, "TS-X", "b").Lage, Tol);
        Etwa([1000, 1037, 1000], Stelle(p, "TS-X", "c").Lage, Tol);
        Etwa([0, 1, 0], Stelle(p, "TS-X", "c").Richtung, 1e-6);
        Ok(Formteil(p, "fam-test-reduktion", "DN 32/25", "RD-X", [2000, 1000, 1000]));
        Etwa([2000, 1000, 1000], Stelle(p, "RD-X", "a").Lage, Tol);
        Etwa([2040, 1000, 1000], Stelle(p, "RD-X", "b").Lage, Tol);
        Assert.Equal(32.0, Stelle(p, "RD-X", "a").Groesse.Dn!.Value);
        Assert.Equal(25.0, Stelle(p, "RD-X", "b").Groesse.Dn!.Value);
    }
}
