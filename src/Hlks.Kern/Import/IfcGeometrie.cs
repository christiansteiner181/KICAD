// IFC-Geometrie (Modul 1b, Auftrag B1): aus der Körper-Repräsentation eines Produkts ein Dreiecksnetz in
// Projektkoordinaten (mm) – Grundlage der Netzdatei (Kollision, Aussparungen, Hintergrund) und der vereinfachten
// KICAD-Geometrie (Achse, Dicke, Höhe, Umriss). Eigene Auswertung ohne Geometrie-Bibliothek (xBIM Essentials liest nur):
//   IfcExtrudedAreaSolid (Profile siehe IfcGeometrieKurven.cs), IfcRevolvedAreaSolid, IfcSweptDiskSolid,
//   IfcFacetedBrep(WithVoids), IfcFaceBasedSurfaceModel, IfcShellBasedSurfaceModel, IfcPolygonalFaceSet,
//   IfcTriangulatedFaceSet, IfcMappedItem (IfcCartesianTransformationOperator), IfcBooleanResult/IfcBooleanClippingResult
//   (erster Operand, bei UNION beide: «vereinfacht»), IfcCsgSolid mit Quader, Zylinder, Kegel, Kugel, Pyramide,
//   IfcBoundingBox.
// Unbekanntes ergibt kein Netz und wird je Entität gezählt (Unbekannt). Ergebnisse werden je Datei zwischengespeichert,
// damit der Übertrag und die Netzdatei dasselbe Netz nur einmal rechnen; die Netzdatei nimmt sie heraus (kein Netz
// doppelt im Speicher).

using System.Runtime.CompilerServices;
using Xbim.Common;
using Xbim.Ifc4.Interfaces;

namespace Hlks.Kern;

/// <summary>Affine Abbildung p' = A·p + t (3×3 und Verschiebung), für Lagen und Abbildungen (MappedItem) der Geometrie.</summary>
public readonly struct Affin
{
    public readonly double M00, M01, M02, T0, M10, M11, M12, T1, M20, M21, M22, T2;

    public Affin(double m00, double m01, double m02, double t0, double m10, double m11, double m12, double t1, double m20, double m21, double m22, double t2)
    {
        M00 = m00; M01 = m01; M02 = m02; T0 = t0; M10 = m10; M11 = m11; M12 = m12; T1 = t1; M20 = m20; M21 = m21; M22 = m22; T2 = t2;
    }

    public static readonly Affin Einheit = new(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0);

    public static Affin Aus(Matrix4 m) => new(m[0, 0], m[0, 1], m[0, 2], m[0, 3], m[1, 0], m[1, 1], m[1, 2], m[1, 3], m[2, 0], m[2, 1], m[2, 2], m[2, 3]);

    /// <summary>Spalten x, y, z und Ursprung o.</summary>
    public static Affin Spalten(double[] x, double[] y, double[] z, double[] o) => new(x[0], y[0], z[0], o[0], x[1], y[1], z[1], o[1], x[2], y[2], z[2], o[2]);

    public static Affin Verschiebung(double x, double y, double z) => new(1, 0, 0, x, 0, 1, 0, y, 0, 0, 1, z);

    /// <summary>a ∘ b: zuerst b, dann a.</summary>
    public static Affin operator *(Affin a, Affin b) => new(
        a.M00 * b.M00 + a.M01 * b.M10 + a.M02 * b.M20, a.M00 * b.M01 + a.M01 * b.M11 + a.M02 * b.M21, a.M00 * b.M02 + a.M01 * b.M12 + a.M02 * b.M22, a.M00 * b.T0 + a.M01 * b.T1 + a.M02 * b.T2 + a.T0,
        a.M10 * b.M00 + a.M11 * b.M10 + a.M12 * b.M20, a.M10 * b.M01 + a.M11 * b.M11 + a.M12 * b.M21, a.M10 * b.M02 + a.M11 * b.M12 + a.M12 * b.M22, a.M10 * b.T0 + a.M11 * b.T1 + a.M12 * b.T2 + a.T1,
        a.M20 * b.M00 + a.M21 * b.M10 + a.M22 * b.M20, a.M20 * b.M01 + a.M21 * b.M11 + a.M22 * b.M21, a.M20 * b.M02 + a.M21 * b.M12 + a.M22 * b.M22, a.M20 * b.T0 + a.M21 * b.T1 + a.M22 * b.T2 + a.T2);

    public (double X, double Y, double Z) Punkt(double x, double y, double z) =>
        (M00 * x + M01 * y + M02 * z + T0, M10 * x + M11 * y + M12 * z + T1, M20 * x + M21 * y + M22 * z + T2);

    public (double X, double Y, double Z) Richtung(double x, double y, double z) =>
        (M00 * x + M01 * y + M02 * z, M10 * x + M11 * y + M12 * z, M20 * x + M21 * y + M22 * z);

    public double[] Punkt(double[] p) { var (x, y, z) = Punkt(p[0], p.Length > 1 ? p[1] : 0, p.Length > 2 ? p[2] : 0); return [x, y, z]; }

    /// <summary>Determinante des 3×3-Teils; negativ = gespiegelt (Dreiecke umdrehen).</summary>
    public double Det => M00 * (M11 * M22 - M12 * M21) - M01 * (M10 * M22 - M12 * M20) + M02 * (M10 * M21 - M11 * M20);

    public double[] AchseX => [M00, M10, M20];
    public double[] AchseY => [M01, M11, M21];
    public double[] AchseZ => [M02, M12, M22];
    public double[] Ursprung => [T0, T1, T2];
}

/// <summary>Dreiecksnetz in mm: Punkte (x, y, z hintereinander) und Dreiecke (drei Punktindizes, 0-basiert).</summary>
public sealed class Dreiecksnetz
{
    public List<double> P { get; } = [];
    public List<int> D { get; } = [];
    public int Punkte => P.Count / 3;
    public int Dreiecke => D.Count / 3;
    public bool Leer => D.Count == 0;

    public int Punkt(double x, double y, double z)
    {
        P.Add(x); P.Add(y); P.Add(z);
        return P.Count / 3 - 1;
    }

    public void Dreieck(int a, int b, int c)
    {
        if (a == b || b == c || a == c) return;
        D.Add(a); D.Add(b); D.Add(c);
    }

    public (double X, double Y, double Z) Lage(int i) => (P[3 * i], P[3 * i + 1], P[3 * i + 2]);

    /// <summary>Hängt ein Netz an, abgebildet mit t (gespiegelte Abbildung dreht die Dreiecke um).</summary>
    public void Anhaengen(Dreiecksnetz n, in Affin t)
    {
        int basis = Punkte;
        for (int i = 0; i < n.P.Count; i += 3)
        {
            var (x, y, z) = t.Punkt(n.P[i], n.P[i + 1], n.P[i + 2]);
            P.Add(x); P.Add(y); P.Add(z);
        }
        bool drehen = t.Det < 0;
        for (int i = 0; i < n.D.Count; i += 3)
        {
            if (drehen) { D.Add(basis + n.D[i]); D.Add(basis + n.D[i + 2]); D.Add(basis + n.D[i + 1]); }
            else { D.Add(basis + n.D[i]); D.Add(basis + n.D[i + 1]); D.Add(basis + n.D[i + 2]); }
        }
    }

    public void Anhaengen(Dreiecksnetz n) => Anhaengen(n, Affin.Einheit);

    public Dreiecksnetz Abgebildet(in Affin t)
    {
        var aus = new Dreiecksnetz();
        aus.Anhaengen(this, t);
        return aus;
    }

    /// <summary>[minx, miny, minz, maxx, maxy, maxz] oder null, wenn leer.</summary>
    public double[]? Begrenzung()
    {
        if (P.Count == 0) return null;
        double[] b = [double.MaxValue, double.MaxValue, double.MaxValue, double.MinValue, double.MinValue, double.MinValue];
        for (int i = 0; i < P.Count; i += 3)
            for (int k = 0; k < 3; k++)
            {
                if (P[i + k] < b[k]) b[k] = P[i + k];
                if (P[i + k] > b[k + 3]) b[k + 3] = P[i + k];
            }
        return b;
    }

    /// <summary>Kleinster und grösster Wert der Punkte in Richtung (ux, uy) ab dem Punkt (ox, oy) – Lage längs bzw. quer zu einer Achse.</summary>
    public (double Min, double Max) Bereich(double ox, double oy, double ux, double uy)
    {
        double mi = double.MaxValue, ma = double.MinValue;
        for (int i = 0; i < P.Count; i += 3)
        {
            double s = (P[i] - ox) * ux + (P[i + 1] - oy) * uy;
            if (s < mi) mi = s;
            if (s > ma) ma = s;
        }
        return (mi, ma);
    }
}

/// <summary>Profilfläche einer Extrusion: Aussenring im Gegenuhrzeigersinn, Löcher im Uhrzeigersinn (2D, mm).</summary>
public sealed record Profilflaeche(List<double[]> Aussen, List<List<double[]>> Loecher);

/// <summary>Körper aus einer einzigen Extrusion in Projektkoordinaten: Profilflächen auf der Grundebene (3D-Punkte), Vektor
/// der Extrusion (mm). Vereinfacht = aus dem ersten Operanden eines Booleans.</summary>
public sealed record Extrusionsform(List<List<double[]>> Aussen, List<List<double[]>> Loecher, double[] Vektor, bool Vereinfacht)
{
    /// <summary>Senkrecht: Extrusion parallel zu z (Abweichung unter rund 0.6°) und Profil waagrecht.</summary>
    public bool Senkrecht
    {
        get
        {
            double l = Math.Sqrt(Vektor[0] * Vektor[0] + Vektor[1] * Vektor[1] + Vektor[2] * Vektor[2]);
            if (l < 1e-9 || Math.Abs(Vektor[2]) / l < 0.99995) return false;
            double z0 = Aussen.SelectMany(r => r).Min(p => p[2]), z1 = Aussen.SelectMany(r => r).Max(p => p[2]);
            return z1 - z0 < 1;
        }
    }
    public double Unten => Math.Min(Aussen.SelectMany(r => r).Min(p => p[2]), Aussen.SelectMany(r => r).Min(p => p[2]) + Vektor[2]);
    public double Hoehe => Math.Abs(Vektor[2]);
}

public sealed partial class IfcGeometrie
{
    static readonly ConditionalWeakTable<IfcDatei, IfcGeometrie> alle = new();

    /// <summary>Geometrie-Auswertung einer geöffneten Datei (eine je Datei, mit Zwischenspeicher).</summary>
    public static IfcGeometrie Von(IfcDatei d) => alle.GetValue(d, x => new IfcGeometrie(x));

    /// <summary>Repräsentationen, die als Körper gelten (in dieser Reihenfolge gesucht).</summary>
    public static readonly string[] KoerperKennungen = ["Body", "Facetation", "Body-FallBack", "Mesh"];

    public IfcDatei Datei { get; }
    readonly double mm;
    readonly double rad;
    readonly Dictionary<int, Dreiecksnetz?> karten = [];
    readonly Dictionary<int, Dreiecksnetz> gemerkt = [];
    IfcBezug? gemerktBezug;
    readonly HashSet<int> vereinfacht = [];
    int aktuell;   // EntityLabel des Produkts, das gerade ausgewertet wird (für «vereinfacht»)

    /// <summary>Entität eines Geometrie-Elements → Anzahl, die kein Netz ergab (nicht unterstützt).</summary>
    public Dictionary<string, int> Unbekannt { get; } = [];

    IfcGeometrie(IfcDatei d)
    {
        Datei = d;
        mm = d.MmJeEinheit;
        var f = d.Modell.ModelFactors.AngleToRadiansConversionFactor;
        rad = f > 0 ? f : Math.PI / 180;
    }

    /// <summary>Wurde der Körper des Produkts vereinfacht (Boolean: nur erster Operand)?</summary>
    public bool IstVereinfacht(IIfcProduct p) => vereinfacht.Contains(p.EntityLabel);

    /// <summary>Anzahl der Produkte mit vereinfachtem Körper.</summary>
    public int AnzahlVereinfacht => vereinfacht.Count;

    // ------------------------------------------------------------------ Produkte

    /// <summary>Elemente der ersten vorhandenen Repräsentation mit einer der Kennungen (RepresentationIdentifier) oder null.</summary>
    public static IReadOnlyList<IIfcRepresentationItem>? Elemente(IIfcProduct p, IReadOnlyList<string> kennungen)
    {
        var reps = p.Representation?.Representations;
        if (reps is null) return null;
        foreach (var k in kennungen)
            foreach (var r in reps)
                if (string.Equals(r.RepresentationIdentifier?.ToString(), k, StringComparison.OrdinalIgnoreCase) && r.Items.Count > 0)
                    return r.Items.ToList();
        return null;
    }

    /// <summary>Hat das Produkt eine Körper-Repräsentation?</summary>
    public static bool HatKoerper(IIfcProduct p) => Elemente(p, KoerperKennungen) is not null;

    /// <summary>Körper des Produkts in Projektkoordinaten (mm) oder null. merken = für die Netzdatei aufheben.</summary>
    public Dreiecksnetz? Koerper(IIfcProduct p, bool merken = true)
    {
        if (gemerktBezug is not null && !Equals(gemerktBezug, Datei.Bezug)) { gemerkt.Clear(); gemerktBezug = null; }
        if (gemerkt.TryGetValue(p.EntityLabel, out var da)) return da;
        var n = Netz(p, KoerperKennungen);
        if (n is not null && merken)
        {
            gemerktBezug ??= Datei.Bezug;
            gemerkt[p.EntityLabel] = n;
        }
        return n;
    }

    /// <summary>Netz einer bestimmten Repräsentation (z. B. «Body», «Reference» bei Öffnungen) in Projektkoordinaten, ohne Zwischenspeicher.</summary>
    public Dreiecksnetz? Netz(IIfcProduct p, IReadOnlyList<string> kennungen)
    {
        var items = Elemente(p, kennungen);
        if (items is null) return null;
        var lokal = new Dreiecksnetz();
        aktuell = p.EntityLabel;
        foreach (var it in items) Element(it, lokal, Affin.Einheit, 0);
        aktuell = 0;
        if (lokal.Leer) return null;
        return lokal.Abgebildet(Affin.Aus(Datei.Lage(p)));
    }

    /// <summary>Gibt ein gemerktes Netz heraus und vergisst es (Netzdatei: jedes Netz nur einmal im Speicher).</summary>
    public Dreiecksnetz? Herausnehmen(int entityLabel)
    {
        if (gemerktBezug is not null && !Equals(gemerktBezug, Datei.Bezug)) { gemerkt.Clear(); gemerktBezug = null; }
        return gemerkt.Remove(entityLabel, out var n) ? n : null;
    }

    /// <summary>Zähler (nicht ausgewertete Entitäten, vereinfachte Körper) sichern – eine Auswertung neben dem Übertrag (IFC einpassen)
    /// soll die Hinweise des Übertrags nicht doppelt zählen.</summary>
    internal (Dictionary<string, int> Unbekannt, HashSet<int> Vereinfacht) ZaehlerSichern() => (new(Unbekannt), new(vereinfacht));

    /// <summary>Setzt die Zähler zurück und vergisst die seither gemerkten Körper – sonst stünde ein gemerkter vereinfachter Körper ohne
    /// seine Marke «vereinfacht» da (gleicher Bezug beim Übertrag).</summary>
    internal void ZaehlerZuruecksetzen((Dictionary<string, int> Unbekannt, HashSet<int> Vereinfacht) z)
    {
        Unbekannt.Clear();
        foreach (var (k, v) in z.Unbekannt) Unbekannt[k] = v;
        vereinfacht.Clear();
        vereinfacht.UnionWith(z.Vereinfacht);
        gemerkt.Clear();
        gemerktBezug = null;
    }

    /// <summary>Vergisst alle gemerkten Netze und Abbildungsquellen.</summary>
    public void Leeren()
    {
        gemerkt.Clear();
        karten.Clear();
    }

    /// <summary>Die Extrusion des Körpers, wenn er aus genau einer Extrusion besteht (auch als erster Operand eines Booleans
    /// oder in einem MappedItem), in Projektkoordinaten; sonst null.</summary>
    public Extrusionsform? Extrusion(IIfcProduct p)
    {
        var items = Elemente(p, KoerperKennungen);
        if (items is null || items.Count != 1) return null;
        var t = Affin.Aus(Datei.Lage(p));
        bool einfach = false;
        IIfcRepresentationItem it = items[0];
        for (int tiefe = 0; tiefe < 10000; tiefe++)
        {
            switch (it)
            {
                case IIfcMappedItem mi when mi.MappingSource?.MappedRepresentation?.Items is { Count: 1 } mItems:
                    t = t * Abbildung(mi);
                    it = mItems.First();
                    continue;
                case IIfcBooleanResult b when b.FirstOperand is IIfcRepresentationItem erster:
                    if (b.Operator != IfcBooleanOperator.UNION) einfach = true;
                    else return null;
                    it = erster;
                    continue;
                case IIfcCsgSolid c when c.TreeRootExpression is IIfcRepresentationItem wurzel:
                    it = wurzel;
                    continue;
                case IIfcExtrudedAreaSolid e:
                {
                    var flaechen = Profil(e.SweptArea);
                    if (flaechen is null || flaechen.Count == 0) return null;
                    var pos = t * Affin.Aus(Datei.Achsen(e.Position));
                    var r = IfcDatei.Richtung(e.ExtrudedDirection) ?? [0, 0, 1];
                    double l = Math.Sqrt(r[0] * r[0] + r[1] * r[1] + r[2] * r[2]);
                    if (l < 1e-12) return null;
                    double tiefeMm = (double)e.Depth * mm;
                    var (vx, vy, vz) = pos.Richtung(r[0] / l * tiefeMm, r[1] / l * tiefeMm, r[2] / l * tiefeMm);
                    List<double[]> Ring(List<double[]> ring) => ring.Select(q => pos.Punkt([q[0], q[1], 0])).ToList();
                    return new Extrusionsform(flaechen.Select(f => Ring(f.Aussen)).ToList(), flaechen.SelectMany(f => f.Loecher).Select(Ring).ToList(), [vx, vy, vz], einfach);
                }
                default:
                    return null;
            }
        }
        return null;
    }

    // ------------------------------------------------------------------ Geometrie-Elemente

    void Zaehlen(IPersistEntity e) => Unbekannt[IfcDatei.Entitaet(e)] = Unbekannt.GetValueOrDefault(IfcDatei.Entitaet(e)) + 1;

    void Vereinfachen() { if (aktuell != 0) vereinfacht.Add(aktuell); }

    /// <summary>Wertet ein Geometrie-Element aus und hängt sein Netz (abgebildet mit t) an ziel an.</summary>
    void Element(IIfcRepresentationItem it, Dreiecksnetz ziel, in Affin t, int tiefe)
    {
        if (tiefe > 40) { Zaehlen(it); return; }
        switch (it)
        {
            case IIfcMappedItem mi:
                if (Karte(mi.MappingSource) is { } k) ziel.Anhaengen(k, t * Abbildung(mi));
                break;
            case IIfcExtrudedAreaSolid e:
                Extrudieren(e, ziel, t);
                break;
            case IIfcRevolvedAreaSolid r:
                Drehen(r, ziel, t);
                break;
            case IIfcSweptDiskSolid s:
                Rohr(s, ziel, t);
                break;
            case IIfcFacetedBrep b:
                Schale(b.Outer?.CfsFaces, ziel, t);
                if (b is IIfcFacetedBrepWithVoids v)
                    foreach (var h in v.Voids) Schale(h.CfsFaces, ziel, t);
                break;
            case IIfcFaceBasedSurfaceModel f:
                foreach (var s in f.FbsmFaces) Schale(s.CfsFaces, ziel, t);
                break;
            case IIfcShellBasedSurfaceModel sb:
                foreach (var s in sb.SbsmBoundary)
                    if (s is IIfcConnectedFaceSet cfs) Schale(cfs.CfsFaces, ziel, t);
                break;
            case IIfcTriangulatedFaceSet tf:
                Dreiecksliste(tf, ziel, t);
                break;
            case IIfcPolygonalFaceSet pf:
                Vielecksliste(pf, ziel, t);
                break;
            case IIfcBooleanResult b:
            {
                // Spezifikation B1: erster Operand; Differenz und Schnitt sind damit vereinfacht (Hinweis). Lange Ketten von
                // Differenzen (Archicad: zehn und mehr je Bauteil) ohne Rekursion bis zum ersten echten Operanden
                var x = b;
                while (x.Operator != IfcBooleanOperator.UNION && x.FirstOperand is IIfcBooleanResult innen) { Vereinfachen(); x = innen; }
                if (x.FirstOperand is IIfcRepresentationItem a) Element(a, ziel, t, tiefe + 1);
                if (x.Operator == IfcBooleanOperator.UNION && x.SecondOperand is IIfcRepresentationItem c) Element(c, ziel, t, tiefe + 1);
                else Vereinfachen();
                break;
            }
            case IIfcCsgSolid cs:
                if (cs.TreeRootExpression is IIfcRepresentationItem w) Element(w, ziel, t, tiefe + 1);
                break;
            case IIfcBlock bl:
                Quader(ziel, t * Affin.Aus(Datei.Achsen(bl.Position)), 0, 0, 0, (double)bl.XLength * mm, (double)bl.YLength * mm, (double)bl.ZLength * mm);
                break;
            case IIfcRectangularPyramid py:
                Pyramide(ziel, t * Affin.Aus(Datei.Achsen(py.Position)), (double)py.XLength * mm, (double)py.YLength * mm, (double)py.Height * mm);
                break;
            case IIfcRightCircularCylinder zy:
                Kegel(ziel, t * Affin.Aus(Datei.Achsen(zy.Position)), (double)zy.Radius * mm, (double)zy.Radius * mm, (double)zy.Height * mm);
                break;
            case IIfcRightCircularCone ke:
                Kegel(ziel, t * Affin.Aus(Datei.Achsen(ke.Position)), (double)ke.BottomRadius * mm, 0, (double)ke.Height * mm);
                break;
            case IIfcSphere ku:
                Kugel(ziel, t * Affin.Aus(Datei.Achsen(ku.Position)), (double)ku.Radius * mm);
                break;
            case IIfcBoundingBox bb:
            {
                var ecke = Datei.Punkt(bb.Corner);
                Quader(ziel, t, ecke[0], ecke[1], ecke[2], (double)bb.XDim * mm, (double)bb.YDim * mm, (double)bb.ZDim * mm);
                break;
            }
            case IIfcCurve or IIfcGeometricSet or IIfcPoint or IIfcAnnotationFillArea or IIfcTextLiteral or IIfcDirection:
                break;   // Linien, Texte, Punkte: kein Körper
            default:
                Zaehlen(it);
                break;
        }
    }

    /// <summary>Abbildung eines MappedItem: Ziel (IfcCartesianTransformationOperator) nach Ursprung der Abbildungsquelle.</summary>
    Affin Abbildung(IIfcMappedItem mi)
    {
        var ursprung = Affin.Aus(Datei.Achsen(mi.MappingSource?.MappingOrigin));
        return Operator(mi.MappingTarget) * ursprung;
    }

    /// <summary>IfcCartesianTransformationOperator (2D/3D, auch ungleichmässig) als Abbildung; Achsen wie IfcBaseAxis.</summary>
    Affin Operator(IIfcCartesianTransformationOperator? op)
    {
        if (op is null) return Affin.Einheit;
        var o = Datei.Punkt(op.LocalOrigin);
        double s1 = op.Scale.HasValue ? (double)op.Scale.Value : 1, s2 = s1, s3 = s1;
        if (op is IIfcCartesianTransformationOperator3DnonUniform nu)
        {
            if (nu.Scale2.HasValue) s2 = (double)nu.Scale2.Value;
            if (nu.Scale3.HasValue) s3 = (double)nu.Scale3.Value;
        }
        else if (op is IIfcCartesianTransformationOperator2DnonUniform nu2 && nu2.Scale2.HasValue) s2 = (double)nu2.Scale2.Value;
        var z = Einheitsvektor(op is IIfcCartesianTransformationOperator3D o3 ? IfcDatei.Richtung(o3.Axis3) : null) ?? [0, 0, 1];
        var x = IfcDatei.Richtung(op.Axis1);
        var y0 = IfcDatei.Richtung(op.Axis2);
        double[] xx;
        if (x is not null) xx = Rechtwinklig(x, z) ?? Ersatz(z);
        else if (y0 is not null && Rechtwinklig(y0, z) is { } yy) xx = Kreuz(yy, z);
        else xx = Ersatz(z);
        var yv = Kreuz(z, xx);
        // Axis2 gegen die abgeleitete y-Achse: zeigt sie entgegen, ist die Abbildung gespiegelt
        if (y0 is not null && x is not null && y0[0] * yv[0] + y0[1] * yv[1] + y0[2] * yv[2] < 0) yv = [-yv[0], -yv[1], -yv[2]];
        return Affin.Spalten([xx[0] * s1, xx[1] * s1, xx[2] * s1], [yv[0] * s2, yv[1] * s2, yv[2] * s2], [z[0] * s3, z[1] * s3, z[2] * s3], o);
    }

    static double[] Ersatz(double[] z) => Math.Abs(z[0]) < 0.9 ? Rechtwinklig([1, 0, 0], z)! : Rechtwinklig([0, 1, 0], z)!;

    static double[]? Einheitsvektor(double[]? v)
    {
        if (v is null) return null;
        double l = Math.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]);
        return l < 1e-12 ? null : [v[0] / l, v[1] / l, v[2] / l];
    }

    static double[]? Rechtwinklig(double[] v, double[] z)
    {
        double d = v[0] * z[0] + v[1] * z[1] + v[2] * z[2];
        return Einheitsvektor([v[0] - d * z[0], v[1] - d * z[1], v[2] - d * z[2]]);
    }

    static double[] Kreuz(double[] a, double[] b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];

    /// <summary>Netz einer Abbildungsquelle in ihren eigenen Koordinaten (einmal je Quelle gerechnet).</summary>
    Dreiecksnetz? Karte(IIfcRepresentationMap? karte)
    {
        if (karte is null) return null;
        if (karten.TryGetValue(karte.EntityLabel, out var n)) return n;
        karten[karte.EntityLabel] = null;   // gegen Rekursion
        var netz = new Dreiecksnetz();
        foreach (var it in karte.MappedRepresentation?.Items ?? Enumerable.Empty<IIfcRepresentationItem>())
            Element(it, netz, Affin.Einheit, 1);
        karten[karte.EntityLabel] = netz.Leer ? null : netz;
        return karten[karte.EntityLabel];
    }

    // ------------------------------------------------------------------ Volumen aus Profilen

    void Extrudieren(IIfcExtrudedAreaSolid e, Dreiecksnetz ziel, in Affin t)
    {
        var flaechen = Profil(e.SweptArea);
        if (flaechen is null) { Zaehlen(e.SweptArea is IPersistEntity pe ? pe : e); return; }
        var r = IfcDatei.Richtung(e.ExtrudedDirection) ?? [0, 0, 1];
        double l = Math.Sqrt(r[0] * r[0] + r[1] * r[1] + r[2] * r[2]);
        if (l < 1e-12) return;
        double tiefe = (double)e.Depth * mm;
        double[] v = [r[0] / l * tiefe, r[1] / l * tiefe, r[2] / l * tiefe];
        var pos = t * Affin.Aus(Datei.Achsen(e.Position));
        foreach (var f in flaechen) Prisma(ziel, f, v, pos);
    }

    /// <summary>Prisma aus einer Profilfläche (z = 0) entlang v, abgebildet mit t: Boden, Deckel, Seiten.</summary>
    static void Prisma(Dreiecksnetz ziel, Profilflaeche f, double[] v, in Affin t)
    {
        var ringe = new List<List<double[]>> { f.Aussen };
        ringe.AddRange(f.Loecher);
        var dreiecke = Dreieckszerlegung.Zerlegen(f.Aussen, f.Loecher);
        bool umdrehen = (v[2] < 0) ^ (t.Det < 0);
        void D(int a, int b, int c) { if (umdrehen) ziel.Dreieck(a, c, b); else ziel.Dreieck(a, b, c); }
        int unten = ziel.Punkte;
        foreach (var r in ringe) foreach (var p in r) { var (x, y, z) = t.Punkt(p[0], p[1], 0); ziel.Punkt(x, y, z); }
        int oben = ziel.Punkte;
        foreach (var r in ringe) foreach (var p in r) { var (x, y, z) = t.Punkt(p[0] + v[0], p[1] + v[1], v[2]); ziel.Punkt(x, y, z); }
        for (int i = 0; i + 2 < dreiecke.Count; i += 3)
        {
            D(unten + dreiecke[i], unten + dreiecke[i + 2], unten + dreiecke[i + 1]);
            D(oben + dreiecke[i], oben + dreiecke[i + 1], oben + dreiecke[i + 2]);
        }
        int basis = 0;
        foreach (var r in ringe)
        {
            int n = r.Count;
            for (int i = 0; i < n; i++)
            {
                int a = basis + i, b = basis + (i + 1) % n;
                D(unten + a, unten + b, oben + b);
                D(unten + a, oben + b, oben + a);
            }
            basis += n;
        }
    }

    void Drehen(IIfcRevolvedAreaSolid r, Dreiecksnetz ziel, in Affin t)
    {
        var flaechen = Profil(r.SweptArea);
        if (flaechen is null || r.Axis is null) { Zaehlen(r); return; }
        var pos = t * Affin.Aus(Datei.Achsen(r.Position));
        var o = Datei.Punkt(r.Axis.Location);
        var a = Einheitsvektor(IfcDatei.Richtung(r.Axis.Axis) ?? [0, 0, 1]) ?? [0, 0, 1];
        double winkel = (double)r.Angle * rad;
        int schritte = Math.Clamp((int)Math.Ceiling(Math.Abs(winkel) / (10 * Math.PI / 180)), 1, 72);
        bool voll = Math.Abs(Math.Abs(winkel) - 2 * Math.PI) < 1e-6;
        // Punkt p um die Achse (o, a) drehen (Rodrigues)
        double[] Dreh(double[] p, double w)
        {
            double[] q = [p[0] - o[0], p[1] - o[1], p[2] - o[2]];
            double c = Math.Cos(w), s = Math.Sin(w), d = a[0] * q[0] + a[1] * q[1] + a[2] * q[2];
            var k = Kreuz(a, q);
            return [o[0] + q[0] * c + k[0] * s + a[0] * d * (1 - c), o[1] + q[1] * c + k[1] * s + a[1] * d * (1 - c), o[2] + q[2] * c + k[2] * s + a[2] * d * (1 - c)];
        }
        foreach (var f in flaechen)
        {
            var ringe = new List<List<double[]>> { f.Aussen };
            ringe.AddRange(f.Loecher);
            var lagen = new List<int>();   // Index des ersten Punkts je Schritt
            for (int k = 0; k <= schritte; k++)
            {
                if (voll && k == schritte) { lagen.Add(lagen[0]); break; }
                lagen.Add(ziel.Punkte);
                foreach (var ring in ringe)
                    foreach (var p in ring) { var q = Dreh([p[0], p[1], 0], winkel * k / schritte); var (x, y, z) = pos.Punkt(q[0], q[1], q[2]); ziel.Punkt(x, y, z); }
            }
            for (int k = 0; k < schritte; k++)
            {
                int basis = 0;
                foreach (var ring in ringe)
                {
                    for (int i = 0; i < ring.Count; i++)
                    {
                        int a0 = lagen[k] + basis + i, b0 = lagen[k] + basis + (i + 1) % ring.Count, a1 = lagen[k + 1] + basis + i, b1 = lagen[k + 1] + basis + (i + 1) % ring.Count;
                        ziel.Dreieck(a0, b0, b1); ziel.Dreieck(a0, b1, a1);
                    }
                    basis += ring.Count;
                }
            }
            if (!voll)
            {
                var dr = Dreieckszerlegung.Zerlegen(f.Aussen, f.Loecher);
                for (int i = 0; i + 2 < dr.Count; i += 3)
                {
                    ziel.Dreieck(lagen[0] + dr[i], lagen[0] + dr[i + 2], lagen[0] + dr[i + 1]);
                    ziel.Dreieck(lagen[^1] + dr[i], lagen[^1] + dr[i + 1], lagen[^1] + dr[i + 2]);
                }
            }
        }
    }

    /// <summary>IfcSweptDiskSolid: Rohr mit zwölf Seiten je gerades Stück der Leitkurve (ohne Deckel).</summary>
    void Rohr(IIfcSweptDiskSolid s, Dreiecksnetz ziel, in Affin t)
    {
        bool gekruemmt = false;
        var pts = Kurve(s.Directrix, ref gekruemmt);
        if (pts is null || pts.Count < 2) { Zaehlen(s); return; }
        double r = (double)s.Radius * mm;
        const int seiten = 12;
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            var a = pts[i]; var b = pts[i + 1];
            var u = Einheitsvektor([b[0] - a[0], b[1] - a[1], b[2] - a[2]]);
            if (u is null) continue;
            var n1 = Ersatz(u); var n2 = Kreuz(u, n1);
            int basis = ziel.Punkte;
            foreach (var p in new[] { a, b })
                for (int k = 0; k < seiten; k++)
                {
                    double w = 2 * Math.PI * k / seiten, c = Math.Cos(w) * r, sn = Math.Sin(w) * r;
                    var (x, y, z) = t.Punkt(p[0] + n1[0] * c + n2[0] * sn, p[1] + n1[1] * c + n2[1] * sn, p[2] + n1[2] * c + n2[2] * sn);
                    ziel.Punkt(x, y, z);
                }
            for (int k = 0; k < seiten; k++)
            {
                int k1 = (k + 1) % seiten;
                ziel.Dreieck(basis + k, basis + k1, basis + seiten + k1);
                ziel.Dreieck(basis + k, basis + seiten + k1, basis + seiten + k);
            }
        }
    }

    // ------------------------------------------------------------------ Flächenmodelle

    /// <summary>Flächen einer Schale (IfcFace mit IfcPolyLoop-Rändern); gemeinsame Punkte nur einmal.</summary>
    void Schale(IEnumerable<IIfcFace>? flaechen, Dreiecksnetz ziel, in Affin t)
    {
        if (flaechen is null) return;
        var index = new Dictionary<int, int>();
        int PunktIndex(IIfcCartesianPoint p, in Affin tt)
        {
            if (index.TryGetValue(p.EntityLabel, out var i)) return i;
            double x = p.X * mm, y = p.Y * mm, z = double.IsNaN(p.Z) ? 0 : p.Z * mm;
            var (a, b, c) = tt.Punkt(x, y, z);
            i = ziel.Punkt(a, b, c);
            index[p.EntityLabel] = i;
            return i;
        }
        foreach (var f in flaechen)
        {
            List<int>? aussen = null;
            List<List<int>>? loecher = null;
            foreach (var rand in f.Bounds)
            {
                if (rand.Bound is not IIfcPolyLoop loop) { if (rand.Bound is not null) Zaehlen(rand.Bound); continue; }
                var ring = new List<int>();
                foreach (var p in loop.Polygon)
                {
                    int i = PunktIndex(p, t);
                    if (ring.Count == 0 || ring[^1] != i) ring.Add(i);
                }
                if (ring.Count > 1 && ring[0] == ring[^1]) ring.RemoveAt(ring.Count - 1);
                if (ring.Count < 3) continue;
                if (!(bool)rand.Orientation) ring.Reverse();
                if (rand is IIfcFaceOuterBound && aussen is null) aussen = ring;
                else (loecher ??= []).Add(ring);
            }
            if (aussen is null && loecher is { Count: > 0 })
            {
                // ohne ausdrücklichen Aussenrand: der grösste Rand ist aussen
                int g = 0;
                for (int i = 1; i < loecher.Count; i++) if (Groesse(ziel, loecher[i]) > Groesse(ziel, loecher[g])) g = i;
                aussen = loecher[g];
                loecher.RemoveAt(g);
            }
            if (aussen is not null) Flaeche(ziel, aussen, loecher);
        }
    }

    static double Groesse(Dreiecksnetz n, List<int> ring)
    {
        var (nx, ny, nz) = Newell(n, ring);
        return Math.Sqrt(nx * nx + ny * ny + nz * nz);
    }

    static (double X, double Y, double Z) Newell(Dreiecksnetz n, List<int> ring)
    {
        double x = 0, y = 0, z = 0;
        for (int i = 0; i < ring.Count; i++)
        {
            var (ax, ay, az) = n.Lage(ring[i]); var (bx, by, bz) = n.Lage(ring[(i + 1) % ring.Count]);
            x += (ay - by) * (az + bz); y += (az - bz) * (ax + bx); z += (ax - bx) * (ay + by);
        }
        return (x, y, z);
    }

    /// <summary>Ebene Fläche (Punktindizes im Netz, Aussenrand und Löcher) in Dreiecke zerlegen; Dreiecke zeigen wie die Fläche.</summary>
    static void Flaeche(Dreiecksnetz ziel, List<int> aussen, List<List<int>>? loecher)
    {
        if (aussen.Count == 3 && loecher is null) { ziel.Dreieck(aussen[0], aussen[1], aussen[2]); return; }
        var (nx, ny, nz) = Newell(ziel, aussen);
        double ax = Math.Abs(nx), ay = Math.Abs(ny), az = Math.Abs(nz);
        if (ax + ay + az < 1e-12) return;
        // auf die Ebene mit der grössten Normalkomponente projizieren; (u, v) zyklisch, damit die Drehrichtung erhalten bleibt
        int k = az >= ax && az >= ay ? 2 : ax >= ay ? 0 : 1;
        double vorzeichen = k == 2 ? nz : k == 0 ? nx : ny;
        double[] P2(int i)
        {
            var (x, y, z) = ziel.Lage(i);
            return k switch { 2 => [x, y], 0 => [y, z], _ => [z, x] };
        }
        if (aussen.Count == 4 && loecher is null)
        {
            // konvexes Viereck: zwei Dreiecke ohne Zerlegung
            var q = aussen.Select(P2).ToArray();
            bool konvex = true;
            double s0 = 0;
            for (int i = 0; i < 4 && konvex; i++)
            {
                var a = q[i]; var b = q[(i + 1) % 4]; var c = q[(i + 2) % 4];
                double kr = (b[0] - a[0]) * (c[1] - b[1]) - (b[1] - a[1]) * (c[0] - b[0]);
                if (i == 0) s0 = kr; else if (kr * s0 <= 0) konvex = false;
            }
            if (konvex) { ziel.Dreieck(aussen[0], aussen[1], aussen[2]); ziel.Dreieck(aussen[0], aussen[2], aussen[3]); return; }
        }
        var alle = new List<int>(aussen);
        var l2 = new List<List<double[]>>();
        if (loecher is not null)
            foreach (var l in loecher) { alle.AddRange(l); l2.Add(l.Select(P2).ToList()); }
        var dr = Dreieckszerlegung.Zerlegen(aussen.Select(P2).ToList(), l2);
        // Zerlegung liefert Dreiecke im Gegenuhrzeigersinn der (u, v)-Ebene, also Normale +k: anpassen
        for (int i = 0; i + 2 < dr.Count; i += 3)
        {
            if (vorzeichen >= 0) ziel.Dreieck(alle[dr[i]], alle[dr[i + 1]], alle[dr[i + 2]]);
            else ziel.Dreieck(alle[dr[i]], alle[dr[i + 2]], alle[dr[i + 1]]);
        }
    }

    /// <summary>Koordinatenliste (IfcCartesianPointList3D/2D) in mm.</summary>
    List<double[]> Koordinaten(IIfcCartesianPointList? liste)
    {
        var aus = new List<double[]>();
        IEnumerable<IEnumerable<Xbim.Ifc4.MeasureResource.IfcLengthMeasure>>? zeilen = liste switch
        {
            IIfcCartesianPointList3D l3 => l3.CoordList,
            IIfcCartesianPointList2D l2 => l2.CoordList,
            _ => null,
        };
        if (zeilen is null) return aus;
        foreach (var z in zeilen)
        {
            double x = 0, y = 0, w = 0; int i = 0;
            foreach (var c in z) { double v = (double)c * mm; if (i == 0) x = v; else if (i == 1) y = v; else if (i == 2) w = v; i++; }
            aus.Add([x, y, w]);
        }
        return aus;
    }

    static List<int>? Indizes(IEnumerable<object>? werte, IReadOnlyList<int>? pn)
    {
        if (werte is null) return null;
        var aus = new List<int>();
        foreach (var w in werte)
        {
            int i = (int)Zahl(w) - 1;
            if (pn is not null) i = i >= 0 && i < pn.Count ? pn[i] - 1 : -1;
            aus.Add(i);
        }
        return aus;
    }

    /// <summary>Zahl aus einem Express-Wert (IfcPositiveInteger, IfcParameterValue …) oder einer Zahl.</summary>
    internal static double Zahl(object? w) => w switch
    {
        null => double.NaN,
        IExpressValueType ev => Convert.ToDouble(ev.Value, System.Globalization.CultureInfo.InvariantCulture),
        _ => Convert.ToDouble(w, System.Globalization.CultureInfo.InvariantCulture),
    };

    void Dreiecksliste(IIfcTriangulatedFaceSet tf, Dreiecksnetz ziel, in Affin t)
    {
        var k = Koordinaten(tf.Coordinates);
        var pn = tf.PnIndex.Count > 0 ? tf.PnIndex.Select(x => (int)Zahl(x)).ToList() : null;
        int basis = ziel.Punkte;
        foreach (var p in k) { var (x, y, z) = t.Punkt(p[0], p[1], p[2]); ziel.Punkt(x, y, z); }
        bool drehen = t.Det < 0;
        foreach (var dreieck in tf.CoordIndex)
        {
            var i = Indizes(dreieck.Cast<object>(), pn);
            if (i is null || i.Count < 3 || i.Take(3).Any(x => x < 0 || x >= k.Count)) continue;
            if (drehen) ziel.Dreieck(basis + i[0], basis + i[2], basis + i[1]);
            else ziel.Dreieck(basis + i[0], basis + i[1], basis + i[2]);
        }
    }

    void Vielecksliste(IIfcPolygonalFaceSet pf, Dreiecksnetz ziel, in Affin t)
    {
        var k = Koordinaten(pf.Coordinates);
        var pn = pf.PnIndex.Count > 0 ? pf.PnIndex.Select(x => (int)Zahl(x)).ToList() : null;
        int basis = ziel.Punkte;
        foreach (var p in k) { var (x, y, z) = t.Punkt(p[0], p[1], p[2]); ziel.Punkt(x, y, z); }
        bool drehen = t.Det < 0;
        List<int>? Ring(IEnumerable<object> werte)
        {
            var i = Indizes(werte, pn);
            if (i is null || i.Any(x => x < 0 || x >= k.Count)) return null;
            var r = new List<int>();
            foreach (var x in i) if (r.Count == 0 || r[^1] != basis + x) r.Add(basis + x);
            if (r.Count > 1 && r[0] == r[^1]) r.RemoveAt(r.Count - 1);
            if (drehen) r.Reverse();
            return r.Count >= 3 ? r : null;
        }
        foreach (var f in pf.Faces)
        {
            var aussen = Ring(f.CoordIndex.Cast<object>());
            if (aussen is null) continue;
            List<List<int>>? loecher = null;
            if (f is IIfcIndexedPolygonalFaceWithVoids fv)
                foreach (var innen in fv.InnerCoordIndices)
                    if (Ring(innen.Cast<object>()) is { } l) (loecher ??= []).Add(l);
            Flaeche(ziel, aussen, loecher);
        }
    }

    // ------------------------------------------------------------------ Grundkörper (CSG)

    static void Quader(Dreiecksnetz ziel, in Affin t, double x0, double y0, double z0, double lx, double ly, double lz)
    {
        int b = ziel.Punkte;
        for (int i = 0; i < 8; i++)
        {
            var (x, y, z) = t.Punkt(x0 + ((i & 1) != 0 ? lx : 0), y0 + ((i & 2) != 0 ? ly : 0), z0 + ((i & 4) != 0 ? lz : 0));
            ziel.Punkt(x, y, z);
        }
        int[][] seiten = [[0, 2, 3, 1], [4, 5, 7, 6], [0, 1, 5, 4], [2, 6, 7, 3], [0, 4, 6, 2], [1, 3, 7, 5]];
        bool drehen = t.Det < 0;
        foreach (var s in seiten)
        {
            if (drehen) { ziel.Dreieck(b + s[0], b + s[2], b + s[1]); ziel.Dreieck(b + s[0], b + s[3], b + s[2]); }
            else { ziel.Dreieck(b + s[0], b + s[1], b + s[2]); ziel.Dreieck(b + s[0], b + s[2], b + s[3]); }
        }
    }

    static void Pyramide(Dreiecksnetz ziel, in Affin t, double lx, double ly, double h)
    {
        int b = ziel.Punkte;
        foreach (var (x, y, z) in new[] { (0.0, 0.0, 0.0), (lx, 0, 0), (lx, ly, 0), (0, ly, 0), (lx / 2, ly / 2, h) })
        {
            var q = t.Punkt(x, y, z);
            ziel.Punkt(q.X, q.Y, q.Z);
        }
        ziel.Dreieck(b, b + 2, b + 1); ziel.Dreieck(b, b + 3, b + 2);
        for (int i = 0; i < 4; i++) ziel.Dreieck(b + i, b + (i + 1) % 4, b + 4);
    }

    /// <summary>Kegelstumpf bzw. Zylinder um die z-Achse ab z = 0 (24 Seiten).</summary>
    static void Kegel(Dreiecksnetz ziel, in Affin t, double r0, double r1, double h)
    {
        const int n = 24;
        int b = ziel.Punkte;
        for (int k = 0; k < n; k++)
        {
            double w = 2 * Math.PI * k / n;
            var u = t.Punkt(r0 * Math.Cos(w), r0 * Math.Sin(w), 0); ziel.Punkt(u.X, u.Y, u.Z);
            var o = t.Punkt(r1 * Math.Cos(w), r1 * Math.Sin(w), h); ziel.Punkt(o.X, o.Y, o.Z);
        }
        var mu = t.Punkt(0, 0, 0); int iu = ziel.Punkt(mu.X, mu.Y, mu.Z);
        var mo = t.Punkt(0, 0, h); int io = ziel.Punkt(mo.X, mo.Y, mo.Z);
        for (int k = 0; k < n; k++)
        {
            int a0 = b + 2 * k, a1 = b + 2 * k + 1, c0 = b + 2 * ((k + 1) % n), c1 = c0 + 1;
            ziel.Dreieck(a0, c0, c1); ziel.Dreieck(a0, c1, a1);
            ziel.Dreieck(iu, c0, a0); ziel.Dreieck(io, a1, c1);
        }
    }

    static void Kugel(Dreiecksnetz ziel, in Affin t, double r)
    {
        const int breiten = 12, laengen = 24;
        int b = ziel.Punkte;
        for (int i = 0; i <= breiten; i++)
        {
            double phi = Math.PI * i / breiten - Math.PI / 2;
            for (int k = 0; k < laengen; k++)
            {
                double w = 2 * Math.PI * k / laengen;
                var q = t.Punkt(r * Math.Cos(phi) * Math.Cos(w), r * Math.Cos(phi) * Math.Sin(w), r * Math.Sin(phi));
                ziel.Punkt(q.X, q.Y, q.Z);
            }
        }
        for (int i = 0; i < breiten; i++)
            for (int k = 0; k < laengen; k++)
            {
                int a = b + i * laengen + k, c = b + i * laengen + (k + 1) % laengen;
                ziel.Dreieck(a, c, c + laengen); ziel.Dreieck(a, c + laengen, a + laengen);
            }
    }
}
