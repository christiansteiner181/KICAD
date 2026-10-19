using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.IfcHilfe;
using static Hlks.Kern.Tests.IfcNetzFassung2Testdatei;
using S = Hlks.Kern.Tests.NetzdateiTestschreiber;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3b, B1b: Prüfkörper aus der Netzdatei Fassung 2 – Teile mit eigenem Ganzen nicht doppelt (Beziehung statt der
/// Lage-Heuristik aus B1), Ganze ohne eigenen Körper aus ihren Teilen (auch als exaktes Netz des übernommenen Elements: die
/// Decken der Bernstrasse), Öffnungen je Wirt (Pruefkoerpermenge.OeffnungenVon) für B2 und B3; Netzdateien Fassung 1 mit der
/// Heuristik und einem Hinweis zum Erneuern.</summary>
public sealed class KollisionTeileOeffnungenTests : IDisposable
{
    readonly List<Projekt> projekte = [];

    public void Dispose()
    {
        foreach (var p in projekte) try { Directory.Delete(Unterlagen.Ordner(p), true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    Projekt Uebernommen(string schema, params string[] bereiche)
    {
        var p = Leer();
        projekte.Add(p);
        Ok(Uebernehmen(p, Datei(schema), bereiche.Length == 0 ? null : new JsonObject { ["bereiche"] = new JsonArray(bereiche.Select(b => (JsonNode)b).ToArray()) }));
        return p;
    }

    static Element Mit(Projekt p, string name) => p.Modell.Elemente.Single(e => e.Kennungen?.GetValueOrDefault("ifc") == Gid(name));

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Hintergrund_TeileMitGanzemNichtDoppelt_GanzeOhneKoerperAusIhrenTeilen(string schema)
    {
        var p = Uebernommen(schema, "raeume");   // Architektur nicht übernommen: alles Hintergrund (wie die Revit-Verknüpfung)
        var ul = p.Modell.DerKlasse("Unterlage").Single();
        var m = Pruefkoerperbau.Erzeugen(p);
        var hg = m.Koerper.Where(k => k.Quelle == "hintergrund").ToList();
        // W-A, F-1, W-B (mit eigenem Körper), W-C (aus Teilen), D-T (aus Teilen, ohne Repräsentation) – keine Teile, keine Öffnungen
        Assert.Equal([Gid("D-T"), Gid("F-1"), Gid("W-A"), Gid("W-B"), Gid("W-C")], hg.Select(k => k.GlobalId!).Order());
        Assert.DoesNotContain(m.Koerper, k => k.Entitaet == Pruefkoerperbau.Teilentitaet);
        Assert.DoesNotContain(m.Koerper, k => k.Entitaet?.StartsWith("IfcOpening") == true);
        // auch der Putz ausserhalb des Körpers von W-B entfällt (die Lage-Heuristik aus B1 hätte ihn behalten)
        Assert.DoesNotContain(m.Koerper, k => k.GlobalId == Gid("Putz WB"));
        Assert.Contains(m.Hinweise, h => h.Contains("3 Teile nicht einzeln geprüft"));
        // ein Teil ohne Zerlegung in der IFC-Datei (wie im Revit-Export): Lage-Heuristik, es liegt ganz in W-A
        Assert.DoesNotContain(m.Koerper, k => k.GlobalId == Gid("Putz frei"));
        Assert.Contains(m.Hinweise, h => h.Contains("1 Hintergrund-Teile"));
        var wb = hg.Single(k => k.GlobalId == Gid("W-B"));
        Assert.Equal(12, wb.Netz!.AnzahlDreiecke);
        Assert.Equal($"{ul.Id}:{Gid("W-B")}", wb.Kennung);
        var dt = hg.Single(k => k.GlobalId == Gid("D-T"));
        Assert.Equal(24, dt.Netz!.AnzahlDreiecke);
        Assert.Equal("IfcSlab", dt.Entitaet);
        Assert.Equal("architektur", dt.Bereich);
        Assert.Equal(new Huelle3(new V3(0, 0, 2750), new V3(6000, 4500, 3000)), dt.Huelle);
        Assert.Equal(p.Modell.DerKlasse("Geschoss").Single(g => g.Zahl("kote") == 3000).Id, dt.Geschoss);
        // die Heuristik betrifft nur Teile ohne Bezug: ausgeschaltet kommt nur «Putz frei» dazu, die Teile von W-B bleiben weg
        var ohne = Pruefkoerperbau.Erzeugen(p, new PruefkoerperOptionen { DoppelteTeileWeglassen = false });
        Assert.Equal(m.Koerper.Select(k => k.Kennung).Append($"{ul.Id}:{Gid("Putz frei")}").Order(), ohne.Koerper.Select(k => k.Kennung).Order());
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Uebernommen_ExakteNetze_AuchDeckeAusTeilen_KeineTeilePruefkoerper(string schema)
    {
        var p = Uebernommen(schema);
        var m = Pruefkoerperbau.Erzeugen(p);
        Assert.DoesNotContain(m.Koerper, k => k.Quelle == "hintergrund");
        foreach (var (name, dreiecke) in new[] { ("W-A", 12), ("W-B", 12), ("W-C", 24), ("D-T", 24) })
        {
            var e = Mit(p, name);
            var k = m.Koerper.Single(x => x.ElementId == e.Id);
            Assert.Equal("netzdatei", k.Quelle);   // B1: die Decke ohne Repräsentation hatte nur den KICAD-Körper
            Assert.Equal(dreiecke, k.Netz!.AnzahlDreiecke);
        }
        var decke = m.Koerper.Single(x => x.ElementId == Mit(p, "D-T").Id);
        Assert.Equal("Decke", decke.Klasse);
        Assert.Equal(new Huelle3(new V3(0, 0, 2750), new V3(6000, 4500, 3000)), decke.Huelle);
        Assert.True(decke.Netz!.HatGeschlosseneTeile);
        Assert.DoesNotContain(m.Hinweise, h => h.Contains("Teile nicht einzeln"));   // übernommene Teile zählen nicht als Hintergrund
        Assert.DoesNotContain(m.Hinweise, h => h.Contains("Fassung 1"));
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Oeffnungen_JeWirt_MitFuellung_UndLogischAbzuziehen(string schema)
    {
        var p = Uebernommen(schema);
        var ul = p.Modell.DerKlasse("Unterlage").Single();
        var m = Pruefkoerperbau.Erzeugen(p);
        Assert.Equal(7, m.Oeffnungen.Count);
        var wa = Mit(p, "W-A");
        var oa = m.OeffnungenVon(wa.Id);
        Assert.Equal([Gid("O-Fenster"), Gid("O-Frei"), Gid("O-Leer")], oa.Select(o => o.GlobalId).Order());
        Assert.Same(oa, m.OeffnungenVon(m.Koerper.Single(k => k.ElementId == wa.Id)));
        var leer = oa.Single(o => o.GlobalId == Gid("O-Leer"));
        Assert.False(leer.Gefuellt);
        Assert.False(leer.WirtAusLage);
        Assert.Equal(Gid("W-A"), leer.WirtGlobalId);
        Assert.Null(leer.Fuellung);
        Assert.Equal(ul.Id, leer.UnterlageId);
        Assert.Equal([wa.Id], leer.WirtKennungen);
        // ohne IfcRelVoidsElement: Wirt aus der Lage (Mitte im Netz von W-A); im Leeren: kein Wirt
        var frei = oa.Single(o => o.GlobalId == Gid("O-Frei"));
        Assert.True(frei.WirtAusLage);
        Assert.Null(frei.WirtGlobalId);
        Assert.Null(frei.BauteilGlobalId);
        Assert.Equal([wa.Id], frei.WirtKennungen);
        var nirgends = m.Oeffnungen.Single(o => o.GlobalId == Gid("O-Frei-Leer"));
        Assert.False(nirgends.WirtAusLage);
        Assert.Empty(nirgends.WirtKennungen);
        Assert.Equal(new Huelle3(new V3(1000, -100, 1000), new V3(1600, 300, 1600)), leer.Huelle);
        Assert.True(leer.Netz.Geschlossen);
        var fenster = oa.Single(o => o.Gefuellt);
        Assert.Equal(Gid("F-1"), fenster.Fuellung);
        Assert.Equal(Mit(p, "F-1").Kennungen!["ifc"], fenster.Fuellung);   // die Füllung ist das übernommene Fenster
        // Wirt ein Teil von W-B (Ganzes mit eigenem Körper): die Öffnung gilt für W-B
        var ot = Assert.Single(m.OeffnungenVon(Mit(p, "W-B").Id));
        Assert.Equal(Gid("Beton WB"), ot.WirtGlobalId);
        Assert.Equal(Gid("W-B"), ot.BauteilGlobalId);
        // Wand aus Teilen und Decke ohne Repräsentation
        Assert.Equal(Gid("O-C"), Assert.Single(m.OeffnungenVon(Mit(p, "W-C").Id)).GlobalId);
        var od = Assert.Single(m.OeffnungenVon(Mit(p, "D-T").Id));
        Assert.Equal(3000, od.GeschossKote);
        // über die GlobalId (auch Wirt-GlobalId des Teils)
        Assert.Equal(2, m.OeffnungenVonGlobalId(Gid("W-A")).Count);
        Assert.Same(ot, Assert.Single(m.OeffnungenVonGlobalId(Gid("Beton WB"))));
        Assert.Same(ot, Assert.Single(m.OeffnungenVonGlobalId(Gid("W-B"))));
        Assert.Empty(m.OeffnungenVon("gibt-es-nicht"));
        Assert.Empty(m.OeffnungenVon(Mit(p, "F-1").Id));

        // logisch abziehen (B2): das Netz der Wand ist nicht ausgeschnitten – eine Achse durch O-Leer quert die Wand, der Durchgang
        // liegt aber ganz in der Öffnung; an massiver Stelle liegt er in keiner Öffnung
        var wand = m.Koerper.Single(k => k.ElementId == wa.Id).Netz!;
        var g = Assert.Single(Durchgaenge.Finden(wand, new V3(1300, -500, 1300), new V3(1300, 700, 1300)));
        Assert.Equal(200, g.Tiefe, 3);
        Assert.True(leer.Enthaelt(g.Eintritt) && leer.Enthaelt(g.Austritt) && leer.Enthaelt(g.Mitte));
        var massiv = Assert.Single(Durchgaenge.Finden(wand, new V3(2500, -500, 1300), new V3(2500, 700, 1300)));
        Assert.DoesNotContain(oa, o => o.Enthaelt(massiv.Mitte));
        // Deckendurchbruch: senkrechte Achse durch O-Decke
        var dn = m.Koerper.Single(k => k.ElementId == Mit(p, "D-T").Id).Netz!;
        var gd = Assert.Single(Durchgaenge.Finden(dn, new V3(1250, 1250, 2000), new V3(1250, 1250, 4000)));
        Assert.Equal(250, gd.Tiefe, 3);
        Assert.True(od.Enthaelt(gd.Mitte));

        // ohne Öffnungen
        Assert.Empty(Pruefkoerperbau.Erzeugen(p, new PruefkoerperOptionen { Oeffnungen = false }).Oeffnungen);
        // Auswahl ohne W-A: die Öffnungen sind da, aber ohne Prüfkörper ihres Wirts
        var nurDecke = Pruefkoerperbau.Erzeugen(p, new PruefkoerperOptionen { Elemente = [Mit(p, "D-T").Id] });
        Assert.Equal(7, nurDecke.Oeffnungen.Count);
        Assert.Empty(nurDecke.OeffnungenVon(wa.Id));
        Assert.Empty(nurDecke.Oeffnungen.Single(o => o.GlobalId == Gid("O-Leer")).WirtKennungen);
        // O-Frei (ohne Wirt in der Datei): ohne W-A liegt ihre Mitte nur noch im Hintergrund-Teil «Putz frei» (die Lage-Heuristik
        // hat ohne W-A kein Ganzes für es)
        Assert.Equal([$"{ul.Id}:{Gid("Putz frei")}"], nurDecke.Oeffnungen.Single(o => o.GlobalId == Gid("O-Frei")).WirtKennungen);
        Assert.Single(nurDecke.OeffnungenVon(Mit(p, "D-T").Id));
    }

    [Fact]
    public void Oeffnungen_HintergrundWirt_UndInKicadGeaenderterWirt()
    {
        // Hintergrund: Kennung «Unterlage-Id:GlobalId»
        var p = Uebernommen("IFC4", "raeume");
        var ul = p.Modell.DerKlasse("Unterlage").Single();
        var m = Pruefkoerperbau.Erzeugen(p);
        Assert.Equal(3, m.OeffnungenVon($"{ul.Id}:{Gid("W-A")}").Count);   // O-Leer, O-Fenster und O-Frei (aus der Lage)
        Assert.Single(m.OeffnungenVon($"{ul.Id}:{Gid("W-B")}"));
        Assert.Single(m.OeffnungenVon($"{ul.Id}:{Gid("D-T")}"));
        // in KICAD geänderte Wand (KICAD-Körper): die Öffnungen der IFC-Fassung hängen weiter an ihr (B2 entscheidet, ob sie passen)
        var q = Uebernommen("IFC4");
        var wa = Mit(q, "W-A");
        Ok(q.Ausfuehren("element_aendern", new JsonObject { ["element"] = wa.Kennzeichen, ["eigenschaften"] = new JsonObject { ["tragend"] = true } }));
        var mq = Pruefkoerperbau.Erzeugen(q);
        Assert.Equal("koerper", mq.Koerper.Single(k => k.ElementId == wa.Id).Quelle);
        Assert.Contains(mq.OeffnungenVon(wa.Id), o => o.GlobalId == Gid("O-Leer"));
        Assert.Contains(mq.OeffnungenVon(wa.Id), o => o.GlobalId == Gid("O-Fenster"));
    }

    [Fact]
    public void Teile_OhneGanzesInDerDatei_BildenDenPruefkoerperDesGanzen()
    {
        // Netzdatei von Hand (Fassung 2), wie ein fremder Schreiber sie liefern könnte: Teile, deren Ganzes fehlt
        var p = Uebernommen("IFC4");
        var ul = p.Modell.DerKlasse("Unterlage").Single();
        var decke = Mit(p, "D-T");
        string ganz = "3GANZOHNENETZ";
        S.AlsNetzDerUnterlage(p, 2,
        [
            // Decke (Element) fehlt in der Datei, ihre zwei Teile sind übernommen → exaktes Netz der Decke aus den Teilen
            S.Quader("3PLATTEA", "IfcBuildingElementPart", [0, 0, 2750], [6000, 4500, 2950], uebernommen: true, rolle: IfcNetzRolle.Teil, bezug: Gid("D-T")),
            S.Quader("3PLATTEB", "IfcBuildingElementPart", [0, 0, 2950], [6000, 4500, 3000], uebernommen: true, rolle: IfcNetzRolle.Teil, bezug: Gid("D-T")),
            // Hintergrund: Ganzes fehlt → ein Prüfkörper unter seiner GlobalId aus beiden Teilen
            S.Quader("3TEIL1", "IfcBuildingElementPart", [10000, 0, 0], [12000, 150, 2600], rolle: IfcNetzRolle.Teil, bezug: ganz),
            S.Quader("3TEIL2", "IfcBuildingElementPart", [10000, 150, 0], [12000, 200, 2600], rolle: IfcNetzRolle.Teil, bezug: ganz),
            // Hintergrund: Ganzes mit eigenem Netz → sein Teil zählt nicht
            S.Quader("3GANZMITNETZ", "IfcWall", [20000, 0, 0], [22000, 200, 2600]),
            S.Quader("3TEIL3", "IfcBuildingElementPart", [20000, 0, 0], [22000, 150, 2600], rolle: IfcNetzRolle.Teil, bezug: "3GANZMITNETZ"),
            // Öffnung im Teil 1: gilt für das zusammengesetzte Ganze
            S.Quader("3OEFFNUNG", "IfcOpeningElement", [10500, -100, 500], [11000, 300, 1000], rolle: IfcNetzRolle.Oeffnung, bezug: "3TEIL1"),
            // Teil ohne Bezug (die IFC-Datei nennt kein Ganzes) im Netz von 3GANZMITNETZ: Lage-Heuristik → entfällt
            S.Quader("3TEILFREI", "IfcBuildingElementPart", [20100, 50, 100], [21900, 150, 2500]),
            // ein Körper um das zusammengesetzte Ganze: das Ganze ist kein «Teil ohne Bezug» und bleibt
            S.Quader("3HUELLE", "IfcCovering", [9900, -10, -10], [12100, 210, 2610]),
        ]);
        var m = Pruefkoerperbau.Erzeugen(p);
        var d = m.Koerper.Single(k => k.ElementId == decke.Id);
        Assert.Equal("netzdatei", d.Quelle);
        Assert.Equal(24, d.Netz!.AnzahlDreiecke);
        Assert.Equal(new Huelle3(new V3(0, 0, 2750), new V3(6000, 4500, 3000)), d.Huelle);
        var gz = m.Koerper.Single(k => k.Quelle == "hintergrund" && k.GlobalId == ganz.PadRight(22, '0'));
        Assert.Equal($"{ul.Id}:{ganz.PadRight(22, '0')}", gz.Kennung);
        Assert.Equal(24, gz.Netz!.AnzahlDreiecke);
        Assert.Equal(new Huelle3(new V3(10000, 0, 0), new V3(12000, 200, 2600)), gz.Huelle);
        Assert.Equal("architektur", gz.Bereich);
        Assert.Single(m.Koerper, k => k.Quelle == "hintergrund" && k.GlobalId == "3GANZMITNETZ".PadRight(22, '0'));
        Assert.DoesNotContain(m.Koerper, k => k.GlobalId is { } g && g.StartsWith("3TEIL"));
        Assert.Single(m.Koerper, k => k.GlobalId == "3HUELLE".PadRight(22, '0'));
        Assert.Contains(m.Hinweise, h => h.Contains("1 Teile nicht einzeln geprüft"));
        Assert.Contains(m.Hinweise, h => h.Contains("1 Hintergrund-Teile"));   // 3TEILFREI nach Lage
        Assert.Contains(m.Hinweise, h => h.Contains("2 Bauteile ohne eigenen Körper aus ihren Teilen"));
        var o = Assert.Single(m.Oeffnungen);
        Assert.Equal("3TEIL1".PadRight(22, '0'), o.WirtGlobalId);
        Assert.Equal(ganz.PadRight(22, '0'), o.BauteilGlobalId);
        Assert.Equal([gz.Kennung], o.WirtKennungen);
        Assert.Same(o, Assert.Single(m.OeffnungenVon(gz)));
    }

    [Fact]
    public void Fassung1_HeuristikNachLage_HinweisZumErneuern_OhneOeffnungen()
    {
        var p = Uebernommen("IFC4", "raeume");
        S.AlsNetzDerUnterlage(p, 1,
        [
            S.Quader("3WAND", "IfcWall", [0, 0, 0], [6000, 200, 2600]),
            S.Quader("3SCHICHTINNEN", "IfcBuildingElementPart", [0, 0, 0], [6000, 150, 2600]),
            S.Quader("3SCHICHTAUSSEN", "IfcBuildingElementPart", [0, 200, 0], [6000, 215, 2600]),
            S.Quader("3OEFFNUNG", "IfcOpeningElement", [1000, -100, 1000], [1600, 300, 1600], rolle: IfcNetzRolle.Oeffnung, bezug: "3WAND"),
        ]);
        var m = Pruefkoerperbau.Erzeugen(p);
        // wie B1: die Schicht im Körper der Wand entfällt (nach Lage), die ausserhalb bleibt
        Assert.Equal(["3SCHICHTAUSSEN".PadRight(22, '0'), "3WAND".PadRight(22, '0')], m.Koerper.Where(k => k.Quelle == "hintergrund").Select(k => k.GlobalId!).Order());
        Assert.Contains(m.Hinweise, h => h.Contains("Fassung 1") && h.Contains("erneut übernehmen"));
        Assert.Contains(m.Hinweise, h => h.Contains("1 Hintergrund-Teile"));
        Assert.Empty(m.Oeffnungen);   // Fassung 1 kennt keine Öffnungen (der Schreiber lässt sie weg)
        // ohne Heuristik bleiben beide Schichten
        Assert.Equal(3, Pruefkoerperbau.Erzeugen(p, new PruefkoerperOptionen { DoppelteTeileWeglassen = false }).Koerper.Count(k => k.Quelle == "hintergrund"));
    }

    [Fact]
    public void WirtInDerLage_NetzGanz_NetzAusgeschnitten_NurBeruehrt()
    {
        static Pruefkoerper K(params (double[] A, double[] B)[] quader)
        {
            var n = Pruefnetz.Aus(quader.Select(q => NetzBau.Quader(q.A, q.B)));
            return new Pruefkoerper { Kennung = "k", Bereich = "architektur", Quelle = "hintergrund", Netz = n, Huelle = n.Huelle, HuelleOhneDaemmung = n.Huelle };
        }
        static Huelle3 H(double[] a, double[] b) => new(V3.Aus(a), V3.Aus(b));
        var fenster = H([1000, -100, 1000], [1600, 300, 1600]);
        // Wand nicht ausgeschnitten (Archicad): Mitte im Netz
        Assert.True(Pruefkoerperbau.WirtInDerLage(K(([0, 0, 0], [6000, 200, 2600])), fenster));
        // Wand ausgeschnitten (Revit): Mitte im Loch, die Leibung umgibt sie links/rechts und oben/unten
        var ausgeschnitten = K(([0, 0, 0], [1000, 200, 2600]), ([1600, 0, 0], [6000, 200, 2600]), ([1000, 0, 0], [1600, 200, 1000]), ([1000, 0, 1600], [1600, 200, 2600]));
        Assert.Equal(Punktlage.Aussen, ausgeschnitten.Netz!.Lage(new V3(1300, 100, 1300)));
        Assert.True(Pruefkoerperbau.WirtInDerLage(ausgeschnitten, fenster));
        // Türöffnung bis zum Boden: Wand links/rechts/oben ja, Decke darunter (berührt nur) nein
        var tuer = H([1000, -100, 0], [1900, 300, 2100]);
        Assert.True(Pruefkoerperbau.WirtInDerLage(K(([0, 0, 0], [1000, 200, 2600]), ([1900, 0, 0], [6000, 200, 2600]), ([1000, 0, 2100], [1900, 200, 2600])), tuer));
        Assert.False(Pruefkoerperbau.WirtInDerLage(K(([-1000, -1000, -250], [7000, 1000, 0])), tuer));
        // nur von zwei Seiten (L-förmig links und unten): umgibt die Mitte nicht
        Assert.False(Pruefkoerperbau.WirtInDerLage(K(([0, 0, 0], [1000, 200, 2600]), ([1000, 0, 0], [1600, 200, 1000])), fenster));
        // weit weg: Hüllquader enthält die Mitte nicht
        Assert.False(Pruefkoerperbau.WirtInDerLage(K(([8000, 0, 0], [9000, 200, 2600])), fenster));
    }

    [Fact]
    public void Heuristik_NurFuerGewaehlteUnterlagen()
    {
        static Pruefkoerper Netz(string kennung, string unterlage, string entitaet, double[] a, double[] b)
        {
            var n = Pruefnetz.Aus(NetzBau.Quader(a, b));
            return new Pruefkoerper { Kennung = kennung, UnterlageId = unterlage, Entitaet = entitaet, Bereich = "architektur", Quelle = "hintergrund", Netz = n, Huelle = n.Huelle, HuelleOhneDaemmung = n.Huelle };
        }
        var liste = new List<Pruefkoerper>
        {
            Netz("U:W", "U", "IfcWall", [0, 0, 0], [6000, 200, 2600]),
            Netz("U:T", "U", Pruefkoerperbau.Teilentitaet, [0, 0, 0], [6000, 150, 2600]),
            Netz("V:W", "V", "IfcWall", [0, 0, 0], [6000, 200, 2600]),
            Netz("V:T", "V", Pruefkoerperbau.Teilentitaet, [0, 0, 0], [6000, 150, 2600]),
        };
        var hinweise = new List<string>();
        Assert.Equal(1, Pruefkoerperbau.TeileImGanzenWeglassen(liste, hinweise, new HashSet<string> { "U" }));
        Assert.Equal(["U:W", "V:W", "V:T"], liste.Select(k => k.Kennung));
    }
}
