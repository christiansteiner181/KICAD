// Übertrag der Architektur aus IFC (Modul 1b, Auftrag B1, entwurf/modul-1b.md Abschnitt «B1»): Wand, Decke, Dach, Stütze,
// Unterzug (Traeger), Tür, Fenster, Achse werden echte KICAD-Elemente. Christian 17.10.2026: «Der Hauptfall wird sein, dass
// wir ein Architekten-IFC importieren und auf dieser Basis arbeiten. Revit behandelt dies wie ein PDF.» Die vereinfachte
// KICAD-Geometrie (Achse, Dicke, Höhe, Umriss) dient zum Zeichnen und Bearbeiten; die exakte Geometrie bleibt in der
// Netzdatei (IfcNetz.cs).
//
// Regeln (Spezifikation, Annahmen gekennzeichnet):
// - Wand: Achse aus «Axis» (sonst Längsrichtung des Netzes), in die Wandmitte gelegt; Dicke aus IfcMaterialLayerSetUsage
//   (Summe), sonst Mengen (Width), sonst Netz quer zur Achse; Unterkante und Höhe aus dem Netz, bei vereinfachtem Netz
//   (Boolean) Höhe aus den Mengen (Height). Je gerades Stück eine Wand; gekrümmte Wände → Hintergrund.
//   Annahme: Wandenden, die in einer anschliessenden Wand liegen (bis 20 mm daneben), enden auf deren Achse – so schliesst
//   der Kern die Ecken wie bei gezeichneten Wänden (Verlängerung um die halbe Dicke).
// - Teile (IfcBuildingElementPart, Zerlegung) gehören zu ihrem Bauteil und werden nicht einzeln übernommen.
// - Material: dickste (tragende) Schicht, deren Name eindeutig einem Material der Klasse entspricht (Annahme: Tabelle
//   Materialien unten); sonst nicht gesetzt.
// - Tür/Fenster: über IfcRelFillsElement → IfcOpeningElement → IfcRelVoidsElement → Wand. abstand = Mitte der Öffnung
//   auf der Achse, breite/hoehe = OverallWidth/OverallHeight (sonst Öffnung), bruestung = Unterkante Öffnung − Geschosskote.
//   Nachbearbeitung N6 (Befund Christian 21.10.2026, Archicad 27 Bernstrasse 42: 23 Türen 5.19 m hoch, Körper 2.15 m): Weicht
//   die Ausdehnung des Körpers längs der z-Achse der Tür bzw. des Fensters um mehr als 400 mm UND mehr als 15 % von
//   OverallHeight ab, gilt der Körper (hoehe, beim Fenster auch bruestung ab Unterkante Körper); Herkunft nennt beide Werte,
//   ein Hinweis nennt jede solche Tür bzw. jedes Fenster. Ohne lesbaren oder mit vereinfachtem Körper bleibt OverallHeight.
//   Schwelle aus den echten Dateien (Probelauf 21.10.2026, 91 bzw. 920 Türen und Fenster mit Körper): Rahmen, Futter und Bänke
//   machen den Körper bis 298 mm (12.6 %) höher als OverallHeight (Bernstrasse bis 170 mm / 7.7 %, BKW Hauptsitz bis 298 mm /
//   12.6 %) – das bleibt OverallHeight; die widersprüchlichen Fälle weichen um 725 bis 4160 mm (20.8 bis 66.5 %) ab: Archicad
//   schreibt dort die Höhe der Öffnung, die unter die Tür reicht (Bernstrasse 3.04 m, BKW 0.74 bis 4.16 m). Die Öffnung selbst
//   bleibt, wie die Datei sie zeichnet (Netzdatei, Kollision).
//   Tür seite/band aus OperationType und Lage der Tür (Annahme IFC-Konvention: Tür öffnet in +y ihrer Lage, «LEFT» = Band
//   links bei Blick in +y); andere Bauarten: leer mit Hinweis.
// - Decke/Dach: Umriss aus dem Profil der Extrusion, sonst Netz-Grundfläche; z = Oberkante, Dicke senkrecht; Löcher als
//   Hinweis. Stütze: Querschnitt, Unterkante, Höhe. Unterzug: Achse, Breite, Höhe, Unterkante; geneigte → Hintergrund.
// - Achse: je IfcGridAxis eine Achse, Kennzeichen und bezeichnung = AxisTag.

using System.Text.RegularExpressions;
using Xbim.Common;
using Xbim.Ifc4.Interfaces;

namespace Hlks.Kern;

public sealed class IfcArchitektur : IIfcUebertrag
{
    public string Bereich => "architektur";
    public IReadOnlySet<string> Klassen { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Wand", "Decke", "Dach", "Stuetze", "Traeger", "Tuer", "Fenster", "Achse" };

    public void Uebertragen(IfcLauf lauf) => new IfcArchitekturLauf(lauf).Ausfuehren();
}

/// <summary>Hinweise gesammelt nach Grund, mit Anzahl und einigen Beispielen (die Übernahme zeigt höchstens 50 Hinweise).
/// Gründe mit «alle» nennen jedes Objekt (N6: Abweichung je Tür bzw. Fenster), nicht nur vier Beispiele.</summary>
sealed class IfcSammler
{
    readonly Dictionary<string, (int Anzahl, List<string> Beispiele)> gruende = [];
    readonly List<string> reihenfolge = [];
    readonly HashSet<string> vollstaendig = [];

    public void Dazu(string grund, string? beispiel = null, int anzahl = 1, bool alle = false)
    {
        if (!gruende.TryGetValue(grund, out var g)) { g = (0, []); reihenfolge.Add(grund); }
        if (alle) vollstaendig.Add(grund);
        if (beispiel is not null && (alle || g.Beispiele.Count < 4)) g.Beispiele.Add(beispiel);
        gruende[grund] = (g.Anzahl + anzahl, g.Beispiele);
    }

    public int Anzahl(string grund) => gruende.TryGetValue(grund, out var g) ? g.Anzahl : 0;

    public void Schreiben(List<string> ziel, string bereich)
    {
        foreach (var grund in reihenfolge)
        {
            var (n, b) = gruende[grund];
            ziel.Add($"{bereich}: {grund} – {n}" + (b.Count == 0 ? "" : vollstaendig.Contains(grund) ? $": {string.Join(", ", b)}" : $" (z. B. {string.Join(", ", b)})"));
        }
    }

    public static string Beispiel(IPersistEntity e, string? name = null) =>
        $"{IfcDatei.Entitaet(e)} #{e.EntityLabel}" + (name is { Length: > 0 } ? $" «{name}»" : e is IIfcRoot r && IfcLauf.Text(r.Name) is { } n ? $" «{n}»" : "");
}

/// <summary>Gemeinsame Lesehilfen der Übertrager B1 (Material, Teile, abgeleitete Elemente).</summary>
static class IfcBauteile
{
    /// <summary>Plant ein Element, das aus einem IFC-Objekt abgeleitet ist, aber eine eigene Kennung braucht (zweites Stück
    /// einer Wand, Achse eines Rasters): Kennung «GlobalId:zusatz», sonst wie IfcLauf.Neu.</summary>
    public static Element NeuAbgeleitet(IfcLauf lauf, string klasse, IIfcRoot quelle, string zusatz, string? geschossId)
    {
        var kennung = $"{quelle.GlobalId}:{zusatz}";
        var id = "ifc:" + kennung;
        if (lauf.Geplant.TryGetValue(id, out var da)) return da;
        var label = ((IPersistEntity)quelle).EntityLabel;
        var e = new Element
        {
            Id = id,
            Klasse = klasse,
            Name = IfcLauf.Text(quelle.Name),
            Geschoss = geschossId ?? (quelle is IIfcObjectDefinition od ? lauf.GeschossId(od) : null),
            Kennungen = new() { ["ifc"] = kennung, ["quelle"] = lauf.UnterlageId },
            Angelegt = lauf.Herkunft with { Quelle = $"{lauf.Quelle}, {IfcDatei.Entitaet((IPersistEntity)quelle)} #{label} ({zusatz})" },
        };
        lauf.Geplant[id] = e;
        lauf.Behandelt.Add(label);
        return e;
    }

    /// <summary>Materialtabelle (Annahme, Prüfliste Christian): Teile des Materialnamens → Material der Katalogauswahl, in
    /// dieser Reihenfolge geprüft (Porenbeton vor Beton, Beton vor Stahl wegen «Stahlbeton»).</summary>
    public static readonly (Regex Muster, string Material)[] Materialien =
    [
        (new Regex(@"poren|gasbeton|ytong|siporex", RegexOptions.IgnoreCase), "porenbeton"),
        (new Regex(@"beton|concrete", RegexOptions.IgnoreCase), "beton"),
        (new Regex(@"kalksand", RegexOptions.IgnoreCase), "kalksandstein"),
        (new Regex(@"backstein|ziegel|brick", RegexOptions.IgnoreCase), "backstein"),
        (new Regex(@"holz|timber|wood", RegexOptions.IgnoreCase), "holz"),
        (new Regex(@"stahl|steel|\bs235\b|\bs275\b|\bs355\b", RegexOptions.IgnoreCase), "stahl"),
        (new Regex(@"gips|trockenbau|leichtbau|st(ä|ae)nderwand", RegexOptions.IgnoreCase), "leichtbau"),
    ];

    /// <summary>Material der Katalogauswahl zu einem Materialnamen oder null.</summary>
    public static string? MaterialFuer(string? name, IReadOnlyCollection<string>? auswahl)
    {
        if (string.IsNullOrWhiteSpace(name) || auswahl is null) return null;
        foreach (var (m, mat) in Materialien)
            if (m.IsMatch(name)) return auswahl.Contains(mat) ? mat : null;
        return null;
    }

    public static string Mm(double x) => Grafik.Format(Math.Round(x, 1), Math.Abs(x - Math.Round(x)) < 0.05 ? 0 : 1);
}

/// <summary>Eine Schicht (Material, Dicke in mm oder NaN, tragend falls angegeben).</summary>
sealed record IfcSchicht(string? Name, double Dicke, bool? Tragend, string Fundstelle);

/// <summary>Ein gerades Stück einer übernommenen Wand (Achse in Wandmitte, Projekt-mm).</summary>
sealed class IfcWandStueck
{
    public required Element Element { get; init; }
    public required IIfcProduct Quelle { get; init; }
    public double Ax, Ay, Bx, By, Dicke, Unterkante, Hoehe;
    public string? GeschossId { get; init; }
    public double Laenge => Math.Sqrt((Bx - Ax) * (Bx - Ax) + (By - Ay) * (By - Ay));
    public double Ux => (Bx - Ax) / Laenge;
    public double Uy => (By - Ay) / Laenge;
}

sealed class IfcArchitekturLauf(IfcLauf lauf)
{
    readonly IfcDatei d = lauf.Datei;
    readonly IfcGeometrie g = IfcGeometrie.Von(lauf.Datei);
    readonly IfcSammler hinweise = new();
    readonly Dictionary<int, List<IIfcProduct>> kinder = [];
    readonly Dictionary<int, IIfcMaterialSelect> material = [];
    readonly Dictionary<int, IIfcOpeningElement> oeffnungVon = [];   // Füllung → Öffnung
    readonly Dictionary<int, IIfcElement> wirtVon = [];              // Öffnung → Wirt
    readonly Dictionary<int, List<IfcWandStueck>> waende = [];       // IFC-Wand → Stücke

    /// <summary>Abstand, bis zu dem ein Wandende noch als «in der anschliessenden Wand» gilt (Annahme, mm).</summary>
    const double AnschlussToleranz = 20;
    /// <summary>Wände fast parallel (|sin| unter 0.05, wie Wandgeometrie): kein Anschluss.</summary>
    const double Parallel = 0.05;
    /// <summary>Geneigt ab 1° (Annahme): Unterzug bzw. Stütze → Hintergrund.</summary>
    static readonly double Neigung = Math.Sin(Math.PI / 180);

    public void Ausfuehren()
    {
        Vorbereiten();
        foreach (var w in d.Modell.Instances.OfType<IIfcWall>().OrderBy(x => x.EntityLabel))
            if (lauf.ImFilter(w)) Wand(w);
        Anschluesse();
        foreach (var s in waende.Values.SelectMany(x => x)) WandSchreiben(s);
        foreach (var t in d.Modell.Instances.OfType<IIfcDoor>().OrderBy(x => x.EntityLabel))
            if (lauf.ImFilter(t)) Oeffnung(t, "Tuer");
        foreach (var f in d.Modell.Instances.OfType<IIfcWindow>().OrderBy(x => x.EntityLabel))
            if (lauf.ImFilter(f)) Oeffnung(f, "Fenster");
        foreach (var p in d.Modell.Instances.OfType<IIfcSlab>().Cast<IIfcProduct>().Concat(d.Modell.Instances.OfType<IIfcRoof>()).OrderBy(x => x.EntityLabel))
            if (lauf.ImFilter(p)) Decke(p);
        foreach (var c in d.Modell.Instances.OfType<IIfcColumn>().OrderBy(x => x.EntityLabel))
            if (lauf.ImFilter(c)) Stuetze(c);
        foreach (var b in d.Modell.Instances.OfType<IIfcBeam>().OrderBy(x => x.EntityLabel))
            if (lauf.ImFilter(b)) Traeger(b);
        foreach (var r in d.Modell.Instances.OfType<IIfcGrid>().OrderBy(x => x.EntityLabel))
            if (lauf.ImFilter(r)) Raster(r);
        if (g.AnzahlVereinfacht > 0)
            hinweise.Dazu("Körper vereinfacht (Boolean: nur erster Operand; Netz und Höhe können zu gross sein), Produkte", null, g.AnzahlVereinfacht);
        foreach (var (ent, n) in g.Unbekannt.OrderByDescending(x => x.Value))
            hinweise.Dazu($"Geometrie {ent} nicht ausgewertet (kein Netz)", null, n);
        hinweise.Schreiben(lauf.Hinweise, "Architektur");
    }

    void Vorbereiten()
    {
        foreach (var r in d.Modell.Instances.OfType<IIfcRelAggregates>())
            foreach (var k in r.RelatedObjects.OfType<IIfcProduct>())
            {
                if (!kinder.TryGetValue(r.RelatingObject.EntityLabel, out var l)) kinder[r.RelatingObject.EntityLabel] = l = [];
                l.Add(k);
            }
        foreach (var r in d.Modell.Instances.OfType<IIfcRelAssociatesMaterial>())
            foreach (var o in r.RelatedObjects)
                if (o is IPersistEntity pe) material.TryAdd(pe.EntityLabel, r.RelatingMaterial);
        foreach (var r in d.Modell.Instances.OfType<IIfcRelFillsElement>())
            if (r.RelatedBuildingElement is not null && r.RelatingOpeningElement is not null) oeffnungVon.TryAdd(r.RelatedBuildingElement.EntityLabel, r.RelatingOpeningElement);
        foreach (var r in d.Modell.Instances.OfType<IIfcRelVoidsElement>())
            if (r.RelatedOpeningElement is not null && r.RelatingBuildingElement is not null) wirtVon.TryAdd(r.RelatedOpeningElement.EntityLabel, r.RelatingBuildingElement);
    }

    // ------------------------------------------------------------------ Hilfen

    void Hintergrund(IIfcProduct p, string grund)
    {
        lauf.NichtVerstandenZaehlen(p);
        hinweise.Dazu(grund + " → Hintergrund", IfcSammler.Beispiel(p));
    }

    /// <summary>Katalogklasse über die IFC-Abbildung; liefert sie keine (Typwert nicht in ifc.json), die Klasse der Entität
    /// mit Hinweis.</summary>
    string Klasse(IIfcProduct p, string nachEntitaet)
    {
        var typ = d.Typ(p);
        var pt = d.PredefinedType(p);
        var t = lauf.Abbildung.KlasseFuer(d.Schema, IfcDatei.Entitaet(p), typ is null ? null : IfcDatei.Entitaet(typ), pt, d.ObjectType(p));
        if (t is not null) return t.Klasse;
        hinweise.Dazu($"PredefinedType {pt ?? "–"} von {IfcDatei.Entitaet(p)} nicht in ifc.json, nach der Entität als {nachEntitaet} übernommen", IfcSammler.Beispiel(p));
        return nachEntitaet;
    }

    /// <summary>Körper des Produkts, sonst das Netz aller Teile (Zerlegung); teile = die Teile mit Körper.</summary>
    Dreiecksnetz? KoerperMitTeilen(IIfcProduct p, out List<IIfcProduct> teile, out bool vereinfacht)
    {
        teile = [];
        var n = g.Koerper(p);
        vereinfacht = g.IstVereinfacht(p);
        if (n is not null) return n;
        if (!kinder.TryGetValue(p.EntityLabel, out var k)) return null;
        var gesamt = new Dreiecksnetz();
        foreach (var t in k)
            if (g.Koerper(t) is { } tn)
            {
                gesamt.Anhaengen(tn);
                teile.Add(t);
                vereinfacht |= g.IstVereinfacht(t);
            }
        return gesamt.Leer ? null : gesamt;
    }

    IIfcMaterialSelect? MaterialVon(IIfcObjectDefinition o) =>
        material.GetValueOrDefault(o.EntityLabel) ?? (o is IIfcObject ob && d.Typ(ob) is { } t ? material.GetValueOrDefault(t.EntityLabel) : null);

    /// <summary>Schichten aus der Materialangabe (Schichtaufbau, Material, Liste); Dicke NaN, wo die Datei keine nennt.</summary>
    List<IfcSchicht> Schichten(IIfcObjectDefinition o)
    {
        var m = MaterialVon(o);
        IIfcMaterialLayerSet? set = m switch { IIfcMaterialLayerSetUsage u => u.ForLayerSet, IIfcMaterialLayerSet s => s, _ => null };
        if (set is not null)
        {
            var art = m is IIfcMaterialLayerSetUsage ? "IfcMaterialLayerSetUsage" : "IfcMaterialLayerSet";
            return set.MaterialLayers.Select(l => new IfcSchicht(IfcLauf.Text(l.Material?.Name), (double)l.LayerThickness * d.MmJeEinheit,
                IfcLauf.Text(l.Category) is { } c ? c.Contains("LoadBearing", StringComparison.OrdinalIgnoreCase) : null,
                $"{art} #{((IPersistEntity)m!).EntityLabel}, Schicht «{IfcLauf.Text(l.Material?.Name)}»")).ToList();
        }
        return m switch
        {
            IIfcMaterialLayer l => [new IfcSchicht(IfcLauf.Text(l.Material?.Name), (double)l.LayerThickness * d.MmJeEinheit, null, $"IfcMaterialLayer #{l.EntityLabel}")],
            IIfcMaterial mat => [new IfcSchicht(IfcLauf.Text(mat.Name), double.NaN, null, $"IfcMaterial #{mat.EntityLabel} «{IfcLauf.Text(mat.Name)}»")],
            IIfcMaterialList ml => ml.Materials.Select(x => new IfcSchicht(IfcLauf.Text(x.Name), double.NaN, null, $"IfcMaterialList #{ml.EntityLabel} «{IfcLauf.Text(x.Name)}»")).ToList(),
            IIfcMaterialConstituentSet cs => cs.MaterialConstituents.Select(x => new IfcSchicht(IfcLauf.Text(x.Material?.Name), double.NaN, null, $"IfcMaterialConstituentSet #{cs.EntityLabel} «{IfcLauf.Text(x.Material?.Name)}»")).ToList(),
            IIfcMaterialProfileSetUsage pu => pu.ForProfileSet.MaterialProfiles.Select(x => new IfcSchicht(IfcLauf.Text(x.Material?.Name), double.NaN, null, $"IfcMaterialProfileSetUsage #{pu.EntityLabel} «{IfcLauf.Text(x.Material?.Name)}»")).ToList(),
            IIfcMaterialProfileSet ps => ps.MaterialProfiles.Select(x => new IfcSchicht(IfcLauf.Text(x.Material?.Name), double.NaN, null, $"IfcMaterialProfileSet #{ps.EntityLabel} «{IfcLauf.Text(x.Material?.Name)}»")).ToList(),
            _ => [],
        };
    }

    IReadOnlyCollection<string>? Auswahl(string klasse, string eigenschaft) =>
        lauf.Katalog.Klassen.TryGetValue(klasse, out var k) ? k.Eigenschaften.FirstOrDefault(e => e.Name == eigenschaft)?.Auswahl : null;

    /// <summary>material = dickste (bei Angabe: tragende) Schicht, deren Name eindeutig einem Material der Klasse entspricht.</summary>
    void MaterialSetzen(Element e, List<IfcSchicht> schichten)
    {
        var auswahl = Auswahl(e.Klasse, "material");
        var passend = schichten.Select(s => (S: s, M: IfcBauteile.MaterialFuer(s.Name, auswahl))).Where(x => x.M is not null).ToList();
        if (passend.Count == 0) return;
        if (passend.Any(x => x.S.Tragend == true)) passend = passend.Where(x => x.S.Tragend == true).ToList();
        var best = passend.OrderByDescending(x => double.IsNaN(x.S.Dicke) ? 0 : x.S.Dicke).First();
        lauf.Wert(e, "material", best.M, best.S.Fundstelle);
    }

    void AufbauSetzen(Element e, List<IfcSchicht> schichten, string fundstelle)
    {
        if (schichten.Count == 0 || schichten.Any(s => double.IsNaN(s.Dicke) || s.Name is null)) return;
        lauf.Wert(e, "aufbau", string.Join(" / ", schichten.Select(s => $"{s.Name} {IfcBauteile.Mm(s.Dicke)}")), fundstelle);
    }

    double? Menge(IIfcObject o, params string[] namen) => d.LaengeMm(IfcDatei.Erster(d.Eigenschaften(o), namen));

    double GeschossKote(string? geschossId) =>
        geschossId is not null && lauf.Kontext.Arbeit.Finde(geschossId)?.Zahl("kote") is double k ? k
        : lauf.GeschossIds.FirstOrDefault(x => x.Value == geschossId).Key?.KoteMm ?? 0;

    static double R(double x) => Math.Round(x, 1);

    // ------------------------------------------------------------------ Wände

    void Wand(IIfcWall w)
    {
        var klasse = Klasse(w, "Wand");
        if (klasse != "Wand") { hinweise.Dazu($"IfcWall als {klasse} abgebildet – nicht übernommen", IfcSammler.Beispiel(w)); return; }
        var netz = KoerperMitTeilen(w, out var teile, out bool vereinfacht);
        var achse = g.Achse(w, out bool gekruemmt);
        if (gekruemmt) { Hintergrund(w, "Gekrümmte Wand (Bogen in der Achse)"); return; }
        if (achse is null && netz is null) { Hintergrund(w, "Wand ohne Achse und ohne Körper"); return; }
        var geschoss = lauf.GeschossId(w);
        var schichten = Schichten(w);
        double? dickeSchichten = schichten.Count > 0 && schichten.All(s => !double.IsNaN(s.Dicke)) && MaterialVon(w) is IIfcMaterialLayerSetUsage or IIfcMaterialLayerSet
            ? schichten.Sum(s => s.Dicke) : null;
        double? dickeMenge = Menge(w, "Qto_WallBaseQuantities.Width", "BaseQuantities.Width");
        double? hoeheMenge = Menge(w, "Qto_WallBaseQuantities.Height", "BaseQuantities.Height");

        // Stücke der Achse (je gerades Stück eine Wand)
        var stuecke = new List<(double[] A, double[] B)>();
        if (achse is not null)
        {
            var pts = new List<double[]> { achse[0] };
            for (int i = 1; i < achse.Count; i++)
            {
                if (pts.Count >= 2)
                {
                    var a = pts[^2]; var b = pts[^1]; var c = achse[i];
                    double ux = b[0] - a[0], uy = b[1] - a[1], vx = c[0] - b[0], vy = c[1] - b[1];
                    double kr = ux * vy - uy * vx, l1 = Math.Sqrt(ux * ux + uy * uy), l2 = Math.Sqrt(vx * vx + vy * vy);
                    if (l1 > 1e-6 && l2 > 1e-6 && Math.Abs(kr) / (l1 * l2) < 0.009 && ux * vx + uy * vy > 0) { pts[^1] = c; continue; }   // gleiche Richtung (< 0.5°)
                }
                pts.Add(achse[i]);
            }
            for (int i = 0; i + 1 < pts.Count; i++)
                if (Math.Sqrt(Math.Pow(pts[i + 1][0] - pts[i][0], 2) + Math.Pow(pts[i + 1][1] - pts[i][1], 2)) > 1) stuecke.Add((pts[i], pts[i + 1]));
            if (stuecke.Count == 0) { if (netz is null) { Hintergrund(w, "Wand mit senkrechter oder leerer Achse"); return; } achse = null; }
        }
        double[]? bb = netz?.Begrenzung();
        if (achse is null)
        {
            // Längsrichtung aus dem Netz: kleinstes umschliessendes Rechteck, nur wenn die Grundfläche es füllt
            var punkte = new List<double[]>();
            for (int i = 0; i < netz!.P.Count; i += 3) punkte.Add([netz.P[i], netz.P[i + 1]]);
            var rechteck = Netzflaechen.Rechteck(punkte);
            double grund = Netzflaechen.Grundflaeche(netz).Sum(f => f.Inhalt);
            if (rechteck is null || rechteck.Breite < 1 || grund < 0.85 * rechteck.Flaeche)
            {
                Hintergrund(w, "Wand ohne Achse, Form nicht als gerades Stück fassbar");
                return;
            }
            double[] m = rechteck.Mitte, u = rechteck.Richtung;
            stuecke.Add(([m[0] - u[0] * rechteck.Laenge / 2, m[1] - u[1] * rechteck.Laenge / 2, bb![2]], [m[0] + u[0] * rechteck.Laenge / 2, m[1] + u[1] * rechteck.Laenge / 2, bb[2]]));
        }

        // Phase 1: alle Stücke rechnen; erst wenn alle gelingen, Elemente planen
        var daten = new List<(double[] A, double[] B, double Dicke, double Unten, double Hoehe)>();
        for (int i = 0; i < stuecke.Count; i++)
        {
            var (a, b) = stuecke[i];
            double l = Math.Sqrt((b[0] - a[0]) * (b[0] - a[0]) + (b[1] - a[1]) * (b[1] - a[1]));
            double ux = (b[0] - a[0]) / l, uy = (b[1] - a[1]) / l, nx = -uy, ny = ux;
            double s0 = 0, s1 = l, qMitte = 0;
            double? dickeNetz = null;
            if (netz is not null)
            {
                // Netzpunkte dieses Stücks; bei mehreren Stücken nur die mittlere Hälfte längs und quer nahe der Achse
                // (an den Knicken liegen Punkte des Nachbarstücks)
                double qMax = (dickeSchichten ?? dickeMenge ?? 1000) + 100;
                double smin = double.MaxValue, smax = double.MinValue, qmin = double.MaxValue, qmax = double.MinValue;
                for (int k = 0; k < netz.P.Count; k += 3)
                {
                    double s = (netz.P[k] - a[0]) * ux + (netz.P[k + 1] - a[1]) * uy, q = (netz.P[k] - a[0]) * nx + (netz.P[k + 1] - a[1]) * ny;
                    if (stuecke.Count > 1 && (s < 0.25 * l || s > 0.75 * l || Math.Abs(q) > qMax)) continue;
                    smin = Math.Min(smin, s); smax = Math.Max(smax, s); qmin = Math.Min(qmin, q); qmax = Math.Max(qmax, q);
                }
                if (qmax > qmin)
                {
                    qMitte = (qmin + qmax) / 2;
                    dickeNetz = qmax - qmin;
                    if (!vereinfacht && stuecke.Count == 1 && smax - smin > 1) { s0 = smin; s1 = smax; }
                }
            }
            else if (MaterialVon(w) is IIfcMaterialLayerSetUsage u && dickeSchichten is double ds)
            {
                // ohne Netz: Achse über den Schichtaufbau in die Mitte (OffsetFromReferenceLine, DirectionSense; die Schichten
                // liegen in +y der Wand, also links, wenn die Achse in +x der Wand läuft)
                double off = (double)u.OffsetFromReferenceLine * d.MmJeEinheit;
                double sinn = u.DirectionSense == IfcDirectionSenseEnum.NEGATIVE ? -1 : 1;
                var y = Affin.Aus(d.Lage(w)).AchseY;
                double links = y[0] * nx + y[1] * ny >= 0 ? 1 : -1;
                qMitte = (off + sinn * ds / 2) * links;
            }
            double dicke = dickeSchichten ?? dickeMenge ?? dickeNetz ?? 0;
            if (dicke <= 0) { Hintergrund(w, "Wand ohne bestimmbare Dicke"); return; }
            if (dickeSchichten is double dsch && dickeNetz is double dn && Math.Abs(dsch - dn) > 5)
                hinweise.Dazu("Dicke aus dem Schichtaufbau weicht vom Körper ab (Schichtaufbau verwendet)", IfcSammler.Beispiel(w) + $" {IfcBauteile.Mm(dsch)} statt {IfcBauteile.Mm(dn)} mm");
            double unten, hoehe;
            if (bb is not null)
            {
                unten = bb[2];
                hoehe = vereinfacht && hoeheMenge is double hm && hm > 0 ? hm : bb[5] - bb[2];
            }
            else
            {
                unten = a[2];
                if (hoeheMenge is not double hm || hm <= 0) { Hintergrund(w, "Wand ohne Körper und ohne Höhe"); return; }
                hoehe = hm;
            }
            daten.Add(([a[0] + ux * s0 + nx * qMitte, a[1] + uy * s0 + ny * qMitte], [a[0] + ux * s1 + nx * qMitte, a[1] + uy * s1 + ny * qMitte], dicke, unten, hoehe));
        }
        // Phase 2: Elemente planen
        var liste = new List<IfcWandStueck>();
        for (int i = 0; i < daten.Count; i++)
        {
            var (a, b, dicke, unten, hoehe) = daten[i];
            var e = i == 0 ? lauf.Neu("Wand", w, geschoss) : IfcBauteile.NeuAbgeleitet(lauf, "Wand", w, (i + 1).ToString(), geschoss);
            liste.Add(new IfcWandStueck
            {
                Element = e, Quelle = w, GeschossId = geschoss,
                Ax = a[0], Ay = a[1], Bx = b[0], By = b[1], Dicke = dicke, Unterkante = unten, Hoehe = hoehe,
            });
        }
        if (stuecke.Count > 1) hinweise.Dazu("Wand mit geknickter Achse in gerade Stücke geteilt", IfcSammler.Beispiel(w) + $" ({stuecke.Count} Stücke)");
        foreach (var t in teile) lauf.Behandelt.Add(t.EntityLabel);
        if (kinder.TryGetValue(w.EntityLabel, out var alleTeile)) foreach (var t in alleTeile) lauf.Behandelt.Add(t.EntityLabel);

        // Material, Aufbau, tragend (am ersten Stück und gleich an den weiteren)
        var (teilSchichten, stimmig) = teile.Count > 0 && schichten.Count == 0 ? TeilSchichten(liste[0], teile) : (null, false);
        if (teilSchichten is not null && !stimmig)
            hinweise.Dazu("Wand aus Teilen: Schichten ergeben die Dicke nicht (Pfosten, Leibungen …) – aufbau leer", IfcSammler.Beispiel(w));
        foreach (var s in liste)
        {
            var e = s.Element;
            var sch = teilSchichten ?? schichten;
            MaterialSetzen(e, sch);
            if (teilSchichten is not null) { if (stimmig) AufbauSetzen(e, teilSchichten, "Teile (IfcBuildingElementPart) mit Material, Dicke aus dem Körper"); }
            else if (dickeSchichten is not null) AufbauSetzen(e, schichten, schichten[0].Fundstelle.Split(',')[0]);
            else if (schichten.Count == 1 && schichten[0].Name is { } nm) lauf.Wert(e, "aufbau", $"{nm} {IfcBauteile.Mm(s.Dicke)}", schichten[0].Fundstelle + ", Dicke der Wand");
            if (d.Eigenschaften(w).TryGetValue("Pset_WallCommon.LoadBearing", out var lb) && lb.Inhalt is bool tragend)
                lauf.Wert(e, "tragend", tragend, "Pset_WallCommon.LoadBearing");
        }
        waende[w.EntityLabel] = liste;
    }

    /// <summary>Schichten aus den Teilen einer Wand: Material des Teils, Dicke = Ausdehnung quer zur Achse, Reihenfolge quer.
    /// Nur Teile, die mindestens die halbe Wandlänge abdecken (Pfosten, Leibungen u. ä. sind keine Schichten). stimmig = die
    /// Schichten ergeben zusammen die Wanddicke (± 5 mm) und überlappen nicht – nur dann taugen sie als Aufbau.</summary>
    (List<IfcSchicht> Schichten, bool Stimmig) TeilSchichten(IfcWandStueck s, List<IIfcProduct> teile)
    {
        double nx = -s.Uy, ny = s.Ux;
        var liste = new List<(double Q0, double Q1, IfcSchicht S)>();
        foreach (var t in teile)
        {
            if (g.Koerper(t) is not { } n) continue;
            var (s0, s1) = n.Bereich(s.Ax, s.Ay, s.Ux, s.Uy);
            if (s1 - s0 < 0.5 * s.Laenge) continue;
            var (q0, q1) = n.Bereich(s.Ax, s.Ay, nx, ny);
            var name = Schichten(t).FirstOrDefault()?.Name ?? IfcLauf.Text(t.Name);
            liste.Add((q0, q1, new IfcSchicht(name, Math.Round(q1 - q0, 1), null, $"Teil {IfcSammler.Beispiel(t, name)}")));
        }
        liste = liste.OrderBy(x => (x.Q0 + x.Q1) / 2).ToList();
        bool stimmig = liste.Count > 0 && Math.Abs(liste.Sum(x => x.S.Dicke) - s.Dicke) <= 5;
        for (int i = 1; i < liste.Count && stimmig; i++) if (liste[i].Q0 < liste[i - 1].Q1 - 5) stimmig = false;
        return (liste.Select(x => x.S).ToList(), stimmig);
    }

    /// <summary>Wandenden, die in einer anschliessenden (nicht parallelen) Wand desselben Geschosses liegen, auf deren Achse
    /// legen; der Kern verlängert sie dann um deren halbe Dicke (Wandgeometrie.Verlaengern) – die Ecke schliesst.</summary>
    void Anschluesse()
    {
        foreach (var gruppe in waende.Values.SelectMany(x => x).GroupBy(s => s.GeschossId))
        {
            var liste = gruppe.ToList();
            var neu = new List<(IfcWandStueck S, bool Anfang, double X, double Y)>();
            foreach (var w in liste)
                foreach (bool anfang in new[] { true, false })
                {
                    double px = anfang ? w.Ax : w.Bx, py = anfang ? w.Ay : w.By;
                    double bestWeg = double.MaxValue; double[]? best = null;
                    foreach (var o in liste)
                    {
                        if (ReferenceEquals(o, w) || o.Dicke <= 0) continue;
                        double kr = w.Ux * o.Uy - w.Uy * o.Ux;
                        if (Math.Abs(kr) < Parallel) continue;
                        // Höhen müssen sich überlappen
                        if (w.Unterkante >= o.Unterkante + o.Hoehe || o.Unterkante >= w.Unterkante + w.Hoehe) continue;
                        double so = (px - o.Ax) * o.Ux + (py - o.Ay) * o.Uy, qo = (px - o.Ax) * -o.Uy + (py - o.Ay) * o.Ux;
                        if (Math.Abs(qo) > o.Dicke / 2 + AnschlussToleranz) continue;
                        if (so < -AnschlussToleranz - o.Dicke / 2 || so > o.Laenge + AnschlussToleranz + o.Dicke / 2) continue;
                        // Schnitt der Achse von w mit der Achse von o: p + t·u, |q_o| = 0
                        double t = -qo / (w.Ux * -o.Uy + w.Uy * o.Ux);
                        double weg = Math.Abs(t);
                        if (weg > (o.Dicke / 2 + AnschlussToleranz) / Math.Abs(kr) + 1) continue;
                        // das Stück darf nicht umklappen
                        double laengeNeu = w.Laenge + (anfang ? -t : t);
                        if (laengeNeu < 1) continue;
                        if (weg < bestWeg) { bestWeg = weg; best = [px + t * w.Ux, py + t * w.Uy]; }
                    }
                    if (best is not null && bestWeg > 1e-6) neu.Add((w, anfang, best[0], best[1]));
                }
            foreach (var (s, anfang, x, y) in neu)
                if (anfang) { s.Ax = x; s.Ay = y; } else { s.Bx = x; s.By = y; }
            if (neu.Count > 0) hinweise.Dazu("Wandenden auf die Achse der anschliessenden Wand gelegt (Ecken und T-Stösse)", null);
        }
    }

    void WandSchreiben(IfcWandStueck s)
    {
        s.Element.Geometrie = new Geometrie
        {
            Art = "linie",
            Punkte = [[R(s.Ax), R(s.Ay), R(s.Unterkante)], [R(s.Bx), R(s.By), R(s.Unterkante)]],
            Dicke = R(s.Dicke),
            Hoehe = R(s.Hoehe),
        };
    }

    // ------------------------------------------------------------------ Türen und Fenster

    void Oeffnung(IIfcElement f, string nachEntitaet)
    {
        var klasse = Klasse(f, nachEntitaet);
        if (klasse is not ("Tuer" or "Fenster")) { hinweise.Dazu($"{IfcDatei.Entitaet(f)} als {klasse} abgebildet – nicht übernommen", IfcSammler.Beispiel(f)); return; }
        string art = klasse == "Tuer" ? "Tür" : "Fenster";
        if (!oeffnungVon.TryGetValue(f.EntityLabel, out var oeff)) { Hintergrund(f, $"{art} ohne Öffnung (IfcRelFillsElement)"); return; }
        var wirt = wirtVon.GetValueOrDefault(oeff.EntityLabel);
        if (wirt is null) { Hintergrund(f, $"{art}: Öffnung ohne Wirtsbauteil"); return; }
        if (!waende.TryGetValue(wirt.EntityLabel, out var stuecke) || stuecke.Count == 0)
        {
            Hintergrund(f, $"{art} ohne übernommene Wirtswand (Wirt {IfcDatei.Entitaet(wirt)})");
            return;
        }
        var on = g.Netz(oeff, ["Body", "Reference"]);
        string quelleLage = $"Öffnung #{oeff.EntityLabel}";
        if (on is null) { on = g.Koerper(f); quelleLage = $"Körper {IfcDatei.Entitaet(f)} #{f.EntityLabel}"; }
        if (on is null) { Hintergrund(f, $"{art} ohne Geometrie der Öffnung"); return; }
        // Wandstück: das, dessen Achse der Öffnung am nächsten liegt
        IfcWandStueck? s = null; double bestQ = double.MaxValue; double s0 = 0, s1 = 0;
        foreach (var w in stuecke)
        {
            var (a0, a1) = on.Bereich(w.Ax, w.Ay, w.Ux, w.Uy);
            var (q0, q1) = on.Bereich(w.Ax, w.Ay, -w.Uy, w.Ux);
            double q = Math.Abs((q0 + q1) / 2), sm = (a0 + a1) / 2;
            if (sm < -w.Dicke || sm > w.Laenge + w.Dicke) q += 1e6;
            if (q < bestQ) { bestQ = q; s = w; s0 = a0; s1 = a1; }
        }
        var wand = s!;
        var bb = on.Begrenzung()!;
        double abstand = R((s0 + s1) / 2);
        var e = lauf.Neu(klasse, f, wand.GeschossId);
        e.Geschoss = wand.GeschossId;
        e.Bezuege["wand"] = wand.Element.Id;
        e.Geometrie = new Geometrie { Art = "punkt", Punkte = [[R(wand.Ax + wand.Ux * abstand), R(wand.Ay + wand.Uy * abstand), R(wand.Unterkante)]] };
        (double? Breite, double? Hoehe) gesamt = f switch
        {
            IIfcDoor t => (t.OverallWidth.HasValue ? (double)t.OverallWidth.Value * d.MmJeEinheit : null, t.OverallHeight.HasValue ? (double)t.OverallHeight.Value * d.MmJeEinheit : null),
            IIfcWindow w => (w.OverallWidth.HasValue ? (double)w.OverallWidth.Value * d.MmJeEinheit : null, w.OverallHeight.HasValue ? (double)w.OverallHeight.Value * d.MmJeEinheit : null),
            _ => (null, null),
        };
        string ent = IfcDatei.Entitaet(f);
        if (gesamt.Breite is double gb && gb > 0) lauf.Wert(e, "breite", R(gb), $"{ent}.OverallWidth");
        else lauf.Wert(e, "breite", R(s1 - s0), $"{quelleLage}, Ausdehnung längs der Wand");
        double unterkante = bb[2];
        string quelleUnterkante = quelleLage;
        if (gesamt.Hoehe is double gh && gh > 0)
        {
            if (HoeheAusKoerper(f, gh) is { } k)
            {
                string koerper = $"Körper {ent} #{f.EntityLabel}";
                lauf.Wert(e, "hoehe", R(k.Hoehe), $"{koerper}, Ausdehnung längs der z-Achse {(klasse == "Tuer" ? "der Tür" : "des Fensters")} {IfcBauteile.Mm(R(k.Hoehe))} mm; " +
                    $"{ent}.OverallHeight {IfcBauteile.Mm(R(gh))} mm weicht um {IfcBauteile.Mm(R(Math.Abs(gh - k.Hoehe)))} mm ab ({SchwelleText}) und gilt nicht");
                unterkante = k.Unterkante;
                quelleUnterkante = koerper;
                hinweise.Dazu($"{art}: Höhe aus dem Körper statt OverallHeight ({SchwelleText}; die Öffnung bleibt, wie die Datei sie zeichnet)",
                    IfcSammler.Beispiel(f) + $" {IfcBauteile.Mm(R(k.Hoehe))} statt {IfcBauteile.Mm(R(gh))} mm", alle: true);
            }
            else lauf.Wert(e, "hoehe", R(gh), $"{ent}.OverallHeight");
        }
        else lauf.Wert(e, "hoehe", R(bb[5] - bb[2]), $"{quelleLage}, Höhe");
        lauf.Wert(e, "abstand", abstand, $"Mitte {quelleLage} auf der Achse der Wand");
        if (klasse == "Fenster")
            lauf.Wert(e, "bruestung", R(unterkante - GeschossKote(wand.GeschossId)), $"Unterkante {quelleUnterkante} − Geschosskote");
        else TuerSeite(e, f, wand);
        if (Math.Abs(bestQ) > wand.Dicke / 2 + 50 && bestQ < 1e6)
            hinweise.Dazu($"{art} liegt neben der Wandachse (auf die Achse gesetzt)", IfcSammler.Beispiel(f) + $" {IfcBauteile.Mm(bestQ)} mm");
    }

    /// <summary>N6: Der Körper gilt statt OverallHeight erst ab dieser Abweichung – beides muss überschritten sein (Herleitung
    /// im Kopf der Datei: Rahmen und Bänke bis 298 mm bzw. 12.6 %, widersprüchliche Dateien ab 725 mm bzw. 20.8 %).</summary>
    const double KoerperAbweichungMm = 400;
    const double KoerperAbweichungAnteil = 0.15;
    static readonly string SchwelleText = $"Abweichung über {IfcBauteile.Mm(KoerperAbweichungMm)} mm und über {IfcBauteile.Mm(KoerperAbweichungAnteil * 100)} %";

    /// <summary>N6: Höhe und Unterkante (z, mm) des Körpers einer Tür bzw. eines Fensters, wenn sie deutlich von OverallHeight
    /// abweicht (<see cref="KoerperAbweichungMm"/> und <see cref="KoerperAbweichungAnteil"/>); sonst null – auch ohne lesbaren
    /// Körper und bei vereinfachtem Körper (Boolean: nur der erste Operand, kann zu gross sein). Höhe = Ausdehnung der Körperpunkte
    /// längs der z-Achse der Lage des Elements (bei lotrechter Lage die z-Ausdehnung).</summary>
    (double Hoehe, double Unterkante)? HoeheAusKoerper(IIfcElement f, double overallHeight)
    {
        if (g.Koerper(f) is not { Leer: false } n || g.IstVereinfacht(f) || n.Begrenzung() is not { } bb) return null;
        var lage = Affin.Aus(d.Lage(f));
        double[] z = lage.AchseZ, o = lage.Ursprung;
        double zl = Math.Sqrt(z[0] * z[0] + z[1] * z[1] + z[2] * z[2]);
        if (zl < 1e-9) return null;
        double z0 = double.MaxValue, z1 = double.MinValue;
        for (int i = 0; i < n.P.Count; i += 3)
        {
            double s = ((n.P[i] - o[0]) * z[0] + (n.P[i + 1] - o[1]) * z[1] + (n.P[i + 2] - o[2]) * z[2]) / zl;
            z0 = Math.Min(z0, s); z1 = Math.Max(z1, s);
        }
        double h = z1 - z0, ab = Math.Abs(h - overallHeight);
        if (h <= 0 || ab <= KoerperAbweichungMm || ab <= KoerperAbweichungAnteil * overallHeight) return null;
        return (h, bb[2]);
    }

    /// <summary>Tür: seite (Aufschlag links/rechts in Wandrichtung) und band (anfang/ende) aus OperationType und Lage.</summary>
    void TuerSeite(Element e, IIfcElement f, IfcWandStueck w)
    {
        string? op = f is IIfcDoor t && t.OperationType.HasValue ? t.OperationType.Value.ToString() : null;
        if (op is null or "NOTDEFINED" or "USERDEFINED")
            op = d.Typ(f) switch
            {
                IIfcDoorType dt => dt.OperationType.ToString(),
                IIfcDoorStyle ds => ds.OperationType.ToString(),
                _ => op,
            };
        var lage = Affin.Aus(d.Lage(f));
        double[] x = lage.AchseX, y = lage.AchseY;
        double nx = -w.Uy, ny = w.Ux;
        double yq = y[0] * nx + y[1] * ny, xs = x[0] * w.Ux + x[1] * w.Uy;
        bool einfluegelig = op is "SINGLE_SWING_LEFT" or "SINGLE_SWING_RIGHT";
        bool drehtuer = einfluegelig || op is "DOUBLE_DOOR_SINGLE_SWING" or "DOUBLE_DOOR_SINGLE_SWING_OPPOSITE_LEFT" or "DOUBLE_DOOR_SINGLE_SWING_OPPOSITE_RIGHT";
        var fund = $"OperationType {op}, Lage {IfcDatei.Entitaet(f)} #{f.EntityLabel} (Öffnung in +y)";
        if (drehtuer && Math.Abs(yq) > 0.5) lauf.Wert(e, "seite", yq > 0 ? "links" : "rechts", fund);
        if (einfluegelig && Math.Abs(xs) > 0.5)
        {
            // Band links bei Blick in +y = Seite −x der Tür
            double bandRichtung = op == "SINGLE_SWING_LEFT" ? -xs : xs;
            lauf.Wert(e, "band", bandRichtung > 0 ? "ende" : "anfang", fund);
        }
        else hinweise.Dazu("Tür ohne eindeutige Bandseite (Bauart " + (op ?? "unbekannt") + "): band leer, Darstellung nach Vorgabe", IfcSammler.Beispiel(f));
    }

    // ------------------------------------------------------------------ Decken, Dächer

    void Decke(IIfcProduct p)
    {
        // N17: Podeste einer Treppe (IfcSlab LANDING als Teil einer IfcStair) gehören zur Treppe (IfcTreppe), nicht zu den Decken
        if (p is IIfcSlab && d.Eltern(p) is IIfcStair or IIfcStairFlight) return;
        // Dach aus Platten (IfcRoof ohne Körper, zerlegt in IfcSlab): die Platten tragen die Geometrie
        if (p is IIfcRoof && !IfcGeometrie.HatKoerper(p) && (!kinder.TryGetValue(p.EntityLabel, out var k) || k.All(x => x is IIfcSlab))) return;
        var klasse = Klasse(p, p is IIfcRoof || d.PredefinedType(p) == "ROOF" ? "Dach" : "Decke");
        if (klasse is not ("Decke" or "Dach")) { hinweise.Dazu($"{IfcDatei.Entitaet(p)} als {klasse} abgebildet – nicht übernommen", IfcSammler.Beispiel(p)); return; }
        var netz = KoerperMitTeilen(p, out var teile, out _);
        if (netz is null) { Hintergrund(p, $"{klasse} ohne Körper"); return; }
        var bb = netz.Begrenzung()!;
        List<double[]>? ring = null; double dicke = 0; int loecher = 0, flaechen = 1;
        var ex = teile.Count == 0 ? g.Extrusion(p) : null;
        if (ex is not null)
        {
            var aussen = ex.Aussen.OrderByDescending(r => Math.Abs(Polygone.Flaecheninhalt(r))).First();
            flaechen = ex.Aussen.Count;
            loecher = ex.Loecher.Count;
            var v = ex.Vektor;
            var nrm = Normale(aussen);
            if (Math.Abs(nrm[2]) > 0.2)
            {
                // Oberseite: der Ring oder der um die Extrusion verschobene Ring, je nachdem welcher oben liegt
                bool hoch = v[2] > 0;
                ring = aussen.Select(q => hoch ? new[] { q[0] + v[0], q[1] + v[1], q[2] + v[2] } : q).ToList();
                dicke = Math.Abs((v[0] * nrm[0] + v[1] * nrm[1] + v[2] * nrm[2]) / nrm[2]);
            }
        }
        if (ring is null)
        {
            var gf = Netzflaechen.Grundflaeche(netz);
            if (gf.Count == 0) { Hintergrund(p, $"{klasse} ohne Grundfläche"); return; }
            var f = gf.OrderByDescending(x => x.Inhalt).First();
            flaechen = gf.Count;
            loecher = f.Loecher.Count;
            ring = f.Aussen.Select(q => new[] { q[0], q[1], bb[5] }).ToList();
            dicke = bb[5] - bb[2];
            if (ex is null && teile.Count == 0) hinweise.Dazu($"{klasse}: Umriss aus der Netz-Grundfläche, Dicke = Höhe des Netzes", IfcSammler.Beispiel(p));
        }
        if (dicke <= 0 || ring.Count < 3) { Hintergrund(p, $"{klasse} ohne Dicke oder Umriss"); return; }
        if (Polygone.Flaecheninhalt(ring) < 0) ring.Reverse();
        var e = lauf.Neu(klasse, p);
        e.Geometrie = new Geometrie { Art = "polygon", Punkte = ring.Select(q => new[] { R(q[0]), R(q[1]), R(q[2]) }).ToList(), Dicke = R(dicke) };
        foreach (var t in teile) lauf.Behandelt.Add(t.EntityLabel);
        if (kinder.TryGetValue(p.EntityLabel, out var alleTeile)) foreach (var t in alleTeile) lauf.Behandelt.Add(t.EntityLabel);
        if (loecher > 0) hinweise.Dazu($"{klasse} mit Aussparungen: Umriss ohne Löcher übernommen (Löcher in der Netzdatei)", IfcSammler.Beispiel(p) + $" {loecher} Löcher");
        if (flaechen > 1) hinweise.Dazu($"{klasse} aus mehreren Flächen: grösste übernommen", IfcSammler.Beispiel(p) + $" {flaechen} Flächen");
        var schichten = Schichten(p);
        if (schichten.Count > 0 && schichten.All(s => !double.IsNaN(s.Dicke)) && MaterialVon(p) is IIfcMaterialLayerSetUsage or IIfcMaterialLayerSet)
            AufbauSetzen(e, schichten, schichten[0].Fundstelle.Split(',')[0]);
        else if (schichten.Count == 1 && schichten[0].Name is { } nm)
            lauf.Wert(e, "aufbau", $"{nm} {IfcBauteile.Mm(dicke)}", schichten[0].Fundstelle + ", Dicke aus dem Körper");
    }

    static double[] Normale(List<double[]> ring)
    {
        double x = 0, y = 0, z = 0;
        for (int i = 0; i < ring.Count; i++)
        {
            var a = ring[i]; var b = ring[(i + 1) % ring.Count];
            x += (a[1] - b[1]) * (a[2] + b[2]); y += (a[2] - b[2]) * (a[0] + b[0]); z += (a[0] - b[0]) * (a[1] + b[1]);
        }
        double l = Math.Sqrt(x * x + y * y + z * z);
        if (l < 1e-12) return [0, 0, 1];
        return z < 0 ? [-x / l, -y / l, -z / l] : [x / l, y / l, z / l];
    }

    // ------------------------------------------------------------------ Stützen, Unterzüge

    void Stuetze(IIfcColumn c)
    {
        var klasse = Klasse(c, "Stuetze");
        if (klasse != "Stuetze") { hinweise.Dazu($"IfcColumn als {klasse} abgebildet – nicht übernommen", IfcSammler.Beispiel(c)); return; }
        var netz = KoerperMitTeilen(c, out var teile, out _);
        if (netz is null) { Hintergrund(c, "Stütze ohne Körper"); return; }
        var bb = netz.Begrenzung()!;
        List<double[]>? ring = null; double unten = bb[2], hoehe = bb[5] - bb[2];
        var ex = teile.Count == 0 ? g.Extrusion(c) : null;
        if (ex is not null)
        {
            if (!ex.Senkrecht) { Hintergrund(c, "Geneigte Stütze"); return; }
            ring = ex.Aussen.OrderByDescending(r => Math.Abs(Polygone.Flaecheninhalt(r))).First().Select(q => new[] { q[0], q[1] }).ToList();
            unten = ex.Unten;
            hoehe = ex.Hoehe;
        }
        else
        {
            // ohne Extrusion (z. B. tesselliert): geneigt, wenn sich die Mitte unten und oben um mehr als 1° der Höhe
            // (mindestens 20 mm) unterscheidet
            double h10 = hoehe / 10;
            double[] u0 = [double.MaxValue, double.MaxValue, double.MinValue, double.MinValue], o0 = [double.MaxValue, double.MaxValue, double.MinValue, double.MinValue];
            static void Dazu(double[] b, double x, double y) { b[0] = Math.Min(b[0], x); b[1] = Math.Min(b[1], y); b[2] = Math.Max(b[2], x); b[3] = Math.Max(b[3], y); }
            for (int i = 0; i < netz.P.Count; i += 3)
            {
                if (netz.P[i + 2] <= bb[2] + h10) Dazu(u0, netz.P[i], netz.P[i + 1]);
                if (netz.P[i + 2] >= bb[5] - h10) Dazu(o0, netz.P[i], netz.P[i + 1]);
            }
            double versatz = Math.Sqrt(Math.Pow((u0[0] + u0[2] - o0[0] - o0[2]) / 2, 2) + Math.Pow((u0[1] + u0[3] - o0[1] - o0[3]) / 2, 2));
            if (versatz > Math.Max(20, hoehe * Neigung)) { Hintergrund(c, "Geneigte Stütze"); return; }
            var gf = Netzflaechen.Grundflaeche(netz);
            if (gf.Count == 0) { Hintergrund(c, "Stütze ohne Querschnitt"); return; }
            ring = gf.OrderByDescending(x => x.Inhalt).First().Aussen;
        }
        if (hoehe <= 0 || ring.Count < 3) { Hintergrund(c, "Stütze ohne Höhe oder Querschnitt"); return; }
        if (Polygone.Flaecheninhalt(ring) < 0) ring.Reverse();
        var e = lauf.Neu("Stuetze", c);
        e.Geometrie = new Geometrie { Art = "polygon", Punkte = ring.Select(q => new[] { R(q[0]), R(q[1]), R(unten) }).ToList(), Hoehe = R(hoehe) };
        foreach (var t in teile) lauf.Behandelt.Add(t.EntityLabel);
        if (kinder.TryGetValue(c.EntityLabel, out var alleTeile)) foreach (var t in alleTeile) lauf.Behandelt.Add(t.EntityLabel);
        MaterialSetzen(e, Schichten(c));
    }

    void Traeger(IIfcBeam b)
    {
        var klasse = Klasse(b, "Traeger");
        if (klasse != "Traeger") { hinweise.Dazu($"IfcBeam als {klasse} abgebildet – nicht übernommen", IfcSammler.Beispiel(b)); return; }
        var netz = KoerperMitTeilen(b, out var teile, out bool vereinfacht);
        if (netz is null) { Hintergrund(b, "Unterzug ohne Körper"); return; }
        var bb = netz.Begrenzung()!;
        double[]? a = null, e2 = null;
        var ex = teile.Count == 0 ? g.Extrusion(b) : null;
        var achse = g.Achse(b, out bool gekruemmt);
        if (gekruemmt) { Hintergrund(b, "Gekrümmter Unterzug"); return; }
        if (ex is not null)
        {
            var v = ex.Vektor;
            double l = Math.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]);
            if (l > 0 && Math.Abs(v[2]) / l > Neigung && Math.Abs(v[2]) / l < 0.99) { Hintergrund(b, "Geneigter Unterzug"); return; }
            if (l > 0 && Math.Abs(v[2]) / l >= 0.99) { Hintergrund(b, "Senkrechter Träger (wie Stütze)"); return; }
            if (achse is null)
            {
                // Waagrechte Extrusion: das Profil steht lotrecht und ist im Grundriss eine Strecke quer zur Achse. Anfang = Mitte
                // dieser Strecke (Lage quer und Unterkante kommen danach aus dem Netz). Nachbearbeitung N7 (21.10.2026): vorher der
                // Flächenschwerpunkt im Grundriss – bei Fläche 0 und Koordinaten weit vom Ursprung ein Rundungsrest, der UZ-14 und
                // UZ-62 (Bernstrasse 42, Archicad 27) um 15.9 bzw. 15.5 m längs ihrer Achse aus dem Gebäude schob.
                var ring = ex.Aussen[0];
                double[] c = [(ring.Min(q => q[0]) + ring.Max(q => q[0])) / 2, (ring.Min(q => q[1]) + ring.Max(q => q[1])) / 2];
                a = [c[0], c[1]]; e2 = [c[0] + v[0], c[1] + v[1]];
            }
        }
        if (achse is not null)
        {
            if (achse.Count > 2) { Hintergrund(b, "Unterzug mit geknickter Achse"); return; }
            a = achse[0]; e2 = achse[^1];
        }
        if (a is null || e2 is null)
        {
            var punkte = new List<double[]>();
            for (int i = 0; i < netz.P.Count; i += 3) punkte.Add([netz.P[i], netz.P[i + 1]]);
            if (Netzflaechen.Rechteck(punkte) is not { } r || r.Laenge < 1) { Hintergrund(b, "Unterzug ohne Längsrichtung"); return; }
            a = [r.Mitte[0] - r.Richtung[0] * r.Laenge / 2, r.Mitte[1] - r.Richtung[1] * r.Laenge / 2];
            e2 = [r.Mitte[0] + r.Richtung[0] * r.Laenge / 2, r.Mitte[1] + r.Richtung[1] * r.Laenge / 2];
        }
        double lx = e2[0] - a[0], ly = e2[1] - a[1], len = Math.Sqrt(lx * lx + ly * ly);
        if (len < 1) { Hintergrund(b, "Unterzug ohne Länge"); return; }
        double ux = lx / len, uy = ly / len;
        var (s0, s1) = netz.Bereich(a[0], a[1], ux, uy);
        var (q0, q1) = netz.Bereich(a[0], a[1], -uy, ux);
        if (ex is null)
        {
            // ohne Extrusion (z. B. tesselliert): Unterkante an beiden Enden vergleichen – geneigt, wenn sie sich um mehr als 1°
            // der Länge (mindestens 20 mm) unterscheidet
            double zA = double.MaxValue, zB = double.MaxValue, l10 = (s1 - s0) / 10;
            for (int i = 0; i < netz.P.Count; i += 3)
            {
                double s = (netz.P[i] - a[0]) * ux + (netz.P[i + 1] - a[1]) * uy;
                if (s <= s0 + l10) zA = Math.Min(zA, netz.P[i + 2]);
                if (s >= s1 - l10) zB = Math.Min(zB, netz.P[i + 2]);
            }
            if (Math.Abs(zA - zB) > Math.Max(20, (s1 - s0) * Neigung)) { Hintergrund(b, "Geneigter Unterzug"); return; }
        }
        if (vereinfacht) { s0 = 0; s1 = len; }
        double qm = (q0 + q1) / 2, breite = q1 - q0, hoehe = bb[5] - bb[2];
        if (breite <= 0 || hoehe <= 0) { Hintergrund(b, "Unterzug ohne Querschnitt"); return; }
        var e = lauf.Neu("Traeger", b);
        e.Geometrie = new Geometrie
        {
            Art = "linie",
            Punkte = [[R(a[0] + ux * s0 - uy * qm), R(a[1] + uy * s0 + ux * qm), R(bb[2])], [R(a[0] + ux * s1 - uy * qm), R(a[1] + uy * s1 + ux * qm), R(bb[2])]],
            Dicke = R(breite),
            Hoehe = R(hoehe),
        };
        foreach (var t in teile) lauf.Behandelt.Add(t.EntityLabel);
        if (kinder.TryGetValue(b.EntityLabel, out var alleTeile)) foreach (var t in alleTeile) lauf.Behandelt.Add(t.EntityLabel);
        MaterialSetzen(e, Schichten(b));
    }

    // ------------------------------------------------------------------ Achsraster

    void Raster(IIfcGrid r)
    {
        var lage = Affin.Aus(d.Lage(r));
        int n = 0;
        foreach (var (familie, achsen) in new[] { ("U", r.UAxes), ("V", r.VAxes), ("W", r.WAxes) })
        {
            int i = 0;
            foreach (var ax in achsen)
            {
                i++;
                bool gekruemmt = false;
                var k = g.Kurve(ax.AxisCurve, ref gekruemmt);
                var tag = IfcLauf.Text(ax.AxisTag);
                if (k is null || k.Count < 2 || gekruemmt) { hinweise.Dazu("Rasterachse gekrümmt oder ohne Linie – nicht übernommen", $"IfcGrid #{r.EntityLabel} Achse {tag}"); continue; }
                if (tag is null) { hinweise.Dazu("Rasterachse ohne Bezeichnung (AxisTag) – nicht übernommen", $"IfcGrid #{r.EntityLabel} {familie}{i}"); continue; }
                var a = lage.Punkt(k[0]); var b = lage.Punkt(k[^1]);
                if (!(bool)ax.SameSense) (a, b) = (b, a);
                var e = IfcBauteile.NeuAbgeleitet(lauf, "Achse", r, $"{familie}{i}", lauf.GeschossId(r));
                e.Name = tag;
                e.Kennzeichen = tag;
                e.Geometrie = new Geometrie { Art = "linie", Punkte = [[R(a[0]), R(a[1]), R(a[2])], [R(b[0]), R(b[1]), R(b[2])]] };
                lauf.Wert(e, "bezeichnung", tag, $"IfcGridAxis #{ax.EntityLabel}.AxisTag");
                n++;
            }
        }
        if (n == 0) Hintergrund(r, "Achsraster ohne verwendbare Achsen");
    }
}
