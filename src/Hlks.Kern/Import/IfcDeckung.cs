// Deckung Nachbau ↔ IFC (Christian 02.11.2026: «nach dem import müssen die beiden geometrien deckungsgleich sein»). Je übernommenem Element
// einer IFC-Unterlage wird der umschliessende Quader des Nachbaus (Körper des Kerns: Quader, Prisma, Zylinder, Kugel, Familienkörper) mit dem
// des IFC-Netzes (Netzdatei, Produkt und seine Teile; verteilte Produkte «GlobalId:2» gemeinsam) verglichen; Abweichung = grösste Differenz
// einer der sechs Seiten in mm. Gemessen im System des Bauteils (Drehung um z wie sein Nachbau-Quader bzw. seine Wand), sonst würde ein schräg
// stehender Rahmen, der die Ecken seines Quaders nicht füllt, als Abweichung zählen. Dazu die Produkte der Bauteilarten, die gar nicht
// nachgebaut wurden (nur Hintergrund). Nur lesen.
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

    /// <summary>Nachlauf von ifc_uebernehmen (Projekt.Ausfuehren): Deckung je IFC-Unterlage als Hinweise – deckungsgleich, abweichend je Klasse,
    /// nicht nachgebaut. Wirft nie.</summary>
    public static IEnumerable<string> NachUebernahme(Projekt p)
    {
        JsonObject r;
        try { r = Pruefen(p, new JsonObject { ["anzahl"] = 0 }); }
        catch (Exception ex) { return [$"Prüfung Nachbau ↔ IFC nicht möglich: {ex.Message}"]; }
        var aus = new List<string>();
        foreach (var u in r["unterlagen"]!.AsArray().OfType<JsonObject>())
        {
            if (u["fehler"] is { } f) { aus.Add($"Prüfung Nachbau ↔ IFC {u["unterlage"]}: {f}"); continue; }
            int n = u["elemente"]!.GetValue<int>(), ok = u["deckungsgleich"]!.GetValue<int>();
            var ab = u["jeKlasse"]!.AsObject().Select(kv => (Klasse: kv.Key, Anzahl: kv.Value!["anzahl"]!.GetValue<int>(), Ok: kv.Value["deckungsgleich"]!.GetValue<int>(), Max: kv.Value["maxMm"]))
                .Where(x => x.Ok < x.Anzahl).Select(x => $"{x.Klasse} {x.Anzahl - x.Ok} (bis {x.Max} mm)").ToList();
            var fehlt = u["nichtNachgebaut"]!.AsObject().Select(kv => $"{kv.Key} {kv.Value}").ToList();
            var ifcK = u["ausIfcKoerper"]!.AsObject().Select(kv => $"{kv.Key} {kv.Value}").ToList();
            var stdK = u["standardFormteile"]!.AsObject().Select(kv => $"{kv.Key} {kv.Value}").ToList();
            aus.Add($"Prüfung Nachbau ↔ IFC {u["unterlage"]} (±{r["toleranz"]} mm): {ok} von {n} deckungsgleich"
                + (ab.Count > 0 ? $"; abweichend: {string.Join(", ", ab)}" : "")
                + (stdK.Count > 0 ? $"; mit unserem Standard nachgezeichnet (Formteile der Serie, nicht 1:1): {string.Join(", ", stdK)}" : "")
                + (ifcK.Count > 0 ? $"; Körper aus dem IFC (HLKS-Bauteile ohne eigene Form, 3D und Kollision aus dem IFC-Netz): {string.Join(", ", ifcK)}" : "")
                + (fehlt.Count > 0 ? $"; nicht nachgebaut (nur Hintergrund): {string.Join(", ", fehlt)}" : "")
                + (ab.Count > 0 || fehlt.Count > 0 ? " – Einzelheiten: Abfrage ifc_deckung." : "."));
        }
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
        var ausIfc = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var standard = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var g in gruppen)
        {
            if (d.Produkt(g.Key) is not { } pr) { ohneNetz++; continue; }
            // HLKS-Bauteile ohne eigene Form (Formteil, Armatur, Apparat, Gerät: in KICAD ein Punkt mit Sinnbild) zeigen in 3D und in der
            // Kollision ihr IFC-Netz, solange sie unverändert sind (IfcOriginal) – kein Nachbau zu messen; eigens gezählt (03.11.2026)
            // Formteile mit dem Formteil unserer Serie (Standard nachgezeichnet, Christian 03.11.2026): nicht 1:1, eigens gezählt
            // Leitung ab Rolle (Bögen in der Leitung aufgegangen, IfcSerien.Zusammenfuehren): Biegung mit dem Radius der Serie – nicht 1:1, eigens gezählt
            // aufgerundete Leitung (Grösse der Serie statt IFC-Mass, Regel «aufrunden»): nicht 1:1, eigens gezählt
            if (g.Any(e => e.Kennungen!.ContainsKey(IfcSerien.IfcGroesse)))
            {
                standard["Leitung aufgerundet"] = standard.GetValueOrDefault("Leitung aufgerundet") + 1;
                continue;
            }
            if (g.Any(e => e.Kennungen!.ContainsKey(IfcSerien.IfcMit)))
            {
                standard["Leitung ab Rolle"] = standard.GetValueOrDefault("Leitung ab Rolle") + 1;
                continue;
            }
            if (g.All(e => HlksEinordnung.Art(p.Katalog, e) == HlksArt.Formteil && Familien.FamilieVon(m, e) is { } f && Familien.DefinitionsId(f)?.StartsWith("ifc-") != true))
            {
                var kl = g.First().Klasse;
                standard[kl] = standard.GetValueOrDefault(kl) + 1;
                continue;
            }
            if (g.All(e => Grafik.QuelleFuer(p.Katalog, e) is HlksGrafik && HlksEinordnung.Art(p.Katalog, e) != HlksArt.Leitung) && IfcOriginal.Netz(p, g.First()) is not null)
            {
                var kl = g.First().Klasse;
                ausIfc[kl] = ausIfc.GetValueOrDefault(kl) + 1;
                continue;
            }
            var teile = g.SelectMany(e => VergleichsTeile(p, e)).ToList();
            double w = (teile.FirstOrDefault(t => t.Art == "quader")?.Drehung ?? 0) * Math.PI / 180;
            var ifc = Box(new[] { pr }.Concat(d.TeileVon(g.Key)).Where(x => x.Dreiecke > 0).Select(x => NetzBox(d, x, w)).OfType<double[]>());
            if (ifc is null) { ohneNetz++; continue; }
            var nb = Box(teile.Select(t => TeilBox(t, w)).OfType<double[]>());
            var erstes = g.OrderBy(e => e.Kennungen!["ifc"].Length).First();
            if (nb is null) { ohneNachbau++; ergebnisse.Add((erstes, pr.Entitaet, double.PositiveInfinity, "kein Nachbau-Körper", [], ifc)); continue; }
            string[] seiten = w == 0 ? ["xmin", "ymin", "zmin", "xmax", "ymax", "zmax"] : ["anfang", "rechts", "unten", "ende", "links", "oben"];
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
        foreach (var e in gruppen.SelectMany(g => g)) if (e.Kennungen!.GetValueOrDefault(IfcSerien.IfcMit) is { Length: > 0 } mit) nachgebaut.UnionWith(mit.Split(','));
        var fehlt = d.Produkte.Where(x => x.Rolle == IfcNetzRolle.Bauteil && !nachgebaut.Contains(x.GlobalId) && Bauteile.Contains(x.Entitaet) && x.Dreiecke > 0)
            .GroupBy(x => x.Entitaet).OrderByDescending(x => x.Count());
        return new JsonObject
        {
            ["unterlage"] = ul.Kennzeichen, ["elemente"] = ergebnisse.Count, ["deckungsgleich"] = ergebnisse.Count(x => x.Abw <= tol),
            ["ohneNachbauKoerper"] = ohneNachbau, ["ohneIfcNetz"] = ohneNetz,
            ["ausIfcKoerper"] = new JsonObject(ausIfc.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value))),
            ["standardFormteile"] = new JsonObject(standard.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value))),
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

    /// <summary>Körper des Nachbaus für den Vergleich: Wände als ganzer Quader (Achse mit Verlängerung, Dicke, Unterkante bis Oberkante) – ohne
    /// Öffnungen und ohne den Abzug der Decken, wie das Hauptnetz der Netzdatei (Fassung 5: Öffnungen nicht ausgeschnitten, Wand bis in die Decke);
    /// sonst der Körper des Kerns.</summary>
    static IEnumerable<Koerperteil> VergleichsTeile(Projekt p, Element e)
    {
        if (e.Klasse == "Wand" && Wandgeometrie.MitVerlaengerung(p.Modell, p.Katalog, e) is { Hoehe: double h } w && h > 0 && w.Dicke > 0)
        {
            var c = w.Punkt((w.Von + w.Bis) / 2, 0);
            return [new Koerperteil { Art = "quader", Mitte = [c[0], c[1], w.Unterkante + h / 2], Masse = [w.Bis - w.Von, w.Dicke, h], Drehung = w.Winkel }];
        }
        return Grafik.QuelleFuer(p.Katalog, e)?.Koerper(p, e)?.Teile ?? [];
    }

    /// <summary>Punkt im um w (Bogenmass, um z) gedrehten System: u längs, v quer, z.</summary>
    static double[] Dreh(double x, double y, double z, double w) => [x * Math.Cos(w) + y * Math.Sin(w), -x * Math.Sin(w) + y * Math.Cos(w), z];

    static double[]? NetzBox(IfcNetzdatei d, IfcNetzProdukt pr, double w)
    {
        var (pu, _) = d.Netz(pr);
        if (pu.Length < 3) return null;
        return Aus(Enumerable.Range(0, pu.Length / 3).Select(i => Dreh(pu[3 * i], pu[3 * i + 1], pu[3 * i + 2], w)));
    }

    /// <summary>Umschliessender Quader eines Körperteils im um w gedrehten System (mm); null für Netze und unvollständige Angaben.</summary>
    public static double[]? TeilBox(Koerperteil t, double w = 0)
    {
        switch (t.Art)
        {
            case "quader" when t.Mitte is { Length: >= 3 } c && t.Masse is { Length: >= 3 } s:
            {
                double d = (t.Drehung ?? 0) * Math.PI / 180, co = Math.Cos(d), si = Math.Sin(d);
                var ecken = new List<double[]>();
                foreach (var a in new[] { -0.5, 0.5 }) foreach (var b in new[] { -0.5, 0.5 }) foreach (var h in new[] { -0.5, 0.5 })
                    ecken.Add(Dreh(c[0] + co * a * s[0] - si * b * s[1], c[1] + si * a * s[0] + co * b * s[1], c[2] + h * s[2], w));
                return Aus(ecken);
            }
            case "prisma" when t.Grundriss is { Count: > 0 } g && t.Unten is double u && t.Oben is double o:
            {
                var b = Aus(g.Select(q => Dreh(q[0], q[1], u, w)));
                b[5] = o;
                return b;
            }
            case "zylinder" when t.Von is { Length: >= 3 } v && t.Nach is { Length: >= 3 } n && t.Radius is double r:
            {
                // genau: quer zur Achse reicht der Kreis je Richtung r · √(1 − u²) (u = Anteil der Achse an dieser Richtung)
                double[] p0 = Dreh(v[0], v[1], v[2], w), p1 = Dreh(n[0], n[1], n[2], w);
                var b = Aus([p0, p1]);
                double l = Math.Sqrt(Enumerable.Range(0, 3).Sum(i => (p1[i] - p0[i]) * (p1[i] - p0[i])));
                for (int i = 0; i < 3; i++)
                {
                    double u = l > 0 ? (p1[i] - p0[i]) / l : 0, q = r * Math.Sqrt(Math.Max(0, 1 - u * u));
                    b[i] -= q; b[i + 3] += q;
                }
                return b;
            }
            case "kugel" when t.Mitte is { Length: >= 3 } c && t.Radius is double r:
            {
                var m = Dreh(c[0], c[1], c[2], w);
                return [m[0] - r, m[1] - r, m[2] - r, m[0] + r, m[1] + r, m[2] + r];
            }
            case "koerper" when t.Ecken is { Count: > 0 } e:
                return Aus(e.Select(q => Dreh(q[0], q[1], q[2], w)));
            default:
                return null;
        }
    }
}
