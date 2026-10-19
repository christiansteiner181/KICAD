// Leitungen (Modul 3, entwurf/modul-3.md): Einhängepunkt für Befehle und Abfragen. Angelegt vom Hauptlauf (Phase A),
// damit die Bauaufträge die zentralen Dateien (Befehle.cs, Abfragen.cs, Tuer.cs) nicht gleichzeitig ändern müssen:
// Befehle hier registrieren, Abfragen hier beschreiben und in Tuer() beantworten.
// B1: Abfrage leitungsserie (Leitungsserie.cs), Prüfungen P13–P15 und P18 (Leitungspruefungen.cs).
// B2: Befehle leitung_zeichnen, leitung_abzweigen, leitung_aufteilen, leitung_groesse, leitung_serie (RoutingBefehle.cs, Routing.cs),
//     Abfrage leitungszug (Leitungszug.cs), P16 (Leitungspruefungen.cs), Nachführung beim Verschieben (LeitungenNachfuehren.cs).
// B8c: Abfrage verlegung (Verlegung.cs: Einlagen im Beton und Leitungen im Erdreich).

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class LeitungenBefehle
{
    /// <summary>Registriert die Leitungsbefehle (leitung_zeichnen, leitung_abzweigen, leitung_aufteilen, leitung_groesse …).</summary>
    public static void Registrieren(Befehlsregister r)
    {
        // B2: Routing im Kern
        r.Hinzufuegen(new LeitungZeichnen());
        r.Hinzufuegen(new LeitungAbzweigen());
        r.Hinzufuegen(new LeitungAufteilen());
        r.Hinzufuegen(new LeitungGroesseAendern());
        r.Hinzufuegen(new LeitungSerieAendern());
    }
}

public static class LeitungenPruefung
{
    /// <summary>Prüfungen P13–P16 und P18 (Codes in Pruefungen.Codes); hängt die Befunde an b an.</summary>
    public static void Pruefen(Modell m, Katalog k, List<Befund> b)
    {
        Leitungspruefungen.Pruefen(m, k, b);   // B1: P13, P14, P15, P18
    }
}

public static class LeitungenAbfragen
{
    /// <summary>Abfragen zu Leitungen (leitungszug, materialliste …) für Abfragen.Liste.</summary>
    public static readonly IReadOnlyList<AbfrageDef> Definitionen =
    [
        new("leitungsserie", Leitungsserie.AbfrageText, [new("familie", "text", true, "Leitungsserie: Familie der Kategorie Rohr bzw. Kanal (Kennzeichen, Name oder id)")]),   // B1
        new("leitungszug", Leitungszug.AbfrageText, [new("leitung", "text", true, Leitungszug.ParLeitung)]),   // B2
        new("materialliste", Ableitungen.MateriallisteText,   // B4 (Api/Ableitungen.cs)
        [
            new("system", "text", false, "Nur dieses System (Kennzeichen)"),
            new("strang", "text", false, "Nur dieser Strang (Kennzeichen)"),
            new("geschoss", "text", false, "Nur dieses Geschoss (Kennzeichen)"),
        ]),
        new("verlegung", Verlegung.AbfrageText,   // Nachtrag B8c (Verlegung.cs)
        [
            new("geschoss", "text", false, Verlegung.ParGeschoss),
            new("art", "text", false, Verlegung.ParArt),
        ]),
    ];

    /// <summary>Beantwortet eine Leitungsabfrage; null = nicht zuständig.</summary>
    public static object? Tuer(Projekt p, string name, JsonObject par) => name switch
    {
        "leitungsserie" => Leitungsserie.Abfrage(p, Param.Text(par, "familie", true)!),   // B1
        "leitungszug" => Leitungszug.Abfrage(p, Param.Text(par, "leitung", true)!),   // B2
        "materialliste" => Ableitungen.Materialliste(p, Param.Text(par, "system"), Param.Text(par, "strang"), Param.Text(par, "geschoss")),   // B4
        "verlegung" => Verlegung.Abfrage(p, Param.Text(par, "geschoss"), Param.Text(par, "art")),   // Nachtrag B8c
        _ => null,
    };
}
