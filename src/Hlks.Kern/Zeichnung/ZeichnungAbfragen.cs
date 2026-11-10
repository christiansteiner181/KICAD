// 2D-CAD «Zeichnung», Stufe 1: Auskünfte über eine geöffnete Zeichnung – Überblick, Layer, Objekte suchen, messen.
// Antworten als JSON für Oberfläche und MCP. Längen in Zeichnungseinheiten; die Einheit und – wenn sie bekannt ist – die
// Umrechnung in Meter stehen in jeder Antwort mit Massen.

using System.Text.Json.Nodes;
using ACadSharp.Entities;
using ACadSharp.Tables;

namespace Hlks.Kern;

public static class ZeichnungAbfragen
{
    public const string OeffnenText = "Öffnet eine DWG- oder DXF-Datei aus den freigegebenen Ordnern als Zeichnung (2D-CAD). Antwort: Fassung, Einheit, Layouts, " +
        "Anzahl Objekte je Art, Layer, Meldungen des Lesers (was nicht verstanden wurde, bleibt in der Datei erhalten). Die Zeichnung bleibt geöffnet; weitere Werkzeuge " +
        "beziehen sich ohne Angabe auf die zuletzt geöffnete.";
    public const string DateienText = "Listet die geöffneten Zeichnungen und die DWG/DXF-Dateien der freigegebenen Ordner (mit Unterordnern).";
    public const string LayerText = "Layer der Zeichnung: Name, ein/aus, gefroren, gesperrt, Farbe, Linientyp, Linienstärke, Anzahl Objekte im Modellbereich.";
    public const string LayerSetzenText = "Schaltet Layer der geöffneten Zeichnung ein oder aus (nur Anzeige in dieser Sitzung; die Datei wird nicht geändert). " +
        "Namen oder Muster mit *, z. B. «A-*».";
    public const string ObjekteText = "Sucht Objekte der Zeichnung nach Layout, Layer, Art, Text, Fenster oder Kennung. Antwort je Objekt: kennung (Handle), art, layer, " +
        "box und die Angaben der Art (Punkte, Länge, Fläche, Text, Blockname und Attribute, Masszahl). Koordinaten in Zeichnungseinheiten.";
    public const string MessenText = "Misst in der Zeichnung: mit punkte [[x,y],…] die Strecke (2 Punkte), den Linienzug bzw. Winkel am mittleren Punkt (3 Punkte) " +
        "und die umschlossene Fläche (ab 3 Punkten); mit objekt (Kennung) Länge und Fläche des Objekts. Ergebnis in Zeichnungseinheiten und, bei bekannter Einheit, in m bzw. m².";

    static double R(double v, int stellen = 4) => Math.Round(v, stellen);
    static JsonArray P(double x, double y) => [R(x), R(y)];

    static string Einheit(Zeichnungsdatei z) => z.EinheitBekannt ? z.Dokument.Header.InsUnits.ToString() : "unbekannt (Annahme mm)";

    // ---------- Überblick ----------

    public static JsonObject Info(Zeichnungsdatei z)
    {
        var d = z.Dokument;
        var arten = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var e in d.ModelSpace.Entities) { var a = Art(e); arten[a] = arten.GetValueOrDefault(a) + 1; }
        var layouts = new JsonArray();
        foreach (var name in z.Layouts())
        {
            var l = z.Layout(name);
            var o = new JsonObject { ["name"] = name, ["objekte"] = (l?.AssociatedBlock ?? d.ModelSpace).Entities.Count };
            if (l is not null)
            {
                o["ansichtsfenster"] = l.AssociatedBlock?.Entities.OfType<Viewport>().Count(v => !v.RepresentsPaper && v.Id != 1) ?? 0;
                var (b, h) = ZeichnungAusgabe.Papier(l);
                if (b > 0 && h > 0) o["papierMm"] = new JsonArray(R(b, 1), R(h, 1));
            }
            layouts.Add(o);
        }
        var meldungen = new JsonArray();
        foreach (var (text, n) in z.Meldungen.Take(40)) meldungen.Add(n > 1 ? $"{n}× {text}" : text);
        return new JsonObject
        {
            ["zeichnung"] = z.Name,
            ["pfad"] = z.Pfad,
            ["fassung"] = d.Header.Version.ToString(),
            ["einheit"] = Einheit(z),
            ["mmJeEinheit"] = z.MmJeEinheit,
            ["layouts"] = layouts,
            ["layer"] = d.Layers.Count(),
            ["bloecke"] = d.BlockRecords.Count(b => !b.Name.StartsWith('*')),
            ["objekteModell"] = new JsonObject(arten.Select(a => KeyValuePair.Create(a.Key, (JsonNode?)a.Value))),
            ["meldungenLeser"] = meldungen,
        };
    }

    // ---------- Layer ----------

    public static JsonObject LayerListe(Zeichnungsdatei z)
    {
        var anzahl = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in z.Dokument.ModelSpace.Entities) { var n = e.Layer?.Name ?? "0"; anzahl[n] = anzahl.GetValueOrDefault(n) + 1; }
        var liste = new JsonArray();
        foreach (var l in z.Dokument.Layers.OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase))
        {
            var o = new JsonObject { ["name"] = l.Name, ["ein"] = l.IsOn };
            if (l.Flags.HasFlag(LayerFlags.Frozen)) o["gefroren"] = true;
            if (l.Flags.HasFlag(LayerFlags.Locked)) o["gesperrt"] = true;
            if (!l.PlotFlag) o["plotten"] = false;
            o["farbe"] = l.Color.IsTrueColor ? $"#{l.Color.R:x2}{l.Color.G:x2}{l.Color.B:x2}" : "ACI " + l.Color.Index;
            if (l.LineType is { } lt && !lt.Name.Equals("Continuous", StringComparison.OrdinalIgnoreCase)) o["linientyp"] = lt.Name;
            if ((int)l.LineWeight >= 0) o["staerkeMm"] = (int)l.LineWeight / 100.0;
            o["objekte"] = anzahl.GetValueOrDefault(l.Name);
            liste.Add(o);
        }
        return new JsonObject { ["zeichnung"] = z.Name, ["anzahl"] = liste.Count, ["layer"] = liste };
    }

    public static JsonObject LayerSetzen(Zeichnungsdatei z, IEnumerable<string> layer, bool ein)
    {
        var treffer = z.LayerSuchen(layer);
        foreach (var l in treffer) l.IsOn = ein;
        return new JsonObject
        {
            ["zeichnung"] = z.Name, ["ein"] = ein, ["geaendert"] = new JsonArray(treffer.Select(l => (JsonNode?)l.Name).ToArray()),
            ["hinweis"] = "Gilt für die Anzeige in dieser Sitzung; die Datei ist nicht geändert.",
        };
    }

    // ---------- Objekte ----------

    /// <summary>Deutscher Name der Objektart.</summary>
    public static string Art(Entity e) => e switch
    {
        Line => "linie",
        LwPolyline or Polyline2D or Polyline3D => "polylinie",
        Arc => "bogen",
        Circle => "kreis",
        Ellipse => "ellipse",
        Spline => "spline",
        AttributeDefinition => "attributdefinition",
        TextEntity => "text",
        MText => "mtext",
        Dimension => "bemassung",
        Insert => "block",
        Hatch => "schraffur",
        Leader => "fuehrung",
        Viewport => "ansichtsfenster",
        Solid => "flaeche",
        Point => "punkt",
        _ => e.GetType().Name,
    };

    static double[]? Box(Entity e)
    {
        try
        {
            var b = e.GetBoundingBox();
            double[] w = [b.Min.X, b.Min.Y, b.Max.X, b.Max.Y];
            return w.Any(v => double.IsNaN(v) || double.IsInfinity(v)) || w[2] < w[0] || w[3] < w[1] ? null : w;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return null; }
    }

    static string? TextVon(Entity e) => e switch
    {
        MText m => string.Join("\n", CadText.MText(m.Value)),
        TextEntity t => CadText.Einfach(t.Value),
        Dimension d => string.IsNullOrWhiteSpace(d.Text) || d.Text == "<>" ? null : CadText.Einfach(d.Text),
        _ => null,
    };

    static List<(double X, double Y, double Bulge)>? Ecken(Entity e, out bool geschlossen)
    {
        geschlossen = false;
        switch (e)
        {
            case LwPolyline lw: geschlossen = lw.IsClosed; return lw.Vertices.Select(v => (v.Location.X, v.Location.Y, v.Bulge)).ToList();
            case Polyline2D p2: geschlossen = p2.IsClosed; return p2.Vertices.Select(v => (v.Location.X, v.Location.Y, v.Bulge)).ToList();
            case Polyline3D p3: geschlossen = p3.IsClosed; return p3.Vertices.Select(v => (v.Location.X, v.Location.Y, 0.0)).ToList();
            default: return null;
        }
    }

    /// <summary>Länge und Fläche eines Objekts in Zeichnungseinheiten (null = nicht bestimmbar).</summary>
    public static (double? Laenge, double? Flaeche) Masse(Entity e)
    {
        switch (e)
        {
            case Line l: return (Math.Sqrt(Math.Pow(l.EndPoint.X - l.StartPoint.X, 2) + Math.Pow(l.EndPoint.Y - l.StartPoint.Y, 2)), null);
            case Arc a:
            {
                double w = ((a.EndAngle - a.StartAngle) % (2 * Math.PI) + 2 * Math.PI) % (2 * Math.PI);
                return (a.Radius * (w < 1e-12 ? 2 * Math.PI : w), null);
            }
            case Circle c: return (2 * Math.PI * c.Radius, Math.PI * c.Radius * c.Radius);
            case Ellipse el:
            {
                var (pts, voll) = ZeichnungGeometrie.EllipsePunkte(el);
                return (Zug(pts, voll), voll ? Math.Abs(ZeichnungGeometrie.Flaeche(pts)) : null);
            }
            case Spline sp:
            {
                var pts = ZeichnungGeometrie.SplinePunkte(sp);
                return (Zug(pts, sp.IsClosed), sp.IsClosed ? Math.Abs(ZeichnungGeometrie.Flaeche(pts)) : null);
            }
            case Hatch h:
            {
                // gerade/ungerade: Ringe nach Grösse, abwechselnd dazu und weg (Inseln)
                var ringe = h.Paths.Select(p => p.GetPoints(32).Select(q => (q.X, q.Y)).ToList()).Where(r => r.Count >= 3)
                    .Select(r => Math.Abs(ZeichnungGeometrie.Flaeche(r))).OrderByDescending(a => a).ToList();
                if (ringe.Count == 0) return (null, null);
                return (null, ringe.Count == 1 ? ringe[0] : ringe[0] - ringe.Skip(1).Sum());
            }
        }
        if (Ecken(e, out bool zu) is { } v) { var (l, f) = ZeichnungGeometrie.Polylinie(v, zu); return (l, f); }
        return (null, null);
    }

    static double Zug(IReadOnlyList<(double X, double Y)> p, bool geschlossen)
    {
        double l = 0;
        for (int i = 0; i + 1 < p.Count; i++) l += Math.Sqrt(Math.Pow(p[i + 1].X - p[i].X, 2) + Math.Pow(p[i + 1].Y - p[i].Y, 2));
        if (geschlossen && p.Count > 2) l += Math.Sqrt(Math.Pow(p[0].X - p[^1].X, 2) + Math.Pow(p[0].Y - p[^1].Y, 2));
        return l;
    }

    static JsonObject Beschreiben(Entity e)
    {
        var o = new JsonObject { ["kennung"] = Zeichnungsdatei.Kennung(e), ["art"] = Art(e), ["layer"] = e.Layer?.Name ?? "0" };
        if (Box(e) is { } b) o["box"] = new JsonArray(R(b[0]), R(b[1]), R(b[2]), R(b[3]));
        var (laenge, flaeche) = Masse(e);
        switch (e)
        {
            case Line l: o["von"] = P(l.StartPoint.X, l.StartPoint.Y); o["bis"] = P(l.EndPoint.X, l.EndPoint.Y); break;
            case Arc a: o["mitte"] = P(a.Center.X, a.Center.Y); o["radius"] = R(a.Radius); o["vonGrad"] = R(a.StartAngle * 180 / Math.PI, 2); o["bisGrad"] = R(a.EndAngle * 180 / Math.PI, 2); break;
            case Circle c: o["mitte"] = P(c.Center.X, c.Center.Y); o["radius"] = R(c.Radius); break;
            case TextEntity t: o["ort"] = P(t.InsertPoint.X, t.InsertPoint.Y); o["hoehe"] = R(t.Height); if (t.Rotation != 0) o["drehungGrad"] = R(t.Rotation * 180 / Math.PI, 2); break;
            case MText m: o["ort"] = P(m.InsertPoint.X, m.InsertPoint.Y); o["hoehe"] = R(m.Height); if (m.Rotation != 0) o["drehungGrad"] = R(m.Rotation * 180 / Math.PI, 2); break;
            case Dimension d: o["masszahl"] = R(d.Measurement); o["textOrt"] = P(d.TextMiddlePoint.X, d.TextMiddlePoint.Y); break;
            case Insert ins:
                o["block"] = ins.Block?.Name;
                o["ort"] = P(ins.InsertPoint.X, ins.InsertPoint.Y);
                if (ins.Rotation != 0) o["drehungGrad"] = R(ins.Rotation * 180 / Math.PI, 2);
                if (ins.XScale != 1 || ins.YScale != 1) o["massstab"] = new JsonArray(R(ins.XScale), R(ins.YScale));
                if (ins.HasAttributes)
                {
                    var att = new JsonObject();
                    foreach (var a in ins.Attributes) att[a.Tag ?? ""] = CadText.Einfach(a.Value);
                    o["attribute"] = att;
                }
                break;
            case Hatch h: o["muster"] = h.IsSolid ? "voll" : h.Pattern?.Name; break;
        }
        if (Ecken(e, out bool zu) is { } v)
        {
            o["geschlossen"] = zu;
            o["anzahlPunkte"] = v.Count;
            o["punkte"] = new JsonArray(v.Take(40).Select(q => (JsonNode?)(q.Bulge == 0 ? P(q.X, q.Y) : new JsonArray(R(q.X), R(q.Y), R(q.Bulge, 6)))).ToArray());
            if (v.Any(q => q.Bulge != 0)) o["hinweis"] = "Dritte Zahl eines Punkts: Ausbuchtung zum nächsten Punkt (tan(Öffnungswinkel/4), > 0 Gegenuhrzeigersinn).";
        }
        if (TextVon(e) is { Length: > 0 } text) o["text"] = text.Length > 500 ? text[..500] + "…" : text;
        if (laenge is double ll) o["laenge"] = R(ll);
        if (flaeche is double ff) o["flaeche"] = R(ff);
        if (ZeichnungWeitere.Griffe(e) is { Count: > 0 and <= 60 } griffe) o["griffe"] = new JsonArray(griffe.Select(g => (JsonNode?)P(g.X, g.Y)).ToArray());
        return o;
    }

    public static JsonObject Objekte(Zeichnungsdatei z, string? layout, string? layer, string? art, string? text, double[]? fenster, string? kennung, int seite, int proSeite)
    {
        if (fenster is not null && fenster.Length != 4) throw new HlksFehler("fenster muss [x0, y0, x1, y1] sein.", "Vier Zahlen in Zeichnungseinheiten.");
        proSeite = Math.Clamp(proSeite, 1, 500);
        seite = Math.Max(1, seite);
        IEnumerable<Entity> alle;
        if (!string.IsNullOrWhiteSpace(kennung)) alle = kennung.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries).Select(z.Objekt).ToList();
        else alle = z.Objekte(z.Layout(layout));
        var layerMuster = layer?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var arten = art?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        double fx0 = 0, fy0 = 0, fx1 = 0, fy1 = 0;
        if (fenster is not null) { fx0 = Math.Min(fenster[0], fenster[2]); fx1 = Math.Max(fenster[0], fenster[2]); fy0 = Math.Min(fenster[1], fenster[3]); fy1 = Math.Max(fenster[1], fenster[3]); }

        bool Trifft(Entity e)
        {
            if (layerMuster is { Length: > 0 } && !layerMuster.Any(m => Zeichnungsdatei.Passt(e.Layer?.Name ?? "0", m))) return false;
            if (arten is { Count: > 0 } && !arten.Contains(Art(e))) return false;
            if (!string.IsNullOrEmpty(text))
            {
                bool hat = TextVon(e)?.Contains(text, StringComparison.OrdinalIgnoreCase) == true;
                if (!hat && e is Insert ins)
                    hat = ins.Block?.Name.Contains(text, StringComparison.OrdinalIgnoreCase) == true
                        || ins.Attributes.Any(a => (a.Value ?? "").Contains(text, StringComparison.OrdinalIgnoreCase));
                if (!hat) return false;
            }
            if (fenster is not null)
            {
                if (Box(e) is not { } b) return false;
                if (b[2] < fx0 || b[0] > fx1 || b[3] < fy0 || b[1] > fy1) return false;
            }
            return true;
        }

        var treffer = alle.Where(Trifft).ToList();
        var liste = new JsonArray(treffer.Skip((seite - 1) * proSeite).Take(proSeite).Select(e => (JsonNode?)Beschreiben(e)).ToArray());
        var jeArt = new JsonObject(treffer.GroupBy(Art).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => KeyValuePair.Create(g.Key, (JsonNode?)g.Count())));
        return new JsonObject
        {
            ["zeichnung"] = z.Name, ["layout"] = z.Layout(layout)?.Name ?? "Model", ["einheit"] = Einheit(z),
            ["treffer"] = treffer.Count, ["jeArt"] = jeArt, ["seite"] = seite, ["seiten"] = Math.Max(1, (treffer.Count + proSeite - 1) / proSeite), ["objekte"] = liste,
        };
    }

    // ---------- Treffer (Klick in der Zeichenfläche) ----------

    static double AbstandStrecke(double px, double py, double ax, double ay, double bx, double by)
    {
        double dx = bx - ax, dy = by - ay, l2 = dx * dx + dy * dy;
        double t = l2 == 0 ? 0 : Math.Clamp(((px - ax) * dx + (py - ay) * dy) / l2, 0, 1);
        return Math.Sqrt(Math.Pow(px - ax - t * dx, 2) + Math.Pow(py - ay - t * dy, 2));
    }

    /// <summary>Abstand des Punkts zur Geometrie des Objekts; null = nur über die Box bestimmbar.</summary>
    static double? Abstand(Entity e, double x, double y)
    {
        switch (e)
        {
            case Line l: return AbstandStrecke(x, y, l.StartPoint.X, l.StartPoint.Y, l.EndPoint.X, l.EndPoint.Y);
            case Arc a:
            {
                double w = Math.Atan2(y - a.Center.Y, x - a.Center.X);
                double rel = ((w - a.StartAngle) % (2 * Math.PI) + 2 * Math.PI) % (2 * Math.PI), sweep = ((a.EndAngle - a.StartAngle) % (2 * Math.PI) + 2 * Math.PI) % (2 * Math.PI);
                if (rel <= sweep) return Math.Abs(Math.Sqrt(Math.Pow(x - a.Center.X, 2) + Math.Pow(y - a.Center.Y, 2)) - a.Radius);
                double d1 = Math.Sqrt(Math.Pow(x - a.Center.X - a.Radius * Math.Cos(a.StartAngle), 2) + Math.Pow(y - a.Center.Y - a.Radius * Math.Sin(a.StartAngle), 2));
                double d2 = Math.Sqrt(Math.Pow(x - a.Center.X - a.Radius * Math.Cos(a.EndAngle), 2) + Math.Pow(y - a.Center.Y - a.Radius * Math.Sin(a.EndAngle), 2));
                return Math.Min(d1, d2);
            }
            case Circle c: return Math.Abs(Math.Sqrt(Math.Pow(x - c.Center.X, 2) + Math.Pow(y - c.Center.Y, 2)) - c.Radius);
            case TextEntity t: return TextAbstand(x, y, t.InsertPoint.X, t.InsertPoint.Y, t.Height, CadText.Einfach(t.Value).Length);
            case MText m: return TextAbstand(x, y, m.InsertPoint.X, m.InsertPoint.Y, m.Height, CadText.MText(m.Value).Select(z => z.Length).DefaultIfEmpty(0).Max());
        }
        if (Ecken(e, out bool zu) is { Count: >= 2 } v)
        {
            double min = double.MaxValue;
            int n = zu ? v.Count : v.Count - 1;
            for (int i = 0; i < n; i++) min = Math.Min(min, AbstandStrecke(x, y, v[i].X, v[i].Y, v[(i + 1) % v.Count].X, v[(i + 1) % v.Count].Y));
            return min;
        }
        return null;
    }

    /// <summary>Grobe Trefferfläche eines Texts: Kreis um den Einfügepunkt mit der halben geschätzten Textbreite.</summary>
    static double TextAbstand(double x, double y, double tx, double ty, double hoehe, int zeichen) =>
        Math.Max(0, Math.Sqrt(Math.Pow(x - tx, 2) + Math.Pow(y - ty, 2)) - Math.Max(hoehe, 0.35 * hoehe * zeichen));

    /// <summary>Das Objekt am Punkt (innerhalb des Fangradius r), das der Geometrie nach am nächsten liegt; Flächen (Schraffur, Block) zuletzt.</summary>
    public static JsonObject? Treffer(Zeichnungsdatei z, string? layout, double x, double y, double r)
    {
        Entity? bester = null;
        double besterAbstand = double.MaxValue, besteFlaeche = double.MaxValue;
        foreach (var e in z.Objekte(z.Layout(layout)))
        {
            if (e is Viewport || e.IsInvisible || (e.Layer is { } l && !Zeichnungsdatei.Sichtbar(l))) continue;
            double abstand;
            double flaeche = 0;
            if (Abstand(e, x, y) is double d) abstand = d;
            else
            {
                if (Box(e) is not { } b || x < b[0] - r || x > b[2] + r || y < b[1] - r || y > b[3] + r) continue;
                abstand = r * 0.99;   // nur über die Box getroffen: hinter allem, was genau getroffen ist
                flaeche = (b[2] - b[0]) * (b[3] - b[1]);
            }
            if (abstand > r) continue;
            if (abstand < besterAbstand - 1e-12 || (Math.Abs(abstand - besterAbstand) <= 1e-12 && flaeche < besteFlaeche))
                (bester, besterAbstand, besteFlaeche) = (e, abstand, flaeche);
        }
        return bester is null ? null : Beschreiben(bester);
    }

    /// <summary>Auswahl mit einem Fenster: kreuzen = false nur ganz darin liegende Objekte, true auch berührte (nach der Box).
    /// Antwort: kennung und box je Objekt (höchstens <paramref name="max"/>), dazu die Anzahl.</summary>
    public static JsonObject Auswahl(Zeichnungsdatei z, string? layout, double[] fenster, bool kreuzen, int max = 5000)
    {
        if (fenster.Length != 4) throw new HlksFehler("fenster muss [x0, y0, x1, y1] sein.");
        double x0 = Math.Min(fenster[0], fenster[2]), x1 = Math.Max(fenster[0], fenster[2]), y0 = Math.Min(fenster[1], fenster[3]), y1 = Math.Max(fenster[1], fenster[3]);
        var liste = new JsonArray();
        int anzahl = 0;
        foreach (var e in z.Objekte(z.Layout(layout)))
        {
            if (e is Viewport || e.IsInvisible || (e.Layer is { } l && (!Zeichnungsdatei.Sichtbar(l) || l.Flags.HasFlag(LayerFlags.Locked)))) continue;
            if (Box(e) is not { } b) continue;
            bool drin = kreuzen ? b[2] >= x0 && b[0] <= x1 && b[3] >= y0 && b[1] <= y1 : b[0] >= x0 && b[2] <= x1 && b[1] >= y0 && b[3] <= y1;
            if (!drin) continue;
            anzahl++;
            if (liste.Count < max) liste.Add(new JsonObject { ["kennung"] = Zeichnungsdatei.Kennung(e), ["art"] = Art(e), ["box"] = new JsonArray(R(b[0]), R(b[1]), R(b[2]), R(b[3])) });
        }
        return new JsonObject { ["anzahl"] = anzahl, ["objekte"] = liste };
    }

    // ---------- Messen ----------

    public static JsonObject Messen(Zeichnungsdatei z, double[][]? punkte, string? objekt)
    {
        var o = new JsonObject { ["zeichnung"] = z.Name, ["einheit"] = Einheit(z) };
        double m = z.MmJeEinheit / 1000;
        void Laenge(string name, double v) { o[name] = R(v); if (z.EinheitBekannt) o[name + "M"] = R(v * m, 4); }
        void Flaeche(double v) { o["flaeche"] = R(v); if (z.EinheitBekannt) o["flaecheM2"] = R(v * m * m, 4); }

        if (!string.IsNullOrWhiteSpace(objekt))
        {
            var e = z.Objekt(objekt);
            var (l, f) = Masse(e);
            o["objekt"] = Zeichnungsdatei.Kennung(e); o["art"] = Art(e); o["layer"] = e.Layer?.Name ?? "0";
            if (l is null && f is null) throw new HlksFehler($"Ein Objekt der Art «{Art(e)}» hat weder Länge noch Fläche.", "Linien, Polylinien, Bögen, Kreise, Ellipsen, Splines und Schraffuren lassen sich messen; sonst punkte angeben.");
            if (l is double ll) Laenge("laenge", ll);
            if (f is double ff) Flaeche(ff);
            return o;
        }
        if (punkte is null || punkte.Length < 2 || punkte.Any(p => p.Length < 2))
            throw new HlksFehler("Zum Messen fehlen punkte oder objekt.", "punkte [[x,y],[x,y],…] (mindestens zwei) oder objekt (Kennung aus zeichnung_objekte) angeben.");
        var pts = punkte.Select(p => (X: p[0], Y: p[1])).ToList();
        Laenge("laenge", Zug(pts, false));
        if (pts.Count == 2)
        {
            Laenge("dx", pts[1].X - pts[0].X); Laenge("dy", pts[1].Y - pts[0].Y);
            o["winkelGrad"] = R(Math.Atan2(pts[1].Y - pts[0].Y, pts[1].X - pts[0].X) * 180 / Math.PI, 3);
        }
        else
        {
            if (pts.Count == 3)
            {
                double w = Math.Atan2(pts[2].Y - pts[1].Y, pts[2].X - pts[1].X) - Math.Atan2(pts[0].Y - pts[1].Y, pts[0].X - pts[1].X);
                w = Math.Abs(w) * 180 / Math.PI;
                o["winkelGrad"] = R(w > 180 ? 360 - w : w, 3);
            }
            Laenge("umfang", Zug(pts, true));
            Flaeche(Math.Abs(ZeichnungGeometrie.Flaeche(pts)));
        }
        return o;
    }
}
