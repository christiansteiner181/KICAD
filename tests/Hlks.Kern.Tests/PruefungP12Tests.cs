using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>P12 (Modul 1c): Hüllenkennwert ohne Quelle. Alle Zahlen sind Testwerte, keine Materialkennwerte.</summary>
public class PruefungP12Tests
{
    static List<Befund> P12(Projekt p) => Pruefungen.Alle(p.Modell, p.Katalog).Where(b => b.Code == "P12").ToList();

    [Fact]
    public void KennwertOhneQuelle_ueberElementAnlegen_IstWarnung()
    {
        var p = Neu();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Material", ["kennzeichen"] = "MAT-T", ["name"] = "Testmaterial",
            ["eigenschaften"] = new JsonObject { ["lambda"] = 0.5 },   // Testwert, ohne Quelle
        }));
        var b = Assert.Single(P12(p));
        Assert.Equal("warnung", b.Schwere);
        Assert.Equal("MAT-T", b.Element);
        Assert.Contains("lambda", b.Text);
        Assert.Contains("material_aendern", b.Vorschlag);
    }

    [Fact]
    public void KennwertMitQuelle_ueberMaterialAnlegen_KeinBefund()
    {
        var p = Neu();
        Ok(p.Ausfuehren("material_anlegen", new JsonObject
        {
            ["name"] = "Testmaterial",
            ["werte"] = new JsonObject { ["lambda"] = new JsonObject { ["wert"] = 0.5, ["quelle"] = "Testwert P12" } },
        }));
        Assert.Empty(P12(p));
    }

    [Fact]
    public void Grundmodell_OhneKennwerte_KeinP12()
    {
        Assert.Empty(P12(Beispielprojekt.Erzeugen(Kat, null)));
    }
}
