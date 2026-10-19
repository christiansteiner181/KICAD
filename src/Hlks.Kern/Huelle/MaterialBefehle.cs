// Befehle für Materialien der Gebäudehülle (Modul 1c, B2): material_anlegen, material_aendern, material_uebernehmen.
// Entscheid: material_aendern ist ein eigener Befehl und nicht nur element_aendern. element_aendern trägt die Quelle zwar
// mit, verlangt sie aber nicht; hier ist sie Pflicht (Grundsatz «kein Kennwert ohne Quelle»). Die Nachführung weist
// ausserdem auf Kennwerte hin, die über andere Wege ohne Quelle ins Modell kamen.

using System.Text.Json.Nodes;
using static Hlks.Kern.HuelleHilfe;

namespace Hlks.Kern;

static class MaterialWerte
{
    public const string Beschreibung = "Kennwerte { lambda | rohdichte | waermekapazitaet | emission | absorption | mu: { wert, quelle } }; jeder Wert braucht eine Quelle";

    /// <summary>Wendet die Kennwerte an. Gibt zurück, ob sich etwas geändert hat. null als Wert entfernt den Kennwert (nur beim Ändern).</summary>
    public static bool Anwenden(Kontext k, Element e, JsonObject? werte, bool entfernenErlaubt)
    {
        if (werte is null) return false;
        bool geaendert = false;
        foreach (var (key, roh) in werte)
        {
            var feld = MaterialBibliothek.Kennwerte.FirstOrDefault(x => x.Name.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (feld.Name is null)
                throw new HlksFehler($"Kennwert «{key}» gibt es für Material nicht.", "Erlaubt: " + string.Join(", ", MaterialBibliothek.Kennwerte.Select(x => x.Name)));
            if (roh is null)
            {
                if (!entfernenErlaubt) continue;
                geaendert |= e.Eigenschaften.Remove(feld.Name);
                continue;
            }
            var (wert, quelle) = WertMitQuelle(roh, feld.Name);
            var einheit = Einheit(k.Katalog, "Material", feld.Name);
            Pruefen(feld.Name, wert, feld.Min, feld.MinEin, feld.Max, einheit);
            var alt = e.Eigenschaft(feld.Name);
            if (alt is not null && GleicheZahl(alt.AlsZahl(), wert) && Quelle(alt) == quelle) continue;
            e.Eigenschaften[feld.Name] = Neu(k, JsonValue.Create(wert), einheit, quelle);
            geaendert = true;
        }
        return geaendert;
    }

    public static JsonObject? Lesen(JsonObject p)
    {
        if (p["werte"] is null) return null;
        return p["werte"] as JsonObject ?? throw new HlksFehler("Parameter «werte» muss ein Objekt sein.",
            "Form: { \"lambda\": { \"wert\": Zahl, \"quelle\": \"…\" } }");
    }

    public static void NameFrei(Kontext k, string name, string? ausser)
    {
        var doppelt = k.Arbeit.DerKlasse("Material").FirstOrDefault(x => x.Id != ausser && string.Equals(x.Name?.Trim(), name, StringComparison.OrdinalIgnoreCase));
        if (doppelt is not null)
            throw new HlksFehler($"Ein Material «{name}» gibt es schon ({doppelt.Kennzeichen}).",
                $"Mit material_aendern {doppelt.Kennzeichen} ergänzen oder einen anderen Namen wählen.");
    }
}

public sealed class MaterialAnlegen : IBefehl
{
    public string Name => "material_anlegen";
    public string Beschreibung => "Legt ein Material der Gebäudehülle an (Kennzeichen MAT-n). Jeder Kennwert (lambda, rohdichte, waermekapazitaet, emission, absorption, mu) braucht eine Quelle (Norm mit Ausgabe und Tabelle, Datenblatt, IFC, Angabe von Christian); ohne Quelle wird abgelehnt. Der Kern setzt keine Werte aus dem Gedächtnis.";
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("name", "text", true, "Bezeichnung des Materials (im Projekt eindeutig)"),
        new("kategorie", "text", false, "Gruppe, z. B. Beton, Mauerwerk, Dämmstoff, Holz, Putz"),
        new("werte", "objekt", false, MaterialWerte.Beschreibung),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var name = Sauber(Param.Text(p, "name", true)) ?? throw new HlksFehler("Parameter «name» ist leer.", "Bezeichnung des Materials angeben.");
        MaterialWerte.NameFrei(k, name, null);
        var d = k.Katalog.Klasse("Material");
        var e = new Element { Id = Guid.NewGuid().ToString(), Kennzeichen = k.NaechstesKennzeichen(d.Kuerzel), Klasse = d.Name, Name = name, Angelegt = k.Herkunft };
        if (Sauber(Param.Text(p, "kategorie")) is { } kat) e.Eigenschaften["kategorie"] = Neu(k, JsonValue.Create(kat), null, null);
        MaterialWerte.Anwenden(k, e, MaterialWerte.Lesen(p), false);
        k.Setzen(e);
        if (!e.Eigenschaften.ContainsKey("lambda"))
            k.Hinweise.Add($"{e.Kennzeichen}: λ fehlt noch – ohne λ gibt es keinen R-Wert der Aufbauten (material_aendern mit werte.lambda und Quelle).");
    }
}

public sealed class MaterialAendern : IBefehl
{
    public string Name => "material_aendern";
    public string Beschreibung => "Ändert ein Material: Name, Kategorie und Kennwerte. Jeder Kennwert braucht eine Quelle; null entfernt einen Kennwert. Aufbauten, die das Material verwenden, werden im selben Paket neu gerechnet.";
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("material", "text", true, "Material (Kennzeichen, Id oder Name)"),
        new("name", "text", false, "Neue Bezeichnung"),
        new("kategorie", "text", false, "Neue Gruppe"),
        new("werte", "objekt", false, MaterialWerte.Beschreibung + "; null entfernt den Kennwert"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var alt = Hole(k, Param.Text(p, "material", true), "Material", "Material");
        var e = alt.Kopie();
        bool geaendert = false;
        if (p.ContainsKey("name"))
        {
            var neu = Sauber(Param.Text(p, "name")) ?? throw new HlksFehler("Parameter «name» ist leer.", "Bezeichnung des Materials angeben.");
            if (neu != e.Name) { MaterialWerte.NameFrei(k, neu, e.Id); e.Name = neu; geaendert = true; }
        }
        if (p.ContainsKey("kategorie"))
        {
            var kat = Sauber(Param.Text(p, "kategorie"));
            if (kat is null) geaendert |= e.Eigenschaften.Remove("kategorie");
            else if (e.Text("kategorie") != kat) { e.Eigenschaften["kategorie"] = Neu(k, JsonValue.Create(kat), null, null); geaendert = true; }
        }
        geaendert |= MaterialWerte.Anwenden(k, e, MaterialWerte.Lesen(p), true);
        if (!geaendert) { k.Hinweise.Add("Keine Änderung."); return; }
        k.Setzen(e);
    }
}

public sealed class MaterialUebernehmen : IBefehl
{
    public string Name => "material_uebernehmen";
    public string Beschreibung => "Übernimmt ein Material aus der Bürobibliothek (kataloge/materialien.json) samt Kennwerten und Quellen ins Projekt. Die Bibliothek ist nur Vorlage; im Projekt kann das Material danach geändert werden.";
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("name", "text", true, "Name des Eintrags in der Bürobibliothek"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var name = Sauber(Param.Text(p, "name", true)) ?? throw new HlksFehler("Parameter «name» ist leer.", "Name eines Bibliothekseintrags angeben (Abfrage materialien listet sie).");
        var bib = MaterialBibliothek.Laden(k.Katalog);
        if (bib.Count == 0)
            throw new HlksFehler($"Die Bürobibliothek ({MaterialBibliothek.Datei} im Katalogordner) enthält noch keine Materialien.",
                "Material mit eigener Quelle selbst anlegen (material_anlegen) oder aus dem IFC übernehmen. Die Bibliothek füllt Christian, sobald feststeht, welche Tabellen gelten (entwurf/modul-1c.md, Quellen für Kennwerte).");
        var eintrag = bib.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? throw new HlksFehler($"«{name}» steht nicht in der Bürobibliothek.", "Vorhanden: " + string.Join(", ", bib.Select(x => x.Name).Order()));
        MaterialWerte.NameFrei(k, eintrag.Name, null);
        var d = k.Katalog.Klasse("Material");
        var e = new Element { Id = Guid.NewGuid().ToString(), Kennzeichen = k.NaechstesKennzeichen(d.Kuerzel), Klasse = d.Name, Name = eintrag.Name, Angelegt = k.Herkunft };
        if (Sauber(eintrag.Kategorie) is { } kat) e.Eigenschaften["kategorie"] = Neu(k, JsonValue.Create(kat), null, $"Bürobibliothek {MaterialBibliothek.Datei}", "katalog");
        foreach (var (key, w) in eintrag.Werte)
            e.Eigenschaften[key] = Neu(k, JsonValue.Create(w.Wert), Einheit(k.Katalog, "Material", key), w.Quelle, "katalog");
        k.Setzen(e);
        k.Hinweise.Add($"{e.Kennzeichen} «{e.Name}» aus der Bürobibliothek übernommen ({eintrag.Werte.Count} Kennwert(e) mit Quelle).");
    }
}
