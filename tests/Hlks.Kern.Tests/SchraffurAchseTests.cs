using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Nachbearbeitung N8: Die Schraffur folgt der Achse des Bauteils (wie Revit «an Element ausrichten»). Der Kern liefert je
/// Fläche Stil.MusterWinkel (Richtung der Achse, [0, 180)) und Stil.MusterUrsprung (Fusspunkt des Lots vom Ursprung auf die Achse);
/// Bildschirm (plangrafik.js, eigener Node-Test) und PDF wenden sie an.</summary>
public class SchraffurAchseTests
{
    static void Wand(Projekt p, double[] a, double[] b, string material = "beton", double dicke = 200) =>
        Ok(p.Ausfuehren("wand_zeichnen", new JsonObject
        {
            ["geschoss"] = "EG", ["punkte"] = Pts(a, b), ["dicke"] = dicke, ["hoehe"] = 2600, ["material"] = material,
        }));

    static List<Stil> Stile(Plangrafik g, Projekt p, string kz) =>
        Teil(g, p, kz).Primitive.Where(x => x.Art == "polygon").Select(x => x.Stil).ToList();

    static Stil Einziger(Plangrafik g, Projekt p, string kz) => Stile(g, p, kz).First();

    // ---------------------------------------------------------------- Winkel

    [Theory]
    [InlineData(0, 0, 1000, 0, 0)]
    [InlineData(0, 0, 1000, 1000, 45)]
    [InlineData(0, 0, 1000, 1732.05, 60)]
    [InlineData(0, 0, 1732.05, 1000, 30)]
    [InlineData(0, 0, 0, 1000, 90)]
    [InlineData(1000, 1000, 0, 0, 45)]          // Gegenrichtung: gleiches Muster
    [InlineData(1000, 0, 0, 0, 0)]              // 180° → 0°
    [InlineData(0, 0, -1000, 1000, 135)]
    [InlineData(1000, 0, 0, 1000, 135)]
    [InlineData(0, 1000, 1000, 0, 135)]         // −45° → 135°
    public void Achswinkel_AufNullBis180Gebracht(double ax, double ay, double bx, double by, double erwartet) =>
        Assert.Equal(erwartet, Schraffuren.Achswinkel(ax, ay, bx, by), 2);

    [Fact]
    public void Achswinkel_Rechenrauschen_GleicherWinkel()
    {
        // zwei Wände auf derselben Achse, eine mit 1e-9° Abweichung (IFC-Rundung): derselbe gerundete Winkel
        Assert.Equal(Schraffuren.Achswinkel(0, 0, 1000, 1000), Schraffuren.Achswinkel(0, 0, 1000, 1000 + 1e-9));
        Assert.Equal(0.0, Schraffuren.Achswinkel(0, 0, 1000, -1e-9));   // knapp unter 0° wird nicht zu 180°
    }

    [Fact]
    public void Wand45Grad_SchraffurWinkel45_Wand0Grad_Winkel0()
    {
        var p = Neu();
        Wand(p, [0, 0], [4000, 4000]);
        Wand(p, [0, 6000], [4000, 6000]);
        var g = Grafik.Plan(p, "AN-EG");
        var schraeg = Einziger(g, p, "W-1");
        Assert.Equal("beton", schraeg.Muster);
        Assert.Equal(45.0, schraeg.MusterWinkel);
        var gerade = Einziger(g, p, "W-2");
        Assert.Equal(0.0, gerade.MusterWinkel);
        // die Winkel der Tabelle bleiben die Winkel gegen die Achse: Beton 45° + 135° → auf dem Blatt 90° und 180°
        Assert.Equal([45, 135], Schraffuren.Definition("beton")!.Winkel);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(135)]
    public void SchraegeWaende_WinkelIstDieRichtungDerAchse(double grad)
    {
        var p = Neu();
        double r = grad * Math.PI / 180;
        Wand(p, [1000, 1000], [1000 + 5000 * Math.Cos(r), 1000 + 5000 * Math.Sin(r)], "backstein");
        var g = Grafik.Plan(p, "AN-EG");
        foreach (var s in Stile(g, p, "W-1"))
        {
            Assert.Equal("backstein", s.Muster);
            Assert.Equal(grad, s.MusterWinkel!.Value, 2);
        }
    }

    // ---------------------------------------------------------------- Anker

    [Fact]
    public void Anker_LiegtAufDerAchse_UndIstDasLotVomUrsprung()
    {
        var p = Neu();
        Wand(p, [0, 1000], [3000, 4000]);   // Achse y = x + 1000, Lot vom Ursprung trifft sie in (−500, 500)
        var s = Einziger(Grafik.Plan(p, "AN-EG"), p, "W-1");
        Assert.Equal(-500, s.MusterUrsprung![0], 1);
        Assert.Equal(500, s.MusterUrsprung[1], 1);
        // auf der Achse: y − x = 1000; senkrecht zur Achse: u · (1, 1) = 0
        Assert.Equal(1000, s.MusterUrsprung[1] - s.MusterUrsprung[0], 1);
        Assert.Equal(0, s.MusterUrsprung[0] + s.MusterUrsprung[1], 1);
    }

    [Fact]
    public void ZweiWaendeAufDerselbenAchse_GleicherWinkelUndAnker_DasMusterLaeuftDurch()
    {
        var p = Neu();
        Wand(p, [0, 1000], [3000, 4000]);
        Wand(p, [5000, 6000], [8000, 9000], dicke: 300);         // gleiche Achse, andere Dicke
        Wand(p, [12000, 13000], [9000, 10000], dicke: 150);      // gleiche Achse, Gegenrichtung
        var g = Grafik.Plan(p, "AN-EG");
        var a = Einziger(g, p, "W-1"); var b = Einziger(g, p, "W-2"); var c = Einziger(g, p, "W-3");
        foreach (var s in new[] { b, c })
        {
            Assert.Equal(a.MusterWinkel, s.MusterWinkel);
            Assert.Equal(a.MusterUrsprung, s.MusterUrsprung);
        }
        // eine parallele Wand daneben hat einen anderen Anker (andere Achse), aber denselben Winkel
        Wand(p, [0, 3000], [3000, 6000]);
        var d = Einziger(Grafik.Plan(p, "AN-EG"), p, "W-4");
        Assert.Equal(a.MusterWinkel, d.MusterWinkel);
        Assert.NotEqual(a.MusterUrsprung, d.MusterUrsprung);
    }

    [Fact]
    public void WandMitOeffnung_AlleTeilflaechenDerselbenWandHabenDieselbeLage()
    {
        var p = Neu();
        Wand(p, [0, 0], [6000, 6000]);
        Ok(p.Ausfuehren("oeffnung_setzen", new JsonObject { ["klasse"] = "Tuer", ["wand"] = "W-1", ["abstand"] = 4243, ["breite"] = 900, ["hoehe"] = 2100 }));
        var s = Stile(Grafik.Plan(p, "AN-EG"), p, "W-1");
        Assert.True(s.Count >= 2, "Tür teilt die Wandfläche");
        Assert.All(s, x => { Assert.Equal(45.0, x.MusterWinkel); Assert.Equal(s[0].MusterUrsprung, x.MusterUrsprung); });
    }

    // ---------------------------------------------------------------- gebogene Wand

    [Fact]
    public void GebogeneWand_JeAbschnittDieTangentenrichtung()
    {
        // Das Modell kennt keine Bogenwand (IFC-Bogenwände gehen als Hintergrund); eine gebogene Wand ist eine Kette gerader
        // Wände (wand_zeichnen mit mehreren Punkten). Jeder Abschnitt hat seine eigene Achse und damit sein eigenes Muster.
        var p = Neu();
        double R = 5000; int n = 6;
        var pts = Enumerable.Range(0, n + 1).Select(i => { double w = i * 15 * Math.PI / 180; return new[] { R * Math.Cos(w), R * Math.Sin(w) }; }).ToArray();
        Ok(p.Ausfuehren("wand_zeichnen", new JsonObject { ["geschoss"] = "EG", ["punkte"] = Pts(pts), ["dicke"] = 200, ["hoehe"] = 2600, ["material"] = "backstein" }));
        var g = Grafik.Plan(p, "AN-EG");
        var winkel = new List<double>();
        for (int i = 0; i < n; i++)
        {
            var s = Einziger(g, p, $"W-{i + 1}");
            double erwartet = Schraffuren.Achswinkel(pts[i][0], pts[i][1], pts[i + 1][0], pts[i + 1][1]);
            Assert.Equal(erwartet, s.MusterWinkel!.Value, 3);
            winkel.Add(s.MusterWinkel.Value);
        }
        Assert.Equal(n, winkel.Distinct().Count());   // jeder Abschnitt anders ausgerichtet
        Assert.Equal(97.5, winkel[0], 1);              // Sehne 0°…15°: Richtung 7.5° + 90° (Kreisbogen) = 97.5°
    }

    // ---------------------------------------------------------------- andere Bauteile

    [Fact]
    public void Unterzug_FolgtDerAchse_StuetzeUndRaumBleibenAchsparallel()
    {
        var p = Neu();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Traeger", ["kennzeichen"] = "UZ-1", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["material"] = "beton" },
            ["geometrie"] = new JsonObject { ["art"] = "linie", ["punkte"] = Pts([0, 0, 0], [3000, 3000, 0]), ["dicke"] = 300, ["hoehe"] = 3000 },
        }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Stuetze", ["kennzeichen"] = "STZ-1", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["material"] = "beton" },
            ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = Pts([5000, 0, 0], [5300, 0, 0], [5300, 300, 0], [5000, 300, 0]), ["hoehe"] = 2750 },
        }));
        var g = Grafik.Plan(p, "AN-EG");
        var uz = Einziger(g, p, "UZ-1");
        Assert.Equal("beton", uz.Muster);
        Assert.Equal(45.0, uz.MusterWinkel);
        Assert.NotNull(uz.MusterUrsprung);
        var stz = Einziger(g, p, "STZ-1");
        Assert.Equal("beton", stz.Muster);
        Assert.Null(stz.MusterWinkel);            // Achse steht senkrecht zur Zeichenebene: keine Richtung
        Assert.Null(stz.MusterUrsprung);
    }

    [Fact]
    public void FlaechenOhneAchse_BleibenWieBisher()
    {
        var p = Neu();
        Wand(p, [0, 0], [4000, 4000]);
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Raum", ["kennzeichen"] = "R-1", ["geschoss"] = "EG",
            ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = Pts([5000, 0, 0], [8000, 0, 0], [8000, 3000, 0], [5000, 3000, 0]) },
        }));
        var g = Grafik.Plan(p, "AN-EG");
        // Raum, Umriss und alles Übrige: nie ein Musterwinkel
        foreach (var t in g.Teile.Where(t => t.Id != Hole(p, "W-1").Id))
            Assert.All(t.Primitive, x => { Assert.Null(x.Stil.MusterWinkel); Assert.Null(x.Stil.MusterUrsprung); });
        // Füllbereich-Muster der Beschriftung (ohne Achse)
        Assert.Null(new Stil { Muster = "kies" }.MusterWinkel);
    }

    [Fact]
    public void AnAchse_OhneMusterOderUnbekanntesMuster_TutNichts()
    {
        var s = new Stil { Stift = null, Fuellung = "schnitt" };
        Schraffuren.AnAchse(s, 0, 0, 1000, 1000);
        Assert.Null(s.MusterWinkel);
        s.Muster = "gibtsnicht";
        Schraffuren.AnAchse(s, 0, 0, 1000, 1000);
        Assert.Null(s.MusterWinkel);
        s.Muster = "beton";
        Schraffuren.AnAchse(s, 5, 5, 5, 5);   // Achse der Länge 0
        Assert.Null(s.MusterWinkel);
        Schraffuren.AnAchse(s, 0, 0, 1000, 1000);
        Assert.Equal(45.0, s.MusterWinkel);
        Assert.Equal(0.0, s.MusterUrsprung![0], 3);   // kein «−0»
        Assert.False(double.IsNegative(s.MusterUrsprung[0]));
    }

    // ---------------------------------------------------------------- Schnitt

    [Fact]
    public void Schnitt_SchraegeWand_SchraffurBleibtImSchnittWaagrecht()
    {
        // Im Schnitt liegt die Wandachse waagrecht (oder zeigt auf den Betrachter): die Schnittfläche einer senkrechten Wand ist
        // ein Rechteck, ihr Muster wird wie bisher nicht gedreht. Gewollt – nur im Grundriss hat die Achse eine Richtung im Bild.
        var p = Neu();
        Wand(p, [0, 0], [4000, 4000]);
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Ansicht", ["kennzeichen"] = "S-1", ["eigenschaften"] = new JsonObject { ["art"] = "schnitt", ["massstab"] = 50, ["blick"] = "links" },
            ["geometrie"] = new JsonObject { ["art"] = "linie", ["punkte"] = Pts([2000, -1000], [2000, 5000]) },
        }));
        var g = Grafik.Plan(p, "S-1");
        var flaeche = g.Teile.Single(t => t.Id == Hole(p, "W-1").Id).Primitive.Single(x => x.Art == "polygon");
        Assert.Equal("beton", flaeche.Stil.Muster);
        Assert.Null(flaeche.Stil.MusterWinkel);
        Assert.Null(flaeche.Stil.MusterUrsprung);
    }

    // ---------------------------------------------------------------- Kopien und Ausgabe

    [Fact]
    public void Plangrafik_Json_TraegtMusterWinkelUndUrsprung_NurWoGesetzt()
    {
        var p = Neu();
        Wand(p, [0, 1000], [3000, 4000]);
        var json = Json.Text(Grafik.Plan(p, "AN-EG"));
        Assert.Contains("\"musterWinkel\":45", json.Replace(" ", ""));
        Assert.Contains("\"musterUrsprung\":[-500,500]", json.Replace(" ", ""));
        var ohne = Json.Text(new Primitiv { Art = "polygon", Stil = new Stil { Muster = "beton" } });
        Assert.DoesNotContain("musterWinkel", ohne);
    }

    [Fact]
    public void Einlageplan_Vergrauen_BehaeltDieMusterlage()
    {
        var p = Neu();
        Wand(p, [0, 0], [4000, 4000]);
        var grau = EinlageGrafik.Vergrauen(Grafik.Plan(p, "AN-EG").Teile).ToList();
        var s = grau.Single(t => t.Id == Hole(p, "W-1").Id).Primitive.First(x => x.Art == "polygon").Stil;
        Assert.Equal(45.0, s.MusterWinkel);
        Assert.NotNull(s.MusterUrsprung);
    }

    // ---------------------------------------------------------------- PDF

    static readonly Regex Strich = new(@"(?<x1>-?\d+(?:\.\d+)?) (?<y1>-?\d+(?:\.\d+)?) m\s+(?<x2>-?\d+(?:\.\d+)?) (?<y2>-?\d+(?:\.\d+)?) l\s+S", RegexOptions.CultureInvariant);

    /// <summary>Zeichnet die Primitive (Papier-mm, A4 quer) in ein PDF und liest die einzelnen Striche zurück (PDF-Punkte, y nach oben
    /// wie auf dem Papier: Winkel Gegenuhrzeigersinn).</summary>
    static List<(double X1, double Y1, double X2, double Y2)> Striche(params Primitiv[] prim)
    {
        Schriften.Sicherstellen();
        var doc = new PdfDocument();
        doc.Options.NoCompression = true;
        doc.Options.CompressContentStreams = false;
        var seite = doc.AddPage();
        seite.Width = XUnit.FromMillimeter(297); seite.Height = XUnit.FromMillimeter(210);
        using (var gfx = XGraphics.FromPdfPage(seite))
            new PdfZeichner(gfx, 210).Zeichnen(prim, Abbildung.Papier);
        using var ms = new MemoryStream();
        doc.Save(ms, false);
        var s = Encoding.Latin1.GetString(ms.ToArray());
        double D(Group g) => double.Parse(g.Value, CultureInfo.InvariantCulture);
        return Strich.Matches(s).Select(m => (D(m.Groups["x1"]), D(m.Groups["y1"]), D(m.Groups["x2"]), D(m.Groups["y2"]))).ToList();
    }

    static Primitiv Feld(Stil stil) => new()
    {
        Art = "polygon", Punkte = [[20, 20], [120, 20], [120, 90], [20, 90]],
        Stil = stil,
    };

    static double WinkelDerStriche((double X1, double Y1, double X2, double Y2) s)
    {
        double w = Math.Atan2(s.Y2 - s.Y1, s.X2 - s.X1) * 180 / Math.PI;   // PDF-Seite: y nach oben (Pt kehrt y um, hier wieder zurück)
        w %= 180;
        return w < 0 ? w + 180 : w;
    }

    [Fact]
    public void Pdf_OhneDrehung_SchraffurWieBisher_45Grad()
    {
        var striche = Striche(Feld(new Stil { Stift = null, Muster = "backstein" }));
        Assert.True(striche.Count > 50, "Schraffurstriche fehlen");
        Assert.All(striche, s => Assert.Equal(45.0, WinkelDerStriche(s), 0.5));
    }

    [Theory]
    [InlineData(0, 45)]
    [InlineData(45, 90)]
    [InlineData(90, 135)]
    [InlineData(30, 75)]
    [InlineData(135, 0)]
    public void Pdf_MitMusterWinkel_StricheLiegenUmDieDrehungGedreht(double drehung, double erwartet)
    {
        var striche = Striche(Feld(new Stil { Stift = null, Muster = "backstein", MusterWinkel = drehung }));
        Assert.True(striche.Count > 50);
        Assert.All(striche, s => Assert.Equal(erwartet, WinkelDerStriche(s), 0.5));
    }

    [Fact]
    public void Pdf_Kreuzschraffur_BeideScharenGedreht()
    {
        var striche = Striche(Feld(new Stil { Stift = null, Muster = "beton", MusterWinkel = 45 }));
        var winkel = striche.Select(s => Math.Round(WinkelDerStriche(s))).Distinct().OrderBy(x => x).ToList();
        Assert.Equal([0.0, 90.0], winkel);   // 45° + 45° und 135° + 45° (= 180° ≙ 0°)
    }

    [Fact]
    public void Pdf_Punktraster_GedrehtUndImAnkerVerankert()
    {
        // putz: Punkte im Raster 0.8 mm; um 30° gedreht, ein Punkt im Anker (60, 50) mm
        const double d = 0.8, drehung = 30;
        var punkte = Striche(Feld(new Stil { Stift = null, Muster = "putz", MusterWinkel = drehung, MusterUrsprung = [60, 50] }));
        Assert.True(punkte.Count > 500, "Punkte fehlen");
        double c = Math.Cos(drehung * Math.PI / 180), s = Math.Sin(drehung * Math.PI / 180);
        double k = PdfZeichner.PtJeMm;
        // Punktlage im Bauteilsystem: ganze Vielfache des Abstands
        Assert.All(punkte.Take(400), q =>
        {
            double px = q.X1 / k - 60, py = q.Y1 / k - 50;   // die Seite ist 210 mm hoch; die Strichlage kommt in Papierkoordinaten
            double lx = (c * px + s * py) / d, ly = (-s * px + c * py) / d;
            Assert.Equal(0.0, lx - Math.Round(lx), 0.05);
            Assert.Equal(0.0, ly - Math.Round(ly), 0.05);
        });
    }

    [Fact]
    public void Pdf_MusterUrsprung_EineMusterlinieLaeuftDurchDenAnker()
    {
        // Backstein 45° + Drehung 45° = senkrechte Linien; Anker bei x = 50 mm: eine Linie liegt genau bei x = 50 mm (141.73 pt),
        // ohne Anker liegen die Linien auf dem 1.2-mm-Raster des Papierursprungs (50 / 1.2 ist keine ganze Zahl)
        double pt = 50 * PdfZeichner.PtJeMm;
        var mit = Striche(Feld(new Stil { Stift = null, Muster = "backstein", MusterWinkel = 45, MusterUrsprung = [50, 40] }));
        Assert.Contains(mit, s => Math.Abs(s.X1 - pt) < 0.05 && Math.Abs(s.X2 - pt) < 0.05);
        var ohne = Striche(Feld(new Stil { Stift = null, Muster = "backstein", MusterWinkel = 45 }));
        Assert.DoesNotContain(ohne, s => Math.Abs(s.X1 - pt) < 0.05);
        // Linienabstand bleibt 1.2 mm
        var xs = mit.Select(s => s.X1).Distinct().OrderBy(x => x).ToList();
        Assert.Equal(1.2 * PdfZeichner.PtJeMm, xs[1] - xs[0], 0.1);
    }

    [Fact]
    public void Pdf_ZweiBauteileAufDerselbenAchse_StricheFluchten()
    {
        // Zwei Felder nebeneinander (gleiche Achse y = x im Modell, Mass 1:1) mit gleichem Winkel und Anker: die Linien des zweiten
        // Feldes setzen die des ersten fort – alle Linienlagen (Abstand zum Anker quer zur Linie) liegen auf demselben 1.2-mm-Raster.
        var stil = () => new Stil { Stift = null, Muster = "backstein", MusterWinkel = 45, MusterUrsprung = [0, 0] };
        Primitiv Band(double x0, double x1) => new() { Art = "polygon", Punkte = [[x0, x0 + 10], [x1, x1 + 10], [x1, x1 - 10], [x0, x0 - 10]], Stil = stil() };
        var striche = Striche(Band(30, 60), Band(80, 110));
        Assert.True(striche.Count > 30);
        // Linienlage = Abstand der Linie vom Ursprung in Richtung ihrer Normalen (Linien 90°: x)
        var lagen = striche.Select(s => s.X1 / PdfZeichner.PtJeMm / 1.2).ToList();
        Assert.All(lagen, l => Assert.Equal(0.0, l - Math.Round(l), 0.02));
    }
}
