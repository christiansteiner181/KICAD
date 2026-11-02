// Deckung Nachbau ↔ IFC (Christian 02.11.2026: «nach dem import müssen die beiden geometrien deckungsgleich sein»). Je übernommenem Element
// einer IFC-Unterlage wird der umschliessende Quader des Nachbaus (Körper des Kerns: Quader, Prisma, Zylinder, Kugel, Familienkörper) mit dem
// des IFC-Netzes (Netzdatei, Produkt und seine Teile; verteilte Produkte «GlobalId:2» gemeinsam) verglichen; Abweichung = grösste Differenz
// einer der sechs Seiten in mm. Dazu die Produkte der Bauteilarten, die gar nicht nachgebaut wurden (nur Hintergrund). Nur lesen.
// Grenze der Aussage: gleiche Hülle heisst nicht gleiche Form (eine L-Wand und ihr Rechteck haben denselben Quader) – das ist der erste,
// schnelle Massstab; die Form zeigt der Vergleich im 3D (ifcDarstellung «beide»).

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class IfcDeckung
{
    public const string Text = "Deckung Nachbau ↔ IFC nach dem Import: je aus einer IFC-Unterlage übernommenem Element der umschliessende Quader des Nachbaus gegen den des IFC-Netzes (mm, grösste Abweichung einer Seite und welche), deckungsgleich bis zur Toleranz; je Klasse Anzahl, deckungsgleich, grösste und mittlere Abweichung; die abweichenden Elemente (grösste zuerst) und die IFC-Produkte der Bauteilarten (Wand, Decke, Dach, Stütze, Träger, Tür, Fenster, Treppe, Geländer, Leitungen, Formteile, Armaturen, Geräte), die nicht nachgebaut, sondern nur Hintergrund sind (je Entität). Nur lesen.";
    public const string ParUnterlage = "IFC-Unterlage (Kennzeichen oder Id); Vorgabe: alle IFC-Unterlagen";
    public const string ParToleranz = "Toleranz in mm (Vorgabe 5)";
    public const string ParAnzahl = "Höchstens so viele abweichende Elemente auflisten (Vorgabe 100)";

    public static IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("unterlage", "text", false, ParUnterlage),
        new("toleranz", "zahl", false, ParToleranz),
        new("anzahl", "zahl", false, ParAnzahl),
    ];

    /// <summary>Bauteilarten, die nachgebaut werden sollten (Entitäten ohne «Ifc», Gross/klein egal).</summary>
    static readonly HashSet<string> Bauteile = new(StringComparer.OrdinalIgnoreCase)
    {
        "IfcWall", "IfcWallStandardCase", "IfcSlab", "IfcRoof", "IfcColumn", "IfcBeam", "IfcDoor", "IfcWindow", "IfcStair", "IfcStairFlight", "IfcRailing",
        "IfcCurtainWall", "IfcPlate", "IfcMember", "IfcRamp", "IfcRampFlight", "IfcCovering",
        "IfcPipeSegment", "IfcDuctSegment", "IfcPipeFitting", "IfcDuctFitting", "IfcValve", "IfcDamper", "IfcPump", "IfcFlowTerminal", "IfcSanitaryTerminal",
        "IfcAirTerminal", "IfcSpaceHeater", "IfcFlowSegment", "IfcFlowFitting", "IfcFlowController", "IfcFlowMovingDevice", "IfcEnergyConversionDevice",
    };

    public static JsonObject Pruefen(Projekt p, JsonObject? par)
    {
        double tol = Param.Zahl(par ?? [], "toleranz") is double t && t >= 0 ? t : 5;
        int anzahl = Param.Zahl(par ?? [], "anzahl") is double a && a > 0 ? (int)a : 100;
        var m = p.Modell;
        var unterlagen = m.Elemente.Where(e => e.Klasse == "Unterlage" && e.Text("art") == "ifc").ToList();
        if (Param.Text(par ?? [], "unterlage") is { Length: > 0 } u)
            unterlagen = [m.Hole(u)];
        var aus = new JsonObject { ["toleranz"] = tol };
        var liste = new JsonArray();
        foreach (var ul in unterlagen)
        {
            if (ul.Text("netz") is not { Length: > 0 } name) continue;
            IfcNetzdatei d;
            try { d = IfcNetzdatei.Oeffnen(Unterlagen.Pfad(p, name)); }
            catch (Exception ex) when (ex is HlksFehler or IOException) { liste.Add(new JsonObject { ["unterlage"] = ul.Kennzeichen, ["fehler"] = ex.Message }); continue; }
            liste.Add(EineUnterlage(p, ul, d, tol, anzahl));
        }
        aus["unterlagen"] = liste;
        return aus;
    }

    static string Basis(string kennung) => kennung.IndexOf(':') is var i and > 0 ? kennung[..i] : kennung;

    static JsonObject EineUnterlage(Projekt p, Element ul, IfcNetzdatei d, double tol, int anzahl)
    {
        var m = p.Modell;
        var gruppen = m.Elemente.Where(e => e.Kennungen?.GetValueOrDefault("quelle") == ul.Id && e.Kennungen.GetValueOrDefault("ifc") is { Length: > 0 })
            .GroupBy(e => Basis(e.Kennungen!["ifc"])).ToList();
        var ergebnisse = new List<(Element E, string Entitaet, double Abw, string Seite, double[] Nb, double[] Ifc)>();
        int ohneNachbau = 0, ohneNetz = 0;
        foreach (var g in gruppen)
        {
            if (d.Produkt(g.Key) is not { } pr) { ohneNetz++; continue; }
            var ifc = Box(new[] { pr }.Concat(d.TeileVon(g.Key)).Where(x => x.Dreiecke > 0).Select(x => x.Begrenzung.Select(v => (double)v).ToArray()));
            if (ifc is null) { ohneNetz++; continue; }
            var nb = Box(g.SelectMany(e => Grafik.QuelleFuer(p.Katalog, e)?.Koerper(p, e)?.Teile ?? []).Select(TeilBox).OfType<double[]>());
            var erstes = g.OrderBy(e => e.Kennungen!["ifc"].Length).First();
            if (nb is null) { ohneNachbau++; ergebnisse.Add((erstes, pr.Entitaet, double.PositiveInfinity, "kein Nachbau-Körper", [], ifc)); continue; }
            string[] seiten = ["xmin", "ymin", "zmin", "xmax", "ymax", "zmax"];
            int i = Enumerable.Range(0, 6).MaxBy(k => Math.Abs(nb[k] - ifc[k]));
            ergebnisse.Add((erstes, pr.Entitaet, Math.Abs(nb[i] - ifc[i]), seiten[i], nb, ifc));
        }
        var jeKlasse = new JsonObject();
        foreach (var k in ergebnisse.GroupBy(x => x.E.Klasse).OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            var endlich = k.Where(x => double.IsFinite(x.Abw)).Select(x => x.Abw).ToList();
            jeKlasse[k.Key] = new JsonObject
            {
                ["anzahl"] = k.Count(), ["deckungsgleich"] = k.Count(x => x.Abw <= tol),
                ["maxMm"] = endlich.Count > 0 ? Math.Round(endlich.Max(), 1) : null, ["mittelMm"] = endlich.Count > 0 ? Math.Round(endlich.Average(), 1) : null,
            };
        }
        static JsonArray Arr(double[] b) => new(b.Select(v => (JsonNode)Math.Round(v, 1)).ToArray());
        var abweichend = new JsonArray(ergebnisse.Where(x => x.Abw > tol).OrderByDescending(x => x.Abw).Take(anzahl).Select(x => (JsonNode)new JsonObject
        {
            ["kennzeichen"] = x.E.Kennzeichen, ["klasse"] = x.E.Klasse, ["entitaet"] = x.Entitaet, ["globalId"] = Basis(x.E.Kennungen!["ifc"]),
            ["abweichungMm"] = double.IsFinite(x.Abw) ? Math.Round(x.Abw, 1) : null, ["seite"] = x.Seite,
            ["nachbau"] = x.Nb.Length == 6 ? Arr(x.Nb) : null, ["ifc"] = Arr(x.Ifc),
        }).ToArray());
        var nachgebaut = gruppen.Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
        var fehlt = d.Produkte.Where(x => x.Rolle == IfcNetzRolle.Bauteil && !nachgebaut.Contains(x.GlobalId) && Bauteile.Contains(x.Entitaet) && x.Dreiecke > 0)
            .GroupBy(x => x.Entitaet).OrderByDescending(x => x.Count());
        return new JsonObject
        {
            ["unterlage"] = ul.Kennzeichen, ["elemente"] = ergebnisse.Count, ["deckungsgleich"] = ergebnisse.Count(x => x.Abw <= tol),
            ["ohneNachbauKoerper"] = ohneNachbau, ["ohneIfcNetz"] = ohneNetz,
            ["jeKlasse"] = jeKlasse, ["abweichend"] = abweichend,
            ["nichtNachgebaut"] = new JsonObject(fehlt.Select(x => KeyValuePair.Create(x.Key, (JsonNode?)x.Count()))),
        };
    }

    static double[]? Box(IEnumerable<double[]> boxen)
    {
        double[]? b = null;
        foreach (var x in boxen)
        {
            if (b is null) { b = (double[])x.Clone(); continue; }
            for (int i = 0; i < 3; i++) { b[i] = Math.Min(b[i], x[i]); b[i + 3] = Math.Max(b[i + 3], x[i + 3]); }
        }
        return b;
    }

    static double[] Aus(IEnumerable<double[]> punkte)
    {
        double[] b = [double.MaxValue, double.MaxValue, double.MaxValue, double.MinValue, double.MinValue, double.MinValue];
        foreach (var q in punkte) for (int i = 0; i < 3; i++) { b[i] = Math.Min(b[i], q[i]); b[i + 3] = Math.Max(b[i + 3], q[i]); }
        return b;
    }

    /// <summary>Umschliessender Quader eines Körperteils in Projekt-mm; null für Netze und unvollständige Angaben.</summary>
    public static double[]? TeilBox(Koerperteil t)
    {
        switch (t.Art)
        {
            case "quader" when t.Mitte is { Length: >= 3 } c && t.Masse is { Length: >= 3 } s:
            {
                double w = (t.Drehung ?? 0) * Math.PI / 180, co = Math.Abs(Math.Cos(w)), si = Math.Abs(Math.Sin(w));
                double ex = co * s[0] / 2 + si * s[1] / 2, ey = si * s[0] / 2 + co * s[1] / 2, ez = s[2] / 2;
                return [c[0] - ex, c[1] - ey, c[2] - ez, c[0] + ex, c[1] + ey, c[2] + ez];
            }
            case "prisma" when t.Grundriss is { Count: > 0 } g && t.Unten is double u && t.Oben is double o:
            {
                var b = Aus(g.Select(q => new[] { q[0], q[1], u }));
                b[5] = o;
                return b;
            }
            case "zylinder" when t.Von is { Length: >= 3 } v && t.Nach is { Length: >= 3 } n && t.Radius is double r:
            {
                var b = Aus([v, n]);
                for (int i = 0; i < 3; i++) { b[i] -= r; b[i + 3] += r; }
                return b;
            }
            case "kugel" when t.Mitte is { Length: >= 3 } c && t.Radius is double r:
                return [c[0] - r, c[1] - r, c[2] - r, c[0] + r, c[1] + r, c[2] + r];
            case "koerper" when t.Ecken is { Count: > 0 } e:
                return Aus(e);
            default:
                return null;
        }
    }
}
