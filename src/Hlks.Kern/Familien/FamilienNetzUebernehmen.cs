// Herstellergeometrie und IFC-Geometrie als Familienkörper «netz» (30.10.2026, Christian: «es müssen alle geometrien abgebildet
// werden können»; «es muss auch möglich sein fehlende familien aus einem ifc geometrisch abzubilden»).
//
// familie_netz_uebernehmen holt ein Dreiecksnetz
//   – aus der Netzdatei einer IFC-Unterlage (Produkt über GlobalId oder über ein übernommenes Element mit Kennung ifc), oder
//   – aus dem Befund form3d einer STL/OBJ/STEP-Unterlage (volles Netz, wahlweise das vereinfachte),
// legt es in die Familie als Körper art «netz» (Ecken in mm, Herkunft im Feld «herkunft») und legt die Familie an, wenn keine
// genannt ist. Das IFC-Netz steht in Projektkoordinaten: es wird um seine Drehung zurückgedreht (Angabe oder kleinstes umschliessendes
// Rechteck im Grundriss, ±45°) und auf den Nullpunkt geschoben (Vorgabe unten Mitte). Ein Netz aus einem Herstellermodell behält
// seinen Ursprung (Vorgabe unveraendert).
// ANNAHMEN: Punkte, die näher als 0.05 mm beieinander liegen, werden zusammengelegt; Koordinaten auf 0.1 mm gerundet. Die
//   Drehung aus dem Rechteck kennt die Vorderseite nicht (Vielfache von 90°) – der Hinweis nennt den Winkel zum Prüfen.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public sealed class FamilieNetzUebernehmen : IBefehl
{
    public string Name => "familie_netz_uebernehmen";
    public string Beschreibung => Text;
    public const string Text =
        "Übernimmt die Geometrie eines IFC-Produkts (Netzdatei einer IFC-Unterlage, z. B. ein Sanitärobjekt des Architekten, für das es keine Familie gibt) " +
        "oder eines Herstellermodells (Befund form3d einer STL/OBJ/STEP-Unterlage) als Körper art «netz» in eine Familie. Ohne familie entsteht eine neue " +
        "Familie (name und kategorie nötig) im Status entwurf. IFC-Netze werden zurückgedreht (drehung oder automatisch) und auf den Nullpunkt geschoben.";
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("objekt", "text", true, "IFC: GlobalId des Produkts oder Kennzeichen eines übernommenen Elements; STL/OBJ/STEP: Id des Befunds form3d (z. B. «3fa2…#1» oder «1»)"),
        new("unterlage", "text", false, "Unterlage (Kennzeichen, Id oder Dateiname); bei einem übernommenen Element die Unterlage seiner Übernahme"),
        new("familie", "text", false, "Bestehende Familie (Kennzeichen, Name oder id), in die der Körper kommt; leer = neue Familie"),
        new("name", "text", false, "Name der neuen Familie"),
        new("kategorie", "text", false, "KICAD-Klasse der neuen Familie, z. B. Sanitaerapparat"),
        new("koerper", "text", false, "id des Körpers (Vorgabe «netz»); ein Körper gleicher id wird ersetzt"),
        new("material", "text", false, "geraet | apparat | armatur | rohr | sonst (Vorgabe apparat)"),
        new("drehung", "zahl", false, "Drehung des Objekts im Projekt um z in Grad; wird zurückgedreht. Leer: IFC automatisch, sonst 0"),
        new("nullpunkt", "text", false, "unten_mitte | unten_hinten_mitte (Wandobjekte: Rückseite +y) | unveraendert; Vorgabe IFC unten_mitte, sonst unveraendert"),
        new("vereinfacht", "bool", false, "form3d: das vereinfachte Netz statt des vollen nehmen"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var objekt = Param.Text(p, "objekt", true)!.Trim();
        var (ecken, dreiecke, herkunft, ausIfc, typname) = Holen(k, p, objekt);
        if (dreiecke.Count == 0) throw new HlksFehler($"Das Objekt «{objekt}» hat kein Netz.", "Ein Objekt mit Körper wählen.");

        double? drehung = Param.Zahl(p, "drehung");
        bool auto = drehung is null && ausIfc;
        if (auto) drehung = Ausrichtung(ecken);
        if (drehung is { } dg && Math.Abs(dg) > 1e-9) Drehen(ecken, -dg);
        var np = Param.Text(p, "nullpunkt")?.Trim().ToLowerInvariant() ?? (ausIfc ? "unten_mitte" : "unveraendert");
        if (np is not ("unten_mitte" or "unten_hinten_mitte" or "unveraendert"))
            throw new HlksFehler($"nullpunkt «{np}» gibt es nicht.", "unten_mitte, unten_hinten_mitte oder unveraendert");
        var (min, max) = Huelle(ecken);
        if (np != "unveraendert")
        {
            double[] null0 = [(min[0] + max[0]) / 2, np == "unten_hinten_mitte" ? max[1] : (min[1] + max[1]) / 2, min[2]];
            foreach (var e in ecken) for (int i = 0; i < 3; i++) e[i] -= null0[i];
            for (int i = 0; i < 3; i++) { min[i] -= null0[i]; max[i] -= null0[i]; }
        }
        if (dreiecke.Count > FamilieDef.NetzMaxDreiecke)
            throw new HlksFehler($"Das Netz hat {dreiecke.Count} Dreiecke, eine Familie nimmt höchstens {FamilieDef.NetzMaxDreiecke}.",
                ausIfc ? "Ein einfacheres Objekt wählen." : "vereinfacht = true setzen.");

        var id = Param.Text(p, "koerper")?.Trim() is { Length: > 0 } kid ? kid : "netz";
        var koerper = new JsonObject
        {
            ["id"] = id, ["name"] = ausIfc ? "Geometrie aus IFC" : "Herstellergeometrie", ["art"] = "netz",
            ["material"] = Param.Text(p, "material")?.Trim() is { Length: > 0 } mat ? mat : "apparat",
            ["herkunft"] = herkunft,
            ["ecken"] = new JsonArray(ecken.Select(e => (JsonNode)new JsonArray(e.Select(c => (JsonNode)JsonValue.Create(Math.Round(c, 1))).ToArray())).ToArray()),
            ["dreiecke"] = new JsonArray(dreiecke.Select(d => (JsonNode)new JsonArray(JsonValue.Create(d[0]), JsonValue.Create(d[1]), JsonValue.Create(d[2]))).ToArray()),
        };

        Element? bisher = Param.Text(p, "familie") is { Length: > 0 } fb ? Familien.Hole(k.Arbeit, fb) : null;
        JsonObject def;
        if (bisher is not null)
        {
            def = (bisher.Eigenschaft("definition")?.Inhalt as JsonObject)?.DeepClone() as JsonObject ?? new JsonObject();
            if (def["koerper"] is not JsonArray ka) def["koerper"] = ka = [];
            int stelle = ka.Select((x, i) => (x, i)).FirstOrDefault(t => t.x?["id"]?.GetValue<string>() == id, (null, -1)).Item2;
            if (stelle >= 0) ka[stelle] = koerper; else ka.Add(koerper);
        }
        else
        {
            var name = Param.Text(p, "name")?.Trim();
            var kat = Param.Text(p, "kategorie")?.Trim();
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(kat))
                throw new HlksFehler("Für eine neue Familie fehlen name oder kategorie.", "name und kategorie angeben oder mit familie eine bestehende Familie nennen.");
            def = new JsonObject
            {
                ["format"] = 1, ["name"] = name, ["kategorie"] = kat,
                ["beschreibung"] = $"Geometrie übernommen: {herkunft}",
                ["parameter"] = new JsonArray(),
                ["typen"] = new JsonArray(new JsonObject { ["name"] = typname ?? "Standard", ["werte"] = new JsonObject() }),
                ["koerper"] = new JsonArray(koerper),
                ["anschluesse"] = new JsonArray(),
            };
        }
        var fam = FamilienBefehle.Speichern(k, bisher, def, Name, "entwurf", FamilienBefehle.Version(bisher) + 1, null);
        k.Hinweise.Add($"Körper «{id}» in {fam.Kennzeichen}: {ecken.Count} Ecken, {dreiecke.Count} Dreiecke, Grösse " +
            $"{max[0] - min[0]:0} × {max[1] - min[1]:0} × {max[2] - min[2]:0} mm" +
            (drehung is { } d2 && Math.Abs(d2) > 1e-9 ? $", um {d2:0.##}° zurückgedreht{(auto ? " (automatisch – Vorderseite −y prüfen, sonst drehung angeben)" : "")}" : "") +
            $", Nullpunkt {np}. Anschlüsse und Parameter fehlen noch.");
    }

    (List<double[]> Ecken, List<int[]> Dreiecke, string Herkunft, bool AusIfc, string? Typname) Holen(Kontext k, JsonObject p, string objekt)
    {
        var ulBezug = Param.Text(p, "unterlage");
        string? gid = null;
        if (k.Arbeit.Finde(objekt) is { Kennungen: { } kn } el && kn.TryGetValue("ifc", out var g))
        {
            gid = g.IndexOf(':') is var dp and > 0 ? g[..dp] : g;
            ulBezug ??= kn.GetValueOrDefault("quelle");
        }
        if (string.IsNullOrWhiteSpace(ulBezug))
            throw new HlksFehler("Parameter «unterlage» fehlt.", "Die IFC- bzw. STL/OBJ/STEP-Unterlage nennen, aus der das Objekt stammt.");

        var ulEl = k.Arbeit.Finde(ulBezug);
        if (ulEl is not null && string.Equals(ulEl.Text("art"), "ifc", StringComparison.OrdinalIgnoreCase))
        {
            gid ??= objekt;
            if (ulEl.Text("netz") is not { Length: > 0 } netzName)
                throw new HlksFehler($"Unterlage {ulEl.Kennzeichen} hat keine Netzdatei.", "Die IFC-Datei mit ifc_uebernehmen übernehmen (das erzeugt die Netzdatei).");
            var datei = IfcNetzdatei.Oeffnen(Unterlagen.Pfad(k.Projekt, netzName));
            var pr = datei.Produkt(gid) ?? throw new HlksFehler($"Das Produkt «{gid}» steht nicht in der Netzdatei von {ulEl.Kennzeichen}.",
                "GlobalId des IFC-Produkts angeben (Eigenschaften des Objekts im 3D bzw. Grundriss).");
            var (pu, dr) = datei.Netz(pr);
            var (ecken, dreiecke) = Verschmelzen(pu.Select(x => (double)x).ToArray(), dr);
            var herkunft = $"IFC {pr.Entitaet} {pr.GlobalId}{(pr.Typname is { } t ? $" «{t}»" : "")} aus Unterlage {ulEl.Kennzeichen} ({Unterlagen.OriginalName(ulEl.Text("datei") ?? "")})";
            return (ecken, dreiecke, herkunft, true, pr.Typname);
        }

        var u = UnterlagenService.AufloesenZumLesen(k.Projekt, ulBezug, Name);
        var r = UnterlagenService.Lesen(k.Projekt, u);
        var bid = objekt.Contains('#') ? objekt : $"{u.Pruefsumme}#{objekt}";
        var b = r.Befunde.Befunde.FirstOrDefault(x => string.Equals(x.Id, bid, StringComparison.OrdinalIgnoreCase))
            ?? throw new HlksFehler($"Befund «{objekt}» gibt es in {u.Anzeige} nicht.", "Id eines Befunds form3d angeben (Abfrage befunde mit art form3d).");
        if (b.Art != "form3d") throw new HlksFehler($"Befund {b.Id} ist {b.Art}, nicht form3d.", "Einen Befund form3d (Netz eines 3D-Modells) wählen.");
        bool verein = Param.Bool(p, "vereinfacht");
        var quelle = verein ? b.Wert?["vereinfacht"] : b.Wert;
        if (quelle?["ecken"] is not JsonArray ea || quelle["dreiecke"] is not JsonArray da)
            throw new HlksFehler($"Befund {b.Id} enthält das volle Netz nicht (zu gross).", "vereinfacht = true setzen.");
        var punkte = ea.SelectMany(e => ((JsonArray)e!).Select(c => Json.Zahl(c) ?? 0)).ToArray();
        var idx = da.SelectMany(d => ((JsonArray)d!).Select(c => (int)(Json.Zahl(c) ?? 0))).ToArray();
        var (ek, dk) = Verschmelzen(punkte, idx);
        return (ek, dk, $"Befund {b.Id}{(verein ? " (vereinfacht)" : "")} aus Unterlage {u.Anzeige} ({u.Original})", false, null);
    }

    /// <summary>Doppelte Punkte (näher als 0.05 mm) zusammenlegen, entartete Dreiecke weglassen.</summary>
    public static (List<double[]> Ecken, List<int[]> Dreiecke) Verschmelzen(double[] punkte, int[] dreiecke)
    {
        var ecken = new List<double[]>();
        var nach = new Dictionary<(long, long, long), int>();
        var neu = new int[punkte.Length / 3];
        for (int i = 0; i < neu.Length; i++)
        {
            var key = ((long)Math.Round(punkte[3 * i] * 10), (long)Math.Round(punkte[3 * i + 1] * 10), (long)Math.Round(punkte[3 * i + 2] * 10));
            if (!nach.TryGetValue(key, out var n)) { n = ecken.Count; nach[key] = n; ecken.Add([punkte[3 * i], punkte[3 * i + 1], punkte[3 * i + 2]]); }
            neu[i] = n;
        }
        var dr = new List<int[]>(dreiecke.Length / 3);
        for (int t = 0; t + 2 < dreiecke.Length; t += 3)
        {
            if ((uint)dreiecke[t] >= neu.Length || (uint)dreiecke[t + 1] >= neu.Length || (uint)dreiecke[t + 2] >= neu.Length) continue;
            int a = neu[dreiecke[t]], b = neu[dreiecke[t + 1]], c = neu[dreiecke[t + 2]];
            if (a != b && b != c && a != c) dr.Add([a, b, c]);
        }
        return (ecken, dr);
    }

    /// <summary>Drehung im Grundriss in Grad (−45 … 45) aus dem kleinsten umschliessenden Rechteck der Ecken.</summary>
    public static double Ausrichtung(List<double[]> ecken)
    {
        var h = Konvex(ecken.Select(e => (e[0], e[1])).Distinct().ToList());
        if (h.Count < 3) return 0;
        double bester = double.MaxValue, winkel = 0;
        for (int i = 0; i < h.Count; i++)
        {
            var (ax, ay) = h[i]; var (bx, by) = h[(i + 1) % h.Count];
            double w = Math.Atan2(by - ay, bx - ax), c = Math.Cos(w), s = Math.Sin(w);
            double x0 = double.MaxValue, x1 = double.MinValue, y0 = double.MaxValue, y1 = double.MinValue;
            foreach (var (x, y) in h)
            {
                double u = x * c + y * s, v = -x * s + y * c;
                x0 = Math.Min(x0, u); x1 = Math.Max(x1, u); y0 = Math.Min(y0, v); y1 = Math.Max(y1, v);
            }
            double f = (x1 - x0) * (y1 - y0);
            if (f < bester - 1e-6) { bester = f; winkel = w; }
        }
        double g = winkel * 180 / Math.PI;
        g = ((g % 90) + 90) % 90;
        if (g > 45) g -= 90;
        return Math.Abs(g) < 0.01 ? 0 : Math.Round(g, 2);
    }

    static List<(double X, double Y)> Konvex(List<(double X, double Y)> p)
    {
        p.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
        if (p.Count < 3) return p;
        static double Kreuz((double X, double Y) o, (double X, double Y) a, (double X, double Y) b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
        var h = new List<(double, double)>();
        foreach (var q in p) { while (h.Count >= 2 && Kreuz(h[^2], h[^1], q) <= 0) h.RemoveAt(h.Count - 1); h.Add(q); }
        int unten = h.Count + 1;
        for (int i = p.Count - 2; i >= 0; i--) { while (h.Count >= unten && Kreuz(h[^2], h[^1], p[i]) <= 0) h.RemoveAt(h.Count - 1); h.Add(p[i]); }
        h.RemoveAt(h.Count - 1);
        return h;
    }

    static void Drehen(List<double[]> ecken, double grad)
    {
        double w = grad * Math.PI / 180, c = Math.Cos(w), s = Math.Sin(w);
        foreach (var e in ecken) { double x = e[0], y = e[1]; e[0] = x * c - y * s; e[1] = x * s + y * c; }
    }

    static (double[] Min, double[] Max) Huelle(List<double[]> ecken)
    {
        double[] min = [double.MaxValue, double.MaxValue, double.MaxValue], max = [double.MinValue, double.MinValue, double.MinValue];
        foreach (var e in ecken) for (int i = 0; i < 3; i++) { min[i] = Math.Min(min[i], e[i]); max[i] = Math.Max(max[i], e[i]); }
        return (min, max);
    }
}
