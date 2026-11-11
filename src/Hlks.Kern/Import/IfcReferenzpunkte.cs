// Referenzpunkte beim Ausrichten (Christian 11.11.2026: «bei den meisten modellen gibt es referenz punkte zum ausrichten. Bsp Kegel, solches sollte bei
// der automatischen ausrichtung geprüft werden. Siehe BKWHS letzte gescheiterte einpassung.», «die kegel liegen meist abseits des gebäudes.»).
//
// Befund BKW HS (11.11.2026): Das Archicad-IFC (Projektbezug) hat zwei Referenzkegel (Typ «Kegel 27») abseits des Gebäudes auf den Landeskoordinaten
// 2 600 830 / 1 200 380 und 2 601 050 / 1 200 610. Das Trimble-Nova-IFC der Gebäudetechnik hat keine Wände und Räume (Einpassen über Kanten unmöglich)
// und keine Kegel, aber sein Gelände- und Gebäudeursprung liegt genau auf dem ersten Kegel.
//
// Verfahren: Referenzpunkte der Datei sind ihre Kegel; ohne Kegel ihr Gelände- und Gebäudeursprung. Die Kegel des Projekts stammen aus dem ifcBezug
// der Projektinformation (referenzkegel, seit 11.11.2026 beim ersten IFC gespeichert) bzw. aus der Datei, die den Projektbezug gesetzt hat. Je
// Lage (projekt = Weltkoordinaten der Datei, gebaeude = Gebäudeursprung auf den Projektursprung) wird gezählt, wie viele Referenzpunkte der Datei im
// Grundriss auf einem Kegel des Projekts liegen (≤ ToleranzMm). Haben beide Seiten mindestens zwei Kegel, wird zusätzlich eine Lage aus zwei
// Kegelpaaren mit gleichem Abstand gerechnet (Drehung und Verschiebung). Eindeutig ist eine Lage, wenn nur sie Treffer hat.

using System.Collections.Concurrent;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class IfcReferenzpunkte
{
    /// <summary>ANNAHME (Hauptlauf): Referenzpunkte gelten als gleich, wenn sie im Grundriss höchstens 50 mm auseinander liegen.</summary>
    public const double ToleranzMm = 50;

    static double Abstand(double dx, double dy) => Math.Sqrt(dx * dx + dy * dy);

    public sealed record Ergebnis(JsonObject Bericht, IfcBezug? Bezug, string? Lage, string Text);

    static readonly ConcurrentDictionary<string, List<double[]>> projektKegelCache = new();

    /// <summary>Kegel der Datei, die den Projektbezug gesetzt hat (Weltkoordinaten jener Datei, mm), oder null, wenn unbekannt.</summary>
    public static List<double[]>? ProjektKegel(Projekt p, JsonObject? projektBezug)
    {
        if (projektBezug is null) return null;
        if (projektBezug["referenzkegel"] is JsonArray a)
            return a.OfType<JsonArray>().Select(k => k.Select(v => Json.Zahl(v) ?? 0).ToArray()).Where(k => k.Length >= 2).ToList();
        if (projektBezug["quelle"] is not JsonValue qv || !qv.TryGetValue<string>(out var quelle)) return null;
        var name = quelle.Split(" (")[0].Trim();
        string pfad;
        try { pfad = Unterlagen.Pfad(p, name); } catch (HlksFehler) { return null; }
        if (!File.Exists(pfad)) return null;
        var schluessel = pfad + "|" + File.GetLastWriteTimeUtc(pfad).Ticks;
        return projektKegelCache.GetOrAdd(schluessel, _ => { using var d = IfcDatei.Oeffnen(pfad); return d.Referenzkegel(); });
    }

    /// <summary>Punkte in Weltkoordinaten (mm) unter einem Bezug in Projektkoordinaten (Grundriss): Modell = DrehungZ(−d)·(Welt − B).</summary>
    static double[] ImProjekt(double[] welt, IfcBezug b)
    {
        double dx = welt[0] - b.X, dy = welt[1] - b.Y, r = -b.Drehung * Math.PI / 180;
        return [dx * Math.Cos(r) - dy * Math.Sin(r), dx * Math.Sin(r) + dy * Math.Cos(r)];
    }

    static int Treffer(List<double[]> datei, IfcBezug b, List<double[]> projektImModell) =>
        datei.Count(p => { var m = ImProjekt(p, b); return projektImModell.Any(k => Abstand(k[0] - m[0], k[1] - m[1]) <= ToleranzMm); });

    /// <summary>Prüft die Lagen projekt und gebaeude (und eine Lage aus zwei Kegelpaaren) gegen die Kegel des Projekts.</summary>
    public static Ergebnis? Pruefen(IfcDatei d, IfcBezug proj, IfcBezug eigener, List<double[]>? projektKegel)
    {
        if (projektKegel is not { Count: > 0 }) return null;
        var kegel = d.Referenzkegel();
        var lage = d.Lage();
        var punkte = kegel.Count > 0 ? kegel : new[] { lage.GelaendeWelt, lage.GebaeudeWelt }.Where(p => p is not null).Select(p => p!).ToList();
        string art = kegel.Count > 0 ? "kegel" : "ursprung";
        var pm = projektKegel.Select(k => ImProjekt(k, proj)).ToList();
        var lagen = new List<(string Name, IfcBezug Bezug, int Treffer)>
        {
            ("projekt", proj, Treffer(punkte, proj, pm)),
            ("gebaeude", eigener, Treffer(punkte, eigener, pm)),
        };
        if (kegel.Count >= 2 && projektKegel.Count >= 2 && Paarlage(kegel, projektKegel, proj, eigener.Z) is { } paar)
            lagen.Add(("referenzkegel", paar, Treffer(punkte, paar, pm)));
        // gleiche Lagen (≤ Toleranz, ≤ 0.01°) nur einmal: projekt und gebaeude fallen zusammen, wenn die Datei im Projektbezug liegt
        var verschieden = new List<(string Name, IfcBezug Bezug, int Treffer)>();
        foreach (var l in lagen.Where(l => l.Treffer > 0))
            if (!verschieden.Any(v => Abstand(v.Bezug.X - l.Bezug.X, v.Bezug.Y - l.Bezug.Y) <= ToleranzMm && Math.Abs(v.Bezug.Drehung - l.Bezug.Drehung) <= 0.01))
                verschieden.Add(l);
        int noetig = art == "kegel" ? Math.Min(2, kegel.Count) : 1;
        var gut = verschieden.Where(v => v.Treffer >= noetig).OrderByDescending(v => v.Treffer).ToList();
        var bericht = new JsonObject
        {
            ["projektKegel"] = projektKegel.Count, ["dateiKegel"] = kegel.Count, ["punkte"] = art,
            ["toleranzMm"] = ToleranzMm,
            ["trefferJeLage"] = new JsonObject(lagen.Select(l => KeyValuePair.Create(l.Name, (JsonNode?)l.Treffer))),
        };
        string was = art == "kegel" ? $"{kegel.Count} Kegel der Datei" : "Gelände- bzw. Gebäudeursprung der Datei (keine Kegel)";
        if (gut.Count == 1)
        {
            var g = gut[0];
            bericht["lage"] = g.Name; bericht["bezug"] = g.Bezug.AlsJson(); bericht["ergebnis"] = IfcEinpassen.Eindeutig;
            var text = $"Referenzpunkte: {was} – {g.Treffer} davon liegen mit der Lage «{g.Name}» auf einem der {projektKegel.Count} Kegel des Projekts (≤ {ToleranzMm:0} mm), mit den anderen Lagen keiner.";
            bericht["text"] = text;
            return new Ergebnis(bericht, g.Bezug, g.Name, text);
        }
        var t = gut.Count == 0
            ? $"Referenzpunkte: {was} – kein Treffer auf den {projektKegel.Count} Kegeln des Projekts."
            : $"Referenzpunkte: {was} – Treffer mit mehreren Lagen ({string.Join(", ", gut.Select(v => v.Name))}), nicht eindeutig.";
        bericht["ergebnis"] = gut.Count == 0 ? "kein Treffer" : "unsicher"; bericht["text"] = t;
        return new Ergebnis(bericht, null, null, t);
    }

    /// <summary>Lage aus zwei Kegelpaaren mit gleichem Abstand (≤ Toleranz): Drehung θ und Verschiebung so, dass die Kegel der Datei auf denen des
    /// Projekts liegen. Bezug B und Drehung d der Datei mit R(−d) = R(−dp)·R(θ) und B = Pi − R(−θ)·(Kk − Bp).</summary>
    static IfcBezug? Paarlage(List<double[]> datei, List<double[]> projekt, IfcBezug proj, double z)
    {
        for (int i = 0; i < datei.Count; i++)
            for (int j = 0; j < datei.Count; j++)
            {
                if (i == j) continue;
                double ld = Abstand(datei[j][0] - datei[i][0], datei[j][1] - datei[i][1]);
                if (ld < 1000) continue;   // zu nahe beieinander für eine Drehung
                for (int k = 0; k < projekt.Count; k++)
                    for (int l = 0; l < projekt.Count; l++)
                    {
                        if (k == l) continue;
                        double lp = Abstand(projekt[l][0] - projekt[k][0], projekt[l][1] - projekt[k][1]);
                        if (Math.Abs(lp - ld) > ToleranzMm) continue;
                        double theta = Math.Atan2(projekt[l][1] - projekt[k][1], projekt[l][0] - projekt[k][0]) - Math.Atan2(datei[j][1] - datei[i][1], datei[j][0] - datei[i][0]);
                        double vx = projekt[k][0] - proj.X, vy = projekt[k][1] - proj.Y;
                        double c = Math.Cos(-theta), s = Math.Sin(-theta);
                        double bx = datei[i][0] - (vx * c - vy * s), by = datei[i][1] - (vx * s + vy * c);
                        double d = proj.Drehung - theta * 180 / Math.PI;
                        d = ((d + 180) % 360 + 360) % 360 - 180;
                        return new IfcBezug(bx, by, z, Math.Round(d, 9));
                    }
            }
        return null;
    }
}
