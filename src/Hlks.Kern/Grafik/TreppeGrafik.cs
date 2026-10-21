// Grafik der Treppe (Nachbearbeitung N17, Christian 21.10.2026: «auch treppen sind im Grundriss noch nicht korrekt angezeigt. resp.
// nicht erkennbar.»). Gezeichnet wird aus den Stufenkanten, Podesten und der Lauflinie des Elements (Treppenlage.cs); die Darstellung
// folgt dem Planbild (H+K, Revit): Entscheide Hauptlauf.
//
// Grundriss (Schnitthöhe zs absolut):
//   - Stufenkanten (Steigungslinien) und Seitenlinien der Trittflächen: dünn; Kanten bzw. Stücke mit z ≤ zs durchgezogen, darüber
//     gestrichelt (die Treppe läuft über die Schnittebene hinauf). Podestumrisse ebenso nach ihrer Höhe.
//   - Schnittlinie: wo die Steigungslinie (linear zwischen den zwei Stufenkanten, die zs einschliessen) die Schnitthöhe erreicht, eine
//     schräge Doppellinie unter 60° zur Gehrichtung (Papierabstand 1 mm, Stift mittel; Annahme: Zahlen und Stift ohne Quelle). Liegen die
//     zwei Kanten nicht an derselben Trittfläche (Podest dazwischen), steht sie auf der oberen Kante.
//   - Lauflinie dünn mit gefülltem Kreis am Antritt (Ø 1.8 mm) und gefülltem Pfeil am Austritt (3 mm lang, 2 mm breit), Papier-mm.
//   - Beschriftung «n Stg. h/a» (Steigungen, Steigung/Auftritt in cm auf 0.5 mm gerundet, z. B. «16 Stg. 17.5/28») neben der Mitte der
//     Lauflinie, 2.5 mm hoch, in Gehrichtung (Mitdrehen: bleibt in gedrehten Ansichten lesbar, N9).
//   - Im Geschoss darüber (Grafik.Sichtbar → <see cref="AusUnterGeschoss"/>) liegt die ganze Treppe unter der Schnittebene: alles
//     durchgezogen, ohne Schnittlinie – die Treppe von oben.
//   - Eine unsichtbare Fläche über den Trittflächen (nur Bildschirm) macht die Treppe anwählbar.
// Schnitt und 3D: Stufenprismen – je Trittfläche (und Podest) ein Prisma von der Trittfläche bis laufdicke darunter. Begründung (statt
// des Original-Netzes): Die Netzdatei kennt nur «übernommen» oder «Hintergrund»; das Original-Netz als Hintergrund zu lassen hiesse, den
// Grundrissschnitt der Unterlage wieder mit dem Gewirr aus Stufenkanten zu füllen. Die Prismen gehören dem Element (editierbar, im
// Schnitt mit Schraffur nach Material wie Stützen, im IFC-Export als Körper); die exakte Geometrie bleibt für die Kollisionsprüfung
// in der Netzdatei.

namespace Hlks.Kern;

internal static class TreppeGrafik
{
    const double KreisRadius = 0.9;        // mm Papier
    const double PfeilLaenge = 3.0;        // mm Papier
    const double PfeilHalbbreite = 1.0;    // mm Papier
    const double Schrift = 2.5;            // mm Papier
    const double Textabstand = 0.6;        // mm Papier, Grundlinie über der Lauflinie
    const double Schnittabstand = 1.0;     // mm Papier zwischen den zwei Linien der Schnittlinie
    const double SchnittwinkelGrad = 60;   // zur Gehrichtung

    // ------------------------------------------------------------------ Sichtbarkeit

    /// <summary>Gehört die Treppe, die nicht in diesem Geschoss steht, doch in den Grundriss? Ja, wenn sie von unten bis in dieses
    /// Geschoss reicht: ihre Oberkante liegt über dem Boden des Geschosses (50 mm Toleranz) und ihre Unterkante unter der Schnittebene
    /// und unter dem Boden des nächsten Geschosses. Beispiel: die Treppe EG → OG erscheint im OG von oben, ein Dachstock-Aufgang
    /// (Antritt im OG, Austritt im DG) nicht im EG.</summary>
    public static bool AusUnterGeschoss(GrafikKontext ctx, Element e)
    {
        if (ctx.Geschoss is null || e.Zahl("unterkante") is not double unten || e.Zahl("oberkante") is not double oben) return false;
        double kote = ctx.Geschoss.Zahl("kote") ?? 0;
        double bis = ctx.Schnitthoehe;
        var darueber = ctx.Modell.DerKlasse("Geschoss").Select(g => g.Zahl("kote")).OfType<double>().Where(k => k > kote + 1).DefaultIfEmpty(double.MaxValue).Min();
        if (darueber < double.MaxValue) bis = Math.Min(bis, darueber - 50);
        return oben > kote - 50 && unten < bis;
    }

    // ------------------------------------------------------------------ Grundriss

    public static IEnumerable<Grafikteil> Plan(GrafikKontext ctx, Element e)
    {
        var t = Treppenlage.Von(e);
        if (t is null || (t.Kanten.Count == 0 && t.Podeste.Count == 0 && t.Gehlinie.Count < 2)) return [];
        double zc = ctx.Schnitthoehe, zs = zc + 0.5;
        var prim = new List<Primitiv>();
        Stil Linienstil(double z, string stift = Stifte.Duenn) => new() { Stift = stift, Linie = z <= zs ? "voll" : "strich" };
        static Primitiv Strecke(double[] a, double[] b, Stil s) => new() { Art = "linie", Punkte = [[a[0], a[1]], [b[0], b[1]]], Stil = s };
        static double[] Zwischen(double[] a, double[] b, double f) => [a[0] + f * (b[0] - a[0]), a[1] + f * (b[1] - a[1])];

        // Anwählbar: unsichtbare Fläche über den Trittflächen
        foreach (var tr in t.Tritte())
            prim.Add(new Primitiv { Art = "polygon", Punkte = tr.Ring, Stil = new Stil { Stift = null, Fuellung = "papier", Deckkraft = 0.01, NurBildschirm = true } });

        // Stufenkanten
        foreach (var k in t.Kanten) prim.Add(Strecke([k[0], k[1]], [k[2], k[3]], Linienstil(k[4])));

        // Seitenlinien der Trittflächen; die Linie wird dort geteilt, wo die Steigungslinie die Schnitthöhe erreicht
        var tritte = t.TrittIndizes();
        foreach (var i in tritte)
        {
            var k = t.Kanten[i]; var n = t.Kanten[i + 1];
            foreach (var (a, b) in new[] { ((double[])[k[0], k[1]], (double[])[n[0], n[1]]), ((double[])[k[2], k[3]], (double[])[n[2], n[3]]) })
            {
                if (n[4] <= zs) prim.Add(Strecke(a, b, Linienstil(n[4])));
                else if (k[4] > zs) prim.Add(Strecke(a, b, Linienstil(k[4])));
                else
                {
                    var m = Zwischen(a, b, (zc - k[4]) / (n[4] - k[4]));
                    prim.Add(Strecke(a, m, Linienstil(zs)));
                    prim.Add(Strecke(m, b, Linienstil(zs + 1)));
                }
            }
        }

        // Podeste
        foreach (var p in t.Podeste) prim.Add(new Primitiv { Art = "polygon", Punkte = p.Ring, Stil = Linienstil(p.Z) });

        // Schnittlinie: zwischen der letzten Kante unter und der ersten über der Schnittebene
        int j = t.Kanten.FindIndex(k => k[4] > zs);
        if (j > 0)
        {
            var oben = t.Kanten[j]; var unten = t.Kanten[j - 1];
            double[] a = [oben[0], oben[1]], b = [oben[2], oben[3]];
            if (tritte.Contains(j - 1))
            {
                double f = (zc - unten[4]) / (oben[4] - unten[4]);
                a = Zwischen([unten[0], unten[1]], a, f); b = Zwischen([unten[2], unten[3]], b, f);
            }
            prim.AddRange(Schnittlinie(ctx, a, b));
        }

        // Lauflinie mit Kreis am Antritt und Pfeil am Austritt
        var gehlinie = t.Gehlinie;
        if (gehlinie.Count >= 2)
        {
            prim.Add(new Primitiv { Art = "polylinie", Punkte = gehlinie.Select(q => new[] { q[0], q[1] }).ToList(), Stil = new Stil { Stift = Stifte.Duenn } });
            var antritt = t.Antritt;
            prim.Add(new Primitiv { Art = "kreis", Punkte = [[antritt[0], antritt[1]]], Radius = ctx.AufModell(KreisRadius), Stil = new Stil { Stift = Stifte.Duenn, Fuellung = "schnitt" } });
            var spitze = t.Austritt; var davor = t.Aufwaerts ? gehlinie[^2] : gehlinie[1];
            prim.Add(Pfeil(ctx, [davor[0], davor[1]], [spitze[0], spitze[1]]));
            if (Beschriftung(t) is { Length: > 0 } text) prim.Add(Text(ctx, gehlinie, text));
        }

        var fang = new List<double[]>();
        foreach (var k in t.Kanten) { fang.Add([k[0], k[1]]); fang.Add([k[2], k[3]]); }
        if (gehlinie.Count >= 2) { fang.Add([gehlinie[0][0], gehlinie[0][1]]); fang.Add([gehlinie[^1][0], gehlinie[^1][1]]); }
        return
        [
            new Grafikteil
            {
                Id = e.Id, Ebene = "wand", Primitive = prim,
                Fangpunkte = fang.Count > 0 ? fang : null,
                Fangkanten = t.Kanten.Count > 0 ? t.Kanten.Select(k => new[] { new[] { k[0], k[1] }, new[] { k[2], k[3] } }).ToList() : null,
            },
        ];
    }

    /// <summary>Doppelte schräge Schnittlinie quer über den Lauf (Kante a → b von rechts nach links in Gehrichtung).</summary>
    static IEnumerable<Primitiv> Schnittlinie(GrafikKontext ctx, double[] a, double[] b)
    {
        double ex = b[0] - a[0], ey = b[1] - a[1], w = Math.Sqrt(ex * ex + ey * ey);
        if (w < 1) yield break;
        double lx = ex / w, ly = ey / w, ux = ly, uy = -lx;   // links bzw. Gehrichtung
        double c = Math.Cos(SchnittwinkelGrad * Math.PI / 180), s = Math.Sin(SchnittwinkelGrad * Math.PI / 180);
        double dx = c * ux + s * lx, dy = c * uy + s * ly, halb = w / 2 / s;
        double mx = (a[0] + b[0]) / 2, my = (a[1] + b[1]) / 2, abstand = ctx.AufModell(Schnittabstand);
        foreach (var v in new[] { -0.5, 0.5 })
        {
            double px = mx + ux * v * abstand, py = my + uy * v * abstand;
            yield return new Primitiv { Art = "linie", Punkte = [[px - dx * halb, py - dy * halb], [px + dx * halb, py + dy * halb]], Stil = new Stil { Stift = Stifte.Mittel } };
        }
    }

    static Primitiv Pfeil(GrafikKontext ctx, double[] von, double[] spitze)
    {
        double dx = spitze[0] - von[0], dy = spitze[1] - von[1], l = Math.Sqrt(dx * dx + dy * dy);
        if (l < 1e-6) { dx = 1; dy = 0; l = 1; }
        dx /= l; dy /= l;
        double len = ctx.AufModell(PfeilLaenge), breit = ctx.AufModell(PfeilHalbbreite);
        double bx = spitze[0] - dx * len, by = spitze[1] - dy * len;
        return new Primitiv
        {
            Art = "polygon", Punkte = [[spitze[0], spitze[1]], [bx - dy * breit, by + dx * breit], [bx + dy * breit, by - dx * breit]],
            Stil = new Stil { Stift = Stifte.Duenn, Fuellung = "schnitt" },
        };
    }

    /// <summary>«n Stg. h/a»: Steigungen, Steigung und Auftritt in cm (auf 0.5 mm gerundet); fehlende Werte entfallen.</summary>
    internal static string Beschriftung(Treppenlage t)
    {
        static string Cm(double mm) => Grafik.Format(Math.Round(mm * 2) / 2 / 10, 2).TrimEnd('0').TrimEnd('.');
        var teile = new List<string>();
        int? n = t.Stufen is double s ? (int)Math.Round(s) : t.Kanten.Count > 0 ? t.Kanten.Count : null;
        if (n is not null) teile.Add($"{n} Stg.");
        if (t.Steigung is double h && t.Auftritt is double a) teile.Add($"{Cm(h)}/{Cm(a)}");
        else if (t.Steigung is double h2) teile.Add(Cm(h2));
        return string.Join(" ", teile);
    }

    static Primitiv Text(GrafikKontext ctx, List<double[]> linie, string text)
    {
        double gesamt = 0;
        for (int i = 1; i < linie.Count; i++) gesamt += Abstand(linie[i - 1], linie[i]);
        double ziel = gesamt / 2, lauf = 0;
        double[] mitte = [linie[0][0], linie[0][1]], richtung = [1, 0];
        for (int i = 1; i < linie.Count; i++)
        {
            double l = Abstand(linie[i - 1], linie[i]);
            if (l < 1e-9) continue;
            richtung = [(linie[i][0] - linie[i - 1][0]) / l, (linie[i][1] - linie[i - 1][1]) / l];
            if (lauf + l >= ziel || i == linie.Count - 1)
            {
                double f = Math.Clamp((ziel - lauf) / l, 0, 1);
                mitte = [linie[i - 1][0] + f * (linie[i][0] - linie[i - 1][0]), linie[i - 1][1] + f * (linie[i][1] - linie[i - 1][1])];
                break;
            }
            lauf += l;
        }
        double winkel = BeschriftungHilfen.LesbarerWinkel(Math.Atan2(richtung[1], richtung[0]) * 180 / Math.PI);
        var oben = BeschriftungHilfen.Drehen([0, 1], winkel);
        double ab = ctx.AufModell(Textabstand);
        return BeschriftungHilfen.Text([mitte[0] + oben[0] * ab, mitte[1] + oben[1] * ab], text, Schrift, "mitte", winkel).Mitdrehend();
    }

    static double Abstand(double[] a, double[] b) => Math.Sqrt((b[0] - a[0]) * (b[0] - a[0]) + (b[1] - a[1]) * (b[1] - a[1]));

    // ------------------------------------------------------------------ Körper (Schnitt, 3D)

    /// <summary>Stufenprismen: je Trittfläche und Podest ein Prisma von der Trittfläche bis laufdicke (mindestens eine Steigung) darunter.</summary>
    public static Koerper? Koerper(Element e)
    {
        if (Treppenlage.Von(e) is not { } t) return null;
        var tritte = t.Tritte();
        if (tritte.Count == 0) return null;
        double dicke = Math.Max(t.Laufdicke ?? Treppenlage.VorgabeLaufdicke, t.Steigung ?? 0);
        return new Koerper
        {
            Id = e.Id, Ebene = "wand",
            Teile = tritte.Select(tr => new Koerperteil { Art = "prisma", Grundriss = tr.Ring, Unten = tr.Z - dicke, Oben = tr.Z, Material = "wand" }).ToList(),
        };
    }
}
