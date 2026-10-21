// Leitungen (Modul 3, entwurf/modul-3.md): Einhängepunkt für Befehle und Abfragen. Angelegt vom Hauptlauf (Phase A),
// damit die Bauaufträge die zentralen Dateien (Befehle.cs, Abfragen.cs, Tuer.cs) nicht gleichzeitig ändern müssen:
// Befehle hier registrieren, Abfragen hier beschreiben und in Tuer() beantworten.
// B1: Abfrage leitungsserie (Leitungsserie.cs), Prüfungen P13–P15 und P18 (Leitungspruefungen.cs).
// B2: Befehle leitung_zeichnen, leitung_abzweigen, leitung_aufteilen, leitung_groesse, leitung_serie (RoutingBefehle.cs, Routing.cs),
//     Abfrage leitungszug (Leitungszug.cs), P16 (Leitungspruefungen.cs), Nachführung beim Verschieben (LeitungenNachfuehren.cs).
// B8c: Abfrage verlegung (Verlegung.cs: Einlagen im Beton und Leitungen im Erdreich).
// Nachtrag 21.10.2026: leitungsserie in zwei Schritten (Vorbereiten: Lesestand unter der Sperre, Rechnung danach ohne Sperre und abbrechbar,
//     LeitungsserieAbfrage.cs); Abfrage abzweigwinkel (Abzweigwinkel einer Serie bzw. Leitung für das Werkzeug Abzweig).

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
    /// <summary>Prüfungen P13–P16, P18, P29, P30 und P45 (Codes in Pruefungen.Codes); hängt die Befunde an b an.</summary>
    public static void Pruefen(Modell m, Katalog k, List<Befund> b)
    {
        Leitungspruefungen.Pruefen(m, k, b);   // B1: P13, P14, P15, P18
        FreieGroesse.PruefenFormteile(m, k, b);   // Nachtrag 21.10.2026: P45 Formteil einer freien Serie ohne eigene Grösse
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
        new("abzweigwinkel", Leitungsserie.AbzweigwinkelAbfrageText,   // Nachtrag 21.10.2026 (LeitungsserieAbfrage.cs)
        [
            new("leitung", "text", false, Leitungsserie.ParAbzweigLeitung),
            new("familie", "text", false, Leitungsserie.ParAbzweigFamilie),
            new("groesse", "objekt", false, Leitungsserie.ParAbzweigGroesse),
        ]),
    ];

    /// <summary>Beantwortet eine Leitungsabfrage; null = nicht zuständig.</summary>
    public static object? Tuer(Projekt p, string name, JsonObject par) => name switch
    {
        "leitungsserie" => Leitungsserie.Abfrage(p, Param.Text(par, "familie", true)!),   // B1
        "leitungszug" => Leitungszug.Abfrage(p, Param.Text(par, "leitung", true)!),   // B2
        "materialliste" => Ableitungen.Materialliste(p, Param.Text(par, "system"), Param.Text(par, "strang"), Param.Text(par, "geschoss")),   // B4
        "verlegung" => Verlegung.Abfrage(p, Param.Text(par, "geschoss"), Param.Text(par, "art")),   // Nachtrag B8c
        "abzweigwinkel" => Leitungsserie.AbzweigwinkelAbfrage(p, Param.Text(par, "leitung"), Param.Text(par, "familie"), par["groesse"]),   // Nachtrag 21.10.2026
        _ => null,
    };

    /// <summary>Abfragen in zwei Schritten (Nachtrag 21.10.2026): unter der Sperre nur den Lesestand vorbereiten, gerechnet wird danach ohne
    /// Sperre und abbrechbar (Tuer.AbfrageVorbereiten); null = die Abfrage rechnet wie alle übrigen in einem Schritt.</summary>
    public static Func<CancellationToken, object>? Vorbereiten(Projekt p, string name, JsonObject par) => name switch
    {
        "leitungsserie" => Leitungsserie.AbfrageVorbereiten(p, Param.Text(par, "familie", true)!),
        _ => null,
    };
}
