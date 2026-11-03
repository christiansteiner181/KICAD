// Leitungen (Rohre, Kanäle) aus einer Revit-Datei – fachliche Auslegung der Objekte aus RevitObjekte (Stufe 2,
// entwurf/rvt-format.md). Gefunden an der Geberit-Silent-db20-RVT (Revit 2017), Werte gegen Revits eigene Texte geprüft
// (Durchmesser 0.183727 ft = 56,0 mm, «56 mmø»):
//   Leitung    jede Klasse mit Kurventreiber (m_pCurveDriver) und Querschnitt (m_dWidthOrDiameter, m_dHeight), z. B. RbsPipeCurve.
//   Achse      m_pCurveDriver.m_pCrv: GLine mit m_origin, m_dirVec, m_endParams → Anfang/Ende = Ursprung + Richtung × Parameter.
//   Rohr       m_pDesignPropManager: m_dOuterDiameter, m_dInnerDiameter, m_sCalculatedSize.
//   Bezüge     m_idType (Typ), m_idSegment (Segment), m_idLevelStart (Ebene); System über die Anschlüsse
//              (Connector.m_modifiers → SegmentConnectorCalculation.m_idSystem); verbunden über Connector.m_arrRefs.
//   Namen      Typen m_symbolInfo.m_name, Systeme m_strName, Ebenen m_text, Segmente m_name.
// Einheiten: Revit speichert intern Fuss; hier mm. Koordinaten im internen Projektsystem von Revit (nicht Vermessung).

namespace Hlks.Kern;

/// <summary>Eine Leitung aus einer Revit-Datei (Längen in mm, Koordinaten im internen Projektsystem).
/// <see cref="BreiteOderDurchmesser"/>: bei Rohren die Nennweite (Revits Grösse, z. B. 60 beim Rohr 63 mm), bei Kanälen die Breite.</summary>
public sealed record RevitLeitung(
    long Id, string Klasse, double[]? Anfang, double[]? Ende, double? BreiteOderDurchmesser, double? Hoehe,
    double? Aussendurchmesser, double? Innendurchmesser, string? Groesse,
    long? TypId, string? Typ, long? SegmentId, string? Segment, long? SystemId, string? System,
    long? EbeneId, string? Ebene, IReadOnlyList<long> Verbunden)
{
    public double? Laenge => Anfang is null || Ende is null ? null
        : Math.Sqrt(Math.Pow(Ende[0] - Anfang[0], 2) + Math.Pow(Ende[1] - Anfang[1], 2) + Math.Pow(Ende[2] - Anfang[2], 2));
}

public static class RevitLeitungen
{
    const double MmJeFuss = 304.8;

    /// <summary>Alle Leitungen der Datei mit Namen von Typ, Segment, System und Ebene.</summary>
    public static List<RevitLeitung> Lesen(RevitDatei datei)
    {
        var schema = datei.Schema();
        var namen = new Dictionary<long, string>();
        var roh = new List<(long Id, RevitObjekt O)>();
        foreach (var s in RevitObjekte.Saetze(datei, schema))
        {
            if (s.Kopf || s.Objekt is not { } o) continue;
            if (Name(o) is { } n) namen.TryAdd(s.Id, n);
            if (IstLeitung(schema, o.Klasse)) roh.Add((s.Id, o));
        }
        return roh.Select(r => Leitung(r.Id, r.O, namen)).ToList();
    }

    static bool IstLeitung(RevitSchema s, RevitKlasse k)
    {
        // Dämmung und Auskleidung (RbsPipeInsulation …) haben denselben Aufbau, sind aber keine Leitungen
        if (k.Name.Contains("Insulation", StringComparison.Ordinal) || k.Name.Contains("Lining", StringComparison.Ordinal)) return false;
        bool kurve = false, quer = false;
        foreach (var f in s.AlleFelder(k)) { kurve |= f.Name == "m_pCurveDriver"; quer |= f.Name == "m_dWidthOrDiameter"; }
        return kurve && quer;
    }

    static string? Name(RevitObjekt o) =>
        (o["m_symbolInfo"] as RevitObjekt)?["m_name"] as string ?? o["m_strName"] as string
        ?? (o.Klasse.Name == "Level" ? o["m_text"] as string : null)
        ?? (o.Klasse.Name.EndsWith("Segment", StringComparison.Ordinal) ? o["m_name"] as string : null);

    static RevitLeitung Leitung(long id, RevitObjekt o, Dictionary<long, string> namen)
    {
        double[]? anfang = null, ende = null;
        if ((o["m_pCurveDriver"] as RevitObjekt)?["m_pCrv"] is RevitObjekt { Klasse.Name: "GLine" } linie
            && Zahlen(linie["m_origin"]) is { Length: 3 } u && Zahlen(linie["m_dirVec"]) is { Length: 3 } r && Zahlen(linie["m_endParams"]) is { Length: 2 } t)
        {
            anfang = [.. Enumerable.Range(0, 3).Select(i => Math.Round((u[i] + r[i] * t[0]) * MmJeFuss, 3))];
            ende = [.. Enumerable.Range(0, 3).Select(i => Math.Round((u[i] + r[i] * t[1]) * MmJeFuss, 3))];
        }
        var bemessung = o["m_pDesignPropManager"] as RevitObjekt;
        var anschluesse = ((o["m_pConnectorManager"] as RevitObjekt)?["m_connPtrArray"] as List<object?> ?? []).OfType<RevitObjekt>().ToList();
        long? system = anschluesse.SelectMany(a => (a["m_modifiers"] as List<object?> ?? []).OfType<RevitObjekt>())
            .Select(m => (m["m_idSystem"] as RevitObjekt)?.AlsId()).FirstOrDefault(x => x is > 0);
        var verbunden = anschluesse.SelectMany(a => (a["m_arrRefs"] as List<object?> ?? []).OfType<RevitObjekt>())
            .Select(rf => (rf["m_id"] as RevitObjekt)?.AlsId()).OfType<long>().Where(x => x != id && x != system && x > 0).Distinct().ToList();
        long? Id(RevitObjekt? q, string f) => (q?[f] as RevitObjekt)?.AlsId() is long v && v > 0 ? v : null;
        string? N(long? i) => i is long v && namen.TryGetValue(v, out var n) ? n : null;
        var typ = Id(o, "m_idType"); var segment = Id(o, "m_idSegment"); var ebene = Id(o, "m_idLevelStart");
        return new RevitLeitung(id, o.Klasse.Name, anfang, ende, Mm(o["m_dWidthOrDiameter"]), Mm(o["m_dHeight"]),
            Mm(bemessung?["m_dOuterDiameter"]), Mm(bemessung?["m_dInnerDiameter"]), bemessung?["m_sCalculatedSize"] as string,
            typ, N(typ), segment, N(segment), system, N(system), ebene, N(ebene), verbunden);
    }

    static double[]? Zahlen(object? v) => v is List<object?> l && l.All(x => x is double) ? [.. l.Cast<double>()] : null;
    static double? Mm(object? v) => v is double d && d > 0 ? Math.Round(d * MmJeFuss, 3) : null;
}
