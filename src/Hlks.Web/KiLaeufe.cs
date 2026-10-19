// Höchstens ein KI-Lauf je Projekt (Modul 3c, Teil K): ein Gespräch der KI-Palette oder ein KI-Auftrag. Beide starten Claude Code mit
// dem MCP-Server «hlks» auf demselben Projektordner; zwei Läufe zugleich würden sich gegenseitig Elemente reservieren und Pakete
// überkreuzen. Der Arbeitsplatz-Server hat eine Instanz (Dienstcontainer); GespraechDienst und AuftragDienst belegen und geben frei.

namespace Hlks.Web;

public sealed class KiLaeufe
{
    /// <summary>Ein laufender KI-Lauf: Projektordner, Art (gespraech | auftrag), Id (Gespräch bzw. Auftrag) und Text für die Meldung.</summary>
    public sealed record Lauf(string Ort, string Art, string Id, string Text);

    readonly object sperre = new();
    readonly List<Lauf> laufende = [];

    /// <summary>Belegt das Projekt für einen Lauf; läuft dort schon einer, 409 «Claude arbeitet gerade an …». Dispose gibt frei.</summary>
    public IDisposable Belegen(string ort, string art, string id, string text)
    {
        var lauf = new Lauf(ort, art, id, text);
        lock (sperre)
        {
            if (laufende.FirstOrDefault(x => string.Equals(x.Ort, ort, StringComparison.OrdinalIgnoreCase)) is { } belegt)
                throw new AuftragHttpFehler(409, $"Claude arbeitet gerade an {belegt.Text}.", Vorschlag(belegt));
            laufende.Add(lauf);
        }
        return new Freigabe(this, lauf);
    }

    /// <summary>Der Lauf, der das Projekt gerade belegt (null = frei).</summary>
    public Lauf? Laufend(string ort)
    {
        lock (sperre) return laufende.FirstOrDefault(x => string.Equals(x.Ort, ort, StringComparison.OrdinalIgnoreCase));
    }

    public static string Vorschlag(Lauf l) => l.Art == "auftrag"
        ? $"Warten, bis der Auftrag fertig ist, oder ihn abbrechen (POST /api/auftraege/{l.Id}/abbrechen)."
        : $"Warten, bis die Antwort da ist, oder die Frage abbrechen (POST /api/ki/gespraeche/{l.Id}/abbrechen).";

    void Freigeben(Lauf l) { lock (sperre) laufende.Remove(l); }

    sealed class Freigabe(KiLaeufe laeufe, Lauf lauf) : IDisposable
    {
        int fertig;
        public void Dispose() { if (Interlocked.Exchange(ref fertig, 1) == 0) laeufe.Freigeben(lauf); }
    }
}
