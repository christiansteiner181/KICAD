// Hilfen der Gebäudehülle (Modul 1c, B2): Kennwerte mit Quelle lesen und schreiben, Bezüge auf Material und Aufbau
// auflösen, Schichten lesen. Keine Kennwerte im Code: was hier steht, sind Prüfungen und Formen, keine Normwerte.

using System.Globalization;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <summary>Eine Schicht eines Aufbaus: Material-Id und Dicke in mm (von innen nach aussen).</summary>
public sealed record Schicht(string? Material, double? Dicke);

static class HuelleHilfe
{
    public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Zahl für Texte (Herkunft, Meldungen): höchstens 6 gültige Ziffern.</summary>
    public static string Z(double d) => d.ToString("G6", Inv);

    public static string? Sauber(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>Quelle eines Werts (Herkunft.Quelle), leer = keine.</summary>
    public static string? Quelle(Wert? w) => Sauber(w?.Herkunft.Quelle);

    public static string Einheit(Katalog kat, string klasse, string eigenschaft) =>
        kat.Klassen.TryGetValue(klasse, out var d) ? d.Eigenschaften.FirstOrDefault(x => x.Name == eigenschaft)?.Einheit ?? "" : "";

    public static List<string> Auswahl(Katalog kat, string klasse, string eigenschaft) =>
        kat.Klassen.TryGetValue(klasse, out var d) ? d.Eigenschaften.FirstOrDefault(x => x.Name == eigenschaft)?.Auswahl ?? [] : [];

    /// <summary>Neuer Wert eines Menschen oder einer KI (Art kommt aus dem Akteur) mit Quelle.</summary>
    public static Wert Neu(Kontext k, JsonNode? inhalt, string? einheit, string? quelle, string? art = null) => new()
    {
        Inhalt = inhalt, Einheit = string.IsNullOrEmpty(einheit) ? null : einheit,
        Herkunft = k.Herkunft with { Art = art ?? k.Herkunft.Art, Quelle = quelle ?? k.Herkunft.Quelle },
    };

    public static Wert Berechnet(Kontext k, double wert, string einheit, string quelle) => new()
    {
        Inhalt = JsonValue.Create(wert), Einheit = string.IsNullOrEmpty(einheit) ? null : einheit,
        Herkunft = k.Herkunft with { Art = "berechnung", Quelle = quelle },
    };

    public static bool GleicheZahl(double? a, double? b) =>
        a is null ? b is null : b is not null && Math.Abs(a.Value - b.Value) <= 1e-12 * Math.Max(1, Math.Abs(b.Value));

    /// <summary>Kennwert eines Elements: Zahl und ob eine Quelle dabeisteht. Ohne Quelle gilt er in der Rechnung als fehlend.</summary>
    public static (double? Wert, bool HatQuelle) ZahlMitQuelle(Element e, string eigenschaft)
    {
        var w = e.Eigenschaft(eigenschaft);
        return (w?.AlsZahl(), Quelle(w) is not null);
    }

    /// <summary>{ wert, quelle } lesen. Die Quelle ist Pflicht (Grundsatz: kein Kennwert ohne Quelle).</summary>
    public static (double Wert, string Quelle) WertMitQuelle(JsonNode? roh, string name, string? sammelquelle = null)
    {
        const string form = "Form: { \"wert\": Zahl, \"quelle\": \"Norm mit Ausgabe und Tabelle, Datenblatt mit Datum, IFC-Datei oder Angabe von Christian\" }";
        double wert; string? quelle = sammelquelle;
        if (roh is JsonObject o)
        {
            wert = Json.Zahl(o["wert"]) ?? throw new HlksFehler($"«{name}»: «wert» fehlt oder ist keine Zahl.", form);
            if (o["quelle"] is not null) quelle = Param.Text(o, "quelle");
        }
        else if (sammelquelle is not null && Json.Zahl(roh) is double z) wert = z;
        else throw new HlksFehler($"«{name}»: Kennwerte brauchen Wert und Quelle.", form);
        if (Sauber(quelle) is not { } q)
            throw new HlksFehler($"«{name}»: Quelle fehlt. Ein Kennwert ohne Quelle wird abgelehnt.",
                "Quelle nennen: Norm mit Ausgabe und Tabelle, Herstellerdatenblatt mit Datum, IFC-Datei oder Angabe von Christian. Der Kern setzt keine Werte aus dem Gedächtnis.");
        return (wert, q);
    }

    public static void Pruefen(string name, double wert, double min, bool minEingeschlossen, double? max, string einheit)
    {
        bool ok = (minEingeschlossen ? wert >= min : wert > min) && (max is null || wert <= max);
        if (ok) return;
        var bereich = max is null ? $"{(minEingeschlossen ? "mindestens" : "grösser als")} {Z(min)}" : $"zwischen {Z(min)} und {Z(max.Value)}";
        throw new HlksFehler($"«{name}» = {Z(wert)} ist ungültig: erwartet {bereich}{(string.IsNullOrEmpty(einheit) ? "" : " " + einheit)}.",
            string.IsNullOrEmpty(einheit) ? "Wert prüfen." : $"Wert in {einheit} angeben.");
    }

    /// <summary>Element der Klasse über Kennzeichen, Id oder (eindeutigen) Namen.</summary>
    public static Element Hole(Kontext k, string? bezug, string klasse, string rolle) => Hole(k.Arbeit, k.Katalog, bezug, klasse, rolle);

    public static Element Hole(Modell m, Katalog kat, string? bezug, string klasse, string rolle)
    {
        if (string.IsNullOrWhiteSpace(bezug)) throw new HlksFehler($"{rolle} fehlt.", $"Kennzeichen, Id oder Name eines Elements der Klasse {klasse} angeben.");
        var e = m.Finde(bezug);
        if (e is not null)
        {
            if (!kat.IstArt(e, klasse))
                throw new HlksFehler($"{rolle} «{e.Kennzeichen}» ist ein {e.Klasse}, erwartet wird {klasse}.", Beispiele(m, kat, klasse));
            return e;
        }
        var treffer = m.Elemente.Where(x => kat.IstArt(x, klasse) && string.Equals(x.Name?.Trim(), bezug.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        if (treffer.Count == 1) return treffer[0];
        if (treffer.Count > 1)
            throw new HlksFehler($"{rolle} «{bezug}» ist mehrdeutig: {string.Join(", ", treffer.Select(x => x.Kennzeichen))}.", "Das Kennzeichen angeben.");
        throw new HlksFehler($"{rolle} «{bezug}» gibt es nicht.", Beispiele(m, kat, klasse));
    }

    static string Beispiele(Modell m, Katalog kat, string klasse)
    {
        var liste = m.Elemente.Where(x => kat.IstArt(x, klasse)).OrderBy(x => x.Kennzeichen, StringComparer.OrdinalIgnoreCase).Take(8)
            .Select(x => x.Name is null ? x.Kennzeichen : $"{x.Kennzeichen} ({x.Name})").ToList();
        return liste.Count > 0 ? $"Vorhanden: {string.Join(", ", liste)}." : klasse == "Material"
            ? "Es gibt noch kein Material: material_anlegen (mit Quelle) oder material_uebernehmen (Bürobibliothek)."
            : $"Zuerst ein Element der Klasse {klasse} anlegen.";
    }

    /// <summary>Schichten eines Aufbaus (Liste { material, dicke }); fehlerhafte Einträge werden zu Schichten ohne Angaben.</summary>
    public static List<Schicht> Schichten(Element aufbau)
    {
        var liste = new List<Schicht>();
        if (aufbau.Eigenschaft("schichten")?.Inhalt is not JsonArray a) return liste;
        foreach (var s in a)
        {
            var o = s as JsonObject;
            var mat = o?["material"] is JsonValue v && v.TryGetValue<string>(out var id) ? id : null;
            liste.Add(new Schicht(mat, Json.Zahl(o?["dicke"])));
        }
        return liste;
    }

    public static Element? Projektinfo(Modell m) =>
        m.DerKlasse("Projektinformation").OrderBy(e => e.Kennzeichen, StringComparer.Ordinal).FirstOrDefault();

    /// <summary>Aufbauarten, die zu einem Bauteil passen; leer = das Bauteil hat keinen Aufbau (Fenster, Stütze, Träger).</summary>
    public static string[] ErlaubteAufbauArten(Katalog kat, Element bauteil)
    {
        if (kat.IstArt(bauteil, "Wand")) return ["wand"];
        if (kat.IstArt(bauteil, "Tuer")) return ["tuer"];
        if (kat.IstArt(bauteil, "Dach")) return ["dach"];
        if (kat.IstArt(bauteil, "Decke")) return bauteil.Text("art") == "dach" ? ["dach"] : ["decke", "boden"];
        return [];
    }

    /// <summary>Aufbauten je Material-Id. Schichten dürfen das Material auch über Kennzeichen nennen (z. B. von Hand gesetzt).</summary>
    public static Dictionary<string, List<Element>> AufbautenNachMaterial(Modell m)
    {
        var index = new Dictionary<string, List<Element>>(StringComparer.Ordinal);
        foreach (var a in m.DerKlasse("Aufbau"))
            foreach (var s in Schichten(a))
            {
                if (s.Material is null) continue;
                var id = m.Finde(s.Material)?.Id ?? s.Material;
                if (!index.TryGetValue(id, out var l)) index[id] = l = [];
                if (!l.Contains(a)) l.Add(a);
            }
        return index;
    }

    public static IEnumerable<Element> BauteileMit(Modell m, string aufbauId) =>
        m.Elemente.Where(e => e.Bezuege.GetValueOrDefault("aufbau") == aufbauId);
}
