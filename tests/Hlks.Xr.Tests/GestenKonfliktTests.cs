// Tests der Quest App V1b-1 (Modul XR, Gestenlabor): Konfliktprüfung – Trennschärfe gegen die anderen eigenen Gesten (Warnstufe) und gegen die festen
// Regelgesten (Einhängepunkt IFesteGestenRegel, WischRegel über die öffentliche API von XrGesten).

using Hlks.Xr.Gesten;
using Xunit.Abstractions;
using G = Hlks.Xr.Tests.GestenGenerator;

namespace Hlks.Xr.Tests;

public class GestenKonfliktTests(ITestOutputHelper aus)
{
    static readonly WischRegel PinchWisch = new();
    static readonly WischRegel OffenWisch = new(mitPinch: false);

    void Zeigen(KonfliktBericht b)
    {
        aus.WriteLine($"{b.GestenId}: Stufe {b.Stufe}, Trennschärfe {b.Trennschaerfe:0.00}, Schwelle {b.Schwelle:0.000}, Streuung {b.Streuung:0.000}");
        foreach (var p in b.Gegen) aus.WriteLine($"    gegen {p.AndereId,-18} {p.Trennschaerfe,6:0.00} {p.Stufe}");
        foreach (var f in b.Feste) aus.WriteLine($"    Beispiel {f.Beispiel + 1} löst «{f.Ausloesung.Geste}» aus ({f.Ausloesung.Seite}, {f.Ausloesung.T:0.00} s)");
    }

    [Fact]
    public void Die_Testgesten_sind_gegenseitig_trennscharf()
    {
        var alle = G.Auftrag.Select(G.Fertig).ToList();
        foreach (var g in alle)
        {
            var b = GestenKonflikte.Pruefen(g, alle);
            Zeigen(b);
            Assert.True(b.Gueltig);
            Assert.Equal(alle.Count - 1, b.Gegen.Count);   // sich selbst nicht
            Assert.Equal(KonfliktStufe.Keine, b.Stufe);
            Assert.True(b.Trennschaerfe >= GestenEinstellungen.Vorgabe.KonfliktWarnFaktor);
        }
    }

    [Fact]
    public void Dieselbe_Bewegung_fuer_eine_andere_Funktion_ist_ein_Konflikt_dieselbe_Funktion_wird_ersetzt()
    {
        var a = G.Fertig(G.TWischLinks);
        var b = G.Geste(G.TWischLinks, saat: 4242, id: "wisch-links-2", funktion: "SeiteVorZwei");
        var bericht = GestenKonflikte.Pruefen(b, [a, G.Fertig(G.TKreis)]);
        Zeigen(bericht);
        Assert.Equal(KonfliktStufe.Konflikt, bericht.Stufe);
        Assert.True(bericht.Trennschaerfe <= 1);
        Assert.Equal("wisch-links", bericht.Gegen[0].AndereId);
        Assert.Equal(KonfliktStufe.Keine, bericht.Gegen[1].Stufe);
        // gleiche Funktion: die neue ersetzt die alte, kein Vergleich
        var ersatz = GestenKonflikte.Pruefen(b with { Funktion = a.Funktion }, [a]);
        Assert.Empty(ersatz.Gegen);
        Assert.Equal(KonfliktStufe.Keine, ersatz.Stufe);
        Assert.True(double.IsPositiveInfinity(ersatz.Trennschaerfe));
    }

    [Fact]
    public void Aehnliche_Bewegung_ergibt_eine_Warnung_Stufen()
    {
        // Wisch links, die Bahn 11° nach unten geneigt: knapp getrennt (Warnung). 9° wäre fast ein Konflikt, 15° keine Warnung mehr (synthetische Reihe).
        GestenBewegung schraeg = u => (null, G.WischLinks(u).Rechts! with
        {
            Mitte = G.WischLinks(u).Rechts!.Mitte + new Vec3(0, -0.35 * u * Math.Tan(11 * Math.PI / 180), 0),
        });
        var t = new Testgeste("wisch-links-schraeg", "Wisch links schräg", "Schraeg", Haendigkeit.Rechts, schraeg, 0.4);
        var b = GestenKonflikte.Pruefen(G.Geste(t, saat: 5151), [G.Fertig(G.TWischLinks)]);
        Zeigen(b);
        Assert.Equal(KonfliktStufe.Warnung, b.Stufe);
        Assert.InRange(b.Trennschaerfe, 1, GestenEinstellungen.Vorgabe.KonfliktWarnFaktor);
        // Stufen an den Grenzen
        Assert.Equal(KonfliktStufe.Konflikt, GestenKonflikte.Stufe(0.5));
        Assert.Equal(KonfliktStufe.Konflikt, GestenKonflikte.Stufe(1));
        Assert.Equal(KonfliktStufe.Warnung, GestenKonflikte.Stufe(1.2));
        Assert.Equal(KonfliktStufe.Keine, GestenKonflikte.Stufe(1.5));
        Assert.Equal(KonfliktStufe.Keine, GestenKonflikte.Stufe(double.PositiveInfinity));
    }

    [Fact]
    public void Fester_Wisch_mit_Pinch_Hebegeste_und_Auseinander_loesen_ihn_nicht_aus_Wisch_hoch_mit_Pinch_schon()
    {
        // Hebegeste und Auseinander: offene Hände, kein Pinch – der feste Wisch der App (mit gehaltenem Pinch, P5a) löst nicht aus
        foreach (var t in new[] { G.THebe, G.TAuseinander })
        {
            var b = GestenKonflikte.Pruefen(G.Fertig(t), [], [PinchWisch]);
            Zeigen(b);
            Assert.Empty(b.Feste);
            Assert.Equal(KonfliktStufe.Keine, b.Stufe);
        }
        // Wisch hoch mit Pinch ist die feste Geste «Pinch + Wisch hoch» (Etage hoch): Konflikt gemeldet
        var w = GestenKonflikte.Pruefen(G.Fertig(G.TWischHochPinch), [], [PinchWisch]);
        Zeigen(w);
        Assert.Equal(KonfliktStufe.Konflikt, w.Stufe);
        Assert.NotEmpty(w.Feste);
        Assert.All(w.Feste, f => Assert.Equal("Pinch + Wisch hoch", f.Ausloesung.Geste));
        Assert.All(w.Feste, f => Assert.Equal(HandSeite.Rechts, f.Ausloesung.Seite));
    }

    [Fact]
    public void Fester_Wisch_mit_offener_Hand_wird_von_Hebegeste_und_Auseinander_ausgeloest_Konflikt_gemeldet()
    {
        // Prototyp (Masslinien) bzw. eine Labor-Variante ohne Pinch: Wisch mit offener Hand in alle Richtungen. Beide beidhändigen Gesten sind schnell genug
        // (Mindesttempo 0.8 m/s, Mindestweg 15 cm): die Prüfung meldet den Konflikt – mit offenem Wisch dürften sie nicht zusammen gelten.
        var hebe = GestenKonflikte.Pruefen(G.Fertig(G.THebe), [], [OffenWisch]);
        Zeigen(hebe);
        Assert.Equal(KonfliktStufe.Konflikt, hebe.Stufe);
        Assert.All(hebe.Feste, f => Assert.Equal("Wisch hoch (offene Hand)", f.Ausloesung.Geste));
        Assert.True(hebe.Feste.Select(f => f.Beispiel).Distinct().Count() >= 3, "die meisten Beispiele");
        var auseinander = GestenKonflikte.Pruefen(G.Fertig(G.TAuseinander), [], [OffenWisch]);
        Zeigen(auseinander);
        Assert.Equal(KonfliktStufe.Konflikt, auseinander.Stufe);
        for (var i = 0; i < 5; i++)
        {
            Assert.Contains(auseinander.Feste, f => f.Beispiel == i && f.Ausloesung is { Geste: "Wisch links (offene Hand)", Seite: HandSeite.Links });
            Assert.Contains(auseinander.Feste, f => f.Beispiel == i && f.Ausloesung is { Geste: "Wisch rechts (offene Hand)", Seite: HandSeite.Rechts });
        }
        // beide Regeln zusammen (wie die App sie einhängen würde): nur die offene meldet
        var beide = GestenKonflikte.Pruefen(G.Fertig(G.TAuseinander), [], [PinchWisch, OffenWisch]);
        Assert.All(beide.Feste, f => Assert.Equal(OffenWisch.Name, f.Ausloesung.Regel));
    }

    [Fact]
    public void Einhaengepunkt_fuer_feste_Regeln_und_ungueltige_Geste()
    {
        var immer = new ImmerRegel();
        var g = G.Fertig(G.TStoss);
        var b = GestenKonflikte.Pruefen(g, [], [immer]);
        Assert.Equal(g.Beispiele.Count, b.Feste.Count);
        Assert.Equal(Enumerable.Range(0, g.Beispiele.Count), b.Feste.Select(f => f.Beispiel));
        Assert.Equal(KonfliktStufe.Konflikt, b.Stufe);
        Assert.Equal("Immer", b.Feste[0].Ausloesung.Regel);
        var ungueltig = GestenKonflikte.Pruefen(g with { Beispiele = g.Beispiele.Take(2).ToList() }, [G.Fertig(G.TKreis)]);
        Assert.False(ungueltig.Gueltig);
        Assert.Empty(ungueltig.Gegen);
        Assert.Contains(ungueltig.Hinweise, h => h.Contains("Zu wenige"));
    }

    sealed class ImmerRegel : IFesteGestenRegel
    {
        public string Name => "Immer";
        public IReadOnlyList<FesteAusloesung> Abspielen(Aufnahme a) => [new FesteAusloesung(Name, "Immer", a.Start, HandSeite.Rechts)];
    }
}
