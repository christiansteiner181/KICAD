// Grundbestand an Familien für neue Projekte (Christian 12.11.2026: «jedes neue projekt [muss] alle freigegebenen familien beinhalten»).
// Lädt jede Familie der Familienbibliothek, deren aktuelle Version freigegeben ist, mit familie_laden (mit Herstellerunterlagen, damit die Quellen im
// Projekt gelten). Entwürfe bleiben draussen. Eine nicht ladbare Familie hält das Anlegen nicht auf, sie steht im Ergebnis.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class FamilienGrundbestand
{
    public sealed record Ergebnis(int Freigegeben, int Geladen, IReadOnlyList<string> Fehler)
    {
        /// <summary>Meldung für die Oberfläche; null, wenn alles geladen wurde.</summary>
        public string? Hinweis => Fehler.Count == 0 ? null
            : $"{Geladen} von {Freigegeben} freigegebenen Familien geladen. Nicht geladen: {string.Join("; ", Fehler)}";
    }

    public static Ergebnis Laden(Projekt p, bool ohneUnterlagen = false)
    {
        var ort = FamilienBibliothekDienst.Ort(p.Katalog);
        if (!ort.Aktiv) return new(0, 0, [ort.Sperre ?? "Familienbibliothek nicht eingestellt"]);
        List<BibIndexEintrag> frei;
        try { frei = (FamilienBibliothekDienst.Lesen(ort).Index?.Familien ?? []).Where(f => f.Status == "freigegeben").ToList(); }
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
