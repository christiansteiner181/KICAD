// Abfragen der Normensuche (Modul 3c, Teil K): normen_suchen und norm_lesen über die Tür des Kerns (Tuer.Abfrage). Der MCP-Server bildet
// sie als Werkzeuge ab (WerkzeugeNormen.cs), die KI-Palette ruft die Suche über GET /api/normen/suche auf. Die Normen hängen nicht am
// Projekt: gelesen wird das Verzeichnis der Wurzel aus HLKS_NORMEN (Vorgabe W:\05_Normen und Merkblätter), siehe Normenverzeichnis.cs.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class NormenAbfragen
{
    public const string SuchenText = "Sucht im Normenverzeichnis des Büros (SIA, SWKI, VKF, suissetec, Sprinkler, Merkblätter; nur deutsche Fassungen) im Volltext. " +
        "Treffer: Norm, Ausgabe, Datei, Seite, Ziffer (Überschrift vor der Fundstelle) und ein kurzer Auszug (höchstens 600 Zeichen). Fachbegriffe statt " +
        "ganzer Sätze angeben. Normwerte nie aus dem Gedächtnis nennen: die Seite mit norm_lesen lesen und Norm, Ausgabe, Ziffer und Seite zitieren.";
    public const string LesenText = "Liest Seiten einer Norm aus dem Normenverzeichnis als Text (höchstens 3 Seiten je Aufruf), z. B. die Seite eines Treffers " +
        "von normen_suchen. Der Text ist lizenziert: in Antworten nur kurze Auszüge wiedergeben und Norm, Ausgabe, Ziffer und Seite nennen.";
    public const string ParFrage = "Fachbegriffe, z. B. «Mindestgefälle Grundleitung» (Füllwörter werden übergangen)";
    public const string ParReihe = "Nur diese Reihe: SIA, SWKI, VKF, Vorlage, suissetec, Sprinkler, Merkblatt";
    public const string ParDatei = "Datei aus dem Treffer (relativ zum Normenordner), Id (N-…) oder Kürzel der Norm (z. B. SIA 2024:2021)";
    public const string ParSeite = "Erste Seite (ab 1)";
    public const string ParBis = "Letzte Seite (Vorgabe = seite; höchstens seite + 2)";

    public static readonly IReadOnlyList<AbfrageDef> Definitionen =
    [
        new("normen_suchen", SuchenText, [new("frage", "text", true, ParFrage), new("reihe", "text", false, ParReihe)]),
        new("norm_lesen", LesenText, [new("datei", "text", true, ParDatei), new("seite", "zahl", true, ParSeite), new("bis", "zahl", false, ParBis)]),
    ];

    /// <summary>Tür: null, wenn die Abfrage nicht zu den Normen gehört.</summary>
    public static object? Tuer(Projekt p, string name, JsonObject par) => name switch
    {
        "normen_suchen" => Normenverzeichnis.Standard().SuchenJson(Param.Text(par, "frage", true)!, Param.Text(par, "reihe")),
        "norm_lesen" => Normenverzeichnis.Standard().Lesen(Param.Text(par, "datei", true)!,
            Param.Zahl(par, "seite") is double s ? (int)Math.Round(s) : throw new HlksFehler("Parameter «seite» fehlt.", "Seite ab 1 angeben, z. B. aus dem Treffer von normen_suchen."),
            Param.Zahl(par, "bis") is double b ? (int)Math.Round(b) : null),
        _ => null,
    };
}
