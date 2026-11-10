// 2D-CAD «Zeichnung», Stufe 2 Teil 4: Abdeckflächen, externe Referenzen, genaue Linienstärken, Winkelmass, Spline, Blockattribute.

using System.Text.Json.Nodes;
using ACadSharp;
using ACadSharp.Blocks;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using CSMath;
using Hlks.Kern;

namespace Hlks.Kern.Tests;

public class ZeichnungTeil4Tests : IDisposable
{
    readonly string ordner = Path.Combine(Path.GetTempPath(), "hlks_zeichnung_4_" + Guid.NewGuid().ToString("N"));
    public ZeichnungTeil4Tests() => Directory.CreateDirectory(ordner);
    public void Dispose() { try { Directory.Delete(ordner, true); } catch (IOException) { } }

    string Schreiben(string name, Action<CadDocument> inhalt)
    {
        var doc = new CadDocument();
        inhalt(doc);
        var pfad = Path.Combine(ordner, name);
        using (var w = new DwgWriter(pfad, doc)) w.Write();
        return pfad;
    }

    static JsonObject J(string text) => (JsonObject)JsonNode.Parse(text)!;
    static List<string> Zeichnen(Zeichnungsdatei z, string objekte) =>
        ZeichnungBearbeiten.Zeichnen(z, null, (JsonArray)JsonNode.Parse(objekte)!)["kennungen"]!.AsArray().Select(n => n!.GetValue<string>()).ToList();

    [Fact]
    public void Darstellung_Abdeckflaeche_Linienstaerke_UndExterneReferenz()
    {
        // b.dwg wird von a.dwg als externe Referenz bei (1000, 0) eingefügt
        Schreiben("b.dwg", d => d.Entities.Add(new Circle { Center = new XYZ(5, 5, 0), Radius = 2 }));
        var a = Schreiben("a.dwg", d =>
        {
            d.Layers.Add(new Layer("L13") { LineWeight = LineWeightType.W13 });
            d.Entities.Add(new Line { EndPoint = new XYZ(10, 0, 0), Layer = d.Layers["L13"] });
            var w = new Wipeout { InsertPoint = new XYZ(0, 0, 0), UVector = new XYZ(100, 0, 0), VVector = new XYZ(0, 50, 0), Size = new XY(1, 1) };   // vier Eckpunkte = vieleckige Grenze
            w.ClipBoundaryVertices.AddRange([new XY(-0.5, -0.5), new XY(0.5, -0.5), new XY(0.5, 0.5), new XY(-0.5, 0.5)]);
            d.Entities.Add(w);
            var x = new BlockRecord("b");
            x.Flags |= BlockTypeFlags.XRef;
            x.BlockEntity.XRefPath = "b.dwg";
            d.BlockRecords.Add(x);
            d.Entities.Add(new Insert(x) { InsertPoint = new XYZ(1000, 0, 0) });
        });
        var z = Zeichnungsdatei.Oeffnen(a);
        var bild = ZeichnungGrafik.Erzeugen(z, null, 1);
        var prim = bild.Teile.SelectMany(t => t.Primitive).ToList();

        Assert.Equal(0.13, Assert.Single(prim, p => p.Art == "linie").Stil.Mm!.Value, 9);   // genau, nicht auf einen Stift gerundet
        var flaeche = Assert.Single(prim, p => p.Art == "polygon");
        Assert.Equal("#ffffff", flaeche.Stil.Fuellung);
        Assert.Equal((0.0, 100.0, 0.0, 50.0), (flaeche.Punkte.Min(q => q[0]), flaeche.Punkte.Max(q => q[0]), flaeche.Punkte.Min(q => q[1]), flaeche.Punkte.Max(q => q[1])));
        var kreis = Assert.Single(prim, p => p.Art == "kreis");   // aus der referenzierten Datei
        Assert.Equal([1005.0, 5.0], kreis.Punkte[0]);
        Assert.Empty(bild.NichtDargestellt);

        // die KI liest Referenzen nur aus freigegebenen Ordnern: ohne Freigabe bleibt die Referenz leer und wird gemeldet
        var gesperrt = Zeichnungsdatei.Oeffnen(a);
        gesperrt.XrefErlaubt = _ => false;
        var ohne = ZeichnungGrafik.Erzeugen(gesperrt, null, 1);
        Assert.DoesNotContain(ohne.Teile.SelectMany(t => t.Primitive), p => p.Art == "kreis");
        Assert.Single(ohne.NichtDargestellt);
    }

    [Fact]
    public void Winkelmass_Spline_Attribute_UeberstehenDasSpeichern()
    {
        var pfad = Schreiben("c.dwg", d =>
        {
            var b = new BlockRecord("Stempel");
            b.Entities.Add(new Line { EndPoint = new XYZ(10, 0, 0) });
            b.Entities.Add(new AttributeDefinition { Tag = "NR", Prompt = "Nummer", Value = "?", Height = 2.5, InsertPoint = new XYZ(1, 1, 0) });
            d.BlockRecords.Add(b);
        });
        var z = Zeichnungsdatei.Oeffnen(pfad);
        var k = Zeichnen(z, """
            [ {"art":"winkelmass","scheitel":[0,0],"von":[100,0],"bis":[0,100],"ort":[60,60]},
              {"art":"spline","punkte":[[0,200],[50,250],[100,200],[150,250]]},
              {"art":"block","name":"Stempel","ort":[300,0],"attribute":{"nr":"1.02"}} ]
            """);
        // das Winkelmass hat ein Bild: Bogen mit Radius = Abstand der Masslinie vom Scheitel, und die Gradzahl 90°
        var winkel = ZeichnungGrafik.Erzeugen(z, null, 1).Teile.SelectMany(t => t.Primitive).ToList();
        Assert.Contains(winkel, p => p.Art == "bogen" && Math.Abs(p.Radius!.Value - Math.Sqrt(7200)) < 1e-6);
        Assert.Contains(winkel, p => p.Art == "text" && p.Text == "90°");
        var ins = z.Dokument.ModelSpace.Entities.OfType<Insert>().Single();
        Assert.Equal("1.02", ins.Attributes.Single().Value);
        ZeichnungBearbeiten.Aendern(z, [k[2]], "attribut", J("""{"werte":{"NR":"1.03"}}"""));
        Assert.Throws<HlksFehler>(() => ZeichnungBearbeiten.Aendern(z, [k[2]], "attribut", J("""{"werte":{"GIBTSNICHT":"x"}}""")));

        var gespeichert = ZeichnungBearbeiten.Speichern(z, null, "2018");
        var ms = Zeichnungsdatei.Oeffnen(z.Pfad).Dokument.ModelSpace.Entities;
        Assert.True(gespeichert["geprueft"]!.GetValue<bool>());
        Assert.Equal(Math.PI / 2, ms.OfType<DimensionAngular3Pt>().Single().Measurement, 6);
        Assert.Equal("1.03", ms.OfType<Insert>().Single().Attributes.Single().Value);
        var sp = ms.OfType<Spline>().Single();
        // die Kurve läuft durch den ersten und den letzten Punkt und ist wirklich gekrümmt (mehr als die vier Stützpunkte)
        var punkte = ZeichnungGeometrie.SplinePunkte(sp);
        Assert.True(punkte.Count > 8, $"Spline nach dem Wiederlesen: {sp.ControlPoints.Count} Kontrollpunkte, {sp.FitPoints.Count} Stützpunkte, {punkte.Count} Kurvenpunkte");
        Assert.Equal((0.0, 200.0), (Math.Round(punkte[0].X, 6), Math.Round(punkte[0].Y, 6)));
        Assert.Equal((150.0, 250.0), (Math.Round(punkte[^1].X, 6), Math.Round(punkte[^1].Y, 6)));
    }
}
