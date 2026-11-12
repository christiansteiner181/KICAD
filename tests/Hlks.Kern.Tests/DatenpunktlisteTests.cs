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

    [Fact]
    public void Datenpunktliste_AnlageUeberNachbarsystem_UndNurAnlagenAlsFilter()
    {
        var p = AnsichtSichtbarkeitTests.Neu();
        var rohr = AnsichtSichtbarkeitTests.Anlegen(p, new JsonObject { ["klasse"] = "Rohr", ["kennzeichen"] = "KW-1", ["system"] = "S-KW", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["dn"] = 32 }, ["geometrie"] = AnsichtSichtbarkeitTests.Linie([0, 0, 2500], [4000, 0, 2500]) });
        // Pumpe ohne eigenes System (freiwillig), am Rohr angeschlossen: ihre Anlage ist die des Rohrs
        var pumpe = AnsichtSichtbarkeitTests.Anlegen(p, new JsonObject { ["klasse"] = "Pumpe", ["kennzeichen"] = "P-1", ["geschoss"] = "EG", ["eigenschaften"] = new JsonObject { ["dn"] = 32 } });
        var v = p.Ausfuehren("verbinden", new JsonObject { ["von"] = "KW-1.ende", ["nach"] = "P-1" });
        Assert.True(v.Angenommen, v.Fehler);
        Assert.Equal("A-1", Datenpunktliste.AnlageVon(p.Modell, pumpe)?.Kennzeichen);
        Assert.True(p.Ausfuehren("datenpunkte_vorschlagen", new JsonObject { ["anlage"] = "A-1" }).Angenommen);
        var liste = (JsonObject)Tuer.Abfrage(p, "datenpunktliste", new JsonObject { ["anlage"] = "A-1" });
        Assert.Equal(3, (int)liste["anzahl"]!);   // Betrieb, Störung, Ein/Aus der Pumpe – vorher «ohne Anlage» und damit 0
        // ein System ist keine Anlage: Meldung statt stiller leerer Liste
        Assert.Contains("kein Anlage", Assert.Throws<HlksFehler>(() => Tuer.Abfrage(p, "datenpunktliste", new JsonObject { ["anlage"] = "S-KW" })).Message);
        Assert.False(p.Ausfuehren("datenpunkte_vorschlagen", new JsonObject { ["anlage"] = "S-KW" }).Angenommen);
    }
}
