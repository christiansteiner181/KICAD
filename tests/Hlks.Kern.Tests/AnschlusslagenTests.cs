using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3, B1: Anschlusslagen für alle Elemente (Anschlusslagen.Von, Abfrage anschlusslagen) – Leitungen aus dem Verlauf,
/// Familienelemente aus der Familie, ohne Familie bzw. nicht platziert ohne Lage. Werte FIKTIV (Testfamilien).</summary>
public class AnschlusslagenTests
{
    [Fact]
    public void Leitung_AnfangUndEnde_RichtungNachAussen()
    {
        var p = MitSerie(false);
        Ok(RohrOhneSerie(p, "RO-1", 20, [0, 0, 500], [1000, 0, 500], [1000, 500, 500]));
        var a = Stelle(p, "RO-1", "anfang"); var e = Stelle(p, "RO-1", "ende");
        // anfang = erster Punkt, Richtung vom zweiten zum ersten; ende = letzter Punkt, Richtung vom vorletzten zum letzten
        Etwa([0, 0, 500], a.Lage); Etwa([-1, 0, 0], a.Richtung);
        Etwa([1000, 500, 500], e.Lage); Etwa([0, 1, 0], e.Richtung);
        Assert.Equal(20, a.Groesse.Dn);
        Assert.Equal("leitung", a.Quelle);
        Assert.Equal("KW", a.Medium);
        Assert.Equal("rein", a.Fluss); Assert.Equal("raus", e.Fluss);
        Assert.Equal("rohr", a.Art);
    }

    [Fact]
    public void Leitung_SchraegeRichtungNormiert()
    {
        var p = MitSerie(false);
        Ok(RohrOhneSerie(p, "RO-1", 20, [0, 0, 0], [300, 400, 0]));
        // (300, 400) / 500 = (0.6, 0.8)
        Etwa([-0.6, -0.8, 0], Stelle(p, "RO-1", "anfang").Richtung, 1e-12);
        Etwa([0.6, 0.8, 0], Stelle(p, "RO-1", "ende").Richtung, 1e-12);
    }

    [Fact]
    public void Kanal_GroesseAusDurchmesserBzwBreiteHoehe()
    {
        var p = MitSerie(false);
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "ZUL", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Zuluft" } }));
        JsonObject Kanal(string kz, JsonObject eig) => new()
        {
            ["klasse"] = "Kanal", ["kennzeichen"] = kz, ["system"] = "ZUL", ["geschoss"] = "EG", ["eigenschaften"] = eig,
            ["geometrie"] = new JsonObject { ["art"] = "linie", ["punkte"] = Pts([0, 0, 2500], [2000, 0, 2500]) },
        };
        Ok(p.Ausfuehren("element_anlegen", Kanal("KA-1", new JsonObject { ["durchmesser"] = 125 })));
        Ok(p.Ausfuehren("element_anlegen", Kanal("KA-2", new JsonObject { ["breite"] = 400, ["hoehe"] = 200 })));
        var r = Stelle(p, "KA-1", "ende");
        Assert.Equal(125, r.Groesse.D); Assert.Null(r.Groesse.Dn); Assert.Equal("rund", r.Form); Assert.Equal("kanal", r.Art);
        var q = Stelle(p, "KA-2", "anfang");
        Assert.Equal(400, q.Groesse.B); Assert.Equal(200, q.Groesse.H); Assert.Equal("rechteck", q.Form);
        Assert.Equal("400 × 200 mm", q.Groesse.ToString());
    }

    [Fact]
    public void Familienelement_AusDerFamilie()
    {
        var p = MitSerie();
        Ok(Formteil(p, "fam-test-bogen", "DN 25", "BO-1", [1000, 2000, 500], winkel: 90));
        // DN 25: da = 30, r = 60, La = Lb = 60 · tan 45° = 60
        var a = Stelle(p, "BO-1", "a"); var b = Stelle(p, "BO-1", "b");
        Etwa([940, 2000, 500], a.Lage, 1e-3); Etwa([-1, 0, 0], a.Richtung);
        Etwa([1000, 2060, 500], b.Lage, 1e-3); Etwa([0, 1, 0], b.Richtung, 1e-9);
        Assert.Equal(25, a.Groesse.Dn);
        Assert.Equal("familie", a.Quelle);
        Assert.Equal("KW", a.Medium);
    }

    [Fact]
    public void OhneFamilie_KeineLageKeineGroesse()
    {
        var p = MitSerie(false);
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "WC", ["kennzeichen"] = "WC-1", ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([100, 100, 0]) },
        }));
        var l = Anschlusslagen.Von(p.Modell, p.Katalog, Hole(p, "WC-1"));
        Assert.NotEmpty(l);
        Assert.All(l, s => { Assert.Null(s.Lage); Assert.Null(s.Richtung); Assert.False(s.HatLage); Assert.False(s.HatGroesse); Assert.Equal("ohne_familie", s.Quelle); });
        // Formteil ohne Familie mit dn (grösste Nennweite): auch ohne Grösse
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "TStueck", ["kennzeichen"] = "TS-1", ["system"] = "KW", ["eigenschaften"] = new JsonObject { ["dn"] = 50 },
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([0, 0, 0]) },
        }));
        Assert.All(Anschlusslagen.Von(p.Modell, p.Katalog, Hole(p, "TS-1")), s => Assert.False(s.HatGroesse));
    }

    [Fact]
    public void NichtPlatziert_OhneLageMitGroesse_ScheitertNicht()
    {
        var p = MitSerie();
        // Bogen mit Familie, aber ohne Geometrie (nur im Schema)
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Bogen", ["kennzeichen"] = "BO-N", ["system"] = "KW",
            ["eigenschaften"] = new JsonObject { ["familientyp"] = "DN 20" }, ["bezuege"] = new JsonObject { ["familie"] = "FAM-2" },
        }));
        Assert.Equal(["a", "b"], Hole(p, "BO-N").Anschluesse.Select(a => a.Name));
        var a = Stelle(p, "BO-N", "a");
        Assert.Null(a.Lage); Assert.Equal("nicht_platziert", a.Quelle); Assert.Equal(20, a.Groesse.Dn);
        // Rohr ohne Geometrie
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Rohr", ["kennzeichen"] = "RO-N", ["system"] = "KW", ["eigenschaften"] = new JsonObject { ["familientyp"] = "DN 20" },
            ["bezuege"] = new JsonObject { ["familie"] = "FAM-1" },
        }));
        var r = Stelle(p, "RO-N", "ende");
        Assert.Null(r.Lage); Assert.Equal("nicht_platziert", r.Quelle); Assert.Equal(20, r.Groesse.Dn);
        // Grafik und Prüfung scheitern nicht daran
        Assert.NotNull(Grafik.Plan(p, "AN-EG"));
        Assert.NotNull(Pruefungen.Alle(p.Modell, p.Katalog));
        var o = (JsonObject)Tuer.Abfrage(p, "anschlusslagen", new JsonObject { ["element"] = "BO-N" });
        Assert.Null(o["einfuegepunkt"]);
        Assert.Contains("nicht platziert", o["hinweis"]!.GetValue<string>());
    }

    [Fact]
    public void Abfrage_FuerAlleElemente()
    {
        var p = MitSerie();
        Ok(RohrOhneSerie(p, "RO-1", 20, [500, 2000, 500], [950, 2000, 500]));
        Ok(Formteil(p, "fam-test-bogen", "DN 20", "BO-1", [1000, 2000, 500], winkel: 90, x: [1, 0, 0], z: [0, -1, 0]));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "WC", ["kennzeichen"] = "WC-1", ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([100, 100, 0]) },
        }));
        JsonObject A(string kz) => (JsonObject)Tuer.Abfrage(p, "anschlusslagen", new JsonObject { ["element"] = kz });
        JsonObject An(JsonObject o, string n) => o["anschluesse"]!.AsArray().Select(x => x!.AsObject()).Single(x => x["name"]!.GetValue<string>() == n);

        var ro = A("RO-1");
        Assert.Null(ro["familie"]);
        Assert.Equal("leitung", An(ro, "ende")["quelle"]!.GetValue<string>());
        Assert.Equal([950.0, 2000, 500], An(ro, "ende")["lage"]!.AsArray().Select(x => x!.GetValue<double>()));
        Assert.Equal(20, An(ro, "ende")["dn"]!.GetValue<double>());

        var bo = A("BO-1");
        Assert.Equal("FAM-2", bo["familie"]!.GetValue<string>());
        Assert.Equal([0.0, 0, 1], bo["ausrichtung"]!["y"]!.AsArray().Select(x => x!.GetValue<double>()));
        Assert.Equal([0.0, 0, 1], An(bo, "b")["richtung"]!.AsArray().Select(x => x!.GetValue<double>()));

        var wc = A("WC-1");
        Assert.Null(An(wc, "kw")["lage"]);
        Assert.Contains("keine Familie", wc["hinweis"]!.GetValue<string>());

        Ok(p.Ausfuehren("verbinden", new JsonObject { ["von"] = "RO-1.ende", ["nach"] = "BO-1.a" }));
        Assert.Equal("BO-1.a", An(A("RO-1"), "ende")["verbunden"]!.GetValue<string>());
        var f = Assert.Throws<HlksFehler>(() => Tuer.Abfrage(p, "anschlusslagen", new JsonObject { ["element"] = "EG" }));
        Assert.Contains("keine Anschlüsse", f.Message);
    }

    [Fact]
    public void Abweichung_AbstandUndWinkel()
    {
        AnschlussStelle S(double[] lage, double[] richtung) => new("x", "*", "beide", lage, richtung, default, null, null, null, "test");
        var (abstand, winkel) = Anschlusslagen.Abweichung(S([0, 0, 0], [1, 0, 0]), S([3, 4, 0], [-1, 0, 0]))!.Value;
        Etwa(5, abstand, 1e-12); Etwa(0, winkel, 1e-9);
        // Richtung B um 30° gegen −A gedreht
        var w = Anschlusslagen.Abweichung(S([0, 0, 0], [1, 0, 0]), S([0, 0, 0], [-Math.Cos(Math.PI / 6), Math.Sin(Math.PI / 6), 0]))!.Value.Winkel;
        Etwa(30, w, 1e-9);
        Assert.Null(Anschlusslagen.Abweichung(S([0, 0, 0], [1, 0, 0]), new("y", "*", "beide", null, null, default, null, null, null, "ohne_familie")));
    }
}
