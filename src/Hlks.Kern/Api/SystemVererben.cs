// System an angeschlossene Teile vererben (Christian 04.11.2026: «wird an einem bauteil ein system geändert, so wird das system nach rückfrage
// an alle angeschlossenen teile vererbt»). Die Rückfrage stellt die Oberfläche: Trockenlauf → Anzahl, Ja → ausführen.
// Weitergegeben wird über die Verbindungen durch Leitungsbauteile (Leitung, Formteil, Armatur). Geräte (Heizkörper, Apparat, Pumpe als
// Zentrale, Wärmeerzeuger …) sind die Grenze und bleiben, wie sie sind – sonst liefe das Vorlaufsystem durch den Heizkörper in den Rücklauf.
// Beginnt die Änderung an einem Gerät, wird nur das Gerät geändert (Hinweis). Ebenso bleibt jedes Teil eines anderen Mediums stehen (z. B. der
// Bypass im Rücklauf hinter dem Mischventil des Vorlaufs); Teile ohne System bzw. Medium übernehmen es.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public sealed class SystemVererben : IBefehl
{
    public string Name => "system_vererben";
    public const string Text = "Setzt das System eines Leitungsbauteils und gibt es an alle angeschlossenen Leitungen, Formteile und Armaturen weiter (über die Verbindungen, bis zu den Geräten; Geräte bleiben unverändert). Ohne system gilt das System des Elements. Im Trockenlauf nennt der Hinweis, wie viele Teile sich ändern würden.";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("element", "text", true, "Bauteil, an dem das System geändert wird (Kennzeichen oder Id)"),
        new("system", "text", false, "Neues System (Kennzeichen oder Id); null entfernt das System; ohne Angabe das System des Elements"),
    ];

    /// <summary>Leitungsbauteile, die über Verbindungen und andere Leitungsbauteile mit start zusammenhängen (ohne start), mit demselben Medium
    /// wie start oder ohne Medium.</summary>
    public static List<Element> Angeschlossene(Modell m, Katalog k, Element start)
    {
        var aus = new List<Element>();
        if (!Netz.IstLeitungsbauteil(k, start)) return aus;
        var medium = Netz.ElementMedium(m, start);
        var besucht = new HashSet<string>(StringComparer.Ordinal) { start.Id };
        var schlange = new Queue<Element>([start]);
        while (schlange.Count > 0)
        {
            var e = schlange.Dequeue();
            foreach (var (n, _, _) in Netz.Nachbarn(m, e, null, "beide", null))
            {
                if (!Netz.IstLeitungsbauteil(k, n)) continue;
                if (medium is not null && Netz.ElementMedium(m, n) is { } nm && !nm.Equals(medium, StringComparison.OrdinalIgnoreCase)) continue;
                if (!besucht.Add(n.Id)) continue;
                aus.Add(n);
                schlange.Enqueue(n);
            }
        }
        return aus;
    }

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var start = k.Arbeit.Hole(Param.Text(p, "element", true));
        JsonNode? neu;
        if (p.ContainsKey("system")) neu = Param.Text(p, "system") is { } s ? s : null;
        else if (start.System is { } eigen) neu = eigen;
        else throw new HlksFehler($"{start.Kennzeichen} hat kein System, das weitergegeben werden könnte.", "Parameter system angeben.");
        string? neuId = neu is null ? null : k.Arbeit.Hole(neu.GetValue<string>()).Id;

        var teile = new List<Element> { start };
        teile.AddRange(Angeschlossene(k.Arbeit, k.Katalog, start));
        int geaendert = 0;
        foreach (var e in teile)
        {
            if (e.System == neuId) continue;
            new ElementAendern().Ausfuehren(k, new JsonObject { ["element"] = e.Id, ["system"] = neu?.DeepClone() });
            if (e != start) geaendert++;
        }
        var name = neuId is null ? "kein System" : $"System {k.Arbeit.Finde(neuId)?.Kennzeichen}";
        if (!Netz.IstLeitungsbauteil(k.Katalog, start))
            k.Hinweise.Add($"{start.Kennzeichen} ist ein Gerät: nur dort {name} gesetzt, nichts weitergegeben (Vor- und Rücklauf bleiben getrennt).");
        else
            k.Hinweise.Add($"{name} an {geaendert} angeschlossene Teile weitergegeben ({teile.Count - 1} angeschlossen).");
    }
}
