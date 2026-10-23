// Quest App P5a: Etagenwechsel mit Wisch hoch/runter bzw. der App «Etagen» – Begehen (gleicher Punkt, sonst grösster Raum) und Sitzungstisch.

using Hlks.Xr.Modell;
using Hlks.Xr.Netz;
using static Hlks.Xr.Tests.ModellHilfen;
using static Hlks.Xr.Tests.T;

namespace Hlks.Xr.Tests;

public class EtagenWechselTests
{
    static ListenGeschoss G(string id, string kz, double kote) => new(id, kz, null, kote, null, new ListenTeil("aaaaaaaa", $"/api/xr/geschoss/{id}.glb?teil=modell", null), null);

    static readonly ListenGeschoss Ug = G("g-ug", "UG", -2700), Eg = G("g-eg", "EG", 0), EgB = G("g-egb", "EG Haus B", 0.5), Og = G("g-og", "1.OG", 2750),
        Og2 = G("g-og2", "2.OG", 5500);
    static readonly List<ListenGeschoss> Alle = [Og2, Eg, Ug, Og, EgB];   // Reihenfolge der Liste spielt keine Rolle

    static Xz[] Rechteck(double x0, double z0, double x1, double z1) => [new(x0, z0), new(x1, z0), new(x1, z1), new(x0, z1)];

    /// <summary>EG: R-EG-1 (4 × 4 m) und R-EG-2 (7 × 6 m); 1.OG: R-OG-1 über R-EG-1, R-OG-2 (7 × 6 m, der grösste); UG: ein Raum; 2.OG ohne Räume.</summary>
    static ModellSzene Szene(double[]? bezug = null, double ogRaumBoden = 2.75)
    {
        var b = bezug ?? [0, 0, 0];
        var bz = b[2] / 1000;
        return new ModellSzene(
        [
            (Datei(b, RaumNetz("R-UG", Rechteck(0, -10, 10, 0), -2.7 - bz, 2.5)), Ug.Id, "modell"),
            (Datei(b, RaumNetz("R-EG-1", Rechteck(0, -4, 4, 0), 0 - bz, 2.6), RaumNetz("R-EG-2", Rechteck(5, -6, 12, 0), 0 - bz, 2.6)), Eg.Id, "modell"),
            (Datei(b, RaumNetz("R-OG-1", Rechteck(0, -4, 4, 0), ogRaumBoden - bz, 2.6), RaumNetz("R-OG-2", Rechteck(5, -6, 12, 0), 2.75 - bz, 2.6)), Og.Id, "modell"),
            (Datei(b, Quadernetz("architektur", ("W", "wand", V(0, 5.5 - bz, -1), V(3, 8 - bz, 0)))), Og2.Id, "modell"),
        ]);
    }

    [Fact]
    public void Nachbar_nach_der_Kote()
    {
        Assert.Equal(Og, EtagenWechsel.Nachbar(Alle, Eg, 1));
        Assert.Equal(Ug, EtagenWechsel.Nachbar(Alle, Eg, -1));
        Assert.Equal(Og, EtagenWechsel.Nachbar(Alle, EgB, 1));     // 0.5 mm über dem EG: dasselbe Geschoss (Haus B), weiter ins 1.OG
        Assert.Null(EtagenWechsel.Nachbar(Alle, Og2, 1));          // oberstes
        Assert.Null(EtagenWechsel.Nachbar(Alle, Ug, -1));          // unterstes
        Assert.Equal(Ug, EtagenWechsel.Nachbar(Alle, null, 1));
        Assert.Equal(Og2, EtagenWechsel.Nachbar(Alle, null, -1));
        Assert.Null(EtagenWechsel.Nachbar([], Eg, 1));
        Assert.Null(EtagenWechsel.Nachbar(Alle, Eg, 0));
    }

    [Fact]
    public void Begehen_ueber_dem_gleichen_Punkt()
    {
        var s = Szene();
        var z = EtagenWechsel.Ziel(s, Alle, Og, new Xz(2, -2));   // im EG in R-EG-1, darüber liegt R-OG-1
        Assert.Equal(EtagenWechsel.GleicherPunkt, z.Art);
        Assert.Equal("R-OG-1", z.Raum);
        Assert.Equal(new Xz(2, -2), z.Punkt);
        Nah(z.Boden, 2.75, 1e-6);
        Assert.Equal(Og.Id, GeschossWahl.AmStand(Alle, GeschossWahl.StandbodenMm(z.Boden, s.Bezugspunkt))!.Id);   // man steht dann im 1.OG
        // nach unten ins UG: der Punkt liegt im Raum des UG
        var u = EtagenWechsel.Ziel(s, Alle, Ug, new Xz(2, -2));
        Assert.Equal(EtagenWechsel.GleicherPunkt, u.Art);
        Nah(u.Boden, -2.7, 1e-6);
    }

    [Fact]
    public void Begehen_sonst_im_groessten_Raum()
    {
        var s = Szene();
        var z = EtagenWechsel.Ziel(s, Alle, Og, new Xz(20, 20));   // ausserhalb aller Räume des 1.OG
        Assert.Equal(EtagenWechsel.GroessterRaum, z.Art);
        Assert.Equal("R-OG-2", z.Raum);
        Nah(z.Punkt.X, 8.5, 1e-6);
        Nah(z.Punkt.Z, -3, 1e-6);   // Schwerpunkt
        Nah(z.Boden, 2.75, 1e-6);
    }

    [Fact]
    public void Begehen_ohne_Raeume_auf_der_Kote_und_mit_Bezugspunkt()
    {
        var s = Szene();
        var z = EtagenWechsel.Ziel(s, Alle, Og2, new Xz(2, -2));
        Assert.Equal(EtagenWechsel.Geschosskote, z.Art);
        Assert.Null(z.Raum);
        Assert.Equal(new Xz(2, -2), z.Punkt);
        Nah(z.Boden, 5.5, 1e-9);
        // Bezugspunkt 1000 mm über null: Koten und Böden relativ dazu
        var sb = Szene([0, 0, 1000]);
        Nah(EtagenWechsel.Ziel(sb, Alle, Og2, new Xz(2, -2)).Boden, 4.5, 1e-9);
        var mit = EtagenWechsel.Ziel(sb, Alle, Og, new Xz(2, -2));
        Assert.Equal(EtagenWechsel.GleicherPunkt, mit.Art);
        Nah(mit.Boden, 1.75, 1e-6);
    }

    [Fact]
    public void Ein_zu_tiefer_Raumboden_gibt_die_Kote()
    {
        // R-OG-1 liegt 45 cm unter der Kote des 1.OG: auf seinem Boden wäre man nach GeschossWahl (0.3 m) im EG – dann gilt die Kote
        var s = Szene(ogRaumBoden: 2.30);
        var z = EtagenWechsel.Ziel(s, Alle, Og, new Xz(2, -2));
        Assert.Equal("R-OG-1", z.Raum);
        Nah(z.Boden, 2.75, 1e-9);
    }

    [Fact]
    public void Sitzungstisch_Etagen_und_Alle_Etagen()
    {
        Assert.Equal(new SitzungEtagenZiel(Og2.Id), EtagenWechsel.Sitzung(Alle, null, true, -1));   // von «Alle Etagen» nach unten: das oberste
        Assert.Null(EtagenWechsel.Sitzung(Alle, null, true, 1));                                    // über «Alle Etagen» nichts
        Assert.True(EtagenWechsel.Sitzung(Alle, Og2.Id, false, 1)!.Alle);                           // über dem obersten: «Alle Etagen»
        Assert.Equal(new SitzungEtagenZiel(Og.Id), EtagenWechsel.Sitzung(Alle, Eg.Id, false, 1));
        Assert.Equal(new SitzungEtagenZiel(Ug.Id), EtagenWechsel.Sitzung(Alle, Eg.Id, false, -1));
        Assert.Null(EtagenWechsel.Sitzung(Alle, Ug.Id, false, -1));                                 // unter dem untersten nichts
        Assert.True(EtagenWechsel.Sitzung(Alle, "unbekannt", false, 1)!.Alle);
        Assert.Equal(new SitzungEtagenZiel(Og2.Id), EtagenWechsel.Sitzung(Alle, "unbekannt", false, -1));
        Assert.Null(EtagenWechsel.Sitzung([], Eg.Id, false, 1));
    }
}
