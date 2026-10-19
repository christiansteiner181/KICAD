// Lage einer Wand und ihrer Öffnungen – gemeinsame Grundlage für Grundriss, Körper, Befehle und Nachführung.
// Wand = Achse Anfang → Ende (Wandmitte), Dicke und Höhe aus der Geometrie, z = Unterkante (entwurf/modul-1a.md).
// Örtliches System der Wand: s entlang der Achse ab Anfang, q quer dazu, positiv nach links (in Wandrichtung).
// Ecken und Anschlüsse: Ein Wandende, das eine andere Wand desselben Geschosses berührt (Abstand zur Achse der anderen
// Wand höchstens deren halbe Dicke + 1 mm), wird um die halbe Dicke der anderen Wand verlängert; die Vereinigung der
// Wandflächen bereinigt dann die Ecke (Spezifikation Modul 1a, Auftrag B1).

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
    /// <summary>Verlängerung über den Anfang bzw. das Ende hinaus (halbe Dicke der berührten Wand), mm.</summary>
    public double VerlAnfang { get; set; }
    public double VerlEnde { get; set; }

    /// <summary>Normale nach links (in Wandrichtung).</summary>
    public double Nx => -Uy;
    public double Ny => Ux;
    public double Oberkante => Unterkante + (Hoehe ?? 0);
    public double Von => -VerlAnfang;
    public double Bis => Laenge + VerlEnde;
    /// <summary>Freie Länge für Öffnungen: ohne die Bereiche, die in einer anschliessenden Wand liegen.</summary>
    public double FreiVon => VerlAnfang;
    public double FreiBis => Laenge - VerlEnde;
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
    public PathD Band() => Rechteck(Von, Bis, Dicke / 2);
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
            w.VerlAnfang = Verlaengerung(w, w.Ax, w.Ay, zellen is null ? waende : zellen.GetValueOrDefault((Zelle(w.Ax), Zelle(w.Ay))) ?? []);
            w.VerlEnde = Verlaengerung(w, w.Bx, w.By, zellen is null ? waende : zellen.GetValueOrDefault((Zelle(w.Bx), Zelle(w.By))) ?? []);
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

    static double Verlaengerung(Wandlage w, double px, double py, List<Wandlage> waende)
    {
        double e = 0;
        foreach (var o in waende)
        {
            if (o.Id == w.Id || o.Dicke <= 0) continue;
            if (Math.Abs(w.Ux * o.Uy - w.Uy * o.Ux) < Parallel) continue;
            double d = Polygone.AbstandZurStrecke(px, py, o.Ax, o.Ay, o.Bx, o.By);
            if (d <= o.Dicke / 2 + 1) e = Math.Max(e, o.Dicke / 2);
        }
        return e;
    }

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

    /// <summary>Unterkante einer Öffnung relativ zur Wandunterkante: Tür ab OK Boden, Fenster ab OK Boden + Brüstung
    /// (Annahme: Fenster ohne Brüstung ab OK Boden); nie unter der Wand.</summary>
    public static double Unterkante(Wandlage w, bool tuer, double? bruestung) =>
        Math.Max(0, w.Boden + (tuer ? 0 : Math.Max(0, bruestung ?? 0)));

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
