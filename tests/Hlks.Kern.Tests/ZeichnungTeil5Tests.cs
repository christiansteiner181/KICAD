// 2D-CAD «Zeichnung», Stufe 2 Teil 5: Polylinien mit Bögen stutzen, versetzen, brechen; genaue Strichmuster der Linientypen.

using System.Text.Json.Nodes;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using CSMath;
using Hlks.Kern;

namespace Hlks.Kern.Tests;

public class ZeichnungTeil5Tests : IDisposable
{
    readonly string ordner = Path.Combine(Path.GetTempPath(), "hlks_zeichnung_5_" + Guid.NewGuid().ToString("N"));
    public ZeichnungTeil5Tests() => Directory.CreateDirectory(ordner);
    public void Dispose() { try { Directory.Delete(ordner, true); } catch (IOException) { } }

    Zeichnungsdatei Neu(Action<CadDocument>? inhalt = null)
    {
        var doc = new CadDocument();
        inhalt?.Invoke(doc);
        var pfad = Path.Combine(ordner, Guid.NewGuid().ToString("N") + ".dwg");
        using (var w = new DwgWriter(pfad, doc)) w.Write();
        return Zeichnungsdatei.Oeffnen(pfad);
    }

    static JsonObject J(string text) => (JsonObject)JsonNode.Parse(text)!;
    static List<string> Zeichnen(Zeichnungsdatei z, string objekte) =>
        ZeichnungBearbeiten.Zeichnen(z, null, (JsonArray)JsonNode.Parse(objekte)!)["kennungen"]!.AsArray().Select(n => n!.GetValue<string>()).ToList();
    static IEnumerable<LwPolyline> Polylinien(Zeichnungsdatei z) => z.Dokument.ModelSpace.Entities.OfType<LwPolyline>();

    // «Langloch»: zwei Geraden 100 lang im Abstand 40, an beiden Enden Halbkreise (Radius 20): Umfang 200 + 2π·20, Fläche 4000 + π·400
    const string Langloch = """[ {"art":"polylinie","geschlossen":true,"punkte":[[0,0],[100,0,1],[100,40],[0,40,1]]} ]""";

    [Fact]
    public void Versetzen_PolylinieMitBoegen_RadienUndFlaeche()
    {
        var z = Neu();
        var k = Zeichnen(z, Langloch);
        var (umfang, flaeche) = ZeichnungAbfragen.Masse(z.Objekt(k[0]));
        Assert.Equal(200 + 2 * Math.PI * 20, umfang!.Value, 6);
        Assert.Equal(4000 + Math.PI * 400, flaeche!.Value, 6);

        ZeichnungBearbeiten.Aendern(z, k, "versetzen", J("""{"abstand":5,"seite":[50,-30]}"""));   // nach aussen: Radius 25, Geraden bleiben 100 lang
        var aussen = Polylinien(z).Single(p => Zeichnungsdatei.Kennung(p) != k[0]);
        var (u2, f2) = ZeichnungAbfragen.Masse(aussen);
        Assert.Equal(200 + 2 * Math.PI * 25, u2!.Value, 6);
        Assert.Equal(100 * 50 + Math.PI * 625, f2!.Value, 6);

        ZeichnungBearbeiten.Aendern(z, k, "versetzen", J("""{"abstand":5,"seite":[50,20]}"""));    // nach innen: Radius 15
        Assert.Contains(Polylinien(z), p => Math.Abs(ZeichnungAbfragen.Masse(p).Flaeche!.Value - (100 * 30 + Math.PI * 225)) < 1e-6);
        Assert.Throws<HlksFehler>(() => ZeichnungBearbeiten.Aendern(z, k, "versetzen", J("""{"abstand":25,"seite":[50,20]}""")));   // grösser als der Radius
    }

    [Fact]
    public void Stutzen_Und_Bruch_PolylinieMitBoegen()
    {
        var z = Neu();
        var k = Zeichnen(z, Langloch);
        Zeichnen(z, """[ {"art":"linie","von":[50,-50],"bis":[50,90]} ]""");   // senkrechte Schnittkante durch die Mitte
        ZeichnungBearbeiten.Aendern(z, k, "stutzen", J("""{"punkt":[120,20]}"""));   // rechte Hälfte (mit dem rechten Halbkreis) weg
        var rest = Polylinien(z).Single();
        Assert.False(rest.IsClosed);
        // Rest: von (50, 40) über den linken Halbkreis nach (50, 0): Länge 50 + π·20 + 50
        Assert.Equal(100 + Math.PI * 20, ZeichnungAbfragen.Masse(rest).Laenge!.Value, 6);
        Assert.Equal((50.0, 40.0, 50.0, 0.0), (Math.Round(rest.Vertices[0].Location.X, 6), Math.Round(rest.Vertices[0].Location.Y, 6), Math.Round(rest.Vertices[^1].Location.X, 6), Math.Round(rest.Vertices[^1].Location.Y, 6)));

        // Bruch am Scheitel des linken Halbkreises (−20, 20): zwei Teile, zusammen so lang wie vorher
        ZeichnungBearbeiten.Aendern(z, [Zeichnungsdatei.Kennung(rest)], "bruch", J("""{"punkt":[-20,20]}"""));
        var teile = Polylinien(z).ToList();
        Assert.Equal(2, teile.Count);
        Assert.Equal(100 + Math.PI * 20, teile.Sum(p => ZeichnungAbfragen.Masse(p).Laenge!.Value), 6);
        Assert.All(teile, p => Assert.Equal(50 + Math.PI * 10, ZeichnungAbfragen.Masse(p).Laenge!.Value, 6));
    }

    [Fact]
    public void Linientyp_StrichmusterMitMassstaeben()
    {
        var z = Neu(d =>
        {
            var lt = new LineType("STRICHPUNKT");
            lt.AddSegment(new LineType.Segment { Length = 12 });
            lt.AddSegment(new LineType.Segment { Length = -3 });
            lt.AddSegment(new LineType.Segment { Length = 0 });
            lt.AddSegment(new LineType.Segment { Length = -3 });
            d.LineTypes.Add(lt);
            d.Header.LineTypeScale = 2;
            d.Entities.Add(new Line { EndPoint = new XYZ(1000, 0, 0), LineType = lt, LineTypeScale = 5 });
        });
        var linie = ZeichnungGrafik.Erzeugen(z, null, 1).Teile.SelectMany(t => t.Primitive).Single();
        Assert.Equal("strichpunkt", linie.Stil.Linie);
        Assert.Equal([120.0, 30.0, 0.0, 30.0], linie.Stil.StrichEinheiten);   // Längen × LTSCALE 2 × Objektmassstab 5
    }
}
