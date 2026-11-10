// 2D-CAD «Zeichnung», Stufe 2 Teil 3 / Stufe 3: Blöcke, Strecken, Bruch, Griffe, weitere Bemassungen, Führung, Bemassungsstil.
//   Aendern   strecken · bruch · griff · block (Block aus Objekten erstellen)
//   Bauen     block (Blockreferenz einfügen) · radiusmass · durchmessermass · fuehrung
//   dazu      Bloecke (Liste), Bemassungsstil (Massstab, Texthöhe, Pfeilgrösse des aktuellen Stils)
// Aufgerufen aus ZeichnungGeoBefehle (dieselbe Tür wie alle Befehle).

using System.Text.Json.Nodes;
using ACadSharp.Blocks;
using ACadSharp.Entities;
using ACadSharp.Tables;
using CSMath;
using static Hlks.Kern.ZeichnungBearbeiten;

namespace Hlks.Kern;

public static class ZeichnungWeitere
{
    public const string BloeckeText = "Blöcke der Zeichnung, die sich einfügen lassen: Name, Anzahl Objekte, Anzahl Referenzen, Attribute.";
    public const string BemassungsstilText = "Stellt den aktuellen Bemassungsstil ein: massstab (Gesamtmassstab wie DIMSCALE – z. B. 50 für einen Plan 1:50 in mm, 0.05 in m), " +
        "texthoehe und pfeil (Papier-mm vor dem Massstab). Alle Bemassungen dieses Stils werden neu gezeichnet. Ohne Angaben: nur lesen.";

    static double Norm(double w) => ((w % (2 * Math.PI)) + 2 * Math.PI) % (2 * Math.PI);
    static BlockRecord BlockVon(Entity e) => e.Owner as BlockRecord ?? throw new HlksFehler($"Das Objekt {Zeichnungsdatei.Kennung(e)} gehört zu einem anderen Objekt und lässt sich so nicht ändern.");
    static T Wie<T>(Entity v, T neu) where T : Entity { neu.Layer = v.Layer; neu.Color = v.Color; neu.LineType = v.LineType; neu.LineWeight = v.LineWeight; neu.LineTypeScale = v.LineTypeScale; return neu; }
    static void Nachfuehren(Entity e) { if (e is Dimension d) MassBild(d); }

    /// <summary>Bild einer Bemassung (ihr Block) neu erzeugen. ACadSharp rechnet den Gesamtmassstab des Stils (DIMSCALE) dabei nicht ein
    /// (Befund 10.11.2026: Text 0.18 statt 0.18 × 50); deshalb werden die Grössen des Stils für das Erzeugen mit dem Massstab multipliziert
    /// und danach zurückgestellt – im Stil bleibt DIMSCALE stehen, wie AutoCAD es erwartet.</summary>
    static void MassBildStandard(Dimension d)
    {
        var st = d.Style;
        double m = st is { ScaleFactor: > 0 } ? st.ScaleFactor : 1;
        try
        {
            if (st is null || m == 1) { d.UpdateBlock(); return; }
            var alt = (st.TextHeight, st.ArrowSize, st.ExtensionLineOffset, st.ExtensionLineExtension, st.DimensionLineGap, st.DimensionLineExtension, st.TickSize, st.CenterMarkSize);
            try
            {
                st.ScaleFactor = 1; st.TextHeight *= m; st.ArrowSize *= m; st.ExtensionLineOffset *= m; st.ExtensionLineExtension *= m; st.DimensionLineGap *= m; st.DimensionLineExtension *= m; st.TickSize *= m; st.CenterMarkSize *= m;
                d.UpdateBlock();
            }
            finally { st.ScaleFactor = m; (st.TextHeight, st.ArrowSize, st.ExtensionLineOffset, st.ExtensionLineExtension, st.DimensionLineGap, st.DimensionLineExtension, st.TickSize, st.CenterMarkSize) = alt; }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { }
    }

    public static (string Name, Action Vor, Action Zurueck)? Aendern(Zeichnungsdatei z, List<Entity> liste, string aktion, JsonObject par) => aktion switch
    {
        "strecken" => Strecken(liste, par),
        "bruch" => Bruch(liste, par),
        "griff" => Griff(liste, par),
        "block" => BlockErstellen(z, liste, par),
        "attribut" => Attribute(liste, par),
        _ => null,
    };

    /// <summary>Bild einer Bemassung neu erzeugen (mit dem Gesamtmassstab des Stils); für das Winkelmass, dessen Bild die Bibliothek leer lässt,
    /// zeichnet KICAD Bogen, Pfeile und Gradzahl selbst.</summary>
    public static void MassBild(Dimension d)
    {
        MassBildStandard(d);
        if (d is DimensionAngular3Pt w && d.Block is { } block && !block.Entities.Any(e => e is Arc or TextEntity or MText)) WinkelBild(w, block);
    }

    static void WinkelBild(DimensionAngular3Pt d, BlockRecord block)
    {
        try
        {
            var st = d.Style;
            double m = st is { ScaleFactor: > 0 } ? st.ScaleFactor : 1, h = (st?.TextHeight is > 0 and var th ? th : 2.5) * m, pfeil = (st?.ArrowSize is > 0 and var ps ? ps : 2.5) * m;
            double sx = d.AngleVertex.X, sy = d.AngleVertex.Y;
            double r = Math.Sqrt(Math.Pow(d.DefinitionPoint.X - sx, 2) + Math.Pow(d.DefinitionPoint.Y - sy, 2));
            if (!(r > 0)) return;
            double wa = Math.Atan2(d.FirstPoint.Y - sy, d.FirstPoint.X - sx), wb = Math.Atan2(d.SecondPoint.Y - sy, d.SecondPoint.X - sx), wo = Math.Atan2(d.DefinitionPoint.Y - sy, d.DefinitionPoint.X - sx);
            // der Bogen liegt in dem Winkelfeld, in dem der Punkt der Masslinie liegt
            bool innen = Norm(wo - wa) <= Norm(wb - wa);
            double von = innen ? wa : wb, sweep = innen ? Norm(wb - wa) : Norm(wa - wb);
            foreach (var alt in block.Entities.ToList()) block.Entities.Remove(alt);
            block.Entities.Add(new Arc { Center = new XYZ(sx, sy, 0), Radius = r, StartAngle = Norm(von), EndAngle = Norm(von + sweep), Color = ACadSharp.Color.ByBlock });
            void Spitze(double winkel, double richtung)   // Pfeil an der Stelle «winkel», Schaft entlang des Bogens (richtung +1 = Gegenuhrzeigersinn)
            {
                double px = sx + r * Math.Cos(winkel), py = sy + r * Math.Sin(winkel);
                double tx = -Math.Sin(winkel) * richtung, ty = Math.Cos(winkel) * richtung, nx = Math.Cos(winkel), ny = Math.Sin(winkel);
                var e1 = new XYZ(px + tx * pfeil + nx * pfeil / 6, py + ty * pfeil + ny * pfeil / 6, 0); var e2 = new XYZ(px + tx * pfeil - nx * pfeil / 6, py + ty * pfeil - ny * pfeil / 6, 0);
                block.Entities.Add(new Solid { FirstCorner = new XYZ(px, py, 0), SecondCorner = e1, ThirdCorner = e2, FourthCorner = e2, Color = ACadSharp.Color.ByBlock });
            }
            if (r * sweep > 2.5 * pfeil) { Spitze(von, 1); Spitze(von + sweep, -1); }
            double mitte = von + sweep / 2, tr = r + h * 1.1;
            var ort = new XYZ(sx + tr * Math.Cos(mitte), sy + tr * Math.Sin(mitte), 0);
            var text = (sweep * 180 / Math.PI).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "°";
            var t = new TextEntity { Value = text, Height = h, InsertPoint = ort, AlignmentPoint = ort, HorizontalAlignment = TextHorizontalAlignment.Center, VerticalAlignment = TextVerticalAlignmentType.Middle, Color = ACadSharp.Color.ByBlock };
            if (st?.Style is { } schrift) t.Style = schrift;
            block.Entities.Add(t);
            d.TextMiddlePoint = ort;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { }
    }

    // ---------- Strecken ----------

    static (string, Action, Action) Strecken(List<Entity> liste, JsonObject par)
    {
        if (par["fenster"] is not JsonArray { Count: 4 } f) throw new HlksFehler("«fenster» fehlt.", "fenster: [x0, y0, x1, y1] – was darin liegt, wird verschoben; Linien und Polylinien, die hineinragen, werden gestreckt.");
        double[] w = f.Select(n => Json.Zahl(n) ?? throw new HlksFehler("fenster muss aus Zahlen bestehen.")).ToArray();
        double x0 = Math.Min(w[0], w[2]), x1 = Math.Max(w[0], w[2]), y0 = Math.Min(w[1], w[3]), y1 = Math.Max(w[1], w[3]);
        double dx = Zahl(par, "dx"), dy = Zahl(par, "dy");
        bool Drin(double x, double y) => x >= x0 && x <= x1 && y >= y0 && y <= y1;
        var schritte = new List<(Action Vor, Action Zurueck)>();
        foreach (var e in liste)
        {
            switch (e)
            {
                case Line l:
                {
                    var (a, b) = (l.StartPoint, l.EndPoint);
                    bool da = Drin(a.X, a.Y), db = Drin(b.X, b.Y);
                    if (!da && !db) break;
                    schritte.Add((() => { if (da) l.StartPoint = new XYZ(a.X + dx, a.Y + dy, a.Z); if (db) l.EndPoint = new XYZ(b.X + dx, b.Y + dy, b.Z); }, () => { l.StartPoint = a; l.EndPoint = b; }));
                    break;
                }
                case LwPolyline p:
                {
                    var alt = p.Vertices.Select(v => v.Location).ToList();
                    var welche = alt.Select(q => Drin(q.X, q.Y)).ToList();
                    if (!welche.Any(q => q)) break;
                    schritte.Add((() => { for (int i = 0; i < alt.Count; i++) if (welche[i]) p.Vertices[i].Location = new XY(alt[i].X + dx, alt[i].Y + dy); },
                                  () => { for (int i = 0; i < alt.Count; i++) p.Vertices[i].Location = alt[i]; }));
                    break;
                }
                default:
                {
                    // übrige Objekte: als Ganzes verschieben, wenn ihr Bezugspunkt im Fenster liegt
                    XYZ? bezug = e switch { Arc a => a.Center, Circle c => c.Center, TextEntity t => t.InsertPoint, MText m => m.InsertPoint, Insert i => i.InsertPoint, Point pt => pt.Location, Ellipse el => el.Center, _ => null };
                    if (bezug is null)
                    {
                        try { var b = e.GetBoundingBox(); if (Drin(b.Min.X, b.Min.Y) && Drin(b.Max.X, b.Max.Y)) bezug = b.Min; } catch (Exception ex) when (ex is not OutOfMemoryException) { }
                    }
                    if (bezug is not { } q || !Drin(q.X, q.Y)) break;
                    schritte.Add((() => { e.ApplyTransform(Transform.CreateTranslation(new XYZ(dx, dy, 0))); Nachfuehren(e); }, () => { e.ApplyTransform(Transform.CreateTranslation(new XYZ(-dx, -dy, 0))); Nachfuehren(e); }));
                    break;
                }
            }
        }
        if (schritte.Count == 0) throw new HlksFehler("Im Fenster liegt kein Punkt der gewählten Objekte: nichts zu strecken.", "Das Fenster über die Enden ziehen, die verschoben werden sollen.");
        return ($"{schritte.Count} strecken", () => schritte.ForEach(s => s.Vor()), () => schritte.ForEach(s => s.Zurueck()));
    }

    // ---------- Bruch ----------

    static (string, Action, Action) Bruch(List<Entity> liste, JsonObject par)
    {
        if (liste.Count != 1) throw new HlksFehler("Bruch nimmt ein Objekt je Aufruf.", "kennungen: das Objekt; punkt: Bruchstelle; punkt2: zweite Stelle (das Stück dazwischen fällt weg).");
        var e = liste[0];
        var (px, py) = Punkt(par["punkt"], "punkt");
        (double X, double Y)? zweiter = par["punkt2"] is null ? null : Punkt(par["punkt2"], "punkt2");
        var neu = new List<Entity>();
        switch (e)
        {
            case Line l:
            {
                double ax = l.StartPoint.X, ay = l.StartPoint.Y, dx = l.EndPoint.X - ax, dy = l.EndPoint.Y - ay, l2 = dx * dx + dy * dy;
                double T(double x, double y) => Math.Clamp(((x - ax) * dx + (y - ay) * dy) / l2, 0, 1);
                double t1 = T(px, py), t2 = zweiter is { } q ? T(q.X, q.Y) : t1;
                if (t1 > t2) (t1, t2) = (t2, t1);
                if (t1 > 1e-9) neu.Add(Wie(e, new Line { StartPoint = l.StartPoint, EndPoint = new XYZ(ax + t1 * dx, ay + t1 * dy, 0) }));
                if (t2 < 1 - 1e-9) neu.Add(Wie(e, new Line { StartPoint = new XYZ(ax + t2 * dx, ay + t2 * dy, 0), EndPoint = l.EndPoint }));
                if (neu.Count == 0 || (zweiter is null && neu.Count < 2)) throw new HlksFehler("Die Bruchstelle liegt am Ende der Linie.", "Einen Punkt auf der Linie wählen.");
                break;
            }
            case Arc a:
            {
                double sweep = Norm(a.EndAngle - a.StartAngle);
                double W(double x, double y) => Math.Min(Norm(Math.Atan2(y - a.Center.Y, x - a.Center.X) - a.StartAngle), sweep);
                double w1 = W(px, py), w2 = zweiter is { } q ? W(q.X, q.Y) : w1;
                if (w1 > w2) (w1, w2) = (w2, w1);
                Arc Teil(double von, double bis) => Wie(e, new Arc { Center = a.Center, Radius = a.Radius, StartAngle = Norm(a.StartAngle + von), EndAngle = Norm(a.StartAngle + bis) });
                if (w1 > 1e-9) neu.Add(Teil(0, w1));
                if (w2 < sweep - 1e-9) neu.Add(Teil(w2, sweep));
                if (neu.Count == 0 || (zweiter is null && neu.Count < 2)) throw new HlksFehler("Die Bruchstelle liegt am Ende des Bogens.", "Einen Punkt auf dem Bogen wählen.");
                break;
            }
            case LwPolyline pl:
                neu.AddRange(ZeichnungPolyPfad.Bruch(pl, px, py, zweiter).Select(teil => (Entity)Wie(e, teil)));
                break;
            case Circle c:
            {
                if (zweiter is not { } q) throw new HlksFehler("Ein Kreis braucht zwei Bruchstellen.", "punkt und punkt2 angeben: das Stück im Gegenuhrzeigersinn von punkt nach punkt2 fällt weg.");
                double w1 = Math.Atan2(py - c.Center.Y, px - c.Center.X), w2 = Math.Atan2(q.Y - c.Center.Y, q.X - c.Center.X);
                if (Norm(w2 - w1) < 1e-9) throw new HlksFehler("Die beiden Bruchstellen sind gleich.");
                neu.Add(Wie(e, new Arc { Center = c.Center, Radius = c.Radius, StartAngle = Norm(w2), EndAngle = Norm(w1) }));
                break;
            }
            default: throw new HlksFehler($"Bruch geht nicht für ein Objekt der Art «{ZeichnungAbfragen.Art(e)}».", "Linien, Bögen, Kreise und Polylinien lassen sich brechen.");
        }
        var block = BlockVon(e);
        return ("bruch", () => { block.Entities.Remove(e); foreach (var n in neu) block.Entities.Add(n); }, () => { foreach (var n in neu) block.Entities.Remove(n); block.Entities.Add(e); });
    }

    // ---------- Griffe ----------

    /// <summary>Griffe eines Objekts: [x, y] in der Reihenfolge der Nummern, die «griff» annimmt.</summary>
    public static List<(double X, double Y)> Griffe(Entity e)
    {
        var g = new List<(double, double)>();
        switch (e)
        {
            case Line l: g.Add((l.StartPoint.X, l.StartPoint.Y)); g.Add((l.EndPoint.X, l.EndPoint.Y)); g.Add(((l.StartPoint.X + l.EndPoint.X) / 2, (l.StartPoint.Y + l.EndPoint.Y) / 2)); break;
            case LwPolyline p: g.AddRange(p.Vertices.Take(400).Select(v => (v.Location.X, v.Location.Y))); break;
            case Arc a:
                g.Add((a.Center.X, a.Center.Y));
                g.Add((a.Center.X + a.Radius * Math.Cos(a.StartAngle), a.Center.Y + a.Radius * Math.Sin(a.StartAngle)));
                g.Add((a.Center.X + a.Radius * Math.Cos(a.EndAngle), a.Center.Y + a.Radius * Math.Sin(a.EndAngle)));
                break;
            case Circle c: g.Add((c.Center.X, c.Center.Y)); g.Add((c.Center.X + c.Radius, c.Center.Y)); g.Add((c.Center.X, c.Center.Y + c.Radius)); g.Add((c.Center.X - c.Radius, c.Center.Y)); g.Add((c.Center.X, c.Center.Y - c.Radius)); break;
            case TextEntity t: g.Add((t.InsertPoint.X, t.InsertPoint.Y)); break;
            case MText m: g.Add((m.InsertPoint.X, m.InsertPoint.Y)); break;
            case Insert i: g.Add((i.InsertPoint.X, i.InsertPoint.Y)); break;
            case Point pt: g.Add((pt.Location.X, pt.Location.Y)); break;
            case Ellipse el: g.Add((el.Center.X, el.Center.Y)); break;
        }
        return g;
    }

    static (string, Action, Action) Griff(List<Entity> liste, JsonObject par)
    {
        if (liste.Count != 1) throw new HlksFehler("Ein Griff gehört zu einem Objekt.", "kennungen: ein Objekt; index: Nummer des Griffs (aus zeichnung_objekte, Feld griffe); nach: [x, y].");
        var e = liste[0];
        int i = (int)Zahl(par, "index");
        var (nx, ny) = Punkt(par["nach"], "nach");
        var griffe = Griffe(e);
        if (i < 0 || i >= griffe.Count) throw new HlksFehler($"Den Griff {i} gibt es an diesem Objekt nicht (0 bis {griffe.Count - 1}).");
        double dx = nx - griffe[i].X, dy = ny - griffe[i].Y;
        void Schieben(double x, double y) { e.ApplyTransform(Transform.CreateTranslation(new XYZ(x, y, 0))); }
        (Action, Action) Ganz() => (() => Schieben(dx, dy), () => Schieben(-dx, -dy));
        (Action Vor, Action Zurueck) s;
        switch (e)
        {
            case Line l:
            {
                var (a, b) = (l.StartPoint, l.EndPoint);
                s = i == 2 ? Ganz() : (() => { if (i == 0) l.StartPoint = new XYZ(nx, ny, a.Z); else l.EndPoint = new XYZ(nx, ny, b.Z); }, () => { l.StartPoint = a; l.EndPoint = b; });
                break;
            }
            case LwPolyline p:
            {
                var alt = p.Vertices[i].Location;
                s = (() => p.Vertices[i].Location = new XY(nx, ny), () => p.Vertices[i].Location = alt);
                break;
            }
            case Arc a:
            {
                if (i == 0) { s = Ganz(); break; }
                var (von, bis) = (a.StartAngle, a.EndAngle);
                double w = Norm(Math.Atan2(ny - a.Center.Y, nx - a.Center.X));
                s = (() => { if (i == 1) a.StartAngle = w; else a.EndAngle = w; }, () => { a.StartAngle = von; a.EndAngle = bis; });
                break;
            }
            case Circle c:
            {
                if (i == 0) { s = Ganz(); break; }
                double alt = c.Radius, r = Math.Sqrt(Math.Pow(nx - c.Center.X, 2) + Math.Pow(ny - c.Center.Y, 2));
                if (!(r > 0)) throw new HlksFehler("Der neue Radius wäre 0.");
                s = (() => c.Radius = r, () => c.Radius = alt);
                break;
            }
            default: s = Ganz(); break;
        }
        return ("Griff ziehen", s.Vor, s.Zurueck);
    }

    // ---------- Blöcke ----------

    static bool Einfuegbar(BlockRecord b) => b.Layout is null && !b.Name.StartsWith('*') && !b.Flags.HasFlag(BlockTypeFlags.XRef) && !b.Flags.HasFlag(BlockTypeFlags.Anonymous);

    public static JsonObject Bloecke(Zeichnungsdatei z)
    {
        var referenzen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var b in z.Dokument.BlockRecords)
            foreach (var i in b.Entities.OfType<Insert>())
                if (i.Block is { } q) referenzen[q.Name] = referenzen.GetValueOrDefault(q.Name) + 1;
        var liste = new JsonArray();
        foreach (var b in z.Dokument.BlockRecords.Where(Einfuegbar).OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase))
        {
            var o = new JsonObject { ["name"] = b.Name, ["objekte"] = b.Entities.Count, ["referenzen"] = referenzen.GetValueOrDefault(b.Name) };
            var att = b.Entities.OfType<AttributeDefinition>().Select(a => (JsonNode?)a.Tag).ToArray();
            if (att.Length > 0) o["attribute"] = new JsonArray(att);
            liste.Add(o);
        }
        return new JsonObject { ["zeichnung"] = z.Name, ["anzahl"] = liste.Count, ["bloecke"] = liste };
    }

    static (string, Action, Action) BlockErstellen(Zeichnungsdatei z, List<Entity> liste, JsonObject par)
    {
        var name = ((par["name"] as JsonValue)?.TryGetValue<string>(out var n) == true ? n : "").Trim();
        if (name.Length == 0 || name.Length > 255 || name.StartsWith('*') || name.IndexOfAny(['<', '>', '/', '\\', '"', ':', ';', '?', '*', '|', ',', '=', '`']) >= 0)
            throw new HlksFehler($"Der Blockname «{name}» ist ungültig.", "Bis 255 Zeichen, ohne < > / \\ \" : ; ? * | , = `");
        if (z.Dokument.BlockRecords.Contains(name)) throw new HlksFehler($"Einen Block «{name}» gibt es schon.", "Einen anderen Namen wählen (zeichnung_bloecke listet die vorhandenen).");
        var (bx, by) = Punkt(par["basis"], "basis");
        var ort = BlockVon(liste[0]);
        if (liste.Any(e => !ReferenceEquals(BlockVon(e), ort))) throw new HlksFehler("Die Objekte liegen in verschiedenen Bereichen (Modell/Layout).");
        var block = new BlockRecord(name);
        foreach (var e in liste)
        {
            var k = (Entity)e.Clone();
            k.ApplyTransform(Transform.CreateTranslation(new XYZ(-bx, -by, 0)));
            block.Entities.Add(k);
        }
        var insert = new Insert(block) { InsertPoint = new XYZ(bx, by, 0), Layer = z.Dokument.Header.CurrentLayer ?? liste[0].Layer };
        return ($"Block {name} erstellen",
            () => { if (!z.Dokument.BlockRecords.Contains(name)) z.Dokument.BlockRecords.Add(block); foreach (var e in liste) ort.Entities.Remove(e); ort.Entities.Add(insert); },
            () => { ort.Entities.Remove(insert); foreach (var e in liste) ort.Entities.Add(e); z.Dokument.BlockRecords.Remove(name); });
    }

    /// <summary>Attributwerte einer Blockreferenz setzen: werte = { TAG: Text, … }.</summary>
    static (string, Action, Action) Attribute(List<Entity> liste, JsonObject par)
    {
        if (liste.Count != 1 || liste[0] is not Insert ins) throw new HlksFehler("Attribute gehören zu einer Blockreferenz.", "kennungen: ein Block; werte: { TAG: Text }.");
        if (par["werte"] is not JsonObject werte || werte.Count == 0) throw new HlksFehler("«werte» fehlt.", "werte: { TAG: Text, … } – die Namen stehen in zeichnung_objekte unter attribute.");
        var neu = new List<(AttributeEntity A, string Alt, string Neu)>();
        foreach (var (tag, wert) in werte)
        {
            var a = ins.Attributes.FirstOrDefault(x => string.Equals(x.Tag, tag, StringComparison.OrdinalIgnoreCase))
                ?? throw new HlksFehler($"Das Attribut «{tag}» gibt es an diesem Block nicht.", "Vorhanden: " + string.Join(", ", ins.Attributes.Select(x => x.Tag)));
            neu.Add((a, a.Value, (wert as JsonValue)?.TryGetValue<string>(out var t) == true ? t : wert?.ToString() ?? ""));
        }
        return ($"{neu.Count} Attribut(e) ändern", () => neu.ForEach(n => n.A.Value = n.Neu), () => neu.ForEach(n => n.A.Value = n.Alt));
    }

    // ---------- Weitere Objektarten ----------

    public static Entity? Bauen(Zeichnungsdatei z, string art, JsonObject o)
    {
        double Z(string name, double? vorgabe = null) => Json.Zahl(o[name]) ?? vorgabe ?? throw new HlksFehler($"«{name}» fehlt oder ist keine Zahl.");
        switch (art)
        {
            case "block":
            {
                var name = (o["name"] as JsonValue)?.TryGetValue<string>(out var n) == true ? n.Trim() : "";
                if (!z.Dokument.BlockRecords.TryGetValue(name, out var block) || !Einfuegbar(block))
                    throw new HlksFehler($"Den Block «{name}» gibt es nicht.", "Mit zeichnung_bloecke die Blöcke auflisten.");
                var (x, y) = Punkt(o["ort"], "ort");
                double sx = Z("massstab", 1), sy = Z("massstabY", sx);
                if (sx == 0 || sy == 0) throw new HlksFehler("Der Massstab darf nicht 0 sein.");
                var ins = new Insert(block) { InsertPoint = new XYZ(x, y, 0), XScale = sx, YScale = sy, ZScale = Math.Abs(sx), Rotation = Z("drehungGrad", 0) * Math.PI / 180 };
                // Attribute aus den Definitionen des Blocks anlegen und mit den mitgegebenen Werten füllen
                if (block.Entities.OfType<AttributeDefinition>().Any())
                {
                    try { ins.UpdateAttributes(); } catch (Exception ex) when (ex is not OutOfMemoryException) { }
                    if (o["attribute"] is JsonObject werte)
                        foreach (var a in ins.Attributes)
                            if (werte.FirstOrDefault(w => string.Equals(w.Key, a.Tag, StringComparison.OrdinalIgnoreCase)).Value is JsonValue v && v.TryGetValue<string>(out var text)) a.Value = text;
                }
                return ins;
            }
            case "radiusmass" or "durchmessermass":
            {
                var kreis = z.Objekt((o["objekt"] as JsonValue)?.TryGetValue<string>(out var k) == true ? k : throw new HlksFehler("«objekt» fehlt.", "objekt: Kennung eines Kreises oder Bogens.")) as Circle
                    ?? throw new HlksFehler("Radius- und Durchmessermass brauchen einen Kreis oder Bogen.");
                var (ox, oy) = Punkt(o["ort"], "ort");
                double w = Math.Atan2(oy - kreis.Center.Y, ox - kreis.Center.X), cx = kreis.Center.X, cy = kreis.Center.Y, r = kreis.Radius;
                var nah = new XYZ(cx + r * Math.Cos(w), cy + r * Math.Sin(w), 0);
                Dimension d = art == "radiusmass"
                    ? new DimensionRadius { DefinitionPoint = new XYZ(cx, cy, 0), AngleVertex = nah, LeaderLength = Math.Sqrt(Math.Pow(ox - nah.X, 2) + Math.Pow(oy - nah.Y, 2)) }
                    : new DimensionDiameter { DefinitionPoint = new XYZ(cx - r * Math.Cos(w), cy - r * Math.Sin(w), 0), AngleVertex = nah, LeaderLength = Math.Sqrt(Math.Pow(ox - nah.X, 2) + Math.Pow(oy - nah.Y, 2)) };
                d.TextMiddlePoint = new XYZ(ox, oy, 0);
                if (z.Dokument.Header.CurrentDimensionStyle is { } stil) d.Style = stil;
                return d;
            }
            case "winkelmass":
            {
                var (sx, sy) = Punkt(o["scheitel"], "scheitel"); var (ax, ay) = Punkt(o["von"], "von"); var (bx, by) = Punkt(o["bis"], "bis"); var (ox, oy) = Punkt(o["ort"], "ort");
                if ((ax == sx && ay == sy) || (bx == sx && by == sy)) throw new HlksFehler("Ein Schenkelpunkt liegt auf dem Scheitel.");
                var d = new DimensionAngular3Pt { AngleVertex = new XYZ(sx, sy, 0), FirstPoint = new XYZ(ax, ay, 0), SecondPoint = new XYZ(bx, by, 0), DefinitionPoint = new XYZ(ox, oy, 0) };
                if (z.Dokument.Header.CurrentDimensionStyle is { } stil) d.Style = stil;
                return d;
            }
            case "spline":
            {
                if (o["punkte"] is not JsonArray { Count: >= 2 } liste) throw new HlksFehler("Ein Spline braucht mindestens zwei punkte.", "punkte: [[x, y], …] – die Kurve läuft durch diese Punkte.");
                var sp = new Spline { Degree = 3 };
                foreach (var n in liste) { var (x, y) = Punkt(n, "punkte"); sp.FitPoints.Add(new XYZ(x, y, 0)); }
                if (o["geschlossen"] is JsonValue g && g.TryGetValue<bool>(out var zu) && zu && liste.Count > 2) sp.FitPoints.Add(sp.FitPoints[0]);
                bool ok;
                try { ok = sp.UpdateFromFitPoints(100); } catch (Exception ex) when (ex is not OutOfMemoryException) { ok = false; }
                if (!ok || sp.ControlPoints.Count < 2) throw new HlksFehler("Aus diesen Punkten lässt sich kein Spline rechnen.", "Mindestens drei verschiedene Punkte angeben oder eine Polylinie zeichnen.");
                return sp;
            }
            case "fuehrung":
            {
                if (o["punkte"] is not JsonArray { Count: >= 2 } liste) throw new HlksFehler("Eine Führung braucht mindestens zwei punkte.", "punkte: [[x, y] Pfeilspitze, [x, y], …]");
                var l = new Leader { ArrowHeadEnabled = true, PathType = LeaderPathType.StraightLineSegments, CreationType = LeaderCreationType.CreatedWithoutAnnotation };
                foreach (var n in liste) { var (x, y) = Punkt(n, "punkte"); l.Vertices.Add(new XYZ(x, y, 0)); }
                if (z.Dokument.Header.CurrentDimensionStyle is { } stil) l.Style = stil;
                return l;
            }
            default: return null;
        }
    }

    // ---------- Bemassungsstil ----------

    public static JsonObject Bemassungsstil(Zeichnungsdatei z, double? massstab, double? texthoehe, double? pfeil)
    {
        var stil = z.Dokument.Header.CurrentDimensionStyle ?? z.Dokument.DimensionStyles.FirstOrDefault()
            ?? throw new HlksFehler("Die Zeichnung hat keinen Bemassungsstil.");
        if (massstab is <= 0 || texthoehe is <= 0 || pfeil is <= 0) throw new HlksFehler("Massstab, Texthöhe und Pfeilgrösse müssen grösser als 0 sein.");
        if (massstab is not null || texthoehe is not null || pfeil is not null)
        {
            var (m0, t0, p0) = (stil.ScaleFactor, stil.TextHeight, stil.ArrowSize);
            void Setzen(double m, double t, double p)
            {
                stil.ScaleFactor = m; stil.TextHeight = t; stil.ArrowSize = p;
                foreach (var b in z.Dokument.BlockRecords.ToList())
                    foreach (var d in b.Entities.OfType<Dimension>().Where(d => ReferenceEquals(d.Style, stil)).ToList()) Nachfuehren(d);
            }
            double m1 = massstab ?? m0, t1 = texthoehe ?? t0, p1 = pfeil ?? p0;
            Setzen(m1, t1, p1);
            z.Journal.Dazu($"Bemassungsstil {stil.Name}", () => Setzen(m0, t0, p0), () => Setzen(m1, t1, p1));
        }
        return Antwort(z, new JsonObject { ["stil"] = stil.Name, ["massstab"] = stil.ScaleFactor, ["texthoehe"] = stil.TextHeight, ["pfeil"] = stil.ArrowSize });
    }
}
