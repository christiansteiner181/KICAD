// Herstellerunterlagen lesen (Modul 2, entwurf/modul-2.md): Einhängepunkt für Befehle und Abfragen. Angelegt vom
// Hauptlauf (Phase A), damit die Bauaufträge die zentralen Dateien (Befehle.cs, Abfragen.cs, Tuer.cs) nicht gleichzeitig
// ändern müssen. Die Leser selbst findet UnterlagenLeser über Reflexion (UnterlagenBefund.cs); die Abfragen stehen in
// UnterlagenAbfrage.cs, Ablage und Auflösen in UnterlagenAblage.cs (Auftrag B2).

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class UnterlagenBefehle
{
    /// <summary>Registriert die Befehle rund um Herstellerunterlagen (Lesen ist eine Abfrage; unterlage_einfuegen steht in GrundlagenBefehle).</summary>
    public static void Registrieren(Befehlsregister r)
    {
        r.Hinzufuegen(new UnterlageAusUrl());   // Internet-Recherche
    }
}

public static class UnterlagenAbfragen
{
    public const string LesenText = "Liest Herstellerunterlagen (Datenblatt-PDF, DWG/DXF-Massbild, XLSX/CSV/Revit-Typkatalog, STL/OBJ, Hersteller-IFC, PNG/JPEG; STEP/IGES mit dem STEP-Leser) und übersetzt sie in Befunde: " +
        "kleine Aussagen mit genauer Stelle (Seite, Lage in pt bzw. Zeichnungseinheiten, Box, Sicherheit). Antwort je Datei: Art, Seiten, Anzahl Befunde je Art, Tabellen mit Kopf und Zeilenzahl, Hinweise – nie alle Wörter. " +
        "Gelesen wird einmal je Datei und Leserversion (Ablage unterlagen/befunde); wiederholtes Lesen kommt aus der Ablage. Einzelne Befunde: befunde; Seitenbild: unterlage_bild.";
    public const string BefundeText = "Befunde einer Unterlage, gefiltert und seitenweise: je Befund Id («<Prüfsumme>#<n>»), Art (wort, zeile, tabelle, zelle, linie, polylinie, bogen, kreis, mass, block, text, bild, form3d, port, pset, typ …), Wert, Stelle und Sicherheit. " +
        "Filter: seite (PDF), art, text (Teil des Werts), box [x0, y0, x1, y1] (Unterlagen-Koordinaten), layout, layer, id. Antworten sind auf rund 60 000 Zeichen begrenzt; das Netz eines 3D-Befunds nur mit voll = true.";
    public const string BildText = "Seite bzw. Ausschnitt einer Unterlage als PNG (für die KI als Bild): PDF-Seite, Bild, DWG/DXF gezeichnet. ausschnitt [x0, y0, x1, y1] in Unterlagen-Koordinaten (PDF pt, y nach oben – die Box eines Befunds), breite in Bildpunkten (Vorgabe 1000). " +
        "Über REST als Base64 im Feld base64, über MCP als Bildinhalt. Die Antwort nennt den gezeigten Bereich, damit sich Bildpunkte in Unterlagen-Koordinaten umrechnen lassen.";

    /// <summary>Abfragen unterlagen_lesen, unterlage_bild, befunde für Abfragen.Liste.</summary>
    public static readonly IReadOnlyList<AbfrageDef> Definitionen =
    [
        new("unterlagen_lesen", LesenText,
            [new("unterlagen", "liste", true, "Unterlagen: Liste aus Kennzeichen (UL-n), Id oder Dateiname im Ordner unterlagen (auch ein Dateipfad: die Datei wird dann abgelegt)")]),
        new("befunde", BefundeText,
        [
            new("unterlage", "text", true, "Unterlage (Kennzeichen, Id oder Dateiname)"),
            new("seite", "zahl", false, "Nur Befunde dieser Dokumentseite (PDF, ab 1)"),
            new("art", "text", false, "Befundart, mehrere mit Komma: wort, zeile, tabelle, zelle, linie, polylinie, bogen, kreis, mass, block, text, bild, form3d, port, pset, typ"),
            new("text", "text", false, "Nur Befunde, deren Wert diesen Text enthält (Gross-/Kleinschreibung egal)"),
            new("box", "liste", false, "Nur Befunde, deren Box dieses Rechteck [x0, y0, x1, y1] berührt (Unterlagen-Koordinaten)"),
            new("layout", "text", false, "DWG/DXF: Layout (Model oder Name des Layouts); XLSX: Blatt"),
            new("layer", "text", false, "DWG/DXF: Layer"),
            new("id", "text", false, "Befund-Id oder Nummer, mehrere mit Komma (z. B. «a1b2c3d4e5f6#17» oder «17, 18»)"),
            new("voll", "bool", false, "3D-Befunde mit Netz (Ecken und Dreiecke); Vorgabe false: nur Hülle und Anzahl"),
            new("proSeite", "zahl", false, "Befunde je Antwort (Vorgabe 50, höchstens 500)"),
            new("trefferSeite", "zahl", false, "Seite der Trefferliste (ab 1, Vorgabe 1)"),
        ]),
        new("unterlage_bild", BildText,
        [
            new("unterlage", "text", true, "Unterlage (Kennzeichen, Id oder Dateiname)"),
            new("seite", "zahl", false, "PDF: Seite (ab 1, Vorgabe 1)"),
            new("ausschnitt", "liste", false, "Ausschnitt [x0, y0, x1, y1] in Unterlagen-Koordinaten (PDF: pt, y nach oben; Bild: Bildpunkte; DWG/DXF: Zeichnungseinheiten); Vorgabe: ganze Seite bzw. Zeichnung"),
            new("breite", "zahl", false, "Breite des Bildes in Bildpunkten (Vorgabe 1000, 100–3000)"),
        ]),
    ];

    /// <summary>Beantwortet eine Unterlagen-Abfrage; null = nicht zuständig.</summary>
    public static object? Tuer(Projekt p, string name, JsonObject par) => name switch
    {
        "unterlagen_lesen" => UnterlagenAbfrage.Lesen(p, par["unterlagen"]),
        "befunde" => UnterlagenAbfrage.Befunde(p, par),
        "unterlage_bild" => UnterlagenAbfrage.Bild(p, par),
        _ => null,
    };
}
