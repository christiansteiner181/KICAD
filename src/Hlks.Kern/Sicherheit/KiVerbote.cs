// Befehle «nur Mensch» zentral im Kern (Entscheid Christian 13.11.2026, Frage 37: «Zentral im Kern: Liste «nur Mensch», für Herkunft «ki»
// abgelehnt (Palette, MCP, HTTP gleich)»). Vorher sperrten die Palette (GespraechDienst.Verbote) bzw. --werkzeuge des MCP-Servers; ein MCP-Server mit
// --art ki ohne --werkzeuge gab der KI alle Werkzeuge, auch Freigaben und HKAP. Projekt.Ausfuehren lehnt diese Befehle für die Art des Aufrufs «ki»
// ab, bevor ein Kontext entsteht. Abfragen, die nach aussen gehen (hkap_*), sperrt Tuer.Abfrage über IstGesperrt ebenso.

namespace Hlks.Kern;

public static class KiVerbote
{
    /// <summary>Befehle bzw. Abfragen, die nur ein Mensch ausführt, je mit Grund (Gruppen wie die Verbotsliste der Palette).</summary>
    public static readonly IReadOnlyDictionary<string, string> NurMensch = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["familie_freigeben"] = "Freigaben macht nur ein Mensch.",
        ["konflikt_entscheiden"] = "Konflikte einer Familie entscheidet nur ein Mensch.",
        ["wert_bestaetigen"] = "Einen Wert ohne Quelle anzunehmen ist eine Freigabe – nur ein Mensch.",
        ["familie_speichern"] = "Schreibt in die Bürobibliothek bzw. eine Datei – nur ein Mensch.",
        // familie_in_bibliothek nicht hier: KI-Aufträge schreiben Entwürfe in die Bibliothek; «als aktuell» lehnt der Befehl selbst für die KI ab
        ["familie_aktualisieren"] = "Übernimmt eine Bibliotheksfassung ins Projekt – nie automatisch.",
        ["mustersystem_speichern"] = "Schreibt in die projektübergreifende Systembibliothek – entscheidet ein Mensch.",
        ["familienauswahl_setzen"] = "Die Auswahl der Bibliotheksfamilien gilt für alle am Projekt – entscheidet ein Mensch.",
        ["aussparung_status"] = "Freigabe und Versand an den Bauingenieur macht der Mensch.",
        ["kollision_status"] = "Kollisionen akzeptieren oder erledigen ist ein Entscheid des Menschen.",
        ["bcf_import"] = "Übernimmt Statusentscheide anderer – entscheidet der Mensch.",
        ["bcf_export"] = "Ausgabe an Dritte – Freigabe durch den Menschen.",
        ["aussparungen_ifc"] = "Ausgabe an Architekt und Bauingenieur – Freigabe durch den Menschen.",
        ["ifc_export"] = "IFC-Ausgabe an Dritte – Freigabe durch den Menschen.",
        ["plan_pdf"] = "Pläne gibt der Mensch aus.",
        ["plansatz_pdf"] = "Planausgabe gibt der Mensch frei.",
        ["baustelle_export"] = "Ausgabe an die Baustelle trägt die Freigabe – gibt der Mensch aus.",
        ["montagestatus_setzen"] = "Montagestatus kommt von der Baustelle bzw. vom Menschen.",
        ["rueckmeldung_uebernehmen"] = "Rückmeldungen der Baustelle übernimmt der Mensch (Projektleiter).",
        ["revision_hinzufuegen"] = "Dokumentenlenkung gegenüber Dritten – nur der Mensch.",
        ["plansatz_revision_hinzufuegen"] = "Dokumentenlenkung gegenüber Dritten – nur der Mensch.",
        ["plansatz_setzen"] = "Das Ausgabeziel der Planausgabe richtet der Mensch ein.",
    };

    /// <summary>Gesperrt für die KI: ein Eintrag der Liste oder ein HKAP-Werkzeug (HKAP im Betrieb, alles hkap_* nur durch den Menschen).</summary>
    public static bool IstGesperrt(string name) => NurMensch.ContainsKey(name) || name.StartsWith("hkap_", StringComparison.Ordinal);

    public static string Grund(string name) => NurMensch.TryGetValue(name, out var g) ? g : "HKAP im Betrieb – nur der Mensch arbeitet mit HKAP.";

    /// <summary>Wirft für die Art «ki», wenn der Befehl bzw. die Abfrage nur für Menschen ist.</summary>
    public static void Pruefen(string name, string? art)
    {
        if (!string.Equals(art, "ki", StringComparison.OrdinalIgnoreCase) || !IstGesperrt(name)) return;
        throw new HlksFehler($"{name} ist der KI nicht erlaubt: {Grund(name)}", "Diesen Schritt macht ein Mensch in der Oberfläche.");
    }
}
