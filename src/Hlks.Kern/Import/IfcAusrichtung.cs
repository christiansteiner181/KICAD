// Ausrichtung eines IFC im Projekt – transparent (Christian 17.10.2026: «mit Revit haben wir immer Probleme bei der
// Ausrichtung der Modelle, besonders beim Export. Dies muss transparent aufgebaut werden.»).
//
// Grundsatz: Kein Modell wird stillschweigend verschoben. Der erste IFC-Übertrag legt den Projektbezug fest (Gebäude-
// ursprung und ±0.00 der Datei, Landeskoordinaten und Koordinationskörper, sofern vorhanden) und schreibt ihn mit Quelle
// in die Projektinformation. Jede weitere Datei wird dagegen geprüft – in dieser Reihenfolge, die genaueste Angabe zählt:
//   1. Koordinationskörper (gleiches Objekt in beiden Dateien, Lage und Drehung)
//   2. Landeskoordinaten (IfcMapConversion bzw. ePSet_MapConversion beider Dateien)
//   3. Gebäudeursprung (nur ein Hinweis: zwei Programme dürfen das Gebäude verschieden platzieren)
// Passt die Datei (≤ 10 mm, ≤ 0.01°), gilt der Projektbezug. Weicht sie ab, bricht der Übertrag mit den Zahlen ab und
// verlangt eine ausdrückliche Wahl (Parameter ausrichtung): projekt (Weltkoordinaten der Datei wie sie sind),
// gebaeude (Gebäudeursprung der Datei auf den Projektursprung), koordinationskoerper (Koordinationskörper aufeinander).
// Die gewählte Lage steht in der Unterlage (Eigenschaft lage) – mit allen Zahlen, auch für den späteren Export (Modul 7).

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public sealed record IfcAusrichtungsErgebnis(IfcBezug Bezug, string Bewertung, string Text, JsonObject Bericht, bool Neu);

public static class IfcAusrichtung
{
    public const double ToleranzMm = 10;
    public const double ToleranzGrad = 0.01;
    public static readonly string[] Arten = ["projekt", "gebaeude", "koordinationskoerper"];

    /// <summary>Bestimmt den Bezug für diese Datei. projektBezug = ifcBezug der Projektinformation (oder null beim ersten IFC),
    /// unterlageLage = gespeicherte Lage einer früheren Fassung derselben Unterlage, wahl = Parameter ausrichtung,
    /// vorgabe = Parameter bezug (ausdrücklicher Bezug), bestehend = Begrenzung des bestehenden Modells (Inhaltsvergleich,
    /// siehe <see cref="ModellBegrenzung"/>).</summary>
    public static IfcAusrichtungsErgebnis Bestimmen(IfcDatei d, JsonObject? projektBezug, JsonObject? unterlageLage, string? wahl, JsonNode? vorgabe, string? kkName,
        double[]? bestehend = null)
    {
        var lage = d.Lage(kkName);
        var eigener = d.BezugVorschlag();
        var bericht = Bericht(d, lage, eigener);
        if (wahl is not null && !Arten.Contains(wahl)) throw new HlksFehler($"Ausrichtung «{wahl}» gibt es nicht.", "ausrichtung: " + string.Join(" | ", Arten));

        // Ausdrücklicher Bezug geht allem vor
        if (IfcBezug.Aus(vorgabe) is { } ausdruecklich)
        {
            bericht["ausrichtung"] = "ausdruecklich";
            return Ende(d, ausdruecklich, "ausdruecklich", $"Ausdrücklich vorgegebener Bezug {Text(ausdruecklich)}.", bericht, projektBezug is null);
        }

        var proj = IfcBezug.Aus(projektBezug);
        if (proj is null)
        {
            bericht["ausrichtung"] = "erste Datei";
            // Projekt mit schon gezeichnetem Inhalt: nur warnen – die erste Datei setzt den Bezug, eine andere Wahl gibt es nicht
            var erste = bestehend is null ? null : Inhalt(d, eigener, bestehend);
            if (erste is not null) bericht["inhalt"] = erste;
            return Ende(d, eigener, "erster", $"Erste IFC-Datei: Projektbezug = Gebäudeursprung und ±0.00 dieser Datei {Text(eigener)}." +
                (erste is null ? "" : (Json.Zahl(erste["ueberdeckung"]) ?? 1) < MindestUeberdeckung
                    ? $" ACHTUNG: Der Inhalt liegt neben dem schon gezeichneten Modell ({InhaltText(erste)}) – mit bezug = {{x, y, z, drehung}} ausdrücklich einpassen."
                    : $" Inhalt: {InhaltText(erste)}."), bericht, true);
        }
        // Genau die Datei, aus der der Projektbezug stammt (gleiche Prüfsumme im Namen): sie ist der Bezug
        if (projektBezug!["quelle"] is JsonValue qv && qv.TryGetValue<string>(out var q) && q.StartsWith(Path.GetFileName(d.Pfad) + " ", StringComparison.OrdinalIgnoreCase))
        {
            bericht["ausrichtung"] = "erste Datei";
            return Ende(d, proj, "erster", $"Erste IFC-Datei: Projektbezug = Gebäudeursprung und ±0.00 dieser Datei {Text(proj)}.", bericht, false);
        }

        // Abweichung bestimmen (genaueste Grundlage zuerst)
        var (grundlage, dx, dy, dz, dr) = Abweichung(d, lage, eigener, proj, projektBezug!);

        // Frühere Fassung derselben Unterlage: dieselbe Wahl gilt wieder, solange die Abweichung gleich geblieben ist
        if (wahl is null && unterlageLage?["ausrichtung"] is JsonValue fv && fv.TryGetValue<string>(out var frueher) && Arten.Contains(frueher))
        {
            var fa = unterlageLage["abweichung"] as JsonObject;
            bool gleich = fa is not null && Json.Zahl(fa["dx"]) is double fx && Json.Zahl(fa["dy"]) is double fy && Json.Zahl(fa["dz"]) is double fz
                          && Math.Sqrt((fx - dx) * (fx - dx) + (fy - dy) * (fy - dy) + (fz - dz) * (fz - dz)) <= ToleranzMm
                          && Math.Abs((Json.Zahl(fa["drehung"]) ?? 0) - dr) <= ToleranzGrad;
            if (gleich) wahl = frueher;
            else bericht["geaendert"] = "Die Lage dieser Datei hat sich seit der letzten Fassung geändert – Ausrichtung neu wählen.";
        }
        bericht["abweichung"] = new JsonObject
        {
            ["grundlage"] = grundlage, ["dx"] = R(dx), ["dy"] = R(dy), ["dz"] = R(dz), ["drehung"] = Math.Round(dr, 4),
            ["text"] = $"{Beschreibung(grundlage)}: {dx / 1000:+0.000;-0.000;0.000} m Ost, {dy / 1000:+0.000;-0.000;0.000} m Nord, {dz / 1000:+0.000;-0.000;0.000} m Höhe, {dr:+0.000;-0.000;0.000}° gegen den Projektbezug",
        };
        bool passt = Math.Sqrt(dx * dx + dy * dy + dz * dz) <= ToleranzMm && Math.Abs(dr) <= ToleranzGrad;

        // Inhaltsvergleich: liegt der Inhalt der Datei mit dem Projektbezug im bestehenden Modell? Ablehnen nur, wenn die Lage
        // allein auf dem Gebäudeursprung beruht (schwacher Hinweis); Koordinationskörper und Landeskoordinaten sind eine
        // ausdrückliche Abmachung – dort darf der Inhalt woanders liegen (Nebengebäude), der Bericht nennt die Überdeckung.
        var inhaltProjekt = bestehend is null ? null : Inhalt(d, proj, bestehend);
        bool daneben = grundlage == "gebaeude" && inhaltProjekt is not null && (Json.Zahl(inhaltProjekt["ueberdeckung"]) ?? 1) < MindestUeberdeckung;
        if (inhaltProjekt is not null) bericht["inhalt"] = inhaltProjekt;

        if (passt && !daneben && wahl is null or "projekt")
        {
            bericht["ausrichtung"] = "projekt";
            return Ende(d, proj, "passt", $"Lage passt zum Projektbezug ({Beschreibung(grundlage)}, Abweichung ≤ {ToleranzMm} mm)" +
                (inhaltProjekt is null ? "." : $"; {InhaltText(inhaltProjekt)}."), bericht, false);
        }
        if (wahl is null)
        {
            bericht["ausrichtung"] = "offen";
            bericht["bezug"] = proj.AlsJson();
            bericht["bewertung"] = "abweichung";
            string inhalt = "";
            if (inhaltProjekt is not null && bestehend is not null)
            {
                var inhaltGebaeude = Inhalt(d, eigener, bestehend);
                bericht["inhaltJeWahl"] = new JsonObject { ["projekt"] = inhaltProjekt["ueberdeckung"]?.DeepClone(), ["gebaeude"] = inhaltGebaeude?["ueberdeckung"]?.DeepClone() };
                double up = Json.Zahl(inhaltProjekt["ueberdeckung"]) ?? 0, ug = Json.Zahl(inhaltGebaeude?["ueberdeckung"]) ?? 0;
                inhalt = $" Inhalt mit «projekt»: {InhaltText(inhaltProjekt)}" + (inhaltGebaeude is null ? "." : $"; mit «gebaeude»: {InhaltText(inhaltGebaeude)}.") +
                         (up < MindestUeberdeckung && ug < MindestUeberdeckung
                             ? " Keine der beiden Lagen trifft das bestehende Modell – Koordinationskörper in beiden Modellen setzen oder den Bezug ausdrücklich angeben (bezug = {x, y, z, drehung})."
                             : up >= 0.8 && ug < MindestUeberdeckung ? " Empfehlung: ausrichtung = projekt (der Inhalt liegt dann im bestehenden Modell)."
                             : ug >= 0.8 && up < MindestUeberdeckung ? " Empfehlung: ausrichtung = gebaeude (der Inhalt liegt dann im bestehenden Modell)."
                             : "");
                if (up >= 0.8 && ug < MindestUeberdeckung) bericht["empfehlung"] = "projekt";
                else if (ug >= 0.8 && up < MindestUeberdeckung) bericht["empfehlung"] = "gebaeude";
            }
            var t = (passt && daneben
                        ? "ACHTUNG Inhalt: Der Gebäudeursprung passt, der Inhalt liegt aber neben dem bestehenden Modell."
                        : $"ACHTUNG Lage: {bericht["abweichung"]!["text"]}.")
                    + inhalt + (bericht["geaendert"] is { } g ? " " + g : "") + " Ohne ausdrückliche Wahl wird nichts übernommen.";
            bericht["text"] = t;
            return new IfcAusrichtungsErgebnis(proj, "abweichung", t, bericht, false);
        }

        IfcBezug bezug = wahl switch
        {
            "projekt" => proj,
            "gebaeude" => eigener,
            _ => NachKoordinationskoerper(lage, projektBezug!) ?? throw new HlksFehler(
                "Ausrichtung nach Koordinationskörper geht nicht: " + (lage.KoordinationskoerperWelt is null ? "die Datei hat keinen." : "das Projekt hat noch keinen (erste Datei ohne Koordinationskörper)."),
                "Koordinationskörper in beiden Modellen platzieren (gleicher Name, z. B. «Koordinationskörper») oder ausrichtung = projekt | gebaeude wählen."),
        };
        bericht["ausrichtung"] = wahl;
        var gewaehlt = bestehend is null ? null : Inhalt(d, bezug, bestehend);
        if (gewaehlt is not null) bericht["inhalt"] = gewaehlt;
        return Ende(d, bezug, "gewaehlt", $"Ausrichtung «{wahl}» gewählt: Bezug dieser Datei {Text(bezug)} (Projektbezug {Text(proj)})." +
            (gewaehlt is null ? "" : (Json.Zahl(gewaehlt["ueberdeckung"]) ?? 1) < MindestUeberdeckung
                ? $" ACHTUNG: Der Inhalt liegt neben dem bestehenden Modell ({InhaltText(gewaehlt)})."
                : $" Inhalt: {InhaltText(gewaehlt)}."), bericht, false);
    }

    /// <summary>Anteil des Inhalts einer weiteren Datei, der mindestens im bestehenden Modell liegen muss, damit eine Lage, die nur
    /// auf dem Gebäudeursprung beruht, ohne Rückfrage gilt. Befund Abnahme 1b vom 18.10.2026: das H+K-IFC (Revit) von Bernstrasse
    /// hat denselben Gebäudeursprung wie das Archicad-IFC, sein Inhalt liegt aber gut 30 m daneben – der Vergleich der Ursprünge
    /// allein zeigt das nicht.</summary>
    public const double MindestUeberdeckung = 0.1;

    /// <summary>Inhaltsvergleich unter einem Bezug: Bauteile der Datei (Mitten ihrer Körper, Stichprobe) gegen die Begrenzung des
    /// bestehenden Modells. ueberdeckung = Anteil der Bauteile, die im Modell liegen (1 m Rand) – robust gegen einzelne Bauteile im
    /// Ursprung oder weit draussen (Nova); versatz = Median der Bauteile minus Mitte des Modells (m, Projektachsen).</summary>
    public static JsonObject? Inhalt(IfcDatei d, IfcBezug bezug, double[] bestehend)
    {
        var welt = InhaltsPunkte(d);
        if (welt.Count == 0) return null;
        var alt = d.Bezug; d.Bezug = bezug;
        try
        {
            var pts = welt.Select(d.Modellpunkt).ToList();
            const double rand = 1000;
            int innen = pts.Count(q => q[0] >= bestehend[0] - rand && q[0] <= bestehend[2] + rand && q[1] >= bestehend[1] - rand && q[1] <= bestehend[3] + rand);
            var xs = pts.Select(q => q[0]).Order().ToList(); var ys = pts.Select(q => q[1]).Order().ToList();
            static JsonArray M(params double[] w) => new(w.Select(x => (JsonNode)Math.Round(x / 1000, 1)).ToArray());
            return new JsonObject
            {
                ["ueberdeckung"] = Math.Round((double)innen / pts.Count, 2),
                ["versatzM"] = M(xs[xs.Count / 2] - (bestehend[0] + bestehend[2]) / 2, ys[ys.Count / 2] - (bestehend[1] + bestehend[3]) / 2),
                ["dateiM"] = M(Begrenzung(pts)!), ["modellM"] = M(bestehend), ["bauteile"] = pts.Count,
            };
        }
        finally { d.Bezug = alt; }
    }

    static string InhaltText(JsonObject i)
    {
        var v = i["versatzM"]!.AsArray();
        if ((Json.Zahl(i["ueberdeckung"]) ?? 0) >= 0.5) return $"{(Json.Zahl(i["ueberdeckung"]) ?? 0) * 100:0} % der Bauteile im bestehenden Modell";
        return $"{(Json.Zahl(i["ueberdeckung"]) ?? 0) * 100:0} % der Bauteile im bestehenden Modell, Mitte {Json.Zahl(v[0]):+0.0;-0.0;0.0} m Ost, {Json.Zahl(v[1]):+0.0;-0.0;0.0} m Nord daneben";
    }

    /// <summary>Höchstzahl Bauteile, deren Körper für den Inhaltsvergleich gerechnet werden (gleichmässige Stichprobe).</summary>
    const int Stichprobe = 400;

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IfcDatei, List<double[]>> inhaltsPunkte = new();

    /// <summary>Mitten der Körper einer Stichprobe von Bauteilen in Weltkoordinaten der Datei (mm), einmal je Datei. Nicht die
    /// Platzierungen: Nova legt alle Bauteile in den Ursprung und schreibt die Lage in die Geometrie.</summary>
    static List<double[]> InhaltsPunkte(IfcDatei d) => inhaltsPunkte.GetValue(d, x =>
    {
        var alt = x.Bezug; x.Bezug = new IfcBezug(0, 0, 0, 0);
        try
        {
            var g = IfcGeometrie.Von(x);
            var produkte = x.Modell.Instances.OfType<Xbim.Ifc4.Interfaces.IIfcProduct>()
                .Where(p => p is not (Xbim.Ifc4.Interfaces.IIfcSpatialStructureElement or Xbim.Ifc4.Interfaces.IIfcDistributionPort
                                or Xbim.Ifc4.Interfaces.IIfcOpeningElement or Xbim.Ifc4.Interfaces.IIfcVirtualElement
                                or Xbim.Ifc4.Interfaces.IIfcAnnotation or Xbim.Ifc4.Interfaces.IIfcGrid)
                            && IfcGeometrie.HatKoerper(p))
                .OrderBy(p => p.EntityLabel).ToList();
            int schritt = Math.Max(1, produkte.Count / Stichprobe);
            var pts = new List<double[]>();
            for (int i = 0; i < produkte.Count; i += schritt)
            {
                double[]? b;
                try { b = g.Koerper(produkte[i], merken: false)?.Begrenzung(); }
                catch (Exception ex) when (ex is not OutOfMemoryException) { b = null; }   // ein unlesbarer Körper verdirbt den Vergleich nicht
                if (b is not null) pts.Add([(b[0] + b[3]) / 2, (b[1] + b[4]) / 2, (b[2] + b[5]) / 2]);
            }
            return pts;
        }
        finally { x.Bezug = alt; }
    });

    /// <summary>Begrenzung [minX, minY, maxX, maxY] (2 %/98 %, Ausreisser zählen nicht); null ohne Punkte.</summary>
    public static double[]? Begrenzung(IEnumerable<double[]> punkte)
    {
        var l = punkte.Where(p => p.Length >= 2 && double.IsFinite(p[0]) && double.IsFinite(p[1])).ToList();
        if (l.Count == 0) return null;
        var xs = l.Select(p => p[0]).Order().ToList(); var ys = l.Select(p => p[1]).Order().ToList();
        double Q(List<double> w, double a) => w[(int)Math.Round(a * (w.Count - 1))];
        return [Q(xs, 0.02), Q(ys, 0.02), Q(xs, 0.98), Q(ys, 0.98)];
    }

    /// <summary>Begrenzung des bestehenden Modells (Elemente mit Modellgrafik, ohne Unterlagen und ohne die Elemente der Unterlage
    /// selbst, also ohne ihre frühere Fassung) oder null, wenn es nichts zum Vergleichen gibt.</summary>
    public static double[]? ModellBegrenzung(Modell m, Katalog k, string? ohneUnterlage) =>
        Begrenzung(m.Elemente.Where(e => e.Klasse != "Unterlage" && Grafik.IstModellgrafik(k, e)
                                         && (ohneUnterlage is null || e.Kennungen?.GetValueOrDefault("quelle") != ohneUnterlage))
            .SelectMany(e => e.Geometrie!.Punkte));

    static IfcAusrichtungsErgebnis Ende(IfcDatei d, IfcBezug bezug, string bewertung, string text, JsonObject bericht, bool neu)
    {
        bericht["bezug"] = bezug.AlsJson();
        bericht["bewertung"] = bewertung;
        bericht["text"] = text;
        return new IfcAusrichtungsErgebnis(bezug, bewertung, text, bericht, neu);
    }

    /// <summary>Abweichung dieser Datei gegen den Projektbezug in Projektkoordinaten (mm, Grad).</summary>
    static (string Grundlage, double Dx, double Dy, double Dz, double Dr) Abweichung(IfcDatei d, IfcLage lage, IfcBezug eigener, IfcBezug proj, JsonObject projektBezug)
    {
        var alt = d.Bezug;
        d.Bezug = proj;
        try
        {
            // 1. Koordinationskörper
            if (lage.KoordinationskoerperWelt is { } kw && projektBezug["koordinationskoerper"] is JsonObject kkp
                && Json.Zahl(kkp["x"]) is double kx && Json.Zahl(kkp["y"]) is double ky && Json.Zahl(kkp["z"]) is double kz)
            {
                var hier = d.Modellpunkt(kw);
                var dr = Winkel((lage.KoordinationskoerperDrehung ?? 0) - proj.Drehung - (Json.Zahl(kkp["drehung"]) ?? 0));
                return ("koordinationskoerper", hier[0] - kx, hier[1] - ky, hier[2] - kz, dr);
            }
            // 2. Landeskoordinaten: der Projektursprung, über diese Datei in Landeskoordinaten umgerechnet, gegen den gespeicherten
            if (d.Karte() is { } karte && projektBezug["landes"] is JsonObject lp
                && Json.Zahl(lp["ost"]) is double ost && Json.Zahl(lp["nord"]) is double nord && Json.Zahl(lp["hoehe"]) is double hoehe)
            {
                var l = karte.Landes([proj.X, proj.Y, proj.Z]);
                var dr = Winkel(karte.Drehung + proj.Drehung - (Json.Zahl(lp["nordwinkel"]) ?? 0));
                // Landes-Differenz (m) in Projektachsen drehen
                double w = -(karte.Drehung + proj.Drehung) * Math.PI / 180, ex = (l[0] - ost) * 1000, ny = (l[1] - nord) * 1000;
                return ("landeskoordinaten", ex * Math.Cos(w) - ny * Math.Sin(w), ex * Math.Sin(w) + ny * Math.Cos(w), (l[2] - hoehe) * 1000, dr);
            }
            // 3. Gebäudeursprung
            var g = d.Modellpunkt([eigener.X, eigener.Y, eigener.Z]);
            return ("gebaeude", g[0], g[1], g[2], Winkel(eigener.Drehung - proj.Drehung));
        }
        finally { d.Bezug = alt; }
    }

    /// <summary>Bezug, mit dem der Koordinationskörper der Datei auf dem des Projekts liegt.</summary>
    static IfcBezug? NachKoordinationskoerper(IfcLage lage, JsonObject projektBezug)
    {
        if (lage.KoordinationskoerperWelt is not { } kw || projektBezug["koordinationskoerper"] is not JsonObject kkp
            || Json.Zahl(kkp["x"]) is not double kx || Json.Zahl(kkp["y"]) is not double ky || Json.Zahl(kkp["z"]) is not double kz) return null;
        // Modell = R(−dr)·(Welt − B) und Modell(kk) = kkProjekt, Drehung(kk) − dr = Drehung im Projekt
        double dr = (lage.KoordinationskoerperDrehung ?? 0) - (Json.Zahl(kkp["drehung"]) ?? 0);
        var r = Matrix4.DrehungZ(dr).Punkt([kx, ky, kz]);
        return new IfcBezug(kw[0] - r[0], kw[1] - r[1], kw[2] - r[2], dr);
    }

    /// <summary>Lagebericht der Datei mit allen Zahlen (Weltkoordinaten der Datei in m, Winkel in Grad).</summary>
    static JsonObject Bericht(IfcDatei d, IfcLage lage, IfcBezug eigener)
    {
        static JsonArray M(double[] p) => new(p.Select(x => (JsonNode)Math.Round(x / 1000, 4)).ToArray());
        var b = new JsonObject
        {
            ["datei"] = Path.GetFileName(d.Pfad),
            ["programm"] = d.Programm,
            ["gelaende"] = new JsonObject { ["lage"] = M(lage.GelaendeWelt), ["drehung"] = lage.GelaendeDrehung },
            ["gebaeude"] = new JsonObject { ["lage"] = M(lage.GebaeudeWelt), ["drehung"] = lage.GebaeudeDrehung },
            ["nullHoehe"] = Math.Round(lage.NullWeltZ / 1000, 4),
            ["eigenerBezug"] = eigener.AlsJson(),
            ["hinweis"] = "Lagen in den Weltkoordinaten der Datei, in m. Revit: «Gemeinsame Koordinaten» bzw. Vermessungspunkt setzt die Weltlage, " +
                          "«Projektbasispunkt»/«Interner Ursprung» nicht – darum hier alle Angaben nebeneinander.",
        };
        if (lage.RefBreite is not null) b["geografisch"] = new JsonObject { ["breite"] = lage.RefBreite, ["laenge"] = lage.RefLaenge, ["hoehe"] = lage.RefHoehe };
        if (lage.Karte is { } k)
        {
            var nullpunkt = k.Landes([eigener.X, eigener.Y, eigener.Z]);
            b["landes"] = new JsonObject
            {
                ["quelle"] = k.Herkunft, ["bezugssystem"] = k.Bezugssystem, ["massstab"] = k.Massstab,
                ["gebaeudeNullpunkt"] = new JsonObject { ["ost"] = Math.Round(nullpunkt[0], 3), ["nord"] = Math.Round(nullpunkt[1], 3), ["hoehe"] = Math.Round(nullpunkt[2], 3) },
                ["nordwinkel"] = Math.Round(k.Drehung + eigener.Drehung, 6),
            };
        }
        if (lage.KoordinationskoerperWelt is { } kw)
            b["koordinationskoerper"] = new JsonObject { ["name"] = lage.KoordinationskoerperName, ["lage"] = M(kw), ["drehung"] = lage.KoordinationskoerperDrehung };
        return b;
    }

    /// <summary>ifcBezug der Projektinformation beim ersten IFC: Bezug, Quelle, Landeskoordinaten des Projektursprungs und
    /// Lage des Koordinationskörpers in Projektkoordinaten, soweit die Datei sie hat.</summary>
    public static JsonObject ProjektBezug(IfcDatei d, IfcBezug bezug, string? kkName = null)
    {
        var o = bezug.AlsJson();
        o["quelle"] = $"{Path.GetFileName(d.Pfad)} ({d.Schema}, {d.Programm})";
        var lage = d.Lage(kkName);   // Befund B4-2: anderer Name des Koordinationskörpers auch beim ersten Bezug
        var alt = d.Bezug; d.Bezug = bezug;
        try
        {
            if (d.Karte() is { } k)
            {
                var l = k.Landes([bezug.X, bezug.Y, bezug.Z]);
                o["landes"] = new JsonObject { ["ost"] = Math.Round(l[0], 3), ["nord"] = Math.Round(l[1], 3), ["hoehe"] = Math.Round(l[2], 3),
                    ["nordwinkel"] = Math.Round(k.Drehung + bezug.Drehung, 6), ["bezugssystem"] = k.Bezugssystem, ["quelle"] = k.Herkunft };
            }
            if (lage.KoordinationskoerperWelt is { } kw)
            {
                var p = d.Modellpunkt(kw);
                o["koordinationskoerper"] = new JsonObject { ["x"] = R(p[0]), ["y"] = R(p[1]), ["z"] = R(p[2]),
                    ["drehung"] = Math.Round(Winkel((lage.KoordinationskoerperDrehung ?? 0) - bezug.Drehung), 6), ["name"] = lage.KoordinationskoerperName };
            }
        }
        finally { d.Bezug = alt; }
        return o;
    }

    static string Beschreibung(string grundlage) => grundlage switch
    {
        "koordinationskoerper" => "Koordinationskörper",
        "landeskoordinaten" => "Landeskoordinaten",
        _ => "Gebäudeursprung (nur Hinweis – Programme platzieren das Gebäude verschieden)",
    };

    public static string Text(IfcBezug b) => $"[{b.X / 1000:0.###}, {b.Y / 1000:0.###}, {b.Z / 1000:0.###}] m, {b.Drehung:0.####}°";

    static double R(double mm) => Math.Round(mm, 1);

    static double Winkel(double w) { w %= 360; if (w > 180) w -= 360; if (w < -180) w += 360; return w; }
}
