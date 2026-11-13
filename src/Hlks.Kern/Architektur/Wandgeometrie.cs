// Lage einer Wand und ihrer Öffnungen – gemeinsame Grundlage für Grundriss, Körper, Befehle und Nachführung.
// Wand = Achse Anfang → Ende (Wandmitte), Dicke und Höhe aus der Geometrie, z = Unterkante (entwurf/modul-1a.md).
// Örtliches System der Wand: s entlang der Achse ab Anfang, q quer dazu, positiv nach links (in Wandrichtung).
// Ecken und Anschlüsse: Ein Wandende, das eine andere Wand desselben Geschosses berührt (Abstand zur Achse der anderen
// Wand höchstens deren halbe Dicke + 1 mm), wird um die halbe Dicke der anderen Wand verlängert; die Vereinigung der
// Wandflächen bereinigt dann die Ecke (Spezifikation Modul 1a, Auftrag B1). Schräge Ecken: Verlängerung nach dem Winkel
// (Eckverlaengerung), damit das Wandende nicht über die Fläche der anderen Wand vorsteht.
// Entscheide Christian 13.11.2026: N5 – spitze Ecken (zwei Wandenden unter 90°) werden als echte Spitze geschlossen: das Wandende liegt auf der
// Gehrung von der Aussen- zur Innenecke der Wandflächen (GehrungAnfang/GehrungEnde, Band() als Viereck), vorher stumpf mit Fase.
// N6 – stösst ein Wandende rechtwinklig an die Fläche einer durchlaufenden Wand (T-Stoss, kein Eckpunkt der anderen Wand), endet die Wand an
// der zugewandten Fläche der anderen (vorher bis zu deren Achse verlängert, Spezifikation Modul 1a); liegt das Ende auf der Achse, wird gekürzt.

using Clipper2Lib;

namespace Hlks.Kern;

public sealed class Wandlage
{
    public required Element Wand { get; init; }
    public double Ax { get; init; }
    public double Ay { get; init; }
    public double Bx { get; init; }
    public double By { get; init; }
    public double Laenge { get; init; }
    /// <summary>Einheitsvektor Anfang → Ende.</summary>
    public double Ux { get; init; }
    public double Uy { get; init; }
    /// <summary>Wanddicke in mm (0 = unbekannt).</summary>
    public double Dicke { get; init; }
    /// <summary>Unterkante absolut (z über ±0.00).</summary>
    public double Unterkante { get; init; }
    /// <summary>Wandhöhe in mm; fehlt sie in der Geometrie, die Geschosshöhe (Annahme wie Vorgabe von wand_zeichnen).</summary>
    public double? Hoehe { get; init; }
    /// <summary>OK Boden des Geschosses absolut (Geschosskote); null = Wandunterkante.</summary>
    public double? Geschosskote { get; init; }
    /// <summary>OK Boden über der Wandunterkante: Bezug von Brüstung und Türunterkante. IFC-Wände beginnen oft unter dem
    /// Boden (Rohdecke, UK Decke darunter); Befund B1 vom 17.10.2026 – vorher galt die Brüstung ab Wandunterkante.</summary>
    public double Boden => (Geschosskote ?? Unterkante) - Unterkante;
    /// <summary>Verlängerung über den Anfang bzw. das Ende hinaus (halbe Dicke der berührten Wand), mm; negativ = gekürzt (T-Stoss, N6).</summary>
    public double VerlAnfang { get; set; }
    public double VerlEnde { get; set; }
    /// <summary>Spitze Ecke (N5): Lage s des Wandendes auf der linken (q = +Dicke/2) bzw. rechten Fläche; null = rechtwinkliges Ende.</summary>
    public (double Links, double Rechts)? GehrungAnfang { get; set; }
    public (double Links, double Rechts)? GehrungEnde { get; set; }

    /// <summary>Normale nach links (in Wandrichtung).</summary>
    public double Nx => -Uy;
    public double Ny => Ux;
    public double Oberkante => Unterkante + (Hoehe ?? 0);
    public double Von => -VerlAnfang;
    public double Bis => Laenge + VerlEnde;
    /// <summary>Freie Länge für Öffnungen: ohne die Bereiche, die in einer anschliessenden Wand liegen.</summary>
    public double FreiVon => Math.Max(Math.Abs(VerlAnfang), GehrungAnfang is { } g ? Math.Max(g.Links, g.Rechts) : 0);
    public double FreiBis => Math.Min(Laenge - Math.Abs(VerlEnde), GehrungEnde is { } g ? Math.Min(g.Links, g.Rechts) : double.MaxValue);
    /// <summary>Richtung der Achse in Grad, Gegenuhrzeigersinn ab +x.</summary>
    public double Winkel => Math.Atan2(Uy, Ux) * 180 / Math.PI;
    public string Id => Wand.Id;

    public double[] Punkt(double s, double q) => [Ax + Ux * s + Nx * q, Ay + Uy * s + Ny * q];
    public double Projektion(double x, double y) => (x - Ax) * Ux + (y - Ay) * Uy;
    public double Quer(double x, double y) => (x - Ax) * Nx + (y - Ay) * Ny;

    /// <summary>Wand ist im Grundriss geschnitten: Unterkante ≤ Schnitthöhe &lt; Oberkante (Höhe unbekannt: geschnitten).</summary>
    public bool Geschnitten(double schnitthoehe) => Unterkante <= schnitthoehe && (Hoehe is null || schnitthoehe < Oberkante);

    /// <summary>Rechteck s0…s1 × −q…+q im Gegenuhrzeigersinn.</summary>
    public PathD Rechteck(double s0, double s1, double q) => Rechteck(s0, s1, -q, q);

    public PathD Rechteck(double s0, double s1, double q0, double q1)
    {
        var a = Punkt(s0, q0); var b = Punkt(s1, q0); var c = Punkt(s1, q1); var d = Punkt(s0, q1);
        return Polygone.Ccw(new PathD { new(a[0], a[1]), new(b[0], b[1]), new(c[0], c[1]), new(d[0], d[1]) });
    }

    /// <summary>Wandfläche samt Verlängerungen (ohne Öffnungen).</summary>
    public PathD Band()
    {
        if (GehrungAnfang is null && GehrungEnde is null) return Rechteck(Von, Bis, Dicke / 2);
        double q = Dicke / 2;
        var (a0, a1) = GehrungAnfang ?? (Von, Von); var (e0, e1) = GehrungEnde ?? (Bis, Bis);
        var pkt = new[] { Punkt(a1, -q), Punkt(e1, -q), Punkt(e0, q), Punkt(a0, q) };
        return Polygone.Ccw(new PathD(pkt.Select(p => new PointD(p[0], p[1]))));
    }
}

/// <summary>Tür oder Fenster in einer Wand: Lage entlang der Achse, Breite, Höhenbereich relativ zur Wandunterkante.</summary>
public sealed record Oeffnungslage(Element Element, bool IstTuer, double Abstand, double Breite, double Unten, double? Oben)
{
    public double Von => Abstand - Breite / 2;
    public double Bis => Abstand + Breite / 2;
    /// <summary>Schneidet die Höhe (relativ zur Wandunterkante) die Öffnung? Höhe unbekannt: ja.</summary>
    public bool Schneidet(double zRelativ) => Unten <= zRelativ && (Oben is null || zRelativ < Oben);
}

public static class Wandgeometrie
{
    /// <summary>Wände, die (fast) parallel laufen, gelten nicht als Ecke und werden nicht verlängert
    /// (Annahme: |sin| &lt; 0.05, rund 3°; sonst stünde die Verlängerung bei ungleicher Dicke seitlich vor).</summary>
    const double Parallel = 0.05;

    /// <summary>Lage ohne Verlängerung; null, wenn die Wand keine Achse hat.</summary>
    public static Wandlage? Lage(Modell m, Element wand)
    {
        var g = wand.Geometrie;
        if (g is null || g.Punkte.Count < 2) return null;
        var a = g.Punkte[0]; var b = g.Punkte[^1];
        double dx = b[0] - a[0], dy = b[1] - a[1], l = Math.Sqrt(dx * dx + dy * dy);
        if (l < 1e-6) return null;
        var geschoss = m.Finde(wand.Geschoss);
        double z0 = geschoss?.Zahl("kote") ?? 0;
        double za = a.Length > 2 ? a[2] : z0, zb = b.Length > 2 ? b[2] : z0;
        return new Wandlage
        {
            Wand = wand, Ax = a[0], Ay = a[1], Bx = b[0], By = b[1], Laenge = l, Ux = dx / l, Uy = dy / l,
            Dicke = g.Dicke is double d && d > 0 ? d : 0,
            Unterkante = Math.Min(za, zb),
            Hoehe = g.Hoehe is double h && h > 0 ? h : geschoss?.Zahl("hoehe"),
            Geschosskote = geschoss?.Zahl("kote"),
        };
    }

    /// <summary>Alle Wände eines Geschosses mit Verlängerung an berührten Enden, nach Kennzeichen geordnet.</summary>
    public static List<Wandlage> DesGeschosses(Modell m, Katalog k, string? geschossId)
    {
        var liste = m.Elemente.Where(e => k.IstArt(e, "Wand") && e.Geschoss == geschossId)
            .OrderBy(e => e.Kennzeichen, StringComparer.OrdinalIgnoreCase).ThenBy(e => e.Id, StringComparer.Ordinal)
            .Select(e => Lage(m, e)).OfType<Wandlage>().ToList();
        Verlaengern(liste);
        return liste;
    }

    /// <summary>Lage einer Wand mit Verlängerung (Nachbarn aus demselben Geschoss).</summary>
    public static Wandlage? MitVerlaengerung(Modell m, Katalog k, Element wand)
    {
        if (Lauf.Aktuell is { } l && ReferenceEquals(l.Modell, m))
        {
            if (!l.Geschosse.TryGetValue(wand.Geschoss ?? "", out var je))
                l.Geschosse[wand.Geschoss ?? ""] = je = DesGeschosses(m, k, wand.Geschoss).ToDictionary(w => w.Id, StringComparer.Ordinal);
            return je.GetValueOrDefault(wand.Id);
        }
        return DesGeschosses(m, k, wand.Geschoss).FirstOrDefault(w => w.Id == wand.Id);
    }

    /// <summary>Zwischenspeicher für einen Lauf über viele Elemente desselben, unveränderten Modells (Grafik.Koerper):
    /// Wandlagen je Geschoss und Öffnungen je Wand werden einmal gerechnet statt je Element. Befund B1 vom 17.10.2026:
    /// BKW (2412 Wände) brauchte 32.5 s. Gilt nur im Thread und bis Dispose; ändert sich das Modell dazwischen, ist der
    /// Aufrufer schuld – nur um reine Leseläufe legen.</summary>
    public static IDisposable Zwischenspeichern(Modell m, Katalog k)
    {
        if (Lauf.Aktuell is not null) return new Lauf(null, k);   // verschachtelt: der äussere Lauf gilt
        return Lauf.Aktuell = new Lauf(m, k);
    }

    /// <summary>Weiterer Wert im Zwischenspeicher des laufenden Leselaufs (z. B. die Decken für die Wandkörper, Auftrag B6):
    /// je Schlüssel einmal gerechnet, solange Zwischenspeichern für dieses Modell gilt; ohne Lauf jedes Mal neu.</summary>
    public static T Zwischengespeichert<T>(Modell m, string schluessel, Func<T> rechnen) where T : class
    {
        if (Lauf.Aktuell is not { } l || !ReferenceEquals(l.Modell, m)) return rechnen();
        if (l.Weitere.TryGetValue(schluessel, out var o) && o is T t) return t;
        var neu = rechnen();
        l.Weitere[schluessel] = neu;
        return neu;
    }

    sealed class Lauf(Modell? m, Katalog k) : IDisposable
    {
        [ThreadStatic] internal static Lauf? Aktuell;
        internal Modell? Modell { get; } = m;
        internal Dictionary<string, object> Weitere { get; } = new(StringComparer.Ordinal);
        internal Dictionary<string, Dictionary<string, Wandlage>> Geschosse { get; } = new(StringComparer.Ordinal);
        Dictionary<string, List<Element>>? oeffnungen;
        internal Dictionary<string, List<Element>> Oeffnungen => oeffnungen ??= OeffnungenJeWand(Modell!, k);
        public void Dispose() { if (Modell is not null && ReferenceEquals(Aktuell, this)) Aktuell = null; }
    }

    /// <summary>Ab so vielen Wänden sucht Verlaengern die berührten Wände über ein Raster statt alle Paare zu prüfen
    /// (Befund B1 Modul 1c, 18.10.2026: BKW mit 2412 Wänden brauchte 0.8 s für alle Paare).</summary>
    const int RasterAb = 64;
    /// <summary>Weite einer Rasterzelle in mm (Annahme; beeinflusst nur die Rechenzeit, nicht das Ergebnis).</summary>
    const double Rasterweite = 2000;
    /// <summary>Wände, deren Rechteck mehr Zellen belegt, werden ohne Raster gerechnet (Schutz vor Ausreissern).</summary>
    const long MaxZellen = 100_000;

    /// <summary>Verlängerung an berührten Enden (Regel siehe Kopf der Datei). raster = false prüft alle Paare (Vergleich in den
    /// Tests). Mit Raster genau dasselbe Ergebnis: Ein Punkt, der höchstens Dicke/2 + 1 mm von der Achse einer Wand liegt, liegt
    /// im Rechteck um diese Achse, vergrössert um Dicke/2 + 1 mm – und damit in einer der Zellen, in die die Wand eingetragen ist.</summary>
    public static void Verlaengern(List<Wandlage> waende, bool raster = true)
    {
        Dictionary<(long, long), List<Wandlage>>? zellen = raster && waende.Count >= RasterAb ? Rastern(waende) : null;
        foreach (var w in waende)
        {
            // aus dem IFC festgehaltene Enden (verlAnfang/verlEnde, 02.11.2026: deckungsgleich mit dem IFC) gelten vor der Regel
            var na = zellen is null ? waende : zellen.GetValueOrDefault((Zelle(w.Ax), Zelle(w.Ay))) ?? [];
            var nb = zellen is null ? waende : zellen.GetValueOrDefault((Zelle(w.Bx), Zelle(w.By))) ?? [];
            w.GehrungAnfang = w.GehrungEnde = null;
            if (w.Wand.Zahl("verlAnfang") is double va) w.VerlAnfang = va;
            else if (Spitze(w, w.Ax, w.Ay, -1, na) is { } ga) { w.GehrungAnfang = ga; w.VerlAnfang = -Math.Min(ga.Links, ga.Rechts); }
            else w.VerlAnfang = Verlaengerung(w, w.Ax, w.Ay, -1, na);
            if (w.Wand.Zahl("verlEnde") is double ve) w.VerlEnde = ve;
            else if (Spitze(w, w.Bx, w.By, 1, nb) is { } ge) { w.GehrungEnde = ge; w.VerlEnde = Math.Max(ge.Links, ge.Rechts) - w.Laenge; }
            else w.VerlEnde = Verlaengerung(w, w.Bx, w.By, 1, nb);
        }
    }

    static long Zelle(double v) => (long)Math.Floor(v / Rasterweite);

    /// <summary>Wände mit Dicke je Rasterzelle ihres Rechtecks (Achse ± Dicke/2 + 1 mm); null, wenn eine Wand zu viele Zellen belegt.</summary>
    static Dictionary<(long, long), List<Wandlage>>? Rastern(List<Wandlage> waende)
    {
        var zellen = new Dictionary<(long, long), List<Wandlage>>();
        foreach (var o in waende)
        {
            if (o.Dicke <= 0) continue;
            double r = o.Dicke / 2 + 1;
            long x0 = Zelle(Math.Min(o.Ax, o.Bx) - r), x1 = Zelle(Math.Max(o.Ax, o.Bx) + r);
            long y0 = Zelle(Math.Min(o.Ay, o.By) - r), y1 = Zelle(Math.Max(o.Ay, o.By) + r);
            if ((x1 - x0 + 1) * (y1 - y0 + 1) > MaxZellen) return null;
            for (long x = x0; x <= x1; x++)
                for (long y = y0; y <= y1; y++)
                    (zellen.TryGetValue((x, y), out var l) ? l : zellen[(x, y)] = []).Add(o);
        }
        return zellen;
    }

    /// <summary>richtung: +1 am Wandende, −1 am Wandanfang (Richtung, in die das Ende verlängert wird).</summary>
    /// <summary>Spitze Ecke (N5): das Wandende trifft ein Ende einer anderen Wand unter weniger als 90° (Innenwinkel). Ergebnis: s der Gehrung auf
    /// der linken und rechten Fläche – von der Aussenecke (Schnitt der äusseren Flächen) zur Innenecke (Schnitt der inneren); null sonst.</summary>
    static (double Links, double Rechts)? Spitze(Wandlage w, double px, double py, double richtung, List<Wandlage> waende)
    {
        if (w.Dicke <= 0) return null;
        foreach (var o in waende)
        {
            if (o.Id == w.Id || o.Dicke <= 0) continue;
            double sin = w.Ux * o.Uy - w.Uy * o.Ux;
            if (Math.Abs(sin) < Parallel) continue;
            // wie Verlaengerung: das Ende berührt die andere Wand (bis Dicke/2 + 1 mm von deren Achse) – damit gilt dasselbe Raster
            if (Polygone.AbstandZurStrecke(px, py, o.Ax, o.Ay, o.Bx, o.By) > o.Dicke / 2 + 1) continue;
            double grenze = (o.Dicke + w.Dicke) / 2 + 1;
            double dA = Math.Sqrt(Math.Pow(px - o.Ax, 2) + Math.Pow(py - o.Ay, 2)), dB = Math.Sqrt(Math.Pow(px - o.Bx, 2) + Math.Pow(py - o.By, 2));
            if (Math.Min(dA, dB) > grenze) continue;
            // Richtungen von der Ecke weg: w zurück zu seinem anderen Ende, o zu seinem anderen Ende
            double awx = -richtung * w.Ux, awy = -richtung * w.Uy;
            double aox = dA <= dB ? o.Ux : -o.Ux, aoy = dA <= dB ? o.Uy : -o.Uy;
            if (awx * aox + awy * aoy <= 1e-6) continue;   // 90° und mehr: wie bisher (Verlängerung, stumpfe Ecken auf Gehrung)
            // s auf der Fläche q = qw von w, wo sie die Fläche q = qo von o schneidet
            double S(double qw, double qo)
            {
                double x0 = w.Ax + w.Nx * qw, y0 = w.Ay + w.Ny * qw;
                return ((o.Ax + o.Nx * qo - x0) * o.Uy - (o.Ay + o.Ny * qo - y0) * o.Ux) / (w.Ux * o.Uy - w.Uy * o.Ux);
            }
            double hw = w.Dicke / 2, ho = o.Dicke / 2;
            var kand = new[] { (q: hw, s: S(hw, ho)), (q: hw, s: S(hw, -ho)), (q: -hw, s: S(-hw, ho)), (q: -hw, s: S(-hw, -ho)) };
            var aussen = richtung > 0 ? kand.MaxBy(x => x.s) : kand.MinBy(x => x.s);
            var innen = richtung > 0 ? kand.MinBy(x => x.s) : kand.MaxBy(x => x.s);
            if (aussen.q == innen.q || !double.IsFinite(aussen.s) || !double.IsFinite(innen.s)) continue;
            return aussen.q > 0 ? (aussen.s, innen.s) : (innen.s, aussen.s);
        }
        return null;
    }

    static double Verlaengerung(Wandlage w, double px, double py, double richtung, List<Wandlage> waende)
    {
        double e = 0;
        double? tStoss = null;
        foreach (var o in waende)
        {
            if (o.Id == w.Id || o.Dicke <= 0) continue;
            double sin = Math.Abs(w.Ux * o.Uy - w.Uy * o.Ux);
            if (sin < Parallel) continue;
            double d = Polygone.AbstandZurStrecke(px, py, o.Ax, o.Ay, o.Bx, o.By);
            if (d > o.Dicke / 2 + 1) continue;
            // Weg quer zur anderen Wand, der dem Wandende bis zu deren Achse noch fehlt (negativ: das Ende liegt schon dahinter)
            double quer = (px - o.Ax) * o.Nx + (py - o.Ay) * o.Ny;
            double fehlt = richtung * (w.Ux * o.Nx + w.Uy * o.Ny) >= 0 ? -quer : quer;
            double cos = Math.Abs(w.Ux * o.Ux + w.Uy * o.Uy);
            // T-Stoss rechtwinklig an eine durchlaufende Wand (kein Ende von o in der Nähe, N6): Ende an der zugewandten Fläche von o
            double grenze = (o.Dicke + w.Dicke) / 2 + 1;
            bool anEnde = Math.Sqrt(Math.Pow(px - o.Ax, 2) + Math.Pow(py - o.Ay, 2)) <= grenze || Math.Sqrt(Math.Pow(px - o.Bx, 2) + Math.Pow(py - o.By, 2)) <= grenze;
            if (cos < 1e-9 && !anEnde) { tStoss = Math.Min(tStoss ?? double.MaxValue, fehlt - o.Dicke / 2); continue; }
            e = Math.Max(e, Eckverlaengerung(w.Dicke, o.Dicke, sin, cos, fehlt));
        }
        return e > 0 || tStoss is null ? e : tStoss.Value;
    }

    /// <summary>Verlängerung eines Wandendes an einer anderen Wand: so weit, dass die vordere Ecke des Wandendes genau auf der
    /// abgewandten Fläche der anderen Wand liegt – (Dicke der anderen / 2 + abstand − eigene Dicke / 2 · |cos|) / |sin|, nie negativ.
    /// abstand = Weg quer zur anderen Wand, der dem Wandende bis zu deren Achse fehlt (0 = Ende auf der Achse, negativ = dahinter).
    /// Rechtwinklig (cos = 0) bleibt es bei der halben Dicke der anderen Wand (Spezifikation Modul 1a); das Wandende liegt dort in
    /// jedem Fall ganz in der anderen Wand. Bei schrägen Ecken stand die Wand mit der festen halben Dicke über die Fläche der anderen
    /// vor (Gesamtprüfung 13.11.2026: 135°-Ecke zweier 200-mm-Wände → Zacken von 59 und 41 mm im Wandumriss; nötig sind 41.4 mm je
    /// Wand, die Ecke wird dann sauber auf Gehrung geschlossen). Ohne den Abstand blieb ein schräges Wandende, das auf der Fläche statt
    /// auf der Achse der anderen Wand endet, offen (Gegenprüfung B1: 45°, Ende auf der Fläche → 41.4 statt 182.8 mm, Kerbe im Umriss).</summary>
    internal static double Eckverlaengerung(double eigeneDicke, double andereDicke, double sin, double cos, double abstand = 0) =>
        cos < 1e-9 ? andereDicke / 2 : Math.Max(0, (andereDicke / 2 + abstand - eigeneDicke / 2 * cos) / sin);

    public static bool IstOeffnung(Katalog k, Element e) => k.IstArt(e, "Tuer") || k.IstArt(e, "Fenster");

    /// <summary>Wand-Id der Öffnung (bezuege.wand) oder null.</summary>
    public static string? WandVon(Element oeffnung) => oeffnung.Bezuege.TryGetValue("wand", out var w) ? w : null;

    /// <summary>Türen und Fenster mit bezuege.wand = Wand.</summary>
    public static IEnumerable<Element> OeffnungenVon(Modell m, Katalog k, string wandId) =>
        Lauf.Aktuell is { } l && ReferenceEquals(l.Modell, m)
            ? l.Oeffnungen.GetValueOrDefault(wandId) ?? []
            : m.Elemente.Where(e => IstOeffnung(k, e) && WandVon(e) == wandId);

    /// <summary>Öffnungen je Wand-Id (einmal über alle Elemente).</summary>
    public static Dictionary<string, List<Element>> OeffnungenJeWand(Modell m, Katalog k)
    {
        var d = new Dictionary<string, List<Element>>(StringComparer.Ordinal);
        foreach (var e in m.Elemente)
            if (IstOeffnung(k, e) && WandVon(e) is { } w)
                (d.TryGetValue(w, out var l) ? l : d[w] = []).Add(e);
        return d;
    }

    /// <summary>Lage einer Öffnung in ihrer Wand; null ohne Breite oder ohne Lage. Abstand ist führend, sonst Projektion
    /// des Punkts. Tür: OK Boden … + hoehe; Fenster: OK Boden + bruestung … + hoehe. Ergebnis relativ zur Wandunterkante.</summary>
    public static Oeffnungslage? Oeffnung(Katalog k, Element o, Wandlage w)
    {
        if (o.Zahl("breite") is not double b || b <= 0) return null;
        double? abstand = o.Zahl("abstand");
        if (abstand is null && o.Geometrie?.Punkte.FirstOrDefault() is { } p) abstand = w.Projektion(p[0], p[1]);
        if (abstand is not double a) return null;
        bool tuer = k.IstArt(o, "Tuer");
        double unten = Unterkante(w, tuer, o.Zahl("bruestung"));
        double? oben = o.Zahl("hoehe") is double h && h > 0 ? unten + h : null;
        return new Oeffnungslage(o, tuer, a, b, unten, oben);
    }

    /// <summary>Unterkante einer Öffnung relativ zur Wandunterkante: OK Boden + Brüstung (Tür: Schwelle; fehlt = 0). Seit 02.11.2026 auch
    /// negativ und unter der Wand (Christian: «nach dem import müssen die beiden geometrien deckungsgleich sein» – Archicad-Fenstertüren
    /// reichen unter OK Boden); der Wandkörper zieht nur ab, was in der Wand liegt.</summary>
    public static double Unterkante(Wandlage w, bool tuer, double? bruestung) => w.Boden + (bruestung ?? 0);

    public static List<Oeffnungslage> Oeffnungen(Katalog k, Wandlage w, IEnumerable<Element> oeffnungen) =>
        oeffnungen.Select(o => Oeffnung(k, o, w)).OfType<Oeffnungslage>().OrderBy(o => o.Von).ToList();

    /// <summary>Warum die Öffnung nicht in die Wand passt (Länge, Höhe, Überschneidung) – oder null, wenn sie passt.</summary>
    public static string? PasstNicht(Wandlage w, Oeffnungslage o, IEnumerable<Oeffnungslage> andere)
    {
        const double eps = 1e-6;
        string kz = w.Wand.Kennzeichen;
        if (o.Von < w.FreiVon - eps || o.Bis > w.FreiBis + eps)
            return $"{Mm(o.Breite)} mm breit bei Abstand {Mm(o.Abstand)} mm reicht über die freie Länge von {kz} hinaus " +
                   $"(frei {Mm(w.FreiVon)} … {Mm(w.FreiBis)} mm ab Wandanfang, Öffnung {Mm(o.Von)} … {Mm(o.Bis)} mm)";
        if (w.Hoehe is double h && o.Oben is double ob && ob > h + eps)
            return $"Oberkante {Mm(ob)} mm über Wandunterkante liegt über der Wandhöhe {Mm(h)} mm von {kz}";
        foreach (var x in andere)
        {
            if (x.Element.Id == o.Element.Id) continue;
            bool laengs = o.Von < x.Bis - eps && x.Von < o.Bis - eps;
            bool hoch = o.Unten < (x.Oben ?? double.MaxValue) - eps && x.Unten < (o.Oben ?? double.MaxValue) - eps;
            if (laengs && hoch)
                return $"überschneidet {x.Element.Kennzeichen} ({Mm(x.Von)} … {Mm(x.Bis)} mm ab Anfang von {kz})";
        }
        return null;
    }

    /// <summary>Freie Abschnitte der Achse (ohne die anderen Öffnungen), für Vorschläge.</summary>
    public static string FreieAbschnitte(Wandlage w, IEnumerable<Oeffnungslage> andere)
    {
        var teile = new List<string>();
        double von = w.FreiVon;
        foreach (var x in andere.OrderBy(x => x.Von))
        {
            if (x.Von > von + 1e-6) teile.Add($"{Mm(von)} … {Mm(Math.Min(x.Von, w.FreiBis))}");
            von = Math.Max(von, x.Bis);
        }
        if (w.FreiBis > von + 1e-6) teile.Add($"{Mm(von)} … {Mm(w.FreiBis)}");
        return teile.Count == 0 ? "keine" : string.Join(", ", teile) + " mm ab Wandanfang";
    }

    public static string Mm(double x) => Grafik.Format(Math.Round(x, 1), Math.Abs(x - Math.Round(x)) < 0.05 ? 0 : 1);
}
