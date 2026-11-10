// 2D-CAD «Zeichnung»: Plotstile (CTB) – lesen und beim Plotten anwenden.

using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using CSMath;
using Hlks.Kern;

namespace Hlks.Kern.Tests;

public class ZeichnungPlotstilTests : IDisposable
{
    readonly string ordner = Path.Combine(Path.GetTempPath(), "hlks_zeichnung_p_" + Guid.NewGuid().ToString("N"));
    public ZeichnungPlotstilTests() => Directory.CreateDirectory(ordner);
    public void Dispose() { try { Directory.Delete(ordner, true); } catch (IOException) { } }

    // Tabelle wie von AutoCAD geschrieben (entpackter Text): Farbe 1 schwarz mit 0.50 mm, Farbe 2 Objektfarbe halb gerastert, Farbe 3 unverändert
    const string Tabelle = """
        description="Test
        aci_table_available=TRUE
        scale_factor=1.0
        apply_factor=FALSE
        custom_lineweight_display_units=0
        aci_table{
         0="Color_1
         1="Color_2
         2="Color_3
        }
        plot_style{
         0{
          name="Color_1
          color=-16777216
          mode_color=-16777216
          screen=100
          lineweight=3
         }
         1{
          name="Color_2
          color=-1
          screen=50
          lineweight=0
         }
         2{
          name="Color_3
          color=-1
          screen=100
          lineweight=0
         }
        }
        custom_lineweight_table{
         0=0
         1=0.13
         2=0.5
         3=0.7
        }
        """;

    string Zeichnung()
    {
        var doc = new CadDocument();
        doc.Layers.Add(new Layer("ROT") { Color = new Color(1), LineWeight = LineWeightType.W13 });
        doc.Entities.Add(new Line { EndPoint = new XYZ(10, 0, 0), Layer = doc.Layers["ROT"] });          // Farbe 1 vom Layer
        doc.Entities.Add(new Line { EndPoint = new XYZ(0, 10, 0), Color = new Color(2) });               // Farbe 2 (gelb)
        doc.Entities.Add(new Line { EndPoint = new XYZ(10, 10, 0), Color = new Color(0, 128, 255) });    // Echtfarbe
        var pfad = Path.Combine(ordner, "p.dwg");
        using (var w = new DwgWriter(pfad, doc)) w.Write();
        return pfad;
    }

    [Fact]
    public void Ctb_Lesen_UndAnwenden()
    {
        var ctb = Path.Combine(ordner, "buero.ctb");
        Plotstil.Schreiben(ctb, Tabelle);
        var stil = Plotstil.Lesen(ctb);
        Assert.Equal(("#000000", 0.5, 100), stil.Eintrag(1));
        Assert.Equal((null, null, 50), stil.Eintrag(2));
        Assert.Equal((null, null, 100), stil.Eintrag(3));

        var z = Zeichnungsdatei.Oeffnen(Zeichnung());
        var linien = ZeichnungGrafik.Erzeugen(z, null, 1, true, stil).Teile.SelectMany(t => t.Primitive).ToList();
        var rot = linien.Single(p => p.Punkte[1][0] == 10 && p.Punkte[1][1] == 0);
        Assert.Null(rot.Stil.Farbe);                       // schwarz = Planfarbe
        Assert.Equal(0.5, rot.Stil.Mm!.Value, 9);          // Strichstärke aus dem Plotstil statt 0.13 vom Layer
        var gelb = linien.Single(p => p.Punkte[1][0] == 0);
        Assert.Equal("#ffff80", gelb.Stil.Farbe);          // Gelb zu 50 % gerastert
        var echt = linien.Single(p => p.Punkte[1][0] == 10 && p.Punkte[1][1] == 10);
        Assert.Equal("#0080ff", echt.Stil.Farbe);          // Echtfarbe bleibt

        // ohne Plotstil: Farben und Stärken der Objekte
        var ohne = ZeichnungGrafik.Erzeugen(z, null, 1).Teile.SelectMany(t => t.Primitive).Single(p => p.Punkte[1][0] == 10 && p.Punkte[1][1] == 0);
        Assert.Equal(("#ff0000", 0.13), (ohne.Stil.Farbe, Math.Round(ohne.Stil.Mm!.Value, 9)));
    }

    [Fact]
    public void Pdf_WaehltPlotstil_NachNameUndMeldetUnbekannte()
    {
        var pfad = Zeichnung();
        Plotstil.Schreiben(Path.Combine(ordner, "buero.ctb"), Tabelle);   // liegt neben der Zeichnung: wird gefunden
        var z = Zeichnungsdatei.Oeffnen(pfad);
        Assert.Contains(ZeichnungAusgabe.Plotstile(z)["plotstile"]!.AsArray(), p => p!["name"]!.GetValue<string>() == "buero.ctb");
        Assert.Equal("buero.ctb", ZeichnungAusgabe.Pdf(z, null, null, null, null, Path.Combine(ordner, "a.pdf"), true, "buero.ctb")["plotstil"]!.GetValue<string>());
        Assert.Equal("monochrom", ZeichnungAusgabe.Pdf(z, null, null, null, null, Path.Combine(ordner, "b.pdf"), true, "monochrom")["plotstil"]!.GetValue<string>());
        Assert.Equal("ohne", ZeichnungAusgabe.Pdf(z, null, null, null, null, Path.Combine(ordner, "c.pdf"))["plotstil"]!.GetValue<string>());
        Assert.Throws<HlksFehler>(() => ZeichnungAusgabe.Pdf(z, null, null, null, null, Path.Combine(ordner, "d.pdf"), true, "gibtsnicht.ctb"));
        Assert.Throws<HlksFehler>(() => Plotstil.Lesen(pfad));   // eine DWG ist keine Plotstiltabelle
    }
}
