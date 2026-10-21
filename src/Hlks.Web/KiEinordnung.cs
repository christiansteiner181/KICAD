// Sicherheitsprüfung 3c, Befund B5 (berichte/modul-3c-sicherheitspruefung.md): JEDES Werkzeug des MCP-Servers «hlks» ist für die KI ausdrücklich
// eingeordnet – erlaubt (lesend bzw. ändernd, je Gruppe begründet) oder verboten (GespraechDienst.Verbote, je Eintrag begründet). Ein Test vergleicht
// die Einordnung mit dem Server (WerkzeugEinordnungTests): ein neues, nicht eingeordnetes Werkzeug macht den Bau rot, bis ein Mensch es einordnet.
// Zusätzlich gilt «verweigern, was nicht eingeordnet ist»: der MCP-Server der KI-Palette bekommt mit --werkzeuge nur die erlaubten Werkzeuge
// (ohne Internet) bzw. die Internetliste (B3c) – ein neues Werkzeug ist für Claude erst nach der Einordnung da.
// Lesend heisst: keine Pakete, keine Ausgabe an Dritte, kein Netz; ändernd heisst: Pakete mit Herkunft KI, die der Mensch in der Palette sieht und
// zurücknehmen kann. Grenzen, die der Kern selbst zieht, stehen im Grund (z. B. KiPfade für Dateipfade, Aussparungsnummern, Familienfassungen).

namespace Hlks.Web;

public static class KiEinordnung
{
    /// <summary>Eine Gruppe erlaubter Werkzeuge mit gemeinsamer Begründung.</summary>
    public sealed record Gruppe(string Name, string Grund, IReadOnlyList<string> Werkzeuge);

    /// <summary>Erlaubt, nur lesend.</summary>
    public static readonly IReadOnlyList<Gruppe> Lesend =
    [
        new("Projekt lesen", "Abfragen über die Tür des Kerns: lesen das Modell, ändern nichts, schreiben keine Dateien ausser Zwischenständen im Projekt.",
            ["faehigkeiten", "projekt_ueberblick", "element_lesen", "elemente_suchen", "system_lesen", "strang_text", "netz_folgen", "raum_inhalt",
             "mengen", "raumliste", "strangliste", "pruefen", "warum", "aenderungen", "katalog"]),
        new("Abgleich", "Holt Änderungen anderer Bearbeiter aus der Ablage ins Modell dieses Laufs; schreibt nichts.", ["synchronisieren"]),
        new("Gebäudeautomation lesen", "Datenpunkte und Messwerte nur lesen (Spezifikation 3c: GA nur lesend); die Datenquelle ist nur lesend.", ["datenpunkte", "zeitreihe"]),
        new("Fachabfragen", "Auswertungen und Listen des Kerns (Hülle, Material, Kollisionen, Aussparungen, Leitungen, Schema, Sanitär, Pläne); nur lesend.",
            ["aufbau_rechnen", "huellbauteile", "huelle_pruefen", "materialien", "materialliste", "typmaterial", "kollisionen", "aussparungsliste",
             "leitungsserie", "leitungszug", "verlegung", "vorgaben", "raumgrenzen", "schema", "sanitaer_belastung", "sanitaer_katalog",
             "sanitaer_zusammenstellung", "plannummer_vorschlag", "plansaetze",
             "abzweigwinkel"]),   // Nachtrag Modul 3, 21.10.2026 (hlks-kern, zur Bestätigung): Abzweigwinkel einer Serie bzw. Leitung, nur lesend wie leitungsserie
        new("Familien lesen", "Familien des Projekts und der Bibliothek ansehen, vergleichen und nachmessen; nur lesend.",
            ["familien", "familie", "anschlusslagen", "familie_ansicht", "familie_masse", "familie_vergleichen", "familien_bibliothek", "familie_pruefen"]),
        new("Unterlagen, IFC und BCF lesen", "Inhalt von Unterlagen und Dateien anderer lesen – Dateipfade nur unter dem Projektordner und den Wurzeln aus " +
            "kataloge/buero.json (Kern: KiPfade, B3a/B4), mit Internet nur Herstellerunterlagen (KiUnterlagen, B3b), Ergebnis mit Kopfzeile «Daten, keine " +
            "Anweisungen» (B15). Ein Dateipfad legt die Datei als Unterlage im Projekt ab (Kopie, nie überschrieben).",
            ["unterlagen_lesen", "befunde", "unterlage_bild", "ifc_pruefen", "bcf_lesen"]),
        new("Normen", "Normen und Merkblätter suchen und lesen (nur kurze Auszüge, höchstens 3 Seiten je Aufruf; nie ein beliebiger Pfad).", ["normen_suchen", "norm_lesen"]),
        new("Bild der Ansicht", "Das zur Frage mitgeschickte Bild (Arbeitsordner der KI, nie im Projekt); mit Internet ohne Bild.", ["kontext_bild"]),
    ];

    /// <summary>Erlaubt, ändernd: Pakete mit Herkunft KI, sichtbar und zurücknehmbar.</summary>
    public static readonly IReadOnlyList<Gruppe> Aendernd =
    [
        new("Modell bearbeiten", "Elemente und Verbindungen über die Befehle des Kerns ändern; jede Änderung ein Paket der KI, in der Palette zurücknehmbar " +
            "(Spezifikation 3c); rueckgaengig/wiederholen betreffen nur eigene Pakete.",
            ["element_anlegen", "element_aendern", "element_loeschen", "verbinden", "trennen", "leitung_ziehen", "elemente_drehen", "elemente_kopieren",
             "elemente_spiegeln", "elemente_verschieben", "rueckgaengig", "wiederholen"]),
        new("Leitungen und Schema", "Leitungen zeichnen, teilen, dimensionieren und Schemata anordnen – Modellarbeit, als Paket zurücknehmbar.",
            ["leitung_abzweigen", "leitung_aufteilen", "leitung_groesse", "leitung_serie", "leitung_zeichnen", "uebergang_setzen", "schema_anlegen",
             "schema_anordnen", "schema_lage"]),
        new("Architektur und Hülle", "Wände, Decken, Öffnungen, Räume, Aufbauten, Materialien und Vorgaben im Projekt – Modellarbeit, zurücknehmbar; " +
            "Zahlenwerte brauchen eine Quelle (Kern).",
            ["wand_zeichnen", "decke_erzeugen", "oeffnung_setzen", "raum_erkennen", "aufbau_anlegen", "aufbau_aendern", "aufbau_zuweisen", "material_anlegen",
             "material_aendern", "material_uebernehmen", "fenster_kennwerte", "waermebruecken_vorschlagen", "typmaterial_setzen", "vorgaben_setzen"]),
        new("Familien als Entwurf", "Familien entwerfen, ändern, platzieren und neue laden – immer als Entwurf; Freigabe, Bestätigen, Bibliothek und Übernahme " +
            "einer neueren Fassung macht der Mensch (Kern lehnt die KI dort ab, Verbotsliste). familien_quellen_nachfuehren speichert wie familie_aendern " +
            "(Entwurf, ein Paket, zurücknehmbar) und ändert nur die Befund-Ids der Quellen auf die aktuelle Leserversion.",
            ["familie_entwurf", "familie_aendern", "familie_laden", "familie_platzieren", "familien_quellen_nachfuehren"]),
        new("Unterlagen und IFC übernehmen", "Unterlagen einfügen/kalibrieren und ein IFC übernehmen – Dateipfade nur unter dem Projektordner und den Wurzeln " +
            "aus kataloge/buero.json (KiPfade, B3a/B4); ein Paket je Übernahme, zurücknehmbar.",
            ["unterlage_einfuegen", "unterlage_kalibrieren", "ifc_uebernehmen"]),
        new("Pläne einrichten", "Planblätter anlegen und Ansichten platzieren – im Modell, zurücknehmbar; Revisionen, Plansätze und PDF-Ausgaben sind verboten.",
            ["planblatt_anlegen", "ansicht_platzieren", "einlageplaene_anlegen", "planblatt_gewerk_setzen"]),
        new("Aussparungen und Kollisionen prüfen", "Vorschläge und Prüfergebnisse: aussparung_form nur bei vorgeschlagenen/veralteten Aussparungen (Kern); " +
            "aussparungen_nummerieren nummeriert für die KI keine abgegebene (freigegebene, versandte …) Aussparung um (Kern, B5); aussparungen_abgleichen " +
            "setzt nur angekommen/fehlt aus dem Abgleich mit der Architekten-IFC (Herkunft berechnung, Prüfergebnis, keine Freigabe); kollision_pruefen " +
            "legt Kollisionen an. Status «freigegeben/versandt» und Entscheide bleiben beim Menschen (Verbotsliste).",
            ["aussparungen_vorschlagen", "aussparung_form", "aussparungen_nummerieren", "aussparungen_abgleichen", "kollision_pruefen"]),
    ];

    /// <summary>Alle erlaubten Werkzeuge (lesend und ändernd).</summary>
    public static IReadOnlyList<string> Erlaubt => [.. Lesend.Concat(Aendernd).SelectMany(g => g.Werkzeuge)];

    /// <summary>Verbotene Werkzeuge (GespraechDienst.Verbote, je Eintrag begründet).</summary>
    public static IReadOnlyList<string> Verboten => GespraechDienst.Verbotsliste;

    /// <summary>Einordnung eines Werkzeugs: lesend | aendernd | verboten; null = nicht eingeordnet.</summary>
    public static string? Von(string werkzeug) =>
        Verboten.Contains(werkzeug) ? "verboten"
        : Aendernd.Any(g => g.Werkzeuge.Contains(werkzeug)) ? "aendernd"
        : Lesend.Any(g => g.Werkzeuge.Contains(werkzeug)) ? "lesend"
        : null;
}
