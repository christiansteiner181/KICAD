// Kurven und Profile der IFC-Geometrie (Modul 1b, B1): Kurven als Punktfolgen in mm (Bögen in Sehnen zerlegt, höchstens
// 10° je Sehne und höchstens 5 mm Abweichung), Profile als Flächen mit Aussenring und Löchern. Die parametrischen
// Profile (Rechteck, Kreis, I, L, T, U, C, Z …) folgen den Definitionen im IFC-Schema (Lage = Mitte der Begrenzung,
// Ausrundungen weggelassen); es sind Geometrie-Definitionen, keine Normwerte.

using Xbim.Common;
using Xbim.Ifc4.Interfaces;

namespace Hlks.Kern;

public sealed partial class IfcGeometrie
{
    /// <summary>Grösster Winkel je Sehne beim Zerlegen von Bögen (Bogenmass).</summary>
    const double SehneWinkel = 10 * Math.PI / 180;
    /// <summary>Grösste Abweichung der Sehne vom Bogen in mm.</summary>
    const double SehneFehler = 5;

    static int Sehnen(double radius, double winkel)
    {
        double w = SehneWinkel;
        if (radius > SehneFehler) w = Math.Min(w, 2 * Math.Acos(1 - SehneFehler / radius));
        return Math.Clamp((int)Math.Ceiling(Math.Abs(winkel) / w - 1e-9), 1, 360);
    }

    // ------------------------------------------------------------------ Kurven

    /// <summary>Punkte einer Kurve in mm (lokale Koordinaten, 2D-Kurven mit z = 0); gekruemmt wird gesetzt, wenn Bögen,
    /// Kreise oder Splines vorkommen. null, wenn die Kurve nicht verstanden wird.</summary>
    public List<double[]>? Kurve(IIfcCurve? c, ref bool gekruemmt)
    {
        switch (c)
        {
            case null:
                return null;
            case IIfcPolyline pl:
                return Bereinigt(pl.Points.Select(Datei.Punkt));
            case IIfcIndexedPolyCurve ip:
                return Indiziert(ip, ref gekruemmt);
            case IIfcCompositeCurve cc:
            {
                var aus = new List<double[]>();
                foreach (var s in cc.Segments)
                {
                    var teil = Kurve(s.ParentCurve, ref gekruemmt);
                    if (teil is null) return null;
                    if (!(bool)s.SameSense) teil.Reverse();
                    foreach (var p in teil) if (aus.Count == 0 || Abstand(aus[^1], p) > 1e-6) aus.Add(p);
                }
                return aus.Count >= 2 ? aus : null;
            }
            case IIfcTrimmedCurve tc:
                return Getrimmt(tc, ref gekruemmt);
            case IIfcCircle ci:
            {
                gekruemmt = true;
                var t = Affin.Aus(Datei.Achsen(ci.Position));
                double r = (double)ci.Radius * mm;
                return Bogen(t, r, r, 0, 2 * Math.PI);
            }
            case IIfcEllipse el:
            {
                gekruemmt = true;
                var t = Affin.Aus(Datei.Achsen(el.Position));
                return Bogen(t, (double)el.SemiAxis1 * mm, (double)el.SemiAxis2 * mm, 0, 2 * Math.PI);
            }
            case IIfcBSplineCurve bs:
                // Näherung über das Kontrollpolygon (Spline-Auswertung folgt bei Bedarf)
                gekruemmt = true;
                Vereinfachen();
                return Bereinigt(bs.ControlPointsList.Select(Datei.Punkt));
            default:
                Zaehlen(c);
                return null;
        }
    }

    static double Abstand(double[] a, double[] b) => Math.Sqrt((a[0] - b[0]) * (a[0] - b[0]) + (a[1] - b[1]) * (a[1] - b[1]) + (a[2] - b[2]) * (a[2] - b[2]));

    static List<double[]>? Bereinigt(IEnumerable<double[]> punkte)
    {
        var aus = new List<double[]>();
        foreach (var p in punkte) if (aus.Count == 0 || Abstand(aus[^1], p) > 1e-6) aus.Add(p);
        return aus.Count >= 2 ? aus : null;
    }

    /// <summary>Ellipsenbogen (Halbachsen a, b) im System t von Winkel w0 über die Länge w (Vorzeichen = Richtung).</summary>
    static List<double[]> Bogen(in Affin t, double a, double b, double w0, double w)
    {
        int n = Sehnen(Math.Max(a, b), w);
        var aus = new List<double[]>(n + 1);
        for (int i = 0; i <= n; i++)
        {
            double phi = w0 + w * i / n;
            aus.Add(t.Punkt([a * Math.Cos(phi), b * Math.Sin(phi), 0]));
        }
        return aus;
    }

    List<double[]>? Indiziert(IIfcIndexedPolyCurve ip, ref bool gekruemmt)
    {
        var k = Koordinaten(ip.Points);
        if (k.Count < 2) return null;
        if (ip.Segments.Count == 0) return Bereinigt(k);
        var aus = new List<double[]>();
        void Dazu(double[] p) { if (aus.Count == 0 || Abstand(aus[^1], p) > 1e-6) aus.Add(p); }
        foreach (var s in ip.Segments)
        {
            var art = s.GetType().Name;
            var idx = (s.Value as System.Collections.IEnumerable)?.Cast<object>().Select(x => (int)Zahl(x) - 1).ToList();
            if (idx is null || idx.Count < 2 || idx.Any(i => i < 0 || i >= k.Count)) return null;
            if (art.Contains("Arc", StringComparison.OrdinalIgnoreCase) && idx.Count == 3)
            {
                gekruemmt = true;
                foreach (var p in DreiPunktBogen(k[idx[0]], k[idx[1]], k[idx[2]])) Dazu(p);
            }
            else foreach (var i in idx) Dazu(k[i]);
        }
        return aus.Count >= 2 ? aus : null;
    }

    /// <summary>Kreisbogen durch drei Punkte (Anfang, Zwischenpunkt, Ende); auf einer Geraden: die Strecke.</summary>
    static List<double[]> DreiPunktBogen(double[] a, double[] b, double[] c)
    {
        // Ebene der drei Punkte: Achsen e1 (a→c), e2 (rechtwinklig in der Ebene)
        double[] ab = [b[0] - a[0], b[1] - a[1], b[2] - a[2]], ac = [c[0] - a[0], c[1] - a[1], c[2] - a[2]];
        var n = Kreuz(ab, ac);
        double nl = Math.Sqrt(n[0] * n[0] + n[1] * n[1] + n[2] * n[2]);
        if (nl < 1e-9) return [a, c];
        var e1 = Einheitsvektor(ac)!; n = [n[0] / nl, n[1] / nl, n[2] / nl];
        var e2 = Kreuz(n, e1);
        double[] P(double[] p) => [(p[0] - a[0]) * e1[0] + (p[1] - a[1]) * e1[1] + (p[2] - a[2]) * e1[2], (p[0] - a[0]) * e2[0] + (p[1] - a[1]) * e2[1] + (p[2] - a[2]) * e2[2]];
        var pb = P(b); var pc = P(c);
        // Mittelpunkt: liegt auf x = pc.x/2 (Mittelsenkrechte von a–c)
        double mx = pc[0] / 2;
        double my = (pb[0] * pb[0] + pb[1] * pb[1] - 2 * mx * pb[0]) / (2 * pb[1]);
        double r = Math.Sqrt(mx * mx + my * my);
        double wa = Math.Atan2(-my, -mx), wb = Math.Atan2(pb[1] - my, pb[0] - mx), wc = Math.Atan2(pc[1] - my, pc[0] - mx);
        // Richtung so, dass b zwischen a und c liegt
        double Norm(double w) { w %= 2 * Math.PI; return w < 0 ? w + 2 * Math.PI : w; }
        double laenge = Norm(wc - wa);
        if (Norm(wb - wa) > laenge) laenge -= 2 * Math.PI;
        int teile = Sehnen(r, laenge);
        var aus = new List<double[]>(teile + 1);
        for (int i = 0; i <= teile; i++)
        {
            double w = wa + laenge * i / teile, x = mx + r * Math.Cos(w), y = my + r * Math.Sin(w);
            aus.Add([a[0] + e1[0] * x + e2[0] * y, a[1] + e1[1] * x + e2[1] * y, a[2] + e1[2] * x + e2[2] * y]);
        }
        aus[0] = a; aus[^1] = c;
        return aus;
    }

    List<double[]>? Getrimmt(IIfcTrimmedCurve tc, ref bool gekruemmt)
    {
        (double[]? Punkt, double? Wert) Trim(IEnumerable<IIfcTrimmingSelect> t)
        {
            double[]? p = null; double? w = null;
            foreach (var x in t)
                if (x is IIfcCartesianPoint cp) p = Datei.Punkt(cp);
                else if (!double.IsNaN(Zahl(x))) w = Zahl(x);
            return (p, w);
        }
        var (p1, w1) = Trim(tc.Trim1);
        var (p2, w2) = Trim(tc.Trim2);
        bool kartesisch = tc.MasterRepresentation != IfcTrimmingPreference.PARAMETER;
        bool sinn = (bool)tc.SenseAgreement;
        switch (tc.BasisCurve)
        {
            case IIfcLine ln:
            {
                var o = Datei.Punkt(ln.Pnt);
                var r = IfcDatei.Richtung(ln.Dir?.Orientation) ?? [1, 0, 0];
                double rl = Math.Sqrt(r[0] * r[0] + r[1] * r[1] + r[2] * r[2]);
                double mag = ln.Dir is null ? 1 : (double)ln.Dir.Magnitude * mm;
                double[] AufLinie(double u) => [o[0] + r[0] / rl * mag * u, o[1] + r[1] / rl * mag * u, o[2] + r[2] / rl * mag * u];
                var a = (kartesisch || w1 is null) && p1 is not null ? p1 : w1 is double u1 ? AufLinie(u1) : null;
                var b = (kartesisch || w2 is null) && p2 is not null ? p2 : w2 is double u2 ? AufLinie(u2) : null;
                return a is null || b is null ? null : Bereinigt(sinn ? [a, b] : [b, a]);
            }
            case IIfcConic k:
            {
                gekruemmt = true;
                var t = Affin.Aus(Datei.Achsen(k.Position));
                double ra, rb;
                if (k is IIfcCircle ci) ra = rb = (double)ci.Radius * mm;
                else if (k is IIfcEllipse el) { ra = (double)el.SemiAxis1 * mm; rb = (double)el.SemiAxis2 * mm; }
                else { Zaehlen(k); return null; }
                // Winkel aus dem Punkt (im System der Kurve) oder aus dem Parameter (ebener Winkel der Datei)
                var inv = Umkehr(t);
                double? Winkel(double[]? p, double? w)
                {
                    if ((kartesisch || w is null) && p is not null)
                    {
                        var q = inv.Punkt(p);
                        return Math.Atan2(q[1] / rb, q[0] / ra);
                    }
                    return w is double x ? x * rad : null;
                }
                if (Winkel(p1, w1) is not double a1 || Winkel(p2, w2) is not double a2) return null;
                double laenge = sinn ? a2 - a1 : a1 - a2;
                laenge %= 2 * Math.PI;
                if (laenge <= 1e-12) laenge += 2 * Math.PI;
                var pts = Bogen(t, ra, rb, sinn ? a1 : a2, laenge);
                if (!sinn) pts.Reverse();
                return pts;
            }
            default:
            {
                // getrimmte Polylinie o. ä.: ganze Grundkurve (selten)
                Vereinfachen();
                return Kurve(tc.BasisCurve, ref gekruemmt);
            }
        }
    }

    /// <summary>Umkehrung einer starren Abbildung (Drehung + Verschiebung).</summary>
    static Affin Umkehr(in Affin t) => new(
        t.M00, t.M10, t.M20, -(t.M00 * t.T0 + t.M10 * t.T1 + t.M20 * t.T2),
        t.M01, t.M11, t.M21, -(t.M01 * t.T0 + t.M11 * t.T1 + t.M21 * t.T2),
        t.M02, t.M12, t.M22, -(t.M02 * t.T0 + t.M12 * t.T1 + t.M22 * t.T2));

    // ------------------------------------------------------------------ Profile

    /// <summary>Profilflächen einer Profil-Definition in mm (Lage Position bzw. Operator angewandt); null = nicht verstanden.</summary>
    public List<Profilflaeche>? Profil(IIfcProfileDef? p)
    {
        if (p is null) return null;
        if (p.ProfileType == IfcProfileTypeEnum.CURVE && p is not IIfcArbitraryClosedProfileDef) return null;
        bool gekruemmt = false;
        List<Profilflaeche>? Einfach(IIfcParameterizedProfileDef pp, List<double[]> aussen, params List<double[]>[] loecher) =>
            [Lage(pp.Position, new Profilflaeche(aussen, loecher.ToList()))];
        switch (p)
        {
            case IIfcRectangleHollowProfileDef rh:
            {
                double x = (double)rh.XDim * mm / 2, y = (double)rh.YDim * mm / 2, w = (double)rh.WallThickness * mm;
                return Einfach(rh, Rechteck(x, y), Umgekehrt(Rechteck(x - w, y - w)));
            }
            case IIfcRoundedRectangleProfileDef rr:
            {
                double x = (double)rr.XDim * mm / 2, y = (double)rr.YDim * mm / 2, r = Math.Min((double)rr.RoundingRadius * mm, Math.Min(x, y));
                var ring = new List<double[]>();
                foreach (var (cx, cy, w0) in new[] { (x - r, -y + r, -Math.PI / 2), (x - r, y - r, 0.0), (-x + r, y - r, Math.PI / 2), (-x + r, -y + r, Math.PI) })
                    ring.AddRange(Bogen(Affin.Verschiebung(cx, cy, 0), r, r, w0, Math.PI / 2).Select(q => new[] { q[0], q[1] }));
                return Einfach(rr, Ring2(ring));
            }
            case IIfcRectangleProfileDef r:
                return Einfach(r, Rechteck((double)r.XDim * mm / 2, (double)r.YDim * mm / 2));
            case IIfcCircleHollowProfileDef ch:
            {
                double r = (double)ch.Radius * mm, w = (double)ch.WallThickness * mm;
                return Einfach(ch, Kreis(r), Umgekehrt(Kreis(r - w)));
            }
            case IIfcCircleProfileDef c:
                return Einfach(c, Kreis((double)c.Radius * mm));
            case IIfcEllipseProfileDef el:
                return Einfach(el, Ring2(Bogen(Affin.Einheit, (double)el.SemiAxis1 * mm, (double)el.SemiAxis2 * mm, 0, 2 * Math.PI)));
            case IIfcIShapeProfileDef i:
            {
                double w = (double)i.OverallWidth * mm / 2, d = (double)i.OverallDepth * mm / 2, tw = (double)i.WebThickness * mm / 2, tf = (double)i.FlangeThickness * mm;
                return Einfach(i, P([-w, -d], [w, -d], [w, -d + tf], [tw, -d + tf], [tw, d - tf], [w, d - tf], [w, d], [-w, d], [-w, d - tf], [-tw, d - tf], [-tw, -d + tf], [-w, -d + tf]));
            }
            case IIfcLShapeProfileDef l:
            {
                double d = (double)l.Depth * mm / 2, w = (l.Width.HasValue ? (double)l.Width.Value : (double)l.Depth) * mm / 2, t = (double)l.Thickness * mm;
                return Einfach(l, P([-w, -d], [w, -d], [w, -d + t], [-w + t, -d + t], [-w + t, d], [-w, d]));
            }
            case IIfcTShapeProfileDef ts:
            {
                double d = (double)ts.Depth * mm / 2, fw = (double)ts.FlangeWidth * mm / 2, tw = (double)ts.WebThickness * mm / 2, tf = (double)ts.FlangeThickness * mm;
                return Einfach(ts, P([-tw, -d], [tw, -d], [tw, d - tf], [fw, d - tf], [fw, d], [-fw, d], [-fw, d - tf], [-tw, d - tf]));
            }
            case IIfcUShapeProfileDef u:
            {
                double d = (double)u.Depth * mm / 2, fw = (double)u.FlangeWidth * mm / 2, tw = (double)u.WebThickness * mm, tf = (double)u.FlangeThickness * mm;
                return Einfach(u, P([-fw, -d], [fw, -d], [fw, -d + tf], [-fw + tw, -d + tf], [-fw + tw, d - tf], [fw, d - tf], [fw, d], [-fw, d]));
            }
            case IIfcCShapeProfileDef cs:
            {
                double d = (double)cs.Depth * mm / 2, w = (double)cs.Width * mm / 2, t = (double)cs.WallThickness * mm, g = (double)cs.Girth * mm;
                return Einfach(cs, P([-w, -d], [w, -d], [w, -d + g], [w - t, -d + g], [w - t, -d + t], [-w + t, -d + t], [-w + t, d - t], [w - t, d - t], [w - t, d - g], [w, d - g], [w, d], [-w, d]));
            }
            case IIfcZShapeProfileDef zs:
            {
                double d = (double)zs.Depth * mm / 2, fw = (double)zs.FlangeWidth * mm, tw = (double)zs.WebThickness * mm / 2, tf = (double)zs.FlangeThickness * mm;
                return Einfach(zs, P([-fw + tw, -d], [tw, -d], [tw, d - tf], [fw - tw, d - tf], [fw - tw, d], [-tw, d], [-tw, -d + tf], [-fw + tw, -d + tf]));
            }
            case IIfcArbitraryProfileDefWithVoids v:
            {
                var aussen = Kurve(v.OuterCurve, ref gekruemmt);
                if (aussen is null) return null;
                var loecher = new List<List<double[]>>();
                foreach (var ic in v.InnerCurves)
                    if (Kurve(ic, ref gekruemmt) is { } l && l.Count >= 3) loecher.Add(l.Select(q => new[] { q[0], q[1] }).ToList());
                return Ordnen(aussen.Select(q => new[] { q[0], q[1] }).ToList(), loecher);
            }
            case IIfcArbitraryClosedProfileDef a:
            {
                var aussen = Kurve(a.OuterCurve, ref gekruemmt);
                return aussen is null ? null : Ordnen(aussen.Select(q => new[] { q[0], q[1] }).ToList(), []);
            }
            case IIfcDerivedProfileDef dp:
            {
                var eltern = Profil(dp.ParentProfile);
                if (eltern is null) return null;
                var t = Operator(dp.Operator);
                List<double[]> Abb(List<double[]> r) => r.Select(q => { var x = t.Punkt([q[0], q[1], 0]); return new[] { x[0], x[1] }; }).ToList();
                return eltern.Select(f => Ordnen(Abb(f.Aussen), f.Loecher.Select(Abb).ToList())).SelectMany(x => x ?? []).ToList();
            }
            case IIfcCompositeProfileDef cp:
            {
                var aus = new List<Profilflaeche>();
                foreach (var teil in cp.Profiles)
                    if (Profil(teil) is { } f) aus.AddRange(f);
                return aus.Count > 0 ? aus : null;
            }
            default:
                Zaehlen(p);
                return null;
        }
    }

    Profilflaeche Lage(IIfcAxis2Placement2D? pos, Profilflaeche f)
    {
        if (pos is null) return f;
        var t = Affin.Aus(Datei.Achsen(pos));
        List<double[]> Abb(List<double[]> r) => r.Select(q => { var x = t.Punkt([q[0], q[1], 0]); return new[] { x[0], x[1] }; }).ToList();
        return new Profilflaeche(Abb(f.Aussen), f.Loecher.Select(Abb).ToList());
    }

    static List<double[]> Rechteck(double x, double y) => [[-x, -y], [x, -y], [x, y], [-x, y]];

    static List<double[]> Kreis(double r) => Ring2(Bogen(Affin.Einheit, r, r, 0, 2 * Math.PI));

    static List<double[]> P(params double[][] p) => p.ToList();

    static List<double[]> Umgekehrt(List<double[]> r) { var x = new List<double[]>(r); x.Reverse(); return x; }

    /// <summary>Ring in 2D ohne doppelten Schlusspunkt.</summary>
    static List<double[]> Ring2(IEnumerable<double[]> punkte)
    {
        var aus = new List<double[]>();
        foreach (var p in punkte)
            if (aus.Count == 0 || Math.Abs(aus[^1][0] - p[0]) + Math.Abs(aus[^1][1] - p[1]) > 1e-6) aus.Add([p[0], p[1]]);
        if (aus.Count > 1 && Math.Abs(aus[0][0] - aus[^1][0]) + Math.Abs(aus[0][1] - aus[^1][1]) < 1e-6) aus.RemoveAt(aus.Count - 1);
        return aus;
    }

    /// <summary>Aussenring im Gegenuhrzeigersinn, Löcher im Uhrzeigersinn; zu kleine Ringe fallen weg.</summary>
    static List<Profilflaeche>? Ordnen(List<double[]> aussen, List<List<double[]>> loecher)
    {
        aussen = Ring2(aussen);
        if (aussen.Count < 3) return null;
        if (Polygone.Flaecheninhalt(aussen) < 0) aussen.Reverse();
        var l = new List<List<double[]>>();
        foreach (var r0 in loecher)
        {
            var r = Ring2(r0);
            if (r.Count < 3) continue;
            if (Polygone.Flaecheninhalt(r) > 0) r.Reverse();
            l.Add(r);
        }
        return [new Profilflaeche(aussen, l)];
    }

    // ------------------------------------------------------------------ Achse

    /// <summary>Achse aus der Repräsentation «Axis» in Projektkoordinaten (mm); gekruemmt bei Bögen. null ohne Achse.</summary>
    public List<double[]>? Achse(IIfcProduct p, out bool gekruemmt)
    {
        gekruemmt = false;
        var items = Elemente(p, ["Axis"]);
        if (items is null) return null;
        var t = Affin.Aus(Datei.Lage(p));
        var aus = new List<double[]>();
        foreach (var it in items)
        {
            if (it is not IIfcCurve c) continue;
            var k = Kurve(c, ref gekruemmt);
            if (k is null) continue;
            foreach (var q in k)
            {
                var w = t.Punkt(q);
                if (aus.Count == 0 || Abstand(aus[^1], w) > 1e-6) aus.Add(w);
            }
        }
        return aus.Count >= 2 ? aus : null;
    }
}
