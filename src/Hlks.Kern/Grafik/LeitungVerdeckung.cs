// Kreuzende Leitungen im Grundriss (Christian 12.11.2026: «leitungen die in einem Grundriss unter einer anderen Leitung durchlaufen (inkl. Dämmung)
// müssen verdeckt dargestellt werden», «bei grob wird die verdeckte leitung unterbrochen»): Läuft eine waagrechte Leitung unter einer höheren durch,
// ist sie dort verdeckt. Deckfläche der höheren Leitung = ihr Band bzw. ihre Dämmung (die breitere), in grob ihre Strichbreite plus eine Lücke.
//   mittel, fein: Füllung der tieferen Leitung unter der Deckfläche ausgespart, Ränder und Achse dort gestrichelt (Ränder dünn, Stift hilf).
//   grob: die tiefere Linie ist an der Deckfläche unterbrochen.
// Höher wie in DaemmGrafik: tiefste Achshöhe der waagrechten Stücke über der höchsten der anderen Leitung (+1 mm); gleich hohe bzw. sich
// überlappende Höhen (Gefälle) verdecken einander nicht. Senkrechte Stücke (Steigleitungen) und Formteile verdecken hier nicht.
// ANNAHME Hauptlauf (Gestaltung): Lücke in grob je Seite 0.5 mm Papier.

using System.Runtime.CompilerServices;
using Clipper2Lib;

namespace Hlks.Kern;

public static class LeitungVerdeckung
{
    const double Eps = 1.0;              // mm Achshöhe
    const double LueckeGrob = 0.5;       // mm Papier je Seite

    sealed record Decke(string Id, PathsD Flaeche, double ZMin, double ZMax, RectD Box);

    static readonly ConditionalWeakTable<GrafikKontext, List<Decke>> decken = new();

    /// <summary>Waagrechte Stücke einer Leitung als Züge im Grundriss und ihre Achshöhen (min, max); null ohne waagrechte Stücke.</summary>
    static (List<List<double[]>> Zuege, double ZMin, double ZMax)? Waagrecht(List<double[]> pts)
    {
        var zuege = new List<List<double[]>>(); List<double[]>? zug = null;
        double zmin = double.MaxValue, zmax = double.MinValue;
        for (int i = 1; i < pts.Count; i++)
        {
            var a = pts[i - 1]; var b = pts[i];
            if (Math.Abs(b[0] - a[0]) + Math.Abs(b[1] - a[1]) <= 1) { zug = null; continue; }
            if (zug is null) { zug = [[a[0], a[1]]]; zuege.Add(zug); }
            zug.Add([b[0], b[1]]);
            double za = a.Length > 2 ? a[2] : 0, zb = b.Length > 2 ? b[2] : 0;
            zmin = Math.Min(zmin, Math.Min(za, zb)); zmax = Math.Max(zmax, Math.Max(za, zb));
        }
        return zuege.Count == 0 ? null : (zuege, zmin, zmax);
    }

    static List<Decke> Decken(GrafikKontext ctx) => decken.GetValue(ctx, c =>
    {
        var l = new List<Decke>();
        bool grob = c.Detailgrad == "grob";
        foreach (var e in c.Modell.Elemente)
        {
            if (!c.Katalog.IstArt(e, "Leitung") || !Grafik.Sichtbar(c, e)) continue;
            var pts = HlksEinordnung.Punkte(e);
            if (pts.Count < 2 || Waagrecht(pts) is not { } w) continue;
            var mass = HlksMasse.Von(c.Projekt, e);
            double breite = grob || mass is null
                ? HlksEinordnung.Strichbreite(c.Detailgrad, HlksEinordnung.Dn(e), c.Massstab) + (grob ? 2 * LueckeGrob * c.Massstab : 0)
                : Math.Max(Math.Max(mass.Breite, 0.18 * c.Massstab), mass.Daemmdicke > 0 ? mass.DaemmBreite : 0);
            var flaeche = new PathsD();
            foreach (var zug in w.Zuege)
                flaeche.AddRange(Clipper.InflatePaths(new PathsD { new PathD(zug.Select(q => new PointD(q[0], q[1]))) }, breite / 2, JoinType.Round, EndType.Butt, 2, 2));
            var u = Clipper.Union(flaeche, FillRule.NonZero);
            if (u.Count > 0) l.Add(new Decke(e.Id, u, w.ZMin, w.ZMax, Clipper.GetBounds(u)));
        }
        return l;
    });

    /// <summary>Verdeckt den Teil der Grafik einer waagrechten Leitung, der unter einer höheren Leitung liegt (siehe Dateikopf). randBreite: Breite des
    /// Bandes in mittel/fein für die gestrichelten Ränder (null = Ränder sind schon als Linien gezeichnet, z. B. Abwasser).</summary>
    public static void Anwenden(GrafikKontext ctx, Element e, Grafikteil teil, List<double[]> pts, double? randBreite, string? farbe)
    {
        if (ctx.Art != "grundriss" || Waagrecht(pts) is not { } w) return;
        var box = teil.Primitive.Count == 0 ? null : Grafik.Begrenzung([teil]);
        if (box is null) return;
        var eigene = new RectD(box[0], box[1], box[2], box[3]);
        var deck = new PathsD();
        foreach (var d in Decken(ctx))
            if (d.Id != e.Id && d.ZMin > w.ZMax + Eps && Ueberlappt(d.Box, eigene)) deck.AddRange(d.Flaeche);
        if (deck.Count == 0) return;
        deck = Clipper.Union(deck, FillRule.NonZero);
        bool grob = ctx.Detailgrad == "grob";

        var neu = new List<Primitiv>();
        string? umrissStift = null;
        foreach (var p in teil.Primitive)
        {
            if (p.Stil.NurBildschirm || p.Art is "text") { neu.Add(p); continue; }
            if (p.Art == "kreis")
            {
                // Kreise im Band (Knicke) unter der Deckfläche entfallen; Symbole (Steigleitung) bleiben
                // gerade-ungerade über alle Ringe: ein Punkt im Loch der Deckfläche (z. B. innerhalb eines Leitungsrings) ist nicht verdeckt
                if (p.Punkte.Count > 0 && deck.Count(r => Polygone.Innen(r, p.Punkte[0][0], p.Punkte[0][1])) % 2 == 1) continue;
                neu.Add(p); continue;
            }
            if (p.Art == "polygon")
            {
                var subj = new PathsD { Polygone.Pfad(p.Punkte) };
                foreach (var loch in p.Loecher ?? []) subj.Add(Polygone.Pfad(loch));
                var rest = Clipper.Difference(subj, deck, FillRule.EvenOdd);
                bool geaendert = Math.Abs(Clipper.Area(rest) - Clipper.Area(subj)) > 1e-3;
                if (!geaendert) { neu.Add(p); continue; }
                if (p.Stil.Stift is not null) umrissStift ??= p.Stil.Stift;   // Umriss übernehmen die Ränder
                foreach (var f in Polygone.Flaechen(rest))
                {
                    var s = p.Stil.Kopie();
                    if (s.Stift is not null) s.Stift = null;
                    neu.Add(new Primitiv { Art = "polygon", Punkte = f.Aussen, Loecher = f.Loecher.Count > 0 ? f.Loecher : null, Stil = s });
                }
                continue;
            }
            if (p.Art is "polylinie" or "linie" && p.Punkte.Count >= 2)
            {
                var pfad = new PathsD { new PathD(p.Punkte.Select(q => new PointD(q[0], q[1]))) };
                foreach (var r in Offen(pfad, deck, ClipType.Difference).Where(r => r.Count >= 2))
                    neu.Add(new Primitiv { Art = "polylinie", Punkte = r.Select(q => new[] { q.x, q.y }).ToList(), Stil = p.Stil.Kopie() });
                if (!grob)
                    foreach (var r in Offen(pfad, deck, ClipType.Intersection).Where(r => r.Count >= 2))
                    {
                        var s = p.Stil.Kopie(); s.Linie = "strich";
                        neu.Add(new Primitiv { Art = "polylinie", Punkte = r.Select(q => new[] { q.x, q.y }).ToList(), Stil = s });
                    }
                continue;
            }
            neu.Add(p);
        }

        // mittel, fein: Ränder des Bandes – sichtbar dort, wo vorher der Umriss des Polygons war; verdeckt gestrichelt dünn
        if (!grob && randBreite is double rb)
            foreach (var zug in w.Zuege)
            {
                var raender = new PathsD(HlksGrafik.Raender(zug, rb, farbe, "voll").Select(r => new PathD(r.Punkte.Select(q => new PointD(q[0], q[1])))));
                if (umrissStift is not null)
                    foreach (var r in Offen(raender, deck, ClipType.Difference).Where(r => r.Count >= 2))
                        neu.Add(new Primitiv { Art = "polylinie", Punkte = r.Select(q => new[] { q.x, q.y }).ToList(), Stil = new Stil { Stift = umrissStift, Linie = "voll", Farbe = farbe } });
                foreach (var r in Offen(raender, deck, ClipType.Intersection).Where(r => r.Count >= 2))
                    neu.Add(new Primitiv { Art = "polylinie", Punkte = r.Select(q => new[] { q.x, q.y }).ToList(), Stil = new Stil { Stift = Stifte.Hilf, Linie = "strich", Farbe = farbe } });
            }
        teil.Primitive = neu;
    }

    static bool Ueberlappt(RectD a, RectD b) => a.left <= b.right && b.left <= a.right && a.top <= b.bottom && b.top <= a.bottom;

    static PathsD Offen(PathsD linien, PathsD flaechen, ClipType art)
    {
        var c = new ClipperD(3);
        c.AddOpenSubject(linien);
        c.AddClip(flaechen);
        var zu = new PathsD(); var auf = new PathsD();
        c.Execute(art, FillRule.NonZero, zu, auf);
        return auf;
    }
}
