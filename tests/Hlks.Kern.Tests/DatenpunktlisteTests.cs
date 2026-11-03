using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 8 MSRL (Gerüst): Datenpunkte der Grundausstattung vorschlagen und die Datenpunktliste je Anlage zählen (Beispielprojekt).</summary>
public class DatenpunktlisteTests
{
    [Fact]
    public void Datenpunkte_vorschlagen_einmal_und_Liste_zaehlt_je_Signal()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        int vorher = p.Modell.DerKlasse("Datenpunkt").Count();
        Ok(p.Ausfuehren("datenpunkte_vorschlagen", new JsonObject()));
        var neu = p.Modell.DerKlasse("Datenpunkt").Where(d => d.Kennzeichen.StartsWith("DP-")).ToList();
        Assert.NotEmpty(neu);
        // jede Pumpe: Betrieb (DI), Störung (DI), Ein/Aus (DO), am Bauteil
        foreach (var pu in p.Modell.DerKlasse("Pumpe"))
            Assert.Equal(["DI", "DI", "DO"], neu.Where(d => d.Bezuege.GetValueOrDefault("an") == pu.Id).Select(d => d.Text("signal")!).Order().ToArray());
        // zweiter Lauf legt nichts doppelt an
        int nachEins = p.Modell.DerKlasse("Datenpunkt").Count();
        Ok(p.Ausfuehren("datenpunkte_vorschlagen", new JsonObject()));
        Assert.Equal(nachEins, p.Modell.DerKlasse("Datenpunkt").Count());
        // Liste: Summe der Signale je Anlage = Anzahl
        var r = (JsonObject)Tuer.Abfrage(p, "datenpunktliste", new JsonObject());
        Assert.Equal(nachEins, (int)r["anzahl"]!);
        foreach (var a in r["anlagen"]!.AsArray().OfType<JsonObject>())
            Assert.Equal((int)a["anzahl"]!, a["jeSignal"]!.AsObject().Sum(x => (int)x.Value!));
        Assert.True(nachEins > vorher);
    }
}
