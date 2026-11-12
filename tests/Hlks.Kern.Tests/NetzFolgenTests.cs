namespace Hlks.Kern.Tests;

/// <summary>Netz.Folgen (Gesamtprüfung 13.11.2026): die Suche folgt dem ganzen Netz, auch über 2000 Bauteile hinaus, und endet in
/// einer Masche von selbst.</summary>
public class NetzFolgenTests
{
    static readonly Katalog Kat = Katalog.Laden(Katalog.StandardOrdner());

    static Operation Rohr(string id) => new()
    {
        Art = "element.setzen", Id = id,
        Element = new Element
        {
            Id = id, Kennzeichen = id, Klasse = "Rohr",
            Anschluesse = [new Anschluss { Name = "anfang", Fluss = "rein" }, new Anschluss { Name = "ende", Fluss = "raus" }],
        },
    };

    static Operation Verbunden(string von, string nach) => new()
    {
        Art = "verbindung.setzen", Id = $"V-{von}-{nach}",
        Verbindung = new Verbindung { Id = $"V-{von}-{nach}", Von = new(von, "ende"), Nach = new(nach, "anfang") },
    };

    [Fact]
    public void Folgen_KetteMit2500Rohren_FindetAlle_AbfrageKuerztMitGrenze()
    {
        const int n = 2500;
        var m = new Modell();
        var ops = new List<Operation>();
        for (int i = 0; i < n; i++) ops.Add(Rohr($"R{i}"));
        for (int i = 1; i < n; i++) ops.Add(Verbunden($"R{i - 1}", $"R{i}"));
        Assert.Empty(m.Anwenden(ops));

        var ab = Netz.Folgen(m, Kat, m.Hole("R0"), "stromab");
        Assert.Equal(n - 1, ab.Count);                      // vorher 2000: die letzten 499 fehlten ohne Hinweis
        Assert.Equal($"R{n - 1}", ab[^1].Element.Kennzeichen);
        Assert.Equal(n - 1, ab[^1].Tiefe);
        Assert.Equal(n - 1, Netz.Folgen(m, Kat, m.Hole($"R{n - 1}"), "stromauf").Count);
        Assert.Empty(Netz.Folgen(m, Kat, m.Hole($"R{n - 1}"), "stromab"));
        Assert.Equal(100, Netz.Folgen(m, Kat, m.Hole("R0"), "stromab", maxSchritte: 100).Count);   // die Grenze gilt, wenn sie verlangt ist
    }

    [Fact]
    public void Folgen_Kreislauf_EndetUndBesuchtJedesElementEinmal()
    {
        var m = new Modell();
        Assert.Empty(m.Anwenden([Rohr("A"), Rohr("B"), Rohr("C"), Verbunden("A", "B"), Verbunden("B", "C"), Verbunden("C", "A")]));
        var ab = Netz.Folgen(m, Kat, m.Hole("A"), "stromab");
        Assert.Equal(["B", "C"], ab.Select(s => s.Element.Kennzeichen).ToArray());   // der Start kommt nicht ein zweites Mal
    }
}
