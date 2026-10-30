// Familien (Modul 2, entwurf/modul-2.md): Einhängepunkt für Befehle und Abfragen. Angelegt vom Hauptlauf (Phase A),
// damit die Bauaufträge die zentralen Dateien (Befehle.cs, Abfragen.cs, Tuer.cs) nicht gleichzeitig ändern müssen:
// Befehle hier registrieren, Abfragen in FamilienAbfragen (FamilienAbfragen.cs) beschreiben und in Tuer() beantworten.
//
// Befehle (B1): familie_entwurf, familie_aendern, familie_laden, familie_platzieren. Alle laufen über Projekt.Ausfuehren
// (Trockenlauf, Pakete, rückgängig); die Nachführung der platzierten Elemente macht FamilienNachfuehren im selben Paket.
// Die Familie im Projekt ist ein Element der Klasse Familie (Kennzeichen FAM-n) mit den Eigenschaften kategorie,
// definition, status, version, bibliothek, freigabe. definition.version und definition.status werden mitgeführt, damit
// eine gespeicherte Datei sie trägt.
// ANNAHMEN (Prüfliste): jede Änderung der Definition (familie_entwurf, familie_aendern) setzt den Status auf entwurf und
//   entfernt die Freigabe; familie_laden übernimmt den Status der Datei (Spezifikation «Status bleibt», ohne Angabe
//   entwurf); fehlt die feste Kennung id, vergibt der Kern «fam-<guid>»; familie_aendern ersetzt mit *_setzen den ganzen
//   Eintrag (gleicher Name bzw. gleiche id), parameter_loeschen entfernt auch die Werte des Parameters in den Typen.
// Nachtrag B7 (entwurf/modul-2-bibliothek.md): familie_laden lädt aus der projektübergreifenden Familienbibliothek (id, version;
//   Unterlagen werden mitkopiert) oder wie bisher aus einer Datei (datei); Schreiben in die Bibliothek: FamilienBibliothekBefehle.cs.
// Nachtrag Q1 (Befund C3, Entscheid Hauptlauf 19.10.2026): familie_laden einer Familie, die (gleiche id) schon im Projekt ist, wirkt wie
//   familie_aktualisieren (Versionsübernahme); das darf die KI nicht ohne Mensch – für den Akteur «ki» lehnt der Kern es ab
//   (FamilieLaden.NurNeueFuerKi). Neue Familien darf die KI laden.

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class FamilienBefehle
{
    /// <summary>Registriert die Familienbefehle (familie_entwurf, familie_aendern, familie_laden, familie_platzieren …).</summary>
    public static void Registrieren(Befehlsregister r)
    {
        r.Hinzufuegen(new FamilieEntwurf());
        r.Hinzufuegen(new FamilieAendern());
        r.Hinzufuegen(new FamilieLaden());
        r.Hinzufuegen(new FamiliePlatzieren());
        // B4 (FamilienFreigabe.cs): nur ein Mensch
        r.Hinzufuegen(new KonfliktEntscheiden());
        r.Hinzufuegen(new WertBestaetigen());
        r.Hinzufuegen(new FamilieFreigeben());
        r.Hinzufuegen(new FamilieSpeichern());
        // Nachtrag B7 (FamilienBibliothekBefehle.cs): projektübergreifende Familienbibliothek
        r.Hinzufuegen(new FamilieInBibliothek());
        r.Hinzufuegen(new FamilieAktualisieren());
        r.Hinzufuegen(new FamilienauswahlSetzen());
        // Nachtrag 21.10.2026 (FamilienQuellenVersion.cs): Quellen auf die aktuelle Leserversion nachführen
        r.Hinzufuegen(new FamilienQuellenNachfuehren());
    }

    /// <summary>Definition ohne die mitgeführten Felder version und status (zum Vergleich «hat sich etwas geändert?»).</summary>
    internal static string Inhalt(JsonObject def)
    {
        var k = (JsonObject)def.DeepClone();
        k.Remove("version"); k.Remove("status");
        return k.ToJsonString();
    }

    /// <summary>Prüft die Definition und legt die Familie an bzw. ersetzt ihre Definition. Gibt das Familie-Element zurück.</summary>
    internal static Element Speichern(Kontext k, Element? bisher, JsonObject definition, string befehl, string status, int version, JsonObject? bibliothek)
    {
        var def = (JsonObject)definition.DeepClone();
        if (def["id"] is not JsonValue idv || !idv.TryGetValue<string>(out var id) || string.IsNullOrWhiteSpace(id))
        {
            id = bisher is null ? null : Familien.DefinitionsId(bisher);
            id ??= "fam-" + Guid.NewGuid().ToString("N")[..12];
            def["id"] = id;
        }
        def["version"] = version;
        def["status"] = status;
        var f = FamilienPruefung.Pruefen(k, def, befehl, bisher);

        foreach (var andere in k.Arbeit.DerKlasse(Familien.Klasse).Where(x => x.Id != bisher?.Id))
        {
            if (Familien.DefinitionsId(andere) == id)
                throw new HlksFehler($"Eine Familie mit der Kennung «{id}» gibt es schon ({andere.Kennzeichen} «{andere.Name}»).",
                    $"Mit familie = {andere.Kennzeichen} ihre Definition ersetzen oder eine andere id wählen.");
            if (string.Equals(andere.Name?.Trim(), f.Name, StringComparison.OrdinalIgnoreCase))
                throw new HlksFehler($"Eine Familie «{f.Name}» gibt es schon ({andere.Kennzeichen}).",
                    $"Mit familie = {andere.Kennzeichen} ihre Definition ersetzen oder einen anderen Namen wählen.");
        }

        var d = k.Katalog.Klasse(Familien.Klasse);
        var e = bisher?.Kopie() ?? new Element
        {
            Id = Guid.NewGuid().ToString(), Kennzeichen = k.NaechstesKennzeichen(d.Kuerzel), Klasse = d.Name, Angelegt = k.Herkunft,
        };
        e.Name = f.Name;
        Wert W(JsonNode? inhalt, string? einheit = null) => new() { Inhalt = inhalt, Einheit = einheit, Herkunft = k.Herkunft };
        e.Eigenschaften["kategorie"] = W(JsonValue.Create(f.Kategorie));
        e.Eigenschaften["definition"] = W(def);
        e.Eigenschaften["status"] = W(JsonValue.Create(status));
        e.Eigenschaften["version"] = W(JsonValue.Create((double)version));
        if (bibliothek is not null) e.Eigenschaften["bibliothek"] = W(bibliothek);
        if (status == "entwurf") e.Eigenschaften.Remove("freigabe");
        var gesetzt = k.Setzen(e);
        k.Hinweise.Add($"Familie {gesetzt.Kennzeichen} «{f.Name}» ({f.Kategorie}), Version {version}, Status {status}: {f.Parameter.Count} Parameter, " +
            $"{f.Typen.Count} Typ(en), {f.Koerper.Count} Körper, {f.Anschluesse.Count} Anschlüsse{(f.Sinnbild is { Count: > 0 } ? ", mit Sinnbild" : "")}.");
        return gesetzt;
    }

    internal static int Version(Element? familie) => familie?.Zahl("version") is double v ? (int)v : 0;
}

public sealed class FamilieEntwurf : IBefehl
{
    public string Name => "familie_entwurf";
    public const string Text = "Prüft eine Familiendefinition (Format schema/hlks-familie.schema.json: Parameter mit Formeln, Typen, Körper, Anschlüsse mit Lage und Richtung, Sinnbild) und legt sie als Familie im Status entwurf an (Kennzeichen FAM-n, Version 1) oder ersetzt die Definition einer bestehenden Familie (Version + 1, Status wieder entwurf). Platzierte Elemente werden im selben Paket nachgeführt (Anschlüsse, Kennwerte). Fehler kommen mit Stelle und Vorschlag; der Trockenlauf ist die Vorschau.";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("definition", "objekt", true, "Die Familie als JSON-Objekt (format 1, name, kategorie, parameter, typen, koerper, anschluesse, sinnbild …)"),
        new("familie", "text", false, "Bestehende Familie (Kennzeichen, Name oder id), deren Definition ersetzt wird; leer = neue Familie"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        if (p["definition"] is not JsonObject def)
            throw new HlksFehler("Parameter «definition» fehlt oder ist kein Objekt.", "Definition als JSON-Objekt nach schema/hlks-familie.schema.json angeben.");
        Element? bisher = Param.Text(p, "familie") is { } fb ? Familien.Hole(k.Arbeit, fb) : null;
        if (bisher?.Eigenschaft("definition")?.Inhalt is JsonObject alt && FamilienBefehle.Inhalt(alt) == FamilienBefehle.Inhalt(def) && bisher.Text("status") == "entwurf")
        {
            k.Hinweise.Add("Keine Änderung: die Definition ist gleich.");
            return;
        }
        FamilienBefehle.Speichern(k, bisher, def, Name, "entwurf", FamilienBefehle.Version(bisher) + 1, null);
    }
}

public sealed class FamilieAendern : IBefehl
{
    public string Name => "familie_aendern";
    public const string Text = "Ändert eine Familie gezielt – der Weg des Editors. ops ist eine Liste von Änderungen, je { op, … }: parameter_setzen { parameter }, parameter_loeschen { name }, parameter_umbenennen { alt, neu } (überall: Formeln, Typwerte, Quellen, Masse, Sinnbild und Exemplarwerte der platzierten Elemente), typ_setzen { typ }, typ_loeschen { name }, koerper_setzen { koerper }, koerper_loeschen { id }, anschluss_setzen { anschluss }, anschluss_loeschen { name }, sinnbild_setzen { sinnbild | null }, leitung_setzen { leitung } (Block «leitung» einer Leitungsserie ersetzen, Modul 3), kopf_setzen { name, kategorie, hersteller, produkt, beschreibung, bericht }. *_setzen ersetzt den Eintrag gleichen Namens (bzw. gleicher id) ganz oder hängt ihn an. Danach wird die Definition wie bei familie_entwurf geprüft (Version + 1, Status entwurf) und platzierte Elemente werden nachgeführt.";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("familie", "text", true, "Familie (Kennzeichen, Name oder id)"),
        new("ops", "liste", true, "Änderungen in dieser Reihenfolge, z. B. [{ \"op\": \"typ_setzen\", \"typ\": { \"name\": \"22-600-1200\", \"werte\": { \"laenge\": 1200 } } }]"),
    ];

    static readonly string[] Ops = ["parameter_setzen", "parameter_loeschen", "parameter_umbenennen", "typ_setzen", "typ_loeschen", "koerper_setzen", "koerper_loeschen",
        "anschluss_setzen", "anschluss_loeschen", "sinnbild_setzen", "leitung_setzen", "kopf_setzen"];
    static readonly string[] KopfFelder = ["name", "kategorie", "hersteller", "produkt", "beschreibung", "bericht"];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var fam = Familien.Hole(k.Arbeit, Param.Text(p, "familie", true));
        if (p["ops"] is not JsonArray ops || ops.Count == 0)
            throw new HlksFehler("Parameter «ops» fehlt oder ist leer.", "Liste von Änderungen angeben, z. B. [{ \"op\": \"parameter_loeschen\", \"name\": \"x\" }]. Ops: " + string.Join(", ", Ops));
        var def = (fam.Eigenschaft("definition")?.Inhalt as JsonObject)?.DeepClone() as JsonObject ?? new JsonObject();
        var umbenannt = new List<(string Alt, string Neu)>();
        int nr = 0;
        foreach (var o in ops)
        {
            nr++;
            if (o is not JsonObject op) throw new HlksFehler($"ops[{nr}] ist kein Objekt.", "Form: { \"op\": \"…\", … }");
            Anwenden(def, op, nr);
            if (T(op["op"]) == "parameter_umbenennen") umbenannt.Add((T(op["alt"])!.Trim(), T(op["neu"])!.Trim()));   // Nachtrag 2: Exemplarwerte unten
        }
        if (FamilienBefehle.Inhalt(def) == FamilienBefehle.Inhalt((JsonObject)fam.Eigenschaft("definition")!.Inhalt!) && fam.Text("status") == "entwurf")
        {
            k.Hinweise.Add("Keine Änderung.");
            return;
        }
        FamilienBefehle.Speichern(k, fam, def, Name, "entwurf", FamilienBefehle.Version(fam) + 1, null);
        FamilienUmbenennen.Exemplarwerte(k, fam, umbenannt);
    }

    static JsonArray Liste(JsonObject def, string feld)
    {
        if (def[feld] is JsonArray a) return a;
        var neu = new JsonArray();
        def[feld] = neu;
        return neu;
    }

    static string? T(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    /// <summary>Eintrag gleichen Schlüssels ersetzen oder anhängen.</summary>
    static void Setzen(JsonObject def, string feld, string schluessel, JsonNode? eintrag, int nr, string op, bool grossKlein)
    {
        if (eintrag is not JsonObject o) throw new HlksFehler($"ops[{nr}] ({op}): Objekt fehlt.", $"Form: {{ \"op\": \"{op}\", \"{op.Split('_')[0]}\": {{ \"{schluessel}\": \"…\", … }} }}");
        var wert = T(o[schluessel])?.Trim();
        if (string.IsNullOrEmpty(wert)) throw new HlksFehler($"ops[{nr}] ({op}): «{schluessel}» fehlt.", $"Den Eintrag über «{schluessel}» benennen.");
        var a = Liste(def, feld);
        var vgl = grossKlein ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        for (int i = 0; i < a.Count; i++)
            if (a[i] is JsonObject x && string.Equals(T(x[schluessel])?.Trim(), wert, vgl)) { a[i] = o.DeepClone(); return; }
        a.Add(o.DeepClone());
    }

    static void Loeschen(JsonObject def, string feld, string schluessel, string? wert, int nr, string op, bool grossKlein)
    {
        if (string.IsNullOrWhiteSpace(wert)) throw new HlksFehler($"ops[{nr}] ({op}): «{schluessel}» fehlt.", $"Form: {{ \"op\": \"{op}\", \"{schluessel}\": \"…\" }}");
        var a = Liste(def, feld);
        var vgl = grossKlein ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        for (int i = 0; i < a.Count; i++)
            if (a[i] is JsonObject x && string.Equals(T(x[schluessel])?.Trim(), wert.Trim(), vgl)) { a.RemoveAt(i); return; }
        throw new HlksFehler($"ops[{nr}] ({op}): «{wert}» gibt es in {feld} nicht.",
            "Vorhanden: " + string.Join(", ", a.OfType<JsonObject>().Select(x => T(x[schluessel])).Where(x => x is not null)));
    }

    static void Anwenden(JsonObject def, JsonObject op, int nr)
    {
        var art = T(op["op"]);
        switch (art)
        {
            case "parameter_setzen": Setzen(def, "parameter", "name", op["parameter"], nr, art, false); break;
            case "parameter_umbenennen": FamilienUmbenennen.Anwenden(def, op, nr); break;
            case "parameter_loeschen":
            {
                var name = T(op["name"]);
                Loeschen(def, "parameter", "name", name, nr, art, false);
                foreach (var t in Liste(def, "typen").OfType<JsonObject>())
                {
                    (t["werte"] as JsonObject)?.Remove(name!);
                    (t["quellen"] as JsonObject)?.Remove(name!);
                }
                break;
            }
            case "typ_setzen": Setzen(def, "typen", "name", op["typ"], nr, art, false); break;
            case "typ_loeschen": Loeschen(def, "typen", "name", T(op["name"]), nr, art, false); break;
            case "koerper_setzen": Setzen(def, "koerper", "id", op["koerper"], nr, art, false); break;
            case "koerper_loeschen": Loeschen(def, "koerper", "id", T(op["id"]), nr, art, false); break;
            case "anschluss_setzen": Setzen(def, "anschluesse", "name", op["anschluss"], nr, art, true); break;
            case "anschluss_loeschen": Loeschen(def, "anschluesse", "name", T(op["name"]), nr, art, true); break;
            case "sinnbild_setzen":
                if (!op.ContainsKey("sinnbild")) throw new HlksFehler($"ops[{nr}] (sinnbild_setzen): «sinnbild» fehlt.", "Liste von Zeichnungsteilen oder null (Sinnbild entfernen).");
                if (op["sinnbild"] is null) def.Remove("sinnbild");
                else if (op["sinnbild"] is JsonArray s) def["sinnbild"] = s.DeepClone();
                else throw new HlksFehler($"ops[{nr}] (sinnbild_setzen): «sinnbild» muss eine Liste oder null sein.");
                break;
            case "leitung_setzen":   // Modul 3: Block «leitung» der Leitungsserie ganz ersetzen
                if (op["leitung"] is not JsonObject lb) throw new HlksFehler($"ops[{nr}] (leitung_setzen): «leitung» fehlt oder ist kein Objekt.", "{ \"op\": \"leitung_setzen\", \"leitung\": { \"querschnitt\": \"rund\", \"groesse\": { \"dn\": \"dn\" }, \"aussen\": \"da\", \"formteile\": { … }, \"regeln\": { … } } }");
                def["leitung"] = lb.DeepClone();
                break;
            case "kopf_setzen":
                foreach (var (feld, wert) in op)
                {
                    if (feld == "op") continue;
                    if (!KopfFelder.Contains(feld)) throw new HlksFehler($"ops[{nr}] (kopf_setzen): Feld «{feld}» gibt es nicht.", "Felder: " + string.Join(", ", KopfFelder));
                    if (wert is null)
                    {
                        if (feld is "name" or "kategorie") throw new HlksFehler($"ops[{nr}] (kopf_setzen): «{feld}» kann nicht entfernt werden.");
                        def.Remove(feld);
                    }
                    else def[feld] = wert.DeepClone();
                }
                break;
            default:
                throw new HlksFehler($"ops[{nr}]: op «{art}» gibt es nicht.", "Ops: " + string.Join(", ", Ops));
        }
    }
}

public sealed class FamilieLaden : IBefehl
{
    public string Name => "familie_laden";
    public const string Text = "Lädt eine Familie ins Projekt (Kopie, der Status bleibt). Aus der Familienbibliothek (Bürobibliothek, Abfrage familien_bibliothek): id (Familien-Id oder Name) " +
        "und optional version (Nummer oder Abzweig; Vorgabe die aktuelle Version) – die Herstellerunterlagen der Familie werden ins Projekt kopiert (unterlagen\\, Prüfsumme bleibt) und als " +
        "Unterlage eingefügt, damit Quellen, Befund-Ids und Kontrollbilder dort gelten; bibliothek = { id, version, pruefsumme, datei, zeit }. Aus einer Datei: datei = <name>.familie.json " +
        "aus kataloge/familien (mitgeliefert, nur lesbar), ein Ordner bzw. eine Datei der Bibliothek oder ein voller Pfad; bibliothek = { datei, pruefsumme }. Gibt es dieselbe Familie " +
        "(gleiche id) schon, wird sie auf die gewählte Fassung gebracht (wie familie_aktualisieren, Version + 1, Elemente nachgeführt) – das übernimmt nur ein Mensch; " +
        "eine KI darf nur Familien laden, die noch nicht im Projekt sind.";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("id", "text", false, "Familie der Bibliothek: Familien-Id oder Name (Abfrage familien_bibliothek)"),
        new("datei", "text", false, "Statt id: Dateiname in kataloge/familien (mit oder ohne .familie.json), Ordner bzw. Datei in der Bibliothek oder voller Pfad"),
        new("version", "text", false, "Mit id: Version (Nummer, z. B. 3) oder Abzweig; leer = aktuelle Version"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var id = Param.Text(p, "id")?.Trim();
        var datei = Param.Text(p, "datei")?.Trim();
        var version = Param.Text(p, "version")?.Trim();
        if (string.IsNullOrEmpty(id) && string.IsNullOrEmpty(datei))
            throw new HlksFehler("Parameter «id» oder «datei» fehlt.", Vorschlag(k));
        if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(datei))
            throw new HlksFehler("id und datei zugleich angegeben.", "Entweder id (Familie der Bibliothek) oder datei (Datei bzw. Pfad) angeben.");
        var ort = FamilienBibliothekDienst.Ort(k.Katalog);
        if (!string.IsNullOrEmpty(id)) { AusBibliothek(k, ort, id, null, version); return; }

        // datei: mitgeliefert (kataloge/familien) › Ordner bzw. Datei der Bibliothek › voller Pfad
        var ordner = Path.Combine(k.Katalog.Ordner, "familien");
        var mitgeliefert = Path.IsPathRooted(datei!) ? null : Path.Combine(ordner, datei!.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? datei : datei + ".familie.json");
        // Sicherheitsprüfung 3c, B3a/B4: für die KI nur Pfade unter dem Projektordner und den Wurzeln aus kataloge/buero.json (auch «..\» im Namen),
        // geprüft vor dem ersten Dateizugriff. Die Bibliothek prüft BibliotheksPfad selbst (nur unter ihrer Wurzel, sonst ohne Zugriff null).
        if (mitgeliefert is not null) mitgeliefert = KiPfade.Pruefen(k.Projekt, mitgeliefert, Name);
        if (mitgeliefert is not null && File.Exists(mitgeliefert)) { DateiLaden(k, mitgeliefert, ordner); return; }
        if (ort.Aktiv && BibliotheksPfad(ort.Pfad!, datei!) is { } bp) { AusBibliothek(k, ort, null, bp.Ordner, version ?? bp.Version); return; }
        var pfad = KiPfade.Pruefen(k.Projekt, Path.IsPathRooted(datei!) ? datei! : mitgeliefert!, Name);
        if (!File.Exists(pfad)) throw new HlksFehler($"Familiendatei «{pfad}» gibt es nicht.", Vorschlag(k));
        if (version is not null) k.Hinweise.Add("version gilt nur für Familien der Bibliothek (id) und wurde ignoriert.");
        DateiLaden(k, pfad, ordner);
    }

    /// <summary>Vorschlag bei fehlender Familie: Familien der Bibliothek, dann die mitgelieferten («Bibliothek kataloge/familien: …», von der Oberfläche gelesen).</summary>
    static string Vorschlag(Kontext k)
    {
        var teile = new List<string>();
        var ort = FamilienBibliothekDienst.Ort(k.Katalog);
        if (ort.Aktiv && FamilienBibliothekDienst.IndexOhneSchreiben(ort) is { Familien.Count: > 0 } idx)
            teile.Add($"Familienbibliothek (id): {string.Join(", ", idx.Familien.Take(30).Select(f => $"{f.Id} «{f.Name}»"))}{(idx.Familien.Count > 30 ? " …" : "")} – familie_laden {{ id }}.");
        else if (!ort.Aktiv) teile.Add($"Familienbibliothek: {ort.Sperre}");
        var ordner = Path.Combine(k.Katalog.Ordner, "familien");
        var vorhanden = Directory.Exists(ordner) ? Directory.GetFiles(ordner, "*.familie.json").Select(Path.GetFileName).Order().ToList() : [];
        teile.Add(vorhanden.Count > 0 ? "Bibliothek kataloge/familien: " + string.Join(", ", vorhanden) : "Vollen Pfad einer .familie.json angeben (die Bibliothek kataloge/familien ist leer).");
        return string.Join(" ", teile);
    }

    /// <summary>Ist die Familie (gleiche id) schon im Projekt, wirkt familie_laden wie familie_aktualisieren – eine Versionsübernahme, die nur ein
    /// Mensch macht. Für den Akteur «ki» (Herkunft ki) wird sie abgelehnt; neue Familien bleiben der KI erlaubt (Q1, Befund C3).</summary>
    static void NurNeueFuerKi(Kontext k, Element bisher)
    {
        if (k.Herkunft.Art != "ki") return;
        throw new HlksFehler($"Eine neuere Fassung übernimmt nur ein Mensch (Aktualisieren der Familie {bisher.Kennzeichen} «{bisher.Name}» – die Familie ist schon im Projekt, der Akteur «{k.Herkunft.Akteur}» ist eine KI).",
            "Den Punkt im Bericht nennen; ein Mensch übernimmt die neuere Fassung mit familie_aktualisieren. Neue Familien, die noch nicht im Projekt sind, kann die KI laden.");
    }

    /// <summary>Ordner bzw. Datei der Bürobibliothek (absolut oder relativ zu ihr) → Familienordner und Version bzw. Abzweig.</summary>
    static (string Ordner, string? Version)? BibliotheksPfad(string root, string datei)
    {
        string voll;
        try { voll = Path.GetFullPath(Path.IsPathRooted(datei) ? datei : Path.Combine(root, datei)); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return null; }
        var r = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (!voll.StartsWith(r + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return null;
        if (!File.Exists(voll) && !Directory.Exists(voll)) return null;
        var rel = Path.GetRelativePath(r, voll).Replace('\\', '/').TrimEnd('/');
        var teile = rel.Split('/');
        if (teile.Length < 2) return null;
        var famOrdner = $"{teile[0]}/{teile[1]}";
        string? version = null;
        if (teile.Length == 4 && teile[2] is "versionen" or "abzweige") version = teile[3].EndsWith(".familie.json", StringComparison.OrdinalIgnoreCase) ? teile[3][..^".familie.json".Length] : teile[3];
        else if (teile.Length != 2 && !(teile.Length == 3 && teile[2].Equals("familie.json", StringComparison.OrdinalIgnoreCase))) return null;
        return (famOrdner, version);
    }

    static void AusBibliothek(Kontext k, BibliotheksOrt ort, string? id, string? ordner, string? version)
    {
        if (!ort.Aktiv)
            throw new HlksFehler(ort.Sperre ?? "Die Familienbibliothek ist nicht eingestellt.",
                $"{FamilienBibliothekDienst.Umgebungsvariable} setzen (in Tests einen Ordner unter %TEMP%) oder eine Datei laden (datei).");
        var bib = FamilienBibliothekDienst.Lesen(ort, !k.Trockenlauf);
        var idx = bib.Index!;
        BibIndexEintrag? e;
        if (ordner is not null) e = idx.Familien.FirstOrDefault(f => string.Equals(f.Ordner, ordner, StringComparison.OrdinalIgnoreCase));
        else
        {
            e = idx.Familie(id);
            if (e is null)
            {
                var treffer = idx.Familien.Where(f => string.Equals(f.Name, id, StringComparison.OrdinalIgnoreCase)).ToList();
                if (treffer.Count > 1) throw new HlksFehler($"Mehrere Familien der Bibliothek heissen «{id}»: {string.Join(", ", treffer.Select(f => f.Id))}.", "Die Familie über ihre id angeben.");
                e = treffer.SingleOrDefault();
            }
        }
        if (e is null)
            throw new HlksFehler($"Familie «{id ?? ordner}» gibt es in der Familienbibliothek nicht.", Vorschlag(k));
        var (v, ab) = FamilienBibliothekSchreiben.VersionLesen(e, version);
        var bisher = k.Arbeit.DerKlasse(Familien.Klasse).FirstOrDefault(f => Familien.DefinitionsId(f) == e.Id);
        // Ohne NAS (seit 30.10.2026): laden aus dem lokalen Spiegel, wenn die Version dort liegt; Abzweige und das Nachführen einer
        // Familie, die schon im Projekt ist, brauchen die Bibliothek
        if (bib.Offline)
        {
            if (ab is not null || bisher is not null || !FamilienBibliothekDienst.ImSpiegel(ort, e, v))
                throw new HlksFehler($"Familienbibliothek nicht erreichbar ({ort.Pfad}) – laden geht erst wieder mit erreichbarer Bibliothek.", "Netzwerk/VPN prüfen und erneut versuchen.");
            k.Hinweise.Add($"Familienbibliothek nicht erreichbar – «{e.Name}» Version {v ?? e.Version} aus dem lokalen Spiegel geladen.");
        }
        if (bisher is not null)
        {
            NurNeueFuerKi(k, bisher);
            k.Hinweise.Add($"{bisher.Kennzeichen} «{bisher.Name}» ist schon im Projekt – sie wird wie mit familie_aktualisieren auf {(ab is null ? $"Version {v}" : $"Abzweig {ab}")} gebracht.");
            FamilieAktualisieren.Aktualisieren(k, bisher, ab ?? v?.ToString(System.Globalization.CultureInfo.InvariantCulture), "familie_laden");
            return;
        }
        var def = FamilienBibliothekDienst.FassungLesen(ort, e, v, ab);
        FamilienBibliothekSchreiben.Uebernehmen(k, ort, e, v, ab, def, null, "familie_laden", []);
    }

    /// <summary>Familie aus einer Datei (mitgeliefert oder voller Pfad) – wie bisher, bibliothek = { datei, pruefsumme der Datei }.</summary>
    void DateiLaden(Kontext k, string pfad, string ordner)
    {
        byte[] bytes;
        try { bytes = File.ReadAllBytes(pfad); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { throw new HlksFehler($"Familiendatei «{pfad}» nicht lesbar: {ex.Message}", "Datei und Zugriffsrechte prüfen."); }
        JsonObject def;
        try { def = JsonNode.Parse(bytes) as JsonObject ?? throw new HlksFehler($"«{pfad}» enthält kein JSON-Objekt.", "Datei nach schema/hlks-familie.schema.json prüfen."); }
        catch (JsonException ex) { throw new HlksFehler($"«{pfad}» ist kein gültiges JSON: {ex.Message}", "Datei nach schema/hlks-familie.schema.json prüfen."); }
        var pruefsumme = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var inKatalog = Path.GetFullPath(pfad).StartsWith(Path.GetFullPath(ordner) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        var relativ = inKatalog ? Path.GetRelativePath(ordner, pfad).Replace('\\', '/') : Path.GetFullPath(pfad);
        var bibliothek = new JsonObject { ["datei"] = relativ, ["pruefsumme"] = pruefsumme, ["art"] = inKatalog ? "mitgeliefert" : "datei" };

        var id = def["id"] is JsonValue iv && iv.TryGetValue<string>(out var s) ? s : null;
        var name = def["name"] is JsonValue nv && nv.TryGetValue<string>(out var n) ? n.Trim() : null;
        var bisher = k.Arbeit.DerKlasse(Familien.Klasse).FirstOrDefault(f => id is not null && Familien.DefinitionsId(f) == id)
            ?? (id is null ? k.Arbeit.DerKlasse(Familien.Klasse).FirstOrDefault(f => string.Equals(f.Name?.Trim(), name, StringComparison.OrdinalIgnoreCase)) : null);
        var status = def["status"] is JsonValue sv && sv.TryGetValue<string>(out var st) ? st : "entwurf";
        if (bisher is not null) NurNeueFuerKi(k, bisher);
        if (bisher is not null && bisher.Eigenschaft("definition")?.Inhalt is JsonObject alt && FamilienBefehle.Inhalt(alt) == FamilienBefehle.Inhalt(def)
            && bisher.Text("status") == status && (bisher.Eigenschaft("bibliothek")?.Inhalt as JsonObject)?["pruefsumme"]?.ToString() == pruefsumme)
        {
            k.Hinweise.Add($"Keine Änderung: {bisher.Kennzeichen} «{bisher.Name}» entspricht der Datei.");
            return;
        }
        int version = bisher is null ? (Json.Zahl(def["version"]) is double v && v >= 1 ? (int)v : 1) : FamilienBefehle.Version(bisher) + 1;
        FamilienBefehle.Speichern(k, bisher, def, Name, status, version, bibliothek);
        if (bisher is not null) k.Hinweise.Add($"{bisher.Kennzeichen} aus der Bibliothek aktualisiert ({relativ}).");
    }
}

public sealed class FamiliePlatzieren : IBefehl
{
    public string Name => "familie_platzieren";
    public const string Text = "Legt ein Element der Kategorie einer Familie an und platziert die Familie: Einfügepunkt (Weltkoordinaten in mm; ohne z auf der Kote des Geschosses), Drehung um z (Grad, Gegenuhrzeigersinn) oder volle Ausrichtung im Raum (ausrichtung { x, z }, geht drehung vor – z. B. Steig- und Fallbögen), Typ und Exemplarwerte. Das Element bekommt bezuege.familie, familientyp, drehung, ausrichtung, parameter, die Anschlüsse der Familie (Name, Medium, Fluss, DN bzw. Kanal d, b, h) und die Kennwerte der Parameter mit «ziel» (Herkunft familie). Geschoss und Raum werden aus der Lage abgeleitet, wenn sie fehlen.";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("familie", "text", true, "Familie (Kennzeichen, Name oder id)"),
        new("typ", "text", true, "Typ aus der Typenliste der Familie"),
        new("punkt", "liste", true, "Einfügepunkt [x, y] oder [x, y, z] in mm"),
        new("drehung", "zahl", false, "Drehung um z in Grad, Gegenuhrzeigersinn (0 = Vorderseite nach Süden, −y)"),
        new("ausrichtung", "objekt", false, "Volle Ausrichtung { x: [dx, dy, dz], z: [dx, dy, dz] }: lokale x- und z-Achse der Familie in Weltkoordinaten, rechtwinklig; geht drehung vor (z. B. Steigbogen x = [1, 0, 0], z = [0, -1, 0])"),
        new("parameter", "objekt", false, "Exemplarwerte { name: wert } (nur Exemplarparameter ohne Formel)"),
        new("kennzeichen", "text", false, "Kennzeichen; fehlt es, wird es aus dem Klassenkürzel gebildet"),
        new("geschoss", "text", false, "Geschoss (Kennzeichen oder Id)"),
        new("raum", "text", false, "Raum (Kennzeichen oder Id)"),
        new("system", "text", false, "System (Kennzeichen oder Id)"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var fam = Familien.Hole(k.Arbeit, Param.Text(p, "familie", true));
        var def = Familien.Definition(fam, k.Katalog);
        if (!def.Gueltig) throw FamilienPruefung.Fehlermeldung($"Familie {fam.Kennzeichen} «{fam.Name}»", def.Fehler);
        var d = k.Katalog.Klassen[def.Kategorie];
        if (def.IstLeitungsserie)
            throw new HlksFehler($"{fam.Kennzeichen} «{fam.Name}» ist eine Leitungsserie – sie wird nicht platziert, sondern Leitungen benutzen sie.",
                $"Leitung ziehen (leitung_ziehen bzw. leitung_zeichnen) und ihr bezuege.familie = {fam.Kennzeichen} und eigenschaften.familientyp = <Grösse> geben; leitungsserie zeigt die Grössen.");
        if (d.Abstrakt)
            throw new HlksFehler($"Die Kategorie {d.Name} der Familie {fam.Kennzeichen} ist nur eine Basisklasse.",
                $"element_anlegen mit einer konkreten Klasse ({string.Join(", ", k.Katalog.Klassen.Values.Where(x => !x.Abstrakt && x.IstArt(d.Name)).Select(x => x.Name).Order())}) und bezuege.familie = {fam.Kennzeichen}, eigenschaften.familientyp.");
        var typ = Param.Text(p, "typ", true)!;
        if (def.Typ(typ) is null) throw new HlksFehler($"Typ «{typ}» gibt es in der Familie {fam.Kennzeichen} «{fam.Name}» nicht.", "Typen: " + string.Join(", ", def.Typen.Select(t => t.Name)));
        var ex = p["parameter"] switch
        {
            null => null,
            JsonObject o => o,
            _ => throw new HlksFehler("Parameter «parameter» muss ein Objekt sein.", "Form: { \"bodenabstand\": 200 }"),
        };
        var exFehler = FamilienAuswertung.ExemplarPruefen(def, ex);
        if (exFehler.Count > 0) throw FamilienPruefung.Fehlermeldung("Exemplarwerte", exFehler);
        var a = FamilienAuswertung.Aufloesen(def, typ, ex);
        if (!a.Gueltig) throw FamilienPruefung.Fehlermeldung($"Familie {fam.Kennzeichen} Typ «{typ}»", a.Fehler);
        if (p["punkt"] is not JsonArray pa || pa.Count is < 2 or > 3)
            throw new HlksFehler("Parameter «punkt» fehlt oder ist kein Punkt.", "Einfügepunkt als [x, y] oder [x, y, z] in mm angeben.");
        var punkt = pa.Select(c => Json.Zahl(c) is double z && double.IsFinite(z) ? z
            : throw new HlksFehler("punkt: eine Koordinate ist keine Zahl.", "Einfügepunkt als [x, y] oder [x, y, z] in mm angeben.")).ToArray();

        var ausrichtung = Familien.AusrichtungLesen(p["ausrichtung"]);   // Modul 3: geprüft vor dem Anlegen (Fehler mit Vorschlag)
        var eig = new JsonObject { ["familientyp"] = typ, ["drehung"] = Param.Zahl(p, "drehung") ?? 0 };
        if (ausrichtung is not null) eig["ausrichtung"] = p["ausrichtung"]!.DeepClone();
        if (ex is { Count: > 0 }) eig["parameter"] = ex.DeepClone();
        var anlegen = new JsonObject
        {
            ["klasse"] = d.Name,
            ["kennzeichen"] = Param.Text(p, "kennzeichen"),
            ["eigenschaften"] = eig,
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = new JsonArray(new JsonArray(punkt.Select(x => (JsonNode)x).ToArray())) },
            ["anschluesse"] = new JsonArray(a.Anschluesse.Where(x => x.Sichtbar).Select(x => (JsonNode)new JsonObject
            {
                ["name"] = x.Name, ["medium"] = x.Medium, ["fluss"] = x.Fluss, ["dn"] = Familien.Ganz(x.Dn), ["d"] = Familien.Ganz(x.D), ["b"] = Familien.Ganz(x.B), ["h"] = Familien.Ganz(x.H), ["gewinde"] = x.Gewinde,
            }).ToArray()),
            ["bezuege"] = new JsonObject { [Familien.Bezug] = fam.Id },
        };
        foreach (var n in new[] { "geschoss", "raum", "system" })
            if (Param.Text(p, n) is { } wert) anlegen[n] = wert;
        new ElementAnlegen().Ausfuehren(k, anlegen);
        var neu = k.Arbeit.Finde(k.Operationen[^1].Id)!;
        string R(V3 v) => "[" + string.Join(", ", Familien.Runden(v.Feld(), 4).Select(x => x.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture))) + "]";
        k.Hinweise.Add($"{neu.Kennzeichen}: Familie {fam.Kennzeichen} «{fam.Name}», Typ «{typ}» bei [{string.Join(", ", (Familien.Einfuegepunkt(neu) ?? punkt).Select(x => Grafik.Format(x, 0)))}], " +
            (ausrichtung is { } au ? $"Ausrichtung x = {R(au.X)}, z = {R(au.Z)}." : $"Drehung {Grafik.Format(Param.Zahl(p, "drehung") ?? 0, 1)}°."));
    }
}
