namespace Hlks.Kern.Tests;

/// <summary>Netz.Verbraucher (Gesamtprüfung 13.11.2026, Leistung): die Suche mit allen Strangbauteilen als Start liefert dasselbe wie
/// die bisherige Suche je Bauteil (<see cref="Netz.VerbraucherEinzeln"/>) – gleiche Endgeräte in gleicher Reihenfolge, auch mit
/// Anschlüssen «beide», Maschen, Endgeräten im Strang und fremden Strängen auf dem Weg.</summary>
public class NetzVerbraucherVergleichTests
{
    static readonly Katalog Kat = Katalog.Laden(Katalog.StandardOrdner());
    static readonly string[] Fluesse = ["rein", "raus", "beide", "beide"];

    /// <summary>Fiktives Zufallsnetz: Rohre und Heizkörper mit 1–3 Anschlüssen beliebiger Richtung, zufällig verbunden (je Anschluss
    /// höchstens eine Verbindung), auf mehrere Stränge verteilt oder ohne Strang.</summary>
    static Modell Zufallsnetz(int saat, int anzahl, int straenge, string systemtyp, string medium, bool gleicheKennzeichen = false)
    {
        var z = new Random(saat);
        var ops = new List<Operation>();
        void Dazu(Element e) => ops.Add(new Operation { Art = "element.setzen", Id = e.Id, Element = e });
        Dazu(new Element { Id = "SYS", Kennzeichen = "SYS", Klasse = "System", Eigenschaften = { ["systemtyp"] = new Wert { Inhalt = System.Text.Json.Nodes.JsonValue.Create(systemtyp) }, ["medium"] = new Wert { Inhalt = System.Text.Json.Nodes.JsonValue.Create(medium) } } });
        for (int s = 0; s < straenge; s++) Dazu(new Element { Id = $"ST{s}", Kennzeichen = $"ST-{s}", Klasse = "Strang", TeilVon = "SYS" });
        var frei = new List<(string Element, string Anschluss)>();
        for (int i = 0; i < anzahl; i++)
        {
            bool geraet = z.Next(5) == 0;
            var e = new Element
            {
                Id = $"E{i}", Kennzeichen = gleicheKennzeichen && geraet ? $"HK-{i % 3}" : $"{(geraet ? "HK" : "RO")}-{i}",
                Klasse = geraet ? "Heizkoerper" : "Rohr", System = "SYS",
                TeilVon = z.Next(4) == 0 ? null : $"ST{z.Next(straenge)}",
            };
            int n = geraet ? 1 + z.Next(2) : 2 + z.Next(2);
            for (int a = 0; a < n; a++)
            {
                e.Anschluesse.Add(new Anschluss { Name = $"a{a}", Fluss = Fluesse[z.Next(Fluesse.Length)] });
                frei.Add((e.Id, $"a{a}"));
            }
            Dazu(e);
        }
        // Anschlüsse paaren: meist mit einem Nachbarn in der Nähe (lange Wege), manchmal quer durchs Netz (Maschen)
        int nr = 0;
        while (frei.Count > 1)
        {
            int i = z.Next(frei.Count);
            var a = frei[i]; frei.RemoveAt(i);
            int j = z.Next(3) == 0 ? z.Next(frei.Count) : Math.Clamp(i + z.Next(-4, 5), 0, frei.Count - 1);
            var b = frei[j]; frei.RemoveAt(j);
            if (z.Next(6) == 0) continue;   // offene Enden
            ops.Add(new Operation { Art = "verbindung.setzen", Id = $"V{nr}", Verbindung = new Verbindung { Id = $"V{nr++}", Von = new(a.Element, a.Anschluss), Nach = new(b.Element, b.Anschluss) } });
        }
        var m = new Modell();
        Assert.Empty(m.Anwenden(ops));
        return m;
    }

    static int Vergleichen(Modell m)
    {
        int gefunden = 0;
        foreach (var st in m.DerKlasse("Strang").ToList())
        {
            var alt = Netz.VerbraucherEinzeln(m, Kat, st);
            var neu = Netz.Verbraucher(m, Kat, st);
            Assert.Equal(alt.Direkt.Select(e => e.Id), neu.Direkt.Select(e => e.Id));
            Assert.Equal(alt.Gesamt.Select(e => e.Id), neu.Gesamt.Select(e => e.Id));
            gefunden += alt.Gesamt.Count + alt.Direkt.Count;
        }
        return gefunden;
    }

    [Theory]
    [InlineData("Heizung Vorlauf", "HZ-VL")]    // Verbraucher stromab
    [InlineData("Heizung Rücklauf", "HZ-RL")]   // Verbraucher stromauf
    public void Verbraucher_Zufallsnetze_WieSucheJeBauteil(string systemtyp, string medium)
    {
        int gefunden = 0;
        for (int saat = 1; saat <= 40; saat++)
            gefunden += Vergleichen(Zufallsnetz(saat, 60 + saat * 7, 1 + saat % 5, systemtyp, medium));
        Assert.True(gefunden > 1000, $"Die Netze sollen nicht trivial sein (gefunden {gefunden}).");
    }

    [Fact]
    public void Verbraucher_GleicheKennzeichenUndDoppeltBelegterAnschluss_WieSucheJeBauteil()
    {
        // gleiche Kennzeichen: das Modell führt je Kennzeichen nur eines im Index, die Liste sortiert aber nach dem Text
        for (int saat = 1; saat <= 10; saat++)
            Vergleichen(Zufallsnetz(saat, 150, 3, "Heizung Vorlauf", "HZ-VL", gleicheKennzeichen: true));

        // ein Anschluss mit zwei Verbindungen: dort hängt Folgen von der Eintrittsseite ab – es rechnet die bisherige Suche
        for (int saat = 1; saat <= 10; saat++)
        {
            var m = Zufallsnetz(saat, 150, 3, "Heizung Vorlauf", "HZ-VL");
            var z = new Random(saat);
            var ops = new List<Operation>();
            for (int i = 0; i < 12; i++)
                ops.Add(new Operation
                {
                    Art = "verbindung.setzen", Id = $"D{i}",
                    Verbindung = new Verbindung { Id = $"D{i}", Von = new($"E{z.Next(150)}", "a0"), Nach = new($"E{z.Next(150)}", "a0") },
                });
            Assert.Empty(m.Anwenden(ops));
            Vergleichen(m);
        }
    }
}
