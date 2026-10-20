// H+K-Plankopf (SIA 400:2000 Ziff. B.1.3, Titelfeld unten rechts) nach den echten H+K-Plänen (Revisionspläne Bernstrasse 21.01.2026,
// Ausschnitt H03 und Strangschema Wasser; Beschreibung in entwurf/planmuster.md und entwurf/modul-7.md):
//   Grundriss-Kopf  Absenderblock mit BEIDEN Standorten (aus kataloge/buero.json › standorte) und Logo, darunter «Projektphase:», «Projekt:»
//                   (Objekt und Plantitel, zwei Zeilen), «Plannummer:»; rechts «Erstelldatum:», «Rev. Index:» (Buchstabe), «Rev. Datum:»,
//                   «Kontrolliert:», «Massstab:» («1 : 50»); darunter die Tabelle «Planänderungen» (Index | Datum | Beschreibung) mit beliebig
//                   vielen Zeilen – der Kopf wächst nach oben (Höhe = Funktion der Zeilenzahl, <see cref="Hoehe"/>).
//   Schema-Kopf     schmal, unten links, nur der erste Standort (Bolligen) mit Telefon und Web, kein Logo, kein Massstab, keine Rev.-Felder;
//                   Planänderungen rechts neben den Feldern.
//   Titelband       das bisherige Band «Planinhalt» (SIA 400 B.1.3: Planinhalt, Format, Einheit der Masse) über dem Kopf – seit Modul 7 abschaltbar,
//                   Vorgabe aus (H+K-Pläne haben es nicht; der Plantitel steht im Projektfeld).
//   Revisionsstempel  roter, schräger Text über dem Kopf (Vorgabe leer = kein Stempel).
// Gezeichnet wird in PDF-Punkten; alle Masse skalieren mit der Breite (Referenzbreite 180 mm Grundriss-Kopf, 150 mm Schema-Kopf).
// Gestaltungswerte (Zeilenhöhen, Schriftgrössen, Spaltenbreiten, Stempelwinkel 12° und Farbe #E8202A) sind aus den Renderings der echten Pläne
// geschätzt (Näherung), nicht aus den PDF-Objekten gelesen.

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
    /// <summary>Massstab im Format «1 : 50»; leer = «1 : xx», «-» = ohne Massstab (nur Listen).</summary>
    public string Massstab { get; set; } = "";
    /// <summary>Format-Angabe im Titelband, z. B. «A3 quer».</summary>
    public string Format { get; set; } = "";
    /// <summary>Hinweis auf die Einheit der Masse (SIA 400 B.5.3), z. B. «Masse in m» (Titelband).</summary>
    public string MasseHinweis { get; set; } = "Masse in m";
    /// <summary>Planänderungen (Index / Datum / Beschreibung). Leer → eine Strichzeile wie auf der Vorlage.</summary>
    public List<(string Index, string Datum, string Text)> Aenderungen { get; set; } = [];
    /// <summary>Index der letzten Revision (Buchstabe) und ihr Datum; leer, wenn es keine gibt.</summary>
    public string RevIndex { get; set; } = "";
    public string RevDatum { get; set; } = "";
    /// <summary>Titelband «Planinhalt» zeigen (Vorgabe aus).</summary>
    public bool Titelband { get; set; }
    public HkPlankopf.Art Kopfart { get; set; } = HkPlankopf.Art.Grundriss;
    /// <summary>Standorte des Absenders in der Reihenfolge der Ablage (kataloge/buero.json); der erste ist der Hauptsitz (fett).</summary>
    public List<Standort> Standorte { get; set; } = [];
    /// <summary>Text des Revisionsstempels (z. B. «Revisionsplan 21.01.2026»); leer = kein Stempel.</summary>
    public string Stempel { get; set; } = "";
}

public static class HkPlankopf
{
    public enum Art { Grundriss, Schema }

    static readonly XColor Ink = XColor.FromArgb(0x1a, 0x1a, 0x1a);
    /// <summary>Stempelfarbe nach Katalog (geschätzt aus dem Rendering): #E8202A.</summary>
    static readonly XColor Rot = XColor.FromArgb(0xE8, 0x20, 0x2A);
    /// <summary>Anstieg des Stempels in Grad (geschätzt aus dem Rendering: ca. 12°).</summary>
    public const double StempelWinkel = 12;

    // Referenzbreite und Zeilenhöhen in Vorlagen-mm (bei Referenzbreite)
    const double BreiteGrundriss = 180, BreiteSchema = 150;
    const double Band = 10;                          // Titelband (Annahme wie bisher)
    const double AbsenderG = 24, FelderG = 33, Luecke = 2, TabKopf = 5.5, ZeileH = 3.4, Unten = 1.8;
    const double AbsenderS = 15, FelderS = 35, TabKopfS = 10;
    /// <summary>Mehr Planänderungen zeigt der Kopf nicht (die letzten); sonst füllt er das Blatt.</summary>
    public const int MaxZeilen = 12;

    const string Schrift = "Calibri";

    /// <summary>Referenzbreite des Kopfs in mm bei der Normalbreite (180 bzw. 150).</summary>
    static double Referenz(Art art) => art == Art.Schema ? BreiteSchema : BreiteGrundriss;

    /// <summary>Höhe des Kopfs in mm bei der Breite <paramref name="breite"/> mit <paramref name="zeilen"/> Planänderungen (mindestens eine Zeile) und
    /// wahlweise Titelband. Der Kopf wächst mit jeder Zeile nach oben.</summary>
    public static double Hoehe(Art art, double breite, int zeilen, bool titelband)
    {
        int n = Math.Clamp(Math.Max(1, zeilen), 1, MaxZeilen);
        double h = art == Art.Schema
            ? AbsenderS + Math.Max(FelderS, TabKopfS + ZeileH + ZeileH * n + Unten + 2)
            : AbsenderG + FelderG + Luecke + TabKopf + ZeileH + ZeileH * n + Unten + (titelband ? Band : 0);
        return h * breite / Referenz(art);
    }

    /// <summary>Zeichnet den Plankopf in das Rechteck (x, y, w, h) in Punkten; h = <see cref="Hoehe"/> (in Punkten umgerechnet).</summary>
    public static void Zeichnen(XGraphics gfx, double x, double y, double w, double h, HkPlankopfDaten d, XImage? logo)
    {
        if (d.Kopfart == Art.Schema) ZeichnenSchema(gfx, x, y, w, h, d);
        else ZeichnenGrundriss(gfx, x, y, w, h, d, logo);
        Stempel(gfx, x, y, w, h, d);
    }

    static XFont F(double u, double mm, bool fett = false) => new(Schrift, Math.Max(1.0, mm * u), fett ? XFontStyleEx.Bold : XFontStyleEx.Regular);

    /// <summary>Text an der Grundlinie; schrumpft bis 65 %, wenn die Zelle zu schmal ist, und wird danach gekürzt.</summary>
    static void Txt(XGraphics gfx, double u, string? t, double mm, double tx, double tyBase, double maxW, bool fett = false, XStringAlignment al = XStringAlignment.Near)
    {
        if (string.IsNullOrWhiteSpace(t)) return;
        var f = F(u, mm, fett);
        for (int i = 0; i < 6 && gfx.MeasureString(t, f).Width > maxW; i++) f = F(u, mm *= 0.93, fett);
        while (t.Length > 1 && gfx.MeasureString(t, f).Width > maxW) t = t[..^2] + "…";
        double b = gfx.MeasureString(t, f).Width;
        double bx = al == XStringAlignment.Far ? tx - b : al == XStringAlignment.Center ? tx - b / 2 : tx;
        gfx.DrawString(t, f, new XSolidBrush(Ink), new XPoint(bx, tyBase), XStringFormats.BaseLineLeft);
    }

    static string MassstabAnzeige(string? m) => string.IsNullOrWhiteSpace(m) ? "1 : xx" : m;

    /// <summary>Die letzten <see cref="MaxZeilen"/> Planänderungen; ohne Eintrag eine Strichzeile wie auf der Vorlage.</summary>
    static List<(string, string, string)> Zeilen(HkPlankopfDaten d)
    {
        var z = d.Aenderungen.Count > 0 ? d.Aenderungen.Select(a => (a.Index, a.Datum, a.Text)).ToList() : [("-", "-", "-")];
        return z.Count > MaxZeilen ? z.GetRange(z.Count - MaxZeilen, MaxZeilen) : z;
    }

    // ============================================================================================================== Grundriss-Kopf

    static void ZeichnenGrundriss(XGraphics gfx, double x, double y, double w, double h, HkPlankopfDaten d, XImage? logo)
    {
        double u = w / BreiteGrundriss;                 // 1 Vorlagen-Millimeter in Punkten
        double band = d.Titelband ? Band * u : 0;
        double y0 = y + band;                           // Oberkante der Vorlage
        var stark = new XPen(Ink, 0.4 * u);
        var fein = new XPen(Ink, 0.22 * u);
        void T(string? t, double mm, double tx, double ty, double maxW, bool fett = false, XStringAlignment al = XStringAlignment.Near) => Txt(gfx, u, t, mm, tx, ty, maxW, fett, al);

        gfx.DrawRectangle(new XSolidBrush(XColors.White), x, y, w, h);
        gfx.DrawRectangle(stark, x, y, w, h);
        double lx = x + 2.4 * u, rx = x + w - 2.4 * u;

        // ---------------- Titelband (abschaltbar) ----------------
        if (d.Titelband)
        {
            double xm = x + w * 0.50;
            gfx.DrawLine(stark, x, y0, x + w, y0);
            T("Planinhalt", 2.4, lx, y + 3.4 * u, xm - lx - 2 * u);
            T(d.Planinhalt, 3.8, lx, y + 8.2 * u, xm - lx - 2 * u, true);
            T(string.IsNullOrWhiteSpace(d.Format) ? null : "Format " + d.Format, 2.5, xm + 2.4 * u, y + 4.0 * u, x + w - xm - 4 * u);
            T(d.MasseHinweis, 2.5, xm + 2.4 * u, y + 8.0 * u, x + w - xm - 4 * u);
        }

        // ---------------- Absenderblock: Standorte links, Logo rechts ----------------
        double colW = w * 0.25 - 2.4 * u - 1.8 * u;
        for (int i = 0; i < Math.Min(2, d.Standorte.Count); i++)
        {
            var s = d.Standorte[i];
            double sx = i == 0 ? lx : x + w * 0.25;
            bool fett = i == 0;
            T(s.Firma, 3.1, sx, y0 + 4.8 * u, colW, true);
            T(s.Strasse, 2.7, sx, y0 + 9.6 * u, colW, fett);
            T(s.PlzOrt, 2.7, sx, y0 + 13.4 * u, colW, fett);
            T(string.IsNullOrWhiteSpace(s.Telefon) ? null : "Tel: " + s.Telefon, 2.7, sx, y0 + 18.8 * u, colW, fett);
            T(string.IsNullOrWhiteSpace(s.Mail) ? null : "Mail: " + s.Mail, 2.7, sx, y0 + 22.4 * u, colW, fett);
        }
        double logoX = x + w * 0.46;
        if (logo != null)
        {
            double lwd = rx - logoX, lh = lwd * logo.PixelHeight / logo.PixelWidth;
            double maxH = AbsenderG * u - 4 * u;
            if (lh > maxH) { lh = maxH; lwd = lh * logo.PixelWidth / logo.PixelHeight; }
            gfx.DrawImage(logo, rx - lwd, y0 + (AbsenderG * u - lh) / 2, lwd, lh);
        }
        else T("H+K PLANUNGS AG", 4.0, logoX, y0 + 9 * u, rx - logoX, true);
        double yAbs = y0 + AbsenderG * u;
        gfx.DrawLine(stark, x, yAbs, x + w, yAbs);

        // ---------------- Felder: links Phase, Projekt, Plannummer; rechts Datum, Revision, Kontrolle, Massstab ----------------
        double kx1 = x + w * 0.585;
        double lw = kx1 - lx - 2 * u;
        T("Projektphase:", 2.4, lx, yAbs + 4.0 * u, lw);
        T(d.Projektphase, 3.1, lx, yAbs + 8.2 * u, lw);
        T("Projekt:", 2.4, lx, yAbs + 13.4 * u, lw);
        T(d.ProjektZeile1, 3.8, lx, yAbs + 18.2 * u, lw);
        T(d.ProjektZeile2, 3.8, lx, yAbs + 23.0 * u, lw);
        T("Plannummer:", 2.4, lx, yAbs + 27.4 * u, lw);
        T(d.Plannummer, 3.1, lx, yAbs + 31.2 * u, lw);

        var datum = string.IsNullOrWhiteSpace(d.Erstelldatum) ? DateTime.Now.ToString("dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture) : d.Erstelldatum;
        double vw = rx - kx1 - 1 * u;
        void Zeile(string label, string? wert, double yb)
        {
            T(label, 2.6, kx1, yb, vw);
            T(wert, 2.8, rx, yb, vw * 0.6, false, XStringAlignment.Far);
        }
        Zeile("Erstelldatum:", datum, yAbs + 8.2 * u);
        Zeile("Rev. Index:", d.RevIndex, yAbs + 13.7 * u);
        Zeile("Rev. Datum:", d.RevDatum, yAbs + 19.2 * u);
        Zeile("Kontrolliert:", d.Kontrolliert, yAbs + 24.7 * u);
        Zeile("Massstab:", MassstabAnzeige(d.Massstab), yAbs + 30.2 * u);
        double yFel = yAbs + FelderG * u;
        gfx.DrawLine(stark, x, yFel, x + w, yFel);

        // ---------------- Planänderungen: Kasten mit Kopf, Spaltenkopf und beliebig vielen Zeilen ----------------
        var zeilen = Zeilen(d);
        double tx0 = lx, tx1 = rx, zH = ZeileH * u;
        double c1 = tx0 + 0.074 * w, c2 = c1 + 0.10 * w;
        double yT = yFel + Luecke * u;                  // Oberkante des Kastens
        double yKopf = yT + TabKopf * u;                // Unterkante der Überschrift = Oberkante des Spaltenkopfs
        double yEnde = yKopf + zH * (zeilen.Count + 1);
        T("Planänderungen", 3.1, tx0 + 1 * u, yT + 3.9 * u, tx1 - tx0 - 2 * u, true);
        gfx.DrawRectangle(fein, tx0, yT, tx1 - tx0, yEnde - yT);
        gfx.DrawLine(fein, tx0, yKopf, tx1, yKopf);
        TabelleZeichnen(gfx, u, zeilen, fein, tx0, tx1, c1, c2, yKopf, zH);
    }

    /// <summary>Spaltenkopf «Index | Datum | Beschreibung» und die Zeilen unter yKopf (Oberkante des Spaltenkopfs).</summary>
    static void TabelleZeichnen(XGraphics gfx, double u, List<(string Index, string Datum, string Text)> zeilen, XPen fein, double t0, double t1, double c1, double c2, double yKopf, double zH)
    {
        double yEnde = yKopf + zH * (zeilen.Count + 1);
        void T(string? t, double mm, double tx, double ty, double maxW, XStringAlignment al = XStringAlignment.Near) => Txt(gfx, u, t, mm, tx, ty, maxW, false, al);
        gfx.DrawLine(fein, t0, yKopf + zH, t1, yKopf + zH);
        T("Index", 2.4, (t0 + c1) / 2, yKopf + zH - 0.9 * u, c1 - t0 - u, XStringAlignment.Center);
        T("Datum", 2.4, (c1 + c2) / 2, yKopf + zH - 0.9 * u, c2 - c1 - u, XStringAlignment.Center);
        T("Beschreibung", 2.4, (c2 + t1) / 2, yKopf + zH - 0.9 * u, t1 - c2 - u, XStringAlignment.Center);
        for (int i = 0; i < zeilen.Count; i++)
        {
            var (idx, dat, txt) = zeilen[i];
            double yb = yKopf + (i + 2) * zH - 0.9 * u;
            T(string.IsNullOrWhiteSpace(idx) ? "-" : idx, 2.4, (t0 + c1) / 2, yb, c1 - t0 - u, XStringAlignment.Center);
            T(string.IsNullOrWhiteSpace(dat) ? "-" : dat, 2.4, (c1 + c2) / 2, yb, c2 - c1 - u, XStringAlignment.Center);
            T(string.IsNullOrWhiteSpace(txt) ? "-" : txt, 2.4, c2 + 1 * u, yb, t1 - c2 - 2 * u);
            gfx.DrawLine(fein, t0, yKopf + (i + 2) * zH, t1, yKopf + (i + 2) * zH);
        }
        foreach (var vx in new[] { c1, c2 }) gfx.DrawLine(fein, vx, yKopf, vx, yEnde);
    }

    // ============================================================================================================== Schema-Kopf

    static void ZeichnenSchema(XGraphics gfx, double x, double y, double w, double h, HkPlankopfDaten d)
    {
        double u = w / BreiteSchema;
        var stark = new XPen(Ink, 0.4 * u);
        var fein = new XPen(Ink, 0.22 * u);
        void T(string? t, double mm, double tx, double ty, double maxW, bool fett = false, XStringAlignment al = XStringAlignment.Near) => Txt(gfx, u, t, mm, tx, ty, maxW, fett, al);

        gfx.DrawRectangle(new XSolidBrush(XColors.White), x, y, w, h);
        gfx.DrawRectangle(stark, x, y, w, h);
        double lx = x + 2.4 * u, rx = x + w - 2.4 * u;

        // Absender: nur der Hauptsitz (erster Standort) mit Telefon und Web (statt Mail)
        if (d.Standorte.Count > 0)
        {
            var s = d.Standorte[0];
            double c2 = x + w * 0.17;
            T(s.Firma, 3.0, lx, y + 4.6 * u, w - 4.8 * u, true);
            T(s.Strasse, 2.6, lx, y + 9.2 * u, c2 - lx - u);
            T(s.PlzOrt, 2.6, lx, y + 12.8 * u, c2 - lx - u);
            T(string.IsNullOrWhiteSpace(s.Telefon) ? null : "Tel: " + s.Telefon, 2.6, c2, y + 9.2 * u, rx - c2);
            T(!string.IsNullOrWhiteSpace(s.Web) ? s.Web : string.IsNullOrWhiteSpace(s.Mail) ? null : "Mail: " + s.Mail, 2.6, c2, y + 12.8 * u, rx - c2);
        }
        double yAbs = y + AbsenderS * u;
        gfx.DrawLine(stark, x, yAbs, x + w, yAbs);

        // linke Spalte: Phase, Projekt, Plannummer; daneben (unten) Erstelldatum und Kontrolliert
        double xm = x + w * 0.50;
        gfx.DrawLine(stark, xm, yAbs, xm, y + h);
        double lw = xm - lx - 2 * u;
        T("Projektphase", 2.4, lx, yAbs + 3.8 * u, lw);
        T(d.Projektphase, 3.1, lx, yAbs + 7.9 * u, lw);
        T("Projekt:", 2.4, lx, yAbs + 13.2 * u, lw);
        T(d.ProjektZeile1, 3.4, lx, yAbs + 17.8 * u, lw);
        T(d.ProjektZeile2, 3.4, lx, yAbs + 22.0 * u, lw);
        T("Plannummer", 2.4, lx, yAbs + 27.0 * u, lw * 0.55);
        T(d.Plannummer, 3.1, lx, yAbs + 31.2 * u, lw * 0.55);
        var datum = string.IsNullOrWhiteSpace(d.Erstelldatum) ? DateTime.Now.ToString("dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture) : d.Erstelldatum;
        double kx1 = x + w * 0.31, kx2 = xm - 2 * u;
        T("Erstelldatum:", 2.5, kx1, yAbs + 27.0 * u, kx2 - kx1);
        T(datum, 2.5, kx2, yAbs + 27.0 * u, (kx2 - kx1) * 0.55, false, XStringAlignment.Far);
        T("Kontrolliert:", 2.5, kx1, yAbs + 31.2 * u, kx2 - kx1);
        T(d.Kontrolliert, 2.5, kx2, yAbs + 31.2 * u, (kx2 - kx1) * 0.55, false, XStringAlignment.Far);

        // rechte Spalte: Planänderungen
        var zeilen = Zeilen(d);
        double zH = ZeileH * u, t0 = xm + 2 * u, t1 = x + w - 1.2 * u;
        double c1 = t0 + 0.10 * w, c2s = c1 + 0.14 * w;
        T("Planänderungen", 3.1, t0, yAbs + 7.6 * u, t1 - t0, false);
        double yKopf = yAbs + 9.6 * u;
        gfx.DrawLine(fein, t0, yKopf, t1, yKopf);
        TabelleZeichnen(gfx, u, zeilen, fein, t0, t1, c1, c2s, yKopf, zH);
        gfx.DrawLine(fein, t0, yKopf + zH * (zeilen.Count + 1), t1, yKopf + zH * (zeilen.Count + 1));
    }

    // ============================================================================================================== Stempel

    /// <summary>Revisionsstempel: roter Text, 12° ansteigend, über dem Kopf (Mitte des Kopfs, Breite etwa drei Viertel des Kopfs).</summary>
    static void Stempel(XGraphics gfx, double x, double y, double w, double h, HkPlankopfDaten d)
    {
        if (string.IsNullOrWhiteSpace(d.Stempel)) return;
        const double ziel = 0.80;
        var f = new XFont("Arial", 20, XFontStyleEx.BoldItalic);
        double b = gfx.MeasureString(d.Stempel, f).Width;
        double gr = 20 * (w * ziel) / b;
        f = new XFont("Arial", gr, XFontStyleEx.BoldItalic);
        b = gfx.MeasureString(d.Stempel, f).Width;
        var mitte = new XPoint(x + w / 2, y + h * 0.52);
        var zustand = gfx.Save();
        gfx.RotateAtTransform(-StempelWinkel, mitte);
        gfx.DrawString(d.Stempel, f, new XSolidBrush(Rot), new XPoint(mitte.X - b / 2, mitte.Y + gr * 0.3), XStringFormats.BaseLineLeft);
        gfx.Restore(zustand);
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
