// Lagen (Christian 05.11.2026: «zum zeigen von höhen in grundrissplänen, arbeiten wir oft mit lagen. diese werden immer auf dem Plan angezeigt
// und können etagenweise vergeben werden. es bräuchte also eine art lagemanager.»). Antworten im Fragefenster 05.11.2026:
// - Lage = Abstand UK roh Decke bis Rohrachse, nur Rohre (nicht Kanäle); «1. Lage UKD −12cm, 2. Lage UKD −24cm usw.»
// - die Lage wird beim Zeichnen bestimmt (Leitungen werden meist nach Lagen gezeichnet)
// - UK roh Decke aus dem Modell (Decke über dem Geschoss), im Lagemanager überschreibbar; Christian 09.11.2026: «uk rohdecke soll aus dem IFC
//   übernommen werden. dies ist im regelfall die betondecke.» → die Betondecke (Aufbau mit «Beton») darüber, sonst die grösste Decke
// - Christian 09.11.2026: «lagen gelten default für alle etagen» → Lagen am Gebäude für alle Geschosse, am Geschoss nur Abweichungen
// - im Plan: so viele Schrägstriche quer über das Rohr wie die Lagenummer, einmal bis zum nächsten Wechsel (Bilder Christian)
// Geschoss: ukRohdecke (mm über Kote, überschreibt das Modell), lagen (Abstände UKD → Achse in mm, z. B. «120, 240, 360»).
// VORGABE (Beispiel Christian 05.11.2026, «usw.»): ohne Angabe 120, 240, 360, 480 mm.

using System.Globalization;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class Lagen
{
    public static readonly double[] Vorgabe = [120, 240, 360, 480];
    /// <summary>ANNAHME: Toleranz, mit der eine Rohrachse einer Lage zugeordnet wird (mm).</summary>
    public const double Toleranz = 5;
    /// <summary>ANNAHME: Eine Betondecke gilt als massgebend, wenn ihre Fläche mindestens diesen Anteil der grössten Decke im Band erreicht –
    /// kleine Betonplatten (Podeste, Stürze) sollen die grosse Decke nicht verdrängen, wenn deren Material im IFC fehlt.</summary>
    public const double BetonMindestanteil = 0.5;

    public const string Text = "Lagen eines Geschosses (Lagemanager): UK roh Decke über der Kote (aus dem Modell – Unterkante der Betondecke darüber, sonst der grössten Decke – oder von Hand " +
        "am Geschoss: ukRohdecke) und die Lagen als Abstand UK roh Decke bis Rohrachse (Gebäude: lagen für alle Geschosse, Geschoss: lagen als Abweichung, z. B. «120, 240, 360»; Vorgabe 120, 240, 360, 480 mm) " +
        "mit der Achshöhe über der Kote je Lage. Nur Rohre haben Lagen; im Grundriss steht die Lage als Schrägstriche (Anzahl = Lage) bis zum nächsten Wechsel.";
    public const string ParGeschoss = "Geschoss (Kennzeichen); ohne: alle Geschosse";
    public static IReadOnlyList<ParameterDef> Parameter { get; } = [new("geschoss", "text", false, ParGeschoss)];

    /// <summary>UK roh Decke über der Geschosskote: Eingabe am Geschoss, sonst die Unterkante der Betondecke darüber (Aufbau mit «Beton», grösste
    /// Fläche), fehlt das Material, die grösste Decke darüber (auf 10 mm gerundet). Christian 09.11.2026: «dies ist im regelfall die betondecke».</summary>
    public static (double? Wert, string Quelle) UkRohdecke(Projekt p, Element g) { var (w, q, _) = UkRohdeckeMit(p, g); return (w, q); }

    /// <summary>Wie UkRohdecke, dazu das Kennzeichen der Decke, aus der der Wert stammt.</summary>
    public static (double? Wert, string Quelle, string? Decke) UkRohdeckeMit(Projekt p, Element g)
    {
        if (g.Zahl("ukRohdecke") is double eigen) return (eigen, "eingabe", null);
        double kote = g.Zahl("kote") ?? 0;
        double oben = kote + (g.Zahl("hoehe") ?? NaechsteKote(p, g, kote) - kote ?? 3000);
        var kand = p.Modell.DerKlasse("Decke")
            .Where(d => d.Text("art") != "boden" && d.Geometrie is { Art: "polygon", Dicke: double dk } gg && dk > 0 && gg.Punkte.Count >= 3)
            .Select(d => (D: d, Z: d.Geometrie!.Punkte.Max(q => q.Length > 2 ? q[2] : 0), Dicke: d.Geometrie.Dicke!.Value,
                Flaeche: Math.Abs(Polygone.Flaecheninhalt(d.Geometrie.Punkte)), Beton: IstBeton(p, d)))
            .Where(d => d.Z > kote + 500 && d.Z <= oben + 1000).ToList();
        if (kand.Count == 0) return (null, "fehlt", null);
        double groesste = kand.Max(d => d.Flaeche);
        var beton = kand.Where(d => d.Beton && d.Flaeche >= BetonMindestanteil * groesste).ToList();
        var w = (beton.Count > 0 ? beton : kand).OrderByDescending(d => d.Flaeche).ThenBy(d => d.D.Kennzeichen, StringComparer.Ordinal).First();
        return (Math.Round((w.Z - w.Dicke - kote) / 10) * 10, "modell", w.D.Kennzeichen);
    }

    /// <summary>Betondecke: «Beton» im IFC-Aufbautext, im Namen des bezogenen Aufbaus oder im Kennzeichen/Namen eines Schichtmaterials (Christian
    /// 13.11.2026, Gesamtprüfung N29: «auch Beton im Aufbaunamen oder in einer Schicht zählt»).</summary>
    public static bool IstBeton(Projekt p, Element decke)
    {
        static bool B(string? t) => t?.Contains("beton", StringComparison.OrdinalIgnoreCase) == true;
        if (B(decke.Text("aufbau"))) return true;
        if (Raumgrenzen.AufbauVon(p.Modell, p.Katalog, decke) is not { } a) return false;
        return B(a.Name) || HuelleHilfe.Schichten(a).Any(x => x.Material is not null && p.Modell.Finde(x.Material) is { } mat && (B(mat.Name) || B(mat.Kennzeichen)) || B(x.Name));
    }

    static double? NaechsteKote(Projekt p, Element g, double kote) =>
        p.Modell.DerKlasse("Geschoss").Where(x => x.TeilVon == g.TeilVon && (x.Zahl("kote") ?? double.MinValue) > kote + 1).Select(x => x.Zahl("kote")).Min();

    /// <summary>Abstände UK roh Decke → Rohrachse je Lage (Lage 1 = erster Eintrag): Geschoss, sonst Gebäude (gilt für alle Geschosse), sonst Vorgabe.</summary>
    public static double[] Abstaende(Projekt p, Element g) => LagenQuelle(p, g).Abstaende;

    /// <summary>Woher die Lagen eines Geschosses kommen: «geschoss», «gebaeude» oder «vorgabe».</summary>
    public static (double[] Abstaende, string Quelle) LagenQuelle(Projekt p, Element g)
    {
        if (Lesen(g.Text("lagen")) is { } eigen) return (eigen, "geschoss");
        if (Gebaeude(p, g) is { } geb && Lesen(geb.Text("lagen")) is { } alle) return (alle, "gebaeude");
        return (Vorgabe, "vorgabe");
    }

    static Element? Gebaeude(Projekt p, Element g) => g.TeilVon is { } t ? p.Modell.Finde(t) : null;

    /// <summary>Ein Wert als Ganzes: wahlweise eine Bezeichnung davor («Lage 1:», «1. Lage UKD »), Vorzeichen, Zahl, Einheit mm/cm/m.</summary>
    static readonly System.Text.RegularExpressions.Regex Abstand = new(@"^(?:.*[:=]|.*[A-Za-zÄÖÜäöü](?=[\s+\-−–]))?\s*[-−–+]?\s*(\d+(?:\.\d+)?)\s*(mm|cm|m)?$",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>Liest die Abstände «120, 240, 360». Einheit je Wert: ohne Angabe und «mm» = mm, «cm» und «m» werden umgerechnet (Christian 05.11.2026
    /// schreibt selbst «UKD −12cm»; vorher galt «12cm» als 12 mm). Ein Minus davor meint dasselbe («unter UK roh Decke») und zählt als Abstand – vorher
    /// fiel der Wert still weg und die Lagen dahinter rückten um eine Nummer vor. Getrennt wird an Komma, Strichpunkt und Zeilenwechsel; jeder Wert
    /// muss als Ganzes lesbar sein (Gegenprüfung B8: «Lage 1: 120, Lage 2: 240» ergab [1, 120, 2, 240], weil jede Ziffernfolge zählte). Was nicht
    /// lesbar ist, kommt nach <paramref name="unlesbar"/> und in die Abfrage, statt still zu fehlen oder falsch zu zählen.</summary>
    static double[]? Lesen(string? t, List<string>? unlesbar = null)
    {
        if (string.IsNullOrWhiteSpace(t)) return null;
        var w = new List<double>();
        foreach (var teil in t.Split([',', ';', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var x = Abstand.Match(teil);
            if (!x.Success || !double.TryParse(x.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || v <= 0) { unlesbar?.Add(teil); continue; }
            w.Add(x.Groups[2].Value.ToLowerInvariant() switch { "cm" => v * 10, "m" => Math.Round(v * 1000, 6), _ => v });
        }
        return w.Count > 0 ? [.. w] : null;
    }

    /// <summary>Hinweis zu Werten in «lagen» (Geschoss bzw. Gebäude), die nicht lesbar sind; null, wenn alles lesbar ist.</summary>
    static string? UnlesbarHinweis(Element g, Element? geb)
    {
        var teile = new List<string>();
        foreach (var e in new[] { g, geb })
        {
            if (e is null) continue;
            var u = new List<string>();
            Lesen(e.Text("lagen"), u);
            if (u.Count > 0) teile.Add($"{e.Kennzeichen}: {string.Join(", ", u.Select(x => $"«{x}»"))}");
        }
        return teile.Count == 0 ? null
            : $"Lagen nicht lesbar ({string.Join("; ", teile)}) – diese Werte zählen nicht. Abstände UK roh Decke bis Rohrachse mit Komma getrennt angeben, z. B. «120, 240, 360» (mm) oder «12cm, 24cm».";
    }

    /// <summary>Lage eines Rohrs (1 …) aus der Höhe seiner Achse; null, wenn es kein Rohr ist, senkrecht oder keiner Lage entspricht.</summary>
    public static int? LageVon(Projekt p, Element e)
    {
        if (!p.Katalog.IstArt(e, "Rohr") || e.Geometrie?.Punkte is not { Count: >= 2 } pts || e.Geschoss is null || p.Modell.Finde(e.Geschoss) is not { } g) return null;
        double? z = null;
        for (int i = 1; i < pts.Count && z is null; i++)
            if (Math.Abs(pts[i][0] - pts[i - 1][0]) + Math.Abs(pts[i][1] - pts[i - 1][1]) > 1 && pts[i - 1].Length > 2) z = pts[i - 1][2];
        if (z is null || UkRohdecke(p, g).Wert is not double ukd) return null;
        double ab = (g.Zahl("kote") ?? 0) + ukd - z.Value;
        var a = Abstaende(p, g);
        for (int i = 0; i < a.Length; i++) if (Math.Abs(a[i] - ab) <= Toleranz) return i + 1;
        return null;
    }

    /// <summary>Abfrage lagen: je Geschoss Kote, UK roh Decke (Quelle) und die Lagen mit Achshöhe über der Kote.</summary>
    public static JsonObject Abfrage(Projekt p, JsonObject par)
    {
        var kz = Param.Text(par, "geschoss")?.Trim();
        var gs = p.Modell.DerKlasse("Geschoss").Where(g => string.IsNullOrEmpty(kz) || g.Kennzeichen == kz || g.Id == kz).OrderBy(g => g.Zahl("kote") ?? 0).ToList();
        if (!string.IsNullOrEmpty(kz) && gs.Count == 0) throw new HlksFehler($"Geschoss «{kz}» gibt es nicht.", "Kennzeichen eines Geschosses angeben oder leer lassen.");
        return new JsonObject
        {
            ["geschosse"] = new JsonArray(gs.Select(g =>
            {
                var (ukd, quelle, decke) = UkRohdeckeMit(p, g);
                var (abst, lq) = LagenQuelle(p, g);
                var geb = Gebaeude(p, g);
                return (JsonNode)new JsonObject
                {
                    ["geschoss"] = g.Kennzeichen, ["name"] = g.Name, ["kote"] = g.Zahl("kote") ?? 0, ["ukRohdecke"] = ukd, ["quelle"] = quelle, ["decke"] = decke,
                    ["vorgabe"] = lq != "geschoss", ["lagenQuelle"] = lq,
                    ["gebaeude"] = geb?.Kennzeichen, ["lagenGebaeude"] = geb?.Text("lagen") is { Length: > 0 } lg ? lg : null,
                    ["hinweis"] = UnlesbarHinweis(g, geb),
                    ["lagen"] = new JsonArray(abst.Select((a, i) => (JsonNode)new JsonObject
                    {
                        ["nr"] = i + 1, ["abstand"] = a, ["hoehe"] = ukd is double u ? u - a : null,
                    }).ToArray()),
                };
            }).ToArray()),
        };
    }

    /// <summary>Lagezeichen eines Rohrs im Grundriss: Anzahl Schrägstriche = Lage, einmal bis zum nächsten Wechsel – nur, wenn das Rohr davor
    /// (Verbindung am Anfang, auch über ein Formteil) nicht dieselbe Lage hat. ANNAHME (Gestaltung): Strich 3 mm, Abstand 1.2 mm, 8 mm nach dem Anfang.</summary>
    public static List<Primitiv> Zeichen(Projekt p, Element e, List<double[]> xy, double M)
    {
        if (LageVon(p, e) is not int n || xy.Count < 2 || GleicheLageDavor(p, e, n)) return [];
        // erstes waagrechtes Stück
        int k = 1;
        while (k < xy.Count && Math.Abs(xy[k][0] - xy[k - 1][0]) + Math.Abs(xy[k][1] - xy[k - 1][1]) < 1) k++;
        if (k >= xy.Count) return [];
        double dx = xy[k][0] - xy[k - 1][0], dy = xy[k][1] - xy[k - 1][1], l = Math.Sqrt(dx * dx + dy * dy);
        dx /= l; dy /= l;
        double ab = Math.Min(8 * M, l / 2), sp = 1.2 * M, h = 1.5 * M;
        double w = Math.Atan2(dy, dx) - Math.PI / 4;   // Strich um 45° gegen die Leitung gedreht (Bilder Christian: «\\» waagrecht, «/» senkrecht)
        double sx = Math.Cos(w) * h, sy = Math.Sin(w) * h;
        var aus = new List<Primitiv>();
        for (int i = 0; i < n; i++)
        {
            double t = ab + (i - (n - 1) / 2.0) * sp;
            double cx = xy[k - 1][0] + dx * t, cy = xy[k - 1][1] + dy * t;
            aus.Add(new Primitiv { Art = "linie", Punkte = [[cx - sx, cy - sy], [cx + sx, cy + sy]], Stil = new Stil { Stift = Stifte.Mittel, Farbe = "#000000" } });   // schwarz (Christian 11.11.2026: «lagen müssen schwarz dargestellt werden»)
        }
        return aus;
    }

    static bool GleicheLageDavor(Projekt p, Element e, int n)
    {
        var m = p.Modell;
        IEnumerable<string> Nachbarn(string el, string? an) => m.VerbindungenVon(el)
            .Select(v => v.Von.Element == el && (an is null || v.Von.Anschluss == an) ? v.Nach.Element : v.Nach.Element == el && (an is null || v.Nach.Anschluss == an) ? v.Von.Element : null)
            .Where(x => x is not null)!;
        foreach (var id in Nachbarn(e.Id, "anfang"))
        {
            if (m.Finde(id) is not { } d) continue;
            if (p.Katalog.IstArt(d, "Rohr")) { if (LageVon(p, d) == n) return true; continue; }
            foreach (var id2 in Nachbarn(d.Id, null))
                if (id2 != e.Id && m.Finde(id2) is { } d2 && p.Katalog.IstArt(d2, "Rohr") && LageVon(p, d2) == n) return true;
        }
        return false;
    }
}
