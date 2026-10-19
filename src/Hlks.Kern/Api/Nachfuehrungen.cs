// Folgeänderungen: Nach jedem Befehl prüfen Regeln, ob abhängige Elemente nachgeführt werden müssen – z. B. Türen
// und Fenster, wenn ihre Wand verschoben oder gelöscht wird. Die Folgeänderungen landen im selben Paket wie der Befehl
// (ein Rückgängig nimmt alles zurück) und erscheinen in den Hinweisen. Eine Regel sieht nur, was der Befehl geändert hat.

namespace Hlks.Kern;

public interface INachfuehrung
{
    string Name { get; }
    /// <param name="geaendert">Ids der Elemente, die der Befehl (oder eine frühere Regel) gesetzt hat.</param>
    /// <param name="geloescht">Ids der Elemente, die gelöscht wurden (Stand vor dem Löschen in den Operationen).</param>
    void Nachfuehren(Kontext k, IReadOnlySet<string> geaendert, IReadOnlySet<string> geloescht);
}

public static class Nachfuehrungen
{
    /// <summary>Alle Regeln in fester Reihenfolge. Neue Regeln hier eintragen.</summary>
    public static readonly IReadOnlyList<INachfuehrung> Liste = [new WaermebrueckenNachfuehren(), new OeffnungenNachfuehren(), new HuelleNachfuehren(), new DeckenNachfuehren(),
        new FamilienNachfuehren(),   // Modul 2 (B1): Anschlüsse und Kennwerte aus der Familie
        new LeitungenNachfuehren(),    // Modul 3 (B2): Leitungsenden folgen verschobenen Anschlüssen
        new FamilienBibliothekNachfuehren()];   // Modul 2 (Nachtrag B7): Familie.bibliothek im selben Paket (Schreiben danach: Projekt.Uebernehmen)

    const int MaxRunden = 5;   // Regeln dürfen einander auslösen, aber nicht endlos

    public static void Ausfuehren(Kontext k)
    {
        int bearbeitet = 0;
        for (int runde = 0; runde < MaxRunden && bearbeitet < k.Operationen.Count; runde++)
        {
            var neu = k.Operationen.Skip(bearbeitet).ToList();
            bearbeitet = k.Operationen.Count;
            var geaendert = neu.Where(o => o.Art == "element.setzen").Select(o => o.Id).ToHashSet(StringComparer.Ordinal);
            var geloescht = neu.Where(o => o.Art == "element.loeschen").Select(o => o.Id).ToHashSet(StringComparer.Ordinal);
            if (geaendert.Count == 0 && geloescht.Count == 0) return;
            foreach (var r in Liste) r.Nachfuehren(k, geaendert, geloescht);
        }
    }
}
