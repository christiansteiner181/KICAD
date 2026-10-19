// IFC-Übertrag HLKS (Modul 1b, B2): einfache Geometrie aus dem Körper (Body-Repräsentation) eines Produkts – Punkte in
// Projektkoordinaten (mm), Hüllquader, Achse und Querschnitt. Gebraucht, wo die Datei keine Ports oder Lagen liefert,
// die taugen: Trimble Nova setzt alle Produkte auf den Ursprung des Geschosses und beschreibt den Körper als
// MappedItem → FaceBasedSurfaceModel in absoluten Koordinaten; Revit exportiert Rohre und Kanäle als Extrusion.
// Bewusst klein: nur Punkte (keine Dreiecke) – die exakte Geometrie für Kollision und Hintergrund baut B1 (IfcGeometrie).

using Xbim.Ifc4.Interfaces;

namespace Hlks.Kern;

/// <summary>Querschnitt einer Leitung in mm: rund (Durchmesser) oder rechteckig (Breite quer, Höhe senkrecht).</summary>
public sealed record IfcHlksQuerschnitt(double? Durchmesser, double? Breite, double? Hoehe, string Herkunft)
{
    public bool Rund => Durchmesser is not null;
}

/// <summary>Punkte und Extrusion des Körpers eines Produkts (Projektkoordinaten, mm).</summary>
public sealed class IfcHlksKoerper
{
    /// <summary>Höchstzahl gelesener Punkte je Produkt (grosse Netze: der Rest ändert Hülle und Achse kaum).</summary>
    public const int MaxPunkte = 20000;

    public List<double[]> Punkte { get; } = [];
    /// <summary>Erste Extrusion des Körpers (Revit: Rohre und Kanäle) mit ihrer Lage in Projektkoordinaten.</summary>
    public IIfcExtrudedAreaSolid? Extrusion { get; private set; }
    public Matrix4 ExtrusionLage { get; private set; } = Matrix4.Einheit;

    readonly IfcDatei datei;
    IfcHlksKoerper(IfcDatei datei) => this.datei = datei;

    /// <summary>Liest den Körper eines Produkts; null, wenn es keinen lesbaren hat.</summary>
    public static IfcHlksKoerper? Lesen(IfcDatei d, IIfcProduct p)
    {
        if (p.Representation is null) return null;
        var k = new IfcHlksKoerper(d);
        var reps = p.Representation.Representations.ToList();
        var body = reps.Where(r => string.Equals(IfcLauf.Text(r.RepresentationIdentifier), "Body", StringComparison.OrdinalIgnoreCase)).ToList();
        if (body.Count == 0)
            body = reps.Where(r => IfcLauf.Text(r.RepresentationIdentifier) is not ("Axis" or "FootPrint" or "Annotation" or "Profile" or "Clearance")).ToList();
        var lage = d.Lage(p);
        foreach (var r in body)
            foreach (var it in r.Items)
                k.Element(it, lage, 0);
        return k.Punkte.Count == 0 && k.Extrusion is null ? null : k;
    }

    void Element(IIfcRepresentationItem it, Matrix4 m, int tiefe)
    {
        if (tiefe > 8 || Punkte.Count >= MaxPunkte) return;
        switch (it)
        {
            case IIfcMappedItem mi:
            {
                var quelle = mi.MappingSource;
                var ursprung = datei.Achsen(quelle.MappingOrigin as IIfcAxis2Placement).InverseStarr();
                var ziel = Operator(mi.MappingTarget);
                foreach (var x in quelle.MappedRepresentation.Items) Element(x, m * ziel * ursprung, tiefe + 1);
                break;
            }
            case IIfcFaceBasedSurfaceModel fb:
                foreach (var fs in fb.FbsmFaces) Flaechen(fs, m);
                break;
            case IIfcShellBasedSurfaceModel sb:
                foreach (var s in sb.SbsmBoundary) if (s is IIfcConnectedFaceSet fs) Flaechen(fs, m);
                break;
            case IIfcManifoldSolidBrep br:
                Flaechen(br.Outer, m);
                break;
            case IIfcTessellatedFaceSet ts:
                foreach (var c in ts.Coordinates.CoordList)
                {
                    if (Punkte.Count >= MaxPunkte) break;
                    var w = c.Select(v => (double)v * datei.MmJeEinheit).ToArray();
                    Punkte.Add(m.Punkt([w.Length > 0 ? w[0] : 0, w.Length > 1 ? w[1] : 0, w.Length > 2 ? w[2] : 0]));
                }
                break;
            case IIfcExtrudedAreaSolid ex:
                Extrudiert(ex, m);
                break;
            case IIfcBooleanResult b when b.FirstOperand is IIfcRepresentationItem erster:
                Element(erster, m, tiefe + 1);   // vereinfacht: erster Operand
                break;
            case IIfcBoundingBox bb:
            {
                var c = datei.Punkt(bb.Corner);
                double dx = (double)bb.XDim * datei.MmJeEinheit, dy = (double)bb.YDim * datei.MmJeEinheit, dz = (double)bb.ZDim * datei.MmJeEinheit;
                for (int i = 0; i < 8; i++)
                    Punkte.Add(m.Punkt([c[0] + ((i & 1) != 0 ? dx : 0), c[1] + ((i & 2) != 0 ? dy : 0), c[2] + ((i & 4) != 0 ? dz : 0)]));
                break;
            }
        }
    }

    void Flaechen(IIfcConnectedFaceSet fs, Matrix4 m)
    {
        foreach (var f in fs.CfsFaces)
            foreach (var b in f.Bounds)
                if (b.Bound is IIfcPolyLoop pl)
                    foreach (var p in pl.Polygon)
                    {
                        if (Punkte.Count >= MaxPunkte) return;
                        Punkte.Add(m.Punkt(datei.Punkt(p)));
                    }
    }

    void Extrudiert(IIfcExtrudedAreaSolid ex, Matrix4 m)
    {
        var lage = m * datei.Achsen(ex.Position);
        if (Extrusion is null) { Extrusion = ex; ExtrusionLage = lage; }
        var umriss = Umriss(ex.SweptArea);
        var r = IfcDatei.Richtung(ex.ExtrudedDirection) ?? [0, 0, 1];
        double h = (double)ex.Depth * datei.MmJeEinheit;
        foreach (var p in umriss)
        {
            if (Punkte.Count >= MaxPunkte) return;
            Punkte.Add(lage.Punkt([p[0], p[1], 0]));
            Punkte.Add(lage.Punkt([p[0] + r[0] * h, p[1] + r[1] * h, r[2] * h]));
        }
    }

    /// <summary>Umriss eines Profils in Profilkoordinaten (mm), Kreise als 16-Eck.</summary>
    List<double[]> Umriss(IIfcProfileDef profil)
    {
        var aus = new List<double[]>();
        var pos = profil is IIfcParameterizedProfileDef pp ? datei.Achsen(pp.Position) : Matrix4.Einheit;
        switch (profil)
        {
            case IIfcRectangleProfileDef r:
            {
                double x = (double)r.XDim * datei.MmJeEinheit / 2, y = (double)r.YDim * datei.MmJeEinheit / 2;
                foreach (var (a, b) in new[] { (-x, -y), (x, -y), (x, y), (-x, y) }) aus.Add(pos.Punkt([a, b, 0]));
                break;
            }
            case IIfcCircleProfileDef c:
            {
                double rad = (double)c.Radius * datei.MmJeEinheit;
                for (int i = 0; i < 16; i++) aus.Add(pos.Punkt([rad * Math.Cos(i * Math.PI / 8), rad * Math.Sin(i * Math.PI / 8), 0]));
                break;
            }
            case IIfcArbitraryClosedProfileDef a when a.OuterCurve is IIfcPolyline pl:
                foreach (var p in pl.Points) aus.Add(datei.Punkt(p));
                break;
            case IIfcArbitraryClosedProfileDef a when a.OuterCurve is IIfcIndexedPolyCurve ip && ip.Points is IIfcCartesianPointList2D l2:
                foreach (var c in l2.CoordList)
                {
                    var w = c.Select(v => (double)v * datei.MmJeEinheit).ToArray();
                    aus.Add([w.Length > 0 ? w[0] : 0, w.Length > 1 ? w[1] : 0, 0]);
                }
                break;
        }
        return aus;
    }

    /// <summary>Lage aus einem IfcCartesianTransformationOperator (Achsen, Ursprung, Massstab).</summary>
    Matrix4 Operator(IIfcCartesianTransformationOperator? op)
    {
        if (op is null) return Matrix4.Einheit;
        var o = datei.Punkt(op.LocalOrigin);
        var x = IfcDatei.Richtung(op.Axis1);
        var z = op is IIfcCartesianTransformationOperator3D o3 ? IfcDatei.Richtung(o3.Axis3) : null;
        double s = op.Scale.HasValue ? (double)op.Scale.Value : 1;
        var m = Matrix4.Achsen(o, x, z);
        return Math.Abs(s - 1) > 1e-9 ? m * Matrix4.Skalierung(s) : m;
    }

    // ------------------------------------------------------------------ Auswertung

    /// <summary>Hüllquader [min, max] in Projektkoordinaten.</summary>
    public (double[] Min, double[] Max)? Huelle()
    {
        if (Punkte.Count == 0) return null;
        double[] a = [double.MaxValue, double.MaxValue, double.MaxValue], b = [double.MinValue, double.MinValue, double.MinValue];
        foreach (var p in Punkte)
            for (int i = 0; i < 3; i++) { a[i] = Math.Min(a[i], p[i]); b[i] = Math.Max(b[i], p[i]); }
        return (a, b);
    }

    /// <summary>Achse der Extrusion (Anfang, Ende) in Projektkoordinaten, wenn der Körper eine hat.</summary>
    public (double[] A, double[] B)? ExtrusionsAchse()
    {
        if (Extrusion is null) return null;
        var r = IfcDatei.Richtung(Extrusion.ExtrudedDirection) ?? [0, 0, 1];
        double h = (double)Extrusion.Depth * datei.MmJeEinheit;
        var c = Extrusion.SweptArea is IIfcParameterizedProfileDef pp && pp.Position is not null ? datei.Achsen(pp.Position).Ursprung : [0, 0, 0];
        return (ExtrusionLage.Punkt([c[0], c[1], 0]), ExtrusionLage.Punkt([c[0] + r[0] * h, c[1] + r[1] * h, r[2] * h]));
    }

    /// <summary>Hauptachse der Punktwolke (Längsrichtung) mit ihren Enden; die Achse geht durch die Mitte des
    /// Querschnitts (Hülle quer zur Achse), nicht durch den Schwerpunkt der Punkte.</summary>
    public (double[] A, double[] B)? Hauptachse()
    {
        if (Punkte.Count < 4) return null;
        var c = Schwerpunkt();
        var u = Hauptrichtung(c);
        if (u is null) return null;
        var (h, w) = Quer(u);
        double t0 = double.MaxValue, t1 = double.MinValue, h0 = double.MaxValue, h1 = double.MinValue, w0 = double.MaxValue, w1 = double.MinValue;
        foreach (var p in Punkte)
        {
            double[] d = [p[0] - c[0], p[1] - c[1], p[2] - c[2]];
            double t = Skalar(d, u), a = Skalar(d, h), b = Skalar(d, w);
            t0 = Math.Min(t0, t); t1 = Math.Max(t1, t); h0 = Math.Min(h0, a); h1 = Math.Max(h1, a); w0 = Math.Min(w0, b); w1 = Math.Max(w1, b);
        }
        double hm = (h0 + h1) / 2, wm = (w0 + w1) / 2;
        double[] Punkt(double t) => [c[0] + t * u[0] + hm * h[0] + wm * w[0], c[1] + t * u[1] + hm * h[1] + wm * w[1], c[2] + t * u[2] + hm * h[2] + wm * w[2]];
        return t1 - t0 < 1 ? null : (Punkt(t0), Punkt(t1));
    }

    /// <summary>Querschnitt quer zur Achse a→b aus der Punktwolke: rund, wenn die Punkte in vielen Richtungen um die Achse
    /// liegen (Kreis als Vieleck; ein Rechteck hat nur vier Ecken), Durchmesser = grösste Ausdehnung quer (robust gegen eine
    /// leicht schiefe Achse); sonst Rechteck mit Breite waagrecht quer und Höhe senkrecht dazu. Senkrechte Kanäle haben kein
    /// Oben: dort gilt die grössere Seite als Breite (Annahme, wie Kanäle beschriftet werden: 550 × 150).</summary>
    public IfcHlksQuerschnitt? QuerschnittAusPunkten(double[] a, double[] b)
    {
        var u = Einheit([b[0] - a[0], b[1] - a[1], b[2] - a[2]]);
        if (u is null || Punkte.Count < 4) return null;
        var (h, w) = Quer(u);
        double h0 = double.MaxValue, h1 = double.MinValue, w0 = double.MaxValue, w1 = double.MinValue;
        var quer = new List<(double H, double W)>(Punkte.Count);
        foreach (var p in Punkte)
        {
            double[] d = [p[0] - a[0], p[1] - a[1], p[2] - a[2]];
            double x = Skalar(d, h), y = Skalar(d, w);
            quer.Add((x, y));
            h0 = Math.Min(h0, x); h1 = Math.Max(h1, x); w0 = Math.Min(w0, y); w1 = Math.Max(w1, y);
        }
        double breite = h1 - h0, hoehe = w1 - w0;
        if (breite < 1 || hoehe < 1) return null;
        double hm = (h0 + h1) / 2, wm = (w0 + w1) / 2;
        var richtungen = new HashSet<int>();
        foreach (var (x, y) in quer)
        {
            double dx = x - hm, dy = y - wm;
            if (dx * dx + dy * dy > 1) richtungen.Add((int)Math.Floor((Math.Atan2(dy, dx) * 180 / Math.PI + 360) % 360 / 10));
        }
        if (richtungen.Count >= 12)
            return new IfcHlksQuerschnitt(Math.Round(Math.Max(breite, hoehe)), null, null, "Körper (Punkte quer zur Achse)");
        if (Math.Abs(u[2]) > 0.9)
            return new IfcHlksQuerschnitt(null, Math.Round(Math.Max(breite, hoehe)), Math.Round(Math.Min(breite, hoehe)), "Körper (Punkte quer zur Achse; senkrecht: grössere Seite als Breite)");
        return new IfcHlksQuerschnitt(null, Math.Round(breite), Math.Round(hoehe), "Körper (Punkte quer zur Achse)");
    }

    /// <summary>Querschnitt aus dem Profil der Extrusion (Kreis: Durchmesser; Rechteck: Breite waagrecht, Höhe senkrecht).</summary>
    public IfcHlksQuerschnitt? QuerschnittAusProfil()
    {
        if (Extrusion is null) return null;
        var lage = ExtrusionLage * (Extrusion.SweptArea is IIfcParameterizedProfileDef pp ? datei.Achsen(pp.Position) : Matrix4.Einheit);
        switch (Extrusion.SweptArea)
        {
            case IIfcCircleHollowProfileDef ch:
                return new IfcHlksQuerschnitt(Math.Round(2 * (double)ch.Radius * datei.MmJeEinheit, 1), null, null, "Profil IfcCircleHollowProfileDef");
            case IIfcCircleProfileDef c:
                return new IfcHlksQuerschnitt(Math.Round(2 * (double)c.Radius * datei.MmJeEinheit, 1), null, null, "Profil IfcCircleProfileDef");
            case IIfcRectangleProfileDef r:
            {
                double x = (double)r.XDim * datei.MmJeEinheit, y = (double)r.YDim * datei.MmJeEinheit;
                var ax = lage.Richtung([1, 0, 0]); var ay = lage.Richtung([0, 1, 0]);
                // Höhe = die Profilseite, die steiler steht; senkrechte Kanäle (beide Seiten waagrecht): grössere als Breite
                if (Math.Abs(ax[2]) < 0.1 && Math.Abs(ay[2]) < 0.1)
                    return new IfcHlksQuerschnitt(null, Math.Round(Math.Max(x, y), 1), Math.Round(Math.Min(x, y), 1), "Profil IfcRectangleProfileDef (senkrecht: grössere Seite als Breite)");
                bool xSenkrecht = Math.Abs(ax[2]) > Math.Abs(ay[2]) + 1e-6;
                return xSenkrecht ? new IfcHlksQuerschnitt(null, Math.Round(y, 1), Math.Round(x, 1), "Profil IfcRectangleProfileDef")
                                  : new IfcHlksQuerschnitt(null, Math.Round(x, 1), Math.Round(y, 1), "Profil IfcRectangleProfileDef");
            }
            default:
                return null;
        }
    }

    double[] Schwerpunkt()
    {
        double x = 0, y = 0, z = 0;
        foreach (var p in Punkte) { x += p[0]; y += p[1]; z += p[2]; }
        return [x / Punkte.Count, y / Punkte.Count, z / Punkte.Count];
    }

    /// <summary>Richtung der grössten Streuung (Potenzverfahren auf der Kovarianzmatrix).</summary>
    double[]? Hauptrichtung(double[] c)
    {
        var k = new double[3, 3];
        foreach (var p in Punkte)
        {
            double[] d = [p[0] - c[0], p[1] - c[1], p[2] - c[2]];
            for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) k[i, j] += d[i] * d[j];
        }
        // Startvektor: Achse mit der grössten Streuung (vermeidet einen Start senkrecht zur Lösung)
        int s = k[0, 0] >= k[1, 1] && k[0, 0] >= k[2, 2] ? 0 : k[1, 1] >= k[2, 2] ? 1 : 2;
        double[] v = [s == 0 ? 1 : 0.1, s == 1 ? 1 : 0.1, s == 2 ? 1 : 0.1];
        for (int n = 0; n < 60; n++)
        {
            double[] w = [k[0, 0] * v[0] + k[0, 1] * v[1] + k[0, 2] * v[2], k[1, 0] * v[0] + k[1, 1] * v[1] + k[1, 2] * v[2], k[2, 0] * v[0] + k[2, 1] * v[1] + k[2, 2] * v[2]];
            var e = Einheit(w);
            if (e is null) return null;
            v = e;
        }
        return v;
    }

    /// <summary>Zwei Richtungen quer zu u: h waagrecht, w möglichst senkrecht (bei senkrechtem u: x und y).</summary>
    static (double[] H, double[] W) Quer(double[] u)
    {
        var h = Einheit([-u[1], u[0], 0]) ?? [1, 0, 0];
        var w = Einheit([u[1] * h[2] - u[2] * h[1], u[2] * h[0] - u[0] * h[2], u[0] * h[1] - u[1] * h[0]]) ?? [0, 1, 0];
        if (w[2] < 0) w = [-w[0], -w[1], -w[2]];
        return (h, w);
    }

    static double Skalar(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];

    static double[]? Einheit(double[] v)
    {
        double l = Math.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]);
        return l < 1e-9 ? null : [v[0] / l, v[1] / l, v[2] / l];
    }
}
