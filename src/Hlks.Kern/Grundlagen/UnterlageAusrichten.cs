// DWG/DXF-Unterlage automatisch auf das Modell ausrichten (Christian 05.11.2026: «können wir das automatisieren» – «DWG automatisch ausrichten»;
// Anlass: 1.UG der Bernstrasse liegt nicht deckungsgleich mit dem IFC).
// Gesucht wird die Drehung und Verschiebung (der Faktor bleibt aus der Einheit der Datei), bei der die Wandlinien der Zeichnung auf den Wänden
// des Geschosses liegen:
// - Modell: Kanten der Wände (Achse ± halbe Dicke bzw. Polygonkanten) und Stützen des Geschosses, als Punkte alle ~100 mm.
// - Zeichnung: Linien der Layer mit «wand», «wänd» oder «mauer» im Namen (sonst alle Linien), ebenso als Punkte.
// - Drehung: Hauptrichtungen beider (Winkel modulo 90°, nach Länge gewichtet) → vier Kandidaten im Abstand von 90°, dazu die bisherige Drehung.
// - Verschiebung: Abstimmung der Punktdifferenzen (Raster 250 mm), die besten Kandidaten verfeinert (nächster Modellpunkt bis 300 mm, Mittel).
// - Trefferquote: Anteil der Zeichnungspunkte höchstens 80 mm neben einer Modellkante.
// ANNAHMEN (Hauptlauf): Abstände 100/250/300/80 mm; übernommen wird nur ab 30 % Treffer und wenn es besser ist als die bisherige Lage.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class UnterlageAusrichten
{
    public const double MindestTreffer = 0.30;
    public const string Text = "Richtet eine DWG/DXF-Unterlage automatisch auf die Wände ihres Geschosses aus: Drehung und Verschiebung so, dass die Wandlinien " +
        "der Zeichnung (Layer mit «wand»/«mauer», sonst alle Linien) auf den Wandkanten des Modells liegen; der Faktor bleibt. Übernommen ab 30 % Treffer " +
        "und nur, wenn es besser ist als die bisherige Lage. Beim Einfügen einer DWG/DXF geschieht das von selbst.";

    public sealed record Ergebnis(UnterlagenLage Lage, double Treffer, double TrefferVorher, int PunkteModell, int PunkteZeichnung);

    sealed class Raster(double zelle)
    {
        readonly Dictionary<(long, long), List<(double X, double Y, double Nx, double Ny)>> felder = [];
        public void Add(double x, double y, double nx, double ny)
        {
            var k = ((long)Math.Floor(x / zelle), (long)Math.Floor(y / zelle));
            if (!felder.TryGetValue(k, out var l)) felder[k] = l = [];
            l.Add((x, y, nx, ny));
        }
        /// <summary>Nächster Punkt höchstens r entfernt (r ≤ zelle × 3).</summary>
        public (double X, double Y, double Nx, double Ny)? Naechster(double x, double y, double r)
        {
            long cx = (long)Math.Floor(x / zelle), cy = (long)Math.Floor(y / zelle);
            int n = (int)Math.Ceiling(r / zelle);
            (double, double, double, double)? best = null; double bd = r * r;
            for (long i = cx - n; i <= cx + n; i++)
                for (long j = cy - n; j <= cy + n; j++)
                    if (felder.TryGetValue((i, j), out var l))
                        foreach (var q in l)
                        {
                            double d = (q.X - x) * (q.X - x) + (q.Y - y) * (q.Y - y);
                            if (d <= bd) { bd = d; best = q; }
                        }
            return best;
        }
    }

    static void Abtasten(List<(double X, double Y, double Nx, double Ny)> aus, double x1, double y1, double x2, double y2, double schritt)
    {
        double l = Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
        if (l < 1e-9) return;
        double nx = -(y2 - y1) / l, ny = (x2 - x1) / l;
        int n = Math.Max(1, (int)(l / schritt));
        for (int i = 0; i <= n; i++) aus.Add((x1 + (x2 - x1) * i / n, y1 + (y2 - y1) * i / n, nx, ny));
    }

    /// <summary>Kanten der Wände und Stützen des Geschosses in Modell-mm.</summary>
    static List<(double X1, double Y1, double X2, double Y2)> ModellKanten(Modell m, string? geschoss)
    {
        var aus = new List<(double, double, double, double)>();
        foreach (var e in m.Elemente)
        {
            if (e.Geschoss != geschoss || e.Klasse is not ("Wand" or "Stuetze") || e.Geometrie is not { } g || g.Punkte.Count < 2) continue;
            if (g.Art == "linie" || g.Art == "polylinie")
            {
                double h = (g.Dicke ?? 0) / 2;
                for (int i = 1; i < g.Punkte.Count; i++)
                {
                    var a = g.Punkte[i - 1]; var b = g.Punkte[i];
                    double dx = b[0] - a[0], dy = b[1] - a[1], l = Math.Sqrt(dx * dx + dy * dy);
                    if (l < 1) continue;
                    double nx = -dy / l * h, ny = dx / l * h;
                    if (h > 0) { aus.Add((a[0] + nx, a[1] + ny, b[0] + nx, b[1] + ny)); aus.Add((a[0] - nx, a[1] - ny, b[0] - nx, b[1] - ny)); }
                    else aus.Add((a[0], a[1], b[0], b[1]));
                }
            }
            else if (g.Art == "polygon")
                for (int i = 0; i < g.Punkte.Count; i++)
                {
                    var a = g.Punkte[i]; var b = g.Punkte[(i + 1) % g.Punkte.Count];
                    aus.Add((a[0], a[1], b[0], b[1]));
                }
        }
        return aus;
    }

    /// <summary>Linien der Zeichnung (Zeichnungseinheiten): Wand-Layer, sonst alle.</summary>
    static List<(double X1, double Y1, double X2, double Y2)> ZeichnungsKanten(CadZeichnung z, bool alleErlaubt)
    {
        var wand = Enumerable.Range(0, z.Layer.Count).Where(i =>
        {
            var l = z.Layer[i].ToLowerInvariant();
            return (l.Contains("wand") || l.Contains("wänd") || l.Contains("mauer")) && !l.Contains("schraff") && !l.Contains("text");
        }).ToHashSet();
        var aus = new List<(double, double, double, double)>();
        if (wand.Count == 0 && !alleErlaubt) return aus;   // beim Einfügen nur mit Wand-Layern (sonst richtet es sich an beliebigen Linien aus)
        foreach (var p in z.Primitive)
        {
            if (p.Art is not ('l' or 'p' or 'g') || (wand.Count > 0 && !wand.Contains(p.Layer))) continue;
            var w = p.W;
            for (int i = 2; i + 1 < w.Length; i += 2) aus.Add((w[i - 2], w[i - 1], w[i], w[i + 1]));
            if (p.Art == 'g' && w.Length >= 6) aus.Add((w[^2], w[^1], w[0], w[1]));
        }
        return aus;
    }

    /// <summary>Hauptrichtung (Grad, modulo 90) nach Länge gewichtet.</summary>
    static double Hauptrichtung(IEnumerable<(double X1, double Y1, double X2, double Y2)> kanten, double faktor = 1)
    {
        var h = new double[90];
        foreach (var (x1, y1, x2, y2) in kanten)
        {
            double dx = x2 - x1, dy = y2 - y1, l = Math.Sqrt(dx * dx + dy * dy) * faktor;
            if (l < 1) continue;
            double w = ((Math.Atan2(dy, dx) * 180 / Math.PI) % 90 + 90) % 90;
            h[(int)Math.Floor(w) % 90] += l;
        }
        int best = Array.IndexOf(h, h.Max());
        // Feinwert: gewichtetes Mittel der Nachbarn
        double s = 0, sw = 0;
        for (int d = -1; d <= 1; d++) { int i = (best + d + 90) % 90; s += (best + d + 0.5) * h[i]; sw += h[i]; }
        return sw > 0 ? s / sw : best + 0.5;
    }

    public static Ergebnis? Suchen(Modell m, CadZeichnung z, string? geschoss, UnterlagenLage aktuell, bool alleLinien = true)
    {
        var mk = ModellKanten(m, geschoss);
        var zk = ZeichnungsKanten(z, alleLinien);
        if (mk.Count < 4 || zk.Count < 4) return null;
        double f = aktuell.Faktor;
        double lm = mk.Sum(k => Math.Sqrt((k.X2 - k.X1) * (k.X2 - k.X1) + (k.Y2 - k.Y1) * (k.Y2 - k.Y1)));
        double lz = zk.Sum(k => Math.Sqrt((k.X2 - k.X1) * (k.X2 - k.X1) + (k.Y2 - k.Y1) * (k.Y2 - k.Y1))) * f;
        var mp = new List<(double X, double Y, double Nx, double Ny)>();
        foreach (var k in mk) Abtasten(mp, k.X1, k.Y1, k.X2, k.Y2, Math.Max(100, lm / 6000));
        var zp = new List<(double X, double Y, double Nx, double Ny)>();
        foreach (var k in zk) Abtasten(zp, k.X1, k.Y1, k.X2, k.Y2, Math.Max(100, lz / 4000) / f);
        var raster = new Raster(100);
        foreach (var q in mp) raster.Add(q.X, q.Y, q.Nx, q.Ny);

        double Treffer(UnterlagenLage l)
        {
            int n = 0;
            foreach (var q in zp) { var (x, y) = l.Auf(q.X, q.Y); if (raster.Naechster(x, y, 80) is not null) n++; }
            return (double)n / zp.Count;
        }
        UnterlagenLage Verfeinern(UnterlagenLage l)
        {
            for (int it = 0; it < 6; it++)
            {
                // kleinste Quadrate Punkt-zu-Kante: Σ n·nᵀ · t = Σ d·n
                double a11 = 0, a12 = 0, a22 = 0, b1 = 0, b2 = 0; int n = 0;
                double r = it < 3 ? 300 : 120;
                foreach (var q in zp)
                {
                    var (x, y) = l.Auf(q.X, q.Y);
                    if (raster.Naechster(x, y, r) is { } t)
                    {
                        double d = (t.X - x) * t.Nx + (t.Y - y) * t.Ny;
                        a11 += t.Nx * t.Nx; a12 += t.Nx * t.Ny; a22 += t.Ny * t.Ny; b1 += d * t.Nx; b2 += d * t.Ny; n++;
                    }
                }
                double det = a11 * a22 - a12 * a12;
                if (n < 10 || det < 1e-6 * n * n) break;   // nur eine Richtung: unbestimmt
                l = l with { Dx = l.Dx + (b1 * a22 - b2 * a12) / det, Dy = l.Dy + (a11 * b2 - a12 * b1) / det };
            }
            return l;
        }

        double vorher = Treffer(aktuell);
        double am = Hauptrichtung(mk), az = Hauptrichtung(zk, f);
        var winkel = new List<double> { aktuell.Drehung };
        for (int k = 0; k < 4; k++) winkel.Add(UnterlagenLage.Normieren(am - az + 90 * k));
        var zufall = new Random(1);
        var mStich = mp.OrderBy(_ => zufall.Next()).Take(400).ToList();
        var zStich = zp.OrderBy(_ => zufall.Next()).Take(400).ToList();
        UnterlagenLage beste = aktuell; double besteT = vorher;
        foreach (var w in winkel.Distinct())
        {
            var l0 = new UnterlagenLage(f, w, 0, 0);
            var stimmen = new Dictionary<(long, long), int>();
            foreach (var u in zStich)
            {
                var (ux, uy) = l0.Auf(u.X, u.Y);
                foreach (var q in mStich)
                {
                    var key = ((long)Math.Round((q.X - ux) / 250), (long)Math.Round((q.Y - uy) / 250));
                    stimmen[key] = stimmen.GetValueOrDefault(key) + 1;
                }
            }
            foreach (var ((kx, ky), _) in stimmen.OrderByDescending(x => x.Value).Take(6))
            {
                var l = Verfeinern(l0 with { Dx = kx * 250.0, Dy = ky * 250.0 });
                double t = Treffer(l);
                if (t > besteT + 1e-9) { besteT = t; beste = l; }
            }
        }
        return new Ergebnis(beste, besteT, vorher, mp.Count, zp.Count);
    }

    /// <summary>Hinweistext zu einem Ergebnis.</summary>
    public static string Beschreibung(string kz, Ergebnis r, bool uebernommen) => uebernommen
        ? $"{kz} automatisch ausgerichtet: Drehung {r.Lage.Drehung:0.##}°, Verschiebung {r.Lage.Dx / 1000:0.000} / {r.Lage.Dy / 1000:0.000} m; {r.Treffer:P0} der Wandlinien liegen auf den Wänden (vorher {r.TrefferVorher:P0})."
        : $"{kz}: automatische Ausrichtung nicht übernommen – beste Lage trifft {r.Treffer:P0} der Wandlinien (vorher {r.TrefferVorher:P0}, nötig {MindestTreffer:P0}); bitte mit unterlage_kalibrieren ausrichten.";
}

public sealed class UnterlageAusrichtenBefehl : IBefehl
{
    public string Name => "unterlage_ausrichten";
    public string Beschreibung => UnterlageAusrichten.Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } = [new("unterlage", "text", true, "Unterlage (Kennzeichen) mit DWG/DXF im Grundriss eines Geschosses")];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var ul = k.Arbeit.HoleDerKlasse(Param.Text(p, "unterlage", true)!, "Unterlage");
        if (ul.Text("art") is not ("dwg" or "dxf")) throw new HlksFehler($"{ul.Kennzeichen} ist keine DWG/DXF-Unterlage.", "Nur DWG und DXF lassen sich an Wänden ausrichten; PDF und Bilder mit unterlage_kalibrieren.");
        var alt = UnterlagenLage.Lesen(ul);
        var r = UnterlageAusrichten.Suchen(k.Arbeit, CadZeichnung.Laden(k.Projekt, ul), ul.Geschoss, alt)
            ?? throw new HlksFehler($"{ul.Kennzeichen}: zu wenig Wände im Geschoss oder Linien in der Zeichnung zum Ausrichten.", "Wände des Geschosses anlegen bzw. übernehmen oder mit unterlage_kalibrieren ausrichten.");
        bool ok = r.Treffer >= UnterlageAusrichten.MindestTreffer && r.Treffer > r.TrefferVorher + 0.02;
        if (!ok)
        {
            if (r.TrefferVorher >= UnterlageAusrichten.MindestTreffer) { k.Hinweise.Add($"{ul.Kennzeichen} liegt schon gut ({r.TrefferVorher:P0} der Wandlinien auf den Wänden) – nichts geändert."); return; }
            throw new HlksFehler(UnterlageAusrichten.Beschreibung(ul.Kennzeichen, r, false), "Mit unterlage_kalibrieren zwei Punkte der Unterlage auf ihre Sollpunkte legen.");
        }
        var e = ul.Kopie();
        if (e.Geometrie is { } g)
            g.Punkte = g.Punkte.Select(q => { var (u, v) = alt.Zurueck(q[0], q[1]); var (x, y) = r.Lage.Auf(u, v); return q.Length > 2 ? new[] { x, y, q[2] } : new[] { x, y }; }).ToList();
        e.Eigenschaften["transformation"] = new Wert { Inhalt = r.Lage.Zu(ul.Eigenschaft("transformation")?.Inhalt as JsonObject), Herkunft = k.Herkunft };
        k.Setzen(e);
        k.Hinweise.Add(UnterlageAusrichten.Beschreibung(ul.Kennzeichen, r, true));
    }
}
