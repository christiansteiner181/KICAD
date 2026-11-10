// 2D-CAD «Zeichnung», Stufe 1: Ausgabe eines Layouts als Vektor-PDF (massstäblich) und als PNG (Ausschnitt, damit die KI sieht,
// was in der Zeichnung steht). Gezeichnet wird mit dem PdfZeichner des Kerns; das PNG entsteht aus dem PDF (PDFium).
//   Modellbereich   mit massstab 1:n (braucht die Einheit der Datei) auf ein Blatt, das die Zeichnung fasst, oder ohne massstab eingepasst
//   Papierbereich   1:1 auf dem Papierformat des Layouts (Einheit des Layouts = mm)

using System.Text.Json.Nodes;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Hlks.Kern;

public static class ZeichnungAusgabe
{
    public const string BildText = "Zeigt die Zeichnung oder einen Ausschnitt als Bild (PNG) – mit Farben, Linienstärken und Schraffuren der Datei. " +
        "Die Antwort nennt den gezeigten Bereich in Zeichnungseinheiten; damit lassen sich Bildpunkte in Koordinaten umrechnen. Masse nie aus dem Bild schätzen, sondern mit zeichnung_messen bzw. zeichnung_objekte lesen.";
    public const string PdfText = "Schreibt ein Layout der Zeichnung als Vektor-PDF. Modellbereich: massstäblich mit massstab (z. B. 50 für 1:50) oder eingepasst auf blatt (A4–A0); " +
        "Papierbereich (Layout): 1:1 auf seinem Papierformat mit den Ansichtsfenstern. Ohne datei nach %TEMP%\\HLKS_CAD\\<Name>.pdf. Antwort: datei, blattMm, massstab, groesseBytes.";

    public const int VorgabeBreite = 1400, MinBreite = 200, MaxBreite = 4000;
    const long MaxSeitenPixel = 36_000_000;
    const double Rand = 5;   // mm

    static readonly (string Name, double B, double H)[] Formate = [("A4", 210, 297), ("A3", 297, 420), ("A2", 420, 594), ("A1", 594, 841), ("A0", 841, 1189)];

    /// <summary>Papierformat eines Layouts in mm (Breite, Höhe; 0 = unbekannt).</summary>
    public static (double B, double H) Papier(ACadSharp.Objects.Layout l)
    {
        double f = l.PaperUnits == ACadSharp.Objects.PlotPaperUnits.Inches ? 25.4 : 1;
        double b = l.PaperWidth * f, h = l.PaperHeight * f;
        return l.PaperRotation is ACadSharp.Objects.PlotRotation.Degrees90 or ACadSharp.Objects.PlotRotation.Degrees270 ? (h, b) : (b, h);
    }

    static byte[] PdfBytes(ZeichnungBild bild, double papB, double papH, Abbildung abb)
    {
        using var doc = new PdfDocument();
        var page = doc.AddPage();
        page.Width = XUnit.FromMillimeter(papB); page.Height = XUnit.FromMillimeter(papH);
        using (var g = XGraphics.FromPdfPage(page))
        {
            g.DrawRectangle(XBrushes.White, 0, 0, page.Width.Point, page.Height.Point);
            var zeichner = new PdfZeichner(g, papH);
            foreach (var teil in bild.Teile)
            {
                if (teil.Primitive.Count == 0) continue;
                XGraphicsState? zustand = teil.Fenster is { } f ? zeichner.Zuschneiden(abb.X(f[0]), abb.Y(f[1]), abb.X(f[2]), abb.Y(f[3])) : null;
                zeichner.Zeichnen(teil.Primitive, abb);
                if (zustand is not null) zeichner.Wiederherstellen(zustand);
            }
        }
        using var ms = new MemoryStream();
        doc.Save(ms, false);
        return ms.ToArray();
    }

    static JsonObject Hinweise(Zeichnungsdatei z, ZeichnungBild bild, JsonObject o)
    {
        if (bild.NichtDargestellt.Count > 0)
            o["nichtDargestellt"] = new JsonObject(bild.NichtDargestellt.Select(n => KeyValuePair.Create(n.Key, (JsonNode?)n.Value)));
        o["hinweis"] = "Schriften als Arial, Linienstärken auf 0.18/0.20/0.35/0.50/0.70 mm gerundet, Linienarten als Strich, Strichpunkt oder Punkt.";
        return o;
    }

    // ---------- Bild ----------

    public static (JsonObject Info, byte[] Png) Bild(Zeichnungsdatei z, string? layout, double[]? ausschnitt, int? breite, bool farbig = true)
    {
        int b = Math.Clamp(breite ?? VorgabeBreite, MinBreite, MaxBreite);
        if (ausschnitt is not null && (ausschnitt.Length != 4 || ausschnitt.Any(v => double.IsNaN(v) || double.IsInfinity(v))))
            throw new HlksFehler("ausschnitt muss [x0, y0, x1, y1] sein.", "Vier Zahlen in Zeichnungseinheiten (Layout: mm des Blatts).");
        var lay = z.Layout(layout);
        // erster Durchgang nur für die Ausdehnung, wenn kein Ausschnitt gegeben ist (Schriftgrösse hängt vom Massstab ab)
        double x0, y0, x1, y1;
        if (ausschnitt is not null) { x0 = Math.Min(ausschnitt[0], ausschnitt[2]); x1 = Math.Max(ausschnitt[0], ausschnitt[2]); y0 = Math.Min(ausschnitt[1], ausschnitt[3]); y1 = Math.Max(ausschnitt[1], ausschnitt[3]); }
        else
        {
            var probe = ZeichnungGrafik.Erzeugen(z, lay, 1, farbig);
            var u = probe.Umriss ?? throw new HlksFehler($"Das Layout «{lay?.Name ?? "Model"}» von {z.Name} enthält nichts, das sich zeichnen lässt.", "Ein anderes Layout wählen (zeichnung_oeffnen nennt die Layouts) oder Layer einschalten.");
            (x0, y0, x1, y1) = (u[0], u[1], u[2], u[3]);
        }
        double cw = x1 - x0, ch = y1 - y0;
        if (cw <= 0 && ch <= 0) throw new HlksFehler("Der Ausschnitt hat keine Fläche.", "ausschnitt [x0, y0, x1, y1] mit x1 > x0 und y1 > y0 angeben.");
        if (cw <= 0) { x0 -= ch / 2; x1 += ch / 2; cw = ch; }
        if (ch <= 0) { y0 -= cw / 2; y1 += cw / 2; ch = cw; }
        // Papier höchstens 400 mm an der langen Seite: so bleiben Linienstärken im Bild lesbar
        double s = (400 - 2 * Rand) / Math.Max(cw, ch);
        double papB = cw * s + 2 * Rand, papH = ch * s + 2 * Rand;
        var bild = ZeichnungGrafik.Erzeugen(z, lay, s, farbig);
        var pdf = PdfBytes(bild, papB, papH, new Abbildung(s, Rand - x0 * s, Rand - y0 * s));
        double skala = Math.Min(b / (papB * 72 / 25.4), Math.Sqrt(MaxSeitenPixel / (papB * papH * Math.Pow(72 / 25.4, 2))));
        var (w, h, bgra) = PdfSeiteBild.Rendern(null, pdf, 1, skala);
        var temp = Path.Combine(Path.GetTempPath(), $"hlks_zeichnung_{Guid.NewGuid():N}.png");
        byte[] png;
        try { PngSchreiber.Schreiben(temp, w, h, bgra, skala * 72); png = File.ReadAllBytes(temp); }
        finally { try { File.Delete(temp); } catch (IOException) { } }
        double randE = Rand / s;
        var info = new JsonObject
        {
            ["zeichnung"] = z.Name, ["layout"] = lay?.Name ?? "Model",
            ["bereich"] = new JsonArray(Math.Round(x0 - randE, 3), Math.Round(y0 - randE, 3), Math.Round(x1 + randE, 3), Math.Round(y1 + randE, 3)),
            ["bildpunkte"] = new JsonArray(w, h),
            ["einheitenJeBildpunkt"] = Math.Round((cw + 2 * randE) / w, 5),
            ["gezeichnet"] = bild.Anzahl,
        };
        if (bild.Ausdehnung is { } a) info["ausdehnung"] = new JsonArray(a.Select(v => (JsonNode?)Math.Round(v, 3)).ToArray());
        return (Hinweise(z, bild, info), png);
    }

    // ---------- PDF ----------

    public static JsonObject Pdf(Zeichnungsdatei z, string? layout, double[]? fenster, double? massstab, string? blatt, string ziel, bool farbig = true)
    {
        if (fenster is not null && fenster.Length != 4) throw new HlksFehler("fenster muss [x0, y0, x1, y1] sein.", "Vier Zahlen in Zeichnungseinheiten.");
        if (massstab is <= 0) throw new HlksFehler("massstab muss grösser als 0 sein.", "Für 1:50 den Wert 50 angeben.");
        var lay = z.Layout(layout);
        double papB, papH, s;
        Abbildung abb;
        double? gewaehlt = null;
        if (lay is not null && fenster is null && massstab is null)
        {
            // Layout 1:1 auf seinem Papier; ohne Papierangabe das Gezeichnete mit Rand
            (papB, papH) = Papier(lay);
            s = 1;
            if (papB > 0 && papH > 0)
            {
                double f = lay.PaperUnits == ACadSharp.Objects.PlotPaperUnits.Inches ? 25.4 : 1;
                abb = new Abbildung(1, lay.UnprintableMargin.Left * f - lay.PlotOriginX * f, lay.UnprintableMargin.Bottom * f - lay.PlotOriginY * f);
            }
            else
            {
                var u = ZeichnungGrafik.Erzeugen(z, lay, 1, farbig).Ausdehnung ?? throw new HlksFehler($"Das Layout «{lay.Name}» ist leer.");
                papB = u[2] - u[0] + 2 * Rand; papH = u[3] - u[1] + 2 * Rand;
                abb = new Abbildung(1, Rand - u[0], Rand - u[1]);
            }
            gewaehlt = 1;
        }
        else
        {
            double[] u = fenster is not null
                ? [Math.Min(fenster[0], fenster[2]), Math.Min(fenster[1], fenster[3]), Math.Max(fenster[0], fenster[2]), Math.Max(fenster[1], fenster[3])]
                : ZeichnungGrafik.Erzeugen(z, lay, 1, farbig).Umriss ?? throw new HlksFehler($"Das Layout «{lay?.Name ?? "Model"}» ist leer.", "Ein anderes Layout wählen oder Layer einschalten.");
            double cw = u[2] - u[0], ch = u[3] - u[1];
            if (cw <= 0 || ch <= 0) throw new HlksFehler("Das Fenster hat keine Fläche.", "fenster [x0, y0, x1, y1] mit x1 > x0 und y1 > y0 angeben.");
            double mm = lay is null ? z.MmJeEinheit : 1;   // Layout-Einheit = mm
            (string Name, double B, double H)? format = null;
            if (!string.IsNullOrWhiteSpace(blatt))
                format = Formate.Cast<(string Name, double B, double H)?>().FirstOrDefault(f => f!.Value.Name.Equals(blatt.Trim(), StringComparison.OrdinalIgnoreCase))
                    ?? throw new HlksFehler($"Das Blattformat «{blatt}» gibt es nicht.", "A4, A3, A2, A1 oder A0 angeben.");
            if (massstab is double n)
            {
                if (lay is null && !z.EinheitBekannt)
                    throw new HlksFehler($"{z.Name} nennt keine Einheit; ein Massstab lässt sich nicht sicher rechnen.", "Ohne massstab ausgeben (eingepasst) oder die Einheit in der Datei setzen (INSUNITS).");
                s = mm / n;
                gewaehlt = n;
                double brauchtB = cw * s + 2 * Rand, brauchtH = ch * s + 2 * Rand;
                if (format is { } f) (papB, papH) = brauchtB >= brauchtH ? (f.H, f.B) : (f.B, f.H);
                else
                {
                    // kleinstes Normformat, das die Zeichnung fasst; sonst freies Format
                    var passend = Formate.Cast<(string Name, double B, double H)?>().FirstOrDefault(q => Math.Max(brauchtB, brauchtH) <= q!.Value.H && Math.Min(brauchtB, brauchtH) <= q.Value.B);
                    (papB, papH) = passend is { } q2 ? (brauchtB >= brauchtH ? (q2.H, q2.B) : (q2.B, q2.H)) : (brauchtB, brauchtH);
                }
                if (brauchtB > papB + 1e-6 || brauchtH > papH + 1e-6)
                    throw new HlksFehler($"Im Massstab 1:{n:0.##} braucht die Zeichnung {brauchtB:0} × {brauchtH:0} mm – mehr als {blatt}.", "Ein grösseres blatt, einen kleineren Massstab oder ein fenster wählen.");
            }
            else
            {
                var f = format ?? Formate[1];
                (papB, papH) = cw >= ch ? (f.H, f.B) : (f.B, f.H);
                s = Math.Min((papB - 2 * Rand) / cw, (papH - 2 * Rand) / ch);
                if (lay is not null || z.EinheitBekannt) gewaehlt = mm / s;
            }
            // Ausschnitt auf dem Blatt zentriert
            abb = new Abbildung(s, (papB - cw * s) / 2 - u[0] * s, (papH - ch * s) / 2 - u[1] * s);
        }
        var bild = ZeichnungGrafik.Erzeugen(z, lay, s, farbig);
        var bytes = PdfBytes(bild, papB, papH, abb);
        Directory.CreateDirectory(Path.GetDirectoryName(ziel)!);
        File.WriteAllBytes(ziel, bytes);
        var o = new JsonObject
        {
            ["zeichnung"] = z.Name, ["layout"] = lay?.Name ?? "Model", ["datei"] = ziel,
            ["blattMm"] = new JsonArray(Math.Round(papB, 1), Math.Round(papH, 1)), ["groesseBytes"] = bytes.Length, ["gezeichnet"] = bild.Anzahl,
        };
        if (gewaehlt is double g) o["massstab"] = "1:" + Math.Round(g, 2).ToString(System.Globalization.CultureInfo.InvariantCulture);
        else o["massstab"] = "eingepasst (Einheit der Datei unbekannt)";
        return Hinweise(z, bild, o);
    }
}
