using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3, Korrektur 27 der Abnahme (19.10.2026): Im Beispielprojekt «MFH Musterweg» folgt jedes waagrechte Stück einer Leitung
/// mit Eigenschaft gefaelle diesem Gefälle (Fliessrichtung anfang → ende, Abweichung höchstens 0.1 %-Punkte, Regel P16). Senkrechte
/// Stücke und Fallstrecken ab 45° sind ausgenommen. Die Prüfung P16 selbst gilt nur für Leitungen mit Serie (das Beispielprojekt hat
/// keine); dieser Test rechnet deshalb selbst nach. Handrechnung der Fälle in den Kommentaren.</summary>
public class BeispielGefaelleTests
{
    static readonly Projekt P = Beispielprojekt.Erzeugen(Kat, null);

    /// <summary>Neigung in % (positiv = fällt) je waagrechtes Stück; ausgenommen: senkrecht (waagrecht höchstens 1 mm) und Fall ab 45°.</summary>
    static List<(int Stueck, double Neigung)> Neigungen(Element l)
    {
        var p = l.Geometrie!.Punkte;
        var r = new List<(int, double)>();
        for (int i = 1; i < p.Count; i++)
        {
            double h = Math.Sqrt(Math.Pow(p[i][0] - p[i - 1][0], 2) + Math.Pow(p[i][1] - p[i - 1][1], 2));
            double dz = p[i][2] - p[i - 1][2];
            if (h <= 1.0 || -dz >= h) continue;
            r.Add((i, -dz / h * 100));
        }
        return r;
    }

    static List<Element> MitGefaelle() => P.Modell.Elemente.Where(e => Kat.IstArt(e, "Leitung") && e.Zahl("gefaelle") is not null).OrderBy(e => e.Kennzeichen, StringComparer.Ordinal).ToList();

    [Fact]
    public void JedesWaagrechteStueckEinerLeitungMitGefaelle_FaelltInFliessrichtungMitDemWert()
    {
        var leitungen = MitGefaelle();
        Assert.True(leitungen.Count >= 25, $"nur {leitungen.Count} Leitungen mit gefaelle");
        var fehler = new List<string>();
        int stuecke = 0;
        foreach (var l in leitungen)
        {
            double soll = l.Zahl("gefaelle")!.Value;
            foreach (var (i, ist) in Neigungen(l))
            {
                stuecke++;
                if (ist <= 0) fehler.Add($"{l.Kennzeichen} Stück {i} steigt oder liegt waagrecht ({ist:0.###} %)");
                else if (Math.Abs(ist - soll) > Leitungspruefungen.GefaelleToleranz) fehler.Add($"{l.Kennzeichen} Stück {i}: {ist:0.###} % statt {soll:0.###} %");
            }
        }
        Assert.True(stuecke >= 30, $"nur {stuecke} waagrechte Stücke geprüft");
        Assert.True(fehler.Count == 0, string.Join("; ", fehler));
    }

    [Fact]
    public void AlleAbwasserleitungenMitGefaelleSindImTestEnthalten()
    {
        // Gegenprobe zum Umfang: Schmutzwasser-Leitungen mit gefaelle (Etagenleitungen, Sammel- und Grundleitung, Waschmaschine)
        var kz = MitGefaelle().Select(l => l.Kennzeichen).ToList();
        foreach (var erwartet in new[] { "SW-S1-1", "SW-S1-2", "SW-G1-1", "SW-G1-2", "SW-U.02-WM", "SW-E.02-WT", "SW-1.03-DU", "SW-E.01-SP" })
            Assert.Contains(erwartet, kz);
        Assert.All(MitGefaelle(), l => Assert.Equal("SW", P.Modell.Finde(l.System)!.Kennzeichen));
    }

    [Fact]
    public void KeineBefundeP13BisP16_ImBeispielprojekt()
    {
        var b = Pruefungen.Alle(P.Modell, Kat).Where(x => x.Code is "P13" or "P14" or "P15" or "P16").ToList();
        Assert.True(b.Count == 0, string.Join("; ", b.Select(x => $"{x.Code} {x.Element}: {x.Text}")));
    }

    /// <summary>Handrechnung SW-G1-2 (1 %): Stück 1 von TS-U.02 (9000,2500) nach (11800,2500), waagrecht 2800 mm → 28 mm Fall;
    /// Stück 2 nach (11800,9800), 7300 mm → 73 mm Fall; zusammen 101 mm bis TS-G1 bei −3000 → TS-U.02 bei −2899 (bisher −2950, 0.27 % im zweiten Stück).</summary>
    [Fact]
    public void Grundleitung_G1_2_Genau1Prozent_TStueckU02BeiMinus2899()
    {
        var l = Hole(P, "SW-G1-2");
        Assert.Equal([-2899.0, -2927.0, -3000.0], l.Geometrie!.Punkte.Select(q => q[2]).ToArray());
        Assert.Equal(-2899.0, Hole(P, "TS-U.02").Geometrie!.Punkte[0][2]);
        var n = Neigungen(l);
        Assert.Equal(2, n.Count);
        Assert.All(n, x => Assert.Equal(1.0, x.Neigung, 6));
    }

    /// <summary>Handrechnung SW-G1-1 (2 %): waagrecht 1700 mm (11800 → 13500) → 34 mm Fall; Kontrollschacht von −3040 auf −3034
    /// (bisher 40 mm = 2.35 %).</summary>
    [Fact]
    public void Grundleitung_G1_1_Genau2Prozent_KontrollschachtBeiMinus3034()
    {
        var l = Hole(P, "SW-G1-1");
        Assert.Equal(2.0, Assert.Single(Neigungen(l)).Neigung, 6);
        Assert.Equal(-3034.0, Hole(P, "KS-1").Geometrie!.Punkte[0][2]);
    }

    /// <summary>Handrechnung SW-U.02-WM (2 %): zweites Stück von (8000,1000) nach (9000,2500), waagrecht √(1000² + 1500²) = 1802.78 mm →
    /// 36.06 mm Fall, auf 36 mm gerundet = 1.997 % (bisher 0 %); Anschluss TS-U.02 bei −2899, Knick bei −2863. BA-U.02 mündet senkrecht
    /// bei −2899 in den Anschluss c.</summary>
    [Fact]
    public void Waschmaschinenleitung_U02_FaelltMit2Prozent_BodenablaufMuendetAufTStueck()
    {
        var wm = Hole(P, "SW-U.02-WM");
        Assert.Equal([-2300.0, -2863.0, -2899.0], wm.Geometrie!.Punkte.Select(q => q[2]).ToArray());
        Assert.Equal(1.997, Assert.Single(Neigungen(wm)).Neigung, 3);
        Assert.Equal(-2899.0, Hole(P, "SW-U.02-BA").Geometrie!.Punkte[^1][2]);
    }

    /// <summary>Handrechnung Bad EG (2 %): Fallstrang-Anschluss (9000,9800) bei z 300 (T-Stück TS-F1-EB); Stück nach T2 (8800,9700) 223.61 mm →
    /// 4.5 mm; weiter über (9300,9700) 500 mm → 10 mm und (9300,9300) 400 mm → 8 mm: T2 304.5, Knick 314.5, T1 322.5.</summary>
    [Fact]
    public void Etagenleitung_Bad_EG_HoehenAusDemGefaelleAbFallstrang()
    {
        Assert.Equal(304.5, Hole(P, "TS-SW-E.02-2").Geometrie!.Punkte[0][2], 6);
        Assert.Equal(322.5, Hole(P, "TS-SW-E.02-1").Geometrie!.Punkte[0][2], 6);
        Assert.Equal([322.5, 314.5, 304.5], Hole(P, "SW-E.02-1").Geometrie!.Punkte.Select(q => q[2]).ToArray());
        Assert.Equal(300.0, Hole(P, "SW-E.02-2").Geometrie!.Punkte[^1][2]);
        Assert.Equal(300.0, Hole(P, "TS-F1-EB").Geometrie!.Punkte[0][2]);
        // 1.OG: gleiche Höhen über der Geschosskote 2900
        Assert.Equal(2900 + 304.5, Hole(P, "TS-SW-1.02-2").Geometrie!.Punkte[0][2], 6);
        Assert.Equal(3200.0, Hole(P, "SW-1.02-2").Geometrie!.Punkte[^1][2]);   // Bogen BO-F1-1
    }

    /// <summary>Jedes Leitungsende an einem punktförmigen Formteil (T-Stück, Bogen …) liegt auf dessen Lage (höchstens 1 mm,
    /// wie P13) – so bleiben die Verbindungen nach dem Anpassen der Höhen stimmig. Gilt für Schmutzwasser (Gefälle korrigiert).</summary>
    [Fact]
    public void LeitungsendenSchmutzwasser_LiegenAufDenVerbundenenFormteilen()
    {
        int geprueft = 0;
        var fehler = new List<string>();
        foreach (var v in P.Modell.Verbindungen)
            foreach (var (a, b) in new[] { (v.Von, v.Nach), (v.Nach, v.Von) })
            {
                var l = P.Modell.Finde(a.Element); var f = P.Modell.Finde(b.Element);
                if (l is null || f is null || !Kat.IstArt(l, "Leitung") || !Kat.IstArt(f, "Formteil") || f.Geometrie?.Punkte.Count != 1) continue;
                if (P.Modell.Finde(l.System)?.Kennzeichen != "SW") continue;
                var p = l.Geometrie!.Punkte;
                var ende = a.Anschluss.Equals("anfang", StringComparison.OrdinalIgnoreCase) ? p[0] : p[^1];
                double d = Geometrie.Abstand(ende, f.Geometrie!.Punkte[0]);
                geprueft++;
                if (d > Anschlusslagen.AbstandMax) fehler.Add($"{l.Kennzeichen}.{a.Anschluss} – {f.Kennzeichen}.{b.Anschluss}: {d:0.###} mm");
            }
        Assert.True(geprueft >= 40, $"nur {geprueft} Verbindungen geprüft");
        Assert.True(fehler.Count == 0, string.Join("; ", fehler));
    }
}
