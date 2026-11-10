// 2D-CAD «Zeichnung», Stufe 2: zeichnen, ändern, rückgängig, speichern. Jede Änderung ist ein Schritt im Journal der Zeichnung
// (Rückgängig/Wiederholen der Sitzung); Oberfläche, Befehlszeile und MCP rufen dieselben Befehle.
//   Zeichnen   linie · polylinie · rechteck · kreis · bogen (Mitte/Radius/Winkel oder drei Punkte) · text
//   Aendern    loeschen · verschieben · kopieren · drehen · skalieren · eigenschaften (layer, farbe, linientyp)
//   Speichern  DWG (2000, 2004, 2010, 2013, 2018) und DXF; die vorherige Datei bleibt als .bak, nach dem Schreiben wird wiedergelesen
// Noch nicht: spiegeln, versetzen, stutzen, dehnen, abrunden (Stufe 2, zweiter Teil).
// Hinweis: Ein gelöschtes und per Rückgängig zurückgeholtes Objekt bekommt eine neue Kennung (Handle).

using System.Text.Json.Nodes;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using CSMath;

namespace Hlks.Kern;

/// <summary>Schritte einer Zeichnung zum Zurücknehmen und Wiederholen.</summary>
public sealed class ZeichnungJournal
{
    public const int MaxSchritte = 200;
    readonly List<(string Name, Action Zurueck, Action Vor)> schritte = [];
    int stelle;       // Anzahl ausgeführter Schritte in der Liste
    int zaehler;      // wächst mit jeder Änderung, fällt beim Zurücknehmen: Vergleich mit dem gespeicherten Stand
    int verworfen;    // vorne weggefallene Schritte

    public int Stand => zaehler;
    public bool KannZurueck => stelle > 0;
    public bool KannVor => stelle < schritte.Count;
    public string? NaechsterZurueck => KannZurueck ? schritte[stelle - 1].Name : null;
    public string? NaechsterVor => KannVor ? schritte[stelle].Name : null;

    public void Dazu(string name, Action zurueck, Action vor)
    {
        schritte.RemoveRange(stelle, schritte.Count - stelle);
        schritte.Add((name, zurueck, vor));
        if (schritte.Count > MaxSchritte) { schritte.RemoveAt(0); verworfen++; }
        stelle = schritte.Count;
        zaehler = stelle + verworfen;
    }

    public string Zurueck()
    {
        if (!KannZurueck) throw new HlksFehler("Es gibt nichts zurückzunehmen.");
        var s = schritte[--stelle];
        s.Zurueck();
        zaehler = stelle + verworfen;
        return s.Name;
    }

    public string Vor()
    {
        if (!KannVor) throw new HlksFehler("Es gibt nichts zu wiederholen.");
        var s = schritte[stelle++];
        s.Vor();
        zaehler = stelle + verworfen;
        return s.Name;
    }
}

public static class ZeichnungBearbeiten
{
    public const string ZeichnenText = "Zeichnet Objekte in die geöffnete Zeichnung (ein Rückgängig-Schritt). objekte: Liste aus " +
        "{art:\"linie\", von:[x,y], bis:[x,y]} · {art:\"polylinie\", punkte:[[x,y] oder [x,y,ausbuchtung],…], geschlossen} · {art:\"rechteck\", ecke1:[x,y], ecke2:[x,y]} · " +
        "{art:\"kreis\", mitte:[x,y], radius} · {art:\"bogen\", mitte:[x,y], radius, vonGrad, bisGrad} bzw. {art:\"bogen\", punkte:[[x,y],[x,y],[x,y]]} (Anfang, Punkt auf dem Bogen, Ende) · " +
        "{art:\"text\", ort:[x,y], text, hoehe, drehungGrad} · {art:\"absatztext\", ort, text (mit Zeilenumbrüchen), hoehe, breite} · {art:\"ellipse\", mitte, hauptachse:[x,y] (Endpunkt), verhaeltnis} · " +
        "{art:\"polygon\", mitte, radius, seiten, drehungGrad, inkreis} · {art:\"punkt\", ort} · {art:\"schraffur\", muster: voll|linien|kreuz, abstand, winkelGrad, objekte:[Kennungen geschlossener Polylinien/Kreise] bzw. grenzen:[[[x,y],…],…]} · " +
        "{art:\"bemassung\", von, bis, ort (Lage der Masslinie), richtung: waagrecht|senkrecht|ausgerichtet} · {art:\"radiusmass\"|\"durchmessermass\", objekt: Kennung eines Kreises/Bogens, ort} · " +
        "{art:\"winkelmass\", scheitel, von, bis, ort} · {art:\"fuehrung\", punkte:[[x,y] Pfeilspitze, …]} · {art:\"spline\", punkte, geschlossen} · {art:\"block\", name, ort, massstab, drehungGrad, attribute:{TAG:Text}} (Blöcke: zeichnung_bloecke). Je Objekt wahlweise layer, farbe (ACI-Nummer 1–255, \"#rrggbb\" oder \"vonLayer\") und linientyp. " +
        "Ohne layer gilt der aktuelle Layer. Koordinaten in Zeichnungseinheiten. Antwort: die Kennungen der neuen Objekte. Die Datei ändert sich erst mit zeichnung_speichern.";
    public const string AendernText = "Ändert Objekte der Zeichnung (ein Rückgängig-Schritt). kennungen: Liste der Objekte (aus zeichnung_objekte). aktion: " +
        "loeschen · verschieben {dx, dy} · kopieren {dx, dy} · drehen {um:[x,y], winkelGrad} · skalieren {um:[x,y], faktor} · eigenschaften {layer, farbe, linientyp} · spiegeln {achse:[[x,y],[x,y]], kopie} · versetzen {abstand, seite:[x,y]} · " +
        "stutzen {punkt:[x,y] auf dem Teil, der weg soll, grenzen:[Kennungen] (leer = alle)} · dehnen {punkt nahe dem Ende, grenzen} · abrunden {radius, punkte:[[x,y],[x,y]]} (zwei Linien) · fasen {abstand1, abstand2, punkte} · " +
        "ursprung (Polylinie/Block zerlegen) · verbinden (Linien, Bögen, offene Polylinien zu einer Polylinie) · reihe {zeilen, spalten, dx, dy} · uebertragen {quelle: Kennung} · strecken {fenster:[x0,y0,x1,y1], dx, dy} · bruch {punkt, punkt2} · " +
        "griff {index, nach:[x,y]} (Griffe: zeichnung_objekte, Feld griffe) · block {name, basis:[x,y]} (Block aus den Objekten erstellen) · attribut {werte:{TAG:Text}} (an einer Blockreferenz). " +
        "Antwort: Anzahl und bei kopieren die neuen Kennungen. Die Datei ändert sich erst mit zeichnung_speichern.";
    public const string SpeichernText = "Speichert die Zeichnung als DWG oder DXF. Ohne datei an ihren Ort (die vorherige Fassung bleibt als .bak daneben), mit datei als neue Datei " +
        "(«Speichern unter»). fassung: 2000, 2004, 2010, 2013 oder 2018 (Vorgabe: wie geöffnet; DWG 2007 wird als 2010 geschrieben). Nach dem Schreiben wird die Datei " +
        "wiedergelesen und die Anzahl Objekte verglichen. Antwort: datei, fassung, groesseBytes, geprueft, meldungen.";
    public const string RueckgaengigText = "Nimmt den letzten Änderungsschritt der Zeichnung zurück (zeichnen, ändern, Layer anlegen).";
    public const string WiederholenText = "Stellt den zuletzt zurückgenommenen Schritt der Zeichnung wieder her.";
    public const string LayerAnlegenText = "Legt einen Layer an bzw. ändert Farbe und Linientyp eines bestehenden; mit aktuell=true wird er der aktuelle Layer für neue Objekte.";

    // ---------- Hilfen ----------

    internal static double Zahl(JsonObject o, string name, double? vorgabe = null) =>
        Json.Zahl(o[name]) ?? vorgabe ?? throw new HlksFehler($"«{name}» fehlt oder ist keine Zahl.", "Zahl in Zeichnungseinheiten bzw. Grad angeben.");

    internal static (double X, double Y) Punkt(JsonNode? n, string name)
    {
        if (n is JsonArray { Count: >= 2 } a && Json.Zahl(a[0]) is double x && Json.Zahl(a[1]) is double y && double.IsFinite(x) && double.IsFinite(y)) return (x, y);
        throw new HlksFehler($"«{name}» muss ein Punkt [x, y] sein.", "Zwei Zahlen in Zeichnungseinheiten, z. B. [1200, 500].");
    }

    static BlockRecord Block(Zeichnungsdatei z, string? layout) => z.Layout(layout)?.AssociatedBlock ?? z.Dokument.ModelSpace;

    static Layer LayerHolen(Zeichnungsdatei z, string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return z.Dokument.Header.CurrentLayer ?? z.Dokument.Layers["0"];
        return z.Dokument.Layers.TryGetValue(name.Trim(), out var l) ? l
            : throw new HlksFehler($"Den Layer «{name}» gibt es nicht.", "Mit zeichnung_layer auflisten oder mit zeichnung_layer_anlegen anlegen.");
    }

    static Color Farbe(JsonNode? n)
    {
        if (n is null) return Color.ByLayer;
        if (Json.Zahl(n) is double d && d >= 0 && d <= 256 && d == Math.Floor(d)) return d == 256 ? Color.ByLayer : d == 0 ? Color.ByBlock : new Color((short)d);
        var s = (n as JsonValue)?.TryGetValue<string>(out var t) == true ? t.Trim() : "";
        if (s.Equals("vonLayer", StringComparison.OrdinalIgnoreCase)) return Color.ByLayer;
        if (s.Equals("vonBlock", StringComparison.OrdinalIgnoreCase)) return Color.ByBlock;
        if (s.Length == 7 && s[0] == '#' && int.TryParse(s.AsSpan(1), System.Globalization.NumberStyles.HexNumber, null, out var rgb))
            return new Color((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        throw new HlksFehler($"Die Farbe «{n.ToJsonString()}» ist ungültig.", "ACI-Nummer 1–255, \"#rrggbb\" oder \"vonLayer\".");
    }

    static LineType Linientyp(Zeichnungsdatei z, string name) =>
        z.Dokument.LineTypes.TryGetValue(name.Trim(), out var lt) ? lt
            : throw new HlksFehler($"Den Linientyp «{name}» gibt es in der Zeichnung nicht.", "Vorhanden: " + string.Join(", ", z.Dokument.LineTypes.Select(l => l.Name)));

    static void Eigenschaften(Zeichnungsdatei z, Entity e, JsonObject o, Layer vorgabe)
    {
        e.Layer = o["layer"] is JsonValue lv && lv.TryGetValue<string>(out var ln) ? LayerHolen(z, ln) : vorgabe;
        if (o["farbe"] is { } f) e.Color = Farbe(f);
        if (o["linientyp"] is JsonValue tv && tv.TryGetValue<string>(out var tn) && tn.Length > 0) e.LineType = Linientyp(z, tn);
    }

    // ---------- Zeichnen ----------

    static Entity Bauen(Zeichnungsdatei z, JsonObject o)
    {
        var art = (o["art"] as JsonValue)?.TryGetValue<string>(out var a) == true ? a.Trim().ToLowerInvariant() : "";
        switch (art)
        {
            case "linie":
            {
                var (x0, y0) = Punkt(o["von"], "von"); var (x1, y1) = Punkt(o["bis"], "bis");
                if (x0 == x1 && y0 == y1) throw new HlksFehler("Die Linie hat keine Länge.", "von und bis müssen verschieden sein.");
                return new Line { StartPoint = new XYZ(x0, y0, 0), EndPoint = new XYZ(x1, y1, 0) };
            }
            case "polylinie":
            {
                if (o["punkte"] is not JsonArray { Count: >= 2 } liste) throw new HlksFehler("Eine Polylinie braucht mindestens zwei punkte.", "punkte: [[x, y], [x, y], …]");
                var p = new LwPolyline { IsClosed = o["geschlossen"] is JsonValue g && g.TryGetValue<bool>(out var zu) && zu };
                foreach (var n in liste)
                {
                    var (x, y) = Punkt(n, "punkte");
                    var v = new LwPolyline.Vertex(new XY(x, y));
                    if (n is JsonArray { Count: >= 3 } drei && Json.Zahl(drei[2]) is double b) v.Bulge = b;
                    p.Vertices.Add(v);
                }
                return p;
            }
            case "rechteck":
            {
                var (x0, y0) = Punkt(o["ecke1"], "ecke1"); var (x1, y1) = Punkt(o["ecke2"], "ecke2");
                if (x0 == x1 || y0 == y1) throw new HlksFehler("Das Rechteck hat keine Fläche.", "ecke1 und ecke2 müssen sich in x und y unterscheiden.");
                var p = new LwPolyline { IsClosed = true };
                foreach (var (x, y) in new[] { (x0, y0), (x1, y0), (x1, y1), (x0, y1) }) p.Vertices.Add(new LwPolyline.Vertex(new XY(x, y)));
                return p;
            }
            case "kreis":
            {
                var (x, y) = Punkt(o["mitte"], "mitte");
                double r = Zahl(o, "radius");
                if (!(r > 0)) throw new HlksFehler("Der Radius muss grösser als 0 sein.");
                return new Circle { Center = new XYZ(x, y, 0), Radius = r };
            }
            case "bogen":
            {
                if (o["punkte"] is JsonArray { Count: 3 } drei)
                {
                    var (ax, ay) = Punkt(drei[0], "punkte"); var (bx, by) = Punkt(drei[1], "punkte"); var (cx, cy) = Punkt(drei[2], "punkte");
                    var (mx, my, rr, von, bis) = BogenDurch(ax, ay, bx, by, cx, cy);
                    return new Arc { Center = new XYZ(mx, my, 0), Radius = rr, StartAngle = von, EndAngle = bis };
                }
                var (x, y) = Punkt(o["mitte"], "mitte");
                double r = Zahl(o, "radius");
                if (!(r > 0)) throw new HlksFehler("Der Radius muss grösser als 0 sein.");
                return new Arc { Center = new XYZ(x, y, 0), Radius = r, StartAngle = Zahl(o, "vonGrad") * Math.PI / 180, EndAngle = Zahl(o, "bisGrad") * Math.PI / 180 };
            }
            case "text":
            {
                var (x, y) = Punkt(o["ort"], "ort");
                var text = (o["text"] as JsonValue)?.TryGetValue<string>(out var t) == true ? t : "";
                if (string.IsNullOrWhiteSpace(text)) throw new HlksFehler("Der Text fehlt.", "text angeben.");
                double h = Zahl(o, "hoehe");
                if (!(h > 0)) throw new HlksFehler("Die Texthöhe muss grösser als 0 sein.");
                var e = new TextEntity { InsertPoint = new XYZ(x, y, 0), Value = text, Height = h, Rotation = Zahl(o, "drehungGrad", 0) * Math.PI / 180 };
                if (z.Dokument.Header.CurrentTextStyle is { } stil) e.Style = stil;
                return e;
            }
            default:
                return ZeichnungGeoBefehle.Bauen(z, art, o) ?? throw new HlksFehler($"Die Art «{art}» lässt sich nicht zeichnen.", "Erlaubt: linie, polylinie, rechteck, kreis, bogen, text, ellipse, polygon, punkt, absatztext, schraffur, bemassung, radiusmass, durchmessermass, winkelmass, fuehrung, spline, block.");
        }
    }

    /// <summary>Bogen durch drei Punkte (Anfang, Punkt auf dem Bogen, Ende): Mitte, Radius, Anfangs- und Endwinkel (Bogenmass, Gegenuhrzeigersinn).</summary>
    public static (double Mx, double My, double R, double Von, double Bis) BogenDurch(double ax, double ay, double bx, double by, double cx, double cy)
    {
        double d = 2 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));
        if (Math.Abs(d) < 1e-12) throw new HlksFehler("Die drei Punkte liegen auf einer Geraden: kein Bogen.", "Den mittleren Punkt neben die Verbindung von Anfang und Ende setzen.");
        double a2 = ax * ax + ay * ay, b2 = bx * bx + by * by, c2 = cx * cx + cy * cy;
        double mx = (a2 * (by - cy) + b2 * (cy - ay) + c2 * (ay - by)) / d, my = (a2 * (cx - bx) + b2 * (ax - cx) + c2 * (bx - ax)) / d;
        double r = Math.Sqrt((ax - mx) * (ax - mx) + (ay - my) * (ay - my));
        double wa = Math.Atan2(ay - my, ax - mx), wc = Math.Atan2(cy - my, cx - mx);
        // Umlaufsinn A → B → C: links herum = Bogen von A nach C, sonst von C nach A (ein Bogen läuft immer im Gegenuhrzeigersinn)
        bool links = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax) > 0;
        return links ? (mx, my, r, wa, wc) : (mx, my, r, wc, wa);
    }

    public static JsonObject Zeichnen(Zeichnungsdatei z, string? layout, JsonArray objekte)
    {
        if (objekte.Count == 0) throw new HlksFehler("objekte ist leer.", "Mindestens ein Objekt angeben.");
        if (objekte.Count > 5000) throw new HlksFehler("Höchstens 5000 Objekte je Aufruf.");
        var block = Block(z, layout);
        var vorgabe = LayerHolen(z, null);
        var neu = new List<Entity>(objekte.Count);
        foreach (var n in objekte)   // erst alles bauen: ein Fehler lässt die Zeichnung unverändert
        {
            if (n is not JsonObject o) throw new HlksFehler("Jedes Objekt muss ein JSON-Objekt mit «art» sein.");
            var e = Bauen(z, o);
            Eigenschaften(z, e, o, vorgabe);
            neu.Add(e);
        }
        void Vor()
        {
            foreach (var e in neu)
            {
                block.Entities.Add(e);
                if (e is Dimension d) ZeichnungWeitere.MassBild(d);   // Bild der Bemassung erzeugen
            }
        }
        void Zurueck() { foreach (var e in neu) block.Entities.Remove(e); }
        Vor();
        z.Journal.Dazu(neu.Count == 1 ? ZeichnungAbfragen.Art(neu[0]) + " zeichnen" : $"{neu.Count} Objekte zeichnen", Zurueck, Vor);
        return Antwort(z, new JsonObject { ["gezeichnet"] = neu.Count, ["kennungen"] = new JsonArray(neu.Select(e => (JsonNode?)Zeichnungsdatei.Kennung(e)).ToArray()) });
    }

    // ---------- Ändern ----------

    static void Bewegen(Entity e, Transform t)
    {
        e.ApplyTransform(t);
        if (e is Dimension d) ZeichnungWeitere.MassBild(d);   // Bild der Bemassung nachführen
    }

    public static JsonObject Aendern(Zeichnungsdatei z, IEnumerable<string> kennungen, string aktion, JsonObject par)
    {
        var liste = kennungen.Select(z.Objekt).Distinct().ToList();
        if (liste.Count == 0) throw new HlksFehler("kennungen ist leer.", "Die Kennungen der Objekte angeben (aus zeichnung_objekte).");
        foreach (var e in liste)
            if (e.Layer is { } l && l.Flags.HasFlag(LayerFlags.Locked))
                throw new HlksFehler($"Das Objekt {Zeichnungsdatei.Kennung(e)} liegt auf dem gesperrten Layer «{l.Name}».", "Den Layer in AutoCAD entsperren oder das Objekt weglassen.");
        var antwort = new JsonObject { ["aktion"] = aktion, ["anzahl"] = liste.Count };
        string name;
        Action vor, zurueck;
        switch (aktion.Trim().ToLowerInvariant())
        {
            case "loeschen":
            {
                var orte = liste.Select(e => (E: e, B: e.Owner as BlockRecord ?? throw new HlksFehler($"Das Objekt {Zeichnungsdatei.Kennung(e)} lässt sich nicht löschen (Teil eines anderen Objekts)."))).ToList();
                vor = () => { foreach (var (e, b) in orte) b.Entities.Remove(e); };
                zurueck = () => { foreach (var (e, b) in orte) b.Entities.Add(e); };
                name = $"{liste.Count} löschen";
                break;
            }
            case "verschieben":
            {
                double dx = Zahl(par, "dx"), dy = Zahl(par, "dy");
                vor = () => { foreach (var e in liste) Bewegen(e, Transform.CreateTranslation(new XYZ(dx, dy, 0))); };
                zurueck = () => { foreach (var e in liste) Bewegen(e, Transform.CreateTranslation(new XYZ(-dx, -dy, 0))); };
                name = $"{liste.Count} verschieben";
                break;
            }
            case "kopieren":
            {
                double dx = Zahl(par, "dx"), dy = Zahl(par, "dy");
                var kopien = new List<(Entity E, BlockRecord B)>();
                foreach (var e in liste)
                {
                    var b = e.Owner as BlockRecord ?? throw new HlksFehler($"Das Objekt {Zeichnungsdatei.Kennung(e)} lässt sich nicht kopieren (Teil eines anderen Objekts).");
                    var k = (Entity)e.Clone();
                    Bewegen(k, Transform.CreateTranslation(new XYZ(dx, dy, 0)));
                    kopien.Add((k, b));
                }
                vor = () => { foreach (var (k, b) in kopien) b.Entities.Add(k); };
                zurueck = () => { foreach (var (k, b) in kopien) b.Entities.Remove(k); };
                name = $"{liste.Count} kopieren";
                vor();
                z.Journal.Dazu(name, zurueck, vor);
                antwort["kennungen"] = new JsonArray(kopien.Select(k => (JsonNode?)Zeichnungsdatei.Kennung(k.E)).ToArray());
                return Antwort(z, antwort);
            }
            case "drehen":
            {
                var (ux, uy) = Punkt(par["um"], "um");
                double w = Zahl(par, "winkelGrad") * Math.PI / 180;
                void Drehen(double winkel)
                {
                    foreach (var e in liste)
                    {
                        Bewegen(e, Transform.CreateTranslation(new XYZ(-ux, -uy, 0)));
                        Bewegen(e, Transform.CreateRotation(XYZ.AxisZ, winkel));
                        Bewegen(e, Transform.CreateTranslation(new XYZ(ux, uy, 0)));
                    }
                }
                vor = () => Drehen(w); zurueck = () => Drehen(-w);
                name = $"{liste.Count} drehen";
                break;
            }
            case "skalieren":
            {
                var (ux, uy) = Punkt(par["um"], "um");
                double f = Zahl(par, "faktor");
                if (!(f > 0)) throw new HlksFehler("Der Faktor muss grösser als 0 sein.");
                vor = () => { foreach (var e in liste) Bewegen(e, Transform.CreateScaling(new XYZ(f, f, f), new XYZ(ux, uy, 0))); };
                zurueck = () => { foreach (var e in liste) Bewegen(e, Transform.CreateScaling(new XYZ(1 / f, 1 / f, 1 / f), new XYZ(ux, uy, 0))); };
                name = $"{liste.Count} skalieren";
                break;
            }
            case "eigenschaften":
            {
                var layer = par["layer"] is JsonValue lv && lv.TryGetValue<string>(out var ln) ? LayerHolen(z, ln) : null;
                Color? farbe = par["farbe"] is { } fn ? Farbe(fn) : null;
                var lt = par["linientyp"] is JsonValue tv && tv.TryGetValue<string>(out var tn) && tn.Length > 0 ? Linientyp(z, tn) : null;
                if (layer is null && farbe is null && lt is null) throw new HlksFehler("Es ist keine Eigenschaft angegeben.", "layer, farbe oder linientyp angeben.");
                var alt = liste.Select(e => (E: e, e.Layer, e.Color, e.LineType)).ToList();
                vor = () => { foreach (var e in liste) { if (layer is not null) e.Layer = layer; if (farbe is { } c) e.Color = c; if (lt is not null) e.LineType = lt; } };
                zurueck = () => { foreach (var a in alt) { a.E.Layer = a.Layer; a.E.Color = a.Color; a.E.LineType = a.LineType; } };
                name = $"Eigenschaften von {liste.Count} ändern";
                break;
            }
            default:
                (name, vor, zurueck) = ZeichnungGeoBefehle.Aendern(z, liste, aktion.Trim().ToLowerInvariant(), par, antwort);
                break;
        }
        vor();
        z.Journal.Dazu(name, zurueck, vor);
        return Antwort(z, antwort);
    }

    // ---------- Layer ----------

    public static JsonObject LayerAnlegen(Zeichnungsdatei z, string name, JsonNode? farbe, string? linientyp, bool aktuell)
    {
        name = name.Trim();
        if (name.Length == 0 || name.Length > 255 || name.IndexOfAny(['<', '>', '/', '\\', '"', ':', ';', '?', '*', '|', ',', '=', '`']) >= 0)
            throw new HlksFehler($"Der Layername «{name}» ist ungültig.", "Bis 255 Zeichen, ohne < > / \\ \" : ; ? * | , = `");
        var d = z.Dokument;
        bool neu = !d.Layers.TryGetValue(name, out var layer);
        layer ??= new Layer(name);
        var (altFarbe, altTyp, altAktuell) = (layer.Color, layer.LineType, d.Header.CurrentLayer);
        Color? f = farbe is null ? null : Farbe(farbe);
        var lt = string.IsNullOrWhiteSpace(linientyp) ? null : Linientyp(z, linientyp);
        void Vor()
        {
            if (neu && !d.Layers.Contains(name)) d.Layers.Add(layer);
            if (f is { } c) layer.Color = c;
            if (lt is not null) layer.LineType = lt;
            if (aktuell) d.Header.CurrentLayerName = layer.Name;
        }
        void Zurueck()
        {
            if (aktuell) d.Header.CurrentLayerName = altAktuell?.Name ?? "0";
            if (neu) d.Layers.Remove(name);
            else { layer.Color = altFarbe; layer.LineType = altTyp; }
        }
        Vor();
        z.Journal.Dazu(neu ? $"Layer {name} anlegen" : $"Layer {name} ändern", Zurueck, Vor);
        return Antwort(z, new JsonObject { ["layer"] = name, ["neu"] = neu, ["aktuell"] = d.Header.CurrentLayer?.Name });
    }

    // ---------- Journal ----------

    public static JsonObject Rueckgaengig(Zeichnungsdatei z) => Antwort(z, new JsonObject { ["zurueckgenommen"] = z.Journal.Zurueck() });
    public static JsonObject Wiederholen(Zeichnungsdatei z) => Antwort(z, new JsonObject { ["wiederholt"] = z.Journal.Vor() });

    /// <summary>Stand der Zeichnung für jede Antwort: geändert, was Rückgängig/Wiederholen als Nächstes täten, aktueller Layer.</summary>
    public static JsonObject Antwort(Zeichnungsdatei z, JsonObject o)
    {
        o["zeichnung"] = z.Name;
        o["geaendert"] = z.Geaendert;
        if (z.Journal.NaechsterZurueck is { } r) o["rueckgaengig"] = r;
        if (z.Journal.NaechsterVor is { } w) o["wiederholen"] = w;
        o["aktuellerLayer"] = z.Dokument.Header.CurrentLayer?.Name ?? "0";
        return o;
    }

    // ---------- Speichern ----------

    static readonly Dictionary<string, ACadVersion> Fassungen = new(StringComparer.OrdinalIgnoreCase)
    {
        ["2000"] = ACadVersion.AC1015, ["2004"] = ACadVersion.AC1018, ["2010"] = ACadVersion.AC1024, ["2013"] = ACadVersion.AC1027, ["2018"] = ACadVersion.AC1032,
    };

    /// <param name="ziel">Voller Pfad (.dwg oder .dxf); null = an den Ort der Zeichnung.</param>
    public static JsonObject Speichern(Zeichnungsdatei z, string? ziel, string? fassung)
    {
        var pfad = ziel ?? z.Pfad;
        bool dxf = pfad.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase);
        if (!dxf && !pfad.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase)) throw new HlksFehler("Die Datei muss auf .dwg oder .dxf enden.");
        var d = z.Dokument;
        var hinweise = new List<string>();
        var alt = d.Header.Version;
        if (!string.IsNullOrWhiteSpace(fassung))
            d.Header.Version = Fassungen.TryGetValue(fassung.Trim(), out var v) ? v : throw new HlksFehler($"Die Fassung «{fassung}» gibt es nicht.", "Erlaubt: 2000, 2004, 2010, 2013, 2018.");
        if (d.Header.Version == ACadVersion.AC1021 && !dxf) { d.Header.Version = ACadVersion.AC1024; hinweise.Add("DWG 2007 lässt sich nicht schreiben: als DWG 2010 gespeichert."); }
        if (d.Header.Version < ACadVersion.AC1015) { d.Header.Version = ACadVersion.AC1015; hinweise.Add("Ältere Fassung als 2000: als DWG/DXF 2000 gespeichert."); }

        Directory.CreateDirectory(Path.GetDirectoryName(pfad)!);
        var temp = pfad + $".{Guid.NewGuid():N}.tmp";
        var meldungen = new SortedDictionary<string, int>(StringComparer.Ordinal);
        void Notiz(object? s, NotificationEventArgs e)
        {
            if (e.NotificationType == NotificationType.None) return;
            var t = e.Message.Length > 160 ? e.Message[..160] + "…" : e.Message;
            meldungen[t] = meldungen.GetValueOrDefault(t) + 1;
        }
        try
        {
            try
            {
                if (dxf) { using var w = new DxfWriter(temp, d, false); w.OnNotification += Notiz; w.Write(); }
                else { using var w = new DwgWriter(temp, d); w.OnNotification += Notiz; w.Write(); }
            }
            catch (Exception ex) when (ex is not HlksFehler and not OutOfMemoryException and not IOException and not UnauthorizedAccessException)
            {
                d.Header.Version = alt;
                throw new HlksFehler($"Die Zeichnung lässt sich nicht als {(dxf ? "DXF" : "DWG")} schreiben: {ex.Message}", "Als DXF bzw. in einer anderen Fassung speichern; die geöffnete Zeichnung ist unverändert.");
            }
            // Gegenprobe: wiederlesen und Objekte je Layout zählen, bevor die bisherige Datei ersetzt wird
            int Anzahl(CadDocument doc) => doc.BlockRecords.Where(b => b.Layout is not null || ReferenceEquals(b, doc.ModelSpace)).Sum(b => b.Entities.Count);
            int soll = Anzahl(d), ist;
            try { ist = Anzahl(dxf ? DxfReader.Read(temp) : DwgReader.Read(temp)); }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                d.Header.Version = alt;
                throw new HlksFehler($"Die geschriebene Datei lässt sich nicht wieder lesen ({ex.Message}); die bisherige Datei bleibt unverändert.", "Als DXF bzw. in einer anderen Fassung speichern.");
            }
            if (ist != soll) hinweise.Add($"Gegenprobe: {soll} Objekte in der Zeichnung, {ist} in der geschriebenen Datei.");
            if (File.Exists(pfad)) File.Copy(pfad, Path.ChangeExtension(pfad, ".bak"), overwrite: true);
            File.Move(temp, pfad, overwrite: true);
            z.Gespeichert(Path.GetFullPath(pfad));
            var o = new JsonObject
            {
                ["datei"] = z.Pfad, ["fassung"] = d.Header.Version.ToString(), ["groesseBytes"] = new FileInfo(pfad).Length, ["objekte"] = soll, ["geprueft"] = ist == soll,
            };
            if (hinweise.Count > 0) o["hinweise"] = new JsonArray(hinweise.Select(h => (JsonNode?)h).ToArray());
            if (meldungen.Count > 0) o["meldungen"] = new JsonArray(meldungen.Take(30).Select(m => (JsonNode?)(m.Value > 1 ? $"{m.Value}× {m.Key}" : m.Key)).ToArray());
            return Antwort(z, o);
        }
        finally { try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { } }
    }
}
