// Gelesene DWG/DXF-Zeichnung als einfache Primitive in Zeichnungseinheiten (Modul 1b, B3). Wird einmal aus der Datei
// gelesen (CadLeser) und als Ableitung «<datei>.dwg.json» im Ordner unterlagen abgelegt; Zeichnen und Kalibrieren
// brauchen die Originaldatei danach nicht mehr. Im Speicher bleiben die letzten Zeichnungen, damit nicht jede
// Plangrafik die Datei neu liest.
//
// Format der Ableitung (JSON, format 1):
//   { "format": 1, "version": "AC1024", "einheit": "Meters", "mmJeEinheit": 1000, "einheitBekannt": true,
//     "umriss": [minx, miny, maxx, maxy]        Hauptteil der Zeichnung (ohne weit abgelegene Einzelteile)
//     "ausdehnung": [minx, miny, maxx, maxy]    alles
//     "layer": ["0", …], "layerAus": ["…"]      alle Layer; in der Datei ausgeschaltete/gefrorene (nicht gezeichnet)
//     "nichtDargestellt": { "Typ": anzahl }, "gekappt": anzahl,
//     "primitive": [ ["l"|"p"|"g", layer, x1, y1, x2, y2, …]        Linie, Polylinie offen, Polylinie geschlossen
//                    ["b", layer, mx, my, r, vonGrad, bisGrad]      Bogen (Gegenuhrzeigersinn)
//                    ["k", layer, mx, my, r]                        Kreis
//                    ["t", layer, x, y, höhe, winkelGrad, ausrichtung (0 links, 1 mitte, 2 rechts), "text"] ] }

using System.Text.Json;

namespace Hlks.Kern;

/// <summary>Ein Primitiv der gelesenen Zeichnung (Zeichnungseinheiten, Grad).</summary>
public sealed class CadPrimitiv
{
    /// <summary>l Linie · p Polylinie offen · g Polylinie geschlossen · b Bogen · k Kreis · t Text</summary>
    public char Art { get; init; }
    public int Layer { get; init; }
    /// <summary>Zahlen je nach Art (siehe Dateikopf).</summary>
    public double[] W { get; init; } = [];
    public string? Text { get; init; }
}

public sealed class CadZeichnung
{
    public const int Format = 1;
    public string Version { get; set; } = "";
    /// <summary>Einheit aus INSUNITS (englischer Name von ACadSharp, z. B. «Meters»).</summary>
    public string Einheit { get; set; } = "";
    /// <summary>Millimeter je Zeichnungseinheit.</summary>
    public double MmJeEinheit { get; set; } = 1;
    /// <summary>false: INSUNITS fehlt (Unitless) oder ist unbekannt – es gilt die Annahme mm.</summary>
    public bool EinheitBekannt { get; set; }
    public double[]? Umriss { get; set; }
    public double[]? Ausdehnung { get; set; }
    public List<string> Layer { get; set; } = [];
    public List<string> LayerAus { get; set; } = [];
    public SortedDictionary<string, int> NichtDargestellt { get; set; } = [];
    /// <summary>Anzahl Primitive, die wegen der Obergrenze nicht mehr aufgenommen wurden.</summary>
    public int Gekappt { get; set; }
    public List<CadPrimitiv> Primitive { get; set; } = [];

    // ---------- Schreiben ----------

    public void Schreiben(string pfad)
    {
        using var fs = File.Create(pfad);
        using var w = new Utf8JsonWriter(fs);
        w.WriteStartObject();
        w.WriteNumber("format", Format);
        w.WriteString("version", Version);
        w.WriteString("einheit", Einheit);
        w.WriteNumber("mmJeEinheit", MmJeEinheit);
        w.WriteBoolean("einheitBekannt", EinheitBekannt);
        Zahlen(w, "umriss", Umriss);
        Zahlen(w, "ausdehnung", Ausdehnung);
        w.WriteStartArray("layer"); foreach (var l in Layer) w.WriteStringValue(l); w.WriteEndArray();
        w.WriteStartArray("layerAus"); foreach (var l in LayerAus) w.WriteStringValue(l); w.WriteEndArray();
        w.WriteStartObject("nichtDargestellt"); foreach (var (k, n) in NichtDargestellt) w.WriteNumber(k, n); w.WriteEndObject();
        w.WriteNumber("gekappt", Gekappt);
        w.WriteStartArray("primitive");
        foreach (var p in Primitive)
        {
            w.WriteStartArray();
            w.WriteStringValue(p.Art.ToString());
            w.WriteNumberValue(p.Layer);
            foreach (var z in p.W) w.WriteNumberValue(Math.Round(z, 5));
            if (p.Text is not null) w.WriteStringValue(p.Text);
            w.WriteEndArray();
        }
        w.WriteEndArray();
        w.WriteEndObject();
    }

    static void Zahlen(Utf8JsonWriter w, string name, double[]? z)
    {
        if (z is null) return;
        w.WriteStartArray(name); foreach (var x in z) w.WriteNumberValue(Math.Round(x, 5)); w.WriteEndArray();
    }

    // ---------- Lesen ----------

    public static CadZeichnung Lesen(string pfad)
    {
        using var fs = File.OpenRead(pfad);
        using var doc = JsonDocument.Parse(fs);
        var r = doc.RootElement;
        if (!r.TryGetProperty("format", out var fm) || fm.GetInt32() != Format)
            throw new HlksFehler($"Die abgeleitete Datei «{Path.GetFileName(pfad)}» hat ein unbekanntes Format.", "Die Datei löschen – sie wird beim nächsten Zeichnen aus der DWG/DXF-Datei neu erzeugt.");
        var z = new CadZeichnung
        {
            Version = r.GetProperty("version").GetString() ?? "",
            Einheit = r.GetProperty("einheit").GetString() ?? "",
            MmJeEinheit = r.GetProperty("mmJeEinheit").GetDouble(),
            EinheitBekannt = r.GetProperty("einheitBekannt").GetBoolean(),
            Umriss = r.TryGetProperty("umriss", out var um) ? um.EnumerateArray().Select(x => x.GetDouble()).ToArray() : null,
            Ausdehnung = r.TryGetProperty("ausdehnung", out var au) ? au.EnumerateArray().Select(x => x.GetDouble()).ToArray() : null,
            Gekappt = r.TryGetProperty("gekappt", out var gk) ? gk.GetInt32() : 0,
        };
        z.Layer = r.GetProperty("layer").EnumerateArray().Select(x => x.GetString() ?? "").ToList();
        z.LayerAus = r.GetProperty("layerAus").EnumerateArray().Select(x => x.GetString() ?? "").ToList();
        foreach (var n in r.GetProperty("nichtDargestellt").EnumerateObject()) z.NichtDargestellt[n.Name] = n.Value.GetInt32();
        foreach (var p in r.GetProperty("primitive").EnumerateArray())
        {
            var teile = p.EnumerateArray().ToList();
            char art = teile[0].GetString()![0];
            int layer = teile[1].GetInt32();
            string? text = null;
            int ende = teile.Count;
            if (art == 't') { text = teile[^1].GetString(); ende--; }
            var w = new double[ende - 2];
            for (int i = 2; i < ende; i++) w[i - 2] = teile[i].GetDouble();
            z.Primitive.Add(new CadPrimitiv { Art = art, Layer = layer, W = w, Text = text });
        }
        return z;
    }

    // ---------- Zwischenspeicher im Arbeitsspeicher ----------

    static readonly object sperre = new();
    static readonly List<(string Schluessel, CadZeichnung Z)> speicher = [];
    const int MaxImSpeicher = 4;

    /// <summary>Zeichnung der Unterlage: aus dem Speicher, sonst aus der Ableitung «.dwg.json» (beim ersten Mal aus der Datei erzeugt).</summary>
    public static CadZeichnung Laden(Projekt p, Element unterlage)
    {
        var datei = unterlage.Text("datei") ?? throw new HlksFehler($"Unterlage {unterlage.Kennzeichen} hat keine Datei.");
        var quelle = Unterlagen.Pfad(p, datei);
        var ableitung = Unterlagen.Ableitung(p, datei, ".dwg.json", ziel => CadLeser.Lesen(quelle).Schreiben(ziel));
        var info = new FileInfo(ableitung);
        var schluessel = $"{ableitung}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
        lock (sperre)
        {
            var i = speicher.FindIndex(x => x.Schluessel == schluessel);
            if (i >= 0) return speicher[i].Z;
        }
        var z = Lesen(ableitung);
        lock (sperre)
        {
            speicher.RemoveAll(x => x.Schluessel == schluessel);
            speicher.Add((schluessel, z));
            while (speicher.Count > MaxImSpeicher) speicher.RemoveAt(0);
        }
        return z;
    }
}
