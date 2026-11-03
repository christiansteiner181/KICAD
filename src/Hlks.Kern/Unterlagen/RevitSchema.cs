// Klassenschema einer Revit-Datei (Stream Formats/Latest, Stufe 2 aus entwurf/rvt-format.md). Aufbau nach Revits
// ArchiveClassMaps::loadAllClasses/loadNewClass/readADDTable/readADDEntry (Utility.dll, Revit 2026, dekompiliert 03.11.2026):
//   Klasse:  u16 Version (0) · u16 Namenlänge · Name · Elternklasse (Klassenverweis) · u32 Schema · u32 Feldzahl · Felder ·
//            u32 Anzahl Upgrade-GUIDs · je 16 B GUID. Ende der Liste: Version 0 und Namenlänge 0.
//   Verweis: u16 Tag; 0 = keine Klasse, Bit 15 gesetzt = neue Klasse folgt inline, sonst Tag einer schon geladenen Klasse.
//            Tags zählen in Ladereihenfolge ab 12 (die erste Klasse der Liste bekommt 12).
//   Feld:    u32 Namenlänge (1–63) · Name · u32 Feldtyp [· u32 Anzahl bei Feldarray] [· Untereintrag bei typedef]
//            [· Klassenverweis bei eingebetteter Klasse].
//   Feldtyp: Byte 0 Grundtyp (1 bool … 11 int64, 13 typedef, 14 class), Bits 8–11 Zeigerart, Bits 12–15 Sammlung.
// In den Partitionen stehen dieselben Tags vor den Objekten (z. B. ElementHeader), deshalb ist das Schema der Schlüssel zur
// Objektebene. Gemessen: Revit 2017 3879, 2018 4024, 2025 4600 Klassen, jeweils bis aufs Endzeichen gelesen.

using System.Text;

namespace Hlks.Kern;

/// <summary>Typ eines Felds wie in Revits FieldType.</summary>
public sealed record RevitFeldtyp(int Grundtyp, int Zeiger, int Sammlung, uint? Anzahl, RevitFeldtyp? Unter, int? KlasseTag)
{
    static readonly string[] Namen = ["?", "bool", "char", "short", "int", "long", "float", "double", "TCHAR", "GUID", "ARuntimeClassRef", "int64", "?", "typedef", "class"];
    public bool IstEingebettet => Grundtyp > 12 && Zeiger == 0;

    /// <summary>Beschreibung wie Revits FieldType::getDescription, mit Klassenname statt «class».</summary>
    public string Beschreibung(RevitSchema s)
    {
        var b = new StringBuilder();
        b.Append(Sammlung switch { 0 => "", 1 => $"array[{Anzahl}] of ", 5 => "collection of ", 6 => "string of ", _ => "unknown collection of " });
        b.Append(Zeiger switch { 0 => "", 1 or 2 => "owner of ", 3 => "pointer to ", 4 => "ref-counted pointer to ", _ => "unknown pointer to " });
        if (Unter is not null) b.Append(Unter.Beschreibung(s));
        else if (KlasseTag is int t && s.Klasse(t) is { } k) b.Append(k.Name);
        else b.Append(Grundtyp < Namen.Length ? Namen[Grundtyp] : "?");
        return b.ToString();
    }
}

public sealed record RevitFeld(string Name, RevitFeldtyp Typ);

public sealed record RevitKlasse(int Tag, string Name, int? ElternTag, uint Schema, IReadOnlyList<RevitFeld> Felder, int Upgrades);

/// <summary>Alle Klassen einer Datei, nach Tag und Name.</summary>
public sealed class RevitSchema
{
    /// <summary>Tag der ersten Klasse in Formats/Latest.</summary>
    public const int ErsterTag = 12;
    readonly List<RevitKlasse?> nachTag = [];
    readonly Dictionary<string, RevitKlasse> nachName = new(StringComparer.Ordinal);

    public IEnumerable<RevitKlasse> Klassen => nachTag.OfType<RevitKlasse>();
    public int Anzahl => nachName.Count;
    public RevitKlasse? Klasse(int tag) => tag >= 0 && tag < nachTag.Count ? nachTag[tag] : null;
    public RevitKlasse? Klasse(string name) => nachName.GetValueOrDefault(name);
    public RevitKlasse? Eltern(RevitKlasse k) => k.ElternTag is int t ? Klasse(t) : null;

    /// <summary>Felder einer Klasse samt geerbten, in Speicherreihenfolge (Elternklasse zuerst; Annahme nach dem Klassenaufbau).</summary>
    public IEnumerable<RevitFeld> AlleFelder(RevitKlasse k) => (Eltern(k) is { } e ? AlleFelder(e) : []).Concat(k.Felder);

    /// <summary>Zerlegt den entpackten Stream Formats/Latest.</summary>
    public static RevitSchema Lesen(byte[] d)
    {
        var s = new RevitSchema();
        for (int i = 0; i < ErsterTag; i++) s.nachTag.Add(null);
        int p = 0;
        ushort U16() { if (p + 2 > d.Length) throw Kurz(); var v = BitConverter.ToUInt16(d, p); p += 2; return v; }
        uint U32() { if (p + 4 > d.Length) throw Kurz(); var v = BitConverter.ToUInt32(d, p); p += 4; return v; }
        string Text(int n) { if (p + n > d.Length) throw Kurz(); var t = Encoding.Latin1.GetString(d, p, n); p += n; return t; }
        HlksFehler Kurz() => new("Das Klassenschema der Revit-Datei ist unvollständig (Formats/Latest zu kurz).");

        int? Verweis()
        {
            int tag = U16();
            if (tag == 0) return null;
            if ((tag & 0x8000) != 0)
            {
                var neu = NeueKlasse() ?? throw new HlksFehler($"Klassenschema: leere Klasse nach Tag {tag & 0x7fff} (Byte {p}).");
                if (neu.Tag != (tag & 0x7fff)) throw new HlksFehler($"Klassenschema: Tag {tag & 0x7fff} erwartet, {neu.Tag} vergeben (Byte {p}).");
                return neu.Tag;
            }
            // auch eine Klasse, die gerade gelesen wird (Selbstbezug, z. B. Baum aus Knoten derselben Klasse)
            if (tag < ErsterTag || tag >= s.nachTag.Count) throw new HlksFehler($"Klassenschema: unbekannter Klassen-Tag {tag} (Byte {p}).");
            return tag;
        }

        RevitKlasse? NeueKlasse()
        {
            int version = U16(), n = U16();
            if (n == 0) return null;
            if (version != 0) throw new HlksFehler($"Klassenschema: unbekannte Version {version} (Byte {p}).");
            int tag = s.nachTag.Count;
            s.nachTag.Add(null);   // Platz vor der Elternklasse belegen: die Klasse zählt vor ihren inline geladenen Eltern
            var name = Text(n);
            var eltern = Verweis();
            uint schema = U32(), nf = U32();
            var felder = new List<RevitFeld>((int)Math.Min(nf, 4096));
            for (uint i = 0; i < nf; i++) felder.Add(Feld());
            int ng = (int)U32();
            p += 16 * ng;
            var k = new RevitKlasse(tag, name, eltern, schema, felder, ng);
            s.nachTag[tag] = k;
            s.nachName.TryAdd(name, k);
            return k;
        }

        RevitFeld Feld()
        {
            uint n = U32();
            if (n is < 1 or > 63) throw new HlksFehler($"Klassenschema: Feldnamenlänge {n} (Byte {p - 4}).");
            return new RevitFeld(Text((int)n), Typ());
        }

        RevitFeldtyp Typ()
        {
            uint t = U32();
            int grund = (int)(t & 0xff), zeiger = (int)((t >> 8) & 0xf), sammlung = (int)((t >> 12) & 0xf);
            uint? anzahl = sammlung == 1 ? U32() : null;
            var unter = grund == 13 ? Feld().Typ : null;   // typedef: vollständiger Untereintrag (Name + Typ)
            var wirksam = unter ?? new RevitFeldtyp(grund, zeiger, sammlung, anzahl, null, null);
            // wie readADDEntry: nach dem Untereintrag gilt dessen Typ; ist er eine eingebettete Klasse, folgt ihr Verweis
            int? klasse = wirksam.IstEingebettet ? Verweis() : null;
            return new RevitFeldtyp(grund, zeiger, sammlung, anzahl, unter, klasse);
        }

        while (NeueKlasse() is not null) { }
        return s;
    }
}
