// Quest App V1a, Vorgabe Christian 24.10.2026 (nach den Bildern 02): «das grau ist nicht so ansprechend. verwende unsere pastelfarben.» Berichtigung: «gewerke werden
// nur für gewerkeunterschiede gebraucht» – die App hat einen eigenen Katalog kataloge/xr_farben.json (App-Pastellreihe, Christian 24.10.2026), keine Farbwerte im Code.
// Je App ein Ton plus Akzent, hell und dunkel; Schrift dunkel auf hellem Pastell (wie KICAD), hell auf der dunklen Fassung.
//   - Welche Fassung: hell, wenn die Umgebung mindestens «hellAb» hell ist (Begehen im Modell, Passthrough, heller Sitzungsraum), sonst dunkel (Sitzungsraum «tron»).
//     Die App-Symbole bleiben auch dunkel in der hellen Fassung (Pastell-Scheibe, dunkles Symbol); die Fenster der dunklen Fassung tragen einen kräftigen dunklen
//     Ton der App (Akzent mit Deckkraft 0.75 über dem dunklen Grund) und den Pastellton als Akzent (Befund Prüfbilder 03: mit 22 % Pastell war dunkel alles grau).
//   - Glas: der Ton der Fassung, halbtransparent; die Deckkraft richtet sich weiter nach der Umgebung (Apple HIG «Materials»: «Glass is an adaptive material …»).
//     Hell: das Glas wirkt mindestens HellMindestens hell (dunkle Schrift bleibt lesbar), dunkel: höchstens so hell wie die Stellschraube «anzeige.glas_helligkeit».
//     Deckkraft mindestens «anzeige.glas_deckkraft», höchstens 0.95 (Mischung a·Ton + (1 − a)·Umgebung, sRGB).
//   - Knöpfe auf dem Glas («thin material»): auf hellem Glas leicht dunkler, auf dunklem leicht heller; eingeschaltet: Akzent mit der Schrift, die darauf besser lesbar ist.
// Rein, ohne StereoKit. Die App liefert den Katalog mit (eingebettet, Hlks.Quest.csproj); Zahlen ausser den Farben sind Annahmen V1a.

using System.Globalization;
using System.Text.Json;

namespace Hlks.Xr.Bedienung;

/// <summary>Farben einer Fassung (hell bzw. dunkel): Ton der Fläche, Akzent, Schrift.</summary>
public sealed record FarbFassung(GlasFarbe Ton, GlasFarbe Akzent, GlasFarbe Schrift);

/// <summary>Farben einer App aus dem Katalog.</summary>
public sealed record AppFarbe(string Id, string Titel, FarbFassung Hell, FarbFassung Dunkel, string Quelle);

/// <summary>
/// Die Farben eines Bildes für eine App (aus Katalog, Umgebung und Stellschrauben): Glas, Akzent, Schrift in drei Stufen, Knöpfe, eingeschaltet; Hell = helle Fassung
/// (dunkle Schrift, Symbole im hellen Satz).
/// </summary>
public sealed record GlasSatz(GlasFarbe Glas, GlasFarbe Akzent, GlasFarbe Text, GlasFarbe TextSekundaer, GlasFarbe TextTertiaer, GlasFarbe Duenn, GlasFarbe DuennNah,
    GlasFarbe DuennGedrueckt, GlasFarbe Ein, GlasFarbe TextAufEin, bool Hell);

public sealed class AppFarben
{
    public const string Art = "hlks-xr-farben";
    public const int Fassung = 1;
    /// <summary>Eintrag für alles ohne eigenen Ton (Schliessen, Tooltips ohne App, unbekannte Apps).</summary>
    public const string VorgabeId = "vorgabe";
    /// <summary>Die Einträge, die der Katalog haben muss (Apps der Symbole, Gesten, Labor, Vorgabe).</summary>
    public static readonly IReadOnlyList<string> Pflicht = ["status", "etagen", "messen", "marker", "tisch", "ort", "ebenen", "gesten", "labor", VorgabeId];

    /// <summary>Helles Glas wirkt mindestens so hell (sRGB), damit dunkle Schrift lesbar bleibt, Annahme V1a.</summary>
    public const double HellMindestens = 0.72;
    /// <summary>Höchste Deckkraft des Glases.</summary>
    public const double DeckkraftMax = 0.95;

    public IReadOnlyDictionary<string, AppFarbe> Apps { get; }
    public double HellAb { get; }
    public GlasFarbe SchriftHell { get; }
    public GlasFarbe SchriftDunkel { get; }

    AppFarben(Dictionary<string, AppFarbe> apps, double hellAb, GlasFarbe schriftHell, GlasFarbe schriftDunkel)
    {
        Apps = apps;
        HellAb = hellAb;
        SchriftHell = schriftHell;
        SchriftDunkel = schriftDunkel;
    }

    /// <summary>Die Farben einer App bzw. die Vorgabe.</summary>
    public AppFarbe Fuer(string? id) => id is not null && Apps.TryGetValue(id, out var f) ? f : Apps[VorgabeId];

    /// <summary>Gilt bei dieser Helligkeit der Umgebung die helle Fassung?</summary>
    public bool IstHell(double hintergrund) => hintergrund >= HellAb;

    /// <summary>Farbe aus «#RRGGBB» (sRGB 0 … 1); wirft FormatException.</summary>
    public static GlasFarbe Hex(string? text, double a = 1)
    {
        if (text is not { Length: 7 } t || t[0] != '#' || !int.TryParse(t.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
            throw new FormatException($"Farbe «{text}»: erwartet #RRGGBB.");
        return new GlasFarbe(((v >> 16) & 255) / 255.0, ((v >> 8) & 255) / 255.0, (v & 255) / 255.0, a);
    }

    /// <summary>Liest und prüft den Katalog (kataloge/xr_farben.json). Wirft FormatException mit der Stelle.</summary>
    public static AppFarben Lesen(string json)
    {
        using var d = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
        var o = d.RootElement;
        string Text(JsonElement e, string name, string wo) =>
            e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var x) && x.ValueKind == JsonValueKind.String && x.GetString() is { Length: > 0 } s
                ? s : throw new FormatException($"xr_farben.json: {wo}.{name} fehlt.");
        if (o.ValueKind != JsonValueKind.Object || Text(o, "art", "") != Art) throw new FormatException($"xr_farben.json: «art» muss «{Art}» sein.");
        if (!o.TryGetProperty("fassung", out var fa) || fa.ValueKind != JsonValueKind.Number || fa.GetInt32() != Fassung) throw new FormatException($"xr_farben.json: «fassung» muss {Fassung} sein.");
        if (!o.TryGetProperty("hellAb", out var ha) || ha.ValueKind != JsonValueKind.Number || ha.GetDouble() is < 0 or > 1) throw new FormatException("xr_farben.json: «hellAb» muss eine Zahl von 0 bis 1 sein.");
        if (!o.TryGetProperty("schrift", out var sc)) throw new FormatException("xr_farben.json: «schrift» fehlt.");
        var schriftHell = Hex(Text(sc, "hell", "schrift"));
        var schriftDunkel = Hex(Text(sc, "dunkel", "schrift"));
        if (!o.TryGetProperty("apps", out var apps) || apps.ValueKind != JsonValueKind.Array) throw new FormatException("xr_farben.json: «apps» muss eine Liste sein.");
        var liste = new Dictionary<string, AppFarbe>(StringComparer.Ordinal);
        foreach (var a in apps.EnumerateArray())
        {
            var id = Text(a, "id", "apps");
            FarbFassung F(string name, GlasFarbe schrift) =>
                a.TryGetProperty(name, out var f) ? new FarbFassung(Hex(Text(f, "ton", $"{id}.{name}")), Hex(Text(f, "akzent", $"{id}.{name}")), schrift)
                    : throw new FormatException($"xr_farben.json: {id}.{name} fehlt.");
            if (!liste.TryAdd(id, new AppFarbe(id, Text(a, "titel", id), F("hell", schriftHell), F("dunkel", schriftDunkel), Text(a, "quelle", id))))
                throw new FormatException($"xr_farben.json: «{id}» kommt doppelt vor.");
        }
        if (Pflicht.FirstOrDefault(p => !liste.ContainsKey(p)) is { } fehlt) throw new FormatException($"xr_farben.json: Eintrag «{fehlt}» fehlt.");
        return new AppFarben(liste, ha.GetDouble(), schriftHell, schriftDunkel);
    }

    // ---------------------------------------------------------------------------------------------------- Kontrast (WCAG 2.x, relative Leuchtdichte)

    static double Linear(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

    /// <summary>Relative Leuchtdichte einer Farbe (sRGB, ohne Deckkraft).</summary>
    public static double Leuchtdichte(GlasFarbe f) => 0.2126 * Linear(f.R) + 0.7152 * Linear(f.G) + 0.0722 * Linear(f.B);

    /// <summary>Kontrastverhältnis zweier Farben (1 … 21).</summary>
    public static double Kontrast(GlasFarbe a, GlasFarbe b)
    {
        double x = Leuchtdichte(a), y = Leuchtdichte(b);
        return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
    }

    /// <summary>Wie eine Fläche über einem grauen Hintergrund wirkt (Mischung a·Farbe + (1 − a)·Hintergrund, sRGB).</summary>
    public static GlasFarbe Ueber(GlasFarbe f, double hintergrund) =>
        new(f.A * f.R + (1 - f.A) * hintergrund, f.A * f.G + (1 - f.A) * hintergrund, f.A * f.B + (1 - f.A) * hintergrund, 1);

    static double Grau(GlasFarbe f) => (f.R + f.G + f.B) / 3;

    /// <summary>
    /// Deckkraft des Glases mit dem Ton über dem Hintergrund: mindestens deckkraft; hell: das Glas wirkt mindestens <see cref="HellMindestens"/> hell, dunkel: höchstens
    /// zielDunkel hell (Stellschraube anzeige.glas_helligkeit); höchstens <see cref="DeckkraftMax"/>.
    /// </summary>
    public static double Deckkraft(GlasFarbe ton, bool hell, double hintergrund, double deckkraft, double zielDunkel)
    {
        var bg = Math.Clamp(hintergrund, 0, 1);
        var g = Grau(ton);
        var a = Math.Clamp(deckkraft, 0.3, DeckkraftMax);
        if (hell && bg < HellMindestens && g > bg) a = Math.Max(a, (HellMindestens - bg) / (g - bg));
        if (!hell && bg > zielDunkel && g < bg) a = Math.Max(a, (bg - zielDunkel) / (bg - g));
        return Math.Clamp(a, 0.3, DeckkraftMax);
    }

    /// <summary>
    /// Die Farben eines Bildes für die App id bei dieser Umgebung und diesen Stellschrauben (siehe Dateikopf). immerHell: die helle Fassung auch in dunkler Umgebung
    /// (die App-Symbole: helle Pastell-Scheibe mit dunklem Symbol, damit die Farbe der App erkennbar bleibt – Befund Prüfbilder 03, dunkel war alles grau).
    /// </summary>
    public GlasSatz Satz(string? id, double hintergrund, double deckkraft, double zielDunkel, bool immerHell = false)
    {
        var hell = immerHell || IstHell(hintergrund);
        var f = hell ? Fuer(id).Hell : Fuer(id).Dunkel;
        var glas = f.Ton with { A = Deckkraft(f.Ton, hell, hintergrund, deckkraft, zielDunkel) };
        var s = f.Schrift;
        // Knöpfe auf dem Glas: auf hellem Glas schwarz, auf dunklem weiss, leicht deckend (wie «thin material»)
        var duenn = hell ? new GlasFarbe(0, 0, 0, 0.07) : new GlasFarbe(1, 1, 1, 0.12);
        var nah = duenn with { A = duenn.A * 1.9 };
        var gedrueckt = duenn with { A = duenn.A * 2.7 };
        var ein = f.Akzent with { A = 0.96 };
        var aufEin = Kontrast(f.Akzent, SchriftHell) >= Kontrast(f.Akzent, SchriftDunkel) ? SchriftHell : SchriftDunkel;
        return new GlasSatz(glas, f.Akzent, s, s with { A = 0.72 }, s with { A = 0.5 }, duenn, nah, gedrueckt, ein, aufEin, hell);
    }
}
