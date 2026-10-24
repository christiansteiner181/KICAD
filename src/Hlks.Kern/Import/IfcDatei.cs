// Eine IFC-Datei lesen (Modul 1b): Fassung prüfen, Einheiten, Lagen (Platzierungen) in Welt- und Projektkoordinaten,
// räumliche Gliederung, Typobjekte, Property Sets. Alles über die IFC4-Schnittstellen von xBIM, die auch IFC2x3-Dateien
// abbilden – so sieht der übrige Import beide Fassungen gleich.
// Entscheid Christian 17.10.2026 «IFC 4 und alle zuvor»: gelesen werden IFC4 und IFC2x3; IFC 4.3 ist nicht vorgesehen,
// ältere Fassungen (IFC2x2 und früher) kann xBIM nicht lesen → klare Meldung.

using System.Reflection;
using Xbim.Common;
using Xbim.Common.Step21;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;

namespace Hlks.Kern;

/// <summary>Lage des Projekts in den IFC-Weltkoordinaten: Weltpunkt (mm), der im Projekt Ursprung und ±0.00 ist, und
/// Drehung (Grad) der IFC-Achsen gegen die Projektachsen. Modell = DrehungZ(−drehung) · (Welt − [x, y, z]).</summary>
public sealed record IfcBezug(double X, double Y, double Z, double Drehung)
{
    public System.Text.Json.Nodes.JsonObject AlsJson() => new() { ["x"] = Math.Round(X, 1), ["y"] = Math.Round(Y, 1), ["z"] = Math.Round(Z, 1), ["drehung"] = Math.Round(Drehung, 6) };

    public static IfcBezug? Aus(System.Text.Json.Nodes.JsonNode? n) =>
        n is System.Text.Json.Nodes.JsonObject o && Json.Zahl(o["x"]) is double x && Json.Zahl(o["y"]) is double y && Json.Zahl(o["z"]) is double z
            ? new IfcBezug(x, y, z, Json.Zahl(o["drehung"]) ?? 0) : null;
}

/// <summary>Georeferenz einer Datei (IFC4 IfcMapConversion bzw. die bei Revit übliche Ersatzangabe ePSet_MapConversion):
/// Landeskoordinaten des Weltursprungs der Datei, Drehung der Datei-x-Achse gegen Ost (Grad), Massstab.</summary>
public sealed record IfcKarte(string? Bezugssystem, double OstM, double NordM, double HoeheM, double Drehung, double Massstab, string Herkunft)
{
    /// <summary>Landeskoordinaten (m) eines Weltpunkts der Datei (mm).</summary>
    public double[] Landes(double[] weltMm)
    {
        double w = Drehung * Math.PI / 180, x = weltMm[0] / 1000 * Massstab, y = weltMm[1] / 1000 * Massstab;
        return [OstM + x * Math.Cos(w) - y * Math.Sin(w), NordM + x * Math.Sin(w) + y * Math.Cos(w), HoeheM + weltMm[2] / 1000];
    }
}

/// <summary>Lagebericht einer IFC-Datei: wo Gelände und Gebäude in den Weltkoordinaten der Datei liegen, ihre
/// Georeferenz, die Höhe von ±0.00 und ein Koordinationskörper, falls vorhanden. Grundlage für die transparente
/// Ausrichtung (Christian 17.10.2026: «mit Revit haben wir immer Probleme bei der Ausrichtung … muss transparent
/// aufgebaut werden»).</summary>
public sealed record IfcLage(
    double[] GelaendeWelt, double GelaendeDrehung,
    double[] GebaeudeWelt, double GebaeudeDrehung,
    double NullWeltZ,
    double? RefBreite, double? RefLaenge, double? RefHoehe,
    IfcKarte? Karte,
    double[]? KoordinationskoerperWelt, double? KoordinationskoerperDrehung, string? KoordinationskoerperName);

/// <summary>Ein Wert aus einem Property Set oder einer Mengenangabe: Inhalt und IFC-Messart (z. B. IfcLengthMeasure).</summary>
public sealed record IfcWert(object? Inhalt, string Art);

/// <summary>Ein Geschoss der Datei (gleich hohe Geschosse mehrerer Gebäude zusammengefasst).</summary>
public sealed record IfcGeschoss(IReadOnlyList<IIfcBuildingStorey> Quellen, string Name, double KoteMm)
{
    public string GlobalId => Quellen[0].GlobalId.ToString();
}

public sealed class IfcDatei : IDisposable
{
    public IfcStore Modell { get; }
    public string Pfad { get; }
    /// <summary>IFC4 | IFC2X3</summary>
    public string Schema { get; }
    public string Programm { get; }
    /// <summary>Millimeter je Längeneinheit der Datei (Meter-Datei: 1000).</summary>
    public double MmJeEinheit { get; }
    public IfcBezug Bezug { get; set; } = new(0, 0, 0, 0);

    readonly Dictionary<int, Matrix4> lagen = [];
    Dictionary<int, IIfcObjectDefinition>? eltern;
    Dictionary<int, IIfcTypeObject>? typen;
    Dictionary<int, List<IIfcPropertySetDefinition>>? psets;
    List<IfcGeschoss>? geschosse;
    double geschosseZ;

    IfcDatei(IfcStore modell, string pfad)
    {
        Modell = modell; Pfad = pfad;
        Schema = modell.SchemaVersion == XbimSchemaVersion.Ifc2X3 ? "IFC2X3" : "IFC4";
        var app = modell.Instances.FirstOrDefault<IIfcApplication>();
        Programm = $"{app?.ApplicationFullName} {app?.Version}".Trim();
        MmJeEinheit = modell.ModelFactors.LengthToMetresConversionFactor * 1000;
    }

    /// <summary>Bis zu dieser Dateigrösse (MB) liest xBIM die Datei in den Arbeitsspeicher, darüber in eine Datenbank.</summary>
    const double SpeicherSchwelleMb = 2000;

    /// <summary>Öffnet eine IFC-Datei. Nur IFC4 und IFC2x3 (Entscheid 17.10.2026); sonst HlksFehler mit Vorschlag.</summary>
    public static IfcDatei Oeffnen(string pfad)
    {
        if (!File.Exists(pfad)) throw new HlksFehler($"IFC-Datei {pfad} gibt es nicht.", "Pfad prüfen (Netzlaufwerk verbunden?).");
        var fassung = FassungAusKopf(pfad);
        if (fassung is not null && !(fassung.StartsWith("IFC2X3") || fassung == "IFC4"))
            throw new HlksFehler($"Die Datei ist {fassung}. KICAD liest IFC4 und IFC2x3 (Entscheid 17.10.2026: «IFC 4 und alle zuvor»).",
                "Im Programm des Absenders als IFC4 oder IFC2x3 ausgeben lassen.");
        IfcStore m;
        // Im Arbeitsspeicher öffnen bis 2 GB: xBIM legt sonst ab 100 MB eine Esent-Datenbank auf der Festplatte an – das
        // Architekten-IFC Bernstrasse (106 MB) brauchte so 57 s statt 30 s (Messung Hauptlauf 18.10.2026); Speicherbedarf
        // im Arbeitsspeicher rund das Fünffache der Datei.
        try { m = IfcStore.Open(pfad, null, SpeicherSchwelleMb); }
        catch (Exception ex) when (ex is not HlksFehler)
        {
            throw new HlksFehler($"IFC-Datei {Path.GetFileName(pfad)} nicht lesbar: {ex.Message}", "Datei als IFC4 oder IFC2x3 (STEP, .ifc) neu ausgeben lassen.");
        }
        if (m.SchemaVersion is not (XbimSchemaVersion.Ifc4 or XbimSchemaVersion.Ifc2X3))
        {
            var s = m.SchemaVersion;
            m.Dispose();
            throw new HlksFehler($"Die Datei ist {s}. KICAD liest IFC4 und IFC2x3 (Entscheid 17.10.2026: «IFC 4 und alle zuvor»).",
                "Im Programm des Absenders als IFC4 oder IFC2x3 ausgeben lassen.");
        }
        return new IfcDatei(m, pfad);
    }

    /// <summary>FILE_SCHEMA aus dem Kopf der STEP-Datei (ohne das ganze Modell zu laden), z. B. IFC4, IFC2X3, IFC4X3_ADD2.</summary>
    public static string? FassungAusKopf(string pfad)
    {
        using var r = new StreamReader(pfad);
        for (int i = 0; i < 60 && r.ReadLine() is { } z; i++)
        {
            var k = z.IndexOf("FILE_SCHEMA", StringComparison.OrdinalIgnoreCase);
            if (k < 0) continue;
            var a = z.IndexOf('\'', k); var b = a < 0 ? -1 : z.IndexOf('\'', a + 1);
            return a >= 0 && b > a ? z[(a + 1)..b].ToUpperInvariant() : null;
        }
        return null;
    }

    public void Dispose() => Modell.Dispose();

    // ------------------------------------------------------------------ Lagen

    /// <summary>Lage einer Platzierung in Weltkoordinaten (mm).</summary>
    public Matrix4 Welt(IIfcObjectPlacement? p)
    {
        if (p is null) return Matrix4.Einheit;
        if (lagen.TryGetValue(p.EntityLabel, out var m)) return m;
        m = p switch
        {
            IIfcLocalPlacement lp => Welt(lp.PlacementRelTo) * Achsen(lp.RelativePlacement),
            _ => Matrix4.Einheit,   // IfcGridPlacement: selten, vorerst ohne Lage (Hinweis im Import)
        };
        lagen[p.EntityLabel] = m;
        return m;
    }

    /// <summary>Achsensystem einer IfcAxis2Placement (2D oder 3D) in mm.</summary>
    public Matrix4 Achsen(IIfcAxis2Placement? a) => a switch
    {
        IIfcAxis2Placement3D p3 => Matrix4.Achsen(Punkt(p3.Location), Richtung(p3.RefDirection), Richtung(p3.Axis)),
        IIfcAxis2Placement2D p2 => Matrix4.Achsen(Punkt(p2.Location), Richtung(p2.RefDirection), null),
        _ => Matrix4.Einheit,
    };

    /// <summary>Punkt in mm (Koordinaten der Datei × Einheit).</summary>
    public double[] Punkt(IIfcCartesianPoint? p)
    {
        if (p is null) return [0, 0, 0];
        var c = p.Coordinates.Select(x => (double)x * MmJeEinheit).ToArray();
        return [c.Length > 0 ? c[0] : 0, c.Length > 1 ? c[1] : 0, c.Length > 2 ? c[2] : 0];
    }

    public static double[]? Richtung(IIfcDirection? d)
    {
        if (d is null) return null;
        var r = d.DirectionRatios.Select(x => (double)x).ToArray();
        return [r.Length > 0 ? r[0] : 0, r.Length > 1 ? r[1] : 0, r.Length > 2 ? r[2] : 0];
    }

    /// <summary>Von Weltkoordinaten (mm) in Projektkoordinaten (mm) nach dem Bezug.</summary>
    public Matrix4 ZuModell => Matrix4.DrehungZ(-Bezug.Drehung) * Matrix4.Verschiebung(-Bezug.X, -Bezug.Y, -Bezug.Z);

    public double[] Modellpunkt(double[] welt) => ZuModell.Punkt(welt);

    /// <summary>Lage eines Produkts in Projektkoordinaten.</summary>
    public Matrix4 Lage(IIfcProduct p) => ZuModell * Welt(p.ObjectPlacement);

    /// <summary>Vorschlag für den Bezug: Ursprung und Drehung des (ersten) Gebäudes; z so, dass die Geschosskoten (Elevation)
    /// stimmen – Median über alle Geschosse von (Weltlage z − Elevation).</summary>
    public IfcBezug BezugVorschlag()
    {
        var geb = Modell.Instances.FirstOrDefault<IIfcBuilding>();
        var wg = Welt(geb?.ObjectPlacement);
        var kandidaten = Modell.Instances.OfType<IIfcBuildingStorey>()
            .Where(s => s.Elevation.HasValue)
            .Select(s => Welt(s.ObjectPlacement).Ursprung[2] - (double)s.Elevation!.Value * MmJeEinheit).Order().ToList();
        double z = kandidaten.Count > 0 ? kandidaten[kandidaten.Count / 2] : wg.Ursprung[2];
        return new IfcBezug(wg.Ursprung[0], wg.Ursprung[1], z, Math.Round(wg.WinkelZ, 9));
    }

    /// <summary>Namen, an denen ein Koordinationskörper erkannt wird (Name oder ObjectType enthält einen davon).</summary>
    public static readonly string[] KoordinationskoerperNamen = ["koordinationsk", "nullpunktk", "coordination body", "koordinationswuerfel", "koordinationswürfel"];

    /// <summary>Lagebericht der Datei (siehe IfcLage).</summary>
    public IfcLage Lage(string? koordinationskoerper = null)
    {
        var site = Modell.Instances.FirstOrDefault<IIfcSite>();
        var geb = Modell.Instances.FirstOrDefault<IIfcBuilding>();
        var ws = Welt(site?.ObjectPlacement); var wg = Welt(geb?.ObjectPlacement);
        var namen = koordinationskoerper is { Length: > 0 } ? [koordinationskoerper.ToLowerInvariant()] : KoordinationskoerperNamen;
        var kk = Modell.Instances.OfType<IIfcProduct>().FirstOrDefault(p =>
            namen.Any(n => (p.Name?.ToString() ?? "").ToLowerInvariant().Contains(n) || (p.ObjectType?.ToString() ?? "").ToLowerInvariant().Contains(n)));
        var wk = kk is null ? (Matrix4?)null : Welt(kk.ObjectPlacement);
        // Befund B4-4: Trimble Nova schreibt Landeskoordinaten (1200380, 2600830) in RefLatitude/RefLongitude – nur echte
        // Winkel (|Breite| ≤ 90°, |Länge| ≤ 180°) gelten als geografische Lage
        double? breite = Winkel(site?.RefLatitude), laenge = Winkel(site?.RefLongitude);
        if (breite is { } b && Math.Abs(b) > 90 || laenge is { } l && Math.Abs(l) > 180) { breite = null; laenge = null; }
        return new IfcLage(ws.Ursprung, Math.Round(ws.WinkelZ, 9), wg.Ursprung, Math.Round(wg.WinkelZ, 9), BezugVorschlag().Z,
            breite, laenge, site?.RefElevation is { } re ? (double)re * MmJeEinheit / 1000 : null,
            Karte(), wk?.Ursprung, wk is { } m ? Math.Round(m.WinkelZ, 9) : null,
            kk is null ? null : IfcLauf.Text(kk.Name) ?? IfcLauf.Text(kk.ObjectType));
    }

    static double? Winkel(object? zusammengesetzt)
    {
        // IfcCompoundPlaneAngleMeasure: Grad, Minuten, Sekunden, Millionstelsekunden
        if (zusammengesetzt?.GetType().GetProperty("Value")?.GetValue(zusammengesetzt) is not System.Collections.IEnumerable teile) return null;
        var t = teile.Cast<object>().Select(Convert.ToDouble).ToArray();
        if (t.Length < 3) return null;
        double s = Math.Sign(t[0] != 0 ? t[0] : t[1] != 0 ? t[1] : t[2]);
        return s * (Math.Abs(t[0]) + Math.Abs(t[1]) / 60 + (Math.Abs(t[2]) + (t.Length > 3 ? Math.Abs(t[3]) / 1e6 : 0)) / 3600);
    }

    /// <summary>Georeferenz: IFC4 IfcMapConversion, sonst ePSet_MapConversion/ePSet_ProjectedCRS am Projekt oder Gelände
    /// (Ersatz in IFC2x3, z. B. aus Revit). null, wenn die Datei keine hat.</summary>
    public IfcKarte? Karte()
    {
        if (Modell.Instances.FirstOrDefault<IIfcMapConversion>() is { } mc)
        {
            double ab = mc.XAxisAbscissa.HasValue ? (double)mc.XAxisAbscissa.Value : 1, or = mc.XAxisOrdinate.HasValue ? (double)mc.XAxisOrdinate.Value : 0;
            return new IfcKarte(IfcLauf.Text(mc.TargetCRS?.Name),
                (double)mc.Eastings * MmJeEinheit / 1000, (double)mc.Northings * MmJeEinheit / 1000, (double)mc.OrthogonalHeight * MmJeEinheit / 1000,
                Math.Atan2(or, ab) * 180 / Math.PI, mc.Scale.HasValue ? (double)mc.Scale.Value : 1, "IfcMapConversion");
        }
        foreach (var o in new IIfcObject?[] { Modell.Instances.FirstOrDefault<IIfcProject>() as IIfcObject, Modell.Instances.FirstOrDefault<IIfcSite>() })
        {
            if (o is null) continue;
            var e = Eigenschaften(o);
            if (e.TryGetValue("ePSet_MapConversion.Eastings", out var ost) && e.TryGetValue("ePSet_MapConversion.Northings", out var nord))
            {
                double Z(string n, double v) => e.TryGetValue("ePSet_MapConversion." + n, out var w) && w.Inhalt is double d ? d : v;
                double ab = Z("XAxisAbscissa", 1), or = Z("XAxisOrdinate", 0);
                var crs = e.TryGetValue("ePSet_ProjectedCRS.Name", out var c) ? c.Inhalt?.ToString() : null;
                return new IfcKarte(crs, (ost.Inhalt is double a ? a : 0) * MmJeEinheit / 1000, (nord.Inhalt is double b ? b : 0) * MmJeEinheit / 1000,
                    Z("OrthogonalHeight", 0) * MmJeEinheit / 1000, Math.Atan2(or, ab) * 180 / Math.PI, Z("Scale", 1), "ePSet_MapConversion");
            }
        }
        return null;
    }

    // ------------------------------------------------------------------ Gliederung

    /// <summary>Geschosse der Datei, gleich hohe (±10 mm) zusammengefasst, aufsteigend; Kote in Projektkoordinaten. Zwischengespeichert für
    /// das z des Bezugs, mit dem sie gerechnet sind (IFC einpassen liest die Datei vorher in der Lage «Gebäudeursprung»).</summary>
    public IReadOnlyList<IfcGeschoss> Geschosse()
    {
        if (geschosse is not null && geschosseZ == Bezug.Z) return geschosse;
        geschosseZ = Bezug.Z;
        var liste = new List<(IIfcBuildingStorey S, double Kote)>();
        foreach (var s in Modell.Instances.OfType<IIfcBuildingStorey>())
        {
            double kote = s.Elevation.HasValue ? (double)s.Elevation.Value * MmJeEinheit + (BezugVorschlagZ() - Bezug.Z)
                : Modellpunkt(Welt(s.ObjectPlacement).Ursprung)[2];
            liste.Add((s, Math.Round(kote, 1)));
        }
        geschosse = [];
        foreach (var g in liste.OrderBy(x => x.Kote).GroupBy(x => Math.Round(x.Kote / 10)))
        {
            var quellen = g.Select(x => x.S).ToList();
            var name = quellen.Select(q => q.LongName?.ToString() is { Length: > 0 } ln && ln != q.Name?.ToString() ? ln : q.Name?.ToString())
                .Where(n => !string.IsNullOrWhiteSpace(n)).OrderByDescending(n => n!.Length).FirstOrDefault() ?? $"Geschoss {g.First().Kote / 1000:0.00}";
            geschosse.Add(new IfcGeschoss(quellen, name!, g.First().Kote));
        }
        return geschosse;
    }

    double? bezugZ;
    /// <summary>Elevation ist relativ zum Gebäude-±0 der Datei; dessen Weltlage ist das z des Vorschlags.</summary>
    double BezugVorschlagZ() => bezugZ ??= BezugVorschlag().Z;

    /// <summary>Geschoss, in dem ein Produkt steht (Enthaltensein oder Zerlegung bis zum Geschoss); null, wenn keins.</summary>
    public IfcGeschoss? GeschossVon(IIfcObjectDefinition o)
    {
        ElternAufbauen();
        var g = Geschosse();
        for (var x = o; x is not null; x = eltern!.GetValueOrDefault(x.EntityLabel))
            if (x is IIfcBuildingStorey s) return g.FirstOrDefault(q => q.Quellen.Any(y => y.EntityLabel == s.EntityLabel));
        return null;
    }

    /// <summary>Übergeordnetes Objekt (räumliche Struktur oder Aggregat) oder null.</summary>
    public IIfcObjectDefinition? Eltern(IIfcObjectDefinition o) { ElternAufbauen(); return eltern!.GetValueOrDefault(o.EntityLabel); }

    void ElternAufbauen()
    {
        if (eltern is not null) return;
        eltern = [];
        foreach (var r in Modell.Instances.OfType<IIfcRelAggregates>())
            foreach (var k in r.RelatedObjects) eltern[k.EntityLabel] = r.RelatingObject;
        foreach (var r in Modell.Instances.OfType<IIfcRelContainedInSpatialStructure>())
            foreach (var k in r.RelatedElements) eltern.TryAdd(k.EntityLabel, r.RelatingStructure);
    }

    // ------------------------------------------------------------------ Typ und Eigenschaften

    public IIfcTypeObject? Typ(IIfcObject o)
    {
        if (typen is null)
        {
            typen = [];
            foreach (var r in Modell.Instances.OfType<IIfcRelDefinesByType>())
                foreach (var k in r.RelatedObjects) typen[k.EntityLabel] = r.RelatingType;
        }
        return typen.GetValueOrDefault(o.EntityLabel);
    }

    /// <summary>Entitätsname, z. B. IfcFlowSegment.</summary>
    public static string Entitaet(IPersistEntity e) => e.ExpressType.Name;

    /// <summary>PredefinedType des Exemplars (IFC4) oder sonst des Typobjekts; null, wenn keiner.</summary>
    public string? PredefinedType(IIfcObject o)
    {
        var w = Vordefiniert(o);
        if (w is null or "NOTDEFINED") w = Typ(o) is { } t ? Vordefiniert(t) ?? w : w;
        return w;
    }

    static string? Vordefiniert(object o) =>
        o.GetType().GetProperty("PredefinedType", BindingFlags.Public | BindingFlags.Instance)?.GetValue(o)?.ToString();

    /// <summary>ObjectType des Exemplars, sonst ElementType des Typobjekts (bei USERDEFINED sagt er, was es ist).</summary>
    public string? ObjectType(IIfcObject o)
    {
        var s = o.ObjectType?.ToString();
        if (string.IsNullOrWhiteSpace(s) && Typ(o) is IIfcElementType et) s = et.ElementType?.ToString();
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }

    /// <summary>Alle Property-Set- und Mengenwerte eines Objekts samt Typobjekt: «Pset.Name» → Wert. Werte des Exemplars
    /// gehen denen des Typs vor.</summary>
    public Dictionary<string, IfcWert> Eigenschaften(IIfcObject o)
    {
        if (psets is null)
        {
            psets = [];
            foreach (var r in Modell.Instances.OfType<IIfcRelDefinesByProperties>())
            {
                // IFC4: auch eine Menge von Definitionen (IfcPropertySetDefinitionSet, Wert = Liste)
                var defs = r.RelatingPropertyDefinition switch
                {
                    IIfcPropertySetDefinition d => [d],
                    { } menge when menge.GetType().GetProperty("Value")?.GetValue(menge) is System.Collections.IEnumerable liste
                        => liste.OfType<IIfcPropertySetDefinition>().ToList(),
                    _ => new List<IIfcPropertySetDefinition>(),
                };
                foreach (var k in r.RelatedObjects)
                {
                    if (!psets.TryGetValue(k.EntityLabel, out var l)) psets[k.EntityLabel] = l = [];
                    l.AddRange(defs);
                }
            }
        }
        var aus = new Dictionary<string, IfcWert>(StringComparer.OrdinalIgnoreCase);
        void Lesen(IEnumerable<IIfcPropertySetDefinition> defs, bool ueberschreiben)
        {
            foreach (var d in defs)
            {
                var name = d.Name?.ToString() ?? "";
                if (d is IIfcPropertySet ps)
                    foreach (var p in ps.HasProperties.OfType<IIfcPropertySingleValue>())
                        Setzen($"{name}.{p.Name}", p.NominalValue is null ? null : new IfcWert(p.NominalValue.Value, p.NominalValue.GetType().Name), ueberschreiben);
                else if (d is IIfcElementQuantity q)
                    foreach (var m in q.Quantities)
                        Setzen($"{name}.{m.Name}", m switch
                        {
                            IIfcQuantityLength l => new IfcWert((double)l.LengthValue, "IfcLengthMeasure"),
                            IIfcQuantityArea a => new IfcWert((double)a.AreaValue, "IfcAreaMeasure"),
                            IIfcQuantityVolume v => new IfcWert((double)v.VolumeValue, "IfcVolumeMeasure"),
                            IIfcQuantityCount c => new IfcWert((double)c.CountValue, "IfcCountMeasure"),
                            _ => null,
                        }, ueberschreiben);
            }
        }
        void Setzen(string schluessel, IfcWert? w, bool ueberschreiben)
        {
            if (w is null) return;
            if (ueberschreiben || !aus.ContainsKey(schluessel)) aus[schluessel] = w;
        }
        if (Typ(o)?.HasPropertySets is { } tps) Lesen(tps, false);
        if (psets.TryGetValue(o.EntityLabel, out var eig)) Lesen(eig, true);
        return aus;
    }

    /// <summary>Länge in mm aus einem Property-Set-Wert. Messart Länge (…LengthMeasure): × Einheit der Datei. Reine Zahl
    /// (IfcReal, IfcInteger …) trägt keine Einheit: in einer mm-Datei mm; sonst × Einheit, ausser der Wert ist dafür
    /// unplausibel gross (≥ 10, also ≥ 10 m bei Metern) – dann ist er schon in mm. Befund B2 vom 17.10.2026: Nova schreibt
    /// Masse als reine Zahl in mm in eine Meter-Datei.</summary>
    public double? LaengeMm(IfcWert? w)
    {
        double? zahl = w?.Inhalt switch { double d => d, long l => l, int i => i, _ => null };
        if (zahl is not double z) return null;
        bool mass = w!.Art.Contains("LengthMeasure", StringComparison.OrdinalIgnoreCase);
        if (mass || MmJeEinheit == 1) return z * MmJeEinheit;
        return Math.Abs(z) * MmJeEinheit >= 10_000 ? z : z * MmJeEinheit;
    }

    /// <summary>Erster vorhandener Wert aus einer Liste von Kandidaten «Pset.Name» (Gross-/Kleinschreibung egal).</summary>
    public static IfcWert? Erster(Dictionary<string, IfcWert> eig, IEnumerable<string> kandidaten)
    {
        foreach (var k in kandidaten) if (eig.TryGetValue(k, out var w)) return w;
        return null;
    }
}
