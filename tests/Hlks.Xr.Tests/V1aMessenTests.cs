// Tests Quest App V1a (XR-C, Messen): Fang auf der Szene aus den GLB (SzenenFang: Ecken, Kanten, Mitte, Flächen, Leitungsachsen, Strahl, Filter, Schnitt) und das
// Messband (Varianten messband und zwei_punkte, Lot, Liste, Wischgeste).

using Hlks.Xr.Bedienung;
using Hlks.Xr.Messen;
using Hlks.Xr.Modell;
using Hlks.Xr.Netz;
using static Hlks.Xr.Tests.T;

namespace Hlks.Xr.Tests;

public class V1aMessenTests
{
    /// <summary>Szene aus Netzen eines Geschosses «g» (Teil modell).</summary>
    internal static ModellSzene Szene(params GlbNetzDaten[] netze) => new([(ModellHilfen.Datei([0, 0, 0], netze), "g", GlbZwischenspeicher.TeilModell)]);

    /// <summary>Rohr als Zylinder wie der GLB-Schreiber des Kerns (n Strecken je Kreis, Deckel als Fächer ohne Mittelpunkt) zwischen a und b, als Element id.</summary>
    internal static GlbNetzDaten Rohr(string id, Vec3 a, Vec3 b, double r, int n = 24, string art = "leitung")
    {
        var achse = (b - a).Einheit();
        var q = Math.Abs(achse.Y) < 0.9 ? achse.Kreuz(new Vec3(0, 1, 0)).Einheit() : achse.Kreuz(new Vec3(1, 0, 0)).Einheit();
        var w = achse.Kreuz(q).Einheit();
        var pos = new List<float>();
        var ind = new List<uint>();
        foreach (var basis in new[] { a, b })
            for (int i = 0; i < n; i++)
            {
                var phi = 2 * Math.PI * i / n;
                var p = basis + (q * Math.Cos(phi) + w * Math.Sin(phi)) * r;
                pos.AddRange([(float)p.X, (float)p.Y, (float)p.Z]);
            }
        for (int i = 0; i < n; i++)
        {
            uint i0 = (uint)i, i1 = (uint)((i + 1) % n), j0 = (uint)(n + i), j1 = (uint)(n + (i + 1) % n);
            ind.AddRange([i0, i1, j1, i0, j1, j0]);   // Mantel
        }
        for (int i = 1; i + 1 < n; i++) ind.AddRange([0u, (uint)(i + 1), (uint)i]);                 // Deckel a (Fächer ohne Mittelpunkt)
        for (int i = 1; i + 1 < n; i++) ind.AddRange([(uint)n, (uint)(n + i), (uint)(n + i + 1)]);   // Deckel b
        return new GlbNetzDaten
        {
            Name = id, Positionen = [.. pos], Normalen = [.. Enumerable.Repeat(new[] { 0f, 1f, 0f }, pos.Count / 3).SelectMany(x => x)], Indizes = [.. ind],
            Gruppe = "hlks", Bereiche = [new ElementBereich(id, 0, ind.Count / 3, art)],
        };
    }

    /// <summary>Wand 4 m lang (x 0 … 4), 0.2 m dick (z −0.2 … 0), 2.5 m hoch.</summary>
    static GlbNetzDaten Wand() => ModellHilfen.Quadernetz("architektur", ("w", "wand", V(0, 0, -0.2), V(4, 2.5, 0)));

    // ------------------------------------------------------------------------------------------------ Fang mit dem Finger

    [Fact]
    public void Quader_ergibt_8_Ecken_12_Kanten_und_fängt_nach_Rangfolge()
    {
        var f = new SzenenFang(Szene(Wand()));
        var e = f.Fang(f.Szene.Finde("w")!);
        Assert.Equal(8, e.Punkte.Count);
        Assert.Equal(12, e.Strecken.Count);   // Diagonalen der Seiten sind keine Kanten
        Assert.Null(e.Achse);
        // Ecke vor Mitte und Kante
        var ecke = f.Punkt(V(4.01, 2.49, 0.01));
        Assert.Equal(Fangart.Ecke, ecke.Art);
        Assert.Equal("modell:w", ecke.Id);
        Nah(ecke.P!.Value.Abstand(V(4, 2.5, 0)), 0, 1e-6);
        // Mitte der oberen Vorderkante (2, 2.5, 0)
        var mitte = f.Punkt(V(2.005, 2.51, 0.005));
        Assert.Equal(Fangart.Mitte, mitte.Art);
        Nah(mitte.P!.Value.Abstand(V(2, 2.5, 0)), 0, 1e-6);
        // Kante (weg von der Mitte)
        var kante = f.Punkt(V(1.2, 2.51, 0.01));
        Assert.Equal(Fangart.Kante, kante.Art);
        Nah(kante.P!.Value.Abstand(V(1.2, 2.5, 0)), 0, 1e-6);
        // Fläche: Fusspunkt, Normale zur Seite des Fingers
        var fl = f.Punkt(V(1.5, 1.2, 0.02));
        Assert.Equal(Fangart.Flaeche, fl.Art);
        Nah(fl.P!.Value.Abstand(V(1.5, 1.2, 0)), 0, 1e-6);
        Nah(fl.N!.Value.Z, 1, 1e-9);
        Assert.NotNull(fl.Flaeche);
        // frei: der Eingangspunkt
        var frei = f.Punkt(V(1.5, 1.2, 0.5));
        Assert.Equal(Fangart.Frei, frei.Art);
        Assert.Equal(V(1.5, 1.2, 0.5), frei.P);
        Assert.Null(frei.Id);
    }

    [Fact]
    public void Gerade_Kette_wird_eine_Kante_mit_Mitte_in_der_Mitte()
    {
        // Wand, deren Vorderseite längs geteilt ist (Ecken bei x = 1 und x = 3 auf den Längskanten), wie IFC-Netze mit Öffnungen
        float[] pos =
        [
            0, 0, 0, 1, 0, 0, 3, 0, 0, 4, 0, 0,        // unten 0..3
            0, 2, 0, 1, 2, 0, 3, 2, 0, 4, 2, 0,        // oben 4..7
        ];
        uint[] ind = [0, 1, 5, 0, 5, 4, 1, 2, 6, 1, 6, 5, 2, 3, 7, 2, 7, 6];
        var netz = new GlbNetzDaten
        {
            Name = "w", Positionen = pos, Normalen = [.. Enumerable.Repeat(new[] { 0f, 0f, 1f }, 8).SelectMany(x => x)], Indizes = ind, Gruppe = "architektur",
            Bereiche = [new ElementBereich("w", 0, 6, "wand")],
        };
        var f = new SzenenFang(Szene(netz));
        var e = f.Fang(f.Szene.Finde("w")!);
        // offene Fläche: 4 Randkanten (die geteilten Längskanten je eine), 4 Ecken
        Assert.Equal(4, e.Strecken.Count);
        Assert.Equal(4, e.Punkte.Count);
        var mitte = f.Punkt(V(2.01, 2.005, 0.005));
        Assert.Equal(Fangart.Mitte, mitte.Art);
        Nah(mitte.P!.Value.X, 2, 1e-9);
        // die Zwischenecken bei x = 1 und 3 sind keine Ecken
        Assert.Equal(Fangart.Kante, f.Punkt(V(1.0, 2.01, 0.01)).Art);
    }

    [Fact]
    public void Rohr_hat_eine_Leitungsachse_mit_Anschlüssen_und_keine_Ecken_am_Rand()
    {
        var f = new SzenenFang(Szene(Rohr("r", V(0, 2, -1), V(3, 2, -1), 0.05)));
        var e = f.Fang(f.Szene.Finde("r")!);
        Assert.NotNull(e.Achse);
        Nah(e.Radius, 0.05, 1e-3);
        var (a0, b0) = (e.Achse!.A, e.Achse.B);   // Richtung beliebig (Hauptachse)
        Nah(Math.Min(a0.Abstand(V(0, 2, -1)) + b0.Abstand(V(3, 2, -1)), a0.Abstand(V(3, 2, -1)) + b0.Abstand(V(0, 2, -1))), 0, 1e-4, "Enden");
        Assert.Equal(2, e.Punkte.Count(p => p.Art == Fangart.Anschluss));
        Assert.DoesNotContain(e.Punkte, p => p.Art == Fangart.Ecke);   // 24 Strecken je Kreis: Knick 15° am Rand, keine Ecke
        // an der Rohroberfläche fängt die Achse
        var a = f.Punkt(V(1.5, 2.06, -1));
        Assert.Equal(Fangart.Achse, a.Art);
        Assert.Equal("Leitungsachse", a.Name);
        Nah(a.P!.Value.Abstand(V(1.5, 2, -1)), 0, 1e-4);
        // am Ende der Anschluss
        var an = f.Punkt(V(3.02, 2.0, -1));
        Assert.Equal(Fangart.Anschluss, an.Art);
        // mit dem Strahl quer auf das Rohr: Achse
        var s = f.Strahl(V(1.5, 2, 2), V(0, 0, -1));
        Assert.Equal(Fangart.Achse, s.Art);
    }

    [Fact]
    public void Kanal_als_Quader_hat_die_Mittellinie_gekrümmtes_Formteil_keine_Achse()
    {
        var kanal = ModellHilfen.Quadernetz("hlks", ("k", "leitung", V(0, 2.4, -1.2), V(5, 2.6, -0.8)));   // 5 m lang, 0.4 × 0.2 m
        var f = new SzenenFang(Szene(kanal));
        var e = f.Fang(f.Szene.Finde("k")!);
        Assert.NotNull(e.Achse);
        Nah(e.Radius, 0.1, 1e-6);   // halbe kleinere Seite
        var mitte = Vec3.Mitte(e.Achse!.A, e.Achse.B);
        Nah(mitte.Abstand(V(2.5, 2.5, -1)), 0, 1e-6);
        Nah(e.Achse.A.Abstand(e.Achse.B), 5, 1e-6);
        // Wand (keine Leitung) hat keine Achse, ein Bogen (zwei Rohre im rechten Winkel als ein Formteil) auch nicht
        var bogen = Rohr("b", V(0, 0, 0), V(1, 0, 0), 0.05, art: "formteil");
        var zweiter = Rohr("b", V(1, 0, 0), V(1, 0, -1), 0.05, art: "formteil");
        var zusammen = new GlbNetzDaten
        {
            Name = "b", Positionen = [.. bogen.Positionen, .. zweiter.Positionen], Normalen = [.. bogen.Normalen, .. zweiter.Normalen],
            Indizes = [.. bogen.Indizes, .. zweiter.Indizes.Select(i => i + (uint)(bogen.Positionen.Length / 3))], Gruppe = "hlks",
            Bereiche = [new ElementBereich("b", 0, (bogen.Indizes.Length + zweiter.Indizes.Length) / 3, "formteil")],
        };
        var f2 = new SzenenFang(Szene(zusammen));
        Assert.Null(f2.Fang(f2.Szene.Finde("b")!).Achse);
    }

    // ------------------------------------------------------------------------------------------------ Strahl

    [Fact]
    public void Strahl_trifft_die_erste_Fläche_beidseitig_und_fängt_nicht_dahinter()
    {
        var hinten = ModellHilfen.Quadernetz("architektur", ("h", "wand", V(0, 0, -3.2), V(4, 2.5, -3)));
        var f = new SzenenFang(Szene(Wand(), hinten));
        var s = f.Strahl(V(1.5, 1.2, 2), V(0, 0, -1));
        Assert.Equal(Fangart.Flaeche, s.Art);
        Assert.Equal("modell:w", s.Id);
        Nah(s.T!.Value, 2, 1e-9);
        Nah(s.N!.Value.Z, 1, 1e-9);   // gegen den Strahl
        // von hinten (aus der Wand heraus) zählt die Rückseite auch – beidseitig, die Normale zeigt zum Strahl
        var r = f.Strahl(V(1.5, 1.2, -1), V(0, 0, 1));
        Assert.Equal(Fangart.Flaeche, r.Art);
        Nah(r.N!.Value.Z, -1, 1e-9);
        // Ecke nahe am Strahl (Radius 3 cm + 3 cm je m): (4, 2.5, 0) aus 2 m Abstand, 5 cm daneben
        var e = f.Strahl(V(4.05, 2.5, 2), V(0, 0, -1));
        Assert.Equal(Fangart.Ecke, e.Art);
        // nichts hinter der ersten Fläche: die Ecke der hinteren Wand wird durch die vordere nicht gefangen
        var dahinter = f.Strahl(V(2, 2.48, 2), V(0, 0, -1));
        Assert.Equal("modell:w", dahinter.Id);
        // ins Leere
        var leer = f.Strahl(V(10, 10, 10), V(0, 1, 0));
        Assert.Equal(Fangart.Frei, leer.Art);
        Assert.Null(leer.P);
    }

    [Fact]
    public void Filter_Sichtbar_und_Massstab_am_Tisch()
    {
        var f = new SzenenFang(Szene(Wand()));
        f.Filter = e => e.Id != "w";
        Assert.Equal(Fangart.Frei, f.Punkt(V(4.01, 2.49, 0.01)).Art);
        f.Filter = null;
        // Schnitt unter 2 m: die oberen Ecken sind nicht sichtbar, die Fläche darunter schon
        f.Sichtbar = p => p.Y <= 2;
        Assert.NotEqual(Fangart.Ecke, f.Punkt(V(4.01, 2.49, 0.01)).Art);
        Assert.Equal(Fangart.Flaeche, f.Strahl(V(1.5, 2.2, 2), V(0, -0.2, -1)).Art);   // trifft unter 2 m
        Assert.Equal(Fangart.Frei, f.Strahl(V(1.5, 2.4, 2), V(0, 0, -1)).Art);        // nur über 2 m: unsichtbar
        f.Sichtbar = null;
        // am Tisch (1:50): 3 cm am Finger sind 1.5 m im Modell – die App gibt den Radius im Massstab mit
        Assert.Equal(Fangart.Ecke, f.Punkt(V(4.8, 2.5, 0.3), XrMessLogik.FangRadiusFinger * 50).Art);
        Assert.Equal(Fangart.Frei, f.Punkt(V(4.8, 2.5, 0.3)).Art);
    }

    [Fact]
    public void Zwischenspeicher_und_Hilfen()
    {
        var netze = Enumerable.Range(0, 5).Select(i => ModellHilfen.Quadernetz("architektur", ($"w{i}", "wand", V(i * 5, 0, 0), V(i * 5 + 1, 1, 1)))).ToArray();
        var f = new SzenenFang(Szene(netze));
        foreach (var e in f.Szene.Elemente.Values) f.Fang(e);
        Assert.Equal(5, f.Aufbereitet);
        f.Fang(f.Szene.Finde("w0")!);
        Assert.Equal(5, f.Aufbereitet);   // aus dem Zwischenspeicher
        Assert.Equal(1.0, SzenenFang.StrahlDreieck(V(0.2, 0.2, 1), V(0, 0, -1), V(0, 0, 0), V(1, 0, 0), V(0, 1, 0)));
        Assert.Null(SzenenFang.StrahlDreieck(V(0.8, 0.8, 1), V(0, 0, -1), V(0, 0, 0), V(1, 0, 0), V(0, 1, 0)));
        Nah(SzenenFang.BoxStrahl(new Box3(V(0, 0, 0), V(1, 1, 1)), V(0.5, 0.5, 3), V(0, 0, -1), 0)!.Value, 2);
        Assert.Null(SzenenFang.BoxStrahl(new Box3(V(0, 0, 0), V(1, 1, 1)), V(3, 3, 3), V(0, 0, -1), 0));
        var achsen = SzenenFang.Hauptachsen([V(0, 0, 0), V(10, 0, 0), V(0, 1, 0), V(10, 1, 0)], V(5, 0.5, 0));
        Assert.Contains(achsen, a => Math.Abs(Math.Abs(a.X) - 1) < 1e-9);
    }

    // ------------------------------------------------------------------------------------------------ Messband

    static FangErgebnis Fang(Vec3 p, string art = Fangart.Ecke, Ebene3? flaeche = null) => new(art, Fangart.Namen[art], p, "modell:w", flaeche?.N, flaeche);

    [Fact]
    public void Messband_aufziehen_loslassen_ergibt_Messung_mit_dx_dy_dz()
    {
        var z = new MessZustand();
        Messband.AnSetzen(z, true);
        Assert.Contains("aufziehen", z.Hinweis);
        Assert.Empty(Messband.Schritt(z, 0, "right", false, V(0, 1, 0), Fang(V(0, 0, 0)), "begehen"));
        var e1 = Messband.Schritt(z, 0.1, "right", true, V(0, 1, 0), Fang(V(0, 0, 0)), "begehen");
        Assert.Equal(MessEreignisArt.Anfang, Assert.Single(e1).Art);
        Assert.Equal("right", z.Hand);
        // die andere Hand stört nicht
        Assert.Empty(Messband.Schritt(z, 0.2, "left", true, V(0.2, 1, 0), Fang(V(5, 5, 5)), "begehen"));
        Assert.Empty(Messband.Schritt(z, 0.3, "right", true, V(0.3, 1, 0), Fang(V(1, 0.5, -2)), "begehen"));
        var e2 = Messband.Schritt(z, 0.5, "right", false, V(0.3, 1, 0), Fang(V(3, 0.5, -4)), "begehen");
        var fertig = Assert.Single(e2);
        Assert.Equal(MessEreignisArt.Fertig, fertig.Art);
        var m = fertig.Messung!;
        Assert.Equal(1, m.Nr);
        Nah(m.Mass.AbstandMm, Math.Sqrt(9 + 0.25 + 16) * 1000, 1e-6);
        Nah(m.Mass.Dx, 3000, 1e-9);
        Nah(m.Mass.Dy, 4000, 1e-9);   // −z = Nord
        Nah(m.Mass.Dz, 500, 1e-9);
        Assert.Equal("begehen", m.Modus);
        Assert.StartsWith("5025 mm", m.Text);
        Assert.Equal("dx 3000 mm · dy 4000 mm · dz 500 mm", m.Unterschiede);
        Assert.Single(z.Liste);
        Assert.Null(z.Erster);
    }

    [Fact]
    public void Messband_zu_kurz_ohne_Ziel_und_Pinch_ins_Leere()
    {
        var z = new MessZustand();
        Messband.AnSetzen(z, true);
        // zu kurz gehalten
        Messband.Schritt(z, 0, "right", true, V(0, 1, 0), Fang(V(0, 0, 0)), "begehen");
        var a = Assert.Single(Messband.Schritt(z, 0.05, "right", false, V(0.1, 1, 0), Fang(V(1, 0, 0)), "begehen"));
        Assert.Equal(MessEreignisArt.Abgebrochen, a.Art);
        Assert.Contains("zu kurz", a.Grund);
        // Hand kaum bewegt
        Messband.Schritt(z, 1, "right", true, V(0, 1, 0), Fang(V(0, 0, 0)), "begehen");
        Assert.Contains("zu kurz", Assert.Single(Messband.Schritt(z, 1.5, "right", false, V(0.01, 1, 0), Fang(V(1, 0, 0)), "begehen")).Grund);
        // ohne Ziel losgelassen
        Messband.Schritt(z, 2, "right", true, V(0, 1, 0), Fang(V(0, 0, 0)), "begehen");
        Assert.Contains("ohne Ziel", Assert.Single(Messband.Schritt(z, 2.5, "right", false, V(0.3, 1, 0), null, "begehen")).Grund);
        // Pinch ins Leere
        Assert.Equal(MessEreignisArt.OhneZiel, Assert.Single(Messband.Schritt(z, 3, "right", true, V(0, 1, 0), new FangErgebnis(Fangart.Frei, "frei", null, null), "begehen")).Art);
        Assert.Empty(z.Liste);
        // aus: nichts
        Messband.AnSetzen(z, false);
        Assert.Empty(Messband.Schritt(z, 4, "right", false, V(0, 1, 0), null, "begehen"));
        Assert.Empty(Messband.Schritt(z, 4.1, "right", true, V(0, 1, 0), Fang(V(0, 0, 0)), "begehen"));
    }

    [Fact]
    public void Zwei_Punkte_mit_Klicks_ein_langer_Pinch_zählt_nicht()
    {
        var z = new MessZustand();
        Messband.AnSetzen(z, true);
        Messband.VarianteSetzen(z, Varianten.ZweiPunkte);
        Assert.Equal(Messband.TextErsterKlick, z.Hinweis);
        Messband.Schritt(z, 0, "right", true, V(0, 1, 0), Fang(V(0, 0, 0)), "sitzungsraum");
        Assert.Equal(MessEreignisArt.Anfang, Assert.Single(Messband.Schritt(z, 0.2, "right", false, V(0, 1, 0.01), Fang(V(0, 0, 0)), "sitzungsraum")).Art);
        Assert.Equal(Messband.TextZweiterKlick, z.Hinweis);
        // langer Pinch (Wisch, Zögern): kein Punkt
        Messband.Schritt(z, 1, "left", true, V(0, 1, 0), Fang(V(2, 0, 0)), "sitzungsraum");
        Assert.Empty(Messband.Schritt(z, 2.5, "left", false, V(0, 1, 0), Fang(V(2, 0, 0)), "sitzungsraum"));
        // bewegter Pinch: kein Punkt
        Messband.Schritt(z, 3, "left", true, V(0, 1, 0), Fang(V(2, 0, 0)), "sitzungsraum");
        Assert.Empty(Messband.Schritt(z, 3.2, "left", false, V(0.2, 1, 0), Fang(V(2, 0, 0)), "sitzungsraum"));
        // zweiter Klick (mit der anderen Hand) setzt das Ende
        Messband.Schritt(z, 4, "left", true, V(0, 1, 0), Fang(V(0, 0, -2)), "sitzungsraum");
        var e = Assert.Single(Messband.Schritt(z, 4.2, "left", false, V(0, 1, 0), Fang(V(0, 0, -2)), "sitzungsraum"));
        Assert.Equal(MessEreignisArt.Fertig, e.Art);
        Nah(e.Messung!.Mass.AbstandMm, 2000, 1e-9);
        Assert.Equal("sitzungsraum", e.Messung.Modus);
        // Klick ohne Ziel
        Messband.Schritt(z, 5, "right", true, V(0, 1, 0), null, "sitzungsraum");
        Assert.Equal(MessEreignisArt.OhneZiel, Assert.Single(Messband.Schritt(z, 5.1, "right", false, V(0, 1, 0), null, "sitzungsraum")).Art);
        // die Variante zu wechseln verwirft einen angefangenen Punkt
        Messband.Schritt(z, 6, "right", true, V(0, 1, 0), Fang(V(0, 0, 0)), "sitzungsraum");
        Messband.Schritt(z, 6.1, "right", false, V(0, 1, 0), Fang(V(0, 0, 0)), "sitzungsraum");
        Assert.NotNull(z.Erster);
        Messband.VarianteSetzen(z, "laser");   // unbekannt = Messband
        Assert.Equal(Varianten.Messband, z.Variante);
        Assert.Null(z.Erster);
    }

    [Fact]
    public void Lot_misst_lotrecht_zur_Fläche_ohne_Fläche_keine_Messung()
    {
        var z = new MessZustand();
        Messband.AnSetzen(z, true);
        Messband.LotSetzen(z, true);
        Assert.Contains("Lot", z.Hinweis);
        var decke = new Ebene3(V(0, -1, 0), V(0, 2.6, 0));
        // vom Punkt am Boden (schräg) zur Decke: Lot = 2.6 m, Ende = Fusspunkt über dem Anfang
        Messband.Schritt(z, 0, "right", true, V(0, 1, 0), Fang(V(1, 0, -1)), "begehen");
        var e = Assert.Single(Messband.Schritt(z, 0.5, "right", false, V(0.3, 1.3, 0), Fang(V(3, 2.6, -2), Fangart.Flaeche, decke), "begehen"));
        var m = e.Messung!;
        Assert.Equal(MessTyp.Lot, m.Typ);
        Nah(m.Mass.AbstandMm, 2600, 1e-9);
        Nah(m.B.P.Abstand(V(1, 2.6, -1)), 0, 1e-9);
        Assert.StartsWith("Lot 2600 mm", m.Text);
        // ohne Fläche: abgebrochen mit Hinweis
        Messband.Schritt(z, 1, "right", true, V(0, 1, 0), Fang(V(1, 0, -1)), "begehen");
        var a = Assert.Single(Messband.Schritt(z, 1.5, "right", false, V(0.3, 1.3, 0), Fang(V(3, 1, -2)), "begehen"));
        Assert.Equal(MessEreignisArt.Abgebrochen, a.Art);
        Assert.Equal(Messband.TextLotBraucht, a.Grund);
        Assert.Equal(Messband.TextLotBraucht, z.Hinweis);
        // der erste Punkt auf der Fläche genügt (Lot vom zweiten aus)
        Messband.Schritt(z, 2, "right", true, V(0, 1, 0), Fang(V(0, 2.6, 0), Fangart.Flaeche, decke), "begehen");
        var m2 = Assert.Single(Messband.Schritt(z, 2.5, "right", false, V(0.3, 1.3, 0), Fang(V(2, 0.5, 0)), "begehen")).Messung!;
        Nah(m2.Mass.AbstandMm, 2100, 1e-9);
    }

    [Fact]
    public void Liste_entfernen_löschen_höchstens_50_und_Wischgeste()
    {
        var z = new MessZustand();
        Messband.AnSetzen(z, true);
        for (int i = 0; i < Messband.MaxMessungen + 3; i++)
            Messband.Fertig(z, new MessPunkt(V(i, 0, 0), Fangart.Ecke), new MessPunkt(V(i, 1, 0), Fangart.Ecke), "begehen", i, out _);
        Assert.Equal(Messband.MaxMessungen, z.Liste.Count);
        Assert.Equal(4, z.Liste[0].Nr);   // die ältesten drei fielen weg
        Assert.Equal(2, Messband.Entfernen(z, [4, 5, 999]));
        Assert.Equal(Messband.MaxMessungen - 2, z.Liste.Count);
        // Wischgeste: ein waagrechter Weg quer durch die Linie der Messung Nr 10 (x = 9, y 0 … 1)
        var linien = z.Liste.Select(m => (m.Nr, m.A.P, m.B.P)).ToList();
        var weg = new List<Vec3> { V(8.8, 0.5, 0.3), V(9.2, 0.5, -0.3) };
        Assert.Equal([10], Messband.Getroffen(weg, linien));
        Assert.Empty(Messband.Getroffen([V(100, 0, 0), V(101, 0, 0)], linien));
        Assert.Equal(Messband.MaxMessungen - 2, Messband.Loeschen(z));
        Assert.Empty(z.Liste);
    }
}
