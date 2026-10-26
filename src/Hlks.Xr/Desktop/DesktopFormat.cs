// Quest App V1c (Modul XR, XR-I): Format der Bilder des PC-Bildschirms (WebSocket /api/xr/desktop, Nachricht «binär») und ihr Lesen in der App. Rein, ohne StereoKit.
// Der PC schreibt sie mit src/Hlks.Web/XrDesktopKodierung.cs (eigene Umsetzung, gegeneinander geprüft in tests/Hlks.Web.Tests/XrDesktopTests.cs – wie GlbSchreiber
// und GlbLeser).
//
// Bild = Kopf (32 Byte, little endian) + Inhalt (Deflate roh, System.IO.Compression):
//    0  4  Magie «HXD1»
//    4  4  Nummer (uint32, ab 1 je Verbindung; die App quittiert sie)
//    8  2  Breite (px)            10  2  Höhe (px)            12  2  Kachel (px, Kantenlänge, 16 … 256)
//   14  1  Art: 0 = Teilbild (nur geänderte Kacheln), 1 = Vollbild (alle Kacheln)
//   15  1  Kodierung: 1 = Deflate, BGR 24 bit, Vorfilter «Sub» je Zeile
//   16  4  Anzahl Kacheln (uint32)  20  4  Länge des Inhalts (uint32)  24  8  Zeit der Erfassung (int64, ms seit 1970 UTC, Uhr des PCs)
//   Inhalt entpackt: je Kachel der Index (uint32, Zeile × Spalten + Spalte), dann ihre Zeilen von oben nach unten, je Zeile Breite × 3 Byte B G R; Vorfilter
//   «Sub»: jedes Byte minus das Byte 3 davor (die ersten 3 roh), modulo 256 – flache Flächen und Schrift werden so zu Nullen und packen gut.
//   Kacheln am rechten bzw. unteren Rand sind schmaler bzw. niedriger.
// Felder (nur in der App): je FeldKacheln × FeldKacheln Kacheln eine Textur (512 × 512 bei Kachel 64): bei 1920 × 1080 sind es 4 × 3 = 12 Texturen bzw.
// Zeichenaufrufe; geändert hochgeladen wird nur ein Feld, in dem sich etwas geändert hat.

using System.Buffers.Binary;
using System.IO.Compression;

namespace Hlks.Xr.Desktop;

/// <summary>Fehler im Format eines Bildes (die App verwirft es und verlangt ein Vollbild).</summary>
public sealed class DesktopFormatFehler(string meldung) : Exception(meldung);

/// <summary>Kopf eines Bildes.</summary>
public readonly record struct DesktopKopf(uint Nr, int Breite, int Hoehe, int Kachel, bool Voll, int Kodierung, int Anzahl, int Laenge, long ZeitMs);

/// <summary>Raster eines Bildschirms: Kacheln (Übertragung) und Felder (Texturen der App).</summary>
public sealed record DesktopRaster(int Breite, int Hoehe, int Kachel, int FeldKacheln = DesktopRaster.FeldKachelnVorgabe)
{
    /// <summary>Kachel 64 px (Annahme V1c: klein genug, dass ein Mauszeiger oder ein Wort nur wenige Kacheln ändert; gross genug für wenig Verwaltung).</summary>
    public const int KachelVorgabe = 64;
    /// <summary>8 × 8 Kacheln je Feld (512 px).</summary>
    public const int FeldKachelnVorgabe = 8;
    public const int MaxSeite = 8192;

    public int Spalten => (Breite + Kachel - 1) / Kachel;
    public int Zeilen => (Hoehe + Kachel - 1) / Kachel;
    public int Kacheln => Spalten * Zeilen;
    public int FeldPx => Kachel * FeldKacheln;
    public int FeldSpalten => (Breite + FeldPx - 1) / FeldPx;
    public int FeldZeilen => (Hoehe + FeldPx - 1) / FeldPx;
    public int Felder => FeldSpalten * FeldZeilen;

    /// <summary>Rechteck einer Kachel in Bildpunkten.</summary>
    public (int X, int Y, int B, int H) KachelRechteck(int index)
    {
        int s = index % Spalten, z = index / Spalten;
        int x = s * Kachel, y = z * Kachel;
        return (x, y, Math.Min(Kachel, Breite - x), Math.Min(Kachel, Hoehe - y));
    }

    /// <summary>Rechteck eines Felds in Bildpunkten.</summary>
    public (int X, int Y, int B, int H) FeldRechteck(int feld)
    {
        int s = feld % FeldSpalten, z = feld / FeldSpalten;
        int x = s * FeldPx, y = z * FeldPx;
        return (x, y, Math.Min(FeldPx, Breite - x), Math.Min(FeldPx, Hoehe - y));
    }

    /// <summary>Feld, in dem eine Kachel liegt.</summary>
    public int FeldVonKachel(int index) => (index / Spalten / FeldKacheln) * FeldSpalten + (index % Spalten) / FeldKacheln;

    public bool Gueltig => Breite is > 0 and <= MaxSeite && Hoehe is > 0 and <= MaxSeite && Kachel is >= 16 and <= 256 && FeldKacheln is >= 1 and <= 32;
}

public static class DesktopFormat
{
    public const int KopfLaenge = 32;
    public const int KodierungDeflate = 1;
    public static ReadOnlySpan<byte> Magie => "HXD1"u8;

    /// <summary>Liest den Kopf; wirft DesktopFormatFehler bei falscher Magie, Grösse oder Länge.</summary>
    public static DesktopKopf KopfLesen(ReadOnlySpan<byte> b)
    {
        if (b.Length < KopfLaenge || !b[..4].SequenceEqual(Magie)) throw new DesktopFormatFehler("Kein Bild des PCs (Magie fehlt).");
        var k = new DesktopKopf(
            BinaryPrimitives.ReadUInt32LittleEndian(b[4..]), BinaryPrimitives.ReadUInt16LittleEndian(b[8..]), BinaryPrimitives.ReadUInt16LittleEndian(b[10..]),
            BinaryPrimitives.ReadUInt16LittleEndian(b[12..]), b[14] == 1, b[15], (int)Math.Min(int.MaxValue, BinaryPrimitives.ReadUInt32LittleEndian(b[16..])),
            (int)Math.Min(int.MaxValue, BinaryPrimitives.ReadUInt32LittleEndian(b[20..])), BinaryPrimitives.ReadInt64LittleEndian(b[24..]));
        var r = new DesktopRaster(k.Breite, k.Hoehe, k.Kachel);
        if (!r.Gueltig) throw new DesktopFormatFehler($"Ungültige Grösse {k.Breite} × {k.Hoehe} (Kachel {k.Kachel}).");
        if (k.Kodierung != KodierungDeflate) throw new DesktopFormatFehler($"Unbekannte Kodierung {k.Kodierung}.");
        if (k.Anzahl > r.Kacheln) throw new DesktopFormatFehler($"{k.Anzahl} Kacheln, das Raster hat nur {r.Kacheln}.");
        if (k.Laenge != b.Length - KopfLaenge) throw new DesktopFormatFehler($"Länge {k.Laenge} statt {b.Length - KopfLaenge}.");
        return k;
    }

    /// <summary>
    /// Entpackt die Kacheln eines Bildes und ruft je Kachel ziel(index, rechteck, bgr) auf (bgr = Zeilen der Kachel, B G R, ohne Vorfilter; nur während des Aufrufs
    /// gültig). Wirft DesktopFormatFehler, wenn der Inhalt nicht zur Zahl der Kacheln passt oder ein Index doppelt bzw. ausserhalb liegt.
    /// </summary>
    public static void Kacheln(ReadOnlySpan<byte> bild, DesktopKopf k, Action<int, (int X, int Y, int B, int H), byte[]> ziel)
    {
        var r = new DesktopRaster(k.Breite, k.Hoehe, k.Kachel);
        var gesehen = new bool[r.Kacheln];
        var puffer = new byte[k.Kachel * k.Kachel * 3];
        var index = new byte[4];
        using var ms = new MemoryStream(bild[KopfLaenge..].ToArray(), writable: false);
        using var ds = new DeflateStream(ms, CompressionMode.Decompress);
        try
        {
            for (int n = 0; n < k.Anzahl; n++)
            {
                if (ds.ReadAtLeast(index, 4, throwOnEndOfStream: false) < 4) throw new DesktopFormatFehler($"Inhalt endet nach {n} von {k.Anzahl} Kacheln.");
                var i = BinaryPrimitives.ReadUInt32LittleEndian(index);
                if (i >= (uint)r.Kacheln || gesehen[i]) throw new DesktopFormatFehler($"Kachel {i} ausserhalb oder doppelt.");
                gesehen[i] = true;
                var rect = r.KachelRechteck((int)i);
                var zeile = rect.B * 3;
                var laenge = zeile * rect.H;
                if (ds.ReadAtLeast(puffer.AsSpan(0, laenge), laenge, throwOnEndOfStream: false) < laenge) throw new DesktopFormatFehler($"Kachel {i} unvollständig.");
                for (int y = 0; y < rect.H; y++)
                {
                    var z = puffer.AsSpan(y * zeile, zeile);
                    for (int x = 3; x < z.Length; x++) z[x] = (byte)(z[x] + z[x - 3]);
                }
                ziel((int)i, rect, puffer);
            }
            if (ds.ReadByte() >= 0) throw new DesktopFormatFehler("Mehr Inhalt als Kacheln.");
        }
        catch (InvalidDataException ex) { throw new DesktopFormatFehler("Inhalt nicht lesbar: " + ex.Message); }
    }
}

/// <summary>
/// Die Bildpunkte des Bildschirms in der App: je Feld ein Puffer RGBA (für Tex.SetColors) mit Stand (zählt bei jeder Änderung hoch). Dekodiert wird im
/// Hintergrund (DesktopVerbindung), hochgeladen im Bild; je Feld eine Sperre, damit keine halbe Kachel hochgeht.
/// </summary>
public sealed class DesktopPuffer
{
    readonly object[] sperren;
    readonly int[] stand;

    public DesktopRaster Raster { get; }
    /// <summary>RGBA je Feld, Zeilen von oben nach unten, Breite und Höhe nach DesktopRaster.FeldRechteck.</summary>
    public byte[][] Felder { get; }
    /// <summary>Nummer des zuletzt angewandten Bildes, Zeit seiner Erfassung (Uhr des PCs) und ob schon ein Vollbild kam.</summary>
    public uint LetzteNr { get; private set; }
    public long LetzteZeitMs { get; private set; }
    public bool HatVollbild { get; private set; }
    public long Kacheln { get; private set; }

    public DesktopPuffer(DesktopRaster raster)
    {
        if (!raster.Gueltig) throw new ArgumentException("Ungültiges Raster.", nameof(raster));
        Raster = raster;
        Felder = new byte[raster.Felder][];
        sperren = new object[raster.Felder];
        stand = new int[raster.Felder];
        for (int f = 0; f < raster.Felder; f++)
        {
            var (_, _, b, h) = raster.FeldRechteck(f);
            Felder[f] = new byte[b * h * 4];
            sperren[f] = new object();
            // vor dem ersten Bild: dunkles Grau, deckend
            for (int i = 0; i < Felder[f].Length; i += 4) { Felder[f][i] = 24; Felder[f][i + 1] = 26; Felder[f][i + 2] = 30; Felder[f][i + 3] = 255; }
        }
    }

    public int Stand(int feld) => Volatile.Read(ref stand[feld]);
    public object Sperre(int feld) => sperren[feld];

    /// <summary>Passt das Bild zu diesem Raster?</summary>
    public bool Passt(DesktopKopf k) => k.Breite == Raster.Breite && k.Hoehe == Raster.Hoehe && k.Kachel == Raster.Kachel;

    /// <summary>Wendet ein Bild an (Kopf schon gelesen und passend). Ein Teilbild vor dem ersten Vollbild wird trotzdem übernommen.</summary>
    public void Anwenden(ReadOnlySpan<byte> bild, DesktopKopf k)
    {
        if (!Passt(k)) throw new DesktopFormatFehler("Das Bild passt nicht zum Raster.");
        var geaendert = new HashSet<int>();
        DesktopFormat.Kacheln(bild, k, (i, rect, bgr) =>
        {
            var f = Raster.FeldVonKachel(i);
            var (fx, fy, fb, _) = Raster.FeldRechteck(f);
            var ziel = Felder[f];
            lock (sperren[f])
            {
                for (int y = 0; y < rect.H; y++)
                {
                    var q = y * rect.B * 3;
                    var o = ((rect.Y - fy + y) * fb + (rect.X - fx)) * 4;
                    for (int x = 0; x < rect.B; x++, q += 3, o += 4)
                    {
                        ziel[o] = bgr[q + 2];
                        ziel[o + 1] = bgr[q + 1];
                        ziel[o + 2] = bgr[q];
                        ziel[o + 3] = 255;
                    }
                }
            }
            geaendert.Add(f);
        });
        foreach (var f in geaendert) Interlocked.Increment(ref stand[f]);
        LetzteNr = k.Nr;
        LetzteZeitMs = k.ZeitMs;
        Kacheln += k.Anzahl;
        if (k.Voll) HatVollbild = true;
    }

    /// <summary>Ein Bildpunkt (R, G, B) – für Tests und Prüfungen.</summary>
    public (byte R, byte G, byte B) Punkt(int x, int y)
    {
        var f = Raster.FeldVonKachel(y / Raster.Kachel * Raster.Spalten + x / Raster.Kachel);
        var (fx, fy, fb, _) = Raster.FeldRechteck(f);
        var o = ((y - fy) * fb + (x - fx)) * 4;
        lock (sperren[f]) return (Felder[f][o], Felder[f][o + 1], Felder[f][o + 2]);
    }
}
