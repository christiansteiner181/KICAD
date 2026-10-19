using Xunit.Abstractions;

namespace Hlks.Kern.Tests;

public class BeispielTests(ITestOutputHelper aus)
{
    static readonly Katalog Kat = Katalog.Laden(Katalog.StandardOrdner());

    [Fact]
    public void BeispielEntstehtOhneFehlerbefunde()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        var befunde = Pruefungen.Alle(p.Modell, Kat);
        foreach (var b in befunde) aus.WriteLine($"{b.Schwere} {b.Code} {b.Element}: {b.Text}");
        Assert.DoesNotContain(befunde, b => b.Schwere == "fehler");
        Assert.DoesNotContain(befunde, b => b.Schwere == "warnung");
    }

    [Fact]
    public void KiTestErwartungIstStimmig()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        var e = KiVerstaendnistest.Erwartung(p);
        aus.WriteLine(Json.Text(e, true));
        var b = KiVerstaendnistest.Bewerten(p, e);
        Assert.Equal(KiVerstaendnistest.Fragen.Count, b["richtig"]!.GetValue<int>());
        // eine falsche Antwort wird erkannt
        var falsch = (System.Text.Json.Nodes.JsonObject)e.DeepClone();
        falsch["f5"] = 99;
        Assert.Equal(KiVerstaendnistest.Fragen.Count - 1, KiVerstaendnistest.Bewerten(p, falsch)["richtig"]!.GetValue<int>());
    }

    [Fact]
    public void Ausgaben()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        var a = new Abfragen(p);
        aus.WriteLine($"Elemente {p.Modell.Elemente.Count}, Verbindungen {p.Modell.Verbindungen.Count}, Pakete {p.Verlauf.Count}");
        aus.WriteLine(a.SystemLesen("SW"));
        aus.WriteLine(a.SystemLesen("KW"));
        aus.WriteLine(a.StrangText("SW-F1"));
        aus.WriteLine(a.StrangText("KW-S2"));
        aus.WriteLine(Json.Text(Ableitungen.Strangliste(p), true));
        aus.WriteLine(Json.Text(Ableitungen.Raumliste(p, "EG"), true));
        aus.WriteLine(Json.Text(Ableitungen.Mengen(p), true));
        aus.WriteLine(Json.Text(a.NetzFolgen("WC-1.02", "stromab", null, "Kontrollschacht"), true));
        aus.WriteLine(Json.Text(a.Datenpunkte(null), true));
    }
}
