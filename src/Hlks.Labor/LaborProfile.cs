// Profile des Labors (XR-Labor L1): ein Profil ist der Satz aller Stellschrauben und Varianten unter einem Namen, gespeichert je Benutzer in
// %OneDrive%\HLKS-CAD\labor\profile\<kennung>.json mit { name, werte } – dieselbe Form wie bisher die Benutzereinstellung «labor.profil.<kennung>» (Prüfung: XrLaborProfil.Pruefen).
// aktiv.json (neben dem Ordner profile) merkt das Profil, das für die App gilt – bisher «labor.profil_aktiv»: { kennung | null, zeit, rechner }.
// Das Labor ersetzt damit die Benutzereinstellungen für Profile (BenutzerEinstellungen bleibt unberührt; die Übernahme liest sie nur, LaborUebernahme.cs).
//
// ENDPUNKTE (nur localhost):
//   GET    /api/labor/profile            { aktiv: <kennung> | null, profile: [{ kennung, name, anzahl, werte{} }] }   nach Name sortiert
//   POST   /api/labor/profile            { kennung, profil: { name, werte{} } } → { kennung, name, anzahl }          speichert (überschreibt ein gleiches)
//   POST   /api/labor/profile/aktiv      { kennung | null } → { aktiv }                                              null = kein Profil gilt für die App
//   DELETE /api/labor/profile/{kennung}  → { geloescht }                                                             ist es das aktive, gilt danach keines
// Fehler: { fehler, vorschlag } (400 Eingabe, 503 Ablage). Dateien mit ungültiger Kennung im Namen (z. B. Konfliktkopien von OneDrive) oder ungültigem Inhalt werden übergangen.

using System.Text;
using System.Text.Json.Nodes;
using Hlks.Kern;
using Hlks.Web;
using static Hlks.Web.EndpunktHilfe;

namespace Hlks.Labor;

/// <summary>Ein Profil mit seiner Kennung.</summary>
public sealed record LaborProfilEintrag(string Kennung, string Name, JsonObject Werte);

public sealed class LaborProfile(LaborAblage ablage, Func<DateTimeOffset> jetzt)
{
    /// <summary>Höchstens so viele Profile (Schutz vor einem überfüllten Ordner).</summary>
    public const int Max = 100;
    readonly object sperre = new();

    string Pfad(string kennung) => Path.Combine(ablage.Profile, kennung + ".json");

    static string Pruefung(string kennung, JsonNode? profil)
    {
        if (!XrLaborProfil.KennungGueltig(kennung)) return "Die Kennung des Profils ist ungültig (Kleinbuchstaben, Ziffern, Unterstrich; bis 24 Zeichen).";
        if (profil is null) return "Das Profil fehlt.";
        return XrLaborProfil.Pruefen(XrLaborProfil.Vorsatz + kennung, profil) ?? "";
    }

    /// <summary>Alle gültigen Profile, nach Name sortiert.</summary>
    public IReadOnlyList<LaborProfilEintrag> Liste()
    {
        var liste = new List<LaborProfilEintrag>();
        var ordner = ablage.Profile;
        if (!Directory.Exists(ordner)) return liste;
        try
        {
            foreach (var datei in Directory.EnumerateFiles(ordner, "*.json").Take(Max * 2))
            {
                var kennung = Path.GetFileNameWithoutExtension(datei);
                if (!XrLaborProfil.KennungGueltig(kennung)) continue;
                if (Lesen(datei) is { } p && Pruefung(kennung, p) == "") liste.Add(new LaborProfilEintrag(kennung, (string)p["name"]!, (JsonObject)p["werte"]!));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* Ordner nicht lesbar: leere Liste */ }
        return [.. liste.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(p => p.Kennung, StringComparer.Ordinal)];
    }

    static JsonObject? Lesen(string datei)
    {
        try { return JsonNode.Parse(File.ReadAllText(datei, Encoding.UTF8), documentOptions: new System.Text.Json.JsonDocumentOptions { MaxDepth = 16 }) as JsonObject; }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or IOException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>Ein Profil { name, werte } oder null (fehlt oder ungültig).</summary>
    public JsonObject? Holen(string kennung)
    {
        if (!XrLaborProfil.KennungGueltig(kennung)) return null;
        var p = Lesen(Pfad(kennung));
        return p is not null && Pruefung(kennung, p) == "" ? p : null;
    }

    /// <summary>Kennung des Profils, das für die App gilt – nur wenn es das Profil noch gibt; sonst null.</summary>
    public string? Aktiv()
    {
        if (Lesen(ablage.AktivDatei) is not { } a || a["kennung"] is not JsonValue v || !v.TryGetValue<string>(out var k) || !XrLaborProfil.KennungGueltig(k)) return null;
        return Holen(k) is null ? null : k;
    }

    /// <summary>Für GET /api/xr/labor/katalog: das aktive Profil mit Kennung, sonst (null, null).</summary>
    public (string? Kennung, JsonNode? Profil) AktivesProfil()
    {
        var k = Aktiv();
        return k is null ? (null, null) : (k, Holen(k));
    }

    /// <summary>Speichert ein Profil (überschreibt das gleiche). Wirft HlksFehler bei ungültiger Form, gesperrter oder nicht beschreibbarer Ablage.</summary>
    public JsonObject Speichern(string kennung, JsonNode? profil)
    {
        var fehler = Pruefung(kennung, profil);
        if (fehler != "") throw new HlksFehler($"Das Profil «{(kennung.Length > 24 ? kennung[..24] : kennung)}» ist ungültig. {fehler}", "Name (bis 40 Zeichen) und Werte { Schlüssel: Wert } senden.");
        var p = (JsonObject)profil!;
        var sauber = new JsonObject { ["name"] = ((string)p["name"]!).Trim(), ["werte"] = p["werte"]!.DeepClone() };
        lock (sperre)
        {
            if (!File.Exists(Pfad(kennung)) && Liste().Count >= Max) throw new HlksFehler($"Es gibt schon {Max} Profile.", "Nicht mehr gebrauchte Profile löschen.");
            ablage.Schreiben(Pfad(kennung), Json.Text(sauber, true));
        }
        return new JsonObject { ["kennung"] = kennung, ["name"] = sauber["name"]!.DeepClone(), ["anzahl"] = ((JsonObject)sauber["werte"]!).Count };
    }

    /// <summary>Setzt das Profil, das für die App gilt (null = keines). Das Profil muss es geben.</summary>
    public string? AktivSetzen(string? kennung)
    {
        if (kennung is not null && Holen(kennung) is null)
            throw new HlksFehler($"Das Profil «{(kennung.Length > 24 ? kennung[..24] : kennung)}» gibt es nicht.", "GET /api/labor/profile listet die Profile.");
        lock (sperre)
        {
            ablage.Schreiben(ablage.AktivDatei, Json.Text(new JsonObject { ["kennung"] = kennung, ["zeit"] = jetzt().ToString("o"), ["rechner"] = ablage.Rechner }, true));
        }
        return kennung;
    }

    /// <summary>Löscht ein Profil; war es das aktive, gilt danach keines. Gibt zurück, ob es das Profil gab.</summary>
    public bool Loeschen(string kennung)
    {
        if (!XrLaborProfil.KennungGueltig(kennung)) throw new HlksFehler("Die Kennung des Profils ist ungültig.", "Kleinbuchstaben, Ziffern, Unterstrich; bis 24 Zeichen.");
        if (ablage.SchreibSperre() is { } s) throw new HlksFehler(s, "Im Test " + LaborAblage.UmleitungVariable + " auf einen Ordner unter %TEMP% setzen.");
        lock (sperre)
        {
            var datei = Pfad(kennung);
            if (!File.Exists(datei)) return false;
            try { File.Delete(datei); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { throw new HlksFehler($"Das Profil konnte nicht gelöscht werden: {ex.Message}", "OneDrive und Schreibrechte prüfen und erneut versuchen."); }
            if (Lesen(ablage.AktivDatei) is { } a && a["kennung"] is JsonValue v && v.TryGetValue<string>(out var k) && k == kennung) AktivSetzen(null);
            return true;
        }
    }

    /// <summary>Antwort von GET /api/labor/profile.</summary>
    public JsonObject Antwort()
    {
        var liste = Liste();
        return new JsonObject
        {
            ["aktiv"] = Aktiv(),
            ["profile"] = new JsonArray([.. liste.Select(p => (JsonNode)new JsonObject { ["kennung"] = p.Kennung, ["name"] = p.Name, ["anzahl"] = p.Werte.Count, ["werte"] = p.Werte.DeepClone() })]),
        };
    }
}

public static class LaborProfilEndpunkte
{
    public static WebApplication MapLaborProfile(this WebApplication app)
    {
        var profile = app.Services.GetRequiredService<LaborProfile>();
        var g = app.MapGroup("/api/labor/profile");

        g.MapGet("", () => Gesichert(() => Task.FromResult(Antwort(profile.Antwort()))));

        g.MapPost("", (HttpRequest r) => Gesichert(async () =>
        {
            var p = await KoerperLesen(r);
            var kennung = Param.Text(p, "kennung", true)!;
            return Antwort(profile.Speichern(kennung, p["profil"]));
        }));

        g.MapPost("/aktiv", (HttpRequest r) => Gesichert(async () =>
        {
            var p = await KoerperLesen(r);
            var kennung = p["kennung"] is null ? null : Param.Text(p, "kennung");
            return Antwort(new JsonObject { ["aktiv"] = profile.AktivSetzen(kennung) });
        }));

        g.MapDelete("/{kennung}", (string kennung) => Gesichert(() => Task.FromResult(Antwort(new JsonObject { ["geloescht"] = profile.Loeschen(kennung) }))));
        return app;
    }
}
