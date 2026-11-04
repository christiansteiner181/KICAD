// Katalog der Geberit Vorwandsysteme (kataloge/vorwand_geberit.json): Systeme mit Montageregeln, Montageelemente mit Massen und
// Anschlusshöhen. Jede Zahl trägt im Katalog ihre Fundstelle (quellen) oder steht als Annahme des Hauptlaufs (annahmen).

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public sealed class VorwandKatalog
{
    public sealed record Querschnitt(double Breite, double Hoehe);
    public sealed record Staenderlaenge(string Artikel, double Von, double Bis);
    public sealed record Paneelformat(double Laenge, double Breite);
    public sealed record Anschlusshoehe(string Name, string Medium, string Fluss, double Z, double S);

    public sealed class SystemDef
    {
        public string Id { get; init; } = "";
        public string Titel { get; init; } = "";
        public double Beplankung { get; init; }
        public double FugeBauwerk { get; init; }
        public double TiefeMin { get; init; }
        public double TiefeMax { get; init; }
        public double TiefeMaxGesamt { get; init; }
        public double HoeheMax { get; init; }
        public Paneelformat? Paneel { get; init; }
        public string? PaneelArtikel { get; init; }
        // GIS
        public Querschnitt Profil { get; init; } = new(50, 30);
        public List<double> Horizontalprofile { get; init; } = [];
        public double HorizontalprofilAbstand { get; init; }
        public double VertikalprofilAbstand { get; init; }
        public double MontagewinkelAbstandMassiv { get; init; }
        public double MontagewinkelAbstandTrockenbau { get; init; }
        public double AbstandAngrenzendeWand { get; init; }
        // Duofix
        public Querschnitt Schiene { get; init; } = new(50, 40);
        public Querschnitt Staender { get; init; } = new(40, 40);
        public double StaenderAbstand { get; init; }
        public double SchienenbefestigungAbstand { get; init; }
        public List<Staenderlaenge> StaenderRaumhoch { get; init; } = [];
        public double WandankerVerlaengerungAb { get; init; }
        public string? WandankerVerlaengerung { get; init; }
        public Dictionary<string, string> Quellen { get; init; } = [];
        public Dictionary<string, string> AnnahmenText { get; init; } = [];

        public IEnumerable<string> Annahmen => AnnahmenText.Values;
        public string Quelle(string feld) => Quellen.TryGetValue(feld, out var q) ? $"Geberit Planungshandbuch Sanitär 2025, {q}" : "Annahme";
        public string? Annahme(string feld) => AnnahmenText.TryGetValue(feld, out var a) ? a : null;
    }

    public sealed class ElementTyp
    {
        public string Id { get; init; } = "";
        public string System { get; init; } = "";
        public string Titel { get; init; } = "";
        public string Kurz { get; init; } = "";
        public List<string> Apparate { get; init; } = [];
        public string? Artikel { get; init; }
        public double Breite { get; init; }
        public double Hoehe { get; init; }
        public double? Mindesttiefe { get; init; }
        public List<Anschlusshoehe> Anschluesse { get; init; } = [];
        public Dictionary<string, string> Quellen { get; init; } = [];
        public Dictionary<string, string> Annahmen { get; init; } = [];

        public string Quelle(string feld) => Quellen.TryGetValue(feld, out var q) ? $"Geberit Planungshandbuch Sanitär 2025, {q}" : Annahmen.ContainsKey(feld) ? "Annahme" : "ohne Quelle";
    }

    public Dictionary<string, SystemDef> Systeme { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ElementTyp> Elemente { get; } = [];

    public ElementTyp? Element(string? id) => id is null ? null : Elemente.FirstOrDefault(e => e.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Passender Elementtyp für einen Apparat (Klasse) im System; bei mehreren der erste im Katalog.</summary>
    public ElementTyp? FuerApparat(Katalog k, Element apparat, string system) =>
        Elemente.FirstOrDefault(e => e.System.Equals(system, StringComparison.OrdinalIgnoreCase) && e.Apparate.Any(a => k.IstArt(apparat, a)));

    static readonly Dictionary<string, (DateTime Stand, VorwandKatalog Katalog)> Zwischenspeicher = [];
    static readonly Lock Sperre = new();

    /// <summary>Lädt kataloge/vorwand_geberit.json (zwischengespeichert, neu bei geänderter Datei); leer, wenn die Datei fehlt.</summary>
    public static VorwandKatalog Laden(Katalog k)
    {
        var pfad = Path.Combine(k.Ordner, "vorwand_geberit.json");
        if (!File.Exists(pfad)) return new VorwandKatalog();
        var stand = File.GetLastWriteTimeUtc(pfad);
        lock (Sperre)
        {
            if (Zwischenspeicher.TryGetValue(pfad, out var z) && z.Stand == stand) return z.Katalog;
            var kat = Lesen(JsonNode.Parse(File.ReadAllText(pfad))!.AsObject());
            Zwischenspeicher[pfad] = (stand, kat);
            return kat;
        }
    }

    static double D(JsonNode? n, double vorgabe = 0) => n is JsonValue v && v.TryGetValue<double>(out var d) ? d : vorgabe;
    static string? T(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
    static Dictionary<string, string> Texte(JsonNode? n) =>
        (n as JsonObject ?? []).Where(x => T(x.Value) is not null).ToDictionary(x => x.Key, x => T(x.Value)!);
    static Querschnitt Q(JsonNode? n, Querschnitt vorgabe) => n is JsonObject o ? new(D(o["breite"], vorgabe.Breite), D(o["hoehe"], vorgabe.Hoehe)) : vorgabe;

    public static VorwandKatalog Lesen(JsonObject j)
    {
        var kat = new VorwandKatalog();
        foreach (var (id, n) in j["systeme"] as JsonObject ?? [])
        {
            if (n is not JsonObject s) continue;
            var tiefe = s["tiefe"] as JsonObject ?? [];
            kat.Systeme[id] = new SystemDef
            {
                Id = id, Titel = T(s["titel"]) ?? id, Beplankung = D(s["beplankung"], 18), FugeBauwerk = D(s["fugeBauwerk"], 10),
                TiefeMin = D(tiefe["min"]), TiefeMax = D(tiefe["max"], double.MaxValue), TiefeMaxGesamt = D(tiefe["maxMitVerlaengerung"], D(tiefe["max"], double.MaxValue)),
                HoeheMax = D(s["hoeheMax"], double.MaxValue),
                Paneel = s["paneel"] is JsonObject p ? new Paneelformat(D(p["laenge"]), D(p["breite"])) : null,
                PaneelArtikel = T(s["paneelArtikel"]),
                Profil = Q(s["profil"], new(50, 30)),
                Horizontalprofile = (s["horizontalprofile"] as JsonArray ?? []).Select(x => D(x)).ToList(),
                HorizontalprofilAbstand = D(s["horizontalprofilAbstand"], 650), VertikalprofilAbstand = D(s["vertikalprofilAbstand"], 2500),
                MontagewinkelAbstandMassiv = D(s["montagewinkelAbstandMassiv"], 1000), MontagewinkelAbstandTrockenbau = D(s["montagewinkelAbstandTrockenbau"], 400),
                AbstandAngrenzendeWand = D(s["abstandAngrenzendeWand"], 100),
                Schiene = Q(s["schiene"], new(50, 40)), Staender = Q(s["staender"], new(40, 40)),
                StaenderAbstand = D(s["staenderAbstand"], 550), SchienenbefestigungAbstand = D(s["schienenbefestigungAbstand"], 500),
                StaenderRaumhoch = (s["staenderRaumhoch"] as JsonArray ?? []).OfType<JsonObject>().Select(x => new Staenderlaenge(T(x["artikel"]) ?? "", D(x["von"]), D(x["bis"]))).ToList(),
                WandankerVerlaengerungAb = D(s["wandankerVerlaengerungAb"], double.MaxValue), WandankerVerlaengerung = T(s["wandankerVerlaengerung"]),
                Quellen = Texte(s["quellen"]), AnnahmenText = Texte(s["annahmen"]),
            };
        }
        foreach (var e in (j["elemente"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var an = new List<Anschlusshoehe>();
            foreach (var (name, x) in e["anschluesse"] as JsonObject ?? [])
                if (x is JsonObject a) an.Add(new Anschlusshoehe(name, T(a["medium"]) ?? "*", T(a["fluss"]) ?? "beide", D(a["z"]), D(a["s"])));
            kat.Elemente.Add(new ElementTyp
            {
                Id = T(e["id"]) ?? "", System = T(e["system"]) ?? "", Titel = T(e["titel"]) ?? "", Kurz = T(e["kurz"]) ?? "",
                Apparate = (e["apparate"] as JsonArray ?? []).Select(T).OfType<string>().ToList(), Artikel = T(e["artikel"]),
                Breite = D(e["breite"], 500), Hoehe = D(e["hoehe"], 1120), Mindesttiefe = e["mindesttiefe"] is JsonValue ? D(e["mindesttiefe"]) : null,
                Anschluesse = an, Quellen = Texte(e["quellen"]), Annahmen = Texte(e["annahmen"]),
            });
        }
        return kat;
    }
}
