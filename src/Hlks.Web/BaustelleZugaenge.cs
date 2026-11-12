// KICAD Baustelle – Zugänge für den Unternehmer (Christian 12.11.2026: «KICAD ist das werkzeug für die planung. KICAD Baustelle ist das werkzeug für den
// monteur. dieser braucht andere Werkzeuge. richte mir im test bitte einen monteur und einen projektleiter nutzer ein.», «hier wollen wir tools für den
// unternehmer der die anlage baut zur verfügung stellen. wir müssen daher einfacher und praxisorientiert arbeiten.»).
// Entscheide Christian 12.11.2026 (Fragefenster): Anmeldung über einen persönlichen Link (einmal öffnen, das Gerät bleibt angemeldet), Projektleiter
// sieht auch den Arbeitsstand, Zugänge je Projekt.
//
// Datei <Projekt>\baustelle\zugaenge.json: { "zugaenge": [ { "name", "rolle": "monteur" | "projektleiter", "schluessel" } ] }.
// - Ohne Datei bleibt der Testserver offen wie bisher (jede Webseite, Name in der Rückmeldung frei).
// - Mit Datei: /baustelle/z/<schluessel> setzt ein Cookie und leitet auf die Übersicht; ohne gültiges Cookie zeigt der Zugang nur einen Hinweis.
//   Monteur: nur die Monteur-Webseiten (freigegebener Stand, Dateiname mit «_monteur_»); Projektleiter: alle Webseiten, auch Arbeitsstand.
//   Rückmeldungen tragen den Namen des Zugangs (nicht frei eingetippt).

using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Hlks.Kern;

namespace Hlks.Web;

public static class BaustelleZugaenge
{
    public const string Monteur = "monteur";
    public const string Projektleiter = "projektleiter";
    public static readonly string[] Rollen = [Monteur, Projektleiter];
    public const string Keks = "kicad_baustelle";

    public sealed record Zugang(string Name, string Rolle, string Schluessel);

    public static string Datei(Projekt p) => Path.Combine(p.Speicher?.Ort ?? throw new HlksFehler("Projekt ohne Ablage."), "baustelle", "zugaenge.json");

    /// <summary>Zugänge des Projekts oder null, wenn es keine Datei gibt (offener Testserver).</summary>
    public static List<Zugang>? Lesen(Projekt p)
    {
        if (p.Speicher is null || !File.Exists(Datei(p))) return null;
        var o = JsonNode.Parse(File.ReadAllText(Datei(p))) as JsonObject;
        return (o?["zugaenge"] as JsonArray ?? []).OfType<JsonObject>()
            .Select(z => new Zugang(((string?)z["name"] ?? "").Trim(), ((string?)z["rolle"] ?? "").Trim().ToLowerInvariant(), ((string?)z["schluessel"] ?? "").Trim()))
            .Where(z => z.Name.Length > 0 && Rollen.Contains(z.Rolle) && z.Schluessel.Length >= 8).ToList();
    }

    /// <summary>Neuer Zugang (Schlüssel 12 Zeichen aus Kleinbuchstaben und Ziffern ohne verwechselbare Zeichen); die Datei wird angelegt bzw. ergänzt.</summary>
    public static Zugang Anlegen(Projekt p, string name, string rolle)
    {
        rolle = rolle.Trim().ToLowerInvariant();
        if (!Rollen.Contains(rolle)) throw new HlksFehler($"Rolle «{rolle}» gibt es nicht.", "rolle: " + string.Join(" | ", Rollen));
        if (string.IsNullOrWhiteSpace(name)) throw new HlksFehler("Name fehlt.");
        const string zeichen = "abcdefghjkmnpqrstuvwxyz23456789";
        var schluessel = new string(Enumerable.Range(0, 12).Select(_ => zeichen[RandomNumberGenerator.GetInt32(zeichen.Length)]).ToArray());
        var liste = Lesen(p) ?? [];
        var z = new Zugang(name.Trim(), rolle, schluessel);
        liste.Add(z);
        Directory.CreateDirectory(Path.GetDirectoryName(Datei(p))!);
        var json = new JsonObject { ["zugaenge"] = new JsonArray(liste.Select(x => (JsonNode)new JsonObject { ["name"] = x.Name, ["rolle"] = x.Rolle, ["schluessel"] = x.Schluessel }).ToArray()) };
        File.WriteAllText(Datei(p), json.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        return z;
    }

    public static Zugang? Angemeldet(HttpRequest r, List<Zugang> liste) =>
        r.Cookies.TryGetValue(Keks, out var k) && !string.IsNullOrEmpty(k) ? liste.FirstOrDefault(z => z.Schluessel == k) : null;

    /// <summary>Darf der Zugang diese Webseite sehen? Monteur nur Monteur-Seiten (Dateiname «…_monteur_…»), Projektleiter alle.</summary>
    public static bool DarfSehen(Zugang z, string datei) =>
        z.Rolle == Projektleiter || Path.GetFileName(datei).Contains("_monteur_", StringComparison.OrdinalIgnoreCase);

    public static string RollenText(string rolle) => rolle == Projektleiter ? "Projektleiter" : "Monteur";
}
