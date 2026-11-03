// Modul 5 Teil B-H: Heizlast übernehmen – die gerechnete Norm-Heizlast ΦHL (Abfrage heizlast) als Raum.heizleistung setzen (Herkunft
// «berechnung» mit Quelle), damit die bestehende Kette rechnet: Leistung je Verbraucher (heizung.leistung_verbraucher), Summen, Massenstrom,
// Prinzipschema. Eine Heizleistung, die nicht aus der Berechnung stammt (von Hand, HKAP, IFC), wird nur mit ueberschreiben ersetzt – sonst Hinweis.
// Christian 04.11.2026 zur Auslegung: «es muss einfach funktionieren».

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public sealed class HeizlastUebernehmen : IBefehl
{
    public string Name => "heizlast_uebernehmen";
    public const string Text = "Heizlast übernehmen (Modul 5 Teil B): setzt die Norm-Heizlast ΦHL je Raum (Abfrage heizlast, SN EN 12831:2003 / SIA 384.201) als " +
        "Eigenschaft heizleistung des Raums, Herkunft «berechnung» mit Quelle. Damit rechnen Verbraucherleistung, Summen, Massenstrom und Prinzipschema weiter. " +
        "Eine Heizleistung von Hand, aus HKAP oder IFC bleibt stehen, ausser mit ueberschreiben. Räume ohne Heizlast (z. B. ohne Innentemperatur) bleiben unverändert.";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("raum", "text", false, Heizlast.ParRaum),
        new("gebaeude", "text", false, Heizlast.ParGebaeude),
        new("ueberschreiben", "bool", false, "Auch eine Heizleistung ersetzen, die nicht aus der Berechnung stammt (von Hand, HKAP, IFC)"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var r = Heizlast.Rechnen(k.Arbeit, k.Katalog, new JsonObject { ["raum"] = Param.Text(p, "raum"), ["gebaeude"] = Param.Text(p, "gebaeude") });
        foreach (var h in (r["hinweise"] as JsonArray ?? []).Select(x => x?.ToString()).OfType<string>()) k.Hinweise.Add(h);
        if (r["aussen"] is null) throw new HlksFehler("Keine Heizlast gerechnet: " + string.Join(" ", k.Hinweise), "Klimastation am Gebäude bzw. die Normtabellen im Büroordner prüfen (Abfrage heizlast).");
        bool ueber = Param.Bool(p, "ueberschreiben");
        double? thetaE = r["aussen"]?["thetaE"]?.GetValue<double>();
        int gesetzt = 0; var behalten = new List<string>(); var ohne = new List<string>();
        foreach (var z in (r["raeume"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var kz = z["raum"]?.GetValue<string>();
            if (kz is null || k.Arbeit.Finde(kz) is not { } raum) continue;
            if (z["phiHL"] is not JsonValue v || !v.TryGetValue<double>(out var phi)) { ohne.Add(kz); continue; }
            if (raum.Eigenschaft("heizleistung") is { } alt && alt.Herkunft.Art != "berechnung" && !ueber) { behalten.Add(kz); continue; }
            var e = raum.Kopie();
            e.Eigenschaften["heizleistung"] = new Wert
            {
                Inhalt = JsonValue.Create(Math.Round(phi)), Einheit = "W",
                Herkunft = k.Herkunft with { Art = "berechnung", Quelle = $"Norm-Heizlast {Heizlast.Norm} (Abfrage heizlast): θint {z["thetaInt"]} °C, θe {thetaE} °C, ΦT {z["phiT"]} W + ΦV {z["phiV"]} W" },
            };
            k.Setzen(e); gesetzt++;
        }
        k.Hinweise.Add($"Heizleistung aus der Heizlast gesetzt: {gesetzt} Raum/Räume.");
        if (behalten.Count > 0) k.Hinweise.Add($"{behalten.Count} Raum/Räume mit Heizleistung von Hand, HKAP oder IFC nicht ersetzt (ueberschreiben): {string.Join(", ", behalten.Take(10))}{(behalten.Count > 10 ? " …" : "")}.");
        if (ohne.Count > 0) k.Hinweise.Add($"{ohne.Count} Raum/Räume ohne Heizlast (Innentemperatur fehlt): {string.Join(", ", ohne.Take(10))}{(ohne.Count > 10 ? " …" : "")}.");
    }
}
