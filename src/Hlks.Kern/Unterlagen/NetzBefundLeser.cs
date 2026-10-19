// Leser «netz» für STL und OBJ (Modul 2, Auftrag B2): liest das Dreiecksnetz und lässt es von der Netz-Analyse von B3 (NetzAnalyse) auswerten:
// form3d (Netz, Hüllquader), flaeche, stutzen, ansicht – wie bei STEP. Version 1 lieferte nur form3d.
// STL: binär und ASCII; OBJ: Ecken (v) und Flächen (f, Vielecke als Fächer in Dreiecke zerlegt), Gruppen (o, g) als Teile der Analyse.
// Ecken gleicher Lage werden verschweisst. Beide Formate nennen keine Einheit: Annahme Millimeter (Hinweis mit der Grösse der Hülle).

using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

public sealed class NetzBefundLeser : IUnterlagenLeser
{
    public string Name => "netz";
    public int Version => 2;   // 2: mit der Netz-Analyse von B3 (flaeche, stutzen, ansicht)
    public IReadOnlyCollection<string> Arten { get; } = ["stl", "obj"];

    /// <summary>Grösse, bis zu der das Netz im Befund steht (Dreiecke); darüber nur die Hülle.</summary>
    public const int MaxDreiecke = 300_000;

    sealed class Netz
    {
        public readonly List<double> Ecken = [];
        public readonly List<int> Dreiecke = [];
        readonly Dictionary<(long, long, long), int> index = [];
        public readonly List<(string Name, int Erstes, int Letztes)> Teile = [];
        public int Entartet;
        public int Punkt(double x, double y, double z)
        {
            var k = ((long)Math.Round(x * 1e4), (long)Math.Round(y * 1e4), (long)Math.Round(z * 1e4));
            if (index.TryGetValue(k, out var i)) return i;
            i = Ecken.Count / 3;
            Ecken.Add(x); Ecken.Add(y); Ecken.Add(z);
            index[k] = i;
            return i;
        }
        public void Dreieck(int a, int b, int c)
        {
            if (a == b || b == c || a == c) { Entartet++; return; }
            Dreiecke.Add(a); Dreiecke.Add(b); Dreiecke.Add(c);
        }
    }

    public UnterlagenBefunde Lesen(string pfad, string datei, string pruefsumme, string art)
    {
        var n = new Netz();
        string format;
        try { format = art == "stl" ? Stl(pfad, n) : Obj(pfad, n); }
        catch (Exception ex) when (ex is not HlksFehler)
        {
            throw new HlksFehler($"Die {art.ToUpperInvariant()}-Datei «{datei}» lässt sich nicht lesen: {ex.Message}", "Datei prüfen oder als STEP bzw. STL (binär) neu exportieren.");
        }
        if (n.Dreiecke.Count == 0) throw new HlksFehler($"Die Datei «{datei}» enthält kein Dreiecksnetz.", "Prüfen, ob die Datei Flächen enthält (STL: facet; OBJ: f-Zeilen).");

        int tris = n.Dreiecke.Count / 3;
        // Netz an die Analyse von B3 (NetzAnalyse): form3d, flaeche, stutzen, ansicht – wie bei STEP. OBJ-Gruppen werden je ein Teil.
        var optionen = new NetzAnalyseOptionen { VollesNetz = tris <= MaxDreiecke };
        var zusatz = new JsonObject { ["format"] = format, ["einheitDatei"] = "unbekannt (Annahme mm)" };
        var satz = NetzAnalyse.Befundsatz(Netze(n), datei, pruefsumme, art, $"{Name} {Version}", optionen, zusatz);
        if (!optionen.VollesNetz) satz.Hinweise.Insert(0, $"Das Netz hat {tris} Dreiecke (mehr als {MaxDreiecke}): im Befund stehen Hülle, Anzahl und das vereinfachte Netz, nicht alle Ecken und Dreiecke.");

        double[] min = [double.MaxValue, double.MaxValue, double.MaxValue], max = [double.MinValue, double.MinValue, double.MinValue];
        for (int i = 0; i < n.Ecken.Count; i += 3)
            for (int k = 0; k < 3; k++) { min[k] = Math.Min(min[k], n.Ecken[i + k]); max[k] = Math.Max(max[k], n.Ecken[i + k]); }
        var groesse = new[] { max[0] - min[0], max[1] - min[1], max[2] - min[2] };
        satz.Hinweise.Insert(0, $"{format}: {tris} Dreiecke, {n.Ecken.Count / 3} Ecken; Hülle {groesse[0]:0.#} × {groesse[1]:0.#} × {groesse[2]:0.#} (Einheit der Datei, Annahme mm).");
        double maxGr = groesse.Max();
        if (maxGr < 1 || maxGr > 100_000)
            satz.Hinweise.Add($"Die längste Seite der Hülle ist {maxGr:0.###}: bei Millimetern unwahrscheinlich für ein Gerät – die Datei hat vermutlich eine andere Einheit (Meter? Zoll?). Mit dem Hersteller oder einem Massbild gegenprüfen.");
        if (n.Entartet > 0) satz.Hinweise.Add($"{n.Entartet} entartete Dreiecke (zwei gleiche Ecken) ausgelassen.");
        return satz;
    }

    /// <summary>Netz der Datei als Netze der Analyse: ohne Gruppen eines, sonst je Gruppe eines (Ecken neu nummeriert).</summary>
    static List<AnalyseNetz> Netze(Netz n)
    {
        int tris = n.Dreiecke.Count / 3;
        if (n.Teile.Count == 0) return [new AnalyseNetz { Name = "", Ecken = [.. n.Ecken], Dreiecke = [.. n.Dreiecke] }];
        var liste = new List<AnalyseNetz>();
        var bereiche = n.Teile.Select(t => (t.Name, t.Erstes, t.Letztes)).ToList();
        // Dreiecke vor der ersten Gruppe oder zwischen Gruppen (ohne Namen) werden ein eigenes Teil
        var abgedeckt = new bool[tris];
        foreach (var (_, a, b) in bereiche) for (int i = a; i <= b; i++) abgedeckt[i] = true;
        if (abgedeckt.Any(x => !x)) bereiche.Add(("ohne Gruppe", -1, -1));
        foreach (var (name, a, b) in bereiche)
        {
            var map = new Dictionary<int, int>();
            var ecken = new List<double>(); var dr = new List<int>();
            for (int t = 0; t < tris; t++)
            {
                bool drin = a < 0 ? !abgedeckt[t] : t >= a && t <= b;
                if (!drin) continue;
                for (int k = 0; k < 3; k++)
                {
                    int v = n.Dreiecke[3 * t + k];
                    if (!map.TryGetValue(v, out var nv)) { map[v] = nv = ecken.Count / 3; ecken.Add(n.Ecken[3 * v]); ecken.Add(n.Ecken[3 * v + 1]); ecken.Add(n.Ecken[3 * v + 2]); }
                    dr.Add(nv);
                }
            }
            if (dr.Count > 0) liste.Add(new AnalyseNetz { Name = name, Ecken = [.. ecken], Dreiecke = [.. dr] });
        }
        return liste;
    }

    // ---------- STL ----------

    static string Stl(string pfad, Netz n)
    {
        var bytes = File.ReadAllBytes(pfad);
        if (bytes.Length >= 84)
        {
            uint anzahl = BitConverter.ToUInt32(bytes, 80);
            if (84 + 50L * anzahl == bytes.Length)
            {
                for (int i = 0; i < anzahl; i++)
                {
                    int o = 84 + 50 * i + 12;   // Normale überspringen
                    int a = n.Punkt(BitConverter.ToSingle(bytes, o), BitConverter.ToSingle(bytes, o + 4), BitConverter.ToSingle(bytes, o + 8));
                    int b = n.Punkt(BitConverter.ToSingle(bytes, o + 12), BitConverter.ToSingle(bytes, o + 16), BitConverter.ToSingle(bytes, o + 20));
                    int c = n.Punkt(BitConverter.ToSingle(bytes, o + 24), BitConverter.ToSingle(bytes, o + 28), BitConverter.ToSingle(bytes, o + 32));
                    n.Dreieck(a, b, c);
                }
                return "STL binär";
            }
        }
        // ASCII
        var text = Encoding.UTF8.GetString(bytes);
        var punkte = new List<int>();
        foreach (var rohZeile in text.Split('\n'))
        {
            var z = rohZeile.Trim();
            if (!z.StartsWith("vertex", StringComparison.OrdinalIgnoreCase)) continue;
            var t = z.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (t.Length < 4) continue;
            punkte.Add(n.Punkt(D(t[1]), D(t[2]), D(t[3])));
            if (punkte.Count == 3) { n.Dreieck(punkte[0], punkte[1], punkte[2]); punkte.Clear(); }
        }
        return "STL ASCII";
    }

    static double D(string s) => double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

    // ---------- OBJ ----------

    static string Obj(string pfad, Netz n)
    {
        var v = new List<int>();   // OBJ-Ecke (ab 1) → Index im verschweissten Netz
        string? gruppe = null; int erstes = 0;
        void Abschluss()
        {
            int tris = n.Dreiecke.Count / 3;
            if (gruppe is not null && tris > erstes) n.Teile.Add((gruppe, erstes, tris - 1));
            erstes = tris;
        }
        foreach (var rohZeile in File.ReadLines(pfad))
        {
            var z = rohZeile.Trim();
            if (z.Length == 0 || z[0] == '#') continue;
            var t = z.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            switch (t[0])
            {
                case "v" when t.Length >= 4:
                    v.Add(n.Punkt(D(t[1]), D(t[2]), D(t[3])));
                    break;
                case "o" or "g":
                    Abschluss();
                    gruppe = t.Length > 1 ? string.Join(" ", t.Skip(1)) : null;
                    break;
                case "f" when t.Length >= 4:
                {
                    var idx = new List<int>();
                    for (int i = 1; i < t.Length; i++)
                    {
                        var teil = t[i].Split('/')[0];
                        if (!int.TryParse(teil, out int k) || k == 0) continue;
                        idx.Add(k > 0 ? v[k - 1] : v[v.Count + k]);
                    }
                    for (int i = 1; i + 1 < idx.Count; i++) n.Dreieck(idx[0], idx[i], idx[i + 1]);
                    break;
                }
            }
        }
        Abschluss();
        return "OBJ";
    }

}
