// Familien (Modul 2, B4): Kontrollbild familie_vergleichen (entwurf/modul-2.md, «Kontrollbild»). Die Seite einer Unterlage (PDF,
// Bild, DWG/DXF – bei STEP/IGES/STL/OBJ/IFC der Umriss des 3D-Modells aus dem Befund «ansicht» bzw. die Hülle) als Hintergrund, die
// Familienansicht (familie_ansicht) rot darüber, als PNG. Weg: PDF-Seite mit Hintergrundbild und Linien über PDFsharp, dann mit
// Docnet zu PNG (wie unterlage_bild).
//
// Lage der Familie auf der Unterlage (Ähnlichkeitsabbildung: Massstab, Drehung, Verschiebung; ohne Spiegelung):
//   1. passpunkte: zwei Paare { familie: [u, v] (Ansichtskoordinaten der Familie in mm), unterlage: [x, y] (wie befunde) };
//   2. sonst ein Massbezug der Familie in dieser Ansicht, dessen quellen ein Mass dieser Unterlage nennen (DXF-Bemassung:
//      Definitionspunkte; PDF-Masslinie: Enden der Linie) – von/bis ↔ die beiden Punkte, Richtung mit der kleineren Drehung;
//   3. 3D-Modell ohne Passpunkte: Massstab 1, linke untere Ecke des Umrisses auf die der Familie.
// Antwort: PNG, die Abweichungsliste der Masse dieses Typs (FamilienMasse) und bei massstäblichen Unterlagen (DWG/DXF, 3D-Modell)
// zusätzlich der geometrische Vergleich: je Zeichnungskante (linie, polylinie, bogen, kreis; ohne die Layer der Bemassungen) der
// grösste Abstand ihrer Punkte zur nächsten Kante der Familie, je DXF-Mass, ob seine Definitionspunkte auf einer Familienkante
// liegen, je Anschluss der Abstand zum nächsten Stutzen bzw. Port. PDF-Massbilder: «schematisch, Überlagerung nur Anschauung».
// Bildbereich: PDF-Seite bzw. Bild wie unterlage_bild; DWG/DXF ohne ausschnitt die Vereinigung von Zeichnung und eingepasster
// Familie plus Rand (4 %, mindestens 50 Bildpunkte) und oben Platz für die Beschriftung (Nachtrag Hauptlauf 18.10.2026: die Familie klebt nicht am Rand).
// ANNAHMEN (Prüfliste): Toleranz einer Kante max(1 mm; 0.5 % ihrer Länge), eines Massendpunkts max(1 mm; 0.5 % des Masses),
//   Anschlusslage 2 mm (Spezifikation); Ausschnitt der Zeichnung ohne Angabe = Umriss der eingepassten Familie plus 10 % (mind.
//   10 mm); Layer mit «mass», «bem», «dim», «text» im Namen zählen nicht als Kanten; 3D: Familie und Modell über die Ecke der
//   Hülle (min x, y, z) aufeinandergelegt, ohne Drehung (B3: Achsen der Datei nicht gedreht); Ansicht «unten» des 3D-Befunds
//   ist um 180° gedreht gegenüber der Familienansicht und wird umgerechnet; Vorgabe Detailgrad mittel, Bildbreite 1200.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Hlks.Kern;

public static class FamilienKontrollbild
{
    public const string AbfrageText = "Kontrollbild einer Familie: die Seite der Unterlage (PDF, Bild, DWG/DXF; bei STEP/STL/OBJ/IFC der Umriss des 3D-Modells) mit der Familienansicht rot darüber, als PNG. " +
        "Eingepasst über zwei passpunkte [{ familie: [u, v], unterlage: [x, y] }, …] (u/v = Ansichtskoordinaten der Familie in mm wie familie_ansicht, x/y = Unterlagen-Koordinaten wie befunde) " +
        "oder über einen Massbezug der Familie, dessen quellen ein Mass dieser Unterlage nennen (DXF-Bemassung, PDF-Masslinie). Dazu die Abweichungsliste der Masse dieses Typs; " +
        "bei massstäblichen Unterlagen (DWG/DXF, 3D-Modell) der geometrische Vergleich (Abstand jeder Zeichnungskante zur nächsten Familienkante, Massendpunkte, Anschlusslage gegen Stutzen bzw. Port). " +
        "PDF-Massbilder sind schematisch: die Überlagerung ist nur Anschauung. REST: PNG als base64; MCP: Bildinhalt.";

    public static readonly IReadOnlyList<ParameterDef> Parameter =
    [
        new("familie", "text", true, "Familie (Kennzeichen, Name oder id)"),
        new("typ", "text", true, "Typ aus der Typenliste der Familie"),
        new("unterlage", "text", true, "Unterlage (Kennzeichen, Id oder Dateiname)"),
        new("ansicht", "text", true, "vorne | hinten | links | rechts | oben | unten"),
        new("seite", "zahl", false, "PDF: Seite (ab 1, Vorgabe 1)"),
        new("passpunkte", "liste", false, "Zwei Passpunkte [{ \"familie\": [u, v], \"unterlage\": [x, y] }, …]; ohne: über einen Massbezug mit Mass dieser Unterlage"),
        new("detailgrad", "text", false, "grob | mittel | fein (Vorgabe mittel)"),
        new("ausschnitt", "liste", false, "Ausschnitt [x0, y0, x1, y1] der Unterlage (Unterlagen-Koordinaten); Vorgabe: ganze Seite bzw. Zeichnung"),
        new("breite", "zahl", false, "Breite des Bildes in Bildpunkten (Vorgabe 1200, 100–3000)"),
    ];

    /// <summary>Ähnlichkeitsabbildung (u, v) → (x, y): Massstab und Drehung (A + iB), Verschiebung.</summary>
    sealed record Abb(double A, double B, double Tx, double Ty, string Quelle)
    {
        public (double X, double Y) Hin(double u, double v) => (A * u - B * v + Tx, B * u + A * v + Ty);
        public (double U, double V) Zurueck(double x, double y)
        {
            x -= Tx; y -= Ty; double d = A * A + B * B;
            return ((A * x + B * y) / d, (-B * x + A * y) / d);
        }
        public double Massstab => Math.Sqrt(A * A + B * B);
        public double Drehung => Math.Atan2(B, A) * 180 / Math.PI;

        public static Abb? Aus((double U, double V) f1, (double X, double Y) g1, (double U, double V) f2, (double X, double Y) g2, string quelle)
        {
            double du = f2.U - f1.U, dv = f2.V - f1.V, dx = g2.X - g1.X, dy = g2.Y - g1.Y;
            double n = du * du + dv * dv;
            if (n < 1e-12 || dx * dx + dy * dy < 1e-12) return null;
            double a = (dx * du + dy * dv) / n, b = (dy * du - dx * dv) / n;
            return new Abb(a, b, g1.X - (a * f1.U - b * f1.V), g1.Y - (b * f1.U + a * f1.V), quelle);
        }
    }

    readonly record struct Strecke(double X1, double Y1, double X2, double Y2)
    {
        public double Laenge => Math.Sqrt((X2 - X1) * (X2 - X1) + (Y2 - Y1) * (Y2 - Y1));
    }

    static string? T(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    /// <summary>REST: Antwort mit dem PNG als base64.</summary>
    public static JsonObject Abfrage(Projekt p, JsonObject par)
    {
        var o = Erzeugen(p, par, out var png);
        o["base64"] = Convert.ToBase64String(png);
        return o;
    }

    public static JsonObject Erzeugen(Projekt p, JsonObject par, out byte[] png)
    {
        var fam = Familien.Hole(p.Modell, Param.Text(par, "familie", true));
        var f = Familien.Definition(fam, p.Katalog);
        if (!f.Gueltig) throw FamilienPruefung.Fehlermeldung($"Familie {fam.Kennzeichen} «{fam.Name}»", f.Fehler);
        var typ = Param.Text(par, "typ", true)!;
        if (f.Typ(typ) is null) throw new HlksFehler($"Typ «{typ}» gibt es in der Familie {fam.Kennzeichen} nicht.", "Typen: " + string.Join(", ", f.Typen.Select(t => t.Name)));
        var ansicht = Param.Text(par, "ansicht", true)!.Trim().ToLowerInvariant();
        if (!FamilieDef.Ansichten.Contains(ansicht)) throw new HlksFehler($"Ansicht «{ansicht}» gibt es für das Kontrollbild nicht.", "vorne, hinten, links, rechts, oben oder unten");
        var detail = Param.Text(par, "detailgrad")?.Trim().ToLowerInvariant() ?? "mittel";
        if (!FamilieDef.Detailgrade.Contains(detail)) throw new HlksFehler($"Detailgrad «{detail}» gibt es nicht.", "grob, mittel oder fein");
        int seite = Param.Zahl(par, "seite") is double sd ? (int)sd : 1;
        int breite = Math.Clamp(Param.Zahl(par, "breite") is double bd ? (int)bd : 1200, UnterlagenAnsicht.MinBreite, UnterlagenAnsicht.MaxBreite);
        double[]? ausschnitt = null;
        if (par["ausschnitt"] is JsonArray aa)
        {
            if (aa.Count != 4) throw new HlksFehler("ausschnitt muss [x0, y0, x1, y1] sein.", "Vier Zahlen in Unterlagen-Koordinaten.");
            ausschnitt = aa.Select(x => Json.Zahl(x) ?? throw new HlksFehler("ausschnitt enthält keine Zahl.", "Vier Zahlen in Unterlagen-Koordinaten.")).ToArray();
        }
        var u = UnterlagenService.Aufloesen(p, Param.Text(par, "unterlage", true));
        var a = FamilienAuswertung.Aufloesen(f, typ, null);
        if (!a.Gueltig) throw FamilienPruefung.Fehlermeldung($"Familie {fam.Kennzeichen} Typ «{typ}»", a.Fehler);
        var def = (JsonObject)fam.Eigenschaft("definition")!.Inhalt!;
        var q = new BefundQuelle(p);
        var hinweise = new List<string>();

        // Familie: Kanten der Ansicht (u/v in mm)
        var fa = FamilienAnsicht.Erzeugen(p, fam.Kennzeichen, typ, ansicht, detail, null);
        var plan = fa["plangrafik"].Deserialize<Plangrafik>(Json.Optionen) ?? new Plangrafik();
        var familie = Kanten(plan);
        if (familie.Count == 0) hinweise.Add($"Die Familie hat im Detailgrad {detail} keine Körper in dieser Ansicht.");
        var anschluesse = a.Anschluesse.Where(x => x.Sichtbar).ToList();

        bool d3 = u.Art is "step" or "iges" or "stl" or "obj" or "ifc";
        bool massstaeblich = d3 || u.Art is "dwg" or "dxf";
        bool yUnten = u.Art == "bild";

        // Einpassen (vor dem Hintergrund: der Ausschnitt einer DWG/DXF richtet sich nach der eingepassten Familie)
        List<List<(double X, double Y)>> umriss = d3 ? Umriss3d(q, u, ansicht, hinweise) : [];
        var abb = Passpunkte(par, yUnten) ?? MassEinpassen(f, def, a, ansicht, u, seite, q)
            ?? (d3 ? HuelleEinpassen(familie, umriss) : null)
            ?? throw new HlksFehler("Die Lage der Familie auf der Unterlage ist nicht bestimmt: es fehlen Passpunkte, und kein Massbezug dieser Ansicht nennt ein Mass dieser Unterlage.",
                "passpunkte angeben: [{ \"familie\": [u1, v1], \"unterlage\": [x1, y1] }, { \"familie\": [u2, v2], \"unterlage\": [x2, y2] }] – u/v aus familie_ansicht (mm), x/y aus befunde bzw. unterlage_bild (Bereich).");

        // Hintergrund und Bereich (Unterlagen-Koordinaten, y nach oben; Bild: y gespiegelt)
        byte[]? bild = null; int w, h; double x0 = 0, y0 = 0, x1 = 0, y1 = 0;
        (double X, double Y, double B, double H)? bildFeld = null;   // Lage des Hintergrundbilds auf der Seite (Bildpunkte); null = ganze Seite
        if (!d3)
        {
            // DWG/DXF ohne ausschnitt: Bereich = Zeichnung und eingepasste Familie zusammen, plus Rand (Nachtrag Hauptlauf 18.10.2026)
            bool vereinigen = u.Art is "dwg" or "dxf" && ausschnitt is null;
            var bg = UnterlagenAnsicht.Erzeugen(p, u, seite, ausschnitt, vereinigen ? UnterlagenAnsicht.MinBreite : breite);
            var b = bg.Bereich;
            (x0, y0, x1, y1) = yUnten ? (b[0], -b[3], b[2], -b[1]) : (b[0], b[1], b[2], b[3]);
            if (vereinigen)
            {
                double bx0 = x0, by0 = y0, bx1 = x1, by1 = y1;
                var pts = familie.SelectMany(s => new[] { abb.Hin(s.X1, s.Y1), abb.Hin(s.X2, s.Y2) })
                    .Concat(anschluesse.Select(x => { var l = FamilienMasse.InAnsicht(ansicht, x.Lage); return abb.Hin(l[0], l[1]); }));
                foreach (var (px, py) in pts) { x0 = Math.Min(x0, px); x1 = Math.Max(x1, px); y0 = Math.Min(y0, py); y1 = Math.Max(y1, py); }
                double rand = Math.Max(0.04, 50.0 / breite) * Math.Max(x1 - x0, y1 - y0);   // 4 %, mindestens 50 Bildpunkte (Anschlussmarken, Namen)
                x0 -= rand; y0 -= rand; x1 += rand; y1 += rand;
                y1 += KopfPx * (x1 - x0) / breite;   // oben Platz für die Beschriftung
                w = breite;
                h = Math.Clamp((int)Math.Round(w * (y1 - y0) / (x1 - x0)), 50, UnterlagenAnsicht.MaxBreite);
                double s = w / (x1 - x0), t = h / (y1 - y0);
                bg = UnterlagenAnsicht.Erzeugen(p, u, seite, null, Math.Clamp((int)Math.Round((bx1 - bx0) * s), UnterlagenAnsicht.MinBreite, UnterlagenAnsicht.MaxBreite));
                b = bg.Bereich;
                bildFeld = ((b[0] - x0) * s, (y1 - b[3]) * t, (b[2] - b[0]) * s, (b[3] - b[1]) * t);
            }
            else { w = bg.Breite; h = bg.Hoehe; }
            bild = bg.Bild;
            hinweise.AddRange(bg.Hinweise);
        }
        else { w = breite; h = 0; }   // Bereich nach dem Einpassen
        if (d3)
        {
            var pts = umriss.SelectMany(r => r).Concat(familie.SelectMany(s => new[] { abb.Hin(s.X1, s.Y1), abb.Hin(s.X2, s.Y2) })).ToList();
            if (pts.Count == 0) pts = [(0, 0), (100, 100)];
            x0 = pts.Min(x => x.X); x1 = pts.Max(x => x.X); y0 = pts.Min(x => x.Y); y1 = pts.Max(x => x.Y);
            double rand = Math.Max(10, 0.06 * Math.Max(x1 - x0, y1 - y0));
            x0 -= rand; y0 -= rand; x1 += rand; y1 += rand;
            h = Math.Clamp((int)Math.Round(w * (y1 - y0) / (x1 - x0)), 50, UnterlagenAnsicht.MaxBreite);
        }
        if (massstaeblich && u.Art is "dwg" or "dxf" && Math.Abs(abb.Massstab - 1) > 0.01)
            hinweise.Add($"Massstab der Einpassung {abb.Massstab:0.####} Zeichnungseinheiten je mm: Zeichnung nicht in mm oder nicht 1:1 – der geometrische Vergleich rechnet in mm der Familie.");

        // Zeichnen
        double sx = w / (x1 - x0), sy = h / (y1 - y0);
        (double X, double Y) Seite(double x, double y) => ((x - x0) * sx, (y1 - y) * sy);
        png = Zeichnen(w, h, bild, bildFeld, umriss, familie, anschluesse, ansicht, abb, Seite, $"{fam.Kennzeichen} «{fam.Name}», Typ {typ}, Ansicht {ansicht} – rot: Familie",
            massstaeblich ? null : "schematisch, Überlagerung nur Anschauung");

        // Abweichungsliste und geometrischer Vergleich
        var masse = FamilienMasse.Vergleichen(f, def, q, typ);
        var o = new JsonObject
        {
            ["familie"] = fam.Kennzeichen, ["name"] = fam.Name, ["typ"] = typ, ["ansicht"] = ansicht, ["detailgrad"] = detail,
            ["unterlage"] = u.Anzeige, ["datei"] = u.Original, ["art"] = u.Art, ["seite"] = u.Art == "pdf" ? seite : null,
            ["mime"] = "image/png", ["breite"] = w, ["hoehe"] = h, ["bytes"] = png.Length,
            ["bereich"] = new JsonArray(Math.Round(x0, 3), Math.Round(yUnten ? -y1 : y0, 3), Math.Round(x1, 3), Math.Round(yUnten ? -y0 : y1, 3)),
            ["einpassung"] = new JsonObject
            {
                ["quelle"] = abb.Quelle, ["massstab"] = Math.Round(abb.Massstab, 6), ["drehung"] = Math.Round(abb.Drehung, 4),
                ["einheit"] = d3 ? "mm des Modells je mm der Familie" : u.Art == "pdf" ? "pt je mm der Familie" : u.Art == "bild" ? "Bildpunkte je mm der Familie" : "Zeichnungseinheiten je mm der Familie",
            },
            ["massstaeblich"] = massstaeblich,
            ["abweichungen"] = new JsonArray(masse.Select(z => (JsonNode)z.AlsJson()).ToArray()),
            ["masseBestanden"] = masse.All(z => z.Bestanden != false),
        };
        if (!massstaeblich) o["hinweis"] = "schematisch, Überlagerung nur Anschauung: Massbilder in Datenblättern sind nicht massstäblich – massgebend ist die Abweichungsliste (Werte je Typ).";
        if (massstaeblich) o["geometrisch"] = Geometrisch(f, a, def, u, q, abb, familie, umriss, d3, detail, ausschnitt, yUnten, hinweise);
        if (hinweise.Count > 0) o["hinweise"] = new JsonArray(hinweise.Distinct().Select(x => (JsonNode)JsonValue.Create(x)).ToArray());
        return o;
    }

    // ---------- Familie: Kanten der Plangrafik ----------

    static List<Strecke> Kanten(Plangrafik g)
    {
        var l = new List<Strecke>();
        void Zug(IReadOnlyList<double[]> pts, bool zu)
        {
            for (int i = 0; i + 1 < pts.Count; i++) l.Add(new Strecke(pts[i][0], pts[i][1], pts[i + 1][0], pts[i + 1][1]));
            if (zu && pts.Count > 2) l.Add(new Strecke(pts[^1][0], pts[^1][1], pts[0][0], pts[0][1]));
        }
        foreach (var t in g.Teile)
            foreach (var pr in t.Primitive)
            {
                if (pr.Stil.NurBildschirm || pr.Art is "text" or "bild") continue;
                switch (pr.Art)
                {
                    case "linie" or "polylinie": Zug(pr.Punkte, false); break;
                    case "polygon":
                        Zug(pr.Punkte, true);
                        foreach (var lo in pr.Loecher ?? []) Zug(lo, true);
                        break;
                    case "kreis" or "bogen":
                    {
                        if (pr.Punkte.Count == 0 || pr.Radius is not double r) break;
                        double von = pr.Art == "kreis" ? 0 : pr.Von ?? 0, bis = pr.Art == "kreis" ? 360 : pr.Bis ?? 360;
                        if (bis <= von) bis += 360;
                        int n = Math.Max(2, (int)Math.Ceiling((bis - von) / 7.5));
                        var pts = Enumerable.Range(0, n + 1).Select(i => { double wi = (von + (bis - von) * i / n) * Math.PI / 180; return new[] { pr.Punkte[0][0] + r * Math.Cos(wi), pr.Punkte[0][1] + r * Math.Sin(wi) }; }).ToList();
                        Zug(pts, false);
                        break;
                    }
                }
            }
        return l;
    }

    // ---------- Einpassen ----------

    static Abb? Passpunkte(JsonObject par, bool yUnten)
    {
        if (par["passpunkte"] is null) return null;
        if (par["passpunkte"] is not JsonArray pp || pp.Count != 2)
            throw new HlksFehler("passpunkte müssen genau zwei Paare sein.", "[{ \"familie\": [u1, v1], \"unterlage\": [x1, y1] }, { \"familie\": [u2, v2], \"unterlage\": [x2, y2] }]");
        var paare = new List<((double, double) F, (double, double) U)>();
        foreach (var x in pp)
        {
            JsonNode? fn, un;
            if (x is JsonObject o) { fn = o["familie"]; un = o["unterlage"]; }
            else if (x is JsonArray a2 && a2.Count == 2) { fn = a2[0]; un = a2[1]; }
            else throw new HlksFehler("Ein Passpunkt ist weder { familie, unterlage } noch [[u, v], [x, y]].", "[{ \"familie\": [u, v], \"unterlage\": [x, y] }, …]");
            (double, double) P(JsonNode? n, string was) => n is JsonArray k && k.Count >= 2 && Json.Zahl(k[0]) is double a && Json.Zahl(k[1]) is double b ? (a, b)
                : throw new HlksFehler($"Passpunkt: «{was}» muss [x, y] mit zwei Zahlen sein.", "[{ \"familie\": [u, v], \"unterlage\": [x, y] }, …]");
            var fp = P(fn, "familie"); var up = P(un, "unterlage");
            if (yUnten) up = (up.Item1, -up.Item2);
            paare.Add((fp, up));
        }
        return Abb.Aus(paare[0].F, paare[0].U, paare[1].F, paare[1].U, "passpunkte")
            ?? throw new HlksFehler("Die beiden Passpunkte fallen zusammen.", "Zwei verschiedene Punkte wählen, möglichst weit auseinander.");
    }

    static Abb? MassEinpassen(FamilieDef f, JsonObject def, FamAufloesung a, string ansicht, UnterlageRef u, int seite, BefundQuelle q)
    {
        var quellen = FamilienMasse.MassQuellen(def);
        Abb? beste = null;
        for (int i = 0; i < f.Masse.Count && i < quellen.Count; i++)
        {
            var m = f.Masse[i];
            if (m.Von is null || m.Bis is null || (m.Ansicht is not null && m.Ansicht != ansicht)) continue;
            foreach (var id in quellen[i])
            {
                if (BefundQuelle.Praefix(id) != u.Pruefsumme) continue;
                var b = q.Hole(id, out _);
                if (b is not { Art: "mass", Lage.Count: >= 2 }) continue;
                if (u.Art == "pdf" && b.Seite != seite) continue;
                double[] von, bis;
                try { von = FamilienMasse.Auswerten(m.Von, a); bis = FamilienMasse.Auswerten(m.Bis, a); }
                catch (FormelFehler) { continue; }
                var fv = FamilienMasse.InAnsicht(ansicht, von); var fb = FamilienMasse.InAnsicht(ansicht, bis);
                (double, double) g1 = (b.Lage![0][0], b.Lage[0][1]), g2 = (b.Lage[1][0], b.Lage[1][1]);
                var quelle = $"Massbezug «{m.Name}» ↔ {b.Art} {id}";
                var k1 = Abb.Aus((fv[0], fv[1]), g1, (fb[0], fb[1]), g2, quelle);
                var k2 = Abb.Aus((fv[0], fv[1]), g2, (fb[0], fb[1]), g1, quelle);
                var k = k1 is null ? k2 : k2 is null ? k1 : Math.Abs(k1.Drehung) <= Math.Abs(k2.Drehung) ? k1 : k2;
                if (k is not null && (beste is null || Math.Abs(k.Drehung) < Math.Abs(beste.Drehung))) beste = k;
            }
        }
        return beste;
    }

    static Abb? HuelleEinpassen(List<Strecke> familie, List<List<(double X, double Y)>> umriss)
    {
        if (familie.Count == 0 || umriss.Count == 0) return null;
        double fx = familie.Min(s => Math.Min(s.X1, s.X2)), fy = familie.Min(s => Math.Min(s.Y1, s.Y2));
        double ux = umriss.SelectMany(r => r).Min(p => p.X), uy = umriss.SelectMany(r => r).Min(p => p.Y);
        return new Abb(1, 0, ux - fx, uy - fy, "Hülle (linke untere Ecke, Massstab 1)");
    }

    /// <summary>Umriss des 3D-Modells in der Ansicht (mm): Befund «ansicht», sonst die Hülle von form3d.</summary>
    static List<List<(double X, double Y)>> Umriss3d(BefundQuelle q, UnterlageRef u, string ansicht, List<string> hinweise)
    {
        var alle = q.Alle(u.Pruefsumme);
        var l = new List<List<(double X, double Y)>>();
        var an = alle.FirstOrDefault(b => b.Art == "ansicht" && T(b.Wert?["ansicht"]) == ansicht);
        bool drehen = ansicht == "unten";   // 3D-Befund «unten»: u = x, v = −y; Familie «unten»: u = −x, v = y
        if (an is not null)
        {
            foreach (var k in an.Wert?["konturen"] as JsonArray ?? [])
            {
                void Ring(JsonNode? n)
                {
                    var r = (n as JsonArray ?? []).OfType<JsonArray>().Where(p => p.Count >= 2).Select(p => (X: Json.Zahl(p[0]) ?? 0, Y: Json.Zahl(p[1]) ?? 0))
                        .Select(p => drehen ? (-p.X, -p.Y) : p).ToList();
                    if (r.Count >= 2) { r.Add(r[0]); l.Add(r); }
                }
                Ring(k?["aussen"]);
                foreach (var lo in k?["loecher"] as JsonArray ?? []) Ring(lo);
            }
            return l;
        }
        var form = alle.FirstOrDefault(b => b.Art == "form3d");
        if (form?.Wert?["huelle"] is JsonObject hu && hu["min"] is JsonArray mi && hu["max"] is JsonArray ma)
        {
            var lo = mi.Select(x => Json.Zahl(x) ?? 0).ToArray(); var hi = ma.Select(x => Json.Zahl(x) ?? 0).ToArray();
            var pts = new List<double[]>();
            foreach (var x in new[] { lo[0], hi[0] }) foreach (var y in new[] { lo[1], hi[1] }) foreach (var z in new[] { lo[2], hi[2] }) pts.Add(FamilienMasse.InAnsicht(ansicht, [x, y, z]));
            double a0 = pts.Min(p => p[0]), a1 = pts.Max(p => p[0]), b0 = pts.Min(p => p[1]), b1 = pts.Max(p => p[1]);
            l.Add([(a0, b0), (a1, b0), (a1, b1), (a0, b1), (a0, b0)]);
            hinweise.Add("Das 3D-Modell hat keinen Umriss-Befund (ansicht): gezeichnet ist seine Hülle.");
        }
        else hinweise.Add("Das 3D-Modell hat weder Umriss (ansicht) noch Hülle (form3d): nur die Familie ist gezeichnet.");
        return l;
    }

    // ---------- Zeichnen ----------

    /// <summary>Höhe der Beschriftung oben im Bild (Bildpunkte), für die ein DWG/DXF-Ausschnitt Platz lässt.</summary>
    const double KopfPx = 30;

    static byte[] Zeichnen(int w, int h, byte[]? bild, (double X, double Y, double B, double H)? bildFeld, List<List<(double X, double Y)>> umriss,
        List<Strecke> familie, List<FamAnschlussErgebnis> anschluesse, string ansicht, Abb abb, Func<double, double, (double X, double Y)> seite, string titel, string? schematisch)
    {
        byte[] pdf;
        using (var doc = new PdfDocument())
        {
            var page = doc.AddPage();
            page.Width = XUnit.FromPoint(w); page.Height = XUnit.FromPoint(h);
            MemoryStream? strom = null;
            try
            {
                using (var g = XGraphics.FromPdfPage(page))
                {
                    Schriften.Sicherstellen();
                    g.DrawRectangle(XBrushes.White, 0, 0, w, h);
                    if (bild is not null)
                    {
                        strom = new MemoryStream(bild);
                        var (bx, by, bb0, bh0) = bildFeld ?? (0, 0, w, h);
                        g.DrawImage(XImage.FromStream(strom), bx, by, bb0, bh0);
                    }
                    var schwarz = new XPen(XColors.Black, 0.8);
                    foreach (var r in umriss)
                        for (int i = 0; i + 1 < r.Count; i++)
                        {
                            var p1 = seite(r[i].X, r[i].Y); var p2 = seite(r[i + 1].X, r[i + 1].Y);
                            g.DrawLine(schwarz, p1.X, p1.Y, p2.X, p2.Y);
                        }
                    var rot = new XPen(XColor.FromArgb(220, 0, 0), 1.4);
                    foreach (var s in familie)
                    {
                        var a1 = abb.Hin(s.X1, s.Y1); var a2 = abb.Hin(s.X2, s.Y2);
                        var p1 = seite(a1.X, a1.Y); var p2 = seite(a2.X, a2.Y);
                        g.DrawLine(rot, p1.X, p1.Y, p2.X, p2.Y);
                    }
                    var font = new XFont("Arial", 10);
                    var rotPinsel = new XSolidBrush(XColor.FromArgb(220, 0, 0));
                    foreach (var x in anschluesse)
                    {
                        var l = FamilienMasse.InAnsicht(ansicht, x.Lage); var r = FamilienMasse.InAnsicht(ansicht, x.Richtung);
                        var a1 = abb.Hin(l[0], l[1]); var p1 = seite(a1.X, a1.Y);
                        g.DrawEllipse(rot, p1.X - 4, p1.Y - 4, 8, 8);
                        double rl = Math.Sqrt(r[0] * r[0] + r[1] * r[1]);
                        if (rl > 0.1)
                        {
                            var rr = abb.Hin(l[0] + r[0] / rl, l[1] + r[1] / rl); var pr = seite(rr.X, rr.Y);
                            double dx = pr.X - p1.X, dy = pr.Y - p1.Y, dl = Math.Sqrt(dx * dx + dy * dy);
                            if (dl > 1e-9) g.DrawLine(rot, p1.X, p1.Y, p1.X + dx / dl * 18, p1.Y + dy / dl * 18);
                        }
                        g.DrawString(x.Name, font, rotPinsel, p1.X + 6, p1.Y - 6);
                    }
                    var kopf = new XFont("Arial", 11);
                    var zeilen = schematisch is null ? new[] { titel } : [titel, schematisch];
                    double bh = 16 * zeilen.Length + 6, bb = Math.Min(w - 4, 8 + zeilen.Max(z => g.MeasureString(z, kopf).Width));
                    g.DrawRectangle(new XSolidBrush(XColor.FromArgb(225, 255, 255, 255)), 2, 2, bb, bh);
                    for (int i = 0; i < zeilen.Length; i++) g.DrawString(zeilen[i], kopf, i == 0 ? rotPinsel : XBrushes.Black, 6, 16 + 16 * i);
                }
                using var ms = new MemoryStream();
                doc.Save(ms, false);
                pdf = ms.ToArray();
            }
            finally { strom?.Dispose(); }
        }
        var (bw, bh2, bgra) = PdfSeiteBild.Rendern(null, pdf, 1, 1.0);
        var temp = Path.Combine(Path.GetTempPath(), $"hlks_kontrollbild_{Guid.NewGuid():N}.png");
        try
        {
            PngSchreiber.Schreiben(temp, bw, bh2, bgra, 96);
            return File.ReadAllBytes(temp);
        }
        finally { try { File.Delete(temp); } catch (IOException) { } }
    }

    // ---------- Geometrischer Vergleich (massstäbliche Unterlagen) ----------

    static double Abstand(double u, double v, List<Strecke> kanten)
    {
        double best = double.PositiveInfinity;
        foreach (var s in kanten)
        {
            double dx = s.X2 - s.X1, dy = s.Y2 - s.Y1, l2 = dx * dx + dy * dy;
            double t = l2 < 1e-18 ? 0 : Math.Clamp(((u - s.X1) * dx + (v - s.Y1) * dy) / l2, 0, 1);
            double px = s.X1 + t * dx - u, py = s.Y1 + t * dy - v;
            best = Math.Min(best, px * px + py * py);
        }
        return Math.Sqrt(best);
    }

    static List<Strecke> BefundStrecken(UnterlagenBefund b)
    {
        var l = new List<Strecke>();
        switch (b.Art)
        {
            case "linie" or "polylinie":
                for (int i = 0; b.Lage is not null && i + 1 < b.Lage.Count; i++) l.Add(new Strecke(b.Lage[i][0], b.Lage[i][1], b.Lage[i + 1][0], b.Lage[i + 1][1]));
                if (b.Zusatz?["geschlossen"] is JsonValue gv && gv.TryGetValue<bool>(out var g) && g && b.Lage is { Count: > 2 })
                    l.Add(new Strecke(b.Lage[^1][0], b.Lage[^1][1], b.Lage[0][0], b.Lage[0][1]));
                break;
            case "bogen" or "kreis":
            {
                var m = b.Wert?["mitte"] as JsonArray;
                if (m is null || Json.Zahl(b.Wert?["radius"]) is not double r) break;
                double mx = Json.Zahl(m[0]) ?? 0, my = Json.Zahl(m[1]) ?? 0;
                double von = b.Art == "kreis" ? 0 : Json.Zahl(b.Wert?["von"]) ?? 0, bis = b.Art == "kreis" ? 360 : Json.Zahl(b.Wert?["bis"]) ?? 360;
                if (bis <= von) bis += 360;
                int n = Math.Max(2, (int)Math.Ceiling((bis - von) / 7.5));
                double px = 0, py = 0;
                for (int i = 0; i <= n; i++)
                {
                    double wi = (von + (bis - von) * i / n) * Math.PI / 180, x = mx + r * Math.Cos(wi), y = my + r * Math.Sin(wi);
                    if (i > 0) l.Add(new Strecke(px, py, x, y));
                    px = x; py = y;
                }
                break;
            }
        }
        return l;
    }

    static bool KeineKante(string? layer)
    {
        if (layer is null) return false;
        var l = layer.ToLowerInvariant();
        return l.Contains("mass") || l.Contains("bem") || l.Contains("dim") || l.Contains("text");
    }

    static JsonObject Geometrisch(FamilieDef f, FamAufloesung a, JsonObject def, UnterlageRef u, BefundQuelle q, Abb abb, List<Strecke> familie,
        List<List<(double X, double Y)>> umriss, bool d3, string detail, double[]? ausschnitt, bool yUnten, List<string> hinweise)
    {
        var o = new JsonObject { ["einheit"] = "mm (Familie)" };
        double s = abb.Massstab;
        // Bereich der Kanten
        double bx0, by0, bx1, by1;
        if (ausschnitt is not null)
        {
            bx0 = Math.Min(ausschnitt[0], ausschnitt[2]); bx1 = Math.Max(ausschnitt[0], ausschnitt[2]);
            by0 = Math.Min(ausschnitt[1], ausschnitt[3]); by1 = Math.Max(ausschnitt[1], ausschnitt[3]);
        }
        else
        {
            var pts = familie.SelectMany(k => new[] { abb.Hin(k.X1, k.Y1), abb.Hin(k.X2, k.Y2) }).ToList();
            if (pts.Count == 0) { o["hinweis"] = "Keine Familienkanten in dieser Ansicht."; return o; }
            bx0 = pts.Min(p => p.X); bx1 = pts.Max(p => p.X); by0 = pts.Min(p => p.Y); by1 = pts.Max(p => p.Y);
            double rand = Math.Max(10 * s, 0.1 * Math.Max(bx1 - bx0, by1 - by0));
            bx0 -= rand; by0 -= rand; bx1 += rand; by1 += rand;
        }
        bool ImBereich(Strecke k) => Math.Max(k.X1, k.X2) >= bx0 && Math.Min(k.X1, k.X2) <= bx1 && Math.Max(k.Y1, k.Y2) >= by0 && Math.Min(k.Y1, k.Y2) <= by1;

        // Zeichnungskanten
        var zeichnung = new List<(string Id, string? Layer, List<Strecke> Strecken)>();
        if (d3)
        {
            int n = 0;
            foreach (var r in umriss)
            {
                n++;
                var st = new List<Strecke>();
                for (int i = 0; i + 1 < r.Count; i++) st.Add(new Strecke(r[i].X, r[i].Y, r[i + 1].X, r[i + 1].Y));
                foreach (var k in st) zeichnung.Add(($"umriss {n}", null, [k]));
            }
        }
        else
        {
            var alle = q.Alle(u.Pruefsumme);
            var massLayer = alle.Where(b => b.Art == "mass" && b.Layer is not null).Select(b => b.Layer!).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var b in alle.Where(b => b.Art is "linie" or "polylinie" or "bogen" or "kreis" && (b.Layout ?? "Model") == "Model"))
            {
                if (b.Layer is not null && (massLayer.Contains(b.Layer) || KeineKante(b.Layer))) continue;
                var st = BefundStrecken(b);
                if (st.Count == 0 || !st.Any(ImBereich)) continue;
                if (b.Art is "linie" or "polylinie") foreach (var k in st) zeichnung.Add((b.Id, b.Layer, [k]));
                else zeichnung.Add((b.Id, b.Layer, st));
            }
        }
        var kanten = new List<(string Id, string? Layer, double Laenge, double Abstand, double Tol, bool Ok)>();
        foreach (var (id, layer, st) in zeichnung)
        {
            double laenge = st.Sum(k => k.Laenge) / s;
            double max = 0;
            foreach (var k in st)
            {
                int n = Math.Clamp((int)Math.Ceiling(k.Laenge / s / 5), 1, 20);
                for (int i = 0; i <= n; i++)
                {
                    double t = (double)i / n;
                    var (uu, vv) = abb.Zurueck(k.X1 + t * (k.X2 - k.X1), k.Y1 + t * (k.Y2 - k.Y1));
                    max = Math.Max(max, Abstand(uu, vv, familie));
                }
            }
            double tol = Math.Max(1, 0.005 * laenge);
            kanten.Add((id, layer, laenge, max, tol, max <= tol + 1e-9));
        }
        var ausserhalb = kanten.Where(k => !k.Ok).OrderByDescending(k => k.Abstand - k.Tol).ToList();
        o["kanten"] = new JsonObject
        {
            ["anzahl"] = kanten.Count, ["bestanden"] = kanten.Count(k => k.Ok), ["ausserhalb"] = ausserhalb.Count,
            ["groessterAbstand"] = kanten.Count == 0 ? null : Math.Round(kanten.Max(k => k.Abstand), 3),
            ["liste"] = new JsonArray(ausserhalb.Take(40).Concat(kanten.Where(k => k.Ok).OrderByDescending(k => k.Abstand).Take(Math.Max(0, 20 - Math.Min(20, ausserhalb.Count))))
                .Select(k => (JsonNode)new JsonObject
                {
                    ["befund"] = k.Id, ["layer"] = k.Layer, ["laenge"] = Math.Round(k.Laenge, 2), ["abstand"] = Math.Round(k.Abstand, 3),
                    ["toleranz"] = Math.Round(k.Tol, 3), ["bestanden"] = k.Ok,
                }).ToArray()),
        };
        if (kanten.Count == 0) hinweise.Add("Im Bereich der Familie liegen keine Zeichnungskanten (ausschnitt prüfen).");

        // Masse der Zeichnung: liegen die Definitionspunkte auf Kanten der Familie?
        if (!d3)
        {
            var mr = new JsonArray();
            foreach (var b in q.Alle(u.Pruefsumme).Where(b => b.Art == "mass" && b.Lage is { Count: >= 2 } && (b.Layout ?? "Model") == "Model"))
            {
                var p1 = b.Lage![0]; var p2 = b.Lage[1];
                if (!ImBereich(new Strecke(p1[0], p1[1], p2[0], p2[1]))) continue;
                var w = Json.Zahl(b.Wert) ?? 0;
                double tol = Math.Max(1, 0.005 * Math.Abs(w / s));
                var (u1, v1) = abb.Zurueck(p1[0], p1[1]); var (u2, v2) = abb.Zurueck(p2[0], p2[1]);
                double d1 = Abstand(u1, v1, familie), d2 = Abstand(u2, v2, familie);
                mr.Add(new JsonObject
                {
                    ["befund"] = b.Id, ["wert"] = w, ["einheit"] = b.Einheit, ["abstandPunkt1"] = Math.Round(d1, 3), ["abstandPunkt2"] = Math.Round(d2, 3),
                    ["toleranz"] = Math.Round(tol, 3), ["bestanden"] = d1 <= tol + 1e-9 && d2 <= tol + 1e-9,
                });
            }
            o["masse"] = mr;
        }
        else
        {
            // Anschlusslage gegen Stutzen bzw. Port (3D, Hüllen-Ecke aufeinander)
            var alle = q.Alle(u.Pruefsumme);
            var form = alle.FirstOrDefault(b => b.Art == "form3d");
            var teile = a.Teile(detail);
            var ziele = new List<(string Id, string Art, double[] P)>();
            foreach (var b in alle)
            {
                if (b.Art == "stutzen" && b.Wert?["mitte"] is JsonArray mi && mi.Count == 3) ziele.Add((b.Id, "stutzen", mi.Select(x => Json.Zahl(x) ?? 0).ToArray()));
                if (b.Art == "port" && b.Wert?["lage"] is JsonArray la && la.Count == 3) ziele.Add((b.Id, "port", la.Select(x => Json.Zahl(x) ?? 0).ToArray()));
            }
            var ar = new JsonArray();
            if (form?.Wert?["huelle"]?["min"] is JsonArray hmin && teile.Count > 0 && ziele.Count > 0)
            {
                var fmin = new[] { double.MaxValue, double.MaxValue, double.MaxValue };
                foreach (var t in teile) { var ad = FamilienKoerper.AlsNetz(t).Ausdehnung(); for (int k = 0; k < 3; k++) fmin[k] = Math.Min(fmin[k], ad[k]); }
                var mmin = hmin.Select(x => Json.Zahl(x) ?? 0).ToArray();
                var verschiebung = Enumerable.Range(0, 3).Select(k => mmin[k] - fmin[k]).ToArray();
                foreach (var x in a.Anschluesse.Where(x => x.Sichtbar))
                {
                    var pos = Enumerable.Range(0, 3).Select(k => x.Lage[k] + verschiebung[k]).ToArray();
                    var naechst = ziele.OrderBy(z => Dist3(z.P, pos)).First();
                    double d = Dist3(naechst.P, pos);
                    ar.Add(new JsonObject
                    {
                        ["anschluss"] = x.Name, ["befund"] = naechst.Id, ["art"] = naechst.Art, ["abstand"] = Math.Round(d, 3), ["toleranz"] = 2.0, ["bestanden"] = d <= 2.0 + 1e-9,
                    });
                }
                o["verschiebung3d"] = new JsonArray(verschiebung.Select(v => (JsonNode)Math.Round(v, 3)).ToArray());
            }
            else if (ziele.Count == 0) hinweise.Add("Das 3D-Modell hat keine Stutzen bzw. Ports: Anschlusslagen nicht verglichen.");
            o["anschluesse"] = ar;
        }
        return o;
    }

    static double Dist3(double[] a, double[] b) => Math.Sqrt((a[0] - b[0]) * (a[0] - b[0]) + (a[1] - b[1]) * (a[1] - b[1]) + (a[2] - b[2]) * (a[2] - b[2]));
}
