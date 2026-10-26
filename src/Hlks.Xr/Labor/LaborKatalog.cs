// Quest App P5b (Modul XR, Interface-Labor): der Katalog des Labors in der App. KICAD liefert ihn mit GET /api/xr/labor/katalog (src/Hlks.Web/XrLaborKatalog.cs,
// Dateien kataloge/xr_stellschrauben.json, xr_pruefaufgaben.json, xr_varianten.json; Vertrag in src/Hlks.Web/OBERFLAECHE.md, «Modul XR: XR-Labor»):
//   { art: "hlks-xr-labor-katalog", fassung: 1, schluessel, gruppen, stellschrauben[], muster, messgroessen, aufgaben[], varianten[], profil: { kennung, name, werte } | null }
// Die App braucht daraus: je Stellschraube Art, Grenzen (min/max) bzw. Auswahl und Vorgabe; die Prüfaufgaben (Titel, Anleitung, Status, zählende Varianten,
// Messgrössen, Erfolgskriterium, Fehlgriff); die Varianten (Optionen, Vorgabe); das Profil «gilt für die App».
// Lesen ist streng beim Kopf (art, fassung) und tolerant bei den Einträgen: ein unbrauchbarer Eintrag fällt weg und steht in den Hinweisen (KICAD prüft die
// Kataloge schon beim Laden streng; zusätzliche Felder einer neueren Fassung stören nicht). Rein, ohne StereoKit und ohne Netz.

using System.Text.Json;

namespace Hlks.Xr.Labor;

/// <summary>Art einer Stellschraube im Katalog.</summary>
public enum StellschraubeArt { Zahl, JaNein, Auswahl }

/// <summary>Urteil über einen Wert vom PC: gilt, ungültig (Typ, Grenzen, Auswahl) bzw. der Schlüssel ist unbekannt.</summary>
public enum WertUrteil { Ok, Ungueltig, Unbekannt }

/// <summary>Eine Stellschraube des Katalogs: Schlüssel, Art, Grenzen (Zahl), erlaubte Werte (Auswahl), Vorgabe, Status (app | folgt_p5), Titel.</summary>
public sealed record LaborStellschraube(string Schluessel, StellschraubeArt Art, double Min, double Max, IReadOnlyList<string> Auswahl, JsonElement Vorgabe, string Status,
    string Titel);

/// <summary>
/// Eine Prüfaufgabe: Id, Titel, Anleitung (für die Brille), Status (p5 = die App spielt sie ab; folgt_v1 = gesperrt), Varianten, die für sie zählen, Messgrössen,
/// Erfolgskriterium, Ort und – P5b – die Festlegung, was als Fehlgriff zählt.
/// </summary>
public sealed record LaborAufgabeDef(string Id, string Titel, string Anleitung, string Status, IReadOnlyList<string> Varianten, IReadOnlyList<string> Messgroessen,
    string Erfolgskriterium, string? Ort, string? Fehlgriff)
{
    public bool Spielbar => Status == "p5";
}

/// <summary>Eine Variante: Name, Titel, Optionen (Ids), Vorgabe.</summary>
public sealed record LaborVarianteDef(string Name, string Titel, IReadOnlyList<string> Optionen, string Vorgabe);

/// <summary>Das Profil, das am PC «gilt für die App» (bereinigt vom Server): Kennung, Name, Werte (Schlüssel → Wert).</summary>
public sealed record LaborProfil(string Kennung, string Name, IReadOnlyDictionary<string, JsonElement> Werte);

public sealed class LaborKatalog
{
    public const string Art = "hlks-xr-labor-katalog";
    public const int Fassung = 1;
    /// <summary>Schlüssel der Varianten: labor.variante.&lt;name&gt;.</summary>
    public const string VarianteVorsatz = "labor.variante.";
    /// <summary>Stellschraube, die eine Prüfaufgabe startet (Wert = Id) bzw. beendet (null).</summary>
    public const string AufgabeSchluessel = "labor.aufgabe";

    readonly Dictionary<string, LaborStellschraube> stellschrauben = new(StringComparer.Ordinal);
    readonly List<LaborAufgabeDef> aufgaben = [];
    readonly Dictionary<string, LaborVarianteDef> varianten = new(StringComparer.Ordinal);
    readonly List<string> hinweise = [];
    readonly Dictionary<string, (double Min, double Max)> grenzen = new(StringComparer.Ordinal);

    /// <summary>Versionsschlüssel (ETag) des Katalogs.</summary>
    public string Schluessel { get; private init; } = "";
    public IReadOnlyDictionary<string, LaborStellschraube> Stellschrauben => stellschrauben;
    public IReadOnlyList<LaborAufgabeDef> Aufgaben => aufgaben;
    public IReadOnlyDictionary<string, LaborVarianteDef> Varianten => varianten;
    public LaborProfil? Profil { get; private set; }
    /// <summary>Was beim Lesen übergangen wurde.</summary>
    public IReadOnlyList<string> Hinweise => hinweise;
    /// <summary>min/max der Stellschrauben der Art Zahl (für XrBedienEinstellungen.MitStellschraube u. a.).</summary>
    public IReadOnlyDictionary<string, (double Min, double Max)> Grenzen => grenzen;

    public LaborAufgabeDef? Aufgabe(string id) => aufgaben.FirstOrDefault(a => a.Id == id);

    static string? Text(JsonElement o, string name) => o.ValueKind == JsonValueKind.Object && o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    static IReadOnlyList<string> Texte(JsonElement o, string name) =>
        o.ValueKind == JsonValueKind.Object && o.TryGetProperty(name, out var a) && a.ValueKind == JsonValueKind.Array
            ? [.. a.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!)]
            : [];

    /// <summary>Liest die Antwort von GET /api/xr/labor/katalog. Wirft <see cref="Netz.XrNetzFehler"/>, wenn es kein Katalog dieser Art und Fassung ist.</summary>
    public static LaborKatalog Lesen(string json)
    {
        JsonDocument d;
        try { d = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 }); }
        catch (JsonException ex) { throw new Netz.XrNetzFehler("Der Katalog des Labors ist kein gültiges JSON: " + ex.Message); }
        using (d)
        {
            var r = d.RootElement;
            if (r.ValueKind != JsonValueKind.Object || Text(r, "art") != Art) throw new Netz.XrNetzFehler($"Der Katalog des Labors hat nicht die Art «{Art}».");
            if (!r.TryGetProperty("fassung", out var f) || f.ValueKind != JsonValueKind.Number || !f.TryGetInt32(out var fz) || fz != Fassung)
                throw new Netz.XrNetzFehler($"Der Katalog des Labors hat eine andere Fassung (die App kennt {Fassung}) – die App aktualisieren.");
            var k = new LaborKatalog { Schluessel = Text(r, "schluessel") ?? "" };
            if (r.TryGetProperty("stellschrauben", out var ss) && ss.ValueKind == JsonValueKind.Array)
                foreach (var s in ss.EnumerateArray()) k.StellschraubeLesen(s);
            if (r.TryGetProperty("aufgaben", out var aa) && aa.ValueKind == JsonValueKind.Array)
                foreach (var a in aa.EnumerateArray()) k.AufgabeLesen(a);
            if (r.TryGetProperty("varianten", out var vv) && vv.ValueKind == JsonValueKind.Array)
                foreach (var v in vv.EnumerateArray()) k.VarianteLesen(v);
            if (r.TryGetProperty("profil", out var p) && p.ValueKind == JsonValueKind.Object) k.ProfilLesen(p);
            return k;
        }
    }

    void StellschraubeLesen(JsonElement s)
    {
        var key = Text(s, "schluessel");
        if (key is null || key.Length > 64 || stellschrauben.ContainsKey(key)) { hinweise.Add($"Stellschraube ohne bzw. mit doppeltem Schlüssel übergangen ({key ?? "–"})."); return; }
        if (!s.TryGetProperty("vorgabe", out var vorgabe)) { hinweise.Add($"Stellschraube {key}: Vorgabe fehlt."); return; }
        var titel = Text(s, "titel") ?? key;
        var status = Text(s, "status") ?? "app";
        switch (Text(s, "art"))
        {
            case "zahl":
                if (!s.TryGetProperty("min", out var mi) || !s.TryGetProperty("max", out var ma) || !mi.TryGetDouble(out var min) || !ma.TryGetDouble(out var max)
                    || !double.IsFinite(min) || !double.IsFinite(max) || min >= max || vorgabe.ValueKind != JsonValueKind.Number || !vorgabe.TryGetDouble(out var vg) || vg < min || vg > max)
                { hinweise.Add($"Stellschraube {key}: Grenzen oder Vorgabe unbrauchbar."); return; }
                stellschrauben[key] = new LaborStellschraube(key, StellschraubeArt.Zahl, min, max, [], vorgabe.Clone(), status, titel);
                grenzen[key] = (min, max);
                break;
            case "ja_nein":
                if (vorgabe.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) { hinweise.Add($"Stellschraube {key}: Vorgabe nicht true/false."); return; }
                stellschrauben[key] = new LaborStellschraube(key, StellschraubeArt.JaNein, 0, 0, [], vorgabe.Clone(), status, titel);
                break;
            case "auswahl":
                var werte = s.TryGetProperty("auswahl", out var aw) && aw.ValueKind == JsonValueKind.Array
                    ? aw.EnumerateArray().Select(x => Text(x, "wert")).Where(x => x is not null).Select(x => x!).ToList() : [];
                if (werte.Count < 2 || vorgabe.ValueKind != JsonValueKind.String || !werte.Contains(vorgabe.GetString()!))
                { hinweise.Add($"Stellschraube {key}: Auswahl oder Vorgabe unbrauchbar."); return; }
                stellschrauben[key] = new LaborStellschraube(key, StellschraubeArt.Auswahl, 0, 0, werte, vorgabe.Clone(), status, titel);
                break;
            default:
                hinweise.Add($"Stellschraube {key}: unbekannte Art.");
                break;
        }
    }

    void AufgabeLesen(JsonElement a)
    {
        var id = Text(a, "id");
        if (id is null || aufgaben.Any(x => x.Id == id)) { hinweise.Add($"Prüfaufgabe ohne bzw. mit doppelter Id übergangen ({id ?? "–"})."); return; }
        aufgaben.Add(new LaborAufgabeDef(id, Text(a, "titel") ?? id, Text(a, "anleitung") ?? "", Text(a, "status") ?? "p5", Texte(a, "varianten"), Texte(a, "messgroessen"),
            Text(a, "erfolgskriterium") ?? "", Text(a, "ort"), Text(a, "fehlgriff")));
    }

    void VarianteLesen(JsonElement v)
    {
        var name = Text(v, "name");
        var optionen = v.TryGetProperty("optionen", out var o) && o.ValueKind == JsonValueKind.Array
            ? o.EnumerateArray().Select(x => Text(x, "id")).Where(x => x is not null).Select(x => x!).ToList() : [];
        var vorgabe = Text(v, "vorgabe");
        if (name is null || varianten.ContainsKey(name) || optionen.Count < 2 || vorgabe is null || !optionen.Contains(vorgabe))
        { hinweise.Add($"Variante übergangen ({name ?? "–"})."); return; }
        varianten[name] = new LaborVarianteDef(name, Text(v, "titel") ?? name, optionen, vorgabe);
    }

    void ProfilLesen(JsonElement p)
    {
        var werte = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (p.TryGetProperty("werte", out var w) && w.ValueKind == JsonValueKind.Object)
            foreach (var e in w.EnumerateObject()) werte[e.Name] = e.Value.Clone();
        Profil = new LaborProfil(Text(p, "kennung") ?? "", Text(p, "name") ?? "", werte);
    }

    /// <summary>
    /// Prüft einen Wert für einen Schlüssel nach dem Katalog: Stellschrauben nach Art und Grenzen, Varianten nach ihren Optionen. Wert null (zurücksetzen) gilt
    /// immer für einen bekannten Schlüssel. Unbekannt ist ein Schlüssel, den der Katalog nicht führt (auch «labor.aufgabe»: das prüft <see cref="Aufgabe"/>).
    /// </summary>
    public WertUrteil Pruefen(string schluessel, JsonElement wert)
    {
        var zurueck = wert.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined;
        if (schluessel.StartsWith(VarianteVorsatz, StringComparison.Ordinal))
        {
            if (!varianten.TryGetValue(schluessel[VarianteVorsatz.Length..], out var v)) return WertUrteil.Unbekannt;
            return zurueck || wert.ValueKind == JsonValueKind.String && v.Optionen.Contains(wert.GetString()!) ? WertUrteil.Ok : WertUrteil.Ungueltig;
        }
        if (!stellschrauben.TryGetValue(schluessel, out var s)) return WertUrteil.Unbekannt;
        if (zurueck) return WertUrteil.Ok;
        return s.Art switch
        {
            StellschraubeArt.Zahl => wert.ValueKind == JsonValueKind.Number && wert.TryGetDouble(out var d) && double.IsFinite(d) && d >= s.Min && d <= s.Max ? WertUrteil.Ok : WertUrteil.Ungueltig,
            StellschraubeArt.JaNein => wert.ValueKind is JsonValueKind.True or JsonValueKind.False ? WertUrteil.Ok : WertUrteil.Ungueltig,
            _ => wert.ValueKind == JsonValueKind.String && s.Auswahl.Contains(wert.GetString()!) ? WertUrteil.Ok : WertUrteil.Ungueltig,
        };
    }
}
