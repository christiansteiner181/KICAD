// Quest App P4a, Nachtrag Brillentest 23.10.2026 (Christian: «drehen sollte nur beim sitzen aktiviert sein», «der schalter soll drehen ein oder ausschalten»,
// «damit kann die sitzposition übersteuert werden»): Haltung aus der Kopfhöhe über dem Boden, Automatik sitzend → Drehen ein, stehend → aus, der Schalter
// übersteuert bis zum nächsten Haltungswechsel (DrehenSchalter); die Drehgeste wirkt nur, wenn Drehen ein ist, und nach einem Wisch gibt es keinen Sprung
// (HandBegehen, drehen).

using System.Text.Json;

namespace Hlks.Xr.Tests;

public class DrehenSchalterTests
{
    /// <summary>Schritte im Abstand von 0.1 s mit der Kopfhöhe h(t); gibt (t, Drehen) nach jedem Schritt zurück.</summary>
    static List<(double T, bool Drehen)> Lauf(DrehenZustand z, double von, double bis, Func<double, double?> h, bool boden = true, HaltungEinstellungen? e = null)
    {
        var aus = new List<(double, bool)>();
        for (int i = 0; von + i * 0.1 <= bis + 1e-9; i++)
        {
            var t = Math.Round(von + i * 0.1, 6);
            aus.Add((t, DrehenSchalter.Schritt(z, t, h(t), boden, e)));
        }
        return aus;
    }

    // ---------------------------------------------------------------------------------------------- Haltung und Automatik

    [Fact]
    public void Vorgaben_und_Beginn_stehend_Drehen_aus()
    {
        var e = HaltungEinstellungen.Vorgabe;
        Assert.Equal(1.30, e.SchwelleM);
        Assert.Equal(0.15, e.HystereseM);
        Assert.Equal(2, e.HaltezeitS);
        var z = new DrehenZustand();
        Assert.False(z.Sitzend);
        Assert.False(z.Drehen);
        Assert.Null(z.VonHand);
        Assert.Equal("aus (stehend)", z.Text);
    }

    [Fact]
    public void Sitzend_nach_2_s_unter_1_30_m_stehend_nach_2_s_ueber_1_45_m()
    {
        var z = new DrehenZustand();
        var a = Lauf(z, 0, 3, _ => 1.20);   // sitzt
        Assert.All(a.Where(x => x.T < 1.95), x => Assert.False(x.Drehen));
        Assert.All(a.Where(x => x.T >= 2 - 1e-9), x => Assert.True(x.Drehen));
        Assert.Equal("ein (sitzend)", z.Text);
        Assert.All(Lauf(z, 3.1, 8, _ => 1.40), x => Assert.True(x.Drehen));   // im Hystereseband bleibt es
        var b = Lauf(z, 8.1, 11, _ => 1.70);                                   // aufstehen
        Assert.All(b.Where(x => x.T < 10.05), x => Assert.True(x.Drehen));
        Assert.All(b.Where(x => x.T >= 10.1 - 1e-9), x => Assert.False(x.Drehen));
        Assert.Equal("aus (stehend)", z.Text);
    }

    [Fact]
    public void Grenzen_genau_auf_1_30_und_1_45_schalten_nicht()
    {
        var z = new DrehenZustand();
        Assert.All(Lauf(z, 0, 5, _ => 1.30), x => Assert.False(x.Drehen));
        Lauf(z, 5.1, 8, _ => 1.29);
        Assert.True(z.Sitzend);
        Assert.All(Lauf(z, 8.1, 13, _ => 1.45), x => Assert.True(x.Drehen));
    }

    [Fact]
    public void Kurzes_Buecken_und_Aufrichten_schalten_nicht_Haltezeit_am_Stueck()
    {
        var z = new DrehenZustand();
        Assert.All(Lauf(z, 0, 6, t => t is >= 2 and < 3.5 ? 1.0 : 1.75), x => Assert.False(x.Drehen));   // 1.5 s gebückt
        Lauf(z, 6.1, 9, _ => 1.15);
        Assert.True(z.Sitzend);
        Assert.All(Lauf(z, 9.1, 14, t => t is >= 10 and < 11.5 ? 1.70 : 1.15), x => Assert.True(x.Drehen));   // 1.5 s aufgerichtet
        // 1.5 s unten, kurz oben, wieder unten: erst 2 s am Stück zählen
        var z2 = new DrehenZustand();
        Assert.All(Lauf(z2, 0, 4, t => t is >= 1.5 and < 1.7 ? 1.70 : 1.10), x => Assert.Equal(x.T >= 3.7 - 1e-9, x.Drehen));
    }

    [Fact]
    public void Start_ohne_Bodenbezug_stehend_ohne_Drehen()
    {
        var z = new DrehenZustand();
        Assert.All(Lauf(z, 0, 3, _ => 0.4, boden: false), x => Assert.False(x.Drehen));
        Assert.True(z.OhneBoden);
        Assert.Equal("stehend", z.Haltung);
        Lauf(z, 3.1, 6, _ => 1.10);   // Bodenbezug kommt: Automatik
        Assert.True(z.Drehen);
        Assert.False(z.OhneBoden);
        DrehenSchalter.Schritt(z, 6.1, 1.10, false);   // Boden verloren: stehend
        Assert.False(z.Drehen);
        Assert.False(DrehenSchalter.Schritt(new DrehenZustand(), 0, null, true));
        Assert.False(DrehenSchalter.Schritt(new DrehenZustand(), 0, double.NaN, true));
    }

    // ---------------------------------------------------------------------------------------------- Schalter übersteuert

    [Fact]
    public void Schalter_uebersteuert_bis_zum_naechsten_Haltungswechsel()
    {
        var z = new DrehenZustand();
        Lauf(z, 0, 3, _ => 1.75);   // steht: aus
        DrehenSchalter.Umschalten(z, true);
        Assert.True(z.Drehen);
        Assert.Equal("ein (von Hand)", z.Text);
        Assert.All(Lauf(z, 3.1, 10, _ => 1.75), x => Assert.True(x.Drehen));   // stehend bleibt stehend: die Wahl hält
        Assert.All(Lauf(z, 10.1, 11.5, _ => 1.20), x => Assert.True(x.Drehen));   // hinsetzen, noch keine 2 s: hält weiter
        Lauf(z, 11.6, 13, _ => 1.20);   // sitzend erkannt: Automatik (ein)
        Assert.Null(z.VonHand);
        Assert.Equal("ein (sitzend)", z.Text);
        DrehenSchalter.Umschalten(z, false);   // im Sitzen aus
        Assert.All(Lauf(z, 13.1, 20, _ => 1.15), x => Assert.False(x.Drehen));
        Assert.Equal("aus (von Hand)", z.Text);
        Lauf(z, 20.1, 23, _ => 1.80);   // aufstehen: Automatik (aus)
        Assert.Null(z.VonHand);
        Assert.Equal("aus (stehend)", z.Text);
        Lauf(z, 23.1, 26, _ => 1.10);   // wieder hinsetzen: ein
        Assert.True(z.Drehen);
    }

    [Fact]
    public void Stellschrauben_der_Haltung()
    {
        var e = HaltungEinstellungen.Vorgabe;
        JsonElement W(string json) => JsonDocument.Parse(json).RootElement;
        Assert.Equal(1.1, e.MitStellschraube("haltung.schwelle_m", W("1.1"))!.SchwelleM);
        Assert.Equal(0.2, e.MitStellschraube("haltung.hysterese_m", W("0.2"))!.HystereseM);
        Assert.Equal(0.5, e.MitStellschraube("haltung.haltezeit_s", W("0.5"))!.HaltezeitS);
        Assert.Equal(1.30, (e with { SchwelleM = 1 }).MitStellschraube("haltung.schwelle_m", W("null"))!.SchwelleM);
        Assert.Null(e.MitStellschraube("haltung.schwelle_m", W("3")));
        Assert.Null(e.MitStellschraube("begehen.ziel_ruhe_s", W("0.3")));
        var z = new DrehenZustand();
        Lauf(z, 0, 0.5, _ => 1.0, e: e with { HaltezeitS = 0.5 });
        Assert.True(z.Sitzend);
    }

    // ---------------------------------------------------------------------------------------------- Drehgeste

    static readonly Vec3 Zeigen = new(0.2, 1.2, -0.4);

    static Dictionary<string, Vec3> Hand(Vec3 p, double spalt) => new()
    {
        ["index-finger-phalanx-proximal"] = p, ["index-finger-tip"] = p + new Vec3(0, -0.02, -0.05), ["thumb-tip"] = p + new Vec3(spalt, -0.02, -0.05),
    };

    /// <summary>Pinch, schneller Wisch nach rechts (25 cm in 0.2 s), danach 0.7 s ruhig auf den Boden gezeigt, Loslassen.</summary>
    static List<BegehenEreignis> WischUndHalten(bool drehen)
    {
        var z = new HandBegehenZustand();
        var ev = new List<BegehenEreignis>();
        void S(double t, Dictionary<string, Vec3> g) => ev.AddRange(HandBegehen.Schritt(z, t, "right", g, new Vec3(0, 1.7, 0), Math.PI / 2, 0, drehen: drehen));
        for (int i = 0; i < 10; i++) S(i / 90.0, Hand(Zeigen, 0.01));
        for (int i = 0; i <= 18; i++) S((10 + i) / 90.0, Hand(Zeigen + new Vec3(0.25 * i / 18, 0, 0), 0.01));
        for (int i = 29; i < 90; i++) S(i / 90.0, Hand(Zeigen + new Vec3(0.25, 0, 0), 0.01));
        S(1.0, Hand(Zeigen + new Vec3(0.25, 0, 0), 0.06));
        return ev;
    }

    [Fact]
    public void Drehen_ein_dreht_der_Wisch_um_30_Grad()
    {
        var ev = WischUndHalten(drehen: true);
        Assert.Equal(1, Assert.Single(ev, e => e.Art == BegehenArt.Drehen).Schritt);   // nach rechts gewischt dreht nach links (umgekehrt, Christian 23.10.2026)
        Assert.DoesNotContain(ev, e => e.Art is BegehenArt.Teleport or BegehenArt.Abbruch);
    }

    [Fact]
    public void Drehen_aus_dreht_nicht_und_springt_nach_dem_Wisch_nicht()
    {
        var ev = WischUndHalten(drehen: false);
        Assert.DoesNotContain(ev, e => e.Art is BegehenArt.Drehen or BegehenArt.Teleport);
        Assert.Equal(BegehenArt.PinchAus, ev[^1].Art);
        // ohne Wisch springt derselbe Pinch auch mit Drehen aus
        var z = new HandBegehenZustand();
        var sprung = new List<BegehenEreignis>();
        for (int i = 0; i < 45; i++) sprung.AddRange(HandBegehen.Schritt(z, i / 90.0, "right", Hand(Zeigen, 0.01), new Vec3(0, 1.7, 0), Math.PI / 2, 0, drehen: false));
        sprung.AddRange(HandBegehen.Schritt(z, 0.5, "right", Hand(Zeigen, 0.06), new Vec3(0, 1.7, 0), Math.PI / 2, 0, drehen: false));
        Assert.Single(sprung, e => e.Art == BegehenArt.Teleport);
    }
}
