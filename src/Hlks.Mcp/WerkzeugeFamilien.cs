// MCP-Werkzeuge der Familien (Modul 2, B1): familie_entwurf, familie_aendern, familie_laden, familie_platzieren und die
// Abfragen familien, familie, anschlusslagen, familie_ansicht (Modul 3: anschlusslagen für alle Elemente, familie_platzieren
// mit ausrichtung). Dünne Hülle über dem Kern wie in Werkzeuge.cs; Name,
// Beschreibung und Parameter stehen genau so im Kern (FamilienBefehle.cs, FamilienAbfragen.Definitionen).

using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hlks.Kern;
using ModelContextProtocol.Server;

namespace Hlks.Mcp;

public sealed partial class Werkzeuge
{
    // ---------- Abfragen ----------

    [McpServerTool(Name = "familien"), Description(FamilienAbfragen.FamilienText)]
    public string FamilienLesen() => Lauf(() => Antwort(FamilienAbfragen.Liste(projekt)));

    [McpServerTool(Name = "familie"), Description(FamilienAbfragen.FamilieText)]
    public string FamilieLesen([Description("Familie (Kennzeichen, Name oder id)")] string familie) =>
        Lauf(() => Antwort(FamilienAbfragen.Familie(projekt, familie)));

    [McpServerTool(Name = "anschlusslagen"), Description(FamilienAbfragen.AnschlusslagenText)]
    public string Anschlusslagen([Description("Element mit Anschlüssen (Kennzeichen oder Id)")] string element) =>
        Lauf(() => Antwort(FamilienAbfragen.Anschlusslagen(projekt, element)));

    [McpServerTool(Name = "familie_ansicht"), Description(FamilienAbfragen.FamilieAnsichtText)]
    public string FamilieAnsicht(
        [Description("Familie (Kennzeichen, Name oder id)")] string familie,
        [Description("Typ aus der Typenliste")] string typ,
        [Description("vorne | hinten | links | rechts | oben | unten | 3d | grundriss")] string ansicht,
        [Description("grob | mittel | fein")] string detailgrad,
        [Description("Exemplarwerte { name: wert }")] JsonElement? parameter = null) =>
        Lauf(() => Antwort(FamilienAnsicht.Erzeugen(projekt, familie, typ, ansicht, detailgrad, Knoten(parameter) as JsonObject)));

    [McpServerTool(Name = "familien_bibliothek"), Description(FamilienBibliothek.Text)]
    public string FamilienBibliothekLesen(
        [Description("Schnellsuche in Name, id, Kategorie, Hersteller, Produkt (Teiltext, Gross/klein egal)")] string? filter = null,
        [Description("true = nur die nach der Familienauswahl des Projekts sichtbaren (Familien des Projekts immer)")] bool auswahl = false,
        [Description("Eine Familie im Detail (id oder Name in der Bibliothek, oder Kennzeichen im Projekt): Versionen, Abzweige, Log, Vorschau")] string? familie = null,
        [Description("Mit familie: Fassung für die Vorschau (Nummer oder Abzweig; Vorgabe die aktuelle Version)")] string? version = null) =>
        Lauf(() =>
        {
            var par = Parameter(("filter", filter), ("familie", familie), ("version", version));
            if (auswahl) par["auswahl"] = true;
            return Antwort(FamilienBibliothek.Liste(projekt, par));
        });

    // ---------- Befehle ----------

    [McpServerTool(Name = "familie_entwurf"), Description(FamilieEntwurf.Text)]
    public string FamilieEntwurfen(
        [Description("Die Familie als JSON-Objekt (format 1, name, kategorie, parameter, typen, koerper, anschluesse, sinnbild …)")] JsonElement definition,
        [Description("Bestehende Familie (Kennzeichen, Name oder id), deren Definition ersetzt wird; leer = neue Familie")] string? familie = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("familie_entwurf", Parameter(("definition", definition), ("familie", familie)), trockenlauf);

    [McpServerTool(Name = "familie_aendern"), Description(FamilieAendern.Text)]
    public string FamilieAendernWerkzeug(
        [Description("Familie (Kennzeichen, Name oder id)")] string familie,
        [Description("Änderungen in dieser Reihenfolge, z. B. [{ \"op\": \"typ_setzen\", \"typ\": { \"name\": \"22-600-1200\", \"werte\": { \"laenge\": 1200 } } }]")] JsonElement ops,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("familie_aendern", Parameter(("familie", familie), ("ops", ops)), trockenlauf);

    [McpServerTool(Name = FamilienQuellenNachfuehren.BefehlName), Description(FamilienQuellenNachfuehren.Text)]   // Nachtrag 21.10.2026
    public string FamilienQuellenNachfuehrenWerkzeug(
        [Description("Familien (Kennzeichen, Name oder id); leer = alle Familien des Projekts")] JsonElement? familien = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl(FamilienQuellenNachfuehren.BefehlName, Parameter(("familien", familien)), trockenlauf);

    [McpServerTool(Name = "familie_netz_uebernehmen"), Description(FamilieNetzUebernehmen.Text)]   // 30.10.2026: Geometrie aus IFC bzw. Herstellermodell
    public string FamilieNetzUebernehmenWerkzeug(
        [Description("IFC: GlobalId des Produkts oder Kennzeichen eines übernommenen Elements; STL/OBJ/STEP: Id des Befunds form3d (z. B. «3fa2…#1» oder «1»)")] string objekt,
        [Description("Unterlage (Kennzeichen, Id oder Dateiname); bei einem übernommenen Element die Unterlage seiner Übernahme")] string? unterlage = null,
        [Description("Bestehende Familie (Kennzeichen, Name oder id), in die der Körper kommt; leer = neue Familie")] string? familie = null,
        [Description("Name der neuen Familie")] string? name = null,
        [Description("KICAD-Klasse der neuen Familie, z. B. Sanitaerapparat")] string? kategorie = null,
        [Description("id des Körpers (Vorgabe «netz»); ein Körper gleicher id wird ersetzt")] string? koerper = null,
        [Description("geraet | apparat | armatur | rohr | sonst (Vorgabe apparat)")] string? material = null,
        [Description("Drehung des Objekts im Projekt um z in Grad; wird zurückgedreht. Leer: IFC automatisch, sonst 0")] double? drehung = null,
        [Description("unten_mitte | unten_hinten_mitte (Wandobjekte: Rückseite +y) | unveraendert; Vorgabe IFC unten_mitte, sonst unveraendert")] string? nullpunkt = null,
        [Description("form3d: das vereinfachte Netz statt des vollen nehmen")] bool vereinfacht = false,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("familie_netz_uebernehmen", Parameter(("objekt", objekt), ("unterlage", unterlage), ("familie", familie), ("name", name), ("kategorie", kategorie),
            ("koerper", koerper), ("material", material), ("drehung", drehung), ("nullpunkt", nullpunkt), ("vereinfacht", vereinfacht ? true : null)), trockenlauf);

    [McpServerTool(Name = "familie_laden"), Description(FamilieLaden.Text)]
    public string FamilieLadenWerkzeug(
        [Description("Statt id: Dateiname in kataloge/familien (mit oder ohne .familie.json), Ordner bzw. Datei in der Bibliothek oder voller Pfad")] string? datei = null,
        [Description("Familie der Bibliothek: Familien-Id oder Name (Abfrage familien_bibliothek)")] string? id = null,
        [Description("Mit id: Version (Nummer, z. B. 3) oder Abzweig; leer = aktuelle Version")] string? version = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("familie_laden", Parameter(("id", id), ("datei", datei), ("version", version)), trockenlauf);

    [McpServerTool(Name = "familie_platzieren"), Description(FamiliePlatzieren.Text)]
    public string FamiliePlatzierenWerkzeug(
        [Description("Familie (Kennzeichen, Name oder id)")] string familie,
        [Description("Typ aus der Typenliste der Familie")] string typ,
        [Description("Einfügepunkt [x, y] oder [x, y, z] in mm")] JsonElement punkt,
        [Description("Drehung um z in Grad, Gegenuhrzeigersinn (0 = Vorderseite nach Süden, −y)")] double? drehung = null,
        [Description("Volle Ausrichtung { x: [dx, dy, dz], z: [dx, dy, dz] }: lokale x- und z-Achse der Familie in Weltkoordinaten, rechtwinklig; geht drehung vor (z. B. Steigbogen x = [1, 0, 0], z = [0, -1, 0])")] JsonElement? ausrichtung = null,
        [Description("Exemplarwerte { name: wert } (nur Exemplarparameter ohne Formel)")] JsonElement? parameter = null,
        [Description("Kennzeichen; fehlt es, wird es aus dem Klassenkürzel gebildet")] string? kennzeichen = null,
        [Description("Geschoss (Kennzeichen oder Id)")] string? geschoss = null,
        [Description("Raum (Kennzeichen oder Id)")] string? raum = null,
        [Description("System (Kennzeichen oder Id)")] string? system = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("familie_platzieren", Parameter(("familie", familie), ("typ", typ), ("punkt", punkt), ("drehung", drehung), ("ausrichtung", ausrichtung), ("parameter", parameter),
            ("kennzeichen", kennzeichen), ("geschoss", geschoss), ("raum", raum), ("system", system)), trockenlauf);
}
