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
