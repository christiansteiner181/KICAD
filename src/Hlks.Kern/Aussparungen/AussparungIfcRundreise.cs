// IFC-Ausgabe der Aussparungen (Modul 3b, B4b): Rundreise-Prüfung. Die geschriebene Datei wird mit dem eigenen IFC-Leser (IfcDatei,
// Xbim) wieder geöffnet und gegen die Daten verglichen, die geschrieben werden sollten – so wie ein Empfänger sie lesen würde:
//   – Datei öffnet sich, Fassung und Einheit (mm) stimmen,
//   – genau ein IfcBuildingElementProxy je Aussparung, mit der GlobalId, dem Namen (Nummer), PredefinedType PROVISIONFORVOID (IFC4)
//     bzw. ObjectType «ProvisionForVoid» (IFC2X3), Beschreibung und Tag,
//   – Geschoss (Name, Kote) und Gliederung (im Gebäude bzw. Geschoss enthalten),
//   – Lage: Mitte des Körpers in Projektkoordinaten (über den Bezug zurückgerechnet, auf 0.1 mm), Achse, Form und Masse,
//   – alle Werte des Property Sets,
//   – die Schema-Regeln von Xbim (ExpressValidation) für jede Entität.
// Eine Abweichung verhindert das Schreiben der Zieldatei (AussparungIfcExport).

using System.Globalization;
using System.Text.Json.Nodes;
using Xbim.Common;
using Xbim.Common.ExpressValidation;
using Xbim.Ifc4.Interfaces;

namespace Hlks.Kern;

public sealed record RundreiseErgebnis(string Fassung, double MmJeEinheit, int Geprueft, int GeschosseGeprueft, int EigenschaftenGeprueft, int Entitaeten,
    JsonObject Validierung, List<string> Abweichungen, List<string> Hinweise);

public static class AussparungIfcRundreise
{
    /// <summary>Toleranz der Lage und der Masse (mm).</summary>
    public const double ToleranzMm = 0.1;

    static string Z(double x) => x.ToString("0.####", CultureInfo.InvariantCulture);

    public static RundreiseErgebnis Pruefen(IfcAusgabe soll, string pfad)
    {
        var abw = new List<string>();
        var hinweise = new List<string>();
        IfcDatei d;
        try { d = IfcDatei.Oeffnen(pfad); }
        catch (HlksFehler f) { return new(soll.Fassung, 0, 0, 0, 0, 0, new JsonObject(), [$"Die geschriebene Datei lässt sich nicht öffnen: {f.Message}"], hinweise); }
        using (d)
        {
            if (d.Schema != soll.Fassung) abw.Add($"Fassung {d.Schema} statt {soll.Fassung}");
            if (Math.Abs(d.MmJeEinheit - 1) > 1e-9) abw.Add($"Längeneinheit: {Z(d.MmJeEinheit)} mm je Einheit statt Millimeter");
            // der Bezug, mit dem der Empfänger die Datei in die Projektkoordinaten zurückholt (wie der Import)
            d.Bezug = soll.Bezug ?? new IfcBezug(0, 0, 0, 0);
            int eigGeprueft = 0, geschGeprueft = 0;

            // Gebäude im Bezug
            if (soll.Bezug is { } sb)
            {
                var vorschlag = d.BezugVorschlag();
                if (Math.Abs(vorschlag.X - sb.X) > ToleranzMm || Math.Abs(vorschlag.Y - sb.Y) > ToleranzMm || Math.Abs(vorschlag.Z - sb.Z) > ToleranzMm
                    || Math.Abs(vorschlag.Drehung - sb.Drehung) > 1e-6)
                    abw.Add($"Gebäudeursprung der Datei [{Z(vorschlag.X)}, {Z(vorschlag.Y)}, {Z(vorschlag.Z)}] {Z(vorschlag.Drehung)}° statt dem Projektbezug [{Z(sb.X)}, {Z(sb.Y)}, {Z(sb.Z)}] {Z(sb.Drehung)}°");
            }

            // Geschosse
            var gelesen = d.Geschosse();
            foreach (var g in soll.Geschosse)
            {
                geschGeprueft++;
                var ig = gelesen.FirstOrDefault(x => x.Quellen.Any(q => q.Name?.ToString() == g.Kennzeichen));
                if (ig is null) { abw.Add($"Geschoss {g.Kennzeichen} fehlt in der Datei"); continue; }
                if (Math.Abs(ig.KoteMm - g.Kote) > ToleranzMm) abw.Add($"Geschoss {g.Kennzeichen}: Kote {Z(ig.KoteMm)} statt {Z(g.Kote)} mm");
                if (ig.Quellen[0].GlobalId.ToString() != g.GlobalId) abw.Add($"Geschoss {g.Kennzeichen}: GlobalId {ig.Quellen[0].GlobalId} statt {g.GlobalId}");
                if (!string.IsNullOrWhiteSpace(g.Name) && g.Name != g.Kennzeichen && ig.Quellen[0].LongName?.ToString() != g.Name)
                    abw.Add($"Geschoss {g.Kennzeichen}: Langname «{ig.Quellen[0].LongName}» statt «{g.Name}»");
                var eg = d.Eigenschaften(ig.Quellen[0]);
                eigGeprueft += Vergleichen($"Geschoss {g.Kennzeichen}", eg, d, AussparungIfcExport.PsetGeschoss,
                    new List<IfcEigenschaft> { new("Kote", "laenge", Laenge: g.Kote) }.Concat(g.Hoehe is double h ? [new IfcEigenschaft("Hoehe", "laenge", Laenge: h)] : []), abw);
            }
            if (gelesen.Count != soll.Geschosse.Count) abw.Add($"{gelesen.Count} Geschosse in der Datei statt {soll.Geschosse.Count}");

            // Aussparungen
            var proxys = d.Modell.Instances.OfType<IIfcBuildingElementProxy>().ToList();
            if (proxys.Count != soll.Aussparungen.Count) abw.Add($"{proxys.Count} IfcBuildingElementProxy in der Datei statt {soll.Aussparungen.Count}");
            var nachGid = proxys.GroupBy(x => x.GlobalId.ToString()).ToDictionary(x => x.Key, x => x.ToList());
            foreach (var a in soll.Aussparungen)
            {
                var wer = $"{a.Kennzeichen} ({a.Name})";
                if (!nachGid.TryGetValue(a.GlobalId, out var liste)) { abw.Add($"{wer}: GlobalId {a.GlobalId} fehlt in der Datei"); continue; }
                if (liste.Count > 1) abw.Add($"{wer}: GlobalId {a.GlobalId} kommt {liste.Count}-mal vor");
                var p = liste[0];
                if (IfcDatei.Entitaet(p) != "IfcBuildingElementProxy") abw.Add($"{wer}: Entität {IfcDatei.Entitaet(p)}");
                if (p.Name?.ToString() != a.Name) abw.Add($"{wer}: Name «{p.Name}» statt «{a.Name}»");
                if (p.Description?.ToString() != a.Beschreibung) abw.Add($"{wer}: Beschreibung «{p.Description}» statt «{a.Beschreibung}»");
                if (p.Tag?.ToString() != a.Kennzeichen) abw.Add($"{wer}: Tag «{p.Tag}» statt «{a.Kennzeichen}»");
                if (soll.Fassung == AussparungIfcExport.Fassung4)
                {
                    if (d.PredefinedType(p) != AussparungIfcExport.PredefinedType4) abw.Add($"{wer}: PredefinedType {d.PredefinedType(p) ?? "–"} statt {AussparungIfcExport.PredefinedType4}");
                }
                else if (p.ObjectType?.ToString() != AussparungIfcExport.ObjectType2x3) abw.Add($"{wer}: ObjectType «{p.ObjectType}» statt «{AussparungIfcExport.ObjectType2x3}»");

                // Geschoss
                var gg = d.GeschossVon(p);
                var erwartetG = a.GeschossId is { } gid ? soll.Geschosse.First(x => x.Id == gid).Kennzeichen : null;
                if ((gg?.Quellen.Select(q => q.Name?.ToString()).FirstOrDefault(n => n == erwartetG)) != erwartetG) abw.Add($"{wer}: im Geschoss «{gg?.Name ?? "–"}» statt «{erwartetG ?? "–"}»");
                if (erwartetG is null && d.Eltern(p) is not IIfcBuilding) abw.Add($"{wer}: nicht im Gebäude enthalten");

                // Lage und Körper
                var koerper = p.Representation?.Representations.SelectMany(r => r.Items).OfType<IIfcExtrudedAreaSolid>().ToList() ?? [];
                if (koerper.Count != 1) { abw.Add($"{wer}: {koerper.Count} Körper (IfcExtrudedAreaSolid) statt einem"); continue; }
                KoerperPruefen(d, p, koerper[0], a, wer, abw);

                eigGeprueft += Vergleichen(wer, d.Eigenschaften(p), d, AussparungIfcExport.PsetAussparung, a.Eigenschaften, abw);
            }

            // Schema-Regeln (Xbim): Pflichtattribute, Wertebereiche, WHERE-Regeln
            var validierung = Validieren(d, abw, hinweise);
            return new(d.Schema, d.MmJeEinheit, soll.Aussparungen.Count - abw.Count(x => x.Contains("fehlt in der Datei")), geschGeprueft, eigGeprueft,
                (int)d.Modell.Instances.Count, validierung, abw, hinweise);
        }
    }

    static void KoerperPruefen(IfcDatei d, IIfcBuildingElementProxy p, IIfcExtrudedAreaSolid k, IfcAussparungDaten a, string wer, List<string> abw)
    {
        var lage = a.Lage;
        var mm = d.MmJeEinheit;
        double Tiefe = (double)k.Depth * mm;
        if (Math.Abs(Tiefe - lage.Tiefe) > ToleranzMm) abw.Add($"{wer}: Tiefe {Z(Tiefe)} statt {Z(lage.Tiefe)} mm");
        var gesamt = d.Lage(p) * d.Achsen(k.Position);
        var z = V3.Aus(IfcDatei.Richtung(k.ExtrudedDirection) ?? [0, 0, 1]).Einheit();
        var profilMitte = k.SweptArea switch
        {
            IIfcParameterizedProfileDef pp => d.Achsen(pp.Position).Ursprung,
            _ => [0.0, 0, 0],
        };
        // Mitte des Körpers = Mitte des Profils, um die halbe Tiefe in Extrusionsrichtung
        var mitte = gesamt.Punkt([profilMitte[0] + z.X * Tiefe / 2, profilMitte[1] + z.Y * Tiefe / 2, profilMitte[2] + z.Z * Tiefe / 2]);
        double dist = Math.Sqrt(Math.Pow(mitte[0] - lage.Mitte.X, 2) + Math.Pow(mitte[1] - lage.Mitte.Y, 2) + Math.Pow(mitte[2] - lage.Mitte.Z, 2));
        if (dist > ToleranzMm) abw.Add($"{wer}: Mitte [{Z(mitte[0])}, {Z(mitte[1])}, {Z(mitte[2])}] statt [{Z(lage.Mitte.X)}, {Z(lage.Mitte.Y)}, {Z(lage.Mitte.Z)}] ({Z(dist)} mm daneben)");
        var achse = gesamt.Richtung(z.Feld());
        var ach = V3.Aus(achse).Einheit();
        if (ach.Mal(lage.Achse) < 1 - 1e-9) abw.Add($"{wer}: Achse [{Z(ach.X)}, {Z(ach.Y)}, {Z(ach.Z)}] statt [{Z(lage.Achse.X)}, {Z(lage.Achse.Y)}, {Z(lage.Achse.Z)}]");
        switch (k.SweptArea)
        {
            case IIfcCircleProfileDef kr:
                if (!lage.Rund) abw.Add($"{wer}: Kreisprofil statt Rechteck");
                else if (Math.Abs((double)kr.Radius * mm * 2 - lage.D) > ToleranzMm) abw.Add($"{wer}: Durchmesser {Z((double)kr.Radius * mm * 2)} statt {Z(lage.D)} mm");
                break;
            case IIfcRectangleProfileDef re:
                if (lage.Rund) { abw.Add($"{wer}: Rechteckprofil statt Kreis"); break; }
                double b = (double)re.XDim * mm, h = (double)re.YDim * mm;
                if (Math.Abs(b - lage.B) > ToleranzMm || Math.Abs(h - lage.H) > ToleranzMm) abw.Add($"{wer}: Mass {Z(b)} × {Z(h)} statt {Z(lage.B)} × {Z(lage.H)} mm");
                // Breite (Profil-x) im Projekt: Profilposition, dann Körperlage
                var px = gesamt.Richtung(d.Achsen(re.Position).Richtung([1, 0, 0]));
                if (Math.Abs(V3.Aus(px).Einheit().Mal(lage.U)) < 1 - 1e-9) abw.Add($"{wer}: Breite liegt nicht entlang U der Bauteilebene");
                break;
            default:
                abw.Add($"{wer}: Profil {k.SweptArea?.GetType().Name} statt Rechteck oder Kreis");
                break;
        }
    }

    /// <summary>Vergleicht die erwarteten Werte mit dem Property Set der Datei; liefert die Zahl der geprüften Werte.</summary>
    static int Vergleichen(string wer, Dictionary<string, IfcWert> gelesen, IfcDatei d, string pset, IEnumerable<IfcEigenschaft> erwartet, List<string> abw)
    {
        int n = 0;
        foreach (var e in erwartet)
        {
            if (e.Art == "laenge" ? e.Laenge is null : string.IsNullOrEmpty(e.Text)) continue;   // wird nicht geschrieben
            n++;
            if (!gelesen.TryGetValue($"{pset}.{e.Name}", out var w)) { abw.Add($"{wer}: Eigenschaft {pset}.{e.Name} fehlt"); continue; }
            if (e.Art == "laenge")
            {
                var l = d.LaengeMm(w);
                if (l is not double ll || Math.Abs(ll - e.Laenge!.Value) > ToleranzMm) abw.Add($"{wer}: {pset}.{e.Name} = {w.Inhalt} statt {Z(e.Laenge!.Value)} mm");
            }
            else if (w.Inhalt?.ToString() != e.Text) abw.Add($"{wer}: {pset}.{e.Name} = «{w.Inhalt}» statt «{e.Text}»");
        }
        return n;
    }

    /// <summary>Prüft jede Entität gegen die Schema-Regeln von Xbim (Pflichtattribute, Typen, WHERE-Regeln). Fehler sind Abweichungen.</summary>
    static JsonObject Validieren(IfcDatei d, List<string> abw, List<string> hinweise)
    {
        int geprueft = 0, fehler = 0;
        var beispiele = new JsonArray();
        var validator = new Validator { CreateEntityHierarchy = false, ValidateLevel = Xbim.Common.Enumerations.ValidationFlags.All };
        foreach (var e in d.Modell.Instances)
        {
            geprueft++;
            foreach (var r in validator.Validate(e))
            {
                fehler++;
                var text = $"#{e.EntityLabel} {e.ExpressType.Name}: {r.Message} ({r.IssueSource})";
                if (beispiele.Count < 10) beispiele.Add(text);
                if (fehler <= 5) abw.Add("Schema (Xbim-Validierung) " + text);
            }
        }
        if (fehler > 5) hinweise.Add($"{fehler - 5} weitere Meldungen der Schema-Validierung.");
        return new JsonObject { ["entitaetenGeprueft"] = geprueft, ["meldungen"] = fehler, ["beispiele"] = beispiele };
    }
}
