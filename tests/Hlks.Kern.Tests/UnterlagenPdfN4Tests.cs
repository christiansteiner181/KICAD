using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using static Hlks.Kern.Tests.GrundlagenHilfe;
using static Hlks.Kern.Tests.UnterlagenHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Nachbearbeitung N4 (20.10.2026), PDF-Leser 2: (1) Dezimalzahlen, deren Ganzzahl und Nachkommastellen in getrennten Zeichenläufen
/// stehen («101» «.4», «4» «.» «3»), und Glyphen, die eine Schrift auf Leerraum plus Punkt abbildet (« .», Befund Geberit-HDPE-Katalog 2020),
/// werden eine Zahl – echte Spalten bleiben getrennt. (2) Zeilenleser der Datenblatt-Tabellen («Erstellt mit … Produktdaten»): Fliesstext
/// über der Tabelle verschmilzt die Spalten nicht mehr; jede Zeile behält ihre Werte (45° und 90° laufen nicht ineinander).
/// Alle Testdateien selbst erzeugt (Nachbildung des Aufbaus, erfundene Werte); die echten Herstellerdateien liegen nie im Repo.</summary>
public class UnterlagenPdfN4Tests
{
    static UnterlagenBefunde LesenPdf(string pfad) => new PdfLeser().Lesen(pfad, Path.GetFileName(pfad), "0123456789abcdef", "pdf");

    static string[][] Daten(UnterlagenBefund t) =>
        ((JsonArray)((JsonObject)t.Wert!)["daten"]!).Select(r => ((JsonArray)r!).Select(x => x!.GetValue<string>()).ToArray()).ToArray();

    static string[] Kopf(UnterlagenBefund t) => ((JsonArray)((JsonObject)t.Wert!)["kopf"]!).Select(x => x!.GetValue<string>()).ToArray();

    // ---------- (1) getrennte Zeichenläufe (PDFsharp) ----------

    /// <summary>Tabelle «Art.-Nr. | d [mm] | di [mm] | s [mm]» (Arial 9 pt). di und s stehen in getrennten Zeichenläufen: Zeile 1 «101» und «.4»
    /// mit 2.5 pt Lücke (gleiche Schrift – PdfPig trennt ab 2 pt), Zeile 2 Nachkommastellen fett mit 0.5 pt Lücke (Schriftwechsel), Zeile 3
    /// drei Läufe «6» «.» «2» bzw. Komma «147» «,6». Darunter die Gegenprobe zweier echter Spalten: «12» und «.5» mit 15 pt Lücke, «7» und
    /// «.5» mit anderthalb Zeichenbreiten (7.5 pt, Ziffer bei Arial 9 pt 5 pt breit) – beide bleiben getrennt.</summary>
    static string LaeufePdf(Ordner o)
    {
        var doc = new PdfDocument();
        var s = doc.AddPage(); s.Width = XUnit.FromPoint(595); s.Height = XUnit.FromPoint(842);
        using (var g = XGraphics.FromPdfPage(s))
        {
            _ = new PdfZeichner(g, 297);
            var f = new XFont("Arial", 9); var fett = new XFont("Arial", 9, XFontStyleEx.Bold);
            void T(string t, double x, double y, XFont? schrift = null) => g.DrawString(t, schrift ?? f, XBrushes.Black, x, y);
            double W(string t) => g.MeasureString(t, f).Width;
            /// Zahl in zwei (oder drei) Läufen: Ganzzahl, Lücke, Rest
            void Getrennt(string ganz, string rest, double x, double y, double luecke, XFont? restSchrift = null)
            {
                T(ganz, x, y);
                if (rest.Length > 1 && rest[0] is '.' or ',' && restSchrift is null && luecke < 0)
                {   // drei Läufe: «4» «.» «9», je 2 pt Lücke
                    T(rest[..1], x + W(ganz) + 2, y);
                    T(rest[1..], x + W(ganz) + 2 + W(rest[..1]) + 2, y);
                    return;
                }
                T(rest, x + W(ganz) + luecke, y, restSchrift);
            }
            double[] xs = [50, 150, 230, 310];
            string[] kopf = ["Art.-Nr.", "d", "di", "s"];
            for (int i = 0; i < 4; i++) T(kopf[i], xs[i], 200);
            for (int i = 1; i < 4; i++) T("[mm]", xs[i], 212);
            T("T-110", xs[0], 230); T("110", xs[1], 230); Getrennt("101", ".4", xs[2], 230, 2.5); Getrennt("4", ".3", xs[3], 230, 2.5);
            T("T-125", xs[0], 244); T("125", xs[1], 244); Getrennt("115", ".2", xs[2], 244, 0.5, fett); Getrennt("4", ".9", xs[3], 244, 0.5, fett);
            T("T-160", xs[0], 258); T("160", xs[1], 258); Getrennt("147", ",6", xs[2], 258, 3); Getrennt("6", ".2", xs[3], 258, -1);
            // Gegenprobe: zwei echte Spalten
            T("Spalten", 50, 400); T("12", 150, 400); T(".5", 150 + W("12") + 15, 400);
            T("Zeichenbreite", 50, 420); T("7", 150, 420); T(".5", 150 + W("7") + 7.5, 420);
        }
        var pfad = o.Datei("laeufe.pdf");
        doc.Save(pfad);
        return pfad;
    }

    [Fact]
    public void Getrennte_Zeichenlaeufe_ergeben_eine_Zahl_in_der_Zelle()
    {
        using var o = new Ordner();
        var b = LesenPdf(LaeufePdf(o));
        Assert.Equal("pdf 2", b.Leser);
        var t = Art(b, "tabelle").Single();
        Assert.Equal(["Art.-Nr.", "d [mm]", "di [mm]", "s [mm]"], Kopf(t));
        var daten = Daten(t);
        Assert.Equal(["T-110", "110", "101.4", "4.3"], daten[0]);    // gleiche Schrift, 2.5 pt Lücke
        Assert.Equal(["T-125", "125", "115.2", "4.9"], daten[1]);    // Nachkommastellen fett, 0.5 pt Lücke
        Assert.Equal(["T-160", "160", "147,6", "6.2"], daten[2]);    // Komma; drei Läufe «6» «.» «2»
        foreach (var (zeile, di, s) in new[] { (1, 101.4, 4.3), (2, 115.2, 4.9), (3, 147.6, 6.2) })
        {
            var zdi = Zelle(b, t.Id, zeile, 3);
            Assert.Equal(di, Zahl(zdi.Wert), 6);
            Assert.Equal("mm", zdi.Einheit);
            Assert.Equal(s, Zahl(Zelle(b, t.Id, zeile, 4).Wert), 6);
        }
        Assert.Contains(Art(b, "wort"), w => w.Text() == "101.4");
        Assert.DoesNotContain(Art(b, "wort"), w => w.Text() is ".4" or "." or ",6");
        Assert.Contains(Art(b, "zeile"), z => z.Text() == "T-110 | 110 | 101.4 | 4.3");
    }

    [Fact]
    public void Gegenprobe_echte_Spalten_bleiben_getrennt()
    {
        using var o = new Ordner();
        var b = LesenPdf(LaeufePdf(o));
        // 15 pt Lücke: zwei Zellen der Zeile
        Assert.Contains(Art(b, "zeile"), z => z.Text() == "Spalten | 12 | .5");
        // anderthalb Zeichenbreiten: zwei Wörter, kein «7.5»
        Assert.DoesNotContain(Art(b, "wort"), w => w.Text() is "7.5" or "12.5");
        Assert.Contains(Art(b, "wort"), w => w.Text() == "7");
        Assert.Equal(2, Art(b, "wort").Count(w => w.Text() == ".5"));
    }

    // ---------- (1) Glyphe « .» (Rohes PDF mit ToUnicode) ----------

    /// <summary>Kleines PDF von Hand (Helvetica, WinAnsi) mit einer ToUnicode-Tabelle, die den Punkt (Code 2E) auf « .» abbildet – wie die Schrift
    /// des Geberit-HDPE-Katalogs 2020. Tabelle «Art. no. | DN | d, ø | di, ø | s» mit Einheitenzeile und drei Zeilen (erfundene Werte).</summary>
    static string GlyphePdf(Ordner o)
    {
        var inhalt = new StringBuilder();
        void T(string t, double x, double y) => inhalt.Append(CultureInfo.InvariantCulture, $"BT /F1 7 Tf 1 0 0 1 {x} {y} Tm ({t}) Tj ET\n");
        double[] xs = [42.5, 120, 190, 230, 270];
        string[] kopf = ["Art. no.", "DN", "d, ø", "di, ø", "s"];
        for (int i = 0; i < 5; i++) T(kopf[i].Replace("ø", "\\370"), xs[i], 622);
        for (int i = 2; i < 5; i++) T("[mm]", xs[i], 613);
        string[][] z = [["900.000.16.0", "90", "90", "83", "3.5"], ["901.000.16.0", "100", "110", "101.4", "4.3"], ["902.000.16.0", "125", "125", "115.2", "4.9"]];
        for (int r = 0; r < 3; r++) for (int i = 0; i < 5; i++) T(z[r][i], xs[i], 602 - 10.7 * r);
        const string cmap = "/CIDInit /ProcSet findresource begin\n12 dict begin\nbegincmap\n/CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def\n" +
            "/CMapName /Adobe-Identity-UCS def\n/CMapType 2 def\n1 begincodespacerange\n<00> <FF>\nendcodespacerange\n" +
            "3 beginbfrange\n<20> <2D> <0020>\n<2F> <7E> <002F>\n<F8> <F8> <00F8>\nendbfrange\n1 beginbfchar\n<2E> <0020002E>\nendbfchar\n" +
            "endcmap\nCMapName currentdict /CMap defineresource pop\nend\nend\n";
        // eigene Schrift ohne eingebettetes Programm (PdfPig wendet ToUnicode bei den 14 Standardschriften nicht an): Breiten wie Helvetica
        var breiten = string.Join(" ", Enumerable.Range(32, 224).Select(c => c is 32 or 44 or 46 ? 278 : c is 105 or 108 ? 222 : 556));
        var objekte = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            $"<< /Type /Font /Subtype /Type1 /BaseFont /TestGrotesk /FirstChar 32 /LastChar 255 /Widths [{breiten}] /FontDescriptor 7 0 R /Encoding /WinAnsiEncoding /ToUnicode 6 0 R >>",
            $"<< /Length {Encoding.Latin1.GetByteCount(inhalt.ToString())} >>\nstream\n{inhalt}endstream",
            $"<< /Length {Encoding.Latin1.GetByteCount(cmap)} >>\nstream\n{cmap}endstream",
            "<< /Type /FontDescriptor /FontName /TestGrotesk /Flags 32 /FontBBox [-166 -225 1000 931] /ItalicAngle 0 /Ascent 718 /Descent -207 /CapHeight 718 /StemV 88 >>",
        };
        var pdf = new StringBuilder("%PDF-1.4\n");
        var lagen = new List<int>();
        for (int i = 0; i < objekte.Length; i++)
        {
            lagen.Add(Encoding.Latin1.GetByteCount(pdf.ToString()));
            pdf.Append(CultureInfo.InvariantCulture, $"{i + 1} 0 obj\n{objekte[i]}\nendobj\n");
        }
        int xref = Encoding.Latin1.GetByteCount(pdf.ToString());
        pdf.Append(CultureInfo.InvariantCulture, $"xref\n0 {objekte.Length + 1}\n0000000000 65535 f \n");
        foreach (var l in lagen) pdf.Append(CultureInfo.InvariantCulture, $"{l:D10} 00000 n \n");
        pdf.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {objekte.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        var pfad = o.Datei("glyphe.pdf");
        File.WriteAllBytes(pfad, Encoding.Latin1.GetBytes(pdf.ToString()));
        return pfad;
    }

    [Fact]
    public void Glyphe_Leerraum_plus_Punkt_ergibt_Dezimalzahl_und_Artikelnummer_ohne_Luecke()
    {
        using var o = new Ordner();
        var pfad = GlyphePdf(o);
        // Voraussetzung: PdfPig liefert den Punkt wirklich als « .» (sonst prüft der Test nichts)
        using (var roh = UglyToad.PdfPig.PdfDocument.Open(pfad))
            Assert.Contains(roh.GetPage(1).Letters, l => l.Value == " .");
        var b = LesenPdf(pfad);
        Assert.Contains(Art(b, "zeile"), z => z.Text() == "901.000.16.0 | 100 | 110 | 101.4 | 4.3");
        Assert.Contains(Art(b, "zeile"), z => z.Text() == "Art. no. | DN | d, ø | di, ø | s");
        var t = Art(b, "tabelle").Single();
        Assert.Equal(["Art. no.", "DN", "d, ø [mm]", "di, ø [mm]", "s [mm]"], Kopf(t));
        Assert.Equal(["901.000.16.0", "100", "110", "101.4", "4.3"], Daten(t)[1]);
        Assert.Equal(101.4, Zahl(Zelle(b, t.Id, 2, 4).Wert), 6);
        Assert.Equal("mm", Zelle(b, t.Id, 2, 4).Einheit);
        Assert.Equal(4.3, Zahl(Zelle(b, t.Id, 2, 5).Wert), 6);
        Assert.Equal(3.5, Zahl(Zelle(b, t.Id, 1, 5).Wert), 6);
        Assert.DoesNotContain(b.Befunde, x => x.Art is "wort" or "zeile" or "zelle" && x.Text().Contains(" ."));
    }

    // ---------- (2) Zeilenleser: Datenblatt mit Fliesstext über der Tabelle ----------

    /// <summary>Nachbildung des Aufbaus eines Datenblatts «Erstellt mit … Produktdaten» (Arial 8 pt, Lagen wie im Befund): Titel, Aufzählungen
    /// «Verwendungszwecke | Eigenschaften» in zwei Spalten (10.5 pt Zeilenabstand), «Technische Daten», «Werkstoff | Stahl unlegiert …»,
    /// dann die Tabelle «Art.-Nr. | DN | d, ø | arc | L | Z | VE1 | VE2 | VE3» mit Zeilen abwechselnd 45° und 90° (15.25 pt, Trennlinien),
    /// unten die Fusszeile. Werte erfunden; VE1 nur in den unteren Zeilen besetzt.</summary>
    public static readonly (string Nr, string Dn, string D, string Arc, string L, string Z, string? Ve1, string Ve2, string? Ve3)[] Boegen =
    [
        ("T0601", "10", "12 mm", "45 °", "3.1 cm", "1.4 cm", null, "5 St.", "25 St."),
        ("T0101", "10", "12 mm", "90 °", "4.1 cm", "2.4 cm", null, "20 St.", "100 St."),
        ("T0602", "12", "15 mm", "45 °", "3.5 cm", "1.6 cm", null, "20 St.", "200 St."),
        ("T0102", "12", "15 mm", "90 °", "4.8 cm", "2.8 cm", null, "20 St.", "200 St."),
        ("T0603", "15", "18 mm", "45 °", "3.6 cm", "1.7 cm", null, "20 St.", "200 St."),
        ("T0103", "15", "18 mm", "90 °", "5.2 cm", "3.2 cm", null, "20 St.", "200 St."),
        ("T0604", "20", "22 mm", "45 °", "4.1 cm", "2.1 cm", null, "20 St.", "100 St."),
        ("T0104", "20", "22 mm", "90 °", "6.0 cm", "3.9 cm", null, "20 St.", "100 St."),
        ("T0609", "65", "76.1 mm", "45 °", "10.2 cm", "4.9 cm", "1 St.", "15 St.", null),
        ("T0109", "65", "76.1 mm", "90 °", "15.8 cm", "10.5 cm", "1 St.", "10 St.", null),
    ];

    static string DatenblattPdf(Ordner o)
    {
        var doc = new PdfDocument();
        var s = doc.AddPage(); s.Width = XUnit.FromPoint(595); s.Height = XUnit.FromPoint(842);
        using (var g = XGraphics.FromPdfPage(s))
        {
            _ = new PdfZeichner(g, 297);
            var f = new XFont("Arial", 8); var titel = new XFont("Arial", 11, XFontStyleEx.Bold);
            void T(string t, double x, double yUnten) => g.DrawString(t, f, XBrushes.Black, x, 842 - yUnten);   // y wie im Befund: pt von unten (Grundlinie)
            g.DrawString("Testfirma C-Stahl Bogen", titel, XBrushes.Black, 28.4, 842 - 765);
            T("Verwendungszwecke", 28.4, 628.3); T("Eigenschaften", 301.6, 628.3);
            string[] links = ["• Für Kühl- und Heizungswasser ohne Frostschutzmittel", "• Für Kühl- und Heizungswasser mit Frostschutzmittel", "• Für Heizungswasser bis 120 °C",
                "• Für Löschwasser (nass)", "• Für Sprinkler (nass)", "• Für Druckluft (Klasse 3)", "• Für Haustechnik, Industrie und Schiffbau"];
            string?[] rechts = ["• Pressindikator", "• Unverpresst undicht", "• Aussen verzinkt", "• Dichtring aus Kautschuk schwarz", "• Pressmuffe mit transparentem Schutzstopfen", null, "Technische Daten"];
            for (int i = 0; i < links.Length; i++)
            {
                T(links[i], 29.4, 614.3 - 10.5 * i);
                if (rechts[i] is { } r) T(r, i == 6 ? 301.6 : 302.7, 614.3 - 10.5 * i);
            }
            T("Werkstoff", 306.6, 535.8); T("Stahl unlegiert 1.0034 E195 (DIN EN 10305)", 364.4, 535.8);
            double[] xs = [33.4, 71.7, 116.4, 193.1, 237.3, 309.6, 381.9, 430.4, 487.5];
            string[] kopf = ["Art.-Nr.", "DN", "d, ø", "arc", "L", "Z", "VE1", "VE2", "VE3"];
            for (int i = 0; i < kopf.Length; i++) T(kopf[i], xs[i], 511.3);
            var stift = new XPen(XColors.Gray, 0.5);
            g.DrawLine(stift, 28.4, 842 - 507.5, 520, 842 - 507.5);
            for (int r = 0; r < Boegen.Length; r++)
            {
                var z = Boegen[r]; double y = 496.0 - 15.25 * r;
                string?[] werte = [z.Nr, z.Dn, z.D, z.Arc, z.L, z.Z, z.Ve1, z.Ve2, z.Ve3];
                for (int i = 0; i < werte.Length; i++) if (werte[i] is { } w) T(w, xs[i], y);
                g.DrawLine(stift, 28.4, 842 - (y - 4.2), 520, 842 - (y - 4.2));
            }
            T("Erstellt mit Testdaten - 20/10/2026", 181.2, 24.2);
        }
        var pfad = o.Datei("datenblatt_boegen.pdf");
        doc.Save(pfad);
        return pfad;
    }

    [Fact]
    public void Datenblatt_Fliesstext_ueber_der_Tabelle_verschmilzt_die_Spalten_nicht()
    {
        using var o = new Ordner();
        var b = LesenPdf(DatenblattPdf(o));
        var t = Art(b, "tabelle").Single(x => Daten(x).Any(r => r[0] == "T0601"));
        Assert.Equal(["Art.-Nr.", "DN", "d, ø", "arc", "L", "Z", "VE1", "VE2", "VE3"], Kopf(t));
        Assert.Equal(1, ((JsonObject)t.Wert!)["kopfZeilen"]!.GetValue<int>());
        var daten = Daten(t);
        Assert.Equal(Boegen.Length, daten.Length);
        // jede Zeile behält ihre eigenen Werte: Winkel, L und Z gehören zur Art.-Nr. derselben Zeile (45° und 90° laufen nicht ineinander)
        for (int r = 0; r < Boegen.Length; r++)
        {
            var e = Boegen[r];
            Assert.Equal([e.Nr, e.Dn, e.D, e.Arc, e.L, e.Z, e.Ve1 ?? "", e.Ve2, e.Ve3 ?? ""], daten[r]);
        }
        // z45 von d15, d18, d22 aus der Zelle der Spalte Z
        foreach (var (zeile, z) in new[] { (3, "1.6 cm"), (5, "1.7 cm"), (7, "2.1 cm") })
        {
            var c = Zelle(b, t.Id, zeile, 6);
            Assert.Equal("Z", c.Zusatz!["kopf"]!.GetValue<string>());
            Assert.Equal(z, c.Zusatz["text"]!.GetValue<string>());
            Assert.Equal("45 °", Zelle(b, t.Id, zeile, 4).Zusatz!["text"]!.GetValue<string>());
        }
        // Fliesstext gehört nicht zur Datentabelle; «Werkstoff | Stahl …» ist kein Kopf
        Assert.DoesNotContain(daten.SelectMany(r => r), c => c.Contains("Frostschutz") || c.Contains("Stahl") || c.Contains("Werkstoff"));
        Assert.DoesNotContain(Kopf(t), k => k.Contains("Werkstoff") || k.Contains("Stahl"));
    }

    [Fact]
    public void Kurzer_Sammelkopf_ueber_mehreren_Spalten_bleibt_Kopf()
    {
        // Gegenprobe zur Fliesstext-Regel: «Abmessungen [mm]» (2 Wörter) über L, B, H gehört weiter zum Kopf
        using var o = new Ordner();
        var doc = new PdfDocument();
        var s = doc.AddPage(); s.Width = XUnit.FromPoint(595); s.Height = XUnit.FromPoint(842);
        using (var g = XGraphics.FromPdfPage(s))
        {
            _ = new PdfZeichner(g, 297);
            var f = new XFont("Arial", 9);
            void T(string t, double x, double y) => g.DrawString(t, f, XBrushes.Black, x, y);
            T("Abmessungen [mm]", 150, 186);
            string[] kopf = ["Typ", "L", "B", "H"]; double[] xs = [50, 150, 200, 250];
            for (int i = 0; i < 4; i++) T(kopf[i], xs[i], 200);
            string[][] z = [["A-1", "100", "50", "30"], ["A-2", "120", "60", "35"], ["A-3", "140", "70", "40"]];
            for (int r = 0; r < 3; r++) for (int i = 0; i < 4; i++) T(z[r][i], xs[i], 214 + 14 * r);
        }
        var pfad = o.Datei("sammelkopf.pdf");
        doc.Save(pfad);
        var t = Art(LesenPdf(pfad), "tabelle").Single();
        Assert.Equal("Typ", Kopf(t)[0]);
        Assert.Contains(Kopf(t), k => k.StartsWith("Abmessungen [mm]", StringComparison.Ordinal));
        Assert.Equal(2, ((JsonObject)t.Wert!)["kopfZeilen"]!.GetValue<int>());
        Assert.Equal(["A-2", "120", "60", "35"], Daten(t)[1]);
    }

    // ---------- echte Unterlagen (nur lokal, Grundausstattung; fehlen sie, kehrt der Test zurück) ----------

    static string? Grundausstattung(string praefix)
    {
        var ordner = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "HLKS_CAD_build", "grundausstattung", "e_KICAD", "unterlagen");
        return Directory.Exists(ordner) ? Directory.EnumerateFiles(ordner, praefix + "_*.pdf").FirstOrDefault() : null;
    }

    [Fact]
    public void Echt_Geberit_HDPE_Katalog_Rohrtabelle_Seite7_Dezimalwerte_als_Zahl()
    {
        var pfad = Grundausstattung("78ec99292421");
        if (pfad is null) return;
        var b = LesenPdf(pfad);
        Assert.Contains(Art(b, "zeile"), z => z.Seite == 7 && z.Text() == "367.000.16.0 | 1430012 | 100 | 110 | 101.4 | 4.3 | 5 | 80.7 | 4 | 12.5");
        var t = Art(b, "tabelle").First(x => x.Seite == 7 && Daten(x).Any(r => r[0] == "367.000.16.0"));
        int zeile = Array.FindIndex(Daten(t), r => r[0] == "367.000.16.0") + 1;
        var di = Zelle(b, t.Id, zeile, 5); var sw = Zelle(b, t.Id, zeile, 6);
        Assert.Equal(101.4, Zahl(di.Wert), 6); Assert.Equal("mm", di.Einheit);
        Assert.Equal(4.3, Zahl(sw.Wert), 6); Assert.Equal("mm", sw.Einheit);
        Assert.DoesNotContain(Art(b, "zeile"), z => z.Seite == 7 && z.Text().Contains(" ."));
    }

    [Fact]
    public void Echt_Geberit_Bogen_Datenblatt_Tabelle_mit_Kopf_und_eigenen_Zeilen()
    {
        var pfad = Grundausstattung("a890c8659952");
        if (pfad is null) return;
        var b = LesenPdf(pfad);
        var t = Art(b, "tabelle").Single(x => Daten(x).Any(r => r[0] == "20603"));
        Assert.Equal(["Art.-Nr.", "DN", "d, ø", "arc", "L", "Z", "VE1", "VE2", "VE3"], Kopf(t));
        var daten = Daten(t);
        string Z(string nr) => daten.Single(r => r[0] == nr)[5];
        Assert.Equal(["20602", "12", "15 mm", "45 °"], daten.Single(r => r[0] == "20602")[..4]);
        Assert.Equal("1.6 cm", Z("20602"));   // z45 d15
        Assert.Equal("1.7 cm", Z("20603"));   // z45 d18
        Assert.Equal("2.1 cm", Z("20604"));   // z45 d22
        Assert.Equal("3.3 cm", Z("20103"));   // z90 d18
    }
}
