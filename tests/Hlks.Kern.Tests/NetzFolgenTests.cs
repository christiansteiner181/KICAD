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

    static Operation RohrLang(string id, double laenge)
    {
        var o = Rohr(id);
        o.Element!.Geometrie = new Geometrie { Art = "linie", Punkte = [[0, 0, 0], [laenge, 0, 0]] };
        return o;
    }

    static Operation Knoten(string id) => new()
    {
        Art = "element.setzen", Id = id,
        Element = new Element { Id = id, Kennzeichen = id, Klasse = "TStueck", Anschluesse = [new Anschluss { Name = "a", Fluss = "beide" }, new Anschluss { Name = "b", Fluss = "beide" }, new Anschluss { Name = "c", Fluss = "beide" }] },
    };

    static Operation An(string rohr, string rohrAn, string knoten, string knotenAn) => new()
    {
        Art = "verbindung.setzen", Id = $"V-{rohr}-{knoten}-{knotenAn}",
        Verbindung = new Verbindung { Id = $"V-{rohr}-{knoten}-{knotenAn}", Von = new(rohr, rohrAn), Nach = new(knoten, knotenAn) },
    };

    /// <summary>Frage 35 (Christian 13.11.2026): in einer Masche gilt der Weg mit dem grössten Druckverlust – bei gleichem spezifischem Druckverlust der
    /// längste. Z (1 m) → T1 → kurzer Ast A (1 m) bzw. langer Ast B (3 m) → T2 → E (2 m): ab Z (der Start zählt nicht mit) bis E 3 + 2 = 5 m über den langen Ast, nicht 1 + 2 = 3 m über den mit den wenigsten Bauteilen.</summary>
    [Fact]
    public void Folgen_Masche_LaengeBisHier_ist_der_laengste_Weg()
    {
        var m = new Modell();
        Assert.Empty(m.Anwenden([RohrLang("Z", 1000), Knoten("T1"), RohrLang("A", 1000), RohrLang("B", 3000), Knoten("T2"), RohrLang("E", 2000),
            An("Z", "ende", "T1", "a"), An("A", "anfang", "T1", "b"), An("B", "anfang", "T1", "c"),
            An("A", "ende", "T2", "a"), An("B", "ende", "T2", "c"), An("E", "anfang", "T2", "b")]));
        var ab = Netz.Folgen(m, Kat, m.Hole("Z"), "stromab");
        Assert.Equal(5000, ab.Single(x => x.Element.Kennzeichen == "E").LaengeBisHier, 6);
        Assert.Equal(1000, ab.Single(x => x.Element.Kennzeichen == "A").LaengeBisHier, 6);   // ohne Masche davor bleibt die Länge
    }
}
