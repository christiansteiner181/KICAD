// Quest App V1a, Vorgabe Christian 24.10.2026 (nach den Bildern 02): «nutze die gleiche symbolsprache wie im cad.» Die Symbole der App sind die von KICAD: eine Quelle,
// src/Hlks.Web/wwwroot/js/oberflaeche/symbole.js (Strich, Vorgabe) bzw. symbole-revit.js (mehrfarbig, Benutzereinstellung «oberflaeche.symbole» = «revit»), dazu
// werkzeuge/symbole.js. werkzeuge/quest_symbole.mjs setzt sie beim Bereitstellen (Edge headless) in Texturen um: src/Hlks.Quest/Assets/symbole/<satz>/<name>.png mit
// symbole.json (Prüfsumme je Symbol; der Node-Test quest-symbole.test.mjs meldet, wenn eine Textur nicht mehr zur Quelle passt). Hier stehen nur die Namen, die die
// App braucht – bestehende Schlüssel, wo es passt; neu in symbole.js (und symbole-revit.js) gezeichnet: sitzungstisch, gesten, haken, V1c bildschirm. Rein, ohne
// StereoKit.

namespace Hlks.Xr.Bedienung;

public static class GlasSymbole
{
    /// <summary>Apps und Knöpfe → Schlüssel der KICAD-Symbole.</summary>
    public const string Status = "info", Etagen = "geschoss", Messen = "bemassung", Marker = "problemMarker", Ort = "raumBegehen", Begehen = "raumBegehen",
        Tisch = "sitzungstisch", Ebenen = "sichtbarkeit", Schliessen = "schliessen", Loeschen = "loeschen", Gesten = "gesten", Haken = "haken",
        Desktop = "bildschirm";   // V1c: «Desktop in der Brille», neu in symbole.js und symbole-revit.js gezeichnet

    /// <summary>Die Symbolsätze: Strich (Vorgabe), Revit hell bzw. dunkel (nach der Fassung der App-Farben).</summary>
    public const string SatzStrich = "strich", SatzRevitHell = "revit_hell", SatzRevitDunkel = "revit_dunkel";

    /// <summary>Alle Namen, die die App braucht (für den Bau der Texturen, die Tests und die Anleitung).</summary>
    public static readonly IReadOnlyList<string> Alle = [Status, Etagen, Messen, Marker, Ort, Tisch, Ebenen, Schliessen, Loeschen, Gesten, Haken, Desktop];

    /// <summary>Datei der Textur (relativ zum Ordner Assets) für Name und Satz; Strich wird in der Farbe der Schrift getönt, Revit trägt seine Farben selbst.</summary>
    public static string Datei(string name, string satz) => $"symbole/{satz}/{name}.png";

    /// <summary>Der Satz für die Benutzereinstellung «oberflaeche.symbole» (strich | revit) und die Fassung der Farben (hell = dunkle Schrift).</summary>
    public static string Satz(string? einstellung, bool hell) => einstellung == "revit" ? (hell ? SatzRevitHell : SatzRevitDunkel) : SatzStrich;
}
