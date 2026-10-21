// Schnitt und Ansicht aus den Körpern (Modul 1a, C1; entwurf/modul-1a.md «Schnitt und Ansicht»): was die Schnittebene trifft,
// wird als Schnittfläche gezeichnet (dicke Kontur, Füllung/Schraffur), was dahinter in Blickrichtung bis zur Sichttiefe
// liegt, als Sichtkante (dünn), Verdecktes nicht. Ansicht = gleiches Verfahren ohne Schnittflächen.
// Regeln mit Quelle: SIA 400:2000 Ziff. B.3.1 (drei Stifte), B.3.3 Tab. 4 (dick = Schnittkanten der Hauptbauteile, dünn =
// Sichtkanten), B.8.3.1 Fig. 34 (Schraffur nach Material, über ArchitekturGrafik.Schnittstil wie im Grundriss).
//
// Ablauf:
//   1. Schnittraum: Linie A → B, Blickrichtung nach `blick`, Sichttiefe `tiefe` (SchnittRaum.cs).
//   2. Körper aller gezeigten Modellelemente (Sichtbarkeit wie Grafik.Sichtbar, ohne Geschossfilter) zerlegen in
//      Schnittflächen und Sichtteile (SchnittZerlegung.cs).
//   3. Schnittflächen je Element vereinigen (Clipper2).
//   3b. Anschluss Wand–Decke (Modul 1c, Auftrag B6, Entscheid Hauptlauf «Decke läuft durch»): Die Schnittfläche jeder Wand
//      wird um die Schnittflächen der Decken und Dächer verkleinert (Wand minus Decke), die Decken bleiben ganz – keine
//      doppelte Füllung, die Kontur der Wand endet an der Deckenunterkante; für alle Materialien. Der Wandkörper endet schon
//      im 3D an der Unterkante einer Decke, die über seiner Achse liegt (ArchitekturGrafik.WandKoerper); 3b erfasst dazu die
//      Fälle, in denen eine Decke nur einen Teil der Wanddicke überdeckt. Bliebe von einer Wand nichts übrig, liegt sie ganz
//      in der Platte (Konflikt im Modell, z. B. geneigtes IFC-Dach als Kasten) und bleibt wie bisher – dort bleibt die
//      Doppelfüllung sichtbar (Annahme, wie im 3D: im Zweifel nichts wegnehmen).
//   4. Verdeckung: sichtbare Kantenstücke je Sichtteil (SchnittVerdeckung.cs).
//   5. Je Element ein Grafikteil (Id = Element, Ebene des Körpers): erst die Sichtkanten, darüber die Schnittflächen;
//      Fangpunkte = Ecken der Schnittflächen und Enden gerader Sichtkanten, Fangkanten = Kanten der Schnittflächen und
//      gerade Sichtkanten.
//   5b. N12: IFC-Unterlagen: Nachbau in der Farbe der Unterlage; Original (ifcDarstellung original/beide) als Schnittlinien der
//      Netze mit der Ebene w = 0 (GrundlagenIfc.Ansicht).
//   6. Geschosskoten und Achsen (SchnittBezug.cs).
// ANNAHMEN (Prüfliste Christian):
//   - Schnittflächen von Decken/Dächern: Füllung «schnitt», Deckkraft 0.25 (wie Wand «unbestimmt»; Decken haben kein Material).
//   - Schnittflächen von Leitungen, Formteilen, Armaturen, Apparaten: Füllung und Kontur in der Medienfarbe des Körpers,
//     ohne Medienfarbe Füllung «schnitt» (voll) und Kontur Planfarbe; Kontur dick.
//   - Schnittflächen von Türen und Fenstern (Nebenbauteile, B.3.3 nennt dick nur für Hauptbauteile): Kontur mittel, Füllung
//     «papier», Glas mit Schraffur «glas» (Fig. 34).
//   - Sichtkanten der HLKS-Körper in ihrer Medienfarbe, alles andere in Planfarbe.
//   - Räume (NichtSchneiden) erscheinen weder im Schnitt noch in der Ansicht. Ebenso Aussparungen als Körper; sie kommen als
//     Symbol über der Grafik dazu (Schritt 7, AussparungGrafik.Ansicht, Modul 3b B4c).
//   - Glas verdeckt wie jeder andere Körper (keine Durchsicht).
//   - Wand minus Decke gilt nur für Wände; Stützen und Unterzüge (Ebene wand) bleiben ganz.

using Clipper2Lib;

namespace Hlks.Kern;

public static class Schnittgrafik
{
    const double DeckeDeckkraft = 0.25;   // Annahme wie Wand «unbestimmt» (ArchitekturGrafik)

    /// <summary>Modellgrafik eines Schnitts bzw. einer Ansicht in u/v-Koordinaten (u entlang der Linie, v = z).</summary>
    public static IEnumerable<Grafikteil> Erzeugen(GrafikKontext ctx)
    {
        var raum = Schnittraum.Aus(ctx.Ansicht);
        bool schnitt = ctx.Art == "schnitt";

        // 2. Körper der gezeigten Elemente (Wandlagen und Decken einmal je Lauf statt je Wand)
        var liste = new List<(Element E, Koerper K)>();
        using (Wandgeometrie.Zwischenspeichern(ctx.Modell, ctx.Katalog))
            foreach (var e in ctx.Modell.Elemente)
            {
                if (!Grafik.IstModellgrafik(ctx.Katalog, e) || !Grafik.Sichtbar(ctx, e)) continue;
                // Familien im Detailgrad der Ansicht (Modul 2), alle anderen Quellen wie bisher
                if (Grafik.QuelleFuer(ctx.Katalog, e) is { } q && FamilienGrafik.KoerperFuer(q, ctx.Projekt, e, ctx.Detailgrad) is { Leer: false } k) liste.Add((e, k));
            }
        var teile = Zeichnen(raum, schnitt, liste, ctx.Katalog);
        IfcDarstellung.Ueberschreiben(ctx, teile);                       // N12: Nachbau in der Farbe der Unterlage
        teile.InsertRange(0, GrundlagenIfc.Ansicht(ctx, raum));          // N12: Original der IFC-Unterlagen (Schnitt mit der Ebene), unterste Ebene

        // 6. Geschosskoten und Achsen
        teile.AddRange(Schnittbezug.Erzeugen(ctx, raum, teile));

        // 7. Aussparungen (Modul 3b, B4c): Symbole als Überlagerung über Schnittflächen und Sichtkanten (AussparungGrafik.Ansicht)
        teile.AddRange(AussparungGrafik.Ansicht(ctx, raum));
        return teile;
    }

    /// <summary>Schritte 2–5 für eine gegebene Liste von Körpern (auch für Familienansichten ohne Modell, FamilienAnsicht).</summary>
    internal static List<Grafikteil> Zeichnen(Schnittraum raum, bool schnitt, List<(Element E, Koerper K)> liste, Katalog katalog)
    {
        var zerlegung = new Schnittzerlegung(raum, schnitt);
        for (int i = 0; i < liste.Count; i++) zerlegung.Koerper(i, liste[i].K);

        // 3. Schnittflächen je Element vereinigt, Wände ohne die Decken (3b)
        var vereinigt = new Dictionary<int, PathsD>();
        foreach (var (el, roh) in zerlegung.Schnitte)
        {
            var v = Polygone.Vereinigen(roh);
            if (v.Count > 0) vereinigt[el] = v;
        }
        AnschlussDecken(katalog, liste, vereinigt);
        var flaechen = new Dictionary<int, List<Flaeche>>();
        foreach (var (el, v) in vereinigt)
            if (v.Count > 0) flaechen[el] = Polygone.Flaechen(v);

        // 4. Verdeckung; sichtbare Kantenstücke je Element, gekrümmte wieder zu Linienzügen verkettet
        var sichtbar = Schnittverdeckung.Rechnen(zerlegung.Teile, zerlegung.Schnittstuecke);
        var kantenJeElement = new Dictionary<int, List<(List<P2> Punkte, string? Farbe)>>();
        for (int i = 0; i < zerlegung.Teile.Count; i++)
        {
            var t = zerlegung.Teile[i];
            if (sichtbar[i].Count == 0) continue;
            if (!kantenJeElement.TryGetValue(t.Element, out var l)) kantenJeElement[t.Element] = l = [];
            foreach (var kette in SchnittGeometrie.Verketten(sichtbar[i].Select(s => (s.A, s.B, s.Gerade)))) l.Add((kette, t.Farbe));
        }

        // 5. Grafikteile je Element
        var teile = new List<Grafikteil>();
        for (int i = 0; i < liste.Count; i++)
        {
            var (e, k) = liste[i];
            var farbe = k.Teile.Select(t => t.Farbe).FirstOrDefault(f => f is not null);
            // Familienkörper (Modul 2): stammen alle Teile aus demselben Grundkörper, trägt das Grafikteil dessen Id
            var koerperId = k.Teile.Select(t => t.Teil).Distinct().ToList() is [{ } einzig] ? einzig : null;
            var teil = new Grafikteil { Id = e.Id, Ebene = k.Ebene, Fangpunkte = [], Fangkanten = [], Teil = koerperId };
            var punkte = new HashSet<(long, long)>();
            void Fangpunkt(double[] p) { if (punkte.Add(((long)Math.Round(p[0] * 1000), (long)Math.Round(p[1] * 1000)))) teil.Fangpunkte!.Add(p); }

            foreach (var (kette, f) in kantenJeElement.GetValueOrDefault(i) ?? [])
            {
                var pts = kette.Select(q => new[] { Math.Round(q.U, Polygone.Praezision), Math.Round(q.V, Polygone.Praezision) }).ToList();
                teil.Primitive.Add(new Primitiv { Art = pts.Count == 2 ? "linie" : "polylinie", Punkte = pts, Stil = new Stil { Stift = Stifte.Duenn, Farbe = f } });
                if (pts.Count == 2)
                {
                    teil.Fangkanten!.Add([pts[0], pts[1]]);
                    Fangpunkt(pts[0]); Fangpunkt(pts[1]);
                }
            }
            foreach (var fl in flaechen.GetValueOrDefault(i) ?? [])
            {
                teil.Primitive.Add(new Primitiv
                {
                    Art = "polygon", Punkte = fl.Aussen, Loecher = fl.Loecher.Count > 0 ? fl.Loecher : null,
                    Stil = Schnittstil(k, e, farbe),
                });
                foreach (var ring in fl.Loecher.Prepend(fl.Aussen))
                    for (int j = 0; j < ring.Count; j++)
                    {
                        Fangpunkt(ring[j]);
                        teil.Fangkanten!.Add([ring[j], ring[(j + 1) % ring.Count]]);
                    }
            }
            if (teil.Primitive.Count == 0) continue;
            if (teil.Fangpunkte!.Count == 0) teil.Fangpunkte = null;
            if (teil.Fangkanten!.Count == 0) teil.Fangkanten = null;
            teile.Add(teil);
        }

        return teile;
    }

    /// <summary>3b. Anschluss Wand–Decke (Dateikopf): Von den Schnittflächen jeder Wand die Schnittflächen der Decken und Dächer
    /// abziehen, deren Begrenzung sie berührt; die Decken bleiben ganz.</summary>
    static void AnschlussDecken(Katalog k, List<(Element E, Koerper K)> liste, Dictionary<int, PathsD> vereinigt)
    {
        var decken = new List<(PathsD Pfade, RectD Box)>();
        foreach (var (i, v) in vereinigt)
            if (k.IstArt(liste[i].E, "Decke") || k.IstArt(liste[i].E, "Dach")) decken.Add((v, Clipper.GetBounds(v)));
        if (decken.Count == 0) return;
        foreach (var i in vereinigt.Keys.ToList())
        {
            if (!k.IstArt(liste[i].E, "Wand")) continue;
            var box = Clipper.GetBounds(vereinigt[i]);
            var clip = new PathsD();
            foreach (var (pfade, b) in decken)
                if (b.left <= box.right && box.left <= b.right && b.top <= box.bottom && box.top <= b.bottom) clip.AddRange(pfade);
            if (clip.Count == 0) continue;
            var rest = Polygone.Abziehen(vereinigt[i], clip);
            if (rest.Count > 0) vereinigt[i] = rest;   // bliebe nichts: Wand ganz in der Platte, bleibt (wie im 3D)
        }
    }

    /// <summary>Stil der Schnittfläche nach Zeichenebene und Material (siehe Annahmen im Dateikopf).</summary>
    static Stil Schnittstil(Koerper k, Element e, string? farbe)
    {
        switch (k.Ebene)
        {
            case "wand":
                var s = ArchitekturGrafik.Schnittstil(e.Text("material"));
                s.Stift = Stifte.Dick;
                return s;
            case "decke":
                return new Stil { Stift = Stifte.Dick, Fuellung = "schnitt", Deckkraft = DeckeDeckkraft };
            case "oeffnung":
                return new Stil { Stift = Stifte.Mittel, Fuellung = "papier", Muster = k.Teile.Any(t => t.Material == "glas") ? "glas" : null };
            default:
                return new Stil { Stift = Stifte.Dick, Farbe = farbe, Fuellung = farbe ?? "schnitt" };
        }
    }
}
