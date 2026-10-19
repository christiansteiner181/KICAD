using System.Text.Json.Nodes;
using Docnet.Core;
using Docnet.Core.Models;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.GrundlagenHilfe;

namespace Hlks.Kern.Tests;

/// <summary>PDF und Bild als Unterlage (Modul 1b, B3): PNG-Schreiber, Bildkopf, PDF-Seite rendern, einfügen, kalibrieren,
/// Primitiv «bild» in der Plangrafik und im Plan-PDF.</summary>
public class GrundlagenBildTests
{
    // ---------------------------------------------------------------- PNG und Bildkopf

    [Fact]
    public void PngSchreiber_Rundlauf_AllePixelStimmen_PdfsharpKannEsLesen()
    {
        using var o = new Ordner();
        var rnd = new Random(7);
        int b = 37, h = 23;
        var bgra = new byte[b * h * 4];
        for (int i = 0; i < b * h; i++)
        {
            // Mischung aus Verläufen (Filter Sub/Up lohnen sich) und Zufall
            bgra[4 * i] = (byte)(i % b * 5); bgra[4 * i + 1] = (byte)(i / b * 9); bgra[4 * i + 2] = (byte)rnd.Next(256); bgra[4 * i + 3] = 255;
        }
        var pfad = o.Datei("r.png");
        PngSchreiber.Schreiben(pfad, b, h, bgra, 150);
        var (br, ho, rgb) = PngLesen(pfad);
        Assert.Equal((b, h), (br, ho));
        for (int i = 0; i < b * h; i++)
        {
            Assert.Equal(bgra[4 * i + 2], rgb[3 * i]); Assert.Equal(bgra[4 * i + 1], rgb[3 * i + 1]); Assert.Equal(bgra[4 * i], rgb[3 * i + 2]);
        }
        var info = BildDatei.Lesen(pfad);
        Assert.Equal(new BildInfo("png", b, h, info.Dpi), info);
        Assert.Equal(150, info.Dpi!.Value, 0);
        using var bild = XImage.FromFile(pfad);   // PDFsharp liest es ebenfalls
        Assert.Equal((b, h), (bild.PixelWidth, bild.PixelHeight));
    }

    [Fact]
    public void BildDatei_Jpeg_Kopf_und_Fehler()
    {
        using var o = new Ordner();
        // kleinstes JPEG-Gerüst: SOI, APP0 JFIF (300 dpi), SOF0 (45 hoch × 123 breit), SOS
        byte[] jpg =
        [
            0xFF, 0xD8,
            0xFF, 0xE0, 0x00, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1, 1, 1, 0x01, 0x2C, 0x01, 0x2C, 0, 0,
            0xFF, 0xC0, 0x00, 0x0B, 8, 0x00, 45, 0x00, 123, 1, 1, 0x11, 0,
            0xFF, 0xDA, 0x00, 0x02,
        ];
        var pfad = o.Datei("x.jpg");
        File.WriteAllBytes(pfad, jpg);
        var info = BildDatei.Lesen(pfad);
        Assert.Equal(new BildInfo("jpg", 123, 45, 300), info);

        var text = o.Datei("t.png"); File.WriteAllText(text, "kein Bild");
        var f = Assert.Throws<HlksFehler>(() => BildDatei.Lesen(text));
        Assert.Contains("weder PNG noch JPEG", f.Message);
    }

    // ---------------------------------------------------------------- PDF-Seite

    [Fact]
    public void PdfSeite_Rendern_GroesseUndInhalt()
    {
        using var o = new Ordner();
        var pdf = Pdf(o, "a.pdf", 210, 297, seiten: 2);
        var (seiten, bw, bh) = PdfSeite.Mass(pdf, 1);
        Assert.Equal(2, seiten);
        Assert.Equal(210 / 25.4 * 72, bw, 0); Assert.Equal(297 / 25.4 * 72, bh, 0);
        var png = o.Datei("s.png");
        var (w, h) = PdfSeite.Rendern(pdf, 1, 150, png);
        Assert.InRange(w, 1239, 1241); Assert.InRange(h, 1753, 1755);
        var (pw, ph, rgb) = PngLesen(png);
        Assert.Equal((w, h), (pw, ph));
        (byte R, byte G, byte B) Pixel(double xMm, double yMmVonOben) { int x = (int)(xMm / 25.4 * 150), y = (int)(yMmVonOben / 25.4 * 150); int i = 3 * (y * pw + x); return (rgb[i], rgb[i + 1], rgb[i + 2]); }
        var rot = Pixel(45, 35);                          // das rote Rechteck (20…70 mm, 20…50 mm vom oberen Rand)
        Assert.InRange(rot.R, 195, 205); Assert.True(rot.G < 5 && rot.B < 5);
        Assert.True(Pixel(150, 200) is { R: >= 250, G: >= 250, B: >= 250 });  // Papier weiss
        Assert.True(Pixel(45, 60) is { R: >= 250, G: >= 250, B: >= 250 });    // unter dem Rechteck

        var f = Assert.Throws<HlksFehler>(() => PdfSeite.Rendern(pdf, 3, 150, png));
        Assert.Contains("2 Seite", f.Message);
        var gross = Assert.Throws<HlksFehler>(() => PdfSeite.Rendern(pdf, 1, 2400, png));
        Assert.Contains("Millionen", gross.Message); Assert.Contains("kleinere Auflösung", gross.Vorschlag);
        var kaputt = o.Datei("k.pdf"); File.WriteAllText(kaputt, "kein pdf");
        Assert.Throws<HlksFehler>(() => PdfSeite.Mass(kaputt, 1));
    }

    // ---------------------------------------------------------------- einfügen

    [Fact]
    public void Einfuegen_Pdf_MitMassstab_LegtBildInDerRichtigenGroesseAn()
    {
        using var o = new Ordner();
        var p = Projekt();
        var e = Ok(Einfuegen(p, Pdf(o, "plan.pdf", 210, 297, 2), "EG", new JsonObject { ["massstab"] = 100, ["seite"] = 2 }));
        var ul = Unterlage(p);
        Assert.Equal("pdf", ul.Text("art"));
        Assert.Equal(2, ul.Zahl("seite"));
        Assert.Equal(100, ul.Zahl("massstab"));
        var l = UnterlagenLage.Lesen(ul);
        // A4 1:100 = 21.0 × 29.7 m; Bildpunkt = 100 · 210 mm / Breite in Bildpunkten
        var png = BildDatei.Lesen(Path.Combine(Unterlagen.Ordner(p), ul.Text("datei")! + ".seite2.png"));
        Assert.InRange(l.Faktor / (100 * 210.0 / png.Breite), 0.999, 1.001);
        Assert.Equal(0, l.Drehung);
        Assert.Equal(l.Faktor * png.Hoehe, l.Dy, 3);                                   // unten links auf (0, 0)
        Nahe([0, 0, 21000, 29700], Box(ul.Geometrie!.Punkte), 30);   // ganze Bildpunkte: bis 0.05 % Abweichung
        Assert.Equal(l.Faktor, ul.Eigenschaft("transformation")!.Inhalt!["erwartung"]!.GetValue<double>(), 6);
        Assert.Equal(150, ul.Eigenschaft("transformation")!.Inhalt!["dpi"]!.GetValue<double>());
        Assert.Contains(e.Hinweise, h => h.Contains("Seite 2 von 2") && h.Contains("150 dpi"));
        Assert.Contains(e.Hinweise, h => h.Contains("Massstab 1:100") && h.Contains("21.0 × 29.7 m"));
        Assert.Contains(e.Hinweise, h => h.Contains("unterlage_kalibrieren") && h.Contains("von oben"));
    }

    [Fact]
    public void Einfuegen_Pdf_OhneMassstab_PapiergroesseMitHinweis_AndereAufloesung_EigeneDatei()
    {
        using var o = new Ordner();
        var p = Projekt();
        var e = Ok(Einfuegen(p, Pdf(o, "plan.pdf"), "EG", new JsonObject { ["dpi"] = 72 }));
        var ul = Unterlage(p);
        Nahe([0, 0, 210, 297], Box(ul.Geometrie!.Punkte), 1);   // 210 × 297 mm = Papiergrösse
        Assert.Contains(e.Hinweise, h => h.Contains("Kein massstab"));
        Assert.Null(ul.Eigenschaft("transformation")!.Inhalt!["erwartung"]);
        Assert.True(File.Exists(Path.Combine(Unterlagen.Ordner(p), ul.Text("datei")! + ".seite1-72dpi.png")));
        var png = BildDatei.Lesen(Path.Combine(Unterlagen.Ordner(p), ul.Text("datei")! + ".seite1-72dpi.png"));
        Assert.Equal(595, png.Breite);
        // ohne Erwartung keine Plausibilitätswarnung, aber ein Hinweis
        var k = Ok(Kalibrieren(p, "UL-1", [[0, 0], [100, 0]], [[0, 0], [5000, 0]]));
        Assert.DoesNotContain(k.Hinweise, h => h.Contains("ACHTUNG"));
        Assert.Contains(k.Hinweise, h => h.Contains("keine Erwartung"));
    }

    [Fact]
    public void Einfuegen_Bild_MitMassstabUndDpi_PixelKalibrierenVonOben()
    {
        using var o = new Ordner();
        var p = Projekt();
        // 100 × 50 Bildpunkte bei 25.4 dpi = 1 mm Papier je Bildpunkt; 1:100 → 100 mm im Modell je Bildpunkt
        var datei = Png(o, "foto.png", 100, 50, (0, 128, 0));
        var e = Ok(Einfuegen(p, datei, "EG", new JsonObject { ["massstab"] = 100, ["dpi"] = 25.4 }));
        var ul = Unterlage(p);
        Assert.Equal("bild", ul.Text("art"));
        var l = UnterlagenLage.Lesen(ul);
        Assert.Equal(100, l.Faktor, 9); Assert.Equal(5000, l.Dy, 9);
        Nahe([0, 0, 10000, 5000], Box(ul.Geometrie!.Punkte));
        Assert.Contains(e.Hinweise, h => h.Contains("100 × 50 Bildpunkte") && h.Contains("angegeben"));

        // untere linke Ecke (0, 50) und untere rechte (100, 50) auf die Zielpunkte; y der Bildpunkte zählt von oben
        var k = Ok(Kalibrieren(p, "UL-1", [[0, 50], [100, 50]], [[1000, 1000], [21000, 1000]]));
        var l2 = UnterlagenLage.Lesen(Unterlage(p));
        Assert.Equal(200, l2.Faktor, 6); Assert.Equal(0, l2.Drehung, 6);
        Assert.Contains(k.Hinweise, h => h.Contains("ACHTUNG") && h.Contains("100 %"));   // doppelt so gross wie erwartet
        var ecken = Unterlage(p).Geometrie!.Punkte;
        Nahe([1000, 1000], ecken[0]); Nahe([21000, 1000], ecken[1]);       // unten links, unten rechts
        Nahe([21000, 11000], ecken[2]); Nahe([1000, 11000], ecken[3]);     // oben rechts, oben links
        // obere linke Bildecke (0, 0) liegt dann bei (1000, 11000)
        var (x, y) = l2.AufPixel(0, 0);
        Assert.Equal(1000, x, 6); Assert.Equal(11000, y, 6);

        // nur verschieben: Bildpunkt (0, 0) nach (0, 0)
        Ok(Kalibrieren(p, "UL-1", [[0, 0]], [[0, 0]], nurVerschieben: true));
        Nahe([0, -10000, 20000, 0], Box(Unterlage(p).Geometrie!.Punkte));
    }

    [Fact]
    public void Einfuegen_Bild_NochmalMitAnderemMassstab_BerechnetDieLageNeu_GleicheAngabenAendernNichts()
    {
        using var o = new Ordner();
        var p = Projekt();
        var datei = Png(o, "foto.png", 100, 50, (0, 128, 0));
        var par = new JsonObject { ["massstab"] = 100, ["dpi"] = 25.4 };
        Ok(Einfuegen(p, datei, "EG", par));
        Ok(Kalibrieren(p, "UL-1", [[0, 50], [100, 50]], [[0, 0], [20000, 0]]));
        Assert.Equal(200, UnterlagenLage.Lesen(Unterlage(p)).Faktor, 6);

        var gleich = Ok(Einfuegen(p, datei, "EG", par));                       // dieselben Angaben: nichts ändert sich
        Assert.Null(gleich.PaketId);
        Assert.Equal(200, UnterlagenLage.Lesen(Unterlage(p)).Faktor, 6);

        var anders = Ok(Einfuegen(p, datei, "EG", new JsonObject { ["massstab"] = 50, ["dpi"] = 25.4 }));
        Assert.Equal(50, UnterlagenLage.Lesen(Unterlage(p)).Faktor, 6);       // neu aus Massstab und Auflösung
        Assert.Equal(0, UnterlagenLage.Lesen(Unterlage(p)).Dx, 6);
        Assert.Contains(anders.Hinweise, h => h.Contains("Kalibrierung ist verworfen"));
        Assert.Single(p.Modell.DerKlasse("Unterlage"));
    }

    [Fact]
    public void Einfuegen_Bild_DpiAusDerDatei_OhneAngabe150_Fehler()
    {
        using var o = new Ordner();
        var p = Projekt();
        var e = Ok(Einfuegen(p, Png(o, "scan.png", 300, 300, (9, 9, 9), dpi: 300), "EG", new JsonObject { ["massstab"] = 50 }));
        Assert.Contains(e.Hinweise, h => h.Contains("300 dpi") && h.Contains("aus der Datei"));
        Assert.Equal(25.4 / 300 * 50, UnterlagenLage.Lesen(Unterlage(p)).Faktor, 4);

        var q = Projekt();
        var jpgOhne = o.Datei("x.jpg");
        File.WriteAllBytes(jpgOhne, [0xFF, 0xD8, 0xFF, 0xC0, 0x00, 0x0B, 8, 0, 20, 0, 40, 1, 1, 0x11, 0, 0xFF, 0xDA, 0, 2]);
        var ohne = Ok(Einfuegen(q, jpgOhne, "EG", new JsonObject { ["massstab"] = 100 }));
        Assert.Contains(ohne.Hinweise, h => h.Contains("150 dpi") && h.Contains("keine"));

        var gif = o.Datei("a.gif"); File.WriteAllBytes(gif, [(byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a', 1, 0, 1, 0]);
        var f = Einfuegen(q, gif, "EG", new JsonObject { ["art"] = "bild" });
        Assert.False(f.Angenommen); Assert.Contains("weder PNG noch JPEG", f.Fehler); Assert.Contains("PNG oder JPEG", f.Vorschlag);
    }

    // ---------------------------------------------------------------- Plangrafik und Plan-PDF

    [Fact]
    public void Plan_Bild_EinPrimitivMitVierEckenQuelleUndDeckkraft()
    {
        using var o = new Ordner();
        var p = Projekt();
        Ok(Einfuegen(p, Png(o, "foto.png", 100, 50, (255, 0, 0)), "EG", new JsonObject { ["massstab"] = 1, ["dpi"] = 25.4 }));   // 1 mm je Bildpunkt
        Ok(Kalibrieren(p, "UL-1", [[0, 50], [100, 50]], [[1000, 2000], [1000, 2100]]));                                     // 90° gedreht, Faktor 1
        var teil = Assert.Single(PlanGrundlage(p).Teile);
        Assert.Equal("grundlage", teil.Ebene);
        var bild = Assert.Single(teil.Primitive);
        Assert.Equal("bild", bild.Art);
        Assert.Equal(Unterlage(p).Id, bild.Quelle);
        Assert.Equal(0.5, bild.Stil.Deckkraft);
        Assert.Null(bild.Stil.Stift);
        Assert.Equal(4, bild.Punkte.Count);
        // unten links (0,50)→(1000,2000); unten rechts (100,50)→(1000,2100); oben rechts (100,0)→(1050,2100); oben links (0,0)→(1050,2000)
        Nahe([1000, 2000], bild.Punkte[0], 1e-6); Nahe([1000, 2100], bild.Punkte[1], 1e-6);
        Nahe([950, 2100], bild.Punkte[2], 1e-6); Nahe([950, 2000], bild.Punkte[3], 1e-6);
        Assert.NotNull(UnterlagenBild.PfadVon(Unterlage(p).Id));
        Assert.Equal("image/png", UnterlagenBild.Medientyp(UnterlagenBild.Pfad(p, Unterlage(p))));

        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "UL-1", ["eigenschaften"] = new JsonObject { ["anzeigen"] = false } }));
        Assert.Empty(PlanGrundlage(p).Teile);
    }

    [Fact]
    public void PlanPdf_ZeichnetDasBildAufrechtUndAnDerRichtigenStelle()
    {
        using var o = new Ordner();
        var p = Projekt();
        // Bild 10 m × 10 m: obere Hälfte rot, untere blau
        int b = 40, h = 40;
        var bgra = new byte[b * h * 4];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < b; x++)
            {
                int i = 4 * (y * b + x);
                if (y < h / 2) { bgra[i + 2] = 255; } else { bgra[i] = 255; }
                bgra[i + 3] = 255;
            }
        var datei = o.Datei("zwei.png");
        PngSchreiber.Schreiben(datei, b, h, bgra, 25.4);
        Ok(Einfuegen(p, datei, "EG", new JsonObject { ["massstab"] = 250, ["dpi"] = 25.4 }));   // 1 mm Papier je Bildpunkt, 1:250 → 250 mm je Bildpunkt → 10 m
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "UL-1", ["eigenschaften"] = new JsonObject { ["deckkraft"] = 1 } }));
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "AN-EG", ["eigenschaften"] = new JsonObject { ["massstab"] = 100 } }));
        Ok(p.Ausfuehren("planblatt_anlegen", new JsonObject { ["nummer"] = "T-1", ["titel"] = "Test", ["format"] = "A3" }));
        Ok(p.Ausfuehren("ansicht_platzieren", new JsonObject { ["planblatt"] = "PB-1", ["ansicht"] = "AN-EG", ["lage"] = new JsonArray(210, 160) }));

        var bytes = Planausgabe.Pdf(p, "PB-1", grafik: (pr, a) => PlanGrundlage(pr, a));
        var pdf = o.Datei("plan.pdf");
        File.WriteAllBytes(pdf, bytes);
        var (w, hoehe, rgb) = Raster(pdf, 1.0);   // 72 dpi
        // rote und blaue Bildpunkte suchen
        var rot = new List<(int X, int Y)>(); var blau = new List<(int X, int Y)>();
        for (int y = 0; y < hoehe; y++)
            for (int x = 0; x < w; x++)
            {
                int i = 3 * (y * w + x);
                if (rgb[i] > 200 && rgb[i + 1] < 60 && rgb[i + 2] < 60) rot.Add((x, y));
                else if (rgb[i + 2] > 200 && rgb[i] < 60 && rgb[i + 1] < 60) blau.Add((x, y));
            }
        Assert.True(rot.Count > 5000 && blau.Count > 5000, $"rot {rot.Count}, blau {blau.Count}");
        // 10 m bei 1:100 = 100 mm = 283 Punkte (72 dpi): jede Hälfte etwa 283 × 142
        Assert.InRange(rot.Count, 283 * 142 * 0.9, 283 * 142 * 1.1);
        Assert.InRange(blau.Count, 283 * 142 * 0.9, 283 * 142 * 1.1);
        // Rot oben, Blau unten (y wächst nach unten), gleiche Spalten
        Assert.True(rot.Average(q => q.Y) < blau.Average(q => q.Y) - 100, "Bild steht auf dem Kopf");
        Assert.Equal(rot.Min(q => q.X), blau.Min(q => q.X));
        Assert.Equal(rot.Max(q => q.X), blau.Max(q => q.X));
        Assert.InRange(blau.Min(q => q.Y) - rot.Max(q => q.Y), 1, 12);   // Übergang rot/blau (Glättung beim Vergrössern), sonst lückenlos
    }

    [Fact]
    public void PlanPdf_Deckkraft_hellt_dasBildAuf()
    {
        using var o = new Ordner();
        var p = Projekt();
        Ok(Einfuegen(p, Png(o, "rot.png", 40, 40, (255, 0, 0), 25.4), "EG", new JsonObject { ["massstab"] = 250, ["dpi"] = 25.4 }));   // deckkraft 0.5
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "AN-EG", ["eigenschaften"] = new JsonObject { ["massstab"] = 100 } }));
        Ok(p.Ausfuehren("planblatt_anlegen", new JsonObject { ["nummer"] = "T-1", ["titel"] = "Test", ["format"] = "A3" }));
        Ok(p.Ausfuehren("ansicht_platzieren", new JsonObject { ["planblatt"] = "PB-1", ["ansicht"] = "AN-EG", ["lage"] = new JsonArray(210, 160) }));
        var pdf = o.Datei("plan.pdf");
        File.WriteAllBytes(pdf, Planausgabe.Pdf(p, "PB-1", grafik: (pr, a) => PlanGrundlage(pr, a)));
        var (w, hoehe, rgb) = Raster(pdf, 1.0);
        int n = 0;
        for (int i = 0; i < w * hoehe; i++)
            if (rgb[3 * i] > 240 && rgb[3 * i + 1] is > 100 and < 160 && rgb[3 * i + 2] is > 100 and < 160) n++;   // halb deckendes Rot auf Weiss ≈ (255, 128, 128)
        Assert.True(n > 20000, $"halbtransparentes Rot: {n} Punkte");
    }

    /// <summary>Rendert die erste Seite eines PDF mit Docnet und gibt RGB zurück.</summary>
    static (int Breite, int Hoehe, byte[] Rgb) Raster(string pdf, double massstab)
    {
        using var doc = DocLib.Instance.GetDocReader(pdf, new PageDimensions(massstab));
        using var seite = doc.GetPageReader(0);
        int w = seite.GetPageWidth(), h = seite.GetPageHeight();
        var bgra = seite.GetImage(new Docnet.Core.Converters.NaiveTransparencyRemover(255, 255, 255));
        var rgb = new byte[w * h * 3];
        for (int i = 0; i < w * h; i++) { rgb[3 * i] = bgra[4 * i + 2]; rgb[3 * i + 1] = bgra[4 * i + 1]; rgb[3 * i + 2] = bgra[4 * i]; }
        return (w, h, rgb);
    }
}
