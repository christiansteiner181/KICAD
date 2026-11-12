// Dämmung im Grundriss als eine vereinigte Fläche (Christian 09.11.2026: «auch bei den isolationen passt die darstellung noch nicht. überlappungen sind
// frablich anders als der rest. zudem hat es beim innenradius noch falsche schwarze darstellungen.»): die Dämmbänder aller sichtbaren Leitungen, der
// Formteile daneben (entlang ihrer Achse) und die Ringe der Steig- und Fallleitungen werden vereinigt (Clipper) – eine transparente Fläche ohne Rand je
// Rohrfarbe (Christian 12.11.2026: «wird die Dämmung gezeigt, so soll sie die Rohrfarbe mit transparenz erhalten»; bis dahin gelb).
// Die Ränder kommen je Teil dazu, nach Höhe geschichtet (Christian 09.11.2026: «bei dämmungen passt die darstellung bei bogen nach oben/bogen nach unten.
// hier die unsichtbaren dämmkanten gestrichelt darstellen»): ein Rand unter einem höher liegenden Teil ist verdeckt und gestrichelt, ein Rand im Inneren
// eines gleich hohen Teils entfällt (keine Schleifen am Innenradius), sonst ist er ausgezogen – in der Rohrfarbe, Stift fein 0.10 mm (Christian 12.11.2026:
// «die aussenkontur in Rohrfarbe jedoch wesentlich feiner»; bis dahin schwarz 0.2 mm).
// Höher liegt: bei waagrechten Teilen das Teil, dessen Achse an der Kreuzung höher liegt (Kreuzungshoehe, Gesamtprüfung 13.11.2026: je Teilstück dort
// verglichen, wo die Dämmbänder im Grundriss überlappen; gleiche oder sich dort schneidende Höhen = gleiche Lage – Leitungen mit Gefälle am Anschluss
// und ihre Formteile, die mit ihrer ganzen Höhenspanne zählen); mit einer Steig- bzw. Fallleitung das Teil, dessen mittlere Achshöhe über ihrer Mitte liegt.
// Detailgrad fein und mittel; grob: Punkt (HlksGrafik). Ganz unter dem Geschossboden (verdeckt) zeichnet HlksGrafik gestrichelt.

using Clipper2Lib;

namespace Hlks.Kern;

public static class DaemmGrafik
{
    const double Eps = 1.0;          // mm Achshöhe
    const double Naht = 0.05;        // mm: Ränder genau auf der Naht zweier gleich hoher Teile gelten als innen
    /// <summary>Linienart verdeckter Dämmkanten: fein gestrichelt (die Stücke sind kurz, das Muster «strich» wäre länger; 09.11.2026).</summary>
    public const string Verdeckt = "strichfein";

    sealed class Teil
    {
        public PathsD Flaeche = [];
        /// <summary>Rohrfarbe (Medienfarbe) der Leitung bzw. der angeschlossenen Leitung; null = Planfarbe.</summary>
        public string? Farbe;
        /// <summary>Element (Leitung bzw. Formteil), zu dem die Dämmung gehört – ein Klick wählt nur dieses (Christian 12.11.2026).</summary>
        public string ElementId = "";
        public double ZMin = double.MaxValue, ZMax = double.MinValue;
        public bool Senkrecht;
        /// <summary>Achse in Teilstücken und Breite der Dämmung (waagrechte Teile): Höhenvergleich an der Kreuzung.</summary>
        public List<Achsstueck> Stuecke = [];
        public double Breite;
        public RectD Box;
        public double Mitte => (ZMin + ZMax) / 2;
        public void Z(double z) { ZMin = Math.Min(ZMin, z); ZMax = Math.Max(ZMax, z); }
    }

    /// <summary>Liegt q über p (verdeckt q die Ränder von p)? Nur mit einer Steig- bzw. Fallleitung (Ring); waagrechte Teile: Kreuzungshoehe.</summary>
    static bool Ueber(Teil q, Teil p) => q.Mitte > p.Mitte + Eps;

    public static IEnumerable<Grafikteil> Teile(GrafikKontext ctx)
    {
        if (ctx.Art != "grundriss" || ctx.Detailgrad == "grob") yield break;
        var m = ctx.Modell; var kat = ctx.Katalog;
        double boden = ctx.Geschoss?.Zahl("kote") ?? double.NegativeInfinity;
        var teile = new List<Teil>();
        var masse =new Dictionary<string, LeitungsMass?>(StringComparer.Ordinal);
        LeitungsMass? Mass(Element e) => masse.TryGetValue(e.Id, out var x) ? x : masse[e.Id] = HlksMasse.Von(ctx.Projekt, e);
        string? Farbe(Element e) => HlksEinordnung.Farbe(HlksEinordnung.Medium(ctx.Projekt, e));
        static void Band(Teil t, List<double[]> xy, double breite)
        {
            if (xy.Count < 2) return;
            var linie = new PathD(xy.Select(q => new PointD(q[0], q[1])));
            t.Flaeche.AddRange(Clipper.InflatePaths(new PathsD { linie }, breite / 2, JoinType.Round, EndType.Butt, 2, 2));
        }
        void Fertig(Teil t)
        {
            if (t.Flaeche.Count == 0 || t.ZMax < boden - Eps) return;   // ganz unter dem Boden: verdeckt, HlksGrafik
            t.Flaeche = Clipper.Union(t.Flaeche, FillRule.NonZero);
            t.Box = Clipper.GetBounds(t.Flaeche);
            teile.Add(t);
        }
        foreach (var e in m.Elemente)
        {
            if (!Grafik.Sichtbar(ctx, e) || e.Geometrie?.Punkte is not { Count: >= 1 } pts) continue;
            if (kat.IstArt(e, "Leitung"))
            {
                if (Mass(e) is not { Daemmdicke: > 0 } ms) continue;
                if (HlksLinien.Laenge(pts.Select(q => new[] { q[0], q[1] }).ToList()) < 1)
                {
                    // Steig- bzw. Fallleitung: Ring bzw. Rechteck der Dämmung
                    var t = new Teil { Senkrecht = true, Farbe = Farbe(e), ElementId = e.Id };
                    foreach (var q in pts) t.Z(q.Length > 2 ? q[2] : 0);
                    var c = new PointD(pts[0][0], pts[0][1]);
                    if (ms.Rechteck)
                    {
                        double b = ms.DaemmBreite / 2, h = ms.DaemmHoehe / 2;
                        t.Flaeche.Add([new(c.x - b, c.y - h), new(c.x + b, c.y - h), new(c.x + b, c.y + h), new(c.x - b, c.y + h)]);
                    }
                    else t.Flaeche.Add(Clipper.Ellipse(c, ms.DaemmBreite / 2, ms.DaemmBreite / 2, 72));
                    Fertig(t);
                    continue;
                }
                // waagrechte bzw. flache Stücke als Züge im Grundriss (senkrechte Stücke einer Polylinie: Ring in HlksGrafik)
                var zug = new List<double[]>();
                var tz = new Teil { Farbe = Farbe(e), ElementId = e.Id, Breite = ms.DaemmBreite };
                void ZugFertig() { Band(tz, zug, ms.DaemmBreite); zug = []; }
                for (int i = 1; i < pts.Count; i++)
                {
                    bool flach = Math.Abs(pts[i][0] - pts[i - 1][0]) + Math.Abs(pts[i][1] - pts[i - 1][1]) > 1;
                    if (flach)
                    {
                        if (zug.Count == 0) zug.Add([pts[i - 1][0], pts[i - 1][1]]);
                        zug.Add([pts[i][0], pts[i][1]]);
                        double za = pts[i - 1].Length > 2 ? pts[i - 1][2] : 0, zb = pts[i].Length > 2 ? pts[i][2] : 0;
                        tz.Z(za); tz.Z(zb);
                        tz.Stuecke.Add(new(pts[i - 1][0], pts[i - 1][1], za, pts[i][0], pts[i][1], zb));
                    }
                    else ZugFertig();
                }
                ZugFertig();
                Fertig(tz);
            }
            else if (kat.IstArt(e, "Formteil") && Familien.Einbaulage(e) is { } lage
                     && Familien.Aufloesung(m, kat, e, out _) is { } a)
            {
                // Dämmung der angeschlossenen Leitung (die dickste), entlang der Achsen des Formteils
                var nachbar = m.VerbindungenVon(e.Id).Select(v => m.Finde(v.Von.Element == e.Id ? v.Nach.Element : v.Von.Element)).OfType<Element>()
                    .Where(n => kat.IstArt(n, "Leitung")).Select(n => (El: n, Ms: Mass(n))).Where(x => x.Ms is { Daemmdicke: > 0 })
                    .OrderByDescending(x => x.Ms!.DaemmBreite).FirstOrDefault();
                if (nachbar.Ms is null) continue;
                var t = new Teil { Farbe = Farbe(nachbar.El), ElementId = e.Id, Breite = nachbar.Ms.DaemmBreite };
                var achsen = new List<List<double[]>>();
                foreach (var achse in a.Achsen)
                {
                    foreach (var p in achse) t.Z(lage.Welt(p)[2]);
                    var xy = achse.Select(p => FamilienGrafik.W2(p, lage)).ToList();
                    achsen.Add(xy);
                    Band(t, xy, nachbar.Ms.DaemmBreite);
                }
                // das Formteil zählt mit seiner ganzen Höhenspanne (gleiche Lage wie die angeschlossene Leitung, auch als Bogen nach oben)
                foreach (var xy in achsen)
                    for (int i = 1; i < xy.Count; i++)
                        t.Stuecke.Add(new(xy[i - 1][0], xy[i - 1][1], t.ZMin, xy[i][0], xy[i][1], t.ZMax, Spanne: true));
                Fertig(t);
            }
        }
        if (teile.Count == 0) yield break;

        // Die vereinigte Fläche ist nicht anklickbar (Id null); Ränder und eine unsichtbare Trefferfläche je Element tragen dessen Id, damit ein Klick
        // auf die Dämmung nur diese Leitung bzw. dieses Formteil wählt und nur deren Dämmung markiert wird (Christian 12.11.2026: «wird eine dämmung
        // angeklickt werden alle dämmungen markiert. dies muss einzel möglich sein.»)
        var teil = new Grafikteil { Id = null, Teil = Kennung, Ebene = "leitung" };
        // Fläche: je Rohrfarbe alles vereinigt, ohne Rand, transparent in der Rohrfarbe (Christian 12.11.2026)
        foreach (var gruppe in teile.GroupBy(t => t.Farbe))
            foreach (var f in Polygone.Flaechen(Clipper.Union(new PathsD(gruppe.SelectMany(t => t.Flaeche)), FillRule.NonZero)))
                teil.Primitive.Add(new Primitiv
                {
                    Art = "polygon", Punkte = f.Aussen, Loecher = f.Loecher.Count > 0 ? f.Loecher : null,
                    Stil = new Stil { Stift = null, Fuellung = HlksGrafik.DaemmFuellung(gruppe.Key), Deckkraft = HlksGrafik.DaemmDeckkraft },
                });
        yield return teil;
        // Ränder je Teil: innen (gleich hoch) weg, unter einem höheren Teil gestrichelt, sonst ausgezogen – je Element ein eigenes Grafikteil
        var jeElement = new Dictionary<string, Grafikteil>(StringComparer.Ordinal);
        foreach (var p in teile)
        {
            if (!jeElement.TryGetValue(p.ElementId, out var et)) jeElement[p.ElementId] = et = new Grafikteil { Id = p.ElementId, Teil = Kennung, Ebene = "leitung" };
            // unsichtbare Trefferfläche (nur Bildschirm): die ganze Dämmfläche dieses Elements ist anklickbar
            foreach (var f in Polygone.Flaechen(p.Flaeche))
                et.Primitive.Add(new Primitiv
                {
                    Art = "polygon", Punkte = f.Aussen, Loecher = f.Loecher.Count > 0 ? f.Loecher : null,
                    Stil = new Stil { Stift = null, Fuellung = HlksGrafik.DaemmFuellung(p.Farbe), Deckkraft = 0, NurBildschirm = true },
                });
            var gleich = new PathsD(); var hoeher = new PathsD();
            foreach (var q in teile)
            {
                if (ReferenceEquals(p, q) || !Ueberlappt(p.Box, q.Box)) continue;
                if (p.Senkrecht || q.Senkrecht)
                {
                    if (Ueber(q, p)) hoeher.AddRange(q.Flaeche);
                    else if (!Ueber(p, q)) gleich.AddRange(q.Flaeche);
                    continue;
                }
                // waagrechte Teile: Höhe je Teilstück an der Kreuzung; ohne Paar überlappen sich die Dämmbänder nicht
                var paare = Kreuzungshoehe.Paare(p.Stuecke, q.Stuecke, (p.Breite + q.Breite) / 2 + Eps);
                if (paare.Count == 0) continue;
                bool ueber = paare.Any(x => x.Lage > 0), eben = paare.Any(x => x.Lage == 0), unter = paare.Any(x => x.Lage < 0);
                if (ueber && !eben && !unter) hoeher.AddRange(q.Flaeche);
                else if (eben && !ueber && !unter) gleich.AddRange(q.Flaeche);
                else if (ueber || eben)
                    // q liegt nur an einem Teil der Kreuzungen höher bzw. gleich (Gefälle, Etage): je Stelle einzeln
                    foreach (var (i, j, lage) in paare)
                        if (lage >= 0)
                            (lage > 0 ? hoeher : gleich).AddRange(Kreuzungshoehe.Ausschnitt(q.Flaeche, q.Stuecke[j], q.Breite / 2 + Eps, p.Stuecke[i], p.Breite / 2 + Eps));
            }
            var rand = new PathsD(p.Flaeche.Select(r => { var o = new PathD(r); if (r.Count > 0) o.Add(r[0]); return o; }));
            if (gleich.Count > 0) rand = Offen(rand, Clipper.InflatePaths(gleich, Naht, JoinType.Miter, EndType.Polygon), ClipType.Difference);
            var verdeckt = hoeher.Count > 0 ? Offen(rand, hoeher, ClipType.Intersection) : [];
            var sichtbar = hoeher.Count > 0 ? Offen(rand, hoeher, ClipType.Difference) : rand;
            foreach (var (pfade, linie) in new[] { (sichtbar, "voll"), (verdeckt, Verdeckt) })
                foreach (var r in pfade.Where(r => r.Count >= 2))
                    et.Primitive.Add(new Primitiv
                    {
                        Art = "polylinie", Punkte = r.Select(q => new[] { q.x, q.y }).ToList(),
                        Stil = new Stil { Stift = Stifte.Fein, Linie = linie, Farbe = HlksGrafik.DaemmRand(p.Farbe) },
                    });
        }
        foreach (var et in jeElement.Values) yield return et;
    }

    /// <summary>Kennung der Dämm-Grafikteile (Grafikteil.Teil): die gemeinsame Fläche (Id null) und die Ränder je Element (Id des Elements).</summary>
    public const string Kennung = "daemmung";

    static bool Ueberlappt(RectD a, RectD b) => a.left <= b.right && b.left <= a.right && a.top <= b.bottom && b.top <= a.bottom;

    /// <summary>Offene Linien an Flächen schneiden (Clipper: offene Subjekte).</summary>
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
