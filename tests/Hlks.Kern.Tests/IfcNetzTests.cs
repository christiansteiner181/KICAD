using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Netzdatei (Modul 1b, B1): exakte Geometrie aller Produkte, übernommene gekennzeichnet; Hintergrund im Grundriss
/// (Schnitt auf Schnitthöhe) und als 3D-Netz; Geometrie-Auswertung (Extrusion, Brep, MappedItem, Zerlegung in Dreiecke).</summary>
public class IfcNetzTests
{
    static IfcNetzdatei Netz(Projekt p)
    {
        var ul = p.Modell.DerKlasse("Unterlage").Single();
        return IfcNetzdatei.Oeffnen(Unterlagen.Pfad(p, ul.Text("netz")!));
    }

    static double Inhalt(float[] pu, int[] dr)
    {
        // Oberfläche aller Dreiecke (mm²)
        double s = 0;
        for (int i = 0; i < dr.Length; i += 3)
        {
            int a = dr[i] * 3, b = dr[i + 1] * 3, c = dr[i + 2] * 3;
            double ux = pu[b] - pu[a], uy = pu[b + 1] - pu[a + 1], uz = pu[b + 2] - pu[a + 2], vx = pu[c] - pu[a], vy = pu[c + 1] - pu[a + 1], vz = pu[c + 2] - pu[a + 2];
            double x = uy * vz - uz * vy, y = uz * vx - ux * vz, z = ux * vy - uy * vx;
            s += Math.Sqrt(x * x + y * y + z * z) / 2;
        }
        return s;
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Netzdatei_AlleProdukte_UebernommeneGekennzeichnet_Kopf(string schema)
    {
        var (p, _) = IfcArchitekturTests.Uebernommen(schema);
        var n = Netz(p);
        Assert.Equal(3, n.Fassung);   // Fassung 3 seit Nachtrag B3c (Material), 2 seit Modul 3b (B1b); Fassung 1 und 2 lesen: IfcNetzFassung2Tests, AussparungMaterialTests
        Assert.Contains("HLKS-CAD Netzdatei", n.Beschreibung);
        var sued = IfcArchitekturTests.Namens(p, "Wand", "W-Sued");
        var w = n.Produkt(sued.Kennungen!["ifc"])!;
        Assert.True(w.Uebernommen);
        Assert.Equal(0, w.GeschossKote);
        Assert.Matches("IfcWall", w.Entitaet);
        // Körper der Wand: 6000 × 200 × 2600 (Aussenfläche y = 0)
        Assert.Equal([0f, 0f, 0f, 6000f, 200f, 2600f], w.Begrenzung);
        var (pu, dr) = n.Netz(w);
        Assert.Equal(12, dr.Length / 3);
        Assert.Equal(2 * (6000.0 * 200 + 6000 * 2600 + 200 * 2600), Inhalt(pu, dr), 0);
        // Hintergrund: Objekt (Proxy) und Rundwand; Tür, Fenster, Decke, Stütze übernommen
        Assert.Contains(n.Produkte, x => x.Entitaet == "IfcBuildingElementProxy" && !x.Uebernommen);
        Assert.All(n.Produkte.Where(x => x.Entitaet is "IfcDoor" or "IfcWindow" or "IfcSlab" or "IfcColumn" or "IfcBeam"), x => Assert.True(x.Uebernommen));
        // Öffnungen und Räume stehen nicht im Netz (IfcUebernehmen gibt sie nicht weiter)
        Assert.DoesNotContain(n.Produkte, x => x.Entitaet is "IfcOpeningElement" or "IfcSpace");
        Assert.Equal(0, n.Begrenzung[0], 0);   // Wände ab x = 0 (Raster ohne Körper)
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Netz_DeckeMitLoch_StuetzeAlsMappedItem(string schema)
    {
        var (p, _) = IfcArchitekturTests.Uebernommen(schema);
        var n = Netz(p);
        var d = n.Produkt(IfcArchitekturTests.Namens(p, "Decke", "Decke 1.OG").Kennungen!["ifc"])!;
        Assert.Equal([0f, 0f, 2750f, 6000f, 4000f, 3000f], d.Begrenzung);
        var (pu, dr) = n.Netz(d);
        // Oberfläche: oben und unten je 24 m² − 1 m² Loch, Rand 2 · (6000 + 4000) · 250, Lochleibung 4 · 1000 · 250
        Assert.Equal(2 * 23e6 + 20000.0 * 250 + 4000.0 * 250, Inhalt(pu, dr), 0);
        var s = n.Produkt(IfcArchitekturTests.Namens(p, "Stuetze", "Stuetze 1").Kennungen!["ifc"])!;
        Assert.Equal([2850f, 1850f, 0f, 3150f, 2150f, 2600f], s.Begrenzung);
    }

    [Fact]
    public void Netzdatei_ZweimalGleich_WirdNichtNeuGeschrieben()
    {
        var p = Leer();
        var datei = IfcArchitekturTestdatei.Datei("IFC4", false);
        Ok(Uebernehmen(p, datei));
        var name = p.Modell.DerKlasse("Unterlage").Single().Text("netz")!;
        var pfad = Unterlagen.Pfad(p, name);
        var zeit = File.GetLastWriteTimeUtc(pfad);
        Ok(Uebernehmen(p, datei));
        Assert.Equal(name, p.Modell.DerKlasse("Unterlage").Single().Text("netz"));
        Assert.Equal(zeit, File.GetLastWriteTimeUtc(pfad));
    }

    [Fact]
    public void NurRaeume_UebrigeProdukteImHintergrund()
    {
        var p = Leer();
        Ok(Uebernehmen(p, IfcArchitekturTestdatei.Datei("IFC2X3", true), new JsonObject { ["bereiche"] = new JsonArray("raeume") }));
        var n = Netz(p);
        Assert.Contains(n.Produkte, x => x.Entitaet == "IfcWall" && !x.Uebernommen);
        Assert.DoesNotContain(n.Produkte, x => x.Uebernommen);
    }

    // ------------------------------------------------------------------ Hintergrund-Grafik

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Hintergrund_Grundriss_SchnittDerNichtUebernommenen(string schema)
    {
        var (p, _) = IfcArchitekturTests.Uebernommen(schema);
        var ul = p.Modell.DerKlasse("Unterlage").Single();
        var pfad = Unterlagen.Pfad(p, ul.Text("netz")!);
        var linien = GrundlagenIfc.Schnitt(pfad, 1000);
        // Objekt-Quader 1000 × 1000 (geschlossener Linienzug) und Rundwand; keine übernommenen Wände
        Assert.Contains(linien, l => ArchitekturHilfe.Box(l).Zip(new[] { 8000.0, 0, 9000, 1000 }).All(z => Math.Abs(z.First - z.Second) < 0.5));
        Assert.DoesNotContain(linien, l => l.All(q => q[1] >= -1 && q[1] <= 201 && q[0] <= 6001));
        Assert.Empty(GrundlagenIfc.Schnitt(pfad, 50000));   // über allem: nichts
    }

    [Fact]
    public void Hintergrund_GrafikquelleUndKoerper_GrauDuennDeckkraftNetz()
    {
        var (p, _) = IfcArchitekturTests.Uebernommen("IFC4");
        var ul = p.Modell.DerKlasse("Unterlage").Single();
        var ansicht = new Element { Id = "a", Kennzeichen = "GR", Klasse = "Ansicht" };
        var ctx = new GrafikKontext { Projekt = p, Ansicht = ansicht, Art = "grundriss", Schnitthoehe = 1000, Geschoss = p.Modell.DerKlasse("Geschoss").First() };
        var teile = new GrundlagenGrafik().Plan(ctx, ul).ToList();
        var t = Assert.Single(teile);
        Assert.Equal(ul.Id, t.Id);
        Assert.Equal("grundlage", t.Ebene);
        Assert.All(t.Primitive, x =>
        {
            Assert.Equal(Stifte.Duenn, x.Stil.Stift);
            Assert.Equal("#8a8a8a", x.Stil.Farbe);
            Assert.Equal(0.5, x.Stil.Deckkraft);
            Assert.Contains(x.Art, new[] { "linie", "polylinie" });
        });
        var k = new GrundlagenGrafik().Koerper(p, ul)!;
        var kt = Assert.Single(k.Teile);
        Assert.Equal("netz", kt.Art);
        Assert.Equal(ul.Id, kt.Quelle);
        // ausgeblendet: nichts
        var aus = ul.Kopie();
        aus.Eigenschaften["anzeigen"] = new Wert { Inhalt = false };
        Assert.Empty(new GrundlagenGrafik().Plan(ctx, aus));
        Assert.Null(new GrundlagenGrafik().Koerper(p, aus));
    }

    [Fact]
    public void Hintergrund_UeberGrafikPlanUndGrafikKoerper()
    {
        var p = Leer();
        Ok(Uebernehmen(p, IfcArchitekturTestdatei.Datei("IFC2X3", false)));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Ansicht", ["kennzeichen"] = "GR-EG", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["art"] = "grundriss", ["massstab"] = 50, ["schnitthoehe"] = 1000 },
        }));
        var ul = p.Modell.DerKlasse("Unterlage").Single();
        var g = Grafik.Plan(p, "GR-EG");
        var t = Assert.Single(g.Teile, x => x.Id == ul.Id);
        Assert.Equal("grundlage", t.Ebene);
        Assert.Contains(t.Primitive, x => ArchitekturHilfe.Box(x.Punkte).Zip(new[] { 8000.0, 0, 9000, 1000 }).All(z => Math.Abs(z.First - z.Second) < 0.5));
        var k =Assert.Single(Grafik.Koerper(p, [ul.Id]));
        Assert.Equal("netz", Assert.Single(k.Teile).Art);
        Assert.Contains(Grafik.Koerper(p), x => x.Id == ul.Id);
    }

    // ------------------------------------------------------------------ Geometrie

    [Fact]
    public void Zerlegung_QuadratMitLoch_FlaecheStimmt()
    {
        List<double[]> aussen = [[0, 0], [10, 0], [10, 10], [0, 10]];
        List<List<double[]>> loecher = [[[3, 3], [3, 7], [7, 7], [7, 3]]];
        var d = Dreieckszerlegung.Zerlegen(aussen, loecher);
        var alle = aussen.Concat(loecher[0]).ToList();
        double f = 0;
        for (int i = 0; i < d.Count; i += 3)
        {
            var a = alle[d[i]]; var b = alle[d[i + 1]]; var c = alle[d[i + 2]];
            double k = ((b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])) / 2;
            Assert.True(k > 0, "Dreieck im Gegenuhrzeigersinn");
            f += k;
        }
        Assert.Equal(100 - 16, f, 9);
    }

    [Fact]
    public void Zerlegung_KonkavesL_UndUhrzeigersinn()
    {
        List<double[]> l = [[0, 0], [0, 10], [5, 10], [5, 5], [10, 5], [10, 0]];   // im Uhrzeigersinn
        var d = Dreieckszerlegung.Zerlegen(l, null);
        Assert.Equal(4 * 3, d.Count);
        double f = 0;
        for (int i = 0; i < d.Count; i += 3)
        {
            var a = l[d[i]]; var b = l[d[i + 1]]; var c = l[d[i + 2]];
            f += ((b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])) / 2;
        }
        Assert.Equal(75, f, 9);
    }

    [Fact]
    public void Rechteck_UndGrundflaeche_AusNetz()
    {
        // gedrehter Quader 4000 × 200 × 1000, 30° gedreht
        var n = new Dreiecksnetz();
        var t = new Affin(Math.Cos(Math.PI / 6), -Math.Sin(Math.PI / 6), 0, 500, Math.Sin(Math.PI / 6), Math.Cos(Math.PI / 6), 0, 700, 0, 0, 1, 0);
        int b = n.Punkte;
        for (int k = 0; k < 8; k++) { var (x, y, z) = t.Punkt((k & 1) != 0 ? 4000 : 0, (k & 2) != 0 ? 200 : 0, (k & 4) != 0 ? 1000 : 0); n.Punkt(x, y, z); }
        int[][] seiten = [[0, 2, 3, 1], [4, 5, 7, 6], [0, 1, 5, 4], [2, 6, 7, 3], [0, 4, 6, 2], [1, 3, 7, 5]];
        foreach (var s in seiten) { n.Dreieck(b + s[0], b + s[1], b + s[2]); n.Dreieck(b + s[0], b + s[2], b + s[3]); }
        var r = Netzflaechen.Rechteck(Enumerable.Range(0, n.Punkte).Select(i => new[] { n.P[3 * i], n.P[3 * i + 1] }))!;
        Assert.Equal(4000, r.Laenge, 6);
        Assert.Equal(200, r.Breite, 6);
        Assert.Equal(30, (Math.Atan2(r.Richtung[1], r.Richtung[0]) * 180 / Math.PI % 180 + 180) % 180, 6);
        var gf = Assert.Single(Netzflaechen.Grundflaeche(n));
        Assert.Equal(800000, gf.Inhalt, 0);
        var strecken = new List<double[]>();
        Netzflaechen.Schneiden(n.P.Select(x => (float)x).ToArray(), n.D.ToArray(), 500, strecken);
        var zug = Assert.Single(Netzflaechen.Verbinden(strecken));
        Assert.Equal(5, zug.Count);   // geschlossen: Anfang = Ende
        Assert.Equal(zug[0], zug[^1]);
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Geometrie_Achse_Extrusion_UnbekanntesZaehlt(string schema)
    {
        using var d = IfcDatei.Oeffnen(IfcArchitekturTestdatei.Datei(schema, false));
        var g = IfcGeometrie.Von(d);
        Assert.Same(g, IfcGeometrie.Von(d));
        var waende = d.Modell.Instances.OfType<Xbim.Ifc4.Interfaces.IIfcWall>().ToList();
        var ost = waende.Single(w => w.Name == "W-Ost");
        var achse = g.Achse(ost, out bool krumm)!;
        Assert.False(krumm);
        Assert.Equal(6000, achse[0][0], 6); Assert.Equal(0, achse[0][1], 6);
        Assert.Equal(6000, achse[1][0], 6); Assert.Equal(4000, achse[1][1], 6);
        var rund = waende.Single(w => w.Name == "Rundwand");
        var bogen = g.Achse(rund, out krumm)!;
        Assert.True(krumm);
        Assert.True(bogen.Count >= 10);   // Viertelkreis R 2000: Sehnen ≤ 10°
        Assert.All(bogen, q => Assert.Equal(2000, Math.Sqrt((q[0] - 12000) * (q[0] - 12000) + q[1] * q[1]), 3));
        var ex = g.Extrusion(ost)!;
        Assert.True(ex.Senkrecht);
        Assert.Equal(0, ex.Unten, 6);
        Assert.Equal(2600, ex.Hoehe, 6);
        Assert.Empty(g.Unbekannt);
    }
}
