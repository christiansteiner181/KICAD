// Modul 8 MSRL – Gerüst (Christian 03.11.2026: «dann die gerüste für MSRL GA und schnittestelle zu webablage»): Datenpunkte aus der Grundausstattung
// je Bauteilklasse vorschlagen (kataloge/datenpunkte.json, Annahme Hauptlauf – im Büro keine Datenpunktliste als Vorlage) und die GA-Datenpunktliste
// je Anlage zusammenzählen (AI, AO, DI, DO, Zähler, Werte). Die Datenpunkte sind gewöhnliche Elemente der Klasse Datenpunkt mit bezuege.an = Bauteil;
// Adressen liefert der GA-Unternehmer (Eigenschaft adresse), die Live-Werte holt Modul 9 über IDatenquelle (heute Simulator).
// Spezifikation: entwurf/modul-8-9-msrl-ga.md.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class Datenpunktliste
{
    public const string Datei = "datenpunkte.json";
    public static readonly string[] Signale = ["AI", "AO", "DI", "DO", "Zaehler", "Wert"];

    public const string Text = "GA-Datenpunktliste (Modul 8 MSRL): je Anlage die Zahl der Datenpunkte nach Signal (AI, AO, DI, DO, Zähler, Werte) und die Liste mit Bauteil, " +
        "Art, Messgrösse, Adresse und Station. Datenpunkte ohne Anlage stehen unter «ohne Anlage». Vorschlag aus der Grundausstattung: Befehl datenpunkte_vorschlagen.";
    public const string ParAnlage = "Nur diese Anlage (Kennzeichen oder Id); leer = alle";
    public static IReadOnlyList<ParameterDef> Parameter { get; } = [new("anlage", "text", false, ParAnlage)];

    static string? T(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    public static JsonObject? Katalog(Katalog k)
    {
        var pfad = Path.Combine(k.Ordner, Datei);
        return File.Exists(pfad) ? JsonNode.Parse(File.ReadAllText(pfad)) as JsonObject : null;
    }

    /// <summary>Anlage eines Bauteils: über sein System (teilVon), sonst null.</summary>
    public static Element? AnlageVon(Modell m, Element? e) => e?.System is { } s && m.Finde(s) is { } sys && sys.TeilVon is { } a ? m.Finde(a) : null;

    public static JsonObject Abfrage(Projekt p, JsonObject? par)
    {
        par ??= [];
        var m = p.Modell;
        Element? nur = Param.Text(par, "anlage") is { Length: > 0 } a ? m.Finde(a) ?? throw new HlksFehler($"Anlage «{a}» gibt es nicht.") : null;
        var gruppen = m.DerKlasse("Datenpunkt")
            .Select(d => (Dp: d, An: d.Bezuege.TryGetValue("an", out var an) ? m.Finde(an) : null))
            .Select(x => (x.Dp, x.An, Anlage: AnlageVon(m, x.An)))
            .Where(x => nur is null || x.Anlage?.Id == nur.Id)
            .GroupBy(x => x.Anlage?.Kennzeichen ?? "ohne Anlage").OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);
        var anlagen = new JsonArray();
        foreach (var g in gruppen)
        {
            var summe = new JsonObject();
            foreach (var s in Signale) summe[s] = g.Count(x => x.Dp.Text("signal") == s);
            anlagen.Add(new JsonObject
            {
                ["anlage"] = g.Key, ["anzahl"] = g.Count(), ["jeSignal"] = summe,
                ["datenpunkte"] = new JsonArray(g.OrderBy(x => x.Dp.Kennzeichen, StringComparer.OrdinalIgnoreCase).Select(x => (JsonNode)new JsonObject
                {
                    ["kennzeichen"] = x.Dp.Kennzeichen, ["name"] = x.Dp.Name, ["signal"] = x.Dp.Text("signal"), ["art"] = x.Dp.Text("art"),
                    ["messgroesse"] = x.Dp.Text("messgroesse"), ["einheit"] = x.Dp.Text("einheit"), ["bauteil"] = x.An?.Kennzeichen, ["klasse"] = x.An?.Klasse,
                    ["adresse"] = x.Dp.Text("adresse"), ["station"] = x.Dp.Bezuege.TryGetValue("station", out var st) ? m.Finde(st)?.Kennzeichen : null,
                }).ToArray()),
            });
        }
        return new JsonObject { ["anlagen"] = anlagen, ["anzahl"] = anlagen.OfType<JsonObject>().Sum(x => (int)x["anzahl"]!) };
    }
}

public sealed class DatenpunkteVorschlagen : IBefehl
{
    public string Name => "datenpunkte_vorschlagen";
    public const string Text = "Datenpunkte vorschlagen (Modul 8 MSRL): legt an Pumpen, Mischventilen, Fühlern, Zählern, Wärmeerzeugern, Lüftungsgeräten und " +
        "Volumenstromreglern die Datenpunkte der Grundausstattung an (kataloge/datenpunkte.json; Kennzeichen DP-<Bauteil>-<kurz>, bezuege.an = Bauteil). " +
        "Vorhandene Datenpunkte mit gleichem Kennzeichen bleiben unverändert. Die Grundausstattung ist eine Annahme, bis Christian eine Bürovorlage bestimmt.";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } = [new("anlage", "text", false, Datenpunktliste.ParAnlage)];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var kat = Datenpunktliste.Katalog(k.Katalog) ?? throw new HlksFehler($"kataloge/{Datenpunktliste.Datei} fehlt.", "Datei aus dem Repository wiederherstellen.");
        var m = k.Arbeit;
        Element? nur = Param.Text(p, "anlage") is { Length: > 0 } a ? m.Finde(a) ?? throw new HlksFehler($"Anlage «{a}» gibt es nicht.") : null;
        var quelle = (kat["quelle"] as JsonValue)?.GetValue<string>() ?? "kataloge/datenpunkte.json";
        int neu = 0, vorhanden = 0;
        foreach (var (klasse, regeln) in (kat["klassen"] as JsonObject ?? []))
        {
            foreach (var e in m.DerKlasse(klasse).OrderBy(x => x.Kennzeichen, StringComparer.OrdinalIgnoreCase).ToList())
            {
                if (nur is not null && Datenpunktliste.AnlageVon(m, e)?.Id != nur.Id) continue;
                foreach (var r in (regeln as JsonArray ?? []).OfType<JsonObject>())
                {
                    var kz = $"DP-{e.Kennzeichen}-{r["kurz"]}";
                    if (m.Finde(kz) is not null) { vorhanden++; continue; }
                    var her = k.Herkunft with { Quelle = "datenpunkte_vorschlagen: " + quelle };
                    Wert W(JsonNode? v) => new() { Inhalt = v?.DeepClone(), Herkunft = her };
                    var messgroesse = r["messgroesseAus"] is JsonValue aus && e.Text(aus.GetValue<string>()) is { } mg ? mg : (r["messgroesse"] as JsonValue)?.GetValue<string>();
                    var d = new Element
                    {
                        Id = Guid.NewGuid().ToString(), Kennzeichen = kz, Klasse = "Datenpunkt", Name = $"{e.Kennzeichen} {r["name"]}",
                        Bezuege = new Dictionary<string, string> { ["an"] = e.Id }, Angelegt = k.Herkunft,
                    };
                    d.Eigenschaften["signal"] = W(r["signal"]);
                    d.Eigenschaften["art"] = W(r["art"]);
                    if (messgroesse is not null) d.Eigenschaften["messgroesse"] = W(JsonValue.Create(messgroesse));
                    if (r["einheit"] is JsonNode ei) d.Eigenschaften["einheit"] = W(ei);
                    d.Eigenschaften["physikalisch"] = W(JsonValue.Create(r["physikalisch"] is JsonValue ph ? ph.GetValue<bool>() : true));
                    k.Setzen(d); neu++;
                }
            }
        }
        k.Hinweise.Add($"Datenpunkte angelegt: {neu}; schon vorhanden: {vorhanden}. Grundausstattung nach kataloge/{Datenpunktliste.Datei} (Annahme Hauptlauf, von Christian zu bestätigen).");
    }
}
