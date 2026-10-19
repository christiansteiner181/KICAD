// Bild einer Unterlage (Modul 2, Auftrag B2): Abfrage unterlage_bild. Die KI sieht sich Datenblattseiten und Zeichnungen als PNG an,
// auch nur einen Ausschnitt in Unterlagen-Koordinaten (PDF: pt, y nach oben; Bild: Bildpunkte, y nach unten; DWG/DXF: Zeichnungs-
// einheiten, y nach oben) – so lassen sich Tabellen in Rasterbildern und kleine Beschriftungen gross lesen.
//   PDF       Seite mit Docnet/PDFium gerendert (wie Modul 1b), auf den Ausschnitt zugeschnitten
//   Bild      PNG (8 Bit, nicht verschachtelt) wird zugeschnitten und verkleinert; JPEG und andere PNG-Formen kommen unverändert
//   DWG/DXF   mit dem PDF-Zeichner des Kerns (PDFsharp) gezeichnet, schwarz auf weiss, dann gerendert
//   Rest      kein Bild: klarer Hinweis (STEP, STL, Tabellen … haben keine Seite; ihre Befunde sind die Auskunft)
// Die Antwort nennt, welchen Bereich der Unterlage das Bild zeigt und wie viele Bildpunkte es hat: damit rechnet die KI
// Bildpunkte in Unterlagen-Koordinaten um.

using System.IO.Compression;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Hlks.Kern;

public sealed record AnsichtErgebnis(byte[] Bild, string Mime, int Breite, int Hoehe, int Seite, int Seiten, double[] Bereich, List<string> Hinweise, string Art);

public static class UnterlagenAnsicht
{
    public const int VorgabeBreite = 1000, MinBreite = 100, MaxBreite = 3000;
    /// <summary>Obergrenze der Bildpunkte der ganzen gerenderten Seite (Speicher: 4 Byte je Punkt).</summary>
    const long MaxSeitenPixel = 36_000_000;

    public static AnsichtErgebnis Erzeugen(Projekt p, UnterlageRef u, int? seite, double[]? ausschnitt, int? breite)
    {
        int b = Math.Clamp(breite ?? VorgabeBreite, MinBreite, MaxBreite);
        if (breite is not null && breite != b) { /* Hinweis unten */ }
        var hinweise = new List<string>();
        if (breite is not null && (breite < MinBreite || breite > MaxBreite)) hinweise.Add($"breite {breite} liegt ausserhalb {MinBreite}–{MaxBreite}: es gilt {b}.");
        if (ausschnitt is not null && (ausschnitt.Length != 4 || ausschnitt.Any(double.IsNaN)))
            throw new HlksFehler("ausschnitt muss [x0, y0, x1, y1] sein.", "Vier Zahlen in Unterlagen-Koordinaten, z. B. [40, 120, 330, 240] (PDF: pt, y nach oben).");
        return u.Art switch
        {
            "pdf" => Pdf(u, seite ?? 1, ausschnitt, b, hinweise),
            "bild" => Bild(u, ausschnitt, b, hinweise),
            "dwg" or "dxf" => Cad(p, u, ausschnitt, b, hinweise),
            _ => throw new HlksFehler($"Zu «{u.Anzeige}» ({u.Art}) gibt es kein Bild.",
                u.Art is "step" or "iges" or "stl" or "obj" or "ifc" ? "3D-Modelle haben keine Seite: Hülle, Flächen und Anschlüsse stehen in den Befunden (befunde mit art form3d, port, stutzen)."
                : u.Art == "html" ? "Eine Webseite hat kein Seitenbild: ihr Inhalt steht in den Befunden (befunde mit art text, tabelle, zelle, link); weiterführende Seiten und Dateien über ihre Links (art link) mit unterlage_aus_url abrufen."
                : "Tabellen haben kein Bild: ihr Inhalt steht in den Befunden (befunde mit art tabelle)."),
        };
    }

    // ---------- PDF ----------

    static AnsichtErgebnis Pdf(UnterlageRef u, int seite, double[]? ausschnitt, int breite, List<string> hinweise)
    {
        var (seiten, bw, bh) = PdfSeite.Mass(u.Pfad, seite);
        if (seiten < 1) throw new HlksFehler($"Das PDF «{u.Anzeige}» hat keine Seiten.", "Eine andere Datei wählen.");
        if (seite < 1 || seite > seiten) throw new HlksFehler($"Das PDF hat {seiten} Seite(n), Seite {seite} gibt es nicht.", $"seite zwischen 1 und {seiten} wählen.");
        var (x0, y0, x1, y1) = Bereich(ausschnitt, 0, 0, bw, bh, "PDF", "pt", hinweise);
        double skala = breite / (x1 - x0);
        double maxSkala = Math.Sqrt(MaxSeitenPixel / (bw * bh));
        if (skala > maxSkala)
        {
            hinweise.Add($"Die höchste Auflösung dieser Seite ist {maxSkala:0.0} Bildpunkte je pt (PDFium-Grenze); der Ausschnitt ist deshalb nur {Math.Floor((x1 - x0) * maxSkala)} Bildpunkte breit.");
            skala = maxSkala;
        }
        var (w, h, bgra) = PdfSeiteBild.Rendern(u.Pfad, null, seite, skala);
        // Ausschnitt in Bildpunkten (Zeile 0 = oben)
        int px0 = Math.Clamp((int)Math.Floor(x0 * skala), 0, w - 1), px1 = Math.Clamp((int)Math.Ceiling(x1 * skala), px0 + 1, w);
        int py0 = Math.Clamp((int)Math.Floor((bh - y1) * skala), 0, h - 1), py1 = Math.Clamp((int)Math.Ceiling((bh - y0) * skala), py0 + 1, h);
        var teil = Zuschnitt(bgra, w, px0, py0, px1, py1);
        var png = Png(teil, px1 - px0, py1 - py0, skala * 72);
        // Bereich, den das Bild wirklich zeigt (durch das Runden auf Bildpunkte etwas anders als angefragt)
        double[] bereich = [px0 / skala, bh - py1 / skala, px1 / skala, bh - py0 / skala];
        return new AnsichtErgebnis(png, "image/png", px1 - px0, py1 - py0, seite, seiten, bereich.Select(v => Math.Round(v, 2)).ToArray(), hinweise, "pdf");
    }

    // ---------- Bild ----------

    static AnsichtErgebnis Bild(UnterlageRef u, double[]? ausschnitt, int breite, List<string> hinweise)
    {
        var info = BildDatei.Lesen(u.Pfad);
        var bytes = File.ReadAllBytes(u.Pfad);
        var mime = info.Format == "jpg" ? "image/jpeg" : "image/png";
        double[] voll = [0, 0, info.Breite, info.Hoehe];
        if (info.Format == "png" && PngDekodieren(bytes, out int w, out int h, out var bgra))
        {
            var (x0, y0, x1, y1) = Bereich(ausschnitt, 0, 0, w, h, "Bild", "Bildpunkte, y nach unten", hinweise);
            int px0 = (int)Math.Floor(x0), py0 = (int)Math.Floor(y0), px1 = Math.Min(w, (int)Math.Ceiling(x1)), py1 = Math.Min(h, (int)Math.Ceiling(y1));
            int cw = px1 - px0, ch = py1 - py0;
            if (ausschnitt is null && breite >= w) return new AnsichtErgebnis(bytes, mime, w, h, 1, 1, voll, hinweise, "bild");   // unverändert
            int tw = Math.Min(breite, cw), th = Math.Max(1, (int)Math.Round((double)ch * tw / cw));
            var klein = Verkleinern(bgra, w, px0, py0, cw, ch, tw, th);
            return new AnsichtErgebnis(Png(klein, tw, th, 72), "image/png", tw, th, 1, 1, [px0, py0, px1, py1], hinweise, "bild");
        }
        if (ausschnitt is not null)
            hinweise.Add("Ausschnitt und breite gelten nicht für dieses Bildformat (JPEG oder PNG mit 16 Bit/Verschachtelung): das ganze Bild kommt unverändert.");
        else if (breite < info.Breite)
            hinweise.Add("Das Bild wird in diesem Format nicht verkleinert: es kommt in Originalgrösse.");
        return new AnsichtErgebnis(bytes, mime, info.Breite, info.Hoehe, 1, 1, voll, hinweise, "bild");
    }

    // ---------- DWG/DXF ----------

    static AnsichtErgebnis Cad(Projekt p, UnterlageRef u, double[]? ausschnitt, int breite, List<string> hinweise)
    {
        var z = CadLeser.Lesen(u.Pfad);
        var voll = z.Umriss ?? z.Ausdehnung ?? throw new HlksFehler($"«{u.Anzeige}» enthält im Modellbereich nichts, das sich zeichnen lässt.",
            "Layouts (Papierbereiche) werden nicht gezeichnet; Befunde: befunde mit art linie, text, mass.");
        var (x0, y0, x1, y1) = Bereich(ausschnitt, voll[0], voll[1], voll[2], voll[3], "DWG/DXF", "Zeichnungseinheiten, y nach oben", hinweise);
        double cw = x1 - x0, ch = y1 - y0;
        // Papier: höchstens 280 × 200 mm (quer) bzw. 200 × 280 (hoch), 3 mm Rand
        const double Rand = 3;
        double maxB = cw >= ch ? 280 : 200, maxH = cw >= ch ? 200 : 280;
        double s = Math.Min((maxB - 2 * Rand) / cw, (maxH - 2 * Rand) / ch);   // Papier-mm je Zeichnungseinheit
        double papB = cw * s + 2 * Rand, papH = ch * s + 2 * Rand;
        var dummy = new Element { Id = "bild", Kennzeichen = "BILD", Klasse = "Unterlage", Name = "bild" };
        var primitive = GrundlagenCad.Primitive(z, UnterlagenLage.Identitaet, 1 / s, dummy);
        foreach (var pr in primitive) pr.Stil = new Stil { Stift = pr.Art == "text" ? Stifte.Duenn : Stifte.Duenn, Farbe = "#000000" };
        byte[] pdfBytes;
        using (var doc = new PdfDocument())
        {
            var page = doc.AddPage();
            page.Width = XUnit.FromMillimeter(papB); page.Height = XUnit.FromMillimeter(papH);
            using (var g = XGraphics.FromPdfPage(page))
            {
                g.DrawRectangle(XBrushes.White, 0, 0, page.Width.Point, page.Height.Point);
                var zeichner = new PdfZeichner(g, papH);
                zeichner.Zeichnen(primitive, new Abbildung(s, Rand - x0 * s, Rand - y0 * s));
            }
            using var ms = new MemoryStream();
            doc.Save(ms, false);
            pdfBytes = ms.ToArray();
        }
        double skala = breite / (papB * 72 / 25.4);
        double maxSkala = Math.Sqrt(MaxSeitenPixel / (papB * papH * Math.Pow(72 / 25.4, 2)));
        skala = Math.Min(skala, maxSkala);
        var (w, h, bgra) = PdfSeiteBild.Rendern(null, pdfBytes, 1, skala);
        hinweise.Add($"Gezeichnet aus {z.Primitive.Count} Elementen (Modellbereich, Blöcke und Bemassungen aufgelöst), schwarz auf weiss, ohne Farben und Linienstärken. Einheit der Datei: {(z.EinheitBekannt ? z.Einheit : "unbekannt")}.");
        if (z.Gekappt > 0) hinweise.Add($"{z.Gekappt} Elemente über der Obergrenze des Lesers fehlen.");
        // Bereich: die Zeichnung füllt das Bild bis auf den Rand von 3 mm
        double randE = Rand / s;
        return new AnsichtErgebnis(Png(bgra, w, h, skala * 72), "image/png", w, h, 1, 1, new[] { x0 - randE, y0 - randE, x1 + randE, y1 + randE }.Select(v => Math.Round(v, 3)).ToArray(), hinweise, u.Art);
    }

    // ---------- Hilfen ----------

    /// <summary>Ausschnitt prüfen und auf die Seite bzw. Zeichnung zuschneiden.</summary>
    static (double X0, double Y0, double X1, double Y1) Bereich(double[]? a, double bx0, double by0, double bx1, double by1, string art, string einheit, List<string> hinweise)
    {
        if (a is null) return (bx0, by0, bx1, by1);
        double x0 = Math.Min(a[0], a[2]), x1 = Math.Max(a[0], a[2]), y0 = Math.Min(a[1], a[3]), y1 = Math.Max(a[1], a[3]);
        double cx0 = Math.Max(x0, bx0), cy0 = Math.Max(y0, by0), cx1 = Math.Min(x1, bx1), cy1 = Math.Min(y1, by1);
        if (cx1 - cx0 < 1e-6 || cy1 - cy0 < 1e-6)
            throw new HlksFehler($"Der Ausschnitt liegt ausserhalb der Unterlage ({art}: x {bx0:0.##}–{bx1:0.##}, y {by0:0.##}–{by1:0.##}, {einheit}).",
                "ausschnitt [x0, y0, x1, y1] in den Koordinaten der Befunde angeben (Box eines Befunds).");
        if (cx0 != x0 || cy0 != y0 || cx1 != x1 || cy1 != y1) hinweise.Add("Der Ausschnitt wurde auf die Unterlage zugeschnitten.");
        return (cx0, cy0, cx1, cy1);
    }

    static byte[] Zuschnitt(byte[] bgra, int breite, int x0, int y0, int x1, int y1)
    {
        int w = x1 - x0, h = y1 - y0;
        var aus = new byte[(long)w * h * 4];
        for (int y = 0; y < h; y++) Buffer.BlockCopy(bgra, ((y0 + y) * breite + x0) * 4, aus, y * w * 4, w * 4);
        return aus;
    }

    /// <summary>Mittelt Bildpunkte (Flächenmittel) beim Verkleinern eines Ausschnitts.</summary>
    static byte[] Verkleinern(byte[] bgra, int breite, int x0, int y0, int cw, int ch, int tw, int th)
    {
        var aus = new byte[(long)tw * th * 4];
        for (int ty = 0; ty < th; ty++)
        {
            int sy0 = y0 + ty * ch / th, sy1 = Math.Max(sy0 + 1, y0 + (ty + 1) * ch / th);
            for (int tx = 0; tx < tw; tx++)
            {
                int sx0 = x0 + tx * cw / tw, sx1 = Math.Max(sx0 + 1, x0 + (tx + 1) * cw / tw);
                long b = 0, g = 0, r = 0; int n = 0;
                for (int y = sy0; y < sy1; y++)
                    for (int x = sx0; x < sx1; x++) { int i = (y * breite + x) * 4; b += bgra[i]; g += bgra[i + 1]; r += bgra[i + 2]; n++; }
                int o = (ty * tw + tx) * 4;
                aus[o] = (byte)(b / n); aus[o + 1] = (byte)(g / n); aus[o + 2] = (byte)(r / n); aus[o + 3] = 255;
            }
        }
        return aus;
    }

    static byte[] Png(byte[] bgra, int breite, int hoehe, double dpi)
    {
        var temp = Path.Combine(Path.GetTempPath(), $"hlks_ansicht_{Guid.NewGuid():N}.png");
        try
        {
            PngSchreiber.Schreiben(temp, breite, hoehe, bgra, dpi);
            return File.ReadAllBytes(temp);
        }
        finally { try { File.Delete(temp); } catch (IOException) { } }
    }

    /// <summary>PNG lesen: 8 Bit (16 Bit auf 8 gekürzt), Grau, RGB, Palette, Grau+Alpha, RGBA; nicht verschachtelt. Ergebnis BGRA auf Weiss.</summary>
    static bool PngDekodieren(byte[] d, out int breite, out int hoehe, out byte[] bgra)
    {
        breite = hoehe = 0; bgra = [];
        try
        {
            if (d.Length < 33 || d[0] != 0x89 || d[1] != 'P') return false;
            int pos = 8, tiefe = 0, typ = 0, interlace = 0;
            var idat = new MemoryStream();
            byte[]? palette = null;
            while (pos + 8 <= d.Length)
            {
                int len = (d[pos] << 24) | (d[pos + 1] << 16) | (d[pos + 2] << 8) | d[pos + 3];
                var name = System.Text.Encoding.ASCII.GetString(d, pos + 4, 4);
                if (name == "IHDR")
                {
                    breite = (d[pos + 8] << 24) | (d[pos + 9] << 16) | (d[pos + 10] << 8) | d[pos + 11];
                    hoehe = (d[pos + 12] << 24) | (d[pos + 13] << 16) | (d[pos + 14] << 8) | d[pos + 15];
                    tiefe = d[pos + 16]; typ = d[pos + 17]; interlace = d[pos + 20];
                }
                else if (name == "PLTE") palette = d.AsSpan(pos + 8, len).ToArray();
                else if (name == "IDAT") idat.Write(d, pos + 8, len);
                else if (name == "IEND") break;
                pos += 12 + len;
            }
            if (interlace != 0 || tiefe is not (8 or 16) || breite <= 0 || hoehe <= 0 || (long)breite * hoehe > 120_000_000) return false;
            int kanaele = typ switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => 0 };
            if (kanaele == 0 || (typ == 3 && (palette is null || tiefe != 8))) return false;
            int bpp = kanaele * (tiefe / 8), zeile = breite * bpp;
            idat.Position = 0;
            using var z = new ZLibStream(idat, CompressionMode.Decompress);
            var roh = new byte[(long)(zeile + 1) * hoehe];
            z.ReadExactly(roh);
            var bild = new byte[(long)zeile * hoehe];
            for (int y = 0; y < hoehe; y++)
            {
                int f = roh[y * (zeile + 1)], o = y * zeile, v = (y - 1) * zeile;
                for (int i = 0; i < zeile; i++)
                {
                    int x = roh[y * (zeile + 1) + 1 + i];
                    int a = i >= bpp ? bild[o + i - bpp] : 0, b = y > 0 ? bild[v + i] : 0, c = y > 0 && i >= bpp ? bild[v + i - bpp] : 0;
                    int r = f switch
                    {
                        0 => x, 1 => x + a, 2 => x + b, 3 => x + ((a + b) >> 1),
                        4 => x + Paeth(a, b, c),
                        _ => throw new InvalidDataException("PNG-Filter"),
                    };
                    bild[o + i] = (byte)r;
                }
            }
            bgra = new byte[(long)breite * hoehe * 4];
            int schritt = tiefe / 8;   // 16 Bit: höherwertiges Byte
            for (int y = 0; y < hoehe; y++)
                for (int x = 0; x < breite; x++)
                {
                    int i = y * zeile + x * bpp, o = (y * breite + x) * 4;
                    int rr, gg, bb, aa = 255;
                    switch (typ)
                    {
                        case 0: rr = gg = bb = bild[i]; break;
                        case 2: rr = bild[i]; gg = bild[i + schritt]; bb = bild[i + 2 * schritt]; break;
                        case 3: { int pi = bild[i] * 3; rr = pi + 2 < palette!.Length ? palette[pi] : 0; gg = pi + 2 < palette.Length ? palette[pi + 1] : 0; bb = pi + 2 < palette.Length ? palette[pi + 2] : 0; break; }
                        case 4: rr = gg = bb = bild[i]; aa = bild[i + schritt]; break;
                        default: rr = bild[i]; gg = bild[i + schritt]; bb = bild[i + 2 * schritt]; aa = bild[i + 3 * schritt]; break;
                    }
                    bgra[o] = (byte)((bb * aa + 255 * (255 - aa)) / 255);
                    bgra[o + 1] = (byte)((gg * aa + 255 * (255 - aa)) / 255);
                    bgra[o + 2] = (byte)((rr * aa + 255 * (255 - aa)) / 255);
                    bgra[o + 3] = 255;
                }
            return true;
        }
        catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or IndexOutOfRangeException or ArgumentException) { return false; }
    }

    static int Paeth(int a, int b, int c)
    {
        int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }
}
