// Hilfen für die Tests der Quest App P4a: kleine GLB-Dateien (unabhängig vom Schreiber im Kern, nach Khronos glTF 2.0, «Binary glTF Layout») und
// Netze aus Quadern für Szenen, Durchgang und Räume.

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using Hlks.Xr.Modell;

namespace Hlks.Xr.Tests;

internal static class ModellHilfen
{
    /// <summary>Ein Netz für <see cref="Glb"/>: Positionen, Normalen (null = keine), Indizes, Farbe, alphaMode, extras des Netzes, Knoten-Transformation.</summary>
    public sealed record GlbTeil(float[] Pos, float[]? Nor, uint[] Ind, float[]? Farbe = null, string AlphaMode = "OPAQUE", JsonObject? Extras = null,
        string Name = "netz", bool Kurz = true, float[]? Matrix = null, float[]? Translation = null, bool OhneIndizes = false);

    /// <summary>Schreibt eine GLB mit einem Knoten je Netz (Wurzeln der Szene 0).</summary>
    public static byte[] Glb(IEnumerable<GlbTeil> teile, JsonObject? szeneExtras = null)
    {
        var bin = new MemoryStream();
        var acc = new JsonArray(); var views = new JsonArray(); var meshes = new JsonArray(); var mats = new JsonArray(); var nodes = new JsonArray();
        int View(byte[] d, int? ziel)
        {
            while (bin.Length % 4 != 0) bin.WriteByte(0);
            var v = new JsonObject { ["buffer"] = 0, ["byteOffset"] = bin.Length, ["byteLength"] = d.Length };
            if (ziel is int z) v["target"] = z;
            bin.Write(d);
            views.Add(v);
            return views.Count - 1;
        }
        int Acc(byte[] d, int typ, int anzahl, string art, int? ziel)
        {
            acc.Add(new JsonObject { ["bufferView"] = View(d, ziel), ["componentType"] = typ, ["count"] = anzahl, ["type"] = art });
            return acc.Count - 1;
        }
        foreach (var t in teile)
        {
            var attr = new JsonObject { ["POSITION"] = Acc(MemoryMarshal.AsBytes(t.Pos.AsSpan()).ToArray(), 5126, t.Pos.Length / 3, "VEC3", 34962) };
            if (t.Nor is not null) attr["NORMAL"] = Acc(MemoryMarshal.AsBytes(t.Nor.AsSpan()).ToArray(), 5126, t.Nor.Length / 3, "VEC3", 34962);
            var prim = new JsonObject { ["attributes"] = attr, ["mode"] = 4, ["material"] = mats.Count };
            if (!t.OhneIndizes)
            {
                prim["indices"] = t.Kurz
                    ? Acc(MemoryMarshal.AsBytes(t.Ind.Select(i => (ushort)i).ToArray().AsSpan()).ToArray(), 5123, t.Ind.Length, "SCALAR", 34963)
                    : Acc(MemoryMarshal.AsBytes(t.Ind.AsSpan()).ToArray(), 5125, t.Ind.Length, "SCALAR", 34963);
            }
            var f = t.Farbe ?? [1, 1, 1, 1];
            mats.Add(new JsonObject
            {
                ["pbrMetallicRoughness"] = new JsonObject { ["baseColorFactor"] = new JsonArray(f.Select(x => (JsonNode)JsonValue.Create((double)x)).ToArray()) },
                ["alphaMode"] = t.AlphaMode, ["doubleSided"] = true,
            });
            var mesh = new JsonObject { ["name"] = t.Name, ["primitives"] = new JsonArray(prim) };
            if (t.Extras is not null) mesh["extras"] = t.Extras.DeepClone();
            meshes.Add(mesh);
            var node = new JsonObject { ["mesh"] = meshes.Count - 1 };
            if (t.Matrix is not null) node["matrix"] = new JsonArray(t.Matrix.Select(x => (JsonNode)JsonValue.Create((double)x)).ToArray());
            if (t.Translation is not null) node["translation"] = new JsonArray(t.Translation.Select(x => (JsonNode)JsonValue.Create((double)x)).ToArray());
            nodes.Add(node);
        }
        var szene = new JsonObject { ["nodes"] = new JsonArray(Enumerable.Range(0, nodes.Count).Select(i => (JsonNode)JsonValue.Create(i)).ToArray()) };
        if (szeneExtras is not null) szene["extras"] = szeneExtras.DeepClone();
        while (bin.Length % 4 != 0) bin.WriteByte(0);
        var gltf = new JsonObject
        {
            ["asset"] = new JsonObject { ["version"] = "2.0" }, ["scene"] = 0, ["scenes"] = new JsonArray(szene), ["nodes"] = nodes, ["meshes"] = meshes,
            ["materials"] = mats, ["accessors"] = acc, ["bufferViews"] = views, ["buffers"] = new JsonArray(new JsonObject { ["byteLength"] = bin.Length }),
        };
        var json = Encoding.UTF8.GetBytes(gltf.ToJsonString());
        var jl = (json.Length + 3) / 4 * 4;
        var bl = (int)bin.Length;
        var aus = new byte[12 + 8 + jl + 8 + bl];
        BinaryPrimitives.WriteUInt32LittleEndian(aus, 0x46546C67);
        BinaryPrimitives.WriteUInt32LittleEndian(aus.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(aus.AsSpan(8), (uint)aus.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(aus.AsSpan(12), (uint)jl);
        BinaryPrimitives.WriteUInt32LittleEndian(aus.AsSpan(16), 0x4E4F534A);
        json.CopyTo(aus, 20);
        for (int i = 20 + json.Length; i < 20 + jl; i++) aus[i] = 0x20;
        BinaryPrimitives.WriteUInt32LittleEndian(aus.AsSpan(20 + jl), (uint)bl);
        BinaryPrimitives.WriteUInt32LittleEndian(aus.AsSpan(24 + jl), 0x004E4942);
        bin.ToArray().CopyTo(aus, 28 + jl);
        return aus;
    }

    /// <summary>Quader (achsparallel, Szene): 8 Ecken, 12 Dreiecke; Positionen und Indizes ab Ecke «basis».</summary>
    public static (List<float> Pos, List<uint> Ind) Quader(Vec3 min, Vec3 max, uint basis = 0)
    {
        var pos = new List<float>();
        for (int i = 0; i < 8; i++)
        {
            pos.Add((float)((i & 1) == 0 ? min.X : max.X));
            pos.Add((float)((i & 2) == 0 ? min.Y : max.Y));
            pos.Add((float)((i & 4) == 0 ? min.Z : max.Z));
        }
        uint[] f =
        [
            0, 2, 1, 1, 2, 3,   // z min
            4, 5, 6, 5, 7, 6,   // z max
            0, 1, 4, 1, 5, 4,   // y min (Unterseite)
            2, 6, 3, 3, 6, 7,   // y max
            0, 4, 2, 2, 4, 6,   // x min
            1, 3, 5, 3, 7, 5,   // x max
        ];
        return (pos, [.. f.Select(x => x + basis)]);
    }

    /// <summary>Netz aus mehreren Quadern (je ein Element) mit Bereichen; Gruppe und Art (Ebene bzw. Entität) je Element.</summary>
    public static GlbNetzDaten Quadernetz(string gruppe, params (string Id, string? Art, Vec3 Min, Vec3 Max)[] elemente)
    {
        var pos = new List<float>();
        var ind = new List<uint>();
        var bereiche = new List<ElementBereich>();
        foreach (var (id, art, min, max) in elemente)
        {
            var (p, i) = Quader(min, max, (uint)(pos.Count / 3));
            bereiche.Add(new ElementBereich(id, ind.Count / 3, i.Count / 3, art));
            pos.AddRange(p);
            ind.AddRange(i);
        }
        return new GlbNetzDaten
        {
            Name = gruppe, Positionen = [.. pos], Normalen = [.. Enumerable.Repeat(new[] { 0f, 1f, 0f }, pos.Count / 3).SelectMany(x => x)], Indizes = [.. ind],
            Gruppe = gruppe, Bereiche = bereiche, Deckkraft = gruppe == "raum" ? 0.15 : 1, Transparent = gruppe == "raum",
        };
    }

    /// <summary>Netz eines Raums mit beliebigem Grundriss (Dreiecke der Unterseite als Prisma: unten, oben und Seiten), Szene.</summary>
    public static GlbNetzDaten RaumNetz(string id, IReadOnlyList<Xz> ring, double boden, double hoehe)
    {
        var pos = new List<float>();
        var ind = new List<uint>();
        // Fächer-Triangulierung (für konvexe und die L-Form aus den Tests korrekt, wenn der Fächer vom einspringenden Eck ausgeht)
        int n = ring.Count;
        foreach (var y in new[] { boden, boden + hoehe })
            foreach (var p in ring) { pos.Add((float)p.X); pos.Add((float)y); pos.Add((float)p.Z); }
        for (int i = 1; i + 1 < n; i++) { ind.Add(0); ind.Add((uint)i); ind.Add((uint)(i + 1)); }                       // unten
        for (int i = 1; i + 1 < n; i++) { ind.Add((uint)n); ind.Add((uint)(n + i + 1)); ind.Add((uint)(n + i)); }      // oben
        for (int i = 0; i < n; i++)
        {
            uint a = (uint)i, b = (uint)((i + 1) % n);
            ind.Add(a); ind.Add(b); ind.Add((uint)(n + b));
            ind.Add(a); ind.Add((uint)(n + b)); ind.Add((uint)(n + a));
        }
        return new GlbNetzDaten
        {
            Name = "raum", Positionen = [.. pos], Normalen = [.. Enumerable.Repeat(new[] { 0f, 1f, 0f }, pos.Count / 3).SelectMany(x => x)], Indizes = [.. ind],
            Gruppe = "raum", Bereiche = [new ElementBereich(id, 0, ind.Count / 3, "raum")], Deckkraft = 0.15, Transparent = true,
        };
    }

    public static GlbDatei Datei(double[]? bezug, params GlbNetzDaten[] netze) => new() { Netze = netze, Bezugspunkt = bezug ?? [0, 0, 0] };
}
