// Revit-Dateien (RFA, RVT) ohne Revit lesen – Stufe 1 aus entwurf/rvt-format.md (Befund 03.11.2026).
// Schichten: OLE-Compound-File (MS-CFB) → Streams; komprimierte Streams sind in Blöcke zu 65 249 B geteilt, deren letzte 353 B
// Fehlerkorrektur-Prüfdaten sind (Revit CRCFile/CRCIO) → nach dem Entfernen gzip-Glieder (Revit GZIO, rohes Deflate), jedes mit
// CRC32 und Länge im Nachspann geprüft. Gelesen werden die unkomprimierten Metadaten: BasicFileInfo (Version), PartAtom
// (Familie, Kategorie, Parameter, Typen), RevitPreview4.0 (PNG), TransmissionData (externe Verweise). Elemente und Geometrie
// (Partitions) sind Revits eigene Serialisierung und werden nicht ausgelegt.

using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace Hlks.Kern;

/// <summary>Ein Parameter aus PartAtom: Name (wie in Revit angezeigt), Wert als Text, Datentyp («Länge», «Ja/Nein» …),
/// Einheit und Herkunft («system» = Revit-Parameter, «custom» = eigener).</summary>
public sealed record RevitParameter(string Name, string Wert, string? Datentyp, string? Einheit, string? Herkunft);

/// <summary>Ein Familientyp aus PartAtom mit seinen Typparametern.</summary>
public sealed record RevitTyp(string Name, IReadOnlyList<RevitParameter> Parameter);

/// <summary>Familienparameter einer Gruppe (PartAtom «Family Parameters»).</summary>
public sealed record RevitParameterGruppe(string Gruppe, IReadOnlyList<RevitParameter> Parameter);

/// <summary>Externer Verweis aus TransmissionData (Art, z. B. «Keynote Table», und gespeicherter Pfad).</summary>
public sealed record RevitVerweis(string Art, string? Pfad);

/// <summary>Was ohne Revit aus einer RFA/RVT lesbar ist.</summary>
public sealed class RevitInfo
{
    /// <summary>Revit-Version beim Speichern («2025»), aus BasicFileInfo; null, wenn nicht erkannt.</summary>
    public string? Version { get; init; }
    public string? Build { get; init; }
    /// <summary>Sprache beim Speichern («DEU»).</summary>
    public string? Sprache { get; init; }
    public bool? Teamarbeit { get; init; }
    /// <summary>Nur Familien (PartAtom): Name, Kategorie, Revit-Version laut PartAtom.</summary>
    public string? Familienname { get; init; }
    public string? Kategorie { get; init; }
    public IReadOnlyList<RevitParameterGruppe> Familienparameter { get; init; } = [];
    public IReadOnlyList<RevitTyp> Typen { get; init; } = [];
    public IReadOnlyList<RevitVerweis> Verweise { get; init; } = [];
    /// <summary>Vorschaubild als PNG (aus RevitPreview4.0) oder null.</summary>
    public byte[]? Vorschau { get; init; }
    /// <summary>Alle Stream-Pfade der Datei (z. B. «Global/Latest», «Partitions/0»).</summary>
    public IReadOnlyList<string> Streams { get; init; } = [];
}

/// <summary>Liest eine Revit-Datei (Compound File) nur lesend.</summary>
public sealed class RevitDatei
{
    /// <summary>Obergrenze der Dateigrösse (grosse RVT bis einige 100 MB; die Datei wird ganz gelesen).</summary>
    public const long MaxBytes = 1L << 30;
    /// <summary>Revit-Fehlerkorrektur: Blockgrösse und Prüfteil am Ende jedes vollen Blocks (gemessen an Revit 2017–2025).</summary>
    public const int Block = 65249, Pruefteil = 353;

    readonly byte[] d;
    readonly int sektor, miniSektor;
    readonly uint miniGrenze;
    readonly List<uint> fat = [], miniFat = [];
    readonly byte[] miniStrom;
    readonly Dictionary<string, (uint Start, long Groesse)> streams = new(StringComparer.Ordinal);

    const uint Ende = 0xFFFFFFFA;

    RevitDatei(byte[] daten)
    {
        d = daten;
        if (d.Length < 512 || BitConverter.ToUInt32(d, 0) != 0xE011CFD0 || BitConverter.ToUInt32(d, 4) != 0xE11AB1A1)
            throw new HlksFehler("Die Datei ist keine Revit-Datei (kein Compound File).", "RFA- oder RVT-Datei angeben.");
        sektor = 1 << U16(0x1E);
        miniSektor = 1 << U16(0x20);
        if (sektor is not (512 or 4096) || miniSektor != 64) throw new HlksFehler("Unbekannter Aufbau der Revit-Datei (Sektorgrösse).");
        uint nFat = U32(0x2C), dirStart = U32(0x30), miniFatStart = U32(0x3C), difat = U32(0x44);
        miniGrenze = U32(0x38);

        // DIFAT → FAT
        var fatSektoren = new List<uint>();
        for (int i = 0; i < 109; i++) { var s = U32(0x4C + i * 4); if (s < Ende) fatSektoren.Add(s); }
        for (int n = 0; difat < Ende && n < 1_000_000; n++)
        {
            int o = Off(difat);
            for (int i = 0; i < sektor / 4 - 1; i++) { var s = U32(o + i * 4); if (s < Ende) fatSektoren.Add(s); }
            difat = U32(o + sektor - 4);
        }
        foreach (var s in fatSektoren.Take((int)nFat))
        {
            int o = Off(s);
            for (int i = 0; i < sektor / 4; i++) fat.Add(U32(o + i * 4));
        }

        // Verzeichnis
        var dir = Kette(dirStart, fat, sektor, d, Off, long.MaxValue);
        var eintraege = new List<(string Name, byte Typ, uint Links, uint Rechts, uint Kind, uint Start, long Groesse)>();
        for (int i = 0; i + 128 <= dir.Length; i += 128)
        {
            int nl = BitConverter.ToUInt16(dir, i + 0x40);
            var name = nl > 2 ? Encoding.Unicode.GetString(dir, i, Math.Min(nl, 64) - 2) : "";
            eintraege.Add((name, dir[i + 0x42], BitConverter.ToUInt32(dir, i + 0x44), BitConverter.ToUInt32(dir, i + 0x48),
                BitConverter.ToUInt32(dir, i + 0x4C), BitConverter.ToUInt32(dir, i + 0x74), (long)BitConverter.ToUInt64(dir, i + 0x78)));
        }
        if (eintraege.Count == 0) throw new HlksFehler("Die Revit-Datei hat kein Verzeichnis.");
        var wurzel = eintraege[0];
        miniStrom = wurzel.Start < Ende ? Kette(wurzel.Start, fat, sektor, d, Off, wurzel.Groesse) : [];
        if (miniFatStart < Ende)
        {
            var mf = Kette(miniFatStart, fat, sektor, d, Off, long.MaxValue);
            for (int i = 0; i + 4 <= mf.Length; i += 4) miniFat.Add(BitConverter.ToUInt32(mf, i));
        }

        // Baum ablaufen (rot-schwarz; Tiefe begrenzt gegen kaputte Verweise)
        var besucht = new HashSet<uint>();
        void Ablaufen(uint idx, string praefix, int tiefe)
        {
            if (idx >= Ende || idx >= eintraege.Count || tiefe > 64 || !besucht.Add(idx)) return;
            var e = eintraege[(int)idx];
            Ablaufen(e.Links, praefix, tiefe + 1);
            Ablaufen(e.Rechts, praefix, tiefe + 1);
            var pfad = praefix.Length == 0 ? e.Name : praefix + "/" + e.Name;
            if (e.Typ == 2) streams[pfad] = (e.Start, e.Groesse);
            else if (e.Typ == 1) Ablaufen(e.Kind, pfad, tiefe + 1);
        }
        Ablaufen(wurzel.Kind, "", 0);
    }

    /// <summary>Öffnet eine Datei nur lesend (andere dürfen sie gleichzeitig offen haben, z. B. Revit).</summary>
    public static RevitDatei Oeffnen(string pfad)
    {
        using var fs = new FileStream(pfad, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (fs.Length > MaxBytes) throw new HlksFehler($"Die Revit-Datei ist grösser als {MaxBytes >> 20} MB.");
        var b = new byte[fs.Length];
        fs.ReadExactly(b);
        return new RevitDatei(b);
    }

    public static RevitDatei AusBytes(byte[] daten) => new(daten);

    public IReadOnlyCollection<string> StreamNamen => streams.Keys;

    /// <summary>Rohinhalt eines Streams oder null, wenn es ihn nicht gibt.</summary>
    public byte[]? Strom(string pfad)
    {
        if (!streams.TryGetValue(pfad, out var s)) return null;
        return s.Groesse < miniGrenze
            ? Kette(s.Start, miniFat, miniSektor, miniStrom, n => (int)(n * miniSektor), s.Groesse)
            : Kette(s.Start, fat, sektor, d, Off, s.Groesse);
    }

    /// <summary>Inhalt eines komprimierten Streams (Global/*, Formats/Latest, Partitions/*): Prüfteile der Blöcke entfernt,
    /// alle gzip-Glieder entpackt und gegen CRC32/Länge geprüft, aneinandergehängt. Null, wenn es den Stream nicht gibt.</summary>
    public byte[]? Entpackt(string pfad) => Strom(pfad) is { } roh ? Entpacken(Entblocken(roh)) : null;

    /// <summary>Klassenschema der Datei (Formats/Latest), Schlüssel zu den Objekten in Global/* und Partitions/*.</summary>
    public RevitSchema Schema() => RevitSchema.Lesen(Entpackt("Formats/Latest")
        ?? throw new HlksFehler("Die Revit-Datei hat kein Klassenschema (Formats/Latest fehlt)."));

    /// <summary>Entfernt aus jedem vollen Block die letzten <see cref="Pruefteil"/> Bytes (Revit-Fehlerkorrektur).</summary>
    public static byte[] Entblocken(byte[] roh)
    {
        if (roh.Length <= Block) return roh;
        using var ms = new MemoryStream(roh.Length);
        for (int o = 0; o < roh.Length; o += Block)
        {
            int n = Math.Min(Block, roh.Length - o);
            ms.Write(roh, o, n == Block ? n - Pruefteil : n);
        }
        return ms.ToArray();
    }

    static readonly byte[] GzipKopf = [0x1f, 0x8b, 0x08, 0x00, 0, 0, 0, 0, 0x00, 0x0b];

    /// <summary>Entpackt alle gzip-Glieder (fester Revit-Kopf) und prüft jedes gegen CRC32 und Länge im Nachspann.</summary>
    public static byte[] Entpacken(byte[] daten)
    {
        int i = daten.AsSpan().IndexOf(GzipKopf);
        if (i < 0 || i > 64) throw new HlksFehler("Der Stream ist nicht komprimiert (kein gzip-Kopf).");
        using var aus = new MemoryStream();
        int glied = 0;
        while (i >= 0)
        {
            glied++;
            var naechster = daten.AsSpan(i + GzipKopf.Length).IndexOf(GzipKopf);
            int ende = naechster < 0 ? daten.Length : i + GzipKopf.Length + naechster;
            byte[] u;
            using (var ds = new DeflateStream(new MemoryStream(daten, i + GzipKopf.Length, ende - i - GzipKopf.Length), CompressionMode.Decompress))
            using (var m = new MemoryStream()) { ds.CopyTo(m); u = m.ToArray(); }
            // Nachspann (CRC32, Länge) liegt direkt hinter dem Deflate-Ende: unter den Bytes vor dem nächsten Glied suchen
            uint crc = Crc32(u), laenge = (uint)u.Length;
            int t = -1;
            for (int k = ende - 8; k >= i + GzipKopf.Length; k--)
                if (BitConverter.ToUInt32(daten, k) == crc && BitConverter.ToUInt32(daten, k + 4) == laenge) { t = k; break; }
            if (t < 0) throw new HlksFehler($"gzip-Glied {glied} des Streams ist verfälscht (Prüfsumme stimmt nicht).",
                "Die Datei ist beschädigt oder hat einen unbekannten Aufbau (andere Blockgrösse der Revit-Fehlerkorrektur).");
            aus.Write(u);
            i = naechster < 0 ? -1 : ende;
        }
        return aus.ToArray();
    }

    // ---------- Metadaten ----------

    /// <summary>Liest Version, Familie (PartAtom), Vorschau und Verweise.</summary>
    public RevitInfo Info()
    {
        var bfi = Strom("BasicFileInfo") is { } b ? BasicFileInfo(b) : default;
        var (name, kategorie, gruppen, typen) = Strom("PartAtom") is { } pa ? PartAtom(pa) : (null, null, [], []);
        return new RevitInfo
        {
            Version = bfi.Version, Build = bfi.Build, Sprache = bfi.Sprache, Teamarbeit = bfi.Teamarbeit,
            Familienname = name, Kategorie = kategorie, Familienparameter = gruppen, Typen = typen,
            Verweise = Strom("TransmissionData") is { } td ? Verweise(td) : [],
            Vorschau = Strom("RevitPreview4.0") is { } pv ? Png(pv) : null,
            Streams = streams.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList(),
        };
    }

    static readonly System.Text.RegularExpressions.Regex AlteVersion =
        new(@"Autodesk Revit(?: MEP| Architecture| Structure)? (\d{4}) \(Build: ([^)]+\)?)\)", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>BasicFileInfo: Textblock in UTF-16BE (ab Revit 2019 gesehen) mit Zeilen «Format: 2025», «Build: …»; ältere Dateien
    /// (gesehen 2012, 2018) nur UTF-16LE mit «Autodesk Revit 2018 (Build: 20170223_1515(x64))».</summary>
    public static (string? Version, string? Build, string? Sprache, bool? Teamarbeit) BasicFileInfo(byte[] b)
    {
        var texte = new List<string>();
        foreach (var enc in new Encoding[] { Encoding.BigEndianUnicode, Encoding.Unicode })
            for (int start = 0; start < 2; start++) texte.Add(enc.GetString(b, start, (b.Length - start) & ~1));
        foreach (var t in texte)
        {
            var version = Zeile(t, "Format:");
            if (version is null) continue;
            return (version.Length >= 4 && version[..4].All(char.IsDigit) ? version[..4] : version, Zeile(t, "Build:"),
                Zeile(t, "Locale when saved:"), Teamarbeit(t));
        }
        foreach (var t in texte)
            if (AlteVersion.Match(t) is { Success: true } m)
                return (m.Groups[1].Value, m.Groups[2].Value, Zeile(t, "Locale when saved:"), Teamarbeit(t));
        return default;
    }

    static bool? Teamarbeit(string t) => Zeile(t, "Worksharing:") is { } tw ? !tw.StartsWith("Not enabled", StringComparison.OrdinalIgnoreCase) : null;

    static string? Zeile(string text, string marke)
    {
        int i = text.IndexOf(marke, StringComparison.Ordinal);
        if (i < 0) return null;
        int a = i + marke.Length, e = a;
        while (e < text.Length && text[e] is not ('\r' or '\n' or '\0')) e++;
        var w = text[a..e].Trim();
        return w.Length == 0 ? null : w;
    }

    static readonly XNamespace Atom = "http://www.w3.org/2005/Atom", Pa = "urn:schemas-autodesk-com:partatom";

    /// <summary>PartAtom (Atom-XML): Familienname, Kategorie, Familienparameter je Gruppe und Typen mit Typparametern.</summary>
    public static (string? Name, string? Kategorie, List<RevitParameterGruppe> Gruppen, List<RevitTyp> Typen) PartAtom(byte[] b)
    {
        XDocument x;
        try
        {
            using var ms = new MemoryStream(b);
            x = XDocument.Load(ms, LoadOptions.None);
        }
        catch (System.Xml.XmlException ex) { throw new HlksFehler($"PartAtom der Familie ist kein gültiges XML: {ex.Message}"); }
        var wurzel = x.Root!;
        var name = (string?)wurzel.Element(Atom + "title");
        var kategorie = wurzel.Elements(Atom + "category")
            .FirstOrDefault(c => ((string?)c.Element(Atom + "scheme"))?.Contains("revit", StringComparison.OrdinalIgnoreCase) == true)
            ?.Element(Atom + "term")?.Value;
        var gruppen = new List<RevitParameterGruppe>();
        foreach (var g in wurzel.Descendants(Pa + "feature").Elements(Pa + "group"))
        {
            var ps = ParameterVon(g, ausgenommen: [Pa + "title"]);
            if (ps.Count > 0) gruppen.Add(new RevitParameterGruppe((string?)g.Element(Pa + "title") ?? "", ps));
        }
        var typen = wurzel.Descendants(Pa + "part")
            .Select(p => new RevitTyp((string?)p.Element(Atom + "title") ?? "", ParameterVon(p, ausgenommen: [Atom + "title"])))
            .ToList();
        return (name, kategorie, gruppen, typen);
    }

    static List<RevitParameter> ParameterVon(XElement e, XName[] ausgenommen) =>
        e.Elements().Where(p => !ausgenommen.Contains(p.Name) && p.Name.Namespace != Pa && !p.HasElements)
            .Select(p => new RevitParameter(
                (string?)p.Attribute("displayName") ?? p.Name.LocalName,
                p.Value.Trim(),
                (string?)p.Attribute("typeOfParameter"),
                (string?)p.Attribute("units"),
                (string?)p.Attribute("type")))
            .ToList();

    /// <summary>TransmissionData: 4 B Länge, dann UTF-16LE-XML mit ExternalFileReference.</summary>
    public static List<RevitVerweis> Verweise(byte[] b)
    {
        if (b.Length < 8) return [];
        try
        {
            var t = Encoding.Unicode.GetString(b, 4, (b.Length - 4) & ~1).TrimEnd('\0');
            var x = XDocument.Parse(t);
            return x.Descendants("ExternalFileReference")
                .Select(r => new RevitVerweis((string?)r.Element("ExternalFileReferenceType") ?? "", Leer((string?)r.Element("LastSavedPath"))))
                .ToList();
        }
        catch (System.Xml.XmlException) { return []; }
    }

    static string? Leer(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    static readonly byte[] PngSignatur = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>PNG aus RevitPreview4.0 (serialisiertes Bild, PNG ab Signatur bis IEND).</summary>
    public static byte[]? Png(byte[] b)
    {
        int a = b.AsSpan().IndexOf(PngSignatur);
        if (a < 0) return null;
        int e = b.AsSpan(a).IndexOf("IEND"u8);
        return e < 0 ? null : b[a..(a + e + 8)];   // «IEND» + CRC
    }

    // ---------- Compound File ----------

    int Off(uint s) => (int)((s + 1L) * sektor);
    ushort U16(int o) => BitConverter.ToUInt16(d, o);
    uint U32(int o) => BitConverter.ToUInt32(d, o);

    static byte[] Kette(uint start, List<uint> tabelle, int groesse, byte[] quelle, Func<uint, int> off, long laenge)
    {
        using var ms = new MemoryStream();
        var gesehen = new HashSet<uint>();
        for (uint s = start; s < Ende && ms.Length < laenge; s = s < tabelle.Count ? tabelle[(int)s] : Ende)
        {
            if (!gesehen.Add(s)) throw new HlksFehler("Die Revit-Datei ist beschädigt (Sektorkette mit Schleife).");
            int o = off(s);
            if (o < 0 || o + groesse > quelle.Length)
            {
                if (o >= 0 && o < quelle.Length) { ms.Write(quelle, o, quelle.Length - o); break; }
                throw new HlksFehler("Die Revit-Datei ist beschädigt oder unvollständig (Sektor ausserhalb der Datei).");
            }
            ms.Write(quelle, o, groesse);
        }
        var r = ms.ToArray();
        return laenge < r.Length ? r[..(int)laenge] : r;
    }

    static readonly uint[] CrcTabelle = Enumerable.Range(0, 256).Select(n =>
    {
        uint c = (uint)n;
        for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
        return c;
    }).ToArray();

    static uint Crc32(byte[] b)
    {
        uint c = 0xFFFFFFFFu;
        foreach (var x in b) c = CrcTabelle[(c ^ x) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }
}
