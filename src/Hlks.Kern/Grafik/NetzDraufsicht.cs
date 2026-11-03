// IFC 1:1 Schritt 3 (entwurf/ifc-1zu1.md), Grundriss: Netzkörper (Familie aus dem IFC bzw. 3D-Modell) unterhalb der Schnitthöhe als dünne
// Sichtkanten von oben – wie Revit: was die Schnittebene schneidet, zeichnet FamilienGrafik.Schnittlinien (Stift mittel), was darunter sichtbar
// ist, hier (Stift dünn).
// Kanten: Umrisskanten (ein angrenzendes Dreieck zeigt nach oben, das andere nicht) und Knickkanten (≥ Schnittzerlegung.Knickwinkel) zwischen
// zwei nach oben zeigenden Dreiecken, auf z ≤ Schnitthöhe beschnitten. Verdeckung über ein Höhenraster: je Zelle die höchste Fläche unter der
// Schnitthöhe, im Inneren der Schnittfläche die Schnitthöhe selbst; eine Kante ist sichtbar, wo sie nicht tiefer als das Raster liegt.
// ANNAHMEN (technisch, Prüfliste): Raster höchstens 400 × 400 Zellen, mindestens 2 mm; Toleranz 2 Zellen + 1 mm; Kanten kürzer als eine Zelle
// entfallen.

namespace Hlks.Kern;

public static class NetzDraufsicht
{
    const int MaxZellen = 400;

    /// <summary>Sichtkanten von oben der Körperteile «koerper» unterhalb von z = hoehe als Linienzüge [x, y] (Koordinaten wie die Teile).</summary>
    public static List<List<double[]>> Kanten(IEnumerable<Koerperteil> teile, double hoehe)
    {
        var netze = teile.Where(t => t.Art == "koerper" && t.Ecken is { Count: >= 3 } && t.Dreiecke is { Count: >= 1 })
            .Where(t => t.Ecken!.Min(p => p[2]) < hoehe).ToList();
        if (netze.Count == 0) return [];
        double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
        foreach (var t in netze) foreach (var p in t.Ecken!) { x0 = Math.Min(x0, p[0]); y0 = Math.Min(y0, p[1]); x1 = Math.Max(x1, p[0]); y1 = Math.Max(y1, p[1]); }
        double zelle = Math.Max(2, Math.Max(x1 - x0, y1 - y0) / MaxZellen);
        int nx = (int)Math.Ceiling((x1 - x0) / zelle) + 1, ny = (int)Math.Ceiling((y1 - y0) / zelle) + 1;
        var raster = new double[nx * ny];
        Array.Fill(raster, double.NegativeInfinity);

        // Höhenraster: höchste Fläche unter der Schnitthöhe
        foreach (var t in netze)
            foreach (var d in t.Dreiecke!)
                Rastern(t.Ecken![d[0]], t.Ecken[d[1]], t.Ecken[d[2]], hoehe, raster, nx, ny, x0, y0, zelle);
        // Schnittfläche: im Inneren der geschlossenen Schnittzüge liegt der Körper bis zur Schnitthöhe
        var strecken = new List<double[]>();
        foreach (var t in netze)
        {
            if (t.Ecken!.Max(p => p[2]) < hoehe) continue;
            var pf = new float[t.Ecken.Count * 3];
            for (int i = 0; i < t.Ecken.Count; i++) { pf[3 * i] = (float)(t.Ecken[i][0] - x0); pf[3 * i + 1] = (float)(t.Ecken[i][1] - y0); pf[3 * i + 2] = (float)(t.Ecken[i][2] - hoehe); }
            var df = t.Dreiecke!.SelectMany(d => d).ToArray();
            Netzflaechen.Schneiden(pf, df, 0, strecken);
        }
        if (strecken.Count > 0)
        {
            var zuege = Netzflaechen.Verbinden(strecken).Where(z => z.Count >= 3).ToList();
            for (int j = 0; j < ny; j++)
                for (int i = 0; i < nx; i++)
                {
                    double px = i * zelle, py = j * zelle;
                    int kreuzt = 0;
                    foreach (var z in zuege)
                        for (int k = 0, m = z.Count - 1; k < z.Count; m = k++)
                            if ((z[k][1] > py) != (z[m][1] > py) && px < (z[m][0] - z[k][0]) * (py - z[k][1]) / (z[m][1] - z[k][1]) + z[k][0]) kreuzt++;
                    if (kreuzt % 2 == 1) raster[j * nx + i] = hoehe;
                }
        }

        // Kanten von oben
        double knick = Math.Cos(Schnittzerlegung.Knickwinkel * Math.PI / 180), tol = 2 * zelle + 1;
        var aus = new List<List<double[]>>();
        foreach (var t in netze)
        {
            var e = t.Ecken!;
            var n = t.Dreiecke!.Select(d => (V3.Aus(e[d[1]]) - V3.Aus(e[d[0]])).Kreuz(V3.Aus(e[d[2]]) - V3.Aus(e[d[0]])).Einheit()).ToArray();
            var anKante = new Dictionary<(int, int), List<int>>();
            for (int i = 0; i < t.Dreiecke!.Count; i++)
            {
                var d = t.Dreiecke[i];
                for (int k = 0; k < 3; k++)
                {
                    int a = d[k], b = d[(k + 1) % 3];
                    var key = a < b ? (a, b) : (b, a);
                    if (!anKante.TryGetValue(key, out var l)) anKante[key] = l = [];
                    l.Add(i);
                }
            }
            foreach (var ((a, b), l) in anKante)
            {
                bool zeichnen = l.Count == 2
                    ? (n[l[0]].Z > 1e-9) != (n[l[1]].Z > 1e-9) || (n[l[0]].Z > 1e-9 && n[l[0]].Mal(n[l[1]]) < knick)
                    : l.Any(i => n[i].Z > 1e-9);
                if (!zeichnen) continue;
                Sichtbar(e[a], e[b], hoehe, raster, nx, ny, x0, y0, zelle, tol, aus);
            }
        }
        aus.RemoveAll(z => z.Count < 2);
        return aus;
    }

    static void Rastern(double[] a, double[] b, double[] c, double hoehe, double[] raster, int nx, int ny, double x0, double y0, double zelle)
    {
        double ax = (a[0] - x0) / zelle, ay = (a[1] - y0) / zelle, bx = (b[0] - x0) / zelle, by = (b[1] - y0) / zelle, cx = (c[0] - x0) / zelle, cy = (c[1] - y0) / zelle;
        double flaeche = (bx - ax) * (cy - ay) - (cx - ax) * (by - ay);
        if (Math.Abs(flaeche) < 1e-12) return;   // senkrecht: von oben keine Fläche
        int i0 = Math.Max(0, (int)Math.Floor(Math.Min(ax, Math.Min(bx, cx)))), i1 = Math.Min(nx - 1, (int)Math.Ceiling(Math.Max(ax, Math.Max(bx, cx))));
        int j0 = Math.Max(0, (int)Math.Floor(Math.Min(ay, Math.Min(by, cy)))), j1 = Math.Min(ny - 1, (int)Math.Ceiling(Math.Max(ay, Math.Max(by, cy))));
        for (int j = j0; j <= j1; j++)
            for (int i = i0; i <= i1; i++)
            {
                double w0 = ((bx - i) * (cy - j) - (cx - i) * (by - j)) / flaeche, w1 = ((cx - i) * (ay - j) - (ax - i) * (cy - j)) / flaeche, w2 = 1 - w0 - w1;
                if (w0 < -1e-9 || w1 < -1e-9 || w2 < -1e-9) continue;
                double z = w0 * a[2] + w1 * b[2] + w2 * c[2];
                if (z <= hoehe && z > raster[j * nx + i]) raster[j * nx + i] = z;
            }
    }

    /// <summary>Strecke a–b, auf z ≤ hoehe beschnitten, in Schritten einer halben Zelle gegen das Raster geprüft; sichtbare Stücke zu aus.</summary>
    static void Sichtbar(double[] a, double[] b, double hoehe, double[] raster, int nx, int ny, double x0, double y0, double zelle, double tol, List<List<double[]>> aus)
    {
        if (a[2] > hoehe && b[2] > hoehe) return;
        if (a[2] > hoehe || b[2] > hoehe)
        {
            var (o, u) = a[2] > hoehe ? (a, b) : (b, a);
            double s = (hoehe - u[2]) / (o[2] - u[2]);
            var m = new[] { u[0] + (o[0] - u[0]) * s, u[1] + (o[1] - u[1]) * s, hoehe };
            (a, b) = (u, m);
        }
        double len = Math.Sqrt((b[0] - a[0]) * (b[0] - a[0]) + (b[1] - a[1]) * (b[1] - a[1]));
        if (len < zelle) return;
        int schritte = Math.Max(1, (int)Math.Ceiling(len / (zelle / 2)));
        List<double[]>? zug = null;
        for (int k = 0; k <= schritte; k++)
        {
            double s = (double)k / schritte;
            double x = a[0] + (b[0] - a[0]) * s, y = a[1] + (b[1] - a[1]) * s, z = a[2] + (b[2] - a[2]) * s;
            int i = Math.Clamp((int)Math.Round((x - x0) / zelle), 0, nx - 1), j = Math.Clamp((int)Math.Round((y - y0) / zelle), 0, ny - 1);
            bool sicht = z >= raster[j * nx + i] - tol;
            if (!sicht) { zug = null; continue; }
            if (zug is null) { zug = [[x, y]]; aus.Add(zug); }
            else if (zug.Count == 1) zug.Add([x, y]);
            else zug[^1] = [x, y];   // gerade Strecke: Anfang und Ende genügen
        }
    }
}
