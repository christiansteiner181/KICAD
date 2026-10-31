// Befehle: die einzige Art, das Modell zu ändern – für Oberfläche, KI und Skripte gleich.
// Ein Befehl plant seine Operationen auf einer Arbeitskopie (Kontext); Projekt.Ausfuehren übernimmt sie als Paket
// oder zeigt im Trockenlauf nur die Auswirkungen.

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <param name="Typ">text | zahl | bool | objekt | liste</param>
public sealed record ParameterDef(string Name, string Typ, bool Pflicht, string Beschreibung);

public interface IBefehl
{
    string Name { get; }
    string Beschreibung { get; }
    IReadOnlyList<ParameterDef> Parameter { get; }
    void Ausfuehren(Kontext k, JsonObject p);
}

/// <summary>Arbeitsumgebung eines Befehls: Arbeitskopie des Modells, gesammelte Operationen, Hinweise.</summary>
public sealed class Kontext(Projekt projekt, Modell arbeit, Herkunft herkunft)
{
    public Projekt Projekt { get; } = projekt;
    public Katalog Katalog => Projekt.Katalog;
    public Modell Arbeit { get; } = arbeit;
    public Herkunft Herkunft { get; } = herkunft;
    public List<Operation> Operationen { get; } = [];
    public List<string> Hinweise { get; } = [];
    /// <summary>Nur Auswirkungen zeigen: teure Nebenarbeiten (z. B. Hintergrund-Netz eines IFC) darf ein Befehl auslassen.</summary>
    public bool Trockenlauf { get; init; }
    /// <summary>Lagebericht für das Ergebnis des Befehls (ifc_uebernehmen: Ausrichtung mit allen Zahlen, auch im Trockenlauf und bei Ablehnung);
    /// null = keiner.</summary>
    public System.Text.Json.Nodes.JsonObject? Lage { get; set; }

    public Element Setzen(Element neu)
    {
        var alt = Arbeit.Finde(neu.Id);
        neu.Geaendert = Herkunft;
        neu.Angelegt ??= alt?.Angelegt ?? Herkunft;
        var op = new Operation { Art = "element.setzen", Id = neu.Id, Basis = alt?.Version ?? 0, Element = neu.Kopie(), ElementVorher = alt?.Kopie() };
        Arbeit.Anwenden([op]);
        Operationen.Add(op);
        return Arbeit.Finde(neu.Id)!;
    }

    public void Loeschen(Element e)
    {
        foreach (var v in Arbeit.VerbindungenVon(e.Id).ToList()) Trennen(v);
        var aktuell = Arbeit.Finde(e.Id)!;
        var op = new Operation { Art = "element.loeschen", Id = e.Id, Basis = aktuell.Version, ElementVorher = aktuell.Kopie() };
        Arbeit.Anwenden([op]);
        Operationen.Add(op);
    }

    public Verbindung Verbinden(Anschlusspunkt von, Anschlusspunkt nach)
    {
        var v = new Verbindung { Id = Guid.NewGuid().ToString(), Von = von, Nach = nach, Angelegt = Herkunft };
        var op = new Operation { Art = "verbindung.setzen", Id = v.Id, Basis = 0, Verbindung = v.Kopie() };
        Arbeit.Anwenden([op]);
        Operationen.Add(op);
        return Arbeit.FindeVerbindung(v.Id)!;
    }

    public void Trennen(Verbindung v)
    {
        var op = new Operation { Art = "verbindung.loeschen", Id = v.Id, Basis = v.Version, VerbindungVorher = v.Kopie() };
        Arbeit.Anwenden([op]);
        Operationen.Add(op);
    }

    public string NaechstesKennzeichen(string kuerzel)
    {
        int max = 0;
        foreach (var e in Arbeit.Elemente)
            if (e.Kennzeichen.StartsWith(kuerzel + "-", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(e.Kennzeichen[(kuerzel.Length + 1)..], out var n) && n > max) max = n;
        return $"{kuerzel}-{max + 1}";
    }
}

public sealed class Befehlsregister
{
    readonly Dictionary<string, IBefehl> befehle = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyCollection<IBefehl> Alle => befehle.Values;
    public void Hinzufuegen(IBefehl b) => befehle[b.Name] = b;
    public IBefehl Hole(string name) => befehle.TryGetValue(name, out var b) ? b
        : throw new HlksFehler($"Befehl «{name}» gibt es nicht.", "Befehle: " + string.Join(", ", befehle.Keys.Order()));

    public static Befehlsregister Standard()
    {
        var r = new Befehlsregister();
        r.Hinzufuegen(new ElementAnlegen());
        r.Hinzufuegen(new ElementAendern());
        r.Hinzufuegen(new ElementLoeschen());
        r.Hinzufuegen(new Verbinden());
        r.Hinzufuegen(new Trennen());
        r.Hinzufuegen(new LeitungZiehen());
        // Modul 1a: je Bereich eine eigene Datei, damit parallel gebaut werden kann
        ArchitekturBefehle.Registrieren(r);
        PlanBefehle.Registrieren(r);
        // Modul 1b
        ImportBefehle.Registrieren(r);
        GrundlagenBefehle.Registrieren(r);
        // Modul 1c
        HuelleBefehle.Registrieren(r);
        RaumgrenzenBefehle.Registrieren(r);
        // Modul 1d
        HkapBefehle.Registrieren(r);
        // Modul 2
        FamilienBefehle.Registrieren(r);
        UnterlagenBefehle.Registrieren(r);
        // Modul 3
        LeitungenBefehle.Registrieren(r);
        SchemaBefehle.Registrieren(r);
        // Modul 3b
        KollisionBefehle.Registrieren(r);
        AussparungBefehle.Registrieren(r);
        // Modul 6 Teil A
        LueftungBefehle.Registrieren(r);
        return r;
    }
}

/// <summary>Hilfen zum Lesen und Prüfen der Befehlsparameter.</summary>
public static class Param
{
    public static string? Text(JsonObject p, string name, bool pflicht = false)
    {
        var n = p[name];
        if (n is null) return pflicht ? throw new HlksFehler($"Parameter «{name}» fehlt.") : null;
        if (n is JsonValue v && v.TryGetValue<string>(out var s)) return s;
        if (n is JsonValue z) return z.ToJsonString();
        throw new HlksFehler($"Parameter «{name}» muss Text sein.");
    }

    public static double? Zahl(JsonObject p, string name)
    {
        var n = p[name];
        if (n is null) return null;
        return Json.Zahl(n) ?? throw new HlksFehler($"Parameter «{name}» muss eine Zahl sein.");
    }

    public static bool Bool(JsonObject p, string name, bool vorgabe = false) =>
        p[name] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : vorgabe;

    public static Geometrie? Geometrie(JsonObject p, string name = "geometrie")
    {
        var n = p[name];
        if (n is null) return null;
        try { return n.Deserialize<Geometrie>(Json.Optionen); }
        catch (JsonException ex) { throw new HlksFehler($"Geometrie ungültig: {ex.Message}", "Form: { \"art\": \"punkt\", \"punkte\": [[x, y, z]] } in mm"); }
    }

    public static List<double[]> Punkte(JsonObject p, string name)
    {
        if (p[name] is not JsonArray a) throw new HlksFehler($"Parameter «{name}» fehlt oder ist keine Liste.", "Form: [[x, y, z], [x, y, z]] in mm");
        var liste = new List<double[]>();
        foreach (var pt in a)
        {
            if (pt is not JsonArray xyz || xyz.Count < 2) throw new HlksFehler($"Punkt in «{name}» ungültig.", "Jeder Punkt als [x, y] oder [x, y, z] in mm");
            liste.Add(xyz.Select(c => Json.Zahl(c) ?? throw new HlksFehler($"Koordinate in «{name}» ist keine Zahl.")).ToArray());
        }
        return liste;
    }

    /// <summary>Anschlusspunkt aus { element, anschluss } oder aus Text «KENNZEICHEN.anschluss».</summary>
    public static (string Element, string? Anschluss)? Punkt(JsonObject p, string name)
    {
        var n = p[name];
        if (n is null) return null;
        if (n is JsonValue v && v.TryGetValue<string>(out var s))
        {
            var i = s.LastIndexOf('.');
            return i > 0 ? (s[..i], s[(i + 1)..]) : (s, null);
        }
        if (n is JsonObject o) return (Text(o, "element", true)!, Text(o, "anschluss"));
        throw new HlksFehler($"Parameter «{name}» muss {{ element, anschluss }} sein.");
    }
}

/// <summary>Gemeinsame Logik von Anlegen und Ändern: Bezüge, Eigenschaften, Geometrie, Zuordnung.</summary>
static class Elementpflege
{
    public static Element BezugPruefen(Kontext k, string? bezug, string rolle, string? klasse)
    {
        var e = k.Arbeit.Hole(bezug, rolle);
        if (klasse is not null && !k.Katalog.IstArt(e, klasse))
            throw new HlksFehler($"{rolle} «{e.Kennzeichen}» ist ein {e.Klasse}, erwartet wird {klasse}.",
                $"Ein Element der Klasse {klasse} angeben (elemente_suchen mit klasse {klasse} listet sie).");
        return e;
    }

    public static void Eigenschaften(Kontext k, Element e, KlassenDef d, JsonObject? eig, bool ersetzenErlaubt)
    {
        if (eig is null) return;
        foreach (var (name, roh) in eig)
        {
            var def = d.Eigenschaften.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                ?? throw new HlksFehler($"Eigenschaft «{name}» gibt es für {d.Name} nicht.",
                    "Erlaubt: " + (d.Eigenschaften.Count == 0 ? "keine" : string.Join(", ", d.Eigenschaften.Select(x => x.Name))));
            if (roh is null)
            {
                if (!ersetzenErlaubt) continue;
                if (def.Pflicht) throw new HlksFehler($"Pflichteigenschaft «{def.Name}» kann nicht entfernt werden.");
                e.Eigenschaften.Remove(def.Name);
                continue;
            }
            JsonNode? inhalt = roh; string? einheit = null; string? quelle = null;
            if (roh is JsonObject o && o.ContainsKey("wert"))
            {
                inhalt = o["wert"]?.DeepClone();
                einheit = o["einheit"]?.GetValue<string>();
                quelle = o["quelle"]?.GetValue<string>();
            }
            else inhalt = roh.DeepClone();
            if (einheit is not null && def.Einheit is not null && !einheit.Equals(def.Einheit, StringComparison.OrdinalIgnoreCase))
                throw new HlksFehler($"Eigenschaft «{def.Name}» erwartet die Einheit {def.Einheit}, nicht {einheit}.", $"Wert in {def.Einheit} umrechnen");
            inhalt = KaelteUmschreiben(k, def, inhalt);
            inhalt = Normalisieren(def, inhalt);
            e.Eigenschaften[def.Name] = new Wert { Inhalt = inhalt, Einheit = def.Einheit ?? einheit, Herkunft = k.Herkunft with { Quelle = quelle ?? k.Herkunft.Quelle } };
        }
    }

    /// <summary>Alte Kennungen in Eingaben lesen, nie ablehnen (Kälte gehört zur Heizung, Entscheid Christian 20.10.2026): systemtyp
    /// «Kühlwasser Vorlauf» → «Kälte Vorlauf», medium «KUE-VL» → «KAE-VL», gewerk «kaelte» → «heizung», gewerke-Liste ohne «kaelte»;
    /// mit Hinweis im Ergebnis.</summary>
    static JsonNode? KaelteUmschreiben(Kontext k, EigenschaftDef def, JsonNode? n)
    {
        if (def.Typ == "liste" && def.Name.Equals("gewerke", StringComparison.OrdinalIgnoreCase) && n is JsonArray liste && KaelteAlias.Gewerkliste(liste) is { } neu)
        {
            k.Hinweise.Add(KaelteAlias.HinweisGewerk);
            return neu;
        }
        if (def.Typ is "auswahl" or "text" && n is JsonValue v && v.TryGetValue<string>(out var text))
        {
            var umgeschrieben = KaelteAlias.Eigenschaftswert(def.Name, text);
            if (umgeschrieben != text)
            {
                k.Hinweise.Add(def.Name.Equals("gewerk", StringComparison.OrdinalIgnoreCase)
                    ? KaelteAlias.HinweisGewerk
                    : $"Kälte gehört zur Heizung: «{text}» wird als «{umgeschrieben}» gelesen.");
                return JsonValue.Create(umgeschrieben);
            }
        }
        return n;
    }

    static JsonNode? Normalisieren(EigenschaftDef def, JsonNode? n)
    {
        switch (def.Typ)
        {
            case "zahl":
                if (Json.Zahl(n) is double d) return JsonValue.Create(d);
                throw new HlksFehler($"Eigenschaft «{def.Name}» muss eine Zahl sein{(def.Einheit is null ? "" : $" (in {def.Einheit})")}.");
            case "bool":
                if (n is JsonValue b && b.TryGetValue<bool>(out var bb)) return JsonValue.Create(bb);
                throw new HlksFehler($"Eigenschaft «{def.Name}» muss true oder false sein.");
            case "auswahl":
            {
                var text = n is JsonValue a && a.TryGetValue<string>(out var at) ? at : throw new HlksFehler($"Eigenschaft «{def.Name}» muss ein Text sein.");
                var treffer = def.Auswahl?.FirstOrDefault(x => x.Equals(text, StringComparison.OrdinalIgnoreCase))
                    ?? throw new HlksFehler($"«{text}» ist für {def.Name} nicht erlaubt.", "Erlaubt: " + string.Join(", ", def.Auswahl ?? []));
                return JsonValue.Create(treffer);
            }
            case "liste":
                return n is JsonArray ? n : throw new HlksFehler($"Eigenschaft «{def.Name}» muss eine Liste sein.");
            case "objekt":
                return n is JsonObject o ? Schnittbereich.Normalisieren(def.Name, o) : throw new HlksFehler($"Eigenschaft «{def.Name}» muss ein Objekt sein.");   // schnittbereich und startansicht der 3D-Ansicht werden geprüft (N13)
            default:
                return n is JsonValue tv && tv.TryGetValue<string>(out var ts) ? JsonValue.Create(ts) : JsonValue.Create(n?.ToJsonString());
        }
    }

    public static void Geometrie(Kontext k, Element e, KlassenDef d, Geometrie? g)
    {
        if (g is null) return;
        if (d.Geometrie is null) throw new HlksFehler($"{d.Name} hat keine eigene Geometrie.");
        var erlaubt = d.Geometrie == "polylinie" ? new[] { "polylinie", "linie" } : d.Geometrie == "linie" ? new[] { "linie", "polylinie" } : new[] { d.Geometrie };
        if (!erlaubt.Contains(g.Art)) throw new HlksFehler($"{d.Name} braucht eine Geometrie der Art {d.Geometrie}, nicht {g.Art}.");
        int min = g.Art switch { "punkt" => 1, "polygon" => 3, _ => 2 };
        if (g.Punkte.Count < min) throw new HlksFehler($"Geometrie {g.Art} braucht mindestens {min} Punkte.");
        if (g.Art == "punkt" && g.Punkte.Count > 1) g.Punkte = [g.Punkte[0]];
        double? z0 = k.Arbeit.Finde(e.Geschoss)?.Zahl("kote");
        g.Punkte = g.Punkte.Select(p => p.Length >= 3 ? new[] { p[0], p[1], p[2] } : new[] { p[0], p[1], z0 ?? 0 }).ToList();
        e.Geometrie = g;
    }

    /// <summary>Geschoss aus der Höhe und Raum aus der Lage ableiten, wenn sie nicht angegeben sind.</summary>
    public static void Zuordnen(Kontext k, Element e, KlassenDef d)
    {
        if (e.Geometrie is null || e.Geometrie.Punkte.Count == 0) return;
        var p = e.Geometrie.Punkte[0];
        if (e.Geschoss is null && d.BrauchtGeschoss && p.Length > 2)
        {
            var g = k.Arbeit.DerKlasse("Geschoss").FirstOrDefault(x => x.Zahl("kote") is double kote && x.Zahl("hoehe") is double h && p[2] >= kote - 1 && p[2] < kote + h);
            if (g is not null) { e.Geschoss = g.Id; k.Hinweise.Add($"{e.Kennzeichen}: Geschoss {g.Kennzeichen} aus der Höhe zugeordnet"); }
        }
        if (e.Raum is null && d.BrauchtRaum && e.Geometrie.Art == "punkt")
        {
            var raeume = k.Arbeit.DerKlasse("Raum").Where(r => (e.Geschoss is null || r.Geschoss == e.Geschoss) && r.Geometrie?.Enthaelt(p[0], p[1]) == true).ToList();
            if (raeume.Count == 1) { e.Raum = raeume[0].Id; e.Geschoss ??= raeume[0].Geschoss; k.Hinweise.Add($"{e.Kennzeichen}: Raum {raeume[0].Kennzeichen} aus der Lage zugeordnet"); }
            else if (raeume.Count > 1) k.Hinweise.Add($"{e.Kennzeichen}: Lage liegt in mehreren Räumen ({string.Join(", ", raeume.Select(r => r.Kennzeichen))}), Raum bitte angeben");
        }
    }

    /// <summary>Anschlüsse mit Medium «*» aus dem System übernehmen; Nennweite der Leitungsbauteile an die Anschlüsse.</summary>
    public static void AnschluesseAbgleichen(Kontext k, Element e, KlassenDef d)
    {
        var sysMedium = Netz.SystemMedium(k.Arbeit, e.System);
        foreach (var a in e.Anschluesse)
        {
            var kat = d.Anschluesse.FirstOrDefault(x => x.Name.Equals(a.Name, StringComparison.OrdinalIgnoreCase));
            if ((kat?.Medium ?? a.Medium) == "*") a.Medium = sysMedium ?? "*";
            if (Netz.IstLeitungsbauteil(k.Katalog, e) && e.Zahl("dn") is double dn) a.Dn = (int)dn;
        }
    }

    public static void Bezuege(Kontext k, Element e, JsonObject? bezuege)
    {
        if (bezuege is null) return;
        foreach (var (rolle, wert) in bezuege)
        {
            if (wert is null) { e.Bezuege.Remove(rolle); continue; }
            var ziel = k.Arbeit.Hole(wert.GetValue<string>(), $"Bezug «{rolle}»");
            e.Bezuege[rolle] = ziel.Id;
        }
    }

    public static void Zuordnungen(Kontext k, Element e, KlassenDef d, JsonObject p)
    {
        if (p.ContainsKey("geschoss")) e.Geschoss = Param.Text(p, "geschoss") is { } g ? BezugPruefen(k, g, "Geschoss", "Geschoss").Id : null;
        if (p.ContainsKey("raum")) e.Raum = Param.Text(p, "raum") is { } r ? BezugPruefen(k, r, "Raum", "Raum").Id : null;
        if (p.ContainsKey("system")) e.System = Param.Text(p, "system") is { } s ? BezugPruefen(k, s, "System", "System").Id : null;
        if (p.ContainsKey("teilVon"))
        {
            var t = Param.Text(p, "teilVon");
            if (t is null) e.TeilVon = null;
            else
            {
                var eltern = k.Arbeit.Hole(t, "teilVon");
                if (eltern.Id == e.Id) throw new HlksFehler("Ein Element kann nicht Teil von sich selbst sein.");
                string? erwartet = d.Name switch { "Strang" => "System", "System" => "Anlage", "Geschoss" => "Gebaeude", "Raum" => "Zone", _ => null };
                if (Netz.IstLeitungsbauteil(k.Katalog, e)) erwartet = "Strang";
                if (erwartet is not null && !k.Katalog.IstArt(eltern, erwartet))
                    throw new HlksFehler($"{d.Name} kann nur teilVon einem {erwartet} sein, «{eltern.Kennzeichen}» ist ein {eltern.Klasse}.");
                e.TeilVon = eltern.Id;
                // Strang → System übernehmen; Leitungsbauteil im Strang → System des Strangs
                if (d.Name == "Strang") e.System = eltern.Id;
                if (eltern.Klasse == "Strang" && eltern.System is not null)
                {
                    if (e.System is not null && e.System != eltern.System)
                        throw new HlksFehler($"Strang {eltern.Kennzeichen} gehört zu einem anderen System als {e.Kennzeichen}.");
                    e.System = eltern.System;
                }
            }
        }
        if (d.BrauchtSystem && e.System is null)
            throw new HlksFehler($"{d.Name} braucht ein System.", "system oder teilVon (Strang) angeben");
    }

    public static void SystemtypUebernehmen(Kontext k, Element e)
    {
        if (e.Klasse != "System" || e.Text("systemtyp") is not { } typ) return;
        var st = k.Katalog.Systemtypen[typ];
        if (e.Text("medium") != st.Medium)
            e.Eigenschaften["medium"] = new Wert { Inhalt = JsonValue.Create(st.Medium), Herkunft = k.Herkunft with { Art = "katalog", Quelle = $"Systemtyp «{typ}»" } };
    }

    /// <summary>Beschriftung gehört zu genau einer Ansicht (bezuege.ansicht).</summary>
    public static void AnsichtPruefen(Kontext k, Element e, KlassenDef d)
    {
        if (!d.BrauchtAnsicht) return;
        if (!e.Bezuege.TryGetValue("ansicht", out var a) || k.Arbeit.Finde(a) is not { } ansicht || !k.Katalog.IstArt(ansicht, "Ansicht"))
            throw new HlksFehler($"{d.Name} ist eine Beschriftung und gehört zu einer Ansicht.", "bezuege: { \"ansicht\": \"AN-GR-EG\" } angeben");
    }

    public static void PflichtPruefen(Element e, KlassenDef d)
    {
        var fehlend = d.Eigenschaften.Where(x => x.Pflicht && !e.Eigenschaften.ContainsKey(x.Name)).Select(x => $"{x.Name} ({x.Beschreibung})").ToList();
        if (fehlend.Count > 0) throw new HlksFehler($"{d.Name} braucht: {string.Join("; ", fehlend)}.");
    }
}

public sealed class ElementAnlegen : IBefehl
{
    public string Name => "element_anlegen";
    public string Beschreibung => "Legt ein Element einer Katalogklasse an (Raum, Apparat, Formteil, Datenpunkt, Ansicht …). Geschoss und Raum werden aus der Lage abgeleitet, wenn sie fehlen. Anschlüsse kommen aus dem Katalog.";
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("klasse", "text", true, "Klasse aus dem Katalog, z. B. WC, Rohr, Raum, Datenpunkt"),
        new("kennzeichen", "text", false, "Lesbares Kennzeichen, eindeutig; fehlt es, wird es aus dem Klassenkürzel gebildet"),
        new("name", "text", false, "Bezeichnung"),
        new("teilVon", "text", false, "Übergeordnetes Element (Strang für Leitungsbauteile, System für Strang, Gebäude für Geschoss)"),
        new("geschoss", "text", false, "Geschoss (Kennzeichen oder Id)"),
        new("raum", "text", false, "Raum (Kennzeichen oder Id)"),
        new("system", "text", false, "System (Kennzeichen oder Id)"),
        new("eigenschaften", "objekt", false, "{ name: wert } oder { name: { wert, einheit, quelle } }"),
        new("geometrie", "objekt", false, "{ art: punkt|linie|polylinie|polygon, punkte: [[x,y,z],…] (mm), hoehe, dicke }"),
        new("anschluesse", "liste", false, "Eigene Anschlüsse statt der Katalogvorgabe: [{ name, medium, fluss, dn }]"),
        new("bezuege", "objekt", false, "{ rolle: element }, z. B. Datenpunkt { an: \"WZ-1\" }"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var d = k.Katalog.Klasse(Param.Text(p, "klasse", true)!);
        var kz = Param.Text(p, "kennzeichen")?.Trim();
        if (string.IsNullOrEmpty(kz)) kz = k.NaechstesKennzeichen(d.Kuerzel);
        if (k.Arbeit.Finde(kz) is { } doppelt && doppelt.Kennzeichen.Equals(kz, StringComparison.OrdinalIgnoreCase))
            throw new HlksFehler($"Kennzeichen «{kz}» ist schon vergeben ({doppelt.Klasse}).", $"Anderes Kennzeichen wählen, z. B. {k.NaechstesKennzeichen(d.Kuerzel)}");
        var e = new Element { Id = Guid.NewGuid().ToString(), Kennzeichen = kz, Klasse = d.Name, Name = Param.Text(p, "name"), Angelegt = k.Herkunft };
        Elementpflege.Zuordnungen(k, e, d, p);
        Elementpflege.Eigenschaften(k, e, d, p["eigenschaften"] as JsonObject, false);
        Elementpflege.SystemtypUebernehmen(k, e);
        Elementpflege.PflichtPruefen(e, d);
        e.Anschluesse = p["anschluesse"] is JsonArray eigene
            ? eigene.Deserialize<List<Anschluss>>(Json.Optionen) ?? []
            : d.Anschluesse.Select(a => a.Kopie()).ToList();
        foreach (var a in e.Anschluesse.Where(a => a.Medium != "*" && !k.Katalog.Medien.ContainsKey(a.Medium)))
            throw new HlksFehler($"Medium «{a.Medium}» am Anschluss {a.Name} gibt es nicht.", "Medien: " + string.Join(", ", k.Katalog.Medien.Keys));
        Elementpflege.Geometrie(k, e, d, Param.Geometrie(p));
        Elementpflege.Zuordnen(k, e, d);
        Elementpflege.Bezuege(k, e, p["bezuege"] as JsonObject);
        Elementpflege.AnsichtPruefen(k, e, d);
        Elementpflege.AnschluesseAbgleichen(k, e, d);
        k.Setzen(e);
    }
}

public sealed class ElementAendern : IBefehl
{
    public string Name => "element_aendern";
    public string Beschreibung => "Ändert ein Element: Kennzeichen, Name, Zuordnung, Eigenschaften (null entfernt), Geometrie, Bezüge.";
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("element", "text", true, "Kennzeichen oder Id"),
        new("kennzeichen", "text", false, "Neues Kennzeichen"),
        new("name", "text", false, "Neue Bezeichnung"),
        new("teilVon", "text", false, "Neues übergeordnetes Element (null entfernt)"),
        new("geschoss", "text", false, "Geschoss"),
        new("raum", "text", false, "Raum"),
        new("system", "text", false, "System"),
        new("eigenschaften", "objekt", false, "{ name: wert | { wert, einheit, quelle } | null }"),
        new("geometrie", "objekt", false, "Neue Geometrie (ersetzt die alte)"),
        new("bezuege", "objekt", false, "{ rolle: element | null }"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var alt = k.Arbeit.Hole(Param.Text(p, "element", true));
        var d = k.Katalog.Klasse(alt.Klasse);
        var e = alt.Kopie();
        if (Param.Text(p, "kennzeichen") is { } kz && !kz.Equals(e.Kennzeichen, StringComparison.Ordinal))
        {
            if (k.Arbeit.Finde(kz) is { } x && x.Id != e.Id) throw new HlksFehler($"Kennzeichen «{kz}» ist schon vergeben.");
            e.Kennzeichen = kz.Trim();
        }
        if (p.ContainsKey("name")) e.Name = Param.Text(p, "name");
        Elementpflege.Zuordnungen(k, e, d, p);
        Elementpflege.Eigenschaften(k, e, d, p["eigenschaften"] as JsonObject, true);
        Elementpflege.SystemtypUebernehmen(k, e);
        if (p.ContainsKey("geometrie")) Elementpflege.Geometrie(k, e, d, Param.Geometrie(p));
        if (p.ContainsKey("geometrie") && !p.ContainsKey("raum")) { if (d.BrauchtRaum) e.Raum = null; }
        Elementpflege.Zuordnen(k, e, d);
        Elementpflege.Bezuege(k, e, p["bezuege"] as JsonObject);
        Elementpflege.AnsichtPruefen(k, e, d);
        Elementpflege.AnschluesseAbgleichen(k, e, d);
        if (Json.Text(e) == Json.Text(alt)) { k.Hinweise.Add("Keine Änderung."); return; }
        k.Setzen(e);
    }
}

public sealed class ElementLoeschen : IBefehl
{
    public string Name => "element_loeschen";
    public string Beschreibung => "Löscht ein Element samt seinen Verbindungen; Türen und Fenster einer Wand gehen mit ihr. Verweisen andere Elemente darauf (teilVon, Raum, System, Bezüge), wird abgelehnt.";
    public IReadOnlyList<ParameterDef> Parameter { get; } = [new("element", "text", true, "Kennzeichen oder Id")];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var e = k.Arbeit.Hole(Param.Text(p, "element", true));
        // Aussparungen (Modul 3b), die auf das Element verweisen (gequertes Bauteil oder Leitung): Bezug lösen, Status veraltet –
        // sie blockieren das Löschen nicht; aussparungen_vorschlagen räumt danach auf bzw. schlägt neu vor
        foreach (var a in k.Arbeit.Elemente.Where(x => x.Id != e.Id && k.Katalog.IstArt(x, "Aussparung") && x.Bezuege.ContainsValue(e.Id)).ToList())
        {
            var a2 = a.Kopie();
            foreach (var rolle in a2.Bezuege.Where(b => b.Value == e.Id).Select(b => b.Key).ToList()) a2.Bezuege.Remove(rolle);
            a2.Eigenschaften["status"] = new Wert { Inhalt = JsonValue.Create("veraltet"), Herkunft = k.Herkunft };
            k.Setzen(a2);
            var nr = a.Text("nummer") is { Length: > 0 } n ? $" ({n})" : "";
            k.Hinweise.Add($"Aussparung {a.Kennzeichen}{nr}: Bezug auf {e.Kennzeichen} gelöst, Status veraltet – aussparungen_vorschlagen rechnet neu.");
        }
        // Türen und Fenster, die nur über bezuege.wand an dieser Wand hängen, löscht die Nachführung mit (OeffnungenNachfuehren)
        // Wärmebrücken-Vorschläge (Kennung «wb») löscht WaermebrueckenNachfuehren mit (Modul 1c)
        bool Mitgeloescht(Element x) => ((k.Katalog.IstArt(x, "Tuer") || k.Katalog.IstArt(x, "Fenster"))
            && x.Bezuege.GetValueOrDefault("wand") == e.Id && x.Bezuege.Count(b => b.Value == e.Id) == 1
            && x.TeilVon != e.Id && x.Geschoss != e.Id && x.Raum != e.Id && x.System != e.Id)
            || WaermebrueckenNachfuehren.Mitgeloescht(k.Katalog, x, e.Id);
        var abhaengig = k.Arbeit.Elemente.Where(x => x.Id != e.Id && !Mitgeloescht(x) &&
            (x.TeilVon == e.Id || x.Geschoss == e.Id || x.Raum == e.Id || x.System == e.Id || x.Bezuege.ContainsValue(e.Id))).ToList();
        if (abhaengig.Count > 0)
            throw new HlksFehler($"{e.Kennzeichen} wird noch von {abhaengig.Count} Element(en) verwendet: {string.Join(", ", abhaengig.Take(10).Select(x => x.Kennzeichen))}{(abhaengig.Count > 10 ? " …" : "")}.",
                "Zuerst diese Elemente löschen oder umhängen (element_aendern).");
        k.Loeschen(e);
    }
}

public sealed class Verbinden : IBefehl
{
    public string Name => "verbinden";
    public string Beschreibung => "Verbindet zwei Anschlüsse. Fehlt der Anschlussname, wird der freie, passende Anschluss gewählt (gleiches Medium, verträgliche Fliessrichtung, bei Lagen der räumlich passende). Haben beide Anschlüsse eine Lage, müssen sie höchstens 1 mm auseinander liegen und gegenläufig zeigen (höchstens 1°); haben beide eine Grösse (DN bzw. Kanalmass), muss sie gleich sein – sonst abgelehnt mit Vorschlag (Element verschieben, Formteil bzw. Reduktion setzen). Ohne Lage (Geräte ohne Familie, nicht platzierte Elemente) wird logisch verbunden. Ein Formteil ohne Strang übernimmt dabei den Strang der angeschlossenen Leitung (bei Leitungen verschiedener Stränge entscheiden die Durchgangsleitungen an a/b, sonst bleibt es ohne Strang, mit Hinweis).";
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("von", "objekt", true, "{ element, anschluss } oder \"KENNZEICHEN.anschluss\""),
        new("nach", "objekt", true, "{ element, anschluss } oder \"KENNZEICHEN.anschluss\""),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var (ve, va) = Param.Punkt(p, "von") ?? throw new HlksFehler("Parameter «von» fehlt.");
        var (ne, na) = Param.Punkt(p, "nach") ?? throw new HlksFehler("Parameter «nach» fehlt.");
        Ausfuehren(k, k.Arbeit.Hole(ve), va, k.Arbeit.Hole(ne), na);
    }

    public static Verbindung Ausfuehren(Kontext k, Element a, string? aName, Element b, string? bName)
    {
        if (a.Id == b.Id) throw new HlksFehler("Ein Element kann nicht mit sich selbst verbunden werden.");
        var (pa, pb) = Waehlen(k, a, aName, b, bName);
        foreach (var (e, x) in new[] { (a, pa), (b, pb) })
            if (k.Arbeit.VerbindungAn(e.Id, x.Name) is not null)
                throw new HlksFehler($"Anschluss {e.Kennzeichen}.{x.Name} ist schon verbunden.", "Zuerst trennen oder einen anderen Anschluss wählen.");
        var ma = Netz.Medium(k.Arbeit, a, pa); var mb = Netz.Medium(k.Arbeit, b, pb);
        if (ma != "*" && mb != "*" && !ma.Equals(mb, StringComparison.OrdinalIgnoreCase))
            throw new HlksFehler($"Medien passen nicht: {a.Kennzeichen}.{pa.Name} ist {ma}, {b.Kennzeichen}.{pb.Name} ist {mb}.");
        if ((pa.Fluss == "raus" && pb.Fluss == "raus") || (pa.Fluss == "rein" && pb.Fluss == "rein"))
            throw new HlksFehler($"Fliessrichtung widerspricht sich: {a.Kennzeichen}.{pa.Name} und {b.Kennzeichen}.{pb.Name} sind beide «{pa.Fluss}».",
                "Bei Leitungen fliesst es von «anfang» nach «ende»: Punkte umkehren oder den anderen Anschluss nehmen.");
        LageUndGroessePruefen(k, a, pa, b, pb);
        if (ma == "*" || mb == "*") k.Hinweise.Add($"Medium an {a.Kennzeichen}.{pa.Name} oder {b.Kennzeichen}.{pb.Name} noch offen (kein System).");
        var verbindung = k.Verbinden(new(a.Id, pa.Name), new(b.Id, pb.Name));
        Strangzuordnung.Uebernehmen(k, a); Strangzuordnung.Uebernehmen(k, b);   // Modul 3, Korrektur 30b: einzelnes Formteil übernimmt den Strang der Leitung
        return verbindung;
    }

    /// <summary>Modul 3 (entwurf/modul-3.md, Grundsatzentscheid 4): Haben beide Anschlüsse eine Lage (Leitungsenden, Familien), liegen
    /// sie höchstens 1 mm auseinander und zeigen gegeneinander (Winkel zwischen Richtung A und −Richtung B ≤ 1°); haben beide eine
    /// Grösse, ist sie gleich. Fehlt einer Seite die Lage (Gerät ohne Familie, nicht platziert), wird logisch verbunden wie bisher.
    /// Der IFC-Import verbindet nicht über diesen Befehl (Entscheid Hauptlauf; Abweichungen meldet dort P13).</summary>
    static void LageUndGroessePruefen(Kontext k, Element a, Anschluss pa, Element b, Anschluss pb)
    {
        var sa = Anschlusslagen.Stelle(k.Arbeit, k.Katalog, a, pa.Name);
        var sb = Anschlusslagen.Stelle(k.Arbeit, k.Katalog, b, pb.Name);
        if (sa is null || sb is null) return;
        static string Z(double x) => Math.Round(x, 2).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        var paar = $"{a.Kennzeichen}.{pa.Name} und {b.Kennzeichen}.{pb.Name}";
        if (Anschlusslagen.Abweichung(sa, sb) is { } ab)
        {
            if (ab.Abstand > Anschlusslagen.AbstandMax)
                throw new HlksFehler($"{paar} liegen {Z(ab.Abstand)} mm auseinander – verbunden wird nur, was bis {Z(Anschlusslagen.AbstandMax)} mm aufeinander liegt " +
                    $"({a.Kennzeichen}.{pa.Name} bei {Punkt(sa.Lage!)}, {b.Kennzeichen}.{pb.Name} bei {Punkt(sb.Lage!)}).",
                    "Element verschieben (element_aendern geometrie), die Leitung mit leitung_zeichnen bis an den Anschluss führen oder ein Formteil setzen; anschlusslagen zeigt Lage und Richtung.");
            if (ab.Winkel > Anschlusslagen.WinkelMax)
                throw new HlksFehler($"{paar} zeigen nicht gegeneinander: Abweichung {Z(ab.Winkel)}° von gegenläufig (erlaubt {Z(Anschlusslagen.WinkelMax)}°; " +
                    $"Richtung {Punkt(sa.Richtung!, 3)} bzw. {Punkt(sb.Richtung!, 3)}).",
                    "Element drehen bzw. ausrichten (element_aendern drehung bzw. ausrichtung), einen Bogen setzen (Formteil) oder die Leitung mit leitung_zeichnen führen; anschlusslagen zeigt die Richtungen.");
        }
        if (sa.HatGroesse && sb.HatGroesse && !sa.Groesse.Gleich(sb.Groesse))
            throw new HlksFehler($"Grössen passen nicht: {a.Kennzeichen}.{pa.Name} ist {sa.Groesse}, {b.Kennzeichen}.{pb.Name} ist {sb.Groesse}.",
                "Reduktion einsetzen (Formteil Reduktion zwischen den Grössen) oder die Grösse angleichen (element_aendern familientyp bzw. dn).");
    }

    static string Punkt(double[] p, int stellen = 1) =>
        "[" + string.Join(", ", p.Select(x => Math.Round(x, stellen).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))) + "]";

    static (Anschluss, Anschluss) Waehlen(Kontext k, Element a, string? aName, Element b, string? bName)
    {
        Anschluss Hole(Element e, string n) => e.AnschlussNamens(n) ?? throw new HlksFehler($"{e.Kennzeichen} hat keinen Anschluss «{n}».",
            "Anschlüsse: " + string.Join(", ", e.Anschluesse.Select(x => $"{x.Name} ({Netz.Medium(k.Arbeit, e, x)}, {x.Fluss})")));
        IEnumerable<Anschluss> Frei(Element e) => e.Anschluesse.Where(x => k.Arbeit.VerbindungAn(e.Id, x.Name) is null);
        bool Passt(Element e1, Anschluss x1, Element e2, Anschluss x2)
        {
            var m1 = Netz.Medium(k.Arbeit, e1, x1); var m2 = Netz.Medium(k.Arbeit, e2, x2);
            bool medium = m1 == "*" || m2 == "*" || m1.Equals(m2, StringComparison.OrdinalIgnoreCase);
            bool fluss = !((x1.Fluss == "raus" && x2.Fluss == "raus") || (x1.Fluss == "rein" && x2.Fluss == "rein"));
            return medium && fluss;
        }
        if (aName is not null && bName is not null) return (Hole(a, aName), Hole(b, bName));
        IEnumerable<Anschluss> qa = aName is not null ? new[] { Hole(a, aName) } : Frei(a);
        IEnumerable<Anschluss> qb = bName is not null ? new[] { Hole(b, bName) } : Frei(b);
        var kandidaten = qa.SelectMany(x => qb.Where(y => Passt(a, x, b, y)).Select(y => (x, y))).ToList();
        if (kandidaten.Count == 1) return kandidaten[0];
        if (kandidaten.Count > 1)
        {
            // Modul 3: haben die Anschlüsse Lagen, gilt das Paar, das räumlich passt (aufeinander, gegenläufig, gleiche Grösse)
            var la = Anschlusslagen.Von(k.Arbeit, k.Katalog, a); var lb = Anschlusslagen.Von(k.Arbeit, k.Katalog, b);
            AnschlussStelle? S(List<AnschlussStelle> l, Anschluss x) => l.FirstOrDefault(s => s.Name.Equals(x.Name, StringComparison.OrdinalIgnoreCase));
            var raeumlich = kandidaten.Where(c => S(la, c.Item1) is { } s1 && S(lb, c.Item2) is { } s2 && Anschlusslagen.PasstRaeumlich(s1, s2)).ToList();
            if (raeumlich.Count == 1) return raeumlich[0];
        }
        if (kandidaten.Count == 0)
            throw new HlksFehler($"Kein freies, passendes Anschlusspaar zwischen {a.Kennzeichen} und {b.Kennzeichen}.",
                $"Anschlüsse {a.Kennzeichen}: {string.Join(", ", a.Anschluesse.Select(x => $"{x.Name} ({Netz.Medium(k.Arbeit, a, x)}, {x.Fluss})"))}; " +
                $"{b.Kennzeichen}: {string.Join(", ", b.Anschluesse.Select(x => $"{x.Name} ({Netz.Medium(k.Arbeit, b, x)}, {x.Fluss})"))}");
        throw new HlksFehler($"Mehrere passende Anschlusspaare zwischen {a.Kennzeichen} und {b.Kennzeichen}: {string.Join(", ", kandidaten.Select(c => $"{c.Item1.Name}–{c.Item2.Name}"))}.",
            "Anschluss ausdrücklich angeben (anschlusslagen zeigt Lage und Richtung je Anschluss).");
    }
}

public sealed class Trennen : IBefehl
{
    public string Name => "trennen";
    public string Beschreibung => "Trennt eine Verbindung, angegeben über die Verbindungs-Id oder einen ihrer Anschlüsse.";
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("verbindung", "text", false, "Id der Verbindung"),
        new("anschluss", "objekt", false, "{ element, anschluss } oder \"KENNZEICHEN.anschluss\" – trennt die Verbindung an diesem Anschluss"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        Verbindung? v = null;
        if (Param.Text(p, "verbindung") is { } id) v = k.Arbeit.FindeVerbindung(id) ?? throw new HlksFehler($"Verbindung {id} gibt es nicht.");
        else if (Param.Punkt(p, "anschluss") is { } ap)
        {
            var el = k.Arbeit.Hole(ap.Element);
            if (ap.Anschluss is null) throw new HlksFehler("Anschlussname fehlt.", "Form: \"KENNZEICHEN.anschluss\"");
            v = k.Arbeit.VerbindungAn(el.Id, ap.Anschluss) ?? throw new HlksFehler($"{el.Kennzeichen}.{ap.Anschluss} ist nicht verbunden.");
        }
        else throw new HlksFehler("verbindung oder anschluss angeben.");
        k.Trennen(v);
    }
}

public sealed class LeitungZiehen : IBefehl
{
    public string Name => "leitung_ziehen";
    public const string Text = "Zieht eine Leitung (Rohr oder Kanal) entlang von Punkten in Fliessrichtung und verbindet Anfang und Ende auf Wunsch mit bestehenden Anschlüssen – in einem Schritt. Mit serie und groesse bekommt die Leitung Serie und Grösse (bezuege.familie, familientyp, dn bzw. breite/hoehe/durchmesser) – sie bleibt eine Polylinie ohne Bögen (leitung_aufteilen setzt sie, leitung_zeichnen zeichnet mit Bögen); bei Kanalserien mit freier Grösse (leitung.frei) jede Grösse innerhalb von Grenzen und Schritt.";
    public string Beschreibung => Text;
    public const string ParSerie = "Leitungsserie (Familie Rohr bzw. Kanal; Kennzeichen, Name oder id) – optional; klasse folgt dann der Serie";
    public const string ParGroesse = "Grösse der Serie (mit serie Pflicht): Typname, Zahl (Rohr DN, Abwasser-Serie nach Aussendurchmesser bzw. Kanal rund d) bzw. { dn } | { d } | { b, h }; bei freien Kanalserien jede Grösse innerhalb von Grenzen und Schritt, z. B. { \"b\": 430, \"h\": 270 }";
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("system", "text", false, "System (Kennzeichen); entfällt, wenn strang angegeben ist"),
        new("strang", "text", false, "Strang, zu dem die Leitung gehört"),
        new("punkte", "liste", true, "Verlauf in Fliessrichtung: [[x,y,z], …] in mm"),
        new("klasse", "text", false, "Rohr (Vorgabe) oder Kanal"),
        new("kennzeichen", "text", false, "Kennzeichen; fehlt es, wird es gebildet"),
        new("geschoss", "text", false, "Geschoss; fehlt es, aus der Höhe des ersten Punkts"),
        new("dn", "zahl", false, "Nennweite"),
        new("material", "text", false, "Werkstoff"),
        new("eigenschaften", "objekt", false, "Weitere Eigenschaften, z. B. { gefaelle: 2 }"),
        new("von", "objekt", false, "Anschluss, an den der Anfang kommt: { element, anschluss }"),
        new("nach", "objekt", false, "Anschluss, an den das Ende kommt: { element, anschluss }"),
        new("serie", "text", false, ParSerie),
        new("groesse", "objekt", false, ParGroesse),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        // Serie und Grösse (Nachtrag freie Grösse): Grösse wie leitung_zeichnen (Rastertyp bzw. freie Grösse einer Kanalserie)
        Leitungsserie? serie = Param.Text(p, "serie") is { } sz ? Leitungsserie.Hole(k.Arbeit, k.Katalog, sz) : null;
        if (serie is null && p["groesse"] is not null) throw new HlksFehler("groesse wirkt nur mit serie.", "serie angeben (Leitungsserie) oder groesse weglassen und dn bzw. eigenschaften setzen.");
        var sg = serie is null ? null : RoutingBefehle.Groesse(serie, p["groesse"]);
        var klasse = Param.Text(p, "klasse") ?? serie?.Kategorie ?? "Rohr";
        if (serie is not null && !(k.Katalog.Klassen.TryGetValue(klasse, out var kd) && kd.IstArt(serie.Kategorie)))
            throw new HlksFehler($"klasse {klasse} passt nicht zur Serie {serie.Bezeichnung} ({serie.Kategorie}).", $"klasse weglassen oder {serie.Kategorie} angeben.");
        var eig = (p["eigenschaften"] as JsonObject)?.DeepClone() as JsonObject ?? new JsonObject();
        if (Param.Zahl(p, "dn") is double dn) eig["dn"] = dn;
        if (Param.Text(p, "material") is { } mat) eig["material"] = mat;
        if (sg is not null)
        {
            if (sg.Groesse.Dn is double sdn) eig["dn"] = sdn;
            if (sg.Groesse.D is double sd) eig["durchmesser"] = sd;
            if (sg.Groesse.B is double sb && sg.Groesse.H is double sh) { eig["breite"] = sb; eig["hoehe"] = sh; }
        }
        var anlegen = new JsonObject
        {
            ["klasse"] = klasse,
            ["kennzeichen"] = Param.Text(p, "kennzeichen"),
            ["eigenschaften"] = eig,
            ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = p["punkte"]?.DeepClone() },
        };
        if (Param.Text(p, "strang") is { } st) anlegen["teilVon"] = st;
        if (Param.Text(p, "system") is { } sy) anlegen["system"] = sy;
        if (Param.Text(p, "geschoss") is { } ge) anlegen["geschoss"] = ge;
        _ = Param.Punkte(p, "punkte");
        new ElementAnlegen().Ausfuehren(k, anlegen);
        var leitung = k.Arbeit.Finde(k.Operationen[^1].Id)!;
        if (serie is not null && sg is not null)
        {
            var mitSerie = leitung.Kopie();
            Routing.GroesseSetzen(k, mitSerie, serie, sg);
            leitung = k.Setzen(mitSerie);
        }
        if (Param.Punkt(p, "von") is { } von) Verbinden.Ausfuehren(k, k.Arbeit.Hole(von.Element), von.Anschluss, leitung, "anfang");
        if (Param.Punkt(p, "nach") is { } nach) Verbinden.Ausfuehren(k, leitung, "ende", k.Arbeit.Hole(nach.Element), nach.Anschluss);
    }
}
