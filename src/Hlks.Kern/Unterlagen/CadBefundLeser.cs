// Leser «cad» für DWG und DXF (Modul 2, Auftrag B2): Massbilder der Hersteller als Befunde, mit ACadSharp (wie Modul 1b).
// Je Layout (Model und Papierbereiche) und Layer: linie, polylinie, bogen, kreis, text; mass aus Bemassungsobjekten
// (Messwert exakt aus der Datei, Definitionspunkte, Textüberschreibung); block mit Name, Einfügepunkt und Attributen.
// Den Modellbereich liest <see cref="CadLeser"/> (Modul 1b): Blöcke sind aufgelöst, Bemassungen als ihr Block aus Linien
// und Text – die Bemassungsobjekte selbst (mass) kommen aus den Entitäten der Datei. Papierbereiche: nur die direkt
// enthaltenen Entitäten (keine Blockauflösung, keine Ansichtsfenster; Annahme).
// Koordinaten: Zeichnungseinheiten der Datei; die Einheit steht im Hinweis und bei mass in Einheit.

using System.Text.Json.Nodes;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Objects;
using ACadSharp.Types.Units;
using CSMath;

namespace Hlks.Kern;

public sealed class CadBefundLeser : IUnterlagenLeser
{
    public string Name => "cad";
    public int Version => 1;
    public IReadOnlyCollection<string> Arten { get; } = ["dwg", "dxf"];

    /// <summary>Obergrenze der Befunde je Layout (Datei-Grösse und Lesezeit).</summary>
    public const int MaxJeLayout = 60_000;

    public UnterlagenBefunde Lesen(string pfad, string datei, string pruefsumme, string art)
    {
        CadDocument doc;
        try { doc = pfad.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase) ? DxfReader.Read(pfad) : DwgReader.Read(pfad); }
        catch (Exception ex) when (ex is not HlksFehler)
        {
            var kind = pfad.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase) ? "DXF" : "DWG";
            throw new HlksFehler($"Die {kind}-Datei «{datei}» lässt sich nicht lesen: {ex.Message}",
                kind == "DWG" ? "DWG-Fassungen AC1014 bis AC1032 werden gelesen; ältere Dateien als DWG 2000 oder neuer bzw. als DXF speichern."
                              : "Die Datei als DXF (ASCII oder binär, R12 oder neuer) neu speichern.");
        }
        var s = new BefundSammler(this, datei, pruefsumme, art);
        var z = CadLeser.Lesen(doc);
        var (mmJe, bekannt) = CadLeser.MmJe(doc.Header.InsUnits);
        string einheit = EinheitKurz(doc.Header.InsUnits);
        s.Hinweis($"{art.ToUpperInvariant()} {z.Version}: Zeichnungseinheit {(bekannt ? doc.Header.InsUnits.ToString() + $" ({mmJe:0.######} mm je Einheit)" : "unbekannt (INSUNITS fehlt) – Annahme Millimeter")}. Alle Koordinaten und Masse in Zeichnungseinheiten.");

        var layouts = doc.Layouts.OrderBy(l => l.IsPaperSpace ? 1 : 0).ThenBy(l => l.TabOrder).ToList();
        s.Ergebnis.Seiten = Math.Max(1, layouts.Count);

        // Modellbereich
        int ab = s.Anzahl;
        foreach (var e in doc.ModelSpace.Entities) Bemassung(s, e, "Model", einheit, doc);
        foreach (var e in doc.ModelSpace.Entities) if (e is Insert ins) BlockBefund(s, ins, "Model");
        foreach (var p in z.Primitive)
        {
            if (s.Anzahl - ab >= MaxJeLayout) { s.Hinweis($"Layout Model: mehr als {MaxJeLayout} Befunde, der Rest ist nicht gemeldet (Grenze je Layout)."); break; }
            Primitiv(s, p, z.Layer[p.Layer], "Model");
        }
        if (z.Gekappt > 0) s.Hinweis($"Die Zeichnung ist sehr gross: {z.Gekappt} Elemente über der Obergrenze des Lesers fehlen.");
        if (z.NichtDargestellt.Count > 0) s.Hinweis("Nicht gelesen (Typ: Anzahl): " + string.Join(", ", z.NichtDargestellt.Select(x => $"{x.Key} {x.Value}")) + ".");
        if (z.LayerAus.Count > 0) s.Hinweis($"In der Datei ausgeschaltet oder gefroren (Entitäten dort fehlen): {string.Join(", ", z.LayerAus.Take(20))}{(z.LayerAus.Count > 20 ? " …" : "")}.");

        // Papierbereiche: direkte Entitäten
        foreach (var l in layouts.Where(l => l.IsPaperSpace))
        {
            var bl = l.AssociatedBlock;
            if (bl is null) continue;
            int n0 = s.Anzahl;
            foreach (var e in bl.Entities)
            {
                if (s.Anzahl - n0 >= MaxJeLayout) { s.Hinweis($"Layout {l.Name}: mehr als {MaxJeLayout} Befunde, der Rest ist nicht gemeldet."); break; }
                Direkt(s, e, l.Name, einheit, doc);
            }
        }
        if (layouts.Any(l => l.IsPaperSpace))
            s.Hinweis("Papierbereiche (Layouts): nur direkt enthaltene Entitäten, Blöcke nicht aufgelöst, Ansichtsfenster nicht abgebildet.");
        return s.Ergebnis;
    }

    static string EinheitKurz(UnitsType u) => u switch
    {
        UnitsType.Millimeters => "mm", UnitsType.Centimeters => "cm", UnitsType.Decimeters => "dm", UnitsType.Meters => "m",
        UnitsType.Inches => "in", UnitsType.Feet => "ft", UnitsType.Kilometers => "km", _ => "",
    };

    static double R4(double x) => Math.Round(x, 4);
    static double[] P2(double x, double y) => [R4(x), R4(y)];
    static double[] P3(XYZ p) => [R4(p.X), R4(p.Y), R4(p.Z)];
    static double[] P2(XYZ p) => [R4(p.X), R4(p.Y)];

    // ---------- Primitive aus CadLeser ----------

    static void Primitiv(BefundSammler s, CadPrimitiv p, string layer, string layout)
    {
        var w = p.W;
        switch (p.Art)
        {
            case 'l':
                s.Neu("linie", null, layout: layout, layer: layer, lage: [P2(w[0], w[1]), P2(w[2], w[3])], box: Box(w[0], w[1], w[2], w[3]));
                break;
            case 'p' or 'g':
            {
                var pts = new List<double[]>();
                for (int i = 0; i + 1 < w.Length; i += 2) pts.Add(P2(w[i], w[i + 1]));
                double x0 = pts.Min(q => q[0]), y0 = pts.Min(q => q[1]), x1 = pts.Max(q => q[0]), y1 = pts.Max(q => q[1]);
                s.Neu("polylinie", null, layout: layout, layer: layer, lage: pts, box: Box(x0, y0, x1, y1), zusatz: p.Art == 'g' ? new JsonObject { ["geschlossen"] = true } : null);
                break;
            }
            case 'b':
                s.Neu("bogen", new JsonObject { ["mitte"] = Bau.Feld([R4(w[0]), R4(w[1])]), ["radius"] = R4(w[2]), ["von"] = R4(w[3]), ["bis"] = R4(w[4]) },
                    layout: layout, layer: layer, lage: [P2(w[0], w[1])], box: Box(w[0] - w[2], w[1] - w[2], w[0] + w[2], w[1] + w[2]));
                break;
            case 'k':
                s.Neu("kreis", new JsonObject { ["mitte"] = Bau.Feld([R4(w[0]), R4(w[1])]), ["radius"] = R4(w[2]), ["durchmesser"] = R4(2 * w[2]) },
                    layout: layout, layer: layer, lage: [P2(w[0], w[1])], box: Box(w[0] - w[2], w[1] - w[2], w[0] + w[2], w[1] + w[2]));
                break;
            case 't':
                s.Neu("text", p.Text, layout: layout, layer: layer, lage: [P2(w[0], w[1])], box: Box(w[0], w[1], w[0], w[1]),
                    zusatz: new JsonObject { ["hoehe"] = R4(w[2]), ["winkel"] = R4(w[3]) });
                break;
        }
    }

    static double[] Box(double x0, double y0, double x1, double y1) => [R4(Math.Min(x0, x1)), R4(Math.Min(y0, y1)), R4(Math.Max(x0, x1)), R4(Math.Max(y0, y1))];

    // ---------- Blöcke ----------

    static void BlockBefund(BefundSammler s, Insert ins, string layout)
    {
        var name = ins.Block?.Name;
        if (string.IsNullOrEmpty(name) || name.StartsWith('*')) return;   // anonyme Blöcke (Bemassungen, Schraffuren)
        var z = new JsonObject
        {
            ["drehung"] = R4(ins.Rotation * 180 / Math.PI), ["skalierung"] = Bau.Feld([R4(ins.XScale), R4(ins.YScale)]),
            ["entitaeten"] = ins.Block!.Entities.Count,
        };
        if (ins.HasAttributes)
        {
            var a = new JsonObject();
            foreach (var at in ins.Attributes) if (!string.IsNullOrEmpty(at.Tag)) a[at.Tag] = at.Value;
            if (a.Count > 0) z["attribute"] = a;
        }
        if (ins.RowCount > 1 || ins.ColumnCount > 1) z["reihe"] = Bau.Feld([ins.ColumnCount, ins.RowCount]);
        s.Neu("block", name, layout: layout, layer: ins.Layer?.Name, lage: [P2(ins.InsertPoint)], box: Box(ins.InsertPoint.X, ins.InsertPoint.Y, ins.InsertPoint.X, ins.InsertPoint.Y), zusatz: z);
    }

    // ---------- Bemassung ----------

    static void Bemassung(BefundSammler s, Entity e, string layout, string einheit, CadDocument doc)
    {
        if (e is not Dimension d) return;
        string art; var punkte = new List<double[]>();
        double messwert = d.Measurement;
        bool berechnet = false;
        switch (d)
        {
            case DimensionLinear dl:
            {
                art = "linear";
                punkte = [P2(dl.FirstPoint), P2(dl.SecondPoint), P2(dl.DefinitionPoint)];
                if (!(messwert > 0))
                {   // Messwert fehlt in der Datei: entlang der Masslinie projizieren
                    double ux = Math.Cos(dl.Rotation), uy = Math.Sin(dl.Rotation);
                    messwert = Math.Abs((dl.SecondPoint.X - dl.FirstPoint.X) * ux + (dl.SecondPoint.Y - dl.FirstPoint.Y) * uy); berechnet = true;
                }
                break;
            }
            case DimensionAligned da:
                art = "ausgerichtet";
                punkte = [P2(da.FirstPoint), P2(da.SecondPoint), P2(da.DefinitionPoint)];
                if (!(messwert > 0)) { messwert = Math.Sqrt(Math.Pow(da.SecondPoint.X - da.FirstPoint.X, 2) + Math.Pow(da.SecondPoint.Y - da.FirstPoint.Y, 2)); berechnet = true; }
                break;
            case DimensionRadius dr:
                art = "radius"; punkte = [P2(dr.DefinitionPoint), P2(dr.AngleVertex)];
                break;
            case DimensionDiameter dd:
                art = "durchmesser"; punkte = [P2(dd.DefinitionPoint), P2(dd.AngleVertex)];
                break;
            case DimensionAngular2Line a2:
                art = "winkel"; punkte = [P2(a2.FirstPoint), P2(a2.SecondPoint), P2(a2.AngleVertex), P2(a2.DefinitionPoint)];
                break;
            case DimensionAngular3Pt a3:
                art = "winkel"; punkte = [P2(a3.FirstPoint), P2(a3.SecondPoint), P2(a3.AngleVertex)];
                break;
            case DimensionOrdinate o:
                art = "koordinate"; punkte = [P2(o.FeatureLocation), P2(o.LeaderEndpoint)];
                break;
            default:
                art = d.GetType().Name; punkte = [P2(d.DefinitionPoint)];
                break;
        }
        var z = new JsonObject { ["bemassung"] = art, ["textMitte"] = Bau.Feld(P2(d.TextMiddlePoint)) };
        if (!string.IsNullOrEmpty(d.Text)) z["text"] = d.Text;   // «<>» = gemessener Wert; sonst überschriebener Text
        if (art is "linear") z["drehung"] = R4(((DimensionLinear)d).Rotation * 180 / Math.PI);
        if (art == "winkel") z["hinweis"] = "Winkelmass: Einheit des Messwerts (Grad oder Bogenmass) nicht geprüft";
        if (berechnet) z["messwertBerechnet"] = true;
        // Text, der angezeigt wird: Überschreibung, sonst der Messwert
        string? anzeige = string.IsNullOrEmpty(d.Text) || d.Text.Trim() == "<>" ? null : d.Text;
        if (anzeige is not null) z["angezeigt"] = anzeige;
        double x0 = punkte.Min(p => p[0]), y0 = punkte.Min(p => p[1]), x1 = punkte.Max(p => p[0]), y1 = punkte.Max(p => p[1]);
        s.Neu("mass", R4(messwert), art is "winkel" ? null : (einheit.Length > 0 ? einheit : null), layout: layout, layer: d.Layer?.Name, lage: punkte,
            box: Box(x0, y0, x1, y1), sicherheit: berechnet ? 0.9 : 1, zusatz: z);
    }

    // ---------- Papierbereiche: direkte Entitäten ----------

    static void Direkt(BefundSammler s, Entity e, string layout, string einheit, CadDocument doc)
    {
        string? layer = e.Layer?.Name;
        switch (e)
        {
            case Line l:
                s.Neu("linie", null, layout: layout, layer: layer, lage: [P2(l.StartPoint), P2(l.EndPoint)], box: Box(l.StartPoint.X, l.StartPoint.Y, l.EndPoint.X, l.EndPoint.Y));
                break;
            case Arc a:
                s.Neu("bogen", new JsonObject { ["mitte"] = Bau.Feld(P2(a.Center)), ["radius"] = R4(a.Radius), ["von"] = R4(a.StartAngle * 180 / Math.PI), ["bis"] = R4(a.EndAngle * 180 / Math.PI) },
                    layout: layout, layer: layer, lage: [P2(a.Center)], box: Box(a.Center.X - a.Radius, a.Center.Y - a.Radius, a.Center.X + a.Radius, a.Center.Y + a.Radius));
                break;
            case Circle c:
                s.Neu("kreis", new JsonObject { ["mitte"] = Bau.Feld(P2(c.Center)), ["radius"] = R4(c.Radius), ["durchmesser"] = R4(2 * c.Radius) },
                    layout: layout, layer: layer, lage: [P2(c.Center)], box: Box(c.Center.X - c.Radius, c.Center.Y - c.Radius, c.Center.X + c.Radius, c.Center.Y + c.Radius));
                break;
            case LwPolyline lw:
            {
                var pts = lw.Vertices.Select(v => P2(v.Location.X, v.Location.Y)).ToList();
                if (pts.Count < 2) break;
                bool bogen = lw.Vertices.Any(v => v.Bulge != 0);
                s.Neu("polylinie", null, layout: layout, layer: layer, lage: pts, box: Box(pts.Min(q => q[0]), pts.Min(q => q[1]), pts.Max(q => q[0]), pts.Max(q => q[1])),
                    zusatz: lw.IsClosed || bogen ? new JsonObject { ["geschlossen"] = lw.IsClosed ? true : null, ["mitBogen"] = bogen ? true : null } : null);
                break;
            }
            case TextEntity t:
                s.Neu("text", t.Value, layout: layout, layer: layer, lage: [P2(t.InsertPoint)], box: Box(t.InsertPoint.X, t.InsertPoint.Y, t.InsertPoint.X, t.InsertPoint.Y),
                    zusatz: new JsonObject { ["hoehe"] = R4(t.Height), ["winkel"] = R4(t.Rotation * 180 / Math.PI) });
                break;
            case MText mt:
                s.Neu("text", CadText.MText(mt.Value) is { } zeilen ? string.Join("\n", zeilen) : mt.Value, layout: layout, layer: layer, lage: [P2(mt.InsertPoint)],
                    box: Box(mt.InsertPoint.X, mt.InsertPoint.Y, mt.InsertPoint.X, mt.InsertPoint.Y), zusatz: new JsonObject { ["hoehe"] = R4(mt.Height), ["winkel"] = R4(mt.Rotation * 180 / Math.PI) });
                break;
            case Insert ins:
                BlockBefund(s, ins, layout);
                break;
            case Dimension:
                Bemassung(s, e, layout, einheit, doc);
                break;
        }
    }
}
