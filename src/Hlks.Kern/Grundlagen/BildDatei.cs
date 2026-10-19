// Bilddateien der Unterlagen (Modul 1b, B3), ohne System.Drawing (soll auch am Mac laufen):
//   BildDatei    Grösse und Auflösung aus PNG und JPEG lesen
//   PngSchreiber PNG schreiben (ZLibStream + CRC32, adaptive Zeilenfilter, pHYs mit der Auflösung)
//   PdfSeite     eine PDF-Seite mit Docnet.Core (PDFium) als Bild rendern

using System.Buffers.Binary;
using System.IO.Compression;
using Docnet.Core;
using Docnet.Core.Converters;
using Docnet.Core.Models;

namespace Hlks.Kern;

/// <summary>Kopfdaten einer Bilddatei: Format («png» oder «jpg»), Pixel und – wenn die Datei sie nennt – Punkte je Zoll.</summary>
public readonly record struct BildInfo(string Format, int Breite, int Hoehe, double? Dpi);

public static class BildDatei
{
    /// <summary>Format und Grösse einer PNG- oder JPEG-Datei (nach dem Inhalt, nicht nach der Endung).</summary>
    public static BildInfo Lesen(string pfad)
    {
        using var fs = File.OpenRead(pfad);
        var kopf = new byte[8];
        int n = fs.Read(kopf, 0, 8);
        fs.Position = 0;
        if (n == 8 && kopf[0] == 0x89 && kopf[1] == 'P' && kopf[2] == 'N' && kopf[3] == 'G') return Png(fs, pfad);
        if (n >= 3 && kopf[0] == 0xFF && kopf[1] == 0xD8) return Jpeg(fs, pfad);
        throw new HlksFehler($"«{Path.GetFileName(pfad)}» ist weder PNG noch JPEG.",
            "Bilder als PNG oder JPEG (.png, .jpg, .jpeg) einfügen; andere Formate zuerst umwandeln.");
    }

    static BildInfo Png(Stream s, string pfad)
    {
        s.Position = 8;
        var kopf = new byte[8];
        int breite = 0, hoehe = 0;
        double? dpi = null;
        while (s.Read(kopf, 0, 8) == 8)
        {
            uint laenge = BinaryPrimitives.ReadUInt32BigEndian(kopf);
            string typ = System.Text.Encoding.ASCII.GetString(kopf, 4, 4);
            if (typ == "IHDR")
            {
                var d = new byte[8];
                s.ReadExactly(d);
                breite = (int)BinaryPrimitives.ReadUInt32BigEndian(d); hoehe = (int)BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(4));
                s.Position += laenge - 8 + 4;
            }
            else if (typ == "pHYs" && laenge == 9)
            {
                var d = new byte[9];
                s.ReadExactly(d);
                uint x = BinaryPrimitives.ReadUInt32BigEndian(d);
                if (d[8] == 1 && x > 0) dpi = x * 0.0254;   // Pixel je Meter
                s.Position += 4;
            }
            else if (typ == "IDAT" || typ == "IEND") break;
            else s.Position += laenge + 4;
        }
        if (breite <= 0 || hoehe <= 0) throw new HlksFehler($"Die PNG-Datei «{Path.GetFileName(pfad)}» ist beschädigt (kein Kopf).", "Datei neu speichern oder exportieren.");
        return new BildInfo("png", breite, hoehe, dpi);
    }

    static BildInfo Jpeg(Stream s, string pfad)
    {
        s.Position = 2;
        double? dpi = null;
        var b = new byte[4];
        while (true)
        {
            int m;
            do { m = s.ReadByte(); } while (m != 0xFF && m >= 0);
            do { m = s.ReadByte(); } while (m == 0xFF);
            if (m < 0 || m == 0xD9 || m == 0xDA) break;
            if (m is 0x01 or (>= 0xD0 and <= 0xD8)) continue;   // Marker ohne Länge
            if (s.Read(b, 0, 2) != 2) break;
            int laenge = (b[0] << 8) | b[1];
            if (m is >= 0xC0 and <= 0xCF and not (0xC4 or 0xC8 or 0xCC))
            {
                var d = new byte[5];
                s.ReadExactly(d);
                return new BildInfo("jpg", (d[3] << 8) | d[4], (d[1] << 8) | d[2], dpi);
            }
            if (m == 0xE0 && laenge >= 16)
            {
                var d = new byte[laenge - 2];
                s.ReadExactly(d);
                if (d[0] == 'J' && d[1] == 'F' && d[2] == 'I' && d[3] == 'F')
                {
                    int einheit = d[7], dx = (d[8] << 8) | d[9];
                    if (dx > 0) dpi = einheit == 1 ? dx : einheit == 2 ? dx * 2.54 : null;
                }
                continue;
            }
            s.Position += laenge - 2;
        }
        throw new HlksFehler($"Die JPEG-Datei «{Path.GetFileName(pfad)}» ist beschädigt (keine Bildgrösse).", "Datei neu speichern oder exportieren.");
    }
}

public static class PngSchreiber
{
    static readonly uint[] Tabelle = Enumerable.Range(0, 256).Select(n =>
    {
        uint c = (uint)n;
        for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
        return c;
    }).ToArray();

    static uint Crc(uint crc, ReadOnlySpan<byte> d)
    {
        foreach (var b in d) crc = Tabelle[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }

    static void Chunk(Stream s, string typ, ReadOnlySpan<byte> daten)
    {
        Span<byte> kopf = stackalloc byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(kopf, (uint)daten.Length);
        System.Text.Encoding.ASCII.GetBytes(typ, kopf[4..]);
        s.Write(kopf);
        s.Write(daten);
        uint crc = Crc(Crc(0xFFFFFFFF, kopf[4..]), daten) ^ 0xFFFFFFFF;
        Span<byte> ende = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(ende, crc);
        s.Write(ende);
    }

    /// <summary>Schreibt ein RGB-PNG (8 Bit). <paramref name="bgra"/> = Zeilen von oben, je Pixel Blau, Grün, Rot, Alpha
    /// (so liefert PDFium); der Alphakanal wird nicht übernommen. <paramref name="dpi"/> kommt in den pHYs-Block.</summary>
    public static void Schreiben(string pfad, int breite, int hoehe, byte[] bgra, double dpi)
    {
        if (bgra.Length < (long)breite * hoehe * 4) throw new ArgumentException("Bilddaten zu kurz.");
        using var fs = File.Create(pfad);
        fs.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);
        var ihdr = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdr, (uint)breite);
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(4), (uint)hoehe);
        ihdr[8] = 8; ihdr[9] = 2;   // 8 Bit, RGB
        Chunk(fs, "IHDR", ihdr);
        var phys = new byte[9];
        uint ppm = (uint)Math.Round(dpi / 0.0254);
        BinaryPrimitives.WriteUInt32BigEndian(phys, ppm); BinaryPrimitives.WriteUInt32BigEndian(phys.AsSpan(4), ppm);
        phys[8] = 1;
        Chunk(fs, "pHYs", phys);

        var komprimiert = new MemoryStream();
        using (var z = new ZLibStream(komprimiert, CompressionLevel.Optimal, leaveOpen: true))
        {
            int zeile = breite * 3;
            var vorher = new byte[zeile];
            var roh = new byte[zeile];
            var kandidaten = new byte[3][];
            for (int i = 0; i < 3; i++) kandidaten[i] = new byte[zeile];
            for (int y = 0; y < hoehe; y++)
            {
                int q = y * breite * 4;
                for (int x = 0; x < breite; x++)
                {
                    roh[3 * x] = bgra[q + 4 * x + 2]; roh[3 * x + 1] = bgra[q + 4 * x + 1]; roh[3 * x + 2] = bgra[q + 4 * x];
                }
                // Filter 0 (keiner), 1 (Sub), 2 (Up): den mit der kleinsten Summe der Beträge nehmen
                Array.Copy(roh, kandidaten[0], zeile);
                for (int i = 0; i < zeile; i++)
                {
                    kandidaten[1][i] = (byte)(roh[i] - (i >= 3 ? roh[i - 3] : 0));
                    kandidaten[2][i] = (byte)(roh[i] - vorher[i]);
                }
                int bester = 0; long beste = long.MaxValue;
                for (int f = 0; f < 3; f++)
                {
                    long summe = 0;
                    foreach (var b in kandidaten[f]) summe += b < 128 ? b : 256 - b;
                    if (summe < beste) { beste = summe; bester = f; }
                }
                z.WriteByte((byte)bester);
                z.Write(kandidaten[bester], 0, zeile);
                (vorher, roh) = (roh, vorher);
            }
        }
        var daten = komprimiert.GetBuffer().AsSpan(0, (int)komprimiert.Length);
        for (int o = 0; o < daten.Length; o += 1 << 16) Chunk(fs, "IDAT", daten.Slice(o, Math.Min(1 << 16, daten.Length - o)));
        Chunk(fs, "IEND", []);
    }
}

public static class PdfSeite
{
    static readonly object sperre = new();
    /// <summary>Sperre für PDFium (Docnet ist nicht für gleichzeitigen Gebrauch gebaut).</summary>
    internal static object Sperre => sperre;
    /// <summary>Obergrenze der Bildpunkte einer gerenderten Seite (Speicher: 4 Byte je Punkt).</summary>
    public const long MaxPixel = 50_000_000;
    /// <summary>Seitengrösse wird mit 8-facher Auflösung gelesen (PDFium liefert ganze Bildpunkte): Fehler unter 0.01 %.</summary>
    const double Aufloesung = 8;

    /// <summary>Anzahl Seiten und Grösse der Seite in PDF-Punkten (1/72 Zoll).</summary>
    public static (int Seiten, double BreitePt, double HoehePt) Mass(string pdf, int seite)
    {
        try
        {
            lock (sperre)
            {
                using var doc = DocLib.Instance.GetDocReader(pdf, new PageDimensions(Aufloesung));
                int n = doc.GetPageCount();
                if (seite < 1 || seite > n) return (n, 0, 0);
                using var s = doc.GetPageReader(seite - 1);
                return (n, s.GetPageWidth() / Aufloesung, s.GetPageHeight() / Aufloesung);
            }
        }
        catch (Exception ex) when (ex is not HlksFehler)
        {
            throw new HlksFehler($"Das PDF «{Path.GetFileName(pdf)}» lässt sich nicht lesen: {ex.Message}",
                "Prüfen, ob die Datei ein gültiges, nicht passwortgeschütztes PDF ist; sonst neu exportieren.");
        }
    }

    /// <summary>Rendert eine Seite (ab 1) mit <paramref name="dpi"/> in eine PNG-Datei, Pixel (Breite, Höhe).</summary>
    public static (int Breite, int Hoehe) Rendern(string pdf, int seite, double dpi, string zielPng)
    {
        var (seiten, bw, bh) = Mass(pdf, seite);
        if (seite < 1 || seite > seiten)
            throw new HlksFehler($"Das PDF hat {seiten} Seite(n), Seite {seite} gibt es nicht.", $"seite zwischen 1 und {seiten} wählen.");
        double s = dpi / 72.0;
        if ((long)Math.Ceiling(bw * s) * (long)Math.Ceiling(bh * s) > MaxPixel)
            throw new HlksFehler($"Die Seite wäre bei {dpi:0} dpi {Math.Ceiling(bw * s):0} × {Math.Ceiling(bh * s):0} Bildpunkte gross (mehr als {MaxPixel / 1_000_000} Millionen).",
                "Eine kleinere Auflösung wählen (dpi, z. B. 100) – der Massstab bleibt davon unberührt.");
        try
        {
            lock (sperre)
            {
                using var doc = DocLib.Instance.GetDocReader(pdf, new PageDimensions(s));
                using var page = doc.GetPageReader(seite - 1);
                int w = page.GetPageWidth(), h = page.GetPageHeight();
                var bgra = page.GetImage(new NaiveTransparencyRemover(255, 255, 255));   // transparente Stellen weiss
                PngSchreiber.Schreiben(zielPng, w, h, bgra, w / (bw / 72.0));
                return (w, h);
            }
        }
        catch (Exception ex) when (ex is not HlksFehler)
        {
            throw new HlksFehler($"Seite {seite} von «{Path.GetFileName(pdf)}» lässt sich nicht darstellen: {ex.Message}",
                "Das PDF neu exportieren oder eine andere Seite wählen.");
        }
    }
}

/// <summary>Rohes Seitenbild einer PDF-Seite im Speicher (Modul 2: Ausschnitte für unterlage_bild). Teilt die Sperre mit
/// <see cref="PdfSeite"/>, weil Docnet/PDFium nicht gleichzeitig benutzt werden darf.</summary>
public static class PdfSeiteBild
{
    /// <summary>Rendert eine Seite (ab 1) mit dem Faktor <paramref name="skala"/> (Bildpunkte je pt) als BGRA-Zeilen von oben.
    /// Quelle: Datei oder – wenn <paramref name="bytes"/> gesetzt ist – das PDF im Speicher.</summary>
    public static (int Breite, int Hoehe, byte[] Bgra) Rendern(string? pdf, byte[]? bytes, int seite, double skala)
    {
        lock (PdfSeite.Sperre)
        {
            using var doc = bytes is not null
                ? DocLib.Instance.GetDocReader(bytes, new PageDimensions(skala))
                : DocLib.Instance.GetDocReader(pdf!, new PageDimensions(skala));
            if (seite < 1 || seite > doc.GetPageCount())
                throw new HlksFehler($"Das PDF hat {doc.GetPageCount()} Seite(n), Seite {seite} gibt es nicht.", $"seite zwischen 1 und {doc.GetPageCount()} wählen.");
            using var page = doc.GetPageReader(seite - 1);
            int w = page.GetPageWidth(), h = page.GetPageHeight();
            var bgra = page.GetImage(new NaiveTransparencyRemover(255, 255, 255));
            return (w, h, bgra);
        }
    }
}
