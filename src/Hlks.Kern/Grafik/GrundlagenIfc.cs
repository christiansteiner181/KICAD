// Grafik der IFC-Unterlagen (Modul 1b, Auftrag B1): Hintergrund = die nicht übernommenen Produkte der Netzdatei. N12: je nach
// Ansichtseigenschaft ifcDarstellung (IfcDarstellung.cs) stattdessen alle Produkte (Original) – im Grundriss, im Schnitt und in der
// Ansicht (Schnitt des Netzes mit der Schnittebene), in der Farbe farbeOriginal der Unterlage.
// Grundriss: Schnitt ihrer Dreiecke mit der Ebene z = Schnitthöhe als Linienzüge – Stift dünn, Farbe grau #8a8a8a (Annahme
// Spezifikation «Hintergrundfarbe grau»), Ebene «grundlage», Deckkraft der Unterlage (Vorgabe 0.5). Körper: ein Teil Art
// «netz» mit Quelle = Id der Unterlage; das 3D lädt die Netzdatei über REST (Oberfläche C1) und zeigt nur die Produkte mit
// Kennzeichen 0 (Hintergrund).
// Schnell genug für grosse Dateien: die Hintergrund-Dreiecke einer Netzdatei bleiben im Speicher (die zuletzt benutzten
// Dateien), das Schnittergebnis je Höhe ebenfalls.

using System.Collections.Concurrent;
using System.Globalization;

namespace Hlks.Kern;

public sealed class GrundlagenIfc : IGrundlagenArt
{
    public const string Farbe = "#8a8a8a";   // Vorgabe von farbeOriginal
    const double VorgabeDeckkraft = 0.5;
    const int DateienImSpeicher = 3;
    const int HoehenJeDatei = 16;

    public bool Fuer(string art) => string.Equals(art, "ifc", StringComparison.OrdinalIgnoreCase);

    public IEnumerable<Grafikteil> Plan(GrafikKontext ctx, Element unterlage)
    {
        if (Pfad(ctx.Projekt, unterlage) is not { } pfad) return [];
        var linien = Schnitt(pfad, ctx.Schnitthoehe, !IfcDarstellung.ZeigtOriginal(IfcDarstellung.Modus(ctx.Ansicht)));
        if (linien.Count == 0) return [];
        var stil = OriginalStil(unterlage);
        return
        [
            new Grafikteil
            {
                Id = unterlage.Id, Ebene = "grundlage",
                Primitive = linien.Select(l => new Primitiv { Art = l.Count == 2 ? "linie" : "polylinie", Punkte = l, Stil = stil }).ToList(),
            },
        ];
    }

    public Koerper? Koerper(Projekt p, Element unterlage)
    {
        if (Pfad(p, unterlage) is null) return null;
        return new Koerper
        {
            Id = unterlage.Id, Ebene = "grundlage",
            Teile = [new Koerperteil { Art = "netz", Quelle = unterlage.Id, Material = "sonst", Farbe = IfcDarstellung.Farbe(unterlage, "farbeOriginal") ?? Farbe, Deckkraft = unterlage.Zahl("deckkraft") ?? VorgabeDeckkraft }],
        };
    }

    /// <summary>Stil des Originals: dünn, Farbe farbeOriginal (Vorgabe grau), Deckkraft der Unterlage (Vorgabe 0.5).</summary>
    static Stil OriginalStil(Element unterlage) =>
        new() { Stift = Stifte.Duenn, Farbe = IfcDarstellung.Farbe(unterlage, "farbeOriginal") ?? Farbe, Deckkraft = unterlage.Zahl("deckkraft") ?? VorgabeDeckkraft };

    /// <summary>Schnitt und Ansicht (N12): das Original aller Produkte, geschnitten mit der Schnittebene w = 0, als Linienzüge (u, v) dünn in der
    /// Farbe des Originals. Nur wenn die Ansicht das Original zeigt (ifcDarstellung original oder beide). Annahme: die Ansicht (ohne
    /// Schnittflächen) zeigt dieselben Schnittlinien in der Ebene der Ansichtslinie; eine Projektion der Fassade ist offen.</summary>
    internal static IEnumerable<Grafikteil> Ansicht(GrafikKontext ctx, Schnittraum raum)
    {
        if (!IfcDarstellung.ZeigtOriginal(IfcDarstellung.Modus(ctx.Ansicht)) || ctx.KlassenAus.Contains("Unterlage")) return [];
        var teile = new List<Grafikteil>();
        foreach (var u in ctx.Modell.Elemente)
        {
            if (u.Klasse != "Unterlage" || u.Text("art") != "ifc" || !Grafik.Bool(u, "anzeigen", true) || Pfad(ctx.Projekt, u) is not { } pfad) continue;
            var linien = SchnittSenkrecht(pfad, raum);
            if (linien.Count == 0) continue;
            var stil = OriginalStil(u);
            teile.Add(new Grafikteil
            {
                Id = u.Id, Ebene = "grundlage",
                Primitive = linien.Select(l => new Primitiv { Art = l.Count == 2 ? "linie" : "polylinie", Punkte = l, Stil = stil }).ToList(),
            });
        }
        return teile;
    }

    /// <summary>Pfad der Netzdatei einer Unterlage oder null (keine Netzdatei, Datei fehlt).</summary>
    static string? Pfad(Projekt p, Element unterlage)
    {
        if (unterlage.Text("netz") is not { Length: > 0 } name) return null;
        try { return Unterlagen.Pfad(p, name); }
        catch (HlksFehler) { return null; }
    }

    // ------------------------------------------------------------------ Zwischenspeicher

    sealed class Hintergrund
    {
        public required float[] Punkte { get; init; }
        public required int[] Dreiecke { get; init; }
        public required float ZMin { get; init; }
        public required float ZMax { get; init; }
        public ConcurrentDictionary<long, List<List<double[]>>> Schnitte { get; } = new();
        /// <summary>Schnitte mit senkrechten Ebenen (Schnitt, Ansicht), je Lage der Ebene.</summary>
        public ConcurrentDictionary<string, List<List<double[]>>> Ebenen { get; } = new();
        public long Benutzt;
    }

    static readonly ConcurrentDictionary<string, Hintergrund> dateien = new(StringComparer.OrdinalIgnoreCase);
    static long zaehler;

    /// <summary>Schnitt der Hintergrund-Netze (nurHintergrund) bzw. aller Produkte (N12: Original) mit z = hoehe als Linienzüge [x, y]
    /// (je Höhe auf 1 mm zwischengespeichert).</summary>
    public static List<List<double[]>> Schnitt(string netzdatei, double hoehe, bool nurHintergrund = true)
    {
        var h = Laden(netzdatei, nurHintergrund);
        h.Benutzt = Interlocked.Increment(ref zaehler);
        long schluessel = (long)Math.Round(hoehe);
        if (h.Schnitte.TryGetValue(schluessel, out var da)) return da;
        var strecken = new List<double[]>();
        if (schluessel >= h.ZMin && schluessel <= h.ZMax) Netzflaechen.Schneiden(h.Punkte, h.Dreiecke, schluessel, strecken);
        var linien = Netzflaechen.Verbinden(strecken);
        if (h.Schnitte.Count >= HoehenJeDatei) h.Schnitte.Clear();
        h.Schnitte[schluessel] = linien;
        return linien;
    }

    /// <summary>Schnitt aller Produkte mit der senkrechten Schnittebene w = 0 eines Schnitts bzw. einer Ansicht als Linienzüge [u, v = z].
    /// Dieselbe Schnittfunktion wie im Grundriss (Netzflaechen.Schneiden), auf die Punkte in (u, z, w) angewendet.</summary>
    internal static List<List<double[]>> SchnittSenkrecht(string netzdatei, Schnittraum raum)
    {
        var h = Laden(netzdatei, false);
        h.Benutzt = Interlocked.Increment(ref zaehler);
        var schluessel = string.Create(CultureInfo.InvariantCulture, $"{raum.Ax:F1}|{raum.Ay:F1}|{raum.Dx:F7}|{raum.Dy:F7}|{raum.Nx:F7}|{raum.Ny:F7}");
        if (h.Ebenen.TryGetValue(schluessel, out var da)) return da;
        var q = new float[h.Punkte.Length];
        for (int i = 0; i + 2 < q.Length; i += 3)
        {
            double x = h.Punkte[i], y = h.Punkte[i + 1];
            q[i] = (float)raum.U(x, y); q[i + 1] = h.Punkte[i + 2]; q[i + 2] = (float)raum.W(x, y);
        }
        var strecken = new List<double[]>();
        Netzflaechen.Schneiden(q, h.Dreiecke, 0, strecken);
        var linien = Netzflaechen.Verbinden(strecken);
        if (h.Ebenen.Count >= HoehenJeDatei) h.Ebenen.Clear();
        h.Ebenen[schluessel] = linien;
        return linien;
    }

    static Hintergrund Laden(string pfad, bool nurHintergrund)
    {
        var key = pfad + (nurHintergrund ? "|h" : "|a");
        if (dateien.TryGetValue(key, out var h)) return h;
        var datei = IfcNetzdatei.Oeffnen(pfad);
        var punkte = new List<float>();
        var dreiecke = new List<int>();
        float zmin = float.MaxValue, zmax = float.MinValue;
        foreach (var (pr, pu, dr) in datei.Alle(nurHintergrund))
        {
            int basis = punkte.Count / 3;
            punkte.AddRange(pu);
            foreach (var i in dr) dreiecke.Add(basis + i);
            zmin = Math.Min(zmin, pr.Begrenzung[2]);
            zmax = Math.Max(zmax, pr.Begrenzung[5]);
        }
        h = new Hintergrund { Punkte = punkte.ToArray(), Dreiecke = dreiecke.ToArray(), ZMin = zmin, ZMax = zmax };
        if (dateien.Count >= DateienImSpeicher)
            foreach (var alt in dateien.OrderBy(x => x.Value.Benutzt).Take(dateien.Count - DateienImSpeicher + 1).ToList())
                dateien.TryRemove(alt.Key, out _);
        dateien[key] = h;
        return h;
    }
}
