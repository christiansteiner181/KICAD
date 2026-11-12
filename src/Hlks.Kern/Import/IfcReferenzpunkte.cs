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
// Grundriss auf einem Kegel des Projekts liegen (≤ ToleranzMm). Trifft genau eine dieser beiden Lagen genügend Kegel, gilt sie.
// Trifft keine, und haben beide Seiten mindestens zwei Kegel, werden Lagen aus zwei Kegelpaaren mit gleichem Abstand gerechnet (Drehung und
// Verschiebung) – ALLE Zuordnungen, denn zwei Kegel bestimmen die Lage nur bis auf eine halbe Drehung (Kegel 1 auf A und 2 auf B oder umgekehrt;
// Gesamtprüfung 13.11.2026: vorher galt stillschweigend die erste Zuordnung). Eindeutig ist eine solche Lage nur, wenn sie allein die meisten
// Kegel trifft (ab drei Kegeln, die nicht spiegelgleich stehen); sonst «unsicher», es wird nichts empfohlen und nichts eingepasst.

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
        if (Bezugsdatei(p, quelle) is not { } pfad) return null;
        var schluessel = pfad + "|" + File.GetLastWriteTimeUtc(pfad).Ticks;
        return projektKegelCache.GetOrAdd(schluessel, _ => { using var d = IfcDatei.Oeffnen(pfad); return d.Referenzkegel(); });
    }

    /// <summary>Datei aus ifcBezug.quelle «&lt;Name&gt; (&lt;Schema&gt;, &lt;Programm&gt;)». Der Name kann selbst « (» enthalten («Modell (2).ifc»), das
    /// Programm auch: von hinten jede Trennstelle versuchen, bis eine abgelegte Unterlage so heisst.</summary>
    static string? Bezugsdatei(Projekt p, string quelle)
    {
        for (int i = quelle.LastIndexOf(" (", StringComparison.Ordinal); i > 0; i = quelle.LastIndexOf(" (", i - 1, StringComparison.Ordinal))
        {
            try { return Unterlagen.Pfad(p, quelle[..i].Trim()); }
            catch (HlksFehler) { }
        }
        return null;
    }

    /// <summary>Punkte in Weltkoordinaten (mm) unter einem Bezug in Projektkoordinaten (Grundriss): Modell = DrehungZ(−d)·(Welt − B).</summary>
    static double[] ImProjekt(double[] welt, IfcBezug b)
    {
        double dx = welt[0] - b.X, dy = welt[1] - b.Y, r = -b.Drehung * Math.PI / 180;
        return [dx * Math.Cos(r) - dy * Math.Sin(r), dx * Math.Sin(r) + dy * Math.Cos(r)];
    }

    static int Treffer(List<double[]> datei, IfcBezug b, List<double[]> projektImModell) =>
        datei.Count(p => { var m = ImProjekt(p, b); return projektImModell.Any(k => Abstand(k[0] - m[0], k[1] - m[1]) <= ToleranzMm); });

    /// <summary>Gleiche Lage: jeder Referenzpunkt der Datei landet mit beiden Bezügen am selben Ort (≤ Toleranz). Verglichen wird die Wirkung,
    /// nicht x/y/Drehung – der Bezugspunkt liegt bei Landeskoordinaten weit weg, kleine Winkel verschieben ihn stark.</summary>
    static bool Gleich(IfcBezug a, IfcBezug b, List<double[]> punkte) =>
        punkte.All(p => { var pa = ImProjekt(p, a); var pb = ImProjekt(p, b); return Abstand(pa[0] - pb[0], pa[1] - pb[1]) <= ToleranzMm; });

    /// <summary>Prüft die Lagen projekt und gebaeude (und Lagen aus zwei Kegelpaaren) gegen die Kegel des Projekts.</summary>
    public static Ergebnis? Pruefen(IfcDatei d, IfcBezug proj, IfcBezug eigener, List<double[]>? projektKegel)
    {
        if (projektKegel is not { Count: > 0 }) return null;
        var lage = d.Lage();
        return Bewerten(d.Referenzkegel(), [lage.GelaendeWelt, lage.GebaeudeWelt], proj, eigener, projektKegel);
    }

    /// <summary>Die Bewertung ohne Datei (rechenbar von Hand): kegel = Kegel der Datei, urspruenge = ihr Gelände- und Gebäudeursprung (zählen nur
    /// ohne Kegel), alles in Weltkoordinaten der Datei (mm); projektKegel in Weltkoordinaten der Bezugsdatei (mm).</summary>
    public static Ergebnis? Bewerten(List<double[]> kegel, IEnumerable<double[]?> urspruenge, IfcBezug proj, IfcBezug eigener, List<double[]>? projektKegel)
    {
        if (projektKegel is not { Count: > 0 }) return null;
        var punkte = kegel.Count > 0 ? kegel : urspruenge.Where(p => p is not null).Select(p => p!).ToList();
        string art = kegel.Count > 0 ? "kegel" : "ursprung";
        var pm = projektKegel.Select(k => ImProjekt(k, proj)).ToList();
        int noetig = art == "kegel" ? Math.Min(2, kegel.Count) : 1;

        // 1. Die Lagen, die die Datei selbst mitbringt. Gleiche Lagen nur einmal: projekt und gebaeude fallen zusammen, wenn die Datei im Projektbezug liegt
        var gegeben = new List<(string Name, IfcBezug Bezug, int Treffer)> { ("projekt", proj, Treffer(punkte, proj, pm)), ("gebaeude", eigener, Treffer(punkte, eigener, pm)) };
        var gut = new List<(string Name, IfcBezug Bezug, int Treffer)>();
        foreach (var l in gegeben.Where(l => l.Treffer >= noetig))
            if (!gut.Any(v => Gleich(v.Bezug, l.Bezug, punkte))) gut.Add(l);

        // 2. Nur wenn keine davon trifft: Lagen aus zwei Kegelpaaren. Sie sind so gerechnet, dass zwei Kegel treffen – das allein belegt nichts;
        //    es zählt, welche Lage die meisten Kegel trifft, und ob sie damit allein ist.
        var paare = new List<(IfcBezug Bezug, int Treffer)>();
        bool halbeDrehung = false;
        if (gut.Count == 0 && kegel.Count >= 2 && projektKegel.Count >= 2)
        {
            paare = Paarlagen(kegel, projektKegel, proj, eigener.Z).Select(b => (b, Treffer(punkte, b, pm))).ToList();
            int max = paare.Count == 0 ? 0 : paare.Max(x => x.Treffer);
            var beste = paare.Where(x => x.Treffer == max && max >= noetig).ToList();
            halbeDrehung = beste.Count > 1;
            gut.AddRange(beste.Select(x => ("referenzkegel", x.Bezug, x.Treffer)));
        }

        var treffer = new JsonObject(gegeben.Select(l => KeyValuePair.Create(l.Name, (JsonNode?)l.Treffer)));
        if (paare.Count > 0) { treffer["referenzkegel"] = paare.Max(x => x.Treffer); treffer["referenzkegelLagen"] = paare.Count; }
        var bericht = new JsonObject
        {
            ["projektKegel"] = projektKegel.Count, ["dateiKegel"] = kegel.Count, ["punkte"] = art,
            ["toleranzMm"] = ToleranzMm, ["trefferJeLage"] = treffer,
        };
        string was = art == "kegel" ? $"{kegel.Count} Kegel der Datei" : "Gelände- bzw. Gebäudeursprung der Datei (keine Kegel)";
        if (gut.Count == 1)
        {
            var g = gut[0];
            bericht["lage"] = g.Name; bericht["bezug"] = g.Bezug.AlsJson(); bericht["ergebnis"] = IfcEinpassen.Eindeutig;
            var text = $"Referenzpunkte: {was} – {g.Treffer} davon liegen mit der Lage «{g.Name}» auf einem der {projektKegel.Count} Kegel des Projekts (≤ {ToleranzMm:0} mm), mit keiner anderen Lage so viele.";
            bericht["text"] = text;
            return new Ergebnis(bericht, g.Bezug, g.Name, text);
        }
        var t = gut.Count == 0
            ? $"Referenzpunkte: {was} – kein Treffer auf den {projektKegel.Count} Kegeln des Projekts."
            : halbeDrehung
                ? $"Referenzpunkte: {was} – {gut.Count} Lagen treffen gleich viele Kegel ({gut[0].Treffer}), nicht eindeutig: zwei Kegel bestimmen die Lage nur bis auf eine halbe Drehung (ein dritter Kegel oder ein Koordinationskörper entscheidet)."
                : $"Referenzpunkte: {was} – Treffer mit mehreren Lagen ({string.Join(", ", gut.Select(v => v.Name))}), nicht eindeutig.";
        bericht["ergebnis"] = gut.Count == 0 ? "kein Treffer" : "unsicher"; bericht["text"] = t;
        return new Ergebnis(bericht, null, null, t);
    }

    /// <summary>Alle verschiedenen Lagen aus zwei Kegelpaaren mit gleichem Abstand (≤ Toleranz): Drehung θ und Verschiebung so, dass Kegel i der
    /// Datei auf Kegel k des Projekts liegt und j auf l. Bezug B und Drehung d der Datei mit R(−d) = R(−dp)·R(θ) und B = Pi − R(−θ)·(Kk − Bp).</summary>
    static List<IfcBezug> Paarlagen(List<double[]> datei, List<double[]> projekt, IfcBezug proj, double z)
    {
        var aus = new List<IfcBezug>();
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
                        var b = new IfcBezug(bx, by, z, Math.Round(d, 9));
                        if (!aus.Any(x => Gleich(x, b, datei))) aus.Add(b);
                    }
            }
        return aus;
    }
}
