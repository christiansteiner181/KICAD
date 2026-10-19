using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;
using static Hlks.Kern.Tests.RoutingHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3, Korrektur 30b der Abnahme (19.10.2026): Ein einzeln platziertes Formteil (familie_platzieren ohne teilVon) übernimmt
/// beim Verbinden den Strang der angeschlossenen Leitung (Befehl verbinden, leitung_ziehen mit von/nach, Routing leitung_zeichnen); bei
/// Leitungen verschiedener Stränge entscheiden die Durchgangsleitungen (Anschlüsse a/b), sonst keine Zuordnung mit Hinweis. Mit
/// Trockenlauf und Rückgängig. Werte FIKTIV (Testrohrserie da = DN + 5; Test-T-Stück l = lc = da, DN 20: 25 mm).
/// Lage (Handrechnung): T-Stück bei (1000, 0, 500): a (975, 0, 500) zeigt −x, b (1025, 0, 500) zeigt +x, c (1000, 25, 500) zeigt +y.</summary>
public class StrangzuordnungTests
{
    /// <summary>Projekt mit Testserie und Formteilen, System KW, Strängen ST-1 und ST-2 (beide KW).</summary>
    static Projekt Projekt2Straenge()
    {
        var p = MitSerie();
        foreach (var st in new[] { "ST-1", "ST-2" })
            Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Strang", ["kennzeichen"] = st, ["teilVon"] = "KW", ["eigenschaften"] = new JsonObject { ["art"] = "steigstrang" } }));
        return p;
    }

    static BefehlsErgebnis RohrIn(Projekt p, string kz, string? strang, params double[][] punkte)
    {
        var o = new JsonObject
        {
            ["klasse"] = "Rohr", ["kennzeichen"] = kz, ["system"] = "KW", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["familientyp"] = "DN 20" }, ["bezuege"] = new JsonObject { ["familie"] = "FAM-1" },
            ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Pts(punkte) },
        };
        if (strang is not null) o["teilVon"] = strang;
        return p.Ausfuehren("element_anlegen", o);
    }

    static BefehlsErgebnis Verb(Projekt p, string von, string nach, bool trocken = false) =>
        p.Ausfuehren("verbinden", new JsonObject { ["von"] = von, ["nach"] = nach }, trocken);

    static string? Strang(Projekt p, string kz) => p.Modell.Finde(Hole(p, kz).TeilVon)?.Kennzeichen;

    /// <summary>T-Stück TS-1 bei (1000, 0, 500) ohne Strang; Rohre RO-A am Durchgang a, RO-B am Durchgang b, RO-C am Abzweig c
    /// (je mit dem angegebenen Strang, null = ohne). Verbunden wird nicht.</summary>
    static Projekt MitTStueck(string? strangA, string? strangB, string? strangC)
    {
        var p = Projekt2Straenge();
        Ok(Formteil(p, "FAM-3", "DN 20", "TS-1", [1000, 0, 500]));
        Ok(RohrIn(p, "RO-A", strangA, [0, 0, 500], [975, 0, 500]));
        Ok(RohrIn(p, "RO-B", strangB, [1025, 0, 500], [3000, 0, 500]));
        Ok(RohrIn(p, "RO-C", strangC, [1000, 25, 500], [1000, 2000, 500]));
        return p;
    }

    [Fact]
    public void Verbinden_FormteilOhneStrang_UebernimmtDenStrangDerLeitung()
    {
        var p = MitTStueck("ST-1", null, null);
        Assert.Null(Hole(p, "TS-1").TeilVon);
        Assert.Equal(0, (int)Json.Zahl(Ableitungen.Materialliste(p, strang: "ST-1")["formteileTotal"])!);
        var e = Ok(Verb(p, "RO-A.ende", "TS-1.a"));
        Assert.Equal("ST-1", Strang(p, "TS-1"));
        Assert.Equal(Hole(p, "KW").Id, Hole(p, "TS-1").System);
        Assert.Contains(e.Hinweise, h => h.Contains("TS-1") && h.Contains("ST-1"));
        var ml = Ableitungen.Materialliste(p, strang: "ST-1");
        Assert.Equal(1, (int)Json.Zahl(ml["formteileTotal"])!);
        Assert.Equal("TStueck", ml["formteile"]![0]!["klasse"]!.GetValue<string>());
    }

    [Fact]
    public void Verbinden_NurAbzweigMitStrang_GiltDerStrangDesAbzweigs()
    {
        // Hängt nur der Abzweig an einer Leitung mit Strang, gilt dessen Strang (eine Leitung = ein Strang)
        var p = MitTStueck(null, null, "ST-2");
        Ok(Verb(p, "RO-C.anfang", "TS-1.c"));
        Assert.Equal("ST-2", Strang(p, "TS-1"));
    }

    [Fact]
    public void Verbinden_FormteilMitEigenemStrang_BleibtUnveraendert()
    {
        var p = MitTStueck("ST-1", null, null);
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "TS-1", ["teilVon"] = "ST-2" }));
        var e = Ok(Verb(p, "RO-A.ende", "TS-1.a"));
        Assert.Equal("ST-2", Strang(p, "TS-1"));
        Assert.DoesNotContain(e.Hinweise, h => h.Contains("gehört jetzt"));
    }

    [Fact]
    public void Verbinden_LeitungOhneStrang_LaesstDasFormteilOhneStrang()
    {
        var p = MitTStueck(null, null, null);
        Ok(Verb(p, "RO-A.ende", "TS-1.a"));
        Assert.Null(Hole(p, "TS-1").TeilVon);
    }

    [Fact]
    public void Verbinden_ReihenfolgeEgal_FormteilGegenLeitungUndUmgekehrt()
    {
        var p = MitTStueck(null, "ST-2", null);
        Ok(Verb(p, "TS-1.b", "RO-B.anfang"));
        Assert.Equal("ST-2", Strang(p, "TS-1"));
    }

    [Fact]
    public void Verbinden_VerschiedeneStraenge_DieDurchgangsleitungenEntscheiden()
    {
        // a und c hängen ohne Strang am T-Stück; danach bekommen sie ihre Stränge (element_aendern ändert die Zuordnung, verbindet nicht neu)
        var p = MitTStueck(null, "ST-1", null);
        Ok(Verb(p, "RO-A.ende", "TS-1.a"));
        Ok(Verb(p, "RO-C.anfang", "TS-1.c"));
        Assert.Null(Hole(p, "TS-1").TeilVon);
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "RO-A", ["teilVon"] = "ST-1" }));
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "RO-C", ["teilVon"] = "ST-2" }));
        // Durchgang b (ST-1) kommt dazu: a und b = ST-1, Abzweig c = ST-2 → ST-1
        Ok(Verb(p, "TS-1.b", "RO-B.anfang"));
        Assert.Equal("ST-1", Strang(p, "TS-1"));
    }

    [Fact]
    public void Verbinden_VerschiedeneStraengeAmDurchgang_KeineZuordnungMitHinweis()
    {
        var p = MitTStueck(null, "ST-2", null);
        Ok(Verb(p, "RO-A.ende", "TS-1.a"));
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "RO-A", ["teilVon"] = "ST-1" }));
        var e = Ok(Verb(p, "TS-1.b", "RO-B.anfang"));    // a = ST-1, b = ST-2
        Assert.Null(Hole(p, "TS-1").TeilVon);
        var h = Assert.Single(e.Hinweise, x => x.Contains("TS-1") && x.Contains("verschiedener Stränge"));
        Assert.Contains("ST-1", h); Assert.Contains("ST-2", h);
        Assert.Contains("teilVon", h);   // Weg zur Lösung
        // von Hand festlegen
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "TS-1", ["teilVon"] = "ST-1" }));
        Assert.Equal("ST-1", Strang(p, "TS-1"));
    }

    [Fact]
    public void Verbinden_VerschiedeneStraenge_DurchgangMitStrangGewinntGegenAbzweig()
    {
        // a = ST-1, c = ST-2, b ohne Strang: beide Stränge, Durchgang eindeutig (nur a) → ST-1
        var p = MitTStueck(null, null, null);
        Ok(Verb(p, "RO-A.ende", "TS-1.a"));
        Ok(Verb(p, "RO-C.anfang", "TS-1.c"));
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "RO-A", ["teilVon"] = "ST-1" }));
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "RO-C", ["teilVon"] = "ST-2" }));
        Ok(Verb(p, "TS-1.b", "RO-B.anfang"));   // RO-B ohne Strang
        Assert.Equal("ST-1", Strang(p, "TS-1"));
        // anderer Fall: b = ST-1 (Durchgang), c = ST-2 (Abzweig), a ohne Strang
        var q = MitTStueck(null, null, null);
        Ok(Verb(q, "RO-A.ende", "TS-1.a"));
        Ok(Verb(q, "RO-C.anfang", "TS-1.c"));
        Ok(q.Ausfuehren("element_aendern", new JsonObject { ["element"] = "RO-C", ["teilVon"] = "ST-2" }));
        Ok(q.Ausfuehren("element_aendern", new JsonObject { ["element"] = "RO-B", ["teilVon"] = "ST-1" }));
        var e = Ok(Verb(q, "TS-1.b", "RO-B.anfang"));   // Durchgang b eindeutig → ST-1
        Assert.Equal("ST-1", Strang(q, "TS-1"));
        Assert.Contains(e.Hinweise, x => x.Contains("gehört jetzt"));
    }

    [Fact]
    public void Verbinden_Trockenlauf_AendertNichts_UndRueckgaengigNimmtDenStrangZurueck()
    {
        var p = MitTStueck("ST-1", null, null);
        var vorher = Inhalt(p);
        var t = Ok(Verb(p, "RO-A.ende", "TS-1.a", trocken: true));
        Assert.True(t.Trockenlauf);
        Assert.Contains(t.Hinweise, h => h.Contains("ST-1"));   // zeigt die Wirkung
        Assert.Equal(vorher, Inhalt(p));
        Assert.Null(Hole(p, "TS-1").TeilVon);

        Ok(Verb(p, "RO-A.ende", "TS-1.a"));
        Assert.Equal("ST-1", Strang(p, "TS-1"));
        Ok(p.Rueckgaengig());
        Assert.Null(Hole(p, "TS-1").TeilVon);
        Assert.Null(p.Modell.VerbindungAn(Hole(p, "TS-1").Id, "a"));
    }

    [Fact]
    public void LeitungZiehen_MitVon_FormteilUebernimmtDenStrang()
    {
        var p = Projekt2Straenge();
        Ok(Formteil(p, "FAM-3", "DN 20", "TS-1", [1000, 0, 500]));
        Ok(p.Ausfuehren("leitung_ziehen", new JsonObject
        {
            ["strang"] = "ST-2", ["kennzeichen"] = "RO-Z", ["geschoss"] = "EG", ["dn"] = 20,
            ["punkte"] = Pts([1025, 0, 500], [3000, 0, 500]),
            ["von"] = new JsonObject { ["element"] = "TS-1", ["anschluss"] = "b" },
        }));
        Assert.Equal("ST-2", Strang(p, "TS-1"));
    }

    [Fact]
    public void Routing_LeitungZeichnenAnEinzelnesFormteil_FormteilUebernimmtDenStrang()
    {
        var p = Projekt2Straenge();
        Ok(Formteil(p, "FAM-3", "DN 20", "TS-1", [1000, 0, 500]));
        Ok(Zeichnen(p, "DN 20", [[1025, 0, 500], [3000, 0, 500]], new JsonObject
        {
            ["strang"] = "ST-1", ["von"] = new JsonObject { ["element"] = "TS-1", ["anschluss"] = "b" },
        }));
        Assert.Equal("ST-1", Strang(p, "TS-1"));
        // und in der Materialliste je Strang
        var ml = Ableitungen.Materialliste(p, strang: "ST-1");
        Assert.Equal(1, (int)Json.Zahl(ml["formteileTotal"])!);
    }

    [Fact]
    public void Routing_ZweitesFormteilMitStrangImZug_BleibtBeimStrangDesZugs()
    {
        // Das Routing legt seine Formteile mit dem Strang des Zugs an; sie ändern sich durch das Verbinden nicht
        var p = Projekt2Straenge();
        Ok(Zeichnen(p, "DN 20", [[0, 0, 500], [2000, 0, 500], [2000, 1500, 500]], new JsonObject { ["strang"] = "ST-1" }));
        var bogen = p.Modell.Elemente.Where(e => e.Klasse == "Bogen").ToList();
        Assert.NotEmpty(bogen);
        Assert.All(bogen, b => Assert.Equal("ST-1", p.Modell.Finde(b.TeilVon)?.Kennzeichen));
    }

    [Fact]
    public void Armaturen_BleibenUnveraendert_NurFormteileUebernehmenDenStrang()
    {
        // ANNAHME (Prüfliste): nur Formteile; die Regel greift nicht für Geräte
        var p = MitTStueck("ST-1", null, null);
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Wasserzaehler", ["kennzeichen"] = "WZ-X", ["system"] = "KW", ["geschoss"] = "EG",
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([2000, 2000, 500]) },
        }));
        Ok(RohrIn(p, "RO-W", "ST-1", [2000, 2000, 500], [2500, 2000, 500]));
        Ok(Verb(p, "WZ-X.aus", "RO-W.anfang"));
        Assert.Null(Hole(p, "WZ-X").TeilVon);
    }
}
