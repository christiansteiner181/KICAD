// Plansätze für das Serien-PDF (Spezifikation entwurf/serien-pdf.md, Entscheide Christian 19.10.2026): je Gewerk automatisch ein
// Plansatz mit allen Planblättern des Gewerks nach Plannummer; weitere Plansätze von Hand. Dieser Teil liest nur:
//   Gewerk eines Planblatts (gesetzt oder Vorschlag aus Plannummer bzw. Ansichten), Plannummern teilen, natürliche Reihenfolge,
//   Sicht auf alle Plansätze (angelegte nachgeführt, automatische ohne Element angeboten), Dateinamen und Ausgabeordner.
// Geändert wird über die Befehle plansatz_setzen und planblatt_gewerk_setzen (PlansatzBefehle.cs), geschrieben über die Abfrage
// plansatz_pdf (Plansatzausgabe.cs).
//
// Annahmen (nicht aus einer Vorgabe von Christian, im Bericht genannt):
//   - Fachbuchstaben nur H, L, S, K (so steht es in der Spezifikation); andere Buchstaben (z. B. E) ergeben keinen Vorschlag.
//   - Mehrere Gewerke auf einem Blatt (Gewerke der Ansichten) ergeben den Vorschlag «koordination»; keine Angabe «allgemein».
//   - Ordnername ohne Anlass: nur das Datum.
//   - Handsatz ohne Gewerk legt in einen Unterordner mit dem Namen des Plansatzes.

using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Hlks.Kern;

/// <summary>Die Gewerke eines Planblatts (Katalog: Planblatt.gewerk) mit Anzeigename und Fachbuchstabe der Plannummer.</summary>
public static class PlanGewerk
{
    public sealed record Eintrag(string Schluessel, string Name, char? Fach);

    /// <summary>In der Reihenfolge des Katalogs. Der Name dient auch als Ordnername im Ausgang (wie «Sanitär», «Lüftung»).</summary>
    public static readonly IReadOnlyList<Eintrag> Alle =
    [
        // Fach K = Koordination wie in den H+K-Plänen (K01–K06), Entscheid Christian 20.10.2026; Kälte vorerst ohne Fachbuchstaben.
        new("heizung", "Heizung", 'H'), new("lueftung", "Lüftung", 'L'), new("sanitaer", "Sanitär", 'S'), new("kaelte", "Kälte", null),
        new("elektro", "Elektro", null), new("koordination", "Koordination", 'K'), new("allgemein", "Allgemein", null),
    ];

    public static Eintrag? Finde(string? schluessel) =>
        string.IsNullOrWhiteSpace(schluessel) ? null : Alle.FirstOrDefault(g => g.Schluessel.Equals(schluessel.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Gewerk aus Schlüssel oder Anzeigename (sanitaer, Sanitär); Fehler mit der Liste der erlaubten.</summary>
    public static Eintrag Hole(string? text)
    {
        var t = text?.Trim();
        var g = Finde(t) ?? Alle.FirstOrDefault(x => x.Name.Equals(t, StringComparison.OrdinalIgnoreCase));
        return g ?? throw new HlksFehler($"Gewerk «{text}» gibt es nicht.", "Erlaubt: " + string.Join(", ", Alle.Select(x => x.Schluessel)));
    }

    public static string Name(string schluessel) => Finde(schluessel)?.Name ?? schluessel;
}

/// <summary>Plannummern im H+K-Muster «1-0768 H01», «1-0768-S01» oder nur «H01».</summary>
public static class Plannummern
{
    static readonly Regex MitProjekt = new(@"^(?<pn>\d+-\d+)[\s\-_.]+(?<rest>\S.*)$", RegexOptions.CultureInvariant);
    static readonly Regex Fach = new(@"^(?<f>[A-Za-z])\d", RegexOptions.CultureInvariant);
    static readonly char[] Trenner = [' ', '\t', '-', '_', '.'];

    /// <summary>Teilt die Plannummer in Projektnummer und Rest («1-0768 S01» → «1-0768», «S01»). Ist die Projektnummer des Projekts bekannt
    /// (Projektinformation), gilt sie zuerst; sonst das Muster «Ziffern-Ziffern» am Anfang. Ohne Projektnummer in der Nummer bleibt
    /// die Projektnummer des Projekts (oder leer) und die ganze Nummer der Rest.</summary>
    public static (string Projekt, string Nummer) Teilen(string? nummer, string? projektnummer)
    {
        var n = (nummer ?? "").Trim();
        var pn = (projektnummer ?? "").Trim();
        if (n.Length == 0) return (pn, "");
        if (pn.Length > 0 && n.StartsWith(pn, StringComparison.OrdinalIgnoreCase) && (n.Length == pn.Length || Array.IndexOf(Trenner, n[pn.Length]) >= 0))
            return (pn, n[pn.Length..].TrimStart(Trenner));
        var t = MitProjekt.Match(n);
        if (t.Success) return (t.Groups["pn"].Value, t.Groups["rest"].Value);
        return (pn, n);
    }

    /// <summary>Fachbuchstabe nach der Projektnummer (H, L, S, K …) oder null.</summary>
    public static char? Fachbuchstabe(string? nummer, string? projektnummer)
    {
        var (_, rest) = Teilen(nummer, projektnummer);
        var t = Fach.Match(rest);
        return t.Success ? char.ToUpperInvariant(t.Groups["f"].Value[0]) : null;
    }

    /// <summary>Natürliche Reihenfolge (H2 vor H10, Gross-/Kleinschreibung egal).</summary>
    public static int Vergleichen(string? a, string? b)
    {
        a ??= ""; b ??= "";
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
            {
                int i0 = i, j0 = j;
                while (i < a.Length && char.IsDigit(a[i])) i++;
                while (j < b.Length && char.IsDigit(b[j])) j++;
                var na = a[i0..i].TrimStart('0'); var nb = b[j0..j].TrimStart('0');
                if (na.Length != nb.Length) return na.Length < nb.Length ? -1 : 1;
                int c = string.CompareOrdinal(na, nb);
                if (c != 0) return c;
                if (i - i0 != j - j0) return i - i0 < j - j0 ? -1 : 1;
            }
            else
            {
                int c = char.ToUpperInvariant(a[i]).CompareTo(char.ToUpperInvariant(b[j]));
                if (c != 0) return c;
                i++; j++;
            }
        }
        return (a.Length - i).CompareTo(b.Length - j);
    }
}

/// <summary>Gewerk eines Planblatts: gesetzt (Eigenschaft gewerk) oder Vorschlag mit Quelle.</summary>
public sealed record BlattGewerk(string Gewerk, bool Gesetzt, string Quelle);

/// <summary>Ein Blatt in einem Plansatz.</summary>
public sealed record BlattSicht(Element Blatt, bool Dabei, BlattGewerk Gewerk);

/// <summary>Ein Plansatz, wie ihn die Ausgabe sieht: angelegt (Element) oder automatisch angeboten (Element null).</summary>
public sealed class SatzSicht
{
    public Element? Element { get; init; }
    /// <summary>Kennzeichen des Plansatzes bzw. «auto:&lt;gewerk&gt;» für einen noch nicht angelegten automatischen Satz.</summary>
    public required string Schluessel { get; init; }
    public required string Name { get; init; }
    public string? Gewerk { get; init; }
    public bool Automatisch { get; init; }
    /// <summary>Alle Blätter des Satzes in Reihenfolge, auch die abgewählten.</summary>
    public List<BlattSicht> Blaetter { get; init; } = [];
    /// <summary>Gespeicherte letzte Ausgabe-Einstellung { anlass, ordner, einzeln, zeit }.</summary>
    public JsonObject Ausgabe { get; init; } = new();
    public IEnumerable<BlattSicht> Gewaehlt => Blaetter.Where(b => b.Dabei);
}

public static class Plaene
{
    public const string AutoVorsatz = "auto:";

    // ------------------------------------------------------------------ Projekt- und Objektangaben

    /// <summary>Projektnummer aus der Projektinformation; fehlt sie, die einzige Projektnummer, die in den Plannummern steht (Muster «1-0768 S01»).</summary>
    public static string? Projektnummer(Modell m)
    {
        var info = m.DerKlasse("Projektinformation").OrderBy(e => e.Kennzeichen, StringComparer.Ordinal).Select(e => e.Text("projektnummer")?.Trim())
            .FirstOrDefault(t => !string.IsNullOrEmpty(t));
        if (info is not null) return info;
        var ausNummern = m.DerKlasse("Planblatt").Select(b => Plannummern.Teilen(b.Text("nummer"), null).Projekt).Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return ausNummern.Count == 1 ? ausNummern[0] : null;
    }

    static string Fest(string s) => s.Normalize(NormalizationForm.FormC);

    /// <summary>Objektordner aus der Ablage …\&lt;Objekt&gt;\c_Pläne und Modelle\e_KICAD; null, wenn das Projekt anders liegt.</summary>
    public static string? Objektordner(Projekt p)
    {
        if (p.Speicher?.Ort is not { Length: > 0 } ort) return null;
        try
        {
            var kicad = new DirectoryInfo(Path.GetFullPath(ort).TrimEnd('\\', '/'));
            var plaene = kicad.Parent;
            if (kicad.Name.Equals("e_KICAD", StringComparison.OrdinalIgnoreCase) && plaene is not null
                && Fest(plaene.Name).Equals(Fest("c_Pläne und Modelle"), StringComparison.OrdinalIgnoreCase) && plaene.Parent is { } objekt)
                return objekt.FullName;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { }
        return null;
    }

    /// <summary>Wurzel der Ausgabe: …\&lt;Objekt&gt;\c_Pläne und Modelle\d_Ausgang\b_Pläne, sonst &lt;Projektordner&gt;\ausgabe, ohne Ablage %TEMP%\HLKS_CAD\ausgabe.</summary>
    public static string Ausgabewurzel(Projekt p)
    {
        if (Objektordner(p) is { } objekt) return Path.Combine(objekt, "c_Pläne und Modelle", "d_Ausgang", "b_Pläne");
        if (p.Speicher?.Ort is { Length: > 0 } ort) return Path.Combine(ort, "ausgabe");
        return Path.Combine(Path.GetTempPath(), "HLKS_CAD", "ausgabe");
    }

    /// <summary>Unterordner im Ausgabeordner: Gewerkname, bei einem Handsatz ohne Gewerk der Name des Plansatzes.</summary>
    public static string Unterordner(SatzSicht s) =>
        Planausgabe.DateinameSicher(s.Gewerk is not null ? PlanGewerk.Name(s.Gewerk) : s.Name);

    /// <summary>Ordnervorschlag &lt;Wurzel&gt;\&lt;TT.MM.JJJJ&gt;_&lt;Anlass&gt;\&lt;Gewerk&gt; (ohne Anlass nur das Datum).</summary>
    public static string OrdnerVorschlag(Projekt p, SatzSicht s, string? anlass, DateOnly datum)
    {
        var tag = datum.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        var a = (anlass ?? "").Trim();
        var name = a.Length == 0 ? tag : tag + "_" + Planausgabe.DateinameSicher(a);
        return Path.Combine(Ausgabewurzel(p), name, Unterordner(s));
    }

    // ------------------------------------------------------------------ Dateinamen

    /// <summary>Sammel-PDF «&lt;Projektnummer&gt;_&lt;Plansatzname&gt;_&lt;JJJJ-MM-TT&gt;.pdf».</summary>
    public static string Sammelname(string? projektnummer, string satzname, DateOnly datum)
    {
        var teile = new List<string>();
        if (!string.IsNullOrWhiteSpace(projektnummer)) teile.Add(projektnummer.Trim());
        teile.Add(satzname.Trim());
        teile.Add(datum.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        return Planausgabe.DateinameSicher(string.Join("_", teile)) + ".pdf";
    }

    /// <summary>Plannummer ohne Projektnummer; ohne Nummer das Kennzeichen des Blatts.</summary>
    public static string KurzNummer(Element blatt, string? projektnummer)
    {
        var (_, rest) = Plannummern.Teilen(blatt.Text("nummer"), projektnummer);
        return rest.Length > 0 ? rest : blatt.Kennzeichen;
    }

    /// <summary>Einzel-PDF im H+K-Muster «&lt;Projektnummer&gt;-&lt;Plannummer ohne Projektnummer&gt;-&lt;Titel&gt;-.pdf»
    /// (z. B. «1-0768-S01-Sanitär 1.Untergeschoss-.pdf»); ungültige Zeichen sind ersetzt.</summary>
    public static string Einzelname(Element blatt, string? projektnummer)
    {
        var (pn, rest) = Plannummern.Teilen(blatt.Text("nummer"), projektnummer);
        if (rest.Length == 0) rest = blatt.Kennzeichen;
        var teile = new[] { pn, rest, blatt.Text("titel")?.Trim() ?? "" }.Where(t => t.Length > 0);
        return Planausgabe.DateinameSicher(string.Join("-", teile) + "-") + ".pdf";
    }

    /// <summary>Einzelnamen aller Blätter; gleiche Namen bekommen « (2)», « (3)» vor der Endung.</summary>
    public static Dictionary<string, string> Einzelnamen(IEnumerable<Element> blaetter, string? projektnummer)
    {
        var belegt = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ergebnis = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var b in blaetter)
        {
            var basis = Einzelname(b, projektnummer);
            var name = basis;
            for (int n = 2; !belegt.Add(name); n++) name = basis[..^4] + $" ({n}).pdf";
            ergebnis[b.Id] = name;
        }
        return ergebnis;
    }

    /// <summary>Lesezeichen im Sammel-PDF «S01 Sanitär 1.UG» (Plannummer ohne Projektnummer, Titel).</summary>
    public static string Lesezeichen(Element blatt, string? projektnummer) =>
        $"{KurzNummer(blatt, projektnummer)} {blatt.Text("titel")?.Trim() ?? ""}".Trim();

    // ------------------------------------------------------------------ Gewerk eines Blatts

    public static IEnumerable<Element> Blaetter(Modell m) => m.DerKlasse("Planblatt");

    /// <summary>Planblätter nach Plannummer (natürlich); Blätter ohne Nummer zuletzt, nach Kennzeichen.</summary>
    public static int Reihenfolge(Element a, Element b)
    {
        var na = a.Text("nummer")?.Trim() ?? ""; var nb = b.Text("nummer")?.Trim() ?? "";
        if ((na.Length == 0) != (nb.Length == 0)) return na.Length == 0 ? 1 : -1;
        int c = Plannummern.Vergleichen(na, nb);
        return c != 0 ? c : Plannummern.Vergleichen(a.Kennzeichen, b.Kennzeichen);
    }

    /// <summary>Gewerk des Blatts: die Eigenschaft gewerk; sonst Vorschlag aus dem Fachbuchstaben der Plannummer, sonst aus den Gewerken
    /// der Ansichten auf dem Blatt (eines → dieses, mehrere → koordination), sonst allgemein.</summary>
    public static BlattGewerk GewerkVon(Modell m, Element blatt, string? projektnummer)
    {
        if (PlanGewerk.Finde(blatt.Text("gewerk")) is { } gesetzt)
            return new BlattGewerk(gesetzt.Schluessel, true, "am Planblatt gesetzt");

        if (Plannummern.Fachbuchstabe(blatt.Text("nummer"), projektnummer) is { } f
            && PlanGewerk.Alle.FirstOrDefault(g => g.Fach == f) is { } nachNummer)
            return new BlattGewerk(nachNummer.Schluessel, false, $"Fachbuchstabe {f} der Plannummer");

        var gewerke = new List<string>();
        foreach (var af in m.Kinder(blatt.Id).Where(e => e.Klasse == "Ansichtsfenster"))
        {
            if (m.Finde(af.Bezuege.GetValueOrDefault("ansicht")) is not { } ansicht) continue;
            if (ansicht.Text("darstellung") == KoordinationGrafik.Darstellung) { if (!gewerke.Contains("koordination")) gewerke.Add("koordination"); continue; }   // Modul 7 Teil B1
            if (ansicht.Eigenschaft("gewerke")?.Inhalt is not JsonArray liste) continue;
            foreach (var n in liste)
                if (n is JsonValue v && v.TryGetValue<string>(out var s) && PlanGewerk.Finde(s) is { } g && !gewerke.Contains(g.Schluessel)) gewerke.Add(g.Schluessel);
        }
        if (gewerke.Count == 1) return new BlattGewerk(gewerke[0], false, $"Gewerk der Ansichten ({PlanGewerk.Name(gewerke[0])})");
        if (gewerke.Count > 1)
            return new BlattGewerk("koordination", false, "mehrere Gewerke auf dem Blatt (" + string.Join(", ", gewerke.Select(PlanGewerk.Name)) + ")");
        return new BlattGewerk("allgemein", false, "keine Angabe in Plannummer und Ansichten");
    }

    // ------------------------------------------------------------------ Plansätze

    static List<string> Liste(Element satz, string name) =>
        satz.Eigenschaft(name)?.Inhalt is JsonArray a
            ? a.Select(n => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null).OfType<string>().Where(s => s.Length > 0).ToList()
            : [];

    static string SatzName(Element satz) => satz.Text("name") is { Length: > 0 } n ? n : satz.Name ?? satz.Kennzeichen;

    static JsonObject AusgabeVon(Element satz) =>
        satz.Eigenschaft("ausgabe")?.Inhalt is JsonObject o ? (JsonObject)o.DeepClone() : new JsonObject();

    /// <summary>Reihenfolge eines automatischen Satzes: die gespeicherte (soweit die Blätter noch dazugehören), neue Blätter
    /// an ihrer Stelle nach Plannummer eingefügt – ohne gespeicherte Reihenfolge einfach nach Plannummer.</summary>
    static List<Element> Geordnet(Modell m, List<Element> mitglieder, List<string> gespeichert)
    {
        var ergebnis = new List<Element>();
        foreach (var kz in gespeichert)
            if (m.Finde(kz) is { } b && mitglieder.Contains(b) && !ergebnis.Contains(b)) ergebnis.Add(b);
        foreach (var neu in mitglieder.Where(b => !ergebnis.Contains(b)).OrderBy(b => b, Comparer<Element>.Create(Reihenfolge)).ToList())
        {
            int i = ergebnis.FindIndex(x => Reihenfolge(x, neu) > 0);
            if (i < 0) ergebnis.Add(neu); else ergebnis.Insert(i, neu);
        }
        return ergebnis;
    }

    /// <summary>Alle Plansätze: automatische je Gewerk (angelegte nachgeführt, fehlende angeboten; Reihenfolge der Gewerke wie im Katalog),
    /// danach die von Hand nach Kennzeichen.</summary>
    public static List<SatzSicht> Alle(Modell m)
    {
        var pn = Projektnummer(m);
        var gewerkVon = Blaetter(m).ToDictionary(b => b.Id, b => GewerkVon(m, b, pn), StringComparer.Ordinal);
        var saetze = m.DerKlasse("Plansatz").OrderBy(s => s.Kennzeichen, Comparer<string>.Create(Plannummern.Vergleichen)).ToList();
        var ergebnis = new List<SatzSicht>();

        SatzSicht Automatisch(Element? satz, string gewerk)
        {
            var mitglieder = Blaetter(m).Where(b => gewerkVon[b.Id].Gewerk == gewerk).ToList();
            var abgew = satz is null ? [] : Liste(satz, "abgewaehlt").Select(kz => m.Finde(kz)?.Id).OfType<string>().ToHashSet(StringComparer.Ordinal);
            var geordnet = Geordnet(m, mitglieder, satz is null ? [] : Liste(satz, "blaetter"));
            return new SatzSicht
            {
                Element = satz, Schluessel = satz?.Kennzeichen ?? AutoVorsatz + gewerk, Name = satz is null ? "Plansatz " + PlanGewerk.Name(gewerk) : SatzName(satz),
                Gewerk = gewerk, Automatisch = true, Ausgabe = satz is null ? new JsonObject() : AusgabeVon(satz),
                Blaetter = geordnet.Select(b => new BlattSicht(b, !abgew.Contains(b.Id), gewerkVon[b.Id])).ToList(),
            };
        }

        foreach (var g in PlanGewerk.Alle)
        {
            var vorhanden = saetze.Where(s => AlsBool(s, "automatisch") && PlanGewerk.Finde(s.Text("gewerk"))?.Schluessel == g.Schluessel).ToList();
            foreach (var s in vorhanden) ergebnis.Add(Automatisch(s, g.Schluessel));
            if (vorhanden.Count == 0 && gewerkVon.Values.Any(x => x.Gewerk == g.Schluessel)) ergebnis.Add(Automatisch(null, g.Schluessel));
        }

        foreach (var s in saetze.Where(s => !AlsBool(s, "automatisch")))
        {
            var abgew = Liste(s, "abgewaehlt").Select(kz => m.Finde(kz)?.Id).OfType<string>().ToHashSet(StringComparer.Ordinal);
            var blaetter = new List<Element>();
            foreach (var kz in Liste(s, "blaetter"))
                if (m.Finde(kz) is { Klasse: "Planblatt" } b && !blaetter.Contains(b)) blaetter.Add(b);
            ergebnis.Add(new SatzSicht
            {
                Element = s, Schluessel = s.Kennzeichen, Name = SatzName(s), Gewerk = PlanGewerk.Finde(s.Text("gewerk"))?.Schluessel, Automatisch = false,
                Ausgabe = AusgabeVon(s),
                Blaetter = blaetter.Select(b => new BlattSicht(b, !abgew.Contains(b.Id), gewerkVon[b.Id])).ToList(),
            });
        }
        return ergebnis;
    }

    internal static bool AlsBool(Element e, string name) =>
        e.Eigenschaft(name)?.Inhalt is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    internal static List<string> ListeVon(Element e, string name) => Liste(e, name);

    /// <summary>Plansatz über Kennzeichen, Id, Name, «auto:&lt;gewerk&gt;» oder den Gewerkschlüssel/-namen (der automatische Satz des Gewerks,
    /// angelegt oder angeboten).</summary>
    public static SatzSicht Finde(Modell m, string? bezug)
    {
        var alle = Alle(m);
        var t = bezug?.Trim() ?? "";
        SatzSicht? s = null;
        if (t.Length > 0)
        {
            var el = m.Finde(t);
            if (el is { Klasse: "Plansatz" }) s = alle.FirstOrDefault(x => x.Element?.Id == el.Id);
            s ??= alle.FirstOrDefault(x => x.Name.Equals(t, StringComparison.OrdinalIgnoreCase));
            if (s is null)
            {
                var g = t.StartsWith(AutoVorsatz, StringComparison.OrdinalIgnoreCase) ? PlanGewerk.Hole(t[AutoVorsatz.Length..]) : PlanGewerk.Alle.FirstOrDefault(x => x.Schluessel.Equals(t, StringComparison.OrdinalIgnoreCase) || x.Name.Equals(t, StringComparison.OrdinalIgnoreCase));
                if (g is not null)
                {
                    s = alle.FirstOrDefault(x => x.Automatisch && x.Gewerk == g.Schluessel)
                        ?? throw new HlksFehler($"Für das Gewerk {g.Name} gibt es keine Planblätter und keinen Plansatz.", "Planblätter dem Gewerk zuordnen (planblatt_gewerk_setzen) oder einen Plansatz von Hand anlegen (plansatz_setzen).");
                }
            }
        }
        return s ?? throw new HlksFehler($"Plansatz «{bezug}» gibt es nicht.",
            alle.Count == 0 ? "Es gibt noch keine Planblätter (planblatt_anlegen)." : "Plansätze: " + string.Join(", ", alle.Select(x => $"{x.Schluessel} ({x.Name})")));
    }

    // ------------------------------------------------------------------ Hinweise und Auskunft

    public static int AnzahlFenster(Modell m, Element blatt) => m.Kinder(blatt.Id).Count(e => e.Klasse == "Ansichtsfenster");

    /// <summary>Hinweise zu den gewählten Blättern: Blatt ohne Ansicht, ohne Plannummer.</summary>
    public static List<string> Hinweise(Modell m, IEnumerable<BlattSicht> blaetter)
    {
        var h = new List<string>();
        foreach (var b in blaetter)
        {
            var name = b.Blatt.Kennzeichen + (b.Blatt.Text("nummer") is { Length: > 0 } n ? $" ({n})" : "");
            if (AnzahlFenster(m, b.Blatt) == 0) h.Add($"{name} hat keine Ansicht (leere Seite mit Plankopf).");
            if (string.IsNullOrWhiteSpace(b.Blatt.Text("nummer"))) h.Add($"{b.Blatt.Kennzeichen} hat keine Plannummer (Dateiname und Lesezeichen mit dem Kennzeichen).");
        }
        return h;
    }

    /// <summary>Datum der Ausgabe aus «JJJJ-MM-TT» oder «TT.MM.JJJJ»; ohne Angabe heute.</summary>
    public static DateOnly Datum(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return DateOnly.FromDateTime(DateTime.Now);
        if (DateOnly.TryParseExact(text.Trim(), ["yyyy-MM-dd", "dd.MM.yyyy", "d.M.yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return d;
        throw new HlksFehler($"Datum «{text}» ist nicht lesbar.", "Form JJJJ-MM-TT (2026-10-19) oder TT.MM.JJJJ (19.10.2026).");
    }

    static string Format(Element b) => (b.Text("format") ?? Blattformate.Vorgabe).ToUpperInvariant();

    /// <summary>Plansatz als JSON für die Abfrage plansaetze.</summary>
    public static JsonObject Auskunft(Projekt p, SatzSicht s, string? anlass, DateOnly datum)
    {
        var m = p.Modell;
        var pn = Projektnummer(m);
        var einzel = Einzelnamen(s.Blaetter.Select(b => b.Blatt), pn);
        var gespeichert = s.Ausgabe;
        var anlassTeil = anlass ?? gespeichert["anlass"]?.GetValue<string>() ?? "";
        var vorschlagOrdner = OrdnerVorschlag(p, s, anlassTeil, datum);
        string? eigenerOrdner = gespeichert["ordner"] is JsonValue ov && ov.TryGetValue<string>(out var os) && os.Length > 0 ? os : null;

        var weitere = s.Automatisch ? [] : Blaetter(m).Where(b => s.Blaetter.All(x => x.Blatt.Id != b.Id)).OrderBy(b => b, Comparer<Element>.Create(Reihenfolge)).ToList();
        return new JsonObject
        {
            ["plansatz"] = s.Element?.Kennzeichen,
            ["schluessel"] = s.Schluessel,
            ["name"] = s.Name,
            ["gewerk"] = s.Gewerk,
            ["gewerkName"] = s.Gewerk is null ? null : PlanGewerk.Name(s.Gewerk),
            ["automatisch"] = s.Automatisch,
            ["angelegt"] = s.Element is not null,
            ["anzahl"] = s.Blaetter.Count,
            ["dabei"] = s.Blaetter.Count(b => b.Dabei),
            ["blaetter"] = new JsonArray(s.Blaetter.Select(b => (JsonNode)new JsonObject
            {
                ["planblatt"] = b.Blatt.Kennzeichen,
                ["nummer"] = b.Blatt.Text("nummer") ?? "",
                ["nummerKurz"] = KurzNummer(b.Blatt, pn),
                ["titel"] = b.Blatt.Text("titel") ?? "",
                ["format"] = Format(b.Blatt),
                ["ausrichtung"] = Planausgabe.Hoch(b.Blatt) ? "hoch" : "quer",
                ["gewerk"] = b.Gewerk.Gewerk,
                ["gewerkName"] = PlanGewerk.Name(b.Gewerk.Gewerk),
                ["gewerkHerkunft"] = b.Gewerk.Gesetzt ? "gesetzt" : "vorschlag",
                ["gewerkQuelle"] = b.Gewerk.Quelle,
                ["dabei"] = b.Dabei,
                ["ansichten"] = AnzahlFenster(m, b.Blatt),
                ["einzelDatei"] = einzel[b.Blatt.Id],
                ["lesezeichen"] = Lesezeichen(b.Blatt, pn),
            }).ToArray()),
            ["weitere"] = new JsonArray(weitere.Select(b => (JsonNode)new JsonObject
            {
                ["planblatt"] = b.Kennzeichen, ["nummer"] = b.Text("nummer") ?? "", ["titel"] = b.Text("titel") ?? "", ["format"] = Format(b),
            }).ToArray()),
            ["ausgabe"] = gespeichert,
            ["vorschlag"] = new JsonObject
            {
                ["anlass"] = anlassTeil,
                ["ordner"] = vorschlagOrdner,
                ["ordnerWirksam"] = eigenerOrdner ?? vorschlagOrdner,
                ["sammelDatei"] = Sammelname(pn, s.Name, datum),
            },
            ["hinweise"] = new JsonArray(Hinweise(m, s.Gewaehlt).Select(h => (JsonNode)JsonValue.Create(h)!).ToArray()),
        };
    }
}
