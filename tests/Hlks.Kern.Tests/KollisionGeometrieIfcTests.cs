using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3b, B1: Prüfkörper aus der Netzdatei einer IFC-Unterlage – unverändert übernommene Bauteile mit dem exakten
/// Netz (über die GlobalId), in KICAD geänderte mit dem KICAD-Körper, Hintergrund-Produkte als eigene Prüfkörper mit GlobalId
/// und Entität; Durchgang und Öffnung an der Decke mit Loch der Testdatei; HLKS-Übertrag: Formteile und Apparate exakt,
/// Leitungen um die Achse.</summary>
public sealed class KollisionGeometrieIfcTests : IDisposable
{
    readonly List<Projekt> projekte = [];
    readonly List<string> ordner = [];

    public void Dispose()
    {
        foreach (var p in projekte) try { Directory.Delete(Unterlagen.Ordner(p), true); } catch (IOException) { }
        foreach (var o in ordner) try { Directory.Delete(o, true); } catch (IOException) { }
    }

    static IfcNetzdatei Netzdatei(Projekt p) => IfcNetzdatei.Oeffnen(Unterlagen.Pfad(p, p.Modell.DerKlasse("Unterlage").Single().Text("netz")!));

    static Huelle3 Huelle(float[] b) => new(new V3(b[0], b[1], b[2]), new V3(b[3], b[4], b[5]));

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Unveraendert_ExaktesNetz_HintergrundMitGlobalIdUndEntitaet(string schema)
    {
        var (p, _) = IfcArchitekturTests.Uebernommen(schema);
        var m = Pruefkoerperbau.Erzeugen(p);
        var ul = p.Modell.DerKlasse("Unterlage").Single();
        var nd = Netzdatei(p);
        var sued = IfcArchitekturTests.Namens(p, "Wand", "W-Sued");
        Assert.True(Pruefkoerperbau.UnveraendertUebernommen(sued));
        var w = m.Koerper.Single(k => k.ElementId == sued.Id);
        Assert.Equal("netzdatei", w.Quelle);
        Assert.Equal(sued.Kennungen!["ifc"], w.GlobalId);
        Assert.Equal(ul.Id, w.UnterlageId);
        Assert.Matches("IfcWall", w.Entitaet);
        Assert.Equal("architektur", w.Bereich);
        Assert.Equal("Wand", w.Klasse);
        Assert.Equal(0, w.GeschossKote);
        Assert.Equal(12, w.Netz!.AnzahlDreiecke);   // Körper 6000 × 200 × 2600 wie in der Netzdatei
        Assert.True(w.Netz.Geschlossen);
        Assert.Equal(new Huelle3(new V3(0, 0, 0), new V3(6000, 200, 2600)), w.Huelle);
        Assert.Equal(Huelle(nd.Produkt(sued.Kennungen["ifc"])!.Begrenzung), w.Huelle);
        // jedes unveränderte übernommene Bauteil mit Netz in der Datei hat sein exaktes Netz
        foreach (var e in p.Modell.Elemente.Where(e => Pruefkoerperbau.IstPruefbar(p.Katalog, e) && e.Kennungen?.ContainsKey("ifc") == true))
            if (nd.Produkt(e.Kennungen!["ifc"]) is { } pr)
                Assert.Equal("netzdatei", m.Koerper.Single(k => k.ElementId == e.Id).Quelle);
        // Hintergrund: Objekt-Quader (Proxy) 8000…9000 × 0…1000 × 0…2000, mit GlobalId, Entität, Geschoss
        var obj = m.Koerper.Single(k => k.Quelle == "hintergrund" && k.Entitaet == "IfcBuildingElementProxy");
        Assert.Null(obj.ElementId);
        Assert.Equal($"{ul.Id}:{obj.GlobalId}", obj.Kennung);
        Assert.Equal(22, obj.GlobalId!.Length);
        Assert.Equal("sonst", obj.Bereich);
        Assert.Equal(new Huelle3(new V3(8000, 0, 0), new V3(9000, 1000, 2000)), obj.Huelle);
        Assert.Equal(p.Modell.DerKlasse("Geschoss").Single(g => g.Zahl("kote") == 0).Id, obj.Geschoss);
        Assert.True(obj.Netz!.Geschlossen);
        // Rundwand (nicht übernommen) als Hintergrund im Bereich Architektur
        Assert.Contains(m.Koerper, k => k.Quelle == "hintergrund" && k.Bereich == "architektur" && k.Entitaet!.StartsWith("IfcWall"));
        // so viele Hintergrund-Prüfkörper wie Hintergrund-Produkte in der Netzdatei
        Assert.Equal(nd.Produkte.Count(x => !x.Uebernommen), m.Koerper.Count(k => k.Quelle == "hintergrund"));
        // ohne Hintergrund
        Assert.DoesNotContain(Pruefkoerperbau.Erzeugen(p, new PruefkoerperOptionen { Hintergrund = false }).Koerper, k => k.Quelle == "hintergrund");
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void DeckeMitLoch_ExaktesNetz_DurchgangUndOeffnung(string schema)
    {
        var (p, _) = IfcArchitekturTests.Uebernommen(schema);
        var decke = IfcArchitekturTests.Namens(p, "Decke", "Decke 1.OG");
        var k = Assert.Single(Pruefkoerperbau.Erzeugen(p, new PruefkoerperOptionen { Elemente = [decke.Id], Hintergrund = false }).Koerper);
        Assert.Equal("netzdatei", k.Quelle);
        Assert.True(k.Netz!.Geschlossen);
        // massive Stelle: ein Durchgang 2750 … 3000
        var g = Assert.Single(Durchgaenge.Finden(k.Netz, new V3(4000, 3000, 2000), new V3(4000, 3000, 4000)));
        Assert.Equal(250, g.Tiefe, 3);
        Assert.Equal(2875, g.Mitte.Z, 3);
        // im Loch (1000 … 2000 × 1000 … 2000): kein Durchgang, Öffnung vorhanden
        Assert.Empty(Durchgaenge.Finden(k.Netz, new V3(1500, 1500, 2000), new V3(1500, 1500, 4000)));
        var z = new V3(0, 0, 1);
        Assert.True(Durchgaenge.OeffnungRund(k.Netz, new V3(1500, 1500, 2875), z, 600, 800).Offen);
        Assert.True(Durchgaenge.OeffnungRechteck(k.Netz, new V3(1500, 1500, 2875), z, 600, 1000, 1000, new V3(1, 0, 0)).Offen);
        Assert.False(Durchgaenge.OeffnungRund(k.Netz, new V3(4000, 3000, 2875), z, 600, 300).Offen);
    }

    [Fact]
    public void InKicadGeaendert_KicadKoerper_OhneExakteNetze_AlleKicadKoerper()
    {
        var p = Leer();
        projekte.Add(p);
        Ok(Uebernehmen(p, IfcArchitekturTestdatei.Datei("IFC4", false)));
        var sued = IfcArchitekturTests.Namens(p, "Wand", "W-Sued");
        Assert.Equal("netzdatei", Pruefkoerperbau.Erzeugen(p).Koerper.Single(k => k.ElementId == sued.Id).Quelle);
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = sued.Kennzeichen, ["eigenschaften"] = new JsonObject { ["tragend"] = false } }));
        Assert.False(Pruefkoerperbau.UnveraendertUebernommen(p.Modell.Finde(sued.Id)!));
        var m = Pruefkoerperbau.Erzeugen(p);
        var w = m.Koerper.Single(k => k.ElementId == sued.Id);
        Assert.Equal("koerper", w.Quelle);                 // in KICAD geändert: KICAD-Körper
        Assert.Equal(sued.Kennungen!["ifc"], w.GlobalId);  // Herkunft bleibt sichtbar
        Assert.True(w.Netz!.AnzahlDreiecke > 0);
        Assert.Equal(6000, w.Huelle.Max.X - w.Huelle.Min.X, 0);
        // andere Bauteile bleiben exakt; ohne exakte Netze alle aus dem KICAD-Körper
        Assert.Equal("netzdatei", m.Koerper.Single(k => k.ElementId == IfcArchitekturTests.Namens(p, "Wand", "W-Nord").Id).Quelle);
        Assert.DoesNotContain(Pruefkoerperbau.Erzeugen(p, new PruefkoerperOptionen { ExakteNetze = false }).Koerper, k => k.Quelle == "netzdatei");
    }

    [Fact]
    public void GeteiltesProdukt_ErstesStueckTraegtDasNetz_GeaendertAlleMitKicadKoerper()
    {
        var p = Leer();
        projekte.Add(p);
        Ok(Uebernehmen(p, IfcArchitekturTestdatei.Datei("IFC4", false)));
        var sued = IfcArchitekturTests.Namens(p, "Wand", "W-Sued");
        var ost = IfcArchitekturTests.Namens(p, "Wand", "W-Ost");
        // W-Ost als zweites Stück von W-Sued nachgebildet (Kennung «GlobalId:2» wie IfcBauteile.NeuAbgeleitet bei einer
        // geknickten Wandachse) – nur im Test direkt im Modell gesetzt, die Testdatei hat keine geknickte Wand
        p.Modell.Finde(ost.Id)!.Kennungen!["ifc"] = sued.Kennungen!["ifc"] + ":2";
        var m = Pruefkoerperbau.Erzeugen(p);
        var w = m.Koerper.Single(k => k.ElementId == sued.Id);
        Assert.Equal("netzdatei", w.Quelle);
        Assert.Equal([ost.Id], w.WeitereElemente);
        Assert.DoesNotContain(m.Koerper, k => k.ElementId == ost.Id);   // vom Netz des ganzen Produkts abgedeckt
        // ein Stück in KICAD geändert: beide Stücke mit ihrem KICAD-Körper (das Netz der Datei stimmt nicht mehr)
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = ost.Kennzeichen, ["eigenschaften"] = new JsonObject { ["tragend"] = false } }));
        var m2 = Pruefkoerperbau.Erzeugen(p);
        Assert.Equal("koerper", m2.Koerper.Single(k => k.ElementId == sued.Id).Quelle);
        Assert.Equal("koerper", m2.Koerper.Single(k => k.ElementId == ost.Id).Quelle);
        Assert.Empty(m2.Koerper.Single(k => k.ElementId == sued.Id).WeitereElemente);
    }

    [Fact]
    public void DoppelteTeile_SchichtenImBauteil_WerdenWeggelassen_AndereBleiben()
    {
        // Wand 6000 × 200 × 2600 mit eigenem Körper (übernommen) und ihre zwei Schichten 150 + 50 als Hintergrund-Teile wie in
        // den Netzdateien von Bernstrasse 42; dazu ein Teil, das hinausragt, und eines aus einer anderen Unterlage
        static Pruefkoerper Netz(string kennung, string? element, string unterlage, string? entitaet, double[] a, double[] b)
        {
            var n = Pruefnetz.Aus(NetzBau.Quader(a, b));
            return new Pruefkoerper
            {
                Kennung = kennung, ElementId = element, UnterlageId = unterlage, Entitaet = entitaet, Bereich = "architektur",
                Quelle = element is null ? "hintergrund" : "netzdatei", Netz = n, Huelle = n.Huelle, HuelleOhneDaemmung = n.Huelle,
            };
        }
        var liste = new List<Pruefkoerper>
        {
            Netz("W", "w", "U", "IfcWall", [0, 0, 0], [6000, 200, 2600]),
            Netz("U:T1", null, "U", Pruefkoerperbau.Teilentitaet, [0, 0, 0], [6000, 150, 2600]),
            Netz("U:T2", null, "U", Pruefkoerperbau.Teilentitaet, [0, 150, 0], [6000, 200, 2600]),
            Netz("U:T3", null, "U", Pruefkoerperbau.Teilentitaet, [0, 150, 0], [6000, 260, 2600]),   // ragt 60 hinaus
            Netz("V:T4", null, "V", Pruefkoerperbau.Teilentitaet, [0, 0, 0], [6000, 150, 2600]),     // andere Unterlage
        };
        var hinweise = new List<string>();
        Assert.Equal(2, Pruefkoerperbau.TeileImGanzenWeglassen(liste, hinweise));
        Assert.Equal(["W", "U:T3", "V:T4"], liste.Select(k => k.Kennung));
        Assert.Contains("2 Hintergrund-Teile", Assert.Single(hinweise));
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void HlksUebertrag_FormteilUndApparatExakt_LeitungenUmDieAchse(string schema)
    {
        var o = IfcHlksTestdatei.Ordner();
        ordner.Add(o);
        var datei = IfcHlksTestdatei.Schreiben(schema, Path.Combine(o, "hlks_b1.ifc"), new IfcHlksSpec());
        var p = Leer();
        projekte.Add(p);
        Ok(Uebernehmen(p, datei, new JsonObject { ["bereiche"] = new JsonArray("hlks") }));   // mit Netzdatei
        var nd = Netzdatei(p);
        var m = Pruefkoerperbau.Erzeugen(p);
        Element N(string name) => p.Modell.Elemente.Single(e => e.Name == name);
        foreach (var name in new[] { "WC Vorwand", "Rohrbogen 90" })
        {
            var e = N(name);
            var k = m.Koerper.Single(x => x.ElementId == e.Id);
            Assert.Equal("netzdatei", k.Quelle);
            Assert.Equal("hlks", k.Bereich);
            Assert.Equal(Huelle(nd.Produkt(e.Kennungen!["ifc"])!.Begrenzung), k.Huelle);
        }
        // Rohr 1 mit Nennweite 100 aus dem Pset (ohne Serie: Ø max(100, 30)), Rohr 2 ohne Nennweite (Ø 30), Kanal 400 × 200
        var r1 = m.Koerper.Single(x => x.ElementId == N("Rohr 1").Id);
        Assert.Equal("achse", r1.Quelle);
        Assert.Equal(50, r1.Achsen[0].Radius, 9);
        Assert.Equal(15, m.Koerper.Single(x => x.ElementId == N("Rohr 2").Id).Achsen[0].Radius, 9);
        var ka = m.Koerper.Single(x => x.ElementId == N("Kanal Zuluft").Id).Achsen[0];
        Assert.Equal(Querschnittsform.Rechteck, ka.Form);
        Assert.Equal(400, ka.Breite, 9); Assert.Equal(200, ka.Hoehe, 9);
    }
}
