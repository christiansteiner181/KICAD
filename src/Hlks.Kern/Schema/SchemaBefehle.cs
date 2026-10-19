// Schema als verknüpfte Ansicht (Modul 3, Teil B3): Befehle schema_lage, schema_anordnen und (Nachtrag B8) schema_anlegen,
// Abfrage «schema», Prüfung P17. Einhängepunkte vom Hauptlauf (Phase A) in Befehle.cs, Abfragen.cs, Tuer.cs und
// Pruefungen.Alle; die Plangrafik steht in SchemaGrafik.cs, der Graph in SchemaGraph.cs, die Anordnung in SchemaAnordnung.cs.
//
// Bearbeiten im Schema geschieht mit den bestehenden Modellbefehlen (verbinden, trennen, element_anlegen …) – das Schema
// ist nur eine andere Sicht. Eigene Befehle braucht es nur für die Hand-Lagen (Ansicht.lagen), die einzigen Daten, die das
// Schema selbst speichert: { elementId: { x, y, manuell: true } } in Papier-mm der Schema-Ansicht, und für das Anlegen einer
// Schema-Ansicht aus einer Vorlage (je Gewerkgruppe ein Schema, Entscheid Christian 19.10.2026):
//   wasser    Systeme mit dem Medium der Systemtypen Trinkwasser kalt, Trinkwasser warm, Zirkulation («Schema Wasser»)
//   abwasser  Systeme mit dem Medium der Systemtypen Schmutzwasser, Regenwasser («Schema Abwasser»)
// Das Medium eines Systems kommt aus dem Modell (System.medium, vom Systemtyp gesetzt), sonst aus dem Katalog (Systemtyp →
// Medium); Kennzeichen werden nicht gedeutet. Die Vorlage schreibt die Systeme fest in Ansicht.systeme – später angelegte
// Systeme kommen mit element_aendern dazu.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class SchemaBefehle
{
    // Parameterbeschreibungen (auch für die MCP-Werkzeuge in Hlks.Mcp/WerkzeugeSchema.cs)
    public const string ParAnsicht = "Schema-Ansicht (Kennzeichen oder Id, art schema)";
    public const string ParElement = "Knoten des Schemas (Kennzeichen oder Id)";
    public const string ParX = "x in Papier-mm der Schema-Ansicht (nach rechts)";
    public const string ParY = "y in Papier-mm der Schema-Ansicht (nach oben)";
    public const string ParZuruecksetzen = "true = Hand-Lage entfernen, es gilt wieder die automatische Anordnung";
    public const string ParAlle = "true = alle Hand-Lagen entfernen, sonst nur die verwaisten";
    public const string ParVorlage = "wasser (Trinkwasser kalt, warm, Zirkulation) | abwasser (Schmutz- und Regenwasser)";
    public const string ParName = "Name der Ansicht (Vorgabe «Schema Wasser» bzw. «Schema Abwasser»)";
    public const string ParKennzeichen = "Kennzeichen der Ansicht (Vorgabe AN-SCHEMA-WASSER bzw. AN-SCHEMA-ABWASSER, falls frei)";
    public const string ParBlickrichtung = "sued | nord | ost | west: Blick von dieser Seite (Projekt-Nord), Vorgabe sued";
    public const string ParWohnungsbloecke = "true = Endgeräte einer Zone (Wohnung) je Geschoss als Block mit Apparatetabelle";

    /// <summary>Vorlagen: Name der Vorlage → Systemtypen der Gewerkgruppe (Katalog systemtypen.json).</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Vorlagen = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["wasser"] = ["Trinkwasser kalt", "Trinkwasser warm", "Zirkulation"],
        ["abwasser"] = ["Schmutzwasser", "Regenwasser"],
    };

    /// <summary>Registriert die Schemabefehle (schema_lage, schema_anordnen, schema_anlegen).</summary>
    public static void Registrieren(Befehlsregister r)
    {
        r.Hinzufuegen(new SchemaLage());
        r.Hinzufuegen(new SchemaAnordnen());
        r.Hinzufuegen(new SchemaAnlegen());
    }

    /// <summary>Medium eines Systems: System.medium im Modell, sonst das Medium seines Systemtyps aus dem Katalog.</summary>
    public static string? MediumVon(Katalog k, Element system) =>
        system.Text("medium") is { Length: > 0 } m ? m
        : system.Text("systemtyp") is { } typ && k.Systemtypen.TryGetValue(typ, out var st) ? st.Medium : null;

    /// <summary>Systeme des Modells, die zu einer Vorlage gehören (nach dem Medium), nach Kennzeichen.</summary>
    public static List<Element> SystemeDerVorlage(Modell m, Katalog k, string vorlage)
    {
        if (!Vorlagen.TryGetValue(vorlage, out var typen))
            throw new HlksFehler($"Vorlage «{vorlage}» gibt es nicht.", "Vorlagen: " + string.Join(", ", Vorlagen.Keys));
        var medien = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in typen)
            if (k.Systemtypen.TryGetValue(t, out var st)) medien.Add(st.Medium);
            else throw new HlksFehler($"Systemtyp «{t}» fehlt im Katalog.", "Katalog systemtypen.json prüfen (Hauptlauf).");
        return m.DerKlasse("System").Where(s => MediumVon(k, s) is { } md && medien.Contains(md)).OrderBy(s => s, SchemaGraph.Ordnung).ToList();
    }

    /// <summary>Schema-Ansicht aus dem Parameter «ansicht» (in der Arbeitskopie), sonst Ablehnung mit Vorschlag.</summary>
    internal static Element Ansicht(Kontext k, JsonObject p)
    {
        var a = k.Arbeit.Hole(Param.Text(p, "ansicht", true), "Ansicht");
        SchemaGraph.SchemaAnsichtPruefen(k.Arbeit, a);
        return a;
    }

    /// <summary>Rohe Hand-Lagen der Ansicht (Kopie) oder ein leeres Objekt.</summary>
    internal static JsonObject Lagen(Element ansicht) =>
        ansicht.Eigenschaft("lagen")?.Inhalt is JsonObject o ? (JsonObject)o.DeepClone() : new JsonObject();

    /// <summary>Schreibt die Hand-Lagen (nach Id geordnet, damit gleiche Lagen gleichen Text und gleiche Prüfsumme geben);
    /// leer = Eigenschaft entfernen.</summary>
    internal static void LagenSetzen(Kontext k, Element ansicht, JsonObject lagen)
    {
        var e = ansicht.Kopie();
        if (lagen.Count == 0) e.Eigenschaften.Remove("lagen");
        else
        {
            var geordnet = new JsonObject(lagen.OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => KeyValuePair.Create(x.Key, x.Value?.DeepClone())));
            e.Eigenschaften["lagen"] = new Wert { Inhalt = geordnet, Herkunft = k.Herkunft };
        }
        k.Setzen(e);
    }

    /// <summary>Warum ein Element kein Knoten des Schemas ist, mit Weg zur Lösung.</summary>
    internal static string NichtImSchema(SchemaGraph g, Element e)
    {
        if (!SchemaGraph.IstNetzelement(g.Katalog, e))
            return $"Im Schema stehen nur Netzelemente (Endgeräte, Geräte, Armaturen, Formteile, Leitungen); {e.Kennzeichen} ist ein {e.Klasse}.";
        if (g.Ausgeblendet(e) is { } grund)
            return grund + " – Filter der Ansicht anpassen (element_aendern mit eigenschaften gewerke bzw. klassenAusblenden).";
        if (SchemaGraph.IstLinienelement(g.Katalog, e))
        {
            var l = g.LinieVon.GetValueOrDefault(e.Id);
            var enden = l is null ? "" : string.Join(" bzw. ", new[] { l.Von, l.Nach }.Where(x => x.Knoten is not null).Select(x => x.Knoten!.Kennzeichen));
            return $"{e.Kennzeichen} ({e.Klasse}) ist Teil einer Schemalinie; Linien folgen ihren Endknoten" + (enden.Length > 0 ? $" – {enden} verschieben." : ".");
        }
        return $"{e.Kennzeichen} steht nicht im Schema.";
    }
}

public sealed class SchemaLage : IBefehl
{
    public string Name => "schema_lage";
    public const string Text = "Setzt die Lage eines Knotens im Schema von Hand (x, y in Papier-mm der Schema-Ansicht, y nach oben); sie bleibt, auch wenn sich das Modell ändert. Mit zuruecksetzen = true gilt wieder die automatische Anordnung. Knoten sind Endgeräte, Geräte, Armaturen, T-Stücke und Formteile; Leitungen, Bögen und Reduktionen folgen ihren Endknoten und werden orthogonal neu geführt.";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("ansicht", "text", true, SchemaBefehle.ParAnsicht),
        new("element", "text", true, SchemaBefehle.ParElement),
        new("x", "zahl", false, SchemaBefehle.ParX),
        new("y", "zahl", false, SchemaBefehle.ParY),
        new("zuruecksetzen", "bool", false, SchemaBefehle.ParZuruecksetzen),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var ansicht = SchemaBefehle.Ansicht(k, p);
        var e = k.Arbeit.Hole(Param.Text(p, "element", true));
        var lagen = SchemaBefehle.Lagen(ansicht);
        if (Param.Bool(p, "zuruecksetzen"))
        {
            if (!lagen.Remove(e.Id))
            {
                k.Hinweise.Add($"{e.Kennzeichen} hat in {ansicht.Kennzeichen} keine Hand-Lage; es gilt schon die automatische Anordnung.");
                return;
            }
            SchemaBefehle.LagenSetzen(k, ansicht, lagen);
            k.Hinweise.Add($"{e.Kennzeichen}: Hand-Lage entfernt, es gilt die automatische Anordnung.");
            return;
        }
        var g = SchemaGraph.Bilden(k.Projekt, ansicht, k.Arbeit);
        if (!g.NachId.ContainsKey(e.Id))
            throw new HlksFehler($"{e.Kennzeichen} ist kein Knoten im Schema {ansicht.Kennzeichen}.", SchemaBefehle.NichtImSchema(g, e));
        if (Param.Zahl(p, "x") is not double x || Param.Zahl(p, "y") is not double y)
            throw new HlksFehler("x und y fehlen.", "Lage in Papier-mm angeben ({ x, y }, y nach oben) – oder zuruecksetzen: true für die automatische Anordnung.");
        if (!double.IsFinite(x) || !double.IsFinite(y) || Math.Abs(x) > 1e6 || Math.Abs(y) > 1e6)
            throw new HlksFehler($"Lage ({x}, {y}) ist ungültig.", "x und y als endliche Zahlen in Papier-mm angeben (Betrag höchstens 1 000 000).");
        var neu = new JsonObject { ["x"] = Math.Round(x, 2), ["y"] = Math.Round(y, 2), ["manuell"] = true };
        if (lagen[e.Id] is JsonObject alt && alt.ToJsonString() == neu.ToJsonString())
        {
            k.Hinweise.Add("Keine Änderung.");
            return;
        }
        lagen[e.Id] = neu;
        SchemaBefehle.LagenSetzen(k, ansicht, lagen);
    }
}

public sealed class SchemaAnordnen : IBefehl
{
    public string Name => "schema_anordnen";
    public const string Text = "Ordnet ein Schema neu an: die automatische Anordnung wird bei jedem Lesen gerechnet; dieser Befehl räumt die Hand-Lagen auf (entfernt die Lagen gelöschter und nicht mehr sichtbarer Elemente). Mit alle = true werden alle Hand-Lagen entfernt – das ganze Schema ist wieder automatisch angeordnet.";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("ansicht", "text", true, SchemaBefehle.ParAnsicht),
        new("alle", "bool", false, SchemaBefehle.ParAlle),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var ansicht = SchemaBefehle.Ansicht(k, p);
        var lagen = SchemaBefehle.Lagen(ansicht);
        if (lagen.Count == 0)
        {
            k.Hinweise.Add($"{ansicht.Kennzeichen} hat keine Hand-Lagen; das Schema ist ganz automatisch angeordnet.");
            return;
        }
        if (Param.Bool(p, "alle"))
        {
            int n = lagen.Count;
            SchemaBefehle.LagenSetzen(k, ansicht, new JsonObject());
            k.Hinweise.Add($"{n} Hand-Lage(n) entfernt; das Schema ist wieder automatisch angeordnet.");
            return;
        }
        var gueltig = Schemaplan.Lagen(ansicht);
        var g = SchemaGraph.Bilden(k.Projekt, ansicht, k.Arbeit);
        var weg = lagen.Select(x => x.Key).Where(id => !gueltig.ContainsKey(id) || !g.NachId.ContainsKey(id)).ToList();
        if (weg.Count == 0)
        {
            k.Hinweise.Add($"Alle {lagen.Count} Hand-Lage(n) gehören zu Knoten im Schema; nichts aufzuräumen.");
            return;
        }
        foreach (var id in weg) lagen.Remove(id);
        SchemaBefehle.LagenSetzen(k, ansicht, lagen);
        var namen = weg.Select(id => k.Arbeit.Finde(id)?.Kennzeichen ?? $"{id} (gelöscht)").Order(StringComparer.OrdinalIgnoreCase);
        k.Hinweise.Add($"{weg.Count} Hand-Lage(n) entfernt (Element gelöscht oder nicht mehr im Schema): {string.Join(", ", namen)}.");
    }
}

public sealed class SchemaAnlegen : IBefehl
{
    public string Name => "schema_anlegen";
    public const string Text = "Legt eine Schema-Ansicht (Strangschema) aus einer Vorlage an: wasser = Systeme Trinkwasser kalt, warm und Zirkulation («Schema Wasser»), abwasser = Schmutz- und Regenwasser («Schema Abwasser»). Die Vorlage setzt systeme auf die Systeme dieser Medien im Modell; dazu wahlweise Blickrichtung und Wohnungsblöcke. Das Schema ist strangweise angeordnet: je Steigzone (Strang.steigzone) ein Bündel mit Kreis, Stränge nach Lage im Gebäude, Endgeräte nach Lage im Netz.";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("vorlage", "text", true, SchemaBefehle.ParVorlage),
        new("name", "text", false, SchemaBefehle.ParName),
        new("kennzeichen", "text", false, SchemaBefehle.ParKennzeichen),
        new("blickrichtung", "text", false, SchemaBefehle.ParBlickrichtung),
        new("wohnungsbloecke", "bool", false, SchemaBefehle.ParWohnungsbloecke),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var vorlage = Param.Text(p, "vorlage", true)!.Trim().ToLowerInvariant();
        var systeme = SchemaBefehle.SystemeDerVorlage(k.Arbeit, k.Katalog, vorlage);
        var typen = SchemaBefehle.Vorlagen[vorlage];
        if (systeme.Count == 0)
            throw new HlksFehler($"Im Modell gibt es kein System für die Vorlage {vorlage} ({string.Join(", ", typen)}).",
                $"Zuerst ein System anlegen, z. B. element_anlegen {{ klasse: System, eigenschaften: {{ systemtyp: {typen[0]} }} }}.");
        string titel = vorlage == "wasser" ? "Wasser" : "Abwasser";
        var kz = Param.Text(p, "kennzeichen")?.Trim();
        if (string.IsNullOrEmpty(kz))
        {
            var vorgabe = "AN-SCHEMA-" + titel.ToUpperInvariant();
            kz = k.Arbeit.Finde(vorgabe) is null ? vorgabe : null;
        }
        var eig = new JsonObject
        {
            ["art"] = "schema",
            ["schemaart"] = "strangschema",
            ["systeme"] = new JsonArray(systeme.Select(s => (JsonNode)JsonValue.Create(s.Kennzeichen)).ToArray()),
        };
        if (Param.Text(p, "blickrichtung")?.Trim().ToLowerInvariant() is { Length: > 0 } blick)
        {
            if (!Schemaplan.Blickrichtungen.Contains(blick))
                throw new HlksFehler($"Blickrichtung «{blick}» gibt es nicht.", "Blickrichtung: " + string.Join(", ", Schemaplan.Blickrichtungen) + " (Projekt-Nord).");
            eig["blickrichtung"] = blick;
        }
        if (Param.Bool(p, "wohnungsbloecke")) eig["wohnungsbloecke"] = true;
        var anlegen = new JsonObject
        {
            ["klasse"] = "Ansicht",
            ["name"] = Param.Text(p, "name")?.Trim() is { Length: > 0 } n ? n : "Schema " + titel,
            ["eigenschaften"] = eig,
        };
        if (kz is not null) anlegen["kennzeichen"] = kz;
        new ElementAnlegen().Ausfuehren(k, anlegen);
        var a = k.Arbeit.Finde(k.Operationen[^1].Id)!;
        var ohne = typen.Where(t => k.Katalog.Systemtypen.TryGetValue(t, out var st)
            && !systeme.Any(s => string.Equals(SchemaBefehle.MediumVon(k.Katalog, s), st.Medium, StringComparison.OrdinalIgnoreCase))).ToList();
        k.Hinweise.Add($"Schema-Ansicht {a.Kennzeichen} «{a.Name}» angelegt: Systeme {string.Join(", ", systeme.Select(s => s.Kennzeichen))}"
            + (ohne.Count > 0 ? $"; ohne System im Modell: {string.Join(", ", ohne)} (später mit element_aendern in systeme ergänzen)" : "") + ".");
    }
}

public static class SchemaPruefung
{
    /// <summary>P17 (Code in Pruefungen.Codes): Netzelement ohne Geometrie steht nur im Schema (nicht platziert) – Hinweis.</summary>
    public static void Pruefen(Modell m, Katalog k, List<Befund> b)
    {
        foreach (var e in m.Elemente)
            if (e.Geometrie is null && SchemaGraph.IstNetzelement(k, e))
                b.Add(new("P17", "hinweis", e.Kennzeichen, $"{e.Klasse} steht nur im Schema (nicht platziert)",
                    "Im Grundriss platzieren: element_aendern mit geometrie bzw. familie_platzieren"));
    }
}

public static class SchemaAbfragen
{
    public const string SchemaText = "Das Schema einer Ansicht (art schema, Strangschema): Knoten mit Lage in Papier-mm (y nach oben; manuell = von Hand gesetzt, platziert = hat Geometrie im Modell, symbol), Schemalinien (Leitungen, Bögen, Reduktionen zwischen zwei Knoten: elemente in Fliessrichtung, von, nach, punkte, groessen, medium; offene Enden), Gruppen (je Steigzone ein Bündel bzw. je Strang, nach Lage im Gebäude, mit Kreis), Spalten (je Stranglinie) und Zeilen (je Geschoss), Wohnungsblöcke (Apparatetabelle, Anschlussarmaturen), Dämmungstexte, Hinweise. Die Anordnung wird bei jedem Lesen gerechnet; Hand-Lagen aus schema_lage bleiben. Bearbeiten mit den Modellbefehlen (verbinden, trennen, element_anlegen …).";

    /// <summary>Abfragen zum Schema für Abfragen.Liste.</summary>
    public static readonly IReadOnlyList<AbfrageDef> Definitionen =
    [
        new("schema", SchemaText, [new("ansicht", "text", true, SchemaBefehle.ParAnsicht)]),
    ];

    /// <summary>Beantwortet eine Schemaabfrage; null = nicht zuständig.</summary>
    public static object? Tuer(Projekt p, string name, JsonObject par) => name switch
    {
        "schema" => Schema(p, Param.Text(par, "ansicht", true)!),
        _ => null,
    };

    public static JsonObject Schema(Projekt p, string ansicht) => Schemaplan.Rechnen(p, ansicht).AlsJson();
}
