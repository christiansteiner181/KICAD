// MCP-Werkzeuge der Gebäudehülle (Modul 1c, B2): je ein Befehl oder eine Abfrage aus Huelle/*.cs. Name, Beschreibung und
// Parameter wörtlich wie IBefehl.Beschreibung bzw. ParameterDef (WerkzeugTests prüfen das). Keine eigene Fachlogik.

using System.ComponentModel;
using System.Text.Json;
using Hlks.Kern;
using ModelContextProtocol.Server;

namespace Hlks.Mcp;

public sealed partial class Werkzeuge
{
    // ---------- Abfragen ----------

    [McpServerTool(Name = "aufbau_rechnen"), Description(HuelleAbfragen.AufbauRechnenText)]
    public string AufbauRechnen([Description("Aufbau (Kennzeichen, Id oder Name)")] string aufbau) =>
        Lauf(() => Antwort(HuelleAbfragen.AufbauRechnen(projekt, aufbau)));

    [McpServerTool(Name = "materialien"), Description(HuelleAbfragen.MaterialienText)]
    public string Materialien() => Lauf(() => Antwort(HuelleAbfragen.Materialien(projekt)));

    [McpServerTool(Name = "huelle_pruefen"), Description(HuelleAbfragen.HuellePruefenText)]
    public string HuellePruefen() => Lauf(() => Antwort(HuelleAbfragen.HuellePruefen(projekt)));

    // ---------- Materialien ----------

    [McpServerTool(Name = "material_anlegen"), Description("Legt ein Material der Gebäudehülle an (Kennzeichen MAT-n). Jeder Kennwert (lambda, rohdichte, waermekapazitaet, emission, absorption, mu) braucht eine Quelle (Norm mit Ausgabe und Tabelle, Datenblatt, IFC, Angabe von Christian); ohne Quelle wird abgelehnt. Der Kern setzt keine Werte aus dem Gedächtnis.")]
    public string MaterialAnlegen(
        [Description("Bezeichnung des Materials (im Projekt eindeutig)")] string name,
        [Description("Gruppe, z. B. Beton, Mauerwerk, Dämmstoff, Holz, Putz")] string? kategorie = null,
        [Description("Kennwerte { lambda | rohdichte | waermekapazitaet | emission | absorption | mu: { wert, quelle } }; jeder Wert braucht eine Quelle")] JsonElement? werte = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("material_anlegen", Parameter(("name", name), ("kategorie", kategorie), ("werte", werte)), trockenlauf);

    [McpServerTool(Name = "material_aendern"), Description("Ändert ein Material: Name, Kategorie und Kennwerte. Jeder Kennwert braucht eine Quelle; null entfernt einen Kennwert. Aufbauten, die das Material verwenden, werden im selben Paket neu gerechnet.")]
    public string MaterialAendern(
        [Description("Material (Kennzeichen, Id oder Name)")] string material,
        [Description("Neue Bezeichnung")] string? name = null,
        [Description("Neue Gruppe")] string? kategorie = null,
        [Description("Kennwerte { lambda | rohdichte | waermekapazitaet | emission | absorption | mu: { wert, quelle } }; jeder Wert braucht eine Quelle; null entfernt den Kennwert")] JsonElement? werte = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("material_aendern", Parameter(("material", material), ("name", name), ("kategorie", kategorie), ("werte", werte)), trockenlauf);

    [McpServerTool(Name = "material_uebernehmen"), Description("Übernimmt ein Material aus der Bürobibliothek (kataloge/materialien.json) samt Kennwerten und Quellen ins Projekt. Die Bibliothek ist nur Vorlage; im Projekt kann das Material danach geändert werden.")]
    public string MaterialUebernehmen(
        [Description("Name des Eintrags in der Bürobibliothek")] string name,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("material_uebernehmen", Parameter(("name", name)), trockenlauf);

    // ---------- Aufbauten ----------

    [McpServerTool(Name = "aufbau_anlegen"), Description("Legt einen Schichtaufbau an (Kennzeichen AUF-n): Art (wand, decke, dach, boden, tuer) und Schichten von innen nach aussen mit Material und Dicke. R und U rechnet der Kern selbst (R = Summe d/λ, U = 1/(Rsi + R + Rse)), sobald λ, Rsi und Rse mit Quelle da sind; sonst nennt er, was fehlt. Ein ausdrücklicher U-Wert braucht eine Quelle und geht vor.")]
    public string AufbauAnlegen(
        [Description("Bezeichnung des Aufbaus (im Projekt eindeutig)")] string name,
        [Description("wand | decke | dach | boden | tuer")] string art,
        [Description("Schichten von innen nach aussen: [{ material (Kennzeichen, Id oder Name), dicke (mm) }]")] JsonElement schichten,
        [Description("horizontal | aufwaerts | abwaerts (wählt Rsi/Rse); ohne Angabe aus der Art: Wand und Tür horizontal, Decke und Dach aufwaerts, Boden abwaerts")] string? waermestrom = null,
        [Description("Ausdrücklicher U-Wert W/(m²·K) aus Datenblatt oder IFC: { wert, quelle }; geht vor der Rechnung")] JsonElement? uWert = null,
        [Description("Quelle der Schichtfolge, z. B. Plan oder Bauteilkatalog des Architekten")] string? quelle = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("aufbau_anlegen", Parameter(("name", name), ("art", art), ("schichten", schichten), ("waermestrom", waermestrom), ("uWert", uWert), ("quelle", quelle)), trockenlauf);

    [McpServerTool(Name = "aufbau_aendern"), Description("Ändert einen Aufbau: Name, Art, Schichten, Wärmestrom, ausdrücklicher U-Wert. R und U werden im selben Paket neu gerechnet. Einen ausdrücklichen U-Wert entfernt element_aendern (eigenschaften: { uWert: null }); dann rechnet der Kern wieder.")]
    public string AufbauAendern(
        [Description("Aufbau (Kennzeichen, Id oder Name)")] string aufbau,
        [Description("Neue Bezeichnung")] string? name = null,
        [Description("wand | decke | dach | boden | tuer; die Art muss zu den Bauteilen passen, die den Aufbau verwenden")] string? art = null,
        [Description("Neue Schichtfolge (ersetzt die alte): Schichten von innen nach aussen: [{ material (Kennzeichen, Id oder Name), dicke (mm) }]")] JsonElement? schichten = null,
        [Description("horizontal | aufwaerts | abwaerts; auto = aus der Art")] string? waermestrom = null,
        [Description("Ausdrücklicher U-Wert W/(m²·K) aus Datenblatt oder IFC: { wert, quelle }; geht vor der Rechnung")] JsonElement? uWert = null,
        [Description("Quelle der neuen Schichtfolge")] string? quelle = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("aufbau_aendern", Parameter(("aufbau", aufbau), ("name", name), ("art", art), ("schichten", schichten), ("waermestrom", waermestrom), ("uWert", uWert), ("quelle", quelle)), trockenlauf);

    [McpServerTool(Name = "aufbau_zuweisen"), Description("Weist Bauteilen (Wand, Decke, Dach, Tür) einen Aufbau zu (bezuege.aufbau). Die Art des Aufbaus muss zur Bauteilklasse passen: Wand ↔ wand, Decke ↔ decke oder boden, Dach ↔ dach, Tür ↔ tuer. Alles oder nichts: ein unpassendes Bauteil lehnt den ganzen Befehl ab.")]
    public string AufbauZuweisen(
        [Description("Bauteile (Kennzeichen oder Ids), z. B. [\"W-1\", \"W-2\"]")] JsonElement elemente,
        [Description("Aufbau (Kennzeichen, Id oder Name)")] string aufbau,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("aufbau_zuweisen", Parameter(("elemente", elemente), ("aufbau", aufbau)), trockenlauf);

    [McpServerTool(Name = "uebergang_setzen"), Description("Setzt die Wärmeübergangswiderstände Rsi und Rse (m²·K/W) der Projektinformation, getrennt nach Wärmestromrichtung (horizontal, aufwaerts, abwaerts) und gegen Erdreich. Die Quelle ist Pflicht (Norm mit Ausgabe und Tabelle); der Kern kennt keine Vorgabewerte. Ohne Rsi/Rse rechnet er nur R, keinen U-Wert. Gruppen, die nicht genannt sind, bleiben unverändert.")]
    public string UebergangSetzen(
        [Description("Quelle der Werte, z. B. Norm mit Ausgabe, Tabelle und Seite")] string quelle,
        [Description("Wärmestrom horizontal (Wände, Türen): { rsi, rse }")] JsonElement? horizontal = null,
        [Description("Wärmestrom aufwärts (Decken, Dächer): { rsi, rse }")] JsonElement? aufwaerts = null,
        [Description("Wärmestrom abwärts (Böden): { rsi, rse }")] JsonElement? abwaerts = null,
        [Description("Bauteile gegen Erdreich: { rsi, rse }")] JsonElement? erdreich = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("uebergang_setzen", Parameter(("quelle", quelle), ("horizontal", horizontal), ("aufwaerts", aufwaerts), ("abwaerts", abwaerts), ("erdreich", erdreich)), trockenlauf);

    // ---------- Fenster und Türen ----------

    [McpServerTool(Name = "fenster_kennwerte"), Description("Setzt Kennwerte von Fenstern und Türen: ug, uf (W/(m²·K)), psiG (W/(m·K)), g, rahmenanteil, fc, sonnenschutz, uw (Fenster) und ud (Tür). Jeder Zahlenwert braucht die gemeinsame Quelle (Datenblatt, Norm mit Tabelle, IFC, Angabe von Christian). Uw rechnet der Kern aus Ug, Uf, ψg, Rahmenanteil und Mass (Annahme: Verglasung dem Fenster ähnlich); ein ausdrückliches uw mit Quelle geht vor.")]
    public string FensterKennwerte(
        [Description("Fenster und Türen (Kennzeichen oder Ids), z. B. [\"F-1\", \"T-1\"]")] JsonElement elemente,
        [Description("U-Wert der Verglasung W/(m²·K) (Fenster)")] double? ug = null,
        [Description("U-Wert des Rahmens W/(m²·K) (Fenster)")] double? uf = null,
        [Description("Längenbezogener Wärmedurchgang des Glasrandverbunds W/(m·K) (Fenster)")] double? psiG = null,
        [Description("Gesamtenergiedurchlassgrad der Verglasung, 0 … 1 (Fenster)")] double? g = null,
        [Description("Anteil der Rahmenfläche an der Fensterfläche, 0 … 1 (Fenster)")] double? rahmenanteil = null,
        [Description("Lage des Sonnenschutzes: keiner | aussen | innen | zwischen (Fenster; braucht keine Quelle)")] string? sonnenschutz = null,
        [Description("Abminderungsfaktor des Sonnenschutzes, 0 … 1 (Fenster)")] double? fc = null,
        [Description("Ausdrücklicher U-Wert des Fensters W/(m²·K); geht vor der Rechnung (Fenster)")] double? uw = null,
        [Description("U-Wert der Tür W/(m²·K) (Tür)")] double? ud = null,
        [Description("Quelle der Zahlenwerte (Pflicht, sobald ein Zahlenwert angegeben ist)")] string? quelle = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("fenster_kennwerte", Parameter(("elemente", elemente), ("ug", ug), ("uf", uf), ("psiG", psiG), ("g", g), ("rahmenanteil", rahmenanteil),
            ("sonnenschutz", sonnenschutz), ("fc", fc), ("uw", uw), ("ud", ud), ("quelle", quelle)), trockenlauf);
}
