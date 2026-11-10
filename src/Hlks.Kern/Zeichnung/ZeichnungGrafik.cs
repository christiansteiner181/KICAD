// 2D-CAD «Zeichnung», Stufe 1: Darstellung eines Layouts als Plangrafik-Primitive (dieselben, die PdfZeichner und der Bildschirm
// zeichnen) – mit Farbe, Linienstärke und Linienart aus Objekt, Layer bzw. Block (VonLayer/VonBlock aufgelöst).
//   Modellbereich   alle Objekte in Zeichnungseinheiten
//   Papierbereich   Objekte des Layouts, dazu je Ansichtsfenster der Modellbereich im Massstab des Fensters, auf das Fenster zugeschnitten
// Vereinfachungen (stehen im Bericht bzw. in «nichtDargestellt»): Linienstärken auf die fünf Stifte des Kerns gerundet, Linienarten als
// Strich/Strichpunkt/Punkt, Schriften als Arial, Polylinienbreiten und Rasterbilder nicht, externe Referenzen nur wenn eingebunden.

using ACadSharp;
using ACadSharp.Blocks;
using ACadSharp.Entities;
using ACadSharp.Tables;
using CSMath;

namespace Hlks.Kern;

/// <summary>Ein Teil der Zeichnung: Primitive in Layout-Koordinaten, wahlweise auf ein Rechteck zugeschnitten (Ansichtsfenster).</summary>
public sealed class ZeichnungTeil
{
    /// <summary>Zuschnitt [x0, y0, x1, y1] in Layout-Koordinaten; null = kein Zuschnitt.</summary>
    public double[]? Fenster { get; init; }
    public List<Primitiv> Primitive { get; } = [];
}

public sealed class ZeichnungBild
{
    public List<ZeichnungTeil> Teile { get; } = [];
    public SortedDictionary<string, int> NichtDargestellt { get; } = new(StringComparer.Ordinal);
    /// <summary>Alles Gezeichnete [x0, y0, x1, y1]; null = leer.</summary>
    public double[]? Ausdehnung { get; set; }
    /// <summary>Hauptteil ohne weit abgelegene Einzelteile.</summary>
    public double[]? Umriss { get; set; }
    public int Anzahl => Teile.Sum(t => t.Primitive.Count);
}

public static class ZeichnungGrafik
{
    const int MaxPrimitive = 3_000_000, MaxTiefe = 8, MaxSchraffurLinien = 20_000;
    /// <summary>Verhältnis Versalhöhe zu Schriftgrad bei Arial: die DWG nennt die Höhe der Grossbuchstaben, PdfZeichner den Schriftgrad.</summary>
    const double Versal = 0.716;

    /// <param name="papierMmJeEinheit">Papier-mm je Layout-Einheit der Ausgabe (für die Schriftgrösse).</param>
    /// <param name="farbig">false: alles in Planfarbe (schwarz auf Papier).</param>
    public static ZeichnungBild Erzeugen(Zeichnungsdatei z, ACadSharp.Objects.Layout? layout, double papierMmJeEinheit, bool farbig = true)
    {
        var lauf = new Lauf(z, papierMmJeEinheit, farbig);
        var haupt = new ZeichnungTeil();
        lauf.Bild.Teile.Add(haupt);
        var fenster = new List<Viewport>();
        foreach (var e in z.Objekte(layout))
        {
            if (e is Viewport vp) { if (layout is not null && !vp.RepresentsPaper && vp.Id != 1) fenster.Add(vp); continue; }
            lauf.Verarbeiten(e, haupt, Kontext.Oben);
        }
        // Ansichtsfenster: Modellbereich im Massstab des Fensters, unter den Objekten des Blatts
        int stelle = 0;
        foreach (var vp in fenster)
        {
            if (!(vp.ViewHeight > 0) || !(vp.Height > 0) || !(vp.Width > 0)) { lauf.Nicht("Viewport (ohne Grösse)"); continue; }
            double k = vp.Height / vp.ViewHeight;
            var t = Afin.Verschiebung(vp.Center.X, vp.Center.Y).Nach(Afin.Skalierung(k, k)).Nach(Afin.Drehung(vp.TwistAngle))
                        .Nach(Afin.Verschiebung(-(vp.ViewTarget.X + vp.ViewCenter.X), -(vp.ViewTarget.Y + vp.ViewCenter.Y)));
            var teil = new ZeichnungTeil { Fenster = [vp.Center.X - vp.Width / 2, vp.Center.Y - vp.Height / 2, vp.Center.X + vp.Width / 2, vp.Center.Y + vp.Height / 2] };
            var gefroren = vp.FrozenLayers.Select(l => l.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var ctx = Kontext.Oben with { T = t, Gefroren = gefroren.Count > 0 ? gefroren : null };
            foreach (var e in z.Objekte(null)) if (e is not Viewport) lauf.Verarbeiten(e, teil, ctx);
            lauf.Bild.Teile.Insert(stelle++, teil);
        }
        lauf.Abschliessen();
        return lauf.Bild;
    }

    /// <summary>Zustand beim Abstieg in Blöcke: Abbildung, Layer und Eigenschaften der Blockreferenz (für Layer 0 und VonBlock).</summary>
    sealed record Kontext(Afin T, Layer? Eltern, string? FarbeBlock, double MmBlock, string LinieBlock, HashSet<string>? Gefroren, int Tiefe)
    {
        public static readonly Kontext Oben = new(Afin.Eins, null, null, 0.25, "voll", null, 0);
    }

    sealed class Lauf(Zeichnungsdatei z, double s, bool farbig)
    {
        public ZeichnungBild Bild { get; } = new();
        readonly Dictionary<(string?, string, string), Stil> stile = [];
        readonly Dictionary<string, Stil> fuellungen = [];
        readonly List<double[]> boxen = [];
        int anzahl;

        public void Nicht(string typ) => Bild.NichtDargestellt[typ] = Bild.NichtDargestellt.GetValueOrDefault(typ) + 1;

        // ---------- Eigenschaften ----------

        /// <param name="fuellung">Füllung: Weiss bleibt weiss (deckt wie in AutoCAD ab), statt zur Planfarbe zu werden.</param>
        static string? Hex(Color c, bool fuellung = false)
        {
            ReadOnlySpan<byte> rgb;
            if (c.IsTrueColor) rgb = c.GetTrueColorRgb();
            else
            {
                if (c.Index is <= 0 or 7 or > 255) return null;   // 7 = schwarz/weiss je Hintergrund: Planfarbe
                rgb = Color.GetIndexRGB((byte)c.Index);
            }
            if (rgb.Length < 3) return null;
            if (rgb[0] >= 250 && rgb[1] >= 250 && rgb[2] >= 250) return fuellung ? "#ffffff" : null;   // weisse Linien wären auf Papier unsichtbar
            return $"#{rgb[0]:x2}{rgb[1]:x2}{rgb[2]:x2}";
        }

        static Layer? LayerVon(Entity e, Kontext k) => e.Layer is { } l && !(l.Name == "0" && k.Eltern is not null) ? l : k.Eltern ?? e.Layer;

        string? Farbe(Entity e, Layer? layer, Kontext k, bool fuellung = false)
        {
            if (!farbig && !fuellung) return null;
            var c = e.Color;
            if (c.IsByBlock) return k.FarbeBlock;
            if (c.IsByLayer) { if (layer is null) return null; c = layer.Color; }
            var hex = Hex(c, fuellung);
            return farbig || hex is null or "#ffffff" ? hex : "#c8c8c8";   // schwarz-weiss: farbige Füllungen hellgrau
        }

        static double Mm(Entity e, Layer? layer, Kontext k)
        {
            var w = e.LineWeight;
            if (w == LineWeightType.ByBlock) return k.MmBlock;
            if (w == LineWeightType.ByLayer) w = layer?.LineWeight ?? LineWeightType.Default;
            return (int)w < 0 ? 0.25 : (int)w / 100.0;   // Vorgabe von AutoCAD: 0.25 mm
        }

        static string Linie(Entity e, Layer? layer, Kontext k)
        {
            var lt = e.LineType;
            if (lt is null) return "voll";
            if (lt.Name.Equals("ByBlock", StringComparison.OrdinalIgnoreCase)) return k.LinieBlock;
            if (lt.Name.Equals("ByLayer", StringComparison.OrdinalIgnoreCase)) lt = layer?.LineType;
            if (lt is null) return "voll";
            var seg = lt.Segments.ToList();
            if (seg.Count < 2) return "voll";
            bool punkt = seg.Any(x => x.Length == 0), strich = seg.Any(x => x.Length > 0);
            return punkt && strich ? "strichpunkt" : punkt ? "punkt" : "strich";
        }

        /// <summary>Linienstärke auf den nächsten Stift des Kerns (0.18 · 0.20 · 0.35 · 0.50 · 0.70 mm).</summary>
        static string Stift(double mm) => mm <= 0.19 ? Stifte.Duenn : mm <= 0.27 ? Stifte.Hilf : mm <= 0.42 ? Stifte.Mittel : mm <= 0.60 ? Stifte.Kontur : Stifte.Dick;

        Stil StilVon(Entity e, Layer? layer, Kontext k)
        {
            var key = (Farbe(e, layer, k), Stift(Mm(e, layer, k)), Linie(e, layer, k));
            if (!stile.TryGetValue(key, out var st)) stile[key] = st = new Stil { Farbe = key.Item1, Stift = key.Item2, Linie = key.Item3 };
            return st;
        }

        Stil Fuellung(string? farbe)
        {
            var f = farbe ?? "#000000";
            if (!fuellungen.TryGetValue(f, out var st)) fuellungen[f] = st = new Stil { Stift = null, Fuellung = f, Farbe = f };
            return st;
        }

        // ---------- Ausgabe ----------

        bool Platz()
        {
            if (anzahl >= MaxPrimitive) { Nicht("über der Obergrenze"); return false; }
            anzahl++;
            letzte = null;
            return true;
        }

        /// <summary>Box des Primitivs, das gerade entsteht (mehrere Ringe: Vereinigung); Aufnehmen wertet sie aus.</summary>
        double[]? letzte;

        void Box(double x0, double y0, double x1, double y1)
        {
            double a = Math.Min(x0, x1), b = Math.Min(y0, y1), c = Math.Max(x0, x1), d = Math.Max(y0, y1);
            letzte = letzte is null ? [a, b, c, d] : [Math.Min(letzte[0], a), Math.Min(letzte[1], b), Math.Max(letzte[2], c), Math.Max(letzte[3], d)];
        }

        /// <summary>Primitiv aufnehmen; in einem Ansichtsfenster nur, was das Fenster berührt (Ausdehnung auf das Fenster begrenzt).</summary>
        void Aufnehmen(ZeichnungTeil teil, Primitiv p)
        {
            var b = letzte;
            letzte = null;
            if (b is not null && teil.Fenster is { } f)
            {
                if (b[2] < f[0] || b[0] > f[2] || b[3] < f[1] || b[1] > f[3]) { anzahl--; return; }
                b = [Math.Max(b[0], f[0]), Math.Max(b[1], f[1]), Math.Min(b[2], f[2]), Math.Min(b[3], f[3])];
            }
            if (b is not null) boxen.Add(b);
            teil.Primitive.Add(p);   // einzige Stelle
        }

        static bool Gueltig(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

        List<double[]>? Punkte(Afin t, IReadOnlyList<(double X, double Y)> pts)
        {
            var aus = new List<double[]>(pts.Count + 1);
            double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
            foreach (var q in pts)
            {
                var (x, y) = t.P(q.X, q.Y);
                if (!Gueltig(x) || !Gueltig(y)) return null;
                aus.Add([x, y]);
                x0 = Math.Min(x0, x); y0 = Math.Min(y0, y); x1 = Math.Max(x1, x); y1 = Math.Max(y1, y);
            }
            Box(x0, y0, x1, y1);
            return aus;
        }

        void Linien(ZeichnungTeil teil, Stil st, Afin t, IReadOnlyList<(double X, double Y)> pts, bool geschlossen)
        {
            if (pts.Count < 2 || !Platz()) return;
            if (Punkte(t, pts) is not { } p) return;
            if (geschlossen && (p[0][0] != p[^1][0] || p[0][1] != p[^1][1])) p.Add(p[0]);
            Aufnehmen(teil, new Primitiv { Art = p.Count == 2 ? "linie" : "polylinie", Punkte = p, Stil = st });
        }

        void Flaeche(ZeichnungTeil teil, Stil st, Afin t, List<List<(double X, double Y)>> ringe)
        {
            ringe = ringe.Where(r => r.Count >= 3).ToList();
            if (ringe.Count == 0 || !Platz()) return;
            var alle = ringe.Select(r => Punkte(t, r)).ToList();
            if (alle.Any(r => r is null)) return;
            Aufnehmen(teil, new Primitiv { Art = "polygon", Punkte = alle[0]!, Loecher = alle.Count > 1 ? alle.Skip(1).Select(r => r!).ToList() : null, Stil = st });
        }

        void Bogen(ZeichnungTeil teil, Stil st, Afin t, double cx, double cy, double r, double von, double bis)
        {
            if (!(r > 0)) return;
            double sweep = ((bis - von) % (2 * Math.PI) + 2 * Math.PI) % (2 * Math.PI);
            if (sweep < 1e-9) sweep = 2 * Math.PI;
            if (t.Aehnlich)
            {
                if (!Platz()) return;
                var m = t.P(cx, cy);
                var a = t.P(cx + r * Math.Cos(von), cy + r * Math.Sin(von));
                var b = t.P(cx + r * Math.Cos(von + sweep), cy + r * Math.Sin(von + sweep));
                double a1 = Math.Atan2(a.Y - m.Y, a.X - m.X) * 180 / Math.PI, a2 = Math.Atan2(b.Y - m.Y, b.X - m.X) * 180 / Math.PI;
                if (t.Det < 0) (a1, a2) = (a2, a1);
                double rr = r * t.Skala;
                if (!Gueltig(m.X) || !Gueltig(m.Y) || !Gueltig(rr)) return;
                Box(m.X - rr, m.Y - rr, m.X + rr, m.Y + rr);
                Aufnehmen(teil, new Primitiv { Art = "bogen", Punkte = [[m.X, m.Y]], Radius = rr, Von = a1, Bis = a2, Stil = st });
                return;
            }
            int n = Math.Max(4, (int)Math.Ceiling(sweep / (5 * Math.PI / 180)));
            var pts = new List<(double, double)>(n + 1);
            for (int i = 0; i <= n; i++) pts.Add((cx + r * Math.Cos(von + sweep * i / n), cy + r * Math.Sin(von + sweep * i / n)));
            Linien(teil, st, t, pts, false);
        }

        void Kreis(ZeichnungTeil teil, Stil st, Afin t, double cx, double cy, double r)
        {
            if (!(r > 0)) return;
            if (t.Aehnlich)
            {
                if (!Platz()) return;
                var m = t.P(cx, cy); double rr = r * t.Skala;
                if (!Gueltig(m.X) || !Gueltig(m.Y) || !Gueltig(rr)) return;
                Box(m.X - rr, m.Y - rr, m.X + rr, m.Y + rr);
                Aufnehmen(teil, new Primitiv { Art = "kreis", Punkte = [[m.X, m.Y]], Radius = rr, Stil = st });
                return;
            }
            var pts = new List<(double, double)>(72);
            for (int i = 0; i < 72; i++) pts.Add((cx + r * Math.Cos(2 * Math.PI * i / 72), cy + r * Math.Sin(2 * Math.PI * i / 72)));
            Linien(teil, st, t, pts, true);
        }

        void Polylinie(ZeichnungTeil teil, Stil st, Afin t, IReadOnlyList<(double X, double Y, double Bulge)> v, bool geschlossen)
        {
            if (v.Count < 2) return;
            if (v.All(x => x.Bulge == 0)) { Linien(teil, st, t, v.Select(x => (x.X, x.Y)).ToList(), geschlossen); return; }
            var lauf = new List<(double, double)> { (v[0].X, v[0].Y) };
            int segmente = geschlossen ? v.Count : v.Count - 1;
            for (int i = 0; i < segmente; i++)
            {
                var a = v[i]; var b = v[(i + 1) % v.Count];
                if (a.Bulge == 0 || (a.X == b.X && a.Y == b.Y)) { lauf.Add((b.X, b.Y)); continue; }
                var (cx, cy, r, w1, w2) = ZeichnungGeometrie.BogenAusBulge(a.X, a.Y, b.X, b.Y, a.Bulge);
                if (lauf.Count >= 2) Linien(teil, st, t, lauf, false);
                lauf = [(b.X, b.Y)];
                if (a.Bulge > 0) Bogen(teil, st, t, cx, cy, r, w1, w2); else Bogen(teil, st, t, cx, cy, r, w2, w1);
            }
            if (lauf.Count >= 2) Linien(teil, st, t, lauf, false);
        }

        void Text(ZeichnungTeil teil, Stil st, Afin t, double x, double y, double hoehe, double rot, int ausr, string text)
        {
            text = text.Trim();
            if (text.Length == 0 || !(hoehe > 0) || !Platz()) return;
            if (text.Length > 2000) text = text[..2000];
            var p = t.P(x, y);
            var (dx, dy) = (Math.Cos(rot), Math.Sin(rot));
            double wink = Math.Atan2(t.B * dx + t.D * dy, t.A * dx + t.C * dy) * 180 / Math.PI;
            double h = hoehe * t.Skala;
            if (!Gueltig(p.X) || !Gueltig(p.Y) || !Gueltig(h)) return;
            Box(p.X, p.Y, p.X, p.Y);
            Aufnehmen(teil, new Primitiv
            {
                Art = "text", Punkte = [[p.X, p.Y]], Text = text, Groesse = h * s / Versal, Winkel = Math.Abs(wink) > 1e-6 ? wink : null,
                Ausrichtung = ausr switch { 1 => "mitte", 2 => "rechts", _ => "links" }, Stil = st,
            });
        }

        // ---------- Objekte ----------

        static bool Ocs(XYZ n, Afin t, out Afin ergebnis)
        {
            ergebnis = t;
            if (Math.Abs(n.X) > 1.0 / 64 || Math.Abs(n.Y) > 1.0 / 64) return false;
            if (n.Z < 0) ergebnis = t.Nach(Afin.SpiegelX);
            return true;
        }

        public void Verarbeiten(Entity e, ZeichnungTeil teil, Kontext k)
        {
            if (e.IsInvisible) return;
            var layer = LayerVon(e, k);
            if (layer is not null)
            {
                if (k.Gefroren?.Contains(layer.Name) == true) return;
                // Blockreferenz auf ausgeschaltetem Layer: ihre Objekte auf eigenen Layern bleiben sichtbar (wie AutoCAD); gefroren blendet alles aus
                if (e is Insert ? layer.Flags.HasFlag(LayerFlags.Frozen) : !Zeichnungsdatei.Sichtbar(layer)) return;
            }
            try { Zeichnen(e, teil, layer, k); }
            catch (Exception ex) when (ex is not HlksFehler and not OutOfMemoryException) { Nicht(e.GetType().Name + " (Fehler)"); }
        }

        void Zeichnen(Entity e, ZeichnungTeil teil, Layer? layer, Kontext k)
        {
            var t = k.T;
            Afin o;
            switch (e)
            {
                case Line l:
                    Linien(teil, StilVon(e, layer, k), t, [(l.StartPoint.X, l.StartPoint.Y), (l.EndPoint.X, l.EndPoint.Y)], false);
                    break;
                case LwPolyline lw:
                    if (!Ocs(lw.Normal, t, out o)) { Nicht("LwPolyline (geneigt)"); break; }
                    Polylinie(teil, StilVon(e, layer, k), o, lw.Vertices.Select(v => (v.Location.X, v.Location.Y, v.Bulge)).ToList(), lw.IsClosed);
                    break;
                case Polyline2D p2:
                    if (!Ocs(p2.Normal, t, out o)) { Nicht("Polyline2D (geneigt)"); break; }
                    Polylinie(teil, StilVon(e, layer, k), o, p2.Vertices.Select(v => (v.Location.X, v.Location.Y, v.Bulge)).ToList(), p2.IsClosed);
                    break;
                case Polyline3D p3:
                    Linien(teil, StilVon(e, layer, k), t, p3.Vertices.Select(v => (v.Location.X, v.Location.Y)).ToList(), p3.IsClosed);
                    break;
                case Arc a:   // vor Circle: Arc ist ein Circle
                    if (!Ocs(a.Normal, t, out o)) { Nicht("Arc (geneigt)"); break; }
                    Bogen(teil, StilVon(e, layer, k), o, a.Center.X, a.Center.Y, a.Radius, a.StartAngle, a.EndAngle);
                    break;
                case Circle c:
                    if (!Ocs(c.Normal, t, out o)) { Nicht("Circle (geneigt)"); break; }
                    Kreis(teil, StilVon(e, layer, k), o, c.Center.X, c.Center.Y, c.Radius);
                    break;
                case Ellipse el:
                {
                    var (pts, voll) = ZeichnungGeometrie.EllipsePunkte(el);
                    Linien(teil, StilVon(e, layer, k), t, pts, voll);
                    break;
                }
                case Spline sp:
                    Linien(teil, StilVon(e, layer, k), t, ZeichnungGeometrie.SplinePunkte(sp), sp.IsClosed);
                    break;
                case AttributeDefinition:
                    break;   // Vorgabe eines Attributs: nur in der Blockdefinition, nicht gezeichnet
                case TextEntity te:
                    if (!Ocs(te.Normal, t, out o)) { Nicht("Text (geneigt)"); break; }
                    TextEintrag(teil, StilVon(e, layer, k), o, te);
                    break;
                case MText mt:
                    MTextEintrag(teil, StilVon(e, layer, k), t, mt);
                    break;
                case Dimension d:
                    if (d.Block is null) { Nicht(d.GetType().Name + " (ohne Block)"); break; }
                    Abstieg(e, layer, k, t, d.Block, teil);
                    break;
                case Insert ins:
                    Einsetzen(teil, layer, k, ins);
                    break;
                case Hatch h:
                    if (!Ocs(h.Normal, t, out o)) { Nicht("Hatch (geneigt)"); break; }
                    Schraffur(teil, e, layer, k, o, h);
                    break;
                case Solid so:
                    if (!Ocs(so.Normal, t, out o)) { Nicht("Solid (geneigt)"); break; }
                    // Reihenfolge der Ecken im DXF: 1, 2, 4, 3
                    Flaeche(teil, Fuellung(Farbe(e, layer, k, true)), o, [[(so.FirstCorner.X, so.FirstCorner.Y), (so.SecondCorner.X, so.SecondCorner.Y), (so.FourthCorner.X, so.FourthCorner.Y), (so.ThirdCorner.X, so.ThirdCorner.Y)]]);
                    break;
                case Face3D f:
                    Linien(teil, StilVon(e, layer, k), t, [(f.FirstCorner.X, f.FirstCorner.Y), (f.SecondCorner.X, f.SecondCorner.Y), (f.ThirdCorner.X, f.ThirdCorner.Y), (f.FourthCorner.X, f.FourthCorner.Y)], true);
                    break;
                case Leader le:
                    Linien(teil, StilVon(e, layer, k), t, le.Vertices.Select(v => (v.X, v.Y)).ToList(), false);
                    break;
                case Point:
                    break;   // Punkte werden nicht geplottet (Hilfspunkte, Bemassungspunkte)
                default:
                    Nicht(e.GetType().Name);
                    break;
            }
        }

        /// <summary>Objekte eines Blocks zeichnen; VonBlock und Layer 0 erben von der Referenz <paramref name="e"/>.</summary>
        void Abstieg(Entity e, Layer? layer, Kontext k, Afin t, BlockRecord block, ZeichnungTeil teil)
        {
            var innen = new Kontext(t, layer, Farbe(e, layer, k), Mm(e, layer, k), Linie(e, layer, k), k.Gefroren, k.Tiefe + 1);
            foreach (var c in block.Entities) Verarbeiten(c, teil, innen);
        }

        void Einsetzen(ZeichnungTeil teil, Layer? layer, Kontext k, Insert ins)
        {
            var block = ins.Block;
            if (block is null || k.Tiefe >= MaxTiefe) { Nicht(block is null ? "Insert (Block fehlt)" : "Insert (zu tief verschachtelt)"); return; }
            if (!Ocs(ins.Normal, k.T, out var o)) { Nicht("Insert (geneigt)"); return; }
            if (block.Flags.HasFlag(BlockTypeFlags.XRef) && block.Entities.Count == 0) { Nicht("externe Referenz (nicht eingebunden)"); return; }
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
                    Abstieg(ins, layer, k, tt, block, teil);
                }
            if (!ins.HasAttributes) return;
            var aussen = new Kontext(k.T, layer, Farbe(ins, layer, k), Mm(ins, layer, k), Linie(ins, layer, k), k.Gefroren, k.Tiefe + 1);
            foreach (var a in ins.Attributes)
            {
                if (a.IsInvisible || a.Flags.HasFlag(AttributeFlags.Hidden)) continue;
                var al = LayerVon(a, aussen);
                if (al is not null && (!Zeichnungsdatei.Sichtbar(al) || k.Gefroren?.Contains(al.Name) == true)) continue;
                if (!Ocs(a.Normal, k.T, out var oa)) continue;
                TextEintrag(teil, StilVon(a, al, aussen), oa, a);
            }
        }

        void TextEintrag(ZeichnungTeil teil, Stil st, Afin o, TextEntity te)
        {
            var text = CadText.Einfach(te.Value);
            double h = te.Height > 0 ? te.Height : te.Style?.Height ?? 0;
            if (h <= 0) { Nicht("Text (ohne Höhe)"); return; }
            var hor = te.HorizontalAlignment;
            var ver = te.VerticalAlignment;
            bool ausgerichtet = hor is TextHorizontalAlignment.Center or TextHorizontalAlignment.Right or TextHorizontalAlignment.Middle || ver != TextVerticalAlignmentType.Baseline;
            var anker = ausgerichtet && hor is not (TextHorizontalAlignment.Aligned or TextHorizontalAlignment.Fit) ? te.AlignmentPoint : te.InsertPoint;
            int ausr = hor switch { TextHorizontalAlignment.Center or TextHorizontalAlignment.Middle => 1, TextHorizontalAlignment.Right => 2, _ => 0 };
            double ab = hor == TextHorizontalAlignment.Middle ? 0.5 * h : ver switch { TextVerticalAlignmentType.Middle => 0.5 * h, TextVerticalAlignmentType.Top => h, _ => 0 };
            Text(teil, st, o, anker.X + Math.Sin(te.Rotation) * ab, anker.Y - Math.Cos(te.Rotation) * ab, h, te.Rotation, ausr, text);
        }

        void MTextEintrag(ZeichnungTeil teil, Stil st, Afin t, MText mt)
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
            double erste = at switch
            {
                AttachmentPointType.TopLeft or AttachmentPointType.TopCenter or AttachmentPointType.TopRight => -h,
                AttachmentPointType.MiddleLeft or AttachmentPointType.MiddleCenter or AttachmentPointType.MiddleRight => gesamt / 2 - h,
                _ => (zeilen.Count - 1) * abstand,
            };
            double rot = mt.Rotation, cs = Math.Cos(rot), sn = Math.Sin(rot);
            for (int i = 0; i < zeilen.Count; i++)
            {
                double dy = erste - i * abstand;
                Text(teil, st, t, mt.InsertPoint.X - sn * dy, mt.InsertPoint.Y + cs * dy, h, rot, ausr, zeilen[i]);
            }
        }

        void Schraffur(ZeichnungTeil teil, Entity e, Layer? layer, Kontext k, Afin o, Hatch h)
        {
            var ringe = h.Paths.Select(p => p.GetPoints(16).Select(q => (q.X, q.Y)).ToList()).Where(r => r.Count >= 3).ToList();
            if (ringe.Count == 0) return;
            var farbe = Farbe(e, layer, k);
            if (h.IsSolid || h.Pattern is null || h.Pattern.Lines.Count == 0)
            {
                // grösster Ring zuerst: Aussenring, die übrigen als Aussparungen bzw. Inseln (Füllregel gerade/ungerade)
                Flaeche(teil, Fuellung(Farbe(e, layer, k, true)), o, ringe.OrderByDescending(r => Math.Abs(ZeichnungGeometrie.Flaeche(r))).ToList());
                return;
            }
            var key = (farbe, Stifte.Duenn, "voll");
            if (!stile.TryGetValue(key, out var st)) stile[key] = st = new Stil { Farbe = farbe, Stift = Stifte.Duenn };
            var strecken = ZeichnungGeometrie.SchraffurLinien(ringe, h.Pattern.Lines.Select(l => (l.Angle, l.BasePoint.X, l.BasePoint.Y, l.Offset.X, l.Offset.Y, (IReadOnlyList<double>)l.DashLengths)).ToList(), MaxSchraffurLinien);
            if (strecken is null) { Nicht("Hatch (Muster zu dicht)"); return; }
            foreach (var (x0, y0, x1, y1) in strecken) Linien(teil, st, o, [(x0, y0), (x1, y1)], false);
        }

        // ---------- Ausdehnung ----------

        public void Abschliessen()
        {
            if (boxen.Count == 0) return;
            static double[] Vereinigung(IEnumerable<double[]> b) =>
                b.Aggregate(new[] { double.MaxValue, double.MaxValue, double.MinValue, double.MinValue },
                    (a, q) => [Math.Min(a[0], q[0]), Math.Min(a[1], q[1]), Math.Max(a[2], q[2]), Math.Max(a[3], q[3])]);
            Bild.Ausdehnung = Vereinigung(boxen);
            // Hauptteil: Mittelpunkte zwischen dem 5. und 95. Perzentil, dazu ihre Breite als Rand (wie CadLeser.Ausdehnungen)
            var mx = boxen.Select(b => (b[0] + b[2]) / 2).Order().ToArray();
            var my = boxen.Select(b => (b[1] + b[3]) / 2).Order().ToArray();
            double Lo(double[] v, double q) => v[(int)Math.Min(v.Length - 1, q * v.Length)];
            double xl = Lo(mx, 0.05), xh = Lo(mx, 0.95), yl = Lo(my, 0.05), yh = Lo(my, 0.95), wx = xh - xl, wy = yh - yl;
            var haupt = boxen.Where(b => (b[0] + b[2]) / 2 >= xl - wx && (b[0] + b[2]) / 2 <= xh + wx && (b[1] + b[3]) / 2 >= yl - wy && (b[1] + b[3]) / 2 <= yh + wy).ToList();
            Bild.Umriss = haupt.Count > 0 ? Vereinigung(haupt) : Bild.Ausdehnung;
            _ = z;
        }
    }
}
