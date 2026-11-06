using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;

namespace Hlks.Kern.Tests;

/// <summary>Rahmen eckiger Lüftungskanäle (Christian 06.11.2026: «bei lüftungskanälen (eckig) auch die rahmen darstellen. auch in den grundrissen»):
/// Werte aus kataloge/lueftung.json › kanalrahmen (Schmidlin Preisbuch Kanäle 07.2026: M20 bis 1000 mm, Überstand 11 mm, Normkanal 1770 mm;
/// Profiltiefe 20 mm bestätigt von Christian 06.11.2026). Kanal 400 × 200 (fiktive Testkanalserie), 4000 mm lang: Rahmen an beiden Enden und an den Stössen 1770 und 3540.</summary>
public class KanalRahmenTests
{
    static readonly Katalog Kat = Katalog.Laden(Katalog.StandardOrdner());

    [Fact]
    public void Eckiger_Kanal_RahmenAnEndenUndStoessen_ImGrundrissUndIn3D()
    {
        var p = BeispielKlein.Erzeugen(Kat, null);
        foreach (var f in new[] { "test_kanalserie", "test_kanalbogen" }) Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Datei(f) }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "ZUL", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Zuluft" } }));
        Ok(p.Ausfuehren("leitung_zeichnen", new JsonObject
        {
            ["serie"] = "fam-test-kanalserie", ["groesse"] = "400x200", ["system"] = "ZUL", ["geschoss"] = "EG",
            ["punkte"] = new JsonArray(new JsonArray(500, 4000, 2400), new JsonArray(4500, 4000, 2400)),
        }));
        var kanal = p.Modell.DerKlasse("Kanal").Single();
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "AN-GR-EG", ["eigenschaften"] = new JsonObject { ["gewerke"] = new JsonArray("lueftung", "architektur") } }));
        Assert.Equal("M20", KanalRahmen.Fuer(p.Katalog, 400, 200)!.Name);

        var teil = Grafik.Plan(p, "AN-GR-EG").Teile.Single(t => t.Id == kanal.Id);
        var rahmen = teil.Primitive.Where(x => x.Art == "polygon" && x.Stil.Deckkraft == 0.35).ToList();
        Assert.Equal(4, rahmen.Count);
        // quer zur Achse 400 + 2 × 11 breit; die Stösse 2 × 20 tief um 1770 bzw. 3540
        Assert.All(rahmen, r => Assert.Equal(422, r.Punkte.Max(q => q[1]) - r.Punkte.Min(q => q[1]), 6));
        Assert.Contains(rahmen, r => Math.Abs(r.Punkte.Min(q => q[0]) - (500 + 1770 - 20)) < 1e-6 && Math.Abs(r.Punkte.Max(q => q[0]) - (500 + 1770 + 20)) < 1e-6);

        var k = Grafik.QuelleFuer(p.Katalog, kanal)!.Koerper(p, kanal)!;
        Assert.Equal(4, k.Teile.Count(t => t.Art == "quader" && t.Masse![1] == 422 && t.Masse[2] == 222));
    }
}
