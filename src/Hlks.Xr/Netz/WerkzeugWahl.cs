// H+K XR E1 (entwurf/xr-empfaenger.md, «Empfänger-Kern»): Regeln für die Wahl des Werkzeugs in der App «H+K XR» – rein, ohne StereoKit, getestet in
// tests/Hlks.Xr.Tests/WerkzeugTests.cs. Die App (src/Hlks.Quest, QuestApp.Werkzeuge.cs) hält zu jedem gekoppelten, erreichbaren Werkzeug einen Kanal offen;
// genau eines ist aktiv (zeigt Inhalt, bekommt Posen).
//   - Start: mit genau einer Kopplung direkt dort (wie die KICAD-App bis E1, auch ohne PC aus dem Zwischenspeicher); mit mehreren die Startseite (gekoppelte
//     Werkzeuge als grosse Glas-Symbole mit Name, PC und «erreichbar»). Am PC wählt --werkzeug &lt;kennung&gt; vor.
//   - Auftrag: ein Werkzeug, das einen Auftrag schickt (KICAD: VR-Auftrag «In VR zeigen»; Labor: Stellschraube «labor.aufgabe» mit einer Aufgabe), holt die App zu
//     sich, auch wenn ein anderes offen ist – mit kurzer schwebender Meldung «Wechsel zu …». «labor.aufgabe» = null (beenden) holt nicht.

using System.Text.Json;
using Hlks.Xr.Labor;

namespace Hlks.Xr.Netz;

/// <summary>Ist ein gekoppeltes Werkzeug erreichbar? (Suche wie bisher: gespeicherte Adressen, Adressen des Pakets, UDP.)</summary>
public enum Erreichbarkeit { Unbekannt, Sucht, Ja, Nein, Fehler }

/// <summary>Ein Werkzeug auf der Startseite: Kennung, Name, PC, Erreichbarkeit, aktiv (das vorn gezeigte), Farbe, Meldung bei Fehler.</summary>
public sealed record StartEintrag(string Kennung, string Name, string Pc, Erreichbarkeit Erreichbar, bool Aktiv, string FarbId, string? Meldung = null);

public static class WerkzeugWahl
{
    /// <summary>
    /// Werkzeug beim Start: die Vorgabe (am PC --werkzeug), wenn sie gekoppelt ist; sonst mit genau einer Kopplung diese (wie bis E1, auch ohne PC); sonst null –
    /// die App zeigt die Startseite, bis man wählt bzw. ein Auftrag kommt.
    /// </summary>
    public static string? Start(IReadOnlyList<string> gekoppelt, string? vorgabe = null)
    {
        if (vorgabe is not null && gekoppelt.Contains(vorgabe)) return vorgabe;
        return gekoppelt.Count == 1 ? gekoppelt[0] : null;
    }

    /// <summary>
    /// Ist eine Stellschraube vom Kanal eines Werkzeugs ein Auftrag (holt die App zu diesem Werkzeug)? Nur «labor.aufgabe» mit einer Aufgabe (Text, nicht leer);
    /// null beendet nur (holt nicht). VR-Aufträge («auftrag») sind immer Aufträge.
    /// </summary>
    public static bool IstAuftrag(string schluessel, JsonElement wert) =>
        schluessel == LaborKatalog.AufgabeSchluessel && wert.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(wert.GetString());

    /// <summary>Muss die App für einen Auftrag von <paramref name="von"/> wechseln? Ja, wenn ein anderes (oder keines) aktiv ist.</summary>
    public static bool Wechseln(string? aktiv, string von) => aktiv != von;

    /// <summary>Die kurze schwebende Meldung beim Wechsel.</summary>
    public static string Meldung(Werkzeug ziel) => $"Wechsel zu {ziel.Name} …";

    /// <summary>Zeile «erreichbar» auf der Startseite.</summary>
    public static string ZustandText(Erreichbarkeit e, string? meldung = null) => e switch
    {
        Erreichbarkeit.Ja => "erreichbar",
        Erreichbarkeit.Nein => "nicht erreichbar",
        Erreichbarkeit.Sucht => "sucht …",
        Erreichbarkeit.Fehler => string.IsNullOrWhiteSpace(meldung) ? "Fehler" : meldung,
        _ => "noch nicht gesucht",
    };

    /// <summary>Ampel wie das Symbol «Status»: 1 grün (erreichbar), 2 orange (nicht erreichbar), 3 rot (Fehler), 0 grau (sucht bzw. unbekannt).</summary>
    public static int Ampel(Erreichbarkeit e) => e switch { Erreichbarkeit.Ja => 1, Erreichbarkeit.Nein => 2, Erreichbarkeit.Fehler => 3, _ => 0 };

    /// <summary>Die Einträge der Startseite in ihrer Reihenfolge (<see cref="Werkzeug.Ordnen"/>).</summary>
    public static IReadOnlyList<StartEintrag> Ordnen(IEnumerable<StartEintrag> eintraege)
    {
        var liste = eintraege.ToList();
        return [.. Werkzeug.Ordnen(liste.Select(e => e.Kennung)).Select(k => liste.First(e => e.Kennung == k))];
    }
}
