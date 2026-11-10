// 2D-CAD «Zeichnung», Stufe 1: nur, was Verhalten sichert – Ordnerfreigabe (Sicherheit), Messwerte (Länge/Fläche mit Bögen),
// Auflösung von VonLayer/VonBlock und Layerschaltung in der Darstellung, Schraffurlinien mit Insel.

using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using CSMath;
using Hlks.Kern;

namespace Hlks.Kern.Tests;

public class ZeichnungTests : IDisposable
{
    readonly string ordner = Path.Combine(Path.GetTempPath(), "hlks_zeichnung_" + Guid.NewGuid().ToString("N"));

    public ZeichnungTests() => Directory.CreateDirectory(Path.Combine(ordner, "frei"));
    public void Dispose() { try { Directory.Delete(ordner, true); } catch (IOException) { } }

    string Schreiben(string name, Action<CadDocument> inhalt)
    {
        var doc = new CadDocument();
        doc.Header.InsUnits = ACadSharp.Types.Units.UnitsType.Millimeters;
        inhalt(doc);
        var pfad = Path.Combine(ordner, name);
        using (var w = new DxfWriter(pfad, doc, false)) w.Write();
        return pfad;
    }

    [Fact]
    public void Sitzung_OeffnetNurAusFreigegebenenOrdnern()
    {
        var innen = Schreiben(Path.Combine("frei", "a.dxf"), d => d.Entities.Add(new Line { EndPoint = new XYZ(1000, 0, 0) }));
        var aussen = Schreiben("b.dxf", d => d.Entities.Add(new Line { EndPoint = new XYZ(1, 0, 0) }));
        var s = new ZeichnungSitzung([Path.Combine(ordner, "frei")]);

        Assert.Equal("a.dxf", s.Oeffnen("a.dxf").Name);                                   // relativ ab dem Ordner
        Assert.Equal("a.dxf", s.Oeffnen(innen).Name);                                     // voller Pfad im Ordner
        Assert.Throws<HlksFehler>(() => s.Oeffnen(aussen));                               // ausserhalb
        Assert.Throws<HlksFehler>(() => s.Oeffnen(Path.Combine("..", "b.dxf")));          // hinaus über ..
        Assert.Throws<HlksFehler>(() => s.Oeffnen(Path.Combine(ordner, "frei2", "a.dxf"))); // Ordner mit gleichem Anfang
        Assert.Throws<HlksFehler>(() => new ZeichnungSitzung([]).Oeffnen(innen));         // nichts freigegeben
    }

    [Fact]
    public void Messen_PolylinieMitBogen_LaengeUndFlaecheExakt()
    {
        // Rechteck 2000 × 1000 mit Halbkreis (r = 500) statt der rechten Seite: Fläche 2 000 000 + π·500²/2, Umfang 5000 + π·500
        string kennung = "";
        var pfad = Schreiben(Path.Combine("frei", "m.dxf"), d =>
        {
            var p = new LwPolyline { IsClosed = true };
            p.Vertices.Add(new LwPolyline.Vertex(new XY(0, 0)));
            p.Vertices.Add(new LwPolyline.Vertex(new XY(2000, 0)) { Bulge = 1 });
            p.Vertices.Add(new LwPolyline.Vertex(new XY(2000, 1000)));
            p.Vertices.Add(new LwPolyline.Vertex(new XY(0, 1000)));
            d.Entities.Add(p);
        });
        var z = Zeichnungsdatei.Oeffnen(pfad);
        kennung = Zeichnungsdatei.Kennung(z.Dokument.ModelSpace.Entities.OfType<LwPolyline>().Single());
        var o = ZeichnungAbfragen.Messen(z, null, kennung);
        Assert.Equal(5000 + Math.PI * 500, o["laenge"]!.GetValue<double>(), 2);
        Assert.Equal(2_000_000 + Math.PI * 500 * 500 / 2, o["flaeche"]!.GetValue<double>(), 1);
        Assert.Equal((2_000_000 + Math.PI * 500 * 500 / 2) / 1e6, o["flaecheM2"]!.GetValue<double>(), 4);

        var strecke = ZeichnungAbfragen.Messen(z, [[0, 0], [3000, 4000]], null);
        Assert.Equal(5000, strecke["laenge"]!.GetValue<double>(), 6);
        Assert.Equal(5, strecke["laengeM"]!.GetValue<double>(), 6);
    }

    [Fact]
    public void Grafik_LoestVonLayerUndVonBlockAuf_UndBlendetLayerAus()
    {
        var pfad = Schreiben(Path.Combine("frei", "g.dxf"), d =>
        {
            var rot = new Layer("ROT") { Color = new Color(1), LineWeight = LineWeightType.W50 };
            var aus = new Layer("AUS") { IsOn = false };
            d.Layers.Add(rot); d.Layers.Add(aus);
            d.Entities.Add(new Line { EndPoint = new XYZ(100, 0, 0), Layer = rot });                       // VonLayer → rot, 0.50 mm
            d.Entities.Add(new Line { EndPoint = new XYZ(0, 100, 0), Layer = aus });                       // ausgeschaltet
            var block = new BlockRecord("B");
            block.Entities.Add(new Circle { Radius = 10, Color = Color.ByBlock });                          // VonBlock → Farbe der Referenz
            d.BlockRecords.Add(block);
            d.Entities.Add(new Insert(block) { InsertPoint = new XYZ(500, 500, 0), XScale = 2, YScale = 2, Color = new Color(5) });
        });
        var z = Zeichnungsdatei.Oeffnen(pfad);
        var prim = ZeichnungGrafik.Erzeugen(z, null, 1).Teile.SelectMany(t => t.Primitive).ToList();

        var linie = Assert.Single(prim, p => p.Art == "linie");
        Assert.Equal("#ff0000", linie.Stil.Farbe);
        Assert.Equal(Stifte.Kontur, linie.Stil.Stift);
        var kreis = Assert.Single(prim, p => p.Art == "kreis");
        Assert.Equal("#0000ff", kreis.Stil.Farbe);
        Assert.Equal(20, kreis.Radius!.Value, 6);
        Assert.Equal([500, 500], kreis.Punkte[0]);

        ZeichnungAbfragen.LayerSetzen(z, ["AU*"], true);
        Assert.Equal(2, ZeichnungGrafik.Erzeugen(z, null, 1).Teile.SelectMany(t => t.Primitive).Count(p => p.Art == "linie"));
    }

    [Fact]
    public void Schraffurlinien_SparenDieInselAus()
    {
        // Quadrat 100 mit Insel 40…60, waagrechte Linien im Abstand 10 ab y = 5: Linien durch die Insel sind zweigeteilt
        List<(double X, double Y)> aussen = [(0, 0), (100, 0), (100, 100), (0, 100)], insel = [(40, 40), (60, 40), (60, 60), (40, 60)];
        var linien = ZeichnungGeometrie.SchraffurLinien([aussen, insel], [(0, 0, 5, 0, 10, Array.Empty<double>())], 1000)!;
        Assert.Equal(12, linien.Count);   // 10 Höhen, davon 2 (y = 45, 55) geteilt
        Assert.Equal(10 * 100 - 2 * 20, linien.Sum(l => Math.Abs(l.X1 - l.X0)), 6);
        Assert.Null(ZeichnungGeometrie.SchraffurLinien([aussen], [(0, 0, 0, 0, 0.001, Array.Empty<double>())], 1000));   // zu dicht
    }
}

/// <summary>Stufe 2: zeichnen, ändern, rückgängig und speichern – geprüft am wiedergelesenen DWG.</summary>
public class ZeichnungBearbeitenTests : IDisposable
{
    readonly string ordner = Path.Combine(Path.GetTempPath(), "hlks_zeichnung_b_" + Guid.NewGuid().ToString("N"));
    public ZeichnungBearbeitenTests() => Directory.CreateDirectory(ordner);
    public void Dispose() { try { Directory.Delete(ordner, true); } catch (IOException) { } }

    Zeichnungsdatei Neu(string name)
    {
        var doc = new CadDocument();
        doc.Header.InsUnits = ACadSharp.Types.Units.UnitsType.Millimeters;
        var pfad = Path.Combine(ordner, name);
        using (var w = new DwgWriter(pfad, doc)) w.Write();
        return Zeichnungsdatei.Oeffnen(pfad);
    }

    static System.Text.Json.Nodes.JsonArray Json(string text) => (System.Text.Json.Nodes.JsonArray)System.Text.Json.Nodes.JsonNode.Parse(text)!;

    [Fact]
    public void Zeichnen_Aendern_Speichern_WiederlesenAlsDwg()
    {
        var z = Neu("a.dwg");
        ZeichnungBearbeiten.LayerAnlegen(z, "H-Rohr", System.Text.Json.Nodes.JsonValue.Create(1), null, aktuell: true);
        var r = ZeichnungBearbeiten.Zeichnen(z, null, Json("""
            [ {"art":"linie","von":[0,0],"bis":[1000,0]},
              {"art":"rechteck","ecke1":[0,0],"ecke2":[2000,1000]},
              {"art":"kreis","mitte":[500,500],"radius":250},
              {"art":"bogen","punkte":[[100,0],[0,100],[-100,0]]},
              {"art":"text","ort":[10,20],"text":"VL 35 °C","hoehe":25,"layer":"0"} ]
            """));
        var k = r["kennungen"]!.AsArray().Select(n => n!.GetValue<string>()).ToList();
        Assert.Equal(5, k.Count);
        Assert.True(z.Geaendert);

        ZeichnungBearbeiten.Aendern(z, [k[0]], "verschieben", new() { ["dx"] = 100, ["dy"] = 50 });
        ZeichnungBearbeiten.Aendern(z, [k[2]], "kopieren", new() { ["dx"] = 1000, ["dy"] = 0 });
        ZeichnungBearbeiten.Aendern(z, [k[1]], "drehen", new() { ["um"] = new System.Text.Json.Nodes.JsonArray(0, 0), ["winkelGrad"] = 90 });
        ZeichnungBearbeiten.Aendern(z, [k[4]], "loeschen", []);
        ZeichnungBearbeiten.Rueckgaengig(z);   // Text ist wieder da

        var s = ZeichnungBearbeiten.Speichern(z, null, "2018");
        Assert.True(s["geprueft"]!.GetValue<bool>());
        Assert.False(z.Geaendert);
        Assert.True(File.Exists(Path.Combine(ordner, "a.bak")));

        var w = Zeichnungsdatei.Oeffnen(z.Pfad).Dokument;
        Assert.Equal("AC1032", w.Header.Version.ToString());
        var ms = w.ModelSpace.Entities;
        Assert.Equal(6, ms.Count);
        var linie = ms.OfType<Line>().Single();
        Assert.Equal((100.0, 50.0, 1100.0, 50.0), (linie.StartPoint.X, linie.StartPoint.Y, linie.EndPoint.X, linie.EndPoint.Y));
        Assert.Equal("H-Rohr", linie.Layer.Name);
        Assert.Equal([500.0, 1500.0], ms.OfType<Circle>().Where(c => c is not Arc).Select(c => c.Center.X).Order());
        var rechteck = ms.OfType<LwPolyline>().Single();   // um den Ursprung um 90° gedreht: (2000, 1000) → (−1000, 2000)
        Assert.Contains(rechteck.Vertices, v => Math.Abs(v.Location.X + 1000) < 1e-6 && Math.Abs(v.Location.Y - 2000) < 1e-6);
        var bogen = ms.OfType<Arc>().Single();             // Halbkreis oben: von 0° bis 180°
        Assert.Equal(100, bogen.Radius, 6);
        Assert.Equal(0, bogen.StartAngle, 6);
        Assert.Equal(Math.PI, bogen.EndAngle, 6);
        Assert.Equal("VL 35 °C", ms.OfType<TextEntity>().Single().Value);
    }

    [Fact]
    public void Rueckgaengig_UndFehlerLassenDieZeichnungUnveraendert()
    {
        var z = Neu("b.dwg");
        Assert.Throws<HlksFehler>(() => ZeichnungBearbeiten.Zeichnen(z, null, Json("""[ {"art":"linie","von":[0,0],"bis":[10,0]}, {"art":"kreis","mitte":[0,0],"radius":0} ]""")));
        Assert.Empty(z.Dokument.ModelSpace.Entities);   // nichts halb gezeichnet
        Assert.False(z.Geaendert);

        var k = ZeichnungBearbeiten.Zeichnen(z, null, Json("""[ {"art":"linie","von":[0,0],"bis":[10,0]} ]"""))["kennungen"]![0]!.GetValue<string>();
        ZeichnungBearbeiten.Aendern(z, [k], "skalieren", new() { ["um"] = new System.Text.Json.Nodes.JsonArray(0, 0), ["faktor"] = 3 });
        Assert.Equal(30, z.Dokument.ModelSpace.Entities.OfType<Line>().Single().EndPoint.X, 9);
        ZeichnungBearbeiten.Rueckgaengig(z);
        Assert.Equal(10, z.Dokument.ModelSpace.Entities.OfType<Line>().Single().EndPoint.X, 9);
        ZeichnungBearbeiten.Rueckgaengig(z);
        Assert.Empty(z.Dokument.ModelSpace.Entities);
        Assert.False(z.Geaendert);
        ZeichnungBearbeiten.Wiederholen(z);
        Assert.Single(z.Dokument.ModelSpace.Entities);
        Assert.Throws<HlksFehler>(() => ZeichnungBearbeiten.Speichern(z, Path.Combine(ordner, "x.pdf"), null));
    }
}
