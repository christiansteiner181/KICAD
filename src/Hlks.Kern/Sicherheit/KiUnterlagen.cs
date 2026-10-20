// Sicherheitsprüfung 3c, Befund B3(b) (berichte/modul-3c-sicherheitspruefung.md): Lesegrenze für Unterlagen in KI-Läufen MIT Internet.
//
// Mit Internet kann eingeschleuster Webinhalt Claude bitten, Projekt-Unterlagen (Pläne, Architekten-IFC, Excel) zu lesen und über eine Abruf-URL
// nach aussen zu tragen. Darum liest die KI in solchen Läufen mit unterlagen_lesen, befunde, unterlage_bild und familie_vergleichen nur:
//   - Herstellerunterlagen: Unterlage-Elemente mit zweck «familie» (dafür sind die Familien-Aufträge da),
//   - aus dem Internet abgerufene Unterlagen (original = http(s)-Adresse, z. B. im selben Lauf mit unterlage_aus_url abgelegt – öffentlich),
//   - ausdrücklich freigegebene Unterlagen: beim Auftrag «Familie aus Unterlagen» die im Auftrag genannten (auch hochgeladene Dateien ohne
//     Element bzw. ohne zweck).
// Dateipfade nimmt die KI mit Internet gar nicht (nur Unterlagen des Projekts). Gesetzt wird die Grenze vom MCP-Server beim Start
// (--nur-familienunterlagen, --unterlagen-erlaubt <Datei>…); sie gilt für das Projekt-Objekt dieses Prozesses.

using System.Runtime.CompilerServices;

namespace Hlks.Kern;

/// <summary>Lesegrenze eines Laufs mit Internet: zusätzlich erlaubte Unterlagen (Dateiname in der Ablage, Kennzeichen oder Id).</summary>
public sealed record KiLesegrenze(IReadOnlyCollection<string> Erlaubt);

public static class KiUnterlagen
{
    static readonly ConditionalWeakTable<Projekt, KiLesegrenze> grenzen = new();

    /// <summary>Begrenzt das Lesen von Unterlagen auf Herstellerunterlagen, abgerufene und die genannten (Lauf mit Internet).</summary>
    public static void NurFamilie(Projekt p, IEnumerable<string>? erlaubt = null) =>
        grenzen.AddOrUpdate(p, new KiLesegrenze([.. (erlaubt ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim())]));

    /// <summary>Die Lesegrenze des Projekts (null = keine).</summary>
    public static KiLesegrenze? Grenze(Projekt p) => grenzen.TryGetValue(p, out var g) ? g : null;

    /// <summary>Wirft, wenn die Unterlage unter der Lesegrenze nicht gelesen werden darf.</summary>
    public static void Pruefen(Projekt p, UnterlageRef u, string werkzeug)
    {
        if (Grenze(p) is not { } g || Erlaubt(p, u, g)) return;
        throw new HlksFehler(
            $"Mit Internet liest die KI nur Herstellerunterlagen (zweck «familie»), aus dem Internet abgerufene und die Unterlagen des Auftrags – «{u.Anzeige}» gehört nicht dazu ({werkzeug}).",
            "Die Frage ohne Internet stellen; oder ein Mensch fügt die Datei als Herstellerunterlage ein (unterlage_einfuegen mit zweck «familie») bzw. nennt sie im Auftrag.");
    }

    /// <summary>Darf die Unterlage unter der Grenze gelesen werden?</summary>
    public static bool Erlaubt(Projekt p, UnterlageRef u, KiLesegrenze g)
    {
        static bool Gleich(string? a, string? b) => a is not null && b is not null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        if (g.Erlaubt.Any(x => Gleich(x, u.Datei) || Gleich(x, u.Kennzeichen) || Gleich(x, u.Id))) return true;
        // alle Unterlage-Elemente derselben Datei: ist eine davon Herstellerunterlage bzw. abgerufen, ist es der Inhalt auch
        return p.Modell.DerKlasse("Unterlage")
            .Where(e => Gleich(e.Id, u.Id) || Gleich(e.Text("datei"), u.Datei))
            .Any(e => e.Text("zweck") == "familie" || IstWeb(e.Text("original")));
    }

    static bool IstWeb(string? s) => s is not null && (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
}
