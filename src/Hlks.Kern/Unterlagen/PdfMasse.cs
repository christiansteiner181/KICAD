// PDF lesen (Modul 2, Auftrag B2): Mass-Kandidaten in Massbildern. Datenblätter zeichnen die Masse als Buchstabe (L, H, ØD)
// oder Zahl (70) an einer Masslinie. Gedeutet wird so (Annahme, Prüfliste): Eine Masslinie ist eine waagrechte oder
// senkrechte dünne Strecke mit Pfeilspitze oder Masshilfslinie (kurze Querstrecke) an mindestens einem Ende; ein
// Beschriftungswort (ein bis drei Zeichen, ggf. mit Ø, oder eine Zahl) nahe an ihr wird als ihr Mass gemeldet. Die
// Sicherheit liegt immer unter 1: Das Massbild kann schematisch sein (Länge der Linie in pt sagt nichts über das Mass), und
// ob der Buchstabe zu dieser Linie gehört, ist gedeutet. Wörter innerhalb erkannter Tabellen zählen nicht.

using System.Text.RegularExpressions;

namespace Hlks.Kern;

sealed record PMass(PWort Wort, PStrecke Linie, int Pfeile, int Hilfslinien, double Sicherheit, bool Zahl);

static class PdfMasse
{
    static readonly Regex Buchstabe = new(@"^[ØøΦ⌀]?[A-Za-z]{1,2}\d?[a-z]?$", RegexOptions.Compiled);
    static readonly Regex Zahl = new(@"^\d{1,4}([.,]\d{1,2})?$", RegexOptions.Compiled);

    public static List<PMass> Finden(PSeite s, List<PTabelle> tabellen)
    {
        var ergebnis = new List<PMass>();
        double max = Math.Max(s.Breite, s.Hoehe);
        var linien = s.Strecken.Where(l => (l.Waagrecht || l.Senkrecht) && l.Laenge >= 6 && l.Laenge <= 0.9 * max && l.Breite <= 1.2).ToList();
        if (linien.Count == 0) return ergebnis;
        // Hilfslinien nur aus Querstrecken, die nicht zu einem Raster gehören: grobe Obergrenze, sonst Rechenaufwand (n²)
        var waagrechte = s.Strecken.Where(l => l.Waagrecht && l.Laenge >= 2.5 && l.Laenge <= 90 && l.Breite <= 1.2).ToList();
        var senkrechte = s.Strecken.Where(l => l.Senkrecht && l.Laenge >= 2.5 && l.Laenge <= 90 && l.Breite <= 1.2).ToList();

        // Raster-Indizes (Kästchen von 8 pt), damit grosse Zeichnungen nicht quadratisch rechnen
        const double K = 8;
        static long Kn(double v) => (long)Math.Floor(v / K);
        var pfeilIndex = new Dictionary<(long, long), List<PPfeil>>();
        foreach (var p in s.Pfeile) { var k = (Kn(p.X), Kn(p.Y)); if (!pfeilIndex.TryGetValue(k, out var li)) pfeilIndex[k] = li = []; li.Add(p); }
        var senkIndex = new Dictionary<long, List<PStrecke>>();   // senkrechte Querstrecken nach x
        var waagIndex = new Dictionary<long, List<PStrecke>>();   // waagrechte Querstrecken nach y
        foreach (var q in senkrechte) { long k = Kn((q.X0 + q.X1) / 2); if (!senkIndex.TryGetValue(k, out var li)) senkIndex[k] = li = []; li.Add(q); }
        foreach (var q in waagrechte) { long k = Kn((q.Y0 + q.Y1) / 2); if (!waagIndex.TryGetValue(k, out var li)) waagIndex[k] = li = []; li.Add(q); }

        int Enden(PStrecke l, out int pfeile, out int hilfen)
        {
            pfeile = 0; hilfen = 0;
            foreach (var (x, y) in new[] { (l.X0, l.Y0), (l.X1, l.Y1) })
            {
                bool pfeil = false;
                for (long i = Kn(x) - 2; i <= Kn(x) + 2 && !pfeil; i++)
                    for (long j = Kn(y) - 2; j <= Kn(y) + 2 && !pfeil; j++)
                        if (pfeilIndex.TryGetValue((i, j), out var li))
                            pfeil = li.Any(p => l.Waagrecht ? Math.Abs(p.Y - y) <= 1.5 && Math.Abs(p.X - x) <= p.Gr + 2.5 : Math.Abs(p.X - x) <= 1.5 && Math.Abs(p.Y - y) <= p.Gr + 2.5);
                if (pfeil) { pfeile++; continue; }
                bool hilfe = false;
                if (l.Waagrecht)
                {
                    for (long i = Kn(x) - 1; i <= Kn(x) + 1 && !hilfe; i++)
                        if (senkIndex.TryGetValue(i, out var li))
                            hilfe = li.Any(q => Math.Abs((q.X0 + q.X1) / 2 - x) <= 1.0 && Math.Min(q.Y0, q.Y1) <= y + 0.5 && Math.Max(q.Y0, q.Y1) >= y - 0.5);
                }
                else
                {
                    for (long i = Kn(y) - 1; i <= Kn(y) + 1 && !hilfe; i++)
                        if (waagIndex.TryGetValue(i, out var li))
                            hilfe = li.Any(q => Math.Abs((q.Y0 + q.Y1) / 2 - y) <= 1.0 && Math.Min(q.X0, q.X1) <= x + 0.5 && Math.Max(q.X0, q.X1) >= x - 0.5);
                }
                if (hilfe) hilfen++;
            }
            return pfeile + hilfen;
        }

        var bewertet = new List<(PStrecke L, int Pf, int Hi)>();
        foreach (var l in linien)
        {
            if (Enden(l, out int pf, out int hi) == 0) continue;
            bewertet.Add((l, pf, hi));
        }
        if (bewertet.Count == 0) return ergebnis;

        foreach (var w in s.Woerter)
        {
            string t = w.Text.Trim();
            bool zahl = Zahl.IsMatch(t);
            if (!zahl && !Buchstabe.IsMatch(t)) continue;
            if (w.T < 0.05 * s.Hoehe || w.B > 0.95 * s.Hoehe) continue;   // Kopf- und Fusszeile, Seitenzahl
            if (tabellen.Any(tb => w.L >= tb.Box[0] - 1 && w.R <= tb.Box[2] + 1 && w.B >= tb.Box[1] - 1 && w.T <= tb.Box[3] + 1)) continue;
            double gr = Math.Max(w.Groesse, 4);
            PMass? best = null;
            foreach (var (l, pf, hi) in bewertet)
            {
                if (l.Laenge < Math.Max(6, gr)) continue;
                double dv, da;
                if (l.Waagrecht)
                {
                    double y = (l.Y0 + l.Y1) / 2, x0 = Math.Min(l.X0, l.X1), x1 = Math.Max(l.X0, l.X1);
                    dv = y >= w.B && y <= w.T ? 0 : Math.Min(Math.Abs(w.B - y), Math.Abs(w.T - y));
                    da = w.R < x0 ? x0 - w.R : w.L > x1 ? w.L - x1 : 0;
                }
                else
                {
                    double x = (l.X0 + l.X1) / 2, y0 = Math.Min(l.Y0, l.Y1), y1 = Math.Max(l.Y0, l.Y1);
                    dv = x >= w.L && x <= w.R ? 0 : Math.Min(Math.Abs(w.L - x), Math.Abs(w.R - x));
                    da = w.T < y0 ? y0 - w.T : w.B > y1 ? w.B - y1 : 0;
                }
                if (dv > 1.6 * gr || da > 1.2 * gr) continue;
                bool parallel = (l.Waagrecht && w.Dreh is 0 or 180) || (l.Senkrecht && w.Dreh is 90 or 270);
                double sicher = 0.3 + 0.12 * Math.Min(2, pf + hi) + (pf > 0 ? 0.05 : 0) + (da == 0 ? 0.1 : 0) + (dv <= gr ? 0.08 : 0) + (!zahl && t.Length <= 3 ? 0.07 : 0) - (parallel ? 0 : 0.05);
                sicher = Math.Min(0.85, sicher);
                if (best is null || sicher > best.Sicherheit || (Math.Abs(sicher - best.Sicherheit) < 1e-9 && l.Laenge > best.Linie.Laenge))
                    best = new PMass(w, l, pf, hi, sicher, zahl);
            }
            if (best is not null) ergebnis.Add(best);
        }
        return ergebnis;
    }
}
