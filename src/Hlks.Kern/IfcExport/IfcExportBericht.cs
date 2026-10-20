// IFC-Export (Modul 7 Teil B2): Exportbericht – als Antwort der Abfrage, als <Name>.bericht.json und lesbar als <Name>.bericht.md.
// Inhalt: Datei, Fassung, Koordinaten, Umfang, Zähler je Klasse und Entität, übersprungene Elemente mit Grund, Kontrolle beim Zurücklesen,
// Schemaprüfung (Xbim), IDS-Ergebnis je Regel mit den Kennzeichen der betroffenen Elemente, Hinweise, Warnungen, Dauer.

using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class IfcExportBericht
{
    static string Z(double x, string f = "0.###") => x.ToString(f, CultureInfo.InvariantCulture);
    static JsonArray Texte(IEnumerable<string> l) => new(l.Select(x => (JsonNode)JsonValue.Create(x)!).ToArray());

    public static JsonObject Json(Projekt p, IfcExAusgabe aus, IfcExportAufbau aufbau, IfcExUmfang umfang, string pfad, long bytes, int entitaeten,
        JsonObject kontrolle, JsonObject schema, JsonArray ids, List<string> hinweise, List<string> warnungen, double dauer, double schreiben, double pruefen)
    {
        var jeKlasse = new JsonArray(aus.Produkte.GroupBy(x => (x.Klasse, x.Entitaet, Typ: x.Typ?.Entitaet, x.PredefinedType))
            .OrderBy(g => g.Key.Klasse, StringComparer.Ordinal).ThenBy(g => g.Key.Entitaet, StringComparer.Ordinal)
            .Select(g => (JsonNode)new JsonObject
            {
                ["klasse"] = g.Key.Klasse, ["entitaet"] = g.Key.Entitaet, ["typobjekt"] = g.Key.Typ, ["predefinedType"] = g.Key.PredefinedType, ["anzahl"] = g.Count(),
            }).ToArray());
        var jeEntitaet = new SortedDictionary<string, int>(StringComparer.Ordinal);
        void Dazu(string e, int n = 1) => jeEntitaet[e] = jeEntitaet.GetValueOrDefault(e) + n;
        foreach (var x in aus.Produkte) { Dazu(x.Entitaet); if (x.Ports.Count > 0) Dazu("IfcDistributionPort", x.Ports.Count); if (x.Daemmung is not null) Dazu("IfcCovering"); }
        foreach (var g in aus.Gruppen) Dazu(g.Entitaet);
        foreach (var t in aus.Typen) Dazu(t.Entitaet);
        Dazu("IfcBuildingStorey", aus.Geschosse.Count);
        Dazu("IfcBuilding", aus.Gebaeude.Count);
        if (aus.Verbindungen.Count > 0) Dazu("IfcRelConnectsPorts", aus.Verbindungen.Count);
        var gk = aus.Geschosse.ToDictionary(g => g.Element.Id, g => g.Kennzeichen);
        var idsRegeln = ids.OfType<JsonObject>().SelectMany(i => i["regeln"]?.AsArray().OfType<JsonObject>() ?? []).ToList();
        var bezug = aus.Bezug;
        return new JsonObject
        {
            ["datei"] = pfad,
            ["fassung"] = aus.Fassung,
            ["einheit"] = "mm",
            ["groesseBytes"] = bytes,
            ["entitaetenDatei"] = entitaeten,
            ["stand"] = aus.Zeit.ToString("o"),
            ["dauerSekunden"] = Math.Round(dauer, 2),
            ["dauer"] = new JsonObject { ["aufbauUndSchreiben"] = Math.Round(schreiben, 2), ["pruefen"] = Math.Round(pruefen, 2) },
            ["projekt"] = new JsonObject { ["name"] = aus.ProjektName, ["id"] = aus.ProjektId },
            ["ergebnis"] = new JsonObject
            {
                ["kontrolle"] = kontrolle["ergebnis"]?.DeepClone(),
                ["schemapruefung"] = schema["ergebnis"]?.DeepClone(),
                ["ids"] = ids.OfType<JsonObject>().All(i => i["ergebnis"]?.GetValue<string>() == "bestanden") ? "bestanden" : "nicht bestanden",
                ["idsRegelnNichtBestanden"] = idsRegeln.Count(r => r["ergebnis"]?.GetValue<string>() == "nicht bestanden"),
            },
            ["koordinaten"] = new JsonObject
            {
                ["art"] = bezug is null ? "projekt" : "gemeinsam",
                ["text"] = bezug is null
                    ? "Projektkoordinaten von KICAD (x Ost, y Nord, z über ±0.00), Gebäude im Ursprung – das Projekt hat keinen IFC-Bezug"
                    : $"gemeinsame Koordinaten: Gebäude im Projektbezug (Weltpunkt {Z(bezug.X / 1000)} / {Z(bezug.Y / 1000)} / {Z(bezug.Z / 1000)} m, Drehung {Z(bezug.Drehung, "0.######")}°), wie die Architekten-IFC",
                ["bezug"] = bezug?.AlsJson(),
                ["kartenbezug"] = aus.Karte is { } k && aus.IstIfc4
                    ? new JsonObject { ["bezugssystem"] = k.Bezugssystem, ["ostM"] = Math.Round(k.OstMm / 1000, 3), ["nordM"] = Math.Round(k.NordMm / 1000, 3), ["hoeheM"] = Math.Round(k.HoeheMm / 1000, 3), ["drehung"] = Math.Round(k.Drehung, 6), ["ifc"] = "IfcMapConversion" }
                    : null,
            },
            ["umfang"] = new JsonObject
            {
                ["gewerke"] = umfang.Gewerke is null ? null : Texte(umfang.Gewerke.Order()),
                ["geschosse"] = umfang.GeschossIds is null ? null : Texte(umfang.GeschossIds.Select(id => gk.GetValueOrDefault(id, id)).Order()),
                ["klassen"] = umfang.Klassen is null ? null : Texte(umfang.Klassen),
                ["raeume"] = umfang.Raeume,
            },
            ["anzahl"] = new JsonObject
            {
                ["elemente"] = aus.Produkte.Count(x => !x.IstRaum), ["raeume"] = aus.Produkte.Count(x => x.IstRaum),
                ["ports"] = aus.Produkte.Sum(x => x.Ports.Count), ["verbindungen"] = aus.Verbindungen.Count,
                ["systeme"] = aus.Gruppen.Count(g => g.Art == "system"), ["straenge"] = aus.Gruppen.Count(g => g.Art == "strang"),
                ["anlagen"] = aus.Gruppen.Count(g => g.Art == "anlage"), ["regelkreise"] = aus.Gruppen.Count(g => g.Art == "regelkreis"),
                ["typobjekte"] = aus.Typen.Count, ["daemmungen"] = aus.Produkte.Count(x => x.Daemmung is not null),
                ["geschosse"] = aus.Geschosse.Count, ["uebersprungen"] = aufbau.Uebersprungen.Count,
            },
            ["jeKlasse"] = jeKlasse,
            ["jeEntitaet"] = new JsonObject(jeEntitaet.Select(x => KeyValuePair.Create(x.Key, (JsonNode?)x.Value))),
            ["uebersprungenJeGrund"] = new JsonObject(aufbau.Uebersprungen.GroupBy(u => u.Grund).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => KeyValuePair.Create(g.Key, (JsonNode?)g.Count()))),
            ["uebersprungen"] = new JsonArray(aufbau.Uebersprungen.Take(500).Select(u => (JsonNode)new JsonObject { ["element"] = u.Kennzeichen, ["klasse"] = u.Klasse, ["grund"] = u.Grund }).ToArray()),
            ["globalId"] = "UUID v5 (RFC 4122) im KICAD-Namensraum aus «<Projekt-Id>/element/<Element-Id>» (Ports, Dämmung, Typobjekte, Beziehungen entsprechend) – je Element fest über Exporte, Fassungen und Rechner",
            ["kontrolle"] = kontrolle,
            ["schemapruefung"] = schema,
            ["ids"] = ids,
            ["hinweise"] = Texte(hinweise),
            ["warnungen"] = Texte(warnungen),
        };
    }

    /// <summary>Lesbarer Bericht (Markdown) aus dem JSON-Bericht.</summary>
    public static string Markdown(JsonObject b)
    {
        string S(JsonNode? n) => n switch { null => "–", JsonValue v when v.TryGetValue<string>(out var s) => s, _ => n.ToJsonString() };
        string Zelle(string? s) => (s ?? "–").Replace("|", "\\|").Replace("\n", " ");
        var md = new StringBuilder();
        md.AppendLine($"# IFC-Exportbericht – {S(b["projekt"]?["name"])}");
        md.AppendLine();
        md.AppendLine($"- Datei: `{S(b["datei"])}` ({Z((b["groesseBytes"]?.GetValue<long>() ?? 0) / 1024.0 / 1024.0, "0.0")} MB, {S(b["entitaetenDatei"])} Entitäten)");
        md.AppendLine($"- Fassung: **{S(b["fassung"])}**, Einheit mm, Stand {S(b["stand"])}, Dauer {S(b["dauerSekunden"])} s");
        md.AppendLine($"- Koordinaten: {S(b["koordinaten"]?["text"])}");
        if (b["koordinaten"]?["kartenbezug"] is JsonObject kb) md.AppendLine($"- Kartenbezug (IfcMapConversion): {S(kb["bezugssystem"])}, Ursprung {S(kb["ostM"])} / {S(kb["nordM"])} / {S(kb["hoeheM"])} m, Drehung {S(kb["drehung"])}°");
        var u = b["umfang"];
        md.AppendLine($"- Umfang: Gewerke {S(u?["gewerke"])}, Geschosse {S(u?["geschosse"])}, Klassen {S(u?["klassen"])}, Räume {S(u?["raeume"])}");
        md.AppendLine($"- Ergebnis: Kontrolle **{S(b["ergebnis"]?["kontrolle"])}**, Schemaprüfung **{S(b["ergebnis"]?["schemapruefung"])}**, IDS **{S(b["ergebnis"]?["ids"])}**");
        md.AppendLine();
        md.AppendLine("## Zähler");
        md.AppendLine();
        var a = b["anzahl"]!.AsObject();
        md.AppendLine(string.Join(", ", a.Select(x => $"{x.Key} {S(x.Value)}")));
        md.AppendLine();
        md.AppendLine("| Klasse | Entität | Typobjekt | PredefinedType | Anzahl |");
        md.AppendLine("| --- | --- | --- | --- | ---: |");
        foreach (var k in b["jeKlasse"]!.AsArray().OfType<JsonObject>())
            md.AppendLine($"| {Zelle(S(k["klasse"]))} | {Zelle(S(k["entitaet"]))} | {Zelle(S(k["typobjekt"]))} | {Zelle(S(k["predefinedType"]))} | {S(k["anzahl"])} |");
        md.AppendLine();
        md.AppendLine("Je Entität: " + string.Join(", ", b["jeEntitaet"]!.AsObject().Select(x => $"{x.Key} {S(x.Value)}")));
        md.AppendLine();
        md.AppendLine("## Übersprungene Elemente");
        md.AppendLine();
        var ueb = b["uebersprungen"]!.AsArray().OfType<JsonObject>().ToList();
        if (ueb.Count == 0) md.AppendLine("Keine.");
        foreach (var g in ueb.GroupBy(x => S(x["grund"])))
            md.AppendLine($"- {g.Key} ({g.Count()}): {string.Join(", ", g.Take(20).Select(x => S(x["element"])))}{(g.Count() > 20 ? ", …" : "")}");
        md.AppendLine();
        md.AppendLine("## Schemaprüfung (Xbim)");
        md.AppendLine();
        var s = b["schemapruefung"]!;
        md.AppendLine($"{S(s["ergebnis"])}: {S(s["entitaetenGeprueft"])} Entitäten geprüft, {S(s["meldungen"])} Meldungen ({S(s["werkzeug"])}).");
        foreach (var x in s["beispiele"]?.AsArray() ?? []) md.AppendLine($"- {S(x)}");
        var k2 = b["kontrolle"]!;
        md.AppendLine();
        md.AppendLine($"Kontrolle beim Zurücklesen: {S(k2["ergebnis"])} ({S(k2["produkte"])} Elemente, {S(k2["ports"])} Ports, {S(k2["gruppen"])} Gruppen; Fassung {S(k2["fassungGelesen"])}, Einheit {S(k2["einheitGelesen"])}).");
        foreach (var x in k2["abweichungen"]?.AsArray() ?? []) md.AppendLine($"- {S(x)}");
        md.AppendLine();
        md.AppendLine("## IDS");
        foreach (var i in b["ids"]!.AsArray().OfType<JsonObject>())
        {
            md.AppendLine();
            md.AppendLine($"### {S(i["titel"])}{(i["basis"]?.GetValue<bool>() == true ? " (IDS-Basis H+K)" : "")}");
            md.AppendLine();
            md.AppendLine($"Datei `{S(i["datei"])}`, Schema: {S(i["xsd"])}, Ergebnis **{S(i["ergebnis"])}**.");
            foreach (var x in i["xsdMeldungen"]?.AsArray() ?? []) md.AppendLine($"- XSD: {S(x)}");
            md.AppendLine();
            md.AppendLine("| Regel | Fassung | Anwendbar | Nicht bestanden | Ergebnis | Betroffene Elemente |");
            md.AppendLine("| --- | --- | ---: | ---: | --- | --- |");
            foreach (var r in i["regeln"]!.AsArray().OfType<JsonObject>())
            {
                var bef = r["befunde"]?.AsArray().OfType<JsonObject>().Select(x => S(x["kennzeichen"])).ToList() ?? [];
                md.AppendLine($"| {Zelle(S(r["name"]))} | {Zelle(S(r["ifcVersion"]))} | {S(r["anwendbar"])} | {S(r["nichtBestandenElemente"])} | {Zelle(S(r["ergebnis"]) + (r["grund"] is { } gr ? $" ({S(gr)})" : ""))} | {Zelle(bef.Count == 0 ? "–" : string.Join(", ", bef.Take(15)) + (bef.Count > 15 ? ", …" : ""))} |");
            }
            foreach (var r in i["regeln"]!.AsArray().OfType<JsonObject>().Where(r => r["befunde"]?.AsArray().Count > 0))
            {
                md.AppendLine();
                md.AppendLine($"Befunde «{S(r["name"])}»:");
                foreach (var f in r["befunde"]!.AsArray().OfType<JsonObject>().Take(30))
                    md.AppendLine($"- {S(f["kennzeichen"])} ({S(f["entitaet"])}): {string.Join("; ", f["gruende"]?.AsArray().Select(S) ?? [])}");
            }
        }
        if (b["warnungen"]?.AsArray().Count > 0)
        {
            md.AppendLine();
            md.AppendLine("## Warnungen");
            md.AppendLine();
            foreach (var x in b["warnungen"]!.AsArray()) md.AppendLine($"- {S(x)}");
        }
        if (b["hinweise"]?.AsArray().Count > 0)
        {
            md.AppendLine();
            md.AppendLine("## Hinweise");
            md.AppendLine();
            foreach (var x in b["hinweise"]!.AsArray()) md.AppendLine($"- {S(x)}");
        }
        return md.ToString();
    }
}
