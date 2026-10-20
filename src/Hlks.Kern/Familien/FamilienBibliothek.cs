// Familien (Modul 2, B1 Nachtrag 2; Nachtrag B7, entwurf/modul-2-bibliothek.md): Abfrage familien_bibliothek – die Familien der
// projektübergreifenden Familienbibliothek (Bürobibliothek) und die mitgelieferten aus kataloge/familien (nur lesbar), je mit Stand
// zum Projekt, Sichtbarkeit nach Projektinformation.familienAuswahl und Hinweis auf mögliche Doppel. Mit «familie»: Versionen,
// Abzweige, Log und Vorschau einer Übernahme (für «Aktualisieren …» mit Vorschau in der Oberfläche).
//
// Stand einer Familie des Projekts (auch Abfrage familien, Feld bibliothekStand) – über die Inhaltsprüfsumme, nicht über den Marker:
//   aktuell              die Fassung des Projekts ist die aktuelle Version
//   neuere_version       die Fassung ist eine ältere Version (bzw. ein entschiedener Abzweig): Übernahme per familie_aktualisieren
//   im_projekt_geaendert die Fassung ist (noch) nicht in der Bibliothek – Warteschlange (warteschlange = true) oder nicht geschrieben
//   parallel_geaendert   die Fassung ist ein offener Abzweig, oder sie ist aktuell und es gibt offene Abzweige
//   nicht_in_bibliothek  die Familie gibt es in der Bibliothek nicht
//   gesperrt             Bibliothek gesperrt (Testlauf ohne HLKS_FAMILIENBIBLIOTHEK)
// Bibliotheksfamilien, die nicht im Projekt sind: nicht_im_projekt.
// Sichtbar (Explorer): im Projekt immer; sonst nur mit familienAuswahl – aus[] nie, ein[] immer, sonst alle nicht leeren Listen
//   (gewerke, kategorien, hersteller, status) treffen (Gross/klein egal).
// ANNAHMEN (Prüfliste): Doppel = gleicher Hersteller und gleiches Produkt (getrimmt, Gross/klein egal, beide nicht leer) bei anderer id,
//   über beide Quellen; Prüfsumme der mitgelieferten Dateien wie bisher SHA-256 der Datei («aktuell» = die Familie stammt aus genau
//   dieser Datei).

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class FamilienBibliothek
{
    public const string Text = "Familien der Familienbibliothek (Bürobibliothek, projektübergreifend; Pfad aus kataloge/buero.json bzw. HLKS_FAMILIENBIBLIOTHEK) und die mitgelieferten aus kataloge/familien (nur lesbar): " +
        "id, Name, Kategorie, Gewerk, Hersteller, Produkt, Status, Version, Kennzeichen im Projekt (imProjekt), Stand zum Projekt (nicht_im_projekt, aktuell, neuere_version, im_projekt_geaendert, " +
        "parallel_geaendert), sichtbar nach der Familienauswahl des Projekts (familienauswahl_setzen; Familien des Projekts immer) und mögliche Doppel (gleicher Hersteller und Produkt, andere id). " +
        "filter = Schnellsuche, auswahl = true nur die sichtbaren. Mit familie: Versionen, Abzweige, Log und Vorschau einer Übernahme (version wählt die Fassung). bibliothek nennt Pfad, " +
        "Erreichbarkeit, Warteschlange und Hinweis (Statusleiste). Laden mit familie_laden { id }, übernehmen mit familie_aktualisieren.";

    public static readonly IReadOnlyList<ParameterDef> Parameter =
    [
        new("filter", "text", false, "Schnellsuche in Name, id, Kategorie, Hersteller, Produkt (Teiltext, Gross/klein egal)"),
        new("auswahl", "bool", false, "true = nur die nach der Familienauswahl des Projekts sichtbaren (Familien des Projekts immer)"),
        new("familie", "text", false, "Eine Familie im Detail (id oder Name in der Bibliothek, oder Kennzeichen im Projekt): Versionen, Abzweige, Log, Vorschau"),
        new("version", "text", false, "Mit familie: Fassung für die Vorschau (Nummer oder Abzweig; Vorgabe die aktuelle Version)"),
    ];

    /// <summary>Ordner der mitgelieferten Familien (kataloge/familien, nur lesbar).</summary>
    public static string Ordner(Katalog k) => Path.Combine(k.Ordner, "familien");

    static string? T(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    /// <summary>Projektinformation.familienAuswahl (null = nur die Familien des Projekts).</summary>
    public static JsonObject? Auswahl(Modell m) => FamilienBibliothekSchreiben.Projektinformation(m)?.Eigenschaft("familienAuswahl")?.Inhalt as JsonObject;

    public static string? Gewerk(Katalog k, string? kategorie) => kategorie is not null && k.Klassen.TryGetValue(kategorie, out var d) ? d.Gewerk : null;

    /// <summary>Sichtbar nach der Familienauswahl (ohne die Regel «im Projekt immer»).</summary>
    public static bool Sichtbar(JsonObject? auswahl, string? id, string? kategorie, string? gewerk, string? hersteller, string? status)
    {
        if (auswahl is null) return false;
        static List<string> L(JsonNode? n) => (n as JsonArray ?? []).Select(x => T(x)).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()).ToList();
        bool Trifft(string feld, string? wert)
        {
            var l = L(auswahl[feld]);
            if (feld == "gewerke") l = l.Select(KaelteAlias.Gewerk).ToList();   // gespeichertes «kaelte» = heizung (Entscheid Christian 20.10.2026)
            return l.Count == 0 || (wert is not null && l.Contains(wert.Trim(), StringComparer.OrdinalIgnoreCase));
        }
        if (id is not null && L(auswahl["aus"]).Contains(id, StringComparer.OrdinalIgnoreCase)) return false;
        if (id is not null && L(auswahl["ein"]).Contains(id, StringComparer.OrdinalIgnoreCase)) return true;
        return Trifft("gewerke", gewerk) && Trifft("kategorien", kategorie) && Trifft("hersteller", hersteller) && Trifft("status", status ?? "entwurf");
    }

    static JsonObject S(string art, string text) => new() { ["art"] = art, ["text"] = text };

    /// <summary>Stand einer Familie des Projekts zur Bibliothek (Arten im Dateikopf).</summary>
    public static JsonObject Stand(Element fam, BibLesen bib, IReadOnlyList<BibAuftrag> ausstehend)
    {
        if (!bib.Ort.Aktiv) return S("gesperrt", bib.Ort.Sperre ?? "Familienbibliothek gesperrt.");
        var id = Familien.DefinitionsId(fam);
        var h = FamilienBibliothekLogik.Inhaltspruefsumme(fam);
        if (id is null || h is null) return S("nicht_in_bibliothek", "Familie ohne feste Kennung (definition.id) – nicht in der Bibliothek.");
        bool wartet = ausstehend.Any(a => a.Id == id && a.Pruefsumme == h);
        var e = bib.Index?.Familie(id);
        JsonObject R(string art, string text, int? version, string? abzweig)
        {
            var o = S(art, text);
            o["version"] = version;
            o["abzweig"] = abzweig;
            o["aktuelleVersion"] = e?.Version;
            o["abzweigeOffen"] = new JsonArray((e?.OffeneAbzweige ?? []).Select(a => (JsonNode)JsonValue.Create(a.Abzweig)).ToArray());
            o["warteschlange"] = wartet;
            o["offline"] = bib.Offline;
            o["ordner"] = e?.Ordner;
            return o;
        }
        if (e is null)
            return wartet ? R("im_projekt_geaendert", "neu im Projekt – wartet in der Warteschlange auf die Bibliothek (NAS nicht erreichbar).", null, null)
                          : R("nicht_in_bibliothek", "nicht in der Bibliothek – familie_in_bibliothek schreibt sie (Änderungen schreibt der Kern automatisch).", null, null);
        var (v, ab) = FamilienBibliothekLogik.Lage(e, h, FamilienBibliothekLogik.Marker(fam));
        var offen = e.OffeneAbzweige.ToList();
        string Projekte(IEnumerable<BibIndexAbzweig> l) => string.Join(", ", l.Select(a => $"{a.Abzweig} ({a.Projekt ?? "?"})"));
        if (v is null && ab is null)
            return wartet ? R("im_projekt_geaendert", $"im Projekt geändert – wartet in der Warteschlange (Bibliothek: Version {e.Version}).", null, null)
                          : R("im_projekt_geaendert", $"im Projekt geändert – noch nicht in der Bibliothek (Version {e.Version} ist dort aktuell); familie_in_bibliothek schreibt sie.", null, null);
        if (ab is not null)
        {
            var a = e.Abzweige.First(x => x.Abzweig == ab);
            return a.Offen
                ? R("parallel_geaendert", $"parallel geändert: diese Fassung ist Abzweig {ab}, aktuell ist Version {e.Version} ({e.Projekt ?? "?"}). Entscheiden (nur ein Mensch): " +
                    "familie_aktualisieren (Bibliothek übernehmen) oder familie_in_bibliothek mit als_aktuell (diese Fassung gilt).", null, ab)
                : R("neuere_version", $"neuere Version {e.Version} in der Bibliothek (Abzweig {ab} ist entschieden: {a.Ergebnis}) – familie_aktualisieren.", null, ab);
        }
        if (v == e.Version)
            return offen.Count > 0
                ? R("parallel_geaendert", $"parallel geändert: aktuell (Version {v}), aber offene Abzweige {Projekte(offen)}. Entscheiden (nur ein Mensch): familie_in_bibliothek mit " +
                    "als_aktuell (diese Fassung gilt) bzw. im anderen Projekt familie_aktualisieren.", v, null)
                : R("aktuell", $"aktuell (Version {v}).", v, null);
        return R("neuere_version", $"neuere Version {e.Version} in der Bibliothek (Projekt: Version {v}) – Übernahme mit familie_aktualisieren (Vorschau im Trockenlauf)" +
            (offen.Count > 0 ? $"; parallel geändert: {Projekte(offen)}" : "") + ".", v, null);
    }

    /// <summary>bibliothekStand je Familie des Projekts (Abfrage familien).</summary>
    public static Func<Element, JsonObject> StandFuer(Projekt p)
    {
        var ort = FamilienBibliothekDienst.Ort(p.Katalog);
        BibLesen? bib = null;
        IReadOnlyList<BibAuftrag>? ausstehend = null;
        return f =>
        {
            bib ??= FamilienBibliothekDienst.Lesen(ort);
            ausstehend ??= FamilienBibliothekDienst.Ausstehend(ort);
            return Stand(f, bib, ausstehend);
        };
    }

    static JsonObject BibliothekInfo(BibLesen bib) => new()
    {
        ["pfad"] = bib.Ort.Pfad, ["quelle"] = bib.Ort.Quelle, ["aktiv"] = bib.Ort.Aktiv, ["gesperrt"] = bib.Ort.Sperre,
        ["erreichbar"] = bib.Erreichbar, ["offline"] = bib.Offline, ["stand"] = bib.Index?.Stand,
        ["warteschlange"] = bib.Warteschlange, ["nachgeholt"] = bib.Nachgeholt,
        ["hinweis"] = bib.Hinweis ?? (bib.Nachgeholt > 0 ? $"{bib.Nachgeholt} ausstehende(r) Schreibvorgang/-vorgänge nachgeschrieben." : null),
        ["fehler"] = bib.Fehler.Count > 0 ? new JsonArray(bib.Fehler.Select(x => (JsonNode)JsonValue.Create(x)).ToArray()) : null,
    };

    /// <summary>Abfrage familien_bibliothek (Parameter siehe <see cref="Parameter"/>).</summary>
    public static JsonObject Liste(Projekt p, JsonObject? par = null)
    {
        par ??= [];
        var filter = Param.Text(par, "filter")?.Trim();
        bool nurAuswahl = Param.Bool(par, "auswahl");
        var detail = Param.Text(par, "familie")?.Trim();
        var version = Param.Text(par, "version")?.Trim();
        var ort = FamilienBibliothekDienst.Ort(p.Katalog);
        var bib = FamilienBibliothekDienst.Lesen(ort);
        var ausstehend = FamilienBibliothekDienst.Ausstehend(ort);
        var auswahl = Auswahl(p.Modell);
        var imProjekt = new Dictionary<string, Element>(StringComparer.Ordinal);
        foreach (var f in p.Modell.DerKlasse(Familien.Klasse).OrderBy(f => f.Kennzeichen, StringComparer.OrdinalIgnoreCase))
            if (Familien.DefinitionsId(f) is { } id) imProjekt.TryAdd(id, f);

        var liste = new List<JsonObject>();
        foreach (var e in bib.Index?.Familien ?? [])
        {
            var gewerk = Gewerk(p.Katalog, e.Kategorie);
            var fam = imProjekt.GetValueOrDefault(e.Id);
            liste.Add(new JsonObject
            {
                ["quelle"] = "bibliothek", ["id"] = e.Id, ["name"] = e.Name, ["kategorie"] = e.Kategorie, ["gewerk"] = gewerk, ["hersteller"] = e.Hersteller,
                ["produkt"] = e.Produkt, ["status"] = e.Status, ["version"] = e.Version, ["versionen"] = e.Versionen.Count, ["pruefsumme"] = e.Pruefsumme,
                ["ordner"] = e.Ordner, ["zeit"] = e.Zeit, ["projekt"] = e.Projekt, ["akteur"] = e.Akteur, ["akteurArt"] = e.AkteurArt, ["typen"] = e.Typen,
                ["unterlagen"] = e.Unterlagen,
                ["abzweigeOffen"] = new JsonArray(e.OffeneAbzweige.Select(a => (JsonNode)JsonValue.Create(a.Abzweig)).ToArray()),
                ["imProjekt"] = fam?.Kennzeichen,
                ["stand"] = fam is null ? S("nicht_im_projekt", $"nicht im Projekt – familie_laden {{ id: «{e.Id}» }}.") : Stand(fam, bib, ausstehend),
                ["sichtbar"] = fam is not null || Sichtbar(auswahl, e.Id, e.Kategorie, gewerk, e.Hersteller, e.Status),
            });
        }
        var (mitgeliefert, hinweis) = Mitgeliefert(p, imProjekt, auswahl);
        liste.AddRange(mitgeliefert);

        // mögliche Doppel: gleicher Hersteller + Produkt, andere id (beide Quellen)
        var doppel = new JsonArray();
        static string N(string? s) => (s ?? "").Trim().ToLowerInvariant();
        foreach (var g in liste.Where(o => N(T(o["hersteller"])).Length > 0 && N(T(o["produkt"])).Length > 0 && T(o["id"]) is not null)
                     .GroupBy(o => (N(T(o["hersteller"])), N(T(o["produkt"])))))
        {
            var ids = g.Select(o => T(o["id"])!).Distinct(StringComparer.Ordinal).ToList();
            if (ids.Count < 2) continue;
            foreach (var o in g) o["doppel"] = new JsonArray(ids.Where(i => i != T(o["id"])).Select(i => (JsonNode)JsonValue.Create(i)).ToArray());
            doppel.Add(new JsonObject
            {
                ["hersteller"] = T(g.First()["hersteller"]), ["produkt"] = T(g.First()["produkt"]),
                ["familien"] = new JsonArray(g.Select(o => (JsonNode)new JsonObject { ["id"] = T(o["id"]), ["name"] = T(o["name"]), ["quelle"] = T(o["quelle"]) }).ToArray()),
                ["hinweis"] = "Mögliches Doppel: gleicher Hersteller und gleiches Produkt mit verschiedener id – prüfen, ob eine Familie genügt (wird nicht zusammengeführt).",
            });
        }

        int gesamt = liste.Count;
        if (!string.IsNullOrEmpty(filter))
            liste = liste.Where(o => new[] { "name", "id", "kategorie", "hersteller", "produkt", "datei", "ordner" }
                .Any(f => T(o[f]) is { } s && s.Contains(filter, StringComparison.OrdinalIgnoreCase))).ToList();
        if (nurAuswahl) liste = liste.Where(o => o["sichtbar"]?.GetValue<bool>() == true).ToList();

        var aus = new JsonObject
        {
            ["bibliothek"] = BibliothekInfo(bib),
            ["auswahl"] = auswahl?.DeepClone(),
            ["anzahl"] = new JsonObject
            {
                ["bibliothek"] = bib.Index?.Familien.Count ?? 0, ["mitgeliefert"] = mitgeliefert.Count, ["gesamt"] = gesamt, ["angezeigt"] = liste.Count,
                ["sichtbar"] = liste.Count(o => o["sichtbar"]?.GetValue<bool>() == true),
            },
            ["familien"] = new JsonArray(liste.Select(o => (JsonNode)o).ToArray()),
            ["doppel"] = doppel,
            ["ordner"] = Ordner(p.Katalog),
            ["hinweis"] = hinweis,
        };
        if (detail is { Length: > 0 }) aus["familie"] = Detail(p, bib, ort, detail, version, imProjekt);
        return aus;
    }

    /// <summary>Mitgelieferte Familien (Dateien &lt;name&gt;.familie.json direkt in kataloge/familien) wie bisher, mit quelle «mitgeliefert».</summary>
    static (List<JsonObject> Liste, string? Hinweis) Mitgeliefert(Projekt p, Dictionary<string, Element> imProjekt, JsonObject? auswahl)
    {
        var ordner = Ordner(p.Katalog);
        var dateien = Directory.Exists(ordner) ? Directory.GetFiles(ordner, "*.familie.json").Order(StringComparer.OrdinalIgnoreCase).ToList() : [];
        var liste = new List<JsonObject>();
        foreach (var pfad in dateien)
        {
            var o = new JsonObject { ["quelle"] = "mitgeliefert", ["datei"] = Path.GetFileName(pfad) };
            try
            {
                var bytes = File.ReadAllBytes(pfad);
                o["pruefsumme"] = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                if (JsonNode.Parse(bytes) is not JsonObject d) { o["fehler"] = "kein JSON-Objekt"; liste.Add(o); continue; }
                var id = T(d["id"]);
                var kategorie = T(d["kategorie"]);
                var gewerk = Gewerk(p.Katalog, kategorie);
                o["name"] = T(d["name"]); o["id"] = id; o["kategorie"] = kategorie; o["gewerk"] = gewerk; o["hersteller"] = T(d["hersteller"]); o["produkt"] = T(d["produkt"]);
                o["version"] = Json.Zahl(d["version"]); o["status"] = T(d["status"]);
                o["typen"] = d["typen"] is JsonArray t ? t.Count : 0;
                var vorhanden = id is null ? null : imProjekt.GetValueOrDefault(id);
                o["imProjekt"] = vorhanden?.Kennzeichen;
                o["aktuell"] = vorhanden is null ? null : T(FamilienBibliothekLogik.Marker(vorhanden)?["pruefsumme"]) == o["pruefsumme"]!.GetValue<string>();
                o["sichtbar"] = vorhanden is not null || Sichtbar(auswahl, id, kategorie, gewerk, T(d["hersteller"]), T(d["status"]));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                o["fehler"] = ex.Message;
                o["sichtbar"] = false;
            }
            liste.Add(o);
        }
        return (liste, Directory.Exists(ordner) ? null : "Die Bibliothek kataloge/familien gibt es noch nicht (keine Familien gespeichert).");
    }

    /// <summary>Eine Familie der Bibliothek im Detail: Versionen, Abzweige, Log, Unterlagen und Vorschau gegenüber dem Projekt.</summary>
    static JsonObject Detail(Projekt p, BibLesen bib, BibliotheksOrt ort, string bezug, string? version, Dictionary<string, Element> imProjekt)
    {
        if (!ort.Aktiv) throw new HlksFehler(ort.Sperre ?? "Familienbibliothek gesperrt.", $"{FamilienBibliothekDienst.Umgebungsvariable} setzen (in Tests einen Ordner unter %TEMP%).");
        var idx = bib.Index ?? new BibIndex();
        var e = idx.Familie(bezug)
            ?? (p.Modell.Finde(bezug) is { Klasse: Familien.Klasse } pf && Familien.DefinitionsId(pf) is { } pid ? idx.Familie(pid) : null)
            ?? (idx.Familien.Where(f => string.Equals(f.Name, bezug, StringComparison.OrdinalIgnoreCase)).ToList() is { Count: 1 } l ? l[0] : null)
            ?? throw new HlksFehler($"Familie «{bezug}» gibt es in der Bibliothek nicht.",
                idx.Familien.Count > 0 ? "Familien: " + string.Join(", ", idx.Familien.Take(30).Select(f => $"{f.Id} «{f.Name}»")) : "Die Bibliothek ist leer.");
        var log = FamilienBibliothekDienst.Log(ort, e);
        var fam = imProjekt.GetValueOrDefault(e.Id);
        var o = new JsonObject
        {
            ["id"] = e.Id, ["name"] = e.Name, ["kategorie"] = e.Kategorie, ["hersteller"] = e.Hersteller, ["produkt"] = e.Produkt, ["status"] = e.Status,
            ["version"] = e.Version, ["ordner"] = e.Ordner, ["imProjekt"] = fam?.Kennzeichen,
            ["stand"] = fam is null ? S("nicht_im_projekt", "nicht im Projekt.") : Stand(fam, bib, FamilienBibliothekDienst.Ausstehend(ort)),
            ["versionen"] = new JsonArray(e.Versionen.OrderByDescending(v => v.Version).Select(v => (JsonNode)new JsonObject
            {
                ["version"] = v.Version, ["aktuell"] = v.Version == e.Version, ["status"] = v.Status, ["zeit"] = v.Zeit, ["projekt"] = v.Projekt, ["akteur"] = v.Akteur,
                ["akteurArt"] = v.AkteurArt, ["basis"] = v.Basis, ["basisAbzweig"] = v.BasisAbzweig, ["pruefsumme"] = v.Pruefsumme,
            }).ToArray()),
            ["abzweige"] = new JsonArray(e.Abzweige.Select(a => (JsonNode)new JsonObject
            {
                ["abzweig"] = a.Abzweig, ["offen"] = a.Offen, ["ergebnis"] = a.Ergebnis, ["basis"] = a.Basis, ["basisAbzweig"] = a.BasisAbzweig, ["status"] = a.Status,
                ["zeit"] = a.Zeit, ["projekt"] = a.Projekt, ["akteur"] = a.Akteur, ["akteurArt"] = a.AkteurArt, ["pruefsumme"] = a.Pruefsumme,
            }).ToArray()),
            ["log"] = log is null ? null : JsonSerializer.SerializeToNode(log.Eintraege, Json.Optionen),
        };
        if (bib.Offline) { o["hinweis"] = "Bibliothek nicht erreichbar – Vorschau erst mit erreichbarer Bibliothek."; return o; }
        var (v, ab) = FamilienBibliothekSchreiben.VersionLesen(e, version);
        var def = FamilienBibliothekDienst.FassungLesen(ort, e, v, ab);
        o["fassung"] = ab ?? $"Version {v}";
        o["unterlagen"] = new JsonArray((def["unterlagen"] as JsonArray ?? []).OfType<JsonObject>().Select(u =>
        {
            var bu = new BibUnterlage { Pruefsumme = T(u["pruefsumme"]) ?? "", Datei = T(u["datei"]), Art = T(u["art"]) };
            return (JsonNode)new JsonObject { ["datei"] = bu.Datei, ["art"] = bu.Art, ["pruefsumme"] = bu.Pruefsumme, ["inBibliothek"] = FamilienBibliothekDienst.UnterlageInBibliothek(ort, bu) is not null };
        }).ToArray());
        if (fam?.Eigenschaft("definition")?.Inhalt is JsonObject alt && FamilienBibliothekLogik.Inhaltspruefsumme(alt) != FamilienBibliothekLogik.Inhaltspruefsumme(def))
            o["vorschau"] = FamilienVorschau.Vergleichen(p.Modell, p.Katalog, fam, alt, def, $"Projekt → {(ab is null ? $"Bibliothek Version {v}" : $"Abzweig {ab}")}").Daten;
        return o;
    }
}
