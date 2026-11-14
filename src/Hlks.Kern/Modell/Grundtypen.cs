// Grundtypen des Datenmodells. Das Modell denkt vom System her: Elemente mit Anschlüssen, Verbindungen zwischen
// Anschlüssen (Topologie), Zuordnung zu Geschoss/Raum/System und eine Geometrie, die am Element hängt.
// Alle Längen in Millimetern, Koordinaten im Projektsystem (x Ost, y Nord, z Höhe über ±0.00).

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Hlks.Kern;

/// <summary>Herkunft eines Werts oder einer Änderung: wer, wann, auf welchem Weg, aus welcher Quelle.</summary>
public sealed record Herkunft
{
    /// <summary>mensch | ki | ifc | import | berechnung | katalog | norm | beispiel</summary>
    public string Art { get; init; } = "mensch";
    public string Akteur { get; init; } = "";
    public DateTimeOffset Zeit { get; init; }
    /// <summary>Fundstelle, z. B. «SIA 410/1 Ziff. 3» oder «Beispielprojekt, fiktiv».</summary>
    public string? Quelle { get; init; }
    /// <summary>Befehl, mit dem der Wert gesetzt wurde.</summary>
    public string? Befehl { get; init; }

    /// <summary>familie: Wert aus der Familie des Elements (Quelle = Familie und Typ); unterlage: Wert aus einer
    /// Herstellerunterlage (Quelle = Befund-Id mit Datei, Seite, Stelle) – beide Modul 2; baustelle: Rückmeldung von der Baustelle (BIM to field,
    /// vom Menschen übernommen).</summary>
    public static readonly string[] Arten = ["mensch", "ki", "ifc", "import", "berechnung", "katalog", "norm", "beispiel", "hkap", "familie", "unterlage", "baustelle"];
}

/// <summary>Ein Eigenschaftswert mit Einheit und Herkunft.</summary>
public sealed class Wert
{
    [JsonPropertyName("wert")] public JsonNode? Inhalt { get; set; }
    public string? Einheit { get; set; }
    public Herkunft Herkunft { get; set; } = new();

    public double? AlsZahl() => Inhalt is JsonValue v && v.GetValueKind() == JsonValueKind.Number ? Json.Zahl(v) : null;
    public string? AlsText() => Inhalt is JsonValue v && v.TryGetValue<string>(out var s) ? s : Inhalt?.ToJsonString();
    public override string ToString() => Einheit is null ? (AlsText() ?? "") : $"{AlsText()} {Einheit}";
    /// <summary>Tiefe Kopie (Inhalt geklont; Herkunft ist unveränderlich und wird geteilt).</summary>
    public Wert Kopie() => new() { Inhalt = Inhalt?.DeepClone(), Einheit = Einheit, Herkunft = Herkunft };
}

/// <summary>Ein Anschluss eines Elements: Medium, Fliessrichtung, Grösse (Rohr: DN; Kanal: d bzw. b × h in mm).</summary>
public sealed class Anschluss
{
    public string Name { get; set; } = "";
    /// <summary>Kennung aus medien.json; «*» = übernimmt das Medium des Systems.</summary>
    public string Medium { get; set; } = "*";
    /// <summary>rein | raus | beide – bezogen auf die Fliessrichtung des Mediums.</summary>
    public string Fluss { get; set; } = "beide";
    /// <summary>Rohr: Nennweite DN.</summary>
    public int? Dn { get; set; }
    /// <summary>Kanal rund: Durchmesser in mm (Nennmass wie Kanal.durchmesser; Modul 2).</summary>
    public int? D { get; set; }
    /// <summary>Kanal rechteckig: Breite in mm (Modul 2).</summary>
    public int? B { get; set; }
    /// <summary>Kanal rechteckig: Höhe in mm (Modul 2).</summary>
    public int? H { get; set; }
    /// <summary>Gewinde wie in der Unterlage, z. B. «Rp 3/4», «G 1/2» (Modul 2, aus der Familie).</summary>
    public string? Gewinde { get; set; }
    public Anschluss Kopie() => new() { Name = Name, Medium = Medium, Fluss = Fluss, Dn = Dn, D = D, B = B, H = H, Gewinde = Gewinde };
}

/// <summary>Geometrie in mm. Punkt (Lage), Linie/Polylinie (Verlauf), Polygon (Grundfläche, mit Höhe als Prisma).</summary>
public sealed class Geometrie
{
    /// <summary>punkt | linie | polylinie | polygon</summary>
    public string Art { get; set; } = "punkt";
    public List<double[]> Punkte { get; set; } = [];
    public double? Hoehe { get; set; }
    public double? Dicke { get; set; }

    public double Laenge()
    {
        double l = 0;
        for (int i = 1; i < Punkte.Count; i++) l += Abstand(Punkte[i - 1], Punkte[i]);
        return l;
    }

    public double Flaeche()
    {
        if (Art != "polygon" || Punkte.Count < 3) return 0;
        double s = 0;
        for (int i = 0; i < Punkte.Count; i++)
        {
            var a = Punkte[i]; var b = Punkte[(i + 1) % Punkte.Count];
            s += a[0] * b[1] - b[0] * a[1];
        }
        return Math.Abs(s) / 2;
    }

    /// <summary>Punkt-in-Polygon (Strahlverfahren) in der xy-Ebene.</summary>
    public bool Enthaelt(double x, double y)
    {
        if (Art != "polygon" || Punkte.Count < 3) return false;
        bool drin = false;
        for (int i = 0, j = Punkte.Count - 1; i < Punkte.Count; j = i++)
        {
            var pi = Punkte[i]; var pj = Punkte[j];
            if ((pi[1] > y) != (pj[1] > y) && x < (pj[0] - pi[0]) * (y - pi[1]) / (pj[1] - pi[1]) + pi[0]) drin = !drin;
        }
        return drin;
    }

    public static double Abstand(double[] a, double[] b)
    {
        double dx = b[0] - a[0], dy = b[1] - a[1], dz = (b.Length > 2 ? b[2] : 0) - (a.Length > 2 ? a[2] : 0);
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    public Geometrie Kopie() => new() { Art = Art, Punkte = Punkte.Select(p => (double[])p.Clone()).ToList(), Hoehe = Hoehe, Dicke = Dicke };
}

/// <summary>Ein Element des Modells. Klasse aus klassen.json bestimmt Schicht, erlaubte Eigenschaften und Anschlüsse.</summary>
public sealed class Element
{
    public string Id { get; set; } = "";
    public string Kennzeichen { get; set; } = "";
    public string Klasse { get; set; } = "";
    public string? Name { get; set; }
    /// <summary>Übergeordnetes Element der Hierarchie (Strang → System → Anlage, Geschoss → Gebäude).</summary>
    public string? TeilVon { get; set; }
    public string? Geschoss { get; set; }
    public string? Raum { get; set; }
    public string? System { get; set; }
    public Dictionary<string, Wert> Eigenschaften { get; set; } = [];
    public List<Anschluss> Anschluesse { get; set; } = [];
    public Geometrie? Geometrie { get; set; }
    /// <summary>Weitere Bezüge mit Rolle, z. B. Datenpunkt «an» → Bauteil, Regelkreis «messung» → Datenpunkt.</summary>
    public Dictionary<string, string> Bezuege { get; set; } = [];
    /// <summary>Kennungen in fremden Systemen (Modul 1b): «ifc» = GlobalId im IFC, «quelle» = Id der Unterlage, aus der
    /// das Element übernommen ist; später «hkap» (Modul 1d). Über sie gleicht ein neuer Import ab (neu, geändert, gelöscht).</summary>
    public Dictionary<string, string>? Kennungen { get; set; }
    public int Version { get; set; }
    /// <summary>Paket, aus dem der aktuelle Stand stammt. Entscheidet Konflikte: die neuere Änderung (grössere Paket-Id) gewinnt.</summary>
    public string? Paket { get; set; }
    public Herkunft? Angelegt { get; set; }
    public Herkunft? Geaendert { get; set; }

    public Wert? Eigenschaft(string name) => Eigenschaften.TryGetValue(name, out var w) ? w : null;
    public double? Zahl(string name) => Eigenschaft(name)?.AlsZahl();
    public string? Text(string name) => Eigenschaft(name)?.AlsText();
    public Anschluss? AnschlussNamens(string name) => Anschluesse.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Tiefe Kopie ohne JSON-Umweg (Gesamtprüfung Frage 46, 14.11.2026: jeder Befehl kopiert das Modell – der JSON-Umweg kostete bei 8000 Elementen
    /// rund 200 ms). Gleiches Ergebnis wie Serialisieren und Lesen (Test KopieTests); neue Felder hier nachtragen.</summary>
    public Element Kopie() => new()
    {
        Id = Id, Kennzeichen = Kennzeichen, Klasse = Klasse, Name = Name, TeilVon = TeilVon, Geschoss = Geschoss, Raum = Raum, System = System,
        Eigenschaften = Eigenschaften.ToDictionary(x => x.Key, x => x.Value.Kopie()),
        Anschluesse = Anschluesse.Select(a => a.Kopie()).ToList(),
        Geometrie = Geometrie?.Kopie(),
        Bezuege = new Dictionary<string, string>(Bezuege),
        Kennungen = Kennungen is null ? null : new Dictionary<string, string>(Kennungen),
        Version = Version, Paket = Paket, Angelegt = Angelegt, Geaendert = Geaendert,
    };

    /// <summary>Kopie über JSON (wie vor dem 14.11.2026) – nur für den Vergleichstest.</summary>
    public Element KopieUeberJson() => JsonSerializer.Deserialize<Element>(JsonSerializer.Serialize(this, Json.Optionen), Json.Optionen)!;
}

public sealed record Anschlusspunkt(string Element, string Anschluss);

/// <summary>Verbindung zweier Anschlüsse. Von → Nach ist keine Fliessrichtung; die steht an den Anschlüssen.</summary>
public sealed class Verbindung
{
    public string Id { get; set; } = "";
    public Anschlusspunkt Von { get; set; } = new("", "");
    public Anschlusspunkt Nach { get; set; } = new("", "");
    public int Version { get; set; }
    /// <summary>Paket, aus dem der aktuelle Stand stammt (Konfliktentscheid wie beim Element).</summary>
    public string? Paket { get; set; }
    public Herkunft? Angelegt { get; set; }

    public Verbindung Kopie() => new() { Id = Id, Von = Von, Nach = Nach, Version = Version, Paket = Paket, Angelegt = Angelegt };
    public Anschlusspunkt? Gegenueber(string element, string anschluss) =>
        Von.Element == element && Von.Anschluss.Equals(anschluss, StringComparison.OrdinalIgnoreCase) ? Nach :
        Nach.Element == element && Nach.Anschluss.Equals(anschluss, StringComparison.OrdinalIgnoreCase) ? Von : null;
}

/// <summary>Eine Änderung am Modell. Trägt Vorher- und Nachher-Zustand, damit Pakete selbsterklärend und umkehrbar sind.</summary>
public sealed class Operation
{
    /// <summary>element.setzen | element.loeschen | verbindung.setzen | verbindung.loeschen</summary>
    public string Art { get; set; } = "";
    public string Id { get; set; } = "";
    /// <summary>Version vor der Änderung (0 = neu). Weicht der Stand ab, liegt ein Konflikt vor.</summary>
    public int Basis { get; set; }
    public Element? Element { get; set; }
    public Element? ElementVorher { get; set; }
    public Verbindung? Verbindung { get; set; }
    public Verbindung? VerbindungVorher { get; set; }
}

/// <summary>Ein Änderungspaket: alle Operationen eines Befehls. Pakete werden nie überschrieben.</summary>
public sealed class Paket
{
    public string Id { get; set; } = "";
    public string Akteur { get; set; } = "";
    public string AkteurArt { get; set; } = "mensch";
    public string Geraet { get; set; } = "";
    public DateTimeOffset Zeit { get; set; }
    public string Befehl { get; set; } = "";
    /// <summary>Programmstand, der das Paket geschrieben hat (Frage 42; Projekt.Programmstand), null = vor dem 14.11.2026.</summary>
    public long? Programmstand { get; set; }
    public JsonObject? Parameter { get; set; }
    public string? RueckgaengigVon { get; set; }
    /// <summary>Warum der Befehl ausgeführt wurde (optional, Protokoll für Mustersituationen und Prüfstand; Rang 4, 30.10.2026).</summary>
    public string? Begruendung { get; set; }
    /// <summary>Prozessschritt bzw. Mustersituation, zu der der Befehl gehört (optional, frei benannt).</summary>
    public string? Prozessschritt { get; set; }
    public List<Operation> Operationen { get; set; } = [];

    /// <summary>Sortierbare, geräteübergreifend eindeutige Kennung: Zeit UTC, Gerät, Akteur, laufende Nummer.</summary>
    public static string NeueId(DateTimeOffset zeit, string geraet, string akteur, int laufnr) =>
        $"{zeit.UtcDateTime:yyyyMMdd'T'HHmmssfff'Z'}_{Sauber(geraet)}_{Sauber(akteur)}_{laufnr:D6}";

    static string Sauber(string s)
    {
        var t = new string(s.Select(c => char.IsAsciiLetterOrDigit(c) || c == '-' ? c : '-').ToArray()).Trim('-');
        return t.Length == 0 ? "x" : t;
    }

    public IEnumerable<string> BetroffeneIds() => Operationen.SelectMany(o =>
        o.Art.StartsWith("element") ? [o.Id] :
        new[] { o.Verbindung?.Von.Element, o.Verbindung?.Nach.Element, o.VerbindungVorher?.Von.Element, o.VerbindungVorher?.Nach.Element }
            .Where(x => x is not null).Select(x => x!)).Distinct();

    /// <summary>Prüft ein aus der Ablage gelesenes Paket, bevor es angewandt wird: null = anwendbar, sonst der Mangel. Gültiges JSON mit
    /// unbrauchbarem Inhalt (leere Datei «{}», Operation einer neueren Programmversion, von Hand verändert) würde sonst mitten im Anwenden
    /// eine Ausnahme auslösen – das Paket wäre halb angewandt und der Abgleich bliebe daran hängen (Gesamtprüfung 13.11.2026).</summary>
    public string? Mangel()
    {
        if (string.IsNullOrWhiteSpace(Id)) return "Paket ohne Id.";
        if (Operationen is null) return "Paket ohne Operationen.";
        for (int i = 0; i < Operationen.Count; i++)
        {
            var o = Operationen[i];
            string Nr() => $"Operation {i + 1}";
            if (o is null) return $"{Nr()} fehlt.";
            if (string.IsNullOrEmpty(o.Id)) return $"{Nr()} ohne Id.";
            switch (o.Art)
            {
                case "element.setzen":
                    if (o.Element is null) return $"{Nr()} (element.setzen) ohne Element.";
                    if (o.Element.Kennzeichen is null) return $"{Nr()} (element.setzen) ohne Kennzeichen.";
                    break;
                case "verbindung.setzen":
                    if (o.Verbindung?.Von?.Element is null || o.Verbindung.Von.Anschluss is null
                        || o.Verbindung.Nach?.Element is null || o.Verbindung.Nach.Anschluss is null)
                        return $"{Nr()} (verbindung.setzen) ohne vollständige Verbindung.";
                    break;
                case "element.loeschen" or "verbindung.loeschen": break;
                default: return $"{Nr()}: unbekannte Art «{o.Art}» (evtl. von einer neueren Programmversion geschrieben).";
            }
        }
        return null;
    }
}

/// <summary>Kopf des Projekts (projekt.json).</summary>
public sealed class ProjektKopf
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int SchemaVersion { get; set; } = 1;
    public DateTimeOffset Angelegt { get; set; }
    public string? Notiz { get; set; }
}

/// <summary>Abbild des Modells nach einer Menge von Paketen (beschleunigt das Öffnen).</summary>
public sealed class StandAbbild
{
    public DateTimeOffset Zeit { get; set; }
    /// <summary>Programmstand, der das Abbild geschrieben hat (Frage 42; Projekt.Programmstand), null = vor dem 14.11.2026.</summary>
    public long? Programmstand { get; set; }
    public List<string> EnthaltenePakete { get; set; } = [];
    public List<Element> Elemente { get; set; } = [];
    public List<Verbindung> Verbindungen { get; set; } = [];
    /// <summary>Gelöschte Elemente: Id → Paket, das gelöscht hat (damit ältere Änderungen sie nicht zurückholen).</summary>
    public Dictionary<string, string> GeloeschteElemente { get; set; } = [];
    public Dictionary<string, string> GeloeschteVerbindungen { get; set; } = [];
}

/// <param name="UebernommenVon">Frage 42: übernommen von «akteur@geraet (seit …)», sonst null.</param>
public sealed record Reservierung(string Element, string Akteur, string Geraet, DateTimeOffset Seit, DateTimeOffset Bis, string? UebernommenVon = null);

public sealed record Konflikt(string Id, string Art, int Erwartet, int Vorhanden, string Text);

/// <summary>Fachlicher Fehler mit Hinweis, wie es richtig geht (für Mensch und KI).</summary>
public sealed class HlksFehler(string meldung, string? vorschlag = null) : Exception(meldung)
{
    public string? Vorschlag { get; } = vorschlag;
}

public static class Json
{
    public static readonly JsonSerializerOptions Optionen = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true,
    };
    public static readonly JsonSerializerOptions Eingerueckt = new(Optionen) { WriteIndented = true };

    /// <summary>Liest eine Zahl aus einem JSON-Knoten, gleich ob sie als int, long, double oder aus Text stammt.</summary>
    public static double? Zahl(JsonNode? n)
    {
        if (n is not JsonValue v) return null;
        if (v.GetValueKind() == JsonValueKind.Number)
            return double.Parse(v.ToJsonString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
        if (v.TryGetValue<string>(out var s) && double.TryParse(s.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d))
            return d;
        return null;
    }

    /// <summary>Liest Ja/Nein: true/false, als Text auch «true»/«false», «ja»/«nein», «1»/«0» (gross/klein egal), als Zahl 1/0 – N38 (Christian
    /// 13.11.2026: «Annehmen und umwandeln»; KI-Aufrufe schicken gern «true» als Text). Sonst null.</summary>
    public static bool? JaNein(JsonNode? n)
    {
        if (n is not JsonValue v) return null;
        if (v.TryGetValue<bool>(out var b)) return b;
        if (v.GetValueKind() == JsonValueKind.Number) return Zahl(v) switch { 1 => true, 0 => false, _ => null };
        if (!v.TryGetValue<string>(out var s)) return null;
        return s.Trim().ToLowerInvariant() switch { "true" or "ja" or "1" => true, "false" or "nein" or "0" => false, _ => null };
    }

    public static string Text(object o, bool eingerueckt = false) => JsonSerializer.Serialize(o, eingerueckt ? Eingerueckt : Optionen);
    public static JsonNode Knoten(object o) => JsonSerializer.SerializeToNode(o, Optionen)!;
}
