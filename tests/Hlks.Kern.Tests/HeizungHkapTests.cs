using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.HeizungHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 5 Teil A-D, Punkt 9: tempVL/tempRL eines HKAP-Strangs fliessen beim Abgleich (Modul 1d) nur HKAP → KICAD in die verknüpfte
/// Heizgruppe (die Gruppen, in deren Wegen Leitungen der KICAD-Stränge liegen), mit Herkunft «hkap»; nie gesendet. Ohne verknüpfte Heizgruppe
/// bleiben sie «nur lesen» (HkapStraengeTests). Gegen die Attrappe (HkapAttrappe), nie die echte HKAP; Werte FIKTIV.</summary>
public sealed class HeizungHkapTests : IDisposable
{
    readonly HkapAttrappe a = new();
    public void Dispose() => a.Dispose();

    static JsonObject Art(JsonObject abgleich, string name) => abgleich["datenarten"]!.AsArray().Single(d => d!["name"]!.GetValue<string>() == name)!.AsObject();

    static IEnumerable<string> Felder(JsonObject art, string richtung, string hkapId) =>
        art[richtung]!["geaendert"]!.AsArray().Select(x => x!.AsObject()).Where(x => x["hkapId"]?.GetValue<string>() == hkapId)
            .SelectMany(x => x["felder"]!.AsArray()).Select(f => f!["feld"]!.GetValue<string>());

    [Fact]
    public void TempVlRl_AusHkap_InDieVerknuepftenHeizgruppen_NieGesendet()
    {
        var p = Netz();
        // ein HKAP-Strang für Vor- und Rücklauf (Bündeln über die Steigzone)
        Gut(p, "element_aendern", """{"element":"HZ-V","name":"Heizung Strang 1","eigenschaften":{"steigzone":"ST 1"}}""");
        Gut(p, "element_aendern", """{"element":"HZ-R","name":"Heizung Strang 1","eigenschaften":{"steigzone":"ST 1"}}""");
        Assert.Equal(["HG-1", "HG-2"], Heizgruppen.FuerStraenge(p.Modell, p.Katalog, [p.Modell.Hole("HZ-V")]).Select(h => h.Kennzeichen));
        Gut(p, "hkap_verknuepfen", $$"""{ "projekt": "P-005", "projektierung": "PJ-2026-003", "url": "{{a.Url}}" }""");
        Gut(p, "hkap_senden", """{ "datenarten": ["straenge"] }""");
        var s = a.Straenge.Single();
        Assert.True(s.Felder["tempVL"] is null, "tempVL darf nie gesendet werden");
        var gesendet = a.Posts("straenge").SelectMany(x => x.Body!["datensaetze"]!.AsArray()).Select(d => d!["felder"]!.AsObject()).ToList();
        Assert.All(gesendet, f => Assert.False(f.ContainsKey("tempVL") || f.ContainsKey("tempRL")));

        // HKAP setzt die Temperaturen: Abgleich zeigt sie als Übernahme (HKAP führt), die Übernahme setzt beide Gruppen mit Herkunft hkap
        a.Aendern($"{HkapAttrappe.Pj}|straenge", s.Id, f => { f["tempVL"] = 60; f["tempRL"] = 40; });
        var art = Art((JsonObject)Tuer.Abfrage(p, "hkap_abgleich", O("""{ "datenarten": ["straenge"] }"""))!, "straenge");
        Assert.Equal(["tempRL", "tempVL"], Felder(art, "nachKicad", s.Id).Order());
        Assert.Empty(Felder(art, "nachHkap", s.Id));
        var e = Gut(p, "hkap_uebernehmen", """{ "datenarten": ["straenge"] }""");
        foreach (var kz in new[] { "HG-1", "HG-2" })
        {
            var hg = p.Modell.Hole(kz);
            Assert.Equal((60.0, 40.0), (hg.Zahl("vlTemp")!.Value, hg.Zahl("rlTemp")!.Value));
            Assert.Equal("hkap", hg.Eigenschaft("vlTemp")!.Herkunft.Art);
            Assert.Contains(s.Id, hg.Eigenschaft("vlTemp")!.Herkunft.Quelle);
        }
        Assert.Contains(e.Hinweise, h => h.Contains("HG-1: vlTemp 60 °C aus HKAP"));
        Assert.Equal(20.0, Heizgruppen.Spreizung(p.Modell.Hole("HG-1")));
        // danach gleich: nichts mehr zu übernehmen, nichts zu senden
        var nach = Art((JsonObject)Tuer.Abfrage(p, "hkap_abgleich", O("""{ "datenarten": ["straenge"] }"""))!, "straenge");
        Assert.Empty(Felder(nach, "nachKicad", s.Id));
        Assert.Empty(Felder(nach, "nachHkap", s.Id));
    }
}
