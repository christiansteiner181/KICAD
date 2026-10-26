// Quest App (Modul XR, P2): schlanker Schreiber für glTF 2.0 binär (GLB), ohne fremde Bibliothek.
//
// Entscheid (P2, 23.10.2026): eigener Schreiber statt SharpGLTF. Gebraucht wird nur ein kleiner, fester Ausschnitt von glTF – Dreiecks-
// netze mit POSITION, NORMAL und Indizes, ein Material je Netz (Farbe, Deckkraft, doppelseitig) und `extras` –, den die Tests vollständig
// nachprüfen (GlbSchreiberTests). Kein Paket, keine Lizenzfrage, kein Download; grosse Netze (IFC-Unterlage, Hunderttausende Dreiecke)
// gehen ohne Zwischenobjekte je Ecke direkt in den Puffer.
//
// Aufbau (Khronos glTF 2.0, Abschnitte «Binary glTF Layout» und «Data Alignment»):
//   Kopf 12 Byte: uint32 magic 0x46546C67 («glTF»), uint32 version 2, uint32 Gesamtlänge.
//   Chunk 0: uint32 Länge, uint32 Typ 0x4E4F534A («JSON»), JSON (UTF-8), mit Leerzeichen auf ein Vielfaches von 4 aufgefüllt.
//   Chunk 1: uint32 Länge, uint32 Typ 0x004E4942 («BIN\0»), Binärdaten, mit Nullbytes auf ein Vielfaches von 4 aufgefüllt (fehlt ohne Netz).
//   Je Netz drei bufferViews (Positionen, Normalen, Indizes), jede auf 4 Byte ausgerichtet; Accessors ohne eigenen Versatz.
//   POSITION und NORMAL: FLOAT VEC3; Indizes UNSIGNED_SHORT bis 65 535 Ecken, sonst UNSIGNED_INT. Jeder Accessor trägt min und max
//   (für POSITION Pflicht, für die übrigen zur Prüfung). Ein Knoten je Netz, alle in der einen Szene.
//   Material: pbrMetallicRoughness mit baseColorFactor (linear, siehe <see cref="GlbSchreiber.Linear"/>), metallicFactor 0,
//   roughnessFactor 1 (matt wie MeshLambertMaterial der 3D-Ansicht), alphaMode OPAQUE bzw. BLEND, doubleSided.

using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <summary>Material eines Netzes: Farbe #rrggbb (sRGB wie in der 3D-Ansicht), Deckkraft 0…1 (unter 1: BLEND), doppelseitig.</summary>
public sealed record GlbMaterial(string Name, string Farbe, double Deckkraft, bool Doppelseitig = true)
{
    public bool Transparent => Deckkraft < 1;
}

/// <summary>Ein Dreiecksnetz in glTF-Koordinaten (Meter): Positionen und Normalen je Ecke (x, y, z hintereinander), Indizes je Dreieck.</summary>
public sealed class GlbNetz
{
    public required string Name { get; init; }
    public required float[] Positionen { get; init; }
    public required float[] Normalen { get; init; }
    public required uint[] Indizes { get; init; }
    public required GlbMaterial Material { get; init; }
    /// <summary>Anwendungsdaten des Netzes (glTF `mesh.extras`).</summary>
    public JsonObject? Extras { get; init; }
    public int Ecken => Positionen.Length / 3;
    public int Dreiecke => Indizes.Length / 3;
}

public static class GlbSchreiber
{
    public const uint Magic = 0x46546C67;     // «glTF»
    public const uint Version = 2;
    public const uint TypJson = 0x4E4F534A;   // «JSON»
    public const uint TypBin = 0x004E4942;    // «BIN\0»
    public const int Float = 5126, UnsignedShort = 5123, UnsignedInt = 5125;
    public const int ArrayBuffer = 34962, ElementArrayBuffer = 34963;
    public const int Dreiecke = 4;            // primitive.mode TRIANGLES

    /// <summary>Schreibt die GLB. Netze ohne Dreiecke entfallen. szeneExtras und assetExtras gehen als `extras` an Szene bzw. asset.</summary>
    public static byte[] Schreiben(IReadOnlyList<GlbNetz> netze, string szeneName, JsonObject? szeneExtras = null, JsonObject? assetExtras = null)
    {
        if (!BitConverter.IsLittleEndian) throw new PlatformNotSupportedException("GLB wird little-endian geschrieben; diese Plattform ist big-endian.");
        var bin = new MemoryStream();
        var accessors = new JsonArray(); var views = new JsonArray(); var meshes = new JsonArray(); var materials = new JsonArray(); var nodes = new JsonArray();

        int View(ReadOnlySpan<byte> daten, int? ziel)
        {
            while (bin.Length % 4 != 0) bin.WriteByte(0);
            var view = new JsonObject { ["buffer"] = 0, ["byteOffset"] = bin.Length, ["byteLength"] = daten.Length };
            if (ziel is int z) view["target"] = z;
            bin.Write(daten);
            views.Add(view);
            return views.Count - 1;
        }

        int Vec3(float[] werte, int ziel)
        {
            float[] min = [float.MaxValue, float.MaxValue, float.MaxValue], max = [float.MinValue, float.MinValue, float.MinValue];
            for (int i = 0; i < werte.Length; i += 3)
                for (int k = 0; k < 3; k++) { var v = werte[i + k]; if (v < min[k]) min[k] = v; if (v > max[k]) max[k] = v; }
            accessors.Add(new JsonObject
            {
                ["bufferView"] = View(MemoryMarshal.AsBytes(werte.AsSpan()), ziel), ["componentType"] = Float, ["count"] = werte.Length / 3, ["type"] = "VEC3",
                // als double (exakter Wert des float32): Prüfprogramme vergleichen min/max mit den Daten
                ["min"] = new JsonArray(min.Select(v => (JsonNode)JsonValue.Create((double)v)).ToArray()),
                ["max"] = new JsonArray(max.Select(v => (JsonNode)JsonValue.Create((double)v)).ToArray()),
            });
            return accessors.Count - 1;
        }

        int Index(uint[] indizes, int ecken)
        {
            uint min = uint.MaxValue, max = 0;
            foreach (var i in indizes) { if (i < min) min = i; if (i > max) max = i; }
            int typ;
            int view;
            if (ecken <= ushort.MaxValue)
            {
                var kurz = new ushort[indizes.Length];
                for (int i = 0; i < kurz.Length; i++) kurz[i] = (ushort)indizes[i];
                typ = UnsignedShort;
                view = View(MemoryMarshal.AsBytes(kurz.AsSpan()), ElementArrayBuffer);
            }
            else { typ = UnsignedInt; view = View(MemoryMarshal.AsBytes(indizes.AsSpan()), ElementArrayBuffer); }
            accessors.Add(new JsonObject
            {
                ["bufferView"] = view, ["componentType"] = typ, ["count"] = indizes.Length, ["type"] = "SCALAR",
                ["min"] = new JsonArray(JsonValue.Create(min)), ["max"] = new JsonArray(JsonValue.Create(max)),
            });
            return accessors.Count - 1;
        }

        foreach (var n in netze)
        {
            if (n.Dreiecke == 0 || n.Ecken == 0) continue;
            if (n.Normalen.Length != n.Positionen.Length || n.Positionen.Length % 3 != 0 || n.Indizes.Length % 3 != 0)
                throw new ArgumentException($"Netz «{n.Name}»: Positionen, Normalen und Indizes passen nicht zusammen.");
            foreach (var i in n.Indizes)
                if (i >= n.Ecken) throw new ArgumentException($"Netz «{n.Name}»: Index {i} ausserhalb von {n.Ecken} Ecken.");
            int pos = Vec3(n.Positionen, ArrayBuffer), nor = Vec3(n.Normalen, ArrayBuffer), idx = Index(n.Indizes, n.Ecken);
            var lin = Linear(n.Material.Farbe);
            double a = Math.Clamp(n.Material.Deckkraft, 0, 1);
            var mat = new JsonObject
            {
                ["name"] = n.Material.Name,
                ["pbrMetallicRoughness"] = new JsonObject
                {
                    ["baseColorFactor"] = new JsonArray(Rund(lin[0]), Rund(lin[1]), Rund(lin[2]), Rund(a)),
                    ["metallicFactor"] = 0, ["roughnessFactor"] = 1,
                },
                ["alphaMode"] = n.Material.Transparent ? "BLEND" : "OPAQUE",
                ["doubleSided"] = n.Material.Doppelseitig,
            };
            materials.Add(mat);
            var mesh = new JsonObject
            {
                ["name"] = n.Name,
                ["primitives"] = new JsonArray(new JsonObject
                {
                    ["attributes"] = new JsonObject { ["POSITION"] = pos, ["NORMAL"] = nor },
                    ["indices"] = idx, ["material"] = materials.Count - 1, ["mode"] = Dreiecke,
                }),
            };
            if (n.Extras is not null) mesh["extras"] = n.Extras.DeepClone();
            meshes.Add(mesh);
            nodes.Add(new JsonObject { ["name"] = n.Name, ["mesh"] = meshes.Count - 1 });
        }
        while (bin.Length % 4 != 0) bin.WriteByte(0);

        var asset = new JsonObject { ["version"] = "2.0", ["generator"] = "KICAD Hlks.Kern GlbSchreiber" };
        if (assetExtras is not null) asset["extras"] = assetExtras.DeepClone();
        var szene = new JsonObject { ["name"] = szeneName };
        if (nodes.Count > 0) szene["nodes"] = new JsonArray(Enumerable.Range(0, nodes.Count).Select(i => (JsonNode)JsonValue.Create(i)).ToArray());   // glTF: nodes mindestens ein Eintrag oder weglassen
        if (szeneExtras is not null) szene["extras"] = szeneExtras.DeepClone();
        var gltf = new JsonObject { ["asset"] = asset, ["scene"] = 0, ["scenes"] = new JsonArray(szene) };
        if (nodes.Count > 0)
        {
            gltf["nodes"] = nodes; gltf["meshes"] = meshes; gltf["materials"] = materials; gltf["accessors"] = accessors; gltf["bufferViews"] = views;
            gltf["buffers"] = new JsonArray(new JsonObject { ["byteLength"] = bin.Length });
        }
        return Glb(gltf, nodes.Count > 0 ? bin.ToArray() : null);
    }

    /// <summary>Behälter: JSON- und (optional) BIN-Chunk mit Kopf, je auf 4 Byte aufgefüllt.</summary>
    public static byte[] Glb(JsonObject gltf, byte[]? bin)
    {
        var json = Encoding.UTF8.GetBytes(gltf.ToJsonString());
        int jsonLaenge = (json.Length + 3) & ~3, binLaenge = bin is null ? 0 : (bin.Length + 3) & ~3;
        long gesamt = 12L + 8 + jsonLaenge + (bin is null ? 0 : 8 + binLaenge);
        if (gesamt > uint.MaxValue) throw new HlksFehler("Die GLB-Datei wäre grösser als 4 GB.", "Das Geschoss in kleinere Teile aufteilen.");
        var aus = new byte[gesamt];
        var s = aus.AsSpan();
        BinaryPrimitives.WriteUInt32LittleEndian(s, Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(s[4..], Version);
        BinaryPrimitives.WriteUInt32LittleEndian(s[8..], (uint)gesamt);
        BinaryPrimitives.WriteUInt32LittleEndian(s[12..], (uint)jsonLaenge);
        BinaryPrimitives.WriteUInt32LittleEndian(s[16..], TypJson);
        json.CopyTo(s[20..]);
        s.Slice(20 + json.Length, jsonLaenge - json.Length).Fill((byte)' ');
        if (bin is not null)
        {
            int o = 20 + jsonLaenge;
            BinaryPrimitives.WriteUInt32LittleEndian(s[o..], (uint)binLaenge);
            BinaryPrimitives.WriteUInt32LittleEndian(s[(o + 4)..], TypBin);
            bin.CopyTo(s[(o + 8)..]);   // Rest bleibt 0
        }
        return aus;
    }

    /// <summary>Farbe #rrggbb (sRGB, wie three.js sie liest) als lineare RGB-Werte 0…1 für baseColorFactor (glTF: baseColorFactor ist
    /// linear). Dieselbe Umrechnung wie die 3D-Ansicht (three r160, ColorManagement, `SRGBToLinear` in lib/three/three.module.js):
    /// c &lt; 0.04045 → c/12.92, sonst ((c + 0.055)/1.055)^2.4. Ungültige Farbe: mittleres Grau #808080.</summary>
    public static double[] Linear(string farbe)
    {
        static double L(int b) { double c = b / 255.0; return c < 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4); }
        var f = XrFarben.Normiert(farbe) ?? "#808080";
        int r = int.Parse(f.AsSpan(1, 2), NumberStyles.HexNumber), g = int.Parse(f.AsSpan(3, 2), NumberStyles.HexNumber), b = int.Parse(f.AsSpan(5, 2), NumberStyles.HexNumber);
        return [L(r), L(g), L(b)];
    }

    static JsonNode Rund(double v) => JsonValue.Create(Math.Round(v, 6));
}
