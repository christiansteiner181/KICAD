// Befehle und Abfrage der Geberit Vorwand: vorwand_zeichnen, montageelement_setzen, vorwand_bestuecken, vorwand_stueckliste.
// Elemente entstehen über element_anlegen (gleiche Prüfungen, Kennzeichen aus dem Klassenkürzel VW bzw. ME).

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public sealed class VorwandZeichnen : IBefehl
{
    public string Name => "vorwand_zeichnen";
    public string Beschreibung => Text;
    public const string Text = "Zeichnet eine Geberit Installationsvorwand (Duofix oder GIS) entlang von Punkten, eine Vorwand je gerades Stück. bezugslinie links/rechts = die Punkte liegen auf der Rohbauwand (Rückseite der Vorwand), mitte = frei stehend. Profile, Schienen, Ständer, Befestigungen und Beplankung erzeugt der Kern aus den Montageregeln des Geberit Planungshandbuchs (vorwand_stueckliste zeigt sie). bestuecken setzt danach die Montageelemente hinter die Apparate davor.";
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("geschoss", "text", true, "Geschoss (Kennzeichen oder Id)"),
        new("punkte", "liste", true, "Linienzug [[x,y], …] in mm, mindestens zwei Punkte; ohne z gilt die Geschosskote (OK Fertigboden)"),
        new("system", "text", true, "gis oder duofix"),
        new("tiefe", "zahl", true, "Vorwandtiefe in mm inkl. Beplankung (GIS 100–700, Duofix 135–220 bzw. 420 mit Wandankerverlängerung)"),
        new("hoehe", "zahl", false, "Höhe über OK Fertigboden in mm (teilhoch: Ablagehöhe, Pflicht; raumhoch: fehlt = Höhe des Raums)"),
        new("ausfuehrung", "text", false, "teilhoch (Vorgabe; Höhe = Ablagehöhe, Pflicht) oder raumhoch (bis unter die Decke; ohne hoehe gilt die Höhe des Raums: lichteHoehe › Geometriehöhe › Geschosshöhe)"),
        new("bezugslinie", "text", false, "links oder rechts (Vorgabe links) = Punkte auf der Rohbauwand links/rechts in Zeichenrichtung; mitte = frei stehend, Punkte auf der Achse"),
        new("bauwerk", "text", false, "massiv (Vorgabe) oder trockenbau – Abstand der Montagewinkel"),
        new("bodenaufbau", "zahl", false, "Bodenaufbau OK Rohboden bis OK Fertigboden in mm (Profile stehen auf dem Rohboden)"),
        new("bauweise", "text", false, "vorgefertigt (in Segmenten eingebracht) oder vor_ort; Vorgabe GIS vorgefertigt, Duofix vor Ort"),
        new("bestuecken", "bool", false, "Danach Montageelemente für die Apparate vor der Vorwand setzen (wie vorwand_bestuecken)"),
    ];

    /// <summary>Raumhoch: Höhe des Raums vor der Vorwand (Punkt eine halbe Tiefe vor der Rohbauwand, sonst die Mitte der Linie) – lichteHoehe ›
    /// Geometriehöhe › Geschosshöhe, wie Raumliste und HKAP.</summary>
    static double? Raumhoehe(Kontext k, Element geschoss, double mx, double my, string bezug, double[] a, double[] b)
    {
        double l = Math.Sqrt((b[0] - a[0]) * (b[0] - a[0]) + (b[1] - a[1]) * (b[1] - a[1]));
        if (l > 1)
        {
            double nx = -(b[1] - a[1]) / l, ny = (b[0] - a[0]) / l, d = bezug == "links" ? -100 : bezug == "rechts" ? 100 : 0;   // Raumseite: weg von der Rohbauwand
            mx += nx * d; my += ny * d;
        }
        var raum = k.Arbeit.Elemente.FirstOrDefault(e => e.Klasse == "Raum" && e.Geschoss == geschoss.Id && e.Geometrie?.Enthaelt(mx, my) == true);
        return raum?.Zahl("lichteHoehe") ?? raum?.Geometrie?.Hoehe ?? (raum is not null ? geschoss.Zahl("hoehe") : null);
    }

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var geschoss = ArchParam.Bezug(k, p, "geschoss", "Geschoss");
        var kat = VorwandKatalog.Laden(k.Katalog);
        var system = (Param.Text(p, "system", true) ?? "").Trim().ToLowerInvariant();
        if (!kat.Systeme.TryGetValue(system, out var sys))
            throw new HlksFehler($"System «{system}» gibt es nicht.", $"system: {string.Join(" oder ", kat.Systeme.Keys)}");
        if (Param.Zahl(p, "tiefe") is not double tiefe || tiefe <= 0)
            throw new HlksFehler("Parameter «tiefe» fehlt – der Kern setzt keine Vorgabetiefe.", $"Vorwandtiefe in mm angeben ({sys.Titel}: {sys.TiefeMin:0}–{sys.TiefeMaxGesamt:0} mm, {sys.Quelle("tiefe")}).");
        if (tiefe < sys.TiefeMin || tiefe > sys.TiefeMaxGesamt)
            throw new HlksFehler($"Vorwandtiefe {tiefe:0} mm ausserhalb {sys.TiefeMin:0}–{sys.TiefeMaxGesamt:0} mm für {sys.Titel}.", sys.Quelle("tiefe"));
        string? ausfuehrung = Param.Text(p, "ausfuehrung") is { Length: > 0 } af ? ArchParam.Auswahl(af, ["teilhoch", "raumhoch"], "ausfuehrung") : null;
        double kote0 = geschoss.Zahl("kote") ?? 0;
        double? hoeheP = Param.Zahl(p, "hoehe");
        if (hoeheP is null && ausfuehrung == "raumhoch" && Param.Punkte(p, "punkte") is [var p0, var p1, ..])
            hoeheP = Raumhoehe(k, geschoss, (p0[0] + p1[0]) / 2, (p0[1] + p1[1]) / 2, Param.Text(p, "bezugslinie") ?? "links", p0, p1);
        if (hoeheP is not double hoehe || hoehe <= 0)
            throw new HlksFehler(ausfuehrung == "raumhoch" ? "Höhe fehlt: kein Raum mit Höhe an der Vorwand gefunden." : "Parameter «hoehe» fehlt.",
                ausfuehrung == "raumhoch" ? "hoehe in mm angeben oder die Vorwand in einem Raum mit lichter Höhe zeichnen." : "Ablagehöhe über OK Fertigboden in mm angeben.");
        if (hoehe > sys.HoeheMax) throw new HlksFehler($"Vorwandhöhe {hoehe:0} mm grösser als {sys.HoeheMax:0} mm für {sys.Titel}.", sys.Quelle("hoeheMax"));
        var bezug = ArchParam.Auswahl(Param.Text(p, "bezugslinie") ?? "links", ["mitte", "links", "rechts"], "bezugslinie");
        var bauwerk = ArchParam.Auswahl(Param.Text(p, "bauwerk") ?? "massiv", ["massiv", "trockenbau"], "bauwerk");
        double q = bezug switch { "links" => -tiefe / 2, "rechts" => tiefe / 2, _ => 0 };
        double kote = geschoss.Zahl("kote") ?? 0;
        var pts = Param.Punkte(p, "punkte").Select(r => new[] { r[0], r[1], r.Length > 2 ? r[2] : kote }).ToList();
        if (pts.Count < 2) throw new HlksFehler("Eine Vorwand braucht mindestens zwei Punkte.", "punkte: [[x1, y1], [x2, y2], …] in mm");
        for (int i = 1; i < pts.Count; i++)
            if (Geometrie.Abstand(pts[i - 1], pts[i]) < 1) throw new HlksFehler($"Punkte {i} und {i + 1} fallen zusammen.", "Doppelte Punkte entfernen.");
        var achse = WandZeichnen.Versetzen(pts, q, false);
        var kz = new List<string>();
        for (int i = 0; i + 1 < achse.Count; i++)
        {
            var eig = new JsonObject { ["system"] = system, ["rueckseite"] = bezug == "mitte" ? "keine" : bezug, ["bauwerk"] = bauwerk };
            if (ausfuehrung is not null) eig["ausfuehrung"] = ausfuehrung;
            if (Param.Zahl(p, "bodenaufbau") is double ba) eig["bodenaufbau"] = ba;
            if (Param.Text(p, "bauweise") is { Length: > 0 } bw) eig["bauweise"] = ArchParam.Auswahl(bw, ["vorgefertigt", "vor_ort"], "bauweise");
            new ElementAnlegen().Ausfuehren(k, new JsonObject
            {
                ["klasse"] = "Vorwand", ["geschoss"] = geschoss.Id, ["name"] = $"Vorwand {sys.Titel}", ["eigenschaften"] = eig,
                ["geometrie"] = new JsonObject
                {
                    ["art"] = "linie", ["punkte"] = new JsonArray(ArchParam.Json3(achse[i]), ArchParam.Json3(achse[i + 1])), ["dicke"] = tiefe, ["hoehe"] = hoehe,
                },
            });
            kz.Add(k.Arbeit.Finde(k.Operationen[^1].Id)!.Kennzeichen);
        }
        k.Hinweise.Add($"{kz.Count} Vorwand/Vorwände {sys.Titel} angelegt: {string.Join(", ", kz)}");
        if (Param.Bool(p, "bestuecken"))
            foreach (var v in kz) VorwandBestuecken.Bestuecken(k, k.Arbeit.Finde(v)!);
    }
}

public sealed class MontageelementSetzen : IBefehl
{
    public string Name => "montageelement_setzen";
    public string Beschreibung => Text;
    public const string Text = "Setzt ein Geberit Montageelement in eine Vorwand: an einem Punkt (wird auf die Vorderseite der Vorwand gezogen) oder im Abstand ab dem Vorwandanfang. Typ aus kataloge/vorwand_geberit.json (element), Artikel und Anschlüsse aus dem Katalog.";
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("vorwand", "text", true, "Vorwand (Kennzeichen oder Id)"),
        new("element", "text", true, "Elementtyp, z. B. gis.wc.sigma12.114, duofix.wt.112 (vorwand_stueckliste ohne vorwand listet die Typen)"),
        new("punkt", "liste", false, "Punkt [x, y] in mm; Elementmitte auf die Vorderseite der Vorwand gezogen"),
        new("abstand", "zahl", false, "Statt punkt: Elementmitte ab dem Vorwandanfang entlang der Achse in mm"),
        new("apparat", "text", false, "Apparat, für den das Element gesetzt wird (bezuege.apparat)"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var vorwand = ArchParam.Bezug(k, p, "vorwand", "Vorwand");
        var kat = VorwandKatalog.Laden(k.Katalog);
        var typ = kat.Element(Param.Text(p, "element", true))
            ?? throw new HlksFehler($"Elementtyp «{Param.Text(p, "element")}» gibt es nicht.", $"element: {string.Join(", ", kat.Elemente.Where(e => e.System == vorwand.Text("system")).Select(e => e.Id))}");
        var auf = VorwandGestell.Ableiten(k.Arbeit, k.Katalog, vorwand) ?? throw new HlksFehler($"{vorwand.Kennzeichen} hat keine brauchbare Geometrie.");
        double s;
        if (Param.Zahl(p, "abstand") is double ab) s = ab;
        else if (ArchParam.Punkt(p, "punkt") is { } pt) s = auf.Oertlich(pt[0], pt[1]).S;
        else throw new HlksFehler("Lage fehlt.", "punkt [x, y] oder abstand in mm angeben.");
        s = Math.Clamp(s, typ.Breite / 2, Math.Max(typ.Breite / 2, auf.Laenge - typ.Breite / 2));
        Setzen(k, vorwand, auf, typ, s, Param.Text(p, "apparat"));
    }

    internal static string Setzen(Kontext k, Element vorwand, VorwandAufbau auf, VorwandKatalog.ElementTyp typ, double s, string? apparat)
    {
        var pt = auf.Punkt(s, auf.Tiefe);
        var par = new JsonObject
        {
            ["klasse"] = "Montageelement", ["teilVon"] = vorwand.Id, ["geschoss"] = vorwand.Geschoss, ["name"] = typ.Titel,
            ["eigenschaften"] = new JsonObject { ["element"] = typ.Id, ["artikel"] = typ.Artikel },
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = new JsonArray(new JsonArray(Math.Round(pt[0], 3), Math.Round(pt[1], 3), auf.Okff)) },
        };
        if (typ.Artikel is null) par["eigenschaften"]!.AsObject().Remove("artikel");
        if (apparat is not null) par["bezuege"] = new JsonObject { ["apparat"] = apparat };
        new ElementAnlegen().Ausfuehren(k, par);
        var kz = k.Arbeit.Finde(k.Operationen[^1].Id)!.Kennzeichen;
        k.Hinweise.Add($"{kz}: {typ.Titel}{(typ.Artikel is { } a ? $" ({a})" : "")} in {vorwand.Kennzeichen}");
        return kz;
    }
}

public sealed class VorwandBestuecken : IBefehl
{
    public string Name => "vorwand_bestuecken";
    public string Beschreibung => Text;
    public const string Text = "Setzt Geberit Montageelemente hinter die Apparate (WC, Waschtisch, Urinal …), die vor einer Vorwand stehen: Element nach Apparateklasse und System aus kataloge/vorwand_geberit.json, Mitte hinter dem Apparat. Apparate, die schon ein Element haben, bleiben.";
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("vorwand", "text", true, "Vorwand (Kennzeichen oder Id)"),
        new("reichweite", "zahl", false, "Apparate bis zu diesem Abstand vor der Vorderseite in mm (Vorgabe 1000; Annahme Hauptlauf)"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p) =>
        Bestuecken(k, ArchParam.Bezug(k, p, "vorwand", "Vorwand"), Param.Zahl(p, "reichweite") ?? 1000);

    internal static void Bestuecken(Kontext k, Element vorwand, double reichweite = 1000)
    {
        var auf = VorwandGestell.Ableiten(k.Arbeit, k.Katalog, vorwand) ?? throw new HlksFehler($"{vorwand.Kennzeichen} hat keine brauchbare Geometrie.");
        var kat = VorwandKatalog.Laden(k.Katalog);
        var schon = k.Arbeit.Elemente.Where(e => k.Katalog.IstArt(e, "Montageelement") && e.Bezuege.TryGetValue("apparat", out _))
            .Select(e => e.Bezuege["apparat"]).ToHashSet(StringComparer.Ordinal);
        int n = 0;
        var ohne = new List<string>();
        foreach (var ap in k.Arbeit.Elemente.Where(e => k.Katalog.IstArt(e, "Apparat") && e.Geschoss == vorwand.Geschoss).OrderBy(e => e.Kennzeichen, StringComparer.Ordinal).ToList())
        {
            if (ap.Geometrie?.Punkte is not [var pt, ..] || schon.Contains(ap.Id)) continue;
            var (s, q) = auf.Oertlich(pt[0], pt[1]);
            if (s < 0 || s > auf.Laenge || q < auf.Tiefe - 1 || q > auf.Tiefe + reichweite) continue;
            var typ = kat.FuerApparat(k.Katalog, ap, auf.System);
            if (typ is null) { ohne.Add($"{ap.Kennzeichen} ({ap.Klasse})"); continue; }
            s = Math.Clamp(s, typ.Breite / 2, Math.Max(typ.Breite / 2, auf.Laenge - typ.Breite / 2));
            MontageelementSetzen.Setzen(k, vorwand, auf, typ, s, ap.Id);
            n++;
        }
        k.Hinweise.Add(n == 0 ? $"{vorwand.Kennzeichen}: kein Apparat vor der Vorwand ohne Montageelement." : $"{vorwand.Kennzeichen}: {n} Montageelement(e) gesetzt.");
        if (ohne.Count > 0) k.Hinweise.Add($"Ohne passendes Element im Katalog: {string.Join(", ", ohne)}");
    }
}

/// <summary>Abfrage vorwand_stueckliste: Gestell, Befestigungen, Beplankung und Montageelemente einer Vorwand (bzw. aller), mit Befunden und
/// Annahmen; ohne Vorwände im Modell die Elementtypen des Katalogs.</summary>
public static class VorwandStueckliste
{
    public const string Text = "Stückliste der Geberit Vorwände: Profile bzw. Schienen und Ständer mit Zuschnitt, Befestigungen (Montagewinkel, Wandanbindungen, Wandanker), Beplankung in m², Montageelemente mit Artikelnummer; dazu Befunde (Tiefe, Höhe, Mindesttiefe je Element, Überschneidungen) und die Annahmen, wo das Planungshandbuch keine Zahl hergibt. Ohne vorwand alle Vorwände; dazu die Elementtypen des Katalogs.";
    public static IReadOnlyList<ParameterDef> Parameter { get; } = [new("vorwand", "text", false, "Vorwand (Kennzeichen oder Id); leer = alle")];

    public static JsonObject Abfrage(Projekt p, JsonObject? par)
    {
        par ??= [];
        var m = p.Modell; var k = p.Katalog;
        var nur = Param.Text(par, "vorwand") is { Length: > 0 } v ? m.Finde(v) ?? throw new HlksFehler($"Vorwand «{v}» gibt es nicht.") : null;
        var liste = new JsonArray();
        foreach (var e in m.Elemente.Where(e => k.IstArt(e, "Vorwand") && (nur is null || e.Id == nur.Id)).OrderBy(e => e.Kennzeichen, StringComparer.Ordinal))
            if (VorwandGestell.Ableiten(m, k, e) is { } auf) liste.Add(VorwandGestell.Stueckliste(auf));
        var kat = VorwandKatalog.Laden(k);
        return new JsonObject
        {
            ["vorwaende"] = liste,
            ["elementtypen"] = new JsonArray(kat.Elemente.Select(e => (JsonNode)new JsonObject
            {
                ["id"] = e.Id, ["system"] = e.System, ["titel"] = e.Titel, ["artikel"] = e.Artikel, ["breite"] = e.Breite, ["hoehe"] = e.Hoehe,
                ["mindesttiefe"] = e.Mindesttiefe, ["apparate"] = new JsonArray(e.Apparate.Select(a => (JsonNode)a).ToArray()),
            }).ToArray()),
            ["grundlage"] = "Geberit Planungshandbuch Sanitär 2025 (Dok. 995.828.00.1); Fundstellen und Annahmen in kataloge/vorwand_geberit.json",
        };
    }
}
