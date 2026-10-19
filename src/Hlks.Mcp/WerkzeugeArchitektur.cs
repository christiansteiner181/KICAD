// MCP-Werkzeuge der Architektur (Modul 1a): je ein Befehl aus ArchitekturBefehle. Name = Befehlsname, Beschreibung und
// Parameter wörtlich wie IBefehl.Beschreibung bzw. ParameterDef (WerkzeugTests prüfen das). Keine eigene Fachlogik.

using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace Hlks.Mcp;

public sealed partial class Werkzeuge
{
    [McpServerTool(Name = "wand_zeichnen"), Description("Zeichnet Wände entlang von Punkten in einem Geschoss, eine Wand je gerades Stück (Achse = Wandmitte, z = Unterkante). bezugslinie links/rechts legt die Punkte auf eine Wandfläche, geschlossen verbindet den letzten mit dem ersten Punkt. Die Dicke ist Pflicht (keine Vorgabe); Ecken und Anschlüsse bereinigt der Kern.")]
    public string WandZeichnen(
        [Description("Geschoss (Kennzeichen oder Id)")] string geschoss,
        [Description("Wandzug [[x,y], …] oder [[x,y,z], …] in mm, mindestens zwei Punkte; ohne z gilt die Geschosskote")] JsonElement punkte,
        [Description("Wanddicke in mm (der Kern setzt keine Vorgabe)")] double dicke,
        [Description("Wandhöhe in mm; Vorgabe Geschosshöhe")] double? hoehe = null,
        [Description("Lage der Punkte zur Wand: mitte (Vorgabe), links oder rechts = Wandfläche links/rechts in Zeichenrichtung")] string? bezugslinie = null,
        [Description("Letzten mit erstem Punkt verbinden (geschlossener Wandzug)")] bool geschlossen = false,
        [Description("Material für die Schraffur: unbestimmt, beton, backstein, kalksandstein, porenbeton, holz, leichtbau")] string? material = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false)
    {
        var par = Parameter(("geschoss", geschoss), ("punkte", punkte), ("dicke", dicke), ("hoehe", hoehe), ("bezugslinie", bezugslinie), ("material", material));
        if (geschlossen) par["geschlossen"] = true;
        return Befehl("wand_zeichnen", par, trockenlauf);
    }

    [McpServerTool(Name = "oeffnung_setzen"), Description("Setzt eine Tür oder ein Fenster in eine Wand – über abstand (Mitte ab Wandanfang) oder einen Punkt, der auf die Wandachse projiziert wird. Prüft, dass die Öffnung in die Wand passt und keine andere überschneidet. Danach folgt die Öffnung ihrer Wand.")]
    public string OeffnungSetzen(
        [Description("Tuer oder Fenster")] string klasse,
        [Description("Wand (Kennzeichen oder Id)")] string wand,
        [Description("Lichte Breite in mm")] double breite,
        [Description("Lichte Höhe in mm")] double hoehe,
        [Description("Mitte der Öffnung ab Wandanfang entlang der Achse in mm")] double? abstand = null,
        [Description("Statt abstand: Punkt [x, y] in mm, wird auf die Wandachse projiziert")] JsonElement? punkt = null,
        [Description("Fenster: Brüstungshöhe über OK Boden in mm (bei Fenstern Pflicht)")] double? bruestung = null,
        [Description("Tür: Aufschlagseite links oder rechts in Wandrichtung Anfang → Ende (Vorgabe links)")] string? seite = null,
        [Description("Tür: Bandseite anfang oder ende = zum Wandanfang bzw. -ende (Vorgabe anfang)")] string? band = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("oeffnung_setzen", Parameter(("klasse", klasse), ("wand", wand), ("breite", breite), ("hoehe", hoehe), ("abstand", abstand),
            ("punkt", punkt), ("bruestung", bruestung), ("seite", seite), ("band", band)), trockenlauf);

    [McpServerTool(Name = "raum_erkennen"), Description("Legt einen Raum an, dessen Polygon aus den Wandflächen um einen Punkt entsteht (wie Revit «Raum»); Türen und Fenster unterbrechen die Begrenzung nicht. Fehler, wenn die Wände den Punkt nicht umschliessen.")]
    public string RaumErkennen(
        [Description("Geschoss (Kennzeichen oder Id)")] string geschoss,
        [Description("Punkt [x, y] in mm im Raum")] JsonElement punkt,
        [Description("Raumnummer, z. B. 1.02")] string? nummer = null,
        [Description("Raumnutzung, z. B. Bad, Küche, Wohnen")] string? nutzung = null,
        [Description("Bezeichnung")] string? name = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("raum_erkennen", Parameter(("geschoss", geschoss), ("punkt", punkt), ("nummer", nummer), ("nutzung", nutzung), ("name", name)), trockenlauf);

    [McpServerTool(Name = "elemente_verschieben"), Description("Verschiebt Elemente mit Geometrie (Architektur, HLKS, Beschriftung) um dx, dy, dz. Türen und Fenster folgen ihrer Wand; eine Öffnung allein wandert entlang ihrer Wand.")]
    public string ElementeVerschieben(
        [Description("Kennzeichen oder Ids, z. B. [\"W-1\", \"T-1\"]")] JsonElement elemente,
        [Description("Verschiebung in x (Ost) in mm")] double? dx = null,
        [Description("Verschiebung in y (Nord) in mm")] double? dy = null,
        [Description("Verschiebung in z (Höhe) in mm")] double? dz = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("elemente_verschieben", Parameter(("elemente", elemente), ("dx", dx), ("dy", dy), ("dz", dz)), trockenlauf);

    [McpServerTool(Name = "elemente_kopieren"), Description("Kopiert Elemente mit Geometrie anzahl-mal, jede Kopie um dx, dy weiter. Kopien erhalten neue Kennzeichen; Türen und Fenster gewählter Wände kommen mit; Verbindungen werden nur innerhalb der Kopie übernommen.")]
    public string ElementeKopieren(
        [Description("Kennzeichen oder Ids, z. B. [\"W-1\", \"T-1\"]")] JsonElement elemente,
        [Description("Abstand der Kopien in x (Ost) in mm")] double? dx = null,
        [Description("Abstand der Kopien in y (Nord) in mm")] double? dy = null,
        [Description("Anzahl Kopien (Vorgabe 1)")] double? anzahl = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("elemente_kopieren", Parameter(("elemente", elemente), ("dx", dx), ("dy", dy), ("anzahl", anzahl)), trockenlauf);

    [McpServerTool(Name = "elemente_drehen"), Description("Dreht Elemente mit Geometrie um einen Punkt (winkel in Grad, Gegenuhrzeigersinn). Türen und Fenster folgen ihrer Wand; Texte und Symbole drehen ihren Winkel mit.")]
    public string ElementeDrehen(
        [Description("Kennzeichen oder Ids, z. B. [\"W-1\", \"T-1\"]")] JsonElement elemente,
        [Description("Drehpunkt [x, y] in mm")] JsonElement mitte,
        [Description("Drehwinkel in Grad, Gegenuhrzeigersinn")] double winkel,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("elemente_drehen", Parameter(("elemente", elemente), ("mitte", mitte), ("winkel", winkel)), trockenlauf);

    [McpServerTool(Name = "elemente_spiegeln"), Description("Spiegelt Elemente mit Geometrie an einer Achse. Bei Türen wird die Aufschlagseite (seite) mitgespiegelt; Türen und Fenster gewählter Wände folgen ihrer Wand.")]
    public string ElementeSpiegeln(
        [Description("Kennzeichen oder Ids, z. B. [\"W-1\", \"T-1\"]")] JsonElement elemente,
        [Description("Spiegelachse [[x1, y1], [x2, y2]] in mm")] JsonElement achse,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("elemente_spiegeln", Parameter(("elemente", elemente), ("achse", achse)), trockenlauf);

    // Modul 1c, Auftrag B3 (DeckeBefehle.cs)
    [McpServerTool(Name = "decke_erzeugen"), Description("Erzeugt Decke, Boden oder flaches Dach aus der Aussenkontur der Wände und Räume eines Geschosses (Löcher gefüllt, getrennte Gebäudeteile je eine Decke) oder aus einem vorgegebenen Umriss. Oberkante: decke = Kote des Geschosses darüber (fehlt es: Kote + Geschosshöhe), boden = Kote, dach = Kote + Geschosshöhe; die Dicke geht nach unten und ist Pflicht. Lehnt ab, wenn an derselben Stelle schon eine Decke derselben Art und Höhe liegt. Die Decke folgt späteren Wandänderungen nicht (Hinweis «neu erzeugen»).")]
    public string DeckeErzeugen(
        [Description("Geschoss, dessen Wände und Räume den Umriss geben (Kennzeichen oder Id)")] string geschoss,
        [Description("decke (über dem Geschoss, gehört zum Geschoss darüber), boden (Oberkante = Kote) oder dach (flach, Oberkante = Kote + Geschosshöhe)")] string art,
        [Description("Dicke in mm, von der Oberkante nach unten (der Kern setzt keine Vorgabe)")] double dicke,
        [Description("Aufbau (Kennzeichen oder Id) – wird als bezuege.aufbau übernommen")] string? aufbau = null,
        [Description("Eigener Umriss [[x, y], …] in mm statt der Kontur aus den Wänden (z wird nicht beachtet)")] JsonElement? umriss = null,
        [Description("Bezeichnung; bei mehreren Gebäudeteilen mit «(Teil n)»")] string? name = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("decke_erzeugen", Parameter(("geschoss", geschoss), ("art", art), ("dicke", dicke), ("aufbau", aufbau), ("umriss", umriss), ("name", name)), trockenlauf);
}
