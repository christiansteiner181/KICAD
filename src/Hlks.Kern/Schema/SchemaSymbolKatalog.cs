// Modul 5, Teil A-S 2 (zweiter Anlauf): Katalog der Schemasymbole aus Christians HKAP-Symbolbibliothek (kataloge/schemasymbole.json).
// Die Datei wird NIE von Hand bearbeitet: werkzeuge/hkap_symbole_uebernehmen erzeugt sie aus data/schema_symbole.json (Testkopie) und dem
// Code der HKAP (SchemaSymbole.cs) nach derselben Regel wie die HKAP selbst (eingebaute Symbole, durch gleichnamige Einträge der Datei ersetzt,
// plus die eigenen Symbole). Die Prüfsumme im Kopf (inhaltspruefsumme) deckt Darstellung, Zuordnung und Symbole ab; der Kern lehnt eine
// Datei mit falscher Prüfsumme ab. Zeichnen: SchemaSymbolZeichnung. Zuordnung Klasse/art → Symbol: hier (Waehle), Daten im Katalog.

using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Hlks.Kern;

/// <summary>Eine Zeichen-Primitive eines Symbols in HKAP-Millimetern (y nach unten, Ursprung = Symbolmitte): t = line | rect | circle | arc | poly | text;
/// p = line [x1,y1,x2,y2] · rect [x,y,w,h] · circle [cx,cy,r] · arc [cx,cy,r,start,sweep] (Grad im Uhrzeigersinn ab 3 Uhr) · poly [x1,y1,…] · text [x,y].</summary>
public sealed class KatalogGeo
{
    [JsonPropertyName("t")] public string T { get; set; } = "line";
    [JsonPropertyName("p")] public double[] P { get; set; } = [];
    [JsonPropertyName("fill")] public bool Fill { get; set; }
    /// <summary>Füllfarbe; leer + fill = in der Linienfarbe gefüllt.</summary>
    [JsonPropertyName("fc")] public string Fc { get; set; } = "";
    [JsonPropertyName("close")] public bool Close { get; set; }
    /// <summary>Text: mitte (leer) | links | rechts.</summary>
    [JsonPropertyName("align")] public string Align { get; set; } = "";
    /// <summary>Text bleibt waagrecht, auch wenn das Symbol gedreht wird.</summary>
    [JsonPropertyName("aufrecht")] public bool Aufrecht { get; set; }
    /// <summary>Strichstärke in Symbol-mm; 0 = Standard 0.35; negativ = ohne Umriss.</summary>
    [JsonPropertyName("w")] public double W { get; set; }
    [JsonPropertyName("dash")] public bool Dash { get; set; }
    [JsonPropertyName("s")] public string S { get; set; } = "";
    [JsonPropertyName("size")] public double Size { get; set; } = 2.8;
    [JsonPropertyName("bold")] public bool Bold { get; set; }
}

public sealed class KatalogAnschluss
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("x")] public double X { get; set; }
    [JsonPropertyName("y")] public double Y { get; set; }
    /// <summary>N | S | E | W: Richtung, in die die Leitung abgeht (HKAP-Sicht, N = oben).</summary>
    [JsonPropertyName("dir")] public string Dir { get; set; } = "E";
}

public sealed class KatalogTextFeld
{
    [JsonPropertyName("key")] public string Key { get; set; } = "";
    [JsonPropertyName("label")] public string Label { get; set; } = "";
}

/// <summary>Ein Symbol der wirksamen HKAP-Bibliothek.</summary>
public sealed class KatalogSymbol
{
    [JsonPropertyName("key")] public string Key { get; set; } = "";
    [JsonPropertyName("gruppe")] public string Gruppe { get; set; } = "";
    /// <summary>Sparten, leerzeichengetrennt (heizung lueftung lueftungsgeraete sanitaer); leer = überall.</summary>
    [JsonPropertyName("sparte")] public string Sparte { get; set; } = "";
    [JsonPropertyName("bereich")] public string Bereich { get; set; } = "";
    [JsonPropertyName("label")] public string Label { get; set; } = "";
    /// <summary>«HKAP-Code» | «HKAP data/schema_symbole.json (angepasst)» | «HKAP data/schema_symbole.json (eigen)».</summary>
    [JsonPropertyName("herkunft")] public string Herkunft { get; set; } = "";
    /// <summary>Angepasste Symbole: Geometrie und Anschlüsse noch die des HKAP-Codes (nur ausgeblendet bzw. umbenannt).</summary>
    [JsonPropertyName("geometrieWieCode")] public bool? GeometrieWieCode { get; set; }
    /// <summary>In der Palette der HKAP ausgeblendet: bleibt verfügbar, wird aber nicht angeboten.</summary>
    [JsonPropertyName("versteckt")] public bool Versteckt { get; set; }
    /// <summary>senkrecht = Durchfluss oben → unten (an waagrechten Leitungen um 90° gedreht) | waagrecht = Durchfluss links → rechts (an senkrechten
    /// Leitungen gedreht) | fest = steht aufrecht (Apparate, Fühler, Instrumente).</summary>
    [JsonPropertyName("einbau")] public string Einbau { get; set; } = "fest";
    [JsonPropertyName("w")] public double W { get; set; }
    [JsonPropertyName("h")] public double H { get; set; }
    [JsonPropertyName("bx0")] public double Bx0 { get; set; }
    [JsonPropertyName("by0")] public double By0 { get; set; }
    [JsonPropertyName("bx1")] public double Bx1 { get; set; }
    [JsonPropertyName("by1")] public double By1 { get; set; }
    [JsonPropertyName("labelPos")] public string LabelPos { get; set; } = "unten";
    [JsonPropertyName("suchbegriffe")] public string Suchbegriffe { get; set; } = "";
    [JsonPropertyName("standardFarbe")] public string StandardFarbe { get; set; } = "";
    [JsonPropertyName("kennbuchstabe")] public string Kennbuchstabe { get; set; } = "";
    [JsonPropertyName("rolle")] public string Rolle { get; set; } = "";
    [JsonPropertyName("geo")] public List<KatalogGeo> Geo { get; set; } = [];
    [JsonPropertyName("anschluesse")] public List<KatalogAnschluss> Anschluesse { get; set; } = [];
    [JsonPropertyName("textFelder")] public List<KatalogTextFeld> TextFelder { get; set; } = [];

    public bool IstSparte(string sparte) => Sparte.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries).Contains(sparte);
}

/// <summary>Eine Zeile der Zuordnung Klasse (und art) → Symbol. Symbol null = ausdrücklich offen (bleibt die bisherige Darstellung).</summary>
public sealed class SymbolZuordnung
{
    [JsonPropertyName("klasse")] public string Klasse { get; set; } = "";
    [JsonPropertyName("art")] public string? Art { get; set; }
    /// <summary>true: nur für Elemente in einem Kältesystem (Medium KAE-…); null: unabhängig davon.</summary>
    [JsonPropertyName("kaelte")] public bool? Kaelte { get; set; }
    /// <summary>Gewerk des Mediums (heizung umfasst die Kälte); null: unabhängig davon. Sanitär-Ventile behalten so ihre bisherige Darstellung.</summary>
    [JsonPropertyName("gewerk")] public string? Gewerk { get; set; }
    [JsonPropertyName("symbol")] public string? Symbol { get; set; }
    /// <summary>Christian 21.10.2026 | Vorschlag Auftrag | Annahme | offen.</summary>
    [JsonPropertyName("quelle")] public string Quelle { get; set; } = "";
    [JsonPropertyName("annahme")] public string Annahme { get; set; } = "";
}

public sealed class SymbolKatalogKopf
{
    [JsonPropertyName("erzeugt")] public string Erzeugt { get; set; } = "";
    [JsonPropertyName("quelleDaten")] public JsonObject? QuelleDaten { get; set; }
    [JsonPropertyName("quelleCode")] public JsonObject? QuelleCode { get; set; }
    [JsonPropertyName("anzahl")] public JsonObject? Anzahl { get; set; }
    [JsonPropertyName("inhaltspruefsumme")] public string Inhaltspruefsumme { get; set; } = "";
}

public sealed class SchemaSymbolKatalog
{
    public const string Datei = "schemasymbole.json";

    public SymbolKatalogKopf Kopf { get; private set; } = new();
    /// <summary>Millimeter im Schema je Millimeter des HKAP-Symbols (Annahme, im Katalog: darstellung.massstab).</summary>
    public double Massstab { get; private set; } = 1;
    public List<SymbolZuordnung> Zuordnung { get; private set; } = [];
    public List<KatalogSymbol> Symbole { get; private set; } = [];
    readonly Dictionary<string, KatalogSymbol> _nachKey = new(StringComparer.OrdinalIgnoreCase);

    public KatalogSymbol? Symbol(string key) => _nachKey.GetValueOrDefault(key);
    public IEnumerable<KatalogSymbol> Sichtbare => Symbole.Where(s => !s.Versteckt);

    static readonly JsonSerializerOptions Kompakt = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Prüfsumme über die kompakte Form von {darstellung, zuordnung, symbole}; dieselbe Rechnung wie im Werkzeug.</summary>
    public static string Pruefsumme(JsonNode darstellung, JsonNode zuordnung, JsonNode symbole)
    {
        var obj = new JsonObject
        {
            ["darstellung"] = JsonNode.Parse(darstellung.ToJsonString(Kompakt)),
            ["zuordnung"] = JsonNode.Parse(zuordnung.ToJsonString(Kompakt)),
            ["symbole"] = JsonNode.Parse(symbole.ToJsonString(Kompakt)),
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(obj.ToJsonString(Kompakt)))).ToLowerInvariant();
    }

    /// <summary>Liest den Katalog; lehnt ihn ab, wenn die Prüfsumme nicht stimmt (Handänderung) oder Pflichtteile fehlen.</summary>
    public static SchemaSymbolKatalog Laden(string datei)
    {
        JsonNode wurzel;
        try { wurzel = JsonNode.Parse(File.ReadAllText(datei, Encoding.UTF8))!; }
        catch (Exception e) when (e is JsonException or IOException) { throw new HlksFehler($"Katalog {Datei} ist nicht lesbar: {e.Message}", "Mit werkzeuge/hkap_symbole_uebernehmen neu erzeugen."); }
        if (wurzel["kopf"] is not JsonObject kopf || wurzel["darstellung"] is null || wurzel["zuordnung"] is null || wurzel["symbole"] is null)
            throw new HlksFehler($"Katalog {Datei}: kopf, darstellung, zuordnung oder symbole fehlt.", "Mit werkzeuge/hkap_symbole_uebernehmen neu erzeugen.");
        var soll = kopf["inhaltspruefsumme"]?.GetValue<string>() ?? "";
        var ist = Pruefsumme(wurzel["darstellung"]!, wurzel["zuordnung"]!, wurzel["symbole"]!);
        if (!string.Equals(soll, ist, StringComparison.OrdinalIgnoreCase))
            throw new HlksFehler($"Katalog {Datei} wurde nicht mit dem Werkzeug erzeugt oder danach verändert (Prüfsumme stimmt nicht).",
                "Nie von Hand ändern: Änderungen in der HKAP-Symbolbibliothek bzw. in werkzeuge/hkap_symbole_uebernehmen/zuordnung.json vornehmen und das Werkzeug neu laufen lassen.");
        var k = new SchemaSymbolKatalog
        {
            Kopf = kopf.Deserialize<SymbolKatalogKopf>() ?? new(),
            Massstab = wurzel["darstellung"]!["massstab"]?.GetValue<double>() ?? 1,
            Zuordnung = wurzel["zuordnung"].Deserialize<List<SymbolZuordnung>>() ?? [],
            Symbole = wurzel["symbole"].Deserialize<List<KatalogSymbol>>() ?? [],
        };
        foreach (var s in k.Symbole) k._nachKey[s.Key] = s;
        foreach (var z in k.Zuordnung)
            if (z.Symbol is not null && !k._nachKey.ContainsKey(z.Symbol))
                throw new HlksFehler($"Katalog {Datei}: Zuordnung {z.Klasse}/{z.Art}: Symbol «{z.Symbol}» fehlt.", "Mit werkzeuge/hkap_symbole_uebernehmen neu erzeugen.");
        return k;
    }

    // ------------------------------------------------------------------ Zuordnung

    /// <summary>Kälte: Kälteerzeuger immer, sonst Medium KAE-… des Systems bzw. (ohne System) aller Anschlüsse mit festem Medium.</summary>
    public static bool IstKaelte(Projekt p, Element e)
    {
        if (p.Katalog.IstArt(e, "Kaelteerzeuger")) return true;
        var medium = Medium(p, e);
        return medium is not null && KaelteAlias.Medium(medium).StartsWith("KAE-", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Medium des Elements: das des Systems, sonst das feste Medium seiner Anschlüsse (alle gleich), sonst null.</summary>
    static string? Medium(Projekt p, Element e)
    {
        var m = e.System is null ? null : Netz.SystemMedium(p.Modell, e.System);
        if (!string.IsNullOrEmpty(m)) return m;
        var fest = e.Anschluesse.Select(a => Netz.Medium(p.Modell, e, a)).Where(x => x != "*").Distinct().ToList();
        return fest.Count == 1 ? fest[0] : null;
    }

    /// <summary>Gewerk (heizung | sanitaer | lueftung) des Mediums des Elements oder null.</summary>
    public static string? Gewerk(Projekt p, Element e) =>
        Medium(p, e) is { } m && p.Katalog.Medien.TryGetValue(m, out var d) ? d.Gewerk : null;

    /// <summary>Symbol der Klasse und art des Elements nach der Zuordnung im Katalog; null, wenn keine Zeile greift oder die Zeile ausdrücklich offen ist.
    /// Eine Klasse, die der Klassenkatalog nicht kennt, lässt die Zeile ins Leere laufen (kein Fehler).</summary>
    public KatalogSymbol? Waehle(Projekt p, Element e)
    {
        string? art = null;
        bool artGeholt = false;
        bool? kaelte = null;
        string? gewerk = null;
        bool gewerkGeholt = false;
        foreach (var z in Zuordnung)
        {
            if (!p.Katalog.Klassen.ContainsKey(z.Klasse) || !p.Katalog.IstArt(e, z.Klasse)) continue;
            if (z.Art is not null)
            {
                if (!artGeholt) { art = e.Text("art"); artGeholt = true; }
                if (!string.Equals(z.Art, art, StringComparison.OrdinalIgnoreCase)) continue;
            }
            if (z.Kaelte is true && !(kaelte ??= IstKaelte(p, e))) continue;
            if (z.Gewerk is not null)
            {
                if (!gewerkGeholt) { gewerk = Gewerk(p, e); gewerkGeholt = true; }
                if (!string.Equals(z.Gewerk, gewerk, StringComparison.OrdinalIgnoreCase)) continue;
            }
            return z.Symbol is null ? null : Symbol(z.Symbol);
        }
        return null;
    }
}
