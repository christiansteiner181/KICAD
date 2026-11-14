// 2D-CAD «Zeichnung», Stufe 2 Teil 2: Änderungsbefehle mit Geometrie (Rechenwerte) und weitere Objektarten (wiedergelesen aus dem DWG).

using System.Text.Json.Nodes;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using Hlks.Kern;

namespace Hlks.Kern.Tests;

public class ZeichnungGeoBefehleTests : IDisposable
{
    readonly string ordner = Path.Combine(Path.GetTempPath(), "hlks_zeichnung_g_" + Guid.NewGuid().ToString("N"));
    public ZeichnungGeoBefehleTests() => Directory.CreateDirectory(ordner);
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
    public void Stutzen_Und_Dehnen_AnDenGrenzen()
    {
        var z = Neu();
        // waagrechte Linie 0…100, senkrechte Grenzen bei x = 30, 70 und 150
        var k = Zeichnen(z, """[ {"art":"linie","von":[0,0],"bis":[100,0]}, {"art":"linie","von":[30,-10],"bis":[30,10]}, {"art":"linie","von":[70,-10],"bis":[70,10]}, {"art":"linie","von":[150,-10],"bis":[150,10]} ]""");
        ZeichnungBearbeiten.Aendern(z, [k[0]], "stutzen", J("""{"punkt":[50,0]}"""));   // Mittelstück weg
        var teile = Alle<Line>(z).Where(l => l.StartPoint.Y == 0 && l.EndPoint.Y == 0).OrderBy(l => l.StartPoint.X).ToList();
        Assert.Equal([(0.0, 30.0), (70.0, 100.0)], teile.Select(l => (Math.Round(l.StartPoint.X, 9), Math.Round(l.EndPoint.X, 9))));

        ZeichnungBearbeiten.Aendern(z, [Zeichnungsdatei.Kennung(teile[1])], "dehnen", J("""{"punkt":[99,0]}"""));   // rechtes Ende bis x = 150
        Assert.Contains(Alle<Line>(z), l => Math.Abs(l.StartPoint.X - 70) < 1e-9 && Math.Abs(l.EndPoint.X - 150) < 1e-9 && l.EndPoint.Y == 0);
        Assert.Throws<HlksFehler>(() => ZeichnungBearbeiten.Aendern(z, [Zeichnungsdatei.Kennung(teile[0])], "dehnen", J("""{"punkt":[1,0]}""")));   // links liegt keine Grenze

        ZeichnungBearbeiten.Rueckgaengig(z); ZeichnungBearbeiten.Rueckgaengig(z);
        Assert.Single(Alle<Line>(z), l => l.StartPoint.X == 0 && l.EndPoint.X == 100);   // wieder die ganze Linie
    }

    [Fact]
    public void Stutzen_Kreis_ZwischenZweiSchnitten_LaesstDenRestbogen()
    {
        var z = Neu();
        var k = Zeichnen(z, """[ {"art":"kreis","mitte":[0,0],"radius":10}, {"art":"linie","von":[0,-20],"bis":[0,20]} ]""");
        ZeichnungBearbeiten.Aendern(z, [k[0]], "stutzen", J("""{"punkt":[10,0]}"""));   // rechte Hälfte weg
        var a = Assert.Single(Alle<Arc>(z));
        Assert.Equal(Math.PI / 2, a.StartAngle, 6);
        Assert.Equal(3 * Math.PI / 2, a.EndAngle, 6);
    }

    [Fact]
    public void Abrunden_Fasen_Versetzen_Rechenwerte()
    {
        var z = Neu();
        var k = Zeichnen(z, """[ {"art":"linie","von":[0,0],"bis":[100,0]}, {"art":"linie","von":[0,0],"bis":[0,100]} ]""");
        ZeichnungBearbeiten.Aendern(z, k, "abrunden", J("""{"radius":10,"punkte":[[50,0],[0,50]]}"""));
        var bogen = Assert.Single(Alle<Arc>(z));   // Viertelkreis um (10, 10) von 180° bis 270°
        Assert.Equal((10.0, 10.0, 10.0), (Math.Round(bogen.Center.X, 9), Math.Round(bogen.Center.Y, 9), Math.Round(bogen.Radius, 9)));
        Assert.Equal(Math.PI, bogen.StartAngle, 6);
        Assert.Equal(3 * Math.PI / 2, bogen.EndAngle, 6);
        Assert.Contains(Alle<Line>(z), l => Math.Abs(l.StartPoint.X - 10) < 1e-9 && Math.Abs(l.StartPoint.Y) < 1e-9 && l.EndPoint.X == 100);
        Assert.Contains(Alle<Line>(z), l => Math.Abs(l.StartPoint.X) < 1e-9 && Math.Abs(l.StartPoint.Y - 10) < 1e-9 && l.EndPoint.Y == 100);

        ZeichnungBearbeiten.Rueckgaengig(z);
        ZeichnungBearbeiten.Aendern(z, Alle<Line>(z).OrderBy(l => l.EndPoint.Y).Select(Zeichnungsdatei.Kennung), "fasen", J("""{"abstand1":20,"abstand2":30,"punkte":[[50,0],[0,50]]}"""));
        Assert.Contains(Alle<Line>(z), l => Math.Abs(l.StartPoint.X - 20) < 1e-9 && Math.Abs(l.StartPoint.Y) < 1e-9 && Math.Abs(l.EndPoint.X) < 1e-9 && Math.Abs(l.EndPoint.Y - 30) < 1e-9);

        // Rechteck 100 × 50 um 10 nach innen versetzt: 80 × 30
        var r = Zeichnen(z, """[ {"art":"rechteck","ecke1":[200,0],"ecke2":[300,50]} ]""");
        ZeichnungBearbeiten.Aendern(z, r, "versetzen", J("""{"abstand":10,"seite":[250,25]}"""));
        var innen = Alle<LwPolyline>(z).Single(p => Zeichnungsdatei.Kennung(p) != r[0]);
        Assert.Equal((210.0, 290.0, 10.0, 40.0), (Math.Round(innen.Vertices.Min(v => v.Location.X), 9), Math.Round(innen.Vertices.Max(v => v.Location.X), 9),
            Math.Round(innen.Vertices.Min(v => v.Location.Y), 9), Math.Round(innen.Vertices.Max(v => v.Location.Y), 9)));
    }

    [Fact]
    public void Spiegeln_Ursprung_Verbinden_Reihe()
    {
        var z = Neu();
        var k = Zeichnen(z, """[ {"art":"bogen","mitte":[10,0],"radius":5,"vonGrad":0,"bisGrad":90}, {"art":"polylinie","punkte":[[0,0],[10,0],[10,10]]} ]""");
        ZeichnungBearbeiten.Aendern(z, [k[0]], "spiegeln", J("""{"achse":[[0,0],[0,1]]}"""));   // an der y-Achse
        var a = Assert.Single(Alle<Arc>(z));
        Assert.Equal(-10, a.Center.X, 9);
        Assert.Equal(Math.PI / 2, a.StartAngle, 6);   // aus 0°…90° wird 90°…180°
        Assert.Equal(Math.PI, a.EndAngle, 6);

        ZeichnungBearbeiten.Aendern(z, [k[1]], "ursprung", []);
        Assert.Empty(Alle<LwPolyline>(z));
        var linien = Alle<Line>(z).Select(Zeichnungsdatei.Kennung).ToList();
        Assert.Equal(2, linien.Count);
        ZeichnungBearbeiten.Aendern(z, linien, "verbinden", []);
        var p = Assert.Single(Alle<LwPolyline>(z));
        Assert.Equal(3, p.Vertices.Count);
        Assert.Empty(Alle<Line>(z));

        ZeichnungBearbeiten.Aendern(z, [Zeichnungsdatei.Kennung(p)], "reihe", J("""{"zeilen":2,"spalten":3,"dx":50,"dy":40}"""));
        Assert.Equal(6, Alle<LwPolyline>(z).Count());
        Assert.Contains(Alle<LwPolyline>(z), q => q.Vertices.Min(v => v.Location.X) == 100 && q.Vertices.Min(v => v.Location.Y) == 40);
    }

    [Fact]
    public void WeitereObjekte_UeberstehenSpeichernUndWiederlesen()
    {
        var z = Neu();
        var k = Zeichnen(z, """
            [ {"art":"rechteck","ecke1":[0,0],"ecke2":[100,50]},
              {"art":"ellipse","mitte":[200,0],"hauptachse":[240,0],"verhaeltnis":0.5},
              {"art":"polygon","mitte":[0,200],"radius":10,"seiten":6},
              {"art":"punkt","ort":[5,5]},
              {"art":"absatztext","ort":[0,-20],"text":"Zeile 1\nZeile 2","hoehe":3},
              {"art":"bemassung","von":[0,0],"bis":[100,0],"ort":[50,-15],"richtung":"waagrecht"} ]
            """);
        var schraffur = new JsonArray(new JsonObject { ["art"] = "schraffur", ["muster"] = "linien", ["abstand"] = 5, ["objekte"] = new JsonArray(k[0]) });
        ZeichnungBearbeiten.Zeichnen(z, null, schraffur);
        Assert.True(ZeichnungBearbeiten.Speichern(z, null, "2018")["geprueft"]!.GetValue<bool>());

        var w = Zeichnungsdatei.Oeffnen(z.Pfad);
        var ms = w.Dokument.ModelSpace.Entities;
        Assert.Equal(0.5, ms.OfType<Ellipse>().Single().RadiusRatio, 9);
        Assert.Single(ms.OfType<LwPolyline>(), p => p.Vertices.Count == 6);
        Assert.Single(ms.OfType<Point>());
        Assert.Equal(["Zeile 1", "Zeile 2"], CadText.MText(ms.OfType<MText>().Single().Value));
        Assert.Equal(100, ms.OfType<Dimension>().Single().Measurement, 6);
        var h = ms.OfType<Hatch>().Single();
        Assert.False(h.IsSolid);
        Assert.Equal(Math.PI / 4, h.Pattern.Lines.Single().Angle, 6);   // Vorgabe 45°, nicht doppelt gedreht
        Assert.Equal(5000, ZeichnungAbfragen.Masse(h).Flaeche!.Value, 3);   // Fläche der Schraffur = Rechteck 100 × 50
        // Darstellung: die Schraffurlinien (Abstand 5, 45°) kommen dazu – deutlich mehr als die sieben Objekte allein
        Assert.True(ZeichnungGrafik.Erzeugen(w, null, 1).Anzahl > 25);
    }

    /// <summary>Frage 40 (Christian 13.11.2026): gespiegelter Text bleibt lesbar wie MIRRTEXT = 0 – an der waagrechten Achse nicht kopfüber,
    /// an der senkrechten nicht von rechts nach links; die Lage wird gespiegelt.</summary>
    [Theory]
    [InlineData(1, 0, 10.0, -20.0)]   // waagrechte Achse y = 0: Text bei (10, 20) → (10, −20), Richtung 0° bleibt (vorher 180°, kopfüber)
    [InlineData(0, 1, -10.0, 20.0)]   // senkrechte Achse x = 0: (10, 20) → (−10, 20), gespiegelt 180° → aufrecht 0°
    public void Spiegeln_TextBleibtLesbar(int bx, int by, double x, double y)
    {
        var z = Neu();
        var k = Zeichnen(z, """[ {"art":"text","ort":[10,20],"text":"Notiz","hoehe":2.5} ]""");
        ZeichnungBearbeiten.Aendern(z, [k[0]], "spiegeln", J($$"""{"achse":[[0,0],[{{bx}},{{by}}]]}"""));
        var t = Assert.Single(Alle<TextEntity>(z));
        Assert.Equal(0, t.Rotation, 6);
        Assert.Equal((x, y), (Math.Round(t.InsertPoint.X, 6), Math.Round(t.InsertPoint.Y, 6)));
    }
}
