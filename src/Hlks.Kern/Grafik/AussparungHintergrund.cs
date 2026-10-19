// Hintergrund der IFC-Unterlagen im Aussparungsplan (Modul 3b, Nachtrag B4e; Entscheid Christian 19.10.2026: keine HLKS-Leitungen im
// Aussparungsplan, «wie Vorlage» – H+K-Bürostandard nach Vorlage Aussparungsplan Bernstrasse 1.UG, 24.09.2024).
//
// GrundlagenIfc zeichnet den Hintergrund (nicht übernommene Produkte der Netzdatei) als EINEN Schnitt aller Dreiecke auf Schnitthöhe.
// Im Aussparungsplan gehören die Produkte der Gebäudetechnik nicht dazu: liegt im Hintergrund einer Unterlage mindestens ein Produkt
// mit Bereich «hlks» (Pruefkoerperbau.BereichVonEntitaet: IfcFlow…, IfcPipe…, IfcDuct…, Geräte der Gebäudetechnik), wird der Schnitt
// hier ohne diese Produkte neu gerechnet – gleicher Stil wie die gewöhnliche Grafik der Unterlage (Stift, Farbe, Deckkraft). Ohne
// HLKS-Produkte bleibt die gewöhnliche Grafik (kein zweiter Schnitt). Ausgeblendete Unterlagen und fehlende Netzdateien zeichnen wie
// gewohnt nichts.
// Zwischenspeicher: je Netzdatei (Pfad, Grösse, Änderungszeit) die Dreiecke ohne HLKS und je Schnitthöhe (auf 1 mm) die Linienzüge –
// die zuletzt benutzten Dateien wie GrundlagenIfc.

using System.Collections.Concurrent;

namespace Hlks.Kern;

internal static class AussparungHintergrund
{
    const int DateienImSpeicher = 3;
    const int HoehenJeDatei = 16;

    sealed class Daten
    {
        public required bool HatHlks { get; init; }
        public required float[] Punkte { get; init; }
        public required int[] Dreiecke { get; init; }
        public ConcurrentDictionary<long, List<List<double[]>>> Schnitte { get; } = new();
        public long Benutzt;
    }

    static readonly ConcurrentDictionary<string, Daten> dateien = new(StringComparer.OrdinalIgnoreCase);
    static long zaehler;

    /// <summary>Grafik einer Unterlage im Aussparungsplan: die gewöhnliche (normal) – bei einer IFC-Unterlage mit HLKS-Produkten im
    /// Hintergrund der Schnitt ohne diese.</summary>
    public static IEnumerable<Grafikteil> Plan(GrafikKontext ctx, Element unterlage, IEnumerable<Grafikteil> normal)
    {
        var liste = normal.ToList();
        if (liste.Count == 0 || !string.Equals(unterlage.Text("art"), "ifc", StringComparison.OrdinalIgnoreCase)) return liste;
        if (unterlage.Text("netz") is not { Length: > 0 } name) return liste;
        string pfad;
        try { pfad = Unterlagen.Pfad(ctx.Projekt, name); }
        catch (HlksFehler) { return liste; }
        if (!File.Exists(pfad)) return liste;
        var d = Laden(pfad);
        if (!d.HatHlks) return liste;
        var stil = liste.SelectMany(t => t.Primitive).FirstOrDefault()?.Stil ?? new Stil { Stift = Stifte.Duenn, Farbe = GrundlagenIfc.Farbe };
        var linien = Schnitt(d, ctx.Schnitthoehe);
        if (linien.Count == 0) return [];
        return
        [
            new Grafikteil
            {
                Id = unterlage.Id, Ebene = "grundlage",
                Primitive = linien.Select(l => new Primitiv
                {
                    Art = l.Count == 2 ? "linie" : "polylinie", Punkte = l,
                    Stil = new Stil { Stift = stil.Stift, Farbe = stil.Farbe, Deckkraft = stil.Deckkraft, Linie = stil.Linie },
                }).ToList(),
            },
        ];
    }

    static List<List<double[]>> Schnitt(Daten d, double hoehe)
    {
        d.Benutzt = Interlocked.Increment(ref zaehler);
        long schluessel = (long)Math.Round(hoehe);
        if (d.Schnitte.TryGetValue(schluessel, out var da)) return da;
        var strecken = new List<double[]>();
        Netzflaechen.Schneiden(d.Punkte, d.Dreiecke, schluessel, strecken);
        var linien = Netzflaechen.Verbinden(strecken);
        if (d.Schnitte.Count >= HoehenJeDatei) d.Schnitte.Clear();
        d.Schnitte[schluessel] = linien;
        return linien;
    }

    static Daten Laden(string pfad)
    {
        var info = new FileInfo(pfad);
        string schluessel = $"{pfad}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
        if (dateien.TryGetValue(schluessel, out var da)) { da.Benutzt = Interlocked.Increment(ref zaehler); return da; }
        var datei = IfcNetzdatei.Oeffnen(pfad);
        var punkte = new List<float>();
        var dreiecke = new List<int>();
        bool hlks = false;
        foreach (var (pr, pu, dr) in datei.Alle(nurHintergrund: true))
        {
            if (Pruefkoerperbau.BereichVonEntitaet(pr.Entitaet) == "hlks") { hlks = true; continue; }
            int basis = punkte.Count / 3;
            punkte.AddRange(pu);
            foreach (var i in dr) dreiecke.Add(basis + i);
        }
        // ohne HLKS im Hintergrund bleibt die gewöhnliche Grafik – die Dreiecke müssen nicht ein zweites Mal im Speicher liegen
        var d = new Daten { HatHlks = hlks, Punkte = hlks ? punkte.ToArray() : [], Dreiecke = hlks ? dreiecke.ToArray() : [], Benutzt = Interlocked.Increment(ref zaehler) };
        if (dateien.Count >= DateienImSpeicher)
            foreach (var alt in dateien.OrderBy(x => x.Value.Benutzt).Take(dateien.Count - DateienImSpeicher + 1).ToList()) dateien.TryRemove(alt.Key, out _);
        dateien[schluessel] = d;
        return d;
    }
}
