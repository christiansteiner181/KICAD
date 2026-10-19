// Abbildung HKAP ↔ KICAD (Modul 1d): welches HKAP-Feld in KICAD wo steht, wie es gelesen, übernommen und gesendet
// wird. Zuordnung der Elemente über Element.Kennungen["hkap"]: «PJ-…/E1» (Geschoss), «PJ-…/E1-S3» (Raum) – HKAP-Ids
// sind nur zusammen mit der Projektierung eindeutig. Namen sind nie Schlüssel (Ausnahme siehe HkapAbgleich: Geschosse
// beim ersten Abgleich über einen eindeutigen Namen).
//
//   projekt    → Projektinformation: zeile1 = bezeichnung, zeile2 = «adresse, plz ort, kunde», projektnummer = kuerzel
//                (sonst id), phase = phase. Nur HKAP → KICAD.
//   geschosse  → Geschoss: name ↔ Name (sonst Kennzeichen), praefix ↔ kurz, koteMm ↔ kote; reihenfolge nur lesen.
//   raeume     → Raum: etage ↔ Geschoss (über dessen Kennung), raumNummer ↔ nummer, bezeichnung ↔ Name, nutzung ↔ nutzung,
//                kicad { id, flaecheM2 (aus dem Polygon), hoeheMm (lichteHoehe › Geometriehöhe › Geschosshöhe) } → HKAP,
//                erst wenn der Raum gezeichnet ist.
//                Anforderungen (personen, zuluft, abluft, heizleistung, kuehlleistung, tempWinter, tempSommer, co2Max) ↔
//                gleichnamige Raum-Eigenschaften (tempWinter nicht auf solltemperatur, Entscheid Christian 18.10.2026):
//                HKAP führt, nur HKAP → KICAD, Einheit aus dem KICAD-Katalog und nur bei gleicher Einheit wie im
//                HKAP-Katalog. strangIds (Modul 3) und flaecheHkapM2 nur lesen und anzeigen.
//   straenge   → Gruppe von KICAD-Strängen mit derselben Kennung «hkap» (Modul 3, B5; siehe HkapStraenge.cs): bezeichnung ↔ Name,
//                medium ← Systemtyp (nur senden), kicad { straenge: [...] } → HKAP; nummer, tempVL, tempRL, anschluss,
//                anschlussEtageId, etagen nur lesen.

using System.Globalization;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <summary>Verknüpfung des KICAD-Projekts mit HKAP (Projektinformation.hkap).</summary>
public sealed record HkapVerknuepfung(string Projekt, string? Projektierung, string? Kuerzel, string? Name, string? Url)
{
    public static Element? Info(Modell m) => m.DerKlasse("Projektinformation").OrderBy(e => e.Kennzeichen, StringComparer.Ordinal).FirstOrDefault();

    public static HkapVerknuepfung? Lesen(Modell m)
    {
        var o = Info(m)?.Eigenschaft("hkap")?.Inhalt as JsonObject;
        var projekt = HkapWerte.Leer(o?["projekt"]);
        if (projekt is null) return null;
        return new(projekt, HkapWerte.Leer(o!["projektierung"]), HkapWerte.Leer(o["kuerzel"]), HkapWerte.Leer(o["name"]), HkapWerte.Leer(o["url"]));
    }

    public static HkapVerknuepfung Pflicht(Modell m) => Lesen(m) ?? throw new HlksFehler("Das Projekt ist nicht mit HKAP verknüpft.",
        "Zuerst hkap_verknuepfen { projekt: \"P-…\", projektierung: \"PJ-…\" } aufrufen; hkap_verbindung listet die HKAP-Projekte.");

    public string PjPflicht => Projektierung ?? throw new HlksFehler("Mit dem Projekt ist keine HKAP-Projektierung verknüpft (Geschosse und Räume brauchen eine).",
        "hkap_verknuepfen mit projektierung (PJ-…) erneut aufrufen; hkap_verbindung mit projekt listet die Projektierungen.");

    /// <summary>Kennung eines Geschosses oder Raums: «PJ-…/E1-S3».</summary>
    public string Kennung(string id) => $"{Projektierung}/{id}";

    /// <summary>HKAP-Id aus der Kennung eines Elements, wenn sie zur verknüpften Projektierung gehört.</summary>
    public string? IdVon(Element e)
    {
        var k = e.Kennungen?.GetValueOrDefault("hkap");
        if (k is null || Projektierung is null || !k.StartsWith(Projektierung + "/", StringComparison.OrdinalIgnoreCase)) return null;
        var id = k[(Projektierung.Length + 1)..];
        return id.Length > 0 ? id : null;
    }

    /// <summary>Herkunft eines übernommenen Werts: «HKAP P-005/PJ-2026-003/E1-S3, Feld bezeichnung, Stand 3f…».</summary>
    public string Quelle(string datenart, string id, string feld, string? version) =>
        $"HKAP {Projekt}{(datenart == "projekt" ? "" : $"/{Projektierung}/{id}")}, Feld {feld}, Stand {version ?? "?"}";

    public JsonObject AlsJson() => new()
    {
        ["projekt"] = Projekt, ["projektierung"] = Projektierung, ["kuerzel"] = Kuerzel, ["name"] = Name, ["url"] = Url,
    };
}

/// <summary>Ein Feld im Abgleich: Name (HKAP-Pfad bzw. KICAD-Eigenschaft beim Projekt), abhängige HKAP-Pfade, Lesen in
/// KICAD, Übernehmen nach KICAD, Senden nach HKAP.</summary>
public sealed class HkapFeld
{
    public required string Name { get; init; }
    public required string[] HkapPfade { get; init; }
    /// <summary>Wert aus den flachen HKAP-Feldern; Vorgabe: Pfad = Name.</summary>
    public Func<JsonObject, JsonNode?>? AusHkap { get; init; }
    /// <summary>Wert in KICAD; null = KICAD hat dafür (noch) keinen Ort → nur lesen.</summary>
    public Func<Modell, Element, JsonNode?>? KicadLesen { get; init; }
    /// <summary>Wert in ein Element übernehmen; null = in KICAD nicht übernehmbar (abgeleitet oder ohne Ort).</summary>
    public Action<Kontext, Element, JsonNode?, Herkunft>? KicadSchreiben { get; init; }
    /// <summary>Wie KicadLesen für einen HKAP-Datensatz, der mehrere KICAD-Elemente bündelt (Stränge, Modul 3): Wert der Gruppe.</summary>
    public Func<Modell, IReadOnlyList<Element>, JsonNode?>? GruppeLesen { get; init; }
    /// <summary>Wie KicadSchreiben für eine Gruppe (die Elemente sind Kopien des Aufrufers, er setzt sie danach).</summary>
    public Action<Kontext, IReadOnlyList<Element>, JsonNode?, Herkunft>? GruppeSchreiben { get; init; }
    /// <summary>Wer zuerst setzt, gewinnt, und KICAD leitet den Wert nur ab (Medium): ein Wert, den HKAP schon hat oder geändert hat,
    /// wird nie überschrieben (Abweichung statt Senden).</summary>
    public bool HkapWertBleibt { get; init; }
    /// <summary>Der KICAD-Wert kann unter dem Pfad Name nach HKAP gesendet werden.</summary>
    public bool Sendbar { get; init; }
    /// <summary>Einheit in KICAD (aus dem Katalog). Nennt der HKAP-Katalog eine andere, fliesst der Wert nicht.</summary>
    public string? Einheit { get; init; }

    /// <summary>Einheit verträglich: eine Seite ohne Angabe oder beide gleich.</summary>
    public bool EinheitPasst(HkapDatenartDef? def) =>
        Einheit is null || Katalogfelder(def).All(x => string.IsNullOrWhiteSpace(x.Einheit) || x.Einheit.Trim().Equals(Einheit, StringComparison.OrdinalIgnoreCase));

    /// <summary>KICAD kann das Feld lesen (Element oder Gruppe).</summary>
    public bool Liest => KicadLesen is not null || GruppeLesen is not null;
    /// <summary>KICAD kann das Feld übernehmen (Element oder Gruppe).</summary>
    public bool Schreibt => KicadSchreiben is not null || GruppeSchreiben is not null;

    /// <summary>KICAD-Wert zum Paar: Wert der Gruppe (Stränge) oder des Elements; null ohne beides.</summary>
    public JsonNode? KicadWert(Modell m, HkapPaar paar) =>
        paar.Gruppe.Count > 0 && GruppeLesen is not null ? GruppeLesen(m, paar.Gruppe)
        : KicadLesen is not null && paar.Element is not null ? KicadLesen(m, paar.Element) : null;

    public JsonNode? Hkap(JsonObject flach) => AusHkap is not null ? AusHkap(flach) : flach.TryGetPropertyValue(Name, out var v) ? v : null;
    public bool BasisBekannt(JsonObject? basis) => basis is not null && HkapPfade.All(basis.ContainsKey);

    /// <summary>Senden erlaubt: der Katalog lässt KICAD → HKAP zu und HKAP führt nicht. neu = Datensatz anlegen (dann
    /// zählt nur die Richtung).</summary>
    public bool DarfSenden(HkapDatenartDef? def, bool neu = false) =>
        Sendbar && EinheitPasst(def) && Katalogfelder(def) is { Count: > 0 } f && f.All(x => x.NachHkap && (neu || x.Fuehrt != "hkap"));

    /// <summary>Übernehmen erlaubt: der Katalog lässt HKAP → KICAD zu, KICAD führt nicht, die Einheit passt.</summary>
    public bool DarfUebernehmen(HkapDatenartDef? def) =>
        Schreibt && EinheitPasst(def) && Katalogfelder(def) is { Count: > 0 } f && f.All(x => x.NachKicad && x.Fuehrt != "kicad");

    /// <summary>Katalogfelder der HKAP-Pfade (ein abgeleitetes Feld wie projektnummer = kuerzel sonst id zählt die, die
    /// der Katalog kennt). Leer = der Katalog kennt das Feld nicht → fliesst in keine Richtung.</summary>
    List<HkapFeldDef> Katalogfelder(HkapDatenartDef? def) =>
        def is null ? [] : HkapPfade.Select(def.Feld).Where(f => f is not null).Select(f => f!).ToList();
}

public static class HkapAbbildung
{
    public static readonly string[] Datenarten = ["projekt", "geschosse", "raeume", "straenge", "pendenzen"];
    /// <summary>Raumanforderungen aus HKAP: gleichnamige Raum-Eigenschaften in KICAD, HKAP führt (nur HKAP → KICAD).</summary>
    public static readonly string[] Anforderungen = ["personen", "zuluft", "abluft", "heizleistung", "kuehlleistung", "tempWinter", "tempSommer", "co2Max"];

    /// <param name="katalog">KICAD-Katalog: Anforderungen nur, wenn die Raumklasse die Eigenschaft hat (Einheit von dort).</param>
    /// <param name="projekt">Projekt für die Ableitungen der Stränge (Leitungslängen); ohne: keine Längen im Block «kicad».</param>
    public static List<HkapFeld> Felder(string datenart, HkapVerknuepfung link, Katalog? katalog = null, Projekt? projekt = null) => datenart switch
    {
        "projekt" => Projekt(),
        "geschosse" => Geschosse(),
        "raeume" => Raeume(link, katalog),
        "straenge" => HkapStraenge.Felder(link, katalog, projekt),
        _ => [],
    };

    // ---------------------------------------------------------------- Hilfen

    static JsonNode? T(string? s) => string.IsNullOrWhiteSpace(s) ? null : JsonValue.Create(s);
    static JsonNode? Z(double? d) => d is double x ? JsonValue.Create(x) : null;

    /// <summary>Eigenschaft setzen (null entfernt); Text als Text, Zahl als Zahl.</summary>
    public static void Eigenschaft(Element e, string name, JsonNode? wert, Herkunft h, string typ = "text", string? einheit = null)
    {
        if (HkapWerte.Normal(wert) is null) { e.Eigenschaften.Remove(name); return; }
        JsonNode inhalt = typ == "zahl"
            ? JsonValue.Create(Json.Zahl(wert) ?? throw new HlksFehler($"Wert «{HkapWerte.Text(wert)}» für {name} ist keine Zahl."))
            : JsonValue.Create(HkapWerte.Text(wert)!.Trim());
        e.Eigenschaften[name] = new Wert { Inhalt = inhalt, Einheit = einheit, Herkunft = h };
    }

    public static Element? GeschossMitId(Modell m, HkapVerknuepfung link, string? etage) =>
        etage is null ? null : m.DerKlasse("Geschoss").FirstOrDefault(g => string.Equals(link.IdVon(g), etage, StringComparison.OrdinalIgnoreCase));

    /// <summary>Fläche in m² (2 Stellen) aus dem Raumpolygon; null ohne Polygon (nicht platzierter Raum).</summary>
    public static double? Flaeche(Element r) =>
        r.Geometrie is { Art: "polygon", Punkte.Count: >= 3 } g ? Math.Round(g.Flaeche() / 1e6, 2) : null;

    /// <summary>Raumhöhe in mm wie die Raumliste: lichteHoehe › Geometriehöhe › Geschosshöhe; ohne Polygon nur lichteHoehe.</summary>
    public static double? Hoehe(Modell m, Element r)
    {
        double? h = r.Zahl("lichteHoehe");
        if (h is null && r.Geometrie is { Art: "polygon", Punkte.Count: >= 3 }) h = r.Geometrie.Hoehe ?? m.Finde(r.Geschoss)?.Zahl("hoehe");
        return h is double x ? Math.Round(x, 0) : null;
    }

    // ---------------------------------------------------------------- Datenarten

    static List<HkapFeld> Projekt()
    {
        static JsonNode? Zeile2(JsonObject f)
        {
            var ort = string.Join(" ", new[] { HkapWerte.Leer(f["plz"]), HkapWerte.Leer(f["ort"]) }.Where(x => x is not null));
            var teile = new[] { HkapWerte.Leer(f["adresse"]), ort.Length > 0 ? ort : null, HkapWerte.Leer(f["kunde"]) }.Where(x => x is not null);
            return T(string.Join(", ", teile));
        }
        HkapFeld Info(string name, string[] pfade, Func<JsonObject, JsonNode?> aus) => new()
        {
            Name = name, HkapPfade = pfade, AusHkap = aus,
            KicadLesen = (_, e) => T(e.Text(name)),
            KicadSchreiben = (_, e, w, h) => Eigenschaft(e, name, w, h),
        };
        return
        [
            Info("zeile1", ["bezeichnung"], f => T(HkapWerte.Leer(f["bezeichnung"]))),
            Info("zeile2", ["adresse", "plz", "ort", "kunde"], Zeile2),
            Info("projektnummer", ["kuerzel", "id"], f => T(HkapWerte.Leer(f["kuerzel"]) ?? HkapWerte.Leer(f["id"]))),
            Info("phase", ["phase"], f => T(HkapWerte.Leer(f["phase"]))),
        ];
    }

    static List<HkapFeld> Geschosse() =>
    [
        new() { Name = "name", HkapPfade = ["name"], Sendbar = true,
                KicadLesen = (_, e) => T(e.Name ?? e.Kennzeichen),
                KicadSchreiben = (_, e, w, _) => e.Name = HkapWerte.Leer(w) },
        new() { Name = "praefix", HkapPfade = ["praefix"], Sendbar = true,
                KicadLesen = (_, e) => T(e.Text("kurz")),
                KicadSchreiben = (_, e, w, h) => Eigenschaft(e, "kurz", w, h) },
        new() { Name = "koteMm", HkapPfade = ["koteMm"], Sendbar = true,
                KicadLesen = (_, e) => Z(e.Zahl("kote")),
                KicadSchreiben = (_, e, w, h) => { if (HkapWerte.Normal(w) is not null) Eigenschaft(e, "kote", w, h, "zahl", "mm"); } },
        new() { Name = "reihenfolge", HkapPfade = ["reihenfolge"] },
    ];

    static List<HkapFeld> Raeume(HkapVerknuepfung link, Katalog? katalog)
    {
        var raumKlasse = katalog is not null && katalog.Klassen.TryGetValue("Raum", out var rk) ? rk : null;
        var felder = new List<HkapFeld>
        {
            new() { Name = "etage", HkapPfade = ["etage"], Sendbar = true,
                    KicadLesen = (m, e) => T(m.Finde(e.Geschoss) is { } g ? link.IdVon(g) : null),
                    KicadSchreiben = (k, e, w, _) =>
                    {
                        var etage = HkapWerte.Leer(w);
                        if (etage is null) return;
                        if (GeschossMitId(k.Arbeit, link, etage) is { } g) e.Geschoss = g.Id;
                        else k.Hinweise.Add($"{e.Kennzeichen}: Etage {etage} hat in KICAD kein zugeordnetes Geschoss – Geschoss bleibt (Geschosse zuerst abgleichen).");
                    } },
            new() { Name = "raumNummer", HkapPfade = ["raumNummer"], Sendbar = true,
                    KicadLesen = (_, e) => T(e.Text("nummer")),
                    KicadSchreiben = (_, e, w, h) => Eigenschaft(e, "nummer", w, h) },
            new() { Name = "bezeichnung", HkapPfade = ["bezeichnung"], Sendbar = true,
                    KicadLesen = (_, e) => T(e.Name),
                    KicadSchreiben = (_, e, w, _) => e.Name = HkapWerte.Leer(w) },
            new() { Name = "nutzung", HkapPfade = ["nutzung"], Sendbar = true,
                    KicadLesen = (_, e) => T(e.Text("nutzung")),
                    KicadSchreiben = (_, e, w, h) => Eigenschaft(e, "nutzung", w, h) },
            // kicad { … } erst, wenn der Raum gezeichnet ist: ein nicht platzierter Raum hat in KICAD noch keine Fläche
            new() { Name = "kicad.id", HkapPfade = ["kicad.id"], Sendbar = true, KicadLesen = (_, e) => T(Flaeche(e) is null ? null : e.Id) },
            new() { Name = "kicad.flaecheM2", HkapPfade = ["kicad.flaecheM2"], Sendbar = true, KicadLesen = (_, e) => Z(Flaeche(e)) },
            new() { Name = "kicad.hoeheMm", HkapPfade = ["kicad.hoeheMm"], Sendbar = true, KicadLesen = (m, e) => Z(Hoehe(m, e)) },
        };
        foreach (var a in Anforderungen)
        {
            // HKAP führt, Einbahn HKAP → KICAD; ohne Katalogeigenschaft (älterer Katalog) nur lesen
            var def = raumKlasse?.Eigenschaften.FirstOrDefault(x => x.Name == a);
            felder.Add(def is null ? new HkapFeld { Name = a, HkapPfade = [a] } : new HkapFeld
            {
                Name = a, HkapPfade = [a], Einheit = def.Einheit,
                KicadLesen = (_, e) => Z(e.Zahl(a)),
                KicadSchreiben = (_, e, w, h) => Eigenschaft(e, a, w, h, "zahl", def.Einheit),
            });
        }
        felder.Add(new HkapFeld { Name = "strangIds", HkapPfade = ["strangIds"] });
        felder.Add(new HkapFeld { Name = "flaecheHkapM2", HkapPfade = ["flaecheHkapM2"] });   // Fläche aus dem HKAP-Plan, nur lesen (die Vorschau zeigt sie neben der KICAD-Fläche)
        return felder;
    }

    /// <summary>Text eines Raums für Meldungen und Pendenzen: «1.02 Bad» (sonst Kennzeichen).</summary>
    public static string RaumText(Element r)
    {
        var t = string.Join(" ", new[] { r.Text("nummer"), r.Name }.Where(x => !string.IsNullOrWhiteSpace(x)));
        return t.Length > 0 ? t : r.Kennzeichen;
    }

    public static string Zahltext(double d) => d.ToString("0.##", CultureInfo.InvariantCulture);
}
