// Abfrage ifc_teile (Christian 03.11.2026: «Es soll auch einen flow geben; Familie aus IFC: IFC einlesen > Teile wählen > Familie nachmodellieren»):
// die Teile einer eingelesenen IFC-Unterlage aus ihrer Netzdatei – zum Auswählen für familie_netz_uebernehmen (objekte). Ohne unterlage: die
// IFC-Unterlagen des Projekts mit Netzdatei (Schritt «IFC wählen»).

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class IfcTeile
{
    public const string Text = "Teile einer IFC-Unterlage zum Auswählen (Familie aus IFC: IFC einlesen › Teile wählen › Familie nachmodellieren): je Bauteil mit Körper " +
        "GlobalId, IFC-Klasse, Typname, Grösse (Hüllquader in mm), Dreiecke, Kote und – falls übernommen – das Element. Ohne unterlage die IFC-Unterlagen des " +
        "Projekts mit Netzdatei. Danach familie_netz_uebernehmen mit objekte = gewählte GlobalIds (und anschluesse = true für die Ports).";
    public const string ParUnterlage = "IFC-Unterlage (Kennzeichen, Id oder Dateiname); leer = Liste der IFC-Unterlagen";
    public const string ParSuche = "Nur Teile, deren Typname, IFC-Klasse oder GlobalId diesen Text enthält (Gross/klein egal)";
    public const string ParAnzahl = "Höchstzahl Teile (Vorgabe 500)";
    public static IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("unterlage", "text", false, ParUnterlage),
        new("suche", "text", false, ParSuche),
        new("anzahl", "zahl", false, ParAnzahl),
    ];

    public static JsonObject Liste(Projekt p, JsonObject? par)
    {
        par ??= [];
        var m = p.Modell;
        var unterlagen = m.DerKlasse("Unterlage").Where(u => string.Equals(u.Text("art"), "ifc", StringComparison.OrdinalIgnoreCase) && u.Text("netz") is { Length: > 0 }).ToList();
        var aus = new JsonObject
        {
            ["unterlagen"] = new JsonArray(unterlagen.Select(u => (JsonNode)new JsonObject
            {
                ["kennzeichen"] = u.Kennzeichen, ["name"] = u.Name, ["datei"] = Unterlagen.OriginalName(u.Text("datei") ?? ""),
            }).ToArray()),
        };
        if (Param.Text(par, "unterlage") is not { Length: > 0 } bezug) { aus["teile"] = new JsonArray(); return aus; }
        var ul = m.Finde(bezug) ?? unterlagen.FirstOrDefault(u => string.Equals(Unterlagen.OriginalName(u.Text("datei") ?? ""), bezug, StringComparison.OrdinalIgnoreCase))
            ?? throw new HlksFehler($"Unterlage «{bezug}» gibt es nicht.", "IFC-Unterlagen: " + string.Join(", ", unterlagen.Select(u => u.Kennzeichen)));
        if (ul.Text("netz") is not { Length: > 0 } netz)
            throw new HlksFehler($"{ul.Kennzeichen} ist keine eingelesene IFC-Unterlage (keine Netzdatei).", "Die IFC-Datei zuerst einlesen (ifc_uebernehmen, bereiche [] liest nur ein).");
        var d = IfcNetzdatei.Oeffnen(Unterlagen.Pfad(p, netz));
        var suche = Param.Text(par, "suche")?.Trim();
        int anzahl = Param.Zahl(par, "anzahl") is double a && a > 0 ? (int)a : 500;
        var elemente = m.Elemente.Where(e => e.Kennungen?.GetValueOrDefault("quelle") == ul.Id && e.Kennungen.GetValueOrDefault("ifc") is { Length: > 0 })
            .GroupBy(e => e.Kennungen!["ifc"].Split(':')[0]).ToDictionary(g => g.Key, g => g.First().Kennzeichen);
        var treffer = d.Produkte.Where(x => x.Rolle == IfcNetzRolle.Bauteil && x.Dreiecke > 0)
            .Where(x => string.IsNullOrEmpty(suche) || (x.Typname?.Contains(suche, StringComparison.OrdinalIgnoreCase) ?? false)
                        || x.Entitaet.Contains(suche, StringComparison.OrdinalIgnoreCase) || x.GlobalId.Contains(suche, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Typname ?? x.Entitaet, StringComparer.CurrentCultureIgnoreCase).ThenBy(x => x.GlobalId, StringComparer.Ordinal).ToList();
        aus["unterlage"] = ul.Kennzeichen;
        aus["gesamt"] = treffer.Count;
        aus["teile"] = new JsonArray(treffer.Take(anzahl).Select(x => (JsonNode)new JsonObject
        {
            ["globalId"] = x.GlobalId, ["entitaet"] = x.Entitaet, ["typ"] = x.Typname, ["dreiecke"] = x.Dreiecke,
            ["groesse"] = new JsonArray(Math.Round(x.Begrenzung[3] - x.Begrenzung[0]), Math.Round(x.Begrenzung[4] - x.Begrenzung[1]), Math.Round(x.Begrenzung[5] - x.Begrenzung[2])),
            ["kote"] = x.GeschossKote is double k ? Math.Round(k) : null,
            ["element"] = elemente.GetValueOrDefault(x.GlobalId),
        }).ToArray());
        return aus;
    }
}
