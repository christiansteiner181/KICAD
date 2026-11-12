// Grundbestand an Familien für neue Projekte (Christian 12.11.2026: «jedes neue projekt [muss] alle freigegebenen familien beinhalten»).
// Lädt jede Familie der Familienbibliothek, deren aktuelle Version freigegeben ist, mit familie_laden (mit Herstellerunterlagen, damit die Quellen im
// Projekt gelten). Entwürfe bleiben draussen. Eine nicht ladbare Familie hält das Anlegen nicht auf, sie steht im Ergebnis.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class FamilienGrundbestand
{
    public sealed record Ergebnis(int Freigegeben, int Geladen, IReadOnlyList<string> Fehler)
    {
        const int Hoechstens = 5;

        /// <summary>Meldung für die Oberfläche; null, wenn alles geladen wurde. Ohne lesbare Bibliothek (nichts freigegeben gefunden) ein eigener
        /// Satz statt «0 von 0 …»; viele Fehler werden auf die ersten fünf gekürzt (die Einblendung ist kurz).</summary>
        public string? Hinweis => Fehler.Count == 0 ? null
            : Freigegeben == 0 ? $"Das Projekt ist ohne Familien angelegt: {string.Join("; ", Fehler).TrimEnd('.')}. Familien lassen sich später laden (Projektbrowser › Familien › Familie laden)."
            : $"{Geladen} von {Freigegeben} freigegebenen Familien geladen. Nicht geladen: {string.Join("; ", Fehler.Take(Hoechstens))}"
              + (Fehler.Count > Hoechstens ? $" und {Fehler.Count - Hoechstens} weitere." : "");
    }

    public static Ergebnis Laden(Projekt p, bool ohneUnterlagen = false)
    {
        var ort = FamilienBibliothekDienst.Ort(p.Katalog);
        if (!ort.Aktiv) return new(0, 0, [ort.Sperre ?? "Familienbibliothek nicht eingestellt"]);
        List<BibIndexEintrag> frei;
        try
        {
            var gelesen = FamilienBibliothekDienst.Lesen(ort);
            frei = (gelesen.Index?.Familien ?? []).Where(f => f.Status == "freigegeben").ToList();
            // Bibliothek nicht erreichbar und kein gemerkter Stand: das Projekt bleibt ohne Familien – das sagen, nicht still leer anlegen
            if (gelesen.Offline && frei.Count == 0) return new(0, 0, [gelesen.Hinweis ?? $"Familienbibliothek nicht erreichbar ({ort.Pfad})"]);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HlksFehler)
        { return new(0, 0, [$"Familienbibliothek nicht lesbar: {ex.Message}"]); }

        var fehler = new List<string>();
        int n = 0;
        foreach (var f in frei)
        {
            var par = new JsonObject { ["id"] = f.Id };
            if (ohneUnterlagen) par["ohne_unterlagen"] = true;
            try
            {
                var e = p.Ausfuehren("familie_laden", par);
                if (e.Angenommen) n++; else fehler.Add($"{f.Name}: {e.Fehler}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HlksFehler) { fehler.Add($"{f.Name}: {ex.Message}"); }
        }
        return new(frei.Count, n, fehler);
    }
}
