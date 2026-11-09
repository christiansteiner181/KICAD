// Dämmung im Grundriss als eine vereinigte Fläche (Christian 09.11.2026: «auch bei den isolationen passt die darstellung noch nicht. überlappungen sind
// frablich anders als der rest. zudem hat es beim innenradius noch falsche schwarze darstellungen.»): die Dämmbänder aller sichtbaren Leitungen und der
// Formteile daneben (entlang ihrer Achse) werden vereinigt (Clipper) – eine Fläche je Zusammenhang, gelb transparent, nur der Aussenrand schwarz und dünn.
// Damit keine doppelt transparenten Überlappungen und keine Schleifen der Randlinien am Innenradius. Detailgrad fein und mittel; grob: Punkt (HlksGrafik).
// Senkrechte Leitungen (Ring) zeichnet weiterhin HlksGrafik.

using Clipper2Lib;

namespace Hlks.Kern;

public static class DaemmGrafik
{
    public static IEnumerable<Grafikteil> Teile(GrafikKontext ctx)
    {
        if (ctx.Art != "grundriss" || ctx.Detailgrad == "grob") yield break;
        var m = ctx.Modell; var kat = ctx.Katalog;
        var pfade = new PathsD();
        var masse = new Dictionary<string, LeitungsMass?>(StringComparer.Ordinal);
        LeitungsMass? Mass(Element e) => masse.TryGetValue(e.Id, out var x) ? x : masse[e.Id] = HlksMasse.Von(ctx.Projekt, e);
        void Band(List<double[]> xy, double breite)
        {
            if (xy.Count < 2) return;
            var linie = new PathD(xy.Select(q => new PointD(q[0], q[1])));
            pfade.AddRange(Clipper.InflatePaths(new PathsD { linie }, breite / 2, JoinType.Round, EndType.Butt, 2, 2));
        }
        foreach (var e in m.Elemente)
        {
            if (!Grafik.Sichtbar(ctx, e) || e.Geometrie?.Punkte is not { Count: >= 1 } pts) continue;
            if (kat.IstArt(e, "Leitung"))
            {
                if (Mass(e) is not { Daemmdicke: > 0 } ms) continue;
                // waagrechte bzw. flache Stücke als Züge im Grundriss (senkrechte Stücke: Ring in HlksGrafik)
                var zug = new List<double[]>();
                for (int i = 1; i < pts.Count; i++)
                {
                    bool flach = Math.Abs(pts[i][0] - pts[i - 1][0]) + Math.Abs(pts[i][1] - pts[i - 1][1]) > 1;
                    if (flach) { if (zug.Count == 0) zug.Add([pts[i - 1][0], pts[i - 1][1]]); zug.Add([pts[i][0], pts[i][1]]); }
                    else { Band(zug, ms.DaemmBreite); zug = []; }
                }
                Band(zug, ms.DaemmBreite);
            }
            else if (kat.IstArt(e, "Formteil") && Familien.Einbaulage(e) is { } lage
                     && Familien.Aufloesung(m, kat, e, out _) is { } a)
            {
                // Dämmung der angeschlossenen Leitung (die dickste), entlang der Achsen des Formteils
                var nachbar = m.VerbindungenVon(e.Id).Select(v => m.Finde(v.Von.Element == e.Id ? v.Nach.Element : v.Von.Element)).OfType<Element>()
                    .Where(n => kat.IstArt(n, "Leitung")).Select(Mass).OfType<LeitungsMass>().Where(x => x.Daemmdicke > 0)
                    .OrderByDescending(x => x.DaemmBreite).FirstOrDefault();
                if (nachbar is null) continue;
                foreach (var achse in a.Achsen)
                    Band(achse.Select(p => FamilienGrafik.W2(p, lage)).ToList(), nachbar.DaemmBreite);
            }
        }
        if (pfade.Count == 0) yield break;
        var flaechen = Polygone.Flaechen(Clipper.Union(pfade, FillRule.NonZero));
        var teil = new Grafikteil { Id = "daemmung", Ebene = "leitung" };
        foreach (var f in flaechen)
        {
            teil.Primitive.Add(new Primitiv
            {
                Art = "polygon", Punkte = f.Aussen,
                Stil = new Stil { Stift = Stifte.Duenn, Linie = "voll", Farbe = HlksGrafik.DaemmLinie, Fuellung = HlksGrafik.DaemmGelb, Deckkraft = HlksGrafik.DaemmDeckkraft },
            });
            foreach (var l in f.Loecher)
                teil.Primitive.Add(new Primitiv { Art = "polygon", Punkte = l, Stil = new Stil { Stift = Stifte.Duenn, Linie = "voll", Farbe = HlksGrafik.DaemmLinie } });
        }
        yield return teil;
    }
}
