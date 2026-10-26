// XR-Labor Spielwiese S1 (entwurf/xr-empfaenger.md, «Neuausrichtung des Labors 26.10.2026»; entwurf/bedien-grundsatz.md, Kapitel 1 App-Menü): neutrale
// Platzhalter-Apps, mit denen die Grundbedienung von «H+K XR» ohne KICAD-Inhalt erlebbar ist. Christian, 26.10.2026: «Aufrufen von Apps: Bsp. Appmenue aufrufen, apps
// wählen, menue schliessen. das labor wird durch dich bedient, wir feilen dann nur an diesem grundsatz.» Die Apps stehen im Katalog kataloge/labor_spielwiese.json
// (nicht im Code), damit Claude Apps und Inhalte ändern kann, ohne die APK neu zu bauen. Der Labor-Server liefert ihn (GET /api/xr/labor/spielwiese, auch über den
// Netzzugang, ETag/304; src/Hlks.Labor/SpielwieseEndpunkt.cs) und prüft ihn mit diesem Leser; die App liest ihn mit demselben (Hlks.Xr.Netz.SpielwieseLader).
//
// Aufbau (Fassung 1):
//   { art: "hlks-labor-spielwiese", fassung: 1, beschreibung,
//     apps: [{ kennung, name, symbol, farbe, inhalt: { art, … } }] }
//   kennung   [a-z][a-z0-9-]{0,23}, eindeutig                         name    1 … 24 Zeichen (Tooltip im Symbolfeld, Titel der Tafel)
//   symbol    Hlks.Xr.Bedienung.GlasSymbole.Bekannt (Textur oder «@…»)   farbe   Eintrag in kataloge/xr_farben.json (geprüft, wenn die Liste mitgegeben ist)
//   inhalt.art:
//     text      { text }                                    1 … 1200 Zeichen
//     seiten    { seiten: [{ titel, text }] }               1 … 12 Seiten, Titel bis 60, Text 1 … 1200 Zeichen
//     liste     { eintraege: [{ titel, zeile? }] }          1 … 200 Einträge, Titel bis 60, Zeile bis 80
//               oder { anzahl, muster, zeile? }             anzahl 1 … 200, muster mit «{n}» (bis 60), zeile bis 80 mit «{n}» und «{anzahl}»
//     schalter  { schalter: [{ id, titel, ein, zeile? }] }  1 … 12, id [a-z][a-z0-9-]{0,23} eindeutig, Titel bis 40, Zeile bis 60
//     kacheln   { kacheln: [{ titel, farbe, form }] }       1 … 12, Titel bis 24, farbe #RRGGBB, form kreis | quadrat | kapsel | ring | streifen
//     uhr       { }                                         Zeit und Datum der Brille
//   1 … 11 Apps: mit «Werkzeuge» höchstens zwölf Symbole im Feld (drei Reihen; Annahme S1).
// Unbekannte Felder sind erlaubt (Hinweis), damit eine spätere Fassung die App nicht bricht. Rein, ohne StereoKit.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Hlks.Xr.Bedienung;
using Hlks.Xr.Netz;

namespace Hlks.Xr.Labor;

/// <summary>Eine Seite der App-Art «seiten».</summary>
public sealed record SpielwieseSeite(string Titel, string Text);

/// <summary>Ein Eintrag der App-Art «liste».</summary>
public sealed record SpielwieseEintrag(string Titel, string? Zeile);

/// <summary>Ein Schalter der App-Art «schalter» (ohne Wirkung; ein = Anfangszustand).</summary>
public sealed record SpielwieseSchalter(string Id, string Titel, bool Ein, string? Zeile);

/// <summary>Eine Kachel der App-Art «kacheln»: Farbfläche mit einfacher Form (keine echten Bilder).</summary>
public sealed record SpielwieseKachel(string Titel, string Farbe, string Form);

/// <summary>Inhalt einer Platzhalter-App; je nach <see cref="Art"/> ist nur ein Teil gefüllt.</summary>
public sealed record SpielwieseInhalt(string Art, string? Text, IReadOnlyList<SpielwieseSeite> Seiten, IReadOnlyList<SpielwieseEintrag> Eintraege,
    IReadOnlyList<SpielwieseSchalter> Schalter, IReadOnlyList<SpielwieseKachel> Kacheln);

/// <summary>Eine Platzhalter-App der Spielwiese.</summary>
public sealed record SpielwieseApp(string Kennung, string Name, string Symbol, string Farbe, SpielwieseInhalt Inhalt);

/// <summary>Der Katalog der Spielwiese (siehe Dateikopf).</summary>
public sealed class SpielwieseKatalog
{
    public const string Art = "hlks-labor-spielwiese";
    public const int Fassung = 1;
    /// <summary>Dateiname im Ordner der Kataloge.</summary>
    public const string Datei = "labor_spielwiese.json";

    /// <summary>Arten des Inhalts.</summary>
    public const string ArtText = "text", ArtSeiten = "seiten", ArtListe = "liste", ArtSchalter = "schalter", ArtKacheln = "kacheln", ArtUhr = "uhr";
    public static readonly IReadOnlyList<string> Arten = [ArtText, ArtSeiten, ArtListe, ArtSchalter, ArtKacheln, ArtUhr];
    /// <summary>Formen der Kacheln.</summary>
    public static readonly IReadOnlyList<string> Formen = ["kreis", "quadrat", "kapsel", "ring", "streifen"];

    /// <summary>Grenzen (Annahmen S1, siehe Dateikopf).</summary>
    public const int AppsMax = 11, NameMax = 24, TextMax = 1200, SeitenMax = 12, SeitenTitelMax = 60, EintraegeMax = 200, EintragTitelMax = 60, EintragZeileMax = 80,
        SchalterMax = 12, SchalterTitelMax = 40, SchalterZeileMax = 60, KachelnMax = 12, KachelTitelMax = 24, BeschreibungMax = 4000;

    static readonly Regex KennungMuster = new("^[a-z][a-z0-9-]{0,23}$", RegexOptions.CultureInvariant);
    static readonly Regex FarbIdMuster = new("^[a-z][a-z0-9-]{0,31}$", RegexOptions.CultureInvariant);
    static readonly Regex HexMuster = new("^#[0-9A-Fa-f]{6}$", RegexOptions.CultureInvariant);

    public string Beschreibung { get; }
    public IReadOnlyList<SpielwieseApp> Apps { get; }
    /// <summary>Kurzer Schlüssel des Texts (SHA-256, 16 Zeichen): gleich, solange sich der Katalog nicht ändert (ETag am Labor-PC, Protokoll der App).</summary>
    public string Schluessel { get; }
    /// <summary>Hinweise beim Lesen (unbekannte Felder).</summary>
    public IReadOnlyList<string> Hinweise { get; }

    SpielwieseKatalog(string beschreibung, List<SpielwieseApp> apps, string schluessel, List<string> hinweise)
    {
        Beschreibung = beschreibung;
        Apps = apps;
        Schluessel = schluessel;
        Hinweise = hinweise;
    }

    /// <summary>Die App zu einer Kennung, sonst null.</summary>
    public SpielwieseApp? App(string? kennung) => Apps.FirstOrDefault(a => a.Kennung == kennung);

    /// <summary>Schlüssel eines Texts (SHA-256 der UTF-8-Bytes, die ersten 16 Hex-Zeichen klein).</summary>
    public static string SchluesselVon(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16].ToLowerInvariant();

    /// <summary>
    /// Liest und prüft den Katalog. farben = die Einträge von kataloge/xr_farben.json (null = Farbe nur nach Muster prüfen). Wirft <see cref="XrNetzFehler"/> mit der
    /// Stelle, wenn der Katalog nicht gilt.
    /// </summary>
    public static SpielwieseKatalog Lesen(string json, IReadOnlyCollection<string>? farben = null)
    {
        JsonDocument d;
        try { d = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 }); }
        catch (JsonException ex) { throw new XrNetzFehler("Der Katalog der Spielwiese ist kein gültiges JSON: " + ex.Message, Vorschlag); }
        using (d)
        {
            var hinweise = new List<string>();
            var r = d.RootElement;
            if (r.ValueKind != JsonValueKind.Object) throw Fehler("", "muss ein Objekt sein");
            if (TextFeld(r, "art", "", 64) != Art) throw Fehler("art", $"muss «{Art}» sein");
            if (!r.TryGetProperty("fassung", out var fa) || fa.ValueKind != JsonValueKind.Number || !fa.TryGetInt32(out var f) || f != Fassung)
                throw Fehler("fassung", $"muss {Fassung} sein");
            var beschreibung = TextFeld(r, "beschreibung", "", BeschreibungMax);
            Unbekannt(r, "", ["art", "fassung", "beschreibung", "apps"], hinweise);
            if (!r.TryGetProperty("apps", out var apps) || apps.ValueKind != JsonValueKind.Array) throw Fehler("apps", "muss eine Liste sein");
            var n = apps.GetArrayLength();
            if (n is < 1 or > AppsMax) throw Fehler("apps", $"1 bis {AppsMax} Apps, nicht {n}");
            var liste = new List<SpielwieseApp>();
            var i = 0;
            foreach (var a in apps.EnumerateArray())
            {
                var wo = $"apps[{i++}]";
                if (a.ValueKind != JsonValueKind.Object) throw Fehler(wo, "muss ein Objekt sein");
                var kennung = TextFeld(a, "kennung", wo, 24);
                if (!KennungMuster.IsMatch(kennung)) throw Fehler($"{wo}.kennung", $"«{kennung}» passt nicht zu [a-z][a-z0-9-]{{0,23}}");
                if (liste.Any(x => x.Kennung == kennung)) throw Fehler($"{wo}.kennung", $"«{kennung}» kommt doppelt vor");
                wo = $"apps[{kennung}]";
                var name = TextFeld(a, "name", wo, NameMax);
                var symbol = TextFeld(a, "symbol", wo, 64);
                if (!GlasSymbole.Bekannt(symbol))
                    throw Fehler($"{wo}.symbol", $"«{symbol}» gibt es nicht (Zeichen: {string.Join(", ", GlasSymbole.Gezeichnet)}; Texturen: {string.Join(", ", GlasSymbole.Alle)})");
                var farbe = TextFeld(a, "farbe", wo, 32);
                if (!FarbIdMuster.IsMatch(farbe)) throw Fehler($"{wo}.farbe", $"«{farbe}» ist keine Kennung aus kataloge/xr_farben.json");
                if (farben is not null && !farben.Contains(farbe)) throw Fehler($"{wo}.farbe", $"«{farbe}» steht nicht in kataloge/xr_farben.json");
                Unbekannt(a, wo, ["kennung", "name", "symbol", "farbe", "inhalt"], hinweise);
                if (!a.TryGetProperty("inhalt", out var inh) || inh.ValueKind != JsonValueKind.Object) throw Fehler($"{wo}.inhalt", "fehlt bzw. ist kein Objekt");
                liste.Add(new SpielwieseApp(kennung, name, symbol, farbe, InhaltLesen(inh, $"{wo}.inhalt", hinweise)));
            }
            return new SpielwieseKatalog(beschreibung, liste, SchluesselVon(json), hinweise);
        }
    }

    static SpielwieseInhalt InhaltLesen(JsonElement e, string wo, List<string> hinweise)
    {
        var art = TextFeld(e, "art", wo, 16);
        if (!Arten.Contains(art)) throw Fehler($"{wo}.art", $"«{art}» gibt es nicht ({string.Join(" | ", Arten)})");
        List<SpielwieseSeite> seiten = [];
        List<SpielwieseEintrag> eintraege = [];
        List<SpielwieseSchalter> schalter = [];
        List<SpielwieseKachel> kacheln = [];
        string? text = null;
        switch (art)
        {
            case ArtText:
                text = TextFeld(e, "text", wo, TextMax);
                Unbekannt(e, wo, ["art", "text"], hinweise);
                break;
            case ArtSeiten:
                foreach (var (s, w) in Liste(e, "seiten", wo, SeitenMax))
                    seiten.Add(new SpielwieseSeite(TextFeld(s, "titel", w, SeitenTitelMax), TextFeld(s, "text", w, TextMax)));
                Unbekannt(e, wo, ["art", "seiten"], hinweise);
                break;
            case ArtListe:
                if (e.TryGetProperty("eintraege", out _))
                {
                    foreach (var (s, w) in Liste(e, "eintraege", wo, EintraegeMax))
                        eintraege.Add(new SpielwieseEintrag(TextFeld(s, "titel", w, EintragTitelMax), TextFeldOder(s, "zeile", w, EintragZeileMax)));
                    Unbekannt(e, wo, ["art", "eintraege"], hinweise);
                    break;
                }
                if (!e.TryGetProperty("anzahl", out var an) || an.ValueKind != JsonValueKind.Number || !an.TryGetInt32(out var anzahl) || anzahl is < 1 or > EintraegeMax)
                    throw Fehler($"{wo}.anzahl", $"«eintraege» oder «anzahl» 1 bis {EintraegeMax} nötig");
                var muster = TextFeld(e, "muster", wo, EintragTitelMax);
                if (!muster.Contains("{n}", StringComparison.Ordinal)) throw Fehler($"{wo}.muster", "muss «{n}» enthalten");
                var zeile = TextFeldOder(e, "zeile", wo, EintragZeileMax);
                for (int k = 1; k <= anzahl; k++)
                    eintraege.Add(new SpielwieseEintrag(Einsetzen(muster, k, anzahl), zeile is null ? null : Einsetzen(zeile, k, anzahl)));
                Unbekannt(e, wo, ["art", "anzahl", "muster", "zeile"], hinweise);
                break;
            case ArtSchalter:
                foreach (var (s, w) in Liste(e, "schalter", wo, SchalterMax))
                {
                    var id = TextFeld(s, "id", w, 24);
                    if (!KennungMuster.IsMatch(id)) throw Fehler($"{w}.id", $"«{id}» passt nicht zu [a-z][a-z0-9-]{{0,23}}");
                    if (schalter.Any(x => x.Id == id)) throw Fehler($"{w}.id", $"«{id}» kommt doppelt vor");
                    if (!s.TryGetProperty("ein", out var ein) || ein.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw Fehler($"{w}.ein", "muss true oder false sein");
                    schalter.Add(new SpielwieseSchalter(id, TextFeld(s, "titel", w, SchalterTitelMax), ein.GetBoolean(), TextFeldOder(s, "zeile", w, SchalterZeileMax)));
                }
                Unbekannt(e, wo, ["art", "schalter"], hinweise);
                break;
            case ArtKacheln:
                foreach (var (s, w) in Liste(e, "kacheln", wo, KachelnMax))
                {
                    var farbe = TextFeld(s, "farbe", w, 7);
                    if (!HexMuster.IsMatch(farbe)) throw Fehler($"{w}.farbe", $"«{farbe}»: erwartet #RRGGBB");
                    var form = TextFeld(s, "form", w, 16);
                    if (!Formen.Contains(form)) throw Fehler($"{w}.form", $"«{form}» gibt es nicht ({string.Join(" | ", Formen)})");
                    kacheln.Add(new SpielwieseKachel(TextFeld(s, "titel", w, KachelTitelMax), farbe.ToUpperInvariant(), form));
                }
                Unbekannt(e, wo, ["art", "kacheln"], hinweise);
                break;
            case ArtUhr:
                Unbekannt(e, wo, ["art"], hinweise);
                break;
        }
        return new SpielwieseInhalt(art, text, seiten, eintraege, schalter, kacheln);
    }

    /// <summary>«{n}» und «{anzahl}» im Muster einer Liste ersetzen.</summary>
    public static string Einsetzen(string muster, int n, int anzahl) =>
        muster.Replace("{n}", n.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{anzahl}", anzahl.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

    static IEnumerable<(JsonElement, string)> Liste(JsonElement e, string name, string wo, int max)
    {
        if (!e.TryGetProperty(name, out var l) || l.ValueKind != JsonValueKind.Array) throw Fehler($"{wo}.{name}", "muss eine Liste sein");
        var n = l.GetArrayLength();
        if (n < 1 || n > max) throw Fehler($"{wo}.{name}", $"1 bis {max} Einträge, nicht {n}");
        var aus = new List<(JsonElement, string)>();
        var i = 0;
        foreach (var x in l.EnumerateArray())
        {
            var w = $"{wo}.{name}[{i++}]";
            if (x.ValueKind != JsonValueKind.Object) throw Fehler(w, "muss ein Objekt sein");
            aus.Add((x, w));
        }
        return aus;
    }

    static string TextFeld(JsonElement e, string name, string wo, int max)
    {
        if (!e.TryGetProperty(name, out var x) || x.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(x.GetString()))
            throw Fehler(Pfad(wo, name), "fehlt bzw. ist kein Text");
        var s = x.GetString()!.Trim();
        if (s.Length > max) throw Fehler(Pfad(wo, name), $"höchstens {max} Zeichen, nicht {s.Length}");
        return s;
    }

    static string? TextFeldOder(JsonElement e, string name, string wo, int max) =>
        e.TryGetProperty(name, out var x) && x.ValueKind != JsonValueKind.Null ? TextFeld(e, name, wo, max) : null;

    static void Unbekannt(JsonElement e, string wo, string[] bekannt, List<string> hinweise)
    {
        foreach (var p in e.EnumerateObject())
            if (!bekannt.Contains(p.Name)) hinweise.Add($"{Pfad(wo, p.Name)}: unbekanntes Feld, übergangen");
    }

    static string Pfad(string wo, string name) => wo.Length == 0 ? name : $"{wo}.{name}";

    const string Vorschlag = "kataloge/labor_spielwiese.json nach dem Aufbau im Kopf von src/Hlks.Xr/Labor/Spielwiese.cs berichtigen.";

    static XrNetzFehler Fehler(string wo, string was) => new($"Katalog der Spielwiese: {(wo.Length > 0 ? wo + " " : "")}{was}.", Vorschlag);
}
