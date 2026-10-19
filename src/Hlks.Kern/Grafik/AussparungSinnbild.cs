// Sinnbild einer Aussparung (Modul 3b, Nachtrag B4d) für Grundriss, Aussparungsplan, Schnitt und Ansicht.
//
// QUELLE: H+K-Bürostandard nach Vorlage Aussparungsplan Bernstrasse 1.UG (24.09.2024, massgebend für die Ausführung; Hauptlauf
// 19.10.2026) und SIA 400:2000 Ziff. B.10.2 Tabelle 5 «zusätzliche Kennzeichnungen» (S. 41), SIA 410/2 Ziff. 2 1 (S. 12: «Sinnbild immer
// massstäblich zeichnen») – zusammengefasst in entwurf/modul-3b.md, Abschnitt «SIA 400 / SIA 410/2»:
//   - Sinnbild des Durchbruchs = massstäbliches Rechteck OHNE Kreuz, dünner Rand in der Planfarbe.
//   - Kennfarbe als volle Füllung: Decken und Bodenkonstruktionen (horizontal) gelb, Wände und Pfeiler (vertikal) braun, beides
//     gelb/braun diagonal geteilt (gelb oben links, braun unten rechts).
//   - Zeichnerische Ergänzung, dünn: horizontal ein Winkel innen parallel zur oberen und linken Kante (SIA «oben und links»); vertikal
//     ein Strich innen entlang der Kante, die in Wandrichtung unten bzw. rechts liegt (Vorlage: ein Strich; SIA «unten und rechts»);
//     beides: Winkel, Strich und Diagonale (unten links → oben rechts). «Oben, links, unten, rechts» gelten auf dem Blatt (Ansicht ohne
//     Drehung: Blatt = Modell-x/y im Grundriss, u/v in Schnitt und Ansicht).
//
// ANNAHMEN (Prüfliste Christian; Darstellung, keine Normwerte – die Norm nennt nur «gelb» und «braun»):
//   Farben wie die Vorlage: Gelb #ffc000, Braun #7f3f00, Deckkraft 1 (voll). Rand, Winkel, Strich und Diagonale Stift «dünn» in der
//   Planfarbe. Winkel: Ecke 1.0 mm Papier innen, Schenkel bis 0.25 mm Papier vor die Gegenkanten; Wandstrich 0.5 mm Papier innen über
//   die ganze Länge (aus der Vorlage 1:50 abgegriffen); beides höchstens ein Viertel der kleinsten Breite. Wandrichtung im Grundriss = U
//   der Aussparung (Strich auf der Kante parallel zur Wand); ohne Wandrichtung (Schnitt, Ansicht) die längste Kante. Ein Umriss, der kein
//   Rechteck ist (schräge Achse: Hülle der Projektion), bekommt den Winkel an der Ecke, die am weitesten oben links liegt.
//   Runde Aussparung (Kernbohrung, nur von Hand; SIA regelt sie nicht eigens – ANNAHME runder Durchbruch): massstäblicher Kreis, Kennfarbe
//   als Füllung (beides: Halbkreise oben links gelb, unten rechts braun), Ergänzung als dünner innerer Halbbogen (45°…225° oben links
//   bzw. 225°…45° unten rechts), beides mit Diagonale.
//   Status (vorgeschlagen, freigegeben …) nur am Bildschirm (NurBildschirm): Rahmen in der Statusfarbe 0.7 mm Papier ausserhalb,
//   Stift «mittel». Im PDF und auf dem Plan steht nur die Darstellung nach Vorlage und SIA.

namespace Hlks.Kern;

internal static class AussparungSinnbild
{
    const double StatusAbstand = 0.7;   // mm Papier (Annahme, Dateikopf)
    const double WinkelEinzug = 1.0;    // mm Papier: Ecke des Winkels (horizontal)
    const double WinkelEnde = 0.25;     // mm Papier: Schenkelende vor der Gegenkante
    const double StrichEinzug = 0.5;    // mm Papier: Wandstrich (vertikal)
    const int Bogenpunkte = 24;         // Stützpunkte je Halbkreis der geteilten Kennfarbe

    static Stil Kennfarbe(string farbe) => new() { Stift = null, Fuellung = farbe, Deckkraft = AussparungGrafik.KennDeckkraft };
    static Stil Umriss() => new() { Stift = Stifte.Duenn };
    static Stil Ergaenzung() => new() { Stift = Stifte.Duenn };
    static Stil Status(string farbe) => new() { Stift = Stifte.Mittel, Farbe = farbe, NurBildschirm = true };

    /// <summary>Konvexes Vieleck (Umriss der Aussparung im Bild) mit Kennfarbe, Umriss, Ergänzung und Statusrahmen (Dateikopf).
    /// wandRichtung: Richtung der Wand im Bild (Grundriss), sonst null. Rückgabe: rechter Rand (grösstes x) des gedruckten Sinnbilds.</summary>
    public static double Vieleck(GrafikKontext ctx, Grafikteil teil, List<double[]> ecken, Bauteillage lage, string statusFarbe, double[]? wandRichtung = null)
    {
        var e = Gegenuhrzeiger(ecken);
        // Kennfarbe
        if (lage == Bauteillage.Beides && Teilen(e) is var (gelb, braun) && gelb.Count >= 3 && braun.Count >= 3)
        {
            teil.Primitive.Add(new Primitiv { Art = "polygon", Punkte = gelb, Stil = Kennfarbe(AussparungGrafik.Gelb) });
            teil.Primitive.Add(new Primitiv { Art = "polygon", Punkte = braun, Stil = Kennfarbe(AussparungGrafik.Braun) });
        }
        else
            teil.Primitive.Add(new Primitiv { Art = "polygon", Punkte = Kopie(e), Stil = Kennfarbe(lage == Bauteillage.Vertikal ? AussparungGrafik.Braun : AussparungGrafik.Gelb) });
        // Umriss (Rechteck ohne Kreuz)
        teil.Primitive.Add(new Primitiv { Art = "polygon", Punkte = Kopie(e), Stil = Umriss() });
        // zeichnerische Ergänzung: Winkel oben links (horizontal), Strich unten bzw. rechts (vertikal)
        double viertel = Breite(e) / 4;
        if (lage != Bauteillage.Vertikal) teil.Primitive.Add(Winkel(e, Ecke(e, -1, 1), Math.Min(ctx.AufModell(WinkelEinzug), viertel), Math.Min(ctx.AufModell(WinkelEnde), viertel)));
        if (lage != Bauteillage.Horizontal) teil.Primitive.Add(Strich(e, Math.Min(ctx.AufModell(StrichEinzug), viertel), wandRichtung));
        if (lage == Bauteillage.Beides)
        {
            var (uLinks, oRechts) = (e[Ecke(e, -1, -1)], e[Ecke(e, 1, 1)]);
            teil.Primitive.Add(new Primitiv { Art = "linie", Punkte = [[uLinks[0], uLinks[1]], [oRechts[0], oRechts[1]]], Stil = Ergaenzung() });
        }
        // Status nur am Bildschirm
        teil.Primitive.Add(new Primitiv { Art = "polygon", Punkte = Einruecken(e, -ctx.AufModell(StatusAbstand)), Stil = Status(statusFarbe) });
        return e.Max(q => q[0]);
    }

    /// <summary>Kreis (runde Aussparung) mit Kennfarbe, Umriss, Ergänzung und Statusrahmen. Rückgabe: rechter Rand (cx + r).</summary>
    public static double Kreis(GrafikKontext ctx, Grafikteil teil, double cx, double cy, double r, Bauteillage lage, string statusFarbe)
    {
        if (lage == Bauteillage.Beides)
        {
            teil.Primitive.Add(new Primitiv { Art = "polygon", Punkte = Halbkreis(cx, cy, r, 45), Stil = Kennfarbe(AussparungGrafik.Gelb) });
            teil.Primitive.Add(new Primitiv { Art = "polygon", Punkte = Halbkreis(cx, cy, r, 225), Stil = Kennfarbe(AussparungGrafik.Braun) });
        }
        else
            teil.Primitive.Add(new Primitiv { Art = "kreis", Punkte = [[cx, cy]], Radius = r, Stil = Kennfarbe(lage == Bauteillage.Vertikal ? AussparungGrafik.Braun : AussparungGrafik.Gelb) });
        teil.Primitive.Add(new Primitiv { Art = "kreis", Punkte = [[cx, cy]], Radius = r, Stil = Umriss() });
        if (lage != Bauteillage.Vertikal)
            teil.Primitive.Add(new Primitiv { Art = "bogen", Punkte = [[cx, cy]], Radius = r - Math.Min(ctx.AufModell(WinkelEinzug), r / 2), Von = 45, Bis = 225, Stil = Ergaenzung() });
        if (lage != Bauteillage.Horizontal)
            teil.Primitive.Add(new Primitiv { Art = "bogen", Punkte = [[cx, cy]], Radius = r - Math.Min(ctx.AufModell(StrichEinzug), r / 2), Von = 225, Bis = 45, Stil = Ergaenzung() });
        if (lage == Bauteillage.Beides)
        {
            double a = r * Math.Sqrt(0.5);
            teil.Primitive.Add(new Primitiv { Art = "linie", Punkte = [[cx - a, cy - a], [cx + a, cy + a]], Stil = Ergaenzung() });
        }
        teil.Primitive.Add(new Primitiv { Art = "kreis", Punkte = [[cx, cy]], Radius = r + ctx.AufModell(StatusAbstand), Stil = Status(statusFarbe) });
        return cx + r;
    }

    /// <summary>Winkel an der Ecke c: die beiden Kanten an c um einzug nach innen, die übrigen um ende (Schenkel enden kurz vor den
    /// Gegenkanten).</summary>
    static Primitiv Winkel(List<double[]> e, int c, double einzug, double ende)
    {
        int n = e.Count;
        var innen = Einruecken(e, j => j == c || j == (c - 1 + n) % n ? einzug : ende);
        var a = innen[(c - 1 + n) % n]; var b = innen[c]; var d = innen[(c + 1) % n];
        return new Primitiv { Art = "polylinie", Punkte = [[a[0], a[1]], [b[0], b[1]], [d[0], d[1]]], Stil = Ergaenzung() };
    }

    /// <summary>Wandstrich: die Kante parallel zur Wandrichtung (ohne Richtung: die längsten Kanten), deren Aussennormale am weitesten
    /// nach unten rechts zeigt, um einzug nach innen versetzt, über die ganze Länge.</summary>
    static Primitiv Strich(List<double[]> e, double einzug, double[]? wandRichtung)
    {
        int n = e.Count;
        var laengen = Enumerable.Range(0, n).Select(j => Math.Sqrt(Math.Pow(e[(j + 1) % n][0] - e[j][0], 2) + Math.Pow(e[(j + 1) % n][1] - e[j][1], 2))).ToList();
        double max = laengen.Max();
        bool Kandidat(int j)
        {
            if (laengen[j] < 1e-9) return false;
            if (wandRichtung is { } w)
            {
                double dx = (e[(j + 1) % n][0] - e[j][0]) / laengen[j], dy = (e[(j + 1) % n][1] - e[j][1]) / laengen[j];
                return Math.Abs(dx * w[1] - dy * w[0]) < 0.1;
            }
            return laengen[j] >= max - 1e-6;
        }
        var kandidaten = Enumerable.Range(0, n).Where(Kandidat).ToList();
        if (kandidaten.Count == 0) kandidaten = Enumerable.Range(0, n).Where(j => laengen[j] >= max - 1e-6).ToList();
        // Aussennormale im Gegenuhrzeigersinn = rechts der Kante (dy, −dx); Richtung unten rechts (1, −1)
        int k = kandidaten.OrderByDescending(j => ((e[(j + 1) % n][1] - e[j][1]) - -(e[(j + 1) % n][0] - e[j][0])) / laengen[j]).ThenBy(j => j).First();
        var p = e[k]; var q = e[(k + 1) % n];
        double ix = -(q[1] - p[1]) / laengen[k] * einzug, iy = (q[0] - p[0]) / laengen[k] * einzug;   // nach innen = links der Kante
        return new Primitiv { Art = "polylinie", Punkte = [[p[0] + ix, p[1] + iy], [q[0] + ix, q[1] + iy]], Stil = Ergaenzung() };
    }

    /// <summary>Marke ohne Lage (achse oder Masse fehlen): Quadrat mit Diagonalkreuz in der Planfarbe, Status nur am Bildschirm.</summary>
    public static double Marke(GrafikKontext ctx, Grafikteil teil, double cx, double cy, double kante, string statusFarbe)
    {
        double h = ctx.AufModell(kante) / 2;
        List<double[]> q = [[cx - h, cy - h], [cx + h, cy - h], [cx + h, cy + h], [cx - h, cy + h]];
        teil.Primitive.Add(new Primitiv { Art = "polygon", Punkte = Kopie(q), Stil = Umriss() });
        var duenn = new Stil { Stift = Stifte.Duenn };
        teil.Primitive.Add(new Primitiv { Art = "linie", Punkte = [[cx - h, cy - h], [cx + h, cy + h]], Stil = duenn });
        teil.Primitive.Add(new Primitiv { Art = "linie", Punkte = [[cx - h, cy + h], [cx + h, cy - h]], Stil = duenn });
        teil.Primitive.Add(new Primitiv { Art = "polygon", Punkte = Einruecken(q, -ctx.AufModell(StatusAbstand)), Stil = Status(statusFarbe) });
        teil.Fangpunkte = [[cx, cy]];
        return cx + h;
    }

    // ------------------------------------------------------------------ Geometrie

    static List<double[]> Kopie(List<double[]> e) => e.Select(q => new[] { q[0], q[1] }).ToList();

    static double Flaeche2(List<double[]> e)
    {
        double a = 0;
        for (int i = 0; i < e.Count; i++) { var p = e[i]; var q = e[(i + 1) % e.Count]; a += p[0] * q[1] - q[0] * p[1]; }
        return a;
    }

    /// <summary>Ecken im Gegenuhrzeigersinn.</summary>
    internal static List<double[]> Gegenuhrzeiger(List<double[]> e)
    {
        var k = Kopie(e);
        if (Flaeche2(k) < 0) k.Reverse();
        return k;
    }

    /// <summary>Index der Ecke, die am weitesten in Richtung (dx, dy) liegt (bei Gleichstand die erste).</summary>
    internal static int Ecke(List<double[]> e, double dx, double dy)
    {
        int best = 0;
        double bw = double.MinValue;
        for (int i = 0; i < e.Count; i++)
        {
            double w = e[i][0] * dx + e[i][1] * dy;
            if (w > bw + 1e-6) { bw = w; best = i; }
        }
        return best;
    }

    /// <summary>Kleinste Breite eines konvexen Vielecks (je Kante der grösste Abstand der Ecken von ihrer Geraden).</summary>
    static double Breite(List<double[]> e)
    {
        double min = double.MaxValue;
        for (int i = 0; i < e.Count; i++)
        {
            var p = e[i]; var q = e[(i + 1) % e.Count];
            double dx = q[0] - p[0], dy = q[1] - p[1], l = Math.Sqrt(dx * dx + dy * dy);
            if (l < 1e-9) continue;
            double max = e.Max(r => Math.Abs((r[0] - p[0]) * dy - (r[1] - p[1]) * dx) / l);
            min = Math.Min(min, max);
        }
        return min == double.MaxValue ? 0 : min;
    }

    /// <summary>Konvexes Vieleck (Gegenuhrzeigersinn) um abstand nach innen gerückt (negativ: nach aussen).</summary>
    internal static List<double[]> Einruecken(List<double[]> e, double abstand) => Einruecken(e, _ => abstand);

    /// <summary>Konvexes Vieleck (Gegenuhrzeigersinn), jede Kante j (Ecke j → j + 1) um abstand(j) nach innen gerückt.</summary>
    internal static List<double[]> Einruecken(List<double[]> e, Func<int, double> abstandJeKante)
    {
        int n = e.Count;
        var linien = new (double Px, double Py, double Dx, double Dy)[n];
        for (int i = 0; i < n; i++)
        {
            var p = e[i]; var q = e[(i + 1) % n];
            double dx = q[0] - p[0], dy = q[1] - p[1], l = Math.Sqrt(dx * dx + dy * dy);
            if (l < 1e-12) { linien[i] = (p[0], p[1], 0, 0); continue; }
            dx /= l; dy /= l;
            double abstand = abstandJeKante(i);
            linien[i] = (p[0] - dy * abstand, p[1] + dx * abstand, dx, dy);   // innen = links der Kante
        }
        var aus = new List<double[]>(n);
        for (int i = 0; i < n; i++)
        {
            var a = linien[(i - 1 + n) % n]; var b = linien[i];
            double kreuz = a.Dx * b.Dy - a.Dy * b.Dx;
            if (Math.Abs(kreuz) < 1e-9) { aus.Add([b.Px, b.Py]); continue; }
            double t = ((b.Px - a.Px) * b.Dy - (b.Py - a.Py) * b.Dx) / kreuz;
            aus.Add([a.Px + a.Dx * t, a.Py + a.Dy * t]);
        }
        return aus;
    }

    /// <summary>Vieleck entlang der Diagonale unten links → oben rechts teilen: (oben links, unten rechts).</summary>
    static (List<double[]> Gelb, List<double[]> Braun) Teilen(List<double[]> e)
    {
        int ul = Ecke(e, -1, -1), ore = Ecke(e, 1, 1), n = e.Count;
        if (ul == ore) return ([], []);
        List<double[]> Weg(int von, int bis)
        {
            var w = new List<double[]>();
            for (int i = von; ; i = (i + 1) % n) { w.Add([e[i][0], e[i][1]]); if (i == bis) break; }
            return w;
        }
        return (Weg(ore, ul), Weg(ul, ore));   // im Gegenuhrzeigersinn: oben rechts → oben links → unten links ist der obere linke Teil
    }

    /// <summary>Halbkreis ab dem Winkel von (Grad, Gegenuhrzeigersinn) über 180°, als Vieleck (Sehne schliesst).</summary>
    static List<double[]> Halbkreis(double cx, double cy, double r, double von)
    {
        var aus = new List<double[]>(Bogenpunkte + 1);
        for (int i = 0; i <= Bogenpunkte; i++)
        {
            double w = (von + 180.0 * i / Bogenpunkte) * Math.PI / 180;
            aus.Add([cx + r * Math.Cos(w), cy + r * Math.Sin(w)]);
        }
        return aus;
    }
}
