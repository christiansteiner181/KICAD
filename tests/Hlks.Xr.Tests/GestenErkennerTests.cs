// Tests der Quest App V1b-1 (Modul XR, Gestenlabor): Vorbereiten, Schwelle (Leave-one-out), Einordnen (erkannt, abgelehnt, unklar) an den synthetischen Reihen.
// Die Raten stehen in der Testausgabe (dotnet test --logger "console;verbosity=detailed").

using Hlks.Xr.Gesten;
using Xunit.Abstractions;
using G = Hlks.Xr.Tests.GestenGenerator;

namespace Hlks.Xr.Tests;

public class GestenErkennerTests(ITestOutputHelper aus)
{
    static readonly Lazy<GestenErkenner> Auftrag = new(() => new GestenErkenner(G.Auftrag.Select(G.Fertig)));

    /// <summary>Rohaufnahme vorzeigen und zuschneiden (gespiegelte Rechts-/Links-Gesten mit «beliebig», damit die bewegte Hand zählt).</summary>
    static Zuschnitt Vorzeigen(Testgeste t, Vorfuehrung v)
    {
        var h = t.Haendigkeit is Haendigkeit.Rechts or Haendigkeit.Links && v.Gespiegelt ? Haendigkeit.Beliebig : t.Haendigkeit;
        return AufnahmeHilfe.Zuschneiden(G.Erzeugen(t.Bewegung, v), h);
    }

    [Fact]
    public void Weniger_als_drei_Beispiele_ungueltig_ab_drei_gueltig()
    {
        var g = G.Fertig(G.TStoss);
        var zwei = GestenErkenner.Vorbereiten(g with { Beispiele = g.Beispiele.Take(2).ToList() });
        Assert.False(zwei.Gueltig);
        Assert.Contains("2 von mindestens 3", zwei.Grund);
        var drei = GestenErkenner.Vorbereiten(g with { Beispiele = g.Beispiele.Take(3).ToList() });
        Assert.True(drei.Gueltig);
        Assert.Equal(3, drei.LooAbstaende.Count);
        // der Erkenner nimmt die ungültige Geste nicht
        var k = new GestenErkenner([g with { Id = "zwei", Funktion = "Zwei", Beispiele = g.Beispiele.Take(2).ToList() }]);
        Assert.Single(k.Alle);
        Assert.Empty(k.Gueltige);
        Assert.Equal(ErkennungsArt.Abgelehnt, k.Einordnen(g.Beispiele[0]).Art);
    }

    [Fact]
    public void Schwelle_aus_Leave_one_out_feste_Schwelle_geht_vor()
    {
        var e = GestenEinstellungen.Vorgabe;
        foreach (var t in G.Auftrag)
        {
            var v = GestenErkenner.Vorbereiten(G.Fertig(t));
            Assert.True(v.Gueltig, v.Grund);
            Assert.Equal(5, v.Beispiele.Count);
            Assert.All(v.LooAbstaende, d => Assert.True(d > 0 && double.IsFinite(d)));
            T.Nah(v.Schwelle, Math.Clamp(e.SchwellenFaktor * v.LooAbstaende.Max(), e.SchwelleMin, e.SchwelleMax), 1e-12, t.Id);
            T.Nah(v.Streuung, v.LooAbstaende.Average(), 1e-12);
            Assert.True(v.DauerMinS > 0.2 && v.DauerMaxS < 1.5);
            aus.WriteLine($"{t.Id,-18} Schwelle {v.Schwelle:0.000}  Streuung {v.Streuung:0.000}  Dauer {v.DauerMinS:0.00}–{v.DauerMaxS:0.00} s");
        }
        Assert.Equal(0.3, GestenErkenner.Vorbereiten(G.Fertig(G.TKreis) with { Schwelle = 0.3 }).Schwelle);
    }

    [Fact]
    public void Erkennung_der_Testgesten_mit_Tempo_Groesse_Drehung_Rauschen_und_Spiegelung()
    {
        var k = Auftrag.Value;
        int soll = 0, richtig = 0, fehl = 0, unklar = 0, spiegelSoll = 0, spiegelAbgelehnt = 0;
        foreach (var t in G.Auftrag)
        {
            var nurEineSeite = t.Haendigkeit is Haendigkeit.Rechts or Haendigkeit.Links;
            int r = 0, n = 0;
            foreach (var (text, v) in G.Abwandlungen(t, gespiegelt: true))
            {
                var z = Vorzeigen(t, v);
                Assert.True(z.Ok, $"{t.Id} {text}: Zuschnitt {z.Rueckmeldung}");
                var u = k.Einordnen(z.Aufnahme!);
                if (u.Art == ErkennungsArt.Unklar) unklar++;
                var alsDiese = u.Art == ErkennungsArt.Erkannt && u.Beste!.GestenId == t.Id;
                if (u.Art == ErkennungsArt.Erkannt && u.Beste!.GestenId != t.Id) fehl++;
                if (v.Gespiegelt && nurEineSeite)
                {
                    // nur rechts: mit der linken Hand gespiegelt vorgezeigt ist es nicht diese Geste
                    spiegelSoll++;
                    if (!alsDiese) spiegelAbgelehnt++;
                    continue;
                }
                soll++; n++;
                if (alsDiese) { richtig++; r++; }
                else aus.WriteLine($"  nicht erkannt: {t.Id} {text}: {u.Art} {u.Beste?.GestenId} {u.Beste?.Verhaeltnis:0.00}");
            }
            aus.WriteLine($"{t.Id,-18} {r}/{n} erkannt");
        }
        aus.WriteLine($"Gesamt {richtig}/{soll} erkannt ({100.0 * richtig / soll:0.#} %), {fehl} falsch erkannt, {unklar} unklar; gespiegelt nur rechts: {spiegelAbgelehnt}/{spiegelSoll} abgelehnt");
        Assert.True(richtig >= 0.95 * soll, $"Erkennungsrate {richtig}/{soll}");
        Assert.Equal(0, fehl);
        Assert.Equal(spiegelSoll, spiegelAbgelehnt);
    }

    [Fact]
    public void Zufaellige_Bewegungen_und_ruhende_Haende_werden_abgelehnt()
    {
        var k = Auftrag.Value;
        int geprueft = 0, abgelehnt = 0;
        var nah = double.PositiveInfinity;
        for (var s = 0; s < 40; s++)
        {
            var roh = G.Erzeugen(G.Zufall(s, mitLinks: s % 3 == 0), new Vorfuehrung { DauerS = 1.2 + 0.02 * s, Saat = 9000 + s });
            var z = AufnahmeHilfe.Zuschneiden(roh, s % 3 == 0 ? Haendigkeit.Beidhaendig : Haendigkeit.Beliebig);
            foreach (var a in new[] { z.Aufnahme, roh })
            {
                if (a is null) continue;
                geprueft++;
                var u = k.Einordnen(a);
                if (u.Art == ErkennungsArt.Abgelehnt) abgelehnt++;
                else aus.WriteLine($"  Zufall {s}: {u.Art} {u.Beste?.GestenId} {u.Beste?.Verhaeltnis:0.00}");
                if (u.Beste is { } b) nah = Math.Min(nah, b.Verhaeltnis);
            }
        }
        aus.WriteLine($"Zufall: {abgelehnt}/{geprueft} abgelehnt, nächstes Verhältnis {nah:0.00}");
        Assert.Equal(geprueft, abgelehnt);

        // ruhende Hände: keine Bewegung zum Zuschneiden, als Ganzes und in Stücken abgelehnt
        var ruhe = G.Erzeugen(G.Ruhe, new Vorfuehrung { DauerS = 3, Saat = 77 });
        Assert.Equal(AufnahmeRueckmeldung.KeineBewegung, AufnahmeHilfe.Zuschneiden(ruhe, Haendigkeit.Beliebig).Rueckmeldung);
        Assert.Equal(ErkennungsArt.Abgelehnt, k.Einordnen(ruhe).Art);
        for (var t0 = 0.0; t0 < 3; t0 += 0.25)
            Assert.Equal(ErkennungsArt.Abgelehnt, k.Einordnen(ruhe.Teil(t0, t0 + 0.5)).Art);
    }

    [Fact]
    public void Zwei_fast_gleiche_Gesten_ergeben_unklar()
    {
        var a = G.Fertig(G.TWischLinks);
        var b = G.Geste(G.TWischLinks, saat: 4242, id: "wisch-links-2", funktion: "SeiteVorZwei");
        var k = new GestenErkenner([a, b]);
        int n = 0, unklar = 0, abgelehnt = 0;
        foreach (var (text, v) in G.Abwandlungen(G.TWischLinks, false))
        {
            n++;
            var u = k.Einordnen(Vorzeigen(G.TWischLinks, v).Aufnahme!);
            if (u.Art == ErkennungsArt.Unklar) unklar++;
            if (u.Art == ErkennungsArt.Abgelehnt) abgelehnt++;
            if (u.Art != ErkennungsArt.Unklar) aus.WriteLine($"  {text}: {u.Art} {u.Beste?.GestenId} {u.Beste?.Verhaeltnis:0.00} / {u.Zweite?.Verhaeltnis:0.00}");
            if (u.Art == ErkennungsArt.Unklar) Assert.NotEqual(u.Beste!.GestenId, u.Zweite!.GestenId);
        }
        aus.WriteLine($"Unklar {unklar}/{n}, abgelehnt {abgelehnt}");
        Assert.True(unklar >= 0.8 * n, $"nur {unklar}/{n} unklar");
        Assert.Equal(0, abgelehnt);
    }

    [Fact]
    public void Entscheid_nach_Verhaeltnis_zur_Schwelle()
    {
        static GestenTreffer Tr(string id, double q) => new(id, id, "F" + id, q * 0.1, 0.1, HandSeite.Rechts);
        Assert.Equal(GestenUrteil.Keines, GestenErkenner.Entscheiden([]));
        var u = GestenErkenner.Entscheiden([Tr("a", 1.2)]);
        Assert.Equal(ErkennungsArt.Abgelehnt, u.Art);
        Assert.Equal("a", u.Beste!.GestenId);
        Assert.Equal(ErkennungsArt.Erkannt, GestenErkenner.Entscheiden([Tr("b", 0.9), Tr("a", 0.5)]).Art);
        Assert.Equal("a", GestenErkenner.Entscheiden([Tr("b", 0.9), Tr("a", 0.5)]).Beste!.GestenId);
        Assert.Equal(ErkennungsArt.Unklar, GestenErkenner.Entscheiden([Tr("a", 0.5), Tr("b", 0.6)]).Art);
        Assert.Equal(ErkennungsArt.Unklar, GestenErkenner.Entscheiden([Tr("a", 0.5), Tr("b", 0.625)]).Art);   // genau UnklarAnteil 0.25
        Assert.Equal(ErkennungsArt.Erkannt, GestenErkenner.Entscheiden([Tr("a", 0.5), Tr("b", 0.63)]).Art);
        Assert.Equal(ErkennungsArt.Erkannt, GestenErkenner.Entscheiden([Tr("a", 0.95), Tr("b", 1.05)]).Art);   // b über seiner Schwelle
        Assert.Equal(ErkennungsArt.Erkannt, GestenErkenner.Entscheiden([Tr("a", 0.5), Tr("b", 0.6)], GestenEinstellungen.Vorgabe with { UnklarAnteil = 0.1 }).Art);
        T.Nah(Tr("x", 0.7).Verhaeltnis, 0.7, 1e-12);
    }

    [Fact]
    public void Auseinander_ist_sauber_getrennt_von_Hebe_Wisch_links_und_Stoss()
    {
        // Ergänzung Christian 24.10.2026: beidhändig aus der Mitte seitwärts, Handflächen nach aussen
        Testgeste[] vier = [G.THebe, G.TWischLinks, G.TStoss, G.TAuseinander];
        var k = new GestenErkenner(vier.Select(G.Fertig));
        foreach (var t in vier)
        {
            var naechsteFremde = double.PositiveInfinity;
            foreach (var (text, v) in G.Abwandlungen(t, gespiegelt: t.Haendigkeit != Haendigkeit.Rechts))
            {
                var u = k.Einordnen(Vorzeigen(t, v).Aufnahme!);
                Assert.True(u.Art == ErkennungsArt.Erkannt && u.Beste!.GestenId == t.Id, $"{t.Id} {text}: {u.Art} {u.Beste?.GestenId}");
                // Auseinander gegen die drei anderen und die drei gegen Auseinander
                foreach (var x in u.Alle.Where(x => x.GestenId != t.Id && (t == G.TAuseinander || x.GestenId == G.TAuseinander.Id)))
                    naechsteFremde = Math.Min(naechsteFremde, x.Verhaeltnis);
            }
            // Wisch links und Stoss zeigt nur die rechte Hand: «Auseinander» (beidhändig) ist dann gar nicht vergleichbar (unendlich weit)
            aus.WriteLine($"{t.Id}: nächstes fremdes Verhältnis (zu bzw. von Auseinander) {naechsteFremde:0.00}");
            if (t == G.TAuseinander || t == G.THebe) Assert.True(double.IsFinite(naechsteFremde));
            if (double.IsFinite(naechsteFremde)) Assert.True(naechsteFremde > 2, $"{t.Id}: {naechsteFremde}");
        }
    }

    [Fact]
    public void Ungueltige_Beispiele_fallen_mit_Hinweis_weg_ungueltige_Id_und_doppelte_Id()
    {
        var g = G.Fertig(G.TWischLinks);
        var nurLinks = G.Erzeugen(G.WischLinks, new Vorfuehrung { Gespiegelt = true, DauerS = 0.4 });   // die rechte Hand fehlt
        var einBild = new Aufnahme([g.Beispiele[0].Bilder[0]]);
        var v = GestenErkenner.Vorbereiten(g with { Beispiele = [.. g.Beispiele.Take(3), nurLinks, einBild] });
        Assert.True(v.Gueltig);
        Assert.Equal(3, v.Beispiele.Count);
        Assert.Equal(2, v.Hinweise.Count);
        Assert.Contains(v.Hinweise, h => h.Contains("Beispiel 4") && h.Contains("Hand fehlt"));
        Assert.Contains(v.Hinweise, h => h.Contains("Beispiel 5"));
        Assert.False(GestenErkenner.Vorbereiten(g with { Id = "Böse Id" }).Gueltig);
        Assert.False(GestenErkenner.Vorbereiten(g with { Funktion = "1Funktion" }).Gueltig);
        Assert.Throws<ArgumentException>(() => new GestenErkenner([g, g with { Funktion = "Anders" }]));
    }

    [Fact]
    public void Beliebig_nimmt_Beispiele_beider_Haende_rechte_Form()
    {
        // Beispiele zur Hälfte mit der linken Hand vorgezeigt: alle in der Form der rechten Hand, erkannt mit beiden Händen
        var r = G.Fertig(G.TKreis);
        var links = Enumerable.Range(0, 3).Select(i => Vorzeigen(G.TKreis, new Vorfuehrung { DauerS = 1, Gespiegelt = true, Saat = 300 + i, BogenM = 0.015 }).Aufnahme!).ToList();
        var g = r with { Beispiele = [.. r.Beispiele.Take(2), .. links] };
        var v = GestenErkenner.Vorbereiten(g);
        Assert.Equal([Merkmalart.Rechts, Merkmalart.Rechts, Merkmalart.LinksGespiegelt, Merkmalart.LinksGespiegelt, Merkmalart.LinksGespiegelt],
            v.Beispiele.Select(b => b.Art));
        var k = new GestenErkenner([g]);
        var mitLinks = k.Einordnen(Vorzeigen(G.TKreis, new Vorfuehrung { DauerS = 0.9, Gespiegelt = true, Saat = 999 }).Aufnahme!);
        Assert.Equal(ErkennungsArt.Erkannt, mitLinks.Art);
        Assert.Equal(HandSeite.Links, mitLinks.Beste!.Seite);
        Assert.Equal(HandSeite.Rechts, k.Einordnen(Vorzeigen(G.TKreis, new Vorfuehrung { DauerS = 0.9, Saat = 998 }).Aufnahme!).Beste!.Seite);
    }
}
