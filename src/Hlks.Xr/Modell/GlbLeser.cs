// Quest App P4a (Modul XR): GLB lesen, wie KICAD sie schreibt (P2, src/Hlks.Kern/Xr/GlbSchreiber.cs und XrModell.cs) – ohne StereoKit.
//
// Warum ein eigener Leser statt Model.FromMemory von StereoKit (Befund P2, OBERFLAECHE.md «Quest App P2»):
//   - StereoKit reicht die extras nicht durch; die App braucht aber die Zuordnung Dreieck → Element (mesh.extras.kicad.elemente), die Gruppe, die Ebene
//     bzw. IFC-Entität und den Bezugspunkt (scene.extras.kicad.bezugspunktMm) – für den Durchgang (Wände unter 500 mm), die Räume als Startpunkt und das
//     Ausblenden der Gruppe «raum».
//   - StereoKit 0.3.11 setzt beim Laden eine Drehung um 180° um die Hochachse in die Knoten. Wer die Netze selbst aus den Puffern baut, hat diese Drehung
//     nicht: die Koordinaten bleiben die der GLB (Meter, y oben, x Ost, z −Nord, relativ zum Bezugspunkt), wie die Regeln in Hlks.Xr sie erwarten.
// Gelesen wird der Ausschnitt von glTF 2.0, den KICAD schreibt, und etwas mehr (Khronos glTF 2.0, «Binary glTF Layout», «Accessors»): Dreiecke
// (mode 4 oder ohne mode), POSITION und NORMAL als FLOAT VEC3 (auch mit byteStride), Indizes UNSIGNED_BYTE/SHORT/INT oder keine, Knoten mit matrix bzw.
// translation/rotation/scale und Kindern, Material mit baseColorFactor (linear), alphaMode und doubleSided. Fehlen Normalen, werden sie je Fläche
// gemittelt. Alles andere (Texturen, Morph-Ziele, Häute, Sparse-Accessoren) wird abgelehnt bzw. übergangen.

using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hlks.Xr.Modell;

/// <summary>Zusammenhängende Dreiecke eines Elements in einem Netz: Dreiecke [Ab, Ab + Anzahl). Art = Ebene (Modell) bzw. IFC-Entität (Unterlage).</summary>
public readonly record struct ElementBereich(string Id, int Ab, int Anzahl, string? Art);

/// <summary>Ein Dreiecksnetz der GLB in Szenen-Koordinaten (m), mit Darstellung und Zuordnung zu Elementen.</summary>
public sealed class GlbNetzDaten
{
    public required string Name { get; init; }
    /// <summary>x, y, z je Ecke.</summary>
    public required float[] Positionen { get; init; }
    /// <summary>x, y, z je Ecke (Einheitsvektoren).</summary>
    public required float[] Normalen { get; init; }
    /// <summary>drei Ecken je Dreieck.</summary>
    public required uint[] Indizes { get; init; }
    /// <summary>Grundfarbe linear (r, g, b, a) aus baseColorFactor.</summary>
    public float[] Farbe { get; init; } = [1, 1, 1, 1];
    /// <summary>alphaMode BLEND.</summary>
    public bool Transparent { get; init; }
    public bool Doppelseitig { get; init; }
    /// <summary>Gruppe aus extras.kicad (architektur, raum, hlks, sonst, unterlage); ohne Angabe «sonst».</summary>
    public string Gruppe { get; init; } = "sonst";
    /// <summary>Deckkraft in VR (extras.kicad.deckkraft, sonst Alpha der Farbe).</summary>
    public double Deckkraft { get; init; } = 1;
    /// <summary>Deckkraft der 3D-Übersicht (extras.kicad.deckkraft3d).</summary>
    public double? Deckkraft3d { get; init; }
    /// <summary>Farbe #rrggbb (sRGB) aus extras.kicad.farbe.</summary>
    public string? FarbeHex { get; init; }
    public IReadOnlyList<ElementBereich> Bereiche { get; init; } = [];
    public int Ecken => Positionen.Length / 3;
    public int Dreiecke => Indizes.Length / 3;

    public Vec3 Ecke(uint i) => new(Positionen[3 * i], Positionen[3 * i + 1], Positionen[3 * i + 2]);
}

/// <summary>Eine gelesene GLB: Netze, extras der Szene (kicad), Bezugspunkt, Teil und Geschoss.</summary>
public sealed class GlbDatei
{
    public required IReadOnlyList<GlbNetzDaten> Netze { get; init; }
    /// <summary>scene.extras.kicad (oder null).</summary>
    public JsonObject? Kicad { get; init; }
    /// <summary>Bezugspunkt in KICAD-mm (scene.extras.kicad.bezugspunktMm), sonst 0, 0, 0.</summary>
    public double[] Bezugspunkt { get; init; } = [0, 0, 0];
    public string? Teil { get; init; }
    public string? GeschossId { get; init; }
    public string? GeschossKennzeichen { get; init; }
    public long Dreiecke => Netze.Sum(n => (long)n.Dreiecke);
}

public static class GlbLeser
{
    const uint Magic = 0x46546C67, TypJson = 0x4E4F534A, TypBin = 0x004E4942;
    const int Float = 5126, UnsignedByte = 5121, UnsignedShort = 5123, UnsignedInt = 5125;

    static GlbFehler F(string m) => new("GLB: " + m);

    /// <summary>Liest die GLB. Wirft <see cref="GlbFehler"/> bei allem, was nicht gelesen werden kann.</summary>
    public static GlbDatei Lesen(byte[] daten)
    {
        var d = daten.AsSpan();
        if (d.Length < 20) throw F("zu kurz");
        if (BinaryPrimitives.ReadUInt32LittleEndian(d) != Magic) throw F("keine GLB (Kennung «glTF» fehlt)");
        if (BinaryPrimitives.ReadUInt32LittleEndian(d[4..]) != 2) throw F("nur glTF 2 wird gelesen");
        var laenge = BinaryPrimitives.ReadUInt32LittleEndian(d[8..]);
        if (laenge > d.Length) throw F("Länge im Kopf grösser als die Datei");
        int pos = 12;
        string? json = null;
        int binAb = -1, binLaenge = 0;
        while (pos + 8 <= laenge)
        {
            var cl = (int)BinaryPrimitives.ReadUInt32LittleEndian(d[pos..]);
            var typ = BinaryPrimitives.ReadUInt32LittleEndian(d[(pos + 4)..]);
            if (cl < 0 || pos + 8 + (long)cl > laenge) throw F("Chunk über das Ende hinaus");
            if (typ == TypJson && json is null) json = Encoding.UTF8.GetString(d.Slice(pos + 8, cl));
            else if (typ == TypBin && binAb < 0) { binAb = pos + 8; binLaenge = cl; }
            pos += 8 + cl;
        }
        if (json is null) throw F("JSON-Chunk fehlt");
        JsonObject g;
        try { g = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { MaxDepth = 64 }) as JsonObject ?? throw F("JSON ist kein Objekt"); }
        catch (JsonException) { throw F("JSON ungültig"); }
        var bin = binAb >= 0 ? new ReadOnlyMemory<byte>(daten, binAb, binLaenge) : ReadOnlyMemory<byte>.Empty;

        var szenen = g["scenes"] as JsonArray;
        var szeneNr = Ganz(g["scene"]) ?? 0;
        var szene = szenen is not null && szeneNr < szenen.Count ? szenen[szeneNr] as JsonObject : null;
        var kicad = szene?["extras"]?["kicad"] as JsonObject;
        var bezug = new double[3];
        if (kicad?["bezugspunktMm"] is JsonArray b && b.Count == 3)
            for (int i = 0; i < 3; i++) bezug[i] = b[i] is JsonValue v && v.TryGetValue<double>(out var x) && double.IsFinite(x) ? x : 0;
        var geschoss = kicad?["geschoss"] as JsonObject;

        var netze = new List<GlbNetzDaten>();
        var knoten = g["nodes"] as JsonArray ?? [];
        var wurzeln = szene?["nodes"] as JsonArray;
        IEnumerable<int> Start() => wurzeln is not null ? wurzeln.Select(x => Ganz(x) ?? -1) : Enumerable.Range(0, knoten.Count);
        var besucht = new HashSet<int>();
        foreach (var w in Start()) Knoten(w, Matrix4x4.Identity);

        void Knoten(int nr, Matrix4x4 eltern)
        {
            if (nr < 0 || nr >= knoten.Count || !besucht.Add(nr)) return;   // ungültig oder Kreis
            if (knoten[nr] is not JsonObject k) return;
            var m = Lokal(k) * eltern;
            if (Ganz(k["mesh"]) is int mi && g["meshes"] is JsonArray ms && mi < ms.Count && ms[mi] is JsonObject mesh)
                netze.AddRange(Netz(g, mesh, bin, m));
            if (k["children"] is JsonArray kinder) foreach (var c in kinder) Knoten(Ganz(c) ?? -1, m);
        }

        return new GlbDatei
        {
            Netze = netze, Kicad = kicad, Bezugspunkt = bezug,
            Teil = Text(kicad?["teil"]), GeschossId = Text(geschoss?["id"]), GeschossKennzeichen = Text(geschoss?["kennzeichen"]),
        };
    }

    static Matrix4x4 Lokal(JsonObject k)
    {
        if (k["matrix"] is JsonArray a && a.Count == 16)
        {
            var w = a.Select(x => (float)(Zahl(x) ?? 0)).ToArray();
            // glTF: Spalten hintereinander (column-major), Spaltenvektoren; System.Numerics: Zeilenvektoren – dieselben 16 Zahlen in Zeilenreihenfolge
            return new Matrix4x4(w[0], w[1], w[2], w[3], w[4], w[5], w[6], w[7], w[8], w[9], w[10], w[11], w[12], w[13], w[14], w[15]);
        }
        var s = k["scale"] is JsonArray sa && sa.Count == 3 ? new Vector3((float)(Zahl(sa[0]) ?? 1), (float)(Zahl(sa[1]) ?? 1), (float)(Zahl(sa[2]) ?? 1)) : Vector3.One;
        var r = k["rotation"] is JsonArray ra && ra.Count == 4
            ? new Quaternion((float)(Zahl(ra[0]) ?? 0), (float)(Zahl(ra[1]) ?? 0), (float)(Zahl(ra[2]) ?? 0), (float)(Zahl(ra[3]) ?? 1))
            : Quaternion.Identity;
        var t = k["translation"] is JsonArray ta && ta.Count == 3 ? new Vector3((float)(Zahl(ta[0]) ?? 0), (float)(Zahl(ta[1]) ?? 0), (float)(Zahl(ta[2]) ?? 0)) : Vector3.Zero;
        return Matrix4x4.CreateScale(s) * Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(r == default ? Quaternion.Identity : r)) * Matrix4x4.CreateTranslation(t);
    }

    static IEnumerable<GlbNetzDaten> Netz(JsonObject g, JsonObject mesh, ReadOnlyMemory<byte> bin, Matrix4x4 m)
    {
        var name = Text(mesh["name"]) ?? "netz";
        var kicad = mesh["extras"]?["kicad"] as JsonObject;
        var prims = mesh["primitives"] as JsonArray ?? [];
        bool erste = true;
        foreach (var p in prims.OfType<JsonObject>())
        {
            var mode = Ganz(p["mode"]) ?? 4;
            if (mode != 4) continue;   // nur Dreiecke
            if (p["attributes"] is not JsonObject at || Ganz(at["POSITION"]) is not int posNr) continue;
            var pos = Vec3Lesen(g, posNr, bin);
            var nor = Ganz(at["NORMAL"]) is int nNr ? Vec3Lesen(g, nNr, bin) : null;
            var ind = Ganz(p["indices"]) is int iNr ? IndizesLesen(g, iNr, bin) : [.. Enumerable.Range(0, pos.Length / 3).Select(i => (uint)i)];
            if (ind.Length % 3 != 0) throw F($"Netz «{name}»: Indizes sind kein Vielfaches von 3");
            var ecken = (uint)(pos.Length / 3);
            foreach (var i in ind) if (i >= ecken) throw F($"Netz «{name}»: Index {i} ausserhalb von {ecken} Ecken");
            if (nor is null || nor.Length != pos.Length) nor = NormalenRechnen(pos, ind);
            if (!m.IsIdentity) Transformieren(pos, nor, m);

            float[] farbe = [1, 1, 1, 1];
            bool transparent = false, doppelt = false;
            if (Ganz(p["material"]) is int matNr && g["materials"] is JsonArray mats && matNr < mats.Count && mats[matNr] is JsonObject mat)
            {
                if (mat["pbrMetallicRoughness"]?["baseColorFactor"] is JsonArray bc && bc.Count == 4)
                    farbe = [.. bc.Select(x => (float)Math.Clamp(Zahl(x) ?? 1, 0, 1))];
                transparent = Text(mat["alphaMode"]) == "BLEND";
                doppelt = mat["doubleSided"] is JsonValue dv && dv.TryGetValue<bool>(out var ds) && ds;
            }
            var bereiche = erste && kicad?["elemente"] is JsonObject el ? Bereiche(el, ind.Length / 3) : [];
            yield return new GlbNetzDaten
            {
                Name = name, Positionen = pos, Normalen = nor, Indizes = ind, Farbe = farbe, Transparent = transparent, Doppelseitig = doppelt,
                Gruppe = Text(kicad?["gruppe"]) ?? "sonst",
                Deckkraft = Zahl(kicad?["deckkraft"]) is double dk ? Math.Clamp(dk, 0, 1) : farbe[3],
                Deckkraft3d = Zahl(kicad?["deckkraft3d"]), FarbeHex = Text(kicad?["farbe"]), Bereiche = bereiche,
            };
            erste = false;
        }
    }

    /// <summary>Spalten { ids, ab, anzahl, ebenen | entitaeten } → Bereiche; ungültige Bereiche (ausserhalb der Dreiecke) fallen weg.</summary>
    static List<ElementBereich> Bereiche(JsonObject el, int dreiecke)
    {
        var ids = el["ids"] as JsonArray;
        var ab = el["ab"] as JsonArray;
        var anzahl = el["anzahl"] as JsonArray;
        var art = (el["ebenen"] ?? el["entitaeten"]) as JsonArray;
        var aus = new List<ElementBereich>();
        if (ids is null || ab is null || anzahl is null || ids.Count != ab.Count || ids.Count != anzahl.Count) return aus;
        for (int i = 0; i < ids.Count; i++)
        {
            var id = Text(ids[i]);
            var a = Ganz(ab[i]);
            var n = Ganz(anzahl[i]);
            if (id is null || a is not int va || n is not int vn || va < 0 || vn <= 0 || (long)va + vn > dreiecke) continue;
            aus.Add(new ElementBereich(id, va, vn, art is not null && i < art.Count ? Text(art[i]) : null));
        }
        return aus;
    }

    static (int Offset, int Stride, int Count, int Typ, string Art) Accessor(JsonObject g, int nr, ReadOnlyMemory<byte> bin, out ReadOnlyMemory<byte> view)
    {
        if (g["accessors"] is not JsonArray acc || nr < 0 || nr >= acc.Count || acc[nr] is not JsonObject a) throw F($"Accessor {nr} fehlt");
        if (a["sparse"] is not null) throw F("sparse Accessoren werden nicht gelesen");
        var count = Ganz(a["count"]) ?? throw F($"Accessor {nr} ohne count");
        var typ = Ganz(a["componentType"]) ?? throw F($"Accessor {nr} ohne componentType");
        var art = Text(a["type"]) ?? throw F($"Accessor {nr} ohne type");
        var bvNr = Ganz(a["bufferView"]) ?? throw F($"Accessor {nr} ohne bufferView");
        if (g["bufferViews"] is not JsonArray bvs || bvNr >= bvs.Count || bvs[bvNr] is not JsonObject bv) throw F($"bufferView {bvNr} fehlt");
        if ((Ganz(bv["buffer"]) ?? 0) != 0) throw F("nur der Puffer der GLB (0) wird gelesen");
        var vOff = Ganz(bv["byteOffset"]) ?? 0;
        var vLen = Ganz(bv["byteLength"]) ?? throw F($"bufferView {bvNr} ohne byteLength");
        if (vOff < 0 || vLen < 0 || (long)vOff + vLen > bin.Length) throw F($"bufferView {bvNr} ausserhalb des Puffers");
        view = bin.Slice(vOff, vLen);
        return (Ganz(a["byteOffset"]) ?? 0, Ganz(bv["byteStride"]) ?? 0, count, typ, art);
    }

    static float[] Vec3Lesen(JsonObject g, int nr, ReadOnlyMemory<byte> bin)
    {
        var (off, stride, count, typ, art) = Accessor(g, nr, bin, out var view);
        if (typ != Float || art != "VEC3") throw F($"Accessor {nr}: erwartet FLOAT VEC3");
        if (stride == 0) stride = 12;
        if (stride < 12 || count < 0 || (count > 0 && off + (long)stride * (count - 1) + 12 > view.Length)) throw F($"Accessor {nr} ausserhalb der bufferView");
        var aus = new float[count * 3];
        var s = view.Span;
        for (int i = 0; i < count; i++)
        {
            var o = off + i * stride;
            aus[3 * i] = BinaryPrimitives.ReadSingleLittleEndian(s[o..]);
            aus[3 * i + 1] = BinaryPrimitives.ReadSingleLittleEndian(s[(o + 4)..]);
            aus[3 * i + 2] = BinaryPrimitives.ReadSingleLittleEndian(s[(o + 8)..]);
        }
        return aus;
    }

    static uint[] IndizesLesen(JsonObject g, int nr, ReadOnlyMemory<byte> bin)
    {
        var (off, stride, count, typ, art) = Accessor(g, nr, bin, out var view);
        if (art != "SCALAR") throw F($"Accessor {nr}: Indizes müssen SCALAR sein");
        int groesse = typ switch { UnsignedByte => 1, UnsignedShort => 2, UnsignedInt => 4, _ => throw F($"Accessor {nr}: Indextyp {typ} unbekannt") };
        if (stride == 0) stride = groesse;
        if (count < 0 || (count > 0 && off + (long)stride * (count - 1) + groesse > view.Length)) throw F($"Accessor {nr} ausserhalb der bufferView");
        var aus = new uint[count];
        var s = view.Span;
        for (int i = 0; i < count; i++)
        {
            var o = off + i * stride;
            aus[i] = groesse switch { 1 => s[o], 2 => BinaryPrimitives.ReadUInt16LittleEndian(s[o..]), _ => BinaryPrimitives.ReadUInt32LittleEndian(s[o..]) };
        }
        return aus;
    }

    /// <summary>Normalen je Ecke als Mittel der angrenzenden Flächen (nur wenn die GLB keine hat).</summary>
    static float[] NormalenRechnen(float[] p, uint[] ind)
    {
        var n = new float[p.Length];
        for (int t = 0; t < ind.Length; t += 3)
        {
            uint a = ind[t], b = ind[t + 1], c = ind[t + 2];
            var pa = new Vector3(p[3 * a], p[3 * a + 1], p[3 * a + 2]);
            var f = Vector3.Cross(new Vector3(p[3 * b], p[3 * b + 1], p[3 * b + 2]) - pa, new Vector3(p[3 * c], p[3 * c + 1], p[3 * c + 2]) - pa);
            foreach (var i in new[] { a, b, c }) { n[3 * i] += f.X; n[3 * i + 1] += f.Y; n[3 * i + 2] += f.Z; }
        }
        for (int i = 0; i < n.Length; i += 3)
        {
            var v = new Vector3(n[i], n[i + 1], n[i + 2]);
            var l = v.Length();
            v = l > 1e-12f ? v / l : Vector3.UnitY;
            n[i] = v.X; n[i + 1] = v.Y; n[i + 2] = v.Z;
        }
        return n;
    }

    static void Transformieren(float[] p, float[] n, Matrix4x4 m)
    {
        Matrix4x4.Invert(m, out var inv);
        var nm = Matrix4x4.Transpose(inv);
        for (int i = 0; i < p.Length; i += 3)
        {
            var v = Vector3.Transform(new Vector3(p[i], p[i + 1], p[i + 2]), m);
            p[i] = v.X; p[i + 1] = v.Y; p[i + 2] = v.Z;
            var w = Vector3.Normalize(Vector3.TransformNormal(new Vector3(n[i], n[i + 1], n[i + 2]), nm));
            n[i] = w.X; n[i + 1] = w.Y; n[i + 2] = w.Z;
        }
    }

    static int? Ganz(JsonNode? n) => n is JsonValue v && v.TryGetValue<int>(out var i) ? i : null;
    static double? Zahl(JsonNode? n) => n is JsonValue v && v.TryGetValue<double>(out var d) && double.IsFinite(d) ? d : null;
    static string? Text(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
}

/// <summary>Fehler beim Lesen des Modells (GLB).</summary>
public sealed class GlbFehler(string meldung) : Exception(meldung);
