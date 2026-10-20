// MCP-Werkzeuge des HLKS-CAD: dünne Hülle über dem Kern. Jedes Werkzeug ruft genau einen Befehl
// (Projekt.Ausfuehren, Rueckgaengig, Wiederholen, Synchronisieren) oder eine Abfrage (Abfragen, Ableitungen) auf.
// Keine eigene Fachlogik. Vor jedem Aufruf wird synchronisiert, damit andere Bearbeiter sofort sichtbar sind.
// Fehler gehen nie als Ausnahme an den Aufrufer, sondern als JSON { "fehler", "vorschlag" }.

using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hlks.Kern;
using ModelContextProtocol.Server;

namespace Hlks.Mcp;

[McpServerToolType]
public sealed partial class Werkzeuge(Projekt projekt)
{
    public const string Anleitung = """
        HLKS-CAD: Modell der Gebäudetechnik (Heizung, Lüftung, Sanitär, MSRL) eines Bauprojekts.
        Das Modell denkt vom System her (Systemgraph): Anlage -> System -> Strang -> Leitungsbauteile (Rohre, Kanäle, Formteile, Armaturen).
        Endgeräte (Apparate, Auslässe) stehen in Räumen. Verbindungen laufen zwischen Anschlüssen und haben eine Fliessrichtung.
        Längen sind in mm, Koordinaten x Ost, y Nord, z über ±0.00. Elemente sprichst du über das Kennzeichen an (Id geht auch).
        Ablauf: zuerst «faehigkeiten» (alle Befehle und Abfragen) und «projekt_ueberblick» aufrufen, dann gezielt abfragen
        (element_lesen, strang_text, system_lesen, netz_folgen, raum_inhalt, raumliste, strangliste, mengen, pruefen).
        Jeder ändernde Befehl (element_anlegen, element_aendern, element_loeschen, verbinden, trennen, leitung_ziehen) kann mit
        trockenlauf=true geprüft werden: er zeigt Änderungen und neue Prüfbefunde, ohne etwas zu speichern. Mit rueckgaengig und
        wiederholen nimmst du eigene Änderungen zurück bzw. stellst sie wieder her.
        «warum» verfolgt einen Wert bis zur Quelle (Herkunft, Rechenweg). Normwerte nie erfinden: fehlt ein Wert, melde das.
        Normen suchst du mit «normen_suchen» und liest die Stelle mit «norm_lesen»; zitiere Norm, Ausgabe, Ziffer und Seite.
        Fehler kommen als JSON mit «fehler» und «vorschlag»; der Vorschlag nennt den Weg zur Lösung.
        """;

    // ---------- Hüllen ----------

    static string Antwort(JsonNode n) => Json.Text(n);

    static string FehlerText(Exception ex) => ex switch
    {
        HlksFehler f => Json.Text(new JsonObject { ["fehler"] = f.Message, ["vorschlag"] = f.Vorschlag }),
        _ => Json.Text(new JsonObject { ["fehler"] = "Interner Fehler: " + ex.Message }),
    };

    /// <summary>Gemeinsame Hülle: Zugriff serialisieren, vorher synchronisieren, Fehler als Text zurückgeben.</summary>
    public string Lauf(Func<string> aktion, bool synchronisieren = true)
    {
        lock (projekt)
        {
            try
            {
                if (synchronisieren) projekt.Synchronisieren();
                return aktion();
            }
            catch (Exception ex) { return FehlerText(ex); }
        }
    }

    static JsonNode? Knoten(JsonElement? e)
    {
        if (e is not { } v || v.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) return null;
        return JsonNode.Parse(v.GetRawText());
    }

    static JsonObject Parameter(params (string Name, object? Wert)[] liste)
    {
        var o = new JsonObject();
        foreach (var (name, wert) in liste)
        {
            switch (wert)
            {
                case null: break;
                case string s: o[name] = s; break;
                case double d: o[name] = d; break;
                case JsonElement je: if (Knoten(je) is { } k) o[name] = k; break;
                default: throw new InvalidOperationException("Parametertyp nicht vorgesehen: " + wert.GetType().Name);
            }
        }
        return o;
    }

    static int Ganz(double? x, int vorgabe) => x is double d ? (int)Math.Round(d) : vorgabe;

    /// <summary>Befehl ausführen (auch von der Kommandozeile benutzt).</summary>
    public string Befehl(string name, JsonObject parameter, bool trockenlauf) =>
        Lauf(() => Antwort(Json.Knoten(projekt.Ausfuehren(name, parameter, trockenlauf))));

    // ---------- Abfragen ----------

    [McpServerTool(Name = "faehigkeiten"), Description("Alle Befehle und Abfragen mit Beschreibung und Parametern. Zuerst aufrufen, wenn unklar ist, was möglich ist.")]
    public string Faehigkeiten() => Lauf(() => Antwort(new Abfragen(projekt).Faehigkeiten()));

    [McpServerTool(Name = "projekt_ueberblick"), Description("Kompakter Überblick: Gebäude, Geschosse, Systeme mit Strängen, Funktionsgraph, Ansichten, Befunde, Modellstand.")]
    public string ProjektUeberblick() => Lauf(() => Antwort(new Abfragen(projekt).ProjektUeberblick()));

    [McpServerTool(Name = "element_lesen"), Description("Ein Element vollständig: Eigenschaften mit Herkunft, Zuordnung, Anschlüsse mit Verbindungen, abgeleitete Werte.")]
    public string ElementLesen([Description("Kennzeichen oder Id")] string element) =>
        Lauf(() => Antwort(new Abfragen(projekt).ElementLesen(element)));

    [McpServerTool(Name = "elemente_suchen"), Description("Elemente filtern nach Klasse, Geschoss, Raum, System oder Text; seitenweise.")]
    public string ElementeSuchen(
        [Description("Klasse oder Basisklasse, z. B. Apparat, Rohr")] string? klasse = null,
        [Description("Geschoss")] string? geschoss = null,
        [Description("Raum")] string? raum = null,
        [Description("System")] string? system = null,
        [Description("Teil von Kennzeichen oder Name")] string? text = null,
        [Description("Seite (ab 1)")] double? seite = null,
        [Description("Einträge je Seite (Vorgabe 50)")] double? proSeite = null) =>
        Lauf(() => Antwort(new Abfragen(projekt).ElementeSuchen(klasse, geschoss, raum, system, text, Ganz(seite, 1), Ganz(proSeite, 50))));

    [McpServerTool(Name = "system_lesen"), Description("Ein System als Textansicht: Stränge, Bauteile, Endgeräte, Netzende.")]
    public string SystemLesen([Description("System")] string system) =>
        Lauf(() => new Abfragen(projekt).SystemLesen(system));

    [McpServerTool(Name = "strang_text"), Description("Ein Strang als Textansicht: Bauteile in Fliessrichtung, angeschlossene Endgeräte je Geschoss mit Raum, weiterer Weg.")]
    public string StrangText([Description("Strang")] string strang) =>
        Lauf(() => new Abfragen(projekt).StrangText(strang));

    [McpServerTool(Name = "netz_folgen"), Description("Folgt dem Netz ab einem Element in Fliessrichtung (stromab) oder dagegen (stromauf).")]
    public string NetzFolgen(
        [Description("Startelement")] string von,
        [Description("stromab | stromauf")] string richtung,
        [Description("Nur Anschlüsse dieses Mediums, z. B. SW")] string? medium = null,
        [Description("Weg an Elementen dieser Klasse beenden")] string? bisKlasse = null) =>
        Lauf(() => Antwort(new Abfragen(projekt).NetzFolgen(von, richtung, medium, bisKlasse)));

    [McpServerTool(Name = "raum_inhalt"), Description("Alles in einem Raum: Endgeräte mit Systemen und offenen Anschlüssen, Datenpunkte.")]
    public string RaumInhalt([Description("Raum")] string raum) =>
        Lauf(() => Antwort(new Abfragen(projekt).RaumInhalt(raum)));

    [McpServerTool(Name = "mengen"), Description("Mengen: Leitungslängen je System, Material und DN; Stückzahlen je Klasse.")]
    public string Mengen(
        [Description("System")] string? system = null,
        [Description("Geschoss")] string? geschoss = null) =>
        Lauf(() => Antwort(Ableitungen.Mengen(projekt, system, geschoss)));

    [McpServerTool(Name = "raumliste"), Description("Raumliste (Ableitung): Fläche, Höhe, Volumen, Endgeräte, Systeme je Raum.")]
    public string Raumliste([Description("Geschoss")] string? geschoss = null) =>
        Lauf(() => Antwort(Ableitungen.Raumliste(projekt, geschoss)));

    [McpServerTool(Name = "strangliste"), Description("Strangliste (Ableitung): Geschosse, Leitungslänge, DN, angeschlossene Endgeräte, Datenpunkte je Strang.")]
    public string Strangliste([Description("System")] string? system = null) =>
        Lauf(() => Antwort(Ableitungen.Strangliste(projekt, system)));

    [McpServerTool(Name = "pruefen"), Description("Prüfbefunde des Modells mit Vorschlag.")]
    public string Pruefen([Description("fehler | warnung | hinweis")] string? schwere = null) =>
        Lauf(() => Antwort(new Abfragen(projekt).Pruefen(schwere)));

    [McpServerTool(Name = "warum"), Description("Verfolgt einen Wert bis zur Quelle: Herkunft, Verlauf, Rechenweg bei abgeleiteten Werten (laenge, flaeche, volumen, raum, geschoss, system).")]
    public string Warum(
        [Description("Element")] string element,
        [Description("Eigenschaft oder abgeleiteter Wert")] string eigenschaft) =>
        Lauf(() => Antwort(new Abfragen(projekt).Warum(element, eigenschaft)));

    [McpServerTool(Name = "aenderungen"), Description("Änderungsprotokoll: wer hat wann was geändert (neueste zuerst).")]
    public string Aenderungen(
        [Description("Nur Änderungen an diesem Element")] string? element = null,
        [Description("Anzahl (Vorgabe 20)")] double? anzahl = null) =>
        Lauf(() => Antwort(new Abfragen(projekt).Aenderungen(element, Ganz(anzahl, 20))));

    [McpServerTool(Name = "katalog"), Description("Katalog: Klassen mit Eigenschaften und Anschlüssen, Medien, Systemtypen.")]
    public string Katalog([Description("Nur diese Klasse")] string? klasse = null) =>
        Lauf(() => Antwort(new Abfragen(projekt).Katalog(klasse)));

    [McpServerTool(Name = "datenpunkte"), Description("Funktionsgraph: Datenpunkte mit Bezug, Station und Adresse; Regelkreise mit Messung, Stellung, Führung.")]
    public string Datenpunkte([Description("Nur Datenpunkte an diesem Element")] string? element = null) =>
        Lauf(() => Antwort(new Abfragen(projekt).Datenpunkte(element)));

    [McpServerTool(Name = "zeitreihe"), Description("Mess- oder Zählerwerte eines Datenpunkts (aktuell aus dem Simulator; später von der Anlage).")]
    public string Zeitreihe(
        [Description("Datenpunkt")] string datenpunkt,
        [Description("ISO-Zeit")] string von,
        [Description("ISO-Zeit")] string bis,
        [Description("Raster in Minuten (Vorgabe 60)")] double? aufloesungMinuten = null) =>
        Lauf(() => Antwort(new Abfragen(projekt).Zeitreihe(datenpunkt, von, bis, aufloesungMinuten ?? 60)));

    // ---------- Befehle ----------

    [McpServerTool(Name = "element_anlegen"), Description("Legt ein Element einer Katalogklasse an (Raum, Apparat, Formteil, Datenpunkt, Ansicht …). Geschoss und Raum werden aus der Lage abgeleitet, wenn sie fehlen. Anschlüsse kommen aus dem Katalog.")]
    public string ElementAnlegen(
        [Description("Klasse aus dem Katalog, z. B. WC, Rohr, Raum, Datenpunkt")] string klasse,
        [Description("Lesbares Kennzeichen, eindeutig; fehlt es, wird es aus dem Klassenkürzel gebildet")] string? kennzeichen = null,
        [Description("Bezeichnung")] string? name = null,
        [Description("Übergeordnetes Element (Strang für Leitungsbauteile, System für Strang, Gebäude für Geschoss)")] string? teilVon = null,
        [Description("Geschoss (Kennzeichen oder Id)")] string? geschoss = null,
        [Description("Raum (Kennzeichen oder Id)")] string? raum = null,
        [Description("System (Kennzeichen oder Id)")] string? system = null,
        [Description("{ name: wert } oder { name: { wert, einheit, quelle } }")] JsonElement? eigenschaften = null,
        [Description("{ art: punkt|linie|polylinie|polygon, punkte: [[x,y,z],…] (mm), hoehe, dicke }")] JsonElement? geometrie = null,
        [Description("Eigene Anschlüsse statt der Katalogvorgabe: [{ name, medium, fluss, dn }]")] JsonElement? anschluesse = null,
        [Description("{ rolle: element }, z. B. Datenpunkt { an: \"WZ-1\" }")] JsonElement? bezuege = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("element_anlegen", Parameter(("klasse", klasse), ("kennzeichen", kennzeichen), ("name", name), ("teilVon", teilVon),
            ("geschoss", geschoss), ("raum", raum), ("system", system), ("eigenschaften", eigenschaften), ("geometrie", geometrie),
            ("anschluesse", anschluesse), ("bezuege", bezuege)), trockenlauf);

    [McpServerTool(Name = "element_aendern"), Description("Ändert ein Element: Kennzeichen, Name, Zuordnung, Eigenschaften (null entfernt), Geometrie, Bezüge.")]
    public string ElementAendern(
        [Description("Kennzeichen oder Id")] string element,
        [Description("Neues Kennzeichen")] string? kennzeichen = null,
        [Description("Neue Bezeichnung")] string? name = null,
        [Description("Neues übergeordnetes Element (null entfernt)")] string? teilVon = null,
        [Description("Geschoss")] string? geschoss = null,
        [Description("Raum")] string? raum = null,
        [Description("System")] string? system = null,
        [Description("{ name: wert | { wert, einheit, quelle } | null }")] JsonElement? eigenschaften = null,
        [Description("Neue Geometrie (ersetzt die alte)")] JsonElement? geometrie = null,
        [Description("{ rolle: element | null }")] JsonElement? bezuege = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("element_aendern", Parameter(("element", element), ("kennzeichen", kennzeichen), ("name", name), ("teilVon", teilVon),
            ("geschoss", geschoss), ("raum", raum), ("system", system), ("eigenschaften", eigenschaften), ("geometrie", geometrie),
            ("bezuege", bezuege)), trockenlauf);

    [McpServerTool(Name = "element_loeschen"), Description("Löscht ein Element samt seinen Verbindungen; Türen und Fenster einer Wand gehen mit ihr. Verweisen andere Elemente darauf (teilVon, Raum, System, Bezüge), wird abgelehnt.")]
    public string ElementLoeschen(
        [Description("Kennzeichen oder Id")] string element,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("element_loeschen", Parameter(("element", element)), trockenlauf);

    [McpServerTool(Name = "verbinden"), Description("Verbindet zwei Anschlüsse. Fehlt der Anschlussname, wird der freie, passende Anschluss gewählt (gleiches Medium, verträgliche Fliessrichtung, bei Lagen der räumlich passende). Haben beide Anschlüsse eine Lage, müssen sie höchstens 1 mm auseinander liegen und gegenläufig zeigen (höchstens 1°); haben beide eine Grösse (DN bzw. Kanalmass), muss sie gleich sein – sonst abgelehnt mit Vorschlag (Element verschieben, Formteil bzw. Reduktion setzen). Ohne Lage (Geräte ohne Familie, nicht platzierte Elemente) wird logisch verbunden. Ein Formteil ohne Strang übernimmt dabei den Strang der angeschlossenen Leitung (bei Leitungen verschiedener Stränge entscheiden die Durchgangsleitungen an a/b, sonst bleibt es ohne Strang, mit Hinweis).")]
    public string Verbinden(
        [Description("{ element, anschluss } oder \"KENNZEICHEN.anschluss\"")] JsonElement von,
        [Description("{ element, anschluss } oder \"KENNZEICHEN.anschluss\"")] JsonElement nach,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("verbinden", Parameter(("von", von), ("nach", nach)), trockenlauf);

    [McpServerTool(Name = "trennen"), Description("Trennt eine Verbindung, angegeben über die Verbindungs-Id oder einen ihrer Anschlüsse.")]
    public string Trennen(
        [Description("Id der Verbindung")] string? verbindung = null,
        [Description("{ element, anschluss } oder \"KENNZEICHEN.anschluss\" – trennt die Verbindung an diesem Anschluss")] JsonElement? anschluss = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("trennen", Parameter(("verbindung", verbindung), ("anschluss", anschluss)), trockenlauf);

    [McpServerTool(Name = "leitung_ziehen"), Description(Hlks.Kern.LeitungZiehen.Text)]
    public string LeitungZiehen(
        [Description("Verlauf in Fliessrichtung: [[x,y,z], …] in mm")] JsonElement punkte,
        [Description("System (Kennzeichen); entfällt, wenn strang angegeben ist")] string? system = null,
        [Description("Strang, zu dem die Leitung gehört")] string? strang = null,
        [Description("Rohr (Vorgabe) oder Kanal")] string? klasse = null,
        [Description("Kennzeichen; fehlt es, wird es gebildet")] string? kennzeichen = null,
        [Description("Geschoss; fehlt es, aus der Höhe des ersten Punkts")] string? geschoss = null,
        [Description("Nennweite")] double? dn = null,
        [Description("Werkstoff")] string? material = null,
        [Description("Weitere Eigenschaften, z. B. { gefaelle: 2 }")] JsonElement? eigenschaften = null,
        [Description("Anschluss, an den der Anfang kommt: { element, anschluss }")] JsonElement? von = null,
        [Description("Anschluss, an den das Ende kommt: { element, anschluss }")] JsonElement? nach = null,
        [Description(Hlks.Kern.LeitungZiehen.ParSerie)] string? serie = null,
        [Description(Hlks.Kern.LeitungZiehen.ParGroesse)] JsonElement? groesse = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("leitung_ziehen", Parameter(("punkte", punkte), ("system", system), ("strang", strang), ("klasse", klasse),
            ("kennzeichen", kennzeichen), ("geschoss", geschoss), ("dn", dn), ("material", material), ("eigenschaften", eigenschaften),
            ("von", von), ("nach", nach), ("serie", serie), ("groesse", groesse)), trockenlauf);

    // ---------- Verlauf und Abgleich ----------

    [McpServerTool(Name = "rueckgaengig"), Description("Macht die letzte eigene Änderung rückgängig (als neues Paket).")]
    public string Rueckgaengig([Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Lauf(() => Antwort(Json.Knoten(projekt.Rueckgaengig(trockenlauf))));

    [McpServerTool(Name = "wiederholen"), Description("Stellt die zuletzt rückgängig gemachte Änderung wieder her.")]
    public string Wiederholen([Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Lauf(() => Antwort(Json.Knoten(projekt.Wiederholen(trockenlauf))));

    [McpServerTool(Name = "synchronisieren"), Description("Holt Änderungen anderer Bearbeiter aus der Projektablage.")]
    public string Synchronisieren() =>
        Lauf(() => Antwort(Json.Knoten(projekt.Synchronisieren())), synchronisieren: false);
}
