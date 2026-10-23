using System.Buffers.Binary;
using System.Text;
using System.Text.Json.Nodes;

namespace Hlks.Kern.Tests;

/// <summary>Unabhängige Prüfung einer GLB-Datei (glTF 2.0 binär) für die Tests der Quest App (P2): liest die Datei selbst (ohne den Schreiber
/// des Kerns) und prüft Kopf, Chunks, Ausrichtung, Verweise, Accessors (min/max gegen die Daten), Indizes, Normalen und Materialien.
/// Auch von den Web-Tests benutzt (verlinkt).</summary>
public sealed class GlbPruefer
{
    public JsonObject Gltf { get; }
    public byte[] Bin { get; }
    public int JsonLaenge { get; }
    public int BinLaenge { get; }

    GlbPruefer(JsonObject gltf, byte[] bin, int jsonLaenge, int binLaenge) { Gltf = gltf; Bin = bin; JsonLaenge = jsonLaenge; BinLaenge = binLaenge; }

    public JsonArray Meshes => Gltf["meshes"] as JsonArray ?? [];
    public JsonObject SzeneExtras => (JsonObject)Gltf["scenes"]![0]!["extras"]!["kicad"]!;
    public JsonObject Extras(int mesh) => (JsonObject)Meshes[mesh]!["extras"]!["kicad"]!;
    public JsonObject Material(int mesh) => (JsonObject)Gltf["materials"]![(int)Meshes[mesh]!["primitives"]![0]!["material"]!]!;

    static void Wahr(bool b, string meldung) { if (!b) throw new Xunit.Sdk.XunitException("GLB ungültig: " + meldung); }

    /// <summary>Liest und prüft die Datei; wirft bei jedem Verstoss.</summary>
    public static GlbPruefer Pruefen(byte[] glb)
    {
        Wahr(glb.Length >= 20, "kürzer als Kopf und Chunk-Kopf");
        Wahr(BinaryPrimitives.ReadUInt32LittleEndian(glb) == 0x46546C67, "Magic ist nicht «glTF»");
        Wahr(Encoding.ASCII.GetString(glb, 0, 4) == "glTF", "Magic-Text");
        Wahr(BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(4)) == 2, "Version ist nicht 2");
        Wahr(BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(8)) == glb.Length, "Länge im Kopf stimmt nicht");
        Wahr(glb.Length % 4 == 0, "Gesamtlänge nicht auf 4 Byte");
        int jl = (int)BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(12));
        Wahr(BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(16)) == 0x4E4F534A, "erster Chunk ist nicht JSON");
        Wahr(jl % 4 == 0 && 20 + jl <= glb.Length, "JSON-Chunk Länge/Ausrichtung");
        var jsonText = Encoding.UTF8.GetString(glb, 20, jl);
        int ende = 20 + jl;
        // Auffüllung des JSON nur mit Leerzeichen (0x20)
        int inhalt = Encoding.UTF8.GetByteCount(jsonText.TrimEnd(' '));
        for (int i = 20 + inhalt; i < ende; i++) Wahr(glb[i] == 0x20, "JSON mit anderem Zeichen als Leerzeichen aufgefüllt");
        var gltf = JsonNode.Parse(jsonText) as JsonObject ?? throw new Xunit.Sdk.XunitException("JSON ist kein Objekt");
        byte[] bin = [];
        int bl = 0;
        if (ende < glb.Length)
        {
            Wahr(ende + 8 <= glb.Length, "BIN-Chunk-Kopf");
            bl = (int)BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(ende));
            Wahr(BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(ende + 4)) == 0x004E4942, "zweiter Chunk ist nicht BIN");
            Wahr(bl % 4 == 0 && ende + 8 + bl == glb.Length, "BIN-Chunk Länge/Ausrichtung");
            bin = glb.AsSpan(ende + 8, bl).ToArray();
        }
        var p = new GlbPruefer(gltf, bin, jl, bl);
        p.Struktur();
        return p;
    }

    static int Groesse(int komponente) => komponente switch { 5126 or 5125 => 4, 5123 => 2, 5121 => 1, _ => throw new Xunit.Sdk.XunitException($"Komponententyp {komponente}") };
    static int Anzahl(string typ) => typ switch { "SCALAR" => 1, "VEC3" => 3, _ => throw new Xunit.Sdk.XunitException($"Typ {typ}") };

    void Struktur()
    {
        Wahr((string?)Gltf["asset"]?["version"] == "2.0", "asset.version");
        var szenen = Gltf["scenes"] as JsonArray;
        Wahr(szenen is { Count: >= 1 }, "keine Szene");
        Wahr((int)Gltf["scene"]! < szenen!.Count, "scene ausserhalb");
        var nodes = Gltf["nodes"] as JsonArray ?? [];
        var meshes = Meshes;
        var accessors = Gltf["accessors"] as JsonArray ?? [];
        var views = Gltf["bufferViews"] as JsonArray ?? [];
        var buffers = Gltf["buffers"] as JsonArray ?? [];
        var materials = Gltf["materials"] as JsonArray ?? [];
        foreach (var s in szenen)
            if (s!["nodes"] is JsonArray sn)
            {
                Wahr(sn.Count >= 1, "scene.nodes leer (dann weglassen)");
                foreach (var n in sn) Wahr((int)n! >= 0 && (int)n! < nodes.Count, "Szene verweist auf fehlenden Knoten");
            }
        foreach (var n in nodes) Wahr(n!["mesh"] is null || ((int)n["mesh"]! >= 0 && (int)n["mesh"]! < meshes.Count), "Knoten verweist auf fehlendes Netz");
        if (buffers.Count > 0)
        {
            Wahr(buffers.Count == 1, "mehr als ein Puffer");
            long bl = (long)buffers[0]!["byteLength"]!;
            Wahr(bl <= Bin.Length && Bin.Length - bl < 4, "buffers[0].byteLength passt nicht zum BIN-Chunk");
        }
        else Wahr(Bin.Length == 0, "BIN-Chunk ohne Puffer");
        foreach (var v in views)
        {
            long o = (long?)v!["byteOffset"] ?? 0, l = (long)v["byteLength"]!;
            Wahr((int)v["buffer"]! == 0 && o >= 0 && l > 0 && o + l <= Bin.Length, "bufferView ausserhalb des Puffers");
            Wahr(o % 4 == 0, "bufferView nicht auf 4 Byte ausgerichtet");
        }
        foreach (var a in accessors)
        {
            int k = (int)a!["componentType"]!, n = Anzahl((string)a["type"]!), count = (int)a["count"]!;
            var v = views[(int)a["bufferView"]!]!;
            long o = ((long?)v["byteOffset"] ?? 0) + ((long?)a["byteOffset"] ?? 0);
            Wahr(count >= 1, "accessor.count < 1");
            Wahr(o % Groesse(k) == 0, "Accessor nicht auf seine Komponentengrösse ausgerichtet");
            Wahr(((long?)a["byteOffset"] ?? 0) + (long)count * n * Groesse(k) <= (long)v["byteLength"]!, "Accessor über die bufferView hinaus");
            Wahr(a["min"] is JsonArray mi && mi.Count == n && a["max"] is JsonArray ma && ma.Count == n, "min/max fehlen");
            // min/max gegen die Daten
            var werte = Lesen(a.AsObject());
            for (int c = 0; c < n; c++)
            {
                double min = double.MaxValue, max = double.MinValue;
                for (int i = c; i < werte.Length; i += n) { min = Math.Min(min, werte[i]); max = Math.Max(max, werte[i]); }
                Wahr((double)a["min"]![c]! == min && (double)a["max"]![c]! == max, $"min/max stimmen nicht mit den Daten überein (Komponente {c})");
            }
            Wahr(werte.All(double.IsFinite), "NaN oder unendlich in den Daten");
        }
        foreach (var m in meshes)
        {
            var prims = m!["primitives"] as JsonArray;
            Wahr(prims is { Count: >= 1 }, "Netz ohne primitives");
            foreach (var pr in prims!)
            {
                Wahr((int?)pr!["mode"] is null or 4, "mode ist nicht TRIANGLES");
                var att = pr["attributes"]!.AsObject();
                var pos = accessors[(int)att["POSITION"]!]!.AsObject();
                var nor = accessors[(int)att["NORMAL"]!]!.AsObject();
                Wahr((int)pos["componentType"]! == 5126 && (string)pos["type"]! == "VEC3", "POSITION nicht FLOAT VEC3");
                Wahr((int)nor["componentType"]! == 5126 && (string)nor["type"]! == "VEC3", "NORMAL nicht FLOAT VEC3");
                Wahr((int)pos["count"]! == (int)nor["count"]!, "POSITION und NORMAL verschieden viele");
                Wahr((int?)views[(int)pos["bufferView"]!]!["target"] == 34962 && (int?)views[(int)nor["bufferView"]!]!["target"] == 34962, "Ziel ARRAY_BUFFER fehlt");
                var n = Lesen(nor);
                for (int i = 0; i < n.Length; i += 3)
                    Wahr(Math.Abs(Math.Sqrt(n[i] * n[i] + n[i + 1] * n[i + 1] + n[i + 2] * n[i + 2]) - 1) < 1e-5, "Normale nicht normiert");
                var idx = accessors[(int)pr["indices"]!]!.AsObject();
                Wahr((string)idx["type"]! == "SCALAR" && (int)idx["componentType"]! is 5123 or 5125, "Indizes nicht SCALAR uint16/uint32");
                Wahr((int?)views[(int)idx["bufferView"]!]!["target"] == 34963, "Ziel ELEMENT_ARRAY_BUFFER fehlt");
                Wahr((int)idx["count"]! % 3 == 0, "Indexzahl nicht durch 3 teilbar");
                if ((int)pos["count"]! > 65535) Wahr((int)idx["componentType"]! == 5125, "über 65535 Ecken ohne uint32");
                foreach (var i in Lesen(idx)) Wahr(i >= 0 && i < (int)pos["count"]!, "Index ausserhalb der Ecken");
                int mat = (int)pr["material"]!;
                Wahr(mat >= 0 && mat < materials.Count, "Material fehlt");
                var mo = materials[mat]!;
                var f = mo["pbrMetallicRoughness"]!["baseColorFactor"] as JsonArray;
                Wahr(f is { Count: 4 } && f.All(x => (double)x! is >= 0 and <= 1), "baseColorFactor");
                var modus = (string?)mo["alphaMode"] ?? "OPAQUE";
                Wahr(modus is "OPAQUE" or "BLEND", "alphaMode");
                Wahr((modus == "BLEND") == ((double)f![3]! < 1), "alphaMode passt nicht zur Deckkraft");
            }
        }
    }

    /// <summary>Werte eines Accessors als double (alle Komponenten hintereinander).</summary>
    public double[] Lesen(JsonObject a)
    {
        int k = (int)a["componentType"]!, n = Anzahl((string)a["type"]!), count = (int)a["count"]!;
        var v = ((JsonArray)Gltf["bufferViews"]!)[(int)a["bufferView"]!]!;
        int o = (int)(((long?)v["byteOffset"] ?? 0) + ((long?)a["byteOffset"] ?? 0));
        var aus = new double[count * n];
        var s = Bin.AsSpan(o);
        for (int i = 0; i < aus.Length; i++)
            aus[i] = k switch
            {
                5126 => BinaryPrimitives.ReadSingleLittleEndian(s[(i * 4)..]),
                5125 => BinaryPrimitives.ReadUInt32LittleEndian(s[(i * 4)..]),
                5123 => BinaryPrimitives.ReadUInt16LittleEndian(s[(i * 2)..]),
                _ => s[i],
            };
        return aus;
    }

    JsonObject Accessor(int mesh, string? attribut)
    {
        var pr = Meshes[mesh]!["primitives"]![0]!;
        int i = attribut is null ? (int)pr["indices"]! : (int)pr["attributes"]![attribut]!;
        return ((JsonArray)Gltf["accessors"]!)[i]!.AsObject();
    }

    public double[] Positionen(int mesh) => Lesen(Accessor(mesh, "POSITION"));
    public double[] Normalen(int mesh) => Lesen(Accessor(mesh, "NORMAL"));
    public int[] Indizes(int mesh) => Lesen(Accessor(mesh, null)).Select(x => (int)x).ToArray();
    public int IndexTyp(int mesh) => (int)Accessor(mesh, null)["componentType"]!;
    public int Dreiecke(int mesh) => (int)Accessor(mesh, null)["count"]! / 3;

    /// <summary>Zuordnung eines Netzes: (Id, ab, anzahl, Zusatz) aus extras.kicad.elemente.</summary>
    public List<(string Id, int Ab, int Anzahl, string? Zusatz)> Zuordnung(int mesh)
    {
        var e = Extras(mesh)["elemente"]!;
        var ids = (JsonArray)e["ids"]!; var ab = (JsonArray)e["ab"]!; var an = (JsonArray)e["anzahl"]!;
        var zusatz = (JsonArray)(e["ebenen"] ?? e["entitaeten"])!;
        Wahr(ids.Count == ab.Count && ab.Count == an.Count && an.Count == zusatz.Count, "Spalten der Zuordnung verschieden lang");
        return Enumerable.Range(0, ids.Count).Select(i => ((string)ids[i]!, (int)ab[i]!, (int)an[i]!, (string?)zusatz[i])).ToList();
    }

    /// <summary>Dreiecke eines Bereichs als Eckpunkte (glTF-Meter).</summary>
    public List<double[]> Dreieckspunkte(int mesh, int ab, int anzahl)
    {
        var pos = Positionen(mesh); var idx = Indizes(mesh);
        var aus = new List<double[]>();
        for (int t = ab; t < ab + anzahl; t++)
            for (int k = 0; k < 3; k++) { int i = idx[t * 3 + k]; aus.Add([pos[i * 3], pos[i * 3 + 1], pos[i * 3 + 2]]); }
        return aus;
    }

    /// <summary>Umschlossenes Volumen (m³) der Dreiecke eines Bereichs (positiv bei Normalen nach aussen).</summary>
    public double Volumen(int mesh, int ab, int anzahl)
    {
        var p = Dreieckspunkte(mesh, ab, anzahl);
        double v = 0;
        for (int i = 0; i < p.Count; i += 3)
        {
            var (a, b, c) = (p[i], p[i + 1], p[i + 2]);
            v += a[0] * (b[1] * c[2] - b[2] * c[1]) + a[1] * (b[2] * c[0] - b[0] * c[2]) + a[2] * (b[0] * c[1] - b[1] * c[0]);
        }
        return v / 6;
    }
}
