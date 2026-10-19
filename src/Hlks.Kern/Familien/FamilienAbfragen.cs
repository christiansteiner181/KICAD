// Familien (Modul 2, B1): Abfragen familien, familie, anschlusslagen, familie_ansicht. Beschreibungen hier (für Abfragen.Liste
// und das MCP), beantwortet über Tuer().

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class FamilienAbfragen
{
    public const string FamilienText = "Familien des Projekts mit Kategorie, Status, Version, Typen, Anzahl Verwendungen, ob die Definition gültig ist und Anzahl Definitionsfehler; " +
        "bibliothekStand je Familie: Stand zur Familienbibliothek (aktuell, neuere_version, im_projekt_geaendert, parallel_geaendert, nicht_in_bibliothek) mit Text, Version und aktueller Version.";
    public const string FamilieText = "Eine Familie vollständig: Kopf, Definition, aufgelöste Werte je Typ (Exemplarparameter mit Vorgabewerten), definitionsfehler, hinweise (Lücken wie fehlende Anschlussgrössen) und auswertungsfehler je Typ (Stelle, Meldung, Vorschlag), verwendende Elemente.";
    public const string AnschlusslagenText = "Anschlüsse eines Elements (alle Elemente mit Anschlüssen, Modul 3) in Weltkoordinaten: je Anschluss Name, Medium, Fluss, DN (Rohr) bzw. d oder b/h (Kanal, mm), Lage [x, y, z] in mm und Richtung aus dem Element heraus (Einheitsvektor), verbunden mit, Quelle der Lage. Leitungen: anfang = erster Punkt, ende = letzter Punkt, Richtung nach aussen, Grösse aus dn bzw. durchmesser, breite × hoehe; Elemente mit Familie: aus Familie, Einfügepunkt, ausrichtung bzw. drehung und Parametern; Elemente ohne Familie und nicht platzierte Elemente: Lage und Richtung null (nur logisch verbindbar).";
    public const string FamilieAnsichtText = "Die Familie allein am Ursprung (Editor, Kontrollbild): Plangrafik der Ansicht vorne, hinten, links, rechts, oben oder unten (Sichtkanten mit Verdeckung, u/v in mm, je Grafikteil die Körper-Id «teil»), grundriss (wie im Grundriss: Sinnbild, sonst Umriss von oben) bzw. die Körper für 3d (je Teil die Körper-Id), je für Typ, Detailgrad und optionale Exemplarwerte.";

    /// <summary>Abfragen der Familien (familien, familie, anschlusslagen, familie_ansicht …) für Abfragen.Liste.</summary>
    public static readonly IReadOnlyList<AbfrageDef> Definitionen =
    [
        new("familien", FamilienText, []),
        new("familie", FamilieText, [new("familie", "text", true, "Familie (Kennzeichen, Name oder id)")]),
        new("anschlusslagen", AnschlusslagenText, [new("element", "text", true, "Element mit Anschlüssen (Kennzeichen oder Id)")]),
        new("familie_ansicht", FamilieAnsichtText,
        [
            new("familie", "text", true, "Familie (Kennzeichen, Name oder id)"),
            new("typ", "text", true, "Typ aus der Typenliste"),
            new("ansicht", "text", true, "vorne | hinten | links | rechts | oben | unten | 3d | grundriss"),
            new("detailgrad", "text", true, "grob | mittel | fein"),
            new("parameter", "objekt", false, "Exemplarwerte { name: wert }"),
        ]),
        new("familien_bibliothek", FamilienBibliothek.Text, FamilienBibliothek.Parameter),   // Nachtrag 2 (C1); Nachtrag B7: Bürobibliothek
        // B4: Massvergleich je Typ und Kontrollbild (FamilienMasse.cs, FamilienKontrollbild.cs)
        new("familie_masse", FamilienMasse.AbfrageText,
        [
            new("familie", "text", true, "Familie (Kennzeichen, Name oder id)"),
            new("typ", "text", false, "Nur dieser Typ; leer = alle Typen"),
        ]),
        new("familie_vergleichen", FamilienKontrollbild.AbfrageText, FamilienKontrollbild.Parameter),
    ];

    /// <summary>Beantwortet eine Familienabfrage; null = nicht zuständig.</summary>
    public static object? Tuer(Projekt p, string name, JsonObject par) => name switch
    {
        "familien" => Liste(p),
        "familie" => Familie(p, Param.Text(par, "familie", true)!),
        "anschlusslagen" => Anschlusslagen(p, Param.Text(par, "element", true)!),
        "familien_bibliothek" => FamilienBibliothek.Liste(p, par),
        "familie_ansicht" => FamilienAnsicht.Erzeugen(p, Param.Text(par, "familie", true)!, Param.Text(par, "typ", true)!, Param.Text(par, "ansicht", true)!,
            Param.Text(par, "detailgrad", true)!, par["parameter"] switch
            {
                null => null,
                JsonObject o => o,
                _ => throw new HlksFehler("Parameter «parameter» muss ein Objekt sein.", "Form: { \"bodenabstand\": 200 }"),
            }),
        "familie_masse" => FamilienMasse.Abfrage(p, Param.Text(par, "familie", true)!, Param.Text(par, "typ")),   // B4
        "familie_vergleichen" => FamilienKontrollbild.Abfrage(p, par),                                              // B4
        _ => null,
    };

    static IEnumerable<Element> Verwender(Modell m, Element familie) =>
        m.Elemente.Where(e => e.Bezuege.GetValueOrDefault(Familien.Bezug) == familie.Id).OrderBy(e => e.Kennzeichen, StringComparer.OrdinalIgnoreCase);

    public static JsonObject Liste(Projekt p)
    {
        var m = p.Modell;
        var anzahl = m.Elemente.Where(e => e.Bezuege.ContainsKey(Familien.Bezug)).GroupBy(e => e.Bezuege[Familien.Bezug]).ToDictionary(g => g.Key, g => g.Count());
        var stand = FamilienBibliothek.StandFuer(p);   // Nachtrag B7: Stand zur Familienbibliothek
        return new JsonObject
        {
            ["familien"] = new JsonArray(m.DerKlasse(Familien.Klasse).OrderBy(f => f.Kennzeichen, StringComparer.OrdinalIgnoreCase).Select(f =>
            {
                var def = Familien.Definition(f, p.Katalog);
                return (JsonNode)new JsonObject
                {
                    ["kennzeichen"] = f.Kennzeichen, ["name"] = f.Name, ["id"] = Familien.DefinitionsId(f), ["kategorie"] = f.Text("kategorie"),
                    ["status"] = f.Text("status"), ["version"] = f.Zahl("version"),
                    ["typen"] = new JsonArray(def.Typen.Select(t => (JsonNode)JsonValue.Create(t.Name)).ToArray()),
                    ["verwendungen"] = anzahl.GetValueOrDefault(f.Id), ["gueltig"] = def.Gueltig, ["definitionsfehler"] = def.Fehler.Count,
                    ["bibliothek"] = (f.Eigenschaft("bibliothek")?.Inhalt as JsonObject)?["datei"]?.DeepClone(),
                    ["bibliothekStand"] = stand(f),
                };
            }).ToArray()),
        };
    }

    static JsonNode? Wert(FWert? w) => w is not { } x ? null : x.IstText ? JsonValue.Create(x.Text) : JsonValue.Create(Math.Round(x.Zahl, 9));

    public static JsonObject Familie(Projekt p, string bezug)
    {
        var f = Familien.Hole(p.Modell, bezug);
        var def = Familien.Definition(f, p.Katalog);
        var typen = new JsonArray();
        if (def.Gueltig)
            foreach (var t in def.Typen)
            {
                var a = FamilienAuswertung.Aufloesen(def, t.Name, null);
                typen.Add(new JsonObject
                {
                    ["name"] = t.Name, ["gueltig"] = a.Gueltig,
                    ["werte"] = new JsonObject(def.Parameter.Select(x => KeyValuePair.Create(x.Name, Wert(a.Wert(x.Name))))),
                    ["auswertungsfehler"] = new JsonArray(a.Fehler.Select(x => (JsonNode)x.AlsJson()).ToArray()),
                });
            }
        return new JsonObject
        {
            ["kennzeichen"] = f.Kennzeichen, ["name"] = f.Name, ["id"] = Familien.DefinitionsId(f), ["kategorie"] = f.Text("kategorie"),
            ["status"] = f.Text("status"), ["version"] = f.Zahl("version"),
            ["bibliothek"] = f.Eigenschaft("bibliothek")?.Inhalt?.DeepClone(), ["freigabe"] = f.Eigenschaft("freigabe")?.Inhalt?.DeepClone(),
            ["gueltig"] = def.Gueltig,
            ["definitionsfehler"] = new JsonArray(def.Fehler.Select(x => (JsonNode)x.AlsJson()).ToArray()),
            ["hinweise"] = new JsonArray(def.Hinweise.Select(x => (JsonNode)x.AlsJson()).ToArray()),
            ["typen"] = typen,
            ["verwendungen"] = new JsonArray(Verwender(p.Modell, f).Select(e => (JsonNode)JsonValue.Create(e.Kennzeichen)).ToArray()),
            ["definition"] = f.Eigenschaft("definition")?.Inhalt?.DeepClone(),
        };
    }

    /// <summary>Anschlusslagen eines Elements (Modul 3: alle Elemente mit Anschlüssen, Anschlusslagen.Von).</summary>
    public static JsonObject Anschlusslagen(Projekt p, string bezug)
    {
        var m = p.Modell;
        var e = m.Hole(bezug);
        if (e.Anschluesse.Count == 0)
            throw new HlksFehler($"{e.Kennzeichen} ({e.Klasse}) hat keine Anschlüsse.",
                "anschlusslagen gilt für Elemente mit Anschlüssen (Leitungen, Formteile, Armaturen, Geräte); element_lesen zeigt das Element.");
        var stellen = global::Hlks.Kern.Anschlusslagen.Von(m, p.Katalog, e);
        var leitung = p.Katalog.IstArt(e, "Leitung");
        var f = Familien.FamilieVon(m, e);
        var lage = f is null || leitung ? null : Familien.Einbaulage(e);
        string? hinweis = stellen.FirstOrDefault()?.Quelle switch
        {
            "ohne_familie" => $"{e.Kennzeichen} hat keine Familie: Anschlüsse ohne Lage, nur logisch verbindbar (verbinden prüft dann Medium und Fluss).",
            "nicht_platziert" => $"{e.Kennzeichen} ist nicht platziert (keine Geometrie): Anschlüsse ohne Lage.",
            "familie_ungueltig" => $"Die Familie von {e.Kennzeichen} lässt sich nicht auswerten: Anschlüsse ohne Lage." +
                (Familien.Aufloesung(m, p.Katalog, e, out var grund) is null && grund is not null ? " " + grund : ""),
            _ => null,
        };
        return new JsonObject
        {
            ["element"] = e.Kennzeichen, ["klasse"] = e.Klasse, ["familie"] = f?.Kennzeichen, ["typ"] = f is null ? null : Familien.Typ(e),
            ["einfuegepunkt"] = lage is null ? null : Json.Knoten(lage.Ursprung), ["drehung"] = lage is null ? null : (double?)Familien.Drehung(e),
            ["ausrichtung"] = lage?.AlsJson(), ["hinweis"] = hinweis,
            ["anschluesse"] = new JsonArray(stellen.Select(s =>
            {
                var v = m.VerbindungAn(e.Id, s.Name);
                var g = v?.Gegenueber(e.Id, s.Name);
                var o = global::Hlks.Kern.Anschlusslagen.AlsJson(s);
                o["verbunden"] = g is null ? null : $"{m.Finde(g.Element)?.Kennzeichen}.{g.Anschluss}";
                return (JsonNode)o;
            }).ToArray()),
        };
    }
}
