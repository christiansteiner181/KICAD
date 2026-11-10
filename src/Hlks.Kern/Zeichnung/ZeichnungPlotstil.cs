// 2D-CAD «Zeichnung»: Plotstile (farbabhängige Plotstiltabellen, CTB). Ein Plotstil legt je AutoCAD-Farbe (1–255) fest, in welcher Farbe, mit
// welcher Strichstärke und welcher Rasterung (Deckkraft in %) geplottet wird. Er gilt nur für die Ausgabe (PDF, Bild), nicht am Bildschirm, und
// nur für Objekte mit einer Indexfarbe – Echtfarben plotten immer in ihrer Farbe (wie in AutoCAD).
// Dateiformat CTB: Textkopf «PIAFILEVERSION_2.0,CTBVER1,compress … pmzlibcodec», danach 12 Bytes (Prüfsumme und Grössen) und ein zlib-Strom.
// Der entpackte Text enthält plot_style{ 0{ color=…  screen=…  lineweight=… } … } (Eintrag 0 = Farbe 1) und custom_lineweight_table{ 0=… }.
//   color       −1 = Farbe des Objekts, sonst RGB in den unteren 24 Bit
//   lineweight   0 = Strichstärke des Objekts, sonst Nummer (ab 1) in der Tabelle der Strichstärken (mm)
//   screen       100 = volle Farbe, kleiner = zum Papierweiss hin aufgehellt
// Eingebaut (ohne Datei): «monochrom» (alles schwarz) und «graustufen». Benannte Plotstile (STB) werden nicht unterstützt.
// Stand 10.11.2026: an einer selbst erzeugten CTB geprüft, noch an keiner Datei aus AutoCAD (auf diesem Rechner und auf W: liegt keine).

using System.IO.Compression;
using System.Text;

namespace Hlks.Kern;

public sealed class Plotstil
{
    public string Name { get; }
    public string? Datei { get; }
    readonly (string? Farbe, double? Mm, int Raster)[] eintraege = new (string?, double?, int)[256];

    Plotstil(string name, string? datei)
    {
        Name = name; Datei = datei;
        for (int i = 0; i < eintraege.Length; i++) eintraege[i] = (null, null, 100);
    }

    /// <summary>Vorgabe für die AutoCAD-Farbe 1–255: Farbe (#rrggbb, null = Farbe des Objekts), Strichstärke in mm (null = die des Objekts), Rasterung in %.</summary>
    public (string? Farbe, double? Mm, int Raster) Eintrag(int aci) => aci is >= 1 and <= 255 ? eintraege[aci] : eintraege[7];

    public static readonly Plotstil Monochrom = Eingebaut("monochrom", _ => "#000000");
    public static readonly Plotstil Graustufen = Eingebaut("graustufen", aci =>
    {
        var rgb = ACadSharp.Color.GetIndexRGB((byte)aci);
        int g = aci == 7 ? 0 : (int)Math.Round(0.299 * rgb[0] + 0.587 * rgb[1] + 0.114 * rgb[2]);
        return $"#{g:x2}{g:x2}{g:x2}";
    });

    static Plotstil Eingebaut(string name, Func<int, string> farbe)
    {
        var p = new Plotstil(name, null);
        for (int i = 1; i <= 255; i++) p.eintraege[i] = (farbe(i), null, 100);
        return p;
    }

    // ---------- CTB lesen ----------

    public static Plotstil Lesen(string pfad)
    {
        var name = Path.GetFileName(pfad);
        if (pfad.EndsWith(".stb", StringComparison.OrdinalIgnoreCase))
            throw new HlksFehler($"«{name}» ist eine benannte Plotstiltabelle (STB); die werden nicht unterstützt.", "Eine farbabhängige Tabelle (CTB) oder «monochrom» wählen.");
        byte[] roh;
        try { roh = File.ReadAllBytes(pfad); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { throw new HlksFehler($"Der Plotstil «{name}» ist nicht lesbar: {ex.Message}"); }
        if (roh.Length > 5 * 1024 * 1024) throw new HlksFehler($"«{name}» ist zu gross für eine Plotstiltabelle.");
        string text;
        try { text = Entpacken(roh); }
        catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or ArgumentException)
        { throw new HlksFehler($"«{name}» ist keine lesbare Plotstiltabelle (CTB): {ex.Message}", "Die Datei aus dem AutoCAD-Ordner «Plot Styles» neu kopieren."); }
        return Deuten(text, name, pfad);
    }

    /// <summary>Entpackt den Inhalt einer CTB-Datei: der zlib-Strom beginnt nach «pmzlibcodec» und 12 Bytes; zur Sicherheit wird sein Kopf (0x78) gesucht.</summary>
    static string Entpacken(byte[] roh)
    {
        var marke = Encoding.ASCII.GetBytes("pmzlibcodec");
        int pos = roh.AsSpan().IndexOf(marke);
        if (pos < 0) throw new InvalidDataException("Kennung «pmzlibcodec» fehlt.");
        pos += marke.Length;
        int start = -1;
        for (int i = pos; i < Math.Min(roh.Length - 1, pos + 32); i++)
            if (roh[i] == 0x78 && (roh[i + 1] == 0x01 || roh[i + 1] == 0x5E || roh[i + 1] == 0x9C || roh[i + 1] == 0xDA)) { start = i; break; }
        if (start < 0) throw new InvalidDataException("Der gepackte Inhalt fehlt.");
        using var ms = new MemoryStream(roh, start, roh.Length - start);
        using var z = new ZLibStream(ms, CompressionMode.Decompress);
        using var aus = new MemoryStream();
        var puffer = new byte[8192];
        int n;
        while ((n = z.Read(puffer, 0, puffer.Length)) > 0)
        {
            aus.Write(puffer, 0, n);
            if (aus.Length > 20 * 1024 * 1024) throw new InvalidDataException("Der Inhalt ist zu gross.");
        }
        return Encoding.Latin1.GetString(aus.ToArray());
    }

    /// <summary>Liest plot_style{ n{ … } } und custom_lineweight_table{ … } aus dem entpackten Text.</summary>
    public static Plotstil Deuten(string text, string name, string? datei = null)
    {
        var p = new Plotstil(name, datei);
        var staerken = new Dictionary<int, double>();
        var roh = new Dictionary<int, (int Farbe, int Staerke, int Raster)>();
        var pfad = new List<string>();
        int nummer = -1; int farbe = -1, staerke = 0, raster = 100;
        foreach (var zeileRoh in text.Split('\n'))
        {
            var zeile = zeileRoh.Trim().TrimEnd('\r', '\0');
            if (zeile.Length == 0) continue;
            if (zeile.EndsWith('{'))
            {
                pfad.Add(zeile[..^1].Trim());
                if (pfad.Count == 2 && pfad[0] == "plot_style" && int.TryParse(pfad[1], out nummer)) { farbe = -1; staerke = 0; raster = 100; } else if (pfad.Count == 2) nummer = -1;
                continue;
            }
            if (zeile == "}")
            {
                if (pfad.Count == 2 && pfad[0] == "plot_style" && nummer >= 0) roh[nummer] = (farbe, staerke, raster);
                if (pfad.Count > 0) pfad.RemoveAt(pfad.Count - 1);
                continue;
            }
            int gleich = zeile.IndexOf('=');
            if (gleich <= 0) continue;
            string schluessel = zeile[..gleich].Trim(), wert = zeile[(gleich + 1)..].Trim().Trim('"');
            if (pfad.Count == 2 && pfad[0] == "plot_style" && nummer >= 0)
            {
                if (schluessel == "color" && int.TryParse(wert, out var c)) farbe = c;
                else if (schluessel == "lineweight" && int.TryParse(wert, out var l)) staerke = l;
                else if (schluessel == "screen" && int.TryParse(wert, out var s)) raster = Math.Clamp(s, 0, 100);
            }
            else if (pfad.Count == 1 && pfad[0] == "custom_lineweight_table" && int.TryParse(schluessel, out var i)
                     && double.TryParse(wert, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var mm))
                staerken[i] = mm;
        }
        if (roh.Count == 0) throw new HlksFehler($"«{name}» enthält keine Plotstile.", "Die Datei ist keine farbabhängige Plotstiltabelle (CTB).");
        foreach (var (n, e) in roh)
        {
            int aci = n + 1;   // Eintrag 0 = Farbe 1
            if (aci is < 1 or > 255) continue;
            string? hex = e.Farbe == -1 ? null : $"#{(e.Farbe >> 16) & 255:x2}{(e.Farbe >> 8) & 255:x2}{e.Farbe & 255:x2}";
            double? mm = e.Staerke > 0 && staerken.TryGetValue(e.Staerke - 1, out var w) && w > 0 ? w : null;
            p.eintraege[aci] = (hex, mm, e.Raster);
        }
        return p;
    }

    /// <summary>Schreibt eine CTB-Datei aus entpacktem Text (für Tests und eigene Tabellen): Kopf, 12 Bytes, zlib.</summary>
    public static void Schreiben(string pfad, string text)
    {
        var inhalt = Encoding.Latin1.GetBytes(text);
        using var gepackt = new MemoryStream();
        using (var z = new ZLibStream(gepackt, CompressionLevel.Optimal, leaveOpen: true)) z.Write(inhalt);
        using var f = File.Create(pfad);
        f.Write(Encoding.ASCII.GetBytes("PIAFILEVERSION_2.0,CTBVER1,compress\r\npmzlibcodec"));
        f.Write(BitConverter.GetBytes(0u)); f.Write(BitConverter.GetBytes((uint)inhalt.Length)); f.Write(BitConverter.GetBytes((uint)gepackt.Length));
        gepackt.Position = 0; gepackt.CopyTo(f);
    }

    // ---------- Suchen ----------

    /// <summary>Ordner, in denen Plotstile gesucht werden: neben der Zeichnung, HLKS_PLOTSTILE, %LOCALAPPDATA%\HLKS_CAD\plotstile, die «Plot Styles» von AutoCAD.</summary>
    static IEnumerable<string> Ordner(string? zeichnungsOrdner)
    {
        if (!string.IsNullOrEmpty(zeichnungsOrdner)) yield return zeichnungsOrdner;
        if (Environment.GetEnvironmentVariable("HLKS_PLOTSTILE") is { Length: > 0 } env) yield return env;
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HLKS_CAD", "plotstile");
        var autodesk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Autodesk");
        if (!Directory.Exists(autodesk)) yield break;
        var opt = new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 5, IgnoreInaccessible = true };
        IEnumerable<string> gefunden;
        try { gefunden = Directory.EnumerateDirectories(autodesk, "Plot Styles", opt).ToList(); } catch (IOException) { gefunden = []; }
        foreach (var o in gefunden) yield return o;
    }

    /// <summary>Vorhandene Plotstile: die eingebauten und die CTB-Dateien der Suchordner.</summary>
    public static List<(string Name, string? Datei)> Vorhandene(string? zeichnungsOrdner)
    {
        var liste = new List<(string, string?)> { ("monochrom", null), ("graustufen", null) };
        var namen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var o in Ordner(zeichnungsOrdner).Where(Directory.Exists))
        {
            IEnumerable<string> dateien;
            try { dateien = Directory.EnumerateFiles(o, "*.ctb").ToList(); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }
            foreach (var f in dateien) if (namen.Add(Path.GetFileName(f))) liste.Add((Path.GetFileName(f), f));
        }
        return liste;
    }

    /// <summary>Plotstil nach Name: «monochrom», «graustufen» (auch monochrome.ctb / grayscale.ctb, wenn die Datei fehlt) oder der Dateiname einer CTB in den Suchordnern; null = nicht gefunden.</summary>
    public static Plotstil? Finden(string name, string? zeichnungsOrdner)
    {
        name = name.Trim();
        if (name.Equals("monochrom", StringComparison.OrdinalIgnoreCase)) return Monochrom;
        if (name.Equals("graustufen", StringComparison.OrdinalIgnoreCase)) return Graustufen;
        var datei = Vorhandene(zeichnungsOrdner).FirstOrDefault(v => v.Datei is not null && v.Name.Equals(Path.GetFileName(name), StringComparison.OrdinalIgnoreCase)).Datei;
        if (datei is not null) return Lesen(datei);
        // die beiden Standardtabellen von AutoCAD haben eine eingebaute Entsprechung
        if (Path.GetFileName(name).Equals("monochrome.ctb", StringComparison.OrdinalIgnoreCase)) return Monochrom;
        if (Path.GetFileName(name).Equals("grayscale.ctb", StringComparison.OrdinalIgnoreCase)) return Graustufen;
        return null;
    }
}
