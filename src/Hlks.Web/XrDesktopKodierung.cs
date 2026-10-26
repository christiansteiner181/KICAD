// Quest App V1c (Modul XR, XR-I «Desktop in der Brille»): Kodierung der Bilder des PC-Bildschirms für die App. Format: src/Hlks.Xr/Desktop/DesktopFormat.cs (Kopf
// 32 Byte, Inhalt Deflate roh). Eigene Umsetzung mit Bordmitteln (System.IO.Compression), ohne Paket – wie der GlbSchreiber; die App liest mit ihrem eigenen
// Leser, die Tests prüfen beides gegeneinander (tests/Hlks.Web.Tests/XrDesktopTests.cs).
//
// Je Bild: der Bildschirm (BGRA, 4 Byte je Punkt, oben links zuerst) in Kacheln (64 px); eine Kachel geht nur mit, wenn sie sich seit dem letzten GESENDETEN Bild
// geändert hat (Vergleich Zeile für Zeile, Span.SequenceEqual) – beim ersten Bild und nach Zurücksetzen alle (Vollbild). Ohne Änderung gibt es kein Bild. Je Kachel
// BGR ohne Alpha (der Bildschirm hat keine Transparenz), Vorfilter «Sub», dann alle Kacheln zusammen mit Deflate «Fastest» gepackt.
// Kein Bild bleibt liegen: nur das zuletzt gesendete Bild steht im Speicher (für den Vergleich), nichts geht auf die Platte.

using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;

namespace Hlks.Web;

/// <summary>Ein kodiertes Bild: die Nachricht, Nummer, Zahl der Kacheln, Grösse vor dem Packen, Dauer der Kodierung (ms), Vollbild.</summary>
public sealed record XrDesktopBild(byte[] Daten, uint Nr, int Kacheln, long Roh, double Ms, bool Voll);

public sealed class XrDesktopKodierer(int kachel = XrDesktopKodierer.KachelVorgabe)
{
    /// <summary>Format «HXD1» (wie DesktopFormat der App, eigene Umsetzung): Kopflänge, Kodierung Deflate, Kachel 64 px, grösste Seite.</summary>
    public const int KopfLaenge = 32, KodierungDeflate = 1, KachelVorgabe = 64, MaxSeite = 8192;
    static ReadOnlySpan<byte> Magie => "HXD1"u8;

    byte[]? vorher;
    int breite, hoehe;
    uint nr;
    readonly MemoryStream roh = new();
    readonly MemoryStream gepackt = new();

    public int Kachel { get; } = Math.Clamp(kachel, 16, 256);

    /// <summary>Das nächste Bild wird ein Vollbild (neue Verbindung, neuer Monitor, die App verlangt es).</summary>
    public void Zuruecksetzen() => vorher = null;

    /// <summary>
    /// Kodiert bgra (Breite × Höhe × 4) gegen das zuletzt gesendete Bild. Gibt null zurück, wenn sich nichts geändert hat. Das Bild gilt danach als gesendet.
    /// </summary>
    public XrDesktopBild? Kodieren(byte[] bgra, int b, int h, long zeitMs)
    {
        var uhr = Stopwatch.StartNew();
        if (b <= 0 || h <= 0 || b > MaxSeite || h > MaxSeite || bgra.Length < b * h * 4)
            throw new ArgumentException("Ungültige Grösse des Bildes.", nameof(bgra));
        var voll = vorher is null || breite != b || hoehe != h;
        if (voll) { vorher = new byte[b * h * 4]; breite = b; hoehe = h; }
        int spalten = (b + Kachel - 1) / Kachel, zeilen = (h + Kachel - 1) / Kachel;
        roh.SetLength(0);
        Span<byte> index = stackalloc byte[4];
        var zeileBgr = new byte[Kachel * 3];
        int anzahl = 0;
        for (int i = 0; i < spalten * zeilen; i++)
        {
            int x = i % spalten * Kachel, y = i / spalten * Kachel, kb = Math.Min(Kachel, b - x), kh = Math.Min(Kachel, h - y);
            if (!voll && Gleich(bgra, vorher!, b, x, y, kb, kh)) continue;
            anzahl++;
            BinaryPrimitives.WriteUInt32LittleEndian(index, (uint)i);
            roh.Write(index);
            for (int zy = 0; zy < kh; zy++)
            {
                var q = ((y + zy) * b + x) * 4;
                // BGR und Vorfilter «Sub» (jedes Byte minus das Byte 3 davor)
                byte pb = 0, pg = 0, pr = 0;
                for (int zx = 0, o = 0; zx < kb; zx++, q += 4, o += 3)
                {
                    byte cb = bgra[q], cg = bgra[q + 1], cr = bgra[q + 2];
                    zeileBgr[o] = (byte)(cb - pb);
                    zeileBgr[o + 1] = (byte)(cg - pg);
                    zeileBgr[o + 2] = (byte)(cr - pr);
                    (pb, pg, pr) = (cb, cg, cr);
                }
                roh.Write(zeileBgr, 0, kb * 3);
                Buffer.BlockCopy(bgra, ((y + zy) * b + x) * 4, vorher!, ((y + zy) * b + x) * 4, kb * 4);
            }
        }
        if (anzahl == 0) return null;
        gepackt.SetLength(0);
        gepackt.Write(new byte[KopfLaenge]);
        using (var ds = new DeflateStream(gepackt, CompressionLevel.Fastest, leaveOpen: true))
            ds.Write(roh.GetBuffer(), 0, (int)roh.Length);
        var daten = gepackt.ToArray();
        nr++;
        var k = daten.AsSpan(0, KopfLaenge);
        Magie.CopyTo(k);
        BinaryPrimitives.WriteUInt32LittleEndian(k[4..], nr);
        BinaryPrimitives.WriteUInt16LittleEndian(k[8..], (ushort)b);
        BinaryPrimitives.WriteUInt16LittleEndian(k[10..], (ushort)h);
        BinaryPrimitives.WriteUInt16LittleEndian(k[12..], (ushort)Kachel);
        k[14] = (byte)(voll ? 1 : 0);
        k[15] = KodierungDeflate;
        BinaryPrimitives.WriteUInt32LittleEndian(k[16..], (uint)anzahl);
        BinaryPrimitives.WriteUInt32LittleEndian(k[20..], (uint)(daten.Length - KopfLaenge));
        BinaryPrimitives.WriteInt64LittleEndian(k[24..], zeitMs);
        return new XrDesktopBild(daten, nr, anzahl, roh.Length, uhr.Elapsed.TotalMilliseconds, voll);
    }

    static bool Gleich(byte[] a, byte[] v, int b, int x, int y, int kb, int kh)
    {
        for (int zy = 0; zy < kh; zy++)
        {
            var o = ((y + zy) * b + x) * 4;
            // nur B, G, R zählen; Alpha ist bei der Erfassung beliebig – hier zählt sie mit (kostet nichts, die Quellen setzen sie fest)
            if (!a.AsSpan(o, kb * 4).SequenceEqual(v.AsSpan(o, kb * 4))) return false;
        }
        return true;
    }
}
