// Familien (Modul 2, Nachtrag B7): Vorschau einer Übernahme aus der Familienbibliothek (familie_aktualisieren, Abfrage
// familien_bibliothek { familie, version }) – was sich ändert (Kopf, Parameter, Typen, Körper, Anschlüsse, Sinnbild, Leitung) und was
// das für die platzierten Elemente heisst: Typ entfällt, Anschluss entfällt (verbunden mit …), Anschluss verschiebt sich um n mm,
// Grösse ändert sich. Die Lagen rechnet der Kern wie für anschlusslagen (Familie, Einfügepunkt, Ausrichtung, Exemplarwerte).
// ANNAHME (Prüfliste): eine Verschiebung unter 0.5 mm wird nicht gemeldet (Rundung der Formeln).

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

public sealed class FamilienVorschau
{
    /// <summary>Lesbare Zeilen; die erste ist die Zusammenfassung.</summary>
    public List<string> Zeilen { get; } = [];
    /// <summary>Strukturiert: { titel, kopf[], parameter[], typen[], koerper[], anschluesse[], weitere[], elemente[], zeilen[] }.</summary>
    public JsonObject Daten { get; } = [];
    /// <summary>Typen, die entfallen, aber von Elementen benutzt werden (Übernahme nicht möglich).</summary>
    public List<(string Typ, List<string> Elemente)> FehlendeTypen { get; } = [];

    const double Schwelle = 0.5;   // mm

    static string? T(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
    static string F(double d) => Math.Round(d, 1).ToString("0.#", CultureInfo.InvariantCulture);
    static string Kurz(JsonNode? n)
    {
        var s = n?.ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) ?? "–";
        return s.Length > 80 ? s[..77] + "…" : s;
    }

    public static FamilienVorschau Vergleichen(Modell m, Katalog kat, Element fam, JsonObject alt, JsonObject neu, string titel)
    {
        var v = new FamilienVorschau();
        var details = new List<string>();
        var kopf = new JsonArray();
        foreach (var f in new[] { "name", "kategorie", "hersteller", "produkt", "beschreibung", "status" })
        {
            var a = T(alt[f]); var b = T(neu[f]);
            if (a == b) continue;
            kopf.Add(new JsonObject { ["feld"] = f, ["alt"] = a, ["neu"] = b });
            details.Add($"Kopf: {f} «{a ?? "–"}» → «{b ?? "–"}»");
        }
        var par = Liste(alt, neu, "parameter", "name", false, "Parameter", details, (x, y) =>
        {
            var teile = new List<string>();
            foreach (var f in new[] { "formel", "wert", "art", "datentyp", "einheit", "gruppe", "ziel" })
                if (!JsonNode.DeepEquals(x[f], y[f])) teile.Add(f == "formel" ? $"Formel «{T(x[f]) ?? "–"}» → «{T(y[f]) ?? "–"}»" : $"{f} {Kurz(x[f])} → {Kurz(y[f])}");
            return teile;
        });
        var typen = Liste(alt, neu, "typen", "name", false, "Typ", details, (x, y) =>
        {
            var teile = new List<string>();
            var wa = x["werte"] as JsonObject ?? []; var wb = y["werte"] as JsonObject ?? [];
            foreach (var k in wa.Select(p => p.Key).Union(wb.Select(p => p.Key)).Order(StringComparer.Ordinal))
                if (!Gleich(wa[k], wb[k])) teile.Add($"{k} {Kurz(wa[k])} → {Kurz(wb[k])}");
            if (!JsonNode.DeepEquals(x["quellen"], y["quellen"])) teile.Add("Quellen geändert");
            return teile;
        });
        var koerper = Liste(alt, neu, "koerper", "id", false, "Körper", details, (x, y) => ["geändert"]);
        var ansch = Liste(alt, neu, "anschluesse", "name", true, "Anschluss", details, (x, y) =>
        {
            var teile = new List<string>();
            foreach (var f in new[] { "medium", "fluss", "art", "form", "dn", "d", "b", "h", "lage", "richtung", "gewinde" })
                if (!JsonNode.DeepEquals(x[f], y[f])) teile.Add($"{f} {Kurz(x[f])} → {Kurz(y[f])}");
            return teile;
        });
        var weitere = new JsonArray();
        foreach (var (f, text) in new[] { ("sinnbild", "Sinnbild"), ("schemasymbol", "Schemasymbol"), ("leitung", "Leitungsserie (Block leitung)"), ("masse", "Massbezüge"),
                     ("konflikte", "Konflikte"), ("abdeckung", "Abdeckung"), ("unterlagen", "Unterlagen") })
            if (!JsonNode.DeepEquals(alt[f], neu[f])) { weitere.Add(f); details.Add($"{text} geändert"); }

        // betroffene Elemente
        var elemente = new JsonArray();
        var fAlt = Familien.Definition(alt, kat);
        var fNeu = Familien.Definition(neu, kat);
        var nutzer = m.Elemente.Where(e => e.Bezuege.GetValueOrDefault(Familien.Bezug) == fam.Id).OrderBy(e => e.Kennzeichen, StringComparer.OrdinalIgnoreCase).ToList();
        var fehlend = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var e in nutzer)
        {
            var meldungen = new List<string>();
            var typ = Familien.Typ(e);
            if (typ is not null && fNeu.Gueltig && fNeu.Typ(typ) is null)
            {
                if (!fehlend.TryGetValue(typ, out var l)) fehlend[typ] = l = [];
                l.Add(e.Kennzeichen);
                meldungen.Add($"Typ «{typ}» entfällt");
            }
            else
            {
                var la = Lagen(fAlt, e); var lb = Lagen(fNeu, e);
                if (la is not null && lb is not null)
                {
                    foreach (var x in la)
                    {
                        var y = lb.FirstOrDefault(z => string.Equals(z.Name, x.Name, StringComparison.OrdinalIgnoreCase));
                        if (y is null)
                        {
                            var vb = m.VerbindungAn(e.Id, x.Name);
                            var g = vb?.Gegenueber(e.Id, x.Name);
                            meldungen.Add($"Anschluss {x.Name} entfällt" + (g is null ? "" : $" (verbunden mit {m.Finde(g.Element)?.Kennzeichen}.{g.Anschluss} – die Verbindung wird getrennt)"));
                            continue;
                        }
                        if (x.Welt is not null && y.Welt is not null)
                        {
                            var d = Math.Sqrt(Enumerable.Range(0, 3).Sum(i => (x.Welt[i] - y.Welt[i]) * (x.Welt[i] - y.Welt[i])));
                            if (d >= Schwelle) meldungen.Add($"Anschluss {x.Name} verschiebt sich um {F(d)} mm" + (m.VerbindungAn(e.Id, x.Name) is not null ? " (verbunden – Leitung prüfen)" : ""));
                        }
                        if (x.Groesse != y.Groesse) meldungen.Add($"Anschluss {x.Name}: {x.Groesse} → {y.Groesse}");
                        if (x.Medium != y.Medium || x.Fluss != y.Fluss) meldungen.Add($"Anschluss {x.Name}: {x.Medium}/{x.Fluss} → {y.Medium}/{y.Fluss}");
                    }
                    foreach (var y in lb.Where(y => !la.Any(x => string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase))))
                        meldungen.Add($"Anschluss {y.Name} kommt dazu");
                }
            }
            if (meldungen.Count == 0) continue;
            elemente.Add(new JsonObject
            {
                ["element"] = e.Kennzeichen, ["typ"] = typ,
                ["meldungen"] = new JsonArray(meldungen.Select(x => (JsonNode)JsonValue.Create(x)).ToArray()),
            });
            foreach (var x in meldungen.Where(x => !x.StartsWith("Typ ", StringComparison.Ordinal))) details.Add($"{e.Kennzeichen}: {x}");
        }
        foreach (var (typ, l) in fehlend)
        {
            v.FehlendeTypen.Add((typ, l));
            details.Add($"Typ «{typ}» entfällt – {l.Count} Element(e) ({string.Join(", ", l.Take(8))}{(l.Count > 8 ? " …" : "")})");
        }
        int nGeaendert(JsonArray a) => a.Count;
        var name = T(neu["name"]) ?? fam.Name;
        var zusammenfassung = $"Vorschau {fam.Kennzeichen} «{name}» ({titel}): {nGeaendert(kopf)} Kopfangabe(n), {nGeaendert(par)} Parameter, {nGeaendert(typen)} Typ(en), " +
            $"{nGeaendert(koerper)} Körper, {nGeaendert(ansch)} Anschluss/Anschlüsse geändert; {elemente.Count} von {nutzer.Count} Element(en) betroffen" +
            (v.FehlendeTypen.Count > 0 ? $"; {v.FehlendeTypen.Sum(x => x.Elemente.Count)} Element(e) mit entfallendem Typ" : "") + ".";
        v.Zeilen.Add(zusammenfassung);
        v.Zeilen.AddRange(details);
        v.Daten["titel"] = titel;
        v.Daten["zusammenfassung"] = zusammenfassung;
        v.Daten["kopf"] = kopf;
        v.Daten["parameter"] = par;
        v.Daten["typen"] = typen;
        v.Daten["koerper"] = koerper;
        v.Daten["anschluesse"] = ansch;
        v.Daten["weitere"] = weitere;
        v.Daten["elemente"] = elemente;
        v.Daten["fehlendeTypen"] = new JsonArray(v.FehlendeTypen.Select(x => (JsonNode)new JsonObject
        {
            ["typ"] = x.Typ, ["elemente"] = new JsonArray(x.Elemente.Select(k => (JsonNode)JsonValue.Create(k)).ToArray()),
        }).ToArray());
        v.Daten["zeilen"] = new JsonArray(v.Zeilen.Select(x => (JsonNode)JsonValue.Create(x)).ToArray());
        return v;
    }

    static bool Gleich(JsonNode? a, JsonNode? b) => JsonNode.DeepEquals(a, b)
        || (Json.Zahl(a) is double x && Json.Zahl(b) is double y && a is JsonValue av && av.GetValueKind() == JsonValueKind.Number
            && b is JsonValue bv && bv.GetValueKind() == JsonValueKind.Number && Math.Abs(x - y) < 1e-9);

    /// <summary>Vergleicht eine Liste der Definition nach Schlüssel: neu, entfällt, geändert (mit Beschreibung).</summary>
    static JsonArray Liste(JsonObject alt, JsonObject neu, string feld, string schluessel, bool grossKlein, string wort, List<string> details,
        Func<JsonObject, JsonObject, List<string>> beschreiben)
    {
        var vgl = grossKlein ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        Dictionary<string, JsonObject> Index(JsonObject d)
        {
            var r = new Dictionary<string, JsonObject>(vgl);
            foreach (var o in (d[feld] as JsonArray ?? []).OfType<JsonObject>()) if (T(o[schluessel])?.Trim() is { Length: > 0 } k) r.TryAdd(k, o);
            return r;
        }
        var a = Index(alt); var b = Index(neu);
        var aus = new JsonArray();
        foreach (var k in a.Keys.Union(b.Keys, vgl).Order(StringComparer.OrdinalIgnoreCase))
        {
            var x = a.GetValueOrDefault(k); var y = b.GetValueOrDefault(k);
            if (x is null) { aus.Add(new JsonObject { ["name"] = k, ["art"] = "neu" }); details.Add($"{wort} «{k}» neu"); continue; }
            if (y is null) { aus.Add(new JsonObject { ["name"] = k, ["art"] = "entfaellt" }); details.Add($"{wort} «{k}» entfällt"); continue; }
            if (JsonNode.DeepEquals(x, y)) continue;
            var teile = beschreiben(x, y);
            if (teile.Count == 0) teile.Add("geändert");
            aus.Add(new JsonObject { ["name"] = k, ["art"] = "geaendert", ["aenderungen"] = new JsonArray(teile.Select(t => (JsonNode)JsonValue.Create(t)).ToArray()) });
            details.Add($"{wort} «{k}»: {string.Join("; ", teile)}");
        }
        return aus;
    }

    sealed record Lage(string Name, double[]? Welt, string Groesse, string Medium, string Fluss);

    /// <summary>Anschlüsse eines Elements nach einer Definition (Welt-Lage, wenn das Element platziert ist); null, wenn nicht auswertbar.</summary>
    static List<Lage>? Lagen(FamilieDef f, Element e)
    {
        if (!f.Gueltig) return null;
        var typ = Familien.Typ(e);
        if (typ is null || f.Typ(typ) is null) return null;
        var a = FamilienAuswertung.Aufloesen(f, typ, Familien.Exemplarwerte(e));
        if (!a.Gueltig) return null;
        var lage = Familien.Einbaulage(e);
        return a.Anschluesse.Where(x => x.Sichtbar).Select(x => new Lage(x.Name, lage?.Welt(x.Lage),
            x.Dn is double dn ? $"DN {F(dn)}" : x.D is double d ? $"Ø {F(d)} mm" : x.B is double b && x.H is double h ? $"{F(b)} × {F(h)} mm" : "–",
            x.Medium, x.Fluss)).ToList();
    }
}
