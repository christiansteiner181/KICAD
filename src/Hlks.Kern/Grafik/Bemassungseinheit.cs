// Einheit der Masszahlen. Vorgabe je Gewerk (Christian Steiner, 17.10.2026): Grundrisse (Architektur) in Meter,
// Lüftung in Millimeter, Heizung und Sanitär in Zentimeter – immer einstellbar. Es gilt, von oben nach unten:
//   1. Masskette    Eigenschaft «einheit» der Bemassung
//   2. Ansicht      Eigenschaft «bemassungEinheit» der Ansicht (bzw. ihrer Vorlage)
//   3. Projekt      Projektinformation «bemassungEinheiten» { gewerk: einheit }
//   4. Vorgabe      Tabelle unten, Gewerk der Ansicht
// Gewerk der Ansicht = erstes HLKS-Gewerk ihrer Liste «gewerke» in der Reihenfolge Lüftung, Heizung, Sanitär, Kälte,
// Elektro, MSRL; ohne HLKS-Gewerk (Architektur oder alle Gewerke) gilt Architektur.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class Bemassungseinheit
{
    public static readonly IReadOnlyList<string> Einheiten = ["m", "cm", "mm"];

    /// <summary>Vorgabe je Gewerk. architektur, lueftung, heizung, sanitaer: Christian Steiner 17.10.2026;
    /// kaelte, elektro, msrl: Annahme (Kälte wie Heizung, Elektro und MSRL wie Grundriss), Prüfliste.</summary>
    public static readonly IReadOnlyDictionary<string, string> Vorgabe = new Dictionary<string, string>
    {
        ["architektur"] = "m", ["lueftung"] = "mm", ["heizung"] = "cm", ["sanitaer"] = "cm",
        ["kaelte"] = "cm", ["elektro"] = "m", ["msrl"] = "m",
    };

    static readonly string[] Vorrang = ["lueftung", "heizung", "sanitaer", "kaelte", "elektro", "msrl"];

    /// <summary>Gewerk, nach dem sich die Masse einer Ansicht richten.</summary>
    public static string Gewerk(Element ansicht)
    {
        var gewerke = Grafik.Liste(ansicht, "gewerke");
        return Vorrang.FirstOrDefault(g => gewerke.Contains(g)) ?? "architektur";
    }

    /// <summary>Einheit der Masszahlen und woher sie kommt (masskette | ansicht | projekt | vorgabe).</summary>
    public static (string Einheit, string Herkunft) Fuer(Modell m, Element ansicht, Element? bemassung = null)
    {
        if (Gueltig(bemassung?.Text("einheit")) is { } b) return (b, "masskette");
        if (Gueltig(ansicht.Text("bemassungEinheit")) is { } a) return (a, "ansicht");
        var gewerk = Gewerk(ansicht);
        var info = m.DerKlasse("Projektinformation").FirstOrDefault();
        if (info?.Eigenschaft("bemassungEinheiten")?.Inhalt is JsonObject tabelle
            && tabelle[gewerk] is JsonValue v && v.TryGetValue<string>(out var t) && Gueltig(t) is { } p) return (p, "projekt");
        return (Vorgabe.TryGetValue(gewerk, out var vg) ? vg : "m", "vorgabe");
    }

    static string? Gueltig(string? e) => e is not null && Einheiten.Contains(e) ? e : null;
}
