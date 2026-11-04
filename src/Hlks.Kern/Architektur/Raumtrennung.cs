// Raumtrennlinie (wie Revit «Raumtrennung», Christian 04.11.2026: «es sollen Räume innerhalb geschlossener Struktur per Klick
// automatisch generiert werden können. dazu gibt es ein trennwerkzeug, wo grosse zonen untertzeilt werden können»).
// - Klasse Raumtrennung: Linie ohne Dicke im Geschoss. Sie begrenzt Räume wie eine Wand – raum_erkennen schliesst damit offene
//   Bereiche (Wohnküche, Loggia) und unterteilt grosse Flächen.
// - raumtrennung_zeichnen legt die Linien an und teilt jeden Raum des Geschosses, den sie ganz durchqueren: der grösste Teil
//   bleibt der bestehende Raum (Kennzeichen, Nummer, Eigenschaften), jeder weitere Teil wird ein neuer Raum mit den
//   Eigenschaften des alten ohne Nummer. Reicht die Linie nicht von Rand zu Rand, bleibt der Raum unverändert (Hinweis).
// - Rechnung: die Linie steht als schmales Band (Breite Band) in der Polygonrechnung; Eckpunkte innerhalb des Bands werden danach
//   auf die Linie gezogen, damit die Teile genau an der Linie aneinanderstossen und die Flächen sich zur alten Fläche ergänzen.

using System.Text.Json.Nodes;
using Clipper2Lib;

namespace Hlks.Kern;

public static class Raumtrennungen
{
    /// <summary>Breite des Bands einer Trennlinie in der Polygonrechnung (mm). Annahme Hauptlauf 04.11.2026: klein gegen jede
    /// Bautoleranz; die Eckpunkte werden danach auf die Linie gezogen, die Breite geht also nicht in die Fläche ein.</summary>
    public const double Band = 1;

    /// <summary>Kleinste Fläche eines Teils, der beim Teilen ein Raum wird (mm²); kleinere Teile sind Splitter der Bandrechnung.
    /// Annahme Hauptlauf: 0.01 m².</summary>
    const double KleinsterTeil = 1e4;

    /// <summary>Strecken der Trennlinien eines Geschosses.</summary>
    public static List<(double[] A, double[] B)> DesGeschosses(Modell m, Katalog k, string geschossId) =>
        m.Elemente.Where(e => k.IstArt(e, "Raumtrennung") && e.Geschoss == geschossId)
            .SelectMany(e => Strecken(e.Geometrie)).ToList();

    static IEnumerable<(double[] A, double[] B)> Strecken(Geometrie? g)
    {
        if (g is null) yield break;
        for (int i = 1; i < g.Punkte.Count; i++)
            if (Geometrie.Abstand(g.Punkte[i - 1], g.Punkte[i]) > 1e-6) yield return (g.Punkte[i - 1], g.Punkte[i]);
    }

    /// <summary>Band um eine Strecke (Rechteck der Breite Band, an den Enden um Band/2 verlängert).</summary>
    public static PathD Bandpfad((double[] A, double[] B) s)
    {
        double dx = s.B[0] - s.A[0], dy = s.B[1] - s.A[1], l = Math.Sqrt(dx * dx + dy * dy), h = Band / 2;
        double ux = dx / l, uy = dy / l, nx = -uy * h, ny = ux * h, ex = ux * h, ey = uy * h;
        return Polygone.Ccw(new PathD
        {
            new(s.A[0] - ex + nx, s.A[1] - ey + ny), new(s.A[0] - ex - nx, s.A[1] - ey - ny),
            new(s.B[0] + ex - nx, s.B[1] + ey - ny), new(s.B[0] + ex + nx, s.B[1] + ey + ny),
        });
    }

    /// <summary>Zieht Eckpunkte, die im Band einer Strecke liegen, auf die Linie und entfernt danach doppelte Punkte und
    /// Spitzen (Hin und zurück auf derselben Stelle).</summary>
    public static List<double[]> AufLinien(List<double[]> ring, IReadOnlyList<(double[] A, double[] B)> strecken)
    {
        var aus = new List<double[]>();
        foreach (var p in ring)
        {
            var q = p;
            foreach (var s in strecken)
                if (Polygone.AbstandZurStrecke(p[0], p[1], s.A[0], s.A[1], s.B[0], s.B[1]) <= Band * 0.75) { q = Lot(p, s); break; }
            aus.Add(q);
        }
        bool geaendert = true;
        while (geaendert && aus.Count > 3)
        {
            geaendert = false;
            for (int i = 0; i < aus.Count && aus.Count > 3; i++)
            {
                var a = aus[(i + aus.Count - 1) % aus.Count]; var b = aus[i]; var c = aus[(i + 1) % aus.Count];
                if (Geometrie.Abstand(a, b) < 0.01 || Geometrie.Abstand(a, c) < 0.01 || Kollinear(a, b, c))
                { aus.RemoveAt(i); geaendert = true; break; }
            }
        }
        return aus;
    }

    static bool Kollinear(double[] a, double[] b, double[] c) =>
        Math.Abs((b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])) < 1e-3 * Math.Max(1, Geometrie.Abstand(a, c));

    static double[] Lot(double[] p, (double[] A, double[] B) s)
    {
        double dx = s.B[0] - s.A[0], dy = s.B[1] - s.A[1], t = ((p[0] - s.A[0]) * dx + (p[1] - s.A[1]) * dy) / (dx * dx + dy * dy);
        t = Math.Clamp(t, 0, 1);
        return [s.A[0] + t * dx, s.A[1] + t * dy];
    }

    /// <summary>Teilt einen Raumring mit Strecken: Teile ab KleinsterTeil, Eckpunkte auf den Linien. Ein Teil = nicht geteilt.</summary>
    public static List<List<double[]>> Teilen(IReadOnlyList<double[]> ring, IReadOnlyList<(double[] A, double[] B)> strecken)
    {
        var teile = Polygone.Abziehen([Polygone.Ccw(Polygone.Pfad(ring))], new PathsD(strecken.Select(Bandpfad)));
        return teile.Where(t => Clipper.Area(t) >= KleinsterTeil)   // Löcher (negativ) und Splitter weg
            .Select(t => AufLinien(Polygone.Ring(t, true), strecken))
            .OrderByDescending(t => Math.Abs(Polygone.Flaecheninhalt(t))).ToList();
    }
}

public sealed class RaumtrennungZeichnen : IBefehl
{
    public string Name => "raumtrennung_zeichnen";
    public string Beschreibung => Text;
    public const string Text = "Zeichnet Raumtrennlinien (Linie ohne Dicke, wie Revit «Raumtrennung») entlang von Punkten, eine je gerades Stück. Jeder Raum des Geschosses, den die Linien ganz durchqueren, wird geteilt: der grösste Teil bleibt der Raum, die übrigen werden neue Räume mit seinen Eigenschaften ohne Nummer. raum_erkennen behandelt Trennlinien wie Wände.";
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("geschoss", "text", true, "Geschoss (Kennzeichen oder Id)"),
        new("punkte", "liste", true, "Linienzug [[x,y], …] in mm, mindestens zwei Punkte"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var geschoss = ArchParam.Bezug(k, p, "geschoss", "Geschoss");
        var punkte = Param.Punkte(p, "punkte");
        if (punkte.Count < 2) throw new HlksFehler("Für eine Raumtrennung braucht es mindestens zwei Punkte.", "Anfangs- und Endpunkt der Linie angeben.");
        double kote = geschoss.Zahl("kote") ?? 0;
        var strecken = new List<(double[] A, double[] B)>();
        var neu = new List<string>();
        for (int i = 1; i < punkte.Count; i++)
        {
            double[] a = [punkte[i - 1][0], punkte[i - 1][1]], b = [punkte[i][0], punkte[i][1]];
            if (Geometrie.Abstand(a, b) < 1) continue;
            new ElementAnlegen().Ausfuehren(k, new JsonObject
            {
                ["klasse"] = "Raumtrennung", ["geschoss"] = geschoss.Id,
                ["geometrie"] = new JsonObject { ["art"] = "linie", ["punkte"] = new JsonArray(new JsonArray(a[0], a[1], kote), new JsonArray(b[0], b[1], kote)) },
            });
            neu.Add(k.Arbeit.Finde(k.Operationen[^1].Id)!.Kennzeichen);
            strecken.Add((a, b));
        }
        if (strecken.Count == 0) throw new HlksFehler("Die Punkte liegen aufeinander – keine Linie.", "Zwei verschiedene Punkte angeben.");
        k.Hinweise.Add($"Raumtrennung {string.Join(", ", neu)} gezeichnet");

        foreach (var raum in k.Arbeit.DerKlasse("Raum").Where(r => r.Geschoss == geschoss.Id && r.Geometrie is { Art: "polygon" } g && g.Punkte.Count >= 3).ToList())
        {
            var ring = raum.Geometrie!.Punkte;
            // nur Räume, die eine der Strecken überhaupt berührt
            if (!strecken.Any(s => Polygone.Abziehen([Polygone.Ccw(Polygone.Pfad(ring))], [Raumtrennungen.Bandpfad(s)]).Sum(t => Clipper.Area(t)) < Math.Abs(Polygone.Flaecheninhalt(ring)) - 1))
                continue;
            var teile = Raumtrennungen.Teilen(ring, strecken);
            if (teile.Count < 2)
            {
                k.Hinweise.Add($"{raum.Kennzeichen} nicht geteilt: die Linie reicht nicht von Rand zu Rand.");
                continue;
            }
            double z = ring[0].Length > 2 ? ring[0][2] : kote;
            JsonArray Punkte(List<double[]> t) => new(t.Select(q => (JsonNode)new JsonArray(q[0], q[1], z)).ToArray());
            new ElementAendern().Ausfuehren(k, new JsonObject
            {
                ["element"] = raum.Id,
                ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = Punkte(teile[0]), ["hoehe"] = raum.Geometrie.Hoehe },
            });
            var namen = new List<string>();
            foreach (var t in teile.Skip(1))
            {
                var eig = new JsonObject();
                foreach (var (n, w) in raum.Eigenschaften.Where(x => x.Key != "nummer"))
                    eig[n] = w.Einheit is null ? w.Inhalt?.DeepClone() : new JsonObject { ["wert"] = w.Inhalt?.DeepClone(), ["einheit"] = w.Einheit };
                var anlegen = new JsonObject
                {
                    ["klasse"] = raum.Klasse, ["geschoss"] = geschoss.Id, ["eigenschaften"] = eig,
                    ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = Punkte(t), ["hoehe"] = raum.Geometrie.Hoehe },
                };
                if (raum.TeilVon is not null) anlegen["teilVon"] = raum.TeilVon;
                new ElementAnlegen().Ausfuehren(k, anlegen);
                var r = k.Arbeit.Finde(k.Operationen[^1].Id)!;
                namen.Add($"{r.Kennzeichen} ({Grafik.Format(Math.Abs(Polygone.Flaecheninhalt(t)) / 1e6, 2)} m²)");
            }
            k.Hinweise.Add($"{raum.Kennzeichen} geteilt: bleibt {Grafik.Format(Math.Abs(Polygone.Flaecheninhalt(teile[0])) / 1e6, 2)} m², neu {string.Join(", ", namen)} – Nummer vergeben.");
        }
    }
}
