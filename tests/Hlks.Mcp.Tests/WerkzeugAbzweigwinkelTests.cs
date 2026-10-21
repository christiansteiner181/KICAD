using System.Text.Json;
using System.Text.Json.Nodes;
using static Hlks.Mcp.Tests.Hilfe;

namespace Hlks.Mcp.Tests;

/// <summary>Nachträge zu Leitungsserien (Modul 3, 21.10.2026) im MCP-Server: Werkzeug abzweigwinkel, leitung_abzweigen mit winkel und seite,
/// leitungsserie abbrechbar. FIKTIVE Testfamilien (tests/daten/familien: Abwasser-Testserie mit Grösse d, Abzweigwinkel 45 und 88.5; T d 110 unter
/// 45° mit l = 165, lc = 220).</summary>
public class WerkzeugAbzweigwinkelTests
{
    static JsonElement Elem(string json) => JsonDocument.Parse(json).RootElement.Clone();
    static string Datei(string name) => Testdaten(Path.Combine("familien", name + ".familie.json"));

    static Werkzeuge MitAbwasser()
    {
        var w = new Werkzeuge(Beispiel());
        foreach (var f in new[] { "test_abwasser_d_serie", "test_abwasser_d_bogen", "test_abwasser_d_t", "test_abwasser_d_reduktion" })
            Assert.True(Json(w.FamilieLadenWerkzeug(Datei(f)))["angenommen"]!.GetValue<bool>(), f);
        return w;
    }

    [Fact]
    public void LeitungAbzweigen_MitWinkelUndSeite_UndAbfrageAbzweigwinkel()
    {
        var w = MitAbwasser();
        var z = Json(w.LeitungZeichnenWerkzeug("fam-test-abwasser-d", Elem("\"d 110\""), Elem("[[0, 90000, 1000], [6000, 90000, 1000]]"), system: "SW", praefix: "SWX"));
        Assert.True(z["angenommen"]!.GetValue<bool>(), z.ToJsonString());
        var a = Json(w.AbzweigwinkelLesen(leitung: "SWX-1"));
        Assert.Equal([45.0, 88.5], a["abzweigwinkel"]!.AsArray().Select(x => x!.GetValue<double>()).ToArray());
        Assert.Equal("zulaufend", a["fliessrichtung"]!.GetValue<string>());
        Assert.Equal(220, a["jeWinkel"]![0]!["einbaulaengen"]!["c"]!.GetValue<double>());
        // Zulauf unter 45° links: c = (3000, 90000, 1000) + 220·(−0.707107, 0.707107, 0)
        var e = Json(w.LeitungAbzweigenWerkzeug("SWX-1", Elem("[3000, 90000, 1000]"), winkel: 45, seite: Elem("\"links\""), laenge: 1500));
        Assert.True(e["angenommen"]!.GetValue<bool>(), e.ToJsonString());
        Assert.Contains(e["hinweise"]!.AsArray(), h => h!.GetValue<string>().StartsWith("Abzweig unter 45° nach links"));
        var tkz = e["aenderungen"]!.AsArray().Single(x => x!["art"]!.GetValue<string>() == "angelegt" && x["klasse"]!.GetValue<string>() == "TStueck")!["kennzeichen"]!.GetValue<string>();
        var ts = Json(w.Anschlusslagen(tkz))["anschluesse"]!.AsArray().Single(x => x!["name"]!.GetValue<string>() == "c")!;
        var lage = ts["lage"]!.AsArray().Select(x => x!.GetValue<double>()).ToArray();
        Assert.Equal(2844.437, lage[0], 2);
        Assert.Equal(90155.563, lage[1], 2);
        // Winkel, den die Serie nicht hat: abgelehnt mit den verfügbaren
        var n = Json(w.LeitungAbzweigenWerkzeug("SWX-1", Elem("[1000, 90000, 1000]"), winkel: 60, seite: Elem("[0, 1, 0]"), laenge: 1000));
        Assert.False(n["angenommen"]!.GetValue<bool>());
        Assert.Contains("verfügbar: 45°, 88.5°", n["fehler"]!.GetValue<string>());
    }

    [Fact]
    public void Leitungsserie_Abgebrochen_FehlerStattErgebnis()
    {
        var w = MitAbwasser();
        Assert.Equal("FAM-1", Json(w.LeitungsserieLesen("fam-test-abwasser-d"))["familie"]!.GetValue<string>());
        using var weg = new CancellationTokenSource();
        weg.Cancel();
        var f = Json(w.LeitungsserieLesen("fam-test-abwasser-d", weg.Token));
        Assert.Equal("Abfrage abgebrochen.", f["fehler"]!.GetValue<string>());
        // fachlicher Fehler wie bisher (unter der Sperre erkannt)
        Assert.NotNull(Json(w.LeitungsserieLesen("GIBT-ES-NICHT"))["fehler"]);
    }
}
