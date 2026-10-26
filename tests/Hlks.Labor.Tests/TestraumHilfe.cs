using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;

namespace Hlks.Labor.Tests;

/// <summary>Hilfen der Testraum-Tests (XR-Labor L1): Ordner der Kataloge dieser Arbeitskopie, Kopie zum Ändern.</summary>
static class TestraumHilfe
{
    /// <summary>kataloge/ dieser Arbeitskopie (über den Pfad der Quelldatei, nicht über den Bauordner: der liegt ausserhalb des Repositorys).</summary>
    public static string KatalogOrdner([CallerFilePath] string quelle = "")
    {
        for (var d = new DirectoryInfo(Path.GetDirectoryName(quelle)!); d is not null; d = d.Parent)
        {
            var k = Path.Combine(d.FullName, "kataloge");
            if (File.Exists(Path.Combine(k, "labor_testraum.json"))) return k;
        }
        throw new InvalidOperationException("kataloge/labor_testraum.json nicht gefunden (von " + quelle + ")");
    }

    public static string KatalogText() => File.ReadAllText(Path.Combine(KatalogOrdner(), TestraumKatalog.Datei), Encoding.UTF8);

    public static JsonObject KatalogJson() => (JsonObject)JsonNode.Parse(KatalogText())!;

    /// <summary>Kopie aller Kataloge in einen Ordner unter %TEMP% (zum Ändern); der Aufrufer löscht ihn.</summary>
    public static string Kopie()
    {
        var ziel = Path.Combine(Path.GetTempPath(), "hlks_testraum_" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(ziel);
        foreach (var f in Directory.GetFiles(KatalogOrdner(), "*.json")) File.Copy(f, Path.Combine(ziel, Path.GetFileName(f)));
        return ziel;
    }
}

/// <summary>
/// Unabhängiger Leser einer GLB (glTF 2.0 binär, Khronos «Binary glTF Layout», «Accessors») für die Testraum-Tests: liest die Datei selbst – ohne den GlbSchreiber
/// des Kerns und ohne den Leser der App – und prüft Kopf, Chunks, Ausrichtung, Verweise, Accessors (Grenzen, min/max gegen die Daten) und Indizes.
/// </summary>
sealed class TestraumGlb
{
    public sealed record Netz(string Name, float[] Pos, float[] Nor, uint[] Ind, JsonObject Material, JsonObject? Extras)
    {
        public int Dreiecke => Ind.Length / 3;
        public JsonObject Kicad => (JsonObject)Extras!["kicad"]!;
    }

    public JsonObject Gltf { get; }
    public List<Netz> Netze { get; } = [];
    public JsonObject Szene => (JsonObject)Gltf["scenes"]![(int)Gltf["scene"]!]!["extras"]!["kicad"]!;
    public JsonObject Labor => (JsonObject)Szene["labor"]!;

    static void Wahr(bool b, string m) { if (!b) throw new Xunit.Sdk.XunitException("GLB ungültig: " + m); }

    TestraumGlb(JsonObject g) { Gltf = g; }

    public static TestraumGlb Lesen(byte[] d)
    {
        Wahr(d.Length >= 28 && Encoding.ASCII.GetString(d, 0, 4) == "glTF", "Kennung");
        Wahr(BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(4)) == 2, "Fassung 2");
        Wahr(BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(8)) == d.Length && d.Length % 4 == 0, "Länge");
        int jl = (int)BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(12));
        Wahr(Encoding.ASCII.GetString(d, 16, 4) == "JSON" && jl % 4 == 0 && 20 + jl <= d.Length, "JSON-Chunk");
        var g = (JsonObject)JsonNode.Parse(Encoding.UTF8.GetString(d, 20, jl).TrimEnd(' '))!;
        int o = 20 + jl;
        Wahr(o + 8 <= d.Length, "BIN-Chunk fehlt");
        int bl = (int)BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(o));
        Wahr(Encoding.ASCII.GetString(d, o + 4, 3) == "BIN" && d[o + 7] == 0 && bl % 4 == 0 && o + 8 + bl == d.Length, "BIN-Chunk");
        var bin = d.AsSpan(o + 8, bl).ToArray();
        Wahr((int)g["buffers"]![0]!["byteLength"]! <= bl, "Puffer grösser als der BIN-Chunk");
        Wahr((string)g["asset"]!["version"]! == "2.0", "asset.version");
        var t = new TestraumGlb(g);
        var szene = (JsonObject)g["scenes"]![(int)g["scene"]!]!;
        foreach (var k in szene["nodes"]!.AsArray())
        {
            var knoten = (JsonObject)g["nodes"]![(int)k!]!;
            Wahr(knoten["matrix"] is null && knoten["translation"] is null && knoten["rotation"] is null, "Knoten ohne Transformation");
            var mesh = (JsonObject)g["meshes"]![(int)knoten["mesh"]!]!;
            var prim = (JsonObject)mesh["primitives"]![0]!;
            Wahr((int)prim["mode"]! == 4, "nur Dreiecke");
            var pos = Floats(g, bin, (int)prim["attributes"]!["POSITION"]!);
            var nor = Floats(g, bin, (int)prim["attributes"]!["NORMAL"]!);
            var ind = Indizes(g, bin, (int)prim["indices"]!);
            Wahr(pos.Length == nor.Length && ind.Length % 3 == 0 && ind.All(i => i < pos.Length / 3), "Netz " + (string?)mesh["name"]);
            for (int i = 0; i < nor.Length; i += 3) Wahr(Math.Abs(Math.Sqrt(nor[i] * nor[i] + nor[i + 1] * nor[i + 1] + nor[i + 2] * nor[i + 2]) - 1) < 1e-4, "Normale nicht Einheit");
            t.Netze.Add(new Netz((string)mesh["name"]!, pos, nor, ind, (JsonObject)g["materials"]![(int)prim["material"]!]!, mesh["extras"] as JsonObject));
        }
        return t;
    }

    static (byte[] Daten, int Ab, int Anzahl, JsonObject Acc) View(JsonObject g, byte[] bin, int nr, int groesse)
    {
        var acc = (JsonObject)g["accessors"]![nr]!;
        var view = (JsonObject)g["bufferViews"]![(int)acc["bufferView"]!]!;
        int ab = (int?)view["byteOffset"] ?? 0, laenge = (int)view["byteLength"]!, anzahl = (int)acc["count"]!;
        Wahr(ab % 4 == 0 && ab + laenge <= bin.Length && anzahl * groesse <= laenge, $"Accessor {nr} ausserhalb");
        return (bin, ab, anzahl, acc);
    }

    static float[] Floats(JsonObject g, byte[] bin, int nr)
    {
        var (d, ab, n, acc) = View(g, bin, nr, 12);
        Wahr((int)acc["componentType"]! == 5126 && (string)acc["type"]! == "VEC3", "FLOAT VEC3");
        var w = new float[n * 3];
        for (int i = 0; i < w.Length; i++) w[i] = BinaryPrimitives.ReadSingleLittleEndian(d.AsSpan(ab + 4 * i));
        for (int k = 0; k < 3; k++)
        {
            var werte = Enumerable.Range(0, n).Select(i => (double)w[3 * i + k]).ToList();
            Wahr(Math.Abs((double)acc["min"]![k]! - werte.Min()) < 1e-6 && Math.Abs((double)acc["max"]![k]! - werte.Max()) < 1e-6, "min/max");
        }
        return w;
    }

    static uint[] Indizes(JsonObject g, byte[] bin, int nr)
    {
        var typ = (int)((JsonObject)g["accessors"]![nr]!)["componentType"]!;
        var (d, ab, n, _) = View(g, bin, nr, typ == 5123 ? 2 : 4);
        Wahr(typ is 5123 or 5125, "Indextyp");
        return [.. Enumerable.Range(0, n).Select(i => typ == 5123 ? BinaryPrimitives.ReadUInt16LittleEndian(d.AsSpan(ab + 2 * i)) : BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(ab + 4 * i)))];
    }

    /// <summary>Netz und Dreiecksbereich eines Elements (mesh.extras.kicad.elemente).</summary>
    public (Netz Netz, int Ab, int Anzahl, string? Ebene)? Element(string id)
    {
        foreach (var n in Netze)
        {
            var el = (JsonObject)n.Kicad["elemente"]!;
            var ids = el["ids"]!.AsArray();
            for (int i = 0; i < ids.Count; i++)
                if ((string)ids[i]! == id) return (n, (int)el["ab"]![i]!, (int)el["anzahl"]![i]!, (string?)el["ebenen"]![i]);
        }
        return null;
    }

    /// <summary>Hüllbox (glTF, m) der Dreiecke eines Elements: [minx, miny, minz, maxx, maxy, maxz].</summary>
    public double[] Box(string id)
    {
        var (n, ab, anzahl, _) = Element(id) ?? throw new Xunit.Sdk.XunitException($"Element {id} fehlt");
        double[] b = [double.MaxValue, double.MaxValue, double.MaxValue, double.MinValue, double.MinValue, double.MinValue];
        for (int t = ab; t < ab + anzahl; t++)
            for (int k = 0; k < 3; k++)
            {
                var i = (int)n.Ind[3 * t + k];
                for (int a = 0; a < 3; a++) { b[a] = Math.Min(b[a], n.Pos[3 * i + a]); b[a + 3] = Math.Max(b[a + 3], n.Pos[3 * i + a]); }
            }
        return b;
    }
}
