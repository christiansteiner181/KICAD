// H+K-Plankopf (SIA 400:2000 Ziff. B.1.3, Titelfeld unten rechts). Kopie von AufgabenProtokolle.Web/Services/HkPlankopf.cs
// (Vorlage der Firma, dort mit PdfSharpCore), hier mit PDFsharp 6.5 und um zwei Dinge erweitert:
//   - Titelband «Planinhalt» über der Vorlage (die Vorlage hat keinen Platz für den Planinhalt, B.1.3 verlangt ihn)
//     mit Blattformat und Hinweis auf die Einheit der Masse (B.5.3)
//   - Logo aus der eingebetteten Ressource statt aus einem Assets-Ordner
// Gezeichnet wird in PDF-Punkten; alle Masse skalieren mit der Höhe (Referenzrahmen 180 × 62 mm: Vorlage 180 × 52 mm
// plus Titelband 10 mm [Annahme]).

using PdfSharp.Drawing;

namespace Hlks.Kern;

/// <summary>Inhalt des H+K-Plankopfs.</summary>
public sealed class HkPlankopfDaten
{
    public string Projektphase { get; set; } = "";
    public string ProjektZeile1 { get; set; } = "";
    public string ProjektZeile2 { get; set; } = "";
    public string Plannummer { get; set; } = "";
    /// <summary>Planinhalt (Titel des Planblatts) im Titelband.</summary>
    public string Planinhalt { get; set; } = "";
    public string Erstelldatum { get; set; } = "";   // leer = heute
    public string Kontrolliert { get; set; } = "";
    public string Massstab { get; set; } = "";       // leer = «1:xx»
    /// <summary>Format-Angabe im Titelband, z. B. «A3 quer».</summary>
    public string Format { get; set; } = "";
    /// <summary>Hinweis auf die Einheit der Masse (SIA 400 B.5.3), z. B. «Masse in m».</summary>
    public string MasseHinweis { get; set; } = "Masse in m";
    /// <summary>Planänderungen (Index / Datum / Beschreibung). Leer → eine Strichzeile wie auf der Vorlage.</summary>
    public List<(string Index, string Datum, string Text)> Aenderungen { get; set; } = [];
}

public static class HkPlankopf
{
    static readonly XColor Ink = XColor.FromArgb(0x1a, 0x1a, 0x1a);

    /// <summary>Referenzmasse in Vorlagen-mm: Breite, Höhe der Vorlage und Höhe des Titelbands (Annahme).</summary>
    const double VorlageBreite = 180, VorlageHoehe = 52, BandHoehe = 10;

    /// <summary>Seitenverhältnis des ganzen Plankopfs (Breite zu Höhe, Titelband eingerechnet).</summary>
    public const double Verhaeltnis = VorlageBreite / (VorlageHoehe + BandHoehe);

    const string Schrift = "Calibri";

    /// <summary>Zeichnet den Plankopf in das Rechteck (x, y, w, h) in Punkten; h und w im Verhältnis <see cref="Verhaeltnis"/>.</summary>
    public static void Zeichnen(XGraphics gfx, double x, double y, double w, double h, HkPlankopfDaten d, XImage? logo)
    {
        double u = h / (VorlageHoehe + BandHoehe);      // 1 Vorlagen-Millimeter in Punkten
        double band = BandHoehe * u;
        double y0 = y + band;                           // Oberkante der Vorlage
        var stark = new XPen(Ink, 0.4 * u);
        var fein = new XPen(Ink, 0.22 * u);
        var ink = new XSolidBrush(Ink);

        gfx.DrawRectangle(new XSolidBrush(XColors.White), x, y, w, h);
        gfx.DrawRectangle(stark, x, y, w, h);
        gfx.DrawLine(stark, x, y0, x + w, y0);

        double xm = x + w * 0.50;                       // Mittelteiler Text | Logo/Planänderungen
        gfx.DrawLine(stark, xm, y0, xm, y + h);

        XFont F(double mm, bool fett = false) => new(Schrift, Math.Max(1.0, mm * u), fett ? XFontStyleEx.Bold : XFontStyleEx.Regular);

        // Text an der Grundlinie; schrumpft bis 65 %, wenn die Zelle zu schmal ist, und wird danach gekürzt.
        void Txt(string? t, double mm, double tx, double tyBase, double maxW, bool fett = false, XStringAlignment al = XStringAlignment.Near)
        {
            if (string.IsNullOrWhiteSpace(t)) return;
            var f = F(mm, fett);
            for (int i = 0; i < 6 && gfx.MeasureString(t, f).Width > maxW; i++) f = F(mm *= 0.93, fett);
            while (t.Length > 1 && gfx.MeasureString(t, f).Width > maxW) t = t[..^2] + "…";
            double b = gfx.MeasureString(t, f).Width;
            double bx = al == XStringAlignment.Far ? tx - b : al == XStringAlignment.Center ? tx - b / 2 : tx;
            gfx.DrawString(t, f, ink, new XPoint(bx, tyBase), XStringFormats.BaseLineLeft);
        }

        // ---------------- Titelband ----------------
        double lx = x + 2.4 * u;
        Txt("Planinhalt", 2.4, lx, y + 3.4 * u, xm - lx - 2 * u);
        Txt(d.Planinhalt, 3.8, lx, y + 8.2 * u, xm - lx - 2 * u, true);
        Txt(string.IsNullOrWhiteSpace(d.Format) ? null : "Format " + d.Format, 2.5, xm + 2.4 * u, y + 4.0 * u, x + w - xm - 4 * u);
        Txt(d.MasseHinweis, 2.5, xm + 2.4 * u, y + 8.0 * u, x + w - xm - 4 * u);

        // ---------------- linke Hälfte ----------------
        double lw = xm - x - 4.4 * u;

        // Firma mit Adresse in zwei Spalten, darunter die Trennlinie des Kopfbandes.
        double fb = 14 * u;
        Txt("H+K Planungs AG, Bolligen", 3.1, lx, y0 + 4.9 * u, lw, true);
        double sp2 = x + w * 0.135;
        Txt("Dorfmärit 9", 2.5, lx, y0 + 9.2 * u, sp2 - lx - u);
        Txt("3065 Bolligen", 2.5, lx, y0 + 12.6 * u, sp2 - lx - u);
        Txt("Tel: 031 / 924 25 35", 2.5, sp2, y0 + 9.2 * u, xm - sp2 - u);
        Txt("www.hkplanung.ch", 2.5, sp2, y0 + 12.6 * u, xm - sp2 - u);
        gfx.DrawLine(stark, x, y0 + fb, xm, y0 + fb);

        Txt("Projektphase", 2.4, lx, y0 + 19.6 * u, lw);
        Txt(d.Projektphase, 3.1, lx, y0 + 23.6 * u, lw);

        Txt("Projekt:", 2.4, lx, y0 + 29.2 * u, lw);
        Txt(d.ProjektZeile1, 3.1, lx, y0 + 33.4 * u, lw);
        Txt(d.ProjektZeile2, 3.1, lx, y0 + 37.4 * u, lw);

        Txt("Plannummer", 2.4, lx, y0 + 43.4 * u, x + w * 0.26 - lx);
        Txt(d.Plannummer, 3.1, lx, y0 + 48.2 * u, x + w * 0.26 - lx);

        // Kennwerte rechtsbündig auf einer gemeinsamen Kante – wie auf der Vorlage.
        double kx1 = x + w * 0.285, kx2 = xm - 2.4 * u;
        var datum = string.IsNullOrWhiteSpace(d.Erstelldatum) ? DateTime.Now.ToString("dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture) : d.Erstelldatum;
        Txt("Erstelldatum:", 2.5, kx1, y0 + 43.2 * u, w * 0.12);
        Txt(datum, 2.5, kx2, y0 + 43.2 * u, w * 0.10, false, XStringAlignment.Far);
        Txt("Kontrolliert:", 2.5, kx1, y0 + 46.4 * u, w * 0.12);
        Txt(d.Kontrolliert, 2.5, kx2, y0 + 46.4 * u, w * 0.10, false, XStringAlignment.Far);
        Txt("Massstab:", 2.5, kx1, y0 + 49.6 * u, w * 0.12);
        Txt(string.IsNullOrWhiteSpace(d.Massstab) ? "1:xx" : d.Massstab, 2.5, kx2, y0 + 49.6 * u, w * 0.10, false, XStringAlignment.Far);

        // ---------------- rechte Hälfte ----------------
        // Logo-Kasten oben; das Bild behält sein Seitenverhältnis und sitzt mittig im Band.
        gfx.DrawRectangle(stark, xm, y0, x + w - xm, fb);
        if (logo != null)
        {
            // Wie auf der Vorlage füllt das Logo den Kasten: erst die Breite ausreizen, die Höhe bleibt unter dem Band.
            double lwd = x + w - xm - 3.2 * u, lh = lwd * logo.PixelHeight / logo.PixelWidth;
            if (lh > fb - 1.8 * u) { lh = fb - 1.8 * u; lwd = lh * logo.PixelWidth / logo.PixelHeight; }
            gfx.DrawImage(logo, xm + 1.6 * u, y0 + (fb - lh) / 2, lwd, lh);
        }
        else Txt("H+K PLANUNGS AG", 4.0, xm + 2.4 * u, y0 + 9 * u, x + w - xm - 4 * u, true);

        Txt("Planänderungen", 3.1, xm + 2.4 * u, y0 + 19.6 * u, x + w - xm - 4 * u, true);

        // Tabelle Index | Datum | Beschreibung. Ohne Einträge eine Strichzeile – wie die Vorlage.
        double t0 = xm + 2 * u, t1 = x + w - 1.2 * u;
        double c1 = t0 + 11 * u, c2 = c1 + 17 * u;
        double yT = y0 + 21.4 * u, zH = 3.6 * u;
        var zeilen = d.Aenderungen.Count > 0 ? d.Aenderungen : [("-", "-", "-")];
        int anz = Math.Min(zeilen.Count, (int)((y + h - yT) / zH) - 1);
        double yEnde = yT + (anz + 1) * zH;

        gfx.DrawLine(fein, t0, yT, t1, yT);
        gfx.DrawLine(fein, t0, yT + zH, t1, yT + zH);
        Txt("Index", 2.4, (t0 + c1) / 2, yT + zH - 1.1 * u, 10 * u, false, XStringAlignment.Center);
        Txt("Datum", 2.4, (c1 + c2) / 2, yT + zH - 1.1 * u, 16 * u, false, XStringAlignment.Center);
        Txt("Beschreibung", 2.4, c2 + 1.2 * u, yT + zH - 1.1 * u, t1 - c2 - 2 * u);
        for (int i = 0; i < anz; i++)
        {
            var (idx, dat, txt) = zeilen[i];
            double yb = yT + (i + 2) * zH - 1.1 * u;
            Txt(string.IsNullOrWhiteSpace(idx) ? "-" : idx, 2.4, (t0 + c1) / 2, yb, 10 * u, false, XStringAlignment.Center);
            Txt(string.IsNullOrWhiteSpace(dat) ? "-" : dat, 2.4, (c1 + c2) / 2, yb, 16 * u, false, XStringAlignment.Center);
            Txt(string.IsNullOrWhiteSpace(txt) ? "-" : txt, 2.4, c2 + 1.2 * u, yb, t1 - c2 - 2 * u);
            gfx.DrawLine(fein, t0, yT + (i + 2) * zH, t1, yT + (i + 2) * zH);
        }
        foreach (var vx in new[] { t0, c1, c2, t1 }) gfx.DrawLine(fein, vx, yT, vx, yEnde);
    }

    /// <summary>Logo aus der eingebetteten Ressource; der Strom muss bis zum Speichern des Dokuments offen bleiben
    /// (Aufrufer gibt ihn danach frei). Null, wenn die Ressource fehlt oder nicht lesbar ist: der Kopf zeigt dann den Schriftzug.</summary>
    public static (XImage? Bild, Stream? Strom) LadeLogo()
    {
        try
        {
            var s = typeof(HkPlankopf).Assembly.GetManifestResourceStream("Hlks.Kern.Plan.hk-logo.png");
            if (s is null) return (null, null);
            var ms = new MemoryStream();
            s.CopyTo(ms); s.Dispose();
            ms.Position = 0;
            return (XImage.FromStream(ms), ms);
        }
        catch { return (null, null); }
    }
}
