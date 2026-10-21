using System.Text.Json.Nodes;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using static Hlks.Kern.Tests.GrundlagenHilfe;
using static Hlks.Kern.Tests.UnterlagenHilfe;

namespace Hlks.Kern.Tests;

/// <summary>PDF-Leser 3 (21.10.2026): Rückschritt von Leser 2 bei den Tabellen des Gewinderohr-Katalogs (Temperguss-Fittings, Seiten aus
/// Doppelseiten: der Text der Nachbarseite steht noch im Inhalt, aber ausserhalb der sichtbaren Seite, x negativ).
/// (1) Ursache 1: Das Spaltenraster der Fliesstext-Regel (N4) rechnete mit der rechten Kante seiner Zellen; lagen alle Zellen eines Blocks auf der
///     Nachbarseite, wurde die Länge des Rasters negativ (OverflowException) und die ganze Seite brach nach den Wörtern und Zeilen ab – keine
///     Tabellen, Zellen, Linien und Masse (Katalog: Seiten 17, 19, 27, 31, 45, 47, 49, 51).
/// (2) Ursache 2: Eine kurze Überschrift allein auf ihrer Zeile («Winkel 90°, egal, ISO/EN A1») galt als Fliesstext und trennte die Tabelle von
///     ihrer Nummer – die Tabelle war eine andere als in Leser 1. Werkstoffzeilen und Aufzählungen mit zwei Zellen trennen weiter (N4).
/// Testdateien selbst erzeugt (Nachbildung des Aufbaus, erfundene Werte); die echte Herstellerdatei liegt nur lokal (Echtdaten-Test).</summary>
public class UnterlagenPdfLeser3Tests
{
    static UnterlagenBefunde LesenPdf(string pfad) => new PdfLeser().Lesen(pfad, Path.GetFileName(pfad), "0123456789ab", "pdf");

    static string[][] Daten(UnterlagenBefund t) =>
        ((JsonArray)((JsonObject)t.Wert!)["daten"]!).Select(r => ((JsonArray)r!).Select(x => x!.GetValue<string>()).ToArray()).ToArray();

    static string[] Kopf(UnterlagenBefund t) => ((JsonArray)((JsonObject)t.Wert!)["kopf"]!).Select(x => x!.GetValue<string>()).ToArray();

    static IEnumerable<string> Unlesbar(UnterlagenBefunde b) => b.Hinweise.Where(h => h.Contains("konnte nicht gelesen werden", StringComparison.Ordinal));

    /// <summary>Seite aus einer Doppelseite wie im Katalog: A4 (595 × 842 pt), der Text der linken Nachbarseite steht noch im Inhalt, aber links
    /// ausserhalb der Seite (x negativ). Gezeichnet wird mit y von oben; <paramref name="zeichnen"/> bekommt Text- und Linienfunktion mit x in
    /// Koordinaten der Doppelseite (0–1190; sichtbar ist 595–1190).</summary>
    static string Doppelseite(Ordner o, string name, Action<Action<string, double, double, XFont>, Action<double, double, double, double>> zeichnen)
    {
        var doc = new PdfDocument();
        var s = doc.AddPage(); s.Width = XUnit.FromPoint(595); s.Height = XUnit.FromPoint(842);
        using (var g = XGraphics.FromPdfPage(s))
        {
            _ = new PdfZeichner(g, 297);   // richtet die Schriften ein
            var stift = new XPen(XColors.Black, 0.5);
            zeichnen((t, x, y, f) => g.DrawString(t, f, XBrushes.Black, x - 595, y), (x0, y0, x1, y1) => g.DrawLine(stift, x0 - 595, y0, x1 - 595, y1));
        }
        var pfad = o.Datei(name);
        doc.Save(pfad);
        return pfad;
    }

    static readonly XFont Titel = new("Arial", 9);
    static readonly XFont Klein = new("Arial", 8.3);

    // ---------- (1) Tabelle der Nachbarseite ausserhalb der Seite ----------

    /// <summary>Links (Nachbarseite, unsichtbar) eine Masstabelle mit vier Spalten oben auf der Seite, rechts (sichtbar) weiter unten eine eigene
    /// Tabelle mit Trennlinien. Erfundene Werte.</summary>
    static string NachbarseitePdf(Ordner o) => Doppelseite(o, "nachbarseite.pdf", (T, L) =>
    {
        double[] xl = [60, 160, 260, 360];
        string[] kl = ["Dim.", "Code", "a", "z"];
        for (int i = 0; i < 4; i++) T(kl[i], xl[i], 100, Titel);
        for (int r = 0; r < 6; r++)
        {
            string[] w = [$"{r + 1}", $"770 400 10{r}", $"{20 + 3 * r}", $"{10 + r}"];
            for (int i = 0; i < 4; i++) T(w[i], xl[i], 115 + 11.5 * r, Klein);
        }
        double[] xr = [640, 700, 800, 900];
        string[] kr = ["Typ", "Code", "a", "z"];
        for (int i = 0; i < 4; i++) T(kr[i], xr[i], 400, Titel);
        for (int r = 0; r < 4; r++)
        {
            string[] w = [$"T-{r + 1}", $"770 500 10{r}", $"{30 + 4 * r}", $"{12 + r}"];
            for (int i = 0; i < 4; i++) T(w[i], xr[i], 415 + 11.5 * r, Klein);
            L(635, 418 + 11.5 * r, 940, 418 + 11.5 * r);
        }
    });

    [Fact]
    public void Tabelle_der_Nachbarseite_ausserhalb_der_Seite_bricht_das_Lesen_der_Seite_nicht_ab()
    {
        using var o = new Ordner();
        var b = LesenPdf(NachbarseitePdf(o));
        Assert.Equal("pdf 3", b.Leser);
        // Voraussetzung: die Wörter der Nachbarseite liegen links der sichtbaren Seite (x negativ)
        Assert.True(Art(b, "wort").Count(w => w.Box![2] < -2) >= 20);
        Assert.Contains(Art(b, "zeile"), z => z.Text() == "Dim. | Code | a | z" && z.Box![2] < 0);
        Assert.Empty(Unlesbar(b));
        // die sichtbare Tabelle, ihre Zellen und die Linien sind da; aus dem unsichtbaren Text entsteht keine Tabelle
        var t = Art(b, "tabelle").Single();
        Assert.Equal(["Typ", "Code", "a", "z"], Kopf(t));
        Assert.Equal(["T-2", "770 500 101", "34", "13"], Daten(t)[1]);
        Assert.Equal(34, Zahl(Zelle(b, t.Id, 2, 3).Wert), 6);
        Assert.True(t.Box![0] >= 0, "Tabelle liegt auf der sichtbaren Seite");
        Assert.True(Art(b, "linie").Count() >= 4);
    }

    // ---------- (2) kurze Überschrift allein auf ihrer Zeile ----------

    /// <summary>Nachbau einer Katalogseite: oben links (Nachbarseite, unsichtbar) Nummer «60» und Titel «Doppelbogen» auf denselben Grundlinien wie
    /// rechts die Nummer «90» und die Überschrift «Winkel 90°, egal, ISO/EN A1»; darunter der Kopf «EN Dim. | Schwarz | Verzinkt | a | z» mit
    /// Einheitenzeile «(inch) | Code | Code (mm) (mm)» und Zeilen «• | Dim | Code schwarz | Code verzinkt | a | z». Erfundene Codes und Masse.
    /// Mit <paramref name="zweiteZelle"/> steht auf der Zeile der Überschrift rechts eine zweite sichtbare Zelle (Gegenprobe).</summary>
    static string UeberschriftPdf(Ordner o, bool zweiteZelle) => Doppelseite(o, zweiteZelle ? "ueberschrift_zwei.pdf" : "ueberschrift.pdf", (T, _) =>
    {
        T("60", 595 - 422.4, 842 - 780.78, Titel); T("Doppelbogen", 595 - 422.4, 842 - 770.78, Titel);
        T("90", 595 + 185.7, 842 - 780.78, Titel); T("Winkel 90°, egal, ISO/EN A1", 595 + 185.7, 842 - 770.78, Titel);
        if (zweiteZelle) T("Temperguss", 595 + 470, 842 - 770.78, Titel);
        T("EN", 595 + 191.7, 842 - 746.61, Titel); T("Dim.", 595 + 209.9, 842 - 746.61, Titel); T("Schwarz", 595 + 247.1, 842 - 746.61, Titel);
        T("Verzinkt", 595 + 303.0, 842 - 746.61, Titel); T("a", 595 + 354.4, 842 - 746.61, Titel); T("z", 595 + 377.4, 842 - 746.61, Titel);
        T("(inch)", 595 + 209.9, 842 - 737.11, Titel); T("Code", 595 + 261.9, 842 - 737.11, Titel); T("Code", 595 + 316.8, 842 - 737.11, Titel);
        T("(mm)", 595 + 344.0, 842 - 737.11, Titel); T("(mm)", 595 + 365.0, 842 - 737.11, Titel);
        string[] dim = ["3/8", "1/2", "3/4", "1", "5/4", "6/4", "2"];
        for (int r = 0; r < dim.Length; r++)
        {
            double y = 842 - (725.13 - 11.54 * r);
            T("•", 595 + 199.0, y, Klein); T(dim[r], 595 + 214.0, y, Klein);
            T($"770 901 10{r}", 595 + 236.3, y, Klein); T($"770 901 20{r}", 595 + 291.3, y, Klein);
            T($"{21 + 4 * r}", 595 + 350.2, y, Klein); T($"{11 + 3 * r}", 595 + 372.9, y, Klein);
        }
    });

    [Fact]
    public void Kurze_Ueberschrift_allein_auf_der_Zeile_trennt_die_Tabelle_nicht_von_ihrer_Nummer()
    {
        using var o = new Ordner();
        var b = LesenPdf(UeberschriftPdf(o, zweiteZelle: false));
        Assert.Empty(Unlesbar(b));
        // Voraussetzung: «60» und «Doppelbogen» der Nachbarseite stehen auf denselben Zeilen wie «90» und die Überschrift
        Assert.Contains(Art(b, "zeile"), z => z.Text() == "60 | 90");
        Assert.Contains(Art(b, "zeile"), z => z.Text() == "Doppelbogen | Winkel 90°, egal, ISO/EN A1");
        var t = Art(b, "tabelle").Single();
        var daten = Daten(t);
        // wie Leser 1: die Nummer gehört zur Tabelle, die Überschrift trennt nicht (Leser 2 begann die Tabelle erst beim Kopf «EN Dim.»)
        Assert.Contains(Kopf(t).Concat(daten.SelectMany(z => z)), c => c == "90" || c.StartsWith("90 ", StringComparison.Ordinal));
        Assert.True(t.Box![3] > 780, $"Box reicht bis zur Nummer: {string.Join(" ", t.Box)}");
        var r = Array.FindIndex(daten, z => z.Contains("770 901 102"));
        Assert.True(r >= 0);
        Assert.Contains("29", daten[r]);
        Assert.Contains("17", daten[r]);
    }

    [Fact]
    public void Gegenprobe_Ueberschrift_mit_zweiter_sichtbarer_Zelle_trennt_wie_in_Leser_2()
    {
        using var o = new Ordner();
        var b = LesenPdf(UeberschriftPdf(o, zweiteZelle: true));
        Assert.Contains(Art(b, "zeile"), z => z.Text() == "Doppelbogen | Winkel 90°, egal, ISO/EN A1 | Temperguss");
        var t = Art(b, "tabelle").Single();
        Assert.DoesNotContain(Daten(t).SelectMany(z => z), c => c is "90" || c.Contains("Winkel"));
        Assert.DoesNotContain(Kopf(t), k => k.Contains("Winkel"));
        Assert.Contains(Kopf(t), k => k.StartsWith("EN", StringComparison.Ordinal) || k.StartsWith("Dim.", StringComparison.Ordinal));
    }

    /// <summary>Datenblatt «Erstellt mit … Produktdaten» (Edelstahl, Arial 8 pt): «Technische Daten», darunter «Werkstoff | CrNiMo-Stahl 1.4401
    /// (DIN EN 10088)» – die Werkstoffzelle hat nur fünf Wörter (kurz), aber die Zeile zwei sichtbare Zellen –, dann die Tabelle
    /// «Art.-Nr. | DN | d, ø | arc | L | Z | VE1 | VE2 | VE3» mit Trennlinien (VE1 nur in den unteren Zeilen). Werte erfunden.</summary>
    static string EdelstahlPdf(Ordner o)
    {
        var doc = new PdfDocument();
        var s = doc.AddPage(); s.Width = XUnit.FromPoint(595); s.Height = XUnit.FromPoint(842);
        using (var g = XGraphics.FromPdfPage(s))
        {
            _ = new PdfZeichner(g, 297);
            var f = new XFont("Arial", 8);
            void T(string t, double x, double yUnten) => g.DrawString(t, f, XBrushes.Black, x, 842 - yUnten);
            T("Technische Daten", 301.6, 551.3);
            T("Werkstoff", 305.6, 535.8); T("CrNiMo-Stahl 1.4401 (DIN EN 10088)", 370.6, 535.8);
            double[] xs = [33.4, 71.7, 116.4, 193.1, 237.3, 309.6, 381.9, 430.4, 487.5];
            string[] kopf = ["Art.-Nr.", "DN", "d, ø", "arc", "L", "Z", "VE1", "VE2", "VE3"];
            for (int i = 0; i < kopf.Length; i++) T(kopf[i], xs[i], 511.3);
            var stift = new XPen(XColors.Gray, 0.5);
            for (int r = 0; r < 8; r++)
            {
                double y = 496.0 - 15.25 * r;
                string?[] w = [$"T{90601 + r}", $"{10 + 5 * r}", $"{12 + 5 * r} mm", r % 2 == 0 ? "45 °" : "90 °", $"{3 + r}.1 cm", $"{1 + r}.4 cm", r >= 6 ? "1 St." : null, "10 St.", "50 St."];
                for (int i = 0; i < w.Length; i++) if (w[i] is { } x) T(x, xs[i], y);
                g.DrawLine(stift, 28.4, 842 - (y - 4.2), 520, 842 - (y - 4.2));
            }
        }
        var pfad = o.Datei("datenblatt_edelstahl.pdf");
        doc.Save(pfad);
        return pfad;
    }

    [Fact]
    public void Werkstoffzeile_mit_zwei_Zellen_trennt_weiter_auch_mit_kurzer_Werkstoffzelle()
    {
        using var o = new Ordner();
        var b = LesenPdf(EdelstahlPdf(o));
        Assert.Contains(Art(b, "zeile"), z => z.Text() == "Werkstoff | CrNiMo-Stahl 1.4401 (DIN EN 10088)");
        var t = Art(b, "tabelle").Single(x => Daten(x).Any(r => r[0] == "T90601"));
        Assert.Equal(["Art.-Nr.", "DN", "d, ø", "arc", "L", "Z", "VE1", "VE2", "VE3"], Kopf(t));
        Assert.Equal(["T90603", "20", "22 mm", "45 °", "5.1 cm", "3.4 cm", "", "10 St.", "50 St."], Daten(t)[2]);
        Assert.DoesNotContain(Daten(t).SelectMany(r => r), c => c.Contains("Werkstoff") || c.Contains("CrNiMo"));
    }

    // ---------- echte Unterlage (nur lokal: %USERPROFILE%\HLKS_CAD_testdaten\unterlagen; fehlt sie, wird der Test übersprungen) ----------

    /// <summary>xUnit v2 überspringt einen Test, dessen Ausnahme-Meldung mit «$XunitDynamicSkip$» beginnt (Muster IfcEchtRauchTests).</summary>
    sealed class EchtDateiFehlt(string grund) : Exception("$XunitDynamicSkip$" + grund);

    const string Gewinderohr = "9da3b77ec732";

    static string EchtDatei(string muster)
    {
        var ordner = EchtOrdner("unterlagen");
        var f = ordner is null ? null : Directory.EnumerateFiles(ordner, muster).OrderBy(x => x, StringComparer.Ordinal).FirstOrDefault();
        return f ?? throw new EchtDateiFehlt($"Echte Datei fehlt auf diesem Rechner: %USERPROFILE%\\HLKS_CAD_testdaten\\unterlagen\\{muster}");
    }

    static UnterlagenBefunde? echt;
    static readonly object sperre = new();

    static UnterlagenBefunde Echt()
    {
        var pfad = EchtDatei(Gewinderohr + "_*.pdf");
        lock (sperre) return echt ??= new PdfLeser().Lesen(pfad, Path.GetFileName(pfad), Gewinderohr, "pdf");
    }

    [Fact]
    public void Echt_Gewinderohr_Katalog_alle_Seiten_gelesen_und_Tabellen_auf_den_Seiten_13_19_27()
    {
        var b = Echt();
        Assert.Empty(Unlesbar(b));
        Assert.Equal(2, Art(b, "tabelle").Count(t => t.Seite == 13));
        Assert.Equal(3, Art(b, "tabelle").Count(t => t.Seite == 19));
        Assert.Equal(1, Art(b, "tabelle").Count(t => t.Seite == 27));
        // die Seiten, die Leser 2 nach den Zeilen abbrach: Linien überall, Tabellen mit Zellen dort, wo es welche gibt
        foreach (var s in new[] { 17, 19, 27, 31, 45, 47, 49, 51 })
            Assert.True(Art(b, "linie").Any(l => l.Seite == s), $"Seite {s}: Linien");
        foreach (var s in new[] { 17, 19, 27, 47, 49 })
            Assert.True(Art(b, "tabelle").Any(x => x.Seite == s) && Art(b, "zelle").Any(z => z.Seite == s), $"Seite {s}: Tabelle und Zellen");
        // Seite 19, Fig. 120 (45°): Zeile «770 120 103» mit a 20 und z 10 als Zellen (Leser 2: nur Wörter)
        var t = Art(b, "tabelle").First(x => x.Seite == 19 && Daten(x).Any(r => r.Contains("770 120 103")));
        var daten = Daten(t);
        int zeile = Array.FindIndex(daten, r => r.Contains("770 120 103"));
        Assert.Equal(["20", "10"], daten[zeile][^2..]);
        Assert.Equal(20, Zahl(Zelle(b, t.Id, zeile + 1, daten[zeile].Length - 1).Wert), 6);
    }

    /// <summary>Die Quellen der Familien Gewinderohr Bogen, T-Stück und Reduktion (FAM-21/22/23 der Grundausstattung) stehen nach Leser 1 auf den
    /// Seiten 13, 19 und 27 – darunter vier Tabellen-Belege. Jede Tabelle und jede Zelle dieser Seiten nach Leser 1 findet in Leser 3 ihr Gegenstück
    /// (BefundZuordner: Seite, Art, Wert ohne Leerraum, Box ±0.5). Braucht zusätzlich die Ablage von Leser 1 (9da3b77ec732.pdf-1.json) im selben Ordner.</summary>
    [Fact]
    public void Echt_Gewinderohr_Katalog_Tabellen_und_Zellen_von_Leser_1_finden_ihr_Gegenstueck()
    {
        var alt = UnterlagenService.Laden(EchtDatei(Gewinderohr + ".pdf-1.json"))!;
        var b = Echt();
        var z = new BefundZuordner(alt.Befunde, b.Befunde, "pdf-1", "pdf-3");
        var belege = alt.Befunde.Where(x => x.Seite is 13 or 19 or 27 && x.Art is "tabelle" or "zelle").ToList();
        Assert.Equal(6, belege.Count(x => x.Art == "tabelle"));
        var zuordnung = belege.Select(x => z.Zuordnen(x.Id)).ToList();
        var ohne = zuordnung.Where(r => r.Neu is null).ToList();
        Assert.True(ohne.Count == 0, string.Join("\n", ohne.Take(10).Select(r => $"{r.Alt}: {r.Grund}")));
        // bis Seite 28 liest Leser 3 gleich viele Befunde wie Leser 1: die Ids dieser Seiten bleiben dieselben (Probe 21.10.2026: die Familien
        // gelten ohne Umschlüsselung nach pdf-3, familien_quellen_nachfuehren meldet sie «ohne Änderung»)
        Assert.All(zuordnung, r => Assert.Equal(r.Alt, r.Neu));
        // die vier Tabellen-Belege der Familien
        foreach (var id in new[] { "#12955", "#20692", "#20811", "#33760" })
            Assert.Equal(Gewinderohr + id, z.Zuordnen(Gewinderohr + id).Neu);
    }
}
