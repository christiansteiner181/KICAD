// IFC-Übertrag HLKS (Modul 1b, B2): Ports → Anschlüsse der Katalogklasse.
// Zuordnung in dieser Reihenfolge: Leitung nur ihre beiden Enden (grösster Abstand); T-Stück Abzweig «c» aus der Lage
// (die zwei Ports mit gleicher Achse sind der Durchgang); dann Medium und Richtung, Medium allein, Richtung allein,
// zuletzt die Reihenfolge der Ports. Was übrig bleibt, wird ein zusätzlicher Anschluss, wenn die Klasse das zulässt
// (Verteiler: «Abgänge bei Bedarf als eigene Anschlüsse ergänzen», klassen.json), sonst ein Hinweis.
// Fliessrichtung: steht sie am Port (oder am Port gegenüber), gilt sie; sonst bleibt die des Katalogs, wenn der Port über
// sein Medium zugeordnet wurde, und wird «beide» (offen), wenn nur die Reihenfolge entschied – nichts erfinden.

namespace Hlks.Kern;

public sealed class IfcHlksZuordnung
{
    public List<Anschluss> Anschluesse { get; } = [];
    /// <summary>Port (EntityLabel) → Name des Anschlusses.</summary>
    public Dictionary<int, string> Name { get; } = [];
    /// <summary>Ports ohne Anschluss (Klasse hat keinen passenden, keine zusätzlichen erlaubt).</summary>
    public List<IfcHlksPort> Ohne { get; } = [];
    /// <summary>Anschlüsse, deren Richtung offen bleibt (Katalog rein/raus, Datei ohne Angabe, nur nach Reihenfolge zugeordnet).</summary>
    public int RichtungOffen { get; set; }
    public IfcHlksPort? PortVon(string anschluss, IReadOnlyList<IfcHlksPort> ports) =>
        ports.FirstOrDefault(p => Name.TryGetValue(p.Port.EntityLabel, out var n) && n.Equals(anschluss, StringComparison.OrdinalIgnoreCase));
}

public static class IfcHlksAnschluesse
{
    /// <summary>Klassen, die zusätzliche Anschlüsse haben dürfen (klassen.json, Verteiler: «Abgänge bei Bedarf als eigene
    /// Anschlüsse ergänzen»). Vorschlag an den Hauptlauf: Kennzeichen im Katalog statt dieser Liste.</summary>
    public static readonly HashSet<string> WeitereErlaubt = ["Verteiler"];

    /// <param name="medium">Medium eines Ports (eigenes System, sonst das gegenüber); null = unbekannt.</param>
    /// <param name="mitte">Lage des Elements (für den Abzweig eines T-Stücks ohne Port-Achsen).</param>
    public static IfcHlksZuordnung Zuordnen(KlassenDef def, IReadOnlyList<IfcHlksPort> ports, Func<IfcHlksPort, string?> medium, double[]? mitte)
    {
        var z = new IfcHlksZuordnung();
        z.Anschluesse.AddRange(def.Anschluesse.Select(a => a.Kopie()));
        var frei = new List<Anschluss>(z.Anschluesse);
        var art = new Dictionary<IfcHlksPort, string>();
        var offen = ports.ToList();
        bool leitung = def.IstArt("Leitung");

        if (leitung && offen.Count > 2)
        {
            // Enden = die zwei Ports mit dem grössten Abstand; die übrigen sind Abzweige an der Seite (Revit: Sattelstutzen)
            int bi = 0, bj = 1; double best = -1;
            for (int i = 0; i < offen.Count; i++)
                for (int j = i + 1; j < offen.Count; j++)
                    if (Geometrie.Abstand(offen[i].Lage, offen[j].Lage) is var a && a > best) { best = a; bi = i; bj = j; }
            var enden = new[] { offen[bi], offen[bj] };
            z.Ohne.AddRange(offen.Where(p => !enden.Contains(p)));
            offen = [.. enden];
        }

        // Wirksame Richtung: bei Leitungen ergibt ein bekanntes Ende das andere
        var fluss = offen.ToDictionary(p => p, p => p.Fluss);
        if (leitung && offen.Count == 2 && (fluss[offen[0]] is null) != (fluss[offen[1]] is null))
        {
            var bekannt = fluss[offen[0]] ?? fluss[offen[1]];
            var unbekannt = fluss[offen[0]] is null ? offen[0] : offen[1];
            fluss[unbekannt] = bekannt == "rein" ? "raus" : "rein";
        }

        void Setzen(IfcHlksPort p, Anschluss a, string wie) { z.Name[p.Port.EntityLabel] = a.Name; frei.Remove(a); offen.Remove(p); art[p] = wie; }
        static bool Gleich(string? a, string? b) => a is not null && b is not null && a.Equals(b, StringComparison.OrdinalIgnoreCase);

        if (def.Name == "TStueck" && offen.Count == 3 && Abzweig(offen, mitte) is { } c && frei.FirstOrDefault(a => a.Name == "c") is { } ac)
            Setzen(c, ac, "geometrie");

        foreach (var p in offen.ToList())
            if (medium(p) is { } m && fluss[p] is { } f && frei.FirstOrDefault(a => Gleich(a.Medium, m) && (a.Fluss == f || a.Fluss == "beide")) is { } a)
                Setzen(p, a, "medium");
        foreach (var p in offen.ToList())
            if (medium(p) is { } m && frei.FirstOrDefault(a => Gleich(a.Medium, m)) is { } a)
                Setzen(p, a, "medium");
        foreach (var p in offen.ToList())
            if (fluss[p] is { } f && frei.FirstOrDefault(a => (a.Medium == "*" || medium(p) is null || Gleich(a.Medium, medium(p))) && a.Fluss == f) is { } a)
                Setzen(p, a, "richtung");
        foreach (var p in offen.ToList())
            if (frei.FirstOrDefault(a => a.Medium == "*" || medium(p) is null || Gleich(a.Medium, medium(p))) is { } a)
                Setzen(p, a, "reihenfolge");

        foreach (var p in offen.ToList())
        {
            if (!WeitereErlaubt.Contains(def.Name)) { z.Ohne.Add(p); continue; }
            var m = medium(p);
            var a = new Anschluss { Name = NeuerName(def, z.Anschluesse, m), Medium = m ?? "*", Fluss = fluss[p] ?? "beide" };
            z.Anschluesse.Add(a);
            z.Name[p.Port.EntityLabel] = a.Name; offen.Remove(p); art[p] = "weitere";
        }

        // Fliessrichtung an den zugeordneten Anschlüssen
        foreach (var (p, wie) in art)
        {
            var a = z.Anschluesse.First(x => x.Name == z.Name[p.Port.EntityLabel]);
            if (fluss.GetValueOrDefault(p) is { } f) a.Fluss = f;
            else if (wie is "medium" or "geometrie" or "weitere") { }
            else if (a.Fluss != "beide") { a.Fluss = "beide"; z.RichtungOffen++; }
        }
        // Nicht belegte Leitungsenden ohne bekannte Richtung: die Richtung des Katalogs ist dann ebenso offen
        if (leitung && art.Count > 0 && art.Keys.All(p => fluss.GetValueOrDefault(p) is null))
            foreach (var a in z.Anschluesse.Where(a => !z.Name.ContainsValue(a.Name) && a.Fluss != "beide")) { a.Fluss = "beide"; z.RichtungOffen++; }
        return z;
    }

    /// <summary>Name eines zusätzlichen Anschlusses: nach dem Muster der Katalogs-Anschlüsse gleichen Mediums
    /// (vl_1, vl_2 → vl_3), sonst abgang_n.</summary>
    static string NeuerName(KlassenDef def, List<Anschluss> vorhanden, string? medium)
    {
        var vorlage = def.Anschluesse.FirstOrDefault(a => medium is not null && a.Medium.Equals(medium, StringComparison.OrdinalIgnoreCase)
                                                          && System.Text.RegularExpressions.Regex.IsMatch(a.Name, @"_\d+$"));
        var stamm = vorlage is null ? "abgang" : vorlage.Name[..vorlage.Name.LastIndexOf('_')];
        for (int n = 1; ; n++)
            if (!vorhanden.Any(a => a.Name.Equals($"{stamm}_{n}", StringComparison.OrdinalIgnoreCase))) return $"{stamm}_{n}";
    }

    /// <summary>Abzweig eines T-Stücks: der Port, der nicht auf der gemeinsamen Achse der beiden anderen liegt.</summary>
    public static IfcHlksPort? Abzweig(IReadOnlyList<IfcHlksPort> p, double[]? mitte)
    {
        if (p.Count != 3) return null;
        double Gerade(int i, int j)
        {
            if (p[i].Achse is { } a && p[j].Achse is { } b) return Math.Abs(a[0] * b[0] + a[1] * b[1] + a[2] * b[2]);
            if (mitte is null) return double.NaN;
            return -Cos(Minus(p[i].Lage, mitte), Minus(p[j].Lage, mitte)) ?? double.NaN;
        }
        var paare = new[] { (0, 1, 2), (0, 2, 1), (1, 2, 0) }.Select(t => (Dritter: t.Item3, Wert: Gerade(t.Item1, t.Item2))).ToList();
        if (paare.Any(x => double.IsNaN(x.Wert))) return null;
        var best = paare.OrderByDescending(x => x.Wert).ToList();
        return best[0].Wert - best[1].Wert < 0.05 ? null : p[best[0].Dritter];
    }

    /// <summary>Umlenkwinkel (Grad) zwischen zwei Ports: 0 = gerade durch, 90 = rechtwinklig. Aus den Port-Achsen (Punkt,
    /// wo sie sich am nächsten kommen), sonst aus der Mitte des Elements; null, wenn es nicht bestimmbar ist.</summary>
    public static double? Umlenkwinkel(IfcHlksPort p1, IfcHlksPort p2, double[]? mitte)
    {
        double[]? m = null;
        if (p1.Achse is { } d1 && p2.Achse is { } d2)
        {
            double b = d1[0] * d2[0] + d1[1] * d2[1] + d1[2] * d2[2];
            if (1 - Math.Abs(b) < 1e-6) return 0;   // Achsen parallel: gerade
            var w0 = Minus(p1.Lage, p2.Lage);
            double dd = d1[0] * w0[0] + d1[1] * w0[1] + d1[2] * w0[2], e = d2[0] * w0[0] + d2[1] * w0[1] + d2[2] * w0[2];
            double n = 1 - b * b, s = (b * e - dd) / n, t = (e - b * dd) / n;
            m = [(p1.Lage[0] + s * d1[0] + p2.Lage[0] + t * d2[0]) / 2, (p1.Lage[1] + s * d1[1] + p2.Lage[1] + t * d2[1]) / 2, (p1.Lage[2] + s * d1[2] + p2.Lage[2] + t * d2[2]) / 2];
        }
        m ??= mitte;
        if (m is null) return null;
        var c = Cos(Minus(p1.Lage, m), Minus(p2.Lage, m));
        return c is null ? null : Math.Round(180 - Math.Acos(Math.Clamp(c.Value, -1, 1)) * 180 / Math.PI, 1);
    }

    static double[] Minus(double[] a, double[] b) => [a[0] - b[0], a[1] - b[1], a[2] - b[2]];

    static double? Cos(double[] a, double[] b)
    {
        double la = Math.Sqrt(a[0] * a[0] + a[1] * a[1] + a[2] * a[2]), lb = Math.Sqrt(b[0] * b[0] + b[1] * b[1] + b[2] * b[2]);
        return la < 1 || lb < 1 ? null : (a[0] * b[0] + a[1] * b[1] + a[2] * b[2]) / (la * lb);
    }
}
