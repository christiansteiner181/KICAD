using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3, Phase A (Hauptlauf, 18.10.2026): die fiktiven Testfamilien unter tests/daten/familien sind gültig.
/// Alle Werte FIKTIV – keine Herstellerdaten, keine Normmasse. Die Rohrserie (Block «leitung») liest seit B1 der Kern.</summary>
public class Modul3TestfamilienTests
{
    static JsonObject Datei(string name) =>
        (JsonObject)JsonNode.Parse(File.ReadAllText(IfcHilfe.Testdaten(Path.Combine("familien", name + ".familie.json"))))!;

    [Theory]
    [InlineData("test_bogen", "Bogen", "fam-test-bogen")]
    [InlineData("test_tstueck", "TStueck", "fam-test-tstueck")]
    [InlineData("test_reduktion", "Reduktion", "fam-test-reduktion")]
    public void Formteilfamilien_SindGueltig(string datei, string kategorie, string id)
    {
        var d = Datei(datei);
        Assert.Equal(id, d["id"]!.GetValue<string>());
        Assert.Equal(kategorie, d["kategorie"]!.GetValue<string>());
        var def = FamilieDef.Lesen(d, Kat);
        Assert.True(def.Gueltig, string.Join(" | ", def.Fehler));
        Assert.Equal(6, def.Typen.Count);
    }

    [Fact]
    public void Rohrserie_IstGueltig()   // B1: Block «leitung» gelesen und geprüft
    {
        var d = Datei("test_rohrserie");
        Assert.Equal("fam-test-rohrserie", d["id"]!.GetValue<string>());
        var def = FamilieDef.Lesen(d, Kat);
        Assert.True(def.Gueltig, string.Join(" | ", def.Fehler));
        Assert.True(def.IstLeitungsserie);
        Assert.Equal(6, def.Typen.Count);
        Assert.Empty(def.Koerper);
        Assert.Empty(def.Anschluesse);
    }

    [Fact]
    public void Rohrserie_VerweistAufDieFormteilfamilien()
    {
        var l = Datei("test_rohrserie")["leitung"]!.AsObject();
        Assert.Equal("rund", l["querschnitt"]!.GetValue<string>());
        foreach (var (art, id) in new[] { ("bogen", "fam-test-bogen"), ("tstueck", "fam-test-tstueck"), ("reduktion", "fam-test-reduktion") })
            Assert.Equal(id, l["formteile"]![art]!["familie"]!.GetValue<string>());
    }
}
