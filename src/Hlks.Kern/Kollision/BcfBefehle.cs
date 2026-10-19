// BCF 3.0 (Modul 3b, B6): Befehl bcf_import, Abfragen bcf_export (BcfExport.cs) und bcf_lesen. Registriert in
// KollisionBefehle.Registrieren bzw. KollisionAbfragen (Definitionen, Tuer). Texte stehen hier, damit MCP-Werkzeuge
// (src/Hlks.Mcp/WerkzeugeBcf.cs) dieselben Beschreibungen benutzen.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class BcfTexte
{
    public const string Export = "Schreibt Kollisionen als BCF 3.0 (BIM Collaboration Format, buildingSMART) für Architekt und Fachplaner: je Kollision ein Topic " +
        "(Guid fest aus der Kollisionskennung – derselbe Befund bleibt über Prüfläufe dasselbe Topic) mit Typ (hart/weich), Status, Titel (Beteiligte), " +
        "Beschreibung (Art, Mass, Lage, Prüflauf), Kommentaren und einem Viewpoint (Kamera schräg von oben auf die Lage, Beteiligte als Auswahl: IFC-GlobalId " +
        "bzw. KICAD-Id). Koordinaten in m: Vorgabe die IFC-Welt über den Projektbezug (ohne Projektbezug Projektkoordinaten von KICAD), beide wählbar. " +
        "Jede XML-Datei wird vor dem Schreiben gegen die offiziellen Schemas geprüft. Ändert das Modell nicht. " +
        "Antwort: datei, Anzahl Topics, Zuordnung Kollision → Topic.";
    public const string Lesen = "Liest ein BCF-3.0-Zip, ohne etwas zu ändern: Topics mit Typ, Status, Titel, Beschreibung, Kommentaren und Viewpoints (Kamera in m, " +
        "Komponenten), dazu – falls im Projekt vorhanden – die zugehörige Kollision und den Status, den bcf_import setzen würde. Vor bcf_import aufrufen.";
    public const string Import = "Übernimmt Rückmeldungen aus einer BCF-3.0-Datei (z. B. vom Architekten): Topics werden über ihre Guid den Kollisionen zugeordnet, " +
        "der Status wird übernommen (Abbildung: Neu, Bestätigt, Akzeptiert, Erledigt, Behoben; übliche englische Bezeichnungen werden verstanden), neue Kommentare " +
        "werden mit Datum und Autor an den Kommentar der Kollision angehängt. Unbekannte Topics werden nur aufgelistet, nicht angelegt. Ein Paket, rückgängig " +
        "machbar – zuerst als Trockenlauf ansehen.";

    public const string ParDateiExport = "Zieldatei (.bcf oder .bcfzip); eine vorhandene Datei wird ersetzt";
    public const string ParDateiLesen = "Pfad der BCF-Datei (.bcf oder .bcfzip)";
    public const string ParKollisionen = "Nur diese Kollisionen (Kennzeichen oder Id), z. B. [\"KOL-1\", \"KOL-4\"]; Vorgabe alle";
    public const string ParStatus = "Nur Kollisionen mit diesem Status (neu, bestaetigt, akzeptiert, erledigt, behoben) als Liste oder Text; Vorgabe alle";
    public const string ParArt = "Nur hart oder weich; Vorgabe beide";
    public const string ParAutor = "Autor im BCF (Topics, eigene Kommentare), z. B. die E-Mail-Adresse; Vorgabe der Bearbeiter, der die Kollision angelegt bzw. geändert hat";
    public const string ParKoordinaten = "ifc (Vorgabe, wenn das Projekt einen Projektbezug hat: IFC-Weltkoordinaten über den Projektbezug, damit die Kamera im Modell des Partners stimmt) | " +
        "projekt (Projektkoordinaten von KICAD; Vorgabe ohne Projektbezug); immer in m";
}

public static class BcfAbfragen
{
    public static readonly IReadOnlyList<AbfrageDef> Definitionen =
    [
        new("bcf_export", BcfTexte.Export,
        [
            new("datei", "text", true, BcfTexte.ParDateiExport),
            new("kollisionen", "liste", false, BcfTexte.ParKollisionen),
            new("status", "liste", false, BcfTexte.ParStatus),
            new("art", "text", false, BcfTexte.ParArt),
            new("autor", "text", false, BcfTexte.ParAutor),
            new("koordinaten", "text", false, BcfTexte.ParKoordinaten),
        ]),
        new("bcf_lesen", BcfTexte.Lesen, [new("datei", "text", true, BcfTexte.ParDateiLesen)]),
    ];

    /// <summary>Beantwortet bcf_export und bcf_lesen; null = nicht zuständig.</summary>
    public static object? Tuer(Projekt p, string name, JsonObject par) => name switch
    {
        "bcf_export" => BcfExport.Schreiben(p, par),
        "bcf_lesen" => BcfLesen.Abfrage(p, Param.Text(par, "datei", true)!),
        _ => null,
    };
}

/// <summary>Abfrage bcf_lesen: Inhalt eines BCF-Zips als JSON, mit Zuordnung zu den Kollisionen des Projekts.</summary>
public static class BcfLesen
{
    public static JsonObject Abfrage(Projekt p, string datei)
    {
        var inhalt = BcfLeser.Lesen(datei, p.Katalog);
        var nachGuid = BcfFormat.TopicGuids(p.Kopf.Id, p.Modell, p.Katalog).ToDictionary(x => x.Value, x => x.Key);
        var aufl = new BcfAufloesung(p, p.Modell);
        int zugeordnet = 0;
        var topics = new JsonArray();
        foreach (var t in inhalt.Topics)
        {
            var kol = nachGuid.TryGetValue(t.Guid, out var id) ? p.Modell.Finde(id) : null;
            if (kol is not null) zugeordnet++;
            var statusKicad = BcfFormat.StatusKicad(t.Status);
            var bekannt = kol is null ? new HashSet<Guid>()
                : BcfKommentare.Zerlegen(kol.Text("kommentar")).Uebernommen.Select(c => c.Guid).Append(BcfFormat.KommentarGuid(t.Guid)).ToHashSet();
            topics.Add(new JsonObject
            {
                ["guid"] = BcfFormat.Text(t.Guid),
                ["titel"] = t.Titel,
                ["typ"] = t.Typ,
                ["status"] = t.Status,
                ["prioritaet"] = t.Prioritaet,
                ["labels"] = Arr(t.Labels),
                ["erstellt"] = t.Erstellt?.ToString("o"),
                ["autor"] = t.Autor,
                ["geaendert"] = t.Geaendert?.ToString("o"),
                ["geaendertVon"] = t.GeaendertVon,
                ["faellig"] = t.Faellig?.ToString("o"),
                ["zugewiesen"] = t.Zugewiesen,
                ["phase"] = t.Phase,
                ["beschreibung"] = t.Beschreibung,
                ["dateien"] = Arr(t.Dateien),
                ["kollision"] = kol?.Kennzeichen,
                ["statusKicad"] = kol?.Text("status") ?? (kol is null ? null : "neu"),
                ["statusNachImport"] = kol is null ? null : statusKicad ?? $"unverändert (Status «{t.Status}» unbekannt)",
                ["kommentare"] = new JsonArray(t.Kommentare.Select(c => (JsonNode)new JsonObject
                {
                    ["guid"] = BcfFormat.Text(c.Guid), ["datum"] = c.Datum?.ToString("o"), ["autor"] = c.Autor, ["text"] = c.Text,
                    ["viewpoint"] = c.Viewpoint is { } cv ? BcfFormat.Text(cv) : null,
                    ["neu"] = kol is null ? null : !bekannt.Contains(c.Guid),
                }).ToArray()),
                ["viewpoints"] = new JsonArray(t.Viewpoints.Select(v => (JsonNode)new JsonObject
                {
                    ["guid"] = BcfFormat.Text(v.Guid), ["datei"] = v.Datei, ["snapshot"] = v.Snapshot,
                    ["kamera"] = v.Kamera is not { } k ? null : new JsonObject
                    {
                        ["art"] = k.Art, ["standpunkt_m"] = Arr(k.Standpunkt), ["richtung"] = Arr(k.Richtung), ["oben"] = Arr(k.Oben),
                        ["sichtfeld_grad"] = k.Sichtfeld, ["sichthoehe_m"] = k.Hoehe, ["seitenverhaeltnis"] = k.Seitenverhaeltnis,
                    },
                    ["komponenten"] = new JsonArray(v.Auswahl.Select(c => (JsonNode)new JsonObject
                    {
                        ["ifcGuid"] = c.IfcGuid, ["system"] = c.Ursprung, ["id"] = c.AuthoringToolId,
                        ["element"] = ElementZu(p, aufl, c)?.Kennzeichen,
                    }).ToArray()),
                }).ToArray()),
            });
        }
        return new JsonObject
        {
            ["datei"] = inhalt.Datei,
            ["bcfVersion"] = inhalt.Version,
            ["projekt"] = inhalt.ProjektId is null ? null : new JsonObject { ["id"] = inhalt.ProjektId, ["name"] = inhalt.ProjektName, ["diesesProjekt"] = inhalt.ProjektId == p.Kopf.Id },
            ["erweiterungen"] = new JsonObject(inhalt.Erweiterungen.Select(x => KeyValuePair.Create(x.Key, (JsonNode?)Arr(x.Value)))),
            ["schemapruefung"] = inhalt.SchemaGeprueft ? (inhalt.Schemaabweichungen == 0 ? "bestanden" : $"{inhalt.Schemaabweichungen} Abweichung(en), siehe hinweise") : "übersprungen",
            ["anzahlTopics"] = inhalt.Topics.Count,
            ["zugeordnet"] = zugeordnet,
            ["unbekannt"] = inhalt.Topics.Count - zugeordnet,
            ["statusabbildung"] = BcfFormat.StatusTabelle(),
            ["topics"] = topics,
            ["hinweise"] = Arr(inhalt.Hinweise),
        };
    }

    static Element? ElementZu(Projekt p, BcfAufloesung aufl, BcfKomponente c)
    {
        if (c.IfcGuid is { } g && aufl.ElementZuGlobalId(g) is { } e) return e;
        if (c.AuthoringToolId is { } a && string.Equals(c.Ursprung, BcfFormat.Ursprungssystem, StringComparison.OrdinalIgnoreCase)) return p.Modell.Finde(a);
        return null;
    }

    static JsonArray Arr(IEnumerable<string> s) => new(s.Select(x => (JsonNode)JsonValue.Create(x)!).ToArray());
    static JsonArray? Arr(double[]? v) => v is null ? null : new JsonArray(v.Select(x => (JsonNode)JsonValue.Create(Math.Round(x, 6))).ToArray());
}

/// <summary>Befehl bcf_import: Rückmeldungen (Status, neue Kommentare) eines BCF-3.0-Zips in die Kollisionen übernehmen.</summary>
public sealed class BcfImport : IBefehl
{
    public string Name => "bcf_import";
    public string Beschreibung => BcfTexte.Import;
    public IReadOnlyList<ParameterDef> Parameter { get; } = [new("datei", "text", true, BcfTexte.ParDateiLesen)];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var inhalt = BcfLeser.Lesen(Param.Text(p, "datei", true)!, k.Katalog);
        var name = Path.GetFileName(inhalt.Datei);
        var nachGuid = BcfFormat.TopicGuids(k.Projekt.Kopf.Id, k.Arbeit, k.Katalog).ToDictionary(x => x.Value, x => x.Key);
        var erlaubt = k.Katalog.Klasse("Kollision").Eigenschaften.FirstOrDefault(x => x.Name == "status")?.Auswahl ?? [];
        var meldungen = new List<string>();
        var unbekannt = new List<string>();
        int zugeordnet = 0, statusGesetzt = 0, kommentareNeu = 0;

        foreach (var t in inhalt.Topics)
        {
            if (!nachGuid.TryGetValue(t.Guid, out var id) || k.Arbeit.Finde(id) is not { } alt)
            {
                unbekannt.Add($"Unbekanntes Topic {BcfFormat.Text(t.Guid)} «{t.Titel ?? "ohne Titel"}» (Status {t.Status ?? "–"}, {t.Kommentare.Count} Kommentar(e)) – nicht angelegt.");
                continue;
            }
            zugeordnet++;
            var neu = alt.Kopie();
            var aenderung = new List<string>();
            var herkunft = k.Herkunft with { Art = "import", Quelle = $"BCF «{name}», Topic {BcfFormat.Text(t.Guid)}" };

            // Status (Abbildung BcfFormat.StatusEingabe, ANNAHME); fehlender Status in KICAD gilt als «neu» wie beim Export
            if (t.Status is { } st)
            {
                var s = BcfFormat.StatusKicad(st);
                var vorher = alt.Text("status") ?? "neu";
                if (s is null || !erlaubt.Contains(s))
                    meldungen.Add($"{alt.Kennzeichen}: Status «{st}» ist unbekannt – nicht übernommen (bekannt: {string.Join(", ", BcfFormat.StatusAusgabe.Select(x => x.Bcf))}).");
                else if (s != vorher)
                {
                    neu.Eigenschaften["status"] = new Wert { Inhalt = JsonValue.Create(s), Herkunft = herkunft };
                    aenderung.Add($"Status {vorher} → {s} (BCF «{st}»)");
                    statusGesetzt++;
                }
            }

            // Neue Kommentare: weder der eigene (feste Guid) noch schon übernommene (Guid im Kommentarblock)
            var kommentar = alt.Text("kommentar");
            var bekannt = BcfKommentare.Zerlegen(kommentar).Uebernommen.Select(c => c.Guid).Append(BcfFormat.KommentarGuid(t.Guid)).ToHashSet();
            var neue = t.Kommentare.Where(c => bekannt.Add(c.Guid)).ToList();   // Add: gleiche Guid zweimal im Topic nur einmal
            var ohneText = neue.Count(c => string.IsNullOrWhiteSpace(c.Text));
            neue = neue.Where(c => !string.IsNullOrWhiteSpace(c.Text)).OrderBy(c => c.Datum ?? DateTimeOffset.MaxValue).ToList();
            if (neue.Count > 0)
            {
                var text = BcfKommentare.Anhaengen(kommentar, neue.Select(c => c with { Text = BcfFormat.Sauber(c.Text) }), herkunft.Zeit);
                neu.Eigenschaften["kommentar"] = new Wert { Inhalt = JsonValue.Create(text), Herkunft = herkunft };
                aenderung.Add($"{neue.Count} Kommentar(e) angehängt ({string.Join("; ", neue.Select(c =>
                    $"{c.Autor ?? "unbekannt"}, {(c.Datum ?? herkunft.Zeit).ToLocalTime().ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture)}"))})");
                kommentareNeu += neue.Count;
            }
            if (ohneText > 0) meldungen.Add($"{alt.Kennzeichen}: {ohneText} Kommentar(e) ohne Text übergangen.");

            if (aenderung.Count > 0)
            {
                k.Setzen(neu);
                meldungen.Add($"{alt.Kennzeichen}: {string.Join("; ", aenderung)}");
            }
        }

        k.Hinweise.Add($"BCF {inhalt.Version} «{name}»: {inhalt.Topics.Count} Topic(s), {zugeordnet} zugeordnet, {unbekannt.Count} unbekannt; " +
                       $"{statusGesetzt} Status übernommen, {kommentareNeu} Kommentar(e) angehängt.");
        if (inhalt.ProjektId is { } pid && pid != k.Projekt.Kopf.Id)
            k.Hinweise.Add($"Die BCF-Datei nennt ein anderes Projekt ({inhalt.ProjektName ?? pid}) – nur Topics mit passender Guid werden zugeordnet.");
        k.Hinweise.AddRange(meldungen);
        k.Hinweise.AddRange(unbekannt);
        k.Hinweise.AddRange(inhalt.Hinweise.Select(h => "Datei: " + h));
    }
}
