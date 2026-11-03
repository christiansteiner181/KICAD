// Webablage – Gerüst (Christian 03.11.2026: «dann die gerüste für MSRL GA und schnittestelle zu webablage»). Annahme Hauptlauf (Christian 04.11.2026:
// «bitte ale fragen selbst bestätigen»): Webablage = die zentrale Ablage des Büros über die Workshare-Schnittstelle (workshare.hkplanung.ch, PHP,
// Token je Person; HKAP_Workshare). KICAD legt dort die Ausgaben eines Projekts ab (Pläne PDF, IFC, Listen, Baustellenpaket) – nur aus dem
// Ordner «ausgabe» des Projekts, nie Modelldaten. Spezifikation: entwurf/webablage.md.
// Gerüst: Konfiguration (kataloge/buero.json › webablage, Token NUR als Umgebungsvariable HLKS_WEBABLAGE_TOKEN), Schnittstelle IWebablage
// und die Abfrage webablage_status (was hochgeladen würde, ob die Verbindung eingerichtet ist). KEIN Netzzugriff in diesem Stand – der
// Adapter für Workshare folgt, wenn der Endpunkt auf dem Server steht (Frage an Christian).

using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <summary>Eintrag in der Webablage: Pfad relativ zum Projekt, Grösse, Änderungszeit, Prüfsumme.</summary>
public sealed record WebablageDatei(string Pfad, long Groesse, DateTime Geaendert, string? Sha256 = null);

/// <summary>Zugriff auf eine Webablage (Adapter je Anbieter). Hochladen nur mit ausdrücklichem Befehl des Menschen.</summary>
public interface IWebablage
{
    string Name { get; }
    IReadOnlyList<WebablageDatei> Liste(string projektId);
    WebablageDatei Hochladen(string projektId, string lokalerPfad, string zielPfad);
}

public static class Webablage
{
    public const string TokenVariable = "HLKS_WEBABLAGE_TOKEN";
    public const string Text = "Webablage (Gerüst): zeigt die Einstellung der Webablage (Adresse, ob ein Token gesetzt ist – das Token selbst nie) und die Dateien im Ordner " +
        "«ausgabe» des Projekts, die hochgeladen würden (Pläne, IFC, Listen, Baustellenpaket), mit Grösse und Datum. Noch ohne Verbindung zum Server.";
    public static IReadOnlyList<ParameterDef> Parameter { get; } = [];

    /// <summary>Einstellung aus kataloge/buero.json › webablage (url, projektPfad); null ohne Eintrag.</summary>
    public static JsonObject? Einstellung(Katalog k)
    {
        var pfad = Path.Combine(k.Ordner, "buero.json");
        return File.Exists(pfad) ? JsonNode.Parse(File.ReadAllText(pfad))?["webablage"] as JsonObject : null;
    }

    public static JsonObject Abfrage(Projekt p, JsonObject? par)
    {
        var e = Einstellung(p.Katalog);
        var ort = p.Speicher?.Ort;
        var dateien = new JsonArray();
        if (ort is { Length: > 0 } && Directory.Exists(Path.Combine(ort, "ausgabe")))
            foreach (var f in Directory.EnumerateFiles(Path.Combine(ort, "ausgabe"), "*", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                var fi = new FileInfo(f);
                dateien.Add(new JsonObject { ["pfad"] = Path.GetRelativePath(ort, f).Replace('\\', '/'), ["groesse"] = fi.Length, ["geaendert"] = fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm") });
            }
        var hinweise = new JsonArray("Gerüst: noch keine Verbindung zum Server; Hochladen folgt mit dem Workshare-Endpunkt (entwurf/webablage.md).");
        if (e is null) hinweise.Add("Keine Einstellung kataloge/buero.json › webablage.");
        if (ort is null) hinweise.Add("Projekt ohne Speicherort – keine Ausgaben.");
        return new JsonObject
        {
            ["url"] = e?["url"]?.DeepClone(), ["anbieter"] = e?["anbieter"]?.DeepClone(),
            ["tokenGesetzt"] = Environment.GetEnvironmentVariable(TokenVariable) is { Length: > 0 },
            ["projekt"] = p.Kopf.Id, ["dateien"] = dateien, ["anzahl"] = dateien.Count, ["hinweise"] = hinweise,
        };
    }
}
