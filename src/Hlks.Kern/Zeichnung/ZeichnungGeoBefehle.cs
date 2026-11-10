// 2D-CAD «Zeichnung», Stufe 2 Teil 2: Änderungsbefehle mit Geometrie und weitere Objektarten.
//   Aendern   spiegeln · versetzen · stutzen · dehnen · abrunden · fasen · ursprung · verbinden · reihe · uebertragen
//   Bauen     ellipse · polygon · punkt · absatztext · schraffur · bemassung (linear, ausgerichtet)
// Aufgerufen aus ZeichnungBearbeiten (eine Tür: Aendern bzw. Zeichnen). Befehle, die ein Objekt umformen (stutzen, dehnen, abrunden, fasen,
// ursprung, verbinden), ersetzen es durch neue Objekte mit denselben Eigenschaften – das neue Objekt hat eine neue Kennung.
// Grenzen: Polylinien mit Bögen lassen sich noch nicht stutzen und versetzen (vorher «ursprung»); Schraffuren und Bemassungen nicht spiegeln.

using System.Text.Json.Nodes;
using ACadSharp.Entities;
using ACadSharp.Tables;
using CSMath;
using static Hlks.Kern.ZeichnungBearbeiten;

namespace Hlks.Kern;

/// <summary>Ein Stück Geometrie für Schnitte: Strecke A→B oder Kreisbogen um C ab Winkel Von über Sweep (Gegenuhrzeigersinn, 2π = Kreis).</summary>
public readonly record struct GeoStueck(bool Bogen, double Ax, double Ay, double Bx, double By, double R, double Von, double Sweep)
{
    public static GeoStueck Strecke(double ax, double ay, double bx, double by) => new(false, ax, ay, bx, by, 0, 0, 0);
    public static GeoStueck Kreisbogen(double cx, double cy, double r, double von, double sweep) => new(true, cx, cy, 0, 0, r, von, sweep);
}

public static class ZeichnungSchnitt
{
    public const double Eps = 1e-9;
    static double Norm(double w) => ((w % (2 * Math.PI)) + 2 * Math.PI) % (2 * Math.PI);

    /// <summary>Stücke eines Objekts (Linie, Bogen, Kreis, Polylinie mit Bögen; Ellipse und Spline als Streckenzug). Leer = nicht schneidbar.</summary>
    public static List<GeoStueck> Stuecke(Entity e)
    {
        var aus = new List<GeoStueck>();
        switch (e)
        {
            case Line l: aus.Add(GeoStueck.Strecke(l.StartPoint.X, l.StartPoint.Y, l.EndPoint.X, l.EndPoint.Y)); break;
            case Arc a: { double s = Norm(a.EndAngle - a.StartAngle); aus.Add(GeoStueck.Kreisbogen(a.Center.X, a.Center.Y, a.Radius, a.StartAngle, s < Eps ? 2 * Math.PI : s)); break; }
            case Circle c: aus.Add(GeoStueck.Kreisbogen(c.Center.X, c.Center.Y, c.Radius, 0, 2 * Math.PI)); break;
            case LwPolyline p:
            {
                var v = p.Vertices;
                int n = p.IsClosed ? v.Count : v.Count - 1;
                for (int i = 0; i < n; i++)
                {
                    var a = v[i]; var b = v[(i + 1) % v.Count];
                    if (a.Bulge == 0) { aus.Add(GeoStueck.Strecke(a.Location.X, a.Location.Y, b.Location.X, b.Location.Y)); continue; }
                    var (cx, cy, r, w1, w2) = ZeichnungGeometrie.BogenAusBulge(a.Location.X, a.Location.Y, b.Location.X, b.Location.Y, a.Bulge);
                    aus.Add(a.Bulge > 0 ? GeoStueck.Kreisbogen(cx, cy, r, w1, Norm(w2 - w1)) : GeoStueck.Kreisbogen(cx, cy, r, w2, Norm(w1 - w2)));
                }
                break;
            }
            case Ellipse el: { var (pts, voll) = ZeichnungGeometrie.EllipsePunkte(el); Zug(aus, pts, voll); break; }
            case Spline sp: Zug(aus, ZeichnungGeometrie.SplinePunkte(sp), sp.IsClosed); break;
        }
        return aus;
    }

    static void Zug(List<GeoStueck> aus, List<(double X, double Y)> p, bool zu)
    {
        for (int i = 0; i + 1 < p.Count; i++) aus.Add(GeoStueck.Strecke(p[i].X, p[i].Y, p[i + 1].X, p[i + 1].Y));
        if (zu && p.Count > 2) aus.Add(GeoStueck.Strecke(p[^1].X, p[^1].Y, p[0].X, p[0].Y));
    }

    static bool AufBogen(GeoStueck b, double x, double y) => b.Sweep >= 2 * Math.PI - Eps || Norm(Math.Atan2(y - b.Ay, x - b.Ax) - b.Von) <= b.Sweep + 1e-7;

    /// <summary>Schnittpunkte der Geraden durch A→B mit einem Stück; t = Lage auf der Geraden (0 = A, 1 = B). Die Gerade ist unbegrenzt.</summary>
    public static IEnumerable<(double T, double X, double Y)> GeradeMit(double ax, double ay, double bx, double by, GeoStueck s)
    {
        double dx = bx - ax, dy = by - ay;
        if (!s.Bogen)
        {
            double ex = s.Bx - s.Ax, ey = s.By - s.Ay, det = dx * ey - dy * ex;
            if (Math.Abs(det) < 1e-12 * (Math.Abs(dx) + Math.Abs(dy)) * (Math.Abs(ex) + Math.Abs(ey))) yield break;   // parallel
            double t = ((s.Ax - ax) * ey - (s.Ay - ay) * ex) / det, u = ((s.Ax - ax) * dy - (s.Ay - ay) * dx) / det;
            if (u >= -1e-9 && u <= 1 + 1e-9) yield return (t, ax + t * dx, ay + t * dy);
            yield break;
        }
        double fx = ax - s.Ax, fy = ay - s.Ay, a = dx * dx + dy * dy, b = 2 * (fx * dx + fy * dy), c = fx * fx + fy * fy - s.R * s.R;
        double disk = b * b - 4 * a * c;
        if (a < 1e-18 || disk < 0) yield break;
        double w = Math.Sqrt(disk);
        foreach (var t in disk < 1e-12 * a ? [-b / (2 * a)] : new[] { (-b - w) / (2 * a), (-b + w) / (2 * a) })
        {
            double x = ax + t * dx, y = ay + t * dy;
            if (AufBogen(s, x, y)) yield return (t, x, y);
        }
    }

    /// <summary>Schnittpunkte des vollen Kreises um (cx, cy) mit einem Stück, als Winkel auf dem Kreis.</summary>
    public static IEnumerable<(double W, double X, double Y)> KreisMit(double cx, double cy, double r, GeoStueck s)
    {
        if (!s.Bogen)
        {
            foreach (var (t, x, y) in GeradeMit(s.Ax, s.Ay, s.Bx, s.By, GeoStueck.Kreisbogen(cx, cy, r, 0, 2 * Math.PI)))
                if (t >= -1e-9 && t <= 1 + 1e-9) yield return (Norm(Math.Atan2(y - cy, x - cx)), x, y);
            yield break;
        }
        double dx = s.Ax - cx, dy = s.Ay - cy, d = Math.Sqrt(dx * dx + dy * dy);
        if (d < 1e-12 || d > r + s.R || d < Math.Abs(r - s.R)) yield break;
        double a = (r * r - s.R * s.R + d * d) / (2 * d), h2 = r * r - a * a, h = h2 > 0 ? Math.Sqrt(h2) : 0;
        double mx = cx + a * dx / d, my = cy + a * dy / d;
        foreach (var vz in h < 1e-9 ? [0.0] : new[] { 1.0, -1.0 })
        {
            double x = mx - vz * h * dy / d, y = my + vz * h * dx / d;
            if (AufBogen(s, x, y)) yield return (Norm(Math.Atan2(y - cy, x - cx)), x, y);
        }
    }
}

public static class ZeichnungGeoBefehle
{
    static double Norm(double w) => ((w % (2 * Math.PI)) + 2 * Math.PI) % (2 * Math.PI);

    static BlockRecord BlockVon(Entity e) => e.Owner as BlockRecord
        ?? throw new HlksFehler($"Das Objekt {Zeichnungsdatei.Kennung(e)} gehört zu einem anderen Objekt und lässt sich so nicht ändern.");

    /// <summary>Kopie mit denselben Eigenschaften (Layer, Farbe, Linientyp), ohne Kennung.</summary>
    static T Wie<T>(Entity vorbild, T neu) where T : Entity
    {
        neu.Layer = vorbild.Layer; neu.Color = vorbild.Color; neu.LineType = vorbild.LineType; neu.LineWeight = vorbild.LineWeight; neu.LineTypeScale = vorbild.LineTypeScale;
        return neu;
    }

    static (string, Action, Action) Ersetzen(string name, List<(Entity Alt, BlockRecord Block, List<Entity> Neu)> liste) =>
        (name,
         () => { foreach (var (alt, b, neu) in liste) { b.Entities.Remove(alt); foreach (var n in neu) b.Entities.Add(n); } },
         () => { foreach (var (alt, b, neu) in liste) { foreach (var n in neu) b.Entities.Remove(n); b.Entities.Add(alt); } });

    static (string, Action, Action) Dazu(string name, List<(Entity Neu, BlockRecord Block)> liste) =>
        (name, () => { foreach (var (n, b) in liste) b.Entities.Add(n); }, () => { foreach (var (n, b) in liste) b.Entities.Remove(n); });

    /// <summary>Stücke der Grenzobjekte; ohne Angabe alle sichtbaren Objekte desselben Bereichs ausser dem Objekt selbst.</summary>
    static List<GeoStueck> Grenzen(Zeichnungsdatei z, JsonObject par, Entity selbst)
    {
        IEnumerable<Entity> quellen = par["grenzen"] is JsonArray { Count: > 0 } g
            ? g.Select(n => z.Objekt(n is JsonValue v && v.TryGetValue<string>(out var s) ? s : ""))
            : BlockVon(selbst).Entities.Where(e => !e.IsInvisible && (e.Layer is null || Zeichnungsdatei.Sichtbar(e.Layer)));
        return quellen.Where(e => !ReferenceEquals(e, selbst)).SelectMany(ZeichnungSchnitt.Stuecke).ToList();
    }

    public static (string Name, Action Vor, Action Zurueck) Aendern(Zeichnungsdatei z, List<Entity> liste, string aktion, JsonObject par, JsonObject antwort)
    {
        switch (aktion)
        {
            case "spiegeln": return Spiegeln(liste, par);
            case "versetzen": return Versetzen(liste, par);
            case "stutzen": return Stutzen(z, liste, par);
            case "dehnen": return Dehnen(z, liste, par);
            case "abrunden": return Ecke(liste, par, fase: false);
            case "fasen": return Ecke(liste, par, fase: true);
            case "ursprung": return Ursprung(liste);
            case "verbinden": return Verbinden(liste);
            case "reihe": return Reihe(liste, par);
            case "uebertragen": return Uebertragen(z, liste, par);
            default:
                throw new HlksFehler($"Die Aktion «{aktion}» gibt es nicht.",
                    "Erlaubt: loeschen, verschieben, kopieren, drehen, skalieren, eigenschaften, spiegeln, versetzen, stutzen, dehnen, abrunden, fasen, ursprung, verbinden, reihe, uebertragen.");
        }
    }

    // ---------- Spiegeln ----------

    static (string, Action, Action) Spiegeln(List<Entity> liste, JsonObject par)
    {
        if (par["achse"] is not JsonArray { Count: 2 } achse) throw new HlksFehler("«achse» fehlt.", "achse: [[x, y], [x, y]] – zwei Punkte der Spiegelachse.");
        var (ax, ay) = Punkt(achse[0], "achse"); var (bx, by) = Punkt(achse[1], "achse");
        double dx = bx - ax, dy = by - ay, l2 = dx * dx + dy * dy;
        if (l2 < 1e-18) throw new HlksFehler("Die beiden Punkte der Spiegelachse sind gleich.");
        double phi = Math.Atan2(dy, dx);
        (double X, double Y) P(double x, double y) { double t = ((x - ax) * dx + (y - ay) * dy) / l2; return (2 * (ax + t * dx) - x, 2 * (ay + t * dy) - y); }
        XYZ Q(XYZ p) { var (x, y) = P(p.X, p.Y); return new XYZ(x, y, p.Z); }
        void Tun(Entity e)
        {
            switch (e)
            {
                case Line l: l.StartPoint = Q(l.StartPoint); l.EndPoint = Q(l.EndPoint); break;
                case LwPolyline p:
                    foreach (var v in p.Vertices) { var (x, y) = P(v.Location.X, v.Location.Y); v.Location = new XY(x, y); v.Bulge = -v.Bulge; }
                    break;
                case Arc a: { a.Center = Q(a.Center); (a.StartAngle, a.EndAngle) = (Norm(2 * phi - a.EndAngle), Norm(2 * phi - a.StartAngle)); break; }
                case Circle c: c.Center = Q(c.Center); break;
                case Ellipse el when el.IsFullEllipse:
                {
                    var m = el.MajorAxisEndPoint; double w = 2 * phi - Math.Atan2(m.Y, m.X), len = Math.Sqrt(m.X * m.X + m.Y * m.Y);
                    el.Center = Q(el.Center); el.MajorAxisEndPoint = new XYZ(len * Math.Cos(w), len * Math.Sin(w), 0);
                    break;
                }
                case Point pt: pt.Location = Q(pt.Location); break;
                // Text bleibt lesbar (wie AutoCAD mit MIRRTEXT = 0): Ort gespiegelt, Richtung gespiegelt und um 180° gedreht
                case TextEntity t: t.InsertPoint = Q(t.InsertPoint); t.AlignmentPoint = Q(t.AlignmentPoint); t.Rotation = Norm(2 * phi - t.Rotation + Math.PI); break;
                case MText m: { double w = Norm(2 * phi - m.Rotation + Math.PI); m.InsertPoint = Q(m.InsertPoint); m.AlignmentPoint = new XYZ(Math.Cos(w), Math.Sin(w), 0); break; }   // Richtung des Absatztexts steht im Richtungsvektor
                case Insert ins: ins.InsertPoint = Q(ins.InsertPoint); ins.Rotation = Norm(2 * phi - ins.Rotation); ins.YScale = -ins.YScale; break;
            }
        }
        var nicht = liste.Where(e => e is not (Line or LwPolyline or Circle or Point or TextEntity or MText or Insert) && !(e is Ellipse { IsFullEllipse: true })).ToList();
        if (nicht.Count > 0)
            throw new HlksFehler($"Spiegeln geht noch nicht für: {string.Join(", ", nicht.Select(ZeichnungAbfragen.Art).Distinct())}.", "Diese Objekte aus der Auswahl nehmen (Schraffuren, Bemassungen, Splines, Ellipsenbögen).");
        bool kopie = par["kopie"] is JsonValue kv && kv.TryGetValue<bool>(out var k) && k;
        if (!kopie) return ($"{liste.Count} spiegeln", () => liste.ForEach(Tun), () => liste.ForEach(Tun));   // zweimal gespiegelt = wie vorher
        var neu = liste.Select(e => { var c = (Entity)e.Clone(); Tun(c); return (c, BlockVon(e)); }).ToList();
        return Dazu($"{liste.Count} spiegeln (Kopie)", neu);
    }

    // ---------- Versetzen ----------

    static (string, Action, Action) Versetzen(List<Entity> liste, JsonObject par)
    {
        double d = Json.Zahl(par["abstand"]) ?? throw new HlksFehler("«abstand» fehlt.", "Abstand in Zeichnungseinheiten angeben.");
        if (!(d > 0)) throw new HlksFehler("Der Abstand muss grösser als 0 sein.");
        var (sx, sy) = Punkt(par["seite"], "seite");
        var neu = new List<(Entity, BlockRecord)>();
        foreach (var e in liste)
        {
            Entity k;
            switch (e)
            {
                case Line l:
                {
                    double dx = l.EndPoint.X - l.StartPoint.X, dy = l.EndPoint.Y - l.StartPoint.Y, len = Math.Sqrt(dx * dx + dy * dy);
                    double nx = -dy / len, ny = dx / len, vz = (sx - l.StartPoint.X) * nx + (sy - l.StartPoint.Y) * ny >= 0 ? d : -d;
                    k = Wie(e, new Line { StartPoint = new XYZ(l.StartPoint.X + nx * vz, l.StartPoint.Y + ny * vz, 0), EndPoint = new XYZ(l.EndPoint.X + nx * vz, l.EndPoint.Y + ny * vz, 0) });
                    break;
                }
                case Circle c:   // auch Bogen
                {
                    bool aussen = Math.Sqrt(Math.Pow(sx - c.Center.X, 2) + Math.Pow(sy - c.Center.Y, 2)) > c.Radius;
                    double r = c.Radius + (aussen ? d : -d);
                    if (!(r > 0)) throw new HlksFehler("Der Abstand ist grösser als der Radius: nach innen bleibt nichts übrig.", "Kleineren Abstand wählen oder nach aussen versetzen.");
                    k = (Entity)e.Clone(); ((Circle)k).Radius = r;
                    break;
                }
                case LwPolyline p:
                {
                    if (p.Vertices.Any(v => v.Bulge != 0)) throw new HlksFehler("Polylinien mit Bögen lassen sich noch nicht versetzen.", "Zuerst mit «ursprung» in Linien und Bögen zerlegen.");
                    var v = p.Vertices.Select(q => (q.Location.X, q.Location.Y)).ToList();
                    var o = PolylinieVersetzen(v, p.IsClosed, d, sx, sy);
                    var q2 = Wie(e, new LwPolyline { IsClosed = p.IsClosed });
                    foreach (var (x, y) in o) q2.Vertices.Add(new LwPolyline.Vertex(new XY(x, y)));
                    k = q2;
                    break;
                }
                default: throw new HlksFehler($"Versetzen geht nicht für ein Objekt der Art «{ZeichnungAbfragen.Art(e)}».", "Linien, Kreise, Bögen und Polylinien ohne Bögen lassen sich versetzen.");
            }
            neu.Add((k, BlockVon(e)));
        }
        return Dazu($"{liste.Count} versetzen", neu);
    }

    /// <summary>Polylinie aus Strecken parallel versetzen, auf die Seite des Punkts (sx, sy); Ecken durch Verschneiden der Nachbarn.</summary>
    public static List<(double X, double Y)> PolylinieVersetzen(List<(double X, double Y)> v, bool zu, double d, double sx, double sy)
    {
        // doppelte Punkte entfernen
        var p = new List<(double X, double Y)>();
        foreach (var q in v) if (p.Count == 0 || Math.Abs(q.X - p[^1].X) + Math.Abs(q.Y - p[^1].Y) > 1e-12) p.Add(q);
        if (zu && p.Count > 1 && Math.Abs(p[0].X - p[^1].X) + Math.Abs(p[0].Y - p[^1].Y) < 1e-12) p.RemoveAt(p.Count - 1);
        int n = zu ? p.Count : p.Count - 1;
        if (n < 1) throw new HlksFehler("Die Polylinie hat keine Länge.");
        // Seite: Vorzeichen am nächsten Abschnitt
        double best = double.MaxValue, vz = 1;
        for (int i = 0; i < n; i++)
        {
            var a = p[i]; var b = p[(i + 1) % p.Count];
            double dx = b.X - a.X, dy = b.Y - a.Y, l2 = dx * dx + dy * dy, t = Math.Clamp(((sx - a.X) * dx + (sy - a.Y) * dy) / l2, 0, 1);
            double ab = Math.Pow(sx - a.X - t * dx, 2) + Math.Pow(sy - a.Y - t * dy, 2);
            if (ab < best) { best = ab; vz = (sx - a.X) * -dy + (sy - a.Y) * dx >= 0 ? 1 : -1; }
        }
        // versetzte Geraden je Abschnitt: Punkt und Richtung
        var g = new List<(double X, double Y, double Dx, double Dy)>();
        for (int i = 0; i < n; i++)
        {
            var a = p[i]; var b = p[(i + 1) % p.Count];
            double dx = b.X - a.X, dy = b.Y - a.Y, len = Math.Sqrt(dx * dx + dy * dy);
            g.Add((a.X - dy / len * d * vz, a.Y + dx / len * d * vz, dx, dy));
        }
        (double, double) Schnitt(int i, int j, (double X, double Y) ersatz)
        {
            var a = g[i]; var b = g[j];
            double det = a.Dx * b.Dy - a.Dy * b.Dx;
            if (Math.Abs(det) < 1e-12 * (Math.Abs(a.Dx) + Math.Abs(a.Dy)) * (Math.Abs(b.Dx) + Math.Abs(b.Dy))) return ersatz;   // gleiche Richtung: der versetzte Eckpunkt
            double t = ((b.X - a.X) * b.Dy - (b.Y - a.Y) * b.Dx) / det;
            return (a.X + t * a.Dx, a.Y + t * a.Dy);
        }
        var aus = new List<(double, double)>();
        if (zu) for (int i = 0; i < n; i++) aus.Add(Schnitt((i + n - 1) % n, i, (g[i].X, g[i].Y)));
        else
        {
            aus.Add((g[0].X, g[0].Y));
            for (int i = 1; i < n; i++) aus.Add(Schnitt(i - 1, i, (g[i].X, g[i].Y)));
            aus.Add((g[n - 1].X + g[n - 1].Dx, g[n - 1].Y + g[n - 1].Dy));
        }
        return aus;
    }

    // ---------- Stutzen ----------

    static (string, Action, Action) Stutzen(Zeichnungsdatei z, List<Entity> liste, JsonObject par)
    {
        if (liste.Count != 1) throw new HlksFehler("Stutzen nimmt ein Objekt je Aufruf.", "kennungen: das zu stutzende Objekt; punkt: Stelle auf dem Teil, der weg soll; grenzen: Schnittkanten (leer = alle).");
        var e = liste[0];
        var (px, py) = Punkt(par["punkt"], "punkt");
        var grenzen = Grenzen(z, par, e);
        var neu = new List<Entity>();
        const double Rand = 1e-7;
        HlksFehler Kein() => new("An dieser Stelle gibt es keinen Schnitt mit einer Grenze: nichts zu stutzen.", "Ein Objekt wählen, das von einer Schnittkante gekreuzt wird, oder es löschen.");
        switch (e)
        {
            case Line l:
            {
                double ax = l.StartPoint.X, ay = l.StartPoint.Y, bx = l.EndPoint.X, by = l.EndPoint.Y, dx = bx - ax, dy = by - ay;
                var t = grenzen.SelectMany(s => ZeichnungSchnitt.GeradeMit(ax, ay, bx, by, s)).Select(q => q.T).Where(q => q > Rand && q < 1 - Rand).Order().ToList();
                double tc = Math.Clamp(((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy), 0, 1);
                double lo = t.Where(q => q <= tc).DefaultIfEmpty(0).Max(), hi = t.Where(q => q > tc).DefaultIfEmpty(1).Min();
                if (lo == 0 && hi == 1) throw Kein();
                if (lo > 0) neu.Add(Wie(e, new Line { StartPoint = l.StartPoint, EndPoint = new XYZ(ax + lo * dx, ay + lo * dy, 0) }));
                if (hi < 1) neu.Add(Wie(e, new Line { StartPoint = new XYZ(ax + hi * dx, ay + hi * dy, 0), EndPoint = l.EndPoint }));
                break;
            }
            case Circle c:   // Kreis oder Bogen: Lage als Winkel ab dem Anfang
            {
                var bogen = c as Arc;
                double von = bogen?.StartAngle ?? 0, sweep = bogen is null ? 2 * Math.PI : Norm(bogen.EndAngle - bogen.StartAngle) is var s0 && s0 < 1e-9 ? 2 * Math.PI : Norm(bogen.EndAngle - bogen.StartAngle);
                var w = grenzen.SelectMany(s => ZeichnungSchnitt.KreisMit(c.Center.X, c.Center.Y, c.Radius, s)).Select(q => Norm(q.W - von)).Where(q => bogen is null || (q > Rand && q < sweep - Rand)).Order().ToList();
                double wc = Norm(Math.Atan2(py - c.Center.Y, px - c.Center.X) - von);
                Arc Teil(double a, double b) => Wie(e, new Arc { Center = c.Center, Radius = c.Radius, StartAngle = Norm(von + a), EndAngle = Norm(von + b) });
                if (bogen is null)
                {
                    if (w.Count < 2) throw new HlksFehler("Ein Kreis braucht zwei Schnitte mit Grenzen, damit ein Bogen übrig bleibt.", "Zwei Schnittkanten wählen oder den Kreis löschen.");
                    double lo = w.Where(q => q <= wc).DefaultIfEmpty(w[^1] - 2 * Math.PI).Max(), hi = w.Where(q => q > wc).DefaultIfEmpty(w[0] + 2 * Math.PI).Min();
                    neu.Add(Teil(hi, lo));   // der Rest des Kreises: von hi weiter bis lo
                }
                else
                {
                    wc = Math.Min(wc, sweep);
                    double lo = w.Where(q => q <= wc).DefaultIfEmpty(0).Max(), hi = w.Where(q => q > wc).DefaultIfEmpty(sweep).Min();
                    if (lo == 0 && hi == sweep) throw Kein();
                    if (lo > 0) neu.Add(Teil(0, lo));
                    if (hi < sweep) neu.Add(Teil(hi, sweep));
                }
                break;
            }
            case LwPolyline p:
            {
                if (p.Vertices.Any(v => v.Bulge != 0)) throw new HlksFehler("Polylinien mit Bögen lassen sich noch nicht stutzen.", "Zuerst mit «ursprung» in Linien und Bögen zerlegen.");
                var v = p.Vertices.Select(q => (q.Location.X, q.Location.Y)).ToList();
                int n = p.IsClosed ? v.Count : v.Count - 1;
                // Lage entlang der Polylinie: Abschnitt + Anteil
                var t = new List<double>();
                double tc = 0, best = double.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    var a = v[i]; var b = v[(i + 1) % v.Count];
                    double dx = b.X - a.X, dy = b.Y - a.Y, l2 = dx * dx + dy * dy;
                    if (l2 < 1e-18) continue;
                    foreach (var s in grenzen)
                        foreach (var q in ZeichnungSchnitt.GeradeMit(a.X, a.Y, b.X, b.Y, s))
                            if (q.T >= -1e-9 && q.T <= 1 + 1e-9) t.Add(i + Math.Clamp(q.T, 0, 1));
                    double u = Math.Clamp(((px - a.X) * dx + (py - a.Y) * dy) / l2, 0, 1), ab = Math.Pow(px - a.X - u * dx, 2) + Math.Pow(py - a.Y - u * dy, 2);
                    if (ab < best) { best = ab; tc = i + u; }
                }
                t = t.Where(q => p.IsClosed || (q > Rand && q < n - Rand)).Distinct().Order().ToList();
                (double X, double Y) Bei(double q) { int i = (int)Math.Min(Math.Floor(q), n - 1); double u = q - i; var a = v[i % v.Count]; var b = v[(i + 1) % v.Count]; return (a.X + u * (b.X - a.X), a.Y + u * (b.Y - a.Y)); }
                LwPolyline Teil(double a, double b)   // offene Polylinie von Lage a bis b (b > a; über das Ende einer geschlossenen hinaus)
                {
                    var q = Wie(e, new LwPolyline());
                    void Dazu((double X, double Y) pt) { if (q.Vertices.Count == 0 || Math.Abs(q.Vertices[^1].Location.X - pt.X) + Math.Abs(q.Vertices[^1].Location.Y - pt.Y) > 1e-9) q.Vertices.Add(new LwPolyline.Vertex(new XY(pt.X, pt.Y))); }
                    Dazu(Bei(((a % n) + n) % n));
                    for (int i = (int)Math.Floor(a) + 1; i < b; i++) Dazu(v[((i % v.Count) + v.Count) % v.Count]);
                    Dazu(b % n == 0 && b > 0 ? v[n % v.Count] : Bei(((b % n) + n) % n));
                    return q;
                }
                if (p.IsClosed)
                {
                    if (t.Count < 2) throw new HlksFehler("Eine geschlossene Polylinie braucht zwei Schnitte mit Grenzen.", "Zwei Schnittkanten wählen oder die Polylinie löschen.");
                    double lo = t.Where(q => q <= tc).DefaultIfEmpty(t[^1] - n).Max(), hi = t.Where(q => q > tc).DefaultIfEmpty(t[0] + n).Min();
                    neu.Add(Teil(hi, lo + n));
                }
                else
                {
                    double lo = t.Where(q => q <= tc).DefaultIfEmpty(0).Max(), hi = t.Where(q => q > tc).DefaultIfEmpty(n).Min();
                    if (lo == 0 && hi == n) throw Kein();
                    if (lo > 0) neu.Add(Teil(0, lo));
                    if (hi < n) neu.Add(Teil(hi, n));
                }
                neu.RemoveAll(q => ((LwPolyline)q).Vertices.Count < 2);
                break;
            }
            default: throw new HlksFehler($"Stutzen geht nicht für ein Objekt der Art «{ZeichnungAbfragen.Art(e)}».", "Linien, Kreise, Bögen und Polylinien ohne Bögen lassen sich stutzen.");
        }
        return Ersetzen("stutzen", [(e, BlockVon(e), neu)]);
    }

    // ---------- Dehnen ----------

    static (string, Action, Action) Dehnen(Zeichnungsdatei z, List<Entity> liste, JsonObject par)
    {
        if (liste.Count != 1) throw new HlksFehler("Dehnen nimmt ein Objekt je Aufruf.", "kennungen: das Objekt; punkt: Stelle nahe dem Ende, das verlängert wird; grenzen: Zielkanten (leer = alle).");
        var e = liste[0];
        var (px, py) = Punkt(par["punkt"], "punkt");
        var grenzen = Grenzen(z, par, e);
        HlksFehler Kein() => new("In dieser Richtung liegt keine Grenze: nichts zu dehnen.", "Eine Zielkante wählen, die in der Verlängerung liegt.");
        Entity neu;
        switch (e)
        {
            case Line l:
            {
                double ax = l.StartPoint.X, ay = l.StartPoint.Y, bx = l.EndPoint.X, by = l.EndPoint.Y;
                bool ende = Math.Pow(px - bx, 2) + Math.Pow(py - by, 2) <= Math.Pow(px - ax, 2) + Math.Pow(py - ay, 2);
                var t = grenzen.SelectMany(s => ZeichnungSchnitt.GeradeMit(ax, ay, bx, by, s)).Select(q => q.T).ToList();
                var ziel = ende ? t.Where(q => q > 1 + 1e-7).DefaultIfEmpty(double.NaN).Min() : t.Where(q => q < -1e-7).DefaultIfEmpty(double.NaN).Max();
                if (double.IsNaN(ziel)) throw Kein();
                var pt = new XYZ(ax + ziel * (bx - ax), ay + ziel * (by - ay), 0);
                neu = Wie(e, new Line { StartPoint = ende ? l.StartPoint : pt, EndPoint = ende ? pt : l.EndPoint });
                break;
            }
            case Arc a:
            {
                double sweep = Norm(a.EndAngle - a.StartAngle);
                double ex = a.Center.X + a.Radius * Math.Cos(a.EndAngle), ey = a.Center.Y + a.Radius * Math.Sin(a.EndAngle);
                double sx = a.Center.X + a.Radius * Math.Cos(a.StartAngle), sy = a.Center.Y + a.Radius * Math.Sin(a.StartAngle);
                bool ende = Math.Pow(px - ex, 2) + Math.Pow(py - ey, 2) <= Math.Pow(px - sx, 2) + Math.Pow(py - sy, 2);
                var w = grenzen.SelectMany(s => ZeichnungSchnitt.KreisMit(a.Center.X, a.Center.Y, a.Radius, s)).Select(q => Norm(q.W - a.StartAngle)).Where(q => q > sweep + 1e-7).ToList();
                if (w.Count == 0) throw Kein();
                // ausserhalb des Bogens liegende Schnitte: der erste hinter dem Ende bzw. der letzte vor dem Anfang
                neu = ende ? Wie(e, new Arc { Center = a.Center, Radius = a.Radius, StartAngle = a.StartAngle, EndAngle = Norm(a.StartAngle + w.Min()) })
                           : Wie(e, new Arc { Center = a.Center, Radius = a.Radius, StartAngle = Norm(a.StartAngle + w.Max()), EndAngle = a.EndAngle });
                break;
            }
            default: throw new HlksFehler($"Dehnen geht nicht für ein Objekt der Art «{ZeichnungAbfragen.Art(e)}».", "Linien und Bögen lassen sich dehnen.");
        }
        return Ersetzen("dehnen", [(e, BlockVon(e), [neu])]);
    }

    // ---------- Abrunden und Fasen ----------

    static (string, Action, Action) Ecke(List<Entity> liste, JsonObject par, bool fase)
    {
        string was = fase ? "Fasen" : "Abrunden";
        if (liste.Count != 2 || liste[0] is not Line l1 || liste[1] is not Line l2)
            throw new HlksFehler($"{was} braucht genau zwei Linien.", "kennungen: zwei Linien; punkte: je ein Punkt auf dem Teil, der bleibt.");
        double a1 = fase ? Json.Zahl(par["abstand1"]) ?? Json.Zahl(par["abstand"]) ?? throw new HlksFehler("«abstand» fehlt.") : 0;
        double a2 = fase ? Json.Zahl(par["abstand2"]) ?? a1 : 0;
        double r = fase ? 0 : Json.Zahl(par["radius"]) ?? throw new HlksFehler("«radius» fehlt.", "Radius angeben; 0 macht eine scharfe Ecke.");
        if (r < 0 || a1 < 0 || a2 < 0) throw new HlksFehler("Radius bzw. Abstand dürfen nicht negativ sein.");
        var schnitt = ZeichnungSchnitt.GeradeMit(l1.StartPoint.X, l1.StartPoint.Y, l1.EndPoint.X, l1.EndPoint.Y,
            GeoStueck.Strecke(l2.StartPoint.X - 1e9 * (l2.EndPoint.X - l2.StartPoint.X), l2.StartPoint.Y - 1e9 * (l2.EndPoint.Y - l2.StartPoint.Y),
                              l2.StartPoint.X + 1e9 * (l2.EndPoint.X - l2.StartPoint.X), l2.StartPoint.Y + 1e9 * (l2.EndPoint.Y - l2.StartPoint.Y))).ToList();
        if (schnitt.Count == 0) throw new HlksFehler("Die beiden Linien sind parallel.", $"{was} geht nur zwischen Linien, die sich (verlängert) schneiden.");
        double xx = schnitt[0].X, xy = schnitt[0].Y;
        var klick = par["punkte"] as JsonArray;
        // je Linie: Richtung vom Schnittpunkt zum Teil, der bleibt (Klickpunkt, sonst das fernere Ende), und das Ende in dieser Richtung
        (double Dx, double Dy, XYZ Ende) Seite(Line l, int i)
        {
            (double X, double Y) ziel;
            if (klick is { Count: 2 }) ziel = Punkt(klick[i], "punkte");
            else ziel = Math.Pow(l.StartPoint.X - xx, 2) + Math.Pow(l.StartPoint.Y - xy, 2) > Math.Pow(l.EndPoint.X - xx, 2) + Math.Pow(l.EndPoint.Y - xy, 2) ? (l.StartPoint.X, l.StartPoint.Y) : (l.EndPoint.X, l.EndPoint.Y);
            double lx = l.EndPoint.X - l.StartPoint.X, ly = l.EndPoint.Y - l.StartPoint.Y, len = Math.Sqrt(lx * lx + ly * ly);
            lx /= len; ly /= len;
            double s = (ziel.X - xx) * lx + (ziel.Y - xy) * ly;
            if (Math.Abs(s) < 1e-9) s = (l.EndPoint.X - xx) * lx + (l.EndPoint.Y - xy) * ly + (l.StartPoint.X - xx) * lx + (l.StartPoint.Y - xy) * ly;
            if (s < 0) { lx = -lx; ly = -ly; }
            var ende = (l.EndPoint.X - xx) * lx + (l.EndPoint.Y - xy) * ly >= (l.StartPoint.X - xx) * lx + (l.StartPoint.Y - xy) * ly ? l.EndPoint : l.StartPoint;
            return (lx, ly, ende);
        }
        var s1 = Seite(l1, 0); var s2 = Seite(l2, 1);
        double cos = Math.Clamp(s1.Dx * s2.Dx + s1.Dy * s2.Dy, -1, 1), theta = Math.Acos(cos);
        double t1 = fase ? a1 : r / Math.Tan(theta / 2), t2 = fase ? a2 : t1;
        double Reicht(XYZ ende, double dx, double dy) => (ende.X - xx) * dx + (ende.Y - xy) * dy;
        if (Reicht(s1.Ende, s1.Dx, s1.Dy) < t1 - 1e-9 || Reicht(s2.Ende, s2.Dx, s2.Dy) < t2 - 1e-9)
            throw new HlksFehler(fase ? "Der Abstand ist länger als eine der Linien." : "Der Radius ist für diese Linien zu gross.", "Kleineren Wert wählen.");
        var p1 = new XYZ(xx + s1.Dx * t1, xy + s1.Dy * t1, 0); var p2 = new XYZ(xx + s2.Dx * t2, xy + s2.Dy * t2, 0);
        var neu1 = new List<Entity> { Wie(l1, new Line { StartPoint = p1, EndPoint = s1.Ende }) };
        var neu2 = new List<Entity> { Wie(l2, new Line { StartPoint = p2, EndPoint = s2.Ende }) };
        if (fase && (a1 > 0 || a2 > 0)) neu1.Add(Wie(l1, new Line { StartPoint = p1, EndPoint = p2 }));
        if (!fase && r > 0)
        {
            double mx = s1.Dx + s2.Dx, my = s1.Dy + s2.Dy, ml = Math.Sqrt(mx * mx + my * my), abst = r / Math.Sin(theta / 2);
            double cx = xx + mx / ml * abst, cy = xy + my / ml * abst;
            double w1 = Math.Atan2(p1.Y - cy, p1.X - cx), w2 = Math.Atan2(p2.Y - cy, p2.X - cx);
            bool links = (p1.X - cx) * (p2.Y - cy) - (p1.Y - cy) * (p2.X - cx) > 0;   // kurzer Weg von p1 nach p2 im Gegenuhrzeigersinn?
            neu1.Add(Wie(l1, new Arc { Center = new XYZ(cx, cy, 0), Radius = r, StartAngle = Norm(links ? w1 : w2), EndAngle = Norm(links ? w2 : w1) }));
        }
        return Ersetzen(fase ? "fasen" : "abrunden", [(l1, BlockVon(l1), neu1), (l2, BlockVon(l2), neu2)]);
    }

    // ---------- Ursprung (zerlegen) und Verbinden ----------

    static (string, Action, Action) Ursprung(List<Entity> liste)
    {
        var alle = new List<(Entity, BlockRecord, List<Entity>)>();
        foreach (var e in liste)
        {
            var neu = new List<Entity>();
            switch (e)
            {
                case LwPolyline p:
                    foreach (var s in ZeichnungSchnitt.Stuecke(p))
                        neu.Add(s.Bogen ? Wie(e, new Arc { Center = new XYZ(s.Ax, s.Ay, 0), Radius = s.R, StartAngle = Norm(s.Von), EndAngle = Norm(s.Von + s.Sweep) })
                                        : Wie(e, new Line { StartPoint = new XYZ(s.Ax, s.Ay, 0), EndPoint = new XYZ(s.Bx, s.By, 0) }));
                    break;
                case Insert ins:
                    try { neu.AddRange(ins.Explode().Select(x => x.Owner is null ? x : (Entity)x.Clone())); }
                    catch (Exception ex) when (ex is not OutOfMemoryException) { throw new HlksFehler($"Der Block «{ins.Block?.Name}» lässt sich nicht zerlegen: {ex.Message}"); }
                    break;
                default: throw new HlksFehler($"Ursprung geht nicht für ein Objekt der Art «{ZeichnungAbfragen.Art(e)}».", "Polylinien und Blöcke lassen sich zerlegen.");
            }
            if (neu.Count == 0) throw new HlksFehler($"Das Objekt {Zeichnungsdatei.Kennung(e)} enthält nichts zum Zerlegen.");
            alle.Add((e, BlockVon(e), neu));
        }
        return Ersetzen($"{liste.Count} zerlegen", alle);
    }

    static (string, Action, Action) Verbinden(List<Entity> liste)
    {
        // Ketten aus Punkten mit Ausbuchtung zum nächsten Punkt
        var ketten = new List<List<(double X, double Y, double B)>>();
        foreach (var e in liste)
        {
            switch (e)
            {
                case Line l: ketten.Add([(l.StartPoint.X, l.StartPoint.Y, 0), (l.EndPoint.X, l.EndPoint.Y, 0)]); break;
                case Arc a:
                {
                    double sweep = Norm(a.EndAngle - a.StartAngle);
                    ketten.Add([(a.Center.X + a.Radius * Math.Cos(a.StartAngle), a.Center.Y + a.Radius * Math.Sin(a.StartAngle), Math.Tan(sweep / 4)),
                                (a.Center.X + a.Radius * Math.Cos(a.EndAngle), a.Center.Y + a.Radius * Math.Sin(a.EndAngle), 0)]);
                    break;
                }
                case LwPolyline { IsClosed: false } p: ketten.Add(p.Vertices.Select(v => (v.Location.X, v.Location.Y, v.Bulge)).ToList()); break;
                default: throw new HlksFehler($"Verbinden geht nicht für ein Objekt der Art «{ZeichnungAbfragen.Art(e)}».", "Linien, Bögen und offene Polylinien lassen sich verbinden.");
            }
        }
        if (ketten.Count < 2) throw new HlksFehler("Verbinden braucht mindestens zwei Objekte.");
        double tol = 1e-6 * Math.Max(1, ketten.SelectMany(k => k).Max(q => Math.Abs(q.X) + Math.Abs(q.Y)));
        bool Gleich((double X, double Y, double B) a, (double X, double Y, double B) b) => Math.Abs(a.X - b.X) <= tol && Math.Abs(a.Y - b.Y) <= tol;
        static List<(double X, double Y, double B)> Um(List<(double X, double Y, double B)> k)   // Richtung umkehren: Ausbuchtungen wandern zum Vorgänger und wechseln das Vorzeichen
        {
            var r = new List<(double, double, double)>();
            for (int i = k.Count - 1; i >= 0; i--) r.Add((k[i].X, k[i].Y, i > 0 ? -k[i - 1].B : 0));
            return r;
        }
        var kette = ketten[0]; ketten.RemoveAt(0);
        while (ketten.Count > 0)
        {
            int i = ketten.FindIndex(k => Gleich(k[0], kette[^1]) || Gleich(k[^1], kette[^1]) || Gleich(k[^1], kette[0]) || Gleich(k[0], kette[0]));
            if (i < 0) throw new HlksFehler("Die Objekte hängen nicht alle an ihren Enden zusammen.", "Nur Objekte wählen, deren Endpunkte aufeinander liegen (Fang Endpunkt).");
            var k = ketten[i]; ketten.RemoveAt(i);
            if (Gleich(k[0], kette[^1])) { kette[^1] = (kette[^1].X, kette[^1].Y, k[0].B); kette.AddRange(k.Skip(1)); }
            else if (Gleich(k[^1], kette[^1])) { var u = Um(k); kette[^1] = (kette[^1].X, kette[^1].Y, u[0].B); kette.AddRange(u.Skip(1)); }
            else if (Gleich(k[^1], kette[0])) { var neu = k.Take(k.Count - 1).ToList(); neu.AddRange(kette); kette = neu; }
            else { var u = Um(k); var neu = u.Take(u.Count - 1).ToList(); neu.AddRange(kette); kette = neu; }
        }
        var p2 = Wie(liste[0], new LwPolyline());
        if (kette.Count > 2 && Gleich(kette[0], kette[^1])) { p2.IsClosed = true; kette.RemoveAt(kette.Count - 1); }
        foreach (var (x, y, b) in kette) p2.Vertices.Add(new LwPolyline.Vertex(new XY(x, y)) { Bulge = b });
        var ersatz = liste.Select((e, i) => (e, BlockVon(e), i == 0 ? new List<Entity> { p2 } : [])).ToList();
        return Ersetzen($"{liste.Count} verbinden", ersatz);
    }

    // ---------- Reihe, Eigenschaften übertragen ----------

    static (string, Action, Action) Reihe(List<Entity> liste, JsonObject par)
    {
        int zeilen = (int)(Json.Zahl(par["zeilen"]) ?? 1), spalten = (int)(Json.Zahl(par["spalten"]) ?? 1);
        double dx = Json.Zahl(par["dx"]) ?? 0, dy = Json.Zahl(par["dy"]) ?? 0;
        if (zeilen < 1 || spalten < 1 || (long)zeilen * spalten < 2) throw new HlksFehler("Eine Reihe braucht mindestens zwei Plätze.", "zeilen und spalten angeben (zusammen mindestens 2), dazu dx (Spaltenabstand) und dy (Zeilenabstand).");
        if ((long)zeilen * spalten * liste.Count > 20000) throw new HlksFehler("Die Reihe ergäbe mehr als 20 000 Objekte.");
        if ((spalten > 1 && dx == 0) || (zeilen > 1 && dy == 0)) throw new HlksFehler("Der Abstand der Reihe fehlt.", "dx für den Spaltenabstand, dy für den Zeilenabstand angeben.");
        var neu = new List<(Entity, BlockRecord)>();
        foreach (var e in liste)
            for (int i = 0; i < zeilen; i++)
                for (int j = 0; j < spalten; j++)
                {
                    if (i == 0 && j == 0) continue;
                    var k = (Entity)e.Clone();
                    k.ApplyTransform(Transform.CreateTranslation(new XYZ(j * dx, i * dy, 0)));
                    neu.Add((k, BlockVon(e)));
                }
        return Dazu($"Reihe {zeilen} × {spalten}", neu);
    }

    static (string, Action, Action) Uebertragen(Zeichnungsdatei z, List<Entity> liste, JsonObject par)
    {
        var q = z.Objekt(par["quelle"] is JsonValue v && v.TryGetValue<string>(out var s) ? s : throw new HlksFehler("«quelle» fehlt.", "quelle: Kennung des Objekts, dessen Eigenschaften übernommen werden."));
        var alt = liste.Select(e => (E: e, e.Layer, e.Color, e.LineType, e.LineWeight, e.LineTypeScale)).ToList();
        return ($"Eigenschaften auf {liste.Count} übertragen",
            () => { foreach (var e in liste) Wie(q, e); },
            () => { foreach (var a in alt) { a.E.Layer = a.Layer; a.E.Color = a.Color; a.E.LineType = a.LineType; a.E.LineWeight = a.LineWeight; a.E.LineTypeScale = a.LineTypeScale; } });
    }

    // ---------- Weitere Objektarten (Zeichnen) ----------

    /// <summary>Baut ein Objekt der Arten ellipse, polygon, punkt, absatztext, schraffur, bemassung; null = Art unbekannt.</summary>
    public static Entity? Bauen(Zeichnungsdatei z, string art, JsonObject o)
    {
        double Z(string name, double? vorgabe = null) => Json.Zahl(o[name]) ?? vorgabe ?? throw new HlksFehler($"«{name}» fehlt oder ist keine Zahl.");
        switch (art)
        {
            case "ellipse":
            {
                var (mx, my) = Punkt(o["mitte"], "mitte"); var (hx, hy) = Punkt(o["hauptachse"], "hauptachse");
                double v = Z("verhaeltnis");
                if (!(v > 0) || v > 1) throw new HlksFehler("«verhaeltnis» (Nebenachse zu Hauptachse) muss zwischen 0 und 1 liegen.");
                if (hx == mx && hy == my) throw new HlksFehler("Die Hauptachse hat keine Länge.", "hauptachse: Endpunkt der Hauptachse.");
                return new Ellipse { Center = new XYZ(mx, my, 0), MajorAxisEndPoint = new XYZ(hx - mx, hy - my, 0), RadiusRatio = v, StartParameter = 0, EndParameter = 2 * Math.PI };
            }
            case "polygon":
            {
                var (mx, my) = Punkt(o["mitte"], "mitte");
                int n = (int)Z("seiten");
                double r = Z("radius"), dreh = Z("drehungGrad", 0) * Math.PI / 180;
                if (n < 3 || n > 1024) throw new HlksFehler("Ein Polygon hat 3 bis 1024 Seiten.");
                if (!(r > 0)) throw new HlksFehler("Der Radius muss grösser als 0 sein.");
                bool inkreis = o["inkreis"] is JsonValue iv && iv.TryGetValue<bool>(out var ik) && ik;   // Seitenmitten auf dem Kreis statt der Ecken
                if (inkreis) { r /= Math.Cos(Math.PI / n); dreh += Math.PI / n; }
                var p = new LwPolyline { IsClosed = true };
                for (int i = 0; i < n; i++) p.Vertices.Add(new LwPolyline.Vertex(new XY(mx + r * Math.Cos(dreh + 2 * Math.PI * i / n), my + r * Math.Sin(dreh + 2 * Math.PI * i / n))));
                return p;
            }
            case "punkt":
            {
                var (x, y) = Punkt(o["ort"], "ort");
                return new Point { Location = new XYZ(x, y, 0) };
            }
            case "absatztext":
            {
                var (x, y) = Punkt(o["ort"], "ort");
                var text = (o["text"] as JsonValue)?.TryGetValue<string>(out var t) == true ? t : "";
                if (string.IsNullOrWhiteSpace(text)) throw new HlksFehler("Der Text fehlt.");
                double h = Z("hoehe");
                if (!(h > 0)) throw new HlksFehler("Die Texthöhe muss grösser als 0 sein.");
                var m = new MText { InsertPoint = new XYZ(x, y, 0), Value = text.Replace("\r", "").Replace("\\", "\\\\").Replace("\n", "\\P"), Height = h, RectangleWidth = Z("breite", 0) };
                if (Z("drehungGrad", 0) is var grad && grad != 0) m.AlignmentPoint = new XYZ(Math.Cos(grad * Math.PI / 180), Math.Sin(grad * Math.PI / 180), 0);
                if (z.Dokument.Header.CurrentTextStyle is { } stil) m.Style = stil;
                return m;
            }
            case "schraffur":
            {
                var h = new Hatch();
                var muster = ((o["muster"] as JsonValue)?.TryGetValue<string>(out var ms) == true ? ms : "voll").Trim().ToLowerInvariant();
                if (muster == "voll") { h.IsSolid = true; h.Pattern = HatchPattern.Solid; }
                else if (muster == "linien" || muster == "kreuz")
                {
                    double w = Z("winkelGrad", 45) * Math.PI / 180, ab = Z("abstand");
                    if (!(ab > 0)) throw new HlksFehler("Der Abstand der Schraffurlinien muss grösser als 0 sein.");
                    var pat = new HatchPattern(muster == "kreuz" ? "ANSI37" : "ANSI31");
                    foreach (var winkel in muster == "kreuz" ? new[] { w, w + Math.PI / 2 } : [w])
                        pat.Lines.Add(new HatchPattern.Line { Angle = winkel, BasePoint = new XY(0, 0), Offset = new XY(-Math.Sin(winkel) * ab, Math.Cos(winkel) * ab) });
                    h.IsSolid = false; h.Pattern = pat; h.PatternType = HatchPatternType.Custom;   // PatternAngle bleibt 0: die Linien tragen den Winkel selbst (sonst dreht ACadSharp sie noch einmal)
                }
                else throw new HlksFehler($"Das Muster «{muster}» gibt es nicht.", "Erlaubt: voll, linien, kreuz.");
                void Pfad(IEnumerable<(double X, double Y, double B)> ecken)
                {
                    var kante = new Hatch.BoundaryPath.Polyline { IsClosed = true };
                    foreach (var (x, y, b) in ecken) kante.Vertices.Add(new XYZ(x, y, b));
                    if (kante.Vertices.Count < 2) throw new HlksFehler("Eine Grenze der Schraffur hat zu wenige Punkte.");
                    var pfad = new Hatch.BoundaryPath { Flags = BoundaryPathFlags.Polyline | BoundaryPathFlags.External };
                    pfad.Edges.Add(kante);
                    h.Paths.Add(pfad);
                }
                foreach (var n in o["grenzen"] as JsonArray ?? [])
                    Pfad((n as JsonArray ?? []).Select(q => { var (x, y) = Punkt(q, "grenzen"); return (x, y, q is JsonArray { Count: >= 3 } d && Json.Zahl(d[2]) is double b ? b : 0.0); }));
                foreach (var n in o["objekte"] as JsonArray ?? [])
                {
                    var e = z.Objekt(n is JsonValue v && v.TryGetValue<string>(out var s) ? s : "");
                    switch (e)
                    {
                        case LwPolyline { IsClosed: true } p: Pfad(p.Vertices.Select(q => (q.Location.X, q.Location.Y, q.Bulge))); break;
                        case Circle c when c is not Arc: Pfad([(c.Center.X - c.Radius, c.Center.Y, 1), (c.Center.X + c.Radius, c.Center.Y, 1)]); break;   // zwei Halbkreise
                        default: throw new HlksFehler($"Das Objekt {Zeichnungsdatei.Kennung(e)} ({ZeichnungAbfragen.Art(e)}) ist keine geschlossene Grenze.", "Geschlossene Polylinien (auch Rechtecke) und Kreise lassen sich schraffieren.");
                    }
                }
                if (h.Paths.Count == 0) throw new HlksFehler("Die Schraffur hat keine Grenze.", "grenzen: [[[x, y], …], …] oder objekte: [Kennungen geschlossener Polylinien bzw. Kreise].");
                return h;
            }
            case "bemassung":
            {
                var (ax, ay) = Punkt(o["von"], "von"); var (bx, by) = Punkt(o["bis"], "bis"); var (ox, oy) = Punkt(o["ort"], "ort");
                if (ax == bx && ay == by) throw new HlksFehler("Die beiden Masspunkte sind gleich.");
                var richtung = ((o["richtung"] as JsonValue)?.TryGetValue<string>(out var rs) == true ? rs : "ausgerichtet").Trim().ToLowerInvariant();
                DimensionAligned d;
                double w;
                if (richtung == "ausgerichtet") { w = Math.Atan2(by - ay, bx - ax); d = new DimensionAligned(); }
                else
                {
                    w = richtung switch { "waagrecht" => 0, "senkrecht" => Math.PI / 2, _ => throw new HlksFehler($"Die Richtung «{richtung}» gibt es nicht.", "Erlaubt: ausgerichtet, waagrecht, senkrecht.") };
                    d = new DimensionLinear { Rotation = w };
                }
                // Punkt der Masslinie über dem zweiten Masspunkt: Lot von «bis» auf die Masslinie durch «ort»
                double nx = -Math.Sin(w), ny = Math.Cos(w), ab = (ox - bx) * nx + (oy - by) * ny;
                d.FirstPoint = new XYZ(ax, ay, 0); d.SecondPoint = new XYZ(bx, by, 0);
                d.DefinitionPoint = new XYZ(bx + nx * ab, by + ny * ab, 0);
                if (z.Dokument.Header.CurrentDimensionStyle is { } stil) d.Style = stil;
                return d;
            }
            default: return null;
        }
    }
}
