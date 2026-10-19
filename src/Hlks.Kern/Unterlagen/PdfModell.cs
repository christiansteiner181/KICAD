// PDF lesen (Modul 2, Auftrag B2): Rohdaten einer Seite – Wörter mit Lage, Vektorpfade, eingebettete Bilder – in einfachen
// Typen, alles in pt, Ursprung unten links der sichtbaren Seite (CropBox), y nach oben. Gelesen mit PdfPig (Apache-2.0).
// Hier nur Rohdaten; Zeilen, Tabellen und Masslinien stehen in PdfTabellen.cs, PdfMasse.cs, die Befunde in PdfLeser.cs.

using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Graphics;
using UglyToad.PdfPig.Util;
using UglyToad.PdfPig.Graphics.Core;

namespace Hlks.Kern;

/// <summary>Ein Wort mit Box. Dreh: 0 waagrecht, 90 von unten nach oben gelesen, 270 von oben nach unten, 180 kopfstehend.</summary>
sealed class PWort
{
    public string Text = "";
    public double L, B, R, T;
    /// <summary>Grundlinie (y bei waagrechtem Text, x bei senkrechtem).</summary>
    public double Grund;
    public double Groesse;
    public int Dreh;
    /// <summary>0 = normaler Text; 1 oder 2 = Text, der mit anderem Text übereinanderliegt (zwei Sprachen, eine verdeckt), Ebene 1 bzw. 2.</summary>
    public int Ebene;
    public double Breite => R - L;
    public double Hoehe => T - B;
}

/// <summary>Eine Strecke eines Vektorpfads (Linie, Kante eines Rechtecks, Mittellinie eines dünnen gefüllten Rechtecks).</summary>
readonly record struct PStrecke(double X0, double Y0, double X1, double Y1, double Breite, bool AusFlaeche)
{
    public double Laenge => Math.Sqrt((X1 - X0) * (X1 - X0) + (Y1 - Y0) * (Y1 - Y0));
    public bool Waagrecht => Math.Abs(Y1 - Y0) <= 0.4 && Math.Abs(X1 - X0) > 0.4;
    public bool Senkrecht => Math.Abs(X1 - X0) <= 0.4 && Math.Abs(Y1 - Y0) > 0.4;
}

/// <summary>Ein gemeldeter Vektorpfad: Art linie, polylinie, bogen oder kreis.</summary>
sealed class PPfad
{
    public string Art = "linie";
    public List<double[]> Punkte = [];
    public double Mx, My, Radius, Von, Bis;
    public bool Gefuellt, Gestrichelt, Geschlossen, Rechteck, Kurve, AusFlaeche;
    public double Breite;
    public double[] Box = [0, 0, 0, 0];
}

/// <summary>Ein kleines gefülltes Vieleck (Pfeilspitze) mit Spitze.</summary>
readonly record struct PPfeil(double X, double Y, double Gr);

sealed class PBild
{
    public double L, B, R, T;
    public int PixelBreite, PixelHoehe;
}

sealed class PSeite
{
    public int Nr;
    public double Breite, Hoehe;
    public int Drehung;
    public List<PWort> Woerter = [];
    public List<PPfad> Pfade = [];
    public List<PStrecke> Strecken = [];
    /// <summary>Strecke aufnehmen (Strecken unter 1.5 pt sind Rauschen: Raster, Punkte).</summary>
    public void Strecke(PStrecke st) { if (st.Laenge >= 1.5 || st.AusFlaeche) Strecken.Add(st); }
    public List<PPfeil> Pfeile = [];
    public List<PBild> Bilder = [];
    /// <summary>Pfade über der Grenze je Seite, nicht gemeldet.</summary>
    public int PfadeAusgelassen;
    /// <summary>Gefüllte Kleinstformen mit vielen Kurven (Schrift als Umriss, Logos), nicht gemeldet.</summary>
    public int Umrisse;
    public int ZeichenDoppelt;
    /// <summary>Stellen mit übereinanderliegendem, verschiedenem Text (zwei Sprachen am selben Ort).</summary>
    public int UeberlagerteStellen;
    /// <summary>2 = der überlagerte Text ist in zwei Ebenen getrennt (Wörter tragen Ebene 1 oder 2).</summary>
    public int Ebenen;
    /// <summary>Überlagerung erkannt, aber nicht in zwei Ebenen trennbar.</summary>
    public bool UeberlagerungOffen;
    /// <summary>Pfade kleiner als 1 pt (Punkte, Raster), nicht gemeldet.</summary>
    public int Kleinst;
}

static class PdfRoh
{
    /// <summary>Obergrenze der gemeldeten Vektorpfade je Seite (Befund-Grösse und Lesezeit).</summary>
    public const int MaxPfade = 4000;

    public static PSeite Seite(Page page)
    {
        double ox = page.CropBox.Bounds.Left, oy = page.CropBox.Bounds.Bottom;
        var s = new PSeite
        {
            Nr = page.Number, Breite = page.CropBox.Bounds.Width, Hoehe = page.CropBox.Bounds.Height, Drehung = (int)page.Rotation.Value,
        };
        if (s.Breite <= 0 || s.Hoehe <= 0) { s.Breite = page.Width; s.Hoehe = page.Height; ox = 0; oy = 0; }
        Woerter(page, s, ox, oy);
        Pfade(page, s, ox, oy);
        Bilder(page, s, ox, oy);
        return s;
    }

    // ---------- Wörter ----------

    static void Woerter(Page page, PSeite s, double ox, double oy)
    {
        var alle = page.Letters.ToList();   // Leerzeichen bleiben: der Wortbildner trennt an ihnen
        var ebenen = Ueberlagerung(alle, out int stellen, out bool zweifarbig);
        s.UeberlagerteStellen = stellen;
        int doppelt = 0;
        if (ebenen is null)
        {
            s.Woerter = WoerterVon(alle, ox, oy, _ => 0, ref doppelt);
            s.UeberlagerungOffen = stellen > 0;   // Konflikte, aber nicht in zwei Ebenen trennbar
        }
        else
        {
            s.Ebenen = 2;
            int Eb(Letter l) => ebenen.TryGetValue(l.TextSequence, out var e) ? e : 0;
            for (int e = 1; e <= 2; e++)
            {
                var satz = alle.Where(l => Eb(l) is 0 || Eb(l) == e).ToList();
                var w = WoerterVon(satz, ox, oy, Eb, ref doppelt);
                s.Woerter.AddRange(e == 1 ? w : w.Where(x => x.Ebene != 0));
            }
        }
        s.ZeichenDoppelt = doppelt;
        // feste Reihenfolge: von oben nach unten, dann links nach rechts (gleiche Dokumente → gleiche Befund-Ids)
        s.Woerter = s.Woerter.OrderByDescending(w => w.Dreh == 0 ? Math.Round(w.Grund * 2) / 2 : double.NegativeInfinity).ThenBy(w => w.L).ThenBy(w => w.Ebene).ThenBy(w => w.Text, StringComparer.Ordinal).ToList();
    }

    static List<PWort> WoerterVon(List<Letter> buchstaben, double ox, double oy, Func<Letter, int> ebene, ref int doppelt)
    {
        var ohne = OhneDoppelte(buchstaben, out int d);
        doppelt += d;
        var waagrecht = ohne.Where(l => l.TextOrientation == TextOrientation.Horizontal).ToList();
        var rest = ohne.Where(l => l.TextOrientation != TextOrientation.Horizontal).ToList();
        var liste = new List<PWort>();
        foreach (var w in DefaultWordExtractor.Instance.GetWords(waagrecht))
        {
            var text = w.Text.Trim();
            if (text.Length == 0 || w.Letters.Count == 0) continue;
            var l0 = w.Letters[0];
            liste.Add(new PWort
            {
                Text = text, L = w.BoundingBox.Left - ox, B = w.BoundingBox.Bottom - oy, R = w.BoundingBox.Right - ox, T = w.BoundingBox.Top - oy,
                Grund = l0.StartBaseLine.Y - oy, Groesse = Groesse(l0), Dreh = 0, Ebene = w.Letters.Select(ebene).FirstOrDefault(e => e != 0),
            });
        }
        liste.AddRange(DrehteWoerter(rest, ox, oy, ebene));
        return liste;
    }

    /// <summary>Übereinanderliegender Text (zwei Sprachen am selben Ort, eine davon im Druck verdeckt): PdfPig liefert beide;
    /// beim Wortbilden mischen sich ihre Buchstaben («CGaeshiänugs»). Erkennung: Zeichen verschiedener Textobjekte, die sich
    /// zu mindestens 60 % decken und verschieden sind. Die Textobjekte (TextSequence) werden in zwei Ebenen eingeteilt
    /// (2-Färbung des Konfliktgraphen); Textobjekte ohne Konflikt gehören zu beiden. Null = keine Überlagerung oder nicht trennbar.</summary>
    static Dictionary<int, int>? Ueberlagerung(List<Letter> buchstaben, out int stellen, out bool zweifarbig)
    {
        stellen = 0; zweifarbig = true;
        var raster = new Dictionary<(long, long), List<Letter>>();
        var kanten = new Dictionary<int, HashSet<int>>();
        foreach (var l in buchstaben)
        {
            if (string.IsNullOrWhiteSpace(l.Value)) continue;
            var r = l.GlyphRectangle;
            double x0 = Math.Min(r.Left, r.Right), x1 = Math.Max(r.Left, r.Right), y0 = Math.Min(r.Bottom, r.Top), y1 = Math.Max(r.Bottom, r.Top);
            double fl = (x1 - x0) * (y1 - y0);
            if (fl <= 0) continue;
            long kx = (long)Math.Floor((x0 + x1) / 2 / 4), ky = (long)Math.Floor((y0 + y1) / 2 / 4);
            for (long dx = -1; dx <= 1; dx++)
                for (long dy = -1; dy <= 1; dy++)
                    if (raster.TryGetValue((kx + dx, ky + dy), out var nah))
                        foreach (var n in nah)
                        {
                            if (n.TextSequence == l.TextSequence || n.Value == l.Value) continue;
                            var q = n.GlyphRectangle;
                            double qx0 = Math.Min(q.Left, q.Right), qx1 = Math.Max(q.Left, q.Right), qy0 = Math.Min(q.Bottom, q.Top), qy1 = Math.Max(q.Bottom, q.Top);
                            double iw = Math.Min(x1, qx1) - Math.Max(x0, qx0), ih = Math.Min(y1, qy1) - Math.Max(y0, qy0);
                            if (iw <= 0 || ih <= 0) continue;
                            double qf = (qx1 - qx0) * (qy1 - qy0);
                            if (iw * ih < 0.6 * Math.Min(fl, qf)) continue;
                            stellen++;
                            if (!kanten.TryGetValue(l.TextSequence, out var a)) kanten[l.TextSequence] = a = [];
                            if (!kanten.TryGetValue(n.TextSequence, out var b)) kanten[n.TextSequence] = b = [];
                            a.Add(n.TextSequence); b.Add(l.TextSequence);
                        }
            if (!raster.TryGetValue((kx, ky), out var z)) raster[(kx, ky)] = z = [];
            z.Add(l);
        }
        if (kanten.Count == 0 || stellen < 3) { return null; }
        var farbe = new Dictionary<int, int>();
        foreach (var start in kanten.Keys.OrderBy(k => k))
        {
            if (farbe.ContainsKey(start)) continue;
            farbe[start] = 1;
            var warte = new Queue<int>([start]);
            while (warte.Count > 0)
            {
                int v = warte.Dequeue();
                foreach (var w in kanten[v])
                {
                    if (!farbe.TryGetValue(w, out var c)) { farbe[w] = 3 - farbe[v]; warte.Enqueue(w); }
                    else if (c == farbe[v]) { zweifarbig = false; return null; }
                }
            }
        }
        return farbe;
    }

    static double Groesse(Letter l) => l.PointSize > 0 ? l.PointSize : l.FontSize > 0 ? l.FontSize : Math.Max(l.GlyphRectangle.Height, 1);

    /// <summary>Wird ein Zeichen zweimal fast an derselben Stelle gedruckt (Pseudo-Fettschrift, Schatten), entsteht beim
    /// Wortbilden Buchstabensalat («MMoottoorr»): das zweite Zeichen wird verworfen.</summary>
    static List<Letter> OhneDoppelte(List<Letter> liste, out int doppelt)
    {
        doppelt = 0;
        var raster = new Dictionary<(string, long, long), List<Letter>>();
        var aus = new List<Letter>(liste.Count);
        foreach (var l in liste)
        {
            if (string.IsNullOrWhiteSpace(l.Value)) { aus.Add(l); continue; }
            double tol = Math.Max(0.3, 0.2 * Groesse(l));
            long kx = (long)Math.Floor(l.StartBaseLine.X / 2), ky = (long)Math.Floor(l.StartBaseLine.Y / 2);
            bool dopp = false;
            for (long dx = -1; dx <= 1 && !dopp; dx++)
                for (long dy = -1; dy <= 1 && !dopp; dy++)
                    if (raster.TryGetValue((l.Value, kx + dx, ky + dy), out var nah))
                        foreach (var n in nah)
                            if (Math.Abs(n.StartBaseLine.X - l.StartBaseLine.X) <= tol && Math.Abs(n.StartBaseLine.Y - l.StartBaseLine.Y) <= tol) { dopp = true; break; }
            if (dopp) { doppelt++; continue; }
            if (!raster.TryGetValue((l.Value, kx, ky), out var z)) raster[(l.Value, kx, ky)] = z = [];
            z.Add(l);
            aus.Add(l);
        }
        return aus;
    }

    /// <summary>Wörter aus gedrehtem Text (90°, 270°, 180°): PdfPig setzt sie nicht zusammen. Buchstaben entlang der
    /// Schreibrichtung ordnen und bei einer Lücke über 0.3 Schriftgrössen trennen.</summary>
    static List<PWort> DrehteWoerter(List<Letter> buchstaben, double ox, double oy, Func<Letter, int> ebene)
    {
        var ergebnis = new List<PWort>();
        foreach (var gruppe in buchstaben.Where(l => !string.IsNullOrWhiteSpace(l.Value)).GroupBy(l => l.TextOrientation))
        {
            // Schreibrichtung d aus den Buchstaben selbst (PdfPig-Benennung 90/270 ist nicht verlässlich), Normale n
            double sx = gruppe.Sum(l => l.EndBaseLine.X - l.StartBaseLine.X), sy = gruppe.Sum(l => l.EndBaseLine.Y - l.StartBaseLine.Y);
            double sl = Math.Sqrt(sx * sx + sy * sy);
            if (sl < 1e-6) continue;
            double dx = sx / sl, dy = sy / sl;
            int dreh;
            if (Math.Abs(dx) < 0.1 && dy > 0.9) dreh = 90;          // von unten nach oben gelesen
            else if (Math.Abs(dx) < 0.1 && dy < -0.9) dreh = 270;   // von oben nach unten gelesen
            else if (dx < -0.9 && Math.Abs(dy) < 0.1) dreh = 180;   // kopfstehend
            else continue;                                           // schräg: nicht als Wort gebildet
            double nx = -dy, ny = dx;
            var mitLage = gruppe.Select(l => (L: l, Along: l.StartBaseLine.X * dx + l.StartBaseLine.Y * dy, Quer: l.StartBaseLine.X * nx + l.StartBaseLine.Y * ny)).ToList();
            var zeilen = new List<List<(Letter L, double Along, double Quer)>>();
            foreach (var m in mitLage.OrderBy(m => m.Quer))
            {
                var z = zeilen.LastOrDefault();
                if (z is not null && Math.Abs(m.Quer - z[^1].Quer) <= 0.3 * Groesse(m.L)) z.Add(m); else zeilen.Add([m]);
            }
            foreach (var z in zeilen)
            {
                var geordnet = z.OrderBy(m => m.Along).ToList();
                var wort = new List<(Letter L, double Along, double Quer)>();
                double ende = double.NegativeInfinity;
                void Abschluss()
                {
                    if (wort.Count == 0) return;
                    double l = wort.Min(m => Math.Min(m.L.GlyphRectangle.Left, m.L.GlyphRectangle.Right)), r = wort.Max(m => Math.Max(m.L.GlyphRectangle.Left, m.L.GlyphRectangle.Right));
                    double b = wort.Min(m => Math.Min(m.L.GlyphRectangle.Bottom, m.L.GlyphRectangle.Top)), t = wort.Max(m => Math.Max(m.L.GlyphRectangle.Bottom, m.L.GlyphRectangle.Top));
                    var l0 = wort[0].L;
                    ergebnis.Add(new PWort
                    {
                        Text = string.Concat(wort.Select(m => m.L.Value)), L = l - ox, B = b - oy, R = r - ox, T = t - oy,
                        Grund = dreh is 90 or 270 ? l0.StartBaseLine.X - ox : l0.StartBaseLine.Y - oy, Groesse = Groesse(l0), Dreh = dreh, Ebene = wort.Select(m => ebene(m.L)).FirstOrDefault(e => e != 0),
                    });
                    wort.Clear();
                }
                foreach (var m in geordnet)
                {
                    double breite = Math.Sqrt(Math.Pow(m.L.EndBaseLine.X - m.L.StartBaseLine.X, 2) + Math.Pow(m.L.EndBaseLine.Y - m.L.StartBaseLine.Y, 2)); if (breite <= 0) breite = 0.5 * Groesse(m.L);
                    if (wort.Count > 0 && m.Along - ende > 0.3 * Groesse(m.L)) Abschluss();
                    wort.Add(m);
                    ende = Math.Max(ende, m.Along + breite);
                }
                Abschluss();
            }
        }
        return ergebnis;
    }


    // ---------- Vektorpfade ----------

    static void Pfade(Page page, PSeite s, double ox, double oy)
    {
        IReadOnlyList<PdfPath> pfade;
        try { pfade = page.Paths; }
        catch (Exception) { pfade = []; }
        foreach (var pfad in pfade)
        {
            if (pfad.IsClipping || (!pfad.IsStroked && !pfad.IsFilled)) continue;
            double breite = pfad.IsStroked ? pfad.LineWidth : 0;
            bool gestrichelt = pfad.LineDashPattern is { } muster && muster.Array.Count() > 0;
            foreach (var sub in pfad)
            {
                try { Teilpfad(s, sub, pfad.IsFilled, pfad.IsStroked, breite, gestrichelt, ox, oy); }
                catch (Exception ex) when (ex is not OutOfMemoryException) { }
            }
        }
    }

    static double[] Pt(PdfPoint p, double ox, double oy) => [p.X - ox, p.Y - oy];

    static void Teilpfad(PSeite s, PdfSubpath sub, bool gefuellt, bool gestrichen, double breite, bool gestrichelt, double ox, double oy)
    {
        // Befehle in Punktzüge (Linien) und Kurven zerlegen
        var lauf = new List<double[]>();
        var kurven = 0; var strecken = 0;
        bool geschlossen = false;
        var segmente = new List<(List<double[]>? Linien, double[][]? Bezier)>();
        double[]? aktuell = null;
        foreach (var c in sub.Commands)
        {
            switch (c)
            {
                case PdfSubpath.Move m:
                    if (lauf.Count > 1) segmente.Add((lauf, null));
                    lauf = [Pt(m.Location, ox, oy)];
                    aktuell = lauf[0];
                    break;
                case PdfSubpath.Line l:
                {
                    var a = Pt(l.From, ox, oy); var b = Pt(l.To, ox, oy);
                    if (lauf.Count == 0) lauf.Add(a);
                    lauf.Add(b); aktuell = b; strecken++;
                    break;
                }
                case PdfSubpath.BezierCurve bz:
                {
                    var a = Pt(bz.StartPoint, ox, oy); var ez = Pt(bz.EndPoint, ox, oy);
                    double[] c1, c2;
                    if (bz is PdfSubpath.CubicBezierCurve cu) { c1 = Pt(cu.FirstControlPoint, ox, oy); c2 = Pt(cu.SecondControlPoint, ox, oy); }
                    else if (bz is PdfSubpath.QuadraticBezierCurve qu)
                    {   // quadratisch → kubisch
                        var q = Pt(qu.ControlPoint, ox, oy);
                        c1 = [a[0] + 2.0 / 3 * (q[0] - a[0]), a[1] + 2.0 / 3 * (q[1] - a[1])];
                        c2 = [ez[0] + 2.0 / 3 * (q[0] - ez[0]), ez[1] + 2.0 / 3 * (q[1] - ez[1])];
                    }
                    else break;
                    if (lauf.Count > 1) segmente.Add((lauf, null));
                    segmente.Add((null, [a, c1, c2, ez]));
                    lauf = [ez];
                    aktuell = ez; kurven++;
                    break;
                }
                case PdfSubpath.Close:
                    geschlossen = true;
                    break;
            }
        }
        if (lauf.Count > 1) segmente.Add((lauf, null));
        if (segmente.Count == 0) return;

        // Schrift als Umriss und Logos: gefüllt, viele Kurven, klein → zählen, nicht melden
        var bb = sub.GetBoundingRectangle();
        double bx = bb?.Width ?? 0, by = bb?.Height ?? 0;
        if (gefuellt && !gestrichen && kurven >= 6 && Math.Max(bx, by) <= 40) { s.Umrisse++; return; }

        // Rechteck (gezeichnet als Rechteck oder vier Strecken)
        var alle = segmente.Count == 1 && segmente[0].Linien is { } e ? e : null;
        if (alle is not null && kurven == 0)
        {
            var punkte = alle;
            if (geschlossen && punkte.Count > 1 && !Gleich(punkte[0], punkte[^1])) punkte = [.. punkte, punkte[0]];
            bool zu = punkte.Count > 2 && Gleich(punkte[0], punkte[^1]);
            if (zu && IstRechteck(punkte, out var l, out var b, out var r, out var t))
            {
                double w = r - l, h = t - b;
                if (gefuellt && !gestrichen && (w <= 1.5 || h <= 1.5) && Math.Max(w, h) >= 2)
                {
                    // dünnes gefülltes Rechteck = Linie
                    var st = w <= h ? new PStrecke((l + r) / 2, b, (l + r) / 2, t, Math.Min(w, h), true) : new PStrecke(l, (b + t) / 2, r, (b + t) / 2, Math.Min(w, h), true);
                    s.Strecke(st);
                    Melden(s, new PPfad { Art = "linie", Punkte = [[st.X0, st.Y0], [st.X1, st.Y1]], Breite = Math.Min(w, h), AusFlaeche = true, Gefuellt = true, Box = [l, b, r, t] });
                    return;
                }
                if (gestrichen) { s.Strecke(new(l, b, r, b, breite, false)); s.Strecke(new(r, b, r, t, breite, false)); s.Strecke(new(r, t, l, t, breite, false)); s.Strecke(new(l, t, l, b, breite, false)); }
                Melden(s, new PPfad { Art = "polylinie", Punkte = punkte, Geschlossen = true, Rechteck = true, Gefuellt = gefuellt, Breite = breite, Gestrichelt = gestrichelt, Box = [l, b, r, t] });
                return;
            }
            // kleines gefülltes Dreieck oder Viereck: Pfeilspitze
            if (zu && gefuellt && punkte.Count is 4 or 5 && Math.Max(bx, by) <= 9 && Math.Max(bx, by) >= 1.5)
            {
                // Spitze = der Punkt mit dem kleinsten Winkel
                var ecken = punkte.Take(punkte.Count - 1).ToList();
                double besterWinkel = double.MaxValue; double[] spitze = ecken[0];
                for (int i = 0; i < ecken.Count; i++)
                {
                    var a = ecken[(i + ecken.Count - 1) % ecken.Count]; var m = ecken[i]; var c = ecken[(i + 1) % ecken.Count];
                    double w1 = Math.Atan2(a[1] - m[1], a[0] - m[0]), w2 = Math.Atan2(c[1] - m[1], c[0] - m[0]);
                    double d = Math.Abs(w1 - w2); if (d > Math.PI) d = 2 * Math.PI - d;
                    if (d < besterWinkel) { besterWinkel = d; spitze = m; }
                }
                if (ecken.Count == 3 || besterWinkel < 1.0) s.Pfeile.Add(new PPfeil(spitze[0], spitze[1], Math.Max(bx, by)));
            }
        }

        // Linien- und Kurvenstücke
        foreach (var (linien, bez) in segmente)
        {
            if (linien is not null)
            {
                var punkte = linien;
                if (geschlossen && segmente.Count == 1 && punkte.Count > 2 && !Gleich(punkte[0], punkte[^1])) punkte = [.. punkte, punkte[0]];
                if (punkte.Count < 2) continue;
                bool zu = punkte.Count > 2 && Gleich(punkte[0], punkte[^1]);
                for (int i = 0; i + 1 < punkte.Count; i++)
                    s.Strecke(new PStrecke(punkte[i][0], punkte[i][1], punkte[i + 1][0], punkte[i + 1][1], breite, false));
                if (!gestrichen && !gefuellt) continue;
                if (punkte.Count == 2)
                {
                    if (Gleich(punkte[0], punkte[1])) continue;
                    Melden(s, new PPfad { Art = "linie", Punkte = punkte, Breite = breite, Gestrichelt = gestrichelt, Box = BoxVon(punkte) });
                }
                else
                    Melden(s, new PPfad { Art = "polylinie", Punkte = punkte, Geschlossen = zu, Gefuellt = gefuellt, Breite = breite, Gestrichelt = gestrichelt, Box = BoxVon(punkte) });
            }
        }
        // Kurven: Kreisbögen zusammenfassen, sonst Polylinie
        var boegen = new List<PPfad>();
        foreach (var (_, bez) in segmente)
        {
            if (bez is null) continue;
            var arc = Bogen(bez, breite, gestrichelt);
            if (arc is null)
            {
                var flach = Flachen(bez);
                foreach (var q in flach.Skip(1).Select((q, i) => (flach[i], q))) s.Strecke(new PStrecke(q.Item1[0], q.Item1[1], q.q[0], q.q[1], breite, false));
                if (gestrichen) Melden(s, new PPfad { Art = "polylinie", Punkte = flach, Kurve = true, Breite = breite, Gefuellt = gefuellt, Gestrichelt = gestrichelt, Box = BoxVon(flach) });
                continue;
            }
            var letzter = boegen.LastOrDefault();
            if (letzter is not null && Verschmelzen(letzter, arc)) { }
            else boegen.Add(arc);
        }
        foreach (var arc in boegen)
        {
            if (!gestrichen && !gefuellt) continue;
            double sweep = Norm(arc.Bis - arc.Von);
            if ((sweep < 1 || sweep > 359) && arc.Punkte.Count >= 4)
            {
                Melden(s, new PPfad { Art = "kreis", Mx = arc.Mx, My = arc.My, Radius = arc.Radius, Punkte = [[arc.Mx, arc.My]], Breite = breite, Gefuellt = gefuellt, Gestrichelt = gestrichelt, Box = [arc.Mx - arc.Radius, arc.My - arc.Radius, arc.Mx + arc.Radius, arc.My + arc.Radius] });
            }
            else
            {
                arc.Punkte = [[arc.Mx, arc.My]];
                arc.Box = BogenBox(arc);
                Melden(s, arc);
            }
        }
    }

    static bool Gleich(double[] a, double[] b) => Math.Abs(a[0] - b[0]) < 0.01 && Math.Abs(a[1] - b[1]) < 0.01;

    static double[] BoxVon(List<double[]> p) => [p.Min(q => q[0]), p.Min(q => q[1]), p.Max(q => q[0]), p.Max(q => q[1])];
    static double[] Vereinigt(double[] a, double[] b) => [Math.Min(a[0], b[0]), Math.Min(a[1], b[1]), Math.Max(a[2], b[2]), Math.Max(a[3], b[3])];

    static bool IstRechteck(List<double[]> p, out double l, out double b, out double r, out double t)
    {
        l = b = r = t = 0;
        if (p.Count != 5) return false;
        l = p.Min(q => q[0]); r = p.Max(q => q[0]); b = p.Min(q => q[1]); t = p.Max(q => q[1]);
        const double tol = 0.05;
        foreach (var q in p.Take(4)) if (!((Math.Abs(q[0] - l) < tol || Math.Abs(q[0] - r) < tol) && (Math.Abs(q[1] - b) < tol || Math.Abs(q[1] - t) < tol))) return false;
        for (int i = 0; i < 4; i++) if (!(Math.Abs(p[i][0] - p[i + 1][0]) < tol || Math.Abs(p[i][1] - p[i + 1][1]) < tol)) return false;
        return true;
    }

    static void Melden(PSeite s, PPfad p)
    {
        if (Math.Max(p.Box[2] - p.Box[0], p.Box[3] - p.Box[1]) < 1.0) { s.Kleinst++; return; }
        if (s.Pfade.Count >= MaxPfade) { s.PfadeAusgelassen++; return; }
        s.Pfade.Add(p);
    }

    // ---------- Kurven ----------

    static double Norm(double grad) { grad %= 360; return grad < 0 ? grad + 360 : grad; }

    static double[] Bez(double[][] c, double t)
    {
        double u = 1 - t;
        return [u * u * u * c[0][0] + 3 * u * u * t * c[1][0] + 3 * u * t * t * c[2][0] + t * t * t * c[3][0],
                u * u * u * c[0][1] + 3 * u * u * t * c[1][1] + 3 * u * t * t * c[2][1] + t * t * t * c[3][1]];
    }

    static List<double[]> Flachen(double[][] c)
    {
        var l = new List<double[]>();
        for (int i = 0; i <= 8; i++) l.Add(Bez(c, i / 8.0));
        return l;
    }

    /// <summary>Kreisbogen durch Anfang, Mitte und Ende der Kurve, wenn die Kurve einem Kreis folgt (sonst null).</summary>
    static PPfad? Bogen(double[][] c, double breite, bool gestrichelt)
    {
        var a = c[0]; var m = Bez(c, 0.5); var e = c[3];
        double d = 2 * (a[0] * (m[1] - e[1]) + m[0] * (e[1] - a[1]) + e[0] * (a[1] - m[1]));
        if (Math.Abs(d) < 1e-9) return null;
        double ux = ((a[0] * a[0] + a[1] * a[1]) * (m[1] - e[1]) + (m[0] * m[0] + m[1] * m[1]) * (e[1] - a[1]) + (e[0] * e[0] + e[1] * e[1]) * (a[1] - m[1])) / d;
        double uy = ((a[0] * a[0] + a[1] * a[1]) * (e[0] - m[0]) + (m[0] * m[0] + m[1] * m[1]) * (a[0] - e[0]) + (e[0] * e[0] + e[1] * e[1]) * (m[0] - a[0])) / d;
        double r = Math.Sqrt((a[0] - ux) * (a[0] - ux) + (a[1] - uy) * (a[1] - uy));
        if (r < 0.05 || r > 5000) return null;
        foreach (var t in new[] { 0.25, 0.75 })
        {
            var q = Bez(c, t);
            if (Math.Abs(Math.Sqrt((q[0] - ux) * (q[0] - ux) + (q[1] - uy) * (q[1] - uy)) - r) > 0.01 * r + 0.03) return null;
        }
        double wa = Math.Atan2(a[1] - uy, a[0] - ux) * 180 / Math.PI, wm = Math.Atan2(m[1] - uy, m[0] - ux) * 180 / Math.PI, we = Math.Atan2(e[1] - uy, e[0] - ux) * 180 / Math.PI;
        bool gegenuhr = Norm(wm - wa) < 180;
        if ((gegenuhr ? Norm(we - wa) : Norm(wa - we)) < 2) return null;   // fast gerade: als Linienzug melden
        var p = new PPfad { Art = "bogen", Mx = ux, My = uy, Radius = r, Von = gegenuhr ? Norm(wa) : Norm(we), Bis = gegenuhr ? Norm(we) : Norm(wa), Breite = breite, Gestrichelt = gestrichelt, Punkte = [a, e], Box = BoxVon([a, m, e]) };
        return p;
    }

    /// <summary>Hängt Bogen b an Bogen a, wenn beide auf demselben Kreis liegen und sich berühren (a wird verändert).</summary>
    static bool Verschmelzen(PPfad a, PPfad b)
    {
        double tol = 0.02 * a.Radius + 0.05;
        if (Math.Abs(a.Mx - b.Mx) >= tol || Math.Abs(a.My - b.My) >= tol || Math.Abs(a.Radius - b.Radius) >= tol) return false;
        bool Nah(double x, double y) { double d = Math.Abs(Norm(x - y)); return d < 0.5 || Math.Abs(d - 360) < 0.5; }
        if (Nah(a.Bis, b.Von)) a.Bis = b.Bis;
        else if (Nah(b.Bis, a.Von)) { a.Von = b.Von; }
        else return false;
        a.Box = Vereinigt(a.Box, b.Box);
        a.Punkte.AddRange(b.Punkte);
        return true;
    }

    static double[] BogenBox(PPfad p)
    {
        double sweep = Norm(p.Bis - p.Von);
        var pts = new List<double[]>();
        for (int i = 0; i <= 16; i++)
        {
            double w = (p.Von + sweep * i / 16) * Math.PI / 180;
            pts.Add([p.Mx + p.Radius * Math.Cos(w), p.My + p.Radius * Math.Sin(w)]);
        }
        return BoxVon(pts);
    }

    // ---------- Bilder ----------

    static void Bilder(Page page, PSeite s, double ox, double oy)
    {
        try
        {
            foreach (var b in page.GetImages())
                s.Bilder.Add(new PBild
                {
                    L = b.Bounds.Left - ox, B = b.Bounds.Bottom - oy, R = b.Bounds.Right - ox, T = b.Bounds.Top - oy,
                    PixelBreite = b.WidthInSamples, PixelHoehe = b.HeightInSamples,
                });
        }
        catch (Exception) { }
    }
}
