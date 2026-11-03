// Objekte einer Revit-Datei lesen (Stufe 2 aus entwurf/rvt-format.md) – nach Revits AArcObj.cpp/DataDict.cpp (Utility.dll,
// dekompiliert 03.11.2026) und an echten Dateien geprüft (Revit 2017: 271 644 von 271 644 Sätzen, 2025: 26 182 von 26 184).
//   Partition (entpackt): Abschnitte im Wechsel – Köpfe [Id][u32 Länge][Daten][u32 Länge] und Körper
//            [Id][u32 Adler32][u32 Länge][Daten][u32 Länge]; Id u64 (neuere Dateien) bzw. u32 (Revit 2017).
//   Objekt:  u16 Klassen-Tag (wie im Schema), dann die Felder der Elternklassen, dann die eigenen, in Schemareihenfolge.
//            Grundtypen roh (Little Endian); Array: feste Anzahl aus dem Schema; Collection/String: u32 Anzahl + Elemente;
//            eingebettete Klasse: ihre Felder ohne Tag; Zeiger: u32 (0 = leer) – Besitzer- und Ref-Zeiger mit u16 Tag,
//            das Objekt selbst folgt erst nach dem Hauptobjekt (Warteschlange); Verweis-Zeiger (Art 3) nur die Nummer.
// Nicht gelesen: Extensible Storage (Objekte mit Schema-GUID statt Tag, Layout nicht im Klassenschema) – der Satz gilt dann
// als unvollständig, die Felder bis dorthin bleiben erhalten.

using System.Text;

namespace Hlks.Kern;

/// <summary>Ein gelesenes Objekt: Klasse und Feldwerte (Elternfelder zuerst). Werte: null, bool, long, double, string,
/// Guid, <see cref="RevitObjekt"/>, <see cref="RevitObjektVerweis"/>, <see cref="RevitKlassenVerweis"/> oder List&lt;object?&gt;.</summary>
public sealed class RevitObjekt(RevitKlasse klasse)
{
    public RevitKlasse Klasse { get; } = klasse;
    public List<KeyValuePair<string, object?>> Felder { get; } = [];

    /// <summary>Wert des ersten Felds mit diesem Namen (auch aus Elternklassen) oder null.</summary>
    public object? this[string name] => Felder.FirstOrDefault(f => f.Key == name).Value;

    /// <summary>Element-Id, wenn das Objekt eine ElementId ist (ElementId → Identifier → m_id64 bzw. m_id).</summary>
    public long? AlsId() => Klasse.Name == "ElementId" && this["m_id"] is RevitObjekt i ? i.Felder.Select(f => f.Value).OfType<long>().Cast<long?>().FirstOrDefault() : null;
}

/// <summary>Verweis-Zeiger auf ein anderes Objekt (Objektnummer im Archiv).</summary>
public readonly record struct RevitObjektVerweis(uint Nummer);

/// <summary>Klassenreferenz (ARuntimeClassRef) als Tag mit Klassenname.</summary>
public readonly record struct RevitKlassenVerweis(int Tag, string? Name);

/// <summary>Ein Satz einer Partition: Element-Id, Kopf oder Körper, das gelesene Objekt und ob er vollständig gelesen ist.</summary>
public sealed record RevitSatz(string Partition, long Id, bool Kopf, RevitObjekt? Objekt, bool Vollstaendig, string? Fehler);

public static class RevitObjekte
{
    static readonly int[] Groesse = [0, 1, 1, 2, 4, 4, 4, 8, 2, 16, 2, 8];   // nach Grundtyp 1–11

    /// <summary>Alle Sätze aller Partitionen der Datei, nacheinander (nicht alle auf einmal im Speicher).</summary>
    public static IEnumerable<RevitSatz> Saetze(RevitDatei datei, RevitSchema schema)
    {
        foreach (var name in datei.StreamNamen.Where(n => n.StartsWith("Partitions/", StringComparison.Ordinal)).OrderBy(n => n, StringComparer.Ordinal))
            foreach (var s in Saetze(name, datei.Entpackt(name)!, schema)) yield return s;
    }

    /// <summary>Sätze eines entpackten Partitionsstreams.</summary>
    public static IEnumerable<RevitSatz> Saetze(string partition, byte[] d, RevitSchema schema)
    {
        int idB = Passt(d, 8) ? 8 : Passt(d, 4) ? 4 : 0;
        if (idB == 0) yield break;
        int p = 0, leer = 0;
        for (int runde = 0; p < d.Length && leer < 2; runde++)
        {
            bool koerper = runde % 2 == 1;
            int n = 0;
            while (p + idB + 8 <= d.Length)
            {
                long id = idB == 8 ? BitConverter.ToInt64(d, p) : BitConverter.ToUInt32(d, p);
                int k = p + idB + (koerper ? 4 : 0);
                if (k + 4 > d.Length) break;
                uint len = BitConverter.ToUInt32(d, k);
                if (k + 8L + len > d.Length || BitConverter.ToUInt32(d, k + 4 + (int)len) != len) break;
                var (obj, ok, fehler) = Lesen(d, k + 4, (int)len, schema);
                yield return new RevitSatz(partition, id, !koerper, obj, ok, fehler);
                p = k + 8 + (int)len;
                n++;
            }
            leer = n == 0 ? leer + 1 : 0;
        }
    }

    static bool Passt(byte[] d, int idB)
    {
        if (d.Length < idB + 8) return false;
        uint l = BitConverter.ToUInt32(d, idB);
        return idB + 8L + l <= d.Length && BitConverter.ToUInt32(d, idB + 4 + (int)l) == l;
    }

    /// <summary>Liest ein Objekt (Satzdaten ab <paramref name="start"/>, Länge <paramref name="len"/>) samt Warteschlange.</summary>
    public static (RevitObjekt? Objekt, bool Vollstaendig, string? Fehler) Lesen(byte[] d, int start, int len, RevitSchema schema)
    {
        if (len < 2) return (null, len == 0, len == 0 ? null : "Satz zu kurz");
        var l = new Leser(d, start, start + len, schema);
        RevitObjekt? haupt = null;
        try
        {
            haupt = l.NeuesObjekt(l.U16());
            l.Felder(haupt, 0);
            while (l.Warteschlange.Count > 0) l.Felder(l.Warteschlange.Dequeue(), 0);
            return l.P == start + len ? (haupt, true, null) : (haupt, false, $"{l.P - start} von {len} Bytes gelesen");
        }
        catch (Exception ex) when (ex is RevitLeseFehler or ArgumentOutOfRangeException or IndexOutOfRangeException)
        {
            return (haupt, false, ex.Message);
        }
    }

    sealed class RevitLeseFehler(string text) : Exception(text);

    /// <summary>Anfang der Meldung, wenn ein Satz an Extensible Storage endet (bekannte Lücke).</summary>
    public const string EsFehler = "Extensible Storage wird nicht gelesen";

    sealed class Leser(byte[] d, int p, int ende, RevitSchema s)
    {
        public int P => p;
        public Queue<RevitObjekt> Warteschlange { get; } = new();

        void Brauche(int n) { if (p + n > ende) throw new RevitLeseFehler($"Satz zu kurz (Byte {p})"); }
        public ushort U16() { Brauche(2); var v = BitConverter.ToUInt16(d, p); p += 2; return v; }
        uint U32() { Brauche(4); var v = BitConverter.ToUInt32(d, p); p += 4; return v; }

        public RevitObjekt NeuesObjekt(int tag) =>
            new(s.Klasse(tag) ?? throw new RevitLeseFehler($"unbekannter Klassen-Tag {tag} (Byte {p})"));

        public void Felder(RevitObjekt o, int tiefe)
        {
            if (tiefe > 64) throw new RevitLeseFehler("Objekte zu tief verschachtelt");
            FelderDer(o.Klasse, o, tiefe);
        }

        void FelderDer(RevitKlasse k, RevitObjekt o, int tiefe)
        {
            if (s.Eltern(k) is { } e) FelderDer(e, o, tiefe);
            foreach (var f in k.Felder)
            {
                if (k.Name == "ESEntity" && f.Name == "m_blob" && f.Typ.Zeiger != 0)
                {
                    // Extensible Storage: statt Klassen-Tag eine Schema-GUID; Aufbau steht in ESSchema-Elementen, nicht im Klassenschema
                    if (U32() == 0) { o.Felder.Add(new(f.Name, null)); continue; }
                    Brauche(16);
                    throw new RevitLeseFehler($"{EsFehler} (Schema {new Guid(d.AsSpan(p, 16))})");
                }
                o.Felder.Add(new(f.Name, Wert(f.Typ, tiefe)));
            }
        }

        object? Wert(RevitFeldtyp t, int tiefe)
        {
            switch (t.Sammlung)
            {
                case 0: return Element(t, tiefe);
                case 1:
                {
                    var l = new List<object?>((int)Math.Min(t.Anzahl ?? 0, 4096));
                    for (uint i = 0; i < t.Anzahl; i++) l.Add(Element(t, tiefe));
                    return l;
                }
                case 5 or 6:
                {
                    uint n = U32();
                    if (n > ende - p) throw new RevitLeseFehler($"Anzahl {n} grösser als der Rest des Satzes (Byte {p - 4})");
                    if (t.Sammlung == 6 && t.Unter is null && t.Grundtyp is 8 or 2)   // Zeichenkette
                    {
                        int bytes = (int)n * (t.Grundtyp == 8 ? 2 : 1);
                        Brauche(bytes);
                        var text = t.Grundtyp == 8 ? Encoding.Unicode.GetString(d, p, bytes) : Encoding.Latin1.GetString(d, p, bytes);
                        p += bytes;
                        return text;
                    }
                    var l = new List<object?>((int)Math.Min(n, 4096));
                    for (uint i = 0; i < n; i++) l.Add(Element(t, tiefe));
                    return l;
                }
                default: throw new RevitLeseFehler($"unbekannte Sammlungsart {t.Sammlung}");
            }
        }

        object? Element(RevitFeldtyp t, int tiefe)
        {
            if (t.Unter is not null) return Wert(t.Unter, tiefe);   // typedef: Untertyp samt eigener Sammlung
            if (t.Zeiger != 0)
            {
                uint r = U32();
                if (r == 0) return null;
                if (t.Zeiger == 3) return new RevitObjektVerweis(r);
                var o = NeuesObjekt(U16());
                Warteschlange.Enqueue(o);   // Inhalt folgt nach dem Hauptobjekt
                return o;
            }
            if (t.Grundtyp == 14)
            {
                var o = new RevitObjekt(s.Klasse(t.KlasseTag ?? -1) ?? throw new RevitLeseFehler("eingebettete Klasse ohne Schema"));
                Felder(o, tiefe + 1);
                return o;
            }
            if (t.Grundtyp is < 1 or > 11) throw new RevitLeseFehler($"unbekannter Grundtyp {t.Grundtyp}");
            int g = Groesse[t.Grundtyp];
            Brauche(g);
            object? v = t.Grundtyp switch
            {
                1 => d[p] != 0,
                2 => (long)d[p],
                3 => (long)BitConverter.ToInt16(d, p),
                4 or 5 => (long)BitConverter.ToInt32(d, p),
                6 => (double)BitConverter.ToSingle(d, p),
                7 => BitConverter.ToDouble(d, p),
                8 => (long)BitConverter.ToUInt16(d, p),
                9 => new Guid(d.AsSpan(p, 16)),
                10 => new RevitKlassenVerweis(BitConverter.ToUInt16(d, p), s.Klasse(BitConverter.ToUInt16(d, p))?.Name),
                _ => BitConverter.ToInt64(d, p),
            };
            p += g;
            return v;
        }
    }
}
