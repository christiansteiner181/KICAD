// Kreuzende Leitungen im Grundriss (Christian 12.11.2026: «leitungen die in einem Grundriss unter einer anderen Leitung durchlaufen (inkl. Dämmung)
// müssen verdeckt dargestellt werden», «bei grob wird die verdeckte leitung unterbrochen»): Läuft eine waagrechte Leitung unter einer höheren durch,
// ist sie dort verdeckt. Deckfläche der höheren Leitung = ihr Band bzw. ihre Dämmung (die breitere), in grob ihre Strichbreite plus eine Lücke.
//   mittel, fein: Füllung der tieferen Leitung unter der Deckfläche ausgespart, Ränder und Achse dort gestrichelt (Ränder dünn, Stift hilf).
//   grob: die tiefere Linie ist an der Deckfläche unterbrochen.
// Höher = an der Kreuzung höher (Kreuzungshoehe, Gesamtprüfung 13.11.2026): je waagrechtes Teilstück wird die Achshöhe dort verglichen, wo die
// Bänder im Grundriss überlappen (+1 mm); gleich hohe bzw. sich dort schneidende Höhen verdecken einander nicht. Eine Abwasserleitung mit Gefälle
// verdeckt also am hohen Ende und ist am tiefen Ende verdeckt. Steig- bzw. Fallleitungen und Formteile verdecken ebenfalls, mit Dämmung (Entscheid
// Christian 13.11.2026, Frage 13): eine senkrechte Leitung zählt mit ihrem oberen Ende (Frage 5), ein Formteil mit seiner ganzen Höhenspanne
// (gleiche Lage wie die angeschlossene Leitung). Deckfläche des Formteils = Band entlang seiner Achsen mit der Breite der angeschlossenen Leitung.
// ANNAHME Hauptlauf (Gestaltung): Lücke in grob je Seite 0.5 mm Papier, gemessen ab dem Rand des gezeichneten Strichs (Stift der Linie).
// Polygone mit Kontur (Kanalband, Kanalrahmen, Fliesspfeil): die Füllung wird ausgespart, der Umriss bleibt ausserhalb der Deckfläche mit dem
// eigenen Stift stehen (Gesamtprüfung 13.11.2026, Restliste) – je Polygon, nicht mehr als Ersatzränder über den ganzen Zug.

using System.Runtime.CompilerServices;
using Clipper2Lib;

namespace Hlks.Kern;

public static class LeitungVerdeckung
{
    const double LueckeGrob = 0.5;       // mm Papier je Seite

    sealed record Decke(string Id, PathsD Flaeche, double Breite, List<Achsstueck> Stuecke, RectD Box);
    sealed record Plan(List<Decke> Alle, Dictionary<string, Decke> NachId);

    static readonly ConditionalWeakTable<GrafikKontext, Plan> decken = new();

    /// <summary>Waagrechte Stücke einer Leitung als Züge im Grundriss und als Teilstücke mit Achshöhe; null ohne waagrechte Stücke.</summary>
    static (List<List<double[]>> Zuege, List<Achsstueck> Stuecke)? Waagrecht(List<double[]> pts)
    {
        var zuege = new List<List<double[]>>(); List<double[]>? zug = null;
        var stuecke = new List<Achsstueck>();
        for (int i = 1; i < pts.Count; i++)
        {
            var a = pts[i - 1]; var b = pts[i];
            if (Math.Abs(b[0] - a[0]) + Math.Abs(b[1] - a[1]) <= 1) { zug = null; continue; }
            if (zug is null) { zug = [[a[0], a[1]]]; zuege.Add(zug); }
            zug.Add([b[0], b[1]]);
            stuecke.Add(new(a[0], a[1], a.Length > 2 ? a[2] : 0, b[0], b[1], b.Length > 2 ? b[2] : 0));
        }
        return zuege.Count == 0 ? null : (zuege, stuecke);
    }

    /// <summary>Breite der Deckfläche einer Leitung: Band bzw. Dämmung (die breitere), in grob die Strichbreite plus Lücke.</summary>
    static double Breite(GrafikKontext c, Element e, LeitungsMass? mass)
    {
        // grob: gezeichnet wird die Linie mit dem Stift der Strichbreite (0.25 mm → «mittel» 0.35 mm), die Lücke zählt ab dessen Rand
        double strich = HlksEinordnung.Strichbreite(c.Detailgrad, HlksEinordnung.Dn(e), c.Massstab);
        return c.Detailgrad == "grob"
            ? Stifte.Mm(HlksEinordnung.Stift(strich / c.Massstab)) * c.Massstab + 2 * LueckeGrob * c.Massstab
            : mass is null ? strich
            : Math.Max(Math.Max(mass.Breite, 0.18 * c.Massstab), mass.Daemmdicke > 0 ? mass.DaemmBreite : 0);
    }

    static Plan Decken(GrafikKontext ctx) => decken.GetValue(ctx, c =>
    {
        var l = new List<Decke>();
        bool grob = c.Detailgrad == "grob";
        var m = c.Modell; var kat = c.Katalog;
        void Neu(string id, PathsD flaeche, double breite, List<Achsstueck> stuecke)
        {
            var u = Clipper.Union(flaeche, FillRule.NonZero);
            if (u.Count > 0) l.Add(new Decke(id, u, breite, stuecke, Clipper.GetBounds(u)));
        }
        foreach (var e in m.Elemente)
        {
            if (!Grafik.Sichtbar(c, e)) continue;
            if (kat.IstArt(e, "Leitung"))
            {
                var pts = HlksEinordnung.Punkte(e);
                if (pts.Count < 2) continue;
                var mass = HlksMasse.Von(c.Projekt, e);
                double breite = Breite(c, e, mass);
                var flaeche = new PathsD(); var stuecke = new List<Achsstueck>();
                if (Waagrecht(pts) is { } w)
                {
                    foreach (var zug in w.Zuege)
                        flaeche.AddRange(Clipper.InflatePaths(new PathsD { new PathD(zug.Select(q => new PointD(q[0], q[1]))) }, breite / 2, JoinType.Round, EndType.Butt, 2, 2));
                    stuecke.AddRange(w.Stuecke);
                }
                // senkrechte Stücke (Steig- bzw. Fallleitung): Kreis mit der Breite, Höhe = oberes Ende (Fragen 5 und 13); in grob der Kreis des
                // Symbols (effektiver Durchmesser) plus Lücke
                for (int i = 1; i < pts.Count; i++)
                {
                    var a = pts[i - 1]; var b = pts[i];
                    if (Math.Abs(b[0] - a[0]) + Math.Abs(b[1] - a[1]) > 1) continue;
                    double za = a.Length > 2 ? a[2] : 0, zb = b.Length > 2 ? b[2] : 0;
                    if (Math.Abs(zb - za) <= 1) continue;
                    double r = grob ? Math.Max(breite / 2, HlksGrafik.EinlinigRadius(mass, HlksEinordnung.Dn(e), c.Massstab) + LueckeGrob * c.Massstab) : breite / 2;
                    flaeche.Add(Clipper.Ellipse(new PointD(a[0], a[1]), r, r, 48));
                    double oben = Math.Max(za, zb);
                    stuecke.Add(new(a[0], a[1], oben, a[0], a[1], oben));
                }
                if (stuecke.Count > 0) Neu(e.Id, flaeche, breite, stuecke);
            }
            else if (kat.IstArt(e, "Formteil") && Familien.Einbaulage(e) is { } lage && Familien.Aufloesung(m, kat, e, out _) is { } fa)
            {
                // Breite der angeschlossenen Leitung (die breiteste); Höhe = ganze Spanne des Formteils
                var nachbar = m.VerbindungenVon(e.Id).Select(v => m.Finde(v.Von.Element == e.Id ? v.Nach.Element : v.Von.Element)).OfType<Element>()
                    .Where(n => kat.IstArt(n, "Leitung")).Select(n => (El: n, B: Breite(c, n, HlksMasse.Von(c.Projekt, n)))).OrderByDescending(x => x.B).FirstOrDefault();
                if (nachbar.El is null) continue;
                var flaeche = new PathsD(); var achsen = new List<List<double[]>>();
                double zMin = double.MaxValue, zMax = double.MinValue;
                foreach (var achse in fa.Achsen)
                {
                    foreach (var p in achse) { double z = lage.Welt(p)[2]; zMin = Math.Min(zMin, z); zMax = Math.Max(zMax, z); }
                    var xy = achse.Select(p => FamilienGrafik.W2(p, lage)).ToList();
                    if (xy.Count < 2 || HlksLinien.Laenge(xy) < 1) continue;
                    achsen.Add(xy);
                    flaeche.AddRange(Clipper.InflatePaths(new PathsD { new PathD(xy.Select(q => new PointD(q[0], q[1]))) }, nachbar.B / 2, JoinType.Round, EndType.Butt, 2, 2));
                }
                var stuecke = new List<Achsstueck>();
                foreach (var xy in achsen)
                    for (int i = 1; i < xy.Count; i++)
                        stuecke.Add(new(xy[i - 1][0], xy[i - 1][1], zMin, xy[i][0], xy[i][1], zMax, Spanne: true));
                if (stuecke.Count > 0) Neu(e.Id, flaeche, nachbar.B, stuecke);
            }
        }
        return new Plan(l, l.ToDictionary(d => d.Id, StringComparer.Ordinal));
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
        var plan = Decken(ctx);
        double eigeneBreite = plan.NachId.TryGetValue(e.Id, out var ich) ? ich.Breite : 0;
        // direkt oder über ein Formteil angeschlossene Elemente verdecken nicht (Bogen zur eigenen Steigleitung, T-Stück am Abzweig)
        var m = ctx.Modell;
        var nah = new HashSet<string>(StringComparer.Ordinal) { e.Id };
        foreach (var v in m.VerbindungenVon(e.Id))
        {
            var n = v.Von.Element == e.Id ? v.Nach.Element : v.Von.Element;
            if (!nah.Add(n) || m.Finde(n) is not { } ne || !ctx.Katalog.IstArt(ne, "Formteil")) continue;
            foreach (var v2 in m.VerbindungenVon(n)) nah.Add(v2.Von.Element == n ? v2.Nach.Element : v2.Von.Element);
        }
        foreach (var d in plan.Alle)
        {
            if (nah.Contains(d.Id) || !Ueberlappt(d.Box, eigene)) continue;
            // Höhe je Teilstück an der Kreuzung (Kreuzungshoehe); überlappen sich die Bänder nirgends, gibt es kein Paar
            var paare = Kreuzungshoehe.Paare(w.Stuecke, d.Stuecke, (eigeneBreite + d.Breite) / 2);
            if (!paare.Any(x => x.Lage > 0)) continue;
            if (paare.All(x => x.Lage > 0)) { deck.AddRange(d.Flaeche); continue; }
            // die andere Leitung liegt nur an einem Teil der Kreuzungen höher (Gefälle, Etage): nur diese Stellen verdecken
            foreach (var (i, j, lage) in paare)
                if (lage > 0) deck.AddRange(Kreuzungshoehe.Ausschnitt(d.Flaeche, d.Stuecke[j], d.Breite / 2 + 1, w.Stuecke[i], eigeneBreite / 2 + 1));
        }
        if (deck.Count == 0) return;
        deck = Clipper.Union(deck, FillRule.NonZero);
        bool grob = ctx.Detailgrad == "grob";

        var neu = new List<Primitiv>();
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
                foreach (var f in Polygone.Flaechen(rest))
                {
                    var s = p.Stil.Kopie();
                    if (s.Stift is not null) s.Stift = null;
                    neu.Add(new Primitiv { Art = "polygon", Punkte = f.Aussen, Loecher = f.Loecher.Count > 0 ? f.Loecher : null, Stil = s });
                }
                if (p.Stil.Stift is not null)
                {
                    // Umriss dieses Polygons (geschlossene Ringe) ausserhalb der Deckfläche, mit seinem Stift und ohne Füllung
                    var ringe = new PathsD(subj.Where(r => r.Count >= 2).Select(r => new PathD(r) { r[0] }));
                    foreach (var r in Offen(ringe, deck, ClipType.Difference).Where(r => r.Count >= 2))
                    {
                        var s = p.Stil.Kopie(); s.Fuellung = null; s.Muster = null;
                        neu.Add(new Primitiv { Art = "polylinie", Punkte = r.Select(q => new[] { q.x, q.y }).ToList(), Stil = s });
                    }
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

        // mittel, fein: Ränder des Bandes unter der Deckfläche gestrichelt dünn (der sichtbare Umriss bleibt am Polygon, siehe oben)
        if (!grob && randBreite is double rb)
            foreach (var zug in w.Zuege)
            {
                var raender = new PathsD(HlksGrafik.Raender(zug, rb, farbe, "voll").Select(r => new PathD(r.Punkte.Select(q => new PointD(q[0], q[1])))));
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
