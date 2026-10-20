// Einstieg in die Grafik des Kerns: Plangrafik einer Ansicht (Grundriss, Schnitt, Ansicht) und Körper für 3D.
// Die Grafik eines Elements liefert seine Grafikquelle; je Bereich eine Quelle in einer eigenen Datei, damit die
// Bereiche unabhängig gebaut und getestet werden können:
//   ArchitekturGrafik  Wand, Tür, Fenster, Decke, Dach, Raum, Achse (Wandanschlüsse und Öffnungen bereinigt)
//   HlksGrafik         Leitungen, Formteile, Armaturen, Apparate, Geräte (alles mit Anschlüssen)
//   BeschriftungGrafik Detaillinie, Füllbereich, Text, Bemassung, Kote, Symbol; Schnittsymbol der Schnittansichten
//   HuelleGrafik       Darstellung «Hülle» im Grundriss (Streifen nach Angrenzung; nur gemeinsame Grafik, nur Bildschirm)
//   AussparungGrafik   Aussparungen (Modul 3b, B4a): Symbol nach Status, Darstellung «aussparungen» = Aussparungsplan, Körper für 3D
//   KollisionGrafik    Kollisionsmarken (Modul 3b, B4a): Kreis mit Kreuz, nur Bildschirm; Kugel für 3D
//   EinlageGrafik      Darstellungen «einlagen» und «grundleitungen» (Modul 3, B10) und der Grundriss-Filter für Einlagen und Grundleitungen
//                      (für kein Element zuständig; Weiche ImPlan und Sichtbarkeit wie der Aussparungsplan)
// Schnitt und Ansicht entstehen aus den Körpern (Schnittgrafik), Grundrisse aus den Quellen direkt.
// Sichtbarkeit: dieselben Regeln wie js/ansichten/filter.js (Gewerke, ausgeblendete Klassen, Geschoss).

using System.Globalization;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

public interface IGrafikquelle
{
    string Name { get; }
    /// <summary>Ist diese Quelle für das Element zuständig? Die erste zuständige Quelle zeichnet.</summary>
    bool Zustaendig(Katalog k, Element e);
    /// <summary>Einmal je Plangrafik vor den Elementen, z. B. Wandumrisse eines Geschosses vereinigen (in ctx.Ablage).</summary>
    void Vorbereiten(GrafikKontext ctx) { }
    /// <summary>Gemeinsame Grafik ohne eigenes Element, z. B. der bereinigte Umriss aller geschnittenen Wände.</summary>
    IEnumerable<Grafikteil> Gemeinsam(GrafikKontext ctx) => [];
    /// <summary>2D-Grafik eines Elements in der Ansicht (Grundriss; Beschriftung auch in Schnitt/Ansicht).</summary>
    IEnumerable<Grafikteil> Plan(GrafikKontext ctx, Element e);
    /// <summary>3D-Körper eines Elements oder null, wenn es keinen hat.</summary>
    Koerper? Koerper(Projekt p, Element e);
}

/// <summary>Alles, was eine Quelle über die Ansicht wissen muss.</summary>
public sealed class GrafikKontext
{
    public required Projekt Projekt { get; init; }
    public Modell Modell => Projekt.Modell;
    public Katalog Katalog => Projekt.Katalog;
    public required Element Ansicht { get; init; }
    /// <summary>grundriss | schnitt | ansicht</summary>
    public required string Art { get; init; }
    public double Massstab { get; init; } = 50;
    /// <summary>grob | mittel | fein</summary>
    public string Detailgrad { get; init; } = "mittel";
    /// <summary>Grundriss: Geschoss der Ansicht (Element) oder null.</summary>
    public Element? Geschoss { get; init; }
    /// <summary>Grundriss: Schnitthöhe absolut (z über ±0.00) = Geschosskote + Schnitthöhe der Ansicht.</summary>
    public double Schnitthoehe { get; init; }
    /// <summary>Gewerke, die gezeigt werden; null = alle.</summary>
    public IReadOnlySet<string>? Gewerke { get; init; }
    public IReadOnlySet<string> KlassenAus { get; init; } = new HashSet<string>();
    /// <summary>Zwischenergebnisse aus Vorbereiten, je Quelle unter ihrem Namen.</summary>
    public Dictionary<string, object> Ablage { get; } = [];

    /// <summary>Papier-mm in Modell-mm (z. B. 2.5 mm Schrift bei 1:50 = 125 mm im Modell).</summary>
    public double AufModell(double papierMm) => papierMm * Massstab;
}

public static class Grafik
{
    /// <summary>Alle Quellen; die erste zuständige zeichnet ein Element.</summary>
    // Die Unterlagen stehen vor HlksGrafik, weil diese als letzte Quelle für alles Übrige zuständig ist (Befund B3-1).
    // FamilienGrafik (Modul 2) zeichnet die familienfähigen Elemente mit bezuege.familie und steht deshalb ebenfalls vor
    // HlksGrafik (ihre Elemente sind von denen der Architektur und der Unterlagen verschieden).
    public static readonly IReadOnlyList<IGrafikquelle> Quellen = [new BeschriftungGrafik(), new ArchitekturGrafik(), new FamilienGrafik(), new EinlageGrafik(), new GrundlagenGrafik(), new HuelleGrafik(), new AussparungGrafik(), new KollisionGrafik(), new HlksGrafik()];

    static readonly HashSet<string> NieGrafisch = ["ansicht", "funktion"];
    static readonly HashSet<string> Gliederung = ["Gebaeude", "Geschoss", "Anlage", "System", "Strang", "Zone"];

    /// <summary>Plangrafik einer Ansicht (Kennzeichen oder Id). Grundriss, Schnitt, Ansicht; Schema (Modul 3) über SchemaGrafik.Plan in Papier-mm, Massstab 1.</summary>
    public static Plangrafik Plan(Projekt p, string ansicht)
    {
        var a = p.Modell.HoleDerKlasse(ansicht, "Ansicht");
        if (a.Text("art") == "schema") return SchemaGrafik.Plan(p, a);   // Modul 3 (Schema/)
        if (a.Text("art") == "liste") return Listentabelle.Plan(p, a);   // Modul 7 Teil A: Liste als Tabelle auf dem Planblatt (Papier-mm, Massstab 1)
        var ctx = Kontext(p, a);
        var g = new Plangrafik { Ansicht = a.Kennzeichen, Art = ctx.Art, Massstab = ctx.Massstab, Stand = p.Modell.Pruefsumme() };
        foreach (var q in Quellen) q.Vorbereiten(ctx);
        if (ctx.Art == "grundriss")
        {
            bool einlagen = EinlageGrafik.Aktiv(ctx);
            foreach (var q in Quellen) g.Teile.AddRange(einlagen ? EinlageGrafik.GemeinsamImPlan(q, q.Gemeinsam(ctx)) : q.Gemeinsam(ctx));
            foreach (var e in p.Modell.Elemente)
                if (Sichtbar(ctx, e) && QuelleFuer(p.Katalog, e) is { } q)
                    g.Teile.AddRange(Zeichnen(ctx, q, e));
        }
        else
        {
            g.Teile.AddRange(Schnittgrafik.Erzeugen(ctx));
            foreach (var e in p.Modell.Elemente)
                if (IstBeschriftungVon(p.Katalog, e, a) && QuelleFuer(p.Katalog, e) is { } q) g.Teile.AddRange(q.Plan(ctx, e));
        }
        g.Begrenzung = Begrenzung(g.Teile);
        return g;
    }

    /// <summary>Körper der Elemente (alle grafischen oder nur die genannten).</summary>
    /// <summary>Weiche je Darstellung des Grundrisses: Aussparungsplan ohne Gebäudetechnik (Modul 3b, B4e), Einlage- bzw. Grundleitungsplan
    /// (Modul 3, B10: Hintergrund grau, nur die Elemente des Plans kräftig), sonst die Quelle selbst.</summary>
    static IEnumerable<Grafikteil> Zeichnen(GrafikKontext ctx, IGrafikquelle q, Element e) =>
        AussparungGrafik.Aktiv(ctx) ? AussparungGrafik.ImAussparungsplan(ctx, q, e)
        : EinlageGrafik.Aktiv(ctx) ? EinlageGrafik.ImPlan(ctx, q, e)
        : q.Plan(ctx, e);

    public static List<Koerper> Koerper(Projekt p, IEnumerable<string>? ids = null)
    {
        var elemente = ids is null ? p.Modell.Elemente : ids.Select(i => p.Modell.Finde(i)).OfType<Element>();
        var liste = new List<Koerper>();
        using var _ = Wandgeometrie.Zwischenspeichern(p.Modell, p.Katalog);   // Wandlagen einmal statt je Wand
        foreach (var e in elemente)
        {
            if (!IstModellgrafik(p.Katalog, e)) continue;
            if (QuelleFuer(p.Katalog, e)?.Koerper(p, e) is { Leer: false } k) liste.Add(k);
        }
        return liste;
    }

    public static IGrafikquelle? QuelleFuer(Katalog k, Element e) => Quellen.FirstOrDefault(q => q.Zustaendig(k, e));

    public static GrafikKontext Kontext(Projekt p, Element ansicht)
    {
        var art = ansicht.Text("art") ?? "grundriss";
        if (art is not ("grundriss" or "schnitt" or "ansicht"))
            throw new HlksFehler($"Ansicht {ansicht.Kennzeichen} ist eine {art}-Ansicht; Plangrafik gibt es für Grundriss, Schnitt und Ansicht.");
        var geschoss = p.Modell.Finde(ansicht.Geschoss);
        if (art == "grundriss" && geschoss is null)
            throw new HlksFehler($"Grundriss {ansicht.Kennzeichen} hat kein Geschoss.", "Geschoss der Ansicht setzen (element_aendern … geschoss).");
        if (art != "grundriss" && (ansicht.Geometrie is null || ansicht.Geometrie.Punkte.Count < 2))
            throw new HlksFehler($"{art} {ansicht.Kennzeichen} hat keine Schnittlinie.", "Geometrie { art: linie, punkte: [[x1,y1],[x2,y2]] } setzen.");
        var gewerke = Liste(ansicht, "gewerke");
        return new GrafikKontext
        {
            Projekt = p,
            Ansicht = ansicht,
            Art = art,
            Massstab = ansicht.Zahl("massstab") is double m && m > 0 ? m : 50,
            Detailgrad = ansicht.Text("detailgrad") ?? "mittel",
            Geschoss = geschoss,
            Schnitthoehe = (geschoss?.Zahl("kote") ?? 0) + (ansicht.Zahl("schnitthoehe") ?? 1000),
            Gewerke = gewerke.Count > 0 ? gewerke.ToHashSet() : null,
            KlassenAus = Liste(ansicht, "klassenAusblenden").ToHashSet(),
        };
    }

    /// <summary>Soll das Element in dieser Ansicht erscheinen? (wie filter.js; Beschriftung nur in ihrer Ansicht,
    /// Achsen in allen Grundrissen, Schnittlinien anderer Ansichten als Symbol in Grundrissen)</summary>
    public static bool Sichtbar(GrafikKontext ctx, Element e)
    {
        var k = ctx.Katalog;
        if (!k.Klassen.TryGetValue(e.Klasse, out var d) || e.Geometrie is null) return false;
        if (d.BrauchtAnsicht) return IstBeschriftungVon(k, e, ctx.Ansicht);
        if (e.Klasse == "Ansicht")
            return ctx.Art == "grundriss" && e.Id != ctx.Ansicht.Id && e.Text("art") is "schnitt" or "ansicht" && e.Geometrie.Punkte.Count >= 2;
        if (NieGrafisch.Contains(d.Schicht) || Gliederung.Contains(e.Klasse)) return false;
        if (ctx.KlassenAus.Contains(e.Klasse) || d.Linie.Any(ctx.KlassenAus.Contains)) return false;
        if (ctx.Gewerke is not null && d.Gewerk is { } gw && gw != "allgemein" && !ctx.Gewerke.Contains(gw)) return false;
        // Modul 3, B10: Einlagen und Grundleitungen nur im Einlage- bzw. Grundleitungsplan; dort nur die Elemente des Plans (EinlageGrafik)
        if (EinlageGrafik.Sichtbarkeit(ctx, e) is bool b) return b;
        // Achsen in allen Grundrissen; Unterlagen ohne Geschoss (IFC über mehrere Geschosse) ebenso, sie schneiden selbst
        bool ueberall = d.Schicht == "bezug" || (d.Schicht == "grundlage" && e.Geschoss is null);
        if (ctx.Art == "grundriss" && !ueberall && ctx.Geschoss is not null && e.Geschoss != ctx.Geschoss.Id) return false;
        return true;
    }

    /// <summary>Gehört das Element in 3D und in Schnitte (also keine Beschriftung, keine Ansicht, keine Gliederung)?</summary>
    public static bool IstModellgrafik(Katalog k, Element e) =>
        e.Geometrie is not null && k.Klassen.TryGetValue(e.Klasse, out var d) && !d.BrauchtAnsicht
        && !NieGrafisch.Contains(d.Schicht) && !Gliederung.Contains(e.Klasse);

    static bool IstBeschriftungVon(Katalog k, Element e, Element ansicht) =>
        k.Klassen.TryGetValue(e.Klasse, out var d) && d.BrauchtAnsicht && e.Bezuege.TryGetValue("ansicht", out var a) && a == ansicht.Id;

    public static List<string> Liste(Element e, string name) =>
        e.Eigenschaft(name)?.Inhalt is JsonArray a
            ? a.Select(x => x is JsonValue v && v.TryGetValue<string>(out var s) ? s : null).OfType<string>().ToList()
            : [];

    public static bool Bool(Element e, string name, bool vorgabe = false) =>
        e.Eigenschaft(name)?.Inhalt is JsonValue v && v.TryGetValue<bool>(out var b) ? b : vorgabe;

    /// <summary>[minx, miny, maxx, maxy] über alle Punkte (Kreise/Bögen mit Radius); null wenn leer.</summary>
    public static double[]? Begrenzung(IEnumerable<Grafikteil> teile)
    {
        double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
        void Dazu(double x, double y) { x0 = Math.Min(x0, x); y0 = Math.Min(y0, y); x1 = Math.Max(x1, x); y1 = Math.Max(y1, y); }
        foreach (var t in teile)
            foreach (var pr in t.Primitive)
            {
                if (pr.Art == "bogen" && pr.Radius is double rb && pr.Punkte.Count > 0 && pr.Von is double von && pr.Bis is double bis)
                {
                    // nur der Bogen selbst: Endpunkte und die Achsextreme (0°, 90°, 180°, 270°), die er überstreicht
                    var m = pr.Punkte[0];
                    double laenge = ((bis - von) % 360 + 360) % 360;
                    if (laenge == 0) laenge = 360;
                    foreach (var w in new[] { von, von + laenge }.Concat(Enumerable.Range(0, 8).Select(i => i * 90.0).Where(a => ((a - von) % 360 + 360) % 360 <= laenge)))
                        Dazu(m[0] + rb * Math.Cos(w * Math.PI / 180), m[1] + rb * Math.Sin(w * Math.PI / 180));
                    continue;
                }
                double r = pr.Art is "kreis" or "bogen" ? pr.Radius ?? 0 : 0;
                foreach (var pt in pr.Punkte) { Dazu(pt[0] - r, pt[1] - r); Dazu(pt[0] + r, pt[1] + r); }
            }
        return x0 == double.MaxValue ? null : [x0, y0, x1, y1];
    }

    /// <summary>Zahl für Texte auf dem Plan, unabhängig von der Spracheinstellung.</summary>
    public static string Format(double wert, int dezimalen) => wert.ToString("F" + dezimalen, CultureInfo.InvariantCulture);
}
