using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.AussparungHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3b (Hauptlauf, Befund aus B3): eine Aussparung blockiert das Löschen ihrer Leitung bzw. ihres Bauteils nicht –
/// der Bezug wird gelöst, die Aussparung veraltet (Hinweis), Rückgängig stellt alles her. Werte FIKTIV (AussparungHilfe).</summary>
public class AussparungLoeschenTests
{
    [Fact]
    public void LeitungLoeschen_AussparungWirdVeraltetUndVerliertDenBezug()
    {
        var p = Wandprojekt();
        RohrDurchW1(p, "RO-A", 3000);
        Ok(Vorschlagen(p));
        var a = VonLeitung(p, "RO-A");
        var leitung = p.Modell.Hole("RO-A");
        var vorher = p.Modell.Pruefsumme();

        var e = Ok(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "RO-A" }));
        Assert.Contains(e.Hinweise, h => h.StartsWith($"Aussparung {a.Kennzeichen}") && h.Contains("Status veraltet"));
        var nachher = p.Modell.Hole(a.Kennzeichen);
        Assert.Equal("veraltet", nachher.Text("status"));
        Assert.DoesNotContain(nachher.Bezuege.Values, v => v == leitung.Id);
        Assert.Null(p.Modell.Finde(leitung.Id));
        Assert.DoesNotContain(Pruefungen.Alle(p.Modell, p.Katalog), b => b.Code == "P08" && b.Element == a.Kennzeichen);
        Assert.Contains(Pruefungen.Alle(p.Modell, p.Katalog), b => b.Code == "P19" && b.Element == a.Kennzeichen);

        Assert.True(p.Rueckgaengig().Angenommen);
        Assert.Equal(vorher, p.Modell.Pruefsumme());
    }

    [Fact]
    public void BauteilLoeschen_AussparungWirdVeraltet_TrockenlaufAendertNichts()
    {
        var p = Wandprojekt();
        RohrDurchW1(p, "RO-A", 3000);
        Ok(Vorschlagen(p));
        var a = VonLeitung(p, "RO-A");
        var wand = p.Modell.Finde(a.Bezuege["bauteil"])!;
        var vorher = p.Modell.Pruefsumme();
        Ok(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = wand.Kennzeichen }, trockenlauf: true));
        Assert.Equal(vorher, p.Modell.Pruefsumme());

        Ok(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = wand.Kennzeichen }));
        var nachher = p.Modell.Hole(a.Kennzeichen);
        Assert.Equal("veraltet", nachher.Text("status"));
        Assert.False(nachher.Bezuege.ContainsKey("bauteil"));
    }
}
