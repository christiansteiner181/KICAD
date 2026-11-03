// Leitungen (Rohre, Kanäle) und Bauteile (Formteile, Armaturen …) aus einer Revit-Datei – fachliche Auslegung der Objekte aus
// RevitObjekte (Stufe 2, entwurf/rvt-format.md). Gefunden an der Geberit-Silent-db20-RVT (Revit 2017), Werte gegen Revits eigene
// Texte geprüft (Durchmesser 0.183727 ft = 56,0 mm, «56 mmø»):
//   Leitung    jede Klasse mit Kurventreiber (m_pCurveDriver) und Querschnitt (m_dWidthOrDiameter, m_dHeight), z. B. RbsPipeCurve.
//   Achse      m_pCurveDriver.m_pCrv: GLine mit m_origin, m_dirVec, m_endParams → Anfang/Ende = Ursprung + Richtung × Parameter.
//   Rohr       m_pDesignPropManager: m_dOuterDiameter, m_dInnerDiameter, m_sCalculatedSize.
//   Bezüge     m_idType (Typ), m_idSegment (Segment), m_idLevelStart (Ebene); System über die Anschlüsse
//              (Connector.m_modifiers → …Calculation.m_idSystem); verbunden über Connector.m_arrRefs.
//   Namen      Typen m_symbolInfo.m_name, Familien m_name, Systeme m_strName, Ebenen m_text, Segmente m_name.
//   Bauteil    FamilyInstance einer HLKS-Kategorie (Kopf: ElementHeader.m_categroryId, so geschrieben). Lage m_pInstanceInfo.m_Trf:
//              Welt = m_3x3 · lokal + m_or, m_3x3 zeilenweise (geprüft am gedrehten Abzweig 2314: Begrenzungsquader der Familie
//              gedreht = der des Exemplars; m_RefDir bleibt dort 1,0,0 und taugt nicht). Typ m_symbolId (leerer Name → m_masterSymbolId),
//              Familie über FamilySymbol.m_familyId; Achse der Formteile im Element m_pDesignPropManager.m_idCenterLine (m_CenterCurves,
//              bereits in Weltkoordinaten); Armaturen haben keine – ihre Anschlusslagen stehen nur in der Familiengeometrie.
// Kategorie-Ids: Enum BuiltInCategory aus RevitAPI.dll (Revit 2026, Metadaten gelesen), z. B. OST_PipeFitting = -2008049.
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

/// <summary>Anschluss eines Bauteils: Revits Nummer, angeschlossene Elemente und System.</summary>
public sealed record RevitAnschluss(int Nummer, IReadOnlyList<long> Verbunden, long? SystemId);

/// <summary>Stück einer Mittellinie in mm: Gerade (<see cref="Mitte"/> null) oder Kreisbogen durch Anfang, Mitte und Ende.</summary>
public sealed record RevitAchsstueck(double[] Anfang, double[] Ende, double[]? Mitte = null, double? Radius = null);

/// <summary>Ein Bauteil (Formteil, Armatur, Apparat …) aus einer Revit-Datei. <see cref="Drehung"/>: Zeilen der Matrix M mit
/// Welt = M · lokal + <see cref="Ursprung"/>. <see cref="Achse"/>: Mittellinie der Formteile, sonst leer.
/// <see cref="TeilVon"/>: verschachteltes Teil einer anderen Familie (m_superInstanceId), nicht doppelt zählen.</summary>
public sealed record RevitBauteil(
    long Id, string Kategorie, long KategorieId, string? Familie, long? TypId, string? Typ,
    double[] Ursprung, double[][] Drehung, string? Groesse, IReadOnlyList<RevitAnschluss> Anschluesse,
    IReadOnlyList<RevitAchsstueck> Achse, long? SystemId, string? System, long? EbeneId, string? Ebene, long? TeilVon)
{
    public IEnumerable<long> Verbunden => Anschluesse.SelectMany(a => a.Verbunden).Distinct();
}

public sealed record RevitNetz(List<RevitLeitung> Leitungen, List<RevitBauteil> Bauteile);

public static class RevitLeitungen
{
    const double MmJeFuss = 304.8;

    /// <summary>HLKS-Kategorien der Bauteile (BuiltInCategory, RevitAPI.dll 2026).</summary>
    public static readonly IReadOnlyDictionary<long, string> Kategorien = new Dictionary<long, string>
    {
        [-2008049] = "Rohrformteil",      // OST_PipeFitting
        [-2008055] = "Rohrzubehör",       // OST_PipeAccessory (Armaturen)
        [-2001160] = "Sanitärapparat",    // OST_PlumbingFixtures
        [-2008010] = "Kanalformteil",     // OST_DuctFitting
        [-2008016] = "Kanalzubehör",      // OST_DuctAccessory
        [-2008013] = "Luftdurchlass",     // OST_DuctTerminal
        [-2001140] = "HLK-Gerät",         // OST_MechanicalEquipment
    };

    /// <summary>Alle Leitungen der Datei mit Namen von Typ, Segment, System und Ebene.</summary>
    public static List<RevitLeitung> Lesen(RevitDatei datei) => Netz(datei).Leitungen;

    /// <summary>Leitungen und Bauteile in einem Durchgang durch die Datei.</summary>
    public static RevitNetz Netz(RevitDatei datei)
    {
        var schema = datei.Schema();
        var namen = new Dictionary<long, string>();
        var familieDesTyps = new Dictionary<long, long>();
        var kategorie = new Dictionary<long, long>();
        var mittellinien = new Dictionary<long, RevitObjekt>();
        var leitungen = new List<(long Id, RevitObjekt O)>();
        var bauteile = new List<(long Id, RevitObjekt O)>();
        foreach (var s in RevitObjekte.Saetze(datei, schema))
        {
            if (s.Objekt is not { } o) continue;
            if (s.Kopf) { if (IdVon(o["m_categroryId"]) is long k) kategorie.TryAdd(s.Id, k); continue; }
            if (Name(o) is { Length: > 0 } n) namen.TryAdd(s.Id, n);
            if (o.Klasse.Name == "FamilySymbol" && IdVon(o["m_familyId"]) is long fam and > 0) familieDesTyps.TryAdd(s.Id, fam);
            if (o["m_CenterCurves"] is List<object?>) mittellinien.TryAdd(s.Id, o);
            if (o.Klasse.Name == "FamilyInstance") bauteile.Add((s.Id, o));
            else if (IstLeitung(schema, o.Klasse)) leitungen.Add((s.Id, o));
        }
        string? N(long? i) => i is long v && namen.TryGetValue(v, out var n) ? n : null;
        return new RevitNetz(
            [.. leitungen.Select(r => Leitung(r.Id, r.O, N))],
            [.. bauteile.Where(b => kategorie.TryGetValue(b.Id, out var k) && Kategorien.ContainsKey(k))
                .Select(b => Bauteil(b.Id, kategorie[b.Id], b.O, N, familieDesTyps, mittellinien))]);
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
        ?? (o.Klasse.Name == "Family" || o.Klasse.Name.EndsWith("Segment", StringComparison.Ordinal) ? o["m_name"] as string : null);

    static RevitLeitung Leitung(long id, RevitObjekt o, Func<long?, string?> N)
    {
        // Rohre und Kanäle sind in den Stichproben immer Geraden; Bögen (GArc) nur in den Mittellinien der Formteile
        var achse = (o["m_pCurveDriver"] as RevitObjekt)?["m_pCrv"] is RevitObjekt { Klasse.Name: "GLine" } linie ? Stueck(linie) : null;
        double[]? anfang = achse?.Anfang, ende = achse?.Ende;
        var bemessung = o["m_pDesignPropManager"] as RevitObjekt;
        var anschluesse = Anschluesse(o, id);
        long? system = anschluesse.Select(x => x.SystemId).FirstOrDefault(x => x is not null);
        var verbunden = anschluesse.SelectMany(x => x.Verbunden).Where(x => x != system).Distinct().ToList();
        var typ = Id(o, "m_idType"); var segment = Id(o, "m_idSegment"); var ebene = Id(o, "m_idLevelStart");
        return new RevitLeitung(id, o.Klasse.Name, anfang, ende, Mm(o["m_dWidthOrDiameter"]), Mm(o["m_dHeight"]),
            Mm(bemessung?["m_dOuterDiameter"]), Mm(bemessung?["m_dInnerDiameter"]), bemessung?["m_sCalculatedSize"] as string,
            typ, N(typ), segment, N(segment), system, N(system), ebene, N(ebene), verbunden);
    }

    static RevitBauteil Bauteil(long id, long kategorie, RevitObjekt o, Func<long?, string?> N,
        Dictionary<long, long> familieDesTyps, Dictionary<long, RevitObjekt> mittellinien)
    {
        var trf = (o["m_pInstanceInfo"] as RevitObjekt)?["m_Trf"] as RevitObjekt;
        var ursprung = Zahlen(trf?["m_or"]) is { Length: 3 } u ? u.Select(x => Math.Round(x * MmJeFuss, 3)).ToArray() : [0, 0, 0];
        var drehung = trf?["m_3x3"] is List<object?> m && m.Select(Zahlen).ToArray() is { Length: 3 } z && z.All(r => r is { Length: 3 })
            ? z.Select(r => r!.Select(x => Math.Round(x, 9)).ToArray()).ToArray()
            : [[1, 0, 0], [0, 1, 0], [0, 0, 1]];   // Einheitsmatrix speichert Revit nicht
        var typ = Id(o["m_pInstanceInfo"] as RevitObjekt, "m_symbolId");
        if (N(typ) is null && Id(o, "m_masterSymbolId") is long meister) typ = meister;
        string? familie = typ is long t && familieDesTyps.TryGetValue(t, out var f) ? N(f) : null;
        var bemessung = o["m_pDesignPropManager"] as RevitObjekt;
        var achse = new List<RevitAchsstueck>();
        if (Id(bemessung, "m_idCenterLine") is long ml && mittellinien.TryGetValue(ml, out var linie))
            foreach (var k in (linie["m_CenterCurves"] as List<object?> ?? []).OfType<RevitObjekt>())
                if (Stueck(k) is { } s) achse.Add(s);
        var anschluesse = Anschluesse(o, id);
        long? system = anschluesse.Select(x => x.SystemId).FirstOrDefault(x => x is not null);
        var ebene = Id(o, "m_assocLevelId");
        return new RevitBauteil(id, Kategorien[kategorie], kategorie, familie, typ, N(typ), ursprung, drehung,
            bemessung?["m_sCalculatedSize"] as string is { Length: > 0 } g ? g : null, anschluesse, achse, system, N(system), ebene, N(ebene),
            Id(o, "m_superInstanceId"));
    }

    static List<RevitAnschluss> Anschluesse(RevitObjekt o, long eigene) =>
        [.. ((o["m_pConnectorManager"] as RevitObjekt)?["m_connPtrArray"] as List<object?> ?? []).OfType<RevitObjekt>().Select(a =>
        {
            long? system = (a["m_modifiers"] as List<object?> ?? []).OfType<RevitObjekt>().Select(x => Id(x, "m_idSystem")).FirstOrDefault(x => x is not null);
            var verbunden = (a["m_arrRefs"] as List<object?> ?? []).OfType<RevitObjekt>().Select(r => Id(r, "m_id"))
                .OfType<long>().Where(x => x != eigene && x != system).Distinct().ToList();
            return new RevitAnschluss(a["m_nIndex"] is { } i ? Convert.ToInt32(i, System.Globalization.CultureInfo.InvariantCulture) : 0, verbunden, system);
        })];

    /// <summary>Gerade (GLine: Ursprung + Richtung × t) oder Kreisbogen (GArc: Mitte + r·(cos t · m_xVec + sin t · m_yVec)) in mm;
    /// andere Kurven null.</summary>
    static RevitAchsstueck? Stueck(RevitObjekt k)
    {
        if (Zahlen(k["m_endParams"]) is not { Length: 2 } t) return null;
        double[] Mm3(Func<int, double> x) => [.. Enumerable.Range(0, 3).Select(i => Math.Round(x(i) * MmJeFuss, 3))];
        if (k.Klasse.Name == "GLine" && Zahlen(k["m_origin"]) is { Length: 3 } u && Zahlen(k["m_dirVec"]) is { Length: 3 } r)
            return new(Mm3(i => u[i] + r[i] * t[0]), Mm3(i => u[i] + r[i] * t[1]));
        if (k.Klasse.Name == "GArc" && Zahlen(k["m_center"]) is { Length: 3 } c && Zahlen(k["m_xVec"]) is { Length: 3 } x
            && Zahlen(k["m_yVec"]) is { Length: 3 } y && k["m_radius"] is double rad)
        {
            double[] Punkt(double p) => Mm3(i => c[i] + rad * (Math.Cos(p) * x[i] + Math.Sin(p) * y[i]));
            return new(Punkt(t[0]), Punkt(t[1]), Punkt((t[0] + t[1]) / 2), Math.Round(rad * MmJeFuss, 3));
        }
        return null;
    }

    static long? IdVon(object? v) => (v as RevitObjekt)?.AlsId();
    static long? Id(RevitObjekt? q, string f) => IdVon(q?[f]) is long v && v > 0 ? v : null;
    static double[]? Zahlen(object? v) => v is List<object?> l && l.All(x => x is double) ? [.. l.Cast<double>()] : null;
    static double? Mm(object? v) => v is double d && d > 0 ? Math.Round(d * MmJeFuss, 3) : null;
}
