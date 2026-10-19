// DWG/DXF lesen (Modul 1b, B3): Modellbereich mit ACadSharp lesen (DWG AC1014–AC1032, DXF) und in einfache Primitive
// umsetzen – Linien, Polylinien (Ausbuchtungen als Bögen), Bögen, Kreise, Ellipsen und Splines (als Polylinien),
// Text/MText, Bemassungen (ihr Block aus Linien und Text), Blöcke (Insert) aufgelöst mit Transformation, Schraffuren
// als Umriss. Alles in Zeichnungseinheiten; die Lage im Modell kommt erst beim Zeichnen (UnterlagenLage).
// Nicht dargestellt (3D, Bilder, Wipeout, MultiLeader …) wird gezählt und gemeldet, nie stillschweigend weggelassen.

using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using ACadSharp.Types.Units;
using CSMath;

namespace Hlks.Kern;

/// <summary>Affine 2D-Abbildung x' = A·x + C·y + E, y' = B·x + D·y + F (Blöcke, Spiegelungen, Drehungen).</summary>
readonly record struct Afin(double A, double B, double C, double D, double E, double F)
{
    public static readonly Afin Eins = new(1, 0, 0, 1, 0, 0);
    public static Afin Verschiebung(double x, double y) => new(1, 0, 0, 1, x, y);
    public static Afin Drehung(double rad) => new(Math.Cos(rad), Math.Sin(rad), -Math.Sin(rad), Math.Cos(rad), 0, 0);
    public static Afin Skalierung(double sx, double sy) => new(sx, 0, 0, sy, 0, 0);
    public static readonly Afin SpiegelX = new(-1, 0, 0, 1, 0, 0);

    /// <summary>Erst <paramref name="n"/>, dann diese Abbildung.</summary>
    public Afin Nach(Afin n) => new(A * n.A + C * n.B, B * n.A + D * n.B, A * n.C + C * n.D, B * n.C + D * n.D, A * n.E + C * n.F + E, B * n.E + D * n.F + F);
    public (double X, double Y) P(double x, double y) => (A * x + C * y + E, B * x + D * y + F);
    public double Det => A * D - B * C;
    public double Skala => Math.Sqrt(Math.Abs(Det));
    /// <summary>Gleichmässige Skalierung mit Drehung (und höchstens Spiegelung): Bögen bleiben Bögen.</summary>
    public bool Aehnlich
    {
        get
        {
            double q = A * A + B * B + C * C + D * D;
            return q > 0 && Math.Abs(A * A + B * B - C * C - D * D) < 1e-9 * q && Math.Abs(A * C + B * D) < 1e-9 * q;
        }
    }
}

public static class CadLeser
{
    /// <summary>Obergrenze der Primitive je Zeichnung (Schutz vor zerlegten Blockbäumen).</summary>
    const int MaxPrimitive = 2_000_000;
    const int MaxTiefe = 8;

    /// <summary>Liest eine DWG- oder DXF-Datei.</summary>
    public static CadZeichnung Lesen(string pfad)
    {
        CadDocument doc;
        try
        {
            doc = pfad.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase) ? DxfReader.Read(pfad) : DwgReader.Read(pfad);
        }
        catch (Exception ex) when (ex is not HlksFehler)
        {
            var art = pfad.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase) ? "DXF" : "DWG";
            throw new HlksFehler($"Die {art}-Datei «{Path.GetFileName(pfad)}» lässt sich nicht lesen: {ex.Message}",
                art == "DWG" ? "DWG-Fassungen AC1014 bis AC1032 (AutoCAD R14 bis 2018 und neuer) werden gelesen; ältere Dateien als DWG 2000 oder neuer bzw. als DXF speichern."
                             : "Die Datei als DXF (ASCII oder binär, R12 oder neuer) neu speichern.");
        }
        return new Sammler(doc).Ergebnis();
    }

    /// <summary>Setzt eine schon gelesene Zeichnung um (Tests, andere Leser).</summary>
    public static CadZeichnung Lesen(CadDocument doc) => new Sammler(doc).Ergebnis();

    /// <summary>Millimeter je Zeichnungseinheit nach INSUNITS; bekannt = false bei fehlender oder unbekannter Einheit (Annahme mm).</summary>
    public static (double Mm, bool Bekannt) MmJe(UnitsType u) => u switch
    {
        UnitsType.Millimeters => (1, true),
        UnitsType.Centimeters => (10, true),
        UnitsType.Decimeters => (100, true),
        UnitsType.Meters => (1000, true),
        UnitsType.Decameters => (10_000, true),
        UnitsType.Hectometers => (100_000, true),
        UnitsType.Kilometers => (1_000_000, true),
        UnitsType.Microns => (0.001, true),
        UnitsType.Nanometers => (1e-6, true),
        UnitsType.Angstroms => (1e-7, true),
        UnitsType.Inches => (25.4, true),
        UnitsType.Feet => (304.8, true),
        UnitsType.Yards => (914.4, true),
        UnitsType.Miles => (1_609_344, true),
        UnitsType.Mils => (0.0254, true),
        UnitsType.Microinches => (0.0000254, true),
        UnitsType.USSurveyInches => (1_200_000.0 / 3937 / 12, true),
        UnitsType.USSurveyFeet => (1_200_000.0 / 3937, true),
        UnitsType.USSurveyYards => (3_600_000.0 / 3937, true),
        UnitsType.USSurveyMiles => (6_336_000_000.0 / 3937, true),
        _ => (1, false),
    };

    sealed class Sammler
    {
        readonly CadDocument doc;
        readonly CadZeichnung z = new();
        readonly Dictionary<string, int> layerIndex = new(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> aus = new(StringComparer.OrdinalIgnoreCase);
        int anzahl;

        public Sammler(CadDocument doc)
        {
            this.doc = doc;
            foreach (var l in doc.Layers)
                if (!l.IsOn || l.Flags.HasFlag(LayerFlags.Frozen)) { aus.Add(l.Name); z.LayerAus.Add(l.Name); }
        }

        public CadZeichnung Ergebnis()
        {
            z.Version = doc.Header.Version.ToString();
            z.Einheit = doc.Header.InsUnits.ToString();
            (z.MmJeEinheit, z.EinheitBekannt) = MmJe(doc.Header.InsUnits);
            foreach (var e in doc.ModelSpace.Entities) Verarbeiten(e, Afin.Eins, null, 0);
            // Layer-Liste nur der benutzten Layer, nach Name
            var namen = layerIndex.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).Select(x => x.Key).ToList();
            var neu = namen.Select((n, i) => (n, i)).ToDictionary(x => layerIndex[x.n], x => x.i);
            z.Layer = namen;
            for (int i = 0; i < z.Primitive.Count; i++)
            {
                var p = z.Primitive[i];
                z.Primitive[i] = new CadPrimitiv { Art = p.Art, Layer = neu[p.Layer], W = p.W, Text = p.Text };
            }
            (z.Ausdehnung, z.Umriss) = Ausdehnungen(z.Primitive);
            return z;
        }

        // ---------- Sammeln ----------

        int Layer(string name)
        {
            if (!layerIndex.TryGetValue(name, out var i)) layerIndex[name] = i = layerIndex.Count;
            return i;
        }

        void Dazu(char art, string layer, double[] w, string? text = null)
        {
            if (w.Any(x => double.IsNaN(x) || double.IsInfinity(x))) return;
            if (anzahl >= MaxPrimitive) { z.Gekappt++; return; }
            anzahl++;
            z.Primitive.Add(new CadPrimitiv { Art = art, Layer = Layer(layer), W = w, Text = text });
        }

        void Nicht(string typ) => z.NichtDargestellt[typ] = z.NichtDargestellt.GetValueOrDefault(typ) + 1;

        void Linie(string layer, Afin t, IReadOnlyList<(double X, double Y)> pts, bool geschlossen)
        {
            if (geschlossen && pts.Count > 2 && pts[0] == pts[^1]) pts = pts.Take(pts.Count - 1).ToList();   // Schlusspunkt nicht doppeln
            if (pts.Count < 2) return;
            var w = new double[pts.Count * 2];
            for (int i = 0; i < pts.Count; i++) (w[2 * i], w[2 * i + 1]) = t.P(pts[i].X, pts[i].Y);
            if (pts.Count == 2 && !geschlossen) { if (w[0] == w[2] && w[1] == w[3]) return; Dazu('l', layer, w); }
            else Dazu(geschlossen ? 'g' : 'p', layer, w);
        }

        /// <summary>Bogen von von bis bis (Bogenmass, Gegenuhrzeigersinn) um (cx, cy).</summary>
        void Bogen(string layer, Afin t, double cx, double cy, double r, double von, double bis)
        {
            if (!(r > 0)) return;
            double sweep = bis - von;
            sweep = ((sweep % (2 * Math.PI)) + 2 * Math.PI) % (2 * Math.PI);
            if (sweep < 1e-9) sweep = 2 * Math.PI;
            if (t.Aehnlich)
            {
                var m = t.P(cx, cy);
                var s = t.P(cx + r * Math.Cos(von), cy + r * Math.Sin(von));
                var e = t.P(cx + r * Math.Cos(von + sweep), cy + r * Math.Sin(von + sweep));
                double a1 = Math.Atan2(s.Y - m.Y, s.X - m.X) * 180 / Math.PI, a2 = Math.Atan2(e.Y - m.Y, e.X - m.X) * 180 / Math.PI;
                if (t.Det < 0) (a1, a2) = (a2, a1);   // Spiegelung kehrt den Umlaufsinn um
                Dazu('b', layer, [m.X, m.Y, r * t.Skala, a1, a2]);
                return;
            }
            int n = Math.Max(4, (int)Math.Ceiling(sweep / (5 * Math.PI / 180)));
            var pts = new List<(double, double)>();
            for (int i = 0; i <= n; i++) pts.Add((cx + r * Math.Cos(von + sweep * i / n), cy + r * Math.Sin(von + sweep * i / n)));
            Linie(layer, t, pts, false);
        }

        void Kreis(string layer, Afin t, double cx, double cy, double r)
        {
            if (!(r > 0)) return;
            if (t.Aehnlich) { var m = t.P(cx, cy); Dazu('k', layer, [m.X, m.Y, r * t.Skala]); return; }
            var pts = new List<(double, double)>();
            for (int i = 0; i < 72; i++) pts.Add((cx + r * Math.Cos(2 * Math.PI * i / 72), cy + r * Math.Sin(2 * Math.PI * i / 72)));
            Linie(layer, t, pts, true);
        }

        /// <summary>Polylinie mit Ausbuchtungen: gerade Strecken als Polylinie, Ausbuchtungen als Bögen.</summary>
        void Polylinie(string layer, Afin t, IReadOnlyList<(double X, double Y, double Bulge)> v, bool geschlossen)
        {
            if (v.Count < 2) return;
            if (v.All(x => x.Bulge == 0)) { Linie(layer, t, v.Select(x => (x.X, x.Y)).ToList(), geschlossen); return; }
            var lauf = new List<(double, double)> { (v[0].X, v[0].Y) };
            int segmente = geschlossen ? v.Count : v.Count - 1;
            for (int i = 0; i < segmente; i++)
            {
                var a = v[i]; var b = v[(i + 1) % v.Count];
                if (a.Bulge == 0 || (a.X == b.X && a.Y == b.Y)) { lauf.Add((b.X, b.Y)); continue; }
                // Bogen: Mitte, Radius, Winkel aus Sehne und Bulge (b > 0: Gegenuhrzeigersinn)
                double dx = b.X - a.X, dy = b.Y - a.Y, c = Math.Sqrt(dx * dx + dy * dy), bu = a.Bulge;
                double r = c * (bu * bu + 1) / (4 * Math.Abs(bu)), s = Math.Abs(bu) * c / 2;
                double mx = (a.X + b.X) / 2, my = (a.Y + b.Y) / 2, vz = Math.Sign(bu) * (r - s);
                double cx = mx + -dy / c * vz, cy = my + dx / c * vz;
                double w1 = Math.Atan2(a.Y - cy, a.X - cx), w2 = Math.Atan2(b.Y - cy, b.X - cx);
                if (lauf.Count >= 2) Linie(layer, t, lauf, false);
                lauf = [(b.X, b.Y)];
                if (bu > 0) Bogen(layer, t, cx, cy, r, w1, w2); else Bogen(layer, t, cx, cy, r, w2, w1);
            }
            if (lauf.Count >= 2) Linie(layer, t, lauf, false);
        }

        void Text(string layer, Afin t, double x, double y, double hoehe, double rot, int ausr, string text)
        {
            text = text.Trim();
            if (text.Length == 0 || !(hoehe > 0)) return;
            if (text.Length > 1000) text = text[..1000];
            var p = t.P(x, y);
            var (dx, dy) = (Math.Cos(rot), Math.Sin(rot));
            double wink = Math.Atan2(t.B * dx + t.D * dy, t.A * dx + t.C * dy) * 180 / Math.PI;
            Dazu('t', layer, [p.X, p.Y, hoehe * t.Skala, wink, ausr], text);
        }

        // ---------- Entitäten ----------

        static string LayerName(Entity e, string? eltern)
        {
            var n = e.Layer?.Name ?? "0";
            return n == "0" && eltern is not null ? eltern : n;
        }

        /// <summary>Objektkoordinatensystem (OCS): Normale +z unverändert, −z spiegelt x; geneigte Normalen werden nicht gezeichnet.</summary>
        static bool Ocs(XYZ n, Afin t, out Afin ergebnis)
        {
            ergebnis = t;
            if (Math.Abs(n.X) > 1.0 / 64 || Math.Abs(n.Y) > 1.0 / 64) return false;
            if (n.Z < 0) ergebnis = t.Nach(Afin.SpiegelX);
            return true;
        }

        void Verarbeiten(Entity e, Afin t, string? eltern, int tiefe)
        {
            if (e.IsInvisible) return;
            var layer = LayerName(e, eltern);
            if (aus.Contains(layer)) return;
            try { Zeichnen(e, t, layer, tiefe); }
            catch (Exception ex) when (ex is not HlksFehler and not OutOfMemoryException)
            {
                Nicht(e.GetType().Name + " (Fehler)");
            }
        }

        void Zeichnen(Entity e, Afin t, string layer, int tiefe)
        {
            Afin o;
            switch (e)
            {
                case Line l:
                    Linie(layer, t, [(l.StartPoint.X, l.StartPoint.Y), (l.EndPoint.X, l.EndPoint.Y)], false);
                    break;
                case LwPolyline lw:
                    if (!Ocs(lw.Normal, t, out o)) { Nicht("LwPolyline (geneigt)"); break; }
                    Polylinie(layer, o, lw.Vertices.Select(v => (v.Location.X, v.Location.Y, v.Bulge)).ToList(), lw.IsClosed);
                    break;
                case Polyline2D p2:
                    if (!Ocs(p2.Normal, t, out o)) { Nicht("Polyline2D (geneigt)"); break; }
                    Polylinie(layer, o, p2.Vertices.Select(v => (v.Location.X, v.Location.Y, v.Bulge)).ToList(), p2.IsClosed);
                    break;
                case Polyline3D p3:
                    Linie(layer, t, p3.Vertices.Select(v => (v.Location.X, v.Location.Y)).ToList(), p3.IsClosed);
                    break;
                case Arc a:   // vor Circle: Arc ist ein Circle
                    if (!Ocs(a.Normal, t, out o)) { Nicht("Arc (geneigt)"); break; }
                    Bogen(layer, o, a.Center.X, a.Center.Y, a.Radius, a.StartAngle, a.EndAngle);
                    break;
                case Circle c:
                    if (!Ocs(c.Normal, t, out o)) { Nicht("Circle (geneigt)"); break; }
                    Kreis(layer, o, c.Center.X, c.Center.Y, c.Radius);
                    break;
                case Ellipse el:
                    Ellipse(layer, t, el);
                    break;
                case Spline sp:
                    Spline(layer, t, sp);
                    break;
                case TextEntity te:
                    if (!Ocs(te.Normal, t, out o)) { Nicht("Text (geneigt)"); break; }
                    TextEintrag(layer, o, te);
                    break;
                case MText mt:
                    MTextEintrag(layer, t, mt);
                    break;
                case Dimension d:
                    if (d.Block is null) { Nicht(d.GetType().Name + " (ohne Block)"); break; }
                    foreach (var c in d.Block.Entities) Verarbeiten(c, t, layer, tiefe + 1);
                    break;
                case Insert ins:
                    Einsetzen(layer, t, ins, tiefe);
                    break;
                case Hatch h:
                    if (!Ocs(h.Normal, t, out o)) { Nicht("Hatch (geneigt)"); break; }
                    foreach (var pfad in h.Paths)
                    {
                        var pts = pfad.GetPoints(16).Select(q => (q.X, q.Y)).ToList();
                        if (pts.Count >= 3) Linie(layer, o, pts, true);
                    }
                    break;
                case Solid so:
                    if (!Ocs(so.Normal, t, out o)) { Nicht("Solid (geneigt)"); break; }
                    // Reihenfolge der Ecken im DXF: 1, 2, 4, 3
                    Linie(layer, o, [(so.FirstCorner.X, so.FirstCorner.Y), (so.SecondCorner.X, so.SecondCorner.Y), (so.FourthCorner.X, so.FourthCorner.Y), (so.ThirdCorner.X, so.ThirdCorner.Y)], true);
                    break;
                case Face3D f:
                    Linie(layer, t, [(f.FirstCorner.X, f.FirstCorner.Y), (f.SecondCorner.X, f.SecondCorner.Y), (f.ThirdCorner.X, f.ThirdCorner.Y), (f.FourthCorner.X, f.FourthCorner.Y)], true);
                    break;
                case Leader le:
                    Linie(layer, t, le.Vertices.Select(v => (v.X, v.Y)).ToList(), false);
                    break;
                default:
                    Nicht(e.GetType().Name);
                    break;
            }
        }

        void Ellipse(string layer, Afin t, Ellipse el)
        {
            var a = el.MajorAxisEndPoint;
            double ax = a.X, ay = a.Y, r = el.RadiusRatio;
            // Nebenachse senkrecht zur Hauptachse; bei Normale −z läuft der Parameter im Uhrzeigersinn
            double bx = (el.Normal.Z < 0 ? ay : -ay) * r, by = (el.Normal.Z < 0 ? -ax : ax) * r;
            double t0 = el.StartParameter, t1 = el.EndParameter;
            if (el.IsFullEllipse || t1 <= t0) { if (el.IsFullEllipse) { t0 = 0; t1 = 2 * Math.PI; } else t1 += 2 * Math.PI; }
            int n = Math.Max(8, (int)Math.Ceiling(64 * (t1 - t0) / (2 * Math.PI)));
            var pts = new List<(double, double)>();
            for (int i = 0; i <= n; i++)
            {
                double w = t0 + (t1 - t0) * i / n;
                pts.Add((el.Center.X + Math.Cos(w) * ax + Math.Sin(w) * bx, el.Center.Y + Math.Cos(w) * ay + Math.Sin(w) * by));
            }
            bool voll = el.IsFullEllipse || Math.Abs(t1 - t0 - 2 * Math.PI) < 1e-9;
            if (voll) pts.RemoveAt(pts.Count - 1);
            Linie(layer, t, pts, voll);
        }

        void Spline(string layer, Afin t, Spline sp)
        {
            List<XYZ>? punkte = null;
            try
            {
                int vorgabe = Math.Clamp((sp.ControlPoints.Count + sp.FitPoints.Count) * 8, 32, 400);
                punkte = sp.PolygonalVertexes(vorgabe);
            }
            catch (Exception) { }
            if (punkte is { Count: >= 2 }) { Linie(layer, t, punkte.Select(v => (v.X, v.Y)).ToList(), sp.IsClosed); return; }
            // Notlösung ohne Kontrollpunkte: Kurve durch die Anpassungspunkte (Catmull-Rom), sonst das Kontrollpolygon
            if (sp.FitPoints.Count >= 3) { Linie(layer, t, CatmullRom(sp.FitPoints), false); return; }
            Linie(layer, t, (sp.FitPoints.Count >= 2 ? sp.FitPoints : sp.ControlPoints).Select(v => (v.X, v.Y)).ToList(), sp.IsClosed);
        }

        static List<(double, double)> CatmullRom(List<XYZ> f)
        {
            var aus = new List<(double, double)>();
            for (int i = 0; i < f.Count - 1; i++)
            {
                XYZ p0 = f[Math.Max(i - 1, 0)], p1 = f[i], p2 = f[i + 1], p3 = f[Math.Min(i + 2, f.Count - 1)];
                for (int s = 0; s < 8; s++)
                {
                    double u = s / 8.0, u2 = u * u, u3 = u2 * u;
                    double X(double a, double b, double c, double d) => 0.5 * (2 * b + (-a + c) * u + (2 * a - 5 * b + 4 * c - d) * u2 + (-a + 3 * b - 3 * c + d) * u3);
                    aus.Add((X(p0.X, p1.X, p2.X, p3.X), X(p0.Y, p1.Y, p2.Y, p3.Y)));
                }
            }
            aus.Add((f[^1].X, f[^1].Y));
            return aus;
        }

        void TextEintrag(string layer, Afin o, TextEntity te)
        {
            var text = CadText.Einfach(te.Value);
            double h = te.Height > 0 ? te.Height : te.Style?.Height ?? 0;
            var hor = te.HorizontalAlignment;
            var ver = te.VerticalAlignment;
            bool ausgerichtet = hor is TextHorizontalAlignment.Center or TextHorizontalAlignment.Right or TextHorizontalAlignment.Middle || ver != TextVerticalAlignmentType.Baseline;
            var anker = ausgerichtet && hor is not (TextHorizontalAlignment.Aligned or TextHorizontalAlignment.Fit) ? te.AlignmentPoint : te.InsertPoint;
            int ausr = hor switch { TextHorizontalAlignment.Center or TextHorizontalAlignment.Middle => 1, TextHorizontalAlignment.Right => 2, _ => 0 };
            // Grundlinie: Mitte = halbe Höhe, Oben = ganze Höhe unter dem Ankerpunkt (in Schriftrichtung gedreht)
            double ab = hor == TextHorizontalAlignment.Middle ? 0.5 * h : ver switch { TextVerticalAlignmentType.Middle => 0.5 * h, TextVerticalAlignmentType.Top => h, _ => 0 };
            double x = anker.X + Math.Sin(te.Rotation) * ab, y = anker.Y - Math.Cos(te.Rotation) * ab;
            if (h <= 0) { Nicht("Text (ohne Höhe)"); return; }
            Text(layer, o, x, y, h, te.Rotation, ausr, text);
        }

        void MTextEintrag(string layer, Afin t, MText mt)
        {
            var zeilen = CadText.MText(mt.Value);
            while (zeilen.Count > 0 && zeilen[^1].Trim().Length == 0) zeilen.RemoveAt(zeilen.Count - 1);
            if (zeilen.Count == 0) return;
            double h = mt.Height > 0 ? mt.Height : mt.Style?.Height ?? 0;
            if (h <= 0) { Nicht("MText (ohne Höhe)"); return; }
            double abstand = h * 5.0 / 3.0 * (mt.LineSpacing > 0 ? mt.LineSpacing : 1);
            var at = mt.AttachmentPoint;
            int ausr = at switch { AttachmentPointType.TopCenter or AttachmentPointType.MiddleCenter or AttachmentPointType.BottomCenter => 1,
                                   AttachmentPointType.TopRight or AttachmentPointType.MiddleRight or AttachmentPointType.BottomRight => 2, _ => 0 };
            double gesamt = (zeilen.Count - 1) * abstand + h;
            // erste Grundlinie relativ zum Einfügepunkt, lokal (+y = Schrift nach oben); weitere Zeilen darunter
            double erste = at switch
            {
                AttachmentPointType.TopLeft or AttachmentPointType.TopCenter or AttachmentPointType.TopRight => -h,
                AttachmentPointType.MiddleLeft or AttachmentPointType.MiddleCenter or AttachmentPointType.MiddleRight => gesamt / 2 - h,
                _ => (zeilen.Count - 1) * abstand,
            };
            double rot = mt.Rotation, cs = Math.Cos(rot), sn = Math.Sin(rot);
            for (int i = 0; i < zeilen.Count; i++)
            {
                double dy = erste - i * abstand;   // lokal, +y = Schriftrichtung nach oben
                Text(layer, t, mt.InsertPoint.X - sn * dy, mt.InsertPoint.Y + cs * dy, h, rot, ausr, zeilen[i]);
            }
        }

        void Einsetzen(string layer, Afin t, Insert ins, int tiefe)
        {
            var block = ins.Block;
            if (block is null || tiefe >= MaxTiefe) { Nicht(block is null ? "Insert (Block fehlt)" : "Insert (zu tief verschachtelt)"); return; }
            if (!Ocs(ins.Normal, t, out var o)) { Nicht("Insert (geneigt)"); return; }
            double sx = ins.XScale == 0 ? 1 : ins.XScale, sy = ins.YScale == 0 ? 1 : ins.YScale;
            var basis = block.BlockEntity?.BasePoint ?? new XYZ(0, 0, 0);
            int zeilen = Math.Max(1, (int)ins.RowCount), spalten = Math.Max(1, (int)ins.ColumnCount);
            if ((long)zeilen * spalten > 10_000) { Nicht("Insert (Reihe zu gross)"); return; }
            for (int r = 0; r < zeilen; r++)
                for (int c = 0; c < spalten; c++)
                {
                    var tt = o.Nach(Afin.Verschiebung(ins.InsertPoint.X, ins.InsertPoint.Y))
                              .Nach(Afin.Drehung(ins.Rotation))
                              .Nach(Afin.Verschiebung(c * ins.ColumnSpacing, r * ins.RowSpacing))
                              .Nach(Afin.Skalierung(sx, sy))
                              .Nach(Afin.Verschiebung(-basis.X, -basis.Y));
                    foreach (var e in block.Entities) Verarbeiten(e, tt, layer, tiefe + 1);
                }
            if (ins.HasAttributes)
                foreach (var a in ins.Attributes)
                {
                    if (a.IsInvisible || a.Flags.HasFlag(AttributeFlags.Hidden) || aus.Contains(a.Layer?.Name ?? "0")) continue;
                    if (!Ocs(a.Normal, t, out var oa)) continue;
                    TextEintrag(a.Layer?.Name ?? layer, oa, a);
                }
        }

        // ---------- Ausdehnung ----------

        static (double[]? Alles, double[]? Haupt) Ausdehnungen(List<CadPrimitiv> prims)
        {
            if (prims.Count == 0) return (null, null);
            var boxen = new List<double[]>(prims.Count);
            foreach (var p in prims)
            {
                double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
                void P(double x, double y) { x0 = Math.Min(x0, x); y0 = Math.Min(y0, y); x1 = Math.Max(x1, x); y1 = Math.Max(y1, y); }
                switch (p.Art)
                {
                    case 'b' or 'k': P(p.W[0] - p.W[2], p.W[1] - p.W[2]); P(p.W[0] + p.W[2], p.W[1] + p.W[2]); break;
                    case 't': P(p.W[0], p.W[1]); break;
                    default: for (int i = 0; i + 1 < p.W.Length; i += 2) P(p.W[i], p.W[i + 1]); break;
                }
                boxen.Add([x0, y0, x1, y1]);
            }
            double[] Vereinigung(IEnumerable<double[]> b) =>
                b.Aggregate(new[] { double.MaxValue, double.MaxValue, double.MinValue, double.MinValue },
                    (a, q) => [Math.Min(a[0], q[0]), Math.Min(a[1], q[1]), Math.Max(a[2], q[2]), Math.Max(a[3], q[3])]);
            var alles = Vereinigung(boxen);

            // Hauptteil: Mittelpunkte zwischen dem 5. und 95. Perzentil, dazu ihre Breite als Rand auf jeder Seite
            // (weit abgelegene Einzelteile – Legenden, Planköpfe – verzerren sonst den Umriss).
            double Lo(Func<double[], double> f, double q) { var s = boxen.Select(f).OrderBy(x => x).ToArray(); return s[(int)Math.Min(s.Length - 1, q * s.Length)]; }
            double mxLo = Lo(b => (b[0] + b[2]) / 2, 0.05), mxHi = Lo(b => (b[0] + b[2]) / 2, 0.95);
            double myLo = Lo(b => (b[1] + b[3]) / 2, 0.05), myHi = Lo(b => (b[1] + b[3]) / 2, 0.95);
            double wx = mxHi - mxLo, wy = myHi - myLo;
            var haupt = boxen.Where(b => (b[0] + b[2]) / 2 >= mxLo - wx && (b[0] + b[2]) / 2 <= mxHi + wx
                                         && (b[1] + b[3]) / 2 >= myLo - wy && (b[1] + b[3]) / 2 <= myHi + wy).ToList();
            return (alles, haupt.Count > 0 ? Vereinigung(haupt) : alles);
        }
    }
}
