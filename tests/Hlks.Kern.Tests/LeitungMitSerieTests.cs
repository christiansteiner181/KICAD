using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3, B1: Leitungen mit Serie (bezuege.familie = Leitungsserie, familientyp = Grösse) – Grösse aus dem Typ mit
/// Herkunft familie, Anschlüsse anfang/ende bleiben samt Verbindungen, Grösse von Hand mit Hinweis, Grafik weiter aus HlksGrafik.
/// Werte FIKTIV (Testrohrserie; Kanalserien als FIKTIVE Mini-Familien im Test).</summary>
public class LeitungMitSerieTests
{
    [Fact]
    public void Rohr_GroesseAusDemTyp_HerkunftFamilie()
    {
        var p = MitSerie(false);
        var e = Ok(Rohr(p, "RO-1", "DN 20", [0, 0, 500], [1000, 0, 500]));
        var ro = Hole(p, "RO-1");
        Assert.Equal(20, ro.Zahl("dn"));
        var w = ro.Eigenschaft("dn")!;
        Assert.Equal("familie", w.Herkunft.Art);
        Assert.Equal("Testrohrserie (fiktiv) Typ DN 20", w.Herkunft.Quelle);
        Assert.Equal("DN", w.Einheit);
        Assert.Equal(["anfang", "ende"], ro.Anschluesse.Select(a => a.Name));
        Assert.All(ro.Anschluesse, a => Assert.Equal(20, a.Dn));
        Assert.Equal("rein", ro.AnschlussNamens("anfang")!.Fluss);
        Assert.Equal("KW", ro.AnschlussNamens("ende")!.Medium);
        Assert.DoesNotContain(e.Hinweise, h => h.Contains("getrennt"));
    }

    [Fact]
    public void Typwechsel_FuehrtGroesseNach_VerbindungBleibt()
    {
        var p = MitSerie(false);
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "WC", ["kennzeichen"] = "WC-1", ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([1000, 0, 0]) },
        }));
        Ok(Rohr(p, "RO-1", "DN 20", [0, 0, 500], [1000, 0, 500]));
        Ok(p.Ausfuehren("verbinden", new JsonObject { ["von"] = "RO-1.ende", ["nach"] = "WC-1.kw" }));
        var e = Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "RO-1", ["eigenschaften"] = new JsonObject { ["familientyp"] = "DN 25" } }));
        var ro = Hole(p, "RO-1");
        Assert.Equal(25, ro.Zahl("dn"));
        Assert.Equal("Testrohrserie (fiktiv) Typ DN 25", ro.Eigenschaft("dn")!.Herkunft.Quelle);
        Assert.All(ro.Anschluesse, a => Assert.Equal(25, a.Dn));
        Assert.NotNull(p.Modell.VerbindungAn(ro.Id, "ende"));
        Assert.DoesNotContain(e.Aenderungen, a => a.Art == "getrennt");
        Ok(p.Rueckgaengig());
        Assert.Equal(20, Hole(p, "RO-1").Zahl("dn"));
        Assert.Single(p.Modell.Verbindungen);
    }

    [Fact]
    public void GroesseVonHand_HinweisUndFamilienwertGilt()
    {
        var p = MitSerie(false);
        Ok(Rohr(p, "RO-1", "DN 20", [0, 0, 500], [1000, 0, 500]));
        var e = Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "RO-1", ["eigenschaften"] = new JsonObject { ["dn"] = 32 } }));
        Assert.Contains(e.Hinweise, h => h.Contains("RO-1: dn kommt aus der") && h.Contains("32"));
        Assert.Equal(20, Hole(p, "RO-1").Zahl("dn"));
        Assert.All(Hole(p, "RO-1").Anschluesse, a => Assert.Equal(20, a.Dn));
        // leitung_ziehen mit dn: ohne Serie gilt dn wie bisher
        Ok(p.Ausfuehren("leitung_ziehen", new JsonObject { ["system"] = "KW", ["dn"] = 32, ["kennzeichen"] = "RO-2", ["punkte"] = Pts([0, 500, 500], [100, 500, 500]) }));
        Assert.Equal(32, Hole(p, "RO-2").Zahl("dn"));
    }

    [Fact]
    public void SerieGeaendert_LeitungenBleibenVerbunden()
    {
        var p = MitSerie(false);
        Ok(Rohr(p, "RO-1", "DN 20", [0, 0, 500], [1000, 0, 500]));
        Ok(Rohr(p, "RO-2", "DN 20", [1000, 0, 500], [2000, 0, 500]));
        Ok(p.Ausfuehren("verbinden", new JsonObject { ["von"] = "RO-1.ende", ["nach"] = "RO-2.anfang" }));
        var e = Ok(p.Ausfuehren("familie_aendern", new JsonObject
        {
            ["familie"] = "FAM-1",
            ["ops"] = new JsonArray(new JsonObject { ["op"] = "typ_setzen", ["typ"] = new JsonObject { ["name"] = "DN 20", ["werte"] = new JsonObject { ["dn"] = 20, ["da"] = 26, ["s"] = 2 } } }),
        }));
        Assert.Single(p.Modell.Verbindungen);
        Assert.Equal(["anfang", "ende"], Hole(p, "RO-1").Anschluesse.Select(a => a.Name));
        Assert.DoesNotContain(e.Hinweise, h => h.Contains("getrennt"));
        Assert.Equal(26, Serie(p).Groesse("DN 20")!.Aussen);
        // ein verwendeter Typ darf nicht verschwinden
        Nein(p.Ausfuehren("familie_aendern", new JsonObject { ["familie"] = "FAM-1", ["ops"] = new JsonArray(new JsonObject { ["op"] = "typ_loeschen", ["name"] = "DN 20" }) }));
    }

    [Fact]
    public void FalscheFamilie_Abgelehnt()
    {
        var p = MitSerie();
        var n = Nein(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Rohr", ["kennzeichen"] = "RO-1", ["system"] = "KW", ["eigenschaften"] = new JsonObject { ["familientyp"] = "DN 20" },
            ["bezuege"] = new JsonObject { ["familie"] = "FAM-2" },
            ["geometrie"] = new JsonObject { ["art"] = "linie", ["punkte"] = Pts([0, 0, 0], [100, 0, 0]) },
        }));
        Assert.Contains("Kategorie Bogen", n.Fehler);
        Nein(Rohr(p, "RO-2", "DN 65", [0, 0, 0], [100, 0, 0]));   // Grösse gibt es nicht
    }

    [Fact]
    public void Grafik_LeitungMitSerieZeichnetHlksGrafik()
    {
        var p = MitSerie(false);
        Ok(Rohr(p, "RO-1", "DN 20", [0, 0, 500], [1000, 0, 500]));
        var ro = Hole(p, "RO-1");
        Assert.False(new FamilienGrafik().Zustaendig(p.Katalog, ro));
        Assert.IsType<HlksGrafik>(Grafik.QuelleFuer(p.Katalog, ro));
        var teil = Grafik.Plan(p, "AN-EG").Teile.Single(t => t.Id == ro.Id);
        Assert.DoesNotContain(teil.Primitive, x => x.Text?.StartsWith("Familie:") == true);
        Assert.NotNull(Grafik.QuelleFuer(p.Katalog, ro)!.Koerper(p, ro));
    }

    static JsonObject Kanalserie(bool rund) => JsonNode.Parse(rund
        ? """
          { "format": 1, "id": "fam-test-kanal-rund", "name": "Kanal rund (fiktiv)", "kategorie": "Kanal",
            "beschreibung": "FIKTIVE Mini-Serie für Tests, keine Herstellerdaten",
            "parameter": [ { "name": "d", "art": "typ", "datentyp": "laenge" } ],
            "typen": [ { "name": "125", "werte": { "d": 125 } }, { "name": "160", "werte": { "d": 160 } } ],
            "koerper": [], "anschluesse": [],
            "leitung": { "querschnitt": "rund", "groesse": { "d": "d" }, "aussen": "d + 1", "regeln": { "winkel": "frei" } } }
          """
        : """
          { "format": 1, "id": "fam-test-kanal-eckig", "name": "Kanal eckig (fiktiv)", "kategorie": "Kanal",
            "beschreibung": "FIKTIVE Mini-Serie für Tests, keine Herstellerdaten",
            "parameter": [ { "name": "b", "art": "typ", "datentyp": "laenge" }, { "name": "h", "art": "typ", "datentyp": "laenge" } ],
            "typen": [ { "name": "400x200", "werte": { "b": 400, "h": 200 } } ],
            "koerper": [], "anschluesse": [],
            "leitung": { "querschnitt": "rechteck", "groesse": { "b": "b", "h": "h" }, "aussen": 0.001 } }
          """)!.AsObject();

    [Fact]
    public void Kanal_RundUndRechteckig()
    {
        var p = MitSerie(false);
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "ZUL", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Zuluft" } }));
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Kanalserie(true) }));
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Kanalserie(false) }));
        BefehlsErgebnis Kanal(string kz, string serie, string typ) => p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Kanal", ["kennzeichen"] = kz, ["system"] = "ZUL", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["familientyp"] = typ }, ["bezuege"] = new JsonObject { ["familie"] = serie },
            ["geometrie"] = new JsonObject { ["art"] = "linie", ["punkte"] = Pts([0, 0, 2500], [2000, 0, 2500]) },
        });
        Ok(Kanal("KA-1", "FAM-2", "160"));
        Ok(Kanal("KA-2", "FAM-3", "400x200"));
        var k1 = Hole(p, "KA-1");
        Assert.Equal(160, k1.Zahl("durchmesser"));
        Assert.Equal("familie", k1.Eigenschaft("durchmesser")!.Herkunft.Art);
        Assert.All(k1.Anschluesse, a => Assert.Equal(160, a.D));
        var k2 = Hole(p, "KA-2");
        Assert.Equal(400, k2.Zahl("breite")); Assert.Equal(200, k2.Zahl("hoehe"));
        Assert.All(k2.Anschluesse, a => { Assert.Equal(400, a.B); Assert.Equal(200, a.H); });
        Assert.Equal(160, Stelle(p, "KA-1", "ende").Groesse.D);
        Assert.Equal(400, Stelle(p, "KA-2", "ende").Groesse.B);
        Assert.Contains("Kanal rund", Nein(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Rohr", ["kennzeichen"] = "RO-K", ["system"] = "KW", ["eigenschaften"] = new JsonObject { ["familientyp"] = "160" },
            ["bezuege"] = new JsonObject { ["familie"] = "FAM-2" },
        })).Fehler);
    }

    [Fact]
    public void Trockenlauf_LegtNichtsAn()
    {
        var p = MitSerie(false);
        var vorher = p.Modell.Elemente.Count;
        var e = p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Rohr", ["kennzeichen"] = "RO-1", ["system"] = "KW", ["eigenschaften"] = new JsonObject { ["familientyp"] = "DN 20" },
            ["bezuege"] = new JsonObject { ["familie"] = "FAM-1" },
            ["geometrie"] = new JsonObject { ["art"] = "linie", ["punkte"] = Pts([0, 0, 0], [100, 0, 0]) },
        }, trockenlauf: true);
        Ok(e);
        Assert.Equal(vorher, p.Modell.Elemente.Count);
    }
}
