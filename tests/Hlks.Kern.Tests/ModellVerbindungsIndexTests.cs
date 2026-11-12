namespace Hlks.Kern.Tests;

/// <summary>Index Element → Verbindungen des Modells (Gesamtprüfung 13.11.2026): VerbindungenVon liefert nach jeder Art von Änderung dasselbe wie
/// das Durchsuchen aller Verbindungen.</summary>
public class ModellVerbindungsIndexTests
{
    static Operation Setzen(string id, int basis, string von, string nach) => new()
    {
        Art = "verbindung.setzen", Id = id, Basis = basis,
        Verbindung = new Verbindung { Id = id, Von = new(von, "ende"), Nach = new(nach, "anfang") },
    };

    static void Gleich(Modell m)
    {
        foreach (var el in new[] { "A", "B", "C", "D", "X" })
            Assert.Equal(
                m.Verbindungen.Where(v => v.Von.Element == el || v.Nach.Element == el).Select(v => v.Id).Order(),
                m.VerbindungenVon(el).Select(v => v.Id).Order());
    }

    [Fact]
    public void VerbindungenVon_FolgtSetzenUmhaengenLoeschenKopieUndLaden()
    {
        var m = new Modell();
        m.Anwenden([Setzen("V1", 0, "A", "B"), Setzen("V2", 0, "B", "C"), Setzen("V3", 0, "C", "C")]);
        Gleich(m);
        Assert.Equal(2, m.VerbindungenVon("B").Count());
        Assert.Single(m.VerbindungenVon("C"), v => v.Id == "V3");   // Verbindung eines Elements mit sich selbst nur einmal
        Assert.Empty(m.VerbindungenVon("X"));

        m.Anwenden([Setzen("V2", 1, "B", "D")]);                    // umhängen: C verliert V2, D bekommt sie
        Gleich(m);
        Assert.DoesNotContain(m.VerbindungenVon("C"), v => v.Id == "V2");
        Assert.Same(m.FindeVerbindung("V2"), Assert.Single(m.VerbindungenVon("D")));   // der aktuelle Stand, nicht der alte

        m.Anwenden([Setzen("V1", 7, "A", "C")], "P-0001");          // Konflikt (falsche Basis), ältere Änderung gegen keinen Paketstand: übernommen
        Gleich(m);

        var kopie = m.Kopie();
        m.Anwenden([new Operation { Art = "verbindung.loeschen", Id = "V2", Basis = 2 }]);
        Gleich(m);
        Assert.Empty(m.VerbindungenVon("D"));
        Gleich(kopie);
        Assert.Single(kopie.VerbindungenVon("D"));                   // die Kopie hat ihren eigenen Index

        var geladen = new Modell();
        geladen.Anwenden([Setzen("V9", 0, "X", "A")]);
        geladen.Laden(kopie.Abbild([]));
        Gleich(geladen);
        Assert.Empty(geladen.VerbindungenVon("X"));
    }
}
