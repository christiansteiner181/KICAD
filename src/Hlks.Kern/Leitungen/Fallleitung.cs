// Senkrechte Leitungen über mehrere Geschosse (Christian 06.11.2026):
// - Fallleitung: «es muss ein werkzeug geben, Fallleitung platzieren», «die fallleitung wird über alle etagen gezeichnet», «eine fallleitung ist immer
//   abwasser. diese wird immer mit 88.5° abgang angeschlossen» (der Abgang: leitung_abzweigen an einer senkrechten Abwasserleitung).
// - Steigleitung: «weiteres modul "steigleitung" in gleicher Art für KW, WW, Zirk, VL, RL es soll gewählt werden können welche leitungen in der
//   steigleitung enthalten sind» – mehrere Leitungen in einer Reihe mit Achsabstand.
// Ein Element erscheint nur im Grundriss seines Geschosses – deshalb je Geschoss ein Rohr (Geschossboden bis Boden des nächsten, das oberste bis zu
// seiner Oberkante), in Fliessrichtung über leitung_zeichnen gezeichnet und an das Rohr davor angeschlossen (von).

using System.Text.Json.Nodes;

namespace Hlks.Kern;

static class Senkrechte
{
    public const string ParVon = "Unterstes Geschoss (Kennzeichen oder Id); fehlt = das unterste des Projekts";
    public const string ParBis = "Oberstes Geschoss (Kennzeichen oder Id); fehlt = das oberste des Projekts";

    /// <summary>Geschosse mit Kote (aufsteigend) und die Indizes des untersten und obersten gewählten.</summary>
    public static (List<Element> Alle, int Unten, int Oben) Geschosse(Kontext k, JsonObject p)
    {
        var alle = k.Arbeit.DerKlasse("Geschoss").Where(g => g.Zahl("kote") is not null).OrderBy(g => g.Zahl("kote")).ToList();
        if (alle.Count == 0) throw new HlksFehler("Im Projekt gibt es kein Geschoss mit Kote.", "Zuerst Geschosse mit kote und hoehe anlegen.");
        Element Grenze(string name, Element vorgabe) =>
            Param.Text(p, name) is { Length: > 0 } t ? alle.FirstOrDefault(g => g.Kennzeichen == t || g.Id == t) ?? throw new HlksFehler($"Geschoss «{t}» gibt es nicht (mit Kote).", $"{name}: {string.Join(", ", alle.Select(g => g.Kennzeichen))}") : vorgabe;
        var unten = Grenze("von_geschoss", alle[0]);
        var oben = Grenze("bis_geschoss", alle[^1]);
        int iu = alle.IndexOf(unten), io = alle.IndexOf(oben);
        if (iu > io) throw new HlksFehler($"von_geschoss {unten.Kennzeichen} liegt über bis_geschoss {oben.Kennzeichen}.", "Die beiden Geschosse tauschen.");
        return (alle, iu, io);
    }

    /// <summary>Je Geschoss ein senkrechtes Rohr an pt, in Fliessrichtung (abwärts: von oben; sonst von unten) gezeichnet und verbunden.</summary>
    public static List<string> Zeichnen(Kontext k, double[] pt, JsonNode? serie, JsonNode? groesse, string? system, JsonNode? strang,
        List<Element> alle, int iu, int io, bool abwaerts)
    {
        string? davor = null;
        var rohre = new List<string>();
        var reihe = abwaerts ? Enumerable.Range(iu, io - iu + 1).Reverse() : Enumerable.Range(iu, io - iu + 1);
        foreach (var i in reihe)
        {
            var g = alle[i];
            double zu = g.Zahl("kote")!.Value;
            double zo = i + 1 < alle.Count ? alle[i + 1].Zahl("kote")!.Value : zu + (g.Zahl("hoehe") ?? throw new HlksFehler($"Geschoss {g.Kennzeichen} hat keine Höhe.", "hoehe am Geschoss eintragen."));
            if (zo - zu < 1) continue;
            double za = abwaerts ? zo : zu, ze = abwaerts ? zu : zo;
            var par = new JsonObject
            {
                ["serie"] = serie?.DeepClone(), ["groesse"] = groesse?.DeepClone(), ["geschoss"] = g.Id,
                ["punkte"] = new JsonArray(new JsonArray(pt[0], pt[1], za), new JsonArray(pt[0], pt[1], ze)),
            };
            if (system is not null) par["system"] = system;
            if (strang is not null) par["strang"] = strang.DeepClone();
            if (davor is not null) par["von"] = new JsonObject { ["element"] = davor };
            var vorher = k.Arbeit.Elemente.Select(e => e.Id).ToHashSet();
            new LeitungZeichnen().Ausfuehren(k, par);
            var neu = k.Arbeit.Elemente.Where(e => !vorher.Contains(e.Id) && e.Klasse == "Rohr" && e.Geometrie?.Punkte.Count >= 2).ToList();
            if (neu.Count == 0) throw new HlksFehler($"Im Geschoss {g.Kennzeichen} ist kein Rohr entstanden.");
            // das Rohr am Ende in Fliessrichtung (abwärts das tiefste, aufwärts das höchste) nimmt das nächste auf
            double Z(Element e, bool max) => max ? e.Geometrie!.Punkte.Max(q => q.Length > 2 ? q[2] : 0) : e.Geometrie!.Punkte.Min(q => q.Length > 2 ? q[2] : 0);
            davor = (abwaerts ? neu.OrderBy(e => Z(e, false)) : neu.OrderByDescending(e => Z(e, true))).First().Kennzeichen;
            rohre.AddRange(neu.Select(e => e.Kennzeichen));
        }
        return rohre;
    }

    /// <summary>System des Projekts mit diesem Medium (bzw. einem der Medien); bei mehreren das erste nach Kennzeichen.</summary>
    public static Element? SystemMitMedium(Kontext k, ICollection<string> medien, string? bevorzugt = null) =>
        k.Arbeit.DerKlasse("System").Where(sy => SchemaBefehle.MediumVon(k.Katalog, sy) is { } md && medien.Contains(md))
            .OrderBy(sy => SchemaBefehle.MediumVon(k.Katalog, sy) == bevorzugt ? 0 : 1).ThenBy(sy => sy.Kennzeichen, StringComparer.Ordinal).FirstOrDefault();
}

public sealed class FallleitungPlatzieren : IBefehl
{
    public string Name => "fallleitung_platzieren";
    public const string Text = "Setzt eine Fallleitung (senkrecht, immer Abwasser: ohne system das Schmutzwasser-System des Projekts; Abgänge daran werden mit 88.5° angeschlossen) an einem Punkt über alle Geschosse des Gebäudes bzw. von_geschoss bis bis_geschoss: je Geschoss ein Rohr vom Geschossboden (Kote) bis zum Boden des nächsten Geschosses (das oberste bis zu seiner Oberkante = Kote + Höhe), von oben nach unten in Fliessrichtung gezeichnet und an das Rohr darüber angeschlossen. Serie, Grösse, System und Strang wie leitung_zeichnen.";
    public const string ParPunkt = "Lage [x, y] in mm";
    public const string ParVon = Senkrechte.ParVon;
    public const string ParBis = Senkrechte.ParBis;
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("punkt", "liste", true, ParPunkt),
        new("serie", "text", true, RoutingBefehle.ParSerie),
        new("groesse", "objekt", true, RoutingBefehle.ParGroesse),
        new("system", "text", false, RoutingBefehle.ParSystem),
        new("strang", "text", false, RoutingBefehle.ParStrang),
        new("von_geschoss", "text", false, ParVon),
        new("bis_geschoss", "text", false, ParBis),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var pt = ArchParam.Punkt(p, "punkt") ?? throw new HlksFehler("Parameter «punkt» fehlt.", "punkt: [x, y] in mm");
        var (alle, iu, io) = Senkrechte.Geschosse(k, p);
        // Eine Fallleitung ist immer Abwasser (Christian 06.11.2026): ohne system/strang das Schmutzwasser-System des Projekts, sonst muss es Abwasser sein
        var abwasser = Rohrgroessen.AbwasserMedien(k.Katalog);
        string? systemKz = null;
        if (Param.Text(p, "system") is { Length: > 0 } sysText)
        {
            var sy = k.Arbeit.Finde(sysText) ?? throw new HlksFehler($"System «{sysText}» gibt es nicht.");
            if (sy.Klasse != "System" || SchemaBefehle.MediumVon(k.Katalog, sy) is not { } md || !abwasser.Contains(md))
                throw new HlksFehler($"{sy.Kennzeichen} ist kein Abwasser-System – eine Fallleitung ist immer Abwasser.", "Ein System Schmutz- oder Regenwasser wählen.");
            systemKz = sy.Kennzeichen;
        }
        else if (Param.Text(p, "strang") is not { Length: > 0 })
            systemKz = Senkrechte.SystemMitMedium(k, abwasser, "SW")?.Kennzeichen
                ?? throw new HlksFehler("Im Projekt gibt es kein Abwasser-System.", "Zuerst ein System Schmutzwasser anlegen (System › systemtyp Schmutzwasser).");
        var rohre = Senkrechte.Zeichnen(k, pt, p["serie"], p["groesse"], systemKz, p["strang"], alle, iu, io, abwaerts: true);
        k.Hinweise.Add($"Fallleitung bei [{pt[0]:0}, {pt[1]:0}] von {alle[io].Kennzeichen} bis {alle[iu].Kennzeichen}: {string.Join(", ", rohre)}.");
    }
}

public sealed class SteigleitungPlatzieren : IBefehl
{
    public string Name => "steigleitung_platzieren";
    public const string Text = "Setzt eine Steigleitung aus mehreren senkrechten Leitungen (z. B. KW, WW, Zirk, Heizung VL, RL) in einer Reihe über alle Geschosse bzw. von_geschoss bis bis_geschoss: die erste Leitung am punkt, jede weitere um abstand (Achsabstand) in richtung versetzt. Je Leitung Medium, Serie und Grösse (System ohne Angabe: das System des Projekts mit diesem Medium). Je Geschoss ein Rohr, in Fliessrichtung gezeichnet und verbunden: Zirkulation und Rückläufe fallen, die übrigen steigen.";
    public const string ParPunkt = "Lage der ersten Leitung [x, y] in mm";
    public const string ParRichtung = "Richtung der Reihe [dx, dy] im Grundriss (Vorgabe [1, 0])";
    public const string ParAbstand = "Achsabstand der Leitungen in mm";
    public const string ParLeitungen = "Liste der Leitungen in der Reihenfolge der Reihe: [{ medium (z. B. KW, WW, ZIR, HZ-VL, HZ-RL), serie, groesse, system? }]";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("punkt", "liste", true, ParPunkt),
        new("leitungen", "liste", true, ParLeitungen),
        new("abstand", "zahl", true, ParAbstand),
        new("richtung", "liste", false, ParRichtung),
        new("von_geschoss", "text", false, Senkrechte.ParVon),
        new("bis_geschoss", "text", false, Senkrechte.ParBis),
    ];

    /// <summary>Fällt das Medium (Zirkulation, Rückläufe)? Sonst steigt es (Vor- und Zuläufe).</summary>
    public static bool Faellt(string medium) => medium.Equals("ZIR", StringComparison.OrdinalIgnoreCase) || medium.EndsWith("-RL", StringComparison.OrdinalIgnoreCase)
        || medium.Contains("-RL-", StringComparison.OrdinalIgnoreCase);

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var pt = ArchParam.Punkt(p, "punkt") ?? throw new HlksFehler("Parameter «punkt» fehlt.", "punkt: [x, y] in mm");
        if (p["leitungen"] is not JsonArray liste || liste.Count == 0) throw new HlksFehler("Keine Leitung gewählt.", "leitungen: [{ medium, serie, groesse }]");
        double abstand = Param.Zahl(p, "abstand") ?? throw new HlksFehler("Parameter «abstand» fehlt.", "Achsabstand der Leitungen in mm angeben.");
        if (liste.Count > 1 && abstand <= 0) throw new HlksFehler("Der Achsabstand muss grösser als 0 sein.");
        var r = ArchParam.Punkt(p, "richtung") ?? [1, 0];
        double rl = Math.Sqrt(r[0] * r[0] + r[1] * r[1]);
        if (rl < 1e-9) throw new HlksFehler("Die Richtung der Reihe ist null.", "richtung: [dx, dy]");
        double ux = r[0] / rl, uy = r[1] / rl;
        var (alle, iu, io) = Senkrechte.Geschosse(k, p);
        var berichte = new List<string>();
        for (int i = 0; i < liste.Count; i++)
        {
            if (liste[i] is not JsonObject l) throw new HlksFehler($"leitungen[{i}] ist kein Objekt.", "{ medium, serie, groesse }");
            var medium = Param.Text(l, "medium") ?? throw new HlksFehler($"leitungen[{i}]: medium fehlt.", "z. B. KW, WW, ZIR, HZ-VL, HZ-RL");
            if (!k.Katalog.Medien.ContainsKey(medium)) throw new HlksFehler($"Medium «{medium}» gibt es nicht.", "Medien: " + string.Join(", ", k.Katalog.Medien.Keys));
            string systemKz;
            if (Param.Text(l, "system") is { Length: > 0 } st)
                systemKz = (k.Arbeit.Finde(st) ?? throw new HlksFehler($"System «{st}» gibt es nicht.")).Kennzeichen;
            else
                systemKz = Senkrechte.SystemMitMedium(k, [medium])?.Kennzeichen
                    ?? throw new HlksFehler($"Im Projekt gibt es kein System mit dem Medium {medium}.", $"Zuerst ein System für {medium} anlegen oder system angeben.");
            double[] q = [pt[0] + ux * abstand * i, pt[1] + uy * abstand * i];
            var rohre = Senkrechte.Zeichnen(k, q, l["serie"], l["groesse"], systemKz, null, alle, iu, io, Faellt(medium));
            berichte.Add($"{medium} ({systemKz}) bei [{q[0]:0}, {q[1]:0}]: {string.Join(", ", rohre)}");
        }
        k.Hinweise.Add($"Steigleitung {alle[iu].Kennzeichen}–{alle[io].Kennzeichen}: {string.Join("; ", berichte)}.");
    }
}
