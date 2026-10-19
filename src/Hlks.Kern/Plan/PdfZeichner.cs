// Zeichnet die Primitive der Plangrafik (Kern-Grafik, Modul 1a) als Vektoren in ein PDF (PDFsharp 6.5).
// Dieselben Primitive zeichnet der Bildschirm; hier nur die Übersetzung: Modell-mm → Papier-mm → PDF-Punkte,
// Strichstärke in Papier-mm → Punkte, Linienarten als Strichmuster, Füllung, Schraffur (Linienscharen/Punkte, auf
// das Polygon zugeschnitten), Text und Rasterbilder von Unterlagen. Primitive mit Stil.NurBildschirm werden weggelassen.
//
// Gestaltungswerte, die nicht aus SIA 400 stammen, sind als «Annahme» markiert (Liste im Bericht an Christian).

using System.Globalization;
using PdfSharp.Drawing;
using PdfSharp.Fonts;

namespace Hlks.Kern;

/// <summary>Abbildung Modell-mm → Papier-mm (Ursprung unten links): papier = O + S · modell, x und y gleich.</summary>
public readonly record struct Abbildung(double S, double Ox, double Oy)
{
    /// <summary>Papier-mm direkt (keine Umrechnung).</summary>
    public static readonly Abbildung Papier = new(1, 0, 0);

    /// <summary>Ansichtsfenster: Modellpunkt (cx, cy) kommt auf den Papierpunkt (lx, ly), Massstab 1:massstab.</summary>
    public static Abbildung Fuer(double massstab, double lx, double ly, double cx, double cy) =>
        new(1 / massstab, lx - cx / massstab, ly - cy / massstab);

    public double X(double x) => Ox + S * x;
    public double Y(double y) => Oy + S * y;
}

public sealed class PdfZeichner
{
    /// <summary>Punkte je Papier-mm (1 mm = 72/25.4 pt).</summary>
    public const double PtJeMm = 72.0 / 25.4;

    // Strichmuster der Linienarten in Papier-mm: Strich, Lücke, … (Annahme; SIA 400 legt nur die Linienart fest)
    static readonly double[] Strich = [3.0, 1.0];
    static readonly double[] Strichpunkt = [6.0, 1.0, 0.5, 1.0];
    static readonly double[] Punkt = [0.5, 1.0];

    /// <summary>Durchmesser eines Punkts der Punktschraffur in mm (Annahme).</summary>
    const double PunktMm = 0.2;
    /// <summary>Obergrenze der Schraffurlinien bzw. -punkte je Polygon; darüber wird der Abstand vergrössert (Annahme, schützt die Dateigrösse).</summary>
    const int MaxLinien = 20000, MaxPunkte = 60000;
    const string Arial = "Arial";
    /// <summary>Zeilenabstand mehrzeiliger Texte als Vielfaches der Schrifthöhe (Annahme).</summary>
    const double Zeilenabstand = 1.2;

    readonly XGraphics gfx;
    readonly double blattHoehe;
    readonly Dictionary<(double, bool), XFont> schriften = [];

    public PdfZeichner(XGraphics gfx, double blattHoeheMm)
    {
        Schriften.Sicherstellen();
        this.gfx = gfx;
        blattHoehe = blattHoeheMm;
    }

    // ---------- Umrechnung ----------

    /// <summary>Breite eines Texts in Papier-mm (für die Anordnung von Titelzeilen).</summary>
    public double Breite(string text, double schriftMm, bool fett = false) => gfx.MeasureString(text, Schrift(schriftMm, fett)).Width / PtJeMm;

    XPoint Pt(double papierX, double papierY) => new(papierX * PtJeMm, (blattHoehe - papierY) * PtJeMm);

    public static XColor Farbe(string? hex, double? deckkraft = null)
    {
        int a = deckkraft is double d ? (int)Math.Round(Math.Clamp(d, 0, 1) * 255) : 255;
        if (hex is { Length: 7 } && hex[0] == '#'
            && int.TryParse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
            return XColor.FromArgb(a, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
        return XColor.FromArgb(a, 0, 0, 0);   // null = Planfarbe: auf dem Papier schwarz
    }

    XPen Stift(Stil s, double? mm = null)
    {
        // Deckkraft gilt der Füllung; ohne Füllung der Linie (Unterlagen DWG/IFC: graue Linien halb deckend)
        var pen = new XPen(Farbe(s.Farbe, s.Fuellung is null ? s.Deckkraft : null), (mm ?? Stifte.Mm(s.Stift)) * PtJeMm) { LineJoin = XLineJoin.Miter };
        var muster = s.Linie switch { "strich" => Strich, "strichpunkt" => Strichpunkt, "punkt" => Punkt, _ => null };
        if (muster is not null)
        {
            // PDFsharp gibt DashPattern als Vielfache der Strichbreite an
            double b = pen.Width;
            pen.DashStyle = XDashStyle.Custom;
            pen.DashPattern = muster.Select(m => m * PtJeMm / b).ToArray();
        }
        return pen;
    }

    XFont Schrift(double mm, bool fett)
    {
        var key = (Math.Round(mm, 3), fett);
        if (!schriften.TryGetValue(key, out var f))
            schriften[key] = f = new XFont(Arial, Math.Max(1, mm * PtJeMm), fett ? XFontStyleEx.Bold : XFontStyleEx.Regular);
        return f;
    }

    // ---------- Zeichnen ----------

    // Rang nach Ebenen.Reihenfolge (Grafiktypen.cs). Eigene Suche, weil Ebenen.Rang die Reihenfolge auf List<string> umwandelt,
    // was bei einem Sammlungsausdruck (Array) scheitert – Befund im Bericht.
    static int Rang(string? ebene)
    {
        var r = Ebenen.Reihenfolge;
        for (int i = 0; i < r.Count; i++) if (r[i] == ebene) return i;
        for (int i = 0; i < r.Count; i++) if (r[i] == "sonst") return i;
        return r.Count;
    }

    /// <summary>Alle Teile einer Plangrafik in der Zeichenreihenfolge der Ebenen (unten → oben).</summary>
    public void Zeichnen(Plangrafik g, Abbildung abb)
    {
        foreach (var t in g.Teile.OrderBy(t => Rang(t.Ebene)))   // stabil: innerhalb einer Ebene Listenreihenfolge
            Zeichnen(t.Primitive, abb);
    }

    public void Zeichnen(IEnumerable<Primitiv> primitive, Abbildung abb)
    {
        foreach (var p in primitive)
        {
            if (p.Stil.NurBildschirm) continue;
            switch (p.Art)
            {
                case "linie" or "polylinie": Linien(p, abb); break;
                case "polygon": Polygon(p, abb); break;
                case "kreis": Kreis(p, abb); break;
                case "bogen": Bogen(p, abb); break;
                case "text": Text(p, abb); break;
                case "bild": Bild(p, abb); break;
            }
        }
    }

    /// <summary>Alles Folgende auf das Rechteck (Papier-mm) zuschneiden; Rückgabe der Zustand zum Wiederherstellen.</summary>
    public XGraphicsState Zuschneiden(double x0, double y0, double x1, double y1)
    {
        var s = gfx.Save();
        var a = Pt(x0, y1); var b = Pt(x1, y0);
        gfx.IntersectClip(new XRect(a.X, a.Y, b.X - a.X, b.Y - a.Y));
        return s;
    }

    public void Wiederherstellen(XGraphicsState s) => gfx.Restore(s);

    XPoint[] Punkte(IEnumerable<double[]> pts, Abbildung abb) => pts.Select(q => Pt(abb.X(q[0]), abb.Y(q[1]))).ToArray();

    void Linien(Primitiv p, Abbildung abb)
    {
        if (p.Punkte.Count < 2 || p.Stil.Stift is null) return;
        var pen = Stift(p.Stil);
        var pts = Punkte(p.Punkte, abb);
        if (pts.Length == 2) gfx.DrawLine(pen, pts[0], pts[1]);
        else gfx.DrawLines(pen, pts);
    }

    XGraphicsPath Pfad(Primitiv p, Abbildung abb)
    {
        var pfad = new XGraphicsPath { FillMode = XFillMode.Alternate };   // Löcher bleiben leer
        pfad.AddPolygon(Punkte(p.Punkte, abb));
        foreach (var loch in p.Loecher ?? [])
            if (loch.Count >= 3) pfad.AddPolygon(Punkte(loch, abb));
        return pfad;
    }

    XBrush? Pinsel(Stil s)
    {
        if (s.Fuellung is null) return null;
        var farbe = s.Fuellung switch
        {
            "schnitt" => Farbe(s.Farbe, s.Deckkraft),
            "papier" => Farbe("#ffffff", s.Deckkraft),
            _ => Farbe(s.Fuellung, s.Deckkraft),
        };
        return new XSolidBrush(farbe);
    }

    void Polygon(Primitiv p, Abbildung abb)
    {
        if (p.Punkte.Count < 3) return;
        var pfad = Pfad(p, abb);
        if (Pinsel(p.Stil) is { } pinsel) gfx.DrawPath(pinsel, pfad);
        if (Schraffuren.Definition(p.Stil.Muster) is { } muster) Schraffieren(p, abb, pfad, muster);
        if (p.Stil.Stift is not null) gfx.DrawPath(Stift(p.Stil), pfad);
    }

    /// <summary>Schraffur im Papier-Raster (Muster liegen deshalb an Nachbarflächen lückenlos aneinander), auf das Polygon geclippt.</summary>
    void Schraffieren(Primitiv p, Abbildung abb, XGraphicsPath pfad, Schraffur muster)
    {
        var ring = p.Punkte.Select(q => (X: abb.X(q[0]), Y: abb.Y(q[1]))).ToList();
        double x0 = ring.Min(q => q.X), x1 = ring.Max(q => q.X), y0 = ring.Min(q => q.Y), y1 = ring.Max(q => q.Y);
        var farbe = Farbe(p.Stil.Farbe);
        if (muster.Art == "voll") { gfx.DrawPath(new XSolidBrush(farbe), pfad); return; }
        if (muster.AbstandMm <= 0) return;

        var zustand = gfx.Save();
        gfx.IntersectClip(pfad);
        if (muster.Art == "linien")
        {
            var pen = new XPen(farbe, Stifte.Mm(Stifte.Duenn) * PtJeMm);
            double cx = (x0 + x1) / 2, cy = (y0 + y1) / 2, r = Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0)) / 2;
            foreach (var winkel in muster.Winkel)
            {
                double w = winkel * Math.PI / 180, dx = Math.Cos(w), dy = Math.Sin(w), nx = -dy, ny = dx;
                // Abstand der Ecken zur Linie durch den Ursprung: n·p
                var ns = new[] { nx * x0 + ny * y0, nx * x1 + ny * y0, nx * x0 + ny * y1, nx * x1 + ny * y1 };
                double d = muster.AbstandMm;
                int k0 = (int)Math.Floor(ns.Min() / d), k1 = (int)Math.Ceiling(ns.Max() / d);
                if (k1 - k0 + 1 > MaxLinien) d = (ns.Max() - ns.Min()) / MaxLinien;
                k0 = (int)Math.Floor(ns.Min() / d); k1 = (int)Math.Ceiling(ns.Max() / d);
                double tc = dx * cx + dy * cy;
                for (int k = k0; k <= k1; k++)
                {
                    double bx = nx * k * d, by = ny * k * d;
                    gfx.DrawLine(pen, Pt(bx + dx * (tc - r), by + dy * (tc - r)), Pt(bx + dx * (tc + r), by + dy * (tc + r)));
                }
            }
        }
        else if (muster.Art == "punkte")
        {
            var pen = new XPen(farbe, PunktMm * PtJeMm) { LineCap = XLineCap.Round };
            double d = muster.AbstandMm;
            double anzahl = (x1 - x0) / d * ((y1 - y0) / d);
            if (anzahl > MaxPunkte) d *= Math.Sqrt(anzahl / MaxPunkte);
            var ringe = new List<List<(double X, double Y)>> { ring };
            foreach (var loch in p.Loecher ?? []) ringe.Add(loch.Select(q => (abb.X(q[0]), abb.Y(q[1]))).ToList());
            for (double y = Math.Ceiling(y0 / d) * d; y <= y1; y += d)
                for (double x = Math.Ceiling(x0 / d) * d; x <= x1; x += d)
                    if (Innen(ringe, x, y)) gfx.DrawLine(pen, Pt(x, y), Pt(x + 0.01, y));   // Punkt = sehr kurzer Strich mit runder Kappe
        }
        gfx.Restore(zustand);
    }

    /// <summary>Gerade-ungerade-Regel über alle Ringe (Aussenring und Löcher).</summary>
    static bool Innen(List<List<(double X, double Y)>> ringe, double x, double y)
    {
        bool innen = false;
        foreach (var r in ringe)
            for (int i = 0, j = r.Count - 1; i < r.Count; j = i++)
                if ((r[i].Y > y) != (r[j].Y > y) && x < (r[j].X - r[i].X) * (y - r[i].Y) / (r[j].Y - r[i].Y) + r[i].X) innen = !innen;
        return innen;
    }

    void Kreis(Primitiv p, Abbildung abb)
    {
        if (p.Punkte.Count < 1 || p.Radius is not double radius) return;
        var m = p.Punkte[0]; double r = radius * abb.S;
        var o = Pt(abb.X(m[0]) - r, abb.Y(m[1]) + r);
        double d = 2 * r * PtJeMm;
        if (Pinsel(p.Stil) is { } pinsel) gfx.DrawEllipse(pinsel, o.X, o.Y, d, d);
        if (p.Stil.Stift is not null) gfx.DrawEllipse(Stift(p.Stil), o.X, o.Y, d, d);
    }

    void Bogen(Primitiv p, Abbildung abb)
    {
        if (p.Punkte.Count < 1 || p.Radius is not double radius || p.Von is not double von || p.Bis is not double bis || p.Stil.Stift is null) return;
        double sweep = (bis - von) % 360;
        if (sweep <= 0) sweep += 360;
        var m = p.Punkte[0]; double r = radius * abb.S;
        var o = Pt(abb.X(m[0]) - r, abb.Y(m[1]) + r);
        double d = 2 * r * PtJeMm;
        // Im PDF zeigt y nach unten: ein Bogen von «von» nach «bis» im Gegenuhrzeigersinn beginnt dort bei -bis und läuft im Uhrzeigersinn.
        gfx.DrawArc(Stift(p.Stil), o.X, o.Y, d, d, -(von + sweep), sweep);
    }


    // ---------- Bild (Unterlage PDF/Bild, Modul 1b) ----------

    readonly Dictionary<string, XImage> bilder = [];

    /// <summary>Rasterbild einer Unterlage in seine vier Ecken setzen (unten links, unten rechts, oben rechts, oben links; gedreht
    /// möglich). Die Bilddatei kennt der Kern über die Id der Unterlage (<see cref="UnterlagenBild.PfadVon"/>). Deckkraft unter 1:
    /// PDFsharp kennt keine Bildtransparenz, darum wird das Bild mit Weiss in Höhe von 1 − Deckkraft überdeckt – gleich wie
    /// Transparenz auf weissem Papier; die Unterlage liegt ganz unten (Ebene «grundlage»).</summary>
    void Bild(Primitiv p, Abbildung abb)
    {
        if (p.Punkte.Count < 4 || p.Quelle is null || UnterlagenBild.PfadVon(p.Quelle) is not { } pfad) return;
        if (!bilder.TryGetValue(pfad, out var bild))
        {
            try { bilder[pfad] = bild = XImage.FromStream(new MemoryStream(File.ReadAllBytes(pfad))); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotImplementedException or InvalidOperationException or ArgumentException) { return; }   // Bild fehlt oder nicht lesbar: Plan ohne Unterlage
        }
        var ol = Pt(abb.X(p.Punkte[3][0]), abb.Y(p.Punkte[3][1]));   // oben links
        var oR = Pt(abb.X(p.Punkte[2][0]), abb.Y(p.Punkte[2][1]));   // oben rechts
        var ul = Pt(abb.X(p.Punkte[0][0]), abb.Y(p.Punkte[0][1]));   // unten links
        double breite = Math.Sqrt((oR.X - ol.X) * (oR.X - ol.X) + (oR.Y - ol.Y) * (oR.Y - ol.Y));
        double hoehe = Math.Sqrt((ul.X - ol.X) * (ul.X - ol.X) + (ul.Y - ol.Y) * (ul.Y - ol.Y));
        double winkel = Math.Atan2(oR.Y - ol.Y, oR.X - ol.X) * 180 / Math.PI;   // Seitenkoordinaten: y nach unten, Winkel im Uhrzeigersinn
        if (breite <= 0 || hoehe <= 0) return;
        var zustand = gfx.Save();
        gfx.RotateAtTransform(winkel, ol);
        gfx.DrawImage(bild, ol.X, ol.Y, breite, hoehe);
        if (p.Stil.Deckkraft is double d && d < 1)
            gfx.DrawRectangle(new XSolidBrush(Farbe("#ffffff", 1 - Math.Clamp(d, 0, 1))), ol.X, ol.Y, breite, hoehe);
        gfx.Restore(zustand);
    }

    void Text(Primitiv p, Abbildung abb)
    {
        if (p.Punkte.Count < 1 || string.IsNullOrEmpty(p.Text)) return;
        double groesse = p.Groesse ?? 2.5;   // Schrifthöhe in Papier-mm; als Schriftgrad (em) verwendet (Annahme)
        var font = Schrift(groesse, p.Fett == true);
        var pinsel = new XSolidBrush(Farbe(p.Stil.Farbe));
        var format = new XStringFormat
        {
            Alignment = p.Ausrichtung switch { "mitte" => XStringAlignment.Center, "rechts" => XStringAlignment.Far, _ => XStringAlignment.Near },
            LineAlignment = XLineAlignment.BaseLine,
        };
        var ort = Pt(abb.X(p.Punkte[0][0]), abb.Y(p.Punkte[0][1]));
        var zustand = gfx.Save();
        if (p.Winkel is double w && w != 0) gfx.RotateAtTransform(-w, ort);   // Gegenuhrzeigersinn
        var zeilen = p.Text.Replace("\r", "").Split('\n');
        for (int i = 0; i < zeilen.Length; i++)
            if (zeilen[i].Length > 0)
                gfx.DrawString(zeilen[i], font, pinsel, new XPoint(ort.X, ort.Y + i * groesse * Zeilenabstand * PtJeMm), format);
        gfx.Restore(zustand);
    }
}

/// <summary>Schriften für die PDF-Ausgabe: Arial im Plan, Calibri im Plankopf (Hausvorlage), aus dem Schriftenordner von Windows.
/// Der eingebaute Windows-Auflöser von PDFsharp kennt Calibri nicht; deshalb ein eigener Auflöser.</summary>
static class Schriften
{
    static bool fertig;
    static readonly object sperre = new();

    public static void Sicherstellen()
    {
        lock (sperre)
        {
            if (fertig) return;
            fertig = true;
            // PDFsharp 6.2.4 (MIT) – keine Lizenzerklärung nötig; ab 6.5 AGPL mit Lizenzprüfung, siehe Hlks.Kern.csproj.
            GlobalFontSettings.FontResolver = new SchriftenAufloeser();
        }
    }
}

sealed class SchriftenAufloeser : IFontResolver
{
    // Familie → Dateien (normal, fett, kursiv, fett kursiv) im Schriftenordner
    static readonly Dictionary<string, string[]> Familien = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Arial"] = ["arial.ttf", "arialbd.ttf", "ariali.ttf", "arialbi.ttf"],
        ["Calibri"] = ["calibri.ttf", "calibrib.ttf", "calibrii.ttf", "calibriz.ttf"],
    };

    static readonly string[] Ordner =
    [
        Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts"),
    ];

    static string? Suche(string datei) => Ordner.Where(o => o.Length > 0).Select(o => Path.Combine(o, datei)).FirstOrDefault(File.Exists);

    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic)
    {
        int stil = (bold ? 1 : 0) + (italic ? 2 : 0);
        int index = stil switch { 0 => 0, 1 => 1, 2 => 2, _ => 3 };
        // gewünschte Familie, sonst die andere (Schriftart fehlt auf dem Rechner)
        foreach (var name in new[] { familyName }.Concat(Familien.Keys))
            if (Familien.TryGetValue(name, out var dateien) && Suche(dateien[index]) is not null)
                return new FontResolverInfo(dateien[index]);
        throw new HlksFehler($"Für die PDF-Ausgabe fehlt die Schrift «{familyName}» (Arial/Calibri im Schriftenordner).", "Arial oder Calibri installieren.");
    }

    public byte[]? GetFont(string faceName) => Suche(faceName) is { } pfad ? File.ReadAllBytes(pfad) : null;
}
