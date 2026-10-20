// Kollisionsprüfung (Modul 3b, B2): Projektvorgaben (Grundsatzentscheid 5, entwurf/modul-3b.md) – das eine Element der Klasse
// Vorgaben (Kennzeichen VG-1) mit Werten MIT Quelle (z. B. «Bürostandard H+K 2026»). Keine eingebauten Werte: was fehlt, fehlt
// – die Prüfung nennt es und lässt den betroffenen Teil aus (Auftrag B2: weiche Prüfung je Paar übersprungen, ohne toleranz_hart
// jede Durchdringung gemeldet).
//
// Befehl vorgaben_setzen { werte: { name: { wert, quelle, einheit? } | null } }: legt VG-1 an bzw. ändert es. Jeder Wert braucht
// eine Quelle; fehlt sie bei einem, wird der ganze Befehl abgelehnt und alle fehlerhaften Werte werden genannt. null (oder
// { wert: null }) entfernt einen Wert. Zahlen in mm, ≥ 0.
// abstand_weich ist ein Objekt { "<a>-<b>": mm } je Gewerkpaar (eine Quelle für das ganze Objekt). a, b: Gewerke aus dem Katalog
// (Systemtypen und Klassen: sanitaer, heizung, lueftung, msrl …; Kälte gehört zu heizung, «kaelte» wird als heizung gelesen), «architektur», «fremd» (Produkte fremder IFC-Dateien,
// Paar hlks-fremd) oder «hlks» (jedes HLKS-Gewerk). Die Reihenfolge der beiden Seiten zählt nicht. Gesucht wird für ein Paar
// zuerst der genaue Schlüssel, dann mit «hlks» an Stelle des HLKS-Gewerks (siehe Projektvorgaben.AbstandWeichFuer).
// Abfrage vorgaben: Werte mit Quelle und Herkunft, fehlende Werte mit ihrer Wirkung, Werte ohne Quelle (über element_aendern
// gesetzt: sie gelten nicht, wie bei den Hüllenkennwerten – P12).
// Nachtrag B3b (Entscheid Christian 19.10.2026, Kernbohrungen nur von Hand): grenze_kernbohrung verlangt kein Befehl mehr. Sie bleibt
// im Katalog und lesbar; vorgaben_setzen nimmt sie mit Hinweis an, die Abfrage führt sie nicht unter «fehlend», sondern unter
// «nichtMehrVerwendet» und bei gesetztem Wert mit verwendet = false und Hinweis «nicht mehr verwendet (Entscheid Christian 19.10.2026)».
// Nachtrag B3c (Entscheid Christian 19.10.2026, keine Abstandsprüfung): abstand_weich und abstand_leitungen sind optional – die Abfrage
// führt sie nicht unter «fehlend», sondern unter «optional» mit «Abstandsprüfung aus (Entscheid Christian 19.10.2026); setzbar, falls
// gewünscht», «Noch ohne Wert» nennt sie nicht. Gesetzt, prüft kollision_pruefen weich wie bisher.

using System.Globalization;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <summary>Eine Projektvorgabe mit ihrer Quelle.</summary>
public sealed record Vorgabe(string Name, JsonNode Wert, string? Einheit, string Quelle, Herkunft Herkunft)
{
    public double? Zahl => Json.Zahl(Wert);
}

/// <summary>Die Projektvorgaben des Modells (Element der Klasse Vorgaben), nur Werte mit Quelle.</summary>
public sealed class Projektvorgaben
{
    public const string Klasse = "Vorgaben";
    public const string Kuerzel = "VG";
    public const string SpielAussparung = "spiel_aussparung";
    public const string AbstandWeich = "abstand_weich";
    public const string GrenzeKernbohrung = "grenze_kernbohrung";
    public const string AbstandZusammenfassen = "abstand_zusammenfassen";
    public const string AbstandLeitungen = "abstand_leitungen";
    public const string ToleranzHart = "toleranz_hart";

    /// <summary>Seite «jedes HLKS-Gewerk» in abstand_weich.</summary>
    public const string SeiteHlks = "hlks";
    /// <summary>Seite Architektur/Tragwerk in abstand_weich.</summary>
    public const string SeiteArchitektur = "architektur";
    /// <summary>Seite Produkte fremder IFC-Dateien (Paar hlks-fremd) in abstand_weich.</summary>
    public const string SeiteFremd = "fremd";

    /// <summary>Was geschieht, wenn eine Vorgabe fehlt (für die Abfrage vorgaben und die Hinweise der Prüfung).</summary>
    public static readonly IReadOnlyDictionary<string, string> Wirkung = new Dictionary<string, string>
    {
        [ToleranzHart] = "kollision_pruefen meldet jede Durchdringung (auch aufliegende Leitungen) als harte Kollision",
        [AbstandWeich] = "keine – Abstandsprüfung aus (Entscheid Christian 19.10.2026); gesetzt, prüft kollision_pruefen weiche Kollisionen je Gewerkpaar",
        [AbstandLeitungen] = "keine – Abstandsprüfung aus (Entscheid Christian 19.10.2026); gesetzt, prüft kollision_pruefen weiche Kollisionen Leitung–Leitung",
        [SpielAussparung] = "nötig für Aussparungsvorschläge (aussparungen_vorschlagen) und die Wahl der Form (aussparung_form)",
        [GrenzeKernbohrung] = "keine – nicht mehr verwendet (Entscheid Christian 19.10.2026): Kernbohrungen nur von Hand mit aussparung_form",
        [AbstandZusammenfassen] = "nötig zum Zusammenfassen benachbarter Aussparungen",
    };

    /// <summary>Vorgaben, die kein Befehl mehr verlangt (Nachtrag B3b). Sie bleiben im Katalog, damit alte Projekte lesbar bleiben;
    /// vorgaben_setzen nimmt sie mit Hinweis an, die Abfrage vorgaben nennt sie «nicht mehr verwendet».</summary>
    public static readonly IReadOnlySet<string> NichtMehrVerwendet = new HashSet<string>(StringComparer.Ordinal) { GrenzeKernbohrung };

    /// <summary>Kennzeichnung einer nicht mehr verwendeten Vorgabe.</summary>
    public const string NichtMehrVerwendetText = "nicht mehr verwendet (Entscheid Christian 19.10.2026)";

    /// <summary>Optionale Vorgaben (Nachtrag B3c, keine Abstandsprüfung): fehlen nicht, sind aber setzbar.</summary>
    public static readonly IReadOnlySet<string> Optional = new HashSet<string>(StringComparer.Ordinal) { AbstandWeich, AbstandLeitungen };

    /// <summary>Text zu den optionalen Vorgaben (Abfrage vorgaben).</summary>
    public const string OptionalText = "Abstandsprüfung aus (Entscheid Christian 19.10.2026); setzbar, falls gewünscht";

    /// <summary>Fehlt diese Vorgabe, wenn sie nicht gesetzt ist (nicht optional, nicht mehr verwendet)?</summary>
    public static bool Verlangt(string name) => !Optional.Contains(name) && !NichtMehrVerwendet.Contains(name);

    /// <summary>Grund je nicht mehr verwendeter Vorgabe.</summary>
    public static string NichtMehrVerwendetGrund(string name) => name == GrenzeKernbohrung
        ? $"{NichtMehrVerwendetText}: aussparungen_vorschlagen schlägt immer rechteckige Aussparungen vor, Kernbohrungen nur von Hand mit aussparung_form"
        : NichtMehrVerwendetText;

    public Element? Element { get; }
    /// <summary>Werte mit Quelle (Name → Vorgabe).</summary>
    public IReadOnlyDictionary<string, Vorgabe> Werte { get; }
    /// <summary>Werte ohne Quelle (gelten nicht).</summary>
    public IReadOnlyList<string> OhneQuelle { get; }
    /// <summary>Weitere Elemente der Klasse Vorgaben (sollte es nicht geben; z. B. auf zwei Rechnern gleichzeitig angelegt).</summary>
    public IReadOnlyList<Element> Weitere { get; }
    /// <summary>abstand_weich mit geordneten Schlüsseln (siehe <see cref="Schluessel"/>), nur gültige Zahlen.</summary>
    public IReadOnlyDictionary<string, double> AbstaendeWeich { get; }

    Projektvorgaben(Element? e, Dictionary<string, Vorgabe> werte, List<string> ohne, List<Element> weitere, Dictionary<string, double> weich)
    {
        Element = e; Werte = werte; OhneQuelle = ohne; Weitere = weitere; AbstaendeWeich = weich;
    }

    /// <summary>Das Element der Projektvorgaben: VG-1, sonst das erste der Klasse Vorgaben nach Kennzeichen (dieselbe Regel wie
    /// AussparungVorgaben.VorgabenElement in Teil B3); null, wenn es keines gibt.</summary>
    public static Element? ElementVon(Modell m) => Alle(m).FirstOrDefault();

    static List<Element> Alle(Modell m) => m.DerKlasse(Klasse)
        .OrderBy(e => e.Kennzeichen.Equals($"{Kuerzel}-1", StringComparison.OrdinalIgnoreCase) ? 0 : 1).ThenBy(e => e.Kennzeichen, StringComparer.Ordinal)
        .ThenBy(e => e.Id, StringComparer.Ordinal).ToList();

    public static Projektvorgaben Lesen(Modell m)
    {
        var alle = Alle(m);
        var e = alle.FirstOrDefault();
        var werte = new Dictionary<string, Vorgabe>(StringComparer.Ordinal);
        var ohne = new List<string>();
        var weich = new Dictionary<string, double>(StringComparer.Ordinal);
        if (e is not null)
            foreach (var (name, w) in e.Eigenschaften.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                if (w.Inhalt is null) continue;
                if (string.IsNullOrWhiteSpace(w.Herkunft.Quelle)) { ohne.Add(name); continue; }
                werte[name] = new Vorgabe(name, w.Inhalt, w.Einheit, w.Herkunft.Quelle!.Trim(), w.Herkunft);
                if (name == AbstandWeich && w.Inhalt is JsonObject o)
                    foreach (var (k, v) in o)
                        if (Json.Zahl(v) is double d && double.IsFinite(d) && d >= 0 && Schluessel(k) is { } s) weich[s] = d;
            }
        return new Projektvorgaben(e, werte, ohne, alle.Skip(1).ToList(), weich);
    }

    /// <summary>Zahlenwert einer Vorgabe mit Quelle (null, wenn sie fehlt).</summary>
    public double? Zahl(string name) => Werte.TryGetValue(name, out var v) && v.Zahl is double d && double.IsFinite(d) ? d : null;

    public string? Quelle(string name) => Werte.TryGetValue(name, out var v) ? v.Quelle : null;

    /// <summary>Geordneter Schlüssel eines Gewerkpaars «a-b» (klein, Seiten alphabetisch); null, wenn er nicht aus zwei Seiten besteht.</summary>
    public static string? Schluessel(string text)
    {
        var teile = text.Trim().ToLowerInvariant().Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(KaelteAlias.Gewerk).ToArray();   // «kaelte» (früher) = heizung
        if (teile.Length != 2) return null;
        return string.CompareOrdinal(teile[0], teile[1]) <= 0 ? $"{teile[0]}-{teile[1]}" : $"{teile[1]}-{teile[0]}";
    }

    /// <summary>Mindestabstand für ein Gewerkpaar aus abstand_weich: zuerst der genaue Schlüssel (a-b), dann mit «hlks» an Stelle
    /// der HLKS-Seite(n) (hlksA, hlksB sagen, welche Seite ein HLKS-Gewerk ist). Rückgabe Wert und benutzter Schlüssel, sonst
    /// null.</summary>
    public (double Wert, string Schluessel)? AbstandWeichFuer(string a, bool hlksA, string b, bool hlksB)
    {
        var kandidaten = new List<string?> { Schluessel($"{a}-{b}") };
        if (hlksA) kandidaten.Add(Schluessel($"{SeiteHlks}-{b}"));
        if (hlksB) kandidaten.Add(Schluessel($"{a}-{SeiteHlks}"));
        if (hlksA && hlksB) kandidaten.Add(Schluessel($"{SeiteHlks}-{SeiteHlks}"));
        foreach (var k in kandidaten)
            if (k is not null && AbstaendeWeich.TryGetValue(k, out var w)) return (w, k);
        return null;
    }

    /// <summary>Gültige Seiten in abstand_weich: Gewerke des Katalogs (Systemtypen und Klassen) sowie hlks, architektur, fremd.</summary>
    public static SortedSet<string> Seiten(Katalog k)
    {
        var s = new SortedSet<string>(StringComparer.Ordinal) { SeiteHlks, SeiteArchitektur, SeiteFremd };
        foreach (var t in k.Systemtypen.Values) if (!string.IsNullOrWhiteSpace(t.Gewerk)) s.Add(t.Gewerk.ToLowerInvariant());
        foreach (var d in k.Klassen.Values) if (!string.IsNullOrWhiteSpace(d.Gewerk)) s.Add(d.Gewerk.ToLowerInvariant());
        return s;
    }

    /// <summary>Gilt die Seite als HLKS-Gewerk (nicht Architektur, nicht fremd)?</summary>
    public static bool IstHlksSeite(string seite) => seite is not (SeiteArchitektur or SeiteFremd);
}

public static class VorgabenTexte
{
    public const string Setzen = "Setzt Projektvorgaben für Kollisionsprüfung und Aussparungen (Element VG-1, je Projekt eines; wird beim ersten Aufruf angelegt). " +
        "Jeder Wert braucht eine Quelle (Bürostandard, Projektfestlegung …) – ohne Quelle wird der ganze Befehl abgelehnt; keine eingebauten Werte. " +
        "Namen: toleranz_hart (mm: Durchdringungen bis zu dieser Tiefe gelten als Berührung), spiel_aussparung, abstand_zusammenfassen (mm, für Aussparungen); optional " +
        "(Abstandsprüfung aus, Entscheid Christian 19.10.2026 – nur setzen, falls gewünscht): abstand_leitungen (mm: Mindestabstand Leitung–Leitung, weich), " +
        "abstand_weich (Objekt je Gewerkpaar in mm, z. B. { \"sanitaer-architektur\": …, \"hlks-fremd\": … }; Seiten: Gewerke, architektur, fremd, hlks = jedes HLKS-Gewerk). " +
        "grenze_kernbohrung wird nicht mehr verwendet (Entscheid Christian 19.10.2026: Kernbohrungen nur von Hand mit aussparung_form) – sie wird noch angenommen, mit Hinweis. " +
        "null entfernt einen Wert. Zuerst als Trockenlauf ansehen.";
    public const string ParWerte = "{ name: { \"wert\": Zahl bzw. Objekt, \"quelle\": \"Herkunft des Werts\" } | null }, z. B. { \"toleranz_hart\": { \"wert\": …, \"quelle\": \"Bürostandard H+K 2026\" } }";
    public const string Abfrage = "Projektvorgaben (VG-1): Werte mit Quelle und Herkunft, fehlende Werte mit ihrer Wirkung auf Kollisionsprüfung und Aussparungen, Werte ohne Quelle (gelten nicht), " +
        "optionale Vorgaben (abstand_weich, abstand_leitungen: Abstandsprüfung aus, Entscheid Christian 19.10.2026; setzbar, falls gewünscht), nicht mehr verwendete Vorgaben " +
        "(grenze_kernbohrung, Entscheid Christian 19.10.2026).";
}

/// <summary>Befehl vorgaben_setzen.</summary>
public sealed class VorgabenSetzen : IBefehl
{
    public string Name => "vorgaben_setzen";
    public string Beschreibung => VorgabenTexte.Setzen;
    public IReadOnlyList<ParameterDef> Parameter { get; } = [new("werte", "objekt", true, VorgabenTexte.ParWerte)];

    const string Beispiel = "werte: { \"toleranz_hart\": { \"wert\": …, \"quelle\": \"Bürostandard H+K 2026\" } } – Zahl in mm mit der Fundstelle des Werts";

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var werte = p["werte"] as JsonObject
            ?? throw new HlksFehler("Parameter «werte» fehlt oder ist kein Objekt.", Beispiel);
        if (werte.Count == 0) throw new HlksFehler("Parameter «werte» ist leer.", Beispiel);
        var d = k.Katalog.Klasse(Projektvorgaben.Klasse);
        var alt = Projektvorgaben.ElementVon(k.Arbeit);
        var e = alt?.Kopie() ?? new Element
        {
            Id = Guid.NewGuid().ToString(), Kennzeichen = NeuesKennzeichen(k), Klasse = d.Name, Name = "Projektvorgaben", Angelegt = k.Herkunft,
        };
        var seiten = Projektvorgaben.Seiten(k.Katalog);
        var fehler = new List<string>();
        var meldungen = new List<string>();
        var hinweiseAlt = new List<string>();   // nicht mehr verwendete Vorgaben (Nachtrag B3b)
        foreach (var (name, roh) in werte)
        {
            var def = d.Eigenschaften.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (def is null) { fehler.Add($"«{name}» gibt es nicht (erlaubt: {string.Join(", ", d.Eigenschaften.Select(x => x.Name))})"); continue; }
            if (roh is null || (roh is JsonObject leer && leer.ContainsKey("wert") && leer["wert"] is null))
            {
                if (e.Eigenschaften.Remove(def.Name)) meldungen.Add($"{def.Name} entfernt");
                continue;
            }
            if (roh is not JsonObject o || !o.ContainsKey("wert"))
            {
                fehler.Add($"{def.Name}: Wert ohne Quelle – Form {{ \"wert\": …, \"quelle\": \"…\" }}");
                continue;
            }
            var quelle = o["quelle"] is JsonValue qv && qv.TryGetValue<string>(out var qs) ? qs.Trim() : null;
            if (string.IsNullOrEmpty(quelle)) { fehler.Add($"{def.Name}: Quelle fehlt (z. B. «Bürostandard H+K 2026» oder die Projektfestlegung mit Datum)"); continue; }
            var einheit = o["einheit"] is JsonValue ev && ev.TryGetValue<string>(out var es) ? es.Trim() : null;
            if (!string.IsNullOrEmpty(einheit) && def.Einheit is not null && !einheit.Equals(def.Einheit, StringComparison.OrdinalIgnoreCase))
            { fehler.Add($"{def.Name}: Einheit {einheit} – erwartet {def.Einheit}"); continue; }
            JsonNode inhalt;
            if (def.Typ == "zahl")
            {
                if (Json.Zahl(o["wert"]) is not double z || !double.IsFinite(z)) { fehler.Add($"{def.Name}: Wert muss eine Zahl{(def.Einheit is null ? "" : $" in {def.Einheit}")} sein"); continue; }
                if (z < 0) { fehler.Add($"{def.Name}: Wert {Zahl(z)} ist negativ"); continue; }
                inhalt = JsonValue.Create(z);
            }
            else if (def.Typ == "objekt")
            {
                if (o["wert"] is not JsonObject obj) { fehler.Add($"{def.Name}: Wert muss ein Objekt sein, z. B. {{ \"sanitaer-architektur\": … }}"); continue; }
                var neu = new JsonObject();
                bool ok = true;
                foreach (var (schluessel, v) in obj)
                {
                    var s = Projektvorgaben.Schluessel(schluessel);
                    var teile = s?.Split('-') ?? [];
                    if (s is null || teile.Any(t => !seiten.Contains(t)))
                    { fehler.Add($"{def.Name}: Schlüssel «{schluessel}» – erwartet «<a>-<b>» mit a, b aus {string.Join(", ", seiten)}"); ok = false; continue; }
                    if (!teile.Any(Projektvorgaben.IstHlksSeite))
                    { fehler.Add($"{def.Name}: Schlüssel «{schluessel}» – eine Seite muss ein HLKS-Gewerk (oder hlks) sein"); ok = false; continue; }
                    if (Json.Zahl(v) is not double z || !double.IsFinite(z) || z < 0)
                    { fehler.Add($"{def.Name}: «{schluessel}» braucht eine Zahl ≥ 0 in mm"); ok = false; continue; }
                    if (neu.ContainsKey(s)) { fehler.Add($"{def.Name}: «{schluessel}» doppelt (Reihenfolge der Seiten zählt nicht)"); ok = false; continue; }
                    neu[s] = z;
                }
                if (!ok) continue;
                inhalt = neu;
            }
            else inhalt = o["wert"]!.DeepClone();
            var w = new Wert { Inhalt = inhalt, Einheit = def.Einheit, Herkunft = k.Herkunft with { Quelle = quelle } };
            if (Projektvorgaben.NichtMehrVerwendet.Contains(def.Name)) hinweiseAlt.Add($"{def.Name}: {Projektvorgaben.NichtMehrVerwendetGrund(def.Name)} – der Wert wird gespeichert, aber von keinem Befehl verwendet.");
            if (e.Eigenschaft(def.Name) is { } vorher && vorher.Inhalt?.ToJsonString() == inhalt.ToJsonString() && vorher.Herkunft.Quelle == quelle) continue;
            e.Eigenschaften[def.Name] = w;
            meldungen.Add($"{def.Name} = {Text(inhalt, def.Einheit)} (Quelle: {quelle})");
        }
        if (fehler.Count > 0)
            throw new HlksFehler($"Vorgaben nicht gesetzt: {string.Join("; ", fehler)}.",
                "Jeden Wert mit Quelle angeben – " + Beispiel + ". Keine Werte aus dem Gedächtnis; fehlt die Festlegung, den Wert weglassen.");
        if (meldungen.Count == 0) { k.Hinweise.Add("Keine Änderung."); k.Hinweise.AddRange(hinweiseAlt); return; }
        k.Setzen(e);
        k.Hinweise.Add($"{e.Kennzeichen}: {string.Join("; ", meldungen)}.");
        k.Hinweise.AddRange(hinweiseAlt);
        var fehlend = d.Eigenschaften.Where(x => !e.Eigenschaften.ContainsKey(x.Name) && Projektvorgaben.Verlangt(x.Name)).Select(x => x.Name).ToList();
        if (fehlend.Count > 0) k.Hinweise.Add($"Noch ohne Wert: {string.Join(", ", fehlend)} (Wirkung: Abfrage vorgaben).");
        var weitere = k.Arbeit.DerKlasse(Projektvorgaben.Klasse).Where(x => x.Id != e.Id).ToList();
        if (weitere.Count > 0)
            k.Hinweise.Add($"Es gibt weitere Elemente der Klasse Vorgaben ({string.Join(", ", weitere.Select(x => x.Kennzeichen))}) – es gilt {e.Kennzeichen}; die übrigen löschen (element_loeschen).");
    }

    static string NeuesKennzeichen(Kontext k)
    {
        var kz = $"{Projektvorgaben.Kuerzel}-1";
        return k.Arbeit.Finde(kz) is null ? kz : k.NaechstesKennzeichen(Projektvorgaben.Kuerzel);
    }

    static string Zahl(double x) => x.ToString("0.###", CultureInfo.InvariantCulture);

    internal static string Text(JsonNode n, string? einheit) =>
        Json.Zahl(n) is double z ? Zahl(z) + (einheit is null ? "" : " " + einheit)
        : n is JsonObject o ? "{ " + string.Join(", ", o.Select(x => $"{x.Key}: {(Json.Zahl(x.Value) is double v ? Zahl(v) : x.Value?.ToJsonString())} mm")) + " }"
        : n.ToJsonString();
}

/// <summary>Abfrage vorgaben.</summary>
public static class VorgabenAbfrage
{
    public static JsonObject Lesen(Projekt p)
    {
        var v = Projektvorgaben.Lesen(p.Modell);
        var d = p.Katalog.Klasse(Projektvorgaben.Klasse);
        var werte = new JsonObject();
        foreach (var def in d.Eigenschaften)
            if (v.Werte.TryGetValue(def.Name, out var w))
            {
                var zeile = new JsonObject
                {
                    ["wert"] = w.Wert.DeepClone(), ["einheit"] = def.Einheit ?? (def.Typ == "objekt" ? "mm je Gewerkpaar" : null), ["quelle"] = w.Quelle,
                    ["gesetzt"] = $"{w.Herkunft.Art}, {w.Herkunft.Akteur}, {w.Herkunft.Zeit.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}",
                    ["beschreibung"] = def.Beschreibung,
                };
                if (Projektvorgaben.NichtMehrVerwendet.Contains(def.Name))
                {
                    zeile["verwendet"] = false;
                    zeile["hinweis"] = Projektvorgaben.NichtMehrVerwendetGrund(def.Name);
                }
                if (Projektvorgaben.Optional.Contains(def.Name)) zeile["optional"] = true;   // gesetzt: Abstandsprüfung an (Nachtrag B3c)
                werte[def.Name] = zeile;
            }
        // nicht mehr verwendete (Nachtrag B3b) und optionale Vorgaben (Nachtrag B3c, Abstandsprüfung aus) fehlen nicht
        var fehlend = new JsonArray(d.Eigenschaften.Where(x => !v.Werte.ContainsKey(x.Name) && Projektvorgaben.Verlangt(x.Name)).Select(x => (JsonNode)new JsonObject
        {
            ["name"] = x.Name, ["beschreibung"] = x.Beschreibung, ["wirkung"] = Projektvorgaben.Wirkung.GetValueOrDefault(x.Name),
        }).ToArray());
        var optional = new JsonArray(d.Eigenschaften.Where(x => Projektvorgaben.Optional.Contains(x.Name)).Select(x => (JsonNode)new JsonObject
        {
            ["name"] = x.Name, ["beschreibung"] = x.Beschreibung, ["gesetzt"] = v.Werte.ContainsKey(x.Name), ["hinweis"] = Projektvorgaben.OptionalText,
        }).ToArray());
        var o = new JsonObject
        {
            ["element"] = v.Element?.Kennzeichen,
            ["werte"] = werte,
            ["fehlend"] = fehlend,
            ["optional"] = optional,
            ["ohneQuelle"] = new JsonArray(v.OhneQuelle.Select(x => (JsonNode)JsonValue.Create(x)!).ToArray()),
            ["nichtMehrVerwendet"] = new JsonArray(d.Eigenschaften.Where(x => Projektvorgaben.NichtMehrVerwendet.Contains(x.Name)).Select(x => (JsonNode)new JsonObject
            {
                ["name"] = x.Name, ["gesetzt"] = v.Werte.ContainsKey(x.Name) || v.OhneQuelle.Contains(x.Name), ["hinweis"] = Projektvorgaben.NichtMehrVerwendetGrund(x.Name),
            }).ToArray()),
            ["abstandWeichSeiten"] = new JsonArray(Projektvorgaben.Seiten(p.Katalog).Select(x => (JsonNode)JsonValue.Create(x)!).ToArray()),
        };
        var hinweise = new List<string>();
        if (v.Element is null) hinweise.Add("Noch keine Projektvorgaben – mit vorgaben_setzen anlegen (je Wert mit Quelle).");
        foreach (var n in Projektvorgaben.NichtMehrVerwendet.Where(v.Werte.ContainsKey).Order(StringComparer.Ordinal))
            hinweise.Add($"{n} ist gesetzt, wird aber {Projektvorgaben.NichtMehrVerwendetGrund(n)}.");
        if (v.OhneQuelle.Count > 0) hinweise.Add($"Ohne Quelle (gelten nicht): {string.Join(", ", v.OhneQuelle)} – mit vorgaben_setzen und Quelle neu setzen.");
        if (v.Weitere.Count > 0) hinweise.Add($"Weitere Elemente der Klasse Vorgaben ({string.Join(", ", v.Weitere.Select(x => x.Kennzeichen))}) – es gilt {v.Element!.Kennzeichen}.");
        o["hinweise"] = new JsonArray(hinweise.Select(x => (JsonNode)JsonValue.Create(x)!).ToArray());
        return o;
    }
}
