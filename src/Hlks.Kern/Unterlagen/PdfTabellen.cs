// PDF lesen (Modul 2, Auftrag B2): Zeilen und Tabellen aus Wörtern und Linien einer Seite.
//
// Zeilen: Wörter auf derselben Grundlinie; grosse Lücken (Spaltenabstand) trennen Zellen und stehen in der Zeile als « | ».
// Tabellen (Annahme, Prüfliste): Eine Tabelle ist eine Folge von Zeilen mit mindestens zwei kurzen Zellen, deren
// Spalten sich über freie Streifen (Rinnen) zwischen den Zellen ergeben – gleichgültig, ob links, rechts oder mittig
// ausgerichtet. Dichte Spalten (in fast jeder Zeile besetzt) bestimmen die Breite der Tabelle; dünn besetzte Spalten
// zwischen ihnen (z. B. ein Wert, der für mehrere Zeilen gilt) gehören dazu. Kopfzeilen sind die Zeilen vor der ersten
// Zeile mit überwiegend Zahlen. Zusammengefasste Zellen werden gemeldet, wie sie stehen (in der obersten Zeile, die
// übrigen Zellen leer), mit Hinweis; sind waagrechte Linien vorhanden, die nur die Spalte der Zelle teilen, steht die
// Zeilenzahl der Zusammenfassung dabei.
// Grenzen: Text in Rasterbildern, Tabellen ohne erkennbare Spalten und Zellen mit mehr als sechs Wörtern (Fliesstext)
// werden nicht erkannt; Tabellen über mehrere Seiten sind je Seite eine Tabelle.

using System.Text;
using System.Text.RegularExpressions;

namespace Hlks.Kern;

sealed class PZelle
{
    public string Text = "";
    public double L, B, R, T, Grund, Groesse;
    public int Woerter;
    /// <summary>1 oder 2, wenn die Zelle aus überlagertem Text besteht (siehe PWort.Ebene), sonst 0.</summary>
    public int Ebene;
    /// <summary>Zusammengefasst über Zeilen: Anzahl Zeilen (0 = nicht zusammengefasst, -1 = vermutlich, Anzahl unbekannt).</summary>
    public int Spanne;
    /// <summary>Grundlinie der letzten Zeile, wenn die Zelle über mehrere Zeilen umbrochen ist (0 = einzeilig).</summary>
    public double Unten;
    /// <summary>Kopfzelle über mehreren Spalten: die Spalten (Index).</summary>
    public List<int>? Spalten;
}

sealed class PZeile
{
    public string Text = "";
    public double L, B, R, T, Grund, Groesse;
    public List<PWort> Woerter = [];
    public List<PZelle> Zellen = [];
    public int Ebene => Woerter.Select(w => w.Ebene).FirstOrDefault(e => e != 0);
}

sealed class PTZeile
{
    public double Grund;
    public PZelle?[] Zellen = [];
}

sealed class PTabelle
{
    public List<(double L, double R)> Spalten = [];
    public List<string> Kopf = [];
    public int KopfZeilen;
    public List<PTZeile> Zeilen = [];
    public double[] Box = [0, 0, 0, 0];
    public List<string> Hinweise = [];
    /// <summary>Waagrechte Linien in der Tabelle gefunden (Zeilentrenner)?</summary>
    public bool MitLinien;
    public int Ebene => Zeilen.SelectMany(z => z.Zellen).Select(c => c?.Ebene ?? 0).FirstOrDefault(e => e != 0);
}

static class PdfZeilen
{
    /// <summary>Wörter (nur waagrechte) zu Zeilen und Zellen. Reihenfolge: von oben nach unten.</summary>
    public static List<PZeile> Bilden(IEnumerable<PWort> woerter)
    {
        var zeilen = new List<PZeile>();
        foreach (var w in woerter.Where(w => w.Dreh == 0).OrderByDescending(w => w.Grund).ThenBy(w => w.L))
        {
            var z = zeilen.LastOrDefault();
            if (z is not null && Math.Abs(z.Woerter[0].Grund - w.Grund) <= Math.Max(0.8, 0.25 * Math.Min(z.Groesse, w.Groesse))) z.Woerter.Add(w);
            else zeilen.Add(new PZeile { Woerter = [w] });
        }
        foreach (var z in zeilen)
        {
            z.Woerter = z.Woerter.OrderBy(w => w.L).ToList();
            z.Grund = z.Woerter[0].Grund;
            z.Groesse = z.Woerter.Max(w => w.Groesse);
            z.L = z.Woerter.Min(w => w.L); z.R = z.Woerter.Max(w => w.R); z.B = z.Woerter.Min(w => w.B); z.T = z.Woerter.Max(w => w.T);
            var text = new StringBuilder();
            PZelle? zelle = null;
            PWort? vor = null;
            foreach (var w in z.Woerter)
            {
                double luecke = vor is null ? 0 : w.L - vor.R;
                double schwelle = Math.Max(6, Math.Max(vor?.Groesse ?? 0, w.Groesse));
                if (zelle is null || luecke > schwelle)
                {
                    if (zelle is not null) text.Append(" | ");
                    zelle = new PZelle { L = w.L, B = w.B, R = w.R, T = w.T, Grund = z.Grund, Groesse = w.Groesse, Text = w.Text, Woerter = 1, Ebene = w.Ebene };
                    z.Zellen.Add(zelle);
                }
                else
                {
                    zelle.Text += " " + w.Text; zelle.R = Math.Max(zelle.R, w.R); zelle.B = Math.Min(zelle.B, w.B); zelle.T = Math.Max(zelle.T, w.T);
                    zelle.Groesse = Math.Max(zelle.Groesse, w.Groesse); zelle.Woerter++; if (w.Ebene != 0) zelle.Ebene = w.Ebene;
                    text.Append(' ');
                }
                text.Append(w.Text);
                vor = w;
            }
            z.Text = text.ToString();
        }
        return zeilen;
    }
}

static class PdfTabellen
{
    sealed class Reihe
    {
        public double Grund;
        public double Groesse;
        public List<PZelle> Alle = [];
        public List<PZelle> Kurz = [];
        /// <summary>Mindestens zwei kurze Zellen, oder «kurze Beschriftung | langer Wert» (die kurze Zelle steht links).</summary>
        public bool Mehrere => Kurz.Count >= 2 || (Alle.Count >= 2 && Kurz.Count >= 1 && Kurz.Contains(Alle[0]));
    }

    static readonly Regex EinheitMuster = new(@"^[\[\(].{0,16}[\]\)]$", RegexOptions.Compiled);

    static bool Zahlig(string t)
    {
        if (Bau.Zahl(t) is not null) return true;
        int ziffern = t.Count(char.IsDigit);
        return ziffern > 0 && ziffern >= 0.5 * t.Count(c => !char.IsWhiteSpace(c));
    }

    /// <summary>Kurze Zelle: höchstens sechs Wörter und höchstens 45 % der Seitenbreite. Längere Zellen (Fliesstext, lange
    /// Werte) bestimmen keine Spalten; sie werden später nur einer Spalte zugeschlagen, wenn sie an deren linkem Rand beginnen.</summary>
    static bool Kurz(PZelle c, double maxBreite) => c.Woerter <= 6 && c.R - c.L <= maxBreite;

    public static List<PTabelle> Erkennen(PSeite seite, List<PZeile> zeilen)
    {
        var ergebnis = new List<PTabelle>();
        double maxBreite = 0.45 * seite.Breite;
        var reihen = zeilen
            .Select(z => new Reihe { Grund = z.Grund, Groesse = z.Groesse, Alle = z.Zellen, Kurz = z.Zellen.Where(c => Kurz(c, maxBreite)).ToList() })
            .OrderByDescending(r => r.Grund).ToList();
        var mehrere = reihen.Where(r => r.Mehrere).ToList();
        if (mehrere.Count < 3) return ergebnis;

        // Blöcke aufeinanderfolgender Zeilen mit mehreren kurzen Zellen
        var bloecke = new List<List<Reihe>>();
        foreach (var r in mehrere)
        {
            var b = bloecke.LastOrDefault();
            if (b is not null)
            {
                var abstaende = b.Zip(b.Skip(1), (x, y) => x.Grund - y.Grund).OrderBy(x => x).ToList();
                double median = abstaende.Count > 0 ? abstaende[abstaende.Count / 2] : 0;
                double grenze = Math.Max(26, 2.4 * median);
                if (b[^1].Grund - r.Grund <= grenze || Fortsetzung(b[^1], r, reihen)) { b.Add(r); continue; }
            }
            bloecke.Add([r]);
        }

        var benutzt = new HashSet<Reihe>();
        foreach (var block in bloecke.Where(b => b.Count >= 3))
        {
            var t = Block(seite, block, reihen, benutzt, maxBreite);
            if (t is not null) ergebnis.Add(t);
        }
        return ergebnis;
    }

    /// <summary>Verbinden nur umbrochene Zellzeilen zwei Tabellenzeilen? Zwischen beiden liegen lückenlos Zeilen (Abstand
    /// höchstens 1.65 Schriftgrössen), die an einer Spalte der oberen Zeile beginnen.</summary>
    static bool Fortsetzung(Reihe oben, Reihe unten, List<Reihe> alle)
    {
        var dazwischen = alle.Where(r => r.Grund < oben.Grund - 0.1 && r.Grund > unten.Grund + 0.1).OrderByDescending(r => r.Grund).ToList();
        if (dazwischen.Count == 0) return false;
        var raender = oben.Alle.Select(c => c.L).ToList();
        double vor = oben.Grund;
        foreach (var r in dazwischen)
        {
            if (vor - r.Grund > 1.65 * Math.Max(r.Groesse, oben.Groesse)) return false;
            if (!r.Alle.Any(c => raender.Any(x => Math.Abs(x - c.L) <= 2.5))) return false;
            vor = r.Grund;
        }
        return vor - unten.Grund <= 1.65 * Math.Max(unten.Groesse, oben.Groesse);
    }

    static PTabelle? Block(PSeite seite, List<Reihe> block, List<Reihe> alle, HashSet<Reihe> benutzt, double maxBreite)
    {
        double oben = block[0].Grund, unten = block[^1].Grund;
        var imBereich = alle.Where(r => r.Grund <= oben + 0.1 && r.Grund >= unten - 0.1 && !benutzt.Contains(r)).ToList();
        var zellen = imBereich.SelectMany(r => r.Kurz).ToList();
        if (zellen.Count == 0) return null;

        // Belegung der x-Achse in 0.5-pt-Schritten
        int n = (int)Math.Ceiling(seite.Breite * 2) + 4;
        var belegt = new int[n];
        foreach (var c in zellen)
            for (int i = Math.Max(0, (int)Math.Floor(c.L * 2)); i < Math.Min(n, (int)Math.Ceiling(c.R * 2)); i++) belegt[i]++;
        var laeufe = new List<(int A, int E)>();
        for (int i = 0; i < n; i++)
        {
            if (belegt[i] < 2) continue;
            int j = i;
            while (j + 1 < n && belegt[j + 1] >= 2) j++;
            if (laeufe.Count > 0 && i - laeufe[^1].E <= 4) laeufe[^1] = (laeufe[^1].A, j); else laeufe.Add((i, j));
            i = j;
        }
        // Zellen den Läufen zuordnen
        var spalten = new List<(double L, double R, List<PZelle> Zellen)>();
        foreach (var (a, e) in laeufe)
        {
            double sl = a / 2.0, sr = (e + 1) / 2.0;
            var mitglieder = zellen.Where(c => Math.Min(c.R, sr) - Math.Max(c.L, sl) >= 0.5 * (c.R - c.L)).ToList();
            if (mitglieder.Count < 2) continue;
            spalten.Add((mitglieder.Min(c => c.L), mitglieder.Max(c => c.R), mitglieder));
        }
        // überlappende Läufe vereinigen (Mitglieder können in zwei Läufen liegen)
        spalten = spalten.OrderBy(s => s.L).ToList();
        for (int i = 0; i + 1 < spalten.Count;)
        {
            if (spalten[i + 1].L < spalten[i].R - 0.5) { spalten[i] = (spalten[i].L, Math.Max(spalten[i].R, spalten[i + 1].R), spalten[i].Zellen.Union(spalten[i + 1].Zellen).ToList()); spalten.RemoveAt(i + 1); }
            else i++;
        }
        // Lange Zellen (Werte mit vielen Wörtern) zählen für die Spalte mit, an deren linkem Rand sie beginnen – aber nur für
        // Spalten rechts einer dichten Spalte aus kurzen Zellen (Schlüssel | Wert). Sonst würde der Fliesstext am linken
        // Seitenrand zur «Spalte».
        int mehr = block.Count;
        var kern = spalten.Where(s => s.Zellen.Count >= 0.5 * mehr).ToList();
        var lange = imBereich.SelectMany(r => r.Alle).Where(c => !Kurz(c, maxBreite)).ToList();
        for (int i = 0; i < spalten.Count; i++)
        {
            if (!kern.Any(d => d.R < spalten[i].L)) continue;
            var ausgerichtet = lange.Where(c => Math.Abs(c.L - spalten[i].L) <= 2.5).ToList();
            if (ausgerichtet.Count > 0) spalten[i] = (spalten[i].L, spalten[i].R, spalten[i].Zellen.Concat(ausgerichtet).ToList());
        }
        var dicht = spalten.Where(s => s.Zellen.Count >= 0.5 * mehr).ToList();
        if (dicht.Count < 2) return null;
        double xmin = dicht.Min(s => s.L), xmax = dicht.Max(s => s.R);
        var spaltenListe = spalten
            .Where(s => dicht.Contains(s) || (s.L >= xmin - 0.5 && s.R <= xmax + 0.5 && s.Zellen.Select(c => c.Grund).Distinct().Count() >= 3))
            .OrderBy(s => s.L).ToList();
        if (spaltenListe.Count < 2) return null;

        var t = new PTabelle { Spalten = spaltenListe.Select(s => (s.L, s.R)).ToList() };
        int sp = spaltenListe.Count;

        // Zeilen der Tabelle: alle Reihen im Bereich plus Kopfzeilen darüber (nicht weiter weg als 1.7 Zeilenabstände:
        // Überschriften über der Tabelle gehören nicht zum Kopf)
        double median = Median(block.Zip(block.Skip(1), (x, y) => x.Grund - y.Grund).ToList(), 12);
        var kandidaten = new List<Reihe>(imBereich);
        var darueber = alle.Where(r => r.Grund > oben + 0.1 && !benutzt.Contains(r)).OrderBy(r => r.Grund).ToList();
        double vor = oben;
        foreach (var r in darueber)
        {
            if (r.Grund - vor > Math.Max(1.7 * median, 16)) break;
            if (Zuordnen(r.Alle, t.Spalten, maxBreite).Count == 0) break;
            kandidaten.Add(r); vor = r.Grund;
        }
        kandidaten = kandidaten.OrderByDescending(r => r.Grund).ToList();

        var reihenZellen = new List<(Reihe R, PZelle?[] Z, List<(PZelle C, List<int> Sp)> Ueber)>();
        foreach (var r in kandidaten)
        {
            var z = new PZelle?[sp];
            var ueber = new List<(PZelle, List<int>)>();
            foreach (var (c, cols) in Zuordnen(r.Alle, t.Spalten, maxBreite))
            {
                if (cols.Count > 1) { ueber.Add((c, cols)); continue; }
                int k = cols[0];
                if (z[k] is null) z[k] = c;
                else
                {   // zwei Zellen in derselben Spalte der Zeile: zusammenziehen (z. B. Zahl und Einheit nebeneinander)
                    var a = z[k]!;
                    z[k] = new PZelle { Text = a.Text + " " + c.Text, L = Math.Min(a.L, c.L), B = Math.Min(a.B, c.B), R = Math.Max(a.R, c.R), T = Math.Max(a.T, c.T), Grund = a.Grund, Groesse = a.Groesse, Woerter = a.Woerter + c.Woerter };
                }
            }
            if (z.Any(x => x is not null) || ueber.Count > 0) reihenZellen.Add((r, z, ueber));
        }
        if (reihenZellen.Count < 3) return null;
        foreach (var r in reihenZellen) benutzt.Add(r.R);

        // Kopfzeilen: Zeilen vor der ersten überwiegend zahligen Zeile (mit mindestens zwei Zellen)
        int erste = -1;
        for (int i = 0; i < reihenZellen.Count; i++)
        {
            var zs = reihenZellen[i].Z.Where(x => x is not null).Select(x => x!).Where(x => !EinheitMuster.IsMatch(x.Text)).ToList();
            if (zs.Count >= 2 && zs.Count(x => Zahlig(x.Text)) >= 0.5 * zs.Count) { erste = i; break; }
        }
        int kopfZeilen = erste > 0 && erste <= 4 ? erste : 0;
        for (int c = 0; c < sp; c++)
        {
            var teile = new List<string>();
            for (int i = 0; i < kopfZeilen; i++)
            {
                if (reihenZellen[i].Z[c] is { } z) teile.Add(z.Text);
                foreach (var (cz, cols) in reihenZellen[i].Ueber) if (cols.Contains(c)) teile.Add(cz.Text);
            }
            t.Kopf.Add(string.Join(" ", teile));
        }
        t.KopfZeilen = kopfZeilen;
        if (kopfZeilen == 0) t.Hinweise.Add("Keine Kopfzeile erkannt.");

        // Datenzeilen. Zeilen mit nur einer Zelle: entweder die Fortsetzung einer umbrochenen Zelle (kurz unter der Zelle
        // darüber in derselben Spalte) oder – in einer Folgespalte zwischen zwei Zeilen – eine zusammengefasste Zelle.
        var daten = reihenZellen.Skip(kopfZeilen).ToList();
        static int Gefuellt(PZelle?[] z) => z.Count(x => x is not null);
        var voll = daten.Where(d => Gefuellt(d.Z) >= 3 || (sp <= 3 && Gefuellt(d.Z) >= 2)).ToList();
        var allein = new List<PZelle>();
        foreach (var d in daten)
        {
            if (Gefuellt(d.Z) == 1 && d.Ueber.Count == 0 && !voll.Contains(d))
            {
                int k = Array.FindIndex(d.Z, x => x is not null);
                var c = d.Z[k]!;
                var vorher = t.Zeilen.AsEnumerable().Reverse().Take(3).FirstOrDefault(z => z.Zellen[k] is { } v && Unten(v) - c.Grund > 0.6 * c.Groesse && Unten(v) - c.Grund <= 1.65 * Math.Max(c.Groesse, v.Groesse));
                if (vorher is not null)
                {
                    var v = vorher.Zellen[k]!;
                    v.Text += " " + c.Text; v.R = Math.Max(v.R, c.R); v.L = Math.Min(v.L, c.L); v.B = Math.Min(v.B, c.B); v.Woerter += c.Woerter; v.Unten = c.Grund;
                    continue;
                }
                if (k > 0 && d.Z[0] is null && voll.Count >= 3)
                {
                    // fast auf der Grundlinie einer vollen Zeile (Zelle in der Mitte einer Zusammenfassung): in diese Zeile
                    var gleich = voll.Where(v => Math.Abs(v.R.Grund - d.R.Grund) <= 2.5 && v.Z[k] is null).Select(v => v.Z).FirstOrDefault();
                    if (gleich is not null) { gleich[k] = c; continue; }
                    if (voll.All(v => Math.Abs(v.R.Grund - d.R.Grund) > 2.5)) { allein.Add(c); continue; }
                }
            }
            t.Zeilen.Add(new PTZeile { Grund = d.R.Grund, Zellen = d.Z });
        }
        if (t.Zeilen.Count < 2) return null;

        // Zusammenfassungen: zuerst die Einzelzellen zwischen den Zeilen – ohne Linien bleibt die Spanne unbekannt
        var linien = Waagrechte(seite, t);
        t.MitLinien = linien.Zeilentrenner;
        foreach (var c in allein)
        {
            int k = Array.FindIndex(t.Spalten.ToArray(), s => Math.Min(c.R, s.R + 1) - Math.Max(c.L, s.L - 1) >= 0.2 * Math.Max(c.R - c.L, 0.1));
            int nah = -1; double best = double.MaxValue;
            for (int i = 0; i < t.Zeilen.Count; i++)
            {
                if (t.Zeilen[i].Zellen[k] is not null) continue;
                double dist = Math.Abs(t.Zeilen[i].Grund - c.Grund) - (t.Zeilen[i].Grund > c.Grund ? 0.01 : 0);   // bei Gleichstand die obere
                if (dist < best) { best = dist; nah = i; }
            }
            if (nah >= 0 && best < 2.5 * median) { t.Zeilen[nah].Zellen[k] = c; c.Spanne = -1; }
            else t.Hinweise.Add($"Die Zelle «{c.Text}» steht zwischen zwei Zeilen und liess sich keiner Zeile zuordnen.");
        }
        if (t.MitLinien)
            for (int k = 0; k < sp; k++)
            {
                var trenner = linien.Je(t.Spalten[k]);
                if (trenner.Count < 2) continue;
                for (int i = 0; i < t.Zeilen.Count; i++)
                {
                    var c = t.Zeilen[i].Zellen[k];
                    if (c is null) continue;
                    double cy = c.Spanne == -1 ? c.Grund : t.Zeilen[i].Grund;
                    double ueber = trenner.Where(y => y > cy).DefaultIfEmpty(double.NaN).Min(), unter = trenner.Where(y => y < cy).DefaultIfEmpty(double.NaN).Max();
                    if (double.IsNaN(ueber) || double.IsNaN(unter)) continue;
                    var innen = Enumerable.Range(0, t.Zeilen.Count).Where(j => t.Zeilen[j].Grund < ueber && t.Zeilen[j].Grund > unter).ToList();
                    if (innen.Count == 0) continue;
                    if (innen.Count >= 2) c.Spanne = innen.Count;
                    else if (c.Spanne == -1) c.Spanne = 0;
                    // die Zelle gehört in die oberste Zeile ihrer Zusammenfassung
                    if (innen[0] != i && t.Zeilen[innen[0]].Zellen[k] is null) { t.Zeilen[innen[0]].Zellen[k] = c; t.Zeilen[i].Zellen[k] = null; }
                }
            }
        t.Zeilen.RemoveAll(z => z.Zellen.All(c => c is null));
        var zus = t.Zeilen.SelectMany((z, i) => z.Zellen.Select((c, k) => (c, k, i))).Where(x => x.c is { Spanne: not 0 }).ToList();
        foreach (var gruppe in zus.GroupBy(x => x.k))
        {
            int unbekannt = gruppe.Count(x => x.c!.Spanne < 0);
            t.Hinweise.Add($"Spalte {gruppe.Key + 1} ({(string.IsNullOrWhiteSpace(t.Kopf[gruppe.Key]) ? "ohne Kopf" : t.Kopf[gruppe.Key])}): {gruppe.Count()} Zellen gelten für mehrere Zeilen (zusammengefasst); " +
                              $"gemeldet in der obersten Zeile, die übrigen Zeilen der Spalte sind leer" + (unbekannt > 0 ? $"; bei {unbekannt} ist die Zeilenzahl unbekannt (keine Trennlinien)." : "."));
        }

        var alleZellen = t.Zeilen.SelectMany(z => z.Zellen).Where(c => c is not null).Select(c => c!).ToList();
        t.Box = [Math.Min(alleZellen.Min(c => c.L), t.Spalten.Min(s => s.L)), alleZellen.Min(c => c.B), Math.Max(alleZellen.Max(c => c.R), t.Spalten.Max(s => s.R)), alleZellen.Max(c => c.T)];
        var kopfBox = reihenZellen.Take(kopfZeilen).SelectMany(r => r.Z.Where(z => z is not null)).ToList();
        if (kopfBox.Count > 0) t.Box = [t.Box[0], Math.Min(t.Box[1], kopfBox.Min(c => c!.B)), t.Box[2], Math.Max(t.Box[3], kopfBox.Max(c => c!.T))];
        return t;
    }

    /// <summary>Grundlinie der letzten Zeile einer (umbrochenen) Zelle.</summary>
    static double Unten(PZelle c) => c.Unten != 0 ? c.Unten : c.Grund;

    static double Median(List<double> werte, double vorgabe)
    {
        if (werte.Count == 0) return vorgabe;
        werte = werte.OrderBy(x => x).ToList();
        return werte[werte.Count / 2];
    }

    /// <summary>Zellen den Spalten zuordnen. Kurze Zellen nach Überdeckung (über mehreren Spalten: alle, die sie zu mindestens
    /// 20 % bedecken); lange Zellen nur, wenn sie am linken Rand einer Spalte beginnen (±2.5 pt).</summary>
    static List<(PZelle, List<int>)> Zuordnen(List<PZelle> zellen, List<(double L, double R)> spalten, double maxBreite)
    {
        var aus = new List<(PZelle, List<int>)>();
        foreach (var c in zellen)
        {
            if (!Kurz(c, maxBreite))
            {
                int nah = -1; double best = 2.5;
                for (int k = 0; k < spalten.Count; k++)
                    if (Math.Abs(c.L - spalten[k].L) <= best) { best = Math.Abs(c.L - spalten[k].L); nah = k; }
                if (nah >= 0) aus.Add((c, [nah]));
                continue;
            }
            double b = Math.Max(c.R - c.L, 0.1);
            var cols = new List<int>();
            for (int k = 0; k < spalten.Count; k++)
                if (Math.Min(c.R, spalten[k].R + 1) - Math.Max(c.L, spalten[k].L - 1) >= 0.2 * b) cols.Add(k);
            if (cols.Count == 0) continue;
            if (cols.Count > 1)
            {   // eine Zelle, die nur wegen 1 pt Toleranz in die Nachbarspalte ragt, bleibt in ihrer Hauptspalte
                var haupt = cols.OrderByDescending(k => Math.Min(c.R, spalten[k].R) - Math.Max(c.L, spalten[k].L)).First();
                var echt = cols.Where(k => Math.Min(c.R, spalten[k].R) - Math.Max(c.L, spalten[k].L) >= 0.2 * b).ToList();
                cols = echt.Count > 1 ? echt : [haupt];
            }
            aus.Add((c, cols));
        }
        return aus;
    }

    // ---------- Linien ----------

    sealed class Trenner
    {
        public bool Zeilentrenner;
        public List<(double Y, double X0, double X1)> Linien = [];
        /// <summary>y-Werte der waagrechten Linien, die die Spalte überdecken.</summary>
        public List<double> Je((double L, double R) spalte) =>
            Linien.Where(l => l.X0 <= spalte.L + Math.Max(1.5, 0.25 * (spalte.R - spalte.L)) && l.X1 >= spalte.R - Math.Max(1.5, 0.25 * (spalte.R - spalte.L))).Select(l => l.Y).Distinct().OrderBy(y => y).ToList();
    }

    /// <summary>Waagrechte Linien (Strecken und dünne Rechtecke) im Bereich der Tabelle. Zeilentrenner = eine Spalte hat
    /// Trennlinien zwischen mindestens 60 % ihrer Zeilen.</summary>
    static Trenner Waagrechte(PSeite seite, PTabelle t)
    {
        var tr = new Trenner();
        double l = t.Spalten.Min(s => s.L) - 3, r = t.Spalten.Max(s => s.R) + 3;
        double pitch = Median(t.Zeilen.Zip(t.Zeilen.Skip(1), (a, b) => a.Grund - b.Grund).Where(x => x > 0.5).ToList(), 12);
        double oben = t.Zeilen.Max(z => z.Grund) + 1.2 * pitch, unten = t.Zeilen.Min(z => z.Grund) - pitch;
        foreach (var s in seite.Strecken)
        {
            if (!s.Waagrecht || s.Laenge < 6) continue;
            double y = (s.Y0 + s.Y1) / 2, x0 = Math.Min(s.X0, s.X1), x1 = Math.Max(s.X0, s.X1);
            if (y < unten || y > oben || x1 < l || x0 > r) continue;
            tr.Linien.Add((y, x0, x1));
        }
        if (tr.Linien.Count < 2) return tr;
        double bestes = 0;
        for (int k = 0; k < t.Spalten.Count; k++) bestes = Math.Max(bestes, tr.Je(t.Spalten[k]).Count(y => y <= oben && y >= unten));
        tr.Zeilentrenner = bestes >= 0.6 * (t.Zeilen.Count - 1);
        return tr;
    }
}
