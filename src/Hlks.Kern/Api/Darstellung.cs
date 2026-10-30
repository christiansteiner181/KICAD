// Darstellungsdaten für Ansichten (Grundriss, 3D, Schema, Listen): je Element, was eine Ansicht zum Zeichnen braucht –
// Schicht, Rolle, Gewerk, Medium mit Farbe und Linienart (SIA 410/1), Zuordnung als Kennzeichen, Geometrie.
// Der Kern liefert die Bedeutung; die Oberfläche entscheidet nur über das Aussehen.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class Darstellung
{
    public static JsonObject Element(Projekt p, Element e)
    {
        var m = p.Modell; var k = p.Katalog;
        k.Klassen.TryGetValue(e.Klasse, out var d);
        string? medium = null;
        if (e.Klasse == "System") medium = e.Text("medium");
        else if (e.System is not null) medium = Netz.SystemMedium(m, e.System);
        else if (Netz.IstNetzgeraet(k, e)) medium = null;
        k.Medien.TryGetValue(medium ?? "", out var md);
        string Kz(string? id) => id is null ? "" : m.Finde(id)?.Kennzeichen ?? "";
        var o = new JsonObject
        {
            ["id"] = e.Id,
            ["kennzeichen"] = e.Kennzeichen,
            ["klasse"] = e.Klasse,
            ["name"] = e.Name,
            ["schicht"] = d?.Schicht,
            ["rolle"] = d?.Rolle,
            ["gewerk"] = Ableitungen.Gewerk(m, k, e),
            ["basis"] = d is null || d.Linie.Count == 0 ? null : d.Linie[0],
            ["medium"] = medium,
            ["farbe"] = md?.FarbeHex,
            ["linie"] = md?.Linie,
            ["geschoss"] = e.Geschoss is null ? null : Kz(e.Geschoss),
            ["raum"] = e.Raum is null ? null : Kz(e.Raum),
            ["system"] = e.System is null ? null : Kz(e.System),
            ["teilVon"] = e.TeilVon is null ? null : Kz(e.TeilVon),
            ["version"] = e.Version,
        };
        if (e.Geometrie is not null) o["geometrie"] = Json.Knoten(e.Geometrie);
        if (e.Eigenschaften.Count > 0)
            o["werte"] = new JsonObject(e.Eigenschaften.OrderBy(x => x.Key).Select(x => KeyValuePair.Create(x.Key,
                // Familie (Modul 2): statt der ganzen Definition nur eine Zusammenfassung; die Definition liefert die Abfrage «familie»
                e.Klasse == Familien.Klasse && x.Key == "definition" ? Familien.Zusammenfassung(e) : x.Value.Inhalt?.DeepClone())));
        if (e.Bezuege.Count > 0) o["bezuege"] = new JsonObject(e.Bezuege.Select(b => KeyValuePair.Create(b.Key, (JsonNode?)Kz(b.Value))));
        // Kennungen in fremden Systemen (ifc, quelle, hkap, wb) – die Oberfläche braucht z. B. die HKAP-Raum-Id (Befund 1d-C)
        if (e.Kennungen is { Count: > 0 } kn) o["kennungen"] = new JsonObject(kn.OrderBy(x => x.Key).Select(x => KeyValuePair.Create(x.Key, (JsonNode?)x.Value)));
        return o;
    }

    /// <summary>Das ganze Modell für den ersten Aufbau einer Ansicht.</summary>
    public static JsonObject Modell(Projekt p) => new()
    {
        ["projekt"] = p.Kopf.Name,
        ["stand"] = Json.Knoten(p.Stand),
        ["elemente"] = new JsonArray(p.Modell.Elemente.OrderBy(e => e.Kennzeichen, StringComparer.OrdinalIgnoreCase).Select(e => (JsonNode)Element(p, e)).ToArray()),
        ["verbindungen"] = new JsonArray(p.Modell.Verbindungen.Select(v => (JsonNode)Verbindung(p, v)).ToArray()),
    };

    static JsonObject Verbindung(Projekt p, Verbindung v) => new()
    {
        ["id"] = v.Id,
        ["von"] = p.Modell.Finde(v.Von.Element)?.Kennzeichen, ["vonAnschluss"] = v.Von.Anschluss,
        ["nach"] = p.Modell.Finde(v.Nach.Element)?.Kennzeichen, ["nachAnschluss"] = v.Nach.Anschluss,
    };

    /// <summary>Nur die genannten Elemente (nach einer Änderung); gelöschte Ids kommen unter «geloescht».</summary>
    public static JsonObject Elemente(Projekt p, IEnumerable<string> ids)
    {
        var vorhanden = new JsonArray(); var geloescht = new JsonArray();
        var verbindungen = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        foreach (var id in ids.Distinct())
        {
            var e = p.Modell.Finde(id);
            if (e is null) { geloescht.Add(id); continue; }
            vorhanden.Add(Element(p, e));
            foreach (var v in p.Modell.VerbindungenVon(e.Id)) verbindungen.TryAdd(v.Id, Verbindung(p, v));
        }
        // verbindungen: alle heutigen Verbindungen der genannten Elemente – die Oberfläche ersetzt damit deren Verbindungen, statt das ganze
        // Modell nachzuladen (Gesamtschau H2, 30.10.2026)
        return new JsonObject { ["stand"] = Json.Knoten(p.Stand), ["elemente"] = vorhanden, ["geloescht"] = geloescht, ["verbindungen"] = new JsonArray([.. verbindungen.Values]) };
    }
}
