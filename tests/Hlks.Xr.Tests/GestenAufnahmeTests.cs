// Tests der Quest App V1b-1 (Modul XR, Gestenlabor): Aufnahme-Hilfe – Bewegungsenergie, Zuschneiden einer Rohaufnahme, Rückmeldungen und GestenRekorder.

using Hlks.Xr.Gesten;
using G = Hlks.Xr.Tests.GestenGenerator;

namespace Hlks.Xr.Tests;

public class GestenAufnahmeTests
{
    static readonly GestenEinstellungen E = GestenEinstellungen.Vorgabe;

    [Theory]
    [InlineData("wisch-links")]
    [InlineData("kreis")]
    [InlineData("faust")]
    [InlineData("hebe")]
    [InlineData("auseinander")]
    public void Zuschneiden_findet_Start_und_Ende_der_Bewegung_zwischen_Ruhe(string id)
    {
        var t = G.Zehn.Single(x => x.Id == id);
        var v = new Vorfuehrung { DauerS = t.DauerS, RuheVorS = 0.8, RuheNachS = 0.7, Saat = 21 };
        var roh = G.Erzeugen(t.Bewegung, v);
        var z = AufnahmeHilfe.Zuschneiden(roh, t.Haendigkeit);
        Assert.True(z.Ok, z.Rueckmeldung.ToString());
        // Minimum-Jerk: die langsamen Enden (unter der Energieschwelle) fallen weg, der Schnitt liegt innerhalb der Bewegung ± Rand
        Assert.InRange(z.StartS, v.RuheVorS - E.RandS - 0.02, v.RuheVorS + 0.25 * t.DauerS);
        Assert.InRange(z.EndeS, v.RuheVorS + 0.75 * t.DauerS, v.RuheVorS + t.DauerS + E.RandS + 0.02);
        Assert.Equal(z.StartS, z.Aufnahme!.Start, 1e-9);
        Assert.Equal(z.EndeS, z.Aufnahme.Ende, 1e-9);
        Assert.Equal(t.Haendigkeit == Haendigkeit.Beidhaendig ? null : HandSeite.Rechts, z.AktiveHand);
    }

    [Fact]
    public void Energie_ruhender_Haende_bleibt_unter_der_Schwelle_auch_bei_5_mm_Rauschen()
    {
        var ruhe = G.Erzeugen(G.Ruhe, new Vorfuehrung { DauerS = 2, Saat = 4 });
        foreach (var s in new[] { HandSeite.Links, HandSeite.Rechts })
            Assert.True(AufnahmeHilfe.Energie(ruhe, s).Max() < E.EnergieAn, s.ToString());
        var faust = G.Erzeugen(G.Faust, new Vorfuehrung { DauerS = 0.5, Saat = 5 });
        Assert.True(AufnahmeHilfe.Energie(faust, HandSeite.Rechts).Max() > 2 * E.EnergieAn, "die Faust bewegt die Finger");
        Assert.All(AufnahmeHilfe.Energie(faust, HandSeite.Links), x => Assert.Equal(0, x));   // linke Hand nicht verfolgt
    }

    [Fact]
    public void Rueckmeldungen_keine_Bewegung_zu_kurz_zu_lang_Hand_verloren_keine_Ruhe()
    {
        Assert.Equal(AufnahmeRueckmeldung.KeineBewegung,
            AufnahmeHilfe.Zuschneiden(G.Erzeugen(G.Ruhe, new Vorfuehrung { DauerS = 1.5 }), Haendigkeit.Rechts).Rueckmeldung);
        Assert.Equal(AufnahmeRueckmeldung.KeineBewegung, AufnahmeHilfe.Zuschneiden(new Aufnahme([]), Haendigkeit.Rechts).Rueckmeldung);
        // ein schneller Stoss (0.3 s), wenn mindestens 0.6 s verlangt sind
        var kurz = AufnahmeHilfe.Zuschneiden(G.Erzeugen(G.Stoss, new Vorfuehrung { DauerS = 0.3 }), Haendigkeit.Rechts, E with { MinDauerS = 0.6 });
        Assert.Equal(AufnahmeRueckmeldung.ZuKurz, kurz.Rueckmeldung);
        Assert.True(kurz.DauerS < 0.6);
        // drei Kreise in 3 s
        GestenBewegung kreise = u => G.Kreis(3 * u);   // drei Kreise hintereinander
        var lang = AufnahmeHilfe.Zuschneiden(G.Erzeugen(kreise, new Vorfuehrung { DauerS = 3 }), Haendigkeit.Rechts);
        Assert.Equal(AufnahmeRueckmeldung.ZuLang, lang.Rueckmeldung);
        Assert.NotNull(lang.Aufnahme);
        // Hand verloren mitten in der Bewegung
        var roh = G.Erzeugen(G.WischLinks, new Vorfuehrung { DauerS = 0.4 });
        var mitte = roh.Bilder.Count / 2;
        var verloren = new Aufnahme(roh.Bilder.Select((b, i) => i == mitte ? b with { Rechts = HandBild.Fehlt } : b).ToList());
        Assert.Equal(AufnahmeRueckmeldung.HandVerloren, AufnahmeHilfe.Zuschneiden(verloren, Haendigkeit.Rechts).Rueckmeldung);
        // falsche Hand: rechts verlangt, links vorgezeigt
        var links = G.Erzeugen(G.WischLinks, new Vorfuehrung { DauerS = 0.4, Gespiegelt = true });
        Assert.Equal(AufnahmeRueckmeldung.KeineBewegung, AufnahmeHilfe.Zuschneiden(links, Haendigkeit.Rechts).Rueckmeldung);
        Assert.True(AufnahmeHilfe.Zuschneiden(links, Haendigkeit.Links).Ok);
        // beidhändig verlangt, nur eine Hand da
        Assert.Equal(AufnahmeRueckmeldung.HandVerloren, AufnahmeHilfe.Zuschneiden(roh, Haendigkeit.Beidhaendig).Rueckmeldung);
        // Bewegung schon am Anfang bzw. noch am Ende der Aufnahme
        Assert.Equal(AufnahmeRueckmeldung.KeineRuheVorher,
            AufnahmeHilfe.Zuschneiden(G.Erzeugen(G.Kreis, new Vorfuehrung { DauerS = 1, RuheVorS = 0 }).Teil(0.3, 9), Haendigkeit.Rechts).Rueckmeldung);
        Assert.Equal(AufnahmeRueckmeldung.KeineRuheNachher,
            AufnahmeHilfe.Zuschneiden(G.Erzeugen(G.Kreis, new Vorfuehrung { DauerS = 1, RuheNachS = 0 }).Teil(0, 1.1), Haendigkeit.Rechts).Rueckmeldung);
        foreach (var r in Enum.GetValues<AufnahmeRueckmeldung>()) Assert.False(string.IsNullOrWhiteSpace(AufnahmeHilfe.Text(r)));
        Assert.Contains("2 s", AufnahmeHilfe.Text(AufnahmeRueckmeldung.ZuLang));
    }

    [Fact]
    public void Beliebig_nimmt_die_bewegte_Hand_und_mehrere_Bewegungen_die_staerkste()
    {
        var links = G.Erzeugen(G.Stoss, new Vorfuehrung { DauerS = 0.45, Gespiegelt = true });
        var z = AufnahmeHilfe.Zuschneiden(links, Haendigkeit.Beliebig);
        Assert.True(z.Ok);
        Assert.Equal(HandSeite.Links, z.AktiveHand);
        Assert.Equal(HandSeite.Links, AufnahmeHilfe.AktiveHand(links));
        // ein kleines Zucken, dann der Wisch: es gilt der Wisch
        var (strom, bew) = G.Strom([
            (u => (null, G.RuheRechts with { Mitte = G.RuheRechts.Mitte + new Vec3(0.05 * u, 0, 0) }), new Vorfuehrung { DauerS = 0.3, RuheVorS = 0.4, RuheNachS = 0.2 }),
            (G.WischLinks, new Vorfuehrung { DauerS = 0.4, RuheVorS = 0.3, RuheNachS = 0.5 })], uebergangS: 0.5);
        var w = AufnahmeHilfe.Zuschneiden(strom, Haendigkeit.Rechts);
        Assert.True(w.Ok, w.Rueckmeldung.ToString());
        Assert.InRange(w.StartS, bew[1].Start - 0.1, bew[1].Ende);
    }

    [Fact]
    public void Rekorder_wartet_laeuft_endet_bei_ruhigen_Haenden_und_schneidet_zu()
    {
        var roh = G.Erzeugen(G.Kreis, new Vorfuehrung { DauerS = 1, RuheVorS = 1, RuheNachS = 1.5, Saat = 31 });
        var r = new GestenRekorder(Haendigkeit.Beliebig);
        Assert.Equal(RekorderZustand.Wartet, r.Zustand);
        var gesehen = new List<RekorderZustand>();
        double? fertigBei = null;
        foreach (var b in roh.Bilder)
        {
            var z = r.Bild(b);
            if (gesehen.Count == 0 || gesehen[^1] != z) gesehen.Add(z);
            if (z == RekorderZustand.Fertig && fertigBei is null) fertigBei = b.T;
        }
        Assert.Equal([RekorderZustand.Wartet, RekorderZustand.Laeuft, RekorderZustand.Fertig], gesehen);
        Assert.InRange(r.BewegungSeit!.Value, 1, 1.3);
        Assert.InRange(fertigBei!.Value, 2 + E.RuheS - 0.2, 2 + E.RuheS + 0.3);   // Ruhe RuheS nach dem Ende
        Assert.True(r.Ergebnis!.Ok, r.Ergebnis.Rueckmeldung.ToString());
        var offline = AufnahmeHilfe.Zuschneiden(roh, Haendigkeit.Beliebig);
        Assert.Equal(offline.StartS, r.Ergebnis.StartS, 1e-9);
        Assert.Equal(offline.EndeS, r.Ergebnis.EndeS, 1e-9);
        // nach dem Ende ändert sich nichts mehr
        Assert.Equal(RekorderZustand.Fertig, r.Bild(roh.Bilder[^1] with { T = 99 }));
    }

    [Fact]
    public void Rekorder_ohne_Bewegung_gibt_auf_ohne_Ende_meldet_zu_lang()
    {
        var r = new GestenRekorder(Haendigkeit.Rechts);
        foreach (var b in G.Erzeugen(G.Ruhe, new Vorfuehrung { DauerS = 6 }).Bilder) r.Bild(b);
        Assert.Equal(RekorderZustand.Fertig, r.Zustand);
        Assert.Equal(AufnahmeRueckmeldung.KeineBewegung, r.Ergebnis!.Rueckmeldung);

        var dauernd = new GestenRekorder(Haendigkeit.Rechts);
        GestenBewegung kreise = u => G.Kreis(4 * u);
        foreach (var b in G.Erzeugen(kreise, new Vorfuehrung { DauerS = 4, RuheNachS = 0 }).Bilder) dauernd.Bild(b);
        Assert.Equal(RekorderZustand.Fertig, dauernd.Zustand);
        Assert.Equal(AufnahmeRueckmeldung.ZuLang, dauernd.Ergebnis!.Rueckmeldung);
        // Bilder mit nicht aufsteigender Zeit fallen weg
        var r2 = new GestenRekorder(Haendigkeit.Rechts);
        var b0 = G.Erzeugen(G.Ruhe, new Vorfuehrung { DauerS = 0.1 }).Bilder[0];
        Assert.Equal(RekorderZustand.Wartet, r2.Bild(b0));
        Assert.Equal(RekorderZustand.Wartet, r2.Bild(b0));
        Assert.Equal(RekorderZustand.Wartet, r2.Bild(b0 with { T = double.NaN }));
    }
}
