// BCF 3.0 (BIM Collaboration Format, buildingSMART) – Modul 3b, Teil B6 (entwurf/modul-3b.md, Annahme 1; Entscheid Christian
// 19.10.2026 «bcf gehen wir auf das aktuellste»). Gemeinsame Bausteine für bcf_export, bcf_import und bcf_lesen: feste Guids
// (UUID v5), Statusabbildung, Bezeichnungen der Erweiterungen, Kommentarformat, Beteiligte → Komponenten, XML-Hilfen.
//
// Grundlage sind allein die offiziellen XML-Schemas in schema/bcf-3.0 (BCF-XML, Zweig release_3_0, unverändert, CC BY-ND –
// siehe schema/bcf-3.0/QUELLE.md). Was die Schemas offenlassen, ist als ANNAHME markiert (Prüfliste Christian):
//
// ANNAHME Guid: Topic-Guid = UUID v5 (RFC 4122 Abschn. 4.3, SHA-1) im KICAD-Namensraum aus «<Projekt-Id>/kollision/<kennung>»
//   (kennung = stabile Kennung der Kollision aus Beteiligten und gerundeter Lage, Grundsatzentscheid 4). Derselbe Befund ergibt
//   über Prüfläufe, Pakete und Rechner dasselbe Topic. Fehlt die Kennung, gilt «…/kollision-id/<Element-Id>»; ebenso für die
//   zweite von zwei Kollisionen mit gleicher Kennung (darf nicht vorkommen, wird aber nicht zur Doppel-Guid). KICAD-Namensraum =
//   UUID v5 aus dem URL-Namensraum 6ba7b811-9dad-11d1-80b4-00c04fd430c8 (RFC 4122 Anhang C) und «https://hkplanung.ch/kicad/bcf».
//   Viewpoint-Guid = UUID v5 aus «<Topic-Guid>/viewpoint», Guid des eigenen Kommentars = UUID v5 aus «<Topic-Guid>/kommentar».
// ANNAHME Status: Abbildung Kollision.status ↔ TopicStatus siehe StatusAusgabe/StatusEingabe (deutsche Bezeichnungen; beim Lesen
//   auch übliche englische). Unbekannter TopicStatus ändert nichts (Hinweis).
// ANNAHME Bezeichnungen: TopicType «Kollision hart» / «Kollision weich»; Priority hart «Hoch», weich «Normal» (Liste Hoch, Normal,
//   Tief); Labels = Gewerke der Beteiligten (Heizung, Lüftung, Sanitär, Kälte, MSRL, Architektur, Allgemein) bzw. «Fremdmodell» für
//   IFC-Produkte, die nicht als Element übernommen sind.
// ANNAHME Kommentare: Kollision.kommentar ist ein Text. Eigener Text = ein Comment (feste Guid). Übernommene BCF-Kommentare stehen
//   je als Block «[BCF <JJJJ-MM-TT hh:mm> · <Autor> · <Guid>] <Text>» (Folgezeilen mit zwei Leerzeichen eingerückt) im Kommentar und
//   gehen beim Export mit ihrer ursprünglichen Guid, Datum und Autor wieder hinaus – so entstehen beim Partner keine Doppel.
// ANNAHME Beteiligte: Format der Liste legt B2 (kollision_pruefen) fest; gelesen werden Text «Element-Id/Kennzeichen»,
//   «Unterlage-Id:GlobalId» (wie Pruefkoerper.Kennung) oder eine GlobalId sowie Objekte { element|id|kennzeichen, globalId|ifc,
//   unterlage, entitaet|klasse, name }.

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml;

namespace Hlks.Kern;

/// <summary>Ein Beteiligter einer Kollision, wie er im BCF erscheint: Anzeige, Komponente (IfcGuid bzw. AuthoringToolId mit
/// OriginatingSystem), Label (Gewerk) und – falls bekannt – Unterlage und Element.</summary>
public sealed record BcfBeteiligter(string Kurz, string Anzeige, string? IfcGuid, string? AuthoringToolId, string? Ursprung, string Label,
    string? UnterlageId, string? ElementId);

/// <summary>Ein Kommentar eines Topics (Comment in markup.bcf).</summary>
public sealed record BcfKommentar(Guid Guid, DateTimeOffset? Datum, string? Autor, string? Text, Guid? Viewpoint = null,
    DateTimeOffset? Geaendert = null, string? GeaendertVon = null);

public static class BcfFormat
{
    public const string Version = "3.0";
    /// <summary>OriginatingSystem für Elemente, die in KICAD entstanden sind (AuthoringToolId = Element-Id).</summary>
    public const string Ursprungssystem = "KICAD";

    /// <summary>RFC 4122 Anhang C: Namensraum für URLs.</summary>
    public static readonly Guid NamensraumUrl = new("6ba7b811-9dad-11d1-80b4-00c04fd430c8");
    /// <summary>KICAD-Namensraum der BCF-Guids (UUID v5 aus dem URL-Namensraum und «https://hkplanung.ch/kicad/bcf»).</summary>
    public static readonly Guid Namensraum = UuidV5(NamensraumUrl, "https://hkplanung.ch/kicad/bcf");

    /// <summary>UUID Version 5 (RFC 4122 Abschn. 4.3): SHA-1 über Namensraum (Netzwerk-Bytefolge) und Name (UTF-8), Version 5,
    /// Variante RFC 4122.</summary>
    public static Guid UuidV5(Guid namensraum, string name)
    {
        var nameBytes = Encoding.UTF8.GetBytes(name);
        var daten = new byte[16 + nameBytes.Length];
        namensraum.TryWriteBytes(daten.AsSpan(0, 16), bigEndian: true, out _);
        nameBytes.CopyTo(daten, 16);
#pragma warning disable CA5350 // SHA-1 ist für UUID v5 vorgeschrieben (keine Sicherheitsfunktion)
        var h = SHA1.HashData(daten);
#pragma warning restore CA5350
        h[6] = (byte)((h[6] & 0x0F) | 0x50);
        h[8] = (byte)((h[8] & 0x3F) | 0x80);
        return new Guid(h.AsSpan(0, 16), bigEndian: true);
    }

    /// <summary>Guid in der Schreibweise des Schemas (shared-types.xsd: Kleinbuchstaben, 8-4-4-4-12).</summary>
    public static string Text(Guid g) => g.ToString("D");

    public static Guid TopicGuid(string projektId, Element kollision) =>
        kollision.Text("kennung") is { Length: > 0 } k ? UuidV5(Namensraum, $"{projektId}/kollision/{k}") : TopicGuidAusId(projektId, kollision);

    static Guid TopicGuidAusId(string projektId, Element kollision) => UuidV5(Namensraum, $"{projektId}/kollision-id/{kollision.Id}");

    public static Guid ViewpointGuid(Guid topic) => UuidV5(Namensraum, $"{Text(topic)}/viewpoint");
    public static Guid KommentarGuid(Guid topic) => UuidV5(Namensraum, $"{Text(topic)}/kommentar");

    /// <summary>Alle Kollisionen des Modells in fester Folge (Kennzeichen natürlich sortiert, dann Id).</summary>
    public static List<Element> Kollisionen(Modell m, Katalog k) =>
        m.Elemente.Where(e => k.IstArt(e, "Kollision")).OrderBy(e => e.Kennzeichen, Natuerlich).ThenBy(e => e.Id, StringComparer.Ordinal).ToList();

    /// <summary>Topic-Guid je Kollision (Element-Id → Guid) über alle Kollisionen des Modells – unabhängig von einem Filter, damit
    /// Export und Import dieselbe Zuordnung haben. Gleiche Kennung zweimal: die zweite bekommt die Guid aus ihrer Element-Id.</summary>
    public static Dictionary<string, Guid> TopicGuids(string projektId, Modell m, Katalog k)
    {
        var d = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var vergeben = new HashSet<Guid>();
        foreach (var e in Kollisionen(m, k))
        {
            var g = TopicGuid(projektId, e);
            if (!vergeben.Add(g)) { g = TopicGuidAusId(projektId, e); vergeben.Add(g); }
            d[e.Id] = g;
        }
        return d;
    }

    // ------------------------------------------------------------------ Status, Typ, Priorität, Labels (ANNAHME)

    /// <summary>Kollision.status → TopicStatus (ANNAHME der Bezeichnungen).</summary>
    public static readonly IReadOnlyList<(string Kicad, string Bcf)> StatusAusgabe =
    [
        ("neu", "Neu"), ("bestaetigt", "Bestätigt"), ("akzeptiert", "Akzeptiert"), ("erledigt", "Erledigt"), ("behoben", "Behoben"),
    ];

    /// <summary>TopicStatus → Kollision.status (ANNAHME). Schlüssel ohne Gross/Klein und ohne Leer- und Satzzeichen.</summary>
    public static readonly IReadOnlyList<(string Kicad, string[] Bcf)> StatusEingabe =
    [
        ("neu", ["Neu", "New", "Open", "Offen", "Active", "Aktiv"]),
        ("bestaetigt", ["Bestätigt", "Bestaetigt", "Confirmed", "In Progress", "In Bearbeitung", "Assigned", "ReOpened", "Wiedereröffnet"]),
        ("akzeptiert", ["Akzeptiert", "Accepted", "Approved", "Genehmigt"]),
        ("erledigt", ["Erledigt", "Resolved", "Done", "Closed", "Geschlossen", "Gelöst", "Geloest", "Fixed"]),
        ("behoben", ["Behoben"]),
    ];

    static readonly Dictionary<string, string> statusNach = StatusEingabe
        .SelectMany(x => x.Bcf.Select(b => (Schluessel: Schluessel(b), x.Kicad)))
        .ToDictionary(x => x.Schluessel, x => x.Kicad);

    static string Schluessel(string s) => new(s.ToLowerInvariant().Where(char.IsLetter).ToArray());

    public static string StatusBcf(string? kicad) =>
        StatusAusgabe.FirstOrDefault(x => x.Kicad == (kicad ?? "neu")).Bcf ?? "Neu";

    /// <summary>Kollision.status zu einem TopicStatus; null = unbekannt (nichts übernehmen).</summary>
    public static string? StatusKicad(string? bcf) =>
        bcf is null ? null : statusNach.GetValueOrDefault(Schluessel(bcf));

    /// <summary>Abbildung als Tabelle für Antworten (Prüfliste).</summary>
    public static JsonObject StatusTabelle() => new()
    {
        ["ausgabe"] = new JsonObject(StatusAusgabe.Select(x => KeyValuePair.Create(x.Kicad, (JsonNode?)JsonValue.Create(x.Bcf)))),
        ["eingabe"] = new JsonObject(StatusEingabe.Select(x => KeyValuePair.Create(x.Kicad, (JsonNode?)new JsonArray(x.Bcf.Select(b => (JsonNode)JsonValue.Create(b)!).ToArray())))),
        ["unbekannt"] = "Status bleibt, Hinweis in der Antwort",
    };

    public static readonly string[] TopicTypen = ["Kollision hart", "Kollision weich"];
    public static string TopicTyp(string? art) => art == "weich" ? TopicTypen[1] : TopicTypen[0];

    public static readonly string[] Prioritaeten = ["Hoch", "Normal", "Tief"];
    public static string Prioritaet(string? art) => art == "weich" ? "Normal" : "Hoch";

    public const string LabelFremd = "Fremdmodell";
    public static readonly string[] Labels = ["Architektur", "Heizung", "Lüftung", "Sanitär", "Kälte", "MSRL", "Allgemein", LabelFremd];

    public static string GewerkLabel(string? gewerk) => gewerk switch
    {
        "heizung" => "Heizung", "lueftung" => "Lüftung", "sanitaer" => "Sanitär", "kaelte" => "Kälte", "msrl" => "MSRL",
        "architektur" => "Architektur", null or "" or "allgemein" => "Allgemein",
        _ => char.ToUpperInvariant(gewerk[0]) + gewerk[1..],
    };

    // ------------------------------------------------------------------ Beteiligte

    /// <summary>IFC-GlobalId nach Schema (markup.xsd/visinfo.xsd IfcGuid: genau 22 Zeichen aus 0-9, A-Z, a-z, _ und $).</summary>
    public static bool IstIfcGuid(string? s) => s is { Length: 22 } && s.All(c => char.IsAsciiLetterOrDigit(c) || c == '_' || c == '$');

    /// <summary>GlobalId ohne den Zusatz geteilter Stücke («GlobalId:2» → «GlobalId», wie Pruefkoerperbau).</summary>
    public static string? GlobalIdBasis(string? kennung) => kennung is null ? null : kennung.IndexOf(':') is var i and > 0 ? kennung[..i] : kennung;

    /// <summary>Dateiname einer Unterlage für Anzeige und Header/File (ursprünglicher Name, sonst Ablagename).</summary>
    public static string? UnterlageName(Element? u)
    {
        if (u is null) return null;
        var n = u.Text("original") is { Length: > 0 } o ? o : u.Text("datei");
        if (string.IsNullOrWhiteSpace(n)) return null;
        var i = n.LastIndexOfAny(['/', '\\']);
        return i >= 0 && i < n.Length - 1 ? n[(i + 1)..] : n;
    }

    // ------------------------------------------------------------------ XML

    /// <summary>Entfernt Zeichen, die XML 1.0 nicht erlaubt (Steuerzeichen ausser Tab, LF, CR; einzelne Ersatzzeichen).</summary>
    public static string Sauber(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (char.IsHighSurrogate(c))
            {
                if (i + 1 < s.Length && char.IsLowSurrogate(s[i + 1])) { sb.Append(c).Append(s[i + 1]); i++; }
                continue;
            }
            if (XmlConvert.IsXmlChar(c)) sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>Text für ein Feld vom Typ NonEmptyOrBlankString: gesäubert; null, wenn nach dem Zusammenfassen nichts bleibt.</summary>
    public static string? NichtLeer(string? s)
    {
        var t = Sauber(s);
        return t.Trim().Length == 0 ? null : t;
    }

    /// <summary>xs:dateTime in UTC, auf Sekunden.</summary>
    public static string Zeit(DateTimeOffset t) => t.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    /// <summary>xs:double, auf 6 Nachkommastellen (µm bei Metern).</summary>
    public static string Zahl(double x) => XmlConvert.ToString(Math.Round(x, 6));

    /// <summary>Natürliche Ordnung (KOL-2 vor KOL-10), Gross/Klein egal.</summary>
    public static readonly IComparer<string> Natuerlich = Comparer<string>.Create((a, b) =>
    {
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsAsciiDigit(a[i]) && char.IsAsciiDigit(b[j]))
            {
                int i0 = i, j0 = j;
                while (i < a.Length && char.IsAsciiDigit(a[i])) i++;
                while (j < b.Length && char.IsAsciiDigit(b[j])) j++;
                var x = a[i0..i].TrimStart('0'); var y = b[j0..j].TrimStart('0');
                int c = x.Length != y.Length ? x.Length.CompareTo(y.Length) : string.CompareOrdinal(x, y);
                if (c != 0) return c;
            }
            else
            {
                int c = char.ToUpperInvariant(a[i]).CompareTo(char.ToUpperInvariant(b[j]));
                if (c != 0) return c;
                i++; j++;
            }
        }
        return (a.Length - i).CompareTo(b.Length - j);
    });
}

/// <summary>Löst die Beteiligten einer Kollision zu BCF-Komponenten auf (GlobalId-Verzeichnis einmal je Lauf).</summary>
public sealed class BcfAufloesung(Projekt p, Modell m)
{
    Dictionary<string, Element>? nachGlobalId;

    Dictionary<string, Element> NachGlobalId()
    {
        if (nachGlobalId is not null) return nachGlobalId;
        nachGlobalId = new(StringComparer.Ordinal);
        foreach (var e in m.Elemente.OrderBy(x => x.Kennzeichen, BcfFormat.Natuerlich))
            if (BcfFormat.GlobalIdBasis(e.Kennungen?.GetValueOrDefault("ifc")) is { Length: > 0 } g && !p.Katalog.IstArt(e, "Kollision"))
                nachGlobalId.TryAdd(g, e);
        return nachGlobalId;
    }

    /// <summary>Element zu einer IFC-GlobalId (übernommenes Element, geteilte Stücke über die Basis), sonst null.</summary>
    public Element? ElementZuGlobalId(string globalId) => NachGlobalId().GetValueOrDefault(globalId);

    /// <summary>Beteiligte in der Folge der Liste, ohne Doppel (gleiche Komponente).</summary>
    public List<BcfBeteiligter> Beteiligte(Element kollision, List<string> hinweise)
    {
        var liste = new List<BcfBeteiligter>();
        if (kollision.Eigenschaft("beteiligte")?.Inhalt is not JsonArray a) return liste;
        foreach (var n in a)
        {
            var b = Beteiligter(n);
            if (b is null) { hinweise.Add($"{kollision.Kennzeichen}: Beteiligter «{n?.ToJsonString()}» nicht lesbar – ausgelassen."); continue; }
            if (!liste.Any(x => x.IfcGuid == b.IfcGuid && x.AuthoringToolId == b.AuthoringToolId && x.Ursprung == b.Ursprung)) liste.Add(b);
        }
        return liste;
    }

    BcfBeteiligter? Beteiligter(JsonNode? n)
    {
        string? elementRef = null, gid = null, ul = null, entitaet = null, name = null;
        if (n is JsonValue v && v.TryGetValue<string>(out var s))
        {
            s = s.Trim();
            if (s.Length == 0) return null;
            if (m.Finde(s) is { } e0) return AusElement(e0);
            var i = s.IndexOf(':');
            if (i > 0 && i < s.Length - 1) { ul = s[..i]; gid = s[(i + 1)..]; }   // «Unterlage-Id:GlobalId» wie Pruefkoerper.Kennung
            else if (BcfFormat.IstIfcGuid(s)) gid = s;
            else elementRef = s;
        }
        else if (n is JsonObject o)
        {
            elementRef = Feld(o, "element", "elementId", "id", "kennzeichen");
            gid = Feld(o, "globalId", "ifcGuid", "ifc", "guid");
            ul = Feld(o, "unterlage", "unterlageId");
            if (ul is null && Feld(o, "quelle") is { } q && m.Finde(q) is { } qe && p.Katalog.IstArt(qe, "Unterlage")) ul = qe.Id;
            entitaet = Feld(o, "entitaet", "klasse");
            name = Feld(o, "name");
        }
        else return null;

        if (elementRef is not null && m.Finde(elementRef) is { } e) return AusElement(e);
        if (gid is not null)
        {
            gid = BcfFormat.GlobalIdBasis(gid)!;
            if (ElementZuGlobalId(gid) is { } eg) return AusElement(eg);
            var u = ul is null ? null : m.Finde(ul);
            var un = BcfFormat.UnterlageName(u);
            var kurz = $"{entitaet ?? "IFC-Produkt"} {gid}";
            var anzeige = kurz + (name is null ? "" : $" «{name}»") + (u is null ? (ul is null ? "" : $", Unterlage {ul}") : $", Unterlage {u.Kennzeichen}" + (un is null ? "" : $" «{un}»"));
            return BcfFormat.IstIfcGuid(gid)
                ? new(kurz, anzeige, gid, null, null, BcfFormat.LabelFremd, u?.Id ?? ul, null)
                : new(kurz, anzeige, null, gid, un ?? "IFC", BcfFormat.LabelFremd, u?.Id ?? ul, null);
        }
        if (elementRef is not null)
            return new(elementRef, $"{elementRef} (nicht im Modell)", null, elementRef, BcfFormat.Ursprungssystem, "Allgemein", null, null);
        return null;
    }

    BcfBeteiligter AusElement(Element e)
    {
        var gid = BcfFormat.GlobalIdBasis(e.Kennungen?.GetValueOrDefault("ifc"));
        var label = BcfFormat.GewerkLabel(Ableitungen.Gewerk(m, p.Katalog, e));
        var anzeige = $"{e.Kennzeichen} ({e.Klasse}{(string.IsNullOrWhiteSpace(e.Name) ? "" : ", " + e.Name)}), {label}";
        if (BcfFormat.IstIfcGuid(gid))
        {
            var ul = e.Kennungen?.GetValueOrDefault("quelle");
            var un = BcfFormat.UnterlageName(m.Finde(ul));
            return new(e.Kennzeichen, anzeige + $" – IFC-GlobalId {gid}" + (un is null ? "" : $" aus «{un}»"), gid, null, null, label, ul, e.Id);
        }
        return new(e.Kennzeichen, anzeige + $" – KICAD-Id {e.Id}", null, e.Id, BcfFormat.Ursprungssystem, label, null, e.Id);
    }

    static string? Feld(JsonObject o, params string[] namen)
    {
        foreach (var (k, v) in o)
            if (namen.Any(n => n.Equals(k, StringComparison.OrdinalIgnoreCase)) && v is JsonValue jv && jv.TryGetValue<string>(out var s) && s.Trim().Length > 0)
                return s.Trim();
        return null;
    }
}

/// <summary>Kommentarformat der Kollision (ANNAHME, siehe Kopf von BcfFormat.cs): eigener Text plus übernommene BCF-Kommentare als
/// Blöcke «[BCF JJJJ-MM-TT hh:mm · Autor · Guid] Text», Folgezeilen mit zwei Leerzeichen eingerückt. Zeit in Ortszeit.</summary>
public static class BcfKommentare
{
    static readonly Regex Kopf = new(@"^\[BCF (\d{4}-\d{2}-\d{2} \d{2}:\d{2}) · (.+) · ([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\](?: (.*))?$",
        RegexOptions.CultureInvariant);

    public sealed record Zerlegt(string Eigen, List<BcfKommentar> Uebernommen);

    public static Zerlegt Zerlegen(string? text)
    {
        var eigen = new List<string>();
        var fremd = new List<BcfKommentar>();
        if (string.IsNullOrEmpty(text)) return new("", fremd);
        var zeilen = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (int i = 0; i < zeilen.Length; i++)
        {
            var m = Kopf.Match(zeilen[i]);
            if (!m.Success || !Guid.TryParse(m.Groups[3].Value, out var g)) { eigen.Add(zeilen[i]); continue; }
            var teile = new List<string> { m.Groups[4].Success ? m.Groups[4].Value : "" };
            int j = i + 1;
            while (j < zeilen.Length)
            {
                if (zeilen[j].StartsWith("  ", StringComparison.Ordinal)) { teile.Add(zeilen[j][2..]); j++; continue; }
                if (zeilen[j].Trim().Length == 0)
                {
                    // Leerzeile (etwa von einer Oberfläche gekürzt) gehört dazu, wenn danach wieder eine eingerückte Zeile kommt
                    int k = j;
                    while (k < zeilen.Length && zeilen[k].Trim().Length == 0) k++;
                    if (k < zeilen.Length && zeilen[k].StartsWith("  ", StringComparison.Ordinal)) { for (; j < k; j++) teile.Add(""); continue; }
                }
                break;
            }
            var dt = DateTime.ParseExact(m.Groups[1].Value, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            var datum = new DateTimeOffset(dt, TimeZoneInfo.Local.GetUtcOffset(dt));
            fremd.Add(new BcfKommentar(g, datum, m.Groups[2].Value, string.Join("\n", teile).TrimEnd('\n', ' ')));
            i = j - 1;
        }
        return new(string.Join("\n", eigen).Trim(), fremd);
    }

    public static string Block(BcfKommentar c, DateTimeOffset ersatzDatum)
    {
        var datum = (c.Datum ?? ersatzDatum).ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        var autor = Regex.Replace(c.Autor ?? "", @"\s+", " ").Trim();
        if (autor.Length == 0) autor = "unbekannt";
        var zeilen = (c.Text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd('\n', ' ').Split('\n');
        var sb = new StringBuilder($"[BCF {datum} · {autor} · {BcfFormat.Text(c.Guid)}]");
        if (zeilen[0].Length > 0) sb.Append(' ').Append(zeilen[0]);
        foreach (var z in zeilen.Skip(1)) sb.Append("\n  ").Append(z);
        return sb.ToString();
    }

    public static string Anhaengen(string? alt, IEnumerable<BcfKommentar> neue, DateTimeOffset ersatzDatum)
    {
        var basis = (alt ?? "").TrimEnd();
        var bloecke = string.Join("\n", neue.Select(c => Block(c, ersatzDatum)));
        return basis.Length == 0 ? bloecke : basis + "\n" + bloecke;
    }
}
