using System.IO.Compression;
using System.Text.Json.Nodes;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using ACadSharp.Types.Units;
using CSMath;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Hilfen der Tests zu den Grundlagen (Modul 1b, B3): kleine DXF-, PDF- und PNG-Dateien selbst erzeugen,
/// Projekt mit Geschossen, echte Dateien aus %USERPROFILE%\HLKS_CAD_testdaten (nur wenn vorhanden).</summary>
static class GrundlagenHilfe
{
    /// <summary>Temp-Ordner für Testdateien; wird am Ende des Tests mit Dispose gelöscht.</summary>
    public sealed class Ordner : IDisposable
    {
        public string Pfad { get; } = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "hlks_b3_" + Guid.NewGuid().ToString("N")[..10])).FullName;
        public string Datei(string name) => Path.Combine(Pfad, name);
        public void Dispose() { try { Directory.Delete(Pfad, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }

    /// <summary>Projekt mit Geschoss EG (Kote 0, Höhe 3000), Grundriss AN-EG (Massstab 50) und Geschoss 1.OG (Kote 3000).</summary>
    public static Projekt Projekt()
    {
        var p = Neu();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Geschoss", ["kennzeichen"] = "1.OG", ["teilVon"] = "GEB",
            ["eigenschaften"] = new JsonObject { ["kote"] = 3000, ["hoehe"] = 3000 },
        }));
        return p;
    }

    public static Element Unterlage(Projekt p, string kz = "UL-1") => Hole(p, kz);

    public static BefehlsErgebnis Einfuegen(Projekt p, string datei, string geschoss = "EG", JsonObject? weitere = null, bool trocken = false)
    {
        var par = new JsonObject { ["datei"] = datei, ["geschoss"] = geschoss };
        foreach (var (k, v) in weitere ?? []) par[k] = v?.DeepClone();
        return p.Ausfuehren("unterlage_einfuegen", par, trocken);
    }

    public static BefehlsErgebnis Kalibrieren(Projekt p, string unterlage, double[][] von, double[][] nach, bool nurVerschieben = false, bool trocken = false)
    {
        var par = new JsonObject { ["unterlage"] = unterlage, ["von"] = Pts(von), ["nach"] = Pts(nach) };
        if (nurVerschieben) par["nurVerschieben"] = true;
        return p.Ausfuehren("unterlage_kalibrieren", par, trocken);
    }

    // ---------- DXF ----------

    /// <summary>Schreibt eine DXF-Datei; <paramref name="fuellen"/> bekommt das Dokument (Modellbereich = doc.Entities).</summary>
    public static string Dxf(Ordner o, string name, UnitsType einheit, Action<CadDocument> fuellen)
    {
        var doc = Dokument(einheit);
        fuellen(doc);
        var pfad = o.Datei(name);
        using (var w = new DxfWriter(pfad, doc, false)) w.Write();
        return pfad;
    }

    public static CadDocument Dokument(UnitsType einheit)
    {
        var doc = new CadDocument();
        doc.Header.InsUnits = einheit;
        return doc;
    }

    public static Layer LayerHolen(CadDocument doc, string name)
    {
        if (doc.Layers.TryGetValue(name, out var l)) return l;
        l = new Layer(name);
        doc.Layers.Add(l);
        return l;
    }

    public static Line Linie(CadDocument doc, double x1, double y1, double x2, double y2, string layer = "0") =>
        new(new XYZ(x1, y1, 0), new XYZ(x2, y2, 0)) { Layer = LayerHolen(doc, layer) };

    /// <summary>Kleines Standard-DXF: Rechteck 10 × 6 (vier Linien) und ein Kreis, in Meter.</summary>
    public static string StandardDxf(Ordner o, string name = "plan.dxf", UnitsType einheit = UnitsType.Meters) =>
        Dxf(o, name, einheit, doc =>
        {
            doc.Entities.Add(Linie(doc, 0, 0, 10, 0, "WAND"));
            doc.Entities.Add(Linie(doc, 10, 0, 10, 6, "WAND"));
            doc.Entities.Add(Linie(doc, 10, 6, 0, 6, "WAND"));
            doc.Entities.Add(Linie(doc, 0, 6, 0, 0, "WAND"));
            doc.Entities.Add(new Circle { Center = new XYZ(5, 3, 0), Radius = 1, Layer = LayerHolen(doc, "MOEBEL") });
        });

    // ---------- PNG ----------

    /// <summary>Einfarbiges PNG (RGB), Breite × Höhe Bildpunkte.</summary>
    public static string Png(Ordner o, string name, int breite, int hoehe, (byte R, byte G, byte B) farbe, double dpi = 72)
    {
        var bgra = new byte[breite * hoehe * 4];
        for (int i = 0; i < breite * hoehe; i++) { bgra[4 * i] = farbe.B; bgra[4 * i + 1] = farbe.G; bgra[4 * i + 2] = farbe.R; bgra[4 * i + 3] = 255; }
        var pfad = o.Datei(name);
        PngSchreiber.Schreiben(pfad, breite, hoehe, bgra, dpi);
        return pfad;
    }

    /// <summary>Entschlüsselt ein von <see cref="PngSchreiber"/> geschriebenes PNG (RGB, 8 Bit) – unabhängige Gegenprobe.</summary>
    public static (int Breite, int Hoehe, byte[] Rgb) PngLesen(string pfad)
    {
        var d = File.ReadAllBytes(pfad);
        Assert.Equal([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A], d[..8]);
        int pos = 8, breite = 0, hoehe = 0;
        var idat = new MemoryStream();
        while (pos < d.Length)
        {
            int len = (d[pos] << 24) | (d[pos + 1] << 16) | (d[pos + 2] << 8) | d[pos + 3];
            var typ = System.Text.Encoding.ASCII.GetString(d, pos + 4, 4);
            var daten = d.AsSpan(pos + 8, len);
            // CRC prüfen
            uint crc = 0xFFFFFFFF;
            foreach (var b in d.AsSpan(pos + 4, len + 4)) { crc ^= b; for (int k = 0; k < 8; k++) crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1; }
            crc ^= 0xFFFFFFFF;
            uint soll = (uint)((d[pos + 8 + len] << 24) | (d[pos + 9 + len] << 16) | (d[pos + 10 + len] << 8) | d[pos + 11 + len]);
            Assert.Equal(soll, crc);
            if (typ == "IHDR") { breite = (daten[0] << 24) | (daten[1] << 16) | (daten[2] << 8) | daten[3]; hoehe = (daten[4] << 24) | (daten[5] << 16) | (daten[6] << 8) | daten[7]; Assert.Equal(8, daten[8]); Assert.Equal(2, daten[9]); }
            else if (typ == "IDAT") idat.Write(daten);
            pos += 12 + len;
        }
        idat.Position = 0;
        using var z = new ZLibStream(idat, CompressionMode.Decompress);
        var roh = new MemoryStream();
        z.CopyTo(roh);
        var r = roh.ToArray();
        int zeile = breite * 3;
        Assert.Equal((zeile + 1) * hoehe, r.Length);
        var rgb = new byte[zeile * hoehe];
        for (int y = 0; y < hoehe; y++)
        {
            byte f = r[y * (zeile + 1)];
            for (int i = 0; i < zeile; i++)
            {
                byte x = r[y * (zeile + 1) + 1 + i];
                byte links = i >= 3 ? rgb[y * zeile + i - 3] : (byte)0, oben = y > 0 ? rgb[(y - 1) * zeile + i] : (byte)0;
                rgb[y * zeile + i] = f switch { 0 => x, 1 => (byte)(x + links), 2 => (byte)(x + oben), _ => throw new Xunit.Sdk.XunitException("Filter " + f) };
            }
        }
        return (breite, hoehe, rgb);
    }

    // ---------- PDF ----------

    /// <summary>PDF mit einer Seite (Breite × Höhe in mm) und einem roten Rechteck mit seinem schwarzen Rand.</summary>
    public static string Pdf(Ordner o, string name, double breiteMm = 210, double hoeheMm = 297, int seiten = 1)
    {
        var doc = new PdfDocument();
        for (int s = 0; s < seiten; s++)
        {
            var seite = doc.AddPage();
            seite.Width = XUnit.FromMillimeter(breiteMm);
            seite.Height = XUnit.FromMillimeter(hoeheMm);
            using var g = XGraphics.FromPdfPage(seite);
            g.DrawRectangle(new XSolidBrush(XColor.FromArgb(255, 200, 0, 0)), XUnit.FromMillimeter(20).Point, XUnit.FromMillimeter(20).Point, XUnit.FromMillimeter(50).Point, XUnit.FromMillimeter(30).Point);
            g.DrawRectangle(new XPen(XColors.Black, 1), 1, 1, seite.Width.Point - 2, seite.Height.Point - 2);
        }
        var pfad = o.Datei(name);
        doc.Save(pfad);
        return pfad;
    }

    // ---------- Plangrafik der Unterlagen ----------

    /// <summary>Plangrafik nur der Unterlagen: wie Grafik.Plan, aber direkt über GrundlagenGrafik. Grafik.Plan erreicht die
    /// Grundlagen heute nicht, weil HlksGrafik.Zustaendig für jedes Element true ist und in Grafik.Quellen vor GrundlagenGrafik
    /// steht (Befund B3-1, siehe Schlussbericht).</summary>
    public static Plangrafik PlanGrundlage(Projekt p, string ansicht = "AN-EG")
    {
        var a = p.Modell.HoleDerKlasse(ansicht, "Ansicht");
        var ctx = Grafik.Kontext(p, a);
        var q = new GrundlagenGrafik();
        var g = new Plangrafik { Ansicht = a.Kennzeichen, Art = ctx.Art, Massstab = ctx.Massstab };
        foreach (var e in p.Modell.Elemente.Where(e => q.Zustaendig(p.Katalog, e) && Grafik.Sichtbar(ctx, e))) g.Teile.AddRange(q.Plan(ctx, e));
        g.Begrenzung = Grafik.Begrenzung(g.Teile);
        return g;
    }

    // ---------- echte Dateien ----------

    /// <summary>Echte Datei aus HLKS_CAD_testdaten oder null (Test überspringt sich).</summary>
    public static string? Echt(string relativ) => IfcHilfe.Echt(relativ);

    public static List<Primitiv> Grundlage(Plangrafik g) =>
        g.Teile.Where(t => t.Ebene == "grundlage").SelectMany(t => t.Primitive).ToList();
}
