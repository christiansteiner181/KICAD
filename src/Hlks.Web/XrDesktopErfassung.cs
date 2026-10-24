// Quest App V1c (Modul XR, XR-I «Desktop in der Brille»): Erfassung des Bildschirms am PC – hinter einer Schnittstelle (IBildschirmQuelle), damit Tests und
// Prüfbilder ein Testbild nutzen und nie den echten Bildschirm.
//
//   XrDesktopGdi       nur Windows: GDI (BitBlt bzw. StretchBlt HALFTONE in eine DIB-Section), DPI-bewusst je Monitor (SetThreadDpiAwarenessContext
//                      PER_MONITOR_AWARE_V2 nur für den erfassenden Thread, während der Erfassung), Monitore über EnumDisplayMonitors, Mauszeiger mit
//                      GetCursorInfo/DrawIconEx eingezeichnet. Keine Pakete, nur user32, gdi32, shcore.
//   XrDesktopTestbild  Attrappe: ein erfundener Bildschirm (Fenster, Schrift in drei Grössen, Linienmuster für die Schärfe, Farbfelder, Bildzähler, Uhr, ein
//                      kreisender Mauszeiger), zwei Monitore. Gilt im Testlauf (HLKS_TEST) und mit HLKS_XR_DESKTOP=testbild.
//   XrDesktopOhne      andere Systeme (Linux, Cloud-Sitzungen): kein Bildschirm.
// Vorgabe (XrDesktopQuellen.Vorgabe): HLKS_XR_DESKTOP=testbild → Testbild; =bildschirm → echter Bildschirm (nur Windows); sonst im Testlauf das Testbild, im Betrieb
// unter Windows GDI. Nichts davon schreibt auf die Platte; erfasst wird nur, solange eine gekoppelte Brille das Bild verlangt (XrDesktop.cs).

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Hlks.Kern;

namespace Hlks.Web;

/// <summary>Ein Monitor des PCs: Id, Name, Lage und Grösse in Bildpunkten (physisch), Hauptbildschirm, DPI (96 = 100 %).</summary>
public sealed record BildschirmInfo(string Id, string Name, int X, int Y, int Breite, int Hoehe, bool Haupt, int Dpi);

/// <summary>Quelle der Bildschirmbilder (siehe Dateikopf).</summary>
public interface IBildschirmQuelle
{
    /// <summary>«bildschirm» (echter Bildschirm), «testbild» oder «keine».</summary>
    string Art { get; }
    /// <summary>Die Monitore (der Hauptbildschirm hat Haupt = true).</summary>
    IReadOnlyList<BildschirmInfo> Bildschirme();
    /// <summary>Erfasst den Monitor id in der Zielgrösse (verkleinert, wenn kleiner) als BGRA, oben links zuerst, in puffer (Länge ≥ Breite × Höhe × 4).
    /// false, wenn es nicht geht (Monitor weg, gesperrter Bildschirm).</summary>
    bool Erfassen(string id, int zielBreite, int zielHoehe, byte[] puffer);
}

public static class XrDesktopQuellen
{
    public const string Umgebung = "HLKS_XR_DESKTOP";

    /// <summary>Die Quelle für diesen Lauf (siehe Dateikopf).</summary>
    public static IBildschirmQuelle Vorgabe()
    {
        var wahl = Environment.GetEnvironmentVariable(Umgebung)?.Trim().ToLowerInvariant();
        if (wahl == "testbild" || (wahl != "bildschirm" && FamilienBibliothekDienst.Testlauf())) return new XrDesktopTestbild();
        if (OperatingSystem.IsWindows()) return new XrDesktopGdi();
        return new XrDesktopOhne();
    }

    /// <summary>Zielgrösse: breiter als maxBreite → verkleinert (Seitenverhältnis bleibt), sonst unverändert.</summary>
    public static (int Breite, int Hoehe) Ziel(int breite, int hoehe, int maxBreite)
    {
        if (breite <= maxBreite || maxBreite <= 0) return (breite, hoehe);
        return (maxBreite, Math.Max(1, (int)Math.Round((double)hoehe * maxBreite / breite)));
    }

    /// <summary>Verkleinert bgra (Mittel über die überdeckten Punkte) in ziel.</summary>
    public static void Verkleinern(byte[] quelle, int qb, int qh, byte[] ziel, int zb, int zh)
    {
        for (int ty = 0; ty < zh; ty++)
        {
            int y0 = (int)((long)ty * qh / zh), y1 = Math.Max(y0 + 1, (int)((long)(ty + 1) * qh / zh));
            for (int tx = 0; tx < zb; tx++)
            {
                int x0 = (int)((long)tx * qb / zb), x1 = Math.Max(x0 + 1, (int)((long)(tx + 1) * qb / zb));
                int sb = 0, sg = 0, sr = 0, n = 0;
                for (int y = y0; y < y1; y++)
                    for (int x = x0, o = (y * qb + x0) * 4; x < x1; x++, o += 4) { sb += quelle[o]; sg += quelle[o + 1]; sr += quelle[o + 2]; n++; }
                var z = (ty * zb + tx) * 4;
                ziel[z] = (byte)(sb / n); ziel[z + 1] = (byte)(sg / n); ziel[z + 2] = (byte)(sr / n); ziel[z + 3] = 255;
            }
        }
    }
}

/// <summary>Kein Bildschirm (andere Systeme als Windows).</summary>
public sealed class XrDesktopOhne : IBildschirmQuelle
{
    public string Art => "keine";
    public IReadOnlyList<BildschirmInfo> Bildschirme() => [];
    public bool Erfassen(string id, int zielBreite, int zielHoehe, byte[] puffer) => false;
}

/// <summary>Attrappe mit Testbild (siehe Dateikopf). bewegt = Zähler, Uhr und Mauszeiger ändern sich mit jedem Bild; sonst steht alles still.</summary>
public sealed class XrDesktopTestbild(bool bewegt = true, Func<double>? sekunden = null) : IBildschirmQuelle
{
    public static readonly BildschirmInfo Eins = new("TESTBILD1", "Testbild 1", 0, 0, 1920, 1080, true, 96);
    public static readonly BildschirmInfo Zwei = new("TESTBILD2", "Testbild 2", 1920, 0, 1280, 1024, false, 96);

    readonly object sperre = new();
    readonly Stopwatch uhr = Stopwatch.StartNew();
    readonly Dictionary<string, byte[]> grund = new(StringComparer.Ordinal);
    byte[]? gross;
    long zaehler;

    public string Art => "testbild";
    public IReadOnlyList<BildschirmInfo> Bildschirme() => [Eins, Zwei];
    /// <summary>So oft wurde erfasst.</summary>
    public long Erfasst => Interlocked.Read(ref zaehler);

    /// <summary>Lage des Mauszeigers (Spitze) im Bild des Monitors zur Zeit t (s).</summary>
    public static (int X, int Y) Zeiger(BildschirmInfo m, double t) =>
        ((int)(m.Breite * (0.42 + 0.16 * Math.Cos(t * 2 * Math.PI / 6))), (int)(m.Hoehe * (0.55 + 0.14 * Math.Sin(t * 2 * Math.PI / 6))));

    public bool Erfassen(string id, int zielBreite, int zielHoehe, byte[] puffer)
    {
        var m = Bildschirme().FirstOrDefault(x => x.Id == id) ?? Eins;
        lock (sperre)
        {
            var n = Interlocked.Increment(ref zaehler);
            var t = bewegt ? sekunden?.Invoke() ?? uhr.Elapsed.TotalSeconds : 0;
            if (!grund.TryGetValue(m.Id, out var g)) grund[m.Id] = g = Grund(m);
            var nativ = zielBreite == m.Breite && zielHoehe == m.Hoehe;
            var bild = nativ ? puffer : (gross is { } gg && gg.Length == m.Breite * m.Hoehe * 4 ? gg : gross = new byte[m.Breite * m.Hoehe * 4]);
            Buffer.BlockCopy(g, 0, bild, 0, g.Length);
            var z = new Zeichner(bild, m.Breite, m.Hoehe);
            // Bildzähler und Uhr (ändern ein paar Kacheln je Bild), Mauszeiger
            var titelY = (int)(m.Hoehe * 0.07) + 8;
            z.Rechteck((int)(m.Breite * 0.42), titelY - 2, 220, 20, 0xE6E6EA);
            z.Text((int)(m.Breite * 0.42) + 6, titelY + 2, 2, $"BILD {(bewegt ? n : 0):000000}", 0x202024);
            var s = TimeSpan.FromSeconds(t);
            z.Rechteck(m.Breite - 190, m.Hoehe - 36, 180, 26, 0x202024);
            z.Text(m.Breite - 180, m.Hoehe - 30, 2, $"{s.Hours:00}:{s.Minutes:00}:{s.Seconds:00}.{s.Milliseconds / 100}", 0xF0F0F0);
            var (px, py) = bewegt ? Zeiger(m, t) : Zeiger(m, 0);
            z.Zeiger(px, py);
            if (!nativ) XrDesktopQuellen.Verkleinern(bild, m.Breite, m.Hoehe, puffer, zielBreite, zielHoehe);
            return true;
        }
    }

    /// <summary>Der stehende Teil des Testbilds (einmal je Monitor gerechnet).</summary>
    static byte[] Grund(BildschirmInfo m)
    {
        int b = m.Breite, h = m.Hoehe;
        var bild = new byte[b * h * 4];
        var z = new Zeichner(bild, b, h);
        z.Rechteck(0, 0, b, h, 0x2B4C6F);                     // Hintergrund des Desktops (flach)
        z.Rechteck(0, h - 48, b, 48, 0x1E1E22);                // Taskleiste
        z.Text(16, h - 34, 3, "KICAD", 0xDDE7EE);
        z.Text(110, h - 30, 2, $"TESTBILD · {m.Name.ToUpperInvariant()} · {b} × {h}", 0xB8C4CC);
        // Fenster 1: Schrift in drei Grössen
        int x1 = (int)(b * 0.06), y1 = (int)(h * 0.07), w1 = (int)(b * 0.55), h1 = (int)(h * 0.62);
        z.Rechteck(x1 + 6, y1 + 6, w1, h1, 0x1C3550);          // Schatten
        z.Rechteck(x1, y1, w1, h1, 0xFFFFFF);
        z.Rechteck(x1, y1, w1, 34, 0xE6E6EA);
        z.Text(x1 + 12, y1 + 10, 2, "KICAD · DESKTOP IN DER BRILLE", 0x202024);
        z.Rechteck(x1 + w1 - 34, y1 + 7, 20, 20, 0xE05A50);
        string[] zeilen =
        [
            "SCHRIFT 1:1  ABCDEFGHIJKLMNOPQRSTUVWXYZ ÄÖÜ 0123456789 .,:-/()+=%",
            "LEITUNG HZ-VL 2.OG · DN 25 · 1.25 M/S · 0.8 BAR · 45 KW",
            "RAUM R-1.04 BÜRO · 24.5 M2 · ZULUFT 120 M3/H · ABLUFT 120 M3/H",
            "WENN DIESE ZEILE IN DER BRILLE LESBAR IST, PASST DIE SCHÄRFE.",
        ];
        int y = y1 + 52;
        foreach (var s in zeilen) { z.Text(x1 + 16, y, 1, s, 0x202024); y += 14; }
        y += 10;
        foreach (var s in zeilen.Take(3)) { z.Text(x1 + 16, y, 2, s, 0x202024); y += 22; }
        y += 10;
        z.Text(x1 + 16, y, 3, "GROSS 3:1  KICAD 2026", 0x14406E); y += 34;
        z.Text(x1 + 16, y, 4, "OBEN LINKS ↑", 0x8A2B2B);
        y += 46;
        // Tabelle wie eine Raumliste (Linien 1 px, Schrift 1:1), darüber kreist der Mauszeiger
        string[] spalten = ["RAUM", "GESCHOSS", "FLÄCHE M2", "ZULUFT M3/H", "ABLUFT M3/H", "HEIZLAST W"];
        var sb = Math.Max(60, (w1 - 32) / spalten.Length);
        for (int zeile = 0; zeile <= 14 && y + zeile * 18 < y1 + h1 - 20; zeile++)
        {
            var zy = y + zeile * 18;
            if (zeile % 2 == 1) z.Rechteck(x1 + 16, zy, sb * spalten.Length, 18, 0xF2F5F8);
            z.Rechteck(x1 + 16, zy, sb * spalten.Length, 1, 0xC8CED6);
            for (int s = 0; s < spalten.Length; s++)
            {
                var text = zeile == 0 ? spalten[s] : s switch
                {
                    0 => $"R-{zeile / 5 + 1}.{zeile:00}", 1 => zeile < 6 ? "EG" : "1.OG", 2 => $"{12 + zeile * 1.7:0.0}", 3 => $"{60 + zeile * 15}", 4 => $"{55 + zeile * 15}",
                    _ => $"{420 + zeile * 37}",
                };
                z.Text(x1 + 22 + s * sb, zy + 6, 1, text, zeile == 0 ? 0x14406E : 0x202024);
                z.Rechteck(x1 + 16 + s * sb, zy, 1, 18, 0xC8CED6);
            }
        }
        // Fenster 2: Linienmuster (Schärfe) und Farbfelder
        int x2 = (int)(b * 0.64), y2 = (int)(h * 0.07), w2 = (int)(b * 0.30), h2 = (int)(h * 0.62);
        z.Rechteck(x2 + 6, y2 + 6, w2, h2, 0x1C3550);
        z.Rechteck(x2, y2, w2, h2, 0xF4F4F6);
        z.Rechteck(x2, y2, w2, 34, 0xE6E6EA);
        z.Text(x2 + 12, y2 + 10, 2, "SCHÄRFE UND FARBEN", 0x202024);
        int mx = x2 + 16, my = y2 + 50, mb = Math.Min(160, (w2 - 48) / 2), mh = 70;
        for (int i = 0; i < 4; i++)
        {
            var (bx, by) = (mx + (i % 2) * (mb + 16), my + (i / 2) * (mh + 30));
            var schritt = i < 2 ? 1 : 2;
            for (int yy = 0; yy < mh; yy++)
                for (int xx = 0; xx < mb; xx++)
                {
                    var an = i % 2 == 0 ? (xx / schritt) % 2 == 0 : ((xx / schritt) + (yy / schritt)) % 2 == 0;
                    z.Punkt(bx + xx, by + yy, an ? 0x000000 : 0xFFFFFF);
                }
            z.Text(bx, by + mh + 6, 1, $"{(i % 2 == 0 ? "LINIEN" : "SCHACH")} {schritt} PX", 0x202024);
        }
        int[] farben = [0xD6E6F5, 0xD9F0E6, 0xF8E0D2, 0xE6DDF3, 0xF3ECD6, 0xDDE7EE, 0xE03030, 0x30B040, 0x3050E0];
        int fy = my + 2 * (mh + 30) + 10, fb = Math.Max(10, (w2 - 32) / farben.Length);
        for (int i = 0; i < farben.Length; i++) z.Rechteck(mx + i * fb, fy, fb - 4, 40, farben[i]);
        z.Text(mx, fy + 48, 1, "PASTELL DER APP · ROT GRÜN BLAU", 0x202024);
        z.Text(b - 300, h - 90, 4, "UNTEN RECHTS", 0xF0F0F0);
        // Felder der App (512 px): Marken an den Ecken, damit die Lage der Texturen im Prüfbild zu sehen ist
        for (int fy0 = 0; fy0 < h; fy0 += 512)
            for (int fx0 = 0; fx0 < b; fx0 += 512)
                z.Text(fx0 + 4, fy0 + 4, 1, $"F{fy0 / 512 * ((b + 511) / 512) + fx0 / 512}", 0xFFD060);
        return bild;
    }

    /// <summary>Einfaches Zeichnen in ein BGRA-Bild (Rechtecke, Punkte, Schrift 5 × 7, Mauszeiger).</summary>
    readonly struct Zeichner(byte[] bild, int b, int h)
    {
        public void Punkt(int x, int y, int rgb)
        {
            if ((uint)x >= (uint)b || (uint)y >= (uint)h) return;
            var o = (y * b + x) * 4;
            bild[o] = (byte)rgb; bild[o + 1] = (byte)(rgb >> 8); bild[o + 2] = (byte)(rgb >> 16); bild[o + 3] = 255;
        }

        public void Rechteck(int x, int y, int w, int hh, int rgb)
        {
            int x0 = Math.Max(0, x), x1 = Math.Min(b, x + w), y0 = Math.Max(0, y), y1 = Math.Min(h, y + hh);
            for (int yy = y0; yy < y1; yy++) for (int xx = x0; xx < x1; xx++) Punkt(xx, yy, rgb);
        }

        public void Text(int x, int y, int skala, string text, int rgb)
        {
            foreach (var c in text)
            {
                var g = Schrift.Zeichen(c);
                for (int zy = 0; zy < 7; zy++)
                    for (int zx = 0; zx < 5; zx++)
                        if ((g[zy] >> (4 - zx) & 1) == 1) Rechteck(x + zx * skala, y + zy * skala, skala, skala, rgb);
                x += 6 * skala;
            }
        }

        /// <summary>Pfeil wie der Mauszeiger von Windows (Spitze bei x, y): schwarzer Rand, weiss gefüllt.</summary>
        public void Zeiger(int x, int y)
        {
            for (int zy = 0; zy < Pfeil.Length; zy++)
                for (int zx = 0; zx < Pfeil[zy].Length; zx++)
                {
                    var c = Pfeil[zy][zx];
                    if (c == 'X') Punkt(x + zx, y + zy, 0x000000);
                    else if (c == '.') Punkt(x + zx, y + zy, 0xFFFFFF);
                }
        }

        static readonly string[] Pfeil =
        [
            "X", "XX", "X.X", "X..X", "X...X", "X....X", "X.....X", "X......X", "X.......X", "X........X", "X.........X", "X......XXXXX", "X...X..X",
            "X..XX..X", "X.X  X..X", "XX   X..X", "X     X..X", "      X..X", "       XX",
        ];
    }
}

/// <summary>Schrift 5 × 7 Punkte (Grossbuchstaben, Ziffern, einige Zeichen; Kleinbuchstaben als Grossbuchstaben) für das Testbild.</summary>
static class Schrift
{
    static readonly Dictionary<char, byte[]> Zeichensatz = new()
    {
        [' '] = [0, 0, 0, 0, 0, 0, 0],
        ['0'] = [0x0E, 0x11, 0x13, 0x15, 0x19, 0x11, 0x0E], ['1'] = [0x04, 0x0C, 0x04, 0x04, 0x04, 0x04, 0x0E], ['2'] = [0x0E, 0x11, 0x01, 0x02, 0x04, 0x08, 0x1F],
        ['3'] = [0x1F, 0x02, 0x04, 0x02, 0x01, 0x11, 0x0E], ['4'] = [0x02, 0x06, 0x0A, 0x12, 0x1F, 0x02, 0x02], ['5'] = [0x1F, 0x10, 0x1E, 0x01, 0x01, 0x11, 0x0E],
        ['6'] = [0x06, 0x08, 0x10, 0x1E, 0x11, 0x11, 0x0E], ['7'] = [0x1F, 0x01, 0x02, 0x04, 0x08, 0x08, 0x08], ['8'] = [0x0E, 0x11, 0x11, 0x0E, 0x11, 0x11, 0x0E],
        ['9'] = [0x0E, 0x11, 0x11, 0x0F, 0x01, 0x02, 0x0C],
        ['A'] = [0x0E, 0x11, 0x11, 0x11, 0x1F, 0x11, 0x11], ['B'] = [0x1E, 0x11, 0x11, 0x1E, 0x11, 0x11, 0x1E], ['C'] = [0x0E, 0x11, 0x10, 0x10, 0x10, 0x11, 0x0E],
        ['D'] = [0x1C, 0x12, 0x11, 0x11, 0x11, 0x12, 0x1C], ['E'] = [0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x1F], ['F'] = [0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x10],
        ['G'] = [0x0E, 0x11, 0x10, 0x17, 0x11, 0x11, 0x0F], ['H'] = [0x11, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11], ['I'] = [0x0E, 0x04, 0x04, 0x04, 0x04, 0x04, 0x0E],
        ['J'] = [0x07, 0x02, 0x02, 0x02, 0x02, 0x12, 0x0C], ['K'] = [0x11, 0x12, 0x14, 0x18, 0x14, 0x12, 0x11], ['L'] = [0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x1F],
        ['M'] = [0x11, 0x1B, 0x15, 0x15, 0x11, 0x11, 0x11], ['N'] = [0x11, 0x11, 0x19, 0x15, 0x13, 0x11, 0x11], ['O'] = [0x0E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E],
        ['P'] = [0x1E, 0x11, 0x11, 0x1E, 0x10, 0x10, 0x10], ['Q'] = [0x0E, 0x11, 0x11, 0x11, 0x15, 0x12, 0x0D], ['R'] = [0x1E, 0x11, 0x11, 0x1E, 0x14, 0x12, 0x11],
        ['S'] = [0x0F, 0x10, 0x10, 0x0E, 0x01, 0x01, 0x1E], ['T'] = [0x1F, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04], ['U'] = [0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E],
        ['V'] = [0x11, 0x11, 0x11, 0x11, 0x11, 0x0A, 0x04], ['W'] = [0x11, 0x11, 0x11, 0x15, 0x15, 0x15, 0x0A], ['X'] = [0x11, 0x11, 0x0A, 0x04, 0x0A, 0x11, 0x11],
        ['Y'] = [0x11, 0x11, 0x11, 0x0A, 0x04, 0x04, 0x04], ['Z'] = [0x1F, 0x01, 0x02, 0x04, 0x08, 0x10, 0x1F],
        ['Ä'] = [0x0A, 0x00, 0x0E, 0x11, 0x1F, 0x11, 0x11], ['Ö'] = [0x0A, 0x00, 0x0E, 0x11, 0x11, 0x11, 0x0E], ['Ü'] = [0x0A, 0x00, 0x11, 0x11, 0x11, 0x11, 0x0E],
        ['.'] = [0, 0, 0, 0, 0, 0x0C, 0x0C], [','] = [0, 0, 0, 0, 0x0C, 0x04, 0x08], [':'] = [0, 0x0C, 0x0C, 0, 0x0C, 0x0C, 0], ['-'] = [0, 0, 0, 0x1F, 0, 0, 0],
        ['/'] = [0, 0x01, 0x02, 0x04, 0x08, 0x10, 0], ['('] = [0x02, 0x04, 0x08, 0x08, 0x08, 0x04, 0x02], [')'] = [0x08, 0x04, 0x02, 0x02, 0x02, 0x04, 0x08],
        ['+'] = [0, 0x04, 0x04, 0x1F, 0x04, 0x04, 0], ['='] = [0, 0, 0x1F, 0, 0x1F, 0, 0], ['%'] = [0x18, 0x19, 0x02, 0x04, 0x08, 0x13, 0x03],
        ['×'] = [0, 0x11, 0x0A, 0x04, 0x0A, 0x11, 0], ['·'] = [0, 0, 0, 0x04, 0, 0, 0], ['?'] = [0x0E, 0x11, 0x01, 0x02, 0x04, 0, 0x04], ['!'] = [0x04, 0x04, 0x04, 0x04, 0x04, 0, 0x04],
        ['_'] = [0, 0, 0, 0, 0, 0, 0x1F], ['↑'] = [0x04, 0x0E, 0x15, 0x04, 0x04, 0x04, 0x04],
    };

    public static byte[] Zeichen(char c) => Zeichensatz.TryGetValue(char.ToUpperInvariant(c), out var g) ? g : Zeichensatz['?'];
}

/// <summary>Erfassung über GDI (nur Windows, siehe Dateikopf). Threadsicher (eine Sperre; zwei Brillen erfassen nacheinander).</summary>
[SupportedOSPlatform("windows")]
public sealed class XrDesktopGdi : IBildschirmQuelle, IDisposable
{
    readonly object sperre = new();
    IntPtr speicherDc, bitmap, alteBitmap, bits;
    int dibB, dibH;
    IReadOnlyList<(BildschirmInfo Info, IntPtr Handle)> monitore = [];
    long monitoreZeit = long.MinValue;

    public string Art => "bildschirm";

    public IReadOnlyList<BildschirmInfo> Bildschirme()
    {
        lock (sperre) return [.. Monitore(true).Select(m => m.Info)];
    }

    /// <summary>Monitore (höchstens alle 2 s neu aufgezählt, sonst aus dem Speicher). Unter der Sperre und im DPI-Bezug des Aufrufers.</summary>
    IReadOnlyList<(BildschirmInfo Info, IntPtr Handle)> Monitore(bool dpiSetzen)
    {
        if (Environment.TickCount64 - monitoreZeit < 2000 && monitore.Count > 0) return monitore;
        var alt = dpiSetzen ? DpiSetzen() : IntPtr.Zero;
        try
        {
            var liste = new List<(BildschirmInfo, IntPtr)>();
            int nr = 0;
            Nativ.MonitorEnumProc rueckruf = (IntPtr h, IntPtr _, ref Nativ.RECT _, IntPtr _) =>
            {
                nr++;
                var mi = new Nativ.MONITORINFOEX { cbSize = Marshal.SizeOf<Nativ.MONITORINFOEX>(), szDevice = "" };
                if (!Nativ.GetMonitorInfo(h, ref mi)) return true;
                int dpi = 96;
                try { if (Nativ.GetDpiForMonitor(h, 0, out var dx, out _) == 0 && dx > 0) dpi = (int)dx; }
                catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { }
                var r = mi.rcMonitor;
                var id = string.IsNullOrEmpty(mi.szDevice) ? $"MONITOR{nr}" : mi.szDevice;
                liste.Add((new BildschirmInfo(id, $"Bildschirm {nr}", r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top, (mi.dwFlags & 1) != 0, dpi), h));
                return true;
            };
            Nativ.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, rueckruf, IntPtr.Zero);
            GC.KeepAlive(rueckruf);
            // Hauptbildschirm zuerst, dann von links nach rechts
            monitore = [.. liste.OrderByDescending(m => m.Item1.Haupt).ThenBy(m => m.Item1.X).ThenBy(m => m.Item1.Y)];
            monitoreZeit = Environment.TickCount64;
            return monitore;
        }
        finally { DpiZurueck(alt); }
    }

    static IntPtr DpiSetzen()
    {
        try { return Nativ.SetThreadDpiAwarenessContext(Nativ.DpiPerMonitorV2); }
        catch (EntryPointNotFoundException) { return IntPtr.Zero; }   // Windows vor 10 1607: ohne
    }

    static void DpiZurueck(IntPtr alt)
    {
        if (alt == IntPtr.Zero) return;
        try { Nativ.SetThreadDpiAwarenessContext(alt); } catch (EntryPointNotFoundException) { }
    }

    public bool Erfassen(string id, int zielBreite, int zielHoehe, byte[] puffer)
    {
        if (zielBreite <= 0 || zielHoehe <= 0 || puffer.Length < zielBreite * zielHoehe * 4) return false;
        lock (sperre)
        {
            var alt = DpiSetzen();
            var bildschirm = IntPtr.Zero;
            try
            {
                var liste = Monitore(false);
                if (liste.Count == 0) return false;
                var m = liste.FirstOrDefault(x => x.Info.Id == id).Info ?? liste[0].Info;
                bildschirm = Nativ.GetDC(IntPtr.Zero);
                if (bildschirm == IntPtr.Zero) return false;
                if (!Bereit(bildschirm, zielBreite, zielHoehe)) return false;
                bool ok;
                if (zielBreite == m.Breite && zielHoehe == m.Hoehe)
                    ok = Nativ.BitBlt(speicherDc, 0, 0, zielBreite, zielHoehe, bildschirm, m.X, m.Y, Nativ.SrcCopy | Nativ.CaptureBlt);
                else
                {
                    Nativ.SetStretchBltMode(speicherDc, Nativ.Halftone);
                    Nativ.SetBrushOrgEx(speicherDc, 0, 0, IntPtr.Zero);
                    ok = Nativ.StretchBlt(speicherDc, 0, 0, zielBreite, zielHoehe, bildschirm, m.X, m.Y, m.Breite, m.Hoehe, Nativ.SrcCopy | Nativ.CaptureBlt);
                }
                if (!ok) return false;
                Zeiger(m, zielBreite, zielHoehe);
                Nativ.GdiFlush();
                Marshal.Copy(bits, puffer, 0, zielBreite * zielHoehe * 4);
                return true;
            }
            finally
            {
                if (bildschirm != IntPtr.Zero) Nativ.ReleaseDC(IntPtr.Zero, bildschirm);
                DpiZurueck(alt);
            }
        }
    }

    /// <summary>Mauszeiger einzeichnen (wenn sichtbar und auf diesem Monitor), an der verkleinerten Stelle, in Originalgrösse.</summary>
    void Zeiger(BildschirmInfo m, int zb, int zh)
    {
        var ci = new Nativ.CURSORINFO { cbSize = Marshal.SizeOf<Nativ.CURSORINFO>() };
        if (!Nativ.GetCursorInfo(ref ci) || (ci.flags & 1) == 0 || ci.hCursor == IntPtr.Zero) return;
        var (px, py) = (ci.ptScreenPos.X - m.X, ci.ptScreenPos.Y - m.Y);
        if (px < 0 || py < 0 || px >= m.Breite || py >= m.Hoehe) return;
        int hx = 0, hy = 0;
        if (Nativ.GetIconInfo(ci.hCursor, out var ii))
        {
            hx = ii.xHotspot;
            hy = ii.yHotspot;
            if (ii.hbmMask != IntPtr.Zero) Nativ.DeleteObject(ii.hbmMask);
            if (ii.hbmColor != IntPtr.Zero) Nativ.DeleteObject(ii.hbmColor);
        }
        var x = (int)((long)px * zb / m.Breite) - hx;
        var y = (int)((long)py * zh / m.Hoehe) - hy;
        Nativ.DrawIconEx(speicherDc, x, y, ci.hCursor, 0, 0, 0, IntPtr.Zero, Nativ.DiNormal);
    }

    /// <summary>Speicher-DC und DIB-Section in der Zielgrösse (oben links zuerst, 32 bit).</summary>
    bool Bereit(IntPtr bildschirm, int b, int h)
    {
        if (speicherDc != IntPtr.Zero && dibB == b && dibH == h) return true;
        Freigeben();
        speicherDc = Nativ.CreateCompatibleDC(bildschirm);
        if (speicherDc == IntPtr.Zero) return false;
        var kopf = new Nativ.BITMAPINFOHEADER { biSize = Marshal.SizeOf<Nativ.BITMAPINFOHEADER>(), biWidth = b, biHeight = -h, biPlanes = 1, biBitCount = 32, biCompression = 0 };
        bitmap = Nativ.CreateDIBSection(bildschirm, ref kopf, 0, out bits, IntPtr.Zero, 0);
        if (bitmap == IntPtr.Zero || bits == IntPtr.Zero) { Freigeben(); return false; }
        alteBitmap = Nativ.SelectObject(speicherDc, bitmap);
        (dibB, dibH) = (b, h);
        return true;
    }

    void Freigeben()
    {
        if (speicherDc != IntPtr.Zero && alteBitmap != IntPtr.Zero) Nativ.SelectObject(speicherDc, alteBitmap);
        if (bitmap != IntPtr.Zero) Nativ.DeleteObject(bitmap);
        if (speicherDc != IntPtr.Zero) Nativ.DeleteDC(speicherDc);
        speicherDc = bitmap = alteBitmap = bits = IntPtr.Zero;
        dibB = dibH = 0;
    }

    public void Dispose()
    {
        lock (sperre) Freigeben();
    }

    static class Nativ
    {
        public static readonly IntPtr DpiPerMonitorV2 = new(-4);
        public const int SrcCopy = 0x00CC0020, CaptureBlt = 0x40000000, Halftone = 4, DiNormal = 3;

        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor, rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szDevice;
        }

        [StructLayout(LayoutKind.Sequential)] public struct CURSORINFO { public int cbSize, flags; public IntPtr hCursor; public POINT ptScreenPos; }
        [StructLayout(LayoutKind.Sequential)] public struct ICONINFO { public int fIcon, xHotspot, yHotspot; public IntPtr hbmMask, hbmColor; }

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public int biSize, biWidth, biHeight;
            public short biPlanes, biBitCount;
            public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
        }

        public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref RECT rect, IntPtr data);

        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
        [DllImport("user32.dll")] public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc proc, IntPtr data);
        [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", CharSet = CharSet.Unicode)] public static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFOEX info);
        [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
        [DllImport("user32.dll")] public static extern bool GetCursorInfo(ref CURSORINFO info);
        [DllImport("user32.dll")] public static extern bool GetIconInfo(IntPtr icon, out ICONINFO info);
        [DllImport("user32.dll")] public static extern bool DrawIconEx(IntPtr hdc, int x, int y, IntPtr icon, int b, int h, int schritt, IntPtr pinsel, int flags);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER kopf, uint verwendung, out IntPtr bits, IntPtr abschnitt, uint versatz);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern bool BitBlt(IntPtr ziel, int x, int y, int b, int h, IntPtr quelle, int qx, int qy, int rop);
        [DllImport("gdi32.dll")] public static extern bool StretchBlt(IntPtr ziel, int x, int y, int b, int h, IntPtr quelle, int qx, int qy, int qb, int qh, int rop);
        [DllImport("gdi32.dll")] public static extern int SetStretchBltMode(IntPtr hdc, int modus);
        [DllImport("gdi32.dll")] public static extern bool SetBrushOrgEx(IntPtr hdc, int x, int y, IntPtr alt);
        [DllImport("gdi32.dll")] public static extern bool GdiFlush();
        [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(IntPtr monitor, int art, out uint dpiX, out uint dpiY);
    }
}
