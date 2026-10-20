// IFC-Export (Modul 7 Teil B2): Körper der Elemente als neutrale Bauteile für den Schreiber – alle Lagen in mm relativ zum
// Ursprung des Elements (seiner IfcLocalPlacement ohne Drehung).
//   Leitungen   je Teilstück eine Extrusion entlang der Achse: rund mit dem Aussendurchmesser, Kanal b × h (Breite waagrecht quer
//               zur Achse, Höhe senkrecht; senkrechte Stücke: b in x, h in y wie HlksGrafik); Masse aus derselben Quelle wie die
//               3D-Darstellung (HlksMasse: Serie bzw. Querschnitt), ohne Serie und ohne da DN-gerecht wie das 3D (max(DN, 30 mm), mit Hinweis).
//   Dämmung     eigene Geometrie (Entscheid A2): je Teilstück ein Hohlprofil (IfcCircleHollowProfileDef bzw. IfcRectangleHollowProfileDef)
//               aussen um die Leitung, Wanddicke = daemmdicke.
//   übrige      aus dem Körper der 3D-Darstellung (Grafik.QuelleFuer(...).Koerper – gleiche Quelle): Quader und Zylinder als Extrusion,
//               Prismen als Extrusion eines Polygons (mit Löchern), Kugeln und Netze als IfcFacetedBrep. Formteile, Armaturen und
//               Apparate ohne Familienkörper: Hüllquader des 3D-Ersatzkörpers (mit Hinweis), wie die Spezifikation es verlangt.

namespace Hlks.Kern;

/// <summary>Ein Körperteil der Ausgabe in Element-Koordinaten (mm).</summary>
public abstract record IfcExKoerper;

/// <summary>Rechteck B × H (zentriert in Lage, B entlang RefX, H entlang Achse × RefX), um Tiefe entlang Achse extrudiert;
/// Wand gesetzt = Hohlprofil mit dieser Wanddicke (Dämmung).</summary>
public sealed record IfcExQuader(double[] Lage, double[] Achse, double[] RefX, double B, double H, double Tiefe, double? Wand = null) : IfcExKoerper;

/// <summary>Kreis mit Radius R (Mitte in Lage), um Tiefe entlang Achse extrudiert; Wand gesetzt = Hohlprofil.</summary>
public sealed record IfcExZylinder(double[] Lage, double[] Achse, double R, double Tiefe, double? Wand = null) : IfcExKoerper;

/// <summary>Polygon (xy, mit Löchern) ab der Höhe Unten senkrecht um Hoehe extrudiert.</summary>
public sealed record IfcExPrisma(double Unten, List<double[]> Aussen, List<List<double[]>> Loecher, double Hoehe) : IfcExKoerper;

/// <summary>Geschlossenes Dreiecksnetz (aussen orientiert).</summary>
public sealed record IfcExNetz(List<double[]> Ecken, List<int[]> Dreiecke) : IfcExKoerper;

public static class IfcExportGeometrie
{
    /// <summary>Kleinster Rohrdurchmesser ohne Aussendurchmesser – wie das 3D (HlksKoerper.MinDurchmesser).</summary>
    public const double MinDurchmesser = HlksKoerper.MinDurchmesser;

    // ------------------------------------------------------------------ Vektoren

    public static double[] Minus(double[] a, double[] b) => [a[0] - b[0], a[1] - b[1], a[2] - b[2]];
    public static double Laenge(double[] v) => Math.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]);
    public static double[] Einheit(double[] v) { var l = Laenge(v); return l < 1e-12 ? [0, 0, 1] : [v[0] / l, v[1] / l, v[2] / l]; }
    public static double[] Kreuz(double[] a, double[] b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
    public static double Skalar(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
    public static double[] Neg(double[] a) => [-a[0], -a[1], -a[2]];

    /// <summary>Richtung quer zur Achse: waagrecht (Z × Achse); bei senkrechter Achse die x-Achse.</summary>
    public static double[] Quer(double[] achse)
    {
        var q = Kreuz([0, 0, 1], achse);
        return Laenge(q) < 1e-9 ? [1, 0, 0] : Einheit(q);
    }

    /// <summary>Eine Richtung senkrecht zu v (für Port-Platzierungen: Axis und RefDirection dürfen nicht parallel sein).</summary>
    public static double[] Senkrecht(double[] v) => Math.Abs(v[2]) < 0.9 ? Quer(v) : Einheit(Kreuz(v, [1, 0, 0]));

    // ------------------------------------------------------------------ Leitungen

    /// <summary>Querschnitt einer Leitung für die Ausgabe: rund (Durchmesser) oder Rechteck (b × h), mit Herkunft und Hinweis, wenn er nur
    /// DN-gerecht gezeichnet ist (wie das 3D, kein Aussendurchmesser bekannt).</summary>
    public sealed record Querschnitt(bool Rechteck, double B, double H, double Daemmdicke, string Herkunft, bool NurDarstellung);

    public static Querschnitt LeitungsQuerschnitt(Projekt p, Element e)
    {
        double daemm = e.Zahl("daemmdicke") is double dd && dd > 0 && double.IsFinite(dd) ? dd : 0;
        if (HlksMasse.Von(p, e) is { } m)
            return new Querschnitt(m.Rechteck, m.Breite, m.Hoehe, m.Daemmdicke,
                m.MitSerie ? $"Leitungsserie {m.Serie!.SerieName}, Typ {m.Serie.Typ}" : "Querschnitt der Leitung (breite/hoehe bzw. durchmesser)", false);
        if (e.Zahl("da") is double da && da > 0) return new Querschnitt(false, da, da, daemm, "Aussendurchmesser da", false);
        var d = Math.Max(HlksEinordnung.Dn(e) ?? 0, MinDurchmesser);
        return new Querschnitt(false, d, d, daemm, HlksEinordnung.Dn(e) is null ? "ohne Grösse: Darstellung Ø 30 mm wie das 3D" : "DN als Durchmesser wie das 3D (kein Aussendurchmesser)", true);
    }

    /// <summary>Teilstücke einer Leitung (Punkte relativ zum Ursprung = erster Punkt): Leitung und – mit Dämmung – das Hohlprofil darum.</summary>
    public static (List<IfcExKoerper> Leitung, List<IfcExKoerper> Daemmung) LeitungsKoerper(List<double[]> punkte, Querschnitt q)
    {
        var leitung = new List<IfcExKoerper>();
        var daemmung = new List<IfcExKoerper>();
        var o = punkte[0];
        for (int i = 1; i < punkte.Count; i++)
        {
            var d = Minus(punkte[i], punkte[i - 1]);
            var l = Laenge(d);
            if (l < 1e-3) continue;
            var achse = Einheit(d);
            var lage = Minus(punkte[i - 1], o);
            if (q.Rechteck)
            {
                var refx = Quer(achse);
                leitung.Add(new IfcExQuader(lage, achse, refx, q.B, q.H, l));
                if (q.Daemmdicke > 0) daemmung.Add(new IfcExQuader(lage, achse, refx, q.B + 2 * q.Daemmdicke, q.H + 2 * q.Daemmdicke, l, q.Daemmdicke));
            }
            else
            {
                leitung.Add(new IfcExZylinder(lage, achse, q.B / 2, l));
                if (q.Daemmdicke > 0) daemmung.Add(new IfcExZylinder(lage, achse, q.B / 2 + q.Daemmdicke, l, q.Daemmdicke));
            }
        }
        return (leitung, daemmung);
    }

    // ------------------------------------------------------------------ Körper der 3D-Darstellung

    /// <summary>Körperteile (Welt, mm) relativ zum Ursprung o. Netze von Unterlagen (Art «netz») entfallen.</summary>
    public static List<IfcExKoerper> AusKoerper(IEnumerable<Koerperteil> teile, double[] o)
    {
        var aus = new List<IfcExKoerper>();
        foreach (var t in teile)
        {
            switch (t.Art)
            {
                case "quader" when t.Mitte is { } m && t.Masse is { } s && s[0] > 0 && s[1] > 0 && s[2] > 0:
                {
                    double w = (t.Drehung ?? 0) * Math.PI / 180;
                    aus.Add(new IfcExQuader(Minus([m[0], m[1], m[2] - s[2] / 2], o), [0, 0, 1], [Math.Cos(w), Math.Sin(w), 0], s[0], s[1], s[2]));
                    break;
                }
                case "zylinder" when t.Von is { } a && t.Nach is { } b && t.Radius is double r && r > 0 && Laenge(Minus(b, a)) > 1e-6:
                    aus.Add(new IfcExZylinder(Minus(a, o), Einheit(Minus(b, a)), r, Laenge(Minus(b, a))));
                    break;
                case "prisma" when t.Grundriss is { Count: >= 3 } g && t.Unten is double u && t.Oben is double ob && ob - u > 1e-6:
                    aus.Add(new IfcExPrisma(u - o[2], g.Select(x => new[] { x[0] - o[0], x[1] - o[1] }).ToList(),
                        (t.Loecher ?? []).Where(l => l.Count >= 3).Select(l => l.Select(x => new[] { x[0] - o[0], x[1] - o[1] }).ToList()).ToList(), ob - u));
                    break;
                case "kugel" or "koerper":
                {
                    var n = FamilienKoerper.AlsNetz(t);
                    if (n.Dreiecke.Count == 0) break;
                    aus.Add(new IfcExNetz(n.Ecken.Select(x => Minus(x, o)).ToList(), n.Dreiecke));
                    break;
                }
            }
        }
        return aus;
    }

    /// <summary>Achsparalleler Hüllquader der Körperteile (Welt, mm) oder null.</summary>
    public static (double[] Min, double[] Max)? Huelle(IEnumerable<Koerperteil> teile)
    {
        double[] a = [double.MaxValue, double.MaxValue, double.MaxValue], b = [double.MinValue, double.MinValue, double.MinValue];
        bool etwas = false;
        void Dazu(double x, double y, double z)
        {
            a[0] = Math.Min(a[0], x); a[1] = Math.Min(a[1], y); a[2] = Math.Min(a[2], z);
            b[0] = Math.Max(b[0], x); b[1] = Math.Max(b[1], y); b[2] = Math.Max(b[2], z);
            etwas = true;
        }
        foreach (var t in teile)
        {
            switch (t.Art)
            {
                case "quader" when t.Mitte is { } m && t.Masse is { } s:
                {
                    double w = (t.Drehung ?? 0) * Math.PI / 180, c = Math.Cos(w), sn = Math.Sin(w);
                    foreach (var (dx, dy) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
                        foreach (var dz in new[] { -1, 1 })
                        {
                            double px = dx * s[0] / 2, py = dy * s[1] / 2;
                            Dazu(m[0] + px * c - py * sn, m[1] + px * sn + py * c, m[2] + dz * s[2] / 2);
                        }
                    break;
                }
                case "zylinder" when t.Von is { } v && t.Nach is { } n && t.Radius is double r:
                {
                    var d = Einheit(Minus(n, v));
                    var ext = d.Select(x => r * Math.Sqrt(Math.Max(0, 1 - x * x))).ToArray();
                    foreach (var q in new[] { v, n }) { Dazu(q[0] - ext[0], q[1] - ext[1], q[2] - ext[2]); Dazu(q[0] + ext[0], q[1] + ext[1], q[2] + ext[2]); }
                    break;
                }
                case "kugel" when t.Mitte is { } m && t.Radius is double r:
                    Dazu(m[0] - r, m[1] - r, m[2] - r); Dazu(m[0] + r, m[1] + r, m[2] + r);
                    break;
                case "koerper" when t.Ecken is { } ecken:
                    foreach (var q in ecken) Dazu(q[0], q[1], q[2]);
                    break;
                case "prisma" when t.Grundriss is { } g && t.Unten is double u && t.Oben is double ob:
                    foreach (var q in g) { Dazu(q[0], q[1], u); Dazu(q[0], q[1], ob); }
                    break;
            }
        }
        return etwas ? (a, b) : null;
    }

    /// <summary>Hüllquader als Extrusion relativ zum Ursprung o (Mindestmass 1 mm je Richtung).</summary>
    public static IfcExKoerper Huellquader((double[] Min, double[] Max) h, double[] o)
    {
        double bx = Math.Max(h.Max[0] - h.Min[0], 1), by = Math.Max(h.Max[1] - h.Min[1], 1), bz = Math.Max(h.Max[2] - h.Min[2], 1);
        double[] mitteUnten = [(h.Min[0] + h.Max[0]) / 2, (h.Min[1] + h.Max[1]) / 2, h.Min[2]];
        return new IfcExQuader(Minus(mitteUnten, o), [0, 0, 1], [1, 0, 0], bx, by, bz);
    }

    /// <summary>RepresentationType der Körperteile: nur Extrusionen «SweptSolid», nur Netze «Brep», gemischt «SolidModel».</summary>
    public static string Darstellungsart(IReadOnlyCollection<IfcExKoerper> k)
    {
        bool netz = k.Any(x => x is IfcExNetz), extrusion = k.Any(x => x is not IfcExNetz);
        return netz && extrusion ? "SolidModel" : netz ? "Brep" : "SweptSolid";
    }
}
