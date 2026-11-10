// 2D-CAD «Zeichnung», Stufe 2 Teil 3: Blöcke, Strecken, Bruch, Griffe, Radiusmass, Führung, Bemassungsstil – Rechenwerte und Wiederlesen.

using System.Text.Json.Nodes;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using Hlks.Kern;

namespace Hlks.Kern.Tests;

public class ZeichnungWeitereTests : IDisposable
{
    readonly string ordner = Path.Combine(Path.GetTempPath(), "hlks_zeichnung_w_" + Guid.NewGuid().ToString("N"));
    public ZeichnungWeitereTests() => Directory.CreateDirectory(ordner);
    public void Dispose() { try { Directory.Delete(ordner, true); } catch (IOException) { } }

    Zeichnungsdatei Neu()
    {
        var pfad = Path.Combine(ordner, Guid.NewGuid().ToString("N") + ".dwg");
        using (var w = new DwgWriter(pfad, new CadDocument())) w.Write();
        return Zeichnungsdatei.Oeffnen(pfad);
    }

    static JsonObject J(string text) => (JsonObject)JsonNode.Parse(text)!;
    static List<string> Zeichnen(Zeichnungsdatei z, string objekte) =>
        ZeichnungBearbeiten.Zeichnen(z, null, (JsonArray)JsonNode.Parse(objekte)!)["kennungen"]!.AsArray().Select(n => n!.GetValue<string>()).ToList();
    static IEnumerable<T> Alle<T>(Zeichnungsdatei z) where T : Entity => z.Dokument.ModelSpace.Entities.OfType<T>();

    [Fact]
    public void Block_Erstellen_Einfuegen_UndWiederlesen()
    {
        var z = Neu();
        var k = Zeichnen(z, """[ {"art":"rechteck","ecke1":[100,100],"ecke2":[200,150]}, {"art":"kreis","mitte":[150,125],"radius":10} ]""");
        ZeichnungBearbeiten.Aendern(z, k, "block", J("""{"name":"Ventil","basis":[100,100]}"""));
        var ref1 = Assert.Single(Alle<Insert>(z));
        Assert.Equal((100.0, 100.0), (ref1.InsertPoint.X, ref1.InsertPoint.Y));
        Assert.Empty(Alle<Circle>(z));   // die Objekte stecken jetzt im Block, relativ zum Basispunkt
        Assert.Contains(ref1.Block.Entities.OfType<Circle>(), c => c.Center.X == 50 && c.Center.Y == 25);

        Zeichnen(z, """[ {"art":"block","name":"Ventil","ort":[500,0],"massstab":2,"drehungGrad":90} ]""");
        var liste = ZeichnungWeitere.Bloecke(z)["bloecke"]!.AsArray().Single(b => b!["name"]!.GetValue<string>() == "Ventil")!;
        Assert.Equal(2, liste["referenzen"]!.GetValue<int>());
        Assert.Throws<HlksFehler>(() => Zeichnen(z, """[ {"art":"block","name":"gibtsnicht","ort":[0,0]} ]"""));

        Assert.True(ZeichnungBearbeiten.Speichern(z, null, "2018")["geprueft"]!.GetValue<bool>());
        var w = Zeichnungsdatei.Oeffnen(z.Pfad);
        var refs = w.Dokument.ModelSpace.Entities.OfType<Insert>().OrderBy(i => i.InsertPoint.X).ToList();
        Assert.Equal(["Ventil", "Ventil"], refs.Select(i => i.Block.Name));
        Assert.Equal(2, refs[1].XScale, 9);
        Assert.Equal(Math.PI / 2, refs[1].Rotation, 9);
        // Darstellung: der gedrehte, doppelt grosse Block zeichnet den Kreis bei (500 − 2·25, 0 + 2·50) mit Radius 20
        var kreise = ZeichnungGrafik.Erzeugen(w, null, 1).Teile.SelectMany(t => t.Primitive).Where(p => p.Art == "kreis").ToList();
        Assert.Contains(kreise, p => Math.Abs(p.Punkte[0][0] - 450) < 1e-6 && Math.Abs(p.Punkte[0][1] - 100) < 1e-6 && Math.Abs(p.Radius!.Value - 20) < 1e-6);

        ZeichnungBearbeiten.Rueckgaengig(z); ZeichnungBearbeiten.Rueckgaengig(z);   // Einfügen und Erstellen zurück
        Assert.Empty(Alle<Insert>(z));
        Assert.Single(Alle<Circle>(z));
        Assert.False(z.Dokument.BlockRecords.Contains("Ventil"));
    }

    [Fact]
    public void Strecken_Bruch_Griff_Rechenwerte()
    {
        var z = Neu();
        var k = Zeichnen(z, """[ {"art":"rechteck","ecke1":[0,0],"ecke2":[100,50]}, {"art":"linie","von":[0,100],"bis":[100,100]}, {"art":"kreis","mitte":[300,0],"radius":10} ]""");
        // Fenster über die rechte Seite des Rechtecks: die beiden rechten Ecken wandern um 40 nach rechts
        ZeichnungBearbeiten.Aendern(z, [k[0]], "strecken", J("""{"fenster":[90,-10,110,60],"dx":40,"dy":0}"""));
        var r = Alle<LwPolyline>(z).Single();
        Assert.Equal((0.0, 140.0), (r.Vertices.Min(v => v.Location.X), r.Vertices.Max(v => v.Location.X)));
        Assert.Equal(7000, ZeichnungAbfragen.Masse(r).Flaeche!.Value, 6);

        ZeichnungBearbeiten.Aendern(z, [k[1]], "bruch", J("""{"punkt":[30,100],"punkt2":[60,101]}"""));
        Assert.Equal([(0.0, 30.0), (60.0, 100.0)], Alle<Line>(z).OrderBy(l => l.StartPoint.X).Select(l => (Math.Round(l.StartPoint.X, 9), Math.Round(l.EndPoint.X, 9))));

        var o = ZeichnungAbfragen.Objekte(z, null, null, null, null, null, k[2], 1, 10)["objekte"]![0]!;
        Assert.Equal(5, o["griffe"]!.AsArray().Count);   // Mitte und vier Quadranten
        ZeichnungBearbeiten.Aendern(z, [k[2]], "griff", J("""{"index":1,"nach":[300,25]}"""));   // Quadrantengriff: Radius = Abstand zur Mitte
        Assert.Equal(25, Alle<Circle>(z).Single().Radius, 9);
        ZeichnungBearbeiten.Aendern(z, [k[2]], "griff", J("""{"index":0,"nach":[400,10]}"""));   // Mittelgriff: verschieben
        Assert.Equal((400.0, 10.0), (Alle<Circle>(z).Single().Center.X, Alle<Circle>(z).Single().Center.Y));
        ZeichnungBearbeiten.Rueckgaengig(z); ZeichnungBearbeiten.Rueckgaengig(z);
        Assert.Equal((300.0, 10.0), (Alle<Circle>(z).Single().Center.X, Alle<Circle>(z).Single().Radius));
    }

    [Fact]
    public void Radiusmass_Fuehrung_Bemassungsstil_UeberstehenDasSpeichern()
    {
        var z = Neu();
        var k = Zeichnen(z, """[ {"art":"kreis","mitte":[0,0],"radius":500}, {"art":"bemassung","von":[0,-800],"bis":[1000,-800],"ort":[500,-1000],"richtung":"waagrecht"} ]""");
        Zeichnen(z, "[ {\"art\":\"radiusmass\",\"objekt\":\"" + k[0] + "\",\"ort\":[700,700]}, {\"art\":\"fuehrung\",\"punkte\":[[0,500],[300,900],[600,900]]} ]");
        var stil = ZeichnungWeitere.Bemassungsstil(z, 50, null, null);
        Assert.Equal(50, stil["massstab"]!.GetValue<double>());
        // mit Massstab 50 ist die Masszahl 50-mal so hoch wie im Stil: im Bild der Bemassung steht ein Text dieser Höhe
        var texte = ZeichnungGrafik.Erzeugen(z, null, 1).Teile.SelectMany(t => t.Primitive).Where(p => p.Art == "text").ToList();
        Assert.Contains(texte, t => t.Text!.Contains("1000"));
        double h = z.Dokument.Header.CurrentDimensionStyle!.TextHeight * 50 / 0.716;
        Assert.Contains(texte, t => Math.Abs(t.Groesse!.Value - h) < h * 0.02);

        Assert.True(ZeichnungBearbeiten.Speichern(z, null, "2018")["geprueft"]!.GetValue<bool>());
        var ms = Zeichnungsdatei.Oeffnen(z.Pfad).Dokument.ModelSpace.Entities;
        Assert.Equal(500, ms.OfType<DimensionRadius>().Single().Measurement, 6);
        Assert.Equal(3, ms.OfType<Leader>().Single().Vertices.Count);
        Assert.Equal(50, ms.OfType<DimensionLinear>().Single().Style.ScaleFactor, 9);
    }
}
