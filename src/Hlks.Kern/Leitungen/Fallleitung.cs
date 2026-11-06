// Fallleitung platzieren (Christian 06.11.2026: «es muss ein werkzeug geben, Fallleitung platzieren», «die fallleitung wird über alle etagen
// gezeichnet»): ein Punkt im Grundriss, senkrecht durch alle Geschosse. Ein Element erscheint nur im Grundriss seines Geschosses – deshalb je
// Geschoss ein Rohr (Geschossboden bis Boden des nächsten, das oberste bis zu seiner Oberkante), von oben nach unten in Fliessrichtung über
// leitung_zeichnen und jeweils an das Rohr darüber angeschlossen (von).

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public sealed class FallleitungPlatzieren : IBefehl
{
    public string Name => "fallleitung_platzieren";
    public const string Text = "Setzt eine Fallleitung (senkrecht) an einem Punkt über alle Geschosse des Gebäudes bzw. von_geschoss bis bis_geschoss: je Geschoss ein Rohr vom Geschossboden (Kote) bis zum Boden des nächsten Geschosses (das oberste bis zu seiner Oberkante = Kote + Höhe), von oben nach unten in Fliessrichtung gezeichnet und an das Rohr darüber angeschlossen. Serie, Grösse, System und Strang wie leitung_zeichnen.";
    public const string ParPunkt = "Lage [x, y] in mm";
    public const string ParVon = "Unterstes Geschoss (Kennzeichen oder Id); fehlt = das unterste des Projekts";
    public const string ParBis = "Oberstes Geschoss (Kennzeichen oder Id); fehlt = das oberste des Projekts";
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
        var alle = k.Arbeit.DerKlasse("Geschoss").Where(g => g.Zahl("kote") is not null).OrderBy(g => g.Zahl("kote")).ToList();
        if (alle.Count == 0) throw new HlksFehler("Im Projekt gibt es kein Geschoss mit Kote.", "Zuerst Geschosse mit kote und hoehe anlegen.");
        Element Grenze(string name, Element vorgabe) =>
            Param.Text(p, name) is { Length: > 0 } t ? alle.FirstOrDefault(g => g.Kennzeichen == t || g.Id == t) ?? throw new HlksFehler($"Geschoss «{t}» gibt es nicht (mit Kote).", $"{name}: {string.Join(", ", alle.Select(g => g.Kennzeichen))}") : vorgabe;
        var unten = Grenze("von_geschoss", alle[0]);
        var oben = Grenze("bis_geschoss", alle[^1]);
        int iu = alle.IndexOf(unten), io = alle.IndexOf(oben);
        if (iu > io) throw new HlksFehler($"von_geschoss {unten.Kennzeichen} liegt über bis_geschoss {oben.Kennzeichen}.", "Die beiden Geschosse tauschen.");

        string? darueber = null;
        var rohre = new List<string>();
        for (int i = io; i >= iu; i--)
        {
            var g = alle[i];
            double zu = g.Zahl("kote")!.Value;
            double zo = i + 1 < alle.Count ? alle[i + 1].Zahl("kote")!.Value : zu + (g.Zahl("hoehe") ?? throw new HlksFehler($"Geschoss {g.Kennzeichen} hat keine Höhe.", "hoehe am Geschoss eintragen."));
            if (zo - zu < 1) continue;
            var par = new JsonObject
            {
                ["serie"] = p["serie"]?.DeepClone(), ["groesse"] = p["groesse"]?.DeepClone(), ["geschoss"] = g.Id,
                ["punkte"] = new JsonArray(new JsonArray(pt[0], pt[1], zo), new JsonArray(pt[0], pt[1], zu)),
            };
            if (p["system"] is { } sy) par["system"] = sy.DeepClone();
            if (p["strang"] is { } st) par["strang"] = st.DeepClone();
            if (darueber is not null) par["von"] = new JsonObject { ["element"] = darueber };
            var vorher = k.Arbeit.Elemente.Select(e => e.Id).ToHashSet();
            new LeitungZeichnen().Ausfuehren(k, par);
            var neu = k.Arbeit.Elemente.Where(e => !vorher.Contains(e.Id) && e.Klasse == "Rohr" && e.Geometrie?.Punkte.Count >= 2).ToList();
            if (neu.Count == 0) throw new HlksFehler($"Im Geschoss {g.Kennzeichen} ist kein Rohr entstanden.");
            var tiefstes = neu.OrderBy(e => e.Geometrie!.Punkte.Min(q => q.Length > 2 ? q[2] : 0)).First();
            darueber = tiefstes.Kennzeichen;
            rohre.AddRange(neu.Select(e => e.Kennzeichen));
        }
        k.Hinweise.Add($"Fallleitung bei [{pt[0]:0}, {pt[1]:0}] von {oben.Kennzeichen} bis {unten.Kennzeichen}: {string.Join(", ", rohre)}.");
    }
}
