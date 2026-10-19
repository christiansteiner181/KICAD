// Familien (Modul 2, B1): Zugriff auf Familien im Modell – Definition eines Familie-Elements (zwischengespeichert je
// Definitionsstand), Familie und Auswertung eines platzierten Elements, Anschlusslagen in Weltkoordinaten (auch für Modul 3).
//
// Zwischenspeicher der Definitionen: zuerst über die Instanz des JSON-Knotens (unverändert, solange das Element nicht
// geändert wird), sonst über den Inhalt (SHA-256 des JSON) – Arbeitskopien eines Befehls (Modell.Kopie) treffen so dieselbe
// gelesene Definition samt ihren Auswertungen. Getrennt je Katalog (gelesen wird gegen dessen Klassen und Medien).

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <summary>Lage eines Anschlusses in Weltkoordinaten (mm) mit Richtung aus dem Gerät heraus (Einheitsvektor); Grösse als DN (Rohr),
/// d (Kanal rund, mm) bzw. b × h (Kanal rechteckig, mm).</summary>
public sealed record AnschlussLage(string Name, string Medium, string Fluss, string? Art, string? Form, double? Dn, double? D, double? B, double? H,
    string? Gewinde, double[] Lage, double[] Richtung);

/// <summary>Lage einer platzierten Familie (Modul 3, volle Ausrichtung): Einfügepunkt (Welt, mm) und die lokalen Achsen x, y, z der
/// Familie in Weltkoordinaten – orthonormal und rechtshändig (y = z × x). Ohne ausrichtung eine Drehung um z (drehung).</summary>
public sealed record Einbaulage(double[] Ursprung, V3 X, V3 Y, V3 Z)
{
    /// <summary>Drehung um z (Grad, Gegenuhrzeigersinn) – die Lage der Modul-2-Familien.</summary>
    public static Einbaulage Gedreht(double[] ein, double drehung)
    {
        double w = drehung * Math.PI / 180, c = Math.Cos(w), s = Math.Sin(w);
        return new Einbaulage(ein, new V3(c, s, 0), new V3(-s, c, 0), new V3(0, 0, 1)) { Drehwinkel = drehung };
    }

    /// <summary>Die angegebene drehung (Grad), wenn die Lage daraus entstand; sonst null.</summary>
    public double? Drehwinkel { get; init; }

    /// <summary>Aus lokaler x- und z-Achse (Welt, normiert und rechtwinklig, siehe Familien.AusrichtungLesen); y = z × x.</summary>
    public static Einbaulage Achsen(double[] ein, V3 x, V3 z)
    {
        var ex = x.Einheit();
        var ez = (z - ex * ex.Mal(z)).Einheit();
        return new Einbaulage(ein, ex, ez.Kreuz(ex), ez);
    }

    /// <summary>Die lokale z-Achse zeigt senkrecht nach oben – die Lage ist eine reine Drehung um z (Sinnbild gilt).</summary>
    public bool Senkrecht => Math.Abs(Z.X) < 1e-9 && Math.Abs(Z.Y) < 1e-9 && Z.Z > 0;

    /// <summary>Drehung der lokalen x-Achse um z (Grad, Gegenuhrzeigersinn); nur bei <see cref="Senkrecht"/> die ganze Lage.</summary>
    public double DrehungZ => Drehwinkel ?? Math.Atan2(X.Y, X.X) * 180 / Math.PI;

    /// <summary>Punkt aus Familien- in Weltkoordinaten.</summary>
    public double[] Welt(double[] p)
    {
        double px = p[0], py = p[1], pz = p.Length > 2 ? p[2] : 0;
        return [Ursprung[0] + X.X * px + Y.X * py + Z.X * pz, Ursprung[1] + X.Y * px + Y.Y * py + Z.Y * pz, Ursprung[2] + X.Z * px + Y.Z * py + Z.Z * pz];
    }

    /// <summary>Richtung aus Familien- in Weltkoordinaten (nur gedreht).</summary>
    public double[] Richtung(double[] d)
    {
        double dx = d[0], dy = d[1], dz = d.Length > 2 ? d[2] : 0;
        return [X.X * dx + Y.X * dy + Z.X * dz, X.Y * dx + Y.Y * dy + Z.Y * dz, X.Z * dx + Y.Z * dy + Z.Z * dz];
    }

    /// <summary>Schlüssel der Achsen (für Zwischenspeicher je Ausrichtung).</summary>
    public string AchsenSchluessel => string.Join(",", new[] { X.X, X.Y, X.Z, Z.X, Z.Y, Z.Z }.Select(v => Math.Round(v, 9).ToString("R", System.Globalization.CultureInfo.InvariantCulture)));

    /// <summary>{ ursprung, x, y, z } für Abfragen.</summary>
    public JsonObject AlsJson() => new()
    {
        ["x"] = Json.Knoten(Familien.Runden(X.Feld(), 9)), ["y"] = Json.Knoten(Familien.Runden(Y.Feld(), 9)), ["z"] = Json.Knoten(Familien.Runden(Z.Feld(), 9)),
    };
}

public static class Familien
{
    /// <summary>Rolle des Bezugs auf die Familie (bezuege.familie).</summary>
    public const string Bezug = "familie";
    public const string Klasse = "Familie";

    static readonly ConditionalWeakTable<Katalog, ConditionalWeakTable<JsonObject, FamilieDef>> nachKnoten = new();
    static readonly ConditionalWeakTable<Katalog, ConcurrentDictionary<string, FamilieDef>> nachInhalt = new();

    /// <summary>Gelesene und geprüfte Definition eines Familie-Elements.</summary>
    public static FamilieDef Definition(Element familie, Katalog k) =>
        familie.Eigenschaft("definition")?.Inhalt is JsonObject def ? Definition(def, k) : FamilieDef.Lesen(new JsonObject(), k);

    /// <summary>Gelesene und geprüfte Definition (zwischengespeichert, siehe Dateikopf).</summary>
    public static FamilieDef Definition(JsonObject def, Katalog k)
    {
        var knoten = nachKnoten.GetValue(k, _ => new());
        if (knoten.TryGetValue(def, out var f)) return f;
        var text = def.ToJsonString();
        var schluessel = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        var inhalt = nachInhalt.GetValue(k, _ => new());
        if (!inhalt.TryGetValue(schluessel, out f))
        {
            f = FamilieDef.Lesen((JsonObject)JsonNode.Parse(text)!, k);
            if (inhalt.Count > 200) inhalt.Clear();
            inhalt[schluessel] = f;
        }
        knoten.AddOrUpdate(def, f);
        return f;
    }

    /// <summary>Familie, auf die bezuege.familie zeigt (oder null).</summary>
    public static Element? FamilieVon(Modell m, Element e) =>
        e.Bezuege.TryGetValue(Bezug, out var id) && m.Finde(id) is { } f && f.Klasse == Klasse ? f : null;

    public static string? DefinitionsId(Element familie) =>
        (familie.Eigenschaft("definition")?.Inhalt as JsonObject)?["id"] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    /// <summary>Familie über Kennzeichen, Id, Name oder die feste Kennung der Definition (id).</summary>
    public static Element Hole(Modell m, string? bezug)
    {
        if (string.IsNullOrWhiteSpace(bezug))
            throw new HlksFehler("Familie fehlt.", "Familie über Kennzeichen (FAM-n), Name oder id angeben; familien listet sie.");
        var e = m.Finde(bezug);
        if (e is not null && e.Klasse == Klasse) return e;
        var b = bezug.Trim();
        var treffer = m.DerKlasse(Klasse).Where(f => string.Equals(f.Name?.Trim(), b, StringComparison.OrdinalIgnoreCase) || DefinitionsId(f) == b).ToList();
        if (treffer.Count == 1) return treffer[0];
        var alle = m.DerKlasse(Klasse).OrderBy(f => f.Kennzeichen, StringComparer.OrdinalIgnoreCase).Select(f => $"{f.Kennzeichen} «{f.Name}»").ToList();
        if (treffer.Count > 1)
            throw new HlksFehler($"Mehrere Familien heissen «{b}»: {string.Join(", ", treffer.Select(f => f.Kennzeichen))}.", "Familie über das Kennzeichen angeben.");
        if (e is not null)
            throw new HlksFehler($"{e.Kennzeichen} ist keine Familie, sondern {e.Klasse}.", alle.Count > 0 ? "Familien: " + string.Join(", ", alle) : "Familie mit familie_entwurf oder familie_laden anlegen.");
        throw new HlksFehler($"Familie «{b}» gibt es nicht.", alle.Count > 0 ? "Familien: " + string.Join(", ", alle) : "Familie mit familie_entwurf oder familie_laden anlegen.");
    }

    /// <summary>Kurzfassung der Definition für die Oberfläche (Darstellung.Element): Name, Kategorie, Anzahl Parameter, Typen,
    /// Anzahl Körper und Anschlüsse, Status, Version. Die ganze Definition liefert die Abfrage «familie» (Entscheid Hauptlauf
    /// 18.10.2026: grosse Antworten vermeiden).</summary>
    public static JsonObject Zusammenfassung(Element familie)
    {
        var d = familie.Eigenschaft("definition")?.Inhalt as JsonObject;
        static int Anzahl(JsonNode? n) => n is JsonArray a ? a.Count : 0;
        static string? T(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
        return new JsonObject
        {
            ["zusammenfassung"] = true,
            ["name"] = T(d?["name"]) ?? familie.Name,
            ["kategorie"] = T(d?["kategorie"]) ?? familie.Text("kategorie"),
            ["parameter"] = Anzahl(d?["parameter"]),
            ["typen"] = new JsonArray(((d?["typen"] as JsonArray) ?? []).OfType<JsonObject>().Select(t => T(t["name"])).Where(x => x is not null)
                .Select(x => (JsonNode)JsonValue.Create(x!)).ToArray()),
            ["koerper"] = Anzahl(d?["koerper"]),
            ["anschluesse"] = Anzahl(d?["anschluesse"]),
            ["status"] = familie.Text("status"),
            ["version"] = familie.Zahl("version"),
        };
    }

    public static string? Typ(Element e) => e.Text("familientyp");
    public static double Drehung(Element e) => e.Zahl("drehung") ?? 0;
    public static JsonObject? Exemplarwerte(Element e) => e.Eigenschaft("parameter")?.Inhalt as JsonObject;

    /// <summary>Toleranz der Rechtwinkligkeit von x und z nach dem Normieren (|x·z|, Modul 3, Annahme).</summary>
    public const double AusrichtungToleranz = 1e-6;

    /// <summary>Liest eine Ausrichtung { x: [dx, dy, dz], z: [dx, dy, dz] } (lokale x- und z-Achse der Familie in Weltkoordinaten,
    /// Modul 3): beide Längen &gt; 0, nach dem Normieren rechtwinklig (|x·z| ≤ 1e-6, kleine Abweichungen werden gerade gerichtet).
    /// null = nicht angegeben; sonst normierte Achsen. Wirft HlksFehler mit Vorschlag.</summary>
    public static (V3 X, V3 Z)? AusrichtungLesen(JsonNode? n, string wer = "ausrichtung")
    {
        if (n is null) return null;
        const string Form = "Form: { \"x\": [dx, dy, dz], \"z\": [dx, dy, dz] } – lokale x- und z-Achse der Familie in Weltkoordinaten, rechtwinklig; z. B. Steigbogen x = [1, 0, 0], z = [0, -1, 0]. Für eine Drehung nur um die Senkrechte genügt drehung.";
        if (n is not JsonObject o) throw new HlksFehler($"{wer} muss ein Objekt {{ x, z }} sein.", Form);
        foreach (var (feld, _) in o)
            if (feld is not ("x" or "z")) throw new HlksFehler($"{wer}: Feld «{feld}» gibt es nicht.", Form);
        V3 Achse(string name)
        {
            if (o[name] is not JsonArray a || a.Count != 3)
                throw new HlksFehler($"{wer}.{name} fehlt oder ist keine Liste mit drei Zahlen.", Form);
            var v = a.Select(c => Json.Zahl(c) is double d && double.IsFinite(d) && c is JsonValue cv && cv.GetValueKind() == System.Text.Json.JsonValueKind.Number ? d
                : throw new HlksFehler($"{wer}.{name}: {c?.ToJsonString() ?? "null"} ist keine Zahl.", Form)).ToArray();
            var r = new V3(v[0], v[1], v[2]);
            if (r.Laenge < 1e-9) throw new HlksFehler($"{wer}.{name} = [0, 0, 0] hat keine Richtung.", Form);
            return r.Einheit();
        }
        var x = Achse("x"); var z = Achse("z");
        var skalar = x.Mal(z);
        if (Math.Abs(skalar) > AusrichtungToleranz)
        {
            var winkel = Math.Acos(Math.Clamp(skalar, -1, 1)) * 180 / Math.PI;
            throw new HlksFehler($"{wer}: x und z stehen nicht rechtwinklig aufeinander (Winkel {winkel.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}° statt 90°).",
                "x und z rechtwinklig angeben (Skalarprodukt 0). " + Form);
        }
        z = (z - x * skalar).Einheit();   // gerade richten (Abweichung ≤ 1e-6)
        return (x, z);
    }

    /// <summary>Ausrichtung als JSON { x, z } (für Befehle, z. B. B2 beim Setzen von Formteilen).</summary>
    public static JsonObject AusrichtungJson(V3 x, V3 z) => new()
    {
        ["x"] = new JsonArray(Runden(x.Feld(), 12).Select(c => (JsonNode)c).ToArray()),
        ["z"] = new JsonArray(Runden(z.Feld(), 12).Select(c => (JsonNode)c).ToArray()),
    };

    /// <summary>Lage eines platzierten Familienelements: Einfügepunkt und Achsen aus ausrichtung (geht vor) bzw. drehung um z.
    /// null = keine Lage (nicht platziert). Eine ungültige gespeicherte ausrichtung (z. B. aus einem fremden Paket) fällt auf
    /// drehung zurück – Grafik und Abfragen scheitern nicht daran (die Prüfung macht die Nachführung beim Ändern).</summary>
    public static Einbaulage? Einbaulage(Element e)
    {
        var ein = Einfuegepunkt(e);
        if (ein is null) return null;
        if (e.Eigenschaft("ausrichtung")?.Inhalt is { } n)
        {
            try { if (AusrichtungLesen(n) is { } a) return global::Hlks.Kern.Einbaulage.Achsen(ein, a.X, a.Z); }
            catch (HlksFehler) { }
        }
        return global::Hlks.Kern.Einbaulage.Gedreht(ein, Drehung(e));
    }

    /// <summary>Einfügepunkt [x, y, z] (erster Punkt der Geometrie) oder null.</summary>
    public static double[]? Einfuegepunkt(Element e)
    {
        var p = e.Geometrie?.Punkte.FirstOrDefault();
        if (p is null || p.Length < 2 || !double.IsFinite(p[0]) || !double.IsFinite(p[1])) return null;
        return [p[0], p[1], p.Length > 2 && double.IsFinite(p[2]) ? p[2] : 0];
    }

    /// <summary>Auswertung eines platzierten Elements; null mit Grund, wenn es keine gültige Familie hat (dann zeigt die Grafik die
    /// Klassendarstellung).</summary>
    public static FamAufloesung? Aufloesung(Modell m, Katalog k, Element e, out string? grund)
    {
        grund = null;
        if (!e.Bezuege.TryGetValue(Bezug, out var id)) { grund = $"{e.Kennzeichen} hat keine Familie (bezuege.familie)."; return null; }
        var f = m.Finde(id);
        if (f is null || f.Klasse != Klasse) { grund = $"Die Familie von {e.Kennzeichen} gibt es nicht mehr."; return null; }
        var def = Definition(f, k);
        if (!def.Gueltig) { grund = $"Familie {f.Kennzeichen} «{f.Name}» ist ungültig: {def.Fehler[0]}"; return null; }
        if (!k.IstArt(e, def.Kategorie)) { grund = $"{e.Kennzeichen} ist ein {e.Klasse}, die Familie {f.Kennzeichen} gehört zur Kategorie {def.Kategorie}."; return null; }
        if (Einfuegepunkt(e) is null) { grund = $"{e.Kennzeichen} hat keine Lage."; return null; }
        var a = FamilienAuswertung.Aufloesen(def, Typ(e), Exemplarwerte(e));
        if (!a.Gueltig) { grund = $"{e.Kennzeichen}, Familie {f.Kennzeichen} Typ «{Typ(e)}»: {a.Fehler[0]}"; return null; }
        return a;
    }

    /// <summary>Anschlüsse eines platzierten Elements in Weltkoordinaten (abgeleitet aus Familie, Einfügepunkt, Drehung und
    /// Parametern, nicht gespeichert). Nur Anschlüsse, die für Typ und Exemplarwerte sichtbar sind.</summary>
    public static List<AnschlussLage> Anschlusslagen(Modell m, Katalog k, Element e)
    {
        var a = Aufloesung(m, k, e, out var grund) ?? throw new HlksFehler(grund ?? "Keine Familie.", "Element mit familie_platzieren anlegen oder bezuege.familie und familientyp setzen.");
        var lage = Einbaulage(e)!;
        var sysMedium = Netz.SystemMedium(m, e.System);
        return a.Anschluesse.Where(x => x.Sichtbar).Select(x => new AnschlussLage(
            x.Name, x.Medium == "*" ? sysMedium ?? "*" : x.Medium, x.Fluss, x.Art, x.Form, x.Dn, x.D, x.B, x.H, x.Gewinde,
            Runden(lage.Welt(x.Lage)), Runden(lage.Richtung(x.Richtung), 9))).ToList();
    }

    /// <summary>Grösse eines Anschlusses (DN, d, b, h) als ganze Zahl für Element.Anschluesse.</summary>
    public static int? Ganz(double? x) => x is double d ? (int)Math.Round(d) : null;

    /// <summary>Gerundet auf 0.001 mm (bzw. 9 Stellen bei Richtungen), damit cos(90°) keine 6e-17 zeigt.</summary>
    public static double[] Runden(double[] p, int stellen = 3) => p.Select(x => { var r = Math.Round(x, stellen); return r == 0 ? 0 : r; }).ToArray();
}
