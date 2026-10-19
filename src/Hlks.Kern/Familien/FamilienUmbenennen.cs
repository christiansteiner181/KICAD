// Familien (Modul 2, B1, Nachtrag 2 – Befund C1): op parameter_umbenennen { alt, neu } in familie_aendern. Umbenannt wird
// überall, wo der Name vorkommt: Name des Parameters (an seiner Stelle), Formeln aller Parameter, Körper, Anschlüsse, Masse,
// Sinnbild und Schemasymbol, typen[].werte und typen[].quellen, masse[].parameter, ein Anschlussmedium, das ein Parameter
// ist, der Block «leitung» einer Leitungsserie (Modul 3: groesse, aussen, innen, wand, regeln.biegeradius, Typregeln) – und die
// Exemplarwerte der platzierten Elemente (im selben Paket, Exemplarwerte()).
// Formeln werden zeichenweise wie vom Zerleger gelesen: nur Namen werden ersetzt, nicht Funktionen (Name vor «(»), nicht
// Text in "…" und nicht Ziffernfolgen.
// ANNAHME (Prüfliste): auch konflikte[].parameter und die Schlüssel von abdeckung («parameter» bzw. «typ/parameter») werden
// umbenannt (Daten von B4, gleicher Name gemeint).

using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Hlks.Kern;

public static class FamilienUmbenennen
{
    static readonly Regex NamensMuster = new("^[A-Za-zÄÖÜäöü_][A-Za-zÄÖÜäöü0-9_]*$");
    static readonly string[] KoerperFelder = ["von", "bis", "nach", "mitte", "r", "r1", "r2", "winkel", "biegeradius", "sichtbar", "kontur", "loecher", "pfad"];
    static readonly string[] AnschlussFelder = ["dn", "d", "b", "h", "sichtbar", "lage", "richtung"];
    static readonly string[] ZeichnungFelder = ["punkte", "radius", "von", "bis", "sichtbar"];

    static string? T(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    /// <summary>Formeltext mit umbenanntem Parameter.</summary>
    public static string Formel(string text, string alt, string neu)
    {
        var sb = new StringBuilder(text.Length + 8);
        int i = 0, n = text.Length;
        while (i < n)
        {
            char c = text[i];
            if (c == '"')
            {
                int ende = text.IndexOf('"', i + 1);
                if (ende < 0) { sb.Append(text, i, n - i); break; }
                sb.Append(text, i, ende - i + 1);
                i = ende + 1;
            }
            else if (char.IsAsciiDigit(c) || (c == '.' && i + 1 < n && char.IsAsciiDigit(text[i + 1])))
            {
                int a = i;
                while (i < n && char.IsAsciiDigit(text[i])) i++;
                if (i < n && text[i] == '.') { i++; while (i < n && char.IsAsciiDigit(text[i])) i++; }
                if (i < n && text[i] is 'e' or 'E')
                {
                    int e = i + 1;
                    if (e < n && text[e] is '+' or '-') e++;
                    if (e < n && char.IsAsciiDigit(text[e])) { i = e; while (i < n && char.IsAsciiDigit(text[i])) i++; }
                }
                sb.Append(text, a, i - a);
            }
            else if (char.IsLetter(c) || c == '_')
            {
                int a = i;
                while (i < n && (char.IsLetterOrDigit(text[i]) || text[i] == '_')) i++;
                var name = text[a..i];
                int j = i;
                while (j < n && char.IsWhiteSpace(text[j])) j++;
                bool funktion = j < n && text[j] == '(';
                sb.Append(!funktion && name == alt ? neu : name);
            }
            else { sb.Append(c); i++; }
        }
        return sb.ToString();
    }

    /// <summary>Zahl oder Formel (auch Listen davon, z. B. Punkte und Konturen) umbenennen.</summary>
    static JsonNode? Zahl(JsonNode? n, string alt, string neu) => n switch
    {
        JsonValue v when v.TryGetValue<string>(out var s) => JsonValue.Create(Formel(s, alt, neu)),
        JsonArray a => new JsonArray(a.Select(x => Zahl(x?.DeepClone(), alt, neu)).ToArray()),
        _ => n,
    };

    static void Felder(JsonObject o, IEnumerable<string> felder, string alt, string neu)
    {
        foreach (var f in felder)
            if (o[f] is { } wert) o[f] = Zahl(wert.DeepClone(), alt, neu);
    }

    /// <summary>Schlüssel eines Objekts umbenennen, Reihenfolge bleibt.</summary>
    static void Schluessel(JsonObject? o, Func<string, string> neuerName)
    {
        if (o is null) return;
        var paare = o.ToList();
        o.Clear();
        foreach (var (k, v) in paare) o[neuerName(k)] = v;
    }

    /// <summary>Die op auf die Definition anwenden (prüft alt vorhanden, neu gültig und frei). Gibt (alt, neu) zurück.</summary>
    public static (string Alt, string Neu) Anwenden(JsonObject def, JsonObject op, int nr)
    {
        var alt = T(op["alt"])?.Trim();
        var neu = T(op["neu"])?.Trim();
        if (string.IsNullOrEmpty(alt) || string.IsNullOrEmpty(neu))
            throw new HlksFehler($"ops[{nr}] (parameter_umbenennen): «alt» und «neu» angeben.", "Form: { \"op\": \"parameter_umbenennen\", \"alt\": \"hoehe\", \"neu\": \"bauhoehe\" }");
        var parameter = (def["parameter"] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];
        var namen = parameter.Select(p => T(p["name"])?.Trim()).OfType<string>().ToList();
        if (!namen.Contains(alt))
            throw new HlksFehler($"ops[{nr}] (parameter_umbenennen): Parameter «{alt}» gibt es nicht.", "Parameter: " + string.Join(", ", namen));
        if (!NamensMuster.IsMatch(neu))
            throw new HlksFehler($"ops[{nr}] (parameter_umbenennen): Name «{neu}» ist ungültig.", "Erlaubt: Buchstaben (auch Umlaute), Ziffern und _, am Anfang keine Ziffer; keine Leerzeichen.");
        if (neu == alt) throw new HlksFehler($"ops[{nr}] (parameter_umbenennen): alter und neuer Name sind gleich («{alt}»).", "Einen anderen Namen angeben.");
        if (namen.Contains(neu))
            throw new HlksFehler($"ops[{nr}] (parameter_umbenennen): einen Parameter «{neu}» gibt es schon.", "Einen freien Namen wählen.");
        if (namen.FirstOrDefault(x => x != alt && x.Equals(neu, StringComparison.OrdinalIgnoreCase)) is { } anderer)
            throw new HlksFehler($"ops[{nr}] (parameter_umbenennen): «{neu}» unterscheidet sich von «{anderer}» nur in Gross/Kleinschreibung.", "Einen deutlich anderen Namen wählen.");

        foreach (var p in parameter)
        {
            if (T(p["name"])?.Trim() == alt) p["name"] = neu;
            if (T(p["formel"]) is { } formel) p["formel"] = Formel(formel, alt, neu);
        }
        foreach (var t in (def["typen"] as JsonArray)?.OfType<JsonObject>() ?? [])
        {
            Schluessel(t["werte"] as JsonObject, k => k == alt ? neu : k);
            Schluessel(t["quellen"] as JsonObject, k => k == alt ? neu : k);
        }
        foreach (var k in (def["koerper"] as JsonArray)?.OfType<JsonObject>() ?? [])
        {
            Felder(k, KoerperFelder, alt, neu);
            if (k["achse"] is JsonObject achse) Felder(achse, ["punkt"], alt, neu);
            if (k["profil"] is JsonObject profil) Felder(profil, ["kreis", "rechteck"], alt, neu);
        }
        foreach (var a in (def["anschluesse"] as JsonArray)?.OfType<JsonObject>() ?? [])
        {
            Felder(a, AnschlussFelder, alt, neu);
            if (T(a["medium"])?.Trim() == alt) a["medium"] = neu;   // Medium aus einem Parameter (Datentyp medium)
            if (T(a["gewinde"])?.Trim() == alt) a["gewinde"] = neu;   // Gewinde aus einem Textparameter
        }
        foreach (var m in (def["masse"] as JsonArray)?.OfType<JsonObject>() ?? [])
        {
            Felder(m, ["von", "bis"], alt, neu);
            if (T(m["parameter"])?.Trim() == alt) m["parameter"] = neu;
        }
        foreach (var feld in new[] { "sinnbild", "schemasymbol" })
            foreach (var z in (def[feld] as JsonArray)?.OfType<JsonObject>() ?? []) Felder(z, ZeichnungFelder, alt, neu);
        if (def["leitung"] is JsonObject leitung)   // Modul 3: Leitungsserie – Grössenparameter, Masse, Regeln, Typregeln (Name und Name_2)
        {
            if (leitung["groesse"] is JsonObject g)
                foreach (var (schl, wert) in g.ToList())
                    if (T(wert)?.Trim() == alt) g[schl] = neu;
            Felder(leitung, ["aussen", "innen", "wand"], alt, neu);
            if (leitung["regeln"] is JsonObject regeln) Felder(regeln, ["biegeradius"], alt, neu);
            foreach (var (_, wahl) in (leitung["formteile"] as JsonObject)?.ToList() ?? [])
                if (wahl is JsonObject w && T(w["typ"]) is { } typ) w["typ"] = Formel(Formel(typ, alt, neu), alt + "_2", neu + "_2");
        }
        foreach (var konflikt in (def["konflikte"] as JsonArray)?.OfType<JsonObject>() ?? [])
            if (T(konflikt["parameter"])?.Trim() == alt) konflikt["parameter"] = neu;
        Schluessel(def["abdeckung"] as JsonObject, k => k == alt ? neu : k.EndsWith("/" + alt, StringComparison.Ordinal) ? k[..^alt.Length] + neu : k);
        return (alt, neu);
    }

    /// <summary>Exemplarwerte der platzierten Elemente dieser Familie umbenennen (im selben Paket, vor der Nachführung).</summary>
    public static void Exemplarwerte(Kontext k, Element familie, IReadOnlyList<(string Alt, string Neu)> umbenannt)
    {
        if (umbenannt.Count == 0) return;
        var geaendert = new List<string>();
        foreach (var e in k.Arbeit.Elemente.Where(x => x.Bezuege.GetValueOrDefault(Familien.Bezug) == familie.Id).ToList())
        {
            if (e.Eigenschaft("parameter")?.Inhalt is not JsonObject ex || !umbenannt.Any(u => ex.ContainsKey(u.Alt))) continue;
            var kopie = e.Kopie();
            var werte = (JsonObject)ex.DeepClone();
            foreach (var (alt, neu) in umbenannt) Schluessel(werte, x => x == alt ? neu : x);
            kopie.Eigenschaften["parameter"] = new Wert { Inhalt = werte, Herkunft = k.Herkunft };
            k.Setzen(kopie);
            geaendert.Add(e.Kennzeichen);
        }
        k.Hinweise.Add($"Parameter umbenannt: {string.Join(", ", umbenannt.Select(u => $"{u.Alt} → {u.Neu}"))}" +
            (geaendert.Count > 0 ? $"; Exemplarwerte nachgeführt: {string.Join(", ", geaendert.Take(10))}{(geaendert.Count > 10 ? " …" : "")}." : "."));
    }
}
