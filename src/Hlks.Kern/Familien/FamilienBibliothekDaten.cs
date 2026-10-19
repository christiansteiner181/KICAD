// Familien (Modul 2, Nachtrag B7, entwurf/modul-2-bibliothek.md): Daten der projektübergreifenden Familienbibliothek und die
// reine Logik ohne Dateizugriff – Inhaltsprüfsumme, Planung einer neuen Version (neu / gleich / Abzweig), Lage einer
// Projektfassung in der Bibliothek. Dateizugriff, Sperren und Warteschlange: FamilienBibliothekDienst.cs.
//
// Inhaltsprüfsumme (Identität einer Fassung, auf allen Rechnern gleich): SHA-256 der Definition in kanonischer Form – Schlüssel
//   sortiert, Zahlen als double («R»), ohne «version» (Zähler des Projekts bzw. der Bibliothek) und ohne unterlagen[].unterlage
//   (Element-Id, gilt nur im eigenen Projekt); unterlagen nach Prüfsumme sortiert. Der Status gehört dazu (Freigabe = neue Version).
//
// Planen (unter der Sperre der Familie, gleiche Regel für die Vorhersage im Befehl):
//   1. noch keine Version                                   → neu, Version 1
//   2. gleiche Prüfsumme wie die aktuelle Version            → gleich (keine neue Version; zwei Rechner, ein Inhalt)
//   3. Basis bestimmen: Eintrag mit der Prüfsumme der vorigen Projektfassung (aktuelle Version vor der des Markers vor der
//      höchsten, sonst der jüngste Abzweig); sonst die Version des Markers, wenn der Marker zur vorigen Fassung gehört
//   4. gleiche Prüfsumme UND gleiche Basis schon vorhanden    → gleich (derselbe Übergang doppelt, z. B. Warteschlange + Hand)
//      Basis unbekannt und Prüfsumme irgendwo vorhanden       → gleich (der Inhalt ist schon erhalten)
//   5. als_aktuell (nur Mensch)                              → neu, Version aktuell + 1; offene Abzweige gelten als entschieden
//   6. Basis = aktuelle Version                              → neu, Version aktuell + 1
//   7. sonst (ältere Version, Abzweig, unbekannt)            → Abzweig «v<basis>-<projekt>-<zeit>» (parallel geändert)
// ANNAHMEN (Prüfliste): Rückgängig im Projekt ist ein neuer Übergang (Basis = die rückgängig gemachte Version) und ergibt deshalb
//   eine weitere Version, auch wenn der Inhalt einer älteren gleicht (Spezifikation, Annahme 2); ein Abzweig ist «offen», bis ein
//   Entscheid ihn nennt (familie_aktualisieren im Projekt des Abzweigs bzw. familie_in_bibliothek mit als_aktuell).

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <summary>Ort der Bürobibliothek: Pfad, woher er stammt, und ob Lesen und Schreiben gesperrt sind (Testlauf ohne Umgebungsvariable).</summary>
public sealed record BibliotheksOrt(string? Pfad, string Quelle, string? Sperre)
{
    /// <summary>Die Bibliothek darf gelesen und beschrieben werden.</summary>
    public bool Aktiv => Pfad is not null && Sperre is null;
}

/// <summary>Projekt, aus dem eine Fassung stammt (log.json).</summary>
public sealed class BibProjekt
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Nummer { get; set; }
    public string? Ordner { get; set; }
}

/// <summary>Herstellerunterlage einer Fassung: Prüfsumme (SHA-256, 64 Zeichen), ursprünglicher Dateiname, Art.</summary>
public sealed class BibUnterlage
{
    public string Pruefsumme { get; set; } = "";
    public string? Datei { get; set; }
    public string? Art { get; set; }
    /// <summary>Nur im Auftrag der Warteschlange: wo die Datei liegt (Projekt bzw. Kopie in der Warteschlange).</summary>
    public string? Quelle { get; set; }
}

/// <summary>Ein Eintrag in log.json: art version, abzweig oder entscheid (offene Abzweige entschieden).</summary>
public sealed class BibLogEintrag
{
    public string Art { get; set; } = "version";
    public int? Version { get; set; }
    public string? Abzweig { get; set; }
    public string? Datei { get; set; }
    public string? Pruefsumme { get; set; }
    public int? Basis { get; set; }
    public string? BasisAbzweig { get; set; }
    public string? Status { get; set; }
    public string? Name { get; set; }
    public string? Kategorie { get; set; }
    public string? Hersteller { get; set; }
    public string? Produkt { get; set; }
    public int? Typen { get; set; }
    public List<BibUnterlage>? Unterlagen { get; set; }
    public BibProjekt? Projekt { get; set; }
    public string? Akteur { get; set; }
    /// <summary>mensch | ki | import …</summary>
    public string? AkteurArt { get; set; }
    public string? Geraet { get; set; }
    public string? Zeit { get; set; }
    public string? Befehl { get; set; }
    public JsonObject? Freigabe { get; set; }
    /// <summary>entscheid: die entschiedenen Abzweige.</summary>
    public List<string>? Abzweige { get; set; }
    /// <summary>entscheid: verworfen (Bibliothek übernommen) | uebernommen (Fassung als aktuell gesetzt).</summary>
    public string? Ergebnis { get; set; }
}

/// <summary>log.json einer Familie: alle Versionen, Abzweige und Entscheide (nur angehängt, nie gekürzt).</summary>
public sealed class BibLog
{
    public int Format { get; set; } = 1;
    public string Id { get; set; } = "";
    public List<BibLogEintrag> Eintraege { get; set; } = [];
}

/// <summary>Eine Version im Index.</summary>
public sealed class BibIndexVersion
{
    public int Version { get; set; }
    public string Pruefsumme { get; set; } = "";
    public int? Basis { get; set; }
    public string? BasisAbzweig { get; set; }
    public string? Status { get; set; }
    public string? Zeit { get; set; }
    public string? Projekt { get; set; }
    public string? ProjektId { get; set; }
    public string? Akteur { get; set; }
    public string? AkteurArt { get; set; }
}

/// <summary>Ein Abzweig (parallele Änderung) im Index.</summary>
public sealed class BibIndexAbzweig
{
    public string Abzweig { get; set; } = "";
    public string Pruefsumme { get; set; } = "";
    public int? Basis { get; set; }
    public string? BasisAbzweig { get; set; }
    public bool Offen { get; set; } = true;
    public string? Ergebnis { get; set; }
    public string? Status { get; set; }
    public string? Zeit { get; set; }
    public string? Projekt { get; set; }
    public string? ProjektId { get; set; }
    public string? Akteur { get; set; }
    public string? AkteurArt { get; set; }
}

/// <summary>Kopfdaten einer Familie im Index (Stand der aktuellen Version, alle Versionen und Abzweige mit Prüfsumme).</summary>
public sealed class BibIndexEintrag
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Kategorie { get; set; }
    public string? Hersteller { get; set; }
    public string? Produkt { get; set; }
    public string? Status { get; set; }
    public int Version { get; set; }
    public string Pruefsumme { get; set; } = "";
    /// <summary>Ordner relativ zur Bibliothek, mit «/» (z. B. «Heizkoerper/Testheizkoerper__fam-test»).</summary>
    public string Ordner { get; set; } = "";
    public string? Zeit { get; set; }
    public int Typen { get; set; }
    public int Unterlagen { get; set; }
    public string? Projekt { get; set; }
    public string? Akteur { get; set; }
    public string? AkteurArt { get; set; }
    public List<BibIndexVersion> Versionen { get; set; } = [];
    public List<BibIndexAbzweig> Abzweige { get; set; } = [];

    public BibIndexVersion? Aktuell => Versionen.FirstOrDefault(v => v.Version == Version);
    public IEnumerable<BibIndexAbzweig> OffeneAbzweige => Abzweige.Where(a => a.Offen);
}

/// <summary>index.json: Kopfdaten aller Familien (Zwischenspeicher; massgebend sind die Familienordner).</summary>
public sealed class BibIndex
{
    public int Format { get; set; } = 1;
    public string? Stand { get; set; }
    public List<BibIndexEintrag> Familien { get; set; } = [];

    public BibIndexEintrag? Familie(string? id) => id is null ? null : Familien.FirstOrDefault(f => f.Id == id);
}

/// <summary>Ein Schreibauftrag (auch als Eintrag der Warteschlange): eine Fassung einer Familie oder ein Entscheid.</summary>
public sealed class BibAuftrag
{
    /// <summary>schreiben | entscheid</summary>
    public string Art { get; set; } = "schreiben";
    public string Id { get; set; } = "";
    public JsonObject? Definition { get; set; }
    public string? Pruefsumme { get; set; }
    /// <summary>Inhaltsprüfsumme der vorigen Fassung im Projekt (Basis), null = neu im Projekt bzw. unbekannt.</summary>
    public string? Vorher { get; set; }
    public int? MarkerVersion { get; set; }
    public string? MarkerAbzweig { get; set; }
    /// <summary>Inhaltsprüfsumme des Projekts, zu der der Marker gehört.</summary>
    public string? MarkerInhalt { get; set; }
    public bool AlsAktuell { get; set; }
    public BibProjekt? Projekt { get; set; }
    public string? Akteur { get; set; }
    public string? AkteurArt { get; set; }
    public string? Geraet { get; set; }
    public string? Zeit { get; set; }
    public string? Befehl { get; set; }
    public JsonObject? Freigabe { get; set; }
    public List<BibUnterlage>? Unterlagen { get; set; }
    /// <summary>entscheid: Abzweige und Ergebnis (verworfen).</summary>
    public List<string>? Abzweige { get; set; }
    public string? Ergebnis { get; set; }
}

/// <summary>Plan bzw. Ergebnis eines Schreibvorgangs.</summary>
/// <param name="Art">neu | gleich | abzweig | ausstehend (Warteschlange) | gesperrt</param>
public sealed record BibPlan(string Art, int? Version, string? Abzweig, int? Basis, string? BasisAbzweig, int? Aktuell, IReadOnlyList<string> Erledigt)
{
    public static BibPlan Ausstehend => new("ausstehend", null, null, null, null, null, []);
}

/// <summary>Ergebnis eines Schreibvorgangs mit Hinweis für den Befehl.</summary>
public sealed record BibErgebnis(BibPlan Plan, string Ordner, string Pruefsumme, bool Parallel, string Hinweis);

public static class FamilienBibliothekLogik
{
    static string? T(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    static readonly ConditionalWeakTable<JsonObject, string> summen = new();

    /// <summary>Inhaltsprüfsumme einer Definition (siehe Dateikopf); zwischengespeichert je JSON-Knoten.</summary>
    public static string Inhaltspruefsumme(JsonObject def)
    {
        if (summen.TryGetValue(def, out var s)) return s;
        var k = (JsonObject)def.DeepClone();
        k.Remove("version");
        if (k["unterlagen"] is JsonArray ul)
        {
            var liste = ul.Select(u =>
            {
                var c = u?.DeepClone();
                if (c is JsonObject o) o.Remove("unterlage");
                return c;
            }).OrderBy(u => T(u?["pruefsumme"]) ?? "", StringComparer.Ordinal).ThenBy(u => Kanonisch(u), StringComparer.Ordinal).ToArray();
            k["unterlagen"] = new JsonArray(liste);
        }
        s = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Kanonisch(k)))).ToLowerInvariant();
        summen.AddOrUpdate(def, s);
        return s;
    }

    /// <summary>Inhaltsprüfsumme der Definition eines Familie-Elements (null ohne Definition).</summary>
    public static string? Inhaltspruefsumme(Element? familie) =>
        familie?.Eigenschaft("definition")?.Inhalt is JsonObject d ? Inhaltspruefsumme(d) : null;

    static string Kanonisch(JsonNode? n)
    {
        var sb = new StringBuilder();
        Kanonisch(n, sb);
        return sb.ToString();
    }

    static void Kanonisch(JsonNode? n, StringBuilder sb)
    {
        switch (n)
        {
            case null: sb.Append("null"); break;
            case JsonObject o:
                sb.Append('{');
                bool erst = true;
                foreach (var (key, v) in o.OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    if (!erst) sb.Append(',');
                    erst = false;
                    sb.Append(JsonSerializer.Serialize(key)).Append(':');
                    Kanonisch(v, sb);
                }
                sb.Append('}');
                break;
            case JsonArray a:
                sb.Append('[');
                for (int i = 0; i < a.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    Kanonisch(a[i], sb);
                }
                sb.Append(']');
                break;
            case JsonValue v:
                switch (v.GetValueKind())
                {
                    case JsonValueKind.Number: sb.Append(Json.Zahl(v)!.Value.ToString("R", CultureInfo.InvariantCulture)); break;
                    case JsonValueKind.String: sb.Append(JsonSerializer.Serialize(v.GetValue<string>())); break;
                    default: sb.Append(v.ToJsonString()); break;
                }
                break;
        }
    }

    /// <summary>Definition, wie sie in die Bibliothek geschrieben wird: version = Bibliotheksversion, ohne unterlagen[].unterlage.</summary>
    public static JsonObject FuerBibliothek(JsonObject def, int version)
    {
        var d = (JsonObject)def.DeepClone();
        d["version"] = version;
        if (d["unterlagen"] is JsonArray ul)
            foreach (var u in ul.OfType<JsonObject>()) u.Remove("unterlage");
        return d;
    }

    /// <summary>Marker Familie.bibliothek eines Elements.</summary>
    public static JsonObject? Marker(Element? familie) => familie?.Eigenschaft("bibliothek")?.Inhalt as JsonObject;

    public static int? MarkerVersion(JsonObject? m) => Json.Zahl(m?["version"]) is double d ? (int)d : null;

    /// <summary>Basis einer Fassung: Version bzw. Abzweig, von der sie ausgeht (siehe Dateikopf, Regel 3).</summary>
    public static (int? Version, string? Abzweig) Basis(BibIndexEintrag e, string? vorher, int? markerVersion, string? markerAbzweig, string? markerInhalt)
    {
        if (vorher is not null)
        {
            var v = e.Versionen.Where(x => x.Pruefsumme == vorher).Select(x => x.Version).ToList();
            if (v.Count > 0)
            {
                if (v.Contains(e.Version)) return (e.Version, null);
                if (markerVersion is int mv && v.Contains(mv)) return (mv, null);
                return (v.Max(), null);
            }
            var a = e.Abzweige.Where(x => x.Pruefsumme == vorher).OrderByDescending(x => x.Offen).ThenByDescending(x => x.Zeit, StringComparer.Ordinal).FirstOrDefault();
            if (a is not null) return (null, a.Abzweig);
            // Marker gehört zur vorigen Fassung (z. B. geladen, die Prüfung hat die Definition dabei ergänzt)
            if (markerInhalt == vorher)
            {
                if (markerVersion is int m2 && e.Versionen.Any(x => x.Version == m2)) return (m2, null);
                if (markerAbzweig is not null && e.Abzweige.Any(x => x.Abzweig == markerAbzweig)) return (null, markerAbzweig);
            }
        }
        return (null, null);
    }

    /// <summary>Plant das Schreiben einer Fassung (Regeln im Dateikopf). e = null: die Familie gibt es noch nicht in der Bibliothek.</summary>
    public static BibPlan Planen(BibIndexEintrag? e, string pruefsumme, string? vorher, int? markerVersion, string? markerAbzweig, string? markerInhalt,
        bool alsAktuell, string abzweigName)
    {
        if (e is null || e.Versionen.Count == 0) return new BibPlan("neu", 1, null, null, null, null, []);
        var offen = e.OffeneAbzweige.Select(a => a.Abzweig).ToList();
        if (e.Aktuell?.Pruefsumme == pruefsumme)
            return new BibPlan("gleich", e.Version, null, null, null, e.Version, alsAktuell ? offen : []);
        var (bv, ba) = Basis(e, vorher, markerVersion, markerAbzweig, markerInhalt);
        bool basisBekannt = bv is not null || ba is not null;
        // derselbe Übergang schon geschrieben (bzw. Inhalt ohne bekannte Basis schon erhalten)
        var gleichV = e.Versionen.Where(x => x.Pruefsumme == pruefsumme && (!basisBekannt || (x.Basis == bv && x.BasisAbzweig == ba))).OrderByDescending(x => x.Version).FirstOrDefault();
        var gleichA = e.Abzweige.Where(x => x.Pruefsumme == pruefsumme && (!basisBekannt || (x.Basis == bv && x.BasisAbzweig == ba))).OrderByDescending(x => x.Offen).FirstOrDefault();
        if (!alsAktuell)
        {
            if (gleichV is not null) return new BibPlan("gleich", gleichV.Version, null, gleichV.Basis, gleichV.BasisAbzweig, e.Version, []);
            if (gleichA is not null) return new BibPlan("gleich", null, gleichA.Abzweig, gleichA.Basis, gleichA.BasisAbzweig, e.Version, []);
        }
        if (alsAktuell) return new BibPlan("neu", e.Version + 1, null, e.Version, null, e.Version, offen);
        if (bv == e.Version) return new BibPlan("neu", e.Version + 1, null, bv, null, e.Version, []);
        int stamm = bv ?? StammVersion(e, ba) ?? e.Version;
        return new BibPlan("abzweig", null, $"v{stamm:D4}-{abzweigName}", bv, ba, e.Version, []);
    }

    /// <summary>Version, auf die ein Abzweig (über weitere Abzweige) zurückgeht.</summary>
    static int? StammVersion(BibIndexEintrag e, string? abzweig)
    {
        for (int i = 0; i < 20 && abzweig is not null; i++)
        {
            var a = e.Abzweige.FirstOrDefault(x => x.Abzweig == abzweig);
            if (a is null) return null;
            if (a.Basis is int v) return v;
            abzweig = a.BasisAbzweig;
        }
        return null;
    }

    /// <summary>Wo steht eine Projektfassung (Inhaltsprüfsumme) in der Bibliothek: Version bzw. Abzweig, sonst (null, null).</summary>
    public static (int? Version, string? Abzweig) Lage(BibIndexEintrag e, string pruefsumme, JsonObject? marker)
    {
        var v = e.Versionen.Where(x => x.Pruefsumme == pruefsumme).Select(x => x.Version).ToList();
        if (v.Count > 0)
        {
            if (v.Contains(e.Version)) return (e.Version, null);
            if (MarkerVersion(marker) is int mv && v.Contains(mv)) return (mv, null);
            return (v.Max(), null);
        }
        var a = e.Abzweige.Where(x => x.Pruefsumme == pruefsumme).OrderByDescending(x => x.Offen).ThenByDescending(x => x.Zeit, StringComparer.Ordinal).FirstOrDefault();
        if (a is not null) return (null, a.Abzweig);
        // geladene Fassung, die die Prüfung beim Laden ergänzt hat: der Marker kennt den Inhalt des Projekts
        if (marker is not null && T(marker["id"]) == e.Id && (T(marker["inhalt"]) ?? T(marker["pruefsumme"])) == pruefsumme)
        {
            if (MarkerVersion(marker) is int m && e.Versionen.Any(x => x.Version == m)) return (m, null);
            if (T(marker["abzweig"]) is { } ab && e.Abzweige.Any(x => x.Abzweig == ab)) return (null, ab);
        }
        return (null, null);
    }

    /// <summary>Index-Eintrag aus log.json (massgebend) und dem relativen Ordner.</summary>
    public static BibIndexEintrag Eintrag(BibLog log, string ordner)
    {
        var e = new BibIndexEintrag { Id = log.Id, Ordner = ordner };
        var erledigt = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var x in log.Eintraege.Where(x => x.Art == "entscheid"))
            foreach (var a in x.Abzweige ?? []) erledigt[a] = x.Ergebnis ?? "entschieden";
        BibLogEintrag? letzte = null;
        foreach (var x in log.Eintraege)
        {
            if (x.Art == "version" && x.Version is int n && x.Pruefsumme is { } p)
            {
                e.Versionen.RemoveAll(v => v.Version == n);
                e.Versionen.Add(new BibIndexVersion
                {
                    Version = n, Pruefsumme = p, Basis = x.Basis, BasisAbzweig = x.BasisAbzweig, Status = x.Status, Zeit = x.Zeit,
                    Projekt = x.Projekt?.Name, ProjektId = x.Projekt?.Id, Akteur = x.Akteur, AkteurArt = x.AkteurArt,
                });
                if (letzte?.Version is not int lv || n >= lv) letzte = x;
            }
            else if (x.Art == "abzweig" && x.Abzweig is { } ab && x.Pruefsumme is { } pa)
            {
                e.Abzweige.RemoveAll(v => v.Abzweig == ab);
                e.Abzweige.Add(new BibIndexAbzweig
                {
                    Abzweig = ab, Pruefsumme = pa, Basis = x.Basis, BasisAbzweig = x.BasisAbzweig, Status = x.Status, Zeit = x.Zeit,
                    Projekt = x.Projekt?.Name, ProjektId = x.Projekt?.Id, Akteur = x.Akteur, AkteurArt = x.AkteurArt,
                    Offen = !erledigt.ContainsKey(ab), Ergebnis = erledigt.GetValueOrDefault(ab),
                });
            }
        }
        e.Versionen.Sort((a, b) => a.Version.CompareTo(b.Version));
        if (letzte is not null)
        {
            e.Version = letzte.Version!.Value;
            e.Pruefsumme = letzte.Pruefsumme!;
            e.Name = letzte.Name ?? "";
            e.Kategorie = letzte.Kategorie;
            e.Hersteller = letzte.Hersteller;
            e.Produkt = letzte.Produkt;
            e.Status = letzte.Status;
            e.Zeit = letzte.Zeit;
            e.Typen = letzte.Typen ?? 0;
            e.Unterlagen = letzte.Unterlagen?.Count ?? 0;
            e.Projekt = letzte.Projekt?.Name;
            e.Akteur = letzte.Akteur;
            e.AkteurArt = letzte.AkteurArt;
        }
        return e;
    }

    /// <summary>Text für Ordner- und Dateinamen: ohne Umlaute und Sonderzeichen (Mehrgeräte-Regel), höchstens max Zeichen.</summary>
    public static string Sauber(string? text, int max = 40)
    {
        var t = (text ?? "").Trim().Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("Ä", "Ae").Replace("Ö", "Oe").Replace("Ü", "Ue")
            .Replace("ß", "ss").Replace('×', 'x').Replace("é", "e").Replace("è", "e").Replace("à", "a");
        var sb = new StringBuilder();
        foreach (var c in t) sb.Append(char.IsAsciiLetterOrDigit(c) || c == '-' ? c : '_');
        var s = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), "_+", "_").Trim('_', '-', '.');
        if (s.Length > max) s = s[..max].Trim('_', '-', '.');
        return s.Length == 0 ? "x" : s;
    }

    /// <summary>Kurzname eines Projekts für Abzweige: Name ohne Sonderzeichen, höchstens 24 Zeichen.</summary>
    public static string ProjektKurz(BibProjekt? p) => Sauber(p?.Name ?? p?.Id ?? "projekt", 24);
}
