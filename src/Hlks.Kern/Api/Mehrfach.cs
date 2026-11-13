// Bearbeitungswerkzeuge B4 (entwurf/bearbeitungswerkzeuge.md, Christian 03.11.2026): Befehle für die Mehrfachauswahl – ein Paket für alle Elemente
// (ein Rückgängig), mit denselben Prüfungen wie element_loeschen bzw. element_aendern je Element.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public sealed class ElementeLoeschen : IBefehl
{
    public string Name => "elemente_loeschen";
    public const string Text = "Löscht mehrere Elemente in einem Paket (Mehrfachauswahl), je wie element_loeschen. Verweise zwischen den genannten Elementen sind erlaubt (gelöscht wird in der passenden Reihenfolge); verweist ein anderes Element auf eines davon, wird der ganze Befehl abgelehnt.";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } = [new("elemente", "liste", true, "Kennzeichen oder Ids, z. B. [\"RO-1\", \"BO-1\"]")];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var offen = ArchParam.Elemente(k, p).Select(e => e.Id).Distinct(StringComparer.Ordinal).ToList();
        if (offen.Count == 0) throw new HlksFehler("Keine Elemente angegeben.", "elemente als Liste von Kennzeichen angeben.");
        int gesamt = offen.Count;
        HlksFehler? letzter = null;
        // Runden: zuerst, was niemand mehr braucht; Verweise innerhalb der Auswahl lösen sich so auf
        while (offen.Count > 0)
        {
            int vorher = offen.Count;
            foreach (var id in offen.ToList())
            {
                if (k.Arbeit.Finde(id) is null) { offen.Remove(id); continue; }   // mitgelöscht (z. B. Tür mit ihrer Wand)
                try { new ElementLoeschen().Ausfuehren(k, new JsonObject { ["element"] = id }); offen.Remove(id); }
                catch (HlksFehler f) { letzter = f; }
            }
            if (offen.Count == vorher) throw letzter!;
        }
        k.Hinweise.Insert(0, $"{gesamt} Element(e) gelöscht.");
    }
}

public sealed class ElementeAendern : IBefehl
{
    public string Name => "elemente_aendern";
    public const string Text = "Setzt Eigenschaften mehrerer Elemente in einem Paket (Mehrfachauswahl, Eigenschaftenpalette), je wie element_aendern mit eigenschaften (null entfernt). Lehnt eines ab, gilt keine Änderung.";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("elemente", "liste", true, "Kennzeichen oder Ids"),
        new("eigenschaften", "objekt", true, "{ name: wert | { wert, einheit, quelle } | null }"),
        new("je_element", "objekt", false, "{ Kennzeichen oder Id: { name: wert } } – ersetzt für dieses Element einzelne Eigenschaften (z. B. die Dämmdicke je Leitung nach Regel)"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var liste = ArchParam.Elemente(k, p);
        if (p["eigenschaften"] is not JsonObject eig || eig.Count == 0) throw new HlksFehler("eigenschaften fehlt.", "{ name: wert } angeben.");
        var je = p["je_element"] as JsonObject;
        foreach (var e in liste)
        {
            var eigE = (JsonObject)eig.DeepClone();
            // Werte je Element (Entscheid Christian 13.11.2026, Frage N1: Dämmdicke je Leitung nach Regel und DN, in einem Paket)
            if (je is not null && (je[e.Kennzeichen] ?? je[e.Id]) is JsonObject eigen)
                foreach (var (n, w) in eigen) eigE[n] = w?.DeepClone();
            try { new ElementAendern().Ausfuehren(k, new JsonObject { ["element"] = e.Id, ["eigenschaften"] = eigE }); }
            catch (HlksFehler f) { throw new HlksFehler($"{e.Kennzeichen}: {f.Message}", f.Vorschlag); }
        }
        k.Hinweise.Insert(0, $"{liste.Count} Element(e) geändert: {string.Join(", ", eig.Select(x => x.Key))}.");
    }
}
