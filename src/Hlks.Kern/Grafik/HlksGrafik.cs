// Grafik der Gebäudetechnik: Leitungen, Formteile, Armaturen, Apparate, Geräte und alles übrige Grafische mit Lage.
// Übertrag aus der Oberfläche (Modul 1a, Teil B2, Entscheid «Planvektoren erzeugt der Kern»):
//
//   js/ansichten/zeichnen2d.js  leitung, formteil, armatur, apparat, sonst     →  Plan() mit den Hilfen Hlks*.cs
//   js/ansichten/sinnbilder.js  APPARATE, GERAETE, VORGABE_APPARAT              →  HlksSinnbilder, Katalog «darstellung»
//   js/ansichten/geometrie.js   punkteVon, dnVon, strichbreite, mitte, …        →  HlksEinordnung, HlksLinien
//   js/ansichten/bauteile3d.js  leitung, formteil, armatur, koerper, sonst      →  Koerper() / HlksKoerper
//
// Aussehen wie heute; wo die Plangrafik etwas nicht kennt, gilt (jeweils im Code als «Annahme» vermerkt):
//   Strichbreiten in Papier-mm werden auf die drei Stifte gerundet (aufwärts); Leitungen, die breiter als 0.35 mm
//   Papier sind (DN-gerecht, 0.6 × DN), werden als Band (Polygone) gezeichnet, siehe HlksLinien.
//   Hintergrundweisse Füllungen (CSS gr-fuell) sind die Füllung «papier», Medienfarben bleiben #rrggbb, alles ohne
//   Medienfarbe ist Planfarbe (Stil.Farbe = null bzw. Füllung «schnitt»).
//   Kennzeichen-Etiketten sind nur am Bildschirm (Stil.NurBildschirm), DN-Angaben (Detailgrad fein) stehen auf dem Plan.
//   Schrift: 2.0 mm Papier (SCHRIFT_KLEIN in zeichnen2d.js).
// Der Grundriss kennt zusätzlich Fangpunkte: Leitungsenden und Knicke, Mitte (und Ecken) von Apparaten, Armaturen, Formteilen.
//
// Modul 3 (B4): Leitungen mit Serie massstäblich (HlksMasse), Dämmung als zweite Kontur, Schnitthöhe (Füllung der Steigleitungen,
// Kreis bzw. Rechteck mit «schnitt»); ausgezogen, gestrichelt nur Verdecktes unter dem Boden (Christian 19.10.2026, alle Leitungen).
//
// Zuständigkeit: HlksGrafik steht zuletzt in Grafik.Quellen und nimmt alles Grafische, das die anderen Quellen nicht nehmen.

using System.Globalization;

namespace Hlks.Kern;

public sealed class HlksGrafik : IGrafikquelle
{
    public string Name => "hlks";

    const double Schrift = 2.0;   // mm Papier (SCHRIFT_KLEIN in zeichnen2d.js: Kennzeichen, DN)

    /// <summary>Alles Grafische, das nicht Architektur und nicht Beschriftung ist.</summary>
    public bool Zustaendig(Katalog k, Element e) => true;

    /// <summary>Flächen, die der Raumstempel meiden soll (Apparate samt Kennzeichen, Armaturen), als [minx, miny, maxx, maxy]
    /// in Modell-mm – wie «hindernis» in zeichnen2d.js. Steht nach <see cref="Vorbereiten"/> in <c>ctx.Ablage["hlks"]</c>.</summary>
    public static IReadOnlyList<double[]> Hindernisse(GrafikKontext ctx) =>
        ctx.Ablage.TryGetValue("hlks", out var o) && o is List<double[]> l ? l : [];

    public void Vorbereiten(GrafikKontext ctx)
    {
        var liste = new List<double[]>();
        ctx.Ablage["hlks"] = liste;
        if (ctx.Art != "grundriss") return;
        foreach (var e in ctx.Modell.Elemente)
        {
            if (!Grafik.Sichtbar(ctx, e) || Grafik.QuelleFuer(ctx.Katalog, e) is not HlksGrafik) continue;
            var pts = HlksEinordnung.Punkte(e);
            if (pts.Count == 0) continue;
            var (x, y) = (pts[0][0], pts[0][1]);
            var M = ctx.Massstab;
            switch (HlksEinordnung.Art(ctx.Katalog, e))
            {
                case HlksArt.Armatur:
                    {
                        var d = 1.2 * M * 1.6 + 80;
                        liste.Add([x - d, y - d, x + d, y + d]);
                        break;
                    }
                case HlksArt.Apparat or HlksArt.Geraet:
                    {
                        var (b, t, _) = HlksEinordnung.Mass(ctx.Katalog, e);
                        var fs = Schrift * M;
                        liste.Add([x - b / 2 - 80, y - t / 2 - 80, x + b / 2 + 0.6 * M + e.Kennzeichen.Length * fs * 0.6 + 80, y + t / 2 + 80]);
                        break;
                    }
            }
        }
    }

    public IEnumerable<Grafikteil> Plan(GrafikKontext ctx, Element e)
    {
        if (ctx.Art != "grundriss" || e.Geometrie is null) return [];
        var pts = HlksEinordnung.Punkte(e);
        if (pts.Count == 0) return [];
        var md = HlksEinordnung.Medium(ctx.Projekt, e);
        Grafikteil? teil = HlksEinordnung.Art(ctx.Katalog, e) switch
        {
            HlksArt.Leitung => Leitung(ctx, e, pts, md),
            HlksArt.Formteil => Formteil(ctx, e, pts, md),
            HlksArt.Armatur => Armatur(ctx, e, pts, md),
            HlksArt.Apparat or HlksArt.Geraet => Apparat(ctx, e, pts, md),
            _ => Sonst(ctx, e, pts, md),
        };
        return teil is null ? [] : [teil];
    }

    public Koerper? Koerper(Projekt p, Element e) => HlksKoerper.Erzeugen(p, e);

    /// <summary>Körper mit Detailgrad (Modul 3: Dämmung nur im Detailgrad fein; ohne Angabe fein wie FamilienGrafik).</summary>
    public Koerper? Koerper(Projekt p, Element e, string detail) => HlksKoerper.Erzeugen(p, e, detail);

    // ------------------------------------------------------------------ 2D

    /// <summary>Lage einer Leitung bzw. eines Teilstücks zur Schnittebene des Grundrisses.</summary>
    enum Hoehe { Unten, Geschnitten, Oben }

    sealed class Lauf
    {
        public List<double[]> Xy = [];
        public bool Verdeckt;
    }

    const double Eps = 1e-6;

    /// <summary>leitung() in zeichnen2d.js, ab Modul 3 (B4) massstäblich und mit Schnitthöhe:
    ///   – Rohr mit Serie: Band mit dem Aussendurchmesser der Grösse (mindestens eine dünne Linie breit), Kanal mit Serie bzw.
    ///     Querschnitt: Umriss b × h bzw. Ø d wie bisher; ohne Serie DN-gerecht wie bisher; Detailgrad grob: einfache Linie.
    ///   – Dämmung (daemmdicke > 0, Detailgrad mittel/fein): zweite Kontur im Abstand daemmdicke, dünn gestrichelt.
    ///   – Entscheid Christian 19.10.2026 (Abnahme Modul 3, Prüfliste 10): über der Schnitthöhe **ausgezogen**, gestrichelt nur
    ///     **Verdecktes**. Verdeckt = Teilstück ganz unterhalb der Kote des dargestellten Geschosses (in bzw. unter Boden, Decke
    ///     oder Bodenplatte, z. B. Grundleitungen) – Auslegung Hauptlauf, Prüfliste. Gilt für alle Leitungen (mit und ohne Serie);
    ///     verdeckte Bänder als gestrichelte Ränder ohne Füllung. Senkrechte Stücke als Kreis bzw. Rechteck: durch die
    ///     Schnittebene Füllung «schnitt», darunter «papier», darüber ohne Füllung, Kontur ausgezogen; ganz verdeckt gestrichelt.
    /// ANNAHMEN (Prüfliste): «ganz unterhalb» heisst beide Endpunkte tiefer als die Geschosskote; ohne Geschoss in der Ansicht ist
    ///   nichts verdeckt. Die Schnitthöhe bestimmt nur noch die Füllung senkrechter Stücke mit Serie. Bei Polylinien wird jedes
    ///   Teilstück nach seinen z beurteilt; senkrechte Teilstücke einer Polylinie erscheinen nur, wenn sie die Schnittebene
    ///   durchstossen. Stift der Steigleitung «mittel». Senkrechter Kanal b × h: b in x, h in y.</summary>
    static Grafikteil Leitung(GrafikKontext ctx, Element e, List<double[]> pts, MediumDef? md)
    {
        var M = ctx.Massstab;
        var dn = HlksEinordnung.Dn(e);
        var w = HlksEinordnung.Strichbreite(ctx.Detailgrad, dn, M);   // Modell-mm
        var farbe = HlksEinordnung.Farbe(md);
        var linie = HlksEinordnung.Linienart(md);
        var xy = HlksLinien.OhneDoppelte(pts);
        var teil = new Grafikteil { Id = e.Id, Ebene = "leitung" };
        bool grob = ctx.Detailgrad == "grob";
        var mass = HlksMasse.Von(ctx.Projekt, e);
        // Schnitthöhe nur für die Füllung senkrechter Stücke mit Serie; «verdeckt» (gestrichelt) für alle Leitungen unter dem Boden
        bool schnittAktiv = mass is { MitSerie: true };
        var schnitt = schnittAktiv ? ctx.Schnitthoehe : double.PositiveInfinity;
        double boden = ctx.Geschoss?.Zahl("kote") ?? double.NegativeInfinity;

        // Senkrechte Leitung: im Grundriss nur ein Ring bzw. Rechteck
        if (HlksLinien.Laenge(xy) < 1)
        {
            var lage = HoeheVon(pts.Min(q => q[2]), pts.Max(q => q[2]), schnitt);
            teil.Primitive.AddRange(Steigleitung(pts[0][0], pts[0][1], mass, lage, pts.Max(q => q[2]) < boden - Eps, farbe, w, dn, M));
            teil.Fangpunkte = [[pts[0][0], pts[0][1]]];
            return teil;
        }

        // Teilstücke nach ihrer Höhe zur Schnittebene in Läufe teilen; senkrechte Stücke einer Polylinie: nur wenn geschnitten
        var laeufe = new List<Lauf>();
        Lauf? aktuell = null;
        for (int i = 1; i < pts.Count; i++)
        {
            var a = pts[i - 1]; var b = pts[i];
            if (Math.Abs(b[0] - a[0]) <= Eps && Math.Abs(b[1] - a[1]) <= Eps)
            {
                if (HoeheVon(Math.Min(a[2], b[2]), Math.Max(a[2], b[2]), schnitt) == Hoehe.Geschnitten)
                    teil.Primitive.AddRange(Steigleitung(a[0], a[1], mass, Hoehe.Geschnitten, false, farbe, w, dn, M));
                continue;
            }
            bool verdeckt = Math.Max(a[2], b[2]) < boden - Eps;
            if (aktuell is null || aktuell.Verdeckt != verdeckt) { aktuell = new Lauf { Verdeckt = verdeckt, Xy = [[a[0], a[1]]] }; laeufe.Add(aktuell); }
            aktuell.Xy.Add([b[0], b[1]]);
        }

        // Kanäle und Lüftungsrohre in echter Grösse (Christian 17.10.2026: Lüftung in mm), ab Modul 3 auch Rohre mit Serie
        if (!grob && mass is not null)
        {
            var breite = Math.Max(mass.Breite, 0.18 * M);   // ANNAHME: nie schmaler als die dünne Linie (0.18 mm Papier)
            foreach (var lauf in laeufe)
            {
                if (lauf.Verdeckt)
                    teil.Primitive.AddRange(Raender(lauf.Xy, breite, farbe, "strich"));
                else if (mass.Rohrband)
                    teil.Primitive.AddRange(linie == "voll"
                        ? HlksLinien.Band(lauf.Xy, breite, farbe ?? "schnitt")
                        : HlksLinien.BandGestrichelt(lauf.Xy, breite, farbe ?? "schnitt", M));
                else
                    foreach (var pr in HlksLinien.Band(lauf.Xy, breite, farbe ?? "schnitt"))
                    {
                        // Annahme: Deckkraft 0.2, Kontur dünn; an Knicken entstehen innere Kanten (Vereinigung folgt mit der Leitungsführung)
                        pr.Stil = pr.Art == "polygon"
                            ? new Stil { Stift = Stifte.Duenn, Linie = linie, Farbe = farbe, Fuellung = farbe ?? "schnitt", Deckkraft = 0.2 }
                            : new Stil { Stift = null, Fuellung = farbe ?? "schnitt", Deckkraft = 0.2 };
                        teil.Primitive.Add(pr);
                    }
                if (mass.Daemmdicke > 0)   // zweite Kontur, dünn gestrichelt
                    teil.Primitive.AddRange(Raender(lauf.Xy, mass.DaemmBreite, farbe, "strich"));
            }
            LeitungsText(ctx, e, xy, breite, teil);
            teil.Fangpunkte = xy.Select(p => new[] { p[0], p[1] }).ToList();
            teil.Fangkanten = Achse(xy);
            return teil;
        }

        // Strichbreite bis 0.35 mm Papier: Stift «mittel» (Annahme: grob 0.25 mm wird ebenfalls «mittel»); breiter: Band
        foreach (var lauf in laeufe)
        {
            if (w / M <= 0.35 + 1e-9)
                teil.Primitive.Add(new Primitiv
                {
                    Art = "polylinie", Punkte = lauf.Xy,
                    Stil = new Stil { Stift = HlksEinordnung.Stift(w / M), Linie = lauf.Verdeckt ? "strich" : linie, Farbe = farbe },
                });
            else if (lauf.Verdeckt)
                teil.Primitive.AddRange(Raender(lauf.Xy, w, farbe, "strich"));
            else
                teil.Primitive.AddRange(linie == "voll"
                    ? HlksLinien.Band(lauf.Xy, w, farbe ?? "schnitt")
                    : HlksLinien.BandGestrichelt(lauf.Xy, w, farbe ?? "schnitt", M));
        }

        LeitungsText(ctx, e, xy, w, teil);
        teil.Fangpunkte = xy.Select(q => new[] { q[0], q[1] }).ToList();
        teil.Fangkanten = Achse(xy);
        return teil;
    }

    /// <summary>Fangkanten einer Leitung (Modul 3, C3): die Achse, je Teilstück eine Kante (Punktpaar) – wie die Achse einer Wand.
    /// Damit rasten Werkzeuge auf der Leitung ein (Schnittpunkt mit anderen Kanten, Mitte, Lot, nächster Punkt), nicht nur auf Enden
    /// und Knicken. Gilt für Leitungen mit und ohne Serie; senkrechte Leitungen (ein Punkt im Grundriss) haben keine Kante.</summary>
    static List<double[][]> Achse(List<double[]> xy)
    {
        var kanten = new List<double[][]>();
        for (int i = 1; i < xy.Count; i++) kanten.Add([[xy[i - 1][0], xy[i - 1][1]], [xy[i][0], xy[i][1]]]);
        return kanten;
    }

    static Hoehe HoeheVon(double zmin, double zmax, double schnitt) =>
        zmin > schnitt + Eps ? Hoehe.Oben : zmax < schnitt - Eps ? Hoehe.Unten : Hoehe.Geschnitten;

    /// <summary>Die beiden Ränder eines Bandes der Breite b als dünne Linien in der Linienart (gestrichelt), ohne Füllung.</summary>
    static IEnumerable<Primitiv> Raender(List<double[]> xy, double b, string? farbe, string linienart)
    {
        foreach (var vorzeichen in new[] { 1, -1 })
            yield return new Primitiv
            {
                Art = "polylinie", Punkte = HlksLinien.Versatz(xy, vorzeichen * b / 2),
                Stil = new Stil { Stift = Stifte.Duenn, Linie = linienart, Farbe = farbe },
            };
    }

    /// <summary>Senkrechte Leitung im Grundriss. Mit Serie massstäblich (Kreis Ø aussen bzw. Rechteck b × h, Stift «mittel»), sonst
    /// wie bisher ein Ring max(0.9 w, DN/2). Füllung: geschnitten «schnitt», unterhalb «papier», oberhalb keine; Kontur ausgezogen,
    /// nur ganz verdeckt (unter dem Boden des Geschosses) gestrichelt.
    /// Mit Dämmung zusätzlich die zweite Kontur dünn gestrichelt.</summary>
    static IEnumerable<Primitiv> Steigleitung(double x, double y, LeitungsMass? mass, Hoehe lage, bool verdeckt, string? farbe, double w, double? dn, double M)
    {
        var fuellung = lage switch { Hoehe.Geschnitten => "schnitt", Hoehe.Unten => "papier", _ => null };
        var linienart = verdeckt ? "strich" : "voll";
        if (mass is { MitSerie: true })
        {
            var b = Math.Max(mass.Breite, 0.18 * M); var h = Math.Max(mass.Hoehe, 0.18 * M);
            var stil = new Stil { Stift = Stifte.Mittel, Linie = linienart, Farbe = farbe, Fuellung = fuellung };
            if (mass.Rechteck)
            {
                yield return new Primitiv { Art = "polygon", Punkte = Rechteck(x, y, b, h), Stil = stil };
                if (mass.Daemmdicke > 0)
                    yield return new Primitiv { Art = "polygon", Punkte = Rechteck(x, y, mass.DaemmBreite, mass.DaemmHoehe), Stil = new Stil { Stift = Stifte.Duenn, Linie = "strich", Farbe = farbe } };
            }
            else
            {
                yield return new Primitiv { Art = "kreis", Punkte = [[x, y]], Radius = b / 2, Stil = stil };
                if (mass.Daemmdicke > 0)
                    yield return new Primitiv { Art = "kreis", Punkte = [[x, y]], Radius = mass.DaemmBreite / 2, Stil = new Stil { Stift = Stifte.Duenn, Linie = "strich", Farbe = farbe } };
            }
            yield break;
        }
        yield return new Primitiv
        {
            Art = "kreis", Punkte = [[x, y]], Radius = Math.Max(w * 0.9, (dn ?? 0) / 2),
            Stil = new Stil { Stift = HlksEinordnung.Stift(w * 0.6 / M), Linie = linienart, Farbe = farbe, Fuellung = fuellung },
        };
    }

    static List<double[]> Rechteck(double x, double y, double b, double h) =>
        [[x - b / 2, y - h / 2], [x + b / 2, y - h / 2], [x + b / 2, y + h / 2], [x - b / 2, y + h / 2]];

    /// <summary>Beschriftung (Detailgrad fein) mittig über der Leitung: «DN 50»/«NW 50», «400×200», «Ø 250» (Leitungsbezeichnung).</summary>
    static void LeitungsText(GrafikKontext ctx, Element e, List<double[]> xy, double w, Grafikteil teil)
    {
        if (ctx.Detailgrad != "fein" || Leitungsbezeichnung.Text(ctx.Projekt, ctx.Ansicht, e) is not { } text) return;
        var m = HlksLinien.PunktBeiLaenge(xy, HlksLinien.Laenge(xy) / 2)!.Value;
        var grad = HlksLinien.LesbarerWinkel(m.Winkel);
        var rad = grad * Math.PI / 180;
        var abstand = w / 2 + 0.4 * ctx.Massstab;   // über der Linie, in Leserichtung gesehen
        teil.Primitive.Add(new Primitiv
        {
            Art = "text", Punkte = [[m.X - Math.Sin(rad) * abstand, m.Y + Math.Cos(rad) * abstand]],
            Text = text, Groesse = Schrift, Ausrichtung = "mitte", Winkel = grad,
        });
    }

    /// <summary>formteil() in zeichnen2d.js: gefüllter Kreis auf der Leitung, mit hellem Rand wie die Papierfarbe.</summary>
    static Grafikteil Formteil(GrafikKontext ctx, Element e, List<double[]> pts, MediumDef? md)
    {
        var M = ctx.Massstab;
        var r = Math.Max(0.7 * M, HlksEinordnung.Strichbreite(ctx.Detailgrad, HlksEinordnung.Dn(e), M) * 0.65);
        var mitte = new[] { pts[0][0], pts[0][1] };
        var teil = new Grafikteil { Id = e.Id, Ebene = "formteil", Fangpunkte = [mitte] };
        // Rand 0.1 mm Papier in der Farbe des Hintergrunds («papier»): ein etwas grösserer Kreis darunter (Annahme: statt Kontur)
        teil.Primitive.Add(new Primitiv { Art = "kreis", Punkte = [mitte], Radius = r + 0.05 * M, Stil = new Stil { Stift = null, Fuellung = "papier" } });
        teil.Primitive.Add(new Primitiv { Art = "kreis", Punkte = [mitte], Radius = r, Stil = new Stil { Stift = null, Fuellung = HlksEinordnung.Farbe(md) ?? "schnitt" } });
        return teil;
    }

    /// <summary>armatur() in zeichnen2d.js: Zähler = Kreis mit Kreuz, sonst Raute mit Kreuz.</summary>
    static Grafikteil Armatur(GrafikKontext ctx, Element e, List<double[]> pts, MediumDef? md)
    {
        var M = ctx.Massstab;
        var s = 1.2 * M;   // Grösse des Sinnbilds
        var (x, y) = (pts[0][0], pts[0][1]);
        var stift = HlksEinordnung.Stift(0.25);   // 0.25 mm Papier heute → «mittel»
        var sk = new Skizze(x, y, stift, HlksEinordnung.Farbe(md));
        var zaehler = e.Klasse.Contains("zaehler", StringComparison.OrdinalIgnoreCase) || e.Klasse.Contains("zähler", StringComparison.OrdinalIgnoreCase);
        if (zaehler)
        {
            var k = s * 0.7;
            sk.Kreis(0, 0, s).Linie(-k, -k, k, k).Linie(-k, k, k, -k);
        }
        else
        {
            var k = s * 0.65;
            sk.Pfad(0, -s * 1.3, [s * 1.3, 0], [0, s * 1.3], [-s * 1.3, 0]).Linie(-k, -k, k, k).Linie(-k, k, k, -k);
        }
        var teil = new Grafikteil { Id = e.Id, Ebene = "armatur", Primitive = sk.Primitive, Fangpunkte = [[x, y]] };
        if (ctx.Detailgrad == "fein")
            teil.Primitive.Add(Etikett(e.Kennzeichen, x + s * 1.6, y - Schrift * M * 0.35));
        return teil;
    }

    /// <summary>apparat() in zeichnen2d.js (auch Geräte): Sinnbild nach Klasse bzw. Rolle, Masse aus dem Katalog.</summary>
    static Grafikteil Apparat(GrafikKontext ctx, Element e, List<double[]> pts, MediumDef? md)
    {
        var M = ctx.Massstab;
        var (x, y) = (pts[0][0], pts[0][1]);
        var (b, t, _) = HlksEinordnung.Mass(ctx.Katalog, e);
        ctx.Katalog.Klassen.TryGetValue(e.Klasse, out var d);
        var stift = Stifte.Duenn;   // 0.18 mm Papier wie heute
        var farbe = HlksEinordnung.Farbe(md);
        var teil = new Grafikteil
        {
            Id = e.Id, Ebene = "apparat",
            Primitive = ctx.Detailgrad == "grob"
                ? HlksSinnbilder.Umriss(b, t, x, y, stift, farbe, "voll")
                : HlksSinnbilder.Zeichen(e.Klasse, d?.Rolle, b, t, x, y, stift, farbe, "voll"),
            // Mitte; die Anschlüsse haben im Modell noch keine Lage, deshalb zusätzlich die Ecken des Umrisses
            Fangpunkte = [[x, y], [x - b / 2, y - t / 2], [x + b / 2, y - t / 2], [x + b / 2, y + t / 2], [x - b / 2, y + t / 2]],
        };
        teil.Primitive.Add(Etikett(e.Kennzeichen, x + b / 2 + 0.6 * M, y - Schrift * M * 0.35));
        return teil;
    }

    /// <summary>sonst() in zeichnen2d.js: einfache Ersatzdarstellung, damit nichts unsichtbar bleibt.</summary>
    static Grafikteil? Sonst(GrafikKontext ctx, Element e, List<double[]> pts, MediumDef? md)
    {
        var M = ctx.Massstab;
        var farbe = HlksEinordnung.Farbe(md);
        var teil = new Grafikteil { Id = e.Id, Ebene = "sonst" };
        if (e.Geometrie?.Art == "polygon" && pts.Count >= 3)
        {
            var ring = pts.Select(q => new[] { q[0], q[1] }).ToList();
            // Annahme: die graue Fläche (CSS gr-fuell-sonst, #e6e6e6) nur am Bildschirm, auf dem Plan nur die Kontur
            teil.Primitive.Add(new Primitiv { Art = "polygon", Punkte = ring.Select(q => (double[])q.Clone()).ToList(), Stil = new Stil { Stift = null, Fuellung = "#e6e6e6", NurBildschirm = true } });
            teil.Primitive.Add(new Primitiv { Art = "polygon", Punkte = ring, Stil = new Stil { Stift = Stifte.Duenn, Farbe = farbe } });
            teil.Fangpunkte = ring.Select(q => new[] { q[0], q[1] }).ToList();
            return teil;
        }
        if (pts.Count >= 2)
        {
            var l = pts.Select(q => new[] { q[0], q[1] }).ToList();
            teil.Primitive.Add(new Primitiv { Art = "polylinie", Punkte = l, Stil = new Stil { Stift = HlksEinordnung.Stift(0.25), Farbe = farbe } });
            teil.Fangpunkte = l.Select(q => new[] { q[0], q[1] }).ToList();
            return teil;
        }
        var (x, y) = (pts[0][0], pts[0][1]);
        const double s = 150;   // halbe Kantenlänge des Ersatzquadrats (fest, Modell-mm)
        teil.Primitive.Add(new Primitiv
        {
            Art = "polygon", Punkte = [[x - s, y - s], [x + s, y - s], [x + s, y + s], [x - s, y + s]],
            Stil = new Stil { Stift = Stifte.Duenn, Farbe = farbe, Fuellung = "papier" },
        });
        teil.Primitive.Add(Etikett(e.Kennzeichen, x + s + 0.6 * M, y - Schrift * M * 0.35));
        teil.Fangpunkte = [[x, y]];
        return teil;
    }

    /// <summary>Kennzeichen-Etikett: nur am Bildschirm (Annahme: gehört nicht auf den Plan), 2.0 mm Papier.</summary>
    static Primitiv Etikett(string text, double x, double y) => new()
    {
        Art = "text", Punkte = [[x, y]], Text = text, Groesse = Schrift, Ausrichtung = "links",
        Stil = new Stil { NurBildschirm = true },
    };
}
