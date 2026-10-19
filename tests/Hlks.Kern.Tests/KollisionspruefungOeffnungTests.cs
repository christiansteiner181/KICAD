using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.IfcNetzFassung2Testdatei;
using static Hlks.Kern.Tests.KollisionspruefungHilfe;
using S = Hlks.Kern.Tests.NetzdateiTestschreiber;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3b, B2: Leitung in einer Architektenöffnung (Netzdatei Fassung 2, Testdatei von B1b: Wand W-A 6000 × 200 × 2600 mit
/// Durchbruch O-Leer x 1000 … 1600, z 1000 … 1600 und Fensteröffnung O-Fenster mit Fenster F-1) ist keine Kollision, wenn der ganze
/// Querschnitt samt Dämmung darin liegt – als übernommenes Element (exaktes Netz) und als Hintergrund-Produkt («Unterlage:GlobalId»).
/// Dazu hlks-fremd mit einer Netzdatei von Hand und die Kollisionsliste. Masse FIKTIV (Testrohrserie da = DN + 5).</summary>
public sealed class KollisionspruefungOeffnungTests : IDisposable
{
    readonly List<Projekt> projekte = [];

    public void Dispose()
    {
        foreach (var p in projekte) try { Directory.Delete(Unterlagen.Ordner(p), true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>B1b-Testdatei übernehmen (bereiche null = alles; «raeume» = Bauteile als Hintergrund), System KW, Testrohrserie.</summary>
    Projekt Ifc(string? bereich = null)
    {
        var p = IfcHilfe.Leer();
        projekte.Add(p);
        Ok(IfcHilfe.Uebernehmen(p, Datei("IFC4"), bereich is null ? null : new JsonObject { ["bereiche"] = new JsonArray(bereich) }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "KW", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Trinkwasser kalt" } }));
        Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Modul3Hilfe.Datei("test_rohrserie") }));
        return p;
    }

    static void Rohr(Projekt p, string kz, string dn, double[] a, double[] b)
    {
        var fam = p.Modell.DerKlasse("Familie").Single();   // nur die Testrohrserie
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Rohr", ["kennzeichen"] = kz, ["system"] = "KW", ["eigenschaften"] = new JsonObject { ["familientyp"] = dn },
            ["bezuege"] = new JsonObject { ["familie"] = fam.Kennzeichen }, ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Pts(a, b) },
        }));
    }

    static Element Wand(Projekt p) => p.Modell.Elemente.Single(e => e.Kennungen?.GetValueOrDefault("ifc") == Gid("W-A"));

    [Theory]
    [InlineData(null)]
    [InlineData("raeume")]
    public void LeitungDurchArchitektenoeffnung_KeineKollision_MassivDurchdringung(string? bereich)
    {
        var p = Ifc(bereich);
        var ul = p.Modell.DerKlasse("Unterlage").Single();
        string wand = bereich is null ? Wand(p).Id : $"{ul.Id}:{Gid("W-A")}";
        Rohr(p, "RO-1", "DN 50", [1300, -500, 1300], [1300, 700, 1300]);     // Ø 55 in O-Leer (600 × 600)
        Rohr(p, "RO-2", "DN 50", [2500, -500, 1300], [2500, 700, 1300]);     // massive Stelle
        var e = Ok(Pruefen(p, new JsonObject { ["paare"] = Par("hlks-architektur") }));
        var k = Assert.Single(Kollisionen(p));
        Assert.Equal("RO-2", p.Modell.Finde(Beteiligte(k).First(x => !x.Contains(':') && p.Modell.Finde(x)?.Klasse == "Rohr"))!.Kennzeichen);
        Assert.Equal("durchdringung", k.Text("typ"));
        Assert.Equal(wand, k.Text("bauteil"));
        Assert.Contains(wand, Beteiligte(k));
        Assert.Equal([2500, 100, 1300], Lage(k));                          // Mitte des Durchgangs (Wand y 0 … 200)
        Assert.Equal(127.5, k.Zahl("mass"));                                // r + halbe Dicke = 27.5 + 100
        Assert.Contains("1 in Architektenöffnung", Hinweis(e, "Keine Kollision:"));
    }

    [Fact]
    public void Oeffnung_GanzerQuerschnittMitDaemmung_SonstDurchdringung()
    {
        var p = Ifc();
        // DN 20 (r 12.5) bei x = 1030: Rand bei 1017.5 – in O-Leer (x ≥ 1000)
        Rohr(p, "RO-1", "DN 20", [1030, -500, 1300], [1030, 700, 1300]);
        Ok(Pruefen(p, new JsonObject { ["paare"] = Par("hlks-architektur") }));
        Assert.Empty(Kollisionen(p));
        // mit Dämmung 20: Rand bei 1030 − 32.5 = 997.5 < 1000 → ragt 2.5 mm in die Wand → Durchdringung
        Daemmung(p, "RO-1", 20);
        Ok(Pruefen(p, new JsonObject { ["paare"] = Par("hlks-architektur") }));
        Assert.Equal("durchdringung", Assert.Single(Kollisionen(p)).Text("typ"));
        // DN 50 bei x = 1020 ohne Dämmung: Rand bei 992.5 → Durchdringung
        Daemmung(p, "RO-1", 0);
        Rohr(p, "RO-2", "DN 50", [1020, -500, 1450], [1020, 700, 1450]);
        Ok(Pruefen(p, new JsonObject { ["paare"] = Par("hlks-architektur") }));
        var ks = Kollisionen(p);
        Assert.Equal("behoben", ks.Single(x => x.Name!.StartsWith("RO-1")).Text("status"));
        Assert.Equal("neu", ks.Single(x => x.Name!.StartsWith("RO-2")).Text("status"));
    }

    [Fact]
    public void GefuellteOeffnung_WandGedeckt_FensterIstKollision()
    {
        var p = Ifc();
        // O-Fenster x 3000 … 4200, z 900 … 2100, gefüllt mit Fenster F-1 (60 mm dick in der Wandmitte)
        Rohr(p, "RO-1", "DN 20", [3600, -500, 1500], [3600, 700, 1500]);
        var e = Ok(Pruefen(p, new JsonObject { ["paare"] = Par("hlks-architektur") }));
        var k = Assert.Single(Kollisionen(p));
        var fenster = p.Modell.Elemente.Single(x => x.Kennungen?.GetValueOrDefault("ifc") == Gid("F-1"));
        Assert.Contains(fenster.Id, Beteiligte(k));
        Assert.Equal("kontakt", k.Text("typ"));                             // ein Fenster ist kein Bauteil für Aussparungen
        Assert.Null(k.Text("bauteil"));
        Assert.Contains("1 in Architektenöffnung", Hinweis(e, "Keine Kollision:"));
    }

    [Fact]
    public void OffenesDeckennetz_QuerungUeberDurchstosspunkte_Durchdringung()
    {
        var p = Ifc("raeume");
        var ul = p.Modell.DerKlasse("Unterlage").Single();
        // Decke nur aus Ober- und Unterseite (z 2750 und 3000, 2 × 2 Dreiecke, keine Seiten): nicht wasserdicht, kein «innen»
        float[] pu = [0, 0, 2750, 4000, 0, 2750, 4000, 4000, 2750, 0, 4000, 2750, 0, 0, 3000, 4000, 0, 3000, 4000, 4000, 3000, 0, 4000, 3000];
        int[] dr = [0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6];
        S.AlsNetzDerUnterlage(p, 2, [new S.Satz("3DECKEOFFEN".PadRight(22, '0'), "IfcSlab", pu, dr, Kote: 3000)]);
        Rohr(p, "RO-1", "DN 20", [2000, 2000, 2000], [2000, 2000, 3500]);   // senkrecht hindurch
        Ok(Pruefen(p, new JsonObject { ["paare"] = Par("hlks-architektur") }));
        var k = Assert.Single(Kollisionen(p));
        Assert.Equal("durchdringung", k.Text("typ"));
        Assert.Equal($"{ul.Id}:{"3DECKEOFFEN".PadRight(22, '0')}", k.Text("bauteil"));
        Assert.Equal([2000, 2000, 2875], Lage(k));                          // Mitte zwischen den Durchstosspunkten
        Assert.Equal(12.5, k.Zahl("mass"));                                 // Kapsel gegen Fläche: r − 0
        // Durchgang der Hilfsfunktion: Tiefe 250, senkrecht
        var menge = Pruefkoerperbau.Erzeugen(p);
        var g = Assert.Single(Kollisionssuche.OffeneQuerungen(menge.Koerper.Single(x => x.Kennzeichen == "RO-1"), menge.Koerper.Single(x => x.GlobalId == "3DECKEOFFEN".PadRight(22, '0')).Netz!));
        Assert.Equal((250, 90), (Math.Round(g.Tiefe, 3), Math.Round(g.Winkel, 3)));
    }

    [Fact]
    public void HlksFremd_UndUnterlagenfilter_NetzdateiVonHand()
    {
        var p = Ifc("raeume");
        var ul = p.Modell.DerKlasse("Unterlage").Single();
        S.AlsNetzDerUnterlage(p, 2,
        [
            S.Quader("3WANDX", "IfcWall", [0, 0, 0], [6000, 200, 2600]),
            S.Quader("3KABELTRASSE", "IfcCableCarrierSegment", [500, 1000, 1200], [5500, 1300, 1300]),
        ]);
        Rohr(p, "RO-1", "DN 20", [3000, -500, 1250], [3000, 1600, 1250]);   // durch Wand und Kabeltrasse
        Ok(Pruefen(p));                                                      // Vorgabe: ohne hlks-fremd
        var k = Assert.Single(Kollisionen(p));
        Assert.Equal($"{ul.Id}:{"3WANDX".PadRight(22, '0')}", k.Text("bauteil"));
        Ok(Pruefen(p, new JsonObject { ["paare"] = Par("hlks-fremd") }));
        var ks = Kollisionen(p);
        Assert.Equal(2, ks.Count);
        var fremd = ks.Single(x => x.Name!.Contains("IfcCableCarrierSegment"));
        Assert.Equal("kontakt", fremd.Text("typ"));
        Assert.Contains($"{ul.Id}:{"3KABELTRASSE".PadRight(22, '0')}", Beteiligte(fremd));
        Assert.EndsWith("Lauf 2 · hlks-fremd", fremd.Text("pruefung"));
        Assert.Equal("neu", ks.Single(x => x != fremd).Text("status"));      // Wand: ausserhalb des Laufs
        // Liste: Entität aus der Netzdatei, Filter nach Element und Typ
        var liste = (JsonObject)Tuer.Abfrage(p, "kollisionen", new JsonObject { ["typ"] = "kontakt" });
        Assert.Equal(1, liste["anzahl"]!.GetValue<int>());
        var zeile = liste["zeilen"]![0]!;
        Assert.Equal(fremd.Kennzeichen, zeile["kollision"]!.GetValue<string>());
        Assert.Contains("IfcCableCarrierSegment 3KABELTRASSE", zeile["beteiligte"]!.GetValue<string>());
        Assert.Contains("RO-1 (Rohr)", zeile["beteiligte"]!.GetValue<string>());
        Assert.Equal("IfcCableCarrierSegment", zeile["beteiligteListe"]!.AsArray().Single(x => x!["entitaet"] is not null)!["entitaet"]!.GetValue<string>());
        var nachWand = (JsonObject)Tuer.Abfrage(p, "kollisionen", new JsonObject { ["element"] = "3WANDX".PadRight(22, '0') });
        Assert.Equal("durchdringung", nachWand["zeilen"]![0]!["typ"]!.GetValue<string>());
        Assert.Contains("IfcWall", nachWand["zeilen"]![0]!["bauteil"]!.GetValue<string>());
        // Unterlagenfilter: eine andere (leere) Auswahl gibt es nicht – mit der einzigen Unterlage dasselbe Ergebnis
        Ok(Pruefen(p, new JsonObject { ["paare"] = Par("hlks-architektur", "hlks-fremd"), ["unterlagen"] = Par(ul.Kennzeichen) }));
        Assert.All(Kollisionen(p), x => Assert.Equal("neu", x.Text("status")));
    }
}
