// Grafik der IFC-Unterlagen (Modul 1b, Auftrag B1): Hintergrund = die nicht übernommenen Produkte der Netzdatei.
// Grundriss: Schnitt ihrer Dreiecke mit der Ebene z = Schnitthöhe als Linienzüge – Stift dünn, Farbe grau #8a8a8a (Annahme
// Spezifikation «Hintergrundfarbe grau»), Ebene «grundlage», Deckkraft der Unterlage (Vorgabe 0.5). Körper: ein Teil Art
// «netz» mit Quelle = Id der Unterlage; das 3D lädt die Netzdatei über REST (Oberfläche C1) und zeigt nur die Produkte mit
// Kennzeichen 0 (Hintergrund).
// Schnell genug für grosse Dateien: die Hintergrund-Dreiecke einer Netzdatei bleiben im Speicher (die zuletzt benutzten
// Dateien), das Schnittergebnis je Höhe ebenfalls.

using System.Collections.Concurrent;

namespace Hlks.Kern;

public sealed class GrundlagenIfc : IGrundlagenArt
{
    public const string Farbe = "#8a8a8a";
    const double VorgabeDeckkraft = 0.5;
    const int DateienImSpeicher = 3;
    const int HoehenJeDatei = 16;

    public bool Fuer(string art) => string.Equals(art, "ifc", StringComparison.OrdinalIgnoreCase);

    public IEnumerable<Grafikteil> Plan(GrafikKontext ctx, Element unterlage)
    {
        if (Pfad(ctx.Projekt, unterlage) is not { } pfad) return [];
        var linien = Schnitt(pfad, ctx.Schnitthoehe);
        if (linien.Count == 0) return [];
        var stil = new Stil { Stift = Stifte.Duenn, Farbe = Farbe, Deckkraft = unterlage.Zahl("deckkraft") ?? VorgabeDeckkraft };
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
            Teile = [new Koerperteil { Art = "netz", Quelle = unterlage.Id, Material = "sonst", Farbe = Farbe, Deckkraft = unterlage.Zahl("deckkraft") ?? VorgabeDeckkraft }],
        };
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
        public long Benutzt;
    }

    static readonly ConcurrentDictionary<string, Hintergrund> dateien = new(StringComparer.OrdinalIgnoreCase);
    static long zaehler;

    /// <summary>Schnitt der Hintergrund-Netze mit z = hoehe als Linienzüge [x, y] (je Höhe auf 1 mm zwischengespeichert).</summary>
    public static List<List<double[]>> Schnitt(string netzdatei, double hoehe)
    {
        var h = Laden(netzdatei);
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

    static Hintergrund Laden(string pfad)
    {
        if (dateien.TryGetValue(pfad, out var h)) return h;
        var datei = IfcNetzdatei.Oeffnen(pfad);
        var punkte = new List<float>();
        var dreiecke = new List<int>();
        float zmin = float.MaxValue, zmax = float.MinValue;
        foreach (var (pr, pu, dr) in datei.Alle(nurHintergrund: true))
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
        dateien[pfad] = h;
        return h;
    }
}
