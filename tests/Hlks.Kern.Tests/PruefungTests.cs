using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Prüfansicht (Christian 02.11.2026: «Pläne prüfen und markieren»): Seiteninfo und Seitenbild eines Planblatts, Markierungen als Elemente.</summary>
public class PruefungTests
{
    [Fact]
    public void Planblatt_SeiteAlsPng_UndMarkierungMitStatus()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        Ok(p.Ausfuehren("planblatt_anlegen", new JsonObject { ["titel"] = "Prüftest", ["format"] = "A3", ["nummer"] = "1-0000 S01" }));
        var pb = p.Modell.DerKlasse("Planblatt").Single().Kennzeichen;

        var (seiten, masse, titel) = PruefDokument.Info(p, pb);
        Assert.Equal(1, seiten);
        Assert.True(masse[0].Breite > masse[0].Hoehe);   // A3 quer
        Assert.Contains("Prüftest", titel);
        var png = PruefDokument.SeitePng(p, pb, 1, 50);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png[..4]);

        // Markierung: Pflicht dokument, form, punkte; Status erledigt über element_aendern
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Pruefmarke", ["kennzeichen"] = "PK-1",
            ["eigenschaften"] = new JsonObject { ["dokument"] = pb, ["form"] = "wolke", ["punkte"] = new JsonArray(new JsonArray(100, 100), new JsonArray(200, 160)), ["text"] = "Leitung zu nah an Wand" },
        }));
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "PK-1", ["eigenschaften"] = new JsonObject { ["status"] = "erledigt" } }));
        Assert.Equal("erledigt", Hole(p, "PK-1").Text("status"));
        Nein(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Pruefmarke", ["eigenschaften"] = new JsonObject { ["dokument"] = pb, ["form"] = "kreis", ["punkte"] = new JsonArray() } }));
        Assert.Throws<HlksFehler>(() => PruefDokument.Info(p, "E.01"));   // ein Raum ist kein PDF
    }
}
