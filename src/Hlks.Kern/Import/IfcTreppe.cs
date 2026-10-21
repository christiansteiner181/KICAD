// Übertrag der Treppen aus IFC (Nachbearbeitung N17, Christian 21.10.2026: «auch treppen sind im Grundriss noch nicht korrekt
// angezeigt. resp. nicht erkennbar.»): IfcStairFlight bzw. IfcStair werden Elemente der Klasse Treppe, je Lauf eines. Vorher blieben
// sie Hintergrund-Netz der Unterlage, und der Grundrissschnitt zeigte ein Gewirr aus Stufenkanten.
//
// Regeln (Spezifikation Hauptlauf, Annahmen gekennzeichnet):
// - Lauf: Jede IfcStairFlight ist eine Treppe. Eine IfcStair ohne IfcStairFlight (Archicad: Teile IfcBuildingElementPart) ist selbst
//   eine Treppe; mit Läufen sind es die Läufe. Podeste (Platten, die zur IfcStair gehören, z. B. IfcSlab LANDING) kommen als Umriss zu
//   dem Lauf, dessen Lauflinie ihnen am nächsten liegt; sie werden nicht zusätzlich als Decke übernommen (IfcArchitektur).
// - Stufenzahl (Steigungen), Steigung, Auftritt: aus den Attributen der Datei – IfcStairFlight.NumberOfRisers, RiserHeight,
//   TreadLength (IFC4 und IFC2x3), sonst Pset_StairFlightCommon bzw. Pset_StairCommon (Eigenschaften NumberOfRiser, RiserHeight,
//   TreadLength; Namen an den PSD von buildingSMART geprüft, Test TreppeIfcTests), am Lauf und sonst an seiner IfcStair. Fehlt ein
//   Wert: aus dem Körper (Treppenanalyse: waagrechte Trittflächen nach Höhe gruppiert, Vorderkanten, Lauflinie durch die Mitten).
//   Stufenkanten, Lauflinie, Podeste, Laufbreite, Unter- und Oberkante kommen immer aus dem Körper.
// - Netzdatei: die Treppe ist übernommen (Kennzeichen 1), das Original-Netz wird also nicht zusätzlich als Hintergrund gezeigt. Die
//   exakte Geometrie bleibt in der Netzdatei (Kollisionsprüfung).

using Xbim.Common;
using Xbim.Ifc4.Interfaces;

namespace Hlks.Kern;

public sealed class IfcTreppe : IIfcUebertrag
{
    public string Bereich => "architektur";
    public IReadOnlySet<string> Klassen { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Treppe" };

    public void Uebertragen(IfcLauf lauf) => new IfcTreppeLauf(lauf).Ausfuehren();
}

sealed class IfcTreppeLauf(IfcLauf lauf)
{
    readonly IfcDatei d = lauf.Datei;
    readonly IfcGeometrie g = IfcGeometrie.Von(lauf.Datei);
    readonly IfcSammler hinweise = new();
    readonly IfcNetzMaterial material = new(lauf.Datei);
    readonly Dictionary<int, List<IIfcProduct>> kinder = [];
    readonly List<(Element E, IIfcProduct Quelle)> angelegt = [];

    /// <summary>Kandidaten für die Zahl der Steigungen im Property Set (IFC4 und IFC2x3 schreiben «NumberOfRiser», nicht «…Risers»).</summary>
    static readonly string[] PsetStufen = ["Pset_StairFlightCommon.NumberOfRiser", "Pset_StairFlightCommon.NumberOfRisers", "Pset_StairCommon.NumberOfRiser", "Pset_StairCommon.NumberOfRisers"];
    static readonly string[] PsetSteigung = ["Pset_StairFlightCommon.RiserHeight", "Pset_StairCommon.RiserHeight"];
    static readonly string[] PsetAuftritt = ["Pset_StairFlightCommon.TreadLength", "Pset_StairCommon.TreadLength"];

    public void Ausfuehren()
    {
        foreach (var r in d.Modell.Instances.OfType<IIfcRelAggregates>())
            Kinder(r.RelatingObject, r.RelatedObjects);
        foreach (var r in d.Modell.Instances.OfType<IIfcRelNests>())
            Kinder(r.RelatingObject, r.RelatedObjects);

        foreach (var f in d.Modell.Instances.OfType<IIfcStairFlight>().OrderBy(x => x.EntityLabel))
            if (lauf.ImFilter(f)) Treppe(f);
        foreach (var s in d.Modell.Instances.OfType<IIfcStair>().OrderBy(x => x.EntityLabel))
        {
            var fluege = KinderVon(s).OfType<IIfcStairFlight>().ToList();
            if (fluege.Count == 0) { if (lauf.ImFilter(s)) Treppe(s); }
            else Podeste(s, fluege);
        }
        hinweise.Schreiben(lauf.Hinweise, "Treppen");
    }

    void Kinder(IIfcObjectDefinition ganzes, IEnumerable<IIfcObjectDefinition> teile)
    {
        if (ganzes is not IIfcProduct) return;
        foreach (var t in teile.OfType<IIfcProduct>())
        {
            if (!kinder.TryGetValue(ganzes.EntityLabel, out var l)) kinder[ganzes.EntityLabel] = l = [];
            if (!l.Contains(t)) l.Add(t);
        }
    }

    List<IIfcProduct> KinderVon(IIfcProduct p) => kinder.GetValueOrDefault(p.EntityLabel) ?? [];

    // ------------------------------------------------------------------ Körper

    /// <summary>Körper des Produkts, sonst die Körper seiner Teile (verschachtelt); teile = alle Nachkommen (zum Kennzeichnen).</summary>
    Dreiecksnetz? Koerper(IIfcProduct p, List<IIfcProduct> teile)
    {
        Alle(p, teile, 0);
        if (g.Koerper(p) is { Leer: false } eigen) return eigen;
        var gesamt = new Dreiecksnetz();
        Sammeln(p, gesamt, 0);
        return gesamt.Leer ? null : gesamt;
    }

    void Alle(IIfcProduct p, List<IIfcProduct> teile, int tiefe)
    {
        if (tiefe > 6) return;
        foreach (var k in KinderVon(p)) { if (!teile.Contains(k)) teile.Add(k); Alle(k, teile, tiefe + 1); }
    }

    void Sammeln(IIfcProduct p, Dreiecksnetz gesamt, int tiefe)
    {
        if (tiefe > 6) return;
        foreach (var k in KinderVon(p))
        {
            if (g.Koerper(k) is { Leer: false } n) gesamt.Anhaengen(n);
            else Sammeln(k, gesamt, tiefe + 1);
        }
    }

    // ------------------------------------------------------------------ Treppe

    void Hintergrund(IIfcProduct p, string grund)
    {
        lauf.NichtVerstandenZaehlen(p);
        hinweise.Dazu(grund + " → Hintergrund", IfcSammler.Beispiel(p));
    }

    void Treppe(IIfcProduct p)
    {
        var typ = p is IIfcObject ob ? d.Typ(ob) : null;
        var t = p is IIfcObject obj ? lauf.Abbildung.KlasseFuer(d.Schema, IfcDatei.Entitaet(p), typ is null ? null : IfcDatei.Entitaet(typ), d.PredefinedType(obj), d.ObjectType(obj)) : null;
        if (t is not null && t.Klasse != "Treppe") { hinweise.Dazu($"{IfcDatei.Entitaet(p)} als {t.Klasse} abgebildet – nicht übernommen", IfcSammler.Beispiel(p)); return; }
        var teile = new List<IIfcProduct>();
        var netz = Koerper(p, teile);
        if (netz is null) { Hintergrund(p, "Treppe ohne Körper"); return; }
        // Attribute der Datei; die Stufenzahl hilft der Analyse, Boden und Podest an den Enden zu finden
        var (aStufen, aSteigung, aAuftritt, qStufen, qSteigung, qAuftritt) = Attribute(p);
        var koten = d.Geschosse().Select(x => x.KoteMm).ToList();
        var form = Treppenanalyse.Aus(netz, koten, aStufen is double sz ? (int)Math.Round(sz) : null);
        if (form is null) { Hintergrund(p, "Treppe: Trittflächen im Körper nicht erkennbar"); return; }
        double stufen = aStufen ?? form.Stufen, steigung = aSteigung ?? form.Steigung, auftritt = aAuftritt ?? form.Auftritt;
        string geo = $"Geometrie {IfcDatei.Entitaet(p)} #{p.EntityLabel}: {form.Herleitung}";

        var e = lauf.Neu("Treppe", p);
        // Geschoss: aus der räumlichen Gliederung, sonst nach der Höhe des Antritts
        if (e.Geschoss is null)
        {
            var passend = lauf.GeschossIds.Where(kv => kv.Key.KoteMm <= form.Unterkante + 50).OrderByDescending(kv => kv.Key.KoteMm).Select(kv => kv.Value).FirstOrDefault();
            e.Geschoss = passend ?? lauf.GeschossIds.OrderBy(kv => kv.Key.KoteMm).Select(kv => kv.Value).FirstOrDefault();
        }
        e.Geometrie = new Geometrie { Art = "polylinie", Punkte = form.Lauflinie.Select(q => new[] { q[0], q[1], q[2] }).ToList() };
        lauf.Wert(e, "stufen", stufen, aStufen is null ? geo : qStufen);
        lauf.Wert(e, "steigung", Treppenlage.Runden(steigung), aSteigung is null ? geo : qSteigung);
        lauf.Wert(e, "auftritt", Treppenlage.Runden(auftritt), aAuftritt is null ? geo : qAuftritt);
        lauf.Wert(e, "laufbreite", form.Laufbreite, geo + " (Ausdehnung der Trittflächen quer zur Gehrichtung, Median)");
        lauf.Wert(e, "richtung", "aufwaerts", "Lauflinie vom tieferen zum höheren Ende (Geometrie)");
        lauf.Wert(e, "unterkante", form.Unterkante, geo);
        lauf.Wert(e, "oberkante", Treppenlage.Runden(form.Unterkante + stufen * steigung), $"unterkante + stufen · steigung ({geo})");
        lauf.Wert(e, "stufenkanten", Treppenlage.KantenJson(form.Kanten), $"{geo} (Vorderkante jeder Trittfläche)");
        if (form.Podeste.Count > 0) lauf.Wert(e, "podeste", Treppenlage.PodesteJson(form.Podeste), $"{geo} (Flächen über dem 2.2fachen der mittleren Trittfläche)");
        lauf.Wert(e, "laufdicke", form.Laufdicke, $"{geo} (Höhe der ersten Trittfläche über der tiefsten Stelle des Körpers)");
        MaterialSetzen(e, p, teile);
        foreach (var k in teile) lauf.Behandelt.Add(k.EntityLabel);
        angelegt.Add((e, p));

        if (aStufen is not null && (aStufen.Value < form.Spruenge || aStufen.Value > form.Spruenge + 2))
            hinweise.Dazu("Stufenzahl aus den Attributen passt nicht zu den Höhenunterschieden der Trittflächen (erwartet: Sprünge bis Sprünge + 2) – Attribute gelten",
                IfcSammler.Beispiel(p) + $" {aStufen:0} Steigungen, {form.Spruenge} Sprünge");
        if (aSteigung is not null && Math.Abs(aSteigung.Value - form.Steigung) > 0.05 * form.Steigung)
            hinweise.Dazu("Steigung aus den Attributen weicht um mehr als 5 % von den Höhenunterschieden der Trittflächen ab – Attribute gelten",
                IfcSammler.Beispiel(p) + $" {aSteigung:0.#} statt {form.Steigung:0.#} mm");
        if (aStufen is null) hinweise.Dazu("Stufenzahl ohne Attribut aus der Geometrie gezählt", IfcSammler.Beispiel(p) + $" {form.Stufen}");
    }

    void MaterialSetzen(Element e, IIfcProduct p, List<IIfcProduct> teile)
    {
        var auswahl = lauf.Katalog.Klassen.TryGetValue("Treppe", out var k) ? k.Eigenschaften.FirstOrDefault(x => x.Name == "material")?.Auswahl : null;
        var angaben = new List<IfcNetzMaterial.Angabe> { material.Von(p) };
        angaben.AddRange(teile.Select(material.Von));
        foreach (var a in angaben)
            foreach (var name in a.Namen)
                if (IfcBauteile.MaterialFuer(name, auswahl) is { } m) { lauf.Wert(e, "material", m, $"{a.Quelle ?? "Material"} «{name}»"); return; }
    }

    /// <summary>Stufen (Steigungen), Steigung (mm), Auftritt (mm) aus den Attributen des Laufs, sonst den Property Sets von Lauf und
    /// IfcStair; mit Fundstelle. Nicht vorhandene Werte sind null.</summary>
    (double? Stufen, double? Steigung, double? Auftritt, string Qs, string Qh, string Qa) Attribute(IIfcProduct p)
    {
        double? stufen = null, steigung = null, auftritt = null;
        string qs = "", qh = "", qa = "";
        if (p is IIfcStairFlight f)
        {
            if (f.NumberOfRisers.HasValue && (long)f.NumberOfRisers.Value > 0) { stufen = (long)f.NumberOfRisers.Value; qs = "IfcStairFlight.NumberOfRisers"; }
            if (f.RiserHeight.HasValue && (double)f.RiserHeight.Value > 0) { steigung = (double)f.RiserHeight.Value * d.MmJeEinheit; qh = "IfcStairFlight.RiserHeight"; }
            if (f.TreadLength.HasValue && (double)f.TreadLength.Value > 0) { auftritt = (double)f.TreadLength.Value * d.MmJeEinheit; qa = "IfcStairFlight.TreadLength"; }
        }
        var quellen = new List<IIfcObject>();
        if (p is IIfcObject o) quellen.Add(o);
        if (d.Eltern(p) is IIfcStair s) quellen.Add(s);
        foreach (var q in quellen)
        {
            var eig = d.Eigenschaften(q);
            if (stufen is null && Zahl(IfcDatei.Erster(eig, PsetStufen)) is double sv && sv > 0 && Array.Find(PsetStufen, k => eig.ContainsKey(k)) is { } ks) { stufen = Math.Round(sv); qs = ks; }
            if (steigung is null && IfcDatei.Erster(eig, PsetSteigung) is { } hw && d.LaengeMm(hw) is double hv && hv > 0 && Array.Find(PsetSteigung, k => eig.ContainsKey(k)) is { } kh) { steigung = hv; qh = kh; }
            if (auftritt is null && IfcDatei.Erster(eig, PsetAuftritt) is { } aw && d.LaengeMm(aw) is double av && av > 0 && Array.Find(PsetAuftritt, k => eig.ContainsKey(k)) is { } ka) { auftritt = av; qa = ka; }
        }
        return (stufen, steigung, auftritt, qs, qh, qa);
    }

    static double? Zahl(IfcWert? w) => w?.Inhalt switch { double x => x, long l => l, int i => i, _ => null };

    // ------------------------------------------------------------------ Podeste eines Treppenhauses mit Läufen

    /// <summary>Platten und sonstige Teile einer IfcStair mit Läufen (Podeste): Umriss der obersten Fläche, dem Lauf zugeordnet,
    /// dessen Lauflinie ihnen am nächsten liegt.</summary>
    void Podeste(IIfcStair s, List<IIfcStairFlight> fluege)
    {
        var laeufe = fluege.Select(f => angelegt.FirstOrDefault(a => a.Quelle.EntityLabel == f.EntityLabel)).Where(a => a.E is not null).ToList();
        foreach (var teil in KinderVon(s).Where(k => k is not IIfcStairFlight))
        {
            var labels = new List<IIfcProduct>();
            var netz = Koerper(teil, labels);
            if (netz is null) continue;
            var oben = Treppenanalyse.Waagrechte(netz).OrderByDescending(x => x.Z).ThenByDescending(x => x.Inhalt).FirstOrDefault();
            if (oben is null) { hinweise.Dazu("Teil einer Treppe ohne waagrechte Oberseite → Hintergrund", IfcSammler.Beispiel(teil)); continue; }
            if (laeufe.Count == 0) continue;
            var mitte = Polygone.Schwerpunkt(oben.Ring);
            var ziel = laeufe.MinBy(a => AbstandZuLauf(a.E, mitte));
            var lage = Treppenlage.Von(ziel.E);
            var podeste = (lage?.Podeste ?? []).ToList();
            podeste.Add(new TreppePodest(Math.Round(oben.Z, 1), oben.Ring));
            lauf.Wert(ziel.E, "podeste", Treppenlage.PodesteJson(podeste), $"{(ziel.E.Eigenschaft("podeste")?.Herkunft.Quelle ?? "")} {IfcDatei.Entitaet(teil)} #{teil.EntityLabel} (Oberseite)".Trim());
            lauf.Behandelt.Add(teil.EntityLabel);
            foreach (var l in labels) lauf.Behandelt.Add(l.EntityLabel);
        }
    }

    static double AbstandZuLauf(Element e, double[] p)
    {
        var pts = e.Geometrie?.Punkte ?? [];
        if (pts.Count == 0) return double.MaxValue;
        return pts.Min(q => Math.Sqrt((q[0] - p[0]) * (q[0] - p[0]) + (q[1] - p[1]) * (q[1] - p[1])));
    }
}
