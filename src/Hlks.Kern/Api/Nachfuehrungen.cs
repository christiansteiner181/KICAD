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
        new FormteilGroessenNachfuehren(),   // Modul 3 (Nachtrag 21.10.2026): Formteile freier Serien tragen ihre Grösse selbst (vor der Familie)
        new FamilienNachfuehren(),   // Modul 2 (B1): Anschlüsse und Kennwerte aus der Familie
        new LeitungenNachfuehren(),    // Modul 3 (B2): Leitungsenden folgen verschobenen Anschlüssen
        new SanitaerNachfuehren(),     // Modul 4 Teil A: Eingaben der Sanitär-Zusammenstellung prüfen (abflusskennzahl, spezialinstallationen)
        new HeizungNachfuehren(),      // Modul 5 Teil A-D: Mischventil-Anschlüsse nach art, Systempaar beidseitig, Heizgruppen-Angaben prüfen
        new FamilienBibliothekNachfuehren(),   // Modul 2 (Nachtrag B7): Familie.bibliothek im selben Paket (Schreiben danach: Projekt.Uebernehmen)
        new PlanwinkelNachfuehren()];   // N9: Planwinkel am Gebäude, sobald es Wände gibt und noch kein Winkel steht (Planwinkel.cs)

    /// <summary>Runden, die jeder Befehl mindestens bekommt (Regeln dürfen einander auslösen).</summary>
    public const int MindestRunden = 8;
    /// <summary>Obergrenze der Runden, auch im grössten Modell – Schutz gegen Regeln, die einander endlos auslösen.</summary>
    public const int HoechstRunden = 2000;

    /// <summary>Grenze der Runden für ein Modell, an der Netzgrösse ausgerichtet: Eine Kette aus n direkt aneinander hängenden Formteilen
    /// bzw. Armaturen braucht n + 1 Runden (je Runde folgt das nächste Glied, zuletzt die Leitung dahinter), und länger als alle Elemente des
    /// Modells kann keine Kette sein. Bis 12.11.2026 lag die Grenze fest bei 5: ab dem fünften Formteil in Folge blieb der Rest ohne Meldung
    /// stehen und das Netz riss (Gesamtprüfung Runde 2).</summary>
    public static int Rundengrenze(int elemente) => Math.Clamp(elemente + 2, MindestRunden, HoechstRunden);

    public static void Ausfuehren(Kontext k) => Ausfuehren(k, Liste, Rundengrenze(k.Arbeit.Elemente.Count));

    /// <summary>Regeln anwenden, bis keine mehr etwas ändert. Greift die Grenze vorher, steht es in den Hinweisen (nie still aufhören).</summary>
    public static void Ausfuehren(Kontext k, IReadOnlyList<INachfuehrung> regeln, int grenze)
    {
        int bearbeitet = 0;
        for (int runde = 0; bearbeitet < k.Operationen.Count; runde++)
        {
            var neu = k.Operationen.Skip(bearbeitet).ToList();
            var geaendert = neu.Where(o => o.Art == "element.setzen").Select(o => o.Id).ToHashSet(StringComparer.Ordinal);
            var geloescht = neu.Where(o => o.Art == "element.loeschen").Select(o => o.Id).ToHashSet(StringComparer.Ordinal);
            if (geaendert.Count == 0 && geloescht.Count == 0) return;
            if (runde >= grenze)
            {
                var offen = geaendert.Concat(geloescht).Select(id => k.Arbeit.Finde(id)?.Kennzeichen
                    ?? neu.Last(o => o.Id == id).ElementVorher?.Kennzeichen ?? id).Distinct().Order(StringComparer.Ordinal).ToList();
                k.Hinweise.Add($"Nachführung nach {grenze} Runden abgebrochen: die Folgeänderungen lösen einander weiter aus (zuletzt geändert: "
                    + $"{string.Join(", ", offen.Take(5))}{(offen.Count > 5 ? " …" : "")}). Was an diesen Elementen hängt, ist nicht mehr nachgeführt – "
                    + "Prüfbefunde ansehen (P13 nennt Anschlüsse, die auseinanderliegen) oder den Befehl rückgängig machen.");
                return;
            }
            bearbeitet = k.Operationen.Count;
            foreach (var r in regeln) r.Nachfuehren(k, geaendert, geloescht);
        }
    }
}
