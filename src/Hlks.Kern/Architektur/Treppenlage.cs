// Lesesicht auf ein Treppen-Element (Nachbearbeitung N17, Christian 21.10.2026: «auch treppen sind im Grundriss noch nicht
// korrekt angezeigt. resp. nicht erkennbar.»). Die Treppe ist ein Lauf: Geometrie = Lauflinie [x, y, z] (Polylinie) vom Antritt
// zum Austritt; Stufenkanten und Podeste stehen als Listen in den Eigenschaften (klassen.json, Klasse Treppe). Diese Datei liest
// sie, rechnet die Trittflächen (Grundlage von Grundriss-Treffer und Körper) und bildet sie bei Verschieben, Drehen und Spiegeln
// mit der Lauflinie ab.
//
// Konventionen (Annahmen, im Katalog beschrieben):
//   - Eine Stufenkante läuft von der rechten zur linken Seite des Laufs in Gehrichtung (aufwärts) gesehen; z = Trittfläche, zu der
//     die Steigung hinaufführt. Die Steigung 1 liegt an der ersten Kante, die letzte Kante (am Austritt) hat z = Oberkante der letzten
//     Steigung, ausser der Lauf endet auf einer Trittfläche in Höhe des Bodens (dann fehlt sie).
//   - Die Trittfläche i liegt zwischen Kante i und Kante i + 1 und hat die Höhe z der Kante i. Ein Podest ist ein Umriss mit seiner
//     Höhe; die Kante, die an ihm beginnt, gehört zum Podest (keine Trittfläche zwischen ihr und der nächsten Kante).

using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <summary>Ein Podest: Oberkante z und Umriss [x, y] im Gegenuhrzeigersinn.</summary>
public sealed record TreppePodest(double Z, List<double[]> Ring);

/// <summary>Eine Trittfläche (Umriss im Gegenuhrzeigersinn) mit ihrer Höhe; Podest = Zwischenpodest bzw. Antrittspodest.</summary>
public sealed record Trittflaeche(List<double[]> Ring, double Z, bool Podest);

public sealed class Treppenlage
{
    public required Element Element { get; init; }
    /// <summary>Lauflinie [x, y, z], Reihenfolge wie im Element (bei richtung abwaerts vom oberen zum unteren Ende).</summary>
    public List<double[]> Lauflinie { get; init; } = [];
    /// <summary>Stufenkanten [x1, y1, x2, y2, z], rechts → links in Gehrichtung aufwärts.</summary>
    public List<double[]> Kanten { get; init; } = [];
    public List<TreppePodest> Podeste { get; init; } = [];
    public double? Stufen { get; init; }
    public double? Steigung { get; init; }
    public double? Auftritt { get; init; }
    public double? Laufbreite { get; init; }
    public double? Unterkante { get; init; }
    public double? Oberkante { get; init; }
    public double? Laufdicke { get; init; }
    public bool Aufwaerts { get; init; } = true;

    /// <summary>Vorgabe der Laufdicke (senkrecht unter den Trittflächen), wenn das Element keine nennt.</summary>
    public const double VorgabeLaufdicke = 250;

    /// <summary>Zwei Punkte, die weniger als diesen Abstand (mm) haben, gelten als derselbe (Kanten am Podest).</summary>
    const double Toleranz = 15;

    /// <summary>Lesesicht oder null, wenn das Element weder Stufenkanten noch Podeste noch eine Lauflinie hat.</summary>
    public static Treppenlage? Von(Element e)
    {
        var kanten = new List<double[]>();
        if (e.Eigenschaft("stufenkanten")?.Inhalt is JsonArray ka)
            foreach (var k in ka)
                if (Zahlen(k) is { Length: >= 5 } z) kanten.Add(z[..5]);
        var podeste = new List<TreppePodest>();
        if (e.Eigenschaft("podeste")?.Inhalt is JsonArray pa)
            foreach (var p in pa)
                if (p is JsonObject o && Json.Zahl(o["z"]) is double pz && o["punkte"] is JsonArray pp)
                {
                    var ring = pp.Select(Zahlen).OfType<double[]>().Where(q => q.Length >= 2).Select(q => new[] { q[0], q[1] }).ToList();
                    if (ring.Count >= 3) podeste.Add(new TreppePodest(pz, ring));
                }
        var lauf = e.Geometrie?.Punkte.Where(q => q.Length >= 2).Select(q => q.Length > 2 ? q : [q[0], q[1], 0]).ToList() ?? [];
        if (kanten.Count == 0 && podeste.Count == 0 && lauf.Count < 2) return null;
        return new Treppenlage
        {
            Element = e, Lauflinie = lauf, Kanten = kanten, Podeste = podeste,
            Stufen = e.Zahl("stufen"), Steigung = e.Zahl("steigung"), Auftritt = e.Zahl("auftritt"), Laufbreite = e.Zahl("laufbreite"),
            Unterkante = e.Zahl("unterkante"), Oberkante = e.Zahl("oberkante"), Laufdicke = e.Zahl("laufdicke"),
            Aufwaerts = !string.Equals(e.Text("richtung"), "abwaerts", StringComparison.OrdinalIgnoreCase),
        };
    }

    static double[]? Zahlen(JsonNode? n) =>
        n is JsonArray a && a.Select(x => Json.Zahl(x)).ToList() is { } l && l.All(x => x is not null) ? l.Select(x => x!.Value).ToArray() : null;

    /// <summary>Spitze des Pfeils: das obere Ende der Lauflinie.</summary>
    public double[] Austritt => Aufwaerts ? Gehlinie[^1] : Gehlinie[0];
    /// <summary>Antritt (Kreis): das untere Ende der Lauflinie.</summary>
    public double[] Antritt => Aufwaerts ? Gehlinie[0] : Gehlinie[^1];

    /// <summary>Lauflinie des Elements; ohne sie (nur Stufenkanten) die Mitten der Stufenkanten.</summary>
    public List<double[]> Gehlinie => Lauflinie.Count >= 2 ? Lauflinie
        : Kanten.Select(k => new[] { (k[0] + k[2]) / 2, (k[1] + k[3]) / 2, k[4] }).ToList();

    /// <summary>Höhe z, ab der oben alles gestrichelt ist, gehört zur Ansicht – hier die Höhe der Kante.</summary>
    public double KanteZ(int i) => Kanten[i][4];

    /// <summary>Gehört die Kante i zu einem Podest (liegt auf seinem Rand in dessen Höhe)?</summary>
    public bool IstPodestKante(int i)
    {
        var k = Kanten[i];
        double mx = (k[0] + k[2]) / 2, my = (k[1] + k[3]) / 2;
        foreach (var p in Podeste)
        {
            if (Math.Abs(p.Z - k[4]) > 5) continue;
            for (int j = 0; j < p.Ring.Count; j++)
                if (AbstandStrecke(mx, my, p.Ring[j], p.Ring[(j + 1) % p.Ring.Count]) <= Toleranz) return true;
        }
        return false;
    }

    static double AbstandStrecke(double px, double py, double[] a, double[] b)
    {
        double dx = b[0] - a[0], dy = b[1] - a[1], l2 = dx * dx + dy * dy;
        double t = l2 < 1e-12 ? 0 : Math.Clamp(((px - a[0]) * dx + (py - a[1]) * dy) / l2, 0, 1);
        double x = a[0] + t * dx - px, y = a[1] + t * dy - py;
        return Math.Sqrt(x * x + y * y);
    }

    /// <summary>Indizes i, für die zwischen Stufenkante i und i + 1 eine Trittfläche liegt: Kante i gehört nicht zu einem Podest, und
    /// die Mitten der beiden Kanten liegen höchstens das 2.5fache des Auftritts auseinander (sonst getrennte Läufe).</summary>
    public List<int> TrittIndizes()
    {
        var aus = new List<int>();
        var abstaende = new List<double>();
        for (int i = 0; i + 1 < Kanten.Count; i++) abstaende.Add(Mitteabstand(Kanten[i], Kanten[i + 1]));
        double typisch = Auftritt is double a && a > 0 ? a : abstaende.Count > 0 ? abstaende.Order().ElementAt(abstaende.Count / 2) : 280;
        for (int i = 0; i + 1 < Kanten.Count; i++)
            if (!IstPodestKante(i) && abstaende[i] <= 2.5 * typisch) aus.Add(i);
        return aus;
    }

    /// <summary>Trittflächen und Podeste: je zwei aufeinanderfolgende Stufenkanten (<see cref="TrittIndizes"/>) ein Viereck in der Höhe
    /// der unteren Kante, dazu die Podeste. Umrisse im Gegenuhrzeigersinn.</summary>
    public List<Trittflaeche> Tritte()
    {
        var aus = new List<Trittflaeche>();
        foreach (var i in TrittIndizes())
        {
            var k = Kanten[i]; var n = Kanten[i + 1];
            var ring = new List<double[]> { new[] { k[0], k[1] }, new[] { k[2], k[3] }, new[] { n[2], n[3] }, new[] { n[0], n[1] } };
            if (Polygone.Flaecheninhalt(ring) < 0) ring.Reverse();
            if (Math.Abs(Polygone.Flaecheninhalt(ring)) < 1) continue;
            aus.Add(new Trittflaeche(ring, k[4], false));
        }
        foreach (var p in Podeste) aus.Add(new Trittflaeche(p.Ring, p.Z, true));
        return aus;
    }

    static double Mitteabstand(double[] a, double[] b)
    {
        double dx = (a[0] + a[2]) / 2 - (b[0] + b[2]) / 2, dy = (a[1] + a[3]) / 2 - (b[1] + b[3]) / 2;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    // ------------------------------------------------------------------ Abbilden (Verschieben, Drehen, Spiegeln)

    /// <summary>Stufenkanten und Podeste mit derselben Abbildung wie die Lauflinie (starre Bewegung bzw. Spiegelung der Ebene; z wird
    /// mit der Abbildung der Punkte gerechnet). <paramref name="spiegelt"/>: die Abbildung kehrt den Umlaufsinn um – die Kanten
    /// tauschen ihre Enden (rechts bleibt rechts), die Podestumrisse laufen wieder im Gegenuhrzeigersinn. Liefert die neuen Werte der
    /// Eigenschaften «stufenkanten» und «podeste» (nur, was das Element hat).</summary>
    public static JsonObject? Abbilden(Element e, Func<double[], double[]> f, bool spiegelt)
    {
        if (Von(e) is not { } t) return null;
        var aus = new JsonObject();
        if (t.Kanten.Count > 0)
        {
            var neu = new JsonArray();
            foreach (var k in t.Kanten)
            {
                var a = f([k[0], k[1], k[4]]); var b = f([k[2], k[3], k[4]]);
                if (spiegelt) (a, b) = (b, a);
                neu.Add(new JsonArray(Runden(a[0]), Runden(a[1]), Runden(b[0]), Runden(b[1]), Runden(a.Length > 2 ? a[2] : k[4])));
            }
            aus["stufenkanten"] = neu;
        }
        if (t.Podeste.Count > 0)
        {
            var neu = new JsonArray();
            foreach (var p in t.Podeste)
            {
                var ring = p.Ring.Select(q => f([q[0], q[1], p.Z])).ToList();
                if (spiegelt) ring.Reverse();
                double z = ring.Count > 0 && ring[0].Length > 2 ? ring[0][2] : p.Z;
                neu.Add(new JsonObject { ["z"] = Runden(z), ["punkte"] = new JsonArray(ring.Select(q => (JsonNode)new JsonArray(Runden(q[0]), Runden(q[1]))).ToArray()) });
            }
            aus["podeste"] = neu;
        }
        var p0 = t.Gehlinie.Count > 0 ? t.Gehlinie[0] : new double[] { 0, 0, 0 };
        if (t.Unterkante is double u) aus["unterkante"] = Runden(f([p0[0], p0[1], u])[2]);
        if (t.Oberkante is double o) aus["oberkante"] = Runden(f([p0[0], p0[1], o])[2]);
        return aus.Count == 0 ? null : aus;
    }

    public static double Runden(double x) => Math.Round(x, 1);

    // ------------------------------------------------------------------ JSON

    public static JsonArray KantenJson(IEnumerable<double[]> kanten) =>
        new(kanten.Select(k => (JsonNode)new JsonArray(Runden(k[0]), Runden(k[1]), Runden(k[2]), Runden(k[3]), Runden(k[4]))).ToArray());

    public static JsonArray PodesteJson(IEnumerable<TreppePodest> podeste) =>
        new(podeste.Select(p => (JsonNode)new JsonObject
        {
            ["z"] = Runden(p.Z),
            ["punkte"] = new JsonArray(p.Ring.Select(q => (JsonNode)new JsonArray(Runden(q[0]), Runden(q[1]))).ToArray()),
        }).ToArray());
}
