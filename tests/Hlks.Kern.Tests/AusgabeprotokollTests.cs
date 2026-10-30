using System.Text.Json.Nodes;

namespace Hlks.Kern.Tests;

/// <summary>Ausgabeprotokoll (Phase 1 Rang 4, 30.10.2026): jede Dateiausgabe hinterlässt eine Zeile mit Datei, Prüfsumme und Modellstand.</summary>
public sealed class AusgabeprotokollTests : IDisposable
{
    static readonly Katalog Kat = Katalog.Laden(Katalog.StandardOrdner());
    readonly string wurzel = Path.Combine(Path.GetTempPath(), "hlks_ausgabe_" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() { try { Directory.Delete(wurzel, true); } catch (IOException) { } }

    [Fact]
    public void Ausgabe_wird_mit_Pruefsumme_und_Modellstand_vermerkt()
    {
        var p = Projekt.Anlegen(Kat, new OrdnerSpeicher(Path.Combine(wurzel, "e_KICAD")), "Ausgabe (fiktiv)", "CS", "pc-a");
        Assert.True(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Gebaeude", ["kennzeichen"] = "GEB-1" }).Angenommen);
        var datei = Path.Combine(wurzel, "plan.pdf");
        File.WriteAllText(datei, "Inhalt");
        Assert.Null(Ausgabeprotokoll.Vermerken(p, "plan_pdf", datei));
        var l = (JsonObject)Tuer.Abfrage(p, "ausgaben", new JsonObject());
        var e = Assert.Single(l["ausgaben"]!.AsArray())!;
        Assert.Equal("plan_pdf", e["art"]!.GetValue<string>());
        Assert.Equal(datei, e["datei"]!.GetValue<string>());
        Assert.Equal(12, e["pruefsumme"]!.GetValue<string>().Length);
        Assert.Equal(p.Stand.Pruefsumme, e["modellPruefsumme"]!.GetValue<string>());
        Assert.Equal(p.Stand.LetztesPaket, e["letztesPaket"]!.GetValue<string>());
        Assert.True(File.Exists(Path.Combine(wurzel, "e_KICAD", "ausgabe", "protokoll", "pc-a.jsonl")));
    }
}
