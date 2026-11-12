using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Dämmdicken nach Energiegesetz (Christian 09.11.2026: «dämmdicken gem. energiegesetzt»): MuKEn 2014 Anhang 4, kataloge/daemmung.json.</summary>
public class DaemmungTests
{
    [Fact]
    public void MuKEn2014_DickeNachDn()
    {
        var gross = Kat.Daemmung!.Regel("MuKEn 2014 (λ > 0,03 W/(m·K))")!;
        var klein = Kat.Daemmung!.Regel("MuKEn 2014 (λ ≤ 0,03 W/(m·K))")!;
        Assert.Equal(40, gross.Dicke(15));
        Assert.Equal(50, gross.Dicke(25));
        Assert.Equal(100, gross.Dicke(125));
        Assert.Equal(120, gross.Dicke(200));
        Assert.Equal(50, gross.Dicke(18));      // zwischen zwei Stufen: nächstgrössere (Annahme)
        Assert.Equal(40, gross.Dicke(8));       // unter DN 10: erste Stufe (Annahme)
        Assert.Null(gross.Dicke(250));          // über DN 200 kein Wert
        Assert.Equal(30, klein.Dicke(15));
        Assert.Equal(80, klein.Dicke(200));
        // jede Regel ist als Auswahl der Systemeigenschaft daemmregel wählbar
        var auswahl = Kat.Klasse("System").Eigenschaften.Single(e => e.Name == "daemmregel").Auswahl!;
        Assert.Equal(Kat.Daemmung.Namen.Order(), auswahl.Order());
    }

    [Fact]
    public void Daemmarten_Katalog_RegelNurMitLambdaUndQuelle()
    {
        // Christian 12.11.2026: «Dämmingsarten sollten auswählbar sein», «Ja, Katalog» – eindeutige Namen; eine λ-Klasse (Regel) nur mit λ und Quelle
        var arten = Kat.Daemmung!.Arten;
        Assert.NotEmpty(arten);
        Assert.Equal(arten.Count, arten.Select(a => a.Name).Distinct().Count());
        Assert.All(arten, a =>
        {
            Assert.False(string.IsNullOrWhiteSpace(a.Name));
            if (a.Regel is not null) { Assert.NotNull(Kat.Daemmung.Regel(a.Regel)); Assert.NotNull(a.Lambda); Assert.False(string.IsNullOrWhiteSpace(a.Quelle)); }
        });
        // ohne λ-Klasse gilt die Regel des Systems
        Assert.Equal("MuKEn 2014 (λ > 0,03 W/(m·K))", Kat.Daemmung.RegelFuer(arten[0].Name, "MuKEn 2014 (λ > 0,03 W/(m·K))")!.Name);
        Assert.Null(Kat.Daemmung.RegelFuer("unbekannt", null));
    }
}
