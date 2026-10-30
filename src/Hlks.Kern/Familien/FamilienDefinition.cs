// Familien (Modul 2, B1): C#-Abbild des Familienformats (schema/hlks-familie.schema.json, entwurf/modul-2.md) mit Lesen und
// Prüfen der Definition. Lesen wirft nie: jede Abweichung wird ein FamFehler mit Stelle (Pfad in der Definition, bei Formeln
// zusätzlich die Zeichenstelle) und Vorschlag. Eine Definition mit Fehlern wird nicht gespeichert (familie_entwurf lehnt ab);
// Fehler, die nur bei bestimmten Werten auftreten (fehlender Typwert, Division durch 0), meldet erst die Auswertung je Typ.
//
// Regeln (Spezifikation, wie Revit):
//   - Ein Parameter mit Formel ist nicht pro Typ oder Exemplar einstellbar.
//   - Formeln von Typparametern dürfen nur Typparameter verwenden.
//   - Namen eindeutig (Parameter, Typen, Körper-Ids, Anschlüsse), Art und Datentyp gültig, kategorie familienfähig, Medium aus
//     dem Katalog oder «*», Richtung eines Anschlusses nicht null, keine zirkulären Formeln, keine unbekannten Namen.
// ANNAHMEN (Prüfliste): Parameternamen, die sich nur in Gross/Kleinschreibung unterscheiden, sind ein Fehler (Verwechslung);
//   Anschlussnamen sind ohne Rücksicht auf Gross/Kleinschreibung eindeutig (wie Element.AnschlussNamens); ein Anschluss-
//   medium darf auch der Name eines Parameters mit Datentyp medium sein; eine Familie braucht mindestens einen Typ (Revit);
//   Werte in typen.werte dürfen auch Exemplarparameter betreffen (typbezogener Vorgabewert, Reihenfolge der Spezifikation:
//   Exemplarwert › Typwert › Formel › Vorgabewert); unbekannte Felder sind Fehler (additionalProperties des Schemas).

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Hlks.Kern;

/// <summary>Ein Fehler in einer Familie: Stelle (z. B. «parameter «oberkante».formel»), Meldung, Weg zur Lösung.</summary>
public sealed record FamFehler(string Stelle, string Meldung, string? Vorschlag = null)
{
    public override string ToString() => $"{Stelle}: {Meldung}";
    public JsonObject AlsJson() => new() { ["stelle"] = Stelle, ["meldung"] = Meldung, ["vorschlag"] = Vorschlag };
}

/// <summary>Zahl oder Formel eines Felds (Körper, Anschluss, Sinnbild).</summary>
public sealed class FamZahl
{
    public double? Konstante { get; init; }
    public Ausdruck? Formel { get; init; }
    /// <summary>Formeltext wie in der Definition.</summary>
    public string? Text { get; init; }
    /// <summary>Stelle in der Definition, z. B. «koerper «platten».bis[3]».</summary>
    public string Ort { get; init; } = "";

    public override string ToString() => Text ?? Konstante?.ToString(CultureInfo.InvariantCulture) ?? "";
}

/// <summary>Punkt [u, v] bzw. [u, v, wölbung] einer Kontur oder eines Sinnbilds.</summary>
public sealed record FamPunkt2(FamZahl U, FamZahl V, FamZahl? Woelbung);

public sealed class FamParameter
{
    public string Name { get; init; } = "";
    /// <summary>typ | exemplar</summary>
    public string Art { get; init; } = "typ";
    /// <summary>laenge | winkel | zahl | text | ja_nein | medium | dn</summary>
    public string Datentyp { get; init; } = "zahl";
    public string? Einheit { get; init; }
    public string? Gruppe { get; init; }
    public JsonNode? Wert { get; init; }
    public string? FormelText { get; init; }
    public Ausdruck? Formel { get; init; }
    public string? Ziel { get; init; }
    public string? Beschreibung { get; init; }

    public bool IstText => Datentyp is "text" or "medium";
    public bool HatFormel => FormelText is not null;

    /// <summary>JSON-Wert (Vorgabe, Typwert, Exemplarwert) in einen Formelwert nach Datentyp; null = kein Wert.</summary>
    public FWert? Umwandeln(JsonNode? n, out string? fehler)
    {
        fehler = null;
        if (n is null) return null;
        if (n is not JsonValue v) { fehler = $"Wert von «{Name}» muss {Erwartet} sein, nicht {Kurz(n)}"; return null; }
        switch (Datentyp)
        {
            case "text" or "medium":
                if (v.TryGetValue<string>(out var s)) return FWert.T(s);
                fehler = $"Wert von «{Name}» ({Datentyp}) muss ein Text sein, nicht {Kurz(n)}";
                return null;
            case "ja_nein":
                if (v.TryGetValue<bool>(out var b)) return FWert.Wahr(b);
                if (v.GetValueKind() == JsonValueKind.Number) return FWert.Wahr(Json.Zahl(v) != 0);
                fehler = $"Wert von «{Name}» (ja_nein) muss true oder false sein, nicht {Kurz(n)}";
                return null;
            default:
                if (v.GetValueKind() is JsonValueKind.Number or JsonValueKind.String && Json.Zahl(v) is double d && double.IsFinite(d)) return FWert.Z(d);
                fehler = $"Wert von «{Name}» ({Datentyp}{(Einheit is null ? "" : ", " + Einheit)}) muss eine Zahl sein, nicht {Kurz(n)}" +
                         (v.TryGetValue<string>(out _) ? " – für eine Formel das Feld «formel» benutzen" : "");
                return null;
        }
    }

    string Erwartet => IstText ? "ein Text" : Datentyp == "ja_nein" ? "true oder false" : "eine Zahl";

    static string Kurz(JsonNode n)
    {
        var t = n.ToJsonString();
        return t.Length > 40 ? t[..40] + "…" : t;
    }

    /// <summary>Formelwert in den Datentyp des Parameters (Ergebnis einer Formel); Fehler bei Text ↔ Zahl.</summary>
    public FWert Passend(FWert w, int stelle)
    {
        if (IstText && !w.IstText) throw new FormelFehler($"«{Name}» ist {Datentyp}, die Formel ergibt aber die Zahl {w}", stelle, "Formel muss einen Text ergeben (z. B. if(…, \"A\", \"B\")).");
        if (!IstText && w.IstText) throw new FormelFehler($"«{Name}» ist {Datentyp}, die Formel ergibt aber den Text «{w.Text}»", stelle, "Formel muss eine Zahl ergeben.");
        return Datentyp == "ja_nein" ? FWert.Wahr(w.Zahl != 0) : w;
    }
}

public sealed class FamTyp
{
    public string Name { get; init; } = "";
    public Dictionary<string, JsonNode?> Werte { get; } = new(StringComparer.Ordinal);
    /// <summary>Typparameter, die es für diesen Typ nicht gibt (typen[].entfaellt, mit Beleg).</summary>
    public HashSet<string> Entfaellt { get; } = new(StringComparer.Ordinal);
}

public sealed class FamKoerper
{
    public string Id { get; init; } = "";
    public string? Name { get; init; }
    /// <summary>quader | zylinder | kegel | kugel | extrusion | rotation | sweep</summary>
    public string Art { get; init; } = "";
    /// <summary>geraet | apparat | armatur | rohr | sonst (Vorgabe geraet, Annahme)</summary>
    public string Material { get; init; } = "geraet";
    public IReadOnlySet<string> Detailgrad { get; init; } = FamilieDef.AlleDetailgrade;
    public FamZahl? Sichtbar { get; init; }
    // quader von/bis, zylinder/kegel von/nach, kugel mitte (Punkte)
    public FamZahl[]? Von { get; init; }
    public FamZahl[]? Bis { get; init; }
    public FamZahl[]? Nach { get; init; }
    public FamZahl[]? Mitte { get; init; }
    public FamZahl? R { get; init; }
    public FamZahl? R1 { get; init; }
    public FamZahl? R2 { get; init; }
    // extrusion, rotation
    public string? Ebene { get; init; }
    public List<FamPunkt2>? Kontur { get; init; }
    public List<List<FamPunkt2>>? Loecher { get; init; }
    public FamZahl? ExVon { get; init; }
    public FamZahl? ExBis { get; init; }
    public FamZahl[]? AchsePunkt { get; init; }
    public string? AchseRichtung { get; init; }
    public FamZahl? Winkel { get; init; }
    // sweep
    public FamZahl? ProfilKreis { get; init; }
    public FamZahl[]? ProfilRechteck { get; init; }
    public List<FamZahl[]>? Pfad { get; init; }
    public FamZahl? Biegeradius { get; init; }
}

public sealed class FamAnschluss
{
    public string Name { get; init; } = "";
    public string Medium { get; init; } = "*";
    public string Fluss { get; init; } = "beide";
    public string? Art { get; init; }
    public string? Form { get; init; }
    public FamZahl? Dn { get; init; }
    /// <summary>Kanal rund: Durchmesser in mm.</summary>
    public FamZahl? D { get; init; }
    public FamZahl? B { get; init; }
    public FamZahl? H { get; init; }
    public string? Gewinde { get; init; }
    public FamZahl[] Lage { get; init; } = [];
    public FamZahl[] Richtung { get; init; } = [];
    public FamZahl? Sichtbar { get; init; }
    public string? Beschreibung { get; init; }
}

/// <summary>Ein Teil des Sinnbilds (oder Schemasymbols): linie, polylinie, polygon, kreis, bogen, text.</summary>
public sealed class FamZeichnung
{
    public string Art { get; init; } = "linie";
    public List<FamPunkt2> Punkte { get; init; } = [];
    public FamZahl? Radius { get; init; }
    public FamZahl? Von { get; init; }
    public FamZahl? Bis { get; init; }
    public string? Text { get; init; }
    public double? Groesse { get; init; }
    public IReadOnlySet<string> Detailgrad { get; init; } = FamilieDef.AlleDetailgrade;
    public FamZahl? Sichtbar { get; init; }
    /// <summary>Stift (duenn, mittel, dick) oder null = keine Kontur; Vorgabe duenn.</summary>
    public string? Stift { get; init; } = Stifte.Duenn;
    public string Linie { get; init; } = "voll";
    public string? Fuellung { get; init; }
}

/// <summary>Formteil einer Leitungsserie je Art (Modul 3): Formteil-Familie über ihre Definitions-id und optional eine Typregel
/// (Formel, die den Typnamen als Text ergibt; Namen = Grössenparameter der Serie, für T-Stück-Abzweig bzw. Reduktion klein mit
/// der Endung «_2»). Ohne Typregel gilt der Typ, dessen ausgewertete Anschlussgrössen passen (Leitungsserie.Formteil).</summary>
public sealed class FamFormteilWahl
{
    /// <summary>bogen | tstueck | reduktion | reinigungsoeffnung</summary>
    public string Art { get; init; } = "";
    /// <summary>Definitions-id der Formteil-Familie (z. B. fam-test-bogen).</summary>
    public string Familie { get; init; } = "";
    public string? TypText { get; init; }
    public Ausdruck? TypFormel { get; init; }
    /// <summary>Typ der Formteil-Familie für freie Grössen (Nachtrag freie Grösse, entwurf/freie-groesse-kanal.md): passt kein Typ,
    /// wird dieser mit Exemplarwerten für die Anschlussgrössen ausgewertet; null = der erste Typ der Familie.</summary>
    public string? Vorlage { get; init; }
}

/// <summary>Grenzen einer freien Grösse je Grössenschlüssel (mm; null = keine Grenze auf dieser Seite).</summary>
public sealed record FamGrenze(double? Min, double? Max);

/// <summary>Freie Grösse einer Kanalserie (Block leitung.frei, Nachtrag Modul 3, entwurf/freie-groesse-kanal.md; Entscheid Christian
/// 20.10.2026: «Rechteckkanäle mit freier Grösse b × h (Normraster nur als Vorschlag)»). Die Typen bleiben das Vorschlagsraster;
/// eine Grösse, die keinem Typ entspricht, wird aus dem Typ <see cref="Vorlage"/> mit ersetzten Grössenparametern ausgewertet.</summary>
public sealed class FamFrei
{
    /// <summary>Typname, dessen Werte und Formeln für jede freie Grösse gelten.</summary>
    public string Vorlage { get; init; } = "";
    /// <summary>Grenzen je Grössenschlüssel (b, h bzw. d) – nur aus der Quelle; fehlt ein Schlüssel = jede Grösse &gt; 0.</summary>
    public IReadOnlyDictionary<string, FamGrenze> Grenzen { get; init; } = new Dictionary<string, FamGrenze>();
    /// <summary>Raster der Eingabe in ganzen mm (Vorgabe 1): eine Grösse muss ein Vielfaches davon sein.</summary>
    public double Schritt { get; init; } = 1;
}

/// <summary>Block «leitung» einer Leitungsserie (Familie der Kategorie Rohr bzw. Kanal, Modul 3, entwurf/modul-3.md
/// Grundsatzentscheid 2). Die Typen sind die Grössen.</summary>
public sealed class FamLeitung
{
    /// <summary>Formteilarten in fester Reihenfolge.</summary>
    public static readonly string[] Formteilarten = ["bogen", "tstueck", "reduktion", "reinigungsoeffnung"];
    /// <summary>KICAD-Klasse der Formteil-Familie je Art.</summary>
    public static string KlasseVon(string art) => art switch
    {
        "bogen" => "Bogen", "tstueck" => "TStueck", "reduktion" => "Reduktion", "reinigungsoeffnung" => "Reinigungsoeffnung",
        _ => throw new HlksFehler($"Formteilart «{art}» gibt es nicht.", "Formteilarten: " + string.Join(", ", Formteilarten)),
    };

    /// <summary>rund | rechteck</summary>
    public string Querschnitt { get; init; } = "rund";
    /// <summary>Grössenschlüssel (dn | d | b | h) → Parametername der Serie.</summary>
    public IReadOnlyDictionary<string, string> Groesse { get; init; } = new Dictionary<string, string>();
    public FamZahl? Aussen { get; init; }
    public FamZahl? Innen { get; init; }
    public FamZahl? Wand { get; init; }
    public FamZahl? Biegeradius { get; init; }
    /// <summary>Werkstoff-Kürzel für die Beschriftung der Rohre dieser Serie, z. B. «CNS» (Entscheid Christian 19.10.2026, B9):
    /// Rohrgroessen.Von ergibt damit bei Abwasser «{kuerzel} {da}» («PE-S 110»), bei den übrigen Rohren «{kuerzel} NW|DN {dn}» («CNS NW 20»; B9b, Entscheide Christian 19.10.2026). Null = nicht angegeben (dann nur die Grösse).</summary>
    public string? Kuerzel { get; init; }
    /// <summary>Formteil je Art (nur die angegebenen).</summary>
    public IReadOnlyDictionary<string, FamFormteilWahl> Formteile { get; init; } = new Dictionary<string, FamFormteilWahl>();
    /// <summary>regeln.winkel = «frei».</summary>
    public bool WinkelFrei { get; init; }
    /// <summary>Erlaubte Bogenwinkel in Grad; null = keine Liste (frei oder nicht angegeben).</summary>
    public IReadOnlyList<double>? Winkel { get; init; }
    /// <summary>regeln.abzweig (tstueck) oder null.</summary>
    public string? Abzweig { get; init; }
    /// <summary>regeln.abzweigwinkel (Modul 3, B7): erlaubte Abzweigwinkel der T-Stücke in Grad (0 &lt; w ≤ 90, Winkel zwischen
    /// Durchgang und Abzweig); null = nicht angegeben (gilt als [90], rechtwinklig).</summary>
    public IReadOnlyList<double>? Abzweigwinkel { get; init; }
    /// <summary>leitung.frei (nur Kanalserien): freie Grösse mit Vorlage-Typ, Grenzen und Schritt; null = nur die Typen.</summary>
    public FamFrei? Frei { get; init; }

    /// <summary>Erlaubte Werte von leitung.einsatz (weitere später).</summary>
    public static readonly string[] Einsaetze = ["abwasser"];
    /// <summary>leitung.einsatz (Nachtrag Abwasser-Serie, entwurf/abwasser-serie.md; Entscheid Christian 20.10.2026: «Geberit PE ist immer
    /// Abwasser»): «abwasser» = Abwasser-Serie, nur Kategorie Rohr – Grösse und Beschriftung nach Aussendurchmesser, unabhängig vom Medium;
    /// null = nicht angegeben.</summary>
    public string? Einsatz { get; init; }
    /// <summary>Abwasser-Serie (einsatz abwasser).</summary>
    public bool Abwasser => Einsatz == "abwasser";
    /// <summary>Abwasser-Serie mit Grösse d = Aussendurchmesser (groesse { d }); die DN des Herstellers steht höchstens als Typparameter
    /// (ziel dn), nie abgeleitet.</summary>
    public bool NachAussendurchmesser => Abwasser && Groesse.ContainsKey("d");
}

/// <summary>Massbezug (für B4: Abweichungsliste je Typ).</summary>
public sealed class FamMass
{
    public string Name { get; init; } = "";
    public string? Ansicht { get; init; }
    public FamZahl[]? Von { get; init; }
    public FamZahl[]? Bis { get; init; }
    public string? Richtung { get; init; }
    public string? Parameter { get; init; }
}

/// <summary>Eine gelesene und geprüfte Familiendefinition. Unveränderlich; Auswertungen je Typ und Exemplarwerten werden
/// hier zwischengespeichert (<see cref="FamilienAuswertung"/>).</summary>
public sealed class FamilieDef
{
    public static readonly IReadOnlySet<string> AlleDetailgrade = new HashSet<string> { "grob", "mittel", "fein" };
    public static readonly string[] Detailgrade = ["grob", "mittel", "fein"];
    public static readonly string[] Arten = ["typ", "exemplar"];
    public static readonly string[] Datentypen = ["laenge", "winkel", "zahl", "text", "ja_nein", "medium", "dn"];
    public static readonly string[] Gruppen = ["abmessung", "anschluss", "kennwert", "sonstiges"];
    public static readonly string[] Koerperarten = ["quader", "zylinder", "kegel", "kugel", "extrusion", "rotation", "sweep"];
    public static readonly string[] Materialien = ["geraet", "apparat", "armatur", "rohr", "sonst"];
    public static readonly string[] Ebenen = ["xy", "xz", "yz"];
    public static readonly string[] Fluesse = ["rein", "raus", "beide"];
    public static readonly string[] AnschlussArten = ["rohr", "kanal", "elektro"];
    public static readonly string[] Formen = ["rund", "rechteck"];
    public static readonly string[] Zeichnungsarten = ["linie", "polylinie", "polygon", "kreis", "bogen", "text"];
    public static readonly string[] Ansichten = ["vorne", "hinten", "links", "rechts", "oben", "unten"];
    static readonly string[] KopfFelder = ["format", "id", "name", "kategorie", "hersteller", "produkt", "beschreibung", "version", "status",
        "parameter", "typen", "koerper", "anschluesse", "leitung", "masse", "sinnbild", "schemasymbol", "unterlagen", "konflikte", "abdeckung", "bericht"];
    static readonly Regex NamensMuster = new("^[A-Za-zÄÖÜäöü_][A-Za-zÄÖÜäöü0-9_]*$");
    static readonly Regex BefundMuster = new("^[0-9a-f]{12}#[0-9]+$");

    public JsonObject Quelle { get; }
    /// <summary>Katalog, gegen den gelesen wurde (Medien, Klassen); null in reinen Formeltests.</summary>
    public Katalog? Katalog { get; private set; }
    public string Name { get; private set; } = "";
    public string? Id { get; private set; }
    public string Kategorie { get; private set; } = "";
    public string? Status { get; private set; }
    public int? Version { get; private set; }
    public List<FamParameter> Parameter { get; } = [];
    public Dictionary<string, FamParameter> NachName { get; } = new(StringComparer.Ordinal);
    public List<FamTyp> Typen { get; } = [];
    public List<FamKoerper> Koerper { get; } = [];
    public List<FamAnschluss> Anschluesse { get; } = [];
    public List<FamMass> Masse { get; } = [];
    public List<FamZeichnung>? Sinnbild { get; private set; }
    public List<FamZeichnung>? Schemasymbol { get; private set; }
    /// <summary>Block «leitung» (Modul 3): gesetzt bei Leitungsserien (Kategorie Rohr bzw. Kanal), sonst null.</summary>
    public FamLeitung? Leitung { get; private set; }
    /// <summary>Die Familie ist eine Leitungsserie (Kategorie IstArt Leitung bzw. ohne Katalog: Block «leitung» vorhanden).</summary>
    public bool IstLeitungsserie => Leitung is not null;
    /// <summary>Fehler der Definition (Aufbau, Namen, Formeln, Regeln). Leer = gültig.</summary>
    public List<FamFehler> Fehler { get; } = [];
    /// <summary>Hinweise zur Definition (Lücken, die einen Entwurf nicht verhindern, z. B. fehlende Anschlussgrösse).</summary>
    public List<FamFehler> Hinweise { get; } = [];
    public bool Gueltig => Fehler.Count == 0;

    /// <summary>Zwischenspeicher der Auswertungen (Typ + Exemplarwerte), siehe FamilienAuswertung.</summary>
    internal System.Collections.Concurrent.ConcurrentDictionary<string, FamAufloesung> Aufloesungen { get; } = new();

    readonly List<FamZahl> alleZahlen = [];

    FamilieDef(JsonObject quelle) { Quelle = quelle; }

    public FamTyp? Typ(string? name) => name is null ? null : Typen.FirstOrDefault(t => t.Name == name);

    /// <summary>Liest und prüft eine Definition. Wirft nie; Fehler stehen in <see cref="Fehler"/>.</summary>
    public static FamilieDef Lesen(JsonObject definition, Katalog? katalog)
    {
        var f = new FamilieDef(definition) { Katalog = katalog };
        try { f.LesenIntern(katalog); }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException or JsonException or KeyNotFoundException or ArgumentException)
        {
            f.Fehler.Add(new("definition", "Definition nicht lesbar: " + ex.Message, "Aufbau nach schema/hlks-familie.schema.json prüfen."));
        }
        return f;
    }

    void Fehl(string stelle, string meldung, string? vorschlag = null) => Fehler.Add(new(stelle, meldung, vorschlag));

    static string? Text(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    void LesenIntern(Katalog? kat)
    {
        var d = Quelle;
        foreach (var (feld, _) in d)
            if (!KopfFelder.Contains(feld))
                Fehl(feld, $"Unbekanntes Feld «{feld}»", "Erlaubt: " + string.Join(", ", KopfFelder));
        if (Json.Zahl(d["format"]) != 1) Fehl("format", "format muss 1 sein", "\"format\": 1 angeben.");
        Name = Text(d["name"])?.Trim() ?? "";
        if (Name.Length == 0) Fehl("name", "Name der Familie fehlt", "\"name\" angeben, z. B. Hersteller und Produkt.");
        Id = Text(d["id"]);
        Status = Text(d["status"]);
        if (Status is not null and not ("entwurf" or "freigegeben")) Fehl("status", $"status «{Status}» gibt es nicht", "entwurf oder freigegeben");
        Version = Json.Zahl(d["version"]) is double ver ? (int)ver : null;
        Kategorie = Text(d["kategorie"])?.Trim() ?? "";
        KlassenDef? klasse = null;
        if (Kategorie.Length == 0) Fehl("kategorie", "kategorie fehlt", "KICAD-Klasse angeben: " + Familienfaehig(kat));
        else if (kat is not null)
        {
            if (!kat.Klassen.TryGetValue(Kategorie, out klasse))
                Fehl("kategorie", $"Klasse «{Kategorie}» gibt es nicht", "Familienfähige Klassen: " + Familienfaehig(kat));
            else if (!klasse.Familie)
            {
                Fehl("kategorie", $"Klasse «{klasse.Name}» ist nicht familienfähig", "Familienfähige Klassen: " + Familienfaehig(kat));
                klasse = null;
            }
            else Kategorie = klasse.Name;
        }

        ParameterLesen(d["parameter"], kat, klasse);
        TypenLesen(d["typen"]);
        KoerperLesen(d["koerper"]);
        AnschluesseLesen(d["anschluesse"], kat);
        LeitungLesen(d, kat, klasse);
        MasseLesen(d["masse"]);
        Sinnbild = ZeichnungLesen(d["sinnbild"], "sinnbild");
        Schemasymbol = ZeichnungLesen(d["schemasymbol"], "schemasymbol");
        QuellenPruefen();
        FormelnPruefen();
    }

    // ------------------------------------------------------------------ Leitungsserie (Modul 3)

    /// <summary>Block «leitung»: nur bei Kategorie IstArt Leitung erlaubt und dort Pflicht (entwurf/modul-3.md, Grundsatzentscheid 2;
    /// Schema «leitung»). Leitungsserien haben weder Körper noch Anschlüsse – Geometrie und Anschlüsse anfang/ende ergeben sich
    /// aus dem Verlauf. Ohne Katalog (reine Formeltests) wird der Block gelesen, wenn er da ist.</summary>
    void LeitungLesen(JsonObject d, Katalog? kat, KlassenDef? klasse)
    {
        var n = d["leitung"];
        bool? istLeitung = klasse?.IstArt("Leitung");
        if (kat is not null && klasse is null) { if (n is null) return; istLeitung = null; }   // Kategorie schon als Fehler gemeldet
        if (n is null)
        {
            if (istLeitung == true)
                Fehl("leitung", $"Eine Leitungsserie (Kategorie {Kategorie}) braucht den Block «leitung»",
                    "\"leitung\": { \"querschnitt\": \"rund\", \"groesse\": { \"dn\": \"dn\" }, \"aussen\": \"da\", \"formteile\": { … }, \"regeln\": { … } } angeben (schema/hlks-familie.schema.json).");
            return;
        }
        if (istLeitung == false)
        {
            Fehl("leitung", $"Den Block «leitung» gibt es nur für Leitungsserien (Kategorie Rohr bzw. Kanal), nicht für {Kategorie}",
                "Block «leitung» entfernen oder als kategorie Rohr bzw. Kanal anlegen.");
            return;
        }
        if (n is not JsonObject o) { Fehl("leitung", "leitung muss ein Objekt sein", "{ querschnitt, groesse, aussen, innen?, wand?, kuerzel?, einsatz?, formteile?, regeln?, frei?, quellen? }"); return; }
        string[] felder = ["querschnitt", "groesse", "aussen", "innen", "wand", "kuerzel", "einsatz", "formteile", "regeln", "frei", "quellen"];
        foreach (var (feld, _) in o)
            if (!felder.Contains(feld)) Fehl("leitung", $"Unbekanntes Feld «{feld}»", "Erlaubt: " + string.Join(", ", felder));

        // Querschnitt und Grösse passend zur Kategorie: Rohr dn (Abwasser-Serie auch d = Aussendurchmesser), Kanal rund d, Kanal rechteckig b und h
        bool istRohr = klasse?.IstArt("Rohr") == true, istKanal = klasse?.IstArt("Kanal") == true;
        var einsatz = o["einsatz"] is null ? null : EinsatzLesen(o["einsatz"]!, klasse, istRohr);
        bool abwasser = einsatz == "abwasser";
        var querschnitt = Text(o["querschnitt"]);
        if (querschnitt is not ("rund" or "rechteck"))
        {
            Fehl("leitung.querschnitt", querschnitt is null ? "querschnitt fehlt" : $"querschnitt «{querschnitt}» gibt es nicht", "rund oder rechteck");
            querschnitt = null;
        }
        else if (istRohr && querschnitt == "rechteck")
            Fehl("leitung.querschnitt", "Rohre sind rund (Grösse dn)", "querschnitt «rund» angeben oder die Serie als Kanal anlegen.");
        string[]? schluessel = querschnitt switch
        {
            "rechteck" => ["b", "h"],
            // Abwasser-Serie (Nachtrag, entwurf/abwasser-serie.md): Grösse d = Aussendurchmesser (empfohlen) oder dn wie bisher
            "rund" when istRohr => abwasser && o["groesse"] is JsonObject ga && ga.ContainsKey("d") ? ["d"] : ["dn"],
            "rund" when istKanal => ["d"],
            "rund" => o["groesse"] is JsonObject g0 && g0.ContainsKey("d") ? ["d"] : ["dn"],   // ohne Klasse: dn oder d
            _ => null,
        };
        var groesse = new Dictionary<string, string>(StringComparer.Ordinal);
        if (o["groesse"] is not JsonObject go)
            Fehl("leitung.groesse", "groesse fehlt oder ist kein Objekt",
                schluessel is null ? "{ dn: \"dn\" } (Rohr), { d: \"d\" } (Abwasser-Serie bzw. Kanal rund) bzw. { b: \"b\", h: \"h\" } (Kanal rechteckig)"
                : $"{{ {string.Join(", ", schluessel.Select(s => $"\"{s}\": \"<Parameter>\""))} }} angeben (Parametername der Grösse).");
        else if (schluessel is not null)
        {
            string Erwartet() => istRohr
                ? (abwasser ? "Abwasser-Serie (einsatz abwasser): d (Aussendurchmesser, empfohlen) oder dn, nicht beides – eine DN des Herstellers als eigener Typparameter mit ziel dn"
                            : "Rohr: dn (die Grösse d = Aussendurchmesser gibt es nur bei einer Abwasser-Serie, leitung.einsatz «abwasser»)")
                : istKanal ? (querschnitt == "rund" ? "Kanal rund: d" : "Kanal rechteckig: b und h") : querschnitt == "rund" ? "rund: dn bzw. d" : "rechteckig: b und h";
            foreach (var (k, _) in go)
                if (!schluessel.Contains(k)) Fehl("leitung.groesse", $"Grössenschlüssel «{k}» passt nicht zu {Kategorie} {querschnitt}", "Erlaubt – " + Erwartet());
            foreach (var k in schluessel)
            {
                var ort = $"leitung.groesse.{k}";
                var pn = Text(go[k])?.Trim();
                if (string.IsNullOrEmpty(pn)) { Fehl(ort, $"Grösse «{k}» fehlt", $"Parametername angeben – {Erwartet()}."); continue; }
                if (!NachName.TryGetValue(pn, out var p)) { Fehl(ort, $"Parameter «{pn}» gibt es nicht", Hlks.Kern.Formel.NamensVorschlag(pn, NachName.Keys)); continue; }
                if (p.Art != "typ") Fehl(ort, $"«{pn}» ist ein Exemplarparameter – die Grössen einer Leitungsserie sind ihre Typen", $"«{pn}» als Typparameter (art typ) führen.");
                if (p.IstText || p.Datentyp == "ja_nein") Fehl(ort, $"«{pn}» ist {p.Datentyp} – eine Grösse ist eine Zahl", "Datentyp dn (Rohr) bzw. laenge (Kanal, mm) wählen.");
                groesse[k] = pn;
            }
        }
        // Abwasser-Serie nach Aussendurchmesser: eine DN steht nur als Herstellerangabe (Typparameter mit Wert je Typ, ziel dn) –
        // nie aus dem Aussendurchmesser abgeleitet (Regel Christian 20.10.2026)
        if (abwasser && groesse.TryGetValue("d", out var dPar))
            foreach (var p in Parameter.Where(p => string.Equals(p.Ziel, "dn", StringComparison.OrdinalIgnoreCase)))
            {
                var ort = $"parameter «{p.Name}».ziel";
                if (p.Name == dPar)
                    Fehl(ort, $"Der Aussendurchmesser «{p.Name}» (Grösse d der Abwasser-Serie) ist keine DN – die DN wird nie aus dem Aussendurchmesser abgeleitet",
                        "ziel dn entfernen; eine DN des Herstellers als eigenen Typparameter (z. B. dn_hersteller, ziel dn) mit den Werten der Unterlage führen – oder ganz weglassen (keine DN nötig).");
                else if (p.HatFormel)
                    Fehl(ort, $"«{p.Name}» (ziel dn) hat eine Formel – die DN einer Abwasser-Serie nach Aussendurchmesser ist eine Herstellerangabe, nie gerechnet",
                        $"Die Formel von «{p.Name}» entfernen und die DN je Typ als Wert aus der Unterlage angeben (typen.werte.{p.Name}) – oder den Parameter weglassen (keine DN nötig).");
                else if (p.Art != "typ")
                    Fehl(ort, $"«{p.Name}» (ziel dn) ist ein Exemplarparameter – die DN des Herstellers gehört zum Typ (zur Grösse)", $"«{p.Name}» als Typparameter (art typ) führen.");
            }

        var aussen = o["aussen"] is null ? null : Zahl(o["aussen"], "leitung.aussen");
        if (o["aussen"] is null) Fehl("leitung.aussen", "aussen fehlt", "Aussendurchmesser als Formel aus den Parametern angeben, z. B. \"da\" (bei rechteckigen Kanälen nicht genutzt, trotzdem Pflicht).");
        var innen = o["innen"] is null ? null : Zahl(o["innen"], "leitung.innen");
        var wand = o["wand"] is null ? null : Zahl(o["wand"], "leitung.wand");
        var kuerzel = o["kuerzel"] is null ? null : KuerzelLesen(o["kuerzel"]!, istKanal);

        // Formteile je Art
        var formteile = new Dictionary<string, FamFormteilWahl>(StringComparer.Ordinal);
        if (o["formteile"] is JsonObject fo)
        {
            foreach (var (art, wahl) in fo)
            {
                var ort = $"leitung.formteile.{art}";
                if (!FamLeitung.Formteilarten.Contains(art)) { Fehl(ort, $"Formteilart «{art}» gibt es nicht", "Erlaubt: " + string.Join(", ", FamLeitung.Formteilarten)); continue; }
                if (wahl is not JsonObject wo) { Fehl(ort, "muss ein Objekt { familie, typ?, vorlage? } sein", "{ \"familie\": \"<id der Formteil-Familie>\" }"); continue; }
                foreach (var (feld, _) in wo)
                    if (feld is not ("familie" or "typ" or "vorlage")) Fehl(ort, $"Unbekanntes Feld «{feld}»", "Erlaubt: familie, typ, vorlage");
                string? vorlage = null;
                if (wo.ContainsKey("vorlage"))
                {
                    vorlage = Text(wo["vorlage"])?.Trim();
                    if (string.IsNullOrEmpty(vorlage)) { Fehl(ort + ".vorlage", "vorlage muss ein Typname (Text) sein", "Typname der Formteil-Familie, der für freie Grössen mit Exemplarwerten ausgewertet wird; oder vorlage weglassen (dann der erste Typ)."); vorlage = null; }
                    else if (o["frei"] is null) Hinweise.Add(new(ort + ".vorlage", "vorlage wirkt nur bei einer Serie mit freier Grösse (leitung.frei)", "leitung.frei ergänzen oder vorlage weglassen."));
                }
                var fam = Text(wo["familie"])?.Trim();
                if (string.IsNullOrEmpty(fam)) Fehl(ort + ".familie", "familie fehlt", $"id der Formteil-Familie (Kategorie {FamLeitung.KlasseVon(art)}) angeben, z. B. \"fam-…\".");
                string? typText = null; Ausdruck? typFormel = null;
                if (wo.ContainsKey("typ"))
                {
                    typText = Text(wo["typ"]);
                    if (string.IsNullOrWhiteSpace(typText)) Fehl(ort + ".typ", "typ muss eine Formel (Text) sein", "Formel, die den Typnamen ergibt, z. B. if(dn = 15, \"DN 15\", \"DN 20\"); oder typ weglassen (Typ nach Anschlussgrössen).");
                    else
                    {
                        try { typFormel = Hlks.Kern.Formel.Parsen(typText); }
                        catch (FormelFehler ff) { Fehl(ort + ".typ", $"Formel «{typText}»: {ff.MitStelle}", ff.Vorschlag); }
                        if (typFormel is not null)
                        {
                            var erlaubt = TypregelNamen(groesse.Values, art);
                            foreach (var (name, stelle) in typFormel.Namen())
                                if (!erlaubt.Contains(name))
                                    Fehl(ort + ".typ", $"Formel «{typText}»: Stelle {stelle}: unbekannter Name «{name}»",
                                        "In der Typregel gibt es nur die Grössen der Serie: " + string.Join(", ", erlaubt) + (art is "tstueck" or "reduktion" ? " (Endung _2 = " + (art == "tstueck" ? "Abzweig" : "kleine Grösse") + ")" : "") + ".");
                        }
                    }
                }
                formteile[art] = new FamFormteilWahl { Art = art, Familie = fam ?? "", TypText = typText, TypFormel = typFormel, Vorlage = vorlage };
            }
        }
        else if (o["formteile"] is not null) Fehl("leitung.formteile", "formteile muss ein Objekt sein", "{ \"bogen\": { \"familie\": \"…\" }, \"tstueck\": …, \"reduktion\": … }");

        // Regeln
        bool winkelFrei = false; List<double>? winkel = null; string? abzweig = null; FamZahl? biegeradius = null; List<double>? abzweigwinkel = null;
        if (o["regeln"] is JsonObject ro)
        {
            foreach (var (feld, _) in ro)
                if (feld is not ("winkel" or "biegeradius" or "abzweig" or "abzweigwinkel")) Fehl("leitung.regeln", $"Unbekanntes Feld «{feld}»", "Erlaubt: winkel, biegeradius, abzweig, abzweigwinkel");
            switch (ro["winkel"])
            {
                case null: break;
                case JsonArray wa:
                    winkel = [];
                    foreach (var w in wa)
                    {
                        if (w is not JsonValue wv || wv.GetValueKind() != JsonValueKind.Number || Json.Zahl(wv) is not double z || !double.IsFinite(z))
                            Fehl("leitung.regeln.winkel", $"Winkel {w?.ToJsonString()} ist keine Zahl", "Liste von Winkeln in Grad, z. B. [15, 30, 45, 90], oder \"frei\".");
                        else if (z <= 0 || z > 180) Fehl("leitung.regeln.winkel", $"Winkel {z.ToString(CultureInfo.InvariantCulture)}° liegt nicht in 0 < w ≤ 180", "Umlenkwinkel in Grad (über 0 bis 180).");
                        else if (winkel.Any(x => Math.Abs(x - z) < 1e-9)) Fehl("leitung.regeln.winkel", $"Winkel {z.ToString(CultureInfo.InvariantCulture)}° doppelt");
                        else winkel.Add(z);
                    }
                    if (wa.Count == 0) Fehl("leitung.regeln.winkel", "Die Winkelliste ist leer", "Winkel angeben, z. B. [15, 30, 45, 90], oder \"frei\".");
                    break;
                default:
                    if (Text(ro["winkel"]) == "frei") winkelFrei = true;
                    else Fehl("leitung.regeln.winkel", $"winkel {ro["winkel"]!.ToJsonString()} ist weder eine Liste noch «frei»", "Liste in Grad, z. B. [15, 30, 45, 90], oder \"frei\".");
                    break;
            }
            if (ro["biegeradius"] is not null) biegeradius = Zahl(ro["biegeradius"], "leitung.regeln.biegeradius");
            if (ro["abzweig"] is not null)
            {
                abzweig = Text(ro["abzweig"]);
                if (abzweig != "tstueck") { Fehl("leitung.regeln.abzweig", $"abzweig «{ro["abzweig"]!.ToJsonString()}» gibt es nicht", "tstueck"); abzweig = null; }
            }
            // Abzweigwinkel der T-Stücke (Modul 3, B7; entwurf/modul-3.md «Lücke Abzweigwinkel»): Liste 0 < w ≤ 90, fehlt = [90]
            const string AwOrt = "leitung.regeln.abzweigwinkel";
            const string AwForm = "Liste der Abzweigwinkel in Grad (Winkel zwischen Durchgang und Abzweig, über 0 bis 90; 90 = rechtwinklig), z. B. [45, 88.5]; fehlt das Feld, gilt [90].";
            switch (ro["abzweigwinkel"])
            {
                case null: break;
                case JsonArray aw:
                    abzweigwinkel = [];
                    foreach (var w in aw)
                    {
                        if (w is not JsonValue wv || wv.GetValueKind() != JsonValueKind.Number || Json.Zahl(wv) is not double z || !double.IsFinite(z))
                            Fehl(AwOrt, $"Abzweigwinkel {w?.ToJsonString()} ist keine Zahl", AwForm);
                        else if (z <= 0 || z > 90) Fehl(AwOrt, $"Abzweigwinkel {z.ToString(CultureInfo.InvariantCulture)}° liegt nicht in 0 < w ≤ 90", AwForm);
                        else if (abzweigwinkel.Any(x => Math.Abs(x - z) < 1e-9)) Fehl(AwOrt, $"Abzweigwinkel {z.ToString(CultureInfo.InvariantCulture)}° doppelt", "Jeden Abzweigwinkel nur einmal nennen.");
                        else abzweigwinkel.Add(z);
                    }
                    if (aw.Count == 0) Fehl(AwOrt, "Die Liste der Abzweigwinkel ist leer", AwForm);
                    break;
                default:
                    Fehl(AwOrt, $"abzweigwinkel {ro["abzweigwinkel"]!.ToJsonString()} ist keine Liste", AwForm);
                    break;
            }
        }
        else if (o["regeln"] is not null) Fehl("leitung.regeln", "regeln muss ein Objekt sein", "{ \"winkel\": [15, 30, 45, 90], \"abzweig\": \"tstueck\" }");

        var frei = o["frei"] is null ? null : FreiLesen(o["frei"]!, istRohr, groesse);

        // Leitungsserien haben keine Körper und keine Anschlüsse
        if (d["koerper"] is JsonArray ka && ka.Count > 0)
            Fehl("koerper", "Eine Leitungsserie hat keine Körper – die Geometrie ergibt sich aus dem Verlauf der Leitung",
                "\"koerper\": [] angeben; der Querschnitt kommt aus leitung.aussen bzw. den Grössen. Formteile sind eigene Familien (leitung.formteile).");
        if (d["anschluesse"] is JsonArray aa && aa.Count > 0)
            Fehl("anschluesse", "Eine Leitungsserie hat keine Anschlüsse – die Leitung hat anfang und ende aus ihrem Verlauf",
                "\"anschluesse\": [] angeben.");

        Leitung = new FamLeitung
        {
            Querschnitt = querschnitt ?? "rund", Groesse = groesse, Aussen = aussen, Innen = innen, Wand = wand, Biegeradius = biegeradius, Kuerzel = kuerzel,
            Formteile = formteile, WinkelFrei = winkelFrei, Winkel = winkel, Abzweig = abzweig, Abzweigwinkel = abzweigwinkel, Frei = frei, Einsatz = einsatz,
        };
    }

    /// <summary>leitung.einsatz (Nachtrag Abwasser-Serie, entwurf/abwasser-serie.md; Entscheid Christian 20.10.2026): «abwasser», nur bei
    /// Rohrserien (ohne Katalog wird er gelesen). Abwasser-Serien werden immer nach Aussendurchmesser beschriftet, auch wenn ihre Grösse dn ist;
    /// die Grösse darf d (Aussendurchmesser) sein; P30 meldet sie in einem anderen Medium als Abwasser.</summary>
    string? EinsatzLesen(JsonNode n, KlassenDef? klasse, bool istRohr)
    {
        const string Ort = "leitung.einsatz";
        var t = Text(n)?.Trim();
        if (t is null || !FamLeitung.Einsaetze.Contains(t))
        {
            Fehl(Ort, $"einsatz {n.ToJsonString()} gibt es nicht", "\"abwasser\" (Abwasser-Serie: Grösse und Beschriftung nach Aussendurchmesser, z. B. «PE 110») – oder das Feld weglassen.");
            return null;
        }
        if (klasse is not null && !istRohr)
        {
            Fehl(Ort, $"einsatz «{t}» gibt es nur bei Rohrserien, nicht bei {Kategorie}", "Feld weglassen (Kanäle haben ihre Grösse d bzw. b × h ohnehin) oder die Serie als Kategorie Rohr anlegen.");
            return null;
        }
        return t;
    }

    /// <summary>Block leitung.frei (Nachtrag freie Grösse, entwurf/freie-groesse-kanal.md): nur Kanalserien (rund und rechteckig) –
    /// Rohrserien nie, dort gilt die DN. vorlage muss ein Typ sein; die Grössenparameter sind Typparameter ohne Formel (sie werden für
    /// jede freie Grösse ersetzt). grenzen je Grössenschlüssel { min, max } in mm (&gt; 0, min ≤ max), nur aus der Quelle – ohne
    /// frei.quellen ein Hinweis. schritt: ganze mm ≥ 1 (Vorgabe 1), weil die Anschlussgrössen ganze mm sind.</summary>
    FamFrei? FreiLesen(JsonNode n, bool istRohr, IReadOnlyDictionary<string, string> groesse)
    {
        const string Ort = "leitung.frei";
        const string Form = "{ \"vorlage\": \"<Typname>\", \"grenzen\": { \"b\": { \"min\": …, \"max\": … }, \"h\": { … } }, \"schritt\": 1, \"quellen\": [ … ] } – Grenzen nur aus der Unterlage.";
        if (istRohr || groesse.ContainsKey("dn"))
        {
            Fehl(Ort, "Freie Grössen gibt es nur bei Kanalserien – bei Rohren gilt die DN bzw. bei Abwasser-Serien der Aussendurchmesser (die Typen sind die Grössen)", "Block frei entfernen; jede Rohrgrösse als Typ führen.");
            return null;
        }
        if (n is not JsonObject o) { Fehl(Ort, "frei muss ein Objekt sein", Form); return null; }
        foreach (var (feld, _) in o)
            if (feld is not ("vorlage" or "grenzen" or "schritt" or "quellen")) Fehl(Ort, $"Unbekanntes Feld «{feld}»", "Erlaubt: vorlage, grenzen, schritt, quellen");
        bool ok = true;
        var vorlage = Text(o["vorlage"])?.Trim();
        if (string.IsNullOrEmpty(vorlage))
        {
            Fehl(Ort + ".vorlage", "vorlage fehlt", "Typname angeben, dessen Werte und Formeln (aussen, innen, wand …) für jede freie Grösse gelten, z. B. ein Typ des Rasters.");
            ok = false;
        }
        else if (Typ(vorlage) is null)
        {
            Fehl(Ort + ".vorlage", $"Typ «{vorlage}» gibt es nicht", "Typen: " + string.Join(", ", Typen.Select(t => t.Name)));
            ok = false;
        }
        foreach (var (schl, pn) in groesse)
            if (NachName.TryGetValue(pn, out var p) && p.HatFormel)
            {
                Fehl(Ort, $"Der Grössenparameter «{pn}» ({schl}) hat eine Formel – bei freier Grösse muss er ein Typparameter ohne Formel sein (er wird durch die gewünschte Grösse ersetzt)",
                    $"Die Formel von «{pn}» entfernen und die Grösse je Typ als Wert angeben (typen.werte.{pn}).");
                ok = false;
            }

        var grenzen = new Dictionary<string, FamGrenze>(StringComparer.Ordinal);
        switch (o["grenzen"])
        {
            case null: break;
            case JsonObject go:
                foreach (var (schl, g) in go)
                {
                    var ort = $"{Ort}.grenzen.{schl}";
                    if (!groesse.ContainsKey(schl))
                    {
                        Fehl(ort, $"Grössenschlüssel «{schl}» gibt es in dieser Serie nicht", "Grössenschlüssel: " + string.Join(", ", groesse.Keys));
                        ok = false; continue;
                    }
                    if (g is not JsonObject gb) { Fehl(ort, "Grenze muss ein Objekt { min, max } sein", "{ \"min\": 145, \"max\": 2500 } in mm"); ok = false; continue; }
                    foreach (var (feld, _) in gb)
                        if (feld is not ("min" or "max")) { Fehl(ort, $"Unbekanntes Feld «{feld}»", "Erlaubt: min, max (mm)"); ok = false; }
                    double? Grenze(string feld)
                    {
                        if (gb[feld] is null) return null;
                        if (gb[feld] is JsonValue v && v.GetValueKind() == JsonValueKind.Number && Json.Zahl(v) is double z && double.IsFinite(z) && z > 0) return z;
                        Fehl($"{ort}.{feld}", $"{feld} {gb[feld]!.ToJsonString()} ist keine Zahl > 0", "Grenze in mm angeben, nur aus der Unterlage.");
                        ok = false;
                        return null;
                    }
                    var min = Grenze("min"); var max = Grenze("max");
                    if (min is double a && max is double b && a > b) { Fehl(ort, $"min {Zahltext(a)} ist grösser als max {Zahltext(b)}", "min ≤ max angeben."); ok = false; }
                    if (min is not null || max is not null) grenzen[schl] = new FamGrenze(min, max);
                }
                break;
            default:
                Fehl(Ort + ".grenzen", "grenzen muss ein Objekt sein", Form);
                ok = false;
                break;
        }
        if (grenzen.Count > 0 && o["quellen"] is not JsonArray { Count: > 0 })
            Hinweise.Add(new(Ort + ".grenzen", "Die Grenzen der freien Grösse haben keine Quelle (frei.quellen)", "Grenzen nur aus der Unterlage übernehmen und den Befund in leitung.frei.quellen nennen; sonst als Annahme im Bericht aufführen."));

        double schritt = 1;
        if (o["schritt"] is { } sn)
        {
            if (sn is JsonValue sv && sv.GetValueKind() == JsonValueKind.Number && Json.Zahl(sv) is double s && double.IsFinite(s) && s >= 1 && Math.Abs(s - Math.Round(s)) < 1e-9) schritt = Math.Round(s);
            else
            {
                Fehl(Ort + ".schritt", $"schritt {sn.ToJsonString()} ist keine ganze Zahl ≥ 1", "Raster der Eingabe in ganzen mm angeben, z. B. 1 oder 5 (die Anschlussgrössen sind ganze mm); ohne Angabe gilt 1.");
                ok = false;
            }
        }
        return ok ? new FamFrei { Vorlage = vorlage!, Grenzen = grenzen, Schritt = schritt } : null;
    }

    static string Zahltext(double x) => x.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Höchstlänge des Werkstoff-Kürzels (leitung.kuerzel). Begründung (Hauptlauf 19.10.2026): das Kürzel steht mit der
    /// Grösse auf dem Rohr im Schema und im Grundriss («CNS NW 20», «PE-S 110»); die üblichen Kürzel haben 2 bis 5 Zeichen,
    /// 12 lässt Platz für Zusammensetzungen («PE-HD/PE-Xa») und hält die Beschriftung kurz genug für ein Rohr auf dem Plan.</summary>
    public const int KuerzelMax = 12;

    /// <summary>Werkstoff-Kürzel prüfen: ein Text, nicht leer, höchstens <see cref="KuerzelMax"/> Zeichen, ohne Zeilenumbruch oder
    /// andere Steuerzeichen (die Beschriftung ist eine Zeile). Führende und nachgestellte Leerzeichen werden abgeschnitten.
    /// Bei Kanalserien ohne Wirkung (Kanäle bleiben «b×h» bzw. «Ø d») – dann ein Hinweis.</summary>
    string? KuerzelLesen(JsonNode n, bool istKanal)
    {
        const string Ort = "leitung.kuerzel";
        const string Form = "Kurzer Text für die Beschriftung, z. B. \"CNS\" (nichtrostender Stahl), \"PE-S\" oder \"PEX\"; ohne Zeilenumbruch, höchstens 12 Zeichen; ohne Angabe das Feld weglassen.";
        if (n is not JsonValue v || !v.TryGetValue<string>(out var roh)) { Fehl(Ort, "kuerzel muss ein Text sein", Form); return null; }
        var s = roh.Trim();
        if (s.Length == 0) { Fehl(Ort, "kuerzel ist leer", "Feld weglassen, wenn die Unterlage kein Kürzel nennt (dann zeigt die Beschriftung nur die Grösse)."); return null; }
        if (s.Any(char.IsControl)) { Fehl(Ort, "kuerzel darf keinen Zeilenumbruch und keine Steuerzeichen enthalten", Form); return null; }
        if (s.Length > KuerzelMax) { Fehl(Ort, $"kuerzel ist mit {s.Length} Zeichen zu lang (höchstens {KuerzelMax})", Form); return null; }
        if (istKanal) Hinweise.Add(new(Ort, "kuerzel wirkt nur bei Rohren – Kanäle bleiben «b×h» bzw. «Ø d»", "Feld weglassen."));
        return s;
    }

    /// <summary>Namen, die eine Typregel (leitung.formteile.*.typ) benutzen darf: die Grössenparameter der Serie, bei T-Stück
    /// (Abzweig) und Reduktion (kleine Grösse) zusätzlich mit der Endung «_2».</summary>
    public static IReadOnlyList<string> TypregelNamen(IEnumerable<string> groessenParameter, string art)
    {
        var l = groessenParameter.ToList();
        if (art is "tstueck" or "reduktion") l.AddRange(l.ToList().Select(x => x + "_2"));
        return l;
    }

    static string Familienfaehig(Katalog? kat) => kat is null ? "" :
        string.Join(", ", kat.Klassen.Values.Where(k => k.Familie).Select(k => k.Abstrakt ? k.Name + " (Basis)" : k.Name).Order());

    // ------------------------------------------------------------------ Parameter

    void ParameterLesen(JsonNode? n, Katalog? kat, KlassenDef? klasse)
    {
        if (n is null) { Fehl("parameter", "parameter fehlt", "\"parameter\": [] angeben (auch leer)."); return; }
        if (n is not JsonArray a) { Fehl("parameter", "parameter muss eine Liste sein"); return; }
        string[] felder = ["name", "art", "datentyp", "einheit", "gruppe", "wert", "formel", "ziel", "beschreibung", "quellen"];
        var klein = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int nr = 0;
        foreach (var x in a)
        {
            nr++;
            if (x is not JsonObject o) { Fehl($"parameter[{nr}]", "Parameter muss ein Objekt sein"); continue; }
            var name = Text(o["name"])?.Trim() ?? "";
            var ort = name.Length > 0 ? $"parameter «{name}»" : $"parameter[{nr}]";
            foreach (var (feld, _) in o)
                if (!felder.Contains(feld)) Fehl(ort, $"Unbekanntes Feld «{feld}»", "Erlaubt: " + string.Join(", ", felder));
            if (name.Length == 0) { Fehl(ort, "Name fehlt", "\"name\" angeben (Buchstaben, Ziffern, _)."); continue; }
            if (!NamensMuster.IsMatch(name))
                Fehl(ort, $"Name «{name}» ist ungültig", "Erlaubt: Buchstaben (auch Umlaute), Ziffern und _, am Anfang keine Ziffer; keine Leerzeichen.");
            if (NachName.ContainsKey(name)) { Fehl(ort, $"Parameter «{name}» gibt es zweimal", "Namen eindeutig machen."); continue; }
            if (klein.TryGetValue(name, out var anderer))
                Fehl(ort, $"«{name}» unterscheidet sich von «{anderer}» nur in Gross/Kleinschreibung", "Namen deutlich unterscheiden (Verwechslungsgefahr in Formeln).");
            klein[name] = name;
            var art = Text(o["art"]) ?? "";
            if (!Arten.Contains(art)) Fehl(ort + ".art", $"art «{art}» gibt es nicht", "typ (je Typ fest) oder exemplar (je Element änderbar)");
            var datentyp = Text(o["datentyp"]) ?? "";
            if (!Datentypen.Contains(datentyp)) Fehl(ort + ".datentyp", $"datentyp «{datentyp}» gibt es nicht", "Erlaubt: " + string.Join(", ", Datentypen));
            var gruppe = Text(o["gruppe"]);
            if (gruppe is not null && !Gruppen.Contains(gruppe)) Fehl(ort + ".gruppe", $"gruppe «{gruppe}» gibt es nicht", "Erlaubt: " + string.Join(", ", Gruppen));
            var einheit = Text(o["einheit"]);
            if (datentyp == "laenge" && einheit is not null and not "mm") Fehl(ort + ".einheit", $"Längen sind immer in mm, nicht {einheit}", "einheit weglassen oder mm; Wert in mm umrechnen.");
            if (datentyp == "winkel" && einheit is not null and not ("°" or "Grad")) Fehl(ort + ".einheit", $"Winkel sind immer in Grad, nicht {einheit}", "einheit weglassen; Wert in Grad umrechnen.");
            var formelText = o.ContainsKey("formel") ? Text(o["formel"]) : null;
            if (o.ContainsKey("formel") && formelText is null) Fehl(ort + ".formel", "formel muss ein Text sein", "z. B. \"bodenabstand + hoehe\"");
            Ausdruck? formel = null;
            if (formelText is not null)
            {
                try { formel = Hlks.Kern.Formel.Parsen(formelText); }
                catch (FormelFehler ff) { Fehl(ort + ".formel", $"Formel «{formelText}»: {ff.MitStelle}", ff.Vorschlag); }
                if (o["wert"] is not null) Fehl(ort, "formel und wert zugleich", "Entweder eine Formel oder einen Vorgabewert angeben, nicht beides.");
            }
            var p = new FamParameter
            {
                Name = name, Art = Arten.Contains(art) ? art : "typ", Datentyp = Datentypen.Contains(datentyp) ? datentyp : "zahl",
                Einheit = einheit, Gruppe = gruppe, Wert = o["wert"], FormelText = formelText, Formel = formel,
                Ziel = Text(o["ziel"]), Beschreibung = Text(o["beschreibung"]),
            };
            if (p.Wert is not null && Datentypen.Contains(datentyp))
            {
                var w = p.Umwandeln(p.Wert, out var fehler);
                if (fehler is not null) Fehl(ort + ".wert", fehler);
                else if (p.Datentyp == "medium" && w is { } mw && kat is not null) MediumPruefen(ort + ".wert", mw.Text!, kat);
            }
            if (p.Ziel is not null) ZielPruefen(ort + ".ziel", p, kat, klasse);
            Parameter.Add(p);
            NachName[name] = p;
        }
    }

    void MediumPruefen(string ort, string medium, Katalog kat)
    {
        if (medium != "*" && !kat.Medien.ContainsKey(medium))
            Fehl(ort, $"Medium «{medium}» gibt es nicht", "Medien: " + string.Join(", ", kat.Medien.Keys) + " oder * (Medium des Systems)");
    }

    void ZielPruefen(string ort, FamParameter p, Katalog? kat, KlassenDef? klasse)
    {
        if (klasse is null) return;   // Kategorie schon als Fehler gemeldet
        if (Katalog.FamilienEigenschaften.Any(e => e.Name.Equals(p.Ziel, StringComparison.OrdinalIgnoreCase)))
        {
            Fehl(ort, $"ziel «{p.Ziel}» ist eine Familieneigenschaft und kann nicht gesetzt werden", "Ein Kennwert der Klasse wählen, z. B. leistung.");
            return;
        }
        var e = klasse.Eigenschaften.FirstOrDefault(x => x.Name.Equals(p.Ziel, StringComparison.OrdinalIgnoreCase));
        if (e is null)
        {
            var erlaubt = klasse.Eigenschaften.Where(x => !Katalog.FamilienEigenschaften.Any(f => f.Name == x.Name)).Select(x => x.Name).ToList();
            Fehl(ort, $"Klasse {klasse.Name} hat keine Eigenschaft «{p.Ziel}»", erlaubt.Count > 0 ? "Eigenschaften: " + string.Join(", ", erlaubt) : $"{klasse.Name} hat keine Eigenschaften für Kennwerte.");
            return;
        }
        bool passt = e.Typ switch
        {
            "zahl" => !p.IstText && p.Datentyp != "ja_nein",
            "bool" => p.Datentyp == "ja_nein",
            "text" or "auswahl" => p.IstText,
            _ => false,
        };
        if (!passt) Fehl(ort, $"Eigenschaft «{e.Name}» ist {e.Typ}, der Parameter {p.Datentyp}", "Datentyp des Parameters passend wählen (zahl/laenge ↔ zahl, ja_nein ↔ bool, text ↔ text/auswahl).");
        if (e.Einheit is not null && p.Einheit is not null && !e.Einheit.Equals(p.Einheit, StringComparison.OrdinalIgnoreCase))
            Fehl(ort, $"Eigenschaft «{e.Name}» ist in {e.Einheit}, der Parameter in {p.Einheit}", $"Parameter in {e.Einheit} führen (Werte umrechnen) – der Kern rechnet Einheiten nicht um.");
        if (e.Typ == "auswahl" && p.Wert is JsonValue v && v.TryGetValue<string>(out var s) && e.Auswahl?.Contains(s, StringComparer.OrdinalIgnoreCase) == false)
            Fehl(ort, $"«{s}» ist für {e.Name} nicht erlaubt", "Erlaubt: " + string.Join(", ", e.Auswahl ?? []));
    }

    // ------------------------------------------------------------------ Typen

    void TypenLesen(JsonNode? n)
    {
        if (n is null) { Fehl("typen", "typen fehlt", "Mindestens einen Typ angeben: [{ \"name\": \"…\", \"werte\": { … } }]."); return; }
        if (n is not JsonArray a) { Fehl("typen", "typen muss eine Liste sein"); return; }
        if (a.Count == 0) Fehl("typen", "Die Familie hat keinen Typ", "Mindestens einen Typ angeben (wie in Revit).");
        int nr = 0;
        foreach (var x in a)
        {
            nr++;
            if (x is not JsonObject o) { Fehl($"typen[{nr}]", "Typ muss ein Objekt sein"); continue; }
            var name = Text(o["name"])?.Trim() ?? "";
            var ort = name.Length > 0 ? $"typ «{name}»" : $"typen[{nr}]";
            foreach (var (feld, _) in o)
                if (feld is not ("name" or "werte" or "quellen" or "entfaellt")) Fehl(ort, $"Unbekanntes Feld «{feld}»", "Erlaubt: name, werte, quellen, entfaellt");
            if (name.Length == 0) { Fehl(ort, "Name fehlt", "\"name\" angeben, z. B. die Typenbezeichnung des Herstellers."); continue; }
            if (Typen.Any(t => t.Name == name)) { Fehl(ort, $"Typ «{name}» gibt es zweimal", "Typnamen eindeutig machen."); continue; }
            var t = new FamTyp { Name = name };
            if (o["werte"] is not JsonObject werte) Fehl(ort, "werte fehlt oder ist kein Objekt", "\"werte\": { \"parameter\": wert }");
            else
                foreach (var (pn, wert) in werte)
                {
                    var wort = $"{ort}.werte.{pn}";
                    if (!NachName.TryGetValue(pn, out var p)) { Fehl(wort, $"Parameter «{pn}» gibt es nicht", Hlks.Kern.Formel.NamensVorschlag(pn, NachName.Keys)); continue; }
                    if (p.HatFormel) { Fehl(wort, $"«{pn}» hat eine Formel und ist nicht pro Typ einstellbar", "Wert aus dem Typ entfernen oder die Formel durch einen Vorgabewert ersetzen."); continue; }
                    var w = p.Umwandeln(wert, out var fehler);
                    if (fehler is not null) Fehl(wort, fehler);
                    else if (p.Datentyp == "medium" && w is { } mw && Katalog is { } kat) MediumPruefen(wort, mw.Text!, kat);
                    t.Werte[pn] = wert;
                }
            if (o["quellen"] is { } q && q is not JsonObject) Fehl(ort + ".quellen", "quellen muss ein Objekt sein", "{ \"parameter\": [\"<befund-id>\"] }");
            else if (o["quellen"] is JsonObject qo)
                foreach (var (pn, _) in qo)
                    if (!NachName.ContainsKey(pn)) Fehl($"{ort}.quellen.{pn}", $"Parameter «{pn}» gibt es nicht", Hlks.Kern.Formel.NamensVorschlag(pn, NachName.Keys));
            // entfaellt (30.10.2026): Typparameter, die es für diesen Typ nicht gibt (z. B. ein Winkel, den der Hersteller in dieser Grösse nicht
            // liefert) – mit Quelle; sie zählen nicht als fehlende Pflichtwerte, einen Wert dazu gibt es nicht
            if (o["entfaellt"] is { } en && en is not JsonObject) Fehl(ort + ".entfaellt", "entfaellt muss ein Objekt sein", "{ \"parameter\": [\"<befund-id>\"] }");
            else if (o["entfaellt"] is JsonObject eo)
                foreach (var (pn, l) in eo)
                {
                    var eort = $"{ort}.entfaellt.{pn}";
                    if (!NachName.TryGetValue(pn, out var p)) Fehl(eort, $"Parameter «{pn}» gibt es nicht", Hlks.Kern.Formel.NamensVorschlag(pn, NachName.Keys));
                    else if (p.Art != "typ") Fehl(eort, $"«{pn}» ist kein Typparameter", "entfaellt gilt nur für Typparameter.");
                    else if (t.Werte.ContainsKey(pn)) Fehl(eort, $"«{pn}» hat in diesem Typ einen Wert und kann nicht entfallen", "Wert entfernen oder den Eintrag in entfaellt.");
                    if (l is not JsonArray { Count: > 0 }) Fehl(eort, "Quelle fehlt: dass es den Wert nicht gibt, braucht einen Beleg", "Befund-Id der Unterlage angeben, z. B. die Zeile «d12/d18 nur 45 oder 90».");
                    t.Entfaellt.Add(pn);
                }
            Typen.Add(t);
        }
    }

    // ------------------------------------------------------------------ Körper

    FamZahl? Zahl(JsonNode? n, string ort, bool pflicht = true)
    {
        if (n is null)
        {
            if (pflicht) Fehl(ort, "Wert fehlt", "Zahl oder Formel angeben.");
            return null;
        }
        FamZahl? z = null;
        if (n is JsonValue v)
        {
            if (v.GetValueKind() == JsonValueKind.Number) z = new FamZahl { Konstante = Json.Zahl(v), Ort = ort };
            else if (v.TryGetValue<bool>(out var b)) z = new FamZahl { Konstante = b ? 1 : 0, Ort = ort };
            else if (v.TryGetValue<string>(out var s))
            {
                try { z = new FamZahl { Formel = Hlks.Kern.Formel.Parsen(s), Text = s, Ort = ort }; }
                catch (FormelFehler ff) { Fehl(ort, $"Formel «{s}»: {ff.MitStelle}", ff.Vorschlag); return null; }
            }
        }
        if (z is null) { Fehl(ort, "muss eine Zahl oder eine Formel (Text) sein"); return null; }
        alleZahlen.Add(z);
        return z;
    }

    FamZahl[]? Punkt(JsonNode? n, string ort, int anzahl, bool pflicht = true)
    {
        if (n is null) { if (pflicht) Fehl(ort, "fehlt", $"[{string.Join(", ", new[] { "x", "y", "z" }.Take(anzahl))}] angeben."); return null; }
        if (n is not JsonArray a || a.Count != anzahl) { Fehl(ort, $"muss eine Liste mit {anzahl} Zahlen oder Formeln sein"); return null; }
        var p = new FamZahl[anzahl];
        bool ok = true;
        for (int i = 0; i < anzahl; i++) { var z = Zahl(a[i], $"{ort}[{i + 1}]"); if (z is null) ok = false; else p[i] = z; }
        return ok ? p : null;
    }

    List<FamPunkt2>? Kontur(JsonNode? n, string ort, int mindestens = 3, bool pflicht = true)
    {
        if (n is null) { if (pflicht) Fehl(ort, "fehlt", "[[u, v], [u, v, wölbung], …] angeben."); return null; }
        if (n is not JsonArray a || a.Count < mindestens) { Fehl(ort, $"braucht mindestens {mindestens} Punkte [u, v] bzw. [u, v, wölbung]"); return null; }
        var l = new List<FamPunkt2>();
        int nr = 0;
        foreach (var x in a)
        {
            nr++;
            if (x is not JsonArray p || p.Count is < 2 or > 3) { Fehl($"{ort}[{nr}]", "Punkt muss [u, v] oder [u, v, wölbung] sein"); continue; }
            var u = Zahl(p[0], $"{ort}[{nr}][1]"); var v = Zahl(p[1], $"{ort}[{nr}][2]");
            var w = p.Count > 2 ? Zahl(p[2], $"{ort}[{nr}][3]") : null;
            if (u is not null && v is not null) l.Add(new(u, v, w));
        }
        return l;
    }

    /// <summary>Geschlossene Kontur eines Körpers (extrusion, rotation, Loch): mindestens 2 Punkte (Schema); mit nur 2 Punkten
    /// braucht es eine Wölbung, sonst umschliesst der Zug keine Fläche (Kreis = zwei Punkte mit Wölbung 1, Entscheid Hauptlauf
    /// 18.10.2026). Ob die Fläche wirklich > 0 ist, prüft die Auswertung je Typ (Formeln).</summary>
    List<FamPunkt2>? Flaechenkontur(JsonNode? n, string ort)
    {
        var k = Kontur(n, ort, 2);
        if (k is { Count: 2 } && k.All(p => p.Woelbung is null || p.Woelbung.Konstante == 0))
            Fehl(ort, "Kontur aus zwei Punkten ohne Wölbung umschliesst keine Fläche",
                "Wölbung angeben (Kreis: [[u1, v, 1], [u2, v, 1]] – zwei Halbkreise) oder mindestens drei Punkte.");
        return k;
    }

    IReadOnlySet<string> DetailgradLesen(JsonNode? n, string ort)
    {
        if (n is null) return AlleDetailgrade;
        if (n is not JsonArray a || a.Count == 0) { Fehl(ort, "detailgrad muss eine nicht leere Liste sein", "[\"grob\", \"mittel\", \"fein\"]"); return AlleDetailgrade; }
        var s = new HashSet<string>();
        foreach (var x in a)
        {
            var t = Text(x);
            if (t is null || !Detailgrade.Contains(t)) Fehl(ort, $"Detailgrad «{x?.ToJsonString()}» gibt es nicht", "grob, mittel, fein");
            else if (!s.Add(t)) Fehl(ort, $"Detailgrad «{t}» doppelt");
        }
        return s.Count > 0 ? s : AlleDetailgrade;
    }

    void KoerperLesen(JsonNode? n)
    {
        if (n is null) { Fehl("koerper", "koerper fehlt", "\"koerper\": [] angeben (auch leer)."); return; }
        if (n is not JsonArray a) { Fehl("koerper", "koerper muss eine Liste sein"); return; }
        string[] gemeinsam = ["id", "name", "art", "detailgrad", "sichtbar", "material", "quellen"];
        var jeArt = new Dictionary<string, string[]>
        {
            ["quader"] = ["von", "bis"], ["zylinder"] = ["von", "nach", "r"], ["kegel"] = ["von", "nach", "r1", "r2"], ["kugel"] = ["mitte", "r"],
            ["extrusion"] = ["ebene", "kontur", "loecher", "von", "bis"], ["rotation"] = ["ebene", "kontur", "achse", "winkel"],
            ["sweep"] = ["profil", "pfad", "biegeradius"],
        };
        int nr = 0;
        foreach (var x in a)
        {
            nr++;
            if (x is not JsonObject o) { Fehl($"koerper[{nr}]", "Körper muss ein Objekt sein"); continue; }
            var id = Text(o["id"])?.Trim() ?? "";
            var ort = id.Length > 0 ? $"koerper «{id}»" : $"koerper[{nr}]";
            if (id.Length == 0) Fehl(ort, "id fehlt", "Eindeutige id angeben, z. B. \"gehaeuse\".");
            else if (Koerper.Any(k => k.Id == id)) Fehl(ort, $"Körper-id «{id}» gibt es zweimal", "ids eindeutig machen.");
            var art = Text(o["art"]) ?? "";
            if (!Koerperarten.Contains(art)) { Fehl(ort + ".art", $"Körperart «{art}» gibt es nicht", "Erlaubt: " + string.Join(", ", Koerperarten)); continue; }
            foreach (var (feld, _) in o)
                if (!gemeinsam.Contains(feld) && !jeArt[art].Contains(feld))
                    Fehl(ort, $"Feld «{feld}» gehört nicht zu einem {art}", $"Felder für {art}: {string.Join(", ", jeArt[art])}");
            var material = Text(o["material"]) ?? "geraet";
            if (!Materialien.Contains(material)) Fehl(ort + ".material", $"material «{material}» gibt es nicht", "Erlaubt: " + string.Join(", ", Materialien));
            var detail = DetailgradLesen(o["detailgrad"], ort + ".detailgrad");
            var sichtbar = o["sichtbar"] is null ? null : Zahl(o["sichtbar"], ort + ".sichtbar");
            FamKoerper k;
            switch (art)
            {
                case "quader":
                    k = new FamKoerper { Von = Punkt(o["von"], ort + ".von", 3), Bis = Punkt(o["bis"], ort + ".bis", 3) };
                    break;
                case "zylinder":
                    k = new FamKoerper { Von = Punkt(o["von"], ort + ".von", 3), Nach = Punkt(o["nach"], ort + ".nach", 3), R = Zahl(o["r"], ort + ".r") };
                    break;
                case "kegel":
                    k = new FamKoerper { Von = Punkt(o["von"], ort + ".von", 3), Nach = Punkt(o["nach"], ort + ".nach", 3), R1 = Zahl(o["r1"], ort + ".r1"), R2 = Zahl(o["r2"], ort + ".r2") };
                    break;
                case "kugel":
                    k = new FamKoerper { Mitte = Punkt(o["mitte"], ort + ".mitte", 3), R = Zahl(o["r"], ort + ".r") };
                    break;
                case "extrusion":
                {
                    var ebene = EbeneLesen(o["ebene"], ort);
                    var loecher = new List<List<FamPunkt2>>();
                    if (o["loecher"] is JsonArray la)
                    {
                        int ln = 0;
                        foreach (var l in la) { ln++; if (Flaechenkontur(l, $"{ort}.loecher[{ln}]") is { } lk) loecher.Add(lk); }
                    }
                    else if (o["loecher"] is not null) Fehl(ort + ".loecher", "loecher muss eine Liste von Konturen sein");
                    k = new FamKoerper { Ebene = ebene, Kontur = Flaechenkontur(o["kontur"], ort + ".kontur"), Loecher = loecher, ExVon = Zahl(o["von"], ort + ".von"), ExBis = Zahl(o["bis"], ort + ".bis") };
                    break;
                }
                case "rotation":
                {
                    var ebene = EbeneLesen(o["ebene"], ort);
                    FamZahl[]? ap = null; string? richtung = null;
                    if (o["achse"] is JsonObject achse)
                    {
                        ap = Punkt(achse["punkt"], ort + ".achse.punkt", 2);
                        richtung = Text(achse["richtung"]);
                        if (richtung is not ("u" or "v")) Fehl(ort + ".achse.richtung", $"richtung «{richtung}» gibt es nicht", "u oder v (Achse parallel zur u- bzw. v-Achse der Ebene)");
                    }
                    else Fehl(ort + ".achse", "achse fehlt", "{ \"punkt\": [u, v], \"richtung\": \"v\" }");
                    k = new FamKoerper { Ebene = ebene, Kontur = Flaechenkontur(o["kontur"], ort + ".kontur"), AchsePunkt = ap, AchseRichtung = richtung,
                        Winkel = o["winkel"] is null ? null : Zahl(o["winkel"], ort + ".winkel") };
                    break;
                }
                default:   // sweep
                {
                    FamZahl? kreis = null; FamZahl[]? rechteck = null;
                    if (o["profil"] is JsonObject pr)
                    {
                        if (pr["kreis"] is not null && pr["rechteck"] is not null) Fehl(ort + ".profil", "Profil hat kreis und rechteck", "Genau eines angeben.");
                        if (pr["kreis"] is not null) kreis = Zahl(pr["kreis"], ort + ".profil.kreis");
                        else if (pr["rechteck"] is not null) rechteck = Punkt(pr["rechteck"], ort + ".profil.rechteck", 2);
                        else Fehl(ort + ".profil", "Profil braucht kreis (Radius) oder rechteck [b, h]");
                        foreach (var (feld, _) in pr) if (feld is not ("kreis" or "rechteck")) Fehl(ort + ".profil", $"Unbekanntes Feld «{feld}»", "kreis oder rechteck");
                    }
                    else Fehl(ort + ".profil", "profil fehlt", "{ \"kreis\": r } oder { \"rechteck\": [b, h] }");
                    List<FamZahl[]>? pfad = null;
                    if (o["pfad"] is JsonArray pa && pa.Count >= 2)
                    {
                        pfad = [];
                        int pn = 0;
                        foreach (var p in pa) { pn++; if (Punkt(p, $"{ort}.pfad[{pn}]", 3) is { } pp) pfad.Add(pp); }
                    }
                    else Fehl(ort + ".pfad", "pfad braucht mindestens 2 Punkte [x, y, z]");
                    k = new FamKoerper { ProfilKreis = kreis, ProfilRechteck = rechteck, Pfad = pfad, Biegeradius = o["biegeradius"] is null ? null : Zahl(o["biegeradius"], ort + ".biegeradius") };
                    break;
                }
            }
            Koerper.Add(new FamKoerper
            {
                Id = id, Name = Text(o["name"]), Art = art, Material = Materialien.Contains(material) ? material : "geraet", Detailgrad = detail, Sichtbar = sichtbar,
                Von = k.Von, Bis = k.Bis, Nach = k.Nach, Mitte = k.Mitte, R = k.R, R1 = k.R1, R2 = k.R2, Ebene = k.Ebene, Kontur = k.Kontur, Loecher = k.Loecher,
                ExVon = k.ExVon, ExBis = k.ExBis, AchsePunkt = k.AchsePunkt, AchseRichtung = k.AchseRichtung, Winkel = k.Winkel,
                ProfilKreis = k.ProfilKreis, ProfilRechteck = k.ProfilRechteck, Pfad = k.Pfad, Biegeradius = k.Biegeradius,
            });
        }
    }

    string? EbeneLesen(JsonNode? n, string ort)
    {
        var e = Text(n);
        if (e is null || !Ebenen.Contains(e)) { Fehl(ort + ".ebene", $"ebene «{e}» gibt es nicht", "xy, xz oder yz"); return null; }
        return e;
    }

    // ------------------------------------------------------------------ Anschlüsse

    void AnschluesseLesen(JsonNode? n, Katalog? kat)
    {
        if (n is null) { Fehl("anschluesse", "anschluesse fehlt", "\"anschluesse\": [] angeben (auch leer)."); return; }
        if (n is not JsonArray a) { Fehl("anschluesse", "anschluesse muss eine Liste sein"); return; }
        string[] felder = ["name", "medium", "fluss", "art", "form", "dn", "d", "gewinde", "b", "h", "lage", "richtung", "beschreibung", "sichtbar", "quellen"];
        int nr = 0;
        foreach (var x in a)
        {
            nr++;
            if (x is not JsonObject o) { Fehl($"anschluesse[{nr}]", "Anschluss muss ein Objekt sein"); continue; }
            var name = Text(o["name"])?.Trim() ?? "";
            var ort = name.Length > 0 ? $"anschluss «{name}»" : $"anschluesse[{nr}]";
            foreach (var (feld, _) in o)
                if (!felder.Contains(feld)) Fehl(ort, $"Unbekanntes Feld «{feld}»", "Erlaubt: " + string.Join(", ", felder));
            if (name.Length == 0) Fehl(ort, "Name fehlt", "\"name\" angeben, z. B. vl, rl, ein, aus.");
            else if (Anschluesse.Any(an => an.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                Fehl(ort, $"Anschluss «{name}» gibt es zweimal", "Anschlussnamen eindeutig machen (Gross/Kleinschreibung zählt nicht).");
            var medium = Text(o["medium"])?.Trim() ?? "";
            if (medium.Length == 0) Fehl(ort + ".medium", "medium fehlt", "Kennung aus medien.json oder * (Medium des Systems).");
            else if (kat is not null && medium != "*" && !kat.Medien.ContainsKey(medium))
            {
                if (!(NachName.TryGetValue(medium, out var mp) && mp.Datentyp == "medium"))
                    Fehl(ort + ".medium", $"Medium «{medium}» gibt es nicht", "Medien: " + string.Join(", ", kat.Medien.Keys) + " oder * (Medium des Systems) oder ein Parameter mit Datentyp medium");
            }
            else if (kat is not null && kat.Medien.TryGetValue(medium, out var md)) medium = md.Kennung;
            var fluss = Text(o["fluss"]) ?? "";
            if (!Fluesse.Contains(fluss)) Fehl(ort + ".fluss", $"fluss «{fluss}» gibt es nicht", "rein, raus oder beide (bezogen auf die Fliessrichtung des Mediums)");
            var art = Text(o["art"]);
            if (art is not null && !AnschlussArten.Contains(art)) Fehl(ort + ".art", $"art «{art}» gibt es nicht", "rohr, kanal oder elektro");
            var form = Text(o["form"]);
            if (form is not null && !Formen.Contains(form)) Fehl(ort + ".form", $"form «{form}» gibt es nicht", "rund oder rechteck");
            var richtung = Punkt(o["richtung"], ort + ".richtung", 3);
            if (richtung is not null && richtung.All(r => r.Konstante is not null) && richtung.All(r => Math.Abs(r.Konstante!.Value) < 1e-12))
                Fehl(ort + ".richtung", "Richtung [0, 0, 0] hat keine Richtung", "Richtung aus dem Gerät heraus angeben, z. B. [0, 0, -1] für einen Anschluss nach unten.");
            GroesseHinweise(ort, art, form, o);
            Anschluesse.Add(new FamAnschluss
            {
                Name = name, Medium = medium.Length > 0 ? medium : "*", Fluss = Fluesse.Contains(fluss) ? fluss : "beide", Art = art, Form = form,
                Dn = o["dn"] is null ? null : Zahl(o["dn"], ort + ".dn"), D = o["d"] is null ? null : Zahl(o["d"], ort + ".d"),
                B = o["b"] is null ? null : Zahl(o["b"], ort + ".b"), H = o["h"] is null ? null : Zahl(o["h"], ort + ".h"), Gewinde = Text(o["gewinde"]),
                Lage = Punkt(o["lage"], ort + ".lage", 3) ?? [], Richtung = richtung ?? [],
                Sichtbar = o["sichtbar"] is null ? null : Zahl(o["sichtbar"], ort + ".sichtbar"), Beschreibung = Text(o["beschreibung"]),
            });
        }
    }

    /// <summary>Grösse des Anschlusses passend zur Art (Entscheid Hauptlauf 18.10.2026): Rohr nimmt dn, Kanal rund d, Kanal
    /// rechteckig b und h. Nachtrag Abwasser-Serie (entwurf/abwasser-serie.md): ein Rohranschluss darf statt dn den Aussendurchmesser d
    /// tragen (Formteile einer Abwasser-Serie nach Aussendurchmesser). Fehlt etwas oder passt es nicht zur Art, ist das ein Hinweis, kein
    /// Fehler – Entwürfe dürfen Lücken haben.</summary>
    void GroesseHinweise(string ort, string? art, string? form, JsonObject o)
    {
        void Hinweis(string text) => Hinweise.Add(new FamFehler(ort, text, null));
        bool Hat(string feld) => o[feld] is not null;
        switch (art)
        {
            case "rohr":
                if (!Hat("dn") && !Hat("d")) Hinweis("Rohranschluss ohne dn (Nennweite) bzw. d (Aussendurchmesser, nur Formteile einer Abwasser-Serie nach Aussendurchmesser) – aus der Unterlage ergänzen");
                if (Hat("dn") && Hat("d")) Hinweis("Rohranschluss mit dn und d: d gilt nur für Kanäle bzw. für Formteile einer Abwasser-Serie nach Aussendurchmesser (dann nur d, eine DN des Herstellers als Typparameter) – für die übrigen Rohre zählt dn");
                if (Hat("b") || Hat("h")) Hinweis("Rohranschluss: b und h gelten nur für Kanäle, für Rohre zählt dn bzw. bei Abwasser-Serien d");
                break;
            case "kanal":
                if (Hat("dn")) Hinweis("Kanalanschluss: dn gilt nur für Rohre, für Kanäle zählen d bzw. b und h");
                if (form == "rund" && !Hat("d")) Hinweis("Runder Kanalanschluss ohne d (Durchmesser in mm) – aus der Unterlage ergänzen");
                else if (form == "rechteck" && (!Hat("b") || !Hat("h"))) Hinweis("Rechteckiger Kanalanschluss ohne b und h (mm) – aus der Unterlage ergänzen");
                else if (form is null) Hinweis("Kanalanschluss ohne form (rund oder rechteck)");
                if (form == "rund" && (Hat("b") || Hat("h"))) Hinweis("Runder Kanalanschluss: b und h gelten nur für rechteckige Kanäle");
                if (form == "rechteck" && Hat("d")) Hinweis("Rechteckiger Kanalanschluss: d gilt nur für runde Kanäle");
                break;
        }
    }

    // ------------------------------------------------------------------ Masse, Sinnbild

    void MasseLesen(JsonNode? n)
    {
        if (n is null) return;
        if (n is not JsonArray a) { Fehl("masse", "masse muss eine Liste sein"); return; }
        int nr = 0;
        foreach (var x in a)
        {
            nr++;
            if (x is not JsonObject o) { Fehl($"masse[{nr}]", "Mass muss ein Objekt sein"); continue; }
            var name = Text(o["name"]) ?? "";
            var ort = name.Length > 0 ? $"mass «{name}»" : $"masse[{nr}]";
            if (name.Length == 0) Fehl(ort, "Name fehlt", "Buchstabe wie in der Unterlage, z. B. L, H, ØD.");
            var ansicht = Text(o["ansicht"]);
            if (ansicht is not null && !Ansichten.Contains(ansicht)) Fehl(ort + ".ansicht", $"ansicht «{ansicht}» gibt es nicht", string.Join(", ", Ansichten));
            var richtung = Text(o["richtung"]);
            if (richtung is not null and not ("x" or "y" or "z" or "abstand")) Fehl(ort + ".richtung", $"richtung «{richtung}» gibt es nicht", "x, y, z oder abstand");
            var par = Text(o["parameter"]);
            if (par is not null && !NachName.ContainsKey(par)) Fehl(ort + ".parameter", $"Parameter «{par}» gibt es nicht", Hlks.Kern.Formel.NamensVorschlag(par, NachName.Keys));
            Masse.Add(new FamMass
            {
                Name = name, Ansicht = ansicht, Richtung = richtung, Parameter = par,
                Von = o["von"] is null ? null : Punkt(o["von"], ort + ".von", 3), Bis = o["bis"] is null ? null : Punkt(o["bis"], ort + ".bis", 3),
            });
        }
    }

    List<FamZeichnung>? ZeichnungLesen(JsonNode? n, string feld)
    {
        if (n is null) return null;
        if (n is not JsonArray a) { Fehl(feld, $"{feld} muss eine Liste sein"); return null; }
        var l = new List<FamZeichnung>();
        int nr = 0;
        foreach (var x in a)
        {
            nr++;
            var ort = $"{feld}[{nr}]";
            if (x is not JsonObject o) { Fehl(ort, "Eintrag muss ein Objekt sein"); continue; }
            var art = Text(o["art"]) ?? "";
            if (!Zeichnungsarten.Contains(art)) { Fehl(ort + ".art", $"art «{art}» gibt es nicht", "Erlaubt: " + string.Join(", ", Zeichnungsarten)); continue; }
            int min = art switch { "linie" => 2, "polylinie" => 2, "polygon" => 3, _ => 1 };
            var punkte = Kontur(o["punkte"], ort + ".punkte", min) ?? [];
            string? stift = Stifte.Duenn; string linie = "voll"; string? fuellung = null;
            if (o["stil"] is JsonObject st)
            {
                if (st.ContainsKey("stift"))
                {
                    stift = Text(st["stift"]);
                    if (stift is not null and not (Stifte.Duenn or Stifte.Mittel or Stifte.Dick)) Fehl(ort + ".stil.stift", $"stift «{stift}» gibt es nicht", "duenn, mittel, dick oder null (keine Kontur)");
                }
                linie = Text(st["linie"]) ?? "voll";
                if (linie is not ("voll" or "strich" or "strichpunkt" or "punkt")) Fehl(ort + ".stil.linie", $"linie «{linie}» gibt es nicht", "voll, strich, strichpunkt, punkt");
                fuellung = Text(st["fuellung"]);
                if (fuellung is not null and not ("schnitt" or "papier") && !Regex.IsMatch(fuellung, "^#[0-9a-fA-F]{6}$"))
                    Fehl(ort + ".stil.fuellung", $"fuellung «{fuellung}» gibt es nicht", "null, schnitt, papier oder #rrggbb");
            }
            var text = Text(o["text"]);
            if (art == "text" && string.IsNullOrEmpty(text)) Fehl(ort + ".text", "Text fehlt");
            if (art is "kreis" or "bogen" && o["radius"] is null) Fehl(ort + ".radius", "radius fehlt");
            if (art == "bogen" && (o["von"] is null || o["bis"] is null)) Fehl(ort, "Bogen braucht von und bis (Winkel in Grad)");
            l.Add(new FamZeichnung
            {
                Art = art, Punkte = punkte, Text = text, Groesse = Json.Zahl(o["groesse"]),
                Radius = o["radius"] is null ? null : Zahl(o["radius"], ort + ".radius"),
                Von = o["von"] is null ? null : Zahl(o["von"], ort + ".von"), Bis = o["bis"] is null ? null : Zahl(o["bis"], ort + ".bis"),
                Detailgrad = DetailgradLesen(o["detailgrad"], ort + ".detailgrad"),
                Sichtbar = o["sichtbar"] is null ? null : Zahl(o["sichtbar"], ort + ".sichtbar"),
                Stift = stift, Linie = linie, Fuellung = fuellung,
            });
        }
        return l;
    }

    /// <summary>Form der Befund-Ids (Inhalt prüft B4 gegen die Befunde).</summary>
    void QuellenPruefen()
    {
        void Liste(JsonNode? n, string ort)
        {
            if (n is null) return;
            if (n is not JsonArray a) { Fehl(ort, "quellen muss eine Liste von Befund-Ids sein", "[\"<prüfsumme 12>#<n>\"]"); return; }
            foreach (var q in a)
                if (Text(q) is not { } s || !BefundMuster.IsMatch(s))
                    Fehl(ort, $"Befund-Id «{q?.ToJsonString()}» hat nicht die Form «<prüfsumme 12>#<n>»", "Ids aus unterlagen_lesen übernehmen.");
        }
        int i = 0;
        foreach (var x in Quelle["parameter"] as JsonArray ?? []) { i++; if (x is JsonObject o) Liste(o["quellen"], $"parameter[{i}].quellen"); }
        i = 0;
        foreach (var x in Quelle["typen"] as JsonArray ?? [])
        {
            i++;
            if (x is JsonObject o && o["quellen"] is JsonObject q) foreach (var (pn, l) in q) Liste(l, $"typen[{i}].quellen.{pn}");
            if (x is JsonObject oe && oe["entfaellt"] is JsonObject en) foreach (var (pn, l) in en) Liste(l, $"typen[{i}].entfaellt.{pn}");
        }
        i = 0;
        foreach (var x in Quelle["koerper"] as JsonArray ?? []) { i++; if (x is JsonObject o) Liste(o["quellen"], $"koerper[{i}].quellen"); }
        i = 0;
        foreach (var x in Quelle["anschluesse"] as JsonArray ?? []) { i++; if (x is JsonObject o) Liste(o["quellen"], $"anschluesse[{i}].quellen"); }
        i = 0;
        foreach (var x in Quelle["masse"] as JsonArray ?? []) { i++; if (x is JsonObject o) Liste(o["quellen"], $"masse[{i}].quellen"); }
        if (Quelle["leitung"] is JsonObject lo)
        {
            Liste(lo["quellen"], "leitung.quellen");   // Modul 3
            if (lo["frei"] is JsonObject fo) Liste(fo["quellen"], "leitung.frei.quellen");   // Nachtrag freie Grösse
        }
    }

    // ------------------------------------------------------------------ Formeln: Namen, Typregel, Zyklen

    void FormelnPruefen()
    {
        foreach (var p in Parameter.Where(p => p.Formel is not null))
            foreach (var (name, stelle) in p.Formel!.Namen())
            {
                var ort = $"parameter «{p.Name}».formel";
                if (!NachName.TryGetValue(name, out var q))
                    Fehl(ort, $"Formel «{p.FormelText}»: Stelle {stelle}: unbekannter Name «{name}»", Hlks.Kern.Formel.NamensVorschlag(name, NachName.Keys));
                else if (p.Art == "typ" && q.Art == "exemplar")
                    Fehl(ort, $"Formel «{p.FormelText}»: Stelle {stelle}: der Typparameter «{p.Name}» verwendet den Exemplarparameter «{name}»",
                        $"Formeln von Typparametern dürfen nur Typparameter verwenden (wie Revit): «{p.Name}» zum Exemplarparameter machen oder «{name}» zum Typparameter.");
            }
        foreach (var z in alleZahlen.Where(z => z.Formel is not null))
            foreach (var (name, stelle) in z.Formel!.Namen())
                if (!NachName.ContainsKey(name))
                    Fehl(z.Ort, $"Formel «{z.Text}»: Stelle {stelle}: unbekannter Name «{name}»", Hlks.Kern.Formel.NamensVorschlag(name, NachName.Keys));

        // Zyklen unter den Formelparametern (Tiefensuche, Pfad im Fehler)
        var zustand = new Dictionary<string, int>();   // 1 = in Arbeit, 2 = fertig
        var pfad = new List<string>();
        var gemeldet = new HashSet<string>();
        void Besuchen(FamParameter p)
        {
            zustand[p.Name] = 1;
            pfad.Add(p.Name);
            foreach (var (name, _) in p.Formel!.Namen())
            {
                if (!NachName.TryGetValue(name, out var q) || q.Formel is null) continue;
                var z = zustand.GetValueOrDefault(name);
                if (z == 1)
                {
                    var kreis = pfad.Skip(pfad.IndexOf(name)).Append(name).ToList();
                    if (gemeldet.Add(string.Join(">", kreis.Order())))
                        Fehl($"parameter «{name}».formel", $"Zirkuläre Formel: {string.Join(" → ", kreis)}", "Eine der Formeln durch einen Wert ersetzen oder anders herleiten.");
                }
                else if (z == 0) Besuchen(q);
            }
            pfad.RemoveAt(pfad.Count - 1);
            zustand[p.Name] = 2;
        }
        foreach (var p in Parameter.Where(p => p.Formel is not null))
            if (zustand.GetValueOrDefault(p.Name) == 0) Besuchen(p);
    }
}
